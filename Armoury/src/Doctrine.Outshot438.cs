using System;
using System.Globalization;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 438 = PAKIET 1 audytu AI bitwy (docs/audyty-2026-11-11/AUDYT-AI-BITWA.md rozdz. 3 "Reakcja piechoty na ostrzal"; Jeff 11.11 "rob wszystko po kolei tak
    /// jak radzisz", decyzja nr 2 "TAK"). Tylko decyzje, zadnych bonusow do statystyk. Trzy klucze (domyslnie TAK), NIE = zachowanie co do znaku jak dotad:
    ///
    /// 1.1 OutshotRushInRbmAttackPlans ("Outshot Rush In RBM Attack Plans"). DOWOD (audyt rozdz. 3, LOG 23:06 :100757 / :100777): plan AttackSplitArchers przez
    ///     579 s i "szykow w biegu 0, wejsc w bieg 0" - Doctrine.OutshotAdvancing uznawala za "taktyke ataku" tylko FullScaleAttack / Charge /
    ///     RangedHarrassmentOffensive / FrontalCavalryCharge, a RBM przydziela atakujacemu wedlug vanillowego id kultury (rbmai:10050-10090) wlasne plany
    ///     RBMTacticAttackSplitArchers (battania = Polnoc), RBMTacticAttackSplitInfantry (sturgia / nord), RBMTacticAttackSplitSkirmishers (aserai / darshi),
    ///     RBMTacticEmbolon (empire) z wagami 5-10 (rbmai:559, 1358, 1746, 2068). Zmiana: te cztery typy (po nazwie, brak = pominiete) tez sa "taktyka ataku"
    ///     w OutshotAdvancing (why 3 "taktyka ataku" segmentu E, sprawdzane PO wszystkich dotychczasowych powodach - druzyna dotad Live z powodu 1-5 liczy sie
    ///     jak dotad; osobne liczniki w linii 438). Funkcja Offensive() bez
    ///     zmian (czyta ja tez pomiar 291 "taktyka ataku przy trzymaniu" i DhMul nie tnie splitow). OZYWIONY KOD: druzyna Live = bieg z tarczami 313b,
    ///     Loose zamiast muru tarcz (CloseRanks.WantLoose), bez postoju 283 R4 / R5 (OutshotPushes), cel pieszy zamiast konnych lucznikow AI (ClosestPost)
    ///     - u ludow z planem split RBM to wszystko dotad nie dzialalo. Liczone: ile szykow naraz w biegu (maks, w tym z planu RBM), ile druzyn naraz.
    /// 1.2 OutshotTriggerByMissileLosses ("Outshot Trigger By Missile Losses"). DOWOD (audyt, mapa-dowody-z-logow.md rozdz. 8): 23:06 "maks. trafionych
    ///     piechoty 0.07" przy 1911 trafieniach strony w 716 s - progi chwilowego odsetka trafionych (bieg: UnderRangedAttackRatio >= 0.33 - gra pomija
    ///     ludzi z tarcza; S2: 20% trafionych przez 20 s) nie widza strat od pociskow. Zmiana: sygnal ostrzalu takze z PRZYROSTU strat od pociskow
    ///     druzyny - TeamQuerySystem.DeathByRangedCount (z 313y: takze smierc z krwawienia po strzale) wzrosl o co najmniej FireDead (2) w ostatnich 30 s
    ///     (probka co 2 s, 16 miejsc), a szyk ma pieszego wroga blizej niz S2Range (200 m - zasieg luku, jak S2). Dotyczy: MarchToContact (bieg 291 C bez tarcz
    ///     i 313b z tarczami, szarza pod ostrzalem 313y) - "fire" = trafieni >= 0.33 ALBO sygnal 140 ALBO przyrost strat; S2 (141) - warunek = trafieni >= 20%
    ///     ALBO przyrost strat (reszta warunku bez zmian: strzelcy x3 < wroga, pieszy wrog <= 200 m, 20 s wejscia, 10 s wyjscia). Licznik 140 (ShieldsUp.FormFire)
    ///     juz byl w sygnale biegu - bez zmian.
    /// 1.3 DefenderLeavesRbmSplitWhenOutshot ("Defender Leaves RBM Split When Outshot"). DOWOD (mapa-dowody-z-logow.md, 13:35): wejscie w S2 1, potem
    ///     "FullScaleAttack -> RBMTacticDefendSplitInfantry (czyli NIE atak)", 116 z 207 padlo od strzal bez zwarcia - RBMTacticDefendSplitInfantry
    ///     (sturgia / nord, waga 10 / 5, rbmai:934) i RBMTacticDefendSplitArchers (battania, waga 10, rbmai:1186) nie maja postfiksow S2 ani IsDefenseApplicable,
    ///     wiec przy wadze gry ok. 1 zawsze wygrywaja. Zmiana: postfiks GetTacticWeight na tych dwoch typach (AccessTools.DeclaredMethod - metoda zadeklarowana
    ///     w nich, sprawdzone w rbmai; GetPatchInfo w linii "Latki nocy (K3)"), Priority.Last po com.rbmai: gdy druzyna przegrywa wymiane ognia - S2 (141) zywe
    ///     albo puszczona bilansem ognia 291e / 320d (_dhOff 3) - waga splitu PRZYCIETA (nie mnozona) do skali gry = najwyzszej wagi obrony gry tej druzyny
    ///     (DefensiveEngagement / DefensiveLine / Ring / HoldChokePoint z DhNote - juz po S2Mul x0.3; po 291e gra daje im 0, bo IsDefenseApplicable NIE).
    ///     Brak wag 291 dla druzyny - sufit S2Hold (waga gry ok. 1 x 0.3). Split nie wygrywa wiec z obrona gry i wybor atak / obrona jest jak u innych ludow.
    ///     438-p: takze PO S2 (druzyna 291 z _dhS2Was), gdy gra sama mowi obrona NIE - sufit 0 (jak taktyki obrony gry); bez tego wahadlo S2 -> atak -> split.
    ///     1.2 438-p: sygnal ze strat bierze tylko szyk piechoty druzyny Live, ktory naciera (Advancing), odleglosc do PIESZEGO wroga (FootGap).
    /// 1.4 sam pomiar (co hamuje bieg - audyt: srednio 1.9 m/s, LOG:88577): co 2 s dla kazdego szyku w biegu (291 C / 313b) najwyzej 40 ludzi - predkosc
    ///     rzeczywista, pelny bieg (GetMaximumForwardUnlimitedSpeed), cap tchu AiBreath, znacznik tarczy 140, WalkMode, ulamek wolniej niz 2 m/s, szyk.
    ///
    /// Linia "Ostrzal piechoty (438)" raz na bitwe polowa (Battle291.Write, po linii "Bitwa (291)"); przy kluczu NIE liczniki "bylby" (na sucho).
    /// Watki: O438Tick, liczniki biegu i pomiar w glownym watku (OnMissionTick); postfiks splitow w ticku druzyn - tylko odczyt tablic (_s2Live / _dhOff /
    /// _dhW pisane z glownego watku albo przez postfiksy tej samej decyzji, jak DhMul), klucz z kopii volatile, liczniki Interlocked, bez alokacji.
    /// Potkniecia liczone (_o438Stum, RoutHold.Stumble), bez globalnego wylacznika.
    /// </summary>
    internal static partial class Doctrine
    {
        internal const float O438LossWin = 30f, O438Every = 2f, O438Range = S2Range, O438ProbeEvery = 2f;
        private const int O438Ring = 16;          // 15 odstepow po 2 s = 30 s
        private const int O438ProbeMen = 40;      // 1.4: najwyzej tylu ludzi na szyk w probce
        private static readonly IFormatProvider O438Inv = CultureInfo.InvariantCulture;

        // ---- klucze (kopie z glownego watku; postfiks splitow czyta)
        private static volatile bool _o438Rbm = true, _o438Loss = true, _o438Split = true;

        // ---- typy RBM (po nazwie, raz na proces)
        private static Type _tAtkArch, _tAtkInf, _tAtkSkir, _tEmbolon, _tDefInf, _tDefArch;
        private static string _o438Patch = "nie wpiete";

        // ---- historia strat od pociskow (glowny watek)
        private static readonly Team[] _o438Ref = new Team[MaxTeams];
        private static readonly int[] _o438Hist = new int[MaxTeams * O438Ring];
        private static readonly int[] _o438N = new int[MaxTeams];
        private static readonly int[] _o438Cur = new int[MaxTeams];
        private static WeakReference _o438M;
        private static float _o438Acc, _o438ProbeAcc;

        // ---- 1.1 stan druzyn (glowny watek)
        private static readonly bool[] _o438RbmNow = new bool[MaxTeams];
        private static readonly bool[] _o438RbmEver = new bool[MaxTeams];
        private static readonly bool[] _o438RbmDry = new bool[MaxTeams];
        // ---- 1.3 stan druzyn (glowny watek)
        private static readonly bool[] _o438SplitWas = new bool[MaxTeams];

        // ---- liczniki misji (Reset w Battle291.Begin)
        private static int _o438RbmTeams, _o438RbmDryTeams, _o438ArchN, _o438InfN, _o438SkirN, _o438EmbN;
        private static float _o438RbmSec;
        private static int _o438PeakRush, _o438PeakRushRbm, _o438PeakTeams, _o438RushTicks;
        private static long _o438RushSum;
        private static float _o438RushRbmSec;
        private static int _o438LossBare, _o438LossShield, _o438LossChg, _o438LossDry;
        private static int _o438S2Loss, _o438S2LossDry;
        private static float _o438S2LossSec;
        private static int _o438SplitS2, _o438SplitFire, _o438SplitDry, _o438SplitPost;   // Interlocked (tick druzyn); 438-p: _o438SplitPost - po S2
        private static float _o438SplitSec;
        private static int _o438SplitLeft, _o438SplitLeftAtk, _o438SplitBack;   // 438-p: _o438SplitBack - powrot do splitu przy przegranym ostrzale (wahadlo)
        private static int _o438Stum;
        // 1.4
        private static int _pbForms, _pbMen, _pbBreath, _pbMarked, _pbWalk, _pbSlow, _pbLoose, _pbWall, _pbOther;
        private static double _pbSpeed, _pbFull, _pbCap, _pbIntegrity;
        private static int _pbIntegrityN;
        private static int _pbStep, _pbSeen;

        /// <summary>Battle291.Begin (AfterStart, przed Begin paczek): liczniki linii 438 od zera, stan druzyn bez misji.</summary>
        internal static void O438Reset()
        {
            for (int i = 0; i < MaxTeams; i++) O438ClearTeam(i);
            _o438M = null;
            _o438Acc = _o438ProbeAcc = 0f;
            _o438RbmTeams = _o438RbmDryTeams = _o438ArchN = _o438InfN = _o438SkirN = _o438EmbN = 0;
            _o438RbmSec = 0f;
            _o438PeakRush = _o438PeakRushRbm = _o438PeakTeams = _o438RushTicks = 0;
            _o438RushSum = 0;
            _o438RushRbmSec = 0f;
            _o438LossBare = _o438LossShield = _o438LossChg = _o438LossDry = 0;
            _o438S2Loss = _o438S2LossDry = 0;
            _o438S2LossSec = 0f;
            Interlocked.Exchange(ref _o438SplitS2, 0); Interlocked.Exchange(ref _o438SplitFire, 0); Interlocked.Exchange(ref _o438SplitDry, 0);
            Interlocked.Exchange(ref _o438SplitPost, 0);   // 438-p
            _o438SplitSec = 0f;
            _o438SplitLeft = _o438SplitLeftAtk = _o438SplitBack = 0;
            _o438Stum = 0;
            _pbForms = _pbMen = _pbBreath = _pbMarked = _pbWalk = _pbSlow = _pbLoose = _pbWall = _pbOther = 0;
            _pbSpeed = _pbFull = _pbCap = _pbIntegrity = 0.0;
            _pbIntegrityN = 0;
            var c = Settings.Current;
            if (c != null) O438Keys(c);
        }

        /// <summary>Doctrine.End: stan druzyn bez referencji do misji (liczniki zostaja do linii w Battle291.Write).</summary>
        private static void O438End()
        {
            for (int i = 0; i < MaxTeams; i++) O438ClearTeam(i);
            _o438M = null;
        }

        private static void O438ClearTeam(int i)
        {
            O438ClearHist(i);
            _o438RbmNow[i] = false; _o438RbmEver[i] = false; _o438RbmDry[i] = false;
        }

        /// <summary>438-p (recenzja 438): nowa druzyna w miejscu tablicy w O438Tick - tylko historia strat i stan splitu (pisane wylacznie w O438Tick).
        /// Flagi 1.1 (_o438RbmNow / Ever / Dry) ustawia OutshotAdvancing (OutshotRush.Tick, co 0.5 s) - zerowane tylko w O438Reset / O438End / zmianie misji,
        /// inaczej pierwsza probka 2 s po rozstawieniu kasowala je i druzyna liczyla sie w 1.1 dwa razy.</summary>
        private static void O438ClearHist(int i)
        {
            _o438Ref[i] = null; _o438N[i] = 0; _o438Cur[i] = 0;
            for (int k = 0; k < O438Ring; k++) _o438Hist[i * O438Ring + k] = 0;
            _o438SplitWas[i] = false;
        }

        private static void O438Keys(Settings c)
        {
            _o438Rbm = c.OutshotRushInRbmAttackPlans;
            _o438Loss = c.OutshotTriggerByMissileLosses;
            _o438Split = c.DefenderLeavesRbmSplitWhenOutshot;
        }

        // ------------------------------------------------------------ latka (raz na proces - ApplyMissionPatches)

        /// <summary>1.1 typy planow ataku RBM i 1.3 postfiks GetTacticWeight obronnych splitow RBM (DeclaredMethod na klasie deklarujacej, GetPatchInfo).
        /// Opis do linii "Latki nocy (K3)".</summary>
        private static string O438ApplyPatch(Harmony h)
        {
            _tAtkArch = O438Type("RBMTacticAttackSplitArchers");
            _tAtkInf = O438Type("RBMTacticAttackSplitInfantry");
            _tAtkSkir = O438Type("RBMTacticAttackSplitSkirmishers");
            _tEmbolon = O438Type("RBMTacticEmbolon");
            _tDefInf = O438Type("RBMTacticDefendSplitInfantry");
            _tDefArch = O438Type("RBMTacticDefendSplitArchers");
            int atk = (_tAtkArch != null ? 1 : 0) + (_tAtkInf != null ? 1 : 0) + (_tAtkSkir != null ? 1 : 0) + (_tEmbolon != null ? 1 : 0);
            int want = 0, ok = 0;
            var miss = new StringBuilder(40);
            foreach (var t in new[] { _tDefInf, _tDefArch })
            {
                if (t == null) continue;
                want++;
                try
                {
                    var target = AccessTools.DeclaredMethod(t, "GetTacticWeight", Type.EmptyTypes);
                    if (target == null || target.ReturnType != typeof(float)) { miss.Append(' ').Append(t.Name); continue; }
                    h.Patch(target, postfix: new HarmonyMethod(typeof(Doctrine), nameof(O438SplitPost)) { priority = Priority.Last, after = new[] { "com.rbmai" } });
                    bool mine = false;
                    var info = Harmony.GetPatchInfo(target);
                    if (info != null) foreach (var p in info.Postfixes) if (p != null && p.owner == h.Id && p.PatchMethod != null && p.PatchMethod.Name == nameof(O438SplitPost)) mine = true;
                    if (mine) ok++; else miss.Append(' ').Append(t.Name);
                }
                catch (Exception e)
                {
                    miss.Append(' ').Append(t.Name).Append('(').Append(e.GetType().Name).Append(')');
                    RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Latka438 " + t.Name, e);
                }
            }
            _o438Patch = "splity obrony " + ok + "/" + want + (miss.Length > 0 ? " (NIE:" + miss + ")" : "") + ", plany ataku RBM " + atk + "/4";
            return _o438Patch;
        }

        private static Type O438Type(string name)
        {
            Type t = null;
            try { t = AccessTools.TypeByName(name) ?? AccessTools.TypeByName("RBMAI." + name); } catch { t = null; }
            return t != null && typeof(TacticComponent).IsAssignableFrom(t) ? t : null;
        }

        /// <summary>1.1: biezacy plan druzyny to plan ataku RBM (split albo klin). 0 nie, 1 AttackSplitArchers, 2 AttackSplitInfantry, 3 AttackSplitSkirmishers,
        /// 4 Embolon.</summary>
        private static int O438RbmAttack(TacticComponent tc)
        {
            if (tc == null) return 0;
            var t = tc.GetType();
            if (t == _tAtkArch && t != null) return 1;
            if (t == _tAtkInf && t != null) return 2;
            if (t == _tAtkSkir && t != null) return 3;
            if (t == _tEmbolon && t != null) return 4;
            return 0;
        }

        /// <summary>1.1 (OutshotAdvancing, glowny watek - OutshotRush.Tick): druzyna z >= FireDead zabitych od pociskow, nie trzyma 291, bez innego powodu -
        /// czy jej plan ataku RBM puszcza bieg. Klucz NIE: false i licznik "bylby" (raz na druzyne).</summary>
        private static bool O438RbmAdvancing(Team t, int ti, TacticComponent cur)
        {
            int k = O438RbmAttack(cur);
            if (k == 0) return false;
            if (!_o438Rbm)
            {
                if (!_o438RbmDry[ti]) { _o438RbmDry[ti] = true; _o438RbmDryTeams++; }
                return false;
            }
            _o438RbmNow[ti] = true;
            if (!_o438RbmEver[ti])
            {
                _o438RbmEver[ti] = true;
                _o438RbmTeams++;
                if (k == 1) _o438ArchN++; else if (k == 2) _o438InfN++; else if (k == 3) _o438SkirN++; else _o438EmbN++;
            }
            return true;
        }

        /// <summary>1.1 (MarchToContact.RushNowCount, glowny watek): druzyna idzie teraz pod ostrzalem z powodu planu ataku RBM.</summary>
        internal static bool O438RbmNow(Team t)
        {
            if (t == null) return false;
            int ti = t.TeamIndex;
            return ti >= 0 && ti < MaxTeams && _o438RbmNow[ti] && OutshotRush.Live(t);
        }

        // ------------------------------------------------------------ 1.2 przyrost strat od pociskow

        /// <summary>1.2: o ile wzrosl licznik zabitych od pociskow druzyny w ostatnich 30 s (probka co 2 s; przed zapelnieniem - od pierwszej probki).</summary>
        private static int O438Bleed(Team t)
        {
            if (t == null) return 0;
            int ti = t.TeamIndex;
            if (ti < 0 || ti >= MaxTeams || !ReferenceEquals(_o438Ref[ti], t)) return 0;
            int n = _o438N[ti];
            if (n <= 0) return 0;
            int old = n >= O438Ring ? _o438Hist[ti * O438Ring + (n % O438Ring)] : _o438Hist[ti * O438Ring];
            int d = _o438Cur[ti] - old;
            return d > 0 ? d : 0;
        }

        /// <summary>1.2 (MarchToContact, glowny watek): sygnal ostrzalu szyku ze strat - druzyna stracila >= FireDead od pociskow w 30 s, a pieszy wrog
        /// (szyk bez konnicy i konnych lucznikow - FootGap, ta sama miara co S2) jest blizej niz 200 m. 438-p (recenzja 438): dotad odleglosc z EnemyGap
        /// (najblizszy szyk wroga DOWOLNEGO rodzaju), wbrew opisowi i podpowiedzi MCM. Klucz NIE - false, dry = bylby sygnal (licznik "bylby" liczy wolajacy
        /// przez O438NoteLoss tylko tam, gdzie przy kluczu TAK byloby wejscie).</summary>
        internal static bool O438LossFire(Formation f, out bool dry)
        {
            dry = false;
            var t = f != null ? f.Team : null;
            if (t == null) return false;
            int bleed = O438Bleed(t);
            if (bleed < P440FireThr(t, bleed, 0)) return false;   // 440 (3.3): prog wedlug profilu wodza (klucz NIE / ZWYKLY - FireDead)
            float gap = FootGap(t, f);   // dopiero po progu strat - petla po szykach wroga tylko przy sygnale
            if (gap > O438Range || gap == float.MaxValue) return false;
            if (!_o438Loss) { dry = true; return false; }
            return true;
        }

        /// <summary>1.2 (MarchToContact): wejscie w bieg tylko dzieki sygnalowi ze strat. kind: 0 bez tarcz (291 C), 1 z tarczami (313b), 2 szarza (313y);
        /// dry - przy kluczu NIE bylby sygnal (szyko-takty).</summary>
        internal static void O438NoteLoss(int kind, bool dry)
        {
            if (dry) { _o438LossDry++; return; }
            if (kind == 0) _o438LossBare++; else if (kind == 1) _o438LossShield++; else _o438LossChg++;
        }

        /// <summary>1.2 (S2Tick, glowny watek): warunek S2 ze strat (gdy trafionych mniej niz S2Ratio). Klucz NIE - false i sekundy "bylby".</summary>
        private static bool O438S2Loss(Team t, float ratio, bool outshot, Formation inf, float gap)
        {
            if (!outshot || inf == null || gap > S2Range || ratio >= S2Ratio) return false;
            int bleed = O438Bleed(t);
            if (bleed < P440FireThr(t, bleed, 1)) return false;   // 440 (3.3): prog wedlug profilu wodza (klucz NIE / ZWYKLY - FireDead)
            if (!_o438Loss) { _o438S2LossDry++; return false; }
            _o438S2LossSec += TickEvery;
            return true;
        }

        // ------------------------------------------------------------ 1.3 postfiks splitow obrony RBM (tick druzyn)

        /// <summary>1.3: postfiks GetTacticWeight RBMTacticDefendSplitInfantry / RBMTacticDefendSplitArchers (tick druzyn, Priority.Last po com.rbmai):
        /// druzyna przegrywa wymiane ognia (S2 zywe albo puszczona 291e / 320d) - waga splitu przycieta do najwyzszej wagi obrony gry tej druzyny. Bez alokacji.</summary>
        private static void O438SplitPost(object __instance, ref float __result)
        {
            P440Los(__instance, ref __result);   // 440 (3.4): los planu RBM PRZED przycieciem 1.3 - sufit obrony gry zostaje sufitem
            if (!_on) return;
            try
            {
                var tc = __instance as TacticComponent;
                var team = tc != null ? tc.Team : null;
                if (team == null) return;
                int ti = team.TeamIndex;
                if (ti < 0 || ti >= MaxTeams) return;
                float r = __result;
                if (!(r > 0f) || float.IsInfinity(r)) return;
                bool s2 = _s2Live[ti] && ReferenceEquals(_s2Ref[ti], team);
                bool dh = ReferenceEquals(_dhRef[ti], team);
                bool fire = dh && _dhOff[ti] == 3;
                // 438-p (recenzja 438): po wyjsciu z S2 (S2Leave - 10 s ciszy przed zwarciem) ta sama regula co dla obrony gry (291d DhTick / DhDefPost:
                // _dhS2Was - gra decyduje sama). Gdy gra mowi obrona NIE, wszystkie cztery taktyki obrony gry daja 0 (vanilla GetTacticWeight DefensiveEngagement,
                // DefensiveLine, DefensiveRing, HoldChokePoint: "!IsDefenseApplicable -> 0"), wiec split tez 0. Dotad split wracal do wagi 10 / 5 RBM (RBM nie
                // pyta o IsDefenseApplicable) i druzyna znow stawala pod ostrzalem: S2 -> atak -> split -> S2. Gdy gra mowi obrona TAK - bez przyciecia (jak dotad).
                bool post = false;
                if (!s2 && !fire && dh && _dhS2Was[ti]) { var ai = team.TeamAI; post = ai != null && !ai.IsDefenseApplicable; }
                if (!s2 && !fire && !post) return;
                float cap;
                if (post) cap = 0f;
                else if (dh)
                {
                    cap = 0f;
                    for (int k = 0; k < DhK; k++) { float v = _dhW[ti * DhK + k]; if (v > cap) cap = v; }
                }
                else cap = S2Hold;   // bez wag 291 - waga gry ok. 1 x S2
                if (r <= cap) return;
                if (!_o438Split) { Interlocked.Increment(ref _o438SplitDry); return; }
                __result = cap;
                if (s2) Interlocked.Increment(ref _o438SplitS2);
                else if (post) Interlocked.Increment(ref _o438SplitPost);
                else Interlocked.Increment(ref _o438SplitFire);
            }
            catch { Interlocked.Increment(ref _postErr); }
        }

        private static bool O438DefSplit(TacticComponent tc)
        {
            if (tc == null) return false;
            var t = tc.GetType();
            return t != null && (t == _tDefInf || t == _tDefArch);
        }

        // ------------------------------------------------------------ takt (glowny watek)

        /// <summary>BattleSense.OnMissionTick (po OutshotRush.Tick): historia strat od pociskow wszystkich druzyn co 2 s, liczniki 1.1 / 1.3 i probka 1.4.</summary>
        internal static void O438Tick(Mission m, float dt)
        {
            if (m == null) return;
            var w = _o438M;
            if (w == null || !ReferenceEquals(w.Target, m))
            {
                for (int i = 0; i < MaxTeams; i++) O438ClearTeam(i);
                _o438M = new WeakReference(m);
                _o438Acc = _o438ProbeAcc = 0f;
            }
            if (!m.IsFieldBattle || m.IsNavalBattle || m.MissionEnded || !m.IsDeploymentFinished) return;
            _o438Acc += dt;
            _o438ProbeAcc += dt;
            if (_o438Acc >= O438Every)
            {
                float step = _o438Acc;
                _o438Acc = 0f;
                var c = Settings.Current;
                if (c != null) O438Keys(c);
                var teams = m.Teams;
                int n = teams != null ? teams.Count : 0;
                int live = 0;
                for (int i = 0; i < n; i++)
                {
                    Team t = null;
                    try { t = teams[i]; } catch { t = null; }
                    if (!MissionTeams.Valid(t)) continue;
                    int ti = t.TeamIndex;
                    if (ti < 0 || ti >= MaxTeams) continue;
                    try
                    {
                        if (!ReferenceEquals(_o438Ref[ti], t)) { O438ClearHist(ti); _o438Ref[ti] = t; }   // 438-p: bez flag 1.1
                        var q = t.QuerySystem;
                        int d = q != null ? q.DeathByRangedCount : 0;
                        _o438Hist[ti * O438Ring + (_o438N[ti] % O438Ring)] = d;
                        _o438N[ti]++;
                        _o438Cur[ti] = d;
                        if (O438RbmNow(t)) { live++; _o438RbmSec += step; }
                        // 1.3: druzyna przegrywa wymiane ognia (S2 / 291e; 438-p: takze po S2, gdy gra mowi obrona NIE) - czas w splicie obrony RBM, wyjscia i powroty
                        bool outshot = (_s2Live[ti] && ReferenceEquals(_s2Ref[ti], t)) || FireReleased(t);
                        if (!outshot && ReferenceEquals(_dhRef[ti], t) && _dhS2Was[ti]) { var ai = t.TeamAI; outshot = ai != null && !ai.IsDefenseApplicable; }
                        var cur = CurTactic(t);
                        bool split = O438DefSplit(cur);
                        if (outshot && split) _o438SplitSec += step;
                        if (outshot && _o438SplitWas[ti] && !split)
                        {
                            _o438SplitLeft++;
                            if (cur is TacticFullScaleAttack || cur is TacticCharge) _o438SplitLeftAtk++;
                        }
                        if (outshot && split && !_o438SplitWas[ti] && _o438N[ti] > 1) _o438SplitBack++;   // 438-p: powrot do splitu (wahadlo); bez pierwszej probki
                        _o438SplitWas[ti] = split;
                    }
                    catch (Exception e) { _o438Stum++; RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.O438 druzyna", e); }
                }
                if (live > _o438PeakTeams) _o438PeakTeams = live;
                // ozywiony kod (zasada 4 CLAUDE.md): ile szykow naraz w biegu pod ostrzalem
                try
                {
                    int rbm;
                    int now = MarchToContact.RushNowCount(out rbm);
                    if (now > 0) { _o438RushTicks++; _o438RushSum += now; }
                    if (now > _o438PeakRush) _o438PeakRush = now;
                    if (rbm > _o438PeakRushRbm) _o438PeakRushRbm = rbm;
                    _o438RushRbmSec += rbm * step;
                }
                catch (Exception e) { _o438Stum++; RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.O438 naraz", e); }
            }
            if (_o438ProbeAcc >= O438ProbeEvery)
            {
                _o438ProbeAcc = 0f;
                try { MarchToContact.RushProbe(O438Probe); }
                catch (Exception e) { _o438Stum++; RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.O438 probka", e); }
            }
        }

        // ------------------------------------------------------------ 1.4 co hamuje bieg (glowny watek, sam odczyt)

        private static readonly Action<Formation> O438Probe = f =>
        {
            if (f == null || f.CountOfUnits <= 0) return;
            try
            {
                _pbForms++;
                var ae = f.ArrangementOrder.OrderEnum;
                if (ae == ArrangementOrder.ArrangementOrderEnum.Loose) _pbLoose++;
                else if (ae == ArrangementOrder.ArrangementOrderEnum.ShieldWall) _pbWall++;
                else _pbOther++;
                float integ = 0f;
                try { integ = f.CachedFormationIntegrityData.AverageMaxUnlimitedSpeedExcludeFarAgents; } catch { integ = 0f; }
                if (integ > 0f) { _pbIntegrity += integ; _pbIntegrityN++; }
                _pbStep = Math.Max(1, (f.CountOfUnits + O438ProbeMen - 1) / O438ProbeMen);
                _pbSeen = 0;
                f.ApplyActionOnEachUnit(O438Man);
            }
            catch (Exception e) { _o438Stum++; RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.O438 szyk", e); }
        };

        private static readonly Action<Agent> O438Man = a =>
        {
            if (a == null) return;
            int k = _pbSeen++;
            if (k % _pbStep != 0) return;
            try
            {
                if (!a.IsActive() || !a.IsHuman || a.MountAgent != null) return;
                float v = a.GetCurrentVelocity().Length;
                float full = 0f;
                try { full = a.GetMaximumForwardUnlimitedSpeed(); } catch { full = 0f; }
                _pbMen++;
                _pbSpeed += v;
                _pbFull += full;
                if (v < 2f) _pbSlow++;
                float cap = AiBreath.CapOf(a);
                if (cap > 0f) { _pbBreath++; _pbCap += cap; }
                if (ShieldsUp.IsMarked(a)) _pbMarked++;
                if (a.WalkMode) _pbWalk++;
            }
            catch { _o438Stum++; }
        };

        // ------------------------------------------------------------ linia

        private static string P438(int a, int n) { return n > 0 ? ((int)Math.Round(100.0 * a / n)).ToString(O438Inv) + "%" : "-"; }

        /// <summary>Battle291.Write (HorsePursuit.End, po Doctrine.End - stan druzyn wyczyszczony, liczniki zostaja): linia "Ostrzal piechoty (438)".</summary>
        internal static string O438Line(string kind)
        {
            var c = Settings.Current;
            bool k1 = c != null && c.OutshotRushInRbmAttackPlans, k2 = c != null && c.OutshotTriggerByMissileLosses, k3 = c != null && c.DefenderLeavesRbmSplitWhenOutshot;
            var sb = new StringBuilder(1400);
            sb.Append("Ostrzal piechoty (438): misja ").Append(kind ?? "-")
              .Append(" | klucze: bieg w planach ataku RBM ").Append(k1 ? "TAK" : "NIE").Append(", wyzwalacz stratami od pociskow ").Append(k2 ? "TAK" : "NIE")
              .Append(", obronca wychodzi ze splitu RBM ").Append(k3 ? "TAK" : "NIE")
              .Append(" | 1.1 druzyn pod ostrzalem w planie ataku RBM ").Append(_o438RbmTeams)
              .Append(" (AttackSplitArchers ").Append(_o438ArchN).Append(", AttackSplitInfantry ").Append(_o438InfN).Append(", AttackSplitSkirmishers ").Append(_o438SkirN)
              .Append(", Embolon ").Append(_o438EmbN).Append("), druzyno-sekund ").Append(_o438RbmSec.ToString("0", O438Inv))
              .Append(", bylby (klucz NIE) ").Append(_o438RbmDryTeams)
              .Append("; naraz: druzyn maks ").Append(_o438PeakTeams).Append(", szykow w biegu maks ").Append(_o438PeakRush)
              .Append(" (w tym z tarczami 313b z planu RBM ").Append(_o438PeakRushRbm).Append("), sr. ").Append(_o438RushTicks > 0 ? ((double)_o438RushSum / _o438RushTicks).ToString("0.0", O438Inv) : "-")
              .Append(" przy biegu, szyko-sekund biegu 313b z planu RBM ").Append(_o438RushRbmSec.ToString("0", O438Inv))
              .Append(" | 1.2 wejsc w bieg tylko od strat (>= ").Append(FireDead).Append(" od pociskow w ").Append(O438LossWin.ToString("0", O438Inv))
              .Append(" s, pieszy wrog <= ").Append(O438Range.ToString("0", O438Inv)).Append(" m): bez tarcz ").Append(_o438LossBare).Append(", z tarczami ").Append(_o438LossShield)
              .Append(", szarza bez capu (szyko-takty) ").Append(_o438LossChg).Append(", bylby (klucz NIE: wejsc w bieg + szyko-takty szarzy) ").Append(_o438LossDry)
              .Append("; S2 warunek tylko od strat ").Append(_o438S2LossSec.ToString("0", O438Inv)).Append(" s, wejsc w S2 od strat ").Append(_o438S2Loss)
              .Append(", bylby (klucz NIE, takty) ").Append(_o438S2LossDry)
              .Append(" | 1.3 split obrony RBM przyciety do wagi obrony gry: decyzji przy S2 ").Append(Volatile.Read(ref _o438SplitS2))
              .Append(", przy bilansie ognia 291e ").Append(Volatile.Read(ref _o438SplitFire)).Append(", po S2 (gra obrona NIE) ").Append(Volatile.Read(ref _o438SplitPost))
              .Append(", bylby (klucz NIE) ").Append(Volatile.Read(ref _o438SplitDry))
              .Append("; przegrany ostrzal w splicie obrony ").Append(_o438SplitSec.ToString("0", O438Inv)).Append(" s, wyjsc ze splitu ").Append(_o438SplitLeft)
              .Append(" (na atak ").Append(_o438SplitLeftAtk).Append("), powrotow do splitu ").Append(_o438SplitBack).Append("; latka ").Append(_o438Patch)
              .Append(" | 1.4 co hamuje bieg (probka co ").Append(O438ProbeEvery.ToString("0", O438Inv)).Append(" s, do ").Append(O438ProbeMen).Append(" ludzi na szyk): szyko-probek ")
              .Append(_pbForms).Append(" (Loose ").Append(_pbLoose).Append(", ShieldWall ").Append(_pbWall).Append(", inny ").Append(_pbOther).Append("), ludzi ").Append(_pbMen)
              .Append(", sr. predkosc ").Append(_pbMen > 0 ? (_pbSpeed / _pbMen).ToString("0.0", O438Inv) : "-")
              .Append(" m/s (pelny bieg ").Append(_pbMen > 0 ? (_pbFull / _pbMen).ToString("0.0", O438Inv) : "-")
              .Append(", szyku ").Append(_pbIntegrityN > 0 ? (_pbIntegrity / _pbIntegrityN).ToString("0.0", O438Inv) : "-")
              .Append("), wolniej niz 2 m/s ").Append(P438(_pbSlow, _pbMen)).Append(", cap tchu ").Append(P438(_pbBreath, _pbMen))
              .Append(" (sr. ").Append(_pbBreath > 0 ? (_pbCap / _pbBreath).ToString("0.0", O438Inv) : "-").Append(" m/s), tarcza 140 ").Append(P438(_pbMarked, _pbMen))
              .Append(", WalkMode ").Append(P438(_pbWalk, _pbMen))
              .Append(" | potkniecia ").Append(_o438Stum).Append('.');
            return sb.ToString();
        }
    }
}
