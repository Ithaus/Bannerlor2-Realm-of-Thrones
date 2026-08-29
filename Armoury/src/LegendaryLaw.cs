using System;
using System.Collections.Generic;
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
