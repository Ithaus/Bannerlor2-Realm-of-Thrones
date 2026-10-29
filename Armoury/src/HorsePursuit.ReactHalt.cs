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
    /// Poprawka 132b (autotest 132b A: "stoi 0 (marsz TAK)" - nie stanely: strona lamana 10, zwarcie 2; jedyny R4 zwolniony po 2 s "wrog w 110 m"): (1) bramka
    /// strony byla odwrocona (Broke(nekajacy, ofiara) - u ofiary 3 rannych w ucieczce po dlugich lukach z 260 m) - teraz bez postoju tylko strona ofiary
    /// w poscigu; (2) 110 m od nietknietego PIESZEGO szyku wroga (FI.FootGap, HFormsGaps) - jazda przy szyku i 2-osobowy szyk nekajacego nie sa "zwarciem";
    /// (3) R5 takze po 3 szkodach od nekajacego w calej bitwie; (4) R4 - szyk ofiary frontem do nekajacego (FacingOrderLookAtDirection), zwrot wraca przy
    /// zwolnieniu. Nekajacy testu dojezdza przed zwarciem armii (MarchToContact.PostSpeed - bez limitu tempa szyku).
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
            // poprawka 132b: R4 - szyk ofiary staje frontem do nekajacego (FacingOrder gry), poprzedni zwrot wraca przy zwolnieniu
            public bool Faced;
            public FacingOrder Facing0;
            public Vec2 FaceDir;
        }

        private const int LEnd = 0, LTime = 1, LNear = 2, LCharge = 3, LPlayer = 4, LTactic = 5, LRetreat = 6, LS4 = 7, LKey = 8, LBattle = 9, LKinds = 10;
        private static readonly int[] LRel = new int[LKinds];
        private static readonly Dictionary<Formation, Halt> Halts = new Dictionary<Formation, Halt>();
        private static readonly List<Halt> HaltList = new List<Halt>(), HaltTmp = new List<Halt>();
        private static readonly HashSet<Formation> LR4F = new HashSet<Formation>(), LR5F = new HashSet<Formation>();
        // recenzja 283: postoj raz na nekajacego (czlowieka, nie obiekt zagrozenia - nowe zagrozenie po ciszy nie zatrzymuje znowu); R4 raz na pare nekajacy-szyk;
        // liczniki "nie stanely" raz na szyk i przyczyne (warunek sprawdzany co pass)
        private static readonly HashSet<int> LR5Who = new HashSet<int>();
        private static readonly Dictionary<Formation, HashSet<int>> LR4Who = new Dictionary<Formation, HashSet<int>>();
        private static readonly HashSet<long> LNoSeen = new HashSet<long>();
        private static int _lNoFront, _lNoFootNear;
        private static AccessTools.FieldRef<FormationAI, List<BehaviorComponent>> _rBeh;
        private static bool _rBehTried;
        private static int _lR4, _lR5, _lR5F, _lRetake, _lLeft, _lNoEng, _lNoS2, _lNoBroke, _lNoMove;
        private static float _lSec;
        // poprawka 132b: zwroty frontem do nekajacego (R4), postoje R5 z sumy szkod (3 szkody, nie tylko 3 w 5 s)
        private static int _lFace, _lR5Sum;
        private const float FaceTurnCos = 0.866f;   // nowy zwrot dopiero przy zmianie kierunku o 30 stopni (bez przestawiania szyku co pass)
        // 291 (ArmyMarchesPastHarassers): nekajacy, ktorym armia nie stanela (R5 pominiete), szyki piechoty / jazdy, ktore poszly dalej (raz na szyk) i ich ludzie,
        // postoje strzelcow (R4 dalej), ludzie R2 ze smycza od miejsca wyslania i ich powroty ze smyczy
        private static readonly HashSet<int> H291Who = new HashSet<int>();
        private static int _l291NoArmy, _l291Go, _l291GoMen, _l291Arch, _l291Anchor, _l291LeashBack;

        private static void HaltClear()
        {
            Halts.Clear(); HaltList.Clear(); HaltTmp.Clear(); LR4F.Clear(); LR5F.Clear();
            LR5Who.Clear(); LR4Who.Clear(); LNoSeen.Clear();
            _lNoFront = _lNoFootNear = 0;
            Array.Clear(LRel, 0, LRel.Length);
            _lR4 = _lR5 = _lR5F = _lRetake = _lLeft = _lNoEng = _lNoS2 = _lNoBroke = _lNoMove = 0;
            _lSec = 0f;
            _lFace = _lR5Sum = 0;
            H291Who.Clear();
            _l291NoArmy = _l291Go = _l291GoMen = _l291Arch = _l291Anchor = _l291LeashBack = 0;
        }

        /// <summary>291: segment A linii 'Bitwa (291)' (HorsePursuit.End, przed Clear).</summary>
        internal static string Seg291A()
        {
            return "A armia idzie dalej " + (_rMarchOn ? "TAK" : "NIE") + ": armia nie stanela " + _l291NoArmy + " (R5 " + _lR5 + "), szykow poszlo dalej " + _l291Go
                   + " (ludzi " + _l291GoMen + "), strzelcy staja " + _l291Arch + " (R4 " + _lR4 + "); oddzial od miejsca wyslania " + _l291Anchor
                   + " (powrotow ze smyczy " + _l291LeashBack + ")";
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

        /// <summary>Szyk moze stanac: AI, nie gracza, w marszu, pieszy szyk wroga dalej niz 110 m, bez odwrotu i szarzy, bez S4.
        /// Poprawka 132b: 110 m od nietknietego PIESZEGO szyku wroga (FootGap) - autotest 132b A: konnica sojusznika gracza przy lucznikach wroga i najblizszy
        /// szyk z gry (takze 2-osobowy) dawaly "zwarcie 2" i zwolnienie "wrog w 110 m" po 2 s; jazda wokol szyku to powod postoju, nie przeszkoda.</summary>
        private static bool CanHalt(FI fi)
        {
            if (fi.Player || fi.Retreat || fi.F == null || fi.F.CountOfUnits <= 0 || fi.F.AI == null) return false;
            if (Halts.ContainsKey(fi.F)) return false;
            if (fi.FootGap <= HuntClose) { if (LNo(fi.F, 1)) _lNoEng++; return false; }
            if (Charging(fi.F) || Doctrine.S4Held(fi.F)) return false;
            if (!Moving(fi.F)) { if (LNo(fi.F, 2)) _lNoMove++; return false; }
            return true;
        }

        private static void Mark4(Formation f, int who)
        {
            HashSet<int> r4;
            if (!LR4Who.TryGetValue(f, out r4)) { r4 = new HashSet<int>(); LR4Who[f] = r4; }
            r4.Add(who);
        }

        private static bool LNo(Formation f, int why) { return LNoSeen.Add(((long)f.GetHashCode() << 4) | (long)why); }

        /// <summary>Recenzja 283 (4): nekajacy z tylu albo z boku szyku - nie przed czolem. Przed czolem = dalej przed przednim szeregiem (pol glebokosci szyku
        /// w kierunku Formation.Direction) niz w bok poza skrzydlem (pol szerokosci) - cwiartka czola; reszta to bok i tyl.</summary>
        private static bool RearOrFlank(FI fi, Vec2 sp)
        {
            Vec2 dir;
            float w, dp;
            try { dir = fi.F.Direction; w = fi.F.Width; dp = fi.F.Depth; } catch { return false; }
            if (!dir.IsValid || dir.LengthSquared < 1E-04f) return false;
            dir = dir.Normalized();
            Vec2 o = sp - fi.C;
            float fwd = Vec2.DotProduct(o, dir) - 0.5f * dp;
            float lat = Math.Abs(Vec2.DotProduct(o, new Vec2(-dir.y, dir.x))) - 0.5f * w;
            return !(fwd > 0f && fwd >= lat);
        }

        /// <summary>ThreatPass (283): R4 szyk ofiary, R5 cala armia. Zwraca liczbe nowo zatrzymanych szykow.</summary>
        private static int HaltAsk(Threat t, Vec2 sp, float now, int down)
        {
            if (!_rHaltKey || t.Group || t.S == null) return 0;
            if (t.Hits + t.MeleeHits <= 0 && down <= 0) return 0;   // tylko po szkodzie (pudlo z daleka nie zatrzymuje wojska)
            if (ReferenceEquals(t.S, _tA)) TestHarm(t, now);   // poprawka 132b: stan wroga w chwili pierwszej szkody nekajacego testu (marsz, wolna piechota)
            var fl = HForms[t.Side];
            Formation vf = t.VicF;
            if (vf == null) return 0;
            var team = vf.Team;
            if (team == null) return 0;
            // poprawka 132b: bramka byla odwrocona - Broke(t.SSide, t.Side) = "strona nekajacego lamie strone ofiary" (u ofiary 3 rannych w ucieczce 143 przy
            // wyzszym odsetku niz u nekajacego - w autotescie 132b po kilku strzalach dlugich lukow z 260 m, 10 + 25 odmow); szyk strony, ktora sie chwieje, staje
            // i zwiera szereg - to wlasnie odpowiedz. Bez postoju tylko strona ofiary w poscigu (lamie strone nekajacego - szyki w szarzy 148, CanHalt i tak).
            if (Broke(t.Side, t.SSide)) { _lNoBroke++; return 0; }
            if (Doctrine.S2On(team)) { _lNoS2++; return 0; }
            // recenzja 283 (4): pieszy przy swoim nietknietym szyku (gracz w swojej linii) - zwykla bitwa; nekajacy przed czolem szyku ofiary - szyk idzie dalej
            // (pod pociskami z przodu sie zwiera, nie staje - S2/141)
            if (!t.Lone && t.S.MountAgent == null) { if (LNo(vf, 3)) _lNoFootNear++; return 0; }
            int vi = -1;
            for (int i = 0; i < fl.Count; i++) if (ReferenceEquals(fl[i].F, vf)) { vi = i; break; }
            if (vi < 0) return 0;
            if (!RearOrFlank(fl[vi], sp)) { if (LNo(vf, 4)) _lNoFront++; return 0; }
            int n = 0;
            int who = t.S.Index;
            // poprawka 132b: armia staje takze po 3 szkodach od tego nekajacego w calej bitwie (strzelec konny strzela co 2-3 s - "3 w 5 s" prawie nie wypada)
            bool sum = t.Hits + t.MeleeHits >= _shots3;
            bool army = !LR5Who.Contains(who) && (t.Lone || t.Leader) && (down > 0 || t.Shots.Count >= _shots3 || sum);
            // 291 (Jeff 29.10: "armia sie zatrzymala aby zobaczyc z kim walcze" - log 00:47 i 01:45 R5 'armia staje', postoj 112-120 s): samotny nekajacy nie zatrzymuje
            // calej armii - odpowiada oddzial (R2 / R3) i strzelcy (R1, R4 nizej), reszta idzie swoim planem
            if (army && _rMarchOn) { army = false; if (H291Who.Add(who)) _l291NoArmy++; }
            if (army)
            {
                // glowna piechota druzyny (najwiekszy szyk piechoty) dalej niz 110 m od pieszego szyku wroga (poprawka 132b: FootGap)
                int best = -1, bn = 0;
                for (int i = 0; i < fl.Count; i++)
                {
                    var fi = fl[i];
                    if (!ReferenceEquals(fi.F.Team, team) || !fi.Foot || fi.Rng || fi.N <= bn) continue;
                    bn = fi.N;
                    best = i;
                }
                if (best >= 0 && fl[best].FootGap <= HuntClose) { army = false; if (LNo(fl[best].F, 1)) _lNoEng++; }
            }
            if (army)
            {
                int k = 0;
                for (int i = 0; i < fl.Count; i++)
                {
                    var fi = fl[i];
                    if (!ReferenceEquals(fi.F.Team, team) || fi.Ha) continue;
                    if (!CanHalt(fi)) continue;
                    if (!TakeHalt(fi.F, t, true, now)) continue;
                    LR5F.Add(fi.F);
                    Mark4(fi.F, who);   // szyk stal w R5 - R4 tego nekajacego juz go nie zatrzyma drugi raz
                    t.Halt5++;
                    k++;
                }
                if (k > 0)
                {
                    t.H5Used = true;   // recenzja 283 (M1): zuzyte tylko, gdy cos stanelo
                    LR5Who.Add(who);
                    if (down <= 0 && t.Shots.Count < _shots3) _lR5Sum++;
                    _lR5++; _lR5F = LR5F.Count; if (ReferenceEquals(t.S, _tA)) _tHalt += k;
                    return k;
                }
                // nic nie stanelo (bez marszu) - R4 szyku ofiary ponizej
            }
            HashSet<int> r4;
            if (LR4Who.TryGetValue(vf, out r4) && r4.Contains(who)) return 0;
            var fv = fl[vi];
            if (fv.Ha) return 0;
            // 291: staje tylko szyk strzelcow pieszych (staje i strzela - R1); trafiona piechota albo jazda idzie dalej z armia (log 00:47: szyk piechoty stal 2 x 60 s
            // frontem do gracza, 'frontem do nekajacego 13', a lucznicy gracza strzelali mu w czolo - 'pod ostrzalem bez zwarcia do konca: padli od pociskow 19')
            if (_rMarchOn && !fv.Rng) { if (LNo(fv.F, 5)) { _l291Go++; _l291GoMen += fv.N; } return 0; }
            if (!CanHalt(fv) || !TakeHalt(fv.F, t, false, now)) return 0;
            Halt h4;
            if (Halts.TryGetValue(fv.F, out h4)) Face(h4, sp);   // poprawka 132b: szyk ofiary frontem do nekajacego
            t.H4Used = true;   // recenzja 283 (M1): zuzyte dopiero po przejeciu
            Mark4(vf, who);
            LR4F.Add(fv.F);
            t.Halt4++;
            _lR4++;
            if (_rMarchOn) _l291Arch++;
            if (ReferenceEquals(t.S, _tA)) _tHalt++;
            n++;
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

        /// <summary>Poprawka 132b: R4 - szyk ofiary frontem do nekajacego (FacingOrderLookAtDirection gry; BehaviorStop sam zwrotu nie ustawia - szyk tarcz
        /// stal plecami do konnego strzelca). Zwrot z chwili przejecia zapamietany, nowy dopiero przy zmianie kierunku o 30 stopni.</summary>
        private static void Face(Halt h, Vec2 sp)
        {
            if (h == null || h.Army || h.F == null) return;
            Vec2 c;
            try { var mp = h.F.CachedMedianPosition; c = mp.IsValid ? mp.AsVec2 : h.F.CachedAveragePosition; } catch { return; }
            if (!c.IsValid || !sp.IsValid) return;
            Vec2 d = sp - c;
            if (d.LengthSquared < 1f) return;
            d = d.Normalized();
            if (h.Faced && Vec2.DotProduct(d, h.FaceDir) >= FaceTurnCos) return;
            if (!h.Faced) { h.Facing0 = h.F.FacingOrder; h.Faced = true; }
            h.FaceDir = d;
            h.F.SetFacingOrder(FacingOrder.FacingOrderLookAtDirection(d));
            _lFace++;
        }

        /// <summary>Odleglosc szyku od nietknietego pieszego szyku wroga z tego passu (HForms); szyku nie ma w HForms - EnemyGap gry.</summary>
        private static float FootGapOf(Formation f)
        {
            for (int s = 0; s < 2; s++)
            {
                var fl = HForms[s];
                for (int i = 0; i < fl.Count; i++) if (ReferenceEquals(fl[i].F, f)) return fl[i].FootGap;
            }
            return MarchToContact.EnemyGap(f);
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
                // poprawka 132b: zwrot sprzed postoju (zachowania gry ustawiaja potem swoj)
                if (h.Faced && h.F != null && h.F.CountOfUnits > 0) h.F.SetFacingOrder(h.Facing0);
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
                    // poprawka 132b: R4 - front za nekajacym, poki zyje
                    if (!h.Army && h.T != null && h.T.S != null && h.T.S.IsActive()) Face(h, h.T.S.Position.AsVec2);
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
            if (FootGapOf(f) <= HuntClose) return LNear;   // poprawka 132b: pieszy szyk wroga (jak CanHalt)
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
