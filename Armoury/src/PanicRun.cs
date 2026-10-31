using System;
using System.Globalization;
using System.Text;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 147 (25.10, fala "AI walczy o zycie", karta syntezy "139b Bieg bez odsapki", D6; Jeff 25.10 ok. 13:30: "dopiero ta zasada peka gdy jest panika, w panice
    /// jest zasada uciekaj i brak rozsadku ... Wtedy ginie najwiecej osob"; ok. 13:20: "przeciwnik nie biegal caly czas bo beda padnieci").
    /// REGULA (FieldCraft.OnMissionTick, klucz PanicRunsUntilSpent): pieszy czlowiek AI w trybie PANIKA (przelacznik 139 - SurvivalMode.InPanic) pije oddech jak
    /// w biegu 127 NIEZALEZNIE od predkosci (cala pula w RunSeconds - ta sama fizjologia co kazdy biegacz, bez mnoznika strachu) i w panice go nie odzyskuje:
    /// postawa RBM idzie do zbioru biegnacych (przy NoBreathWhileRunning) - oddech 137 liczy mu m = 0, regeneracja RBM pominieta, dlug biegu nie wraca; bez RBM
    /// wlasna pula bez regeneracji. Po wyczerpaniu pasek nie wraca do progu wyjscia z zadyszki - 50% predkosci do granicy pola albo do smierci. Bez animacji upadku.
    /// Jezdziec bez zmian (bieg pije tylko piechur, kon - HorseWind 136).
    /// DOTAD: spanikowany, ktory zwolnil ponizej progu biegu (strefa 10% albo zadyszka 50%), nie byl juz "w biegu": oddech 137 wracal mu jak w chodzie (m = WalkM),
    /// po powrocie paska do WindedExitBar (30%) wychodzil z zadyszki i znow biegl pelna para - odsapka w ucieczce.
    /// Ta klasa: liczniki i linia "Bieg zlamanych (147)" raz na misje (FieldCraft.OnEndMission). Tylko glowny watek (FieldCraft co 0.2 s). Wyjatek liczony per
    /// czlowiek (po MaxFails wypada z probek; regula dziala dalej), potkniecia NB2 - miejsce "bieg". Przy kluczu NIE ten sam pomiar bez zmiany drenazu.
    /// </summary>
    internal static class PanicRun
    {
        internal const int MaxFails = 5;

        /// <summary>Stan czlowieka (w FieldCraft.State - zyje tyle co wpis FieldCraft).</summary>
        internal sealed class Man
        {
            public bool Seen, MountSeen, In, Spent, Out;
            public int Fails;
            public float At = -1f;      // czas misji wejscia w biezaca panike (pieszo)
            public int SlowN;           // 307d: takty ponizej progu biegu (probka co ProbeEvery takt)
            public float SlowAcc;       // 307d: czas taktow ponizej progu od ostatniej probki (waga nastepnej probki)
        }

        // ---- liczniki misji (zerowane w FieldCraft.ResetRunStats: AfterStart i po linii)
        private static readonly int[] _men = new int[2];   // 0 strona gracza (i sojusznicy), 1 wrog
        private static int _mounted, _compMen, _spells, _atEntry, _spentN, _exitEarly, _recover, _fails, _out, _endIn, _endHalf, _barN;
        private static float _spentSum, _spentMin = float.MaxValue, _spentMax, _secs, _halfSecs, _zoneSecs, _slowSecs, _compSecs, _barSum, _runTSum;

        // 307b (krytyk 307 "zlamani nie uciekaja", 142a: 82-98% czasu paniki ponizej progu biegu): rozbicie czasu ponizej progu na przyczyny (nie rozlaczne - czlowiek
        // moze miec kilka naraz), probka natywna tylko w takcie ponizej progu (co 0.2 s na zlamanego), najwyzej MaxProbes na misje (dalej - "bez probki").
        // Takze HP przy wejsciu w panike (bol FieldCraft < AiFleeBelowPercent = lamie sie prawie martwy) i kurs ucieczki (RoutKeeper.Decide, klucz PanicFleeHoldCourse).
        // 307d (przeglad scalenia, CPU): probka co ProbeEvery. takt na czlowieka z waga = czas taktow od poprzedniej probki; sufit 20000 probek na misje.
        // Czas taktow bez probki (ogon po ostatniej probce czlowieka, sufit) idzie do "bez probki"; predkosci srednie dzielone przez czas z probka.
        internal const int MaxProbes = 20000;
        internal const int ProbeEvery = 5;
        private static float _sW, _vSecs;   // 307d: czas przypisany probkom (waga), czas z pomiarem predkosci
        private static int _probes, _entryPain, _entryWound, _hpN, _courseHeld, _courseTurned, _courseBlocked, _courseFree;
        private static float _sFoe, _sLim, _sNotRun, _sGoal, _sStuck, _sWound, _sCrowd, _sOther, _sNoProbe, _vSum, _mxSum, _limSum, _limSecs, _hpSum;
        private static readonly MBList<Agent> _near = new MBList<Agent>();

        /// <summary>307b: RoutKeeper.Decide - kurs utrzymany mimo zmiany najlepszego kierunku (0), zmieniony (1), zmieniony bo droga zablokowana (2);
        /// 307e: 1 i 2 rozlaczne (linia: "zmieniony bez blokady N, zmieniony przy zablokowanej drodze M").</summary>
        internal static void Course(int what)
        {
            if (what == 0) _courseHeld++;
            else if (what == 1) _courseTurned++;
            else if (what == 3) _courseFree++;   // 310: droga wolna sprawdzona przed Choose (klucz RoutKeeperCourseFirst) - kurs trzymany bez oceny kierunku
            else _courseBlocked++;
        }

        internal static void Reset()
        {
            _probes = _entryPain = _entryWound = _hpN = _courseHeld = _courseTurned = _courseBlocked = _courseFree = 0;
            _sFoe = _sLim = _sNotRun = _sGoal = _sStuck = _sWound = _sCrowd = _sOther = _sNoProbe = _vSum = _mxSum = _limSum = _limSecs = _hpSum = 0f;
            _sW = _vSecs = 0f;
            _men[0] = 0; _men[1] = 0;
            _mounted = _compMen = _spells = _atEntry = _spentN = _exitEarly = _recover = _fails = _out = _endIn = _endHalf = _barN = 0;
            _spentSum = 0f; _spentMin = float.MaxValue; _spentMax = 0f;
            _secs = _halfSecs = _zoneSecs = _slowSecs = _compSecs = _barSum = _runTSum = 0f;
        }

        /// <summary>Sam odwrot komponentu bez paniki i bez RoutHold (ofiara szarzy konnej RBM, ok. 10 s - przelacznik 139 mowi PANIKA). Tylko do licznika.</summary>
        private static bool ComponentOnly(Agent a)
        {
            var cai = a.CommonAIComponent;
            if (cai == null || cai.IsPanicked || !cai.IsRetreating) return false;
            var k = RoutHold.Live;
            return k == null || !k.Knows(a);
        }

        /// <summary>Takt pieszego (co 0.2 s): panic = przelacznik 139 mowi PANIKA w tym takcie; windedBefore / winded - zadyszka FieldCraft przed i po takcie;
        /// pace - tempo z oddechu po takcie; sprinting - bieg ponad prog predkosci (stara regula drenazu); barBefore - pasek przed takciem.</summary>
        internal static void Step(Agent a, Man man, bool panic, bool windedBefore, bool winded, float pace, bool sprinting, float barBefore, float runSeconds,
                                  float step, float now)
        {
            if (man == null || man.Out) return;
            try
            {
                if (panic)
                {
                    if (!man.Seen)
                    {
                        man.Seen = true;
                        int side = a.Team != null && (a.Team.IsPlayerTeam || a.Team.IsPlayerAlly) ? 0 : 1;
                        _men[side]++;
                        if (ComponentOnly(a)) _compMen++;
                    }
                    if (!man.In)
                    {
                        // wejscie w panike: drenaz w tym takcie juz byl - poczatek od poprzedniego taktu
                        man.In = true;
                        man.At = now - step;
                        man.Spent = false;
                        _spells++;
                        _barN++;
                        _barSum += barBefore;
                        _runTSum += runSeconds;
                        if (windedBefore) { man.Spent = true; _atEntry++; }
                        Entry(a);
                    }
                    if (!man.Spent && winded)
                    {
                        man.Spent = true;
                        float t = now - man.At;
                        if (t < 0f) t = 0f;
                        _spentN++;
                        _spentSum += t;
                        if (t < _spentMin) _spentMin = t;
                        if (t > _spentMax) _spentMax = t;
                    }
                    _secs += step;
                    if (winded) _halfSecs += step;
                    else if (pace < 0.999f) _zoneSecs += step;
                    if (!sprinting) { _slowSecs += step; Slow(a, man, step); }
                    if (windedBefore && !winded) _recover++;   // odsapka w panice: wyjscie z zadyszki (przy kluczu TAK oczekiwane 0)
                    if (ComponentOnly(a)) _compSecs += step;
                }
                else if (man.In)
                {
                    man.In = false;
                    if (!man.Spent) _exitEarly++;
                }
            }
            catch (Exception e)
            {
                man.Fails++;
                _fails++;
                if (man.Fails >= MaxFails && !man.Out) { man.Out = true; _out++; }
                RoutHold.Stumble(RoutHold.SiteRun, "PanicRun.Step", e);
            }
        }

        /// <summary>307b: HP przy wejsciu w panike - ponizej progu bolu FieldCraft (AiFleeBelowPercent) i ponizej progu kary ran (WoundedBelowPercent).</summary>
        private static void Entry(Agent a)
        {
            var c = Settings.Current;
            float max = a.HealthLimit;
            if (c == null || !(max > 0f)) return;
            float r = a.Health / max;
            _hpN++;
            _hpSum += r;
            if (r < c.AiFleeBelowPercent / 100f) _entryPain++;
            else if (r < c.WoundedBelowPercent / 100f) _entryWound++;
        }

        /// <summary>307b: takt ponizej progu biegu w panice - przyczyny (wrog w 3 m, limit predkosci ponizej 1, bez natywnego odwrotu, cel odwrotu w 5 m,
        /// stoi ponizej 0.5 m/s, kara ran, tlok swoich w 1.5 m) i predkosc rzeczywista wobec mozliwej. Wyjatek idzie do Step (liczony per czlowiek).</summary>
        private static void Slow(Agent a, Man man, float step)
        {
            // 307d: probka w 1., 6., 11. ... takcie czlowieka ponizej progu; waga = czas taktow od poprzedniej probki (wlacznie z tym)
            man.SlowAcc += step;
            if (man.SlowN++ % ProbeEvery != 0) return;
            step = man.SlowAcc; man.SlowAcc = 0f;
            if (_probes >= MaxProbes) { _sNoProbe += step; return; }
            _probes++;
            var m = a.Mission;
            if (m == null || !MissionTeams.Valid(a.Team)) { _sNoProbe += step; return; }
            _sW += step;
            Vec2 my = a.Position.AsVec2;
            bool any = false;
            bool foe = false;
            try
            {
                m.GetNearbyEnemyAgents(my, 3f, a.Team, _near);
                for (int i = 0; i < _near.Count; i++)
                {
                    var x = _near[i];
                    if (x != null && x.IsActive() && x.IsHuman && !x.IsRunningAway) { foe = true; break; }
                }
            }
            finally { _near.Clear(); }
            if (foe) { _sFoe += step; any = true; }
            float lim = a.GetMaximumSpeedLimit();
            if (lim >= 0f && !float.IsInfinity(lim)) { _limSum += lim * step; _limSecs += step; }
            if (lim >= 0f && lim < 1f) { _sLim += step; any = true; }
            if (!a.IsRunningAway) { _sNotRun += step; any = true; }
            var rp = a.GetRetreatPos();
            Vec2 rv = rp.AsVec2;
            if (a.IsRunningAway && rv.IsValid && rv.DistanceSquared(my) < 25f) { _sGoal += step; any = true; }   // recenzja 307b: cel tylko przy natywnym odwrocie (inaczej stary/zerowy)
            float v = a.GetCurrentVelocity().Length;
            float mx = a.GetMaximumForwardUnlimitedSpeed();
            if (v >= 0f && !float.IsInfinity(v) && mx >= 0f && !float.IsInfinity(mx)) { _vSum += v * step; _mxSum += mx * step; _vSecs += step; }
            if (v < 0.5f) { _sStuck += step; any = true; }
            var c = Settings.Current;
            float hm = a.HealthLimit;
            if (c != null && c.WoundedPenaltiesEnabled && hm > 0f && a.Health / hm < c.WoundedBelowPercent / 100f) { _sWound += step; any = true; }
            if (m.GetNearbyAllyAgentsCount(my, 1.5f, a.Team) >= 3) { _sCrowd += step; any = true; }
            if (!any) _sOther += step;
        }

        /// <summary>Spanikowany w siodle - poza regula (bieg pije tylko piechur), liczony raz na czlowieka.</summary>
        internal static void Mounted(Man man)
        {
            if (man == null || man.MountSeen) return;
            man.MountSeen = true;
            _mounted++;
        }

        /// <summary>Koniec misji: kto jest jeszcze w panice na polu i czy na 50%.</summary>
        internal static void AtEnd(Agent a, Man man, bool winded)
        {
            if (man == null || !man.In || man.Out || a == null || !a.IsActive()) return;
            _endIn++;
            if (winded) _endHalf++;
        }

        private static string S(float v) { return v.ToString("0", CultureInfo.InvariantCulture); }
        private static string Pc(float a, float n) { return n > 0.01f ? (a / n * 100f).ToString("0", CultureInfo.InvariantCulture) + "%" : "-"; }

        /// <summary>Linia "Bieg zlamanych (147)" raz na misje: w bitwie na mapie (takze kryjowka) albo gdy ktos spanikowal.</summary>
        internal static void Line(Mission m, Settings c)
        {
            if (m == null || c == null) return;
            int men = _men[0] + _men[1];
            bool battle = MissionTeams.IsBattle(m);
            if (!battle && men + _mounted == 0) return;
            var sb = new StringBuilder(900);
            sb.Append("Bieg zlamanych (147): misja ").Append(MissionTeams.Kind(m));
            if (!c.SprintFatigueEnabled)
            {
                sb.Append(" - nieczynne (Sprint Fatigue NIE - bez zadyszki i drenazu biegu).");
                Log.Info(sb.ToString());
                return;
            }
            if (c.PanicRunsUntilSpent)
                sb.Append(", klucz TAK (pieszy w PANICE - przelacznik 139 - pije oddech jak w biegu 127 niezaleznie od predkosci, bez mnoznika strachu; po wyczerpaniu 50% do konca)");
            else
                sb.Append(", klucz NIE (tylko pomiar: drenaz jak dotad tylko w biegu ponad prog predkosci, oddech wraca po zwolnieniu)");
            sb.Append("; bez oddechu w biegu ").Append(c.NoBreathWhileRunning ? "TAK (oddech 137 m = 0 / regeneracja RBM pominieta)" : "NIE (w panice oddech wraca jak w biegu - 127 NIE)")
              .Append(", oddech dwufazowy 137 ").Append(c.BreathTwoPhase ? "TAK" : "NIE").Append(", stamina RBM ").Append(c.UseRbmStamina ? "TAK" : "NIE (wlasna pula)");
            sb.Append(" | zlamani pieszo ").Append(men).Append(" (strona gracza ").Append(_men[0]).Append(" / wroga ").Append(_men[1])
              .Append("; wejsc w panike ").Append(_spells).Append("; w siodle pominieci ").Append(_mounted)
              .Append("; sam odwrot komponentu - szarza RBM ").Append(_compMen).Append(" ludzi, ").Append(S(_compSecs)).Append(" s)");
            sb.Append(" | do wyczerpania: doszlo ").Append(_spentN).Append(" z ").Append(_spells - _atEntry).Append(" wejsc ze swiezym oddechem");
            if (_spentN > 0)
                sb.Append(", sr. czas do wyczerpania ").Append((_spentSum / _spentN).ToString("0.0", CultureInfo.InvariantCulture)).Append(" s (najkrocej ")
                  .Append(S(_spentMin)).Append(", najdluzej ").Append(S(_spentMax)).Append(')');
            else sb.Append(", sr. czas do wyczerpania -");
            if (_barN > 0)
            {
                // 292 (recenzja): RunSeconds jest juz x mnoznik staminy ludzi - znak K w napisie (przy K = 1 napis jak dotad)
                float k292 = BattleWind.HumanStaminaMul(c);
                sb.Append(", pasek przy wejsciu sr. ").Append(S(_barSum / _barN * 100f)).Append("%, pelny bieg 127")
                  .Append(Math.Abs(k292 - 1f) > 0.001f ? " x" + k292.ToString("0.00", CultureInfo.InvariantCulture) + " (292)" : "")
                  .Append(" sr. ").Append(S(_runTSum / _barN)).Append(" s");
            }
            sb.Append("; bez tchu juz przy wejsciu ").Append(_atEntry).Append("; koniec paniki przed wyczerpaniem ").Append(_exitEarly);
            sb.Append(" | czas w panice pieszo ").Append(S(_secs)).Append(" s: na 50% (zadyszka) ").Append(S(_halfSecs)).Append(" s - udzial biegu na 50% ")
              .Append(_secs > 0.01f ? S(_halfSecs / _secs * 100f) + "%" : "-").Append(", strefa 10% ").Append(S(_zoneSecs)).Append(" s, ponizej progu biegu ")
              .Append(S(_slowSecs)).Append(" s (").Append(c.PanicRunsUntilSpent ? "drenaz tylko przez 147" : "bez drenazu - przy kluczu TAK drenaz").Append(')');
            // 307b: przyczyny czasu ponizej progu biegu (nie rozlaczne), HP przy wejsciu, kurs ucieczki
            float sl = _slowSecs;
            var inv = CultureInfo.InvariantCulture;
            float noProbe = _sNoProbe + Math.Max(0f, sl - _sW - _sNoProbe);   // 307d: ogon taktow po ostatniej probce czlowieka tez "bez probki" 
            sb.Append(" | ponizej progu biegu - przyczyny (307b, nie rozlaczne): wrog w 3 m ").Append(S(_sFoe)).Append(" s (").Append(Pc(_sFoe, sl))
              .Append("), limit predkosci < 1 ").Append(S(_sLim)).Append(" s (").Append(Pc(_sLim, sl)).Append("), bez natywnego odwrotu ").Append(S(_sNotRun))
              .Append(" s (").Append(Pc(_sNotRun, sl)).Append("), cel odwrotu w 5 m ").Append(S(_sGoal)).Append(" s (").Append(Pc(_sGoal, sl))
              .Append("), stoi < 0.5 m/s ").Append(S(_sStuck)).Append(" s (").Append(Pc(_sStuck, sl)).Append("), kara ran ").Append(S(_sWound))
              .Append(" s (").Append(Pc(_sWound, sl)).Append("), tlok swoich w 1.5 m ").Append(S(_sCrowd)).Append(" s (").Append(Pc(_sCrowd, sl))
              .Append("), zadna z nich ").Append(S(_sOther)).Append(" s (").Append(Pc(_sOther, sl)).Append("), bez probki ").Append(S(noProbe)).Append(" s")
              .Append("; predkosc sr. ").Append(_vSecs > 0.01f ? (_vSum / _vSecs).ToString("0.00", inv) : "-")
              .Append(" m/s przy mozliwej ").Append(_vSecs > 0.01f ? (_mxSum / _vSecs).ToString("0.00", inv) : "-")
              .Append(" m/s, limit natywny sr. ").Append(_limSecs > 0.01f ? (_limSum / _limSecs).ToString("0.00", inv) : "-");
            sb.Append(" | HP przy wejsciu w panike sr. ").Append(_hpN > 0 ? S(_hpSum / _hpN * 100f) + "%" : "-").Append(" (ponizej progu bolu ").Append(_entryPain)
              .Append(", ponizej progu kary ran ").Append(_entryWound).Append(", zdrowszych ").Append(_hpN - _entryPain - _entryWound).Append(')');
            sb.Append(" | kurs ucieczki (307b, klucz ").Append(c.PanicFleeHoldCourse ? "TAK" : "NIE").Append("): utrzymany ").Append(_courseHeld)
              // 307e: liczniki rozlaczne (Course: 1 albo 2) - napis "w tym" mylil (zmieniony 0, w tym zablokowana 1)
              .Append(", zmieniony bez blokady ").Append(_courseTurned).Append(", zmieniony przy zablokowanej drodze ").Append(_courseBlocked)
              .Append(", droga wolna bez oceny kierunku (310) ").Append(_courseFree);
            sb.Append(" | odsapki w panice (wyjscia z zadyszki) ").Append(_recover).Append(c.PanicRunsUntilSpent ? " (oczekiwane 0)" : " (stara regula)");
            sb.Append(" | na koniec misji w panice na polu ").Append(_endIn).Append(", z nich na 50% ").Append(_endHalf);
            sb.Append(" | potkniecia bieg ").Append(_fails).Append(" (wypadli z probek ").Append(_out).Append(").");
            Log.Info(sb.ToString());
        }
    }
}
