using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 344 (A) "Konnica z glowa wobec konnego strzelca" (07.11, Jeff po bitwach 01:01 / 01:07 / 01:15 z WGRANIEM 171: "atakuje kawaleria i jada za mna; jak zblizamy
    /// sie do mojego wojska, to zawracaja, ja wtedy jade za nimi i wybijam ich w plecy strzalami; nikt z nich nie atakuje mnie i moge tak wybic niemal wszystkich").
    /// Klucz CavalryFaceShooterAtArmyEdge (przy kluczu 204 RidersRunDownShooters). Projekt: docs/PROJEKT-344-AI-BITWY.md.
    /// Przyczyna (log Jeffa 07.11, bitwa 1): 333 SAMO tworzylo (A) - zerwanie poscigu przy strefie armii strzelca oddawalo wszystkich jezdzcow naraz zachowaniu szyku
    /// (tylem do strzelca, klusem - cap marszu 128 liczony od szykow jego armii), pamiec przynety blokowala obrone (ponowny poscig dopiero 100 m od strefy po 60-240 s,
    /// tylna straz 3 ludzi od 40 m), trafienie jezdzca szyku bylo tylko pomiarem; 52 s pogoni za szybszym strzelcem bez kontaktu.
    ///
    /// STREFA ARMII STRZELCA (bez zmian z 333): kazdy nietkniety szyk strony strzelca (od IntactMin ludzi, nie w odwrocie), poza szykiem samego strzelca: piechota
    /// promien + 50 m, lucznicy piesi promien + 0.8 x zasieg (60..120 m), konnica promien + 60 m; g = odleglosc strzelca od krawedzi strefy (g < 0 = w strefie).
    /// POJECIA (decyzja na poziomie zagrozenia, pass 1 s; gracz i AI-strzelec na tych samych prawach - IsMainAgent tylko w liniach):
    ///  - G: jezdzcy 204 na strzelcu + szyki konnicy AI strony, ktorych ludzi strzelec trafil w ostatnich 30 s (bez nich - najblizszy szyk konnicy AI w smyczy);
    ///    d = odleglosc strzelca od najblizszego jezdzca / krawedzi szyku G; Rs 60 m (zasieg strzelca - ZShotMin), Rw 40 m (wypad), Rb 30 m (bliski wypad - HuntScreen),
    ///    oslona 20 m (strzelec dalej od swojej strefy jest "poza oslona");
    ///  - przewaga: ludzi G >= 2 x (1 + eskorta), eskorta = konni strony strzelca w Rw od niego (jak L2/L3 204);
    ///  - szybszy: najwieksza predkosc konia strzelca > 1.05 x mediana koni jezdzcow (GetMaximumForwardUnlimitedSpeed - ta sama miara co przeciecie 204);
    ///  - zbliza sie: d maleje >= 1 m/s przez 2 s albo strzelec trafil kogos z G w ostatnich 5 s z d <= Rs.
    /// STANY grupy (zastepuja WOLNY / POSCIG / ZERWANY / TYLNA STRAZ 333):
    ///  - W wolny: start poscigu P tylko gdy g >= 30 m, punkt za 3 s >= 10 m przed strefa, przewaga, strona nie przegrywa; strzelec szybszy i nie zbliza sie - tylko
    ///    oddzial L1 i tylko w Rw (szansa na cios); pamiec przynety (>= 2 krawedzie strefy tego strzelca) - najwyzej L1; g 10..30 m, a jazda >= 40 m od strefy - L1 (333 W3);
    ///  - P poscig 204 (punkty przyciete 10 m przed strefa); strzelec w g < 10 m albo szybszy i 8 s bez zblizania -> F;
    ///  - F front: kazdy jezdziec staje na swoim miejscu (z rozpedem 1 s, przyciete 10 m przed strefa) PRZODEM do strzelca (SetScriptedPositionAndDirection) - nikt nie
    ///    zawraca tylem; strzelec dalej niz Rs od G przez 3 s albo w swojej strefie 5 s bez strzalu -> PS;
    ///  - K kontra: strzelec poza oslona (g >= 20 m) i zbliza sie albo trafil kogos z G z d <= Rw, albo trafil z d <= Rb przy g >= 10 m, przy przewadze -> CALY G prosto
    ///    na niego (w Rb prosto, dalej przeciecie, bez odciecia); konczy: g < 10 (F), szybszy 8 s bez zblizania (F), 40 s (F), strzelec padl;
    ///  - PS powrot szykiem: jezdzcy oddani grze (bez przerwy), szyk 30 s bez capu marszu 128 (MarchToContact - galop); strzal w G w tym czasie: g >= 20 i przewaga - K,
    ///    inaczej znowu F tymi samymi ludzmi; po 30 s - W;
    ///  - R odruch (szyk konnicy AI poza G, np. oslona skrzydla pod ostrzalem): trafiony jezdziec + 2 najblizsi od razu na strzelca (glowny watek, trafienie z ShieldsUp),
    ///    gdy trafienie z d <= Rw przy g >= 20 albo z d <= Rb przy g >= 10.
    /// 344r (recenzje 344, W1-W4): F pod ostrzalem nie stoi - trafienie bez kontry: docisniecie do krawedzi strefy strzelca (punkt w Rb od niego, przy przewadze;
    ///    najwyzej ZPressFor s, dalej trafia - PS), inaczej PS; F konczy sie tez, gdy strzelec pod oslona (g &lt; 20 m) milczy 5 s; kontra i odruch poza oslona w calym Rs
    ///    (strzal z 40-60 m); strzal w PS pod oslona - powrot dalej (bez nawrotu do F), F z powrotu tylko gdy strzelec wyszedl za wracajacymi poza oslone w Rw;
    ///    konni lucznicy poza przewaga G; K najwyzej max(PerVictim + 1, 3 x (1 + eskorta)) z szyku; linia: "front pod ostrzalem" (A344-8 bez reakcji = 0).
    /// Bez ochloniecia: po F / K / PS zadnej blokady 60/120/240 s - pamiec przynety ogranicza tylko START P, nigdy F / K / PS / R. Strona przegrywajaca: P zakazany,
    /// F, K i R dozwolone (obrona). Strzelec AI w nietknietym szyku swojej strony (szyk na szyk 204) - bez K / R. Bohaterowie (335) i chorazowie nie jada (Elig 204).
    /// Miary w linii "AI bitwy (344): misja ..." (A344-1..7, sprawdz_logi --grupa ai344); linie "AI bitwy (344) front / kontra / powrot / odruch" (najwyzej 16 na bitwe).
    /// Koszt: na zagrozenie co 1 s jedno zapytanie o eskorte (GetNearbyAllyAgents), petla po jezdzcach i szykach; kat plecow tylko przy trafieniu.
    /// </summary>
    internal static partial class HorsePursuit
    {
        // ------------------------------------------------------------ stale (opis klasy)
        internal const float ZFootAdd = 50f, ZCavAdd = 60f, ZShotShare = 0.8f, ZShotMin = 60f, ZShotMax = 120f;
        internal const float ZBreakGap = 10f, ZStartGap = 30f, ZClampGap = 10f, ZLightFar = 40f, ZLoopWin = 120f, ZBackHitWindow = 20f;
        internal const int ZLightAfter = 2;
        // 344: zasiegi i czasy grupy wobec strzelca
        internal const float ZRs = ZShotMin, ZRw = 40f, ZRb = HuntScreen, ZOutGap = 20f, ZKMax = 40f, ZSlowFor = 8f, ZFarHold = 3f, ZDeepHold = 5f, ZPsFor = 30f;
        internal const float ZCloseV = 1f, ZCloseWin = 2f, ZHitRecent = 5f, ZFaster = 1.05f, ZBackAng = 120f, ZFaceAng = 60f, ZAnswer = 2f, ZPredict = 3f, ZGKeep = 30f;
        internal const float ZTrotNear = 60f, ZStopLead = 1f;
        // 344r (recenzje W1-W3): front pod ostrzalem - trafienie w ostatnich ZHitNow s; docisniecie do krawedzi strefy strzelca najwyzej ZPressFor s (Rs przy galopie
        // ~10 m/s), potem odwrot PS; przedluzenie powrotu przy strzale w wracajacych
        internal const float ZHitNow = 1.5f, ZGallop = 10f, ZPressFor = ZRs / ZGallop;
        private const int ZMaxLines = 16, ZListCap = 256;
        private const int BFull = 0, BNone = 2, BLight = 3, BFront = 4, BKontra = 5;   // BLight - jak BFull, ale najwyzej oddzial L1
        private const byte SW = 0, SP = 1, SF = 2, SK = 3, SPS = 4;
        private static readonly string[] SName = { "wolny", "poscig", "front", "kontra", "powrot" };
        internal const byte ZNone = 0, ZFrontRole = 1, ZKontraRole = 2;

        /// <summary>Stan grupy wobec jednego strzelca (Threat.Q).</summary>
        private sealed class St344
        {
            public byte St;
            public float At;
            public float BestD = float.MaxValue, BestDAt = -1f;
            public float D1 = -1f, D1At = -1f, D2 = -1f, D2At = -1f;
            public float HitAt = -1000f, HitD = float.MaxValue;
            public float FarSince = -1f, PsUntil = -1f, CondAt = -1f, AnsAt = -1000f, NoAdvAt = -1000f, HitEvAt = -1000f;
            public float GPrev = float.MaxValue;
            public float PressAt = -1f, FAnsAt = -1000f;   // 344r: docisniecie frontu, ostatnia reakcja na ostrzal frontu
            public bool KFull, KContact, FOpen, LongNoted;
            public bool KPend, KPPs;   // 347b A344-3b: kontra bez jezdzcow na strzelcu czeka na KontraTake (KontraConfirm); KPPs - weszla z powrotu PS
            public bool KEmptyOn;   // 347b-r: pusta kontra juz policzona w tym epizodzie (do kontry z jezdzcami albo passu bez warunku kontry)
            public string KPWhy;
            public int FBack, Esc;
            public readonly List<float> FPend = new List<float>();   // 344r: trafienia w F czekajace na reakcje (A344-8)
            public readonly Dictionary<Formation, float> GF = new Dictionary<Formation, float>();
            public readonly List<Agent> Men = new List<Agent>();
            public readonly List<float> Pend = new List<float>();
        }

        /// <summary>Pamiec przynety wedlug strzelca (przezywa koniec zagrozenia): ile razy doprowadzil poscig do krawedzi swojej strefy.</summary>
        private sealed class Bait
        {
            public float BrokeAt = -1f;
            public int Breaks, Loops;
        }

        private static bool _zOn;
        private static byte _z4Next;   // rola 344 nowych jezdzcow (StartHunter) - ustawiana na czas Take / ReFront
        private static readonly Dictionary<Agent, Bait> Baits = new Dictionary<Agent, Bait>();
        private static readonly List<Vec2>[] ZC = { new List<Vec2>(), new List<Vec2>() };
        private static readonly List<float>[] ZR = { new List<float>(), new List<float>() };
        private static readonly List<Formation>[] ZF = { new List<Formation>(), new List<Formation>() };
        private static readonly HashSet<int> ZNoStartSeen = new HashSet<int>();
        private static readonly List<Formation> GList = new List<Formation>();
        private static readonly List<Formation> GTmp = new List<Formation>();
        private static readonly List<float> ZSpeeds = new List<float>();
        private static readonly List<Hunter> ZTmp = new List<Hunter>();
        // MarchToContact (glowny watek): strzelcy aktywnych zagrozen wedlug strony ostrzeliwanej, szyki w powrocie PS (bez capu marszu do chwili)
        private static readonly List<Vec2>[] SPos = { new List<Vec2>(), new List<Vec2>() };
        private static readonly Dictionary<Formation, float> Ret344 = new Dictionary<Formation, float>();
        private static readonly HashSet<Formation> TrotSeen = new HashSet<Formation>();
        // 347b A344-6: strzelcy w SPos poprzedniego passu - klus liczony tylko przy nich (cap z MarchToContact.Eval liczony jeszcze bez nowego strzelca to opoznienie)
        private static readonly HashSet<int> SPosPrev = new HashSet<int>(), SPosNow = new HashSet<int>();
        private static readonly List<float> KReact = new List<float>();
        private static readonly List<int> FBacks = new List<int>();
        private static int _zFullBefore;
        // liczniki misji
        private static int _zFull, _zNoStart, _zLight, _zLightFull, _zBreaks, _zShooterBreakMax, _zLoops, _zRiderIn, _zClamp, _zZoneMax, _zLines, _zStum;
        private static int _z4L1Fast, _z4Long, _z4NoAdv, _z4F, _z4FRiders, _z4FEdge, _z4FFast, _z4FLose, _z4BackF, _z4BackAll, _z4FaceN, _z4FaceOk;
        private static int _z4K, _z4KFull, _z4R, _z4RRiders, _z4KRiders, _z4Ev, _z4Ans, _z4Miss, _z4KEnd, _z4KOk, _z4Unans, _z4PendN, _z4Ps, _z4PsK, _z4PsF, _z4Trot;
        private static int _z4Press, _z4FOut, _z4FHit, _z4FUnans, _z4PsExt;   // 344r
        // 347b A344-3b/3c: kontry cofniete, bo KontraTake nikogo nie wyslal; powody odrzucenia w Take (tylko na czas KontraTake, bez NoteNw)
        private static int _z4KEmpty, _ktForm, _ktCav, _ktCap, _ktSkip, _ktCand;
        private static readonly int[] KtNo = new int[NwKinds];
        private static readonly HashSet<int> KEmptySeen = new HashSet<int>();

        private static void Bait333Clear()
        {
            Baits.Clear();
            for (int s = 0; s < 2; s++) { ZC[s].Clear(); ZR[s].Clear(); ZF[s].Clear(); SPos[s].Clear(); }
            ZNoStartSeen.Clear(); GList.Clear(); GTmp.Clear(); ZSpeeds.Clear(); ZTmp.Clear(); Ret344.Clear(); TrotSeen.Clear(); KReact.Clear(); FBacks.Clear();
            SPosPrev.Clear(); SPosNow.Clear();
            _z4Next = ZNone;
            _zFullBefore = 0;
            _zFull = _zNoStart = _zLight = _zLightFull = _zBreaks = _zShooterBreakMax = _zLoops = _zRiderIn = _zClamp = _zZoneMax = _zLines = _zStum = 0;
            _z4L1Fast = _z4Long = _z4NoAdv = _z4F = _z4FRiders = _z4FEdge = _z4FFast = _z4FLose = _z4BackF = _z4BackAll = _z4FaceN = _z4FaceOk = 0;
            _z4K = _z4KFull = _z4R = _z4RRiders = _z4KRiders = _z4Ev = _z4Ans = _z4Miss = _z4KEnd = _z4KOk = _z4Unans = _z4PendN = _z4Ps = _z4PsK = _z4PsF = _z4Trot = 0;
            _z4Press = _z4FOut = _z4FHit = _z4FUnans = _z4PsExt = 0;
            _z4KEmpty = 0;
            KEmptySeen.Clear();
        }

        // ------------------------------------------------------------ strefa (pass, po BuildHForms) - bez zmian z 333

        private static void ZoneBuild()
        {
            for (int s = 0; s < 2; s++)
            {
                ZC[s].Clear(); ZR[s].Clear(); ZF[s].Clear();
                if (!_zOn) continue;
                var fl = HForms[s];
                for (int i = 0; i < fl.Count; i++)
                {
                    var fi = fl[i];
                    if (fi.N < IntactMin || fi.Retreat) continue;
                    float add;
                    if (fi.Cav) add = ZCavAdd;
                    else if (fi.Rng)
                    {
                        float r = 0f;
                        try { var q = fi.F.QuerySystem; r = q.MissileRangeAdjusted; if (r <= 1f) r = q.MaximumMissileRange; } catch { r = 0f; }
                        if (r <= 1f) r = ZShotMax / ZShotShare;
                        add = Math.Max(ZShotMin, Math.Min(ZShotMax, ZShotShare * r));
                    }
                    else if (fi.Foot) add = ZFootAdd;
                    else continue;
                    ZC[s].Add(fi.C);
                    ZR[s].Add(fi.R + add);
                    ZF[s].Add(fi.F);
                }
                if (ZC[s].Count > _zZoneMax) _zZoneMax = ZC[s].Count;
            }
        }

        /// <summary>Odleglosc punktu od krawedzi strefy armii strony side (bez szyku ex); float.MaxValue = strona bez szyku w strefie.</summary>
        private static float ZoneGap(int side, Vec2 p, Formation ex)
        {
            if (side < 0 || side > 1) return float.MaxValue;
            var cs = ZC[side];
            var rs = ZR[side];
            var fs = ZF[side];
            float g = float.MaxValue;
            for (int i = 0; i < cs.Count; i++)
            {
                if (ex != null && ReferenceEquals(fs[i], ex)) continue;
                float d = p.Distance(cs[i]) - rs[i];
                if (d < g) g = d;
            }
            return g;
        }

        /// <summary>Punkt jezdzca odsuniety na ZClampGap przed strefe (przejscia do braku ruchu, najwyzej liczba stref + 1 - nakladajace sie strefy).</summary>
        private static Vec2 ZoneClamp(Threat t, Vec2 p)
        {
            int side = t.SSide;
            if (side < 0 || side > 1) return p;
            var cs = ZC[side];
            var rs = ZR[side];
            var fs = ZF[side];
            bool moved = false;
            int its = cs.Count + 1;
            for (int it = 0; it < its; it++)
            {
                int bi = -1;
                float bv = 0f;
                for (int i = 0; i < cs.Count; i++)
                {
                    if (t.ZEx != null && ReferenceEquals(fs[i], t.ZEx)) continue;
                    float v = rs[i] + ZClampGap - p.Distance(cs[i]);
                    if (v > bv) { bv = v; bi = i; }
                }
                if (bi < 0) break;
                Vec2 c = cs[bi];
                Vec2 d = p - c;
                float l = d.Length;
                if (l < 0.1f) break;
                p = c + d * ((rs[bi] + ZClampGap) / l);
                moved = true;
            }
            if (moved) _zClamp++;
            return p;
        }

        /// <summary>W3 333: najblizszy strzelcowi szyk konnicy AI (bez gracza i odwrotu, w smyczy) ma krawedz >= ZLightFar od strefy armii strzelca.</summary>
        private static bool CavFarFromZone(Threat t, Vec2 sp)
        {
            if (t.Side < 0 || t.Side > 1) return false;
            var fl = HForms[t.Side];
            int best = -1;
            float bd = float.MaxValue;
            for (int i = 0; i < fl.Count; i++)
            {
                var fi = fl[i];
                if (!fi.Cav || fi.Player || fi.Retreat) continue;
                float e = Math.Max(0f, sp.Distance(fi.C) - fi.R);
                if (e > _maxFrom || e >= bd) continue;
                bd = e;
                best = i;
            }
            if (best < 0) return false;
            return ZoneGap(t.SSide, fl[best].C, t.ZEx) - fl[best].R >= ZLightFar;
        }

        // ------------------------------------------------------------ grupa G (pass)

        private static int FindFI(List<FI> fl, Formation f)
        {
            if (f == null) return -1;
            for (int i = 0; i < fl.Count; i++) if (ReferenceEquals(fl[i].F, f)) return i;
            return -1;
        }

        private static int RidersOf(Threat t)
        {
            int n = 0;
            for (int i = 0; i < t.Hs.Count; i++)
            {
                var r = t.Hs[i].Role;
                if (r == RoleIntercept || r == RoleCut) n++;
            }
            return n;
        }

        /// <summary>Szyki G (jezdzcy 204 + szyki trafione w ZGKeep s) do GList; bez nich najblizszy szyk konnicy AI w smyczy. Zwraca d (strzelec - najblizszy jezdziec /
        /// krawedz szyku G), gN - ludzi G.</summary>
        private static float GNear(Threat t, Vec2 sp, float now, out int gN)
        {
            var q = t.Q;
            for (int i = 0; i < t.Hs.Count; i++)
            {
                var h = t.Hs[i];
                if ((h.Role == RoleIntercept || h.Role == RoleCut) && h.F != null) q.GF[h.F] = now;
            }
            GTmp.Clear();
            foreach (var kv in q.GF) if (now - kv.Value > ZGKeep) GTmp.Add(kv.Key);
            for (int i = 0; i < GTmp.Count; i++) q.GF.Remove(GTmp[i]);
            GTmp.Clear();
            GList.Clear();
            gN = 0;
            float d = float.MaxValue;
            var fl = HForms[t.Side];
            foreach (var kv in q.GF)
            {
                int i = FindFI(fl, kv.Key);
                if (i < 0) continue;
                var fi = fl[i];
                if (!fi.Cav || fi.Ha || fi.Player || fi.Retreat) continue;   // recenzja 344 W4: konni lucznicy nie jada w K - bez nich w przewadze
                GList.Add(fi.F);
                gN += fi.N;
                float e = Math.Max(0f, sp.Distance(fi.Rest) - fi.R);
                if (e < d) d = e;
            }
            if (GList.Count == 0)
            {
                int best = -1;
                float bd = float.MaxValue;
                for (int i = 0; i < fl.Count; i++)
                {
                    var fi = fl[i];
                    if (!fi.Cav || fi.Ha || fi.Player || fi.Retreat) continue;
                    float e = Math.Max(0f, sp.Distance(fi.C) - fi.R);
                    if (e > _maxFrom || e >= bd) continue;
                    bd = e;
                    best = i;
                }
                if (best >= 0) { GList.Add(fl[best].F); gN = fl[best].N; d = bd; }
            }
            for (int i = 0; i < t.Hs.Count; i++)
            {
                var h = t.Hs[i];
                if (h.Role != RoleIntercept && h.Role != RoleCut || h.A == null) continue;
                float e = h.A.Position.AsVec2.Distance(sp);
                if (e < d) d = e;
            }
            int riders = RidersOf(t);
            if (gN < riders) gN = riders;
            return d;
        }

        /// <summary>Eskorta: konni ludzie strony strzelca w Rw od niego (bez niego) - jedno zapytanie.</summary>
        private static int Escort(Mission m, Agent s, Vec2 sp)
        {
            int n = 0;
            try
            {
                m.GetNearbyAllyAgents(sp, ZRw, s.Team, HBuf);
                for (int i = 0; i < HBuf.Count; i++)
                {
                    var x = HBuf[i];
                    if (x == null || ReferenceEquals(x, s) || !x.IsHuman || !x.IsActive() || x.MountAgent == null || x.IsRunningAway) continue;
                    n++;
                }
            }
            finally { HBuf.Clear(); }
            return n;
        }

        /// <summary>Strzelec szybszy: najwieksza predkosc jego konia > 1.05 x mediana koni jezdzcow G (bez jezdzcow - pierwszy konny szyku G).</summary>
        private static bool Faster(Threat t, Agent s)
        {
            var sm = s.MountAgent;
            if (sm == null) return false;
            float vs;
            try { vs = sm.GetMaximumForwardUnlimitedSpeed(); } catch { return false; }
            ZSpeeds.Clear();
            for (int i = 0; i < t.Hs.Count; i++)
            {
                var h = t.Hs[i];
                if (h.Role != RoleIntercept && h.Role != RoleCut || h.A == null) continue;
                var am = h.A.MountAgent;
                if (am == null) continue;
                try { ZSpeeds.Add(am.GetMaximumForwardUnlimitedSpeed()); } catch { }
            }
            if (ZSpeeds.Count == 0)
            {
                for (int i = 0; i < GList.Count && ZSpeeds.Count == 0; i++)
                {
                    Agent u = null;
                    try { u = GList[i].GetFirstUnit(); } catch { u = null; }
                    var um = u != null ? u.MountAgent : null;
                    if (um == null) continue;
                    try { ZSpeeds.Add(um.GetMaximumForwardUnlimitedSpeed()); } catch { }
                }
            }
            if (ZSpeeds.Count == 0) return false;
            ZSpeeds.Sort();
            float vr = ZSpeeds[ZSpeeds.Count / 2];
            ZSpeeds.Clear();
            return vs > ZFaster * vr;
        }

        /// <summary>Zbliza sie: d maleje >= ZCloseV m/s przez ZCloseWin s (dwa ostatnie passy).</summary>
        private static bool Closing(St344 q, float d, float now)
        {
            bool c = false;
            if (d < float.MaxValue && q.D2At >= 0f && q.D1At >= 0f)
            {
                float dt = now - q.D2At;
                c = dt >= 0.75f * ZCloseWin && dt <= 2f * ZCloseWin && (q.D2 - d) / dt >= ZCloseV && q.D1 - d >= 0f;
            }
            if (d < float.MaxValue)
            {
                q.D2 = q.D1; q.D2At = q.D1At;
                q.D1 = d; q.D1At = now;
            }
            else { q.D1At = q.D2At = -1f; }
            return c;
        }

        // ------------------------------------------------------------ brama zagrozenia (ThreatPass, przed wysylaniem jezdzcow)

        /// <summary>Stan grupy 344 i tryb wysylki: BFull / BLight (204), BNone, BFront (jezdzcy stoja przodem), BKontra (caly G na strzelca).</summary>
        private static int BaitGate(Mission m, Threat t, Vec2 sp, float now, bool rx, bool losing)
        {
            _zFullBefore = 0;
            var q = t.Q;
            t.Z333 = _zOn && _hKey && !(rx && MeleeKind(t));
            if (!t.Z333) { if (q.St != SW) Leave(t, q, now, false); return BFull; }
            var s = t.S;
            Formation ex = null;
            try { ex = s.Formation; } catch { ex = null; }
            t.ZEx = ex;
            float g = ZoneGap(t.SSide, sp, ex);
            t.Gap333 = g;
            q.GPrev = g;
            int riders = RidersOf(t);
            _zFullBefore = riders;
            int gN;
            float d = GNear(t, sp, now, out gN);
            bool closing = Closing(q, d, now);
            bool hitRw = now - q.HitAt <= ZHitRecent && q.HitD <= ZRw;
            bool hitRb = now - q.HitAt <= ZHitRecent && q.HitD <= ZRb;
            bool hitRs = now - q.HitAt <= ZHitRecent && q.HitD <= ZRs;   // recenzja 344 W2: strzelec poza oslona strzelajacy z 40-60 m - tez kontra
            int esc = gN > 0 ? Escort(m, s, sp) : 0;
            q.Esc = esc;
            bool adv = gN > 0 && gN >= 2 * (1 + esc);
            bool faster = Faster(t, s);
            if (d < float.MaxValue && d < q.BestD - 1f) { q.BestD = d; q.BestDAt = now; }
            bool outCover = g >= ZOutGap;
            bool kc = !t.Group && adv && d <= _maxFrom && ((outCover && (closing || hitRs)) || (g >= ZBreakGap && hitRb));
            if (!kc) q.KEmptyOn = false;   // 347b-r: koniec epizodu pustej kontry
            // A344-2: zdarzenie "strzelec poza oslona zbliza sie / trafia z Rw" przy przewadze - czeka na K / R
            bool ev = !t.Group && adv && outCover && (closing || hitRw) && d <= ZRw;
            // poczatek zdarzenia: trafienie z Rw poza oslona (OnRiderShot344) albo ten pass - reakcja liczona od trafienia (pass co 1 s)
            if (ev && q.St != SK && q.CondAt < 0f) { q.CondAt = now - q.HitEvAt <= 1.5f ? q.HitEvAt : now; _z4Ev++; }
            else if (!ev && q.St != SK && q.CondAt >= 0f) { q.CondAt = -1f; _z4Miss++; }
            if (!adv && gN > 0 && (closing || hitRw) && outCover) { _z4NoAdv++; q.NoAdvAt = now; }
            PendCheck(q, now);
            FPendCheck(q, now);
            if (riders > 0 && q.St == SW) { q.St = SP; q.At = now; q.BestD = d; q.BestDAt = now; }
            switch (q.St)
            {
                case SP:
                    if (riders == 0) { Leave(t, q, now, false); break; }
                    if (kc) { EnterK(t, q, now, true, "strzelec " + F0(g) + " m od strefy swojej armii, zbliza sie / trafia"); return BKontra; }
                    if (g < ZBreakGap)
                    {
                        BaitMemory(s, now);
                        _z4FEdge++;
                        EnterF(t, q, now, "krawedz strefy (" + F0(g) + " m)");
                        return BFront;
                    }
                    if (faster && q.BestDAt >= 0f && now - q.BestDAt >= ZSlowFor)
                    {
                        if (now - q.BestDAt > ZSlowFor + 1.5f && !q.LongNoted) { q.LongNoted = true; _z4Long++; }
                        _z4FFast++;
                        EnterF(t, q, now, "strzelec szybszy, " + F0(ZSlowFor) + " s bez zblizania");
                        return BFront;
                    }
                    if (losing) { _z4FLose++; EnterF(t, q, now, "strona przegrywa"); return BFront; }
                    return Light(s) ? BLight : BFull;
                case SF:
                    if (riders == 0) { Leave(t, q, now, false); break; }
                    if (kc) { EnterK(t, q, now, true, "strzelec wyszedl " + F0(g) + " m za oslone"); return BKontra; }
                    if (d >= ZRs) { if (q.FarSince < 0f) q.FarSince = now; } else q.FarSince = -1f;
                    // recenzja 344 W1 (c): strzelec pod oslona swojej armii (nie tylko w strefie) i cichy 5 s - front nie stoi bez konca
                    bool deep = g < ZOutGap && now - t.LastFire >= ZDeepHold;
                    if ((q.FarSince >= 0f && now - q.FarSince >= ZFarHold) || deep) { EnterPS(t, q, now, deep ? "strzelec pod oslona swojej armii bez strzalu" : "strzelec poza zasiegiem"); return BNone; }
                    // recenzja 344 W1 (a, b): front pod ostrzalem bez kontry - docisniecie do krawedzi strefy strzelca (szansa na cios, strzelec musi sie cofnac), gdy
                    // ten punkt jest w Rb od niego; strzelec glebiej albo docisniecie ZPressFor s bez skutku (dalej trafia) - odwrot PS poza zasieg
                    bool hitNow = q.HitAt >= q.At && now - q.HitAt <= ZHitNow;
                    Vec2 edge = ZoneClamp(t, sp);
                    bool edgeOk = edge.Distance(sp) <= ZRb;
                    if (q.PressAt >= 0f)
                    {
                        if (hitNow && now - q.PressAt >= ZPressFor) { _z4FOut++; EnterPS(t, q, now, "docisniecie " + F0(ZPressFor) + " s bez skutku, dalej ostrzal - odwrot poza zasieg"); return BNone; }
                        if (edgeOk) FrontTo(t, edge);
                        else q.PressAt = -1f;   // strzelec cofnal sie w glab swojej armii - jezdzcy stoja na punkcie docisniecia (ostrzal stamtad - odwrot)
                    }
                    else if (hitNow)
                    {
                        if (!edgeOk || !adv) { _z4FOut++; EnterPS(t, q, now, !adv ? "ostrzal bez przewagi nad strzelcem i eskorta - odwrot poza zasieg" : "ostrzal spod oslony armii strzelca - odwrot poza zasieg"); return BNone; }
                        q.PressAt = now;
                        q.FAnsAt = now;
                        _z4Press++;
                        FrontTo(t, edge);
                        ZLine("front: strzelec " + Who(t.S) + " " + F0(g) + " m od strefy swojej armii strzela w stojacych | docisniecie do krawedzi jego strefy ("
                              + F0(edge.Distance(sp)) + " m od niego), najwyzej " + F0(ZPressFor) + " s");
                    }
                    FaceSample(t, sp);
                    return BFront;
                case SK:
                    if (!q.KFull && kc) q.KFull = true;
                    if (riders == 0)
                    {
                        if (now - q.At > 1.5f) { Leave(t, q, now, false); return BNone; }   // 347b A344-3a: KEnd robi Leave (wczesniej liczony dwa razy)   // recenzja 344 D1 (techn.): po wyjsciu bez kontry
                        return q.KFull ? BKontra : BNone;
                    }
                    if (d <= ShieldsUp.RaiseNear) q.KContact = true;
                    if (g < ZBreakGap) { KEnd(q, true); EnterF(t, q, now, "kontra odepchnela strzelca pod jego armie"); return BFront; }
                    if (faster && now - q.BestDAt >= ZSlowFor) { KEnd(q, q.KContact); _z4FFast++; EnterF(t, q, now, "kontra: strzelec szybszy, bez zblizania"); return BFront; }
                    if (now - q.At >= ZKMax) { KEnd(q, q.KContact); EnterF(t, q, now, "kontra " + F0(ZKMax) + " s"); return BFront; }
                    return q.KFull ? BKontra : BNone;
                case SPS:
                    if (kc) { _z4PsK++; EnterK(t, q, now, true, "strzelec za wracajacymi, " + F0(g) + " m od strefy"); return BKontra; }
                    if (q.HitAt >= q.At && now - q.HitAt <= ZHitNow)
                    {
                        if (g >= ZOutGap && adv) { _z4PsK++; EnterK(t, q, now, true, "strzal w wracajacych"); return BKontra; }
                        // recenzja 344 W3: front z powrotu tylko, gdy strzelec wyszedl za wracajacymi poza oslone (stanac przodem do szarzy z eskorta); pod oslona -
                        // dalej powrot galopem poza zasieg (bez petli PS -> F -> PS w zasiegu oslonietego strzelca)
                        if (g >= ZOutGap && d <= ZRw && ReFront(m, t, q, now)) { _z4PsF++; return BFront; }
                        PsExtend(q, now);
                    }
                    if (now >= q.PsUntil) { q.St = SW; q.At = now; q.Men.Clear(); q.FOpen = false; FClose(q); }
                    return BNone;
            }
            // SW
            if (kc) { EnterK(t, q, now, true, "strzelec " + F0(g) + " m od strefy swojej armii nadjezdza / trafia"); return BKontra; }
            if (losing) return BNone;
            bool startOk = g >= ZStartGap && PredGap(t, s, sp) >= ZClampGap;
            if (!startOk)
            {
                if (g >= ZBreakGap && g < ZStartGap && CavFarFromZone(t, sp)) { _zLight++; return BLight; }
                if (g < ZStartGap && ZNoStartSeen.Add(s.Index)) _zNoStart++;
                return BNone;
            }
            if (!adv) return BNone;
            if (faster && !closing)
            {
                if (d <= ZRw) { _z4L1Fast++; return BLight; }
                return BNone;
            }
            return Light(s) ? BLight : BFull;
        }

        private static bool Light(Agent s)
        {
            Bait b;
            bool l = Baits.TryGetValue(s, out b) && b.Breaks >= ZLightAfter;
            return l;
        }

        /// <summary>Odleglosc punktu strzelca za ZPredict s (predkosc konia) od jego strefy.</summary>
        private static float PredGap(Threat t, Agent s, Vec2 sp)
        {
            Vec2 v = Vec2.Zero;
            try { var sm = s.MountAgent ?? s; v = sm.Velocity.AsVec2; } catch { v = Vec2.Zero; }
            return ZoneGap(t.SSide, sp + v * ZPredict, t.ZEx);
        }

        private static void BaitMemory(Agent s, float now)
        {
            Bait b;
            if (!Baits.TryGetValue(s, out b)) { b = new Bait(); Baits[s] = b; }
            if (b.BrokeAt >= 0f && now - b.BrokeAt < ZLoopWin) { b.Loops++; if (b.Loops > _zLoops) _zLoops = b.Loops; }
            b.BrokeAt = now;
            b.Breaks++;
            if (b.Breaks > _zShooterBreakMax) _zShooterBreakMax = b.Breaks;
            _zBreaks++;
        }

        /// <summary>A344-4: trafienia z Rw poza oslona bez K / R w ZAnswer s.</summary>
        private static void PendCheck(St344 q, float now)
        {
            for (int i = q.Pend.Count - 1; i >= 0; i--)
            {
                float p = q.Pend[i];
                if (q.AnsAt >= p - 0.01f || (q.St == SK && q.At <= p + ZAnswer) || q.NoAdvAt >= p - 0.01f) { q.Pend.RemoveAt(i); continue; }   // bez przewagi - F (nie odpowiedz) jest rozsadny
                if (now - p < ZAnswer) continue;
                _z4Unans++;
                q.Pend.RemoveAt(i);
            }
        }

        /// <summary>344r A344-8: trafienia jezdzcow frontu bez reakcji (docisniecie / odwrot / kontra) w ZAnswer s.</summary>
        private static void FPendCheck(St344 q, float now)
        {
            for (int i = q.FPend.Count - 1; i >= 0; i--)
            {
                float p = q.FPend[i];
                if (q.FAnsAt >= p - 0.01f) { q.FPend.RemoveAt(i); continue; }
                if (now - p < ZAnswer) continue;
                _z4FUnans++;
                q.FPend.RemoveAt(i);
            }
        }

        /// <summary>344r docisniecie: jezdzcy frontu na punkt krawedzi strefy strzelca (kazdy z przesunieciem wzgledem srodka grupy - bez tloku w jednym punkcie).</summary>
        private static void FrontTo(Threat t, Vec2 edge)
        {
            Vec2 sum = Vec2.Zero;
            int n = 0;
            for (int i = 0; i < t.Hs.Count; i++)
            {
                var h = t.Hs[i];
                if (h.Z4 != ZFrontRole || h.A == null) continue;
                sum += h.A.Position.AsVec2;
                n++;
            }
            if (n == 0) return;
            Vec2 mean = sum * (1f / n);
            for (int i = 0; i < t.Hs.Count; i++)
            {
                var h = t.Hs[i];
                if (h.Z4 != ZFrontRole || h.A == null) continue;
                h.FrontAt = ZoneClamp(t, edge + (h.A.Position.AsVec2 - mean));
            }
        }

        /// <summary>344r (recenzja W3): strzal w wracajacych pod oslona strzelca - powrot dalej galopem (okno PS i bez capu marszu od nowa).</summary>
        private static void PsExtend(St344 q, float now)
        {
            if (q.PsUntil >= now + ZPsFor - 1f) return;
            q.PsUntil = now + ZPsFor;
            q.FAnsAt = now;
            _z4PsExt++;
            for (int i = 0; i < q.Men.Count; i++)
            {
                var a = q.Men[i];
                Formation f = null;
                try { f = a != null && a.IsActive() ? a.Formation : null; } catch { f = null; }
                if (f != null) Ret344[f] = q.PsUntil;
            }
        }

        // ------------------------------------------------------------ przejscia

        private static void FClose(St344 q)
        {
            if (!q.FOpen) return;
            q.FOpen = false;
            if (FBacks.Count < ZListCap) FBacks.Add(q.FBack);
            q.FBack = 0;
        }

        /// <summary>Wyjscie ze stanu do W (bez jezdzcow, klucz).</summary>
        private static void Leave(Threat t, St344 q, float now, bool line)
        {
            if (q.St == SK && !q.KPend) KEnd(q, q.KContact);
            q.KPend = false;
            FClose(q);
            q.St = SW;
            q.At = now;
            q.FarSince = -1f;
            q.CondAt = -1f;
            q.PressAt = -1f;
            q.LongNoted = false;
            for (int i = 0; i < t.Hs.Count; i++) t.Hs[i].Z4 = ZNone;
        }

        private static void KEnd(St344 q, bool ok)
        {
            _z4KEnd++;
            if (ok) _z4KOk++;
        }

        /// <summary>F: jezdzcy grupy staja na swoim miejscu (z rozpedem ZStopLead s, przyciete przed strefa) przodem do strzelca.</summary>
        private static void EnterF(Threat t, St344 q, float now, string why)
        {
            FClose(q);
            int n = 0;
            for (int i = 0; i < t.Hs.Count; i++)
            {
                var h = t.Hs[i];
                if (h.Role != RoleIntercept && h.Role != RoleCut || h.A == null) continue;
                Vec2 p = h.A.Position.AsVec2;
                try { var am = h.A.MountAgent ?? h.A; p += am.Velocity.AsVec2 * ZStopLead; } catch { }
                h.FrontAt = ZoneClamp(t, p);
                h.Z4 = ZFrontRole;
                n++;
            }
            q.St = SF;
            q.At = now;
            q.FarSince = -1f;
            q.PressAt = -1f;
            q.FOpen = true;
            q.FBack = 0;
            q.LongNoted = false;
            _z4F++;
            _z4FRiders += n;
            ZLine("front: strzelec " + Who(t.S) + " (" + (t.S.IsMainAgent ? "gracz" : "AI") + ") " + F0(t.Gap333) + " m od strefy swojej armii | " + why + " | jezdzcow "
                  + n + " staje przodem do niego, bez zawracania");
        }

        /// <summary>K: caly G na strzelca (full) albo tylko obecni jezdzcy (odruch R); czas reakcji A344-2.</summary>
        private static void EnterK(Threat t, St344 q, float now, bool full, string why)
        {
            FClose(q);
            int n = 0;
            for (int i = 0; i < t.Hs.Count; i++)
            {
                var h = t.Hs[i];
                if (h.Role != RoleIntercept && h.Role != RoleCut) continue;
                h.Z4 = ZKontraRole;
                h.Role = RoleIntercept;
                n++;
            }
            bool was = q.St == SK;
            bool fromPs = q.St == SPS;
            q.St = SK;
            q.At = now;
            q.KFull = full || q.KFull;
            q.KContact = false;
            q.BestD = float.MaxValue;
            q.BestDAt = now;
            q.PressAt = -1f;
            q.Men.Clear();
            // 347b A344-3b: nowa kontra calego G bez nikogo na strzelcu liczy sie (odpowiedz, kontra, linia) dopiero, gdy KontraTake kogos wysle (KontraConfirm)
            if (!was && full && n == 0)
            {
                q.KPend = true;
                q.KPPs = fromPs;
                q.KPWhy = why;
                return;
            }
            KCount(t, q, now, was, full, why, n);
        }

        /// <summary>Odpowiedz A344-2, licznik kontr i linia "kontra:" (z EnterK albo po potwierdzeniu w KontraConfirm).</summary>
        private static void KCount(Threat t, St344 q, float now, bool was, bool full, string why, int n)
        {
            q.AnsAt = now;
            q.FAnsAt = now;
            q.KEmptyOn = false;   // 347b-r
            if (q.CondAt >= 0f)
            {
                if (KReact.Count < ZListCap) KReact.Add(now - q.CondAt);
                _z4Ans++;
                q.CondAt = -1f;
            }
            if (was) return;
            _z4K++;
            if (full) _z4KFull++;
            ZLine("kontra: strzelec " + Who(t.S) + " (" + (t.S.IsMainAgent ? "gracz" : "AI") + ") " + F0(t.Gap333) + " m od strefy swojej armii | " + why + " | "
                  + (full ? "caly G" : "oddzial") + ", jezdzcow juz na nim " + n);
        }

        /// <summary>347b A344-3b: po KontraTake (ten sam pass) - kontra z wyslanymi jezdzcami liczy sie jak dotad; bez nikogo (sent 0, jezdzcow 0) cofnieta do W
        /// bez wyniku kontry i bez odpowiedzi (zdarzenie A344-2 czeka dalej), osobny licznik i linia z powodami odrzucenia (A344-3c, raz na strzelca).</summary>
        private static void KontraConfirm(Threat t, float now, int sent)
        {
            var q = t.Q;
            if (!q.KPend) return;
            q.KPend = false;
            if (q.St != SK) return;
            if (sent > 0 || RidersOf(t) > 0) { KCount(t, q, now, false, true, q.KPWhy, 0); return; }
            if (!q.KEmptyOn) { q.KEmptyOn = true; _z4KEmpty++; }   // 347b-r: raz na epizod (SW ponawia EnterK co pass, poki kc)
            if (q.KPPs && _z4PsK > 0) _z4PsK--;
            float cond = q.CondAt;
            q.St = SW;
            Leave(t, q, now, false);
            q.CondAt = cond;
            q.KFull = false;
            if (t.S == null || !KEmptySeen.Add(t.S.Index)) return;
            var sb = new StringBuilder();
            for (int i = 0; i < NwKinds; i++) if (KtNo[i] > 0) sb.Append(sb.Length > 0 ? ", " : "").Append(i).Append(':').Append(KtNo[i]);
            ZLine("kontra bez wolnych jezdzcow: strzelec " + Who(t.S) + " (" + (t.S.IsMainAgent ? "gracz" : "AI") + ") " + F0(t.Gap333) + " m od strefy swojej armii | "
                  + q.KPWhy + " | szykow G " + GList.Count + ", eskorta " + q.Esc + " | szyki pominiete " + _ktForm + ", zwarcie/oslona przed jazda (283) " + _ktCav
                  + ", limit przegladu " + _ktCap + ", juz w poscigu/lucznicy/piesi " + _ktSkip + ", kandydaci " + _ktCand + " | odrzuceni wedlug Nw (0 zwarcie, 1 panika, "
                  + "2 poscig 148, 3 bohater, 4 chorazy, 5 kon, 6 daleko, 7 gracz, 9 zajety, 10 przerwa, 11 limit zapytan, 12 inne, 13 nietkniety szyk, 14 strefa) "
                  + (sb.Length > 0 ? sb.ToString() : "-") + " | cofnieta, bez wyniku");
        }

        /// <summary>PS: jezdzcy oddani grze bez przerwy, szyki 30 s bez capu marszu; ludzie zapamietani (strzal w plecy - znowu F tymi samymi).</summary>
        private static void EnterPS(Threat t, St344 q, float now, string why)
        {
            ZTmp.Clear();
            for (int i = 0; i < t.Hs.Count; i++)
            {
                var h = t.Hs[i];
                if (h.Role == RoleIntercept || h.Role == RoleCut) ZTmp.Add(h);
            }
            q.Men.Clear();
            int n = 0;
            for (int i = 0; i < ZTmp.Count; i++)
            {
                var h = ZTmp[i];
                Hunter cur;
                if (h.A == null || !Hunters.TryGetValue(h.A, out cur) || !ReferenceEquals(cur, h)) continue;
                if (q.Men.Count < 64) q.Men.Add(h.A);
                if (h.F != null) Ret344[h.F] = now + ZPsFor;
                try { EndHunter(h, UPs344, now, false, false); }   // recenzja 344 D6: osobny powod (nie 'zawroceni w strefie')
                catch (Exception e) { _zStum++; HStumble("HorsePursuit.Powrot344", e); DropHunter(h); }
                n++;
            }
            ZTmp.Clear();
            q.St = SPS;
            q.At = now;
            q.PsUntil = now + ZPsFor;
            q.FAnsAt = now;
            q.PressAt = -1f;
            _z4Ps++;
            ZLine("powrot: strzelec " + Who(t.S) + " " + F0(t.Gap333) + " m od strefy swojej armii | " + why + " | jezdzcow " + n + " wraca do szyku galopem ("
                  + F0(ZPsFor) + " s bez capu marszu)");
        }

        /// <summary>PS -> F: strzal w wracajacych przy oslonie strzelca - ci sami ludzie znowu staja przodem.</summary>
        private static bool ReFront(Mission m, Threat t, St344 q, float now)
        {
            var fl = HForms[t.Side];
            int n = 0;
            _z4Next = ZFrontRole;
            try
            {
                for (int i = 0; i < q.Men.Count; i++)
                {
                    var a = q.Men[i];
                    if (a == null || !a.IsActive() || Hunters.ContainsKey(a) || a.MountAgent == null || a.IsRunningAway) continue;
                    if (Elig(m, a, t, now) != -1) continue;   // recenzja 344 D1: te same warunki co 204 (odwrot, panika, przerwa, kon, szyk strzelca, strefa)
                    int k = FindFI(fl, a.Formation);
                    if (k < 0 || fl[k].Player || fl[k].Retreat) continue;
                    var h = StartHunter(t, a, fl[k], RoleIntercept, 0f, now);
                    h.FrontAt = ZoneClamp(t, a.Position.AsVec2);
                    Steer(h, now);
                    n++;
                }
            }
            finally { _z4Next = ZNone; }
            if (n <= 0) return false;
            EnterF(t, q, now, "strzal w wracajacych przy oslonie strzelca");
            return true;
        }

        /// <summary>K: wolni jezdzcy wszystkich szykow G (Take - te same warunki co 204), rola kontry.</summary>
        private static void KontraTake(Mission m, Threat t, List<FI> fl, float now, ref int sent, ref int sentCharge)
        {
            _z4Next = ZKontraRole;
            int before = sent;
            _ktForm = _ktCav = _ktCap = _ktSkip = _ktCand = 0;   // 347b A344-3c: powody odrzucenia tej kontry
            Array.Clear(KtNo, 0, KtNo.Length);
            try
            {
                GTmp.Clear();
                GTmp.AddRange(GList);
                for (int i = 0; i < GTmp.Count; i++)
                {
                    int k = FindFI(fl, GTmp[i]);
                    if (k < 0) { _ktForm++; continue; }
                    var fi = fl[k];
                    if (!fi.Cav || fi.Ha || fi.Retreat) { _ktForm++; continue; }
                    if (fi.Player) { _ktForm++; NotePlayerForm(t, fi, 0f); continue; }
                    Take(m, t, fi, Math.Max(PerVictim + 1, 3 * (1 + t.Q.Esc)), now, ref sent, ref sentCharge);   // recenzja 344 D2: nie cale skrzydlo za jednym strzelcem
                }
                GTmp.Clear();
            }
            finally { _z4Next = ZNone; }
            _z4KRiders += sent - before;
        }

        /// <summary>A344-5: jezdzcy F z koniem przodem do strzelca (kat &lt; 60 st.).</summary>
        private static void FaceSample(Threat t, Vec2 sp)
        {
            for (int i = 0; i < t.Hs.Count; i++)
            {
                var h = t.Hs[i];
                if (h.Z4 != ZFrontRole || h.A == null || !h.A.IsActive()) continue;
                Vec2 look;
                try { var am = h.A.MountAgent ?? h.A; look = am.LookDirection.AsVec2; } catch { continue; }
                Vec2 to = sp - h.A.Position.AsVec2;
                if (to.Length < 0.5f || look.Length < 0.01f) continue;
                _z4FaceN++;
                if (Deg(look, to) < ZFaceAng) _z4FaceOk++;
            }
        }

        private static float Deg(Vec2 a, Vec2 b)
        {
            double d = Math.Atan2(b.y, b.x) - Math.Atan2(a.y, a.x);
            while (d > Math.PI) d -= 2.0 * Math.PI;
            while (d < -Math.PI) d += 2.0 * Math.PI;
            return (float)(Math.Abs(d) * 180.0 / Math.PI);
        }

        /// <summary>ThreatPass po wysylce w stanie BFull / BLight: nowy poscig (licznik), stan P.</summary>
        private static void BaitFullSent(Threat t, float now, int sent)
        {
            if (!t.Z333 || sent <= 0 || _zFullBefore > 0) return;
            _zFull++;
            var q = t.Q;
            if (q.St == SW) { q.St = SP; q.At = now; q.BestD = float.MaxValue; q.BestDAt = now; q.LongNoted = false; }
        }

        /// <summary>EndThreat: K konczy sie wynikiem (padl = dobry), F zamkniety.</summary>
        private static void Front344End(Threat t, bool down)
        {
            var q = t.Q;
            if (q.St == SK && !q.KPend) KEnd(q, down || q.KContact);
            q.KPend = false;
            FClose(q);
            q.St = SW;
        }

        // ------------------------------------------------------------ trafienie jezdzca AI (ShieldsUp.RiderHit -> OnRiderShot, glowny watek)

        /// <summary>344: kazdy konny czlowiek AI trafiony pociskiem strzelca s (nie tylko szyk IsCavalryFormation): trafienie G (kontra), plecy (A344-1), odruch R,
        /// trafienia czekajace na odpowiedz (A344-4).</summary>
        private static void OnRiderShot344(Mission m, Agent a, Agent s)
        {
            if (!_zOn || !_hKey || a == null || s == null || !s.IsHuman || s.MountAgent == null) return;
            Threat t;
            if (!Threats.TryGetValue(s, out t) || t.S == null) return;
            if (t.Side < 0 || t.Side > 1) return;   // recenzja 344 D2 (techn.): poza passem - HForms[t.Side]
            var q = t.Q;
            float now = m.CurrentTime;
            Vec2 sp = s.Position.AsVec2, ap = a.Position.AsVec2;
            float d = ap.Distance(sp);
            float g = ZoneGap(t.SSide, sp, t.ZEx);
            Hunter h;
            bool mine = Hunters.TryGetValue(a, out h) && ReferenceEquals(h.T, t) && h.Role != RoleRanged;
            var f = a.Formation;
            bool inG = mine || (f != null && q.GF.ContainsKey(f));
            if (f != null) q.GF[f] = now;
            q.HitAt = now;
            q.HitD = d;
            // plecy: kierunek jazdy jezdzca odwrocony od strzelca (> 120 st.)
            if (inG && d <= ZRs && g >= ZBreakGap)
            {
                Vec2 mv;
                try { var am = a.MountAgent ?? a; mv = am.Velocity.AsVec2; if (mv.Length < 1f) mv = am.LookDirection.AsVec2; } catch { mv = Vec2.Zero; }
                if (mv.Length > 0.01f && Deg(mv, sp - ap) > ZBackAng)
                {
                    _z4BackAll++;
                    if (q.St == SF || q.St == SPS) { _z4BackF++; q.FBack++; }
                }
            }
            bool outCover = g >= ZOutGap;
            bool reach = (outCover && d <= ZRs) || (g >= ZBreakGap && d <= ZRb);   // recenzja 344 W2: poza oslona caly zasieg Rs
            if (mine && q.St == SF && h.Z4 == ZFrontRole)
            {
                _z4FHit++;
                if (q.PressAt < 0f && q.FPend.Count < 16) q.FPend.Add(now);   // A344-8: trafienie stojacego frontu - reakcja w ZAnswer s
            }
            if (outCover && d <= ZRw && !t.Group)
            {
                q.HitEvAt = now;
                _z4PendN++;
                if (q.Pend.Count < 16) q.Pend.Add(now);
            }
            if (!reach || t.Group || mine || q.St == SK) return;
            // R: trafiony + 2 najblizsi z jego szyku (szyk konnicy AI, nie gracza, nie w odwrocie, nie w zwarciu z szykiem wroga)
            var fl = HForms[t.Side];
            int k = FindFI(fl, f);
            if (k < 0) return;
            var fi = fl[k];
            if (fi.Ha || fi.Player || fi.Retreat) return;   // konni lucznicy szykiem strzelaja (gra); kazdy inny szyk - konni z niego (Elig 204)
            if (!_rKey)
            {
                float eg;
                try { eg = MarchToContact.EnemyGap(f); } catch { eg = float.MaxValue; }
                if (eg <= MarchToContact.ContactGap) return;
            }
            int sent = 0, sc = 0;
            // poza passem: wlasny przeglad i zapytania (licznik passu bywa wyczerpany - odruch nie moze czekac na nastepny pass)
            int sv = _hScan, qv = _hQPass;
            _hScan = 0;
            _hQPass = 0;
            HFormsTxt.Length = 0;
            _z4Next = ZKontraRole;
            try { Take(m, t, fi, PerVictim + 1, now, ref sent, ref sc); }
            finally { _z4Next = ZNone; _hScan += sv; _hQPass += qv; }
            if (sent <= 0) return;
            _z4R++;
            _z4RRiders += sent;
            if (q.CondAt < 0f) { q.CondAt = now; _z4Ev++; }
            if (!t.Active) StartEp(t, now, sent, sc, 0, d, 0);
            EnterK(t, q, now, false, "odruch: trafiony jezdziec z " + F0(d) + " m");
        }

        // ------------------------------------------------------------ dla MarchToContact (glowny watek)

        /// <summary>Pass: strzelcy aktywnych zagrozen wedlug strony ostrzeliwanej; A344-6 szyki konnicy pod capem marszu 128 w 60 m od strzelca.</summary>
        private static void ShooterPosPass(float now)
        {
            SPos[0].Clear(); SPos[1].Clear();
            TrotSeen.Clear();
            SPosNow.Clear();
            if (!_zOn) { SPosPrev.Clear(); return; }
            for (int i = 0; i < ThreatList.Count; i++)
            {
                var t = ThreatList[i];
                var s = t.S;
                if (s == null || !s.IsActive() || s.MountAgent == null || t.Side < 0 || t.Side > 1) continue;
                if (!t.Active && t.Q.St == SW && now - t.LastFire > _hold) continue;
                Vec2 sp = s.Position.AsVec2;
                SPos[t.Side].Add(sp);
                SPosNow.Add(s.Index);
                if (!SPosPrev.Contains(s.Index)) continue;   // 347b A344-6: nowy strzelec - Eval szykow jeszcze go nie widzial
                var fl = HForms[t.Side];
                for (int j = 0; j < fl.Count; j++)
                {
                    var fi = fl[j];
                    if (!fi.Cav || fi.Ha || fi.Player || fi.Retreat) continue;
                    if (Math.Max(0f, sp.Distance(fi.C) - fi.R) > ZTrotNear || !TrotSeen.Add(fi.F)) continue;
                    int ck = 0;
                    try { ck = MarchToContact.CapKind(fi.F); } catch { ck = 0; }
                    if (ck == 1) _z4Trot++;
                }
            }
            TrotSeen.Clear();
            SPosPrev.Clear();
            SPosPrev.UnionWith(SPosNow);
            if (Ret344.Count > 0)
            {
                GTmp.Clear();
                foreach (var kv in Ret344) if (now >= kv.Value) GTmp.Add(kv.Key);
                for (int i = 0; i < GTmp.Count; i++) Ret344.Remove(GTmp[i]);
                GTmp.Clear();
            }
        }

        /// <summary>MarchToContact.Eval (konnica): odleglosc szyku od najblizszego aktywnego strzelca, ktory strzela w jego strone (mediana minus pol glebokosci).</summary>
        internal static float ShooterGap344(Formation f)
        {
            if (!_live || !_zOn || f == null) return float.MaxValue;
            int side = SideOf(f.Team);
            if (side < 0) return float.MaxValue;
            var ps = SPos[side];
            if (ps.Count == 0) return float.MaxValue;
            Vec2 c = f.CachedMedianPosition.AsVec2;
            if (!c.IsValid) return float.MaxValue;
            float best = float.MaxValue;
            for (int i = 0; i < ps.Count; i++)
            {
                float d = c.Distance(ps[i]);
                if (d < best) best = d;
            }
            float r = best - 0.5f * f.Depth;
            return r < 0f ? 0f : r;
        }

        /// <summary>MarchToContact.Eval: szyk w powrocie PS 344 (bez capu marszu do konca okna).</summary>
        internal static bool Return344(Formation f, float now)
        {
            if (!_live || f == null || Ret344.Count == 0) return false;
            float u;
            return Ret344.TryGetValue(f, out u) && now < u;
        }

        private static void ZLine(string txt)
        {
            if (_zLines >= ZMaxLines) return;
            _zLines++;
            Log.Info("AI bitwy (344) " + txt + ".");
        }

        // ------------------------------------------------------------ linia "AI bitwy (344): misja ..." (raz na bitwe polowa)

        private static string Med(List<float> l)
        {
            if (l.Count == 0) return "-";
            var c = new List<float>(l);
            c.Sort();
            return c[c.Count / 2].ToString("0.0", Inv);
        }

        private static string MedI(List<int> l, out int max)
        {
            max = 0;
            if (l.Count == 0) return "-";
            var c = new List<int>(l);
            c.Sort();
            max = c[c.Count - 1];
            return c[c.Count / 2].ToString(Inv);
        }

        private static void AiBattle333Write()
        {
            if (!_field) return;
            var c = Settings.Current;
            bool keyA = c != null && c.CavalryFaceShooterAtArmyEdge;
            int fbMax;
            string fbMed = MedI(FBacks, out fbMax);
            var sb = new StringBuilder(2400);
            sb.Append("AI bitwy (344): misja ").Append(_kind).Append(" | konnica wobec strzelca klucz ").Append(keyA ? "TAK" : "NIE").Append(" (jazda 204 ").Append(_hKey ? "TAK" : "NIE")
              .Append("; strefa armii strzelca: piechota +").Append(F0(ZFootAdd)).Append(" m, lucznicy +").Append(ZShotShare.ToString("0.0", Inv)).Append(" zasiegu ")
              .Append(F0(ZShotMin)).Append('-').Append(F0(ZShotMax)).Append(" m, konnica +").Append(F0(ZCavAdd)).Append(" m; front < ").Append(F0(ZBreakGap))
              .Append(" m, start >= ").Append(F0(ZStartGap)).Append(" m, oslona ").Append(F0(ZOutGap)).Append(" m; Rs ").Append(F0(ZRs)).Append(" m, Rw ").Append(F0(ZRw))
              .Append(" m, Rb ").Append(F0(ZRb)).Append(" m; pogon bez zblizania ").Append(F0(ZSlowFor)).Append(" s, kontra do ").Append(F0(ZKMax)).Append(" s, powrot ")
              .Append(F0(ZPsFor)).Append(" s bez capu; przewaga 2 x (1 + eskorta))")
              .Append(" | poscigi ").Append(_zFull).Append(" (L1 przy szybszym strzelcu ").Append(_z4L1Fast).Append(", pogonie bez szans ponad ").Append(F0(ZSlowFor))
              .Append(" s ").Append(_z4Long).Append(')')
              .Append(" | front wejsc ").Append(_z4F).Append(" (jezdzcow ").Append(_z4FRiders).Append("; krawedz strefy ").Append(_z4FEdge).Append(", szybszy strzelec ")
              .Append(_z4FFast).Append(", strona przegrywa ").Append(_z4FLose).Append("), trafienia w plecy w front/powrot ").Append(_z4BackF).Append(" (na wejscie mediana ")
              .Append(fbMed).Append(", najwiecej ").Append(fbMax).Append("), wszystkie w plecy ").Append(_z4BackAll).Append(", przodem do strzelca ").Append(_z4FaceOk)
              .Append('/').Append(_z4FaceN).Append(" probek")
              .Append(" | kontry ").Append(_z4K).Append(" (calego G ").Append(_z4KFull).Append(", odruchow ").Append(_z4R).Append(", jezdzcow odruchu ").Append(_z4RRiders)
              .Append(", jezdzcow kontry ").Append(_z4KRiders).Append("), zdarzen kontry ").Append(_z4Ev).Append(" (odpowiedzi ").Append(_z4Ans).Append(", mediana reakcji ")
              .Append(Med(KReact)).Append(" s, bez odpowiedzi ").Append(_z4Miss).Append(", bez przewagi ").Append(_z4NoAdv).Append("), wynik kontry dobry ").Append(_z4KOk)
              .Append('/').Append(_z4KEnd).Append(", kontry bez wolnych jezdzcow ").Append(_z4KEmpty)
              .Append(" | trafienia bez odpowiedzi ").Append(_z4Unans).Append(" (trafien z Rw poza oslona ").Append(_z4PendN).Append(')')
              .Append(" | powroty szykiem ").Append(_z4Ps).Append(" (kontra z powrotu ").Append(_z4PsK).Append(", front z powrotu ").Append(_z4PsF).Append("), klus przy strzelcu ")
              .Append(_z4Trot).Append(" s")
              .Append(" | front pod ostrzalem: trafien ").Append(_z4FHit).Append(", docisniec ").Append(_z4Press).Append(", odwrotow spod ognia ").Append(_z4FOut)
              .Append(", bez reakcji ").Append(_z4FUnans).Append(", powrotow przedluzonych ").Append(_z4PsExt)
              .Append(" | strzelcy przy swojej armii bez poscigu ").Append(_zNoStart).Append(", lekkie odpedzenia L1 ").Append(_zLight).Append(", krawedzi strefy na strzelca najwiecej ")
              .Append(_zShooterBreakMax).Append(" (razem ").Append(_zBreaks).Append("), petla przynety ").Append(_zLoops)
              .Append(" | jezdzcy zawroceni w strefie ").Append(_zRiderIn).Append(", nie wyslani przy strefie ").Append(Nw[NwZone]).Append(", punktow przycietych do strefy ")
              .Append(_zClamp).Append(", szykow w strefie najwiecej ").Append(_zZoneMax)
              .Append(" | ");
            Pendulum333.Append(sb);
            sb.Append(" | test bez przewijania: odrzucone wlaczenia ").Append(NoFastForward344.Blocked);
            sb.Append(" | potkniecia 344 ").Append(_zStum + Pendulum333.Stumbles).Append('.');
            Log.Info(sb.ToString());
        }
    }
}
