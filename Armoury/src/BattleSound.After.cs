using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using MapEvent = TaleWorlds.CampaignSystem.MapEvents.MapEvent;
using TerrainType = TaleWorlds.Core.TerrainType;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P2: PO BITWIE (Jeff 01:05-01:06: "jeki rannych po bitwie i niech beda to rozne glosy", "odglosy ptakow i krukow po bitwie").
    ///  - Ciala: OnRemoved (zgon / nokaut) - miejsce do kolejki (najwyzej 256 na takt, lista najwyzej 300 ostatnich).
    ///  - Po bitwie = Mission.MissionEnded przy trwajacej misji (gracz chodzi po polu). Jeki (BattleWoundedAfter): od 4 s po koncu, przy cialach NIEPRZYTOMNYCH
    ///    do 60 m od sluchacza, kazde cialo najwyzej 2 razy, glos (rola z manifestu) inny niz w trzech ostatnich jekach, najwyzej 2 naraz; odstep 3-6 s rosnie
    ///    z czasem (x (1 + t/60)) - cichna; po 300 s koniec. Klipy jk (3D 5-60 m).
    ///  - Ptaki (BattleRavensAfter): przed bitwa raz - ptaki wedlug terenu (las - ptaki lasu, brzeg - mewy, reszta - dalekie wrony) ok. 25 m od sluchacza;
    ///    po bitwie od 8 s - kruki zlatuja sie nad ciala blisko sluchacza (do 120 m; skrzydla / wrony, potem co 15-30 s kruk, wrony, klotnia), po 75 s raz
    ///    "ptaki wracaja" (las albo dalekie wrony); najwyzej 12 na misje. Klipy pt (3D 10-150 m).
    ///  - Ranny kon (BattleHorseSounds): kon pada w bitwie do 60 m od sluchacza - kwik (ko_wounded), najwyzej raz na 12 s.
    /// </summary>
    internal static partial class BattleSound
    {
        private struct Body { internal Vec3 Pos; internal bool Unconscious, Mount; internal float At; internal int Moans; }

        private const int BodiesMax = 300, BirdsMax = 12;
        private const float MoanRange = 60f, MoanMin = 5f, MoanMax = 60f, MoanEndSec = 300f, BirdMin = 10f, BirdMax = 150f, BirdNear = 120f;

        private static readonly List<Body> _bodyQ = new List<Body>();
        private static readonly List<Body> _bodies = new List<Body>();
        private static readonly List<string> _moanRoles = new List<string>();
        private static float _afterAt = -1f, _nextMoan = -1f, _nextBird = -1f;
        private static Stamp _horseHurt, _moanReal, _birdReal;   // odstepy takze w czasie rzeczywistym (przyspieszenie czasu - bez lawiny)
        private static bool _birdsBefore, _birdsBack;
        private static int _birdStep;
        private static int _cMoan, _cBird, _cBirdBefore, _cHorseHurt;
        private static readonly HashSet<string> _moanVoices = new HashSet<string>();

        private static void ResetAfter()
        {
            lock (_q) { _bodyQ.Clear(); }
            _bodies.Clear(); _moanRoles.Clear(); _moanVoices.Clear();
            _afterAt = _nextMoan = _nextBird = -1f; _horseHurt = default(Stamp); _moanReal = default(Stamp); _birdReal = default(Stamp);
            _birdsBefore = _birdsBack = false; _birdStep = 0;
            _cMoan = _cBird = _cBirdBefore = _cHorseHurt = 0;
        }

        private static void Bodies(Mission m, Settings c)
        {
            _drainBodies.Clear();
            lock (_q)
            {
                _drainBodies.AddRange(_bodyQ);
                _bodyQ.Clear();
            }
            for (int i = 0; i < _drainBodies.Count; i++)
            {
                var b = _drainBodies[i];
                if (b.Mount)
                {
                    // ranny / zabity kon w bitwie blisko sluchacza
                    if (c.BattleHorseSounds && !m.MissionEnded && _horseHurt.Ready(12f) && LisDist(b.Pos) <= 60f)
                    {
                        var hc = Pick("ko", "wounded");
                        if (hc != null && Play(m, hc, GAmb, b.Pos + new Vec3(0f, 0f, 1f), 10f, 120f, true, null, null, "ranny kon") != null) { _horseHurt.Hit(); _cHorseHurt++; }
                    }
                    continue;
                }
                if (_bodies.Count >= BodiesMax) _bodies.RemoveAt(0);
                _bodies.Add(b);
            }
            _drainBodies.Clear();
        }

        private static readonly List<Body> _drainBodies = new List<Body>();

        private static void BirdsBefore(Mission m, Settings c)
        {
            if (_birdsBefore || !c.BattleRavensAfter || _now - _beganAt < 2f) return;
            _birdsBefore = true;
            if (_siege) return;
            string key = "distant";
            try
            {
                var me = MapEvent.PlayerMapEvent;
                if (me != null)
                {
                    var tt = me.EventTerrainType;
                    if (tt == TerrainType.Forest) key = "forest";
                    else if (tt == TerrainType.Beach || tt == TerrainType.Lake || tt == TerrainType.Water || tt == TerrainType.CoastalSea) key = "gulls";
                }
            }
            catch { }
            var bc = Pick("pt", key) ?? Pick("pt", "distant");
            if (bc != null && Play(m, bc, GAmb, Around(25f), BirdMin, BirdMax, true, null, null, "ptaki przed bitwa") != null) _cBirdBefore++;
        }

        private static Vec3 Around(float r)
        {
            float a = (float)(_rng.NextDouble() * Math.PI * 2.0);
            return _lis + new Vec3((float)Math.Cos(a) * r, (float)Math.Sin(a) * r, 3f);
        }

        private static void After(Mission m, Settings c)
        {
            if (!m.MissionEnded) return;
            if (_afterAt < 0f)
            {
                _afterAt = _now;
                _nextMoan = _now + 4f;
                _nextBird = _now + 8f;
            }
            float t = _now - _afterAt;
            if (c.BattleWoundedAfter && t <= MoanEndSec && _now >= _nextMoan && _moanReal.Ready(3f)) Moan(m, t);
            if (c.BattleRavensAfter && _now >= _nextBird && _cBird < BirdsMax && _birdReal.Ready(15f)) Ravens(m, t);
        }

        private static void Moan(Mission m, float t)
        {
            _nextMoan = _now + (3f + (float)_rng.NextDouble() * 3f) * (1f + t / 60f);
            _moanReal.Hit();
            int live = 0;
            for (int i = 0; i < _live.Count; i++) if (_live[i].Tag == "jek") live++;
            if (live >= 2) return;
            // cialo nieprzytomne blisko sluchacza, losowe z najwyzej 6 najblizszych
            int pick = -1, seen = 0;
            for (int i = _bodies.Count - 1; i >= 0; i--)
            {
                var b = _bodies[i];
                if (!b.Unconscious || b.Moans >= 2 || LisDist(b.Pos) > MoanRange) continue;
                seen++;
                if (_rng.Next(seen) == 0) pick = i;
                if (seen >= 6) break;
            }
            if (pick < 0) return;
            var mc = PickMoan(_moanRoles);
            if (mc == null) return;
            var body = _bodies[pick];
            if (Play(m, mc, GVoice, body.Pos + new Vec3(0f, 0f, 0.4f), MoanMin, MoanMax, true, null, null, "jek") == null) return;
            body.Moans++;
            _bodies[pick] = body;
            _cMoan++;
            _moanVoices.Add(mc.Role);
            _moanRoles.Add(mc.Role);
            while (_moanRoles.Count > 3) _moanRoles.RemoveAt(0);
        }

        private static void Ravens(Mission m, float t)
        {
            _nextBird = _now + 15f + (float)_rng.NextDouble() * 15f;
            _birdReal.Hit();
            // miejsce: cialo blisko sluchacza (do 120 m), inaczej 40 m od sluchacza
            Vec3 pos = Around(40f);
            for (int i = _bodies.Count - 1; i >= 0; i--)
            {
                if (LisDist(_bodies[i].Pos) <= BirdNear) { pos = _bodies[i].Pos + new Vec3(0f, 0f, 2f); break; }
            }
            string key;
            if (_birdStep == 0) key = _rng.Next(2) == 0 ? "wings" : "crows";
            else if (!_birdsBack && t >= 75f) { key = "__back"; }
            else { int r = _rng.Next(3); key = r == 0 ? "raven" : r == 1 ? "crows" : "squabble"; }
            Clip bc;
            if (key == "__back")
            {
                _birdsBack = true;
                string back = "distant";
                try { var me = MapEvent.PlayerMapEvent; if (me != null && me.EventTerrainType == TerrainType.Forest) back = "forest"; } catch { }
                bc = Pick("pt", back);
                pos = Around(30f);
            }
            else bc = Pick("pt", key) ?? Pick("pt", "crows");
            _birdStep++;
            if (bc != null && Play(m, bc, GAmb, pos, BirdMin, BirdMax, true, null, null, "ptaki po bitwie") != null) _cBird++;
        }

        private static void AppendAfter(StringBuilder sb, Settings c)
        {
            int unc = 0, dead = 0;
            for (int i = 0; i < _bodies.Count; i++) { if (_bodies[i].Unconscious) unc++; else dead++; }
            sb.Append(" | po bitwie ").Append(_afterAt >= 0f ? (_now - _afterAt).ToString("0", Inv) + " s" : "-").Append(": jeki ").Append(c != null && c.BattleWoundedAfter ? "TAK" : "NIE")
              .Append(' ').Append(_cMoan).Append(" (glosow ").Append(_moanVoices.Count).Append("), ptaki ").Append(c != null && c.BattleRavensAfter ? "TAK" : "NIE").Append(' ')
              .Append(_cBird).Append(" (przed bitwa ").Append(_cBirdBefore).Append("), cial zapamietanych ").Append(_bodies.Count).Append(" (nieprzytomnych ").Append(unc)
              .Append(", zabitych ").Append(dead).Append(')');
        }
    }
}
