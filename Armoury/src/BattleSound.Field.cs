using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Campaign = TaleWorlds.CampaignSystem.Campaign;
using CampaignTime = TaleWorlds.CampaignSystem.CampaignTime;
using MapEvent = TaleWorlds.CampaignSystem.MapEvents.MapEvent;
using TerrainType = TaleWorlds.Core.TerrainType;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P2: TLO POLA ("jesli zostanie czas" - kazde osobnym kluczem, tylko gdy blisko sluchacza, najwyzej 4 zdarzenia tla naraz).
    ///  - Wiatr (BattleWindAndRain): bitwy na otwartym polu (nie oblezenie, nie wypad) - petla 2D wedlug sniegu sceny / zimy (Westeros: WesterosClimate.SkySeason),
    ///    terenu (gory - wiatr gorski, step / pustynia - stepowy) albo lagodny; deszcz - gdy scena ma deszcz (Scene.GetRainDensity &gt; 0, jak SandboxApplyWeatherEffectsModel):
    ///    ponad 0,6 ulewa, inaczej lekki. Petla = klip 12 s ponawiany po koncu (czas misji i rzeczywisty), cisza 1,5 s po starcie misji.
    ///  - Konie (BattleHorseSounds): szyk konny od 10 ludzi do 120 m od sluchacza, nie w szarzy - parskanie / rzenie co 12-25 s (druzyna co 8 s); przy szarzy
    ///    konnicy (sygnal BattleCries) tetent za szykiem (ko_charge, raz na szyk na 30 s); ranny kon - BattleSound.After.cs.
    ///  - Szczek broni (BattleClashOfArms): szyki wrecz od 15 ludzi w zwarciu (najblizszy wrog do 20 m) - petla zgielku (sz_melee) w polowie miedzy szykami,
    ///    najwyzej 2 miejsca naraz, najblizsze sluchacza do 150 m, miejsca blizej niz 30 m - jedno.
    ///  - Ogien (SiegeFireSounds): oblezenie - po rozstawieniu petla ognia obozu oblegajacych (za ich szykami, 60 m od obroncow) i plonacego miasta (przy
    ///    obroncach); gra, gdy sluchacz jest w zasiegu (do 200 m - 154, dotad 250).
    /// </summary>
    internal static partial class BattleSound
    {
        private const float HorseRange = 120f, HorseTeamGap = 8f, ClashNear = 20f, ClashRange = 150f;
        // 154 (odleglosci 3D min/max m, x glosnosc; pelna glosnosc tylko przy zrodle): konie (parskanie, rzenie, ranny kon) 4/120 (dotad 10/300 i 10/120),
        // tetent 8/250 (dotad 10/300), szczek 10/150 (bez zmian), ogien 10/200 (dotad 15/250)
        private const float HorseMin = 4f, HorseMax = 120f, GallopMin = 8f, GallopMax = 250f, ClashMin = 10f, FireMin = 10f, FireMax = 200f;

        private sealed class Loop
        {
            internal string Code, Key, Tag;
            internal Snd Cur;
            internal bool Is3d;
            internal Vec3 Pos;
            internal float Min, Max;
            internal bool Want;               // szczek: miejsce nadal w zwarciu (ocena co 1 s) - takt petli miedzy ocenami
        }

        private static Loop _wind, _rain;
        private static readonly List<Loop> _fires = new List<Loop>();
        private static readonly Loop[] _clash = new Loop[2];
        private static readonly Dictionary<Formation, float> _horseNext = new Dictionary<Formation, float>();
        private static readonly Dictionary<Formation, Stamp> _gallop = new Dictionary<Formation, Stamp>();
        private static readonly Dictionary<Team, Stamp> _horseTeam = new Dictionary<Team, Stamp>();
        private static readonly List<Vec3> _clashPts = new List<Vec3>();
        private static readonly List<float> _clashD = new List<float>();
        private static bool _windTried, _firesTried;
        private static float _clashAcc;
        private static int _cWind, _cRain, _cHorse, _cGallop, _cClash, _cFire;
        private static string _windKey, _rainKey;

        private static void ResetField()
        {
            _wind = _rain = null; _fires.Clear(); _clash[0] = _clash[1] = null;
            _horseNext.Clear(); _gallop.Clear(); _horseTeam.Clear(); _clashPts.Clear(); _clashD.Clear();
            _windTried = _firesTried = false; _clashAcc = 0f;
            _cWind = _cRain = _cHorse = _cGallop = _cClash = _cFire = 0;
            _windKey = _rainKey = null;
        }

        /// <summary>Petla: nastepny klip po koncu poprzedniego (Pool zwalnia - Cur null), tylko w zasiegu sluchacza. true = zagral nowy klip.</summary>
        private static bool LoopTick(Mission m, Loop l, bool want)
        {
            if (l == null) return false;
            if (l.Cur != null && !_live.Contains(l.Cur)) l.Cur = null;
            if (!want) { if (l.Cur != null) { Cut(l.Cur); l.Cur = null; } return false; }
            if (l.Cur != null)
            {
                float em = _now - l.Cur.T0;
                double er = Real - l.Cur.R0;
                if (em < l.Cur.C.Dur - 0.1f || er < 0.9 * l.Cur.C.Dur) return false;
            }
            var clip = Pick(l.Code, l.Key);
            if (clip == null) return false;
            var s = Play(m, clip, GAmb, l.Is3d ? l.Pos : Vec3.Zero, l.Min, l.Max, l.Is3d, null, null, l.Tag);
            if (s == null) return false;
            l.Cur = s;
            return true;
        }

        private static void Weather(Mission m, Settings c)
        {
            bool on = c.BattleWindAndRain && _now - _beganAt >= 1.5f;
            if (on && !_windTried)
            {
                _windTried = true;
                float snow = 0f, rain = 0f;
                try { var sc = m.Scene; if (sc != null) { snow = sc.GetSnowDensity(); rain = sc.GetRainDensity(); } } catch { }
                if (!_siege && !m.IsSallyOutBattle)
                {
                    string k = "gentle";
                    TerrainType tt = TerrainType.Plain;
                    bool terr = false;
                    try { var me = MapEvent.PlayerMapEvent; if (me != null) { tt = me.EventTerrainType; terr = true; } } catch { }
                    bool winter = snow > 0f || (terr && tt == TerrainType.Snow);
                    if (!winter && Campaign.Current != null)
                    {
                        try { var t = CampaignTime.Now; winter = WesterosClimate.SkySeason(ref t) == CampaignTime.Seasons.Winter; } catch { }
                    }
                    if (winter) k = "winter";
                    else if (terr && (tt == TerrainType.Mountain || tt == TerrainType.Canyon || tt == TerrainType.Cliff)) k = "mountain";
                    else if (terr && (tt == TerrainType.Steppe || tt == TerrainType.Desert || tt == TerrainType.Dune)) k = "steppe";
                    if (Has("wi", k) || Has("wi", "gentle"))
                    {
                        if (!Has("wi", k)) k = "gentle";
                        _windKey = k;
                        _wind = new Loop { Code = "wi", Key = k, Tag = "wiatr", Is3d = false };
                    }
                }
                if (rain > 0f)
                {
                    string k = rain > 0.6f ? "heavy" : "light";
                    if (!Has("rn", k)) k = Has("rn", "light") ? "light" : Has("rn", "heavy") ? "heavy" : null;
                    if (k != null) { _rainKey = k + " " + rain.ToString("0.00", Inv); _rain = new Loop { Code = "rn", Key = k, Tag = "deszcz", Is3d = false }; }
                }
            }
            if (LoopTick(m, _wind, on)) _cWind++;
            if (LoopTick(m, _rain, on)) _cRain++;
        }

        // ---- konie

        private static void Horses(Mission m, Settings c)
        {
            if (!c.BattleHorseSounds || !m.IsDeploymentFinished || m.MissionEnded) return;
            var teams = m.Teams;
            for (int i = 0; teams != null && i < teams.Count; i++)
            {
                var t = teams[i];
                if (!MissionTeams.Valid(t)) continue;
                Stamp tl;
                if (_horseTeam.TryGetValue(t, out tl) && !tl.Ready(HorseTeamGap)) continue;
                var fl = t.FormationsIncludingSpecialAndEmpty;
                for (int j = 0; fl != null && j < fl.Count; j++)
                {
                    var f = fl[j];
                    if (f == null || f.CountOfUnits < 10 || !Mounted(f)) continue;
                    var oe = f.GetReadonlyMovementOrderReference().OrderEnum;
                    if (oe == MovementOrder.MovementOrderEnum.Charge || oe == MovementOrder.MovementOrderEnum.ChargeToTarget) continue;
                    float nx;
                    if (!_horseNext.TryGetValue(f, out nx)) { _horseNext[f] = _now + 3f + (float)_rng.NextDouble() * 10f; continue; }
                    if (_now < nx) continue;
                    _horseNext[f] = _now + 12f + (float)_rng.NextDouble() * 13f;
                    Vec3 p = FormPos(f);
                    if (LisDist(p) > HorseRange) continue;
                    var hc = _rng.Next(2) == 0 ? Pick("ko", "snort") : Pick("ko", "neigh");
                    if (hc == null) hc = Pick("ko", "snort") ?? Pick("ko", "neigh");
                    if (hc != null && Play(m, hc, GAmb, p, HorseMin, HorseMax, true, null, f, "konie") != null) { _cHorse++; tl.Hit(); _horseTeam[t] = tl; break; }
                }
            }
        }

        private static bool Mounted(Formation f)
        {
            try { var q = f.QuerySystem; return q != null && (q.IsCavalryFormationReadOnly || q.IsRangedCavalryFormationReadOnly || q.CavalryUnitRatioReadOnly + q.RangedCavalryUnitRatioReadOnly >= 0.5f); }
            catch { return false; }
        }

        /// <summary>Szarza konnicy (z OnChargeStarted - tylko kolejka; dzwiek w DrainCharges przez ten sam Play w Tick).</summary>
        private static readonly List<Formation> _gallopQ = new List<Formation>();

        private static void ChargeHorse(Formation f, Settings c)
        {
            if (!c.BattleHorseSounds || f.CountOfUnits < 10 || !Mounted(f)) return;
            Stamp s;
            _gallop.TryGetValue(f, out s);
            if (!s.Ready(30f)) return;
            s.Hit();
            _gallop[f] = s;
            if (_gallopQ.Count < 16) _gallopQ.Add(f);
        }

        private static void Gallops(Mission m)
        {
            for (int i = 0; i < _gallopQ.Count; i++)
            {
                var f = _gallopQ[i];
                if (f == null || f.CountOfUnits <= 0) continue;
                var hc = Pick("ko", "charge");
                if (hc != null && Play(m, hc, GAmb, FormPos(f), GallopMin, GallopMax, true, null, f, "tetent") != null) _cGallop++;
            }
            _gallopQ.Clear();
        }

        // ---- szczek broni

        private static void Clash(Mission m, Settings c, float dt)
        {
            bool on = c.BattleClashOfArms && m.IsDeploymentFinished && !m.MissionEnded;
            _clashAcc += dt;
            if (on && _clashAcc < 1f) { KeepClash(m); return; }
            _clashAcc = 0f;
            _clashPts.Clear(); _clashD.Clear();
            if (on)
            {
                var teams = m.Teams;
                for (int i = 0; teams != null && i < teams.Count; i++)
                {
                    var t = teams[i];
                    if (!MissionTeams.Valid(t)) continue;
                    var fl = t.FormationsIncludingSpecialAndEmpty;
                    for (int j = 0; fl != null && j < fl.Count; j++)
                    {
                        var f = fl[j];
                        if (f == null || f.CountOfUnits < 15) continue;
                        if (EnemyDist(f) > ClashNear) continue;
                        FormationQuerySystem eq = null;
                        try { var q = f.QuerySystem; if (q == null || q.IsRangedFormationReadOnly) continue; eq = f.CachedClosestEnemyFormation; } catch { continue; }
                        if (eq == null || eq.Formation == null) continue;
                        Vec3 a = FormPos(f), b = FormPos(eq.Formation);
                        Vec3 mid = (a + b) * 0.5f;
                        float d = LisDist(mid);
                        if (d > ClashRange * LoudScale(c) + FarExtra) continue;
                        bool dup = false;
                        for (int k = 0; k < _clashPts.Count; k++) if (_clashPts[k].DistanceSquared(mid) < 30f * 30f) { dup = true; break; }
                        if (dup) continue;
                        int ins = 0;
                        while (ins < _clashD.Count && _clashD[ins] <= d) ins++;
                        _clashD.Insert(ins, d); _clashPts.Insert(ins, mid);
                    }
                }
            }
            for (int s = 0; s < _clash.Length; s++)
            {
                bool want = s < _clashPts.Count;
                if (want)
                {
                    if (_clash[s] == null) _clash[s] = new Loop { Code = "sz", Key = "melee", Tag = "szczek", Is3d = true, Min = ClashMin, Max = ClashRange };
                    _clash[s].Pos = _clashPts[s];
                    if (_clash[s].Cur != null && _live.Contains(_clash[s].Cur)) { _clash[s].Cur.Pos = _clashPts[s]; try { _clash[s].Cur.Ev.SetPosition(_clashPts[s]); } catch { } }
                }
                if (_clash[s] != null) _clash[s].Want = want;
                if (_clash[s] != null && LoopTick(m, _clash[s], want)) _cClash++;
            }
        }

        private static void KeepClash(Mission m)
        {
            for (int s = 0; s < _clash.Length; s++)
                if (_clash[s] != null && _clash[s].Want && LoopTick(m, _clash[s], true)) _cClash++;
        }

        // ---- ogien oblezenia

        private static void Fires(Mission m, Settings c)
        {
            bool on = c.SiegeFireSounds && _siege && m.IsDeploymentFinished;
            if (on && !_firesTried)
            {
                _firesTried = true;
                Vec2 att, def;
                if (TeamCenter(m.AttackerTeam, out att) && TeamCenter(m.DefenderTeam, out def))
                {
                    Vec2 dir = att - def;
                    float len = dir.Length;
                    if (len > 1f) dir = dir * (1f / len); else dir = new Vec2(1f, 0f);
                    Vec2 camp = att + dir * 60f;
                    float z = _lis.z;
                    try { var sc = m.Scene; if (sc != null) z = sc.GetGroundHeightAtPosition(new Vec3(camp.x, camp.y, 0f)); } catch { }
                    if (Has("fi", "camp")) _fires.Add(new Loop { Code = "fi", Key = "camp", Tag = "ogien obozu", Is3d = true, Pos = new Vec3(camp.x, camp.y, z + 2f), Min = FireMin, Max = FireMax });
                    float zd = _lis.z;
                    try { var sc = m.Scene; if (sc != null) zd = sc.GetGroundHeightAtPosition(new Vec3(def.x, def.y, 0f)); } catch { }
                    string town = Has("fi", "palisade") ? "palisade" : Has("fi", "tents") ? "tents" : null;
                    if (town != null) _fires.Add(new Loop { Code = "fi", Key = town, Tag = "ogien miasta", Is3d = true, Pos = new Vec3(def.x, def.y, zd + 4f), Min = FireMin, Max = FireMax });
                }
            }
            for (int i = 0; i < _fires.Count; i++)
            {
                var l = _fires[i];
                bool near = on && LisDist(l.Pos) <= l.Max * LoudScale(c) + FarExtra;
                if (LoopTick(m, l, near)) _cFire++;
            }
        }

        private static bool TeamCenter(Team t, out Vec2 c)
        {
            c = Vec2.Zero;
            if (!MissionTeams.Valid(t)) return false;
            int n = 0;
            Vec2 sum = Vec2.Zero;
            var fl = t.FormationsIncludingSpecialAndEmpty;
            for (int j = 0; fl != null && j < fl.Count; j++)
            {
                var f = fl[j];
                if (f == null || f.CountOfUnits <= 0) continue;
                sum += f.CachedAveragePosition * f.CountOfUnits;
                n += f.CountOfUnits;
            }
            if (n == 0) return false;
            c = sum * (1f / n);
            return true;
        }

        private static void AppendField(StringBuilder sb, Settings c)
        {
            sb.Append(" | tlo: wiatr ").Append(c != null && c.BattleWindAndRain ? "TAK" : "NIE").Append(' ').Append(_cWind).Append(" (").Append(_windKey ?? "-").Append("), deszcz ")
              .Append(_cRain).Append(" (").Append(_rainKey ?? "-").Append("), konie ").Append(c != null && c.BattleHorseSounds ? "TAK" : "NIE").Append(' ').Append(_cHorse)
              .Append(" (tetent ").Append(_cGallop).Append(", ranny kon ").Append(_cHorseHurt).Append("), szczek ").Append(c != null && c.BattleClashOfArms ? "TAK" : "NIE").Append(' ')
              .Append(_cClash).Append(", ogien ").Append(c != null && c.SiegeFireSounds ? "TAK" : "NIE").Append(' ').Append(_cFire).Append(" (miejsc ").Append(_fires.Count).Append(')');
        }
    }
}
