using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 561 - MARTWE I MAGICZNE WIERZCHOWCE (Jeff 15.11, zrzut targu Winterfell: "Ivory Unicorn" 1 za 19219 i "Wight Horse" 2 za 856 na sprzedaz):
    /// "na martwych koniach moga jezdzic tylko martwi; kucyki jednorozce nie da sie kupic, to zwierzeta magiczne - wywal je z armii ludzi; konie zmarlych
    /// tylko w armii zmarlych". Klucz NoMagicMountsForLiving.
    ///  - jednorozec (unicorn1/2/3, kultura skagosi, item_category horse) - nikt go nie trzyma: ani polka, ani zywy, ani umarly;
    ///  - kon umarlych (wight_horse, kultura wights / walkers - ta sama regula co HorseWind.WightHorse) - tylko partie i bohaterowie Innych (Undead).
    /// Obie rodziny mialy item_category horse i Monster.horse, wiec Stables.IsPlainMount bral je za zwykle konie (Stajnia AI, zapas, sprzedaz nadwyzek,
    /// pula weteranow, Prologue.Dragons). Teraz IsPlainMount je pomija (Forbidden).
    /// (1) Szablony jednostek NIE-bohaterow (pamiec sesji, przy kazdym wczytaniu, z LegendaryLaw.OnSession przed TroopFit): jednorozec, a u zywych takze
    ///     kon umarlych, ustepuje gorskiemu koniowi (t2_sturgia_horse "Mountain Garron", od tieru 6 t3_sturgia_horse "Mountain Destrier"; zapas: konie
    ///     Polnocy t2/t3_battania_horse). Skagos to gorzysta wyspa - rase moze zmienic Jeff (stale Repl*).
    /// (2) Czystka swiata - wczytanie i doba (bezpieczne chwile na mapie, nic w misji), do tego polka osady przy wejsciu gracza: polki osad, tabory partii,
    ///     zbrojownie DTE (AI i gracza), pula weteranow HorseFate, slot konia bohaterow (zywy na zakazanym dostaje najlepszy IsPlainMount z taboru swojej
    ///     partii, inaczej pusto - nic z niczego).
    /// UJSCIE, nie przeniesienie: nikt zywy nie moze ich trzymac. Jednorozec "uwolniony", kon umarlych u zywych "rozpada sie" (jak MeltDeadLoot w CS).
    /// Bez zaplaty - wartosc tylko w logu; sztuki w ksiedze koni (HorseBook.Note561), zeby linia E6/0 nie pokazywala straty bez autora.
    /// Potkniecia liczone per obiekt (bez wylacznika globalnego). Wozy w drodze (TradeCarts) i stajnia piechoty gracza (FootStable) - nie ruszane:
    /// sztuka wyladuje na polce albo wroci do ladunku i zejdzie przy nastepnej czystce.
    /// Ta sama lista id co CrashScribe Mends.IsDeadGear (wight) - trzymac w zgodzie.
    /// </summary>
    internal sealed class DeadMounts561 : CampaignBehaviorBase
    {
        private const string ReplLow = "t2_sturgia_horse", ReplHigh = "t3_sturgia_horse";
        private const string ReplLowAlt = "t2_battania_horse", ReplHighAlt = "t3_battania_horse";

        private static int _stumbles;
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        internal static bool On { get { var s = Settings.Current; return s != null && s.NoMagicMountsForLiving; } }

        /// <summary>Jednorozec ROT (unicorn1 Ebony, unicorn2 Ivory, unicorn3 Unicorn).</summary>
        internal static bool IsUnicorn(ItemObject it)
        {
            if (it == null || it.ItemType != ItemObject.ItemTypeEnum.Horse) return false;
            var id = it.StringId;
            return id != null && id.StartsWith("unicorn", StringComparison.Ordinal);
        }

        /// <summary>Kon umarlych: id z "wight" albo kultura wights / walkers (jak HorseWind.WightHorse).</summary>
        internal static bool IsWightMount(ItemObject it)
        {
            if (it == null || it.ItemType != ItemObject.ItemTypeEnum.Horse) return false;
            var id = it.StringId ?? "";
            if (id.IndexOf("wight", StringComparison.Ordinal) >= 0) return true;
            string cul = "";
            try { if (it.Culture != null) cul = it.Culture.StringId ?? ""; } catch { }
            return cul.IndexOf("wight", StringComparison.Ordinal) >= 0 || cul.IndexOf("walker", StringComparison.Ordinal) >= 0;
        }

        /// <summary>Stables.IsPlainMount: ani jednorozec, ani kon umarlych nie jest zwyklym koniem (klucz wlaczony).</summary>
        internal static bool Forbidden(ItemObject it)
        {
            return On && (IsUnicorn(it) || IsWightMount(it));
        }

        /// <summary>Czy partia moze trzymac sztuke: jednorozec nigdy, kon umarlych tylko Inni.</summary>
        internal static bool MayHold(MobileParty mp, ItemObject it)
        {
            if (IsUnicorn(it)) return false;
            if (IsWightMount(it)) return mp != null && Undead.Party(mp);
            return true;
        }

        /// <summary>Czy postac moze dosiasc: jednorozec nigdy, kon umarlych tylko umarly.</summary>
        internal static bool MayRide(BasicCharacterObject co, ItemObject it)
        {
            if (IsUnicorn(it)) return false;
            if (IsWightMount(it)) return co != null && Undead.Character(co);
            return true;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, s =>
            {
                _stumbles = 0; _errSites.Clear();
                try { SweepWorld("wczytanie"); } catch (Exception e) { Log.Error("DeadMounts561.SweepWorld", e); }
            });
            // R561/4: czystka dobowa w pomiarze profilu doby (E8/0, jak LegendaryLaw) - tylko log
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, DayProfile.Dx("DeadMounts561.SweepWorld", () =>
            {
                try { SweepWorld("dzien"); } catch (Exception e) { Log.Error("DeadMounts561.SweepWorld", e); }
            }));
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, (mp, st, h) =>
            {
                try { if (mp != null && mp == MobileParty.MainParty) SweepShelfOnEntry(st); } catch (Exception e) { Stumble("Entry", e); }
            });
            // R561/2: gracz czeka w osadzie (menu oczekiwania) - partia AI moze tam sprzedac lup po Innych; co godzine czystka tylko tej jednej polki,
            // zeby zakazany kon nie stal w oknie handlu do konca doby. Godzina mapy (czas stoi w misji i przy otwartym oknie handlu) - bezpieczna chwila.
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, DayProfile.Hr("DeadMounts561.PlayerShelf", () =>
            {
                try
                {
                    var mp = MobileParty.MainParty;
                    var st = mp != null ? mp.CurrentSettlement : null;
                    if (st != null) SweepShelfOnEntry(st, "gracz w osadzie");
                }
                catch (Exception e) { Stumble("Hourly", e); }
            }));
        }

        public override void SyncData(IDataStore dataStore) { }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errSites.Add(where)) Log.Error("DeadMounts561." + where, e);
        }

        // ================================================================ (1) SZABLONY
        private static ItemObject Find(string id)
        {
            try { return MBObjectManager.Instance.GetObject<ItemObject>(id); } catch { return null; }
        }

        /// <summary>Wolane z LegendaryLaw.OnSession po SweepTemplates, przed TroopFit.Run (wymogi skilli z koncowego szablonu).</summary>
        internal static void SweepTemplates()
        {
            if (!On) return;
            var all = MBObjectManager.Instance.GetObjectTypeList<CharacterObject>();
            if (all == null) return;
            var low = Find(ReplLow) ?? Find(ReplLowAlt);
            var high = Find(ReplHigh) ?? Find(ReplHighAlt) ?? low;
            if (low == null) low = high;
            int swapped = 0, emptied = 0;
            var troops = new List<string>();
            foreach (var ch in all)
            {
                if (ch == null || ch.IsHero) continue;
                try
                {
                    bool touched = false;
                    for (int pass = 0; pass < 2; pass++)
                        foreach (var eq in pass == 0 ? ch.BattleEquipments : ch.CivilianEquipments)
                        {
                            if (eq == null) continue;
                            var it = eq[EquipmentIndex.Horse].Item;
                            if (it == null || MayRide(ch, it)) continue;
                            var repl = ch.Tier >= 6 ? high : low;
                            eq[EquipmentIndex.Horse] = repl != null ? new EquipmentElement(repl) : new EquipmentElement(null);
                            if (repl != null) swapped++; else emptied++;
                            touched = true;
                        }
                    if (touched && troops.Count < 20) troops.Add(ch.StringId);
                }
                catch (Exception e) { Stumble("Template", e); }
            }
            if (swapped + emptied > 0)
                Log.Info("Zakazane wierzchowce (561): szablony - " + swapped + " jednorozcow / koni umarlych u zywych podmienionych na "
                         + (low != null ? low.StringId : "-") + " / " + (high != null ? high.StringId : "-") + " (tier 6+), pustych slotow " + emptied
                         + " | jednostki: " + string.Join(", ", troops.ToArray()) + ".");
        }

        // ================================================================ (2) CZYSTKA SWIATA
        private sealed class Tally
        {
            internal int Uni, Wight, Kept, Owners;
            internal long UniVal, WightVal;
            internal readonly Dictionary<string, int> UniAt = new Dictionary<string, int>(), WightAt = new Dictionary<string, int>();
            internal bool PlayerUni, PlayerWight;

            internal void Add(ItemObject it, int n, string where)
            {
                if (it == null || n <= 0) return;
                bool uni = IsUnicorn(it);
                var d = uni ? UniAt : WightAt;
                int had; d.TryGetValue(where, out had); d[where] = had + n;
                if (uni) { Uni += n; UniVal += (long)it.Value * n; } else { Wight += n; WightVal += (long)it.Value * n; }
                try { HorseBook.Note561(HorseBook.Cat(it), n); } catch { }
            }
        }

        private static string Places(Dictionary<string, int> d)
        {
            if (d.Count == 0) return "";
            var l = new List<string>();
            foreach (var kv in d) l.Add(kv.Key + " " + kv.Value);
            return " (" + string.Join(", ", l.ToArray()) + ")";
        }

        /// <summary>Usuwa ze stosu wszystkie sztuki, ktorych trzymac nie wolno (holder = null - polka: nic). Zwraca liczbe zdjetych.</summary>
        private static int CleanRoster(ItemRoster r, MobileParty holder, Tally t, string where, List<string> list, string name)
        {
            if (r == null) return 0;
            int cut = 0;
            for (int i = r.Count - 1; i >= 0; i--)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0 || it.ItemType != ItemObject.ItemTypeEnum.Horse) continue;
                if (!IsUnicorn(it) && !IsWightMount(it)) continue;
                if (holder != null && MayHold(holder, it)) { t.Kept += el.Amount; continue; }
                r.AddToCounts(el.EquipmentElement, -el.Amount);
                t.Add(it, el.Amount, where);
                cut += el.Amount;
                if (list != null && list.Count < 12) list.Add(name + ": " + it.StringId + " x" + el.Amount);
            }
            return cut;
        }

        private static void SweepWorld(string why)
        {
            if (!On) return;
            var t = new Tally();
            var shelfList = new List<string>();
            // polki i magazyny osad - nikt ich tu nie trzyma (kupic nie wolno nikomu)
            foreach (var st in Settlement.All)
            {
                try { if (st != null) CleanRoster(st.ItemRoster, null, t, "polki", shelfList, st.Name != null ? st.Name.ToString() : st.StringId); }
                catch (Exception e) { Stumble("Shelf", e); }
            }
            // R561/1: schowek gracza (Settlement.Stash) - gracz jest zywy: jednorozec i kon umarlych schodza i stad (ksiega ArmsBook.ForEachRoster i tak go liczy)
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null) continue;
                    ItemRoster stash = null;
                    try { stash = st.Stash; } catch { }
                    if (stash == null || ReferenceEquals(stash, st.ItemRoster)) continue;
                    int u0 = t.Uni, w0 = t.Wight;
                    CleanRoster(stash, MobileParty.MainParty, t, "schowek gracza", shelfList, (st.Name != null ? st.Name.ToString() : st.StringId) + " (schowek)");
                    t.PlayerUni |= t.Uni > u0; t.PlayerWight |= t.Wight > w0;
                }
                catch (Exception e) { Stumble("Stash", e); }
            }
            // tabory partii (takze gracza) - Inni trzymaja konie umarlych
            foreach (var mp in MobileParty.All)
            {
                try
                {
                    if (mp == null || mp.ItemRoster == null) continue;
                    bool player = mp == MobileParty.MainParty;
                    int u0 = t.Uni, w0 = t.Wight;
                    CleanRoster(mp.ItemRoster, mp, t, player ? "tabor gracza" : "tabory AI", null, null);
                    if (player) { t.PlayerUni |= t.Uni > u0; t.PlayerWight |= t.Wight > w0; }
                }
                catch (Exception e) { Stumble("Party", e); }
            }
            // zbrojownie DTE partii AI (EveryoneCampaignBehavior.PartyArmories)
            try
            {
                var all = AiGear.Armories();
                if (all != null)
                    foreach (var kv in all)
                    {
                        try
                        {
                            var arm = kv.Value;
                            if (arm == null || arm.Count == 0) continue;
                            List<ItemObject> kill = null;
                            MobileParty owner = null; bool res = false;
                            foreach (var a in arm)
                            {
                                var it = a.Key;
                                if (it == null || a.Value <= 0 || it.ItemType != ItemObject.ItemTypeEnum.Horse) continue;
                                bool uni = IsUnicorn(it);
                                if (!uni && !IsWightMount(it)) continue;
                                if (!uni)
                                {
                                    if (!res) { res = true; owner = UniqueLaw.FindParty(kv.Key); t.Owners++; }
                                    // wlasciciel nieznany - kon umarlych zostaje (moze to partia Innych); jednorozec schodzi zawsze
                                    if (owner == null || MayHold(owner, it)) { t.Kept += a.Value; continue; }
                                }
                                if (kill == null) kill = new List<ItemObject>();
                                kill.Add(it);
                            }
                            if (kill == null) continue;
                            foreach (var it in kill)
                            {
                                int n = arm[it];
                                arm.Remove(it);
                                t.Add(it, n, "zbrojownie DTE");
                            }
                        }
                        catch (Exception e) { Stumble("Armory", e); }
                    }
            }
            catch (Exception e) { Stumble("Armories", e); }
            // zbrojownia DTE gracza (ArmyArmory)
            try
            {
                var pa = HorseFate.PlayerArmory();
                if (pa != null)
                {
                    int u0 = t.Uni, w0 = t.Wight;
                    CleanRoster(pa, MobileParty.MainParty, t, "zbrojownia gracza", null, null);
                    t.PlayerUni |= t.Uni > u0; t.PlayerWight |= t.Wight > w0;
                }
            }
            catch (Exception e) { Stumble("PlayerArmory", e); }
            // pula koni weteranow (HorseFate) - nikt jej nie trzyma
            try { HorseFate.Purge561(it => IsUnicorn(it) || IsWightMount(it), (it, n) => t.Add(it, n, "pula weteranow")); }
            catch (Exception e) { Stumble("VetPool", e); }
            // bohaterowie - slot konia rynsztunku bojowego i cywilnego
            int heroSwap = 0;
            var heroList = new List<string>();
            foreach (var h in Hero.AllAliveHeroes)
            {
                try
                {
                    if (h == null || h.CharacterObject == null) continue;
                    foreach (var eq in new[] { h.BattleEquipment, h.CivilianEquipment })
                    {
                        if (eq == null) continue;
                        var it = eq[EquipmentIndex.Horse].Item;
                        if (it == null || it.ItemType != ItemObject.ItemTypeEnum.Horse) continue;
                        if (!IsUnicorn(it) && !IsWightMount(it)) continue;
                        if (MayRide(h.CharacterObject, it)) { t.Kept++; continue; }
                        bool player = h.PartyBelongedTo != null && h.PartyBelongedTo == MobileParty.MainParty;
                        if (player) { if (IsUnicorn(it)) t.PlayerUni = true; else t.PlayerWight = true; }
                        var repl = BestFromBaggage(h.PartyBelongedTo);
                        eq[EquipmentIndex.Horse] = repl;
                        t.Add(it, 1, "bohaterowie");
                        if (!repl.IsEmpty) heroSwap++;
                        if (heroList.Count < 12) heroList.Add((h.Name != null ? h.Name.ToString() : h.StringId) + ": " + it.StringId
                                                            + " -> " + (repl.IsEmpty ? "pieszo" : repl.Item.StringId));
                    }
                }
                catch (Exception e) { Stumble("Hero", e); }
            }
            if (t.PlayerUni) InformationManager.DisplayMessage(new InformationMessage("The unicorn slips its halter and is gone into the wild.", Colors.Yellow));
            if (t.PlayerWight) InformationManager.DisplayMessage(new InformationMessage("The wight horse crumbles to bone and rot.", Colors.Yellow));
            if (t.Uni + t.Wight > 0 || why != "dzien" || (int)CampaignTime.Now.ToDays % 7 == 0)
                Log.Info("Zakazane wierzchowce (561): " + why + " | jednorozce uwolnione " + t.Uni + Places(t.UniAt) + " wartosc " + t.UniVal.ToString(CultureInfo.InvariantCulture)
                         + " | konie umarlych u zywych rozpadly sie " + t.Wight + Places(t.WightAt) + " wartosc " + t.WightVal.ToString(CultureInfo.InvariantCulture)
                         + " | Inni zatrzymali " + t.Kept + " (wlascicieli zbrojowni sprawdzonych " + t.Owners + ")"
                         + (shelfList.Count > 0 ? " | polki: [" + string.Join(", ", shelfList.ToArray()) + "]" : "")
                         + (heroList.Count > 0 ? " | bohaterowie (z taboru " + heroSwap + "): [" + string.Join(", ", heroList.ToArray()) + "]" : "")
                         + " | bez zaplaty (ujscie), potkniecia " + _stumbles + ".");
        }

        /// <summary>Najlepszy zwykly kon (IsPlainMount, bez modyfikatora) z taboru partii - zdjety z taboru (przeniesienie); brak - pusto.</summary>
        private static EquipmentElement BestFromBaggage(MobileParty mp)
        {
            var r = mp != null ? mp.ItemRoster : null;
            if (r == null) return new EquipmentElement(null);
            int best = -1; int bestVal = -1;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0 || !Stables.IsPlainMount(it)) continue;
                if (it.HorseComponent == null || !it.HorseComponent.IsRideable) continue;
                if (it.Value > bestVal) { bestVal = it.Value; best = i; }
            }
            if (best < 0) return new EquipmentElement(null);
            var pick = r.GetElementCopyAtIndex(best).EquipmentElement;
            r.AddToCounts(pick, -1);
            return pick;
        }

        /// <summary>Wejscie gracza do osady (i co godzine, gdy w niej stoi - R561/2): czystka tylko tej polki - sztuka, ktora przyszla w ciagu doby, nie trafi do okna handlu.</summary>
        private static void SweepShelfOnEntry(Settlement st, string why = "wejscie gracza")
        {
            if (!On || st == null || st.ItemRoster == null) return;
            var t = new Tally();
            var list = new List<string>();
            CleanRoster(st.ItemRoster, null, t, "polka: " + why, list, st.Name != null ? st.Name.ToString() : st.StringId);
            if (t.Uni + t.Wight > 0)
                Log.Info("Zakazane wierzchowce (561): " + why + " | jednorozce uwolnione " + t.Uni + ", konie umarlych rozpadly sie " + t.Wight
                         + " [" + string.Join(", ", list.ToArray()) + "] wartosc " + (t.UniVal + t.WightVal).ToString(CultureInfo.InvariantCulture) + " | bez zaplaty (ujscie).");
        }
    }
}
