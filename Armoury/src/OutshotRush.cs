using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 313b krok 2 (01.11, klucz OutshotRushWithShields; Jeff: "Jak strzelam z luku na koniu do piechoty to ta na mnie biegnie a nie wolno idzie" - piechota pod
    /// ostrzalem ma BIEC do zwarcia; krytyka 313 luka 1). DOWOD (log Jeffa Armoury-2026-11-01_15-03-28.log, bitwa 15:16:54-15:21:50): banda 14 ludzi z tarczami
    /// w FullScaleAttack ('napastnik nie podchodzi 90 s 1', 'S2 ... inna 1 [FullScaleAttack]'), 291 C 'z tarczami pod ostrzalem zostal krok 1 (sr. tarcz 1.00)',
    /// HIT z 4-28 m od gracza - konnego lucznika ('konny strzelec 49'), 'pod ostrzalem bez zwarcia do konca: padli od pociskow 9'. Szla krokiem (134c/144) za
    /// murem tarcz (146/310) za koniem, ktory odjezdzal.
    ///
    /// ZASADA (glowny watek co 0.5 s, druzyna AI w bitwie polowej): druzyna idzie pod ostrzalem = Doctrine.OutshotAdvancing - co najmniej Doctrine.FireDead (2)
    /// zabitych od pociskow (TeamQuerySystem.DeathByRangedCount), nie trzyma pozycji 291, a jest puszczona (291e bilans ognia albo S2 141) albo ma biezaca
    /// taktyke ataku. Jej szyki piechoty AI pod ostrzalem (miara gry UnderRangedAttackRatio >= 0.33 albo sygnal S4 140):
    ///  - MarchToContact: bieg do zwarcia takze z tarczami (ta sama droga co 291 C - bez capu marszu, AiBreath z zapasem pod ostrzalem 25% przez Rushing,
    ///    koniec przy zwarciu / szarzy / zmianie rozkazu; odwrot FallBack, szyki gracza, smoki - nigdy). Recenzja 313b (3): tylko przy aktywnym BehaviorAdvance
    ///    albo BehaviorCharge (Defend / Regroup / PullBack z rozkazem Move do pozycji - krok jak dotad; gra moze sama wrocic do obrony po ustaniu ognia);
    ///  - 283 R4 / R5 (HorsePursuit.ReactHalt, recenzja 313b (1)): druzyna Live nie staje, postoj zalozony wczesniej puszczony (powod 'ostrzal');
    ///  - CloseRanks (146 / 310): w Advance i Charge Loose zamiast ShieldWall (mur tarcz ma limit biegu 0.3), chyba ze szyk konnicy uderzeniowej wroga jest
    ///    w 90 m (K11 - luzny szyk pod szarza to rzez; konni strzelcy sie nie licza) - wtedy decyzja jak dotad;
    ///  - cel (postfiks Formation.CacheClosestEnemyFormation, tick druzyn): gdy najblizszy szyk wroga to szyk konnych lucznikow AI (IsRangedCavalryFormation
    ///    bez gracza), najblizszy szyk ma byc jego pieszy szyk (piechota albo lucznicy pieszo). Recenzja 313b (WYSOKA 1): szyk z GRACZEM nie jest przekierowany -
    ///    Jeff: "to ta NA MNIE biegnie" - banda biegnie na gracza-konnego lucznika, nie na jego piechote. Cel zmienia BehaviorCharge gry (bandy: RBM oddaje
    ///    bitwy z bandytami grze, ChargeToTarget(CachedClosestEnemyFormation)); BehaviorAdvance bierze go tylko przy jednym szyku wroga (przy >= 2 -
    ///    Team.MedianTargetFormation, recenzja 313b (5)); RBM poza bandami bierze cel z FindSignificantEnemy bez konnicy (bez zmian). Dystansu
    ///    CachedClosestEnemyFormationDistanceSquared NIE zmieniamy (recenzja 313b (4)) - ShieldsUp.Near (zakaz zmiany broni w 8 m) liczy dalej do konnego
    ///    strzelca. Pozostali czytelnicy samego celu (MarchToContact.EnemyGap - zwarcie i prog szarzy, AiBreath d_szyk, HorsePursuit.PullWhy) widza pieszy szyk.
    ///    Brak pieszego szyku wroga - cel jak dotad (bieg na konnego strzelca, ale nie stoi).
    /// Klucz NIE: nic (bez pomiaru na sucho - te same liczniki 291 C pokazuja krok z tarczami). Linia: segment E linii 'Bitwa (291)' (Battle291.Write); recenzja
    /// 313b (4): szyk w biegu wedlug faktycznego szyku (Loose / ShieldWall / inny) i srednia predkosc srodka szyku w biegu i w szarzy pod ostrzalem (sedno skargi:
    /// biegnie czy idzie - takze czy 140 DefendDown pierwszego szeregu nie hamuje biegu).
    /// Watki: Tick i liczniki biegu / szyku na glownym watku; postfiks w ticku druzyn (nie biegnie rownolegle z OnMissionTick - Mission.cs:3770) czyta tylko
    /// tablice z Ticku, liczniki Interlocked, bez alokacji. Potkniecia liczone (bez globalnego wylacznika).
    /// </summary>
    internal static class OutshotRush
    {
        private const int MaxTeams = 16;
        private const float TickEvery = 0.5f;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ---- stan (glowny watek; postfiks czyta)
        private static volatile bool _on;
        private static readonly Team[] _ref = new Team[MaxTeams];
        private static readonly bool[] _live = new bool[MaxTeams];
        private static readonly bool[] _ever = new bool[MaxTeams];
        private static WeakReference _m;
        private static float _acc;

        // ---- latka (raz na proces)
        private static bool _tried;
        private static string _patch = "bez latki";
        private static AccessTools.FieldRef<Formation, Formation> _fClosest;

        // ---- liczniki misji (Reset w Battle291.Begin)
        private static int _teams, _byFire, _byS2, _byAtk, _stum;
        private static int _rushIn, _rushOut;
        private static float _rushSec;
        // recenzja 313b (6): klucz int (TeamIndex x 16 + FormationIndex) - bez referencji do szykow (i Mission) po bitwie
        private static readonly HashSet<int> _rushForms = new HashSet<int>(), _looseForms = new HashSet<int>(), _horseForms = new HashSet<int>();
        private static int _looseN, _wallWouldN;
        // recenzja 313b (4): szyko-sekundy biegu wedlug faktycznego szyku, droga i czas srodka szyku (bieg 313b, szarza bez capu pod ostrzalem)
        private static float _secLoose, _secWall, _secOther, _chargeSec;
        private static double _runDist, _runTime, _chgDist, _chgTime;
        private static int _redir, _redirCharge, _redirNoFoot, _postErr;   // Interlocked (tick druzyn)

        /// <summary>Doctrine.ApplyMissionPatches (pierwsza bitwa, raz na proces): postfiks Formation.CacheClosestEnemyFormation (DeclaredMethod - pulapka 15.10).</summary>
        internal static string ApplyPatch(Harmony h)
        {
            if (_tried) return _patch;
            _tried = true;
            try
            {
                var t = AccessTools.DeclaredMethod(typeof(Formation), "CacheClosestEnemyFormation");
                if (t == null) { _patch = "NIE (brak metody)"; return _patch; }
                _fClosest = AccessTools.FieldRefAccess<Formation, Formation>("_cachedClosestEnemyFormation");
                h.Patch(t, postfix: new HarmonyMethod(typeof(OutshotRush), nameof(ClosestPost)) { priority = Priority.Last });
                bool ok = false;
                var info = Harmony.GetPatchInfo(t);
                if (info != null) foreach (var p in info.Postfixes) if (p != null && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(OutshotRush)) ok = true;
                if (!ok) _fClosest = null;
                _patch = ok ? "TAK" : "NIEWPIETA";
            }
            catch (Exception e)
            {
                _fClosest = null;
                _patch = "NIE (" + e.GetType().Name + ")";
                RoutHold.Stumble(RoutHold.SiteDoctrine, "OutshotRush.Latka", e);
            }
            return _patch;
        }

        /// <summary>Battle291.Begin (AfterStart, przed Begin paczek): liczniki segmentu E od zera, stan bez druzyn.</summary>
        internal static void Reset()
        {
            ClearState();
            _teams = _byFire = _byS2 = _byAtk = _stum = 0;
            _rushIn = _rushOut = 0;
            _rushSec = 0f;
            _rushForms.Clear(); _looseForms.Clear(); _horseForms.Clear();
            _looseN = _wallWouldN = 0;
            _secLoose = _secWall = _secOther = _chargeSec = 0f;
            _runDist = _runTime = _chgDist = _chgTime = 0.0;
            Interlocked.Exchange(ref _redir, 0); Interlocked.Exchange(ref _redirCharge, 0); Interlocked.Exchange(ref _redirNoFoot, 0); Interlocked.Exchange(ref _postErr, 0);
        }

        private static void ClearState()
        {
            _on = false;
            for (int i = 0; i < MaxTeams; i++) { _ref[i] = null; _live[i] = false; _ever[i] = false; }
            _acc = 0f;
        }

        /// <summary>BattleSense.Clean (po Doctrine.End): postfiks wylaczony, stan druzyn wyczyszczony; liczniki zostaja do linii 'Bitwa (291)' (HorsePursuit.End).</summary>
        internal static void End()
        {
            _on = false;
            for (int i = 0; i < MaxTeams; i++) { _ref[i] = null; _live[i] = false; }
            _m = null;
        }

        /// <summary>BattleSense.OnMissionTick (po Doctrine.Tick - stan 291e / S2 tego taktu): co 0.5 s - ktore druzyny ida pod ostrzalem.</summary>
        internal static void Tick(Mission m, float dt)
        {
            if (m == null) return;
            _acc += dt;
            if (_acc < TickEvery) return;
            _acc = 0f;
            var w = _m;
            if (w == null || !ReferenceEquals(w.Target, m))
            {
                for (int i = 0; i < MaxTeams; i++) { _ref[i] = null; _live[i] = false; _ever[i] = false; }
                _m = new WeakReference(m);
            }
            var c = Settings.Current;
            bool want = c != null && c.OutshotRushWithShields && m.IsFieldBattle && !m.IsNavalBattle && !m.MissionEnded && m.IsDeploymentFinished;
            if (!want)
            {
                _on = false;
                for (int i = 0; i < MaxTeams; i++) _live[i] = false;
                return;
            }
            var teams = m.Teams;
            int n = teams != null ? teams.Count : 0;
            for (int i = 0; i < n; i++)
            {
                Team t = null;
                try { t = teams[i]; } catch { t = null; }
                if (!MissionTeams.Valid(t)) continue;
                int ti = t.TeamIndex;
                if (ti < 0 || ti >= MaxTeams) continue;
                try
                {
                    if (!ReferenceEquals(_ref[ti], t)) { _ref[ti] = t; _live[ti] = false; _ever[ti] = false; }
                    byte why = 0;
                    bool live = (t.Side == BattleSideEnum.Attacker || t.Side == BattleSideEnum.Defender) && Doctrine.OutshotAdvancing(t, out why);
                    if (live && !_ever[ti])
                    {
                        _ever[ti] = true;
                        _teams++;
                        if (why == 1) _byFire++; else if (why == 2) _byS2++; else _byAtk++;
                    }
                    _live[ti] = live;
                }
                catch (Exception e) { _stum++; _live[ti] = false; RoutHold.Stumble(RoutHold.SiteDoctrine, "OutshotRush.Druzyna", e); }
            }
            _on = true;
        }

        /// <summary>Druzyna idzie teraz pod ostrzalem (stan z Ticku; glowny watek i tick druzyn). Klucz NIE albo poza bitwa polowa - false.</summary>
        internal static bool Live(Team t)
        {
            if (!_on || t == null) return false;
            int ti = t.TeamIndex;
            return ti >= 0 && ti < MaxTeams && _live[ti] && ReferenceEquals(_ref[ti], t);
        }

        // ------------------------------------------------------------ bieg (MarchToContact.MarchCap, glowny watek)

        private static int Key(Formation f) { var t = f.Team; return (t != null ? t.TeamIndex : 15) * 16 + ((int)f.FormationIndex & 15); }

        /// <summary>MarchToContact: szyk z tarczami w biegu 313b w tym takcie (wejscie = pierwszy takt biegu); szyko-sekundy wedlug faktycznego szyku (recenzja 4).</summary>
        internal static void NoteRush(Formation f, bool entered, float sec)
        {
            if (entered) { _rushIn++; if (f != null) _rushForms.Add(Key(f)); }
            _rushSec += sec;
            if (f == null) return;
            var ae = f.ArrangementOrder.OrderEnum;
            if (ae == ArrangementOrder.ArrangementOrderEnum.Loose) _secLoose += sec;
            else if (ae == ArrangementOrder.ArrangementOrderEnum.ShieldWall) _secWall += sec;
            else _secOther += sec;
        }

        /// <summary>MarchToContact: szarza (bez capu, blizej niz prog biegu) szyku piechoty AI druzyny Live pod ostrzalem - takt (recenzja 313b (4)).</summary>
        internal static void NoteCharge(float sec) { _chargeSec += sec; }

        /// <summary>MarchToContact: droga srodka szyku miedzy kolejnymi taktami biegu 313b (charge false) albo szarzy pod ostrzalem (charge true).</summary>
        internal static void NoteMove(bool charge, float dist, float dt)
        {
            if (!(dt > 0f) || float.IsNaN(dist) || float.IsInfinity(dist)) return;
            if (charge) { _chgDist += dist; _chgTime += dt; } else { _runDist += dist; _runTime += dt; }
        }

        /// <summary>MarchToContact: bieg 313b skonczony (zwarcie, szarza, rozkaz, koniec ostrzalu).</summary>
        internal static void NoteRushEnd() { _rushOut++; }

        /// <summary>MarchToContact: bieg 313b trwa dalej (zdjecie na poczatku Eval nie bylo koncem).</summary>
        internal static void NoteRushEndUndo() { _rushOut--; }

        // ------------------------------------------------------------ szyk (CloseRanks pass, glowny watek)

        /// <summary>CloseRanks: szyk AI piechoty pod ostrzalem przed zwarciem w Advance / Charge. Zwraca true = Loose (bez muru tarcz). wall = 146 wybralby ShieldWall.</summary>
        internal static bool WantLoose(Mission m, Formation f, bool wall)
        {
            if (f == null || !Live(f.Team)) return false;
            if (ShockCavNear(m, f)) { _horseForms.Add(Key(f)); return false; }
            _looseN++;
            if (wall) _wallWouldN++;
            _looseForms.Add(Key(f));
            return true;
        }

        /// <summary>Jak ShieldsUp.CavNear (90 m, K11), ale bez konnych strzelcow: szarza grozi tylko od konnicy uderzeniowej.</summary>
        private static bool ShockCavNear(Mission m, Formation f)
        {
            if (m == null || f == null || f.Team == null) return false;
            var my = f.CachedAveragePosition;
            float r2 = ShieldsUp.CavalryNear * ShieldsUp.CavalryNear;
            var teams = m.Teams;
            for (int i = 0; i < teams.Count; i++)
            {
                var t = teams[i];
                if (t == null || !t.IsEnemyOf(f.Team)) continue;
                var fl = t.FormationsIncludingEmpty;
                for (int j = 0; j < fl.Count; j++)
                {
                    var ef = fl[j];
                    if (ef == null || ef.CountOfUnits == 0) continue;
                    var q = ef.QuerySystem;
                    if (q == null || !q.IsCavalryFormation || MountedShooter(ef)) continue;
                    if (ef.CachedAveragePosition.DistanceSquared(my) < r2) return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------ cel (postfiks, tick druzyn)

        /// <summary>ShockCavNear: konny strzelec - szyk konnych lucznikow (IsRangedCavalryFormation) albo szyk z graczem na koniu z bronia dystansowa w reku,
        /// ale tylko gdy gracz jest w nim sam albo szyk nie ma rodzaju (recenzja 313b (2): gracz z lukiem w szyku kopijnikow - to dalej konnica uderzeniowa, K11).</summary>
        private static bool MountedShooter(Formation ef)
        {
            var q = ef.QuerySystem;
            if (q == null) return false;
            if (q.IsRangedCavalryFormation) return true;
            var a = Agent.Main;
            if (a == null || !a.IsActive() || a.MountAgent == null || !ReferenceEquals(a.Formation, ef)) return false;
            if (ef.CountOfUnits > 1 && (q.IsCavalryFormation || q.IsInfantryFormation || q.IsRangedFormation)) return false;
            var wi = a.GetPrimaryWieldedItemIndex();
            if (wi == EquipmentIndex.None) return false;
            var u = a.Equipment[wi].CurrentUsageItem;
            return u != null && u.IsRangedWeapon;
        }

        /// <summary>Cel do przekierowania: szyk konnych lucznikow AI - bez gracza (recenzja 313b WYSOKA 1: Jeff "to ta na mnie biegnie").</summary>
        private static bool AiHorseArchers(Formation ef)
        {
            var q = ef.QuerySystem;
            if (q == null || !q.IsRangedCavalryFormation) return false;
            var a = Agent.Main;
            return a == null || !ReferenceEquals(a.Formation, ef);
        }

        /// <summary>Po Formation.CacheClosestEnemyFormation (tick druzyn): szyk piechoty AI druzyny pod ostrzalem - szyk konnych lucznikow AI jako najblizszy wrog
        /// zamieniony na najblizszy pieszy szyk wroga (ta sama miara co gra). Dystans gry (CachedClosestEnemyFormationDistanceSquared) zostaje - recenzja 313b (4).
        /// Bez alokacji.</summary>
        public static void ClosestPost(Formation __instance)
        {
            if (!_on || __instance == null) return;
            try
            {
                var f = __instance;
                var fc = _fClosest;
                if (fc == null || !f.IsAIControlled || f.CountOfUnits <= 0) return;
                var team = f.Team;
                if (!Live(team)) return;
                var fq = f.QuerySystem;
                if (fq == null || !fq.IsInfantryFormation) return;
                var cur = fc(f);
                if (cur == null || !AiHorseArchers(cur)) return;
                var m = Mission.Current;
                if (m == null) return;
                var me = new Vec3(f.CachedAveragePosition, f.CachedMedianPosition.GetNavMeshZ());
                Formation best = null;
                float bd = float.MaxValue;
                var teams = m.Teams;
                for (int i = 0; i < teams.Count; i++)
                {
                    var t = teams[i];
                    if (t == null || !t.IsEnemyOf(team)) continue;
                    var fl = t.FormationsIncludingSpecialAndEmpty;
                    for (int j = 0; j < fl.Count; j++)
                    {
                        var ef = fl[j];
                        if (ef == null || ef.CountOfUnits <= 0 || ReferenceEquals(ef, cur)) continue;
                        var eq = ef.QuerySystem;
                        if (eq == null || eq.IsRangedCavalryFormation || !(eq.IsInfantryFormation || eq.IsRangedFormation)) continue;
                        float d = ef.CachedMedianPosition.GetNavMeshVec3().DistanceSquared(me);
                        if (d < bd) { bd = d; best = ef; }
                    }
                }
                if (best == null) { Interlocked.Increment(ref _redirNoFoot); return; }
                fc(f) = best;
                Interlocked.Increment(ref _redir);
                var ai = f.AI;
                if (ai != null && ai.ActiveBehavior is BehaviorCharge) Interlocked.Increment(ref _redirCharge);   // recenzja 313b (5): tylko tu cel na pewno dziala
            }
            catch { Interlocked.Increment(ref _postErr); }
        }

        // ------------------------------------------------------------ linia

        /// <summary>Segment E linii 'Bitwa (291)' (Battle291.Write - HorsePursuit.End, po Doctrine.End i End; liczniki do Reset).</summary>
        private static string Mps(double dist, double time) { return time >= 1.0 ? (dist / time).ToString("0.0", Inv) : "-"; }

        internal static string Segment()
        {
            var c = Settings.Current;
            var sb = new StringBuilder(600);
            sb.Append("E 313b obronca atakuje przy przegranym ostrzale ").Append(c != null && c.DefendersChargeWhenOutshot ? "TAK" : "NIE").Append(": ")
              .Append(Doctrine.FireSegment())
              .Append("; 283 bez postoju: bilans ognia ").Append(HorsePursuit.NoHaltFire).Append(", bieg pod ostrzalem ").Append(HorsePursuit.NoHaltRush)
              .Append(", postoj puszczony ostrzalem ").Append(HorsePursuit.HaltFireRel)
              .Append("; bieg z tarczami pod ostrzalem ").Append(c != null && c.OutshotRushWithShields ? "TAK" : "NIE")
              .Append(": druzyn ").Append(_teams).Append(" (291e ").Append(_byFire).Append(", S2 ").Append(_byS2).Append(", taktyka ataku ").Append(_byAtk)
              .Append("), szykow w biegu ").Append(_rushForms.Count).Append(", wejsc w bieg ").Append(_rushIn).Append(", koncow biegu ").Append(_rushOut)
              .Append(", szyko-sekund biegu ").Append(_rushSec.ToString("0", Inv)).Append(" (Loose ").Append(_secLoose.ToString("0", Inv))
              .Append(", ShieldWall ").Append(_secWall.ToString("0", Inv)).Append(", inny ").Append(_secOther.ToString("0", Inv)).Append("), sr. predkosc w biegu ")
              .Append(Mps(_runDist, _runTime)).Append(" m/s; szarza pod ostrzalem bez capu: szyko-sekund ").Append(_chargeSec.ToString("0", Inv))
              .Append(", sr. predkosc ").Append(Mps(_chgDist, _chgTime)).Append(" m/s")
              .Append("; Loose zamiast muru tarcz: szykow ").Append(_looseForms.Count).Append(" (decyzji ").Append(_looseN).Append(", w tym zamiast ShieldWall ")
              .Append(_wallWouldN).Append("), bez Loose przy konnicy w 90 m ").Append(_horseForms.Count)
              .Append("; cel pieszy zamiast konnych lucznikow AI ").Append(Volatile.Read(ref _redir)).Append(" (w BehaviorCharge ").Append(Volatile.Read(ref _redirCharge))
              .Append(", bez pieszego szyku wroga ")
              .Append(Volatile.Read(ref _redirNoFoot)).Append("), latka celu ").Append(_patch)
              .Append("; potkniecia ").Append(_stum + Volatile.Read(ref _postErr));
            return sb.ToString();
        }
    }
}
