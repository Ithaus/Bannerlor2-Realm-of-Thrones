using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
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
    ///    niewoli - CZYNNI DOPIERO Z E4/9 (do tego czasu 0 - zostaja uchodzcami; BK daje swoich jencow z rabunku z niczego, U14), las (2 + 3 x niebezpieczenstwo)% do puli
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

        private static bool _raidPatched, _endPatched;   // latki wpiete (raz na proces)

        /// <summary>Spustoszenie czynne: klucz, obie latki rabunku, jednostka ludzi (E4/3) i przyrost naturalny (E4/4).</summary>
        internal static bool On
        {
            get
            {
                var s = Settings.Current;
                return s != null && s.DevastationEnabled && _raidPatched && _endPatched && PeopleUnit.On && PopulationLaw.GrowthOn;
            }
        }

        /// <summary>E4/9 podmieni na klucz RaidCaptivesFromVillage; do tego czasu pojmani = 0 (U14 - BK dalej daje swoich jencow z niczego).</summary>
        internal static bool CaptivesLive { get { return false; } }

        // ------------------------------------------------------------ rabunek w toku (bez zapisu)
        private sealed class RaidState
        {
            public double PersonDays;                   // osobodni lupienia od ostatniego rozliczenia
            public double TotalDays, Hit, Dead, CapWould;   // ten rabunek razem: osobodni, trafieni, zabici, pojmani "byloby" (E4/9)
            public float Virt; public bool HasVirt;     // hearth, jaki wies mialaby w grze po dotychczasowych krokach rabunku
            public int Steps, MaxMen;
            public bool Slaver;                         // najezdzca z kultury z niewola (LosersFlee.SlaveCulture - ta sama regula co H3)
        }
        private static readonly object _lock = new object();
        private static readonly Dictionary<RaidEventComponent, RaidState> _raids = new Dictionary<RaidEventComponent, RaidState>();
        private static Dictionary<string, Village> _byId;           // id osady -> wies (raz na kampanie)

        /// <summary>Stan wsi miedzy prefiksem a finalizerem latek rabunku.</summary>
        internal struct Tick { public Village V; public float Real, Virt; public bool Swapped; public object Rs; public double K; public Hero Leader; public int Gold; }

        // ------------------------------------------------------------ liczniki doby (zeruje DayLine)
        private static readonly double[] _dHit = new double[2], _dDays = new double[2];
        private static readonly int[] _dFull = new int[2];
        private static readonly HashSet<string>[] _dVillages = { new HashSet<string>(), new HashSet<string>() };
        private static double _dDead, _dFled, _dAway, _dCapWould, _dSlaverHit, _dSlaverDead, _dGameHearth, _dGamePeople;
        private static long _dLootAi, _dLootPlayer;
        private static int _dSteps, _dRaids, _dRaidsFull, _dRaidMen, _dResumed, _dForagers, _dNoK, _dMoveFail, _dForeign;
        private static int _stumbles, _stumblesAll;
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        internal static void Reset()
        {
            lock (_lock) { _raids.Clear(); _byId = null; }
            lock (_errSites) _errSites.Clear();
            _stumblesAll = 0;
            NewDay();
        }

        internal static void NewDay()
        {
            Array.Clear(_dHit, 0, 2); Array.Clear(_dDays, 0, 2); Array.Clear(_dFull, 0, 2);
            _dVillages[0].Clear(); _dVillages[1].Clear();
            _dDead = _dFled = _dAway = _dCapWould = _dSlaverHit = _dSlaverDead = _dGameHearth = _dGamePeople = 0.0;
            _dLootAi = _dLootPlayer = 0;
            _dSteps = _dRaids = _dRaidsFull = _dRaidMen = _dResumed = _dForagers = _dNoK = _dMoveFail = _dForeign = 0;
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
        private static double MaxShare(Settings s) { return Math.Max(0, Math.Min(95, s.DevastationMaxPercent)) / 100.0; }

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
        private static double Strike(Village v, double personDays, double perDay, int kind, bool slaver, RaidState rs)
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
            double capW = slaver ? Clamp01(s.DevastationCaptivesPercent / 100.0) : 0.0;   // "byloby" - log, E4/9
            double cap = CaptivesLive ? capW : 0.0;
            double flee = 0.0;
            if (OutlawLaw.On)                                 // bez prawa wyrzutkow puli nikt nie prowadzi - zostaja uchodzcami
                flee = Clamp01((Math.Max(0, s.DevastationOutlawBasePercent) + Math.Max(0, s.DevastationOutlawDangerPercent) * DangerOf(v)) / 100.0);
            if (kill + cap > 1.0) cap = Math.Max(0.0, 1.0 - kill);
            if (kill + cap + flee > 1.0) flee = Math.Max(0.0, 1.0 - kill - cap);
            double dead = Math.Min(hit * kill, personDays);   // [113 KRYT 8] najwyzej 1 zabity na zbrojnego na dobe; nadwyzka zostaje uchodzcami
            double capt = hit * cap, fled = hit * flee;
            double refugees = hit - dead - capt - fled;
            if (refugees < 0.0) refugees = 0.0;
            // JEDYNY ruch hearth (U23): trafieni z DOM
            double moved = -PeopleBook.Move(v, PeopleBook.ADom, PeopleBook.ARefugees, hit, kind == KRaid ? PeopleBook.MDevRaid : PeopleBook.MDevForage, PeopleUnit.GamePerMan);
            if (!(moved > 0.0)) { _dMoveFail++; return 0.0; }         // Move nie rzuca; 0 = wyjatek w Shift - nikogo nie przenosimy
            double f = Math.Min(1.0, moved * k / hit);                  // gdyby Move przyjal mniej - wszystkie konta w tej samej proporcji
            var region = OutlawLaw.RegionFor(v.Settlement);
            int day = (int)CampaignTime.Now.ToDays;
            PeopleBook.Devastated(v, refugees * f, dead * f, region, day);
            if (fled > 0.0) OutlawLaw.AddFled(v.Settlement, (float)(fled * f));   // pula regionu 1:1, bez hearth (zdjal Move)
            // pojmani (od E4/9): capt * f -> konto JENCY; w E4/5 capt == 0
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
                Strike(v, men, Math.Max(0f, s.DevastationPerForagerDay), KForage, SlaverOf(mp.MapFaction), null);
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
                rs = new RaidState();
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
                        long gold = (long)__state.Leader.Gold - __state.Gold;
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

        /// <summary>Pojmani tego rabunku (E4/9: BK ExecuteCapture = dokladnie te osoby); w E4/5 zawsze 0.</summary>
        internal static double RaidCaptives(RaidEventComponent r)
        {
            if (r == null || !CaptivesLive) return 0.0;
            lock (_lock) { RaidState rs; return _raids.TryGetValue(r, out rs) ? rs.CapWould : 0.0; }
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

        private static string F(double v, string fmt) { return v.ToString(fmt, Inv); }
        private static string F0(double v) { return Math.Round(v).ToString("0", Inv); }

        // ------------------------------------------------------------ linia doby
        /// <summary>ArmouryBehavior.OnDailyTick po PeopleLedger.Daily: linia "Ludzie (spustoszenie)" i zerowanie licznikow doby.</summary>
        internal static void DayLine()
        {
            try { }
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
                  .Append("% x niebezpieczenstwo, pojmani 0 (od E4/9; przy niewoli byloby ").Append(s.DevastationCaptivesPercent).Append("%), powrot po ").Append(s.RefugeeReturnQuietDays)
                  .Append(" dobach spokoju liniowo do ").Append(s.RefugeeReturnDays).Append(" dob od ostatniego najazdu w regionie (oblezenie wstrzymuje), smierc uchodzcow ")
                  .Append(F(Math.Max(0f, s.RefugeeYearlyLossPercent), "0.##")).Append("%/rok, plon ^").Append(F(Math.Max(0f, Math.Min(2f, s.DevastationYieldElasticity)), "0.##"))
                  .Append(", wsie spladrowane produkuja: ").Append(s.LootedProducesByPeople ? "TAK" : "NIE");
            }
            sb.Append("; wpiete: ").Append(_wired.Count > 0 ? string.Join(", ", _wired.ToArray()) : "nic")
              .Append("; BRAK: ").Append(_missing.Count > 0 ? string.Join(", ", _missing.ToArray()) : "-").Append('.');
            Log.Info(sb.ToString());
        }
    }
}
