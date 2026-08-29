using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// PRAWO LEGEND. Jeff 28.08: "legendarne bronie sa unikatowe - nie moga
    /// wszyscy wojacy w nich biegac". ROT wpisal nazwane klingi (value 100k+,
    /// Brightroar, Widow's Wail...) do SZABLONOW elitarnych jednostek - kazdy
    /// taki zolnierz rodzil sie z unikatem. Trzy ostrza tego prawa:
    /// (1) SweepTemplates przy kazdym wczytaniu: w wyposazeniu jednostek
    ///     NIE-bohaterow legenda schodzi, wchodzi najlepszy ZWYKLY odpowiednik
    ///     tej samej klasy broni (bohaterowie nosza swoje klingi dalej);
    /// (2) ReplacementFor jest uzywane tez przy spawnie misji (DragonUnmount) -
    ///     gdyby DTE ubralo szeregowego w legende ze swojego magazynu;
    /// (3) jednorazowa czystka sakw gracza: z kazdej klingi zostaje JEDEN
    ///     egzemplarz (najlepszy stan), nadwyzki znikaja.
    /// </summary>
    internal sealed class LegendaryLaw : CampaignBehaviorBase
    {
        private bool _playerCulledAll;
        private static readonly Dictionary<ItemObject, ItemObject> Repl = new Dictionary<ItemObject, ItemObject>();

        // bronie-persony BEZ wpisanego value (CraftedItem liczy wartosc z czesci,
        // wiec prog 100k ich nie lapal - stad 6 mlotow Roberta u Jeffa; audyt
        // 28.08 po ROTassets.xml). Reszta legend ma value 150k-350k i lapie sie
        // progiem.
        private static readonly HashSet<string> LegendIds = new HashSet<string>
        {
            "baratheon_hammer",   // Robert Baratheon's Hammer - jest JEDEN na swiecie
            "needle",             // Needle - igla Aryi
            "gendry_hammer"       // Gendry's Hammer
        };

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSession);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this,
                delegate { try { SweepAiArmories("dzien"); } catch { } });
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("armouryLegendsCulledAll", ref _playerCulledAll);
        }

        private void OnSession(CampaignGameStarter starter)
        {
            try { SweepTemplates(); } catch (Exception e) { Log.Error("LegendaryLaw.SweepTemplates", e); }
            try { if (!_playerCulledAll) { CullPlayerAll(); _playerCulledAll = true; } }
            catch (Exception e) { Log.Error("LegendaryLaw.Cull", e); }
            try { SweepAiArmories("wczytanie"); } catch (Exception e) { Log.Error("LegendaryLaw.SweepAiArmories", e); }
            try { SweepWorld(); } catch (Exception e) { Log.Error("LegendaryLaw.SweepWorld", e); }
            try { LockLegendPieces(); } catch (Exception e) { Log.Error("LegendaryLaw.LockLegendPieces", e); }
            // wyrownanie skilli jednostek MUSI isc PO sweepie legend - inaczej
            // policzyloby wymogi z klingi, ktora za chwile znika z szablonu
            try { TroopFit.Run(); } catch (Exception e) { Log.Error("TroopFit.Run", e); }
        }

        /// <summary>KUZNIA NIE POWIELA LEGEND (Jeff: "jak wykuje, to moze byc
        /// wiecej niz jedna - popraw to"). Czesci skladowe legendarnych klng
        /// (brightroar_blade itd.) znikaja z projektownika kuzni - bez klingi
        /// Brightroara nie zlozysz Brightroara. Czesc WSPOLDZIELONA ze zwykla
        /// bronia zostaje widoczna (nie psujemy normalnego kucia) - wystarczy,
        /// ze choc jedna dedykowana czesc legendy jest ukryta.</summary>
        private static void LockLegendPieces()
        {
            var fHide = AccessTools.Field(typeof(CraftingPiece), "<IsHiddenOnDesigner>k__BackingField");
            if (fHide == null) { Log.Info("LegendaryLaw: brak pola IsHiddenOnDesigner - kucia nie blokuje."); return; }

            // czesci uzywane przez ZWYKLE przedmioty - tych nie wolno ukryc
            var common = new HashSet<CraftingPiece>();
            foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
            {
                if (it == null || IsLegend(it) || it.WeaponDesign == null) continue;
                var used = it.WeaponDesign.UsedPieces;
                if (used == null) continue;
                foreach (var el in used)
                    if (el != null && el.CraftingPiece != null) common.Add(el.CraftingPiece);
            }

            int locked = 0, shared = 0;
            foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
            {
                if (!IsLegend(it) || it.WeaponDesign == null) continue;
                var used = it.WeaponDesign.UsedPieces;
                if (used == null) continue;
                foreach (var el in used)
                {
                    var p = el != null ? el.CraftingPiece : null;
                    if (p == null || p.IsHiddenOnDesigner) continue;
                    if (common.Contains(p)) { shared++; continue; }
                    fHide.SetValue(p, true);
                    locked++;
                }
            }
            if (locked > 0)
                Log.Info("LegendaryLaw: " + locked + " dedykowanych czesci legend ukrytych w kuzni (wspoldzielonych pominieto "
                         + shared + ") - legend NIE da sie wykuc.");
        }

        /// <summary>ZERO NA START (Jeff: "wyzeruj start i save'a - wykuc mozna,
        /// kupic nie: nie da sie kupic 5 mieczy Aryi"). Przy kazdym wczytaniu:
        /// (1) kazda legenda dostaje NotMerchandise - nigdy wiecej w zaopatrzeniu
        /// sklepow (kucie wlasnorecznie w kuzni ZOSTAJE dozwolone);
        /// (2) targi wszystkich osad czyszczone z zalegajacych legend;
        /// (3) bagaze partii AI czyszczone (u gracza robi to CullPlayerAll).</summary>
        private static void SweepWorld()
        {
            int flagged = 0, offShelves = 0, offBags = 0;
            try
            {
                var fMerch = AccessTools.Field(typeof(ItemObject), "<NotMerchandise>k__BackingField");
                if (fMerch != null)
                    foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                        if (IsLegend(it) && !it.NotMerchandise) { fMerch.SetValue(it, true); flagged++; }
            }
            catch (Exception e) { Log.Error("LegendaryLaw.flag", e); }
            try
            {
                foreach (var st in TaleWorlds.CampaignSystem.Settlements.Settlement.All)
                {
                    var roster = st != null ? st.ItemRoster : null;
                    if (roster == null) continue;
                    for (int i = roster.Count - 1; i >= 0; i--)
                    {
                        var el = roster.GetElementCopyAtIndex(i);
                        if (!IsLegend(el.EquipmentElement.Item) || el.Amount <= 0) continue;
                        roster.AddToCounts(el.EquipmentElement, -el.Amount);
                        offShelves += el.Amount;
                    }
                }
            }
            catch (Exception e) { Log.Error("LegendaryLaw.shelves", e); }
            try
            {
                foreach (var mp in MobileParty.All)
                {
                    if (mp == null || mp == MobileParty.MainParty || mp.ItemRoster == null) continue;
                    var roster = mp.ItemRoster;
                    for (int i = roster.Count - 1; i >= 0; i--)
                    {
                        var el = roster.GetElementCopyAtIndex(i);
                        if (!IsLegend(el.EquipmentElement.Item) || el.Amount <= 0) continue;
                        roster.AddToCounts(el.EquipmentElement, -el.Amount);
                        offBags += el.Amount;
                    }
                }
            }
            catch (Exception e) { Log.Error("LegendaryLaw.bags", e); }
            if (flagged > 0 || offShelves > 0 || offBags > 0)
                Log.Info("LegendaryLaw: swiat wyzerowany - " + flagged + " legend poza handlem, "
                         + offShelves + " szt. z targow, " + offBags + " szt. z bagazy AI.");
        }

        /// <summary>Wirtualne magazyny DTE partii AI (EveryoneCampaignBehavior.
        /// PartyArmories) - tam lezal recykling setek legend z poleglych.
        /// Jeff: "usun z innych armii AI te unikatowe bronie - moze byc jedna
        /// na swiecie i ktos ja nosi, ale nie ze polowa armii ja ma".
        /// Bohaterow nie tykamy - noszone klingi zostaja przy wlascicielach.</summary>
        private static void SweepAiArmories(string why)
        {
            try
            {
                var t = AccessTools.TypeByName("DynamicTroopEquipmentReupload.EveryoneCampaignBehavior");
                var f = t != null ? AccessTools.Field(t, "PartyArmories") : null;
                var map = f != null ? f.GetValue(null) as System.Collections.IDictionary : null;
                if (map == null) return;
                int cut = 0, parties = 0;
                foreach (System.Collections.DictionaryEntry e in map)
                {
                    var inner = e.Value as System.Collections.IDictionary;
                    if (inner == null) continue;
                    List<object> kill = null;
                    foreach (System.Collections.DictionaryEntry kv in inner)
                    {
                        var it = kv.Key as ItemObject;
                        if (it == null || !IsLegend(it)) continue;
                        if (kill == null) kill = new List<object>();
                        kill.Add(kv.Key);
                        try { cut += Convert.ToInt32(kv.Value); } catch { cut++; }
                    }
                    if (kill != null)
                    {
                        parties++;
                        foreach (var k in kill) inner.Remove(k);
                    }
                }
                if (cut > 0)
                    Log.Info("LegendaryLaw: magazyny AI (" + why + ") - " + cut + " legend przepadlo z " + parties + " partii.");
            }
            catch (Exception e) { Log.Error("LegendaryLaw.SweepAiArmories", e); }
        }

        internal static bool IsLegend(ItemObject it)
        {
            if (it == null || !it.HasWeaponComponent || it.StringId == null) return false;
            if (LegendIds.Contains(it.StringId)) return true;
            var floor = Settings.Current.LegendaryLootValueFloor;
            return floor > 0 && it.Value >= floor;
        }

        /// <summary>Najlepszy ZWYKLY odpowiednik legendy: ta sama klasa broni,
        /// tier nie wyzszy niz legenda, najdrozszy z pozostalych. Cache na sesje.</summary>
        internal static ItemObject ReplacementFor(ItemObject legend)
        {
            if (legend == null) return null;
            ItemObject cached;
            if (Repl.TryGetValue(legend, out cached)) return cached;
            ItemObject best = null;
            try
            {
                var wc = legend.PrimaryWeapon != null ? legend.PrimaryWeapon.WeaponClass : WeaponClass.Undefined;
                var all = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();
                foreach (var it in all)
                {
                    if (it == null || !it.HasWeaponComponent || IsLegend(it)) continue;
                    if (it.ItemType != legend.ItemType) continue;
                    if (it.PrimaryWeapon == null || it.PrimaryWeapon.WeaponClass != wc) continue;
                    if (it.Tier > legend.Tier) continue;
                    if (best == null || it.Tier > best.Tier
                        || (it.Tier == best.Tier && it.Value > best.Value)) best = it;
                }
            }
            catch (Exception e) { Log.Error("LegendaryLaw.ReplacementFor", e); }
            Repl[legend] = best;
            if (best != null)
                Log.Info("LegendaryLaw: zamiennik dla " + legend.StringId + " -> " + best.StringId + " (t" + ((int)best.Tier + 1) + ").");
            return best;
        }

        /// <summary>Szablony jednostek NIE-bohaterow: legenda w slocie broni
        /// schodzi na rzecz zwyklego odpowiednika. Szablony zyja w pamieci
        /// sesji, wiec sweep idzie przy kazdym wczytaniu.</summary>
        private static void SweepTemplates()
        {
            var all = MBObjectManager.Instance.GetObjectTypeList<CharacterObject>();
            if (all == null) return;
            int swapped = 0, troops = 0;
            foreach (var ch in all)
            {
                if (ch == null || ch.IsHero) continue;
                bool touched = false;
                foreach (var eq in ch.BattleEquipments)
                {
                    if (eq == null) continue;
                    for (int slot = 0; slot < 4; slot++)
                    {
                        var item = eq[(EquipmentIndex)slot].Item;
                        if (!IsLegend(item)) continue;
                        var repl = ReplacementFor(item);
                        eq[(EquipmentIndex)slot] = repl != null
                            ? new EquipmentElement(repl) : new EquipmentElement(null);
                        swapped++; touched = true;
                    }
                }
                if (touched) troops++;
            }
            if (swapped > 0)
                Log.Info("LegendaryLaw: " + swapped + " legendarnych klng zdjetych z szablonow " + troops + " jednostek.");

            // ZRODLO mnozenia: ROT-owe szablony WLADCOW (vla_bat_template_tywin
            // z brightroar) maja culture=neutral_culture + IsLordTemplate, wiec
            // gra LOSUJE je przypadkowym bohaterom - kazdy wylosowany "Tywin"
            // to kolejna kopia klingi w swiecie. Czyscimy SZABLONY (rostery):
            // istniejacy wladcy trzymaja swoje kopie w save, nowi bohaterowie
            // losuja juz czyste zestawy.
            int setSwapped = 0;
            try
            {
                var rosters = MBObjectManager.Instance.GetObjectTypeList<MBEquipmentRoster>();
                if (rosters != null)
                    foreach (var ro in rosters)
                    {
                        if (ro == null) continue;
                        foreach (var eq in ro.AllEquipments)
                        {
                            if (eq == null) continue;
                            for (int slot = 0; slot < 4; slot++)
                            {
                                var item = eq[(EquipmentIndex)slot].Item;
                                if (!IsLegend(item)) continue;
                                var repl = ReplacementFor(item);
                                eq[(EquipmentIndex)slot] = repl != null
                                    ? new EquipmentElement(repl) : new EquipmentElement(null);
                                setSwapped++;
                            }
                        }
                    }
            }
            catch (Exception e) { Log.Error("LegendaryLaw.SweepRosters", e); }
            if (setSwapped > 0)
                Log.Info("LegendaryLaw: " + setSwapped + " legend zdjetych z LOSOWANYCH szablonow bohaterow (zrodlo mnozenia).");
        }

        /// <summary>Sakwy gracza: WSZYSTKIE legendy znikaja co do sztuki
        /// (Jeff 28.08: "usun wszystkie unikatowe miecze u mnie tez i mlot
        /// Roberta - gra totalnie stracila sens"). Jednorazowo (SyncData).</summary>
        private static void CullPlayerAll()
        {
            var roster = MobileParty.MainParty != null ? MobileParty.MainParty.ItemRoster : null;
            if (roster == null) return;
            int cut = 0;
            for (int i = roster.Count - 1; i >= 0; i--)
            {
                var el = roster.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (!IsLegend(it) || el.Amount <= 0) continue;
                roster.AddToCounts(el.EquipmentElement, -el.Amount);
                cut += el.Amount;
            }
            if (cut > 0)
            {
                Log.Info("LegendaryLaw: sakwy gracza - " + cut + " legendarnych broni usunieto CO DO SZTUKI.");
                Log.Player("The stolen legends are gone from your packs - " + cut + " named weapons struck out.", true);
            }
        }
    }
}
