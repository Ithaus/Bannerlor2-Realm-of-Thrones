using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// noc 25.10 P2: BEBNY MARSZU (klucz BattleDrumsMarch; krytyk K7). Szyk piechoty albo strzelcow pieszych od 20 ludzi bije w beben, gdy idzie marszem:
    /// przy MarchToContactEnabled - MarchToContact.IsMarching(f) (cap marszu P1), przy wylaczonym marszu - rozkaz Advance / Move; do tego szyk sie rusza
    /// (stoi dluzej niz 4 s - cisza), wrog dalej niz 45 m, bez szarzy. Styl wedlug kultury (DrumStyle; Dothrakowie, konnica, Inni, bandyci - bez bebna).
    /// Najwyzej 2 bebny na druzyne - szyki najblizsze sluchacza do 300 m; oblezenie - tylko atakujacy. Takt = klip petli 8 s ponawiany po koncu poprzedniego
    /// (czas misji ORAZ co najmniej 0,9 dlugosci w czasie rzeczywistym - bez lawiny przy przyspieszeniu); w jednym marszu ten sam klip (rowny rytm).
    /// Koniec warunku -> Release od razu (krytyk: bez wyciszania). Beben idzie za szykiem (srodek szyku co 0,25 s).
    /// </summary>
    internal static partial class BattleSound
    {
        private const int DrumsPerTeam = 2, MinMenDrum = 20;
        private const float DrumMin = 20f, DrumMax = 300f, DrumEnemyNear = 45f, DrumStillSec = 4f;

        private sealed class Drum
        {
            internal Formation F;
            internal Snd Cur;
            internal Clip Clip;
            internal bool Active;
            internal float StillSince = -1f;
        }

        private static readonly Dictionary<Formation, Drum> _drums = new Dictionary<Formation, Drum>();
        private static readonly List<Formation> _drumPick = new List<Formation>();
        private static readonly List<float> _drumDist = new List<float>();
        private static readonly List<Formation> _drumWant = new List<Formation>();
        private static int _cDrumStart, _cDrumBeat, _cDrumStop;

        private static void ResetDrums()
        {
            _drums.Clear(); _drumPick.Clear(); _drumDist.Clear(); _drumWant.Clear();
            _cDrumStart = _cDrumBeat = _cDrumStop = 0;
        }

        private static bool Marching(Formation f, Settings c)
        {
            var oe = f.GetReadonlyMovementOrderReference().OrderEnum;
            if (oe == MovementOrder.MovementOrderEnum.Charge || oe == MovementOrder.MovementOrderEnum.ChargeToTarget) return false;
            if (c.MarchToContactEnabled) return MarchToContact.IsMarching(f);
            return oe == MovementOrder.MovementOrderEnum.Advance || oe == MovementOrder.MovementOrderEnum.Move;
        }

        private static void Drums(Mission m, Settings c)
        {
            _drumWant.Clear();
            bool on = c.BattleDrumsMarch && m.IsDeploymentFinished && !m.MissionEnded;
            if (on)
            {
                var teams = m.Teams;
                for (int i = 0; teams != null && i < teams.Count; i++)
                {
                    var t = teams[i];
                    if (!MissionTeams.Valid(t)) continue;
                    if (_siege && !ReferenceEquals(t, m.AttackerTeam) && t.Side != BattleSideEnum.Attacker) continue;
                    _drumPick.Clear(); _drumDist.Clear();
                    var fl = t.FormationsIncludingSpecialAndEmpty;
                    for (int j = 0; fl != null && j < fl.Count; j++)
                    {
                        var f = fl[j];
                        if (f == null || f.CountOfUnits < MinMenDrum) continue;
                        if (!DrumCandidate(f, c)) continue;
                        float d = LisDist(FormPos(f));
                        if (d > DrumMax * LoudScale(c) + FarExtra) continue;
                        // najblizsze sluchacza: wstawianie do krotkiej listy
                        int k = 0;
                        while (k < _drumDist.Count && _drumDist[k] <= d) k++;
                        _drumDist.Insert(k, d); _drumPick.Insert(k, f);
                    }
                    for (int k = 0; k < _drumPick.Count && k < DrumsPerTeam; k++) _drumWant.Add(_drumPick[k]);
                }
            }
            // stop bebnow poza lista
            foreach (var kv in _drums)
            {
                var d = kv.Value;
                if (!d.Active || _drumWant.Contains(d.F)) continue;
                d.Active = false;
                if (d.Cur != null) { Cut(d.Cur); d.Cur = null; }
                _cDrumStop++;
            }
            // takt bebnow z listy
            for (int i = 0; i < _drumWant.Count; i++)
            {
                var f = _drumWant[i];
                Drum d;
                if (!_drums.TryGetValue(f, out d)) { d = new Drum { F = f }; _drums[f] = d; }
                if (d.Cur != null && !_live.Contains(d.Cur)) d.Cur = null;
                if (d.Cur != null)
                {
                    float em = _now - d.Cur.T0;
                    double er = Real - d.Cur.R0;
                    if (em < d.Cur.C.Dur - 0.1f || er < 0.9 * d.Cur.C.Dur) continue;   // takt trwa
                }
                if (!d.Active || d.Clip == null)
                {
                    BasicCharacterObject ch;
                    string cul = FormCul(f, out ch);
                    d.Clip = Pick("dr", DrumStyle(cul));
                    if (d.Clip == null) continue;
                }
                var s = Play(m, d.Clip, GDrum, FormPos(f), DrumMin, DrumMax, true, null, f, "beben");
                if (s == null) continue;
                d.Cur = s;
                _cDrumBeat++;
                if (!d.Active) { d.Active = true; _cDrumStart++; }
            }
        }

        private static bool DrumCandidate(Formation f, Settings c)
        {
            try
            {
                var q = f.QuerySystem;
                if (q == null || q.IsCavalryFormationReadOnly || q.IsRangedCavalryFormationReadOnly) return false;
            }
            catch { return false; }
            if (!Marching(f, c)) { Still(f, false); return false; }
            if (EnemyDist(f) < DrumEnemyNear) return false;
            BasicCharacterObject ch;
            string cul = FormCul(f, out ch);
            if (Silent(ch, cul) || DrumStyle(cul) == null) return false;
            // stoi dluzej niz 4 s - cisza
            bool still = f.CachedCurrentVelocity.LengthSquared < 0.09f;
            return !Still(f, still);
        }

        /// <summary>true = szyk stoi dluzej niz DrumStillSec.</summary>
        private static bool Still(Formation f, bool still)
        {
            Drum d;
            if (!_drums.TryGetValue(f, out d)) { if (!still) return false; d = new Drum { F = f }; _drums[f] = d; }
            if (!still) { d.StillSince = -1f; return false; }
            if (d.StillSince < 0f) d.StillSince = _now;
            return _now - d.StillSince > DrumStillSec;
        }

        private static void DrumEnded(Snd s)
        {
            if (s.Group != GDrum || s.FollowF == null) return;
            Drum d;
            if (_drums.TryGetValue(s.FollowF, out d) && ReferenceEquals(d.Cur, s)) d.Cur = null;
        }

        private static void AppendDrums(StringBuilder sb, Settings c)
        {
            sb.Append(" | bebny ").Append(c != null && c.BattleDrumsMarch ? "TAK" : "NIE").Append(": start ").Append(_cDrumStart).Append(", takty ").Append(_cDrumBeat)
              .Append(", stop ").Append(_cDrumStop).Append(" (marsz ").Append(c != null && c.MarchToContactEnabled ? "MarchToContact" : "rozkaz Advance/Move").Append(')');
        }
    }
}
