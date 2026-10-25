using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 142 (25.10, fala "AI walczy o zycie", karta "139a Panika bez powrotu" z syntezy ai-przetrwanie; Jeff 13:30: "dopiero ta zasada peka gdy jest panika,
    /// w panice jest zasada uciekaj i brak rozsadku ... Wtedy ginie najwiecej osob"; decyzja Jeffa ok. 15:00: panika z morale "Bez powrotu, zbiorka pozniej").
    /// Klucz RoutedMenNeverRally (dziala przy BrokenMenNeverRally - to maszyna RoutHold): w bitwie polowej kazda panika gry (morale gry ponizej 0.01, RBM
    /// CoordinatedRetreat - morale 0 calej strony, RBMAI Tactics.cs:385-442) u czlowieka AI spoza zbioru zlamanych idzie do RoutHold. Sama mechanika w
    /// RoutKeeper (RoutHold.cs): OnAgentPanicked -> kolejka -> nastepny OnMissionTick -> Break(a, MissionTeams.GamePanic) sciezka "juz w panice".
    /// Dowod, ze bez tego panika gry wraca do walki: RBMAI AgentAi.cs:651-657 - postfiks HumanAIComponent.OnTick w kazdej klatce wola
    /// CommonAIComponent.StopRetreating kazdemu z morale &gt; 0 i 10 s bez ciosu wrecz; StopRetreating zeruje IsRetreating i IsPanicked (CommonAI.cs:
    /// StopRetreating), a AgentMoraleInteractionLogic.ApplyMoraleEffectOnAgentIncapacitated daje premie morale (smierc wroga w 4 m) takze spanikowanym -
    /// czlowiek bez broni (ArtemsLivelyAnimations DropAllWeapons przy OnAgentPanicked) wracal do walki na piesci ("bija piescia", 16.10). RoutHold trzyma
    /// morale 0 przy trwajacej panice (RBM: warunek "morale &gt; 0" nie zachodzi), a gdy panike i tak zgasi ktos inny - ponawia odwrot bez okrzyku.
    /// Ta klasa: tylko liczniki misji i linia "PANIKA BEZ POWROTU (142)" zaraz po "LOS ZLAMANYCH (139)" (RoutHold.Write, pod Gate). Liczniki zeruje
    /// RoutHold.Ensure (zmiana misji). Pomiar paniki gry w polu dziala takze przy kluczu NIE (porownanie przed / po: czas bitwy, powroty RBM z linii 139).
    /// Potkniecia: miejsce "powrot" linii NB2 (RoutHold.SiteNoReturn) - funkcji nie gasimy.
    /// Nazwa klasy nie moze byc "Log", "Settings" ani "McmSettings" (CrashScribe Net.cs:155).
    /// </summary>
    internal static class NoReturn
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static bool _begun, _here, _key, _rally;
        // ---- pomiar paniki gry w polu (zawsze)
        private static readonly HashSet<int> CandPeople = new HashSet<int>();
        private static int _candPanics;
        private static float _firstPanic = -1f, _deployed = -1f, _ended = -1f, _missionEnd = -1f;
        // ---- przejecie z kolejki
        private static int _took, _tookHero, _tookMine, _tookFoe;
        private static float _waitSum, _waitMax;
        private static int _queueMax, _limitFrames, _droppedAtEnd;
        internal static int Gone, OtherWay, Quenched, Refused, WaitPins;
        // ---- bez powrotu (przejeci z paniki gry)
        internal static readonly HashSet<int> PinGame = new HashSet<int>();
        internal static int ExtinctGame, ResendGame, RePanicGame;
        // ---- wpisy RoutKeeper i koszt ticku
        private static int _maxLive, _costFrames;
        private static long _costTicks, _costMax;

        /// <summary>Zmiana misji (RoutHold.Ensure, wolajacy trzyma Gate): wszystko od zera.</summary>
        internal static void ResetCounters()
        {
            _begun = _here = _key = _rally = false;
            CandPeople.Clear();
            _candPanics = 0;
            _firstPanic = _deployed = _ended = _missionEnd = -1f;
            _took = _tookHero = _tookMine = _tookFoe = 0;
            _waitSum = _waitMax = 0f;
            _queueMax = _limitFrames = _droppedAtEnd = 0;
            Gone = OtherWay = Quenched = Refused = WaitPins = 0;
            PinGame.Clear();
            ExtinctGame = ResendGame = RePanicGame = 0;
            _maxLive = _costFrames = 0;
            _costTicks = _costMax = 0;
        }

        /// <summary>RoutKeeper.AfterStart: bitwa polowa (liczona raz w RoutKeeper) i stan kluczy na te misje.</summary>
        internal static void Begin(Mission m, bool here)
        {
            var c = Settings.Current;
            lock (RoutHold.Gate)
            {
                RoutHold.EnsureLocked(m);
                _begun = true;
                _here = here;
                _key = c != null && c.RoutedMenNeverRally;
                _rally = c != null && c.BrokenMenNeverRally;
            }
        }

        /// <summary>RoutKeeper.OnDeploymentFinished: poczatek bitwy do "czasu bitwy".</summary>
        internal static void Deployed(float t)
        {
            if (_deployed < 0f) _deployed = t;
        }

        /// <summary>RoutKeeper.Enqueue (callback OnAgentPanicked): panika gry u czlowieka AI spoza zbioru w bitwie polowej - pomiar, takze przy kluczu NIE.</summary>
        internal static void Candidate(Agent a, float now)
        {
            lock (RoutHold.Gate)
            {
                _candPanics++;
                CandPeople.Add(a.Index);
                if (_firstPanic < 0f) _firstPanic = now;
            }
        }

        internal static void QueueLen(int n) { if (n > _queueMax) _queueMax = n; }
        internal static void LimitFrame() { _limitFrames++; }

        /// <summary>Przejecie udane (Break sciezka "juz w panice"): czas w kolejce, bohater, strona gracza (Team.IsPlayerTeam / IsPlayerAlly) albo wroga.</summary>
        internal static void Took(float wait, bool hero, bool mine)
        {
            _took++;
            if (hero) _tookHero++;
            if (mine) _tookMine++; else _tookFoe++;
            if (wait < 0f) wait = 0f;
            _waitSum += wait;
            if (wait > _waitMax) _waitMax = wait;
        }

        /// <summary>RoutKeeper.OnMissionTick: pierwsza klatka z MissionEnded - koniec bitwy; czekajacy w kolejce przepadaja.</summary>
        internal static void BattleEnded(float t, int dropped)
        {
            if (_ended < 0f) _ended = t;
            _droppedAtEnd += dropped;
        }

        /// <summary>RoutKeeper.OnEndMission przed linia: koniec misji i czekajacy w kolejce.</summary>
        internal static void Close(float t, int dropped)
        {
            _missionEnd = t;
            _droppedAtEnd += dropped;
        }

        /// <summary>Koszt ticku RoutKeeper w klatce z praca (zbior zlamanych albo kolejka niepuste) i zywe wpisy po ticku.</summary>
        internal static void Cost(long ticks, int live)
        {
            _costFrames++;
            _costTicks += ticks;
            if (ticks > _costMax) _costMax = ticks;
            if (live > _maxLive) _maxLive = live;
        }

        private static string Ms(double ticks)
        {
            return (ticks * 1000.0 / Stopwatch.Frequency).ToString("0.000", Inv);
        }

        private static string S(float v)
        {
            return v.ToString("0.0", Inv);
        }

        /// <summary>Linia "PANIKA BEZ POWROTU (142)" po linii 139 (RoutHold.Write, wolajacy trzyma Gate). Tylko bitwa polowa; przy kluczu NIE tylko,
        /// gdy byla panika gry w polu (pomiar przed / po).</summary>
        internal static void Write(Settings c)
        {
            if (!_begun || !_here) return;
            if (!_key && CandPeople.Count == 0) return;
            bool on = _key && _rally;
            var sb = new StringBuilder(1300);
            sb.Append("PANIKA BEZ POWROTU (142): misja bitwa, klucz ").Append(_key ? "TAK" : "NIE");
            if (on) sb.Append(" (Broken Men Never Rally TAK; kazda panika gry w polu do RoutHold sciezka 'juz w panice', najwyzej ").Append(RoutHold.TakePerFrame)
                      .Append(" na klatke, limit wpisow ").Append(RoutHold.MaxEntries).Append(')');
            else if (_key) sb.Append(" (nieczynne: Broken Men Never Rally NIE - tylko pomiar)");
            else sb.Append(" (tylko pomiar - panika gry wraca przez RBM jak dotad)");
            sb.Append(" | panika gry w polu: ludzi ").Append(CandPeople.Count).Append(" (paniki ").Append(_candPanics)
              .Append(", pierwsza po ").Append(_firstPanic >= 0f ? S(_firstPanic) : "-").Append(" s misji)")
              .Append(" -> RoutHold ").Append(_took).Append(" (bohaterow ").Append(_tookHero).Append("; strona gracza ").Append(_tookMine)
              .Append(" / wroga ").Append(_tookFoe).Append("; bez drugiego Panic, okrzyku i animacji)")
              .Append(" | nie przejeci: zgaszona przed przejeciem ").Append(Quenched).Append(", wzieta inna droga (BrokenMen / FieldCraft / choragiew) ")
              .Append(OtherWay).Append(", padli lub uszli w kolejce ").Append(Gone).Append(", odmowy Break ").Append(Refused)
              .Append(", po koncu bitwy ").Append(_droppedAtEnd)
              .Append(" | kolejka: najwiecej czekajacych ").Append(_queueMax).Append(", czekanie sr. ")
              .Append(_took > 0 ? (_waitSum / _took).ToString("0.00", Inv) : "-").Append(" s / najdluzej ").Append(_waitMax.ToString("0.00", Inv))
              .Append(" s, klatek na limicie wpisow ").Append(_limitFrames).Append(", morale 0 czekajacym ").Append(WaitPins).Append(" razy")
              .Append(" | bez powrotu (przejeci z paniki gry): premia morale zdjeta u ludzi ").Append(PinGame.Count)
              .Append(", zgaszone mimo to (RBM po premii w tej samej klatce) ").Append(ExtinctGame).Append(" -> ponowiony odwrot bez okrzyku ").Append(ResendGame)
              .Append(", ponowne paniki gry ").Append(RePanicGame).Append(" (wszyscy zlamani ").Append(RoutHold.RePanic).Append(')')
              .Append(" | powroty RBM poza RoutHold (linia 139): ");
            if (BrokenFate.LineLocked) sb.Append(BrokenFate.ReturnsLocked).Append(" (ludzi ").Append(BrokenFate.ReturnPeopleLocked).Append(')');
            else sb.Append("- (Fate Of The Broken Log NIE)");
            sb.Append(" | wpisy RoutKeeper: najwiecej naraz ").Append(_maxLive).Append(" (limit ").Append(RoutHold.MaxEntries)
              .Append("), w bitwie razem ").Append(RoutHold.Broken + RoutHold.Fled + _took).Append(" (nasi ").Append(RoutHold.Broken + RoutHold.Fled)
              .Append(" + panika gry ").Append(_took).Append(')');
            float start = _deployed >= 0f ? _deployed : 0f;
            sb.Append(" | czas bitwy: od ").Append(_deployed >= 0f ? "rozstawienia" : "startu misji").Append(" do rozstrzygniecia ");
            if (_ended >= 0f) sb.Append(S(Math.Max(0f, _ended - start))).Append(" s, od pierwszej paniki gry ")
                                .Append(_firstPanic >= 0f ? S(Math.Max(0f, _ended - _firstPanic)) : "-").Append(" s");
            else sb.Append("- (bez rozstrzygniecia)");
            sb.Append(" (koniec misji ").Append(_missionEnd >= 0f ? S(_missionEnd) : "-").Append(" s)")
              .Append(" | tick RoutKeeper: sr. ").Append(_costFrames > 0 ? Ms((double)_costTicks / _costFrames) : "-").Append(" ms (klatek z praca ")
              .Append(_costFrames).Append("), najwiecej ").Append(Ms(_costMax)).Append(" ms")
              .Append(" | potkniecia powrot ").Append(RoutHold.StumblesLocked(RoutHold.SiteNoReturn)).Append('.');
            Log.Info(sb.ToString());
        }
    }
}
