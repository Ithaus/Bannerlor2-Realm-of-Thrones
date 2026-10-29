using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// E4/8 (164b-2, PLAN 4.7, 4.9; projekt etapu 4 R2, R4, R6, R19; plan kodu docs/etap4/PLAN-KODU-E48.md): WOJSKO Z LUDZI. Kazdy czlowiek, ktorego dotad dawal
    /// szablon albo model gry, schodzi z DOM swojego regionu przez PeopleBook.TakeLevy (E4/7: wies -1/k, miasto -1 stanu, zamek - jego wsie, podloga 50 hearth),
    /// a jego udzial pochodzenia idzie do rosteru (GiveNew). Czego DOM nie da - tego czlowieka nie ma (zdejmowany z rosteru, ktory gra przed chwila napelnila):
    ///  - ZALOGI (K1): przyrost z modelu zalogi gry = 0 (prefiks bool na TickGarrisonChangeForTown) - zaloga rosnie tylko z platnego werbunku od notabli (A5 - zdejmuje
    ///    E4/7), z partii i z patroli BK; A5 mierzony (limit dzienny / bez ochotnikow / zold albo miejsce); K8 (Jeff 11.10 16:55): limit dzienny A5 do
    ///    GarrisonAutoRecruitPerDay (2; gra 1) - zold i miejsce pilnuje gra, ludzie przez E4/7 A5Post; bez K8 Straz, Inni, ksiega bez poboru, hamulec. Straz - do E4/9;
    ///  - MILICJA (K2): przyrost w ticku wsi i warowni, po odbudowie wsi i po obronie oblezenia - ochotnicy z wolnych rak regionu; kraina z hamulcem (FreeHands.Braked)
    ///    i osady Innych nie dostaja nowej milicji; ubytek bez walki (BK "Over supported limit") wraca do DOM tej osady; rozwiazana partia we wsi - w oknie E4/7;
    ///  - NOWE DRUZYNY LORDOW (K3, PLAN 4.9, Jeff P3): szablon klanu oplacony z wolnych rak regionu siedziby, potem z zalogi siedziby (zaloga zostawia 50 w miescie /
    ///    25 w zamku, sprzet idzie z ludzmi), reszty nie ma; zboze 15-30 z zapasu siedziby (albo najblizszej warowni krolestwa), nie z niczego;
    ///  - KARAWANY (K4, Jeff P1): straz i woznice z ludzi osady startu, bez hamulca wolnych rak (jak najemnik); uzupelnienie konwojow morskich - z miasta;
    ///  - WEDROWCY I KONWOJE BK (K5, R19): cala partia wyjezdza z DOM osady pochodzenia i wchodzi do DOM osady, do ktorej dojdzie (migracja miedzy regionami);
    ///    eskorta konwojow wraca do domu; niewolnicy w konwojach - E4/9.
    /// Miasta (R2) - TownGrowth.cs. Latki wpiete zawsze (U26), licza przy kluczach i LevyLive (przed siewem E4/7 wszystko jak dotad). Korekty rosteru PO oryginale
    /// (postfiks/finalizer), na kopii skladu; kazde cialo we wlasnym try, potkniecia liczone per miejsce (bez globalnych wylacznikow - CLAUDE.md 7).
    /// Straz (Nocna) - "Straz - do E4/9": zaloga rosnie po staremu, milicja/druzyny/karawany/wedrowcy Strazy - koszyk "" (bez zdjecia z DOM).
    /// E4/9 (192, Y1-Y5): zaloga Strazy dalej rosnie z modelu (Jeff 11.10 TAK), ale kazdy przybysz - i kazdy nowy czlowiek milicji, szablonu i karawany osad frakcji Strazy -
    /// jest prawdziwym czlowiekiem z Westeros (WatchLaw.GiveLevy: Mur, pule wyrzutkow, ludnosc); osada Daru poza frakcja Strazy - "" jak dotad; wedrowcy BK z Daru -
    /// zwykla regula R19 (migracja rodzin z DOM osady, nie sluzba). Przy wylaczonym WatchLaw - "" jak w E4/8.
    /// </summary>
    internal static partial class ArmsFromPeople
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const double Eps = 1e-6;

        private static Settings S { get { return Settings.Current; } }
        internal static bool GarrisonOn { get { var s = S; return s != null && s.GarrisonGrowthOnlyRecruits; } }
        internal static bool MilitiaOn { get { var s = S; return s != null && s.MilitiaFromFreeHands; } }
        internal static bool SpawnOn { get { var s = S; return s != null && s.NewPartyFromPeople; } }
        internal static bool SpawnGarrisonOn { get { var s = S; return s != null && s.NewPartyFromSeatGarrison; } }
        internal static bool GrainOn { get { var s = S; return s != null && s.NewPartyGrainFromSeat; } }
        internal static bool CaravansOn { get { var s = S; return s != null && s.CaravansFromPeople; } }
        internal static bool TravellersOn { get { var s = S; return s != null && s.TravellersFromPeople; } }
        // K8: limit dzienny A5 (1..5 - jak suwak MCM; 1 = gra)
        internal static int K8Limit { get { var s = S; return s == null ? 1 : Math.Max(1, Math.Min(5, s.GarrisonAutoRecruitPerDay)); } }

        // ---------------------------------------------------------------- wpiecie
        private static bool _applied, _startLogged;
        private static readonly List<string> _wired = new List<string>(), _missing = new List<string>();
        private static readonly List<KeyValuePair<string, MethodBase>> _targets = new List<KeyValuePair<string, MethodBase>>();
        private static PropertyInfo _pKind, _pSlave;

        // ---------------------------------------------------------------- liczniki doby (bez zapisu)
        private sealed class Day
        {
            // zalogi
            public long zBlocked; public int zTowns; public long zWatch; public int zWatchTowns;
            // A5 (pomiar)
            public int a5Towns, a5With, a5Daily, a5NoVol, a5Wage; public long a5Men;
            // K8: dobrani ponad limit gry (k8Game) i odmowy (twierdze ponizej limitu K8)
            public int k8Game = 1, k8Towns, k8Wage, k8NoVol, k8Brake, k8Out; public long k8Extra;
            // milicja
            public long mUp, m1v, m1t, m1c, m1k, m1h, m1o, m2; public double m1, mFrac;
            public long r1, r2, r3; public readonly HashSet<string> r1Reg = new HashSet<string>(); public readonly Dictionary<string, int> r1Realm = new Dictionary<string, int>();
            public long hAll; public double h1, h2, h3, h4; public double j;     // h4 - E4/9 (recenzja R2-4): milicja osady Strazy na Mur
            public long mTickUp, mTickDown;      // tylko okno ticku (N7 - porownanie z "milicja (E4/0): z modelu w ticku")
            // nowe druzyny
            public int p, p1, p2, p3; public long n, nOff; public double a1, aFrac; public long a2, a3h, a3g, a3f, a4;
            public long kitCut;     // 159: ludzie szablonu bez sprzetu, ktorzy zostali we wsi (poza rachunkiem szablonu - NewLordKit.Plan)
            public long g, g1, g2, g3, g3x, gOth; public int gN; public long gifts; public long giftsValue;   // m6b (5): g3x - zboze bez zapasu zdjete (FoodSourcesInLedger), gOth - zboze gry dla druzyn Innych (nazwane w ksiedze towarow)
            // karawany
            public int k; public long kMen; public double c1, c1t, c1c, c1v, cFrac; public long c2, c2Master; public double c3; public long c4; public int c3N;
            public long cWatch;
            // wedrowcy i konwoje BK
            public int w, wCargo; public double n1w, n1e, n3; public long s;
            public int b1N, b2N, b3N, b4N, b5N; public double b1, b2, b3, b4, b5, bNone, bOther; public double sEnd;
            // rozwiazane bez bitwy (R6)
            public int q, qRemoved; public double qHere, qOther, qNone, qWall, qVet; public readonly Dictionary<int, int> qKind = new Dictionary<int, int>();
            public double ob;                    // m97-wioski krok 2: obroncy wioski pobocznej zdjeci z DOM wsi okregu (PeopleBook.TakeLevyHere, MLevyMilitia) - do KONTROLI
            // 295: orszaki dworow BK (RetinueComponent.DailyTick) - rt zdjeci z DOM wsi (MLevyMilitia, do KONTROLI); reszta - linia "Orszaki dworow BK (295)"
            public double rt, rtFrac, rtH1, rtH2, rtH3, rtH4;
            public int rtCalls, rtSkip, rtUp, rtNew, rtStrip, rtHome;
            // 295 (recenzja): orszaki majatkow BK (Estate.Tick / TakeRetinue) i zamkow z Castle Retinue (TickCastle) - przyrost wliczony tez w rtUp / rt / rtFrac / rtStrip
            public int esCalls, esUp, esNew, csUp, mvN; public double mvMen, mvLeft;
            public long ticks;
        }
        private static Day D = new Day();
        private static int _stumbles, _stumblesDay;
        private static readonly Dictionary<string, int> _stumbleAt = new Dictionary<string, int>();
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        private static void Stumble(string where, Exception e)
        {
            try
            {
                _stumbles++; _stumblesDay++;
                int v; _stumbleAt.TryGetValue(where, out v); _stumbleAt[where] = v + 1;
                if (_errSites.Add(where)) Log.Error("ArmsFromPeople." + where, e);
            }
            catch { }
        }

        /// <summary>Konstruktor ArmouryBehavior: stan jednej kampanii nie przecieka do drugiej (latki wpiete raz na proces - nie zerowane).</summary>
        internal static void Reset()
        {
            D = new Day(); _stumbles = _stumblesDay = 0; _stumbleAt.Clear(); _errSites.Clear(); _startLogged = false; _bkTop = null;
            _tickV = null; _tickDH = 0.0; _k8Town = null;
        }

        // ---------------------------------------------------------------- pomocnicze
        private static int Men(TroopRoster r) { return PeopleBook.Men(r); }

        private static int Ceil(double x) { return x <= Eps ? 0 : (int)Math.Ceiling(x - 1e-6); }

        /// <summary>Szeregowi rosteru wedlug typu (bez bohaterow i wightow) - kopia do porownan.</summary>
        private static Dictionary<CharacterObject, int> Regulars(TroopRoster r) { return Regulars(r, false); }

        /// <summary>Szeregowi wedlug typu; wights = true - razem z wightami (milicja osad Innych - recenzja R2-4).</summary>
        private static Dictionary<CharacterObject, int> Regulars(TroopRoster r, bool wights)
        {
            var d = new Dictionary<CharacterObject, int>();
            if (r == null) return d;
            for (int i = 0; i < r.Count; i++)
            {
                var c = r.GetCharacterAtIndex(i);
                if (c == null || c.IsHero || (!wights && PeopleBook.IsWight(c))) continue;
                int k = r.GetElementNumber(i);
                if (k <= 0) continue;
                int v; d.TryGetValue(c, out v); d[c] = v + k;
            }
            return d;
        }

        /// <summary>Ilu przybylo (typy ponad stan "before").</summary>
        private static Dictionary<CharacterObject, int> AddedOf(Dictionary<CharacterObject, int> before, TroopRoster r)
        {
            var now = Regulars(r);
            var add = new Dictionary<CharacterObject, int>();
            foreach (var kv in now)
            {
                int was = 0;
                if (before != null) before.TryGetValue(kv.Key, out was);
                if (kv.Value > was) add[kv.Key] = kv.Value - was;
            }
            return add;
        }

        /// <summary>Rozdzial n proporcjonalnie do wag (najwieksze reszty), nie wiecej niz waga typu.</summary>
        private static Dictionary<CharacterObject, int> Split(Dictionary<CharacterObject, int> w, int n)
        {
            var res = new Dictionary<CharacterObject, int>();
            int all = 0; foreach (var v in w.Values) all += Math.Max(0, v);
            if (n <= 0 || all <= 0) return res;
            if (n >= all) { foreach (var kv in w) if (kv.Value > 0) res[kv.Key] = kv.Value; return res; }
            var rest = new List<KeyValuePair<CharacterObject, double>>();
            int given = 0;
            foreach (var kv in w)
            {
                if (kv.Value <= 0) continue;
                double exact = (double)n * kv.Value / all;
                int f = Math.Min(kv.Value, (int)Math.Floor(exact));
                if (f > 0) { res[kv.Key] = f; given += f; }
                rest.Add(new KeyValuePair<CharacterObject, double>(kv.Key, exact - f));
            }
            rest.Sort((a, b) => b.Value.CompareTo(a.Value));
            for (int i = 0; given < n && rest.Count > 0; i = (i + 1) % rest.Count)
            {
                var c = rest[i].Key; int have; res.TryGetValue(c, out have);
                if (have < w[c]) { res[c] = have + 1; given++; }
                else if (rest.TrueForAll(x => { int h; res.TryGetValue(x.Key, out h); return h >= w[x.Key]; })) break;
            }
            return res;
        }

        /// <summary>6.3: zdejmuje n ludzi sposrod przybylych (proporcjonalnie do ksztaltu przybylych, typ po typie z kopii; nigdy ponizej stanu "before").</summary>
        private static int StripAdded(TroopRoster r, Dictionary<CharacterObject, int> before, int n, CharacterObject keep = null)
        {
            if (r == null || n <= 0) return 0;
            var add = AddedOf(before, r);
            if (keep != null && add.ContainsKey(keep)) add.Remove(keep);
            int done = 0;
            foreach (var kv in Split(add, n))
            {
                if (kv.Value <= 0) continue;
                r.AddToCounts(kv.Key, -kv.Value);
                done += kv.Value;
            }
            return done;
        }

        /// <summary>5.2: zdejmuje n szeregowych (bez bohaterow i wightow; wights = true - razem z wightami) od najliczniejszego typu, zdrowi przed rannymi
        /// (milicja - bez stanu "przed" w pamieci).</summary>
        private static int StripMost(TroopRoster r, int n, bool wights = false)
        {
            if (r == null || n <= 0) return 0;
            var list = Regulars(r, wights).OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList();
            int done = 0;
            for (int pass = 0; pass < 2 && done < n; pass++)
                foreach (var c in list)
                {
                    if (done >= n) break;
                    int idx = r.FindIndexOfTroop(c);
                    if (idx < 0) continue;
                    int num = r.GetElementNumber(idx), wnd = r.GetElementWoundedNumber(idx);
                    int t = pass == 0 ? Math.Min(Math.Max(0, num - wnd), n - done) : Math.Min(num, n - done);
                    if (t <= 0) continue;
                    if (pass == 0) r.AddToCounts(c, -t); else r.AddToCounts(c, -t, false, -Math.Min(t, wnd));
                    done += t;
                }
            return done;
        }

        /// <summary>Straz (Nocna) - "Straz - do E4/9": osada Strazy (frakcja Strazy albo kultura Daru z dnia kalibracji).</summary>
        private static bool WatchAt(Settlement s)
        {
            try
            {
                if (s == null) return false;
                if (CrownGifts.IsWatch(s.MapFaction)) return true;
                if (PeopleBook.CultureAtCalibration(s) == CrownGifts.WatchId) return true;
                var reg = OutlawLaw.RegionFor(s);
                return reg != null && FreeHands.RealmOf(reg) == CrownGifts.WatchId;
            }
            catch { return false; }
        }

        /// <summary>Osada Innych (frakcja/klan kultury whitewalker - ten sam test co E4/2): wight z niczego nie powstaje.</summary>
        private static bool OthersAt(Settlement s)
        {
            try
            {
                if (s == null) return false;
                if (OthersRise.IsWhiteWalkerClan(s.OwnerClan)) return true;
                var f = s.MapFaction;
                return f != null && f.Culture != null && f.Culture.StringId == "whitewalker";
            }
            catch { return false; }
        }

        private static string RealmName(Settlement s)
        {
            try { var reg = OutlawLaw.RegionFor(s); return reg != null ? FreeHands.RealmOf(reg) : "?"; } catch { return "?"; }
        }

        // ================================================================ K1: zalogi (4.1) - przyrost z modelu = 0
        private static int GarMen(Town t) { var g = t != null ? t.GarrisonParty : null; return g != null ? Men(g.MemberRoster) : 0; }

        // __state: -1 = nic (klucz wylaczony, nie Straz), ludzie zalogi + 1 = Straz (stan przed); 0 = nasz prefiks POMINIETY - wczesniejszy prefiks bool
        // (ClanBudget.GarRecruitPrefix Strazy przy pulapie puli) zwrocil false, a Harmony nie wola wtedy dalszych prefiksow bool (recenzja R2-7)
        public static bool GarrisonGrowthPrefix(Town __0, out int __state)
        {
            __state = -1;
            try
            {
                if (!GarrisonOn || __0 == null || __0.Settlement == null || Campaign.Current == null) return true;
                var st = __0.Settlement;
                if (CrownGifts.IsWatch(st.MapFaction)) { __state = GarMen(__0) + 1; return true; }     // Straz - do E4/9 (dawny przyrost)
                int would = 0;
                try { would = (int)Campaign.Current.Models.SettlementGarrisonModel.CalculateBaseGarrisonChange(st, false).ResultNumber; }
                catch (Exception e) { Stumble("GarrisonGrowthPrefix(model)", e); }
                if (would > 0) { D.zBlocked += would; D.zTowns++; }
                return false;
            }
            catch (Exception e) { Stumble("GarrisonGrowthPrefix", e); return true; }
        }

        public static Exception GarrisonGrowthFin(Exception __exception, Town __0, int __state)
        {
            if (__state <= 0) return __exception;
            try
            {
                int d = GarMen(__0) - (__state - 1);
                if (d > 0)
                {
                    D.zWatch += d; D.zWatchTowns++;
                    // E4/9 (Y1): przybysze zalogi Strazy z modelu - prawdziwi ludzie Westeros (Levy); bez prawa - jak dotad (uzgodnienie doby nazwie ich "z niczego - Straz")
                    var g = __0 != null ? __0.GarrisonParty : null;
                    if (g != null && WatchLaw.Live) WatchLaw.GiveLevy(g.MemberRoster, d, WatchLaw.KGarrison, -1);
                }
            }
            catch (Exception e) { Stumble("GarrisonGrowthFin", e); }
            return __exception;
        }

        // ================================================================ K1: A5 - pomiar limitow (4.2), bez zmiany gry
        [ThreadStatic] private static Town _a5Town;

        public static void A5MeasurePre(Town __0, out Town __state) { __state = _a5Town; _a5Town = __0; }

        public static Exception A5MeasureFin(Exception __exception, Town __state) { _a5Town = __state; return __exception; }

        public static void A5LimitPostfix(Town __0, ExplainedNumber __result)
        {
            if (__0 == null || !ReferenceEquals(_a5Town, __0)) return;       // UI wola te sama metode - liczymy tylko z okna A5
            try
            {
                float bas = __result.BaseNumber, res = __result.ResultNumber;
                int daily = 1;
                try { daily = Campaign.Current.Models.SettlementGarrisonModel.GetMaximumDailyAutoRecruitmentCount(__0); } catch { }
                // recenzja R2-8: gra dodaje liczbe ochotnikow (BaseNumber) tylko przy dodatnim budzecie zoldu zalogi - bez budzetu BaseNumber = 0, a wiaze zold
                int budget = 1;
                try { var gp = __0.GarrisonParty; budget = gp != null ? gp.GetAvailableWageBudget() : (__0.Settlement != null ? __0.Settlement.GarrisonWagePaymentLimit : 1); } catch { }
                var d = D;
                d.a5Towns++;
                if (res > 0f) { d.a5With++; d.a5Men += (long)Math.Ceiling(res); }
                if (budget <= 0) d.a5Wage++;
                else if (!(bas > 0f)) d.a5NoVol++;
                else if (res >= daily && bas > res) d.a5Daily++;
                else if (res < bas) d.a5Wage++;
                // K8: wolanie wyzej przeszlo przez K8Postfix (zewnetrzny poziom) - stan tej twierdzy: limit gry i powod
                if (ReferenceEquals(_k8Town, __0) && _k8Why != K8Off)
                {
                    int g = _k8Game; d.k8Game = g;
                    if (_k8Why == K8Out) d.k8Out++;
                    else if (_k8Why == K8Brake) { if (budget > 0 && bas > res && res >= g) d.k8Brake++; }   // hamulec zatrzymal ochotnika ponad limit gry
                    else
                    {
                        long got = (long)Math.Ceiling(res);                    // gra bierze ceil(Result) ochotnikow (petla i < Result)
                        if (got > g) { d.k8Extra += got - g; d.k8Towns++; }
                        if (res < daily)
                        {
                            if (budget <= 0) d.k8Wage++;
                            else if (bas < daily && !(res < bas)) d.k8NoVol++;
                            else d.k8Wage++;                                   // zold albo miejsce w zalodze
                        }
                    }
                }
            }
            catch (Exception e) { Stumble("A5LimitPostfix", e); }
        }

        // ================================================================ K8 (Jeff 11.10 16:55, plan rozdz. 18 pkt 8): A5 do GarrisonAutoRecruitPerDay na dobe (2; 1 = gra)
        // Lancuch modeli zalogi: ROT ROTSettlementGarrisonModel (_previousModel) albo NavalDLC (BaseModel) -> BK BKGarrisonModel (dziedziczy Default, tej metody nie
        // nadpisuje) -> DefaultSettlementGarrisonModel (stala 1). Kolejnosc ladowania modulow nie jest znana przy starcie - latka na KAZDEJ zadeklarowanej metodzie,
        // a dziala tylko na zewnetrznym poziomie (licznik zagniezdzenia jak SpeedDepth; max() i tak idempotentny). Zold pilnuje sama gra: "Wage Limit"
        // (GetAvailableWageBudget zalogi = Settlement.GarrisonWagePaymentLimit - zold - 5; limit: w pokoju cel 166 ClanBudget, w wojnie limit gry - GarrisonWarFull, BK wlacza
        // autowerbunek AI tylko w wojnie -, u gracza jego limit; tansi ochotnicy pierwsi) i "Party Size Limit" wchodza w ten sam ExplainedNumber PRZED limitem
        // dziennym (G GarrisonRecruitmentCampaignBehavior :172-200). Ludzie: gra oproznia slot notabla przy kazdym rekrucie, E4/7 A5Post zdejmuje kazdy oprozniony
        // slot osobno (TakeLevy z DOM) - drugi rekrut tej samej doby tez. Jedna regula dla zalog AI i gracza (gra wola A5 dla kazdej twierdzy z wlaczonym autowerbunkiem).
        // Bez K8 (limit gry): Straz i osady Innych (A5 tam "" - do E4/9), ksiega bez poboru (!LevyLive - A5 "" jak dotad), kraina z hamulcem wolnych rak.
        private const int K8Off = 0, K8On = 1, K8Brake = 2, K8Out = 3;
        [ThreadStatic] private static int _k8Depth;
        [ThreadStatic] private static Town _k8Town;
        [ThreadStatic] private static int _k8Game, _k8Why;

        public static void K8DepthPre() { _k8Depth++; }

        public static Exception K8DepthFin(Exception __exception) { if (_k8Depth > 0) _k8Depth--; return __exception; }

        public static void K8Postfix(Town __0, ref int __result)
        {
            if (_k8Depth > 1) return;                                          // model wewnetrzny lancucha - tylko zewnetrzny
            try
            {
                int game = __result, why = K8Off, lim = K8Limit;
                if (lim > game && __0 != null && __0.Settlement != null && Campaign.Current != null)
                {
                    var st = __0.Settlement;
                    if (!PeopleBook.LevyLive || CrownGifts.IsWatch(st.MapFaction) || OthersAt(st)) why = K8Out;
                    else if (FreeHands.Braked(st)) why = K8Brake;
                    else { why = K8On; __result = lim; }
                }
                _k8Town = __0; _k8Game = game; _k8Why = why;
            }
            catch (Exception e) { Stumble("K8Postfix", e); }
        }

        // ================================================================ K2: milicja (5.2, 5.3)
        private const int SrcTick = 0, SrcHeal = 1, SrcSiege = 2;
        // okno W1 Measure40 i podloga E4/4 (PopulationLaw.FloorFinalizer) mierza hearth wsi przed/po ticku - hearth, ktory ksiega przesunela w naszym postfiksie TEJ wsi,
        // jest juz w linii "Hearth" (o13) i nie jest podloga gry - oba finalizery go odejmuja (recenzja R1-5/R2-6: odczyt bez zerowania, kolejnosc finalizerow niepewna;
        // zerowanie tylko w MilVillagePre na poczatku ticku kazdej wsi)
        [ThreadStatic] private static Village _tickV;
        [ThreadStatic] private static double _tickDH;

        /// <summary>Finalizery ticku wsi (Measure40.VillageFin, PopulationLaw.FloorFinalizer): hearth wsi przesuniety przez ksiege w postfiksie milicji E4/8 w tym ticku (bez zerowania).</summary>
        internal static double PeekTickHearth(Village v)
        {
            if (v == null || !ReferenceEquals(_tickV, v)) return 0.0;
            return _tickDH;
        }

        private static MobileParty MilParty(Settlement s)
        {
            var mc = s != null ? s.MilitiaPartyComponent : null;
            return mc != null ? mc.MobileParty : null;
        }

        /// <summary>Szeregowi razem z wightami (milicja osady Innych - gra dopisuje ja z kultury osady, ROT ustawia osadzie kulture Innych: wighty).</summary>
        private static int AllMen(TroopRoster r)
        {
            int n = 0;
            if (r == null) return 0;
            for (int i = 0; i < r.Count; i++)
            {
                var c = r.GetCharacterAtIndex(i);
                if (c == null || c.IsHero) continue;
                int k = r.GetElementNumber(i);
                if (k > 0) n += k;
            }
            return n;
        }

        /// <summary>Miara milicji okna: ludzie (Men), w osadzie Innych - razem z wightami (recenzja R2-4: Men() pomija wighty, wiec przyrost wightow dawal d = 0).</summary>
        private static int MilMen(Settlement s, MobileParty mp, bool others)
        {
            if (mp == null) return 0;
            return others ? AllMen(mp.MemberRoster) : Men(mp.MemberRoster);
        }

        private static int MilPre(Settlement s)
        {
            if (s == null || !MilitiaOn || !PeopleBook.LevyLive) return -1;
            try { return MilMen(s, MilParty(s), OthersAt(s)); }
            catch (Exception e) { Stumble("MilPre", e); return -1; }
        }

        public static void MilVillagePre(Village __instance, out int __state) { _tickV = null; _tickDH = 0.0; __state = __instance != null ? MilPre(__instance.Settlement) : -1; }

        public static void MilVillagePost(Village __instance, int __state)
        {
            if (__state < 0 || __instance == null) return;
            float h0 = __instance.Hearth;
            MilitiaDelta(__instance.Settlement, __state, SrcTick);
            try
            {
                _tickV = __instance; _tickDH = (double)__instance.Hearth - h0;     // takze 0 - kazdy tick nadpisuje (bez starej wartosci)
            }
            catch { }
        }

        public static void MilTownPre(Town __instance, out int __state) { __state = __instance != null ? MilPre(__instance.Settlement) : -1; }
        public static void MilTownPost(Town __instance, int __state) { if (__state >= 0 && __instance != null) MilitiaDelta(__instance.Settlement, __state, SrcTick); }

        public static void MilHealPre(Settlement __0, out int __state) { __state = MilPre(__0); }
        public static void MilHealPost(Settlement __0, int __state) { if (__state >= 0) MilitiaDelta(__0, __state, SrcHeal); }

        public static void MilSiegePre(Settlement __0, out int __state) { __state = MilPre(__0); }
        public static void MilSiegePost(Settlement __0, int __state) { if (__state >= 0) MilitiaDelta(__0, __state, SrcSiege); }

        /// <summary>
        /// Wspolne cialo okien milicji: d = szeregowi po - przed. d &gt; 0: Straz - "" (do E4/9); osada Innych i kraina z hamulcem - zdjeci; inaczej TakeLevy z osady
        /// (MLevyMilitia) i udzial do slownika milicji, czego DOM nie dal - zdjeci (ulamek czlowieka - ""); d &lt; 0 (ubytek bez walki): udzialy ze slownika do DOM tej osady
        /// (inne regiony - HomeKnown, "" - Note). Nie rzuca.
        /// </summary>
        private static void MilitiaDelta(Settlement s, int men0, int src)
        {
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var mp = MilParty(s);
                bool others = OthersAt(s);
                int men1 = MilMen(s, mp, others);
                int d = men1 - men0;
                if (d == 0) return;
                var day = D;
                if (others)
                {
                    // osada Innych (recenzja R2-4; przed Straza - dawna osada Strazy przejeta przez Innych ma kulture Daru z kalibracji): wight z niczego nie powstaje -
                    // przyrost (wighty razem z ludzmi) zdjety; ubytek - partia Innych bez slownika skladu (KUndead), wighty to nie ludzie: tylko licznik ticku (N7)
                    if (d > 0)
                    {
                        day.mUp += d; if (src == SrcTick) day.mTickUp += d;
                        int st = StripMost(mp.MemberRoster, d, true); day.r2 += st;
                        if (src == SrcTick) day.mTickUp -= st;
                    }
                    else if (src == SrcTick) day.mTickDown += -d;
                    return;
                }
                if (d > 0)
                {
                    var r = mp.MemberRoster;
                    day.mUp += d; if (src == SrcTick) day.mTickUp += d;
                    if (WatchAt(s))
                    {
                        // E4/9 (Y2): milicja osady frakcji Strazy - z Westeros (Levy); osada Daru poza Straza - "" jak dotad (Dar - tylko do plonu, P R5)
                        if (CrownGifts.IsWatch(s.MapFaction)) WatchLaw.GiveLevy(r, d, WatchLaw.KMilitia, PeopleBook.FWatchArms);
                        else PeopleBook.GiveNone(r, d, PeopleBook.FWatchArms);
                        day.m2 += d; return;
                    }
                    if (FreeHands.Braked(s))
                    {
                        int st = StripMost(r, d); day.r1 += st;
                        if (src == SrcTick) day.mTickUp -= st;
                        try { var reg = OutlawLaw.RegionFor(s); if (reg != null && reg.StringId != null && day.r1Reg.Add(reg.StringId)) { string k = FreeHands.RealmOf(reg); int v; day.r1Realm.TryGetValue(k, out v); day.r1Realm[k] = v + 1; } } catch { }
                        if (d - st > 0) PeopleBook.GiveNone(r, d - st, PeopleBook.FArmsFloor);
                        return;
                    }
                    Settlement region;
                    double got = PeopleBook.TakeLevy(s, d, PeopleBook.MLevyMilitia, out region, PeopleBook.MLevyMilitia);
                    int keep = Math.Min(d, Ceil(got));
                    if (got > Eps) PeopleBook.GiveNew(r, region, got, PeopleBook.FMilitiaLevy);
                    if (keep - got > Eps) { PeopleBook.GiveNone(r, keep - got, PeopleBook.FArmsFloor); day.mFrac += keep - got; }
                    int strip = d - keep;
                    if (strip > 0)
                    {
                        int st = StripMost(r, strip); day.r3 += st;
                        if (src == SrcTick) day.mTickUp -= st;
                        if (strip - st > 0) PeopleBook.GiveNone(r, strip - st, PeopleBook.FArmsFloor);
                    }
                    day.m1 += got;
                    if (s.IsVillage) day.m1v += keep; else if (s.IsTown) day.m1t += keep; else day.m1c += keep;
                    if (src == SrcTick) day.m1k += keep; else if (src == SrcHeal) day.m1h += keep; else day.m1o += keep;
                }
                else
                {
                    int n = -d;
                    day.hAll += n; if (src == SrcTick) day.mTickDown += n;
                    var sh = PeopleBook.Take(mp != null ? mp.MemberRoster : null, n, PeopleBook.FMilitiaHome);
                    var a = PeopleBook.ArriveAt(s, sh, PeopleBook.AService, PeopleBook.MBackMilitia, PeopleBook.MBackMilitiaNone, false);
                    day.h1 += a.Here; day.h2 += a.Other; day.h3 += a.None; day.h4 += a.Wall;
                }
            }
            catch (Exception e) { Stumble("MilitiaDelta", e); }
            finally { D.ticks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>Okno E4/7 rozwiazania partii (5.6): ludzie rozwiazanej partii, ktorych gra dopisala do milicji wsi.</summary>
        internal static void NoteMilitiaDisband(double men) { if (men > 0.0) D.j += men; }
        /// <summary>m97-wioski krok 2: obroncy wioski pobocznej zdjeci z DOM przyczyna MLevyMilitia (SideVillageVisit.StartFight) - skladnik ob KONTROLI linii E4/8.</summary>
        internal static void NoteVillageDefenders(double men) { if (men > 0.0 && !double.IsInfinity(men)) D.ob += men; }

        // ================================================================ K3: nowe druzyny lordow (6.2-6.4)
        public sealed class SpawnState { public Dictionary<CharacterObject, int> Before; public int Grain0; }

        private static int Grain(MobileParty mp) { try { return mp != null && mp.ItemRoster != null ? mp.ItemRoster.GetItemNumber(DefaultItems.Grain) : 0; } catch { return 0; } }

        public static void SpawnPre(MobileParty __0, Hero __1, out SpawnState __state)
        {
            __state = null;
            try { if (__0 != null && __1 != null) __state = new SpawnState { Before = Regulars(__0.MemberRoster), Grain0 = Grain(__0) }; }
            catch (Exception e) { __state = null; Stumble("SpawnPre", e); }
        }

        public static void SpawnPost(MobileParty __0, Hero __1, SpawnState __state)
        {
            if (__state == null || __0 == null || __1 == null) return;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var day = D;
                day.p++;
                if (Undead.Party(__0) || OthersRise.IsWhiteWalkerClan(__1.Clan)) { day.p2++; try { OthersGrain(__0, __state.Grain0); } catch (Exception e) { Stumble("SpawnPost(zboze Innych)", e); } return; }     // Inni: E4/2 tnie szablon do puli CIALA
                try { SpawnGrain(__0, __1, __state.Grain0); } catch (Exception e) { Stumble("SpawnPost(zboze)", e); }
                if (__0 == MobileParty.MainParty || __1.Clan == Clan.PlayerClan) { day.p3++; return; }      // rod gracza - bez szablonu (gra)
                var r = __0.MemberRoster;
                var added = AddedOf(__state.Before, r);
                int n = 0; foreach (var v in added.Values) n += v;
                if (n <= 0) return;
                bool watch = CrownGifts.IsWatch(__1.MapFaction);
                // 159 (NewLordPartyArmedMenOnly, Jeff 26.10 "Pozyczka + mniejsze"): tylko ludzie, ktorych pan ubierze (NewLordKit.Plan - wycena na polce najblizszego
                // przyjaznego miasta, zloto ponad rezerwe i pozyczka Banku do limitu); reszta szablonu zdjeta PRZED rachunkiem szablonu i poborem z DOM - zostaja we
                // wsi (z ksiegi ludzi nic nie schodzi). "szablon" linii E4/8 = wzieci (kontrola N8 bez zmian), zdjeci osobno "bez sprzetu zostali we wsi (159)"
                if (!watch && SpawnOn && PeopleBook.LevyLive)
                {
                    try
                    {
                        var kitKeep = NewLordKit.Plan(__0, __1, added);
                        if (kitKeep != null)
                        {
                            int cut = 0;
                            foreach (var kv in added)
                            {
                                int k; kitKeep.TryGetValue(kv.Key, out k);
                                int x = Math.Min(kv.Value, kv.Value - Math.Max(0, k));
                                if (x <= 0) continue;
                                r.AddToCounts(kv.Key, -x);
                                cut += x;
                            }
                            n -= cut; day.kitCut += cut;
                            if (n <= 0) return;
                        }
                    }
                    catch (Exception e) { Stumble("SpawnPost(159)", e); }
                }
                day.n += n;
                if (watch) { day.p1++; WatchLaw.GiveLevy(r, n, WatchLaw.KSpawn, PeopleBook.FWatchArms); day.a4 += n; return; }   // E4/9 (Y3): szablon Strazy z Westeros (pulap - SpawnPrefix)
                if (!SpawnOn || !PeopleBook.LevyLive) { day.nOff += n; return; }      // jak dotad (uzgodnienie E4/7 nazwie "z niczego - partie rodow")
                bool foreign;
                var seat = SeatOf(__1, __0, out foreign);
                bool braked = FreeHands.Braked(seat);
                double a1 = 0.0; Settlement reg = null;
                if (!braked && seat != null) a1 = PeopleBook.TakeLevy(seat, n, PeopleBook.MLevySpawn, out reg, PeopleBook.MLevySpawn);
                int keep = Math.Min(n, Ceil(a1));
                if (a1 > Eps) PeopleBook.GiveNew(r, reg, a1, PeopleBook.FSpawnLevy);
                if (keep - a1 > Eps) { PeopleBook.GiveNone(r, keep - a1, PeopleBook.FArmsFloor); day.aFrac += keep - a1; }
                day.a1 += a1;
                int rest = n - keep;
                if (rest > 0)
                {
                    // recenzja R2-2: najpierw zdjeci WSZYSCY ludzie szablonu, ktorych DOM nie dal (ksztalt szablonu, 6.3), dopiero potem dopisani ludzie zalogi
                    // (bez dalszego zdejmowania) - koncowe zdjecie liczone od stanu sprzed szablonu bralo tez weteranow zalogi
                    int st = StripAdded(r, __state.Before, rest);
                    if (rest - st > 0) { PeopleBook.GiveNone(r, rest - st, PeopleBook.FArmsFloor); day.aFrac += rest - st; }
                    int t = 0; bool tried = false;
                    if (st > 0 && SpawnGarrisonOn && !foreign)
                    {
                        try { t = FromSeatGarrison(__0, __1, seat, st, out tried); }
                        catch (Exception e) { Stumble("SpawnPost(zaloga)", e); }
                    }
                    day.a2 += t;
                    int miss = st - t;
                    if (miss > 0) { if (tried) day.a3g += miss; else if (braked) day.a3h += miss; else day.a3f += miss; }
                }
            }
            catch (Exception e) { Stumble("SpawnPost", e); }
            finally { D.ticks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>
        /// Siedziba lorda jako zrodlo poboru szablonu (i zalogi): HomeSettlement (wies, miasto, zamek) nie w wojnie z frakcja lorda i nieoblegana; recenzja R1-7: siedziba
        /// wroga (rod, ktory stracil lenno - dom w krolestwie, z ktorym walczy) albo oblezona - pobor z regionu pozycji partii (gra stawia nowa druzyne przy osadzie swojej
        /// frakcji bez bitwy, SettlementHelper.GetBestSettlementToSpawnAround), zaloga siedziby nie (foreign = true). Siedziba neutralna zostaje (niezalezne mniejsze rody
        /// bez lenna maja dom w cudzym krolestwie - przy scislej rownosci frakcji ich zboze szloby z niczego, prog N9). Zaloga i tak tylko tej samej frakcji (FromSeatGarrison).
        /// Brak siedziby - region pozycji jak dotad.
        /// </summary>
        private static Settlement SeatOf(Hero h, MobileParty mp, out bool foreign)
        {
            foreign = false;
            var s = h != null ? h.HomeSettlement : null;
            if (s != null && (s.IsVillage || s.IsTown || s.IsCastle))
            {
                if (SeatUsable(s, h)) return s;
                foreign = true;
            }
            try { return mp != null ? OutlawLaw.RegionAt(mp.Position.ToVec2()) : null; } catch { return null; }
        }

        /// <summary>Siedziba nie w wojnie z frakcja lorda i nieoblegana (wies - jej warownia).</summary>
        private static bool SeatUsable(Settlement s, Hero h)
        {
            if (s == null || h == null) return false;
            var f = h.MapFaction; var sf = s.MapFaction;
            if (f != null && sf != null && sf != f && FactionManager.IsAtWarAgainstFaction(f, sf)) return false;
            var fort = s.IsVillage && s.Village != null ? s.Village.Bound : s;
            return fort == null || !fort.IsUnderSiege;
        }

        /// <summary>6.2 (b): do `want` zdrowych ludzi zalogi siedziby (tej samej frakcji) do druzyny - ludzi szablonu zdjal juz wolajacy (recenzja R2-2); zaloga zostawia
        /// 50 w miescie / 25 w zamku (rezerwa gry). Zaloga w bitwie albo oblezeniu - niedostepna (recenzja R2-3, jak ClanBudget przeglad C1 uwaga 5).</summary>
        private static int FromSeatGarrison(MobileParty party, Hero hero, Settlement seat, int want, out bool tried)
        {
            tried = false;
            var gs = seat != null && seat.IsVillage && seat.Village != null ? seat.Village.Bound : seat;
            if (gs == null || gs.Town == null || gs.MapFaction != hero.MapFaction) return 0;
            var g = gs.Town.GarrisonParty;
            if (g == null || g == party) return 0;
            tried = true;
            if (!g.IsActive || g.MapEvent != null || gs.IsUnderSiege || (gs.Party != null && gs.Party.MapEvent != null)) return 0;     // lista MapEventParty i straty bitwy - roster nietykalny
            var gr = g.MemberRoster;
            var healthy = new Dictionary<CharacterObject, int>();
            int hs = 0;
            for (int i = 0; i < gr.Count; i++)
            {
                var c = gr.GetCharacterAtIndex(i);
                if (c == null || c.IsHero || PeopleBook.IsWight(c)) continue;
                int k = Math.Max(0, gr.GetElementNumber(i) - gr.GetElementWoundedNumber(i));
                if (k <= 0) continue;
                int v; healthy.TryGetValue(c, out v); healthy[c] = v + k; hs += k;
            }
            int t = Math.Min(want, hs - (gs.IsTown ? 50 : 25));
            if (t <= 0) return 0;
            var moved = Split(healthy, t);
            int m = 0;
            foreach (var kv in moved) { gr.AddToCounts(kv.Key, -kv.Value); party.MemberRoster.AddToCounts(kv.Key, kv.Value); m += kv.Value; }
            PeopleBook.Give(party.MemberRoster, PeopleBook.Take(gr, m, PeopleBook.FSpawnGarrison), PeopleBook.FSpawnGarrison);
            try { GarrisonArmory.KitsWithMen(g, party, gs, moved); } catch (Exception e) { Stumble("FromSeatGarrison(sprzet)", e); }
            return m;
        }

        /// <summary>6.2 pkt 6: zboze, ktore gra dala z niczego (15-30), schodzi z zapasu siedziby (Town.FoodStocks; 1 jednostka = 1 sztuka zboza), inaczej najblizszej
        /// warowni tej samej frakcji; brak - zostaje (z niczego, licznik). Recenzja R1-7: siedziba nie wroga i nieoblegana (SeatUsable), kazda warownia (siedziba tez)
        /// z zapasem co najmniej 2g (nie schodzi do zera - glod E4/6), oblezone pomijane. Tylko po starcie kampanii (nowa gra - jak dotad).</summary>
        private static void SpawnGrain(MobileParty mp, Hero h, int grain0)
        {
            if (!GrainOn || Campaign.Current == null || !Campaign.Current.GameStarted) return;
            int g = Grain(mp) - grain0;
            if (g <= 0) return;
            var day = D;
            day.g += g; day.gN++;
            var seat = h.HomeSettlement;
            var home = seat != null && seat.IsVillage && seat.Village != null ? seat.Village.Bound : seat;
            var f = h.MapFaction;
            if (home != null && home.Town != null && SeatUsable(home, h) && home.Town.FoodStocks >= 2f * g) { home.Town.FoodStocks -= g; day.g1 += g; GoodsLedger.NoteSource(DefaultItems.Grain, g, "ze spichlerza siedziby"); return; }   // m6b (5): zrodlo z platnikiem w ksiedze towarow
            Settlement best = null; float bd = float.MaxValue;
            var pos = mp.Position.ToVec2();
            foreach (var t in Town.AllFiefs)
            {
                if (t == null || t.Settlement == null || t.Settlement.MapFaction != f || t.Settlement.IsUnderSiege || t.FoodStocks < 2f * g) continue;
                float dd = pos.DistanceSquared(t.Settlement.GetPosition2D);
                if (dd < bd) { bd = dd; best = t.Settlement; }
            }
            if (best != null) { best.Town.FoodStocks -= g; day.g2 += g; GoodsLedger.NoteSource(DefaultItems.Grain, g, "ze spichlerza innej warowni"); return; }
            day.g3 += g;
            // m6b (5, FoodSourcesInLedger): zadna warownia krolestwa nie ma zapasu - zboze nie powstaje z niczego; druzyna rusza bez niego i kupi zywnosc
            // na najblizszym targu (gra: PartiesBuyFoodCampaignBehavior, BK zaopatrzenie); glod bez zywnosci to tylko kara morale (G FoodConsumptionBehavior.cs:195-201)
            if (GoodsLedger.SourcesOn && mp.ItemRoster != null)
            {
                int have = mp.ItemRoster.GetItemNumber(DefaultItems.Grain);
                int take = Math.Min(g, have);
                if (take > 0) { mp.ItemRoster.AddToCounts(DefaultItems.Grain, -take); day.g3x += take; }
            }
        }

        /// <summary>m6b (5): zboze gry (15-30) dla nowej druzyny Innych - SpawnGrain ich pomija (E4/2); w ksiedze towarow nazwane jako zrodlo z niczego.</summary>
        private static void OthersGrain(MobileParty mp, int grain0)
        {
            if (Campaign.Current == null || !Campaign.Current.GameStarted) return;
            int g = Grain(mp) - grain0;
            if (g <= 0) return;
            D.gOth += g;
            GoodsLedger.NoteSource(DefaultItems.Grain, g, "druzyny Innych - z niczego");
        }

        public static void GiftsPre(MobileParty __0, out long[] __state)
        {
            __state = null;
            try { if (__0 != null && __0.ItemRoster != null) __state = Goods(__0.ItemRoster); } catch (Exception e) { Stumble("GiftsPre", e); }
        }

        public static Exception GiftsFin(Exception __exception, MobileParty __0, long[] __state)
        {
            // E6/K1 (211, NewPartyHorsesFromSeat): dary obsluzyl HorseFate - konie i zywnosc kupione z polki siedziby, nie dar z niczego (licznik E4/8 bez nich)
            bool seat = false;
            try { seat = HorseFate.TakeGiftRun(); } catch { }
            try { if (!seat && __state != null && __0 != null && __0.ItemRoster != null) { var now = Goods(__0.ItemRoster); D.gifts += now[0] - __state[0]; D.giftsValue += now[1] - __state[1]; } }
            catch (Exception e) { Stumble("GiftsFin", e); }
            return __exception;
        }

        private static long[] Goods(ItemRoster ir)
        {
            long n = 0, v = 0;
            for (int i = 0; i < ir.Count; i++)
            {
                var el = ir.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0) continue;
                n += el.Amount; v += (long)it.Value * el.Amount;
            }
            return new[] { n, v };
        }

        // ================================================================ K4: karawany (7.1, 7.2)
        [ThreadStatic] private static int _inCaravan;

        public static void CaravanPre(out bool __state) { _inCaravan++; __state = true; }

        public static Exception CaravanFin(Exception __exception, bool __state) { if (__state && _inCaravan > 0) _inCaravan--; return __exception; }

        public static void CaravanPost(Settlement __1, bool __3, MobileParty __result)
        {
            if (__result == null || __1 == null || __3 || !CaravansOn || !PeopleBook.LevyLive) return;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var r = __result.MemberRoster;
                int n = Men(r);
                if (n <= 0) return;
                var day = D;
                day.k++; day.kMen += n;
                if (WatchAt(__1))
                {
                    // E4/9 (Y4): karawana z osady frakcji Strazy - z Westeros (Levy); osada Daru poza Straza - "" jak dotad
                    if (CrownGifts.IsWatch(__1.MapFaction)) WatchLaw.GiveLevy(r, n, WatchLaw.KCaravan, PeopleBook.FWatchArms);
                    else PeopleBook.GiveNone(r, n, PeopleBook.FWatchArms);
                    day.cWatch += n; return;
                }
                Settlement reg;
                double got = PeopleBook.TakeLevy(__1, n, PeopleBook.MCaravanOut, out reg, PeopleBook.MCaravanOut);      // bez hamulca wolnych rak (Jeff P1)
                int keep = Math.Min(n, Ceil(got));
                if (got > Eps) PeopleBook.GiveNew(r, reg, got, PeopleBook.FCaravan);
                if (keep - got > Eps) { PeopleBook.GiveNone(r, keep - got, PeopleBook.FArmsFloor); day.cFrac += keep - got; }
                day.c1 += got;
                if (__1.IsTown) day.c1t += got; else if (__1.IsCastle) day.c1c += got; else day.c1v += got;
                int strip = n - keep;
                if (strip > 0)
                {
                    CharacterObject master = null;
                    try { var o = __result.Party != null ? __result.Party.Owner : null; master = o != null && o.Culture != null ? o.Culture.CaravanMaster : null; } catch { }
                    int st = StripMost2(r, strip, master);
                    day.c2 += st;
                    if (strip - st > 0) { PeopleBook.GiveNone(r, strip - st, PeopleBook.FArmsFloor); day.c2Master += strip - st; }      // mistrz karawany zostaje zawsze
                }
            }
            catch (Exception e) { Stumble("CaravanPost", e); }
            finally { D.ticks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>StripMost bez jednego typu (mistrz karawany).</summary>
        private static int StripMost2(TroopRoster r, int n, CharacterObject keep)
        {
            if (keep == null) return StripMost(r, n);
            var w = Regulars(r);
            w.Remove(keep);
            int done = 0;
            foreach (var kv in Split(w, n)) { if (kv.Value <= 0) continue; r.AddToCounts(kv.Key, -kv.Value); done += kv.Value; }
            return done;
        }

        public static void RefillPre(MobileParty __0, out Dictionary<CharacterObject, int> __state)
        {
            __state = null;
            try { if (_inCaravan == 0 && __0 != null && CaravansOn && PeopleBook.LevyLive) __state = Regulars(__0.MemberRoster); }
            catch (Exception e) { __state = null; Stumble("RefillPre", e); }
        }

        public static void RefillPost(MobileParty __0, Dictionary<CharacterObject, int> __state)
        {
            if (__state == null || __0 == null) return;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var r = __0.MemberRoster;
                int d = 0; foreach (var v in AddedOf(__state, r).Values) d += v;
                if (d <= 0) return;
                var day = D;
                day.c3N++;
                var st = __0.CurrentSettlement;
                if (st == null || WatchAt(st))
                {
                    // E4/9 (Y4): konwoj uzupelniony w osadzie frakcji Strazy - z Westeros (Levy); poza miastem albo osada Daru poza Straza - "" jak dotad
                    if (st != null && CrownGifts.IsWatch(st.MapFaction)) WatchLaw.GiveLevy(r, d, WatchLaw.KCaravan, PeopleBook.FWatchArms);
                    else PeopleBook.GiveNone(r, d, PeopleBook.FWatchArms);
                    day.cWatch += d; return;
                }
                Settlement reg;
                double got = PeopleBook.TakeLevy(st, d, PeopleBook.MCaravanRefill, out reg, PeopleBook.MCaravanRefill);
                int keep = Math.Min(d, Ceil(got));
                if (got > Eps) PeopleBook.GiveNew(r, reg, got, PeopleBook.FCaravanRefill);
                if (keep - got > Eps) { PeopleBook.GiveNone(r, keep - got, PeopleBook.FArmsFloor); day.cFrac += keep - got; }
                day.c3 += got;
                if (d - keep > 0)
                {
                    int s2 = StripAdded(r, __state, d - keep);
                    day.c4 += s2;
                    if (d - keep - s2 > 0) PeopleBook.GiveNone(r, d - keep - s2, PeopleBook.FArmsFloor);
                }
            }
            catch (Exception e) { Stumble("RefillPost", e); }
            finally { D.ticks += Stopwatch.GetTimestamp() - t0; }
        }

        // ================================================================ koniec partii bez zniszczenia (recenzja R1-1/R2-5): sklad przed MobileParty.RemoveParty
        public static void RemovePre(MobileParty __instance, out bool __state)
        {
            __state = false;
            try { __state = PeopleBook.RemovingPre(__instance); } catch (Exception e) { Stumble("RemovePre", e); }
        }

        public static Exception RemoveFin(Exception __exception, MobileParty __instance, bool __state)
        {
            if (__state) { try { PeopleBook.RemovingDone(__instance); } catch (Exception e) { Stumble("RemoveFin", e); } }
            return __exception;
        }

        // ================================================================ K5: wedrowcy i konwoje BK (8.2) - ramka tworzenia
        public sealed class BkFrame { public Settlement Origin; public BkFrame Prev; public readonly List<MobileParty> Parties = new List<MobileParty>(1); }
        [ThreadStatic] private static BkFrame _bkTop;

        private static BkFrame Push(Settlement origin) { var f = new BkFrame { Origin = origin, Prev = _bkTop }; _bkTop = f; return f; }

        public static void TravPre(Settlement __1, out BkFrame __state) { __state = Push(__1); }
        public static void CargoPre(Settlement __0, out BkFrame __state) { __state = Push(__0); }

        public static Exception BkFin(Exception __exception, BkFrame __state)
        {
            if (__state == null) return __exception;
            _bkTop = __state.Prev;
            long t0 = Stopwatch.GetTimestamp();
            foreach (var mp in __state.Parties)
            {
                try { RoadOut(__state.Origin, mp); } catch (Exception e) { Stumble("BkFin", e); }
            }
            D.ticks += Stopwatch.GetTimestamp() - t0;
            return __exception;
        }

        /// <summary>Nasluch MobilePartyCreated: partia ludnosci BK powstala w otwartej ramce (CreateTravellerParty / CreateFoodCaravan / CreateSlaveCaravan).</summary>
        internal static void OnPartyCreated(MobileParty mp)
        {
            var f = _bkTop;
            if (f == null || mp == null) return;
            try { var c = mp.PartyComponent; if (c != null && c.GetType().Name == "PopulationPartyComponent") f.Parties.Add(mp); }
            catch (Exception e) { Stumble("OnPartyCreated", e); }
        }

        /// <summary>Konwoj BK (zywnosc, niewolnicy, towar) - nie wedrowcy (rodziny, ktore przenosza sie do celu).</summary>
        internal static bool IsBkCargo(MobileParty mp)
        {
            try
            {
                var c = mp != null ? mp.PartyComponent : null;
                if (c == null) return false;
                if (_pKind == null) _pKind = AccessTools.Property(c.GetType(), "Kind");
                if (_pSlave == null) _pSlave = AccessTools.Property(c.GetType(), "SlaveCaravan");
                object k = _pKind != null ? _pKind.GetValue(c, null) : null;
                if (k != null && k.ToString() != "Unset") return true;
                object sl = _pSlave != null ? _pSlave.GetValue(c, null) : null;
                return sl is bool && (bool)sl;
            }
            catch { return false; }
        }

        /// <summary>8.2: sklad nowej partii BK (juz wypelniony) z DOM osady pochodzenia - bez hamulca (rodziny, nie rekruci); czego DOM nie da - "" (bez zdejmowania:
        /// BK liczy u celu cywilow ze skladu, a "" nie dotyka DOM u celu). Niewolnicy w lochu konwoju - "" (E4/9).</summary>
        private static void RoadOut(Settlement origin, MobileParty mp)
        {
            if (mp == null || !TravellersOn || !PeopleBook.LevyLive || !PeopleBook.IsRoadParty(mp)) return;
            var day = D;
            bool cargo = IsBkCargo(mp);
            var r = mp.MemberRoster;
            int n = Men(r);
            day.w++; if (cargo) day.wCargo++;
            if (n > 0)
            {
                // E4/9 (Y5): wedrowcy i konwoje BK z Daru - zwykla regula R19 (migracja rodzin z DOM osady, nie sluzba Strazy); bez osady pochodzenia - "" jak dotad
                if (origin == null || (WatchAt(origin) && !WatchLaw.Live)) { PeopleBook.GiveNone(r, n, PeopleBook.FWatchArms); day.n3 += n; }
                else
                {
                    Settlement reg;
                    double got = PeopleBook.TakeLevy(origin, n, PeopleBook.MRoadOut, out reg, PeopleBook.MRoadOut);
                    if (got > Eps) PeopleBook.GiveNew(r, reg, got, PeopleBook.FRoadOut);
                    if (n - got > Eps) PeopleBook.GiveNone(r, n - got, PeopleBook.FRoadNone);
                    if (cargo) day.n1e += got; else day.n1w += got;
                    day.n3 += Math.Max(0.0, n - got);
                }
            }
            int sl = Men(mp.PrisonRoster);
            if (sl > 0) { PeopleBook.GiveNone(mp.PrisonRoster, sl, PeopleBook.FSlavesBk); day.s += sl; }
        }

        /// <summary>PeopleBook.ArmsPartyGone: wedrowcy/konwoj u celu (1 do domu, 2 do innej osady regionu, 3 migracja, 4 zniszczeni w drodze - do domu, 5 eskorta konwoju).</summary>
        internal static void NoteRoadEnd(int what, PeopleBook.Arrive a)
        {
            var d = D;
            double placed = a.Here + a.Other;
            switch (what)
            {
                case 1: d.b1N++; d.b1 += placed; break;
                case 2: d.b2N++; d.b2 += placed; break;
                case 3: d.b3N++; d.b3 += placed; break;
                case 4: d.b4N++; d.b4 += placed; break;
                default: d.b5N++; d.b5 += placed; break;
            }
            d.bNone += a.None; d.bOther += a.Other;
        }

        internal static void NoteSlavesEnd(double n) { if (n > 0.0) D.sEnd += n; }

        internal static void NoteDisbandHome(int kind, PeopleBook.Arrive a, bool removed, double vet)
        {
            var d = D;
            d.q++; d.qHere += a.Here; d.qOther += a.Other; d.qNone += a.None; d.qWall += a.Wall; d.qVet += vet;   // vet: R6/W5 - do karczmy (E4/10, strumien "rozwiazane partie")
            if (removed) d.qRemoved++;
            int v; d.qKind.TryGetValue(kind, out v); d.qKind[kind] = v + 1;
        }

        // ================================================================ wpiecie (SubModuleMain, po Devastation/Famine i po PeopleBook.ApplyOrigin)
        private static void Wire(Harmony h, string name, MethodBase m, HarmonyMethod pre, HarmonyMethod post, HarmonyMethod fin)
        {
            try
            {
                _targets.Add(new KeyValuePair<string, MethodBase>(name, m));
                if (m == null) { _missing.Add(name); return; }
                h.Patch(m, prefix: pre, postfix: post, finalizer: fin);
                _wired.Add(name);
            }
            catch (Exception e) { _missing.Add(name + " (" + e.GetType().Name + ")"); Stumble("Wire(" + name + ")", e); }
        }

        internal static void ApplyAll(Harmony h)
        {
            if (_applied || h == null) return;
            _applied = true;
            Func<string, HarmonyMethod> P = n => new HarmonyMethod(typeof(ArmsFromPeople), n);
            Func<string, int, HarmonyMethod> PP = (n, prio) => new HarmonyMethod(typeof(ArmsFromPeople), n) { priority = prio };
            var grc = typeof(GarrisonRecruitmentCampaignBehavior);
            Wire(h, "przyrost zalog z modelu (TickGarrisonChangeForTown)", AccessTools.Method(grc, "TickGarrisonChangeForTown", new[] { typeof(Town) }), PP(nameof(GarrisonGrowthPrefix), Priority.Low), null, P(nameof(GarrisonGrowthFin)));
            Wire(h, "A5 - okno pomiaru (TickAutoRecruitmentGarrisonChange)", AccessTools.Method(grc, "TickAutoRecruitmentGarrisonChange", new[] { typeof(Town) }), P(nameof(A5MeasurePre)), null, P(nameof(A5MeasureFin)));
            Wire(h, "A5 - limity (GetAutoRecruitmentGarrisonChangeExplainedNumber)", AccessTools.Method(grc, "GetAutoRecruitmentGarrisonChangeExplainedNumber", new[] { typeof(Town) }), null, P(nameof(A5LimitPostfix)), null);
            // K8: kazda zadeklarowana GetMaximumDailyAutoRecruitmentCount(Town) modeli zalogi (gra, BK, ROT, NavalDLC) - licznik zagniezdzenia + postfiks
            var k8 = new List<MethodInfo>();
            try
            {
                var gm = typeof(TaleWorlds.CampaignSystem.ComponentInterfaces.SettlementGarrisonModel);
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException rtle) { types = rtle.Types; }
                    catch { continue; }
                    if (types == null) continue;
                    foreach (var t in types)
                    {
                        try
                        {
                            if (t == null || t.IsAbstract || !gm.IsAssignableFrom(t)) continue;
                            var m = t.GetMethod("GetMaximumDailyAutoRecruitmentCount", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, new[] { typeof(Town) }, null);
                            if (m != null && !m.IsAbstract && !k8.Contains(m)) k8.Add(m);
                        }
                        catch { }
                    }
                }
            }
            catch (Exception e) { Stumble("ApplyAll(K8 modele)", e); }
            if (k8.Count == 0) Wire(h, "K8 - limit dzienny A5 (GetMaximumDailyAutoRecruitmentCount)", null, null, null, null);
            foreach (var m in k8)
                Wire(h, "K8 - limit dzienny A5 (" + m.DeclaringType.Name + ".GetMaximumDailyAutoRecruitmentCount)", m, PP(nameof(K8DepthPre), Priority.First), P(nameof(K8Postfix)), P(nameof(K8DepthFin)));
            Wire(h, "milicja - tick wsi (Village.DailyTick)", AccessTools.Method(typeof(Village), "DailyTick"), PP(nameof(MilVillagePre), Priority.First), P(nameof(MilVillagePost)), null);
            Wire(h, "milicja - tick warowni (Town.DailyTick)", AccessTools.Method(typeof(Town), "DailyTick"), PP(nameof(MilTownPre), Priority.First), P(nameof(MilTownPost)), null);
            Wire(h, "milicja po odbudowie wsi (IncreaseSettlementHealthAction.ApplyInternal)", AccessTools.Method(typeof(IncreaseSettlementHealthAction), "ApplyInternal", new[] { typeof(Settlement), typeof(float) }), P(nameof(MilHealPre)), P(nameof(MilHealPost)), null);
            Wire(h, "milicja po obronie oblezenia (OnAfterSiegeCompleted)", AccessTools.Method(typeof(MilitiasCampaignBehavior), "OnAfterSiegeCompleted"), P(nameof(MilSiegePre)), P(nameof(MilSiegePost)), null);
            Wire(h, "nowa druzyna lorda (InitializeLordPartyProperties)", AccessTools.Method(typeof(LordPartyComponent.InitializationArgs), "InitializeLordPartyProperties", new[] { typeof(MobileParty), typeof(Hero) }),
                 PP(nameof(SpawnPre), Priority.First), PP(nameof(SpawnPost), Priority.Low), null);
            Wire(h, "dary startowe druzyny - pomiar (GiveInitialItemsToParty)", AccessTools.Method(typeof(HeroSpawnCampaignBehavior), "GiveInitialItemsToParty", new[] { typeof(MobileParty) }), P(nameof(GiftsPre)), null, P(nameof(GiftsFin)));
            Wire(h, "karawany (CreateCaravanParty)", AccessTools.Method(typeof(CaravanPartyComponent), "CreateCaravanParty"), P(nameof(CaravanPre)), PP(nameof(CaravanPost), Priority.Low), P(nameof(CaravanFin)));
            Wire(h, "koniec partii bez zniszczenia - sklad przed (MobileParty.RemoveParty)", AccessTools.Method(typeof(MobileParty), "RemoveParty", Type.EmptyTypes), PP(nameof(RemovePre), Priority.First), null, P(nameof(RemoveFin)));
            Wire(h, "konwoje morskie (RefillConvoyTroops)", AccessTools.Method(typeof(CaravansCampaignBehavior), "RefillConvoyTroops", new[] { typeof(MobileParty) }), P(nameof(RefillPre)), P(nameof(RefillPost)), null);
            var ppc = AccessTools.TypeByName("BannerKings.Components.PopulationPartyComponent");
            MethodInfo trav = null, food = null, slave = null;
            if (ppc != null)
            {
                trav = AccessTools.Method(ppc, "CreateTravellerParty");
                food = AccessTools.Method(ppc, "CreateFoodCaravan");
                slave = AccessTools.Method(ppc, "CreateSlaveCaravan");
                // parametry po pozycji - typ pochodzenia sprawdzony (niezgodnosc = brak wpiecia, wedrowcy jak dotad)
                if (trav != null && (trav.GetParameters().Length < 2 || trav.GetParameters()[1].ParameterType != typeof(Settlement))) trav = null;
                if (food != null && (food.GetParameters().Length < 1 || food.GetParameters()[0].ParameterType != typeof(Settlement))) food = null;
                if (slave != null && (slave.GetParameters().Length < 1 || slave.GetParameters()[0].ParameterType != typeof(Settlement))) slave = null;
            }
            Wire(h, "wedrowcy BK (CreateTravellerParty)", trav, P(nameof(TravPre)), null, P(nameof(BkFin)));
            Wire(h, "konwoje BK - zywnosc (CreateFoodCaravan)", food, P(nameof(CargoPre)), null, P(nameof(BkFin)));
            Wire(h, "konwoje BK - niewolnicy (CreateSlaveCaravan)", slave, P(nameof(CargoPre)), null, P(nameof(BkFin)));
            WireRetinue(h, P);   // 295: orszaki dworow BK (RetinueComponent.DailyTick) - ArmsFromPeople.Retinue295.cs
            Log.Info("Wojsko z ludzi (E4/8): latki przy starcie gry " + _wired.Count + " z " + _targets.Count + "; BRAK: " + (_missing.Count > 0 ? string.Join(", ", _missing.ToArray()) : "-")
                     + "; pelna linia z wlascicielami latek - w pierwszej dobie kampanii.");
        }

        private static string Owners()
        {
            var parts = new List<string>();
            foreach (var kv in _targets)
            {
                try
                {
                    if (kv.Value == null) continue;
                    var info = Harmony.GetPatchInfo(kv.Value);
                    if (info == null) continue;
                    var o = info.Owners.Distinct().ToList();
                    if (o.Count > 0) parts.Add(kv.Value.Name + ": " + string.Join("/", o.ToArray()));
                }
                catch { }
            }
            return parts.Count > 0 ? string.Join("; ", parts.ToArray()) : "-";
        }

        private static string StartLine()
        {
            var s = S;
            var sb = new StringBuilder(1600);
            sb.Append("Wojsko z ludzi (E4/8): wpiete: ").Append(_wired.Count > 0 ? string.Join(", ", _wired.ToArray()) : "-")
              .Append("; BRAK: ").Append(_missing.Count > 0 ? string.Join(", ", _missing.ToArray()) : "-")
              .Append("; wlasciciele latek celow: ").Append(Owners())
              .Append("; ksiega pochodzenia E4/7: ").Append(s != null && s.PeopleOriginEnabled ? "TAK" : "BRAK (People Origin Enabled)")
              .Append("; ustawienia startowe przed MCM (stan gry - linia dzienna).");
            return sb.ToString();
        }

        // ================================================================ linia doby (po "Pochodzenie (E4/7)" i "Ludzie (ksiega)")
        private static string F0(double v) { return v.ToString("0", Inv); }
        private static string F1(double v) { return v.ToString("0.#", Inv); }
        private static string TN(bool b) { return b ? "T" : "N"; }

        /// <summary>ArmouryBehavior.OnDailyTick, zaraz po OthersRise.Daily (tam PeopleBook.Daily zamyka dobe ksiegi i kopiuje ruch DOM przyczynami E4/8): linie
        /// "Wojsko z ludzi (E4/8)" i "Miasta (E4/8 R2)", zerowanie licznikow doby. Bez logu - tylko zerowanie.</summary>
        internal static void Daily()
        {
            var s = S;
            bool log = s != null && s.LogEnabled;
            int day = 0;
            try { day = (int)CampaignTime.Now.ToDays - 1; } catch { }
            var d = D;
            D = new Day();
            if (!log) { _stumblesDay = 0; _stumbleAt.Clear(); try { TownGrowth.Line(day, false); } catch (Exception e) { Stumble("Daily(Miasta)", e); } return; }
            long t0 = Stopwatch.GetTimestamp();
            if (!_startLogged) { _startLogged = true; try { Log.Info(StartLine()); } catch (Exception e) { Stumble("StartLine", e); } }
            try { Log.Info(Line(day, d, s, t0)); } catch (Exception e) { Stumble("Line", e); }
            try { Log.Info(RetinueLine(day, d, s)); } catch (Exception e) { Stumble("Line(295)", e); }
            try { TownGrowth.Line(day, true); } catch (Exception e) { Stumble("Daily(Miasta)", e); }
            _stumblesDay = 0; _stumbleAt.Clear();
        }

        private static string Line(int day, Day d, Settings s, long t0)
        {
            string book = !(s != null && s.PeopleOriginEnabled) ? "WYLACZONA" : (PeopleBook.LevyLive ? "CZYNNA" : (PeopleBook.Sown ? "BEZ POBORU (LevyLive)" : "BEZ SIEWU"));
            var sb = new StringBuilder(3600);
            sb.Append("Wojsko z ludzi (E4/8): dzien ").Append(day)
              .Append(" | wlaczone: zalogi ").Append(TN(GarrisonOn)).Append(", milicja ").Append(TN(MilitiaOn)).Append(", nowe druzyny ").Append(TN(SpawnOn))
              .Append(" (z zalogi ").Append(TN(SpawnGarrisonOn)).Append(", zboze ").Append(TN(GrainOn)).Append("), karawany ").Append(TN(CaravansOn)).Append(", wedrowcy BK ").Append(TN(TravellersOn))
              .Append("; ksiega pochodzenia: ").Append(book);
            // zalogi
            sb.Append(" | ZALOGI: przyrost z modelu zablokowany -").Append(d.zBlocked).Append(" w ").Append(d.zTowns).Append(" twierdzach (gorna granica - model bez limitow gry), ")
              .Append(WatchLaw.Live ? "Straz: przybysze z Westeros (E4/9) +" : "Straz zostawiona do E4/9 +")
              .Append(d.zWatch).Append(" w ").Append(d.zWatchTowns).Append(" twierdzach");
            sb.Append(" | A5: twierdz z werbunkiem ").Append(d.a5With).Append(" z ").Append(d.a5Towns).Append(" (ludzi ").Append(d.a5Men).Append("), limit dzienny ").Append(d.a5Daily)
              .Append(", bez ochotnikow ").Append(d.a5NoVol).Append(", zold albo miejsce ").Append(d.a5Wage)
              .Append("; K8: limit ").Append(K8Limit).Append("/d, dobrano ").Append(d.k8Extra).Append(" (ponad ").Append(d.k8Game).Append("/d, w ").Append(d.k8Towns)
              .Append(" twierdzach), odmowy: zold albo miejsce ").Append(d.k8Wage).Append(" / brak ochotnikow ").Append(d.k8NoVol).Append(" / hamulec ").Append(d.k8Brake)
              .Append(", poza K8 (Straz, Inni, bez poboru) ").Append(d.k8Out);
            // milicja
            long m1 = d.m1v + d.m1t + d.m1c;
            long refused = d.r1 + d.r2 + d.r3;
            string realms = d.r1Realm.Count > 0 ? string.Join(", ", d.r1Realm.OrderByDescending(kv => kv.Value).Take(8).Select(kv => kv.Key + " " + kv.Value).ToArray()) : "-";
            sb.Append(" | MILICJA: przyrost w oknach +").Append(m1 + d.m2).Append(" = z wolnych rak ").Append(m1).Append(" (z DOM ").Append(F1(d.m1)).Append(", ulamki bez DOM ").Append(F1(d.mFrac))
              .Append("; wsie ").Append(d.m1v).Append(", miasta ").Append(d.m1t).Append(", zamki ").Append(d.m1c).Append("; tick ").Append(d.m1k).Append(", odbudowa wsi ").Append(d.m1h)
              .Append(", obrona oblezenia ").Append(d.m1o).Append(") + Straz \"\" ").Append(d.m2)
              .Append("; odmowione -").Append(refused).Append(" (bez wolnych rak ").Append(d.r1).Append(" w ").Append(d.r1Reg.Count).Append(" regionach [krainy: ").Append(realms)
              .Append("], osady Innych ").Append(d.r2).Append(", podloga ").Append(d.r3).Append(")")
              .Append("; ubytek bez walki do domu -").Append(d.hAll).Append(" (do tej osady ").Append(F1(d.h1)).Append(", inne regiony ").Append(F1(d.h2)).Append(", bez pochodzenia ").Append(F1(d.h3)).Append(", na Mur (E4/9) ").Append(F1(d.h4)).Append(")")
              .Append("; z rozwiazanych partii we wsi +").Append(F1(d.j))
              .Append("; tick netto (po korekcie) ").Append(d.mTickUp - d.mTickDown >= 0 ? "+" : "").Append(d.mTickUp - d.mTickDown);
            // nowe druzyny
            long a3 = d.a3h + d.a3g + d.a3f;
            sb.Append(" | NOWE DRUZYNY: ").Append(d.p).Append(" partii (Straz ").Append(d.p1).Append(", Inni ").Append(d.p2).Append(", rod gracza ").Append(d.p3).Append(")")
              .Append(", szablon ").Append(d.n).Append(" = z wolnych rak ").Append(F1(d.a1)).Append(" + ulamki bez DOM ").Append(F1(d.aFrac)).Append(" + z zalogi siedziby ").Append(d.a2)
              .Append(" + nie wystawieni ").Append(a3).Append(" (bez wolnych rak ").Append(d.a3h).Append(", zaloga ponizej rezerwy albo w bitwie/oblezeniu ").Append(d.a3g).Append(", podloga ").Append(d.a3f).Append(")")
              .Append(" + Straz \"\" ").Append(d.a4).Append(" + ksiega bez poboru ").Append(d.nOff)
              .Append(" (159: bez sprzetu zostali we wsi, poza rachunkiem szablonu ").Append(d.kitCut).Append(")")
              .Append("; zboze ").Append(d.g).Append(" szt. (").Append(d.gN).Append(" partii): z zapasu siedziby ").Append(d.g1).Append(", z innej warowni ").Append(d.g2).Append(", z niczego (brak zapasu) ").Append(d.g3)
              .Append(" (zdjete m6b ").Append(d.g3x).Append(", zboze gry dla druzyn Innych ").Append(d.gOth).Append(")")
              .Append("; dary startowe gry (poza E4/8 - etap 6): ").Append(d.gifts).Append(" szt. za ").Append(d.giftsValue).Append(" zl");
            // karawany
            sb.Append(" | KARAWANY: ").Append(d.k).Append(" nowych (ludzi ").Append(d.kMen).Append("): z ludzi osady startu ").Append(F1(d.c1)).Append(" (miasta ").Append(F1(d.c1t)).Append(", zamki ").Append(F1(d.c1c))
              .Append(", wsie ").Append(F1(d.c1v)).Append("), nie wystawieni ").Append(d.c2).Append(", bez DOM (mistrz karawany, ulamki) ").Append(F1(d.c2Master + d.cFrac)).Append(", Straz \"\" ").Append(d.cWatch)
              .Append("; konwoje morskie uzupelnione ").Append(F1(d.c3)).Append(" (").Append(d.c3N).Append(" razy, nie wystawieni ").Append(d.c4).Append(")");
            // wedrowcy
            double rAll, rKnown; int rParties;
            PeopleBook.RoadNow(out rAll, out rKnown, out rParties);
            sb.Append(" | WEDROWCY BK (R19): wyjazdy ").Append(d.w).Append(" (konwoje ").Append(d.wCargo).Append("; ludzi: wedrowcy ").Append(F1(d.n1w)).Append(", eskorta konwojow ").Append(F1(d.n1e))
              .Append(", bez pochodzenia ").Append(F1(d.n3)).Append("), dojscia: do domu ").Append(d.b1N).Append(" (").Append(F1(d.b1)).Append(" ludzi), do innej osady regionu ").Append(d.b2N).Append(" (").Append(F1(d.b2))
              .Append("), migracja do innego regionu ").Append(d.b3N).Append(" (").Append(F1(d.b3)).Append("), zniszczeni w drodze do domu ").Append(d.b4N).Append(" (").Append(F1(d.b4))
              .Append("), eskorta konwojow do domu ").Append(d.b5N).Append(" (").Append(F1(d.b5)).Append("), u celu bez pochodzenia ").Append(F1(d.bNone))
              .Append("; niewolnicy BK w konwojach (E4/9) ").Append(d.s).Append(" (u celu ").Append(F1(d.sEnd)).Append(")")
              .Append("; w drodze teraz ").Append(F1(rAll)).Append(" w ").Append(rParties).Append(" partiach (z pochodzeniem ").Append(F1(rKnown)).Append(")");
            // R6
            string qk = d.qKind.Count > 0 ? string.Join(", ", d.qKind.OrderByDescending(kv => kv.Value).Select(kv => KindName(kv.Key) + " " + kv.Value).ToArray()) : "-";
            sb.Append(" | ROZWIAZANE BEZ BITWY (R6): partii ").Append(d.q).Append(" [").Append(qk).Append("; w tym usuniete bez zniszczenia - np. konwoje DTE ").Append(d.qRemoved).Append("], do domu ").Append(F1(d.qHere + d.qOther)).Append(" (inne regiony ").Append(F1(d.qOther))
              .Append("), bez pochodzenia ").Append(F1(d.qNone)).Append(", na Mur (E4/9) ").Append(F1(d.qWall))
              .Append(", do karczmy (E4/10 - jak rozwiazane partie) ").Append(F1(d.qVet));
            // kontrola kodu: ruch DOM przyczynami E4/8 w ksiedze (PeopleBook.ArmsDayMen) = to, co linia zdjela
            var bm = PeopleBook.ArmsDayMen;
            double bookOut = bm[PeopleBook.MLevyMilitia] + bm[PeopleBook.MLevySpawn] + bm[PeopleBook.MCaravanOut] + bm[PeopleBook.MCaravanRefill] + bm[PeopleBook.MRoadOut];
            double ours = d.m1 + d.a1 + d.c1 + d.c3 + d.n1w + d.n1e + d.ob + d.rt;   // m97-wioski krok 2: + ob (obroncy wiosek pobocznych); 295: + rt (orszaki dworow BK)
            sb.Append(" | KONTROLA: zdjete z DOM przyczynami E4/8 ").Append(F1(bookOut)).Append(" - (m1 + a1 + c1 + c3 + n1w + n1e + ob + rt) = ").Append((bookOut - ours).ToString("0.00", Inv)).Append(" (ob - obroncy wiosek pobocznych ").Append(F1(d.ob))
              .Append(", rt - orszaki dworow BK (295) ").Append(F1(d.rt)).Append(")");
            sb.Append(" | potkniecia ").Append(_stumblesDay);
            if (_stumbleAt.Count > 0) sb.Append(" (").Append(string.Join(", ", _stumbleAt.Select(kv => kv.Key + " " + kv.Value).ToArray())).Append(")");
            double ms = (d.ticks + (Stopwatch.GetTimestamp() - t0)) * 1000.0 / Stopwatch.Frequency;
            sb.Append(" | czas ").Append(ms.ToString("0.0", Inv)).Append(" ms.");
            return sb.ToString();
        }

        private static string KindName(int k)
        {
            switch (k)
            {
                case PeopleBook.KLord: return "partie rodow";
                case PeopleBook.KGarrison: return "zalogi";
                case PeopleBook.KMilitia: return "milicje";
                case PeopleBook.KCaravan: return "karawany";
                case PeopleBook.KPatrolBk: return "patrole BK";
                case PeopleBook.KPatrolGame: return "patrole gry";
                case PeopleBook.KWatch: return "Straz";
                default: return "inne";
            }
        }
    }
}
