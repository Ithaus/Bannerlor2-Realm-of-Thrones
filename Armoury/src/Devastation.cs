using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// SPUSTOSZENIE JAKO ULAMEK OKREGU (demografia krok 4 + 6a; docs/DEMOGRAFIA-SILA-ROBOCZA-2026-10-05.md rozdz. 4.3, 4.6, 5.2
    /// i docs/DEMOGRAFIA-ANEKS-2026-10-05.md rozdz. 1). Jeff 05.10: "spalenie wsi to jest tylko symbol - to nie jest jedna wies,
    /// tylko szereg wsi; jak bedzie kilka lat odbudowy, to wszystkie wioski beda spalone".
    ///
    /// Wies na mapie to okreg (srednio 74 tys. ludzi, ok. 160 prawdziwych wsi). Gra przy kazdym pelnym rabunku zdejmowala jej
    /// 39.7% hearth bez wzgledu na to, czy przyszlo 60 ludzi, czy 2000, a nasze zerowanie armii - hearth w skali gry
    /// (0.8 hearth na 500 ludzi dziennie = 355 ludzi ksiegi w Reach, 3 w Nocnej Strazy). Ludzie znikali z ksiegi i wracali
    /// z niczego (odrost gry, potem "+0.5 hearth ponizej 40"). Odtad:
    ///  - RABUNEK i ZEROWANIE pustosza tyle ludzi, ilu zbrojnych i ile czasu: `DevastationPerRaiderDay` (0.5) czlowieka na
    ///    zbrojnego na dobe lupienia, `DevastationPerForagerDay` (0.2) na dobe postoju wrogiej armii przy wsi. Okregu nie da sie
    ///    spustoszyc ponad `DevastationMaxPercent` (90%): trafieni = (pulap - juz spustoszeni) x (1 - e^(-osobodni x R / pulap));
    ///  - trafieni NIE znikaja: `DevastationKilledPercent` (5%) ginie (nie wiecej niz jeden na zbrojnego na dobe),
    ///    `DevastationOutlawPercent` (5%) idzie w las do puli wyrzutkow regionu (zamiast dawnych 3% hearth przy spaleniu),
    ///    reszta to UCHODZCY - konto wsi w ksiedze ludzi (zapis: arm_people, sekcja "u:"), chronia sie w warowni regionu i po
    ///    sasiednich wsiach;
    ///  - POWROT: co dobe wraca `RefugeeReturnPercent` (0.1%) uchodzcow x (1 - niebezpieczenstwo) - ta sama miara co w przyroscie
    ///    naturalnym (bezprawie warowni, wojna, spalone wsie regionu); x0.25, gdy warownia gloduje; nikt nie wraca do wsi spalonej,
    ///    lupionej, pod przymusem ani gdy warownia jest oblezona. Odrost wsi = powrot uchodzcow + przyrost naturalny (krok 3);
    ///    regula "+0.5 hearth ponizej 40" (ScorchedEarth.RefugeeReturn) przy czynnym spustoszeniu nie dziala;
    ///  - 6a, PLON: produkcja wsi (kazdy towar, wydobycie i zywnosc dla warowni) x (ludzie w domu / ludzie sprzed spustoszenia)
    ///    ^ `DevastationYieldElasticity` (0.5: -10% rak = -5% plonu) - MaterialLaw.ProdPostfix i FoodPostfix nizej. Progi produkcji
    ///    gry (200 / 600 hearth) licza sie od ludzi sprzed spustoszenia (LevelPostfix) - ubytek rak dziala tylko przez ciagly
    ///    mnoznik, wies nie spada o poziom (-50% plonu) od jednego rabunku.
    /// Lupiezca dostaje od gry zloto i towar jak dotad: w srodku kodu rabunku wies ma hearth, jaki mialaby w grze (RaidState.Virt),
    /// a poza nim prawdziwy - to samo widza cudze nasluchy konca rabunku (nekromancja ROT, laski BKROT, jency BK).
    /// Wymaga jednostki ludzi (PeopleUnit, krok 2) i przyrostu naturalnego (krok 3); bez nich albo przy wylaczonym
    /// `DevastationEnabled` wszystko jest jak po kroku 3, a uchodzcy wracaja do wsi od razu (Abandon).
    /// </summary>
    internal static class Devastation
    {
        /// <summary>Gra i BK podnosza wies do 10 hearth z niczego (Village.DailyTick, prefiks BK) - spustoszenie tam nie schodzi.</summary>
        internal const float FloorHearth = 11f;
        // krok rabunku gry: co 0.05 punktow osady wies traci 0.5 x krok x hearth (RaidEventComponent.Update)
        private const float GameStep = 0.05f, GameStepKeep = 0.975f;
        private const int KRaid = 0, KForage = 1;
        private const int Top = 8;

        private static bool _raidPatched, _endPatched, _levelPatched, _foodPatched;

        internal static bool On
        {
            get
            {
                var s = Settings.Current;
                return s != null && s.DevastationEnabled && _raidPatched && _endPatched && PeopleUnit.On && PopulationLaw.GrowthOn;
            }
        }

        // ------------------------------------------------------------ stan (zapis: arm_people, sekcja "u:")
        private sealed class Scar { public double Away, Dead; }      // uchodzcy poza domem; zabici przy spustoszeniu od poczatku kampanii
        private static readonly object _lock = new object();
        private static readonly Dictionary<string, Scar> _scars = new Dictionary<string, Scar>();      // id osady wsi -> konto
        private static volatile int _awayCount;                     // wsie z uchodzcami poza domem (0 = szybka sciezka latek)
        private static Dictionary<string, Village> _byId;           // id osady -> wies (raz na kampanie)

        // rabunek w toku (bez zapisu: po wczytaniu w polowie rabunku hearth gry odtwarzamy z RaidDamage)
        private sealed class RaidState
        {
            public double PersonDays;       // osobodni lupienia od ostatniego kroku gry
            public double TotalDays, Hit;   // osobodni i trafieni tego rabunku razem
            public float Virt; public bool HasVirt;     // hearth, jaki wies mialaby w grze po dotychczasowych krokach rabunku
            public int Steps, MaxMen;
        }
        private static readonly Dictionary<RaidEventComponent, RaidState> _raids = new Dictionary<RaidEventComponent, RaidState>();

        /// <summary>Stan wsi miedzy prefiksem a finalizerem latek rabunku.</summary>
        internal struct Tick { public Village V; public float Real, Virt; public bool Swapped; public object Rs; }

        // liczniki doby
        private static readonly double[] _dHit = new double[2], _dDays = new double[2];
        private static readonly int[] _dFull = new int[2];
        private static readonly HashSet<string>[] _dVillages = { new HashSet<string>(), new HashSet<string>() };
        private static double _dDead, _dFled, _dAway, _dBack, _dGameHearth, _dGamePeople, _dRateSum;
        private static int _dBackVillages, _dRaids, _dRaidsFull, _dRaidMen, _dResumed, _dForagers, _dNoK, _dStopState, _dStopSiege, _dStopOther, _dRated, _dForeign;
        private static int _stumbles;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static bool _abandonLogged;

        internal static void Reset()
        {
            lock (_lock) { _scars.Clear(); _awayCount = 0; _raids.Clear(); _byId = null; }
            _errSites.Clear(); _abandonLogged = false;
            NewDay();
        }

        internal static void NewDay()
        {
            Array.Clear(_dHit, 0, 2); Array.Clear(_dDays, 0, 2); Array.Clear(_dFull, 0, 2);
            _dVillages[0].Clear(); _dVillages[1].Clear();
            _dDead = _dFled = _dAway = _dBack = _dGameHearth = _dGamePeople = _dRateSum = 0.0;
            _dBackVillages = _dRaids = _dRaidsFull = _dRaidMen = _dResumed = _dForagers = _dNoK = _dStopState = _dStopSiege = _dStopOther = _dRated = _dForeign = 0;
            _stumbles = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;                                    // liczymy potkniecia, niczego nie gasimy
            bool first;
            lock (_errSites) first = _errSites.Add(where);
            if (first) Log.Error(where, e);                 // pierwszy wyjatek kazdego miejsca do logu, kolejne tylko w liczniku doby
        }

        // ------------------------------------------------------------ odczyty
        private static string IdOf(Village v) { return v != null && v.Settlement != null ? v.Settlement.StringId : null; }

        /// <summary>Uchodzcy tej wsi poza domem (ludzie).</summary>
        internal static double AwayOf(Village v)
        {
            if (_awayCount == 0) return 0.0;
            string id = IdOf(v);
            if (id == null) return 0.0;
            lock (_lock) { Scar sc; return _scars.TryGetValue(id, out sc) ? sc.Away : 0.0; }
        }

        private static double MaxShare(Settings s) { return Math.Max(0, Math.Min(95, s.DevastationMaxPercent)) / 100.0; }

        /// <summary>
        /// Ludzi na punkt hearth tej wsi do rachunku spustoszenia: k jej krainy, a dla kultury spoza tabeli ludnosci srednia
        /// swiata (ok. 191; liczy sie raz i idzie do zapisu z kalibracja pod kluczem "*"). 0 = przelicznika nie ma wcale
        /// (ludnosc nieskalibrowana albo Population Scale 0).
        /// </summary>
        private static float KOf(Village v)
        {
            float k = PopulationLaw.PeoplePerHearthOrWorld(v);
            if (k > 0f || !PopulationLaw.Calibrated) return k;
            PopulationLaw.WorldPeoplePerHearth();
            return PopulationLaw.PeoplePerHearthOrWorld(v);
        }

        /// <summary>
        /// Czy spustoszenie obejmuje te wies: jest czynne i wies ma przelicznik ludzi. Wies bez przelicznika zostaje przy regulach
        /// sprzed kroku 4 (rabunek gry, zerowanie w hearth, +0.5 ponizej progu) - ta sama zasada co w PeopleUnit: bez przelicznika
        /// liczymy dokladnie po staremu. W ROT przelicznik ma kazda z 571 wsi.
        /// </summary>
        internal static bool Covers(Village v)
        {
            if (v == null || !On) return false;
            try { return KOf(v) > 0f; }
            catch (Exception e) { Stumble("Devastation.Covers", e); return false; }
        }

        /// <summary>
        /// Mnoznik plonu wsi od ubytku rak przez spustoszenie (6a): (ludzie w domu / ludzie sprzed spustoszenia) ^ elastycznosc.
        /// Dokladnie 1, gdy spustoszenie jest wylaczone albo wies nie ma uchodzcow poza domem.
        /// </summary>
        internal static float YieldFactor(Village v)
        {
            if (_awayCount == 0 || v == null) return 1f;
            try
            {
                if (!On) return 1f;
                double away = AwayOf(v);
                if (!(away > 0.0)) return 1f;
                float k = PopulationLaw.PeoplePerHearthOrWorld(v);
                if (!(k > 0f)) return 1f;
                double home = (double)Math.Max(0f, v.Hearth) * k;
                var s = Settings.Current;
                double e = Math.Max(0f, Math.Min(2f, s.DevastationYieldElasticity));
                double m = Math.Pow(home / (home + away), e);
                if (double.IsNaN(m) || double.IsInfinity(m)) return 1f;
                // dno z pulapu spustoszenia: okreg spustoszony do pulapu daje (1 - pulap) ^ e; nizej nie schodzimy nawet po zmianie pulapu w MCM
                double lo = Math.Pow(Math.Max(0.05, 1.0 - MaxShare(s)), e);
                if (m < lo) m = lo;
                return m >= 1.0 ? 1f : (float)m;
            }
            catch (Exception e) { Stumble("Devastation.YieldFactor", e); return 1f; }
        }

        /// <summary>Ilu ludzi tej wsi mozna jeszcze spustoszyc (do pulapu okregu i nie ponizej dna hearth); 0 = okreg nasycony.</summary>
        private static double Room(Village v, Settings s, out float k, out double away, out double reach)
        {
            k = 0f; away = 0.0; reach = 0.0;
            if (v == null || v.Settlement == null) return 0.0;
            float hearth = v.Hearth;
            if (!(hearth > 0f) || float.IsInfinity(hearth)) return 0.0;      // takze NaN
            k = KOf(v);
            if (!(k > 0f)) return 0.0;
            away = AwayOf(v);
            double home = (double)hearth * k;
            reach = MaxShare(s) * (home + away);                             // tylu ludzi okregu jest w zasiegu napastnika
            double open = reach - away;
            double floor = ((double)hearth - FloorHearth) * k;
            if (open > floor) open = floor;
            return open > 0.0 ? open : 0.0;
        }

        /// <summary>Czy marsz armii ma tu jeszcze co pustoszyc (ScorchedEarth: wies nasycona nie zywi wojska - jak dawna podloga hearth).</summary>
        internal static bool Open(Village v)
        {
            try
            {
                var s = Settings.Current;
                if (s == null) return false;
                PopulationLaw.EnsureCalibrated();
                float k; double away, reach;
                return Room(v, s, out k, out away, out reach) > 0.0;
            }
            catch (Exception e) { Stumble("Devastation.Open", e); return false; }
        }

        // ------------------------------------------------------------ spustoszenie
        /// <summary>
        /// `personDays` osobodni napastnika przy tej wsi, `perDay` ludzi okregu na zbrojnego na dobe. Zdejmuje trafionych z hearth
        /// wsi (1/k za czlowieka) i dzieli ich: zabici (ubytek trwaly), wyrzutki (pula regionu), uchodzcy (konto wsi).
        /// Zwraca liczbe trafionych; trafieni = zabici + wyrzutki + uchodzcy co do ulamka czlowieka.
        /// </summary>
        private static double Strike(Village v, double personDays, double perDay, int kind)
        {
            var s = Settings.Current;
            if (s == null || v == null || !(personDays > 0.0) || !(perDay > 0.0)) return 0.0;
            PopulationLaw.EnsureCalibrated();        // k z hearth sprzed pierwszego zdjecia (jak PeopleUnit)
            float k; double away, reach;
            double open = Room(v, s, out k, out away, out reach);
            if (!(k > 0f)) { _dNoK++; return 0.0; }
            if (!(open > 0.0)) { _dFull[kind]++; return 0.0; }
            // napastnik przeczesuje okreg na oslep: czesc tego, co pali, jest juz spalona - stad wykladnik, nie iloczyn
            double hit = (reach - away) * (1.0 - Math.Exp(-perDay * personDays / reach));
            if (hit > open) hit = open;
            if (!(hit > 0.0) || double.IsNaN(hit)) return 0.0;
            double killShare = Math.Max(0, Math.Min(100, s.DevastationKilledPercent)) / 100.0;
            double fleeShare = OutlawLaw.On ? Math.Max(0, Math.Min(100, s.DevastationOutlawPercent)) / 100.0 : 0.0;     // bez prawa wyrzutkow nie ma puli - zostaja uchodzcami
            if (killShare + fleeShare > 1.0) fleeShare = 1.0 - killShare;
            double dead = Math.Min(hit * killShare, personDays);        // [KRYT 8] nie wiecej zabitych niz jeden na zbrojnego na dobe
            double fled = hit * fleeShare;
            double refugees = hit - dead - fled;
            string id = IdOf(v);

            PeopleUnit.Shift(v, -hit / k);
            lock (_lock)
            {
                Scar sc;
                if (!_scars.TryGetValue(id, out sc)) _scars[id] = sc = new Scar();
                bool had = sc.Away > 0.0;
                sc.Away += refugees; sc.Dead += dead;
                if (!had && sc.Away > 0.0) _awayCount++;
            }
            if (fled > 0.0)
            {
                OutlawLaw.AddFled(v.Settlement, (float)fled);
                PeopleUnit.NoteOut((float)fled, (float)(fled / k));
            }
            PeopleUnit.NoteRuin(dead + refugees, (dead + refugees) / k);
            _dHit[kind] += hit; _dDead += dead; _dFled += fled; _dAway += refugees;
            _dVillages[kind].Add(id);
            return hit;
        }

        /// <summary>Wroga armia zywi sie z okregu (ScorchedEarth.OnDaily): doba postoju `men` ludzi przy wsi.</summary>
        internal static void Forage(Village v, int men)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || v == null || men <= 0) return;
                _dForagers++; _dDays[KForage] += men;
                Strike(v, men, Math.Max(0f, s.DevastationPerForagerDay), KForage);
            }
            catch (Exception e) { Stumble("Devastation.Forage", e); }
        }

        // ------------------------------------------------------------ rabunek: RaidEventComponent.Update(ref bool finish)
        private static double TickDays()
        {
            // czas tego tykniecia mapy - ten sam, z ktorego gra liczy postep rabunku (DefaultRaidModel.CalculateHitDamage)
            try { double d = CampaignTime.DeltaTime.ToDays; return d > 0.0 && d < 1.0 ? d : 0.0; }
            catch (Exception e) { Stumble("Devastation.TickDays", e); return 0.0; }
        }

        /// <summary>
        /// Stan rabunku; nowy wpis po wczytaniu gry w polowie rabunku odtwarza hearth gry z RaidDamage (kroki po 0.05, kazdy
        /// x0.975) - przyblizenie dotyczy tylko zlota i towaru lupiezcy z pozostalych krokow, ksiegi ludzi nie dotyka.
        /// </summary>
        private static RaidState StateOf(RaidEventComponent raid, Village v, bool create)
        {
            lock (_lock)
            {
                RaidState rs;
                if (_raids.TryGetValue(raid, out rs) || !create) return rs;
                rs = new RaidState();
                float done = raid.RaidDamage;
                if (done > 0f && v.Hearth > 0f)
                {
                    rs.Virt = (float)(v.Hearth * Math.Pow(GameStepKeep, done / GameStep)); rs.HasVirt = true;
                    _dResumed++;
                }
                _raids[raid] = rs;
                return rs;
            }
        }

        public static void RaidPrefix(RaidEventComponent __instance, out Tick __state)
        {
            __state = default(Tick);
            if (__instance == null || !On) return;
            try
            {
                var st = __instance.MapEventSettlement;
                var v = st != null ? st.Village : null;
                if (v == null) return;
                // nowa kampania, pierwsza doba: ludnosc kalibruje sie dopiero przy pierwszych rentach - rabunek, ktory ruszyl wczesniej,
                // szedlby po staremu (-39.7% hearth w nicosc, a k liczone potem z hearth juz zdjetego). Jak PeopleUnit: k sprzed zdjecia
                PopulationLaw.EnsureCalibrated();
                if (!Covers(v)) return;                       // wies bez przelicznika ludzi: rabunek gry bez zmian
                var rs = StateOf(__instance, v, true);
                var def = __instance.DefenderSide; var att = __instance.AttackerSide;
                if (def != null && att != null && def.TroopCount == 0)        // faza lupienia - ten sam warunek co w grze
                {
                    int men = att.TroopCount;
                    if (men > 0)
                    {
                        rs.PersonDays += men * TickDays();
                        if (men > rs.MaxMen) rs.MaxMen = men;
                    }
                }
                float real = v.Hearth;
                __state.Rs = rs; __state.Real = real; __state.Virt = rs.HasVirt ? rs.Virt : real; __state.V = v;
                // gra liczy strate, zloto i towar lupiezcy od hearth, jaki wies mialaby u niej po poprzednich krokach
                if (rs.HasVirt) { v.Hearth = rs.Virt; __state.Swapped = true; }
            }
            catch (Exception e) { __state = default(Tick); Stumble("Devastation.RaidPrefix", e); }
        }

        public static Exception RaidFinalizer(Tick __state, Exception __exception)
        {
            var v = __state.V;
            if (v == null) return __exception;
            try
            {
                float after = v.Hearth;                 // hearth gry po kroku (albo bez zmiany - wiekszosc tykniec)
                float took = __state.Virt - after;
                float real = __state.Real;
                // hearth URASTAJACY w srodku kroku gry to nie rabunek (nie powinno sie zdarzyc) - nie gubimy go
                if (took < 0f) { real -= took; _dForeign++; }
                v.Hearth = real;                        // prawdziwy hearth wsi z powrotem, co do bitu
                if (took > 0f)
                {
                    var rs = (RaidState)__state.Rs;
                    rs.Virt = after; rs.HasVirt = true; rs.Steps++;
                    _dGameHearth += took; _dGamePeople += (double)took * PopulationLaw.PeoplePerHearthOrWorld(v);
                    Settle(v, rs);
                }
            }
            catch (Exception e) { Stumble("Devastation.RaidFinalizer", e); }
            return __exception;
        }

        /// <summary>Rozlicza osobodni lupienia zebrane od ostatniego kroku.</summary>
        private static void Settle(Village v, RaidState rs)
        {
            double pd = rs.PersonDays;
            if (!(pd > 0.0)) return;
            rs.PersonDays = 0.0; rs.TotalDays += pd; _dDays[KRaid] += pd;
            rs.Hit += Strike(v, pd, Math.Max(0f, Settings.Current.DevastationPerRaiderDay), KRaid);
        }

        // ------------------------------------------------------------ koniec rabunku: RaidEventComponent.OnBeforeFinalize()
        // Gra ustawia tu stan wsi (spalona albo z powrotem zwykla) i rozsyla RaidCompleted. Cudze nasluchy czytaja wtedy hearth
        // wsi (ROT: upiory = hearth / 2; BKROT: laska = hearth; BK: jency, gdy brak danych ludnosci) - widza hearth gry jak dotad.
        public static void EndPrefix(RaidEventComponent __instance, out Tick __state)
        {
            __state = default(Tick);
            if (__instance == null) return;
            try
            {
                var st = __instance.MapEventSettlement;
                var v = st != null ? st.Village : null;
                if (v == null) return;
                bool on = Covers(v);
                var rs = StateOf(__instance, v, on);
                if (rs == null) return;
                float real = v.Hearth;
                __state.Rs = rs; __state.Real = real; __state.Virt = on && rs.HasVirt ? rs.Virt : real; __state.V = v;
                if (on && rs.HasVirt) { v.Hearth = rs.Virt; __state.Swapped = true; }
            }
            catch (Exception e) { __state = default(Tick); Stumble("Devastation.EndPrefix", e); }
        }

        public static Exception EndFinalizer(RaidEventComponent __instance, Tick __state, Exception __exception)
        {
            try
            {
                var v = __state.V;
                if (v != null)
                {
                    if (__state.Swapped)
                    {
                        // gra w tej metodzie hearth nie rusza; gdyby ruszyl go cudzy nasluch, zmiana zostaje przy wsi
                        float moved = v.Hearth - __state.Virt;
                        if (moved != 0f) _dForeign++;
                        v.Hearth = __state.Real + moved;
                    }
                    var rs = (RaidState)__state.Rs;
                    if (On)
                    {
                        Settle(v, rs);        // czas lupienia po ostatnim kroku gry (takze rabunek przerwany przed pierwszym krokiem)
                        if (rs.TotalDays > 0.0)
                        {
                            _dRaids++; _dRaidMen += rs.MaxMen;
                            if (v.VillageState == Village.VillageStates.Looted) _dRaidsFull++;
                        }
                    }
                }
            }
            catch (Exception e) { Stumble("Devastation.EndFinalizer", e); }
            finally
            {
                if (__instance != null) lock (_lock) _raids.Remove(__instance);
            }
            return __exception;
        }

        // ------------------------------------------------------------ 6a: progi produkcji i zywnosc wsi
        /// <summary>
        /// Village.GetHearthLevel(): progi 200 / 600 hearth licza sie od ludzi SPRZED spustoszenia (hearth + uchodzcy / k). Czytaja
        /// je model produkcji i zywnosci wsi, limit lodzi rybackich NavalDLC, Village.DailyTick i model gry zywnosci warowni
        /// (w zestawie modow rejestruja wlasne BK i ROT; BKFoodModel hearth wsi nie czyta). Poziom tylko podnosimy.
        /// </summary>
        public static void LevelPostfix(Village __instance, ref int __result)
        {
            if (_awayCount == 0 || __result >= 2 || __instance == null) return;
            try
            {
                if (!On) return;
                double away = AwayOf(__instance);
                if (!(away > 0.0)) return;
                float k = PopulationLaw.PeoplePerHearthOrWorld(__instance);
                if (!(k > 0f)) return;
                double before = __instance.Hearth + away / k;
                int level = before >= 600.0 ? 2 : (before >= 200.0 ? 1 : 0);
                if (level > __result) __result = level;
            }
            catch (Exception e) { Stumble("Devastation.LevelPostfix", e); }
        }

        /// <summary>DefaultVillageProductionCalculatorModel.CalculateDailyFoodProductionAmount: zywnosc wsi x mnoznik rak.</summary>
        public static void FoodPostfix(Village __0, ref float __result)
        {
            if (_awayCount == 0 || !(__result > 0f)) return;
            float m = YieldFactor(__0);
            if (m != 1f) __result *= m;
        }

        // ------------------------------------------------------------ doba: powroty
        private static Dictionary<string, Village> ById()
        {
            var map = _byId;
            if (map != null) return map;
            map = new Dictionary<string, Village>();
            foreach (var st in Settlement.All)
                if (st != null && st.IsVillage && st.Village != null && st.StringId != null) map[st.StringId] = st.Village;
            _byId = map;
            return map;
        }

        /// <summary>
        /// Jaka czesc uchodzcow tej wsi wraca dzis. 0: wies spalona, lupiona albo pod przymusem (`why` 1), warownia oblezona (2).
        /// Inaczej RefugeeReturnPercent x (1 - niebezpieczenstwo), x0.25 przy glodujacej warowni (jak powrot wyrzutkow w OutlawLaw).
        /// </summary>
        private static double ReturnRate(Village v, Settings s, out int why)
        {
            why = 0;
            if (v.VillageState != Village.VillageStates.Normal) { why = 1; return 0.0; }
            Settlement node = v.Bound;
            if (node != null && node.IsUnderSiege) { why = 2; return 0.0; }
            float security, burnt; bool war;
            float danger = PopulationLaw.Danger(v, out security, out war, out burnt);
            double rate = Math.Max(0f, s.RefugeeReturnPercent) / 100.0 * (1.0 - danger);
            if (node != null && node.IsStarving) rate *= 0.25;
            if (double.IsNaN(rate) || rate < 0.0) rate = 0.0;
            if (rate <= 0.0) why = 3;
            return rate > 1.0 ? 1.0 : rate;
        }

        /// <summary>Raz na dobe (ArmouryBehavior.OnDailyTick, po ScorchedEarth): uchodzcy wracaja do swoich wsi.</summary>
        internal static void Daily()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || Campaign.Current == null) return;
                if (!On) { Abandon(); return; }
                _abandonLogged = false;
                if (_awayCount == 0) return;
                List<string> ids;
                lock (_lock) ids = _scars.Where(kv => kv.Value.Away > 0.0).Select(kv => kv.Key).ToList();
                var map = ById();
                foreach (var id in ids)
                {
                    try
                    {
                        Village v;
                        if (!map.TryGetValue(id, out v) || v == null) continue;
                        int why;
                        double rate = ReturnRate(v, s, out why);
                        if (!(rate > 0.0))
                        {
                            if (why == 1) _dStopState++; else if (why == 2) _dStopSiege++; else _dStopOther++;
                            continue;
                        }
                        float k = PopulationLaw.PeoplePerHearthOrWorld(v);
                        if (!(k > 0f)) { _dNoK++; continue; }
                        double back;
                        lock (_lock)
                        {
                            Scar sc;
                            if (!_scars.TryGetValue(id, out sc) || !(sc.Away > 0.0)) continue;
                            back = sc.Away * rate;
                            if (sc.Away - back < 1.0) back = sc.Away;        // ostatni czlowiek wraca caly
                            sc.Away -= back;
                            if (!(sc.Away > 0.0)) { sc.Away = 0.0; _awayCount--; }
                        }
                        PeopleUnit.Shift(v, back / k);
                        PeopleUnit.NoteHome(back, back / k);
                        _dBack += back; _dBackVillages++; _dRateSum += rate; _dRated++;
                    }
                    catch (Exception e) { Stumble("Devastation.Daily(wies)", e); }
                }
            }
            catch (Exception e) { Stumble("Devastation.Daily", e); }
        }

        /// <summary>
        /// Spustoszenie wylaczone (MCM albo brak jednostki ludzi / przyrostu naturalnego), a w ksiedze sa uchodzcy: wracaja do
        /// swoich wsi od razu - ludzie nie moga utknac na koncie, ktorego nikt nie obsluguje. Liczniki zabitych zostaja.
        /// </summary>
        private static void Abandon()
        {
            if (_awayCount == 0) return;
            List<string> ids;
            lock (_lock) ids = _scars.Where(kv => kv.Value.Away > 0.0).Select(kv => kv.Key).ToList();
            var map = ById();
            double people = 0.0, hearth = 0.0; int villages = 0, left = 0;
            foreach (var id in ids)
            {
                try
                {
                    Village v;
                    float k = map.TryGetValue(id, out v) && v != null ? PopulationLaw.PeoplePerHearthOrWorld(v) : 0f;
                    if (!(k > 0f)) { left++; continue; }
                    double back;
                    lock (_lock)
                    {
                        Scar sc;
                        if (!_scars.TryGetValue(id, out sc) || !(sc.Away > 0.0)) continue;
                        back = sc.Away; sc.Away = 0.0; _awayCount--;
                    }
                    PeopleUnit.Shift(v, back / k);
                    PeopleUnit.NoteHome(back, back / k);
                    people += back; hearth += back / k; villages++;
                }
                catch (Exception e) { left++; Stumble("Devastation.Abandon", e); }
            }
            if (villages > 0 || (left > 0 && !_abandonLogged))
            {
                _abandonLogged = true;
                var s = Settings.Current;
                Log.Info("Spustoszenie: WYLACZONE (" + (s != null && !s.DevastationEnabled ? "Devastation Enabled w MCM" : "wymaga People Unit Enabled i Natural Growth Enabled oraz wpietych latek rabunku")
                         + ") - " + F0(people) + " uchodzcow z " + villages + " wsi wrocilo do domu od razu (+" + F(hearth, "0.###") + " hearth)"
                         + (left > 0 ? "; " + left + " wsi bez przelicznika ludzi zostaje na koncie" : "")
                         + ". Odtad jak po kroku 3: rabunek gry -39.7% hearth, zerowanie armii w hearth, +0.5 hearth dziennie ponizej progu.");
            }
        }

        // ------------------------------------------------------------ zapis
        /// <summary>Sekcje "|u:idWsi=uchodzcy:zabici" do klucza arm_people (PopulationLaw.ExportPeople); puste, gdy nie ma kont.</summary>
        internal static string ExportSections()
        {
            var sb = new StringBuilder();
            lock (_lock)
                foreach (var kv in _scars)
                {
                    if (!(kv.Value.Away > 0.0) && !(kv.Value.Dead > 0.0)) continue;
                    if (kv.Key.IndexOf('|') >= 0 || kv.Key.IndexOf('=') >= 0) continue;
                    sb.Append("|u:").Append(kv.Key).Append('=').Append(kv.Value.Away.ToString("R", CultureInfo.InvariantCulture))
                      .Append(':').Append(kv.Value.Dead.ToString("R", CultureInfo.InvariantCulture));
                }
            return sb.ToString();
        }

        /// <summary>Odczyt sekcji "u:" z arm_people; zapis sprzed tej wersji (albo bez sekcji) = brak uchodzcow.</summary>
        internal static void Import(string data)
        {
            try
            {
                lock (_lock) { _scars.Clear(); _awayCount = 0; _raids.Clear(); }
                if (string.IsNullOrEmpty(data)) return;
                foreach (var part in data.Split('|'))
                {
                    if (!part.StartsWith("u:", StringComparison.Ordinal)) continue;
                    int eq = part.LastIndexOf('=');
                    if (eq <= 2) continue;
                    var nums = part.Substring(eq + 1).Split(':');
                    double away, dead = 0.0;
                    if (nums.Length < 1 || !double.TryParse(nums[0], NumberStyles.Float, CultureInfo.InvariantCulture, out away)) continue;
                    if (nums.Length > 1) double.TryParse(nums[1], NumberStyles.Float, CultureInfo.InvariantCulture, out dead);
                    if (double.IsNaN(away) || double.IsInfinity(away) || away < 0.0) away = 0.0;
                    if (double.IsNaN(dead) || double.IsInfinity(dead) || dead < 0.0) dead = 0.0;
                    if (!(away > 0.0) && !(dead > 0.0)) continue;
                    string id = part.Substring(2, eq - 2);
                    lock (_lock)
                    {
                        Scar old;
                        if (_scars.TryGetValue(id, out old) && old.Away > 0.0) _awayCount--;      // ta sama wies dwa razy w zapisie - liczy sie ostatni wpis
                        _scars[id] = new Scar { Away = away, Dead = dead };
                        if (away > 0.0) _awayCount++;
                    }
                }
            }
            catch (Exception e) { Log.Error("Devastation.Import", e); }
        }

        // ------------------------------------------------------------ log (ksiega "Ludzie:")
        private static string F(double v, string fmt) { return v.ToString(fmt, CultureInfo.InvariantCulture); }
        private static string F0(double v) { return Math.Round(v).ToString("0", CultureInfo.InvariantCulture); }
        private static string NameOf(Settlement st)
        {
            if (st == null) return "?";
            try { var n = st.Name; if (n != null) return n.ToString(); } catch { }
            return st.StringId ?? "?";
        }

        /// <summary>Uchodzcy poza domem na calym swiecie (ludzie) - ksiega dolicza ich do ludnosci.</summary>
        internal static double AwayWorld()
        {
            if (_awayCount == 0) return 0.0;
            lock (_lock) { double sum = 0.0; foreach (var sc in _scars.Values) sum += sc.Away; return sum; }
        }

        /// <summary>Konto regionu (warownia + jej wsie): uchodzcy poza domem i zabici przy spustoszeniu od poczatku kampanii.</summary>
        internal static void RegionOf(Settlement node, out double away, out double dead)
        {
            away = 0.0; dead = 0.0;
            if (node == null || node.BoundVillages == null) return;
            lock (_lock)
            {
                if (_scars.Count == 0) return;
                foreach (var v in node.BoundVillages)
                {
                    string id = IdOf(v); Scar sc;
                    if (id != null && _scars.TryGetValue(id, out sc)) { away += sc.Away; dead += sc.Dead; }
                }
            }
        }

        /// <summary>Dopisek do linii "Ludzie:" (PeopleLedger): pozycje spustoszenia tej doby i stan konta uchodzcow.</summary>
        internal static string DayNote()
        {
            var s = Settings.Current;
            if (s == null) return "";
            double away = 0.0, dead = 0.0; int villages = 0;
            lock (_lock) foreach (var sc in _scars.Values) { away += sc.Away; dead += sc.Dead; if (sc.Away > 0.0) villages++; }
            if (!On)
            {
                // wylaczone: jedna krotka pozycja, zeby bylo widac, wedle ktorej reguly wsie traca ludzi
                return " | spustoszenie WYLACZONE (" + (!s.DevastationEnabled ? "Devastation Enabled w MCM" : "brak jednostki ludzi, przyrostu naturalnego albo latek rabunku")
                       + ") - rabunek i zerowanie zdejmuja hearth jak po kroku 3" + (away > 0.0 ? "; na koncie jeszcze " + F0(away) + " uchodzcow (wroca przy najblizszej dobie)" : "");
            }
            double hit = _dHit[KRaid] + _dHit[KForage];
            var sb = new StringBuilder(" | spustoszenie dzis: zdjeci z wsi ");
            sb.Append(F(hit, "0.#")).Append(" ludzi (rabunki ").Append(F(_dHit[KRaid], "0.#")).Append(" w ").Append(_dVillages[KRaid].Count).Append(" wsiach, zerowanie armii ")
              .Append(F(_dHit[KForage], "0.#")).Append(" w ").Append(_dVillages[KForage].Count).Append(" wsiach) = zabici ").Append(F(_dDead, "0.#"))
              .Append(" + w las do puli wyrzutkow ").Append(F(_dFled, "0.#")).Append(" + uchodzcy ").Append(F(_dAway, "0.#"))
              .Append("; wrocilo do domu ").Append(F(_dBack, "0.#")).Append(" z ").Append(_dBackVillages).Append(" wsi")
              .Append("; uchodzcy w drodze (poza domem) ").Append(F0(away)).Append(" z ").Append(villages).Append(" wsi, zabici przy spustoszeniu od poczatku ").Append(F0(dead));
            if (_stumbles > 0) sb.Append("; potkniecia: ").Append(_stumbles);
            return sb.ToString();
        }

        private sealed class RegionRow { public Settlement Node; public double Away, Dead, Home; public float Worst = 1f; }

        /// <summary>
        /// Linia "Ludzie (spustoszenie):" - pomiar doby (rabunki, osobodni, co zdjelaby gra, powroty) i regiony najbardziej
        /// spustoszone. Pisana, gdy mechanika jest czynna i cokolwiek sie dzialo albo ktos jest poza domem.
        /// </summary>
        internal static void RegionsNote(int day)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !On) return;
                bool any = _dRaids > 0 || _dForagers > 0 || _dHit[KRaid] > 0.0 || _dHit[KForage] > 0.0 || _dBack > 0.0 || _awayCount > 0;
                if (!any) return;
                var map = ById();
                var rows = new Dictionary<Settlement, RegionRow>();
                double awayAll = 0.0; float worst = 1f; string worstName = "";
                List<KeyValuePair<string, double[]>> scars;
                lock (_lock) scars = _scars.Select(kv => new KeyValuePair<string, double[]>(kv.Key, new[] { kv.Value.Away, kv.Value.Dead })).ToList();
                foreach (var kv in scars)
                {
                    Village v;
                    if (!map.TryGetValue(kv.Key, out v) || v == null) continue;
                    var node = v.Bound ?? v.Settlement;
                    RegionRow r;
                    if (!rows.TryGetValue(node, out r)) rows[node] = r = new RegionRow { Node = node };
                    r.Away += kv.Value[0]; r.Dead += kv.Value[1]; awayAll += kv.Value[0];
                    float m = YieldFactor(v);
                    if (m < r.Worst) r.Worst = m;
                    if (m < worst) { worst = m; worstName = NameOf(v.Settlement); }
                }
                foreach (var r in rows.Values)
                    if (r.Node.BoundVillages != null)
                        foreach (var v in r.Node.BoundVillages)
                            if (v != null) r.Home += (double)Math.Max(0f, v.Hearth) * PopulationLaw.PeoplePerHearthOrWorld(v);
                var top = rows.Values.Where(r => r.Away > 0.0).OrderByDescending(r => r.Away / Math.Max(1.0, r.Home + r.Away)).Take(Top).ToList();

                var sb = new StringBuilder("Ludzie (spustoszenie): dzien ").Append(day);
                sb.Append(" | rabunki zakonczone ").Append(_dRaids).Append(" (wies spalona ").Append(_dRaidsFull).Append(", przerwane ").Append(_dRaids - _dRaidsFull).Append(')');
                if (_dRaids > 0) sb.Append(", napastnikow srednio ").Append(F((double)_dRaidMen / _dRaids, "0"));
                sb.Append("; osobodni lupienia ").Append(F(_dDays[KRaid], "0.#")).Append(" x ").Append(F(Math.Max(0f, s.DevastationPerRaiderDay), "0.##"))
                  .Append(" -> ").Append(F(_dHit[KRaid], "0.#")).Append(" ludzi; gra zdjelaby ").Append(F(_dGameHearth, "0.#")).Append(" hearth = ").Append(F0(_dGamePeople)).Append(" ludzi ksiegi");
                if (_dResumed > 0) sb.Append(" (rabunki podjete po wczytaniu gry: ").Append(_dResumed).Append(')');
                sb.Append(" | zerowanie: partii ").Append(_dForagers).Append(", osobodni ").Append(F(_dDays[KForage], "0")).Append(" x ").Append(F(Math.Max(0f, s.DevastationPerForagerDay), "0.##"))
                  .Append(" -> ").Append(F(_dHit[KForage], "0.#")).Append(" ludzi");
                if (_dFull[KRaid] + _dFull[KForage] > 0) sb.Append("; okreg nasycony (pulap ").Append(Math.Max(0, Math.Min(95, s.DevastationMaxPercent))).Append("% albo dno hearth): ").Append(_dFull[KRaid] + _dFull[KForage]).Append(" razy");
                sb.Append(" | powrot: ").Append(F(_dBack, "0.#")).Append(" ludzi do ").Append(_dBackVillages).Append(" wsi");
                if (_dRated > 0) sb.Append(", tempo srednio ").Append(F(100.0 * _dRateSum / _dRated, "0.###")).Append("% dziennie (ustawienie ").Append(F(Math.Max(0f, s.RefugeeReturnPercent), "0.##")).Append("% x (1 - niebezpieczenstwo))");
                sb.Append("; wstrzymany: wies spalona, lupiona albo pod przymusem ").Append(_dStopState).Append(", warownia oblezona ").Append(_dStopSiege);
                if (_dStopOther > 0) sb.Append(", niebezpieczenstwo pelne albo tempo 0: ").Append(_dStopOther);
                sb.Append(" | uchodzcy poza domem ").Append(F0(awayAll)).Append(" w ").Append(rows.Values.Count(r => r.Away > 0.0)).Append(" regionach");
                if (worst < 1f) sb.Append("; najnizszy mnoznik plonu x").Append(F(worst, "0.####")).Append(" (").Append(worstName).Append(')');
                if (top.Count > 0)
                {
                    var parts = new List<string>();
                    foreach (var r in top)
                        parts.Add(NameOf(r.Node) + " [" + (r.Node.IsTown ? "miasto" : (r.Node.IsCastle ? "zamek" : "wies")) + ", " + (r.Node.Culture != null ? r.Node.Culture.StringId : "?") + "] uchodzcy "
                                  + F0(r.Away) + " z " + F0(r.Home + r.Away) + " ludzi wsi (" + F(100.0 * r.Away / Math.Max(1.0, r.Home + r.Away), "0.##") + "%), plon najslabszej wsi x"
                                  + F(r.Worst, "0.####") + ", zabici od poczatku " + F0(r.Dead));
                    sb.Append(" | regiony najbardziej spustoszone (").Append(top.Count).Append("): ").Append(string.Join("; ", parts.ToArray()));
                }
                if (_dNoK > 0) sb.Append(" | bez przelicznika ludzi: ").Append(_dNoK);
                if (_dForeign > 0) sb.Append(" | hearth dopisany przez kogos w srodku kroku rabunku (zachowany): ").Append(_dForeign);
                sb.Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Devastation.RegionsNote", e); }
        }

        // ------------------------------------------------------------ latki
        internal static void ApplyAll(Harmony h)
        {
            var done = new List<string>(); var miss = new List<string>();
            try
            {
                var upd = AccessTools.Method(typeof(RaidEventComponent), "Update");
                if (upd != null && upd.DeclaringType == typeof(RaidEventComponent) && upd.GetParameters().Length == 1)
                {
                    h.Patch(upd, prefix: new HarmonyMethod(typeof(Devastation), nameof(RaidPrefix)), finalizer: new HarmonyMethod(typeof(Devastation), nameof(RaidFinalizer)));
                    _raidPatched = true; done.Add("krok rabunku");
                }
                else miss.Add("RaidEventComponent.Update");
            }
            catch (Exception e) { miss.Add("RaidEventComponent.Update(" + e.Message + ")"); }
            try
            {
                var end = AccessTools.Method(typeof(RaidEventComponent), "OnBeforeFinalize");
                if (end != null && end.DeclaringType == typeof(RaidEventComponent) && end.GetParameters().Length == 0)
                {
                    h.Patch(end, prefix: new HarmonyMethod(typeof(Devastation), nameof(EndPrefix)), finalizer: new HarmonyMethod(typeof(Devastation), nameof(EndFinalizer)));
                    _endPatched = true; done.Add("koniec rabunku");
                }
                else miss.Add("RaidEventComponent.OnBeforeFinalize");
            }
            catch (Exception e) { miss.Add("RaidEventComponent.OnBeforeFinalize(" + e.Message + ")"); }
            try
            {
                var lvl = AccessTools.Method(typeof(Village), "GetHearthLevel");
                if (lvl != null && lvl.ReturnType == typeof(int) && lvl.GetParameters().Length == 0)
                {
                    h.Patch(lvl, postfix: new HarmonyMethod(typeof(Devastation), nameof(LevelPostfix)));
                    _levelPatched = true; done.Add("progi produkcji od ludzi sprzed spustoszenia");
                }
                else miss.Add("Village.GetHearthLevel");
            }
            catch (Exception e) { miss.Add("Village.GetHearthLevel(" + e.Message + ")"); }
            try
            {
                var food = AccessTools.Method(typeof(DefaultVillageProductionCalculatorModel), "CalculateDailyFoodProductionAmount", new[] { typeof(Village) });
                if (food != null && food.ReturnType == typeof(float))
                {
                    h.Patch(food, postfix: new HarmonyMethod(typeof(Devastation), nameof(FoodPostfix)));
                    _foodPatched = true; done.Add("zywnosc wsi");
                }
                else miss.Add("DefaultVillageProductionCalculatorModel.CalculateDailyFoodProductionAmount");
            }
            catch (Exception e) { miss.Add("CalculateDailyFoodProductionAmount(" + e.Message + ")"); }

            // stan z ustawien startowych; MCM wchodzi przy starcie kampanii - prawdziwy stan stoi co dobe w linii "Ludzie:"
            var s = Settings.Current;
            Log.Info("Devastation: spustoszenie jako ulamek okregu (uchodzcy zamiast -39.7% hearth za rabunek i zamiast +0.5 hearth ponizej progu) "
                     + (On ? "CZYNNE" : (s != null && !s.DevastationEnabled ? "wylaczone w ustawieniach" : "NIECZYNNE - wymaga jednostki ludzi, przyrostu naturalnego i obu latek rabunku"))
                     + (s != null ? " - rabunek " + F(Math.Max(0f, s.DevastationPerRaiderDay), "0.##") + " i zerowanie " + F(Math.Max(0f, s.DevastationPerForagerDay), "0.##")
                                    + " czlowieka na zbrojnego na dobe, pulap " + Math.Max(0, Math.Min(95, s.DevastationMaxPercent)) + "% okregu, ginie " + s.DevastationKilledPercent
                                    + "%, w las " + s.DevastationOutlawPercent + "%, powrot " + F(Math.Max(0f, s.RefugeeReturnPercent), "0.##") + "% dziennie x (1 - niebezpieczenstwo), plon ^"
                                    + F(Math.Max(0f, s.DevastationYieldElasticity), "0.##") : "")
                     + "; wpiete: " + (done.Count > 0 ? string.Join(", ", done.ToArray()) : "nic")
                     + (miss.Count > 0 ? "; BRAK: " + string.Join(", ", miss.ToArray()) : "")
                     + (_levelPatched && _foodPatched ? "" : " (bez latki progow albo zywnosci mnoznik plonu dziala tylko w produkcji towarow)") + ".");
        }
    }
}
