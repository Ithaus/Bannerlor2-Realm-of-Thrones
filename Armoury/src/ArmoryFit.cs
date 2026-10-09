using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// STARE ZBROJOWNIE PO 175 - pomiar T2 (projekt 175 rozdz. 6.1 pkt 9 i rozdz. 0 pkt 2; przeglad 175: nie byl zrobiony w zadnym
    /// drzewie). Tylko log, raz na sesje, w pierwszej godzinie gry po wczytaniu (po SkillSinew i NorthHardy CrashScribe, ktore ida
    /// w OnSessionLaunched - kolejnosc modulow nieznana, wiec nie w samym OnSessionLaunched). Wylacznik Army175Measure.
    /// Autotest nie ma bitew z graczem, a linie kwatermistrza i straz SkillLawWard pisza sie tylko przy ekranie zbrojowni i w bitwie
    /// z graczem - dlatego liczymy statycznie, ta sama regula co straz DTE i kwatermistrz (ItemReq.Meets: umiejetnosc >= wymog):
    ///  (a) ludzie x sloty broni, tarczy i amunicji wzorca (sloty 0-3 pierwszego zestawu), dla ktorych zbrojownia nie ma sztuki tego
    ///      samego rodzaju do wymogu po NOWYCH umiejetnosciach - przydzial zachlanny: najslabsi pierwsi biora najlzejsze sztuki
    ///      (sztuka liczona raz, dzielona miedzy oddzialy);
    ///  (b) sztuki w zbrojowni (bron, tarcze, amunicja, pancerz; bez koni i rzedow), ktorych nikt z szeregowych partii nie udzwignie.
    /// Gracz (zbrojownia DTE ArmyArmory) osobno; AI - partie lordow i zalogi AI wedlug krolestw (klucz HorseCensus.KeyOfParty).
    /// (c) zakupy AiGear w dobach po wczytaniu - istniejaca linia "ZakupyAI: dzien" (wobec biegu zatwierdzonego).
    /// Liczby ida do Jeffa (rozdz. 0 pkt 2) i do decyzji o kolejnosci wgrania z K1 (1.9) - bez progu.
    /// </summary>
    internal static class ArmoryFit
    {
        private static bool _pending;

        internal static void Reset() { _pending = false; }

        /// <summary>Z OnSessionLaunched: pomiar w pierwszej godzinie gry.</summary>
        internal static void Arm() { _pending = true; }

        internal static void HourlyOnce()
        {
            if (!_pending) return;
            _pending = false;
            if (!HorseCensus.On) return;
            try { Run(); } catch (Exception e) { Log.Error("ArmoryFit.Run", e); }
        }

        private sealed class Tally
        {
            public long Slots, Missing, MissW, MissS, MissA, Pieces, Unliftable; public int Parties;
            public void Add(Tally o) { Slots += o.Slots; Missing += o.Missing; MissW += o.MissW; MissS += o.MissS; MissA += o.MissA; Pieces += o.Pieces; Unliftable += o.Unliftable; Parties += o.Parties; }
        }

        private static int Kind(ItemObject.ItemTypeEnum t)
        {
            switch (t)
            {
                case ItemObject.ItemTypeEnum.Shield: return 1;
                case ItemObject.ItemTypeEnum.Arrows:
                case ItemObject.ItemTypeEnum.Bolts: return 2;
                case ItemObject.ItemTypeEnum.OneHandedWeapon:
                case ItemObject.ItemTypeEnum.TwoHandedWeapon:
                case ItemObject.ItemTypeEnum.Polearm:
                case ItemObject.ItemTypeEnum.Bow:
                case ItemObject.ItemTypeEnum.Crossbow:
                case ItemObject.ItemTypeEnum.Thrown: return 0;
                default: return -1;
            }
        }

        private sealed class Need { public CharacterObject Ch; public int Men; public int Cap; }

        private static void Count(Tally t, MobileParty mp, Dictionary<ItemObject, int> armory)
        {
            t.Parties++;
            var roster = mp.MemberRoster;
            var men = new List<KeyValuePair<CharacterObject, int>>();
            for (int i = 0; i < roster.Count; i++)
            {
                var el = roster.GetElementCopyAtIndex(i);
                if (el.Character == null || el.Character.IsHero || el.Number <= 0) continue;
                men.Add(new KeyValuePair<CharacterObject, int>(el.Character, el.Number));
            }
            // (a) popyt wedlug rodzaju przedmiotu (ItemType) ze wzorca
            var needs = new Dictionary<ItemObject.ItemTypeEnum, List<Need>>();
            foreach (var m in men)
            {
                Equipment eq = null;
                try { eq = m.Key.FirstBattleEquipment; } catch { }
                if (eq == null) continue;
                for (int s = 0; s < 4; s++)
                {
                    var it = eq[(EquipmentIndex)s].Item;
                    if (it == null || Kind(it.ItemType) < 0) continue;
                    var sk = ItemReq.SkillFor(it);
                    int cap = sk != null ? m.Key.GetSkillValue(sk) : int.MaxValue;
                    List<Need> l;
                    if (!needs.TryGetValue(it.ItemType, out l)) { l = new List<Need>(); needs[it.ItemType] = l; }
                    l.Add(new Need { Ch = m.Key, Men = m.Value, Cap = cap });
                }
            }
            // podaz: sztuki zbrojowni wedlug rodzaju, od najlzejszego wymogu
            var supply = new Dictionary<ItemObject.ItemTypeEnum, List<KeyValuePair<ItemObject, int>>>();
            foreach (var kv in armory)
            {
                if (kv.Key == null || kv.Value <= 0) continue;
                var ty = kv.Key.ItemType;
                if (ty == ItemObject.ItemTypeEnum.Horse || ty == ItemObject.ItemTypeEnum.HorseHarness || !SupplyDemand.Equipmentish(kv.Key)) continue;
                t.Pieces += kv.Value;
                // (b) nikt z szeregowych partii nie udzwignie
                bool any = false;
                foreach (var m in men) if (ItemReq.Meets(m.Key, kv.Key)) { any = true; break; }
                if (!any) t.Unliftable += kv.Value;
                if (Kind(ty) < 0) continue;
                List<KeyValuePair<ItemObject, int>> l;
                if (!supply.TryGetValue(ty, out l)) { l = new List<KeyValuePair<ItemObject, int>>(); supply[ty] = l; }
                l.Add(new KeyValuePair<ItemObject, int>(kv.Key, kv.Value));
            }
            foreach (var kv in needs)
            {
                int kind = Kind(kv.Key);
                var list = kv.Value;
                list.Sort((a, b) => a.Cap.CompareTo(b.Cap));
                List<KeyValuePair<ItemObject, int>> sl;
                supply.TryGetValue(kv.Key, out sl);
                int[] left = null;
                if (sl != null)
                {
                    sl.Sort((a, b) => a.Key.Difficulty.CompareTo(b.Key.Difficulty));
                    left = new int[sl.Count];
                    for (int i = 0; i < sl.Count; i++) left[i] = sl[i].Value;
                }
                foreach (var nd in list)
                {
                    int want = nd.Men;
                    t.Slots += want;
                    if (sl != null)
                        for (int i = 0; i < sl.Count && want > 0; i++)
                        {
                            if (left[i] <= 0 || !ItemReq.Meets(nd.Ch, sl[i].Key)) continue;
                            int take = Math.Min(want, left[i]); left[i] -= take; want -= take;
                        }
                    if (want <= 0) continue;
                    t.Missing += want;
                    if (kind == 1) t.MissS += want; else if (kind == 2) t.MissA += want; else t.MissW += want;
                }
            }
        }

        private static string Text(Tally t)
        {
            return t.Missing + " z " + t.Slots + " ludzi x slotow bez sztuki do wymogu (bron " + t.MissW + ", tarcza " + t.MissS + ", amunicja " + t.MissA
                   + "), sztuk ponad mozliwosci kazdego w partii " + t.Unliftable + " z " + t.Pieces;
        }

        private static void Run()
        {
            Tally player = null;
            try
            {
                var arm = QuartermasterLaw.DteArmory();
                if (arm != null && MobileParty.MainParty != null)
                {
                    var d = new Dictionary<ItemObject, int>();
                    for (int i = 0; i < arm.Count; i++)
                    {
                        var el = arm[i];
                        var it = el.EquipmentElement.Item;
                        if (it == null || el.Amount <= 0) continue;
                        int n; d.TryGetValue(it, out n); d[it] = n + el.Amount;
                    }
                    player = new Tally();
                    Count(player, MobileParty.MainParty, d);
                }
            }
            catch { player = null; }
            var all = AiGear.Armories();
            var byK = new Dictionary<string, Tally>();
            var world = new Tally();
            int noArmory = 0;
            foreach (var mp in MobileParty.All)
            {
                if (mp == null || !mp.IsActive || mp.IsMainParty || !(mp.IsLordParty || mp.IsGarrison) || mp.MemberRoster == null) continue;
                string key = HorseCensus.KeyOfParty(mp.Party);
                if (key == HorseCensus.PlayerClanKey) continue;   // rod gracza - nie AI
                Dictionary<ItemObject, int> a = null;
                if (all != null) all.TryGetValue(mp.Id, out a);
                if (a == null) { noArmory++; continue; }   // bez zbrojowni DTE (np. zaloga po wczytaniu bez 171) - to nie "stara zbrojownia"
                Tally t;
                if (!byK.TryGetValue(key, out t)) { t = new Tally(); byK[key] = t; }
                try { Count(t, mp, a); } catch { }
            }
            foreach (var t in byK.Values) world.Add(t);
            int day = (int)CampaignTime.Now.ToDays;
            Log.Info("Stare zbrojownie (175, T2 - stan po wczytaniu, NOWE umiejetnosci, regula ItemReq jak straz DTE): dzien " + day
                     + " | gracz: " + (player != null ? Text(player) : "brak zbrojowni DTE")
                     + " | AI (partie lordow i zalogi ze zbrojownia DTE " + world.Parties + ", bez zbrojowni " + noArmory + " - pominiete): " + Text(world)
                     + " | wedlug krolestw: balans.log | zakupy AiGear w kolejnych dobach: linie 'ZakupyAI: dzien'.");
            var keys = new List<string>(byK.Keys);
            keys.Sort((x, y) => byK[y].Missing.CompareTo(byK[x].Missing));
            var sb = new StringBuilder("Balans krolestw (175) wedlug krolestw - stare zbrojownie (T2): dzien ");
            sb.Append(day);
            foreach (var k in keys)
            {
                var t = byK[k];
                sb.Append(" | ").Append(k).Append(": brak ").Append(t.Missing).Append('/').Append(t.Slots)
                  .Append(" (bron ").Append(t.MissW).Append(", tarcza ").Append(t.MissS).Append(", amunicja ").Append(t.MissA)
                  .Append("), ponad mozliwosci ").Append(t.Unliftable).Append('/').Append(t.Pieces).Append(" w ").Append(t.Parties).Append(" partiach");
            }
            sb.Append('.');
            Log.Info(sb.ToString());   // Log.TopicOf: prefiks "Balans krolestw (175) wedlug krolestw" -> balans.log
        }
    }
}
