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
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 437 = PAKIET 0 audytu AI bitwy (docs/audyty-2026-11-11/AUDYT-AI-BITWA.md, rozdzial 4 "Pomiary do dodania najpierw"). TYLKO POMIAR - zadnej zmiany wag,
    /// rozkazow ani stanu misji. Klucz BattleAiMeasurements ("Battle AI Measurements", domyslnie TAK); NIE = zachowanie misji nie jest dodawane, latki nie sa
    /// zakladane, zadnej nowej linii, etykieta szyku w liniach "AI bitwy (333)" i sonda 370 jak dotad (co do znaku).
    /// Bitwa polowa (Mission.IsFieldBattle, nie szturm). Linie "Pomiary AI bitwy (437)":
    ///  - start (po rozstawieniu + 1 s): sklad obu stron wedlug klas (agent: konno + bron dystansowa), druzyny AI z wodzem (Valor, Calculating), plan otwarcia,
    ///    rzut losu "-" (pakiet 3 nie wdrozony), stan latek;
    ///  - os czasu co 15 s dla KAZDEJ druzyny AI (druzyna gracza-wodza pominieta): plan, a dla kazdego szyku klasa wedlug skladu, ludzi, zachowanie, rozkaz,
    ///    odleglosc od najblizszego szyku wroga, trafienia pociskami w oknie (ludzie p / konie k); jazda: stan szarzy RBM i klasa celu, trzy najwyzsze wagi
    ///    zachowan (WeightFactor x premia aktywnego 1.2-2.0 jak FormationAI.FindBestBehavior; GetAiWeight NIE wolany - liczenie zachowania zostaje w watku AI);
    ///  - plan: kazda zmiana planu po starcie - trzy najwyzsze wagi z tej decyzji (wartosci po wszystkich postfiksach RBM i doktryny; biezacy x1.5 jak gra),
    ///    powod (ResetTacticalPositions = false albo waga nowego > 1.5 x biezacego), numer zmiany druzyny bez pierwszego wyboru;
    ///  - ostrzal: szyk AI stracil >= 5% (i >= 2) od pociskow w 30 s (ludzie i konie) -> co zrobil w nastepnych 30 s (stal / szedl / biegl / szarza / odwrot / w bok);
    ///  - pierwszy cios szarzy: pierwszy cios wrecz jezdzca szyku jazdy AI po wejsciu w szarze - klasa szyku ofiary, strona ciosu (czolo / bok / tyl wzgledem
    ///    kierunku szyku ofiary), ofiara zwiazana z innym wrogiem, czy ofiara jest celem RBM;
    ///  - koniec: padli (zabici + nieprzytomni) wedlug zrodla (pociski / wrecz pieszy / wrecz konny / szarza koniem / w ucieczce) i fazy (przed kontaktem
    ///    szyku ofiary i po), straty stron w % wejsc, wynik, plany druzyn (otwarcie, zmiany, czas w planach), jazda pod ostrzalem (szyko-s, w tym stala),
    ///    "strzelcy wroga odslonieci w zasiegu szarzy, a jazda stala" (szyko-s), trafienia pociskami jazdy AI (ludzie / konie), reakcje na ostrzal wedlug klasy,
    ///    pierwsze ciosy szarzy, zachowania szykow AI przed i po kontakcie (szyko-s), wywolania latek wedlug watku, potkniecia.
    /// Miary sukcesu planu (rozdzial 4): (a) "plan otwarcia" w linii start na druzyne i wodza; (b) "jazda pod ostrzalem ... stala X%" w linii koniec;
    /// (c) "straty: A x%, O y%" i "wynik" w linii koniec.
    /// Latki (raz na proces, przy pierwszej bitwie polowej z kluczem; wlasna instancja Harmony, Priority.Last po com.rbmai i com.jeff.armoury - wagi czytane
    /// PO doktrynie): prefiks + postfiks TeamAIComponent.MakeDecision (DeclaredMethod), postfiksy GetTacticWeight i ResetTacticalPositions na kazdym typie
    /// taktyki gry i RBM, ktory je deklaruje (DeclaredMethod na klasie deklarujacej; latka sprawdzana w GetPatchInfo - policzone w linii start).
    /// Watek: latki tylko zapisuja kontekst decyzji (ThreadStatic) i kolejke zdarzen pod lockiem; OnAgentHit / OnAgentRemoved tylko do kolejki pod lockiem
    /// (bywaja wolane z ticku asynchronicznego - wzor 313d); cala reszta (probkowanie co 1 s, os czasu, linie) w OnMissionTick w glownym watku, same odczyty
    /// (wlasciwosci ReadOnly zapytan szyku, pola zachowan, refleksja na pola RBM). Wyjatki per obiekt licz (potkniecia), pierwsze 3 slady do logu.
    /// Rozmiar: os czasu najwyzej MaxLines linii na misje (reszta liczona), linie planu / ostrzalu / ciosu po kilkadziesiat.
    /// </summary>
    internal static class BattleMeasure437
    {
        internal const string Tag = "Pomiary AI bitwy (437)";
        private const string HarmonyId = "com.jeff.armoury.measure437";
        internal const float Every = 15f, Sample = 1f, StartDelay = 1f, NoDeployStart = 10f, ContactGap = 15f, FireRecent = 10f;
        internal const float LossWin = 30f, LossShare = 0.05f, ReactWin = 30f, StillPerSec = 1.5f;
        internal const float ExposeMin = 30f, ExposeMax = 200f, BlockPad = 10f;
        private const int LossMin = 2, MaxLines = 360, MaxPlanLines = 60, MaxEvLines = 30, MaxBlowLines = 24, ErrMax = 3, QMax = 8192, PendMax = 64, MinCav = 5;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly string[] SrcName = { "pociski", "wrecz pieszy", "wrecz konny", "szarza koniem", "w ucieczce" };
        private static readonly string[] ClsKeys = { "pie", "str", "jaz", "kstr" };

        // ------------------------------------------------------------ latki (raz na proces)

        private static bool _tried;
        private static string _note = "nie wpiete";
        private static AccessTools.FieldRef<TeamAIComponent, TacticComponent> _fCur;
        private static AccessTools.FieldRef<FormationAI, List<BehaviorComponent>> _fBeh;
        private static Type _tCavCharge;
        private static FieldInfo _fChargeState, _fLastTarget;

        private sealed class Ctx
        {
            public bool Active;
            public TeamAIComponent Ai;
            public TacticComponent Before;
            public int Reset = -1;
            public readonly List<TacticComponent> WT = new List<TacticComponent>(12);
            public readonly List<float> WV = new List<float>(12);
        }

        private sealed class PlanEv
        {
            public Team T;
            public string From, To, Top, Why;
        }

        [ThreadStatic] private static Ctx _ctx;
        private static volatile bool _on;
        private static int _mainThread = -1;
        private static int _decMain, _decOther, _wMain, _wOther, _postErr, _pendDrop;   // Interlocked (watek ticku druzyn)
        private static readonly object L = new object();
        private static readonly List<PlanEv> Pend = new List<PlanEv>();

        internal static string ApplyPatches()
        {
            if (_tried) return _note;
            _tried = true;
            int wWant = 0, wOk = 0, rWant = 0, rOk = 0;
            string dec = "NIE";
            try
            {
                var h = new Harmony(HarmonyId);
                var after = new[] { "com.rbmai", "com.jeff.armoury" };
                try { _fCur = AccessTools.FieldRefAccess<TeamAIComponent, TacticComponent>("_currentTactic"); } catch { _fCur = null; }
                try { _fBeh = AccessTools.FieldRefAccess<FormationAI, List<BehaviorComponent>>("_behaviors"); } catch { _fBeh = null; }
                try { _tCavCharge = AccessTools.TypeByName("RBMAI.RBMBehaviorCavalryCharge") ?? AccessTools.TypeByName("RBMBehaviorCavalryCharge"); } catch { _tCavCharge = null; }
                if (_tCavCharge != null)
                {
                    try { _fChargeState = AccessTools.Field(_tCavCharge, "_chargeState"); } catch { _fChargeState = null; }
                    try { _fLastTarget = AccessTools.Field(_tCavCharge, "_lastTarget"); } catch { _fLastTarget = null; }
                }
                if (_fCur != null)
                {
                    try
                    {
                        var md = AccessTools.DeclaredMethod(typeof(TeamAIComponent), "MakeDecision", Type.EmptyTypes);
                        if (md != null)
                        {
                            h.Patch(md, prefix: new HarmonyMethod(typeof(BattleMeasure437), nameof(PreDec)) { priority = Priority.Last, after = after },
                                        postfix: new HarmonyMethod(typeof(BattleMeasure437), nameof(PostDec)) { priority = Priority.Last, after = after });
                            dec = Ours(md, true) && Ours(md, false) ? "TAK" : "NIE (latka polknieta)";
                        }
                        else dec = "NIE (brak MakeDecision)";
                    }
                    catch (Exception e) { dec = "NIE (" + e.GetType().Name + ")"; Stum("Latka.MakeDecision", e); }
                    // taktyki gry i RBM: tylko typy, ktore deklaruja metode (DeclaredMethod na klasie deklarujacej)
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        string an;
                        try { an = asm.GetName().Name ?? ""; } catch { continue; }
                        if (an != "TaleWorlds.MountAndBlade" && an.IndexOf("RBM", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        Type[] ts;
                        try { ts = asm.GetTypes(); }
                        catch (ReflectionTypeLoadException rt) { ts = rt.Types; }
                        catch { continue; }
                        if (ts == null) continue;
                        foreach (var t in ts)
                        {
                            if (t == null || !typeof(TacticComponent).IsAssignableFrom(t)) continue;
                            MethodInfo mw = null, mr = null;
                            try { mw = AccessTools.DeclaredMethod(t, "GetTacticWeight", Type.EmptyTypes); } catch { }
                            try { mr = AccessTools.DeclaredMethod(t, "ResetTacticalPositions", Type.EmptyTypes); } catch { }
                            if (mw != null && !mw.IsAbstract && mw.ReturnType == typeof(float))
                            {
                                wWant++;
                                try
                                {
                                    h.Patch(mw, postfix: new HarmonyMethod(typeof(BattleMeasure437), nameof(PostW)) { priority = Priority.Last, after = after });
                                    if (Ours(mw, false)) wOk++;
                                }
                                catch (Exception e) { Stum("Latka.Waga " + t.Name, e); }
                            }
                            if (mr != null && !mr.IsAbstract && mr.ReturnType == typeof(bool))
                            {
                                rWant++;
                                try
                                {
                                    h.Patch(mr, postfix: new HarmonyMethod(typeof(BattleMeasure437), nameof(PostR)) { priority = Priority.Last, after = after });
                                    if (Ours(mr, false)) rOk++;
                                }
                                catch (Exception e) { Stum("Latka.Reset " + t.Name, e); }
                            }
                        }
                    }
                }
                else dec = "NIE (brak pola TeamAIComponent._currentTactic)";
            }
            catch (Exception e) { dec = "NIE (" + e.GetType().Name + ")"; Stum("Latki", e); }
            _note = "decyzja " + dec + ", wagi taktyk " + wOk + "/" + wWant + ", reset pozycji " + rOk + "/" + rWant
                    + ", wagi zachowan " + (_fBeh != null ? "TAK" : "NIE") + ", szarza RBM " + (_fChargeState != null ? "TAK" : "NIE");
            return _note;
        }

        private static bool Ours(MethodBase m, bool prefix)
        {
            var info = Harmony.GetPatchInfo(m);
            if (info == null) return false;
            var list = prefix ? info.Prefixes : info.Postfixes;
            foreach (var p in list) if (p != null && p.owner == HarmonyId) return true;
            return false;
        }

        private static void Thr(ref int main, ref int other)
        {
            if (Thread.CurrentThread.ManagedThreadId == Volatile.Read(ref _mainThread)) Interlocked.Increment(ref main);
            else Interlocked.Increment(ref other);
        }

        /// <summary>Prefiks MakeDecision (watek ticku druzyn): kontekst decyzji - plan przed decyzja, wagi od zera. Tylko odczyt.</summary>
        private static void PreDec(TeamAIComponent __instance)
        {
            if (!_on) return;
            try
            {
                Thr(ref _decMain, ref _decOther);
                var c = _ctx ?? (_ctx = new Ctx());
                c.Active = true;
                c.Ai = __instance;
                c.Before = _fCur(__instance);
                c.Reset = -1;
                c.WT.Clear(); c.WV.Clear();
            }
            catch { Interlocked.Increment(ref _postErr); }
        }

        /// <summary>Postfiks GetTacticWeight (kazdy typ taktyki): waga po wszystkich postfiksach - tylko w kontekscie decyzji.</summary>
        private static void PostW(TacticComponent __instance, float __result)
        {
            if (!_on) return;
            try
            {
                Thr(ref _wMain, ref _wOther);
                var c = _ctx;
                if (c == null || !c.Active || __instance == null) return;
                int i = c.WT.IndexOf(__instance);
                if (i >= 0) c.WV[i] = __result;
                else if (c.WT.Count < 32) { c.WT.Add(__instance); c.WV.Add(__result); }
            }
            catch { Interlocked.Increment(ref _postErr); }
        }

        /// <summary>Postfiks ResetTacticalPositions: wynik zapamietany w kontekscie decyzji (gra wola go dla biezacego planu przy innym zwyciezcy).</summary>
        private static void PostR(TacticComponent __instance, bool __result)
        {
            if (!_on) return;
            try
            {
                var c = _ctx;
                if (c == null || !c.Active) return;
                c.Reset = __result ? 1 : 0;
            }
            catch { Interlocked.Increment(ref _postErr); }
        }

        /// <summary>Postfiks MakeDecision: zmiana planu -> zdarzenie do kolejki (trzy najwyzsze wagi, powod). Linia w glownym watku.</summary>
        private static void PostDec(TeamAIComponent __instance)
        {
            var c = _ctx;
            if (c == null || !c.Active) return;
            c.Active = false;
            if (!_on) return;
            try
            {
                var now = _fCur(__instance);
                if (ReferenceEquals(now, c.Before) || now == null) return;
                var ev = new PlanEv { T = now.Team, From = c.Before != null ? TacName(c.Before) : "-", To = TacName(now) };
                // trzy najwyzsze (wagi surowe; biezacy w MaxBy gry x1.5)
                int n = c.WT.Count;
                var idx = new List<int>(n);
                for (int i = 0; i < n; i++) idx.Add(i);
                idx.Sort((a, b) => c.WV[b].CompareTo(c.WV[a]));
                var sb = new StringBuilder(120);
                for (int k = 0; k < idx.Count && k < 3; k++)
                {
                    int i = idx[k];
                    if (k > 0) sb.Append(", ");
                    sb.Append(TacName(c.WT[i])).Append(' ').Append(F2(c.WV[i]));
                    if (ReferenceEquals(c.WT[i], c.Before)) sb.Append(" (biezacy x1.5 = ").Append(F2(c.WV[i] * 1.5f)).Append(')');
                }
                ev.Top = n == 0 ? "-" : sb.ToString() + (n > 3 ? " (z " + n + ")" : "");
                if (c.Before == null) ev.Why = "pierwszy wybor";
                else
                {
                    float wNew = -1f, wOld = -1f;
                    for (int i = 0; i < n; i++)
                    {
                        if (ReferenceEquals(c.WT[i], now)) wNew = c.WV[i];
                        if (ReferenceEquals(c.WT[i], c.Before)) wOld = c.WV[i];
                    }
                    if (c.Reset == 0) ev.Why = "ResetTacticalPositions biezacego = false (zmiana bez progu 1.5)";
                    else if (wNew >= 0f && wOld >= 0f && wNew > wOld * 1.5f) ev.Why = "waga nowego > 1.5 x biezacego";
                    else ev.Why = "inny (np. brak wroga - TacticCharge gry; reset " + (c.Reset < 0 ? "-" : (c.Reset == 1 ? "true" : "false")) + ")";
                }
                lock (L)
                {
                    if (Pend.Count < PendMax) Pend.Add(ev);
                    else _pendDrop++;
                }
            }
            catch { Interlocked.Increment(ref _postErr); }
        }

        // ------------------------------------------------------------ stan misji (glowny watek; kolejka zdarzen pod Q)

        private struct Ev
        {
            public byte K;            // 0 trafienie, 1 strata (zabity / nieprzytomny)
            public Formation F;       // szyk ofiary (przy koniu - szyk jezdzca)
            public Agent By;
            public bool Horse, Missile, Coll, ByMounted, Flee;
            public int Side;
        }

        private sealed class FS
        {
            public Formation F;
            public Team T;
            public string Cls = "pie";
            public bool Ai;
            public Vec2 Pos = Vec2.Invalid;
            public float Moved = -1f, Gap = float.MaxValue;
            public Formation Near;
            public bool Contact;
            public int WinHit, WinHorse;
            public float FireAt = -1000f;
            public readonly List<float> MLoss = new List<float>();
            // reakcja na ostrzal
            public float EvAt = -1f, EvCool = -1000f;
            public Vec2 EvPos = Vec2.Invalid, EvAxis = Vec2.Invalid;
            public int EvLoss, EvN;
            public string EvBeh, EvCls;
            public bool EvCharge;
            // szarza jazdy
            public bool InCharge, FirstDone;
            public float ChargeAt;
        }

        private sealed class TR
        {
            public Team T;
            public string Open, Cur, Gen = "-";
            public int Chg, ChgSeen, Dec0, Men0;
            public readonly Dictionary<string, float> Sec = new Dictionary<string, float>(StringComparer.Ordinal);
        }

        private static bool _live, _started, _written;
        private static WeakReference _m;
        private static float _startAt = -1f, _t0, _next, _lastSample = -1f, _contactAt = -1f;
        private static bool _anyContact;
        private static int _stum, _errs, _lines, _linesSkip, _readings, _planLines, _planSkip, _evLines, _blowLines, _qDrop;
        private static readonly object Q = new object();
        private static List<Ev> _q = new List<Ev>(512), _qWork = new List<Ev>(512);
        private static readonly Dictionary<Formation, FS> Forms = new Dictionary<Formation, FS>();
        private static readonly Dictionary<Team, TR> Teams = new Dictionary<Team, TR>();
        private static readonly int[,,] Dead = new int[2, 2, 5];   // strona (0 A, 1 O), faza (0 przed kontaktem szyku ofiary, 1 po), zrodlo
        private static readonly int[] Built = new int[2];
        private static readonly int[,] Comp0 = new int[2, 4];
        private static float _cavFire, _cavFireStill, _exp, _expStill;
        private static int _hitRider, _hitHorse, _chargeEp, _chargeNoBlow;
        private static readonly Dictionary<string, int> Reac = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> FirstBlow = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly Dictionary<string, float> BehPre = new Dictionary<string, float>(StringComparer.Ordinal);
        private static readonly Dictionary<string, float> BehPost = new Dictionary<string, float>(StringComparer.Ordinal);
        private static readonly StringBuilder Sb = new StringBuilder(2048);

        internal static bool Live { get { return _live; } }

        private static void Clear()
        {
            _started = _written = _anyContact = false;
            _startAt = -1f; _t0 = 0f; _next = 0f; _lastSample = -1f; _contactAt = -1f;
            _stum = _errs = _lines = _linesSkip = _readings = _planLines = _planSkip = _evLines = _blowLines = _qDrop = 0;
            lock (Q) { _q.Clear(); _qWork.Clear(); }
            lock (L) Pend.Clear();
            Forms.Clear(); Teams.Clear();
            Array.Clear(Dead, 0, Dead.Length); Array.Clear(Built, 0, Built.Length); Array.Clear(Comp0, 0, Comp0.Length);
            _cavFire = _cavFireStill = _exp = _expStill = 0f;
            _hitRider = _hitHorse = _chargeEp = _chargeNoBlow = 0;
            Reac.Clear(); FirstBlow.Clear(); BehPre.Clear(); BehPost.Clear();
            Interlocked.Exchange(ref _decMain, 0); Interlocked.Exchange(ref _decOther, 0);
            Interlocked.Exchange(ref _wMain, 0); Interlocked.Exchange(ref _wOther, 0);
            Interlocked.Exchange(ref _postErr, 0); Interlocked.Exchange(ref _pendDrop, 0);
        }

        private static bool Here(Mission m)
        {
            var w = _m;
            return _live && m != null && w != null && ReferenceEquals(w.Target, m);
        }

        /// <summary>Logic.AfterStart: bitwa polowa z kluczem - stan od zera, latki raz na proces.</summary>
        internal static void Begin(Mission m)
        {
            _on = false;
            _live = false;
            Clear();
            _m = null;
            var s = Settings.Current;
            if (m == null || s == null || !s.BattleAiMeasurements) return;
            bool field;
            try { field = m.IsFieldBattle && !m.IsSiegeBattle; } catch { field = false; }
            if (!field) return;
            _m = new WeakReference(m);
            Volatile.Write(ref _mainThread, Thread.CurrentThread.ManagedThreadId);
            ApplyPatches();
            _live = true;
            _on = true;
        }

        internal static void End(Mission m)
        {
            if (!Here(m)) return;
            try { Drain(m, Now(m)); } catch (Exception e) { Stum("Koniec.Kolejka", e); }
            if (_started && !_written)
            {
                _written = true;
                try { WriteEnd(m); } catch (Exception e) { Stum("Koniec", e); }
            }
            _on = false;
            _live = false;
            Clear();
            _m = null;
        }

        private static float Now(Mission m) { try { return m.CurrentTime; } catch { return 0f; } }

        private static void Stum(string where, Exception e)
        {
            _stum++;
            if (_errs >= ErrMax) return;
            _errs++;
            try { Log.Error("BattleMeasure437." + where, e); } catch { }
        }

        // ------------------------------------------------------------ zdarzenia agentow (moga byc z ticku asynchronicznego - tylko kolejka)

        internal static void OnBuild(Agent a)
        {
            // tylko posilki po starcie - wojsko z rozstawienia liczy WriteStart (spawn gry bywa przed naszym AfterStart)
            if (!_live || !_started || a == null) return;
            try
            {
                if (!a.IsHuman) return;
                var t = a.Team;
                if (t == null) return;
                int s = SideIx(t.Side);
                if (s < 0) return;
                Interlocked.Increment(ref Built[s]);
            }
            catch { }
        }

        internal static void OnHit(Agent v, Agent by, in Blow blow)
        {
            if (!_live || v == null) return;
            try
            {
                if (blow.InflictedDamage <= 0) return;
                bool horse = v.IsMount;
                var man = horse ? v.RiderAgent : v;
                if (man == null || (!horse && !v.IsHuman)) return;
                var ev = new Ev { K = 0, F = man.Formation, By = by, Horse = horse, Missile = blow.IsMissile, Coll = blow.AttackType == AgentAttackType.Collision };
                lock (Q)
                {
                    if (_q.Count < QMax) _q.Add(ev);
                    else _qDrop++;
                }
            }
            catch { }
        }

        internal static void OnRemoved(Agent v, Agent by, AgentState st, KillingBlow kb)
        {
            if (!_live || v == null) return;
            try
            {
                if (st != AgentState.Killed && st != AgentState.Unconscious) return;
                Ev ev;
                if (v.IsMount)
                {
                    var r = v.RiderAgent;
                    if (r == null || !kb.IsMissile) return;
                    ev = new Ev { K = 1, F = r.Formation, Horse = true, Missile = true, Side = -1 };
                }
                else
                {
                    if (!v.IsHuman) return;
                    var t = v.Team;
                    ev = new Ev
                    {
                        K = 1, F = v.Formation, Missile = kb.IsMissile, Coll = kb.AttackType == AgentAttackType.Collision,
                        ByMounted = by != null && (by.IsMount || by.HasMount), Flee = v.IsRunningAway, Side = t != null ? SideIx(t.Side) : -1
                    };
                }
                lock (Q)
                {
                    if (_q.Count < QMax) _q.Add(ev);
                    else _qDrop++;
                }
            }
            catch { }
        }

        private static int SideIx(BattleSideEnum s) { return s == BattleSideEnum.Attacker ? 0 : (s == BattleSideEnum.Defender ? 1 : -1); }

        // ------------------------------------------------------------ tick (glowny watek)

        internal static void Tick(Mission m)
        {
            if (!Here(m)) return;
            float now = Now(m);
            try
            {
                if (!_started)
                {
                    bool deployed;
                    try { deployed = m.IsDeploymentFinished || (m.Mode != MissionMode.Deployment && now >= NoDeployStart); } catch { deployed = false; }
                    if (!deployed) { DrainPlansBeforeStart(); return; }
                    if (_startAt < 0f) { _startAt = now + StartDelay; return; }
                    if (now < _startAt) { DrainPlansBeforeStart(); return; }
                    DrainPlansBeforeStart();
                    _started = true;
                    _t0 = now;
                    lock (Q) _q.Clear();   // ciosy z rozstawienia nie licza sie
                    SampleAll(m, now, 0f);
                    WriteStart(m, now);
                    _lastSample = now;
                    _next = now + Every;
                    return;
                }
                bool ended;
                try { ended = m.MissionEnded; } catch { ended = false; }
                Drain(m, now);
                if (ended) return;
                if (now - _lastSample >= Sample)
                {
                    float dt = now - _lastSample;
                    _lastSample = now;
                    SampleAll(m, now, dt);
                }
                if (now >= _next)
                {
                    _next = now + Every;
                    Timeline(m, now);
                }
            }
            catch (Exception e) { Stum("Tick", e); }
        }

        /// <summary>Decyzje przed startem (rozstawienie): tylko licznik druzyny, bez linii.</summary>
        private static void DrainPlansBeforeStart()
        {
            List<PlanEv> evs = null;
            lock (L)
            {
                if (Pend.Count == 0) return;
                evs = new List<PlanEv>(Pend);
                Pend.Clear();
            }
            foreach (var e in evs)
            {
                try
                {
                    if (e.T == null || !AiTeam(e.T)) continue;
                    Tr(e.T).Dec0++;
                }
                catch (Exception x) { Stum("PlanPrzedStartem", x); }
            }
        }

        private static bool AiTeam(Team t)
        {
            return MissionTeams.Valid(t) && !(t.IsPlayerTeam && t.IsPlayerGeneral) && t.HasBots;
        }

        private static TR Tr(Team t)
        {
            TR r;
            if (!Teams.TryGetValue(t, out r)) { r = new TR { T = t }; Teams[t] = r; }
            return r;
        }

        private static FS Fs(Formation f)
        {
            FS s;
            if (!Forms.TryGetValue(f, out s)) { s = new FS { F = f, T = f.Team }; Forms[f] = s; }
            return s;
        }

        /// <summary>Kolejka zdarzen agentow i plany z latek - przetwarzane w glownym watku.</summary>
        private static void Drain(Mission m, float now)
        {
            List<PlanEv> evs = null;
            lock (L)
            {
                if (Pend.Count > 0) { evs = new List<PlanEv>(Pend); Pend.Clear(); }
            }
            if (evs != null) foreach (var e in evs) PlanLine(e, now);
            List<Ev> work;
            lock (Q)
            {
                if (_q.Count == 0) return;
                work = _q; _q = _qWork; _qWork = work;
            }
            for (int i = 0; i < work.Count; i++)
            {
                try { One(work[i], now); }
                catch (Exception e) { Stum("Zdarzenie", e); }
            }
            work.Clear();
        }

        private static void One(Ev e, float now)
        {
            FS vf = null;
            if (e.F != null) Forms.TryGetValue(e.F, out vf);
            if (e.K == 0)
            {
                if (e.Missile)
                {
                    if (vf == null && e.F != null && e.F.CountOfUnits > 0) vf = Fs(e.F);
                    if (vf == null) return;
                    if (e.Horse) vf.WinHorse++; else vf.WinHit++;
                    vf.FireAt = now;
                    if (vf.Ai && (vf.Cls == "jaz" || vf.Cls == "kstr")) { if (e.Horse) _hitHorse++; else _hitRider++; }
                    return;
                }
                // pierwszy cios wrecz szyku jazdy AI w szarzy
                var by = e.By;
                if (by == null) return;
                var rider = by.IsMount ? by.RiderAgent : by;
                if (rider == null || !rider.HasMount) return;
                var bf = rider.Formation;
                FS cf;
                if (bf == null || !Forms.TryGetValue(bf, out cf) || !cf.Ai || !cf.InCharge || cf.FirstDone) return;
                cf.FirstDone = true;
                string vcls = vf != null ? vf.Cls : "?";
                string side = "?";
                try
                {
                    if (e.F != null)
                    {
                        Vec2 vp = e.F.CachedAveragePosition, ap = rider.Position.AsVec2, dir = e.F.Direction;
                        if (vp.IsValid && dir.IsValid)
                        {
                            Vec2 d = ap - vp;
                            float len = d.Length;
                            if (len > 0.1f)
                            {
                                float dot = (d.x * dir.x + d.y * dir.y) / (len * Math.Max(0.001f, dir.Length));
                                side = dot > 0.5f ? "czolo" : (dot < -0.5f ? "tyl" : "bok");
                            }
                        }
                    }
                }
                catch { }
                bool bound = vf != null && vf.Gap < ContactGap && vf.Near != null && !ReferenceEquals(vf.Near, bf);
                string tgt = "-";
                try
                {
                    var b = bf.AI != null ? bf.AI.ActiveBehavior : null;
                    if (b != null && _fLastTarget != null && _tCavCharge != null && _tCavCharge.IsInstanceOfType(b))
                    {
                        var lq = _fLastTarget.GetValue(b) as FormationQuerySystem;
                        tgt = lq != null && e.F != null ? (ReferenceEquals(lq.Formation, e.F) ? "TAK" : "NIE") : "-";
                    }
                }
                catch { }
                string key = vcls + "/" + side + (bound ? "/zwiazany" : "");
                int n;
                FirstBlow.TryGetValue(key, out n);
                FirstBlow[key] = n + 1;
                if (_blowLines < MaxBlowLines)
                {
                    _blowLines++;
                    Log.Info(Tag + " pierwszy cios szarzy: t=" + F0(now - _t0) + " | szyk " + FName(bf) + " " + bf.CountOfUnits + " ludzi (" + SideTxt(bf.Team)
                             + ") | ofiara " + (e.F != null ? FName(e.F) : "-") + " " + vcls + ", cios " + side + ", zwiazana z innym wrogiem " + (bound ? "TAK" : "NIE")
                             + ", cel RBM " + tgt + " | od wejscia w szarze " + F0(now - cf.ChargeAt) + " s.");
                }
                return;
            }
            // strata
            if (e.Missile && vf != null && vf.Ai) vf.MLoss.Add(now);   // okno 30 s czyszczone w React (tylko szyki AI)
            if (e.Horse || e.Side < 0) return;
            bool after = vf != null ? vf.Contact : _anyContact;
            int src = e.Flee ? 4 : (e.Missile ? 0 : (e.Coll ? 3 : (e.ByMounted ? 2 : 1)));
            Dead[e.Side, after ? 1 : 0, src]++;
        }

        // ------------------------------------------------------------ probkowanie co 1 s

        private static void SampleAll(Mission m, float now, float dt)
        {
            var teams = m.Teams;
            int tn = teams != null ? teams.Count : 0;
            // 1) stan wszystkich szykow
            for (int i = 0; i < tn; i++)
            {
                Team t;
                try { t = teams[i]; if (!MissionTeams.Valid(t)) continue; }
                catch (Exception e) { Stum("Druzyna", e); continue; }
                bool ai = AiTeam(t);
                if (ai)
                {
                    try
                    {
                        var tr = Tr(t);
                        string plan = PlanOf(t);
                        if (tr.Open == null) tr.Open = plan;
                        if (dt > 0f)
                        {
                            float s0;
                            tr.Sec.TryGetValue(plan, out s0);
                            tr.Sec[plan] = s0 + dt;
                            if (tr.Cur != null && tr.Cur != plan) tr.ChgSeen++;
                        }
                        tr.Cur = plan;
                    }
                    catch (Exception e) { Stum("Plan", e); }
                }
                var fl = t.FormationsIncludingSpecialAndEmpty;
                for (int j = 0; fl != null && j < fl.Count; j++)
                {
                    try
                    {
                        var f = fl[j];
                        if (f == null || f.CountOfUnits <= 0) continue;
                        var s = Fs(f);
                        s.T = t;
                        s.Ai = ai && f.IsAIControlled;
                        s.Cls = Cls(f);
                        Vec2 p = f.CachedAveragePosition;
                        s.Moved = s.Pos.IsValid && p.IsValid && dt > 0f ? p.Distance(s.Pos) : -1f;
                        s.Pos = p;
                        float d2 = f.CachedClosestEnemyFormationDistanceSquared;
                        var nq = f.CachedClosestEnemyFormation;
                        var nf = nq != null ? nq.Formation : null;
                        s.Near = nf;
                        s.Gap = float.MaxValue;
                        if (nf != null && d2 >= 0f && d2 < 1e10f) s.Gap = Math.Max(0f, (float)Math.Sqrt(d2) - 0.5f * (f.Depth + nf.Depth));
                        if (s.Gap < ContactGap && _started)
                        {
                            s.Contact = true;
                            if (!_anyContact) { _anyContact = true; _contactAt = now; }
                        }
                    }
                    catch (Exception e) { Stum("Szyk", e); }
                }
            }
            if (dt <= 0f) return;
            // 2) pomiary szykow AI
            foreach (var s in Forms.Values)
            {
                try
                {
                    var f = s.F;
                    if (!s.Ai || f == null || f.CountOfUnits <= 0 || !MissionTeams.Valid(s.T)) continue;
                    string beh = BehName(f.AI != null ? f.AI.ActiveBehavior : null);
                    var ord = f.GetReadonlyMovementOrderReference().OrderEnum;
                    string bk = s.Cls + " " + beh;
                    var bd = s.Contact ? BehPost : BehPre;
                    float b0;
                    bd.TryGetValue(bk, out b0);
                    bd[bk] = b0 + dt;
                    bool still = s.Moved >= 0f && s.Moved < StillPerSec * dt;
                    bool cav = s.Cls == "jaz";
                    // jazda pod ostrzalem (ludzie albo konie trafieni pociskiem w ostatnich 10 s)
                    if (cav && f.CountOfUnits >= MinCav && now - s.FireAt <= FireRecent)
                    {
                        _cavFire += dt;
                        if (still) _cavFireStill += dt;
                    }
                    // strzelcy wroga odslonieci w zasiegu szarzy
                    if (cav && f.CountOfUnits >= MinCav && s.Gap >= ContactGap && s.Pos.IsValid && Exposed(s))
                    {
                        _exp += dt;
                        if (still) _expStill += dt;
                    }
                    // szarza jazdy (epizod = wejscie w zachowanie szarzy albo rozkaz Charge)
                    if (cav)
                    {
                        bool ch = beh.IndexOf("Charge", StringComparison.Ordinal) >= 0 || ord == MovementOrder.MovementOrderEnum.Charge;
                        if (ch && !s.InCharge) { _chargeEp++; s.FirstDone = false; s.ChargeAt = now; }
                        else if (!ch && s.InCharge && !s.FirstDone) _chargeNoBlow++;
                        s.InCharge = ch;
                    }
                    // reakcja na ostrzal
                    React(s, f, beh, ord, now);
                }
                catch (Exception e) { Stum("Pomiar", e); }
            }
        }

        private static void React(FS s, Formation f, string beh, MovementOrder.MovementOrderEnum ord, float now)
        {
            var ml = s.MLoss;
            while (ml.Count > 0 && ml[0] < now - LossWin) ml.RemoveAt(0);
            if (s.EvAt >= 0f)
            {
                if (beh.IndexOf("Charge", StringComparison.Ordinal) >= 0 || ord == MovementOrder.MovementOrderEnum.Charge) s.EvCharge = true;
                if (now < s.EvAt + ReactWin) return;
                string kind = "stal";
                float dist = -1f, along = 0f;
                if (s.Pos.IsValid && s.EvPos.IsValid)
                {
                    Vec2 d = s.Pos - s.EvPos;
                    dist = d.Length;
                    if (s.EvAxis.IsValid) along = d.x * s.EvAxis.x + d.y * s.EvAxis.y;
                    bool cavCls = s.EvCls == "jaz" || s.EvCls == "kstr";
                    float run = cavCls ? 5f : 2.5f;
                    if (s.EvCharge && along > 8f) kind = "szarza";
                    else if (dist < 8f) kind = "stal";
                    else if (along < -8f) kind = "odwrot";
                    else if (along > 8f) kind = along / ReactWin >= run ? "biegl" : "szedl";
                    else kind = "w bok";
                }
                string key = s.EvCls + " " + kind;
                int n;
                Reac.TryGetValue(key, out n);
                Reac[key] = n + 1;
                if (_evLines < MaxEvLines)
                {
                    _evLines++;
                    Log.Info(Tag + " ostrzal: t=" + F0(s.EvAt - _t0) + " | szyk " + FName(f) + " " + s.EvCls + " " + s.EvN + " ludzi (" + SideTxt(s.T) + ") | strata od pociskow "
                             + s.EvLoss + " (" + Pct(s.EvLoss, s.EvN + s.EvLoss) + ") w " + F0(LossWin) + " s | nastepne " + F0(ReactWin) + " s: " + kind
                             + (dist >= 0f ? ", przesuniecie " + F0(dist) + " m (ku wrogowi " + F0(along) + " m, " + F1(along / ReactWin) + " m/s)" : "")
                             + ", zachowanie " + s.EvBeh + " -> " + beh + ".");
                }
                s.EvAt = -1f;
                s.EvCool = now + ReactWin;
                return;
            }
            if (now < s.EvCool) return;
            int loss = ml.Count, men = f.CountOfUnits;
            if (loss < LossMin || loss < LossShare * (men + loss)) return;
            s.EvAt = now;
            s.EvPos = s.Pos;
            s.EvAxis = Vec2.Invalid;
            if (s.Near != null && s.Pos.IsValid)
            {
                Vec2 ep = s.Near.CachedAveragePosition;
                if (ep.IsValid)
                {
                    Vec2 a = ep - s.Pos;
                    float l = a.Length;
                    if (l > 1f) s.EvAxis = new Vec2(a.x / l, a.y / l);
                }
            }
            s.EvLoss = loss;
            s.EvN = men;
            s.EvBeh = beh;
            s.EvCls = s.Cls;
            s.EvCharge = false;
        }

        /// <summary>Czy jakis szyk strzelcow wroga (pieszy, nie w zwarciu) stoi 30-200 m od jazdy i zaden szyk piechoty wroga nie zaslania drogi.</summary>
        private static bool Exposed(FS c)
        {
            foreach (var e in Forms.Values)
            {
                if (e.Cls != "str" || e.F == null || e.F.CountOfUnits < MinCav || e.Gap < ContactGap || !e.Pos.IsValid) continue;
                if (!MissionTeams.Enemies(c.T, e.T)) continue;
                float d = c.Pos.Distance(e.Pos);
                if (d < ExposeMin || d > ExposeMax) continue;
                bool blocked = false;
                foreach (var b in Forms.Values)
                {
                    if (ReferenceEquals(b, e) || b.Cls != "pie" || b.F == null || b.F.CountOfUnits < 10 || !b.Pos.IsValid) continue;
                    if (!MissionTeams.Enemies(c.T, b.T)) continue;
                    float r = 0.5f * b.F.Width + BlockPad;
                    if (SegDist(b.Pos, c.Pos, e.Pos) < r) { blocked = true; break; }
                }
                if (!blocked) return true;
            }
            return false;
        }

        private static float SegDist(Vec2 p, Vec2 a, Vec2 b)
        {
            float vx = b.x - a.x, vy = b.y - a.y, wx = p.x - a.x, wy = p.y - a.y;
            float l2 = vx * vx + vy * vy;
            float t = l2 > 0.0001f ? (wx * vx + wy * vy) / l2 : 0f;
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            float dx = a.x + t * vx - p.x, dy = a.y + t * vy - p.y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        // ------------------------------------------------------------ linie

        private static void PlanLine(PlanEv e, float now)
        {
            try
            {
                if (e.T == null || !AiTeam(e.T)) return;
                var tr = Tr(e.T);
                if (!_started) { tr.Dec0++; return; }
                tr.Chg++;
                if (_planLines >= MaxPlanLines) { _planSkip++; return; }
                _planLines++;
                Log.Info(Tag + " plan: t=" + F0(now - _t0) + " | T" + e.T.TeamIndex + " (" + SideTxt(e.T) + ") | " + e.From + " -> " + e.To + " | wagi " + e.Top
                         + " | powod: " + e.Why + " | zmiana nr " + tr.Chg + ".");
            }
            catch (Exception x) { Stum("Plan", x); }
        }

        private static void WriteStart(Mission m, float now)
        {
            // sklad stron wedlug klas (agent: konno, bron dystansowa)
            try
            {
                foreach (var a in m.Agents)
                {
                    try
                    {
                        if (a == null || !a.IsActive() || !a.IsHuman || a.Team == null) continue;
                        int s = SideIx(a.Team.Side);
                        if (s < 0) continue;
                        bool mounted = a.HasMount, ranged = a.IsRangedCached;
                        Comp0[s, mounted ? (ranged ? 3 : 2) : (ranged ? 1 : 0)]++;
                    }
                    catch (Exception e) { Stum("Sklad", e); }
                }
            }
            catch (Exception e) { Stum("Sklad", e); }
            for (int s = 0; s < 2; s++) Interlocked.Add(ref Built[s], Comp0[s, 0] + Comp0[s, 1] + Comp0[s, 2] + Comp0[s, 3]);   // wejscia = sklad startu + posilki (OnBuild)
            var sb = Sb;
            sb.Length = 0;
            sb.Append(Tag).Append(" start: t=").Append(F0(now)).Append(" s misji | sklad A: ").Append(CompTxt(0)).Append("; O: ").Append(CompTxt(1)).Append(" | druzyny AI:");
            int n = 0;
            foreach (var tr in Teams.Values)
            {
                try
                {
                    var t = tr.T;
                    if (!AiTeam(t)) continue;
                    int men = 0;
                    var fl = t.FormationsIncludingSpecialAndEmpty;
                    for (int j = 0; fl != null && j < fl.Count; j++) if (fl[j] != null) men += fl[j].CountOfUnits;
                    tr.Men0 = men;
                    tr.Gen = General(t);
                    sb.Append(n++ > 0 ? ";" : "").Append(" T").Append(t.TeamIndex).Append(" (").Append(SideTxt(t)).Append(") ludzi ").Append(men).Append(", wodz ").Append(tr.Gen)
                      .Append(", plan otwarcia ").Append(tr.Open ?? "-").Append(" (decyzji przed startem ").Append(tr.Dec0).Append(')');
                }
                catch (Exception e) { Stum("Start.Druzyna", e); }
            }
            if (n == 0) sb.Append(" -");
            sb.Append(" | rzut losu - (pakiet 3 nie wdrozony) | latki: ").Append(_note)
              .Append(" | os czasu co ").Append(F0(Every)).Append(" s, kontakt szyku < ").Append(F0(ContactGap)).Append(" m od najblizszego szyku wroga (mediana minus pol glebokosci obu).");
            Log.Info(sb.ToString());
        }

        private static string CompTxt(int s)
        {
            return "piechota " + Comp0[s, 0] + ", strzelcy " + Comp0[s, 1] + ", jazda " + Comp0[s, 2] + ", konni strzelcy " + Comp0[s, 3];
        }

        private static string General(Team t)
        {
            try
            {
                var g = t.GeneralAgent;
                if (g == null) return "brak";
                var co = g.Character as CharacterObject;
                var h = co != null ? co.HeroObject : null;
                string nm = g.Name != null ? g.Name.ToString() : "?";
                if (h == null) return nm + " (bez bohatera)";
                int v = h.GetTraitLevel(DefaultTraits.Valor), c = h.GetTraitLevel(DefaultTraits.Calculating);
                return nm + " (Valor " + v + ", Calculating " + c + ")";
            }
            catch { return "?"; }
        }

        private static void Timeline(Mission m, float now)
        {
            _readings++;
            foreach (var tr in Teams.Values)
            {
                var t = tr.T;
                try { if (!AiTeam(t)) continue; } catch { continue; }
                if (_lines >= MaxLines) { _linesSkip++; continue; }
                var sb = Sb;
                sb.Length = 0;
                try
                {
                    sb.Append(Tag).Append(" t=").Append(F0(now - _t0)).Append(" | T").Append(t.TeamIndex).Append(" (").Append(SideTxt(t)).Append(") | plan ").Append(PlanOf(t)).Append(" |");
                    var fl = t.FormationsIncludingSpecialAndEmpty;
                    int fn = 0;
                    for (int j = 0; fl != null && j < fl.Count; j++)
                    {
                        try
                        {
                            var f = fl[j];
                            if (f == null || f.CountOfUnits <= 0) continue;
                            FS s;
                            if (!Forms.TryGetValue(f, out s)) continue;
                            sb.Append(fn++ > 0 ? ";" : "").Append(' ').Append(FName(f)).Append(' ').Append(s.Cls).Append(' ').Append(f.CountOfUnits);
                            if (!f.IsAIControlled) sb.Append("(gracz)");
                            var b = f.AI != null ? f.AI.ActiveBehavior : null;
                            sb.Append(' ').Append(BehName(b)).Append('/').Append(OrdName(f.GetReadonlyMovementOrderReference().OrderEnum));
                            float d2 = f.CachedClosestEnemyFormationDistanceSquared;
                            sb.Append(' ').Append(d2 >= 0f && d2 < 1e10f ? F0((float)Math.Sqrt(d2)) + "m" : "-");
                            if (s.Gap < ContactGap) sb.Append(" zw");
                            sb.Append(" p").Append(s.WinHit);
                            if (s.WinHorse > 0) sb.Append(" k").Append(s.WinHorse);
                            if (s.Cls == "jaz" || s.Cls == "kstr") sb.Append(CavTxt(f, b, now));
                        }
                        catch (Exception e) { Stum("Os.Szyk", e); sb.Append(" ?"); }
                    }
                    if (fn == 0) sb.Append(" brak");
                    sb.Append('.');
                    _lines++;
                    Log.Info(sb.ToString());
                }
                catch (Exception e) { Stum("Os", e); }
            }
            foreach (var s in Forms.Values) { s.WinHit = 0; s.WinHorse = 0; }
        }

        /// <summary>Jazda: stan szarzy RBM i klasa celu, trzy najwyzsze wagi zachowan (WeightFactor x premia aktywnego jak FindBestBehavior).</summary>
        private static string CavTxt(Formation f, BehaviorComponent active, float now)
        {
            var sb = new StringBuilder(120);
            try
            {
                if (active != null && _tCavCharge != null && _tCavCharge.IsInstanceOfType(active))
                {
                    string st = "?", tg = "-";
                    try { if (_fChargeState != null) st = Convert.ToString(_fChargeState.GetValue(active), Inv); } catch { }
                    try
                    {
                        var lq = _fLastTarget != null ? _fLastTarget.GetValue(active) as FormationQuerySystem : null;
                        if (lq != null && lq.Formation != null) tg = FName(lq.Formation) + " " + Cls(lq.Formation);
                    }
                    catch { }
                    sb.Append(" szarza ").Append(st).Append(">").Append(tg);
                }
            }
            catch { }
            try
            {
                var ai = f.AI;
                var lst = ai != null && _fBeh != null ? _fBeh(ai) : null;
                if (lst != null)
                {
                    BehaviorComponent[] arr;
                    try { arr = lst.ToArray(); } catch { arr = null; }
                    if (arr != null)
                    {
                        var ws = new List<KeyValuePair<string, float>>(arr.Length);
                        for (int i = 0; i < arr.Length; i++)
                        {
                            var b = arr[i];
                            if (b == null) continue;
                            float w = b.WeightFactor;
                            if (w <= 1e-7f) continue;
                            bool act = ReferenceEquals(b, ai.ActiveBehavior);
                            if (act)
                            {
                                float k = (b.PreserveExpireTime - now) / 5f;
                                if (k < 0f) k = 0f; else if (k > 1f) k = 1f;
                                w *= 1.2f + 0.8f * k;
                            }
                            ws.Add(new KeyValuePair<string, float>(BehName(b) + (act ? "*" : ""), w));
                        }
                        ws.Sort((a, b) => b.Value.CompareTo(a.Value));
                        sb.Append(" [wagi");
                        for (int i = 0; i < ws.Count && i < 3; i++) sb.Append(i > 0 ? ", " : " ").Append(ws[i].Key).Append(' ').Append(F1(ws[i].Value));
                        if (ws.Count == 0) sb.Append(" -");
                        sb.Append(']');
                    }
                }
            }
            catch (Exception e) { Stum("Os.Wagi", e); }
            return sb.ToString();
        }

        private static void WriteEnd(Mission m)
        {
            float now = Now(m);
            var sb = Sb;
            sb.Length = 0;
            sb.Append(Tag).Append(": koniec | ").Append(MissionTeams.Kind(m)).Append(" | czas ").Append(F0(now - _t0)).Append(" s, pierwszy kontakt ")
              .Append(_contactAt >= 0f ? F0(_contactAt - _t0) + " s" : "-").Append(" | wynik ");
            try { var r = m.MissionResult; sb.Append(r != null ? r.BattleState.ToString() : "-"); } catch { sb.Append('?'); }
            // padli wedlug zrodla i fazy, straty stron
            for (int s = 0; s < 2; s++)
            {
                int all = 0;
                sb.Append(" | padli ").Append(s == 0 ? "A" : "O").Append(':');
                for (int ph = 0; ph < 2; ph++)
                {
                    sb.Append(ph == 0 ? " przed kontaktem " : "; po kontakcie ");
                    for (int k = 0; k < 5; k++)
                    {
                        all += Dead[s, ph, k];
                        sb.Append(k > 0 ? ", " : "").Append(SrcName[k]).Append(' ').Append(Dead[s, ph, k]);
                    }
                }
                sb.Append(" (razem ").Append(all).Append(" z ").Append(Built[s]).Append(" wejsc, straty ").Append(Pct(all, Built[s])).Append(')');
            }
            // plany druzyn
            sb.Append(" | plany:");
            int tn = 0;
            foreach (var tr in Teams.Values)
            {
                if (tr.T == null) continue;
                sb.Append(tn++ > 0 ? ";" : "").Append(" T").Append(tr.T.TeamIndex).Append(" (").Append(SideTxt(tr.T)).Append(", ").Append(tr.Gen)
                  .Append(") otwarcie ").Append(tr.Open ?? "-").Append(", zmian ").Append(tr.Chg).Append(" (co 1 s widzianych ").Append(tr.ChgSeen).Append("), czas w planach ")
                  .Append(Top(tr.Sec, 4));
            }
            if (tn == 0) sb.Append(" -");
            sb.Append(" | jazda AI pod ostrzalem (trafienie w ").Append(F0(FireRecent)).Append(" s) szyko-s ").Append(F0(_cavFire)).Append(", w tym stala ").Append(F0(_cavFireStill))
              .Append(" (").Append(_cavFire > 0.5f ? (100f * _cavFireStill / _cavFire).ToString("0", Inv) + "%" : "-").Append(')')
              .Append(" | strzelcy wroga odslonieci w zasiegu szarzy (").Append(F0(ExposeMin)).Append('-').Append(F0(ExposeMax)).Append(" m) szyko-s ").Append(F0(_exp))
              .Append(", jazda stala ").Append(F0(_expStill))
              .Append(" | trafienia pociskami jazdy AI: ludzie ").Append(_hitRider).Append(", konie ").Append(_hitHorse)
              .Append(" | reakcje na ostrzal (strata >= ").Append((LossShare * 100f).ToString("0", Inv)).Append("% w ").Append(F0(LossWin)).Append(" s): ").Append(TopI(Reac, 16))
              .Append(" | szarze jazdy AI ").Append(_chargeEp).Append(" (bez ciosu ").Append(_chargeNoBlow).Append("), pierwszy cios: ").Append(TopI(FirstBlow, 10))
              .Append(" | zachowania AI szyko-s przed kontaktem ").Append(Top(BehPre, 8)).Append("; po kontakcie ").Append(Top(BehPost, 10))
              .Append(" | latki wedlug watku: decyzje glowny ").Append(Volatile.Read(ref _decMain)).Append(" / inny ").Append(Volatile.Read(ref _decOther))
              .Append(", wagi glowny ").Append(Volatile.Read(ref _wMain)).Append(" / inny ").Append(Volatile.Read(ref _wOther))
              .Append(", bledy latek ").Append(Volatile.Read(ref _postErr)).Append(", zgubione plany ").Append(Volatile.Read(ref _pendDrop))
              .Append(" | linie osi ").Append(_lines).Append(" (pominiete ").Append(_linesSkip).Append("), odczytow ").Append(_readings).Append(", linie planu pominiete ").Append(_planSkip)
              .Append(", zgubione zdarzenia ").Append(_qDrop)
              .Append(" | potkniecia ").Append(_stum).Append('.');
            Log.Info(sb.ToString());
        }

        // ------------------------------------------------------------ pomocnicze (odczyty)

        /// <summary>Klasa szyku wedlug skladu (zapytania ReadOnly - pamiec podreczna, bez liczenia): pie / str / jaz / kstr.</summary>
        internal static string Cls(Formation f)
        {
            var q = f != null ? f.QuerySystem : null;
            if (q == null) return "?";
            float bi = q.InfantryUnitRatioReadOnly, r = q.RangedUnitRatioReadOnly, c = q.CavalryUnitRatioReadOnly, h = q.RangedCavalryUnitRatioReadOnly;
            string best = "pie";
            float bv = bi;
            if (r > bv) { best = "str"; bv = r; }
            if (c > bv) { best = "jaz"; bv = c; }
            if (h > bv) { best = "kstr"; }
            return best;
        }

        /// <summary>Etykieta "AI bitwy (333)" przy kluczu 437: klasa wedlug skladu (nie indeks szyku) i udzial konnych.</summary>
        internal static string ClassLong(Formation f)
        {
            var q = f != null ? f.QuerySystem : null;
            if (q == null) return "?";
            string c = Cls(f);
            string n = c == "pie" ? "piechota" : (c == "str" ? "strzelcy" : (c == "jaz" ? "jazda" : "konni strzelcy"));
            float mounted = q.CavalryUnitRatioReadOnly + q.RangedCavalryUnitRatioReadOnly;
            if (mounted >= 0.05f && c != "jaz" && c != "kstr") n += " (konnych " + (100f * mounted).ToString("0", Inv) + "%)";
            return "wg skladu " + n;
        }

        private static string PlanOf(Team t)
        {
            var ai = t.TeamAI;
            if (ai == null || _fCur == null) return "-";
            var tac = _fCur(ai);
            return tac != null ? TacName(tac) : "-";
        }

        private static string TacName(TacticComponent t)
        {
            string n = t.GetType().Name;
            if (n.StartsWith("RBMTactic", StringComparison.Ordinal) && n.Length > 9) return "RBM:" + n.Substring(9);
            if (n.StartsWith("Tactic", StringComparison.Ordinal) && n.Length > 6) return n.Substring(6);
            return n;
        }

        private static string BehName(BehaviorComponent b)
        {
            if (b == null) return "-";
            string n = b.GetType().Name;
            if (n.StartsWith("RBMBehavior", StringComparison.Ordinal) && n.Length > 11) return "RBM:" + n.Substring(11);
            if (n.StartsWith("Behavior", StringComparison.Ordinal) && n.Length > 8) return n.Substring(8);
            return n;
        }

        private static string OrdName(MovementOrder.MovementOrderEnum o) { return o.ToString(); }

        private static string FName(Formation f) { return "F" + (int)f.FormationIndex; }

        private static string SideTxt(Team t)
        {
            if (t == null) return "?";
            return (t.Side == BattleSideEnum.Attacker ? "A" : (t.Side == BattleSideEnum.Defender ? "O" : "?")) + ", "
                   + (t.IsPlayerTeam ? "druzyna gracza" : (t.IsPlayerAlly ? "z graczem" : "wrog gracza"));
        }

        private static string Top(Dictionary<string, float> d, int k)
        {
            if (d.Count == 0) return "-";
            var l = new List<KeyValuePair<string, float>>(d);
            l.Sort((a, b) => b.Value.CompareTo(a.Value));
            var sb = new StringBuilder(160);
            for (int i = 0; i < l.Count && i < k; i++) sb.Append(i > 0 ? ", " : "").Append(l[i].Key).Append(' ').Append(F0(l[i].Value));
            if (l.Count > k) sb.Append(" (+").Append(l.Count - k).Append(')');
            return sb.ToString();
        }

        private static string TopI(Dictionary<string, int> d, int k)
        {
            if (d.Count == 0) return "-";
            var l = new List<KeyValuePair<string, int>>(d);
            l.Sort((a, b) => b.Value.CompareTo(a.Value));
            var sb = new StringBuilder(160);
            for (int i = 0; i < l.Count && i < k; i++) sb.Append(i > 0 ? ", " : "").Append(l[i].Key).Append(' ').Append(l[i].Value);
            if (l.Count > k) sb.Append(" (+").Append(l.Count - k).Append(')');
            return sb.ToString();
        }

        private static string Pct(int a, int b) { return b > 0 ? (100f * a / b).ToString("0.0", Inv) + "%" : "-"; }
        private static string F0(float v) { return v.ToString("0", Inv); }
        private static string F1(float v) { return v.ToString("0.0", Inv); }
        private static string F2(float v) { return v.ToString("0.00", Inv); }

        // ------------------------------------------------------------ zachowanie misji

        internal sealed class Logic : MissionLogic
        {
            public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

            public override void AfterStart()
            {
                try { Begin(Mission); } catch (Exception e) { Stum("Begin", e); }
                base.AfterStart();
            }

            public override void OnAgentBuild(Agent agent, Banner banner)
            {
                OnBuild(agent);
            }

            public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData attackCollisionData)
            {
                OnHit(affectedAgent, affectorAgent, in blow);
            }

            public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
            {
                OnRemoved(affectedAgent, affectorAgent, agentState, blow);
            }

            public override void OnMissionTick(float dt)
            {
                Tick(Mission);
            }

            protected override void OnEndMission()
            {
                try { End(Mission); } catch (Exception e) { Stum("End", e); }
                base.OnEndMission();
            }
        }
    }
}
