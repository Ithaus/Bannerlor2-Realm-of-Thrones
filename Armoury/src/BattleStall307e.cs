using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 307e (autotest 143a bitwa 4 [mocna], gracz 65 / wrog 44): od 575 do 1376 s czasu misji ciosy wrecz staly na 511, strzaly na 100, zywi gracz 4 / wrog 6-7 -
    /// 800 s bez jednego ciosu, limit czasu i odwrot. Log nie mowi, kim byli ci ludzie (linia postepu autotestu ma tylko liczby). Z linii misji wiadomo:
    /// 'frey_rider x3 - puste 2 (bron boczna (biala) 3)' (konni lucznicy gracza bez broni bocznej), lucznicy skagosi wroga strzelali do 10:15:11, potem strzal
    /// juz nie bylo; RBM (AgentAi :651-657) zdejmuje odwrot kazdemu z morale > 0 po 10 s bez ciosu wrecz - zwykly Retreat bez paniki nie wyprowadza z pola.
    /// HIPOTEZA (nie udowodniona): zostali ludzie bez czym walczyc (luk bez strzal, bez broni recznej) - strzelec bez strzal i bez noza nie podejdzie do wroga,
    /// a wrog nie ma do kogo isc albo sam stoi w tym samym polozeniu.
    /// REGULA (klucz BattleStallBreak, bitwa polowa kampanii, obie strony jednakowo): po pierwszym ciosie w bitwie, gdy przez BattleStallSeconds sekund czasu misji
    /// nie padl zaden cios i nie wylecial zaden pocisk - kazdy czlowiek AI (nie gracz, nie bohater - recenzja 307e) bez broni recznej, bez amunicji do luku / kuszy i bez broni rzucanej
    /// schodzi z pola: RoutHold.Break (ta sama droga zlamanego - bez powrotu, do granicy pola; liczy sie w linii NB2), bez RoutKeeper - morale 0 (droga paniki
    /// gry). Kto ma czym walczyc - bez zmian. Pierwszy zastoj misji (i do 3 na misje) - linia diagnozy "Zastoj bitwy (307e)" ze spisem obu stron (bron, szyk,
    /// rozkaz, zachowanie szyku, predkosc, odleglosc do wroga, panika) - dowod do nastepnego kroku. Linia konca misji "Zastoj bitwy (307e): koniec misji".
    /// Nic co klatke na agenta: sprawdzenie co 1 s (licznik czasu), spis tylko przy zastoju. Wyjatki liczone (bez globalnego wylacznika).
    /// </summary>
    internal sealed class BattleStall307e : MissionLogic
    {
        private const float CheckEvery = 1f, RecheckEvery = 10f;
        private const int DumpMax = 3, ListMax = 12, SampleMax = 40;   // recenzja 307e: odleglosc do wroga tylko dla probki (bez O(n^2) po calym polu)
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private float _last = -1f, _next, _nextRecheck;
        private bool _contact, _open, _field, _fieldKnown, _deployed, _ended;
        private int _episodes, _dumps, _outPlayer, _outFoe, _outRefused, _outMorale, _outMoraleP, _outMoraleF, _skipUndead, _stumbles;
        private float _longest, _stallSince;
        private bool _err;

        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        private bool Field()
        {
            if (!_fieldKnown)
            {
                _fieldKnown = true;
                try
                {
                    var m = Mission;
                    bool naval = false;
                    try { naval = m.IsNavalBattle || m.IsNavalRaidBattle; } catch { naval = false; }
                    _field = m != null && m.IsFieldBattle && !naval && !HideoutAlarm.IsHideout(m) && MissionTeams.Kind(m) == "bitwa";
                }
                catch { _field = false; }
            }
            return _field;
        }

        public override void OnDeploymentFinished()
        {
            _deployed = true;
            base.OnDeploymentFinished();
        }

        public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData attackCollisionData)
        {
            try
            {
                var m = Mission;
                if (m == null) return;
                _last = m.CurrentTime;
                if (affectorAgent != null && affectedAgent != null && affectedAgent.IsHuman) _contact = true;
            }
            catch { }
        }

        public override void OnAgentShootMissile(Agent shooterAgent, EquipmentIndex weaponIndex, Vec3 position, Vec3 velocity, Mat3 orientation, bool hasRigidBody, int forcedMissileIndex)
        {
            try { var m = Mission; if (m != null) _last = m.CurrentTime; } catch { }
        }

        // 309: koszt klatki (FrameCost309) - pomiar calego ticku, zachowanie bez zmian
        public override void OnMissionTick(float dt)
        {
            long t309 = FrameCost309.T();
            try { Tick309(dt); }
            finally { FrameCost309.Add(FrameCost309.MStall, t309); }
        }

        private void Tick309(float dt)
        {
            var m = Mission;
            if (m == null || _ended) return;
            float now = m.CurrentTime;
            if (now < _next) return;
            _next = now + CheckEvery;
            try
            {
                if (m.MissionEnded) { _ended = true; return; }
                var c = Settings.Current;
                if (c == null || !_contact || _last < 0f || !Field()) return;           // pierwszy cios = rozstawienie dawno za nami (_deployed tylko do linii)
                float stallSec = MBMath.ClampFloat(c.BattleStallSeconds, 20, 300);
                float idle = now - _last;
                if (idle < stallSec)
                {
                    if (_open) { _open = false; float len = now - _stallSince; if (len > _longest) _longest = len; }
                    return;
                }
                if (!BothSides(m)) return;                                  // jedna strona juz bez ludzi na polu (koniec bitwy, gracz chodzi po polu) - to nie zastoj
                if (!_open)
                {
                    _open = true; _episodes++; _stallSince = _last; _nextRecheck = now;
                    if (_dumps < DumpMax) { _dumps++; Dump(m, idle, c); }
                }
                if (!c.BattleStallBreak || now < _nextRecheck) return;
                _nextRecheck = now + RecheckEvery;
                SendOff(m);
            }
            catch (Exception e) { Stumble("Tick", e); }
        }

        /// <summary>Czy czlowiek ma czym walczyc: bron reczna, luk / kusza z amunicja albo bron rzucana z zapasem (MissionEquipment, cache gry).</summary>
        private static bool CanFight(Agent a, out int ammo, out bool melee, out bool ranged)
        {
            ammo = 0; melee = false; ranged = false;
            var eq = a.Equipment;
            if (eq == null) return false;
            melee = eq.ContainsMeleeWeapon();
            for (var i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.NumAllWeaponSlots; i++)
            {
                var w = eq[i];
                if (w.IsEmpty) continue;
                var u = w.CurrentUsageItem;
                if (u == null) continue;
                if (u.IsRangedWeapon && !u.IsConsumable) { ranged = true; int n = eq.GetAmmoAmount(i); if (n > 0) ammo += n; }
                else if (u.IsRangedWeapon && u.IsConsumable && w.Amount > 0) { ranged = true; ammo += w.Amount; }
            }
            return melee || ammo > 0;
        }

        private void SendOff(Mission m)
        {
            List<Agent> q = null;
            foreach (var a in m.Agents)
            {
                try
                {
                    if (a == null || !a.IsHuman || !a.IsActive() || a.IsMainAgent || !a.IsAIControlled || a.Team == null || a.IsRunningAway) continue;
                    // recenzja 307e: bohater zostaje (tylko w diagnozie) - rozbitemu bohaterowi gra robi WoundTroop, lord moglby trafic do niewoli
                    if (a.IsHero) continue;
                    if (RoutHold.KnownAnyThread(a)) continue;
                    int ammo; bool melee, ranged;
                    if (CanFight(a, out ammo, out melee, out ranged)) continue;
                    if (q == null) q = new List<Agent>();
                    q.Add(a);
                }
                catch (Exception e) { Stumble("SendOff(spis)", e); }
            }
            if (q == null) return;
            foreach (var a in q)
            {
                try
                {
                    bool pl = a.Team.IsPlayerTeam || a.Team.IsPlayerAlly;
                    if (RoutHold.On(m))
                    {
                        if (!RoutHold.Break(a, MissionTeams.Flee)) { _outRefused++; continue; }
                        if (pl) _outPlayer++; else _outFoe++;
                    }
                    else
                    {
                        // recenzja 307e: bez RoutKeeper - trup nie zna strachu (jak RoutHold.Break); morale 0 liczone osobno (to nie "zeszli z pola" - gra moze ich nie wyprowadzic)
                        if (Undead.Character(a.Character)) { _skipUndead++; continue; }
                        a.SetMorale(0f); _outMorale++;
                        if (pl) _outMoraleP++; else _outMoraleF++;
                    }
                }
                catch (Exception e) { Stumble("SendOff", e); }
            }
        }

        private void Dump(Mission m, float idle, Settings c)
        {
            try
            {
                var sb = new StringBuilder(2048);
                // recenzja 307e: pozycje ludzi obu stron zebrane raz (strona gracza / wrog) - odleglosc do najblizszego wroga tylko dla probki SampleMax
                var posPl = new List<Vec3>(); var posFoe = new List<Vec3>();
                foreach (var o in m.Agents)
                {
                    if (o == null || !o.IsHuman || !o.IsActive() || o.Team == null) continue;
                    if (o.Team.IsPlayerTeam || o.Team.IsPlayerAlly) posPl.Add(o.Position); else posFoe.Add(o.Position);
                }
                sb.Append("Zastoj bitwy (307e): ").Append(idle.ToString("0", Inv)).Append(" s czasu misji bez ciosu i strzalu (czas misji ").Append(m.CurrentTime.ToString("0", Inv))
                  .Append(" s, prog ").Append(c.BattleStallSeconds).Append(" s, klucz Battle Stall Break ").Append(c.BattleStallBreak ? "TAK" : "NIE").Append(')');
                for (int side = 0; side < 2; side++)
                {
                    bool wantPlayer = side == 0;
                    int n = 0, fight = 0, none = 0, panic = 0, mounted = 0, rangedEmpty = 0, listed = 0, sampled = 0;
                    var foes = wantPlayer ? posFoe : posPl;
                    float dSum = 0f, dMin = float.MaxValue;
                    var det = new StringBuilder(800);
                    foreach (var a in m.Agents)
                    {
                        if (a == null || !a.IsHuman || !a.IsActive() || a.Team == null) continue;
                        bool pl = a.Team.IsPlayerTeam || a.Team.IsPlayerAlly;
                        if (pl != wantPlayer) continue;
                        n++;
                        int ammo; bool melee, ranged;
                        bool can = CanFight(a, out ammo, out melee, out ranged);
                        if (can) fight++; else none++;
                        if (ranged && ammo == 0) rangedEmpty++;
                        bool pan = a.IsRunningAway || RoutHold.KnownAnyThread(a);
                        if (pan) panic++;
                        if (a.HasMount) mounted++;
                        float d = float.MaxValue;
                        if (sampled < SampleMax)
                        {
                            sampled++;
                            d = Nearest(foes, a.Position);
                            if (d < float.MaxValue) { dSum += d; if (d < dMin) dMin = d; }
                        }
                        if (listed >= ListMax) continue;
                        listed++;
                        string troop = "?";
                        try { troop = a.Character != null ? a.Character.StringId : (a.Name ?? "?"); } catch { }
                        string fo = "-", ord = "-", beh = "-";
                        try
                        {
                            var f = a.Formation;
                            if (f != null)
                            {
                                fo = f.RepresentativeClass.ToString() + (f.IsAIControlled ? " AI" : " gracz");
                                ord = f.GetReadonlyMovementOrderReference().OrderType.ToString();
                                var b = f.AI != null ? f.AI.ActiveBehavior : null;
                                beh = b != null ? b.GetType().Name : "-";
                            }
                        }
                        catch { }
                        float v = 0f;
                        try { v = a.MovementVelocity.Length; } catch { }
                        det.Append(listed > 1 ? "; " : "").Append(troop).Append(a.IsMainAgent ? " (gracz)" : a.IsHero ? " (bohater)" : "")
                           .Append(a.HasMount ? " konno" : " pieszo").Append(", bron: ").Append(melee ? "reczna" : "bez recznej").Append(ranged ? ", amunicja " + ammo : "")
                           .Append(", szyk ").Append(fo).Append(" ").Append(ord).Append(" ").Append(beh)
                           .Append(", v ").Append(v.ToString("0.0", Inv)).Append(" m/s, wrog ").Append(d < float.MaxValue ? d.ToString("0", Inv) + " m" : "-")
                           .Append(pan ? ", w ucieczce" : "").Append(a.IsAIControlled ? "" : ", sterowany przez gracza");
                    }
                    sb.Append(" | ").Append(wantPlayer ? "strona gracza" : "wrog").Append(": ludzi ").Append(n).Append(" (moga walczyc ").Append(fight).Append(", bez czym walczyc ")
                      .Append(none).Append(", luk/rzucana bez amunicji ").Append(rangedEmpty).Append(", w ucieczce ").Append(panic).Append(", konno ").Append(mounted)
                      .Append("; do najblizszego wroga (probka ").Append(sampled).Append(") sr. ").Append(sampled > 0 && dMin < float.MaxValue ? (dSum / sampled).ToString("0", Inv) : "-").Append(" m, najmniej ")
                      .Append(dMin < float.MaxValue ? dMin.ToString("0", Inv) : "-").Append(" m) [").Append(det).Append(']');
                }
                Log.Info(sb.Append('.').ToString());
            }
            catch (Exception e) { Stumble("Dump", e); }
        }

        /// <summary>Obie strony maja na polu czlowieka czynnego i nie w ucieczce.</summary>
        private static bool BothSides(Mission m)
        {
            bool pl = false, foe = false;
            foreach (var a in m.Agents)
            {
                if (a == null || !a.IsHuman || !a.IsActive() || a.Team == null || a.IsRunningAway || RoutHold.KnownAnyThread(a)) continue;
                if (a.Team.IsPlayerTeam || a.Team.IsPlayerAlly) pl = true; else foe = true;
                if (pl && foe) return true;
            }
            return false;
        }

        /// <summary>Najblizszy wrog z listy pozycji drugiej strony (zebranej raz w Dump).</summary>
        private static float Nearest(List<Vec3> foes, Vec3 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < foes.Count; i++)
            {
                float d = foes[i].Distance(p);
                if (d < best) best = d;
            }
            return best;
        }

        private void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_err) return;
            _err = true;
            try { Log.Error("BattleStall307e." + where, e); } catch { }
        }

        protected override void OnEndMission()
        {
            try
            {
                if (_fieldKnown && _field || _episodes > 0)
                {
                    var m = Mission;
                    if (_open && m != null) { float len = m.CurrentTime - _stallSince; if (len > _longest) _longest = len; }
                    var c = Settings.Current;
                    Log.Info("Zastoj bitwy (307e): koniec misji | rozstawienie " + (_deployed ? "TAK" : "NIE") + " | klucz Battle Stall Break " + (c != null && c.BattleStallBreak ? "TAK" : "NIE") + ", prog "
                             + (c != null ? c.BattleStallSeconds.ToString(Inv) : "-") + " s | pierwszy cios " + (_contact ? "TAK" : "NIE") + " | zastojow " + _episodes
                             + " (najdluzszy " + _longest.ToString("0", Inv) + " s, linii diagnozy " + _dumps + ") | zeszli z pola bez czym walczyc: strona gracza " + _outPlayer
                             + ", wrog " + _outFoe + " (odmowy RoutHold " + _outRefused + ") | bez RoutKeeper morale 0: strona gracza " + _outMoraleP + ", wrog " + _outMoraleF
                             + " (razem " + _outMorale + ", nieumarli pominieci " + _skipUndead + ") | bohaterowie nie schodza | potkniecia " + _stumbles + ".");
                }
            }
            catch { }
            base.OnEndMission();
        }
    }
}
