using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 552 (15.11, Jeff w bitwie: "strzelam do szyku - ludzie z tarczami stoja z tylu, bez tarcz z przodu ... stoja jak kretyni i sie nie chronia;
    /// potem strzelam do wojska jak idzie ... musi sie odwrocic do mnie tarczami ... a ida jak zombie i olewaja to"). Specyfikacja 552 b/c.
    /// Zakres: szyk AI piechoty kazdej strony (f.IsAIControlled i bez gracza w szyku - HorsePursuit.PlayerFormDirect), tylko bitwa polowa (pass CloseRanks
    /// biegnie tylko przy _live = S4 i bitwa polowa), tylko przy _on (klucz AiFormationsUnderFire i latka 146). Strzelcow nie ruszamy (regula 140).
    ///  B (szyk stojacy, klucz FormationsFaceTheShooter): szyk pod ostrzalem (S4 z 552a), przed zwarciem, bez konnicy, nie w szarzy, stojacy (srodek
    ///    przesunal sie mniej niz 1 m przez 2 s), strzelcy z jednego kierunku (rozrzut >= 0.6) co najmniej 50 st. od czola, bez innego zagrozenia (nierozbity
    ///    szyk piechoty / konnicy wroga w 120 m pod katem > 60 st. od strzelca), nie czworobok 446, nie postoj R4 283, nie odwod 457 (459 ma szyk 8 nie-AI) -
    ///    czolo do strzelca: slot na szyk + postfiks FormationAI.TickOccasionally (po zachowaniu - gra i RBM - nadpisuje FacingOrder w tym samym wywolaniu).
    ///    Nowy kierunek przy zmianie o 30 st. najwczesniej 6 s po poprzednim; koniec: 3 s bez ostrzalu, zwarcie, konnica, ruch, gracz, zagrozenie.
    ///  B3 (klucz ShieldsToTheFrontRank): w ShieldWall / Square (Interval 0 - gra nie sortuje, LineFormation.SwitchFrontUnitTypesToFrontRows) zamiany
    ///    czlowieka bez tarczy z czlowiekiem z tarcza za nim (najpierw ten sam rzad, potem szereg 0 z rzedami +-1/+-2), 8 na pass (4 gdy szyk sie rozlazl).
    ///    Nie rusza bohaterow, kapitana, gracza, chorazych, paniki, odwrotu 143 (straz sztandaru 550 - do uzgodnienia przy scaleniu).
    ///  C2 (klucz ShieldsTowardShooterOnMarch): szyk idacy (nie bieg 291 C / 313b, nie szarza), strzelec 50-130 st. od czola - ludzie z tarcza patrza
    ///    na strzelca (prefiks Agent.SetFormationFrameEnabled: kierunek ramki i wspolczynnik >= 0.75), miejsce i kurs szyku bez zmian.
    /// Watki: pass na glownym watku pisze sloty; postfiks B2 (FormationAI.TickOccasionally z Team.Tick -> Formation.Tick - glowny watek) i prefiks C2
    /// (HumanAIComponent.ParallelUpdateFormationMovement - watki robocze) tylko czytaja (Volatile), bez alokacji, logu i Settings; liczniki Interlocked.
    /// Recenzja 552: srodek z CachedAveragePosition (nie mediana - artefakt 333e), grace ruchu i przerwa zwrotu / ponownego wejscia rosna z szerokoscia
    /// szyku (FaceGap), koniec B2 oddaje FacingOrder sprzed zwrotu (BehaviorStop/Reserve/PullBack/Charge/Retreat facingu co tick nie ustawiaja), R4 bierze
    /// ten zapisany facing (Face552Prior), zagrozenie z innej strony sprawdzane takze bez swiezego kierunku strzelca (FaceDir).
    /// Potkniecia: RoutHold.Stumble miejsce "szyk" (CloseRanks.Front552), licznik na szyk - od 5 szyk pominiety do konca bitwy; funkcji nie gasimy.
    /// </summary>
    internal static partial class CloseRanks
    {
        // ------------------------------------------------------------ stale 552
        private const int Slots552 = 16 * 10;                 // druzyna x szyk (jak Doctrine.T442Idx)
        private const float StillMove = 1f, StillSpan = 2f;   // szyk stoi: srodek < 1 m w 2 s (4 passy)
        private const float MinSpread = 0.6f, TurnFrom = 50f, ReTurnCos = 0.8660254f /* 30 st. */, ReTurnGap = 6f, FaceSafeGap = 120f, OtherFrom = 60f;
        private const float QuietEnd = 3f, BackFrom = 135f, ShieldGap = 40f, MeasureAfter = 3f, MarchMeasure = 2f, MarchMax = 130f;
        private const int SwapsPerPass = 8, SwapsSlow = 4, StumbleCap = 5, LiveLines = 20, Sample = 8;
        private const byte EQuiet = 0, EContact = 1, EHorse = 2, EMove = 3, EPlayer = 4, EThreat = 5, EOther = 6, EKinds = 7;
        private static readonly string[] EndName = { "cisza", "zwarcie", "konnica", "ruch", "gracz", "zagrozenie", "inne (446/R4/rezerwa/klucz)" };

        // ------------------------------------------------------------ sloty (pisze pass - glowny watek; czytaja latki - Volatile)
        private static readonly Formation[] FForm = new Formation[Slots552], MForm = new Formation[Slots552];
        private static readonly float[] FAng = new float[Slots552], MAng = new float[Slots552];
        private static readonly bool[] FOn = new bool[Slots552], MOn = new bool[Slots552];
        private static readonly FacingOrder[] FPrior = new FacingOrder[Slots552];   // recenzja 552 (4): facing sprzed zwrotu - tylko glowny watek
        private static volatile bool _fAny, _mAny;
        private static bool _kFace, _kFront, _kMarch, _f552;

        // ------------------------------------------------------------ latki 552 (raz na proces)
        private static bool _pFaceTried, _pMarchTried;
        private static bool _pFace, _pMarch;
        private static string _pFaceS = "bez latki", _pMarchS = "bez latki";
        private static int _pfCalls, _pfOver, _pfErr, _pmCalls, _pmOver, _pmErr;   // Interlocked

        // ------------------------------------------------------------ liczniki 552 (glowny watek)
        private static int _fIn, _fBack, _fSkipThreat, _fSkipHorse, _fSkipSpread, _fSkipHold, _fRe, _stum552, _stumForms552, _lines552;
        private static double _fAngSum, _fSecSum;
        private static int _fSecN;
        private static readonly int[] FEnd = new int[EKinds];
        private static int _bPass, _bSwap, _bRef, _bFormsM;
        private static double _bFront0, _bFront3, _bDev0, _bDev3;
        private static readonly int[] HitRank = new int[2 * 3];   // [szereg 0 / dalej] x [cialo, tarcza w rece, plecy] - szyki z B3
        private static int _mForms, _mExpect;
        private static double _mAng0, _mAng2, _mSpd0, _mSpd2;
        private static int _mAngN0, _mAngN2;

        // bufory passu (glowny watek)
        private static Agent[] _grid = new Agent[256];

        /// <summary>Stan 552 na szyk (pole St.S552, tworzony przy pierwszym passie piechoty).</summary>
        private sealed class S552
        {
            public readonly Vec2[] P = new Vec2[5];
            public readonly float[] PT = new float[5];
            public int PI, PN;
            public int Stum;
            public bool Face;            // B2 slot wlaczony
            public Vec2 FaceDir;
            public float FaceAt, FaceStart, QuietSince = -1f;
            public float FaceEndAt = -1000f;   // recenzja 552 (2): ostatni EndFace - przerwa przed ponownym wejsciem
            public FacingOrder Facing0;        // recenzja 552 (4): FacingOrder szyku sprzed pierwszego zwrotu
            public bool Front;           // B3 epizod
            public float FrontAt;
            public bool FrontMeasured, Slow;
            public float Front0, Dev0;
            public bool March;           // C2 slot wlaczony
            public float MarchAt;
            public bool MarchMeasured;
            public Vec2 MarchDir;
            public readonly Agent[] MS = new Agent[Sample];
            public int MSN;
        }

        private static int Idx552(Formation f)
        {
            var t = f != null ? f.Team : null;
            if (t == null) return -1;
            int ti = t.TeamIndex, fi = (int)f.FormationIndex;
            if (ti < 0 || ti >= 16 || fi < 0 || fi >= 10) return -1;
            return ti * 10 + fi;
        }

        // ------------------------------------------------------------ latki

        /// <summary>Begin (pierwsza bitwa z kluczem FormationsFaceTheShooter): postfiks FormationAI.TickOccasionally(float) - DeclaredMethod na klasie
        /// deklarujacej (pulapka 15.10), kontrola przez Harmony.GetPatchInfo.</summary>
        private static void EnsureFace552()
        {
            if (_pFaceTried) return;
            _pFaceTried = true;
            var h = NightHooks.H;
            if (h == null) { _pFaceS = "NIE (brak Harmony)"; return; }
            try
            {
                var t = AccessTools.DeclaredMethod(typeof(FormationAI), "TickOccasionally", new[] { typeof(float) });
                if (t == null) _pFaceS = "NIE (brak metody)";
                else
                {
                    h.Patch(t, postfix: new HarmonyMethod(typeof(CloseRanks), nameof(FacePost)) { priority = Priority.Last, after = new[] { "com.rbmai" } });
                    _pFace = Has(h, t, nameof(FacePost), false);
                    _pFaceS = _pFace ? "TAK" : "NIE (polknieta)";
                }
            }
            catch (Exception e) { _pFaceS = "NIE (" + e.GetType().Name + ")"; Stumble("CloseRanks.Latka552Front", e); }
            Log.Info("Front do strzelca (552): latka raz na proces - FormationAI.TickOccasionally (postfiks, czolo do strzelca) " + _pFaceS + ".");
        }

        /// <summary>Begin (pierwsza bitwa z kluczem ShieldsTowardShooterOnMarch): prefiks Agent.SetFormationFrameEnabled (DeclaredMethod, GetPatchInfo).</summary>
        private static void EnsureMarch552()
        {
            if (_pMarchTried) return;
            _pMarchTried = true;
            var h = NightHooks.H;
            if (h == null) { _pMarchS = "NIE (brak Harmony)"; return; }
            try
            {
                var t = AccessTools.DeclaredMethod(typeof(Agent), "SetFormationFrameEnabled",
                                                   new[] { typeof(TaleWorlds.Engine.WorldPosition), typeof(Vec2), typeof(Vec2), typeof(float) });
                if (t == null) _pMarchS = "NIE (brak metody)";
                else
                {
                    h.Patch(t, prefix: new HarmonyMethod(typeof(CloseRanks), nameof(FramePre)) { priority = Priority.Last });
                    _pMarch = Has(h, t, nameof(FramePre), true);
                    _pMarchS = _pMarch ? "TAK" : "NIE (polknieta)";
                }
            }
            catch (Exception e) { _pMarchS = "NIE (" + e.GetType().Name + ")"; Stumble("CloseRanks.Latka552Marsz", e); }
            Log.Info("Front do strzelca (552): latka raz na proces - Agent.SetFormationFrameEnabled (prefiks, tarcze w strone strzelca w marszu) " + _pMarchS + ".");
        }

        private static bool Has(Harmony h, MethodBase target, string name, bool prefix)
        {
            try
            {
                var info = Harmony.GetPatchInfo(target);
                if (info == null) return false;
                var list = prefix ? info.Prefixes : info.Postfixes;
                foreach (var p in list) if (p != null && p.owner == h.Id && p.PatchMethod != null && p.PatchMethod.Name == name) return true;
            }
            catch { }
            return false;
        }

        /// <summary>B2: po FormationAI.TickOccasionally (Team.Tick -> Formation.Tick -> FormationAI.Tick - glowny watek): szyk z wlaczonym slotem patrzy na strzelca (zachowanie gry / RBM
        /// ustawilo swoj FacingOrder chwile wczesniej w tym samym wywolaniu - nadpisujemy go, szyk nie migocze). Bez alokacji i logu.</summary>
        public static void FacePost(Formation ____formation)
        {
            if (!_fAny) return;
            try
            {
                Interlocked.Increment(ref _pfCalls);
                var f = ____formation;
                int i = Idx552(f);
                if (i < 0 || !Volatile.Read(ref FOn[i]) || !ReferenceEquals(Volatile.Read(ref FForm[i]), f) || !f.IsAIControlled) return;
                float a = Volatile.Read(ref FAng[i]);
                f.SetFacingOrder(FacingOrder.FacingOrderLookAtDirection(new Vec2((float)Math.Cos(a), (float)Math.Sin(a))));
                Interlocked.Increment(ref _pfOver);
            }
            catch { Interlocked.Increment(ref _pfErr); }
        }

        /// <summary>C2: przed Agent.SetFormationFrameEnabled (HumanAIComponent.ParallelUpdateFormationMovement - watki robocze): czlowiek AI z tarcza
        /// w szyku z wlaczonym slotem marszu patrzy na strzelca (kierunek ramki), wspolczynnik wymuszenia kierunku co najmniej 0.75. Pozycja bez zmian.</summary>
        public static void FramePre(Agent __instance, ref Vec2 direction, ref float formationDirectionEnforcingFactor)
        {
            if (!_mAny) return;
            try
            {
                Interlocked.Increment(ref _pmCalls);
                var a = __instance;
                var f = a != null ? a.Formation : null;
                int i = Idx552(f);
                if (i < 0 || !Volatile.Read(ref MOn[i]) || !ReferenceEquals(Volatile.Read(ref MForm[i]), f)) return;
                if (!a.IsAIControlled || !a.HasShieldCached) return;
                float ang = Volatile.Read(ref MAng[i]);
                direction = new Vec2((float)Math.Cos(ang), (float)Math.Sin(ang));
                if (formationDirectionEnforcingFactor < 0.75f) formationDirectionEnforcingFactor = 0.75f;
                Interlocked.Increment(ref _pmOver);
            }
            catch { Interlocked.Increment(ref _pmErr); }
        }

        // ------------------------------------------------------------ wejscia z CloseRanks (glowny watek)

        /// <summary>CloseRanks.Begin (po wyliczeniu _on): klucze 552 i latki raz na proces.</summary>
        private static void Begin552(Settings c)
        {
            _kFace = c != null && c.FormationsFaceTheShooter;
            _kFront = c != null && c.ShieldsToTheFrontRank;
            _kMarch = c != null && c.ShieldsTowardShooterOnMarch;   // 552c
            if (_key && _live && _kFace) EnsureFace552();
            if (_key && _live && _kMarch) EnsureMarch552();
            _f552 = _on && (_kFace || _kFront || _kMarch);
        }

        private static void Clear552()
        {
            _fAny = false;
            _mAny = false;
            for (int i = 0; i < Slots552; i++)
            {
                Volatile.Write(ref FOn[i], false); Volatile.Write(ref MOn[i], false);
                Volatile.Write(ref FForm[i], null); Volatile.Write(ref MForm[i], null);
            }
            _fIn = _fBack = _fSkipThreat = _fSkipHorse = _fSkipSpread = _fSkipHold = _fRe = _stum552 = _stumForms552 = _lines552 = 0;
            _fAngSum = _fSecSum = 0; _fSecN = 0;
            for (int i = 0; i < FEnd.Length; i++) FEnd[i] = 0;
            _bPass = _bSwap = _bRef = _bFormsM = 0;
            _bFront0 = _bFront3 = _bDev0 = _bDev3 = 0;
            for (int i = 0; i < HitRank.Length; i++) HitRank[i] = 0;
            _mForms = _mExpect = 0;
            _mAng0 = _mAng2 = _mSpd0 = _mSpd2 = 0; _mAngN0 = _mAngN2 = 0;
            Interlocked.Exchange(ref _pfCalls, 0); Interlocked.Exchange(ref _pfOver, 0); Interlocked.Exchange(ref _pfErr, 0);
            Interlocked.Exchange(ref _pmCalls, 0); Interlocked.Exchange(ref _pmOver, 0); Interlocked.Exchange(ref _pmErr, 0);
            Array.Clear(_grid, 0, _grid.Length);
            Array.Clear(FPrior, 0, FPrior.Length);
        }

        /// <summary>OnHit (pod Gate): trafienie w szyku z epizodem B3 - szereg 0 / dalej.</summary>
        private static void HitRank552(St st, Agent v, byte kind)
        {
            if (st.S == null || !st.S.Front) return;
            int r = ((IFormationUnit)v).FormationRankIndex;
            HitRank[(r == 0 ? 0 : 3) + kind]++;
        }

        /// <summary>One: szyk pominiety w passie (czworobok 446) - sloty zwolnione.</summary>
        private static void Drop552(Formation f, St st)
        {
            if (st == null || st.S == null) return;
            EndFace(f, st.S, EOther, null, false);   // 446 trzyma szyk - facing jego
            EndMarch(f, st.S);
            st.S.Front = false;
        }

        /// <summary>One (koniec passu szyku, glowny watek): B2 / B3 / C2 w osobnym try - potkniecie liczy sie na szyk, od 5 szyk pominiety do konca bitwy.</summary>
        private static void Front552(Mission m, Team t, Formation f, St st, bool inf, bool fire, bool contact, float gap, int beh, bool horse, int men, int hand)
        {
            if (!_f552 || st == null) return;
            if (st.S == null) st.S = new S552();
            var s = st.S;
            if (s.Stum >= StumbleCap) return;
            try { Core552(m, t, f, s, inf, fire, contact, gap, beh, horse, men, hand); }
            catch (Exception e)
            {
                s.Stum++;
                _stum552++;
                Stumble("CloseRanks.Front552", e);
                if (s.Stum >= StumbleCap)
                {
                    _stumForms552++;
                    try { EndFace(f, s, EOther, null, true); EndMarch(f, s); s.Front = false; } catch { }
                }
            }
        }

        private static string Tag(Formation f)
        {
            var t = f.Team;
            return "T" + (t != null ? t.TeamIndex : -1) + "/F" + (int)f.FormationIndex;
        }

        private static float AngDeg(Vec2 a, Vec2 b)
        {
            float d = Vec2.DotProduct(a, b);
            if (d > 1f) d = 1f; else if (d < -1f) d = -1f;
            return (float)(Math.Acos(d) * 180.0 / Math.PI);
        }

        private static void Core552(Mission m, Team t, Formation f, S552 s, bool inf, bool fire, bool contact, float gap, int beh, bool horse, int men, int hand)
        {
            float now = _now;
            // recenzja 552 (1): srednia, nie mediana - mediana (czlowiek najblizej sredniej) przeskakuje miedzy rzedami (ok. 1.1 m w Line) i dawala
            // falszywe "idzie" / "stoi" przy progu 1 m (ten sam artefakt co 333e)
            Vec2 c = f.CachedAveragePosition;
            // pierscien srodka (4 passy = 2 s)
            s.P[s.PI] = c; s.PT[s.PI] = now;
            s.PI = (s.PI + 1) % 5;
            if (s.PN < 5) s.PN++;
            bool still = false, moving = false;
            if (s.PN == 5)
            {
                int old = s.PI;   // najstarszy (nastepny do nadpisania)
                float span = now - s.PT[old];
                if (span <= StillSpan + 0.75f)   // passy bez przerwy (inaczej ruch nieznany - ani stoi, ani idzie)
                {
                    float d = c.Distance(s.P[old]);
                    still = d < StillMove;
                    moving = d > StillMove;
                }
            }
            bool scope = _on && !_ended && inf && f.IsAIControlled && !f.IsPlayerTroopInFormation;
            if (!scope)
            {
                if (s.Face) EndFace(f, s, f.IsAIControlled && !f.IsPlayerTroopInFormation ? EOther : EPlayer, null, true);
                EndMarch(f, s);
                s.Front = false;
                return;
            }
            if (fire) s.QuietSince = -1f; else if (s.QuietSince < 0f) s.QuietSince = now;
            var q = f.QuerySystem;
            bool cav = horse;
            if (!cav && q != null) { try { cav = q.IsUnderCavalryChargeFromFront; } catch { cav = false; } }
            // poza ostrzalem przed zwarciem One nie liczy konnicy - przy trwajacym slocie liczymy sami (ta sama miara)
            if (!cav && !(fire && !contact) && (s.Face || s.March)) cav = ShieldsUp.CavNear(m, f);
            Vec2 dir = Vec2.Zero;
            float spread = 0f, dist = 0f;
            int n = 0;
            bool hasDir = fire && ShieldsUp.ShooterDir(f, c, now, out dir, out spread, out n, out dist);
            float ratio = men > 0 ? (float)hand / men : 0f;
            // recenzja 552 (2/3): obrot szerokiego szyku trwa dluzej i przesuwa srodek - grace ruchu oraz przerwa zwrotu / ponownego wejscia rosna z szerokoscia
            float faceGap = FaceGap(f);

            // ---------------- B1/B2 czolo do strzelca
            if (_kFace && _pFace)
            {
                bool held = Doctrine.Q446Holds(f) || HorsePursuit.HaltHolds(f) || InfantryReserve457.HeldReserve(f);
                if (s.Face)
                {
                    byte why = 255;
                    if (held) why = EOther;
                    else if (contact) why = EContact;
                    else if (cav) why = EHorse;
                    else if (beh == 1 || (moving && now - s.FaceAt > faceGap)) why = EMove;
                    else if (!fire && s.QuietSince >= 0f && now - s.QuietSince >= QuietEnd) why = EQuiet;
                    else if (OtherThreat(m, f, c, hasDir ? dir : s.FaceDir)) why = EThreat;   // recenzja 552 (6): takze bez swiezego kierunku
                    if (why != 255) EndFace(f, s, why, null, !held);   // przy 446/R4/457 facing nalezy do wlasciciela (R4 bierze Face552Prior)
                    else if (hasDir && spread >= MinSpread && Vec2.DotProduct(dir, s.FaceDir) < ReTurnCos && now - s.FaceAt >= faceGap)
                    {
                        float ang = AngDeg(f.Direction, dir);
                        SetFace(f, s, dir, now);
                        _fRe++;
                        if (ang > BackFrom) _fBack++;
                    }
                }
                else if (fire && !contact && beh != 1 && still && hasDir && now - s.FaceEndAt >= faceGap)
                {
                    float ang = AngDeg(f.Direction, dir);
                    if (ang >= TurnFrom)
                    {
                        if (cav) _fSkipHorse++;
                        else if (held) _fSkipHold++;
                        else if (spread < MinSpread) _fSkipSpread++;
                        else if (OtherThreat(m, f, c, dir)) _fSkipThreat++;
                        else
                        {
                            SetFace(f, s, dir, now);
                            s.FaceStart = now;
                            _fIn++;
                            _fAngSum += ang;
                            if (ang > BackFrom) _fBack++;
                            if (_lines552 < LiveLines)
                            {
                                _lines552++;
                                Log.Info("552 zwrot | " + Tag(f) + " " + BehName[beh] + " " + f.ArrangementOrder.OrderEnum + " | kat " + ang.ToString("0", Inv)
                                         + " st. | strzelec " + dist.ToString("0", Inv) + " m (rozrzut " + spread.ToString("0.00", Inv) + ", zdarzen " + n
                                         + ") | tarcze w rece " + (ratio * 100f).ToString("0", Inv) + "%");
                            }
                        }
                    }
                }
            }
            else if (s.Face) EndFace(f, s, EOther, null, true);

            // ---------------- B3 tarcze na czolo
            bool front = _kFront && fire && gap > ShieldGap && !cav && beh != 1 && !Doctrine.Q446Holds(f);
            var arr = front ? f.Arrangement : null;
            if (front && (!(arr is LineFormation) || arr is TransposedLineFormation)) front = false;
            if (front)
            {
                bool wall = f.Interval <= 0f;   // ShieldWall / Square - gra nie sortuje (LineFormation.SwitchFrontUnitTypesToFrontRows)
                int sh, fr;
                if (!s.Front)
                {
                    s.Front = true;
                    s.FrontAt = now;
                    s.FrontMeasured = false;
                    s.Front0 = FrontShare(f, arr, out sh, out fr);
                    s.Dev0 = f.CachedFormationIntegrityData.DeviationOfPositionsExcludeFarAgents;
                }
                if (wall)
                {
                    _bPass++;
                    Swap(f, arr, s.Slow ? SwapsSlow : SwapsPerPass);
                }
                if (!s.FrontMeasured && now - s.FrontAt >= MeasureAfter)
                {
                    s.FrontMeasured = true;
                    float f3 = FrontShare(f, arr, out sh, out fr);
                    float d3 = f.CachedFormationIntegrityData.DeviationOfPositionsExcludeFarAgents;
                    if (s.Front0 >= 0f && f3 >= 0f) { _bFront0 += s.Front0; _bFront3 += f3; _bFormsM++; _bDev0 += s.Dev0; _bDev3 += d3; }
                    if (s.Dev0 > 0.01f && d3 > 2f * s.Dev0) s.Slow = true;   // ryzyko F1: szyk rozlazl sie od zamian - mniej zamian
                }
            }
            else s.Front = false;

            // ---------------- C2 tarcze w strone strzelca w marszu
            bool march = false;
            if (_kMarch && _pMarch && fire && !contact && !cav && moving && beh != 1 && hasDir && spread >= MinSpread
                && !MarchToContact.Rush313(f) && !MarchToContact.RunningBare(f))
            {
                float ang = AngDeg(f.Direction, dir);
                march = ang >= TurnFrom && ang <= MarchMax;
            }
            if (march)
            {
                int i = Idx552(f);
                if (i >= 0)
                {
                    float a = (float)Math.Atan2(dir.y, dir.x);
                    Volatile.Write(ref MAng[i], a);
                    Volatile.Write(ref MForm[i], f);
                    Volatile.Write(ref MOn[i], true);
                    _mAny = true;
                    if (!s.March)
                    {
                        s.March = true;
                        s.MarchAt = now;
                        s.MarchMeasured = false;
                        _mForms++;
                        MarchSample(f, s, dir, true);
                    }
                    s.MarchDir = dir;
                    _mExpect += hand;   // ludzie z tarcza w rece x 0.5 s x ok. 2 ramki/s
                    if (!s.MarchMeasured && now - s.MarchAt >= MarchMeasure) { s.MarchMeasured = true; MarchSample(f, s, dir, false); }
                }
            }
            else EndMarch(f, s);
        }

        private static void SetFace(Formation f, S552 s, Vec2 dir, float now)
        {
            int i = Idx552(f);
            if (i < 0) return;
            if (!s.Face) { s.Facing0 = f.FacingOrder; FPrior[i] = s.Facing0; }   // recenzja 552 (4): facing zachowania sprzed zwrotu
            Volatile.Write(ref FAng[i], (float)Math.Atan2(dir.y, dir.x));
            Volatile.Write(ref FForm[i], f);
            Volatile.Write(ref FOn[i], true);
            _fAny = true;
            s.Face = true;
            s.FaceDir = dir;
            s.FaceAt = now;
            f.SetFacingOrder(FacingOrder.FacingOrderLookAtDirection(dir));   // od razu, bez czekania na tick zachowania
        }

        private static void EndFace(Formation f, S552 s, byte why, string extra, bool restore)
        {
            if (!s.Face) return;
            s.Face = false;
            s.FaceEndAt = _now;
            int i = Idx552(f);
            if (i >= 0 && ReferenceEquals(FForm[i], f)) { Volatile.Write(ref FOn[i], false); Volatile.Write(ref FForm[i], null); }
            // recenzja 552 (4): BehaviorStop/Reserve/PullBack/Charge/Retreat nie ustawiaja facingu w TickOccasionally - bez oddania szyk zostawal obrocony na
            // dawnego strzelca do konca; zachowania, ktore ustawiaja facing co tick, i tak nadpisza go przy nastepnym ticku. Bez oddania, gdy szyk trzyma
            // 446 / R4 / 457 (facing nalezy do wlasciciela; R4 zapisal Face552Prior) albo szyk nie jest juz AI (gracz).
            if (restore && f.CountOfUnits > 0 && f.IsAIControlled && !f.IsPlayerTroopInFormation) f.SetFacingOrder(s.Facing0);
            if (why < EKinds) FEnd[why]++;
            float sec = _now - s.FaceStart;
            if (sec >= 0f) { _fSecSum += sec; _fSecN++; }
            if (_lines552 < LiveLines)
            {
                _lines552++;
                Log.Info("552 koniec | " + Tag(f) + " | po " + sec.ToString("0.0", Inv) + " s | " + (why < EKinds ? EndName[why] : "?") + (extra ?? ""));
            }
        }

        /// <summary>Recenzja 552 (2/3): grace ruchu przy zwrocie oraz przerwa miedzy zwrotami i przed ponownym wejsciem - 6 s, przy szerokim szyku
        /// 0.25 s na metr szerokosci (36 m - 9 s), najwyzej 12 s (skrzydla linii ida przy 90 st. ok. 0.7 szerokosci).</summary>
        private static float FaceGap(Formation f)
        {
            float w = 0f;
            try { w = f.Width; } catch { w = 0f; }
            float g = w * 0.25f;
            if (g > 12f) g = 12f;
            return g > ReTurnGap ? g : ReTurnGap;
        }

        /// <summary>Recenzja 552 (4), dla R4 (HorsePursuit.ReactHalt.Face, glowny watek): szyk ma trwajacy zwrot 552 - facing zachowania sprzed zwrotu,
        /// zeby postoj R4 nie zapisal naszego LookAtDirection jako swojego Facing0.</summary>
        internal static bool Face552Prior(Formation f, out FacingOrder prior)
        {
            prior = default(FacingOrder);
            int i = Idx552(f);
            if (i < 0 || !FOn[i] || !ReferenceEquals(FForm[i], f)) return false;
            prior = FPrior[i];
            return true;
        }

        private static void EndMarch(Formation f, S552 s)
        {
            if (!s.March) return;
            s.March = false;
            int i = Idx552(f);
            if (i >= 0 && ReferenceEquals(MForm[i], f)) { Volatile.Write(ref MOn[i], false); Volatile.Write(ref MForm[i], null); }
        }

        /// <summary>B1: nierozbity szyk piechoty albo konnicy wroga w FaceSafeGap od srodka, pod katem wiekszym niz 60 st. od kierunku strzelca.</summary>
        private static bool OtherThreat(Mission m, Formation f, Vec2 c, Vec2 dir)
        {
            var teams = m.Teams;
            int nt = teams != null ? teams.Count : 0;
            float r2 = FaceSafeGap * FaceSafeGap;
            float cosOther = (float)Math.Cos(OtherFrom * Math.PI / 180.0);
            for (int i = 0; i < nt; i++)
            {
                var t = teams[i];
                if (t == null || f.Team == null || !t.IsEnemyOf(f.Team)) continue;
                var fl = t.FormationsIncludingSpecialAndEmpty;
                int nf = fl != null ? fl.Count : 0;
                for (int j = 0; j < nf; j++)
                {
                    var e = fl[j];
                    if (e == null || e.CountOfUnits <= 0) continue;
                    var eq = e.QuerySystem;
                    if (eq == null || !(eq.IsInfantryFormation || eq.IsCavalryFormation)) continue;
                    var ai = e.AI;
                    if (ai != null && ai.ActiveBehavior is BehaviorRetreat) continue;   // rozbity / w odwrocie
                    var emp = e.CachedMedianPosition;
                    Vec2 ec = emp.IsValid ? emp.AsVec2 : e.CachedAveragePosition;
                    Vec2 d = ec - c;
                    float l2 = d.LengthSquared;
                    if (l2 > r2 || l2 < 1f) continue;
                    if (Vec2.DotProduct(d.Normalized(), dir) < cosOther) return true;
                }
            }
            return false;
        }

        /// <summary>B3: czy czlowieka wolno przestawic (obie strony zamiany).</summary>
        private static bool Movable(Agent a, Formation f, RoutKeeper rk)
        {
            if (a == null || !a.IsActive() || !a.IsHuman || a.IsHero || a.IsMainAgent || !a.IsAIControlled) return false;
            if (ReferenceEquals(a, f.Captain) || HorsePursuit.Banner(a)) return false;
            if (SurvivalMode.InPanic(a) || a.IsRunningAway || (rk != null && rk.Withdrawing(a))) return false;
            // 550 (straz sztandaru): znacznik do uzgodnienia przy scaleniu galezi - tu jeszcze go nie ma
            return ((IFormationUnit)a).FormationFileIndex >= 0;
        }

        /// <summary>Siatka szyku z GetAllUnits (migawka): _grid[file * ranks + rank]; zwraca false, gdy pusto.</summary>
        private static bool Grid(IFormationArrangement arr, out int files, out int ranks)
        {
            files = 0; ranks = 0;
            var all = arr.GetAllUnits();
            int n = all != null ? all.Count : 0;
            for (int i = 0; i < n; i++)
            {
                var u = all[i];
                if (u == null) continue;
                int fi = u.FormationFileIndex, ri = u.FormationRankIndex;
                if (fi < 0 || ri < 0) continue;
                if (fi + 1 > files) files = fi + 1;
                if (ri + 1 > ranks) ranks = ri + 1;
            }
            if (files == 0 || ranks == 0) return false;
            int need = files * ranks;
            if (_grid.Length < need) _grid = new Agent[Math.Max(need, _grid.Length * 2)];
            else Array.Clear(_grid, 0, need);
            for (int i = 0; i < n; i++)
            {
                var a = all[i] as Agent;
                if (a == null) continue;
                int fi = ((IFormationUnit)a).FormationFileIndex, ri = ((IFormationUnit)a).FormationRankIndex;
                if (fi < 0 || ri < 0 || fi >= files || ri >= ranks) continue;
                _grid[fi * ranks + ri] = a;
            }
            return true;
        }

        /// <summary>B3 pomiar: ludzie z tarcza w szeregu 0 / min(zajete miejsca w szeregu 0, ludzie z tarcza); -1 = brak danych.</summary>
        private static float FrontShare(Formation f, IFormationArrangement arr, out int shields, out int front)
        {
            shields = 0; front = 0;
            int files, ranks;
            if (!Grid(arr, out files, out ranks)) return -1f;
            int frontShield = 0;
            for (int fi = 0; fi < files; fi++)
                for (int ri = 0; ri < ranks; ri++)
                {
                    var a = _grid[fi * ranks + ri];
                    if (a == null || !a.IsActive()) continue;
                    bool hs = a.HasShieldCached;
                    if (hs) shields++;
                    if (ri == 0) { front++; if (hs) frontShield++; }
                }
            int den = Math.Min(front, shields);
            return den > 0 ? (float)frontShield / den : -1f;
        }

        /// <summary>B3: zamiany (najkrotsza droga): 1) ten sam rzad - bez tarczy w r-1, z tarcza w r; 2) szereg 0 bez tarczy z rzedem +-1 / +-2, w ktorym
        /// w szeregu 1 albo 2 stoi czlowiek z tarcza. Kontrola: szereg sie nie zmienil = odmowa (RBM OverrideLineFormation albo gra).</summary>
        private static void Swap(Formation f, IFormationArrangement arr, int cap)
        {
            int files, ranks;
            if (!Grid(arr, out files, out ranks) || ranks < 2) return;
            var rk = RoutHold.Live;
            int done = 0;
            for (int r = 1; r < ranks && done < cap; r++)
                for (int fi = 0; fi < files && done < cap; fi++)
                {
                    var front = _grid[fi * ranks + r - 1];
                    var back = _grid[fi * ranks + r];
                    if (front == null || back == null || front.HasShieldCached || !back.HasShieldCached) continue;
                    if (!Movable(front, f, rk) || !Movable(back, f, rk)) continue;
                    if (Do(arr, front, back, fi, r - 1, fi, r, ranks)) done++;
                }
            for (int fi = 0; fi < files && done < cap; fi++)
            {
                var front = _grid[fi * ranks];
                if (front == null || front.HasShieldCached || !Movable(front, f, rk)) continue;
                bool ok = false;
                for (int k = 1; k <= 2 && !ok; k++)
                    for (int sg = -1; sg <= 1 && !ok; sg += 2)
                    {
                        int fj = fi + sg * k;
                        if (fj < 0 || fj >= files) continue;
                        for (int r = 1; r <= 2 && r < ranks && !ok; r++)
                        {
                            var back = _grid[fj * ranks + r];
                            if (back == null || !back.HasShieldCached || !Movable(back, f, rk)) continue;
                            if (Do(arr, front, back, fi, 0, fj, r, ranks)) { done++; ok = true; }
                            else ok = true;   // odmowa - nastepny czlowiek z szeregu 0
                        }
                    }
            }
        }

        private static bool Do(IFormationArrangement arr, Agent a, Agent b, int fa, int ra, int fb, int rb, int ranks)
        {
            arr.SwitchUnitLocations(a, b);
            if (((IFormationUnit)a).FormationRankIndex == ra) { _bRef++; return false; }   // RBM (OverrideLineFormation) albo gra nie zamienila
            _grid[fa * ranks + ra] = b;
            _grid[fb * ranks + rb] = a;
            _bSwap++;
            return true;
        }

        /// <summary>C2 pomiar: probka do 8 ludzi z tarcza - kat patrzenia do strzelca i predkosc, przy wejsciu (first) i 2 s po.</summary>
        private static void MarchSample(Formation f, S552 s, Vec2 dir, bool first)
        {
            if (first)
            {
                s.MSN = 0;
                Units.Clear();
                f.ApplyActionOnEachUnit(Collect);   // gra iteruje swoje listy - my tylko zbieramy (bufor 146, pusty po spisie w One)
                for (int u = 0; u < Units.Count && s.MSN < Sample; u++)
                {
                    var a = Units[u];
                    if (a != null && a.IsActive() && a.IsHuman && a.IsAIControlled && a.HasShieldCached) s.MS[s.MSN++] = a;
                }
                Units.Clear();
            }
            for (int u = 0; u < s.MSN; u++)
            {
                var a = s.MS[u];
                if (a == null || !a.IsActive()) continue;
                var ld = a.LookDirection.AsVec2;
                if (ld.LengthSquared < 1E-04f) continue;
                float ang = AngDeg(ld.Normalized(), dir);
                float spd = a.MovementVelocity.Length;
                if (first) { _mAng0 += ang; _mSpd0 += spd; _mAngN0++; }
                else { _mAng2 += ang; _mSpd2 += spd; _mAngN2++; }
            }
            if (!first) { for (int u = 0; u < Sample; u++) s.MS[u] = null; s.MSN = 0; }
        }

        // ------------------------------------------------------------ linia bitwy (po linii 146)

        private static void Write552()
        {
            if (!_kFace && !_kFront && !_kMarch) return;
            int harassForms, harassOnly, evDir, evNoDir;
            ShieldsUp.HarassCounts(out harassForms, out harassOnly, out evDir, out evNoDir);
            var sb = new StringBuilder(1200);
            sb.Append("Front do strzelca (552): klucze czolo ").Append(_kFace ? "TAK" : "NIE").Append(", tarcze na czolo ").Append(_kFront ? "TAK" : "NIE")
              .Append(", marsz ").Append(_kMarch ? "TAK" : "NIE").Append(_f552 ? "" : " - nieczynne (146 nieczynne: klucz AiFormationsUnderFire / latka RBM)")
              .Append(" | nekanie: szykow ").Append(harassForms).Append(" (bez 3/5 s ").Append(harassOnly).Append("), zdarzen z kierunkiem ").Append(evDir).Append('/')
              .Append("bez ").Append(evNoDir)
              .Append(" | zwrot (B2, postfiks FormationAI.TickOccasionally ").Append(_pFaceS).Append("): wejsc ").Append(_fIn).Append(" (sr. kat ")
              .Append(Avg(_fAngSum, _fIn, "0")).Append(" st., >").Append(BackFrom.ToString("0", Inv)).Append(" st. ").Append(_fBack).Append("), zmian kierunku ").Append(_fRe)
              .Append(", sr. czas ").Append(Avg(_fSecSum, _fSecN, "0.0")).Append(" s, konce:");
            for (int i = 0; i < EKinds; i++) sb.Append(i == 0 ? " " : " / ").Append(EndName[i]).Append(' ').Append(FEnd[i]);
            sb.Append("; pominiete: wrog z innej strony ").Append(_fSkipThreat).Append(", konnica ").Append(_fSkipHorse).Append(", rozrzut ").Append(_fSkipSpread)
              .Append(", 446/R4/rezerwa ").Append(_fSkipHold).Append("; postfiks wywolan ").Append(Volatile.Read(ref _pfCalls)).Append(", nadpisan ")
              .Append(Volatile.Read(ref _pfOver)).Append(", bledow ").Append(Volatile.Read(ref _pfErr))
              .Append(" | tarcze na czolo (B3): passow ").Append(_bPass).Append(", zamian ").Append(_bSwap).Append(", odmow ").Append(_bRef)
              .Append(", czolo z tarcza przy wejsciu ").Append(Pct(_bFront0, _bFormsM)).Append(" -> po ").Append(MeasureAfter.ToString("0", Inv)).Append(" s ")
              .Append(Pct(_bFront3, _bFormsM)).Append(" (szykow ").Append(_bFormsM).Append("), odchylenie ").Append(Avg(_bDev0, _bFormsM, "0.00")).Append(" -> ")
              .Append(Avg(_bDev3, _bFormsM, "0.00")).Append("; trafienia szereg 0 cialo/tarcza/plecy ").Append(HitRank[0]).Append('/').Append(HitRank[1]).Append('/')
              .Append(HitRank[2]).Append(", dalej ").Append(HitRank[3]).Append('/').Append(HitRank[4]).Append('/').Append(HitRank[5])
              .Append(" | marsz (C2, prefiks Agent.SetFormationFrameEnabled ").Append(_pMarchS).Append("): szykow ").Append(_mForms).Append(", prefiks wywolan ")
              .Append(Volatile.Read(ref _pmCalls)).Append(", nadpisan ").Append(Volatile.Read(ref _pmOver)).Append(" (oczekiwane ok. ").Append(_mExpect)
              .Append("), kat do strzelca przed ").Append(Avg(_mAng0, _mAngN0, "0")).Append(" st. / po ").Append(Avg(_mAng2, _mAngN2, "0"))
              .Append(" st., predkosc przed ").Append(Avg(_mSpd0, _mAngN0, "0.00")).Append(" / po ").Append(Avg(_mSpd2, _mAngN2, "0.00")).Append(" m/s, bledow ")
              .Append(Volatile.Read(ref _pmErr))
              .Append(" | potkniecia 552 ").Append(_stum552).Append(", szykow pominietych ").Append(_stumForms552).Append('.');
            Log.Info(sb.ToString());
        }

        private static string Pct(double sum, int n) { return n > 0 ? (sum / n * 100.0).ToString("0", Inv) + "%" : "-"; }
    }
}
