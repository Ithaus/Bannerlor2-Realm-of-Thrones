using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// ZUZYCIE I NAPRAWY SPRZETU AI (wpis 85; Jeff 05.10: "jak AI naprawia sprzet zepsuty i ile to trwa?" - nie naprawialo,
    /// bo nic sie nie zuzywalo). Zbrojownia DTE lorda AI to lista "przedmiot: ile" bez stanu - obok prowadzimy wlasny zapis:
    /// ile sztuk danego przedmiotu jest obitych i w jakim stanie (modyfikator gry). Reszta = sprawna.
    ///  - Po bitwie (wygranej albo przegranej, kto przezyl): BattleWear - TroopWearPercent sztuk w uzyciu dostaje zuzycie walki;
    ///    lup, ktory DTE dolozyl do zbrojowni (przybytek od ostatniego spisu po bitwie), wchodzi obity jak lup gracza.
    ///  - Zakupy AiGear i komplety rekrutow - sprawne (przybytek bez bitwy).
    ///  - W miescie, raz dziennie (wejscie + dzienny tick postoju): kowale miasta naprawiaja tyle, ile zdaza (rece rzemieslnikow
    ///    x udzial platnerzy i miecznikow x 18 h pracy / 0.6 h na sztuke), placi sakiewka ludzi do kasy miasta. Wraki zostaja.
    ///  - Sprzedaz nadwyzek (MenPurse) bierze najgorsze obite sztuki ze swoim stanem; sprawne ida jako sprawne.
    /// </summary>
    internal static class AiWear
    {
        private static bool On { get { var s = Settings.Current; return s != null && s.AiWearEnabled; } }

        // partia -> przedmiot -> modyfikator -> ile obitych
        private static readonly Dictionary<string, Dictionary<string, Dictionary<string, int>>> _worn = new Dictionary<string, Dictionary<string, Dictionary<string, int>>>();
        // partia -> przedmiot -> ile bylo w zbrojowni przy ostatnim spisie
        private static readonly Dictionary<string, Dictionary<string, int>> _known = new Dictionary<string, Dictionary<string, int>>();
        private static readonly HashSet<string> _battleSince = new HashSet<string>();
        private static readonly Dictionary<string, int> _lastMend = new Dictionary<string, int>();
        private static int _dayLoot, _dayWorn, _dayMended, _dayPaid, _dayStamp = -1;

        internal static void Reset() { _worn.Clear(); _known.Clear(); _battleSince.Clear(); _lastMend.Clear(); _dayLoot = _dayWorn = _dayMended = _dayPaid = 0; _dayStamp = -1; }

        internal static string Export()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var p in _worn)
                foreach (var it in p.Value)
                    foreach (var m in it.Value)
                        if (m.Value > 0) sb.Append(p.Key).Append(',').Append(it.Key).Append(',').Append(m.Key).Append(',').Append(m.Value).Append(';');
            return sb.ToString();
        }

        internal static void Import(string s)
        {
            _worn.Clear(); _known.Clear(); _battleSince.Clear();
            if (string.IsNullOrEmpty(s)) return;
            foreach (var rec in s.Split(';'))
            {
                var a = rec.Split(','); int n;
                if (a.Length != 4 || !int.TryParse(a[3], out n) || n <= 0) continue;
                AddWorn(a[0], a[1], a[2], n);
            }
        }

        private static void AddWorn(string p, string item, string mod, int n)
        {
            Dictionary<string, Dictionary<string, int>> byItem;
            if (!_worn.TryGetValue(p, out byItem)) _worn[p] = byItem = new Dictionary<string, Dictionary<string, int>>();
            Dictionary<string, int> byMod;
            if (!byItem.TryGetValue(item, out byMod)) byItem[item] = byMod = new Dictionary<string, int>();
            int v; byMod.TryGetValue(mod, out v); v += n;
            if (v > 0) byMod[mod] = v; else byMod.Remove(mod);
            if (byMod.Count == 0) byItem.Remove(item);
            if (byItem.Count == 0) _worn.Remove(p);
        }

        private static int WornOf(string p, string item)
        {
            Dictionary<string, Dictionary<string, int>> byItem; Dictionary<string, int> byMod;
            if (!_worn.TryGetValue(p, out byItem) || !byItem.TryGetValue(item, out byMod)) return 0;
            int n = 0; foreach (var v in byMod.Values) n += v; return n;
        }

        private static ItemModifier Mod(string id) { try { return MBObjectManager.Instance.GetObject<ItemModifier>(id); } catch { return null; } }

        private static Dictionary<ItemObject, int> Armory(MobileParty mp)
        {
            var d = AiGear.Armories(); Dictionary<ItemObject, int> a;
            return d != null && mp != null && d.TryGetValue(mp.Id, out a) ? a : null;
        }

        /// <summary>Spis zbrojowni: przybytek po bitwie = lup (obity), bez bitwy = sprawny; ubytki przycinaja zapis obitych.</summary>
        private static void Sync(MobileParty mp)
        {
            var arm = Armory(mp);
            if (arm == null) return;
            string p = mp.StringId;
            Dictionary<string, int> known;
            bool first = !_known.TryGetValue(p, out known);
            if (first) _known[p] = known = new Dictionary<string, int>();
            bool battle = _battleSince.Remove(p);
            var seen = new HashSet<string>();
            foreach (var kv in arm)
            {
                if (kv.Key == null || !SupplyDemand.Equipmentish(kv.Key) || ArmouryBehavior.NoWear(kv.Key) || MenPurse.HorseKind(kv.Key)) continue;
                string id = kv.Key.StringId; seen.Add(id);
                int was; known.TryGetValue(id, out was);
                int now = Math.Max(0, kv.Value);
                if (!first && battle && now > was)
                    for (int k = 0; k < now - was; k++)
                    {
                        var m = ArmouryBehavior.PickWornModifier(kv.Key);
                        if (m != null) { AddWorn(p, id, m.StringId, 1); _dayLoot++; }
                    }
                // ubytek (polegli, sprzedaz): obitych nie wiecej niz sztuk
                int w = WornOf(p, id);
                if (w > now) TrimWorn(p, id, w - now);
                known[id] = now;
            }
            foreach (var id in known.Keys.ToList()) if (!seen.Contains(id)) { known.Remove(id); int w = WornOf(p, id); if (w > 0) TrimWorn(p, id, w); }
        }

        private static void TrimWorn(string p, string item, int n)
        {
            Dictionary<string, Dictionary<string, int>> byItem; Dictionary<string, int> byMod;
            if (!_worn.TryGetValue(p, out byItem) || !byItem.TryGetValue(item, out byMod)) return;
            foreach (var m in byMod.Keys.ToList()) { if (n <= 0) break; int c = Math.Min(n, byMod[m]); AddWorn(p, item, m, -c); n -= c; }
        }

        /// <summary>wpis 89: kupiona sztuka ze stanem z polki - obita zostaje obita (dotad AI kupowalo tanio zuzyte i mialo je jako nowe).</summary>
        internal static void NoteBought(MobileParty mp, EquipmentElement el, int n)
        {
            if (!On || mp == null || el.Item == null) return;
            NoteSound(mp, el.Item, n);
            var m = el.ItemModifier;
            if (m != null && m.PriceMultiplier < 1f && !MenPurse.HorseKind(el.Item) && !ArmouryBehavior.NoWear(el.Item)) AddWorn(mp.StringId, el.Item.StringId, m.StringId, n);
        }

        internal static void Forget(MobileParty mp)
        {
            if (mp == null) return;
            string p = mp.StringId; _worn.Remove(p); _known.Remove(p); _battleSince.Remove(p); _lastMend.Remove(p);
        }

        /// <summary>AiGear dolozyl kupione sztuki - sprawne, spis od razu.</summary>
        internal static void NoteSound(MobileParty mp, ItemObject it, int n)
        {
            if (!On || mp == null || it == null) return;
            Dictionary<string, int> known;
            if (!_known.TryGetValue(mp.StringId, out known)) return;   // brak spisu - pierwszy Sync uzna calosc za sprawna
            int was; known.TryGetValue(it.StringId, out was); known[it.StringId] = was + n;
        }

        // ------------------------------------------------------------ bitwa
        internal static void OnMapEventEnded(MapEvent me)
        {
            try
            {
                if (!On || me == null) return;
                Day();
                var s = Settings.Current;
                float share = MBMath.ClampFloat(s.TroopWearPercent, 0f, 100f) / 100f;
                foreach (var side in new[] { me.AttackerSide, me.DefenderSide })
                {
                    if (side == null) continue;
                    foreach (var mep in side.Parties)
                    {
                        var mp = mep != null && mep.Party != null ? mep.Party.MobileParty : null;
                        if (mp == null || mp.IsMainParty || !mp.IsLordParty || !mp.IsActive) continue;
                        _battleSince.Add(mp.StringId);   // przybytek do nastepnego spisu = lup (DTE doklada go w tym samym zdarzeniu)
                        if (share <= 0f) continue;
                        // zuzycie walki: czesc sztuk W UZYCIU (do liczby ludzi) - sprawne staja sie obite
                        var arm = Armory(mp); if (arm == null) continue;
                        var need = AiGear.NeedBuckets(mp);
                        foreach (var kv in arm.ToList())
                        {
                            var it = kv.Key;
                            if (it == null || kv.Value <= 0 || !SupplyDemand.Equipmentish(it) || ArmouryBehavior.NoWear(it) || MenPurse.HorseKind(it)) continue;
                            int nd; need.TryGetValue(AiGear.Bucket(it), out nd);
                            int inUse = Math.Min(nd, kv.Value);
                            int sound = kv.Value - WornOf(mp.StringId, it.StringId);
                            int hits = Math.Min(sound, (int)Math.Floor(inUse * share + MBRandom.RandomFloat));
                            for (int k = 0; k < hits; k++)
                            {
                                var m = ArmouryBehavior.PickWornModifier(it);
                                if (m != null) { AddWorn(mp.StringId, it.StringId, m.StringId, 1); _dayWorn++; }
                            }
                        }
                    }
                }
            }
            catch (Exception e) { Log.Error("AiWear.OnMapEventEnded", e); }
        }

        // ------------------------------------------------------------ stan przy sprzedazy nadwyzek
        /// <summary>Sztuka idzie do kupca: najgorsza obita, jesli jest; inaczej sprawna (null).</summary>
        internal static ItemModifier TakeCondition(MobileParty mp, ItemObject it)
        {
            if (!On || mp == null || it == null) return ArmouryBehavior.PickWornModifier(it);
            Sync(mp);
            Dictionary<string, Dictionary<string, int>> byItem; Dictionary<string, int> byMod;
            if (!_worn.TryGetValue(mp.StringId, out byItem) || !byItem.TryGetValue(it.StringId, out byMod) || byMod.Count == 0) { Known(mp, it, -1); return null; }
            string worst = null; float wm = float.MaxValue;
            foreach (var id in byMod.Keys) { var m = Mod(id); float pm = m != null ? m.PriceMultiplier : 1f; if (pm < wm) { wm = pm; worst = id; } }
            AddWorn(mp.StringId, it.StringId, worst, -1);
            Known(mp, it, -1);
            return Mod(worst);
        }

        private static void Known(MobileParty mp, ItemObject it, int d)
        {
            Dictionary<string, int> known;
            if (!_known.TryGetValue(mp.StringId, out known)) return;
            int v; known.TryGetValue(it.StringId, out v); known[it.StringId] = Math.Max(0, v + d);
        }

        // ------------------------------------------------------------ naprawy w miescie
        private static int UnitCost(ItemObject it, ItemModifier m) { return Math.Max(1, (int)(it.Value * (1f - (m != null ? m.PriceMultiplier : 1f)) * 0.25f)); }
        private static bool Mendable(ItemModifier m) { return m != null && m.PriceMultiplier < 1f && m.PriceMultiplier >= 0.1f; }

        internal static int OutstandingCost(MobileParty mp)
        {
            if (!On || mp == null) return 0;
            int sum = 0;
            Dictionary<string, Dictionary<string, int>> byItem;
            if (!_worn.TryGetValue(mp.StringId, out byItem)) return 0;
            foreach (var it in byItem)
            {
                var item = MBObjectManager.Instance.GetObject<ItemObject>(it.Key); if (item == null) continue;
                foreach (var m in it.Value) { var mod = Mod(m.Key); if (Mendable(mod)) sum += UnitCost(item, mod) * m.Value; }
            }
            return sum;
        }

        /// <summary>Dzien postoju w miescie: kowale naprawiaja, ile zdaza i na ile starcza sakiewki ludzi.</summary>
        internal static void MendInTown(MobileParty mp, Settlement st)
        {
            try
            {
                if (!On || mp == null || st == null || !st.IsTown || st.Town == null || mp.IsMainParty) return;
                int today = (int)CampaignTime.Now.ToDays, last;
                if (_lastMend.TryGetValue(mp.StringId, out last) && last == today) return;   // dzien pracy kowali raz na dobe
                _lastMend[mp.StringId] = today;
                Day();
                Sync(mp);
                Dictionary<string, Dictionary<string, int>> byItem;
                if (!_worn.TryGetValue(mp.StringId, out byItem)) return;
                var s = Settings.Current;
                // wpis 91: z WSPOLNEJ puli kowali miasta - lordowie i gracz dziela te same rece
                float perPiece = Math.Max(0.05f, s.MendLootHoursPerPiece);
                int cap = (int)(SmithHours.Available(st.Town) / perPiece);
                var jobs = new List<KeyValuePair<string, string>>();   // przedmiot, modyfikator - najgorsze najpierw
                foreach (var it in byItem) foreach (var m in it.Value) if (Mendable(Mod(m.Key))) for (int k = 0; k < m.Value; k++) jobs.Add(new KeyValuePair<string, string>(it.Key, m.Key));
                jobs.Sort((a, b) => { var ma = Mod(a.Value); var mb = Mod(b.Value); return (ma != null ? ma.PriceMultiplier : 1f).CompareTo(mb != null ? mb.PriceMultiplier : 1f); });
                int done = 0, paid = 0;
                foreach (var j in jobs)
                {
                    if (done >= cap) break;
                    var item = MBObjectManager.Instance.GetObject<ItemObject>(j.Key); if (item == null) continue;
                    int unit = UnitCost(item, Mod(j.Value));
                    if (MenPurse.Get(mp) < unit) break;
                    MenPurse.Take(mp, unit); st.Town.ChangeGold(unit);
                    AddWorn(mp.StringId, j.Key, j.Value, -1);
                    done++; paid += unit;
                }
                SmithHours.Use(st.Town, done * perPiece);
                _dayMended += done; _dayPaid += paid;
            }
            catch (Exception e) { Log.Error("AiWear.MendInTown", e); }
        }

        private static void Day()
        {
            int d = (int)CampaignTime.Now.ToDays;
            if (_dayStamp == d) return;
            if (_dayStamp >= 0 && (_dayLoot + _dayWorn + _dayMended) > 0)
            {
                int parties = _worn.Count, pieces = 0; foreach (var p in _worn.Values) foreach (var it in p.Values) foreach (var v in it.Values) pieces += v;
                Log.Info("Zuzycie AI: dzien " + _dayStamp + " - lup obity " + _dayLoot + " szt., zuzyte w bitwach " + _dayWorn + ", naprawione w miastach " + _dayMended
                         + " za " + _dayPaid + " z sakiewek ludzi; obitych razem " + pieces + " szt. w " + parties + " partiach.");
            }
            _dayLoot = _dayWorn = _dayMended = _dayPaid = 0; _dayStamp = d;
        }
    }
}
