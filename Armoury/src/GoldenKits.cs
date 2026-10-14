using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// PLAN 1.6 (Jeff 09.10, status "zdecyd."; inwentarz B4; CHANGELOG 14.10 "NIE WLACZONE - warunek nie jest w kodzie"): kusznicy Zlotej Kompanii
    /// (golden_crossbowman t3, golden_veteran_crossbowman t4, golden_master_crossbowman t5) dostaja od CrashScribe (Army175Gear.GoldenBows) trzy zestawy
    /// bojowe - kusza / luk dwukrzywy / luk cisowy (ADWD: 1/3 kusze, 1/3 luki dwukrzywe, reszta cisowe). W bitwie DTE losuje zestaw, a SkillsDecide 175.G
    /// bierze bron dystansowa z wylosowanego zestawu. Dotad potrzeba sprzetu AI czytala tylko 1. zestaw (ch.Equipment = kusza + belty): AiGear.Deficit /
    /// NeedBuckets (zakupy brakow, cwiczenia, linia "Pokrycie"), GarrisonArmory.AddNeed (nadwyzki MenPurse, przenoszenie sprzetu z ludzmi) i dopasowanie
    /// "co nosza ludzie", MenUpgrade.Buckets ("lepsze za swoje") - AI nigdy nie kupiloby lukow ani strzal, a luki z lupu szlyby jako nadwyzka na sprzedaz.
    /// JEDNA REGULA: dla tych trzech jednostek przy wlaczonym Army175GoldenBows potrzeba liczy sie ze WSZYSTKICH zestawow bojowych, ludzie po rowno
    /// (waga 1/n; reszta z dzielenia do pierwszych zestawow - pierwszy to kusza). Inne jednostki ROT z kilkoma zestawami - bez zmian (1. zestaw, jak
    /// dotad): inaczej zmiana zakupow calego swiata (warunek z wpisu 14.10). Off (Army175GoldenBows = false) = 449d453: sam ch.Equipment, wszyscy ludzie.
    /// Pomiar: linia "Zlota Kompania (PLAN 1.6)" raz na dobe przy wlaczonym kluczu (tylko log).
    /// </summary>
    internal static class GoldenKits
    {
        /// <summary>Te same id co CS Army175Gear.GoldenXbow i SkillsDecide.GoldenXbow (punkt styku z CrashScribe).</summary>
        internal static readonly string[] Ids = { "golden_crossbowman", "golden_veteran_crossbowman", "golden_master_crossbowman" };

        /// <summary>Czy potrzeba tej jednostki liczy sie z wielu zestawow (klucz wlaczony i jedna z trzech jednostek).</summary>
        internal static bool On(CharacterObject ch)
        {
            var s = Settings.Current;
            if (s == null || !s.Army175GoldenBows || ch == null) return false;
            string id = ch.StringId;
            return id != null && id.StartsWith("golden_", StringComparison.Ordinal) && Array.IndexOf(Ids, id) >= 0;
        }

        /// <summary>Ile zestawow liczy sie do potrzeby: 1 (jak dotad - ch.Equipment) albo liczba zestawow bojowych kusznika ZK przy wlaczonym kluczu.</summary>
        internal static int Count(CharacterObject ch)
        {
            if (!On(ch)) return 1;
            int n = 0;
            try { foreach (var e in ch.BattleEquipments) if (e != null) n++; } catch { n = 0; }
            return n > 1 ? n : 1;
        }

        /// <summary>Zestaw q z kn (kn z Count): przy kn = 1 - ch.Equipment (jak dotad), inaczej q-ty zestaw bojowy.</summary>
        internal static Equipment Kit(CharacterObject ch, int q, int kn)
        {
            try
            {
                if (kn <= 1) return ch.Equipment;
                int i = 0;
                foreach (var e in ch.BattleEquipments)
                {
                    if (e == null) continue;
                    if (i == q) return e;
                    i++;
                }
            }
            catch { }
            return null;
        }

        /// <summary>Ludzi na zestaw q z kn: po rowno, reszta z dzielenia do pierwszych zestawow (suma = number).</summary>
        internal static int Men(int number, int q, int kn)
        {
            if (kn <= 1) return number;
            return number / kn + (q < number % kn ? 1 : 0);
        }

        // ------------------------------------------------------------ pomiar (tylko log)
        /// <summary>
        /// Linia doby "Zlota Kompania (PLAN 1.6)" - tylko przy wlaczonym kluczu: zestawy trzech jednostek (ile, w tym z lukiem), kusznicy ZK w partiach AI,
        /// potrzeba tych partii wedlug typu (luki, kusze, strzaly, belty - z jednej miary Deficit, cala partia), brak po zbrojowni i zakupy brakow, oraz
        /// pokrycie lukow i strzal (1 - brak / potrzeba). Partie rodu gracza pominiete (o jego ludziach decyduje gracz).
        /// </summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || !s.Army175GoldenBows) return;
            int day = (int)CampaignTime.Now.ToDays;
            var kits = new List<string>();
            foreach (var id in Ids)
            {
                CharacterObject co = null;
                try { co = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<CharacterObject>(id); } catch { }
                if (co == null) { kits.Add(id + " brak"); continue; }
                int n = 0, bows = 0;
                try
                {
                    foreach (var e in co.BattleEquipments)
                    {
                        if (e == null) continue;
                        n++;
                        for (int sl = 0; sl < 4; sl++) { var it = e[(EquipmentIndex)sl].Item; if (it != null && it.ItemType == ItemObject.ItemTypeEnum.Bow) { bows++; break; } }
                    }
                }
                catch { }
                kits.Add(id + " " + n + " (z lukiem " + bows + ")");
            }
            var armories = AiGear.Armories();
            int parties = 0, men = 0;
            long needBow = 0, needXbow = 0, needArrow = 0, needBolt = 0, lackBow = 0, lackXbow = 0, lackArrow = 0, lackBolt = 0;
            var player = Clan.PlayerClan;
            foreach (var mp in MobileParty.All)
            {
                if (mp == null || !mp.IsActive || mp.IsMainParty) continue;
                if (player != null && mp.ActualClan == player) continue;
                var roster = mp.MemberRoster;
                if (roster == null) continue;
                int here = 0;
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    if (el.Character != null && el.Number > 0 && On(el.Character)) here += el.Number;
                }
                if (here <= 0) continue;
                parties++; men += here;
                Dictionary<ItemObject, int> arm = null;
                if (armories != null) armories.TryGetValue(mp.Id, out arm);
                var needOut = new Dictionary<int, int>();
                var lack = AiGear.Deficit(mp, arm, needOut);
                foreach (var kv in needOut)
                {
                    var t = (ItemObject.ItemTypeEnum)(kv.Key / 10);
                    if (t == ItemObject.ItemTypeEnum.Bow) needBow += kv.Value;
                    else if (t == ItemObject.ItemTypeEnum.Crossbow) needXbow += kv.Value;
                    else if (t == ItemObject.ItemTypeEnum.Arrows) needArrow += kv.Value;
                    else if (t == ItemObject.ItemTypeEnum.Bolts) needBolt += kv.Value;
                }
                foreach (var kv in lack)
                {
                    if (kv.Value <= 0) continue;
                    var t = (ItemObject.ItemTypeEnum)(kv.Key / 10);
                    if (t == ItemObject.ItemTypeEnum.Bow) lackBow += kv.Value;
                    else if (t == ItemObject.ItemTypeEnum.Crossbow) lackXbow += kv.Value;
                    else if (t == ItemObject.ItemTypeEnum.Arrows) lackArrow += kv.Value;
                    else if (t == ItemObject.ItemTypeEnum.Bolts) lackBolt += kv.Value;
                }
            }
            Func<long, long, string> cover = (need, lack) => need > 0 ? (100.0 * (need - lack) / need).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%" : "-";
            Log.Info("Zlota Kompania (PLAN 1.6): dzien " + day + " | zestawy: " + string.Join(", ", kits.ToArray())
                     + " | partie AI z kusznikami ZK " + parties + ", kusznikow ZK " + men
                     + " | potrzeba tych partii (cala partia, jedna miara Deficit): luki " + needBow + ", kusze " + needXbow + ", strzaly " + needArrow + ", belty " + needBolt
                     + " | brak po zbrojowni: luki " + lackBow + ", kusze " + lackXbow + ", strzaly " + lackArrow + ", belty " + lackBolt
                     + " | pokrycie lukow " + cover(needBow, lackBow) + ", strzal " + cover(needArrow, lackArrow) + ", kusz " + cover(needXbow, lackXbow) + ", beltow " + cover(needBolt, lackBolt) + ".");
        }
    }
}
