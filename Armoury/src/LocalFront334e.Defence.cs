using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 334e - OBRONA PUSTYCH ZIEM (klucz LocalDefence334e, rozdz. 5) - przez ocene Defender wsi pod rabunkiem (ScorePost). Oblezenia twierdz zostaja przy OB5 / 326c.
    /// Napastnik = strona atakujaca rabunku + jego sojusznicy, ktorzy dojda przed koncem rabunku. T = pozostaly czas rabunku (wzor gry). Kandydaci = jednostki AI
    /// strony wsi (krolestwo i koalicja 274; bez gracza, armii w odpoczynku / nocnym obozie, obozow oblezniczych, wodzow chronionych 335 - ci decyduja jak dotad), ktore dojda w T, po
    /// czasie dojscia. Najblizsi az razem >= 1.2 x napastnik - przydzieleni (ocena x1); reszta x0.5 (nie leca wszyscy); nikt nie zdazy / nie uzbiera - nikt nie
    /// wraca (x0.25 - ocena gry zostaje, ale ponizej innych celow). Przydzial trwa, dopoki suma przydzielonych, ktorzy dalej zdaza, >= 1.0 x napastnik; jednostka
    /// zmienia przydzial najwyzej raz na dobe. Klucz wylaczony: tylko "nie zdazy" x0.25 (wariant A).
    /// </summary>
    internal static partial class LocalFront334e
    {
        private sealed class DefRec { public int Hour = -1; public double Att, Sum; public float T; public readonly HashSet<MobileParty> Assigned = new HashSet<MobileParty>(); public bool None; public int Said = -1; }
        private static readonly Dictionary<Settlement, DefRec> _def = new Dictionary<Settlement, DefRec>();
        private static readonly Dictionary<MobileParty, KeyValuePair<int, Settlement>> _defOf = new Dictionary<MobileParty, KeyValuePair<int, Settlement>>();   // doba, osada
        private static int _dDefAssign, _dDefNone, _dDefSwitch, _dDefLines;

        private static void ResetDefence() { _def.Clear(); _defOf.Clear(); ZeroDefenceDay(); }
        private static void ZeroDefenceDay() { _dDefAssign = _dDefNone = _dDefSwitch = _dDefLines = 0; }

        /// <summary>Doba (pod _lk): przydzialy z poprzednich dob i nieaktywne druzyny, wsie bez rabunku.</summary>
        private static void DefenceDayClean()
        {
            int today = Today;
            var dead = new List<MobileParty>();
            foreach (var kv in _defOf) if (kv.Key == null || !kv.Key.IsActive || kv.Value.Key < today) dead.Add(kv.Key);
            foreach (var p in dead) _defOf.Remove(p);
            var deadV = new List<Settlement>();
            foreach (var kv in _def) { var me = kv.Key != null && kv.Key.Party != null ? kv.Key.Party.MapEvent : null; if (me == null || !me.IsRaid) deadV.Add(kv.Key); }
            foreach (var v in deadV) _def.Remove(v);
        }

        private static bool Busy(MobileParty mp)
        {
            try
            {
                if (mp.IsMainParty || WithPlayer(mp) || mp.BesiegerCamp != null || mp.MapEvent != null) return true;
                if (mp.Army != null && ArmyRestDay.Resting(mp.Army)) return true;
                if (NightRest.FieldCampHolds(mp) || NightRest.IsCamping(mp)) return true;
            }
            catch { }
            return false;
        }

        /// <summary>Mnoznik oceny Defender wsi pod rabunkiem dla druzyny p. Wolajacy trzyma _lk.</summary>
        private static float DefenceMult(MobileParty p, Settlement v)
        {
            var me = v.Party != null ? v.Party.MapEvent : null;
            if (me == null || !me.IsRaid || me.AttackerSide == null) return 1f;
            int men = 0; double att = 0;
            foreach (var mep in me.AttackerSide.Parties) { if (mep == null || mep.Party == null) continue; att += PStr(mep.Party); men += mep.Party.NumberOfHealthyMembers; }
            float T = RaidDays(v.SettlementHitPoints, men);
            float our = OurDays(p, v);
            if (our < 0f || our > T) return 0.25f;                     // nie zdazymy (A)
            if (!DefenceOn) return 1f;
            var vf = v.MapFaction;
            DefRec r;
            if (!_def.TryGetValue(v, out r)) { r = new DefRec(); _def[v] = r; }
            if (r.Hour != NowH)
            {
                r.Hour = NowH; r.T = T;
                var lead = me.AttackerSide.LeaderParty != null ? me.AttackerSide.LeaderParty.MobileParty : null;
                float first; double ally;
                double extra = lead != null ? ZFor(v, vf, null, T, out first, out ally) : 0;   // wrogowie wsi (sojusznicy napastnika) w drodze
                r.Att = att + extra;
                // przydzial trwa, dopoki przydzieleni, ktorzy dalej zdaza, maja >= 1.0 x napastnik
                double keep = 0;
                var drop = new List<MobileParty>();
                foreach (var a in r.Assigned)
                {
                    var u = a != null && a.IsActive ? LiveUnit(a) : null;
                    float d = u != null ? DaysTo(u, v) : -1f;
                    if (u == null || d < 0f || d > T || Busy(a)) drop.Add(a); else keep += u.Str;
                }
                foreach (var a in drop) r.Assigned.Remove(a);
                if (r.Assigned.Count > 0 && keep >= DefDrop * r.Att) { r.Sum = keep; r.None = false; }
                else
                {
                    var cands = new List<KeyValuePair<float, float>>();
                    var who = new List<MobileParty>();
                    var l = ListOf(v);
                    var pairs = new List<KeyValuePair<float, MobileParty>>();
                    foreach (var e in l.E)
                    {
                        var lp = e.U.Lead;
                        if (lp == null || !lp.IsActive || !SameSide(vf, lp.MapFaction) || Busy(lp)) continue;
                        try { if (LordEscort.Guard335Busy(lp)) continue; } catch { }
                        var u = LiveUnit(lp);
                        if (u == null) continue;
                        float d = DaysTo(u, v);
                        if (d < 0f || d > T) continue;
                        KeyValuePair<int, Settlement> was;
                        if (_defOf.TryGetValue(lp, out was) && was.Key == Today && was.Value != v) continue;   // raz na dobe
                        pairs.Add(new KeyValuePair<float, MobileParty>(d, lp));
                    }
                    pairs.Sort((a, b) => a.Key.CompareTo(b.Key));
                    foreach (var kv in pairs) { cands.Add(new KeyValuePair<float, float>(kv.Key, LiveUnit(kv.Value).Str)); who.Add(kv.Value); }
                    double sum;
                    int n = PickDefenders(cands, r.Att, T, out sum);
                    var old = new HashSet<MobileParty>(r.Assigned);
                    r.Assigned.Clear();
                    for (int i = 0; i < n; i++)
                    {
                        var mp = who[i];
                        r.Assigned.Add(mp);
                        KeyValuePair<int, Settlement> was;
                        if (_defOf.TryGetValue(mp, out was) && was.Key == Today && was.Value != v) _dDefSwitch++;
                        _defOf[mp] = new KeyValuePair<int, Settlement>(Today, v);
                    }
                    r.Sum = sum; r.None = n == 0;
                    if (n > 0) _dDefAssign++; else _dDefNone++;
                    bool changed = !old.SetEquals(r.Assigned);
                    if (changed && S != null && S.LogEnabled && _dDefLines < 40)
                    {
                        _dDefLines++;
                        Log.Info(Tag + "obrona | dzien " + Today + " | " + v.StringId + " (" + Nm(v) + ", " + Id(vf) + ") | napastnik " + F0(r.Att) + " | T_obrony " + F0(T * 24f) + " h | przydzieleni " + n
                                 + " (" + F0(sum) + ", stosunek " + F2(r.Att > 0 ? sum / r.Att : 0) + ") | zostaja " + Math.Max(0, cands.Count - n) + ".");
                    }
                }
            }
            if (r.None) return 0.25f;
            return r.Assigned.Contains(p) || (p.Army != null && r.Assigned.Contains(p.Army.LeaderParty)) ? 1f : 0.5f;
        }
    }
}
