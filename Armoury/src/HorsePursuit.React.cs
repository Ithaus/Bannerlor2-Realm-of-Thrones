using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 283 "Nekanie z tylu i z boku" (27.10, Jeff ok. 21:40 po bitwie z WGRANIEM 130: "wjechalem za plecy lucznikom wroga ... zaczalem z konia strzelac do piechoty
    /// z 3 m, po pierwsze lucznicy by sie zatrzymali zaczeli walic do mnie ... po drugie ruszyla by kawaleria na mnie, czesciowo ruszyla ale nie cala ... armia powinna
    /// sie zatrzymac ... a jesli jestem pieszo dodatkowo piechota nie pojdzie do boju jak im na ogonie siedzi koles"; 21:55: "jezdzcow nie odwoluje sie jesli jest zwiazana
    /// walka ... chyba ze strzelcy sa wazniejsi do ochrony lub w strzelcach jest dowodca ... party leader jest chroniony"; 22:00: "nie umiesz sam wydedukowac w logice AI
    /// kazdego mozliwego zachowania w sredniowiecznej bitwie"). Klucze FormationsAnswerHarassers (macierz) i ArmyHaltsForHarassers (R4/R5 postoj).
    /// Przyczyna (log Jeffa 27.10 21:25-21:38, linia 204): 52 wykrycia, 2 epizody, 3 jezdzcow; lucznicy na cel 32, cel zdjety 19 razy przez FriendsBlock (ktokolwiek ze
    /// swoich w 8 m od strzelca - Jeff jechal 3 m za ich szeregiem); 204 brala tylko konnych strzelcow (pieszo nic), cios mieczem nie byl wykryciem, epizod tylko z jezdzcami,
    /// szyk w marszu szedl dalej (P1 MarchToContact).
    ///
    /// MACIERZ (projekt 283 - zagrozenie x szyk x stan -> odpowiedz):
    ///  - wykrycie tylko po szkodzie (pocisk 204: trafienie / pudlo przy ludziach / przelot / cel AI; cios wrecz w szyk, ktory nie jest w zwarciu - kolejka z OnHit);
    ///    rodzaj na zywo: konny strzelec, pieszy strzelec, konny wrecz, pieszy wrecz (ostatnie zdarzenie: pocisk czy cios); "samotny" = bez nietknietego szyku swojej
    ///    strony w 80 m (IntactRadius / IntactMin 148); pieszy strzelec i nekajacy wrecz AI tylko samotny (w swoim szyku to zwykla bitwa - RBM / 141 / 146), gracz zawsze;
    ///  - R1 lucznicy strzelaja, R1b czysta linia (jeden promien gry RayCastForClosestAgent: oko -> piers celu, grubosc = promien kapsuly ciala + odleglosc x AiShooterError
    ///    gry; swoj blizej niz cel = zablokowany - krok w bok o 1-2 odstepy szyku albo 2 odstepy w tyl, pierwszy punkt na siatce z czysta linia; przypisany lucznik co pass
    ///    wedlug stanu silnika GetLastTargetVisibilityState == FriendInWay), w pojedynku strzeleckim tylko polowa blizsza nekajacemu (RBM 0.5), lucznik w 6 m od nekajacego
    ///    wrecz z bronia boczna - gra (walczy), bez niej krok w tyl na 12 m;
    ///  - R2 oddzial piechoty (tylko pieszy nekajacy albo konny w walce wrecz w 30 m): najpierw szyk wodza (R6), szyk ofiary, potem nie zwiazane szyki piesze w 50 m;
    ///    ilu = PerVictim x max(1, tier nekajacego / tier szyku) + padlo od niego (x2 przy wodzu), w szyku zostaje max(8, polowa); smycz 50 m (InfRearm), 60 s, przerwa 30 s;
    ///  - R3 jazda (204 + 283): samotny konny nekajacy, ktory kogos powalil, dal 3 szkody w 5 s albo uderzyl w lucznikow - od razu L3 (cala wolna jazda w 250 m); pieszy
    ///    nekajacy - oddzial na miare R2, przy 3 szkodach w 5 s albo powalonym L3; szyk konnicy w zwarciu (wrog 25 m - ContactGap) zostaje, chyba ze wodz (R6), szyk ofiary
    ///    ma wieksza FormationPower niz wrog tego szyku albo trzyma polowe strzelcow strony; szyk w oslonie skrzydla (ProtectFlank) zostaje, gdy nietkniety szyk konnicy
    ///    wroga stoi w 110 m; jezdziec z wrogiem w 6 m zawsze zostaje (204);
    ///  - R4 szyk ofiary staje, R5 cala armia (szyki AI druzyny w marszu, bez konnych lucznikow) - przejecie wag zachowan (BehaviorStop gry: stoj, tarcze, ogien wedlug
    ///    uznania), raz na nekajacego, najwyzej 60 s, tylko gdy wrog dalej niz 110 m (HuntClose), strona nie lamana, bez S2 (141) i S4 (Doctrine);
    ///  - R6 wodz / dowodca szyku / pan druzyny trafiony, celem albo w 10 m (149) - R1 + R3 pelna sila (takze z zwarcia, bez zwiazanych), R2 z jego szyku x2;
    ///  - R7 chorazy trafiony - R2 z jego szyku (zwiazanych nie ruszamy; 158 pilnuje sztandaru po upadku);
    ///  - R8 powrot: cel padl, odpedzony, smycz, czas, konie, panika, odwrot, gracz przejal, klucz, koniec bitwy - cel automatyczny, pozycja zdjeta, przerwa 30 s.
    /// Pominiete (z powodem): T8 szarza szyku na skrzydlo - RBM (prowadzenie 50 jezdzcow po jednym walczyloby z rozkazami szyku RBM co tick); tabor - pole bitwy nie ma
    /// taboru; wykrycie samotnego zanim cos zrobi - przeglad wszystkich ludzi, a ranni 143, uciekajacy i bohaterowie 149 sa "samotni" i niegrozni; oblezenia i morze.
    /// Watki: OnHit (dowolny watek) - tylko kolejka pod HLock i Interlocked, tablica _quiet pisana w passie; reszta glowny watek (pass co 1 s po Pass 148, krok 0.25 s).
    /// Nic do zapisu - stan jednej misji. Linia "Nekanie (283): misja ..." raz na bitwe polowa, epizody "Nekanie (283) start / koniec" (najwyzej 12).
    /// </summary>
    internal static partial class HorsePursuit
    {
        // ------------------------------------------------------------ stale 283 (zrodla - opis klasy)
        private const byte RoleFoot = 3;
        internal const float LeaderNear = 10f;                  // 149: bohater / wodz w 10 m
        private const int RaysPerPass = ShieldsUp.QueriesPerPass; // 140: limit zapytan na pass
        private const int RLinesMax = 12;
        private const int QuietTeams = 16, QuietForms = 16;
        private const int KShootM = 0, KShootF = 1, KMeleeM = 2, KMeleeF = 3, KKinds = 4;
        private static readonly string[] KName = { "konny strzelec", "pieszy strzelec", "konny wrecz", "pieszy wrecz" };
        // wynik epizodu 283
        private const int ROResp = 0, ROOthers = 1, ROAway = 2, ROLeash = 3, ROEnded = 4, ROOther = 5, ROKinds = 6;
        private static readonly string[] ROName = { "padl od odpowiedzi", "padl od innych", "odpedzony", "uszedl", "koniec bitwy", "inne" };
        // piechota nie wyslana (rozni ludzie / szyki)
        private const int FwEngaged = 0, FwHalf = 1, FwHero = 2, FwBanner = 3, FwBusy = 4, FwCool = 5, FwMelee = 6, FwIntact = 7, FwQuery = 8, FwPanic = 9, FwKinds = 10;

        // ------------------------------------------------------------ stan misji 283 (glowny watek, poza _rLive / _quiet / kolejka)
        private static bool _rKey, _rHaltKey;
        private static bool _rMarchOn;   // 291: ArmyMarchesPastHarassers (z 283) - bez R5, R4 tylko strzelcy, smycz R2 od miejsca wyslania
        private static int _rLive;                                   // OnHit: 1 = klucz 283 i po rozstawieniu (Volatile)
        private static readonly bool[] _quiet = new bool[QuietTeams * QuietForms];   // szyk nie w zwarciu (pisze pass, czyta OnHit)
        private static readonly List<Agent> MeleeQA = new List<Agent>(), MeleeQV = new List<Agent>();   // pod HLock
        private static readonly List<Agent> RQA = new List<Agent>(), RQV = new List<Agent>();
        private static readonly Dictionary<Formation, int> FormFoot = new Dictionary<Formation, int>();
        private static readonly List<KeyValuePair<float, int>> RFormOrder = new List<KeyValuePair<float, int>>();
        private static readonly HashSet<Formation> RFwSeenF = new HashSet<Formation>(), RKeptF = new HashSet<Formation>(), RPfF = new HashSet<Formation>();
        private static readonly HashSet<long> RFwSeen = new HashSet<long>();
        private static int _rRays, _rLines;
        private static long _rTicks, _rMaxTicks;
        private static int _rPassN;

        // ------------------------------------------------------------ liczniki misji 283
        private static readonly int[] RDet = new int[KKinds], ROutN = new int[ROKinds], FEnd = new int[UKinds], Fw = new int[FwKinds];
        private static readonly HashSet<int> RPlayerT = new HashSet<int>(), RAiT = new HashSet<int>(), RLoneT = new HashSet<int>(), RArchT = new HashSet<int>(),
                                             RLeaderT = new HashSet<int>(), RBannerT = new HashSet<int>(), RFoot = new HashSet<int>(), RCavFree = new HashSet<int>(),
                                             RCavSent = new HashSet<int>(), RArch = new HashSet<int>();
        private static readonly HashSet<Formation> RFootForms = new HashSet<Formation>();
        private static int _rQ, _rNotLone, _rFrontSkip, _rEp, _rFootShots, _rRayN, _rRayClear, _rRayFriend, _rRayMax, _rRayCap, _rEngine, _rStepTry, _rStepOk,
                           _rSide, _rBack, _rHalf, _rKeptMen, _rPulled, _rPullLeader, _rPullValue, _rPullRanged, _rPfMen, _rFootLost, _rFromPlayer, _rLeft, _rStum,
                           _rFootTake, _rLeaderNear;
        // recenzja 283
        private static int _rLeaderAim, _rFootCoolN, _rRespNear, _rRespDrop;
        private static readonly HashSet<int> RUnreachT = new HashSet<int>();
        private static float _rFootFar;
        private static int _rQOver, _rFfArch;   // Interlocked (OnHit)
        // poprawka 132b: R2 straz strzelcow - oddzial z szyku piechoty dalej niz smycz (do 110 m), gdy nekajacy bije strzelcow / wodza / chorazego
        private static int _rGuardN;
        private static readonly HashSet<Formation> RGuardForms = new HashSet<Formation>();

        // ------------------------------------------------------------ poczatek / koniec misji

        private static void ReactBegin(Settings c)
        {
            _rKey = c != null && c.FormationsAnswerHarassers;
            _rHaltKey = _rKey && c.ArmyHaltsForHarassers;
            _rMarchOn = _rKey && c.ArmyMarchesPastHarassers;   // 291
            Volatile.Write(ref _rLive, 0);
        }

        private static void ReactClear()
        {
            Volatile.Write(ref _rLive, 0);
            for (int i = 0; i < _quiet.Length; i++) _quiet[i] = false;
            lock (HLock) { MeleeQA.Clear(); MeleeQV.Clear(); }
            RQA.Clear(); RQV.Clear(); FormFoot.Clear(); RFormOrder.Clear(); RFwSeenF.Clear(); RKeptF.Clear(); RPfF.Clear(); RFwSeen.Clear();
            _rRays = _rLines = 0;
            _rTicks = _rMaxTicks = 0L;
            _rPassN = 0;
            Array.Clear(RDet, 0, RDet.Length); Array.Clear(ROutN, 0, ROutN.Length); Array.Clear(FEnd, 0, FEnd.Length); Array.Clear(Fw, 0, Fw.Length);
            RPlayerT.Clear(); RAiT.Clear(); RLoneT.Clear(); RArchT.Clear(); RLeaderT.Clear(); RBannerT.Clear(); RFoot.Clear(); RCavFree.Clear(); RCavSent.Clear(); RArch.Clear();
            RFootForms.Clear();
            _rQ = _rNotLone = _rFrontSkip = _rEp = _rFootShots = _rRayN = _rRayClear = _rRayFriend = _rRayMax = _rRayCap = _rEngine = _rStepTry = _rStepOk = 0;
            _rSide = _rBack = _rHalf = _rKeptMen = _rPulled = _rPullLeader = _rPullValue = _rPullRanged = _rPfMen = _rFootLost = _rFromPlayer = _rLeft = _rStum = 0;
            _rFootTake = _rLeaderNear = 0;
            _rLeaderAim = _rFootCoolN = _rRespNear = _rRespDrop = 0;
            RUnreachT.Clear();
            _rFootFar = 0f;
            Interlocked.Exchange(ref _rQOver, 0); Interlocked.Exchange(ref _rFfArch, 0);
            HaltClear();
            NearClear();                 // poprawka 132b
            _rGuardN = 0; RGuardForms.Clear();
        }

        private static void RStumble(string where, Exception e)
        {
            _rStum++;
            Stumble(where, e);
        }

        // ------------------------------------------------------------ szyki nie w zwarciu (pass; OnHit czyta)

        private static int Slot(Team t, Formation f)
        {
            if (t == null || f == null) return -1;
            int ti = t.TeamIndex, fi = (int)f.FormationIndex;
            if (ti < 0 || ti >= QuietTeams || fi < 0 || fi >= QuietForms) return -1;
            return ti * QuietForms + fi;
        }

        /// <summary>BuildHForms (283): szyk nie w zwarciu = cios w niego to nekanie, nie front. Pisze glowny watek (bez zerowania calej tablicy co pass -
        /// OnHit z innego watku nie widzi chwilowego "w zwarciu"; szyk, ktorego juz nie ma, nie ma tez ofiar).</summary>
        private static void QuietSet(Formation f, bool engaged)
        {
            int sl = Slot(f.Team, f);
            if (sl >= 0) Volatile.Write(ref _quiet[sl], !engaged);
        }

        // ------------------------------------------------------------ wykrycie wrecz (OnHit - dowolny watek; kolejka pod HLock)

        /// <summary>Cios wrecz czlowieka strony w czlowieka drugiej strony: do kolejki, gdy bije gracz / nekajacy testu albo ofiara jest w szyku, ktory nie jest
        /// w zwarciu (front nie trafia do kolejki).</summary>
        private static void ReactOnHit(Agent att, Agent vic, in Blow blow)
        {
            if (Volatile.Read(ref _rLive) == 0 || blow.IsMissile) return;
            if (!att.IsHuman || !vic.IsHuman) return;
            var at = att.Team;
            var vt = vic.Team;
            if (!MissionTeams.Valid(at) || !MissionTeams.Valid(vt) || at.Side == vt.Side) return;
            if (!att.IsMainAgent && !ReferenceEquals(att, _tA))
            {
                int sl = Slot(vt, vic.Formation);
                if (sl < 0 || !Volatile.Read(ref _quiet[sl])) return;
            }
            lock (HLock)
            {
                if (MeleeQA.Count < HuntBlowCap) { MeleeQA.Add(att); MeleeQV.Add(vic); return; }
            }
            Interlocked.Increment(ref _rQOver);
        }

        /// <summary>Pass: kolejka ciosow wrecz na glownym watku - zagrozenie (samotny albo gracz), okno szkod 5 s, ofiara (lucznicy, chorazy, wodz).</summary>
        private static void DrainMelee(Mission m, float now)
        {
            int n;
            lock (HLock) { n = MeleeQA.Count; }
            if (n == 0) return;
            lock (HLock) { RQA.AddRange(MeleeQA); RQV.AddRange(MeleeQV); MeleeQA.Clear(); MeleeQV.Clear(); }
            for (int i = 0; i < RQA.Count; i++)
            {
                try { MeleeEvent(RQA[i], RQV[i], now); }
                catch (Exception e) { RStumble("HorsePursuit.NekanieWrecz", e); }
            }
            RQA.Clear(); RQV.Clear();
        }

        private static void MeleeEvent(Agent a, Agent v, float now)
        {
            if (a == null || v == null || !a.IsActive() || !a.IsHuman) return;
            int ss = SideOf(a.Team), vs = SideOf(v.Team);
            if (ss < 0 || vs < 0 || ss == vs) return;
            _rQ++;
            // recenzja 283 (5): filtr frontu dla kazdego ciosu, takze przy istniejacym zagrozeniu (gracz po jednym strzale w zwarciu frontu nie podnosi poziomu,
            // nie odswieza ciszy i nie zmienia ofiary)
            bool lone = !IntactNear(ss, a.Position.AsVec2, IntactRadius);
            if (!lone)
            {
                if (!a.IsMainAgent && !ReferenceEquals(a, _tA)) { _rNotLone++; return; }   // AI w swoim szyku - zwykla walka (RBM)
                int sl = Slot(v.Team, v.Formation);
                if (sl < 0 || !_quiet[sl]) { _rFrontSkip++; return; }                         // gracz w zwarciu frontu - nie nekanie
            }
            Threat t;
            if (!Threats.TryGetValue(a, out t)) t = Notice(a, ss, vs, -1, now, false);
            t.LastMelee = now;
            t.MeleeHits++;
            Fire(t, now);
            Victim(t, v, true);
            RDet[KindNow(t)]++;
            if (ReferenceEquals(a, _tA)) { _tDet++; _tMelee++; }   // 291 (J204-1): wykrycie ciosem osobno od pociskow
        }

        // ------------------------------------------------------------ zagrozenie: rodzaj, ofiara, wodz, tier

        private static int KindNow(Threat t)
        {
            var s = t.S;
            bool mounted = s != null && s.MountAgent != null;
            bool melee = t.LastMelee >= 0f && t.LastMelee >= t.LastShot;
            return melee ? (mounted ? KMeleeM : KMeleeF) : (mounted ? KShootM : KShootF);
        }

        private static bool MeleeKind(Threat t) { return t.Kind == KMeleeM || t.Kind == KMeleeF; }

        /// <summary>Ofiara szkody (trafiony, cel AI, cios): szyk zagrozony, lucznicy, chorazy (R7), wodz / dowodca / pan druzyny (R6). harm - trafienie albo cios;
        /// recenzja 283 (2): sam cel AI (bez trafienia) nie robi z zagrozenia "wodz" (R6).</summary>
        private static void Victim(Threat t, Agent v, bool harm)
        {
            if (v == null || !_rKey) return;
            var f = v.Formation;
            if (f != null && f.CountOfUnits > 0) t.VicF = f;
            if (!t.OnArchers)
            {
                var q = f != null ? f.QuerySystem : null;
                if (v.IsRangedCached || (q != null && (q.IsRangedFormation || q.IsRangedCavalryFormation))) { t.OnArchers = true; RArchT.Add(t.S.Index); }
            }
            if (!t.OnBanner && Banner(v)) { t.OnBanner = true; RBannerT.Add(t.S.Index); }
            if (!t.Leader && IsLeader(v))
            {
                if (harm) MarkLeader(t, v);
                else _rLeaderAim++;
            }
        }

        /// <summary>Recenzja 283 (5): szyk nie w zwarciu (tablica _quiet z passu).</summary>
        private static bool Quiet(Formation f)
        {
            int sl = f != null ? Slot(f.Team, f) : -1;
            return sl >= 0 && _quiet[sl];
        }

        /// <summary>Wodz druzyny, dowodca szyku albo pan druzyny kampanii (Hero.PartyBelongedTo.LeaderHero).</summary>
        private static bool IsLeader(Agent v)
        {
            if (v == null) return false;
            var tm = v.Team;
            if (tm != null && ReferenceEquals(tm.GeneralAgent, v)) return true;
            var f = v.Formation;
            if (f != null && ReferenceEquals(f.Captain, v)) return true;
            if (!v.IsHero) return false;
            try
            {
                var co = v.Character as CharacterObject;
                var h = co != null ? co.HeroObject : null;
                var p = h != null ? h.PartyBelongedTo : null;
                return p != null && ReferenceEquals(p.LeaderHero, h);
            }
            catch { return false; }
        }

        private static void MarkLeader(Threat t, Agent v)
        {
            t.Leader = true;
            t.LeaderF = v != null ? v.Formation : null;
            RLeaderT.Add(t.S.Index);
        }

        /// <summary>Tier: bohater i gracz - VeteranAi.SkillTier z najlepszej umiejetnosci broni (m113 HeroTierFromSkill), reszta CharacterObject.Tier.</summary>
        private static int TierOf(Agent a)
        {
            if (a == null) return 1;
            try
            {
                var ch = a.Character;
                if (ch == null) return 1;
                if (a.IsHero || a.IsMainAgent)
                {
                    int best = 0;
                    best = Math.Max(best, ch.GetSkillValue(DefaultSkills.OneHanded));
                    best = Math.Max(best, ch.GetSkillValue(DefaultSkills.TwoHanded));
                    best = Math.Max(best, ch.GetSkillValue(DefaultSkills.Polearm));
                    best = Math.Max(best, ch.GetSkillValue(DefaultSkills.Bow));
                    best = Math.Max(best, ch.GetSkillValue(DefaultSkills.Crossbow));
                    best = Math.Max(best, ch.GetSkillValue(DefaultSkills.Throwing));
                    return VeteranAi.SkillTier(best);
                }
                var co = ch as CharacterObject;
                return co != null ? Math.Max(1, co.Tier) : 1;
            }
            catch { return 1; }
        }

        /// <summary>Tier szyku: pierwszy czlowiek szyku, ktory nie jest bohaterem (bez niego 1).</summary>
        private static int FormTier(Formation f)
        {
            // recenzja 283 (M2): bohater (dowodca szyku) na czele listy nie zawyza tieru - pierwszy, ktory nie jest bohaterem (bohaterow kilku, petla krotka)
            try
            {
                int n = f.CountOfUnits;
                for (int i = 0; i < n; i++)
                {
                    var u = f.GetUnitWithIndex(i);
                    if (u != null && !u.IsHero) return TierOf(u);
                }
            }
            catch { }
            return 1;
        }

        /// <summary>Ilu na nekajacego: PerVictim x max(1, tier nekajacego / tier odpowiadajacych) + padlo od niego (jeden za jednego 148/204).</summary>
        private static int RWant(Threat t, int rt, int down)
        {
            int ratio = (int)Math.Round((double)Math.Max(1, t.Tier) / Math.Max(1, rt), MidpointRounding.AwayFromZero);
            return PerVictim * Math.Max(1, ratio) + down;
        }

        /// <summary>Pass (ThreatPass): rodzaj na zywo, samotny, wodz w 10 m (wodz druzyny i dowodcy szykow strony).</summary>
        private static void ReactRefresh(Mission m, Threat t, Vec2 sp, bool lone)
        {
            t.Kind = KindNow(t);
            t.Lone = lone;
            var s = t.S;
            if (lone) RLoneT.Add(s.Index);
            if (s.IsMainAgent) RPlayerT.Add(s.Index); else RAiT.Add(s.Index);
            if (t.Leader) return;
            if (!lone && !Quiet(t.VicF)) return;   // recenzja 283 (5): kapitan w 10 m w zwarciu frontu - zwykla bitwa, nie ochrona wodza
            var fl = HForms[t.Side];
            for (int i = 0; i < fl.Count; i++)
            {
                var c = fl[i].Cap;
                if (c != null && c.IsActive() && c.Position.AsVec2.Distance(sp) <= LeaderNear) { MarkLeader(t, c); _rLeaderNear++; return; }
            }
            var teams = m.Teams;
            int nt = teams != null ? teams.Count : 0;
            for (int i = 0; i < nt; i++)
            {
                Team tm;
                try { tm = teams[i]; } catch { continue; }
                if (SideOf(tm) != t.Side) continue;
                var g = tm.GeneralAgent;
                if (g != null && g.IsActive() && g.Position.AsVec2.Distance(sp) <= LeaderNear) { MarkLeader(t, g); _rLeaderNear++; return; }
            }
        }

        // ------------------------------------------------------------ R3: jazda w zwarciu, oslona skrzydla

        /// <summary>Szyk konnicy w zwarciu: 0 = zostaje, 1 wodz, 2 wartosc szyku ofiary (FormationPower gry) wieksza niz wroga tego szyku, 3 szyk ofiary trzyma
        /// polowe strzelcow strony.</summary>
        private static int PullWhy(Threat t, FI fi)
        {
            if (t.Leader) return 1;
            var vf = t.VicF;
            if (vf == null || vf.CountOfUnits <= 0) return 0;
            try
            {
                var eq = fi.F.CachedClosestEnemyFormation;
                float ep = eq != null ? eq.FormationPower : 0f;
                var vq = vf.QuerySystem;
                if (vq != null && vq.FormationPower > ep) return 2;
                if (vq != null && (vq.IsRangedFormation || vq.IsRangedCavalryFormation))
                {
                    int all = 0;
                    var fl = HForms[t.Side];
                    for (int i = 0; i < fl.Count; i++) if (fl[i].Rng || fl[i].Ha) all += fl[i].N;
                    if (all > 0 && vf.CountOfUnits * 2 >= all) return 3;
                }
            }
            catch { }
            return 0;
        }

        /// <summary>Nietkniety szyk konnicy strony nekajacego w 110 m (HuntClose) od szyku w oslonie skrzydla - oslona trzyma.</summary>
        private static bool EnemyCavNear(int s, Vec2 c, float r)
        {
            if (s < 0) return false;
            var fl = HForms[s];
            for (int i = 0; i < fl.Count; i++)
            {
                var fi = fl[i];
                if (!fi.Cav || fi.N < IntactMin) continue;
                if (c.Distance(fi.C) - fi.R - r <= HuntClose) return true;
            }
            return false;
        }

        /// <summary>Take (283): szyk w zwarciu albo w oslonie przed jazda - false = nie bierzemy (liczniki raz na szyk).</summary>
        private static bool CavAllowed(Threat t, FI fi, out int pull)
        {
            pull = 0;
            if (!_rKey) return true;
            if (fi.Engaged)
            {
                pull = PullWhy(t, fi);
                if (pull == 0)
                {
                    if (RKeptF.Add(fi.F)) _rKeptMen += fi.N;
                    return false;
                }
                return true;
            }
            if (fi.Pf && !t.Leader && EnemyCavNear(t.SSide, fi.C, fi.R))
            {
                if (RPfF.Add(fi.F)) _rPfMen += fi.N;
                return false;
            }
            return true;
        }

        private static void NotePull(int pull, int n)
        {
            if (pull <= 0 || n <= 0) return;
            _rPulled += n;
            if (pull == 1) _rPullLeader += n; else if (pull == 2) _rPullValue += n; else _rPullRanged += n;
        }

        // ------------------------------------------------------------ R2: oddzial piechoty

        /// <summary>Oddzial piechoty na pieszego nekajacego (albo konnego w walce wrecz przy piechocie, HuntScreen). Zwraca liczbe nowych.</summary>
        private static int FootPass(Mission m, Threat t, Vec2 sp, float now, int down)
        {
            if (t.Group || t.S == null) return 0;
            if (now < t.FootCool) return 0;   // recenzja 283 (3): piechota wrocila ze smyczy / czasu / konny uszedl - przerwa 30 s na tego nekajacego
            // nekajacy przy swoim nietknietym szyku (15 m, CounterFootGap) - piechota nie biegnie w jego szyk (i tak wrocilaby "przy szyku nekajacego")
            if (IntactNear(t.SSide, sp, CounterFootGap)) return 0;
            bool mounted = t.S.MountAgent != null;
            var fl = HForms[t.Side];
            if (mounted)
            {
                if (!MeleeKind(t)) return 0;   // konny strzelec - piechota konia nie dogoni
                bool near = false;
                for (int i = 0; i < fl.Count && !near; i++) if (fl[i].Foot && !fl[i].Rng && Math.Max(0f, sp.Distance(fl[i].C) - fl[i].R) <= HuntScreen) near = true;
                if (!near) return 0;
            }
            RFormOrder.Clear();
            // poprawka 132b (autotest 132b C: wojownik wrecz w lucznikach wroga, "piechoty 0 (wolnej 30)", a w linii bitwy zadnego "nie wyslani" - piechota wroga
            // szla do zwarcia z armia gracza dalej niz smycz 50 m od nekajacego i odpadala bez licznika): straz strzelcow - gdy nekajacy bije strzelcow, wodza
            // albo chorazego i stoi w smyczy od ich szyku, oddzial moze przyjsc z szyku piechoty do 110 m (HuntClose - zasieg odpowiedzi 204) od nekajacego,
            // nie w zwarciu; smycz takiego czlowieka - od drogi miedzy jego szykiem a szykiem strzezonym (FootLeash), nie od jego szyku
            Formation guard = null, gf = null;
            if (t.Leader && t.LeaderF != null && t.LeaderF.CountOfUnits > 0) gf = t.LeaderF;
            else if (t.VicF != null && t.VicF.CountOfUnits > 0 && (t.OnBanner || RangedF(t.VicF))) gf = t.VicF;
            if (gf != null && sp.Distance(FootHome(gf)) <= MarchToContact.InfRearm) guard = gf;
            for (int i = 0; i < fl.Count; i++)
            {
                var fi = fl[i];
                if (!fi.Foot || fi.Rng || fi.Cav || fi.Retreat) continue;
                bool lead = t.Leader && ReferenceEquals(fi.F, t.LeaderF);
                bool own = lead || ReferenceEquals(fi.F, t.VicF);
                float dm = sp.Distance(fi.C);
                float e = Math.Max(0f, dm - fi.R);
                bool far = !own && e > MarchToContact.InfRearm;
                if (far && (guard == null || e > HuntClose)) continue;
                if (fi.Player) continue;   // szyk gracza - nigdy
                // recenzja 283 (3): nekajacy poza smycza 50 m od srodka szyku (InfRearm, smycz HunterCheck) - nieosiagalny, takze dla szyku ofiary i wodza (R1, jazda)
                if (!far && dm > MarchToContact.InfRearm) { RUnreachT.Add(t.S.Index); continue; }
                if (fi.Engaged && !lead)
                {
                    if (RFwSeenF.Add(fi.F)) Fw[FwEngaged] += fi.N;
                    continue;
                }
                RFormOrder.Add(new KeyValuePair<float, int>(lead ? -2f : (own ? -1f : e), far ? (i | GuardBit) : i));
            }
            if (RFormOrder.Count == 0) return 0;
            if (RFormOrder.Count > 1) RFormOrder.Sort((x, y) => x.Key.CompareTo(y.Key));
            int sent = 0;
            for (int k = 0; k < RFormOrder.Count; k++)
            {
                int idx = RFormOrder[k].Value;
                var fi = fl[idx & ~GuardBit];
                Formation home = (idx & GuardBit) != 0 ? guard : null;
                int want = RWant(t, FormTier(fi.F), down) * (t.Leader ? 2 : 1);
                int need = want - t.Foot;
                if (need <= 0) break;
                int keep = Math.Max(IntactMin, (fi.N + 1) / 2);   // RBM FormationFightingInMelee 0.5, nie mniej niz nietkniety szyk 148
                int already;
                FormFoot.TryGetValue(fi.F, out already);
                int room = fi.N - keep - already;
                if (room <= 0)
                {
                    if (RFwSeen.Add(((long)fi.F.GetHashCode() << 8) | FwHalf)) Fw[FwHalf]++;
                    continue;
                }
                sent += TakeFoot(m, t, fi, Math.Min(need, room), now, home);
            }
            return sent;
        }

        private const int GuardBit = 1 << 20;   // poprawka 132b: indeks szyku strazy w RFormOrder

        private static bool RangedF(Formation f)
        {
            try { var q = f.QuerySystem; return q != null && (q.IsRangedFormation || q.IsRangedCavalryFormation); } catch { return false; }
        }

        /// <summary>Poprawka 132b: smycz piechoty 283 - od srodka jej szyku, a u strazy strzelcow od odcinka miedzy jej szykiem i szykiem strzezonym
        /// (biegnie ta droga; wraca, gdy odbiegnie od niej dalej niz InfRearm).</summary>
        private static float FootLeash(Hunter h, Vec2 p)
        {
            if (h.Home == null && h.Anchor.IsValid) return p.Distance(h.Anchor);   // 291: od miejsca wyslania
            Vec2 a = FootHome(h.F);
            var g = h.Home;
            if (g == null || g.CountOfUnits <= 0) return p.Distance(a);
            Vec2 b = FootHome(g);
            Vec2 ab = b - a;
            float l2 = ab.LengthSquared;
            float u = l2 > 0.01f ? Vec2.DotProduct(p - a, ab) / l2 : 0f;
            if (u < 0f) u = 0f; else if (u > 1f) u = 1f;
            return p.Distance(a + ab * u);
        }

        private static void NoteFw(Agent a, int why)
        {
            if (RFwSeen.Add(((long)a.Index << 8) | (long)why)) Fw[why]++;
        }

        /// <summary>-1 = moze (zwarcie sprawdza TakeFoot), -2 = pominiety bez licznika, inaczej Fw*.</summary>
        private static int EligFoot(Agent a, Threat t, float now)
        {
            if (a == null || !a.IsActive() || !a.IsHuman || Hunters.ContainsKey(a)) return -2;
            if (a.IsMainAgent || !a.IsAIControlled || a.MountAgent != null || a.IsRangedCached) return -2;   // lucznicy strzelaja (R1)
            if (a.IsHero) return FwHero;
            if (Undead.Character(a.Character)) return -2;
            if (Banner(a)) return FwBanner;
            if (a.IsRunningAway || SurvivalMode.InPanic(a) || Withdrawing(a)) return FwPanic;
            float cu;
            if (HuntCool.TryGetValue(a, out cu) && now < cu) return FwCool;
            if (!a.CanBeAssignedForScriptedMovement()) return FwBusy;
            float mu;
            if (MeleeUntil.TryGetValue(a, out mu) && now < mu) return FwMelee;
            if (IntactNear(t.SSide, a.Position.AsVec2, CounterFootGap)) return FwIntact;
            return -1;
        }

        private static int TakeFoot(Mission m, Threat t, FI fi, int cap, float now, Formation home = null)
        {
            int left = _hScanCap - _hScan;
            if (left <= 0) { _hScanCut++; return 0; }
            HUnits.Clear();
            fi.F.ApplyActionOnEachUnit(HCollect);
            HCand.Clear();
            Vec2 sp = t.S.Position.AsVec2;
            int nu = HUnits.Count, off = nu > left ? _hScanOff % nu : 0, used = 0;
            for (int k = 0; k < nu; k++)
            {
                int u = off + k;
                if (u >= nu) u -= nu;
                var a = HUnits[u];
                int why = EligFoot(a, t, now);
                if (why == -2) continue;
                if (used >= left) { _hScanCut++; _hScanOff += k; break; }
                used++;
                if (why >= 0) { NoteFw(a, why); continue; }
                HCand.Add(new KeyValuePair<float, Agent>(a.Position.AsVec2.Distance(sp), a));
            }
            _hScan += used;
            HUnits.Clear();
            if (HCand.Count > 1) HCand.Sort((x, y) => x.Key.CompareTo(y.Key));
            int n = 0;
            for (int i = 0; i < HCand.Count && n < cap; i++)
            {
                var a = HCand[i].Value;
                if (_hQPass >= ShieldsUp.QueriesPerPass)
                {
                    for (int j = i; j < HCand.Count; j++) NoteFw(HCand[j].Value, FwQuery);
                    break;
                }
                _hQPass++;
                if (EnemyNear(m, a, t.S))
                {
                    MeleeUntil[a] = now + ShieldsUp.Window;   // walczy z kims innym - zostaje (6 m, RaiseNear 140)
                    NoteFw(a, FwMelee);
                    continue;
                }
                var hf = StartHunter(t, a, fi, RoleFoot, 0f, now);
                if (home != null) { hf.Home = home; _rGuardN++; RGuardForms.Add(fi.F); }   // poprawka 132b: straz strzelcow
                // 291: szyk nie staje (idzie dalej z armia) - oddzial trzyma sie miejsca, z ktorego wyszedl, nie uciekajacej mediany szyku (inaczej smycz 50 m
                // sciagalaby go po ok. 30 s marszu, przerwa 30 s i znowu wypad - "raz ida do mnie raz do nich")
                else if (_rMarchOn) { hf.Anchor = FootHome(fi.F); _l291Anchor++; }
                n++;
            }
            HCand.Clear();
            if (n > 0) _rFootTake++;
            return n;
        }

        /// <summary>Miejsce szyku dla smyczy piechoty i krokow lucznikow: mediana szyku (w szyku zostaje co najmniej polowa).</summary>
        private static Vec2 FootHome(Formation f)
        {
            var mp = f.CachedMedianPosition;
            return mp.IsValid ? mp.AsVec2 : f.CachedAveragePosition;
        }

        // ------------------------------------------------------------ linie epizodu 283

        private static string Rows(Threat t)
        {
            var sb = new StringBuilder(24);
            if (t.Ranged > 0) sb.Append("R1 ");
            if (t.Foot > 0) sb.Append("R2 ");
            if (t.Melee > 0) sb.Append("R3 ");
            if (t.Halt4 > 0) sb.Append("R4 ");
            if (t.Halt5 > 0) sb.Append("R5 ");
            if (t.Leader) sb.Append("R6 ");
            if (t.OnBanner) sb.Append("R7 ");
            if (sb.Length == 0) return "-";
            sb.Length--;
            return sb.ToString();
        }

        private static void ReactStartLine(Threat t, float dist)
        {
            _rEp++;
            if (_rLines >= RLinesMax) return;
            _rLines++;
            var s = t.S;
            Log.Info("Nekanie (283) start: nekajacy " + Who(s) + " (" + (s.IsMainAgent ? "gracz" : "AI") + ", " + (s.MountAgent != null ? "konno" : "pieszo") + ", " + Weapon(s)
                     + ") | rodzaj " + KName[t.Kind] + " | wiersz " + Rows(t) + " | samotny " + (t.Lone ? "TAK" : "NIE") + " | wodz " + (t.Leader ? "TAK" : "NIE")
                     + " | lucznikow " + t.Ranged + ", piechoty " + t.Foot + ", jezdzcow " + t.Melee + ", szykow stoi " + HaltsOf(t) + " | tier " + t.Tier
                     + " | odleglosc od szyku " + F0(dist) + " m");
        }

        private static void ReactEndLine(Threat t, int outcome, float dur)
        {
            int ro;
            switch (outcome)
            {
                case HDownRiders: ro = ROResp; break;
                case HDownOthers: ro = t.DownByResp ? ROResp : ROOthers; break;
                case HAway: ro = ROAway; break;
                case HLeash: ro = ROLeash; break;
                case HEnded: ro = ROEnded; break;
                default: ro = ROOther; break;
            }
            ROutN[ro]++;
            if (_rLines >= RLinesMax) return;
            _rLines++;
            Log.Info("Nekanie (283) koniec: " + ROName[ro] + " po " + dur.ToString("0.0", Inv) + " s | nekajacy " + Who(t.S) + " | rodzaj " + KName[t.Kind]
                     + " | piechoty wyslanej " + t.FootEver + ", jezdzcow " + t.MeleeEver + ", szykow stoi " + HaltsOf(t) + " | padlo od niego " + t.DownAtEnd);
        }

        // ------------------------------------------------------------ linia bitwy (raz na bitwe polowa, po linii 204)

        private static void ReactWrite()
        {
            if (!_field) return;
            int det = RDet[0] + RDet[1] + RDet[2] + RDet[3];
            int r1 = RArch.Count;
            var sb = new StringBuilder(2400);
            sb.Append("Nekanie (283): misja ").Append(_kind).Append(", klucz ").Append(_rKey ? "TAK" : "NIE").Append(", postoj ").Append(_rHaltKey ? "TAK" : "NIE")
              .Append(" (samotny nekajacy - bez nietknietego szyku swojej strony w ").Append(F0(IntactRadius)).Append(" m, gracz zawsze; lucznicy z czysta linia, krok w bok; piechota ")
              .Append(PerVictim).Append(" x tier + padlo, smycz ").Append(F0(MarchToContact.InfRearm)).Append(" m / ").Append(F0(MaxSeconds)).Append(" s; jazda w zwarciu tylko dla wodza, wartosci, strzelcow; postoj gdy wrog dalej niz ")
              .Append(F0(HuntClose)).Append(" m, najwyzej ").Append(F0(MaxSeconds)).Append(" s; wodz w ").Append(F0(LeaderNear)).Append(" m chroniony)")
              .Append(" | wykrycia ").Append(det).Append(" (konny strzelec ").Append(RDet[KShootM]).Append(", pieszy strzelec ").Append(RDet[KShootF]).Append(", konny wrecz ")
              .Append(RDet[KMeleeM]).Append(", pieszy wrecz ").Append(RDet[KMeleeF]).Append("; gracz ").Append(RPlayerT.Count).Append(", AI ").Append(RAiT.Count)
              .Append("; samotni ").Append(RLoneT.Count).Append("; na lucznikow ").Append(RArchT.Count).Append(", na wodza ").Append(RLeaderT.Count).Append(" (w ")
              .Append(F0(LeaderNear)).Append(" m ").Append(_rLeaderNear).Append("), na chorazego ").Append(RBannerT.Count).Append("; kolejka wrecz ").Append(_rQ)
              .Append(", nadmiar ").Append(Volatile.Read(ref _rQOver)).Append(", w szyku swoich ").Append(_rNotLone).Append(", front ").Append(_rFrontSkip)
              .Append("; strzalow pieszo ").Append(_rFootShots).Append(')')
              .Append(" | epizody ").Append(_rEp)
              .Append(" | odpowiedzi: R1 lucznicy na cel ").Append(r1).Append(" (strzaly w nekajacego ").Append(_hRangedShots).Append(", polowa w pojedynku ").Append(_rHalf)
              .Append(", bron boczna ").Append(_rSide).Append(", krok w tyl ").Append(_rBack).Append(')')
              .Append(", R1b promieni ").Append(_rRayN).Append(" (czysta ").Append(_rRayClear).Append(", swoi na linii ").Append(_rRayFriend).Append(", silnik FriendInWay ")
              .Append(_rEngine).Append(", krok w bok ").Append(_rStepTry).Append(" udany ").Append(_rStepOk).Append(", limit ").Append(_rRayCap).Append(')')
              .Append(", R2 piechota wyslani ").Append(RFoot.Count).Append(" (szykow ").Append(RFootForms.Count).Append(')')
              .Append(", R3 jazda wolnej ").Append(RCavFree.Count).Append(" / wyslanej ").Append(RCavSent.Count).Append(" / w zwarciu zostala ").Append(_rKeptMen)
              .Append(" / wyciagnieta ").Append(_rPulled).Append(" (wodz ").Append(_rPullLeader).Append(", wartosc szyku ").Append(_rPullValue).Append(", lucznicy strony ")
              .Append(_rPullRanged).Append(") / oslona przed jazda ").Append(_rPfMen)
              .Append(", R4 szyk staje ").Append(_lR4).Append(", R5 armia staje ").Append(_lR5).Append(" (szykow ").Append(_lR5F).Append(')')
              .Append(", R6 oslon wodza ").Append(RLeaderT.Count).Append(", R7 chorazego ").Append(RBannerT.Count)
              .Append(" | postoj: sekund ").Append(F0(_lSec)).Append(", przejete znowu ").Append(_lRetake).Append(", puszczone: koniec ").Append(LRel[LEnd]).Append(", 60 s ")
              .Append(LRel[LTime]).Append(", wrog w ").Append(F0(HuntClose)).Append(" m ").Append(LRel[LNear]).Append(", szarza ").Append(LRel[LCharge]).Append(", gracz ")
              .Append(LRel[LPlayer]).Append(", taktyka ").Append(LRel[LTactic]).Append(", odwrot ").Append(LRel[LRetreat]).Append(", S4 ").Append(LRel[LS4]).Append(", klucz ")
              .Append(LRel[LKey]).Append(", koniec bitwy ").Append(LRel[LBattle]).Append("; nie stanely: zwarcie ").Append(_lNoEng).Append(", S2 ").Append(_lNoS2)
              .Append(", strona ofiary w poscigu ").Append(_lNoBroke).Append(", bez marszu ").Append(_lNoMove)
              .Append(" | piechota wrocila: padl ").Append(FEnd[UGone]).Append(", smycz ").Append(F0(MarchToContact.InfRearm)).Append(" m ").Append(FEnd[ULeash]).Append(", czas ")
              .Append(F0(MaxSeconds)).Append(" s ").Append(FEnd[UTime]).Append(", cel padl ").Append(FEnd[UThreat]).Append(", zwarcie ").Append(FEnd[UMelee]).Append(", konny uszedl ")
              .Append(FEnd[URange]).Append(", przy szyku nekajacego ").Append(FEnd[UIntact]).Append(", panika ").Append(FEnd[UPanic]).Append(", inne ")
              .Append(FEnd[UOther] + FEnd[UPlayer] + FEnd[UKey] + FEnd[URetreat] + FEnd[UEnded])
              .Append("; nie wyslani: szyk w zwarciu ").Append(Fw[FwEngaged]).Append(", ponizej polowy ").Append(Fw[FwHalf]).Append(", bohater ").Append(Fw[FwHero])
              .Append(", chorazy ").Append(Fw[FwBanner]).Append(", zajety ").Append(Fw[FwBusy]).Append(", przerwa ").Append(Fw[FwCool]).Append(", w zwarciu ").Append(Fw[FwMelee])
              .Append(", przy szyku nekajacego ").Append(Fw[FwIntact]).Append(", limit zapytan ").Append(Fw[FwQuery]).Append(", panika ").Append(Fw[FwPanic])
              .Append(" | recenzja 283: cel AI na wodza bez trafienia ").Append(_rLeaderAim).Append("; piechota: nekajacy poza smycza ").Append(RUnreachT.Count)
              .Append(", przerwa po powrocie ").Append(_rFootCoolN).Append("; lucznicy: wstrzymani przy odpowiedzi ").Append(_rRespNear).Append(", zdjeci ").Append(_rRespDrop)
              .Append("; postoj: z przodu ").Append(_lNoFront).Append(", pieszy przy swoich ").Append(_lNoFootNear)
              .Append(" | poprawka 132b: lucznicy - swoi przy celu: zwarcie ").Append(_rNearMelee).Append(", rozrzut ").Append(_rNearSpread).Append(", zdjeci ")
              .Append(_rNearDrop).Append(", krok bez strzalu ").Append(_rNearHold).Append(" (zapytan ").Append(_rNearQ).Append("); piechota: straz strzelcow ")
              .Append(_rGuardN).Append(" (szykow ").Append(RGuardForms.Count).Append("); postoj: frontem do nekajacego ").Append(_lFace).Append(", armia z sumy szkod ")
              .Append(_lR5Sum)
              .Append(" | wyniki ");
            for (int i = 0; i < ROKinds; i++) sb.Append(i > 0 ? ", " : "").Append(ROName[i]).Append(' ').Append(ROutN[i]);
            sb.Append("; najdalej piechota ").Append(F0(_rFootFar)).Append(" m, jazda ").Append(F0(_hMaxFar)).Append(" m; piechoty padlo ").Append(_rFootLost)
              .Append(" | FF od lucznikow 283 ").Append(Volatile.Read(ref _rFfArch)).Append(" (strzalow ").Append(_hRangedShots).Append(')')
              .Append(" | z szyku gracza ").Append(_rFromPlayer).Append(" (oczekiwane 0)")
              .Append(" | pod sterowaniem 283 na koniec ").Append(_rLeft).Append(" (oczekiwane 0)")
              .Append(" | zatrzymane szyki na koniec ").Append(_lLeft).Append(" (oczekiwane 0)")
              .Append(" | koszt passu sr. ").Append(_rPassN > 0 ? (_rTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency / _rPassN).ToString("0.000", Inv) : "-")
              .Append(" / max ").Append(_rPassN > 0 ? (_rMaxTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency).ToString("0.000", Inv) : "-")
              .Append(" ms (passow ").Append(_rPassN).Append("; promieni najwiecej ").Append(_rRayMax).Append(" na pass, limit ").Append(RaysPerPass).Append(')')
              .Append(" | test: ").Append(ReactTestSummary())
              .Append(" | potkniecia nekanie ").Append(_rStum).Append('.');
            Log.Info(sb.ToString());
        }
    }
}
