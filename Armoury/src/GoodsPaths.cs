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
    ///  - "osady: BannerKings:BKPartyBehavior" (zboze, owoce, jaja, mieso wieloryba): DailySettlementTick -> SendSlaveCaravan / SendFoodCaravan -> PopulationPartyComponent
    ///    .CreateSlaveCaravan / CreateFoodCaravan -> BannerKingsComponent.GiveFood (kazde jedzenie: ludzie x 16 / wartosc x U^4 z niczego). U celu AddPopulationPartyBehavior:
    ///    karawana niewolnikow - DestroyPartyAction (jedzenie przepada); karawana ZYWNOSCI (CargoKind.Food) - zrodlo stracilo FoodStocks, a u celu wartosc CALEGO
    ///    jedzenia w karawanie (zboze z zapasu zrodla RAZEM z darem GiveFood) idzie do spichlerza celu (FoodStocks) - dar z niczego dosypywal spichlerze celow.
    ///    GiveFood wolaja tez inne partie BK (MilitiaComponent, EstateComponent, FreeCompanyComponent) - BKPartyConsumptionModel.DoesPartyConsumeFood zwraca false
    ///    dla kazdej podklasy BannerKingsComponent, wiec tego jedzenia tez nikt nie je (lup albo znika z partia). Wedrowcow (CreateTravellerParty) zamknal juz 36b
    ///    (TravellerTrade36b - zywnosc drogi z polki) - tu bez zmian. Regula (BkCaravanNoFreeFood): dar BK zdjety zaraz po GiveFood (prefiks - stan, postfiks - zdjecie
    ///    tego, co GiveFood dopisal), gdy partia nie je; liczniki wedlug rodzaju partii, przy karawanach zywnosci - ile jedzenia nie dojdzie do spichlerzy celow
    ///    (1 sztuka = 1 jednostka zapasu, jak BK) i ile celow karawan zywnosci jest dzis bez zapasu.
    ///  - "mnoznik BK rzemieslnikow +X (z Y)" w linii "Towary:" (audyt: ok. 1 460 szt./d "z niczego") - przy ArtisanOwnInputs dodatkowe cykle biora wlasny wsad
    ///    (linia 147), a ksiega porownywala wyrob calej ramki cyklu z receptura JEDNEGO cyklu. Ksiega liczy teraz liczbe cykli z wsadu zjedzonego w ramce (GoodsLedger).
    ///  - ryby "(nieznany)" w tickach osad (ok. 70-130/d) - zrodla nie znaleziono w dekompilacji (NavalDLC, BK, RBL, BEE, gra); K4 rozpoznaje teraz metody latane
    ///    (Harmony.GetOriginalMethodFromStackframe), konie nie zuzywaja juz sladow K4, a jedzenie ma wlasny limit sladow doby - nastepny bieg poda autora (prog ZC1-14).
    ///    ZRODLO OTWARTE do czasu nazwania autora.
    ///
    /// POPRAWKI (kazda osobno; zmiany produkcji i cen ida przejsciem RampDays = 60 dob od pierwszej doby z kluczem - zasada Jeffa, start w zapisie arm_zc1):
    ///  1. WhaleMeatFromWhalersOnly: wyrob kategorii "meat" z cyklu warsztatu losowany jako WhaleMeat (BKItems, kategoria meat) -> "meat" U ZRODLA - postfiks
    ///     WorkshopsCampaignBehavior.GetItemsToProduce (lista wyrobow cyklu i utarg cyklu cena meat), wiec ProduceAnOutputToTown, ProduceAnOutputToWarehouse (warsztat
    ///     gracza), zbiorcze OnItemProduced gry, pula doplaty 35% (TownFolk.CraftSale) i prog zysku cyklu widza ten sam towar; wies wielorybnikow (typ whaler) oddaje
    ///     WhaleShare x przejscie ryb z ticku produkcji jako mieso wieloryba (te same sztuki 10 kg).
    ///  2. BkRecipeYieldByWorkshop: ArtisanInputs - linia rzemieslnikow zboze -> piwo daje 1 + przejscie sztuk na cykl (warzelnia gry 1 -> 2 po 60 dobach); chleb 1:1.
    ///  3. SilkToWeavers / SilverOreToSmiths: RZEMIOSLO PIERWSZE - mieszczanie (wiersz konsumpcji BK HistoricalPrices.BudgetPostfix, kotwica kiesy TownFolk.AnchorBudget-
    ///     Postfix i lista dostatku TownFolk.LadderOffer) kupuja jedwab surowy i rude srebra tylko z polki PONAD zapas warsztatow miasta: CraftDays dob dziennego wsadu
    ///     linii warsztatow tego miasta, ktore biora te kategorie (szybkosc linii z modelu warsztatu x sztuki wsadu), x przejscie. Tkalnie aksamitu i warsztaty rudy
    ///     srebra maja towar pierwsze, nadwyzka ponad ich zapas idzie do domow jak dotad (bez zalewu polek i bez utraty rynku wsi jedwabnych i kopaln srebra). Miasto
    ///     bez takiego warsztatu (dzis zwykle ruda srebra - mennicy ani pary zlotnika jeszcze nie ma) - mieszczanie kupuja jak dotad. Czesc kiesy niewydana na towar
    ///     ponizej zapasu zostaje w kiesie (jak plotno ponad prog polki).
    ///     PlanksToBuilding: budzet wiersza mieszczan dla desek x (1 - przejscie) (do 0 po 60 dobach) - wiersz i czlon dobrobytu kotwicy razem; budowy biora deski pierwsze.
    ///  4. CharcoalBurners: wies drwali po ticku produkcji wypala 1 ladunek drewna z wlasnego magazynu na wegiel (6.5 kg drewna na 1 kg wegla), gdy wegiel z ladunku
    ///     placi w miescie targowym wiecej niz ladunek drewna (decyzja tylko z ceny - zalany wegiel tanieje i wypal staje); jedna wies na miasto na dobe.
    ///  5. IronIngotsOffShelf: raz na dobe sztaby (Iron1-6) z polek miast i zamkow -> zelazo (lupa / ruda) tej samej osady wedlug wartosci (zaokraglenie losowe) -
    ///     kowale miasta przekuwaja je na zelazo (naprawy kowali, MendMaterial, biora wtedy rude i drewno zamiast sztab z polki).
    ///  6. BkSideGoodsOrigin: marmur kopaln srebra x (1 - przejscie) (GoodsRates.SideStep), zdjete rozdane glinankom (clay_mine - wapien BK) nastepnego dnia: budzet / glinanki,
    ///     ktore wczoraj mialy tick produkcji, nierozdana reszta przechodzi na kolejna dobe. Miod pitny drwali: ciety tylko o tyle, ile miodu pitnego zrobily wczoraj
    ///     miasta (warsztaty, para miod -> miod pitny - ZC5) wobec miodu pitnego drwali (najwyzej przejscie) - swiat ma tyle miodu pitnego co dotad, a dopoki miasta go
    ///     nie robia, drwale robia go jak dotad; za zdjety miod pitny drwale przynosza miod lesny (HoneyPerMead). Papirus - juz T8; zloto Zachodu - juz 24d.
    ///  7. CartPriceByDestination: MarketCarts - czesc ladunku wozu wsi po cenie z chwili wjazdu: tyle, ile wsie handlujace w tym miescie NAPRAWDE kupily tam wczoraj
    ///     (TownFolk.VillageBought: zloto zakupow wsi wedlug kategorii - sol, narzedzia, len, garnki, piwo), a lupa zelaza - dzienne zuzycie kowali i dymarek miasta
    ///     (CaravanBulk.UseNow); x przejscie; reszta sztuka po sztuce jak dotad.
    ///  8. BulkClayLimeCharcoal (tylko kampania zaczeta z kluczem): glina i wapien w ladunkach (HistoricalPrices.Apply - waga x czynnik ladunku kampanii),
    ///     zapas startowy przeliczony (StartStock), wsad gliny i wapna w liniach warsztatow wedlug kg (ConsumeInputFromTownMarket i ConsumeInputFromWarehouse:
    ///     liczba / czynnik, losowo; DetermineItemRosterHasSufficientInputs: koszt wsadu tak samo, z rosteru, z ktorego cykl bierze). Wegiel zostaje w brylach.
    /// Zapis arm_zc1 "2|start BK|ladunki gliny|dzien decyzji|marmur do rozdania|start jedwab|start ruda srebra|start deski|start woz|start wieloryb|start piwo|
    /// miod pitny drwali wczoraj|miod pitny miast wczoraj" ("1|..." - pierwsze 5 pol, reszta od nowa). Brak pola = stary zapis. Potkniecia per obiekt (licznik).
    /// </summary>
    internal static class GoodsPaths
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal const float WhaleShare = 0.35f;       // czesc polowu wielorybnikow wyladowana jako mieso wieloryba (reszta ryby) - wieloryb to mieso i tran, polow dorszy - ryby
        internal const float HoneyPerMead = 0.5f;      // miod pitny to miod i woda (ok. 1 : 2) - za sztuke miodu pitnego (10 l) pol sztuki miodu (5 kg)
        internal const int RampDays = 60;              // zasada Jeffa: zmiana zajecia wsi, zakupow i cen nie od razu - ok. 60 dob przejscia
        internal const float WoodKgPerCoalKg = 6.5f;   // retorta / mielerz: 15% masy drewna (historia 15-20%)
        internal const float CraftDays = 2f;           // rzemioslo pierwsze: na polce zostaje wsad warsztatow miasta na 2 doby (lud kupuje raz na dobe, dostawy ida wozami w ciagu doby)

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

        // ------------------------------------------------------------ przejscia (RampDays dob od pierwszej doby z kluczem; klucz wylaczony - od nowa przy ponownym wlaczeniu)
        internal const int RSide = 0, RSilk = 1, RSilver = 2, RPlanks = 3, RCart = 4, RWhale = 5, RBeer = 6, RN = 7;
        private static readonly string[] RName = { "pochodzenie BK", "jedwab", "ruda srebra", "deski", "woz", "wieloryb", "piwo" };
        private static readonly int[] _rs = { -1, -1, -1, -1, -1, -1, -1 };

        private static bool RampKey(int k)
        {
            switch (k)
            {
                case RSide: return SideOn;
                case RSilk: return SilkOn;
                case RSilver: return SilverOn;
                case RPlanks: return PlanksOn;
                case RCart: return CartOn;
                case RWhale: return WhaleOn;
                case RBeer: return BeerOn;
                default: return false;
            }
        }

        /// <summary>Przejscie k 0..1 (dowolny watek; 0 przy wylaczonym kluczu i przed pierwsza doba z kluczem).</summary>
        internal static float Ramp(int k)
        {
            if (k < 0 || k >= RN || !RampKey(k)) return 0f;
            int st = _rs[k];
            if (st < 0) return 0f;
            int d;
            try { d = Today() - st; } catch { return 0f; }
            return d <= 0 ? 0f : Math.Min(1f, d / (float)RampDays);
        }

        internal static float SideT { get { return Ramp(RSide); } }
        internal static float CartT { get { return Ramp(RCart); } }
        internal static float WhaleT { get { return Ramp(RWhale); } }
        internal static float BeerT { get { return Ramp(RBeer); } }

        // ------------------------------------------------------------ stan kampanii (zapis arm_zc1)
        private static int _bulk;                     // 0 = stary zapis / nieustalone, 1 = kampania zaczeta z BulkClayLimeCharcoal, 2 = zaczeta bez
        private static int _bulkDay = -1;
        private static bool _newGame;
        private static double _marbleBudget, _marbleToday;   // marmur do rozdania dzis (zdjety wczoraj + nierozdana reszta) / zdjety dzis (szt. modelu)
        private static int _clayMines, _clayTicked;   // glinanki, ktore wczoraj mialy tick produkcji (dzielnik budzetu) / dzis
        private static double _meadBaseToday, _meadBaseY;    // miod pitny drwali z modelu PRZED cieciem (tylko tick wsi) - dzis / wczoraj
        private static long _meadTownToday; private static double _meadTownY;   // miod pitny zrobiony w miastach (OnItemProduced poza wsiami) - dzis / wczoraj

        // ------------------------------------------------------------ przedmioty (raz na sesje)
        private static ItemObject _whale, _meat, _fish, _honey, _mead, _marble, _coal, _wood, _iron, _clay, _lime, _grain;
        private static ItemCategory _clayCat, _limeCat;
        private static ItemObject[] _ingots;
        private static bool _looked;

        // ------------------------------------------------------------ liczniki doby
        private static long _dWhaleSwap, _dWhaleFish, _dWhaleMade, _dHoney, _dMarbleGiven, _dCoal, _dCoalWood, _dCoalNoWood, _dCoalNoProfit, _dCoalNoTown;
        private static double _dMeadCut, _dMarbleCut;
        private static long _dIngotPcs, _dIngotValue, _dIngotIron;
        private static int _dIngotTowns;
        private static long _dDteConv, _dDteFood, _dDteEats, _dBkEats;
        // dar BK zdjety wedlug rodzaju partii: 0 karawany niewolnikow, 1 karawany zywnosci, 2 milicje, 3 majatki, 4 wolne kompanie, 5 inne partie BK
        private static readonly string[] BkKindName = { "karawany niewolnikow", "karawany zywnosci", "milicje", "majatki", "wolne kompanie", "inne partie BK" };
        private static readonly long[] _dBkN = new long[6], _dBkUnits = new long[6], _dBkValue = new long[6];
        private static long _dBkOtherEats;
        private static long _dBulkIn, _dBulkTaken, _dBulkCostCut;
        private static long _dBeerExtra, _dBeerUnsold;
        private static int _stumbles, _stumblesDay;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static readonly HashSet<Settlement> _coalTowns = new HashSet<Settlement>();
        private static readonly HashSet<Settlement> _foodTargets = new HashSet<Settlement>();   // cele karawan zywnosci BK w tej sesji (dni glodu celow - linia)
        private static string _hooks = "nie wpiete";
        private static long _ticks;

        internal static void Reset()
        {
            for (int k = 0; k < RN; k++) _rs[k] = -1;
            _bulk = 0; _bulkDay = -1; _newGame = false; _marbleBudget = 0; _marbleToday = 0; _clayMines = 0; _clayTicked = 0;
            _meadBaseToday = _meadBaseY = 0; _meadTownToday = 0; _meadTownY = 0;
            _looked = false; _whale = _meat = _fish = _honey = _mead = _marble = _coal = _wood = _iron = _clay = _lime = _grain = null; _clayCat = _limeCat = null; _ingots = null;
            _stumbles = 0; _errSites.Clear(); _foodTargets.Clear(); _need.Clear(); _needDay = -1; _saltMk = null;
            DayReset();
        }

        private static void DayReset()
        {
            _dWhaleSwap = _dWhaleFish = _dWhaleMade = _dHoney = _dMarbleGiven = _dCoal = _dCoalWood = _dCoalNoWood = _dCoalNoProfit = _dCoalNoTown = 0;
            _dMeadCut = _dMarbleCut = 0;
            _dIngotPcs = _dIngotValue = _dIngotIron = 0; _dIngotTowns = 0;
            _dDteConv = _dDteFood = _dDteEats = _dBkEats = _dBkOtherEats = 0; _dHeroLedger = 0;
            Array.Clear(_dBkN, 0, _dBkN.Length); Array.Clear(_dBkUnits, 0, _dBkUnits.Length); Array.Clear(_dBkValue, 0, _dBkValue.Length);
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
            var sb = new StringBuilder(96);
            sb.Append("2|").Append(_rs[RSide].ToString(Inv)).Append('|').Append(_bulk.ToString(Inv)).Append('|').Append(_bulkDay.ToString(Inv)).Append('|').Append(_marbleBudget.ToString("R", Inv));
            for (int k = RSilk; k < RN; k++) sb.Append('|').Append(_rs[k].ToString(Inv));
            sb.Append('|').Append(_meadBaseY.ToString("R", Inv)).Append('|').Append(_meadTownY.ToString("R", Inv));
            return sb.ToString();
        }

        private static double Num(string s)
        {
            double m;
            return double.TryParse(s, NumberStyles.Float, Inv, out m) && !double.IsNaN(m) && !double.IsInfinity(m) && m > 0 && m < 1e7 ? m : 0;
        }

        /// <summary>Wczytanie: brak pola, inny format, blad - stary zapis (glina i wapien w workach, przejscia od pierwszej doby z kluczem).
        /// "1|..." (5 pol, przed poprawkami recenzji) - przejscie BK i marmur z zapisu, reszta przejsc od pierwszej doby z kluczem.</summary>
        internal static void Import(string v)
        {
            for (int k = 0; k < RN; k++) _rs[k] = -1;
            _bulk = 0; _bulkDay = -1; _marbleBudget = 0; _marbleToday = 0; _meadBaseY = 0; _meadTownY = 0;
            if (string.IsNullOrEmpty(v)) return;
            try
            {
                var p = v.Split('|');
                if (p.Length < 5 || (p[0] != "1" && p[0] != "2")) return;
                _rs[RSide] = int.Parse(p[1], Inv);
                int b = int.Parse(p[2], Inv);
                _bulk = b >= 0 && b <= 2 ? b : 0;
                _bulkDay = int.Parse(p[3], Inv);
                _marbleBudget = Num(p[4]);
                if (p[0] == "2" && p.Length >= 5 + (RN - 1) + 2)
                {
                    for (int k = RSilk; k < RN; k++) _rs[k] = int.Parse(p[4 + k], Inv);
                    _meadBaseY = Num(p[4 + RN]); _meadTownY = Num(p[5 + RN]);
                }
            }
            catch (Exception e) { for (int k = 0; k < RN; k++) _rs[k] = -1; _bulk = 0; _bulkDay = -1; _marbleBudget = 0; _meadBaseY = 0; _meadTownY = 0; Stumble("Import", e); }
        }

        private static void Look()
        {
            if (_looked) return;
            var om = MBObjectManager.Instance;
            if (om == null) return;
            _looked = true;
            _whale = om.GetObject<ItemObject>("WhaleMeat"); _meat = om.GetObject<ItemObject>("meat"); _fish = om.GetObject<ItemObject>("fish");
            _honey = om.GetObject<ItemObject>("honey"); _mead = om.GetObject<ItemObject>("mead"); _marble = om.GetObject<ItemObject>("marble"); _coal = om.GetObject<ItemObject>("charcoal");
            _wood = om.GetObject<ItemObject>("hardwood"); _iron = om.GetObject<ItemObject>("iron"); _grain = om.GetObject<ItemObject>("grain");
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
        /// <summary>Postfiks WorkshopsCampaignBehavior.GetItemsToProduce (ApplyEarly - przed latkami cyklu warsztatu): wyrob WhaleMeat z cyklu warsztatu (uboj w miescie)
        /// -> meat w zwracanej liscie, utarg cyklu (income) przeliczony cena meat. Wszystko dalej (ProduceAnOutputToTown / ToWarehouse, zbiorcze OnItemProduced gry,
        /// pula doplaty 35%, prog zysku cyklu, prefiks BK) widzi meat. Modyfikator jakosci nie przechodzi (sztuki towarow ida bez modyfikatora). Nie rzuca.</summary>
        public static void ItemsPost(List<EquipmentElement> __result, Workshop workshop, ref int income)
        {
            try
            {
                if (__result == null || __result.Count == 0 || !WhaleOn) return;
                Look();
                if (_whale == null || _meat == null) return;
                Town town = workshop != null && workshop.Settlement != null ? workshop.Settlement.Town : null;
                for (int i = 0; i < __result.Count; i++)
                {
                    if (!ReferenceEquals(__result[i].Item, _whale)) continue;
                    var e = new EquipmentElement(_meat);
                    if (town != null)
                    {
                        try { income += town.GetItemPrice(e, null, true) - town.GetItemPrice(__result[i], null, true); }
                        catch (Exception ex) { Stumble("ItemsPost(utarg)", ex); }
                    }
                    __result[i] = e;
                    _dWhaleSwap++;
                }
            }
            catch (Exception ex) { Stumble("ItemsPost", ex); }
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

        /// <summary>GoodsRates.SideStep: miod pitny drwali z modelu PRZED cieciem w ticku TEJ wsi (podstawa udzialu miast - MeadT).</summary>
        internal static void NoteMeadBase(Village v, float before)
        {
            if (v == null || !(before > 0f) || !ReferenceEquals(v, _tv)) return;
            _meadBaseToday += before;
        }

        /// <summary>Czesc miodu pitnego drwali do zdjecia 0..1: miod pitny miast wczoraj / miod pitny drwali wczoraj, najwyzej przejscie BK (dowolny watek).
        /// Miasta go nie robia (dzis: meadery BK stoja, para miod -> miod pitny - ZC5) - 0, drwale robia jak dotad.</summary>
        internal static float MeadT
        {
            get
            {
                float t = SideT;
                double b = _meadBaseY, m = _meadTownY;
                if (!(t > 0f) || !(b > 0) || !(m > 0)) return 0f;
                return (float)Math.Min(t, Math.Min(1.0, m / b));
            }
        }

        /// <summary>Nasluch OnItemProducedEvent (ArmouryBehavior): miod pitny dopisany miastu albo zamkowi (warsztat, rzemioslo miasta) - podstawa MeadT. Nie rzuca.</summary>
        internal static void OnItemProduced(ItemObject it, Settlement st, int n)
        {
            try
            {
                if (it == null || st == null || n <= 0 || st.IsVillage) return;
                Look();
                if (_mead != null && ReferenceEquals(it, _mead)) _meadTownToday += n;
            }
            catch (Exception e) { Stumble("OnItemProduced", e); }
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
                else if (type == "clay_mine") { _clayTicked++; Marble(v, r); }
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
            _dWhaleFish += added;
            float t = WhaleT;   // przejscie: polow wielorybnikow zmienia sie przez 60 dob (zasada Jeffa)
            if (!(t > 0f)) return;
            int k = MBRandom.RoundRandomized(added * WhaleShare * t);
            if (k > added) k = added;
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
            if (!SideOn || !(_marbleBudget > 0) || _marble == null) return;
            if (_clayMines <= 0) _clayMines = ClayMinesNow();   // po wczytaniu przed pierwsza doba - liczba glinanek bez spladrowanych (budzet z zapisu rozdany od razu)
            if (_clayMines <= 0) return;
            int n = MBRandom.RoundRandomized((float)(_marbleBudget / _clayMines));
            if (n <= 0) return;
            var f = Fz(v.Settlement, "marmur z kamieniolomow (glinanki)");
            try { r.AddToCounts(_marble, n); }
            finally { GoodsLedger.End(f); }
            _dMarbleGiven += n;
            try { CampaignEventDispatcher.Instance.OnItemProduced(_marble, v.Settlement, n); } catch (Exception e) { Stumble("Marble(zdarzenie)", e); }
        }

        private static int ClayMinesNow()
        {
            int clay = 0;
            foreach (var v in Village.All)
                if (v != null && v.VillageType != null && v.VillageType.StringId == "clay_mine" && !v.IsDeserted && v.VillageState == Village.VillageStates.Normal) clay++;
            return clay;
        }

        private static void Coal(Village v, ItemRoster r)
        {
            if (!CoalOn || _coal == null || _wood == null) return;
            var mk = v.TradeBound ?? v.Bound;
            if (mk == null || mk.Town == null || mk.ItemRoster == null) { _dCoalNoTown++; return; }
            if (_coalTowns.Contains(mk)) return;
            if (r.GetItemNumber(_wood) < 1) { _dCoalNoWood++; return; }
            // decyzja tylko z ceny (bez stalego progu sztuk): wegiel z ladunku drewna placi w miescie targowym wiecej niz ten ladunek - zalany wegiel tanieje i wypal staje
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

        // ============================================================ 5. sztaby z polek -> zelazo (kowale miasta przekuwaja)
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
                    if (n > 0) WorkshopFairScore.NoteProduced(t.Settlement, _iron, n, WorkshopFairScore.SrcScrap);   // F4 (recenzja): zelazo z przekutych sztab do pomiaru wsadu regionu (zamek - pominiety) - bez zdarzenia gry
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

        // ============================================================ 3. mieszczanie: rzemioslo pierwsze (jedwab, ruda srebra), deski do budow
        /// <summary>Czynnik wiersza konsumpcji mieszczan i czlonu dobrobytu kotwicy (HistoricalPrices.BudgetPostfix / ProsperityPart): deski x (1 - przejscie), reszta 1.</summary>
        internal static float FolkFactor(ItemCategory c)
        {
            if (c == null || c.StringId != "planks" || !PlanksOn) return 1f;
            return 1f - Ramp(RPlanks);
        }

        /// <summary>Kategoria z regula "rzemioslo pierwsze" (jedwab surowy przy SilkToWeavers, ruda srebra przy SilverOreToSmiths).</summary>
        internal static bool CraftFirst(ItemCategory c)
        {
            if (c == null || c.StringId == null) return false;
            string id = c.StringId;
            return (id == "cotton" && SilkOn) || (id == "silver" && SilverOn);
        }

        private static readonly Dictionary<Town, float[]> _need = new Dictionary<Town, float[]>();   // dzienny wsad linii warsztatow miasta: [0] jedwab, [1] ruda srebra (szt./d)
        private static int _needDay = -1;

        private static float[] NeedOf(Town town)
        {
            int d = Today();
            if (d != _needDay) { _need.Clear(); _needDay = d; }
            float[] a;
            if (_need.TryGetValue(town, out a)) return a;
            a = new float[2];
            var wm = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.WorkshopModel : null;
            var ws = town.Workshops;
            if (ws != null)
                foreach (var w in ws)
                {
                    var wt = w != null ? w.WorkshopType : null;
                    if (wt == null || wt.Productions == null) continue;
                    foreach (var p in wt.Productions)
                    {
                        if (p.Inputs == null) continue;   // Production to struct
                        float k0 = 0f, k1 = 0f;
                        foreach (var i in p.Inputs)
                        {
                            if (i.Item1 == null || i.Item2 <= 0) continue;
                            if (i.Item1.StringId == "cotton") k0 += i.Item2; else if (i.Item1.StringId == "silver") k1 += i.Item2;
                        }
                        if (!(k0 > 0f) && !(k1 > 0f)) continue;
                        float sp = p.ConversionSpeed;
                        try { if (wm != null) sp = wm.GetEffectiveConversionSpeedOfProduction(w, p.ConversionSpeed, false).ResultNumber; } catch { }
                        if (!(sp > 0f) || float.IsInfinity(sp)) continue;
                        a[0] += sp * k0; a[1] += sp * k1;
                    }
                }
            _need[town] = a;
            return a;
        }

        /// <summary>Zapas warsztatow miasta (szt.), ktorego mieszczanie nie biora: CraftDays x dzienny wsad linii warsztatow tej kategorii x przejscie. 0 - poza regula.</summary>
        internal static float CraftReserve(Town town, ItemCategory c)
        {
            try
            {
                if (town == null || !CraftFirst(c)) return 0f;
                bool silk = c.StringId == "cotton";
                float t = Ramp(silk ? RSilk : RSilver);
                if (!(t > 0f)) return 0f;
                float need = NeedOf(town)[silk ? 0 : 1];
                return need > 0f ? t * CraftDays * need : 0f;
            }
            catch (Exception e) { Stumble("CraftReserve", e); return 0f; }
        }

        /// <summary>Budzet mieszczan (zloto) na kategorie z regula "rzemioslo pierwsze": najwyzej polka ponad zapas warsztatow x cena (stos z najwieksza liczba sztuk).
        /// Kategoria poza regula, brak zapasu warsztatow, blad - budget bez zmian. Dowolne wywolanie wiersza (konsumpcja, prognozy, kotwica).</summary>
        internal static float FolkCap(Town town, ItemCategory c, float budget)
        {
            try
            {
                if (!(budget > 0f) || town == null || !CraftFirst(c)) return budget;
                float res = CraftReserve(town, c);
                if (!(res > 0f)) return budget;
                var r = town.Owner != null ? town.Owner.ItemRoster : null;
                if (r == null) return budget;
                int pcs = 0, big = 0; ItemObject top = null;
                for (int k = 0; k < r.Count; k++)
                {
                    var el = r.GetElementCopyAtIndex(k);
                    var it = el.EquipmentElement.Item;
                    if (it == null || el.Amount <= 0 || it.ItemCategory != c) continue;
                    pcs += el.Amount;
                    if (el.Amount > big) { big = el.Amount; top = it; }
                }
                if (top == null) return budget;                       // pusta polka - BK i tak nic nie kupi
                double room = (pcs - res) * (double)Math.Max(1, town.GetItemPrice(top));
                if (!(room > 0)) return 0f;
                return room < budget ? (float)room : budget;
            }
            catch (Exception e) { Stumble("FolkCap", e); return budget; }
        }

        // ============================================================ 2. piwo rzemieslnikow (ArtisanInputs - liczniki)
        internal static void NoteBeer(int extra, int unsold) { _dBeerExtra += extra; _dBeerUnsold += unsold; }

        // ============================================================ konwoje DTE i partie BK (jedzenie z niczego)
        public static void DteSupPre(MobileParty __0, out Dictionary<ItemObject, int> __state)
        {
            __state = null;
            try
            {
                if (!DteOn || __0 == null || __0.ItemRoster == null) return;
                __state = FoodOf(__0.ItemRoster);
            }
            catch (Exception e) { __state = null; Stumble("DteSupPre", e); }
        }

        private static Dictionary<ItemObject, int> FoodOf(ItemRoster r)
        {
            var d = new Dictionary<ItemObject, int>();
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || !it.IsFood || el.Amount <= 0) continue;
                int n; d.TryGetValue(it, out n); d[it] = n + el.Amount;
            }
            return d;
        }

        /// <summary>Zdejmuje z rosteru jedzenie dopisane od stanu was (kazdy rodzaj: teraz - wtedy, gdy dodatnie); units / value - zdjete sztuki i ich wartosc.</summary>
        private static void TakeAdded(ItemRoster r, Dictionary<ItemObject, int> was, out long units, out long value)
        {
            units = 0; value = 0;
            foreach (var kv in FoodOf(r))
            {
                int w; was.TryGetValue(kv.Key, out w);
                int d = kv.Value - w;
                if (d <= 0) continue;
                r.AddToCounts(kv.Key, -d);
                units += d; value += (long)Math.Max(0, kv.Key.Value) * d;
            }
        }

        public static void DteSupPost(MobileParty __0, Dictionary<ItemObject, int> __state)
        {
            if (__state == null || __0 == null) return;
            try
            {
                _dDteConv++;
                var m = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.MobilePartyFoodConsumptionModel : null;
                if (m == null || m.DoesPartyConsumeFood(__0)) { _dDteEats++; return; }   // konwoj, ktory je - zapas drogi DTE zostaje
                long u, v;
                TakeAdded(__0.ItemRoster, __state, out u, out v);
                _dDteFood += u;
            }
            catch (Exception e) { Stumble("DteSupPost", e); }
        }

        [ThreadStatic] private static int _bkCaravan;
        [ThreadStatic] private static int _bkKind;   // rodzaj karawany w oknie CreateSlaveCaravan (0) / CreateFoodCaravan (1)
        public static void BkSlavePre() { _bkCaravan++; _bkKind = 0; }
        public static void BkFoodCarPre(Settlement __1)
        {
            _bkCaravan++; _bkKind = 1;
            try { if (__1 != null && __1.Town != null) _foodTargets.Add(__1); } catch (Exception e) { Stumble("BkFoodCarPre", e); }
        }
        public static void BkCaravanFin() { if (_bkCaravan > 0) _bkCaravan--; }

        internal sealed class BkFood { public int Kind; public Dictionary<ItemObject, int> Before; }

        // f14-ksiega (noc 18.10; fin92 Armoury-2026-10-18_09-33-39.log): dar BK dla bandy herszta (BanditHeroComponent.CreateParty i ConsiderLeaveHideout ->
        // BannerKingsComponent.GiveFood) - gra bez zmian (pomiar E8/P3, decyzja "dar zostaje" - bez zywnosci herszt BK nie wychodzi na rabunek), ale ksiega towarow
        // dostaje nazwane zrodlo (GoodsLedger.NoteKnownSource): przy zalozeniu partii (DailyTickClan, poza ramkami) dar szedl dotad do "bez wyjasnienia"
        // (d109176 +15, d109213 +429 = "GiveFood 1 razy, 429 szt." linii E5/A8), w ticku partii (ramka) - do "inne ticki dobowe". Rodzaj -2 = tylko ksiega.
        private const int KHeroLedger = -2;
        private const string HeroFoodSub = "dar BK dla bandy herszta (GiveFood, z niczego)";
        private static long _dHeroLedger;   // sztuki daru herszta nazwane w ksiedze (doba) - tylko licznik

        private static bool BanditHero(MobileParty mp)
        {
            var pc = mp != null ? mp.PartyComponent : null;
            return pc != null && pc.GetType().Name == "BanditHeroComponent";   // ten sam test co Stage8World.BanditHeroParty (pomiar E8/P3)
        }

        /// <summary>Rodzaj partii BK dla daru GiveFood: -1 = nie ruszamy (wedrowcy - 36b, partie spoza BK).</summary>
        private static int BkKindOf(MobileParty mp)
        {
            if (_bkCaravan > 0) return _bkKind == 1 ? 1 : 0;
            var pc = mp.PartyComponent;
            string tn = pc != null ? pc.GetType().Name : null;
            if (tn == null) return -1;
            switch (tn)
            {
                case "PopulationPartyComponent": return -1;     // wedrowcy (CreateTravellerParty) - zywnosc drogi z polki daje 36b (TravellerTrade36b)
                case "MilitiaComponent": return 2;
                case "EstateComponent": return 3;
                case "FreeCompanyComponent": return 4;
            }
            var t = pc.GetType();
            while (t != null && t != typeof(object)) { if (t.Name == "BannerKingsComponent") return 5; t = t.BaseType; }
            return -1;                                          // herszci band (BanditHeroComponent) i reszta - jedza, dar BK zostaje (pomiar E8/P3)
        }

        /// <summary>Prefiks BannerKingsComponent.GiveFood(ref MobileParty): stan jedzenia partii BK, ktora nie je (karawany niewolnikow i zywnosci, milicje, majatki,
        /// wolne kompanie) - postfiks zdejmie to, co GiveFood dopisze. Wedrowcy - 36b; partie, ktore jedza - bez zmian.</summary>
        public static void BkGiveFoodPre(ref MobileParty __0, out BkFood __state)
        {
            __state = null;
            try
            {
                if (__0 == null || __0.ItemRoster == null) return;
                // f14-ksiega: herszt BK - dar zostaje (jak dotad), stan jedzenia tylko do nazwania daru w ksiedze towarow (bez ksiegi - nic)
                if (GoodsLedger.On && _bkCaravan <= 0 && BanditHero(__0)) { __state = new BkFood { Kind = KHeroLedger, Before = FoodOf(__0.ItemRoster) }; return; }
                if (!BkFoodOn) return;
                int kind = BkKindOf(__0);
                if (kind < 0) return;
                var m = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.MobilePartyFoodConsumptionModel : null;
                if (m == null || m.DoesPartyConsumeFood(__0)) { if (kind <= 1) _dBkEats++; else _dBkOtherEats++; return; }
                __state = new BkFood { Kind = kind, Before = FoodOf(__0.ItemRoster) };
            }
            catch (Exception e) { __state = null; Stumble("BkGiveFoodPre", e); }
        }

        public static void BkGiveFoodPost(ref MobileParty __0, BkFood __state)
        {
            if (__state == null || __0 == null || __0.ItemRoster == null) return;
            try
            {
                if (__state.Kind == KHeroLedger) { NoteHeroFood(__0.ItemRoster, __state.Before); return; }   // f14-ksiega: tylko ksiega, jukow nie ruszamy
                long u, v;
                TakeAdded(__0.ItemRoster, __state.Before, out u, out v);
                int k = __state.Kind;
                _dBkN[k]++; _dBkUnits[k] += u; _dBkValue[k] += v;
            }
            catch (Exception e) { Stumble("BkGiveFoodPost", e); }
        }

        /// <summary>f14-ksiega: jedzenie, ktore GiveFood dopisal bandzie herszta (teraz - wtedy, gdy dodatnie) - nazwane zrodlo ksiegi towarow (NoteKnownSource zdejmuje
        /// przyrost z otwartej ramki, wiec w ticku partii nie idzie juz do "inne ticki dobowe"). Juki bez zmian. Nie rzuca (wolajacy w try).</summary>
        private static void NoteHeroFood(ItemRoster r, Dictionary<ItemObject, int> was)
        {
            foreach (var kv in FoodOf(r))
            {
                int w; was.TryGetValue(kv.Key, out w);
                int d = kv.Value - w;
                if (d <= 0) continue;
                GoodsLedger.NoteKnownSource(kv.Key, d, HeroFoodSub);
                _dHeroLedger += d;
            }
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

        /// <summary>Prefiks ConsumeInputFromTownMarket i ConsumeInputFromWarehouse (Priority.First - przed BK i rachunkiem cyklu): wsad gliny / wapna linii warsztatu =
        /// liczba z receptury / czynnik ladunku (losowo - te same kg co w workach; 0 sztuk = cykl bez zdjecia ladunku).</summary>
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

        /// <summary>Postfiks DetermineItemRosterHasSufficientInputs(production, itemRoster, town, out cost): koszt wsadu gliny / wapna w progu cyklu wedlug kg
        /// (cena ladunku x liczba / czynnik) - z rosteru, ktory gra sprawdza (polka miasta albo magazyn gracza).</summary>
        public static void BulkCostPost(WorkshopType.Production __0, ItemRoster __1, Town __2, ref int __3, bool __result)
        {
            try
            {
                if (_bulk != 1 || !__result || __2 == null || __1 == null || __0.Inputs == null) return;
                var r = __1;
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
                var items = AccessTools.Method(b, "GetItemsToProduce");
                var cons = AccessTools.Method(b, "ConsumeInputFromTownMarket");
                var ware = AccessTools.Method(b, "ConsumeInputFromWarehouse");
                var suf = AccessTools.Method(b, "DetermineItemRosterHasSufficientInputs");
                if (items != null) { h.Patch(items, postfix: new HarmonyMethod(typeof(GoodsPaths), nameof(ItemsPost))); parts.Add("wyroby cyklu (mieso wieloryba -> meat)"); }
                else parts.Add("BRAK GetItemsToProduce (mieso wieloryba z warsztatow zostaje)");
                if (cons != null) { h.Patch(cons, prefix: new HarmonyMethod(typeof(GoodsPaths), nameof(BulkInPre)) { priority = Priority.First }); parts.Add("wsad z targu"); }
                else parts.Add("BRAK ConsumeInputFromTownMarket");
                if (ware != null) { h.Patch(ware, prefix: new HarmonyMethod(typeof(GoodsPaths), nameof(BulkInPre)) { priority = Priority.First }); parts.Add("wsad z magazynu"); }
                else parts.Add("BRAK ConsumeInputFromWarehouse");
                if (suf != null) { h.Patch(suf, postfix: new HarmonyMethod(typeof(GoodsPaths), nameof(BulkCostPost))); parts.Add("koszt wsadu"); }
                else parts.Add("BRAK DetermineItemRosterHasSufficientInputs");
            }
            catch (Exception e) { parts.Add("blad " + e.GetType().Name); Log.Error("GoodsPaths.ApplyEarly", e); }
            _hooks = "warsztaty: " + string.Join(", ", parts.ToArray());
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
                    var slave = AccessTools.Method(ppc, "CreateSlaveCaravan");
                    var food = AccessTools.Method(ppc, "CreateFoodCaravan");
                    if (slave != null) { h.Patch(slave, prefix: new HarmonyMethod(typeof(GoodsPaths), nameof(BkSlavePre)) { priority = Priority.First }, finalizer: new HarmonyMethod(typeof(GoodsPaths), nameof(BkCaravanFin))); n++; }
                    if (food != null) { h.Patch(food, prefix: new HarmonyMethod(typeof(GoodsPaths), nameof(BkFoodCarPre)) { priority = Priority.First }, finalizer: new HarmonyMethod(typeof(GoodsPaths), nameof(BkCaravanFin))); n++; }
                    h.Patch(give, prefix: new HarmonyMethod(typeof(GoodsPaths), nameof(BkGiveFoodPre)) { priority = Priority.First }, postfix: new HarmonyMethod(typeof(GoodsPaths), nameof(BkGiveFoodPost)));
                }
                parts.Add(give != null ? "partie BK (karawany " + n + "/2, dar GiveFood)" : (ppc == null ? "bez BK" : "BRAK BK GiveFood"));
            }
            catch (Exception e) { parts.Add("BK: blad " + e.GetType().Name); Log.Error("GoodsPaths.ApplyAll(BK)", e); }
            _hooks = _hooks + "; " + string.Join(", ", parts.ToArray());
            Log.Info("Towary (ZC1): latki przy starcie gry - " + _hooks + ".");
        }

        // ============================================================ doba (ArmouryBehavior.OnDailyTick, watek glowny)
        private static HashSet<Town> _saltMk;   // miasta targowe wsi solnych (stale w sesji) - kasy do linii (woz za cena celu)

        private static HashSet<Town> SaltMarkets()
        {
            if (_saltMk != null) return _saltMk;
            var hs = new HashSet<Town>();
            foreach (var v in Village.All)
            {
                if (v == null || v.VillageType == null || v.VillageType.StringId != "salt_mine") continue;
                var mk = v.TradeBound ?? v.Bound;
                if (mk != null && mk.Town != null && mk.IsTown) hs.Add(mk.Town);
            }
            _saltMk = hs;
            return hs;
        }

        internal static void Daily()
        {
            long t0 = Stopwatch.GetTimestamp();
            var s = S;
            if (s == null) return;
            int day = Today() - 1;
            double meadBase = _meadBaseToday, meadTown = _meadTownToday;
            int clayTicked = _clayTicked;
            try
            {
                // przejscia: start przy pierwszej dobie z kluczem; wylaczony - od nowa przy ponownym wlaczeniu
                for (int k = 0; k < RN; k++)
                {
                    if (RampKey(k)) { if (_rs[k] < 0) _rs[k] = Today(); }
                    else _rs[k] = -1;
                }
                // miod pitny: wczoraj drwale (model przed cieciem) i miasta - podstawa jutrzejszego ciecia (MeadT)
                _meadBaseY = meadBase; _meadTownY = meadTown; _meadBaseToday = 0; _meadTownToday = 0;
                // budzet marmuru na jutro: zdjete dzis + nierozdana reszta; dzielnik - glinanki, ktore dzis mialy tick (spladrowane nie tykaja)
                double left = Math.Max(0.0, _marbleBudget - _dMarbleGiven);
                _marbleBudget = SideOn ? _marbleToday + left : 0; _marbleToday = 0;
                _clayMines = clayTicked > 0 ? clayTicked : ClayMinesNow();
                _clayTicked = 0;
                Ingots();
            }
            catch (Exception e) { Stumble("Daily(przebieg)", e); }
            try
            {
                Look();
                // indeksy w miastach: aksamit (prog projektu: < 10 w >= 50% miast), miod pitny, zboze; rzemioslo pierwsze - zapas warsztatow i polki
                int towns = 0, velvetLow = 0, velvetHave = 0;
                ItemObject velvet = MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObject<ItemObject>("velvet") : null;
                double velvetIdx = 0, meadIdx = 0, grainIdx = 0;
                int meadN = 0, grainN = 0;
                var om = MBObjectManager.Instance;
                ItemCategory silkC = om != null ? om.GetObject<ItemCategory>("cotton") : null, silvC = om != null ? om.GetObject<ItemCategory>("silver") : null,
                             plC = om != null ? om.GetObject<ItemCategory>("planks") : null;
                double[] cRes = new double[2], cShelf = new double[2]; int[] cTowns = new int[2], cLow = new int[2];
                long saltGold = 0; int saltTowns = 0;
                var smk = SaltMarkets();
                foreach (var t in Town.AllTowns)
                {
                    if (t == null || !t.IsTown || t.Owner == null) continue;
                    towns++;
                    if (velvet != null && velvet.ItemCategory != null)
                    {
                        float ix = t.GetItemCategoryPriceIndex(velvet.ItemCategory);
                        velvetIdx += ix;
                        if (ix < 9.99f) velvetLow++;
                        if (t.Owner.ItemRoster != null && t.Owner.ItemRoster.GetItemNumber(velvet) > 0) velvetHave++;
                    }
                    if (_mead != null && _mead.ItemCategory != null) { meadIdx += t.GetItemCategoryPriceIndex(_mead.ItemCategory); meadN++; }
                    if (_grain != null && _grain.ItemCategory != null) { grainIdx += t.GetItemCategoryPriceIndex(_grain.ItemCategory); grainN++; }
                    for (int k = 0; k < 2; k++)
                    {
                        var c = k == 0 ? silkC : silvC;
                        if (c == null || !CraftFirst(c)) continue;
                        float res = CraftReserve(t, c);
                        if (!(res > 0f)) continue;
                        cTowns[k]++; cRes[k] += res;
                        int pcs = 0;
                        var r = t.Owner.ItemRoster;
                        if (r != null) for (int i = 0; i < r.Count; i++) { var el = r.GetElementCopyAtIndex(i); if (el.EquipmentElement.Item != null && el.EquipmentElement.Item.ItemCategory == c) pcs += el.Amount; }
                        cShelf[k] += pcs;
                        if (pcs <= res) cLow[k]++;
                    }
                    if (smk.Contains(t)) { saltTowns++; saltGold += t.Gold; }
                }
                // cele karawan zywnosci BK bez zapasu dzis (dni glodu celow)
                int fTargets = 0, fHungry = 0;
                foreach (var st in _foodTargets)
                {
                    var tw = st != null ? st.Town : null;
                    if (tw == null) continue;
                    fTargets++;
                    if (tw.FoodStocks <= 0f) fHungry++;
                }
                long carts, cartFirst, cartFair, cartOld, ironN, ironFirst, ironFair, ironOld, cartMis;
                MarketCarts.TakeZc1(out carts, out cartFirst, out cartFair, out cartOld, out ironN, out ironFirst, out ironFair, out ironOld, out cartMis);
                var sb = new StringBuilder(2600);
                double f = 1000.0 / Stopwatch.Frequency;
                sb.Append("Towary (ZC1): dzien ").Append(day)
                  .Append(" | przejscia (").Append(RampDays).Append(" dob): ");
                for (int k = 0; k < RN; k++) { if (k > 0) sb.Append(", "); sb.Append(RName[k]).Append(' ').Append(Ramp(k).ToString("0.00", Inv)); }
                sb.Append(" | wieloryb (Whale Meat From Whalers Only ").Append(TN(s.WhaleMeatFromWhalersOnly)).Append("): wyrob warsztatow WhaleMeat -> meat (losowania cyklu, takze cykle bez wyrobu) ").Append(_dWhaleSwap)
                  .Append(" szt., wielorybnicy: ryby z ticku ").Append(_dWhaleFish).Append(", z tego mieso wieloryba ").Append(_dWhaleMade)
                  .Append(" | piwo rzemieslnikow x(1 + przejscie) (Bk Recipe Yield By Workshop ").Append(TN(s.BkRecipeYieldByWorkshop)).Append("): drugie piwo z cyklu ").Append(_dBeerExtra)
                  .Append(", niesprzedane (kasa bez nadwyzki) ").Append(_dBeerUnsold)
                  .Append(", indeks zboza srednio ").Append(grainN > 0 ? (grainIdx / grainN).ToString("0.00", Inv) : "-")
                  .Append(" | rzemioslo pierwsze (mieszczanie ponad ").Append(CraftDays.ToString("0", Inv)).Append(" doby wsadu warsztatow): jedwab ").Append(TN(s.SilkToWeavers))
                  .Append(" - miast z zapasem ").Append(cTowns[0]).Append(", zapas ").Append(cRes[0].ToString("0", Inv)).Append(" szt., polki ").Append(cShelf[0].ToString("0", Inv))
                  .Append(", polka <= zapas ").Append(cLow[0]).Append("; ruda srebra ").Append(TN(s.SilverOreToSmiths)).Append(" - miast z zapasem ").Append(cTowns[1])
                  .Append(", zapas ").Append(cRes[1].ToString("0", Inv)).Append(" szt., polki ").Append(cShelf[1].ToString("0", Inv)).Append(", polka <= zapas ").Append(cLow[1])
                  .Append(" (bez warsztatu rudy - mieszczanie jak dotad); deski (Planks To Building ").Append(TN(s.PlanksToBuilding)).Append(") budzet mieszczan x ")
                  .Append(FolkFactor(plC).ToString("0.00", Inv)).Append(", budowy biora deski pierwsze")
                  .Append(" | aksamit: miast ").Append(towns).Append(", z indeksem < 10 ").Append(velvetLow).Append(" (").Append(towns > 0 ? (100.0 * velvetLow / towns).ToString("0", Inv) : "-")
                  .Append("%), z aksamitem na polce ").Append(velvetHave).Append(", indeks srednio ").Append(towns > 0 ? (velvetIdx / towns).ToString("0.00", Inv) : "-")
                  .Append(" | wegiel (Charcoal Burners ").Append(TN(s.CharcoalBurners)).Append("): drwale wypalili ").Append(_dCoalWood).Append(" ladunkow drewna na ").Append(_dCoal)
                  .Append(" bryl; nie: bez drewna w magazynie ").Append(_dCoalNoWood).Append(", bez zysku (wegiel z ladunku tanszy niz ladunek) ").Append(_dCoalNoProfit)
                  .Append(", bez miasta ").Append(_dCoalNoTown)
                  .Append(" | sztaby z polek (Iron Ingots Off Shelf ").Append(TN(s.IronIngotsOffShelf)).Append("): osad ").Append(_dIngotTowns).Append(", sztab ").Append(_dIngotPcs)
                  .Append(" warte ").Append(_dIngotValue).Append(" zl -> zelaza ").Append(_dIngotIron)
                  .Append(" | pochodzenie BK (Bk Side Goods Origin ").Append(TN(s.BkSideGoodsOrigin)).Append("): przejscie ").Append(SideT.ToString("0.00", Inv))
                  .Append(" (start ").Append(_rs[RSide]).Append("); marmur zdjety kopalniom srebra ").Append(_dMarbleCut.ToString("0.0", Inv))
                  .Append(", glinanki dostaly ").Append(_dMarbleGiven).Append(" (budzet na jutro ").Append(_marbleBudget.ToString("0.0", Inv)).Append(" na ").Append(_clayMines)
                  .Append(" glinanek z tickiem); miod pitny drwali (model) ").Append(meadBase.ToString("0.0", Inv)).Append(", miast ").Append(meadTown.ToString("0", Inv))
                  .Append(", zdjety drwalom ").Append(_dMeadCut.ToString("0.0", Inv)).Append(" (czesc jutro ").Append(MeadT.ToString("0.00", Inv)).Append("), miod lesny ").Append(_dHoney)
                  .Append(", indeks miodu pitnego srednio ").Append(meadN > 0 ? (meadIdx / meadN).ToString("0.00", Inv) : "-")
                  .Append(" | woz za cena celu (Cart Price By Destination ").Append(TN(s.CartPriceByDestination)).Append("): sol ").Append(carts).Append(" szt. - woz dostal ")
                  .Append(Per(cartFair, carts)).Append(" d/szt., cena miasta przy wjezdzie ").Append(Per(cartFirst, carts)).Append(" (stosunek ").Append(Ratio(cartFair, cartFirst))
                  .Append("; stara regula ").Append(Ratio(cartOld, cartFirst)).Append("); zelazo ").Append(ironN).Append(" szt. - ").Append(Per(ironFair, ironN)).Append(" / ")
                  .Append(Per(ironFirst, ironN)).Append(" (stosunek ").Append(Ratio(ironFair, ironFirst)).Append("; stara regula ").Append(Ratio(ironOld, ironFirst)).Append(")")
                  .Append("; sprzedazy wozow z odtworzeniem niewiernym (poza licznikami) ").Append(cartMis)
                  .Append("; kasy miast targowych wsi solnych ").Append(saltTowns).Append(" miast ").Append(saltGold).Append(" zl")
                  .Append(" | jedzenie z niczego: konwoje DTE (Dte Convoy No Free Food ").Append(TN(s.DteConvoyNoFreeFood)).Append(") ").Append(_dDteConv).Append(", zdjete ").Append(_dDteFood)
                  .Append(" szt., konwoj je (zostawione) ").Append(_dDteEats)
                  .Append("; partie BK (Bk Caravan No Free Food ").Append(TN(s.BkCaravanNoFreeFood)).Append(") bez daru ").Append(_dBkN[0] + _dBkN[1]).Append(" karawan, partia je (dar zostal) ").Append(_dBkEats);
                for (int k = 0; k < BkKindName.Length; k++)
                    sb.Append(k == 0 ? " [" : "; ").Append(BkKindName[k]).Append(' ').Append(_dBkN[k]).Append(" x, ").Append(_dBkUnits[k]).Append(" szt. warte ").Append(_dBkValue[k]).Append(" zl");
                sb.Append("], inne partie BK, ktore jedza ").Append(_dBkOtherEats)
                  .Append("; do spichlerzy celow karawan zywnosci nie dojdzie ").Append(_dBkUnits[1]).Append(" szt. (cele ").Append(fTargets).Append(", bez zapasu dzis ").Append(fHungry).Append(")")
                  .Append("; dar BK dla band hersztow nazwany w ksiedze towarow (f14-ksiega, gra bez zmian) ").Append(_dHeroLedger).Append(" szt.")
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
