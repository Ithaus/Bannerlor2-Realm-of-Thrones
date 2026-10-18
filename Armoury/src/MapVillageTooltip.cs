using System;
using System.Collections.Generic;
using System.Reflection;
using Armoury.Villages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>E7/W2 (233): dane jednego dymka wioski - klucz typu w rejestrze dymkow gry (InformationManager.RegisterTooltip) i gotowe linie (budowane raz
    /// na najechanie w MapVillagesView.Visual.OnHover; MapScreen.OnHoverMapEntity wola OnHover tylko przy zmianie najechanego obiektu).</summary>
    public sealed class MapVillageTip
    {
        internal const byte Always = 0, OnlyExtended = 1, OnlyShort = 2;

        internal struct Line
        {
            public string Text;                                // null przy OnlyShort = wiersz gry "Hold Alt for more info" (str_map_tooltip_info)
            public int Height;
            public TooltipProperty.TooltipPropertyFlags Flags;
            public uint Color;                                 // kolor rodu (tylko zapas bez herbu); 0 = zwykly tekst
            public byte When;
        }

        internal readonly List<Line> Lines = new List<Line>(14);
        internal Banner Banner;                                // herb pana (Clan.Banner - dla rodu panujacego herb krolestwa, jak przy partiach gry)
        internal int Mode = 1;                                 // tlo jak dymek osady gry: 3 wojna z graczem, 2 krolestwo gracza, 1 obce
        internal string Uid = "";

        internal void Add(string text, TooltipProperty.TooltipPropertyFlags flags = TooltipProperty.TooltipPropertyFlags.None, byte when = Always, int height = 0, uint color = 0)
        {
            Lines.Add(new Line { Text = text, Flags = flags, When = when, Height = height, Color = color });
        }
    }

    /// <summary>VM dymka wioski: dymek gry (PropertyBasedTooltipVM - lista linii, tlo sojusznik / wrog, Alt, odswiezanie) + herb pana nad lista.
    /// Konstruktor (Type, object[]) publiczny - wymagany przez Activator.CreateInstance w GauntletInformationView.OnShowTooltip; konstruktor bazy wola od razu
    /// Refresh, wiec Banner ustawia callback jeszcze w konstruktorze (pola bez inicjalizatorow).</summary>
    public sealed class MapVillageTooltipVM : PropertyBasedTooltipVM
    {
        private BannerImageIdentifierVM _banner;
        private bool _hasBanner;

        public MapVillageTooltipVM(Type invokedType, object[] invokedArgs) : base(invokedType, invokedArgs) { }

        [DataSourceProperty]
        public BannerImageIdentifierVM Banner
        {
            get { return _banner; }
            set { if (value != _banner) { _banner = value; OnPropertyChangedWithValue(value, "Banner"); } }
        }

        [DataSourceProperty]
        public bool HasBanner
        {
            get { return _hasBanner; }
            set { if (value != _hasBanner) { _hasBanner = value; OnPropertyChangedWithValue(value, "HasBanner"); } }
        }

        internal void SetBanner(Banner b)
        {
            if (b == null) { HasBanner = false; return; }
            if (_banner == null || _banner.Id != b.BannerCode)
            {
                var old = _banner;
                Banner = new BannerImageIdentifierVM(b, false);   // kwadrat herbu (nineGrid = ksztalt choragwi)
                if (old != null) old.OnFinalize();
            }
            HasBanner = true;
        }

        protected override void OnFinalizeInternal()
        {
            base.OnFinalizeInternal();
            if (_banner != null) _banner.OnFinalize();
        }
    }

    /// <summary>
    /// E7/W2 (233, PLAN 7.9 a, R11/R12; projekt etapu 7 v2 rozdz. 3 "E7/W2") - DYMEK WIOSKI Z HERBEM PANA, RYCERZEM, WIARA BK I PO ALT ZAJECIEM, HISTORIA, PAMIECIA WOJNY.
    /// Zwykly dymek gry nie umie obrazka (TooltipProperty: tekst, kolor, flagi; prefab PropertyBasedTooltip = same RichTextWidget), wiec wlasny typ dymka:
    /// RegisterTooltip&lt;MapVillageTip, MapVillageTooltipVM&gt;(cb, "ArmMapVillageTooltip") raz na proces (SubModuleMain.OnBeforeInitialModuleScreenSetAsRoot) i wlasny
    /// prefab Modules\Armoury\GUI\Prefabs\ArmMapVillageTooltip.xml (kopia ROT PropertyBasedTooltip.xml + herb 52x52; UIResourceManager dodaje \GUI\ kazdego modulu
    /// przy starcie gry) - bez UIExtenderEx i bez ruszania prefabow gry, ROT i ForgeView. PULAPKA: TooltipBaseVM.InvokeRefreshData&lt;T&gt; sprawdza
    /// "OnRefreshData is Action&lt;T, object[]&gt;" z T = PropertyBasedTooltipVM - delegat musi byc Action&lt;PropertyBasedTooltipVM, object[]&gt; (kontrawariancja pozwala
    /// go podac), inaczej pusty dymek bez bledu. Blad prefabu = GauntletInformationView lapie wyjatek (brak dymka, nie wywrotka); brak pliku prefabu = brak rejestracji.
    /// Zapas (rejestracja nieudana albo brak prefabu): dymek gry z tymi samymi liniami (Alt przez flagi OnlyShowWhenExtended), linia pana w kolorze rodu zamiast herbu.
    /// Klucz VillageTipLore = off: dymek jak dotad (MapVillagesView.Visual.OnHover nie wola Show).
    /// </summary>
    internal static class MapVillageTooltip
    {
        internal const string Movie = "ArmMapVillageTooltip";
        private static bool _registered, _tried;
        private static string _status = "nie rejestrowany";
        private static int _stBuild, _stRefresh;                       // potkniecia (sesja)
        private static int _dTip, _dFallback, _dStBuild, _dStRefresh;  // doba

        internal static string StatusText { get { return _status; } }

        internal static void ResetSession() { _stBuild = _stRefresh = 0; DayReset(); }
        internal static void DayReset() { _dTip = _dFallback = _dStBuild = _dStRefresh = 0; }

        internal static string DayText()
        {
            return "dymkow z herbem " + _dTip + ", zapas bez herbu " + _dFallback + " (" + (_registered ? "dymek zarejestrowany" : "BEZ rejestracji: " + _status)
                   + ") | potkniecia dymka dzis: budowa " + _dStBuild + ", odswiezanie " + _dStRefresh + " (sesja " + _stBuild + "/" + _stRefresh + ")";
        }

        /// <summary>Plik w module Armoury (Modules\Armoury\...) - jak MapVillageData.DataPath: od DLL dwa katalogi w gore, zapas BasePath.</summary>
        internal static string ModulePath(params string[] parts)
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);   // Modules\Armoury\bin\Win64_Shipping_Client
                string p = System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, "..", ".."));
                foreach (string s in parts) p = System.IO.Path.Combine(p, s);
                if (System.IO.File.Exists(p)) return p;
            }
            catch { }
            try
            {
                string p = System.IO.Path.Combine(BasePath.Name, "Modules", "Armoury");
                foreach (string s in parts) p = System.IO.Path.Combine(p, s);
                return p;
            }
            catch { return null; }
        }

        /// <summary>Raz na proces (SubModuleMain.OnBeforeInitialModuleScreenSetAsRoot, wlasny try i metoda NoInlining). Bez pliku prefabu - bez rejestracji (zapas).</summary>
        internal static void Register()
        {
            if (_tried) return;
            _tried = true;
            try
            {
                string prefab = ModulePath("GUI", "Prefabs", Movie + ".xml");
                if (prefab == null || !System.IO.File.Exists(prefab))
                {
                    _status = "BRAK prefabu GUI\\Prefabs\\" + Movie + ".xml w module Armoury - zapas (dymek gry bez herbu)";
                    Log.Info("Dymek wioski (E7/W2): " + _status + ".");
                    return;
                }
                Action<PropertyBasedTooltipVM, object[]> cb = Refresh;   // PULAPKA delegata (opis klasy) - nie Action<MapVillageTooltipVM, object[]>
                InformationManager.RegisterTooltip<MapVillageTip, MapVillageTooltipVM>(cb, Movie);
                _registered = true;
                _status = "zarejestrowany";
                Log.Info("Dymek wioski (E7/W2): dymek " + Movie + " zarejestrowany (prefab " + prefab + ").");
            }
            catch (Exception e) { _registered = false; _status = "rejestracja nieudana (" + e.GetType().Name + ") - zapas"; Log.Error("MapVillageTooltip.Register", e); }
        }

        internal static void Unregister()
        {
            try { if (_registered) InformationManager.UnregisterTooltip<MapVillageTip>(); } catch { }
            _registered = false;
        }

        /// <summary>Dymek wioski z linii widoku (te same co dzisiejszy dymek: tytul, pan, ludzie, stan, ogien) + rycerz, wiara, po Alt lore i pamiec wojny.
        /// false - blad budowy (widok pokazuje wtedy dzisiejszy dymek); blad liczony, nic nie gasi.</summary>
        internal static bool Show(string uid, string name, Settlement district, string held, bool heldIsLord, string people, string state, string fire)
        {
            MapVillageTip t;
            try { t = Build(uid, name, district, held, heldIsLord, people, state, fire); }
            catch (Exception e)
            {
                _stBuild++; _dStBuild++;
                if (_stBuild <= 3 || _stBuild % 200 == 0) Log.Error("MapVillageTooltip.Build " + uid + " (potkniecie " + _stBuild + ")", e);
                return false;
            }
            if (_registered)
            {
                _dTip++;
                InformationManager.ShowTooltip(typeof(MapVillageTip), new object[] { t });
                return true;
            }
            // zapas: dymek gry z liniami (RefreshGenericPropertyBasedTooltip filtruje po OnlyShowWhenExtended / OnlyShowWhenNotExtended), pan w kolorze rodu
            bool longForm = LongForm();
            var list = new List<TooltipProperty>(t.Lines.Count);
            foreach (var l in t.Lines)
            {
                if (l.When == MapVillageTip.OnlyShort && longForm) continue;
                string text = l.Text;
                if (text == null)
                {
                    GameTexts.SetVariable("EXTEND_KEY", GameTexts.FindText("str_game_key_text", "anyalt").ToString());   // klawiatura - jak GauntletInformationView
                    text = GameTexts.FindText("str_map_tooltip_info").ToString();
                }
                var p = l.Color != 0
                    ? new TooltipProperty("", text, l.Height, Color.FromUint(l.Color | 0xFF000000u), false, l.Flags)   // kolor rodu zawsze nieprzezroczysty
                    : new TooltipProperty("", text, l.Height, false, l.Flags);
                if (l.When == MapVillageTip.OnlyExtended && !longForm) p.OnlyShowWhenExtended = true;
                if (l.When == MapVillageTip.OnlyShort) p.OnlyShowWhenNotExtended = true;
                list.Add(p);
            }
            _dFallback++;
            InformationManager.ShowTooltip(typeof(List<TooltipProperty>), new object[] { list });
            return true;
        }

        private static bool LongForm()
        {
            try { return Campaign.Current != null && Campaign.Current.IsMapTooltipLongForm; } catch { return false; }
        }

        /// <summary>Linie dymka. Kolejnosc: nazwa (herb nad nia w prefabie), pan, rycerz, ludzie, stan, ogien, wiara BK; po Alt: zajecie, zdanie o swietym miejscu
        /// (faith_bk = dominujaca wiara BK), historia, pamiec wojny (W1; bez niej war_lore z ksiazek). Bez lore (klucz, bramka) - bez linii lore, reszta jak wyzej.</summary>
        private static MapVillageTip Build(string uid, string name, Settlement district, string held, bool heldIsLord, string people, string state, string fire)
        {
            var t = new MapVillageTip { Uid = uid ?? "" };
            Settlement bound = district != null && district.Village != null ? district.Village.Bound : null;
            Clan lord = bound != null ? bound.OwnerClan : null;    // pan jak HeldLine (MapVillagesView)
            t.Banner = lord != null ? lord.Banner : null;
            // Jeff 16.10 (D1, VillageBannerPerHolder): herb i linia GOSPODARZA wioski (VillageHolders - ten sam co proporczyk i okno okregu); null - jak dotad (pan)
            VillageHolders.Holder holder = null;
            if (Settings.Current.VillageBannerPerHolder)
            {
                holder = VillageHolders.Of(district, uid);
                if (holder != null && holder.Banner != null) t.Banner = holder.Banner;
            }
            t.Mode = ModeOf(district);
            t.Add(name, TooltipProperty.TooltipPropertyFlags.Title);
            t.Add(held, TooltipProperty.TooltipPropertyFlags.None, MapVillageTip.Always, 0, heldIsLord && lord != null ? lord.Color : 0u);
            MapVillageLore.Row row = null;
            string knight = null;
            try
            {
                row = MapVillageLore.Of(uid);
                knight = holder != null ? VillageHolders.Line(holder) : MapVillageLore.KnightLine(row, district, lord);
            }
            catch (Exception e) { MapVillageLore.CountRowStumble(uid, e); }
            if (knight != null) t.Add(knight);
            t.Add(people);
            string est = DailyLife.SideTipLine(district);          // ZC2: sklad stanow okregu i Dostatek (null - klucz DailyLifeEstates off, przed pomiarem)
            if (est != null) t.Add(est);
            if (state != null) t.Add(state);
            if (fire != null) t.Add(fire);
            // inwest (ui_text 5): co robi wioska, jaki to procent produkcji okregu, prace (wszystkie i gracza), po ogniu - strata i odbudowa; "Kit in place" po Alt
            foreach (string wl in DistrictWorks.PartLines(district, uid, true))
                t.Add(wl, TooltipProperty.TooltipPropertyFlags.MultiLine, wl.StartsWith("Kit in place", StringComparison.Ordinal) ? MapVillageTip.OnlyExtended : MapVillageTip.Always);
            string bkId;
            string faith = MapVillageLore.FaithLine(district, out bkId);
            if (faith != null) t.Add(faith);
            // po Alt (albo przy dlugich dymkach gry - IsMapTooltipLongForm)
            var more = new List<string>(4);
            if (row != null)
            {
                // GL-5 (P5): wies podmieniona na len w tej sesji - zdanie zajecia starego typu (plik lore z typow mapy) -> len (GoodsRates.Livelihood; inne bez zmian)
                string live = row.Livelihood.Length > 0 ? VineOlive.Livelihood(district, GoodsRates.Livelihood(district, uid, row.Livelihood)) : row.Livelihood;   // B6: winnica z gaju / palm
                if (!string.IsNullOrEmpty(live)) more.Add(live);
                if (faith != null && row.Faith.Length > 0 && row.FaithMatches(bkId)) more.Add(row.Faith);
                if (row.History.Length > 0) more.Add(row.History);
            }
            string mem = VillageNews.MemoryText(uid);
            if (mem == null && row != null && row.WarLore.Length > 0) mem = row.WarLore;
            if (mem != null) more.Add(mem);
            if (more.Count > 0)
            {
                t.Add("", TooltipProperty.TooltipPropertyFlags.DefaultSeperator, MapVillageTip.OnlyExtended);
                foreach (string s in more) t.Add(s, TooltipProperty.TooltipPropertyFlags.MultiLine, MapVillageTip.OnlyExtended);
                t.Add("", TooltipProperty.TooltipPropertyFlags.None, MapVillageTip.OnlyShort, -1);   // odstep jak w dymkach gry (TooltipRefresherCollection)
                t.Add(null, TooltipProperty.TooltipPropertyFlags.None, MapVillageTip.OnlyShort);     // "Hold Alt for more info"
            }
            return t;
        }

        /// <summary>Tlo dymka jak RefreshSettlementTooltip gry: 3 wojna z frakcja gracza, 2 ta sama frakcja, 1 obca.</summary>
        private static int ModeOf(Settlement district)
        {
            try
            {
                IFaction f = district != null ? district.MapFaction : null;
                IFaction mine = MobileParty.MainParty != null ? MobileParty.MainParty.MapFaction : null;
                if (f == null || mine == null) return 1;
                if (FactionManager.IsAtWarAgainstFaction(f, mine)) return 3;
                return ReferenceEquals(f, mine) ? 2 : 1;
            }
            catch { return 1; }
        }

        /// <summary>Callback rejestru dymkow (konstruktor VM i kazda zmiana Alt): linie z MapVillageTip wedlug IsExtended, tlo, herb.</summary>
        private static void Refresh(PropertyBasedTooltipVM vm, object[] args)
        {
            var t = args != null && args.Length > 0 ? args[0] as MapVillageTip : null;
            if (vm == null || t == null) return;
            try
            {
                bool ext = vm.IsExtended || LongForm();
                vm.Mode = t.Mode;
                foreach (var l in t.Lines)
                {
                    if (l.When == MapVillageTip.OnlyExtended && !ext) continue;
                    if (l.When == MapVillageTip.OnlyShort && ext) continue;
                    string text = l.Text;
                    if (text == null)
                    {
                        GameTexts.SetVariable("EXTEND_KEY", vm.GetKeyText("ExtendModifier"));   // jak dymki partii gry (TooltipRefresherCollection)
                        text = GameTexts.FindText("str_map_tooltip_info").ToString();
                    }
                    vm.AddProperty("", text, l.Height, l.Flags);
                }
                var me = vm as MapVillageTooltipVM;
                if (me != null) me.SetBanner(t.Banner);
            }
            catch (Exception e)
            {
                _stRefresh++; _dStRefresh++;
                if (_stRefresh <= 3 || _stRefresh % 200 == 0) Log.Error("MapVillageTooltip.Refresh " + t.Uid + " (potkniecie " + _stRefresh + ")", e);
            }
        }
    }
}
