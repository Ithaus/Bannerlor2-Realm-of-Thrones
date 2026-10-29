using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>155: odbiorca paska staminy konia gracza w HUD (mixin MissionAgentStatusVM w HorseWindBarUi.cs). Zwykly interfejs - HorseWind nie dotyka typow
    /// UIExtenderEx, wiec brak tego zestawu nie wywraca taktu (TypeLoadException).</summary>
    internal interface IHorseWindBar
    {
        /// <summary>shown - pasek widoczny; frac 0..1 - stamina konia; low - ponizej progu zmeczenia (kon zwalnia). Glowny watek.</summary>
        void Set(bool shown, float frac, bool low);
    }

    /// <summary>155: lacznik HorseWind - pasek HUD. Sink = biezacy mixin (konstruktor VM HUD ustawia, OnFinalize zdejmuje). Liczniki NARASTAJACE na caly proces
    /// (recenzja 155, poprawka 1): oba rosna w EarlyStart misji (film HUD i VM), PRZED AfterStart, gdzie HorseWind zeruje liczniki misji - nie zerowac.</summary>
    internal static class HorseWindBarLink
    {
        internal static volatile IHorseWindBar Sink;
        internal static int Applied;     // nalozenia wstawki AgentStatus od startu gry (getter Nodes - wolany tylko po trafieniu xpath)
        internal static int MixinMade;   // mixiny MissionAgentStatusVM utworzone od startu gry
    }

    /// <summary>
    /// 136 (25.10, Jeff ok. 12:48: "i konie tez powinny miec stamine, ze nie moge ciagle biegac po polu walki, stamina powinna byc zalezna od cech konia";
    /// wczesniej "ogolna zasada" - gracz i AI tak samo, "wyjsc od sredniej"; ok. 13:30 "przeciwnik ... walczy o zycie"). STAMINA KONI W BITWIE.
    ///
    /// Stan nalezy do KONIA (agent wierzchowca; przesiadka na swiezego konia = pelna stamina), pula S 0..100. Model z rozpoznania 'fizjologia' sekcja 7
    /// (moc krytyczna i rezerwa beztlenowa: Prince 2002, Weber 1987, Taylor 1980, Powell 2008, Stefansdottir 2017):
    ///  - co 0.25 s na konia z jezdzcem: v = droga pozioma / dt (pozycja konia; v > 1.5 Vbase odrzucone - teleport smoka, odrzut), u = v / Vbase;
    ///    Vbase = MountSpeed swiezo z modelu (po RBM, BEZ naszej kary) - NIGDY GetMaximumForwardUnlimitedSpeed (spada razem z kara = petla: wyczerpany kon
    ///    na swoim obnizonym maksimum wygladalby na pelny galop i nigdy by nie odpoczal);
    ///  - zapotrzebowanie D(u) = 0.15 + 0.60u + 0.70u^2 (w jednostkach VO2max: step 0.23, klus 0.45, canter 0.63, pelny galop 1.45);
    ///  - obciazenie r = (75 + zbroja jezdzca + bron jezdzca + uprzaz_norm) / waga konia, uprzaz_norm = waga do 40 kg, ponad 40 kg x HorseHarnessHeavyScale
    ///    (uprzeze ROT i gry sa ok. 5 razy za ciezkie: mail_and_plate_barding 145 wobec RBM 26.8, "siodlo" rot_horse_saddle2 100 kg);
    ///    f = (1+r)/1.20 x (1 + 1.5 x max(0, r-0.25)) (Taylor: koszt proporcjonalny do masy; Powell / Stefansdottir: przeciazenie ponad ok. 25%);
    ///  - cechy konia: W' = 44 x clamp(Speed/50, 0.85, 1.35) (szybszy kon ma wiekszy zapas - Prince), CP = 0.75 (+0.05 rodziny wytrzymale - stepowe,
    ///    pustynne, wielblad; -0.05 kon od 500 kg), tau = HorseRecoverySeconds x sqrt(waga/450); jezdziec: drenaz x (1.15 - 0.30 x min(Riding,300)/300);
    ///  - D x f ponad CP: S -= 100 x (D x f - CP) / W' x dt x HorseGallopDrainScale x jezdziec; D x f ponizej CP: S += (100 - S) / tau x g x dt,
    ///    g = clamp((CP - D x f) / (CP - 0.23), 0, 1); kon bez jezdzca tylko odpoczywa (jak stepem);
    ///  - mnoznik predkosci m = S &lt; 30 ? HorseTiredFloor + (1 - HorseTiredFloor) x S/30 : 1.
    /// Przecietny kon (r 20%) pelnym galopem do zera ok. 60 s, rycerz z bardem (r 35%) ok. 40 s; step i prawdziwy klus (u ok. 0.3, ok. 3.5-4 m/s) to odpoczynek.
    /// Paczka 203 (Jeff 26.10 ok. 22:15: "kon za szybko sie meczy, wydluz x2 ilosc staminy lub zmniejsz o 50% zuzycie staminy konia w galopie"): domyslne
    /// HorseGallopDrainScale 1 -> 0.5 (Armoury.json Jeffa klucza nie ma) - drenaz ponad CP o polowe, wiec galop do zera x2 (przecietny ok. 120 s, rycerz z bardem
    /// ok. 80 s, kon gracza t3_empire_horse 46 -> 92 s); odpoczynek (tau, g, HorseRecoverySeconds) i prog zmeczenia bez zmian. Linie: start i koniec bitwy.
    /// Recenzja 136: "klus" podejscia konnicy w MarchToContact (CavalryApproachPace 0.45 x pelna, z marginesem linii u ok. 0.5) to w modelu canter - typowy kon ROT
    /// (vlandia_horse 425 kg, "siodlo" 100 kg, r ok. 0.37, f ok. 1.34) traci na nim powoli (D x f ok. 0.76-0.83 &gt; CP 0.75). Dlatego odpoczynek konnicy AI ma wlasne tempo:
    /// RestSpeed(szyk) = srednia z koni szyku u_odp x Vbase, gdzie u_odp daje g = 0.5 przy obciazeniu konia (D(u) x f = 0.23 + 0.5 x (CP - 0.23); u 0.12..0.35).
    ///
    /// Jak kara trafia do silnika (dowod w rozpoznaniu, krytyk pkt 1): przez AgentDrivenProperties.MountSpeed konia - ta sama droga, ktora gra sama
    /// zmienia predkosc konia w bitwie (BannerBearerLogic: mount.UpdateAgentProperties przy chorazych), a RBM AdjustSpeedLimitPatch wpisuje konnicy AI
    /// limit = MountSpeed co ok. 0.5 s. Kon jest zawsze agentem AI (takze pod graczem), wiec ta sama droga obejmuje gracza i AI.
    ///  (1) postfiks SandboxAgentStatCalculateModel.UpdateAgentStats (AccessTools.DeclaredMethod - metoda zadeklarowana w tej klasie; biegnie po
    ///      UpdateHorseStats i postfiksie RBM na nim): dla konia, ktorego liczymy, zapis swiezej Vbase i MountSpeed = Vbase x m (idempotentnie - zawsze
    ///      swieza wartosc z modelu). Kara przetrwa przeliczenia gry (chorazowie) i natywny callback. Callback [MBCallback(null, false)] = tylko glowny
    ///      watek; obcy watek liczony i pomijany (slownik pisze tylko glowny watek). Bez alokacji, logu i Settings.Current.
    ///  (2) tick (0.25 s): gdy m zmienia sie o krok >= 0.03 (albo wraca do 1) - adp.MountSpeed = Vbase x m i UpdateCustomDrivenProperties (bez lancucha
    ///      modeli), najwyzej 16 zapisow na tick, kolejne od miejsca, gdzie skonczyl sie limit (bez glodzenia).
    /// Zadnego SetMaximumSpeedLimit na koniu - konie uwiazane w obozie (CampWalk: limit 0, Controller None) nigdy nie maja jezdzca, wiec tu nie trafiaja.
    ///
    /// Wykluczenia (bez staminy, m = 1): smoki (takze dragonfly i forma latajaca), rydwany, slonie i mamuty, konie umarlych (wight_horse, kultura wights /
    /// walkers) i jezdziec umarly (Undead.Character). Wielblady i muly liczone (wielblad - rodzina wytrzymala).
    /// Tylko bitwa na mapie (MapEvent.PlayerMapEvent, rodzaj misji bitwa / kryjowka), nie turniej, arena ani morze.
    /// Gracz: komunikaty (zmeczenie, zajechany, odzyskal oddech; kazdy najwyzej co 10 s). AI: odpoczynek konnicy w MarchToContact (AvgStamina).
    /// Linie: "Konie (136): latka ..." raz na proces (pierwsza bitwa) i "Konie (136): ..." na koniec bitwy.
    /// 155: pasek staminy konia gracza w HUD - PublishBar na koncu kazdego taktu (po sredniach szykow) wpisuje S konia gracza do IHorseWindBar (HorseWindBarLink.Sink);
    /// tylko odczyt slownika w tym samym watku i takcie, S ani kary nie zmienia. Klucz HorseStaminaBar NIE = pasek ukryty.
    /// Wyjatek przy koniu = licznik potkniec (CLAUDE.md: licz, nie gas), funkcja zostaje wlaczona.
    /// </summary>
    internal sealed class HorseWind : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        internal const string HarmonyId = "com.jeff.armoury.horsewind";
        private const float Step = 0.25f;            // takt modelu (s)
        private const int WritesPerTick = 16;        // najwyzej tyle zapisow MountSpeed na takt
        private const float MinStep = 0.03f;         // zapis dopiero przy zmianie mnoznika o tyle
        private const float TiredBelow = 30f;        // ponizej - kon zwalnia (m < 1)
        private const float WindBackAbove = 60f;     // powyzej - "odzyskal oddech" (komunikat gracza)
        private const float MsgGap = 10f;            // komunikat gracza najwyzej co tyle sekund (kazdy osobno)
        private const float RiderKg = 75f;           // jezdziec bez rynsztunku (fizjologia: 70-75 kg)
        private const float HarnessFreeKg = 40f;     // uprzaz do tej wagi liczona w calosci
        private const float FullGallopD = 1.45f;     // D(1) - pelny galop
        private const float RestG = 0.5f, RestUMin = 0.12f, RestUMax = 0.35f;   // tempo odpoczynku konnicy AI: g = 0.5, od stepu do klusa
        private const float StableFor = 1f;          // dowod T1: probka liczona, gdy mnoznik w silniku stoi od tylu sekund (kon zdazyl zwolnic)
        private const float T1MaxM = 0.85f;          // dowod T1: tylko przy m do tyle (1/m wyraznie ponad 1)
        private const int MaxTeams = 16, Stride = 10, Slots = MaxTeams * Stride;

        // rodzaje wykluczen (indeksy licznikow linii)
        private const byte ExNone = 0, ExDragon = 1, ExChariot = 2, ExBig = 3, ExDead = 4, ExOther = 5;
        private const int ExKinds = 6;

        private sealed class Horse
        {
            public Agent Rider;          // ostatni jezdziec (zmiana = obciazenie i jezdziec od nowa)
            public byte Ex;              // wykluczenie (ExNone = liczony)
            public bool Counted;         // policzony do "koni z jezdzcem"
            public float S = 100f;       // pula 0..100
            public float M = 1f;         // biezacy mnoznik predkosci (czyta postfiks)
            public float MWritten = 1f;  // mnoznik, ktory siedzi teraz w MountSpeed silnika
            public float MAt = -1000f;   // czas misji ostatniej zmiany MWritten (zapis w takcie albo postfiks)
            public float Vbase = -1f;    // MountSpeed z modelu bez naszej kary (m/s wedlug gry)
            public float RestU = 0.3f;   // tempo odpoczynku (u) przy obciazeniu tego konia - dla konnicy AI (RestSpeed)
            public string Id = "?";
            public float Kg = 450f, HarnessRaw, Harness;
            public float Wprim = 44f, CP = 0.75f, Tau = 120f;
            public float R, F = 1f, RideMul = 1f;
            public bool HasPos;
            public float PX, PY;
            public bool Blown;           // zajechany do zera (licznik raz na konia)
            public bool SaidTired, SaidBlown;   // komunikaty gracza od ostatniego odpoczynku
        }

        // ---- stan misji (glowny watek)
        private readonly Dictionary<Agent, Horse> _horses = new Dictionary<Agent, Horse>();
        private bool _on;                // bitwa na mapie (nie turniej, arena, morze) - liczone w AfterStart
        private bool _done;              // linia misji wypisana
        private float _acc;
        private int _rot;                // od ktorego agenta zaczac nastepny takt (zapisy odlozone przez limit)
        private readonly float[] _msgAt = { -1000f, -1000f, -1000f };

        // ---- czytane przez postfiks (glowny watek - sprawdzane) i przez MarchToContact
        private static volatile HorseWind _live;
        private static int _mainTid = -1;
        private static float _clock;     // czas misji biezacego taktu (postfiks zapisuje nim MAt)

        // ---- srednia S i tempo odpoczynku koni szyku (dla odpoczynku konnicy AI w MarchToContact) - liczone w takcie, bez alokacji
        private static readonly float[] _fSum = new float[Slots];
        private static readonly float[] _fRestV = new float[Slots];
        private static readonly int[] _fN = new int[Slots];
        private static readonly Formation[] _fWho = new Formation[Slots];
        private static readonly float[] _avg = new float[Slots];
        private static readonly float[] _restV = new float[Slots];
        private static readonly Formation[] _avgWho = new Formation[Slots];
        private static bool _avgOn;

        // ---- latka (raz na proces)
        private static bool _patchTried, _patched;

        // ---- liczniki misji
        private static int _seen, _blownOurs, _blownFoe, _harnessScaled, _writes, _rejected, _stum, _stumSession, _aiRests, _plWrites;
        private static int _postN, _postPen, _postOff, _postErr;
        private static readonly int[] _ex = new int[ExKinds];
        private static float _rSum, _tSum, _restUSum;
        private static int _rN, _tN;
        // gracz (dowod T1: czy MountSpeed spowalnia konia gracza). Recenzja 136: zamiast maksimum (jedna odstajaca probka do 1.5 Vbase albo probka
        // tuz po obnizce, gdy kon jeszcze hamuje, dawala "silnik nie slucha") - liczba i srednia probek galopu przy mnozniku stojacym od StableFor s.
        private static Horse _pl;
        private static float _plMaxV, _plEng1, _plEngLow, _plEngLowM;
        private static int _p1N, _pLN, _pLOver;              // probki galopu: przy m = 1 / przy m <= T1MaxM; z tych drugich blizej 1/m niz 1
        private static float _p1Sum, _pLSum, _pLMSum, _pLVb;  // sumy: v/Vbase przy m = 1; v/(Vbase x m), m, v/Vbase przy m <= T1MaxM
        // 155: pasek gracza w misji (liczniki narastajace na proces - HorseWindBarLink)
        private static int _barNoSink, _barErr;
        private static float _barTime, _barMin, _barLast;

        private static void ResetCounters()
        {
            _barNoSink = _barErr = 0;
            _barTime = 0f; _barMin = -1f; _barLast = -1f;
            _seen = _blownOurs = _blownFoe = _harnessScaled = _writes = _rejected = _stum = _aiRests = _plWrites = 0;
            _postN = _postPen = 0;
            Interlocked.Exchange(ref _postOff, 0);
            Interlocked.Exchange(ref _postErr, 0);
            for (int i = 0; i < ExKinds; i++) _ex[i] = 0;
            _rSum = _tSum = _restUSum = 0f; _rN = _tN = 0;
            _pl = null;
            _plMaxV = 0f; _plEng1 = -1f; _plEngLow = -1f; _plEngLowM = 1f;
            _p1N = _pLN = _pLOver = 0;
            _p1Sum = _pLSum = _pLMSum = _pLVb = 0f;
        }

        // ------------------------------------------------------------ rejestracja i misja

        /// <summary>SubModuleMain.OnMissionBehaviorInitialize: bitwa gracza na mapie (turniej, arena, cwiczenia, spacer - nie). Rodzaj i morze sprawdza AfterStart.</summary>
        internal static bool Wanted(Mission mission)
        {
            try { return mission != null && TaleWorlds.CampaignSystem.MapEvents.MapEvent.PlayerMapEvent != null; }
            catch { return false; }
        }

        public override void AfterStart()
        {
            base.AfterStart();
            _live = null;   // poprzednia misja bez OnEndMission nie zostaje podpieta do postfiksu
            try
            {
                var m = Mission;
                string kind = MissionTeams.Kind(m);
                _on = m != null && (kind == "bitwa" || kind == "kryjowka") && !m.IsNavalBattle && !m.IsNavalRaidBattle;
                _done = false;
                _acc = 0f;
                _rot = 0;
                _horses.Clear();
                ClearAvg();
                ResetCounters();
                if (!_on) return;
                _mainTid = Thread.CurrentThread.ManagedThreadId;
                EnsurePatched();
                _live = this;
            }
            catch (Exception e) { _on = false; Log.Error("HorseWind.AfterStart", e); }
        }

        public override void OnAgentDeleted(Agent affectedAgent)
        {
            try { if (affectedAgent != null) _horses.Remove(affectedAgent); } catch { }
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            try { if (affectedAgent != null) _horses.Remove(affectedAgent); } catch { }
        }

        protected override void OnEndMission()
        {
            Finish();
            base.OnEndMission();
        }

        public override void OnRemoveBehavior()
        {
            Finish();
            base.OnRemoveBehavior();
        }

        private void Finish()
        {
            try { if (_on && !_done) { _done = true; Write(); } } catch (Exception e) { Log.Error("HorseWind.Linia", e); }
            HideBar();   // 155: pasek HUD gracza zgaszony (VM HUD juz zamkniety = Sink null, nic)
            if (ReferenceEquals(_live, this)) _live = null;
            _on = false;
            ClearAvg();
            _horses.Clear();
            _pl = null;
        }

        private static void ClearAvg()
        {
            _avgOn = false;
            for (int i = 0; i < Slots; i++) { _fSum[i] = 0f; _fRestV[i] = 0f; _fN[i] = 0; _fWho[i] = null; _avg[i] = -1f; _restV[i] = -1f; _avgWho[i] = null; }
        }

        // ------------------------------------------------------------ takt

        public override void OnMissionTick(float dt)
        {
            if (!_on) return;
            _acc += dt;
            if (_acc < Step) return;
            float step = _acc;
            _acc = 0f;
            var c = Settings.Current;
            if (c == null || !c.HorseStaminaEnabled)
            {
                // klucz wylaczony w trakcie bitwy: kara zdjeta (z tym samym limitem zapisow), srednie szykow niedostepne
                _avgOn = false;
                RestoreAll();
                HideBar();   // 155: bez staminy koni nie ma paska (powiadomienie tylko przy zmianie)
                return;
            }
            float scale = Clamp(c.HorseGallopDrainScale, 0.25f, 3f);
            float floor = Clamp(c.HorseTiredFloor, 0.4f, 0.9f);
            var mission = Mission;
            float now = mission.CurrentTime;
            _clock = now;
            for (int i = 0; i < Slots; i++) { _fSum[i] = 0f; _fRestV[i] = 0f; _fN[i] = 0; _fWho[i] = null; }

            var agents = mission.Agents;
            int n = agents != null ? agents.Count : 0;
            int budget = WritesPerTick, resume = -1;
            int start = _rot > 0 && _rot < n ? _rot : 0;
            for (int k = 0; k < n; k++)
            {
                int i = start + k;
                if (i >= n) i -= n;
                Agent a;
                try { a = agents[i]; } catch { continue; }
                if (a == null) continue;
                try
                {
                    Horse h;
                    Agent rider = a.RiderAgent;
                    if (rider == null)
                    {
                        // luzak: tylko te konie, ktore ktos dosiadal w tej bitwie - odpoczynek jak stepem (u = 0, g = 1)
                        if (!_horses.TryGetValue(a, out h)) continue;
                        h.HasPos = false;
                        if (h.Ex == ExNone && h.S < 100f) { h.S += (100f - h.S) / h.Tau * step; if (h.S > 100f) h.S = 100f; }
                    }
                    else
                    {
                        if (!_horses.TryGetValue(a, out h)) { h = new Horse(); _horses[a] = h; Setup(a, h, c); }
                        if (!ReferenceEquals(h.Rider, rider)) NewRider(h, rider, scale);
                        if (h.Ex == ExNone) Ride(mission, a, h, rider, step, scale, now);
                    }
                    float m = h.Ex != ExNone ? 1f : (h.S < TiredBelow ? floor + (1f - floor) * h.S / TiredBelow : 1f);
                    h.M = m;
                    float d = m - h.MWritten;
                    if (d >= MinStep || d <= -MinStep || (m >= 0.999f && h.MWritten < 0.999f))
                    {
                        // recenzja 136: kon gracza poza limitem zapisow (jedno wolanie natywne na takt najwyzej) - w duzej bitwie kolejka kilkuset meczacych sie koni
                        // opozniala jego kare o kilka sekund i dawala duze skoki (komunikat "blown" przed zwolnieniem)
                        if (rider != null && rider.IsMainAgent) { Push(a, h, m); _plWrites++; }
                        else if (budget > 0) { budget--; Push(a, h, m); }
                        else if (resume < 0) resume = i;
                    }
                }
                catch (Exception e) { Stumble("HorseWind.Kon", e); }
            }
            _rot = resume;

            for (int i = 0; i < Slots; i++)
            {
                if (_fN[i] > 0) { _avg[i] = _fSum[i] / _fN[i]; _restV[i] = _fRestV[i] / _fN[i]; _avgWho[i] = _fWho[i]; }
                else { _avg[i] = -1f; _restV[i] = -1f; _avgWho[i] = null; }
            }
            _avgOn = true;
            PublishBar(mission, c, step);   // 155: pasek HUD gracza - po calym takcie (nowy kon gracza ma juz wpis z Setup)
        }

        /// <summary>155: pasek staminy konia gracza w HUD - S konia, na ktorym siedzi Agent.Main (mission.MainAgent), gdy kon jest liczony (nie smok, rydwan,
        /// slon, mamut, kon umarlych, jezdziec umarly); inaczej ukryty. Ponizej TiredBelow (kon zwalnia, komunikat "Your horse is tiring") - czerwony.
        /// Tylko odczyt slownika w takcie (glowny watek); wyjatek = licznik potkniec (licz, nie gas).</summary>
        private void PublishBar(Mission mission, Settings c, float step)
        {
            var sink = HorseWindBarLink.Sink;
            if (sink == null) { _barNoSink++; return; }
            try
            {
                float s = -1f;
                if (c.HorseStaminaBar)
                {
                    Agent main = mission.MainAgent;
                    Agent mount = main != null ? main.MountAgent : null;
                    Horse h;
                    if (mount != null && _horses.TryGetValue(mount, out h) && h.Ex == ExNone) s = h.S;
                }
                bool shown = s >= 0f;
                if (shown)
                {
                    _barTime += step;
                    if (_barMin < 0f || s < _barMin) _barMin = s;
                    _barLast = s;
                }
                sink.Set(shown, shown ? s / 100f : 0f, shown && s < TiredBelow);
            }
            catch (Exception e) { _barErr++; Stumble("HorseWind.Pasek", e); }
        }

        /// <summary>155: pasek HUD ukryty (klucz staminy wylaczony w trakcie bitwy, koniec misji). Nie rzuca.</summary>
        private static void HideBar()
        {
            try { var k = HorseWindBarLink.Sink; if (k != null) k.Set(false, 0f, false); } catch { }
        }

        /// <summary>Pierwsze zetkniecie z koniem pod jezdzcem: Vbase (MountSpeed jeszcze bez naszej kary), wykluczenia, cechy konia.</summary>
        private void Setup(Agent a, Horse h, Settings c)
        {
            try { var adp = a.AgentDrivenProperties; h.Vbase = adp != null ? adp.MountSpeed : -1f; } catch { h.Vbase = -1f; }
            ItemObject item = null, harn = null;
            try
            {
                var eq = a.SpawnEquipment;
                if (eq != null) { item = eq[EquipmentIndex.Horse].Item; harn = eq[EquipmentIndex.HorseHarness].Item; }
            }
            catch { }
            if (item != null) h.Id = item.StringId ?? "?";
            string mid = "", mu = "";
            try { var mo = a.Monster; if (mo != null) { mid = mo.StringId ?? ""; mu = mo.MonsterUsage ?? ""; } } catch { }
            var fam = MountLaw.FamilyOf(item);

            byte ex = ExNone;
            if (fam == MountLaw.Fam.Dragon || mid.StartsWith("dragon", StringComparison.Ordinal) || mu == "dragon" || mu == "dragonfly" || DragonFeed.IsDragon(item)) ex = ExDragon;
            else if (fam == MountLaw.Fam.Chariot || mid.StartsWith("chariot", StringComparison.Ordinal) || mu == "chariot") ex = ExChariot;
            else if (fam == MountLaw.Fam.Elephant || fam == MountLaw.Fam.Mammoth || mid.StartsWith("elephant", StringComparison.Ordinal)
                     || mid.StartsWith("mammoth", StringComparison.Ordinal)) ex = ExBig;
            else if (WightHorse(item)) ex = ExDead;
            else if (item == null || item.Weight < 50f || h.Vbase < 0.5f) ex = ExOther;
            if (ex != ExNone) { h.Ex = ex; _ex[ex]++; return; }

            h.Kg = item.Weight;
            h.HarnessRaw = harn != null ? Math.Max(0f, harn.Weight) : 0f;
            float heavy = Clamp(c.HorseHarnessHeavyScale, 0.1f, 1f);
            h.Harness = h.HarnessRaw <= HarnessFreeKg ? h.HarnessRaw : HarnessFreeKg + (h.HarnessRaw - HarnessFreeKg) * heavy;
            if (h.HarnessRaw > HarnessFreeKg) _harnessScaled++;
            int spd = 50;
            try { if (item.HorseComponent != null) spd = item.HorseComponent.Speed; } catch { }
            h.Wprim = 44f * Clamp(spd / 50f, 0.85f, 1.35f);
            h.CP = 0.75f + (Hardy(item, fam) ? 0.05f : 0f) - (h.Kg >= 500f ? 0.05f : 0f);
            h.Tau = Clamp(c.HorseRecoverySeconds, 30f, 600f) * (float)Math.Sqrt(h.Kg / 450f);
        }

        /// <summary>Nowy jezdziec na koniu: obciazenie (zbroja, bron, uprzaz), oszczednosc jezdzca (Riding), jezdziec umarly = wykluczenie (na stale).</summary>
        private void NewRider(Horse h, Agent rider, float scale)
        {
            h.Rider = rider;
            if (h.Ex != ExNone) return;
            BasicCharacterObject ch = null;
            try { ch = rider.Character; } catch { }
            if (Undead.Character(ch))
            {
                h.Ex = ExDead;
                _ex[ExDead]++;
                return;
            }
            float armour = 0f, weap = 0f;
            try
            {
                var eq = rider.SpawnEquipment;
                if (eq != null) { armour = eq.GetTotalWeightOfArmor(true); weap = eq.GetTotalWeightOfWeapons(); }
            }
            catch { }
            h.R = (RiderKg + Math.Max(0f, armour) + Math.Max(0f, weap) + h.Harness) / Math.Max(50f, h.Kg);
            h.F = (1f + h.R) / 1.20f * (1f + 1.5f * Math.Max(0f, h.R - 0.25f));
            int riding = 0;
            try { if (ch != null) riding = ch.GetSkillValue(DefaultSkills.Riding); } catch { }
            h.RideMul = 1.15f - 0.30f * Math.Min(Math.Max(riding, 0), 300) / 300f;
            h.RestU = RestPace(h.F, h.CP);
            if (!h.Counted) { h.Counted = true; _seen++; }
            _rSum += h.R; _restUSum += h.RestU; _rN++;
            float t = GallopToZero(h, scale);
            if (t > 0f && t < 100000f) { _tSum += t; _tN++; }
        }

        /// <summary>Sekundy pelnego galopu (u = 1) od pelnej puli do zera; MaxValue = kon pelnym galopem nie traci tchu.</summary>
        private static float GallopToZero(Horse h, float scale)
        {
            float over = FullGallopD * h.F - h.CP;
            if (over <= 0f) return float.MaxValue;
            float perSec = 100f * over / h.Wprim * scale * h.RideMul;
            return perSec > 0f ? 100f / perSec : float.MaxValue;
        }

        /// <summary>Recenzja 136: tempo odpoczynku (u) przy obciazeniu konia - D(u) x f = 0.23 + RestG x (CP - 0.23), czyli g = 0.5 (powrot z 35 do 65
        /// w ok. 2.5 min przy tau 120 s); od stepu do klusa (RestUMin..RestUMax). Typowy kon ROT (f 1.34, CP 0.75): u ok. 0.27, ok. 3.3 m/s przy Vbase 12.</summary>
        private static float RestPace(float f, float cp)
        {
            float target = 0.23f + RestG * (cp - 0.23f);
            float c = 0.15f - target / Math.Max(0.5f, f);       // 0.70u^2 + 0.60u + c = 0
            if (c >= 0f) return RestUMin;
            float u = (-0.60f + (float)Math.Sqrt(0.36f - 2.8f * c)) / 1.4f;
            return Clamp(u, RestUMin, RestUMax);
        }

        /// <summary>Kon pod jezdzcem (liczony): pomiar drogi, model, srednia szyku, zajechani, komunikaty gracza.</summary>
        private void Ride(Mission mission, Agent a, Horse h, Agent rider, float step, float scale, float now)
        {
            Vec3 p = a.Position;
            if (h.HasPos && step > 0.001f && h.Vbase > 0.5f)
            {
                float dx = p.x - h.PX, dy = p.y - h.PY;
                float v = (float)Math.Sqrt(dx * dx + dy * dy) / step;
                if (v > 1.5f * h.Vbase) _rejected++;
                else
                {
                    Model(h, v / h.Vbase, step, scale);
                    if (rider.IsMainAgent) NotePlayer(a, h, v, now);
                }
            }
            h.PX = p.x; h.PY = p.y; h.HasPos = true;

            // srednia S szyku (konnica AI odpoczywa w MarchToContact)
            Formation f = null;
            try { f = rider.Formation; } catch { }
            int idx = Idx(f);
            if (idx >= 0)
            {
                if (_fWho[idx] == null) _fWho[idx] = f;
                if (ReferenceEquals(_fWho[idx], f)) { _fSum[idx] += h.S; _fRestV[idx] += h.RestU * h.Vbase; _fN[idx]++; }
            }

            if (h.S <= 0f && !h.Blown)
            {
                h.Blown = true;
                bool ours = false;
                try
                {
                    var pt = mission.PlayerTeam;
                    var rt = rider.Team;
                    ours = pt != null && MissionTeams.Valid(rt) && rt.Side == pt.Side;
                }
                catch { }
                if (ours) _blownOurs++; else _blownFoe++;
            }
            if (rider.IsMainAgent) PlayerMessages(h, now);
        }

        private static void Model(Horse h, float u, float dt, float scale)
        {
            if (u < 0f) u = 0f;
            float load = (0.15f + 0.60f * u + 0.70f * u * u) * h.F;
            if (load > h.CP)
            {
                h.S -= 100f * (load - h.CP) / h.Wprim * dt * scale * h.RideMul;
                if (h.S < 0f) h.S = 0f;
            }
            else if (load < h.CP && h.S < 100f)
            {
                float g = (h.CP - load) / (h.CP - 0.23f);
                if (g > 1f) g = 1f;
                if (g > 0f) h.S += (100f - h.S) / h.Tau * g * dt;
                if (h.S > 100f) h.S = 100f;
            }
        }

        /// <summary>Zapis kary do silnika: MountSpeed = Vbase x m i wypchniecie wlasciwosci (bez lancucha modeli).</summary>
        private static void Push(Agent a, Horse h, float m)
        {
            if (h.Vbase <= 0.5f) { h.MWritten = m; return; }
            var adp = a.AgentDrivenProperties;
            if (adp == null) return;
            adp.MountSpeed = h.Vbase * m;
            a.UpdateCustomDrivenProperties();
            h.MWritten = m;
            h.MAt = _clock;
            _writes++;
        }

        /// <summary>Klucz wylaczony w trakcie bitwy: MountSpeed z powrotem na Vbase (najwyzej 16 zapisow na takt), postfiks mnozy przez 1.</summary>
        private void RestoreAll()
        {
            int budget = WritesPerTick;
            foreach (var kv in _horses)
            {
                var h = kv.Value;
                h.M = 1f;
                if (h.MWritten < 0.999f && budget > 0)
                {
                    try { if (kv.Key != null && kv.Key.IsActive()) { Push(kv.Key, h, 1f); budget--; } else h.MWritten = 1f; }
                    catch (Exception e) { Stumble("HorseWind.Zdjecie", e); }
                }
            }
        }

        // ------------------------------------------------------------ gracz

        /// <summary>Probka konia gracza (dowod T1 i jednostek). Liczone tylko probki galopu (v ponad 0.8 obecnego sufitu) przy mnozniku, ktory stoi w silniku
        /// od StableFor s - po obnizce kon jeszcze hamuje. Przy m &lt;= T1MaxM: silnik slucha MountSpeed = v/(Vbase x m) ok. 1; nie slucha = ok. 1/m;
        /// probka blizej 1/m niz 1 (prog (1 + 1/m) / 2) liczona osobno - pojedyncze odstajace (zjazd, scisk, odrzut) nie wywracaja wyniku.</summary>
        private static void NotePlayer(Agent a, Horse h, float v, float now)
        {
            _pl = h;
            if (v > _plMaxV) _plMaxV = v;
            float eng = -1f;
            try { eng = a.GetMaximumForwardUnlimitedSpeed(); } catch { }
            float m = h.MWritten;
            bool stable = now - h.MAt >= StableFor;
            if (m < 0.999f)
            {
                if (m <= _plEngLowM) { _plEngLowM = m; _plEngLow = eng; }
                if (stable && m <= T1MaxM && m > 0.05f)
                {
                    float r2 = v / (h.Vbase * m);
                    if (r2 > 0.8f)
                    {
                        _pLN++; _pLSum += r2; _pLMSum += m; _pLVb += v / h.Vbase;
                        if (r2 > 0.5f * (1f + 1f / m)) _pLOver++;
                    }
                }
            }
            else
            {
                _plEng1 = eng;
                if (stable)
                {
                    float r1 = v / h.Vbase;
                    if (r1 > 0.8f) { _p1N++; _p1Sum += r1; }
                }
            }
        }

        private void PlayerMessages(Horse h, float now)
        {
            if (h.S < TiredBelow && !h.SaidTired && now - _msgAt[0] >= MsgGap)
            {
                Log.Player("Your horse is tiring - ease it to a trot to let it breathe.", true);
                h.SaidTired = true;
                _msgAt[0] = now;
            }
            if (h.S <= 0f && !h.SaidBlown && now - _msgAt[1] >= MsgGap)
            {
                Log.Player("Your horse is blown - it can only canter until it gets its wind back.", true);
                h.SaidBlown = true;
                h.SaidTired = true;
                _msgAt[1] = now;
            }
            if (h.S > WindBackAbove && (h.SaidTired || h.SaidBlown) && now - _msgAt[2] >= MsgGap)
            {
                Log.Player("Your horse has its wind back.");
                h.SaidTired = false;
                h.SaidBlown = false;
                _msgAt[2] = now;
            }
        }

        // ------------------------------------------------------------ dla MarchToContact (glowny watek)

        /// <summary>Srednia S (0..100) koni liczonych w szyku z ostatniego taktu; -1 = brak danych (klucz wylaczony, nie bitwa, szyk bez liczonych koni).</summary>
        internal static float AvgStamina(Formation f)
        {
            if (!_avgOn || f == null) return -1f;
            int idx = Idx(f);
            if (idx < 0 || !ReferenceEquals(_avgWho[idx], f)) return -1f;
            return _avg[idx];
        }

        /// <summary>Recenzja 136: tempo odpoczynku koni szyku w m/s (srednia RestU x Vbase z ostatniego taktu - Vbase bez kary, wiec zmeczenie go nie obniza);
        /// -1 = brak danych (jak AvgStamina). MarchToContact dzieli przez margines linii, zeby limit konia wyszedl na to tempo.</summary>
        internal static float RestSpeed(Formation f)
        {
            if (!_avgOn || f == null) return -1f;
            int idx = Idx(f);
            if (idx < 0 || !ReferenceEquals(_avgWho[idx], f)) return -1f;
            return _restV[idx];
        }

        /// <summary>292: segment koni linii "Stamina (292)" (BattleWind.StartLine292, glowny watek) - konie bez mnoznika staminy ludzi: pula 0..100, pelny galop
        /// do zera z GallopToZero (srednia koni z jezdzcem policzonych do teraz i kon gracza, jesli na nim siedzi). Tylko odczyt.</summary>
        internal static void Note292(System.Text.StringBuilder sb)
        {
            var hw = _live;
            if (hw == null || !hw._on) { sb.Append("konie: - (stamina koni nieczynna w tej misji)"); return; }
            var c = Settings.Current;
            float scale = c != null ? Clamp(c.HorseGallopDrainScale, 0.25f, 3f) : 1f;
            sb.Append("konie: bez mnoznika (x1.00), pula 0-100, z jezdzcem ").Append(_seen)
              .Append(", pelny galop do zera sr. ").Append(_tN > 0 ? F(_tSum / _tN, "0") + " s" : "-")
              .Append(" (drenaz galopu x").Append(F(scale, "0.00")).Append(')');
            Horse p = null;
            Agent main = null;
            try { main = hw.Mission != null ? hw.Mission.MainAgent : null; } catch { }
            if (main != null)
                foreach (var kv in hw._horses)
                    if (kv.Value != null && kv.Value.Ex == ExNone && kv.Value.Counted && ReferenceEquals(kv.Value.Rider, main)) { p = kv.Value; break; }
            if (p != null)
            {
                float t = GallopToZero(p, scale);
                sb.Append(", gracz: kon ").Append(p.Id).Append(", pelny galop do zera ").Append(t < 100000f ? F(t, "0") + " s" : "nigdy");
            }
            else sb.Append(", gracz: -");
        }

        /// <summary>MarchToContact: szyk konnicy AI zaczal odpoczywac (licznik linii).</summary>
        internal static void NoteAiRest() { _aiRests++; }

        /// <summary>148 (poscig konnicy AI): stamina S (0..100) konia z ostatniego taktu; -1 = brak danych (klucz wylaczony, nie bitwa, kon wykluczony
        /// - smok, rydwan, umarly - albo jeszcze nie liczony). Glowny watek (slownik pisze tylko OnMissionTick).</summary>
        internal static float StaminaOf(Agent mount)
        {
            var hw = _live;
            if (!_avgOn || hw == null || mount == null) return -1f;
            Horse h;
            if (!hw._horses.TryGetValue(mount, out h) || h.Ex != ExNone) return -1f;
            return h.S;
        }

        private static int Idx(Formation f)
        {
            if (f == null) return -1;
            var t = f.Team;
            if (t == null) return -1;
            int ti = t.TeamIndex, fi = (int)f.FormationIndex;
            if (ti < 0 || ti >= MaxTeams || fi < 0 || fi >= Stride) return -1;
            return ti * Stride + fi;
        }

        // ------------------------------------------------------------ latka modelu

        private static void EnsurePatched()
        {
            if (_patchTried) return;
            _patchTried = true;
            var sb = new StringBuilder(400);
            sb.Append("Konie (136): latka UpdateAgentStats wpieta ");
            try
            {
                var t = typeof(SandBox.GameComponents.SandboxAgentStatCalculateModel);
                var target = AccessTools.DeclaredMethod(t, "UpdateAgentStats", new[] { typeof(Agent), typeof(AgentDrivenProperties) });
                if (target == null) sb.Append("NIE (brak SandboxAgentStatCalculateModel.UpdateAgentStats)");
                else
                {
                    new Harmony(HarmonyId).Patch(target, postfix: new HarmonyMethod(typeof(HorseWind), nameof(PostStats)) { priority = Priority.Last });
                    _patched = Owned(target, HarmonyId, false);
                    sb.Append(_patched ? "TAK" : "NIE (Harmony nie pokazuje naszego postfiksu - cicho polknieta?)");
                    sb.Append(" (postfiksy na celu: ").Append(Owners(target, false)).Append(')');
                }
                var horse = AccessTools.DeclaredMethod(t, "UpdateHorseStats");
                sb.Append(" | postfiks RBM na UpdateHorseStats (com.rbmcombat) ").Append(horse != null && Owned(horse, "com.rbmcombat", false) ? "TAK" : "NIE");
            }
            catch (Exception e)
            {
                sb.Append("NIE (").Append(e.GetType().Name).Append(": ").Append(e.Message).Append(')');
                Log.Error("HorseWind.Latka", e);
            }
            try
            {
                var mgm = MissionGameModels.Current;
                var model = mgm != null ? mgm.AgentStatCalculateModel : null;
                sb.Append(" | czynny model statystyk: ").Append(model != null ? model.GetType().FullName : "-");
            }
            catch { }
            try   // paczka 203: wspolczynnik drenazu galopu w linii startu (czytany na zywo w takcie - tu wartosc przy pierwszej bitwie)
            {
                var c = Settings.Current;
                sb.Append(" | drenaz galopu x").Append(c != null ? F(Clamp(c.HorseGallopDrainScale, 0.25f, 3f), "0.00") : "-")
                  .Append(" (HorseGallopDrainScale; 203: domyslnie 0.50 = polowa z 136, galop do zera x2), odpoczynek bez zmian (HorseRecoverySeconds ")
                  .Append(c != null ? F(Clamp(c.HorseRecoverySeconds, 30f, 600f), "0") : "-").Append(" s)");
            }
            catch { }
            sb.Append('.');
            Log.Info(sb.ToString());
        }

        private static bool Owned(System.Reflection.MethodBase target, string id, bool prefix)
        {
            try
            {
                var info = Harmony.GetPatchInfo(target);
                if (info == null) return false;
                var list = prefix ? info.Prefixes : info.Postfixes;
                foreach (var p in list) if (p != null && p.owner == id) return true;
            }
            catch { }
            return false;
        }

        private static string Owners(System.Reflection.MethodBase target, bool prefix)
        {
            try
            {
                var info = Harmony.GetPatchInfo(target);
                if (info == null) return "-";
                var sb = new StringBuilder(120);
                foreach (var p in prefix ? info.Prefixes : info.Postfixes)
                {
                    if (p == null) continue;
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(p.owner);
                }
                return sb.Length > 0 ? sb.ToString() : "-";
            }
            catch { return "?"; }
        }

        /// <summary>Postfiks SandboxAgentStatCalculateModel.UpdateAgentStats: kon, ktorego liczymy - swieza Vbase i MountSpeed = Vbase x m.
        /// Glowny watek (MBCallback isMultiThreadCallable = false; wolania zarzadzane tez glowne); obcy watek - licznik i nic. Bez alokacji i logu.</summary>
        private static void PostStats(Agent agent, AgentDrivenProperties agentDrivenProperties)
        {
            var hw = _live;
            if (hw == null || agent == null || agentDrivenProperties == null) return;
            try
            {
                if (Thread.CurrentThread.ManagedThreadId != _mainTid) { Interlocked.Increment(ref _postOff); return; }
                Horse h;
                if (!hw._horses.TryGetValue(agent, out h) || h.Ex != ExNone) return;
                _postN++;
                float fresh = agentDrivenProperties.MountSpeed;
                if (fresh <= 0.5f) { if (h.MWritten < 0.999f) h.MAt = _clock; h.MWritten = 1f; return; }
                h.Vbase = fresh;
                float m = h.M;
                if (m < 0.999f) { agentDrivenProperties.MountSpeed = fresh * m; _postPen++; }
                if (m - h.MWritten > 0.001f || h.MWritten - m > 0.001f) h.MAt = _clock;
                h.MWritten = m;
            }
            catch { Interlocked.Increment(ref _postErr); }
        }

        // ------------------------------------------------------------ linia misji

        private void Write()
        {
            var c = Settings.Current;
            float scale = c != null ? Clamp(c.HorseGallopDrainScale, 0.25f, 3f) : 1f;
            float sSum = 0f;
            int sN = 0;
            foreach (var kv in _horses)
            {
                var h = kv.Value;
                if (h.Ex != ExNone || !h.Counted) continue;
                sSum += h.S; sN++;
            }
            int exAll = 0;
            for (int i = 1; i < ExKinds; i++) exAll += _ex[i];
            var sb = new StringBuilder(700);
            sb.Append("Konie (136): koni z jezdzcem ").Append(_seen)
              .Append(" (wykluczonych ").Append(exAll).Append(": smoki ").Append(_ex[ExDragon]).Append(", rydwany ").Append(_ex[ExChariot])
              .Append(", slonie i mamuty ").Append(_ex[ExBig]).Append(", umarli ").Append(_ex[ExDead]).Append(", inne ").Append(_ex[ExOther]).Append(')')
              .Append(" | stamina koni ").Append(c != null && c.HorseStaminaEnabled ? "TAK" : "NIE")
              .Append(" | zajechanych do 0: strona gracza ").Append(_blownOurs).Append(", wrog ").Append(_blownFoe)
              .Append(" | sr. S na koniec ").Append(sN > 0 ? F(sSum / sN, "0") : "-").Append(" (koni ").Append(sN).Append(')')
              .Append(" | sr. obciazenie r ").Append(_rN > 0 ? F(100f * _rSum / _rN, "0") + "%" : "-")
              .Append(" (tempo odpoczynku AI sr. u ").Append(_rN > 0 ? F(_restUSum / _rN, "0.00") : "-").Append(')')
              .Append(" (uprzaz przeliczona x").Append(F(c != null ? Clamp(c.HorseHarnessHeavyScale, 0.1f, 1f) : 0.2f, "0.##")).Append(' ').Append(_harnessScaled).Append(" razy)")
              .Append(", pelny galop do zera sr. ").Append(_tN > 0 ? F(_tSum / _tN, "0") + " s" : "-")
              .Append(" (drenaz galopu x").Append(F(scale, "0.00")).Append(')')   // paczka 203
              .Append(" | zapisow MountSpeed ").Append(_writes).Append(" (kon gracza poza limitem ").Append(_plWrites).Append(")")
              .Append(", przeliczen modelu ").Append(_postN).Append(" (postfiks; z kara ").Append(_postPen)
              .Append(", obcy watek ").Append(Volatile.Read(ref _postOff)).Append(", bledy ").Append(Volatile.Read(ref _postErr)).Append(')')
              .Append(", odrzuconych probek (skok > 1.5 Vbase) ").Append(_rejected)
              .Append(" | odpoczynkow konnicy AI ").Append(_aiRests)
              .Append(" (ponizej ").Append(c != null ? Math.Max(0, Math.Min(80, c.HorseAiRestBelow)).ToString(CultureInfo.InvariantCulture) : "-")
              .Append(", do ").Append(c != null ? Math.Max(20, Math.Min(100, c.HorseAiRestUntil)).ToString(CultureInfo.InvariantCulture) : "-").Append(')');
            var p = _pl;
            if (p != null)
            {
                float t = GallopToZero(p, scale);
                sb.Append(" | gracz: kon ").Append(p.Id).Append(' ').Append(F(p.Kg, "0")).Append(" kg, r ").Append(F(100f * p.R, "0")).Append("%, Vbase ")
                  .Append(F(p.Vbase, "0.0")).Append(" m/s, pelny galop do zera ").Append(t < 100000f ? F(t, "0") + " s" : "nigdy")
                  .Append(" (przy x1 z 136: ").Append(GallopToZero(p, 1f) < 100000f ? F(GallopToZero(p, 1f), "0") + " s" : "nigdy").Append(')')   // paczka 203
                  .Append(", S na koniec ").Append(F(p.S, "0"))
                  .Append(", najwieksza zmierzona v ").Append(F(_plMaxV, "0.0")).Append(" m/s (surowa, z odstajacymi)")
                  .Append(", galop przy m=1: sr. v/Vbase ").Append(_p1N > 0 ? F(_p1Sum / _p1N, "0.00") : "-").Append(" (probek ").Append(_p1N)
                  .Append("; jednostki dobre, gdy ok. 0.85-1.05)")
                  .Append(", T1 przy m<=").Append(F(T1MaxM, "0.00")).Append(" (m stale >= 1 s, probki galopu): ");
                if (_pLN > 0)
                    sb.Append("probek ").Append(_pLN).Append(", sr. m ").Append(F(_pLMSum / _pLN, "0.00")).Append(", sr. v/Vbase ").Append(F(_pLVb / _pLN, "0.00"))
                      .Append(", sr. v/(Vbase x m) ").Append(F(_pLSum / _pLN, "0.00")).Append(", blizej 1/m niz 1: ").Append(F(100f * _pLOver / _pLN, "0")).Append('%');
                else sb.Append("- (kon gracza nie zwolnil do tego m albo nie galopowal)");
                sb.Append(" (silnik slucha MountSpeed: v/(Vbase x m) ok. 1 i blizej 1/m ponizej 20%; nie slucha: ok. 1/m i ponad 50%)")
                  .Append(", silnik GetMaximumForwardUnlimitedSpeed przy m=1 ").Append(_plEng1 >= 0f ? F(_plEng1, "0.0") : "-")
                  .Append(", przy m ").Append(F(_plEngLowM, "0.00")).Append(' ').Append(_plEngLow >= 0f ? F(_plEngLow, "0.0") : "-");
            }
            else sb.Append(" | gracz: - (bez liczonego konia)");
            // 155 (recenzja, poprawka 1): liczniki wstawki i mixinu narastajace od startu gry (rosna w EarlyStart, przed AfterStart) - nie "TAK/NIE" z Sink w Finish
            sb.Append(" | pasek gracza (155): klucz HorseStaminaBar ").Append(c != null && c.HorseStaminaBar ? "TAK" : "NIE")
              .Append(", wstawka AgentStatus nalozona ").Append(Volatile.Read(ref HorseWindBarLink.Applied)).Append(" razy od startu gry")
              .Append(", mixin MissionAgentStatusVM utworzony ").Append(Volatile.Read(ref HorseWindBarLink.MixinMade)).Append(" razy od startu gry")
              .Append(", taktow bez odbiorcy ").Append(_barNoSink)
              .Append(", widoczny ").Append(F(_barTime, "0")).Append(" s, najnizsze S ").Append(_barMin >= 0f ? F(_barMin, "0") : "-")
              .Append(", ostatnie S ").Append(_barLast >= 0f ? F(_barLast, "0") : "-")
              .Append(" (czerwony ponizej ").Append(F(TiredBelow, "0")).Append("), bledy ").Append(_barErr);
            sb.Append(" | potkniecia ").Append(_stum).Append('.');
            Log.Info(sb.ToString());
        }

        // ------------------------------------------------------------ pomocnicze

        private static readonly string[] HardyKeys = { "aserai", "khuzait", "eastern", "sand", "dothraki", "steppe", "camel" };

        /// <summary>Rodziny wytrzymale (konie stepowe i pustynne, wielblad): id albo kultura przedmiotu.</summary>
        private static bool Hardy(ItemObject item, MountLaw.Fam fam)
        {
            if (fam == MountLaw.Fam.Camel) return true;
            if (item == null) return false;
            string id = item.StringId ?? "";
            string cul = "";
            try { if (item.Culture != null) cul = item.Culture.StringId ?? ""; } catch { }
            for (int i = 0; i < HardyKeys.Length; i++)
                if (id.IndexOf(HardyKeys[i], StringComparison.Ordinal) >= 0 || cul.IndexOf(HardyKeys[i], StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        /// <summary>Kon umarlych (wight_horse, kultura wights / walkers).</summary>
        private static bool WightHorse(ItemObject item)
        {
            if (item == null) return false;
            string id = item.StringId ?? "";
            string cul = "";
            try { if (item.Culture != null) cul = item.Culture.StringId ?? ""; } catch { }
            return id.IndexOf("wight", StringComparison.Ordinal) >= 0 || cul.IndexOf("wight", StringComparison.Ordinal) >= 0
                   || cul.IndexOf("walker", StringComparison.Ordinal) >= 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stum++;
            _stumSession++;
            if (_stumSession <= 3) Log.Error(where, e);
        }

        private static float Clamp(float v, float lo, float hi) { return v < lo ? lo : (v > hi ? hi : v); }

        private static string F(float v, string fmt) { return v.ToString(fmt, CultureInfo.InvariantCulture); }
    }
}
