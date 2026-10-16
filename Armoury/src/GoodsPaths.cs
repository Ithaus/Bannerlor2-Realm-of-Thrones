using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// ZC1 DROGI TOWAROW (projekt docs/PROJEKT-ZYCIE-CODZIENNE-2026-10-16.md rozdz. 13 i 14, paczka ZC1; audyt towarow 16.10 roz/zycie/towary.md "Dziwactwa").
    /// Jedna linia doby "Towary (ZC1): dzien N | ..." - sekcja na poprawke; kazda poprawka ma wlasny klucz (Settings, grupa "Goods paths (ZC1, 16.10)").
    ///
    /// WYTROPIONE ZRODLA I UJSCIA "BEZ NAZWY" (K4 "Towary (inne ticki dobowe)" pokazywal "(nieznany)", bo wolajace metody sa latane - ramka DMD nie miala typu):
    ///  - "+17 kazdego jedzenia w miastach" (u Jeffa +34 = dwa konwoje na dobe): DTE CutTheirSupplyBehavior.OnDailyTickTown -> SpawnReinforcementCaravan ->
    ///    AddReinforcementSupplies: KAZDY przedmiot IsFood x 17 z niczego do nowego konwoju zaopatrzenia (plus 17 sumpter_horse - PackSources). Konwoj to karawana
    ///    (CaravanPartyComponent) - nie je w drodze (DoesPartyConsumeFood = false dla karawan; lancuch FoodChain). U celu TransferCargoToTargetStash kopiuje do
    ///    zbrojowni DTE tylko sprzet, a CleanupCaravan -> MobileParty.RemoveParty kasuje roster bez zdarzenia zniszczenia - "-17 w RemoveParty" to to samo jedzenie.
    ///    Regula (DteConvoyNoFreeFood): postfiks AddReinforcementSupplies zdejmuje z konwoju jedzenie, ktore DTE wlasnie dopisalo, gdy konwoj nie je (inaczej
    ///    zostaje - licznik "konwoj je"). Zrodlo i ujscie znikaja razem; lupu z rozbitego konwoju juz nie ma (dotad jedzenie z niczego dla zwyciezcy).
    ///  - "osady: BannerKings:BKPartyBehavior" (zboze, owoce, jaja, mieso wieloryba): DailySettlementTick -> SendSlaveCaravan -> PopulationPartyComponent.CreateSlaveCaravan
    ///    -> BannerKingsComponent.GiveFood (kazde jedzenie: ludzie x 16 / wartosc x U^4 z niczego); u celu AddPopulationPartyBehavior -> DestroyPartyAction (jedzenie
    ///    przepada). Partie BK nie jedza (BKPartyConsumptionModel). Wedrowcow zamknal juz 36b (TravellerTrade36b). Regula (BkCaravanNoFreeFood): w czasie
    ///    CreateSlaveCaravan / CreateFoodCaravan prefiks GiveFood pomija dar BK, gdy partia nie je.
    ///  - "mnoznik BK rzemieslnikow +X (z Y)" w linii "Towary:" (audyt: ok. 1 460 szt./d "z niczego") - przy ArtisanOwnInputs dodatkowe cykle biora wlasny wsad
    ///    (linia 147), a ksiega porownywala wyrob calej ramki cyklu z receptura JEDNEGO cyklu. Ksiega liczy teraz liczbe cykli z wsadu zjedzonego w ramce (GoodsLedger).
    ///  - ryby "(nieznany)" w tickach osad (ok. 70-130/d) - zrodla nie znaleziono w dekompilacji (NavalDLC, BK, RBL, BEE, gra); K4 rozpoznaje teraz metody latane
    ///    (Harmony.GetOriginalMethodFromStackframe), wiec nastepny bieg poda autora.
    ///
    /// POPRAWKI (kazda osobno):
    ///  1. WhaleMeatFromWhalersOnly: wyrob kategorii "meat" z cyklu warsztatu losowany jako WhaleMeat (BKItems, kategoria meat) -> "meat" (WorkshopTrade.OutPrefix,
    ///     przed BK); wies wielorybnikow (typ whaler: ryby 5 + tran) oddaje WhaleShare (ok. 1/3) ryb z ticku produkcji jako mieso wieloryba (te same sztuki 10 kg).
    ///  2. BkRecipeYieldByWorkshop: ArtisanInputs - linia rzemieslnikow zboze -> piwo daje 2 sztuki na cykl (warzelnia gry 1 -> 2); chleb 1:1 bez zmian.
    ///  3. SilkToWeavers / SilverOreToSmiths / PlanksToBuilding: budzet wiersza konsumpcji mieszczan (HistoricalPrices.BudgetPostfix) dla jedwabiu, rudy srebra
    ///     i desek = 0; lista dostatku (TownFolk.LadderOffer) pomija jedwab i rude srebra (ich czesc grupy idzie na reszte grupy); budowy kupuja deski pierwsze.
    ///  4. CharcoalBurners: wies drwali po ticku produkcji wypala 1 ladunek drewna z wlasnego magazynu na wegiel (6.5 kg drewna na 1 kg wegla), gdy w miescie
    ///     targowym lezy mniej niz CoalTownFloor bryl, a wegiel placi tam wiecej niz drewno; jedna wies na miasto na dobe (ramka ksiegi ZC1, ksiega drewna "wegiel").
    ///  5. IronIngotsOffShelf: raz na dobe sztaby (Iron1-6) z polek miast i zamkow -> zelazo (lupa / ruda) tej samej osady wedlug wartosci (zaokraglenie losowe).
    ///  6. BkSideGoodsOrigin: przejscie SideDays (60) dob od pierwszej doby z kluczem (zapis): marmur kopaln srebra x (1 - t) (GoodsRates.SideStep, ostatni krok
    ///     modelu), to, co zdjete wczoraj, rozdane po rowno glinankom (clay_mine - wapien BK) dzis; miod pitny drwali x (1 - t), zamiast niego miod lesny
    ///     (HoneyPerMead sztuki miodu za sztuke miodu pitnego). Papirus - juz T8 (MineralOnce.CropFilter, gorace poludnie); zloto Zachodu - juz 24d (RockGold).
    ///  7. CartPriceByDestination: MarketCarts - czesc ladunku do dziennego koszyka wsi targu (TownFolk.VillageWant) po cenie z chwili wjazdu, reszta jak dotad.
    ///  8. BulkClayLimeCharcoal (tylko kampania zaczeta z kluczem): glina i wapien w ladunkach (HistoricalPrices.Apply - waga x czynnik ladunku kampanii),
    ///     zapas startowy przeliczony (StartStock), wsad gliny i wapna w liniach warsztatow wedlug kg (ConsumeInputFromTownMarket: liczba / czynnik, losowo;
    ///     DetermineItemRosterHasSufficientInputs: koszt wsadu tak samo). Wegiel zostaje w brylach (kuznia liczy go bryla).
    /// Zapis arm_zc1: "1|start przejscia BK|ladunki gliny (0 stary zapis, 1 tak, 2 nie)|dzien decyzji|marmur do rozdania". Brak pola = stary zapis (glina w workach,
    /// przejscie od pierwszej doby z kluczem). Potkniecia per obiekt (licznik), bez globalnych wylacznikow.
    /// </summary>
    internal static class GoodsPaths
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal const float WhaleShare = 0.35f;       // czesc polowu wielorybnikow wyladowana jako mieso wieloryba (reszta ryby) - wieloryb to mieso i tran, polow dorszy - ryby
        internal const float HoneyPerMead = 0.5f;      // miod pitny to miod i woda (ok. 1 : 2) - za sztuke miodu pitnego (10 l) pol sztuki miodu (5 kg)
        internal const int SideDays = 60;              // zasada Jeffa: zmiana zajecia wsi nie od razu - ok. 60 dob przejscia
        internal const float WoodKgPerCoalKg = 6.5f;   // retorta / mielerz: 15% masy drewna (historia 15-20%)
        internal const int CoalTownFloor = 8;          // miasto trzyma kilka bryl wegla dla kowali i gracza - ponizej wypalaja go drwale

        private static Settings S { get { return Settings.Current; } }
        internal static bool WhaleOn { get { var s = S; return s != null && s.WhaleMeatFromWhalersOnly; } }
        internal static bool BeerOn { get { var s = S; return s != null && s.BkRecipeYieldByWorkshop; } }
        internal static bool SilkOn { get { var s = S; return s != null && s.SilkToWeavers; } }
        internal static bool SilverOn { get { var s = S; return s != null && s.SilverOreToSmiths; } }
        internal static bool PlanksOn { get { var s = S; return s != null && s.PlanksToBuilding; } }
        internal static bool CoalOn { get { var s = S; return s != null && s.CharcoalBurners; } }
        internal static bool IngotsOn { get { var s = S; return s != null && s.IronIngotsOffShelf; } }
        internal static bool SideOn { get { var s = S; return s != null && s.BkSideGoodsOrigin; } }
        internal static bool CartOn { get { var s = S; return s != null && s.CartPriceByDestination; } }
        internal static bool DteOn { get { var s = S; return s != null && s.DteConvoyNoFreeFood; } }
        internal static bool BkFoodOn { get { var s = S; return s != null && s.BkCaravanNoFreeFood; } }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }

        // ------------------------------------------------------------ stan kampanii (zapis arm_zc1)
        private static int _sideStart = -1;           // dzien startu przejscia BkSideGoodsOrigin (-1 = nie zaczete; klucz wylaczony zeruje)
        private static int _bulk;                     // 0 = stary zapis / nieustalone, 1 = kampania zaczeta z BulkClayLimeCharcoal, 2 = zaczeta bez
        private static int _bulkDay = -1;
        private static bool _newGame;
        private static double _marbleBudget, _marbleToday;   // marmur zdjety kopalniom srebra wczoraj (do rozdania dzis) / dzis (szt. modelu)
        private static int _clayMines;

        // ------------------------------------------------------------ przedmioty (raz na sesje)
        private static ItemObject _whale, _meat, _fish, _honey, _marble, _coal, _wood, _iron, _clay, _lime;
        private static ItemCategory _clayCat, _limeCat;
        private static ItemObject[] _ingots;
        private static bool _looked;

        // ------------------------------------------------------------ liczniki doby
        private static long _dWhaleSwap, _dWhaleFish, _dWhaleMade, _dHoney, _dMarbleGiven, _dCoal, _dCoalWood, _dCoalFull, _dCoalNoWood, _dCoalNoProfit, _dCoalNoTown;
        private static double _dMeadCut, _dMarbleCut;
        private static long _dIngotPcs, _dIngotValue, _dIngotIron;
        private static int _dIngotTowns;
        private static long _dDteConv, _dDteFood, _dDteEats, _dBkSkip, _dBkEats;
        private static long _dBulkIn, _dBulkTaken, _dBulkCostCut;
        private static long _dBeerExtra, _dBeerUnsold;
        private static int _stumbles, _stumblesDay;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static readonly HashSet<Settlement> _coalTowns = new HashSet<Settlement>();
        private static string _hooks = "nie wpiete";
        private static long _ticks;

        internal static void Reset()
        {
            _sideStart = -1; _bulk = 0; _bulkDay = -1; _newGame = false; _marbleBudget = 0; _marbleToday = 0; _clayMines = 0;
            _looked = false; _whale = _meat = _fish = _honey = _marble = _coal = _wood = _iron = _clay = _lime = null; _clayCat = _limeCat = null; _ingots = null;
            _stumbles = 0; _errSites.Clear();
            DayReset();
        }

        private static void DayReset()
        {
            _dWhaleSwap = _dWhaleFish = _dWhaleMade = _dHoney = _dMarbleGiven = _dCoal = _dCoalWood = _dCoalFull = _dCoalNoWood = _dCoalNoProfit = _dCoalNoTown = 0;
            _dMeadCut = _dMarbleCut = 0;
            _dIngotPcs = _dIngotValue = _dIngotIron = 0; _dIngotTowns = 0;
            _dDteConv = _dDteFood = _dDteEats = _dBkSkip = _dBkEats = 0;
            _dBulkIn = _dBulkTaken = _dBulkCostCut = 0;
            _dBeerExtra = _dBeerUnsold = 0;
            _stumblesDay = 0; _ticks = 0;
            _coalTowns.Clear();
        }

        internal static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            if (_errSites.Add(where)) { try { Log.Error("GoodsPaths." + where, e); } catch { } }
        }

        internal static void OnNewGame() { _newGame = true; }

        internal static string Export()
        {
            return "1|" + _sideStart.ToString(Inv) + "|" + _bulk.ToString(Inv) + "|" + _bulkDay.ToString(Inv) + "|" + _marbleBudget.ToString("R", Inv);
        }

        /// <summary>Wczytanie: brak pola, inny format, blad - stary zapis (glina i wapien w workach, przejscie BK od pierwszej doby z kluczem).</summary>
        internal static void Import(string v)
        {
            _sideStart = -1; _bulk = 0; _bulkDay = -1; _marbleBudget = 0; _marbleToday = 0;
            if (string.IsNullOrEmpty(v)) return;
            try
            {
                var p = v.Split('|');
                if (p.Length < 5 || p[0] != "1") return;
                _sideStart = int.Parse(p[1], Inv);
                int b = int.Parse(p[2], Inv);
                _bulk = b >= 0 && b <= 2 ? b : 0;
                _bulkDay = int.Parse(p[3], Inv);
                double m;
                if (double.TryParse(p[4], NumberStyles.Float, Inv, out m) && !double.IsNaN(m) && !double.IsInfinity(m) && m > 0 && m < 1e6) _marbleBudget = m;
            }
            catch (Exception e) { _sideStart = -1; _bulk = 0; _bulkDay = -1; _marbleBudget = 0; Stumble("Import", e); }
        }

        private static void Look()
        {
            if (_looked) return;
            var om = MBObjectManager.Instance;
            if (om == null) return;
            _looked = true;
            _whale = om.GetObject<ItemObject>("WhaleMeat"); _meat = om.GetObject<ItemObject>("meat"); _fish = om.GetObject<ItemObject>("fish");
            _honey = om.GetObject<ItemObject>("honey"); _marble = om.GetObject<ItemObject>("marble"); _coal = om.GetObject<ItemObject>("charcoal");
            _wood = om.GetObject<ItemObject>("hardwood"); _iron = om.GetObject<ItemObject>("iron");
            _clay = om.GetObject<ItemObject>("clay"); _lime = om.GetObject<ItemObject>("limestone");
            _clayCat = _clay != null ? _clay.ItemCategory : null; _limeCat = _lime != null ? _lime.ItemCategory : null;
            var ing = new List<ItemObject>();
            foreach (var g in new[] { CraftingMaterials.Iron1, CraftingMaterials.Iron2, CraftingMaterials.Iron3, CraftingMaterials.Iron4, CraftingMaterials.Iron5, CraftingMaterials.Iron6 })
            {
                var it = Recipes.MaterialItem(g);
                if (it != null) ing.Add(it);
            }
            _ingots = ing.ToArray();
        }

        // ============================================================ 1. mieso wieloryba tylko od wielorybnikow
        /// <summary>WorkshopTrade.OutPrefix (ProduceAnOutputToTown, Priority.First - przed rachunkiem rzemieslnikow 147 i prefiksem BK): wyrob WhaleMeat z cyklu
        /// warsztatu (uboj w miescie) -> meat. Modyfikator jakosci nie przechodzi (sztuki BK i gry dla towarow ida bez modyfikatora). Nie rzuca.</summary>
        internal static void WhaleOutput(ref EquipmentElement e)
        {
            try
            {
                if (e.Item == null || !WhaleOn) return;
                Look();
                if (_whale == null || _meat == null || !ReferenceEquals(e.Item, _whale)) return;
                e = new EquipmentElement(_meat);
                _dWhaleSwap++;
            }
            catch (Exception ex) { Stumble("WhaleOutput", ex); }
        }

        // ============================================================ tick produkcji wsi (VillageGoodProductionCampaignBehavior.TickGoodProduction)
        [ThreadStatic] private static Village _tv;
        [ThreadStatic] private static int _tvFish0;
        [ThreadStatic] private static double _tvCutMead, _tvCutMarble;

        /// <summary>GoodsRates.SideStep (ostatni krok modelu produkcji wsi): ile modelu zdjelo przejscie BK w ticku TEJ wsi (wolane tylko w watku ticku).</summary>
        internal static void NoteCut(Village v, bool marble, float cut)
        {
            if (v == null || !(cut > 0f) || !ReferenceEquals(v, _tv)) return;
            if (marble) _tvCutMarble += cut; else _tvCutMead += cut;
        }

        /// <summary>Przejscie BkSideGoodsOrigin 0..1 (dowolny watek; 0 przy wylaczonym kluczu i przed pierwsza doba z kluczem).</summary>
        internal static float SideT
        {
            get
            {
                if (!SideOn || _sideStart < 0) return 0f;
                int d;
                try { d = Today() - _sideStart; } catch { return 0f; }
                return d <= 0 ? 0f : Math.Min(1f, d / (float)SideDays);
            }
        }

        public static void TickPre(Village __0, bool __1)
        {
            _tv = null; _tvFish0 = -1; _tvCutMead = 0; _tvCutMarble = 0;
            try
            {
                if (__0 == null || __1) return;
                var c = Campaign.Current;
                if (c == null || !c.GameStarted) return;
                _tv = __0;
                var t = __0.VillageType;
                if (t != null && t.StringId == "whaler" && WhaleOn)
                {
                    Look();
                    var r = __0.Owner != null ? __0.Owner.ItemRoster : null;
                    if (r != null && _fish != null) _tvFish0 = r.GetItemNumber(_fish);
                }
            }
            catch (Exception e) { _tv = null; Stumble("TickPre", e); }
        }

        public static void TickPost(Village __0, bool __1)
        {
            var v = _tv;
            _tv = null;
            if (v == null || !ReferenceEquals(v, __0)) return;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var r = v.Owner != null ? v.Owner.ItemRoster : null;
                if (r == null) return;
                Look();
                string type = v.VillageType != null ? v.VillageType.StringId : "";
                if (type == "whaler") Whale(v, r);
                else if (type == "lumberjack") { Honey(v, r); Coal(v, r); }
                else if (type == "clay_mine") Marble(v, r);
                if (_tvCutMarble > 0) { _marbleToday += _tvCutMarble; _dMarbleCut += _tvCutMarble; }
            }
            catch (Exception e) { Stumble("TickPost", e); }
            finally { _tvCutMead = 0; _tvCutMarble = 0; _tvFish0 = -1; _ticks += Stopwatch.GetTimestamp() - t0; }
        }

        public static void TickFin() { _tv = null; }

        private static void Whale(Village v, ItemRoster r)
        {
            if (!WhaleOn || _tvFish0 < 0 || _fish == null || _whale == null) return;
            int added = r.GetItemNumber(_fish) - _tvFish0;
            if (added <= 0) return;
            int k = MBRandom.RoundRandomized(added * WhaleShare);
            if (k > added) k = added;
            _dWhaleFish += added;
            if (k <= 0) return;
            var f = Fz(v.Settlement, "mieso wieloryba z polowu (wielorybnicy)");
            try { r.AddToCounts(_fish, -k); r.AddToCounts(_whale, k); }
            finally { GoodsLedger.End(f); }
            _dWhaleMade += k;
        }

        /// <summary>Wlasna ramka ksiegi towarow (pozycja "drogi towarow (ZC1)" z opisem) - w ramce ticku wsi; null, gdy ksiega wylaczona.</summary>
        private static GoodsLedger.Frame Fz(Settlement st, string label)
        {
            var f = GoodsLedger.Begin(GoodsLedger.FZc1, st);
            if (f != null) f.Info = label;
            return f;
        }

        private static void Honey(Village v, ItemRoster r)
        {
            if (!(_tvCutMead > 0) || _honey == null) return;
            _dMeadCut += _tvCutMead;
            int n = MBRandom.RoundRandomized((float)(_tvCutMead * HoneyPerMead));
            if (n <= 0) return;
            var f = Fz(v.Settlement, "miod lesny zamiast miodu pitnego (drwale)");
            try { r.AddToCounts(_honey, n); }
            finally { GoodsLedger.End(f); }
            _dHoney += n;
            try { CampaignEventDispatcher.Instance.OnItemProduced(_honey, v.Settlement, n); } catch (Exception e) { Stumble("Honey(zdarzenie)", e); }
        }

        private static void Marble(Village v, ItemRoster r)
        {
            if (!SideOn || !(_marbleBudget > 0) || _clayMines <= 0 || _marble == null) return;
            int n = MBRandom.RoundRandomized((float)(_marbleBudget / _clayMines));
            if (n <= 0) return;
            var f = Fz(v.Settlement, "marmur z kamieniolomow (glinanki)");
            try { r.AddToCounts(_marble, n); }
            finally { GoodsLedger.End(f); }
            _dMarbleGiven += n;
            try { CampaignEventDispatcher.Instance.OnItemProduced(_marble, v.Settlement, n); } catch (Exception e) { Stumble("Marble(zdarzenie)", e); }
        }

        private static void Coal(Village v, ItemRoster r)
        {
            if (!CoalOn || _coal == null || _wood == null) return;
            var mk = v.TradeBound ?? v.Bound;
            if (mk == null || mk.Town == null || mk.ItemRoster == null) { _dCoalNoTown++; return; }
            if (_coalTowns.Contains(mk)) return;
            if (mk.ItemRoster.GetItemNumber(_coal) >= CoalTownFloor) { _dCoalFull++; return; }
            if (r.GetItemNumber(_wood) < 1) { _dCoalNoWood++; return; }
            float units = Math.Max(0.1f, _wood.Weight) / WoodKgPerCoalKg / Math.Max(0.1f, _coal.Weight);
            int pc = mk.Town.GetItemPrice(_coal, null, true), pw = mk.Town.GetItemPrice(_wood, null, true);
            if (units * pc <= pw) { _dCoalNoProfit++; return; }
            int n = MBRandom.RoundRandomized(units);
            if (n <= 0) return;
            var f = Fz(v.Settlement, "wegiel z drewna (drwale)");   // wlasna ramka: wsie dalej licza w ksiedze to, co wydobyly (kontrola z ksiega drewna)
            try { r.AddToCounts(_wood, -1); r.AddToCounts(_coal, n); }
            finally { GoodsLedger.End(f); }
            OreLedger.NoteCoalWood(_wood, 1);
            _coalTowns.Add(mk);
            _dCoalWood++; _dCoal += n;
        }

        // ============================================================ 5. sztaby z polek -> zelazo
        private static void Ingots()
        {
            if (!IngotsOn) return;
            Look();
            if (_iron == null || _ingots == null || _ingots.Length == 0) return;
            foreach (var t in Town.AllFiefs)
            {
                try
                {
                    var r = t != null && t.Owner != null ? t.Owner.ItemRoster : null;
                    if (r == null) continue;
                    bool any = false;
                    for (int i = 0; i < r.Count && !any; i++) { var el = r.GetElementCopyAtIndex(i); any = el.Amount > 0 && IsIngot(el.EquipmentElement.Item); }
                    if (!any) continue;
                    long val = 0, pcs = 0; int n = 0;
                    var f = Fz(t.Settlement, "sztaby z polek -> zelazo");
                    try
                    {
                        for (int i = r.Count - 1; i >= 0; i--)
                        {
                            var el = r.GetElementCopyAtIndex(i);
                            if (el.Amount <= 0 || !IsIngot(el.EquipmentElement.Item)) continue;
                            val += (long)Math.Max(0, el.EquipmentElement.Item.Value) * el.Amount;
                            pcs += el.Amount;
                            r.AddToCounts(el.EquipmentElement, -el.Amount);
                        }
                        n = MBRandom.RoundRandomized((float)((double)val / Math.Max(1, _iron.Value)));
                        if (n > 0) r.AddToCounts(_iron, n);
                    }
                    finally { GoodsLedger.End(f); }
                    if (n > 0) OreLedger.NoteIngotIron(_iron, n);
                    _dIngotTowns++; _dIngotPcs += pcs; _dIngotValue += val; _dIngotIron += n;
                }
                catch (Exception e) { Stumble("Ingots(osada)", e); }
            }
        }

        private static bool IsIngot(ItemObject it)
        {
            if (it == null || _ingots == null) return false;
            for (int i = 0; i < _ingots.Length; i++) if (ReferenceEquals(_ingots[i], it)) return true;
            return false;
        }

        // ============================================================ 3. budzet mieszczan i lista dostatku
        /// <summary>HistoricalPrices.BudgetPostfix (budzet wiersza konsumpcji BK): true = mieszczanie nie kupuja tej kategorii (jedwab, ruda srebra, deski - klucze).</summary>
        internal static bool NoTownBudget(ItemCategory c)
        {
            if (c == null || c.StringId == null) return false;
            string id = c.StringId;
            return (id == "cotton" && SilkOn) || (id == "silver" && SilverOn) || (id == "planks" && PlanksOn);
        }

        /// <summary>TownFolk.LadderOffer: kategoria wypada z listy dostatku (jej czesc grupy idzie na reszte grupy) - jedwab i ruda srebra przy kluczach.</summary>
        internal static bool NoFolkLuxury(ItemCategory c)
        {
            if (c == null || c.StringId == null) return false;
            string id = c.StringId;
            return (id == "cotton" && SilkOn) || (id == "silver" && SilverOn);
        }

        // ============================================================ 2. piwo rzemieslnikow (ArtisanInputs - liczniki)
        internal static void NoteBeer(int extra, int unsold) { _dBeerExtra += extra; _dBeerUnsold += unsold; }

        // ============================================================ konwoje DTE i karawany BK (jedzenie z niczego)
        public static void DteSupPre(MobileParty __0, out Dictionary<ItemObject, int> __state)
        {
            __state = null;
            try
            {
                if (!DteOn || __0 == null || __0.ItemRoster == null) return;
                var r = __0.ItemRoster;
                __state = new Dictionary<ItemObject, int>();
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (it == null || !it.IsFood || el.Amount <= 0) continue;
                    int n; __state.TryGetValue(it, out n); __state[it] = n + el.Amount;
                }
            }
            catch (Exception e) { __state = null; Stumble("DteSupPre", e); }
        }

        public static void DteSupPost(MobileParty __0, Dictionary<ItemObject, int> __state)
        {
            if (__state == null || __0 == null) return;
            try
            {
                _dDteConv++;
                var m = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.MobilePartyFoodConsumptionModel : null;
                if (m == null || m.DoesPartyConsumeFood(__0)) { _dDteEats++; return; }   // konwoj, ktory je - zapas drogi DTE zostaje
                var r = __0.ItemRoster;
                var take = new List<KeyValuePair<ItemObject, int>>();
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (it == null || !it.IsFood || el.Amount <= 0) continue;
                    take.Add(new KeyValuePair<ItemObject, int>(it, el.Amount));
                }
                var now = new Dictionary<ItemObject, int>();
                foreach (var kv in take) { int n; now.TryGetValue(kv.Key, out n); now[kv.Key] = n + kv.Value; }
                foreach (var kv in now)
                {
                    int was; __state.TryGetValue(kv.Key, out was);
                    int d = kv.Value - was;
                    if (d <= 0) continue;
                    r.AddToCounts(kv.Key, -d);
                    _dDteFood += d;
                }
            }
            catch (Exception e) { Stumble("DteSupPost", e); }
        }

        [ThreadStatic] private static int _bkCaravan;
        public static void BkCaravanPre() { _bkCaravan++; }
        public static void BkCaravanFin() { if (_bkCaravan > 0) _bkCaravan--; }

        /// <summary>Prefiks BannerKingsComponent.GiveFood(ref MobileParty): w czasie CreateSlaveCaravan / CreateFoodCaravan (licznik zagniezdzenia) dar BK pominiety,
        /// gdy partia nie je. Inne wolania GiveFood (milicje, majatki, bandy BK) bez zmian.</summary>
        public static bool BkGiveFoodPre(ref MobileParty __0)
        {
            try
            {
                if (_bkCaravan <= 0 || !BkFoodOn || __0 == null) return true;
                var m = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.MobilePartyFoodConsumptionModel : null;
                if (m == null || m.DoesPartyConsumeFood(__0)) { _dBkEats++; return true; }
                _dBkSkip++;
                return false;
            }
            catch (Exception e) { Stumble("BkGiveFoodPre", e); return true; }
        }

        // ============================================================ 8. glina i wapien w ladunkach (nowa kampania)
        /// <summary>HistoricalPrices.Apply (krok 0): ladunki gliny i wapna - tylko kampania zaczeta z kluczem; sesja, ktora zalozyla kampanie, ustala stan raz.</summary>
        internal static bool BulkClayActive()
        {
            try
            {
                if (_newGame && _bulk == 0) { var s = S; _bulk = s != null && s.BulkClayLimeCharcoal ? 1 : 2; _bulkDay = Today(); }
            }
            catch (Exception e) { Stumble("BulkClayActive", e); }
            return _bulk == 1;
        }

        internal static bool BulkClay { get { return _bulk == 1; } }

        private static float BulkScaleOf(ItemCategory c)
        {
            if (_bulk != 1 || c == null) return 1f;
            Look();
            if (c == _clayCat && _clay != null) return HistoricalPrices.CountScale(_clay);
            if (c == _limeCat && _lime != null) return HistoricalPrices.CountScale(_lime);
            return 1f;
        }

        /// <summary>Prefiks ConsumeInputFromTownMarket (Priority.First - przed BK i rachunkiem cyklu): wsad gliny / wapna linii warsztatu = liczba z receptury / czynnik
        /// ladunku (losowo - te same kg co w workach; 0 sztuk = cykl bez zdjecia ladunku).</summary>
        public static void BulkInPre(ItemCategory __0, ref int __1)
        {
            try
            {
                if (_bulk != 1 || __0 == null || __1 <= 0) return;
                float sc = BulkScaleOf(__0);
                if (sc <= 1.01f) return;
                int n = MBRandom.RoundRandomized(__1 / sc);
                _dBulkIn += __1; _dBulkTaken += n;
                __1 = Math.Max(0, n);
            }
            catch (Exception e) { Stumble("BulkInPre", e); }
        }

        /// <summary>Postfiks DetermineItemRosterHasSufficientInputs: koszt wsadu gliny / wapna w progu cyklu wedlug kg (cena ladunku x liczba / czynnik).</summary>
        public static void BulkCostPost(WorkshopType.Production __0, Town __2, ref int __3, bool __result)
        {
            try
            {
                if (_bulk != 1 || !__result || __2 == null || __0.Inputs == null || __2.Owner == null) return;
                var r = __2.Owner.ItemRoster;
                foreach (var x in __0.Inputs)
                {
                    float sc = BulkScaleOf(x.Item1);
                    if (sc <= 1.01f || x.Item2 <= 0) continue;
                    ItemObject it = null;
                    for (int i = 0; i < r.Count && it == null; i++) { var o = r.GetItemAtIndex(i); if (o != null && o.ItemCategory == x.Item1) it = o; }
                    if (it == null) continue;
                    int cut = (int)Math.Round(__2.GetItemPrice(it) * x.Item2 * (1.0 - 1.0 / sc));
                    if (cut <= 0) continue;
                    __3 = Math.Max(0, __3 - cut);
                    _dBulkCostCut += cut;
                }
            }
            catch (Exception e) { Stumble("BulkCostPost", e); }
        }

        /// <summary>StartStock: dodatkowe towary w ladunkach w tej kampanii (glina, wapien) - przeliczane razem z ruda i drewnem.</summary>
        internal static string[] BulkIds() { return _bulk == 1 ? new[] { "clay", "limestone" } : new string[0]; }

        // ============================================================ wpiecie
        /// <summary>SubModuleMain, PRZED WorkshopTrade.ApplyAll (latki cyklu warsztatu kompiluja wolane metody - te musza miec latke wczesniej).</summary>
        internal static void ApplyEarly(Harmony h)
        {
            var parts = new List<string>();
            try
            {
                var b = typeof(WorkshopsCampaignBehavior);
                var cons = AccessTools.Method(b, "ConsumeInputFromTownMarket");
                var suf = AccessTools.Method(b, "DetermineItemRosterHasSufficientInputs");
                if (cons != null) { h.Patch(cons, prefix: new HarmonyMethod(typeof(GoodsPaths), nameof(BulkInPre)) { priority = Priority.First }); parts.Add("wsad z targu"); }
                else parts.Add("BRAK ConsumeInputFromTownMarket");
                if (suf != null) { h.Patch(suf, postfix: new HarmonyMethod(typeof(GoodsPaths), nameof(BulkCostPost))); parts.Add("koszt wsadu"); }
                else parts.Add("BRAK DetermineItemRosterHasSufficientInputs");
            }
            catch (Exception e) { parts.Add("blad " + e.GetType().Name); Log.Error("GoodsPaths.ApplyEarly", e); }
            _hooks = "glina i wapien w ladunkach: " + string.Join(", ", parts.ToArray());
        }

        internal static void ApplyAll(Harmony h)
        {
            var parts = new List<string>();
            try
            {
                var tick = AccessTools.Method(typeof(VillageGoodProductionCampaignBehavior), "TickGoodProduction", new[] { typeof(Village), typeof(bool) });
                if (tick != null)
                {
                    // prefiks Priority.First: BK zastepuje TickGoodProduction prefiksem z wynikiem false, a Harmony 2.4.2 pomija po nim prefiksy z parametrem typu referencyjnego
                    h.Patch(tick, prefix: new HarmonyMethod(typeof(GoodsPaths), nameof(TickPre)) { priority = Priority.First }, postfix: new HarmonyMethod(typeof(GoodsPaths), nameof(TickPost)),
                            finalizer: new HarmonyMethod(typeof(GoodsPaths), nameof(TickFin)));
                    parts.Add("tick produkcji wsi");
                }
                else parts.Add("BRAK TickGoodProduction (wieloryby, miod, marmur, wegiel nieczynne)");
            }
            catch (Exception e) { parts.Add("tick: blad " + e.GetType().Name); Log.Error("GoodsPaths.ApplyAll(tick)", e); }
            try
            {
                var t = AccessTools.TypeByName("DynamicTroopEquipmentReupload.CutTheirSupplyBehavior");
                var sup = t != null ? AccessTools.Method(t, "AddReinforcementSupplies", new[] { typeof(MobileParty) }) : null;
                if (sup != null) { h.Patch(sup, prefix: new HarmonyMethod(typeof(GoodsPaths), nameof(DteSupPre)) { priority = Priority.First }, postfix: new HarmonyMethod(typeof(GoodsPaths), nameof(DteSupPost))); parts.Add("konwoje DTE"); }
                else parts.Add(t == null ? "bez DTE" : "BRAK DTE AddReinforcementSupplies");
            }
            catch (Exception e) { parts.Add("DTE: blad " + e.GetType().Name); Log.Error("GoodsPaths.ApplyAll(DTE)", e); }
            try
            {
                var ppc = AccessTools.TypeByName("BannerKings.Components.PopulationPartyComponent");
                var bkc = AccessTools.TypeByName("BannerKings.Components.BannerKingsComponent");
                var give = bkc != null ? AccessTools.Method(bkc, "GiveFood") : null;
                int n = 0;
                if (ppc != null && give != null)
                {
                    foreach (var name in new[] { "CreateSlaveCaravan", "CreateFoodCaravan" })
                    {
                        var m = AccessTools.Method(ppc, name);
                        if (m == null) continue;
                        h.Patch(m, prefix: new HarmonyMethod(typeof(GoodsPaths), nameof(BkCaravanPre)) { priority = Priority.First }, finalizer: new HarmonyMethod(typeof(GoodsPaths), nameof(BkCaravanFin)));
                        n++;
                    }
                    if (n > 0) h.Patch(give, prefix: new HarmonyMethod(typeof(GoodsPaths), nameof(BkGiveFoodPre)));
                }
                parts.Add(n > 0 ? "karawany BK (" + n + "/2)" : (ppc == null ? "bez BK" : "BRAK BK CreateSlaveCaravan / GiveFood"));
            }
            catch (Exception e) { parts.Add("BK: blad " + e.GetType().Name); Log.Error("GoodsPaths.ApplyAll(BK)", e); }
            _hooks = _hooks + "; " + string.Join(", ", parts.ToArray());
            Log.Info("Towary (ZC1): latki przy starcie gry - " + _hooks + ".");
        }

        // ============================================================ doba (ArmouryBehavior.OnDailyTick, watek glowny)
        internal static void Daily()
        {
            long t0 = Stopwatch.GetTimestamp();
            var s = S;
            if (s == null) return;
            int day = Today() - 1;
            try
            {
                // przejscie BK: start przy pierwszej dobie z kluczem; wylaczony - od nowa przy ponownym wlaczeniu
                if (SideOn) { if (_sideStart < 0) _sideStart = Today(); }
                else _sideStart = -1;
                // budzet marmuru na jutro i liczba glinanek
                _marbleBudget = SideOn ? _marbleToday : 0; _marbleToday = 0;
                int clay = 0;
                foreach (var v in Village.All) if (v != null && v.VillageType != null && v.VillageType.StringId == "clay_mine" && !v.IsDeserted) clay++;
                _clayMines = clay;
                Ingots();
            }
            catch (Exception e) { Stumble("Daily(przebieg)", e); }
            try
            {
                Look();
                // indeks aksamitu w miastach (prog projektu: < 10 w >= 50% miast) i mieso wieloryba w sklepach i na polkach
                int towns = 0, velvetLow = 0, velvetHave = 0;
                ItemObject velvet = MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObject<ItemObject>("velvet") : null;
                double velvetIdx = 0;
                if (velvet != null && velvet.ItemCategory != null)
                    foreach (var t in Town.AllTowns)
                    {
                        if (t == null || !t.IsTown || t.Owner == null) continue;
                        towns++;
                        float ix = t.GetItemCategoryPriceIndex(velvet.ItemCategory);
                        velvetIdx += ix;
                        if (ix < 9.99f) velvetLow++;
                        if (t.Owner.ItemRoster != null && t.Owner.ItemRoster.GetItemNumber(velvet) > 0) velvetHave++;
                    }
                long carts, cartFirst, cartFair, cartOld, ironN, ironFirst, ironFair, ironOld;
                MarketCarts.TakeZc1(out carts, out cartFirst, out cartFair, out cartOld, out ironN, out ironFirst, out ironFair, out ironOld);
                var sb = new StringBuilder(1600);
                double f = 1000.0 / Stopwatch.Frequency;
                sb.Append("Towary (ZC1): dzien ").Append(day)
                  .Append(" | wieloryb (Whale Meat From Whalers Only ").Append(TN(s.WhaleMeatFromWhalersOnly)).Append("): wyrob warsztatow WhaleMeat -> meat ").Append(_dWhaleSwap)
                  .Append(" szt., wielorybnicy: ryby z ticku ").Append(_dWhaleFish).Append(", z tego mieso wieloryba ").Append(_dWhaleMade)
                  .Append(" | piwo rzemieslnikow x2 (Bk Recipe Yield By Workshop ").Append(TN(s.BkRecipeYieldByWorkshop)).Append("): drugie piwo z cyklu ").Append(_dBeerExtra)
                  .Append(", niesprzedane (kasa bez nadwyzki) ").Append(_dBeerUnsold)
                  .Append(" | mieszczanie nie kupuja: jedwabiu ").Append(TN(s.SilkToWeavers)).Append(", rudy srebra ").Append(TN(s.SilverOreToSmiths)).Append(", desek ").Append(TN(s.PlanksToBuilding))
                  .Append(" (budowy biora deski pierwsze)")
                  .Append(" | aksamit: miast ").Append(towns).Append(", z indeksem < 10 ").Append(velvetLow).Append(" (").Append(towns > 0 ? (100.0 * velvetLow / towns).ToString("0", Inv) : "-")
                  .Append("%), z aksamitem na polce ").Append(velvetHave).Append(", indeks srednio ").Append(towns > 0 ? (velvetIdx / towns).ToString("0.00", Inv) : "-")
                  .Append(" | wegiel (Charcoal Burners ").Append(TN(s.CharcoalBurners)).Append("): drwale wypalili ").Append(_dCoalWood).Append(" ladunkow drewna na ").Append(_dCoal)
                  .Append(" bryl; nie: miasto ma >= ").Append(CoalTownFloor).Append(" ").Append(_dCoalFull).Append(", bez drewna w magazynie ").Append(_dCoalNoWood)
                  .Append(", bez zysku ").Append(_dCoalNoProfit).Append(", bez miasta ").Append(_dCoalNoTown)
                  .Append(" | sztaby z polek (Iron Ingots Off Shelf ").Append(TN(s.IronIngotsOffShelf)).Append("): osad ").Append(_dIngotTowns).Append(", sztab ").Append(_dIngotPcs)
                  .Append(" warte ").Append(_dIngotValue).Append(" zl -> zelaza ").Append(_dIngotIron)
                  .Append(" | pochodzenie BK (Bk Side Goods Origin ").Append(TN(s.BkSideGoodsOrigin)).Append("): przejscie ").Append(SideT.ToString("0.00", Inv))
                  .Append(" (start ").Append(_sideStart).Append(", ").Append(SideDays).Append(" dob); marmur zdjety kopalniom srebra ").Append(_dMarbleCut.ToString("0.0", Inv))
                  .Append(", glinanki dostaly ").Append(_dMarbleGiven).Append(" (budzet na jutro ").Append(_marbleBudget.ToString("0.0", Inv)).Append(" na ").Append(_clayMines)
                  .Append(" glinanek); miod pitny zdjety drwalom ").Append(_dMeadCut.ToString("0.0", Inv)).Append(", miod lesny ").Append(_dHoney)
                  .Append(" | woz za cena celu (Cart Price By Destination ").Append(TN(s.CartPriceByDestination)).Append("): sol ").Append(carts).Append(" szt. - woz dostal ")
                  .Append(Per(cartFair, carts)).Append(" d/szt., cena miasta przy wjezdzie ").Append(Per(cartFirst, carts)).Append(" (stosunek ").Append(Ratio(cartFair, cartFirst))
                  .Append("; stara regula ").Append(Ratio(cartOld, cartFirst)).Append("); zelazo ").Append(ironN).Append(" szt. - ").Append(Per(ironFair, ironN)).Append(" / ")
                  .Append(Per(ironFirst, ironN)).Append(" (stosunek ").Append(Ratio(ironFair, ironFirst)).Append("; stara regula ").Append(Ratio(ironOld, ironFirst)).Append(")")
                  .Append(" | jedzenie z niczego: konwoje DTE (Dte Convoy No Free Food ").Append(TN(s.DteConvoyNoFreeFood)).Append(") ").Append(_dDteConv).Append(", zdjete ").Append(_dDteFood)
                  .Append(" szt., konwoj je (zostawione) ").Append(_dDteEats)
                  .Append("; karawany BK (Bk Caravan No Free Food ").Append(TN(s.BkCaravanNoFreeFood)).Append(") bez daru ").Append(_dBkSkip).Append(", partia je (dar zostal) ").Append(_dBkEats)
                  .Append(" | glina i wapien w ladunkach (Bulk Clay Lime Charcoal ").Append(TN(s.BulkClayLimeCharcoal)).Append("): kampania ").Append(BulkText())
                  .Append(", wsad linii ").Append(_dBulkIn).Append(" -> ").Append(_dBulkTaken).Append(" ladunkow, koszt wsadu w progu -").Append(_dBulkCostCut)
                  .Append(" | latki: ").Append(_hooks)
                  .Append(" | potkniecia ").Append(_stumblesDay).Append(" (sesja ").Append(_stumbles).Append("), czas ticku wsi ").Append((_ticks * f).ToString("0.0", Inv))
                  .Append(" ms, linia ").Append(((Stopwatch.GetTimestamp() - t0) * f).ToString("0.0", Inv)).Append(" ms.");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily(linia)", e); }
            DayReset();
        }

        private static string BulkText()
        {
            switch (_bulk)
            {
                case 1: return "zaczeta z kluczem (dzien " + _bulkDay + ") - ladunki 100 kg";
                case 2: return "zaczeta bez klucza (dzien " + _bulkDay + ") - worki 10 kg na stale";
                default: return "sprzed paczki (stary zapis) - worki 10 kg na stale";
            }
        }

        private static string TN(bool b) { return b ? "tak" : "nie"; }
        private static string Per(long a, long n) { return n > 0 ? ((double)a / n).ToString("0.00", Inv) : "-"; }
        private static string Ratio(long a, long b) { return b > 0 ? ((double)a / b).ToString("0.00", Inv) : "-"; }
    }
}
