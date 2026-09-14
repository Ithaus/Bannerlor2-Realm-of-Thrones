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

        // SERYJNE KLINGI VALYRIANSKIE (Jeff 14.09: "czemu valyrianska stal jest
        // powszechna, jakby kazdy na rogu mogl walczyc elitarna bronia - to ma
        // byc unikatowe"). ROTassets.xml daje im value 200k, tier 6, ale
        // is_merchandise=TRUE - wiec prog "100k + NotMerchandise" je przepuszczal
        // i lezaly w workach z lupem, magazynach DTE i rekach szeregowych
        // (screen 14.09: 25 sztuk po 3% w sakwach). Decyzja z 29.08 ("maja
        // zostac zwyklym drogim sprzetem") ODWROCONA na zyczenie Jeffa.
        // Ta sama lista co CrashScribe.Mends.BladePrefixes - trzymac w zgodzie.
        private static readonly string[] LegendPrefixes = {
            "val_steel_sword_", "koa_sword_", "whyt_sword", "skull_sword"
        };

        private static bool HasLegendPrefix(string id)
        {
            if (id == null) return false;
            for (int i = 0; i < LegendPrefixes.Length; i++)
                if (id.StartsWith(LegendPrefixes[i], StringComparison.Ordinal)) return true;
            return false;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSession);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this,
                delegate { try { SweepAiArmories("dzien"); } catch { } try { SweepMarkets("dzien"); } catch { } });
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("armouryLegendsCulledAll", ref _playerCulledAll);
        }

        private void OnSession(CampaignGameStarter starter)
        {
            try { BuildLegendSet(); } catch (Exception e) { Log.Error("LegendaryLaw.BuildLegendSet", e); }
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
            try { SweepMarkets("wczytanie"); } catch (Exception e) { Log.Error("LegendaryLaw.SweepMarkets", e); }
            try
            {
                foreach (var mp in MobileParty.All)
                {
                    if (mp == null || mp == MobileParty.MainParty || mp.ItemRoster == null) continue;
                    var roster = mp.ItemRoster;
                    for (int i = roster.Count - 1; i >= 0; i--)
                    {
                        var el = roster.GetElementCopyAtIndex(i);
                        var it2 = el.EquipmentElement.Item;
                        if (it2 == null || el.Amount <= 0) continue;
                        // slonie-towar w bagazach AI tez precz (Jeff 29.08:
                        // "slonie ma Zlota Kompania" - jej bojowe zyja
                        // w szablonach, nie w workach)
                        bool elephant = it2.StringId != null
                            && (it2.StringId == "elephant" || it2.StringId.StartsWith("rot_elephant"));
                        if (!elephant && !IsLegend(it2)) continue;
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

        /// <summary>GEOGRAFIA WIERZCHOWCOW na targach (Jeff 14.09): mamut nigdzie,
        /// wielblad w Dorne i Qarth, rydwan w Essos, slon w Volantis - reszta
        /// schodzi z polek, bo stamtad kupowala je nasza stajnia AI.</summary>
        private static void SweepMarkets(string why)
        {
            int off = 0;
            try
            {
                foreach (var st in TaleWorlds.CampaignSystem.Settlements.Settlement.All)
                {
                    var roster = st != null ? st.ItemRoster : null;
                    if (roster == null) continue;
                    for (int i = roster.Count - 1; i >= 0; i--)
                    {
                        var el = roster.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (it == null || el.Amount <= 0 || !MountLaw.IsExotic(it)) continue;
                        if (MountLaw.AllowedForSettlement(st, it)) continue;
                        roster.AddToCounts(el.EquipmentElement, -el.Amount);
                        off += el.Amount;
                    }
                }
            }
            catch (Exception e) { Log.Error("LegendaryLaw.SweepMarkets", e); }
            if (off > 0) Log.Info("LegendaryLaw: targi (" + why + ") - " + off + " egzotycznych wierzchowcow zdjetych z polek nie u swoich.");
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
                int cut = 0, parties = 0, clubs = 0, mounts = 0;
                foreach (System.Collections.DictionaryEntry e in map)
                {
                    var inner = e.Value as System.Collections.IDictionary;
                    if (inner == null) continue;
                    // SPRZET OLBRZYMOW (Jeff 14.09): z magazynu partii BEZ olbrzymow
                    // precz - a partia z olbrzymami zostawia go swoim (DTE zbroi
                    // z magazynu, wiec czystka rozbroilaby olbrzymow)
                    bool giants = true;
                    MobileParty owner = null;
                    try
                    {
                        if (e.Key is MBGUID) owner = MBObjectManager.Instance.GetObject((MBGUID)e.Key) as MobileParty;
                        if (owner != null) giants = GiantGear.PartyHasGiants(owner);
                    }
                    catch { }
                    List<object> kill = null;
                    foreach (System.Collections.DictionaryEntry kv in inner)
                    {
                        var it = kv.Key as ItemObject;
                        if (it == null) continue;
                        if (!giants && GiantGear.Is(it))
                        {
                            if (kill == null) kill = new List<object>();
                            kill.Add(kv.Key);
                            try { clubs += Convert.ToInt32(kv.Value); } catch { clubs++; }
                            continue;
                        }
                        // GEOGRAFIA WIERZCHOWCOW (Jeff 14.09): mamut/wielblad/rydwan/slon
                        // w magazynie partii, ktorej sie nie naleza - precz
                        if (owner != null && MountLaw.IsExotic(it) && !MountLaw.AllowedForParty(owner, it))
                        {
                            if (kill == null) kill = new List<object>();
                            kill.Add(kv.Key);
                            try { mounts += Convert.ToInt32(kv.Value); } catch { mounts++; }
                            continue;
                        }
                        if (!IsLegend(it)) continue;
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
                if (cut > 0 || clubs > 0 || mounts > 0)
                    Log.Info("LegendaryLaw: magazyny AI (" + why + ") - " + cut + " legend przepadlo z " + parties + " partii"
                             + (clubs > 0 ? ", " + clubs + " szt. sprzetu olbrzymow z partii bez olbrzymow" : "")
                             + (mounts > 0 ? ", " + mounts + " egzotycznych wierzchowcow nie u swoich" : "") + ".");
            }
            catch (Exception e) { Log.Error("LegendaryLaw.SweepAiArmories", e); }
        }

        // FABRYCZNY zbior legend (Jeff 29.08: drogi LUK wpadl pod prog 100k,
        // dostal od nas NotMerchandise i stara regula kuzni zablokowala kucie -
        // "mam info legendary"). Legenda lore ma is_merchandise=false JUZ W XML
        // (Ice, Brightroar, mloty person...); drogie luki i seryjne valyriany
        // NIE maja - i maja zostac zwyklym (drogim) sprzetem. Zbior budujemy
        // RAZ na proces, ZANIM SweepWorld cokolwiek poustawia.
        private static HashSet<ItemObject> _legendSet;

        internal static void BuildLegendSet()
        {
            if (_legendSet != null) return;
            var set = new HashSet<ItemObject>();
            try
            {
                var floor = Settings.Current.LegendaryLootValueFloor;
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (it == null || !it.HasWeaponComponent || it.StringId == null) continue;
                    if (LegendIds.Contains(it.StringId) || HasLegendPrefix(it.StringId)) { set.Add(it); continue; }
                    if (floor > 0 && it.Value >= floor && it.NotMerchandise) set.Add(it);
                }
                Log.Info("LegendaryLaw: zbior legend zbudowany - " + set.Count
                         + " broni (fabryczne NotMerchandise 100k+, lista person, seryjne klingi valyrianskie).");
            }
            catch (Exception e) { Log.Error("LegendaryLaw.BuildLegendSet", e); }
            _legendSet = set;
        }

        internal static bool IsLegend(ItemObject it)
        {
            if (it == null || !it.HasWeaponComponent || it.StringId == null) return false;
            if (_legendSet != null) return _legendSet.Contains(it);
            if (LegendIds.Contains(it.StringId) || HasLegendPrefix(it.StringId)) return true;
            var floor = Settings.Current.LegendaryLootValueFloor;
            return floor > 0 && it.Value >= floor && it.NotMerchandise;
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
                // UWAGA 14.09: dawna etykieta brzmiala "(zrodlo mnozenia)" i BYLA NIEPRAWDA.
                // To podmiana W SLOCIE - liczba sztuk sie nie zmienia. Do tego wszystkie
                // rostery niosace legendy maja culture="Culture.neutral_culture",
                // a DefaultEquipmentSelectionModel.GetSuitableEquipmentSet wymaga rownosci
                // kultur; zaden bohater w calym zestawie modow neutral_culture nie ma,
                // wiec te rostery sa MARTWE. Ten napis wyslal cale sledztwo w sprawie
                // "miliona mieczy valyrianskich" w slepa uliczke - stad nowa tresc.
                Log.Info("LegendaryLaw: " + setSwapped + " legend podmienionych w losowanych szablonach bohaterow (podmiana w slocie, nie dodanie; rostery neutral_culture sa w praktyce nieuzywane).");
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
