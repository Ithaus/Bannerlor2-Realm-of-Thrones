using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 283 R4 / R5 - postoj szyku i armii pod nekaniem (klucz ArmyHaltsForHarassers). Wojsko nie zostawia rozciagnietego tylu: staje, zwiera szyk, a straz tylna
    /// (R1-R3) robi swoje. Droga gry: BehaviorStop (TickOccasionally - rozkaz Stop; przy wlaczeniu szyk tarcz, gdy ludzie maja tarcze, i ogien wedlug uznania;
    /// waga 0.01) - przejecie wag zachowan FormationAI (to samo pole _behaviors i ten sam wzor co Doctrine S4: kopia wag wedlug obiektu zachowania, wszystkie
    /// na 0, BehaviorStop 1; brak BehaviorStop na liscie - dodany raz przez AddAiBehavior, po zwolnieniu z waga 0). FindBestBehavior gry wybiera wtedy postoj
    /// w najblizszym ticku szyku (0.5 s).
    /// R4: szyk ofiary w marszu (rozkaz Advance / FallBack / Move daleko od punktu - UnitDiameter x 5 jak gra, albo marsz MarchToContact), szkoda (trafienie, cios
    /// albo padly). R5: samotny nekajacy albo wodz (R6), padlo od niego albo 3 szkody w 5 s - wszystkie szyki AI tej druzyny w marszu oprocz konnych lucznikow
    /// (harcuja wolno). Oba: raz na nekajacego, wrog dalej niz 110 m (HuntClose; glowna piechota druzyny dla R5), strona nie lamana (Broke 148), bez S2 (141) i S4.
    /// Zwolnienie: nekajacy skonczony + ShieldsUpHoldSeconds, 60 s (MaxSeconds 148), wrog w 110 m, rozkaz szarzy, gracz przejal, odwrot, S4, klucz, koniec bitwy.
    /// Taktyka przestawila wagi w czasie postoju - przejecie od nowa (licznik "przejete znowu", kopia nowych wag), do limitu 60 s.
    /// Szyk gracza - nigdy. Glowny watek (pass co 1 s; tick druzyn nie biegnie rownolegle - jak Doctrine).
    /// </summary>
    internal static partial class HorsePursuit
    {
        private sealed class Halt
        {
            public Formation F;
            public BehaviorComponent[] Who;
            public float[] W;
            public BehaviorStop Stop;
            public float StopW, Start;
            public Threat T;
            public bool Army;
        }

        private const int LEnd = 0, LTime = 1, LNear = 2, LCharge = 3, LPlayer = 4, LTactic = 5, LRetreat = 6, LS4 = 7, LKey = 8, LBattle = 9, LKinds = 10;
        private static readonly int[] LRel = new int[LKinds];
        private static readonly Dictionary<Formation, Halt> Halts = new Dictionary<Formation, Halt>();
        private static readonly List<Halt> HaltList = new List<Halt>(), HaltTmp = new List<Halt>();
        private static readonly HashSet<Formation> LR4F = new HashSet<Formation>(), LR5F = new HashSet<Formation>();
        private static AccessTools.FieldRef<FormationAI, List<BehaviorComponent>> _rBeh;
        private static bool _rBehTried;
        private static int _lR4, _lR5, _lR5F, _lRetake, _lLeft, _lNoEng, _lNoS2, _lNoBroke, _lNoMove;
        private static float _lSec;

        private static void HaltClear()
        {
            Halts.Clear(); HaltList.Clear(); HaltTmp.Clear(); LR4F.Clear(); LR5F.Clear();
            Array.Clear(LRel, 0, LRel.Length);
            _lR4 = _lR5 = _lR5F = _lRetake = _lLeft = _lNoEng = _lNoS2 = _lNoBroke = _lNoMove = 0;
            _lSec = 0f;
        }

        private static List<BehaviorComponent> Beh(Formation f)
        {
            if (!_rBehTried)
            {
                _rBehTried = true;
                try { _rBeh = AccessTools.FieldRefAccess<FormationAI, List<BehaviorComponent>>("_behaviors"); } catch { _rBeh = null; }
            }
            var ai = f.AI;
            return _rBeh != null && ai != null ? _rBeh(ai) : null;
        }

        private static int HaltsOf(Threat t) { return t.Halt4 + t.Halt5; }

        /// <summary>Szyk idzie: marsz MarchToContact, rozkaz Advance / FallBack, albo Move z punktem dalej niz UnitDiameter x 5 (prog "na miejscu" gry).</summary>
        private static bool Moving(Formation f)
        {
            var oe = f.GetReadonlyMovementOrderReference().OrderEnum;
            if (oe == MovementOrder.MovementOrderEnum.Charge || oe == MovementOrder.MovementOrderEnum.ChargeToTarget || oe == MovementOrder.MovementOrderEnum.Retreat
                || oe == MovementOrder.MovementOrderEnum.Stop) return false;
            if (MarchToContact.IsMarching(f)) return true;
            if (oe == MovementOrder.MovementOrderEnum.Advance || oe == MovementOrder.MovementOrderEnum.FallBack) return true;
            if (oe != MovementOrder.MovementOrderEnum.Move) return false;
            Vec2 op = f.OrderPosition;
            var mp = f.CachedMedianPosition;
            if (!op.IsValid || !mp.IsValid) return false;
            float ud = f.UnitDiameter;
            return mp.AsVec2.DistanceSquared(op) > ud * ud * 25f;
        }

        private static bool Charging(Formation f)
        {
            var oe = f.GetReadonlyMovementOrderReference().OrderEnum;
            return oe == MovementOrder.MovementOrderEnum.Charge || oe == MovementOrder.MovementOrderEnum.ChargeToTarget;
        }

        /// <summary>Szyk moze stanac: AI, nie gracza, w marszu, wrog dalej niz 110 m, bez odwrotu i szarzy, bez S4.</summary>
        private static bool CanHalt(FI fi)
        {
            if (fi.Player || fi.Retreat || fi.F == null || fi.F.CountOfUnits <= 0 || fi.F.AI == null) return false;
            if (Halts.ContainsKey(fi.F)) return false;
            if (fi.Gap <= HuntClose) { _lNoEng++; return false; }
            if (Charging(fi.F) || Doctrine.S4Held(fi.F)) return false;
            if (!Moving(fi.F)) { _lNoMove++; return false; }
            return true;
        }

        /// <summary>ThreatPass (283): R4 szyk ofiary, R5 cala armia. Zwraca liczbe nowo zatrzymanych szykow.</summary>
        private static int HaltAsk(Threat t, Vec2 sp, float now, int down)
        {
            if (!_rHaltKey || t.Group || t.S == null) return 0;
            if (t.Hits + t.MeleeHits <= 0 && down <= 0) return 0;   // tylko po szkodzie (pudlo z daleka nie zatrzymuje wojska)
            var fl = HForms[t.Side];
            Formation vf = t.VicF;
            if (vf == null) return 0;
            var team = vf.Team;
            if (team == null) return 0;
            if (Broke(t.SSide, t.Side)) { _lNoBroke++; return 0; }
            if (Doctrine.S2On(team)) { _lNoS2++; return 0; }
            int n = 0;
            bool army = !t.H5Used && (t.Lone || t.Leader) && (down > 0 || t.Shots.Count >= _shots3);
            if (army)
            {
                // glowna piechota druzyny (najwiekszy szyk piechoty) dalej niz 110 m od wroga
                int best = -1, bn = 0;
                for (int i = 0; i < fl.Count; i++)
                {
                    var fi = fl[i];
                    if (!ReferenceEquals(fi.F.Team, team) || !fi.Foot || fi.Rng || fi.N <= bn) continue;
                    bn = fi.N;
                    best = i;
                }
                if (best >= 0 && fl[best].Gap <= HuntClose) { army = false; _lNoEng++; }
            }
            if (army)
            {
                t.H5Used = true;
                int k = 0;
                for (int i = 0; i < fl.Count; i++)
                {
                    var fi = fl[i];
                    if (!ReferenceEquals(fi.F.Team, team) || fi.Ha) continue;
                    if (!CanHalt(fi)) continue;
                    if (!TakeHalt(fi.F, t, true, now)) continue;
                    LR5F.Add(fi.F);
                    t.Halt5++;
                    k++;
                }
                if (k > 0) { _lR5++; _lR5F = LR5F.Count; if (ReferenceEquals(t.S, _tA)) _tHalt += k; }
                n += k;
                return n;
            }
            if (t.H4Used) return 0;
            for (int i = 0; i < fl.Count; i++)
            {
                var fi = fl[i];
                if (!ReferenceEquals(fi.F, vf)) continue;
                if (fi.Ha) break;
                t.H4Used = true;
                if (!CanHalt(fi) || !TakeHalt(fi.F, t, false, now)) break;
                LR4F.Add(fi.F);
                t.Halt4++;
                _lR4++;
                if (ReferenceEquals(t.S, _tA)) _tHalt++;
                n++;
                break;
            }
            return n;
        }

        /// <summary>Przejecie wag: kopia wedlug obiektu, wszystkie 0, BehaviorStop 1 (dodany, gdy brak).</summary>
        private static bool TakeHalt(Formation f, Threat t, bool army, float now)
        {
            var lst = Beh(f);
            if (lst == null) return false;
            BehaviorStop stop = null;
            for (int i = 0; i < lst.Count; i++) { stop = lst[i] as BehaviorStop; if (stop != null) break; }
            if (stop == null)
            {
                stop = new BehaviorStop(f);
                f.AI.AddAiBehavior(stop);
                lst = Beh(f);
                if (lst == null) return false;
            }
            var h = new Halt { F = f, Stop = stop, Start = now, T = t, Army = army, StopW = stop.WeightFactor };
            Snap(h, lst);
            Halts[f] = h;
            HaltList.Add(h);
            return true;
        }

        private static void Snap(Halt h, List<BehaviorComponent> lst)
        {
            var who = new BehaviorComponent[lst.Count];
            var w = new float[lst.Count];
            for (int i = 0; i < lst.Count; i++) { who[i] = lst[i]; w[i] = lst[i] != null ? lst[i].WeightFactor : 0f; }
            h.Who = who;
            h.W = w;   // waga BehaviorStop sprzed pierwszego przejecia zostaje w StopW (przy ponownym przejeciu kopia bez niej)
            for (int i = 0; i < lst.Count; i++)
            {
                var b = lst[i];
                if (b == null) continue;
                b.WeightFactor = ReferenceEquals(b, h.Stop) ? 1f : 0f;
            }
        }

        /// <summary>Wagi to dalej nasze (taktyka ich nie przestawila).</summary>
        private static bool OursHalt(Halt h, List<BehaviorComponent> lst)
        {
            for (int i = 0; i < lst.Count; i++)
            {
                var b = lst[i];
                if (b == null) continue;
                if (ReferenceEquals(b, h.Stop)) { if (Math.Abs(b.WeightFactor - 1f) > 0.001f) return false; }
                else if (b.WeightFactor > 1E-07f) return false;
            }
            return true;
        }

        /// <summary>Zwolnienie: zapisane wagi wracaja do tych samych obiektow (gdy nasze), BehaviorStop do swojej wagi.</summary>
        private static void Release(Halt h, int why)
        {
            Halts.Remove(h.F);
            HaltList.Remove(h);
            LRel[why]++;
            try
            {
                var lst = h.F != null ? Beh(h.F) : null;
                if (lst != null && OursHalt(h, lst))
                {
                    for (int k = 0; k < h.Who.Length; k++)
                    {
                        var b = h.Who[k];
                        if (b == null || ReferenceEquals(b, h.Stop)) continue;
                        for (int i = 0; i < lst.Count; i++)
                            if (ReferenceEquals(lst[i], b)) { b.WeightFactor = h.W[k]; break; }
                    }
                }
                if (h.Stop != null) h.Stop.WeightFactor = h.StopW;
            }
            catch (Exception e) { RStumble("HorsePursuit.NekaniePostojZwolnij", e); }
        }

        /// <summary>Pass (po ThreatsRun): warunki zwolnienia, ponowne przejecie po zmianie wag taktyki, sekundy postoju.</summary>
        private static void HaltPass(Mission m, float now)
        {
            if (HaltList.Count == 0) return;
            HaltTmp.Clear();
            HaltTmp.AddRange(HaltList);
            for (int i = 0; i < HaltTmp.Count; i++)
            {
                var h = HaltTmp[i];
                try
                {
                    int why = HaltWhy(m, h, now);
                    if (why >= 0) { Release(h, why); continue; }
                    _lSec += PassEvery;
                    var lst = Beh(h.F);
                    if (lst != null && !OursHalt(h, lst))
                    {
                        if (h.Stop != null && !lst.Contains(h.Stop)) { Release(h, LTactic); continue; }
                        Snap(h, lst);   // taktyka przestawila wagi (zmiana fazy) - od nowa, kopia nowych wag
                        _lRetake++;
                    }
                }
                catch (Exception e)
                {
                    RStumble("HorsePursuit.NekaniePostoj", e);
                    Halts.Remove(h.F);
                    HaltList.Remove(h);
                }
            }
            HaltTmp.Clear();
        }

        private static int HaltWhy(Mission m, Halt h, float now)
        {
            var f = h.F;
            if (f == null || f.CountOfUnits <= 0 || f.AI == null) return LEnd;
            if (m.MissionEnded) return LBattle;
            if (!_rHaltKey) return LKey;
            if (PlayerForm(f)) return LPlayer;
            if (FormRetreat(f)) return LRetreat;
            if (Charging(f)) return LCharge;
            if (Doctrine.S4Held(f)) return LS4;
            if (now - h.Start >= MaxSeconds) return LTime;
            if (MarchToContact.EnemyGap(f) <= HuntClose) return LNear;
            var t = h.T;
            bool live = t != null && t.S != null && Threats.TryGetValue(t.S, out var cur) && ReferenceEquals(cur, t);
            if (!live && (t == null || now - t.GoneAt >= _hold)) return LEnd;
            return -1;
        }

        /// <summary>End: wszystkie postoje zwolnione ("koniec bitwy"), potem liczba zatrzymanych (oczekiwane 0).</summary>
        private static void HaltEndAll()
        {
            HaltTmp.Clear();
            HaltTmp.AddRange(HaltList);
            for (int i = 0; i < HaltTmp.Count; i++)
            {
                try { Release(HaltTmp[i], LBattle); } catch { Halts.Remove(HaltTmp[i].F); HaltList.Remove(HaltTmp[i]); }
            }
            HaltTmp.Clear();
            _lLeft = HaltList.Count;
        }
    }
}
