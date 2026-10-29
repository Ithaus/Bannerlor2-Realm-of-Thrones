using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 291 (29.10, Jeff po grze na 136: "jak ja atakuje uciekajacego wroga to on zajmuje pozycje obronna i czeka na mnie a nie ze ja stoje a on mnie atakuje mimo
    /// ze to ja jego zaatakowalem; jesli ja sie bronie to dziala dobrze ... ale jak ja atakuje to przeciwnik wybiera najlepsze pozycje do obrony i czeka na mnie,
    /// moze jedynie jesli ma konnych lucznikow mnie nekac podjazdami ... ale nie wdaje sie w walke"). Klucz DefendersHoldTheirGround.
    ///
    /// DOWOD (log Jeffa 29.10 Armoury-2026-10-29_00-09-40.log, bitwa 00:46-00:49 gracz 50 ludzi atakuje Lewysa Lyddena 36 ludzi): linia NB2 'doktryna ...
    /// probka atak -, obrona T176/vlandia Westerlands (Shock 1, Shot 1, Horse 1.2, Hold 1) +RangedHarrass [FullScaleAttack s. 2]' - obronca od 2. sekundy misji
    /// w FullScaleAttack (szedl na gracza), 'S2 (141) ... wejsc w S2 0' - nie z powodu ostrzalu.
    /// DOWOD [K] (TaleWorlds.MountAndBlade 1.4.8, RBMAI): TeamAIComponent.MakeDecision co ok. 5 s - MaxBy(GetTacticWeight x 1.5 dla biezacej);
    /// TacticFullScaleAttack = piechota x sqrt(RemainingPowerRatio), TacticDefensiveEngagement = (piechota + strzelcy) x 1.1 x przewaga konnicy x wzgorze (0.7..1)
    /// / sqrt(RemainingPowerRatio) - przy wzgorzu dalej niz 150 m i piechocie 75% atak wygrywa juz przy sile x1.03, czyli obronca rowny albo slabszy wychodzi
    /// do ataku; RBM TacticFullScaleAttackPatch.PostfixGetAiWeight: obronca z piechota ponad 90% - waga ataku 100 (zawsze atak). RemainingPowerRatio (TeamQuerySystem)
    /// = (sila strony bez strat + 1) / (sila wroga bez strat + 1), cala strona (sojusznicy razem).
    ///
    /// ZASADA (druzyna AI po stronie Defender z rady wojennej - WarCouncil.One -> ApplyList; druzyna gracza poza, tlum ponizej 30 Tactics tez, pozne DR tez):
    ///  - trzyma pozycje: postfiks wag taktyk ataku (FullScaleAttack, Charge, RangedHarrassmentOffensive, FrontalCavalryCharge - Priority.Last po RBM) daje 0,
    ///    gdy ktoras z taktyk obrony tej druzyny (DefensiveEngagement, DefensiveLine, DefensiveRing, HoldChokePoint) miala w ostatniej decyzji wage > 0;
    ///    bez wagi obrony (gra: IsDefenseApplicable NIE albo brak piechoty) - nic (gra wybiera atak, bo nie ma z czego sie bronic). Konni lucznicy w obronie
    ///    harcuja jak dotad (RBM TacticDefensiveEngagementPatch.PostfixDefend: BehaviorMountedSkirmish).
    ///  - puszczone (wagi gry, RBM i doktryny jak dotad): silniejszy - RemainingPowerRatio >= 1.5 (ten sam prog "wyraznie lepiej" co MakeDecision, x1.5), powrot
    ///    do trzymania ponizej 1.0; S2 (141, obronca przestrzelany pod ostrzalem) - na czas S2; napastnik nie podchodzi - najblizszy szyk wroga nie zblizyl sie
    ///    o 25 m (S1ContactGap) przez 90 s (BaseShootSec - czas ostrzalu przed atakiem S1) - do konca bitwy; zwarcie glownej piechoty (25 m od pieszego wroga) -
    ///    do konca bitwy (taktyka obrony sama przechodzi w Engage).
    ///  - klucz NIE: pomiar bez ciecia (te same liczniki - porownanie przed / po).
    /// Stan per druzyna czyszczony w Clear (Begin / End), liczniki misji w Dh291Reset (Battle291.Begin) - linia "Bitwa (291)" pisze HorsePursuit.End po Doctrine.End.
    /// Postfiksy czytaja tylko tablice pisane z glownego watku (tick druzyn nie biegnie rownolegle z OnMissionTick - jak _mul / _s2Live); wagi obrony pisze
    /// postfiks obrony (ten sam tick druzyn, ta sama decyzja albo poprzednia). Potkniecia liczone (_dhStum), bez globalnego wylacznika.
    /// </summary>
    internal static partial class Doctrine
    {
        internal const float DhStrongIn = 1.5f, DhStrongOut = 1.0f, DhStallSec = BaseShootSec, DhStallStep = S1ContactGap, DhGrace = 6f;
        private const int DhK = 4;   // 0 DefensiveEngagement, 1 DefensiveLine, 2 DefensiveRing, 3 HoldChokePoint
        private static readonly IFormatProvider DhInv = CultureInfo.InvariantCulture;

        // ---- stan per druzyna (glowny watek; _dhRef / _dhOn czyta postfiks; _dhW pisze postfiks obrony)
        private static readonly Team[] _dhRef = new Team[MaxTeams];
        private static readonly bool[] _dhOn = new bool[MaxTeams];
        private static readonly float[] _dhW = new float[MaxTeams * DhK];
        private static readonly byte[] _dhOff = new byte[MaxTeams];          // 0 trzyma, 1 napastnik nie podchodzi, 2 zwarcie
        private static readonly bool[] _dhStrong = new bool[MaxTeams];
        private static readonly bool[] _dhStarted = new bool[MaxTeams];
        private static readonly float[] _dhBest = new float[MaxTeams];
        private static readonly float[] _dhBestAt = new float[MaxTeams];
        private static readonly float[] _dhOnAt = new float[MaxTeams];       // poczatek biezacego trzymania (-1 = nie trzyma)
        private static readonly string[] _dhTac = new string[MaxTeams];      // biezaca taktyka (nazwa)
        private static readonly string[] _dhTac0 = new string[MaxTeams];     // pierwsza taktyka po rozstawieniu

        // ---- liczniki misji (glowny watek; postfiksy - Interlocked)
        private static int _dhTeams, _dhStrongN, _dhS2N, _dhStallN, _dhContactN, _dhStum;
        private static float _dhHoldSec, _dhAtkSec;
        private static int _dhCut, _dhNoDef;
        private static string _dhSample;
        private static int _dhSampleTi = -1;

        /// <summary>Battle291.Begin (BattleSense.AfterStart, przed Doctrine.Begin i rada wojenna): liczniki linii "Bitwa (291)" od zera.</summary>
        internal static void Dh291Reset()
        {
            _dhTeams = _dhStrongN = _dhS2N = _dhStallN = _dhContactN = _dhStum = 0;
            _dhHoldSec = _dhAtkSec = 0f;
            Interlocked.Exchange(ref _dhCut, 0);
            Interlocked.Exchange(ref _dhNoDef, 0);
            _dhSample = null;
            _dhSampleTi = -1;
            _dhSampleTac = null;
        }

        private static void DhClearTeam(int i)
        {
            _dhRef[i] = null; _dhOn[i] = false; _dhOff[i] = 0; _dhStrong[i] = false; _dhStarted[i] = false; _dhBest[i] = 0f; _dhBestAt[i] = 0f; _dhOnAt[i] = -1f;
            _dhTac[i] = null; _dhTac0[i] = null;
            for (int k = 0; k < DhK; k++) _dhW[i * DhK + k] = 0f;
        }

        private static bool IsDefensive(Type t)
        {
            return t == typeof(TacticDefensiveEngagement) || t == typeof(TacticDefensiveLine) || t == typeof(TacticDefensiveRing) || t == typeof(TacticHoldChokePoint);
        }

        private static bool Offensive(TacticComponent tc)
        {
            return tc is TacticFullScaleAttack || tc is TacticCharge || tc is TacticRangedHarrassmentOffensive || tc is TacticFrontalCavalryCharge;
        }

        /// <summary>ApplyList (glowny watek): druzyna obroncy z taktyka obrony na liscie - pod 291 (takze tlum i pozne DR).</summary>
        private static void DhRegister(Team team, int ti, List<Type> have, Settings c)
        {
            if (team == null || team.Side != BattleSideEnum.Defender) return;
            bool def = false;
            int n = have != null ? have.Count : 0;
            for (int i = 0; i < n; i++) if (IsDefensive(have[i])) def = true;
            if (!def) return;
            if (!ReferenceEquals(_dhRef[ti], team)) _dhTeams++;
            DhClearTeam(ti);
            _dhRef[ti] = team;
            bool key = c != null && c.DefendersHoldTheirGround;
            _dhOn[ti] = key;   // od pierwszej decyzji gry (przed pierwszym taktem doktryny)
            _dhOnAt[ti] = key ? 0f : -1f;
        }

        /// <summary>Postfiks wag taktyk ataku (tick druzyn, po Mul): 0 przy trzymaniu, gdy obrona ma wage. Bez alokacji.</summary>
        private static void DhMul(object inst, ref float r)
        {
            if (!_on) return;
            try
            {
                var tc = inst as TacticComponent;
                var team = tc != null ? tc.Team : null;
                if (team == null) return;
                int ti = team.TeamIndex;
                if (ti < 0 || ti >= MaxTeams || !_dhOn[ti] || !ReferenceEquals(_dhRef[ti], team)) return;
                if (r <= 0f || float.IsNaN(r) || float.IsInfinity(r)) return;
                float hw = 0f;
                for (int k = 0; k < DhK; k++) { float v = _dhW[ti * DhK + k]; if (v > hw) hw = v; }
                if (hw <= 0f) { Interlocked.Increment(ref _dhNoDef); return; }
                r = 0f;
                Interlocked.Increment(ref _dhCut);
            }
            catch { Interlocked.Increment(ref _postErr); }
        }

        /// <summary>Postfiks wag taktyk obrony (tick druzyn, po Mul i S2Mul): ostatnia waga obrony druzyny obroncy (do DhMul).</summary>
        private static void DhNote(object inst, float r)
        {
            if (!_on) return;
            try
            {
                var tc = inst as TacticComponent;
                var team = tc != null ? tc.Team : null;
                if (team == null) return;
                int ti = team.TeamIndex;
                if (ti < 0 || ti >= MaxTeams || !ReferenceEquals(_dhRef[ti], team)) return;
                int k = inst is TacticDefensiveEngagement ? 0 : (inst is TacticDefensiveLine ? 1 : (inst is TacticDefensiveRing ? 2 : (inst is TacticHoldChokePoint ? 3 : -1)));
                if (k < 0) return;
                _dhW[ti * DhK + k] = float.IsNaN(r) || float.IsInfinity(r) || r < 0f ? 0f : r;
            }
            catch { Interlocked.Increment(ref _postErr); }
        }

        /// <summary>Doctrine.Tick (glowny watek, co TickEvery): warunki trzymania jednej druzyny obroncy i pomiar.</summary>
        private static void DhTick(Mission m, Team t, int ti, Settings c, float now)
        {
            bool key = c.DefendersHoldTheirGround;
            var q = t.QuerySystem;
            if (q == null) return;
            float rp = 1f;
            try { rp = q.RemainingPowerRatio; } catch { rp = 1f; }
            if (!_dhStrong[ti] && rp >= DhStrongIn) { _dhStrong[ti] = true; _dhStrongN++; }
            else if (_dhStrong[ti] && rp < DhStrongOut) _dhStrong[ti] = false;
            if (m.IsDeploymentFinished && _dhOff[ti] == 0)
            {
                var inf = MainInfantry(t);
                float fg = inf != null ? FootGap(t, inf) : float.MaxValue;
                if (inf != null && fg <= S1ContactGap) { _dhOff[ti] = 2; _dhContactN++; }
                else
                {
                    float gap = TeamGap(t);
                    if (!_dhStarted[ti]) { _dhStarted[ti] = true; _dhBest[ti] = gap; _dhBestAt[ti] = now; }
                    else if (gap < _dhBest[ti] - DhStallStep) { _dhBest[ti] = gap; _dhBestAt[ti] = now; }
                    if (now - _dhBestAt[ti] >= DhStallSec) { _dhOff[ti] = 1; _dhStallN++; }
                }
            }
            bool s2 = _s2Live[ti] && ReferenceEquals(_s2Ref[ti], t);
            bool on = key && _dhOff[ti] == 0 && !_dhStrong[ti] && !s2;
            if (_dhOn[ti] && !on && s2) _dhS2N++;
            if (on && !_dhOn[ti]) _dhOnAt[ti] = now;
            if (!on) _dhOnAt[ti] = -1f;
            _dhOn[ti] = on;
            var cur = CurTactic(t);
            if (cur != null)
            {
                _dhTac[ti] = TacName(cur);
                if (_dhTac0[ti] == null && m.IsDeploymentFinished) _dhTac0[ti] = _dhTac[ti] + " s. " + ((int)now).ToString(DhInv);
            }
            if (on)
            {
                _dhHoldSec += TickEvery;
                // gra decyduje co ok. 5 s - pierwsze DhGrace s trzymania bez liczenia (stara taktyka ataku jeszcze biezaca)
                if (cur != null && Offensive(cur) && _dhOnAt[ti] >= 0f && now - _dhOnAt[ti] >= DhGrace) _dhAtkSec += TickEvery;
            }
            if (_dhSampleTi < 0 && m.IsDeploymentFinished)
            {
                _dhSampleTi = ti;
                _dhSample = "T" + _tacOf[ti].ToString(DhInv) + "/" + (_culOf[ti] ?? "?") + " sila x" + rp.ToString("0.00", DhInv) + (on ? " trzyma" : " nie trzyma");
            }
        }

        /// <summary>Segment B linii "Bitwa (291)" (HorsePursuit.End po Doctrine.End - stan druzyn wyczyszczony; probka i liczniki zostaja).</summary>
        internal static string Dh291Segment()
        {
            var c = Settings.Current;
            bool key = c != null && c.DefendersHoldTheirGround;
            var sb = new StringBuilder(400);
            sb.Append("B obronca trzyma pozycje ").Append(key ? "TAK" : "NIE (tylko pomiar)").Append(": druzyn obroncy ").Append(_dhTeams)
              .Append(", sekund trzymania ").Append(_dhHoldSec.ToString("0", DhInv)).Append(", wagi ataku ciete ").Append(Volatile.Read(ref _dhCut))
              .Append(" (bez wagi obrony ").Append(Volatile.Read(ref _dhNoDef)).Append("), taktyka ataku przy trzymaniu ").Append(_dhAtkSec.ToString("0", DhInv))
              .Append(" s; puszczone: silniejszy x").Append(DhStrongIn.ToString("0.0", DhInv)).Append(' ').Append(_dhStrongN).Append(", S2 ").Append(_dhS2N)
              .Append(", napastnik nie podchodzi ").Append(DhStallSec.ToString("0", DhInv)).Append(" s ").Append(_dhStallN).Append(", zwarcie ").Append(_dhContactN)
              .Append("; probka ").Append(_dhSample ?? "-").Append(", taktyka ").Append(_dhSampleTac ?? "-").Append("; potkniecia ").Append(_dhStum);
            return sb.ToString();
        }

        private static string _dhSampleTac;

        /// <summary>Doctrine.End (przed Clear): taktyka probki (pierwsza po rozstawieniu i na koniec) do segmentu B.</summary>
        private static void DhEnd()
        {
            int ti = _dhSampleTi;
            _dhSampleTac = ti >= 0 && ti < MaxTeams ? (_dhTac0[ti] ?? "-") + " -> " + (_dhTac[ti] ?? "-") : null;
        }
    }
}
