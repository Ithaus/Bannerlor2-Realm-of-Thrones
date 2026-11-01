using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// Cialo ma swoje prawa - realia pola bitwy, o ktore prosil Jeff:
    /// 1. ZADYSZKA (model punktowy, wg slow Jeffa): czlowiek ma pule staminy
    ///    w PUNKTACH (RBM: baza 1000, Atletyka JUZ ja podnosi; my dokladamy
    ///    Endurance bohaterow). Sprint POBIERA stale punkty na sekunde,
    ///    tym wiecej, im ciezszy pancerz. Ile sekund pobiegniesz, wynika SAMO
    ///    z puli i drenazu - zadnych "sekund do zmeczenia" w ustawieniach.
    ///    W OSTATNICH 10% puli czlowiek zwalnia (bieg i ciosy), przy ZERZE
    ///    lapie zadyszke: -50% ruchu, walki i obrazen, i odsapuje, az oddech
    ///    wroci do 10%. Dotyczy kazdego na polu: gracza, jego ludzi i wroga.
    /// 2. RANNI: ponizej progu zdrowia czlowiek zwalnia i wolniej wymachuje
    ///    (kara rosnie z rana), a od 10 HP krwawi i moze sie wykrwawic na smierc.
    ///    AI bliskie smierci lamie sie i ucieka z pola.
    /// 3. JEZ ZE STRZAL: strzala, ktora ledwo drasnela (pancerz ja zatrzymal),
    ///    nie zostaje wbita w czlowieka - odpada. W tarczy zostaje jak nalezy.
    /// </summary>
    internal sealed class FieldCraft : MissionBehavior, IPlayerInputEffector
    {
        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        // 313c (recenzja): flagi chodu/biegu gracza przez oficjalne wejscie gry - MissionMainAgentController.ControlTick zeruje EventControlFlags
        // i zaraz potem zbiera je od IPlayerInputEffector (mmac.cs:957-966); jednorazowo, potem zero
        private static Agent.EventControlFlag _p3Want;

        public Agent.EventControlFlag OnCollectPlayerEventControlFlags()
        {
            var f = _p3Want;
            _p3Want = (Agent.EventControlFlag)0;
            return f;
        }

        private sealed class State
        {
            public float Pace = 1f;        // mnoznik tempa z oddechu (1 = swiezy, 0.5 = odsapuje)
            public bool Winded;            // ZADYSZKA: pula spadla do zera - odsapuje, az wroci 10%
            public float BaseSwing = -1f;  // zlapana baza do kar bojowych
            public float BaseThrust = -1f;
            public bool SwingApplied;      // czy nasza kara siedzi teraz we wlasciwosciach agenta
            public float LastSwingF = -1f; // 309 (klucz FieldCraftSwingOnChange): mnoznik zamachu z ostatniego zapisu do silnika
            public bool Panicked;
            // policzone raz na agenta
            public float RunSeconds = -1f;  // 127: sekundy pelnego biegu do zera (fizjologia: Atletyka i waga zbroi) - drenaz = cala pula / RunSeconds
            public float EndBonus = 1f;     // mnoznik puli z Endurance (bohaterowie)
            public float TiredFloor;
            public float PoolMax;           // wlasna pula, gdy RBM nieobecny
            public float Pool = -1f;
            public float RegenPerSec;       // regeneracja wlasnej puli
            public bool EnduranceApplied;   // bonus Endurance dolozony do puli RBM
            public object RbmStance;        // RBMAI.Stance tego agenta (jesli RBM obecny)
            public bool RbmChecked;
            public float RbmRecheck;        // RBM zaklada stance PO spawnie - ponawiamy szukanie
            public float SprintDebt;        // ile MY zabralismy z puli RBM sprintem - tyle oddajemy szybko
            public float BleedDebt;         // uzbierany ulamek krwawienia - silnik przyjmuje tylko pelne punkty
            public bool SpeedApplied;       // noc 25.10 P1 (M0): nasz limit predkosci siedzi teraz w agencie (do jednorazowego zdjecia po karze)
            public bool EverWinded;         // 137: mial juz zadyszke w tej misji (linia "Oddech (137)": ludzi z zadyszka)
            public float WindedFor;         // 137: sekundy biezacej zadyszki
            public float Ratio = 1f;        // 144: biezacy pasek staminy / max (przy oddechu 137 = pula - F - S) - budzet biegu AiBreath
            public AiBreath.Man Ai;         // 144: stan zasady "AI oszczedza sily" (AiBreath.Step)
            public PanicRun.Man Pr;         // 147: pomiar biegu w panice (linia "Bieg zlamanych (147)") - tworzony przy pierwszej panice
            public float RunSeconds0 = -1f;  // 292: pelny bieg do zera z reguly 127/203 PRZED mnoznikiem staminy ludzi (pomiar biegu w linii "Bieg (127)")
            public float Run292Prev = -1f;   // 292: pasek na koniec poprzedniego taktu ciaglego biegu (-1 = poprzedni takt bez biegu)
            public float Run292Max;          // 292: maxStamina RBM przy Run292Prev (zmiana puli w biegu - takt pominiety)
            public bool Walk313;             // 313c: gracz po pustym pasku idzie (histereza do StaminaWalkUntilBar) - takze przy kluczu NIE (pomiar "bylby")
            public bool Walk313Forced;       // 313c: MY przelaczylismy gracza w tryb chodu gry (tylko wtedy oddajemy mu bieg)
            public bool Walk313Seen;         // 313c (recenzja): po naszym wymuszeniu gra pokazala WalkMode = true (potem false = gracz sam wlaczyl bieg)
            public float F313Prev = 1f;      // 313c (recenzja): mnoznik krzywej paska z poprzedniego taktu (prog biegu gracza wzgledem limitu)
            public bool Ctrl313Prev;         // 313c (recenzja): w poprzednim takcie limit gracza scial klawisz chodu (Ctrl) - wtedy prog biegu bez zmian
            public bool Meas313;             // 313c (recenzja): ten takt kwalifikuje sie do pomiaru limitu (klucz TAK, f < 1, poza chodem)
            // 313d (Bleed313): krwawienie z rany, rzut paniki z bolu, kustykanie
            public float BleedRate;          // HP/s z ran (suma), maleje wykladniczo (BleedClotSeconds); 0 = nie krwawi (regula BleedFromWounds)
            public int BleedBy = -1;         // indeks ostatniego, kto zadal krwawiaca rane (kredyt za zgon z krwawienia)
            public Agent BleedByA;           // recenzja 313d: ten sam czlowiek (indeks moze przejac posilek) - kredyt tylko, gdy to wciaz on
            public float BleedHp0;           // HP na starcie krwawienia (pomiar BleedDeathLog)
            public float Ath = -1f;          // Atletyka (raz na czlowieka)
            public int Hits;                 // trafienia z obrazeniami w bitwie (kolejka Bleed313) - rzut odwrotu 143 raz na swieza rane (BrokenMen)
            public bool PainDue;             // swieza rana od ostatniego taktu - rzut paniki z bolu, gdy ponizej progu
            public bool PainRolled;          // byl juz rzut z bolu (pierwsze zejscie ponizej progu bez ciosu - np. krwawieniem - tez rzuca raz)
            public bool PainBroken;          // rzut z bolu wygrany - lamie sie jak dotad (RoutHold.Break)
            public float LimpCap;            // cap kustykania wpisany do Bleed313 (m/s, 0 = brak)
            public float LimpSince = -1f;    // recenzja 313d (pomiar): czas misji wpisania capu (predkosc mierzona po Bleed313.LimpSettle s)
            public bool LimpLow;             // 313y (histereza): kustyka z HP <= WoundedLimpBelowHp; zdjete dopiero powyzej progu + Bleed313.LimpHystHp
            public bool BleedMissile;        // 313y: krwawiaca rana (biezace krwawienie) od pocisku - zgon z krwawienia liczy sie jako zgon od pocisku
        }

        // ---- noc 25.10 P1 (M0, klucz SpeedCapOnlyWhenWinded): liczniki zapisow limitu predkosci (glowny watek; segment 'marsz' linii NB2,
        // zerowane w RoutHold.Ensure przez MarchToContact.ResetCounters)
        private static int _spdPenalty, _spdMarch, _spdRelease, _spdOld;

        internal static void ResetSpeedCounters() { _spdPenalty = _spdMarch = _spdRelease = _spdOld = 0; ResetSwingCounters(); }

        /// <summary>Koncowka segmentu 'marsz' (bez znaku '|'): zapisy limitu FieldCraft w tej misji. Wolajacy trzyma RoutHold.Gate.</summary>
        internal static void AppendSpeed(System.Text.StringBuilder sb, Settings c)
        {
            sb.Append("; FieldCraft limit tylko przy karze ").Append(c != null && c.SpeedCapOnlyWhenWinded ? "TAK" : "NIE")
              .Append(": zapisy kary ").Append(_spdPenalty).Append(" (w marszu ").Append(_spdMarch).Append("), zdjecia ").Append(_spdRelease)
              .Append(", stare zapisy co 0.2 s ").Append(_spdOld);
        }

        // ---- 127 (25.10, Jeff: "bieganie w zbrojach ... w zaleznosci od tego ile mam na sobie", "ogolna zasada", "wyjsc od sredniej"):
        // postawy RBM ludzi, ktorzy TERAZ biegna - BattleWind.TickStaminaPrefix nie oddaje im oddechu (glowny watek: FieldCraft pisze co 0.2 s,
        // RBM StanceLogic.OnMissionTick czyta). Liczniki linii "Bieg (127)" na koniec misji.
        private static readonly HashSet<object> _runningStances = new HashSet<object>();
        private static int _runN, _regenSkipped;
        private static float _runKgSum, _runKgMax, _runTSum, _runTMin = float.MaxValue;
        private static float _plKg = -1f, _plAth, _plT, _plFit;   // paczka 203: _plFit - sprawnosc gracza (linia "Bieg (127)", sprawdz_logi p203 liczy T od nowa)
        // 292: czas pelnego biegu PO mnozniku staminy ludzi (st.RunSeconds) - linia "Bieg (127)" zostaje przy regule 127/203 (sprawdz_logi p203), K w dopisku
        private static float _runT292Sum, _plT292;
        // 292 (recenzja): POMIAR - spadek paska postawy RBM w ciaglym biegu (odczyt co takt: drenaz biegu, koszty RBM, oddech 137) wobec reguly sprzed 292
        // (krok / RunSeconds0) - sekundy biegu, spadek paska, oczekiwany spadek sprzed 292; ludzie i gracz osobno
        private static float _m292Sec, _m292Bar, _m292Exp, _m292PlSec, _m292PlBar, _m292PlExp;
        private bool _k292Said;      // linia "Stamina (292)" juz wypisana w tej misji
        private float _k292From;     // Mission.CurrentTime z AfterStart
        // Paczka 203: k z paczki 127 - tylko do porownania w linii "Bieg (127)" ("przy 2.25%/kg z 127 bylo X s"); regula liczy z ArmourRunCostPerKgPercent
        private const float ArmourRunCost127 = 2.25f;

        internal static bool IsRunning(object stance) { return stance != null && _runningStances.Count > 0 && _runningStances.Contains(stance); }

        internal static void NoteRegenSkipped() { _regenSkipped++; }

        // ---- 313c (01.11, Jeff: "jak biegalem i spadla mi stamina do 0 to nadal biegne zamiast caly czas stopniowo zwalniac, az jak dojdzie do 0 po prostu
        // powinienem przejsc w tryb marszu"). Dotad: pelna predkosc do 10% paska, w ostatnich 10% do 0.5, przy zerze zadyszka 0.5 x pelnej predkosci (dalej
        // animacja biegu) do WindedExitBar. Teraz u GRACZA pieszo: mnoznik ruchu z paska - od StaminaSlowFromBar liniowo do WalkSpeedShare przy 0
        // (ta sama sciezka zapisu co kara zadyszki: SetMaximumSpeedLimit; Agent.Controller przy przejeciu przez gracza zdejmuje limit - silnik czyta go
        // i u gracza), a przy pustym pasku wymuszony tryb chodu gry (EventControlFlag.Walk - to samo, co wysyla CapsLock, MissionMainAgentController:811)
        // do StaminaWalkUntilBar. Zadyszka (st.Winded) dalej rzadzi zamachem (st.Pace), ruchem gracza juz krzywa. Konno - nic (i zdjety nasz chod).
        // AI bez zmian (AiBreath 144). Pomiar takze przy kluczu NIE ("bylby"). Linia "Bieg gracza (313c)" na koniec misji.
        // Recenzja 313c: (1) prog biegu gracza wzgledem limitu krzywej (RunThr313) - bez tego drenaz stawal przy ok. 11% paska i chodu nie bylo;
        // (2) flagi Walk/Run gracza przez IPlayerInputEffector (zbierane tuz po wyzerowaniu w ControlTick), nie wprost w OnMissionTick;
        // (3) pomiar limitu po koncowym limicie i tylko do przodu (Sample313); (4) tempo chodu krzywej >= 0.4 (podloga limitu);
        // (5) osobno "gracz wlaczyl bieg" i "flaga nie weszla"; (6) agent bez gracza w naszym chodzie - bieg oddany.
        private static float _p3Foot, _p3Low, _p3LowRun, _p3Min = 1f, _p3WalkSec, _p3SpdSum, _p3LimSum, _p3NoDrain;
        private static int _p3WalkIn, _p3Reforce, _p3NoTake, _p3Out, _p3Samples, _p3Over, _p3Stumbles, _p3StumblesSession;

        private static void Reset313()
        {
            _p3Foot = _p3Low = _p3LowRun = _p3WalkSec = _p3SpdSum = _p3LimSum = _p3NoDrain = 0f; _p3Min = 1f;
            _p3WalkIn = _p3Reforce = _p3NoTake = _p3Out = _p3Samples = _p3Over = _p3Stumbles = 0;
            _p3Want = (Agent.EventControlFlag)0;
        }

        // zakresy jak RANGES w tools/gen_mcm.py; tempo chodu krzywej nie nizej niz podloga limitu ruchu 0.4 (if (limit < 0.4f) nizej) - recenzja: krzywa i log
        // pokazuja ten sam mnoznik, ktory naprawde trafia do limitu (klawisz Ctrl dalej wedlug WalkSpeedShare od 0.2)
        private static float SlowFrom313(Settings c) { return Math.Max(0.2f, Math.Min(0.9f, c.StaminaSlowFromBar)); }
        private static float WalkUntil313(Settings c) { return Math.Max(0.05f, Math.Min(0.5f, c.StaminaWalkUntilBar)); }
        private static float WalkShare313(Settings c) { return Math.Max(0.4f, Math.Min(1f, c.WalkSpeedShare)); }

        /// <summary>313c: mnoznik ruchu z paska - 1 od progu w gore, liniowo do tempa chodu przy 0.</summary>
        internal static float RunCurve313(float ratio, Settings c)
        {
            float from = SlowFrom313(c), walk = WalkShare313(c);
            if (ratio >= from) return 1f;
            if (ratio <= 0f) return walk;
            return walk + (1f - walk) * ratio / from;
        }

        /// <summary>313c (recenzja, usterka WYSOKA): prog biegu gracza pieszo przy kluczu TAK liczony wzgledem limitu krzywej z poprzedniego taktu
        /// (0.85 x f x pelna predkosc), inaczej przy pasku ok. 11% limit spychal predkosc pod staly prog 0.55 - drenaz stawal, pasek krazyl wokol 11%
        /// i gracz nigdy nie dochodzil do zera (chodu). Tylko ruch do przodu (cofanie i krok bokiem w zwarciu nie pija jak bieg), nie w trybie chodu
        /// gry i nie przy klawiszu Ctrl. Klucz NIE, AI, konno - prog jak dotad.</summary>
        private static float RunThr313(Agent agent, State st, Settings c, float runThr, float run)
        {
            try
            {
                if (!agent.IsMainAgent || !c.StaminaSlowsRunning || st.Ctrl313Prev || st.F313Prev >= 0.999f || run <= 0.8f || agent.WalkMode) return runThr;
                var mv = agent.MovementVelocity;
                float len = mv.Length;
                if (len < 0.5f || mv.y < 0.7f * len) return runThr;
                float t = run * st.F313Prev * 0.85f;
                if (t < 1.0f) t = 1.0f;
                return Math.Min(runThr, t);
            }
            catch (Exception e)
            {
                _p3Stumbles++; _p3StumblesSession++;
                if (_p3StumblesSession <= 3) Log.Error("FieldCraft.Run313 prog (potkniecie " + _p3StumblesSession + ")", e);
                return runThr;
            }
        }

        private static void Release313(Agent agent, State st)
        {
            st.Walk313Forced = false;
            st.Walk313Seen = false;
            _p3Out++;
            if (!agent.WalkMode) return;
            if (agent.IsMainAgent) _p3Want = Agent.EventControlFlag.Run;      // przez IPlayerInputEffector (kontroler zeruje flagi gracza co klatke)
            else agent.EventControlFlags |= Agent.EventControlFlag.Run;     // recenzja: agent stracil gracza (przejecie przez AI) - bez kontrolera gracza
        }

        /// <summary>313c: tempo ruchu gracza (zamiast st.Pace) - przy kluczu NIE, konno albo bez Sprint Fatigue zwraca st.Pace (jak dotad).</summary>
        private static float Run313(Agent agent, State st, Settings c, float step, float speedNow, bool sprintNow)
        {
            float pace = st.Pace;
            st.Meas313 = false;
            try
            {
                bool on = c.StaminaSlowsRunning;
                if (agent.MountAgent != null || !c.SprintFatigueEnabled)
                {
                    if (st.Walk313Forced) Release313(agent, st);   // w siodle nasz chod szedlby na konia
                    st.Walk313 = false;
                    st.F313Prev = 1f;
                    return pace;
                }
                float r = st.Ratio;
                if (r <= 0.005f) { if (!st.Walk313) _p3WalkIn++; st.Walk313 = true; }   // ten sam prog pustego paska co zadyszka
                else if (st.Walk313 && r >= WalkUntil313(c)) st.Walk313 = false;
                float f = RunCurve313(r, c);
                st.F313Prev = on ? f : 1f;                 // prog biegu w nastepnym takcie (RunThr313) - przy kluczu NIE bez zmian
                _p3Foot += step;
                if (f < 0.999f || st.Walk313)
                {
                    _p3Low += step;
                    if (sprintNow) _p3LowRun += step;
                    if (f < _p3Min) _p3Min = f;
                }
                if (st.Walk313) _p3WalkSec += step;
                if (!on)
                {
                    if (st.Walk313Forced) Release313(agent, st);   // klucz zdjety w trakcie misji
                    return pace;
                }
                // pomiar (recenzja): ruch blisko limitu krzywej bez drenazu - tak wygladalaby petla wokol 11% paska (powinno byc ok. 0)
                if (f < 0.999f && !st.Walk313 && !sprintNow && !st.Ctrl313Prev && speedNow > 0.5f && !agent.WalkMode)
                {
                    float full = agent.GetMaximumForwardUnlimitedSpeed();
                    if (full > 0.8f && speedNow / full > 0.75f * f) _p3NoDrain += step;
                }
                st.Meas313 = f < 0.999f && !st.Walk313;    // pomiar limitu po koncowym limicie (Sample313)
                if (st.Walk313)
                {
                    if (agent.WalkMode)
                    {
                        if (st.Walk313Forced) st.Walk313Seen = true;
                    }
                    else
                    {
                        if (st.Walk313Forced)
                        {
                            if (st.Walk313Seen) _p3Reforce++;   // gra weszla w chod, a gracz wlaczyl bieg (CapsLock) - znow chod
                            else _p3NoTake++;                   // flaga chodu nie weszla (woda, drabina, stan skryptowy) - ponawiamy
                        }
                        st.Walk313Seen = false;
                        _p3Want = Agent.EventControlFlag.Walk;   // przez IPlayerInputEffector (OnCollectPlayerEventControlFlags)
                        st.Walk313Forced = true;
                    }
                }
                else if (st.Walk313Forced) Release313(agent, st);
                pace = f;
                // 313y (recenzja integracji): w trybie chodu gry tempo daje sam chod - bez mnoznika krzywej (natywne SetMaximumSpeedLimit x 0.42 moglo mnozyc
                // predkosc chodu, gracz wloklby sie ok. 0.7 m/s); kara za rany i Ctrl dalej przez Math.Min nizej
                if (st.Walk313 && agent.WalkMode) pace = 1f;
            }
            catch (Exception e)
            {
                _p3Stumbles++; _p3StumblesSession++;
                if (_p3StumblesSession <= 3) Log.Error("FieldCraft.Run313 (potkniecie " + _p3StumblesSession + ")", e);
            }
            return pace;
        }

        /// <summary>313c (recenzja): pomiar "czy limit trzyma gracza" po KONCOWYM limicie (rany, Ctrl, podloga 0.4), tylko ruch do przodu
        /// (cofanie i krok bokiem sa natywnie wolniejsze); osobno probki ponad limit + 10 pp. Zapamietuje Ctrl dla progu biegu.</summary>
        private static void Sample313(Agent agent, State st, float limit, float speedNow, bool ctrl)
        {
            try
            {
                st.Ctrl313Prev = ctrl;
                if (!st.Meas313 || speedNow <= 0.5f) return;
                var mv = agent.MovementVelocity;
                float len = mv.Length;
                if (len < 0.5f || mv.y < 0.8f * len) return;
                float full = agent.GetMaximumForwardUnlimitedSpeed();
                if (full <= 0.8f) return;
                float share = speedNow / full;
                _p3SpdSum += share; _p3LimSum += limit; _p3Samples++;
                if (share > limit + 0.1f) _p3Over++;
            }
            catch (Exception e)
            {
                _p3Stumbles++; _p3StumblesSession++;
                if (_p3StumblesSession <= 3) Log.Error("FieldCraft.Run313 pomiar (potkniecie " + _p3StumblesSession + ")", e);
            }
        }

        private void Line313(Settings c)
        {
            try
            {
                if (c == null || _p3Foot <= 0f) return;
                bool battle = false;
                try { battle = Mission != null && Mission.Mode == MissionMode.Battle; } catch { }
                if (!battle && _p3Low <= 0f) return;
                var ic = System.Globalization.CultureInfo.InvariantCulture;
                bool on = c.StaminaSlowsRunning;
                Log.Info("Bieg gracza (313c): klucz Stamina Slows Running " + (on ? "TAK" : "NIE (pomiar: bylby)")
                         + " | regula: od " + (SlowFrom313(c) * 100f).ToString("0", ic) + "% paska plynnie do chodu (" + WalkShare313(c).ToString("0.00", ic)
                         + " pelnej) przy 0, po pustym pasku chod do " + (WalkUntil313(c) * 100f).ToString("0", ic) + "%"
                         + " | gracz pieszo " + _p3Foot.ToString("0", ic) + " s, ponizej progu " + _p3Low.ToString("0", ic) + " s (w biegu " + _p3LowRun.ToString("0", ic)
                         + " s, bez drenazu blisko limitu " + _p3NoDrain.ToString("0", ic) + " s), najnizszy mnoznik " + _p3Min.ToString("0.00", ic)
                         + " | chod po pustym pasku: wejsc " + _p3WalkIn + ", sekund " + _p3WalkSec.ToString("0", ic) + ", ponowien (gracz wlaczyl bieg) " + _p3Reforce
                         + ", flaga nie weszla " + _p3NoTake + ", oddan biegu " + _p3Out
                         + " | pomiar limitu (do przodu): probek " + _p3Samples + (_p3Samples > 0 ? ", sr. predkosc " + (_p3SpdSum / _p3Samples * 100f).ToString("0", ic)
                            + "% pelnej przy limicie sr. " + (_p3LimSum / _p3Samples * 100f).ToString("0", ic) + "%, ponad limit +10 pp " + _p3Over : "")
                         + " | potkniecia " + _p3Stumbles + " (sesja " + _p3StumblesSession + ")");
            }
            catch (Exception e) { Log.Error("FieldCraft.Line313", e); }
        }

        private static void ResetRunStats()
        {
            Reset313();                         // 313c: liczniki linii "Bieg gracza (313c)"
            _runningStances.Clear();
            _runN = 0; _regenSkipped = 0;
            _runKgSum = 0f; _runKgMax = 0f; _runTSum = 0f; _runTMin = float.MaxValue;
            _plKg = -1f; _plAth = 0f; _plT = 0f; _plFit = 0f;
            _runT292Sum = 0f; _plT292 = 0f;
            _m292Sec = _m292Bar = _m292Exp = _m292PlSec = _m292PlBar = _m292PlExp = 0f;
            PanicRun.Reset();                   // 147: liczniki linii "Bieg zlamanych (147)"
        }

        /// <summary>127: sekundy pelnego biegu do zera, jedna regula dla kazdego czlowieka na polu:
        /// T = RunSecondsUnarmoured x sprawnosc / (1 + k x kg)^2; sprawnosc = 0.75 + Atletyka/400 (0.6..1.6); k = ArmourRunCostPerKgPercent/100
        /// (Askew, Formenti, Minetti 2012: zbroja 30-50 kg = koszt biegu x1.9 -> 2.25%/kg; kwadrat - drozszy krok i nizsze tempo, ktore cialo utrzyma).
        /// Paczka 203 (Jeff 26.10 ok. 22:15: "stamina, bieganie w zbroi za szybko meczy obniz o polowe wzor na meczenie sie w zbroi w stosunku do kg"):
        /// domyslne k 2.25 -> 1.125%/kg (Settings.ArmourRunCostPerKgPercent; Armoury.json Jeffa klucza nie ma). Wzor bez zmian: Atletyka 241 i 46.3 kg = 38 -> 69 s.</summary>
        internal static float RunSecondsFor(float athletics, float armourKg, Settings c)
        {
            return RunSecondsWith(RunFit(athletics, c), armourKg, c.ArmourRunCostPerKgPercent, c);
        }

        /// <summary>Paczka 203: sprawnosc biegacza (0.6..2.5) z Atletyki - wydzielone z RunSecondsFor bez zmiany liczb (linia "Bieg (127)" pokazuje ja dla gracza).</summary>
        private static float RunFit(float athletics, Settings c)
        {
            float fit = 0.75f + BattleWind.CurvedAthletics(Math.Max(0f, athletics), c) / 400f;   // 130: Atletyka po tej samej krzywej co pula RBM
            if (fit < 0.6f) fit = 0.6f;
            if (fit > 2.5f) fit = 2.5f;   // 152: mistrzowie ponad 300 Atletyki (A' rosnie wykladniczo) biegna dluzej - dotad sufit 1.6 (A' 340)
            return fit;
        }

        /// <summary>Paczka 203: T przy danym k (%/kg) - RunSecondsFor z k z ustawien; linia "Bieg (127)" takze z k z 127 do porownania.</summary>
        private static float RunSecondsWith(float fit, float armourKg, float kPercent, Settings c)
        {
            float m = 1f + Math.Max(0f, armourKg) * Math.Max(0f, kPercent) / 100f;
            return Math.Max(5f, Math.Max(10f, c.RunSecondsUnarmoured) * fit / (m * m));
        }

        // ---- most do staminy RBM: sprint pije z TEGO SAMEGO paska, ktory widac w walce ----
        private static bool _rbmInit;
        private static System.Reflection.FieldInfo _rbmValues;    // AgentStances.values
        private static System.Reflection.FieldInfo _rbmStamina;   // Stance.stamina
        private static System.Reflection.FieldInfo _rbmMaxStamina;
        private static System.Reflection.FieldInfo _rbmRegen;     // Stance.staminaRegenPerTick

        private static void RbmInit()
        {
            if (_rbmInit) return;
            _rbmInit = true;
            try
            {
                Type stances = null, stance = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        if (stances == null) stances = asm.GetType("RBMAI.AgentStances");
                        if (stance == null) stance = asm.GetType("RBMAI.Stance");
                    }
                    catch { }
                }
                if (stances == null || stance == null) return;
                _rbmValues = stances.GetField("values", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                _rbmStamina = stance.GetField("stamina", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                _rbmMaxStamina = stance.GetField("maxStamina", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                _rbmRegen = stance.GetField("staminaRegenPerTick", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (_rbmValues != null && _rbmStamina != null)
                    Log.Info("FieldCraft: zadyszka wpieta w stamine RBM.");
            }
            catch (Exception e) { Log.Error("RbmInit", e); }
        }

        private static object RbmStanceOf(Agent agent, State st, float step)
        {
            if (st.RbmStance != null) return st.RbmStance;
            if (st.RbmChecked)
            {
                // RBM potrafi zalozyc agentowi stance JUZ PO naszym pierwszym
                // sprawdzeniu - kto sprawdzil raz i odpuscil, czytal potem wlasna,
                // NIEWIDOCZNA pule, a pasek RBM na ekranie stal na zerze bez kar.
                // Dlatego ponawiamy szukanie co ~2 s, az znajdziemy.
                st.RbmRecheck -= step;
                if (st.RbmRecheck > 0f) return null;
            }
            st.RbmChecked = true;
            st.RbmRecheck = 2f;
            try
            {
                RbmInit();
                if (_rbmValues == null) { st.RbmRecheck = float.MaxValue; return null; }
                var dict = _rbmValues.GetValue(null) as System.Collections.IDictionary;
                if (dict != null && dict.Contains(agent)) st.RbmStance = dict[agent];
            }
            catch { }
            return st.RbmStance;
        }

        /// <summary>
        /// Tempo z oddechu wg Jeffa: pelna para do 10% puli, w ostatnich 10%
        /// czlowiek zwalnia (bieg I ciosy), a przy ZERZE lapie ZADYSZKE:
        /// -50% ruchu, -50% tempa walki (a z nim obrazen) i odsapuje tak
        /// dlugo, az oddech wroci do 10% puli. Dotyczy KAZDEGO czlowieka
        /// na polu - gracza, jego ludzi i wroga.
        /// </summary>
        private static float PaceFromRatio(State st, float ratio, float exitBar)
        {
            // 137: przy oddechu dwufazowym wyjscie z zadyszki dopiero przy pasku >= WindedExitBar (30%) - w 10% puli wraca po 6-9 s, a moc
            // wraca z fosfokreatyna (Bogdanis 1995; szybka faza tlenu 33-35 s, Ozyener 2001); bez oddechu dwufazowego jak dotad 10%
            if (ratio <= 0.005f) st.Winded = true;
            else if (st.Winded && ratio >= exitBar) st.Winded = false;
            if (st.Winded) return 0.5f;
            if (ratio < 0.10f) return 0.5f + 0.5f * (ratio / 0.10f);
            return 1f;
        }

        // 127: czas pelnego biegu z fizjologii (RunSecondsFor), drenaz = cala pula / ten czas - wielkosc puli (Endurance bohaterow,
        // Atletyka w RBM) wydluza WALKE, nie bieg. Dawniej staly drenaz 4 + 1 pkt/kg: bohater z regeneracja 37 pkt/s (x3 po 10 s bez walki)
        // w 46 kg zbroi nie tracil nic. Endurance bohaterow dalej powieksza pule (raz, gdy BattleWind nieczynny).
        private static void MeasureWind(Agent agent, State st)
        {
            var c = Settings.Current;
            float ath = 0f;
            Hero hero = null;
            try
            {
                var co = agent.Character as CharacterObject;
                if (co != null)
                {
                    ath = co.GetSkillValue(TaleWorlds.Core.DefaultSkills.Athletics);
                    if (co.IsHero) hero = co.HeroObject;
                }
            }
            catch { }
            // wykladniczy mnoznik Endurance - ta sama krzywa co BattleWind
            // (2^((END-2.5)/co_ile)); NPC bez bohatera zostaje na x1
            st.EndBonus = 1f;
            try { if (hero != null && !c.StaminaFromAthleticsOnly) st.EndBonus = BattleWind.EnduranceMul(hero); }   // 130: bez Endurance - Atletyka jak u kazdego
            catch { }
            float w = 0f;
            try { w = agent.SpawnEquipment.GetTotalWeightOfArmor(true); } catch { }

            // 292: pelny bieg do zera x mnoznik staminy ludzi - pula RBM jest x K (BattleWind.ApplyHumanMul), a drenaz biegu = pula / RunSeconds, wiec bez tego
            // bieg trwalby tyle samo; ta sama liczba idzie do AiBreath (budzet biegu AI) i PanicRun (pomiar). Linia "Bieg (127)" liczy regule 127/203 (t0)
            float k292 = BattleWind.HumanStaminaMul(c);
            if (Math.Abs(k292 - 1f) <= 0.001f) k292 = 1f;
            float t0 = RunSecondsFor(ath, w, c);
            st.RunSeconds = t0 * k292;
            st.RunSeconds0 = t0;
            _runN++;
            _runKgSum += w;
            if (w > _runKgMax) _runKgMax = w;
            _runTSum += t0;
            _runT292Sum += st.RunSeconds;
            if (t0 < _runTMin) _runTMin = t0;
            if (agent.IsMainAgent) { _plKg = w; _plAth = ath; _plT = t0; _plT292 = st.RunSeconds; _plFit = RunFit(ath, c); }
            st.TiredFloor = Math.Min(0.92f, c.TiredSpeedFactor + ath / 1000f);

            // wlasna pula (gdy RBM nieobecny) - te same zasady co RBM: 1000 * bonus z Atletyki
            float athCap = Math.Min(ath, 500f);
            // 292: wlasna pula i jej regeneracja x K - jak pula i regeneracja RBM (BattleWind.ApplyHumanMul); RegenPerSec oddaje tez dlug biegu przy pasku RBM bez oddechu 137
            st.PoolMax = 1000f * (1f + 3f * athCap / 500f) * st.EndBonus * k292;
            st.Pool = st.PoolMax;
            // regeneracja rosnie z Atletyka (jak w RBM) ORAZ z Endurance bohatera
            st.RegenPerSec = Math.Max(1f, c.StaminaRegenPerSecond) * (1f + 2f * athCap / 500f) * st.EndBonus * k292;
        }

        private static bool _sprintLogged;
        private readonly Dictionary<Agent, State> _states = new Dictionary<Agent, State>();

        // 139 (25.10): FieldCraft biezacej misji - odczyt zadyszki dla pomiaru losu zlamanych (BrokenFate, "z zadyszka"). Tylko odczyt, glowny watek
        // (FieldCraft pisze st.Winded w OnMissionTick, BrokenFate czyta w ticku RoutKeeper). Ustawiane w AfterStart, zerowane na koniec misji.
        private static FieldCraft _live;

        internal static bool LiveIn(Mission m)
        {
            var f = _live;
            return f != null && m != null && ReferenceEquals(f.Mission, m);
        }

        internal static bool WindedNow(Agent a)
        {
            var f = _live;
            State st;
            return f != null && a != null && f._states.TryGetValue(a, out st) && st.Winded;
        }

        // ---- 313d: odczyty dla BrokenMen (rzut odwrotu 143) i RoutKeeper (powod "z bolu") - glowny watek

        /// <summary>Recenzja 313d: BrokenMen przed rzutami odwrotu 143 zdejmuje kolejke trafien (glowny watek) - licznik ran (HitsOf) jest juz po ciosie, ktory
        /// zbil rannego ponizej progu; inaczej ta sama rana dostawala drugi rzut po ProcessHits (co 0.2 s). Kilka wywolan na klatke - bez szkody (kolejka pusta).</summary>
        internal static void DrainHits(Settings c)
        {
            var f = _live;
            if (f == null || c == null) return;
            try { f.ProcessHits(c); }
            catch (Exception e) { Bleed313.Stumble("Drain", e); }
        }
        internal static int HitsOf(Agent a)
        {
            var f = _live;
            State st;
            return f != null && a != null && f._states.TryGetValue(a, out st) ? st.Hits : 0;
        }

        /// <summary>Czlowiek krwawi teraz: regula BleedFromWounds - rana krwawi (BleedRate &gt; 0); stara regula - HP &lt;= BleedBelowHp.</summary>
        internal static bool BleedingNow(Agent a)
        {
            var c = Settings.Current;
            if (a == null || c == null) return false;
            if (!c.BleedFromWounds) return a.Health <= c.BleedBelowHp;
            var f = _live;
            State st;
            return f != null && f._states.TryGetValue(a, out st) && st.BleedRate > 0f;
        }

        internal static float AthOf(Agent a)
        {
            var f = _live;
            State st;
            if (f != null && a != null && f._states.TryGetValue(a, out st))
            {
                if (st.Ath < 0f) st.Ath = ReadAth(a);
                return st.Ath;
            }
            return ReadAth(a);
        }

        internal static bool PainBrokeNow(Agent a)
        {
            var f = _live;
            State st;
            return f != null && a != null && f._states.TryGetValue(a, out st) && st.PainBroken;
        }

        private static float ReadAth(Agent a)
        {
            try { var ch = a != null ? a.Character : null; return ch != null ? ch.GetSkillValue(TaleWorlds.Core.DefaultSkills.Athletics) : 100f; }
            catch { return 100f; }
        }

        public override void OnRemoveBehavior()
        {
            if (ReferenceEquals(_live, this)) _live = null;   // 139
            try { AiBreath.Abort(); } catch { }               // 144: latka nie czyta capu po usunieciu zachowania
            base.OnRemoveBehavior();
        }
        private readonly List<KeyValuePair<Agent, int>> _unstick = new List<KeyValuePair<Agent, int>>();
        // 309 (soczewka wydajnosci): wspolny bufor migawki Mission.Agents zamiast nowej listy co takt 0.2 s (alokacja ok. 600-1500 wpisow na takt w duzej bitwie)
        private readonly List<Agent> _snap = new List<Agent>(512);
        // 309: zapisy zamachu do silnika (UpdateCustomDrivenProperties) - wywolane / pominiete bez zmiany / ponowione po nadpisaniu przez gre lub RBM
        internal static int SwingWrites, SwingSkips, SwingRewrites;
        internal static void ResetSwingCounters() { SwingWrites = SwingSkips = SwingRewrites = 0; }
        // czyj to wierzchowiec: kon -> bohater z klanu gracza (spisujemy w locie)
        private readonly Dictionary<Agent, Hero> _mountOwners = new Dictionary<Agent, Hero>();
        private float _accum;

        public override void OnAgentDeleted(Agent affectedAgent)
        {
            try
            {
                State gone;
                if (_states.TryGetValue(affectedAgent, out gone) && gone.RbmStance != null) _runningStances.Remove(gone.RbmStance);
                if (gone != null && gone.Ai != null) AiBreath.CloseRun(gone.Ai);   // 144: bieg do licznikow, wpis tablicy capu zwolniony
                AiBreath.Forget(affectedAgent);
                WindedShow.Forget(affectedAgent);
                Bleed313.SetLimp(affectedAgent, 0f);   // 313d: cap kustykania zdjety
                _states.Remove(affectedAgent); _mountOwners.Remove(affectedAgent);
            }
            catch { }
        }

        // 144 (B1): panika i ucieczka - cap oddechu zdjety od razu (po Agent.OnFleeing czlowiek nie ma szyku, gra juz nie liczy mu limitu - cap zostalby na zawsze)
        public override void OnAgentPanicked(Agent affectedAgent)
        {
            try { AiBreath.Drop(affectedAgent); } catch { }
        }

        public override void OnAgentFleeing(Agent affectedAgent)
        {
            try { AiBreath.Drop(affectedAgent); } catch { }
        }

        /// <summary>
        /// Martwy kon zostaje martwy. Vanilla po bitwie "wskrzesza" wierzchowca
        /// gracza - u nas kon zabity w PRAWDZIWEJ walce (nie na arenie i nie
        /// w turnieju) znika z ekwipunku na zawsze. Uprzaz zdejmujesz z trupa.
        /// Dotyczy bohaterow klanu gracza (Ty i towarzysze).
        /// </summary>
        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            try
            {
                var c = Settings.Current;
                if (!c.HorseDeathPermanent) return;
                if (affectedAgent == null || !affectedAgent.IsMount) return;
                if (agentState != AgentState.Killed) return;
                if (Mission.CombatType != Mission.MissionCombatType.Combat) return;   // arena/turniej: konie pozyczone

                Hero owner = null;
                var rider = affectedAgent.RiderAgent;
                if (rider != null)
                {
                    var co = rider.Character as CharacterObject;
                    if (co != null && co.IsHero) owner = co.HeroObject;
                }
                if (owner == null) _mountOwners.TryGetValue(affectedAgent, out owner);
                if (owner == null || owner.Clan != Clan.PlayerClan) return;

                var eq = owner.BattleEquipment;
                if (eq == null || eq[EquipmentIndex.Horse].IsEmpty) return;
                var horse = eq[EquipmentIndex.Horse].Item;
                eq[EquipmentIndex.Horse] = default(EquipmentElement);

                string who = owner == Hero.MainHero ? "Your" : owner.Name + "'s";
                TaleWorlds.Library.InformationManager.DisplayMessage(new TaleWorlds.Library.InformationMessage(
                    who + " " + (horse != null ? horse.Name.ToString() : "horse") + " was killed. Dead horses stay dead - the harness came off the corpse.",
                    TaleWorlds.Library.Colors.Red));
                Log.Info("FieldCraft: kon bohatera " + owner.Name + " zabity w walce - usuniety z ekwipunku.");
            }
            catch (Exception e) { Log.Error("FieldCraft.HorseDeath", e); }
        }

        // ---- ksiega zuzycia: KTORY kawalek sprzetu gracza ucierpial i od czego.
        // Zbroja zuzywa sie TAM, GDZIE pada cios; bron - od kazdego celnego
        // uderzenia; tarcza - od blokow; uprzaz - gdy obrywa kon.
        // ArmouryBehavior konsumuje to po bitwie. ----
        internal static readonly float[] WearLedger = new float[12];

        internal static void TakeLedger(float[] into)
        {
            for (int i = 0; i < 12; i++) { into[i] = WearLedger[i]; WearLedger[i] = 0f; }
        }

        /// <summary>m110-stan krok 2 (CompanionGearWear): ksiega ciosow agenta - gracz WearLedger (jak dotad), towarzysz w druzynie gracza zarejestrowany przy wejsciu
        /// do bitwy (HeroHp, zgodna zbroja) - jego ksiega misji (koniec misji przenosi ja do HeroBook); reszta (i klucz NIE) - null.</summary>
        internal static float[] LedgerOf(Agent a)
        {
            if (a == null) return null;
            if (a.IsMainAgent) return WearLedger;
            return HeroHp.LedgerOf(a);
        }

        // E8/A (250, PLAN 8.8e, regula R4 "arena i turniej nie obijaja"): ksiegi zuzycia (WearLedger bohatera i TroopWearLedger wojska) pisza tylko w BITWIE
        // kampanii - misja z bitwa gracza na mapie (MapEvent.PlayerMapEvent) i CombatType Combat. Sam CombatType nie wystarcza: zaden zarzadzany kod gry
        // 1.4.8 go nie ustawia (recenzja E8/X), wiec arena i turniej moga miec Combat; rozstrzyga bitwa na mapie - ten sam warunek co strzal w
        // XpFair.ShotPrefix. Arena, turniej, cwiczenia, bojka w zaulku, ucieczka z lochu nie maja bitwy na mapie - ciosy z nich nie ida do ksiag.
        // Ksiega bohatera jest tez zerowana na poczatku kazdej bitwy gracza i po bitwie przegranej albo opuszczonej (ArmouryBehavior) - dotad ciosy spoza
        // bitwy i z kleski czekaly w niej do najblizszego zwyciestwa. Klucz ArenaNoWear.
        private int _battle = -1;                 // -1 nie sprawdzone, 0 misja poza bitwa, 1 bitwa kampanii (stale przez cala misje)
        private int _skipHits, _skipShots;        // ciosy / strzaly bohatera pominiete w tej misji (misja poza bitwa)

        private bool InBattle()
        {
            if (_battle < 0)
            {
                try
                {
                    _battle = TaleWorlds.CampaignSystem.MapEvents.MapEvent.PlayerMapEvent != null
                              && Mission.CombatType == Mission.MissionCombatType.Combat ? 1 : 0;
                }
                catch { return true; }            // nie wiadomo - jak dotad (ksiegi pisza); sprawdzimy przy nastepnym ciosie
            }
            return _battle == 1;
        }

        /// <summary>E8/A: suma ksiegi bohatera i jej wyzerowanie (poczatek bitwy gracza, bitwa przegrana albo opuszczona).</summary>
        internal static float DropLedger()
        {
            float sum = 0f;
            for (int i = 0; i < 12; i++) { sum += WearLedger[i]; WearLedger[i] = 0f; }
            return sum;
        }

        /// <summary>292 (recenzja): koncowka linii "Bieg (127)" - POMIAR z postaw RBM: (1) pasek w ciaglym biegu (sekundy, pelny pasek w X s, krotnosc wobec
        /// reguly sprzed 292 = oczekiwany spadek / zmierzony), ludzie i gracz; (2) sr. maxStamina RBM zywych ludzi na koniec misji (odczyt postawy, bez umarlych).
        /// Tylko odczyt; wolane z OnEndMission przed ResetRunStats.</summary>
        private string Measured292()
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new System.Text.StringBuilder(260);
            sb.Append(" | 292 zmierzone: bieg ludzi ").Append(_m292Sec.ToString("0", inv)).Append(" s");
            if (_m292Sec >= 1f && _m292Bar > 0.0001f)
                sb.Append(", pelny pasek w ").Append((_m292Sec / _m292Bar).ToString("0", inv)).Append(" s, x").Append((_m292Exp / _m292Bar).ToString("0.00", inv))
                  .Append(" wobec reguly sprzed 292");
            else sb.Append(", pelny pasek w - s");
            sb.Append(", gracz ").Append(_m292PlSec.ToString("0", inv)).Append(" s");
            if (_m292PlSec >= 1f && _m292PlBar > 0.0001f)
                sb.Append(" (pelny pasek w ").Append((_m292PlSec / _m292PlBar).ToString("0", inv)).Append(" s, x").Append((_m292PlExp / _m292PlBar).ToString("0.00", inv)).Append(')');
            int n = 0;
            double sum = 0.0;
            if (_rbmMaxStamina != null)
                foreach (var kv in _states)
                {
                    var a = kv.Key;
                    var st = kv.Value;
                    if (a == null || st == null || st.RbmStance == null) continue;
                    try
                    {
                        if (!a.IsActive() || !a.IsHuman || Undead.Character(a.Character)) continue;
                        sum += (float)_rbmMaxStamina.GetValue(st.RbmStance);
                        n++;
                    }
                    catch { }
                }
            sb.Append(" | pula RBM zywych ludzi na koniec: ludzi ").Append(n);
            if (n > 0) sb.Append(", sr. ").Append((sum / n).ToString("0", inv));
            return sb.ToString();
        }

        protected override void OnEndMission()
        {
            try
            {
                if (_skipHits + _skipShots > 0)
                    Log.Info("Zuzycie (E8/A): misja poza bitwa (arena, turniej, bojka) - " + _skipHits + " ciosow i " + _skipShots
                             + " strzalow bohatera nie poszlo do ksiegi zuzycia; rynsztunek bez zmian.");
                _skipHits = 0; _skipShots = 0;
            }
            catch { }
            // 292 (recenzja): misja krotsza niz 3 s czasu misji (autotest, odwrot) - linia "Stamina (292)" teraz, przed "Bieg (127)" (raz na misje jak dotad)
            if (!_k292Said && _runN > 0)
            {
                _k292Said = true;
                try
                {
                    var ma = Mission.MainAgent;
                    BattleWind.StartLine292(Mission, Settings.Current, _runN, _runTSum, _runT292Sum, _plKg >= 0f ? _plT : 0f, _plKg >= 0f ? _plT292 : 0f,
                                            ma == null || ma.MountAgent == null, true);
                }
                catch (Exception e) { RoutHold.Stumble(RoutHold.SiteRun, "BattleWind.StartLine292", e); }
            }
            try
            {
                var c = Settings.Current;
                if (_runN > 0 && c != null)
                    Log.Info("Bieg (127): ludzi zmierzonych " + _runN + " | zbroja sr. " + (_runKgSum / _runN).ToString("0.0") + " kg (najwiecej "
                             + _runKgMax.ToString("0.0") + ") | pelny bieg do zera sr. " + (_runTSum / _runN).ToString("0") + " s (najkrocej "
                             + _runTMin.ToString("0") + ") | regula: " + c.RunSecondsUnarmoured.ToString("0") + " s bez zbroi przy Atletyce 100, "
                             + c.ArmourRunCostPerKgPercent.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "%/kg do kwadratu (203: domyslnie 1.125 = polowa "
                             + ArmourRunCost127.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " z 127) | bez oddechu w biegu " + (c.NoBreathWhileRunning ? "TAK" : "NIE")
                             + " (pominiete regeneracje RBM " + _regenSkipped + ")"
                             + (_plKg >= 0f ? " | gracz: zbroja " + _plKg.ToString("0.0") + " kg, Atletyka " + _plAth.ToString("0") + " -> " + _plT.ToString("0") + " s"
                                + " (sprawnosc " + _plFit.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + ", przy " + ArmourRunCost127.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                                + "%/kg z 127 bylo " + RunSecondsWith(_plFit, _plKg, ArmourRunCost127, c).ToString("0") + " s)" : " | gracz: -")
                             // 292: dopisek na koncu (wyrazenie p203 konczy sie na segmencie gracza) - pelny bieg po mnozniku staminy ludzi
                             + " | 292: stamina ludzi x" + BattleWind.HumanStaminaMul(c).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                             + ", pelny bieg do zera sr. " + (_runT292Sum / _runN).ToString("0") + " s" + (_plKg >= 0f ? ", gracz " + _plT292.ToString("0") + " s" : ", gracz -")
                             + Measured292());
            }
            catch { }
            // 144: linia "Oddech AI (144)" raz na bitwe - biegi w toku zamkniete, capy zdjete
            try
            {
                foreach (var kv in _states) if (kv.Value != null && kv.Value.Ai != null) AiBreath.CloseRun(kv.Value.Ai);
                AiBreath.End(Mission);
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteBreath, "AiBreath.End", e); }
            // 147: linia "Bieg zlamanych (147)" raz na misje - kto jest jeszcze w panice na polu, potem liczniki
            try
            {
                foreach (var kv in _states) if (kv.Value != null && kv.Value.Pr != null) PanicRun.AtEnd(kv.Key, kv.Value.Pr, kv.Value.Winded);
                PanicRun.Line(Mission, Settings.Current);
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteRun, "PanicRun.Line", e); }
            Line313(Settings.Current);                        // 313c: linia "Bieg gracza (313c)"
            try { WeaponEase.Flush(Mission); } catch { }      // 313c: linia "Wprawa (WeaponEase)" raz na misje
            // 313d: linia "Krwawienie (313d)" raz na misje - kto jeszcze krwawi, potem capy kustykania zdjete
            try
            {
                foreach (var kv in _states)
                    if (kv.Value != null && kv.Value.BleedRate > 0f && kv.Key != null && kv.Key.IsActive()) Bleed313.BleedingAtEnd++;
                if (InBattle()) Bleed313.NoteBattle();
                Bleed313.Line(Settings.Current);
            }
            catch (Exception e) { Bleed313.Stumble("Line", e); }
            try { Bleed313.ClearLimp(); } catch { }
            try { Bleed313.EndMission(); } catch { }          // recenzja 313d: kolejka trafien i zbiory ludzi oproznione (bez referencji do agentow)
            ResetRunStats();
            try { BattleWind.BreathFlush(); } catch { }      // 137: linia "Oddech (137)" raz na misje
            try { MissionTeams.Flush(Mission); } catch { }   // NM: linia "Druzyny w misji (NM)" raz na misje
            if (ReferenceEquals(_live, this)) _live = null;   // 139: bez odczytu zadyszki po koncu misji
            base.OnEndMission();
        }

        // NM: liczniki misji od poczatku misji (rodzaj misji i bitwa na mapie liczone raz)
        public override void AfterStart()
        {
            try { MissionTeams.Begin(Mission); } catch { }
            ResetRunStats();
            _k292Said = false;                                // 292: linia "Stamina (292)" kilka sekund po starcie misji
            try { _k292From = Mission.CurrentTime; } catch { _k292From = 0f; }
            try { AiBreath.Begin(Mission); } catch { }        // 144: liczniki i capy oddechu AI od zera
            try { Bleed313.Begin(); } catch { }               // 313d: liczniki, kolejka trafien i capy kustykania od zera
            _live = this;                                     // 139: odczyt zadyszki dla BrokenFate
            base.AfterStart();
        }

        internal static int ArmorSlotFor(BoneBodyPartType part, Agent a)   // m110-stan: takze LootWear (ta sama czesc ciala -> slot zbroi co u gracza)
        {
            switch (part)
            {
                case BoneBodyPartType.Head:
                case BoneBodyPartType.Neck: return 5;                        // helm
                case BoneBodyPartType.ShoulderLeft:
                case BoneBodyPartType.ShoulderRight:
                    try { if (a.SpawnEquipment[EquipmentIndex.Cape].Item != null) return 9; } catch { }
                    return 6;                                                // peleryna albo kirys
                case BoneBodyPartType.ArmLeft:
                case BoneBodyPartType.ArmRight: return 8;                    // rekawice
                case BoneBodyPartType.Legs: return 7;                        // nogawice
                default: return 6;                                           // kirys
            }
        }

        private static int WeaponSlotFor(Agent a, ItemObject item)
        {
            if (item == null) return -1;
            try
            {
                for (int i = 0; i < 4; i++)
                    if (a.SpawnEquipment[(EquipmentIndex)i].Item == item) return i;
            }
            catch { }
            return -1;
        }

        /// <summary>
        /// Luk zuzywa sie OD STRZELANIA - kazdy strzal kosztuje, trafienie czy pudlo.
        /// Wytrzymalosc wg Jeffa: tier 1 = 2500 strzalow, kazdy tier mnozy (t6 = x6),
        /// a kazdy punkt umiejetnosci Bow/Crossbow dodaje +1% strzalow.
        /// </summary>
        public override void OnAgentShootMissile(Agent shooterAgent, EquipmentIndex weaponIndex, Vec3 position, Vec3 velocity, Mat3 orientation, bool hasRigidBody, int forcedMissileIndex)
        {
            try
            {
                var c = Settings.Current;
                // E8/A: poza bitwa kampanii (arena, turniej, cwiczenia) strzal nie zuzywa luku bohatera ani wojska - cala metoda pisze tylko do ksiag
                if (c.ArenaNoWear && !InBattle())
                {
                    if (c.WearEnabled && shooterAgent != null && shooterAgent.IsMainAgent) _skipShots++;
                    return;
                }
                // wpis 96: strzal zolnierza partii gracza - luk/kusza
                if (c.TroopWearEnabled && shooterAgent != null && shooterAgent.IsHuman && !shooterAgent.IsHero && BattlefieldLaw.IsMainPartyAgent(shooterAgent))
                {
                    var ti = shooterAgent.SpawnEquipment[weaponIndex].Item;
                    if (ti != null && (ti.ItemType == ItemObject.ItemTypeEnum.Bow || ti.ItemType == ItemObject.ItemTypeEnum.Crossbow)) TroopWearLedger.Add(ti.ItemType, 1f);
                }
                if (!c.WearEnabled || shooterAgent == null) return;
                var led = LedgerOf(shooterAgent);   // m110-stan krok 2: gracz albo towarzysz z ksiega misji (CompanionGearWear)
                if (led == null) return;
                var item = shooterAgent.SpawnEquipment[weaponIndex].Item;
                if (item == null) return;
                if (item.ItemType != ItemObject.ItemTypeEnum.Bow && item.ItemType != ItemObject.ItemTypeEnum.Crossbow) return;

                int tier = Recipes.Grade(item);
                int skill = 0;
                try
                {
                    var who = shooterAgent.IsMainAgent ? Hero.MainHero : (HeroHp.HeroOf(shooterAgent) ?? Hero.MainHero);   // umiejetnosc strzelca
                    skill = who.GetSkillValue(item.ItemType == ItemObject.ItemTypeEnum.Bow
                        ? TaleWorlds.Core.DefaultSkills.Bow : TaleWorlds.Core.DefaultSkills.Crossbow);
                }
                catch { }
                float uses = Math.Max(100, c.BowUsesAtTier1) * tier
                           * (1f + skill * Math.Max(0f, c.BowSkillBonusPercentPerPoint) / 100f);
                // ApplyWear dzieli potem przez solidnosc tieru - kompensujemy, zeby liczby Jeffa trzymaly co do sztuki
                float sturdiness = 1f + Math.Max(0, tier - 1) * Settings.Current.TierDurabilityFactor;
                int slot = (int)weaponIndex;
                if (slot >= 0 && slot < 4) led[slot] += 100f / uses * sturdiness;
            }
            catch { }
        }

        public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData attackCollisionData)
        {
            try
            {
                var c = Settings.Current;
                // E8/A: ksiegi zuzycia pisza tylko w bitwie kampanii (arena, turniej, bojka - nie); odpinanie strzal nizej dziala wszedzie jak dotad
                bool counts = !c.ArenaNoWear || InBattle();

                // 313d: rana do kolejki (pod zamkiem - OnAgentHit bywa wolany z ticku asynchronicznego); rzut krwawienia i liczniki ran w ticku na glownym watku.
                // Tylko czlowiek, cios z obrazeniami, nie blok tarcza, tylko bitwa na mapie (turniej, arena, bojka - bez krwawienia i rzutow)
                // Recenzja 313d: bez ciosu od siebie bez energii - Artem (KnockBackAnimation) wola RegisterBlow z Blow(agent.Index), InflictedDamage 1, BaseMagnitude 0
                if ((c.BleedFromWounds || c.PainBreakByChance || c.WoundedFleeByChance) && affectedAgent != null && affectedAgent.IsHuman
                    && blow.InflictedDamage > 0 && !attackCollisionData.AttackBlockedWithShield
                    && !(blow.OwnerId == affectedAgent.Index && blow.BaseMagnitude <= 0f) && InBattle())
                {
                    try
                    {
                        Bleed313.Enqueue(new Bleed313.Hit
                        {
                            V = affectedAgent, By = blow.OwnerId,
                            ByA = affectorAgent != null && affectorAgent.Index == blow.OwnerId ? affectorAgent : null,
                            Dmg = blow.InflictedDamage, Abs = Math.Max(0, attackCollisionData.AbsorbedByArmor),
                            Type = blow.DamageType, Part = blow.VictimBodyPart, Missile = blow.IsMissile
                        });
                    }
                    catch { }
                }

                if (!counts && c.WearEnabled && blow.InflictedDamage + Math.Max(0, attackCollisionData.AbsorbedByArmor) > 0
                    && ((affectedAgent != null && affectedAgent.IsMainAgent) || (affectorAgent != null && affectorAgent.IsMainAgent)))
                    _skipHits++;

                // wpis 96: zuzycie WOJSKA z przebiegu walki - trafienia zolnierzy partii gracza (nie bohaterow)
                if (c.TroopWearEnabled && counts)
                {
                    try
                    {
                        int raw0 = blow.InflictedDamage + Math.Max(0, attackCollisionData.AbsorbedByArmor);
                        if (raw0 > 0 && affectedAgent != null && affectedAgent.IsHuman && !affectedAgent.IsHero && BattlefieldLaw.IsMainPartyAgent(affectedAgent))
                        {
                            if (attackCollisionData.AttackBlockedWithShield) TroopWearLedger.Add(ItemObject.ItemTypeEnum.Shield, 1f);
                            else TroopWearLedger.Add(TroopWearLedger.TypeOfSlot(ArmorSlotFor(blow.VictimBodyPart, affectedAgent)),
                                blow.IsMissile ? Math.Max(0f, Math.Min(100f, c.MissileArmorWearPercent)) / 100f : 1f);
                        }
                        if (raw0 > 0 && affectorAgent != null && affectorAgent.IsHuman && !affectorAgent.IsHero && affectorWeapon.Item != null
                            && !blow.IsMissile && BattlefieldLaw.IsMainPartyAgent(affectorAgent))
                            TroopWearLedger.Add(affectorWeapon.Item.ItemType, 1f);
                    }
                    catch { }
                }

                // --- zuzycie sprzetu gracza: tylko od faktycznych zdarzen ---
                if (c.WearEnabled && counts)
                {
                    int raw = blow.InflictedDamage + Math.Max(0, attackCollisionData.AbsorbedByArmor);
                    // strzala robi w pancerzu dziurke, nie wyrwe - liczymy tylko ulamek
                    // jej obrazen jako zuzycie (obrazenia HP zostaja normalne)
                    float missileScale = blow.IsMissile
                        ? Math.Max(0f, Math.Min(100f, c.MissileArmorWearPercent)) / 100f
                        : 1f;
                    // m110-stan krok 2: ksiega ofiary - gracz (WearLedger, jak dotad) albo towarzysz z ksiega misji (CompanionGearWear)
                    var vled = affectedAgent != null && raw > 0 ? LedgerOf(affectedAgent) : null;
                    if (vled != null)
                    {
                        if (attackCollisionData.AttackBlockedWithShield)
                        {
                            int sh = -1;
                            try
                            {
                                for (int i = 0; i < 4; i++)
                                {
                                    var it = affectedAgent.SpawnEquipment[(EquipmentIndex)i].Item;
                                    if (it != null && it.ItemType == ItemObject.ItemTypeEnum.Shield) { sh = i; break; }
                                }
                            }
                            catch { }
                            // TA SAMA ULGA CO W BITWIE (Jeff 13.09): tarcza zatrzymujaca
                            // strzale liczyla PELNE zuzycie, bez znizki dla pociskow, ktora
                            // dostaje pancerz linijke nizej - a przy wlaczonym "Break At Zero
                            // Condition" dojechana do zera tarcza znika z ekwipunku na zawsze.
                            // Strzaly zuzywaja tarcze w tym samym procencie, w jakim ja lupia.
                            float shieldMissile = blow.IsMissile
                                ? Math.Max(0f, Math.Min(100f, c.MissileShieldDamagePercent)) / 100f
                                : 1f;
                            if (sh >= 0) vled[sh] += raw * c.WearDamageFactor * Math.Max(0f, c.WearShieldFactor) * shieldMissile;
                        }
                        else
                        {
                            int slot = ArmorSlotFor(blow.VictimBodyPart, affectedAgent);
                            // model Jeffa: do ksiegi idzie SUROWY cios - na stan przeliczy go
                            // pula wytrzymalosci sztuki (pancerz x 20 x tier), jeden za jeden
                            vled[slot] += raw * missileScale;
                        }
                    }
                    // Twoj celny cios zuzywa TWOJA bron BIALA; luki i kusze placa
                    // ZA STRZAL (OnAgentShootMissile), nie za trafienie
                    // m110-stan krok 2: takze bron towarzysza z ksiega misji (LedgerOf)
                    var aled = affectorAgent != null && raw > 0
                        && affectorWeapon.Item != null
                        && affectorWeapon.Item.ItemType != ItemObject.ItemTypeEnum.Bow
                        && affectorWeapon.Item.ItemType != ItemObject.ItemTypeEnum.Crossbow
                        && affectorWeapon.Item.ItemType != ItemObject.ItemTypeEnum.Thrown ? LedgerOf(affectorAgent) : null;
                    if (aled != null)
                    {
                        int ws = WeaponSlotFor(affectorAgent, affectorWeapon.Item);
                        if (ws >= 0) aled[ws] += Math.Max(0f, c.WearWeaponPerHit);
                    }
                    // kon gracza obrywa - cierpi uprzaz (ulamek, nie polowa:
                    // kon to wielki cel i przy 0.5 siodla sypaly sie seriami); m110-stan krok 2: takze kon towarzysza z ksiega misji
                    var hled = affectedAgent != null && affectedAgent.IsMount && affectedAgent.RiderAgent != null && raw > 0 ? LedgerOf(affectedAgent.RiderAgent) : null;
                    if (hled != null)
                        hled[11] += raw * Math.Max(0f, c.HarnessWearFactor) * missileScale;
                }

                if (!c.ArrowUnstickEnabled || affectedAgent == null || !affectedAgent.IsHuman) return;
                if (!blow.IsMissile) return;
                if (attackCollisionData.AttackBlockedWithShield) return;      // w tarczy strzaly zostaja
                // OSZCZEP TO NIE STRZALA (Jeff 02.09: "jak dostaje wlocznia
                // rzucana, nie rob tak, ze tkwi w moim ciele - wlocznie tkwia,
                // jak zabijesz kogos albo obrazenia sa powyzej 80"): bron
                // miotana (oszczep, topor, noz) ma wlasny, wysoki prog;
                // zabity zostaje z pociskiem sam z siebie (martwy agent nie
                // wchodzi do odpinania w ticku).
                bool thrown = false;
                try
                {
                    var wc = (!affectorWeapon.IsEmpty && affectorWeapon.CurrentUsageItem != null) ? affectorWeapon.CurrentUsageItem.WeaponClass : WeaponClass.Undefined;
                    thrown = wc == WeaponClass.Javelin || wc == WeaponClass.ThrowingAxe || wc == WeaponClass.ThrowingKnife;
                }
                catch { }
                int minStick = thrown ? c.JavelinStickMinDamage : c.ArrowStickMinDamage;
                if (blow.InflictedDamage >= minStick) return;                  // weszla naprawde - tkwi
                _unstick.Add(new KeyValuePair<Agent, int>(affectedAgent, 0));
            }
            catch { }
        }

        // 309: koszt klatki (FrameCost309) - pomiar calego ticku, zachowanie bez zmian
        public override void OnMissionTick(float dt)
        {
            long t309 = FrameCost309.T();
            try { Tick309(dt); }
            finally { FrameCost309.Add(FrameCost309.MFieldCraft, t309); }
        }

        private void Tick309(float dt)
        {
            try
            {
                var c = Settings.Current;
                _accum += dt;

                // odpiecie strzal-drasniec (tick pozniej, zeby silnik zdazyl je przypiac)
                if (_unstick.Count > 0)
                {
                    for (int i = _unstick.Count - 1; i >= 0; i--)
                    {
                        var a = _unstick[i].Key;
                        _unstick.RemoveAt(i);
                        if (a == null || !a.IsActive()) continue;
                        int n = a.GetAttachedWeaponsCount();
                        if (n > 0) a.DeleteAttachedWeapon(n - 1);
                    }
                }

                if (_accum < 0.2f) return;                    // reszta co 0.2 s wystarczy
                float step = _accum; _accum = 0f;

                // 313d: rany z kolejki OnAgentHit - rzut krwawienia (BleedFromWounds), liczniki ran dla rzutow bolu i odwrotu
                ProcessHits(c);

                // 144: AI oszczedza sily (AiBreath - decyzja co 0.6 s na czlowieka) i zadyszke widac / slychac (WindedShow) - bramki na takt
                float now = Mission.CurrentTime;
                bool aiLive = false, show = false;
                try { aiLive = AiBreath.Gate(Mission, c, now); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteBreath, "AiBreath.Gate", e); }
                try { show = WindedShow.Gate(Mission, c); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteBreath, "WindedShow.Gate", e); }
                int aiTick = AiBreath.Tick;
                long aiCost = 0;

                // MIGAWKA LISTY (log 15.09: "Collection was modified" x25 w OnMissionTick):
                // agent.Die() przy wykrwawieniu usuwa agenta z Mission.Agents W TRAKCIE
                // tej petli - reszta ludzi nie dostawala tiku. Iterujemy po kopii.
                _snap.Clear();
                _snap.AddRange(Mission.Agents);
                bool swingOnChange = c.FieldCraftSwingOnChange;
                for (int ai = 0; ai < _snap.Count; ai++)
                {
                    var agent = _snap[ai];
                    if (agent == null || !agent.IsActive() || !agent.IsHuman) continue;
                    // wight nie zna zadyszki, ran, krwawienia ani strachu - trup
                    // maszeruje i bije, poki go nie potna na kawalki
                    if (Undead.Character(agent.Character)) continue;
                    State st;
                    if (!_states.TryGetValue(agent, out st)) { st = new State(); _states[agent] = st; }

                    // spisz wlasciciela wierzchowca (na wypadek, gdy kon ginie bez jezdzca na grzbiecie)
                    if (agent.MountAgent != null && !_mountOwners.ContainsKey(agent.MountAgent))
                    {
                        try
                        {
                            var mco = agent.Character as CharacterObject;
                            if (mco != null && mco.IsHero && mco.HeroObject != null && mco.HeroObject.Clan == Clan.PlayerClan)
                                _mountOwners[agent.MountAgent] = mco.HeroObject;
                        }
                        catch { }
                    }

                    float hpr = agent.HealthLimit > 0f ? agent.Health / agent.HealthLimit : 1f;
                    bool sprintNow = false;      // 144: bieg w tym takcie (AiBreath: sekundy biegu wedlug sytuacji)
                    float speedNow = -1f;        // 144: predkosc pieszego (-1 = konny albo nieliczona)

                    // 313d (klucz WoundedLimp, Jeff: ranny "nie biega jak glupi tylko wolno wycofuje sie"): pieszy AI w odwrocie 143 albo z HP <= WoundedLimpBelowHp
                    // (takze w panice) - cap kustykania (Bleed313: tablica dla latki AdjustSpeedLimit), wpis limitu nizej w sekcji ruchu
                    float limpCap = 0f;
                    if (c.WoundedLimp || st.LimpCap > 0f)
                    {
                        try { limpCap = c.WoundedLimp ? LimpNow(agent, st, c) : 0f; }
                        catch (Exception e) { limpCap = 0f; Bleed313.Stumble("Limp", e); }
                        // recenzja 313d (pomiar): predkosc czlowieka, ktory ma cap od co najmniej LimpSettle s (limit z poprzednich taktow juz dziala)
                        if (limpCap > 0f && st.LimpCap > 0f && st.LimpSince >= 0f && now - st.LimpSince >= Bleed313.LimpSettle)
                        {
                            try
                            {
                                float v = agent.Velocity.AsVec2.Length;
                                if (v >= 0f && v < 50f)
                                {
                                    Bleed313.LimpSpeedN++;
                                    Bleed313.LimpSpeedSum += v;
                                    if (v > Bleed313.LimpSpeedMax) Bleed313.LimpSpeedMax = v;
                                    if (v > st.LimpCap * 1.25f + 0.2f) Bleed313.LimpFast++;
                                }
                            }
                            catch (Exception e) { Bleed313.Stumble("LimpSpeed", e); }
                        }
                        if (Math.Abs(limpCap - st.LimpCap) > 0.001f)
                        {
                            if (st.LimpCap <= 0f && limpCap > 0f) st.LimpSince = now;
                            Bleed313.SetLimp(agent, limpCap);
                            st.LimpCap = limpCap;
                        }
                    }

                    // --- zadyszka (tylko piechota, sprint) - MODEL PUNKTOWY:
                    // pula staminy w punktach (RBM: 1000 * bonus z Atletyki; my raz dokladamy
                    // Endurance bohatera), sprint pobiera stale punkty/s wg obciazenia.
                    // Ile sekund pobiegnie - wynika samo z puli i drenazu.
                    if (c.SprintFatigueEnabled)
                    {
                        if (st.RunSeconds < 0f) MeasureWind(agent, st);
                        // sprint drenuje TYLKO piechura (konia nie meczy jezdziec),
                        // ale ODDECH czytamy kazdemu: jezdziec tez wymachuje i tez
                        // moze zejsc do zera pula RBM od ciosow
                        bool onFoot = agent.MountAgent == null;
                        bool sprinting = false;
                        bool windedBefore = st.Winded;   // 147: stan przed takciem (pomiar odsapek i wyczerpania w panice)
                        float barBefore = st.Ratio;
                        if (onFoot)
                        {
                            float speed = agent.GetCurrentVelocity().Length;
                            // prog biegu WZGLEDEM mozliwosci agenta - RBM mocno tnie
                            // predkosci (stary sztywny prog 4.2 m/s nigdy nie padal,
                            // stad "stamina nie spada"). Marsz ~40% maksimum, wiec
                            // wszystko powyzej ~55% maksimum traktujemy jako bieg.
                            float runThr = 3.0f;
                            try
                            {
                                float run = agent.GetMaximumForwardUnlimitedSpeed();
                                if (run > 0.8f)
                                {
                                    runThr = run * 0.55f;
                                    if (runThr < 2.2f) runThr = 2.2f;   // marsz (~1.6 m/s) to NIE bieg
                                    if (runThr > 4.2f) runThr = 4.2f;
                                    runThr = RunThr313(agent, st, c, runThr, run);   // 313c (recenzja): gracz - prog wzgledem limitu krzywej paska
                                }
                            }
                            catch { }
                            sprinting = speed > runThr;
                            sprintNow = sprinting;
                            speedNow = speed;
                            if (sprinting && !_sprintLogged)
                            {
                                _sprintLogged = true;
                                // 292 (recenzja): RunSeconds jest juz x mnoznik staminy ludzi - znak K w napisie (przy K = 1 napis jak dotad)
                                float k292 = BattleWind.HumanStaminaMul(c);
                                Log.Info("FieldCraft: bieg wykryty (" + speed.ToString("0.0") + " m/s, prog "
                                         + runThr.ToString("0.0") + "), pelny bieg do zera " + st.RunSeconds.ToString("0") + " s (127"
                                         + (Math.Abs(k292 - 1f) > 0.001f ? " x" + k292.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " - 292" : "") + ").");
                            }
                        }
                        // 147 (klucz PanicRunsUntilSpent, karta syntezy "139b Bieg bez odsapki"): pieszy AI w PANICE (przelacznik 139) pije oddech jak w biegu
                        // NIEZALEZNIE od predkosci - ta sama regula co bieg 127 (cala pula w RunSeconds, w biegu bez oddechu: zbior biegnacych -> oddech 137 m = 0,
                        // regeneracja RBM pominieta, dlug biegu nie wraca), bez mnoznika strachu. Po wyczerpaniu pasek nie dochodzi do progu wyjscia - 50% do konca.
                        // Dotad zwolnienie ponizej progu biegu (strefa 10%, zadyszka) konczylo "bieg" i oddech wracal - odsapka w ucieczce. Jezdziec bez zmian.
                        bool panicNow = !agent.IsMainAgent && SurvivalMode.InPanic(agent);
                        // 313d: kustykajacy (cap kustykania) nie biegnie - panika nie pije mu oddechu jak bieg
                        bool drains = sprinting || (onFoot && panicNow && c.PanicRunsUntilSpent && limpCap <= 0f);
                        var stance = c.UseRbmStamina ? RbmStanceOf(agent, st, step) : null;
                        // 127: w biegu oddech nie wraca nikomu (BattleWind.TickStaminaPrefix pomija regeneracje RBM tej postawy); 147: takze w panice
                        if (stance != null)
                        {
                            if (drains && c.NoBreathWhileRunning) _runningStances.Add(stance);
                            else _runningStances.Remove(stance);
                        }
                        if (stance != null)
                        {
                            try
                            {
                                // 137: oddech dwufazowy tej postawy (klucz BreathTwoPhase na zywo) - dlug biegu oddaje on, prog wyjscia z zadyszki WindedExitBar
                                bool breathOwns = BattleWind.HasBreath(stance);
                                float stam = (float)_rbmStamina.GetValue(stance);
                                float maxStam = _rbmMaxStamina != null ? (float)_rbmMaxStamina.GetValue(stance) : 1000f;
                                if (maxStam < 1f) maxStam = 1000f;

                                // Endurance bohatera powieksza pule RBM - raz, przy pierwszym
                                // zetknieciu. Gdy BattleWind zalatwil to juz w InitializeStamina
                                // (Active), NIE dokladamy drugi raz.
                                if (!st.EnduranceApplied)
                                {
                                    st.EnduranceApplied = true;
                                    if (!BattleWind.Active && st.EndBonus > 1.001f && _rbmMaxStamina != null)
                                    {
                                        maxStam *= st.EndBonus;
                                        stam *= st.EndBonus;
                                        _rbmMaxStamina.SetValue(stance, maxStam);
                                        _rbmStamina.SetValue(stance, stam);
                                        // Endurance przyspiesza tez ODDECH: regeneracja RBM
                                        // (ktora Atletyka juz skaluje) rosnie o ten sam mnoznik
                                        if (_rbmRegen != null)
                                        {
                                            try
                                            {
                                                float rg = (float)_rbmRegen.GetValue(stance);
                                                _rbmRegen.SetValue(stance, rg * st.EndBonus);
                                            }
                                            catch { }
                                        }
                                    }
                                }

                                if (drains)
                                {
                                    float d = maxStam / st.RunSeconds * step;   // 127: cala pula w RunSeconds pelnego biegu (147: takze w panice)
                                    float stam0 = stam;
                                    stam -= d;
                                    // 137: przy oddechu dwufazowym bez wlasnego dlugu biegu - wydatek zostaje na pasku i uzgadnia go BattleWind (F/S)
                                    if (breathOwns) st.SprintDebt = 0f;
                                    else st.SprintDebt += d;
                                    if (stam < 0f) stam = 0f;
                                    _rbmStamina.SetValue(stance, stam);
                                    // 137 (recenzja): zdjete punkty to bieg, nie walka - BattleWind nie otworzy za nie okna walki (BreathFightShare)
                                    if (breathOwns) BattleWind.NoteRunSpend(stance, stam0 - stam);
                                    // 292 (recenzja): pomiar - spadek paska od konca poprzedniego taktu biegu (tick RBM i oddech 137 miedzy taktami w srodku);
                                    // takt z paskiem na zerze albo ze zmiana puli pominiety (spadek obciety / inna pula)
                                    float r292 = stam / maxStam;
                                    if (st.Run292Prev >= 0f && Math.Abs(maxStam - st.Run292Max) < 0.5f && stam > 0f && st.RunSeconds0 > 0f)
                                    {
                                        float db = st.Run292Prev - r292;
                                        _m292Sec += step; _m292Bar += db; _m292Exp += step / st.RunSeconds0;
                                        if (agent.IsMainAgent) { _m292PlSec += step; _m292PlBar += db; _m292PlExp += step / st.RunSeconds0; }
                                    }
                                    st.Run292Prev = r292;
                                    st.Run292Max = maxStam;
                                }
                                else if (st.SprintDebt > 0.01f && stam < maxStam && !breathOwns && !BattleWind.Manages(stance))
                                {
                                    // 137: milczy przy oddechu dwufazowym - inaczej dlug biegu wracalby podwojnie (35 pkt/s przy Atletyce 100 plus oddech)
                                    // ODDECH SPRINTERA: to, co zabral bieg, wraca tempem
                                    // biegacza (25/s x Atletyka x Endurance) - ale TYLKO
                                    // dlug sprintu. Koszty ciosow RBM oddaja sie dalej po
                                    // ich staremu (~1/s), zeby nie zepsuc tempa melee.
                                    // Bohaterow z BattleWind pomijamy - ich regen RBM juz
                                    // jest tempem biegacza i oddalby dlug PODWOJNIE.
                                    float back = Math.Min(st.SprintDebt, st.RegenPerSec * step);
                                    if (back > maxStam - stam) back = maxStam - stam;
                                    stam += back;
                                    st.SprintDebt -= back;
                                    _rbmStamina.SetValue(stance, stam);
                                }
                                if (!drains) st.Run292Prev = -1f;   // 292: pomiar tylko w ciaglym biegu
                                bool wasWinded = st.Winded;
                                st.Ratio = stam / maxStam;   // 144: pasek po drenazu biegu i zwrocie dlugu (oddech 137 - pasek RBM to pula - F - S)
                                st.Pace = PaceFromRatio(st, stam / maxStam, breathOwns ? BattleWind.WindedExit(c) : 0.10f);
                                // 137: liczniki zadyszek do linii "Oddech (137)" (stan zadyszki tylko tutaj - st.Winded)
                                if (breathOwns && (st.Winded || wasWinded))
                                {
                                    bool main = agent.IsMainAgent;
                                    if (!wasWinded)
                                    {
                                        st.WindedFor = 0f;
                                        BattleWind.NoteWindIn(main, !st.EverWinded);
                                        st.EverWinded = true;
                                    }
                                    else
                                    {
                                        st.WindedFor += step;
                                        BattleWind.NoteWindTime(step, main);
                                        if (!st.Winded) BattleWind.NoteWindOut(st.WindedFor);
                                    }
                                }
                            }
                            catch { st.Pace = 1f; st.Winded = false; st.Run292Prev = -1f; }
                        }
                        else
                        {
                            st.Run292Prev = -1f;   // 292: pomiar tylko na postawie RBM
                            // bez RBM: wlasna pula na tych samych zasadach
                            if (st.Pool < 0f) st.Pool = st.PoolMax;
                            if (drains) st.Pool -= st.PoolMax / st.RunSeconds * step;   // 147: takze w panice
                            else st.Pool += st.RegenPerSec * step;
                            if (st.Pool < 0f) st.Pool = 0f;
                            if (st.Pool > st.PoolMax) st.Pool = st.PoolMax;
                            float ratio = st.PoolMax > 1f ? st.Pool / st.PoolMax : 1f;
                            st.Ratio = ratio;   // 144
                            st.Pace = PaceFromRatio(st, ratio, 0.10f);
                        }
                        // 147: pomiar biegu w panice (takze przy kluczu NIE) - czas do wyczerpania, czas na 50%, odsapki; wyjatek liczony w PanicRun.Step
                        if (onFoot && (panicNow || (st.Pr != null && st.Pr.In)))
                        {
                            if (st.Pr == null) st.Pr = new PanicRun.Man();
                            PanicRun.Step(agent, st.Pr, panicNow, windedBefore, st.Winded, st.Pace, sprinting, barBefore, st.RunSeconds, step, now);
                        }
                        else if (panicNow && !onFoot)
                        {
                            if (st.Pr == null) st.Pr = new PanicRun.Man();
                            PanicRun.Mounted(st.Pr);
                        }
                    }
                    else
                    {
                        st.Pace = 1f; st.Winded = false;
                        if (st.RbmStance != null) _runningStances.Remove(st.RbmStance);
                    }

                    // 144: AI oszczedza sily - decyzja co trzeci takt na czlowieka ((Index + takt) % 3), cap czyta latka AdjustSpeedLimit; true = zdjac cap od razu
                    bool breathRelease = false;
                    if (aiLive)
                    {
                        if (st.Ai == null) st.Ai = new AiBreath.Man();
                        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                        breathRelease = AiBreath.Step(agent, st.Ai, st.Ratio, st.RunSeconds, sprintNow, speedNow, st.Winded, step, now, ((agent.Index + aiTick) % 3) == 0);
                        aiCost += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
                    }
                    if (show || WindedShow.Any) WindedShow.See(agent, st.Winded, speedNow, now);

                    // --- kara za rany (ruch liczony razem z zadyszka) ---
                    float woundSlow = 0f;
                    float thr = c.WoundedBelowPercent / 100f;
                    if (c.WoundedPenaltiesEnabled && hpr < thr && thr > 0.01f)
                        woundSlow = c.WoundedMaxSlow * (thr - hpr) / thr;

                    // ruch: zadyszka (ostatnie 10% puli -> do -50%) i rany licza sie razem; 313c: gracz pieszo - krzywa paska i chod po pustym pasku
                    float movePace = agent.IsMainAgent ? Run313(agent, st, c, step, speedNow, sprintNow) : st.Pace;
                    if (!agent.IsMainAgent && st.Walk313Forced)
                    {
                        // 313c (recenzja): agent stracil gracza (przejecie przez AI) w naszym chodzie - oddajemy bieg raz
                        try { st.Walk313 = false; Release313(agent, st); } catch { st.Walk313Forced = false; }
                    }
                    float limit = movePace;
                    limit = Math.Min(limit, 1f - woundSlow);
                    if (limit < 0.4f) limit = 0.4f;
                    bool ctrl313 = false;   // 313c (recenzja): Ctrl gracza w tym takcie (pomiar limitu i prog biegu w nastepnym)

                    // chod na zyczenie: trzymanie lewego Ctrl = idziesz, stamina odpoczywa, wygladasz
                    // jak czlowiek, nie jak sprinter. 136 (poprawka komentarza, bez zmiany zachowania):
                    // gra na PC MA przelacznik chodu - GameKey 30 ToggleWalkMode = CapsLock
                    // (CombatHotKeyCategory.cs:161; MissionMainAgentController.cs:811 wysyla Walk/Run
                    // takze w siodle); Ctrl to nasz chod "na przytrzymanie". W siodle Ctrl ogranicza
                    // tylko jezdzca (limit czlowieka), nie konia - czy kon go slucha, niesprawdzone
                    if (c.WalkKeyEnabled && agent.IsMainAgent)
                    {
                        try
                        {
                            if (TaleWorlds.InputSystem.Input.IsKeyDown(TaleWorlds.InputSystem.InputKey.LeftControl))
                            {
                                limit = Math.Min(limit, Math.Max(0.2f, c.WalkSpeedShare));
                                ctrl313 = true;
                            }
                        }
                        catch { }
                    }
                    if (agent.IsMainAgent) Sample313(agent, st, limit, speedNow, ctrl313);   // 313c (recenzja): pomiar po koncowym limicie
                    // noc 25.10 P1 (M0, Jeff: "armie nie biegly non stop tylko szly"): dawniej limit 1.0 co 0.2 s KAZDEMU kasowal limit szyku, ktory
                    // silnik liczy co 0.5 s (HumanAIComponent.AdjustSpeedLimit - linia i tempo szyku, progi RBM). Teraz zapis tylko przy karze
                    // (zadyszka, rany, klawisz chodu: limit < 0.999), a po jej zejsciu raz (-1, bez mnoznika) - silnik przywraca swoj limit w ciagu 0.5 s.
                    // Kara spotyka marsz (MarchToContact): Min(limit x pelna predkosc, gorny limit marszu), wartosc bezwzgledna. Klucz NIE = jak dawniej.
                    if (limpCap > 0f)
                    {
                        // 313d: kustykanie - limit bezwzgledny (m/s) co takt: cap kustykania, a przy karze (zadyszka, rany) nizszy z dwoch; po zejsciu capu
                        // st.SpeedApplied zdejmuje limit galezia nizej (jak po karze)
                        float lim = limpCap;
                        if (limit < 0.999f)
                        {
                            float runL = 0f;
                            try { runL = agent.GetMaximumForwardUnlimitedSpeed(); } catch { runL = 0f; }
                            if (runL > 0.5f && limit * runL < lim) lim = limit * runL;
                        }
                        agent.SetMaximumSpeedLimit(lim, false);
                        st.SpeedApplied = true;
                        Bleed313.LimpWrites++;
                    }
                    else if (!c.SpeedCapOnlyWhenWinded)
                    {
                        agent.SetMaximumSpeedLimit(limit, true);
                        st.SpeedApplied = true;
                        _spdOld++;
                    }
                    else if (limit < 0.999f)
                    {
                        // 144 (punkt 4.3): gorny limit to cap marszu albo - bez niego - cap oddechu AiBreath (kara nie podnosi limitu ponad krok)
                        bool capBreath = false;
                        float cap = agent.MountAgent == null ? MarchToContact.CapFor(agent, out capBreath) : -1f;
                        float run = 0f;
                        if (cap > 0f) { try { run = agent.GetMaximumForwardUnlimitedSpeed(); } catch { run = 0f; } }
                        if (cap > 0f && run > 0.5f)
                        {
                            agent.SetMaximumSpeedLimit(Math.Min(limit * run, cap), false);
                            if (!capBreath) _spdMarch++;
                        }
                        else agent.SetMaximumSpeedLimit(limit, true);
                        st.SpeedApplied = true;
                        _spdPenalty++;
                        if (aiLive)
                        {
                            // przyczyna wiazaca: klawisz chodu, rany albo zadyszka / strefa 10% (najnizszy skladnik limitu)
                            int cause = AiBreath.PenWind;
                            float woundLim = 1f - woundSlow;
                            if (agent.IsMainAgent && c.WalkKeyEnabled && limit <= Math.Max(0.2f, c.WalkSpeedShare) + 0.001f && limit < Math.Min(movePace, woundLim) - 0.001f) cause = AiBreath.PenKey;
                            else if (woundLim < movePace) cause = AiBreath.PenWound;
                            AiBreath.NotePenalty(cause, capBreath && cap > 0f && run > 0.5f);
                        }
                    }
                    else if (st.SpeedApplied)
                    {
                        // 144 (punkt 4.4): zdjecie kary - zapis capu marszu albo oddechu zamiast -1 (inaczej ok. 0.5 s biegu bez limitu do najblizszego AdjustSpeedLimit)
                        float rc = agent.MountAgent == null ? MarchToContact.CapFor(agent) : -1f;
                        agent.SetMaximumSpeedLimit(rc > 0f ? rc : -1f, false);
                        if (rc > 0f && aiLive) AiBreath.NotePenaltyReleaseCap();
                        st.SpeedApplied = false;
                        _spdRelease++;
                    }
                    else if (breathRelease)
                    {
                        // 144: cap oddechu zszedl (bieg na ostatnie metry, zwarcie, wyjatek) - limit zdjety od razu; gra przepisze swoj przy najblizszym AdjustSpeedLimit
                        agent.SetMaximumSpeedLimit(-1f, false);
                        AiBreath.NoteRelease();
                    }

                    // tempo CIOSOW: zadyszka i rany skladaja sie w jeden mnoznik
                    // zamachu i pchniecia. Wolniejszy zamach = mniejsza energia
                    // ciosu, wiec obrazenia spadaja razem z tempem - dokladnie
                    // tak, jak chcial Jeff (-50% walki i obrazen przy zerze).
                    // Baze lapiemy TYLKO, gdy zadna kara nie dziala, zeby nie
                    // zjadac zmian RBM (postura) ani nie utrwalic wlasnej kary.
                    float swingF = st.Pace;
                    if (c.WoundedPenaltiesEnabled && hpr < thr) swingF *= 1f - woundSlow * 0.7f;
                    var adp = agent.AgentDrivenProperties;
                    if (swingF >= 0.999f)
                    {
                        if (st.SwingApplied)
                        {
                            if (st.BaseSwing > 0f) adp.SwingSpeedMultiplier = st.BaseSwing;
                            if (st.BaseThrust > 0f) adp.ThrustOrRangedReadySpeedMultiplier = st.BaseThrust;
                            agent.UpdateCustomDrivenProperties();
                            st.SwingApplied = false;
                            st.LastSwingF = -1f;
                        }
                        else
                        {
                            st.BaseSwing = adp.SwingSpeedMultiplier;
                            st.BaseThrust = adp.ThrustOrRangedReadySpeedMultiplier;
                        }
                    }
                    else
                    {
                        if (!st.SwingApplied && st.BaseSwing <= 0f)
                        {
                            st.BaseSwing = adp.SwingSpeedMultiplier;
                            st.BaseThrust = adp.ThrustOrRangedReadySpeedMultiplier;
                        }
                        // 309 (klucz FieldCraftSwingOnChange; Willow: 224 zapisy/s czasu misji przy ok. 45 rannych): zapis tylko przy zmianie mnoznika o >= 0.02
                        // albo gdy gra / RBM nadpisaly wlasciwosci (UpdateAgentProperties przy zmianie broni) - wtedy wartosc w adp odbiega od naszej
                        bool same = false;
                        if (swingOnChange && st.SwingApplied && st.LastSwingF > 0f && Math.Abs(swingF - st.LastSwingF) < 0.02f)
                        {
                            bool okS = !(st.BaseSwing > 0f) || Math.Abs(adp.SwingSpeedMultiplier - st.BaseSwing * st.LastSwingF) < 0.0005f;
                            bool okT = !(st.BaseThrust > 0f) || Math.Abs(adp.ThrustOrRangedReadySpeedMultiplier - st.BaseThrust * st.LastSwingF) < 0.0005f;
                            if (okS && okT) same = true;
                            else SwingRewrites++;
                        }
                        if (same) SwingSkips++;
                        else
                        {
                            if (st.BaseSwing > 0f) adp.SwingSpeedMultiplier = st.BaseSwing * swingF;
                            if (st.BaseThrust > 0f) adp.ThrustOrRangedReadySpeedMultiplier = st.BaseThrust * swingF;
                            agent.UpdateCustomDrivenProperties();
                            st.LastSwingF = swingF;
                            SwingWrites++;
                        }
                        st.SwingApplied = true;
                    }

                    // --- wykrwawianie i zalamanie ---
                    // 313d (klucz BleedFromWounds): krwawi rana z rzutu przy trafieniu (st.BleedRate, ProcessHits), nie pasek HP; pelne HP zeruje krwawienie
                    // (leczenie autotestu HorsePursuit.TestGuard, RBM). Klucz NIE - stara regula od BleedBelowHp punktow zdrowia
                    if (st.BleedRate > 0f && agent.HealthLimit > 0f && agent.Health >= agent.HealthLimit - 0.5f) { st.BleedRate = 0f; st.BleedDebt = 0f; }
                    bool bleeding = c.BleedFromWounds ? st.BleedRate > 0f : agent.Health <= c.BleedBelowHp;
                    bool broken = hpr < c.AiFleeBelowPercent / 100f;
                    // 313d (klucz PainBreakByChance): ponizej progu bolu lamie sie tylko ten, kto przegral rzut - raz na swieza rane (i raz przy pierwszym zejsciu
                    // ponizej progu bez ciosu, np. krwawieniem), nie co takt. Kto sie oparl - zostaje w odwrocie 143 albo walczy ostroznie (145)
                    if (broken && c.PainBreakByChance)
                    {
                        if (!st.PainBroken && (st.PainDue || !st.PainRolled) && c.AiFleeWhenNearDeath && !agent.IsMainAgent && agent.IsAIControlled && !st.Panicked
                            && (!c.WoundedFleeBattlesOnly || InBattle()))
                        {
                            try { RollPain(agent, st, bleeding, c); }
                            catch (Exception e) { Bleed313.Stumble("Pain", e); }
                        }
                        broken = st.PainBroken;
                    }
                    st.PainDue = false;
                    if (bleeding || broken)
                    {
                        float bleedRate = c.BleedFromWounds ? st.BleedRate : c.BleedPerSecond;
                        if (bleeding && bleedRate > 0f)
                        {
                            if (st.BleedHp0 <= 0f) st.BleedHp0 = agent.Health;   // 313d: pomiar - HP na starcie krwawienia (stara regula: pierwszy takt krwawienia)
                            // ZAOKRAGLANIE SILNIKA (13.09, audyt): setter Agent.Health robi
                            // MathF.Ceiling na kazdym zapisie, wiec strata 0.1 HP na takt
                            // (0.5/s x 0.2 s) zaokraglala sie z powrotem w gore i NIGDY sie
                            // nie zapisywala - krwawienie bylo martwym kodem, nikt nigdy
                            // nie wykrwawil sie na polu. Zbieramy ulamki w dlugu i zdejmujemy
                            // dopiero PELNE punkty, ktore silnik przyjmuje.
                            st.BleedDebt += bleedRate * step;
                            if (c.BleedFromWounds)
                            {
                                // 313d: krzepniecie - tempo maleje wykladniczo, ponizej 0.05 HP/s krwawienie ustaje
                                st.BleedRate *= (float)Math.Exp(-step / Bleed313.ClotSeconds(c));
                                if (st.BleedRate < Bleed313.StopRate) { st.BleedRate = 0f; Bleed313.Stopped++; }
                            }
                            int lose = (int)st.BleedDebt;
                            if (lose > 0)
                            {
                                st.BleedDebt -= lose;
                                float nh = agent.Health - lose;
                                if (nh <= 0f) BleedDeath(agent, st, c);
                                else agent.Health = nh;
                            }
                            if (st.BleedRate <= 0f && c.BleedFromWounds) { st.BleedDebt = 0f; st.BleedHp0 = 0f; }
                            if (!agent.IsActive()) continue;   // 313d: wykrwawil sie w tym takcie - bez zalamania trupa
                        }
                        // NM: FleeHere na koncu warunku - poza bitwa na mapie (turniej, arena, bojka) st.Panicked zostaje false
                        // NB2 (16.10, klucz BrokenMenNeverRally): jedna regula z BrokenMen - RoutHold.Break (raz Panic na bitwe, morale
                        // przypiete na 0, ponowienia bez okrzyku i cel ucieczki w RoutKeeper). Odmowa Break (koniec bitwy, bez druzyny, odwrot
                        // z rozkazu szyku) zostawia st.Panicked false - sprobujemy przy nastepnym takcie. Ponowne FleeTheField (Panic co 0,2 s -
                        // zapetlony krzyk z logu 16.10: 257 wywolan na 7 ludzi) tylko na starej drodze (klucz wylaczony).
                        bool rout = RoutHold.On(Mission);
                        if (broken && c.AiFleeWhenNearDeath && !agent.IsMainAgent && agent.IsAIControlled && !st.Panicked && FleeHere(agent)
                            && (rout || !HorsePursuit.TestShielded(agent)))   // recenzja 291: stara droga (bez RoutHold) - nekajacy autotestu jak w RoutHold.Break
                        {
                            if (rout) st.Panicked = RoutHold.Break(agent, MissionTeams.Flee);
                            else
                            {
                                st.Panicked = true;
                                FleeTheField(agent);
                            }
                        }
                        else if (!rout && st.Panicked && !agent.IsMainAgent && agent.IsAIControlled && !agent.IsRetreating() && FleeHere(agent))
                        {
                            // odwrot mu wygasl (zderzyl sie z kims, silnik go zawrocil) -
                            // przypominamy trupowi ze mial uciekac, nie boksowac
                            FleeTheField(agent);
                        }
                    }
                }
                _snap.Clear();   // 309: bez referencji do ludzi miedzy taktami
                // 144: koniec taktu - szyki rozciagniete (D3), koszt zasady; pokaz zadyszki sprzata wpisy nie widziane w tym takcie
                try { if (aiLive) AiBreath.EndTick(aiCost * 1000.0 / System.Diagnostics.Stopwatch.Frequency); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteBreath, "AiBreath.EndTick", e); }
                try { WindedShow.EndTick(now); } catch (Exception e) { RoutHold.Stumble(RoutHold.SiteBreath, "WindedShow.EndTick", e); }
                // 292: linia "Stamina (292)" raz na misje, 3 s po starcie (ludzie startu juz w bitwie i zmierzeni do biegu, konie z jezdzcem policzone)
                if (!_k292Said && now - _k292From >= 3f)
                {
                    _k292Said = true;
                    try
                    {
                        var ma = Mission.MainAgent;
                        BattleWind.StartLine292(Mission, c, _runN, _runTSum, _runT292Sum, _plKg >= 0f ? _plT : 0f, _plKg >= 0f ? _plT292 : 0f, ma == null || ma.MountAgent == null, false);
                    }
                    catch (Exception e) { RoutHold.Stumble(RoutHold.SiteRun, "BattleWind.StartLine292", e); }
                }
            }
            catch (Exception e) { Log.Error("FieldCraft.Tick", e); }
        }

        // ------------------------------------------------------------ 313d: krwawienie z rany, rzut bolu, kustykanie (glowny watek)

        /// <summary>313d: rany z kolejki OnAgentHit. Kazda: licznik ran (rzut odwrotu 143, BrokenMen) i znacznik swiezej rany (rzut bolu); przy BleedFromWounds
        /// rzut krwawienia (Bleed313.Chance) i strata calkowita do tempa (strata / BleedClotSeconds). Wyjatek na rane liczony, reszta kolejki dalej.</summary>
        private void ProcessHits(Settings c)
        {
            var list = Bleed313.Take();
            if (list.Count == 0) return;
            try
            {
                float clot = Bleed313.ClotSeconds(c);
                for (int i = 0; i < list.Count; i++)
                {
                    var h = list[i];
                    var v = h.V;
                    try
                    {
                        if (v == null || !v.IsActive() || !v.IsHuman || Undead.Character(v.Character)) continue;
                        State st;
                        if (!_states.TryGetValue(v, out st)) { st = new State(); _states[v] = st; }
                        st.Hits++;
                        st.PainDue = true;
                        if (!c.BleedFromWounds) continue;
                        if (v.IsMainAgent && !c.BleedAffectsPlayer) continue;
                        Bleed313.Hits++;
                        if (st.Ath < 0f) st.Ath = ReadAth(v);
                        float cond = Bleed313.Cond(st.Ath, c);
                        float w;
                        float p = Bleed313.Chance(h, v.HealthLimit, cond, c, out w);
                        if (w <= 0f) { Bleed313.Shallow++; continue; }
                        if (p <= 0f || MBRandom.RandomFloat >= p) continue;
                        float loss = Bleed313.Loss(h.Dmg, cond, c);
                        if (loss <= 0f) continue;
                        bool was = st.BleedRate > 0f;
                        if (!was) st.BleedMissile = false;   // 313y: nowe krwawienie - od pocisku tylko, gdy ktoras z jego ran byla od pocisku
                        st.BleedMissile |= h.Missile;
                        st.BleedRate += loss / clot;
                        st.BleedBy = h.By;
                        st.BleedByA = h.ByA;
                        Bleed313.Bleeds++;
                        Bleed313.ByType[Bleed313.TypeGroup(h)]++;
                        Bleed313.ByPart[Bleed313.PartGroup(h.Part)]++;
                        Bleed313.PSum += p;
                        Bleed313.LSum += loss;
                        Bleed313.AthSum += st.Ath;
                        if (!was)
                        {
                            st.BleedHp0 = v.Health;
                            if (v.IsMainAgent)
                            {
                                Bleed313.PlayerBleeds++;
                                TaleWorlds.Library.InformationManager.DisplayMessage(new TaleWorlds.Library.InformationMessage("You are bleeding.", TaleWorlds.Library.Colors.Red));
                            }
                        }
                    }
                    catch (Exception e) { Bleed313.Stumble("Hit", e); }
                }
            }
            finally { list.Clear(); }
        }

        /// <summary>313d: rzut paniki z bolu - PainBreakChance x (krwawi ? PainBreakBleedingFactor : 1) x (1.5 - morale/100) x kondycja, obciete 5..95%.</summary>
        private static void RollPain(Agent agent, State st, bool bleeding, Settings c)
        {
            st.PainRolled = true;
            float morale = 50f;
            try { morale = agent.GetMorale(); } catch { morale = 50f; }
            if (st.Ath < 0f) st.Ath = ReadAth(agent);
            float p = Bleed313.Clamp(c.PainBreakChance, 0f, 1f) * (bleeding ? Bleed313.Clamp(c.PainBreakBleedingFactor, 1f, 3f) : 1f)
                      * Bleed313.MoraleMul(morale) * Bleed313.Cond(st.Ath, c);
            p = Bleed313.Clamp(p, 0.05f, 0.95f);
            Bleed313.PainRolls++;
            Bleed313.PainMoraleSum += morale;
            if (MBRandom.RandomFloat < p)
            {
                st.PainBroken = true;
                Bleed313.PainBroke++;
                if (bleeding) Bleed313.PainBrokeBleeding++;
            }
            else Bleed313.PainResist++;
        }

        /// <summary>313d: zgon z krwawienia - Blow od siebie jak dotad, ale przy BleedFromWounds na konto tego, kto zadal rane (gdy jeszcze stoi na polu; inaczej
        /// gra sama bierze ostatni cios z 5 s - Agent.Die, CanOverrideBlow). Pomiar BleedDeathLog: stan (zabity / nieprzytomny), kredyt, panika, odwrot 143.</summary>
        private void BleedDeath(Agent agent, State st, Settings c)
        {
            bool log = c.BleedDeathLog, panic = false, wd = false;
            if (log)
            {
                try { panic = st.Panicked || SurvivalMode.InPanic(agent); } catch { panic = false; }
                try { var k = RoutHold.Live; wd = k != null && k.Withdrawing(agent); } catch { wd = false; }
            }
            var b = new Blow(agent.Index);
            bool credit = false;
            // recenzja 313d: kredyt tylko temu samemu czlowiekowi (zapamietany Agent, nie sam indeks - indeks usunietego przejmuje posilek)
            if (c.BleedFromWounds && st.BleedBy >= 0 && st.BleedBy != agent.Index && st.BleedByA != null)
            {
                Agent by = null;
                try { by = Mission.FindAgentWithIndex(st.BleedBy); } catch { by = null; }
                if (by != null && ReferenceEquals(by, st.BleedByA) && by.IsActive()) { b.OwnerId = st.BleedBy; credit = true; }
            }
            b.DamageType = DamageTypes.Cut;
            b.InflictedDamage = (int)agent.Health + 1;
            b.GlobalPosition = agent.Position;
            float hp0 = st.BleedHp0;
            // 313y (recenzja integracji, WAZNA): Agent.Die liczy zgon od pocisku tylko przy b.IsMissile (WeaponRecord) - nasz Blow go nie ma, wiec smierc
            // z krwawienia po strzale nie podnosila TeamQuerySystem.DeathByRangedCount (prog 291e FireDead, OutshotRush.Live, bilans ognia gry). Liczymy sami
            // (jak gra: druzyna szyku przed Die), tylko przy BleedFromWounds - stara regula nie wie, skad rana
            bool byMissile = c.BleedFromWounds && st.BleedMissile;
            if (byMissile)
            {
                try
                {
                    var fm = agent.Formation;
                    var tq = fm != null && fm.Team != null ? fm.Team.QuerySystem : null;
                    if (tq != null) { tq.RegisterDeathByRanged(); if (log) Bleed313.DeathMissile++; }
                }
                catch (Exception e) { Bleed313.Stumble("DeathMissile", e); }
            }
            agent.Die(b, Agent.KillInfo.Invalid);
            if (!log) return;
            AgentState s = AgentState.Killed;
            try { s = agent.State; } catch { s = AgentState.Killed; }
            if (s == AgentState.Unconscious) Bleed313.DeathUncon++; else Bleed313.DeathKilled++;
            if (credit) Bleed313.DeathCredit++;
            if (panic) Bleed313.DeathPanic++;
            if (wd) Bleed313.DeathWithdraw++;
            if (!c.BleedFromWounds) Bleed313.DeathOld++;
            Bleed313.DeathHp0Sum += hp0;
        }

        /// <summary>313d: cap kustykania (m/s) albo 0 - pieszy AI (nie gracz, nie nekajacy autotestu) w bitwie na mapie: w odwrocie rannego 143 albo z HP &lt;=
        /// WoundedLimpBelowHp (takze w panice z bolu). Liczniki ludzi do linii.</summary>
        private float LimpNow(Agent agent, State st, Settings c)
        {
            if (agent.IsMainAgent || !agent.IsAIControlled || agent.MountAgent != null) return 0f;
            if (!InBattle()) return 0f;
            // 313y (recenzja integracji, histereza): RBM leczy +0.9 HP co 10 s przy pasku > 85% - kustykajacy odpoczywa, wiec po ok. 10 s mial 11 HP, tracil cap
            // i znow biegl (miganie). Raz pod progiem: kustyka, az HP przekroczy prog + LimpHystHp
            int thrHp = c.WoundedLimpBelowHp;
            float hpNow = agent.Health;
            bool low = thrHp > 0 && (hpNow <= thrHp || (st.LimpLow && hpNow <= thrHp + Bleed313.LimpHystHp));
            st.LimpLow = low;
            bool wd = false, hero = false;
            var k = RoutHold.Live;
            // recenzja 313d: w odwrocie 143 kustyka do konca odwrotu, ale nie ten, kto wrocil do walki (na postoju znow w przedniej polowie szyku)
            if (k != null && ReferenceEquals(k.Mission, Mission)) wd = k.LimpingWithdraw(agent);
            // 313y (recenzja integracji, WAZNA): ranny lord / bohater AI (HeroCaution tryb ranny, ponizej 45% HP) wycofuje sie za szyk - tez krokiem rannego,
            // nie sprintem (Jeff 01.11: "ranny nie biega jak glupi tylko wolno wycofuje sie"); ta sama sciezka capu co odwrot 143 (PostSpeed i FieldCraft)
            if (!wd) hero = HeroCaution.WoundedWithdrawing(agent);
            if (!low && !wd && !hero) return 0f;
            if (HorsePursuit.TestRunAnyThread(agent)) return 0f;   // nekajacy autotestu - test bez zmian
            Bleed313.LimpMen.Add(agent);
            if (wd) Bleed313.LimpWithdraw.Add(agent);
            else if (hero) Bleed313.LimpHero.Add(agent);
            else if (SurvivalMode.InPanic(agent)) Bleed313.LimpPanic.Add(agent);
            return Bleed313.LimpSpeed(agent, c);
        }

        // NM (klucz WoundedFleeBattlesOnly): zlamany ucieka tylko w bitwie na mapie (MissionTeams.IsBattle, raz na misje) - w turnieju, na arenie
        // i w bojce walczy do nokautu, jak chce gra (TournamentFightMissionController.CanAgentRout = false). Wylaczenie klucza = jak dotad.
        private int _fleeHere = -1;               // -1 nie sprawdzone, 0 misja poza bitwa na mapie, 1 bitwa

        private bool FleeHere(Agent agent)
        {
            var c = Settings.Current;
            if (c == null || !c.WoundedFleeBattlesOnly) return true;
            if (_fleeHere < 0) _fleeHere = MissionTeams.IsBattle(Mission) ? 1 : 0;
            if (_fleeHere == 1) return true;
            MissionTeams.Held(MissionTeams.Flee, agent);
            return false;
        }

        /// <summary>
        /// Zlamany UCIEKA Z POLA, jak chcial Jeff: od najblizszego wroga,
        /// w strone najblizszego skraju mapy po tamtej polkuli. Panika natywna
        /// wylacza mu walke, ale cel odwrotu liczymy SAMI - stary GetRetreatPos
        /// na scenach bez punktow odwrotu zwracal miejsce, w ktorym stal,
        /// wiec zlamani rzucali bron i stali jak kolki, boksujac z bliska.
        /// </summary>
        private static void FleeTheField(Agent agent)
        {
            try
            {
                var mission = TaleWorlds.MountAndBlade.Mission.Current;
                if (mission == null || agent == null || !agent.IsActive()) return;
                // NM: czlowiek bez druzyny (Team.Invalid) nie ucieka - przed Panic, zeby nie zostal w panice bez odwrotu
                if (!MissionTeams.Valid(agent.Team)) { MissionTeams.Skip(agent, MissionTeams.Flee); return; }
                try { var cai = agent.CommonAIComponent; if (cai != null) cai.Panic(); } catch { }

                Vec2 my = agent.Position.AsVec2;
                Agent foe = null; float best = float.MaxValue;
                foreach (var a in mission.Agents)
                {
                    if (a == null || !a.IsActive() || !a.IsHuman) continue;
                    // NM: wrog tylko z wazna druzyna (natywne IsEnemy na Team.Invalid sie wywraca)
                    if (MissionTeams.Skip(a, MissionTeams.Flee) || !MissionTeams.Enemies(a.Team, agent.Team)) continue;
                    float d = a.Position.AsVec2.DistanceSquared(my);
                    if (d < best) { best = d; foe = a; }
                }

                Vec2 dir;
                if (foe != null) dir = my - foe.Position.AsVec2;                  // plecami do wroga
                else dir = mission.GetClosestBoundaryPosition(my) - my;           // bez wroga: wprost do skraju
                if (dir.LengthSquared < 0.01f) dir = new Vec2(1f, 0f);
                dir.Normalize();

                Vec2 far = my + dir * 300f;
                Vec2 edge;
                try { edge = mission.GetClosestBoundaryPosition(far); }           // przyciecie do granicy mapy
                catch { edge = far; }
                var wp = new WorldPosition(mission.Scene, new Vec3(edge.x, edge.y, agent.Position.z));
                MissionTeams.Pushed(MissionTeams.Flee, agent);
                agent.Retreat(wp);
            }
            catch (Exception e) { MissionTeams.Stumble(MissionTeams.Flee, "FleeTheField", e); }
        }
    }
}
