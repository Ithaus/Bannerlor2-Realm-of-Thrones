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
        private sealed class Acc { public readonly long[] R = new long[Ring]; public readonly int[] D = new int[Ring]; public int First; }
        private static readonly Dictionary<MobileParty, Acc> _acc = new Dictionary<MobileParty, Acc>();

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

        internal static void Reset()
        {
            _soldToday.Clear(); _soldLast.Clear(); _ema.Clear(); _days = 0; _held.Clear(); _heldReady = false; _food = null;
            _bkWithheld.Clear(); _bkWithheldMp = null;
            _impOk = _impBad = 0; _impHad = _impLogged = false;
            _acc.Clear(); _sellIdx.Clear(); _sellDay = -1;
            Path = 0; _bkSelling = 0; _vanSelling = 0; _b.Mp = null;
            _stumbleSess = 0; _err.Clear();
            _g285N = _g285C = 0; _g285Acts = 0; _g285By.Clear();   // 285
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
            return IsFoodCat(c) ? FoodDays : Math.Max(1, s != null ? s.CaravanBulkStockDays : 10);
        }

        private static float Ema(ItemCategory c) { float e; return _ema.TryGetValue(c, out e) ? e : 0f; }
        private static long Held(ItemCategory c) { long h; return _held.TryGetValue(c, out h) ? h : 0; }
        private static float Cap(ItemCategory c) { return Ema(c) * DaysOf(c); }

        /// <summary>Bramka przeplywu (klucz CaravanBuyByFlow): po FlowMinDays dobach pomiaru wszystkie karawany razem wioza wiecej niz sprzedaja miastom w 10 dob (jedzenie 7).</summary>
        internal static bool GateShut(ItemCategory c)
        {
            var s = Settings.Current;
            if (s == null || !s.CaravanBuyByFlow || _days < FlowMinDays || !Tracked(c)) return false;
            EnsureHeld();
            return Held(c) > Cap(c);
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
            _held.Clear();
            long n = 0;
            foreach (var mp in MobileParty.AllCaravanParties)
                if (mp != null && mp.IsActive && mp.ItemRoster != null) n += ScanHeld(mp, null);
            _heldLazy++;
            Log.Info("Karawany przeplyw towarow (284): juki karawan przeliczone po wczytaniu przed pierwsza doba - kategorii " + _held.Count + ", sztuk " + n + ".");
        }

        /// <summary>Juki jednej karawany wedlug kategorii do _held (i koszt, gdy cost != null) - przeglad doby i EnsureHeld.</summary>
        private static long ScanHeld(MobileParty mp, Dictionary<ItemCategory, double> cost)
        {
            var r = mp.ItemRoster;
            HashSet<ItemObject> seen = null;
            long n = 0;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0 || !Tracked(it.ItemCategory)) continue;
                if (seen == null) seen = new HashSet<ItemObject>();
                if (!seen.Add(it)) continue;
                int have = r.GetItemNumber(it);
                var c = it.ItemCategory;
                _held[c] = Held(c) + have; n += have;
                if (cost != null) { double c0; cost.TryGetValue(c, out c0); cost[c] = c0 + CaravanGoods.HeldCost(mp, it, have); }
            }
            return n;
        }

        // ------------------------------------------------------------ zdarzenia handlu (CaravanGoods.TradePostfix)
        internal static void NoteBuy(MobileParty mp, ItemObject it, int got)
        {
            try
            {
                var c = it != null ? it.ItemCategory : null;
                if (got > 0 && _heldReady && Tracked(c)) _held[c] = Held(c) + got;
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
                if (Tracked(c))
                {
                    if (_heldReady) _held[c] = Math.Max(0, Held(c) - moved);
                    if (p != PathBulkGlut && !mp.IsDisbanding) { int x; _soldToday.TryGetValue(c, out x); _soldToday[c] = x + moved; }
                }
            }
            catch (Exception e) { Stumble("NoteSale", e); }
        }

        private static Acc AccOf(MobileParty mp, int day)
        {
            Acc a;
            if (!_acc.TryGetValue(mp, out a))
            {
                a = new Acc { First = day };
                for (int i = 0; i < Ring; i++) a.D[i] = int.MinValue;
                _acc[mp] = a;
            }
            return a;
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
        private sealed class Buy { public MobileParty Mp; public bool Exempt; public int Capital, Floor, Wage, Purse0; public double Stock0, Pen = -1, Toll; public readonly HashSet<ItemCategory> Seen = new HashSet<ItemCategory>(); }
        private static readonly Buy _b = new Buy();

        /// <summary>Prefiks BK BuyGoods (z CaravanGoods.BkBuyPrefix): kapital i podloga 259, ladunek po koszcie, kiesa, myto - raz na wywolanie.</summary>
        internal static void BuyStart(MobileParty mp, Town town)
        {
            _b.Mp = mp; _b.Exempt = true; _b.Seen.Clear(); _b.Pen = -1;
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
                    switch (why) { case 1: _rGate++; break; case 2: _rCap++; break; case 3: _rWage++; break; case 4: _rGain++; break; default: _rFood++; break; }
                return why != 0;
            }
            catch (Exception e) { Stumble("BuyBlocked", e); return false; }
            finally { _ticks += Stopwatch.GetTimestamp() - t0; }
        }

        private static int Why(MobileParty mp, ItemCategory cat, Town town, Settings s)
        {
            if (s.CaravanBuyByFlow && GateShut(cat)) return 1;
            int purse = mp.PartyTradeGold;
            double cargo = _b.Stock0 + Math.Max(0, _b.Purse0 - purse);      // ladunek po koszcie: z poczatku BuyGoods + to, co BK juz wydal w tym wywolaniu
            double spend = Math.Min(0.5 * Math.Max(0, purse), 1500.0);        // BK BuyCategory: najwyzej pol kiesy i 1 500 na kategorie
            if (s.CaravanBuyWorkingCapital)
            {
                // recenzja 284 (drobna 4): podloga z rezerwa 30 dni zoldu (karawana bez zwrotu zoldu) moze byc >= kapital - to podloga wyplaty, nie granica handlu;
                // wtedy bez reguly kapitalu (kiese dalej pilnuje rezerwa zoldu nizej)
                if (_b.Capital - _b.Floor > 0 && cargo >= _b.Capital - _b.Floor) return 2;
                if (purse - spend < CashDays * _b.Wage) return 3;
            }
            if (s.CaravanBuyProfitCheck)
            {
                float idx = town.GetItemCategoryPriceIndex(cat), e = SellIdx(cat);
                if (_b.Pen < 0) _b.Pen = PenOf(mp, town);
                // recenzja 284 (wazne 1 i 3): bez zoldu w tescie zakupu - zold karawana placi, czy kupi, czy nie (koszt staly, jak po stronie sprzedazy
                // CaravanSellNoSunkWage); odmowa zakupu z dodatnia marza nie oszczedza zoldu, tylko traci marze. Koszty stale pilnuja reguly 2 i 3.
                if (!(e > 0f) || idx * (1.0 + _b.Pen) >= e / (1.0 + _b.Pen) * (1.0 - _b.Toll)) return 4;
                // recenzja 284 (wazne 2): jedzenie > 7 dob dopiero po FlowMinDays dobach pomiaru (jak bramka) i tylko gdy cos jest w jukach - 0 w jukach to 0 dob,
                // nie nieskonczonosc (inaczej kategoria bez sprzedazy zostaje zamknieta na zawsze)
                if (_days >= FlowMinDays && IsFoodCat(cat))
                {
                    EnsureHeld();
                    long h = Held(cat);
                    if (h > 0)
                    {
                        float ema = Ema(cat);
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
            if (d != _sellDay) { _sellIdx.Clear(); _sellDay = d; }
            float v;
            if (_sellIdx.TryGetValue(c, out v)) return v;
            float avg = CaravanGoods.AvgIndexOf(c);
            var l = new List<float>();
            foreach (var t in Town.AllTowns) { if (t == null) continue; float x = t.GetItemCategoryPriceIndex(c); if (x >= avg) l.Add(x); }
            if (l.Count == 0) v = avg;
            else { l.Sort(); v = l[l.Count / 2]; }
            _sellIdx[c] = v;
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
                if (s == null || !s.CaravanBkSellAtCost || __0 == null || __1 == null) return;
                var mp = __0.MobileParty;
                if (mp == null || !mp.IsCaravan || !__1.IsSettlement || __1.Settlement == null || __1.Settlement.Town == null) return;
                var it = __2.EquipmentElement.Item;
                if (it == null || !it.IsTradeGood || it.HasHorseComponent || it.ItemCategory == null || it.ItemCategory == DefaultItemCategories.PackAnimal) return;
                if (!CaravanBulk.TradesForT1(mp) || MaterialOrders.HasContract(mp) || CaravanBulk.HasOrders(mp)) return;
                if (CaravanBulk.Owns(it) && CaravanGoods.CostEstimated(mp, it)) return;   // surowiec masowy z kosztem z oszacowania - bez progu (jak CaravanBulk.Sell)
                if (CaravanGoods.LossReason(mp, it, __1.Settlement) != 0) { if (bk) _bkExit++; else _vanExit++; return; }
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
                    if (bk) { _bkHeldN += __3 - k; _bkHeldC += (long)Math.Round((__3 - k) * unit); }
                    else { _vanHeldN += __3 - k; _vanHeldC += (long)Math.Round((__3 - k) * unit); }
                    if (!ReferenceEquals(_bkWithheldMp, mp)) { _bkWithheld.Clear(); _bkWithheldMp = mp; }
                    int w0; _bkWithheld.TryGetValue(__2.EquipmentElement, out w0);
                    _bkWithheld[__2.EquipmentElement] = w0 + (__3 - Math.Max(0, k));
                    __3 = Math.Max(0, k);
                }
            }
            catch (Exception e) { Stumble("SellGuardPrefix", e); }
            finally { _ticks += Stopwatch.GetTimestamp() - t0; }
        }

        // 285: przesyt karawan do miasta z brakiem surowca tabeli (SellGuardPrefix) - liczniki od ostatniej linii 285
        private static long _g285N, _g285C; private static int _g285Acts;
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
                if (s == null || !s.CaravanBuyByFlow || _days < FlowMinDays || __0.ItemRoster == null) return;
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
        /// sprzed gabki; wzor CaravanBulk.LeftPostfix).</summary>
        public static void LeftPostfix(MobileParty __0, Settlement __1, Dictionary<ItemObject, int> __state)
        {
            if (__state == null) return;
            try
            {
                var town = __1.Town;
                var pack = __0.ItemRoster;
                var shelf = town.Owner.ItemRoster;
                List<KeyValuePair<ItemObject, int>> back = null;
                for (int i = 0; i < pack.Count; i++)
                {
                    var it = pack.GetElementCopyAtIndex(i).EquipmentElement.Item;
                    if (it == null || !it.IsTradeGood || it.HasHorseComponent || !GateShut(it.ItemCategory)) continue;
                    int was; __state.TryGetValue(it, out was);
                    int extra = pack.GetItemNumber(it) - was;
                    if (extra <= 0) continue;
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
                _held.Clear(); _heldReady = true;
                var cost = new Dictionary<ItemCategory, double>();
                var reals = new List<long>();
                int nCar = 0, withTrade = 0, withProfit = 0, fN = 0, fBelow = 0;
                long sum7 = 0; double fMargin = 0, fWage = 0;
                var alive = new HashSet<MobileParty>();
                foreach (var mp in MobileParty.AllCaravanParties)
                {
                    if (mp == null || !mp.IsActive || mp.ItemRoster == null) continue;
                    nCar++; alive.Add(mp);
                    ScanHeld(mp, cost);
                    var a = AccOf(mp, today);
                    bool any;
                    long r7 = SumLast(a, day, 7, out any);
                    if (any) { withTrade++; reals.Add(r7); sum7 += r7; if (r7 > 0) withProfit++; }
                    if (today - a.First >= MeasureDays)
                    {
                        bool any2;
                        double pd = SumLast(a, day, Ring, out any2) / (double)Math.Min(Ring, Math.Max(1, today - a.First));
                        fN++; fMargin += pd; fWage += Math.Max(0, mp.TotalWage);
                        if (pd < Math.Max(0, mp.TotalWage)) fBelow++;
                    }
                }
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
