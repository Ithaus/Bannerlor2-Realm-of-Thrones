using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P2: ROGI, ROZKAZY KAPITANOW, MOTTA PRZY SZARZY, PRZEMOWY RODOW, OKRZYKI CHORAGWI.
    ///  - Szarza (BattleCries.ChargeStarted -> OnChargeStarted, glowny watek, raz na przejscie szyku AI albo gracza): decyzja od razu - bohater-kapitan szyku
    ///    (mezczyzna, nie gracz, raz na bohatera na bitwe; klucz BattleChargeMottos) krzyczy motto rodu (mo_&lt;rod&gt;, inaczej motto regionu), a gdy motta nie ma -
    ///    kapitan "Charge!" (or_&lt;region&gt;_charge; BattleCaptainOrders, ten sam rozkaz szyku co 20 s); okrzyk ludzi (BattleCries) czeka TakeYellLead: 0,4 s po
    ///    rozkazie, do 2,5 s po motcie. Rog szarzy kultury (BattleHornsCulture, szyk od 20 ludzi) - nie gra, gdy rog szarzy tej strony gra Immersive Battlefields
    ///    (ForeignVoices.IbChargeHorn: strona gracza EnableHorn od SmallHorn, wrog EnableHorn2 od SmallHorn2). Tetent konnicy - BattleSound.Field.cs.
    ///  - Zmiana rozkazu szyku (takt 0,25 s, po rozstawieniu, przed koncem bitwy; pierwszy odczyt szyku bez dzwieku): Advance / Move - "march" i rog marszu
    ///    (szyk od 20 ludzi, gracza od 8; wrog dalej niz 60 m; 20 s po szarzy - nie), Retreat - rog odwrotu i "fallback", FallBack - "fallback",
    ///    rogi marszu i odwrotu tylko dla szykow gracza (szykom AI gra je gra - TacticComponent.SoundTacticalHorn, krytyk K8; rog szarzy AI - tak),
    ///    Stop po ruchu przy wrogu do 200 m - "hold". Szarza tylko z sygnalu BattleCries (K4 - bez drugiego detektora).
    ///  - Ostrzal: pierwszy pocisk w szyk piechoty po 30 s spokoju - "shields"; pierwszy strzal szyku strzelcow po 15 s ciszy - "loose" (zbiory z wywolan
    ///    zwrotnych pod lockiem, tu tylko odczyt).
    ///  - P1: lucznicy za piechote - "archback", konnica na skrzydlo - "flank"; P3: padl sztandar / dowodca kompanii - wlasna strona "ob" (Save the banner! Rally to me!)
    ///    i rog zbiorki, najblizszy szyk wroga "bd" (Their banner is down!); zlamanie kompanii - najblizszy szyk wroga "br" (They break!). Raz na (szyk, kompanie).
    ///  - Odstepy (czas misji i rzeczywisty): rozkaz szyku: ten sam co 20 s (shields 30, loose 25), dowolny co 6 s; glos rozkazu druzyny co 2,5 s (gracza 1,5 s);
    ///    rog druzyny co 10 s, ten sam sygnal: marsz 40 s, szarza 15 s, odwrot i zbiorka 20 s. ROTBA mowi rozkazy gracza (OrderVoiceChance > 0) - nasze rozkazy
    ///    szykow gracza co drugi raz.
    ///  - Przemowa (BattleHouseSpeeches): raz na strone, 3 s po OnDeploymentFinished (albo po rozstawieniu bez tego zdarzenia), strona od 40 ludzi, mowca:
    ///    strona gracza - bohater z najwyzszym Leadership (nie gracz), wrog - general (inaczej jak u gracza); wrog dalej niz 150 m od jego szyku; wroga przemowa
    ///    tylko do 250 m od sluchacza, po naszej. Klip: wlasna przemowa rodu (sp_&lt;rod&gt;), inaczej przemowa regionu (sp_&lt;region&gt;) i zaraz po niej motto rodu
    ///    dowodcy albo regionu. Nocna Straz przeciw dzikim - dwa dzwieki rogu, przeciw Innym - trzy (kanon), raz na bitwe.
    /// </summary>
    internal static partial class BattleSound
    {
        private const int OMarch = 0, OCharge = 1, OHold = 2, OShields = 3, OLoose = 4, OFallback = 5, ORally = 6, OArchback = 7, OFlank = 8, OKinds = 9;
        private static readonly string[] OName = { "march", "charge", "hold", "shields", "loose", "fallback", "rally", "archback", "flank" };
        private static readonly float[] OGap = { 20f, 20f, 20f, 30f, 25f, 20f, 30f, 30f, 30f };
        private const int HMarch = 0, HCharge = 1, HRetreat = 2, HRally = 3, HKinds = 4;
        private static readonly string[] HName = { "march", "charge", "retreat", "rally" };
        private static readonly float[] HGap = { 40f, 15f, 20f, 20f };
        private const float HornTeamGap = 10f, OrderFormGap = 6f, OrderTeamGapAi = 2.5f, OrderTeamGapPlayer = 1.5f;
        private const int MinMenAi = 20, MinMenPlayer = 8, MinMenHorn = 20, MinMenSpeech = 40;
        // odleglosci 3D (min-max m, x glosnosc)
        private const float OrderMin = 30f, OrderMax = 250f, MottoMin = 35f, MottoMax = 450f, SpeechMin = 40f, SpeechMax = 300f, HornMin = 50f, HornMax = 700f;
        private const float SpeechEnemyListener = 250f, SpeechEnemyFar = 150f;

        private sealed class FState
        {
            internal MovementOrder.MovementOrderEnum Order;
            internal bool Init;
            internal float ChargeAt = -1000f, ChargeEnd = -1000f, LastHit = -1000f, LastShot = -1000f;
            internal Stamp AnyOrder;
            internal readonly Stamp[] Kind = new Stamp[OKinds];
        }

        private sealed class TState
        {
            internal Stamp AnyHorn, OrderVoice;
            internal readonly Stamp[] Horn = new Stamp[HKinds];
            internal bool PlayerToggle;
        }

        private sealed class ChargeEv { internal Formation F; internal Clip Motto, Order; internal Agent Spk; internal bool Horn; }

        private static readonly Dictionary<Formation, FState> _fs = new Dictionary<Formation, FState>();
        private static readonly Dictionary<Team, TState> _ts = new Dictionary<Team, TState>();
        private static readonly HashSet<Agent> _mottoDone = new HashSet<Agent>();
        private static readonly HashSet<long> _bannerDone = new HashSet<long>();
        private static readonly List<ChargeEv> _chargeQ = new List<ChargeEv>();
        private static Formation _leadF;
        private static float _lead;

        // kolejki z wywolan zwrotnych silnika (pod _q)
        private static readonly object _q = new object();
        private static readonly HashSet<Formation> _shotQ = new HashSet<Formation>();
        private static readonly HashSet<Formation> _hitQ = new HashSet<Formation>();
        private static readonly List<Formation> _drain = new List<Formation>();

        // przemowy
        private static float _speechAt = -1f, _speech2At = -1f;
        private static Agent _speech2Spk;
        private static Team _speech2Team;
        private static bool _speechDone, _nwDone;

        // liczniki
        private static readonly int[] _cOrder = new int[OKinds];
        private static readonly int[] _cHorn = new int[HKinds];
        private static int _cHornIb, _cHornNw, _cOrderBa, _cMotto, _cSpHouse, _cSpRegion, _cSpMotto, _cSpSkip, _cBreak, _cBannerDown, _cOurBanner;
        private static string _spSample;

        private static FState FS(Formation f)
        {
            FState s;
            if (!_fs.TryGetValue(f, out s)) { s = new FState(); _fs[f] = s; }
            return s;
        }

        private static TState TS(Team t)
        {
            TState s;
            if (!_ts.TryGetValue(t, out s)) { s = new TState(); _ts[t] = s; }
            return s;
        }

        private static void ResetOrders()
        {
            _fs.Clear(); _ts.Clear(); _mottoDone.Clear(); _bannerDone.Clear(); _chargeQ.Clear(); _spk.Clear(); _fcul.Clear();
            _leadF = null; _lead = 0f;
            lock (_q) { _shotQ.Clear(); _hitQ.Clear(); }
            _drain.Clear();
            _speechAt = _speech2At = -1f; _speech2Spk = null; _speech2Team = null; _speechDone = _nwDone = false;
            for (int i = 0; i < OKinds; i++) _cOrder[i] = 0;
            for (int i = 0; i < HKinds; i++) _cHorn[i] = 0;
            _cHornIb = _cHornNw = _cOrderBa = _cMotto = _cSpHouse = _cSpRegion = _cSpMotto = _cSpSkip = _cBreak = _cBannerDown = _cOurBanner = 0;
            _spSample = null;
        }

        // ============================================================ szarza (K4: jedyny sygnal z BattleCries)

        /// <summary>BattleCries.ChargeStarted (glowny watek, w BattleCries.Tick): decyzja motto / rozkaz / rog teraz, dzwiek w Tick tej samej klatki.</summary>
        internal static void OnChargeStarted(Formation f)
        {
            _leadF = null; _lead = 0f;
            var c = On();
            if (c == null || f == null || f.CountOfUnits <= 0 || !MissionTeams.Valid(f.Team)) return;
            var m = Mission.Current;
            if (m == null || m.MissionEnded) return;
            var st = FS(f);
            st.ChargeAt = _now;
            var ev = new ChargeEv { F = f };
            BasicCharacterObject ch;
            string cul = FormCul(f, out ch);
            bool silent = Silent(ch, cul);
            var cap = f.Captain;
            float k = LoudScale(c);
            // motto: bohater-kapitan, raz na bitwe (zaliczone dopiero, gdy zagra - DrainCharges); poza zasiegiem sluchacza - bez motta i bez czekania okrzyku
            if (c.BattleChargeMottos && cap != null && cap.IsHero && CanSpeak(cap) && !_mottoDone.Contains(cap) && LisDist(HeadOf(cap)) <= MottoMax * k + FarExtra)
            {
                string house = HouseOf(cap);
                var mc = Pick("mo", house) ?? Pick("mo", MottoRegion(CulOf(cap.Character)));
                if (mc != null) { ev.Motto = mc; ev.Spk = cap; }
            }
            if (ev.Motto == null && c.BattleCaptainOrders && !silent && st.Kind[OCharge].Ready(OGap[OCharge]) && st.AnyOrder.Ready(OrderFormGap))
            {
                var spk = SpeakerOf(f);
                if (spk != null && LisDist(HeadOf(spk)) <= OrderMax * k + FarExtra && PlayerVoiceOk(f))
                {
                    var oc = OrderClip(cul, OCharge);
                    if (oc != null) { ev.Order = oc; ev.Spk = spk; }
                }
            }
            ev.Horn = c.BattleHornsCulture && !silent && f.CountOfUnits >= MinMenHorn;
            if (ev.Motto != null) { _leadF = f; _lead = Math.Min(YellLeadMax, ev.Motto.Dur * 0.9f); }
            else if (ev.Order != null) { _leadF = f; _lead = OrderLead; }
            ChargeHorse(f, c);   // tetent konnicy (BattleSound.Field.cs)
            if (ev.Motto != null || ev.Order != null || ev.Horn) { if (_chargeQ.Count < 64) _chargeQ.Add(ev); }
        }

        /// <summary>BattleCries: ile sekund okrzyk ludzi ma czekac na glos dowodcy tego szyku (0 - nic nie mowi). Raz na sygnal.</summary>
        internal static float TakeYellLead(Formation f)
        {
            if (f == null || !ReferenceEquals(f, _leadF)) return 0f;
            float l = _lead;
            _leadF = null; _lead = 0f;
            return l;
        }

        private static bool HasChargeQueue() { return _chargeQ.Count > 0; }

        /// <summary>BattleCries: mowca zaraz powie motto / rozkaz (kolejka szarzy tej klatki) albo wlasnie mowi - bez natywnego okrzyku na jego glosie.</summary>
        internal static bool Busy(Agent a)
        {
            if (a == null) return false;
            for (int i = 0; i < _chargeQ.Count; i++) if (_chargeQ[i].Spk == a) return true;
            return IsSpeaking(a);
        }

        private static void DrainCharges(Mission m, Settings c)
        {
            for (int i = 0; i < _chargeQ.Count; i++)
            {
                var ev = _chargeQ[i];
                var f = ev.F;
                if (f == null || f.CountOfUnits <= 0) continue;
                var st = FS(f);
                if (ev.Motto != null && ev.Spk != null && ev.Spk.IsActive())
                {
                    if (!_mottoDone.Contains(ev.Spk) && Play(m, ev.Motto, GVoice, HeadOf(ev.Spk), MottoMin, MottoMax, true, ev.Spk, null, "motto") != null)
                    {
                        _mottoDone.Add(ev.Spk);
                        _cMotto++;
                    }
                }
                else if (ev.Order != null && ev.Spk != null && ev.Spk.IsActive())
                {
                    if (Say(m, f, st, OCharge, ev.Order, ev.Spk)) { }
                }
                if (ev.Horn) Horn(m, f, HCharge, true);
            }
            _chargeQ.Clear();
        }

        // ============================================================ zmiany rozkazow szykow (takt 0,25 s)

        private static void Orders(Mission m, Settings c)
        {
            if (!m.IsDeploymentFinished || m.MissionEnded) return;
            bool orders = c.BattleCaptainOrders, horns = c.BattleHornsCulture;
            var teams = m.Teams;
            int nt = teams != null ? teams.Count : 0;
            for (int i = 0; i < nt; i++)
            {
                var t = teams[i];
                if (!MissionTeams.Valid(t)) continue;
                var fl = t.FormationsIncludingSpecialAndEmpty;
                int nf = fl != null ? fl.Count : 0;
                for (int j = 0; j < nf; j++)
                {
                    var f = fl[j];
                    if (f == null || f.CountOfUnits <= 0) continue;
                    var st = FS(f);
                    var oe = f.GetReadonlyMovementOrderReference().OrderEnum;
                    if (!st.Init) { st.Init = true; st.Order = oe; continue; }
                    if (oe == st.Order) continue;
                    var prev = st.Order;
                    st.Order = oe;
                    bool wasCharge = prev == MovementOrder.MovementOrderEnum.Charge || prev == MovementOrder.MovementOrderEnum.ChargeToTarget;
                    if (wasCharge) st.ChargeEnd = _now;
                    if (!orders && !horns) continue;
                    OrderChanged(m, f, st, prev, oe, wasCharge, orders, horns);
                }
            }
        }

        private static void OrderChanged(Mission m, Formation f, FState st, MovementOrder.MovementOrderEnum prev, MovementOrder.MovementOrderEnum oe, bool wasCharge,
                                         bool orders, bool horns)
        {
            BasicCharacterObject ch;
            string cul = FormCul(f, out ch);
            if (Silent(ch, cul)) return;
            int men = f.CountOfUnits;
            bool player = !f.IsAIControlled;
            float ed = EnemyDist(f);
            switch (oe)
            {
                case MovementOrder.MovementOrderEnum.Advance:
                case MovementOrder.MovementOrderEnum.Move:
                    if (men < (player ? MinMenPlayer : MinMenAi) || ed < 60f || _now - st.ChargeEnd < 20f) return;
                    if (orders) SayFormation(m, f, st, OMarch, cul);
                    if (horns && player && men >= MinMenHorn) Horn(m, f, HMarch, false);   // AI: rog marszu gra gra (TacticComponent.SoundTacticalHorn) - bez dubla
                    return;
                case MovementOrder.MovementOrderEnum.Retreat:
                    if (orders) SayFormation(m, f, st, OFallback, cul);
                    if (horns && player && men >= MinMenHorn) Horn(m, f, HRetreat, false);   // AI: rog odwrotu gra gra
                    return;
                case MovementOrder.MovementOrderEnum.FallBack:
                    if (orders && men >= MinMenPlayer) SayFormation(m, f, st, OFallback, cul);
                    return;
                case MovementOrder.MovementOrderEnum.Stop:
                    bool moving = prev == MovementOrder.MovementOrderEnum.Advance || prev == MovementOrder.MovementOrderEnum.Move || wasCharge;
                    if (orders && moving && ed < 200f && men >= MinMenPlayer) SayFormation(m, f, st, OHold, cul);
                    return;
            }
        }

        /// <summary>Szyki gracza przy rozkazach BA: co drugi raz (przelacznik na druzyne).</summary>
        private static bool PlayerVoiceOk(Formation f)
        {
            if (f.IsAIControlled || !ForeignVoices.BaOrderVoices) return true;
            var ts = TS(f.Team);
            ts.PlayerToggle = !ts.PlayerToggle;
            if (!ts.PlayerToggle) { _cOrderBa++; return false; }
            return true;
        }

        private static Clip OrderClip(string cul, int kind)
        {
            string reg = OrderRegion(cul);
            if (reg == null) return null;
            if (reg == "dothraki") return kind == OCharge ? Pick("or", "dothraki_charge") : null;
            var oc = Pick("or", reg + "_" + OName[kind]);
            if (oc == null && reg == "ghiscari") oc = Pick("or", "essos_" + OName[kind]);
            return oc;
        }

        private static void SayFormation(Mission m, Formation f, FState st, int kind, string cul)
        {
            if (!st.Kind[kind].Ready(OGap[kind]) || !st.AnyOrder.Ready(OrderFormGap)) return;
            var spk = SpeakerOf(f);
            if (spk == null) return;
            var oc = OrderClip(cul, kind);
            if (oc == null) return;
            if (!PlayerVoiceOk(f)) return;
            Say(m, f, st, kind, oc, spk);
        }

        private static bool Say(Mission m, Formation f, FState st, int kind, Clip oc, Agent spk)
        {
            var ts = TS(f.Team);
            if (!ts.OrderVoice.Ready(f.IsAIControlled ? OrderTeamGapAi : OrderTeamGapPlayer)) return false;
            if (IsSpeaking(spk)) return false;
            if (Play(m, oc, GVoice, HeadOf(spk), OrderMin, OrderMax, true, spk, null, "rozkaz") == null) return false;
            st.Kind[kind].Hit(); st.AnyOrder.Hit(); ts.OrderVoice.Hit();
            _cOrder[kind]++;
            return true;
        }

        /// <summary>Rog kultury szyku (pozycja: kapitan / srodek szyku). charge - ustapienie rogowi szarzy IB.</summary>
        private static void Horn(Mission m, Formation f, int sig, bool charge)
        {
            if (f == null || !MissionTeams.Valid(f.Team)) return;
            BasicCharacterObject ch;
            string cul = FormCul(f, out ch);
            if (Silent(ch, cul)) return;
            var ts = TS(f.Team);
            if (!ts.AnyHorn.Ready(HornTeamGap) || !ts.Horn[sig].Ready(HGap[sig])) return;
            if (charge)
            {
                bool playerSide = ReferenceEquals(f.Team, m.PlayerTeam);
                bool enemySide = ReferenceEquals(f.Team, m.PlayerEnemyTeam);
                if ((playerSide || enemySide) && ForeignVoices.IbChargeHorn(playerSide, f.CountOfUnits)) { _cHornIb++; ts.Horn[sig].Hit(); return; }
            }
            string style = HornStyle(cul);
            if (style == null) return;
            string key;
            if (style == "nightswatch") key = sig == HMarch ? "nightswatch_one" : "north_" + HName[sig];
            else key = style + "_" + HName[sig];
            var hc = Pick("hr", key);
            if (hc == null) return;
            var cap = f.Captain;
            Vec3 pos = cap != null && cap.IsActive() ? HeadOf(cap) : FormPos(f);
            if (Play(m, hc, GHorn, pos, HornMin, HornMax, true, null, null, "rog") == null) return;
            ts.AnyHorn.Hit(); ts.Horn[sig].Hit();
            _cHorn[sig]++;
        }

        // ============================================================ ostrzal: shields / loose

        private static void Volleys(Mission m, Settings c)
        {
            _drain.Clear();
            lock (_q)
            {
                foreach (var f in _hitQ) _drain.Add(f);
                _hitQ.Clear();
            }
            bool orders = c.BattleCaptainOrders && m.IsDeploymentFinished && !m.MissionEnded;
            for (int i = 0; i < _drain.Count; i++)
            {
                var f = _drain[i];
                if (f == null || f.CountOfUnits <= 0 || !MissionTeams.Valid(f.Team)) continue;
                var st = FS(f);
                bool quiet = _now - st.LastHit >= 30f;
                st.LastHit = _now;
                if (!orders || !quiet || f.CountOfUnits < 10) continue;
                bool inf = false;
                try { var q = f.QuerySystem; inf = q != null && (q.IsInfantryFormationReadOnly || q.HasShieldReadOnly) && !q.IsCavalryFormationReadOnly; } catch { }
                if (!inf) continue;
                BasicCharacterObject ch;
                string cul = FormCul(f, out ch);
                if (!Silent(ch, cul)) SayFormation(m, f, st, OShields, cul);
            }
            _drain.Clear();
            lock (_q)
            {
                foreach (var f in _shotQ) _drain.Add(f);
                _shotQ.Clear();
            }
            for (int i = 0; i < _drain.Count; i++)
            {
                var f = _drain[i];
                if (f == null || f.CountOfUnits <= 0 || !MissionTeams.Valid(f.Team)) continue;
                var st = FS(f);
                bool quiet = _now - st.LastShot >= 15f;
                st.LastShot = _now;
                if (!orders || !quiet || f.CountOfUnits < 8) continue;
                bool ranged = false;
                try { var q = f.QuerySystem; ranged = q != null && q.IsRangedFormationReadOnly; } catch { }
                if (!ranged) continue;
                BasicCharacterObject ch;
                string cul = FormCul(f, out ch);
                if (!Silent(ch, cul)) SayFormation(m, f, st, OLoose, cul);
            }
            _drain.Clear();
        }

        // ============================================================ zdarzenia P1 (doktryna) i P3 (choragwie)

        private sealed class TagEv { internal int Kind; internal Formation F; internal int Company; internal Vec3 Pos; }
        private const int EArchback = 0, EFlank = 1, EStandard = 2, ECommander = 3, EBroke = 4;
        private static readonly List<TagEv> _evQ = new List<TagEv>();

        internal static void OnArchersFellBack(Formation f) { Enq(EArchback, f, 0, Vec3.Zero); }
        internal static void OnFlankRide(Formation f) { Enq(EFlank, f, 0, Vec3.Zero); }
        internal static void OnStandardFell(Formation f, int company, Vec3 pos) { Enq(EStandard, f, company, pos); }
        internal static void OnCommanderFell(Formation f, int company, Vec3 pos) { Enq(ECommander, f, company, pos); }
        internal static void OnCompanyBroke(Formation f, int company, Vec3 pos) { Enq(EBroke, f, company, pos); }

        private static void Enq(int kind, Formation f, int company, Vec3 pos)
        {
            if (!_on || f == null || _evQ.Count >= 64) return;
            _evQ.Add(new TagEv { Kind = kind, F = f, Company = company, Pos = pos });
        }

        private static void Events(Mission m, Settings c)
        {
            if (_evQ.Count == 0) return;
            bool orders = c.BattleCaptainOrders && !m.MissionEnded;
            for (int i = 0; i < _evQ.Count && orders; i++)
            {
                var e = _evQ[i];
                var f = e.F;
                if (f == null || !MissionTeams.Valid(f.Team)) continue;
                BasicCharacterObject ch;
                string cul;
                switch (e.Kind)
                {
                    case EArchback:
                    case EFlank:
                        if (f.CountOfUnits <= 0) break;
                        cul = FormCul(f, out ch);
                        if (!Silent(ch, cul)) SayFormation(m, f, FS(f), e.Kind == EArchback ? OArchback : OFlank, cul);
                        break;
                    case EStandard:
                    case ECommander:
                    case EBroke:
                        Banner(m, e, c);
                        break;
                }
            }
            _evQ.Clear();
        }

        /// <summary>Sztandar / dowodca / zlamanie kompanii: raz na (szyk, kompanie, rodzaj).</summary>
        private static void Banner(Mission m, TagEv e, Settings c)
        {
            var f = e.F;
            long key = ((long)f.GetHashCode() << 8) ^ ((long)e.Company << 3) ^ e.Kind;
            if (!_bannerDone.Add(key)) return;
            Vec3 pos = e.Pos.x == 0f && e.Pos.y == 0f ? FormPos(f) : e.Pos;
            if (e.Kind != EBroke && f.CountOfUnits > 0)
            {
                // wlasna strona: "Save the banner! Rally to me!" + rog zbiorki
                BasicCharacterObject ch;
                string cul = FormCul(f, out ch);
                var st = FS(f);
                if (!Silent(ch, cul) && st.Kind[ORally].Ready(OGap[ORally]))
                {
                    var spk = SpeakerOf(f);
                    var cl = Pick("ob", OrderRegion(cul));
                    if (spk != null && cl != null && !IsSpeaking(spk) && Play(m, cl, GVoice, HeadOf(spk), OrderMin, OrderMax, true, spk, null, "sztandar") != null)
                    {
                        st.Kind[ORally].Hit(); _cOurBanner++; _cOrder[ORally]++;
                    }
                    if (c.BattleHornsCulture) Horn(m, f, HRally, false);
                }
            }
            if (e.Kind == ECommander) return;   // padly dowodca - wrog nie widzi tego tak jak upadku sztandaru
            // strona wroga: najblizszy szyk wroga do 150 m od miejsca
            Formation best = null;
            float bd = 150f * 150f;
            var teams = m.Teams;
            for (int i = 0; teams != null && i < teams.Count; i++)
            {
                var t = teams[i];
                if (!MissionTeams.Enemies(t, f.Team)) continue;
                var fl = t.FormationsIncludingSpecialAndEmpty;
                for (int j = 0; fl != null && j < fl.Count; j++)
                {
                    var g = fl[j];
                    if (g == null || g.CountOfUnits <= 0) continue;
                    float d = g.CachedAveragePosition.DistanceSquared(pos.AsVec2);
                    if (d < bd) { bd = d; best = g; }
                }
            }
            if (best == null) return;
            BasicCharacterObject ch2;
            string cul2 = FormCul(best, out ch2);
            if (Silent(ch2, cul2)) return;
            var spk2 = SpeakerOf(best);
            var cl2 = Pick(e.Kind == EBroke ? "br" : "bd", OrderRegion(cul2));
            if (spk2 == null || cl2 == null || IsSpeaking(spk2)) return;
            if (Play(m, cl2, GVoice, HeadOf(spk2), OrderMin, OrderMax, true, spk2, null, e.Kind == EBroke ? "zlamanie" : "sztandar wroga") == null) return;
            if (e.Kind == EBroke) _cBreak++; else _cBannerDown++;
        }

        // ============================================================ przemowy przed bitwa

        private static void DeploymentDone(Mission m)
        {
            if (_speechAt < 0f) _speechAt = _now + 3f;
        }

        private static void Speeches(Mission m, Settings c)
        {
            if (m.MissionEnded) { _speechAt = -1f; _speech2At = -1f; return; }
            if (_speechAt < 0f && !_speechDone && m.IsDeploymentFinished) _speechAt = _now + 3f;   // bitwa bez zdarzenia OnDeploymentFinished
            if (!_speechDone && _speechAt >= 0f && _now >= _speechAt)
            {
                _speechDone = true;
                _speechAt = -1f;
                if (c.BattleHornsCulture) WatchHorn(m);
                if (!c.BattleHouseSpeeches) return;
                var mine = m.PlayerTeam;
                var foe = m.PlayerEnemyTeam;
                Snd first = null;
                if (MissionTeams.Valid(mine)) first = Speech(m, mine, false);
                if (MissionTeams.Valid(foe))
                {
                    var spk = SpeakerForSpeech(foe, true);
                    if (spk != null)
                    {
                        _speech2Spk = spk; _speech2Team = foe;
                        _speech2At = first != null ? _now + first.C.Dur + (first.Then != null ? first.Then.Dur + 0.3f : 0f) + 1.5f : _now + 0.5f;
                    }
                }
            }
            if (_speech2At >= 0f && _now >= _speech2At)
            {
                _speech2At = -1f;
                if (c.BattleHouseSpeeches && _speech2Spk != null && CanSpeak(_speech2Spk))
                {
                    if (LisDist(HeadOf(_speech2Spk)) <= SpeechEnemyListener) Speech(m, _speech2Team, true, _speech2Spk);
                    else _cSpSkip++;
                }
                _speech2Spk = null; _speech2Team = null;
            }
        }

        private static Agent SpeakerForSpeech(Team t, bool general)
        {
            try
            {
                var act = t.ActiveAgents;
                if (act == null || act.Count < MinMenSpeech) return null;
                if (general && CanSpeak(t.GeneralAgent)) return t.GeneralAgent;
                Agent best = null;
                int bl = -1;
                for (int i = 0; i < act.Count; i++)
                {
                    var a = act[i];
                    if (a == null || !a.IsHero || !CanSpeak(a)) continue;
                    int l = 0;
                    try { l = a.Character.GetSkillValue(DefaultSkills.Leadership); } catch { }
                    if (l > bl) { bl = l; best = a; }
                }
                return best;
            }
            catch (Exception e) { RoutHold.Stumble(RoutHold.SiteSound, "BattleSound.Mowca przemowy", e); return null; }
        }

        private static Snd Speech(Mission m, Team t, bool enemy, Agent spk = null)
        {
            if (spk == null) spk = SpeakerForSpeech(t, false);
            if (spk == null) { _cSpSkip++; return null; }
            var f = spk.Formation;
            if (f != null && EnemyDist(f) < SpeechEnemyFar) { _cSpSkip++; return null; }
            string house = HouseOf(spk);
            string cul = CulOf(spk.Character);
            Clip sp = null, then = null;
            bool own = false;
            if (house == "goldencompany") { sp = Pick("sp", "goldencompany"); then = Pick("mo", "goldencompany"); }
            else if (house != null && Has("sp", house)) { sp = Pick("sp", house); own = true; }
            if (sp == null)
            {
                sp = Pick("sp", SpeechRegion(cul));
                then = Pick("mo", house) ?? Pick("mo", MottoRegion(cul));
            }
            if (sp == null) { _cSpSkip++; return null; }
            var s = Play(m, sp, GVoice, HeadOf(spk), SpeechMin, SpeechMax, true, spk, null, "przemowa");
            if (s == null) { _cSpSkip++; return null; }
            if (own) _cSpHouse++;
            else { _cSpRegion++; if (then != null) { s.Then = then; s.ThenMin = MottoMin; s.ThenMax = SpeechMax; } }
            if (_spSample == null) _spSample = (enemy ? "wrog " : "nasi ") + sp.Id + (s.Then != null ? "+" + s.Then.Id : "") + " (" + (house ?? "bez rodu") + ", " + (cul ?? "?") + ")";
            return s;
        }

        private sealed class ThenEv { internal Clip C; internal Agent Spk; internal float At, Min, Max; }
        private static readonly List<ThenEv> _thenQ = new List<ThenEv>();

        private static void QueueThen(Snd s)
        {
            if (_thenQ.Count < 8) _thenQ.Add(new ThenEv { C = s.Then, Spk = s.Follow, At = _now + 0.3f, Min = s.ThenMin, Max = s.ThenMax });
        }

        private static void Thens(Mission m)
        {
            for (int i = _thenQ.Count - 1; i >= 0; i--)
            {
                var t = _thenQ[i];
                if (_now < t.At) continue;
                _thenQ.RemoveAt(i);
                if (t.Spk == null || !t.Spk.IsActive()) continue;
                if (Play(m, t.C, GVoice, HeadOf(t.Spk), t.Min, t.Max, true, t.Spk, null, "motto przemowy") != null) _cSpMotto++;
            }
        }

        /// <summary>Nocna Straz: dwa dzwieki rogu przeciw dzikim, trzy przeciw Innym (kanon) - raz na bitwe, przy dowodcy Strazy.</summary>
        private static void WatchHorn(Mission m)
        {
            if (_nwDone) return;
            _nwDone = true;
            var teams = m.Teams;
            for (int i = 0; teams != null && i < teams.Count; i++)
            {
                var t = teams[i];
                BasicCharacterObject ch;
                if (TeamCul(t, out ch) != "nightswatch") continue;
                string foe = null;
                for (int j = 0; j < teams.Count; j++)
                {
                    if (!MissionTeams.Enemies(teams[j], t)) continue;
                    BasicCharacterObject ch2;
                    string fc = TeamCul(teams[j], out ch2);
                    bool undead = false;
                    try { undead = Undead.Character(ch2); } catch { }
                    if (fc == "whitewalker" || undead) { foe = "three"; break; }
                    if (Wild(fc)) foe = "two";
                }
                if (foe == null) continue;
                var hc = Pick("hr", "nightswatch_" + foe);
                var a = t.GeneralAgent ?? t.Leader;
                Vec3 pos = a != null && a.IsActive() ? HeadOf(a) : _lis;
                if (hc != null && Play(m, hc, GHorn, pos, HornMin, HornMax, true, null, null, "rog Strazy") != null) { _cHornNw++; TS(t).AnyHorn.Hit(); }
                return;
            }
        }

        private static void AppendOrders(StringBuilder sb, Settings c)
        {
            int horns = 0, orders = 0;
            for (int i = 0; i < HKinds; i++) horns += _cHorn[i];
            for (int i = 0; i < OKinds; i++) orders += _cOrder[i];
            sb.Append(" | rogi ").Append(c != null && c.BattleHornsCulture ? "TAK" : "NIE").Append(' ').Append(horns + _cHornNw).Append(" (marsz ").Append(_cHorn[HMarch])
              .Append(", szarza ").Append(_cHorn[HCharge]).Append(", odwrot ").Append(_cHorn[HRetreat]).Append(", zbiorka ").Append(_cHorn[HRally])
              .Append(", Straz ").Append(_cHornNw).Append("; ustapione IB ").Append(_cHornIb).Append(')');
            sb.Append(" | rozkazy ").Append(c != null && c.BattleCaptainOrders ? "TAK" : "NIE").Append(' ').Append(orders).Append(" (");
            for (int i = 0; i < OKinds; i++) sb.Append(i == 0 ? "" : ", ").Append(OName[i]).Append(' ').Append(_cOrder[i]);
            sb.Append("; ustapione BA ").Append(_cOrderBa).Append(')');
            sb.Append(" | motta przy szarzy ").Append(c != null && c.BattleChargeMottos ? "TAK" : "NIE").Append(' ').Append(_cMotto);
            sb.Append(" | przemowy ").Append(c != null && c.BattleHouseSpeeches ? "TAK" : "NIE").Append(' ').Append(_cSpHouse + _cSpRegion).Append(" (rodu ").Append(_cSpHouse)
              .Append(", regionu ").Append(_cSpRegion).Append(" + motto ").Append(_cSpMotto).Append(", pominiete ").Append(_cSpSkip)
              .Append(_spSample != null ? "; probka " + _spSample : "").Append(')');
            sb.Append(" | okrzyki choragwi: zlamanie ").Append(_cBreak).Append(", sztandar wroga ").Append(_cBannerDown).Append(", nasz sztandar ").Append(_cOurBanner);
        }
    }
}
