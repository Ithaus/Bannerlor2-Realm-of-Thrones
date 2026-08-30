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
                Log.Info("AWANS: " + (from != null ? from.StringId : "?") + " -> "
                    + (to != null ? to.StringId : "?") + " x" + num + " | magazyn: " + total + " szt.");
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
        private Dictionary<string,int> _prisonerBaseline;

        public ArmouryBehavior() { Instance = this; }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                dataStore.SyncData("arm_condition", ref _condition);
                dataStore.SyncData("arm_projects", ref _projects);
                dataStore.SyncData("arm_player_stock", ref _playerStock);
                dataStore.SyncData("arm_ammo_refund_v1", ref _ammoRefundDone);
                if (dataStore.IsSaving) _armoryWear = BuildArmoryWearSnapshot();
                dataStore.SyncData("arm_armory_wear", ref _armoryWear);
                if (dataStore.IsLoading && _armoryWear != null && _armoryWear.Count > 0) _wearRestorePending = true;
                dataStore.SyncData("arm_orders", ref Orders.Board);
                dataStore.SyncData("arm_order_cooldowns", ref Orders.Cooldowns);
                // dniowka w kuzni MUSI przezyc save/load - inaczej po wczytaniu
                // gra znow kaze placic 25/h za oplacona juz dobe
                string daypass = DayPass.Export();
                dataStore.SyncData("arm_daypass", ref daypass);
                if (dataStore.IsLoading) DayPass.Import(daypass);
                string nightrest = NightRest.Export();
                dataStore.SyncData("arm_nightrest", ref nightrest);
                if (dataStore.IsLoading) NightRest.Import(nightrest);
                string glut = MarketGlut.Export();
                dataStore.SyncData("arm_glut", ref glut);
                if (dataStore.IsLoading) MarketGlut.Import(glut);
                // wiedza luczarska: odkryte wzory lukow/kusz i punkty nauki
                string lore = RangedLore.Export();
                dataStore.SyncData("arm_rangedlore", ref lore);
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
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
            // polegli oddaja rynsztunek na wozy zaraz po bitwie
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this,
                delegate (MapEvent me) { try { if (me != null && me.IsPlayerMapEvent) GatherFallen(); } catch { } });
            CampaignEvents.OnNewItemCraftedEvent.AddNonSerializedListener(this, OnNewItemCrafted);
            // DEPOZYT KWATERMISTRZA NIE MA PRAWA WEJSC DO SAVE'A: schowane
            // na czas ekranu zbrojowni sztuki zyja poza rosterem - zapis gry
            // w tym oknie utrwalilby save BEZ nich (27.08 bylo o wlos: save
            // 16:33, schowanie 1177 szt. o 16:34). Przed kazdym zapisem
            // wszystko wraca na polki.
            CampaignEvents.OnBeforeSaveEvent.AddNonSerializedListener(this,
                delegate { try { QuartermasterEscrow.ReleaseReserve(); } catch { } });
            // jeniec wziety po bitwie zostaje obszukany - jego rynsztunek idzie do sakw
            CampaignEvents.OnPrisonerTakenEvent.AddNonSerializedListener(this, OnPrisonerTaken);
            // DIAGNOSTYKA (Jeff 29.08: "awans wycina sprzet z magazynu?") -
            // ani DTE, ani my nie sluchamy awansow, wiec logujemy sume
            // magazynu przy kazdym awansie: jak suma spada miedzy wpisami,
            // zlodziej istnieje; jak stoi - to zmiana wzorca po awansie.
            CampaignEvents.PlayerUpgradedTroopsEvent.AddNonSerializedListener(this, OnPlayerUpgradedTroops);
            // klawisz O na mapie: szybki oboz (BannerKings) bez klikania przez ekrany
            CampaignEvents.TickEvent.AddNonSerializedListener(this, delegate (float dt) { NightRest.OnTick(dt); });
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
            // pas bezpieczenstwa depozytu kwatermistrza: czas plynie = ekran
            // zbrojowni zamkniety; gdyby domkniecie nie oddalo sprzetu, oddajemy tu
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this,
                delegate
                {
                    try { QuartermasterEscrow.ReleaseReserve(); } catch { }
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
                            QuartermasterLaw.ShoutShortages("Quartermaster after the battle - the men go SHORT (have/need):");
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
                // po swiezej bitwie starocie wojskowe ida w niepamiec
                try { if (CampaignTime.Now <= _spoilsWindow) TrimWarStores(); } catch { }
                // samonaprawa depozytu: otwarte menu gry = na pewno NIE ekran
                // zbrojowni; jesli cokolwiek wisi w depozycie (Release nie
                // odpalil przy zamykaniu ekranu), wraca na polki teraz
                try { QuartermasterEscrow.ReleaseReserve(); } catch { }
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
                        var mod = el.EquipmentElement.ItemModifier;
                        if (mod == null) continue;
                        if (mod.PriceMultiplier * 100f > s.LootMinConditionPercent + 0.01f) continue;
                        roster.AddToCounts(el.EquipmentElement, -el.Amount);
                        cut += el.Amount;
                    }
                }
                if (cut > 0)
                {
                    Log.Info("CleanseTrashInBags: " + cut + " szt. ponizej progu zniszczenia wyrzucono z sakw/magazynu.");
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
                            if (LegendaryLaw.IsLegend(item)) continue;
                            armory.AddToCounts(new EquipmentElement(item, PickWornModifier(item)), 1);
                            StockDeposit(item.StringId, 1);   // dowodca dysponuje sprzetem poleglych
                            pieces++;
                        }
                    }
                }
                if (pieces > 0)
                {
                    Log.Info("GatherFallen: " + men + " poleglych oddalo " + pieces + " sztuk na wozy (przeksiegowane na gracza).");
                    Log.Player("The fallen are gathered - " + pieces + " pieces of their kit come back on the wagons, yours to claim.", true);
                }
            }
            catch (Exception e) { Log.Error("GatherFallen", e); }
        }

        /// <summary>
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
            try { FixCharcoalWeight(); } catch (Exception e) { Log.Error("FixCharcoalWeight", e); }
            try { WearGroups.Fix(); } catch (Exception e) { Log.Error("WearGroups.Fix", e); }
            try { CleanseNegativeStacks(); } catch (Exception e) { Log.Error("CleanseNegativeStacks", e); }
            try { TryRestoreArmoryWear("sesja"); } catch (Exception e) { Log.Error("TryRestoreArmoryWear", e); }
            try { CleanseDragonStables(true); } catch (Exception e) { Log.Error("CleanseDragonStables", e); }
            try { CleanseTrashInBags(); } catch (Exception e) { Log.Error("CleanseTrashInBags", e); }
            try { QualityRich.Enrich(); } catch (Exception e) { Log.Error("QualityRich.Enrich", e); }
            try { SmithMenu.Add(starter); Log.Info("Menu kowala dodane."); }
            catch (Exception e) { Log.Error("OnSessionLaunched", e); }
            try { CleanseAmmo(); } catch (Exception e) { Log.Error("CleanseAmmo", e); }
            try { NightRest.AddMenus(starter); } catch (Exception e) { Log.Error("NightRest.AddMenus", e); }
            try { RangedLore.ReportLedger(false); } catch (Exception e) { Log.Error("RangedLore.ReportLedger", e); }
        }

        private static float Today { get { return (float)CampaignTime.Now.ToDays; } }
        private static float _lastIdleLogDay = -10f;

        // ---------------------------------------------------------- robota przy kowadle
        internal void StartProject(ItemObject item, int tempo, float days, Settlement where)
        { StartProject(item, tempo, days, where, "", ""); }

        internal void StartProject(ItemObject item, int tempo, float days, Settlement where, string kind, string modifierId)
        {
            try
            {
                var p = new Project
                {
                    Item = item, DaysLeft = days, Tempo = tempo,
                    SettlementId = where != null ? where.StringId : "",
                    Kind = kind ?? "", ModifierId = modifierId ?? ""
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
            if (p.Kind == "van") Forge.Deliver(p.Item, p.ModifierId);   // sukces zapadl przy kowadle
            else Forge.Finish(p.Item, p.Tempo);
        }

        private void AdvanceProjects()
        {
            try
            {
                if (_projects.Count == 0) return;
                var here = Settlement.CurrentSettlement;
                // KOWAL PRACUJE, KIEDY TY JEDZIESZ (Jeff 27.08: "wykulem miecz,
                // odczekalem dlugo i przepadl" - stary zegar tykal TYLKO, gdy
                // gracz stal w osadzie projektu, a on czekal na mapie obok).
                // Teraz robota idzie zawsze; XP za prace w trakcie dostajesz
                // tylko na miejscu (to twoje rece), a GOTOWY wyrob lezy
                // w warsztacie i czeka na odbior przy wejsciu do osady.
                bool remote = Settings.Current.ForgeWorksWithoutYou;
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

                    // KROK GODZINOWY: zegar konczy sie DOKLADNIE z robota, bez
                    // doczekiwania do polnocy (blad, ktory wkurzyl Jeffa przy mieczu)
                    p.DaysLeft -= 1f / 24f;
                    var rr = Recipes.For(p.Item);
                    // XP liczy sie od WLASCIWEGO czasu projektu: bron "van" ma swoj
                    // przelicznik (WeaponDaysPerTier), pancerze swoj (Jeff 29.08:
                    // "balagan z godzinami" - to byl jeden z rozjazdow)
                    float totalDays = p.Kind == "van"
                        ? MathF.Max(0.5f, Recipes.Grade(p.Item) * Settings.Current.WeaponDaysPerTier)
                        : MathF.Max(1f, rr.Tier * Settings.Current.DaysPerTier * Project.TimeFactor(p.Tempo));
                    if (atForge)   // XP tylko za wlasna prace przy kowadle
                        Hero.MainHero.HeroDeveloper.AddSkillXp(DefaultSkills.Crafting,
                            Forge.ProjectXp(rr) * Settings.Current.XpShareWhileWorking / totalDays / 24f);

                    _projects.Remove(line);
                    if (p.DaysLeft > 0f) _projects.Add(p.Serialize());
                    else if (atForge) HandOver(p);
                    else
                    {
                        // skonczone pod twoja nieobecnosc: wyrob czeka na polce
                        p.DaysLeft = 0f;
                        _projects.Add(p.Serialize());
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
            try { Orders.DailyTick(); }
            catch (Exception e) { Log.Error("OnDailyTick", e); }
            try { MarketGlut.DailyDigest(); }
            catch (Exception e) { Log.Error("GlutDigest", e); }
            try
            {
                // poranny meldunek: braki wykrzyczane na glos, zeby Jeff WIEDZIAL
                // bez otwierania zbrojowni ("czemu ja o tym nie wiem!")
                QuartermasterLaw.ShoutShortages("Quartermaster's morning report - the men go SHORT (have/need):");
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
            try
            {
                var s = Settings.Current;
                if (!s.WeaponCraftingTakesTime || isCraftingOrderItem) return;   // zlecenia maja swoje terminy
                if (item == null) return;
                var here = Settlement.CurrentSettlement;
                if (here == null) return;

                int tier = Recipes.Grade(item);
                float days = MathF.Max(0.5f, tier * s.WeaponDaysPerTier);

                var roster = MobileParty.MainParty.ItemRoster;
                var el = new EquipmentElement(item, modifier);
                if (roster.GetItemNumber(item) > 0) roster.AddToCounts(el, -1);

                // "van": sukces i jakosc zapadly PRZY KOWADLE (vanilla) - dostawa
                // po czasie ma NIE rzucac drugi raz (Jeff: "wykulem, a potem fail
                // i miecza nie ma"). Modyfikator jedzie z projektem i wraca.
                StartProject(item, 1, days, here, "van", modifier != null ? modifier.StringId : "");
                Log.Player("The blade is roughed out. " + Project.TimeLabel(days) + " of the SMITH'S finishing work remain at "
                           + here.Name + " - he works it himself, wherever you ride.");
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

                // PRZEGRANA ALBO UCIECZKA = NIC Z POLA. Wraki zbierane w trakcie
                // misji lezaly w kolejce niezaleznie od wyniku i po godzinie
                // wsypywaly sie do sakw - takze wtedy, gdy gracz uciekl z pola
                // ("jak uciekam, to nie zdobywam gearu, przeciez uciekłem" - Jeff).
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
                _spoilsWindow = CampaignTime.HoursFromNow(2f);

                // KRYJOWKA: zaden ekran lupow nie przyjdzie - dzialka gracza
                // z kolejki BattlefieldLaw idzie prosto do sakw, a jency
                // (bandyci poddani po walce) sa obszukiwani od reki
                if (mapEvent.IsHideoutBattle)
                {
                    BattlefieldLaw.FlushShareToBaggage("kryjowka zdobyta");
                    TryStripNewCaptives("kryjowka");
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
                if (share <= 0f) return;
                int worn = 0;

                foreach (var type in QuartermasterLaw.KitTypes)
                {
                    int inUse = QuartermasterLaw.WornFor(type, needs);
                    if (inUse <= 0) continue;
                    int hits = (int)MathF.Ceiling(inUse * share);

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
                        var el = armory.GetElementCopyAtIndex(idx[MBRandom.RandomInt(idx.Count)]).EquipmentElement;
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
                    Log.Info("Zuzycie wojska: " + worn + " sztuk zeszlo o stopien.");
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
            if (fromBattle)
            {
                try { _prisonerBaseline = SnapshotPrisoners(); } catch { }
                return;
            }
            try
            {
                var half = CampaignTime.HoursFromNow(0.5f);
                if (_spoilsWindow < half) _spoilsWindow = half;
            }
            catch { }
            TryStripNewCaptives("OnPrisonerTaken");
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
                if (fixedUp > 0) Log.Info("Amunicja oczyszczona ze stanow: " + fixedUp + " szt.");
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
                if (had == id) return float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture);
                // w slocie lezy INNA sztuka - stary rachunek jej nie dotyczy
                _condition[i] = slot + "|100|" + (el.ItemModifier != null ? el.ItemModifier.StringId : "") + "|" + id;
                return 100f;
            }
            // pierwszy raz - zapamietaj oryginalny modyfikator i sztuke
            _condition.Add(slot + "|100|" + (el.ItemModifier != null ? el.ItemModifier.StringId : "") + "|" + id);
            return 100f;
        }

        /// <summary>Po naprawie zalozonej czesci stan wraca do 100 - inaczej Wear odlozylby modyfikator z powrotem.</summary>
        internal void ResetSlotCondition(int slot) { SetCondition(slot, 100f); }

        private void SetCondition(int slot, float cond)
        {
            for (int i = 0; i < _condition.Count; i++)
            {
                var p = _condition[i].Split('|');
                if (int.Parse(p[0]) != slot) continue;
                _condition[i] = slot + "|" + cond.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
                                + "|" + (p.Length > 2 ? p[2] : "") + "|" + (p.Length > 3 ? p[3] : "");
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

        /// <summary>Dobiera modyfikator z wlasnej grupy przedmiotu - im gorszy stan, tym gorszy modyfikator.</summary>
        private void ApplyModifierForCondition(int slot, float cond)
        {
            try
            {
                var s = Settings.Current;
                var eq = Hero.MainHero.BattleEquipment;
                var el = eq[slot];
                if (el.Item == null) return;
                var group = el.Item.ItemComponent != null ? el.Item.ItemComponent.ItemModifierGroup : null;
                if (group == null) return;

                int step;
                if (cond > s.ThresholdWorn) step = 0;
                else if (cond > s.ThresholdDamaged) step = 1;
                else if (cond > s.ThresholdRuined) step = 2;
                else step = 3;
                if (step == 0) return;

                // posortuj modyfikatory od najgorszego (najnizszy mnoznik ceny)
                var bad = new List<ItemModifier>();
                foreach (var m in group.ItemModifiers)
                    if (m != null && m.PriceMultiplier < 1f) bad.Add(m);
                if (bad.Count == 0) return;
                bad.Sort((a, b) => a.PriceMultiplier.CompareTo(b.PriceMultiplier));   // najgorszy pierwszy

                ItemModifier chosen = step >= 3 ? bad[0] : bad[MathF.Min(bad.Count - 1, bad.Count - step)];
                if (el.ItemModifier == chosen) return;

                eq[slot] = new EquipmentElement(el.Item, chosen);
                Log.Info("Zuzycie: slot " + slot + " " + el.Item.StringId + " -> " + chosen.StringId + " (stan " + (int)cond + ")");
                Log.Player(el.Item.Name + " is showing hard use (" + chosen.Name + ", condition " + (int)cond + "%).", true);
            }
            catch (Exception e) { Log.Error("ApplyModifierForCondition", e); }
        }

        // ---------------------------------------------------------- naprawa
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
                    cost += (int)(el.Item.Value * (1f - cond / 100f) * Settings.Current.RepairCostFactor);
                }
            }
            catch (Exception e) { Log.Error("RepairCost", e); }
            return cost;
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
            try
            {
                var it = Hero.MainHero.BattleEquipment[slot].Item;
                id = it != null ? it.StringId : "";
            }
            catch { }
            foreach (var c in _condition)
            {
                var p = c.Split('|');
                if (int.Parse(p[0]) != slot) continue;
                string had = p.Length > 3 ? p[3] : "";
                if (had.Length > 0 && id.Length > 0 && had != id) return 100f;   // inna czesc w slocie
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

        internal void RepairAll()
        {
            try
            {
                int cost = RepairCost();
                if (cost <= 0) { Log.Player("Your gear is sound. Nothing to mend."); return; }
                if (Hero.MainHero.Gold < cost) { Log.Player("You cannot pay the smith's price.", true); return; }
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, cost);

                var eq = Hero.MainHero.BattleEquipment;
                for (int slot = 0; slot < 12; slot++)
                {
                    var el = eq[slot];
                    if (el.Item == null) continue;
                    var origId = OriginalModifier(slot);
                    ItemModifier orig = string.IsNullOrEmpty(origId) ? null : MBObjectManager.Instance.GetObject<ItemModifier>(origId);
                    eq[slot] = new EquipmentElement(el.Item, orig);
                    SetCondition(slot, 100f);
                }
                Log.Player("The smith has made your harness whole again for " + cost + " gold.");
                Log.Info("Naprawa za " + cost);
            }
            catch (Exception e) { Log.Error("RepairAll", e); }
        }
    }
}
