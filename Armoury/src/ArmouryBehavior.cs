using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    public class ArmouryBehavior : CampaignBehaviorBase
    {
        public static ArmouryBehavior Instance;

        // kondycja: "slot|condition|originalModifierId"
        private List<string> _condition = new List<string>();
        private int _hpBeforeBattle = -1;
        private List<string> _projects = new List<string>();
        private Dictionary<string,int> _lootSnapshot = new Dictionary<string,int>();
        private CampaignTime _spoilsWindow = CampaignTime.Zero;
        private CampaignTime _lootFlushDue = CampaignTime.Zero;
        private CampaignTime _shortageShoutDue = CampaignTime.Zero;   // meldunek brakow PO odebraniu lupow
        // legendy wykute raz na zawsze (StringId) - drugiej takiej swiat nie ujrzy
        internal static readonly List<string> Legends = new List<string>();
        // wybrany pomocnik kowala (Jeff 30.08: "wybieram postac, ona sie uczy
        // i mi pomaga"): "" / "auto" = najlepszy z druzyny, "none" = pracuje
        // sam, inaczej StringId bohatera
        internal static string SmithHelperId = "";

        /// <summary>Nauczone wzory unikatow (id itemow) - przetopienie zdobytego
        /// egzemplarza uczy kuznie go odtwarzac (Jeff 30.08).</summary>
        internal static System.Collections.Generic.List<string> UniqueLore =
            new System.Collections.Generic.List<string>();

        private static bool _restitution0831;

        /// <summary>RESTYTUCJA 31.08: czystka sakw zjadla wziete lupy i wlasny
        /// zuzyty sprzet, zanim wycielismy jej prog kondycji. Trzy sztuki znane
        /// z logu wracaja do sakw - raz na kampanie.</summary>
        private static void Restitution0831()
        {
            try
            {
                if (_restitution0831) return;
                _restitution0831 = true;
                int given = 0;
                // 3 wlasne sztuki znane z logu + lupy z 19 Broken Men
                // (Jeff: "12 i potem 7 osob") - reprezentatywny sprzet looterow
                var back = new[] {
                    new[] { "battania_civil_cape", "ripped_cloth_unarmoured" },
                    new[] { "bandit_saddle_steppe", "ripped_cloth" },
                    new[] { "sturgia_sword_1_t2", "rusty_sword" },
                    // Jeff: "oni mieli COMMON ARMOUR, plytowe" - DTE ubral bande
                    // w pospolite plyty ROT (diff 30-70); oddajemy wlasnie te
                    new[] { "common_armor", null }, new[] { "common_armor", null }, new[] { "common_armor", null },
                    new[] { "common_barbute", null }, new[] { "common_barbute", null },
                    new[] { "common_kettle", null }, new[] { "common_spangen", null },
                    new[] { "common_pauldrons", null },
                    new[] { "common_boots", null }, new[] { "common_boots", null },
                    new[] { "common_bracers", null }, new[] { "common_gloves", null }
                };
                var ro = MobileParty.MainParty != null ? MobileParty.MainParty.ItemRoster : null;
                if (ro == null) return;
                foreach (var pair in back)
                {
                    var it = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemObject>(pair[0]);
                    if (it == null) continue;
                    var mod = pair[1] != null ? TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemModifier>(pair[1]) : null;
                    ro.AddToCounts(new EquipmentElement(it, mod), 1);
                    given++;
                }
                if (given > 0)
                    Log.Player("The quartermaster returns what the purge wrongly burned - " + given + " pieces back in your bags.");
            }
            catch (Exception e) { Log.Error("Restitution0831", e); }
        }

        internal static void LearnUnique(ItemObject it)
        {
            try
            {
                if (it == null || UniqueLore.Contains(it.StringId)) return;
                UniqueLore.Add(it.StringId);
                Log.Player("You have studied the make of " + it.Name + " - the CRAFT bench can now reproduce it, and your men may wear it.");
                Log.Info("UniqueLore: nauczono " + it.StringId + " (razem " + UniqueLore.Count + ").");
            }
            catch (Exception e) { Log.Error("LearnUnique", e); }
        }
        // KSIEGA WKLADOW GRACZA (Jeff 29.08: "w Armoury widze i ruszam TYLKO to,
        // co sam wrzucilem - lupy 60% to wlasnosc wojska"): itemId -> ile sztuk
        // nalezy do gracza; ekran zbrojowni pokazuje wylacznie te sztuki
        private Dictionary<string,int> _playerStock = new Dictionary<string,int>();

        internal static int StockOf(string id)
        {
            try
            {
                var self = Instance;
                if (self == null || string.IsNullOrEmpty(id)) return 0;
                int v; return self._playerStock.TryGetValue(id, out v) ? v : 0;
            }
            catch { return 0; }
        }

        internal static void StockDeposit(string id, int n)
        {
            try
            {
                var self = Instance;
                if (self == null || string.IsNullOrEmpty(id) || n <= 0) return;
                int v; self._playerStock.TryGetValue(id, out v);
                self._playerStock[id] = v + n;
            }
            catch { }
        }

        // STANY MAGAZYNU PRZEZYWAJA SAVE (Jeff 29.08: "load automatycznie
        // naprawia sprzet wojska!" - DOWOD: DTE zapisuje magazyn jako
        // Dictionary<string,int> = id -> liczba, BEZ modifierow; kazdy load
        // odtwarzal wszystko czyste). My dopisujemy wlasna ksiege stanow:
        // przy zapisie zrzut "itemId|modifierId|ile", przy wczytaniu zbite
        // sztuki wracaja na miejsce czystych.
        private List<string> _armoryWear = new List<string>();
        private bool _wearRestorePending;

        // ZWROT PO MOIM BLEDZIE (29.08, Jeff: "teraz pozera kazda ilosc
        // strzal!"). Przez jeden build kwatermistrz przejmowal na wojsko
        // kazdy wklad, ktorego nie mial czym wymienic - sztuki zostawaly
        // fizycznie w magazynie, ale znikaly z ksiegi gracza. Log podaje
        // dokladne ilosci; oddajemy je raz, po czym flaga gasi zwrot.
        private bool _ammoRefundDone;
        private bool _lorePurged;

        /// <summary>SANACJA WIEDZY (Jeff 01.09: swieza kampania znala 26/26
        /// wzorow lukow T6 - statyczna lista przeciekla ze starej kampanii
        /// w tym samym procesie i sie ZAPISALA). Jednorazowo: mloda kampania
        /// (do 30 dni) ze wzorami T4+ w ksiedze = przeciek; reset do tieru 1.
        /// Starych, uczciwie wygranych kampanii nie tykamy.</summary>
        private void LorePurgeOnce()
        {
            try
            {
                if (_lorePurged) return;
                _lorePurged = true;
                if (CampaignTime.Now.ToDays >= 30) return;
                if (!RangedLore.HasHighTierKnowledge(4)) return;
                RangedLore.ResetToVirgin();
                Log.Player("The bowyer's ledger was found forged - patterns above tier 1 are struck out. Learn them anew: smelt or study captured pieces.", true);
                Log.Info("Sanacja wiedzy: mloda kampania znala wzory T4+ (przeciek miedzy kampaniami) - reset do T1.");
            }
            catch (Exception e) { Log.Error("LorePurgeOnce", e); }
        }
        private static readonly string[][] AmmoRefund =
        {
            new[] { "ravens_teeth_arrows", "87" },
            new[] { "blunt_arrows", "12" },
            new[] { "range_arrows", "5" },
        };

        private void RefundSeizedAmmo()
        {
            try
            {
                if (_ammoRefundDone) return;
                _ammoRefundDone = true;
                var armory = QuartermasterLaw.DteArmory();
                if (armory == null) { return; }
                int back = 0;
                foreach (var row in AmmoRefund)
                {
                    int n; if (!int.TryParse(row[1], out n) || n <= 0) continue;
                    // nie oddajemy wiecej, niz naprawde lezy na polkach
                    int onShelf = 0;
                    for (int i = 0; i < armory.Count; i++)
                    {
                        var el = armory.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (it != null && it.StringId == row[0]) onShelf += el.Amount;
                    }
                    int give = Math.Min(n, Math.Max(0, onShelf - StockOf(row[0])));
                    if (give <= 0) continue;
                    StockDeposit(row[0], give);
                    back += give;
                }
                if (back > 0)
                {
                    Log.Info("Zwrot amunicji przejetej przez blad: " + back + " szt. wrocilo do ksiegi gracza.");
                    Log.Player("Quartermaster: a miscount is set right - " + back
                               + " quivers are back on YOUR shelf, yours to take.", true);
                }
            }
            catch (Exception e) { Log.Error("RefundSeizedAmmo", e); }
        }

        private void OnPlayerUpgradedTroops(CharacterObject from, CharacterObject to, int num)
        {
            try
            {
                var armory = QuartermasterLaw.DteArmory();
                int total = 0;
                if (armory != null)
                    for (int i = 0; i < armory.Count; i++) total += armory.GetElementCopyAtIndex(i).Amount;
                // od 14.09 dopisujemy, CZY ten awans w ogole mial kosztowac rumaka -
                // inaczej "kon przy awansie kawalerii" jest hipoteza, nie pomiarem
                string mount;
                try
                {
                    bool srcMounted = from != null && !from.IsHero && from.IsMounted;
                    var cat = to != null ? to.UpgradeRequiresItemFromCategory : null;
                    mount = " | zrodlo konne: " + srcMounted
                          + ", wymog celu: " + (cat != null ? cat.StringId : "brak");
                }
                catch { mount = ""; }
                Log.Info("AWANS: " + (from != null ? from.StringId : "?") + " -> "
                    + (to != null ? to.StringId : "?") + " x" + num + " | magazyn: " + total + " szt." + mount);
            }
            catch (Exception e) { Log.Error("OnPlayerUpgradedTroops", e); }
        }

        private List<string> BuildArmoryWearSnapshot()
        {
            var list = new List<string>();
            try
            {
                var armory = QuartermasterLaw.DteArmory();
                if (armory == null) return list;
                for (int i = 0; i < armory.Count; i++)
                {
                    var el = armory.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    var mod = el.EquipmentElement.ItemModifier;
                    if (it == null || mod == null || el.Amount <= 0) continue;
                    list.Add(it.StringId + "|" + mod.StringId + "|" + el.Amount);
                }
            }
            catch (Exception e) { Log.Error("BuildArmoryWearSnapshot", e); }
            return list;
        }

        private void TryRestoreArmoryWear(string why)
        {
            try
            {
                if (!_wearRestorePending) return;
                var armory = QuartermasterLaw.DteArmory();
                if (armory == null || armory.Count == 0) return;   // DTE jeszcze nie odtworzyl - czekamy
                _wearRestorePending = false;
                int restored = 0;
                foreach (var line in _armoryWear)
                {
                    var parts = (line ?? "").Split('|');
                    if (parts.Length != 3) continue;
                    var it = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemObject>(parts[0]);
                    var mod = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemModifier>(parts[1]);
                    int n; if (!int.TryParse(parts[2], out n) || n <= 0) continue;
                    if (it == null || mod == null) continue;
                    // ile czystych sztuk tego itemu jest do "zbicia" z powrotem
                    int clean = 0;
                    for (int i = 0; i < armory.Count; i++)
                    {
                        var el = armory.GetElementCopyAtIndex(i);
                        if (el.EquipmentElement.Item == it && el.EquipmentElement.ItemModifier == null)
                        { clean = el.Amount; break; }
                    }
                    int take = Math.Min(n, clean);
                    if (take <= 0) continue;
                    armory.AddToCounts(new EquipmentElement(it), -take);
                    armory.AddToCounts(new EquipmentElement(it, mod), take);
                    restored += take;
                }
                _armoryWear.Clear();
                if (restored > 0)
                    Log.Info("ArmoryWear (" + why + "): odtworzono stany " + restored + " szt. magazynu (save DTE gubi modifiery).");
            }
            catch (Exception e) { Log.Error("TryRestoreArmoryWear", e); }
        }

        internal static void StockWithdraw(string id, int n)
        {
            try
            {
                var self = Instance;
                if (self == null || string.IsNullOrEmpty(id) || n <= 0) return;
                int v; self._playerStock.TryGetValue(id, out v);
                v -= n;
                if (v <= 0) self._playerStock.Remove(id); else self._playerStock[id] = v;
            }
            catch { }
        }

        /// <summary>Kopia ksiegi wkladow (na czas sesji ekranu zbrojowni -
        /// Reset/Cancel ekranu cofa rostery hurtem bez TransferItem, wiec
        /// ksiege trzeba umiec cofnac razem z nimi).</summary>
        internal static Dictionary<string, int> StockSnapshot()
        {
            try
            {
                var self = Instance;
                return self != null ? new Dictionary<string, int>(self._playerStock) : null;
            }
            catch { return null; }
        }

        internal static void StockRestore(Dictionary<string, int> snap)
        {
            try
            {
                var self = Instance;
                if (self == null || snap == null) return;
                self._playerStock = new Dictionary<string, int>(snap);
            }
            catch { }
        }

        /// <summary>
        /// KSIEGA-DUCH. Sztuki gracza schodza z polek roznymi drzwiami bez
        /// StockWithdraw: DTE zdejmuje kolczan przy spawnie lucznika,
        /// wystrzelany do zera przepada, CleanseTrashInBags i sanacja tna
        /// stosy. Ksiega puchnie ponad polki, a wtedy WarOwnedOf zaniza stan
        /// wojska i HoldReserve pokazuje graczowi cudze sztuki jako wlasne.
        /// Przed kazdym otwarciem zbrojowni przycinamy ksiege per id do tego,
        /// co NAPRAWDE lezy na polkach.
        /// </summary>
        internal static void ReconcileStock(string why)
        {
            try
            {
                var self = Instance;
                var armory = QuartermasterLaw.DteArmory();
                if (self == null || armory == null || QuartermasterEscrow.Active) return;
                // pusty magazyn = DTE jeszcze nie odtworzyl rostera po load
                // (por. TryRestoreArmoryWear) - przyciecie teraz wyzerowaloby
                // CALA ksiege gracza; czekamy na prawdziwe polki
                if (armory.Count == 0) return;
                // DTE trzyma tez pozycje NIEROZWIAZANE (id, ktorych nie umial
                // znalezc przy load) i doklada je na polki dopiero pozniej
                // (RestoreReadyArmoryItems) - poki cos wisi, ksiegi nie tykamy,
                // zeby nie przycinac wkladu, ktory za chwile wroci
                try
                {
                    var tBeh = QuartermasterLaw.FindType("DynamicTroopEquipmentReupload.ArmyArmoryBehavior");
                    if (tBeh != null)
                    {
                        object beh = null;
                        var mGet = typeof(Campaign).GetMethod("GetCampaignBehavior");
                        if (mGet != null && Campaign.Current != null)
                            beh = mGet.MakeGenericMethod(tBeh).Invoke(Campaign.Current, null);
                        var fU = beh != null ? HarmonyLib.AccessTools.Field(tBeh, "_unresolvedArmoryItemCounts") : null;
                        var dict = fU != null ? fU.GetValue(beh) as System.Collections.IDictionary : null;
                        if (dict != null && dict.Count > 0)
                        {
                            Log.Info("ReconcileStock(" + why + "): DTE ma " + dict.Count
                                     + " nierozwiazanych pozycji - rekonsyliacja odlozona.");
                            return;
                        }
                    }
                }
                catch { }
                var shelf = new Dictionary<string, int>();
                for (int i = 0; i < armory.Count; i++)
                {
                    var el = armory.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (it == null || it.StringId == null || el.Amount <= 0) continue;
                    int v; shelf.TryGetValue(it.StringId, out v);
                    shelf[it.StringId] = v + el.Amount;
                }
                int ghost = 0;
                var ids = new List<string>(self._playerStock.Keys);
                foreach (var id in ids)
                {
                    int book = self._playerStock[id];
                    int have; shelf.TryGetValue(id, out have);
                    if (book <= have) continue;
                    ghost += book - have;
                    if (have <= 0) self._playerStock.Remove(id); else self._playerStock[id] = have;
                }
                if (ghost > 0)
                    Log.Info("ReconcileStock(" + why + "): ksiega gracza przycieta o " + ghost
                             + " szt. ducha (ubytki z polek poza ekranem zbrojowni).");
            }
            catch (Exception e) { Log.Error("ReconcileStock", e); }
        }
        private Dictionary<string,int> _prisonerBaseline;

        public ArmouryBehavior() { Instance = this; HistoricalPrices.Reset(); MaterialLaw.Reset(); WesterosClimate.Reset(); OutlawLaw.Reset(); PopulationLaw.Reset(); WorkshopLaw.Reset(); IronBank.Reset(); MarketGlut.Reset(); ArmsPricing.Reset(); StartKit.Reset(); AiGear.Reset(); Levy.Reset(); VolunteerKit.Reset(); LevyGold.Reset(); WearKeep.Reset(); UniqueSpoils.Reset(); BuildDiary.Reset(); BuildFunding.Reset(); KingdomLedger.Reset(); ColdStart.Reset(); MenPurse.Reset(); ArmyClothing.Reset(); AiWear.Reset(); SmithHours.Reset(); RecruitKit.Reset(); OreLedger.Reset(); GoodsLedger.Reset(); FreeSupplies.Reset(); VillageWoodlot.Reset(); SpoilsSeal.Reset(); MarketRoad.Reset(); MarketCarts.Reset(); VillageClogDiag.Reset(); CartTownExit.Reset(); MoneyLedger.Reset(); PeopleLedger.Reset(); CaravanBulk.Reset(); StartStock.Reset(); MineralOnce.Reset(); SoldierPay.Reset(); KingdomTreasury.Reset(); RecruitCost.Reset(); NightRest.ResetWorld(); WorldMeasure.Reset(); CirculationWindows.Reset(); ClanIncomeBook.Reset(); LosersFlee.Reset(); PrisonerLaw.Reset(); RecruitSources.Reset(); GarrisonCarts.Reset(); GarrisonArmory.Reset(); ArmsDrill.Reset(); CaravanAmmo.Reset(); ArmsLeaks.Reset(); SupplyDemand.ResetOrders(); MaterialOrders.Reset(); RawNoRot.Reset(); ArmsScrap.Reset(); }   // stan jednej kampanii nie przecieka do drugiej (audyt 04.10)

        public override void SyncData(IDataStore dataStore)
        {
            // zapas startowy rudy i drewna przeliczony na ladunki raz na kampanie (StartStock); brak klucza = stary zapis.
            // Osobny try PRZED reszta: wyjatek innego klucza nie moze zgubic tej flagi - zgubiona flaga i wczytanie w pierwszej
            // dobie kampanii to drugie dzielenie zapasu przez przelicznik ladunku.
            try
            {
                string startStock = StartStock.Export();
                SaveText.Sync(dataStore, "arm_startstock", ref startStock);
                if (dataStore.IsLoading) StartStock.Import(startStock);
            }
            catch (Exception e) { Log.Error("SyncData.StartStock", e); }
            try
            {
                dataStore.SyncData("arm_condition", ref _condition);
                dataStore.SyncData("arm_projects", ref _projects);
                dataStore.SyncData("arm_player_stock", ref _playerStock);
                dataStore.SyncData("arm_ammo_refund_v1", ref _ammoRefundDone);
                dataStore.SyncData("arm_lore_purge_v1", ref _lorePurged);
                if (dataStore.IsSaving) _armoryWear = BuildArmoryWearSnapshot();
                dataStore.SyncData("arm_armory_wear", ref _armoryWear);
                if (dataStore.IsLoading && _armoryWear != null && _armoryWear.Count > 0) _wearRestorePending = true;
                dataStore.SyncData("arm_smith_helper", ref SmithHelperId);
                dataStore.SyncData("arm_uniq_lore", ref UniqueLore);
                dataStore.SyncData("arm_restitution_0831", ref _restitution0831);
                dataStore.SyncData("arm_orders", ref Orders.Board);
                dataStore.SyncData("arm_order_cooldowns", ref Orders.Cooldowns);
                // dniowka w kuzni MUSI przezyc save/load - inaczej po wczytaniu
                // gra znow kaze placic 25/h za oplacona juz dobe
                string daypass = DayPass.Export();
                SaveText.Sync(dataStore, "arm_daypass", ref daypass);
                if (dataStore.IsLoading) DayPass.Import(daypass);
                string nightrest = NightRest.Export();
                SaveText.Sync(dataStore, "arm_nightrest", ref nightrest);
                if (dataStore.IsLoading) NightRest.Import(nightrest);
                // ludnosc krain: ludzi na punkt hearth/dobrobytu (Jeff 04.10)
                string popk = PopulationLaw.Export();
                SaveText.Sync(dataStore, "arm_population", ref popk);
                if (dataStore.IsLoading) PopulationLaw.Import(popk);
                // prawo wyrzutkow: pula ludzi w regionach (Jeff 04.10)
                string outlaws = OutlawLaw.Export();
                SaveText.Sync(dataStore, "arm_outlaws", ref outlaws);
                if (dataStore.IsLoading) OutlawLaw.Import(outlaws);
                // klimat Westeros: historia por roku (Jeff 04.10)
                // unikaty ROT: kopie startowe zdjete raz na kampanie (wpis 62)
                // dorobek stuleci: zbrojownie i zapas kupiecki raz na kampanie (wpis 79)
                // sakiewki ludzi (wpis 84)
                string purse = MenPurse.Export();
                SaveText.Sync(dataStore, "arm_menpurse", ref purse);
                if (dataStore.IsLoading) MenPurse.Import(purse);
                // 150: odziez wojska - potrzeba czekajaca na zakup (partie i zalogi)
                string cloth = ArmyClothing.Export();
                SaveText.Sync(dataStore, "arm_armyclothing", ref cloth);
                if (dataStore.IsLoading) ArmyClothing.Import(cloth);
                // tarcza zoldu: znaczniki zoldu w kasach miast (paczka zold; puste, gdy tarcza wylaczona)
                string wagehold = SoldierPay.ExportHeld();
                SaveText.Sync(dataStore, "arm_wagehold", ref wagehold);
                if (dataStore.IsLoading) SoldierPay.ImportHeld(wagehold);
                string rk = RecruitKit.Export();
                SaveText.Sync(dataStore, "arm_recruitkit", ref rk);
                if (dataStore.IsLoading) RecruitKit.Import(rk);
                string aiw = AiWear.Export();
                SaveText.Sync(dataStore, "arm_aiwear", ref aiw);
                if (dataStore.IsLoading) AiWear.Import(aiw);
                string cold = ColdStart.Export();
                SaveText.Sync(dataStore, "arm_coldstart", ref cold);
                if (dataStore.IsLoading) ColdStart.Import(cold);
                string uniq = UniqueSpoils.Export();
                SaveText.Sync(dataStore, "arm_uniq_init", ref uniq);
                if (dataStore.IsLoading) UniqueSpoils.Import(uniq);
                string climate = WesterosClimate.Export();
                SaveText.Sync(dataStore, "arm_climate", ref climate);
                if (dataStore.IsLoading) WesterosClimate.Import(climate);
                // Bank Zelazny: dlugi rodow i kapital Banku (Jeff 04.10)
                string bank = IronBank.Export();
                SaveText.Sync(dataStore, "arm_ironbank", ref bank);
                if (dataStore.IsLoading) IronBank.Import(bank);
                string glut = MarketGlut.Export();
                SaveText.Sync(dataStore, "arm_glut", ref glut);
                if (dataStore.IsLoading) MarketGlut.Import(glut);
                // wiedza luczarska: odkryte wzory lukow/kusz i punkty nauki
                string lore = RangedLore.Export();
                SaveText.Sync(dataStore, "arm_rangedlore", ref lore);
                if (dataStore.IsLoading) RangedLore.Import(lore);
                // ksiega legend: raz wykuta legenda nigdy nie powstaje po raz drugi
                var legends = Legends;
                dataStore.SyncData("arm_legends", ref legends);
                if (dataStore.IsLoading && legends != null && !ReferenceEquals(legends, Legends))
                {
                    Legends.Clear();
                    Legends.AddRange(legends);
                }
                if (_projects == null) _projects = new List<string>();
                if (_condition == null) _condition = new List<string>();
                if (_playerStock == null) _playerStock = new Dictionary<string,int>();
            }
            catch (Exception e) { Log.Error("SyncData", e); }
            // paczka 169: dochod staly rodow D (pierscien 28 dob, klucz Clan.StringId) - osobny try, dane tylko do logu (166/168 dopiero beda ich uzywac);
            // brak klucza (stary zapis) = pusta ksiega, start D = G/60 w pierwszym Daily
            try
            {
                string ci = ClanIncomeBook.Export();
                SaveText.Sync(dataStore, "arm_clanincome", ref ci);
                if (dataStore.IsLoading) ClanIncomeBook.Import(ci);
            }
            catch (Exception e) { Log.Error("SyncData.ClanIncome", e); }
            // zapas kowali miast z napraw u kwatermistrza Spoils (MendMaterial: reszty calych sztuk materialu zdjetych z polki, kg i wartosc);
            // osobny try - wyjatek innego klucza go nie gubi; brak klucza (stary zapis) = pusty zapas
            try
            {
                string mend = MendMaterial.Export();
                SaveText.Sync(dataStore, "arm_mendstock", ref mend);
                if (dataStore.IsLoading) MendMaterial.Import(mend);
            }
            catch (Exception e) { Log.Error("SyncData.MendStock", e); }
            // 171: zamowienia zamkow w drodze i zbrojownie zalog (DTE ich nie zapisuje) - kazdy klucz we wlasnym try; Export tylko przy zapisie
            // (krytyka 8: przy wczytaniu Export przechodzilby osady i zbrojownie DTE w trakcie deserializacji); brak klucza = null
            try { string gc = dataStore.IsSaving ? GarrisonCarts.Export() : null; SaveText.Sync(dataStore, "arm_garrisoncarts", ref gc); if (dataStore.IsLoading) GarrisonCarts.Import(gc); }
            catch (Exception e) { Log.Error("SyncData.arm_garrisoncarts", e); }
            try { string ga = dataStore.IsSaving ? GarrisonArmory.Export() : null; SaveText.Sync(dataStore, "arm_garrisonarmory", ref ga); if (dataStore.IsLoading) GarrisonArmory.Import(ga); }
            catch (Exception e) { Log.Error("SyncData.arm_garrisonarmory", e); }
            // 174.0b: robota w toku warsztatow zbrojnych (sztuki zaczete, dlug ulamkowy surowca) i zamowienia - kazdy klucz we wlasnym try; brak klucza = stary zapis (pusto)
            try { string ws = dataStore.IsSaving ? WorkshopLaw.Export() : null; SaveText.Sync(dataStore, "arm_workshops", ref ws); if (dataStore.IsLoading) WorkshopLaw.Import(ws); }
            catch (Exception e) { Log.Error("SyncData.arm_workshops", e); }
            try { string un = dataStore.IsSaving ? SupplyDemand.ExportOrders() : null; SaveText.Sync(dataStore, "arm_unmet", ref un); if (dataStore.IsLoading) SupplyDemand.ImportOrders(un); }
            catch (Exception e) { Log.Error("SyncData.arm_unmet", e); }
            // 174.2: kontrakty surowca w drodze (karawana, cel, zrodlo, surowiec, ilosc) - zawsze, takze przy wylaczonym wylaczniku (po wczytaniu karawana zwolniona)
            try { string mo = dataStore.IsSaving ? MaterialOrders.Export() : null; SaveText.Sync(dataStore, "arm_matorders", ref mo); if (dataStore.IsLoading) MaterialOrders.Import(mo); }
            catch (Exception e) { Log.Error("SyncData.arm_matorders", e); try { if (dataStore.IsLoading) MaterialOrders.ImportFailed(); } catch { } }   // recenzja 174: linia, ze kontrakty pominiete
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, _ => DayPass.Clear());   // przeglad 07.10: doby kuzni z poprzedniej gry
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.OnItemProducedEvent.AddNonSerializedListener(this, OreLedger.OnProduced);   // wpis 98: ksiega rudy i drewna - sztuki faktycznie dopisane osadom
            CampaignEvents.OnItemConsumedEvent.AddNonSerializedListener(this, OreLedger.OnConsumed);   // wpis 98: wsad linii towarowych (narzedzia, deski)
            // pelny system rynku uzbrojenia (Jeff 04.10, docs/MODEL-MATERIALOW.md)
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, ArmsPricing.OnWarDeclared);
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, MenPurse.OnEntered);   // wpis 84: nadwyzki ludzi PRZED zakupami
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, AiGear.OnSettlementEntered);
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, CaravanAmmo.OnEntered);   // paczka 172b: amunicja z taborow karawany na polke miasta (cena rynkowa, kasa miasta); dopisana PRZED CaravanBulk, wiec idzie PO nim - surowce, ktorych miastu brakuje, pierwsze do kasy miasta
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, CaravanBulk.OnEntered);   // wpis 103: karawana sprzedaje miastu surowiec masowy, ktorego mu brakuje
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, MaterialOrders.OnEntered);   // 174.2: dostawa kontraktu surowca - dopisana PO CaravanBulk, wiec idzie PRZED nim (najpierw zamowiony ladunek)
            CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, MenPurse.OnLeft);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, delegate { try { TroopSelfMend.Hourly(); } catch { } });
            CampaignEvents.DailyTickPartyEvent.AddNonSerializedListener(this, AiGear.OnDailyTickParty);
            CampaignEvents.DailyTickPartyEvent.AddNonSerializedListener(this, UniqueSpoils.OnDailyTickParty);
            CampaignEvents.DailyTickPartyEvent.AddNonSerializedListener(this, ArmyClothing.OnDailyTickParty);   // 150: zuzycie odziezy w partiach rodow
            // SUWAKI MCM NA ZYWO (Jeff 03.09: "nadal 1 predkosc, o co chodzi" -
            // World Pace Percent przestawiony w grze nie dzialal). McmSettings.Apply()
            // szlo TYLKO w OnGameStart, wiec kazda zmiana w Mod Options czekala
            // na restart. Nasze pola MCM to zwykle auto-property bez powiadomien,
            // wiec zamiast zgadywac zdarzenia MCM przepisujemy wartosci co godzine
            // gry - tanie (kilkaset przypisan), a suwak wchodzi po paru sekundach.
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this,
                delegate { try { McmSettings.Apply(); } catch { } try { AmmoTracer.HourlyCheck(); } catch { } });
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, WarLedger.OnOwnerChanged);
            // sprzet startowy dopasowany do umiejetnosci (Jeff 04.10)
            CampaignEvents.OnCharacterCreationIsOverEvent.AddNonSerializedListener(this, delegate { try { StartKit.OnCharacterCreationOver(); } catch { } });   // wpis 86 (audyt pkt 17): limit zlota startowego niezalezny od StartKitEnabled
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, delegate { try { StartKit.Hourly(); } catch { } });
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, delegate { MapClock.ApplySpeed(); });   // gra wraca do x4 przy wczytaniu
            // prawo wyrzutkow (Jeff 04.10): pula ludzi wyjetych spod prawa
            CampaignEvents.OnTroopsDesertedEvent.AddNonSerializedListener(this, OutlawLaw.OnTroopsDeserted);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OutlawLaw.OnMapEventEnded);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, BattleChronicle.OnMapEventEnded);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, AiWear.OnMapEventEnded);   // wpis 85
            CampaignEvents.VillageLooted.AddNonSerializedListener(this, OutlawLaw.OnVillageLooted);
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, GoodsLedger.OnPartyDestroyed);  // paczka 146: towar, ktory przepada z partia (tylko log)
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, MaterialOrders.OnPartyDestroyed);   // 174.2: kontrakt przepada z rozbita karawana (ladunek - jak w grze)
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OutlawLaw.OnPartyDestroyed);
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, MenPurse.OnPartyDestroyed);   // wpis 89
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, ArmyClothing.OnPartyDestroyed);   // 150
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, MarketRoad.OnPartyDestroyed);   // wpis 100: rozbite tabory wiesniakow (log)
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, VillageClogDiag.OnPartyDestroyed);   // diagnoza zatkanych wsi: kiedy wies stracila woz (tylko log)
            // ksiega pieniadza i przeplywow osad (K1) oraz ksiega ludzi (demografia, krok 1) - same nasluchy, tylko log
            CampaignEvents.BeforeSettlementEnteredEvent.AddNonSerializedListener(this, MoneyLedger.OnBeforeEntered);   // tabor wsi: stan PRZED sprzedaza / podzialem utargu
            CampaignEvents.AfterSettlementEntered.AddNonSerializedListener(this, MoneyLedger.OnAfterEntered);          // ... i PO
            CampaignEvents.HeroOrPartyTradedGold.AddNonSerializedListener(this, MoneyLedger.OnGoldTraded);             // kazdy GiveGoldAction gry
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, CirculationWindows.OnPartyDestroyed);    // paczka 169: kiesy partii bez wodza, ktore znikaja z mapy (tylko log)
            CampaignEvents.KingdomDestroyedEvent.AddNonSerializedListener(this, CirculationWindows.OnKingdomDestroyed); // paczka 169: skarbce krolestw, ktore upadly (tylko log)
            CampaignEvents.PlayerInventoryExchangeEvent.AddNonSerializedListener(this, SellByCondition.OnPlayerExchange);   // cena sprzedazy sprzetu: kazda sprzedaz gracza do handel.log (tylko log)
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, PeopleLedger.OnMapEventEnded);
            CampaignEvents.OnTroopRecruitedEvent.AddNonSerializedListener(this, PeopleLedger.OnTroopRecruited);
            CampaignEvents.OnUnitRecruitedEvent.AddNonSerializedListener(this, PeopleLedger.OnUnitRecruited);
            CampaignEvents.OnTroopsDesertedEvent.AddNonSerializedListener(this, PeopleLedger.OnTroopsDeserted);
            CampaignEvents.OnPrisonerReleasedEvent.AddNonSerializedListener(this, PrisonerLaw.OnReleased);   // I1: gracz wypuscil jencow - w krainie bez niewoli do domu / na Mur
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, delegate { try { OutlawLaw.Hourly(); } catch { } });
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
            // polegli oddaja rynsztunek na wozy zaraz po bitwie
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this,
                delegate (MapEvent me) { try { if (me != null && me.IsPlayerMapEvent) GatherFallen(); } catch { } try { if (me != null && me.IsPlayerMapEvent) WearKeep.AfterBattle(); } catch { } try { if (me != null && me.IsPlayerMapEvent) AmmoRecovery.AfterBattle(); } catch { } });
            CampaignEvents.OnNewItemCraftedEvent.AddNonSerializedListener(this, OnNewItemCrafted);
            // DEPOZYT KWATERMISTRZA NIE MA PRAWA WEJSC DO SAVE'A: schowane
            // na czas ekranu zbrojowni sztuki zyja poza rosterem - zapis gry
            // w tym oknie utrwalilby save BEZ nich (27.08 bylo o wlos: save
            // 16:33, schowanie 1177 szt. o 16:34). Przed kazdym zapisem
            // wszystko wraca na polki.
            CampaignEvents.OnBeforeSaveEvent.AddNonSerializedListener(this,
                delegate { try { QuartermasterEscrow.SafetyRelease(); } catch { } });
            // jeniec wziety po bitwie zostaje obszukany - jego rynsztunek idzie do sakw
            CampaignEvents.OnPrisonerTakenEvent.AddNonSerializedListener(this, OnPrisonerTaken);
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, UniqueSpoils.OnPrisonerTaken);
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, UniqueSpoils.OnHeroKilled);
            // DIAGNOSTYKA (Jeff 29.08: "awans wycina sprzet z magazynu?") -
            // ani DTE, ani my nie sluchamy awansow, wiec logujemy sume
            // magazynu przy kazdym awansie: jak suma spada miedzy wpisami,
            // zlodziej istnieje; jak stoi - to zmiana wzorca po awansie.
            CampaignEvents.PlayerUpgradedTroopsEvent.AddNonSerializedListener(this, OnPlayerUpgradedTroops);
            // klawisz O na mapie: szybki oboz (BannerKings) bez klikania przez ekrany
            CampaignEvents.TickEvent.AddNonSerializedListener(this, delegate (float dt)
            {
                NightRest.OnTick(dt);
                try { CrossingLaw.OnTick(dt); } catch { }
            });
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this,
                delegate (MobileParty mp, Settlement st, Hero h)
                {
                    try { Orders.OnSettlementEntered(mp, st); } catch { }
                    try { Stables.OnSettlementEntered(mp, st); } catch { }
                    try { if (mp == MobileParty.MainParty) ElephantQuarantine.Sweep(st); } catch { }
                });
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this,
                delegate (Settlement st)
                {
                    try { ElephantQuarantine.Sweep(st); } catch { }
                    try { TroopSelfMend.Run(st); } catch { }
                });
            // menu kucia otwarte JAKAKOLWIEK droga (takze wznowione z save'a,
            // z pominieciem StartCraftingMenu) - dniowka kupuje sie od razu
            CampaignEvents.GameMenuOpened.AddNonSerializedListener(this, OnGameMenuOpened);
            // T6 (noc 08/09.10): miara marszu - pozycje partii lordow co godzine (tylko log, wylacznik WorldMeasureLog)
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, delegate { try { WorldMeasure.Hourly(); } catch (Exception e) { Log.Error("WorldMeasure.Hourly", e); } });
            // pas bezpieczenstwa depozytu kwatermistrza: czas plynie = ekran
            // zbrojowni zamkniety; gdyby domkniecie nie oddalo sprzetu, oddajemy tu
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this,
                delegate
                {
                    try { QuartermasterEscrow.SafetyRelease(); } catch { }
                    try { AdvanceProjects(); } catch (Exception e) { Log.Error("AdvanceProjects(h)", e); }
                    try { TryStripNewCaptives("hourly"); } catch { }
                    try { NightRest.OnHourly(); } catch { }
                    try
                    {
                        // ekran lupow nie odebral kolejki w godzine - do sakw z nia
                        if (_lootFlushDue != CampaignTime.Zero && CampaignTime.Now > _lootFlushDue)
                        {
                            _lootFlushDue = CampaignTime.Zero;
                            if (BattlefieldLaw.SharePending()) BattlefieldLaw.FlushShareToBaggage("ekran lupow nie przyszedl");
                        }
                    }
                    catch { }
                    try
                    {
                        // pobitewny meldunek kwatermistrza - juz PO wchlonieciu lupow
                        if (_shortageShoutDue != CampaignTime.Zero && CampaignTime.Now > _shortageShoutDue)
                        {
                            _shortageShoutDue = CampaignTime.Zero;
                            QuartermasterLaw.ShoutShortages("QM short (have/need):");
                        }
                    }
                    catch { }
                });
        }

        private void OnGameMenuOpened(TaleWorlds.CampaignSystem.GameMenus.MenuCallbackArgs args)
        {
            try
            {
                // gotowe wyroby z kuzni wydaja sie od progu, bez czekania na tick
                try { CollectReadyProjects(); } catch { }
                // stany magazynu wracaja PRZED czystkami (DTE odtwarza roster
                // dopiero po sesji - lapiemy pierwszy moment, gdy juz jest)
                try { TryRestoreArmoryWear("menu"); } catch { }
                // ZWROT WYLACZONY 30.08 - Jeff wyjasnil, ze wklad MA przechodzic
                // do wojska; oddawanie kolczanow na polke gracza byloby wbrew temu
                // po kazdej bitwie menu sie otwiera - smok wyleci zanim DTE go osiodla
                try { CleanseDragonStables(true); } catch { }
                // ...a smieci <=3% i slonie-towar zaraz za nim (Spoils naklada
                // stany PO naszym filtrze lupow - tu wymiatamy je od reki)
                try { CleanseTrashInBags(); } catch { }
                // CZYSTKA MAGAZYNU WYLACZONA (Jeff 03.09: "nie ma byc wymiana 1:1,
                // nic nie znika" - 2 z 3 zdobytych pancerzy przepadly, bo limit
                // TrimWarStores = liczba zolnierzy, a mial 2 ludzi). Zdjety sprzet
                // zostaje w magazynie DTE w calosci; TrimWarStores zostaje w kodzie
                // jako martwy na wypadek powrotu do pomyslu z 29.08.
                // try { if (CampaignTime.Now <= _spoilsWindow) TrimWarStores(); } catch { }
                // samonaprawa depozytu: otwarte menu gry = na pewno NIE ekran
                // zbrojowni; jesli cokolwiek wisi w depozycie (Release nie
                // odpalil przy zamykaniu ekranu), wraca na polki teraz
                try { QuartermasterEscrow.SafetyRelease(); } catch { }
                var gm = args != null && args.MenuContext != null ? args.MenuContext.GameMenu : null;
                if (gm != null && gm.StringId == "bannerkings_wait_crafting")
                {
                    DayPass.EnsureBought();
                    // BLAD CZASU (Jeff, throwing axes): menu odczekiwania godzin
                    // kuzni BK potrafi otworzyc sie z zatrzymanym czasem i starym
                    // zegarem - odpalamy czas OD RAZU i liczymy godziny od TERAZ,
                    // zeby nie doczekiwac ani chwili ponad wykupione godziny
                    try
                    {
                        gm.StartWait();
                        var tAct = QuartermasterLaw.FindType("BannerKings.Behaviours.BKSettlementActions");
                        if (tAct != null)
                        {
                            var f = HarmonyLib.AccessTools.Field(tAct, "actionStart");
                            if (f != null)
                            {
                                object beh = null;
                                if (!f.IsStatic)
                                {
                                    var mGet = typeof(Campaign).GetMethod("GetCampaignBehavior");
                                    if (mGet != null) beh = mGet.MakeGenericMethod(tAct).Invoke(Campaign.Current, null);
                                }
                                if (f.IsStatic || beh != null) f.SetValue(f.IsStatic ? null : beh, CampaignTime.Now);
                            }
                        }
                    }
                    catch (Exception e2) { Log.Error("BkWaitFix", e2); }
                }
                TryStripNewCaptives("menu");   // pierwsze menu po bitwie - jency juz przypisani
            }
            catch (Exception e) { Log.Error("OnGameMenuOpened", e); }
        }

        /// <summary>Wegiel drzewny wazy u nas 5 kg od sztuki - absurd; sadzowa bryla to pol kilo.</summary>
        private static void FixCharcoalWeight()
        {
            var c = Settings.Current;
            if (c.CharcoalWeight <= 0f) return;
            var coal = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemObject>("charcoal");
            if (coal == null || Math.Abs(coal.Weight - c.CharcoalWeight) < 0.01f) return;
            var f = typeof(ItemObject).GetProperty("Weight");
            if (f != null && f.CanWrite) { f.SetValue(coal, c.CharcoalWeight, null); }
            else
            {
                var bf = typeof(ItemObject).GetField("<Weight>k__BackingField",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (bf != null) bf.SetValue(coal, c.CharcoalWeight);
            }
            Log.Info("Wegiel drzewny: waga " + coal.Weight + " kg/szt.");
        }

        /// <summary>
        /// SANACJA SAKW. Ujemny stos w ItemRoster (pozostalosc po bledzie
        /// przetopu z 27.08 - AddToCounts(-1) na nieistniejacym wpisie) wywraca
        /// caly ekran ekwipunku: kazda kategoria swieci pustka. Przy kazdym
        /// wczytaniu zerujemy takie wpisy w sakwach i w zbrojowni DTE.
        /// </summary>
        private static void CleanseNegativeStacks()
        {
            int fixedTotal = 0;
            foreach (var roster in new[] { MobileParty.MainParty != null ? MobileParty.MainParty.ItemRoster : null,
                                           QuartermasterLaw.DteArmory() })
            {
                if (roster == null) continue;
                try
                {
                    for (int i = roster.Count - 1; i >= 0; i--)
                    {
                        var el = roster.GetElementCopyAtIndex(i);
                        if (el.Amount >= 0) continue;
                        roster.AddToCounts(el.EquipmentElement, -el.Amount);   // do zera
                        fixedTotal++;
                        Log.Info("Sanacja sakw: " + (el.EquipmentElement.Item != null ? el.EquipmentElement.Item.StringId : "?")
                                 + " mial stan " + el.Amount + " - wyzerowany.");
                    }
                }
                catch (Exception e) { Log.Error("CleanseNegativeStacks", e); }
            }
            if (fixedTotal > 0)
                Log.Player("The quartermaster set the ledgers straight - " + fixedTotal + " ruined entries struck out.", true);
        }

        /// <summary>Smieci ponizej progu zniszczenia precz z sakw i magazynu
        /// (Jeff 29.08: "usun jednak te 3%, to nie ma sensu") - stare wraki
        /// sprzed progu; nowych prog nie wpuszcza do lupow.</summary>
        internal static void CleanseTrashInBags()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || s.LootMinConditionPercent <= 0) return;
                int cut = 0;
                foreach (var roster in new[] { MobileParty.MainParty != null ? MobileParty.MainParty.ItemRoster : null,
                                               QuartermasterLaw.DteArmory() })
                {
                    if (roster == null) continue;
                    for (int i = roster.Count - 1; i >= 0; i--)
                    {
                        var el = roster.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (el.Amount <= 0 || it == null) continue;
                        // slonie-towar precz (Jeff: "slonie ma Zlota Kompania")
                        if (it.StringId != null && (it.StringId == "elephant" || it.StringId.StartsWith("rot_elephant")))
                        {
                            roster.AddToCounts(el.EquipmentElement, -el.Amount);
                            cut += el.Amount;
                            continue;
                        }
                        // legendy zalegajace z dawnych lupow w NASZYM magazynie
                        // (screen Jeffa: [STORES] Lady Forlorn) - precz
                        if (LegendaryLaw.IsLegend(it))
                        {
                            roster.AddToCounts(el.EquipmentElement, -el.Amount);
                            cut += el.Amount;
                            continue;
                        }
                        // sprzet olbrzymow w ludzkich sakwach/magazynie (Jeff 14.09) -
                        // precz, chyba ze w kompanii sluza olbrzymy
                        if ((GiantGear.Is(it) && !GiantGear.PartyHasGiants(MobileParty.MainParty))
                            || (MountLaw.IsExotic(it) && !MountLaw.AllowedForParty(MobileParty.MainParty, it)))
                        {
                            roster.AddToCounts(el.EquipmentElement, -el.Amount);
                            cut += el.Amount;
                            continue;
                        }
                        // PROG KONDYCJI WYCIETY (Jeff 31.08: "wzialem pancerz
                        // z bitwy i znika w inventory"). Zuzyty lup to surowiec
                        // do NAPRAWY, nie smiec - a czystka zjadala tez WLASNY
                        // sprzet wojska zuzyty w walce (rusty_sword stan 6).
                        // Prawdziwy zlom odsiewa prog wrakow NA POLU, zanim
                        // cokolwiek trafi na ekran lupow. Decyzji gracza
                        // ("biore") nikt nie nadpisuje.
                    }
                }
                if (cut > 0)
                {
                    Log.Info("CleanseTrashInBags: " + cut + " szt. (slonie-towar, zalegle legendy) wyrzucono z sakw/magazynu.");
                    Log.Player("The ruined scraps are thrown out - " + cut + " pieces past any mending.", true);
                }
            }
            catch (Exception e) { Log.Error("CleanseTrashInBags", e); }
        }

        // sklad wlasnej partii na progu bitwy - roznica po bitwie = polegli
        private Dictionary<CharacterObject, int> _fallenSnapshot;

        internal void SnapshotOwnRanks()
        {
            try
            {
                var roster = MobileParty.MainParty != null ? MobileParty.MainParty.MemberRoster : null;
                if (roster == null) { _fallenSnapshot = null; return; }
                var map = new Dictionary<CharacterObject, int>();
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    if (el.Character == null || el.Character.IsHero) continue;
                    int v; map.TryGetValue(el.Character, out v);
                    map[el.Character] = v + el.Number;
                }
                _fallenSnapshot = map;
            }
            catch { _fallenSnapshot = null; }
        }

        /// <summary>
        /// ZBIERAMY POLEGLYCH (Jeff 29.08: "zabici zolnierze MOJEJ armii -
        /// ich ekwipunek ma zasilic armoury i moge go zabrac; teraz znika").
        /// Po bitwie roznica skladu = polegli; za kazdego jego rynsztunek
        /// (wedle szablonu, ze stanem bojowym, bez choragwi/smokow/legend)
        /// wraca na wozy i jest PRZEKSIEGOWANY NA GRACZA - dowodca dysponuje
        /// sprzetem poleglych, jak w kazdej kompanii tamtych czasow.
        /// </summary>
        internal void GatherFallen()
        {
            try
            {
                if (_fallenSnapshot == null) return;
                var before = _fallenSnapshot; _fallenSnapshot = null;
                var roster = MobileParty.MainParty != null ? MobileParty.MainParty.MemberRoster : null;
                var armory = QuartermasterLaw.DteArmory();
                if (roster == null || armory == null) return;

                var now = new Dictionary<CharacterObject, int>();
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    if (el.Character == null || el.Character.IsHero) continue;
                    int v; now.TryGetValue(el.Character, out v);
                    now[el.Character] = v + el.Number;
                }

                int men = 0, pieces = 0;
                foreach (var kv in before)
                {
                    int left; now.TryGetValue(kv.Key, out left);
                    int fallen = kv.Value - left;
                    for (int m = 0; m < fallen; m++)
                    {
                        Equipment eq = null; int n = 0;
                        foreach (var e in kv.Key.BattleEquipments) { n++; if (MBRandom.RandomInt(n) == 0) eq = e; }
                        if (eq == null) continue;
                        men++;
                        for (int slot = 0; slot < 12; slot++)
                        {
                            if (slot == 4) continue;
                            var item = eq[(EquipmentIndex)slot].Item;
                            if (item == null || item.ItemType == ItemObject.ItemTypeEnum.Banner) continue;
                            if (item.StringId != null && item.StringId.StartsWith("dragon_")) continue;
                            if (LegendaryLaw.IsLegend(item) || GiantGear.Is(item)) continue;
                            // PRAWO ZACHOWANIA LUPU (audyt 30.08): FIZYCZNE sztuki
                            // poleglych zwraca juz DTE (ItemsToRecover -> magazyn) -
                            // dosypywanie szablonu bylo DRUGIM kompletem tej samej
                            // zbroi. My tylko PRZEKSIEGOWUJEMY na gracza; gdyby DTE
                            // czegos nie zwrocil, ksiege-ducha przytnie ReconcileStock.
                            StockDeposit(item.StringId, 1);   // dowodca dysponuje sprzetem poleglych
                            pieces++;
                        }
                    }
                }
                if (pieces > 0)
                {
                    Log.Info("GatherFallen: " + men + " poleglych - " + pieces
                             + " sztuk przeksiegowanych na gracza (fizyczne sztuki zwraca DTE, bez dosypywania).");
                    Log.Player("The fallen are gathered - " + pieces + " pieces of their kit come back on the wagons, yours to claim.", true);
                }
            }
            catch (Exception e) { Log.Error("GatherFallen", e); }
        }

        /// <summary>
        /// MARTWE OD 03.09 (Jeff: "nie ma byc wymiana 1:1, nic nie znika") - nie
        /// wolane nigdzie; zostawione na wypadek powrotu do pomyslu z 29.08.
        /// WYMIENIONY SPRZET ZNIKA (Jeff 29.08: "wojsko przezbraja sie w lupy,
        /// a starocie po prostu znikaja"). Wojskowa czesc magazynu (ponad wklady
        /// gracza) trzyma per TYP najwyzej tylu sztuk, ilu ludzi w kompanii
        /// (amunicja x2) - najlepsze zostaja, gorsza nadwyzka idzie w niepamiec.
        /// Wklady gracza i sztuki przypisane w ksiedze - nietykalne.
        /// </summary>
        internal void TrimWarStores()
        {
            try
            {
                var armory = QuartermasterLaw.DteArmory();
                var roster = MobileParty.MainParty != null ? MobileParty.MainParty.MemberRoster : null;
                if (armory == null || roster == null) return;
                int men = 0;
                for (int i = 0; i < roster.Count; i++)
                {
                    var e = roster.GetElementCopyAtIndex(i);
                    if (e.Character != null && !e.Character.IsHero) men += e.Number;
                }
                if (men <= 0) men = 1;

                var byType = new Dictionary<ItemObject.ItemTypeEnum, List<ItemRosterElement>>();
                for (int i = 0; i < armory.Count; i++)
                {
                    var el = armory[i];
                    var it = el.EquipmentElement.Item;
                    if (it == null || el.Amount <= 0) continue;
                    List<ItemRosterElement> list;
                    if (!byType.TryGetValue(it.ItemType, out list))
                        byType[it.ItemType] = list = new List<ItemRosterElement>();
                    list.Add(el);
                }

                int trimmed = 0;
                foreach (var kv in byType)
                {
                    int keep = kv.Key == ItemObject.ItemTypeEnum.Arrows || kv.Key == ItemObject.ItemTypeEnum.Bolts
                        ? men * 2 : men;
                    // najgorsze na poczatek - te wylatuja pierwsze
                    kv.Value.Sort((a, b) => a.EquipmentElement.ItemValue.CompareTo(b.EquipmentElement.ItemValue));
                    int total = 0;
                    foreach (var el in kv.Value) total += el.Amount;
                    int over = total - keep;
                    if (over <= 0) continue;
                    foreach (var el in kv.Value)
                    {
                        if (over <= 0) break;
                        var it = el.EquipmentElement.Item;
                        var id = it.StringId ?? "";
                        int protectedHere = Math.Min(el.Amount, StockOf(id));      // wklad gracza swiety
                        if (MusterBook.IsPinnedItem(id)) protectedHere = el.Amount; // rozkaz z ksiegi swiety
                        int cuttable = el.Amount - protectedHere;
                        if (cuttable <= 0) continue;
                        int cut = Math.Min(cuttable, over);
                        armory.AddToCounts(el.EquipmentElement, -cut);
                        over -= cut; trimmed += cut;
                    }
                }
                if (trimmed > 0)
                    Log.Info("TrimWarStores: " + trimmed + " staroci wojskowych poszlo w niepamiec (wymienione znika).");
            }
            catch (Exception e) { Log.Error("TrimWarStores", e); }
        }

        /// <summary>Smoki precz z sakw i magazynu DTE - inaczej DTE wsadza kawalerzyste na smoka w bitwie.</summary>
        internal static void CleanseDragonStables(bool shout)
        {
            int gone = 0;
            foreach (var roster in new[] { MobileParty.MainParty != null ? MobileParty.MainParty.ItemRoster : null,
                                           QuartermasterLaw.DteArmory() })
            {
                if (roster == null) continue;
                try
                {
                    for (int i = roster.Count - 1; i >= 0; i--)
                    {
                        var el = roster.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (it == null || it.StringId == null || el.Amount <= 0) continue;
                        if (!it.StringId.StartsWith("dragon_")) continue;
                        if (it.ItemType != ItemObject.ItemTypeEnum.Horse) continue;
                        roster.AddToCounts(el.EquipmentElement, -el.Amount);
                        gone += el.Amount;
                        Log.Info("Smocza stajnia: " + it.StringId + " x" + el.Amount + " usuniety z zapasow.");
                    }
                }
                catch (Exception e) { Log.Error("CleanseDragonStables", e); }
            }
            if (gone > 0 && shout)
                Log.Player("The dragons were never yours to stable - " + gone + " struck from the rolls.", true);
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            SaveText.ReportAfterLoad();   // 161: ile dlugich napisow uratowal ratunek przy wczytaniu
            try { RecruitKit.ResolvePending("wczytanie"); } catch (Exception e) { Log.Error("RecruitKit.ResolvePending", e); }   // 161: komplety rekrutow dopiero teraz (w SyncData bohaterow nie ma)
            // 171: zamowienia zamkow w drodze; zbrojownie zalog z zapisu (DTE skonczyl OnGameLoaded) albo jednorazowe odtworzenie po starym zapisie - PRZED ColdStart.Run;
            // echo ROT, gdy typu nie bylo przy starcie; cwiczenia wlasna bronia - latki modeli w kampanii (modele BK czytaja singletony BK)
            try { GarrisonCarts.ResolvePending("wczytanie"); } catch (Exception e) { Log.Error("GarrisonCarts.ResolvePending", e); }
            try { WorkshopLaw.ResolvePending("wczytanie"); } catch (Exception e) { Log.Error("WorkshopLaw.ResolvePending", e); }   // 174.0b: robota w toku warsztatow z zapisu (przed pierwszym cyklem)
            try { MaterialOrders.ResolvePending("wczytanie"); } catch (Exception e) { Log.Error("MaterialOrders.ResolvePending", e); }   // 174.2: kontrakty surowca na karawany z zapisu
            try { GarrisonArmory.Restore("wczytanie"); } catch (Exception e) { Log.Error("GarrisonArmory.Restore", e); }
            try { RecruitSources.ApplyLate(); } catch (Exception e) { Log.Error("RecruitSources.ApplyLate", e); }
            try { ArmsDrill.EnsureHooks(); } catch (Exception e) { Log.Error("ArmsDrill.EnsureHooks", e); }
            try { FixCharcoalWeight(); } catch (Exception e) { Log.Error("FixCharcoalWeight", e); }   // wpis 87 (audyt pkt 11d): waga wegla PRZED wycena
            try { LootPrices.Apply(); } catch (Exception e) { Log.Error("LootPrices", e); }   // wpis 97: cena lupu = stan
            try { McmSettings.Apply(); MaterialLaw.Apply(); ArmsPricing.Build(); HistoricalPrices.Apply(); StartStock.Run(); ArmsPricing.ClearCostCache(); MapClock.ApplySpeed(); UniqueSpoils.OnSessionLaunched(); ColdStart.Run(); } catch (Exception e) { Log.Error("MaterialLaw/ArmsPricing", e); }   // surowce PRZED wycena uzbrojenia; StartStock zaraz PO Apply (przelicznik ladunku juz obowiazuje)
            try { VillageClimate.Apply(); } catch (Exception e) { Log.Error("VillageClimate.Apply", e); }   // T8: typ wsi wedlug klimatu (PO McmSettings.Apply; bez zapisu)
            try { VillageWoodlot.Calibrate(true); } catch (Exception e) { Log.Error("VillageWoodlot.Calibrate", e); }   // T8: stala lasu wedlug klimatu PO zmianie typow (drwale)
            try { Stables.BuildRiderMap(); } catch (Exception e) { Log.Error("Stables.BuildRiderMap", e); }
            try { MountedWage.EnsureContextHooks(); } catch (Exception e) { Log.Error("MountedWage.EnsureContextHooks", e); }   // paczka 160: zold partii - karawany bez premii konnego (latka w kampanii)
            try { CirculationWindows.EnsureModelHooks(); } catch (Exception e) { Log.Error("CirculationWindows.EnsureModelHooks", e); }   // paczka 169: linie modelu finansow i kapital nowych karawan (latki w kampanii, tylko log)
            try { RawPrice.SeedNewCampaign(); } catch (Exception e) { Log.Error("RawPrice.SeedNewCampaign", e); }   // cena surowcow: w nowej kampanii pamiec rynku z tickow startowych na nowa monete - PO HistoricalPrices.Apply i StartStock.Run
            try { TownCrafts.SessionStart(); } catch (Exception e) { Log.Error("TownCrafts.SessionStart", e); }   // paczka 148: proporcje rzemiosla miasta z wartosci - PO HistoricalPrices.Apply (linia startowa)
            try { CaravanAmmo.SessionStart(); } catch (Exception e) { Log.Error("CaravanAmmo.SessionStart", e); }   // paczka 172b: kategorie amunicji i stan latek (linia startowa)
            try { ArmsLeaks.SessionStart(); } catch (Exception e) { Log.Error("ArmsLeaks.SessionStart", e); }   // paczka 174.0: linia startowa (ujscia uzbrojenia zamkniete, BK bron i tarcze, konserwacja zapasu)
            try { WorkshopLaw.HandsStartLine(); } catch (Exception e) { Log.Error("WorkshopLaw.HandsStartLine", e); }   // 174.3: linia "Rece (174)" - po ColdStart (ten liczy dawnymi rekami)
            try { TownFletchers.SessionStart(); } catch (Exception e) { Log.Error("TownFletchers.SessionStart", e); }   // paczka 172: kandydaci strzelarzy, koszyki wzorcow, linia startowa - PO HistoricalPrices.Apply i ColdStart
            // 124: kapital startowy warsztatow w nowej monecie - TU, po HistoricalPrices.Apply. Gra wola sluchaczy zdarzenia od ostatnio
            // dopisanego (MbEvent: lista z wstawianiem na poczatek), wiec WorkshopTradeBehavior (dodany po nas) szedl PRZED przeliczeniem cen
            try { var seed = WorkshopTrade.SeedNewCampaign(); if (seed != null) Log.Info("WorkshopTrade: " + seed); } catch (Exception e) { Log.Error("WorkshopTrade.SeedNewCampaign", e); }
            try { WearGroups.Fix(); } catch (Exception e) { Log.Error("WearGroups.Fix", e); }
            try { SellByCondition.OnSessionLaunched(); } catch (Exception e) { Log.Error("SellByCondition.OnSessionLaunched", e); }   // cena sprzedazy sprzetu: wylacznik kary BK z MCM (PO McmSettings.Apply wyzej) i kontrola "Kara handlowa BK: x5.0"; ksiega skupu od zera
            try { CleanseNegativeStacks(); } catch (Exception e) { Log.Error("CleanseNegativeStacks", e); }
            try { TryRestoreArmoryWear("sesja"); } catch (Exception e) { Log.Error("TryRestoreArmoryWear", e); }
            try { LorePurgeOnce(); } catch (Exception e) { Log.Error("LorePurgeOnce", e); }
            try { CleanseDragonStables(true); } catch (Exception e) { Log.Error("CleanseDragonStables", e); }
            try { CleanseTrashInBags(); } catch (Exception e) { Log.Error("CleanseTrashInBags", e); }
            try { QualityRich.Enrich(); } catch (Exception e) { Log.Error("QualityRich.Enrich", e); }
            try { ValyrianSteel.Rename(); } catch (Exception e) { Log.Error("ValyrianSteel.Rename", e); }
            try { Restitution0831(); } catch (Exception e) { Log.Error("Restitution0831", e); }
            try { IronBank.AddMenus(starter); } catch (Exception e) { Log.Error("IronBank.AddMenus", e); }
            try { SmithMenu.Add(starter); Log.Info("Menu kowala dodane."); }
            catch (Exception e) { Log.Error("OnSessionLaunched", e); }
            try { CleanseAmmo(); } catch (Exception e) { Log.Error("CleanseAmmo", e); }
            try { NightRest.AddMenus(starter); } catch (Exception e) { Log.Error("NightRest.AddMenus", e); }
            try { RangedLore.ReportLedger(false); } catch (Exception e) { Log.Error("RangedLore.ReportLedger", e); }
            try { ArmyClothing.StartLine(); } catch (Exception e) { Log.Error("ArmyClothing.StartLine", e); }   // 150: stawki odziezy wojska i zaleznosci
        }

        private static float Today { get { return (float)CampaignTime.Now.ToDays; } }
        private static float _lastIdleLogDay = -10f;

        // ---------------------------------------------------------- robota przy kowadle
        internal void StartProject(ItemObject item, int tempo, float days, Settlement where)
        { StartProject(item, tempo, days, where, "", ""); }

        internal void StartProject(ItemObject item, int tempo, float days, Settlement where, string kind, string modifierId)
        { StartProject(item, tempo, days, where, kind, modifierId, 1); }

        internal void StartProject(ItemObject item, int tempo, float days, Settlement where, string kind, string modifierId, int count)
        {
            try
            {
                var p = new Project
                {
                    Item = item, DaysLeft = days, Tempo = tempo,
                    SettlementId = where != null ? where.StringId : "",
                    Kind = kind ?? "", ModifierId = modifierId ?? "", Count = Math.Max(1, count)
                };
                _projects.Add(p.Serialize());
                Log.Info("Projekt dodany: " + p.Serialize());
            }
            catch (Exception e) { Log.Error("StartProject", e); }
        }

        internal string ProjectSummary()
        {
            try
            {
                if (_projects.Count == 0) return "";
                var sb = new System.Text.StringBuilder();
                foreach (var line in _projects)
                {
                    var p = Project.Parse(line);
                    if (p.Item == null) continue;
                    if (sb.Length > 0) sb.Append("\n");
                    if (p.DaysLeft <= 0f)
                        sb.Append(p.Item.Name + " - READY, waiting for collection at the forge");
                    else
                        sb.Append(p.Item.Name + " - " + Project.TimeLabel(p.DaysLeft) + " of work left"
                                  + (Settings.Current.ForgeWorksWithoutYou ? "" : " (clock runs only while you stay there)")
                                  + ", worked " + p.TempoName);
                }
                return sb.ToString();
            }
            catch (Exception e) { Log.Error("ProjectSummary", e); return ""; }
        }

        internal bool HasProjects { get { return _projects.Count > 0; } }

        private static void HandOver(Project p)
        {
            if (p.Kind == "van" || p.Kind == "bk") Forge.Deliver(p.Item, p.ModifierId, Math.Max(1, p.Count));   // sukces zapadl przy kowadle
            else Forge.Finish(p.Item, p.Tempo);
        }

        private void AdvanceProjects()
        {
            try
            {
                if (_projects.Count == 0) return;
                // SUFIT Z SUWAKA (Jeff 16.09: "czekam na cztery miecze ponad 169 godzin"):
                // zlecenie broni zlozone przy starym przeliczniku ma DaysLeft policzone
                // raz, przy starcie - obnizka WeaponDaysPerTier sama go nie skroci.
                // Kazdy tick przycina wiec DaysLeft broni do nowego wzoru (ten sam co
                // przy starcie: max(0.1, tier * WeaponDaysPerTier)). Pancerze bez zmian.
                int capped = 0;
                for (int i = 0; i < _projects.Count; i++)
                {
                    var q = Project.Parse(_projects[i]);
                    if (q.Item == null || q.Kind != "van" || ForgeClock.On) continue;   // wpis 83: godziny z ekranu BK, bez przycinania
                    float cap = MathF.Max(0.1f, Recipes.Grade(q.Item) * Settings.Current.WeaponDaysPerTier);
                    if (q.DaysLeft > cap) { q.DaysLeft = cap; _projects[i] = q.Serialize(); capped++; }
                }
                if (capped > 0) Log.Info("Kuznia: " + capped + " zlecen broni w kolejce skrocono do nowego przelicznika (WeaponDaysPerTier " + Settings.Current.WeaponDaysPerTier + ").");
                var here = Settlement.CurrentSettlement;
                // KOWAL PRACUJE, KIEDY TY JEDZIESZ (Jeff 27.08: "wykulem miecz,
                // odczekalem dlugo i przepadl" - stary zegar tykal TYLKO, gdy
                // gracz stal w osadzie projektu, a on czekal na mapie obok).
                // Teraz robota idzie zawsze; XP za prace w trakcie dostajesz
                // tylko na miejscu (to twoje rece), a GOTOWY wyrob lezy
                // w warsztacie i czeka na odbior przy wejsciu do osady.
                // wpis 83 (Jeff 05.10: "nie kuje sie, jak mnie nie ma w miescie") - to Twoje rece przy kowadle
                bool remote = Settings.Current.ForgeWorksWithoutYou && !Settings.Current.ForgeOnlyWhileThere;
                var copy = new List<string>(_projects);
                bool idleWarned = false;
                // JEDNA LINIA CZASU U KOWALA (Jeff 28.08: "wytapiam miecze,
                // a moge dolozyc nastepne i ida rownolegle"). W kazdej osadzie
                // tyka TYLKO najstarszy niegotowy projekt - reszta czeka na
                // swoja kolej. Rozne miasta = rozni kowale, pracuja niezaleznie.
                var busyForge = new HashSet<string>();
                foreach (var line in copy)
                {
                    var p = Project.Parse(line);
                    if (p.Item == null) { _projects.Remove(line); continue; }

                    bool atForge = here != null && here.StringId == p.SettlementId;

                    if (p.DaysLeft <= 0f)
                    {
                        // gotowy wyrob lezy w warsztacie - wydanie na miejscu
                        if (atForge) { _projects.Remove(line); HandOver(p); }
                        continue;
                    }

                    // kowal tej osady juz kuje wczesniejszy projekt - ten czeka
                    if (!busyForge.Add(p.SettlementId)) continue;

                    if (!atForge && !remote)
                    {
                        if (!idleWarned && Today - _lastIdleLogDay > 0.99f)
                        {
                            idleWarned = true;
                            _lastIdleLogDay = Today;
                            Log.Info("Projekt stoi - gracz poza osada.");
                        }
                        continue;
                    }

                    // CZELADZ TEZ SPI (Jeff 31.08): warsztat stoi noca 23-5 -
                    // zadnego postepu przez szesc ciemnych godzin. "Dni pracy"
                    // projektu to dni PRZY KOWADLE, wiec robota trwa realnie
                    // ~1/3 dluzej kalendarza - jak w prawdziwej kuzni.
                    if (Settings.Current.WorkshopNightRest)
                    {
                        int hh = CampaignTime.Now.GetHourOfDay;
                        if (hh >= 23 || hh < 5) continue;
                    }

                    // kuznia za kazdy dzien roboty (Jeff 07.10): KAZDA robota, ktora idzie naprzod - wlasny projekt, sztuka z ekranu BK,
                    // bron kuta po vanillowemu; przy kowadle i pod nieobecnosc gracza (kowal pracuje bez ciebie) - placi dobe kuzni tej
                    // osady (jeden rejestr z karnetem BK); bez pieniedzy robota czeka. Przeglad 07.10: dotad tylko wlasny projekt, tylko
                    // przy kowadle i tylko przy jednym zegarze kuzni - reszta szla za darmo.
                    if (Settings.Current.ForgeHireHistorical)
                    {
                        bool rentOk = true;
                        try { rentOk = Forge.PayDayRent(atForge ? Settlement.CurrentSettlement : Settlement.Find(p.SettlementId)); } catch (Exception er) { Log.Error("PayDayRent", er); }
                        if (!rentOk) continue;
                    }

                    // KROK GODZINOWY: zegar konczy sie DOKLADNIE z robota, bez
                    // doczekiwania do polnocy (blad, ktory wkurzyl Jeffa przy mieczu)
                    p.DaysLeft -= 1f / 24f;
                    if (!Settings.Current.ForgeHireHistorical && atForge && ForgeClock.On && (p.Kind == "bk" || p.Kind == "van")) { try { DayPass.EnsureBought(); } catch { } }   // kuznia wynajeta na dobe
                    var rr = Recipes.For(p.Item);
                    // XP liczy sie od WLASCIWEGO czasu projektu: bron "van" ma swoj
                    // przelicznik (WeaponDaysPerTier), pancerze swoj (Jeff 29.08:
                    // "balagan z godzinami" - to byl jeden z rozjazdow)
                    // minimum 0.1 dnia (bylo 0.5 - Jeff 16.09: "kucie miecza za dlugie, -80%";
                    // przy 0.1 dnia/tier pol dnia podlogi zjadloby cala obnizke)
                    float totalDays = p.Kind == "van"
                        ? MathF.Max(0.1f, Recipes.Grade(p.Item) * Settings.Current.WeaponDaysPerTier)
                        : MathF.Max(1f, rr.Tier * Settings.Current.DaysPerTier * Project.TimeFactor(p.Tempo));
                    if (atForge && p.Kind != "bk")   // XP tylko za wlasna prace przy kowadle (bk: XP dal BK przy kliknieciu)
                        Hero.MainHero.HeroDeveloper.AddSkillXp(DefaultSkills.Crafting,
                            Forge.ProjectXp(rr) * Settings.Current.XpShareWhileWorking / totalDays / 24f);

                    // wpis 89 (audyt): zapis W MIEJSCU - dotad Remove + Add przesuwal tykajaca sztuke na koniec listy,
                    // wiec "najstarsza" zmieniala sie co godzine i cala kolejka szla na zmiane, konczac sie razem
                    int at = _projects.IndexOf(line);
                    if (p.DaysLeft > 0f) { if (at >= 0) _projects[at] = p.Serialize(); else _projects.Add(p.Serialize()); }
                    else if (atForge) { _projects.Remove(line); HandOver(p); }
                    else
                    {
                        // skonczone pod twoja nieobecnosc: wyrob czeka na polce
                        p.DaysLeft = 0f;
                        if (at >= 0) _projects[at] = p.Serialize(); else _projects.Add(p.Serialize());
                        var s = Settlement.Find(p.SettlementId);
                        Log.Player("The smith has finished your " + p.Item.Name + " - collect it at "
                                   + (s != null ? s.Name.ToString() : p.SettlementId) + ".");
                    }
                }
            }
            catch (Exception e) { Log.Error("AdvanceProjects", e); }
        }

        /// <summary>Odbior gotowych wyrobow zaraz przy wejsciu do osady - bez czekania na tick godzinowy.</summary>
        internal void CollectReadyProjects()
        {
            try
            {
                if (_projects.Count == 0) return;
                var here = Settlement.CurrentSettlement;
                if (here == null) return;
                var copy = new List<string>(_projects);
                foreach (var line in copy)
                {
                    var p = Project.Parse(line);
                    if (p.Item == null) { _projects.Remove(line); continue; }
                    if (p.DaysLeft <= 0f && p.SettlementId == here.StringId)
                    { _projects.Remove(line); HandOver(p); }
                }
            }
            catch (Exception e) { Log.Error("CollectReadyProjects", e); }
        }

        private void OnDailyTick()
        {
            try { CampFever.OnDaily(); } catch (Exception e) { Log.Error("CampFever", e); }
            try { RecruitKit.Reconcile("doba"); } catch (Exception e) { Log.Error("RecruitKit.Reconcile", e); }   // 161: komplety bez ochotnika w puli (sieroty)
            // audyt predkosci STAD, nie z postfixa - w postfixie SpeedExplained
            // wchodzilo o poziom glebiej i rozpiska pokazywala gole liczby vanilli
            try { TerrainEase.DailyAudit(); } catch (Exception e) { Log.Error("SpeedAudit", e); }
            try { WorldMeasure.Daily(); } catch (Exception e) { Log.Error("WorldMeasure.Daily", e); }   // T6: linia "Miara: marsz" (tylko log)
            try { PlagueWatch.DailyReport(); } catch (Exception e) { Log.Error("PlagueWatch", e); }
            try { InfluenceWatch.DailyReport(); } catch (Exception e) { Log.Error("InfluenceWatch", e); }
            try { WesterosClimate.Daily(); } catch (Exception e) { Log.Error("WesterosClimate.Daily", e); }   // biale kruki: koniec pory roku
            try { WinterBite.OnDaily(); } catch (Exception e) { Log.Error("WinterBite", e); }
            var gfForage = GoodsLedger.Begin(GoodsLedger.FForage);   // paczka 146: ksiega towarow - zboze z furazu armii (tylko licznik)
            try { ScorchedEarth.OnDaily(); } catch (Exception e) { Log.Error("ScorchedEarth", e); }
            finally { GoodsLedger.End(gfForage); }
            try { WarLedger.OnDaily(); } catch (Exception e) { Log.Error("WarLedger", e); }
            try { Orders.DailyTick(); }
            catch (Exception e) { Log.Error("OnDailyTick", e); }
            // wpis 86 (audyt pkt 18): kazdy system we wlasnym try - wyjatek jednego nie zatrzymuje reszty dnia
            try { HistoricalPrices.Recheck(); } catch (Exception e) { Log.Error("HistoricalPrices.Recheck", e); }
            try { UniqueSpoils.Daily(); } catch (Exception e) { Log.Error("UniqueSpoils.Daily", e); }
            try { BattleChronicle.Daily(); } catch (Exception e) { Log.Error("BattleChronicle.Daily", e); }
            try { LosersFlee.Daily(); } catch (Exception e) { Log.Error("LosersFlee.Daily", e); }   // H3: linia "Przegrani (H3):" zaraz po "Bitwy:" i plik h3-domy.csv (PRZED OutlawLaw.Daily)
            try { PrisonerLaw.Daily(); } catch (Exception e) { Log.Error("PrisonerLaw.Daily", e); }   // I1: linia "Prawo jenca (I1):" - do domu, na Mur, niewolnicy (ta sama doba co H3 "prawo jenca I1")
            try { SupplyDemand.DecayOrders(); } catch (Exception e) { Log.Error("SupplyDemand.DecayOrders", e); }
            try { ArmsPricing.Daily(); } catch (Exception e) { Log.Error("ArmsPricing.Daily", e); }   // indeksy surowcow i premie wojenne PRZED handlem
            try { MoneyLedger.BlockOpen(); } catch { }   // ksiega przeplywow osad (tylko log): stan kas przed naszym rozliczeniem doby
            try { PopulationLaw.Daily(); } catch (Exception e) { Log.Error("PopulationLaw.Daily", e); }   // ludnosc i renty krain
            try { MoneyLedger.Mark(MoneyLedger.MRent); } catch { }
            // wpis 86 (audyt pkt 7): budowy PO rentach - 10% od dzisiejszego dochodu
            var gfBuild = GoodsLedger.Begin(GoodsLedger.FBuild);     // paczka 146: ksiega towarow - material zdjety przez budowy (tylko licznik)
            try { BuildFunding.Daily(); } catch (Exception e) { Log.Error("BuildFunding.Daily", e); }
            finally { GoodsLedger.End(gfBuild); }
            try { MoneyLedger.Mark(MoneyLedger.MBuild); } catch { }
            try { BuildDiary.Daily(); } catch (Exception e) { Log.Error("BuildDiary.Daily", e); }
            try { OreLedger.Daily(); } catch (Exception e) { Log.Error("OreLedger.Daily", e); }   // wpis 94: ksiega rudy (tylko log)
            try { GoodsLedger.Daily(); } catch (Exception e) { Log.Error("GoodsLedger.Daily", e); }   // paczka 146: ksiega towarow (tylko log) - zaraz po ksiedze rudy (kontrola tymi samymi liczbami)
            try { FreeSupplies.Daily(); } catch (Exception e) { Log.Error("FreeSupplies.Daily", e); }   // paczka 125: dosypka RBL zablokowana / przepuszczona (tylko log)
            try { RawNoRot.Daily(); } catch (Exception e) { Log.Error("RawNoRot.Daily", e); }   // 174.3: linia "BK gnicie surowcow (174.3)" (tylko log)
            try { VillageWoodlot.Daily(); } catch (Exception e) { Log.Error("VillageWoodlot.Daily", e); }   // paczka 126: las wsi - drewno kazdej wsi bez drwali (tylko log)
            try { SpoilsSeal.Daily(); } catch (Exception e) { Log.Error("SpoilsSeal.Daily", e); }   // paczka 128: Spoils of War bez sprzedazy automatycznej i bez zlota z niczego (tylko log)
            try { SpoilsCompany.Daily(); } catch (Exception e) { Log.Error("SpoilsCompany.Daily", e); }   // klan najemnikow Spoils tylko z prawdziwych zolnierzy (tylko log)
            try { RawPrice.Daily(); } catch (Exception e) { Log.Error("RawPrice.Daily", e); }     // cena surowcow od niedoboru: linia "Ceny surowcow:" (tylko log)
            try { MineralOnce.Daily(); } catch (Exception e) { Log.Error("MineralOnce.Daily", e); }   // powtorzenia mineralu zdjete z list produkcji BK (tylko log)
            try { MarketRoad.Daily(); } catch (Exception e) { Log.Error("MarketRoad.Daily", e); }   // wpis 100: dowoz wsi zamkowych na targi (log)
            try { VillageClogDiag.Daily(); } catch (Exception e) { Log.Error("VillageClogDiag.Daily", e); }   // diagnoza zatkanych wsi - zaraz po "Dowoz (skutki)" (tylko log)
            try { MarketCarts.Daily(); } catch (Exception e) { Log.Error("MarketCarts.Daily", e); }   // poprawka 119: wozy wsi do najlepiej placacego miasta, cena ladunku sztuka po sztuce (log + porzadki wiesci z drogi)
            try { CartTownExit.Daily(); } catch (Exception e) { Log.Error("CartTownExit.Daily", e); }   // paczka 130: bezpiecznik (woz w miescie od N dob - do domu) i linia "Wozy w miastach:"
            try { CaravanBulk.Daily(); } catch (Exception e) { Log.Error("CaravanBulk.Daily", e); }   // wpis 103: surowce masowe w karawanach (przeliczenie swiata + log)
            try { CaravanAmmo.Daily(); } catch (Exception e) { Log.Error("CaravanAmmo.Daily", e); }   // paczka 172b: linia "Karawany bez amunicji (172b)" (zablokowane, sprzedane z taborow, w taborach)
            try { KingdomTreasury.Daily(); KingdomTreasury.Levies(); KingdomTreasury.WageRefund(); KingdomLedger.Daily(); } catch (Exception e) { Log.Error("KingdomTreasury.Daily", e); }   // powinnosci wasali wobec korony (po rentach); potem zwrot zoldu w wojnie
            try { MoneyLedger.Mark(MoneyLedger.MCrown); } catch { }
            try { SoldierPay.Daily(); } catch (Exception e) { Log.Error("SoldierPay.Daily", e); }   // zold do obiegu: linia "Zold:" i liczniki doby (po zwrocie ze skarbca)
            try { ArmyClothing.Daily(); } catch (Exception e) { Log.Error("ArmyClothing.Daily", e); }   // 150: linia "Odziez wojska (150):" (zlota nie rusza)
            try { OutlawLaw.Daily(); } catch (Exception e) { Log.Error("OutlawLaw.Daily", e); }   // wyrzutki: bieda, powroty, werbunek band
            try { IronBank.Daily(); } catch (Exception e) { Log.Error("IronBank.Daily", e); }   // Bank Zelazny: pozyczki AI, raty, bankructwa
            try { ClanIncomeBook.Daily(); } catch (Exception e) { Log.Error("ClanIncomeBook.Daily", e); }   // paczka 169: D rodow, budzet i dlugi na sucho (tylko log) - po rentach, zwrocie, mennicy i Banku dnia
            // 171: zawrocony towar i nadwyzki zalog na polkach PRZED handlem kupcow (kupcy wywioza nadwyzke zamkow tego samego dnia)
            try { GarrisonCarts.Daily(); } catch (Exception e) { Log.Error("GarrisonCarts.Daily", e); }     // zamowienia zamkow w drodze: dostawy, zawrocenia, linia "Zaopatrzenie zamkow (171)"
            try { GarrisonArmory.Daily(); } catch (Exception e) { Log.Error("GarrisonArmory.Daily", e); }   // nadwyzki zalog raz w tygodniu, linia "Zbrojownie zalog (171): dzien"
            try { ArmsDrill.Daily(); } catch (Exception e) { Log.Error("ArmsDrill.Daily", e); }             // linie "Cwiczenia (171)" i co 5 dob "Pokrycie zbrojowni AI (171)" (tylko log)
            try { SupplyDemand.DailyTrade(); } catch (Exception e) { Log.Error("SupplyDemand.DailyTrade", e); }
            try { MoneyLedger.Mark(MoneyLedger.MRest); } catch { }   // 174.2: kasy osad przed kontraktami - pozostale moduly osobno
            try { MaterialOrders.Daily(); } catch (Exception e) { Log.Error("MaterialOrders.Daily", e); }   // 174.2: kontrakty surowca dla prawdziwych karawan (po handlu bronia i wozach zamkow), linia "Kontrakty surowca (174)"
            try { MoneyLedger.Mark(MoneyLedger.MOrders); } catch { }
            try { ArmsScrap.Daily(); } catch (Exception e) { Log.Error("ArmsScrap.Daily", e); }   // 174 pytanie 4: pomiar popytu koszykow zawsze; skup na zlom tylko przy OldStockToScrap (domyslnie wylaczony)
            try { SellByCondition.Daily(); } catch (Exception e) { Log.Error("SellByCondition.Daily", e); }   // cena sprzedazy sprzetu: linia "Skup sprzetu" wedlug sprzedajacego (tylko log)
            try { RecruitCost.Daily(); } catch (Exception e) { Log.Error("RecruitCost.Daily", e); }   // poprawka 157 / paczka 160: linia "Konie rekrutow" (tylko log)
            try { MountedWage.Daily(); } catch (Exception e) { Log.Error("MountedWage.Daily", e); }   // paczka 160: linia "Zold konnych (160)" - sklad wojska i premia konnego (tylko log)
            try { MoneyLedger.Daily(); } catch (Exception e) { Log.Error("MoneyLedger.Daily", e); }     // K1: "Pieniadz swiata" i "Przeplywy osad" (tylko log) - po calym naszym rozliczeniu doby
            try { PeopleLedger.Daily(); } catch (Exception e) { Log.Error("PeopleLedger.Daily", e); }   // demografia krok 1: "Ludzie:" i plik regionow (tylko log)
            try { MarketGlut.DailyDigest(); }
            catch (Exception e) { Log.Error("GlutDigest", e); }
            try
            {
                // poranny meldunek: braki wykrzyczane na glos, zeby Jeff WIEDZIAL
                // bez otwierania zbrojowni ("czemu ja o tym nie wiem!")
                QuartermasterLaw.ShoutShortages("QM morning: short (have/need):");
            }
            catch (Exception e) { Log.Error("MorningReport", e); }
        }

        internal bool HasProjectsHere(Settlement here)
        {
            try
            {
                if (here == null) return false;
                foreach (var line in _projects)
                {
                    var p = Project.Parse(line);
                    if (p.Item != null && p.SettlementId == here.StringId) return true;
                }
            }
            catch { }
            return false;
        }

        internal float ProjectHoursLeftHere(Settlement here)
        {
            float h = 0f;
            try
            {
                if (here == null) return 0f;
                foreach (var line in _projects)
                {
                    var p = Project.Parse(line);
                    if (p.Item != null && p.SettlementId == here.StringId) h += p.DaysLeft * 24f;
                }
            }
            catch { }
            return h;
        }

        /// <summary>Natywne kucie broni tez nie moze byc natychmiastowe - zabieramy wyrob i oddajemy po czasie.</summary>
        private void OnNewItemCrafted(ItemObject item, ItemModifier modifier, bool isCraftingOrderItem)
        {
            try { HistoricalPrices.PriceOne(item); } catch { }   // wpis 90: wykuty miecz w pensach, jak reszta swiata
            try
            {
                var s = Settings.Current;
                if (!s.WeaponCraftingTakesTime || isCraftingOrderItem) return;   // zlecenia maja swoje terminy
                if (item == null) return;
                var here = Settlement.CurrentSettlement;
                if (here == null) return;

                int tier = Recipes.Grade(item);
                float days = MathF.Max(0.1f, tier * s.WeaponDaysPerTier);   // minimum 0.1 dnia (Jeff 16.09: -80%)
                // wpis 83: godziny z ekranu BK - koszt staminy tej sztuki / 6
                if (ForgeClock.On)
                    try { days = ForgeClock.HoursOf(Campaign.Current.Models.SmithingModel.GetEnergyCostForSmithing(item, Hero.MainHero)) / 24f; } catch { }

                var roster = MobileParty.MainParty.ItemRoster;
                var el = new EquipmentElement(item, modifier);   // takim lezy w sakwach
                if (roster.GetItemNumber(item) > 0) roster.AddToCounts(el, -1);

                // JAKOSC PRZY KOWADLE (Jeff 30.08, seria Albion: "popup nie
                // pokazuje +1/-1, nie ma Legendary ani spartaczonych"):
                // vanillowe CreateCraftedWeaponInFreeBuildMode przyjmuje
                // modyfikator Z ZEWNATRZ (domyslnie null) i nasza sciezka
                // dostawala golego - kazdy "van" wychodzil pospolity BEZ
                // rzutu. Rzucamy nasza wierna formula vanilla (kuznia-1do1
                // krok 1) RAZ, tutaj - dostawa po czasie tylko oddaje wynik.
                if (modifier == null)
                    try
                    {
                        modifier = Forge.RollQuality(item, Recipes.For(item));
                        if (modifier != null)
                            Log.Info("Jakosc przy kowadle: " + item.StringId + " -> " + modifier.StringId);
                    }
                    catch (Exception qe) { Log.Error("VanQuality", qe); }

                // "van": sukces i jakosc zapadly PRZY KOWADLE (vanilla) - dostawa
                // po czasie ma NIE rzucac drugi raz (Jeff: "wykulem, a potem fail
                // i miecza nie ma"). Modyfikator jedzie z projektem i wraca.
                StartProject(item, 1, days, here, "van", modifier != null ? modifier.StringId : "");
                Log.Player("The blade is roughed out. " + Project.TimeLabel(days) + " of finishing work remain at "
                           + here.Name + (ForgeClock.On ? " - the work goes on only while you stay here." : " - he works it himself, wherever you ride."));
                // gra przed chwila POKAZALA "dodano do ekwipunku" - bez glosnego
                // baneru wyglada to na zniknieciecie miecza
                try
                {
                    MBInformationManager.AddQuickInformation(new TaleWorlds.Localization.TextObject(
                        "{=!}The smith keeps the " + item.Name + " for finishing - " +
                        Project.TimeLabel(days) + " at " + here.Name + ". Stay or return to collect it."));
                }
                catch { }
                Log.Info("Bron w toku: " + item.StringId + " dni=" + days);
            }
            catch (Exception e) { Log.Error("OnNewItemCrafted", e); }
        }

        // ---------------------------------------------------------- zuzycie
        private void OnMapEventStarted(MapEvent mapEvent, PartyBase a, PartyBase b)
        {
            try
            {
                if (mapEvent == null || !mapEvent.IsPlayerMapEvent) return;
                _hpBeforeBattle = Hero.MainHero.HitPoints;
                _lootSnapshot = SnapshotBaggage();
                SnapshotOwnRanks();   // roznica po bitwie = polegli (GatherFallen)
                _prisonerBaseline = SnapshotPrisoners();
            }
            catch (Exception e) { Log.Error("OnMapEventStarted", e); }
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            try
            {
                if (mapEvent == null || !mapEvent.IsPlayerMapEvent) return;

                // 17.09: pekanie kolczanow po bitwie (AmmoAttrition, Jeff 02.09) WYWALONE
                // na zadanie Jeffa ("nic ma nie pekac") - amunicja ubywa tylko w polu

                // PRZEGRANA ALBO UCIECZKA = NIC Z POLA. Wraki zbierane w trakcie
                // misji lezaly w kolejce niezaleznie od wyniku i po godzinie
                // wsypywaly sie do sakw - takze wtedy, gdy gracz uciekl z pola
                // ("jak uciekam, to nie zdobywam gearu, przeciez ucieklem" - Jeff).
                bool won = false;
                try { won = mapEvent.WinningSide == mapEvent.PlayerSide; } catch { }
                if (!won)
                {
                    int dropped = BattlefieldLaw.DropShare();
                    _lootFlushDue = CampaignTime.Zero;
                    if (dropped > 0)
                        Log.Info("Pole przegrane/opuszczone - " + dropped + " sztuk lupu zostaje na ziemi.");
                    _hpBeforeBattle = -1;
                    return;
                }

                // okno na obszukanie jencow: ekran "wez jencow" przychodzi tuz
                // po bitwie - tylko wtedy zdzieramy z nich rynsztunek (nie przy
                // zwyklym przekladaniu jencow z lochu czy garnizonu)
                // audyt pelny W3 (04.10): okna obszukania po bitwie NIE otwieramy - sprzet jencow z walki (nieprzytomnych)
                // DTE juz wlozyl do lupu z pola; obszukanie bylo druga zaplata za ten sam rynsztunek.
                // Obszukanie zostaje tylko dla kapitulantow bez walki (OnPrisonerTaken poza bitwa).

                // wdzieczne wioski: wygrana z bandytami cieszy okolice
                BanditCheer.AfterVictory(mapEvent);

                // KRYJOWKA: zaden ekran lupow nie przyjdzie - dzialka gracza
                // z kolejki BattlefieldLaw idzie prosto do sakw, a jency
                // (bandyci poddani po walce) sa obszukiwani od reki
                if (mapEvent.IsHideoutBattle)
                {
                    BattlefieldLaw.FlushShareToBaggage("kryjowka zdobyta");
                    try { _prisonerBaseline = SnapshotPrisoners(); } catch { }   // jency z walki - bez drugiego obszukania (W3)
                    _lootFlushDue = CampaignTime.Zero;
                }
                else if (BattlefieldLaw.SharePending())
                {
                    // zwykla bitwa: ekran Spoils powinien odebrac kolejke;
                    // jesli w godzine tego nie zrobi - wysypujemy do sakw
                    _lootFlushDue = CampaignTime.HoursFromNow(1f);
                }

                // po bitwie i rozliczeniu lupow kwatermistrz melduje braki na glos
                _shortageShoutDue = CampaignTime.HoursFromNow(1f);

                if (!Settings.Current.WearEnabled) return;

                int damage = 0;
                if (_hpBeforeBattle > 0) damage = Math.Max(0, _hpBeforeBattle - Hero.MainHero.HitPoints);
                _hpBeforeBattle = -1;

                if (Settings.Current.LootArrivesWorn) WearTheLoot(damage);
                if (Settings.Current.TroopWearEnabled) WearTheTroops();

                // Zuzycie CELOWANE: FieldCraft spisal w misji, gdzie padaly ciosy
                // (zbroja tam, gdzie trafiono; bron za celne uderzenia; tarcza za bloki).
                var ledger = new float[12];
                FieldCraft.TakeLedger(ledger);
                float sum = 0f; for (int i = 0; i < 12; i++) sum += ledger[i];
                float flat = Settings.Current.WearPerBattle;
                if (sum > 0.01f)
                {
                    Log.Info("Bitwa zakonczona: obrazenia " + damage + ", zuzycie celowane " + sum.ToString("0.0") + " pkt.");
                    ApplyWearPerSlot(ledger, flat);
                }
                else if (flat > 0.01f || damage > 0)
                {
                    // bitwa automatyczna (bez misji) - stara droga: od obrazen
                    float wear = flat + damage * Settings.Current.WearDamageFactor;
                    Log.Info("Bitwa zakonczona (auto): obrazenia " + damage + ", zuzycie " + wear.ToString("0.0"));
                    ApplyWear(wear);
                }

                CleanseAmmo();   // lupy moga przyniesc "zuzyte" strzaly z cudzych systemow - czyscimy
            }
            catch (Exception e) { Log.Error("OnMapEventEnded", e); }
        }

        private Dictionary<string,int> SnapshotBaggage()
        {
            var map = new Dictionary<string,int>();
            try
            {
                var roster = MobileParty.MainParty.ItemRoster;
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster[i];
                    if (el.EquipmentElement.Item == null) continue;
                    var key = Key(el.EquipmentElement);
                    map[key] = (map.ContainsKey(key) ? map[key] : 0) + el.Amount;
                }
            }
            catch (Exception e) { Log.Error("SnapshotBaggage", e); }
            return map;
        }

        private static string Key(EquipmentElement el)
        {
            return el.Item.StringId + "#" + (el.ItemModifier != null ? el.ItemModifier.StringId : "");
        }

        /// <summary>Sprzet zdarty z poleglych nie jest nieskazitelny. Ktos w nim wlasnie zginal.</summary>
        /// <summary>
        /// ZUZYCIE SPRZETU WOJSKA (Jeff 27.08: "zolnierze zawsze maja 100%,
        /// jakby sprzet sie nie psul"). Zuzycie tykalo dotad WYLACZNIE sprzet
        /// bohatera - zbrojownia DTE zyla wiecznie nowa i muster kwatermistrza
        /// klamal. Po kazdej bitwie czesc sztuk W UZYCIU (wg liczby zolnierzy
        /// noszacych dany typ) schodzi o JEDEN stopien drabinki modyfikatorow -
        /// ta sama, ktora brudzi lupy. Naprawa: "Mend the men's kit" u kowala.
        /// </summary>
        private void WearTheTroops()
        {
            try
            {
                var s = Settings.Current;
                var armory = QuartermasterLaw.DteArmory();
                if (armory == null) return;
                var needs = QuartermasterLaw.CountNeeds();
                float share = MBMath.ClampFloat(s.TroopWearPercent, 0f, 100f) / 100f;
                int worn = 0;
                // wpis 96 (Jeff: "nie na sztywno - z walki"): bitwa rozgrywana - z trafien; symulacja - wedlug strat (ranni / ludzie)
                var ledger = TroopWearLedger.TakeLast();
                float intensity = 1f;
                if (ledger == null)
                {
                    try
                    {
                        var r = MobileParty.MainParty.MemberRoster;
                        float men = Math.Max(1, r.TotalManCount - r.TotalHeroes);
                        intensity = MBMath.ClampFloat((r.TotalWounded / men) / Math.Max(0.01f, s.TroopWearBaseCasualtyShare), 0.1f, 3f);
                    }
                    catch { }
                }
                var report = new List<string>();

                foreach (var type in QuartermasterLaw.KitTypes)
                {
                    int inUse = QuartermasterLaw.WornFor(type, needs);
                    if (inUse <= 0) continue;
                    int hits;
                    if (ledger != null)
                    {
                        float h; ledger.TryGetValue(type, out h);
                        float chance = type == ItemObject.ItemTypeEnum.Shield ? s.TroopWearPerBlock
                                     : (type == ItemObject.ItemTypeEnum.Bow || type == ItemObject.ItemTypeEnum.Crossbow) ? s.TroopWearPerShot
                                     : (type == ItemObject.ItemTypeEnum.OneHandedWeapon || type == ItemObject.ItemTypeEnum.TwoHandedWeapon || type == ItemObject.ItemTypeEnum.Polearm) ? s.TroopWearPerStrike
                                     : s.TroopWearPerHit;
                        float exp = h * Math.Max(0f, chance);
                        hits = (int)Math.Floor(exp + MBRandom.RandomFloat);   // ulamek - losowo
                        hits = Math.Min(hits, inUse);
                        if (h > 0f) report.Add(type + " " + (int)h + "->" + hits);
                    }
                    else hits = (int)Math.Floor(inUse * share * intensity + MBRandom.RandomFloat);
                    if (hits <= 0) continue;

                    // kandydaci: sztuki tego typu podlegajace zuzyciu
                    var idx = new List<int>();
                    for (int i = 0; i < armory.Count; i++)
                    {
                        var el = armory.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (it == null || it.ItemType != type || el.Amount <= 0) continue;
                        if (NoWear(it) || it.ItemComponent == null || it.ItemComponent.ItemModifierGroup == null) continue;
                        idx.Add(i);
                    }
                    if (idx.Count == 0) continue;

                    for (int n = 0; n < hits; n++)
                    {
                        // wpis 96 (audyt pkt 5): kazda SZTUKA ma rowna szanse (dotad kazdy rodzaj - pojedyncze sztuki psuly sie w kolko)
                        int totalAmt = 0; foreach (var ii in idx) totalAmt += armory.GetElementCopyAtIndex(ii).Amount;
                        int pickN = MBRandom.RandomInt(Math.Max(1, totalAmt)), pickI = idx[0];
                        foreach (var ii in idx) { int a0 = armory.GetElementCopyAtIndex(ii).Amount; if (pickN < a0) { pickI = ii; break; } pickN -= a0; }
                        var el = armory.GetElementCopyAtIndex(pickI).EquipmentElement;
                        var group = el.Item.ItemComponent.ItemModifierGroup;
                        var bad = new List<ItemModifier>();
                        foreach (var m in group.ItemModifiers)
                            if (m != null && m.PriceMultiplier < 1f) bad.Add(m);
                        if (bad.Count == 0) continue;
                        bad.Sort((x, y) => x.PriceMultiplier.CompareTo(y.PriceMultiplier));   // [0] = najgorszy

                        ItemModifier next;
                        var cur = el.ItemModifier;
                        if (cur == null) next = bad[bad.Count - 1];               // pierwsza rysa: najlzejszy zly stan
                        else
                        {
                            next = null;                                          // nastepny gorszy od obecnego
                            for (int b = bad.Count - 1; b >= 0; b--)
                                if (bad[b].PriceMultiplier < cur.PriceMultiplier) { next = bad[b]; break; }
                            if (next == null) continue;                           // juz na dnie drabinki
                        }
                        armory.AddToCounts(el, -1);
                        armory.AddToCounts(new EquipmentElement(el.Item, next), 1);
                        worn++;
                        // indeksy moga sie przesunac po podmianie - odswiez liste raz na sztuke
                        idx.Clear();
                        for (int i = 0; i < armory.Count; i++)
                        {
                            var e2 = armory.GetElementCopyAtIndex(i);
                            var it2 = e2.EquipmentElement.Item;
                            if (it2 == null || it2.ItemType != type || e2.Amount <= 0) continue;
                            if (NoWear(it2) || it2.ItemComponent == null || it2.ItemComponent.ItemModifierGroup == null) continue;
                            idx.Add(i);
                        }
                        if (idx.Count == 0) break;
                    }
                }

                if (worn > 0)
                {
                    Log.Info("Zuzycie wojska: " + worn + " sztuk zeszlo o stopien" + (ledger != null ? " (z trafien: " + string.Join(", ", report.ToArray()) + ")" : " (symulacja, natezenie " + intensity.ToString("0.00") + ")") + ".");
                    Log.Player("The battle wore the men's kit - " + worn + " pieces the worse for it.", true);
                }
            }
            catch (Exception e) { Log.Error("WearTheTroops", e); }
        }

        private void WearTheLoot(int damageTaken)
        {
            try
            {
                var s = Settings.Current;
                var roster = MobileParty.MainParty.ItemRoster;
                var after = SnapshotBaggage();
                var changes = new List<KeyValuePair<EquipmentElement,int>>();

                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster[i];
                    var item = el.EquipmentElement.Item;
                    if (item == null) continue;
                    if (el.EquipmentElement.ItemModifier != null) continue;              // juz ma jakis stan
                    if (item.ItemComponent == null || item.ItemComponent.ItemModifierGroup == null) continue;
                    if (!item.HasArmorComponent && !item.HasWeaponComponent) continue;
                    if (NoWear(item)) continue;                                          // strzaly/belty bez stanow

                    var key = Key(el.EquipmentElement);
                    int before = _lootSnapshot.ContainsKey(key) ? _lootSnapshot[key] : 0;
                    int gained = el.Amount - before;
                    if (gained <= 0) continue;
                    changes.Add(new KeyValuePair<EquipmentElement,int>(el.EquipmentElement, gained));
                }

                int worn = 0;
                foreach (var c in changes)
                {
                    var group = c.Key.Item.ItemComponent.ItemModifierGroup;
                    var bad = new List<ItemModifier>();
                    foreach (var m in group.ItemModifiers)
                        if (m != null && m.PriceMultiplier < 1f) bad.Add(m);
                    if (bad.Count == 0) continue;
                    bad.Sort((x, y) => x.PriceMultiplier.CompareTo(y.PriceMultiplier));

                    for (int n = 0; n < c.Value; n++)
                    {
                        // im ciezsza byla bitwa, tym gorszy stan lupu
                        float cond = s.LootWearBase + (MBRandom.RandomFloat - 0.5f) * 2f * s.LootWearSpread
                                     - damageTaken * 0.15f;
                        int idx;
                        if (cond > 55f) idx = bad.Count - 1;
                        else if (cond > 30f) idx = MathF.Max(0, bad.Count - 2);
                        else idx = 0;

                        roster.AddToCounts(c.Key, -1);
                        roster.AddToCounts(new EquipmentElement(c.Key.Item, bad[idx]), 1);
                        worn++;
                    }
                }

                if (worn > 0)
                {
                    Log.Info("Lup zuzyty: " + worn + " przedmiotow.");
                    Log.Player(worn + " pieces of plunder came off the field battered. Mend them or melt them down.", true);
                }
            }
            catch (Exception e) { Log.Error("WearTheLoot", e); }
        }

        /// <summary>
        /// LUPY Z JENCOW. Jeff: "pojmalem Ravens' Teeth, to powinienem miec jego
        /// pancerz". Vanilla i Spoils of War losuja lupy tylko z pola - jeniec
        /// zabieral caly swoj rynsztunek do niewoli. Jency wchodza do niewoli
        /// ROZNYMI drzwiami (ekran po bitwie, kryjowka, automat) - zdarzenie
        /// OnPrisonerTaken lapie tylko pierwsze, wiec liczymy ROZNICE: stan
        /// jencow sprzed bitwy kontra teraz. Kazdy NOWY szeregowy jeniec w oknie
        /// po bitwie zostaje obszukany: cale jego wyposazenie wpada do sakw,
        /// zuzyte jak lupy. Lordowie NIE - za nich bierze sie okup, nie plaszcz.
        /// </summary>
        private void OnPrisonerTaken(TaleWorlds.CampaignSystem.Roster.FlattenedTroopRoster roster)
        {
            // KONIEC PODWOJNEGO LICZENIA (Jeff 29.08: "lupy po bitwie, a potem
            // jeszcze rozbieram jencow - czy to nie dubel?"). BYL DUBEL: sprzet
            // pokonanych JUZ idzie do lupow po KAZDEJ bitwie (realna - DTE
            // zbiera z pola; symulowana - pelny drop), a jeniec z bitwy to jeden
            // z pokonanych. Obszukanie go byloby DRUGA nagroda za ten sam
            // rynsztunek. Jency z bitwy: tylko aktualizacja ksiegi (zeby
            // hourly/menu ich pozniej nie "doszukalo"). Obszukiwanie zostaje
            // WYLACZNIE dla kapitulantow bez walki (dialog na mapie, poddanie
            // band) - tam zaden lup z pola nie padl.
            bool fromBattle = false;
            try
            {
                fromBattle = MapEvent.PlayerMapEvent != null
                    || TaleWorlds.CampaignSystem.Encounters.PlayerEncounter.Battle != null;
            }
            catch { }
            if (fromBattle)   // audyt pelny W3: jeniec z walki oddal sprzet w lupie z pola (DTE liczy nieprzytomnych jak poleglych)
            {
                // stary lad (bez prawa zachowania lupu): vanilla loteria juz
                // zaplacila za rannych - obszukanie byloby DRUGA nagroda
                try { _prisonerBaseline = SnapshotPrisoners(); } catch { }
                return;
            }
            // PRAWO ZACHOWANIA LUPU: loteria szablonowa wycieta, wiec jeniec
            // z bitwy placi FIZYCZNIE przy obszukaniu - raz i do naga
            // (Jeff 30.08: "jak biore jencow, rozbieramy ich do naga")
            try
            {
                var half = CampaignTime.HoursFromNow(0.5f);
                if (_spoilsWindow < half) _spoilsWindow = half;
            }
            catch { }
            TryStripNewCaptives(fromBattle ? "bitwa" : "OnPrisonerTaken");
        }

        private Dictionary<string,int> SnapshotPrisoners()
        {
            var map = new Dictionary<string,int>();
            try
            {
                var r = MobileParty.MainParty != null ? MobileParty.MainParty.PrisonRoster : null;
                if (r == null) return map;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    if (el.Character == null || el.Character.IsHero) continue;
                    var id = el.Character.StringId;
                    map[id] = (map.ContainsKey(id) ? map[id] : 0) + el.Number;
                }
            }
            catch (Exception e) { Log.Error("SnapshotPrisoners", e); }
            return map;
        }

        internal void TryStripNewCaptives(string source)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.CaptiveSpoilsEnabled) return;
                if (CampaignTime.Now > _spoilsWindow)
                {
                    // poza oknem: ksiega jencow ma NADAZAC za sprzedazami i
                    // zwolnieniami - inaczej kapitulanci chowaliby sie pod
                    // starym stanem i wchodzili do lochu w pelnym rynsztunku
                    _prisonerBaseline = SnapshotPrisoners();
                    return;
                }
                var roster = MobileParty.MainParty != null ? MobileParty.MainParty.PrisonRoster : null;
                var bag = MobileParty.MainParty != null ? MobileParty.MainParty.ItemRoster : null;
                if (roster == null || bag == null) return;
                if (_prisonerBaseline == null) _prisonerBaseline = new Dictionary<string,int>();

                int pieces = 0, men = 0;
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    var troop = el.Character;
                    if (troop == null || troop.IsHero) continue;
                    int before = _prisonerBaseline.ContainsKey(troop.StringId) ? _prisonerBaseline[troop.StringId] : 0;
                    int fresh = el.Number - before;
                    for (int m = 0; m < fresh; m++)
                    {
                        // ktores z jego bitewnych wyposazen - tak chodzil, tak go wzieto
                        Equipment eq = null; int n = 0;
                        foreach (var e in troop.BattleEquipments) { n++; if (MBRandom.RandomInt(n) == 0) eq = e; }
                        if (eq == null) continue;
                        men++;
                        for (int slot = 0; slot < 12; slot++)
                        {
                            if (slot == 4) continue;                                     // choragiew zostaje przy sztandarze
                            if (!s.CaptiveSpoilsIncludeMounts && slot >= 10) continue;   // kon i rzad konski
                            var item = eq[(EquipmentIndex)slot].Item;
                            if (item == null || item.ItemType == ItemObject.ItemTypeEnum.Banner) continue;
                            // smoka nie poprowadzisz na powrozie - zadnych dragon_* w sakwach
                            if (item.StringId != null && item.StringId.StartsWith("dragon_")
                                && item.ItemType == ItemObject.ItemTypeEnum.Horse) continue;
                            bag.AddToCounts(new EquipmentElement(item, PickWornModifier(item)), 1);
                            pieces++;
                        }
                    }
                }

                _prisonerBaseline = SnapshotPrisoners();   // nowy stan = nowa baza, zadnego dublowania
                if (pieces > 0)
                {
                    Log.Info("Jency obszukani (" + source + "): " + men + " ludzi, " + pieces + " sztuk do sakw.");
                    Log.Player("The captives are stripped at the rope: " + pieces + " pieces of gear go into the baggage.", true);
                }
            }
            catch (Exception e) { Log.Error("TryStripNewCaptives", e); }
        }

        /// <summary>Stan sprzetu zdartego z jenca - ta sama loteria co lupy z pola.</summary>
        /// <summary>wpis 95: pierwszy (najlagodniejszy) stopien zuzycia - zuzycie walki, nie stan lupu.</summary>
        internal static ItemModifier MildWornModifier(ItemObject item)
        {
            try
            {
                if (item == null || NoWear(item) || item.ItemComponent == null || item.ItemComponent.ItemModifierGroup == null) return null;
                ItemModifier best = null;
                foreach (var m in item.ItemComponent.ItemModifierGroup.ItemModifiers)
                    if (m != null && m.PriceMultiplier < 1f && (best == null || m.PriceMultiplier > best.PriceMultiplier)) best = m;
                return best;
            }
            catch { return null; }
        }

        internal static ItemModifier PickWornModifier(ItemObject item)
        {
            try
            {
                var s = Settings.Current;
                if (NoWear(item)) return null;                                  // strzaly/belty poza systemem zuzycia
                if (!item.HasArmorComponent && !item.HasWeaponComponent) return null;
                if (item.ItemComponent == null || item.ItemComponent.ItemModifierGroup == null) return null;

                var bad = new List<ItemModifier>();
                foreach (var m in item.ItemComponent.ItemModifierGroup.ItemModifiers)
                    if (m != null && m.PriceMultiplier < 1f) bad.Add(m);
                if (bad.Count == 0) return null;
                bad.Sort((x, y) => x.PriceMultiplier.CompareTo(y.PriceMultiplier));

                float cond = s.LootWearBase + (MBRandom.RandomFloat - 0.5f) * 2f * s.LootWearSpread;
                if (cond > 55f) return bad[bad.Count - 1];
                if (cond > 30f) return bad[MathF.Max(0, bad.Count - 2)];
                return bad[0];
            }
            catch { return null; }
        }

        /// <summary>
        /// Strzaly i belty NIE podlegaja zuzyciu - to amunicja, zuzywa sie
        /// przez WYSTRZELENIE (znika z kolczanu), nie przez "niszczenie".
        /// </summary>
        internal static bool IsAmmo(ItemObject it)
        {
            return it != null && (it.ItemType == ItemObject.ItemTypeEnum.Arrows
                               || it.ItemType == ItemObject.ItemTypeEnum.Bolts);
        }

        /// <summary>
        /// ZYWY INWENTARZ - konie, muly, swinie, byczki - NIE MA zuzycia, jak
        /// jedzenie. Kulawy kon to kulawy kon (vanilla), nie "kon 50% do
        /// naprawy w kuzni". Zuzycie ma wylacznie pancerz i bron.
        /// </summary>
        internal static bool IsBeast(ItemObject it)
        {
            return it != null && (it.HasHorseComponent
                               || it.ItemType == ItemObject.ItemTypeEnum.Horse
                               || it.ItemType == ItemObject.ItemTypeEnum.Animal);
        }

        /// <summary>
        /// TOWAR TO NIE PANCERZ. Ryba, maslo, zboze, len, glina - to ladunek,
        /// nie rynsztunek. Zjada sie go albo przerabia, nie "niszczy do 14%"
        /// (Jeff: "fish i butter nie ma zuzycia, przeciez to jedzenie").
        /// Zuzycie ma wylacznie to, co ma pancerz albo ostrze.
        /// </summary>
        internal static bool IsGoods(ItemObject it)
        {
            try
            {
                if (it == null) return true;
                return !it.HasArmorComponent && !it.HasWeaponComponent;
            }
            catch { return false; }
        }

        /// <summary>Poza systemem zuzycia: amunicja, zywy inwentarz i wszelki towar.</summary>
        internal static bool NoWear(ItemObject it)
        {
            return IsAmmo(it) || IsBeast(it) || IsGoods(it);
        }

        /// <summary>
        /// Sprzatanie po starych zasadach: amunicja z "uszkodzonym" stanem
        /// (z lupow, ze starych bitew) wraca do stanu fabrycznego - w sakwach
        /// i w kolczanach na grzbiecie.
        /// POPRAWKA 159 (recenzja ceny sprzedazy 07.10): konie i zwierzeta (IsBeast) NIE - kon nie ma komponentu broni ani zbroi, wiec
        /// IsGoods bral go za towar i kulawy kon gracza (lame_horse) wracal zdrowy przy kazdym wczytaniu i po kazdej bitwie: kulawy kon
        /// kupiony tanio i sprzedany zdrowy dawal zarobek z niczego (Handel 300). Kulawy kon to kulawy kon (opis IsBeast) - stan zostaje.
        /// </summary>
        internal void CleanseAmmo()
        {
            try
            {
                int fixedUp = 0;
                var roster = MobileParty.MainParty.ItemRoster;
                for (int i = roster.Count - 1; i >= 0; i--)
                {
                    var el = roster[i].EquipmentElement;
                    // amunicja ORAZ towar (jedzenie, surowce) - stan im nie przystoi;
                    // inne mody potrafia nalozyc "uszkodzenie" na rybe i maslo,
                    // scinajac przy okazji ich cene do grosza
                    if (!IsAmmo(el.Item) && !IsGoods(el.Item)) continue;
                    if (IsBeast(el.Item)) continue;                            // poprawka 159: kon i zwierze zachowuja swoj stan (kulawy zostaje kulawy)
                    if (el.ItemModifier == null) continue;
                    if (el.ItemModifier.PriceMultiplier >= 1f) continue;      // dodatnie stany zostaja
                    int n = roster[i].Amount;
                    roster.AddToCounts(el, -n);
                    roster.AddToCounts(new EquipmentElement(el.Item), n);
                    fixedUp += n;
                }
                var eq = Hero.MainHero.BattleEquipment;
                for (int slot = 0; slot < 4; slot++)
                {
                    var el = eq[(EquipmentIndex)slot];
                    if (!IsAmmo(el.Item) || el.ItemModifier == null) continue;
                    if (el.ItemModifier.PriceMultiplier >= 1f) continue;
                    eq[(EquipmentIndex)slot] = new EquipmentElement(el.Item);
                    fixedUp++;
                }
                if (fixedUp > 0) Log.Info("Amunicja oczyszczona ze stanow: " + fixedUp + " szt. (konie i zwierzeta zachowuja stan - poprawka 159)");
            }
            catch (Exception e) { Log.Error("CleanseAmmo", e); }
        }

        /// <summary>
        /// Pula wytrzymalosci sztuki pancerza wedle wzoru Jeffa:
        /// (suma punktow ochrony) x DurabilityPerArmorPoint x tier.
        /// 61 pancerza przy tierze 3 = 61 x 20 x 3 = 3660 punktow.
        /// </summary>
        internal static int ArmorPool(ItemObject it)
        {
            try
            {
                if (it == null || !it.HasArmorComponent) return 0;
                var a = it.ArmorComponent;
                int pts = it.Type == ItemObject.ItemTypeEnum.HorseHarness
                    ? a.BodyArmor
                    : a.HeadArmor + a.BodyArmor + a.LegArmor + a.ArmArmor;
                int tier = Recipes.Grade(it);
                // wpis 95 (audyt zuzycia pkt 7): rekawice (10 pkt) i nogawice zuzywaly sie 5-8x szybciej niz napiersnik - podloga puli
                pts = Math.Max(pts, Math.Max(1, Settings.Current.ArmorPoolMinPoints));
                int pool = (int)(pts * Math.Max(1f, Settings.Current.DurabilityPerArmorPoint) * tier);
                return Math.Max(1, pool);
            }
            catch { return 0; }
        }

        /// <summary>Zuzycie per slot z ksiegi bitwy + ewentualna baza rozlozona po staremu.</summary>
        private void ApplyWearPerSlot(float[] perSlot, float flat)
        {
            try
            {
                var eq = Hero.MainHero.BattleEquipment;
                for (int slot = 0; slot < 12; slot++)
                {
                    float amount = (perSlot != null ? perSlot[slot] : 0f) + flat;
                    if (amount <= 0.01f) continue;
                    var el = eq[slot];
                    if (el.Item == null || NoWear(el.Item)) continue;
                    if (el.Item.ItemComponent == null || el.Item.ItemComponent.ItemModifierGroup == null) continue;
                    float cond = GetCondition(slot, el);
                    if (el.Item.HasArmorComponent)
                    {
                        // pancerz: ksiega niesie SUROWE obrazenia; pula = pancerz x 20 x tier,
                        // kazdy punkt obrazen zdejmuje jeden punkt puli
                        int pool = ArmorPool(el.Item);
                        if (pool > 0) cond = MathF.Max(0f, cond - amount * 100f / pool);
                    }
                    else
                    {
                        int tier = Recipes.Grade(el.Item);
                        float sturdiness = 1f + MathF.Max(0, tier - 1) * Settings.Current.TierDurabilityFactor;
                        cond = MathF.Max(0f, cond - amount / sturdiness);
                    }
                    SetCondition(slot, cond);
                    if (cond <= 0f && Settings.Current.BreakAtZeroCondition)
                    {
                        Log.Player(el.Item.Name + " gave out in the fight and is beyond saving.", true);
                        eq[slot] = new EquipmentElement(null);
                        SetCondition(slot, 100f);
                        continue;
                    }
                    ApplyModifierForCondition(slot, cond);
                }
            }
            catch (Exception e) { Log.Error("ApplyWearPerSlot", e); }
        }

        private void ApplyWear(float amount)
        {
            try
            {
                var eq = Hero.MainHero.BattleEquipment;
                for (int slot = 0; slot < 12; slot++)
                {
                    var el = eq[slot];
                    if (el.Item == null || NoWear(el.Item)) continue;
                    if (el.Item.ItemComponent == null || el.Item.ItemComponent.ItemModifierGroup == null) continue;

                    float cond = GetCondition(slot, el);
                    int tier = Recipes.Grade(el.Item);
                    float sturdiness = 1f + MathF.Max(0, tier - 1) * Settings.Current.TierDurabilityFactor;
                    float roll = amount * (0.6f + MBRandom.RandomFloat * 0.8f) / sturdiness;
                    cond = MathF.Max(0f, cond - roll);
                    SetCondition(slot, cond);
                    if (cond <= 0f && Settings.Current.BreakAtZeroCondition)
                    {
                        Log.Player(el.Item.Name + " gave out in the fight and is beyond saving.", true);
                        Log.Info("Przedmiot pekl: " + el.Item.StringId);
                        eq[slot] = new EquipmentElement(null);
                        SetCondition(slot, 100f);
                        continue;
                    }
                    ApplyModifierForCondition(slot, cond);
                }
            }
            catch (Exception e) { Log.Error("ApplyWear", e); }
        }

        /// <summary>
        /// Stan CZESCI, nie przegrodki. Ksiega trzyma teraz takze ID przedmiotu:
        /// bez tego swiezo zalozony kirys dziedziczyl zuzycie po poprzednim
        /// (slot 6 pamietal 30%, a nowka zaraz dostawala dopisek "Battered").
        /// Inna sztuka w slocie = nowy rachunek od 100%.
        /// </summary>
        private float GetCondition(int slot, EquipmentElement el)
        {
            string id = el.Item != null ? el.Item.StringId : "";
            for (int i = 0; i < _condition.Count; i++)
            {
                var p = _condition[i].Split('|');
                if (int.Parse(p[0]) != slot) continue;
                string had = p.Length > 3 ? p[3] : "";
                if (had.Length == 0)
                {
                    // stary zapis (bez ID) - przypisujemy go temu, co teraz lezy w slocie
                    _condition[i] = p[0] + "|" + p[1] + "|" + (p.Length > 2 ? p[2] : "") + "|" + id;
                    return float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture);
                }
                if (had == id)
                {
                    // ZBROJA Z KUZNI JAK BRON: ta sama nazwa, inny stan niz zostawilo zuzycie = INNA sztuka (dwie kopie tej samej
                    // zbroi z kuzni - legendarna i zardzewiala); bez tego naprawa dawala zamienionej sztuce stan poprzedniczki
                    if (ArmourQuality.On && !SamePiece(p, el)) { _condition[i] = BookLine(slot, el, id); return 100f; }
                    if (ArmourQuality.On && p.Length == 4) _condition[i] = _condition[i] + "|" + ModId(el);   // stary wpis: od teraz zna stan sztuki
                    return float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture);
                }
                // w slocie lezy INNA sztuka - stary rachunek jej nie dotyczy
                _condition[i] = ArmourQuality.On ? BookLine(slot, el, id) : slot + "|100|" + (el.ItemModifier != null ? el.ItemModifier.StringId : "") + "|" + id;
                return 100f;
            }
            // pierwszy raz - zapamietaj oryginalny modyfikator i sztuke
            _condition.Add(ArmourQuality.On ? BookLine(slot, el, id) : slot + "|100|" + (el.ItemModifier != null ? el.ItemModifier.StringId : "") + "|" + id);
            return 100f;
        }

        private static string ModId(EquipmentElement el) { return el.ItemModifier != null ? el.ItemModifier.StringId : ""; }

        /// <summary>
        /// ZBROJA Z KUZNI JAK BRON: czy w slocie lezy TA SAMA sztuka, ktora zna wpis ksiegi (ta sama nazwa nie wystarcza).
        /// Wpis z piatym polem - po stanie, w jakim ja zostawilismy. Stary wpis (4 pola, zapis sprzed paczki) stanu nie zna:
        /// ta sama sztuka ma swoj stan oryginalny albo dokladnie ten stan zuzycia, ktory ksiega daje przy tym stanie - kazdy
        /// inny stan to inna kopia tej zbroi. Bez tego swiezo wykuta lordly albo legendarna zalozona w miejsce zuzytej zwyklej
        /// przejmowala stary wpis (zuzycie i "oryginalny" stan zwyklej) i u kowala stawala sie zwykla, a zardzewiala kopia
        /// zalozona w miejsce lekko zuzytej legendy dostawala u kowala legende (podwojenie, jak w 127).
        /// </summary>
        private static bool SamePiece(string[] p, EquipmentElement el)
        {
            string now = ModId(el);
            if (p.Length > 4) return p[4] == now;
            string orig = p.Length > 2 ? p[2] : "";
            if (now == orig) return true;
            float cond;
            if (p.Length < 2 || !float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out cond))
                return true;   // nieczytelny wpis - jak dotad (po samej nazwie)
            var group = el.Item != null && el.Item.ItemComponent != null ? el.Item.ItemComponent.ItemModifierGroup : null;
            var worn = WornModifierFor(group, cond);
            return worn != null && worn == el.ItemModifier;
        }

        /// <summary>Nowy wpis ksiegi: slot|stan|stan oryginalny|sztuka|stan, ktory sztuka ma teraz (piate pole - zbroja z kuzni jak bron).</summary>
        private static string BookLine(int slot, EquipmentElement el, string id) { return slot + "|100|" + ModId(el) + "|" + id + "|" + ModId(el); }

        /// <summary>Piate pole wpisu: stan, w jakim zostawilismy sztuke w slocie (po zuzyciu albo naprawie).</summary>
        private void SetLast(int slot, string modId)
        {
            for (int i = 0; i < _condition.Count; i++)
            {
                var p = _condition[i].Split('|');
                if (int.Parse(p[0]) != slot) continue;
                _condition[i] = p[0] + "|" + (p.Length > 1 ? p[1] : "100") + "|" + (p.Length > 2 ? p[2] : "") + "|" + (p.Length > 3 ? p[3] : "") + "|" + (modId ?? "");
                return;
            }
        }

        /// <summary>
        /// Po naprawie zalozonej czesci stan wraca do 100 - inaczej Wear odlozylby modyfikator z powrotem - a ksiega zapamietuje
        /// NOWY stan sztuki jako oryginal. Przeglad 07.10: dotad zostawal modyfikator sprzed naprawy (np. Plundered), wiec
        /// pozniejsza naprawa calej uprzezy brala zaplate drugi raz i przywracala sztuce stan sprzed oplaconej naprawy.
        /// ZBROJA Z KUZNI JAK BRON (132): przy wlaczonej jakosci zbroi wpis z piatym polem (BookLine) - sztuka zaczyna ksiege od
        /// nowa w stanie, jaki ma teraz; przy wylaczonej - wpis 4 pola (ta sama regula, scalenie TOWARY 3).
        /// </summary>
        internal void ResetSlotCondition(int slot)
        {
            try
            {
                var el = Hero.MainHero.BattleEquipment[slot];
                string id = el.Item != null ? el.Item.StringId : "";
                string line = ArmourQuality.On ? BookLine(slot, el, id) : slot + "|100|" + ModId(el) + "|" + id;
                for (int i = 0; i < _condition.Count; i++)
                {
                    var p = _condition[i].Split('|');
                    if (int.Parse(p[0]) != slot) continue;
                    _condition[i] = line;
                    return;
                }
                _condition.Add(line);
            }
            catch (Exception e) { Log.Error("ResetSlotCondition", e); }
        }

        /// <summary>
        /// ZBROJA Z KUZNI JAK BRON: w jakim stanie lawka naprawcza oddaje zalozona sztuke. Zuzyta w boju sztuka, ktora wyszla
        /// z kuzni (albo z lupu) dobra, lordly albo legendarna, wraca do swojego stanu; reszta - zwykla (jak dotad). Dotad lawka
        /// zawsze dawala zwykla: zuzyta legenda po naprawie na lawce przestawala byc legenda.
        /// </summary>
        internal ItemModifier GoodOriginal(int slot)
        {
            try
            {
                if (!ArmourQuality.On || GetConditionQuiet(slot) >= 100f) return null;   // ksiega nie zna tej sztuki albo nie byla bita
                var origId = OriginalModifier(slot);
                var orig = string.IsNullOrEmpty(origId) ? null : MBObjectManager.Instance.GetObject<ItemModifier>(origId);
                return orig != null && orig.PriceMultiplier >= 1f ? orig : null;
            }
            catch (Exception e) { Log.Error("GoodOriginal", e); return null; }
        }

        private void SetCondition(int slot, float cond)
        {
            for (int i = 0; i < _condition.Count; i++)
            {
                var p = _condition[i].Split('|');
                if (int.Parse(p[0]) != slot) continue;
                _condition[i] = slot + "|" + cond.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
                                + "|" + (p.Length > 2 ? p[2] : "") + "|" + (p.Length > 3 ? p[3] : "")
                                // zbroja z kuzni jak bron: stan sztuki zostaje w ksiedze; przy wylaczonym znika (wpis jak w 127 -
                                // po ponownym wlaczeniu stary wpis dostaje stan sztuki od nowa, zamiast uznac ja za obca)
                                + (ArmourQuality.On && p.Length > 4 ? "|" + p[4] : "");
                return;
            }
        }

        private string OriginalModifier(int slot)
        {
            foreach (var c in _condition)
            {
                var p = c.Split('|');
                if (int.Parse(p[0]) == slot) return p.Length > 2 ? p[2] : "";
            }
            return "";
        }

        /// <summary>
        /// Stan zuzycia, ktory ksiega daje sztuce przy tym stanie: modyfikator z wlasnej grupy przedmiotu - im gorszy stan, tym
        /// gorszy modyfikator; null = stan powyzej progu ThresholdWorn albo grupa bez stanow zuzycia. Jedna regula dla zuzycia
        /// (ApplyModifierForCondition) i dla rozpoznania sztuki ze starego wpisu ksiegi (SamePiece).
        /// </summary>
        private static ItemModifier WornModifierFor(ItemModifierGroup group, float cond)
        {
            if (group == null) return null;
            var s = Settings.Current;
            int step;
            if (cond > s.ThresholdWorn) step = 0;
            else if (cond > s.ThresholdDamaged) step = 1;
            else if (cond > s.ThresholdRuined) step = 2;
            else step = 3;
            if (step == 0) return null;

            // posortuj modyfikatory od najgorszego (najnizszy mnoznik ceny)
            var bad = new List<ItemModifier>();
            foreach (var m in group.ItemModifiers)
                if (m != null && m.PriceMultiplier < 1f) bad.Add(m);
            if (bad.Count == 0) return null;
            bad.Sort((a, b) => a.PriceMultiplier.CompareTo(b.PriceMultiplier));   // najgorszy pierwszy

            return step >= 3 ? bad[0] : bad[MathF.Min(bad.Count - 1, bad.Count - step)];
        }

        /// <summary>Dobiera modyfikator z wlasnej grupy przedmiotu - im gorszy stan, tym gorszy modyfikator.</summary>
        private void ApplyModifierForCondition(int slot, float cond)
        {
            try
            {
                var eq = Hero.MainHero.BattleEquipment;
                var el = eq[slot];
                if (el.Item == null) return;
                var group = el.Item.ItemComponent != null ? el.Item.ItemComponent.ItemModifierGroup : null;
                if (group == null) return;

                ItemModifier chosen = WornModifierFor(group, cond);
                if (chosen == null) return;
                if (el.ItemModifier == chosen) return;
                // ZBROJA Z KUZNI JAK BRON: zuzycie nigdy nie poprawia sztuki - zardzewiala zbroja z kuzni (stan 30%) przy 70%
                // ksiegi nie staje sie "tylko pogieta" (60%, lepsza ochrona); zostaje gorszy z dwoch stanow
                if (ArmourQuality.On && el.ItemModifier != null
                    && ConditionScaling.ConditionOf(el.ItemModifier) < 0.999f
                    && ConditionScaling.ConditionOf(el.ItemModifier) <= ConditionScaling.ConditionOf(chosen)) return;

                eq[slot] = new EquipmentElement(el.Item, chosen);
                if (ArmourQuality.On) SetLast(slot, chosen.StringId);
                Log.Info("Zuzycie: slot " + slot + " " + el.Item.StringId + " -> " + chosen.StringId + " (stan " + (int)cond + ")");
                Log.Player(el.Item.Name + " is showing hard use (" + chosen.Name + ", condition " + (int)cond + "%).", true);
            }
            catch (Exception e) { Log.Error("ApplyModifierForCondition", e); }
        }

        // ---------------------------------------------------------- naprawa
        /// <summary>Robota kowala przy jednej czesci uprzezy: dawniej wartosc x brak x RepairCostFactor; przy regule kowali miasta
        /// (MendMaterial.RuleOn) - ulamek wykonania (MendMaterialMaxShare x brak) po dniowce mistrza w tym miescie, jak lup i sztuka na lawie.</summary>
        private static int HarnessLabor(ItemObject it, float cond)
        {
            float missing = Math.Max(0f, (100f - cond) / 100f);
            // poprawka po audycie TOWARY 3: stara kwota starym wyrazeniem (n120) - inne wyrazenie float dawalo czasem 1 d roznicy przy wylaczonej regule
            int old = (int)(it.Value * (1f - cond / 100f) * Settings.Current.RepairCostFactor);
            if (!MendMaterial.RuleOn) return old;
            var s = Settings.Current;
            return MendMaterial.Labor(it, Math.Max(0f, s.MendMaterialMaxShare) * Math.Min(1f, missing), Settlement.CurrentSettlement, old);
        }

        internal int RepairCost()
        {
            int cost = 0;
            try
            {
                var eq = Hero.MainHero.BattleEquipment;
                for (int slot = 0; slot < 12; slot++)
                {
                    var el = eq[slot];
                    if (el.Item == null) continue;
                    float cond = GetConditionQuiet(slot);
                    if (cond >= 100f) continue;
                    if (LootPrices.HarnessWreck(el.ItemModifier, cond)) continue;   // paczka 158: wrak na zlom - kowale miasta go nie odnawiaja
                    cost += HarnessLabor(el.Item, cond);
                }
            }
            catch (Exception e) { Log.Error("RepairCost", e); }
            return cost;
        }

        /// <summary>Paczka 158: ile czesci na grzbiecie to wraki (modyfikator wraku albo stan <= 10%) - kowale miasta ich nie odnawiaja.</summary>
        internal int HarnessWrecks()
        {
            int n = 0;
            try
            {
                if (!LootPrices.ScrapRule) return 0;
                var eq = Hero.MainHero.BattleEquipment;
                for (int slot = 0; slot < 12; slot++)
                {
                    if (eq[slot].Item == null) continue;
                    float cond = GetConditionQuiet(slot);
                    if (cond < 100f && LootPrices.HarnessWreck(eq[slot].ItemModifier, cond)) n++;
                }
            }
            catch (Exception e) { Log.Error("HarnessWrecks", e); }
            return n;
        }

        /// <summary>Poprawka po recenzji 158: czesc zalozona w tym slocie to wrak (modyfikator wraku albo stan w ksiedze <= 10%) - ta sama regula co
        /// HarnessWrecks / RepairCost, dla "Pick a piece" (SmithMenu.SmithRefusesHere). Wylaczone WrecksToScrap - false.</summary>
        internal bool SlotWreck(int slot)
        {
            try
            {
                if (!LootPrices.ScrapRule || slot < 0 || slot >= 12) return false;
                var el = Hero.MainHero.BattleEquipment[slot];
                if (el.Item == null) return false;
                return LootPrices.HarnessWreck(el.ItemModifier, GetConditionQuiet(slot));
            }
            catch (Exception e) { Log.Error("SlotWreck", e); return false; }
        }

        /// <summary>Ile czesci na grzbiecie wymaga naprawy.</summary>
        internal int WornPieces()
        {
            int n = 0;
            try
            {
                var eq = Hero.MainHero.BattleEquipment;
                for (int slot = 0; slot < 12; slot++)
                    if (eq[slot].Item != null && GetConditionQuiet(slot) < 100f) n++;
            }
            catch (Exception e) { Log.Error("WornPieces", e); }
            return n;
        }

        /// <summary>Podglad stanu bez zapisu - takze pilnuje, ze to TA SAMA sztuka.</summary>
        /// <summary>
        /// STAN TEJ SZTUKI, ktora masz na sobie - prosto z ksiegi, nie z etykiety.
        /// Panel przedmiotu czytal dotad procent z MODYFIKATORA, a modyfikator
        /// dokladamy dopiero po przekroczeniu progu. Kirys zbity ze 100% na 82%
        /// wygladal wiec jak nowka ("ciagle mam 100%" - Jeff). Zwraca -1, gdy tej
        /// sztuki nie ma na grzbiecie i ksiega o niej nic nie wie.
        /// </summary>
        internal float WornCondition(ItemObject item)
        {
            try
            {
                if (item == null) return -1f;
                var eq = Hero.MainHero.BattleEquipment;
                for (int slot = 0; slot < 12; slot++)
                {
                    if (eq[slot].Item != item) continue;
                    return GetConditionQuiet(slot);
                }
            }
            catch { }
            return -1f;
        }

        private float GetConditionQuiet(int slot)
        {
            string id = "";
            EquipmentElement cur = default(EquipmentElement);
            try
            {
                cur = Hero.MainHero.BattleEquipment[slot];
                var it = cur.Item;
                id = it != null ? it.StringId : "";
            }
            catch { }
            foreach (var c in _condition)
            {
                var p = c.Split('|');
                if (int.Parse(p[0]) != slot) continue;
                string had = p.Length > 3 ? p[3] : "";
                if (had.Length > 0 && id.Length > 0 && had != id) return 100f;   // inna czesc w slocie
                // zbroja z kuzni jak bron: ta sama nazwa w innym stanie, niz ja zostawilismy - inna sztuka (np. druga kopia z kuzni);
                // stary wpis bez stanu sztuki (zapis sprzed paczki) - SamePiece rozpoznaje ja wedle stanu oryginalnego i zuzycia
                if (ArmourQuality.On && id.Length > 0 && !SamePiece(p, cur)) return 100f;
                return float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture);
            }
            return 100f;
        }

        /// <summary>Naprawa przy wlasnym kowadle - material i wytrzymalosc zamiast zlota.</summary>
        internal void RepairAllSelf()
        {
            try
            {
                var eq = Hero.MainHero.BattleEquipment;
                int mended = 0;
                var blocked = new List<string>();
                for (int slot = 0; slot < 12; slot++)
                {
                    var el = eq[slot];
                    if (el.Item == null) continue;
                    float cond = GetConditionQuiet(slot);
                    if (cond >= 100f) continue;
                    float missing = (100f - cond) / 100f;
                    string why;
                    if (!Forge.SelfRepair(el.Item, missing, out why))
                    {
                        if (why != null && blocked.Count < 4) blocked.Add(el.Item.Name + ": needs " + why);
                        continue;
                    }

                    var origId = OriginalModifier(slot);
                    ItemModifier orig = string.IsNullOrEmpty(origId) ? null : MBObjectManager.Instance.GetObject<ItemModifier>(origId);
                    eq[slot] = new EquipmentElement(el.Item, orig);
                    SetCondition(slot, 100f);
                    if (ArmourQuality.On) SetLast(slot, origId);   // zbroja z kuzni jak bron: sztuka wrocila do swojego stanu
                    mended++;
                }
                if (mended > 0) Log.Player("You worked " + mended + " pieces back into shape yourself.");
                if (blocked.Count > 0)
                    foreach (var b in blocked) Log.Player(b, true);
                else if (mended == 0)
                    Log.Player("Your harness is sound - nothing on you needs mending.");
                Log.Info("Naprawa wlasnoreczna: " + mended + " szt., zablokowane: " + blocked.Count);
            }
            catch (Exception e) { Log.Error("RepairAllSelf", e); }
        }

        /// <summary>
        /// Plan naprawy uprzezy u kowali miasta (lawa naprawcza, SmithMendFromMarket): kazda czesc ze stanem ponizej 100 - robocizna
        /// jak dotad (wartosc x brak x RepairCostFactor), material wedle braku (MendMaterial.NeedsFor - ta sama regula co lup), brak
        /// materialu = ta czesc czeka. Wrakow tu nie ma: naprawa przywraca oryginalny modyfikator (stan lupu zostaje). slots - przegrodki,
        /// ktore kowale zrobia. Na probie - polke i zapas zmienia dopiero RepairAll (Commit).
        /// </summary>
        internal MendMaterial.Order PlanRepair(List<int> slots)
        {
            var o = new MendMaterial.Order(Settlement.CurrentSettlement);
            try
            {
                var eq = Hero.MainHero.BattleEquipment;
                for (int slot = 0; slot < 12; slot++)
                {
                    var el = eq[slot];
                    if (el.Item == null) continue;
                    float cond = GetConditionQuiet(slot);
                    if (cond >= 100f) continue;
                    if (LootPrices.HarnessWreck(el.ItemModifier, cond)) { o.Wrecks++; continue; }   // paczka 158: wrak na zlom (AddLot widzi sztuke bez modyfikatora)
                    float missing = (100f - cond) / 100f;
                    int labor = HarnessLabor(el.Item, cond);   // ta sama robocizna co RepairCost
                    if (o.AddLot(new EquipmentElement(el.Item), MendMaterial.NeedsFor(el.Item, missing), labor, 1, long.MaxValue, int.MaxValue) > 0 && slots != null)
                        slots.Add(slot);
                }
            }
            catch (Exception e) { Log.Error("PlanRepair", e); }
            return o;
        }

        /// <summary>Uprzaz u kowali miasta z materialem z targu (SmithMendFromMarket): czy zlecenie sie odbedzie - inaczej komunikat, nic nie
        /// zaplacone i nic nie zdjete z polki. Kowale robia tylko czesci, na ktore jest material (PlanRepair); reszta czeka.</summary>
        private static bool MarketRepairGo(MendMaterial.Order o, string town)
        {
            if (o.Pieces == 0 && o.Wait == 0 && o.NoSmith == 0 && o.Wrecks == 0) { Log.Player("Your gear is sound. Nothing to mend."); return false; }   // paczka 158: same wraki - ponizej, z napisem
            if (!o.Ok) { Log.Player("There are no town smiths here - nothing was mended.", true); return false; }
            if (o.Pieces == 0)
            {
                Log.Player("The smiths of " + town + " could mend nothing of your harness." + o.LeftEn(town), true);
                Log.Info("Lawa naprawcza (uprzaz) - kowale " + town + ": " + o.LogPl());
                return false;
            }
            if (Hero.MainHero.Gold < o.Total) { Log.Player("You cannot pay the smiths' price.", true); return false; }
            return true;
        }

        internal void RepairAll()
        {
            try
            {
                // SmithMendFromMarket: kowale miasta robia tylko czesci, na ktore jest material na targu (PlanRepair) - robota jak dotad
                // + material; reszta czeka. Przywracanie stanu - ta sama petla co dotad (jedna regula dla obu sciezek).
                List<int> only = null;
                int wrecksLeft = 0;   // paczka 158: wraki pominiete przy naprawie bez reguly kowali (z regula - order.Wrecks)
                MendMaterial.Order order = null;
                var here = Settlement.CurrentSettlement;
                string town = here != null && here.Name != null ? here.Name.ToString() : "this town";
                int cost;
                if (SmithMenu.MarketRule)
                {
                    only = new List<int>();
                    order = PlanRepair(only);
                    if (!MarketRepairGo(order, town)) return;
                    cost = order.Total;
                    order.Bench.Commit();
                }
                else
                {
                    cost = RepairCost();
                    if (cost <= 0 && HarnessWrecks() > 0) { Log.Player("The smith restores none of your harness for coin. " + HarnessWrecks() + LootPrices.WreckWhatEn(HarnessWrecks()), true); return; }   // paczka 158
                    if (cost <= 0) { Log.Player("Your gear is sound. Nothing to mend."); return; }
                    if (Hero.MainHero.Gold < cost) { Log.Player("You cannot pay the smith's price.", true); return; }
                }
                Pay.ToSettlement(cost);   // wpis 90 (audyt): zaplata kowalowi do kasy miasta, nie w nicosc

                var eq = Hero.MainHero.BattleEquipment;
                for (int slot = 0; slot < 12; slot++)
                {
                    if (only != null && !only.Contains(slot)) continue;   // SmithMendFromMarket: ta czesc czeka na material albo jest cala
                    var el = eq[slot];
                    if (el.Item == null) continue;
                    if (only == null && LootPrices.HarnessWreck(el.ItemModifier, GetConditionQuiet(slot))) { wrecksLeft++; continue; }   // paczka 158: bez reguly kowali - wrak tez zostaje
                    // ZBROJA Z KUZNI JAK BRON: kowal naprawia tylko sztuki zuzyte wedle ksiegi - te same, za ktore policzyl w RepairCost.
                    // Dotad kazda zalozona czesc dostawala "oryginalny" stan przegrodki: sztuka nigdy nie bita (zardzewiala zbroja
                    // z kuzni wychodzila zwykla za darmo, przy okazji naprawy butow) i sztuka o innej nazwie (dostawala stan
                    // poprzedniczki - takze legendarny, nawet z obcej grupy)
                    if (ArmourQuality.On && GetConditionQuiet(slot) >= 100f) continue;
                    var origId = OriginalModifier(slot);
                    ItemModifier orig = string.IsNullOrEmpty(origId) ? null : MBObjectManager.Instance.GetObject<ItemModifier>(origId);
                    eq[slot] = new EquipmentElement(el.Item, orig);
                    SetCondition(slot, 100f);
                    if (ArmourQuality.On) SetLast(slot, origId);
                }
                if (order != null)
                {
                    Log.Player("The smiths of " + town + " made " + order.Pieces + (order.Pieces == 1 ? " piece" : " pieces") + " of your harness whole again for " + cost
                               + " gold, paid into the town's coffers: " + order.Labor + " for their work and " + order.MatGold + " for materials from its market." + order.LeftEn(town));
                    Log.Info("Lawa naprawcza (uprzaz) - kowale " + town + ": sloty " + string.Join(",", only) + "; " + order.LogPl());
                    return;
                }
                Log.Player("The smith has made your harness whole again for " + cost + " gold." + (wrecksLeft > 0 ? " " + wrecksLeft + LootPrices.WreckWhatEn(wrecksLeft) : ""));
                Log.Info("Naprawa za " + cost + (wrecksLeft > 0 ? ", wraki na zlom " + wrecksLeft : ""));
            }
            catch (Exception e) { Log.Error("RepairAll", e); }
        }
    }
}
