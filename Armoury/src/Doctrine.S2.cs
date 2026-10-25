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
    /// 141 (25.10, fala "AI walczy o zycie", synteza "137 S2: obronca nie stoi pod strzalami"; Jeff 13:30: "Jak widzi ze kolega padl od strzaly a ma tarcze to
    /// zalozy tarcze i sie za nia schowa i idzie na przeciwnika", decyzja D1: naprzod do zwarcia). Druzyna AI, ktora broni sie bez strzelcow pod deszczem strzal,
    /// nie czeka na smierc w miejscu: waga DefensiveLine i DefensiveEngagement x0.3, wiec wybor taktyki gry przechodzi na atak (tarcze w gore robi 140).
    ///
    /// DOWOD [K] (TaleWorlds.MountAndBlade 1.4.8 - scratchpad/dec/mbfull; RBMAI - scratchpad/dec/rbmai):
    ///  - TeamAIComponent.MakeDecision (:254-335, co 5 s z ticku druzyn): MaxBy(GetTacticWeight x 1.5 dla biezacej), zmiana tylko gdy waga nowej > 1.5 x waga
    ///    biezacej; GetTacticWeight wola tylko MakeDecision (grep calej gry i RBM);
    ///  - TacticDefensiveEngagement.GetTacticWeight (:173-192) = (piechota + strzelcy) x 1.1 x przewaga x wzgorze / sqrt(RemainingPowerRatio), TacticDefensiveLine
    ///    (:204-219) x 1.2 x punktacja pozycji; TacticFullScaleAttack (:179-183) = piechota x sqrt(RemainingPowerRatio) - przy rownych silach obrona wygrywa
    ///    nieznacznie, x0.3 z histereza 1.5 (0.45) odwraca wybor;
    ///  - CheckIsDefenseApplicable (:215-247) liczy ostrzal przez TeamQuerySystem.MaxUnderRangedAttackRatio, a ten pomija ludzi z tarcza w ekwipunku
    ///    (TeamQuerySystem.cs:545) - piechota z tarczami pod ostrzalem dla gry nie jest pod ostrzalem i obrona zostaje do konca;
    ///  - RBM EarlyStart (Tactics.cs:253-337): obronca ma DefensiveEngagement, DefensiveLine, FullScaleAttack, CoordinatedRetreat (+ splity wedlug kultury);
    ///    TacticFullScaleAttackPatch.cs:92-104 daje obroncy z piechota ponad 90% wage ataku 100 (taki i tak atakuje - S2 dotyczy reszty);
    ///  - FormationQuerySystem.UnderRangedAttackRatio (:489-494) = czesc szyku z Agent.LastRangedHitTime w ostatnich 10 s; LastRangedHitTime tylko przy
    ///    obrazeniach (Agent.cs:5438-5442) - strzala w tarczy sie nie liczy (to miara strat, nie lecacych pociskow jak w 140).
    ///
    /// ZASADY:
    ///  - tylko druzyny z rady wojennej (WarCouncil.One -> ApplyList): AI z TeamAIGeneral, bitwa polowa; druzyna GRACZA poza (One jej nie bierze), pozne DR poza;
    ///  - warunek (Tick co TickEvery = 2 s, glowny watek, po rozstawieniu): strzelcy strony (sojusznicy razem - lucznicy gracza tez oslaniaja, liczba ludzi)
    ///    x 3 &lt; strzelcy wroga; glowna piechota (najwiekszy szyk piechoty druzyny - jak DefensiveEngagement) - czesc ludzi w trybie ROZSADEK (przelacznik 139)
    ///    z LastRangedHitTime w 10 s &gt;= 0.2 (miara UnderRangedAttackRatio gry bez ludzi w PANICE); najblizszy PIESZY szyk wroga &lt;= 200 m od glownej piechoty
    ///    (zasieg S1; konni lucznicy sami nie wystarcza - piechota ich nie dogoni);
    ///  - wejscie po 20 s warunku bez przerwy, wyjscie po 10 s bez warunku; po zwarciu (glowna piechota &lt;= 25 m od pieszego wroga, prog S1) S2 trzyma do konca
    ///    bitwy (jak S1 "do konca bitwy" po zwarciu) - bez powrotu na linie obrony w srodku walki;
    ///  - bez FullScaleAttack i Charge na liscie taktyk S2 nie wchodzi (sam x0.3 przerzucilby druzyne na pierscien albo przesmyk) - liczone ("bez taktyki ataku");
    ///  - mnoznik tylko instancji TacticDefensiveLine i TacticDefensiveEngagement (DefensiveRing, HoldChokePoint, splity RBM bez zmian), na wierzchu Hold kultury;
    ///  - klucz NIE (a inny klucz doktryny TAK): ten sam stan i liczniki - pomiar wyjsciowy do porownania przed / po - bez mnoznika.
    /// Stan per druzyna czyszczony w Begin i End (Clear); postfiks czyta tylko _s2Ref / _s2Live (zapis z glownego watku, tick druzyn nie biegnie rownolegle).
    /// Potkniecia: per druzyna liczone (_s2Stum) i RoutHold.Stumble(SiteDoctrine) - funkcja dalej dziala.
    /// </summary>
    internal static partial class Doctrine
    {
        internal const float S2Hold = 0.3f, S2Ratio = 0.2f, S2LowRatio = 0.1f, S2EnterSec = 20f, S2ExitSec = 10f, S2Range = 200f, S2HitWindow = 10f;
        private static readonly IFormatProvider S2Inv = CultureInfo.InvariantCulture;

        // ---- stan per druzyna (glowny watek; _s2Ref i _s2Live czyta postfiks w ticku druzyn - jak _teamRef / _mul)
        private static readonly Team[] _s2Ref = new Team[MaxTeams];
        private static readonly bool[] _s2Live = new bool[MaxTeams];       // mnoznik dziala: stan S2 i klucz TAK
        private static readonly bool[] _s2State = new bool[MaxTeams];      // stan S2 (takze przy kluczu NIE - pomiar)
        private static readonly bool[] _s2Atk = new bool[MaxTeams];        // na liscie FullScaleAttack albo Charge
        private static readonly bool[] _s2NoAtkSeen = new bool[MaxTeams];
        private static readonly bool[] _s2Latch = new bool[MaxTeams];      // zwarcie w stanie S2 - do konca bitwy
        private static readonly bool[] _s2Moved = new bool[MaxTeams];      // taktyka zmieniona od wejscia
        private static readonly bool[] _s2Outshot = new bool[MaxTeams];
        private static readonly float[] _s2Since = new float[MaxTeams];    // poczatek warunku bez przerwy, -1 = brak
        private static readonly float[] _s2Quiet = new float[MaxTeams];    // poczatek ciszy w stanie S2, -1 = brak
        private static readonly float[] _s2OnAt = new float[MaxTeams];
        private static readonly float[] _s2FireAt = new float[MaxTeams];   // pierwszy warunek (pomiar: czas pod ostrzalem przed zwarciem), -1
        private static readonly float[] _s2ContactAt = new float[MaxTeams];// pierwsze zwarcie glownej piechoty, -1
        private static readonly int[] _s2DeadAtContact = new int[MaxTeams];
        private static readonly Type[] _s2TacAt = new Type[MaxTeams];

        // ---- liczniki misji (glowny watek; linia w End)
        private static bool _s2Field;
        private static int _s2Teams, _s2LateSkip, _s2OutTeams, _s2Enter, _s2Exit, _s2Latched, _s2NoAttack, _s2AtLine, _s2AtEng, _s2AtOther,
                           _s2ToAttack, _s2ToOther, _s2NoSwitch, _s2PanicSkip, _s2NoInf, _s2Stum;
        private static float _s2SecFire, _s2SecLow, _s2MaxRatio;
        private static double _s2ToAttackSec;
        private static string _s2OtherAt, _s2OtherTo, _s2Sample;
        private static int _s2SampleTi = -1;
        private static bool _s2SampleMoved;
        private static int _s2Calls;                                         // Interlocked (postfiks, tick druzyn)

        // ---- liczenie trafionych w glownej piechocie (glowny watek)
        private static float _hNow;
        private static int _hAll, _hHit, _hPanic;
        private static readonly Action<Agent> HitCount = a =>
        {
            if (a == null || !a.IsActive()) return;
            if (SurvivalMode.InPanic(a)) { _hPanic++; return; }
            _hAll++;
            if (_hNow - a.LastRangedHitTime < S2HitWindow) _hHit++;
        };

        /// <summary>Postfiks Hold (tick druzyn, po Mul): x0.3 tylko DefensiveLine i DefensiveEngagement druzyny w stanie S2 przy kluczu TAK. Bez alokacji.</summary>
        private static void S2Mul(object inst, ref float r)
        {
            if (!_on) return;
            try
            {
                if (!(inst is TacticDefensiveLine) && !(inst is TacticDefensiveEngagement)) return;
                var team = ((TacticComponent)inst).Team;
                if (team == null) return;
                int ti = team.TeamIndex;
                if (ti < 0 || ti >= MaxTeams || !_s2Live[ti] || !ReferenceEquals(_s2Ref[ti], team)) return;
                if (r <= 0f || float.IsNaN(r) || float.IsInfinity(r)) return;
                r *= S2Hold;
                Interlocked.Increment(ref _s2Calls);
            }
            catch { Interlocked.Increment(ref _postErr); }
        }

        /// <summary>ApplyList (glowny watek): druzyna rady wojennej pod S2 - pozna DR poza; czy lista ma FullScaleAttack albo Charge (po dodatkach doktryny).</summary>
        private static void S2Register(Team team, int ti, List<Type> have, bool late)
        {
            if (late) { _s2Ref[ti] = null; _s2LateSkip++; return; }
            bool atk = false;
            int n = have != null ? have.Count : 0;
            for (int i = 0; i < n; i++) if (have[i] == typeof(TacticFullScaleAttack) || have[i] == typeof(TacticCharge)) atk = true;
            _s2Atk[ti] = atk;
            if (!ReferenceEquals(_s2Ref[ti], team)) _s2Teams++;
            _s2Ref[ti] = team;
        }

        private static void S2ClearTeam(int i)
        {
            _s2Ref[i] = null; _s2Live[i] = false; _s2State[i] = false; _s2Atk[i] = false; _s2NoAtkSeen[i] = false; _s2Latch[i] = false; _s2Moved[i] = false;
            _s2Outshot[i] = false; _s2Since[i] = -1f; _s2Quiet[i] = -1f; _s2OnAt[i] = 0f; _s2FireAt[i] = -1f; _s2ContactAt[i] = -1f; _s2DeadAtContact[i] = 0;
            _s2TacAt[i] = null;
        }

        private static void S2ResetCounters(Mission m)
        {
            _s2Field = m != null && m.IsFieldBattle;
            _s2Teams = _s2LateSkip = _s2OutTeams = _s2Enter = _s2Exit = _s2Latched = _s2NoAttack = _s2AtLine = _s2AtEng = _s2AtOther = 0;
            _s2ToAttack = _s2ToOther = _s2NoSwitch = _s2PanicSkip = _s2NoInf = _s2Stum = 0;
            _s2SecFire = _s2SecLow = _s2MaxRatio = 0f;
            _s2ToAttackSec = 0d;
            _s2OtherAt = _s2OtherTo = _s2Sample = null;
            _s2SampleTi = -1;
            _s2SampleMoved = false;
            Interlocked.Exchange(ref _s2Calls, 0);
        }

        /// <summary>Doctrine.Tick (glowny watek, co TickEvery, po rozstawieniu): warunek, histereza i pomiar jednej druzyny.</summary>
        private static void S2Tick(Mission m, Team t, int ti, Settings c, float now)
        {
            bool key = c.DefendersAdvanceUnderFire;
            var q = t.QuerySystem;
            if (q == null) return;
            // strzelcy strony (sojusznicy razem) i wroga - liczba ludzi, nie odsetek (rozne wielkosci armii)
            float ours = (q.AllyRangedRatio + q.AllyRangedCavalryRatio) * q.AllyUnitCount;
            float enemy = (q.EnemyRangedRatio + q.EnemyRangedCavalryRatio) * q.EnemyUnitCount;
            bool outshot = enemy >= 1f && ours * 3f < enemy;
            var inf = MainInfantry(t);
            float ratio = 0f, gap = float.MaxValue;
            if (inf != null) { ratio = HitRatio(inf, now); gap = FootGap(t, inf); }
            else if (outshot) _s2NoInf++;
            bool cond = outshot && inf != null && ratio >= S2Ratio && gap <= S2Range;
            if (outshot && !_s2Outshot[ti]) { _s2Outshot[ti] = true; _s2OutTeams++; }
            if (outshot && inf != null && gap <= S2Range)
            {
                if (ratio > _s2MaxRatio) _s2MaxRatio = ratio;
                if (ratio >= S2Ratio) _s2SecFire += TickEvery; else if (ratio >= S2LowRatio) _s2SecLow += TickEvery;
            }
            // pomiar przed / po: pierwszy ostrzal (warunek) i pierwsze zwarcie glownej piechoty (padli od pociskow wedlug licznika gry)
            if (cond && _s2FireAt[ti] < 0f) _s2FireAt[ti] = now;
            if (inf != null && gap <= S1ContactGap && _s2ContactAt[ti] < 0f) { _s2ContactAt[ti] = now; _s2DeadAtContact[ti] = q.DeathByRangedCount; }

            if (!_s2State[ti])
            {
                if (!cond) _s2Since[ti] = -1f;
                else
                {
                    if (_s2Since[ti] < 0f) _s2Since[ti] = now;
                    if (now - _s2Since[ti] >= S2EnterSec)
                    {
                        if (_s2Atk[ti]) S2Enter(t, ti, now, ratio, ours, enemy, key);
                        else if (!_s2NoAtkSeen[ti]) { _s2NoAtkSeen[ti] = true; _s2NoAttack++; }
                    }
                }
            }
            else
            {
                if (!_s2Latch[ti] && inf != null && gap <= S1ContactGap) { _s2Latch[ti] = true; _s2Latched++; }
                if (_s2Latch[ti] || cond) _s2Quiet[ti] = -1f;
                else
                {
                    if (_s2Quiet[ti] < 0f) _s2Quiet[ti] = now;
                    if (now - _s2Quiet[ti] >= S2ExitSec) S2Leave(ti);
                }
                if (_s2State[ti] && !_s2Moved[ti]) S2Watch(t, ti, now);
            }
            _s2Live[ti] = _s2State[ti] && key;
        }

        private static void S2Enter(Team t, int ti, float now, float ratio, float ours, float enemy, bool key)
        {
            _s2State[ti] = true;
            _s2Quiet[ti] = -1f;
            _s2OnAt[ti] = now;
            _s2Moved[ti] = false;
            _s2Enter++;
            var cur = CurTactic(t);
            _s2TacAt[ti] = cur != null ? cur.GetType() : null;
            if (cur is TacticDefensiveLine) _s2AtLine++;
            else if (cur is TacticDefensiveEngagement) _s2AtEng++;
            else { _s2AtOther++; _s2OtherAt = AddName(_s2OtherAt, TacName(cur)); }
            if (_s2Sample != null) return;
            _s2SampleTi = ti;
            var s = new StringBuilder(200);
            s.Append('T').Append(_tacOf[ti]).Append('/').Append(_culOf[ti] ?? "?").Append(t.Side == BattleSideEnum.Defender ? " obrona" : " atak")
             .Append(": ostrzal ").Append(ratio.ToString("0.00", S2Inv)).Append(" przez ").Append(((int)(now - _s2Since[ti])).ToString(S2Inv))
             .Append(" s, strzelcy ").Append(enemy > 0f ? (ours / enemy).ToString("0.0#", S2Inv) : "-").Append(" wroga (").Append(((int)ours).ToString(S2Inv))
             .Append('/').Append(((int)enemy).ToString(S2Inv)).Append(')').Append(key ? " -> Hold x0.3; " : " -> klucz NIE, bez mnoznika; ").Append(TacName(cur));
            _s2Sample = s.ToString();
        }

        private static void S2Leave(int ti)
        {
            if (!_s2Moved[ti]) _s2NoSwitch++;
            _s2State[ti] = false;
            _s2Live[ti] = false;
            _s2Since[ti] = -1f;
            _s2Quiet[ti] = -1f;
            _s2Exit++;
        }

        /// <summary>Pierwsza zmiana taktyki od wejscia w S2 (MakeDecision gry co 5 s).</summary>
        private static void S2Watch(Team t, int ti, float now)
        {
            var cur = CurTactic(t);
            if (cur == null) return;
            var was = _s2TacAt[ti];
            if (was != null && cur.GetType() == was) return;
            _s2Moved[ti] = true;
            float sec = now - _s2OnAt[ti];
            if (cur is TacticFullScaleAttack || cur is TacticCharge) { _s2ToAttack++; _s2ToAttackSec += sec; }
            else { _s2ToOther++; _s2OtherTo = AddName(_s2OtherTo, TacName(cur)); }
            if (ti == _s2SampleTi && _s2Sample != null && !_s2SampleMoved)
            {
                _s2SampleMoved = true;
                _s2Sample = _s2Sample + " -> " + TacName(cur) + " po " + ((int)sec).ToString(S2Inv) + " s";
            }
        }

        private static TacticComponent CurTactic(Team t)
        {
            if (_fCur == null || t == null) return null;
            try { var ai = t.TeamAI; return ai != null ? _fCur(ai) : null; } catch { return null; }
        }

        private static string TacName(TacticComponent tc)
        {
            if (tc == null) return "?";
            string nm = tc.GetType().Name;
            return nm.StartsWith("Tactic", StringComparison.Ordinal) ? nm.Substring(6) : nm;
        }

        private static string AddName(string list, string nm)
        {
            if (list == null) return nm;
            if (list.Length > 120 || (", " + list + ",").Contains(", " + nm + ",")) return list;
            return list + ", " + nm;
        }

        /// <summary>Glowna piechota: najwiekszy szyk piechoty druzyny (jak TacticDefensiveEngagement._mainInfantry).</summary>
        private static Formation MainInfantry(Team t)
        {
            Formation best = null;
            var fl = t.FormationsIncludingSpecialAndEmpty;
            int nf = fl != null ? fl.Count : 0;
            for (int j = 0; j < nf; j++)
            {
                var f = fl[j];
                if (f == null || f.CountOfUnits <= 0) continue;
                var fq = f.QuerySystem;
                if (fq == null || !fq.IsInfantryFormation) continue;
                if (best == null || f.CountOfUnits > best.CountOfUnits) best = f;
            }
            return best;
        }

        /// <summary>Czesc ludzi szyku w trybie ROZSADEK trafionych pociskiem w ostatnich 10 s (definicja UnderRangedAttackRatio gry bez PANIKI).</summary>
        private static float HitRatio(Formation f, float now)
        {
            _hNow = now;
            _hAll = _hHit = _hPanic = 0;
            f.ApplyActionOnEachUnit(HitCount);
            _s2PanicSkip += _hPanic;
            return _hAll > 0 ? (float)_hHit / _hAll : 0f;
        }

        /// <summary>Najblizszy PIESZY szyk wroga (piechota albo lucznicy, bez konnicy i konnych lucznikow) - mediana do mediany minus polowy glebokosci.</summary>
        private static float FootGap(Team team, Formation f)
        {
            float best = float.MaxValue;
            var my = f.CachedMedianPosition.AsVec2;
            var teams = team.Mission != null ? team.Mission.Teams : null;
            int n = teams != null ? teams.Count : 0;
            for (int i = 0; i < n; i++)
            {
                var t = teams[i];
                if (!MissionTeams.Enemies(team, t)) continue;
                var fl = t.FormationsIncludingSpecialAndEmpty;
                int nf = fl != null ? fl.Count : 0;
                for (int j = 0; j < nf; j++)
                {
                    var e = fl[j];
                    if (e == null || e.CountOfUnits <= 0) continue;
                    var eq = e.QuerySystem;
                    if (eq == null || eq.IsCavalryFormation || eq.IsRangedCavalryFormation) continue;
                    float d = my.Distance(e.CachedMedianPosition.AsVec2) - 0.5f * (f.Depth + e.Depth);
                    if (d < best) best = d;
                }
            }
            return best < 0f ? 0f : best;
        }

        /// <summary>Doctrine.End (glowny watek, raz na misje - _m zerowane po nas): linia "S2 (141)".</summary>
        private static void S2End(Mission m)
        {
            var w = _m;
            if (w == null || m == null || !ReferenceEquals(w.Target, m) || !_s2Field) return;
            var c = Settings.Current;
            bool key = c != null && c.DefendersAdvanceUnderFire;
            int fireTeams = 0, deadBefore = 0, noContact = 0, deadNoContact = 0, stillOn = 0;
            double fireSec = 0d;
            for (int i = 0; i < MaxTeams; i++)
            {
                var t = _s2Ref[i];
                if (t == null) continue;
                if (_s2State[i]) { stillOn++; if (!_s2Moved[i]) _s2NoSwitch++; }
                if (_s2FireAt[i] < 0f) continue;
                if (_s2ContactAt[i] >= _s2FireAt[i]) { fireTeams++; fireSec += _s2ContactAt[i] - _s2FireAt[i]; deadBefore += _s2DeadAtContact[i]; }
                else if (_s2ContactAt[i] < 0f)
                {
                    noContact++;
                    try { var q = t.QuerySystem; if (q != null) deadNoContact += q.DeathByRangedCount; } catch { }
                }
            }
            if (!key && _s2OutTeams == 0 && _s2Enter == 0) return;   // klucz NIE i nikt nie byl przestrzelany - bez linii
            int stum = _s2Stum;
            var sb = new StringBuilder(1200);
            sb.Append("S2 (141): misja ").Append(MissionTeams.Kind(m)).Append(", klucz ").Append(key ? "TAK" : "NIE (tylko pomiar)")
              .Append(" (obronca bez strzelcow pod ostrzalem idzie do zwarcia: Hold x").Append(S2Hold.ToString("0.0", S2Inv))
              .Append(" tylko DefensiveLine i DefensiveEngagement; strzelcy strony x3 < wroga, glowna piechota >= ").Append(((int)(S2Ratio * 100f)).ToString(S2Inv))
              .Append("% ludzi w ROZSADKU trafionych pociskiem w 10 s przez ").Append(S2EnterSec.ToString("0", S2Inv)).Append(" s, pieszy wrog <= ")
              .Append(S2Range.ToString("0", S2Inv)).Append(" m; wyjscie po ").Append(S2ExitSec.ToString("0", S2Inv)).Append(" s bez ostrzalu, po zwarciu (")
              .Append(S1ContactGap.ToString("0", S2Inv)).Append(" m) do konca bitwy; druzyna gracza poza)")
              .Append(" | druzyn ").Append(_s2Teams).Append(" (pozne DR pominiete ").Append(_s2LateSkip).Append(')')
              .Append(" | przestrzelane 3:1 druzyny ").Append(_s2OutTeams).Append(" (bez piechoty ").Append(_s2NoInf).Append(" probek): maks. trafionych piechoty ")
              .Append(_s2MaxRatio.ToString("0.00", S2Inv)).Append(", pod ostrzalem >= 0.2 ").Append(_s2SecFire.ToString("0", S2Inv)).Append(" s, 0.1-0.2 ")
              .Append(_s2SecLow.ToString("0", S2Inv)).Append(" s (suma druzyn, wrog pieszy <= ").Append(S2Range.ToString("0", S2Inv)).Append(" m); w panice pominieto ")
              .Append(_s2PanicSkip).Append(" probek ludzi")
              .Append(" | wejsc w S2 ").Append(_s2Enter).Append(" (wyjsc po ciszy ").Append(_s2Exit).Append(", zwarc w S2 - do konca bitwy ").Append(_s2Latched)
              .Append(", w S2 na koncu ").Append(stillOn).Append("); taktyka przy wejsciu: DefensiveLine ").Append(_s2AtLine).Append(", DefensiveEngagement ").Append(_s2AtEng)
              .Append(", inna ").Append(_s2AtOther).Append(_s2OtherAt != null ? " [" + _s2OtherAt + "]" : "")
              .Append(" | po wejsciu: na atak (FullScaleAttack / Charge) ").Append(_s2ToAttack).Append(" (sr. po ").Append(_s2ToAttack > 0 ? (_s2ToAttackSec / _s2ToAttack).ToString("0", S2Inv) : "-")
              .Append(" s), na inna ").Append(_s2ToOther).Append(_s2OtherTo != null ? " [" + _s2OtherTo + "]" : "").Append(", bez zmiany taktyki ").Append(_s2NoSwitch)
              .Append(" | bez taktyki ataku: ").Append(_s2NoAttack)
              .Append(" | wywolan z Hold x").Append(S2Hold.ToString("0.0", S2Inv)).Append(' ').Append(Volatile.Read(ref _s2Calls))
              .Append(" | czas pod ostrzalem przed zwarciem: druzyn ").Append(fireTeams).Append(", sr. ").Append(fireTeams > 0 ? (fireSec / fireTeams).ToString("0", S2Inv) : "-")
              .Append(" s, padli od pociskow do zwarcia ").Append(deadBefore).Append("; pod ostrzalem bez zwarcia do konca: druzyn ").Append(noContact)
              .Append(" (padli od pociskow ").Append(deadNoContact).Append(')')
              .Append(" | probka ").Append(_s2Sample ?? "-")
              .Append(" | potkniecia S2 ").Append(stum).Append('.');
            Log.Info(sb.ToString());
        }
    }
}
