using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;
using Bannerlord.UIExtenderEx.ViewModels;
using BannerKings.UI.Crafting;
using BannerKings.UI.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace ForgeView
{
    /// <summary>
    /// m109-kuznia (Jeff 23.10: "dorobic te liczby na ekranie kucia, aby bylo widac: mamy 3 drewna, czyli 300 kg, i 87 kg w pamieci - jako liczba nad ikonkami
    /// metali; to wszystko musi byc widoczne, aby gracz dokladnie wiedzial, czy moze kuc"). JEDEN rzad rachunku nad paskiem surowcow - ten sam na ekranie broni
    /// gry i w zakladce CRAFT BK (pancerze, luki, kusze, amunicja, tarcze). Kafel: potrzeba (czerwona przy braku), sklad "3x100 +87", ikona surowca (obraz
    /// przedmiotu - ItemImageIdentifierVM, jak MaterialRowExtension; bez nowych sprite'ow), plakietka "387 kg", podpowiedz po najechaniu.
    /// Liczby liczy Armoury.ForgeBill (refleksja, jak PatternGate) - ta sama funkcja, ktora zuzywa. Bez Armoury albo przy kluczu NIE: null = rzad pusty,
    /// rzad BK (skora, len) jak dotad. Odswiezenie: postfiks BK CraftingMixin.OnRefresh (BkMaterialsModeFix.Sync) - biegnie po kazdym UpdateAll gry
    /// (UpdateCraftingStamina), wyborze kafla CRAFT, kuciu i zmianie trybu; kafle uzywane ponownie, gdy zestaw surowcow ten sam (bez nowych miniatur).
    /// Recenzja KUZ-2: mixin BK przypisany do ekranu przez prywatne pole BK CraftingMixin.crafting; bez niego (inna wersja BK) - do biezacego ekranu (ekran
    /// kucia jest jeden), zamiast pustego rzedu. Recenzja KUZ-3: bledy rzadu ida takze do logu Armoury (ScreenNote "ForgeView blad ...") - ForgeView.log jest
    /// nadpisywany przy starcie i autotest go nie zbiera.
    /// </summary>
    [ViewModelMixin]
    internal sealed class ForgeBillMixin : BaseViewModelMixin<CraftingVM>
    {
        internal static ForgeBillMixin Current;
        private static CraftingMixin _lastBk;      // ostatni mixin BK z odswiezenia (kolejnosc tworzenia mixinow BK / nasz nie jest pewna)

        private readonly CraftingVM _vm;
        private CraftingMixin _bk;
        private MBBindingList<FvBillTileVM> _rows = new MBBindingList<FvBillTileVM>();
        private bool _shown;
        private int _mode = -1;                    // ostatnie odswiezenie: 0 bron, 1 CRAFT, 2 inne (wytop, rafinacja)

        private static AccessTools.FieldRef<CraftingMixin, CraftingVM> _fBkVm;
        private static AccessTools.FieldRef<CraftingVM, Crafting> _fCrafting;
        private static bool _fLooked;
        private static int _errors;

        public ForgeBillMixin(CraftingVM vm) : base(vm)
        {
            _vm = vm;
            Current = this;
            try
            {
                ForgeBillLink.Ensure();
                var last = _lastBk;
                if (last != null && SameVm(last, vm)) Refresh(last, false);   // mixin BK tego ekranu powstal pierwszy - jego odswiezenie juz bylo
            }
            catch (Exception e) { Error("ForgeBillMixin.ctor", e); }
        }

        public override void OnFinalize()
        {
            try
            {
                if (ReferenceEquals(Current, this)) Current = null;
                if (_lastBk != null && SameVm(_lastBk, _vm)) _lastBk = null;
                _bk = null;
            }
            catch { }
            base.OnFinalize();
        }

        [DataSourceProperty]
        public MBBindingList<FvBillTileVM> FvBillRows
        {
            get { return _rows; }
            set { if (value != _rows) { _rows = value; ViewModel.OnPropertyChangedWithValue(value, "FvBillRows"); } }
        }

        [DataSourceProperty]
        public bool FvBillShown
        {
            get { return _shown; }
            set { if (value != _shown) { _shown = value; ViewModel.OnPropertyChangedWithValue(value, "FvBillShown"); } }
        }

        private static void Error(string where, Exception e)
        {
            _errors++;
            if (_errors <= 3 || _errors % 100 == 0)
            {
                Log.Error(where + " (blad " + _errors + ")", e);
                ForgeBillLink.Note("ForgeView blad " + where + " (blad " + _errors + "): " + (e != null ? e.GetType().Name + ": " + e.Message : "null"));
            }
        }

        private static void Look()
        {
            if (_fLooked) return;
            _fLooked = true;
            try { _fBkVm = AccessTools.FieldRefAccess<CraftingMixin, CraftingVM>("crafting"); } catch (Exception e) { _fBkVm = null; Error("ForgeBillMixin.Look(crafting)", e); }
            try { _fCrafting = AccessTools.FieldRefAccess<CraftingVM, Crafting>("_crafting"); } catch (Exception e) { _fCrafting = null; Error("ForgeBillMixin.Look(_crafting)", e); }
        }

        /// <summary>Mixin BK nalezy do ekranu vm. Pole BK nieznane (inna wersja BK) - tak (ekran kucia jest jeden naraz), zamiast pustego rzedu (recenzja KUZ-2).</summary>
        private static bool SameVm(CraftingMixin m, CraftingVM vm)
        {
            if (m == null) return false;
            Look();
            return _fBkVm == null || ReferenceEquals(_fBkVm(m), vm);
        }

        private static WeaponDesign DesignOf(CraftingVM vm)
        {
            Look();
            var c = vm != null && _fCrafting != null ? _fCrafting(vm) : null;
            return c != null ? c.CurrentWeaponDesign : null;
        }

        /// <summary>BkMaterialsModeFix (po OnRefresh BK i po zmianie IsInArmorMode): odswiezenie rzadu tego ekranu. true = rachunek czynny (klucz TAK) - rzad BK
        /// ma byc pusty w obu trybach; false = jak dotad. modeOnly: setter IsInArmorMode - tylko przy zmianie trybu (BK ustawia go na poczatku kazdego OnRefresh).</summary>
        internal static bool Sync(CraftingMixin m, bool modeOnly)
        {
            _lastBk = m;
            var cur = Current;
            if (cur != null && SameVm(m, cur._vm)) return cur.Refresh(m, modeOnly);
            return ForgeBillLink.Rows(null, null) != null;   // nasz mixin tego ekranu jeszcze nie powstal - sam klucz
        }

        private bool Refresh(CraftingMixin m, bool modeOnly)
        {
            try
            {
                _bk = m;
                object[] rows;
                int mode;
                if (m != null && m.IsInArmorMode)
                {
                    mode = 1;
                    if (modeOnly && mode == _mode) return true;
                    var ac = m.ArmorCrafting;
                    var cur = ac != null ? ac.CurrentItem : null;
                    rows = ForgeBillLink.Rows(cur != null ? cur.Item : null, null);
                }
                else if (_vm.IsInCraftingMode)
                {
                    mode = 0;
                    if (modeOnly && mode == _mode) return true;
                    var d = DesignOf(_vm);
                    rows = d != null ? ForgeBillLink.Rows(null, d) : ForgeBillLink.Rows(null, null);
                }
                else
                {
                    mode = 2;
                    rows = ForgeBillLink.Rows(null, null);   // wytop i rafinacja - bez rachunku kucia
                }
                if (rows == null)
                {
                    // klucz NIE albo blad po stronie Armoury - rzad pusty, rzad BK jak dotad
                    if (_rows.Count > 0) _rows.Clear();
                    FvBillShown = false;
                    _mode = -1;
                    return false;
                }
                _mode = mode;
                Apply(rows);
                return true;
            }
            catch (Exception e) { Error("ForgeBillMixin.Refresh", e); return false; }
        }

        /// <summary>Kafle: ten sam zestaw surowcow w tej samej kolejnosci - tylko teksty; inny - lista od nowa.</summary>
        private void Apply(object[] rows)
        {
            bool same = rows.Length == _rows.Count;
            for (int i = 0; same && i < rows.Length; i++)
            {
                var r = rows[i] as object[];
                if (r == null || r.Length < 6 || !ReferenceEquals(r[0], _rows[i].Material)) same = false;
            }
            if (!same)
            {
                _rows.Clear();
                foreach (var o in rows)
                {
                    var r = o as object[];
                    if (r == null || r.Length < 6 || !(r[0] is ItemObject)) continue;
                    _rows.Add(new FvBillTileVM((ItemObject)r[0]));
                }
            }
            int k = 0;
            foreach (var o in rows)
            {
                var r = o as object[];
                if (r == null || r.Length < 6 || !(r[0] is ItemObject)) continue;
                if (k >= _rows.Count) break;
                _rows[k++].Set(r[1] as string, r[2] as string, r[3] as string, r[4] is bool && (bool)r[4], r[5] as string);
            }
            FvBillShown = _rows.Count > 0;
        }

        private string TilesText()
        {
            var l = new List<string>();
            foreach (var t in _rows) l.Add((t.Material != null ? t.Material.StringId : "?") + "|" + t.FvNeed + "|" + t.FvHave + "|" + (t.FvShort ? "brak" : "jest"));
            return l.Count > 0 ? string.Join("; ", l.ToArray()) : "pusto";
        }

        private static string RowsText(object[] rows)
        {
            if (rows == null) return "null";
            var l = new List<string>();
            foreach (var o in rows)
            {
                var r = o as object[];
                if (r == null || r.Length < 6) continue;
                l.Add((r[0] is ItemObject ? ((ItemObject)r[0]).StringId : "?") + "|" + (r[1] as string) + "|" + (r[2] as string) + "|" + ((r[4] is bool && (bool)r[4]) ? "brak" : "jest"));
            }
            return l.Count > 0 ? string.Join("; ", l.ToArray()) : "pusto";
        }

        /// <summary>Autotest (Armoury.ForgeBillTest przez refleksje, tylko pod plikiem armoury-kuznia.flag): 0 - bron z ekranu gry, 1..3 - CRAFT BK z przedmiotem
        /// itemId (zbroja, luk, strzaly), 9 - wyjscie z ekranu (ExecuteCancel). Zwraca "tryb id | ekran: ... | rachunek: ... | zgodne TAK|NIE" - kafle VM wobec
        /// rachunku policzonego od nowa ta sama funkcja (Armoury.ForgeBill.ScreenRows), albo "BLAD: ...".</summary>
        public static string TestStep(int step, string itemId)
        {
            try
            {
                var cur = Current;
                if (cur == null) return "BLAD: ekran kucia nie otwarty (brak mixinu)";
                var bk = cur._bk ?? (_lastBk != null && SameVm(_lastBk, cur._vm) ? _lastBk : null);
                if (step == 9) { cur._vm.ExecuteCancel(); return "zamkniety"; }
                if (step == 0)
                {
                    if (bk != null && bk.IsInArmorMode) cur._vm.ExecuteSwitchToCrafting();
                    else if (bk != null) bk.OnRefresh();
                }
                else
                {
                    if (bk == null) return "BLAD: brak mixinu BK (CraftingMixin)";
                    var item = MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObject<ItemObject>(itemId ?? "") : null;
                    if (item == null) return "BLAD: brak przedmiotu " + itemId;
                    if (!bk.IsInArmorMode) bk.ExecuteSwitchToArmor();
                    if (!bk.IsInArmorMode) return "BLAD: zakladka CRAFT BK niedostepna (BK Smithing wylaczony?)";
                    var ac = bk.ArmorCrafting;
                    if (ac == null) return "BLAD: brak ArmorCrafting";
                    ArmorItemVM pick = null;
                    if (ac.Armors != null) foreach (var a in ac.Armors) if (a != null && a.Item == item) { pick = a; break; }
                    if (pick == null) pick = new ArmorItemVM(ac, item, ac.GetItemType(item));
                    bool same = ReferenceEquals(ac.CurrentItem, pick);
                    ac.CurrentItem = pick;   // BK: OnRefresh -> BkMaterialsModeFix.Sync -> Refresh (setter BK odswieza tylko przy zmianie)
                    if (same) bk.OnRefresh();
                }
                bk = cur._bk ?? bk;
                string mode;
                object[] fresh;
                if (bk != null && bk.IsInArmorMode)
                {
                    var it = bk.ArmorCrafting != null && bk.ArmorCrafting.CurrentItem != null ? bk.ArmorCrafting.CurrentItem.Item : null;
                    mode = "CRAFT " + (it != null ? it.StringId : "-");
                    fresh = ForgeBillLink.Rows(it, null);
                }
                else
                {
                    var d = DesignOf(cur._vm);
                    mode = "bron " + (d != null && d.Template != null ? d.Template.StringId : "-");
                    fresh = d != null ? ForgeBillLink.Rows(null, d) : null;
                }
                string screen = cur.TilesText(), bill = RowsText(fresh);
                bool ok = fresh != null && fresh.Length > 0 && screen == bill && cur.FvBillShown;
                return mode + " | ekran: " + screen + " | rachunek: " + bill + " | zgodne " + (ok ? "TAK" : "NIE");
            }
            catch (Exception e) { Error("ForgeBillMixin.TestStep", e); return "BLAD: wyjatek " + e.GetType().Name; }
        }
    }

    /// <summary>Kafel rachunku: surowiec, potrzeba, sklad, stan, podpowiedz.</summary>
    internal sealed class FvBillTileVM : ViewModel
    {
        internal readonly ItemObject Material;
        private ItemImageIdentifierVM _visual;
        private string _need = "", _have = "", _detail = "", _hintText;
        private bool _short;
        private HintViewModel _hint;

        public FvBillTileVM(ItemObject material)
        {
            Material = material;
            _visual = new ItemImageIdentifierVM(material, "");
            _hint = new HintViewModel();
        }

        internal void Set(string need, string have, string detail, bool isShort, string hint)
        {
            FvNeed = need ?? "";
            FvHave = have ?? "";
            FvDetail = detail ?? "";
            if (isShort != _short)
            {
                _short = isShort;
                OnPropertyChangedWithValue(_short, "FvShort");
                OnPropertyChangedWithValue(!_short, "FvOk");
            }
            if (hint != _hintText)
            {
                _hintText = hint;
                FvHint = new HintViewModel(new TextObject("{=!}" + (hint ?? "")));
            }
        }

        [DataSourceProperty]
        public ItemImageIdentifierVM FvVisual
        {
            get { return _visual; }
            set { if (value != _visual) { _visual = value; OnPropertyChangedWithValue(value, "FvVisual"); } }
        }

        [DataSourceProperty]
        public string FvNeed
        {
            get { return _need; }
            set { if (value != _need) { _need = value; OnPropertyChangedWithValue(value, "FvNeed"); } }
        }

        [DataSourceProperty]
        public string FvHave
        {
            get { return _have; }
            set { if (value != _have) { _have = value; OnPropertyChangedWithValue(value, "FvHave"); } }
        }

        [DataSourceProperty]
        public string FvDetail
        {
            get { return _detail; }
            set { if (value != _detail) { _detail = value; OnPropertyChangedWithValue(value, "FvDetail"); } }
        }

        [DataSourceProperty] public bool FvShort { get { return _short; } }
        [DataSourceProperty] public bool FvOk { get { return !_short; } }

        [DataSourceProperty]
        public HintViewModel FvHint
        {
            get { return _hint; }
            set { if (value != _hint) { _hint = value; OnPropertyChangedWithValue(value, "FvHint"); } }
        }
    }

    /// <summary>Most do Armoury.ForgeBill (refleksja, bez twardej zaleznosci): ScreenRows, ScreenNote. Jedna linia "PODPIETY/BRAK" (ForgeView.log i log Armoury).</summary>
    internal static class ForgeBillLink
    {
        private static MethodInfo _rows, _note;
        private static bool _looked;
        private static int _errors;

        internal static void Ensure()
        {
            if (_looked) return;
            _looked = true;
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = asm.GetType("Armoury.ForgeBill", false);
                    if (t == null) continue;
                    const BindingFlags bf = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
                    _rows = t.GetMethod("ScreenRows", bf);
                    _note = t.GetMethod("ScreenNote", bf);
                    break;
                }
            }
            catch (Exception e) { _rows = null; Log.Error("ForgeBillLink.Ensure", e); }
            string line = "ForgeView rzad wymagan - wpiety (mixin CraftingVM, wstawka Crafting Index " + ForgeBillRowExtension.At + "), Armoury "
                          + (_rows != null ? "PODPIETY (Armoury.ForgeBill.ScreenRows)" : "BRAK (Armoury.ForgeBill.ScreenRows) - zostaje rzad skory i lnu BK jak dotad") + ".";
            Log.Info(line);
            if (_note != null) { try { _note.Invoke(null, new object[] { line }); } catch { } }
        }

        /// <summary>Wiersze rachunku albo null (klucz NIE, brak Armoury, blad - rzad BK jak dotad).</summary>
        internal static object[] Rows(ItemObject item, WeaponDesign design)
        {
            Ensure();
            if (_rows == null) return null;
            try { return _rows.Invoke(null, new object[] { item, design }) as object[]; }
            catch (Exception e)
            {
                _errors++;
                if (_errors <= 3 || _errors % 100 == 0)
                {
                    Log.Error("ForgeBillLink.Rows (blad " + _errors + ")", e);
                    var x = e.InnerException ?? e;
                    Note("ForgeView blad ForgeBillLink.Rows (blad " + _errors + "): " + x.GetType().Name + ": " + x.Message);
                }
                return null;
            }
        }

        /// <summary>Linia do logu Armoury (ScreenNote; recenzja KUZ-3 - bledy rzadu w logu zbieranym przez autotest). Bez Armoury - nic.</summary>
        internal static void Note(string text)
        {
            try
            {
                Ensure();
                if (_note != null) _note.Invoke(null, new object[] { text });
            }
            catch { }
        }
    }

    /// <summary>Rzad kafli rachunku: ta sama kotwica co pasek surowcow gry i rzad BK (Center/Bottom, MarginRight 675), nad "-N" gry (94-116 px od dolu).
    /// Wstawka typu Insert (sprawdzona - SetAttribute wieszal ekran). Polozenie w stalych ponizej (do sprawdzenia na zdjeciu).</summary>
    [PrefabExtension("Crafting", "descendant::CraftingScreenWidget/Children", "Crafting")]
    internal class ForgeBillRowExtension : PrefabExtensionInsertPatch
    {
        internal const int At = 7;               // po TableauExtension (6) - nad podgladem 3D
        private const int MarginRight = 675;     // kotwica paska gry i rzedu BK
        private const int MarginBottom = 118;    // nad "-N" paska gry (ChangeAmountTextWidget, PositionYOffset -20 nad kaflem 96 px)
        private const int TileW = 120, TileH = 124;

        private readonly List<XmlNode> nodes;
        public override InsertType Type => (InsertType)3;
        public override int Index => At;

        [PrefabExtensionXmlNodes]
        public IEnumerable<XmlNode> Nodes => nodes;

        public ForgeBillRowExtension()
        {
            var xml =
              "<Widget DoNotAcceptEvents=\"true\" WidthSizePolicy=\"CoverChildren\" HeightSizePolicy=\"CoverChildren\" HorizontalAlignment=\"Center\" VerticalAlignment=\"Bottom\" " +
              "MarginRight=\"" + MarginRight + "\" MarginBottom=\"" + MarginBottom + "\" IsVisible=\"@FvBillShown\"><Children>" +
                "<ListPanel DataSource=\"{FvBillRows}\" DoNotAcceptEvents=\"true\" WidthSizePolicy=\"CoverChildren\" HeightSizePolicy=\"CoverChildren\" " +
                "HorizontalAlignment=\"Center\" VerticalAlignment=\"Bottom\" StackLayout.LayoutMethod=\"HorizontalLeftToRight\"><ItemTemplate>" +
                  // kafel jak w pasku gry: DoNotPassEventsToChildren na kaflu, HintWidget na calej powierzchni (Crafting.xml ROT, ResourceItem)
                  "<Widget WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"Fixed\" SuggestedWidth=\"" + TileW + "\" SuggestedHeight=\"" + TileH + "\" VerticalAlignment=\"Bottom\" " +
                  "DoNotPassEventsToChildren=\"true\"><Children>" +
                    "<ListPanel WidthSizePolicy=\"CoverChildren\" HeightSizePolicy=\"CoverChildren\" HorizontalAlignment=\"Center\" VerticalAlignment=\"Bottom\" " +
                    "StackLayout.LayoutMethod=\"VerticalTopToBottom\"><Children>" +
                      // potrzeba: jasna albo czerwona przy braku (kolor przez Brush.FontColor - jak TableauExtension)
                      "<Widget WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"Fixed\" SuggestedWidth=\"" + TileW + "\" SuggestedHeight=\"24\"><Children>" +
                        "<TextWidget WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"StretchToParent\" Brush=\"Crafting.Tier.Text\" Brush.FontSize=\"20\" " +
                        "Brush.FontColor=\"#EFD9A6FF\" Brush.TextHorizontalAlignment=\"Center\" Text=\"@FvNeed\" IsVisible=\"@FvOk\"/>" +
                        "<TextWidget WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"StretchToParent\" Brush=\"Crafting.Tier.Text\" Brush.FontSize=\"20\" " +
                        "Brush.FontColor=\"#D65252FF\" Brush.TextHorizontalAlignment=\"Center\" Text=\"@FvNeed\" IsVisible=\"@FvShort\"/>" +
                      "</Children></Widget>" +
                      // sklad: sztuki x kg sztuki + pamiec
                      "<TextWidget WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"Fixed\" SuggestedWidth=\"" + TileW + "\" SuggestedHeight=\"18\" Brush=\"Crafting.Tier.Text\" " +
                      "Brush.FontSize=\"14\" Brush.FontColor=\"#C8B89AFF\" Brush.TextHorizontalAlignment=\"Center\" Text=\"@FvDetail\"/>" +
                      // ikona surowca: obraz przedmiotu (wiazanie jak MaterialRowExtension - sprawdzone u Jeffa)
                      "<ImageIdentifierWidget DataSource=\"{FvVisual}\" WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"Fixed\" SuggestedWidth=\"90\" SuggestedHeight=\"55\" " +
                      "HorizontalAlignment=\"Center\" TextureProviderName=\"@TextureProviderName\" ImageId=\"@Id\" AdditionalArgs=\"@AdditionalArgs\"/>" +
                      // plakietka: ile masz (jak licznik paska gry)
                      "<Widget WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"Fixed\" SuggestedWidth=\"110\" SuggestedHeight=\"24\" HorizontalAlignment=\"Center\" " +
                      "Sprite=\"Crafting\\number_background\" Color=\"#EFAB6BFF\"><Children>" +
                        "<TextWidget WidthSizePolicy=\"CoverChildren\" HeightSizePolicy=\"CoverChildren\" Brush=\"Refinement.Amount.Text\" HorizontalAlignment=\"Center\" " +
                        "VerticalAlignment=\"Center\" MarginTop=\"5\" Text=\"@FvHave\"/>" +
                      "</Children></Widget>" +
                    "</Children></ListPanel>" +
                    "<HintWidget DataSource=\"{FvHint}\" WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"StretchToParent\" Command.HoverBegin=\"ExecuteBeginHint\" " +
                    "Command.HoverEnd=\"ExecuteEndHint\" IsDisabled=\"true\"/>" +
                  "</Children></Widget>" +
                "</ItemTemplate></ListPanel>" +
              "</Children></Widget>";
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            nodes = new List<XmlNode> { doc };
            Log.Info("ForgeBillRowExtension: rzad rachunku kuzni (m109) - wstawka Index " + At + ", kotwica MarginRight " + MarginRight + ", MarginBottom " + MarginBottom + ".");
        }
    }
}
