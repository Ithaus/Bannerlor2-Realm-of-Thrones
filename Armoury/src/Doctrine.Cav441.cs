using System;
using System.Globalization;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 441 = PAKIET 4 krok 4.0 audytu AI bitwy (docs/audyty-2026-11-11/AUDYT-AI-BITWA.md rozdz. 3 "Jazda", fakty techniczne 1-3; Jeff 11.11 "rob wszystko po kolei
    /// tak jak radzisz", decyzje AI 11.11: jazda pod ostrzalem rozbija lucznikow albo chowa sie za piechote, jazda poluje na wodza z tylu). SZKIELET - sam nic nie
    /// robi: w 441 kazdy szyk ma role NONE. Klucz CavalryCommander ("Cavalry Commander", domyslnie TAK); NIE = zachowanie roli nie jest dodawane, zadnych
    /// pominiec, zadnego wyjatku klusa - co do znaku jak dotad (latka zalozona, ale postfiks wychodzi na pierwszej linii; linia "Jazda AI (441)" zostaje).
    ///
    /// Fakt 1 (mb FormationAI.FindBestBehavior: premia aktywnego x1.2-2.0; oslona skrzydla RBM 10 na sztywno): rola zwraca wage 100 (RoleWeight) - wiecej niz
    ///   20, czyli wygrywa z kazdym zachowaniem gry i RBM; oddanie szyku = zwrot 0 i WeightFactor 0 (zachowanie w ogole nie jest kandydatem).
    /// Fakt 2 (rbmai: postfiksy taktyk RBM wolaja FormationAI.ResetBehaviorWeights przy kazdej zmianie fazy -> BehaviorComponent.ResetBehavior, metoda wirtualna,
    ///   bazowa daje WeightFactor 0): BehaviorCavRole441 nadpisuje ResetBehavior - WeightFactor 1 przy roli, 0 przy NONE; reset RBM nie zdejmuje roli.
    ///   Dodawane tam, gdzie RBM dodaje swoje: postfiks TeamAIGeneral.OnUnitAddedToFormationForTheFirstTime (DeclaredMethod - metoda zadeklarowana w TeamAIGeneral,
    ///   mb; RBM lata ja tak samo, rbmai OverrideTeamAIGeneral). Gra wola ja przy KAZDYM przejsciu szyku z 0 na > 0 ludzi (Formation.AddUnit) - RBM dodaje wtedy
    ///   swoje zachowania drugi raz, my nie (GetBehavior - licznik "juz bylo"). Latki zakladane przy pierwszej bitwie procesu (NightHooks.FirstBattle -> AfterStart):
    ///   szyki zapelnione przed latka (albo przed AfterStart) dostaja zachowanie w takcie glownego watku (licznik "zalegle").
    /// Wyjatek limitu klusa (MarchToContact.Eval, galaz konnicy): szyk w roli z galopem (RoleGallop) jedzie rozkazem Move bez capu marszu 128 - inaczej kazdy nasz
    ///   Move jazdy szedlby klusem. W 441 bez rol - wyjatek nigdy nie wchodzi (licznik 0).
    /// Jeden wlasciciel szyku: szyk w roli (C441Owned) jest dla 148 (poscig), 204 (jazda na strzelca), 283 (nekanie), 311a (oslona) i 344 (front / kontra / odruch)
    ///   tym, czym szyk gracza - nie bierze z niego jezdzcow, a jezdziec takiego szyku wraca (HunterCheck UPlayer, koniec poscigu EPlayer). Pominiecia liczone
    ///   osobno (nie w licznikach "szyk gracza"). W 441 bez rol - 0.
    /// Role: NONE, RAID (4.2 rajd na strzelcow), HIDE (4.2 schowaj za piechote), WAIT / STRIKE (4.3 kowadlo i mlot), CAVFIRST (4.4 najpierw jazda wroga),
    ///   DISENGAGE (4.5 wyjscie z grzezawiska), HUNT (polowanie na wodza). Nadaje je tylko C441SetRole z glownego watku (kolejne kroki) - najpierw rola i rozkaz,
    ///   potem WeightFactor; oddanie odwrotnie.
    /// Watki: zachowanie (GetAiWeight, ResetBehavior, CalculateCurrentOrder, TickOccasionally) biegnie w watku AI / silnika - tylko odczyt pol volatile i Interlocked;
    ///   postfiks dodania biegnie tam, gdzie Formation.AddUnit (glowny watek albo tick druzyn) - jedna alokacja jak RBM, bez logu i Settings.Current.
    ///   Skan (zalegle, miary, oddanie szyku graczowi) w OnMissionTick (Doctrine.Tick, glowny watek, co 1 s). Potkniecia liczone, bez globalnego wylacznika.
    /// Pomiar 437: GetAiWeight roli zapisuje wynik w tablicy wag 437 (BattleMeasure437.NoteWeight) - os czasu 437 pokaze "CavRole441" w trzech najwyzszych wagach
    ///   jazdy, gdy rola zacznie dzialac (przy NONE WeightFactor 0 - 437 jej nie wypisuje).
    /// </summary>
    internal static partial class Doctrine
    {
        internal const byte CrNone = 0, CrRaid = 1, CrHide = 2, CrWait = 3, CrStrike = 4, CrCavFirst = 5, CrDisengage = 6, CrHunt = 7, CrK = 8;
        private static readonly string[] CrName = { "NONE", "RAID", "HIDE", "WAIT", "STRIKE", "CAVFIRST", "DISENGAGE", "HUNT" };
        // rola, w ktorej Move jedzie galopem (wyjatek klusa 128 w MarchToContact); WAIT czeka - klus / stoj jak dotad
        private static readonly bool[] CrGallop = { false, true, true, false, true, true, true, true };
        internal const float C441Every = 1f;
        private const int CrSlots = MaxTeams * 10, CrRegular = 8;   // FormationClass.NumberOfRegularFormations
        private const int SkPursuit = 0, SkRiders = 1, SkShot = 2, SkK = 3;   // 148 / szyki 204-283-311a-344 (FI) / odruch 344 na trafienie jezdzca
        private static readonly IFormatProvider C4Inv = CultureInfo.InvariantCulture;

        // ---- stan czytany z innych watkow (pisze glowny watek): klucz misji, rola i szyk slotu [TeamIndex x 10 + FormationIndex]
        private static volatile bool _c4Live;
        private static readonly byte[] _c4Role = new byte[CrSlots];
        private static readonly Formation[] _c4Who = new Formation[CrSlots];
        private static readonly BehaviorCavRole441[] _c4Beh = new BehaviorCavRole441[CrSlots];
        private static float _c4Acc;

        // ---- latka (raz na proces)
        private static string _c4Patch = "nie wpiete";

        // ---- liczniki misji (C441Reset w Battle291.Begin); z watku AI - Interlocked
        private static int _c4AddPost, _c4AddTick, _c4Dup, _c4PostErr, _c4Weight, _c4Reset, _c4ResetRole, _c4Pace, _c4Stum, _c4Back;
        private static readonly int[] _c4Skip = new int[SkK];
        private static float _c4CavSec, _c4NoBehSec, _c4ActSec;
        private static readonly float[] _c4RoleSec = new float[CrK];

        /// <summary>Battle291.Begin (AfterStart): liczniki linii 441 od zera (stan slotow czysci Doctrine.Clear).</summary>
        internal static void C441Reset()
        {
            Interlocked.Exchange(ref _c4AddPost, 0); Interlocked.Exchange(ref _c4Dup, 0); Interlocked.Exchange(ref _c4PostErr, 0);
            Interlocked.Exchange(ref _c4Weight, 0); Interlocked.Exchange(ref _c4Reset, 0); Interlocked.Exchange(ref _c4ResetRole, 0);
            for (int k = 0; k < SkK; k++) Interlocked.Exchange(ref _c4Skip[k], 0);
            _c4AddTick = _c4Pace = _c4Stum = _c4Back = 0;
            _c4CavSec = _c4NoBehSec = _c4ActSec = 0f;
            for (int k = 0; k < CrK; k++) _c4RoleSec[k] = 0f;
        }

        /// <summary>Doctrine.Begin (AfterStart, po Clear): klucz misji - bitwa polowa, nie morska.</summary>
        private static void C441Begin(Mission m)
        {
            var c = Settings.Current;
            bool live = false;
            try { live = c != null && c.CavalryCommander && m != null && m.IsFieldBattle && !m.IsNavalBattle; } catch { live = false; }
            _c4Acc = 0f;
            _c4Live = live;
        }

        /// <summary>Doctrine.Clear (Begin / End): role i szyki slotow od zera. Najpierw klucz - inne watki przestaja czytac sloty.</summary>
        private static void C441ClearMission()
        {
            _c4Live = false;
            for (int i = 0; i < CrSlots; i++)
            {
                var b = _c4Beh[i];
                if (b != null) { try { b.Give(); } catch { } }
                _c4Role[i] = CrNone; _c4Who[i] = null; _c4Beh[i] = null;
            }
            _c4Acc = 0f;
        }

        // ------------------------------------------------------------ latka (raz na proces - ApplyMissionPatches)

        private static string C441ApplyPatch(Harmony h)
        {
            try
            {
                var target = AccessTools.DeclaredMethod(typeof(TeamAIGeneral), "OnUnitAddedToFormationForTheFirstTime", new[] { typeof(Formation) });
                if (target == null) return _c4Patch = "NIE (brak TeamAIGeneral.OnUnitAddedToFormationForTheFirstTime)";
                h.Patch(target, postfix: new HarmonyMethod(typeof(Doctrine), nameof(C441AddPost)) { priority = Priority.Last, after = new[] { "com.rbmai" } });
                var info = Harmony.GetPatchInfo(target);
                bool ok = false;
                if (info != null)
                    foreach (var p in info.Postfixes)
                        if (p != null && p.owner == h.Id && p.PatchMethod != null && p.PatchMethod.Name == nameof(C441AddPost)) ok = true;
                return _c4Patch = ok ? "dodanie roli 1/1" : "dodanie roli 0/1 (latka polknieta)";
            }
            catch (Exception e) { return _c4Patch = "NIE (" + e.GetType().Name + ")"; }
        }

        /// <summary>Postfiks TeamAIGeneral.OnUnitAddedToFormationForTheFirstTime (watek Formation.AddUnit - glowny albo tick druzyn): zachowanie roli do szyku
        /// regularnego, raz (RBM dodaje przy kazdym zapelnieniu - my nie). Bez logu i Settings.Current.</summary>
        private static void C441AddPost(Formation formation)
        {
            if (!_c4Live || formation == null) return;
            try
            {
                if ((int)formation.FormationIndex >= CrRegular) return;
                var ai = formation.AI;
                if (ai == null) return;
                if (ai.GetBehavior<BehaviorCavRole441>() != null) { Interlocked.Increment(ref _c4Dup); return; }
                ai.AddAiBehavior(new BehaviorCavRole441(formation));
                Interlocked.Increment(ref _c4AddPost);
            }
            catch { Interlocked.Increment(ref _c4PostErr); }
        }

        // ------------------------------------------------------------ odczyty dla innych modulow (bez alokacji; takze z innych watkow)

        private static int C441Idx(Formation f)
        {
            var t = f.Team;
            if (t == null) return -1;
            int ti = t.TeamIndex, fi = (int)f.FormationIndex;
            if (ti < 0 || ti >= MaxTeams || fi < 0 || fi >= CrRegular) return -1;
            return ti * 10 + fi;
        }

        /// <summary>Jeden wlasciciel: szyk ma role Armoury (nie NONE) - 148 / 204 / 283 / 311a / 344 traktuja go jak szyk gracza (nie biora jezdzcow).</summary>
        internal static bool C441Owned(Formation f)
        {
            if (!_c4Live || f == null) return false;
            int idx = C441Idx(f);
            return idx >= 0 && _c4Role[idx] != CrNone && ReferenceEquals(_c4Who[idx], f);
        }

        /// <summary>MarchToContact (glowny watek): szyk w roli z galopem - rozkaz Move bez klusa 128.</summary>
        internal static bool C441FullPace(Formation f)
        {
            if (!_c4Live || f == null) return false;
            int idx = C441Idx(f);
            if (idx < 0) return false;
            byte r = _c4Role[idx];
            return r != CrNone && r < CrK && CrGallop[r] && ReferenceEquals(_c4Who[idx], f);
        }

        internal static void C441NoteFullPace() { _c4Pace++; }

        /// <summary>443 (glowny watek): rola szyku wedlug 441 (NONE, gdy klucz NIE, slot innego szyku albo rola oddana przez 441 - gracz / szyk pusty).</summary>
        internal static byte C441RoleOf(Formation f)
        {
            if (!_c4Live || f == null) return CrNone;
            int idx = C441Idx(f);
            if (idx < 0 || !ReferenceEquals(_c4Who[idx], f)) return CrNone;
            return _c4Role[idx];
        }

        /// <summary>443: dowodca jazdy w tej misji (klucz Cavalry Commander, bitwa polowa) - bez niego role RAID / HIDE nie wchodza (tylko "bylby").</summary>
        internal static bool C441Live { get { return _c4Live; } }

        /// <summary>Pominiecie szyku w roli: 0 = 148, 1 = szyki 204 / 283 / 311a / 344 (pass), 2 = odruch 344 na trafienie jezdzca.</summary>
        internal static void C441NoteSkip(int who)
        {
            if (who >= 0 && who < SkK) Interlocked.Increment(ref _c4Skip[who]);
        }

        internal static void C441NoteWeight(BehaviorComponent b, float w)
        {
            Interlocked.Increment(ref _c4Weight);
            BattleMeasure437.NoteWeight(b, w);
        }

        internal static void C441NoteReset(bool role)
        {
            Interlocked.Increment(ref _c4Reset);
            if (role) Interlocked.Increment(ref _c4ResetRole);
        }

        /// <summary>Glowny watek (kolejne kroki pakietu 4): nadanie / oddanie roli. Rola i rozkaz przed WeightFactor, oddanie odwrotnie (Give). Szyk gracza - nie.</summary>
        internal static bool C441SetRole(Formation f, byte role, MovementOrder order)
        {
            if (!_c4Live || f == null || role >= CrK) return false;
            int idx = C441Idx(f);
            if (idx < 0) return false;
            var ai = f.AI;
            var b = ai != null ? ai.GetBehavior<BehaviorCavRole441>() : null;
            if (b == null) return false;
            if (role == CrNone) { b.Give(); _c4Role[idx] = CrNone; _c4Who[idx] = null; _c4Beh[idx] = null; return true; }
            if (HorsePursuit.PlayerForm(f) || f.HasPlayerControlledTroop) return false;
            _c4Who[idx] = f;
            _c4Beh[idx] = b;
            _c4Role[idx] = role;
            b.Take(role, order);
            return true;
        }

        // ------------------------------------------------------------ takt glownego watku (Doctrine.Tick, co 1 s)

        internal static void C441Tick(Mission m, float dt)
        {
            if (!_c4Live || m == null) return;
            var w = _m;
            if (w == null || !ReferenceEquals(w.Target, m) || m.MissionEnded) return;
            _c4Acc += dt;
            if (_c4Acc < C441Every) return;
            float step = _c4Acc;
            _c4Acc = 0f;
            bool dep;
            try { dep = m.IsDeploymentFinished || (m.Mode != MissionMode.Deployment && m.CurrentTime >= P440NoDeploy); } catch { dep = false; }
            var teams = m.Teams;
            int n = teams != null ? teams.Count : 0;
            for (int i = 0; i < n; i++)
            {
                Team t = null;
                try { t = teams[i]; } catch { t = null; }
                if (!MissionTeams.Valid(t) || !(t.TeamAI is TeamAIGeneral)) continue;
                if (t.Side != BattleSideEnum.Attacker && t.Side != BattleSideEnum.Defender) continue;
                int ti = t.TeamIndex;
                if (ti < 0 || ti >= MaxTeams) continue;
                var fl = t.FormationsIncludingSpecialAndEmpty;
                int nf = fl != null ? fl.Count : 0;
                for (int j = 0; j < nf; j++)
                {
                    Formation f = null;
                    try { f = fl[j]; } catch { f = null; }
                    if (f == null) continue;
                    try { C441Form(f, dep, step); }
                    catch (Exception e) { _c4Stum++; RoutHold.Stumble(RoutHold.SiteDoctrine, "Doctrine.Cav441 szyk", e); }
                }
            }
        }

        private static void C441Form(Formation f, bool dep, float step)
        {
            int idx = C441Idx(f);
            if (idx < 0) return;
            var ai = f.AI;
            if (ai == null) return;
            if (f.CountOfUnits <= 0)
            {
                if (_c4Role[idx] != CrNone) { C441SetRole(f, CrNone, default(MovementOrder)); _c4Back++; }
                return;
            }
            var b = ai.GetBehavior<BehaviorCavRole441>();
            if (b == null)
            {
                // zalegle: szyk zapelniony przed latka (pierwsza bitwa procesu) albo przed AfterStart - dodanie tutaj (OnMissionTick nie biegnie razem z tickiem druzyn)
                ai.AddAiBehavior(new BehaviorCavRole441(f));
                _c4AddTick++;
                b = ai.GetBehavior<BehaviorCavRole441>();
            }
            // szyk przejety przez gracza albo z czlowiekiem gracza - rola oddana (gracz jest wlascicielem)
            if (_c4Role[idx] != CrNone && (HorsePursuit.PlayerForm(f) || f.HasPlayerControlledTroop)) { C441SetRole(f, CrNone, default(MovementOrder)); _c4Back++; }
            if (!dep) return;
            bool act = b != null && ReferenceEquals(ai.ActiveBehavior, b);
            if (act) _c4ActSec += step;
            var q = f.QuerySystem;
            if (q == null || !q.IsCavalryFormation || !f.IsAIControlled) return;
            _c4CavSec += step;
            if (b == null) _c4NoBehSec += step;
            byte r = ReferenceEquals(_c4Who[idx], f) ? _c4Role[idx] : CrNone;
            if (r < CrK) _c4RoleSec[r] += step;
        }

        // ------------------------------------------------------------ linia konca bitwy (Battle291.Write)

        internal static string C441Line(string kind)
        {
            var c = Settings.Current;
            var sb = new StringBuilder(700);
            sb.Append("Jazda AI (441): misja ").Append(kind ?? "-")
              .Append(" | klucz dowodca jazdy ").Append(c != null && c.CavalryCommander ? "TAK" : "NIE")
              .Append(" | zachowanie roli dodane: postfiks ").Append(Volatile.Read(ref _c4AddPost)).Append(", zalegle w takcie ").Append(_c4AddTick)
              .Append(", juz bylo (ponowne zapelnienie szyku) ").Append(Volatile.Read(ref _c4Dup))
              .Append(" | jazda AI ").Append(_c4CavSec.ToString("0", C4Inv)).Append(" szyko-s (bez zachowania roli ").Append(_c4NoBehSec.ToString("0", C4Inv)).Append(") w rolach:");
            for (int k = 0; k < CrK; k++) sb.Append(' ').Append(CrName[k]).Append(' ').Append(_c4RoleSec[k].ToString("0", C4Inv));
            sb.Append(" | rola aktywnym zachowaniem ").Append(_c4ActSec.ToString("0", C4Inv)).Append(" szyko-s, GetAiWeight roli ").Append(Volatile.Read(ref _c4Weight))
              .Append(", ResetBehavior ").Append(Volatile.Read(ref _c4Reset)).Append(" (przy roli ").Append(Volatile.Read(ref _c4ResetRole)).Append(')')
              .Append(", rola oddana (gracz / szyk pusty) ").Append(_c4Back)
              .Append(" | wyjatek klusa (Move w roli galopem) ").Append(_c4Pace)
              .Append(" | szyk w roli pominiety: poscig 148 ").Append(Volatile.Read(ref _c4Skip[SkPursuit]))
              .Append(", szyki 204/283/311a/344 ").Append(Volatile.Read(ref _c4Skip[SkRiders]))
              .Append(", odruch 344 ").Append(Volatile.Read(ref _c4Skip[SkShot]))
              .Append(" | latka ").Append(_c4Patch).Append(" | potkniecia ").Append(_c4Stum).Append(" (postfiks ").Append(Volatile.Read(ref _c4PostErr)).Append(").");
            return sb.ToString();
        }
    }

    /// <summary>441: zachowanie roli jazdy Armoury (szkielet pakietu 4). Przy NONE: WeightFactor 0 (nie jest kandydatem FindBestBehavior), GetAiWeight 0.
    /// Przy roli: waga 100, ResetBehavior zostawia WeightFactor 1 (reset RBM nie zdejmuje roli), rozkaz z glownego watku (OrdBox - podmiana referencji).
    /// Metody wolane w watku AI / silnika - tylko odczyt pol volatile.</summary>
    internal sealed class BehaviorCavRole441 : BehaviorComponent
    {
        internal const float RoleWeight = 100f;

        private sealed class OrdBox
        {
            internal readonly MovementOrder O;
            internal OrdBox(MovementOrder o) { O = o; }
        }

        private volatile byte _role;
        private volatile OrdBox _ord;
        private OrdBox _applied;   // tylko watek AI (CalculateCurrentOrder)

        internal BehaviorCavRole441(Formation formation) : base(formation)
        {
            CurrentOrder = MovementOrder.MovementOrderStop;
            BehaviorCoherence = 0f;
        }

        public override float NavmeshlessTargetPositionPenalty => 1f;

        internal byte Role => _role;

        /// <summary>Glowny watek: rola i rozkaz, potem WeightFactor.</summary>
        internal void Take(byte role, MovementOrder order)
        {
            _ord = new OrdBox(order);
            _role = role;
            WeightFactor = 1f;
        }

        /// <summary>Glowny watek: oddanie szyku - WeightFactor 0, potem rola NONE (zwrot 0).</summary>
        internal void Give()
        {
            WeightFactor = 0f;
            _role = Doctrine.CrNone;
            _ord = null;
        }

        protected override float GetAiWeight()
        {
            float w = _role != Doctrine.CrNone ? RoleWeight : 0f;
            Doctrine.C441NoteWeight(this, w);
            return w;
        }

        public override void ResetBehavior()
        {
            bool role = _role != Doctrine.CrNone;
            WeightFactor = role ? 1f : 0f;
            Doctrine.C441NoteReset(role);
        }

        protected override void CalculateCurrentOrder()
        {
            if (_role == Doctrine.CrNone) return;
            var o = _ord;
            if (o != null && !ReferenceEquals(o, _applied)) { _applied = o; CurrentOrder = o.O; }
        }

        public override void TickOccasionally()
        {
            if (_role == Doctrine.CrNone) return;
            CalculateCurrentOrder();
            Formation.SetMovementOrder(CurrentOrder);
        }

        public override TextObject GetBehaviorString()
        {
            return new TextObject("{=!}hold your place in the cavalry plan");
        }
    }
}
