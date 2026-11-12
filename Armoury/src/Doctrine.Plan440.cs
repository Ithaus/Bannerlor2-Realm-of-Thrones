using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 440 = PAKIET 3 audytu AI bitwy (docs/audyty-2026-11-11/AUDYT-AI-BITWA.md rozdz. 3 "Wodz AI: rozne otwarcia i charakter"; Jeff 11.11 "rob wszystko po kolei
    /// tak jak radzisz", "aby bitwa nie byla wciaz taka sama ... a AI bardziej kombinowalo"). Tylko decyzje, zadnych bonusow do statystyk. Trzy klucze (domyslnie
    /// TAK), NIE = zachowanie co do znaku jak dotad (zostaje sam pomiar w linii "Wodz AI (440)"). Bitwa polowa (nie morska), druzyny AI bez druzyny gracza.
    ///
    /// 3.1 + 3.4 BattlePlanVariety ("Battle Plan Variety"). DOWOD (audyt rozdz. 3 i 4): ta sama armia ma zawsze ten sam plan otwarcia - wagi planow sa
    ///     deterministyczne (mnozniki doktryny kultury, RBM), a plan otwarcia gry wybiera ResetTactic w AfterStart przy skladzie 0 (A/Doctrine.cs:52-54,
    ///     Hold291 "pierwsza taktyka z ResetTactic AfterStart stoi do konca rozstawienia"), potem MakeDecision z histereza x1.5 (mb TeamAIComponent.MakeDecision).
    ///     Zmiana: LOS BITWY - w pierwszym takcie po rozstawieniu (nie w AfterStart: sklad 0) raz na druzyne mnoznik 0.8-1.25 (rozklad rowny w logarytmie) dla
    ///     KAZDEGO typu planu osobno: gra (FullScaleAttack, Charge, RangedHarrass, FrontalCavalryCharge, DefensiveLine, DefensiveEngagement, DefensiveRing,
    ///     HoldChokePoint - w istniejacych postfiksach GetTacticWeight doktryny, po mnozniku kultury) i RBM (3.4: AttackSplitArchers / AttackSplitInfantry /
    ///     AttackSplitSkirmishers / Embolon - nowy postfiks, DeclaredMethod na klasie deklarujacej, sprawdzone w rbmai :559 :1358 :1746 :2068; DefendSplitInfantry /
    ///     DefendSplitArchers - w postfiksie 438 PRZED przycieciem 1.3, wiec sufit obrony gry zostaje sufitem). Zaraz po rzucie druzyna wybiera plan otwarcia od
    ///     nowa (TeamAIComponent.ResetTactic(false) z glownego watku, jak gra w rozstawieniu - bez histerezy wobec wyboru przy skladzie 0); bez tego rzut
    ///     (najwyzej x1.56 miedzy dwoma planami) prawie nigdy nie przebije histerezy x1.5. Pozne druzyny DR - sam rzut, bez ponownego wyboru.
    /// 3.2 + 3.3 CommanderPersonalityInBattle ("Commander Personality In Battle"). Profil wodza z cech bohatera Team.GeneralAgent: Valor minus Calculating
    ///     > 0 PORYWCZY, < 0 OSTROZNY, 0 albo brak bohatera ZWYKLY (ROT_lords.xml: ostroznych ok. 43%, porywczych ok. 8%). Profil przesuwa progi pakietow 1 i 2:
    ///     438 (1.2) wyzwalacz biegu i S2 od strat od pociskow - porywczy od 1 zabitego w 30 s, ostrozny od 3 (zwykly 2 = FireDead); 439 (2.1) obronca w trzymaniu
    ///     puszcza Engage - porywczy przy szyku wroga w 40 m albo 25% piechoty wrecz, ostrozny dopiero przy 50% wrecz (25 m bez zmian), zwykly 25 m / 35%.
    ///     Klucz NIE - progi jak dotad, profil tylko w linii (liczniki "bylby").
    /// 3.5 CommandConfusionAfterCommanderDeath ("Command Confusion After Commander Death"). Wodz (Team.GeneralAgent z chwili rzutu) przestaje byc aktywny
    ///     (zabity, nieprzytomny, uciekl) -> przez 45 s druzyna nie zmienia planu (prefiks TeamAIComponent.MakeDecision, DeclaredMethod; RBM go nie latuje -
    ///     audyt), potem lista planow wedlug najlepszego ZYWEGO taktyka druzyny (umiejetnosc Tactics bohaterow w ActiveAgents, progi rady wojennej 30 / 60):
    ///     gdy nizszy prog niz na starcie - plany ponad jego prog maja wage 0 (nic nie jest dodawane ani zdejmowane z listy; tylko gdy na liscie zostaje plan
    ///     w jego progu). Zamrozenie NIE obejmuje przetrwania: druzyna z S2 (141) zywym, puszczona bilansem ognia 291e / 320d albo tracaca od pociskow
    ///     (>= FireDead zabitych w 30 s, 438) decyduje jak dotad - S2 / 291e potrzebuja MakeDecision (wybor taktyki, DhDefPost w CheckIsDefenseApplicable),
    ///     a 313b (OutshotRush) dziala na szykach niezaleznie od decyzji; linia liczy sekundy S2 i biegu 313b w zamecie. Bez biezacego planu - decyzja jak dotad.
    ///
    /// Watki: rzut, profil, smierc wodza, zamrozenie i nastepca w glownym watku (P440Tick z Doctrine.Tick, co 0.5 s, pierwszy raz w pierwszej klatce po
    /// rozstawieniu - przed linia start 437 po 1 s); postfiksy wag i prefiks decyzji w ticku druzyn - tylko odczyt tablic (referencja druzyny zapisywana PO
    /// tablicach) + Interlocked, bez alokacji. Potkniecia liczone (_p4Stum), bez globalnego wylacznika.
    /// </summary>
    internal static partial class Doctrine
    {
        internal const float P440Lo = 0.8f, P440Hi = 1.25f, P440Every = 0.5f, P440NoDeploy = 10f, P440OpenAfter = 1f, P440Confuse = 45f;
        internal const float P440RashGap = 40f, P440RashMelee = 0.25f, P440CalmMelee = 0.5f;
        internal const int P440RashFire = 1, P440CalmFire = 3, P440TierMid = 30, P440TierHigh = 60;
        private const int BpK = 14;
        private static readonly string[] BpName = { "FullScaleAttack", "Charge", "RangedHarrass", "FrontalCavalryCharge", "DefensiveLine", "DefensiveEngagement",
                                                   "DefensiveRing", "HoldChokePoint", "AttackSplitArchers", "AttackSplitInfantry", "AttackSplitSkirmishers", "Embolon",
                                                   "DefendSplitInfantry", "DefendSplitArchers" };
        // prog rady wojennej (WarCouncil): 0 tlum (< 30: Charge, FullScaleAttack, obrona gry), 1 od 30 (zestaw RBM, RangedHarrass doktryny), 2 od 60 (konnica, pierscien, przesmyk)
        private static readonly byte[] BpTier = { 0, 0, 1, 2, 0, 0, 2, 2, 1, 1, 1, 1, 1, 1 };
        private static readonly string[] CpName = { "OSTROZNY", "ZWYKLY", "PORYWCZY" };
        private static readonly IFormatProvider P4Inv = CultureInfo.InvariantCulture;

        // ---- klucze (kopie z glownego watku; postfiksy / prefiks czytaja)
        private static volatile bool _bpKey, _cpKey, _ccKey, _p440Live;

        // ---- stan druzyn: postfiksy czytaja _p4Ref / _bpMul / _cpProf / _ccTier / _ccFrozen (pisze glowny watek, _p4Ref po tablicach)
        private static readonly Team[] _p4Ref = new Team[MaxTeams];
        private static readonly float[] _bpMul = new float[MaxTeams * BpK];
        private static readonly sbyte[] _cpProf = new sbyte[MaxTeams];
        private static readonly sbyte[] _ccTier = new sbyte[MaxTeams];
        private static readonly bool[] _ccFrozen = new bool[MaxTeams];
        // ---- stan druzyn tylko glownego watku
        private static readonly string[] _p4Gen = new string[MaxTeams];
        private static readonly string[] _p4Open0 = new string[MaxTeams];
        private static readonly string[] _p4Open = new string[MaxTeams];
        private static readonly float[] _p4RollAt = new float[MaxTeams];
        private static readonly bool[] _p4LateT = new bool[MaxTeams];
        private static readonly Agent[] _ccGen = new Agent[MaxTeams];
        private static readonly int[] _ccTac0 = new int[MaxTeams];
        private static readonly float[] _ccAt = new float[MaxTeams];
        private static readonly bool[] _ccDone = new bool[MaxTeams];
        private static readonly string[] _ccNote = new string[MaxTeams];
        private static bool _p4Started;
        private static float _p4Acc;

        // ---- latki
        private static AccessTools.FieldRef<TeamAIComponent, Team> _fAiTeam;
        private static FieldInfo _fAvail;
        private static string _p4Patch = "nie wpiete";

        // ---- liczniki misji (P440Reset w Battle291.Begin); postfiksy - Interlocked
        private static int _p4Teams, _p4Late, _p4Reset, _p4ResetChg, _p4Stum, _bpCalls;
        private static int _cpN0, _cpN1, _cpN2, _cpNoHero;
        private static int _cpFireEarly, _cpFireLate, _cpFireDry, _cpS2Early, _cpS2Late, _cpS2Dry;
        private static int _cpGapEarly, _cpGapDry, _cpMeleeEarly, _cpMeleeLate;
        private static int _ccDead, _ccDeadDry, _ccUnfreeze, _ccRestrict, _ccSkip, _ccZero;
        private static float _ccFrozenSec, _ccOverSec, _ccS2Sec, _ccRushSec;
        private static readonly List<string> _p4Snap = new List<string>();
        // histogram planu otwarcia od startu procesu (strona + plan) - "ta sama armia w 6 biegach >= 3 rozne otwarcia" (audyt, pomiar pakietu 3)
        private static readonly Dictionary<string, int> _p4Hist = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Battle291.Begin (AfterStart): liczniki linii 440 od zera (stan druzyn czysci Doctrine.Clear).</summary>
        internal static void P440Reset()
        {
            _p4Teams = _p4Late = _p4Reset = _p4ResetChg = _p4Stum = 0;
            Interlocked.Exchange(ref _bpCalls, 0);
            _cpN0 = _cpN1 = _cpN2 = _cpNoHero = 0;
            _cpFireEarly = _cpFireLate = _cpFireDry = _cpS2Early = _cpS2Late = _cpS2Dry = 0;
            Interlocked.Exchange(ref _cpGapEarly, 0); Interlocked.Exchange(ref _cpGapDry, 0);
            _cpMeleeEarly = _cpMeleeLate = 0;
            _ccDead = _ccDeadDry = _ccUnfreeze = _ccRestrict = 0;
            Interlocked.Exchange(ref _ccSkip, 0); Interlocked.Exchange(ref _ccZero, 0);
            _ccFrozenSec = _ccOverSec = _ccS2Sec = _ccRushSec = 0f;
            _p4Snap.Clear();
            var c = Settings.Current;
            if (c != null) P440Keys(c);
        }

        private static void P440Keys(Settings c)
        {
            _bpKey = c.BattlePlanVariety;
            _cpKey = c.CommanderPersonalityInBattle;
            _ccKey = c.CommandConfusionAfterCommanderDeath;
        }

        /// <summary>Doctrine.Clear (Begin / End): stan misji 440 bez druzyn.</summary>
        private static void P440ClearMission()
        {
            _p440Live = false;
            _p4Started = false;
            _p4Acc = 0f;
            for (int i = 0; i < MaxTeams; i++) P440ClearTeam(i);
        }

        private static void P440ClearTeam(int i)
        {
            _p4Ref[i] = null;   // najpierw referencja - postfiks przestaje czytac tablice druzyny
            for (int k = 0; k < BpK; k++) _bpMul[i * BpK + k] = 1f;
            _cpProf[i] = 1; _ccTier[i] = -1; _ccFrozen[i] = false;
            _p4Gen[i] = null; _p4Open0[i] = null; _p4Open[i] = null; _p4RollAt[i] = 0f; _p4LateT[i] = false;
            _ccGen[i] = null; _ccTac0[i] = 0; _ccAt[i] = -1f; _ccDone[i] = false; _ccNote[i] = null;
        }

        // ------------------------------------------------------------ latki (raz na proces - ApplyMissionPatches, po O438ApplyPatch: typy RBM)

        private static string P440ApplyPatch(Harmony h)
        {
            var miss = new StringBuilder(60);
            int want = 0, ok = 0;
            var post = new HarmonyMethod(typeof(Doctrine), nameof(P440RbmPost)) { priority = Priority.Last, after = new[] { "com.rbmai" } };
            foreach (var t in new[] { _tAtkArch, _tAtkInf, _tAtkSkir, _tEmbolon })
            {
                if (t == null) continue;
                want++;
                if (P440PatchOne(h, t, "GetTacticWeight", typeof(float), post, nameof(P440RbmPost), false)) ok++; else miss.Append(' ').Append(t.Name);
            }
            try { _fAiTeam = AccessTools.FieldRefAccess<TeamAIComponent, Team>("Team"); } catch { _fAiTeam = null; }
            try { _fAvail = AccessTools.Field(typeof(TeamAIComponent), "_availableTactics"); } catch { _fAvail = null; }
            string dec;
            if (_fAiTeam == null || _fCur == null) dec = "NIE (brak pola Team / _currentTactic)";
            else dec = P440PatchOne(h, typeof(TeamAIComponent), "MakeDecision", typeof(void), new HarmonyMethod(typeof(Doctrine), nameof(P440DecPre)), nameof(P440DecPre), true)
                       ? "TAK" : "NIE";
            _p4Patch = "plany ataku RBM " + ok + "/" + want + (miss.Length > 0 ? " (NIE:" + miss + ")" : "") + ", splity obrony RBM w latce 438, MakeDecision " + dec
                       + ", lista taktyk " + (_fAvail != null ? "TAK" : "NIE");
            return _p4Patch;
        }

        private static bool P440PatchOne(Harmony h, Type t, string name, Type ret, HarmonyMethod fix, string fixName, bool prefix)
        {
            try
            {
                var target = AccessTools.DeclaredMethod(t, name, Type.EmptyTypes);
                if (target == null || target.ReturnType != ret) return false;
                if (prefix) h.Patch(target, prefix: fix); else h.Patch(target, postfix: fix);
                var info = Harmony.GetPatchInfo(target);
                if (info == null) return false;
                foreach (var p in prefix ? info.Prefixes : info.Postfixes)
                    if (p != null && p.owner == h.Id && p.PatchMethod != null && p.PatchMethod.Name == fixName) return true;
                return false;
            }
            catch (Exception e)
            {
                RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Latka440 " + t.Name + "." + name, e);
                return false;
            }
        }

        // ------------------------------------------------------------ postfiksy wag i prefiks decyzji (tick druzyn - tylko odczyt)

        private static int P440Slot(TacticComponent tc)
        {
            var t = tc.GetType();
            if (t == typeof(TacticFullScaleAttack)) return 0;
            if (t == typeof(TacticCharge)) return 1;
            if (t == typeof(TacticRangedHarrassmentOffensive)) return 2;
            if (t == typeof(TacticFrontalCavalryCharge)) return 3;
            if (t == typeof(TacticDefensiveLine)) return 4;
            if (t == typeof(TacticDefensiveEngagement)) return 5;
            if (t == typeof(TacticDefensiveRing)) return 6;
            if (t == typeof(TacticHoldChokePoint)) return 7;
            if (t == _tAtkArch && t != null) return 8;
            if (t == _tAtkInf && t != null) return 9;
            if (t == _tAtkSkir && t != null) return 10;
            if (t == _tEmbolon && t != null) return 11;
            if (t == _tDefInf && t != null) return 12;
            if (t == _tDefArch && t != null) return 13;
            return -1;
        }

        /// <summary>3.1 / 3.4 / 3.5 (postfiks GetTacticWeight - PostShock / PostShot / PostHorse / PostHold po Mul, O438SplitPost przed przycieciem, P440RbmPost):
        /// waga x los druzyny dla typu planu; po zamecie plan ponad prog nastepcy - 0. Bez alokacji.</summary>
        private static void P440Los(object inst, ref float r)
        {
            if (!_p440Live) return;
            try
            {
                var tc = inst as TacticComponent;
                var team = tc != null ? tc.Team : null;
                if (team == null) return;
                int ti = team.TeamIndex;
                if (ti < 0 || ti >= MaxTeams || !ReferenceEquals(_p4Ref[ti], team)) return;
                if (float.IsNaN(r) || float.IsInfinity(r)) return;
                int k = P440Slot(tc);
                if (k < 0) return;
                sbyte tier = _ccTier[ti];
                if (tier >= 0 && BpTier[k] > tier)
                {
                    if (r > 0f) { r = 0f; Interlocked.Increment(ref _ccZero); }
                    return;
                }
                if (!_bpKey) return;
                float f = _bpMul[ti * BpK + k];
                if (f == 1f) return;
                r *= f;
                Interlocked.Increment(ref _bpCalls);
            }
            catch { Interlocked.Increment(ref _postErr); }
        }

        /// <summary>3.4: postfiks GetTacticWeight planow ataku RBM (Priority.Last po com.rbmai).</summary>
        private static void P440RbmPost(object __instance, ref float __result) { P440Los(__instance, ref __result); }

        /// <summary>3.5: prefiks TeamAIComponent.MakeDecision - druzyna w zamecie po smierci wodza nie zmienia planu (biezacy plan zostaje; bez planu - jak dotad).</summary>
        private static bool P440DecPre(TeamAIComponent __instance)
        {
            if (!_ccKey || !_p440Live || __instance == null) return true;
            try
            {
                var fr = _fAiTeam;
                var team = fr != null ? fr(__instance) : null;
                if (team == null) return true;
                int ti = team.TeamIndex;
                if (ti < 0 || ti >= MaxTeams || !_ccFrozen[ti] || !ReferenceEquals(_p4Ref[ti], team)) return true;
                var fc = _fCur;
                if (fc == null || fc(__instance) == null) return true;
                Interlocked.Increment(ref _ccSkip);
                return false;
            }
            catch { Interlocked.Increment(ref _postErr); return true; }
        }

        // ------------------------------------------------------------ progi pakietow 1 i 2 wedlug profilu (3.3)

        private static sbyte P440Prof(Team t)
        {
            if (t == null) return 1;
            int ti = t.TeamIndex;
            return ti >= 0 && ti < MaxTeams && ReferenceEquals(_p4Ref[ti], t) ? _cpProf[ti] : (sbyte)1;
        }

        /// <summary>438 (glowny watek - O438LossFire kind 0, O438S2Loss kind 1): prog zabitych od pociskow w 30 s wedlug profilu. Klucz NIE - FireDead
        /// (licznik "bylby", gdy profil zmienilby wynik).</summary>
        private static int P440FireThr(Team t, int bleed, int kind)
        {
            sbyte p = P440Prof(t);
            int adj = p == 2 ? P440RashFire : (p == 0 ? P440CalmFire : FireDead);
            if (adj == FireDead) return FireDead;
            bool b = bleed >= FireDead, a = bleed >= adj;
            if (a != b)
            {
                if (!_cpKey) { if (kind == 0) _cpFireDry++; else _cpS2Dry++; }
                else if (a) { if (kind == 0) _cpFireEarly++; else _cpS2Early++; }
                else { if (kind == 0) _cpFireLate++; else _cpS2Late++; }
            }
            return _cpKey ? adj : FireDead;
        }

        /// <summary>439 (H439Tick, glowny watek - od 439-p): odleglosc zwolnienia od PIESZEGO wroga - porywczy 40 m, reszta 25 m (S1ContactGap). Tylko odczyt.</summary>
        private static float P440Gap(Team t, int ti)
        {
            return _cpKey && ReferenceEquals(_p4Ref[ti], t) && _cpProf[ti] == 2 ? P440RashGap : S1ContactGap;
        }

        /// <summary>439 (postfiks): pieszy szyk wroga w 25-40 m (odleglosc z glownego watku, 439-p) - przy kluczu NIE licznik "bylby" dla porywczego.</summary>
        private static void P440GapDry(Team t, int ti, float d)
        {
            if (!_cpKey && d <= P440RashGap && ReferenceEquals(_p4Ref[ti], t) && _cpProf[ti] == 2) Interlocked.Increment(ref _cpGapDry);
        }

        /// <summary>439 (H439Tick, glowny watek): prog udzialu piechoty wrecz - porywczy 25%, ostrozny 50%, zwykly 35%. Klucz NIE - 35%.</summary>
        private static float P440MeleeShare(Team t)
        {
            if (!_cpKey) return H439MeleeShare;
            sbyte p = P440Prof(t);
            return p == 2 ? P440RashMelee : (p == 0 ? P440CalmMelee : H439MeleeShare);
        }

        private static void P440NoteMelee(bool adj, bool b)
        {
            if (adj && !b) _cpMeleeEarly++;
            else if (!adj && b) _cpMeleeLate++;
        }

        // ------------------------------------------------------------ takt (glowny watek, Doctrine.Tick - kazda klatka, wlasny zegar)

        internal static void P440Tick(Mission m, float dt)
        {
            var w = _m;
            if (m == null || w == null || !ReferenceEquals(w.Target, m)) return;
            var c = Settings.Current;
            if (c == null) return;
            P440Keys(c);
            if (!m.IsFieldBattle || m.IsNavalBattle || m.MissionEnded) return;
            _p440Live = _bpKey || _ccKey;
            float now = m.CurrentTime;
            bool dep;
            try { dep = m.IsDeploymentFinished || (m.Mode != MissionMode.Deployment && now >= P440NoDeploy); } catch { dep = false; }
            if (!dep) return;
            _p4Acc += dt;
            if (_p4Started && _p4Acc < P440Every) return;
            float step = _p4Started ? _p4Acc : 0f;
            bool late = _p4Started;
            _p4Acc = 0f;
            _p4Started = true;
            var teams = m.Teams;
            int n = teams != null ? teams.Count : 0;
            for (int i = 0; i < n; i++)
            {
                Team t = null;
                try { t = teams[i]; } catch { t = null; }
                if (!MissionTeams.Valid(t) || t.IsPlayerTeam || !t.HasTeamAi) continue;
                if (t.Side != BattleSideEnum.Attacker && t.Side != BattleSideEnum.Defender) continue;
                int ti = t.TeamIndex;
                if (ti < 0 || ti >= MaxTeams) continue;
                try
                {
                    if (!ReferenceEquals(_p4Ref[ti], t)) P440Roll(m, t, ti, c, now, late);
                    P440Team(m, t, ti, now, step);
                }
                catch (Exception e) { _p4Stum++; RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Plan440 druzyna", e); }
            }
        }

        /// <summary>3.1 / 3.2: rzut losu i profil wodza (raz na druzyne), ponowny wybor planu otwarcia.</summary>
        private static void P440Roll(Mission m, Team t, int ti, Settings c, float now, bool late)
        {
            P440ClearTeam(ti);
            // 3.2 profil z cech wodza
            Agent g = null;
            try { g = t.GeneralAgent; } catch { g = null; }
            int v = 0, cal = 0;
            bool hero = false;
            string gn = "brak";
            if (g != null)
            {
                try { gn = g.Name != null ? g.Name.ToString() : "?"; } catch { gn = "?"; }
                var co = g.Character as CharacterObject;
                var h = co != null ? co.HeroObject : null;
                if (h != null)
                {
                    hero = true;
                    v = h.GetTraitLevel(DefaultTraits.Valor);
                    cal = h.GetTraitLevel(DefaultTraits.Calculating);
                }
            }
            int score = v - cal;
            sbyte prof = !hero ? (sbyte)1 : (score > 0 ? (sbyte)2 : (score < 0 ? (sbyte)0 : (sbyte)1));
            _cpProf[ti] = prof;
            if (!hero) _cpNoHero++;
            if (prof == 0) _cpN0++; else if (prof == 2) _cpN2++; else _cpN1++;
            string who;
            _ccGen[ti] = g;
            _ccTac0[ti] = P440BestTactics(t, out who);
            // 3.1 / 3.4 los: kazdy typ planu osobno, rowno w logarytmie (0.8 i 1.25 to odwrotnosci)
            if (c.BattlePlanVariety)
            {
                double lo = Math.Log(P440Lo), hi = Math.Log(P440Hi);
                for (int k = 0; k < BpK; k++) _bpMul[ti * BpK + k] = (float)Math.Exp(lo + MBRandom.RandomFloat * (hi - lo));
            }
            _p4Ref[ti] = t;   // po tablicach - postfiks widzi calosc
            string before = TacName(CurTactic(t));
            // plan otwarcia od nowa (pierwsze przejscie po rozstawieniu; pozne DR - sam rzut)
            if (c.BattlePlanVariety && !late)
            {
                var ai = t.TeamAI;
                var lst = ai != null && _fAvail != null ? _fAvail.GetValue(ai) as List<TacticComponent> : null;
                if (ai != null && lst != null && lst.Count > 0)
                {
                    try { var q = t.QuerySystem; if (q != null) q.Expire(); } catch { }
                    try
                    {
                        ai.ResetTactic(false);
                        _p4Reset++;
                        if (TacName(CurTactic(t)) != before) _p4ResetChg++;
                    }
                    catch (Exception e) { _p4Stum++; RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Plan440 ResetTactic", e); }
                }
            }
            _p4Open0[ti] = before;
            _p4RollAt[ti] = now;
            _p4LateT[ti] = late;
            _p4Gen[ti] = gn + (hero ? " (Valor " + v.ToString(P4Inv) + ", Calculating " + cal.ToString(P4Inv) + ")" : " (bez bohatera)") + " " + CpName[prof]
                         + ", taktyk " + who + " " + _ccTac0[ti].ToString(P4Inv);
            _p4Teams++;
            if (late) _p4Late++;
        }

        /// <summary>Takt druzyny (co 0.5 s): plan otwarcia po 1 s od rzutu (jak 437), 3.5 smierc wodza, zamrozenie, nastepca.</summary>
        private static void P440Team(Mission m, Team t, int ti, float now, float step)
        {
            if (_p4Open[ti] == null && now - _p4RollAt[ti] >= P440OpenAfter)
            {
                string o = TacName(CurTactic(t));
                _p4Open[ti] = o;
                string key = (t.Side == BattleSideEnum.Attacker ? "A " : "O ") + o;
                int cnt;
                _p4Hist.TryGetValue(key, out cnt);
                _p4Hist[key] = cnt + 1;
            }
            var g = _ccGen[ti];
            if (g != null && _ccAt[ti] < 0f && !_ccDone[ti])
            {
                bool gone;
                string st = "?";
                try { gone = !g.IsActive(); st = g.State.ToString(); } catch { gone = true; }
                if (gone)
                {
                    _ccAt[ti] = now;
                    _ccNote[ti] = "wodz " + st + " w s. " + ((int)now).ToString(P4Inv) + " przy planie " + TacName(CurTactic(t));
                    if (_ccKey) _ccDead++; else _ccDeadDry++;
                }
            }
            bool frozen = false;
            if (_ccAt[ti] >= 0f && !_ccDone[ti])
            {
                if (now - _ccAt[ti] >= P440Confuse) { _ccDone[ti] = true; P440Successor(t, ti); }
                else if (_ccKey && CurTactic(t) != null)
                {
                    // przetrwanie przed zametem: S2 (141), puszczona bilansem ognia 291e / 320d, straty od pociskow w 30 s (438) - decyzje jak dotad
                    bool s2 = S2On(t), fire = FireReleased(t), bleed = O438Bleed(t) >= FireDead;
                    if (s2) _ccS2Sec += step;
                    if (OutshotRush.Live(t)) _ccRushSec += step;
                    if (s2 || fire || bleed) _ccOverSec += step;
                    else { frozen = true; _ccFrozenSec += step; }
                }
            }
            _ccFrozen[ti] = frozen;
        }

        /// <summary>3.5 po 45 s: najlepszy zywy taktyk druzyny; nizszy prog rady wojennej niz na starcie - plany ponad prog z waga 0 (gdy zostaje plan w progu).</summary>
        private static void P440Successor(Team t, int ti)
        {
            string who;
            int tac = P440BestTactics(t, out who);
            int t0 = P440Tier(_ccTac0[ti]), t1 = P440Tier(tac);
            string res = "bez zmiany listy";
            if (_ccKey)
            {
                _ccUnfreeze++;
                if (t1 < t0)
                {
                    if (P440HasTier(t, t1)) { _ccTier[ti] = (sbyte)t1; _ccRestrict++; res = "lista do progu " + (t1 == 0 ? "< 30" : "30-59"); }
                    else res = "brak planu w progu - lista bez zmian";
                }
            }
            else res = "klucz NIE";
            _ccNote[ti] = (_ccNote[ti] ?? "") + ", nastepca " + who + " Tactics " + tac.ToString(P4Inv) + " (" + res + ")";
        }

        private static int P440Tier(int tac) { return tac < P440TierMid ? 0 : (tac < P440TierHigh ? 1 : 2); }

        private static bool P440HasTier(Team t, int tier)
        {
            var ai = t.TeamAI;
            var lst = ai != null && _fAvail != null ? _fAvail.GetValue(ai) as List<TacticComponent> : null;
            if (lst == null) return false;
            for (int i = 0; i < lst.Count; i++)
            {
                var tc = lst[i];
                if (tc == null) continue;
                int k = P440Slot(tc);
                if (k >= 0 && BpTier[k] <= tier) return true;
            }
            return false;
        }

        /// <summary>Najwyzsza umiejetnosc Tactics zywego (aktywnego) bohatera druzyny.</summary>
        private static int P440BestTactics(Team t, out string who)
        {
            who = "brak";
            int best = 0;
            var list = t.ActiveAgents;
            int n = list != null ? list.Count : 0;
            for (int i = 0; i < n; i++)
            {
                var a = list[i];
                if (a == null || !a.IsActive() || !a.IsHero) continue;
                var co = a.Character as CharacterObject;
                var h = co != null ? co.HeroObject : null;
                if (h == null) continue;
                int s = h.GetSkillValue(DefaultSkills.Tactics);
                if (s > best || who == "brak") { best = s; try { who = a.Name != null ? a.Name.ToString() : "?"; } catch { who = "?"; } }
            }
            return best;
        }

        // ------------------------------------------------------------ koniec misji i linie

        /// <summary>Doctrine.End (przed Clear): migawka druzyn do linii 440.</summary>
        private static void P440End()
        {
            _p440Live = false;
            for (int i = 0; i < MaxTeams; i++) _ccFrozen[i] = false;
            for (int ti = 0; ti < MaxTeams; ti++)
            {
                var t = _p4Ref[ti];
                if (t == null) continue;
                var sb = new StringBuilder(200);
                sb.Append('T').Append(ti).Append(" (").Append(t.Side == BattleSideEnum.Attacker ? "A" : "O").Append(") ").Append(_p4Gen[ti] ?? "-")
                  .Append(", otwarcie ").Append(_p4Open[ti] ?? TacName(CurTactic(t))).Append(" (przed rzutem ").Append(_p4Open0[ti] ?? "-").Append(')');
                if (_p4LateT[ti]) sb.Append(" DR");
                sb.Append(", los ").Append(P440Muls(t, ti));
                if (_ccNote[ti] != null) sb.Append(", ").Append(_ccNote[ti]);
                _p4Snap.Add(sb.ToString().Replace('|', '/'));
            }
        }

        /// <summary>Mnozniki losu planow z listy druzyny ("-" bez rzutu / klucz NIE).</summary>
        private static string P440Muls(Team t, int ti)
        {
            var ai = t.TeamAI;
            var lst = ai != null && _fAvail != null ? _fAvail.GetValue(ai) as List<TacticComponent> : null;
            if (lst == null) return "-";
            var sb = new StringBuilder(120);
            for (int i = 0; i < lst.Count; i++)
            {
                var tc = lst[i];
                if (tc == null) continue;
                int k = P440Slot(tc);
                if (k < 0) continue;
                float f = _bpMul[ti * BpK + k];
                if (f == 1f) continue;
                sb.Append(sb.Length > 0 ? ", " : "").Append(BpName[k]).Append(" x").Append(f.ToString("0.00", P4Inv));
            }
            return sb.Length > 0 ? sb.ToString() : "-";
        }

        /// <summary>BattleMeasure437 linia start (glowny watek): rzut losu i profil druzyn; null = wszystkie klucze 440 NIE (tekst 437 jak dotad).</summary>
        internal static string P440StartNote()
        {
            var c = Settings.Current;
            if (c == null || !(c.BattlePlanVariety || c.CommanderPersonalityInBattle || c.CommandConfusionAfterCommanderDeath)) return null;
            var sb = new StringBuilder(300);
            for (int ti = 0; ti < MaxTeams; ti++)
            {
                var t = _p4Ref[ti];
                if (t == null) continue;
                try
                {
                    sb.Append(sb.Length > 0 ? "; " : "").Append('T').Append(ti).Append(' ').Append(CpName[_cpProf[ti]]).Append(", przed rzutem ").Append(_p4Open0[ti] ?? "-")
                      .Append(", los ").Append(P440Muls(t, ti));
                }
                catch (Exception e) { _p4Stum++; RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Plan440 start", e); }
            }
            return sb.Length > 0 ? sb.ToString().Replace('|', '/') : "brak druzyn z rzutem";
        }

        private static string P440Hist()
        {
            if (_p4Hist.Count == 0) return "-";
            var keys = new List<string>(_p4Hist.Keys);
            keys.Sort(StringComparer.Ordinal);
            var sb = new StringBuilder(200);
            for (int i = 0; i < keys.Count && i < 30; i++) sb.Append(i > 0 ? ", " : "").Append(keys[i]).Append(' ').Append(_p4Hist[keys[i]]);
            return sb.ToString();
        }

        /// <summary>Battle291.Write (po linii 439; po Doctrine.End - stan druzyn w migawce): linia "Wodz AI (440)".</summary>
        internal static string P440Line(string kind)
        {
            var c = Settings.Current;
            bool k1 = c != null && c.BattlePlanVariety, k2 = c != null && c.CommanderPersonalityInBattle, k3 = c != null && c.CommandConfusionAfterCommanderDeath;
            var sb = new StringBuilder(1600);
            sb.Append("Wodz AI (440): misja ").Append(kind ?? "-")
              .Append(" | klucze: los planow ").Append(k1 ? "TAK" : "NIE").Append(", charakter wodza ").Append(k2 ? "TAK" : "NIE")
              .Append(", zamet po smierci wodza ").Append(k3 ? "TAK" : "NIE")
              .Append(" | 3.1/3.4 druzyn z rzutem ").Append(_p4Teams).Append(" (pozne DR ").Append(_p4Late).Append("), ponowny wybor otwarcia ").Append(_p4Reset)
              .Append(" (inny plan niz przed rzutem ").Append(_p4ResetChg).Append("), wywolan wagi z losem ").Append(Volatile.Read(ref _bpCalls))
              .Append("; druzyny: ").Append(_p4Snap.Count > 0 ? string.Join("; ", _p4Snap) : "-")
              .Append(" | histogram otwarc od startu gry: ").Append(P440Hist())
              .Append(" | 3.2 profile: ostrozny ").Append(_cpN0).Append(", zwykly ").Append(_cpN1).Append(", porywczy ").Append(_cpN2).Append(" (bez bohatera ").Append(_cpNoHero).Append(')')
              .Append(" | 3.3 bieg od strat (szyko-takty): wczesniej - porywczy od ").Append(P440RashFire).Append(' ').Append(_cpFireEarly)
              .Append(", pozniej - ostrozny od ").Append(P440CalmFire).Append(' ').Append(_cpFireLate).Append(", bylby (klucz NIE) ").Append(_cpFireDry)
              .Append("; S2 od strat (takty): wczesniej ").Append(_cpS2Early).Append(", pozniej ").Append(_cpS2Late).Append(", bylby ").Append(_cpS2Dry)
              .Append("; 439 Engage puszczony szykiem wroga w ").Append(S1ContactGap.ToString("0", P4Inv)).Append('-').Append(P440RashGap.ToString("0", P4Inv))
              .Append(" m (porywczy, wywolan) ").Append(Volatile.Read(ref _cpGapEarly)).Append(", bylby ").Append(Volatile.Read(ref _cpGapDry))
              .Append("; wrecz (porywczy ").Append(((int)(P440RashMelee * 100f)).ToString(P4Inv)).Append("%, ostrozny ").Append(((int)(P440CalmMelee * 100f)).ToString(P4Inv))
              .Append("%, druzyno-takty): wczesniej ").Append(_cpMeleeEarly).Append(", pozniej ").Append(_cpMeleeLate)
              .Append(" | 3.5 smierc wodza: druzyn ").Append(_ccDead).Append(" (bylby, klucz NIE ").Append(_ccDeadDry).Append("), zamrozenie ")
              .Append(_ccFrozenSec.ToString("0", P4Inv)).Append(" s, decyzji pominietych ").Append(Volatile.Read(ref _ccSkip))
              .Append(", przepuszczone przetrwaniem (S2 / 291e / straty od pociskow) ").Append(_ccOverSec.ToString("0", P4Inv)).Append(" s (S2 zywe ")
              .Append(_ccS2Sec.ToString("0", P4Inv)).Append(" s, bieg 313b ").Append(_ccRushSec.ToString("0", P4Inv)).Append(" s); koniec zametu ").Append(_ccUnfreeze)
              .Append(", lista zawezona do zywego taktyka ").Append(_ccRestrict).Append(" (wag wyzerowanych ").Append(Volatile.Read(ref _ccZero)).Append(')')
              .Append(" | latka ").Append(_p4Patch).Append(" | potkniecia ").Append(_p4Stum).Append('.');
            return sb.ToString();
        }
    }
}
