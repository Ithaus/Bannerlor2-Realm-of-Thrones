using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// m97-wioski (20.10, Jeff: "czy mozemy zrobic tak, ze do wioski pobocznej tez mozna wjechac jak do wioski glownej? ... nie bedzie zadnych ludzi/bohaterow, ale bedzie
    /// mozna zainwestowac, zakupic towar, przespac sie, oboz rozbic, pladrowac kazda osobno, zaplacic okup"). KAZDA wioska poboczna na mapie (wiersz ukladu wiosek
    /// arm_map_villages, takze przysiolki), nie tylko wioska gracza (ta ma swoje menu - SideVillageFief), dostaje wjazd i menu jak wies gry, bez bohaterow:
    ///  V1 WJAZD (klucz SideVillagesEnterable): klik na wioske na mapie (MapVillagesView) = jazda do niej (MapState.ProcessTravel, wzor zajazdow i wioski gracza) i menu
    ///     "arm_sidevil" po dojechaniu (do 1.6 jedn.). Bez pytania przy postoju (wiosek sa setki - pytanie przy kazdym postoju meczyloby). Tick 4 razy na sekunde: tylko
    ///     czekajacy klik (jedno porownanie odleglosci). Druzyna znika z mapy jak w zajezdzie (RoadsideInnStay - ta sama droga i te same klucze: Roadside Inn Hide Party),
    ///     takze w menu wioski gracza z mapy i przy snie w wiosce; dzwiek i obrazek jak w menu wsi gry ("village": settlement_village, 2d/village, wait_mesh wsi okregu).
    ///  V2 OPIS (przy wejsciu, tekst menu liczony w init): czyja (pan okregu, krolestwo), okreg i wies gry, ludzie i osady, stan po rabunku (ksiega wiosek), co robi
    ///     i jaka czesc okregu (DistrictWorks.PartLines - te same linie co dymek), okup / ochrona, wojna.
    ///  V3 INWESTYCJE: okno prac TEJ czesci okregu (DistrictWorks.OpenPartFor - prace okregu juz sa per czesc: wies gry albo jedna wioska poboczna), warunek jak w menu
    ///     wsi gry (CanTrade: wojna, rabunek) i wycena czesci (Allowed, rodzaj prac) - liczone raz przy wejsciu i po oknie prac.
    ///  V4 HANDEL: wioska nie ma wlasnego magazynu (setki nowych magazynow = koszt i druga ekonomia). Koszyk liczony LENIWIE przy kliknieciu "Trade": dla kazdego
    ///     towaru w magazynie wsi okregu ta czesc, jaka ta wioska ma w jego produkcji (VillageCrafts.PartsOf - sztuki dnia czesci / sztuki dnia okregu), zaokraglona;
    ///     towar, ktorego wioska nie robi - nie w koszyku. Ekran handlu jak wioska gracza z mapy (ceny targu wsi okregu, kiesa wsi jako kupiec); po "Done" magazyn wsi
    ///     okregu zmienia sie DOKLADNIE o roznice koszyka (kupione schodzi, sprzedane wchodzi - dowoz do wsi okregu), a kiesa wsi o to, co gra dala/zabrala graczowi.
    ///     Anulowanie - gra cofa koszyk, magazyn bez zmian. Zamknieta ekonomia: platnik i odbiorca to gracz i kiesa / magazyn wsi okregu.
    ///  V5 NOCLEG: wies okregu rodu gracza - za darmo pod dachem (TownInns.HostSleep - jak wioska gracza; TownInns.HostNear przyjmuje te wioske); obca wioska - za oplata jak
    ///     wspolna izba zajazdu (TownInns.RoadQCondition / RoadCommit z "zajazdem" tej wioski: ten sam rachunek, pieniadze do kiesy wsi okregu z podatkiem pana). W wojnie,
    ///     przy rabunku i w spalonej wiosce - nie. OBOZ: "Make camp here" - menu zamkniete, oboz rozbija tick (NightRest.MakeCampNow, droga klawisza O), bez SwitchToMenu.
    ///  V6 WROGIE AKCJE (SideVillageVisit.Raid.cs): rabunek TEJ wioski (osobno, nie przez wies gry) i okup tej wioski z ochrona 30 dob. Recenzja 20.10: obrona = cala
    ///     milicja wsi okregu (jak okup wsi), rabunek wsi gry przez gracza pali tylko jej osady (DistrictRaid.PlayerMainOnly), ochrona po okupie dziala tez w lancuchu
    ///     rabunku wsi gry (VillageBook) i pomniejsza okup wsi gry tej frakcji (RaidTribute.QuoteOf). Krok 2 (SideVillageVisit.Defenders.cs, klucz SideVillageDefenders): wioske
    ///     broni jej WLASNA ludnosc (1 na 10 w domu, pulap 60) - poddanie przy 4:1, inaczej zwykla bitwa z tymczasowa druzyna; AI slabsze niz obroncy pomija wioske w lancuchu.
    ///  V7 POBOR (SideVillageVisit.Levy.cs, klucz SideVillageRecruit, zapis arm_sidelevy - m113-pobor, Jeff 24.10): "Recruit men of the village" - wlasna pula mlodych TEJ wioski
    ///     (10% jej ludzi), odnawiana tylko przez dorastanie; kazdy rekrut schodzi z ludzi wioski i z rak jej okregu.
    /// Zapis arm_sidevil (SaveText): okupy, ostatnie rabunki gracza (10 dob), rabunek w toku. Brak klucza (stary zapis) = nic. AI nie wjezdza do wiosek pobocznych: wjazd, handel,
    /// nocleg i pobor sa tylko dla gracza; AI omija wioski po okupie i slabsze od obroncow w lancuchu rabunku (Defenders) - pule obroncow liczy ta sama regula co pobor
    /// (lancuch AI nie obniza puli mlodych - nie zdejmuje ludzi z ksiegi; pule obniza tylko pobor gracza, bitwa z obroncami i pobor gry z okregu).
    /// Linie logu "Wioski poboczne (WP): ..." - start sesji, wjazd, handel, nocleg, oboz, prace, rabunek, okup, doba. Potkniecia liczone, nic nie gasimy globalnie.
    /// </summary>
    internal static partial class SideVillageVisit
    {
        private const string P = "Wioski poboczne (WP): ";
        internal const string Menu = "arm_sidevil", HostileMenu = "arm_sidevil_hostile", RaidMenu = "arm_sidevil_raid";
        private const float EnterR = 1.6f;
        private const string AmbVillage = "event:/map/ambient/node/settlements/2d/village", PanelVillage = "event:/ui/panels/settlement_village";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On { get { var s = Settings.Current; return s != null && s.SideVillagesEnterable; } }

        // ------------------------------------------------------------ wioski ukladu (raz na sesje, po liscie ksiegi)
        private static Dictionary<string, MapVillageRecord> _rec;
        private static object _recFrom;
        // "zajazdy" wiosek pobocznych dla noclegu za oplata i ukrycia partii (RoadsideInns.Find - zapas po uid); tylko pamiec
        private static readonly Dictionary<string, RoadsideInns.Inn> _inns = new Dictionary<string, RoadsideInns.Inn>(StringComparer.Ordinal);

        // ------------------------------------------------------------ stan wejscia (tylko pamiec)
        private static string _uid;                 // wioska otwartego menu
        private static string _pending; private static float _px, _py;
        private static long _lastCheck;
        private static bool _campAfter;
        private static int _invUidHour = -1; private static string _invUid; private static bool _invOk; private static string _invWhy;

        // ------------------------------------------------------------ liczniki
        private static int _st, _stDay;
        private static readonly HashSet<string> _err = new HashSet<string>(StringComparer.Ordinal);
        private static int _dEnter, _dClick, _dTrade, _dRest, _dRestFree, _dCamp, _dInvest;
        private static long _dBuy, _dSell, _dPcsOut, _dPcsIn;
        private static long _tickTicks; private static int _tickN;

        internal static void Reset()
        {
            _rec = null; _recFrom = null; _inns.Clear();
            _uid = null; _pending = null; _lastCheck = 0; _campAfter = false;
            _invUidHour = -1; _invUid = null; _invOk = false; _invWhy = null;
            _st = _stDay = 0; lock (_err) _err.Clear();
            DayZero();
            RaidReset();
            LevyReset();   // m113-pobor: pule wiosek, braki rak po poborze
        }

        private static void DayZero()
        {
            _dEnter = _dClick = _dTrade = _dRest = _dRestFree = _dCamp = _dInvest = 0; _dBuy = _dSell = _dPcsOut = _dPcsIn = 0; _stDay = 0;
            _tickTicks = 0; _tickN = 0;
            RaidDayZero();
            LevyDayZero();   // m113-pobor
        }

        private static void Stumble(string where, Exception e)
        {
            _st++; _stDay++;
            bool first; lock (_err) first = _err.Add(where);
            if (first || _st % 200 == 0) Log.Error("SideVillageVisit." + where + " (potkniecie " + _st + ")", e);
        }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }
        private static string Txt(TextObject t) { try { return t != null ? t.ToString() : "?"; } catch { return "?"; } }

        internal static MapVillageRecord Rec(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return null;
            var l = VillageBook.LayoutRecords();
            if (l == null) return null;
            if (_rec == null || !ReferenceEquals(_recFrom, l))
            {
                var d = new Dictionary<string, MapVillageRecord>(l.Count, StringComparer.Ordinal);
                foreach (var r in l) if (r != null && !string.IsNullOrEmpty(r.Uid) && !string.IsNullOrEmpty(r.VillageId)) d[r.Uid] = r;
                _rec = d; _recFrom = l;
            }
            MapVillageRecord x;
            return _rec.TryGetValue(uid, out x) ? x : null;
        }

        private static Settlement DistOf(MapVillageRecord r)
        {
            if (r == null) return null;
            var st = Settlement.Find(r.VillageId);
            return st != null && st.IsVillage && st.Village != null ? st : null;
        }

        private static string NameOf(MapVillageRecord r) { return r == null ? "?" : (string.IsNullOrEmpty(r.Name) ? r.Uid : r.Name); }

        // ============================================================ "zajazd" wioski (nocleg za oplata, ukrycie partii)
        /// <summary>RoadsideInns.Find (zapas, gdy uid nie jest zajazdem): "zajazd" wioski pobocznej - nazwa wioski, wies okregu, jedna jakosc (wspolna izba), miejsce
        /// postoju = miejsce partii przy wejsciu (po wczytaniu: partia przy wiosce - jej miejsce, inaczej srodek wioski z ukladu). null - to nie wioska poboczna.</summary>
        internal static RoadsideInns.Inn InnFor(string uid)
        {
            try
            {
                if (uid == null) return null;
                RoadsideInns.Inn inn;
                if (_inns.TryGetValue(uid, out inn)) return inn;
                var r = Rec(uid);
                var st = DistOf(r);
                if (st == null) return null;
                float x = r.X, y = r.Y;
                var mp = MobileParty.MainParty;
                if (mp != null && mp.CurrentSettlement == null)
                {
                    var p = mp.GetPosition2D;
                    if (p.Distance(new Vec2(r.X, r.Y)) <= 4.0f) { x = p.x; y = p.y; }
                }
                inn = new RoadsideInns.Inn { Uid = uid, Name = NameOf(r), VillageId = r.VillageId, Kind = "village", X = x, Y = y, Bx = r.X, By = r.Y, Tiers = 1, Village = st, Culture = "" };
                _inns[uid] = inn;
                return inn;
            }
            catch (Exception e) { Stumble("InnFor", e); return null; }
        }

        private static void PlaceInn(string uid, float x, float y)
        {
            var inn = InnFor(uid);
            if (inn != null) { inn.X = x; inn.Y = y; }
        }

        /// <summary>RoadsideInnStay.InnNow: menu, w ktorym partia gracza przy wiosce pobocznej znika z mapy (to menu, menu wioski gracza i jej domu - z mapy).</summary>
        internal static bool StayMenu(string id)
        {
            if (id == null || !On) return false;
            if (id == Menu || id == HostileMenu) return _uid != null;   // wrogie akcje to dalej pobyt w wiosce; rabunek (menu oczekiwania) - partia na mapie
            return (id == SideVillageFief.Menu || id == SideVillageHome.HomeMenu) && SideVillageFief.HasFief;
        }

        /// <summary>RoadsideInnStay.InnNow: "zajazd" wioski menu (StayMenu). Wioska gracza - miejsce postoju przy niej (SideVillageFief.PlayerDist), gdy partia stoi obok.</summary>
        internal static RoadsideInns.Inn StayInn(string id)
        {
            if (id == Menu || id == HostileMenu) return InnFor(_uid);
            var inn = InnFor(SideVillageFief.Uid);
            try
            {
                var mp = MobileParty.MainParty;
                if (inn != null && mp != null && mp.CurrentSettlement == null && SideVillageFief.PlayerDist() <= 2.2f) { var p = mp.GetPosition2D; inn.X = p.x; inn.Y = p.y; }
            }
            catch { }
            return inn;
        }

        /// <summary>TownInns.HostNear: nocleg pod dachem w wiosce okregu rodu gracza (V5) - partia w wsi okregu albo poza osada najwyzej r od miejsca postoju wioski.</summary>
        internal static bool HostNear(string uid, string distId, float r)
        {
            try
            {
                if (!On || uid == null) return false;
                var rec = Rec(uid);
                if (rec == null || rec.VillageId != distId) return false;
                var mp = MobileParty.MainParty;
                if (mp == null) return false;
                if (mp.CurrentSettlement != null) return mp.CurrentSettlement.StringId == distId;
                var inn = InnFor(uid);
                return inn != null && mp.GetPosition2D.Distance(new Vec2(inn.X, inn.Y)) <= r;
            }
            catch { return false; }
        }

        private static bool IsVillageUid(string uid) { return uid != null && Rec(uid) != null; }

        // ============================================================ V1 wjazd z mapy
        /// <summary>MapVillagesView: klik na te wioske prowadzi do niej (kursor raczki). Wioska gracza z wlasnym menu - SideVillageFief.</summary>
        internal static bool Clickable(string uid)
        {
            if (!On || string.IsNullOrEmpty(uid) || SideVillageFief.Clickable(uid)) return false;
            try { return DistOf(Rec(uid)) != null; } catch { return false; }
        }

        internal static bool OnClick(string uid, float x, float y)
        {
            try
            {
                if (!Clickable(uid)) return false;
                var mp = MobileParty.MainParty;
                if (mp == null || mp.MapEvent != null || mp.CurrentSettlement != null) return false;
                if (mp.Army != null && mp.Army.LeaderParty != mp) return false;
                var ms = Game.Current != null ? Game.Current.GameStateManager.ActiveState as MapState : null;
                if (ms == null) return false;
                ms.ProcessTravel(new CampaignVec2(new Vec2(x, y), true));
                _pending = uid; _px = x; _py = y;
                _dClick++;
                return true;
            }
            catch (Exception e) { Stumble("OnClick", e); return false; }
        }

        /// <summary>CampaignEvents.TickEvent: 4 razy na sekunde (zegar rzeczywisty) - czekajacy klik (menu po dojechaniu), oboz po wyjsciu z menu, straznik rabunku.</summary>
        internal static void Tick()
        {
            if (_pending == null && !_campAfter && !RaidActive && _fight == null && !_sweepPending) return;
            long tq = Stopwatch.GetTimestamp();
            if (tq - _lastCheck < Stopwatch.Frequency / 4) return;
            _lastCheck = tq;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                RaidGuard();
                if (_sweepPending && Campaign.Current != null && MobileParty.MainParty != null) { _sweepPending = false; if (On) SweepOrphans(); }   // krok 2: druzyny obroncow po wczytaniu
                FightGuard();   // krok 2: koniec bitwy z obroncami wioski (spotkanie skonczone) - takze przy otwartym menu
                var c = Campaign.Current;
                if (c == null || c.CurrentMenuContext != null) return;
                if (PlayerEncounter.Current != null || PopupBlocked()) return;
                var mp = MobileParty.MainParty;
                if (mp == null || Hero.MainHero == null || !Hero.MainHero.IsAlive) { _pending = null; _campAfter = false; return; }
                if (mp.CurrentSettlement != null || mp.MapEvent != null || mp.IsCurrentlyAtSea || PlayerCaptivity.IsCaptive || mp.SiegeEvent != null || mp.BesiegerCamp != null)
                { _pending = null; _campAfter = false; return; }
                if (_campAfter)
                {
                    _campAfter = false;
                    _dCamp++;
                    Log.Info(P + "oboz przy wiosce " + NameOf(Rec(_uid)) + " (" + (_uid ?? "-") + ") - NightRest.MakeCampNow (droga klawisza O).");
                    NightRest.MakeCampNow();
                    return;
                }
                if (_pending == null) return;
                if (!On) { _pending = null; return; }
                var pos = mp.GetPosition2D;
                var target = new Vec2(_px, _py);
                if (pos.Distance(target) <= EnterR) { string u = _pending; _pending = null; Open(u, "klik na wioske"); return; }
                if (mp.TargetPosition.ToVec2().Distance(target) > EnterR + 0.5f) _pending = null;   // gracz pojechal gdzie indziej
            }
            catch (Exception e) { Stumble("Tick", e); }
            finally { _tickTicks += Stopwatch.GetTimestamp() - t0; _tickN++; }
        }

        private static bool PopupBlocked()
        {
            if (LogGate.Mode == 1 || LogGate.Mode == 2) return true;   // autotest CrashScribe - nikt nie kliknie (jak zajazdy i wioska gracza)
            if (InformationManager.IsAnyInquiryActive()) return true;
            var cm = Campaign.Current != null ? Campaign.Current.ConversationManager : null;
            if (cm != null && (cm.IsConversationInProgress || cm.IsConversationFlowActive)) return true;
            var ms = Game.Current != null && Game.Current.GameStateManager != null ? Game.Current.GameStateManager.ActiveState as MapState : null;
            return ms == null;
        }

        private static void Open(string uid, string how)
        {
            var c = Campaign.Current;
            if (c == null || c.CurrentMenuContext != null || PlayerEncounter.Current != null) return;
            var mp = MobileParty.MainParty;
            if (mp == null || mp.CurrentSettlement != null || mp.MapEvent != null) return;
            var r = Rec(uid);
            var st = DistOf(r);
            if (st == null) return;
            _uid = uid;
            var p = mp.GetPosition2D;
            PlaceInn(uid, p.x, p.y);                 // miejsce postoju = miejsce partii (nocleg i ukrycie licza od niego)
            _invUidHour = -1;
            _dEnter++;
            try { mp.SetMoveModeHold(); } catch { }
            GameMenu.ActivateGameMenu(Menu);
            Log.Info(P + "wjazd do " + NameOf(r) + " (" + uid + ", okreg " + Txt(st.Name) + " " + st.StringId + ", pan " + (st.OwnerClan != null ? Txt(st.OwnerClan.Name) : "-")
                     + ", wojna z graczem " + (AtWar(st) ? "TAK" : "nie") + ") - " + how + ", partia " + p.Distance(new Vec2(r.X, r.Y)).ToString("0.00", Inv) + " jedn. od srodka wioski.");
        }

        // ============================================================ wspolne warunki
        internal static bool AtWar(Settlement st)
        {
            IFaction mf = Hero.MainHero != null ? Hero.MainHero.MapFaction : null, df = st != null ? st.MapFaction : null;
            return mf != null && df != null && !ReferenceEquals(mf, df) && FactionManager.IsAtWarAgainstFaction(mf, df);
        }

        private static bool OwnLands(Settlement st) { return st != null && Clan.PlayerClan != null && ReferenceEquals(st.OwnerClan, Clan.PlayerClan); }

        /// <summary>Czesc okregu tej wioski z ksiegi (PartsOf) i suma ludzi w domu oraz osad okregu. false - ksiega jej nie zna.</summary>
        private static bool PartOf(Settlement st, string uid, out VillageBook.PartView me, out double homeAll, out double nAll)
        {
            me = null; homeAll = 0; nAll = 0;
            if (st == null || st.Village == null || uid == null) return false;
            var pv = new List<VillageBook.PartView>();
            if (!VillageBook.PartsOf(st.Village, pv)) return false;
            foreach (var p in pv)
            {
                if (p == null) continue;
                homeAll += p.Home; nAll += p.Nw;
                if (!p.Main && string.Equals(p.Uid, uid, StringComparison.Ordinal)) me = p;
            }
            return me != null;
        }

        private static bool Burned(VillageBook.PartView me)
        {
            return me != null && (me.State == VillageBook.SBurned || me.State == VillageBook.SBurning || me.State == VillageBook.SAbandoned);
        }

        private static bool AtVillage()
        {
            var mp = MobileParty.MainParty;
            var inn = InnFor(_uid);
            return mp != null && inn != null && mp.CurrentSettlement == null && mp.MapEvent == null && mp.GetPosition2D.Distance(new Vec2(inn.X, inn.Y)) <= EnterR + 0.6f;
        }

        // ============================================================ menu
        internal static void AddMenus(CampaignGameStarter starter)
        {
            try
            {
                starter.AddGameMenu(Menu, "{=!}{ARM_SIDEVIL_TEXT}", MenuInit, GameMenu.MenuOverlayType.None);
                starter.AddGameMenuOption(Menu, "arm_sidevil_rest", "{=!}{ARM_SIDEVIL_REST}", RestCondition, RestConsequence, false, 0);
                starter.AddGameMenuOption(Menu, "arm_sidevil_camp", "{=!}Make camp here", CampCondition,
                    delegate (MenuCallbackArgs a)
                    {
                        try { _campAfter = true; RoadsideInnStay.Leave("oboz przy wiosce pobocznej"); GameMenu.ExitToLast(); }
                        catch (Exception e) { Stumble("Camp", e); }
                    }, false, 1);
                starter.AddGameMenuOption(Menu, "arm_sidevil_trade", "{=!}Trade", TradeCondition, TradeConsequence, false, 2);
                starter.AddGameMenuOption(Menu, "arm_sidevil_invest", "{=!}Invest in its works", InvestCondition,
                    delegate (MenuCallbackArgs a)
                    {
                        try
                        {
                            var st = DistOf(Rec(_uid));
                            if (st == null) return;
                            _dInvest++; _invUidHour = -1;
                            Log.Info(P + "okno prac w " + NameOf(Rec(_uid)) + " (" + _uid + ", okreg " + st.StringId + ").");
                            DistrictWorks.OpenPartFor(st, _uid);
                        }
                        catch (Exception e) { Stumble("Invest", e); }
                    }, false, 3);
                starter.AddGameMenuOption(Menu, "arm_sidevil_hostile", "{=!}Take a hostile action", HostileCondition,
                    delegate (MenuCallbackArgs a) { try { GameMenu.SwitchToMenu(HostileMenu); } catch (Exception e) { Stumble("Hostile", e); } }, false, 4);
                // m113-pobor: indeks 2 wstawia (GameMenu.AddOption - Insert) - dodane PO wszystkich opcjach z indeksem, inaczej przesuwa Trade za wrogie akcje
                // (kolejnosc: nocleg, oboz, pobor, handel, prace, wrogie akcje, wyjazd)
                starter.AddGameMenuOption(Menu, LevyOption, "{=!}Recruit men of the village", LevyMenuCondition, LevyMenuConsequence, false, 2);
                starter.AddGameMenuOption(Menu, "arm_sidevil_leave", "{=!}Leave",
                    delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                    delegate (MenuCallbackArgs a)
                    {
                        try { RoadsideInnStay.Leave("wyjazd z wioski pobocznej"); _uid = null; GameMenu.ExitToLast(); }
                        catch (Exception e) { Stumble("Leave", e); }
                    }, true, 9);
                AddRaidMenus(starter);   // SideVillageVisit.Raid.cs: wrogie akcje (rabunek tej wioski, okup)
            }
            catch (Exception e) { Stumble("AddMenus", e); }
        }

        /// <summary>Dzwiek i obrazek jak menu wsi gry ("village": PlayerTownVisitCampaignBehavior - settlement_village, 2d/village, wait_mesh wsi). Po OnMenuCreate gry
        /// (jak RoadsideInnStay.InnMenuSound) - przy wejsciu, po ekwipunku, po wczytaniu. Jedna metoda na id menu (nikt inny nie ma "arm_sidevil").</summary>
        [GameMenuInitializationHandler(Menu)]
        public static void MenuSound(MenuCallbackArgs args)
        {
            try
            {
                if (!On || args == null || args.MenuContext == null) return;
                args.MenuContext.SetAmbientSound(AmbVillage);
                args.MenuContext.SetPanelSound(PanelVillage);
            }
            catch (Exception e) { Stumble("MenuSound", e); }
        }

        [GameMenuInitializationHandler(HostileMenu)]
        public static void HostileSound(MenuCallbackArgs args)
        {
            try { if (On && args != null && args.MenuContext != null) args.MenuContext.SetAmbientSound(AmbVillage); }
            catch (Exception e) { Stumble("HostileSound", e); }
        }

        /// <summary>RoadsideInnStay.InnSleepSound (sen, "arm_sleep_wait"): sen w wiosce pobocznej (za oplata - "zajazd" wioski, albo pod dachem we wlasnej wiosce / ziemi
        /// rodu) - tlo wsi zamiast karczmy. true - obsluzone (karczma nie gra).</summary>
        internal static bool SleepSound(MenuCallbackArgs args)
        {
            try
            {
                if (!On || args == null || args.MenuContext == null) return false;
                string uid = TownInns.RoadSleepUid();
                if (uid == null || !IsVillageUid(uid)) uid = TownInns.HostSleepUidNow();
                if (uid == null || !IsVillageUid(uid)) return false;
                args.MenuContext.SetAmbientSound(AmbVillage);
                return true;
            }
            catch (Exception e) { Stumble("SleepSound", e); return false; }
        }

        /// <summary>Wejscie z zapisu w srodku menu: wioska przy partii (najblizsza z ukladu do 2.2 jedn.) - jedno przejscie po liscie, tylko gdy _uid nie ma.</summary>
        private static void RecoverUid()
        {
            if (_uid != null) return;
            var mp = MobileParty.MainParty;
            var l = VillageBook.LayoutRecords();
            if (mp == null || l == null) return;
            var p = mp.GetPosition2D;
            float bd = 4.0f; string best = null;
            foreach (var r in l) { if (r == null) continue; float d = p.Distance(new Vec2(r.X, r.Y)); if (d < bd) { bd = d; best = r.Uid; } }
            _uid = best;
            if (best != null) { PlaceInn(best, p.x, p.y); Log.Info(P + "menu wioski po wczytaniu - wioska " + NameOf(Rec(best)) + " (" + best + "), " + bd.ToString("0.00", Inv) + " jedn. od partii."); }
        }

        private static void MenuInit(MenuCallbackArgs args)
        {
            try
            {
                RecoverUid();
                var r = Rec(_uid);
                var st = DistOf(r);
                try
                {
                    string mesh = st != null && st.SettlementComponent != null ? st.SettlementComponent.WaitMeshName : null;
                    args.MenuContext.SetBackgroundMeshName(string.IsNullOrEmpty(mesh) ? "wait_fallback" : mesh);
                }
                catch { }
                var inn = InnFor(_uid);
                if (inn != null) RoadsideInnStay.OnMenuInit(args, inn);   // partia znika z mapy jak w zajezdzie (klucz Roadside Inn Hide Party; wlasny try)
                _invUidHour = -1;
                MBTextManager.SetTextVariable("ARM_SIDEVIL_TEXT", MenuText(r, st));
            }
            catch (Exception e) { Stumble("MenuInit", e); try { MBTextManager.SetTextVariable("ARM_SIDEVIL_TEXT", "A side village."); } catch { } }
        }

        private static string MenuText(MapVillageRecord r, Settlement st)
        {
            if (r == null || st == null) return "You stand among a few homesteads. Nobody here knows what village this is.";
            var sb = new StringBuilder(700);
            string name = NameOf(r), dn = Txt(st.Name);
            var lord = st.OwnerClan;
            sb.Append(name).Append(" - a side village in the lands of ").Append(dn);
            if (lord != null) sb.Append(", sworn to ").Append(MapVillagesView.HouseName(lord));
            if (st.MapFaction != null) sb.Append(" (").Append(Txt(st.MapFaction.Name)).Append(')');
            sb.Append(". No lord or headman lives here - its folk answer to ").Append(dn).Append(".\n");
            VillageBook.PartView me; double homeAll, nAll;
            if (PartOf(st, _uid, out me, out homeAll, out nAll))
            {
                sb.Append(Math.Round(me.Home + me.Away).ToString("N0", Inv)).Append(" people in ").Append(Math.Max(1.0, Math.Round(me.Nw)).ToString("0", Inv)).Append(" homesteads");
                if (me.State == VillageBook.SBurned || me.State == VillageBook.SBurning) sb.Append(" - burned; people are slowly coming back.");
                else if (me.State == VillageBook.SAbandoned) sb.Append(" - abandoned in hunger.");
                else if (me.State == VillageBook.SRebuilding) sb.Append(" - rebuilding.");
                else if (me.Br + me.Bf > 0.5) sb.Append(" - raiders have emptied part of it.");
                else sb.Append('.');
                sb.Append('\n');
                string lv = LevyLine(st, _uid, me);   // m113-pobor: pula mlodych, dorastanie, etap
                if (lv != null) sb.Append(lv).Append('\n');
            }
            foreach (string l in DistrictWorks.PartLines(st, _uid, false)) sb.Append(l).Append('\n');
            if (st.Village.VillageState == Village.VillageStates.BeingRaided) sb.Append("Raiders are on the lands of ").Append(dn).Append(" - the village is barred and quiet.\n");
            string tri = TributeLine(st);
            if (tri != null) sb.Append(tri).Append('\n');
            if (AtWar(st)) sb.Append("You are at war with ").Append(Txt(st.MapFaction.Name)).Append(": the villagers watch ").Append(PartyWords.Pick("your men", "you", "you and your companions")).Append(" from behind their doors.");   // m110-sam: bez zolnierzy - o graczu
            else if (OwnLands(st)) sb.Append("These are your own lands.");
            return sb.ToString().TrimEnd('\n');
        }

        // ------------------------------------------------------------ V5 nocleg
        private static bool RestCondition(MenuCallbackArgs a)
        {
            a.optionLeaveType = GameMenuOption.LeaveType.Wait;
            try
            {
                MBTextManager.SetTextVariable("ARM_SIDEVIL_REST", "Rest the night here");
                var r = Rec(_uid); var st = DistOf(r);
                if (st == null) return false;
                var mp = MobileParty.MainParty;
                if (NightRest.EnlistedNow) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}Your lord decides when the men sleep."); return true; }
                if (mp != null && mp.Army != null && mp.Army.LeaderParty != mp) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}You march in another lord's army - he decides where the host sleeps."); return true; }
                string why = RestBlock(st);
                if (why != null) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}" + why + " You can still make camp here."); return true; }
                if (OwnLands(st))
                {
                    if (!TownInns.On) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}Lodgings need Town Inns and Night Rest (mod settings)."); return true; }
                    // m110-sam: bez zolnierzy - podpowiedz o graczu (sam) albo o nim i towarzyszach
                    a.Tooltip = new TextObject("{=!}Your own villagers take " + PartyWords.Men() + " in for the night - no charge. "
                                               + PartyWords.Pick("They sleep on straw under roofs until dawn and wake rested if they sleep their fill: ",
                                                                 "You sleep on straw under a roof until dawn and wake rested if you sleep your fill: ",
                                                                 "You sleep on straw under roofs until dawn and wake rested if you sleep your fill: ")
                                               + TownInns.HostBonusText() + ".");
                    return true;
                }
                var inn = InnFor(_uid);
                if (inn == null) return false;
                bool show = TownInns.RoadQCondition(a, inn, 0);   // ten sam rachunek i podpowiedz co wspolna izba zajazdu (pieniadze do kiesy wsi okregu)
                int cost = TownInns.VisitCost();
                if (cost > 0) MBTextManager.SetTextVariable("ARM_SIDEVIL_REST", "Rest the night here - " + cost + " d");
                return show;
            }
            catch (Exception e) { Stumble("RestCondition", e); return false; }
        }

        /// <summary>Powod, dla ktorego wioska nie przyjmie na noc (po angielsku) - wojna, rabunek okregu, wioska spalona, partia nie przy wiosce. null - przyjmie.</summary>
        private static string RestBlock(Settlement st)
        {
            if (!AtVillage()) return "You are not at the village.";
            if (AtWar(st)) return "At war with " + Txt(st.MapFaction.Name) + " - nobody here will take " + PartyWords.Pick("your men", "you", "you and your companions") + " in.";   // m110-sam: bez zolnierzy - o graczu
            if (st.Village.VillageState == Village.VillageStates.BeingRaided) return "Raiders are on these lands - the villagers have barred their doors.";
            VillageBook.PartView me; double h, n;
            if (PartOf(st, _uid, out me, out h, out n) && Burned(me)) return "The village is burned - there are no roofs left to sleep under.";
            return null;
        }

        private static void RestConsequence(MenuCallbackArgs a)
        {
            try
            {
                var r = Rec(_uid); var st = DistOf(r);
                if (st == null || RestBlock(st) != null) return;
                if (OwnLands(st))
                {
                    if (!TownInns.HostSleep(st.StringId, _uid, NameOf(r))) return;
                    _dRest++; _dRestFree++;
                    Log.Info(P + "nocleg w " + NameOf(r) + " (" + _uid + ", okreg rodu gracza " + st.StringId + ") - pod dachem, bez oplaty (TownInns.HostSleep).");
                    NightRest.BeginInnSleep();   // zwykle menu - SwitchToMenu do snu wolno
                    return;
                }
                _dRest++;
                Log.Info(P + "nocleg w " + NameOf(r) + " (" + _uid + ", okreg " + st.StringId + ") - za oplata jak wspolna izba (TownInns.RoadCommit, kiesa wsi okregu).");
                TownInns.RoadCommit(InnFor(_uid), 0);
            }
            catch (Exception e) { Stumble("Rest", e); }
        }

        private static bool CampCondition(MenuCallbackArgs a)
        {
            a.optionLeaveType = GameMenuOption.LeaveType.Wait;
            try
            {
                var s = Settings.Current;
                if (s == null || !s.NightRestEnabled) return false;
                if (NightRest.EnlistedNow) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}Your lord decides where the men camp."); return true; }
                var mp = MobileParty.MainParty;
                if (mp != null && mp.Army != null && mp.Army.LeaderParty != mp) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}You march in another lord's army - he decides where the host camps."); return true; }
                a.Tooltip = new TextObject("{=!}Pitch your tents by the village, as anywhere on the map.");
                return true;
            }
            catch (Exception e) { Stumble("CampCondition", e); return false; }
        }

        // ------------------------------------------------------------ V3 inwestycje
        private static bool InvestCondition(MenuCallbackArgs a)
        {
            a.optionLeaveType = GameMenuOption.LeaveType.Trade;
            try
            {
                var st = DistOf(Rec(_uid));
                if (st == null || !VillageBook.Active) return false;
                if (!DistrictWorks.On) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}District works are switched off (mod settings)."); return true; }
                string why;
                if (!InvestOk(st, out why)) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}" + why); return true; }
                a.Tooltip = new TextObject("{=!}Pay the villagers of this village for boats, herds, ploughs or tools. It makes more of its own goods, and half of what the extra sells for is yours.");
                return true;
            }
            catch (Exception e) { Stumble("InvestCondition", e); return false; }
        }

        /// <summary>Prace tej wioski da sie kupic: warunek menu wsi gry (CanTrade) i wycena czesci (Allowed, rodzaj prac) - raz na godzine gry i po oknie prac (jak wioska gracza).</summary>
        private static bool InvestOk(Settlement st, out string why)
        {
            int h = (int)CampaignTime.Now.ToHours;
            if (h != _invUidHour || !string.Equals(_invUid, _uid, StringComparison.Ordinal))
            {
                _invUidHour = h; _invUid = _uid; _invOk = false; _invWhy = "No works here.";
                try
                {
                    string w;
                    if (!DistrictWorks.CanTrade(Clan.PlayerClan, st.Village, false, out w)) _invWhy = w ?? "Not now.";
                    else
                    {
                        var qs = new List<DistrictWorks.Quote>();
                        if (!DistrictWorks.QuotesOf(st.Village, qs, Clan.PlayerClan)) _invWhy = "The villages of this district are not known yet.";
                        else
                        {
                            DistrictWorks.Quote q = null;
                            foreach (var x in qs) if (x != null && string.Equals(x.Uid, _uid, StringComparison.Ordinal)) { q = x; break; }
                            if (q != null && q.Allowed && q.WKind >= 0) { _invOk = true; _invWhy = null; }
                            else if (q != null && !string.IsNullOrEmpty(q.Why)) _invWhy = q.Why;
                        }
                    }
                }
                catch (Exception e) { _invOk = false; _invWhy = "No works here."; Stumble("InvestOk", e); }
            }
            why = _invWhy;
            return _invOk;
        }

        // ------------------------------------------------------------ V4 handel (koszyk leniwie)
        private static bool TradeCondition(MenuCallbackArgs a)
        {
            a.optionLeaveType = GameMenuOption.LeaveType.Trade;
            try
            {
                var st = DistOf(Rec(_uid));
                if (st == null) return false;
                string dn = Txt(st.Name);
                if (AtWar(st)) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}At war with " + Txt(st.MapFaction.Name) + ", nobody here will trade with you."); return true; }
                if (st.Village.VillageState != Village.VillageStates.Normal) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}Raiders have been through " + dn + " - there is nothing to trade now."); return true; }
                VillageBook.PartView me; double h, n;
                if (PartOf(st, _uid, out me, out h, out n) && Burned(me)) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}The village is burned - there is nothing to trade."); return true; }
                if (!AtVillage()) { a.IsEnabled = false; a.Tooltip = new TextObject("{=!}You are not at the village."); return true; }
                a.Tooltip = new TextObject("{=!}Buy what this village makes - its share of the goods in the store of " + dn + " - and sell your own goods to it, at the prices of " + dn
                                           + ". The coin goes to the village purse of " + dn + " (" + st.Village.Gold.ToString("N0", Inv) + " now); what you sell is carted to " + dn + ".");
                return true;
            }
            catch (Exception e) { Stumble("TradeCondition", e); return false; }
        }

        /// <summary>Koszyk wioski: dla kazdego towaru magazynu wsi okregu - udzial tej wioski w jego produkcji dnia (VillageCrafts.PartsOf: Pcs czesci / Pcs okregu), zaokraglony.
        /// Ksiega bez czesci - koszyk pusty (gracz moze tylko sprzedac). Liczone przy kliknieciu, raz.</summary>
        private static ItemRoster Basket(Settlement st, string uid, out int kinds, out long pieces, out string how)
        {
            kinds = 0; pieces = 0; how = "-";
            var basket = new ItemRoster();
            var roster = st.ItemRoster;
            if (roster == null || roster.Count == 0) { how = "magazyn wsi pusty"; return basket; }
            var parts = new List<VillageCrafts.PartCraft>();
            bool ks;
            if (!VillageCrafts.PartsOf(st.Village, parts, out ks) || parts.Count == 0) { how = "czesci okregu nieznane - koszyk pusty"; return basket; }
            VillageCrafts.PartCraft mine = null;
            foreach (var p in parts) if (p != null && !p.Main && string.Equals(p.Uid, uid, StringComparison.Ordinal)) { mine = p; break; }
            if (mine == null || mine.Pcs == null || mine.Pcs.Count == 0) { how = "wioska bez produkcji w ksiedze - koszyk pusty"; return basket; }
            for (int i = 0; i < roster.Count; i++)
            {
                var el = roster.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (el.Amount <= 0 || it == null) continue;
                double m;
                if (!mine.Pcs.TryGetValue(it, out m) || !(m > 0)) continue;
                double all = 0;
                foreach (var p in parts) { double x; if (p != null && p.Pcs != null && p.Pcs.TryGetValue(it, out x) && x > 0) all += x; }
                if (!(all > 0)) continue;
                int n = (int)Math.Round(el.Amount * Math.Min(1.0, m / all));
                if (n <= 0) continue;
                if (n > el.Amount) n = el.Amount;
                basket.AddToCounts(el.EquipmentElement, n);
                kinds++; pieces += n;
            }
            how = (ks ? "KS" : "bez KS") + ", towarow wioski " + mine.Pcs.Count;
            return basket;
        }

        private static void TradeConsequence(MenuCallbackArgs a)
        {
            try
            {
                var r = Rec(_uid); var st = DistOf(r);
                if (st == null || !AtVillage()) return;
                var v = st.Village;
                var gsm = Game.Current != null ? Game.Current.GameStateManager : null;
                if (gsm == null || v.Owner == null || Settlement.CurrentSettlement != null) return;   // z osady gra placi sama - tu tylko z mapy
                int kinds; long pieces; string how;
                var basket = Basket(st, _uid, out kinds, out pieces, out how);
                var before = new ItemRoster(basket);
                var listener = new PurseListener(v);
                var state = gsm.CreateState<InventoryState>();
                state.InventoryMode = InventoryScreenHelper.InventoryMode.Trade;
                var logic = new InventoryLogic(v.Owner);
                logic.Initialize(basket, PartyBase.MainParty.ItemRoster, PartyBase.MainParty.MemberRoster, true, true, CharacterObject.PlayerCharacter,
                                 InventoryScreenHelper.InventoryCategoryType.None, v.MarketData, false, state.InventoryMode, new TextObject("{=!}" + NameOf(r)));
                logic.SetInventoryListener(listener);
                state.InventoryLogic = logic;
                int heroGold0 = Hero.MainHero.Gold, vilGold0 = v.Gold;
                string uid = _uid;
                state.DoneLogicExtrasDelegate = delegate { TradeDone(st, uid, logic, listener, basket, before, heroGold0, vilGold0); };
                _dTrade++;
                Log.Info(P + "handel w " + NameOf(r) + " (" + uid + ") - koszyk z magazynu wsi okregu " + st.StringId + ": " + kinds + " rodzajow, " + pieces + " sztuk (" + how
                         + "), ceny targu wsi okregu, kiesa wsi " + vilGold0 + ", gracza " + heroGold0 + ".");
                gsm.PushState(state);
            }
            catch (Exception e) { Stumble("Trade", e); }
        }

        /// <summary>Po udanym DoneLogic: magazyn wsi okregu o roznice koszyka (po - przed), kiesa wsi o to, co gra dala graczowi bez osady (min(-TotalAmount, zloto kupca) -
        /// jak SideVillageHome.MapTradeDone). Kontrola w linii: zmiana magazynu = - zmiana koszyka po stronie gracza.</summary>
        private static void TradeDone(Settlement st, string uid, InventoryLogic logic, PurseListener l, ItemRoster basket, ItemRoster before, int heroGold0, int vilGold0)
        {
            try
            {
                var v = st.Village;
                var store = st.ItemRoster;
                long outP = 0, inP = 0;
                for (int i = 0; i < basket.Count; i++)
                {
                    var el = basket.GetElementCopyAtIndex(i);
                    int bi = before.FindIndexOfElement(el.EquipmentElement);
                    int nb = bi >= 0 ? before.GetElementNumber(bi) : 0;
                    int d = el.Amount - nb;
                    if (d == 0) continue;
                    if (d < 0) { int si = store.FindIndexOfElement(el.EquipmentElement); int have = si >= 0 ? store.GetElementNumber(si) : 0; d = -Math.Min(-d, Math.Max(0, have)); outP += -d; }
                    else inP += d;
                    if (d != 0) store.AddToCounts(el.EquipmentElement, d);
                }
                for (int i = 0; i < before.Count; i++)
                {
                    var el = before.GetElementCopyAtIndex(i);
                    if (el.Amount <= 0 || basket.FindIndexOfElement(el.EquipmentElement) >= 0) continue;
                    int si = store.FindIndexOfElement(el.EquipmentElement);
                    int have = si >= 0 ? store.GetElementNumber(si) : 0;
                    int d = Math.Min(el.Amount, Math.Max(0, have));
                    if (d > 0) { store.AddToCounts(el.EquipmentElement, -d); outP += d; }
                }
                int toPlayer = 0;
                if (Settlement.CurrentSettlement == null || Settlement.CurrentSettlement.SettlementComponent == null)
                {
                    int t = logic.TotalAmount;
                    int g = l.LastGold >= 0 ? l.LastGold : v.Gold;
                    toPlayer = Math.Min(-t, g);                 // to samo, co gra przelala graczowi (bez osady - z niczego)
                    if (toPlayer != 0) v.ChangeGold(-toPlayer);  // kiesa wsi okregu w druga strone
                }
                if (toPlayer < 0) _dBuy += -toPlayer; else _dSell += toPlayer;
                _dPcsOut += outP; _dPcsIn += inP;
                Log.Info(P + "handel w " + NameOf(Rec(uid)) + " zamkniety: gracz " + (toPlayer >= 0 ? "+" : "") + toPlayer + " (kiesa " + heroGold0 + " -> " + Hero.MainHero.Gold + "), wies okregu "
                         + st.StringId + " " + (toPlayer <= 0 ? "+" : "") + (-toPlayer) + " (kiesa " + vilGold0 + " -> " + v.Gold + "); magazyn wsi okregu: wydane " + outP + " sztuk, przyjete " + inP + ".");
            }
            catch (Exception e) { Stumble("TradeDone", e); }
        }

        /// <summary>Kiesa wsi okregu jako kupiec (jak MerchantInventoryListener gry). LastGold - ostatni odczyt (ten, ktorym DoneLogic ograniczyl wyplate).</summary>
        private sealed class PurseListener : InventoryListener
        {
            private readonly Village _v;
            internal int LastGold = -1;
            internal PurseListener(Village v) { _v = v; }
            public override int GetGold() { LastGold = _v.Gold; return LastGold; }
            public override TextObject GetTraderName() { return _v.Name; }
            public override void SetGold(int gold) { _v.ChangeGold(gold - _v.Gold); }
            public override PartyBase GetOppositeParty() { return _v.Owner; }
            public override void OnTransaction() { }
        }

        // ============================================================ sesja, doba, zapis
        internal static void SessionStart()
        {
            try
            {
                var s = Settings.Current;
                var l = VillageBook.LayoutRecords();
                Log.Info(P + "start sesji | klucz Side Villages Enterable " + (On ? "TAK" : "WYLACZONY") + " | wiosek w ukladzie " + (l != null ? l.Count.ToString(Inv) : "- (ksiega nieczynna)")
                         + " | ukrycie partii: Roadside Inn Hide Party " + (s != null && s.RoadsideInns && s.RoadsideInnHideParty ? "tak" : "NIE") + ", nocleg za oplata: Town Inns "
                         + (TownInns.On ? "tak" : "NIE") + ", okup: Raid Tribute " + (RaidTribute.On ? "tak" : "NIE")
                         + ", rabunek: ksiega " + (VillageBook.Active ? "tak" : "NIE") + " | " + RaidSessionText() + " | potkniecia " + _st + " | " + LevySessionText() + ".");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        internal static void Daily()
        {
            try
            {
                int lvAct = LevyDaily();   // m113-pobor: linia "Pobor w wioskach (m113): dzien N" (tylko z ruchem), przyrost mlodych, wygasle braki rak
                ProtSync();   // recenzja 20.10: wygasle ochrony po okupie z migawki ksiegi wiosek
                bool any = _dEnter + _dClick + _dTrade + _dRest + _dCamp + _dInvest + _stDay > 0 || RaidDayAny() || lvAct > 0;
                if (!any) return;
                double ms = _tickN > 0 ? _tickTicks * 1000.0 / Stopwatch.Frequency / _tickN : 0.0;
                Log.Info(P + "dzien " + (Today() - 1) + " | klucz " + (On ? "TAK" : "WYL") + " | wjazdy " + _dEnter + " (kliki " + _dClick + ") | handel " + _dTrade + ": gracz kupil za " + _dBuy
                         + ", sprzedal za " + _dSell + " (kiesy wsi), sztuki z magazynow wsi " + _dPcsOut + ", do magazynow " + _dPcsIn + " | noclegi " + _dRest + " (bez oplaty " + _dRestFree + ")"
                         + ", obozy " + _dCamp + ", okna prac " + _dInvest + " | " + RaidDayText() + " | tick: " + _tickN + " sprawdzen, srednio " + ms.ToString("0.0000", Inv) + " ms"
                         + " | potkniecia doby " + _stDay + ", sesji " + _st + ".");
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { DayZero(); }
        }
    }

    internal static partial class TownInns
    {
        /// <summary>m97-wioski: suma noclegu "wspolna izba" dla partii gracza teraz (ten sam rachunek co zajazd) - napis opcji menu wioski pobocznej. 0 - nie znana.</summary>
        internal static int VisitCost()
        {
            try { return On ? Plan(null, 0, false, false).Total : 0; }
            catch (Exception e) { Stumble("VisitCost", e); return 0; }
        }

        /// <summary>m97-wioski: uid wioski, w ktorej gracz spi teraz pod dachem (stan noclegu bez oplaty, faza 1, partia przy wiosce - HostNear); inaczej null.</summary>
        internal static string HostSleepUidNow()
        {
            var r = _rest;
            return r != null && r.Phase == 1 && r.HostUid != null && HostNear(r) ? r.HostUid : null;
        }
    }
}
