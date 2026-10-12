using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// SPUSTOSZENIE JAKO ULAMEK OKREGU (E4/5 = paczka 113n; projekt etapu 4 R8, R9a, E4/5; plan docs/etap4/PLAN-KODU-E45.md; HR 2.1-2.8; decyzje Jeffa 11.10 (4)).
    /// Jeff 05.10: "spalenie wsi to jest tylko symbol - to nie jest jedna wies, tylko szereg wsi".
    /// Wies na mapie to okreg (srednio 74 tys. ludzi). Gra przy kazdym pelnym rabunku zdejmowala jej ok. 40% hearth bez wzgledu na to, czy przyszlo 60 ludzi,
    /// czy 2000, a zerowanie armii (ScorchedEarth) - hearth w skali gry; ludzie znikali z ksiegi. Odtad:
    ///  - RABUNEK i ZEROWANIE pustosza tyle ludzi, ilu zbrojnych i ile czasu: DevastationPerRaiderDay (0.5) czlowieka na zbrojnego na dobe lupienia,
    ///    DevastationPerForagerDay (0.2) na dobe postoju wrogiej armii; okregu nie da sie spustoszyc ponad DevastationMaxPercent (90%):
    ///    trafieni = (pulap - juz poza domem) x (1 - e^(-R x osobodni / pulap)), nie ponizej 11 hearth;
    ///  - trafieni NIE znikaja (zamknieta ksiega ludzi): zabici 2% (5% gdy najezdzca z kultury z niewola, nie wiecej niz 1 na zbrojnego na dobe), pojmani 10% przy
    ///    niewoli - E4/9: przy uderzeniu zostaja uchodzcami, przy koncu wygranego rabunku przechodza z konta UCHODZCY do lochu najezdzcy (ThrallLaw.CapturePrefix,
    ///    TakeCaptives - plan CapWould); BK nie daje juz jencow z niczego (U14, U22), las (2 + 3 x niebezpieczenstwo)% do puli
    ///    wyrzutkow regionu, reszta UCHODZCY - konto wsi w ksiedze ludzi (PeopleBook "u:"). Jedyny ruch hearth - PeopleBook.Move (MDevRaid, MDevForage; U23);
    ///  - POWROT: nikt nie wraca, dopoki w regionie (warownia + jej wsie) byl rabunek albo zerowanie z trafionymi w ostatnich RefugeeReturnQuietDays (30) dobach (zegar
    ///    regionu PeopleBook "r:"); potem liniowo - wszyscy zywi w domu najpozniej RefugeeReturnDays (364 = rok Armoury) dob po ostatnim najezdzie; oblezona warownia
    ///    wstrzymuje (wzor dogania); uchodzcy umieraja RefugeeYearlyLossPercent (1.3%) rocznie;
    ///  - PLON (R9a): towar, wydobycie i zywnosc wsi x (ludzie w domu / ludzie wsi)^DevastationYieldElasticity; progi produkcji gry (200 / 600 hearth) od ludzi
    ///    razem z uchodzcami; wies spladrowana (Looted) produkuje jak Normal x udzial ludzi w domu (LootedProducesByPeople) zamiast zera przez dwa tygodnie.
    /// Lupiezca dostaje od gry zloto i towar jak dotad: w srodku kodu rabunku wies ma hearth, jaki mialaby w grze (RaidState.Virt), poza nim prawdziwy -
    /// to samo widza cudze nasluchy konca rabunku (ROT, BKROT, BK, BEE, Diplomacy, NavalDLC). Wymaga People Unit Enabled i Natural Growth Enabled; bez nich
    /// albo przy wylaczonym DevastationEnabled wszystko jak przed E4/5, a uchodzcy jeszcze poza domem wracaja nastepnej doby (Abandon).
    /// Stanu w zapisie ta klasa nie ma (konto i zegary - PeopleBook; trwajace rabunki - bez zapisu, po wczytaniu odtwarzane z RaidDamage).
    /// </summary>
    internal static class Devastation
    {
        /// <summary>Gra i BK podnosza wies do 10 hearth z niczego (Village.DailyTick, prefiks BK) - spustoszenie tam nie schodzi.</summary>
        internal const float FloorHearth = 11f;
        // krok rabunku gry: co 0.05 punktow osady wies traci 0.5 x krok x hearth (RaidEventComponent.Update)
        private const float GameStep = 0.05f, GameStepKeep = 0.975f;
        private const int KRaid = 0, KForage = 1;
        private const int Top = 8;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static bool _raidPatched, _endPatched, _levelPatched, _foodPatched, _lootedPatched;   // latki wpiete (raz na proces)

        /// <summary>Spustoszenie czynne: klucz, obie latki rabunku, jednostka ludzi (E4/3) i przyrost naturalny (E4/4).</summary>
        internal static bool On
        {
            get
            {
                var s = Settings.Current;
                return s != null && s.DevastationEnabled && _raidPatched && _endPatched && PeopleUnit.On && PopulationLaw.GrowthOn;
            }
        }

        /// <summary>Wies spladrowana (Looted) produkuje jak Normal x udzial ludzi w domu (K5): spustoszenie czynne, latka TickProductions wpieta, klucz.</summary>
        internal static bool LootedOn
        {
            get { var s = Settings.Current; return On && _lootedPatched && s != null && s.LootedProducesByPeople; }
        }

        /// <summary>E4/9 (6.3): jency z rabunku z ludzi napadnietej wsi - spustoszenie czynne i klucz RaidCaptivesFromVillage (ThrallLaw.CapturePrefix, linie). Pojmani
        /// przechodza z konta UCHODZCY do lochu najezdzcy przy koncu wygranego rabunku; przy uderzeniu (Strike) zostaja uchodzcami.</summary>
        internal static bool CaptivesLive { get { var s = Settings.Current; return On && s != null && s.RaidCaptivesFromVillage; } }

        // ------------------------------------------------------------ rabunek w toku (bez zapisu)
        private sealed class RaidState
        {
            public double PersonDays;                   // osobodni lupienia od ostatniego rozliczenia
            public double TotalDays, Hit, Dead, CapWould;   // ten rabunek razem: osobodni, trafieni, zabici, pojmani "byloby" (E4/9)
            public float Virt; public bool HasVirt;     // hearth, jaki wies mialaby w grze po dotychczasowych krokach rabunku
            public int Steps, MaxMen;
            public bool Slaver;                         // najezdzca z kultury z niewola (LosersFlee.SlaveCulture - ta sama regula co H3)
            public bool CapTaken;                       // E4/9: pojmani tego rabunku juz wzieci (ThrallLaw.CapturePrefix - drugie wolanie BK = 0)
            public RaidEventComponent Comp;             // E4/11 (Z2, krytyka K2): klucz sesji ksiegi wiosek = ten sam obiekt, co MapEvent.Component widoku
        }
        private static readonly object _lock = new object();
        private static readonly Dictionary<RaidEventComponent, RaidState> _raids = new Dictionary<RaidEventComponent, RaidState>();
        private static Dictionary<string, Village> _byId;           // id osady -> wies (raz na kampanie)

        /// <summary>Stan wsi miedzy prefiksem a finalizerem latek rabunku.</summary>
        internal struct Tick { public Village V; public float Real, Virt; public bool Swapped; public object Rs; public double K; public Hero Leader; public int Gold; public long Paid0; }

        // ------------------------------------------------------------ liczniki doby (zeruje DayLine)
        private static readonly double[] _dHit = new double[2], _dDays = new double[2];
        private static readonly int[] _dFull = new int[2];
        private static readonly HashSet<string>[] _dVillages = { new HashSet<string>(), new HashSet<string>() };
        private static double _dDead, _dFled, _dAway, _dCapWould, _dSlaverHit, _dSlaverDead, _dGameHearth, _dGamePeople;
        private static long _dLootAi, _dLootPlayer;
        private static int _dSteps, _dRaids, _dRaidsFull, _dRaidMen, _dResumed, _dForagers, _dNoK, _dMoveFail, _dForeign;
        private static int _stumbles, _stumblesAll;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        // K5: wies spladrowana produkuje (doba)
        private static int _dLootedOn, _dLootedMade, _dLootedChanged, _dLootedGate;
        private static long _dLootedPieces;
        // K3: powrot, smierc, wstrzymania (doba)
        private static double _dBack, _dDied, _dLinErr;
        private static int _dBackVillages, _dStopQuiet, _dStopSiege, _dNoClock, _dNoVillage, _dAbandonLeft;
        // E4/9 (6.2): jency z rabunku - rabunki z pojmanymi, pojmani, plan (10% trafionych przy niewoli), nie wzieci: gracz Leave, bez stanu rabunku, za malo uchodzcow,
        // brak postaci; pojmani u najezdzcy bez niewoli (kontrola - 0); gracz
        private static int _dCapRaids, _dCapCalls, _dCapNoState, _dCapNoChar, _dCapNotSlaver;
        private static double _dCapJ, _dCapPlan, _dCapLeave, _dCapFew, _dCapPlayer;
        private static int _dStopHunger;                                    // E4/6: powrot wstrzymany - wies nie przyjmuje (Famine.ReturnGate: glod gospodarzy, a konto w innym regionie - takze pole nie karmi i spichlerz ponizej FamineReturnMinDays) albo czesc z glodu czeka na odzyskanie wsi
        private static readonly HashSet<string> _dBackRegions = new HashSet<string>();
        private static bool _abandonLogged;
        // E4/11 (Z3, krytyka K5): powod wstrzymania powrotu konta wsi w DZISIEJSZYM Daily - czyszczony na poczatku Daily, wypelniany w tych samych continue
        // (logika i liczby powrotu bez zmian); bez wpisu = 0 (nic nie wstrzymalo albo konto puste)
        internal const int StopQuiet = 1, StopSiege = 2, StopGate = 3, StopStay = 4, StopFamWait = 5;
        private static readonly Dictionary<string, int> _stopOf = new Dictionary<string, int>();

        internal static void Reset()
        {
            lock (_lock) { _raids.Clear(); _byId = null; }
            _spiral.Clear();
            lock (_errSites) _errSites.Clear();
            _stumblesAll = 0; _abandonLogged = false;
            _stopOf.Clear();
            NewDay();
        }

        internal static void NewDay()
        {
            Array.Clear(_dHit, 0, 2); Array.Clear(_dDays, 0, 2); Array.Clear(_dFull, 0, 2);
            _dVillages[0].Clear(); _dVillages[1].Clear();
            _dDead = _dFled = _dAway = _dCapWould = _dSlaverHit = _dSlaverDead = _dGameHearth = _dGamePeople = 0.0;
            _dLootAi = _dLootPlayer = 0;
            _dSteps = _dRaids = _dRaidsFull = _dRaidMen = _dResumed = _dForagers = _dNoK = _dMoveFail = _dForeign = 0;
            _dBack = _dDied = _dLinErr = 0.0;
            _dBackVillages = _dStopQuiet = _dStopSiege = _dNoClock = _dNoVillage = _dAbandonLeft = 0;
            _dStopHunger = 0;
            _dCapRaids = _dCapCalls = _dCapNoState = _dCapNoChar = _dCapNotSlaver = 0; _dCapJ = _dCapPlan = _dCapLeave = _dCapFew = _dCapPlayer = 0.0;
            _dBackRegions.Clear();
            _dLootedOn = _dLootedMade = _dLootedChanged = _dLootedGate = 0; _dLootedPieces = 0;
            _stumbles = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesAll++;                    // liczymy potkniecia, niczego nie gasimy
            bool first;
            lock (_errSites) first = _errSites.Add(where);
            if (first) Log.Error(where, e);                 // pierwszy wyjatek kazdego miejsca do logu, kolejne tylko w liczniku doby
        }

        private static double Clamp01(double x) { return double.IsNaN(x) ? 0.0 : (x < 0.0 ? 0.0 : (x > 1.0 ? 1.0 : x)); }
        /// <summary>Pulap spustoszenia okregu 0..0.95 (Room; E4/11: pulap pustych osad wioski - ten sam).</summary>
        internal static double MaxShare(Settings s) { return Math.Max(0, Math.Min(95, s.DevastationMaxPercent)) / 100.0; }

        // ------------------------------------------------------------ czy spustoszenie obejmuje wies
        /// <summary>
        /// Rabunek, zerowanie, OnVillageLooted: czynne i wies ma przelicznik ludzi (kalibruje - pierwsza doba nowej kampanii: rabunek, ktory ruszyl przed
        /// pierwszymi rentami, szedlby po staremu). `per` = stawka PeopleUnit.PerMan - ta sama, ktora PeopleBook.Move zdejmie (k ponizej 2: obcieta do 0.5).
        /// Wies bez przelicznika zostaje przy regulach sprzed E4/5 (w ROT przelicznik ma kazda wies).
        /// </summary>
        internal static bool Covers(Village v, out float per)
        {
            per = 0f;
            if (v == null || v.Settlement == null || !On) return false;
            try
            {
                PopulationLaw.EnsureCalibrated();
                bool unit;
                float p = PeopleUnit.PerMan(v, PeopleUnit.GamePerMan, out unit);
                if (!unit || !(p > 0f) || float.IsInfinity(p)) return false;
                per = p;
                return true;
            }
            catch (Exception e) { Stumble("Devastation.Covers", e); return false; }
        }

        /// <summary>Latki modeli, RefugeeReturn, okno W6 (moga isc z watkow AI / z modelu hearth): bez kalibracji, k z PopulationLaw.KFor.</summary>
        internal static bool CoversQuiet(Village v)
        {
            if (v == null || v.Settlement == null || !On) return false;
            try { float k = PopulationLaw.KFor(v.Settlement); return k > 0f && !float.IsInfinity(k); }
            catch (Exception e) { Stumble("Devastation.CoversQuiet", e); return false; }
        }

        /// <summary>Ilu ludzi tej wsi mozna jeszcze spustoszyc (do pulapu okregu i nie ponizej 11 hearth); 0 = okreg nasycony.</summary>
        private static double Room(Village v, Settings s, double k, out double away, out double reach)
        {
            away = 0.0; reach = 0.0;
            if (v == null || v.Settlement == null || !(k > 0.0)) return 0.0;
            float hearth = v.Hearth;
            if (!(hearth > 0f) || float.IsInfinity(hearth)) return 0.0;      // takze NaN
            away = PeopleBook.RefugeesOf(v);
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
                float per;
                if (s == null || !Covers(v, out per)) return false;
                double away, reach;
                return Room(v, s, 1.0 / per, out away, out reach) > 0.0;
            }
            catch (Exception e) { Stumble("Devastation.Open", e); return false; }
        }

        /// <summary>Niebezpieczenstwo wsi 0..1 (PopulationLaw.Danger - ta sama miara co przyrost naturalny).</summary>
        private static double DangerOf(Village v)
        {
            try { float sec, burnt; bool war; return Clamp01(PopulationLaw.Danger(v, out sec, out war, out burnt)); }
            catch (Exception e) { Stumble("Devastation.DangerOf", e); return 0.0; }
        }

        /// <summary>Najezdzca z kultury z niewola (ta sama regula co H3 - LosersFlee.SlaveCulture).</summary>
        private static bool SlaverOf(IFaction f)
        {
            try { return f != null && LosersFlee.SlaveCulture(f.Culture); }
            catch (Exception e) { Stumble("Devastation.SlaverOf", e); return false; }
        }

        // ------------------------------------------------------------ spustoszenie (rdzen)
        /// <summary>
        /// `personDays` osobodni napastnika przy tej wsi, `perDay` ludzi okregu na zbrojnego na dobe. Trafieni schodza z DOM jednym ruchem PeopleBook.Move
        /// (1/k za czlowieka) i dziela sie: zabici (konto wsi, smierc R3 - licznik do E4/7), pojmani (0 do E4/9), las (pula wyrzutkow regionu 1:1),
        /// uchodzcy (konto wsi). Miedzy Move a kontami nic nie rzuca (U24). Zwraca liczbe trafionych; trafieni = suma udzialow co do ulamka czlowieka.
        /// </summary>
        private static double Strike(Village v, double personDays, double perDay, int kind, bool slaver, RaidState rs, MobileParty src = null)
        {
            var s = Settings.Current;
            if (s == null || v == null || !(personDays > 0.0) || !(perDay > 0.0)) return 0.0;
            float per;
            if (!Covers(v, out per)) { _dNoK++; return 0.0; }
            double k = 1.0 / per;
            double away, reach;
            double open = Room(v, s, k, out away, out reach);
            if (!(open > 0.0)) { _dFull[kind]++; return 0.0; }
            // napastnik przeczesuje okreg na oslep: czesc tego, co pali, jest juz spalona - stad wykladnik, nie iloczyn
            double hit = (reach - away) * (1.0 - Math.Exp(-perDay * personDays / reach));
            if (hit > open) hit = open;
            if (!(hit > 0.0) || double.IsNaN(hit) || double.IsInfinity(hit)) return 0.0;
            double kill = Clamp01((slaver ? s.DevastationKilledSlaverPercent : s.DevastationKilledPercent) / 100.0);
            double capW = slaver ? Clamp01(s.DevastationCaptivesPercent / 100.0) : 0.0;   // pojmani calego rabunku (R8) - E4/9: plan ThrallLaw.CapturePrefix
            // E4/9 (6.3): pojmani przy uderzeniu zostaja uchodzcami - przechodza do lochu najezdzcy dopiero przy koncu WYGRANEGO rabunku (ThrallLaw.CapturePrefix,
            // z konta UCHODZCY); bez stanu "w drodze" (nie gina przy zapisie w polowie rabunku), przegrany rabunek albo gracz "Leave" - zostaja uchodzcami
            double cap = 0.0;
            double flee = 0.0;
            if (OutlawLaw.On)                                 // bez prawa wyrzutkow puli nikt nie prowadzi - zostaja uchodzcami
                flee = Clamp01((Math.Max(0, s.DevastationOutlawBasePercent) + Math.Max(0, s.DevastationOutlawDangerPercent) * DangerOf(v)) / 100.0);
            if (kill + cap > 1.0) cap = Math.Max(0.0, 1.0 - kill);
            if (kill + cap + flee > 1.0) flee = Math.Max(0.0, 1.0 - kill - cap);
            double dead = Math.Min(hit * kill, personDays);   // [113 KRYT 8] najwyzej 1 zabity na zbrojnego na dobe; nadwyzka zostaje uchodzcami
            double capt = hit * cap, fled = hit * flee;
            double refugees = hit - dead - capt - fled;
            if (refugees < 0.0) refugees = 0.0;
            double lBefore = (double)Math.Max(0f, v.Hearth) * k + away;       // E4/6: ludzie okregu przed ruchem (ulamek spalonego spichlerza)
            // JEDYNY ruch hearth (U23): trafieni z DOM
            double moved = -PeopleBook.Move(v, PeopleBook.ADom, PeopleBook.ARefugees, hit, kind == KRaid ? PeopleBook.MDevRaid : PeopleBook.MDevForage, PeopleUnit.GamePerMan);
            if (!(moved > 0.0)) { _dMoveFail++; return 0.0; }         // Move nie rzuca; 0 = wyjatek w Shift - nikogo nie przenosimy
            double f = Math.Min(1.0, moved * k / hit);                  // gdyby Move przyjal mniej - wszystkie konta w tej samej proporcji
            var region = OutlawLaw.RegionFor(v.Settlement);
            int day = (int)CampaignTime.Now.ToDays;
            PeopleBook.Devastated(v, refugees * f, dead * f, region, day);
            if (fled > 0.0) OutlawLaw.AddFled(v.Settlement, (float)(fled * f));   // pula regionu 1:1, bez hearth (zdjal Move)
            if (lBefore > 0.0) Famine.Burn(v, hit * f / lBefore);         // E4/6 (GL 2.2 pkt 6): spichlerz okregu traci ten sam ulamek co ludzie; po kontach, nie rzuca
            // E4/11 (Z1, U24): trafieni na spalone osady wiosek pobocznych - po kontach, wlasny try (OnStrike nie rzuca); rabunek - klucz sesji, zerowanie - oboz armii
            // E7/W1 (232): + zabici tego uderzenia (pamiec wojny wioski - te same liczby, ktore nizej dopisuje rs.Dead)
            try { VillageBook.OnStrike(v, hit * f, refugees * f, dead * f, kind == KRaid, rs != null ? rs.Comp : null, src, lBefore); }
            catch (Exception e) { Stumble("Devastation.Strike(E4/11)", e); }
            // pojmani przy uderzeniu = 0 na stale (E4/9: z konta UCHODZCY przy koncu rabunku); CapWould - plan pojmanych calego rabunku
            double h = hit * f;
            if (rs != null) { rs.Hit += h; rs.Dead += dead * f; rs.CapWould += h * capW; }
            _dHit[kind] += h; _dDead += dead * f; _dFled += fled * f; _dAway += refugees * f; _dCapWould += h * capW;
            if (slaver) { _dSlaverHit += h; _dSlaverDead += dead * f; }
            if (v.Settlement.StringId != null) _dVillages[kind].Add(v.Settlement.StringId);
            return h;
        }

        /// <summary>Wroga armia zywi sie z okregu (ScorchedEarth.OnDaily): doba postoju partii przy wsi (ludzie = caly sklad, jak dawny drain).</summary>
        internal static void Forage(Village v, MobileParty mp)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || v == null || mp == null || mp.MemberRoster == null) return;
                int men = mp.MemberRoster.TotalManCount;
                if (men <= 0) return;
                _dForagers++; _dDays[KForage] += men;
                Strike(v, men, Math.Max(0f, s.DevastationPerForagerDay), KForage, SlaverOf(mp.MapFaction), null, mp);
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
        /// Stan rabunku; nowy wpis po wczytaniu gry w polowie rabunku odtwarza hearth gry z RaidDamage (kroki po 0.05, kazdy x0.975) - przyblizenie dotyczy
        /// tylko zlota i towaru lupiezcy z pozostalych krokow, ksiegi ludzi nie dotyka.
        /// </summary>
        private static RaidState StateOf(RaidEventComponent raid, Village v, bool create)
        {
            RaidState rs;
            lock (_lock)
            {
                if (_raids.TryGetValue(raid, out rs) || !create) return rs;
                rs = new RaidState { Comp = raid };
                float done = raid.RaidDamage;
                if (done > 0f && v.Hearth > 0f)
                {
                    rs.Virt = (float)(v.Hearth * Math.Pow(GameStepKeep, done / GameStep)); rs.HasVirt = true;
                    _dResumed++;
                }
                _raids[raid] = rs;
            }
            try { var att = raid.AttackerSide; var lp = att != null ? att.LeaderParty : null; rs.Slaver = lp != null && SlaverOf(lp.MapFaction); }
            catch (Exception e) { rs.Slaver = false; Stumble("Devastation.StateOf(niewola)", e); }
            return rs;
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
                float per;
                if (!Covers(v, out per)) return;              // wies bez przelicznika ludzi: rabunek gry bez zmian
                var rs = StateOf(__instance, v, true);
                var def = __instance.DefenderSide; var att = __instance.AttackerSide;
                if (def != null && att != null && def.TroopCount == 0)        // faza lupienia - ten sam warunek co w grze (R:162)
                {
                    int men = att.TroopCount;
                    if (men > 0)
                    {
                        rs.PersonDays += men * TickDays();
                        if (men > rs.MaxMen) rs.MaxMen = men;
                    }
                }
                float real = v.Hearth;
                __state.Rs = rs; __state.Real = real; __state.Virt = rs.HasVirt ? rs.Virt : real; __state.V = v; __state.K = 1.0 / per;
                // lup z niczego (zloto gry za hearth) - kiesa wodza przed krokiem (pomiar przejety z okna W6 E4/0 dla wsi objetych)
                try { var lp = att != null ? att.LeaderParty : null; __state.Leader = lp != null ? lp.LeaderHero : null; __state.Gold = __state.Leader != null ? __state.Leader.Gold : 0; }
                catch { __state.Leader = null; }
                __state.Paid0 = SmallGold.RaidPaidSum;                       // E5/A7: lup z kiesy wsi to przelew, nie zloto z niczego - finalizer go odejmuje
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
                    _dSteps++; _dGameHearth += took; _dGamePeople += (double)took * __state.K;
                    if (__state.Leader != null)
                    {
                        long gold = (long)__state.Leader.Gold - __state.Gold - (SmallGold.RaidPaidSum - __state.Paid0);   // E5/A7: bez tego, co zaplacila kiesa wsi
                        if (gold > 0) { if (__state.Leader == Hero.MainHero) _dLootPlayer += gold; else _dLootAi += gold; }
                    }
                    Settle(v, rs);
                }
            }
            catch (Exception e) { Stumble("Devastation.RaidFinalizer", e); }
            return __exception;
        }

        /// <summary>Rozlicza osobodni lupienia zebrane od ostatniego rozliczenia (na PRAWDZIWYM hearth wsi).</summary>
        private static void Settle(Village v, RaidState rs)
        {
            double pd = rs.PersonDays;
            if (!(pd > 0.0)) return;
            rs.PersonDays = 0.0; rs.TotalDays += pd; _dDays[KRaid] += pd;
            Strike(v, pd, Math.Max(0f, Settings.Current.DevastationPerRaiderDay), KRaid, rs.Slaver, rs);
        }

        // ------------------------------------------------------------ koniec rabunku: RaidEventComponent.OnBeforeFinalize()
        // Gra ustawia tu stan wsi (spalona albo z powrotem zwykla) i rozsyla RaidCompleted (R:138). Najpierw rozliczamy czas lupienia od ostatniego kroku
        // (takze rabunek przerwany przed pierwszym krokiem) - na PRAWDZIWYM hearth - zeby nasluchy (ROT/OthersRise K7, BKROT, BK jency, BEE, Diplomacy, NavalDLC)
        // widzialy komplet zabitych; potem podmieniamy hearth na hearth gry (to samo, co widzieli dotad).
        public static void EndPrefix(RaidEventComponent __instance, out Tick __state)
        {
            __state = default(Tick);
            if (__instance == null) return;
            try
            {
                var st = __instance.MapEventSettlement;
                var v = st != null ? st.Village : null;
                if (v == null) return;
                float per;
                bool on = Covers(v, out per);
                var rs = StateOf(__instance, v, on);
                if (rs == null) return;
                if (on) Settle(v, rs);
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
                    if (rs != null && rs.TotalDays > 0.0)
                    {
                        _dRaids++; _dRaidMen += rs.MaxMen;
                        if (v.VillageState == Village.VillageStates.Looted) _dRaidsFull++;
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

        /// <summary>Zabici calego rabunku przy spustoszeniu (po rozliczeniu w EndPrefix) - rajd Innych (OthersRise, K7); 0 gdy brak stanu.</summary>
        internal static double RaidDead(RaidEventComponent r)
        {
            if (r == null) return 0.0;
            lock (_lock) { RaidState rs; return _raids.TryGetValue(r, out rs) ? rs.Dead : 0.0; }
        }

        /// <summary>
        /// E4/9 (6.2 pkt 2): stan rabunku wsi v (koniec rabunku - BK ExecuteCapture biegnie w OnBeforeFinalize, PRZED EndFinalizer, ktory usuwa stan) - plan pojmanych
        /// calego rabunku (CapWould: 10% trafionych przy najezdzcy z niewola) i znacznik "wzieci" (drugie wolanie = 0). false - brak stanu (konsola BK, wies bez przelicznika).
        /// </summary>
        internal static bool TakeCaptives(Village v, out double planned, out bool slaver)
        {
            planned = 0.0; slaver = false;
            if (v == null || v.Settlement == null) return false;
            try
            {
                lock (_lock)
                {
                    foreach (var kv in _raids)
                    {
                        var comp = kv.Key; var rs = kv.Value;
                        if (comp == null || rs == null || comp.MapEventSettlement != v.Settlement) continue;
                        slaver = rs.Slaver;
                        if (!rs.CapTaken) { rs.CapTaken = true; planned = rs.CapWould; }
                        return true;
                    }
                }
            }
            catch (Exception e) { Stumble("Devastation.TakeCaptives", e); }
            return false;
        }

        /// <summary>E4/9: liczniki jencow z rabunku (ThrallLaw.CapturePrefix) - linia "Ludzie (spustoszenie)" i odcinek "Jency (E4/0)" (Measure40 czyta przed DayLine).</summary>
        internal static void NoteCapture(bool found, double plan, int taken, bool leave, bool noChar, int few, bool slaver, bool player)
        {
            _dCapCalls++;
            if (!found) { _dCapNoState++; return; }
            _dCapPlan += plan;
            if (leave) _dCapLeave += plan;
            if (noChar) _dCapNoChar++;
            _dCapFew += few;
            if (taken > 0) { _dCapRaids++; _dCapJ += taken; if (!slaver) _dCapNotSlaver++; if (player) _dCapPlayer += taken; }
        }

        /// <summary>E4/9: pojmani z wsi od ostatniej linii doby (bez zerowania - zeruje DayLine): rabunki z pojmanymi, pojmani, gracz.</summary>
        internal static void CaptivesToday(out int raids, out double taken, out double player)
        {
            raids = _dCapRaids; taken = _dCapJ; player = _dCapPlayer;
        }

        /// <summary>Pojmani tego rabunku (E4/9: BK ExecuteCapture = dokladnie te osoby); w E4/5 zawsze 0.</summary>
        internal static double RaidCaptives(RaidEventComponent r)
        {
            if (r == null || !CaptivesLive) return 0.0;
            lock (_lock) { RaidState rs; return _raids.TryGetValue(r, out rs) ? rs.CapWould : 0.0; }
        }

        // ------------------------------------------------------------ plon (R9a, K4): mnoznik rak, progi produkcji, zywnosc wsi
        /// <summary>
        /// Mnoznik plonu wsi od ubytku rak (R9a): (ludzie w domu / (ludzie w domu + uchodzcy)) ^ elastycznosc, dno (1 - pulap) ^ e. Dokladnie 1, gdy spustoszenie
        /// jest wylaczone albo wies nie ma uchodzcow. Z latek modeli (moga isc z watkow AI): bez kalibracji, k z PopulationLaw.KFor; nie rzuca.
        /// </summary>
        internal static float YieldFactor(Village v)
        {
            if (PeopleBook.RefugeeVillages == 0 || v == null) return 1f;
            try
            {
                if (!CoversQuiet(v)) return 1f;
                double away = PeopleBook.RefugeesOf(v);
                if (!(away > 0.0)) return 1f;
                float k = PopulationLaw.KFor(v.Settlement);
                if (!(k > 0f) || float.IsInfinity(k)) return 1f;
                var s = Settings.Current;
                double home = (double)Math.Max(0f, v.Hearth) * k;
                double e = Math.Max(0f, Math.Min(2f, s.DevastationYieldElasticity));
                double m = Math.Pow(home / (home + away), e);
                if (double.IsNaN(m) || double.IsInfinity(m)) return 1f;
                // dno z pulapu: okreg spustoszony do pulapu daje (1 - pulap) ^ e; nizej nie schodzimy nawet po zmianie pulapu w MCM
                double lo = Math.Pow(Math.Max(0.05, 1.0 - MaxShare(s)), e);
                if (m < lo) m = lo;
                return m >= 1.0 ? 1f : (float)m;
            }
            catch (Exception ex) { Stumble("Devastation.YieldFactor", ex); return 1f; }
        }

        /// <summary>
        /// Village.GetHearthLevel(): progi 200 / 600 hearth licza sie od ludzi razem z uchodzcami (hearth + uchodzcy / k) - ubytek rak dziala tylko przez ciagly
        /// mnoznik (YieldFactor), wies nie spada o poziom (-50% plonu) od jednego rabunku. Poziom tylko podnosimy.
        /// </summary>
        public static void LevelPostfix(Village __instance, ref int __result)
        {
            if (PeopleBook.RefugeeVillages == 0 || __result >= 2 || __instance == null) return;
            try
            {
                if (!CoversQuiet(__instance)) return;
                double away = PeopleBook.RefugeesOf(__instance);
                if (!(away > 0.0)) return;
                float k = PopulationLaw.KFor(__instance.Settlement);
                if (!(k > 0f) || float.IsInfinity(k)) return;
                double before = __instance.Hearth + away / k;
                int level = before >= 600.0 ? 2 : (before >= 200.0 ? 1 : 0);
                if (level > __result) __result = level;
            }
            catch (Exception e) { Stumble("Devastation.LevelPostfix", e); }
        }

        /// <summary>DefaultVillageProductionCalculatorModel.CalculateDailyFoodProductionAmount: zywnosc wsi x mnoznik rak (obok zimy WinterBite - mnoza sie).</summary>
        public static void FoodPostfix(Village __0, ref float __result)
        {
            if (PeopleBook.RefugeeVillages == 0 || !(__result > 0f)) return;
            float m = YieldFactor(__0);
            if (m != 1f) __result *= m;
        }

        // ------------------------------------------------------------ wies spladrowana produkuje (K5; P :430, SP:459-460)
        // Gra w TickProductions produkuje tylko dla !village.IsDeserted (IsDeserted = stan Looted - G VillageGoodProductionCampaignBehavior.cs:141, Village.cs:81),
        // a model Default i tak daje 0 poza Normal. Na czas jednego wywolania TickProductions wies Looted ma stan Normal (POLE - setter rozsyla OnVillageBecomeNormal,
        // tego nie chcemy; tylko watek glowny, przywracane w finalizerze takze przy wyjatku): bramka magazynu 1.5 W, BK TickGoodProduction -> modele (zima, BK,
        // MaterialLaw z czynnikiem rak), TickFoodProduction -> zywnosc (zima, FoodPostfix), las wsi VillageWoodlot, ramki GoodsLedger - widza Normal.
        // Po wyjsciu - Looted (postoj 8-17 dob i blokada ponownego rabunku bez zmian). Inne wywolania modelu (dymek, rozmiar taboru) dalej widza 0.
        private static AccessTools.FieldRef<Village, Village.VillageStates> _stateRef;
        private static int _mainThread = -1;    // ManagedThreadId z ApplyAll (menu glowne = watek glowny)

        internal sealed class LootedTick { public Village V; public int Before; }

        private static int Pieces(Village v)
        {
            var o = v != null ? v.Owner : null;
            var r = o != null ? o.ItemRoster : null;
            if (r == null) return 0;
            int n = 0;
            for (int i = 0; i < r.Count; i++) n += r[i].Amount;
            return n;
        }

        private static bool SwapLooted(Village v)
        {
            return v != null && _stateRef != null && LootedOn && _stateRef(v) == Village.VillageStates.Looted
                   && System.Threading.Thread.CurrentThread.ManagedThreadId == _mainThread && CoversQuiet(v);
        }

        /// <summary>Prefiks Priority.First na VillageGoodProductionCampaignBehavior.TickProductions(Settlement, bool): wies Looted -> Normal na czas wywolania.</summary>
        public static void LootedPrefix(Settlement __0, bool __1, out LootedTick __state)
        {
            __state = null;
            try
            {
                var v = __0 != null ? __0.Village : null;
                if (__1 || !SwapLooted(v)) return;           // rozdanie startowe nowej kampanii - bez zmian
                var t = new LootedTick { V = v, Before = Pieces(v) };
                __state = t;
                _stateRef(v) = Village.VillageStates.Normal;
                _dLootedOn++;
                // poprawka po recenzji E4/5 (R2): bramka gry 1.5 W (TickProductions, VGP.cs:148-149) liczona tu po podmianie - pojemnosc jak dla Normal, ta sama
                // liczba, ktora gra sprawdzi za chwile. Woz wsi rusza tylko ze wsi Normal (VillagerCampaignBehavior.TickVillageThink :125), wiec wies Looted
                // produkuje do 1.5 W (ok. 7.5 doby produkcji), potem stoi z towarem w magazynie do powrotu do Normal - to nie blad produkcji (S13 bez tych dob)
                try { if ((float)t.Before >= (float)v.GetWarehouseCapacity() * 1.5f) _dLootedGate++; }
                catch (Exception e) { Stumble("Devastation.LootedPrefix(bramka)", e); }
            }
            catch (Exception e) { Stumble("Devastation.LootedPrefix", e); }
        }

        public static Exception LootedFinalizer(LootedTick __state, Exception __exception)
        {
            if (__state == null) return __exception;
            try
            {
                if (_stateRef(__state.V) == Village.VillageStates.Normal) _stateRef(__state.V) = Village.VillageStates.Looted;
                else _dLootedChanged++;                      // ktos zmienil stan w srodku (nie powinno sie zdarzyc) - zostawiamy jego zmiane
                int d = Pieces(__state.V) - __state.Before;
                if (d > 0) { _dLootedMade++; _dLootedPieces += d; }
            }
            catch (Exception e) { Stumble("Devastation.LootedFinalizer", e); }
            return __exception;
        }

        /// <summary>Measure40 (linia "Zima (E4/0)", pomiar zywnosci wsi swiata): model gry z ta sama podmiana stanu dla wsi Looted, co w TickProductions (rozdz. 15 pkt 4).</summary>
        internal static float FoodToday(VillageProductionCalculatorModel model, Village v)
        {
            if (model == null || v == null) return 0f;
            bool swapped = false;
            try
            {
                if (SwapLooted(v)) { _stateRef(v) = Village.VillageStates.Normal; swapped = true; }
                return model.CalculateDailyFoodProductionAmount(v);
            }
            finally
            {
                if (swapped && _stateRef(v) == Village.VillageStates.Normal) _stateRef(v) = Village.VillageStates.Looted;
            }
        }

        // ------------------------------------------------------------ odczyty dla innych linii
        /// <summary>Okno W6 (linia "Hearth (E4/0)"): kroki rabunku gry pod spustoszeniem i lup z niczego od ostatniej linii doby (bez zerowania - zeruje DayLine).</summary>
        internal static void StepsToday(out int steps, out long lootAi, out long lootPlayer)
        {
            steps = _dSteps; lootAi = _dLootAi; lootPlayer = _dLootPlayer;
        }

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

        // ------------------------------------------------------------ doba: smierc, powrot, oblezenie (K3)
        private static int Clamp(int x, int lo, int hi) { return x < lo ? lo : (x > hi ? hi : x); }

        /// <summary>Dzienne ryzyko smierci uchodzcy: 1 - (1 - L%)^(1/dni roku) (1.3%/rok przy 364 = 3.595e-5/d).</summary>
        private static double DailyLoss(Settings s)
        {
            double y = Clamp01(s.RefugeeYearlyLossPercent / 100.0);
            return 1.0 - Math.Pow(1.0 - y, 1.0 / Math.Max(28, CampaignTime.DaysInYear));
        }

        /// <summary>
        /// Raz na dobe (ArmouryBehavior.OnDailyTick, zaraz po ScorchedEarth.OnDaily - zegar widzi dzisiejsze zerowanie): uchodzcy umieraja (bez narodzin),
        /// potem wracaja - nikt, dopoki w regionie byl rabunek albo zerowanie z trafionymi w ostatnich Q dobach; potem dziennie reszta / (D - d), d = dni od ostatniego
        /// najazdu w regionie - liniowo, wszyscy zywi w domu w dobie D; ostatni czlowiek wraca caly; oblezona warownia regionu wstrzymuje (wzor dogania - "rok" trzyma).
        /// Bez czynnika niebezpieczenstwa w tempie (lamalby "rok" - U13). Wylaczone - wszyscy do domu od razu (Abandon).
        /// </summary>
        internal static void Daily()
        {
            _stopOf.Clear();
            try
            {
                var s = Settings.Current;
                if (s == null || Campaign.Current == null) return;
                if (!On) { Abandon(); return; }
                _abandonLogged = false;
                if (PeopleBook.RefugeeVillages == 0) return;
                int today = (int)CampaignTime.Now.ToDays;
                int Q = Clamp(s.RefugeeReturnQuietDays, 0, 360);
                int D = Clamp(s.RefugeeReturnDays, Q + 1, 3640);
                double q = DailyLoss(s);
                var list = new List<KeyValuePair<string, double>>();
                PeopleBook.RefugeesSnapshot(list);
                var map = ById();
                foreach (var kv in list)
                {
                    try
                    {
                        Village v;
                        if (!map.TryGetValue(kv.Key, out v) || v == null || v.Settlement == null) { _dNoVillage++; continue; }
                        double away = kv.Value;
                        if (q > 0.0)
                        {
                            double died = PeopleBook.RefugeesDie(v, away * q);
                            _dDied += died; away -= died;
                        }
                        if (!(away > 0.0)) continue;
                        var region = OutlawLaw.RegionFor(v.Settlement);
                        int last;
                        bool clock = PeopleBook.LastStrike(region, out last);
                        // E4/6 (GL 2.6): nikt nie wraca do wsi, ktora nie przyjmuje (Famine.ReturnGate); schronieni w murach oblezonej warowni nie wychodza.
                        // Poprawka po recenzji E4/6 (R1-1): konto dzieli sie na czesc z glodu (famAway - rok od doby, w ktorej wies znow przyjmuje: famDay; -1 = czekaja)
                        // i reszte (rabunek, zerowanie - rok od ostatniego najazdu w regionie, decyzja Jeffa 11.10) - glod nie przestawia zegara uchodzcom z rabunku
                        int famDay; double famAway;
                        int famStop = Famine.ReturnGate(v, today, away, out famDay, out famAway);
                        if (famStop == 1) { _dStopHunger++; NoteStop(kv.Key, StopGate); continue; }
                        if (famStop == 2) { _dStopSiege++; NoteStop(kv.Key, StopStay); continue; }
                        if (clock && today - last < Q) { _dStopQuiet++; NoteStop(kv.Key, StopQuiet); continue; }                    // 30 dob spokoju po najezdzie w regionie - dla calego konta
                        if (region != null && region.IsUnderSiege) { _dStopSiege++; NoteStop(kv.Key, StopSiege); continue; }       // HR 2.8: uchodzcy siedza w oblezonej warowni
                        double raidAway = Math.Max(0.0, away - famAway), backR = 0.0, backF = 0.0;
                        if (raidAway > 0.0)
                        {
                            if (!clock) { last = today - Q; _dNoClock++; }                             // nie powinno sie zdarzyc (zegar rusza razem z kontem rabunku)
                            backR = raidAway / Math.Max(1, D - (today - last));
                            if (raidAway - backR < 1.0) backR = raidAway;                              // ostatni czlowiek wraca caly
                        }
                        if (famAway > 0.0)
                        {
                            if (famDay < 0) { _dStopHunger++; NoteStop(kv.Key, StopFamWait); }         // wies jeszcze nie przyjmuje uchodzcow z glodu - czekaja (reszta wraca)
                            else
                            {
                                backF = famAway / Math.Max(1, D - (today - famDay));
                                if (famAway - backF < 1.0) backF = famAway;
                            }
                        }
                        double back = backR + backF;
                        if (!(back > 0.0)) continue;
                        double got = PeopleBook.RefugeesHome(v, back, PeopleBook.MRefugeeHome, PeopleUnit.GamePerMan);
                        if (famAway > 0.0) Famine.Returned(v, famAway, got > 0.0 ? got * (backF / back) : 0.0);   // czesc z glodu po powrocie (nie rzuca)
                        double err = Math.Abs(got - back) / Math.Max(1.0, back);                       // kontrola liniowosci: 0 poza dnem konta
                        if (err > _dLinErr) _dLinErr = err;
                        if (got > 0.0)
                        {
                            _dBack += got; _dBackVillages++;
                            if (region != null && region.StringId != null) _dBackRegions.Add(region.StringId);
                        }
                    }
                    catch (Exception e) { Stumble("Devastation.Daily(wies)", e); }
                }
            }
            catch (Exception e) { Stumble("Devastation.Daily", e); }
        }

        private static void NoteStop(string id, int code) { if (id != null) _stopOf[id] = code; }

        /// <summary>E4/11 (Z3, krytyka K5): powod wstrzymania powrotu konta tej wsi w dzisiejszym Daily (StopQuiet..StopFamWait), 0 - brak. Watek glowny (doba).</summary>
        internal static int StopOf(Village v)
        {
            int c;
            return v != null && v.Settlement != null && v.Settlement.StringId != null && _stopOf.TryGetValue(v.Settlement.StringId, out c) ? c : 0;
        }

        /// <summary>E4/11 (G5): trafieni rabunkiem i zerowaniem od ostatniej linii doby (zeruje DayLine; VillageBook.Daily czyta przed nia - to samo okno).</summary>
        internal static double HitToday { get { return _dHit[KRaid] + _dHit[KForage]; } }

        /// <summary>
        /// Spustoszenie wylaczone (MCM albo brak jednostki ludzi / przyrostu naturalnego / latek rabunku), a w ksiedze sa uchodzcy: wracaja do swoich wsi od razu
        /// (PeopleBook.RefugeesHome - ta sama stawka min(1/k_v, 0.5), z ktora ich zdjeto; k_v z dnia kalibracji wprost z ksiegi - PeopleBook.TryUnit, takze przy
        /// wylaczonej jednostce ludzi) - ludzie nie moga utknac na koncie, ktorego nikt nie obsluguje. Zabici zostaja w liczniku.
        /// </summary>
        private static void Abandon()
        {
            if (PeopleBook.RefugeeVillages == 0) return;
            var list = new List<KeyValuePair<string, double>>();
            PeopleBook.RefugeesSnapshot(list);
            var map = ById();
            double people = 0.0, hearth = 0.0; int villages = 0, left = 0;
            PopulationLaw.EnsureCalibrated();           // jak dotad przez HearthPerMan (TryUnit sam nie kalibruje)
            foreach (var kv in list)
            {
                try
                {
                    // poprawka po recenzji E4/5 (R1): k_v z dnia kalibracji wprost z ksiegi (TryUnit nie zalezy od People Unit Enabled). HearthPerMan przy
                    // wylaczonej jednostce bierze k BIEZACEJ kultury osady (asymilacja BK, przejecie przez Innych) - powrot po innej stawce niz zdjecie = hearth
                    // z niczego albo w nicosc, a kultura spoza tabeli (whitewalker) = 0 i uchodzcy na koncie na zawsze
                    Village v; float k;
                    float per = map.TryGetValue(kv.Key, out v) && v != null && v.Settlement != null && PeopleBook.TryUnit(v.Settlement, out k) && k > 0f && !float.IsInfinity(k)
                                ? 1f / k : 0f;
                    if (!(per > 0f) || float.IsInfinity(per)) { left++; continue; }
                    per = Math.Min(per, PeopleUnit.GamePerMan);           // ta sama stawka, z ktora Strike ich zdjal (k ponizej 2: 0.5)
                    double got = PeopleBook.RefugeesHome(v, kv.Value, PeopleBook.MRefugeeHome, per);
                    if (!(got > 0.0)) { left++; continue; }
                    people += got; hearth += got * per; villages++;
                    _dBack += got; _dBackVillages++;
                }
                catch (Exception e) { left++; Stumble("Devastation.Abandon", e); }
            }
            _dAbandonLeft = left;
            if (villages > 0 || (left > 0 && !_abandonLogged))
            {
                _abandonLogged = true;
                var s = Settings.Current;
                Log.Info("Ludzie (spustoszenie): WYLACZONE (" + (s != null && !s.DevastationEnabled ? "Devastation Enabled w MCM" : "wymaga People Unit Enabled i Natural Growth Enabled oraz wpietych latek rabunku")
                         + ") - " + F0(people) + " uchodzcow z " + villages + " wsi wrocilo do domu od razu (+" + F(hearth, "0.###") + " hearth)"
                         + (left > 0 ? "; " + left + " wsi bez przelicznika ludzi zostaje na koncie" : "")
                         + ". Odtad jak przed E4/5: rabunek gry, zerowanie armii w hearth, +0.5 hearth dziennie ponizej progu.");
            }
        }

        private static string F(double v, string fmt) { return v.ToString(fmt, Inv); }
        private static string F0(double v) { return Math.Round(v).ToString("0", Inv); }

        // ------------------------------------------------------------ linia doby (K6)
        private sealed class RegionRow { public Settlement Node; public double Away, Dead, Home; public float Worst = 1f; }
        private static readonly Dictionary<string, int> _spiral = new Dictionary<string, int>();   // region -> kolejne doby: > 25% uchodzcow i najazd < Q dob (bez zapisu; rozdz. 12 pkt 18)

        private static string NameOf(Settlement st)
        {
            if (st == null) return "?";
            try { var n = st.Name; if (n != null) return n.ToString(); } catch { }
            return st.StringId ?? "?";
        }

        private static string Sg(double v, string fmt) { return (v >= 0.0 ? "+" : "") + v.ToString(fmt, Inv); }

        /// <summary>
        /// ArmouryBehavior.OnDailyTick po PeopleLedger.Daily: linia "Ludzie (spustoszenie)" (pisana, gdy czynne i cokolwiek sie dzialo albo konto niepuste;
        /// parsuje sprawdz_logi.py etap4_e45) i zerowanie licznikow doby. Konto UCHODZCY - odcinek zamkniety w PeopleBook.Daily tej samej doby (ta sama liczba, S4).
        /// </summary>
        internal static void DayLine()
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                var s = Settings.Current;
                if (s == null || !s.LogEnabled || Campaign.Current == null) return;
                int day = (int)CampaignTime.Now.ToDays - 1;
                var seg = PeopleBook.LastSeg;
                if (!On)
                {
                    double left = PeopleBook.RefugeesWorld();
                    Log.Info("Ludzie (spustoszenie): dzien " + day + " | spustoszenie WYLACZONE (" + (!s.DevastationEnabled ? "Devastation Enabled w MCM"
                             : "wymaga People Unit Enabled, Natural Growth Enabled i obu latek rabunku") + ") - rabunek i zerowanie jak przed E4/5"
                             + "; wrocilo dzis " + F(_dBack, "0.#") + " ludzi z " + _dBackVillages + " wsi"
                             + (left > 0.0 ? "; na koncie jeszcze " + F0(left) + " uchodzcow (wroca jutro" + (_dAbandonLeft > 0 ? "; " + _dAbandonLeft + " wsi bez przelicznika" : "") + ")" : "; konto UCHODZCY puste") + ".");
                    return;
                }
                double hitAll = _dHit[KRaid] + _dHit[KForage];
                bool any = _dRaids > 0 || _dForagers > 0 || _dSteps > 0 || hitAll > 0.0 || _dBack > 0.0 || PeopleBook.RefugeeVillages > 0 || (seg.Has && (seg.N > 0.0 || seg.N0 > 0.0));
                if (!any) return;

                int today = (int)CampaignTime.Now.ToDays;
                int Q = Clamp(s.RefugeeReturnQuietDays, 0, 360);
                var map = ById();
                // konto wsi -> regiony, plon
                var full = new List<KeyValuePair<string, double[]>>();
                PeopleBook.RefugeesFull(full);
                var rows = new Dictionary<Settlement, RegionRow>();
                int refV = 0, third = 0; float worst = 1f, thirdBest = 0f; string worstName = "-"; double worstShare = 0.0;
                foreach (var kv in full)
                {
                    try
                    {
                        Village v;
                        if (!map.TryGetValue(kv.Key, out v) || v == null || v.Settlement == null) continue;
                        var node = OutlawLaw.RegionFor(v.Settlement) ?? v.Settlement;
                        RegionRow r;
                        if (!rows.TryGetValue(node, out r)) rows[node] = r = new RegionRow { Node = node };
                        r.Away += kv.Value[0]; r.Dead += kv.Value[1];
                        if (!(kv.Value[0] > 0.0)) continue;
                        refV++;
                        float m = YieldFactor(v);
                        float k = PopulationLaw.KFor(v.Settlement);
                        double home = (double)Math.Max(0f, v.Hearth) * (k > 0f ? k : 0f);
                        double share = home + kv.Value[0] > 0.0 ? kv.Value[0] / (home + kv.Value[0]) : 0.0;
                        if (m < r.Worst) r.Worst = m;
                        if (worstName == "-" || m < worst) { worst = m; worstName = NameOf(v.Settlement); worstShare = share; }
                        if (share >= 1.0 / 3.0) { third++; if (m > thirdBest) thirdBest = m; }
                    }
                    catch (Exception e) { Stumble("Devastation.DayLine(wies)", e); }
                }
                // ludnosc wsi w domu (kazda wies raz - Settlement.All podaje w ROT dwie wsie dwa razy), wsie Looted, ludzie wsi regionow z uchodzcami
                double homeAll = 0.0; int looted = 0;
                var seen = new HashSet<Village>();
                foreach (var st in Settlement.All)
                {
                    try
                    {
                        if (st == null || !st.IsVillage || st.Village == null || !seen.Add(st.Village)) continue;
                        var v = st.Village;
                        if (v.VillageState == Village.VillageStates.Looted) looted++;
                        float k = PopulationLaw.KFor(st);
                        if (!(k > 0f)) continue;
                        double h = (double)Math.Max(0f, v.Hearth) * k;
                        homeAll += h;
                        var node = OutlawLaw.RegionFor(st) ?? st;
                        RegionRow r;
                        if (rows.TryGetValue(node, out r)) r.Home += h;
                    }
                    catch (Exception e) { Stumble("Devastation.DayLine(ludnosc)", e); }
                }
                // spirala na pograniczu: region > 25% uchodzcow i najazd mlodszy niz Q dob - kolejne doby (INFO)
                int spiralNow = 0, spiral90 = 0;
                var spiralSeen = new HashSet<string>();
                foreach (var r in rows.Values)
                {
                    if (r.Node == null || r.Node.StringId == null) continue;
                    int last; bool clock = PeopleBook.LastStrike(r.Node, out last);
                    double share = r.Away / Math.Max(1.0, r.Home + r.Away);
                    if (share > 0.25 && clock && today - last < Q)
                    {
                        int sd; _spiral.TryGetValue(r.Node.StringId, out sd); _spiral[r.Node.StringId] = ++sd;
                        spiralSeen.Add(r.Node.StringId); spiralNow++;
                        if (sd > 90) spiral90++;
                    }
                }
                foreach (var key in _spiral.Keys.ToList()) if (!spiralSeen.Contains(key)) _spiral.Remove(key);
                var top = rows.Values.Where(r => r.Away > 0.0).OrderByDescending(r => r.Away / Math.Max(1.0, r.Home + r.Away)).Take(Top).ToList();

                double cap = 0.0;      // pojmani przy uderzeniu: 0 (E4/9 - pojmani z konta UCHODZCY przy koncu rabunku, odcinek "pojmani (E4/9)")
                double check = hitAll - _dDead - cap - _dFled - _dAway;
                var sb = new StringBuilder(3000);
                sb.Append("Ludzie (spustoszenie): dzien ").Append(day).Append(" | CZYNNE");
                sb.Append(" | rabunki zakonczone ").Append(_dRaids).Append(" (spalone ").Append(_dRaidsFull).Append(", przerwane ").Append(_dRaids - _dRaidsFull).Append(')')
                  .Append(", napastnikow srednio ").Append(_dRaids > 0 ? F((double)_dRaidMen / _dRaids, "0") : "-").Append(", krokow gry ").Append(_dSteps)
                  .Append("; osobodni lupienia ").Append(F(_dDays[KRaid], "0.#")).Append(" x ").Append(F(Math.Max(0f, s.DevastationPerRaiderDay), "0.##"))
                  .Append(" -> trafieni ").Append(F(_dHit[KRaid], "0.#")).Append("; gra zdjelaby ").Append(F(_dGameHearth, "0.##")).Append(" hearth = ").Append(F0(_dGamePeople))
                  .Append(" ludzi ksiegi; lup z niczego ").Append(_dLootAi + _dLootPlayer).Append(" zl (gracz ").Append(_dLootPlayer).Append(')');
                sb.Append(" | zerowanie: partii ").Append(_dForagers).Append(", osobodni ").Append(F(_dDays[KForage], "0")).Append(" x ").Append(F(Math.Max(0f, s.DevastationPerForagerDay), "0.##"))
                  .Append(" -> trafieni ").Append(F(_dHit[KForage], "0.#"));
                sb.Append(" | okreg nasycony ").Append(_dFull[KRaid] + _dFull[KForage]).Append(" razy (pulap ").Append(Math.Max(0, Math.Min(95, s.DevastationMaxPercent))).Append("% albo dno 11 hearth)");
                sb.Append(" | trafieni ").Append(F(hitAll, "0.##")).Append(" = zabici ").Append(F(_dDead, "0.##")).Append(" + pojmani ").Append(F(cap, "0"))
                  .Append(" + las ").Append(F(_dFled, "0.##")).Append(" + uchodzcy ").Append(F(_dAway, "0.##")).Append(" (kontrola ").Append(Sg(check, "0.000")).Append(')')
                  .Append(" [najezdzcy z niewola: trafieni ").Append(F(_dSlaverHit, "0.#")).Append(", zabici ").Append(F(_dSlaverDead, "0.#")).Append(", pojmani byloby ").Append(F(_dCapWould, "0.#")).Append(']')
                  .Append(" w ").Append(_dVillages[KRaid].Count).Append(" wsiach rabunku i ").Append(_dVillages[KForage].Count).Append(" zerowania");
                if (CaptivesLive)
                    sb.Append(" | pojmani (E4/9): rabunkow wygranych z pojmanymi ").Append(_dCapRaids).Append(", z uchodzcow wsi do lochu najezdzcy +").Append(F(_dCapJ, "0"))
                      .Append(" (wyliczone ").Append(Math.Max(0, Math.Min(100, s.DevastationCaptivesPercent))).Append("% trafionych przy niewoli ").Append(F(_dCapPlan, "0.#"))
                      .Append("; nie wzieci - zostaja uchodzcami: gracz zostaw ").Append(F(_dCapLeave, "0.#")).Append(", bez stanu rabunku ").Append(_dCapNoState)
                      .Append(", za malo uchodzcow ").Append(F(_dCapFew, "0")).Append(", bez postaci ").Append(_dCapNoChar).Append("; nie z niewola ").Append(_dCapNotSlaver)
                      .Append(", gracz ").Append(F(_dCapPlayer, "0")).Append(", wolan BK ").Append(_dCapCalls).Append("); zerowanie armii bez pojmanych");
                else sb.Append(" | pojmani (E4/9): WYLACZONE (Raid Captives From Village) - jency z rabunku BK z niczego jak dotad");
                sb.Append(" | powrot: ludzi ").Append(F(_dBack, "0.#")).Append(" do ").Append(_dBackVillages).Append(" wsi, regionow w fazie powrotu ").Append(_dBackRegions.Count)
                  .Append("; wstrzymany: ").Append(Q).Append(" dob spokoju ").Append(_dStopQuiet).Append(" wsi, oblezenie ").Append(_dStopSiege).Append(" wsi")
                  .Append(", wies nie przyjmuje - glod gospodarzy, a konto w innym regionie takze pole nie karmi i spichlerz ponizej ").Append(Math.Max(0, s.FamineReturnMinDays)).Append(" dni dla gospodarzy, albo czesc z glodu czeka na odzyskanie wsi (E4/6) ").Append(_dStopHunger).Append(" wsi")
                  .Append("; kontrola liniowosci max ").Append(_dLinErr.ToString("0.######", Inv));
                if (_dNoClock > 0) sb.Append("; bez zegara regionu ").Append(_dNoClock).Append(" wsi");
                if (seg.Has)
                {
                    sb.Append(" | konto UCHODZCY: ");
                    if (seg.First) sb.Append("pierwsza doba sesji (kontrola od jutra): nowi ").Append(F(seg.In, "0.#")).Append(" - powrot ").Append(F(seg.Home, "0.#"))
                                     .Append(" - podloga 10 ").Append(F(seg.Floor, "0.#")).Append(" - zmarli ").Append(F(seg.Died, "0.###")).Append(" - pojmani ").Append(F(seg.Capt, "0.#")).Append(" = ").Append(F(seg.N, "0.#"));
                    else sb.Append("wczoraj ").Append(F(seg.N0, "0.#")).Append(" + nowi ").Append(F(seg.In, "0.#")).Append(" - powrot ").Append(F(seg.Home, "0.#"))
                           .Append(" - podloga 10 ").Append(F(seg.Floor, "0.#")).Append(" - zmarli ").Append(F(seg.Died, "0.###")).Append(" - pojmani ").Append(F(seg.Capt, "0.#")).Append(" = ").Append(F(seg.N, "0.#"))
                           .Append(" (kontrola ").Append(Sg(seg.Check, "0.000")).Append(')');
                    sb.Append(" w ").Append(seg.Villages).Append(" wsiach (smierc ").Append(F(Math.Max(0f, s.RefugeeYearlyLossPercent), "0.##")).Append("%/rok, zmarli dzis w tej linii ").Append(F(_dDied, "0.###")).Append(')');
                }
                else sb.Append(" | konto UCHODZCY: brak odcinka doby (PeopleBook.Daily nie zamknal doby)");
                double n = seg.Has ? seg.N : PeopleBook.RefugeesWorld();
                sb.Append(" | ludnosc wsi w domu ").Append((homeAll / 1e6).ToString("0.000", Inv)).Append(" + uchodzcy ").Append(F0(n)).Append(" = razem ")
                  .Append(((homeAll + n) / 1e6).ToString("0.000", Inv)).Append(" mln");
                sb.Append(" | plon: wsi z uchodzcami ").Append(refV).Append(", najnizszy mnoznik ").Append(worst.ToString("0.####", Inv)).Append(" (").Append(worstName)
                  .Append(", udzial uchodzcow ").Append(worstShare.ToString("0.####", Inv)).Append("), wsi z >= 1/3 uchodzcow ").Append(third);
                if (third > 0) sb.Append(" (najwyzszy mnoznik wsrod nich ").Append(thirdBest.ToString("0.####", Inv)).Append(')');
                sb.Append(" | wsie spladrowane: Looted dzis ").Append(looted).Append(", produkowaly ").Append(_dLootedMade).Append(" (sztuk ").Append(_dLootedPieces)
                  .Append("; podmian stanu ").Append(_dLootedOn).Append(", bramka magazynu 1.5 W zamknieta ").Append(_dLootedGate)
                  .Append(" (towar czeka na woz - woz rusza tylko ze wsi Normal), bez sztuk przy otwartej bramce ").Append(Math.Max(0, _dLootedOn - _dLootedGate - _dLootedMade)).Append(')')
                  .Append(LootedOn ? "" : " [produkcja wsi spladrowanej wylaczona: " + (!_lootedPatched ? "latka nie wpieta" : "Looted Produces By People") + "]");
                if (_dLootedChanged > 0) sb.Append(", stan zmieniony w srodku ").Append(_dLootedChanged);
                if (top.Count > 0)
                {
                    var parts = new List<string>();
                    foreach (var r in top)
                    {
                        int last; bool clock = PeopleBook.LastStrike(r.Node, out last);
                        parts.Add(NameOf(r.Node) + " [" + (r.Node.IsTown ? "miasto" : (r.Node.IsCastle ? "zamek" : "wies")) + ", " + PeopleBook.CultureAtCalibration(r.Node) + "] uchodzcy "
                                  + F0(r.Away) + " z " + F0(r.Home + r.Away) + " (" + F(100.0 * r.Away / Math.Max(1.0, r.Home + r.Away), "0.##") + "%), dni od najazdu "
                                  + (clock ? (today - last).ToString(Inv) : "-") + ", plon x" + r.Worst.ToString("0.####", Inv) + ", zabici od poczatku " + F0(r.Dead));
                    }
                    sb.Append(" | regiony najbardziej spustoszone (").Append(top.Count).Append("): ").Append(string.Join("; ", parts.ToArray()));
                }
                sb.Append(" | spirala na pograniczu (> 25% uchodzcow i najazd < ").Append(Q).Append(" dob): regionow dzis ").Append(spiralNow).Append(", od ponad 90 dob ").Append(spiral90);
                sb.Append(" | bez przelicznika ").Append(_dNoK).Append(" | hearth dopisany przez kogos w srodku kroku rabunku ").Append(_dForeign)
                  .Append(" | rabunki podjete po wczytaniu ").Append(_dResumed);
                if (_dMoveFail > 0) sb.Append(" | Move nie przyjal ").Append(_dMoveFail);
                if (_dNoVillage > 0) sb.Append(" | konto bez wsi na mapie ").Append(_dNoVillage);
                sb.Append(" | potkniecia ").Append(_stumbles).Append(" (razem ").Append(_stumblesAll).Append(')');
                sb.Append(" | czas ").Append(((System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency).ToString("0.0", Inv)).Append(" ms.");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Devastation.DayLine", e); }
            finally { NewDay(); }
        }

        // ------------------------------------------------------------ latki
        private static readonly List<string> _wired = new List<string>(), _missing = new List<string>();

        internal static void ApplyAll(Harmony h)
        {
            if (h == null) return;
            _wired.Clear(); _missing.Clear();
            try
            {
                var upd = AccessTools.Method(typeof(RaidEventComponent), "Update", new[] { typeof(bool).MakeByRefType() });
                if (upd != null && upd.DeclaringType == typeof(RaidEventComponent))
                {
                    h.Patch(upd, prefix: new HarmonyMethod(typeof(Devastation), nameof(RaidPrefix)), finalizer: new HarmonyMethod(typeof(Devastation), nameof(RaidFinalizer)));
                    _raidPatched = true; _wired.Add("krok rabunku");
                }
                else _missing.Add("RaidEventComponent.Update");
            }
            catch (Exception e) { _missing.Add("RaidEventComponent.Update (" + e.GetType().Name + ")"); Log.Error("Devastation.ApplyAll(Update)", e); }
            try
            {
                var end = AccessTools.Method(typeof(RaidEventComponent), "OnBeforeFinalize");
                if (end != null && end.DeclaringType == typeof(RaidEventComponent) && end.GetParameters().Length == 0)
                {
                    h.Patch(end, prefix: new HarmonyMethod(typeof(Devastation), nameof(EndPrefix)), finalizer: new HarmonyMethod(typeof(Devastation), nameof(EndFinalizer)));
                    _endPatched = true; _wired.Add("koniec rabunku");
                }
                else _missing.Add("RaidEventComponent.OnBeforeFinalize");
            }
            catch (Exception e) { _missing.Add("RaidEventComponent.OnBeforeFinalize (" + e.GetType().Name + ")"); Log.Error("Devastation.ApplyAll(OnBeforeFinalize)", e); }
            try
            {
                var lvl = AccessTools.Method(typeof(Village), "GetHearthLevel");
                if (lvl != null && lvl.ReturnType == typeof(int) && lvl.GetParameters().Length == 0)
                {
                    h.Patch(lvl, postfix: new HarmonyMethod(typeof(Devastation), nameof(LevelPostfix)));
                    _levelPatched = true; _wired.Add("progi produkcji");
                }
                else _missing.Add("Village.GetHearthLevel");
            }
            catch (Exception e) { _missing.Add("Village.GetHearthLevel (" + e.GetType().Name + ")"); Log.Error("Devastation.ApplyAll(GetHearthLevel)", e); }
            try
            {
                var food = AccessTools.Method(typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultVillageProductionCalculatorModel), "CalculateDailyFoodProductionAmount", new[] { typeof(Village) });
                if (food != null && food.ReturnType == typeof(float))
                {
                    h.Patch(food, postfix: new HarmonyMethod(typeof(Devastation), nameof(FoodPostfix)));
                    _foodPatched = true; _wired.Add("zywnosc wsi");
                }
                else _missing.Add("DefaultVillageProductionCalculatorModel.CalculateDailyFoodProductionAmount");
            }
            catch (Exception e) { _missing.Add("CalculateDailyFoodProductionAmount (" + e.GetType().Name + ")"); Log.Error("Devastation.ApplyAll(zywnosc)", e); }
            try
            {
                _mainThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
                var tp = AccessTools.Method(typeof(VillageGoodProductionCampaignBehavior), "TickProductions", new[] { typeof(Settlement), typeof(bool) });
                if (tp != null)
                {
                    _stateRef = AccessTools.FieldRefAccess<Village, Village.VillageStates>("_villageState");
                    h.Patch(tp, prefix: new HarmonyMethod(typeof(Devastation), nameof(LootedPrefix)) { priority = Priority.First },
                                finalizer: new HarmonyMethod(typeof(Devastation), nameof(LootedFinalizer)));
                    _lootedPatched = true; _wired.Add("produkcja wsi spladrowanej");
                }
                else _missing.Add("VillageGoodProductionCampaignBehavior.TickProductions");
            }
            catch (Exception e) { _stateRef = null; _missing.Add("TickProductions (" + e.GetType().Name + ")"); Log.Error("Devastation.ApplyAll(TickProductions)", e); }
            StartLine();
        }

        private static void StartLine()
        {
            // stan z ustawien startowych; MCM wchodzi przy starcie kampanii - prawdziwy stan stoi co dobe w linii "Ludzie (spustoszenie)"
            var s = Settings.Current;
            string state = On ? "CZYNNE" : (s != null && !s.DevastationEnabled ? "wylaczone w ustawieniach"
                                         : "NIECZYNNE - wymaga People Unit Enabled, Natural Growth Enabled i obu latek rabunku");
            var sb = new StringBuilder(900);
            sb.Append("Devastation (E4/5): spustoszenie jako ulamek okregu ").Append(state);
            if (s != null)
            {
                sb.Append(" - rabunek ").Append(F(Math.Max(0f, s.DevastationPerRaiderDay), "0.##")).Append(" i zerowanie ").Append(F(Math.Max(0f, s.DevastationPerForagerDay), "0.##"))
                  .Append(" czlowieka na zbrojnego na dobe, pulap ").Append(Math.Max(0, Math.Min(95, s.DevastationMaxPercent))).Append("%, zabici ").Append(s.DevastationKilledPercent)
                  .Append("% (przy niewoli ").Append(s.DevastationKilledSlaverPercent).Append("%), las ").Append(s.DevastationOutlawBasePercent).Append("% + ").Append(s.DevastationOutlawDangerPercent)
                  .Append("% x niebezpieczenstwo, pojmani przy uderzeniu 0 (E4/9: przy koncu wygranego rabunku ").Append(s.DevastationCaptivesPercent).Append("% trafionych przy niewoli z uchodzcow wsi do lochu najezdzcy - Raid Captives From Village ").Append(s.RaidCaptivesFromVillage ? "TAK" : "NIE").Append("), powrot po ").Append(s.RefugeeReturnQuietDays)
                  .Append(" dobach spokoju liniowo do ").Append(s.RefugeeReturnDays).Append(" dob od ostatniego najazdu w regionie (oblezenie wstrzymuje), smierc uchodzcow ")
                  .Append(F(Math.Max(0f, s.RefugeeYearlyLossPercent), "0.##")).Append("%/rok, plon ^").Append(F(Math.Max(0f, Math.Min(2f, s.DevastationYieldElasticity)), "0.##"))
                  .Append(", wsie spladrowane produkuja: ").Append(s.LootedProducesByPeople ? "TAK" : "NIE");
            }
            sb.Append("; wpiete: ").Append(_wired.Count > 0 ? string.Join(", ", _wired.ToArray()) : "nic")
              .Append("; BRAK: ").Append(_missing.Count > 0 ? string.Join(", ", _missing.ToArray()) : "-")
              .Append(_levelPatched && _foodPatched ? "" : " (bez latki progow albo zywnosci mnoznik plonu dziala tylko w produkcji towarow)").Append('.');
            Log.Info(sb.ToString());
        }
    }
}
