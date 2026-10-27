using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// E3-T1 (noc-ekonomia3 16.10, czesc 1; klucz CaravanSellWhereDear) - KARAWANA SPRZEDAJE TAM, GDZIE MA ZYSK.
    /// POPRAWKA JEFFA 16.10 (wiazaca, zastepuje regule planu "sprzedaz tam, gdzie indeks >= srednia indeksow"): "Karawany sprzedaja tam, gdzie maja zysk, a nie
    /// powyzej sredniej, bo nie sprzedaja na stracie, chyba ze juz nie maja wyjscia".
    /// Dowod [L] (test noc-ekonomia2 21-25-46 d109195, linia "Karawany - zapas wedlug towaru (nekon)"): narzedzia - karawany 2 371 z 2 671 sztuk swiata (88.8%),
    /// miasta 194, karawany kupily 18 i sprzedaly 19 na dobe; srebro w jukach 4 736 (kupily 303, sprzedaly 32 - stos rosnie), wino 819 (81.6% swiata).
    /// Przyczyna [K]: BK SellGoods (BKCaravansBehavior.cs:1592-1611) sprzedaje na wizyte tyle, ile wynosi wynik kategorii / cene (1-2 sztuki), i tylko przy
    /// indeksie >= (1.1 - do 0.4) x srednia; reszta jedzie dalej.
    /// Regula (przy wjezdzie do MIASTA, po surowcach masowych CaravanBulk, przed amunicja CaravanAmmo i przed BK - ArmouryBehavior):
    ///  - towar handlowy (IsTradeGood) spoza tabeli CaravanBulk, bez zwierzat i koni; karawana z kontraktem 174 albo z rozkazem gracza (BK) - pominieta;
    ///  - sztuka schodzi, gdy cena ostatniej sztuki kroku w tym miescie PO MYCIE E3-G3 (2% - TownToll.CaravanRateFor; poprawki: marza 0-2% byla strata netto)
    ///    > koszt zakupu sztuki + jej czesc zoldu karawany za dni w drodze (zold dnia x dni od zakupu (najwyzej 30) x koszt sztuki / koszt calego ladunku; najwyzej
    ///    100% kosztu sztuki). Koszt zakupu = srednia wazona partii w jukach - ksiega sesji z latki SellItemsAction.ApplyInternal (kazdy zakup karawany od osady:
    ///    BK, CaravanBulk, kontrakty, gabka); bez klucza zapisu;
    ///  - POPRAWKI (uwaga recenzji 1, powazna): towar sprzed sesji (wczytanie zapisu) dostaje przy pierwszym dotknieciu ksiegi (migawka wszystkich karawan,
    ///    raz na sesje) GORNE oszacowanie ceny zakupu: wartosc x max(1, srednia indeksu kategorii 97 miast) - ta sama miara, wedlug ktorej kupuje BK
    ///    (CalculateBuyValue: kupuje tylko przy indeksie < srednia x (1 - do 0.4), BKCaravansBehavior.cs:1884-1915), a gdy dziennik BK (_tradeActionLogs, w zapisie,
    ///    7 dob) ma zakup tej sztuki - wyzsza z dwoch cen; dzien zakupu z dziennika BK, a bez wpisu 7 dob wstecz (dziennik trzyma 7 dob - starszy zakup); dziennik
    ///    nieczytelny - doba wczytania (jak dotad). Dotad koszt = wartosc, wiec stos 2 371 narzedzi szedl do indeksu ok. 1.0 - na realnej stracie wobec ceny BK;
    ///  - NA STRACIE tylko bez wyjscia: towar lezy w jukach > 30 dob, jedzenie > 7 dob (psuje sie), kiesa < 3 dni zoldu, juki PONAD udzwig (jak BK - toLoseWeight
    ///    przy TotalWeightCarried > InventoryCapacity; poprawki, uwaga recenzji 2: dotad >= 95%, a BK laduje karawany prawie do pelna) i wtedy tylko tyle, ile trzeba
    ///    do zejscia pod udzwig, karawana rozwiazywana (IsDisbanding) - powod w linii doby;
    ///  - BEZ progu "powyzej sredniej" (poprawka Jeffa). Zeby nie bylo petli sprzedaz-odkupienie: kategorii, ktora T1 sprzedal w miescie, ZADNA karawana BK tam nie
    ///    odkupuje przez 7 dob albo do chwili, gdy polka tej kategorii wroci do stanu sprzed sprzedazy (lud wykupil) - postfiks BKCaravansBehavior.CalculateBuyValue
    ///    (poprawki, uwaga recenzji 1: dotad tylko ta sama karawana w tej samej wizycie, inne odkupywaly); gdzie sprzedac, dalej decyduje zysk; cena ostatniej
    ///    sztuki kroku >= 0.4 x wartosci (gabka BK przy wyjezdzie kupuje ponizej 0.33 - BKPartyBehavior :329); liczniki "odkupione";
    ///  - kasa miasta tylko ponad TownPurse.BuyFloor i najwyzej 1/20 tej nadwyzki na dobe (wycena limitu BuyFloorPeek - bez licznikow 169c);
    ///  - krokami po 5 sztuk (sztuka > 200 d - po 1), cena liczona od nowa po kazdym kroku, najwyzej polowa stosu na wizyte, jedzenie zostaje na 10 dob druzyny.
    ///  - STROJENIE PO TESCIE 120 DOB (S1; test 16.10 02-08-15 na zapisie F: na stracie 28.8 tys. zl/d przy zysku 30.1 tys. - glownie stary zapas "dlugo
    ///    w jukach" po wczytaniu - i zrzut na jeden targ: najwiekszy spadek indeksu kategorii w jednym miescie w dobie 92.7% (Ibben oil 6.13 -> 0.45), ponad 40%
    ///    w 120 z 121 dob): gra placi sztuka po sztuce (SellItemsAction.ApplyInternal - cena kazdej sztuki od nowa), wiec krok z zyskiem sprawdza cene OSTATNIEJ
    ///    sztuki kroku i nie przeskakuje progu zysku; krok, ktorego ostatnia sztuka nie ma zysku, jest przycinany do sztuk z zyskiem (reszta - regula straty).
    ///    Sprzedaz NA STRACIE z kazdego powodu poza rozwiazaniem i brakiem na zold idzie PORCJAMI: po sprzedazy indeks kategorii w tym miescie nie nizej niz
    ///    0.70 x indeks przed pierwsza sprzedaza T1 tej kategorii w tym miescie tej doby, a cena kazdej sztuki >= 0.60 x wartosci (Value); sztuka po sztuce -
    ///    stop, gdy nastepna przekroczylaby ktoras granice; reszta czeka na nastepne miasto (licznik "wstrzymane (porcja)"). Rozwiazanie i brak na zold - jak dotad.
    /// E3-T2 (S2, klucz CaravanProfitToOwner) - NADWYZKA KIESY KARAWANY DO WLASCICIELA raz na dobe (ProfitToOwners nizej; opis tam).
    /// Kto placi, kto dostaje: kasa miasta -> kiesa karawany (SellItemsAction - zwykly handel gry), od utargu myto E3-G3 (TownToll). Potem lud i wsie kupuja
    /// z polki: kiesy -> kasa -> zawor. Kontrola w linii (poprawki): kasy miast zmierzone przed i po sprzedazy = przyrost kies karawan + myto - myto wrocone do kas.
    /// Linia "Karawany - towar tam, gdzie maja zysk (E3-T1): dzien N" (z zyskiem / na stracie z powodem, odrzucone, wedlug towaru, ksiega kosztu, blokady,
    /// najwiekszy spadek indeksu w jednym miescie, juki i mediana indeksu wybranych towarow). Nic w zapisie. Wyjatek per wizyta - licznik potkniec (sprzedaz
    /// zostaje jak w BK).
    /// </summary>
    internal static class CaravanGoods
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int MaxAgeDays = 30, FoodAgeDays = 7, FoodKeepDays = 10, CashDays = 3, StepN = 5, DearValue = 200, TownDayShare = 20, BlockDays = 7, BkLogDays = 7;
        private const float GabkaFloor = 0.40f;
        private const float PortionDrop = 0.30f, LossFloor = 0.60f;   // S1: na stracie porcjami - indeks kategorii w miescie najwyzej -30% w dobie, cena sztuki >= 0.6 x wartosci
        private static readonly string[] WatchIds = { "tools", "fur", "silver", "wine", "Papyrus", "velvet", "jewelry", "Eggs", "honey", "salt" };

        // ------------------------------------------------------------ ksiega kosztu (sesja)
        private sealed class Lot { public int N; public double Cost, Day; public bool Est; }   // 284: Est - czesc kosztu z oszacowania (wczytanie bez wpisu, towar bez zakupu w ksiedze)
        private static readonly Dictionary<MobileParty, Dictionary<ItemObject, Lot>> _book = new Dictionary<MobileParty, Dictionary<ItemObject, Lot>>();
        private sealed class Visit { public Settlement St; public HashSet<ItemCategory> Cats = new HashSet<ItemCategory>(); }
        private static readonly Dictionary<MobileParty, Visit> _visit = new Dictionary<MobileParty, Visit>();
        private static readonly Dictionary<Town, int> _townLeft = new Dictionary<Town, int>(); private static int _townDay = -1;
        private static MobileParty _bkBuyer;                         // karawana w BK BuyGoods (prefiks/finalizer) - liczniki blokady (inne karawany)

        // blokada odkupienia na poziomie MIASTA (poprawki, uwaga recenzji 1): miasto -> kategoria -> doba ostatniej sprzedazy T1 i stan polki przed pierwsza sprzedaza
        private sealed class Block { public int Day, Base, Sold; }
        private static readonly Dictionary<Settlement, Dictionary<ItemCategory, Block>> _blocks = new Dictionary<Settlement, Dictionary<ItemCategory, Block>>();

        // migawka ksiegi przy wczytaniu (poprawki, uwaga recenzji 1): raz na sesje, przed pierwszym uzyciem ksiegi
        private static bool _snapDone;
        private static int _snapCaravans; private static long _snapUnits, _snapBk, _snapAvg;
        private static readonly Dictionary<ItemCategory, float> _avgIdx = new Dictionary<ItemCategory, float>(); private static int _avgDay = -1;
        // dziennik BK (_tradeActionLogs) - odczyt przez refleksje, raz na sesje wiazany
        private struct BkRow { public int Price; public double Day; }
        private static object _bkBeh; private static FieldInfo _fLogs, _fItem, _fPrice, _fTime; private static bool _bkTried; private static string _bkLogState = "nie czytany";

        public struct TState { public bool On; public int Kind; public MobileParty Mp; public Settlement St; public ItemObject It; public int Gold0, N0; }

        private static bool _hooked, _startDone, _bkHooked;
        private static readonly HashSet<string> _err = new HashSet<string>();

        // ------------------------------------------------------------ liczniki doby
        private sealed class Row { public int ProfitN, LossN; public long ProfitG, LossG, ProfitCost, LossCost; }
        private static readonly Dictionary<ItemObject, Row> _rows = new Dictionary<ItemObject, Row>();
        private static int _entries, _sellers, _rNoProfit, _bkBlocked, _bkBlockedOther, _rGabka, _rCash, _rLimit, _rFood, _rHalf, _skipOrders, _skipContract, _stumbles, _stumbleSess;
        private static int _lAge, _lFood, _lCash, _lFull, _lDisband, _fullCut;
        private static int _rPortionIdx, _rPortionPrice, _portionCut, _profitCut;   // S1: wstrzymane (porcja) - indeks / cena; kroki przyciete porcja; kroki przyciete do sztuk z zyskiem
        private static readonly float[] _dropKind = new float[3];                  // S1: najwiekszy spadek indeksu wedlug rodzaju sprzedazy: 0 z zyskiem, 1 na stracie porcjami, 2 bez wyjscia
        private static long _lAgeG, _lFoodG, _lCashG, _lFullG, _lDisbandG, _gross, _toll, _rebought, _reboughtOther, _rebuiltLate, _bookBuys, _bookBuyUnits;
        private static long _townSpare0;
        private static long _cTown, _cPurse, _cToll, _cTollTown;     // kontrola: kasy miast zmierzone (-), kiesy karawan (+), myto, myto wrocone do kas osad
        private static readonly Dictionary<Town, Dictionary<ItemCategory, float>> _idx0 = new Dictionary<Town, Dictionary<ItemCategory, float>>();
        private static float _dropMax; private static string _dropWhere;

        internal static void Reset()
        {
            _book.Clear(); _visit.Clear(); _townLeft.Clear(); _townDay = -1; _bkBuyer = null; _blocks.Clear();
            _snapDone = false; _snapCaravans = 0; _snapUnits = _snapBk = _snapAvg = 0; _avgIdx.Clear(); _avgDay = -1;
            _bkBeh = null; _fLogs = _fItem = _fPrice = _fTime = null; _bkTried = false; _bkLogState = "nie czytany";
            _startDone = false; _stumbleSess = 0; _err.Clear();
            LastOwnerPaid = LastOwnerNot = LastOwnerLord = LastOwnerPlayer = 0; LastOwnerN = 0; _oStumbles = _oStumbleSess = 0;
            _pend284 = null; _had284 = false; _cbLots = _cbBad = _cbGone = 0; _cbUnits = 0; _snapLimit = 0;   // 284: ksiega z zapisu (Import284 po Reset)
            ZeroDay();
        }

        private static void ZeroDay()
        {
            _rows.Clear();
            _entries = _sellers = _rNoProfit = _bkBlocked = _bkBlockedOther = _rGabka = _rCash = _rLimit = _rFood = _rHalf = _skipOrders = _skipContract = _stumbles = 0;
            _lAge = _lFood = _lCash = _lFull = _lDisband = _fullCut = 0;
            _rPortionIdx = _rPortionPrice = _portionCut = _profitCut = 0; Array.Clear(_dropKind, 0, _dropKind.Length);
            _lAgeG = _lFoodG = _lCashG = _lFullG = _lDisbandG = _gross = _toll = _rebought = _reboughtOther = _rebuiltLate = _bookBuys = _bookBuyUnits = 0;
            _townSpare0 = 0;
            _cTown = _cPurse = _cToll = _cTollTown = 0;
            _idx0.Clear(); _dropMax = 0f; _dropWhere = null;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumbleSess++;
            try { if (_err.Add(where)) Log.Error("CaravanGoods." + where, e); } catch { }
        }

        private static bool On { get { var s = Settings.Current; return s != null && s.CaravanSellWhereDear; } }
        private static double Now { get { return CampaignTime.Now.ToDays; } }

        // ------------------------------------------------------------ gorne oszacowanie ceny zakupu (towar bez wpisu w ksiedze)
        /// <summary>Srednia indeksu kategorii w 97 miastach (ta sama miara co BK _priceDictionary.AverageBuySellPriceIndex - CreatePriceDataCache) - raz na dobe na kategorie.</summary>
        private static float AvgIndex(ItemCategory c)
        {
            if (c == null) return 1f;
            int d = (int)Now;
            if (d != _avgDay) { _avgIdx.Clear(); _avgDay = d; }
            float v;
            if (_avgIdx.TryGetValue(c, out v)) return v;
            double sum = 0; int n = 0;
            foreach (var t in Town.AllTowns) { if (t == null) continue; sum += t.GetItemCategoryPriceIndex(c); n++; }
            v = n > 0 ? (float)(sum / n) : 1f;
            if (!(v > 0f) || float.IsInfinity(v)) v = 1f;
            _avgIdx[c] = v;
            return v;
        }

        /// <summary>284 (CaravanTrade284): srednia indeksu kategorii w 97 miastach (ta sama miara co BK) - pamiec doby.</summary>
        internal static float AvgIndexOf(ItemCategory c) { return AvgIndex(c); }

        private static readonly object _worthLock = new object();

        /// <summary>
        /// 259 (CaravanSettle): wartosc TOWARU w jukach karawany - TYLKO ODCZYT (bez EnsureSnap / Sync / Restore - nic nie zapisuje do ksiegi; dozwolone tylko
        /// pamieci sredniej indeksu AvgIndex). Towar handlowy jak T1 (IsTradeGood), a zwierzeta i inne przedmioty - tylko kupione z kiesy (nizej, recenzja 259).
        /// Na sztuke: znany koszt zakupu (ksiega E3-T1) -> nizsza z kosztu i rynku (wartosc x srednia indeksu 97 miast); sztuki bez wpisu w ksiedze
        /// (po wczytaniu, przed migawka) -> wartosc x min(1, srednia indeksu) (blad w dol). cost - koszt (znany + oszacowanie bez wpisu), market - rynek,
        /// kinds / units - rodzaje i sztuki. Wynik zaokraglony w dol do pelnych denarow (te same liczby ida do logu i do reguly wyplaty).
        /// Recenzja 259: ZWIERZETA I INNE PRZEDMIOTY KUPIONE Z KIESY (konie, bydlo, juczne ponad potrzebe, bron i zbroje z BK OnSettlementLeft) - to tez towar
        /// na sprzedaz (BK BuyGoods kupuje kategorie IsTradeGood || IsAnimal, SellGoods sprzedaje je z zyskiem), a zakup jest w ksiedze E3-T1 (TradePostfix
        /// ksieguje kazdy przedmiot). Licza sie tylko sztuki z wpisem w ksiedze (kupione z kiesy), po nizszej z kosztu i rynku; bez wpisu - pominiete (juczne
        /// gry i K5, dary BK, lup - nie z kiesy). Juczne potrzebne do niesienia (do MemberRoster.TotalManCount - ta sama granica, od ktorej BK uznaje juczne
        /// za nadwyzke na sprzedaz) - pominiete, najpierw sztuki bez wpisu. other - ta czesc wyniku (zwierzeta i inne), osobno w linii gracza.
        /// </summary>
        internal static long WorthOf(MobileParty mp, out long cost, out long market, out int kinds, out int units, out long other)
        {
            cost = market = other = 0; kinds = units = 0;
            var r = mp != null ? mp.ItemRoster : null;
            if (r == null) return 0;
            lock (_worthLock)
            {
                Dictionary<ItemObject, Lot> d;
                if (!_book.TryGetValue(mp, out d)) d = null;
                double worth = 0, dc = 0, dm = 0, dOther = 0;
                int packNeed = mp.MemberRoster != null ? Math.Max(0, mp.MemberRoster.TotalManCount) : 0;
                HashSet<ItemObject> seen = null;
                // juczne bez wpisu w ksiedze (K5, gra, dary BK) niosa jako pierwsze - zmniejszaja potrzebe, zanim policzymy kupione z kiesy
                for (int i = 0; i < r.Count && packNeed > 0; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (it == null || el.Amount <= 0 || it.ItemCategory != DefaultItemCategories.PackAnimal) continue;
                    if (seen == null) seen = new HashSet<ItemObject>();
                    if (!seen.Add(it)) continue;
                    int have = r.GetItemNumber(it);
                    Lot lp;
                    int known = d != null && d.TryGetValue(it, out lp) && lp != null && lp.N > 0 ? Math.Min(lp.N, have) : 0;
                    packNeed -= Math.Min(packNeed, Math.Max(0, have - known));
                }
                if (seen != null) seen.Clear();
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (it == null || el.Amount <= 0) continue;
                    bool goods = it.IsTradeGood && !it.HasHorseComponent && it.ItemCategory != DefaultItemCategories.PackAnimal;
                    Lot l = null;
                    if (!goods && (d == null || !d.TryGetValue(it, out l) || l == null || l.N <= 0)) continue;   // nie towar i bez zakupu z kiesy - pominiete
                    if (seen == null) seen = new HashSet<ItemObject>();
                    if (!seen.Add(it)) continue;                      // ten sam towar w kilku wierszach (modyfikatory) - GetItemNumber liczy wszystkie
                    int have = r.GetItemNumber(it);
                    if (have <= 0) continue;
                    double val = Math.Max(1, it.Value), idx = AvgIndex(it.ItemCategory);
                    if (!goods)
                    {
                        int known = Math.Min(l.N, have), cnt = known;
                        if (it.ItemCategory == DefaultItemCategories.PackAnimal)
                        {
                            int skip = Math.Min(packNeed, known);     // reszta potrzeby (po jucznych bez wpisu - petla wyzej) z kupionych
                            packNeed -= skip; cnt = known - skip;
                        }
                        if (cnt <= 0) continue;
                        double kcO = l.Cost * cnt / l.N, mO = cnt * val * idx, wO = Math.Min(kcO, mO);
                        kinds++; units += cnt;
                        worth += wO; dOther += wO; dc += kcO; dm += mO;
                        continue;
                    }
                    kinds++; units += have;
                    int knownG = 0; double kc = 0;
                    if (d != null && d.TryGetValue(it, out l) && l != null && l.N > 0) { knownG = Math.Min(l.N, have); kc = l.Cost * knownG / l.N; }
                    double unk = (have - knownG) * val * Math.Min(1.0, idx);
                    worth += Math.Min(kc, knownG * val * idx) + unk;
                    dc += kc + unk;
                    dm += have * val * idx;
                }
                cost = (long)Math.Floor(dc); market = (long)Math.Floor(dm); other = (long)Math.Floor(dOther);
                return (long)Math.Floor(worth);
            }
        }

        /// <summary>Wiazanie odczytu dziennika BK (raz na sesje): BKCaravansBehavior._tradeActionLogs i pola TradeActionLog (BuyPrice - cena sztuki, BoughtTime, ItemRosterElement).</summary>
        private static bool BkLogInit()
        {
            if (_bkTried) return _bkBeh != null;
            _bkTried = true;
            try
            {
                var bkc = AccessTools.TypeByName("BannerKings.Behaviours.BKCaravansBehavior");
                if (bkc == null || Campaign.Current == null) { _bkLogState = "brak BK"; return false; }
                var gm = typeof(Campaign).GetMethod("GetCampaignBehavior", Type.EmptyTypes);
                var beh = gm != null ? gm.MakeGenericMethod(bkc).Invoke(Campaign.Current, null) : null;
                _fLogs = AccessTools.Field(bkc, "_tradeActionLogs");
                var lt = AccessTools.Inner(bkc, "TradeActionLog");
                if (lt != null) { _fItem = AccessTools.Field(lt, "ItemRosterElement"); _fPrice = AccessTools.Field(lt, "BuyPrice"); _fTime = AccessTools.Field(lt, "BoughtTime"); }
                bool ok = beh != null && _fLogs != null && _fItem != null && _fPrice != null && _fTime != null
                          && _fItem.FieldType == typeof(ItemRosterElement) && _fPrice.FieldType == typeof(int) && _fTime.FieldType == typeof(CampaignTime);
                _bkBeh = ok ? beh : null;
                _bkLogState = ok ? "czytany" : "NIE (brak zachowania albo inne pola BK)";
                return ok;
            }
            catch (Exception e) { _bkBeh = null; _bkLogState = "NIE (wyjatek)"; Stumble("BkLogInit", e); return false; }
        }

        /// <summary>Zakupy tej karawany z dziennika BK (7 dob): przedmiot -> najwyzsza cena sztuki i najpozniejsza doba. null - dziennik nieczytelny.</summary>
        private static Dictionary<ItemObject, BkRow> BkRows(MobileParty mp)
        {
            if (!BkLogInit()) return null;
            var res = new Dictionary<ItemObject, BkRow>();
            try
            {
                var dict = _fLogs.GetValue(_bkBeh) as IDictionary;
                var list = dict != null && dict.Contains(mp) ? dict[mp] as IList : null;
                if (list == null) return res;
                foreach (var o in list)
                {
                    if (o == null) continue;
                    var it = ((ItemRosterElement)_fItem.GetValue(o)).EquipmentElement.Item;
                    if (it == null) continue;
                    int pr = (int)_fPrice.GetValue(o);
                    double day = ((CampaignTime)_fTime.GetValue(o)).ToDays;
                    BkRow r;
                    if (res.TryGetValue(it, out r)) { if (pr > r.Price) r.Price = pr; if (day > r.Day) r.Day = day; }
                    else r = new BkRow { Price = pr, Day = day };
                    res[it] = r;
                }
            }
            catch (Exception e) { Stumble("BkRows", e); return null; }
            return res;
        }

        /// <summary>Migawka ksiegi (raz na sesje, przed pierwszym uzyciem): kazda karawana i kazdy towar handlowy w jukach - wpis z gornym oszacowaniem ceny zakupu
        /// i dniem z dziennika BK (bez wpisu - 7 dob wstecz). Dobowo dopuszczalne (jeden przeglad karawan na sesje).</summary>
        private static void EnsureSnap()
        {
            if (_snapDone) return;
            var c = Campaign.Current;
            if (c == null || !c.GameStarted) return;
            _snapDone = true;
            if (_pend284 != null) { try { Resolve284(); } catch (Exception e) { Stumble("Resolve284", e); } }   // 284: wpisy z zapisu najpierw - migawka dopelnia tylko braki
            try
            {
                foreach (var mp in MobileParty.AllCaravanParties)
                {
                    try
                    {
                        if (mp == null || !mp.IsActive || mp.ItemRoster == null) continue;
                        var r = mp.ItemRoster;
                        Dictionary<ItemObject, BkRow> bk = null; bool read = false, any = false;
                        for (int i = 0; i < r.Count; i++)
                        {
                            var el = r.GetElementCopyAtIndex(i);
                            var it = el.EquipmentElement.Item;
                            if (it == null || el.Amount <= 0 || !it.IsTradeGood) continue;
                            if (!read) { bk = BkRows(mp); read = true; }
                            Restore(mp, it, r.GetItemNumber(it), bk, true);
                            any = true;
                        }
                        if (any) _snapCaravans++;
                    }
                    catch (Exception e) { Stumble("EnsureSnap(karawana)", e); }
                }
            }
            catch (Exception e) { Stumble("EnsureSnap", e); }
            try
            {
                var s = Settings.Current;
                Log.Info("Ksiega kosztu karawan (284): " + (_had284 ? "wczytano wpisow " + _cbLots + " (sztuk " + _cbUnits + ") z zapisu arm_cg284, bez karawany albo towaru " + _cbGone
                                                                     + ", nieczytelnych " + _cbBad : "zapis bez klucza arm_cg284 (starszy zapis albo klucz byl wylaczony) - wpisow 0, nieczytelnych 0")
                         + " | migawka: karawan z towarem " + _snapCaravans + ", sztuk dopelnionych oszacowaniem " + _snapUnits + " (cena z dziennika BK " + _snapBk + ", bez wpisu " + _snapAvg
                         + (Est284 ? ", w tym prog zakupu BK " + _snapLimit : ", wartosc x max(1, srednia indeksu)") + "; dziennik BK " + _bkLogState + ")"
                         + " | klucz Caravan Cost Book Save " + (s != null && s.CaravanCostBookSave ? "TAK" : "NIE") + ".");
            }
            catch (Exception e) { Stumble("EnsureSnap(linia 284)", e); }
        }

        // ------------------------------------------------------------ 284 (klucz CaravanCostBookSave): ksiega kosztu w zapisie
        private static string _pend284; private static bool _had284;
        private static int _cbLots, _cbBad, _cbGone; private static long _cbUnits, _snapLimit;
        private static bool Est284 { get { var s = Settings.Current; return s != null && s.CaravanCostBookSave; } }

        /// <summary>284: prog zakupu BK dla sztuki bez wpisu (BKCaravansBehavior.CalculateBuyValue kupuje tylko przy indeksie miasta < srednia x (1 - h), h = sqrt(min(V, 5000)
        /// / 5000) x 0.4, V - wartosc kategorii w jukach jak RefreshTotalValueOfItemsAtCategoryForParty: sztuki x (wartosc + 10)); cena zakupu = wartosc x indeks x (1 + kara
        /// handlu). Gorna granica tego, co BK zaplacil, ale ciasna (dotad wartosc x max(1, srednia) - bez progu).</summary>
        private static double BkBuyLimit(MobileParty mp, ItemObject it)
        {
            var cat = it.ItemCategory;
            double avg = AvgIndex(cat);
            long vc = 0;
            var r = mp != null ? mp.ItemRoster : null;
            if (r != null)
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    var x = el.EquipmentElement.Item;
                    if (x != null && el.Amount > 0 && x.ItemCategory == cat) vc += (long)el.Amount * (x.Value + 10);
                }
            double h = Math.Sqrt(Math.Min(vc, 5000L) / 5000.0) * 0.4;
            double pen = Penalty(mp, it);
            return Math.Max(1.0, Math.Max(1, it.Value) * avg * (1.0 - h) * (1.0 + pen));
        }

        /// <summary>284: kara handlu karawany przy zakupie (model cen gry - GetTradePenalty, bez kupca); blad albo poza 0..1 - 0.</summary>
        internal static double Penalty(MobileParty mp, ItemObject it)
        {
            try
            {
                var m = Campaign.Current != null ? Campaign.Current.Models.TradeItemPriceFactorModel : null;
                double p = m != null && it != null ? m.GetTradePenalty(it, mp, null, false, 0f, 0f, 0f) : 0.0;
                return p >= 0.0 && p <= 1.0 ? p : 0.0;
            }
            catch { return 0.0; }
        }

        private static bool BadId(string id) { return string.IsNullOrEmpty(id) || id.IndexOf(';') >= 0 || id.IndexOf(':') >= 0 || id.IndexOf('~') >= 0 || id.IndexOf('|') >= 0; }

        /// <summary>284: zapis arm_cg284 - "cg284|karawana~towar:sztuk:koszt:doba:oszacowanie(0/1);..." (sztuk najwyzej tyle, ile w jukach). Migawka jeszcze nie zrobiona (zapis tuz po
        /// wczytaniu) - wpisy z wczytania bez zmian. Klucz wylaczony - pusty napis (wczytanie: ksiega z oszacowania).</summary>
        internal static string Export284()
        {
            var s = Settings.Current;
            if (s == null || !s.CaravanCostBookSave) return "";
            if (!_snapDone && _pend284 != null) return _pend284;
            var sb = new StringBuilder(1 << 16);
            sb.Append("cg284|");
            bool first = true;
            lock (_worthLock)
            {
                foreach (var kv in _book)
                {
                    var mp = kv.Key;
                    if (mp == null || !mp.IsActive || !mp.IsCaravan || mp.ItemRoster == null || BadId(mp.StringId)) continue;
                    foreach (var lv in kv.Value)
                    {
                        var it = lv.Key; var l = lv.Value;
                        if (it == null || l == null || l.N <= 0 || BadId(it.StringId)) continue;
                        int n = Math.Min(l.N, mp.ItemRoster.GetItemNumber(it));
                        if (n <= 0) continue;
                        double cost = l.Cost * n / l.N;
                        sb.Append(first ? "" : ";").Append(mp.StringId).Append('~').Append(it.StringId).Append(':').Append(n).Append(':')
                          .Append(cost.ToString("0.##", Inv)).Append(':').Append(l.Day.ToString("0.###", Inv)).Append(':').Append(l.Est ? '1' : '0');
                        first = false;
                    }
                }
            }
            return sb.ToString();
        }

        /// <summary>284: wczytanie arm_cg284 (po Reset) - napis czeka do pierwszej migawki ksiegi (karawany i przedmioty juz sa). null - zapis bez klucza.</summary>
        internal static void Import284(string s)
        {
            _had284 = !string.IsNullOrEmpty(s);
            _pend284 = _had284 ? s : null;
        }

        private static void Resolve284()
        {
            string s = _pend284; _pend284 = null;
            if (s == null) return;
            if (!s.StartsWith("cg284|", StringComparison.Ordinal)) { _cbBad++; return; }
            var cars = new Dictionary<string, MobileParty>();
            foreach (var mp in MobileParty.AllCaravanParties) if (mp != null && mp.IsActive && mp.StringId != null) cars[mp.StringId] = mp;
            var om = MBObjectManager.Instance;
            double now = Now;
            foreach (var rec in s.Substring(6).Split(';'))
            {
                if (rec.Length == 0) continue;
                int t = rec.IndexOf('~');
                var f = t > 0 ? rec.Substring(t + 1).Split(':') : null;
                int n; double cost, day;
                if (f == null || (f.Length != 4 && f.Length != 5) || !int.TryParse(f[1], NumberStyles.Integer, Inv, out n) || !double.TryParse(f[2], NumberStyles.Float, Inv, out cost)
                    || !double.TryParse(f[3], NumberStyles.Float, Inv, out day) || n <= 0 || !(cost >= 0.0) || double.IsInfinity(cost) || double.IsNaN(day) || double.IsInfinity(day)) { _cbBad++; continue; }
                MobileParty mp;
                ItemObject it = null;
                try { it = om != null ? om.GetObject<ItemObject>(f[0]) : null; } catch { it = null; }
                if (!cars.TryGetValue(rec.Substring(0, t), out mp) || it == null || mp.ItemRoster == null) { _cbGone++; continue; }
                int have = mp.ItemRoster.GetItemNumber(it);
                if (have <= 0) { _cbGone++; continue; }
                int k = Math.Min(n, have);
                Dictionary<ItemObject, Lot> d;
                if (!_book.TryGetValue(mp, out d)) { d = new Dictionary<ItemObject, Lot>(); _book[mp] = d; }
                d[it] = new Lot { N = k, Cost = cost * k / n, Day = Math.Min(now, day), Est = f.Length < 5 || f[4] != "0" };
                _cbLots++; _cbUnits += k;
            }
        }

        // ------------------------------------------------------------ 284: pomocnicze dla BK SellGoods, CaravanBulk i CaravanTrade284
        /// <summary>284: koszt sztuki z ksiegi (wpis zgodny z jukami - Sync, takze odtworzenie oszacowaniem). 0 - brak towaru.</summary>
        internal static double UnitCost(MobileParty mp, ItemObject it)
        {
            if (mp == null || it == null || mp.ItemRoster == null) return 0.0;
            var l = Sync(mp, it, mp.ItemRoster.GetItemNumber(it), true);
            return l != null && l.N > 0 ? l.Cost / l.N : 0.0;
        }

        /// <summary>284: koszt sztuki w ksiedze choc czesciowo z oszacowania (wczytanie bez wpisu) - CaravanBulk i straz BK dla surowcow masowych nie stawiaja wtedy progu kosztu
        /// (BK surowcow nie kupuje, wiec jego prog zakupu nie jest ich kosztem; surowiec kupiony regula CaravanBulk taniej niz srednia).</summary>
        internal static bool CostEstimated(MobileParty mp, ItemObject it)
        {
            if (mp == null || it == null) return true;
            lock (_worthLock)
            {
                Dictionary<ItemObject, Lot> d; Lot l;
                return !(_book.TryGetValue(mp, out d) && d.TryGetValue(it, out l) && l != null && l.N > 0 && !l.Est);
            }
        }

        /// <summary>284: koszt calego ladunku towarow handlowych z ksiegi (jak "cargo" w T1 - podzial zoldu).</summary>
        internal static double CargoCost(MobileParty mp)
        {
            var pack = mp != null ? mp.ItemRoster : null;
            if (pack == null) return 0.0;
            double cargo = 0;
            HashSet<ItemObject> seen = null;
            for (int i = 0; i < pack.Count; i++)
            {
                var el = pack.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0 || !it.IsTradeGood) continue;
                if (seen == null) seen = new HashSet<ItemObject>();
                if (!seen.Add(it)) continue;
                int have = pack.GetItemNumber(it);
                var l = Sync(mp, it, have, true);
                if (l != null && l.N > 0) cargo += l.Cost * Math.Min(have, l.N) / l.N;
            }
            return cargo;
        }

        /// <summary>284: koszt towaru w jukach TYLKO ODCZYT (bez Sync - przeglad doby): znany koszt z ksiegi, reszta wartosc x min(1, srednia indeksu) - jak WorthOf.</summary>
        internal static double HeldCost(MobileParty mp, ItemObject it, int have)
        {
            if (mp == null || it == null || have <= 0) return 0.0;
            lock (_worthLock)
            {
                Dictionary<ItemObject, Lot> d; Lot l;
                int known = 0; double kc = 0;
                if (_book.TryGetValue(mp, out d) && d.TryGetValue(it, out l) && l != null && l.N > 0) { known = Math.Min(l.N, have); kc = l.Cost * known / l.N; }
                return kc + (have - known) * Math.Max(1, it.Value) * Math.Min(1.0, AvgIndex(it.ItemCategory));
            }
        }

        /// <summary>284: powod sprzedazy na stracie dla towaru karawany teraz (te same wyjscia co T1 - LossWhy): 0 brak; 1 rozwiazanie, 2 kiesa < 3 dni zoldu, 3 juki ponad
        /// udzwig, 4 jedzenie > 7 dob, 5 towar > 30 dob bez rynku z zyskiem (poprawka 132a - AgeMarket; st - miasto sprzedazy, myto E3-G3).</summary>
        internal static int LossReason(MobileParty mp, ItemObject it, Settlement st)
        {
            if (mp == null || it == null || mp.ItemRoster == null) return 0;
            int wage = Math.Max(0, mp.TotalWage);
            bool cash = mp.PartyTradeGold < CashDays * wage, full = mp.TotalWeightCarried > Math.Max(1, mp.InventoryCapacity);
            var l = Sync(mp, it, mp.ItemRoster.GetItemNumber(it), false);
            double age = l != null ? Math.Max(0, Now - l.Day) : 0;
            bool market = l != null && l.N > 0 && AgeMarket(mp, it, l.Cost / l.N, 1.0 - TownToll.CaravanRateFor(mp, st), age);
            return LossWhy(mp.IsDisbanding, cash, full, it.IsFood, age, market);
        }

        /// <summary>
        /// Poprawka 132a (autotest 132a, 30 dob: T1 "dlugo w jukach" 0-15 szt./d do doby 109048, od 109049 - 24 doby po wczytaniu, migawka ksiegi daje sztukom bez
        /// wpisu dziennika BK dzien zakupu 7 dob wstecz - 325/336/330/264/240/289 szt./d za 86/79/59/65/69/50 tys. zl przy koszcie ok. 2x; T1 na stracie ostatnie 10 dob
        /// 44 783 zl/d, z tego dlugo w jukach 42 459; ta sama doba bawelna i futra szly tez z zyskiem po 3 000 i 400 zl/szt. w innych miastach): towar > 30 dob
        /// nie jest "bez wyjscia", gdy rynek z zyskiem istnieje - mediana indeksu miast placacych co najmniej srednia (CaravanTrade284.SellIdxOf - ta sama miara co
        /// zakup "zysk osiagalny") daje cene po karze handlu (cena sprzedazy gry = wartosc x indeks / (1 + kara), DefaultTradeItemPriceFactorModel.GetPriceFactor) i po
        /// mycie co najmniej `min` (koszt sztuki; T1 - prog sprzedazy z zyskiem). Wtedy towar czeka na takie miasto (T1 i BK sprzedaja tam z zyskiem); bez takiego
        /// rynku - wyjscie na stracie porcjami jak dotad. Jedzenie - bez zmian (powod 4 przed wiekiem). Tylko przy age > 30 dob (liczy sie licznik "czeka").
        /// </summary>
        private static bool AgeMarket(MobileParty mp, ItemObject it, double min, double keep, double age)
        {
            if (!(age > MaxAgeDays) || it == null || it.IsFood || it.ItemCategory == null || !(min > 0.0)) return false;
            double e = CaravanTrade284.SellIdxOf(it.ItemCategory);
            if (!(e > 0.0)) return false;
            bool ok = Math.Max(1, it.Value) * e / (1.0 + Penalty(mp, it)) * keep >= min;
            if (ok) CaravanTrade284.NoteAgeWait();
            return ok;
        }

        /// <summary>284: najwiecej sztuk z n, ktorych KAZDA idzie w tym miescie po cenie po mycie (E3-G3) nie nizszej niz koszt sztuki (cena k-tej sztuki maleje z k -
        /// PriceAt, ta sama miara co T1; wyszukiwanie polowkowe).</summary>
        internal static int MaxAtCost(Town town, Settlement st, MobileParty mp, EquipmentElement el, int n, double unit)
        {
            var model = Campaign.Current != null ? Campaign.Current.Models.TradeItemPriceFactorModel : null;
            var cat = el.Item != null ? el.Item.ItemCategory : null;
            if (model == null || cat == null || town == null || n <= 0) return n;
            var d = town.MarketData.GetCategoryData(cat);
            int wv = Math.Max(1, HistoricalPrices.ShelfWorth(el.Item));
            double keep = 1.0 - TownToll.CaravanRateFor(mp, st);
            int lo = 0, hi = n;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (PriceAt(model, el, mp, d, mid, wv) * keep >= unit) lo = mid; else hi = mid - 1;
            }
            return lo;
        }

        /// <summary>Wpis ksiegi dla `have` sztuk: brakujace sztuki z gornym oszacowaniem ceny zakupu (atStart - migawka: dzien z dziennika BK albo 7 dob wstecz;
        /// pozniej - sztuki bez zakupu w ksiedze: doba teraz).</summary>
        private static Lot Restore(MobileParty mp, ItemObject it, int have, Dictionary<ItemObject, BkRow> bk, bool atStart)
        {
            Dictionary<ItemObject, Lot> d;
            if (!_book.TryGetValue(mp, out d)) { d = new Dictionary<ItemObject, Lot>(); _book[mp] = d; }
            Lot l;
            if (!d.TryGetValue(it, out l)) { l = new Lot { N = 0, Cost = 0, Day = Now }; d[it] = l; }
            if (l.N >= have) return l;
            int miss = have - l.N;
            double now = Now, day = now;
            double unit = Math.Max(1, it.Value) * Math.Max(1.0, AvgIndex(it.ItemCategory));
            // 284 (klucz CaravanCostBookSave): bez wpisu dziennika BK - prog zakupu BK (wartosc x srednia x (1 - h) x (1 + kara handlu)), z wpisem - cena z dziennika
            // (to naprawde zaplacono); dotad wyzsza z dwoch i wartosc x max(1, srednia) - zysk pierwszych sprzedazy po wczytaniu wychodzil ujemny
            bool est = Est284;
            if (est) unit = BkBuyLimit(mp, it);
            BkRow br;
            if (atStart && bk != null && bk.TryGetValue(it, out br))
            {
                if (est) unit = Math.Max(1, br.Price);
                else if (br.Price > unit) unit = br.Price;
                day = Math.Min(now, br.Day);
                _snapBk += miss;
            }
            else if (atStart)
            {
                if (bk != null) day = now - BkLogDays;              // dziennik czytelny, zakupu brak - starszy niz 7 dob
                _snapAvg += miss;
                if (est) _snapLimit += miss;
            }
            else _rebuiltLate += miss;
            if (atStart) _snapUnits += miss;
            l.Est = true;
            l.Day = l.N > 0 ? (l.Day * l.N + day * miss) / have : day;
            l.Cost += miss * unit;
            l.N = have;
            return l;
        }

        // ------------------------------------------------------------ ksiega kosztu: zgodnosc z jukami
        /// <summary>Wpis ksiegi zgodny z jukami (`have` sztuk): nadmiar wpisu (towar zszedl bez SellItemsAction - jedzenie, cofniecie gabki) zdejmowany proporcjonalnie,
        /// brak wpisu odtwarzany gornym oszacowaniem ceny zakupu (Restore). Zwraca wpis albo null przy have <= 0.</summary>
        private static Lot Sync(MobileParty mp, ItemObject it, int have, bool create)
        {
            EnsureSnap();
            Dictionary<ItemObject, Lot> d;
            if (!_book.TryGetValue(mp, out d)) { if (!create && have <= 0) return null; d = new Dictionary<ItemObject, Lot>(); _book[mp] = d; }
            Lot l;
            if (!d.TryGetValue(it, out l)) { if (have <= 0 && !create) return null; l = new Lot { N = 0, Cost = 0, Day = Now }; d[it] = l; }
            if (l.N > have)
            {
                if (have <= 0) { l.N = 0; l.Cost = 0; l.Est = false; }
                else { l.Cost = l.Cost * have / l.N; l.N = have; }
            }
            else if (l.N < have) Restore(mp, it, have, null, false);
            return l;
        }

        // ------------------------------------------------------------ blokada odkupienia (miasto)
        private static Block BlockOf(Settlement st, ItemCategory cat)
        {
            Dictionary<ItemCategory, Block> bd; Block b;
            return st != null && cat != null && _blocks.TryGetValue(st, out bd) && bd.TryGetValue(cat, out b) ? b : null;
        }

        /// <summary>Blokada czynna: ostatnia sprzedaz T1 nie dalej niz 7 dob temu i na polce wiecej sztuk kategorii niz przed pierwsza sprzedaza (`inStore`).</summary>
        private static bool Alive(Block b, int inStore, int today) { return b != null && today - b.Day <= BlockDays && inStore > b.Base; }

        // ------------------------------------------------------------ latka SellItemsAction.ApplyInternal (zakup karawany - ksiega; sprzedaz karawany - ksiega i myto)
        public static void TradePrefix(PartyBase __0, PartyBase __1, ItemRosterElement __2, int __3, out TState __state)
        {
            __state = default(TState);
            try
            {
                var c = Campaign.Current;
                if (c == null || !c.GameStarted || __0 == null || __1 == null) return;
                var it = __2.EquipmentElement.Item;
                if (it == null) return;
                MobileParty buyer = __1.MobileParty, seller = __0.MobileParty;
                if (buyer != null && buyer.IsCaravan && __0.IsSettlement && __0.Settlement != null && __0.Settlement.Town != null)
                {
                    __state.On = true; __state.Kind = 1; __state.Mp = buyer; __state.St = __0.Settlement;
                }
                else if (seller != null && seller.IsCaravan && __1.IsSettlement && __1.Settlement != null && __1.Settlement.Town != null)
                {
                    __state.On = true; __state.Kind = 2; __state.Mp = seller; __state.St = __1.Settlement;
                    TownToll.LastTaken = 0;
                }
                else return;
                EnsureSnap();                                              // migawka ksiegi PRZED zmiana jukow (poprawki)
                __state.It = it; __state.Gold0 = __state.Mp.PartyTradeGold; __state.N0 = __state.Mp.ItemRoster.GetItemNumber(it);
            }
            catch (Exception e) { __state.On = false; Stumble("TradePrefix", e); }
        }

        public static void TradePostfix(TState __state)
        {
            if (!__state.On) return;
            try
            {
                var mp = __state.Mp; var it = __state.It;
                int n1 = mp.ItemRoster.GetItemNumber(it);
                if (__state.Kind == 1)
                {
                    int got = n1 - __state.N0;
                    long paid = (long)__state.Gold0 - mp.PartyTradeGold;
                    if (got <= 0 || paid < 0) return;
                    var l = Sync(mp, it, __state.N0, true);
                    double now = Now;
                    l.Day = l.N > 0 ? (l.Day * l.N + now * got) / (l.N + got) : now;
                    l.Cost += paid; l.N += got;
                    _bookBuys++; _bookBuyUnits += got;
                    if (mp.ActualClan == Clan.PlayerClan) CaravanSettle.NoteTrade(mp, 1, paid, 0, 0.0);   // 259: zakup karawany gracza (linia majatku gracza)
                    CaravanTrade284.NoteBuy(mp, it, got);   // 284: juki karawan wedlug kategorii (bramka przeplywu na zywo)
                    var cat = it.ItemCategory;
                    Visit v;
                    if (_visit.TryGetValue(mp, out v) && ReferenceEquals(v.St, __state.St) && v.Cats.Contains(cat)) _rebought += got;   // odkupione to, co tu sprzedalismy (gabka, CaravanBulk)
                    else if (_blocks.Count > 0 && cat != null)
                    {
                        var b = BlockOf(__state.St, cat);
                        if (b != null && Alive(b, __state.St.Town.MarketData.GetItemCountOfCategory(cat) + got, (int)now)) _reboughtOther += got;   // inna karawana (nie przez BK - zablokowane)
                    }
                }
                else
                {
                    int moved = __state.N0 - n1;
                    int got = mp.PartyTradeGold - __state.Gold0;
                    double costRm = 0;   // 259: koszt zdjety z ksiegi (zysk ze sprzedazy w linii majatku gracza)
                    if (moved > 0)
                    {
                        var l = Sync(mp, it, __state.N0, false);
                        if (l != null && l.N > 0) { int m = Math.Min(moved, l.N); costRm = l.Cost * m / l.N; l.Cost -= costRm; l.N -= m; if (l.N <= 0) l.Est = false; }
                    }
                    if (got > 0) TownToll.LastTaken = TownToll.TakeCaravan(__state.St, mp, got);
                    if (got > 0 && mp.ActualClan == Clan.PlayerClan) CaravanSettle.NoteTrade(mp, 2, got, TownToll.LastTaken, costRm);   // 259: sprzedaz karawany gracza (utarg przed mytem, myto)
                    if (moved > 0) CaravanTrade284.NoteSale(mp, it, moved, Math.Max(0, got), got > 0 ? TownToll.LastTaken : 0, costRm);   // 284: zysk zrealizowany wedlug drogi, pomiar sprzedazy kategorii
                }
            }
            catch (Exception e) { Stumble("TradePostfix", e); }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(SellItemsAction), "ApplyInternal");
                var ps = m != null ? m.GetParameters() : null;
                if (m != null && m.IsStatic && ps.Length >= 4 && ps[0].ParameterType == typeof(PartyBase) && ps[1].ParameterType == typeof(PartyBase)
                    && ps[2].ParameterType == typeof(ItemRosterElement) && ps[3].ParameterType == typeof(int))
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(CaravanGoods), nameof(TradePrefix)), postfix: new HarmonyMethod(typeof(CaravanGoods), nameof(TradePostfix)));
                    _hooked = true;
                }
                TownToll.NoteHooks(false, _hooked);
                // blokada odkupienia: BK BuyGoods(MobileParty, Town) ustawia karawane (liczniki), CalculateBuyValue(ItemCategory, Town, float, float) -> 0 dla kategorii sprzedanej
                // w tym miescie przez T1 (kazda karawana, do 7 dob albo do zejscia sprzedanych sztuk z polki)
                var bkc = AccessTools.TypeByName("BannerKings.Behaviours.BKCaravansBehavior");
                var goods = bkc != null ? AccessTools.DeclaredMethod(bkc, "BuyGoods", new[] { typeof(MobileParty), typeof(Town) }) : null;
                var value = bkc != null ? AccessTools.DeclaredMethod(bkc, "CalculateBuyValue", new[] { typeof(ItemCategory), typeof(Town), typeof(float), typeof(float) }) : null;
                if (goods != null && value != null && value.ReturnType == typeof(float))
                {
                    h.Patch(goods, prefix: new HarmonyMethod(typeof(CaravanGoods), nameof(BkBuyPrefix)), finalizer: new HarmonyMethod(typeof(CaravanGoods), nameof(BkBuyFinalizer)));
                    h.Patch(value, postfix: new HarmonyMethod(typeof(CaravanGoods), nameof(BkValuePostfix)));
                    _bkHooked = true;
                }
                Log.Info("CaravanGoods (E3-T1, E3-G3): SellItemsAction.ApplyInternal - latka " + (_hooked ? "wpieta" : "NIE wpieta (inna sygnatura)") + " (ksiega kosztu karawan, myto karawan)"
                         + "; blokada odkupienia przez BK (BuyGoods + CalculateBuyValue) - " + (_bkHooked ? "wpieta" : "NIE (brak metod BK)") + " (tylko ta sama karawana przy tej samej wizycie - Jeff 16.10: inne karawany moga odkupic).");
            }
            catch (Exception e) { Log.Error("CaravanGoods.ApplyAll", e); }
        }

        // ------------------------------------------------------------ blokada odkupienia przez BK w miescie sprzedazy
        public static void BkBuyPrefix(MobileParty __0, Town __1)
        {
            _bkBuyer = __0;
            try { CaravanTrade284.BuyStart(__0, __1); } catch (Exception e) { Stumble("BkBuyPrefix(284)", e); }   // 284: kapital, podloga, ladunek - raz na BuyGoods
        }
        public static Exception BkBuyFinalizer(Exception __exception) { _bkBuyer = null; CaravanTrade284.BuyEnd(); return __exception; }

        /// <summary>BK CalculateBuyValue: kategoria, ktora T1 sprzedal w tym miescie (do 7 dob, dopoki sprzedane sztuki leza na polce) - wycena 0 dla KAZDEJ karawany. Nie rzuca.</summary>
        public static void BkValuePostfix(ItemCategory __0, Town __1, ref float __result)
        {
            if (__result <= 0f || __0 == null || __1 == null) return;
            // 284: zasady zakupu (bramka przeplywu, kapital obrotowy, rezerwa zoldu, zysk osiagalny) - PRZED wczesnym wyjsciem blokady odkupienia (bez blokad tez dzialaja)
            if (_bkBuyer != null)
            {
                try { if (CaravanTrade284.BuyBlocked(_bkBuyer, __0, __1)) { __result = 0f; return; } }
                catch (Exception e) { Stumble("BkValuePostfix(284)", e); }
            }
            if (_blocks.Count == 0) return;
            try
            {
                var st = __1.Settlement;
                var b = BlockOf(st, __0);
                // Jeff 16.10: "jedni kupuja, drudzy sprzedaja" - inne karawany moga odkupic (sprzedadza z zyskiem tam, gdzie drozej); blokada tylko
                // dla TEJ SAMEJ karawany przy TEJ SAMEJ wizycie (pusty ruch: sprzedaz i odkup tej samej kategorii w jednym miescie)
                Visit v0;
                if (b == null || _bkBuyer == null || !(_visit.TryGetValue(_bkBuyer, out v0) && ReferenceEquals(v0.St, st) && v0.Cats.Contains(__0))) return;
                __result = 0f;
                _bkBlocked++;
                Visit v;
                if (_bkBuyer != null && !(_visit.TryGetValue(_bkBuyer, out v) && ReferenceEquals(v.St, st) && v.Cats.Contains(__0))) _bkBlockedOther++;
            }
            catch (Exception e) { Stumble("BkValuePostfix", e); }
        }

        // ------------------------------------------------------------ wjazd do miasta

        private sealed class Cand { public ItemObject It; public EquipmentElement El; public int Have; public double Unit, Min; public bool Food; public float Gain; public int Price; }

        /// <summary>Powod sprzedazy na stracie (0 - brak wyjscia nie zachodzi): 1 rozwiazanie, 2 brak na zold, 3 juki ponad udzwig, 4 jedzenie psuje sie, 5 dlugo w jukach
        /// (poprawka 132a: tylko bez rynku z zyskiem - market z AgeMarket).</summary>
        private static int LossWhy(bool disband, bool cash, bool full, bool food, double age, bool market)
        {
            if (disband) return 1;
            if (cash) return 2;
            if (full) return 3;
            if (food && age > FoodAgeDays) return 4;
            if (age > MaxAgeDays && !market) return 5;
            return 0;
        }

        internal static void OnEntered(MobileParty mp, Settlement st, Hero hero)
        {
            if (mp == null || st == null || !mp.IsCaravan || !st.IsTown) return;   // tanie wyjscie: zdarzenie pada dla kazdej partii i bohatera
            try
            {
                if (_visit.Count > 0) _visit.Remove(mp);
                var s = Settings.Current;
                if (s == null || !s.CaravanSellWhereDear || st.Town == null || !CaravanBulk.TradesForT1(mp)) return;
                if (MaterialOrders.HasContract(mp)) { _skipContract++; return; }
                if (CaravanBulk.HasOrders(mp)) { _skipOrders++; return; }
                Sell(mp, st, st.Town, s);
            }
            catch (Exception e) { Stumble("OnEntered", e); }
        }

        private static string Name(Settlement st) { return st == null ? "?" : (st.Name != null ? st.Name.ToString() : st.StringId); }

        /// <summary>Cena k-tej sztuki kroku (1 = pierwsza) przy dzisiejszej polce `d`: gra placi sztuka po sztuce i po kazdej sztuce polka rosnie o jej wage
        /// (SellItemsAction.ApplyInternal, TownMarketData.OnTownInventoryUpdated) - ta sama miara co dotad dla ostatniej sztuki kroku.</summary>
        private static int PriceAt(TradeItemPriceFactorModel model, EquipmentElement el, MobileParty mp, ItemData d, int k, int wv)
        {
            return model.GetPrice(el, mp, null, true, d.InStoreValue + (Math.Max(1, k) - 1) * wv, d.Supply, d.Demand);
        }

        /// <summary>Indeks kategorii po sprzedazy k sztuk (ta sama miara co Town.GetItemCategoryPriceIndex - TownMarketData.GetPriceFactor).</summary>
        private static float IndexAfter(TradeItemPriceFactorModel model, ItemCategory cat, ItemData d, int k, int wv)
        {
            return model.GetBasePriceFactor(cat, d.InStoreValue + k * wv, d.Supply, d.Demand, false, 0);
        }

        /// <summary>Indeks kategorii w miescie przed pierwsza sprzedaza T1 tej doby (gdy dzis jeszcze nic - obecny).</summary>
        private static float RefIndex(Town town, ItemCategory cat)
        {
            Dictionary<ItemCategory, float> ix; float v;
            return _idx0.TryGetValue(town, out ix) && ix.TryGetValue(cat, out v) ? v : town.GetItemCategoryPriceIndex(cat);
        }

        /// <summary>S1: czy porcja k sztuk na stracie miesci sie w granicach (0 tak; 1 indeks spadlby ponizej 0.70 x indeksu sprzed pierwszej sprzedazy doby;
        /// 2 cena k-tej sztuki < 0.60 x wartosci).</summary>
        private static int PortionStop(TradeItemPriceFactorModel model, EquipmentElement el, MobileParty mp, ItemCategory cat, ItemData d, int k, int wv, float ref0, int value)
        {
            if (ref0 > 0f && IndexAfter(model, cat, d, k, wv) < (1f - PortionDrop) * ref0) return 1;
            if (PriceAt(model, el, mp, d, k, wv) < LossFloor * value) return 2;
            return 0;
        }

        private static void Sell(MobileParty mp, Settlement st, Town town, Settings s)
        {
            _entries++;
            var pack = mp.ItemRoster;
            var model = Campaign.Current.Models.TradeItemPriceFactorModel;
            if (pack == null || model == null) return;
            double now = Now;
            int wage = Math.Max(0, mp.TotalWage), cap = Math.Max(1, mp.InventoryCapacity);
            bool disband = mp.IsDisbanding;
            double foodUse = Math.Max(0.0, -mp.FoodChange);
            int foodKeep = (int)Math.Ceiling(foodUse * FoodKeepDays), foodHave = 0;
            double keepK = 1.0 - TownToll.CaravanRateFor(mp, st);          // utarg po mycie E3-G3 (poprawki: prog zysku netto); R3-05a: z oplata jarmarku
            // ksiega: koszt calego ladunku towarow handlowych (podzial zoldu) i kandydaci
            double cargo = 0;
            var cands = new List<Cand>();
            for (int i = 0; i < pack.Count; i++)
            {
                var el = pack.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0) continue;
                if (it.IsFood) foodHave += el.Amount;
                if (!it.IsTradeGood) continue;
                var l = Sync(mp, it, pack.GetItemNumber(it), true);
                if (l == null || l.N <= 0) continue;
                cargo += l.Cost * el.Amount / l.N;
                if (it.HasHorseComponent || it.ItemCategory == null || it.ItemCategory == DefaultItemCategories.PackAnimal || it.NotMerchandise || CaravanBulk.Owns(it)) continue;
                bool dup = false;
                foreach (var c in cands) if (ReferenceEquals(c.It, it)) { dup = true; break; }
                if (dup) continue;
                cands.Add(new Cand { It = it, El = el.EquipmentElement, Have = pack.GetItemNumber(it), Unit = l.Cost / l.N, Food = it.IsFood });
            }
            if (cands.Count == 0) return;
            foreach (var c in cands)
            {
                var l = Sync(mp, c.It, c.Have, false);
                double age = l != null ? Math.Min(MaxAgeDays, Math.Max(0, now - l.Day)) : 0;
                double travel = cargo > 0 ? c.Unit * Math.Min(1.0, wage * age / cargo) : 0;
                c.Min = s.CaravanSellNoSunkWage ? c.Unit : c.Unit + travel;   // 284: zold juz wydany nie podnosi ceny (koszt utopiony); wyjscia na stracie bez zmian
                c.Price = Math.Max(1, town.GetItemPrice(c.El, mp, true));
                c.Gain = (float)((c.Price * keepK - c.Min) / c.Price);
            }
            cands.Sort((a, b) => b.Gain.CompareTo(a.Gain));             // najpierw najwiekszy zysk na denara kasy miasta
            int reserve = (int)Math.Max(0f, s.TownRentFloorGold);
            int today = (int)now;
            if (today != _townDay) { _townLeft.Clear(); _townDay = today; }
            int left;
            if (!_townLeft.TryGetValue(town, out left))
            {
                int spare0 = town.Gold - TownPurse.BuyFloorPeek(town, reserve);   // poprawki (uwaga recenzji 2): wycena limitu bez licznikow 169c
                left = Math.Max(0, spare0) / TownDayShare;
                _townLeft[town] = left; _townSpare0 += Math.Max(0, spare0);
            }
            bool any = false;
            List<(EquipmentElement, int)> done = null;
            foreach (var c in cands)
            {
                var it = c.It; var cat = it.ItemCategory;
                int maxSell = (c.Have + 1) / 2, sold = 0;
                int wv = Math.Max(1, HistoricalPrices.ShelfWorth(it));
                for (int guard = 0; guard < 100 && sold < maxSell; guard++)
                {
                    int at = pack.FindIndexOfItem(it);
                    if (at < 0) break;
                    var el = pack.GetElementCopyAtIndex(at);
                    if (el.Amount <= 0) break;
                    int step = Math.Min(it.Value > DearValue ? 1 : StepN, Math.Min(maxSell - sold, el.Amount));
                    if (c.Food)
                    {
                        int room = foodHave - foodKeep;
                        if (room <= 0) { _rFood++; break; }
                        step = Math.Min(step, room);
                    }
                    if (step <= 0) break;
                    var d = town.MarketData.GetCategoryData(cat);
                    int pLast = PriceAt(model, el.EquipmentElement, mp, d, step, wv);   // cena ostatniej sztuki kroku
                    var lot = Sync(mp, it, pack.GetItemNumber(it), false);
                    double age = lot != null ? Math.Max(0, now - lot.Day) : 0;
                    float wgt = it.Weight;
                    bool cash = mp.PartyTradeGold < CashDays * wage, full = wgt > 0f && mp.TotalWeightCarried > cap;   // poprawki (uwaga recenzji 2): ponad udzwig, jak BK
                    bool profit = pLast * keepK > c.Min;
                    if (!profit && step > 1)
                    {
                        // S1: krok nie przeskakuje progu zysku - najwiecej sztuk, z ktorych OSTATNIA ma jeszcze zysk (gra placi sztuka po sztuce); reszta - regula straty
                        int kp = step - 1;
                        while (kp >= 1 && PriceAt(model, el.EquipmentElement, mp, d, kp, wv) * keepK <= c.Min) kp--;
                        if (kp >= 1) { step = kp; pLast = PriceAt(model, el.EquipmentElement, mp, d, kp, wv); profit = true; _profitCut++; }
                    }
                    int why = profit ? 0 : LossWhy(disband, cash, full, c.Food, age, AgeMarket(mp, it, c.Min, keepK, age));   // 132a: > 30 dob bez rynku z zyskiem
                    if (why == 3)
                    {
                        // juki ponad udzwig: na stracie tylko tyle wagi, ile trzeba do zejscia pod udzwig
                        int need = Math.Max(1, (int)Math.Ceiling((mp.TotalWeightCarried - cap) / wgt));
                        if (need < step)
                        {
                            step = need; _fullCut++;
                            pLast = PriceAt(model, el.EquipmentElement, mp, d, step, wv);
                            if (pLast * keepK > c.Min) { profit = true; why = 0; }
                        }
                    }
                    if (!profit && why == 0) { _rNoProfit++; break; }
                    if (why >= 3)
                    {
                        // S1: na stracie (juki ponad udzwig, jedzenie psuje sie, dlugo w jukach) porcjami - sztuka po sztuce, stop przed sztuka, ktora zbilaby indeks
                        // kategorii w tym miescie ponizej 0.70 x indeksu sprzed pierwszej sprzedazy doby albo poszlaby ponizej 0.60 x wartosci; reszta czeka
                        float ref0 = RefIndex(town, cat);
                        int kl = step, stop = 0;
                        for (; kl >= 1; kl--)
                        {
                            stop = PortionStop(model, el.EquipmentElement, mp, cat, d, kl, wv, ref0, it.Value);
                            if (stop == 0) break;
                        }
                        if (kl <= 0) { if (stop == 1) _rPortionIdx++; else _rPortionPrice++; break; }
                        if (kl < step) { step = kl; _portionCut++; pLast = PriceAt(model, el.EquipmentElement, mp, d, kl, wv); }
                    }
                    if (pLast < GabkaFloor * it.Value) { _rGabka++; break; }
                    int price = Math.Max(1, town.GetItemPrice(el.EquipmentElement, mp, true));
                    int spare = town.Gold - TownPurse.BuyFloor(town, reserve);
                    if (spare < price) { _rCash++; break; }
                    step = Math.Min(step, spare / price);
                    if (left < price) { _rLimit++; break; }
                    step = Math.Min(step, left / price);
                    if (step <= 0) { _rLimit++; break; }
                    // blokada odkupienia: stan polki kategorii przed sprzedaza (nowa blokada liczy od niego) i indeks przed pierwsza sprzedaza tej doby (NE3-5b)
                    int inStore0 = town.MarketData.GetItemCountOfCategory(cat);
                    Dictionary<ItemCategory, float> ix0;
                    if (!_idx0.TryGetValue(town, out ix0)) { ix0 = new Dictionary<ItemCategory, float>(); _idx0[town] = ix0; }
                    if (!ix0.ContainsKey(cat)) ix0[cat] = town.GetItemCategoryPriceIndex(cat);
                    int purse = mp.PartyTradeGold, had = pack.GetItemNumber(it), tg0 = town.Gold;
                    bool broke = false;
                    TownToll.LastTaken = 0; TownToll.LastToTown = 0;
                    CaravanTrade284.Path = CaravanTrade284.PathT1;   // 284: droga sprzedazy w ksiedze zysku
                    try { SellItemsAction.Apply(mp.Party, town.Owner, el, step, st); }
                    catch (Exception e) { broke = true; Stumble("Sell(SellItemsAction)", e); }
                    finally { CaravanTrade284.Path = 0; }
                    int moved = had - pack.GetItemNumber(it);
                    int toll = TownToll.LastTaken; TownToll.LastTaken = 0;
                    int tollTown = TownToll.LastToTown; TownToll.LastToTown = 0;
                    int gross = Math.Max(0, mp.PartyTradeGold - purse) + toll;   // utarg przed mytem E3-G3
                    if (broke && moved > 0 && gross == 0)
                    {
                        try { town.Owner.ItemRoster.AddToCounts(el.EquipmentElement, -moved); pack.AddToCounts(el.EquipmentElement, moved); moved = 0; }
                        catch (Exception e) { Stumble("Sell(cofniecie)", e); }
                    }
                    // kontrola (poprawki, uwaga recenzji 1): zmierzone po obu stronach - kasa miasta i kiesa karawany, takze przy wyjatku
                    _cTown += tg0 - town.Gold; _cPurse += mp.PartyTradeGold - purse; _cToll += toll; _cTollTown += tollTown;
                    if (moved <= 0) break;
                    sold += moved; any = true;
                    left = Math.Max(0, left - gross);
                    if (c.Food) foodHave -= moved;
                    _gross += gross; _toll += toll;
                    double cost = c.Unit * moved;
                    Row r; if (!_rows.TryGetValue(it, out r)) { r = new Row(); _rows[it] = r; }
                    if (why == 0) { r.ProfitN += moved; r.ProfitG += gross; r.ProfitCost += (long)cost; }
                    else
                    {
                        r.LossN += moved; r.LossG += gross; r.LossCost += (long)cost;
                        switch (why) { case 1: _lDisband += moved; _lDisbandG += gross; break; case 2: _lCash += moved; _lCashG += gross; break; case 3: _lFull += moved; _lFullG += gross; break; case 4: _lFood += moved; _lFoodG += gross; break; default: _lAge += moved; _lAgeG += gross; break; }
                    }
                    Visit v; if (!_visit.TryGetValue(mp, out v)) { v = new Visit { St = st }; _visit[mp] = v; }
                    v.Cats.Add(cat);
                    // blokada odkupienia w tym miescie: nowa (stan polki sprzed sprzedazy) albo przedluzona
                    var bl = BlockOf(st, cat);
                    if (!Alive(bl, inStore0, today))
                    {
                        Dictionary<ItemCategory, Block> bd;
                        if (!_blocks.TryGetValue(st, out bd)) { bd = new Dictionary<ItemCategory, Block>(); _blocks[st] = bd; }
                        bl = new Block { Day = today, Base = inStore0 };
                        bd[cat] = bl;
                    }
                    bl.Day = today; bl.Sold += moved;
                    // najwiekszy spadek indeksu sprzedanej kategorii w jednym miescie od pierwszej sprzedazy tej doby (NE3-5b)
                    float i0 = ix0[cat], i1 = town.GetItemCategoryPriceIndex(cat);
                    if (i0 > 0f)
                    {
                        float drop = 1f - i1 / i0;
                        if (drop > _dropMax) { _dropMax = drop; _dropWhere = Name(st) + " " + cat.StringId + " " + i0.ToString("0.00", Inv) + " -> " + i1.ToString("0.00", Inv); }
                        int kind = why == 0 ? 0 : (why >= 3 ? 1 : 2);                // S1: z zyskiem / na stracie porcjami / bez wyjscia (rozwiazanie, brak na zold)
                        if (drop > _dropKind[kind]) _dropKind[kind] = drop;
                    }
                    if (done == null) done = new List<(EquipmentElement, int)>();
                    done.Add((el.EquipmentElement, moved));
                    if (broke) break;
                }
                if (sold >= maxSell && sold > 0) _rHalf++;
            }
            _townLeft[town] = left;
            if (any) _sellers++;
            if (done != null)
            {
                try { CampaignEventDispatcher.Instance.OnCaravanTransactionCompleted(mp, town, done); }   // jak CaravanBulk: sluchacze handlu karawan (marza P1, ksiegi)
                catch (Exception e) { Stumble("Sell(zdarzenie)", e); }
            }
        }

        // ------------------------------------------------------------ doba
        private static string Median(ItemCategory c)
        {
            var l = new List<float>();
            foreach (var t in Town.AllTowns) if (t != null) l.Add(t.GetItemCategoryPriceIndex(c));
            if (l.Count == 0) return "-";
            l.Sort();
            return l[l.Count / 2].ToString("0.00", Inv);
        }

        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            try
            {
                int day = (int)Now - 1;
                EnsureSnap();
                if (!_startDone)
                {
                    _startDone = true;
                    Log.Info("Karawany - towar tam, gdzie maja zysk (E3-T1): start sesji | klucz Caravan Sell Where Dear " + (s.CaravanSellWhereDear ? "tak" : "nie")
                             + " | latka SellItemsAction.ApplyInternal " + (_hooked ? "wpieta" : "NIE") + " (ksiega kosztu partii w jukach - sesja)"
                             + " | migawka ksiegi przy wczytaniu: karawan " + _snapCaravans + ", sztuk " + _snapUnits + " (cena z dziennika BK " + _snapBk + ", ze sredniej indeksu " + _snapAvg
                             + "; koszt = wartosc x max(1, srednia indeksu 97 miast), z dziennika BK wyzsza z cen; dzien z dziennika BK, bez wpisu " + BkLogDays + " dob wstecz; dziennik BK " + _bkLogState + ")"
                             + " | regula: sprzedaz, gdy cena po mycie E3-G3 > koszt zakupu" + (s.CaravanSellNoSunkWage ? " (284: bez zoldu juz wydanego)" : " + zold za dni w drodze (najwyzej " + MaxAgeDays + " dob, najwyzej 100% kosztu)") + "; na stracie tylko: rozwiazanie, kiesa < "
                             + CashDays + " dni zoldu, juki ponad udzwig (tylko do zejscia pod udzwig), jedzenie > " + FoodAgeDays + " dob, towar > " + MaxAgeDays + " dob"
                             + " | na stracie (poza rozwiazaniem i brakiem na zold) porcjami: indeks kategorii w miescie najwyzej -" + (100 * PortionDrop).ToString("0", Inv)
                             + "% w dobie wobec indeksu przed pierwsza sprzedaza, cena sztuki >= " + LossFloor.ToString("0.00", Inv) + " x wartosci (reszta czeka na nastepne miasto); krok z zyskiem"
                             + " nie przeskakuje progu zysku (cena ostatniej sztuki kroku, krok przyciety do sztuk z zyskiem)"
                             + " | bez progu sredniej (Jeff 16.10); zawsze: ta sama karawana nie odkupuje przy tej samej wizycie kategorii, ktora sprzedala (inne karawany moga - Jeff 16.10; blokada "
                             + (_bkHooked ? "wpieta" : "NIE") + "), cena >= " + GabkaFloor.ToString("0.00", Inv)
                             + " x wartosci (gabka), kasa miasta ponad BuyFloor i najwyzej 1/" + TownDayShare + " nadwyzki na dobe, krok " + StepN + " szt. (ponad " + DearValue + " d - 1), polowa stosu,"
                             + " jedzenie na " + FoodKeepDays + " dob.");
                }
                try { ProfitToOwners(s, day); } catch (Exception e) { OStumble("ProfitToOwners", e); }   // E3-T2 (S2): wlasna linia doby, wlasny licznik potkniec
                // juki wybranych towarow (raz na dobe) i porzadki ksiegi i blokad
                var cats = new Dictionary<ItemCategory, string>();
                var om = MBObjectManager.Instance;
                if (om != null) foreach (var id in WatchIds) { var c = om.GetObject<ItemCategory>(id); if (c != null && !cats.ContainsKey(c)) cats[c] = id; }
                var held = new Dictionary<ItemCategory, long>();
                int caravans = 0, books = 0; long bookUnits = 0;
                var gone = new List<MobileParty>();
                foreach (var kv in _book)
                {
                    if (kv.Key == null || !kv.Key.IsActive || !kv.Key.IsCaravan) { gone.Add(kv.Key); continue; }
                    books++;
                    foreach (var l in kv.Value.Values) bookUnits += l.N;
                }
                foreach (var p in gone) _book.Remove(p);
                var vgone = new List<MobileParty>();
                foreach (var kv in _visit) if (kv.Key == null || !kv.Key.IsActive || !ReferenceEquals(kv.Key.CurrentSettlement, kv.Value.St)) vgone.Add(kv.Key);
                foreach (var p in vgone) _visit.Remove(p);
                int today = (int)Now, blocks = 0, blockTowns = 0; long blockSold = 0;
                var bgone = new List<Settlement>();
                foreach (var kv in _blocks)
                {
                    var t = kv.Key != null ? kv.Key.Town : null;
                    var dead = new List<ItemCategory>();
                    foreach (var b in kv.Value) if (t == null || !Alive(b.Value, t.MarketData.GetItemCountOfCategory(b.Key), today)) dead.Add(b.Key); else { blocks++; blockSold += b.Value.Sold; }
                    foreach (var c in dead) kv.Value.Remove(c);
                    if (kv.Value.Count == 0) bgone.Add(kv.Key); else blockTowns++;
                }
                foreach (var st in bgone) _blocks.Remove(st);
                foreach (var c in MobileParty.AllCaravanParties)
                {
                    if (c == null || !c.IsActive || c.ItemRoster == null) continue;
                    caravans++;
                    var r = c.ItemRoster;
                    for (int i = 0; i < r.Count; i++)
                    {
                        var el = r.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (it == null || it.ItemCategory == null || el.Amount <= 0 || !cats.ContainsKey(it.ItemCategory)) continue;
                        long h0; held.TryGetValue(it.ItemCategory, out h0); held[it.ItemCategory] = h0 + el.Amount;
                    }
                }
                var soldBy = new Dictionary<ItemCategory, long>();
                foreach (var kv in _rows) { var c = kv.Key.ItemCategory; if (c == null) continue; long x; soldBy.TryGetValue(c, out x); soldBy[c] = x + kv.Value.ProfitN + kv.Value.LossN; }
                long pN = 0, pG = 0, pC = 0, lN = 0, lG = 0, lC = 0;
                var rows = new List<KeyValuePair<ItemObject, Row>>(_rows);
                foreach (var kv in rows) { pN += kv.Value.ProfitN; pG += kv.Value.ProfitG; pC += kv.Value.ProfitCost; lN += kv.Value.LossN; lG += kv.Value.LossG; lC += kv.Value.LossCost; }
                rows.Sort((a, b) => (b.Value.ProfitG + b.Value.LossG).CompareTo(a.Value.ProfitG + a.Value.LossG));
                long ctl = _cTown - (_cPurse + _cToll - _cTollTown);
                var sb = new StringBuilder(3400);
                sb.Append("Karawany - towar tam, gdzie maja zysk (E3-T1): dzien ").Append(day).Append(" | ").Append(s.CaravanSellWhereDear ? "CZYNNE" : "WYLACZONE (MCM)")
                  .Append(" | wjazdow z towarem ").Append(_entries).Append(", sprzedaly ").Append(_sellers)
                  .Append(" | z zyskiem ").Append(pN).Append(" szt. za ").Append(pG).Append(" (koszt zakupu ").Append(pC).Append(", marza ").Append(pC > 0 ? ((pG / (double)pC - 1) * 100).ToString("+0.0;-0.0", Inv) + "%" : "-").Append(')')
                  .Append(" | na stracie ").Append(lN).Append(" szt. za ").Append(lG).Append(" (koszt ").Append(lC).Append(", strata ").Append(Math.Max(0, lC - lG)).Append("): rozwiazanie ").Append(_lDisband).Append('/').Append(_lDisbandG)
                  .Append(", brak na zold ").Append(_lCash).Append('/').Append(_lCashG).Append(", pelne juki (ponad udzwig) ").Append(_lFull).Append('/').Append(_lFullG)
                  .Append(", jedzenie psuje sie ").Append(_lFood).Append('/').Append(_lFoodG).Append(", dlugo w jukach ").Append(_lAge).Append('/').Append(_lAgeG).Append(" (szt./zl)")
                  .Append("; kroki przyciete do zejscia pod udzwig ").Append(_fullCut)
                  .Append(" | odrzucone kroki: bez zysku ").Append(_rNoProfit).Append(", gabka (cena < ").Append(GabkaFloor.ToString("0.00", Inv)).Append(" x wartosci) ").Append(_rGabka)
                  .Append(", kasa miasta (BuyFloor) ").Append(_rCash).Append(", limit 1/").Append(TownDayShare).Append(" nadwyzki ").Append(_rLimit).Append(", rezerwa jedzenia ").Append(_rFood)
                  .Append(", polowa stosu ").Append(_rHalf)
                  .Append(", wstrzymane (porcja) ").Append(_rPortionIdx + _rPortionPrice).Append(" (indeks kategorii w miescie spadlby ponizej ").Append((1f - PortionDrop).ToString("0.00", Inv))
                  .Append(" x indeksu sprzed pierwszej sprzedazy doby ").Append(_rPortionIdx).Append(", cena sztuki < ").Append(LossFloor.ToString("0.00", Inv)).Append(" x wartosci ").Append(_rPortionPrice)
                  .Append("; kroki przyciete porcja ").Append(_portionCut).Append("); kroki przyciete do sztuk z zyskiem ").Append(_profitCut)
                  .Append(" | pominiete: kontrakt 174 ").Append(_skipContract).Append(", rozkaz gracza (BK) ").Append(_skipOrders)
                  .Append(" | wedlug towaru (z zyskiem / na stracie, szt./zl): ");
                int k = 0;
                foreach (var kv in rows)
                {
                    if (k++ >= 10) break;
                    if (k > 1) sb.Append(", ");
                    sb.Append(kv.Key.StringId).Append(' ').Append(kv.Value.ProfitN).Append('/').Append(kv.Value.ProfitG).Append(" ; ").Append(kv.Value.LossN).Append('/').Append(kv.Value.LossG);
                }
                if (k == 0) sb.Append('-');
                sb.Append(" | zloto: z kas miast ").Append(_gross).Append(" (nadwyzka kas ponad BuyFloor przy pierwszej wizycie ").Append(_townSpare0).Append("), myto E3-G3 ").Append(_toll)
                  .Append(", przyrost kies karawan ").Append(_gross - _toll)
                  .Append("; kontrola: kasy -").Append(_cTown).Append(" = kiesy +").Append(_cPurse).Append(" + myto ").Append(_cToll).Append(" - myto do kas osad ").Append(_cTollTown).Append(" (roznica ").Append(ctl).Append(')')
                  .Append(" | blokada odkupienia (miasto, kazda karawana, do ").Append(BlockDays).Append(" dob albo do zejscia z polki): wycen BK zablokowanych ").Append(_bkBlocked)
                  .Append(" (w tym innym karawanom ").Append(_bkBlockedOther).Append("), blokad czynnych ").Append(blocks).Append(" w ").Append(blockTowns).Append(" miastach (sprzedane w nich ").Append(blockSold).Append(" szt.)")
                  .Append("; odkupione mimo to: w tej samej wizycie (gabka, inne) ").Append(_rebought).Append(" szt., przez inne karawany ").Append(_reboughtOther).Append(" szt.")
                  .Append(" | najwiekszy spadek indeksu sprzedanej kategorii w jednym miescie dzis ").Append((100 * _dropMax).ToString("0.0", Inv)).Append('%').Append(_dropWhere != null ? " (" + _dropWhere + ")" : "")
                  .Append(" | spadek indeksu wedlug rodzaju sprzedazy (najwiekszy w jednym miescie dzis): z zyskiem ").Append((100 * _dropKind[0]).ToString("0.0", Inv))
                  .Append("%, na stracie porcjami ").Append((100 * _dropKind[1]).ToString("0.0", Inv)).Append("% (granica ").Append((100 * PortionDrop).ToString("0", Inv))
                  .Append("%), bez wyjscia (rozwiazanie, brak na zold) ").Append((100 * _dropKind[2]).ToString("0.0", Inv)).Append('%')
                  .Append(" | ksiega kosztu: karawan ").Append(books).Append(", sztuk ").Append(bookUnits).Append(", zakupow dzis ").Append(_bookBuys).Append(" (").Append(_bookBuyUnits).Append(" szt.)")
                  .Append(", odtworzone przy wczytaniu ").Append(_snapUnits).Append(" szt. (z dziennika BK ").Append(_snapBk).Append(", ze sredniej indeksu ").Append(_snapAvg).Append("; dziennik BK ").Append(_bkLogState)
                  .Append("), dzis bez zakupu w ksiedze ").Append(_rebuiltLate).Append(" szt.")
                  .Append(" | w jukach ").Append(caravans).Append(" karawan (szt., dni do wyczerpania przy dzisiejszej sprzedazy T1): ");
                k = 0;
                foreach (var kv in cats)
                {
                    long h0; held.TryGetValue(kv.Key, out h0); long sd; soldBy.TryGetValue(kv.Key, out sd);
                    if (k++ > 0) sb.Append(", ");
                    sb.Append(kv.Value).Append(' ').Append(h0).Append(" (").Append(sd > 0 ? (h0 / (double)sd).ToString("0.0", Inv) : "-").Append(')');
                }
                sb.Append(" | indeks mediana: ");
                k = 0;
                foreach (var kv in cats) { if (k++ > 0) sb.Append(", "); sb.Append(kv.Value).Append(' ').Append(Median(kv.Key)); }
                sb.Append(" | wplyw na skrzynie: kasy miast -").Append(_gross).Append(", karawany +").Append(_gross - _toll).Append(", myto (glowy rodow, skarbce, kasy - linia E3-G3) +").Append(_toll)
                  .Append(" | potkniecia ").Append(_stumbles).Append(" (sesja ").Append(_stumbleSess).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ZeroDay(); }
        }

        // ------------------------------------------------------------ E3-T2 (S2, strojenie po tescie 120 dob; klucz CaravanProfitToOwner) - zysk karawany do wlasciciela
        /// <summary>
        /// Dowod [L] (test 16.10 02-08-15, 120 dob na zapisie F, baza WGRANIA 64 20-16-31): kiesy karawan 4.34 -> 5.73 mln (+32%), 2.62% zlota swiata wobec 1.98%
        /// w bazie, nachylenie +19.8 tys./d - zysk T1 (z kas miast ok. 59 tys./d) zostaje w kiesach. Przyczyna [K]: przy BK Realistic Caravan Income
        /// (u Jeffa wlaczone) gra nie oddaje zysku karawan wlascicielom - BK EconomyPatches.AddIncomeFromPartyPrefix wylacza AddIncomeFromParty karawan
        /// (gra: 10% kiesy ponad 10 000 na dobe do rodu, DefaultClanFinanceModel :693-718), a BKClanFinanceModel.CalculateOwnerIncomeFromCaravan zwraca 0
        /// (gra: (kiesa - kapital startowy) / RevenueSmoothenFraction na dobe do notabla, :864-889); zostaje BKPartyBehavior.AddRealisticIncome: wszystko
        /// ponad 10 000 tylko przy wjezdzie do siedziby, wlasnej osady albo miasta udzialowca wlasciciela (BK :941-975; E3/9 Seal31B - takze miasta krolestwa
        /// pana) - 2-9 wplat na dobe na ok. 350 karawan (328 to karawany notabli). ROT.Models.ROTClanFinanceModel deleguje do BK.
        /// Regula (raz na dobe, CaravanGoods.Daily, przed ksiega pieniadza): karawana bez wodza oddaje wlascicielowi (CaravanPartyComponent.Owner - notabl,
        /// lord albo gracz; ten sam odbiorca co BK) wszystko ponad kapital obrotowy = max(10 000, 30 dni zoldu): 10 000 to kapital startowy gry
        /// (DefaultCaravanModel.GetInitialTradeGold) i prog wplaty BK, wystarcza na pelny zakup BK w miescie (BuyCategory: najwyzej 1 500 na kategorie, 5 kategorii;
        /// BudgetFactor pelny od 5 000). Zadnego dubla: tylko przy BK Realistic Caravan Income; karawana z wodzem pominieta (gra placi z niej rodowi przez
        /// AddIncomeFromParty - BK puszcza ja, gdy wodz nie ma tytulu); karawana, za ktora czynny model finansow sam placi (CalculateOwnerIncomeFromCaravan > 0),
        /// pominieta; rozwiazywana (kiesa do wlasciciela przy rozwiazaniu - E3/7) i w bitwie - pominiete; BK wplaca dalej to, co przy wjezdzie ponad 10 000.
        /// Platnik -> odbiorca: kiesa karawany -> zloto wlasciciela (GiveGoldAction.ApplyForPartyToCharacter - zdarzenie gry; ksiega pieniadza widzi przelew,
        /// nie zloto z niczego; pozycja w linii "Obieg: notable i karawany"). Notabl ponad 10 500 - pasmo gry oddaje nadwyzke kasie miasta handlu (E5/A3).
        /// Te same skutki co wplata zysku w grze: slawa rodu za perk Great Investor (AddIncomeFromParty, AddRealisticIncome). Gracz = AI; jeden komunikat dziennie.
        /// Nic w zapisie. Wyjatek per karawana - licznik potkniec (reszta karawan dalej).
        /// </summary>
        private const int KeepGold = 10000;
        internal const int KeepWageDays = 30;   // 259: ten sam zapas zoldu dla karawany rodu, ktorej rod nie zwraca zoldu (CaravanSettle - wodz z tytulem Lordship)
        internal static long LastOwnerPaid, LastOwnerNot, LastOwnerLord, LastOwnerPlayer;   // linia "Obieg: notable i karawany" (MoneyLedger.Daily - ta sama doba)
        internal static int LastOwnerN;
        private static int _oStumbles, _oStumbleSess;

        private static void OStumble(string where, Exception e)
        {
            _oStumbles++; _oStumbleSess++;
            try { if (_err.Add(where)) Log.Error("CaravanGoods." + where, e); } catch { }
        }

        private static void ProfitToOwners(Settings s, int day)
        {
            LastOwnerPaid = LastOwnerNot = LastOwnerLord = LastOwnerPlayer = 0; LastOwnerN = 0;
            var sb = new StringBuilder(1400);
            sb.Append("Karawany - zysk do wlasciciela (E3-T2): dzien ").Append(day);
            if (!s.CaravanProfitToOwner)
            {
                Log.Info(sb.Append(" | WYLACZONE (MCM) | potkniecia ").Append(_oStumbles).Append(" (sesja ").Append(_oStumbleSess).Append(").").ToString());
                _oStumbles = 0;
                return;
            }
            if (!Seal31B.RealisticCaravanIncome())
            {
                Log.Info(sb.Append(" | NIECZYNNE: BK Realistic Caravan Income wylaczone albo brak BK - gra i BK same oddaja zysk karawan wlascicielom (bez dubla) | potkniecia ")
                           .Append(_oStumbles).Append(" (sesja ").Append(_oStumbleSess).Append(").").ToString());
                _oStumbles = 0;
                return;
            }
            int n = 0, below = 0, paidN = 0, notN = 0, lordN = 0, plN = 0, sDis = 0, sBat = 0, sLead = 0, sOwn = 0, sModel = 0, sClan = 0;
            long keepAll = 0, held = 0, paid = 0, pNot = 0, pLord = 0, pPl = 0, ctlFrom = 0, ctlTo = 0;
            var cfm = Campaign.Current.Models.ClanFinanceModel;
            var list = new List<MobileParty>(MobileParty.AllCaravanParties);
            foreach (var mp in list)
            {
                try
                {
                    if (mp == null || !mp.IsActive || !mp.IsCaravan) continue;
                    n++;
                    // 259: karawana lorda albo towarzysza, ktorej zold placi rod - rozlicza ja rod codziennie (CaravanSettle, w rozliczeniu rodu); tu tylko licznik
                    if (CaravanSettle.Settled(mp)) { sClan++; continue; }
                    int g = mp.PartyTradeGold;
                    long keep = Math.Max(KeepGold, (long)KeepWageDays * Math.Max(0, mp.TotalWage));
                    keepAll += keep; held += Math.Min(Math.Max(0, g), keep);
                    if (g <= keep) { below++; continue; }
                    if (mp.IsDisbanding) { sDis++; continue; }
                    if (mp.MapEvent != null) { sBat++; continue; }
                    if (mp.LeaderHero != null) { sLead++; continue; }
                    var owner = mp.Owner;
                    if (owner == null || !owner.IsAlive) { sOwn++; continue; }
                    if (cfm != null && cfm.CalculateOwnerIncomeFromCaravan(mp) > 0) { sModel++; continue; }
                    int amt = (int)Math.Min(int.MaxValue, g - keep);
                    int g0 = mp.PartyTradeGold, h0 = owner.Gold;
                    GiveGoldAction.ApplyForPartyToCharacter(mp.Party, owner, amt, true);
                    int moved = g0 - mp.PartyTradeGold, got = owner.Gold - h0;
                    ctlFrom += moved; ctlTo += got;
                    if (moved <= 0) continue;
                    paid += moved; paidN++;
                    if (owner == Hero.MainHero) { pPl += moved; plN++; }
                    else if (owner.IsNotable) { pNot += moved; notN++; }
                    else { pLord += moved; lordN++; }
                    var cl = owner.Clan;
                    if (cl != null && cl.Leader != null && cl.Leader.GetPerkValue(DefaultPerks.Trade.GreatInvestor)) cl.AddRenown(DefaultPerks.Trade.GreatInvestor.PrimaryBonus, true);
                }
                catch (Exception e) { OStumble("ProfitToOwners(karawana)", e); }
            }
            LastOwnerPaid = paid; LastOwnerNot = pNot; LastOwnerLord = pLord; LastOwnerPlayer = pPl; LastOwnerN = paidN;
            if (pPl > 0) Log.Player("Your caravans handed you " + pPl + " denars - all they held above what they keep for trade and a month of wages.");
            sb.Append(" | CZYNNE | karawan ").Append(n).Append(", oddaly ").Append(paidN).Append(" za ").Append(paid).Append(" zl (notablom ").Append(notN).Append('/').Append(pNot)
              .Append(", lordom i innym ").Append(lordN).Append('/').Append(pLord).Append(", graczowi ").Append(plN).Append('/').Append(pPl).Append(')')
              .Append(" | rozliczane przez rod (259) ").Append(sClan)
              .Append(" | kapital obrotowy (zostaje w kiesie): max(").Append(KeepGold).Append(", ").Append(KeepWageDays).Append(" dni zoldu) - razem ").Append(keepAll)
              .Append(", w kiesach do kapitalu ").Append(held).Append(" (").Append(keepAll > 0 ? (100.0 * held / keepAll).ToString("0.0", Inv) : "-").Append("%), karawan z kiesa nie wieksza niz kapital ").Append(below)
              .Append(" | pominiete z nadwyzka: rozwiazywane ").Append(sDis).Append(" (kiesa do wlasciciela przy rozwiazaniu - E3/7), w bitwie ").Append(sBat)
              .Append(", z wodzem ").Append(sLead).Append(" (gra placi rodowi z kiesy partii z wodzem - AddIncomeFromParty), bez zywego wlasciciela ").Append(sOwn)
              .Append(", model finansow placi sam ").Append(sModel)
              .Append(" | kontrola: kiesy karawan -").Append(ctlFrom).Append(" = wlasciciele +").Append(ctlTo).Append(" (roznica ").Append(ctlFrom - ctlTo).Append(')')
              .Append(" | wplyw na skrzynie: karawany -").Append(paid).Append(", notable +").Append(pNot).Append(" (ponad 10 500 pasmo gry oddaje nadwyzke kasie miasta handlu - E5/A3)")
              .Append(", lordowie i inni +").Append(pLord).Append(", gracz +").Append(pPl)
              .Append(" | BK Realistic Caravan Income tak - wplata BK ponad 10 000 przy wjezdzie do siedziby, osady lub miasta udzialowca (i E3/9 w miastach krolestwa pana) zostaje")
              .Append(" | potkniecia ").Append(_oStumbles).Append(" (sesja ").Append(_oStumbleSess).Append(").");
            Log.Info(sb.ToString());
            _oStumbles = 0;
        }
    }
}
