using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 333 (B) "Szyk stoi albo idzie - bez wahadla przed kontaktem" (06.11, Jeff po bitwie: "formacje dziwnie sie zachowuja, ida do przodu i do tylu, do przodu i do tylu
    /// zamiast stac w formacji; dopiero jak wrog jest blizej, jakby wychodza z trybu ping-pong i zaczynaja sie normalnie zachowywac"). Klucz FormationsSteadyBeforeContact.
    /// Przyczyna (rozpoznanie 333, dekompilacja dek333): przed zwarciem RBM (Utilities.HasBattleBeenJoined - 75 m, mediany + pol glebokosci obu szykow) szyki ida
    /// zachowaniami "fazy podejscia", a kazde liczy rozkaz Move od nowa co 0.5 s (FormationAI.TickOccasionally -> BehaviorComponent.TickOccasionally ->
    /// Formation.SetMovementOrder) bez histerezy kierunku:
    ///  - BehaviorScreenedSkirmish (lucznicy, waga 1 w taktykach RBM): postfiks RBM CalculateCurrentOrder stawia punkt = mediana glownej piechoty - Direction piechoty x
    ///    (pol glebokosci obu + 5 m), prefiks TickOccasionally co tick SetMovementOrder - lucznicy chodza za kazdym krokiem i obrotem piechoty (wahadlo piechoty x 1, obrot
    ///    Direction przerzuca punkt przod / tyl);
    ///  - BehaviorHoldHighGround (glowna piechota obroncy): vanilla - wzgorze HighGroundCloseToForeseenBattleGround (przesuwa sie z przewidywanym polem bitwy), postfiks RBM -
    ///    wrog w 200 m: punkt z pamieci positionsStorage (zapisany, gdy wrog byl dalej: srednia szyku + Direction x 10 m), wrog dalej: znowu punkt vanilla; przejscie przez
    ///    200 m w obie strony = skok punktu tam i z powrotem;
    ///  - BehaviorCautiousAdvance / Advance / Regroup - kroki RBM +5 / +10 / -10 m i medianowe punkty (opis 333).
    /// Pozycja szyku idzie tylko przez rozkaz ruchu: Formation.Tick bierze _movementOrder.CreateNewOrderWorldPositionMT (Move = staly punkt _position) -> SetPositioning;
    /// RBM SetPositioningPatch zmienia tylko odstep (unitSpacing), a Stop nie ma punktu (szyk zostaje na ostatnim). Jedna droga = prefiks Formation.SetMovementOrder.
    ///
    /// ZASADA (333b, jedna na wszystkie zachowania, bez dotykania wag RBM): prefiks Formation.SetMovementOrder (Priority.Last, po prefiksie com.rbmai), tylko rozkaz Move,
    /// tylko szyk AI (nie szyk gracza - HorsePursuit.PlayerForm), pieszy (piechota albo lucznicy), od IntactMin ludzi, bitwa polowa po rozstawieniu, glowny watek,
    /// i tylko PRZED KONTAKTEM (najblizszy pieszy szyk wroga - bez pieszego dowolny - dalej niz 75 m miara RBM; wyjscie z kontaktu > 90 m). Szyk ma PUNKT PRZYJETY P
    /// (ostatni przepuszczony rozkaz). Nowy punkt T:
    ///  1. pierwszy rozkaz przed kontaktem - przyjety (powrot obroncy na wzgorze, lucznicy za piechote, pierwszy marsz - w dowolna strone);
    ///  2. MARTWA STREFA: |T - P| < max(12 m, glebokosc szyku) - zostaje P (kroki RBM +5 / +10 / -10 m, drganie mediany piechoty pod punktem lucznikow S4);
    ///  3. ZAPADKA KIERUNKU: skladowa (T - P) na osi do wroga przeciwna do ostatniego przyjetego ruchu naprzod / wstecz (ponad pol martwej strefy) - zostaje P, dopoki od
    ///     tamtego ruchu nie minie okno 30 s; kazdy przepuszczony nawrot podwaja okno tego szyku (30 / 60 / 120 / 240 s) - wahadlo z konstrukcji gasnie;
    ///  4. TRZYMANIE: szyk stoi na P (mediana w martwej strefie od P) - nowy punkt najwczesniej 10 s po przyjeciu P (bez dreptania co 0.5 s);
    ///  5. reszta (ruch w tym samym kierunku albo w bok, szyk jeszcze w drodze) - przyjety.
    /// "Zostaje P" = rozkaz zamieniony na Move w punkt P (ta sama WorldPosition - gra uznaje rozkaz za praktycznie ten sam i nie przestawia szyku).
    /// KONTAKT (recenzja 333 W1): takze dowolny szyk wroga (konnica) w 75 m miara RBM albo konnica wroga w 60 m, albo w 120 m i zbliza sie >= 3 m/s (szarza) -
    /// wtedy przez 4 s od ostatniego takiego pomiaru wszystko jak w grze (lucznicy uciekaja przed szarza, piechota sie ustawia); P i zapadka zapomniane.
    /// Szyki druzyny gracza-wodza (takze oddane AI przez F6) - poza 333. Odwrot (Retreat), szarza, Advance / FallBack, Stop - bez zmian.
    /// POMIAR (linia, takze przy kluczu NIE - do porownania): co 0.5 s kazdy szyk AI jak wyzej przed kontaktem - przesuniecie mediany rzutowane na os do wroga,
    /// filtr zygzak z progiem max(3 m, pol glebokosci): nawrot = zmiana kierunku po odcinku >= progu; wynik na szyko-minute przed kontaktem i wedlug zachowania.
    /// Koszt: prefiks raz na rozkaz (co 0.5 s na szyk) - petla po szykach wroga (kilkanascie), pomiar co 0.5 s po szykach; bez zapytan o ludzi.
    /// Potkniecia licza sie (Stumbles), bez wylacznika.
    /// DIAGNOSTYKA 333d (runda 2: nawroty 0.97/min mimo 106 zatrzymanych rozkazow wstecz - ruch ucieka inna droga): linia "AI bitwy (333) nawrot" (pierwsze
    /// MaxRevLines na misje) - szyk, strona, zachowanie, odcinek i powrot mediany oraz srodka ciezkosci (srednia pozycji ludzi, CachedAveragePosition - ten sam
    /// filtr), czy P zmienil sie od ekstremum i ktora regula go przyjela, ostatni cel gry wzgledem P, rozkaz ostatni / biezacy, s od wyjscia z kontaktu, zmiana
    /// kierunku szyku, glebokosci i szerokosci od ekstremum, FacingOrder / ArrangementOrder; "droga" = pierwsza pasujaca przyczyna (artefakt mediany, nowy P,
    /// rozkaz nie-Move, po kontakcie, przestrzelenie P, obrot / przestawienie szyku, P bez zmian). Linie "AI bitwy (333) rozkaz przed kontaktem" (zmiana rozkazu na
    /// nie-Move) i "AI bitwy (333) ustawienie przed kontaktem" (FacingOrder / ArrangementOrder na poczatku i przy zmianie) - po MaxSetLines na misje.
    /// Miara wahadla bez zmian (mediana) - srodek ciezkosci liczony obok, do porownania. Zachowanie latki bez zmian.
    /// 333e (wyniki 333d, bieg at-333-D3: 62 z 86 nawrotow mediany to skok mediany - CachedMedianPosition = czlowiek najblizej sredniej, przeskakuje miedzy ludzmi
    /// szerokiego Loose; srodek ciezkosci stoi; prawdziwe nawroty 24 - nowy P regula 5 "w bok" ze schodkiem wstecz 9, regula Z 6, drgania przy dojsciu do P 7):
    ///  - latka liczy pozycje szyku ze srodka ciezkosci (CachedAveragePosition; bez niego mediana) - regula 1 (kierunek) i 4 (trzymanie);
    ///  - zapadka: przeciwna skladowa na osi do wroga juz od DeadMin (3 m), nie od pol martwej strefy - w oknie punkt OBCIETY do osi P (zostaje sam ruch w bok,
    ///    gdy >= martwej strefy i na siatce nawigacji; inaczej zostaje P); po oknie nawrot tylko >= 2 martwe strefy na osi (mniejszy - obciety jak w oknie);
    ///  - trzymanie liczone takze od dojscia do P (srodek ciezkosci w martwej strefie), nie tylko od przyjecia P.
    /// Wyjatki kontaktu, regula 1 (powrot obroncy na wzgorze, lucznicy za piechote) i miara bez zmian; w linii: nawroty razem ze srodkiem ciezkosci (scisle),
    /// punkty obciete do ruchu w bok, nawroty po oknie za male.
    /// 333f (bieg R3o1 z 333e: mediana 0.50/min, w tyl 0.28/min, srodek ciezkosci 0.31 / 0.22 - drogi: nowy P regula B 4, przestrzelenie P 4, regula Z 2, maly
    /// szyk lucznikow regula 5 "w bok" 3, BehaviorStop artefakt mediany 3):
    ///  - zapadka i obciecie liczone od miejsca, gdzie szyk JEST (Formation.CurrentPosition - srodek ciezkosci w ukladzie punktu rozkazu), nie od P:
    ///    cel za szykiem wzgledem kierunku ruchu (kazda ilosc) obciety do linii szyku (ruch w bok), nawrot po oknie tylko >= 2 martwe strefy OD SZYKU;
    ///  - kierunek ruchu (Dir) od DeadMin (3 m) celu wzgledem szyku, nie od pol martwej strefy;
    ///  - szyk minal P wzdluz kierunku o > OverMin (2 m) - P przesuniety do linii szyku (bez powrotu do minietego punktu);
    ///  - kontakt samej konnicy (4 s) kasuje P, ale nie kierunek zapadki (pierwszy rozkaz po nim przeciwny - obciety do linii szyku).
    /// Wyjatki kontaktu, regula 1 (powrot obroncy na wzgorze, lucznicy za piechote) i miara bez zmian.
    /// </summary>
    internal static class Pendulum333
    {
        internal const float ContactIn = 75f, ContactOut = 90f, SampleEvery = 0.5f, DeadMin = 3f, RefreshEvery = 0.5f;
        internal const float CavClose = 60f, CavNear = 120f, CavClosing = 3f, CavKeep = 4f;
        // 333b: martwa strefa rozkazu, zapadka kierunku (okno podwajane po kazdym nawrocie), trzymanie punktu
        internal const float DeadMove = 12f, Ratchet = 30f, RatchetMax = 240f, HoldMin = 10f;
        internal const float OverMin = 2f;   // 333f: szyk minal P o wiecej - P przesuniety do linii szyku (powrot ponizej progu pomiaru 3 m)
        private const int MaxBeh = 8, MaxRevLines = 30, MaxSetLines = 16, MaxWays = 10;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class PF
        {
            public Formation F;
            public bool Contact, FootContact;
            public float CavGap = float.MaxValue, CavAt = -1000f, CavUntil = -1000f;
            public float Gap = float.MaxValue, GapAt = -1000f;
            public Vec2 Axis = Vec2.Invalid;
            // 333b: punkt przyjety, zapadka
            public Vec2 P = Vec2.Invalid;
            public WorldPosition PW;
            public float PAt = -1000f, DirAt = -1000f, ArrAt = -1000f;   // 333e: ArrAt - dojscie do P (srodek ciezkosci w martwej strefie)
            public sbyte Dir;
            public int Revs;
            // pomiar
            public Vec2 Last = Vec2.Invalid;
            public float X, Lo, Hi, Ext;
            public sbyte Trend;
            // 333d: diagnostyka
            public float OutAt = -1000f;
            public int AccN;
            public char AccRule = '-';
            public sbyte AccSg;
            public float AccAt = -1000f;
            public Vec2 T = Vec2.Invalid;
            public float TAt = -1000f;
            public MovementOrder.MovementOrderEnum Ord;
            public float OrdAt = -1000f;
            public int Fac = -1, Arr = -1;
            // srodek ciezkosci (srednia pozycji ludzi) - ten sam zygzak co mediana
            public Vec2 LastA = Vec2.Invalid;
            public float XA, LoA, HiA, ExtA;
            public sbyte TrendA;
            // stan mediany w ekstremum i w poprzednim ekstremum
            public float XALo, XAHi, LoAt, HiAt;
            public float ExtAt, ExtPrev, ExtPrevAt, XAExt, XAPrevExt, DepthExt, WidthExt;
            public Vec2 DirExt = Vec2.Invalid, PExt = Vec2.Invalid, CExt = Vec2.Invalid;
            public int AccNExt, FacExt = -1, ArrExt = -1;
        }

        private static bool _live, _key, _tried;
        private static int _mainThread = -1;
        private static WeakReference _m;
        private static float _acc;
        private static readonly Dictionary<Formation, PF> Forms = new Dictionary<Formation, PF>();
        private static readonly Dictionary<string, int> RevBy = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly HashSet<Formation> HeldForms = new HashSet<Formation>();
        // liczniki misji (nazwy segmentu jak w 333: wstecz zatrzymane = zapadka na ruchu wstecz, wstecz dozwolone S4 / po zmianie zachowania = przyjete ruchy wstecz
        // lucznikow S4 / innych, naprzod wstrzymane po cofnieciu = zapadka na ruchu naprzod)
        private static int _seen, _backHeld, _backS4, _backBeh, _fwdHeld, _rev, _revBack, _forms, _contactIn, _contactCav;
        private static int _first, _dead, _hold, _revOk, _accFwd, _accSide;
        private static float _preSec;
        // 333d: diagnostyka
        private static int _revLog, _ordLog, _setLog, _revA, _revABack, _revSmall, _ordChg, _facChg, _arrChg;
        // 333e: nawroty mediany, w ktorych srodek ciezkosci idzie i wraca razem (scisle), punkty obciete do ruchu w bok, nawroty po oknie za male
        private static int _revBoth, _revBothBack, _clip, _zSmall;
        // 333f: P przesuniete po minieciu, cele obciete o mniej niz DeadMin za szykiem
        private static int _over, _clipSmall;
        private static readonly Dictionary<string, int[]> RevWay = new Dictionary<string, int[]>(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> NonMoveBy = new Dictionary<string, int>(StringComparer.Ordinal);
        private static int _stum, _stumPatch;   // recenzja 333 (7): potkniecie latki (przed pierwszym Begin) nie ginie w Clear
        internal static int Stumbles { get { return _stum + _stumPatch; } }
        private static string _patch = "nie wpieta";

        // ------------------------------------------------------------ latka (Doctrine.ApplyMissionPatches - pierwsza bitwa, raz na proces)

        internal static string ApplyPatch(Harmony h)
        {
            if (_tried) return _patch;
            _tried = true;
            try
            {
                var target = AccessTools.DeclaredMethod(typeof(Formation), nameof(Formation.SetMovementOrder), new[] { typeof(MovementOrder) });
                if (target == null) { _patch = "NIE (brak Formation.SetMovementOrder)"; return _patch; }
                h.Patch(target, prefix: new HarmonyMethod(typeof(Pendulum333), nameof(Pre)) { priority = Priority.Last, after = new[] { "com.rbmai" } });
                _patch = "TAK";
            }
            catch (Exception e)
            {
                _patch = "NIE (" + e.GetType().Name + ")";
                _stumPatch++;
                RoutHold.Stumble(RoutHold.SiteDoctrine, "Pendulum333.Latka", e);
            }
            return _patch;
        }

        // ------------------------------------------------------------ misja (HorsePursuit.Begin / Tick / End - glowny watek)

        internal static void Begin(Mission m, bool field)
        {
            Clear();
            _live = field && m != null;
            _m = m != null ? new WeakReference(m) : null;
            _mainThread = Thread.CurrentThread.ManagedThreadId;
            var c = Settings.Current;
            _key = c != null && c.FormationsSteadyBeforeContact;
        }

        internal static void End()
        {
            _live = false;
            Clear();
            _m = null;
        }

        private static void Clear()
        {
            Forms.Clear(); RevBy.Clear(); HeldForms.Clear();
            _acc = 0f;
            _seen = _backHeld = _backS4 = _backBeh = _fwdHeld = _rev = _revBack = _forms = _contactIn = _contactCav = 0;
            _first = _dead = _hold = _revOk = _accFwd = _accSide = 0;
            _preSec = 0f;
            _stum = 0;
            _revLog = _ordLog = _setLog = _revA = _revABack = _revSmall = _ordChg = _facChg = _arrChg = 0;
            _revBoth = _revBothBack = _clip = _zSmall = 0;
            _over = _clipSmall = 0;
            RevWay.Clear(); NonMoveBy.Clear();
        }

        private static bool Here(Mission m)
        {
            var w = _m;
            return _live && m != null && w != null && ReferenceEquals(w.Target, m);
        }

        /// <summary>Szyk, ktorego to dotyczy: AI, nie gracza, pieszy, od IntactMin ludzi, poza rozstawieniem.</summary>
        private static bool Eligible(Formation f)
        {
            if (f == null || f.CountOfUnits < HorsePursuit.IntactMin || !f.IsAIControlled || f.IsDeployment) return false;
            if (!MissionTeams.Valid(f.Team) || HorsePursuit.PlayerForm(f)) return false;
            var q = f.QuerySystem;
            if (q == null || q.IsCavalryFormation || q.IsRangedCavalryFormation) return false;
            return q.IsInfantryFormation || q.IsRangedFormation;
        }

        private static PF Get(Formation f)
        {
            PF pf;
            if (!Forms.TryGetValue(f, out pf)) { pf = new PF { F = f }; Forms[f] = pf; _forms++; }
            return pf;
        }

        /// <summary>Odleglosc miara RBM (mediany + pol glebokosci obu) do najblizszego pieszego szyku wroga od IntactMin ludzi (bez pieszego - jakiegokolwiek), os do niego,
        /// stan kontaktu z histereza. Najwyzej co RefreshEvery s.</summary>
        private static void Refresh(PF pf, float now)
        {
            if (now - pf.GapAt < RefreshEvery) return;
            pf.GapAt = now;
            var f = pf.F;
            var own = f.Team;
            var m = Mission.Current;
            Vec2 c = f.CachedMedianPosition.AsVec2;
            float best = float.MaxValue, bestAny = float.MaxValue, bestCav = float.MaxValue;
            Vec2 ax = Vec2.Invalid, axAny = Vec2.Invalid;
            var teams = m != null ? m.Teams : null;
            int nt = teams != null ? teams.Count : 0;
            for (int i = 0; i < nt; i++)
            {
                Team t;
                try { t = teams[i]; } catch { continue; }
                if (!MissionTeams.Valid(t) || own == null || !t.IsEnemyOf(own)) continue;
                var fl = t.FormationsIncludingSpecialAndEmpty;
                int nf = fl != null ? fl.Count : 0;
                for (int j = 0; j < nf; j++)
                {
                    var e = fl[j];
                    if (e == null || e.CountOfUnits < HorsePursuit.IntactMin) continue;
                    var eq = e.QuerySystem;
                    if (eq == null) continue;
                    Vec2 ec = e.CachedMedianPosition.AsVec2;
                    if (!ec.IsValid) continue;
                    float d = c.Distance(ec) + 0.5f * (f.Depth + e.Depth);
                    bool foot = !eq.IsCavalryFormation && !eq.IsRangedCavalryFormation;
                    if (foot && d < best) { best = d; ax = ec - c; }
                    if (d < bestAny) { bestAny = d; axAny = ec - c; }
                    if (!foot && d < bestCav) bestCav = d;
                }
            }
            float near = bestAny;   // kontakt: pieszy wrog albo dowolny szyk (konnica) miara RBM - recenzja 333 W1
            if (best == float.MaxValue) { best = bestAny; ax = axAny; }
            pf.Gap = best;
            if (ax.IsValid && ax.Length > 0.1f) pf.Axis = ax.Normalized();
            bool was = pf.Contact;
            if (near <= ContactIn) pf.FootContact = true;
            else if (near > ContactOut) pf.FootContact = false;
            // konnica wroga: bardzo blisko albo w 120 m i szarzuje (zbliza sie >= 3 m/s) - kontakt na CavKeep s od ostatniego takiego pomiaru
            bool cav = false;
            if (bestCav <= CavClose) cav = true;
            else if (bestCav <= CavNear && pf.CavGap != float.MaxValue && now - pf.CavAt > 0.05f && now - pf.CavAt < 3f
                     && (pf.CavGap - bestCav) / (now - pf.CavAt) >= CavClosing) cav = true;
            pf.CavGap = bestCav;
            pf.CavAt = now;
            if (cav) pf.CavUntil = now + CavKeep;
            pf.Contact = pf.FootContact || now < pf.CavUntil;
            if (pf.Contact && !was) { _contactIn++; if (!pf.FootContact) _contactCav++; }
            if (!pf.Contact && was) pf.OutAt = now;   // 333d
        }

        // ------------------------------------------------------------ prefiks Formation.SetMovementOrder

        private static void Pre(Formation __instance, ref MovementOrder input)
        {
            if (!_live) return;
            try
            {
                if (Thread.CurrentThread.ManagedThreadId != _mainThread) return;
                var m = Mission.Current;
                if (!Here(m) || !m.IsDeploymentFinished) return;
                var f = __instance;
                if (!Eligible(f)) return;
                float now = m.CurrentTime;
                var pf = Get(f);
                Refresh(pf, now);
                // 333d: kazdy rozkaz zapamietany (OrderEnum, czas); nie-Move przed kontaktem - licznik i linia przy zmianie
                var oe = input.OrderEnum;
                var prevOrd = pf.Ord;
                pf.Ord = oe;
                pf.OrdAt = now;
                bool pre = !pf.Contact && pf.Gap != float.MaxValue && pf.Axis.IsValid;
                if (oe != MovementOrder.MovementOrderEnum.Move)
                {
                    if (pre) NonMove(pf, oe, prevOrd, now);
                    return;
                }
                if (!_key) return;
                if (!pre) { Forget(pf, pf.Contact && !pf.FootContact); return; }
                Vec2 q = Cur(f);   // 333f: pozycja szyku w ukladzie punktu rozkazu (srodek ciezkosci przesuniety o srednie miejsce w szyku - Formation.CurrentPosition)
                WorldPosition tw = input.CreateNewOrderWorldPositionMT(f, WorldPosition.WorldPositionEnforcedCache.None);
                Vec2 tp = tw.AsVec2;
                if (!q.IsValid || !tw.IsValid || !tp.IsValid) return;
                _seen++;
                pf.T = tp;   // 333d: ostatni cel gry
                pf.TAt = now;
                float dz = Math.Max(DeadMove, f.Depth);
                Type beh = null;
                try { var ai = f.AI; beh = ai != null && ai.ActiveBehavior != null ? ai.ActiveBehavior.GetType() : null; } catch { beh = null; }
                // 333f: kierunek celu liczony od miejsca, w ktorym szyk JEST (q), nie od P - 333e liczyl zapadke i obciecie od P, wiec gdy P zostal za szykiem
                // (szyk doszedl dalej albo P obciety "w bok" od starego P), punkt "w bok" albo "nawrot za maly" i tak cofal szyk (bieg R3o1: nowy P regula B 4,
                // regula Z 2, przestrzelenie P 4)
                float u = (tp - q).DotProduct(pf.Axis);
                sbyte sg = SignD(u);
                // 1. pierwszy rozkaz przed kontaktem - przyjety (powrot obroncy na wzgorze, lucznicy za piechote); po kontakcie samej konnicy (Dir zostal) - cel
                // przeciwny do ruchu sprzed kontaktu obciety do linii szyku (bez nawrotu po 4 s szarzy)
                if (!pf.P.IsValid)
                {
                    _first++;
                    if (pf.Dir != 0 && u * pf.Dir < 0f && !(u * pf.Dir < -DeadMin && now - pf.DirAt >= Win(pf) && Math.Abs(u) >= 2f * dz)
                        && Side(f, pf, tw, tp - pf.Axis * u, now, beh, ref input))
                    {
                        pf.AccRule = '1';
                        return;
                    }
                    Accept(pf, tw, tp, sg, now, beh, '1');
                    return;
                }
                // 0. 333f: szyk minal P wzdluz kierunku ruchu (o > OverMin) - P przesuniety na os do linii szyku: bez powrotu do punktu juz minietego
                if (pf.Dir != 0)
                {
                    float o = (q - pf.P).DotProduct(pf.Axis);
                    if (o * pf.Dir > OverMin) Over(pf, o, now);
                }
                char rule = '5';
                Vec2 tt = tp;
                bool clip = false;
                // 3. zapadka kierunku (okno 30 s, podwajane po kazdym przepuszczonym nawrocie) - od pozycji szyku: kazda skladowa celu na osi za szykiem (wzgledem
                // kierunku ruchu) obcieta do linii szyku (zostaje sam ruch w bok); nawrot tylko po oknie i >= 2 martwe strefy od szyku
                if (pf.Dir != 0 && u * pf.Dir < 0f)
                {
                    bool big = u * pf.Dir < -DeadMin;
                    bool inWin = now - pf.DirAt < Win(pf);
                    if (big && !inWin && Math.Abs(u) >= 2f * dz)
                    {
                        pf.Revs++;
                        _revOk++;
                        rule = 'Z';   // zapadka przepuscila nawrot po oknie
                    }
                    else
                    {
                        if (big)
                        {
                            if (!inWin) _zSmall++;
                            if (u < 0f) _backHeld++; else _fwdHeld++;
                        }
                        else _clipSmall++;
                        tt = tp - pf.Axis * u;
                        clip = true;
                    }
                }
                // 2. martwa strefa (cel po obcieciu)
                if (tt.Distance(pf.P) < dz) { Keep(f, pf, ref input); if (!clip) _dead++; return; }
                // 4. trzymanie: szyk stoi na P - nowy punkt najwczesniej HoldMin s po przyjeciu P i od dojscia do P (333e)
                if (rule != 'Z' && q.Distance(pf.P) < dz)
                {
                    if (pf.ArrAt < pf.PAt) pf.ArrAt = now;
                    if (now - Math.Max(pf.PAt, pf.ArrAt) < HoldMin)
                    {
                        Keep(f, pf, ref input);
                        _hold++;
                        return;
                    }
                }
                // 5. przyjety (obciety - ruch w bok bez zmiany kierunku zapadki; poza siatka nawigacji zostaje P)
                if (clip)
                {
                    if (!Side(f, pf, tw, tt, now, beh, ref input)) Keep(f, pf, ref input);
                    return;
                }
                Accept(pf, tw, tp, sg, now, beh, rule);
            }
            catch (Exception e)
            {
                _stum++;
                RoutHold.Stumble(RoutHold.SiteDoctrine, "Pendulum333.Rozkaz", e);
            }
        }

        /// <summary>333f: kierunek celu wzgledem szyku - od DeadMin (3 m; 333e: pol martwej strefy - cel 3-6 m za szykiem "w bok" nie ustawial kierunku).</summary>
        private static sbyte SignD(float u)
        {
            return u > DeadMin ? (sbyte)1 : u < -DeadMin ? (sbyte)-1 : (sbyte)0;
        }

        /// <summary>Okno zapadki szyku: 30 / 60 / 120 / 240 s wedlug przepuszczonych nawrotow.</summary>
        private static float Win(PF pf)
        {
            return Math.Min(RatchetMax, Ratchet * (float)Math.Pow(2.0, Math.Min(pf.Revs, 3)));
        }

        /// <summary>333f: pozycja szyku w ukladzie punktu rozkazu - Formation.CurrentPosition (srodek ciezkosci przesuniety o srednie miejsce ludzi w szyku;
        /// Move w P stawia przod szyku w P, srodek ciezkosci stoi pol glebokosci za nim); bez niego srodek ciezkosci, potem mediana.</summary>
        private static Vec2 Cur(Formation f)
        {
            Vec2 v;
            try { v = f.CurrentPosition; } catch { v = Vec2.Invalid; }
            return v.IsValid ? v : Pos(f);
        }

        /// <summary>333f: szyk minal P o o metrow wzdluz osi (w kierunku ruchu) - P przesuniety o o na osi (bok bez zmian), gdy na siatce nawigacji; trzymanie od teraz.</summary>
        private static void Over(PF pf, float o, float now)
        {
            Vec2 np = pf.P + pf.Axis * o;
            var w = pf.PW;
            w.SetVec2(np);
            if (!w.IsValid || w.GetNavMesh() == UIntPtr.Zero) return;
            pf.P = np;
            pf.PW = w;
            pf.ArrAt = now;
            pf.AccN++;
            pf.AccRule = 'O';
            pf.AccSg = 0;
            pf.AccAt = now;
            _over++;
        }

        private static void Accept(PF pf, WorldPosition tw, Vec2 tp, sbyte sg, float now, Type beh, char rule)
        {
            pf.AccN++;   // 333d
            pf.AccRule = rule;
            pf.AccSg = sg;
            pf.AccAt = now;
            pf.P = tp;
            pf.PW = tw;
            pf.PAt = now;
            if (sg != 0) { pf.Dir = sg; pf.DirAt = now; }
            if (sg > 0) _accFwd++;
            else if (sg < 0) { if (beh == typeof(BehaviorScreenedSkirmish)) _backS4++; else _backBeh++; }
            else _accSide++;
        }

        /// <summary>Kontakt: punkt przyjety i zapadka zapomniane (po wyjsciu z kontaktu - od nowa; licznik nawrotow okna zostaje). 333f: kontakt samej konnicy
        /// (4 s od szarzy, bez pieszego wroga w 75 m) - kierunek zapadki zostaje (keepDir), znika tylko P.</summary>
        private static void Forget(PF pf, bool keepDir)
        {
            pf.P = Vec2.Invalid;
            if (keepDir) return;
            pf.Dir = 0;
            pf.DirAt = -1000f;
        }

        /// <summary>333e: pozycja szyku dla latki - srodek ciezkosci (srednia pozycji ludzi), bez niego mediana.</summary>
        private static Vec2 Pos(Formation f)
        {
            Vec2 a = f.CachedAveragePosition;
            return a.IsValid ? a : f.CachedMedianPosition.AsVec2;
        }

        /// <summary>333e: punkt obciety do osi P (sam ruch w bok) - przyjety bez zmiany kierunku zapadki, gdy lezy na siatce nawigacji; false - zostaje P.</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]   // 333t2: MovementOrder tylko w misji
        private static bool Side(Formation f, PF pf, WorldPosition tw, Vec2 tc, float now, Type beh, ref MovementOrder input)
        {
            var w = tw;
            w.SetVec2(tc);
            if (!w.IsValid || w.GetNavMesh() == UIntPtr.Zero) return false;
            input = MovementOrder.MovementOrderMove(w);
            HeldForms.Add(f);
            _clip++;
            Accept(pf, w, tc, 0, now, beh, 'B');
            return true;
        }

        /// <summary>Rozkaz zamieniony na Move w punkt przyjety P (ta sama WorldPosition).</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]   // 333t2: MovementOrder tylko w misji (Pre: Here(m))
        private static void Keep(Formation f, PF pf, ref MovementOrder input)
        {
            input = MovementOrder.MovementOrderMove(pf.PW);
            HeldForms.Add(f);
        }

        // ------------------------------------------------------------ pomiar wahadla (co 0.5 s)

        internal static void Tick(Mission m, float now, float dt)
        {
            if (!Here(m)) return;
            _acc += dt;
            if (_acc < SampleEvery) return;
            float step = _acc;
            _acc = 0f;
            try
            {
                if (!m.IsDeploymentFinished) return;
                var teams = m.Teams;
                int nt = teams != null ? teams.Count : 0;
                for (int i = 0; i < nt; i++)
                {
                    Team t;
                    try { t = teams[i]; } catch { continue; }
                    if (!MissionTeams.Valid(t)) continue;
                    var fl = t.FormationsIncludingSpecialAndEmpty;
                    int nf = fl != null ? fl.Count : 0;
                    for (int j = 0; j < nf; j++)
                    {
                        var f = fl[j];
                        if (!Eligible(f)) continue;
                        Sample(Get(f), now, step);
                    }
                }
            }
            catch (Exception e)
            {
                _stum++;
                RoutHold.Stumble(RoutHold.SiteDoctrine, "Pendulum333.Pomiar", e);
            }
        }

        private static void Sample(PF pf, float now, float step)
        {
            Refresh(pf, now);
            var f = pf.F;
            Vec2 c = f.CachedMedianPosition.AsVec2;
            Vec2 a = f.CachedAveragePosition;
            if (pf.Contact || pf.Gap == float.MaxValue || !pf.Axis.IsValid || !c.IsValid || !a.IsValid)
            {
                pf.Last = Vec2.Invalid;
                pf.LastA = Vec2.Invalid;
                pf.Trend = 0;
                pf.TrendA = 0;
                if (pf.Contact) Forget(pf, !pf.FootContact);
                return;
            }
            Poll(pf, now);   // 333d: FacingOrder / ArrangementOrder przed kontaktem
            if (pf.P.IsValid && pf.ArrAt < pf.PAt && Cur(f).Distance(pf.P) < Math.Max(DeadMove, f.Depth)) pf.ArrAt = now;   // 333e: dojscie do P (333f: pozycja szyku)
            _preSec += step;
            if (!pf.Last.IsValid || !pf.LastA.IsValid)
            {
                pf.Last = c;
                pf.LastA = a;
                pf.X = pf.Lo = pf.Hi = pf.Ext = 0f;
                pf.Trend = 0;
                pf.XA = pf.LoA = pf.HiA = pf.ExtA = 0f;
                pf.TrendA = 0;
                pf.XALo = pf.XAHi = 0f;
                pf.LoAt = pf.HiAt = now;
                Snap(pf, c, now);
                pf.ExtPrev = 0f; pf.XAPrevExt = 0f; pf.ExtPrevAt = now;
                return;
            }
            pf.X += (c - pf.Last).DotProduct(pf.Axis);
            pf.Last = c;
            pf.XA += (a - pf.LastA).DotProduct(pf.Axis);
            pf.LastA = a;
            float thr = Math.Max(DeadMin, 0.5f * f.Depth);
            ZigA(pf, thr);
            float x = pf.X;
            if (pf.Trend == 0)
            {
                if (x < pf.Lo) { pf.Lo = x; pf.XALo = pf.XA; pf.LoAt = now; }
                if (x > pf.Hi) { pf.Hi = x; pf.XAHi = pf.XA; pf.HiAt = now; }
                if (x - pf.Lo >= thr) { pf.Trend = 1; pf.ExtPrev = pf.Lo; pf.XAPrevExt = pf.XALo; pf.ExtPrevAt = pf.LoAt; pf.Ext = x; Snap(pf, c, now); }
                else if (pf.Hi - x >= thr) { pf.Trend = -1; pf.ExtPrev = pf.Hi; pf.XAPrevExt = pf.XAHi; pf.ExtPrevAt = pf.HiAt; pf.Ext = x; Snap(pf, c, now); }
                return;
            }
            if (pf.Trend > 0)
            {
                if (x > pf.Ext) { pf.Ext = x; Snap(pf, c, now); }
                else if (pf.Ext - x >= thr) { Reversal(pf, true, x, thr, c, a, now); pf.Trend = -1; Turn(pf, x, c, now); }
            }
            else
            {
                if (x < pf.Ext) { pf.Ext = x; Snap(pf, c, now); }
                else if (x - pf.Ext >= thr) { Reversal(pf, false, x, thr, c, a, now); pf.Trend = 1; Turn(pf, x, c, now); }
            }
        }

        /// <summary>333d: stan szyku w chwili ekstremum mediany (do porownania przy nawrocie).</summary>
        private static void Snap(PF pf, Vec2 c, float now)
        {
            var f = pf.F;
            pf.ExtAt = now;
            pf.XAExt = pf.XA;
            pf.DirExt = f.Direction;
            pf.PExt = pf.P;
            pf.CExt = c;
            pf.DepthExt = f.Depth;
            pf.WidthExt = f.Width;
            pf.AccNExt = pf.AccN;
            pf.FacExt = pf.Fac;
            pf.ArrExt = pf.Arr;
        }

        private static void Turn(PF pf, float x, Vec2 c, float now)
        {
            pf.ExtPrev = pf.Ext;
            pf.XAPrevExt = pf.XAExt;
            pf.ExtPrevAt = pf.ExtAt;
            pf.Ext = x;
            Snap(pf, c, now);
        }

        /// <summary>333d: ten sam zygzak dla srodka ciezkosci (srednia pozycji ludzi) - do porownania z mediana, bez wplywu na miare.</summary>
        private static void ZigA(PF pf, float thr)
        {
            float x = pf.XA;
            if (pf.TrendA == 0)
            {
                if (x < pf.LoA) pf.LoA = x;
                if (x > pf.HiA) pf.HiA = x;
                if (x - pf.LoA >= thr) { pf.TrendA = 1; pf.ExtA = x; }
                else if (pf.HiA - x >= thr) { pf.TrendA = -1; pf.ExtA = x; }
                return;
            }
            if (pf.TrendA > 0)
            {
                if (x > pf.ExtA) pf.ExtA = x;
                else if (pf.ExtA - x >= thr) { _revA++; _revABack++; pf.TrendA = -1; pf.ExtA = x; }
            }
            else
            {
                if (x < pf.ExtA) pf.ExtA = x;
                else if (x - pf.ExtA >= thr) { _revA++; pf.TrendA = 1; pf.ExtA = x; }
            }
        }

        private static string BehName(Formation f)
        {
            try { var ai = f.AI; return ai != null && ai.ActiveBehavior != null ? ai.ActiveBehavior.GetType().Name : "brak"; } catch { return "?"; }
        }

        private static string Who(Formation f)
        {
            var q = f.QuerySystem;
            var t = f.Team;
            return f.FormationIndex + " (" + f.CountOfUnits + " ludzi, " + (q != null && q.IsRangedFormation ? "lucznicy" : "piechota") + ", strona "
                + (t != null ? t.Side.ToString() : "?") + ", " + (t != null && (t.IsPlayerTeam || t.IsPlayerAlly) ? "z graczem" : "wrog gracza") + ")";
        }

        private static string FacName(int v) { return v < 0 ? "-" : ((FacingOrder.FacingOrderEnum)v).ToString(); }
        private static string ArrName(int v) { return v < 0 ? "-" : ((ArrangementOrder.ArrangementOrderEnum)v).ToString(); }
        private static string S1(float v) { return v.ToString("0.0", Inv); }
        private static string F0(float v) { return v.ToString("0", Inv); }

        /// <summary>Kat miedzy kierunkami w stopniach (0-180).</summary>
        private static float Ang(Vec2 a, Vec2 b)
        {
            if (!a.IsValid || !b.IsValid || a.Length < 1e-3f || b.Length < 1e-3f) return 0f;
            double d = Math.Atan2(b.y, b.x) - Math.Atan2(a.y, a.x);
            while (d > Math.PI) d -= 2.0 * Math.PI;
            while (d < -Math.PI) d += 2.0 * Math.PI;
            return (float)(Math.Abs(d) * 180.0 / Math.PI);
        }

        /// <summary>333d: FacingOrder / ArrangementOrder przed kontaktem - poczatek i kazda zmiana (linie najwyzej MaxSetLines na misje).</summary>
        private static void Poll(PF pf, float now)
        {
            var f = pf.F;
            int fac = (int)f.FacingOrder.OrderEnum, arr = (int)f.ArrangementOrder.OrderEnum;
            if (pf.Fac < 0)
            {
                pf.Fac = fac;
                pf.Arr = arr;
                SetLine(pf, now, "poczatek FacingOrder " + FacName(fac) + ", ArrangementOrder " + ArrName(arr));
                return;
            }
            if (fac != pf.Fac) { _facChg++; SetLine(pf, now, "FacingOrder " + FacName(pf.Fac) + " -> " + FacName(fac)); pf.Fac = fac; }
            if (arr != pf.Arr) { _arrChg++; SetLine(pf, now, "ArrangementOrder " + ArrName(pf.Arr) + " -> " + ArrName(arr)); pf.Arr = arr; }
        }

        private static void SetLine(PF pf, float now, string what)
        {
            if (_setLog >= MaxSetLines) return;
            _setLog++;
            var f = pf.F;
            Log.Info("AI bitwy (333) ustawienie przed kontaktem: " + S1(now) + " s | szyk " + Who(f) + " | zachowanie " + BehName(f) + " | " + what
                + " | od wroga " + F0(pf.Gap) + " m, kierunek szyku do osi wroga " + F0(Ang(f.Direction, pf.Axis)) + " st, glebokosc " + S1(f.Depth) + " m, szerokosc "
                + S1(f.Width) + " m.");
        }

        /// <summary>333d: rozkaz inny niz Move przed kontaktem (latka go nie pilnuje) - licznik wedlug rodzaju, linia przy zmianie na nie-Move.</summary>
        private static void NonMove(PF pf, MovementOrder.MovementOrderEnum oe, MovementOrder.MovementOrderEnum prev, float now)
        {
            string k = oe.ToString();
            int n;
            NonMoveBy.TryGetValue(k, out n);
            if (n == 0 && NonMoveBy.Count >= MaxBeh) k = "inne";
            NonMoveBy.TryGetValue(k, out n);
            NonMoveBy[k] = n + 1;
            if (oe == prev) return;
            _ordChg++;
            if (_ordLog >= MaxSetLines) return;
            _ordLog++;
            var f = pf.F;
            Log.Info("AI bitwy (333) rozkaz przed kontaktem: " + S1(now) + " s | szyk " + Who(f) + " | zachowanie " + BehName(f) + " | " + prev + " -> " + oe
                + " | od wroga " + F0(pf.Gap) + " m, P " + (pf.P.IsValid ? "jest" : "brak") + ", s od wyjscia z kontaktu " + (pf.OutAt > -999f ? S1(now - pf.OutAt) : "-") + ".");
        }

        private static void Reversal(PF pf, bool back, float x, float thr, Vec2 c, Vec2 a, float now)
        {
            var f = pf.F;
            _rev++;
            if (back) _revBack++;
            string n = BehName(f);
            int k;
            RevBy.TryGetValue(n, out k);
            if (k == 0 && RevBy.Count >= MaxBeh) n = "inne";
            RevBy.TryGetValue(n, out k);
            RevBy[n] = k + 1;
            // 333d: droga nawrotu
            try
            {
                float legM = pf.Ext - pf.ExtPrev, retM = x - pf.Ext, legA = pf.XAExt - pf.XAPrevExt, retA = pf.XA - pf.XAExt;
                if (Math.Abs(legM) < 2f * thr) _revSmall++;
                if (legA * legM > 0f && Math.Abs(legA) >= 0.5f * thr && retA * retM > 0f && Math.Abs(retA) >= 0.5f * thr) { _revBoth++; if (back) _revBothBack++; }
                bool medArt = Math.Abs(retA) < 0.5f * thr || retA * retM < 0f;   // srodek ciezkosci nie wrocil (albo poszedl dalej)
                int dP = pf.AccN - pf.AccNExt;
                float dDir = Ang(pf.DirExt, f.Direction);
                float dDepth = f.Depth - pf.DepthExt, dWidth = f.Width - pf.WidthExt;
                var cur = f.GetReadonlyMovementOrderReference().OrderEnum;
                Vec2 ax = pf.Axis;
                float offExt = pf.PExt.IsValid && pf.CExt.IsValid ? (pf.CExt - pf.PExt).DotProduct(ax) : 0f;
                string way;
                if (medArt) way = "artefakt mediany";
                else if (dP > 0) way = "nowy P regula " + pf.AccRule;
                else if (cur != MovementOrder.MovementOrderEnum.Move) way = "rozkaz " + cur;
                else if (pf.OrdAt >= pf.ExtPrevAt && pf.Ord != MovementOrder.MovementOrderEnum.Move) way = "rozkaz " + pf.Ord;
                else if (pf.OutAt >= pf.ExtPrevAt) way = "po kontakcie";
                else if (!pf.P.IsValid) way = "bez P";
                else if (pf.PExt.IsValid && offExt * legM > 0f && Math.Abs(offExt) >= 0.5f * thr) way = "przestrzelenie P";
                else if (dDir > 20f || Math.Abs(dDepth) > Math.Max(2f, 0.3f * pf.DepthExt) || Math.Abs(dWidth) > Math.Max(3f, 0.3f * pf.WidthExt)) way = "obrot / przestawienie szyku";
                else way = "P bez zmian";
                int[] w;
                if (!RevWay.TryGetValue(way, out w))
                {
                    if (RevWay.Count >= MaxWays) way = "inne";
                    if (!RevWay.TryGetValue(way, out w)) { w = new int[2]; RevWay[way] = w; }
                }
                w[0]++;
                if (back) w[1]++;
                if (_revLog >= MaxRevLines) return;
                _revLog++;
                var sb = new StringBuilder(640);
                sb.Append("AI bitwy (333) nawrot: ").Append(S1(now)).Append(" s | szyk ").Append(Who(f)).Append(" | zachowanie ").Append(n)
                  .Append(" | ").Append(back ? "w tyl" : "naprzod").Append(", droga ").Append(way)
                  .Append(" | mediana: odcinek ").Append(S1(legM)).Append(" m w ").Append(S1(pf.ExtAt - pf.ExtPrevAt)).Append(" s, powrot ").Append(S1(retM))
                  .Append(" m w ").Append(S1(now - pf.ExtAt)).Append(" s (prog ").Append(S1(thr)).Append(" m)")
                  .Append(" | srodek ciezkosci: odcinek ").Append(S1(legA)).Append(" m, powrot ").Append(S1(retA)).Append(" m, mediana - srodek ").Append(S1(c.Distance(a))).Append(" m")
                  .Append(" | P: ");
                if (pf.P.IsValid)
                    sb.Append("zmian od ekstremum ").Append(dP).Append(", przyjec razem ").Append(pf.AccN).Append(", ostatnia regula ").Append(pf.AccRule)
                      .Append(pf.AccSg > 0 ? " naprzod" : pf.AccSg < 0 ? " wstecz" : " w bok").Append(' ').Append(S1(now - pf.AccAt)).Append(" s temu, mediana - P na osi teraz ")
                      .Append(S1((c - pf.P).DotProduct(ax))).Append(" / w ekstremum ").Append(S1(offExt)).Append(" m");
                else sb.Append("brak");
                sb.Append("; cel gry - P na osi ")
                  .Append(pf.T.IsValid && pf.P.IsValid ? S1((pf.T - pf.P).DotProduct(ax)) : "-").Append(" m (").Append(pf.TAt > -999f ? S1(now - pf.TAt) : "-").Append(" s temu)")
                  .Append(" | rozkaz ostatni ").Append(pf.Ord).Append(' ').Append(pf.OrdAt > -999f ? S1(now - pf.OrdAt) : "-").Append(" s temu, teraz ").Append(cur)
                  .Append(", pozycja rozkazu - mediana na osi ").Append(S1((f.OrderPosition - c).DotProduct(ax))).Append(" m")
                  .Append(" | od wyjscia z kontaktu ").Append(pf.OutAt > -999f ? S1(now - pf.OutAt) : "-").Append(" s, od wroga ").Append(F0(pf.Gap)).Append(" m")
                  .Append(" | kierunek szyku od ekstremum ").Append(F0(dDir)).Append(" st (do osi wroga ").Append(F0(Ang(f.Direction, ax))).Append(" st), FacingOrder ")
                  .Append(FacName(pf.Fac)).Append(pf.FacExt != pf.Fac ? " (w ekstremum " + FacName(pf.FacExt) + ")" : "").Append(", ArrangementOrder ").Append(ArrName(pf.Arr))
                  .Append(pf.ArrExt != pf.Arr ? " (w ekstremum " + ArrName(pf.ArrExt) + ")" : "")
                  .Append(" | glebokosc ").Append(S1(pf.DepthExt)).Append(" -> ").Append(S1(f.Depth)).Append(" m, szerokosc ").Append(S1(pf.WidthExt)).Append(" -> ")
                  .Append(S1(f.Width)).Append(" m.");
                Log.Info(sb.ToString());
            }
            catch (Exception e)
            {
                _stum++;
                RoutHold.Stumble(RoutHold.SiteDoctrine, "Pendulum333.Nawrot", e);
            }
        }

        // ------------------------------------------------------------ segment linii "AI bitwy (333)"

        internal static void Append(StringBuilder sb)
        {
            float min = _preSec / 60f;
            sb.Append("wahadlo: klucz ").Append(_key ? "TAK" : "NIE").Append(" (latka ").Append(_patch).Append("; przed kontaktem > ").Append(ContactIn.ToString("0", Inv))
              .Append(" m miara RBM, wyjscie > ").Append(ContactOut.ToString("0", Inv)).Append(" m; 333b: punkt przyjety, martwa strefa rozkazu max(")
              .Append(DeadMove.ToString("0", Inv)).Append(" m, glebokosc), zapadka kierunku ").Append(Ratchet.ToString("0", Inv)).Append('/')
              .Append((2f * Ratchet).ToString("0", Inv)).Append('/').Append((4f * Ratchet).ToString("0", Inv)).Append('/').Append(RatchetMax.ToString("0", Inv))
              .Append(" s, trzymanie ").Append(HoldMin.ToString("0", Inv)).Append(" s; pomiar martwa strefa max(").Append(DeadMin.ToString("0", Inv))
              .Append(" m, pol glebokosci); konnica wroga ").Append(CavClose.ToString("0", Inv))
              .Append(" m albo szarza w ").Append(CavNear.ToString("0", Inv)).Append(" m = kontakt)")
              .Append(" | szykow AI ").Append(_forms).Append(", szyko-minut przed kontaktem ").Append(min.ToString("0.0", Inv))
              .Append(", nawroty ").Append(_rev).Append(" (w tym w tyl ").Append(_revBack).Append("; ").Append(min > 0.01f ? (_rev / min).ToString("0.00", Inv) : "-")
              .Append(" na szyko-minute; wedlug zachowania ");
            if (RevBy.Count == 0) sb.Append('-');
            else
            {
                bool first = true;
                foreach (var kv in RevBy)
                {
                    if (!first) sb.Append(", ");
                    first = false;
                    sb.Append(kv.Key).Append(' ').Append(kv.Value);
                }
            }
            sb.Append("), wejsc w kontakt ").Append(_contactIn).Append(" (w tym przez konnice wroga ").Append(_contactCav).Append(')');
            // 333d: diagnostyka (bez '|' w srodku - wyrazenie A333 w sprawdz_logi)
            sb.Append("; 333d: nawroty srodka ciezkosci ").Append(_revA).Append(" (w tyl ").Append(_revABack).Append("), nawroty razem ze srodkiem ciezkosci ").Append(_revBoth)
              .Append(" (w tyl ").Append(_revBothBack).Append("), nawrotow z odcinkiem < 2 progi ").Append(_revSmall)
              .Append(", drogi ");
            if (RevWay.Count == 0) sb.Append('-');
            else
            {
                bool fw = true;
                foreach (var kv in RevWay)
                {
                    if (!fw) sb.Append(", ");
                    fw = false;
                    sb.Append(kv.Key).Append(' ').Append(kv.Value[0]).Append(" (w tyl ").Append(kv.Value[1]).Append(')');
                }
            }
            sb.Append(", rozkazy nie-Move przed kontaktem ");
            if (NonMoveBy.Count == 0) sb.Append('-');
            else
            {
                bool fn = true;
                foreach (var kv in NonMoveBy)
                {
                    if (!fn) sb.Append(", ");
                    fn = false;
                    sb.Append(kv.Key).Append(' ').Append(kv.Value);
                }
            }
            sb.Append(" (zmian na nie-Move ").Append(_ordChg).Append("), zmiany FacingOrder ").Append(_facChg).Append(", ArrangementOrder ").Append(_arrChg)
              .Append(" | rozkazy Move przed kontaktem ").Append(_seen).Append(": wstecz zatrzymane ").Append(_backHeld).Append(", wstecz dozwolone S4 ").Append(_backS4)
              .Append(", wstecz po zmianie zachowania ").Append(_backBeh).Append(", naprzod wstrzymane po cofnieciu ").Append(_fwdHeld).Append(", szykow zatrzymanych ")
              .Append(HeldForms.Count)
              .Append("; 333b: pierwszych ").Append(_first).Append(", martwa strefa ").Append(_dead).Append(", trzymanie ").Append(_hold)
              .Append(", nawroty przepuszczone po oknie ").Append(_revOk).Append(", przyjete naprzod ").Append(_accFwd).Append(", w bok ").Append(_accSide)
              .Append("; 333e: obciete do ruchu w bok ").Append(_clip).Append(", nawroty po oknie za male ").Append(_zSmall)
              .Append("; 333f: zapadka od pozycji szyku, P przesuniete po minieciu ").Append(_over).Append(", cele obciete o mniej niz ").Append(DeadMin.ToString("0", Inv))
              .Append(" m za szykiem ").Append(_clipSmall);
        }
    }
}
