using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 468 (Jeff 12.11: "tak rob ... a czemu nie moga odpechanc dluga bronia jak maja? np wlocznia halabarda ??"). OBRONCY SPYCHAJA DRABINY TAKZE DRZEWCEM.
    /// Dowod (dekompilacja 1.4.8, d370/SiegeLadder.cs, rv441/mb):
    ///  - punkt spychania drabiny (_pushingWithForkStandingPoint, StandingPointWithWeaponRequirement z InitRequiredWeapon(push_fork), SL:293) wpuszcza
    ///    tylko kogos, kto ma widly push_fork w glownej rece (StandingPointWithWeaponRequirement.IsDisabledForAgent :46-51); obronca AI musi wiec najpierw
    ///    dojsc do lezacych widel (_forkPickUpStandingPoint), podniesc je (animacja), dopiero potem idzie do punktu spychania (SL:777-811);
    ///  - okno spychania jest krotkie: punkty obroncow czynne tylko na murze i przy CanLadderBePushed (nikt wyzej niz 55% drabiny, suma <= 170 - SL:1137-1158),
    ///    a kolejka atakujacych rusza zaraz po OnWall (SL:714-724) - droga po widly zjada okno;
    ///  - przydzial i wejscie na punkt ida przez IsDisabledForAgent: UsableMachine.IsSlotAtIndexAvailableForAgent (DetachmentManager) i
    ///    Agent.CanReachAndUseObject -> CanUseObject (UsableMachineAIBase.OnTick przy dojsciu); w trakcie uzycia nikt go nie sprawdza (StandingPoint.TickAux);
    ///  - animacja act_usage_ladder_push_back to akcja postaci (kanal 1), kod zarzadzany nie sprawdza, co jest w rece (SL:960-990); widly to TwoHandedPolearm;
    ///  - DropExtraWeaponOnStopUsageComponent na punkcie spychania wyrzuca tylko ExtraWeaponSlot - drzewce w slotach 0-3 zostaje u obroncy.
    /// ZMIANA (klucz SiegeDefendersPushLadders; NIE = gra co do bitu - postfiks wychodzi od razu, zostaje tylko pomiar): postfiks na
    /// StandingPointWithWeaponRequirement.IsDisabledForAgent (AccessTools.DeclaredMethod na klasie deklarujacej):
    ///  (a) punkt spychania drabiny w misji szturmu, ktory gra zamknela: otwarty dla obroncy AI (nie gracz) bez konia, z pustym ExtraWeaponSlot (chorazy nie
    ///      straci sztandaru), z bronia drzewcowa (TwoHandedPolearm / LowGripPolearm / OneHandedPolearm, dlugosc >= 150) w slotach 0-3 - przy przydziale;
    ///      przy wejsciu na punkt (czlowiek idacy do niego) drzewce musi juz byc w glownej rece - inaczej czeka przy punkcie, a logika misji mu je dobywa;
    ///  (b) punkt podniesienia widel tej drabiny: zamkniety, gdy ktos juz idzie do punktu spychania albo na nim stoi (jeden czlowiek na drabine - gra ma to
    ///      samo w GetSuitableAgentForStandingPoint, ale nie w DetachmentManager).
    /// Ludzi wysyla gra (DetachmentManager), okno spychania (CanLadderBePushed) bez zmian - regula tylko skraca droge.
    /// Logika misji: co 5 s przeglad drabin (pola przez FieldRefAccess, zbior punktow podmieniany w calosci - postfiks moze biec rownolegle i tylko czyta),
    /// co 0.5 s dobycie drzewca idacym na punkt, co klatke pomiar stanow drabin. Linie "Oblezenie misja (370) drabiny (468)": start (latka, prefiksy
    /// SiegeLadder.IsDisabledForBattleSide - com.rbmai = obroncy nie dostaja drabin), pierwsze 10 zepchniec, koniec misji. Wyjatek per drabina / czlowiek =
    /// potkniecie (pierwsze 3 do logu), bez globalnego wylacznika. Bez MovementOrder.
    /// </summary>
    internal static class SiegeLadderPush468
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int ErrMax = 3;
        private const int MinLength = 150;
        private const int MaxWieldTries = 6;   // 468-p: proby na jedno wyslanie (co 0.5 s = 3 s), potem odeslany
        private const int PushLines = 10;

        private static bool _installed;
        private static string _installInfo = "NIE";

        // zbior punktow misji (podmieniany w calosci, czytany w postfiksie)
        private sealed class Points
        {
            internal readonly HashSet<StandingPoint> Push = new HashSet<StandingPoint>();
            internal readonly Dictionary<StandingPoint, StandingPoint> PickToPush = new Dictionary<StandingPoint, StandingPoint>();
        }
        private static volatile Points _points;
        // 468-p (U2): odmowieni - nie dobyli drzewca w 3 s; zbior podmieniany w calosci (czytany w postfiksie)
        private static volatile HashSet<Agent> _refused;

        private static int _stumbles, _errs;

        private static bool On { get { var s = Settings.Current; return s != null && s.SiegeDefendersPushLadders; } }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errs >= ErrMax) return;
            _errs++;
            try { Log.Error("SiegeLadderPush468." + where, e); } catch { }
        }

        internal static void Install(Harmony h)
        {
            if (_installed || h == null) return;
            _installed = true;
            try
            {
                var target = AccessTools.DeclaredMethod(typeof(StandingPointWithWeaponRequirement), "IsDisabledForAgent", new[] { typeof(Agent) });
                if (target == null) { _installInfo = "NIE (brak celu)"; }
                else
                {
                    h.Patch(target, postfix: new HarmonyMethod(typeof(SiegeLadderPush468), nameof(Postfix)));
                    _installInfo = "wpieta";
                }
            }
            catch (Exception e) { _installInfo = "NIE (" + e.GetType().Name + ")"; Stumble("Install", e); }
            try { Log.Info("Oblezenie misja (370) drabiny (468): latka StandingPointWithWeaponRequirement.IsDisabledForAgent " + _installInfo + "."); } catch { }
        }

        // ---------------- postfiks (moze biec rownolegle: tylko odczyt, bez licznikow) ----------------

        public static void Postfix(StandingPointWithWeaponRequirement __instance, Agent agent, ref bool __result)
        {
            var pts = _points;
            if (pts == null || agent == null) return;
            try
            {
                if (__result)
                {
                    // (a) punkt spychania zamkniety przez gre - drzewce zamiast widel
                    if (!pts.Push.Contains(__instance) || !On) return;
                    if (__instance.IsDeactivated || !Eligible(agent)) return;
                    // 468-p (U3): nie podmieniac idacego z widlami (gra wyslala go po widly i teraz niesie je do punktu)
                    var mv = __instance.MovingAgent;
                    if (mv != null && mv != agent && !mv.Equipment[EquipmentIndex.ExtraWeaponSlot].IsEmpty) return;
                    EquipmentIndex slot;
                    if (__instance.MovingAgent == agent)
                    {
                        // wejscie na punkt: drzewce juz w glownej rece
                        if (!WieldedPolearm(agent)) return;
                    }
                    else if (__instance.UserAgent != agent && !PolearmSlot(agent, out slot)) return;
                    __result = false;
                }
                else
                {
                    // (b) widly: nikt nie idzie po nie, gdy punkt spychania jest juz zajety
                    StandingPoint push;
                    if (!pts.PickToPush.TryGetValue(__instance, out push) || push == null || !On) return;
                    if (agent.IsAIControlled && (push.HasAIMovingTo || push.HasUser)) __result = true;
                }
            }
            catch { }
        }

        internal static bool Eligible(Agent agent)
        {
            if (!agent.IsAIControlled || agent.MountAgent != null || agent.Team == null || agent.Team.Side != BattleSideEnum.Defender) return false;
            if (!agent.IsAbleToUseMachine()) return false;
            if (!agent.Equipment[EquipmentIndex.ExtraWeaponSlot].IsEmpty) return false;
            var r = _refused;
            return r == null || !r.Contains(agent);
        }

        private static bool IsPolearm(ItemObject it)
        {
            if (it == null || it.Weapons == null) return false;
            foreach (var wc in it.Weapons)
            {
                if (wc == null || wc.IsConsumable || wc.WeaponLength < MinLength) continue;
                var c = wc.WeaponClass;
                if (c == WeaponClass.TwoHandedPolearm || c == WeaponClass.LowGripPolearm || c == WeaponClass.OneHandedPolearm) return true;
            }
            return false;
        }

        internal static bool PolearmSlot(Agent agent, out EquipmentIndex slot)
        {
            for (var i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.ExtraWeaponSlot; i++)
            {
                var w = agent.Equipment[i];
                if (!w.IsEmpty && IsPolearm(w.Item)) { slot = i; return true; }
            }
            slot = EquipmentIndex.None;
            return false;
        }

        internal static bool WieldedPolearm(Agent agent)
        {
            var i = agent.GetPrimaryWieldedItemIndex();
            return i != EquipmentIndex.None && i < EquipmentIndex.ExtraWeaponSlot && IsPolearm(agent.Equipment[i].Item);
        }

        // ---------------- logika misji ----------------

        internal sealed class Logic : MissionLogic
        {
            private sealed class Lad
            {
                internal SiegeLadder L;
                internal int Idx;
                internal StandingPointWithWeaponRequirement Push, Pick;
                internal SiegeLadder.LadderState Prev;
                internal bool WasOnWall, WindowOpen, PushStarted, WindowWould;
                internal float OnWallAt = -1f, WindowAt = -1f;
                internal Agent LastMover, LastPicker;
                internal int MoverTries;   // 468-p: proby dobycia drzewca obecnemu idacemu
            }

            private static readonly AccessTools.FieldRef<SiegeLadder, StandingPointWithWeaponRequirement> _fPush = Ref("_pushingWithForkStandingPoint");
            private static readonly AccessTools.FieldRef<SiegeLadder, StandingPointWithWeaponRequirement> _fPick = Ref("_forkPickUpStandingPoint");

            private static AccessTools.FieldRef<SiegeLadder, StandingPointWithWeaponRequirement> Ref(string name)
            {
                try { return AccessTools.FieldRefAccess<SiegeLadder, StandingPointWithWeaponRequirement>(name); } catch { return null; }
            }

            private readonly List<Lad> _lads = new List<Lad>();
            private readonly Dictionary<SiegeLadder, Lad> _byLadder = new Dictionary<SiegeLadder, Lad>();
            private bool _known, _siege, _written, _started;
            private float _nextScan = -1f, _nextWield = -1f;
            private string _prefixes = "-";
            private ItemObject _fork;

            // liczniki
            private int _onWall, _windows, _windowsWould, _sentFork, _sentPole, _toFork, _pushFork, _pushPole, _pushPlayer, _pushOther,
                        _stopped, _down, _wields, _wieldFail, _lines, _noFields;
            private double _windowSum, _toPushSum;
            private int _toPushN;

            public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

            public override void OnBehaviorInitialize()
            {
                base.OnBehaviorInitialize();
                _points = null;
                _refused = null;
                _stumbles = _errs = 0;
            }

            private bool Siege()
            {
                if (!_known)
                {
                    try { _siege = Mission != null && Mission.IsSiegeBattle && !GameNetwork.IsMultiplayer; } catch { _siege = false; }
                    _known = true;
                }
                return _siege;
            }

            public override void OnMissionTick(float dt)
            {
                if (_written || !Siege()) return;
                float now;
                try { now = Mission.CurrentTime; } catch (Exception e) { Stumble("Tick.time", e); return; }
                if (now >= _nextScan) { _nextScan = now + 5f; Scan(now); }
                bool wield = now >= _nextWield;
                if (wield) _nextWield = now + 0.5f;
                for (int i = 0; i < _lads.Count; i++)
                {
                    var d = _lads[i];
                    try { Track(d, now, wield); }
                    catch (Exception e) { Stumble("Track", e); }
                }
            }

            private void Scan(float now)
            {
                try
                {
                    if (_fork == null)
                        try { _fork = Game.Current != null ? Game.Current.ObjectManager.GetObject<ItemObject>("push_fork") : null; } catch { }
                    var objs = Mission.MissionObjects;
                    bool added = false;
                    foreach (var o in objs)
                    {
                        var l = o as SiegeLadder;
                        if (l == null || _byLadder.ContainsKey(l)) continue;
                        try
                        {
                            var d = new Lad { L = l, Idx = _lads.Count + 1, Prev = l.State };
                            d.Push = _fPush != null ? _fPush(l) : null;
                            d.Pick = _fPick != null ? _fPick(l) : null;
                            if (d.Push == null) _noFields++;
                            _byLadder[l] = d;
                            _lads.Add(d);
                            added = true;
                        }
                        catch (Exception e) { Stumble("Scan.ladder", e); }
                    }
                    if (added)
                    {
                        var p = new Points();
                        foreach (var d in _lads)
                        {
                            if (d.Push == null) continue;
                            p.Push.Add(d.Push);
                            if (d.Pick != null) p.PickToPush[d.Pick] = d.Push;
                        }
                        _points = p;
                    }
                    if (!_started && _lads.Count > 0)
                    {
                        _started = true;
                        _prefixes = Prefixes();
                        Log.Info("Oblezenie misja (370) drabiny (468): start | klucz " + (On ? "TAK" : "NIE") + ", drabin " + _lads.Count
                                 + (_noFields > 0 ? " (bez punktu spychania " + _noFields + ")" : "") + ", widly " + (_fork != null ? "push_fork" : "BRAK")
                                 + " | latka IsDisabledForAgent " + _installInfo + " | prefiksy SiegeLadder.IsDisabledForBattleSide: " + _prefixes + ".");
                    }
                }
                catch (Exception e) { Stumble("Scan", e); }
            }

            private static string Prefixes()
            {
                try
                {
                    var m = AccessTools.DeclaredMethod(typeof(SiegeLadder), "IsDisabledForBattleSide", new[] { typeof(BattleSideEnum) });
                    if (m == null) return "brak celu";
                    var info = Harmony.GetPatchInfo(m);
                    if (info == null || info.Prefixes == null || info.Prefixes.Count == 0) return "brak";
                    var sb = new StringBuilder();
                    foreach (var p in info.Prefixes) { if (sb.Length > 0) sb.Append(", "); sb.Append(p.owner); }
                    return sb.ToString();
                }
                catch (Exception e) { return "blad " + e.GetType().Name; }
            }

            private bool HoldsFork(Agent a)
            {
                if (_fork == null || a == null) return false;
                var i = a.GetPrimaryWieldedItemIndex();
                return i != EquipmentIndex.None && a.Equipment[i].Item == _fork;
            }

            private void Track(Lad d, float now, bool wield)
            {
                var st = d.L.State;
                bool onWall = st == SiegeLadder.LadderState.OnWall;
                if (onWall && !d.WasOnWall) { _onWall++; d.OnWallAt = now; }
                d.WasOnWall = onWall;

                if (d.Push != null)
                {
                    // okno spychania: punkt obroncow czynny na murze
                    bool open = onWall && !d.Push.IsDeactivated;
                    if (open && !d.WindowOpen) { d.WindowOpen = true; d.WindowAt = now; d.WindowWould = false; _windows++; }
                    else if (!open && d.WindowOpen) { d.WindowOpen = false; if (d.WindowAt >= 0f) _windowSum += now - d.WindowAt; }

                    // przy NIE: czy w oknie stal uprawniony wlocznik w promieniu 10 m (bylby)
                    if (d.WindowOpen && !d.WindowWould && wield && !On && WouldHave(d)) { d.WindowWould = true; _windowsWould++; }

                    // wyslani do punktu spychania
                    var mover = d.Push.MovingAgent;
                    if (mover != d.LastMover)
                    {
                        d.MoverTries = 0;
                        if (mover != null)
                        {
                            if (HoldsFork(mover) || (_fork != null && mover.Equipment[EquipmentIndex.ExtraWeaponSlot].Item == _fork)) _sentFork++; else _sentPole++;
                        }
                    }
                    d.LastMover = mover;
                    // 468-p (U5): stojacemu na punkcie broni nie dobywamy (wejscie i tak wymaga drzewca w rece; dobycie przerwaloby spychanie)
                    if (mover != null && wield && On) Wield(d, mover);
                }
                // wyslani po widly
                if (d.Pick != null)
                {
                    var picker = d.Pick.MovingAgent;
                    if (picker != null && picker != d.LastPicker) _toFork++;
                    d.LastPicker = picker;
                }

                if (st != d.Prev)
                {
                    // 468-p (U1): StartFromWall przechodzi w BeingPushedBack w tej samej klatce (OnLadderStateChange, SL:488-496) - poczatek
                    // zepchniecia to wejscie w BeingPushedBack prosto z muru (BeingRaisedStopped -> BeingPushedBack to nieudane podniesienie, nie liczy sie)
                    bool fromWall = d.Prev == SiegeLadder.LadderState.OnWall || d.Prev == SiegeLadder.LadderState.FallToWall
                                    || d.Prev == SiegeLadder.LadderState.BeingPushedBackStartFromWall;
                    if (!d.PushStarted && (st == SiegeLadder.LadderState.BeingPushedBackStartFromWall
                                           || (st == SiegeLadder.LadderState.BeingPushedBack && fromWall)))
                    {
                        d.PushStarted = true;
                        var u = d.Push != null ? d.Push.UserAgent : null;
                        string who;
                        if (u == null) { who = "-"; _pushOther++; }
                        else if (!u.IsAIControlled) { who = "gracz"; _pushPlayer++; }
                        else if (HoldsFork(u)) { who = "widly"; _pushFork++; }
                        else
                        {
                            var i = u.GetPrimaryWieldedItemIndex();
                            var it = i != EquipmentIndex.None ? u.Equipment[i].Item : null;
                            who = it != null ? it.StringId : "pusta reka";
                            _pushPole++;
                        }
                        float since = d.OnWallAt >= 0f ? now - d.OnWallAt : -1f;
                        if (since >= 0f) { _toPushSum += since; _toPushN++; }
                        if (_lines < PushLines)
                        {
                            _lines++;
                            Log.Info("Oblezenie misja (370) drabiny (468): spycha drabina #" + d.Idx + ", kto: " + who + ", od oparcia na murze "
                                     + (since >= 0f ? since.ToString("0.0", Inv) + " s" : "-") + ", czas misji " + now.ToString("0", Inv) + " s.");
                        }
                    }
                    else if (st == SiegeLadder.LadderState.BeingPushedBackStopped && d.PushStarted) { _stopped++; d.PushStarted = false; }
                    else if ((st == SiegeLadder.LadderState.FallToLand || st == SiegeLadder.LadderState.OnLand) && d.PushStarted) { _down++; d.PushStarted = false; }
                    d.Prev = st;
                }
            }

            private void Wield(Lad d, Agent a)
            {
                try
                {
                    // 468-p (U4, U6): nieaktywny w tej klatce, gracz, idacy z widlami (w rece albo w dodatkowym slocie) - bez zmian
                    if (!a.IsActive() || !a.IsAIControlled || HoldsFork(a) || !a.Equipment[EquipmentIndex.ExtraWeaponSlot].IsEmpty) return;
                    if (WieldedPolearm(a)) return;
                    EquipmentIndex slot;
                    bool has = PolearmSlot(a, out slot);
                    if (!has || d.MoverTries >= MaxWieldTries)
                    {
                        // 468-p (U2): nie dobyl drzewca w 3 s (AI wrocilo do innej broni) - odeslany, punkt wolny dla widel jak w grze
                        Refuse(a);
                        _wieldFail++;
                        a.StopUsingGameObject(false);   // jak UsableMissionObject.OnUse dla idacego (MB/UsableMissionObject.cs:376)
                        return;
                    }
                    d.MoverTries++;
                    a.TryToWieldWeaponInSlot(slot, Agent.WeaponWieldActionType.WithAnimation, false);
                    _wields++;
                }
                catch (Exception e) { Stumble("Wield", e); }
            }

            private static void Refuse(Agent a)
            {
                var old = _refused;
                var n = old != null ? new HashSet<Agent>(old) : new HashSet<Agent>();
                n.Add(a);
                _refused = n;
            }

            private bool WouldHave(Lad d)
            {
                try
                {
                    var pos = d.Push.GameEntity.GlobalPosition.AsVec2;
                    var s = AgentProximityMap.BeginSearch(Mission, pos, 10f);
                    while (s.LastFoundAgent != null)
                    {
                        var a = s.LastFoundAgent;
                        EquipmentIndex slot;
                        if (a.IsActive() && Eligible(a) && PolearmSlot(a, out slot)) return true;
                        AgentProximityMap.FindNext(Mission, ref s);
                    }
                }
                catch (Exception e) { Stumble("WouldHave", e); }
                return false;
            }

            protected override void OnEndMission()
            {
                Final();
                base.OnEndMission();
            }

            public override void OnRemoveBehavior()
            {
                Final();
                base.OnRemoveBehavior();
            }

            private void Final()
            {
                if (_written) return;
                _written = true;
                _points = null;
                _refused = null;
                try
                {
                    if (!_siege || _lads.Count == 0) return;
                    float now = Mission != null ? Mission.CurrentTime : 0f;
                    foreach (var d in _lads)
                        if (d.WindowOpen && d.WindowAt >= 0f) { _windowSum += now - d.WindowAt; d.WindowOpen = false; }
                    Log.Info("Oblezenie misja (370) drabiny (468): koniec | klucz " + (On ? "TAK" : "NIE") + ", drabin " + _lads.Count
                             + ", oparc na murze " + _onWall + ", okien spychania " + _windows
                             + (_windows > 0 ? " (srednio " + (_windowSum / _windows).ToString("0.0", Inv) + " s)" : "")
                             + (On ? "" : ", okien z wlocznikiem w 10 m (bylby) " + _windowsWould)
                             + ", wyslanych do spychania: z widlami " + _sentFork + " / z drzewcem " + _sentPole + ", po widly " + _toFork
                             + ", zepchniec zaczetych: widly " + _pushFork + " / drzewce " + _pushPole + " / gracz " + _pushPlayer + (_pushOther > 0 ? " / inne " + _pushOther : "")
                             + (_toPushN > 0 ? " (od oparcia srednio " + (_toPushSum / _toPushN).ToString("0.0", Inv) + " s)" : "")
                             + ", zatrzymanych " + _stopped + ", do ziemi " + _down
                             + ", prob dobycia drzewca " + _wields + ", odeslanych bez drzewca w rece po 3 s " + _wieldFail
                             + ", potkniecia " + _stumbles + " | prefiksy IsDisabledForBattleSide: " + Prefixes() + ".");
                }
                catch (Exception e) { Stumble("Final", e); }
            }
        }
    }
}
