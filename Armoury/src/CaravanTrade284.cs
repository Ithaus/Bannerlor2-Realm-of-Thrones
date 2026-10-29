using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// PACZKA 284 (27.10, Jeff: "i nadal warsztat zarabia grosze a karawana nic nie zarabia sprawdz moj log", "rob te plotna skory wszystkie surowce aby ta gospodarka
    /// zyla i dzialala") - HANDEL KARAWAN Z PRAWDZIWEJ ROZNICY CEN.
    /// Dowod [L] (log Jeffa 27.10 20-56-51, build 130, doby 109003-109024): karawana gracza sprzedala za 22 857, kupila za 17 991, "zysk ze sprzedazy -5071" (z tego ok.
    /// -5888 w dwoch pierwszych dobach - towar z wczytania wyceniony za wysoko), potem +55/d przy zoldzie 495/d; 88 karawan rodow, wyplate dostalo 13, "wartosc>kapital ale
    /// kiesa<=podloga 63"; E3-T1 23 doby: sprzedaz na stracie 332 tys. (jedzenie > 7 dob 196 tys., brak na zold 118 tys.); srebro w jukach 69% swiata (44 doby sprzedazy).
    /// Przyczyny [K]: (1) ksiega kosztu tylko na sesje (CaravanGoods), (2) T1 wlicza zold juz wydany w cene minimalna, (3) BK SellGoods sprzedaje bez kosztu,
    /// (4) BK BuyCategory wydaje do 97% kiesy na towar bez sprawdzenia zbytu, zoldu i myta (CalculateBuyValue > 0, gdy indeks < srednia x (1 - h)), (5) bramka przeplywu 282
    /// tylko dla rudy.
    /// Ten plik (reszta w CaravanGoods, CaravanBulk, MaterialOrders, CaravanSettle):
    ///  - pomiar sprzedazy karawan miastom wedlug kategorii (kazda droga sprzedazy poza rozladunkiem nadmiaru surowcow i rozwiazaniem; srednia ok. 14 dob jak 282; zapis
    ///    arm_cs284) i juki karawan wedlug kategorii (przeglad doby + zakupy i sprzedaze na zywo);
    ///  - zasady zakupu BK (postfiks CalculateBuyValue w CaravanGoods, tylko w BuyGoods): bramka przeplywu (juki > sprzedaz 14 dob x 10 dob, jedzenie x 7 - klucz
    ///    CaravanBuyByFlow), kapital obrotowy (towar po koszcie >= kapital - podloga 259) i rezerwa zoldu (kiesa po zakupie < 3 dni zoldu - klucz CaravanBuyWorkingCapital),
    ///    zysk osiagalny (indeks tu x (1 + kara) >= mediana indeksu miast placacych ponad srednia / (1 + kara) x (1 - myto) - bez zoldu, recenzja 284; jedzenie, ktore
    ///    jechaloby > 7 dob, po 3 dobach pomiaru - klucz CaravanBuyProfitCheck); kontrakt 174 i rozkaz gracza BK - pominiete (jak T1); surowce masowe - regula CaravanBulk;
    ///  - BK SellGoods nie sprzedaje ponizej kosztu (prefiks SellItemsAction.ApplyInternal, Priority.First, tylko w SellGoods: liczba sztuk przycieta do sztuk z cena po mycie
    ///    >= koszt; wyjscia na stracie T1 bez zmian - klucz CaravanBkSellAtCost);
    ///  - gabka BK przy wyjezdzie cofnieta dla kategorii z zamknietym kupnem (towar na polke, zloto do kiesy - wzor CaravanBulk);
    ///  - ksiega zysku zrealizowanego (utarg - myto - koszt) na karawane (14 dob, bez zapisu) i wedlug drogi sprzedazy; F (klucz CaravanSizeByTrade, domyslnie NIE - tylko pomiar):
    ///    karawana z zyskiem handlu na dobe < zold na dobe nie werbuje strazy.
    /// Kto placi, kto dostaje: nic nowego - tylko mniej zakupow i sprzedazy ponizej kosztu; cofniecie gabki = zwrot tej samej ceny (kasa miasta -> kiesa, towar -> polka).
    /// Linie doby: "Handel karawan (284)", "Przeplyw towarow karawan (284)", "Wielkosc karawan (284, na sucho)". Wyjatek - licznik potkniec (handel jak w BK).
    /// </summary>
    internal static class CaravanTrade284
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const int PathOther = 0, PathT1 = 1, PathBk = 2, PathBulkShort = 3, PathBulkGlut = 4, PathContract = 5, Paths = 6;
        private static readonly string[] PathNames = { "inne", "T1", "BK", "surowce masowe - brak miasta", "surowce masowe - rozladunek ponad brak", "kontrakt 174" };
        private const int FlowMinDays = 3, FlowSmooth = 14, FoodDays = 7, CashDays = 3, Ring = 14, MeasureDays = 7;

        /// <summary>Droga sprzedazy - ustawiaja wolajacy wokol SellItemsAction (T1, CaravanBulk, MaterialOrders.Deliver) i prefiks BK SellGoods; 0 - inne.</summary>
        internal static int Path;
        private static int _bkSelling;
        // poprawka 132a: sprzedaz gry (CaravansCampaignBehavior.SellGoods - wjazd i wyjazd karawany; BK wylacza tylko HourlyTickParty i DoInitialTradeRuns) - droga "inne"
        private static int _vanSelling, _vanExit, _ageWait;
        private static long _vanHeldN, _vanHeldC;
        private static bool _wVan;

        // ------------------------------------------------------------ pomiar (zapis arm_cs284)
        private static readonly Dictionary<ItemCategory, int> _soldToday = new Dictionary<ItemCategory, int>(), _soldLast = new Dictionary<ItemCategory, int>();
        private static readonly Dictionary<ItemCategory, float> _ema = new Dictionary<ItemCategory, float>();
        private static int _days;
        private static readonly Dictionary<ItemCategory, long> _held = new Dictionary<ItemCategory, long>();
        private static HashSet<ItemCategory> _food;
        private static int _impOk, _impBad; private static bool _impHad, _impLogged;

        // ------------------------------------------------------------ ksiega zysku na karawane (14 dob, bez zapisu)
        // 294: Below - kolejne doby pomiaru z wynikiem handlu ponizej zoldu (CaravanMarket294 - zwijanie stratnej karawany), BelowDay - doba ostatniego liczenia,
        // Wind - doba poczatku zwijania (int.MinValue - nie), Old - karawana sprzed pierwszej doby sesji bez wpisu w zapisie (bez historii - nie 'nowa').
        // Zapis (klucz arm_k294, tylko przy kluczu CaravanLosingWoundUp albo CaravanEntryWhenTradePays): ksiega przezywa wczytanie - zwijanie i brama nie zaczynaja
        // od zera po kazdym wczytaniu (recenzja 294: przy krotkich sesjach Jeffa wszystkie karawany byly 'nowe' przez 7 dob, a zwijanie nie ruszalo przed 28. doba).
        private sealed class Acc { public readonly long[] R = new long[Ring]; public readonly int[] D = new int[Ring]; public int First, Below, BelowDay = int.MinValue, Wind = int.MinValue; public bool Old;
            public int Born = int.MinValue; }   // 298 (tylko pomiar, bez zapisu): doba powstania - wpis z przegladu doby: doba zamknieta (First = jutro), ze sprzedazy: jej doba
        private static readonly Dictionary<MobileParty, Acc> _acc = new Dictionary<MobileParty, Acc>();
        private static string _bookPend;                                        // 294: napis arm_k294 do pierwszego uzycia ksiegi (karawany juz sa)
        private static bool _bookHad, _dayRan;                                  // klucz w zapisie; pierwsza doba sesji juz byla
        private static int _bookOk, _bookGone, _bookBad, _rWind;               // wpisy wczytane / karawan juz nie ma / nieczytelne; kupno BK wstrzymane zwijaniem

        // ------------------------------------------------------------ liczniki doby
        private static readonly long[] _pN = new long[Paths], _pG = new long[Paths], _pT = new long[Paths], _pC = new long[Paths], _pR = new long[Paths];
        private static readonly long[] _pLossN = new long[Paths], _pLossG = new long[Paths], _pLossC = new long[Paths], _plG = new long[Paths], _plR = new long[Paths];
        private static int _rGate, _rCap, _rWage, _rGain, _rFood, _skipExempt, _buyVisits, _bkExit, _bulkHeldGlut, _stumbles, _stumbleSess, _fCalls, _fWould, _fSkip;
        private static long _bkHeldN, _bkHeldC, _bulkHeldN, _spongeN, _spongeG, _ticks;
        // recenzja 284: wizyty z podloga >= kapital (regula kapitalu pominieta), przeliczenie jukow po wczytaniu przed pierwsza doba, sztuki wstrzymane zdjete z dostaw P1
        private static int _noRoom, _heldLazy; private static long _p1Phantom;
        private static bool _heldReady;
        private static readonly Dictionary<EquipmentElement, int> _bkWithheld = new Dictionary<EquipmentElement, int>();
        private static MobileParty _bkWithheldMp;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static bool _wSell, _wGuard, _wLeft, _wSize;

        // ------------------------------------------------------------ 287: popyt rynkow (klucz CaravanFlowByMarketDemand)
        // Dowod (autotest 135b = 286b, 90 dob 109025-109114 na kopii zapisu Jeffa, Armoury-2026-10-28_17-29-45.log; 134b = WGRANE 134 tak samo): bramka przeplywu
        // mierzyla zbyt karawan ich WLASNA sprzedaza miastom (Ema). Od doby 109031 kupno stalo w 35-36 z 36 kategorii do konca biegu (134b: 33 z 36), 'kupno BK
        // odrzucone - przesyt' 1 355-1 885 wizyt x kategorii na dobe, 'Towary' - karawany kupily 0 srebra / wina / soli / futer. Karawany tylko wyprzedawaly juki
        // z wczytania (56 979 -> 9 371 szt.), sprzedaz 14 dob malala razem z nimi (1 818 -> 151 szt./d), stosunek juki / sprzedaz rosl 24 -> 62 dob - bramka nie
        // mogla sie otworzyc (miara zalezy od tego, co bramka blokuje). Rynek jej nie widzial: wino w jukach 172 szt. = 0.2 doby zuzycia swiata (620/d), a 143 doby
        // 'sprzedazy karawan' (1.2/d); srebro 1 501 = 5.6 doby zuzycia (267.6/d), 120 dob 'sprzedazy karawan'. Miara 287: ile kupuja rynki miast - popyt targu
        // gry (ItemData.Demand, ten, z ktorego idzie cena) zamieniony na sztuki tak, jak gra zamienia go na zakupy mieszczan: budzet doby
        // SettlementEconomyModel.CalculateDailySettlementBudgetForItemCategory (popyt x indeks^0.3) / cena sztuki (wartosc na polce x indeks), suma 97 miast.
        // Bramka i reguly, ktore pytaja o dni zbytu (jedzenie > 7 dob, przesyt gleboki 286b) - na max(sprzedaz karawan, popyt rynkow). Zysk osiagalny (regula 4),
        // kapital obrotowy, rezerwa zoldu, 'nie ponizej kosztu' - bez zmian. Pamiec doby (kategoria liczona przy pierwszym pytaniu w dobie), nic w zapisie.
        private static readonly Dictionary<ItemCategory, float> _mkt = new Dictionary<ItemCategory, float>();
        private static int _mktDay = int.MinValue;
        private static Dictionary<ItemCategory, float> _unit;          // wartosc sztuki kategorii na polce (srednia ShelfWorth towarow handlowych) - raz na dobe
        private static int _mktStum;

        // ------------------------------------------------------------ 294b: glebokosc rynku (klucz CaravanBuyByMarketDepth), bramka jedzenia na towar handlowy
        // (klucz CaravanFoodGateTradeStock). Dowod (autotest 296a = 136 + 290 + 291..295, 90 dob na kopii zapisu Jeffa): zakupy (regula 4) i 'czekanie na rynek
        // z zyskiem' (CaravanGoods.AgeMarket) liczyly sie z OFERTY - mediany indeksu miast drozszych niz srednia - bez spadku ceny po pierwszej sztuce i bez towaru,
        // ktory inne karawany juz wioza do tych samych miast (bawelna 9.10 -> 1.62 w jednej wizycie; wino - 71% wina swiata w jukach, a 'CZYNNE' w 287). Juki po
        // koszcie ok. 11.4 tys. na karawane przy kapitale 10 tys. (kupno BK 'kapital w towarze' 517/d), obrot 48-63 dob, zold 0.92% jukow na dobe - zold pokrywa
        // 0.325 karawan. Miasto odniesienia (DepthRef) - mediana miast placacych co najmniej srednia (ten sam zbior co SellIdx), migawka jego danych targu z pierwszego
        // pytania doby; cena k-tej sztuki tam (DepthNet - ta sama formula co CaravanGoods.PriceAt / MaxAtCost: kara handlu, polka rosnie o ShelfWorth na sztuke)
        // po mycie tego miasta; k = moja sztuka w kolejce za udzialem jukow innych karawan na jedno miasto drozsze, minus to, co to miasto zje w dobie drogi (QueueK).
        private sealed class DepthRef { public Town Ref; public float Idx, InStore, Supply, Demand; public int N; }
        private static readonly Dictionary<ItemCategory, DepthRef> _depth = new Dictionary<ItemCategory, DepthRef>();
        private static HashSet<ItemCategory> _perish;                    // kategorie z jedzeniem psujacym sie (CaravanMarket294.Perishable) - raz na sesje
        private static Dictionary<ItemCategory, ItemObject> _rep;        // pierwszy towar handlowy kategorii (linia 294b) - raz na sesje
        private static int _nTowns, _nTownsDay = int.MinValue;
        private static readonly Dictionary<ItemObject, int> _pantryTmp = new Dictionary<ItemObject, int>();
        private static long _pantryN;                                    // spizarnia eskorty poza bramka (ostatni przeglad jukow), szt.
        // liczniki doby (linia 294b)
        private static int _dEval, _dStop, _dOld, _dOpen, _dNone, _dAgeWait, _dAgeOldWait, _dStum, _dStumSess;
        private static long _dSpongeN, _dLossN, _dLossG, _dLossC, _dBk5N, _dBk5C;
        private static long _dLossOldN, _dHeldT1, _dHeldBk;              // 294b recenzja: sprzedane, choc stara regula czekalaby; wstrzymane do miasta drozszego (T1 / BK i gra)

        /// <summary>294b: marza przewoznika - ta sama co wozy KS-H (KsFoodCore.Margin: indeks celu >= 1.03 x koszt na miejscu); jedna regula przewozu.</summary>
        internal const double CarrierMargin = 1.0 + KsFoodCore.Margin;

        /// <summary>294b R1: cena sztuki wedlug glebokosci rynku (klucz CaravanBuyByMarketDepth przy regule 'zysk osiagalny' CaravanBuyProfitCheck).</summary>
        internal static bool DepthOn { get { var s = Settings.Current; return s != null && s.CaravanBuyByMarketDepth && s.CaravanBuyProfitCheck; } }

        /// <summary>294b R2: spizarnia eskorty poza bramka (klucz CaravanFoodGateTradeStock przy CaravanFoodBuyOwnSales).</summary>
        private static bool R2On(Settings s) { return s != null && s.CaravanFoodGateTradeStock && s.CaravanFoodBuyOwnSales; }

        /// <summary>294b R2: zbyt i dni jedzenia wedlug rodzaju (psujace / trwale jak towar) - przy R2 i mierze popytu rynkow 287.</summary>
        private static bool R2Full(Settings s) { return R2On(s) && s.CaravanFlowByMarketDemand; }

        /// <summary>298 (tylko pomiar): blad pomiaru linii 298 - wlasny licznik (potkniecia 298), bez licznikow 284 i 294b, ktore oceniaja reguly handlu.</summary>
        private static void M298Stumble(string where, Exception e)
        {
            _m298Stum++; _m298StumSess++;
            try { if (_err.Add("Line298" + where)) Log.Error("CaravanTrade284.Line298." + where, e); } catch { }
        }

        private static void DStumble(string where, Exception e)
        {
            // 298 (recenzja): pomocnicy glebokosci wolani z pomiaru linii 298 (IdleWhy) licza bledy w potknieciach 298, nie 294b (K294-8 ocenia regule kupna)
            if (_in298 > 0) { M298Stumble(where, e); return; }
            _dStum++; _dStumSess++;
            try { if (_err.Add("Depth" + where)) Log.Error("CaravanTrade284.Depth" + where, e); } catch { }
        }

        internal static void Reset()
        {
            _mkt.Clear(); _mktDay = int.MinValue; _unit = null; _mktStum = 0;   // 287
            _soldToday.Clear(); _soldLast.Clear(); _ema.Clear(); _days = 0; _held.Clear(); _heldReady = false; _food = null;
            _bkWithheld.Clear(); _bkWithheldMp = null;
            _impOk = _impBad = 0; _impHad = _impLogged = false;
            _acc.Clear(); _sellIdx.Clear(); _sellDay = -1;
            _depth.Clear(); _perish = null; _rep = null; _nTowns = 0; _nTownsDay = int.MinValue; _pantryN = 0; _dStumSess = 0;   // 294b
            _h1Bk = _h1Van = 0; _pk.Mp = null; _pk.PeekTown = null; _pk.PeekK.Clear(); _pk.PeekNet.Clear();   // 294c
            _bookPend = null; _bookHad = _dayRan = false; _bookOk = _bookGone = _bookBad = 0;   // 294
            Path = 0; _bkSelling = 0; _vanSelling = 0; _b.Mp = null;
            _stumbleSess = 0; _err.Clear();
            _g285N = _g285C = 0; _g285Acts = 0; _g285By.Clear();   // 285
            _day0 = int.MinValue; _m298Stum = _m298StumSess = 0;   // 298
            ZeroDay();
        }

        private static void ZeroDay()
        {
            Array.Clear(_pN, 0, Paths); Array.Clear(_pG, 0, Paths); Array.Clear(_pT, 0, Paths); Array.Clear(_pC, 0, Paths); Array.Clear(_pR, 0, Paths);
            Array.Clear(_pLossN, 0, Paths); Array.Clear(_pLossG, 0, Paths); Array.Clear(_pLossC, 0, Paths); Array.Clear(_plG, 0, Paths); Array.Clear(_plR, 0, Paths);
            _rGate = _rCap = _rWage = _rGain = _rFood = _skipExempt = _buyVisits = _bkExit = _bulkHeldGlut = _stumbles = _fCalls = _fWould = _fSkip = 0;
            _bkHeldN = _bkHeldC = _bulkHeldN = _spongeN = _spongeG = _ticks = 0;
            _noRoom = _heldLazy = 0; _p1Phantom = 0;
            _vanExit = _ageWait = 0; _vanHeldN = _vanHeldC = 0;
            _rWind = 0;   // 294
            _dEval = _dStop = _dOld = _dOpen = _dNone = _dAgeWait = _dAgeOldWait = _dStum = 0;   // 294b
            _dSpongeN = _dLossN = _dLossG = _dLossC = _dBk5N = _dBk5C = 0;
            _dLossOldN = _dHeldT1 = _dHeldBk = 0;
            _fLossN = _fLossG = _fLossC = _fsLossN = _fsLossG = _fsLossC = _fBuyN = _fBuyKs = 0; _fBuyU = 0.0;   // 298
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumbleSess++;
            try { if (_err.Add(where)) Log.Error("CaravanTrade284." + where, e); } catch { }
        }

        // ------------------------------------------------------------ kategorie
        /// <summary>Kategoria pod zasadami 284: towar handlowy, nie zwierzeta i nie juczne; surowce masowe z tabeli CaravanBulk - jego regula (282).</summary>
        internal static bool Tracked(ItemCategory c)
        {
            return c != null && c.IsTradeGood && !c.IsAnimal && c != DefaultItemCategories.PackAnimal && !CaravanBulk.OwnsCategory(c);
        }

        /// <summary>Kategoria jedzenia (ktorys przedmiot tej kategorii ma IsFood) - liczone raz na sesje.</summary>
        internal static bool IsFoodCat(ItemCategory c)
        {
            if (c == null) return false;
            if (_food == null)
            {
                var f = new HashSet<ItemCategory>();
                try
                {
                    var om = MBObjectManager.Instance;
                    var all = om != null ? om.GetObjectTypeList<ItemObject>() : null;
                    if (all != null) foreach (var it in all) if (it != null && it.IsFood && it.ItemCategory != null) f.Add(it.ItemCategory);
                }
                catch (Exception e) { Stumble("IsFoodCat", e); }
                _food = f;
            }
            return _food.Contains(c);
        }

        private static int DaysOf(ItemCategory c)
        {
            var s = Settings.Current;
            // 294b (R2): jedzenie, ktore sie nie psuje (294 A - CaravanMarket294.Durable), jak towar - 10 dob zbytu jak kazdy towar trwaly
            bool food7 = IsFoodCat(c) && !(R2Full(s) && !SpoilCat(c));
            return food7 ? FoodDays : Math.Max(1, s != null ? s.CaravanBulkStockDays : 10);
        }

        /// <summary>294b: kategoria z jedzeniem psujacym sie (ktorys towar handlowy kategorii - CaravanMarket294.Perishable) - raz na sesje.</summary>
        internal static bool PerishableCat(ItemCategory c)
        {
            if (c == null) return false;
            if (_perish == null)
            {
                var f = new HashSet<ItemCategory>();
                try
                {
                    var om = MBObjectManager.Instance;
                    var all = om != null ? om.GetObjectTypeList<ItemObject>() : null;
                    if (all != null) foreach (var it in all) if (it != null && it.IsTradeGood && it.ItemCategory != null && CaravanMarket294.Perishable(it)) f.Add(it.ItemCategory);
                }
                catch (Exception e) { DStumble("Perish", e); }
                _perish = f;
            }
            return _perish.Contains(c);
        }

        /// <summary>294b: jedzenie, ktore sie psuje w jukach (regula 7 dob): klucz 294 A NIE - kazde jedzenie (jak 290); TAK - kategoria z jedzeniem psujacym sie.</summary>
        private static bool SpoilCat(ItemCategory c)
        {
            return IsFoodCat(c) && (!CaravanMarket294.FoodKeepsOn || PerishableCat(c));
        }

        /// <summary>294b: miasta z danymi targu (mianownik 'srednie miasto' - popyt rynkow / miasta) - raz na dobe.</summary>
        private static int NTowns()
        {
            int d = (int)CampaignTime.Now.ToDays;
            if (d == _nTownsDay && _nTowns > 0) return _nTowns;
            int n = 0;
            foreach (var t in Town.AllTowns) if (t != null && t.IsTown && t.MarketData != null) n++;
            _nTowns = Math.Max(1, n); _nTownsDay = d;
            return _nTowns;
        }

        private static float Ema(ItemCategory c) { float e; return _ema.TryGetValue(c, out e) ? e : 0f; }
        private static long Held(ItemCategory c) { long h; return _held.TryGetValue(c, out h) ? h : 0; }
        private static float CapOld(ItemCategory c) { return Ema(c) * DaysOf(c); }
        private static float Cap(ItemCategory c) { return Flow(c) * DaysOf(c); }

        /// <summary>287: zbyt doby kategorii, z ktorego licza sie dni (bramka, jedzenie > 7 dob, przesyt gleboki): przy kluczu CaravanFlowByMarketDemand
        /// max(sprzedaz karawan miastom, popyt rynkow miast w sztukach); bez klucza - sama sprzedaz karawan (jak 284).</summary>
        private static float Flow(ItemCategory c)
        {
            float e = Ema(c);
            var s = Settings.Current;
            if (s == null || !s.CaravanFlowByMarketDemand) return e;
            // 290 (klucz CaravanFoodBuyOwnSales): jedzenie psuje sie w jukach po 7 dobach (CaravanGoods FoodAgeDays - T1 sprzedaje je wtedy na stracie), wiec
            // liczy sie to, co karawany SAME zdolaja sprzedac miastom w tym czasie, nie caly popyt miast (ten bierze glownie dowoz wsi i wozy KS-H). Autotest 136a,
            // doba 109133: chleb juki 4 098 / sprzedaz karawan 158 szt./d = 26 dob, a prog 287 (popyt rynkow x 7) 12 283 - kupno czynne; ryby 18 dob, zboze 23,
            // winogrona 23; T1 na stracie 'jedzenie psuje sie' 152-216 szt./d (89% sprzedazy na stracie; 134b bez 287: 22-63 szt./d). Bramka (regula 1), regula 5
            // zakupu, gabka - ta sama miara. Towar trwaly - jak 287 (petla 'sprzedaz zalezy od tego, co bramka blokuje' dotyczy towaru, ktory czeka 30 dob; jedzenie
            // schodzi tez przez jedzenie eskorty z jukow 288 - bramka otwiera sie, gdy juki spadna do 7 dob sprzedazy).
            if (s.CaravanFoodBuyOwnSales && IsFoodCat(c))
            {
                // 294b (klucz CaravanFoodGateTradeStock): psujace - wlasna sprzedaz karawan, ale nie mniej niz zakupy doby jednego sredniego miasta (popyt rynkow /
                // miasta z targiem) - bramka moze sie otworzyc, gdy sprzedaz karawan zgasla (296a: 873 -> 29 szt./d przy zamknietej bramce); trwale - jak towar (287)
                // 298 L1 (klucz CaravanFoodGateByPayingTowns przy DepthOn): psujace - prog swiata = N miast placacych (DepthRef.N) x to, co jedno z nich zje w zmierzonym
                // odcinku (LegEat), czyli Flow = popyt rynkow / miasta x N x min(odcinek, 7) / 7 (Cap = Flow x 7 = N x LegEat) - ten sam punkt, od ktorego test glebokosci
                // 294b widzi kolejke (k > 1); dolny prog 294b (jedno srednie miasto) zostaje (bramka tylko w gore). Bez miary (brak miasta odniesienia, odcinek 0) - jak 294b.
                if (s.CaravanFoodGateTradeStock)
                {
                    if (!SpoilCat(c)) return Math.Max(e, Mkt(c));
                    float floor = Mkt(c) / NTowns();
                    int n; double leg;
                    double l1 = SpoilLegFlow(c, out n, out leg);
                    return Math.Max(e, l1 > floor ? (float)l1 : floor);
                }
                return e;
            }
            return Math.Max(e, Mkt(c));
        }

        /// <summary>298 L1: zbyt doby psujacych z miast placacych - popyt rynkow 287 / miasta z targiem x N miast placacych co najmniej srednia (DepthRef.N, ten sam zbior
        /// co SellIdx i test glebokosci) x min(zmierzony odcinek Stage8World.LegDays, 7) / 7; -1 - klucz NIE, DepthOn NIE albo bez miary (n, leg - wejscia, do linii 290).
        /// Wola tylko DepthOf (SellIdx: indeksy miast, AvgIndex), Mkt i NTowns - zadne z nich nie wola Flow, Cap ani GateShut (bez rekurencji).</summary>
        private static double SpoilLegFlow(ItemCategory c, out int n, out double leg)
        {
            n = 0; leg = 0.0;
            var s = Settings.Current;
            if (s == null || !s.CaravanFoodGateByPayingTowns || !DepthOn || c == null) return -1.0;
            var r = DepthOf(c);
            double lg = Stage8World.LegDays;
            if (r == null || r.N <= 0 || !(lg > 0.0) || double.IsInfinity(lg)) return -1.0;
            n = r.N; leg = lg;
            return Mkt(c) / (double)NTowns() * n * Math.Min(lg, (double)FoodDays) / FoodDays;
        }

        /// <summary>287: wartosc sztuki kategorii na polce (jednostka danych rynku - HistoricalPrices.ShelfWorth), srednia towarow handlowych kategorii; raz na dobe (z pamiecia Mkt).</summary>
        private static float Unit(ItemCategory c)
        {
            if (_unit == null)
            {
                var u = new Dictionary<ItemCategory, float>();
                var n = new Dictionary<ItemCategory, int>();
                try
                {
                    var om = MBObjectManager.Instance;
                    var all = om != null ? om.GetObjectTypeList<ItemObject>() : null;
                    if (all != null)
                        foreach (var it in all)
                        {
                            if (it == null || !it.IsTradeGood || it.HasHorseComponent || it.ItemCategory == null) continue;
                            int w = HistoricalPrices.ShelfWorth(it);
                            if (w <= 0) continue;
                            float x; u.TryGetValue(it.ItemCategory, out x); u[it.ItemCategory] = x + w;
                            int k; n.TryGetValue(it.ItemCategory, out k); n[it.ItemCategory] = k + 1;
                        }
                    foreach (var kv in n) u[kv.Key] = u[kv.Key] / Math.Max(1, kv.Value);
                }
                catch (Exception e) { _mktStum++; Stumble("Unit", e); }
                _unit = u;
            }
            float v;
            return c != null && _unit.TryGetValue(c, out v) ? v : 0f;
        }

        /// <summary>287: popyt rynkow miast na kategorie w sztukach na dobe - budzet zakupow doby z modelu gry (CalculateDailySettlementBudgetForItemCategory
        /// z popytu targu ItemData.Demand) / cena sztuki (wartosc na polce x indeks miasta), suma miast. Pamiec doby.</summary>
        internal static float Mkt(ItemCategory c)
        {
            if (c == null) return 0f;
            int d = (int)CampaignTime.Now.ToDays;
            if (d != _mktDay) { _mkt.Clear(); _mktDay = d; _unit = null; }   // wartosc sztuki tez od nowa (przeliczenie cen historycznych przy starcie sesji)
            float v;
            if (_mkt.TryGetValue(c, out v)) return v;
            double sum = 0;
            try
            {
                float w = Unit(c);
                var cg = Campaign.Current;
                var em = cg != null && cg.Models != null ? cg.Models.SettlementEconomyModel : null;
                if (w > 0f)
                    foreach (var t in Town.AllTowns)
                    {
                        if (t == null || !t.IsTown || t.MarketData == null) continue;
                        float dem = t.MarketData.GetCategoryData(c).Demand;
                        if (!(dem > 0f)) continue;
                        float idx = Math.Max(0.1f, t.GetItemCategoryPriceIndex(c));
                        float bud = em != null ? em.CalculateDailySettlementBudgetForItemCategory(t, dem, c) : dem * (float)Math.Pow(idx, 0.3);
                        if (bud > 0f && !float.IsNaN(bud) && !float.IsInfinity(bud)) sum += bud / (w * (double)idx);
                    }
            }
            catch (Exception e) { _mktStum++; Stumble("Mkt", e); sum = 0; }   // bez miary rynku - bramka jak 284 (sama sprzedaz karawan)
            v = (float)sum;
            _mkt[c] = v;
            return v;
        }

        /// <summary>Bramka przeplywu (klucz CaravanBuyByFlow): po FlowMinDays dobach pomiaru wszystkie karawany razem wioza wiecej niz sprzedaja miastom w 10 dob (jedzenie 7).</summary>
        internal static bool GateShut(ItemCategory c)
        {
            var s = Settings.Current;
            if (s == null || !s.CaravanBuyByFlow || _days < FlowMinDays || !Tracked(c)) return false;
            EnsureHeld();
            return Held(c) > Cap(c);
        }

        /// <summary>Poprawka 135a (286b, CaravanGoods.AgedGlut): przesyt gleboki - bramka zamknieta i juki karawan swiata wiecej niz `days` dob sprzedazy miastom
        /// (ta sama miara co bramka: juki / srednia sprzedaz doby). Przy days = wiek starego towaru (30 dob) sztuka kupiona dzis zestarzeje sie, zanim stos zejdzie
        /// sprzedaza swiata - tylko taki stos nie czeka na rynek z zyskiem. Sprzedaz zmierzona 0 przy towarze w jukach - gleboki.</summary>
        internal static bool GlutDeep(ItemCategory c, int days)
        {
            if (!GateShut(c)) return false;
            float e = Flow(c);   // 287: ta sama miara zbytu co bramka (przy kluczu CaravanFlowByMarketDemand - z popytem rynkow)
            long h = Held(c);
            return e > 0.01f ? h > (double)e * Math.Max(1, days) : h > 0;
        }

        /// <summary>Recenzja 284 (drobna 1): po wczytaniu zapisu _held jest pusty az do pierwszej doby (pomiar _days i _ema wraca z zapisu, juki nie) - bramka bylaby
        /// otwarta dla kazdego towaru. Pierwsze uzycie po wczytaniu przelicza juki ze wszystkich karawan (ten sam przeglad co Daily); do tej chwili NoteBuy/NoteSale nie
        /// zmieniaja _held (przeglad i tak widzi juki po tych ruchach).</summary>
        private static void EnsureHeld()
        {
            if (_heldReady) return;
            var cg = Campaign.Current;
            if (cg == null || !cg.GameStarted) return;
            _heldReady = true;
            _held.Clear(); _pantryN = 0;
            long n = 0;
            foreach (var mp in MobileParty.AllCaravanParties)
                if (mp != null && mp.IsActive && mp.ItemRoster != null) n += ScanHeld(mp, null);
            _heldLazy++;
            Log.Info("Karawany przeplyw towarow (284): juki karawan przeliczone po wczytaniu przed pierwsza doba - kategorii " + _held.Count + ", sztuk " + n + ".");
        }

        /// <summary>Juki jednej karawany wedlug kategorii do _held (i koszt, gdy cost != null) - przeglad doby i EnsureHeld. 294b (klucz CaravanFoodGateTradeStock):
        /// spizarnia eskorty (PantryOf - sztuki, ktorych T1 nie sprzedaje) poza _held - bramka i dni licza tylko towar handlowy; koszt - wszystkie sztuki (jak dotad).</summary>
        private static long ScanHeld(MobileParty mp, Dictionary<ItemCategory, double> cost)
        {
            var r = mp.ItemRoster;
            HashSet<ItemObject> seen = null;
            long n = 0;
            var pan = R2On(Settings.Current) ? PantryOf(mp) : null;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0 || !Tracked(it.ItemCategory)) continue;
                if (seen == null) seen = new HashSet<ItemObject>();
                if (!seen.Add(it)) continue;
                int have = r.GetItemNumber(it);
                var c = it.ItemCategory;
                int keep = 0;
                if (pan != null && pan.TryGetValue(it, out keep)) { keep = Math.Max(0, Math.Min(keep, have)); _pantryN += keep; }
                _held[c] = Held(c) + have - keep; n += have - keep;
                if (cost != null) { double c0; cost.TryGetValue(c, out c0); cost[c] = c0 + CaravanGoods.HeldCost(mp, it, have); }
            }
            return n;
        }

        /// <summary>294b: spizarnia eskorty karawany - CaravanGoods.PantryUnits sztuk jedzenia (ta sama miara, ktora T1 zatrzymuje), przydzielona najtanszym najpierw
        /// (kolejnosc jedzenia eskorty 288 - CaravanEscort.Eat). Wspolna pamiec _pantryTmp (watek glowny); null - brak spizarni.</summary>
        private static Dictionary<ItemObject, int> PantryOf(MobileParty mp)
        {
            _pantryTmp.Clear();
            var r = mp != null ? mp.ItemRoster : null;
            if (r == null) return null;
            int left;
            try { left = CaravanGoods.PantryUnits(mp); }
            catch (Exception e) { DStumble("Pantry", e); return null; }
            if (left <= 0) return null;
            List<ItemObject> foods = null;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0 || !it.IsFood || it.NotMerchandise) continue;
                if (foods == null) foods = new List<ItemObject>();
                if (!foods.Contains(it)) foods.Add(it);
            }
            if (foods == null) return null;
            foods.Sort((x, y) => x.Value.CompareTo(y.Value));
            foreach (var it in foods)
            {
                if (left <= 0) break;
                int k = Math.Min(left, r.GetItemNumber(it));
                if (k > 0) { _pantryTmp[it] = k; left -= k; }
            }
            return _pantryTmp.Count > 0 ? _pantryTmp : null;
        }

        /// <summary>294b (recenzja): sztuki spizarni eskorty w kategorii c przy jukach `units` (gabka - juki sprzed zakupu BK); ten sam przydzial co PantryOf.</summary>
        private static int PantryOfCat(MobileParty mp, Dictionary<ItemObject, int> units, ItemCategory c)
        {
            int left;
            try { left = CaravanGoods.PantryUnits(mp); }
            catch (Exception e) { DStumble("Pantry", e); return 0; }
            if (left <= 0 || units == null) return 0;
            var foods = new List<KeyValuePair<ItemObject, int>>();
            foreach (var kv in units) if (kv.Key != null && kv.Value > 0 && kv.Key.IsFood && !kv.Key.NotMerchandise) foods.Add(kv);
            foods.Sort((x, y) => x.Key.Value.CompareTo(y.Key.Value));
            int inCat = 0;
            foreach (var kv in foods)
            {
                if (left <= 0) break;
                int k = Math.Min(left, kv.Value);
                if (kv.Key.ItemCategory == c) inCat += k;
                left -= k;
            }
            return inCat;
        }

        /// <summary>294b: sztuki kategorii w jukach karawany, ktore ida na sprzedaz (bez spizarni eskorty przy kluczu CaravanFoodGateTradeStock).</summary>
        private static int TradeUnits(MobileParty mp, ItemCategory c)
        {
            var r = mp != null ? mp.ItemRoster : null;
            if (r == null || c == null) return 0;
            int n = 0;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it != null && el.Amount > 0 && it.ItemCategory == c) n += el.Amount;
            }
            if (n > 0 && IsFoodCat(c) && R2On(Settings.Current))
            {
                var pan = PantryOf(mp);
                if (pan != null) foreach (var kv in pan) if (kv.Key.ItemCategory == c) n -= kv.Value;
            }
            return Math.Max(0, n);
        }

        // ------------------------------------------------------------ 294b: glebokosc rynku
        private static DepthRef DepthOf(ItemCategory c)
        {
            if (c == null) return null;
            SellIdx(c);
            DepthRef r;
            return _depth.TryGetValue(c, out r) ? r : null;
        }

        /// <summary>294b: ile jedno srednie miasto zje towaru kategorii w dobie drogi - popyt rynkow 287 (szt./d, suma miast) / miasta z targiem x zmierzony
        /// odcinek karawan (Stage8World.LegDays; 0 - bez pomiaru).</summary>
        private static double LegEat(ItemCategory c)
        {
            double leg = Stage8World.LegDays;
            if (!(leg > 0.0) || double.IsInfinity(leg)) return 0.0;
            return Mkt(c) / (double)NTowns() * leg;
        }

        /// <summary>294b: k - miejsce mojej sztuki w kolejce na polce typowego drozszego miasta: ownAfter (moje sztuki z ta wlacznie) + udzial jukow innych karawan
        /// na jedno miasto drozsze (ceil((juki swiata - moje) / N miast drozszych)) - to, co to miasto zje w dobie drogi (ceil(LegEat)); najmniej 1.</summary>
        internal static int QueueK(MobileParty mp, ItemCategory c, int ownAfter)
        {
            int own;
            try { own = mp != null ? TradeUnits(mp, c) : 0; }
            catch (Exception e) { DStumble("Queue", e); return Math.Max(1, ownAfter); }
            return QueueKOwn(c, own, ownAfter);
        }

        /// <summary>294b (recenzja): QueueK z jawna liczba moich sztuk handlowych w jukach swiata (Held) - gabka liczy je sprzed zakupu BK (BK kupuje AddToCounts,
        /// bez NoteBuy, wiec Held nie ma sztuk gabki).</summary>
        private static int QueueKOwn(ItemCategory c, int ownTrade, int ownAfter)
        {
            try
            {
                var r = DepthOf(c);
                int n = r != null ? Math.Max(1, r.N) : 1;
                EnsureHeld();
                long others = Math.Max(0L, Held(c) - Math.Max(0, ownTrade));
                double k = ownAfter + Math.Ceiling(others / (double)n) - Math.Ceiling(LegEat(c));
                return (int)Math.Max(1.0, Math.Min(1000000.0, k));
            }
            catch (Exception e) { DStumble("Queue", e); return Math.Max(1, ownAfter); }
        }

        /// <summary>294b: cena k-tej sztuki w miescie odniesienia (mediana miast placacych co najmniej srednia) po mycie tego miasta - model cen gry (kara handlu tej
        /// karawany), polka z migawki doby + (k - 1) x ShelfWorth (CaravanGoods.PriceAt). NaN - brak miary (bez miasta odniesienia albo modelu).</summary>
        internal static double DepthNet(MobileParty mp, EquipmentElement el, int k)
        {
            try
            {
                var it = el.Item;
                if (it == null || it.ItemCategory == null) return double.NaN;
                var r = DepthOf(it.ItemCategory);
                var cg = Campaign.Current;
                var model = cg != null && cg.Models != null ? cg.Models.TradeItemPriceFactorModel : null;
                if (r == null || r.Ref == null || r.Ref.Settlement == null || model == null) return double.NaN;
                int wv = Math.Max(1, HistoricalPrices.ShelfWorth(it));
                int p = model.GetPrice(el, mp, null, true, r.InStore + (Math.Max(1, k) - 1) * (float)wv, r.Supply, r.Demand);
                return p * (1.0 - TownToll.CaravanRateFor(mp, r.Ref.Settlement));
            }
            catch (Exception e) { DStumble("Net", e); return double.NaN; }
        }

        /// <summary>294b (CaravanGoods.AgeMarket): cena starego towaru w miescie odniesienia po mycie dla OSTATNIEJ sztuki, ktora karawana postawi na jednej polce
        /// drozszego miasta - polowa jej sztuk kategorii (T1 sprzedaje w jednym miescie najwyzej polowe stosu, CaravanGoods.Sell maxSell), w kolejce za udzialem
        /// jukow innych karawan (recenzja 294b: ownAfter 1 kazal czekac calemu stosowi, gdy rynek bral tylko pierwsza sztuke). NaN - brak miary.</summary>
        internal static double DepthNetAged(MobileParty mp, ItemObject it)
        {
            if (it == null || it.ItemCategory == null) return double.NaN;
            int own;
            try { own = TradeUnits(mp, it.ItemCategory); }
            catch (Exception e) { DStumble("Aged", e); return double.NaN; }
            return DepthNet(mp, new EquipmentElement(it), QueueKOwn(it.ItemCategory, own, Math.Max(1, (own + 1) / 2)));
        }

        /// <summary>294b: pierwszy towar handlowy kategorii na polce miasta (jak PenOf - wzor ceny kupna); null - brak.</summary>
        private static ItemObject RepOnShelf(Town town, ItemCategory cat)
        {
            var r = town != null && town.Owner != null ? town.Owner.ItemRoster : null;
            if (r == null) return null;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it != null && el.Amount > 0 && it.ItemCategory == cat && it.IsTradeGood && !it.HasHorseComponent) return it;
            }
            return null;
        }

        /// <summary>294b (regula 4 zakupu BK): 1 - kupno stoi (cena sztuki, ktora przywiozlabym, w kolejce: DepthNet(QueueK(moje + 1)) < 1.03 x cena kupna tu),
        /// 0 - czynne, -1 - bez miary (brak towaru na polce, miasta odniesienia, ceny) - wtedy stara regula ofert.</summary>
        private static int DepthBuy(MobileParty mp, ItemCategory cat, Town town)
        {
            try
            {
                var rep = RepOnShelf(town, cat);
                if (rep == null) return -1;
                var el = new EquipmentElement(rep);
                int buy = town.GetItemPrice(el, mp, false);
                if (buy <= 0) return -1;
                double net = DepthNet(mp, el, QueueK(mp, cat, TradeUnits(mp, cat) + 1));
                if (double.IsNaN(net)) return -1;
                return net < CarrierMargin * buy ? 1 : 0;
            }
            catch (Exception e) { DStumble("Buy", e); return -1; }
        }

        /// <summary>294b (gabka BK przy wyjezdzie): towar, ktory gabka dokupila, a ktorego pierwsza sztuka nie przechodzi testu glebokosci (k = moje sprzed gabki + 1).</summary>
        private static bool SpongeDepth(MobileParty mp, ItemObject it, Town town, Dictionary<ItemObject, int> before)
        {
            if (!DepthOn || it == null || !Tracked(it.ItemCategory)) return false;
            try
            {
                var cat = it.ItemCategory;
                int own = 0;
                foreach (var kv in before) if (kv.Key != null && kv.Key.ItemCategory == cat) own += kv.Value;
                // recenzja 294b: spizarnia eskorty poza towarem handlowym takze tu (jak TradeUnits) - przydzial najtanszym najpierw na jukach sprzed gabki
                if (own > 0 && IsFoodCat(cat) && R2On(Settings.Current)) own = Math.Max(0, own - PantryOfCat(mp, before, cat));
                var el = new EquipmentElement(it);
                int buy = town.GetItemPrice(el, mp, false);
                if (buy <= 0) return false;
                double net = DepthNet(mp, el, QueueKOwn(cat, own, own + 1));
                return !double.IsNaN(net) && net < CarrierMargin * buy;
            }
            catch (Exception e) { DStumble("Sponge", e); return false; }
        }

        /// <summary>294b (CaravanGoods.AgeMarket przy kluczu): ocena starego towaru - czeka wedlug glebokosci (okDepth) i co mowila stara regula ofert (okOld).</summary>
        internal static void NoteDepthAge(bool okDepth, bool okOld) { if (okDepth) _dAgeWait++; if (okOld) _dAgeOldWait++; }

        /// <summary>294b (T1): sprzedaz na stracie regula 5, gdy werdykt 'bez rynku' wydal test glebokosci (CaravanGoods.AgeMarket) - sztuki, utarg, koszt; old - stara
        /// regula ofert kazalaby czekac (odpis przypisany 294b).</summary>
        internal static void NoteDepthLoss(int n, long gross, long cost, bool old) { if (n > 0) { _dLossN += n; _dLossG += gross; _dLossC += cost; if (old) _dLossOldN += n; } }

        /// <summary>294b (recenzja, T1): sztuki kroku na stracie wstrzymane, bo to miasto placi mniej niz cena tej sztuki w miescie odniesienia po mycie / 1.03.</summary>
        internal static void NoteDepthHeldT1(int n) { if (n > 0) _dHeldT1 += n; }

        /// <summary>294b (recenzja): najnizsza cena po mycie starego towaru bez rynku z glebokoscia - min(koszt, cena tej sztuki w miescie odniesienia po mycie / marza
        /// przewoznika 1.03). Sprzedaz taniej niz miasto drozsze o dobe drogi dalej - nie tutaj (reszta czeka). 0 - bez progu (brak miary).</summary>
        internal static double DepthAgedFloor(double unitCost, double depthNet)
        {
            if (!(unitCost > 0.0) || double.IsNaN(depthNet) || !(depthNet > 0.0)) return 0.0;
            return Math.Min(unitCost, depthNet / CarrierMargin);
        }

        // ------------------------------------------------------------ zdarzenia handlu (CaravanGoods.TradePostfix)
        internal static void NoteBuy(MobileParty mp, ItemObject it, int got, Settlement st)
        {
            try
            {
                var c = it != null ? it.ItemCategory : null;
                if (got > 0 && _heldReady && Tracked(c)) _held[c] = Held(c) + got;
                // 298 (tylko pomiar): kupno jedzenia psujacego sie i glod miasta zrodla (KsHunger.HungerOf - migawka doby; miasto bez migawki poza srednia)
                if (got > 0 && c != null && SpoilCat(c))
                {
                    _fBuyN += got;
                    float u = KsHunger.HungerOf(st != null ? st.Town : null);
                    if (u >= 0f) { _fBuyKs += got; _fBuyU += u * (double)got; }
                }
            }
            catch (Exception e) { Stumble("NoteBuy", e); }
        }

        /// <summary>Sprzedaz karawany miastu: utarg przed mytem, myto, koszt zdjety z ksiegi. Zysk zrealizowany = utarg - myto - koszt, wedlug drogi; pomiar sprzedazy kategorii
        /// (bez rozladunku nadmiaru surowcow i bez rozwiazywanej karawany - to nie jest zbyt, ktory miasta przejmuja).</summary>
        internal static void NoteSale(MobileParty mp, ItemObject it, int moved, int got, int toll, double cost)
        {
            try
            {
                int p = Path;
                if (p == PathOther && _bkSelling > 0) p = PathBk;
                if (p < 0 || p >= Paths) p = PathOther;
                long real = (long)Math.Round(got - toll - cost);
                _pN[p] += moved; _pG[p] += got; _pT[p] += toll; _pC[p] += (long)Math.Round(cost); _pR[p] += real;
                if (real < 0) { _pLossN[p] += moved; _pLossG[p] += got; _pLossC[p] += (long)Math.Round(cost); }
                if (mp.ActualClan != null && mp.ActualClan == Clan.PlayerClan) { _plG[p] += got; _plR[p] += real; }
                AddReal(mp, (int)CampaignTime.Now.ToDays, real);
                var c = it != null ? it.ItemCategory : null;
                if (real < 0 && IsFoodCat(c))   // 298 (tylko pomiar): jedzenie ponizej kosztu wszystkimi drogami (ta sama miara co _pLoss*, kategoria jedzenia)
                {
                    long cr = (long)Math.Round(cost);
                    _fLossN += moved; _fLossG += got; _fLossC += cr;
                    if (SpoilCat(c)) { _fsLossN += moved; _fsLossG += got; _fsLossC += cr; }
                }
                if (Tracked(c))
                {
                    if (_heldReady) _held[c] = Math.Max(0, Held(c) - moved);
                    if (p != PathBulkGlut && !mp.IsDisbanding) { int x; _soldToday.TryGetValue(c, out x); _soldToday[c] = x + moved; }
                }
            }
            catch (Exception e) { Stumble("NoteSale", e); }
        }

        /// <summary>294: okno ksiegi zysku karawany (doby) - seria stratnej karawany liczy sie w tych dobach.</summary>
        internal const int RingDays = Ring;

        /// <summary>294: karawany w zwijaniu (kopia listy - WindHourly moze je usuwac); null - zadnej.</summary>
        internal static List<MobileParty> Winding()
        {
            EnsureBook();
            List<MobileParty> l = null;
            foreach (var kv in _acc) if (kv.Value.Wind != int.MinValue) { if (l == null) l = new List<MobileParty>(); l.Add(kv.Key); }
            return l;
        }

        /// <summary>294: doby zwijania karawany; -1 - nie jest zwijana.</summary>
        internal static int WindDays(MobileParty mp)
        {
            Acc a;
            if (mp == null || !_acc.TryGetValue(mp, out a) || a.Wind == int.MinValue) return -1;
            return Math.Max(0, (int)CampaignTime.Now.ToDays - a.Wind);
        }

        /// <summary>294: koniec zwijania (karawana nie moze byc zwinieta - bohater, kontrakt, statki) - kupno BK znow czynne, seria od zera.</summary>
        internal static void Unwind(MobileParty mp)
        {
            Acc a;
            if (mp != null && _acc.TryGetValue(mp, out a)) { a.Wind = int.MinValue; a.Below = 0; }
        }

        private static Acc AccOf(MobileParty mp, int day, int born = int.MinValue)
        {
            EnsureBook();
            Acc a;
            if (!_acc.TryGetValue(mp, out a))
            {
                a = new Acc { First = day, Old = !_dayRan, Born = born != int.MinValue ? born : day };
                for (int i = 0; i < Ring; i++) a.D[i] = int.MinValue;
                _acc[mp] = a;
            }
            return a;
        }

        private static bool BookOn(Settings s) { return s != null && (s.CaravanLosingWoundUp || s.CaravanEntryWhenTradePays); }

        /// <summary>294: zapis ksiegi zysku karawan (arm_k294): "k294|1|" + wpisy "id,First,Below,BelowDay,Wind,dob_temu:wynik/..." (doba sprzedazy jako doby przed
        /// zapisem, tylko okno 14 dob). Pusty napis przy kluczach 294 B i C NIE - ksiega jak dotad tylko w sesji.</summary>
        internal static string ExportBook()
        {
            var s = Settings.Current;
            if (!BookOn(s)) return "";
            if (_bookPend != null) return _bookPend;                            // zapis przed pierwszym uzyciem ksiegi - wczytany napis bez zmian
            int now = (int)CampaignTime.Now.ToDays;
            var sb = new StringBuilder(1 << 16);
            sb.Append("k294|1|");
            bool first = true;
            foreach (var kv in _acc)
            {
                var mp = kv.Key; var a = kv.Value;
                string id = mp != null ? mp.StringId : null;
                if (mp == null || !mp.IsActive || string.IsNullOrEmpty(id) || id.IndexOfAny(BadIdChars) >= 0) continue;
                if (!first) sb.Append(';');
                first = false;
                sb.Append(id).Append(',').Append(a.First.ToString(Inv)).Append(',').Append(a.Below.ToString(Inv)).Append(',').Append(a.BelowDay.ToString(Inv))
                  .Append(',').Append(a.Wind.ToString(Inv)).Append(',');
                bool f2 = true;
                for (int k = 0; k < Ring; k++)
                {
                    if (a.D[k] == int.MinValue || a.D[k] > now || now - a.D[k] >= Ring) continue;
                    if (!f2) sb.Append('/');
                    f2 = false;
                    sb.Append((now - a.D[k]).ToString(Inv)).Append(':').Append(a.R[k].ToString(Inv));
                }
            }
            return sb.ToString();
        }

        private static readonly char[] BadIdChars = { ';', ',', '|', ':', '/' };

        /// <summary>294: wczytanie arm_k294 (po Reset) - napis czeka do pierwszego uzycia ksiegi. null / pusty - zapis bez klucza (ksiega od zera, jak dotad).</summary>
        internal static void ImportBook(string s)
        {
            _bookHad = !string.IsNullOrEmpty(s);
            _bookPend = _bookHad ? s : null;
        }

        private static void EnsureBook()
        {
            string s = _bookPend;
            if (s == null) return;
            _bookPend = null;
            try
            {
                if (!BookOn(Settings.Current)) return;                          // klucze 294 B i C NIE - ksiega tylko w sesji (jak 290)
                if (!s.StartsWith("k294|1|", StringComparison.Ordinal)) { _bookBad++; return; }
                int now = (int)CampaignTime.Now.ToDays;
                var cars = new Dictionary<string, MobileParty>();
                foreach (var mp in MobileParty.AllCaravanParties) if (mp != null && mp.IsActive && mp.StringId != null) cars[mp.StringId] = mp;
                foreach (var rec in s.Substring(7).Split(';'))
                {
                    if (rec.Length == 0) continue;
                    var f = rec.Split(',');
                    int first, below, bday, wind;
                    if (f.Length != 6 || !int.TryParse(f[1], NumberStyles.Integer, Inv, out first) || !int.TryParse(f[2], NumberStyles.Integer, Inv, out below)
                        || !int.TryParse(f[3], NumberStyles.Integer, Inv, out bday) || !int.TryParse(f[4], NumberStyles.Integer, Inv, out wind) || below < 0 || first > now) { _bookBad++; continue; }
                    MobileParty mp;
                    if (!cars.TryGetValue(f[0], out mp) || _acc.ContainsKey(mp)) { _bookGone++; continue; }
                    var a = new Acc { First = first, Below = below, BelowDay = bday, Wind = wind };
                    for (int i = 0; i < Ring; i++) a.D[i] = int.MinValue;
                    bool ok = true;
                    foreach (var e in f[5].Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        int c = e.IndexOf(':'), ago; long r;
                        if (c <= 0 || !int.TryParse(e.Substring(0, c), NumberStyles.Integer, Inv, out ago) || !long.TryParse(e.Substring(c + 1), NumberStyles.Integer, Inv, out r)
                            || ago < 0 || ago >= Ring) { ok = false; break; }
                        int d = now - ago, k = ((d % Ring) + Ring) % Ring;
                        a.D[k] = d; a.R[k] = r;
                    }
                    if (!ok) { _bookBad++; continue; }
                    _acc[mp] = a; _bookOk++;
                }
            }
            catch (Exception e) { _bookBad++; Stumble("EnsureBook", e); }
        }

        private static void AddReal(MobileParty mp, int day, long v)
        {
            if (mp == null) return;
            var a = AccOf(mp, day);
            int k = ((day % Ring) + Ring) % Ring;
            if (a.D[k] != day) { a.D[k] = day; a.R[k] = 0; }
            a.R[k] += v;
        }

        /// <summary>Suma zysku z dob (end - n, end] i czy byla w nich jakas sprzedaz.</summary>
        private static long SumLast(Acc a, int end, int n, out bool any)
        {
            long s = 0; any = false;
            for (int k = 0; k < Ring; k++)
                if (a.D[k] != int.MinValue && a.D[k] <= end && end - a.D[k] < n) { s += a.R[k]; any = true; }
            return s;
        }

        // ------------------------------------------------------------ zakup BK (BuyGoods -> CalculateBuyValue)
        private sealed class Buy { public MobileParty Mp; public bool Exempt; public int Capital, Floor, Wage, Purse0; public double Stock0, Pen = -1, Toll; public readonly HashSet<ItemCategory> Seen = new HashSet<ItemCategory>(), SeenD = new HashSet<ItemCategory>();
            public Town PeekTown; public readonly Dictionary<ItemCategory, int> PeekK = new Dictionary<ItemCategory, int>(); public readonly Dictionary<ItemObject, double> PeekNet = new Dictionary<ItemObject, double>(); }
        private static readonly Buy _b = new Buy();
        private static readonly Buy _pk = new Buy();   // 294c: ocena celu BK (WhyPeek) - osobny stan, _b (wizyta BuyGoods) nietkniety

        /// <summary>294c (CaravanRoute294c, ThinkNextDestination): stan oceny celu dla karawany - kapital i podloga 259, ladunek po koszcie (CargoCost), kiesa; raz na decyzje.
        /// false - karawana bez zasad 284 (kontrakt, rozkaz gracza, nie handluje) albo klucze zakupu NIE.</summary>
        internal static bool PeekStart(MobileParty mp)
        {
            _pk.Mp = null; _pk.PeekTown = null; _pk.PeekK.Clear(); _pk.PeekNet.Clear(); _pk.Pen = -1;
            var s = Settings.Current;
            if (s == null || mp == null || !(s.CaravanBuyByFlow || s.CaravanBuyWorkingCapital || s.CaravanBuyProfitCheck)) return false;
            if (!mp.IsCaravan || mp.Owner == null || !CaravanBulk.TradesForT1(mp) || MaterialOrders.HasContract(mp) || CaravanBulk.HasOrders(mp)) return false;
            int cap, floor;
            CaravanSettle.CapFloor(mp, out cap, out floor);
            _pk.Capital = cap; _pk.Floor = floor; _pk.Wage = Math.Max(0, mp.TotalWage); _pk.Purse0 = mp.PartyTradeGold;
            _pk.Stock0 = CaravanGoods.CargoCost(mp);
            _pk.Mp = mp;
            return true;
        }

        internal static void PeekEnd() { _pk.Mp = null; _pk.PeekTown = null; _pk.PeekK.Clear(); _pk.PeekNet.Clear(); }

        /// <summary>294c: regula zakupu BK, ktora zablokowalaby kategorie w miescie town (WhyCore bez licznikow i bez reguly 3): 0 - kupno czynne; 1 bramka, 2 kapital,
        /// 4 zysk / glebokosc, 5 jedzenie > 7 dob, 6 zwijanie. Tylko kategorie Tracked, po PeekStart tej karawany; inaczej 0.</summary>
        internal static int WhyPeek(MobileParty mp, ItemCategory cat, Town town)
        {
            if (mp == null || town == null || !ReferenceEquals(_pk.Mp, mp) || !Tracked(cat)) return 0;
            var s = Settings.Current;
            if (s == null) return 0;
            try
            {
                if (!ReferenceEquals(_pk.PeekTown, town)) { _pk.PeekTown = town; _pk.Pen = -1; _pk.Toll = TownToll.CaravanRateFor(mp, town.Settlement); }
                return WhyCore(_pk, mp, cat, town, s, true);
            }
            catch (Exception e) { Stumble("WhyPeek", e); return 0; }
        }

        /// <summary>294c: DepthBuy dla oceny celu - k (QueueK, zalezy tylko od karawany i kategorii) i cena sztuki w miescie odniesienia (zalezy od towaru) z pamieci
        /// decyzji; w miescie celu tylko pierwszy towar kategorii na polce i jego cena kupna (ta sama regula: net < 1.03 x cena kupna - kupno stoi).</summary>
        private static int DepthBuyPeek(Buy b, MobileParty mp, ItemCategory cat, Town town)
        {
            try
            {
                var rep = RepOnShelf(town, cat);
                if (rep == null) return -1;
                var el = new EquipmentElement(rep);
                int buy = town.GetItemPrice(el, mp, false);
                if (buy <= 0) return -1;
                int k;
                if (!b.PeekK.TryGetValue(cat, out k)) { k = QueueK(mp, cat, TradeUnits(mp, cat) + 1); b.PeekK[cat] = k; }
                double net;
                if (!b.PeekNet.TryGetValue(rep, out net)) { net = DepthNet(mp, el, k); b.PeekNet[rep] = net; }
                if (double.IsNaN(net)) return -1;
                return net < CarrierMargin * buy ? 1 : 0;
            }
            catch (Exception e) { DStumble("BuyPeek", e); return -1; }
        }

        /// <summary>294c: kopia spizarni eskorty karawany (PantryOf - wspolna pamiec _pantryTmp nadpisywana przez kazde TradeUnits); null - brak spizarni.</summary>
        internal static Dictionary<ItemObject, int> PantryCopy(MobileParty mp)
        {
            var p = PantryOf(mp);
            return p != null ? new Dictionary<ItemObject, int>(p) : null;
        }

        /// <summary>294c: karawana sprzedala cos w dobach (end - 7, end] (ksiega zysku 284 - ta sama miara co 'karawany 7 dob: z handlem'). Bez wpisu - false.</summary>
        internal static bool SoldIn7(MobileParty mp, int end)
        {
            Acc a;
            if (mp == null || !_acc.TryGetValue(mp, out a)) return false;
            bool any;
            SumLast(a, end, 7, out any);
            return any;
        }

        /// <summary>294c: ile JEDNO miasto zjada kategorii na dobe w jednostkach polki (wartosc) - ten sam czlon co petla Mkt (budzet doby modelu gry / indeks miasta).</summary>
        internal static double MktTownValue(Town t, ItemCategory c)
        {
            if (t == null || c == null || !t.IsTown || t.MarketData == null) return 0.0;
            float dem = t.MarketData.GetCategoryData(c).Demand;
            if (!(dem > 0f)) return 0.0;
            var cg = Campaign.Current;
            var em = cg != null && cg.Models != null ? cg.Models.SettlementEconomyModel : null;
            float idx = Math.Max(0.1f, t.GetItemCategoryPriceIndex(c));
            float bud = em != null ? em.CalculateDailySettlementBudgetForItemCategory(t, dem, c) : dem * (float)Math.Pow(idx, 0.3);
            if (!(bud > 0f) || float.IsNaN(bud) || float.IsInfinity(bud)) return 0.0;
            return bud / (double)idx;
        }

        /// <summary>Prefiks BK BuyGoods (z CaravanGoods.BkBuyPrefix): kapital i podloga 259, ladunek po koszcie, kiesa, myto - raz na wywolanie.</summary>
        internal static void BuyStart(MobileParty mp, Town town)
        {
            _b.Mp = mp; _b.Exempt = true; _b.Seen.Clear(); _b.SeenD.Clear(); _b.Pen = -1;
            var s = Settings.Current;
            if (s == null || mp == null || town == null || !(s.CaravanBuyByFlow || s.CaravanBuyWorkingCapital || s.CaravanBuyProfitCheck)) return;
            var c = Campaign.Current;
            if (c == null || !c.GameStarted) return;
            if (!mp.IsCaravan || mp.Owner == null || !CaravanBulk.TradesForT1(mp) || MaterialOrders.HasContract(mp) || CaravanBulk.HasOrders(mp)) { _skipExempt++; return; }
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                int cap, floor;
                CaravanSettle.CapFloor(mp, out cap, out floor);
                _b.Capital = cap; _b.Floor = floor; _b.Wage = Math.Max(0, mp.TotalWage); _b.Purse0 = mp.PartyTradeGold;
                _b.Stock0 = CaravanGoods.CargoCost(mp);
                _b.Toll = TownToll.CaravanRateFor(mp, town.Settlement);
                _b.Exempt = false;
                _buyVisits++;
                if (s.CaravanBuyWorkingCapital && cap - floor <= 0) _noRoom++;   // recenzja 284 (drobna 4): podloga 30 dni zoldu >= kapital - regula kapitalu pominieta (Why)
            }
            catch (Exception e) { _b.Exempt = true; Stumble("BuyStart", e); }   // karawana bez zasad 284 w tej wizycie (BK kupuje jak dotad)
            finally { _ticks += Stopwatch.GetTimestamp() - t0; }
        }

        internal static void BuyEnd() { _b.Mp = null; }

        /// <summary>Postfiks CalculateBuyValue (z CaravanGoods.BkValuePostfix, tylko w BuyGoods tej karawany): true - wycena 0 (BK nie kupi tej kategorii w tej wizycie).</summary>
        internal static bool BuyBlocked(MobileParty mp, ItemCategory cat, Town town)
        {
            if (!ReferenceEquals(_b.Mp, mp) || _b.Exempt || !Tracked(cat)) return false;
            var s = Settings.Current;
            if (s == null) return false;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                int why = Why(mp, cat, town, s);
                if (why != 0 && _b.Seen.Add(cat))
                    switch (why) { case 1: _rGate++; break; case 2: _rCap++; break; case 3: _rWage++; break; case 4: _rGain++; break; case 6: _rWind++; break; default: _rFood++; break; }
                return why != 0;
            }
            catch (Exception e) { Stumble("BuyBlocked", e); return false; }
            finally { _ticks += Stopwatch.GetTimestamp() - t0; }
        }

        private static int Why(MobileParty mp, ItemCategory cat, Town town, Settings s) { return WhyCore(_b, mp, cat, town, s, false); }

        /// <summary>Reguly zakupu BK (Why). 294c: peek - ta sama logika dla oceny celu BK (CaravanRoute294c, klucz CaravanRouteByBuyRules) bez licznikow, bez stanu
        /// wizyty (_b) i bez reguly 3 (rezerwa zoldu - kiesa w miescie celu bedzie inna po sprzedazy); k glebokosci i cena sztuki w miescie odniesienia z pamieci decyzji.</summary>
        private static int WhyCore(Buy b, MobileParty mp, ItemCategory cat, Town town, Settings s, bool peek)
        {
            if (s.CaravanLosingWoundUp && WindDays(mp) >= 0) return 6;          // 294: karawana w zwijaniu - wlasciciel juz nie kupuje, tylko wyprzedaje
            if (s.CaravanBuyByFlow && GateShut(cat)) return 1;
            int purse = mp.PartyTradeGold;
            double cargo = b.Stock0 + Math.Max(0, b.Purse0 - purse);      // ladunek po koszcie: z poczatku BuyGoods + to, co BK juz wydal w tym wywolaniu
            double spend = Math.Min(0.5 * Math.Max(0, purse), 1500.0);        // BK BuyCategory: najwyzej pol kiesy i 1 500 na kategorie
            if (s.CaravanBuyWorkingCapital)
            {
                // recenzja 284 (drobna 4): podloga z rezerwa 30 dni zoldu (karawana bez zwrotu zoldu) moze byc >= kapital - to podloga wyplaty, nie granica handlu;
                // wtedy bez reguly kapitalu (kiese dalej pilnuje rezerwa zoldu nizej)
                if (b.Capital - b.Floor > 0 && cargo >= b.Capital - b.Floor) return 2;
                if (!peek && purse - spend < CashDays * b.Wage) return 3;
            }
            if (s.CaravanBuyProfitCheck)
            {
                float idx = town.GetItemCategoryPriceIndex(cat), e = SellIdx(cat);
                if (b.Pen < 0) b.Pen = PenOf(mp, town);
                // recenzja 284 (wazne 1 i 3): bez zoldu w tescie zakupu - zold karawana placi, czy kupi, czy nie (koszt staly, jak po stronie sprzedazy
                // CaravanSellNoSunkWage); odmowa zakupu z dodatnia marza nie oszczedza zoldu, tylko traci marze. Koszty stale pilnuja reguly 2 i 3.
                bool old4 = !(e > 0f) || idx * (1.0 + b.Pen) >= e / (1.0 + b.Pen) * (1.0 - b.Toll);
                if (DepthOn)
                {
                    // 294b (klucz CaravanBuyByMarketDepth): zamiast oferty indeksu - cena sztuki, ktora ta karawana by przywiozla, w kolejce za swoimi sztukami i udzialem
                    // jukow innych karawan na jedno miasto drozsze, minus zjedzone tam w dobie drogi (DepthBuy); marza przewoznika 3% jak KS-H; bez zoldu (koszt staly,
                    // recenzja 284). Nadkup ograniczony przez BK (1 500 zl na kategorie na wizyte), w nastepnej wizycie moje + udzial juz blokuja. Bez miary - stara regula.
                    int dz = peek ? DepthBuyPeek(b, mp, cat, town) : DepthBuy(mp, cat, town);
                    if (!peek && b.SeenD.Add(cat)) { _dEval++; if (old4) _dOld++; if (dz == 1) _dStop++; else if (dz == 0) _dOpen++; else _dNone++; }
                    if (dz == 1 || (dz < 0 && old4)) return 4;
                }
                else if (old4) return 4;
                // recenzja 284 (wazne 2): jedzenie > 7 dob dopiero po FlowMinDays dobach pomiaru (jak bramka) i tylko gdy cos jest w jukach - 0 w jukach to 0 dob,
                // nie nieskonczonosc (inaczej kategoria bez sprzedazy zostaje zamknieta na zawsze); 294b (R2): tylko jedzenie, ktore sie psuje - trwale jak towar
                if (_days >= FlowMinDays && IsFoodCat(cat) && !(R2Full(s) && !SpoilCat(cat)))
                {
                    EnsureHeld();
                    long h = Held(cat);
                    if (h > 0)
                    {
                        float ema = Flow(cat);   // 287: zbyt z popytem rynkow (klucz CaravanFlowByMarketDemand), bez klucza - sprzedaz karawan jak dotad
                        double raw = ema > 0.01f ? h / (double)ema : double.PositiveInfinity;
                        if (raw > FoodDays) return 5;
                    }
                }
            }
            return 0;
        }

        private static double PenOf(MobileParty mp, Town town)
        {
            var r = town.Owner != null ? town.Owner.ItemRoster : null;
            if (r != null)
                for (int i = 0; i < r.Count; i++)
                {
                    var it = r.GetElementCopyAtIndex(i).EquipmentElement.Item;
                    if (it != null && it.IsTradeGood && !it.HasHorseComponent) return CaravanGoods.Penalty(mp, it);
                }
            return 0.0;
        }

        // mediana indeksu miast, ktore placa za kategorie co najmniej srednia (tam sprzedaja T1 i BK) - raz na dobe na kategorie
        private static readonly Dictionary<ItemCategory, float> _sellIdx = new Dictionary<ItemCategory, float>();
        private static int _sellDay = -1;

        /// <summary>Poprawka 132a (CaravanGoods.AgeMarket): mediana indeksu miast placacych za kategorie co najmniej srednia - pamiec doby jak SellIdx.</summary>
        internal static float SellIdxOf(ItemCategory c) { try { return c != null ? SellIdx(c) : 0f; } catch (Exception e) { Stumble("SellIdxOf", e); return 0f; } }

        /// <summary>Poprawka 132a: towar > 30 dob nie poszedl na strate, bo rynek z zyskiem istnieje (CaravanGoods.AgeMarket) - licznik doby.</summary>
        internal static void NoteAgeWait() { _ageWait++; }

        private static float SellIdx(ItemCategory c)
        {
            int d = (int)CampaignTime.Now.ToDays;
            if (d != _sellDay) { _sellIdx.Clear(); _depth.Clear(); _sellDay = d; }
            float v;
            if (_sellIdx.TryGetValue(c, out v)) return v;
            float avg = CaravanGoods.AvgIndexOf(c);
            var l = new List<KeyValuePair<float, Town>>();
            foreach (var t in Town.AllTowns) { if (t == null) continue; float x = t.GetItemCategoryPriceIndex(c); if (x >= avg) l.Add(new KeyValuePair<float, Town>(x, t)); }
            DepthRef dr = null;
            if (l.Count == 0) v = avg;
            else
            {
                l.Sort((x, y) => x.Key.CompareTo(y.Key));
                var m = l[l.Count / 2];
                v = m.Key;
                // 294b: miasto odniesienia (to, ktorego indeks zwraca mediana) - migawka jego danych targu na dobe i liczba miast drozszych N
                try
                {
                    var md = m.Value.MarketData;
                    if (md != null)
                    {
                        var id = md.GetCategoryData(c);
                        dr = new DepthRef { Ref = m.Value, Idx = v, InStore = id.InStoreValue, Supply = id.Supply, Demand = id.Demand, N = l.Count };
                    }
                }
                catch (Exception e) { DStumble("Ref", e); dr = null; }
            }
            _sellIdx[c] = v; _depth[c] = dr;
            return v;
        }

        // ------------------------------------------------------------ BK SellGoods: nie ponizej kosztu
        public static void SellGoodsPre() { if (_bkSelling <= 0) { _bkWithheld.Clear(); _bkWithheldMp = null; } _bkSelling++; Path = PathBk; }

        /// <summary>Recenzja 284 (drobne 3 i 5): BK SellGoods dopisuje do listy transakcji liczbe sztuk sprzed przyciecia (SellGuardPrefix) i po petli wola
        /// OnCaravanTransactionCompleted z ta lista. TownFolk.OnCaravanTrade (P1) odejmuje tu sztuki wstrzymane tej karawany (raz - wpis zdjety).</summary>
        internal static int TakeWithheld(MobileParty mp, EquipmentElement el)
        {
            if (_bkWithheld.Count == 0 || mp == null || !ReferenceEquals(mp, _bkWithheldMp)) return 0;
            int x;
            if (!_bkWithheld.TryGetValue(el, out x)) return 0;
            _bkWithheld.Remove(el);
            _p1Phantom += x;
            return x;
        }
        public static Exception SellGoodsFin(Exception __exception) { if (_bkSelling > 0) _bkSelling--; if (_bkSelling == 0) Path = PathOther; return __exception; }

        /// <summary>Poprawka 132a: sprzedaz gry CaravansCampaignBehavior.SellGoods (wjazd do miasta i zrzut wagi przy wyjezdzie) - ta sama straz "nie ponizej kosztu"
        /// co BK SellGoods (SellGuardPrefix), droga sprzedazy zostaje "inne". Dowod (132a, 30 dob): droga "inne" 5 925 szt. za 1.54 mln, zysk -599 tys. zl; ponizej
        /// kosztu 3 779 szt. za 1.31 mln przy koszcie 1.96 mln - gra sprzedaje wedlug swojego indeksu (priceIndexSellLimit 1.1), bez kosztu zakupu. Lista transakcji gry
        /// (OnCaravanTransactionCompleted) ma liczbe sprzed przyciecia - TakeWithheld jak w BK.</summary>
        public static void VanSellPre() { if (_vanSelling <= 0 && _bkSelling <= 0) { _bkWithheld.Clear(); _bkWithheldMp = null; } _vanSelling++; }
        public static Exception VanSellFin(Exception __exception) { if (_vanSelling > 0) _vanSelling--; return __exception; }

        /// <summary>Prefiks SellItemsAction.ApplyInternal (Priority.First, tylko w BK SellGoods i - poprawka 132a - w SellGoods gry): karawana sprzedaje miastu towar handlowy - liczba sztuk przycieta do tylu,
        /// ile idzie po cenie po mycie nie nizszej niz koszt sztuki (0 - nic nie schodzi, gra nie przenosi zlota). Wyjscia na stracie T1 (rozwiazanie, brak na zold, juki ponad
        /// udzwig - takze toLoseWeight BK, jedzenie > 7 dob, towar > 30 dob) - bez zmian.</summary>
        public static void SellGuardPrefix(PartyBase __0, PartyBase __1, ItemRosterElement __2, ref int __3)
        {
            if ((_bkSelling <= 0 && _vanSelling <= 0) || __3 <= 0) return;   // poprawka 132a: takze sprzedaz gry (VanSellPre)
            bool bk = _bkSelling > 0;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var s = Settings.Current;
                if (s == null || __0 == null || __1 == null) return;
                // 294c H1 (klucz CaravanPantryAllSales): spizarnia eskorty (ta sama miara, ktora T1 zatrzymuje - PantryOf, najtansze najpierw) nie schodzi tez w sprzedazy
                // BK i gry - przed reszta zasad i niezaleznie od kontraktu / rozkazu (eskorta je z jukow w drodze, 288); wstrzymane przez Withhold (lista transakcji BK)
                if (s.CaravanPantryAllSales) PantryGuard(__0.MobileParty, __1, __2.EquipmentElement, bk, ref __3);
                if (!s.CaravanBkSellAtCost || __3 <= 0) return;
                var mp = __0.MobileParty;
                if (mp == null || !mp.IsCaravan || !__1.IsSettlement || __1.Settlement == null || __1.Settlement.Town == null) return;
                var it = __2.EquipmentElement.Item;
                if (it == null || !it.IsTradeGood || it.HasHorseComponent || it.ItemCategory == null || it.ItemCategory == DefaultItemCategories.PackAnimal) return;
                if (!CaravanBulk.TradesForT1(mp) || MaterialOrders.HasContract(mp) || CaravanBulk.HasOrders(mp)) return;
                if (CaravanBulk.Owns(it) && CaravanGoods.CostEstimated(mp, it)) return;   // surowiec masowy z kosztem z oszacowania - bez progu (jak CaravanBulk.Sell)
                int lr = CaravanGoods.LossReason(mp, it, __1.Settlement);
                if (lr != 0)
                {
                    if (bk) _bkExit++; else _vanExit++;
                    if (lr == 4 && CaravanMarket294.FoodKeepsOn)
                    {
                        // 294 (klucz CaravanFoodKeepsByKind): psujace sie jedzenie w sprzedazy BK / gry - ta sama granica co T1: po mycie nie taniej niz koszt x (1 - 1.5% x doby
                        // w jukach); reszta czeka na nastepne miasto
                        double u4 = CaravanGoods.UnitCost(mp, it);
                        double fl4 = CaravanMarket294.SpoilFloor(u4, CaravanGoods.AgeOf(mp, it));
                        if (fl4 > 0.0)
                        {
                            int kf = Math.Max(0, CaravanGoods.MaxAtCost(__1.Settlement.Town, __1.Settlement, mp, __2.EquipmentElement, __3, fl4));
                            if (kf < __3)
                            {
                                CaravanMarket294.NoteFloorBk(__3 - kf);
                                Withhold(mp, __2.EquipmentElement, bk, __3 - kf, u4);
                                __3 = kf;
                            }
                        }
                        return;
                    }
                    if (lr == 5 && CaravanMarket294.Durable(it) && CaravanGoods.AgedGlut(it.ItemCategory))
                    {
                        // poprawka 135a (286b, klucz CaravanAgedGlutAtBookWorth): stary towar w przesycie schodzi w sprzedazy BK / gry tylko po cenie po mycie nie nizszej
                        // niz wartosc sztuki w ksiedze 259 (min(koszt, wartosc x srednia indeksu 97 miast) - CaravanGoods.WorthOf); reszta czeka na miasto, ktore placi wiecej
                        double u5 = CaravanGoods.UnitCost(mp, it);
                        double fl = u5 > 0.0 ? CaravanGoods.AgedGlutFloor(it, u5) : 0.0;
                        if (fl > 0.0)
                        {
                            int kf = Math.Max(0, CaravanGoods.MaxAtCost(__1.Settlement.Town, __1.Settlement, mp, __2.EquipmentElement, __3, fl));
                            if (kf < __3)
                            {
                                CaravanGoods.NoteAgedFloor286(__3 - kf, false);
                                Withhold(mp, __2.EquipmentElement, bk, __3 - kf, u5);
                                __3 = kf;
                            }
                        }
                        CaravanGoods.NoteAged286(__3, 0, 0, it.ItemCategory, false);   // 286: stary towar w przesycie (licznik - sztuki wypuszczone)
                    }
                    if (lr == 5 && __3 > 0 && CaravanGoods.AgeDepthNo)
                    {
                        // 294b (recenzja): stary towar bez rynku z glebokoscia - w sprzedazy BK / gry ta sama granica co T1: po mycie nie taniej niz min(koszt, cena tej sztuki
                        // w miescie odniesienia po mycie / 1.03) - nie oddaje towaru miastu tanszemu niz typowe miasto drozsze o odcinek dalej; reszta czeka
                        double u5d = CaravanGoods.UnitCost(mp, it);
                        double fld = DepthAgedFloor(u5d, CaravanGoods.AgeDepthNet);
                        if (fld > 0.0)
                        {
                            int kf = Math.Max(0, CaravanGoods.MaxAtCost(__1.Settlement.Town, __1.Settlement, mp, __2.EquipmentElement, __3, fld));
                            if (kf < __3)
                            {
                                _dHeldBk += __3 - kf;
                                Withhold(mp, __2.EquipmentElement, bk, __3 - kf, u5d);
                                __3 = kf;
                            }
                        }
                        if (__3 > 0) { _dBk5N += __3; _dBk5C += (long)Math.Round(__3 * u5d); if (CaravanGoods.AgeOldWait) _dLossOldN += __3; }   // 294b: licznik
                    }
                    return;
                }
                double unit = CaravanGoods.UnitCost(mp, it);
                if (!(unit > 0.0)) return;
                int k = CaravanGoods.MaxAtCost(__1.Settlement.Town, __1.Settlement, mp, __2.EquipmentElement, __3, unit);
                // 285 (klucz CaravanGlutToShortTown): towar z zamknietym kupnem (przesyt - juki ponad 10 dob sprzedazy swiata) i surowiec tabeli zamowien, ktorego temu miastu
                // brakuje do wlasnego punktu zamowienia (MaterialOrders.ShortOf) - brak miasta schodzi po cenie dnia takze ponizej kosztu (strata juz zaszla przy zakupie;
                // stos w jukach i pusta polka warsztatu to ten sam towar). Reszta jak dotad - nie ponizej kosztu.
                if (k < __3 && s.CaravanGlutToShortTown && GateShut(it.ItemCategory))
                {
                    int sh = MaterialOrders.ShortOf(__1.Settlement.Town, it);
                    if (sh > 0)
                    {
                        int k2 = Math.Min(__3, Math.Max(Math.Max(0, k), sh));
                        int rel = k2 - Math.Max(0, k);
                        if (rel > 0)
                        {
                            _g285N += rel; _g285C += (long)Math.Round(rel * unit); _g285Acts++;
                            var cid = it.ItemCategory.StringId ?? "?"; int g0; _g285By.TryGetValue(cid, out g0); _g285By[cid] = g0 + rel;
                            k = k2;
                        }
                    }
                }
                if (k < __3)
                {
                    Withhold(mp, __2.EquipmentElement, bk, __3 - Math.Max(0, k), unit);
                    __3 = Math.Max(0, k);
                }
            }
            catch (Exception e) { Stumble("SellGuardPrefix", e); }
            finally { _ticks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>SellGuardPrefix: n sztuk wstrzymanych (licznik BK / gry, koszt) i wpis dla TakeWithheld (lista transakcji ma liczbe sprzed przyciecia) - wydzielone
        /// w poprawce 135a, ta sama ksiega co dotad (takze dla progu wartosci starego towaru w przesycie).</summary>
        private static void Withhold(MobileParty mp, EquipmentElement el, bool bk, int n, double unit)
        {
            if (n <= 0) return;
            if (bk) { _bkHeldN += n; _bkHeldC += (long)Math.Round(n * unit); }
            else { _vanHeldN += n; _vanHeldC += (long)Math.Round(n * unit); }
            WithholdNote(mp, el, n);
        }

        /// <summary>294c: wpis sztuk wstrzymanych dla TakeWithheld (lista transakcji BK / gry ma liczbe sprzed przyciecia) - bez licznikow 'ponizej kosztu'.</summary>
        private static void WithholdNote(MobileParty mp, EquipmentElement el, int n)
        {
            if (n <= 0) return;
            if (!ReferenceEquals(_bkWithheldMp, mp)) { _bkWithheld.Clear(); _bkWithheldMp = mp; }
            int w0; _bkWithheld.TryGetValue(el, out w0);
            _bkWithheld[el] = w0 + n;
        }

        // 294c H1: spizarnia eskorty wstrzymana w sprzedazy BK / gry (szt.) - od ostatniej linii 288 (CaravanEscort.Daily czyta i zeruje)
        private static long _h1Bk, _h1Van;

        /// <summary>294c H1: sprzedaz BK albo gry jedzenia karawany - najwyzej sztuki ponad spizarnie eskorty tego towaru (PantryOf: ceil(-FoodChange x 10) sztuk,
        /// najtansze najpierw - ten sam przydzial co bramka 294b i T1).</summary>
        private static void PantryGuard(MobileParty mp, PartyBase to, EquipmentElement el, bool bk, ref int n)
        {
            var it = el.Item;
            if (n <= 0 || mp == null || !mp.IsCaravan || mp.IsDisbanding || it == null || !it.IsFood || mp.ItemRoster == null || !to.IsSettlement) return;
            var pan = PantryOf(mp);
            int keep;
            if (pan == null || !pan.TryGetValue(it, out keep) || keep <= 0) return;
            int have = mp.ItemRoster.GetItemNumber(it);
            int k = Math.Max(0, Math.Min(n, have - keep));
            if (k >= n) return;
            int held = n - k;
            if (bk) _h1Bk += held; else _h1Van += held;
            WithholdNote(mp, el, held);
            n = k;
        }

        /// <summary>294c (linia 288): spizarnia wstrzymana w sprzedazy BK i gry od ostatniej linii - odczyt i zero.</summary>
        internal static void TakePantryHeld(out long bk, out long van) { bk = _h1Bk; van = _h1Van; _h1Bk = _h1Van = 0; }

        // 285: przesyt karawan do miasta z brakiem surowca tabeli (SellGuardPrefix) - liczniki od ostatniej linii 285
        private static long _g285N, _g285C; private static int _g285Acts;
        // 298 (tylko pomiar): pierwsza doba sesji (Daily) - kohorta karawan powstalych od 31. doby biegu; potkniecia linii 298 (doba / sesja)
        private static int _day0 = int.MinValue, _m298Stum, _m298StumSess, _in298;
        private const int CohortDay = 30;
        // 298 (recenzja, tylko pomiar - kontrola L1): jedzenie sprzedane ponizej kosztu wszystkimi drogami (szt. / utarg / koszt; psujace osobno) i kupno psujacych
        // przez karawany (szt.; w miastach z migawka glodu KS: szt. i suma glod x szt.) - liczniki doby (ZeroDay)
        private static long _fLossN, _fLossG, _fLossC, _fsLossN, _fsLossG, _fsLossC, _fBuyN, _fBuyKs;
        private static double _fBuyU;
        private static readonly Dictionary<string, int> _g285By = new Dictionary<string, int>();

        /// <summary>285 (MaterialOrders.Line285): odcinek "przesyt karawan do miasta z brakiem" i zerowanie licznikow.</summary>
        internal static string Seg285()
        {
            var sb = new StringBuilder(200);
            sb.Append("przesyt karawan do miasta z brakiem (sprzedaz BK i gry ponizej kosztu dopuszczona do braku miasta): sztuk ").Append(_g285N).Append(" (koszt ").Append(_g285C)
              .Append(" zl) w ").Append(_g285Acts).Append(" sprzedazach [");
            bool f = true;
            foreach (var kv in _g285By) { sb.Append(f ? "" : ", ").Append(kv.Key).Append(' ').Append(kv.Value); f = false; }
            sb.Append(f ? "-]" : "]");
            _g285N = _g285C = 0; _g285Acts = 0; _g285By.Clear();
            return sb.ToString();
        }

        /// <summary>CaravanBulk.Sell: sztuki surowca wstrzymane, bo poszlyby ponizej kosztu (glut - rozladunek ponad brak).</summary>
        internal static void NoteBulkHeld(int n, bool glut) { if (n > 0) { _bulkHeldN += n; if (glut) _bulkHeldGlut += n; } }

        // ------------------------------------------------------------ gabka BK przy wyjezdzie (BKPartyBehavior.OnSettlementLeft)
        public static void LeftPrefix(MobileParty __0, Settlement __1, out Dictionary<ItemObject, int> __state)
        {
            __state = null;
            if (__0 == null || __1 == null || !__0.IsCaravan || __1.Town == null) return;
            try
            {
                var s = Settings.Current;
                // 294b: gabka cofana takze dla towaru bez glebokosci rynku (DepthOn) - niezaleznie od bramki przeplywu
                if (s == null || !((s.CaravanBuyByFlow && _days >= FlowMinDays) || DepthOn) || __0.ItemRoster == null) return;
                if (!CaravanBulk.TradesForT1(__0) || MaterialOrders.HasContract(__0) || CaravanBulk.HasOrders(__0)) return;
                var r = __0.ItemRoster;
                var d = new Dictionary<ItemObject, int>();
                for (int i = 0; i < r.Count; i++)
                {
                    var it = r.GetElementCopyAtIndex(i).EquipmentElement.Item;
                    if (it != null && !d.ContainsKey(it)) d[it] = r.GetItemNumber(it);
                }
                __state = d;
            }
            catch (Exception e) { Stumble("LeftPrefix", e); }
        }

        /// <summary>Postfiks gabki: towar kategorii z zamknietym kupnem, ktory gabka BK dokupila, wraca na polke, a zloto do kiesy (ta sama cena - polka wraca do stanu
        /// sprzed gabki; wzor CaravanBulk.LeftPostfix). 294b: takze towar, ktorego kupna zabrania test glebokosci rynku (SpongeDepth, k = moje sprzed gabki + 1).</summary>
        public static void LeftPostfix(MobileParty __0, Settlement __1, Dictionary<ItemObject, int> __state)
        {
            if (__state == null) return;
            try
            {
                var town = __1.Town;
                var pack = __0.ItemRoster;
                var shelf = town.Owner.ItemRoster;
                List<KeyValuePair<ItemObject, int>> back = null;
                HashSet<ItemObject> byDepth = null;
                for (int i = 0; i < pack.Count; i++)
                {
                    var it = pack.GetElementCopyAtIndex(i).EquipmentElement.Item;
                    if (it == null || !it.IsTradeGood || it.HasHorseComponent) continue;
                    int was; __state.TryGetValue(it, out was);
                    int extra = pack.GetItemNumber(it) - was;
                    if (extra <= 0) continue;
                    if (!GateShut(it.ItemCategory))
                    {
                        if (!SpongeDepth(__0, it, town, __state)) continue;
                        if (byDepth == null) byDepth = new HashSet<ItemObject>();
                        byDepth.Add(it);
                    }
                    if (back == null) back = new List<KeyValuePair<ItemObject, int>>();
                    bool dup = false;
                    foreach (var kv in back) if (kv.Key == it) { dup = true; break; }
                    if (!dup) back.Add(new KeyValuePair<ItemObject, int>(it, extra));
                }
                if (back == null) return;
                foreach (var kv in back)
                {
                    var el = new EquipmentElement(kv.Key);
                    pack.AddToCounts(el, -kv.Value);
                    shelf.AddToCounts(el, kv.Value);
                    int refund = Math.Min(town.Gold, (int)(kv.Value * (float)town.GetItemPrice(el, __0, false)));
                    if (refund > 0)
                    {
                        town.ChangeGold(-refund); __0.PartyTradeGold += refund;
                        MoneyLedger.Note169(MoneyLedger.N169Other, __1, -refund, TownFolk.BBack);   // jak CaravanBulk.LeftPostfix: linia kas (tylko licznik)
                    }
                    _spongeN += kv.Value; _spongeG += Math.Max(0, refund);
                    if (byDepth != null && byDepth.Contains(kv.Key)) _dSpongeN += kv.Value;   // 294b
                }
            }
            catch (Exception e) { Stumble("LeftPostfix", e); }
        }

        // ------------------------------------------------------------ F: wielkosc karawany wedlug handlu (RecruitmentCampaignBehavior.CheckRecruiting)
        public static bool SizePrefix(MobileParty __0, Settlement __1)
        {
            if (__0 == null || __1 == null || !__0.IsCaravan || !__1.IsTown) return true;
            // 285 (klucz CaravanGuardByRisk): straz wedlug drogi i majatku - najem do potrzeby i zwolnienie nadwyzki (CaravanGuards285); false = cialo gry pominiete
            if (!CaravanGuards285.Recruit(__0, __1)) return false;
            try
            {
                _fCalls++;
                int today = (int)CampaignTime.Now.ToDays;
                Acc a;
                if (!_acc.TryGetValue(__0, out a) || today - a.First < MeasureDays) return true;
                bool any;
                long m = SumLast(a, today, Ring, out any);
                double perDay = m / (double)Math.Min(Ring, Math.Max(1, today - a.First + 1));
                if (perDay >= Math.Max(0, __0.TotalWage)) return true;
                _fWould++;
                var s = Settings.Current;
                if (s != null && s.CaravanSizeByTrade) { _fSkip++; return false; }
            }
            catch (Exception e) { Stumble("SizePrefix", e); }
            return true;
        }

        // ------------------------------------------------------------ zapis arm_cs284
        internal static string Export()
        {
            var sb = new StringBuilder(2048);
            sb.Append("cs284|").Append(_days).Append('|');
            bool first = true;
            foreach (var kv in _ema)
            {
                var id = kv.Key != null ? kv.Key.StringId : null;
                if (string.IsNullOrEmpty(id) || id.IndexOf(';') >= 0 || id.IndexOf(':') >= 0 || id.IndexOf('|') >= 0) continue;
                int x; _soldToday.TryGetValue(kv.Key, out x);
                sb.Append(first ? "" : ";").Append(id).Append(':').Append(kv.Value.ToString("R", Inv)).Append(':').Append(x);
                first = false;
            }
            return sb.ToString();
        }

        internal static void Import(string s)
        {
            _impHad = !string.IsNullOrEmpty(s);
            if (!_impHad) return;
            try
            {
                var p = s.Split('|');
                int days;
                if (p.Length != 3 || p[0] != "cs284" || !int.TryParse(p[1], NumberStyles.Integer, Inv, out days) || days < 0) { _impBad++; return; }
                _days = days;
                var om = MBObjectManager.Instance;
                foreach (var rec in p[2].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var f = rec.Split(':');
                    float ema; int today;
                    ItemCategory c = null;
                    try { c = f.Length == 3 && om != null ? om.GetObject<ItemCategory>(f[0]) : null; } catch { c = null; }
                    if (c == null || !float.TryParse(f[1], NumberStyles.Float, Inv, out ema) || !int.TryParse(f[2], NumberStyles.Integer, Inv, out today) || float.IsNaN(ema) || ema < 0f || today < 0) { _impBad++; continue; }
                    _ema[c] = ema; if (today > 0) _soldToday[c] = today; _impOk++;
                }
            }
            catch (Exception e) { _impBad++; Stumble("Import", e); }
        }

        // ------------------------------------------------------------ latki
        internal static void ApplyAll(Harmony h)
        {
            var parts = new List<string>();
            try
            {
                var bkc = AccessTools.TypeByName("BannerKings.Behaviours.BKCaravansBehavior");
                var sell = bkc != null ? AccessTools.DeclaredMethod(bkc, "SellGoods", new[] { typeof(MobileParty), typeof(Town), typeof(float), typeof(bool) }) : null;
                var apply = AccessTools.DeclaredMethod(typeof(SellItemsAction), "ApplyInternal");
                var ps = apply != null ? apply.GetParameters() : null;
                bool okApply = apply != null && apply.IsStatic && ps.Length >= 4 && ps[0].ParameterType == typeof(PartyBase) && ps[1].ParameterType == typeof(PartyBase)
                               && ps[2].ParameterType == typeof(ItemRosterElement) && ps[3].ParameterType == typeof(int);
                if (sell != null && okApply)
                {
                    h.Patch(sell, prefix: new HarmonyMethod(typeof(CaravanTrade284), nameof(SellGoodsPre)), finalizer: new HarmonyMethod(typeof(CaravanTrade284), nameof(SellGoodsFin)));
                    _wSell = true;
                    h.Patch(apply, prefix: new HarmonyMethod(typeof(CaravanTrade284), nameof(SellGuardPrefix)) { priority = Priority.First });
                    _wGuard = true;
                }
                parts.Add("BK SellGoods + SellItemsAction.ApplyInternal (nie ponizej kosztu) " + (_wSell && _wGuard ? "wpiete" : "NIE (brak metody BK albo inna sygnatura)"));
                // poprawka 132a: sprzedaz gry przy wjezdzie/wyjezdzie karawany (CaravansCampaignBehavior.SellGoods, prywatna) - ta sama straz kosztu
                var van = _wGuard ? AccessTools.DeclaredMethod(typeof(CaravansCampaignBehavior), "SellGoods", new[] { typeof(MobileParty), typeof(Town), typeof(float), typeof(bool) }) : null;
                if (van != null)
                {
                    h.Patch(van, prefix: new HarmonyMethod(typeof(CaravanTrade284), nameof(VanSellPre)), finalizer: new HarmonyMethod(typeof(CaravanTrade284), nameof(VanSellFin)));
                    _wVan = true;
                }
                parts.Add("sprzedaz gry CaravansCampaignBehavior.SellGoods (nie ponizej kosztu, poprawka 132a) " + (_wVan ? "wpieta" : "NIE"));
                var bkp = AccessTools.TypeByName("BannerKings.Behaviours.BKPartyBehavior");
                var left = bkp != null ? AccessTools.DeclaredMethod(bkp, "OnSettlementLeft", new[] { typeof(MobileParty), typeof(Settlement) }) : null;
                if (left != null)
                {
                    h.Patch(left, prefix: new HarmonyMethod(typeof(CaravanTrade284), nameof(LeftPrefix)), postfix: new HarmonyMethod(typeof(CaravanTrade284), nameof(LeftPostfix)));
                    _wLeft = true;
                }
                parts.Add("gabka BK (OnSettlementLeft) " + (_wLeft ? "wpieta" : "NIE"));
                var rec = AccessTools.Method(typeof(RecruitmentCampaignBehavior), "CheckRecruiting", new[] { typeof(MobileParty), typeof(Settlement) });
                if (rec != null)
                {
                    h.Patch(rec, prefix: new HarmonyMethod(typeof(CaravanTrade284), nameof(SizePrefix)));
                    _wSize = true;
                }
                parts.Add("werbunek karawan (CheckRecruiting) " + (_wSize ? "wpiety" : "NIE"));
                if (_wSize) CaravanGuards285.Apply(h);   // recenzja 285: ocena strazy przy wyjezdzie BK (postfiks HourlyTickPartyImpl - cel nastepnego odcinka znany)
                parts.Add("straz 285 przy wyjezdzie BK (HourlyTickPartyImpl) " + (CaravanGuards285.DepartWired ? "wpieta" : "NIE"));
            }
            catch (Exception e) { parts.Add("wyjatek " + e.GetType().Name); Log.Error("CaravanTrade284.ApplyAll", e); }
            Log.Info("CaravanTrade284 (284): " + string.Join(", ", parts.ToArray()) + "; zasady zakupu BK - postfiks CalculateBuyValue w CaravanGoods (BuyGoods).");
        }

        // ------------------------------------------------------------ doba
        private static string YN(bool b) { return b ? "TAK" : "NIE"; }

        private static double Med(List<double> l)
        {
            if (l == null || l.Count == 0) return double.NaN;
            l.Sort();
            return l.Count % 2 == 1 ? l[l.Count / 2] : 0.5 * (l[l.Count / 2 - 1] + l[l.Count / 2]);
        }

        /// <summary>287: linia doby "Handel z roznicy cen (287)" - tylko pomiar (bramka liczy sie w GateShut). Roznica cen miast: indeks cwiartki tanszej (p25 - tam
        /// karawana kupuje) i drozszej (p75 - tam sprzedaje) kategorii w 97 miastach, marza po karze handlowej (mediana probki karawan, w obie strony) i mycie;
        /// koszt drogi: zold karawan swiata na dobe wobec ich jukow po koszcie - marza / ten udzial = ile dob drogi marza unosi.</summary>
        private static string Line287(Settings s, int day, List<ItemCategory> rows, List<double> pdL, List<double> wgL,
                                      List<double> penL, int nCover, double wAll, double toll, double cAll)
        {
            var sb = new StringBuilder(3000);
            try
            {
                double pen = penL.Count > 0 ? Med(penL) : 0.0;
                if (double.IsNaN(pen) || pen < 0 || pen > 1) pen = 0.0;
                int shut = 0, shutOld = 0; double mAll = 0, eAll = 0; long hAll = 0;
                var mg = new List<double>(); int pos = 0;
                var cat = new StringBuilder(1600);
                int k = 0;
                foreach (var c in rows)
                {
                    long h = Held(c); hAll += h;
                    float m = Mkt(c), e = Ema(c);
                    mAll += m; eAll += e;
                    if (GateShut(c)) shut++;
                    if (s.CaravanBuyByFlow && _days >= FlowMinDays && h > CapOld(c)) shutOld++;
                    var ix = new List<double>();
                    foreach (var t in Town.AllTowns) if (t != null && t.IsTown) ix.Add(t.GetItemCategoryPriceIndex(c));
                    double p25 = double.NaN, p75 = double.NaN, mr = double.NaN;
                    if (ix.Count >= 4)
                    {
                        ix.Sort();
                        p25 = ix[ix.Count / 4]; p75 = ix[(3 * ix.Count) / 4];
                        if (p25 > 0) { mr = (p75 / (1.0 + pen) * (1.0 - toll)) / (p25 * (1.0 + pen)) - 1.0; mg.Add(mr); if (mr > 0) pos++; }
                    }
                    if (k++ > 0) cat.Append(", ");
                    cat.Append(c.StringId).Append(' ').Append(m.ToString("0.0", Inv)).Append('/').Append(e.ToString("0.0", Inv)).Append('/')
                       .Append(double.IsNaN(p25) ? "-" : p25.ToString("0.00", Inv)).Append('-').Append(double.IsNaN(p75) ? "-" : p75.ToString("0.00", Inv)).Append('/')
                       .Append(double.IsNaN(mr) ? "-" : (100.0 * mr).ToString("0", Inv) + "%");
                }
                if (k == 0) cat.Append('-');
                double mgMed = Med(mg), q = cAll > 0 ? wAll / cAll : double.NaN;
                double pdMed = Med(pdL), wgMed = Med(wgL);
                sb.Append("Handel z roznicy cen (287): dzien ").Append(day).Append(" | klucz popyt rynkow ").Append(YN(s.CaravanFlowByMarketDemand))
                  .Append(" | bramka: kategorii ").Append(rows.Count).Append(", kupno stoi ").Append(shut).Append(" (wedlug samej sprzedazy karawan ").Append(shutOld)
                  .Append("), popyt rynkow razem ").Append(mAll.ToString("0", Inv)).Append(" szt./d, sprzedaz karawan miastom ").Append(eAll.ToString("0.0", Inv))
                  .Append(" szt./d, w jukach ").Append(hAll).Append(" szt.")
                  .Append(" | karawany zmierzone (>= ").Append(MeasureDays).Append(" dob) ").Append(pdL.Count).Append(": wynik handlu/d mediana ")
                  .Append(double.IsNaN(pdMed) ? "-" : pdMed.ToString("0", Inv)).Append(" zl, zold/d mediana ").Append(double.IsNaN(wgMed) ? "-" : wgMed.ToString("0", Inv))
                  .Append(" zl, wynik >= zold ").Append(nCover)
                  .Append(" | roznica cen miast (indeks cwiartki tanszej / drozszej, kara handlowa ").Append((100.0 * pen).ToString("0.0", Inv)).Append("%, myto ")
                  .Append((100.0 * toll).ToString("0.0", Inv)).Append("%): kategorii ").Append(mg.Count).Append(", z marza > 0 ").Append(pos).Append(", mediana marzy ")
                  .Append(double.IsNaN(mgMed) ? "-" : (100.0 * mgMed).ToString("0", Inv) + "%")
                  .Append(" | koszt drogi: zold karawan ").Append(wAll.ToString("0", Inv)).Append(" zl/d, juki po koszcie ").Append(((long)cAll).ToString(Inv))
                  .Append(" zl, zold doby ").Append(double.IsNaN(q) ? "-" : (100.0 * q).ToString("0.00", Inv) + "%").Append(" jukow, mediana marzy unosi ")
                  .Append(double.IsNaN(q) || double.IsNaN(mgMed) || q <= 0 ? "-" : (mgMed / q).ToString("0.0", Inv)).Append(" dob drogi")
                  .Append(" | wedlug kategorii (popyt rynkow/d / sprzedaz karawan/d / indeks p25-p75 / marza): ").Append(cat)
                  .Append(" | potkniecia 287 ").Append(_mktStum).Append(" (sesja).");
            }
            catch (Exception e) { _mktStum++; Stumble("Line287", e); return "Handel z roznicy cen (287): dzien " + day + " | blad linii | potkniecia 287 " + _mktStum + " (sesja)."; }
            return sb.ToString();
        }

        /// <summary>298 M6 (tylko odczyt): przyczyna, ze karawana nic nie sprzedala w 7 dobach. 0 pusta kiesa (bez towaru handlowego, pol kiesy < 3 doby zoldu - regula 3
        /// zakupu), 1 bez towaru z kiesa, 2 caly towar bez rynku z zyskiem wedlug glebokosci (cena ostatniej sztuki w miescie odniesienia po mycie, DepthNetAged, nizsza
        /// od kosztu sztuki z ksiegi - HeldCost, bez Sync), 3 towar z rynkiem z zyskiem - czeka, 4 towar bez miary. Spizarnia eskorty poza towarem (PantryCopy).</summary>
        private static int IdleWhy(MobileParty mp)
        {
            var r = mp.ItemRoster;
            var pan = R2On(Settings.Current) ? PantryCopy(mp) : null;
            bool goods = false, measured = false;
            HashSet<ItemObject> seen = null;
            for (int i = 0; r != null && i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0 || !it.IsTradeGood || it.HasHorseComponent || !Tracked(it.ItemCategory)) continue;
                if (seen == null) seen = new HashSet<ItemObject>();
                if (!seen.Add(it)) continue;
                int all = r.GetItemNumber(it), keep = 0;
                if (pan != null) pan.TryGetValue(it, out keep);
                if (all - Math.Max(0, keep) <= 0) continue;
                goods = true;
                double unit = CaravanGoods.HeldCost(mp, it, all) / Math.Max(1, all);
                double net = DepthNetAged(mp, it);
                if (double.IsNaN(net) || !(unit > 0.0)) continue;
                measured = true;
                if (net >= unit) return 3;
            }
            if (!goods) { int purse = mp.PartyTradeGold; return purse <= 0 || 0.5 * purse < CashDays * Math.Max(0, mp.TotalWage) ? 0 : 1; }
            return measured ? 2 : 4;
        }

        /// <summary>298 (tylko pomiar): linia doby "Karawany bez handlu i kohorta (298)" - karawany bez sprzedazy w 7 dobach (ksiega zysku 284, ta sama miara co
        /// 'karawany 7 dob: z handlem') wedlug przyczyny (IdleWhy) i karawany powstale od 31. doby biegu (bez jukow sprzed testu): placace zold / tracace, mediana
        /// (wynik handlu - zold)/d - ta sama miara co linia 294. Nic nie zmienia w grze; wyjatek na karawane - licznik potkniec.</summary>
        private static string Line298(int day, int nCar, List<MobileParty> idle, int n, int w, List<double> net, Dictionary<ItemCategory, double> cost)
        {
            var why = new int[5];
            foreach (var mp in idle)
            {
                _in298++;   // recenzja: bledy pomocnikow glebokosci (DStumble) z tego pomiaru - w potknieciach 298
                try { why[IdleWhy(mp)]++; }
                catch (Exception e) { M298Stumble("Idle", e); }
                finally { _in298--; }
            }
            net.Sort();
            // recenzja (kontrola L1): juki psujacych po koszcie wobec wszystkich jukow po koszcie (przeglad doby ScanHeld - ze spizarnia eskorty, jak 'juki po koszcie' 287)
            double cs = 0.0, ca = 0.0;
            try { if (cost != null) foreach (var kv in cost) { ca += kv.Value; if (SpoilCat(kv.Key)) cs += kv.Value; } }
            catch (Exception e) { M298Stumble("Cost", e); cs = ca = 0.0; }
            var sb = new StringBuilder(600);
            sb.Append("Karawany bez handlu i kohorta (298): dzien ").Append(day)
              .Append(" | bez sprzedazy w 7 dobach ").Append(idle.Count).Append(" z ").Append(nCar)
              .Append(": pusta kiesa (bez towaru, pol kiesy < ").Append(CashDays).Append(" doby zoldu) ").Append(why[0])
              .Append(", bez towaru z kiesa ").Append(why[1]).Append(", towar bez rynku z zyskiem wedlug glebokosci ").Append(why[2])
              .Append(", towar z rynkiem z zyskiem - czeka ").Append(why[3]).Append(", towar bez miary ").Append(why[4])
              .Append(" | karawany od ").Append(CohortDay + 1).Append(". doby biegu (doba powstania >= ").Append(_day0 != int.MinValue ? (_day0 - 1 + CohortDay).ToString(Inv) : "-")
              .Append("): zmierzone ").Append(n).Append(", placace ").Append(w).Append(", tracace ").Append(n - w)
              .Append(", udzial placacych ").Append(n > 0 ? (w / (double)n).ToString("0.000", Inv) : "-")
              .Append(", mediana wyniku - zoldu/d ").Append(net.Count > 0 ? net[net.Count / 2].ToString("0", Inv) : "-").Append(" zl")
              .Append(" | jedzenie ponizej kosztu wszystkimi drogami ").Append(_fLossN).Append(" szt. za ").Append(_fLossG).Append(" zl (koszt ").Append(_fLossC)
              .Append(" zl), w tym psujace ").Append(_fsLossN).Append(" szt. za ").Append(_fsLossG).Append(" zl (koszt ").Append(_fsLossC).Append(" zl)")
              .Append("; kupno psujacych ").Append(_fBuyN).Append(" szt., w miastach z pomiarem glodu KS ").Append(_fBuyKs).Append(" szt. (glod miasta zakupu srednio ")
              .Append(_fBuyKs > 0 ? (100.0 * _fBuyU / _fBuyKs).ToString("0.0", Inv) + "%" : "-").Append(")")
              .Append("; juki psujace po koszcie ").Append(((long)cs).ToString(Inv)).Append(" zl z ").Append(((long)ca).ToString(Inv)).Append(" zl (udzial ")
              .Append(ca > 0.0 ? (cs / ca).ToString("0.000", Inv) : "-").Append(", ze spizarnia eskorty)")
              .Append(" | potkniecia 298 ").Append(_m298Stum).Append(" (sesja ").Append(_m298StumSess).Append(").");
            _m298Stum = 0;
            return sb.ToString();
        }

        /// <summary>290: linia doby "Jedzenie karawan (290)" - kategorie jedzenia z jukami albo sprzedaza: juki, wlasna sprzedaz karawan miastom (14 dob), dni jukow
        /// przy tej sprzedazy, popyt rynkow (287), prog bramki (Cap - miara Flow przy kluczu) i stan. Tylko odczyt.</summary>
        private static string Line290(Settings s, int day, List<ItemCategory> rows)
        {
            try
            {
                var sb = new StringBuilder(1600);
                long h = 0; double e = 0, mk = 0; int k = 0, shut = 0;
                var parts = new StringBuilder(1200);
                foreach (var c in rows)
                {
                    if (!IsFoodCat(c)) continue;
                    long hc = Held(c); float ec = Ema(c), mc = Mkt(c);
                    bool sh = GateShut(c);
                    k++; if (sh) shut++; h += hc; e += ec; mk += mc;
                    if (parts.Length > 0) parts.Append(", ");
                    parts.Append(c.StringId).Append(' ').Append(hc).Append('/').Append(ec.ToString("0.0", Inv)).Append('/')
                         .Append(ec > 0.05f ? (hc / (double)ec).ToString("0.0", Inv) : "-").Append('/').Append(mc.ToString("0.0", Inv)).Append('/')
                         .Append(Cap(c).ToString("0", Inv)).Append('/').Append(sh ? "KUPNO STOI" : "CZYNNE");
                    if (R2Full(s)) parts.Append(SpoilCat(c) ? "/psujace" : "/trwale");   // 294b: rodzaj (psujace - 7 dob zbytu, trwale - jak towar)
                    if (R2Full(s) && SpoilCat(c))
                    {
                        // 298 L1: N miast placacych, z ktorych liczy sie prog (tylko gdy regula L1 dala miare - ta sama chwila co Cap wyzej)
                        int nl; double lgl;
                        if (SpoilLegFlow(c, out nl, out lgl) >= 0.0) parts.Append('/').Append(nl);
                    }
                }
                sb.Append("Jedzenie karawan (290): dzien ").Append(day).Append(" | klucz kupno jedzenia wedlug wlasnej sprzedazy ").Append(YN(s.CaravanFoodBuyOwnSales))
                  .Append(" | kategorii jedzenia ").Append(k).Append(", kupno stoi ").Append(shut)
                  .Append(" | w jukach ").Append(h).Append(" szt., sprzedaz karawan miastom ").Append(e.ToString("0.0", Inv)).Append(" szt./d (")
                  .Append(e > 0.05 ? (h / e).ToString("0.0", Inv) : "-").Append(" dob), popyt rynkow ").Append(mk.ToString("0.0", Inv)).Append(" szt./d")
                  .Append(" | wedlug kategorii (juki / sprzedaz karawan/d / dni / popyt rynkow/d / prog / stan): ").Append(parts.Length > 0 ? parts.ToString() : "-")
                  .Append(" | 294b: bramka na towar handlowy ").Append(YN(R2Full(s))).Append(", spizarnia eskorty ").Append(_pantryN)
                  .Append(" szt. (poza bramka), miast z targiem ").Append(NTowns()).Append(" - prog dolny psujacych (srednie miasto) = popyt rynkow / miasta x ").Append(FoodDays)
                  .Append(" dob, trwale jak towar ").Append(Math.Max(1, s.CaravanBulkStockDays)).Append(" dob")
                  .Append("; 298: psujace - miasta placace ").Append(YN(s.CaravanFoodGateByPayingTowns && DepthOn))
                  .Append(" (prog = 7 x max(sprzedaz, popyt / miasta, popyt / miasta x N placacych x min(odcinek, 7) / 7), odcinek ")
                  .Append(Stage8World.LegDays > 0.0 ? Stage8World.LegDays.ToString("0.000", Inv) : "-").Append(" dob)")
                  .Append(" | potkniecia 287 ").Append(_mktStum).Append(" (sesja).");
                return sb.ToString();
            }
            catch (Exception ex) { Stumble("Line290", ex); return "Jedzenie karawan (290): dzien " + day + " | blad linii | potkniecia 287 " + _mktStum + " (sesja)."; }
        }

        /// <summary>294b: pierwszy towar handlowy kategorii (bez koni) - wzor ceny w linii doby; raz na sesje.</summary>
        private static ItemObject RepOf(ItemCategory c)
        {
            if (_rep == null)
            {
                var d = new Dictionary<ItemCategory, ItemObject>();
                try
                {
                    var om = MBObjectManager.Instance;
                    var all = om != null ? om.GetObjectTypeList<ItemObject>() : null;
                    if (all != null)
                        foreach (var it in all)
                            if (it != null && it.IsTradeGood && !it.HasHorseComponent && it.ItemCategory != null && !it.NotMerchandise && !d.ContainsKey(it.ItemCategory)) d[it.ItemCategory] = it;
                }
                catch (Exception e) { DStumble("Rep", e); }
                _rep = d;
            }
            ItemObject x;
            return c != null && _rep.TryGetValue(c, out x) ? x : null;
        }

        /// <summary>294b: linia doby "Glebokosc rynku karawan (294b)" - liczniki zakupu (regula 4), starego towaru (AgeMarket, regula 5), gabki, spizarnia eskorty,
        /// zmierzony odcinek; wedlug kategorii: juki swiata, N miast drozszych, indeks miasta odniesienia, udzial innych (ceil(juki / N)), zjedzone w dobie drogi,
        /// k (sztuka karawany bez wlasnych w kolejce), cena k-tej po mycie (probka karawany AI), cena kupna w miescie cwiartki tanszej (p25), stan wedlug marzy 3%.</summary>
        private static string LineDepth(Settings s, int day, List<ItemCategory> rows, MobileParty sample)
        {
            var sb = new StringBuilder(4000);
            try
            {
                var cat = new StringBuilder(2600);
                int k0 = 0, open = 0, stop = 0;
                foreach (var c in rows)
                {
                    if (!Tracked(c)) continue;
                    var r = DepthOf(c);
                    var rep = RepOf(c);
                    long h = Held(c);
                    int n = r != null ? Math.Max(1, r.N) : 0;
                    long share = n > 0 ? (long)Math.Ceiling(h / (double)n) : 0;
                    double eat = LegEat(c);
                    int k = (int)Math.Max(1.0, Math.Min(1000000.0, 1 + share - Math.Ceiling(eat)));
                    double pk = sample != null && rep != null ? DepthNet(sample, new EquipmentElement(rep), k) : double.NaN;
                    double buy = double.NaN;
                    if (sample != null && rep != null)
                    {
                        var ix = new List<KeyValuePair<float, Town>>();
                        foreach (var t in Town.AllTowns) if (t != null && t.IsTown && t.MarketData != null) ix.Add(new KeyValuePair<float, Town>(t.GetItemCategoryPriceIndex(c), t));
                        if (ix.Count >= 4)
                        {
                            ix.Sort((x, y) => x.Key.CompareTo(y.Key));
                            buy = ix[ix.Count / 4].Value.GetItemPrice(new EquipmentElement(rep), sample, false);
                        }
                    }
                    string st = double.IsNaN(pk) || double.IsNaN(buy) || !(buy > 0) ? "BEZ MIARY" : (pk >= CarrierMargin * buy ? "CZYNNE" : "STOI");
                    if (st == "CZYNNE") open++; else if (st == "STOI") stop++;
                    if (k0++ > 0) cat.Append(", ");
                    cat.Append(c.StringId).Append(' ').Append(h).Append('/').Append(n).Append('/').Append(r != null ? r.Idx.ToString("0.00", Inv) : "-").Append('/')
                       .Append(share).Append('/').Append(eat.ToString("0.0", Inv)).Append('/').Append(k).Append('/')
                       .Append(double.IsNaN(pk) ? "-" : pk.ToString("0.0", Inv)).Append('/').Append(double.IsNaN(buy) ? "-" : buy.ToString("0", Inv)).Append('/').Append(st);
                }
                if (k0 == 0) cat.Append('-');
                double leg = Stage8World.LegDays;
                sb.Append("Glebokosc rynku karawan (294b): dzien ").Append(day)
                  .Append(" | klucze: cena sztuki wedlug glebokosci rynku ").Append(YN(DepthOn)).Append(", bramka jedzenia na towar handlowy ").Append(YN(R2On(s)))
                  .Append(", jedzenie na zmierzona droge ").Append(YN(s.CaravanRoadFoodMeasuredLeg && s.CaravanRoadFood))
                  .Append(" | kupno: ocen ").Append(_dEval).Append(" (kategoria x wizyta), stoi wedlug glebokosci ").Append(_dStop).Append(" (stara regula ofert stalaby ")
                  .Append(_dOld).Append("), czynne ").Append(_dOpen).Append(", bez miary ").Append(_dNone).Append("; gabka cofnieta wedlug glebokosci ").Append(_dSpongeN).Append(" szt.")
                  .Append(" | wiek > 30 dob: czeka (rynek z glebokoscia) ").Append(_dAgeWait).Append(", bez rynku z glebokoscia - na stracie porcjami T1 ").Append(_dLossN)
                  .Append(" szt. za ").Append(_dLossG).Append(" zl (koszt ").Append(_dLossC).Append(" zl), BK i gra wypuszczone ").Append(_dBk5N).Append(" szt. (koszt ")
                  .Append(_dBk5C).Append(" zl) (stara regula: czekalby ").Append(_dAgeOldWait).Append("; sprzedane, choc stara regula czekalaby ").Append(_dLossOldN)
                  .Append(" szt.), wstrzymane do miasta drozszego (cena w miescie odniesienia / 1.03) T1 ").Append(_dHeldT1).Append(", BK i gra ").Append(_dHeldBk).Append(" szt.")
                  .Append(" | jedzenie: spizarnia eskorty ").Append(_pantryN).Append(" szt. poza bramka")
                  .Append(" | dni drogi zmierzone ").Append(leg > 0.0 ? leg.ToString("0.00", Inv) : "-").Append(" (karawany / wyjazdy, ").Append(Stage8World.LegSamples).Append(" dob)")
                  .Append(" | marza przewoznika ").Append((100.0 * (CarrierMargin - 1.0)).ToString("0", Inv)).Append("%, kategorii czynne ").Append(open).Append(", stoi ").Append(stop)
                  .Append(" | wedlug kategorii (juki swiata / miast drozszych N / miasto odniesienia indeks / udzial innych / zjedzone w dobie drogi / k / cena k-tej po mycie / cena kupna p25 / CZYNNE|STOI): ")
                  .Append(cat)
                  .Append(" | potkniecia 294b ").Append(_dStum).Append(" (sesja ").Append(_dStumSess).Append(").");
            }
            catch (Exception e) { DStumble("Line", e); sb.Clear(); sb.Append("Glebokosc rynku karawan (294b): dzien ").Append(day).Append(" | blad linii | potkniecia 294b ").Append(_dStum).Append(" (sesja ").Append(_dStumSess).Append(")."); }
            return sb.ToString();
        }

        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            long t0 = Stopwatch.GetTimestamp();
            int today = (int)CampaignTime.Now.ToDays, day = today - 1;
            try
            {
                if (!_impLogged)
                {
                    _impLogged = true;
                    Log.Info("Karawany przeplyw towarow (284): " + (_impHad ? "wczytano pomiar sprzedazy karawan - kategorii " + _impOk + ", dob pomiaru " + _days + ", nieczytelnych " + _impBad
                                                                          : "zapis bez klucza arm_cs284 - pomiar od zera, bramka kupna wszystkich towarow po " + FlowMinDays + " dobach") + ".");
                }
                if (_day0 == int.MinValue) _day0 = today;   // 298: pierwsza doba sesji (tylko pomiar)
                // 1. srednia sprzedazy (pierwsze 14 dob srednia zwykla, potem wygladzenie 1/14 - jak 282)
                if (_days < int.MaxValue) _days++;
                var cats = new HashSet<ItemCategory>(_ema.Keys);
                foreach (var k in _soldToday.Keys) cats.Add(k);
                _soldLast.Clear();
                foreach (var c in cats)
                {
                    int x; _soldToday.TryGetValue(c, out x);
                    float e = Ema(c);
                    e += (x - e) / Math.Min(FlowSmooth, _days);
                    _ema[c] = e; _soldLast[c] = x;
                }
                _soldToday.Clear();
                // 2. przeglad karawan: juki wedlug kategorii, zysk zrealizowany 7 dob, F
                _held.Clear(); _heldReady = true; _pantryN = 0;
                MobileParty sample = null;                                          // 294b: probka karawany AI (kara handlu, myto) dla linii glebokosci
                var cost = new Dictionary<ItemCategory, double>();
                var reals = new List<long>();
                int nCar = 0, withTrade = 0, withProfit = 0, fN = 0, fBelow = 0;
                long sum7 = 0; double fMargin = 0, fWage = 0;
                var alive = new HashSet<MobileParty>();
                var pdL = new List<double>(); var wgL = new List<double>(); var penL = new List<double>();   // 287: wynik handlu i zold na dobe, kara handlowa
                int nCover = 0; double wAll = 0, toll = 0;
                // 294: rynek karawan bez konwojow DTE - placace (W), tracace (L), nowe ponizej MeasureDays (T), bez historii (U - sprzed pierwszej doby sesji, bez wpisu
                // w zapisie, jeszcze bez pomiaru), seria stratnych, zwijanie (zaczete dzis / w zwijaniu / wstrzymane), wynik - zold, wynik, zold
                int w294 = 0, l294 = 0, t294 = 0, u294 = 0, s294 = 0, wind294 = 0, wNew294 = 0;
                var wWhy294 = new Dictionary<string, int>();
                bool windOn = s.CaravanLosingWoundUp;
                var n294 = new List<double>(); var p294 = new List<double>(); var g294 = new List<double>();
                var idle298 = new List<MobileParty>(); var net298 = new List<double>(); int n298 = 0, w298 = 0;   // 298: bez sprzedazy w 7 dobach; kohorta od 31. doby
                foreach (var mp in MobileParty.AllCaravanParties)
                {
                    if (mp == null || !mp.IsActive || mp.ItemRoster == null) continue;
                    nCar++; alive.Add(mp);
                    ScanHeld(mp, cost);
                    if (sample == null && mp.ActualClan != Clan.PlayerClan && !Measure31.IsDteConvoy(mp)) sample = mp;
                    try   // 287: zold swiata karawan, kara handlowa (probka do 60 karawan z towarem), myto karawan
                    {
                        wAll += Math.Max(0, mp.TotalWage);
                        if (penL.Count < 60)
                            for (int i = 0; i < mp.ItemRoster.Count; i++)
                            {
                                var it0 = mp.ItemRoster.GetElementCopyAtIndex(i).EquipmentElement.Item;
                                if (it0 != null && it0.IsTradeGood && !it0.HasHorseComponent) { penL.Add(CaravanGoods.Penalty(mp, it0)); break; }
                            }
                        if (toll <= 0) toll = TownToll.CaravanRateFor(mp);
                    }
                    catch (Exception e) { _mktStum++; Stumble("Daily(287)", e); }
                    var a = AccOf(mp, today, day);   // 298: nowy wpis - powstala w dobie zamknietej (Born = day; First = today jak dotad)
                    bool any;
                    long r7 = SumLast(a, day, 7, out any);
                    if (any) { withTrade++; reals.Add(r7); sum7 += r7; if (r7 > 0) withProfit++; }
                    else idle298.Add(mp);   // 298: przyczyna liczona po przegladzie (juki swiata _held kompletne)
                    if (today - a.First >= MeasureDays)
                    {
                        bool any2;
                        double pd = SumLast(a, day, Ring, out any2) / (double)Math.Min(Ring, Math.Max(1, today - a.First));
                        fN++; fMargin += pd; fWage += Math.Max(0, mp.TotalWage);
                        if (pd < Math.Max(0, mp.TotalWage)) fBelow++;
                        pdL.Add(pd); wgL.Add(Math.Max(0, mp.TotalWage)); if (pd >= Math.Max(0, mp.TotalWage)) nCover++;   // 287
                        // 294: seria stratnej karawany (raz na dobe) i rynek karawan bez konwojow DTE (nie handluja)
                        if (a.BelowDay != today) { a.BelowDay = today; a.Below = pd < Math.Max(0, mp.TotalWage) ? a.Below + 1 : 0; }
                        if (!Measure31.IsDteConvoy(mp))
                        {
                            double wg = Math.Max(0, mp.TotalWage);
                            if (pd >= wg) w294++; else { l294++; if (a.Below >= Ring) s294++; }
                            n294.Add(pd - wg); p294.Add(pd); g294.Add(wg);
                            // 298 (pomiar): karawana powstala od 31. doby biegu (caly towar kupiony w biegu, bez jukow sprzed testu)
                            // recenzja: doba powstania (Born), nie First - wpis z przegladu doby ma First = jutro (granica o 1 dobe za wczesnie)
                            if (!a.Old && _day0 != int.MinValue && (a.Born != int.MinValue ? a.Born : a.First) - (_day0 - 1) >= CohortDay) { n298++; if (pd >= wg) w298++; net298.Add(pd - wg); }
                            // 294 (klucz CaravanLosingWoundUp): seria >= 14 dob pomiaru ponizej zoldu i karawana sledzona >= 28 dob - wlasciciel zaczyna zwijac: kupno BK
                            // stoi (Why 6), towar schodzi zwyklymi regulami T1 / BK w nastepnych miastach, rozwiazanie w miescie, gdy gotowa (CaravanFlow.WindHourly)
                            if (windOn && a.Wind == int.MinValue && a.Below >= Ring && today - a.First >= CaravanMarket294.ExitDays)
                            {
                                string why = CaravanFlow.WindBlocker(mp);
                                if (why == null) { a.Wind = today; wNew294++; }
                                else { int q; wWhy294.TryGetValue(why, out q); wWhy294[why] = q + 1; }
                            }
                        }
                    }
                    else if (!Measure31.IsDteConvoy(mp)) { if (a.Old) u294++; else t294++; }   // 294: nowa (jeszcze bez pomiaru) - niczego nie dowiodla; bez historii osobno
                    if (a.Wind != int.MinValue && !windOn) a.Wind = int.MinValue;   // 294: klucz NIE - koniec zwijania (kupno BK jak dotad)
                    if (a.Wind != int.MinValue) wind294++;
                }
                _dayRan = true;
                var gone = new List<MobileParty>();
                foreach (var k in _acc.Keys) if (!alive.Contains(k)) gone.Add(k);
                foreach (var k in gone) _acc.Remove(k);
                reals.Sort();
                string med = reals.Count > 0 ? reals[reals.Count / 2].ToString(Inv) : "-";
                double ms = (Stopwatch.GetTimestamp() - t0 + _ticks) * 1000.0 / Stopwatch.Frequency;

                // linia 1
                var sb = new StringBuilder(2400);
                sb.Append("Handel karawan (284): dzien ").Append(day)
                  .Append(" | klucze: ksiega w zapisie ").Append(YN(s.CaravanCostBookSave)).Append(", bez zoldu w cenie ").Append(YN(s.CaravanSellNoSunkWage))
                  .Append(", nie ponizej kosztu ").Append(YN(s.CaravanBkSellAtCost)).Append(", kupno wedlug sprzedazy ").Append(YN(s.CaravanBuyByFlow))
                  .Append(", kapital obrotowy ").Append(YN(s.CaravanBuyWorkingCapital)).Append(", zysk osiagalny ").Append(YN(s.CaravanBuyProfitCheck))
                  .Append(", surowce masowe wszystkie ").Append(YN(s.CaravanBulkFlowGateAll)).Append(", wielkosc wedlug handlu ").Append(YN(s.CaravanSizeByTrade))
                  .Append(" | sprzedaz wedlug drogi (szt./utarg/myto/koszt/zysk; ponizej kosztu szt./utarg/koszt): ");
                long rAll = 0;
                for (int p = 0; p < Paths; p++)
                {
                    if (p > 0) sb.Append(", ");
                    sb.Append(PathNames[p]).Append(' ').Append(_pN[p]).Append('/').Append(_pG[p]).Append('/').Append(_pT[p]).Append('/').Append(_pC[p]).Append('/').Append(_pR[p])
                      .Append(" (").Append(_pLossN[p]).Append('/').Append(_pLossG[p]).Append('/').Append(_pLossC[p]).Append(')');
                    rAll += _pR[p];
                }
                sb.Append(" | razem zysk zrealizowany ").Append(rAll).Append(" zl")
                  .Append(" | karawany 7 dob: karawan ").Append(nCar).Append(", z handlem ").Append(withTrade).Append(", z zyskiem > 0 ").Append(withProfit)
                  .Append(", mediana zysku ").Append(med).Append(" zl, suma ").Append(sum7).Append(" zl")
                  .Append(" | kupno BK odrzucone (kategoria x wizyta): przesyt ").Append(_rGate).Append(", kapital w towarze ").Append(_rCap).Append(", rezerwa zoldu ").Append(_rWage)
                  .Append(", bez zysku po mycie i drodze ").Append(_rGain).Append(", jedzenie > ").Append(FoodDays).Append(" dob ").Append(_rFood)
                  .Append("; wizyt z zasadami ").Append(_buyVisits).Append(", pominiete (kontrakt, rozkaz gracza, nie handluje) ").Append(_skipExempt)
                  .Append(" | ponizej kosztu wstrzymane: BK ").Append(_bkHeldN).Append(" szt. (koszt ").Append(_bkHeldC).Append(" zl), BK na stracie z wyjscia ").Append(_bkExit)
                  .Append(", surowce masowe ").Append(_bulkHeldN).Append(" szt. (w tym rozladunek ").Append(_bulkHeldGlut).Append(')')
                  .Append(" | gabka BK cofnieta ").Append(_spongeN).Append(" szt. (zwrot ").Append(_spongeG).Append(" zl)")
                  .Append(" | karawany gracza wedlug drogi (utarg/zysk): ");
                for (int p = 0; p < Paths; p++) { if (p > 0) sb.Append(", "); sb.Append(PathNames[p]).Append(' ').Append(_plG[p]).Append('/').Append(_plR[p]); }
                sb.Append(" | latki: BK sprzedaz ").Append(YN(_wSell && _wGuard)).Append(", gabka ").Append(YN(_wLeft)).Append(", werbunek ").Append(YN(_wSize))
                  .Append(" | recenzja 284: wizyt z podloga >= kapital (bez reguly kapitalu) ").Append(_noRoom).Append(", juki przeliczone po wczytaniu ").Append(_heldLazy)
                  .Append(", wstrzymane zdjete z dostaw P1 ").Append(_p1Phantom).Append(" szt.")
                  .Append(" | poprawka 132a: sprzedaz gry ponizej kosztu wstrzymana ").Append(_vanHeldN).Append(" szt. (koszt ").Append(_vanHeldC).Append(" zl), gra na stracie z wyjscia ")
                  .Append(_vanExit).Append(", latka gry ").Append(YN(_wVan)).Append(", dlugo w jukach przy rynku z zyskiem - czeka ").Append(_ageWait)
                  .Append(" | czas ").Append(ms.ToString("0.00", Inv)).Append(" ms | potkniecia ").Append(_stumbles).Append(" (sesja ").Append(_stumbleSess).Append(").");
                Log.Info(sb.ToString());

                // linia 2: przeplyw wedlug kategorii (wszystkie z jukami albo sprzedaza)
                var rows = new List<ItemCategory>();
                foreach (var c in _ema.Keys) if (Held(c) > 0 || Ema(c) > 0.05f) rows.Add(c);
                foreach (var c in _held.Keys) if (!_ema.ContainsKey(c) && Held(c) > 0) rows.Add(c);
                rows.Sort((x, y) => (Held(y) / Math.Max(1.0, Cap(y))).CompareTo(Held(x) / Math.Max(1.0, Cap(x))));
                long hAll = 0; double cAll = 0, eAll = 0; int shut = 0;
                foreach (var c in rows) { hAll += Held(c); double cc; cost.TryGetValue(c, out cc); cAll += cc; eAll += Ema(c); if (GateShut(c)) shut++; }
                var sb2 = new StringBuilder(4000);
                sb2.Append("Przeplyw towarow karawan (284): dzien ").Append(day).Append(" | klucz kupno wedlug sprzedazy ").Append(YN(s.CaravanBuyByFlow))
                   .Append(", dob pomiaru ").Append(_days).Append(" (bramka od ").Append(FlowMinDays).Append(")")
                   .Append(" | kategorii ").Append(rows.Count).Append(", z kupnem stoi ").Append(shut)
                   .Append(" | w jukach razem ").Append(hAll).Append(" szt. (koszt ").Append(((long)cAll).ToString(Inv)).Append(" zl), sprzedaz 14 dob ").Append(eAll.ToString("0.0", Inv)).Append("/d")
                   .Append(" | wedlug kategorii (w jukach / sprzedaz 14 dob / prog / stan): ");
                int n = 0;
                foreach (var c in rows)
                {
                    if (n++ > 0) sb2.Append(", ");
                    sb2.Append(c.StringId).Append(' ').Append(Held(c)).Append(" / ").Append(Ema(c).ToString("0.0", Inv)).Append("/d / ").Append(Cap(c).ToString("0", Inv))
                       .Append(" / ").Append(GateShut(c) ? "KUPNO STOI" : "CZYNNE");
                }
                if (n == 0) sb2.Append('-');
                sb2.Append('.');
                Log.Info(sb2.ToString());

                // linia 287: bramka wedlug popytu rynkow, roznica cen miast wobec kosztu drogi, wynik handlu karawan wobec zoldu
                Log.Info(Line287(s, day, rows, pdL, wgL, penL, nCover, wAll, toll, cAll));

                // linia 290 (tylko pomiar): jedzenie w jukach wobec wlasnej sprzedazy karawan i popytu rynkow, stan bramki (miara Flow)
                if (s.CaravanFlowByMarketDemand) Log.Info(Line290(s, day, rows));

                // linia 294b (takze przy kluczach NIE - pomiar): glebokosc rynku, stary towar, spizarnia eskorty, dni drogi
                Log.Info(LineDepth(s, day, rows, sample));

                // linia 294: rynek karawan (jedzenie wedlug rodzaju, zwijanie stratnych, nowe karawany wedlug miejsca) - takze przy kluczach NIE (pomiar)
                Log.Info(CaravanMarket294.Daily(day, w294, l294, t294, u294, s294, n294, p294, g294, wNew294, wind294, wWhy294, _rWind, _bookHad, _bookOk, _bookGone, _bookBad));

                // linia 298 (tylko pomiar): karawany bez sprzedazy w 7 dobach wedlug przyczyny i kohorta karawan od 31. doby biegu
                Log.Info(Line298(day, nCar, idle298, n298, w298, net298, cost));

                // linia 4 (F, na sucho przy kluczu NIE)
                Log.Info(new StringBuilder(500).Append("Wielkosc karawan (284, na sucho): dzien ").Append(day).Append(" | klucz Caravan Size By Trade ").Append(YN(s.CaravanSizeByTrade))
                    .Append(" | karawan zmierzonych (>= ").Append(MeasureDays).Append(" dob) ").Append(fN).Append(": zysk handlu/d ponizej zoldu/d ").Append(fBelow).Append(", nie ponizej ").Append(fN - fBelow)
                    .Append("; suma zysku handlu/d ").Append(fMargin.ToString("0", Inv)).Append(" zl, suma zoldu/d ").Append(fWage.ToString("0", Inv)).Append(" zl")
                    .Append(" | proby werbunku karawan ").Append(_fCalls).Append(", regula pominelaby ").Append(_fWould).Append(", pominela ").Append(_fSkip).Append('.').ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ZeroDay(); }
        }
    }
}
