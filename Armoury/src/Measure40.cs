using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// PACZKA E4/0 (188) - POMIAR LUDZI, JENCOW I ZIMY (projekt etapu 4, rozdz. 3 "E4/0"). SAM LOG: zadna latka nie zwraca false, nie zmienia
    /// wyniku, parametrow, hearth, dobrobytu, milicji, ludzi, towaru ani zlota. Okna (prefiks void Priority.First + finalizer; stan w __state):
    ///  W1 tick wsi gry (Village.DailyTick - zastepuje go prefiks BK; nasz First biegnie przed nim, finalizer zawsze): hearth i milicja przed/po,
    ///     wynik modelu hearth (wolany przez nas PRZED ustawieniem InVillageTick - ScorchedEarth go nie liczy) i stala gry (baza pasma, z mnoznikami);
    ///  W2 tick warowni gry (Town.DailyTick): dobrobyt i milicja, osobno miasta i zamki;
    ///  W3/W4 tabor wsi gry (AddVillagersToParty / CreateVillagerParty): hearth przed/po i ludzie;
    ///  W5 lodzie rybackie NavalDLC (OnDailySettlementTick / TryReinforceParty) - typ po nazwie, brak = BRAK;
    ///  W6 rabunek gry (RaidEventComponent.Update): hearth kazdego kroku, wsie, lup z niczego (kiesa wodza);
    ///  W7 dekret BK Growth (VillageDecreeManager.OnDailyTickSettlement) - oczekiwane 0 (dekret tylko z konsoli);
    ///  W10 jency po bitwie (MapEvent.LootDefeatedPartyPrisoners / CaptureDefeatedPartyMembers): odbici i wcieleni do skladu zwyciezcy, wzieci do niewoli;
    ///  W11 jency z rabunku BK (BKRaidCaptureBehavior.ExecuteCapture) - trzecie okno void na tej metodzie.
    /// Okna wpinane ZAWSZE (U26), licza tylko przy On (LogEnabled + PeopleMeasureEnabled). Liczniki modulow sa CZYTANE (U8), nie liczone drugi raz:
    /// od E4/3 (108n) kazdy ruch hearth za ludzi naszych modulow (wyrzutki, powroty z puli i z bitew, rozwiazane tabory, pobor i zadania gracza, siew)
    /// idzie przez ksiege ludzi (PeopleBook.Move) - linia "Hearth" czyta jej hearth wedlug przyczyny (PeopleBook.TakeHearth40) w chwili migawki wsi, wiec
    /// zmiana hearth i licznik sa zawsze w tej samej dobie (dawne TakeOutlaw/TakeFlee/TakeSeed z dopelnieniem "od zerowania do migawki" zdjete). Tabory
    /// i lodzie gry mierza okna W3-W5 (wynik po przeliczeniu 1/k). Zapisy tylko tam, gdzie licznika nie ma: NoteHearth (zerowanie armii ScorchedEarth),
    /// NoteScar (blizna w ticku wsi), NoteDisease (zaraza CampFever - roster przed/po), NoteReturnNoVillage (powrot z puli bez regionu z wsiami).
    /// Kazde Note we wlasnym try z licznikiem potkniec (U24); zadne wywolanie nie stoi miedzy zdjeciem a dopisaniem ludzi. Bez zapisu w grze - liczniki
    /// sesji (po wczytaniu pierwsza doba = migawka).
    /// Linie raz na dobe (przed ksiega ludzi): "Hearth (E4/0)", "Dobrobyt (E4/0)", "Jency (E4/0)", "Zima (E4/0)"; linia startowa "Pomiar ludzi (E4/0)".
    /// "Partie (E4/0)", "Krainy (4.15)", "Straz i Wolni Ludzie (4.15)" pisze PeopleLedger (jego nasluchy), milicje - linia "Zalogi (169c)", Innych - "Wzrost Innych (169c)".
    /// </summary>
    internal static class Measure40
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.LogEnabled && s.PeopleMeasureEnabled; } }
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly object Gate = new object();
        [ThreadStatic] internal static bool InVillageTick;      // W1: prawda tylko miedzy naszym prefiksem a finalizerem ticku wsi gry

        // ------------------------------------------------------------ wpiecie i potkniecia
        private static bool _applied;
        private static readonly List<string> _wired = new List<string>(), _missing = new List<string>();
        private static readonly List<KeyValuePair<string, MethodBase>> _targets = new List<KeyValuePair<string, MethodBase>>();
        private static int _stumbles, _stumblesSess;
        private static readonly Dictionary<string, int> _stumbleAt = new Dictionary<string, int>();
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static bool _startLogged;

        private static void Stumble(string where, Exception e)
        {
            try
            {
                lock (Gate) { _stumbles++; _stumblesSess++; int v; _stumbleAt.TryGetValue(where, out v); _stumbleAt[where] = v + 1; }
                if (_errSites.Add(where)) Log.Error("Measure40." + where, e);
            }
            catch { }
        }

        private static void Wire(Harmony h, string name, MethodBase m, string pre, string fin)
        {
            try
            {
                _targets.Add(new KeyValuePair<string, MethodBase>(name, m));
                if (m == null) { _missing.Add(name); return; }
                h.Patch(m, prefix: new HarmonyMethod(typeof(Measure40), pre) { priority = Priority.First },
                           finalizer: new HarmonyMethod(typeof(Measure40), fin));
                _wired.Add(name);
            }
            catch (Exception e) { _missing.Add(name + " (blad)"); Stumble("Wire(" + name + ")", e); }
        }

        /// <summary>SubModuleMain (po ScorchedEarth.ApplyAll): okna W1-W7, W10, W11 - raz na proces, bez warunku na klucz (U26).</summary>
        internal static void ApplyAll(Harmony h)
        {
            if (_applied || h == null) return;
            _applied = true;
            _wired.Clear(); _missing.Clear(); _targets.Clear();
            Wire(h, "tick wsi", AccessTools.Method(typeof(Village), "DailyTick"), nameof(VillagePre), nameof(VillageFin));
            Wire(h, "tick warowni", AccessTools.Method(typeof(Town), "DailyTick"), nameof(TownPre), nameof(TownFin));
            Wire(h, "tabor wsi - nowy", AccessTools.Method(typeof(VillagerCampaignBehavior), "CreateVillagerParty", new[] { typeof(Village) }), nameof(CartNewPre), nameof(CartFin));
            Wire(h, "tabor wsi - uzupelnienie", AccessTools.Method(typeof(VillagerCampaignBehavior), "AddVillagersToParty", new[] { typeof(MobileParty), typeof(int) }), nameof(CartRefillPre), nameof(CartFin));
            var fish = AccessTools.TypeByName("NavalDLC.CampaignBehaviors.FishingPartyCampaignBehavior");
            Wire(h, "lodz rybacka - nowa", fish != null ? AccessTools.Method(fish, "OnDailySettlementTick", new[] { typeof(Settlement) }) : null, nameof(FishNewPre), nameof(FishNewFin));
            Wire(h, "lodz rybacka - uzupelnienie", fish != null ? AccessTools.Method(fish, "TryReinforceParty") : null, nameof(FishRefillPre), nameof(FishRefillFin));
            Wire(h, "rabunek (krok)", AccessTools.Method(typeof(RaidEventComponent), "Update", new[] { typeof(bool).MakeByRefType() }), nameof(RaidPre), nameof(RaidFin));
            var dec = AccessTools.TypeByName("BannerKings.CampaignContent.Economy.Layered.VillageDecreeManager");
            Wire(h, "dekret BK", dec != null ? AccessTools.Method(dec, "OnDailyTickSettlement", new[] { typeof(Settlement) }) : null, nameof(DecreePre), nameof(DecreeFin));
            Wire(h, "jency po bitwie - odbici", AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyPrisoners"), nameof(LootPre), nameof(LootFin));
            Wire(h, "jency po bitwie - wzieci", AccessTools.Method(typeof(MapEvent), "CaptureDefeatedPartyMembers"), nameof(CapturePre), nameof(CaptureFin));
            var bkr = AccessTools.TypeByName("BannerKings.Behaviours.Raids.BKRaidCaptureBehavior");
            Wire(h, "jency z rabunku BK", bkr != null ? AccessTools.Method(bkr, "ExecuteCapture") : null, nameof(BkCapPre), nameof(BkCapFin));
            Log.Info("Pomiar ludzi (E4/0): okna wpiete przy starcie gry " + _wired.Count + " z " + _targets.Count + " (" + string.Join(", ", _wired.ToArray()) + "); BRAK: "
                     + (_missing.Count > 0 ? string.Join(", ", _missing.ToArray()) : "-") + "; liczone tylko przy LogEnabled + PeopleMeasureEnabled; pelna linia z wlascicielami latek - w pierwszej dobie kampanii.");
        }

        // ------------------------------------------------------------ stan doby
        // W1 tick wsi
        private static int _vTickN, _vFloorN, _vOddN;
        private static double _vTick, _vBase, _vBaseMul, _vModel, _vScar, _vFloor, _vOdd, _milV, _milT, _milC;
        // E4/4 (109n): przyrost naturalny i powrot uchodzcow w ticku wsi (rachunek PopulationLaw.GrowthPostfix tej odpowiedzi modelu); _vExpl - czesc wyniku
        // modelu wyjasniona w ticku (przyrost + powrot, a przy wylaczonym przyroscie stala gry z mnoznikami) - reszta modelu = wynik - _vExpl - blizna
        private static int _vGrowN;
        private static double _vNat, _vRef, _vExpl;
        private static int _dupV; private static string _dupTxt;   // wsie powtorzone w Settlement.All (migawka liczy kazda raz) - z ostatniej linii Hearth
        private static long _modelTicks;
        // W2 tick warowni
        private static double _pTown, _pCastle, _pTownPeople;
        // W3-W5 tabory i lodzie, W6 rabunek, W7 dekret
        private static int _cartNewN, _cartRefN, _cartNewMen, _cartRefMen, _fishNewN, _fishRefN, _fishNewMen, _fishRefMen, _raidSteps, _decreeN;
        private static double _cartNewH, _cartRefH, _fishNewH, _fishRefH, _raidH, _decreeH;
        private static long _raidGoldAi, _raidGoldPlayer;
        private static readonly HashSet<Village> _raidVill = new HashSet<Village>();
        // budzet wedlug regionu (do ludzie-regiony.csv) i ludzie tabeli wedlug okna
        private const int BTick = 0, BCarts = 1, BRaid = 2, BDecree = 3, BKinds = 4;
        private static readonly Dictionary<Settlement, float[]> _regDay = new Dictionary<Settlement, float[]>();
        private static readonly double[] _peopleDay = new double[BKinds];
        private static readonly Dictionary<string, float[]> _regOut = new Dictionary<string, float[]>();
        // przekazania z modulow (U8): od E4/3 hearth ruchow ludzi z ksiegi (PeopleBook.TakeHearth40 w Daily)
        private static float _noVillage, _forage; private static int _forageN;
        // W10 / W11 / zaraza
        private const int PLord = 0, PGarrison = 1, PMilitia = 2, PBandit = 3, POthers = 4, PPlayer = 5, POther = 6, PKinds = 7;
        private static readonly string[] PName = { "partie rodow", "zalogi", "milicje", "bandy", "Inni", "gracz", "inne" };
        private static readonly int[] _taken = new int[PKinds], _rescued = new int[PKinds];
        private static int _lootRemoved, _lootBattles, _capBattles, _bkRaidPris, _bkRaidPlayer, _bkRaidN;
        private static int _rescueFallen;   // E4/2 (f): odbici przez Innych - zdjeci ze skladu bandy jako polegli PRZED finalizerem W10 (OthersRise.RescueFin)
        private static readonly Dictionary<string, int> _diseaseByCulture = new Dictionary<string, int>();
        private static readonly int[] _diseaseByGroup = new int[PeopleLedger.Groups];
        private static int _diseaseDead, _diseaseUnhit;

        // ------------------------------------------------------------ migawki miedzy dobami (bez zapisu)
        private static readonly Dictionary<Village, float> _hLast = new Dictionary<Village, float>(), _kLast = new Dictionary<Village, float>();
        private static readonly Dictionary<Town, float> _pLast = new Dictionary<Town, float>();
        private static readonly Dictionary<Settlement, string> _cultLast = new Dictionary<Settlement, string>(), _cultStart = new Dictionary<Settlement, string>();
        private static int _prisLastP = -1, _prisLastL = -1;
        private static double _food0 = -1;

        internal static void Reset()
        {
            lock (Gate)
            {
                ZeroDay();
                _hLast.Clear(); _kLast.Clear(); _pLast.Clear(); _cultLast.Clear(); _cultStart.Clear(); _regOut.Clear();
                _diseaseByCulture.Clear(); Array.Clear(_diseaseByGroup, 0, _diseaseByGroup.Length); _diseaseDead = _diseaseUnhit = 0;
                _prisLastP = _prisLastL = -1; _food0 = -1;
                _stumbles = _stumblesSess = 0; _stumbleAt.Clear(); _errSites.Clear(); _startLogged = false;
                _dupV = 0; _dupTxt = null;
            }
        }

        private static void ZeroDay()
        {
            _vTickN = _vFloorN = _vOddN = 0; _vTick = _vBase = _vBaseMul = _vModel = _vScar = _vFloor = _vOdd = 0; _milV = _milT = _milC = 0; _modelTicks = 0;
            _vGrowN = 0; _vNat = _vRef = _vExpl = 0;
            _pTown = _pCastle = _pTownPeople = 0;
            _cartNewN = _cartRefN = _cartNewMen = _cartRefMen = _fishNewN = _fishRefN = _fishNewMen = _fishRefMen = _raidSteps = _decreeN = 0;
            _cartNewH = _cartRefH = _fishNewH = _fishRefH = _raidH = _decreeH = 0; _raidGoldAi = _raidGoldPlayer = 0; _raidVill.Clear();
            _regDay.Clear(); Array.Clear(_peopleDay, 0, BKinds);
            _noVillage = 0f; _forage = 0f; _forageN = 0;
            Array.Clear(_taken, 0, PKinds); Array.Clear(_rescued, 0, PKinds); _lootRemoved = _lootBattles = _capBattles = _bkRaidPris = _bkRaidPlayer = _bkRaidN = 0; _rescueFallen = 0;
        }

        // ------------------------------------------------------------ pomocnicze (tylko odczyt)
        private static string S(double v) { return (v >= 0 ? "+" : "") + v.ToString("0.00", Inv); }
        private static string S0(double v) { return (v >= 0 ? "+" : "") + v.ToString("0", Inv); }
        private static string F2(double v) { return v.ToString("0.00", Inv); }

        /// <summary>Ludzi tabeli na punkt hearth wsi / dobrobytu miasta tej osady - od E4/3 k na osade (PopulationLaw.KFor; przy wylaczonej jednostce k kultury, jak dotad).</summary>
        private static float KOf(Settlement st, bool town)
        {
            try { return st != null ? PopulationLaw.KFor(st) : 0f; } catch { return 0f; }
        }

        /// <summary>Kultura osady z pierwszej doby sesji (U3) - migawka przy pierwszym odczycie w kampanii; bez migawki biezaca.</summary>
        internal static string CultureAtStart(Settlement st)
        {
            if (st == null) return "?";
            try
            {
                EnsureCultures();
                string c;
                if (_cultStart.TryGetValue(st, out c)) return c;
                return st.Culture != null ? st.Culture.StringId : "?";
            }
            catch { return "?"; }
        }

        private static void EnsureCultures()
        {
            if (_cultStart.Count > 0 || Campaign.Current == null) return;
            foreach (var st in Settlement.All)
                if (st != null && (st.IsVillage || st.IsTown || st.IsCastle)) _cultStart[st] = st.Culture != null ? st.Culture.StringId : "?";
        }

        internal static int MenOf(MobileParty mp)
        {
            try { return mp != null && mp.MemberRoster != null ? mp.MemberRoster.TotalManCount : 0; } catch { return 0; }
        }

        private static int KindOf(PartyBase pb)
        {
            var mp = pb != null ? pb.MobileParty : null;
            if (mp == null) return POther;
            if (mp.IsMainParty) return PPlayer;
            if (Undead.Party(mp)) return POthers;
            if (mp.IsLordParty) return PLord;
            if (mp.IsGarrison) return PGarrison;
            if (mp.IsMilitia) return PMilitia;
            if (mp.IsBandit) return PBandit;
            return POther;
        }

        private static void AddVillage(Village v, int kind, float dH)   // pod Gate
        {
            if (v == null || dH == 0f) return;
            var st = v.Settlement;
            var reg = st != null ? OutlawLaw.RegionFor(st) : null;
            if (reg != null)
            {
                float[] a;
                if (!_regDay.TryGetValue(reg, out a)) { a = new float[BKinds]; _regDay[reg] = a; }
                a[kind] += dH;
            }
            _peopleDay[kind] += (double)dH * KOf(st, false);
        }

        // ============================================================ W1: tick wsi gry
        internal sealed class VState { public Village V; public float H, Mil, Result, Base, BaseMul, Nat, Ref; public bool Grow; }

        public static void VillagePre(Village __instance, out VState __state)
        {
            __state = null;
            InVillageTick = false;
            try
            {
                if (!On || __instance == null || Campaign.Current == null) return;
                var st = __instance.Settlement;
                var w = new VState { V = __instance, H = __instance.Hearth, Mil = st != null ? st.Militia : 0f };
                long t0 = Stopwatch.GetTimestamp();
                var en = Campaign.Current.Models.SettlementProsperityModel.CalculateHearthChange(__instance, false);   // czysty rachunek - InVillageTick jeszcze false
                long dt = Stopwatch.GetTimestamp() - t0;
                w.Result = en.ResultNumber;
                // E4/4: rozklad tej odpowiedzi na przyrost naturalny i powrot uchodzcow (tylko odczyt pamieci postfiksu przyrostu z tego samego pytania)
                try { float nat, rf; w.Grow = PopulationLaw.TakeLast(__instance, out nat, out rf); w.Nat = nat; w.Ref = rf; } catch { w.Grow = false; }
                var vs = __instance.VillageState;
                w.Base = vs == Village.VillageStates.Normal ? (w.H < 300f ? 4f : (w.H < 600f ? 1.2f : 0.2f)) : (vs == Village.VillageStates.Looted ? -1f : 0f);
                w.BaseMul = w.Base * (1f + en.SumOfFactors);
                lock (Gate) _modelTicks += dt;
                __state = w;
                InVillageTick = true;
            }
            catch (Exception e) { __state = null; InVillageTick = false; Stumble("VillagePre", e); }
        }

        public static Exception VillageFin(Exception __exception, VState __state)
        {
            InVillageTick = false;
            try
            {
                if (__state == null) return __exception;
                var v = __state.V;
                float dH = v.Hearth - __state.H;
                // E4/8: hearth przesuniety przez ksiege w postfiksie milicji TEJ wsi (pobor milicji, powrot milicji do domu) - jest w "nasze" (o13), nie w ticku gry
                // (odczyt bez zerowania - ten sam odczyt robi podloga E4/4 w FloorFinalizer; recenzja E4/8 R1-5/R2-6)
                try { dH -= (float)ArmsFromPeople.PeekTickHearth(v); } catch { }
                float rest = dH - __state.Result;                 // podloga 10 (dodatnia) albo niezgodnosc z wynikiem modelu
                var st = v.Settlement;
                float dMil = st != null ? st.Militia - __state.Mil : 0f;
                lock (Gate)
                {
                    _vTickN++; _vTick += dH; _vBase += __state.Base; _vBaseMul += __state.BaseMul; _vModel += __state.Result;
                    if (__state.Grow) { _vGrowN++; _vNat += __state.Nat; _vRef += __state.Ref; _vExpl += __state.Nat + __state.Ref; }
                    else _vExpl += __state.BaseMul;
                    if (rest > 0.0001f) { _vFloor += rest; _vFloorN++; }
                    else if (rest < -0.0001f) { _vOdd += rest; _vOddN++; }
                    _milV += dMil;
                    AddVillage(v, BTick, dH);
                }
            }
            catch (Exception e) { Stumble("VillageFin", e); }
            return __exception;
        }

        /// <summary>ScorchedEarth.HearthScarPostfix: o ile blizna / uchodzcy zmienili wynik modelu - tylko w ticku wsi gry (InVillageTick), nie w naszym odczycie.</summary>
        internal static void NoteScar(float d)
        {
            try { if (!InVillageTick || d == 0f) return; lock (Gate) _vScar += d; }
            catch (Exception e) { Stumble("NoteScar", e); }
        }

        // ============================================================ W2: tick warowni gry
        internal sealed class TState { public Town T; public float P, Mil; }

        public static void TownPre(Town __instance, out TState __state)
        {
            __state = null;
            try
            {
                if (!On || __instance == null) return;
                var st = __instance.Settlement;
                __state = new TState { T = __instance, P = __instance.Prosperity, Mil = st != null ? st.Militia : 0f };
            }
            catch (Exception e) { __state = null; Stumble("TownPre", e); }
        }

        public static Exception TownFin(Exception __exception, TState __state)
        {
            try
            {
                if (__state == null) return __exception;
                var t = __state.T; var st = t.Settlement;
                float dP = t.Prosperity - __state.P;
                float dMil = st != null ? st.Militia - __state.Mil : 0f;
                bool town = st != null && st.IsTown;
                float k = town ? KOf(st, true) : 0f;
                lock (Gate)
                {
                    if (town) { _pTown += dP; _milT += dMil; _pTownPeople += (double)dP * k; }
                    else { _pCastle += dP; _milC += dMil; }
                }
            }
            catch (Exception e) { Stumble("TownFin", e); }
            return __exception;
        }

        // ============================================================ W3-W5: tabory wsi i lodzie rybackie
        internal sealed class CState { public Village V; public MobileParty P; public object Comp; public float H; public int Men; public bool New, Boat; }

        public static void CartNewPre(Village __0, out CState __state)
        {
            __state = null;
            try { if (On && __0 != null) __state = new CState { V = __0, H = __0.Hearth, Comp = __0.VillagerPartyComponent, New = true }; }
            catch (Exception e) { __state = null; Stumble("CartNewPre", e); }
        }

        public static void CartRefillPre(MobileParty __0, out CState __state)
        {
            __state = null;
            try
            {
                if (!On || __0 == null) return;
                var home = __0.HomeSettlement; var v = home != null ? home.Village : null;
                if (v == null) return;
                __state = new CState { V = v, P = __0, H = v.Hearth, Men = MenOf(__0) };
            }
            catch (Exception e) { __state = null; Stumble("CartRefillPre", e); }
        }

        public static void FishNewPre(Settlement __0, out CState __state)
        {
            __state = null;
            try { var v = __0 != null ? __0.Village : null; if (On && v != null) __state = new CState { V = v, H = v.Hearth, Comp = v.VillagerPartyComponent, New = true, Boat = true }; }
            catch (Exception e) { __state = null; Stumble("FishNewPre", e); }
        }

        public static void FishRefillPre(object __0, out CState __state)
        {
            __state = null;
            try
            {
                var comp = __0 as VillagerPartyComponent;
                if (!On || comp == null || comp.Village == null) return;
                __state = new CState { V = comp.Village, P = comp.MobileParty, H = comp.Village.Hearth, Men = MenOf(comp.MobileParty), Boat = true };
            }
            catch (Exception e) { __state = null; Stumble("FishRefillPre", e); }
        }

        public static Exception CartFin(Exception __exception, CState __state) { Settle(__state, "CartFin"); return __exception; }
        public static Exception FishRefillFin(Exception __exception, CState __state) { Settle(__state, "FishRefillFin"); return __exception; }

        public static Exception FishNewFin(Exception __exception, Settlement __0, CState __state)
        {
            try
            {
                if (__state == null) return __exception;
                if (Math.Abs(__state.H - __state.V.Hearth) < 0.00001f) return __exception;   // lodz nie zwodowana (co dobe dla kazdej wsi)
                // nowa lodz = ostatnia partia tej wsi na liscie taborow gry, ktora nie jest jej taborem ladowym (jak 108)
                var all = MobileParty.AllVillagerParties;
                for (int i = all.Count - 1; i >= 0; i--)
                {
                    var p = all[i];
                    if (p == null || p.PartyComponent == null || p.HomeSettlement != __0) continue;
                    if (ReferenceEquals(p.PartyComponent, __state.V.VillagerPartyComponent)) continue;
                    __state.P = p; break;
                }
            }
            catch (Exception e) { Stumble("FishNewFin(szukanie)", e); }
            Settle(__state, "FishNewFin");
            return __exception;
        }

        private static void Settle(CState w, string where)
        {
            try
            {
                if (w == null) return;
                float taken = w.H - w.V.Hearth;
                int men;
                if (w.New)
                {
                    MobileParty born = w.P;
                    if (born == null && !w.Boat)
                    {
                        var comp = w.V.VillagerPartyComponent;
                        born = comp != null && !ReferenceEquals(comp, w.Comp) ? comp.MobileParty : null;
                    }
                    men = MenOf(born);
                }
                else men = MenOf(w.P) - w.Men;
                if (Math.Abs(taken) < 0.00001f && men == 0) return;
                lock (Gate)
                {
                    if (w.Boat) { if (w.New) { _fishNewN++; _fishNewH += taken; _fishNewMen += men; } else { _fishRefN++; _fishRefH += taken; _fishRefMen += men; } }
                    else { if (w.New) { _cartNewN++; _cartNewH += taken; _cartNewMen += men; } else { _cartRefN++; _cartRefH += taken; _cartRefMen += men; } }
                    AddVillage(w.V, BCarts, -taken);
                }
            }
            catch (Exception e) { Stumble(where, e); }
        }

        // ============================================================ W6: rabunek gry (krok)
        internal sealed class RState { public Village V; public float H; public Hero Leader; public int Gold; }

        public static void RaidPre(RaidEventComponent __instance, out RState __state)
        {
            __state = null;
            try
            {
                if (!On || __instance == null) return;
                var me = __instance.MapEvent;
                // Update biegnie co klatke kampanii; hearth zmienia gra tylko w fazie lupu (DefenderSide.TroopCount == 0, dec Raid.cs:162) - w walce z milicja
                // wychodzimy przed odczytami i alokacja (ten sam czysty odczyt TroopCount gra robi zaraz potem)
                var def = me != null ? me.DefenderSide : null;
                if (def == null || def.TroopCount > 0) return;
                var st = me.MapEventSettlement;
                if (st == null || !st.IsVillage || st.Village == null) return;
                // E4/5: krok pod spustoszeniem - hearth rusza tylko ksiega (PeopleBook.Move MDevRaid), kroki i lup liczy Devastation (StepsToday). Bez tego finalizer
                // W6 liczylby przywrocony hearth jako "rabunek gry" i ten sam ruch drugi raz z ksiegi (albo smieci, gdyby pobiegl przed finalizerem Devastation)
                if (Devastation.CoversQuiet(st.Village)) return;
                Hero leader = null;
                try { var lp = me.AttackerSide != null ? me.AttackerSide.LeaderParty : null; leader = lp != null ? lp.LeaderHero : null; } catch { leader = null; }
                __state = new RState { V = st.Village, H = st.Village.Hearth, Leader = leader, Gold = leader != null ? leader.Gold : 0 };
            }
            catch (Exception e) { __state = null; Stumble("RaidPre", e); }
        }

        public static Exception RaidFin(Exception __exception, RState __state)
        {
            try
            {
                if (__state == null) return __exception;
                float taken = __state.H - __state.V.Hearth;
                if (Math.Abs(taken) < 0.00001f) return __exception;
                long gold = __state.Leader != null ? __state.Leader.Gold - __state.Gold : 0;
                lock (Gate)
                {
                    _raidSteps++; _raidH += taken; _raidVill.Add(__state.V);
                    if (gold > 0) { if (__state.Leader == Hero.MainHero) _raidGoldPlayer += gold; else _raidGoldAi += gold; }
                    AddVillage(__state.V, BRaid, -taken);
                }
            }
            catch (Exception e) { Stumble("RaidFin", e); }
            return __exception;
        }

        // ============================================================ W7: dekret BK Growth
        internal sealed class DState { public Village V; public float H; }

        public static void DecreePre(Settlement __0, out DState __state)
        {
            __state = null;
            try { var v = __0 != null ? __0.Village : null; if (On && v != null) __state = new DState { V = v, H = v.Hearth }; }
            catch (Exception e) { __state = null; Stumble("DecreePre", e); }
        }

        public static Exception DecreeFin(Exception __exception, DState __state)
        {
            try
            {
                if (__state == null) return __exception;
                float d = __state.V.Hearth - __state.H;
                if (Math.Abs(d) < 0.00001f) return __exception;
                lock (Gate) { _decreeN++; _decreeH += d; AddVillage(__state.V, BDecree, d); }
            }
            catch (Exception e) { Stumble("DecreeFin", e); }
            return __exception;
        }

        // ============================================================ W10: jency po bitwie
        internal sealed class LState
        {
            public readonly List<TroopRoster> R = new List<TroopRoster>(4); public readonly List<int> Kind = new List<int>(4), Before = new List<int>(4);
            public readonly List<TroopRoster> Def = new List<TroopRoster>(4); public int DefBefore;
        }

        private static bool Has(List<TroopRoster> l, TroopRoster r) { for (int i = 0; i < l.Count; i++) if (ReferenceEquals(l[i], r)) return true; return false; }

        private static void AddReceivers(LState w, MBReadOnlyList<MapEventParty> winners, bool prisoners)
        {
            for (int i = 0; i < winners.Count; i++)
            {
                try
                {
                    var mep = winners[i];
                    if (mep == null || mep.Party == null) continue;
                    if (!mep.IsNpcParty) continue;      // gracz: PlayerEncounter tworzy te rostery leniwie (pole w zapisie) - nie dotykamy; jego jency ida przez ekran lupu, poza oknem
                    var r = prisoners ? mep.RosterToReceiveLootPrisoners : mep.RosterToReceiveLootMembers;
                    if (r == null || Has(w.R, r)) continue;
                    int kind = KindOf(mep.Party);
                    // E4/2 (f): u band Innych "odbici wcieleni" to tylko ZYWI (wighty z odbitych dopisuje OthersRise przed tym finalizerem - sa w linii E4/2)
                    w.R.Add(r); w.Kind.Add(kind); w.Before.Add(kind == POthers && !prisoners ? OthersRise.LivingRegulars(r) : r.TotalRegulars);
                }
                catch (Exception e) { Stumble("W10(odbiorca)", e); }
            }
        }

        public static void LootPre(MBReadOnlyList<MapEventParty> __0, MBReadOnlyList<MapEventParty> __1, out LState __state)
        {
            __state = null;
            try
            {
                if (!On || __0 == null || __1 == null) return;
                var w = new LState();
                for (int i = 0; i < __1.Count; i++)
                {
                    var pr = __1[i] != null && __1[i].Party != null ? __1[i].Party.PrisonRoster : null;
                    if (pr == null || Has(w.Def, pr)) continue;
                    w.Def.Add(pr); w.DefBefore += pr.TotalRegulars;
                }
                if (w.DefBefore <= 0) return;                     // pokonani bez szeregowych jencow - nic do odbicia
                AddReceivers(w, __0, false);
                __state = w;
            }
            catch (Exception e) { __state = null; Stumble("LootPre", e); }
        }

        public static Exception LootFin(Exception __exception, LState __state)
        {
            try
            {
                if (__state == null) return __exception;
                int after = 0;
                foreach (var pr in __state.Def) after += pr.TotalRegulars;
                lock (Gate)
                {
                    _lootBattles++; _lootRemoved += __state.DefBefore - after;
                    for (int i = 0; i < __state.R.Count; i++)
                    {
                        int now = __state.Kind[i] == POthers ? OthersRise.LivingRegulars(__state.R[i]) : __state.R[i].TotalRegulars;   // E4/2: Inni - zywi
                        int d = now - __state.Before[i];
                        if (d > 0) _rescued[__state.Kind[i]] += d;
                    }
                }
            }
            catch (Exception e) { Stumble("LootFin", e); }
            return __exception;
        }

        public static void CapturePre(MBReadOnlyList<MapEventParty> __0, out LState __state)
        {
            __state = null;
            try
            {
                if (!On || __0 == null) return;
                var w = new LState();
                AddReceivers(w, __0, true);
                if (w.R.Count > 0) __state = w;
            }
            catch (Exception e) { __state = null; Stumble("CapturePre", e); }
        }

        public static Exception CaptureFin(Exception __exception, LState __state)
        {
            try
            {
                if (__state == null) return __exception;
                bool any = false;
                lock (Gate)
                {
                    for (int i = 0; i < __state.R.Count; i++)
                    {
                        int d = __state.R[i].TotalRegulars - __state.Before[i];
                        if (d > 0) { _taken[__state.Kind[i]] += d; any = true; }
                    }
                    if (any) _capBattles++;
                }
            }
            catch (Exception e) { Stumble("CaptureFin", e); }
            return __exception;
        }

        /// <summary>Wzrost Innych (169c): odbici jency wcieleni do band Innych w tej dobie (zywi ludzie - dzis w "reszcie" linii 169c; U6).</summary>
        internal static int RescuedToOthers { get { lock (Gate) return _rescued[POthers]; } }
        /// <summary>E4/2 (f): odbici jency zdjeci ze skladu bandy Innych jako polegli (OthersRise) - w linii Jency osobno, nie w "bez odbiorcy".</summary>
        internal static void NoteRescuedFallen(int n) { try { if (!On || n <= 0) return; lock (Gate) _rescueFallen += n; } catch (Exception e) { Stumble("NoteRescuedFallen", e); } }
        internal static int TakenByOthers { get { lock (Gate) return _taken[POthers]; } }

        // ============================================================ W11: jency z rabunku BK
        // Recenzja E4/9 (R1-1 = R2-1): prefiks W11 i ThrallLaw.CapturePrefix maja Priority.First, a W11 jest wpiety wczesniej (nizszy indeks) - Harmony 2.4.2 uruchamia
        // go PIERWSZEGO, wiec migawka lochu bylaby sprzed naszych pojmanych i finalizer policzylby ich drugi raz jako "z rabunku BK". Przy czynnym prawie (nasz prefiks
        // zawsze zwraca false - BK nie dopisuje nikogo) okno nie liczy nic; pojmanych liczy tylko odcinek "pojmani z wsi (E4/9)". Kolejnosci NIE odwracac: prefiks void
        // z parametrem typu referencyjnego po false jest pomijany (MethodCreatorTools.AffectsOriginal), __state zostalby 0 i finalizer wzialby caly loch najezdzcy.
        public static void BkCapPre(MobileParty __0, out int __state)
        {
            __state = -1;
            try
            {
                if (ThrallLaw.Patched && Devastation.CaptivesLive) return;
                if (On && __0 != null && __0.PrisonRoster != null) __state = __0.PrisonRoster.TotalRegulars;
            }
            catch (Exception e) { __state = -1; Stumble("BkCapPre", e); }
        }

        public static Exception BkCapFin(Exception __exception, MobileParty __0, int __state)
        {
            try
            {
                if (__state < 0 || __0 == null || __0.PrisonRoster == null) return __exception;
                int d = __0.PrisonRoster.TotalRegulars - __state;
                if (d <= 0) return __exception;
                lock (Gate) { _bkRaidN++; _bkRaidPris += d; if (__0 == MobileParty.MainParty) _bkRaidPlayer += d; }
            }
            catch (Exception e) { Stumble("BkCapFin", e); }
            return __exception;
        }

        // ============================================================ przekazania z modulow (U8: czytane, nie liczone drugi raz)
        internal const int SrcForage = 0;

        /// <summary>ScorchedEarth: zerowanie armii (tu licznika nie ma) - zmiana hearth wsi (ujemna).</summary>
        internal static void NoteHearth(int src, float delta, Settlement st)
        {
            try
            {
                if (!On || delta == 0f || st == null || st.Village == null) return;
                lock (Gate) { _forage += delta; _forageN++; }
            }
            catch (Exception e) { Stumble("NoteHearth", e); }
        }

        /// <summary>OutlawLaw.Daily: powrot z puli bez regionu z wsiami - ludzie zeszli z puli, ReturnHome nic nie dopisal (od E4/3 region bez wsi oddaje do najblizszego z wsiami).</summary>
        internal static void NoteReturnNoVillage(float men)
        {
            try { if (men > 0f) lock (Gate) _noVillage += men; }
            catch (Exception e) { Stumble("NoteReturnNoVillage", e); }
        }

        /// <summary>CampFever po kazdym Strike: zmarli z rosteru (przed - po) i zadani (dead) - roznica = nietrafione HitRandom (U9).</summary>
        internal static void NoteDisease(MobileParty mp, int died, int planned)
        {
            try
            {
                if (!On || mp == null || (died <= 0 && planned <= 0)) return;
                int n = Math.Max(0, died);
                string c = "?";
                try { c = CultureAtStart(OutlawLaw.RegionAt(mp.GetPosition2D)); } catch { c = "?"; }
                int g = PeopleLedger.GroupOf(mp.MapFaction, mp);
                lock (Gate)
                {
                    _diseaseDead += n; _diseaseUnhit += Math.Max(0, planned - n);
                    int v; _diseaseByCulture.TryGetValue(c, out v); _diseaseByCulture[c] = v + n;
                    if (g >= 0 && g < _diseaseByGroup.Length) _diseaseByGroup[g] += n;
                }
            }
            catch (Exception e) { Stumble("NoteDisease", e); }
        }

        /// <summary>PeopleLedger (linie "Krainy" i "Straz i Wolni Ludzie"): zmarli z zarazy od ostatniego odczytu - kopie i zerowanie.</summary>
        internal static void TakeDisease(Dictionary<string, int> byCulture, int[] byGroup, out int dead, out int unhit)
        {
            dead = 0; unhit = 0;
            try
            {
                lock (Gate)
                {
                    foreach (var kv in _diseaseByCulture) byCulture[kv.Key] = kv.Value;
                    for (int i = 0; i < byGroup.Length && i < _diseaseByGroup.Length; i++) byGroup[i] = _diseaseByGroup[i];
                    dead = _diseaseDead; unhit = _diseaseUnhit;
                    _diseaseByCulture.Clear(); Array.Clear(_diseaseByGroup, 0, _diseaseByGroup.Length); _diseaseDead = _diseaseUnhit = 0;
                }
            }
            catch (Exception e) { Stumble("TakeDisease", e); }
        }

        /// <summary>Linia "Zalogi (169c)" - milicja z modelu w tickach osad od ostatniej linii (wsie, miasta, zamki); zeruje.</summary>
        internal static void TakeMilitia(out double v, out double t, out double c)
        {
            lock (Gate) { v = _milV; t = _milT; c = _milC; _milV = _milT = _milC = 0; }
        }

        /// <summary>ludzie-regiony.csv: budzet hearth regionu z ostatniej doby (tick, tabory i lodzie, rabunek, inne = zmiana - okna) albo null.</summary>
        internal static float[] RegionBudget(Settlement region)
        {
            try { float[] a; lock (Gate) return region != null && _regOut.TryGetValue(region.StringId, out a) ? a : null; }
            catch { return null; }
        }

        // ============================================================ raz na dobe (przed PeopleLedger.Daily)
        internal static void Daily()
        {
            long t0 = Stopwatch.GetTimestamp();
            if (Campaign.Current == null) return;
            // E4/3: hearth ruchow ludzi z ksiegi od ostatniej migawki (zawsze - bez tego wlaczenie klucza w trakcie dawaloby zla dobe)
            var mv = new double[PeopleBook.MKinds];
            try { PeopleBook.TakeHearth40(mv); } catch (Exception e) { Stumble("Daily(PeopleBook)", e); }
            if (!On)
            {
                lock (Gate)
                {
                    ZeroDay(); _hLast.Clear(); _kLast.Clear(); _pLast.Clear(); _cultLast.Clear(); _regOut.Clear();
                    _diseaseByCulture.Clear(); Array.Clear(_diseaseByGroup, 0, _diseaseByGroup.Length); _diseaseDead = _diseaseUnhit = 0;
                    _milV = _milT = _milC = 0; _prisLastP = _prisLastL = -1;
                }
                return;
            }
            int day = (int)CampaignTime.Now.ToDays - 1;
            if (!_startLogged) { _startLogged = true; try { Log.Info(StartLine()); } catch (Exception e) { Stumble("StartLine", e); } }
            try { EnsureCultures(); } catch (Exception e) { Stumble("EnsureCultures", e); }
            string cultTxt = "-"; double cultPeople = 0; int cultN = 0;
            try { cultTxt = CultureChanges(out cultN, out cultPeople); } catch (Exception e) { Stumble("CultureChanges", e); }
            try { Log.Info(HearthLine(day, mv, cultN, cultTxt, cultPeople, t0)); } catch (Exception e) { Stumble("HearthLine", e); }
            try { Log.Info(ProsperityLine(day)); } catch (Exception e) { Stumble("ProsperityLine", e); }
            try { Log.Info(PrisonersLine(day)); } catch (Exception e) { Stumble("PrisonersLine", e); }
            try { Log.Info(WinterLine(day)); } catch (Exception e) { Stumble("WinterLine", e); }
            lock (Gate)
            {
                ZeroDay();
                if (!Measure169c.LinesOn) _milV = _milT = _milC = 0;   // milicje czyta linia "Zalogi (169c)" (biegnie wczesniej) - bez niej zerujemy tu
            }
            if (_stumbles > 0)
            {
                try
                {
                    var parts = new List<string>();
                    lock (Gate) foreach (var kv in _stumbleAt) parts.Add(kv.Key + " " + kv.Value);
                    Log.Info("Pomiar ludzi (E4/0): potkniecia dzis " + _stumbles + " (sesja " + _stumblesSess + "; " + string.Join(", ", parts.ToArray()) + ") - pierwszy blad kazdego miejsca w logu.");
                }
                catch { }
                lock (Gate) { _stumbles = 0; _stumbleAt.Clear(); }
            }
        }

        private static string Owners(MethodBase m)
        {
            try
            {
                if (m == null) return "brak metody";
                var info = Harmony.GetPatchInfo(m);
                if (info == null) return "bez latek";
                var o = new List<string>();
                foreach (var x in info.Owners) if (!o.Contains(x)) o.Add(x);
                return o.Count > 0 ? string.Join(" + ", o.ToArray()) : "bez latek";
            }
            catch (Exception e) { return "blad (" + e.Message + ")"; }
        }

        private static string StartLine()
        {
            var sb = new StringBuilder(1500);
            sb.Append("Pomiar ludzi (E4/0): okna wpiete: ").Append(_wired.Count > 0 ? string.Join(", ", _wired.ToArray()) : "-")
              .Append("; BRAK: ").Append(_missing.Count > 0 ? string.Join(", ", _missing.ToArray()) : "-")
              .Append("; wlasciciele latek na ").Append(_targets.Count).Append(" celach: ");
            for (int i = 0; i < _targets.Count; i++) sb.Append(i > 0 ? "; " : "").Append(_targets[i].Key).Append(": ").Append(Owners(_targets[i].Value));
            var s = Settings.Current;
            bool d169 = s != null && s.ClanIncomeBookEnabled && s.ClanIncomeBookStableD;
            sb.Append(" | dane 169c (niewolni BK w \"Krainy\", Inni w \"Wzrost Innych\", milicja w \"Zalogi\"): ").Append(d169 ? "TAK" : "BRAK").Append(" - ClanIncomeBookEnabled + StableD")
              .Append(" | przekazania licznikow: ksiega ludzi PeopleBook (E4/3: wyrzutki, powroty z puli i z bitew, rozwiazane tabory, pobor i zadania gracza, siew), ScorchedEarth (zerowanie, blizna), CampFever (zaraza)")
              .Append(" | linie raz na dobe przy LogEnabled + PeopleMeasureEnabled (teraz: ").Append(On ? "TAK" : "NIE").Append(").");
            return sb.ToString();
        }

        /// <summary>Zmiany kultury osad od wczoraj (U3) - lista i ludzie tabeli, ktorzy przez to "przybyli" (hearth/dobrobyt bez zmian, inne k).</summary>
        private static string CultureChanges(out int n, out double people)
        {
            n = 0; people = 0;
            var list = new List<string>();
            bool first = _cultLast.Count == 0;
            bool frozen = PeopleUnit.On;      // E4/3 (U3): k na osade - zmiana kultury nie zmienia ludzi
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null || !(st.IsVillage || st.IsTown || st.IsCastle)) continue;
                    string c = st.Culture != null ? st.Culture.StringId : "?";
                    string old;
                    if (!first && _cultLast.TryGetValue(st, out old) && old != c)
                    {
                        n++;
                        if (list.Count < 12) list.Add((st.Name != null ? st.Name.ToString() : st.StringId) + " " + old + " -> " + c);
                        if (!frozen)
                        {
                            float kOld = st.IsVillage ? PopulationLaw.KOf(old, false) : (st.IsTown ? PopulationLaw.KOf(old, true) : 0f);
                            float kNew = st.IsVillage ? PopulationLaw.KOf(c, false) : (st.IsTown ? PopulationLaw.KOf(c, true) : 0f);
                            float units = st.IsVillage && st.Village != null ? Math.Max(0f, st.Village.Hearth) : (st.IsTown && st.Town != null ? Math.Max(0f, st.Town.Prosperity) : 0f);
                            people += (double)units * (kNew - kOld);
                        }
                    }
                    _cultLast[st] = c;
                }
                catch (Exception e) { Stumble("CultureChanges(osada)", e); }
            }
            if (first) return "pierwsza doba sesji";
            return (n == 0 ? "-" : string.Join("; ", list.ToArray()) + (n > list.Count ? "; ..." : "")) + (frozen && n > 0 ? "; k na osade (E4/3) - ludzie bez zmian" : "");
        }

        private static string HearthLine(int day, double[] mv, int cultN, string cultTxt, double cultPeople, long t0)
        {
            // migawka wsi
            double H = 0, peopleNow = 0, dPeople = 0, dHRegions = 0; int nv = 0, low = 0, mid = 0, high = 0;
            bool first = _hLast.Count == 0;
            var regDH = new Dictionary<Settlement, float>();
            var hNow = new Dictionary<Village, float>(); var kNow = new Dictionary<Village, float>();
            // E4/4: kazda wies RAZ. Wedlug logu testu A zbioru 30 Settlement.All podaje w ROT dwie wsie dwa razy (571 wpisow, 569 wsi w regionach; suma migawki
            // = suma hearth regionow z CSV co do 0.3): H liczyl je podwojnie, a migawka (slownik po wsi) raz - "zmiana od wczoraj" zawierala co dobe ich caly hearth
            // (ok. +705 -> +940), czyli RESZTE +624..+677/d pomiaru E4/0. Linia nazywa te wsie (do potwierdzenia w nastepnym tescie).
            int dup = 0; var dupList = new List<string>(); var firstOf = new Dictionary<Village, Settlement>();
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null || !st.IsVillage || st.Village == null) continue;
                    var v = st.Village;
                    Settlement s0;
                    if (firstOf.TryGetValue(v, out s0))
                    {
                        dup++;
                        if (dupList.Count < 6) dupList.Add((st.Name != null ? st.Name.ToString() : "?") + " [" + st.StringId + (ReferenceEquals(s0, st) ? ", ta sama osada" : " = wies osady " + s0.StringId) + "]");
                        continue;
                    }
                    firstOf[v] = st;
                    float h = v.Hearth, k = KOf(st, false);
                    nv++; H += h; peopleNow += (double)Math.Max(0f, h) * k;
                    if (h < 200f) low++; else if (h < 600f) mid++; else high++;
                    hNow[v] = h; kNow[v] = k;
                    float h0, k0;
                    if (!first && _hLast.TryGetValue(v, out h0))
                    {
                        _kLast.TryGetValue(v, out k0);
                        dPeople += (double)(h - h0) * k0;
                        var reg = OutlawLaw.RegionFor(st);
                        if (reg != null) { float x; regDH.TryGetValue(reg, out x); regDH[reg] = x + (h - h0); }
                        dHRegions += h - h0;
                    }
                }
                catch (Exception e) { Stumble("HearthLine(wies)", e); }
            }
            double Hlast = 0; foreach (var kv in _hLast) Hlast += kv.Value;
            _dupV = dup;
            _dupTxt = dup > 0 ? string.Join("; ", dupList.ToArray()) + (dup > dupList.Count ? "; ..." : "") : null;
            // budzet regionow do CSV ksiegi ludzi
            lock (Gate)
            {
                _regOut.Clear();
                if (!first)
                    foreach (var kv in regDH)
                    {
                        float[] a; _regDay.TryGetValue(kv.Key, out a);
                        float tick = a != null ? a[BTick] : 0f, carts = a != null ? a[BCarts] : 0f, raid = a != null ? a[BRaid] : 0f;
                        _regOut[kv.Key.StringId] = new[] { tick, carts, raid, kv.Value - tick - carts - raid };
                    }
            }
            _hLast.Clear(); foreach (var kv in hNow) _hLast[kv.Key] = kv.Value;
            _kLast.Clear(); foreach (var kv in kNow) _kLast[kv.Key] = kv.Value;

            double cartsH = _cartNewH + _cartRefH, fishH = _fishNewH + _fishRefH;
            // E4/3: nasze ruchy hearth za ludzi wprost z ksiegi (PeopleBook.Move; znak: + gdy wies zyskuje). Tabory i lodzie gry (MCart) mierza okna W3-W5 - nie drugi raz.
            double o1 = -mv[PeopleBook.MMisery], o2 = -mv[PeopleBook.MRaidFlee], o3 = mv[PeopleBook.MPoolHome] + mv[PeopleBook.MCommon], o4 = mv[PeopleBook.MH3Cart], o5 = _forage;
            double o6 = mv[PeopleBook.MDisband], o7 = mv[PeopleBook.MPress] + mv[PeopleBook.MQuest] + mv[PeopleBook.MIncident];
            // E4/5: spustoszenie (rabunek i zerowanie - trafieni z DOM) i powrot uchodzcow - hearth z ksiegi; podloga 10 z uchodzcow (MRefugeeFloor) NIE - jej hearth jest w ticku W1
            double o8 = mv[PeopleBook.MDevRaid] + mv[PeopleBook.MDevForage], o9 = mv[PeopleBook.MRefugeeHome];
            // E4/7: pobor z wsi (BK DeduceManpower, A5 - ujemne) i powroty zolnierzy ze znanym pochodzeniem do DOM regionu pochodzenia (dodatnie) - hearth z ksiegi
            double o10 = mv[PeopleBook.MLevyVillage] + mv[PeopleBook.MLevyA5], o11 = 0;
            for (int i = PeopleBook.MBackH3; i <= PeopleBook.MBackDisband; i++) o11 += mv[i];
            o11 += mv[PeopleBook.MBackPress];                    // E4/7b: niewzieci z poboru wymuszonego gracza wracaja do DOM swojej wsi (+1/k)
            // E4/6: glod - zmarli z glodu i uciekli z glodu (DOM -> smierc, DOM -> UCHODZCY) - hearth z ksiegi (scalenie z E4/7: o12, w paczce E4/6 bylo o10)
            double o12 = mv[PeopleBook.MFamineDead] + mv[PeopleBook.MFamineFled];
            // E4/8: wojsko z ludzi (milicja, nowe druzyny, karawany, konwoje - ujemne), milicja do domu i wedrowcy BK u celu (dodatnie), migracja do miast (ujemne) - hearth z ksiegi
            double e8Mil = mv[PeopleBook.MLevyMilitia], e8MilHome = mv[PeopleBook.MBackMilitia], e8Spawn = mv[PeopleBook.MLevySpawn];
            double e8Car = mv[PeopleBook.MCaravanOut] + mv[PeopleBook.MCaravanRefill], e8RoadOut = mv[PeopleBook.MRoadOut], e8RoadIn = mv[PeopleBook.MRoadIn] + mv[PeopleBook.MRoadBack];
            double e8Mig = mv[PeopleBook.MTownMigrate];        // rozwiazane bez bitwy do domu (R6) - MBackDisband, w o11
            double o13 = e8Mil + e8MilHome + e8Spawn + e8Car + e8RoadOut + e8RoadIn + e8Mig;
            // E4/10: weterani niewynajeci po czasie czekania wracaja do DOM regionu pochodzenia (z hearth, jak MBack*) - inaczej RESZTA hearth wchlonie te powroty
            // (scalenie calosci etapu 4: w paczce E4/10 bylo o13 - o13 zajal E4/8)
            double o14 = mv[PeopleBook.MBackVeteran];
            // E4/9: pobor do Strazy z ludnosci Westeros (ujemne, -1/k) i dluznicy do domu z pochodzeniem (dodatnie) - hearth z ksiegi (inaczej RESZTA wchlonie je)
            double o15w = mv[PeopleBook.MWatchLevy], o15d = mv[PeopleBook.MBackDebt];
            double seedH = -mv[PeopleBook.MSeed];
            bool seedInside = !first && seedH != 0;
            double named = _vTick + _decreeH - cartsH - fishH - _raidH - o1 - o2 + o3 + o4 + o5 + o6 + o7 + o8 + o9 + o10 + o11 + o12 + o13 + o14 + o15w + o15d - (seedInside ? seedH : 0);
            int devSteps = 0; long devLootAi = 0, devLootPlayer = 0;
            try { Devastation.StepsToday(out devSteps, out devLootAi, out devLootPlayer); } catch (Exception e) { Stumble("Daily(Devastation)", e); }
            double dH = H - Hlast;
            double rest = dH - named;
            double restModel = _vModel - _vExpl - _vScar;
            var sb = new StringBuilder(2200);
            sb.Append("Hearth (E4/0): dzien ").Append(day).Append(" | wsie ").Append(nv).Append(", hearth ").Append(H.ToString("0.0", Inv));
            if (first) sb.Append(" - pierwsza doba sesji: migawka wsi, bilans od nastepnej doby (liczniki tej doby pominiete)");
            else
            {
                sb.Append(" (zmiana od wczoraj ").Append(S(dH)).Append(") = tick wsi ").Append(S(_vTick)).Append(" w ").Append(_vTickN).Append(" wsiach [");
                // E4/4: przy czynnym przyroscie tick to przyrost naturalny + powrot uchodzcow; stala gry tylko pokazana (nie wchodzi). "stala gry: baza" zostaje w linii (narzedzie T3)
                if (_vGrowN > 0)
                    sb.Append("przyrost naturalny (E4/4) ").Append(S(_vNat)).Append(", powrot uchodzcow (+0.5 ponizej ").Append(Settings.Current != null ? Math.Max(1, Settings.Current.RefugeeFloorHearth) : 40)
                      .Append(Devastation.On ? ", tylko wsie bez spustoszenia E4/5) " : ", spustoszenie E4/5 wylaczone) ").Append(S(_vRef)).Append(" w ").Append(_vGrowN).Append(" tickach z przyrostem; stala gry: baza ").Append(S(_vBase))
                      .Append(" (zastapiona przez E4/4 - nie wchodzi do ticku), z mnoznikami ").Append(S(_vBaseMul));
                else
                    sb.Append("stala gry: baza ").Append(S(_vBase)).Append(", z mnoznikami ").Append(S(_vBaseMul));
                sb.Append(", blizna i uchodzcy ScorchedEarth ").Append(S(_vScar))
                  .Append(", reszta modelu (perki, budynki, polityki, BK, BKROT, zadania; przy E4/4 limity korytarza) ").Append(S(restModel))
                  .Append(", podloga 10 +").Append(F2(_vFloor)).Append(" w ").Append(_vFloorN).Append(" wsiach, niezgodnosc ticku z modelem ").Append(S(_vOdd)).Append(" w ").Append(_vOddN).Append(" wsiach]")
                  .Append(" + dekret Growth BK ").Append(S(_decreeH)).Append(" (").Append(_decreeN).Append(" wsi)")
                  .Append(" - tabory wsi -").Append(F2(cartsH)).Append(" (nowe ").Append(_cartNewN).Append(", uzupelnienia ").Append(_cartRefN).Append("; ludzi ").Append(_cartNewMen + _cartRefMen)
                  .Append(PeopleUnit.On ? "; od E4/3: n/k osady, gra: (n+1)/2)" : "; gra: (n+1)/2)")
                  .Append(" - lodzie rybackie -").Append(F2(fishH)).Append(" (nowe ").Append(_fishNewN).Append(", uzupelnienia ").Append(_fishRefN).Append("; ludzi ").Append(_fishNewMen + _fishRefMen).Append(')')
                  .Append(" - rabunek gry -").Append(F2(_raidH)).Append(" (krokow ").Append(_raidSteps).Append(", wsi ").Append(_raidVill.Count).Append("; lup z niczego ").Append(_raidGoldAi + _raidGoldPlayer + devLootAi + devLootPlayer)
                  .Append(" zl, w tym gracz ").Append(_raidGoldPlayer + devLootPlayer).Append("; pod spustoszeniem E4/5 krokow ").Append(devSteps).Append(" - hearth w ksiedze, lup ")
                  .Append(devLootAi + devLootPlayer).Append(" zl)")
                  .Append(" + nasze (ksiega ludzi PeopleBook.Move, E4/3): wyrzutki w las (bieda) -").Append(F2(o1)).Append(", spalona wies w las -").Append(F2(o2))
                  .Append(", powrot do wsi (OutlawLaw z puli + LosersFlee ToCommon) +").Append(F2(o3)).Append(", rozbite tabory do swojej wsi +").Append(F2(o4))
                  .Append(", rozwiazane tabory i lodzie do swojej wsi (R18) +").Append(F2(o6)).Append(", gracz (pobor wymuszony, zadania, incydenty) ").Append(S(o7))
                  .Append(", zerowanie armii ").Append(S(o5)).Append(" (").Append(_forageN).Append(" razy)")
                  .Append(", spustoszenie (E4/5: rabunek, zerowanie) ").Append(S(o8)).Append(", powrot uchodzcow +").Append(F2(o9))
                  .Append(", glod (E4/6: zmarli i uciekli z glodu) ").Append(S(o12))
                  .Append(", pobor z wsi (E4/7) ").Append(S(o10)).Append(", powroty z pochodzeniem (E4/7) +").Append(F2(o11))
                  .Append(", nasze (E4/8): milicja z wolnych rak ").Append(S(e8Mil)).Append(", milicja do domu ").Append(S(e8MilHome)).Append(", nowe druzyny ").Append(S(e8Spawn))
                  .Append(", karawany ").Append(S(e8Car)).Append(", wedrowcy BK ").Append(S(e8RoadOut)).Append("/").Append(S(e8RoadIn)).Append(", migracja do miast ").Append(S(e8Mig))
                  .Append(", powroty weteranow (E4/10) +").Append(F2(o14))
                  .Append(", pobor do Strazy z ludnosci Westeros (E4/9) ").Append(S(o15w)).Append(", dluznicy do domu (E4/9) +").Append(F2(o15d));
            }
            sb.Append(" | powrot z puli w regionie bez wsi (zeszli z puli, hearth bez zmian) ").Append(_noVillage.ToString("0.0", Inv)).Append(" ludzi");
            if (seedH != 0) sb.Append(" | siew puli wyrzutkow -").Append(F2(seedH)).Append(seedInside ? " (w bilansie, poza RESZTA)" : " (przed pierwsza migawka sesji - poza bilansem)");
            sb.Append(" | zmiana kultury osad: ").Append(cultN).Append(" (").Append(cultTxt).Append("), ludzie tabeli ").Append(S0(cultPeople)).Append(" (U3)");
            if (!first)
                sb.Append(" | RESZTA ").Append(S(rest)).Append(" (inwestycje BEE, konsola, inne mody; od E4/3 zadania, pobor wymuszony i incydenty gracza nazwane w \"nasze\")");
            if (dup > 0) sb.Append(" | wsie powtorzone w Settlement.All - liczone raz (E4/4): ").Append(dup).Append(_dupTxt != null ? " (" + _dupTxt + ")" : "");
            sb.Append(" | wsie wedlug progow 200/600: ").Append(low).Append(" / ").Append(mid).Append(" / ").Append(high);
            if (!first && PopulationLaw.Calibrated)
            {
                double modulesAndRest = dPeople - _peopleDay[BTick] - _peopleDay[BCarts] - _peopleDay[BRaid] - _peopleDay[BDecree];
                sb.Append(" | w ludziach tabeli (hearth x k): ludzie wsi ").Append(peopleNow.ToString("0", Inv)).Append(" (").Append(S0(dPeople + cultPeople)).Append(") = tick ").Append(S0(_peopleDay[BTick]))
                  .Append(", tabory i lodzie ").Append(S0(_peopleDay[BCarts])).Append(", rabunek ").Append(S0(_peopleDay[BRaid])).Append(", dekret ").Append(S0(_peopleDay[BDecree]))
                  .Append(", nasze moduly i RESZTA (moduly nie podaja wsi) ").Append(S0(modulesAndRest)).Append(", zmiana kultury ").Append(S0(cultPeople));
            }
            double f = 1000.0 / Stopwatch.Frequency;
            sb.Append(" | czas ").Append(((Stopwatch.GetTimestamp() - t0) * f).ToString("0.0", Inv)).Append(" ms (linia Hearth; model hearth w oknie ticku wsi ").Append((_modelTicks * f).ToString("0.0", Inv)).Append(" ms)");
            if (_stumbles > 0) sb.Append(" | potkniecia ").Append(_stumbles);
            sb.Append('.');
            return sb.ToString();
        }

        private static string Ms(long t0) { return ((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency).ToString("0.0", Inv); }

        private static string ProsperityLine(int day)
        {
            long t0 = Stopwatch.GetTimestamp();
            double P = 0, Q = 0, people = 0, dPeople = 0; int nt = 0, nc = 0;
            bool first = _pLast.Count == 0;
            double Plast = 0, Qlast = 0;
            var now = new Dictionary<Town, float>();
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null || st.Town == null || !(st.IsTown || st.IsCastle)) continue;
                    float p = st.Town.Prosperity; now[st.Town] = p;
                    float p0; bool had = _pLast.TryGetValue(st.Town, out p0);
                    if (st.IsTown)
                    {
                        nt++; P += p; float k = KOf(st, true); people += (double)Math.Max(0f, p) * k;
                        if (had) { Plast += p0; dPeople += (double)(p - p0) * k; }
                    }
                    else { nc++; Q += p; if (had) Qlast += p0; }
                }
                catch (Exception e) { Stumble("ProsperityLine(osada)", e); }
            }
            _pLast.Clear(); foreach (var kv in now) _pLast[kv.Key] = kv.Value;
            var sb = new StringBuilder(600);
            sb.Append("Dobrobyt (E4/0): dzien ").Append(day).Append(" | miasta ").Append(nt).Append(": dobrobyt ").Append(P.ToString("0", Inv));
            if (first) sb.Append(" - pierwsza doba sesji, zmiany od nastepnej | zamki ").Append(nc).Append(": dobrobyt ").Append(Q.ToString("0", Inv)).Append('.');
            else
            {
                double dP = P - Plast, dQ = Q - Qlast;
                sb.Append(" (").Append(S(dP)).Append(") = tick ").Append(S(_pTown)).Append(", reszta ").Append(S(dP - _pTown)).Append(" (RBL ekspansja i glod; zdobycie; zdarzenia)")
                  .Append(" | w ludziach tabeli (dobrobyt x k): ludzie miast ").Append(people.ToString("0", Inv)).Append(" (").Append(S0(dPeople)).Append(" = tick ").Append(S0(_pTownPeople))
                  .Append(", reszta ").Append(S0(dPeople - _pTownPeople)).Append(')')
                  .Append(PopulationLaw.TownsFrozen ? " [E4/4: ludzie miast ksiegi to stan (" + PeopleBook.TownSum.ToString("0", Inv) + ") - liczba wyzej to tylko dobrobyt x k, bez wplywu na ksiege]" : "")
                  .Append(" | zamki ").Append(nc).Append(": dobrobyt ").Append(Q.ToString("0", Inv)).Append(" (").Append(S(dQ)).Append(") = tick ").Append(S(_pCastle)).Append(", reszta ").Append(S(dQ - _pCastle)).Append('.');
            }
            sb.Length--; sb.Append(" | czas ").Append(Ms(t0)).Append(" ms.");
            return sb.ToString();
        }

        private static string PrisonersLine(int day)
        {
            long t0 = Stopwatch.GetTimestamp();
            int P = 0, L = 0, heroes = 0;
            foreach (var mp in MobileParty.All)
            {
                try { if (mp != null && mp.IsActive && mp.PrisonRoster != null) { P += mp.PrisonRoster.TotalRegulars; heroes += mp.PrisonRoster.TotalHeroes; } }
                catch (Exception e) { Stumble("PrisonersLine(partia)", e); }
            }
            foreach (var st in Settlement.All)
            {
                try { if (st != null && st.Party != null && st.Party.PrisonRoster != null) { L += st.Party.PrisonRoster.TotalRegulars; heroes += st.Party.PrisonRoster.TotalHeroes; } }
                catch (Exception e) { Stumble("PrisonersLine(osada)", e); }
            }
            bool first = _prisLastP < 0;
            int d = first ? 0 : (P + L) - (_prisLastP + _prisLastL);
            _prisLastP = P; _prisLastL = L;
            int taken = 0, rescued = 0;
            for (int i = 0; i < PKinds; i++) { taken += _taken[i]; rescued += _rescued[i]; }
            var sb = new StringBuilder(900);
            sb.Append("Jency (E4/0): dzien ").Append(day).Append(" | stan (szeregowi): w partiach ").Append(P).Append(", w lochach ").Append(L)
              .Append(first ? " - pierwsza doba sesji" : " (zmiana " + S0(d) + ")").Append(", bohaterow w niewoli ").Append(heroes)
              .Append(" | po bitwach: wzieci do niewoli +").Append(taken).Append(" w ").Append(_capBattles).Append(" bitwach (");
            for (int i = 0; i < PKinds; i++) { if (i == PPlayer) continue; sb.Append(i > 0 ? ", " : "").Append(PName[i]).Append(' ').Append(_taken[i]); }
            sb.Append("; zaloga i milicja oddaja do lochu osady; Inni - skasowani przez ROT w linii \"Wzrost Innych\"; gracz - ekran lupu, poza oknem)")
              .Append(" | z niewoli pokonanych zabrano ").Append(_lootRemoved).Append(" w ").Append(_lootBattles).Append(" bitwach: odbici i wcieleni do skladu zwyciezcy -").Append(rescued).Append(" (");
            for (int i = 0; i < PKinds; i++) { if (i == PPlayer) continue; sb.Append(i > 0 ? ", " : "").Append("do " + PName[i]).Append(' ').Append(_rescued[i]); }
            sb.Append("), odbici przez Innych - polegli (E4/2) ").Append(_rescueFallen)
              .Append(", bez odbiorcy (gra usuwa; w bitwach gracza - ekran lupu) ").Append(Math.Max(0, _lootRemoved - rescued - _rescueFallen))
              .Append(" | z rabunku BK +").Append(_bkRaidPris).Append(" w ").Append(_bkRaidN).Append(" rabunkach (z niczego; gracz ").Append(_bkRaidPlayer).Append(')')
              .Append(CapturedSegment())
              .Append(PeopleBook.EscapeSegment())       // poprawki E4 po tescie koncowym (fixA, P2-T4-lochy): ucieczki ponad limit lochu partii (jout rownania jencow)
              .Append(" | wypuszczeni, sprzedani, na Mur, straceni - linia \"Prawo jenca (I1)\"; uwolnieni bez odbiorcy w bitwach H3 - linia \"Przegrani (H3)\"; jency rozwiazanych partii - linia \"Partie (E4/0)\"");
            sb.Append(" | czas ").Append(Ms(t0)).Append(" ms.");
            return sb.ToString();
        }

        /// <summary>E4/9 (W11): pojmani z wsi (ThrallLaw.CapturePrefix - z konta UCHODZCY do lochu najezdzcy) od ostatniej linii; przy czynnym prawie okno W11 nie liczy
        /// (BkCapPre - recenzja E4/9 R1-1/R2-1: prefiks W11 biegnie PRZED naszym), wiec "z rabunku BK" (z niczego) jest 0. Liczniki Devastation czytane przed jej DayLine (zeruje).</summary>
        private static string CapturedSegment()
        {
            try
            {
                int r; double j, pl;
                Devastation.CaptivesToday(out r, out j, out pl);
                return " | pojmani z wsi (E4/9) +" + j.ToString("0", Inv) + " w " + r + " rabunkach (z konta UCHODZCY napadnietych wsi do lochu najezdzcy; gracz " + pl.ToString("0", Inv)
                       + (Devastation.CaptivesLive ? "; BK bez jencow z niczego)" : "; prawo WYLACZONE - BK z niczego jak dotad)");
            }
            catch (Exception e) { Stumble("CapturedSegment", e); return ""; }
        }

        private static string WinterLine(int day)
        {
            long t0 = Stopwatch.GetTimestamp();
            var sb = new StringBuilder(900);
            sb.Append("Zima (E4/0): dzien ").Append(day).Append(" | pora (ekonomia) ").Append(SafeSeason());
            int season, sday, slen;
            if (WesterosClimate.SeasonInfo(out season, out sday, out slen))
                sb.Append("; kalendarz Westeros: ").Append((CampaignTime.Seasons)season).Append(", dzien pory ").Append(sday).Append(" z ").Append(slen).Append(" (do konca ").Append(Math.Max(0, slen - sday)).Append(" dni)");
            else sb.Append("; kalendarz Westeros: brak stanu (klimat wylaczony albo przed pierwsza doba)");
            bool winter = WinterBite.WinterNow();
            double food = 0, cutSum = 0; int nv = 0;
            var byC = new Dictionary<string, double[]>();
            var model = Campaign.Current.Models.VillageProductionCalculatorModel;
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null || !st.IsVillage || st.Village == null) continue;
                    nv++;
                    if (model != null) food += Devastation.FoodToday(model, st.Village);   // E4/5: wies spladrowana z ta sama podmiana stanu co w TickProductions (produkuje)
                    if (!winter) continue;
                    float cut = WinterBite.VillageCut(st.Village);
                    cutSum += cut;
                    string c = CultureAtStart(OutlawLaw.RegionFor(st));
                    double[] a; if (!byC.TryGetValue(c, out a)) { a = new double[2]; byC[c] = a; }
                    a[0] += cut; a[1] += 1;
                }
                catch (Exception e) { Stumble("WinterLine(wies)", e); }
            }
            if (_food0 < 0) _food0 = food;
            if (winter)
            {
                sb.Append(" | ciecie plonu wsi dzis (WinterBite.VillageCut): swiat srednio ").Append(nv > 0 ? (100.0 * cutSum / nv).ToString("0.0", Inv) : "-").Append("%, krainy: ");
                var keys = new List<string>(byC.Keys); keys.Sort(StringComparer.Ordinal);
                for (int i = 0; i < keys.Count; i++) { var a = byC[keys[i]]; sb.Append(i > 0 ? ", " : "").Append(keys[i]).Append(' ').Append((100.0 * a[0] / Math.Max(1, a[1])).ToString("0.0", Inv)).Append('%'); }
                double men = 0, bonus = 0;
                foreach (var mp in MobileParty.All)
                {
                    try
                    {
                        if (mp == null || !mp.IsActive || mp.IsGarrison || mp.IsMilitia || mp.MemberRoster == null) continue;
                        int n = mp.MemberRoster.TotalManCount; if (n <= 0) continue;
                        men += n; bonus += n * WinterBite.PartyFoodBonus(mp);
                    }
                    catch (Exception e) { Stumble("WinterLine(partia)", e); }
                }
                sb.Append(" | dodatek jedzenia partii zima srednio +").Append(men > 0 ? (100.0 * bonus / men).ToString("0.0", Inv) : "0").Append('%');
            }
            else sb.Append(" | poza zima: ciecie plonu 0, dodatek jedzenia partii 0");
            sb.Append(" | zywnosc wsi swiata dzis (model gry, CalculateDailyFoodProductionAmount) ").Append(food.ToString("0.0", Inv)).Append(" (pierwsza doba sesji ").Append(_food0.ToString("0.0", Inv))
              .Append(", ").Append(_food0 > 0 ? S((food / _food0 - 1.0) * 100.0) + "%" : "-").Append(") | warownie - linia \"Zapasy warowni (T6)\" (ksiega ludzi)");
            sb.Append(" | czas ").Append(Ms(t0)).Append(" ms.");
            return sb.ToString();
        }

        private static string SafeSeason() { try { return WesterosClimate.Now().ToString(); } catch { return "?"; } }
    }
}
