using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// PACZKA E5/B1 (173, PLAN 5.5) - KIESA LUDU CZYNNA (projekt etapu 5 v2 rozdz. 3 "E5/B1", R1, R5, R8, R9, R11-R14, D1, D4; plan kodu
    /// docs/etap5/PLAN-KODU-E5B1.md; model KL docs/PROJEKT-KIESA-LUDU-2026-10-09.md L1-L9, tabele 1.3-1.5). Model = model E5/0 "na sucho" (TownFolk.cs)
    /// z prawdziwymi przelewami: kiesy miast W/R/P i gmina, kiesa podzamcza (zamek) - nowi posiadacze zlota w pamieci moda (zapis arm_townfolk,
    /// ksiega pieniadza H22-H24); wies - jej Village.Gold jak dotad.
    ///  - Jedyne wejscie do ludu: Earn (arytmetyka, nie rzuca; wolajacy przelewa platnikowi reszte): 25% wydatkow zolnierzy, karawan, band i zalog,
    ///    20% dworu (A2 CourtFolkShare), place budow, kopalnie, place warsztatow; w bloku dobowym z kasy osady: robocizna handlu, obrot patrycjatu,
    ///    doplata do 35% wartosci wyrobow, 5% zaworu miasta / 15% zaworu zamku.
    ///  - Jedyne wyjscie na towar: PayForGoods (z CastlePurse.ConsumePostfix First -> TownPurse.OnConsumed / galaz zamku) zamiast cofniecia "zakupow" A1:
    ///    klasy placa swoja czesc z kiesy, kasa dostaje 98%, gmina akcyze 2%; czego lud nie zaplaci - cofniete jak w A1 ("zjedzone bez zaplaty").
    ///    Zamek: podzamcze placi, reszte pokrywa kredyt dworu (80% wplat dworu do siedziby-zamku) bez ruchu zlota (zloto dworu jest juz w kasie).
    ///  - Kotwica (R1, K1): raz na (osade, kategorie, dobe) - slownik budzetu w UpdateDemandShift x czynnik grupy (m x Afford: jedzenie / reszta) i czlon
    ///    dobrobytu wiersza BK (CalculateBudget, Low) tym samym czynnikiem; budzet nadwyzki na kategorie "lepsze".
    ///  - Doba (TownFolk.Daily w bloku): podatki panu od zarobku okna (1/2/3% W/R/P, podzamcze 3%; cech 1% R -> gmina), przelewy z kas, czynsz/leczenie/sluzba,
    ///    gmina (farma panu, roboty), zapas i nadwyzka, zakupy wsi w miescie targowym, kotwica na jutro.
    ///  - Wies (R13): podatek BK x VillageLordScale + VillageLordExtraShare utargu (decyzja Jeffa 12.10: 35/45/55%; 112 dopisuje wsi reszte), renta 20% -> oplata
    ///    stala (PopulationLaw), danina od kiesy ponad 15 dni zakupow. Dziesiecina (R21/C3, TownFolk.Tithe.cs) i tempo nadwyzki FolkSurplusSpendShare (lud miast i wsie).
    ///  - R14: zadowolenie BK grupy z Afford < 0.98 = migawka + min(0, delta BK) - 0.0015. R5: zapasy startowe z daru (nowa kampania) albo z nadwyzki (stary zapis).
    /// Wylacznik TownFolkLive (i TownFolkMode 1, A1 cofa "zakupy"): wylaczony = stan sprzed B1 - kiesy wracaja do kas w pierwszej dobie (ReturnAll).
    /// Kazda osada we wlasnym try, licznik potkniec, bez globalnych wylacznikow (CLAUDE.md 7). Wszystko na watku glownym (poza nim - zachowanie A1).
    /// </summary>
    internal static partial class TownFolk
    {
        // ------------------------------------------------------------ rodzaje zarobku poza oknem Note (podzial klas KL 1.3)
        internal const int ELabour = 11, ETurn = 12, ETopUp = 13, EValve = 14, EPublic = 15;
        private const int EKinds = 16;
        // kategorie "lepsze" (budzet nadwyzki) i zakupy wsi - lista sprawdzana przy pierwszym uzyciu z ItemCategories.All (linia stanu podaje znalezione)
        private static readonly string[] BetterIds = { "meat", "beer", "wine", "linen", "velvet", "pottery", "cheese", "oil" };
        private static readonly string[] VillIds = { "salt", "tools", "linen", "pottery", "beer" };
        private const int ShelfKeep = 5;                      // wies kupuje najwyzej sztuki ponad 5 na polce miasta targowego
        private const double R14Step = 0.0015;               // stala BK z galezi braku (EconomyPatches :532-544)
        private const double OfferIdxPow = 0.15;             // BK CalculateBudget: popyt x indeks^0.15 (zwykla polityka) - oferta nadwyzki w zlocie

        // ------------------------------------------------------------ przelaczniki
        private static float Unit(float v) { return ValveSplit.Unit(v); }

        /// <summary>Kiesa czynna z ustawien (bez GameStarted): TownFolkLive, TownFolkMode >= 1 i A1 cofa "zakupy" (TownPurse.NoMintNow) - bez A1 lud placilby za towar,
        /// za ktory BK i tak dopisuje zloto (KL 5.1 pkt 3). NIE zalezy od LogEnabled.</summary>
        internal static bool LiveKey { get { var s = Settings.Current; return s != null && s.TownFolkLive && s.TownFolkMode >= 1 && TownPurse.NoMintNow; } }
        /// <summary>E5/B1 czynne: klucz i kampania po GameStarted (tworzenie swiata - stan sprzed B1).</summary>
        internal static bool Live { get { var c = Campaign.Current; return LiveKey && c != null && c.GameStarted; } }
        /// <summary>Zamki: dodatkowo zawor zamku 110 (CastlePurseEnabled - cofa "zakupy" zamkow).</summary>
        internal static bool CastleLive { get { var s = Settings.Current; return Live && s != null && s.CastlePurseEnabled; } }

        // ------------------------------------------------------------ stan kampanii (zapis arm_townfolk) i sesji
        private static int _seedT, _seedC;                   // zapasy startowe: 0 jeszcze nie, 1 z daru (nowa kampania), 2 z nadwyzki (stary zapis), 3 wylaczone
        private static string _pending;                      // zapis wczytany, osady rozwiazywane przy pierwszym uzyciu
        private static long _orphan;                         // kiesy osad nieznanych przy wczytaniu - do kasy miasta w najblizszej dobie (posiadacz "kiesy ludu miast")
        private static string _stateKey;
        private static Town _tickTown; private static int _tickPaid;   // zaplata ludu w ticku konsumpcji (MoneyLedger.ConsumePostfix)
        private static double _brFacFood = 1, _brFacRest = 1; private static bool _brAnchor;
        private static readonly List<ItemCategory> _keys = new List<ItemCategory>();
        private static List<ItemCategory> _better, _villCats; private static string _betterText = "-", _villText = "-";
        private static long _ctlStock0 = long.MinValue, _ctlIn, _ctlOut;   // kontrola zamknieta kies (miasta, zamki, gminy)

        // liczniki okna (zdarzenia miedzy dobami: konsumpcja, Earn, zawor) - DailyLive zamyka
        private sealed class LWin
        {
            public long PaidW, PaidR, PaidP, PaidC, Court, UnpaidT, UnpaidC, GmFood, Excise, MadeT, MadeC, FromTown, ValveT, ValveC, CourtGrant, SlaveW, VFee;
            public int Ticks, TicksC, Fallback, R14Food, R14Rest, VFeeN; public double R14Bk; public long VTaxCut, VTaxBase, VTaxAdd;
            public readonly long[] Earn = new long[EKinds]; public long EarnC;
            public int C2Hit, C2NoFood, C2Wait, C2NoShelf;     // E5/C2: korekty jedzenia R14 i zdjete (brak jedzenia, poza glodem z braku pieniedzy, bez jedzenia na polce)
            // P1 (pomiar, TownFolk.Savings.cs): oferta nadwyzki dopisana do budzetu kategorii "lepszych" (zl), kupione z niej, oferta bez wiersza na polce; bez kategorii w slowniku
            public readonly double[] OfX = new double[OfMax], OfBought = new double[OfMax], OfNoRow = new double[OfMax];
            public readonly int[] OfN = new int[OfMax], OfNoRowN = new int[OfMax];
            public double OfNoCat; public int OfNoCatN, OfPlaces;
        }
        private static LWin _win = new LWin();

        // liczniki doby (DailyLive) i linie
        private sealed class LDay
        {
            public int Towns, Castles, Villages, NFood, NFull, MLow, FacLow, NoTrade, VNoGoods, VNoCal, NoLordN, CNoCourt;
            public double HW, HR, HP, HS, People, B, Wg, Wf, Wp, G0, G0a, MSum, MMin = double.NaN, FfSum, FrSum, FfMin = double.NaN, FrMin = double.NaN; public int MN;
            public long TaxW, TaxR, TaxP, TaxC, Guild, Farm, Held, NoLord, Player;
            public long LabT, TurnT, TopT, LabC, Cut, LabS, Skup; public double BaseFolk, BaseVill, BaseLife, BaseCourt, BaseSale, BaseCar, BaseGm, LabF, LabG, LabA;
            public long Rent, Heal, Serv, GmWage, GmMat, SurpWage; public long OffT, OffC;
            public long KW, KR, KP, KG, KC, CCr; public double NW, NR, NP, AW, AR, AP, NFW, NFR, NFP, AFW, AFR, AFP, CNeed, CAff, Lokata;
            public double CInc, CWg, CHeads;
            public long VBuy, VPieces; public double VNeed, VWant;
            // Towary 2 / K2 (VillageClothAboveReserve): plotno w koszyku dnia wsi - wiersze zatrzymane progiem polki, budzet plotna niewydany przez prog (zostaje w kiesach wsi),
            // kupione sztuki plotna, suma progu i liczba wierszy plotna (srednia progu)
            public long VClHeld, VClKept, VClPcs, VClCap, VClRows;
            // E3-W1b (VillageClothHalfReserve): wiersze plotna z progiem polki obnizonym do polowy (plotno tanie w tym miescie); E3-G4: czynsz gruntowy do panow miast
            public long VClHalf, GroundRent;
            public long Seed, Orphan;
            // dziesiecina (E5/C3, TownFolk.Tithe.cs): wsie (nalezne / zaplacone), lud miast W/R/P, jalmuzna miast / wsi, place i material septow, bez kaplana, bez miasta targowego
            public long TiVDue, TiV, TiW, TiR, TiP, TiAlmsT, TiAlmsV, TiWage, TiMat, TiNoPriestT, TiNoPriestV, TiNoMarket; public int TiPriestN, TiNoPriestN, TiVN;
            public long TiVDsRest; public int TiVDsRestN;   // DS (F1): utarg okna wsi DS bez dziesieciny przy wozie (VillageTithe bierze ja po staremu)
            // P1 (obieg kiesy ludu): danina z oszczednosci do korony wedlug klasy (W, R, P, podzamcze) i kiesa ponad zapas dni przed / po daninie (klasy z nadwyzka)
            public readonly long[] Aid = new long[4]; public readonly double[] Ov0 = new double[4], Ov1 = new double[4];
        }
        private static LDay _ld;
        private static LWin _lw;
        private static int _liveDay = -1;                              // E5/C4: dzien biezacej doby kiesy czynnej (stempel ADay osad - okno People of this land)
        private static string _lineState, _lineSeed, _lineRet, _lineLab, _lineRights, _lineR14, _lineTithe, _lineC2;
        private static double _lastR = double.NaN; private static long _lastDStock, _lastIn, _lastOut;
        // zapasy startowe (linia jednorazowa)
        private static long _sW, _sR, _sP, _sC, _sV, _sGift; private static int _sTowns, _sCastles, _sVills, _sShort, _sNoBk;

        // ---- E3-W1 (VillageCartBringsGoods): zaleglosc koszyka wsi (potrzeba dnia niekupiona w miescie targowym, najwyzej W1Days dni potrzeby) - pamiec sesji;
        // zakup przez woz tej wsi w chwili sprzedazy plonu w innym miescie albo zamku (MarketCarts.SellPostfix -> CartBuys); liczniki od ostatniej linii doby
        private const double W1Days = 7.0;
        private static readonly Dictionary<Village, double> _w1Owed = new Dictionary<Village, double>();
        private static readonly LDay _w1d = new LDay();
        private static readonly HashSet<Village> _w1Vills = new HashSet<Village>();
        private static int _w1N, _w1Tries, _w1NoGoods, _w1NoGold, _w1NoOwed, _w1Stumbles;
        private static long _w1Gold, _w1Pcs;
        private static bool W1On { get { var s = Settings.Current; return s != null && s.VillageCartBringsGoods; } }
        private static void W1Zero() { _w1N = _w1Tries = _w1NoGoods = _w1NoGold = _w1NoOwed = _w1Stumbles = 0; _w1Gold = _w1Pcs = 0; _w1Vills.Clear(); }
        // poprawki noc-ekonomia3 (uwaga recenzji 1, NE3-16): wsie "bez towaru" w miescie targowym przy ostatnim koszyku dnia i te z nich, ktore potem kupily wozem (W1);
        // obrot zbiorow na poczatku Villages (raz na dobe) - linia doby podaje "wsi bez towaru po W1" z poprzedniego koszyka i wozow od niego
        private static readonly HashSet<Village> _w1NgCur = new HashSet<Village>(), _w1Served = new HashSet<Village>();
        private static int _w1NgLastN = -1, _w1ServedLastN; private static bool _w1NgRan;

        // ZC1 (CartPriceByDestination, MarketCarts): zloto, ktore wsie NAPRAWDE wydaly w miescie na kategorie koszyka dnia (sol, narzedzia, len, garnki, piwo) - koszyk
        // dnia w miescie targowym i zakupy wozem w miescie sprzedazy (E3-W1), wszystko przez BuyGoods; [Prev] - ostatnia pelna doba ludu. Tylko odczyt dla wozow.
        private static Dictionary<Settlement, Dictionary<ItemCategory, double>> _vwCur = new Dictionary<Settlement, Dictionary<ItemCategory, double>>(),
                                                                                 _vwPrev = new Dictionary<Settlement, Dictionary<ItemCategory, double>>();

        /// <summary>ZC1: ile zl wsie wydaly wczoraj w miescie mk na kategorie cat koszyka dnia (0 - poza koszykiem, brak zakupow, pierwsza doba sesji).</summary>
        internal static double VillageBought(Settlement mk, ItemCategory cat)
        {
            try
            {
                var p = _vwPrev; var cats = _villCats;
                Dictionary<ItemCategory, double> m; double x;
                if (mk == null || cat == null || p == null || cats == null || !cats.Contains(cat) || !p.TryGetValue(mk, out m) || m == null || !m.TryGetValue(cat, out x)) return 0;
                return x > 0 && !double.IsNaN(x) && !double.IsInfinity(x) ? x : 0;
            }
            catch { return 0; }
        }

        private static void NoteVillBought(Settlement mk, ItemCategory c, int got)
        {
            if (mk == null || c == null || got <= 0) return;
            Dictionary<ItemCategory, double> m;
            if (!_vwCur.TryGetValue(mk, out m)) { m = new Dictionary<ItemCategory, double>(); _vwCur[mk] = m; }
            double v; m.TryGetValue(c, out v); m[c] = v + got;
        }

        private static void ResetLive()
        {
            _vwCur = new Dictionary<Settlement, Dictionary<ItemCategory, double>>(); _vwPrev = new Dictionary<Settlement, Dictionary<ItemCategory, double>>();   // ZC1: zakupy wsi wedlug targu - sesja
            _w1Owed.Clear(); W1Zero(); _w1NgCur.Clear(); _w1Served.Clear(); _w1NgLastN = -1; _w1ServedLastN = 0; _w1NgRan = false;                                 // E3-W1: zaleglosci koszyka wsi i liczniki - sesja
            _seedT = _seedC = 0; _pending = null; _orphan = 0; _stateKey = null;
            _tickTown = null; _tickPaid = 0; _brFacFood = _brFacRest = 1; _brAnchor = false; _keys.Clear(); _zcCarry.Clear(); _zcLad.Clear();   // ZC4 (KS etap 2): ulamki koszyka wsi - sesja
            _better = null; _villCats = null; _betterText = _villText = "-"; _villBy = null;
            _ctlStock0 = long.MinValue; _ctlIn = _ctlOut = 0;
            _win = new LWin(); _ld = null; _lw = null; _liveDay = -1;
            _lineState = _lineSeed = _lineRet = _lineLab = _lineRights = _lineR14 = _lineTithe = _lineC2 = null;
            _lastR = double.NaN; _lastDStock = _lastIn = _lastOut = 0;
            _sW = _sR = _sP = _sC = _sV = _sGift = 0; _sTowns = _sCastles = _sVills = _sShort = _sNoBk = 0;
            ResetTithe();
            ResetP1();   // P1: danina z oszczednosci i pomiary obiegu kiesy (pamiec sesji)
            ResetHorses();   // K1-3a krok 2: liczniki linii koni roboczych (licznik osady jest w Folk - czysci go Reset)
            ResetLadder();   // P3-P5 (zbytki): kategorie listy dostatku, liczniki linii (podzial osady jest w Folk - czysci go Reset)
            ResetBread();    // M3: indeks doby, liczniki linii i komunikatu gracza (stan osady jest w Folk - pamiec sesji)
            ResetHoard();    // kiesa2: liczniki doby i linia startu sesji podatku od nadwyzki
            M96Reset();      // m96-koszyk: liczniki linii i linia startu sesji (okno placy zatrzymanej jest w Folk - czysci je Reset)
        }

        // ============================================================ wejscie do ludu (Earn)
        private static void SplitOf(int kind, out double w, out double r, out double p)
        {
            switch (kind)
            {
                case KLife: case KLifeCaravan: case KLifeBand: case KGarrison: w = 0.8; r = 0.1; p = 0.1; return;   // KL 1.3: uslugi
                case KCourt: w = 0.8; r = 0.2; p = 0; return;
                case KBuildLabour: w = 0.6; r = 0.4; p = 0; return;
                case KCraftPaid: case ETopUp: w = 0.7; r = 0.3; p = 0; return;
                case ELabour: w = LabW; r = 1 - LabW; p = 0; return;
                case ETurn: w = 0; r = 0; p = 1; return;
                case EValve: w = 0.2; r = 0.1; p = 0.7; return;                                                      // KL L6
                case EPublic: w = 0.5; r = 0.5; p = 0; return;
                default: w = 1; r = 0; p = 0; return;                                                                // kopalnie, inne - wyrobnicy
            }
        }

        /// <summary>Zapis zarobku do kies osady (czysta arytmetyka - nie rzuca): podzial klas wedlug rodzaju, praca niewolnych do wlascicieli (R40/P60),
        /// zarobek okna (podstawa podatkow). Zamek - cala kwota do podzamcza.</summary>
        private static void Credit(Folk f, long amt, int kind)
        {
            if (amt <= 0) return;
            f.LDayIn += amt;
            if (kind >= 0 && kind < EKinds) f.EarnK[kind] += amt;       // P1 (pomiar): sklad zarobku osady w oknie (tylko licznik)
            if (f.Kind == 1) { f.KW += amt; f.EarnWin[0] += amt; return; }
            double w, r, p;
            SplitOf(kind, out w, out r, out p);
            if (f.Slave && f.HS + f.HW > 0 && w > 0)
            {
                double y = w * f.HS / (f.HS + f.HW);
                w -= y; r += y * OwnR; p += y * OwnP;
                _win.SlaveW += (long)(amt * y);
            }
            long a1 = (long)(amt * r), a2 = (long)(amt * p), a0 = amt - a1 - a2;
            f.KW += a0; f.KR += a1; f.KP += a2;
            f.EarnWin[0] += a0; f.EarnWin[1] += a1; f.EarnWin[2] += a2;
        }

        /// <summary>
        /// Jedyne wejscie do ludu (R11): zarobek `amt` osady `st` rodzaju `kind` - zwraca kwote przyjeta (0 przy wylaczonym B1, wsi, poza watkiem albo
        /// wyjatku - wolajacy przelewa wtedy calosc platnikowi jak dotad). Sam zapis w kiesie (bez zlota z kasy) - wolajacy daje kasie `amt - wynik`.
        /// </summary>
        internal static int Earn(Settlement st, int amt, int kind)
        {
            try
            {
                if (amt <= 0 || st == null || !Live || OffMain()) return 0;
                if (st.IsCastle && !CastleLive) return 0;
                EnsureImported();
                var f = Get(st);
                if (f == null || f.Kind > 1) return 0;
                long t = Stopwatch.GetTimestamp();
                // f5-lud (KsFolkIncome, klucze Ks Folk Income All Kinds / R P): w miescie z KS czesci klas x kappa klas (W2 / R / P) - Earn zwraca przyjete, wolajacy daje
                // reszte kasie miasta - wszyscy, takze kiesa wsi (roboty gminy DS - DaPayVillage, przebudowa zajecia wioski - CraftsRebuildPay, oba przez PayVillageWage:
                // recenzja calosci fali 5 - jedna regula, wies placi pelna cene jak zolnierz i pan); bez reguly albo wszystkie czynniki 1 - Credit co do monety jak dotad
                long acc = amt;
                double k0, k1, k2;
                if (f.Kind == 0 && KsFolkIncome.Factors(st, out k0, out k1, out k2)) acc = CreditF5(f, amt, kind, k0, k1, k2);
                else Credit(f, amt, kind);
                if (f.Kind == 1) _win.EarnC += acc; else if (kind >= 0 && kind < EKinds) _win.Earn[kind] += acc;
                _ctlIn += acc;
                _patchTicks += Stopwatch.GetTimestamp() - t;
                return (int)Math.Max(0, Math.Min(amt, acc));
            }
            catch (Exception e) { Stumble("Earn", e); return 0; }
        }

        /// <summary>Hak 75/25 (R9, KL 1.3): z wydatku zolnierzy, ludzi karawan, band i zalog w osadzie `TownFolkServiceShare` do ludu (uslugi), reszta kupcom.</summary>
        internal static int EarnService(Settlement st, int amt, int kind)
        {
            try
            {
                if (amt <= 0 || !Live) return 0;
                int x = (int)(amt * (double)Unit(Settings.Current.TownFolkServiceShare));
                return x > 0 ? Earn(st, Math.Min(x, amt), kind) : 0;
            }
            catch (Exception e) { Stumble("EarnService", e); return 0; }
        }

        /// <summary>Kasa osady -> lud (przelew w calosci tutaj: ChangeGold, potem zapis). Zwraca to, co naprawde zeszlo z kasy.</summary>
        private static long FromCoffer(Folk f, long x, int kind)
        {
            if (x <= 0 || f == null || f.St == null || f.St.Town == null) return 0;
            var town = f.St.Town;
            int ask = (int)Math.Min(int.MaxValue, Math.Min(x, Math.Max(0, town.Gold)));
            if (ask <= 0) return 0;
            int had = town.Gold;
            town.ChangeGold(-ask);
            long got = had - town.Gold;
            if (got <= 0) return 0;
            Credit(f, got, kind);
            _ctlIn += got;
            return got;
        }

        /// <summary>WorkshopTrade.PayToTown: place warsztatu gracza zaplacone z jego kiesy (przelew gry do kasy miasta) - czesc ludu z kasy do kiesy (ksiega N9).</summary>
        internal static void EarnFromTown(Settlement st, int x, int kind)
        {
            try
            {
                if (x <= 0 || st == null || !Live || OffMain()) return;
                var f = Get(st);
                if (f == null || f.Kind > 1 || (f.Kind == 1 && !CastleLive)) return;
                // f5-lud: w miescie z KS z kasy schodzi tylko czesc przyjeta przez klasy (x kappa W2 / R / P) - reszta zostaje w kasie; bez reguly - FromCoffer jak dotad
                double k0, k1, k2;
                long got = f.Kind == 0 && KsFolkIncome.Factors(st, out k0, out k1, out k2) ? FromCofferF5(f, x, kind, k0, k1, k2) : FromCoffer(f, x, kind);
                if (got <= 0) return;
                _win.FromTown += got;
                if (kind >= 0 && kind < EKinds) _win.Earn[kind] += got;
                MoneyLedger.Note(MoneyLedger.NFolk, st, -(int)got, -1);   // recenzja B1 (1-4): src -1 - przelew przechodni (kiesa gracza -> kasa -> lud), nie zakup kasy; poza "nasze sciezki" (OnTownCoin pomija src < 0)
            }
            catch (Exception e) { Stumble("EarnFromTown", e); }
        }

        /// <summary>Zawor miasta (R4, KL L6): czesc ludu (TownFolkMerchantProfitShare) z kasy do kies - W20/R10/P70. Zwraca to, co zeszlo z kasy.</summary>
        internal static int EarnValve(Settlement st, int x)
        {
            try
            {
                if (x <= 0 || st == null || !st.IsTown || !Live || OffMain()) return 0;
                var f = Get(st);
                if (f == null) return 0;
                long got = FromCoffer(f, x, EValve);
                _win.ValveT += got; _win.Earn[EValve] += got;
                return (int)got;
            }
            catch (Exception e) { Stumble("EarnValve", e); return 0; }
        }

        /// <summary>Zawor zamku (R12, hak CastleDuesSuburbShare): czesc podzamcza z kasy zamku do kiesy podzamcza. Zwraca to, co zeszlo z kasy.</summary>
        internal static int EarnCastleValve(Settlement st, int x)
        {
            try
            {
                if (x <= 0 || st == null || !st.IsCastle || !CastleLive || OffMain()) return 0;
                var f = Get(st);
                if (f == null) return 0;
                long got = FromCoffer(f, x, EValve);
                _win.ValveC += got; _win.EarnC += got;
                return (int)got;
            }
            catch (Exception e) { Stumble("EarnCastleValve", e); return 0; }
        }

        /// <summary>A2 -> B1 (KL 1.2 "podzamcze: czesc wiktu z kuchni pana"): wplata dworu do siedziby-zamku (czesc kupcow) - kredyt na jedzenie podzamcza,
        /// bez ruchu zlota (zloto dworu jest juz w kasie zamku); najwyzej 7 dob przewidywanej konsumpcji zamku.</summary>
        internal static void CourtCredit(Settlement seat, int x)
        {
            try
            {
                var s = Settings.Current;
                if (x <= 0 || seat == null || !seat.IsCastle || !CastleLive || !s.TownFolkCastleCourtFeeds || OffMain()) return;
                var f = Get(seat);
                if (f == null) return;
                double cap = 7.0 * Math.Max(0, f.PredFood + f.PredRest);
                long nw = f.CourtCr + x;
                if (cap > 0 && nw > cap) nw = Math.Max(f.CourtCr, (long)cap);
                if (cap <= 0) nw = Math.Max(f.CourtCr, x);
                _win.CourtGrant += Math.Max(0, nw - f.CourtCr);
                f.CourtCr = nw;
            }
            catch (Exception e) { Stumble("CourtCredit", e); }
        }

        private static long PurseOf(Folk f, int c) { return c == 0 ? f.KW : c == 1 ? f.KR : c == 2 ? f.KP : f.KG; }
        private static void AddPurse(Folk f, int c, long x) { if (c == 0) f.KW += x; else if (c == 1) f.KR += x; else if (c == 2) f.KP += x; else f.KG += x; }

        /// <summary>Przelew wewnatrz ludu osady (kiesa -> kiesa, 3 = gmina): najwyzej stan kiesy platnika; odbiorca-klasa zapisuje zarobek okna.</summary>
        private static long Move(Folk f, int from, int to, long x)
        {
            long y = Math.Min(x, PurseOf(f, from));
            if (y <= 0) return 0;
            AddPurse(f, from, -y); AddPurse(f, to, y);
            if (to < 3) f.EarnWin[to] += y;
            return y;
        }

        /// <summary>E5/B2 (184, scalenie B - plan B1 pkt 6: korona dopisuje czlon w tej samej funkcji co podatki pana): podatek wojenny korony od zarobku okna
        /// klasy `c` (ta sama podstawa co podatek pana) - KingdomTreasury.FolkWarTax (wojna, stawka s, bramka oblezenia KFolkWarTax, przelew do skarbca krolestwa)
        /// najwyzej do stanu kiesy; tu zdjecie z kiesy tej klasy. Kiesa -> skarbiec (posiadacze H22/H23 -> H9), w bloku dobowym przed CrownIncome.Begin. Zwraca zaplacone.</summary>
        private static long PayCrown(Folk f, int c, int cls, long earned)
        {
            if (earned <= 0) return 0;
            DsSeason.NoteEarn(f.St, cls, earned);   // DS F3: zarobek wazony klasy - podstawa plonu nadania (prognoza; tylko licznik)
            int t = KingdomTreasury.FolkWarTax(f.St, cls, earned, Math.Max(0, PurseOf(f, c)));
            if (t <= 0) return 0;
            AddPurse(f, c, -t);
            _ctlOut += t; f.LDayOut += t;
            return t;
        }

        /// <summary>Kiesa `c` osady -> pan osady (podatki od zarobku, farma gminy): ChangeHeroGold w try (wyjatek - nic nie schodzi), RentToday (D "ziemia",
        /// powinnosci 165, budowy). Bez zywego pana - do gminy (odbiorca zapasowy). Oblezona osada - zostaje (BesiegedPay KFolkDues). Zwraca zaplacone panu.</summary>
        private static long PayLord(Folk f, int c, long x, LDay d)
        {
            x = Math.Min(x, PurseOf(f, c));
            if (x <= 0) return 0;
            var st = f.St;
            if (BesiegedPay.Holds(st, BesiegedPay.KFolkDues, x, true)) { d.Held += x; return 0; }
            var clan = st.OwnerClan;
            var lord = clan != null ? clan.Leader : null;
            if (lord == null || !lord.IsAlive)
            {
                if (c != 3 && f.Kind == 0) { Move(f, c, 3, x); d.NoLord += x; d.NoLordN++; }   // miasto: do gminy; zamek - zostaje w kiesie
                return 0;
            }
            int ix = (int)Math.Min(int.MaxValue, x);
            try { lord.ChangeHeroGold(ix); } catch (Exception e) { Stumble("PayLord", e); return 0; }
            AddPurse(f, c, -ix);
            _ctlOut += ix; f.LDayOut += ix;
            int r0; PopulationLaw.RentToday.TryGetValue(clan, out r0); PopulationLaw.RentToday[clan] = r0 + ix;
            CirculationWindows.NoteHeroGold(lord, ix);
            if (lord == Hero.MainHero) d.Player += ix;
            return ix;
        }

        // ============================================================ kotwica (nawias konsumpcji)
        /// <summary>
        /// DemandPostfix (po pomiarze G0/Idx/Sat0): slownik budzetu osady x czynnik grupy (jedzenie: m x Afford_jedzenie, reszta: m x Afford_reszta) - raz na
        /// (osade, kategorie, dobe); budzet nadwyzki ludu na kategorie "lepsze". Czynniki zapamietane dla AnchorBudgetPostfix (czlon dobrobytu wiersza BK).
        /// </summary>
        private static void AnchorDict(Folk f, Town town, Dictionary<ItemCategory, float> dict, double g0)
        {
            if (f == null || f.Kind > 1 || !f.Cal || !Live || (f.Kind == 1 && !CastleLive)) return;
            var s = Settings.Current;
            if (!f.FacOk)
            {
                // pierwsza doba po wczytaniu / nowa osada: przewidywanie z zapisu, a bez niego budzet tej doby (podzial jedzenie / reszta ze slownika)
                if (f.PredFood + f.PredRest > 0) Anchor(f, f.PredFood, f.PredRest);
                else
                {
                    double gf = 0;
                    foreach (var kv in dict) if (kv.Key != null && kv.Value > 0 && GroupOf(kv.Key) == 0) gf += kv.Value;
                    Anchor(f, gf, Math.Max(0, g0 - gf));
                }
            }
            double ff = s.TownFolkAnchorBasket ? Clamp01(f.FacFood) : 1.0, fr = s.TownFolkAnchorBasket ? Clamp01(f.FacRest) : 1.0;
            f.FacFoodUsed = ff; f.FacRestUsed = fr; f.OfferFood = f.OfferRest = 0;
            // ZC4 (KS etap 2): kategorie koszyka stanow - jedynym sufitem jest kiesa (Afford, bez sufitu "koszyk / wydatek"), z waga lambda; jedzenie koszyka przy braku
            // pieniedzy dzieli drabina zakupow (StartCalibration.Zc4Ladder: przetrwanie, sol, napoj, bialko, reszta); poza ZC4 - jak dotad
            double zl = 0; var zc = _zcCls;
            bool zon = f.Kind == 0 && s.TownFolkAnchorBasket && StartCalibration.Zc4ClassCost(f.St, zc, out zl) && zl > 0;
            if (!zon) zl = 0;
            double ffz = zon ? (1 - zl) * ff + zl * Clamp01(f.AffFood) : ff, frz = zon ? (1 - zl) * fr + zl * Clamp01(f.AffRest) : fr;
            if (zon) { f.FacFoodUsed = ffz; f.FacRestUsed = frz; }   // przewidywanie wydatku bez kotwicy (Predict) - czynnik, ktory naprawde cial koszyk (inaczej m malaloby co dobe)
            double after = g0;
            if (ff < 1 || fr < 1 || ffz < 1 || frz < 1)
            {
                _keys.Clear();
                foreach (var kv in dict) _keys.Add(kv.Key);
                after = 0;
                _zcLad.Clear();
                foreach (var k in _keys)
                {
                    float v;
                    if (k == null || !dict.TryGetValue(k, out v) || !(v > 0)) continue;
                    bool zk = zon && StartCalibration.Zc4OfferFactor(town, k) < 1.0;   // kategoria koszyka (zastepowana) w tym miescie
                    double fk = GroupOf(k) == 0 ? (zk ? ffz : ff) : (zk ? frz : fr);
                    if (zk && GroupOf(k) == 0 && ffz < 1 && StartCalibration.Zc4LadderCat(town, k)) { _zcLad.Add(k); continue; }
                    float nv = (float)(v * fk);
                    dict[k] = nv; after += nv;
                }
                if (_zcLad.Count > 0) { try { after += StartCalibration.Zc4Ladder(town, dict, _zcLad, ffz); } catch (Exception e) { Stumble("Zc4Ladder", e); foreach (var k in _zcLad) { float v; if (dict.TryGetValue(k, out v)) { dict[k] = (float)(v * ffz); after += v * ffz; } } } }
            }
            long off = f.Offer[0] + f.Offer[1] + f.Offer[2];
            if (off > 0 || LadderOn) after += AddOffer(f, town, dict, off);   // P3: przy liscie dostatku takze oferta 0 - podzial targu na jutro (sygnal P4, zakupy P5)
            f.G0a += after;
            _brFacFood = ff; _brFacRest = fr; _brAnchor = ff < 1 || fr < 1;
        }

        private static readonly double[] _zcCls = new double[3];          // ZC4: koszt koszyka klas W/R/P (StartCalibration.Zc4ClassCost)
        private static readonly List<ItemCategory> _zcLad = new List<ItemCategory>();   // ZC4: kategorie jedzenia koszyka do drabiny zakupow tego nawiasu

        private static double Clamp01(double v) { return double.IsNaN(v) ? 1.0 : (v < 0 ? 0 : (v > 1 ? 1 : v)); }

        private static List<ItemCategory> CatsOf(string[] ids, out string text)
        {
            var l = new List<ItemCategory>(); var found = new List<string>();
            try
            {
                foreach (var c in ItemCategories.All)
                    if (c != null && Array.IndexOf(ids, c.StringId) >= 0 && !l.Contains(c)) { l.Add(c); found.Add(c.StringId); }
            }
            catch (Exception e) { Stumble("CatsOf", e); }
            text = found.Count > 0 ? string.Join(", ", found.ToArray()) : "BRAK";
            return l;
        }

        /// <summary>Budzet nadwyzki ludu (oferta z wczorajszej doby) dopisany do kategorii "lepszych" obecnych w slowniku, proporcjonalnie do ich budzetu (w zlocie:
        /// / indeks^0.15 i / czesc domowa HistoricalPrices). Brak kategorii - pieniadz zostaje w kiesie. Zwraca dopisany popyt (do G0a).</summary>
        private static double AddOffer(Folk f, Town town, Dictionary<ItemCategory, float> dict, long off)
        {
            if (LadderOn) return LadderOffer(f, town, dict, off);       // P3 (zbytki, klucz FolkLuxuryList): lista dostatku z wagami grup (TownFolk.Luxury.cs)
            if (_better == null) _better = CatsOf(BetterIds, out _betterText);
            double sum = 0;
            foreach (var c in _better) { float v; if (dict.TryGetValue(c, out v) && v > 0) sum += v; }
            if (!(sum > 0)) { OfferNoCat(off); return 0; }             // P1 (pomiar): cala oferta bez kategorii w slowniku (pieniadz zostaje w kiesie)
            double added = 0;
            int bi = -1;
            bool cl = ClothOn;                                           // plotno 14.10 (poprawka 1c): plotno z oferty tylko ponad prog polki (TownFolk.Luxury.cs ClothRoom)
            foreach (var c in _better)
            {
                float v;
                bi++;
                if (!dict.TryGetValue(c, out v) || !(v > 0)) continue;
                double x = off * v / sum * StartCalibration.Zc4OfferFactor(town, c);   // ZC4: kategorie koszyka x (1 - lambda) - reszta zostaje w kiesie
                if (!(x > 0)) continue;
                if (cl && IsClothCat(c))
                {
                    double room = ClothRoom(f, town, c, v);
                    if (x > room) { if (f.Kind == 0) { _lxw.ClCut += x - room; _lxw.ClCutN++; } x = room; }
                    if (f.Kind == 0) _lxw.ClOff += x;
                    if (!(x >= 1)) continue;
                }
                double idx = Math.Max(0.01, town.GetItemCategoryPriceIndex(c));
                double hs = Math.Max(0.05, HistoricalPrices.HouseShare(c));
                double add = x / (Math.Pow(idx, OfferIdxPow) * hs);
                dict[c] = (float)(v + add); added += add;
                OfferNote(bi, c, x, add, v);                             // P1 (pomiar): oferta w zlocie na kategorie (tylko licznik)
                if (GroupOf(c) == 0) f.OfferFood += x; else f.OfferRest += x;
            }
            return added;
        }

        /// <summary>
        /// Zamienniki jedzenia (B2, FoodSubstitute - postfiks UpdateDemandShift PRZED DemandPostfix): gorna granica popytu, ktory AddOffer dopisze dzis do kategorii c
        /// tej osady (jednostki slownika budzetu dnia, jak `add` w AddOffer: zloto / (indeks^0.15 x czesc domowa)). Lista dostatku: oferta x waga calej grupy kategorii
        /// (udzial kategorii w grupie zalezy od slownika, wiec bierzemy cala grupe); stara regula: cala oferta, gdy c jest wsrod BetterIds. Te same warunki co AnchorDict;
        /// 0 = kategoria bez oferty. Tylko odczyt (osada spoza slownika ludu - 0, Folk nie jest zakladany). Wyjatek - nieskonczonosc (zamiennik pominiety) i potkniecie.
        /// </summary>
        internal static double OfferCapFor(Town town, ItemCategory c)
        {
            try
            {
                if (town == null || c == null || town.Settlement == null || !Live) return 0;
                Folk f;
                if (!_f.TryGetValue(town.Settlement, out f) || f == null || f.Kind > 1 || !f.Cal || (f.Kind == 1 && !CastleLive)) return 0;
                long off = f.Offer[0] + f.Offer[1] + f.Offer[2];
                if (off <= 0) return 0;
                double x = 0;
                if (LadderOn)
                {
                    EnsureLx();
                    int i;
                    if (_lxIdx != null && _lxIdx.TryGetValue(c, out i) && i >= 0 && i < _lxGroup.Length) x = off * LxWeight[_lxGroup[i]];
                }
                else
                {
                    if (_better == null) _better = CatsOf(BetterIds, out _betterText);
                    if (_better.Contains(c)) x = off;
                }
                if (!(x > 0)) return 0;
                double idx = Math.Max(0.01, town.GetItemCategoryPriceIndex(c));
                double hs = Math.Max(0.05, HistoricalPrices.HouseShare(c));
                return x / (Math.Pow(idx, OfferIdxPow) * hs);
            }
            catch (Exception e) { Stumble("OfferCapFor", e); return double.PositiveInfinity; }
        }

        /// <summary>
        /// Postfiks Priority.Low na BK EconomyPatches.CalculateBudget (po HistoricalPrices.BudgetPostfix, przed pomiarem E5/0): w nawiasie konsumpcji tej osady
        /// czlon dobrobytu wiersza (liczony od nowa przy kazdej pozycji polki) x czynnik grupy - jak slownik (K1: nie caly wynik, bez m^k). Wywolania spoza
        /// nawiasu (RawPrice.HouseBudget) - bez zmian. Wyjatek - wynik bez zmian.
        /// </summary>
        public static void AnchorBudgetPostfix(Town __0, ItemCategory __2, ref float __result)
        {
            if (__2 == null) return;
            bool br = _brAnchor && _brOpen && ReferenceEquals(__0, _brTown);
            // GL-2 (P2 b): czlon dobrobytu wiersza x m_t (zamek x skala podzamcza) - kazde wywolanie (nawias konsumpcji, prognoza targu zamku, RawPrice.HouseBudget),
            // bo to ten sam popyt ludzi osady; 1 = klucz MarketByPeople wylaczony albo brak skali - wtedy dokladnie dawna kotwica (tylko w dol, tylko w nawiasie)
            float sc = PeopleMarket.DemandScale(__0);
            if (!br && sc == 1f) return;
            long t0 = sc != 1f ? Stopwatch.GetTimestamp() : 0;
            try
            {
                double fac = br ? (GroupOf(__2) == 0 ? _brFacFood : _brFacRest) : 1.0;
                double k = sc * fac;                                       // NOWA galaz "w gore" (k > 1): tylko miasta i zamki z m x czynnik kotwicy > 1 (licznik linii GL2)
                if (sc == 1f ? fac >= 1 : k == 1.0) return;
                float part = HistoricalPrices.ProsperityPart(__0, __2);
                if (!(part > 0)) return;
                float v = __result + (float)((k - 1.0) * part);
                __result = v > 0f ? v : 0f;
                // ZC1 (SilkToWeavers / SilverOreToSmiths): galaz "w gore" nie podnosi budzetu ponad polke nad zapasem warsztatow miasta (ta sama granica co BudgetPostfix)
                if (k > 1.0) __result = GoodsPaths.FolkCap(__0, __2, __result);
            }
            catch (Exception e) { Stumble("AnchorBudgetPostfix", e); }
            finally { if (sc != 1f) PeopleMarket.NoteAnchorWork(Stopwatch.GetTimestamp() - t0); }
        }

        /// <summary>R13: postfiks na BK BKTaxModel.CalculateVillageTaxFromIncome(Village, int marketIncome) - przy kiesie czynnej podatek pana x VillageLordScale
        /// + VillageLordExtraShare x utarg (decyzja Jeffa 12.10: 0.5 x 70% + 10% = 45% zwykla, 35% niska, 55% wysoka), najwyzej caly utarg; majatki BK biora
        /// swoj udzial z tej czesci pana jak dotad (BK AccumulateTradeTax). 112 (VillageTakings) dopisuje wsi DOKLADNIE reszte (TaxOf czyta ten sam model).
        /// Wyjatek - wynik bez zmian.</summary>
        public static void VillageTaxPostfix(int __1, ref int __result)
        {
            try
            {
                if (__result <= 0 || !Live) return;
                var s = Settings.Current;
                long inc = Math.Max(0, __1);
                long add = (long)(inc * (double)Unit(s.VillageLordExtraShare));
                long v = (long)(__result * (double)Unit(s.VillageLordScale)) + add;
                if (v > inc) v = inc;
                if (!OffMain()) { _win.VTaxBase += __result; _win.VTaxCut += __result - v; _win.VTaxAdd += add; }
                __result = (int)v;
            }
            catch (Exception e) { Stumble("VillageTaxPostfix", e); }
        }

        // ============================================================ zaplata za towar (R1) - z CastlePurse.ConsumePostfix (First)
        /// <summary>
        /// Zamiast cofniecia "zakupow" A1: `made` = przyrost kasy w konsumpcji (BK ChangeGold(num6) - jedyny zapis zlota w nawiasie). Miasto: klasy placa swoja czesc
        /// (udzialy z kotwicy: towar x Afford + oferta) najwyzej z kiesy, kasa zatrzymuje zaplate bez akcyzy, gmina akcyze; dokup jedzenia BK przy zapasie <= 10%
        /// niezaplacony przez lud - z gminy. Zamek: podzamcze placi, reszte pokrywa kredyt dworu (bez zlota). Reszta przyrostu wraca do nicosci jak w A1 - JEDNO
        /// ChangeGold. Zwraca, ile kasa zatrzymala (>= 0), albo -1 (nie obsluzone - wolajacy cofa calosc jak A1).
        /// </summary>
        internal static int PayForGoods(Town town, int made)
        {
            bool applied = false; long keep = 0;
            try
            {
                if (town == null || made <= 0 || !Live || OffMain()) return -1;
                if (town.IsCastle && !CastleLive) return -1;
                EnsureImported();
                var f = Get(town.Settlement);
                if (f == null || f.Kind > 1) return -1;
                var s = Settings.Current;
                long p0 = 0, p1 = 0, p2 = 0, fromG = 0, excise = 0, court = 0, unpaid;
                if (f.Kind == 0)
                {
                    double s0 = f.Share[0], s1 = f.Share[1], s2 = f.Share[2], ss = s0 + s1 + s2;
                    if (!(ss > 0)) { s0 = f.KW; s1 = f.KR; s2 = f.KP; ss = s0 + s1 + s2; }   // bez kotwicy (przed kalibracja) - wedlug kies
                    if (ss > 0)
                    {
                        long w0 = (long)(made * s0 / ss), w1 = (long)(made * s1 / ss), w2 = made - w0 - w1;
                        p0 = Math.Min(w0, Math.Max(0, f.KW)); p1 = Math.Min(w1, Math.Max(0, f.KR)); p2 = Math.Min(w2, Math.Max(0, f.KP));
                    }
                    long paid = p0 + p1 + p2;
                    excise = (long)Math.Round(paid * (double)Math.Max(0f, Math.Min(0.2f, s.TownFolkExcise)));
                    long rest = made - paid;
                    // dokup jedzenia BK (zapas <= 10%) niezaplacony przez lud - z gminy (plan 4.2, R1); recenzja B1 (1-5): tylko czesc jedzenia niezaplaconej reszty
                    // (reszta rozlozona na towar wedlug zlota grup w nawiasie) - sukno, zbytki i towar ogolny bez zaplaty zostaja "zjedzone bez zaplaty"
                    if (rest > 0 && _brLowFood && ReferenceEquals(_brTown, town))
                    {
                        long fg = Math.Min((long)made, BracketFoodGold(town));
                        long restFood = fg > 0 ? (long)(rest * (double)fg / made) : 0;
                        fromG = Math.Min(restFood, Math.Max(0, f.KG + excise));
                    }
                    keep = paid - excise + fromG;
                    unpaid = made - paid - fromG;
                }
                else
                {
                    p0 = Math.Min((long)made, Math.Max(0, f.KW));
                    if (s.TownFolkCastleCourtFeeds) court = Math.Min(made - p0, Math.Max(0, f.CourtCr));
                    keep = p0;
                    unpaid = made - p0 - court;
                }
                if (made - keep > 0) town.ChangeGold(-(int)(made - keep));   // jedyny zapis zlota (reszta przyrostu wraca do nicosci jak w A1)
                applied = true;
                f.KW -= p0; f.KR -= p1; f.KP -= p2; f.KG += excise - fromG; f.CourtCr -= court;
                f.PaidG[0] += p0; f.PaidG[1] += p1; f.PaidG[2] += p2; f.PaidKeep += keep;
                _tickTown = town; _tickPaid = (int)keep;
                _ctlOut += keep;
                if (f.Kind == 0) { _win.PaidW += p0; _win.PaidR += p1; _win.PaidP += p2; _win.UnpaidT += unpaid; _win.GmFood += fromG; _win.Excise += excise; _win.MadeT += made; _win.Ticks++; }
                else { _win.PaidC += p0; _win.Court += court; _win.UnpaidC += unpaid; _win.MadeC += made; _win.TicksC++; }
                f.LDayPaid += p0 + p1 + p2; f.LDayUnpaid += unpaid; f.LDayCourt += court; f.LDayOut += p0 + p1 + p2;
                try { R14(f); } catch (Exception e) { Stumble("R14", e); }
                return (int)keep;
            }
            catch (Exception e) { Stumble("PayForGoods", e); return applied ? (int)keep : -1; }
        }

        /// <summary>MoneyLedger.ConsumePostfix (po CastlePurse First): ile z przyrostu kasy w tym ticku to zaplata ludu (przelew z kiesy, nie zrodlo) - zeruje.</summary>
        internal static int TakeTickPaid(Town town)
        {
            if (town == null || !ReferenceEquals(town, _tickTown)) return 0;
            int x = _tickPaid; _tickTown = null; _tickPaid = 0;
            return x;
        }

        /// <summary>A1 cofnelo caly przyrost (B1 nie obsluzylo ticku: przed kalibracja, potkniecie) - licznik linii.</summary>
        internal static void NoteFallback() { _win.Fallback++; }

        /// <summary>
        /// R14 (K2): zadowolenie BK grupy z Afford < 0.98 = migawka sprzed konsumpcji + min(0, delta BK) - 0.0015 (obciete 0..1); Afford >= 0.98 - wynik BK.
        /// Jedzenie -> Food, reszta -> Industrial / General / Luxury (BK ConsumptionType: Luxury 0, Industrial 1, General 2, Food 3). Bez latki na UpdateSatisfaction.
        /// E5/C2 (TownFolkHungerHurts): Food tylko przy glodzie z braku pieniedzy i jedzeniu na polce (C2Food); wylaczone - jak B1 (Afford &lt; 0.98).
        /// </summary>
        private static void R14(Folk f)
        {
            var s = Settings.Current;
            if (!s.TownFolkSatisfactionFromBasket || !f.Sat0Ok || !f.FacOk) return;
            bool food = f.AffFood < 0.98, rest = f.AffRest < 0.98;
            if (food && s.TownFolkHungerHurts) food = C2Food(f);      // E5/C2: galaz jedzenia tylko przy glodzie z braku pieniedzy (wylaczone - jak B1)
            if (!food && !rest) return;
            var sat = Sats(f);
            if (sat == null || sat.Length < 4) return;
            for (int i = 0; i < 4; i++)
            {
                bool g = i == 3 ? food : rest;
                if (!g) continue;
                double dl = sat[i] - f.Sat0[i];
                if (dl > 0) _win.R14Bk += dl;
                double v = f.Sat0[i] + Math.Min(0.0, dl) - R14Step;
                sat[i] = (float)(v < 0 ? 0 : (v > 1 ? 1 : v));
                if (i == 3) _win.R14Food++; else _win.R14Rest++;
            }
            if (food && s.TownFolkHungerHurts) { _win.C2Hit++; f.C2Hit = true; }
        }

        // ============================================================ E5/C2 (203, PLAN 5.9, D2) - glod ma skutki: galaz jedzenia R14
        /// <summary>
        /// Galaz jedzenia R14 przy TownFolkHungerHurts [D 09.10 A]: tylko glod z braku pieniedzy (Hunger.Of = NoMoney: Afford jedzenia &lt; 0.95 przez 7 dob z rzedu,
        /// kampania &gt;= 120 dob) i tylko gdy jedzenie jest na polce (nawias tej osady przeszedl wiersz jedzenia). Brak jedzenia (Food 0 warowni, glod okregu E4/6)
        /// karza juz BK "Food Shortage", RBL "Severe Starvation" i E4/6 - bez drugiej kary. Liczniki okna do linii "Glod ludu (E5/C2)".
        /// </summary>
        private static bool C2Food(Folk f)
        {
            var c = Hunger.Of(f.St);
            if (c == HungerCause.NoFood) { _win.C2NoFood++; return false; }
            if (c != HungerCause.NoMoney) { _win.C2Wait++; return false; }
            if (!BracketHadFood(f.St.Town)) { _win.C2NoShelf++; return false; }
            return true;
        }

        /// <summary>E5/C2 "jedzenie na polce": w otwartym nawiasie konsumpcji tej osady BK przeszedl choc jeden wiersz jedzenia (CalculateBudget idzie tylko po towarze
        /// z polki, ktorego kategoria jest w popycie - BK EconomyPatches ItemConsumptionPatch.Prefix). Nawias nie tej osady - false (bez kary).</summary>
        private static bool BracketHadFood(Town town)
        {
            if (!_brOpen || town == null || !ReferenceEquals(town, _brTown)) return false;
            foreach (var kv in _brTrack) if (kv.Key != null && kv.Value != null && kv.Value.Rows > 0 && GroupOf(kv.Key) == 0) return true;
            return false;
        }

        /// <summary>E5/C2 (Hunger.Of): glod z braku pieniedzy - kiesa czynna, miasto albo zamek z kotwica, Afford jedzenia na jutro &lt; 0.95 przez 7 dob z rzedu
        /// (licznik HungerDays z doby), kampania &gt;= 120 dob. Watek glowny (slownik osad); poza nim - false.</summary>
        internal static bool MoneyHungry(Settlement st)
        {
            if (st == null || !Live || OffMain()) return false;
            if (st.IsCastle && !CastleLive) return false;
            Folk f;
            if (!_f.TryGetValue(st, out f) || f == null || f.Kind > 1 || !f.Cal || !f.FacOk) return false;
            return f.HungerDays >= Hunger.MoneyDays && f.AffFood < Hunger.MoneyAfford && Hunger.CampaignAge() >= Hunger.FromDay;
        }

        /// <summary>E5/C2: potkniecie z Hunger (licznik TownFolk, bez wylacznikow).</summary>
        internal static void StumbleOut(string where, Exception e) { Stumble(where, e); }

        /// <summary>E5/C2: licznik dob glodu z braku pieniedzy - po kotwicy doby (Afford na jutro).</summary>
        private static void NoteHungerDay(Folk f)
        {
            f.HungerDays = f.FacOk && f.AffFood < Hunger.MoneyAfford ? Math.Min(f.HungerDays + 1, 100000) : 0;
        }

        /// <summary>E5/C2: linia doby "Glod ludu (E5/C2)" - przyczyny glodu osad (Hunger.Of) po kotwicy doby, korekty okna i kontrola "obie kary tej samej doby"
        /// (korekta jedzenia C2 w oknie, a warownia z Food 0 w chwili doby - warunek BK "Food Shortage" / RBL "Severe Starvation").</summary>
        private static void HungerLine(int day, LWin w)
        {
            var s = Settings.Current;
            int money = 0, moneyT = 0, moneyC = 0, streak = 0, starving = 0, district = 0, both = 0, osad = 0;
            foreach (var f in _f.Values)
            {
                if (f.Kind > 1 || f.St == null) continue;
                try
                {
                    if (f.Kind == 1 && !CastleLive) { f.C2Hit = false; continue; }
                    osad++;
                    bool st0 = Hunger.Starving(f.St);
                    if (f.C2Hit && st0) both++;
                    f.C2Hit = false;
                    if (f.HungerDays > 0) streak++;
                    var c = Hunger.Of(f.St);
                    if (c == HungerCause.NoFood) { if (st0) starving++; else district++; }
                    else if (c == HungerCause.NoMoney) { money++; if (f.Kind == 0) moneyT++; else moneyC++; }
                }
                catch (Exception e) { f.C2Hit = false; Stumble("HungerLine", e); }
            }
            double age = Hunger.CampaignAge();
            _lineC2 = "Glod ludu (E5/C2): dzien " + day + " | TownFolkHungerHurts " + (s.TownFolkHungerHurts ? "tak" : "nie") + ", kampania " + ((int)age).ToString(Inv)
                      + " dob (kara od " + ((int)Hunger.FromDay).ToString(Inv) + ": " + (age >= Hunger.FromDay ? "tak" : "nie") + "), osad " + osad
                      + " | brak pieniedzy (Afford jedzenia < " + Hunger.MoneyAfford.ToString("0.00", Inv) + " przez " + Hunger.MoneyDays + " dob): " + money + " (miasta " + moneyT + " / zamki " + moneyC
                      + "), z licznikiem >= 1 doby " + streak + " | brak jedzenia (bez kary zadowolenia - BK/RBL/E4/6): Food 0 warowni " + starving + ", glod okregu ponad prog " + district
                      + " | korekty jedzenia R14 (C2) " + w.C2Hit + "; zdjete: brak jedzenia " + w.C2NoFood + ", poza glodem z braku pieniedzy (Afford >= 0.95, < 7 dob albo < 120 dob) " + w.C2Wait + ", bez jedzenia na polce " + w.C2NoShelf
                      + " | obie kary tej samej doby " + both + ".";
        }

        // ============================================================ doba (TownFolk.Daily przy kiesie czynnej)
        private static void DailyLive()
        {
            int day = (int)CampaignTime.Now.ToDays - 1;
            _liveDay = day;                                            // E5/C4: stempel doby liczb osad (ADay)
            EnsureImported();
            bool first = _sessionDay < 0;
            if (first) _sessionDay = day;
            StateLine(true);
            if (!_calDone)
            {
                if (!PopulationLaw.Calibrated)
                {
                    _lineFolk = "Kiesa ludu: dzien " + day + " | CZYNNA - czeka na kalibracje ksiegi ludzi (PopulationLaw) - przeplywy okna zostaja w kiesach, kotwica od kalibracji";
                    CloseWin(); ClearWindowsLive(); return;
                }
                Calibrate(day);
            }
            var d = new LDay();
            // stary zapis albo wlaczenie w trakcie: zapasy z nadwyzki kas (najwyzej polowa) - przelew i migawka M20 przed robota doby (M18)
            try { SeedOld(d); } catch (Exception e) { Stumble("SeedOld", e); }
            if (_orphan > 0) { try { OrphanBack(d); } catch (Exception e) { Stumble("OrphanBack", e); } }
            var lw = CloseWin();
            // dziesiecina (E5/C3): kaplani BK raz na dobe, potem wsie (utarg okna) - przed miastami: place i material septow wsi licza sie w miastach targowych tej doby
            try { TitheBegin(); } catch (Exception e) { Stumble("TitheBegin", e); }
            try { DsCartTitheZc(); } catch (Exception e) { Stumble("DsCartTitheZc", e); }   // DS (F1): dziesiecina wsi DS z wozow od ostatniej doby - do ZC0 (tylko licznik)
            try { VillageTithe(d); } catch (Exception e) { Stumble("VillageTithe", e); }
            AidBegin();                                                // P1: liczniki doby daniny z oszczednosci (czas poboru, nadwyzka po daninie wedlug krolestw)
            HoardBegin();                                              // kiesa2: liczniki doby podatku od nadwyzki (zeruje tez Last* dla linii Obieg)
            GuardBegin();                                              // obieg2: straznik obiegu - lud wobec poziomu normalnego, mnoznik i stawka doby; liczniki wsi (TownFolk.Guard.cs)
            try { LoansDay(day); } catch (Exception e) { Stumble("LoansDay", e); }   // obieg2: pozyczki od ludu - odsetki, raty, niesplata (TownFolk.Loans.cs), przed miastami
            LadderBegin();                                             // P3-P5: liczniki doby zakupow podzamczy i wsi z listy dostatku, oferta kupujacych z zewnatrz (P4) od zera
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var f in _f.Values)
                {
                    if (f.Kind != pass) continue;
                    try { if (pass == 0) TownLive(f, d); else CastleLiveDay(f, d); }
                    catch (Exception e) { Stumble("DailyLive(rodzaj " + f.Kind + ")", e); }
                }
            }
            try { DsVillage.DecreeMemo(StartCalibration.SwCostFast); Villages(d); } catch (Exception e) { Stumble("Villages", e); } finally { DsVillage.DecreeMemo(false); }   // ks-koszt: dekret pana raz na wies w petli
            try { KsWageHands.DayLine(day); } catch (Exception e) { Stumble("KsWageHands.DayLine", e); }   // r4-koszyk: linia "Place za brak rak" (po miastach)
            try { KsFolkWage.DayLine(day); } catch (Exception e) { Stumble("KsFolkWage.DayLine", e); }     // f4-place-ludu: linia "Place W wedlug koszyka" (bez kluczy i bez KS - nic)
            try { KsFolkIncome.DayLine(day); } catch (Exception e) { Stumble("KsFolkIncome.DayLine", e); } // f5-lud: linia "Dochod ludu wedlug koszyka" (bez kluczy i bez KS - nic)
            M96DayLine(day);                                                                                // m96-koszyk: linia "Placa dla klas w biedzie" (nie rzuca)
            try { LadderDay(day, d); } catch (Exception e) { Stumble("LadderDay", e); }   // P3-P5: linia "Zbytki ludu (P3-P5)"
            try { HungerLine(day, lw); } catch (Exception e) { Stumble("HungerLine", e); }   // E5/C2: po kotwicach doby (licznik glodu)
            try { HoardDay(day); } catch (Exception e) { HoardStumble("HoardDay", e); }   // kiesa2: linia doby i liczby dla "Obieg" (po miastach i zamkach)
            try { GuardDay(day); } catch (Exception e) { Stumble("GuardDay", e); }   // obieg2: linie straznika i wsi, liczby "Obieg", kiesy ludu na koniec doby (doplyw jutro)
            // kontrola zamknieta (kiesy miast, podzamczy, gminy): zmiana stanu = wplywy - wydatki
            long stock = TotalTown() + TotalCastle() + TotalGmina();
            if (_ctlStock0 != long.MinValue) { _lastDStock = stock - _ctlStock0; _lastIn = _ctlIn; _lastOut = _ctlOut; _lastR = _lastDStock - (_ctlIn - _ctlOut); }
            else _lastR = double.NaN;
            _ctlStock0 = stock; _ctlIn = _ctlOut = 0;
            _ld = d; _lw = lw;
            _aidDay = day;                                             // P1: liczby daniny tej doby gotowe (linia korony "Danina z oszczednosci (P1)" czyta je w tym samym takcie)
            bool csvDay = !first && ((day - CampaignStart(day) + 1) % 7 == 0 || !_csvFirst);
            if (csvDay) _csvFirst = true;
            try { MeasureP1Day(day, d, lw, first, csvDay); } catch (Exception e) { Stumble("MeasureP1Day", e); }   // P1: pomiary obiegu kiesy (sam log; tygodniowe razem z CSV) - przed BuildLines (zeruje liczby doby osad)
            try { BuildLines(day, d, lw, first, csvDay); } catch (Exception e) { Stumble("BuildLines", e); }
            ClearWindowsLive();
        }

        private static LWin CloseWin() { var w = _win; _win = new LWin(); return w; }

        private static void ClearWindowsLive()
        {
            foreach (var f in _f.Values)
            {
                ClearWindow(f);
                Array.Clear(f.PaidG, 0, 3); f.PaidKeep = 0; f.G0a = 0; f.OfferFood = f.OfferRest = 0;
            }
        }

        /// <summary>Przewidywany wydatek gry na jutro bez kotwicy: wydatek nawiasu tej doby (bez oferty nadwyzki) / czynnik uzyty, najwyzej budzet gry dnia (G0).</summary>
        private static void Predict(Folk f, out double wf, out double wr)
        {
            wf = f.PredFood; wr = f.PredRest;
            if (!(f.G0 > 0)) return;                                   // nawias nie biegl w tym oknie - przewidywanie z wczoraj
            double ofF, ofR; OfferSpent(f, out ofF, out ofR);         // P3 (poprawka Predict, A 9.9): przy liscie dostatku oferta KUPIONA, inaczej oferowana (jak dotad)
            double sf = Math.Max(0, f.SpendFood - ofF), sr = Math.Max(0, (f.SpendGame - f.SpendFood) - ofR);
            wf = sf / Math.Max(0.05, f.FacFoodUsed); wr = sr / Math.Max(0.05, f.FacRestUsed);
            double cap = f.G0;
            if (wf + wr > cap && wf + wr > 0) { double k = cap / (wf + wr); wf *= k; wr *= k; }
        }

        /// <summary>Kotwica i Afford (KL 5.2, S4) na jutro: miasto - koszyk klas D1 x indeks, m = min(1, koszyk / wydatek), Afford wedlug kies wobec PELNEJ potrzeby;
        /// zamek - koszyk podzamcza, kiesa + kredyt dworu.</summary>
        private static void Anchor(Folk f, double wFood, double wRest)
        {
            var s = Settings.Current;
            double idx = f.Idx > 0 && !double.IsNaN(f.Idx) ? f.Idx : 1.0;
            f.PredFood = wFood; f.PredRest = wRest;
            double W = wFood + wRest;
            f.FacOk = true;
            if (f.Kind == 1)
            {
                double B = CastleHeads(f) * BC * idx;                  // GL-3 (P3): podzamcze wedlug ludzi wsi zamku (klucz wylaczony - f.HW)
                double L = Math.Max(0, f.KW) + (s.TownFolkCastleCourtFeeds ? Math.Max(0, f.CourtCr) : 0);
                double need = W > 0 ? Math.Min(W, B) : B, m = W > 0 && B > 0 ? Math.Min(1.0, B / W) : 1.0, F = need * FdC;
                double af = F > 0 ? Math.Min(1.0, L / F) : 1.0, ar = need - F > 0 ? Math.Min(1.0, Math.Max(0, L - F) / (need - F)) : 1.0;
                f.Need[0] = need; f.Need[1] = f.Need[2] = 0;
                f.AffFood = af; f.AffRest = ar; f.MAnc = m; f.AffC[0] = need > 0 ? Math.Min(1.0, L / need) : 1.0; f.AffFC[0] = af;
                f.FacFood = B > 0 ? m * af : 1.0; f.FacRest = B > 0 ? m * ar : 1.0;
                f.Share[0] = 1; f.Share[1] = f.Share[2] = 0;
                return;
            }
            double r = Scale(f);
            // sw-uchodzcy (RaidRefugeesToTowns, TownFolk.Refugees.cs): uchodzcy z najazdu mieszkajacy w miescie to biedota - ich glowy w koszyku klasy W (kiesa W placi, jutro
            // kupuja z targu jak biedota); nikt w miescie albo klucz wylaczony - 0 (kotwica co do monety jak dotad). Przy ZC4 koszt klasy W ze stanow juz ich ma (KsSnapshot)
            double bW = (f.HW * r + RefugeeHeadsOf(f, r)) * BW * idx, bR = f.HR * r * BR * idx, bP = f.HP * r * BP * idx;
            if (f.Slave) { double sl = f.HS * r * BS * idx; bR += sl * OwnR; bP += sl * OwnP; }
            // ZC4 (KS etap 2): koszyki klas BW/BR/BP/BS zastapione kosztem koszyka stanow po cenach targu (W: wyrobnicy i chlopi miasta, R: rzemieslnicy + 40% niewolnych,
            // P: kupcy i szlachta + 60% niewolnych) z waga lambda; poza ZC4 - jak dotad
            double zl; var zc = _zcCls;
            if (StartCalibration.Zc4ClassCost(f.St, zc, out zl) && zl > 0)
            {
                bW = (1 - zl) * bW + zl * zc[0]; bR = (1 - zl) * bR + zl * zc[1]; bP = (1 - zl) * bP + zl * zc[2];
            }
            double Bt = bW + bR + bP;
            if (!(Bt > 0))
            {
                // bez ludzi BK (i bez mediany) - bez kotwicy; zaplata wedlug kies
                f.FacFood = f.FacRest = f.AffFood = f.AffRest = f.MAnc = 1;
                for (int c = 0; c < 3; c++) { f.Need[c] = 0; f.Share[c] = 0; f.AffC[c] = f.AffFC[c] = 1; }
                return;
            }
            double G = W > 0 ? Math.Min(W, Bt) : Bt, mm = W > 0 ? Math.Min(1.0, Bt / W) : 1.0;
            double sumN = 0, af2 = 0, ar2 = 0;
            for (int c = 0; c < 3; c++)
            {
                double b = c == 0 ? bW : c == 1 ? bR : bP, keep = c == 0 ? KeepW : c == 1 ? KeepR : KeepP;
                f.Need[c] = b / Bt * G / keep; sumN += f.Need[c];
            }
            for (int c = 0; c < 3; c++)
            {
                double fd = c == 0 ? FdW : c == 1 ? FdR : FdP, keep = c == 0 ? KeepW : c == 1 ? KeepR : KeepP;
                double N = f.Need[c], F = N * fd, L = Math.Max(0, PurseOf(f, c));
                double ffc = F > 0 ? Math.Min(1.0, L / F) : 1.0, frc = N - F > 0 ? Math.Min(1.0, Math.Max(0, L - F) / (N - F)) : 1.0;
                double sc = sumN > 0 ? N / sumN : 0;
                af2 += sc * ffc; ar2 += sc * frc;
                f.AffFC[c] = ffc; f.AffC[c] = N > 0 ? Math.Min(1.0, L / N) : 1.0;
                f.Share[c] = N * keep * f.AffC[c] + f.Offer[c] * OfferPayPart(f);   // towar klasy, za ktory jutro zaplaci, + oferta nadwyzki (P3: x czesc kupiona ostatnio)
            }
            if (!(sumN > 0)) { af2 = ar2 = 1; }
            f.AffFood = af2; f.AffRest = ar2; f.MAnc = mm;
            f.FacFood = mm * af2; f.FacRest = mm * ar2;
        }

        private static void TownLive(Folk f, LDay d)
        {
            var s = Settings.Current;
            var st = f.St; var town = st.Town;
            if (town == null) return;
            d.Towns++;
            // ---- 1. podatki od zarobku okna: pan 1/2/3% (W/R/P), cech 1% R -> gmina (raz na dobe); E5/B2 korona 184 na wierzch (scalenie B)
            long eW = f.EarnWin[0], eR = f.EarnWin[1], eP = f.EarnWin[2];
            f.ZcE[0] = eW; f.ZcE[1] = eR; f.ZcE[2] = eP; f.ZcDay = _liveDay;   // ZC0 (DailyLife): zarobek okna klas tej doby - tylko odczyt pomiaru Dostatku
            long tW = (long)Math.Round(eW * TaxW), tR = (long)Math.Round(eR * TaxR), tP = (long)Math.Round(eP * TaxP);
            long guild = (long)Math.Round(eR * GuildR);
            Array.Clear(f.EarnWin, 0, 3);
            d.TaxW += PayLord(f, 0, tW, d); d.TaxR += PayLord(f, 1, tR, d); d.TaxP += PayLord(f, 2, tP, d);
            d.Guild += Move(f, 1, 3, guild);
            PayCrown(f, 0, KingdomTreasury.WtW, eW); PayCrown(f, 1, KingdomTreasury.WtR, eR); PayCrown(f, 2, KingdomTreasury.WtP, eP);
            // ---- 1b. dziesiecina (E5/C3, R21; decyzja Jeffa 12.10) od tego samego zarobku okna: W 1% / R 2% / P 3% -> kaplan osady (material septow - sprzedaz kupcow przed krokiem 2)
            TownTithe(f, town, eW, eR, eP, d);
            // ---- 2. z kasy do ludu (koszt sprzedazy - najwyzej stan kasy): robocizna handlu, obrot P, doplata do 35% wartosci wyrobow
            double svc = Unit(s.TownFolkServiceShare), cs = Unit(s.CourtFolkShare);
            double rf = Unit(s.TownFolkLabourFood), rg = Unit(s.TownFolkLabourGoods), ra = Unit(s.TownFolkLabourArms);
            double life = f.Win[KLife] + f.Win[KLifeCaravan] + f.Win[KLifeBand] + f.Win[KGarrison], court = f.Win[KCourt];
            double lf = (1 - svc) * life, lg = 0, la = 0;                  // podstawa: jedzenie / towar / bron
            lf += (1 - cs) * court * 0.5; lg += (1 - cs) * court * 0.2; la += (1 - cs) * court * 0.3;
            double sale = 0;
            for (int mi = 0; mi < Mixes; mi++) { lf += f.Sale[mi] * MixOf[mi][0]; lg += f.Sale[mi] * MixOf[mi][1]; la += f.Sale[mi] * MixOf[mi][2]; sale += f.Sale[mi]; }
            double fp = f.PaidKeep, gF = 1, gG = 0, gA = 0;
            if (f.SpendGame > 0) { gF = f.Grp[0] / f.SpendGame; gG = f.Grp[1] / f.SpendGame; gA = f.Grp[2] / f.SpendGame; }
            lf += fp * gF; lg += fp * gG; la += fp * gA;
            lf += f.VillWin * 0.2; lg += f.VillWin * 0.8;
            lg += f.GmMatWin;
            double car = f.CarBuy + f.CarSell;
            double lab = lf * rf + lg * rg + la * ra + CaravanLab * car;
            // GL-B / 11c (klucz FolkEarnFromVillageBuys): robocizna od SKUPU plonu wsi - zaplata kasy taborom wsi w oknie x TownFolkLabourVillageBuys (wyladunek, miara,
            // przechowanie, przemial); koszt skupu - ta sama droga i to samo przyciecie do stanu kasy co robocizna od sprzedazy. Klucz wylaczony: okno 0, lab bez zmian
            double skupLab = 0;
            if (f.SkupWin > 0) { skupLab = f.SkupWin * (double)Unit(s.TownFolkLabourVillageBuys); d.Skup += f.SkupWin; lab += skupLab; }
            f.SkupWin = 0;
            double turnBase = (1 - svc) * life + (1 - cs) * court + sale + fp + f.VillWin + f.GmMatWin + f.CarBuy;
            double turn = Math.Max(0f, Math.Min(0.2f, s.TownFolkMerchantDraw)) * turnBase;
            double top = Math.Max(0, Unit(s.TownFolkCraftWageShare) * f.ProdV - f.Win[KCraftPaid]);
            // r4-koszyk (klucz Ks Wage By Hands): tam, gdzie pracodawcom brakuje rak, kasa miasta placi wiecej za te sama prace (robocizna i doplata do wyrobow x wskaznik
            // plac za brak rak, KsWageHands) - zloto z kasy do kies W i R, najwyzej stan kasy (to samo przyciecie nizej); bez klucza i poza KS - 1 (jak dotad)
            double hwF = KsWageHands.Day(st, _liveDay);
            if (hwF > 1.0) { lab *= hwF; skupLab *= hwF; top *= hwF; }
            // f4-place-ludu (KsFolkWage): czynnik plac WYROBNIKOW W tej doby - (a) wskaznik plac ponizej 1 przy duzej liczbie wolnych rak (Ks Wage Hands Down), (b) stawka wedlug
            // koszyka KS (Ks Labour To Basket: kiesa W w dniach koszyka ponad pasmo - nizsza stawka); podloga przezycia w KsFolkWage. Tnie tylko czesc W robocizny i doplaty -
            // czesc R bez zmiany; czego kasa nie placi, zostaje w kasie (nic nie schodzi). Czynnik 1 (klucze wylaczone, miasto bez KS, podloga) - droga co do monety jak dotad
            // f5-lud (KsFolkIncome): kappy dochodu klas W2 / R / P tej doby (z kies i potrzeb klas wczoraj) - przed placami z kasy; bez kluczy / bez KS - nic
            try { KsFolkIncome.Day(st, f.KW, f.KR, f.KP, f.Need[0], f.Need[1], f.Need[2], f.FacOk); } catch (Exception e) { Stumble("KsFolkIncome.Day", e); }
            double f4A = 1.0, f4B = 1.0;
            double f4NeedW = KsFolkIncome.NeedForF4(st, f.FacOk, f.Need[0]);   // f5-lud (Ks Folk Income Zero Need): W bez koszyka przy policzonej kotwicy - ponad pasmem; inaczej Need[0]
            double f4W = KsFolkWage.WFactor(st, hwF, f.KW, f4NeedW, f.KR, f.KP, f.Need[1], f.Need[2], out f4A, out f4B);
            double f4H = KsFolkWage.HouseholdsOn ? f4B : 1.0;                    // (b) takze place z nadwyzki R/P dla W (krok 5) - klucz Ks Labour Basket Households
            bool f4Split = f4W < 1.0 - 1e-9;
            // f5-lud (Ks Folk Income R P): czesc R robocizny i doplaty x kappa R, obrot P x kappa P (reszta zostaje w kasie); czynniki 1 - droga jak dotad
            double f5R = 1.0, f5P = 1.0;
            bool f5RP = KsFolkIncome.RP(st, out f5R, out f5P);
            bool f5Cut = f5RP && f5R < 1.0 - 1e-9;
            double f4LabW = 0, f4LabR = 0, f4TopW = 0, f4TopR = 0, f4LabFW = 0, f4TopFW = 0, f4TopSw = 0.7, f5LabFR = 0, f5TopFR = 0;
            if (f4Split || f5Cut)
            {
                double sw, sr0, sp0;
                SplitOf(ELabour, out sw, out sr0, out sp0); f4LabW = sw * lab * f4W; f5LabFR = lab - sw * lab; f4LabR = f5LabFR * f5R; f4LabFW = sw * lab;
                SplitOf(ETopUp, out f4TopSw, out sr0, out sp0); f4TopW = f4TopSw * top * f4W; f5TopFR = top - f4TopSw * top; f4TopR = f5TopFR * f5R; f4TopFW = f4TopSw * top;
                double lab0 = lab;
                lab = f4LabW + f4LabR; top = f4TopW + f4TopR;
                if (lab0 > 0) skupLab *= lab / lab0;
            }
            double f5TurnF = turn;
            if (f5RP && f5P < 1.0 - 1e-9) turn *= f5P;
            d.BaseFolk += fp; d.BaseVill += f.VillWin; d.BaseLife += (1 - svc) * life; d.BaseCourt += (1 - cs) * court; d.BaseSale += sale; d.BaseCar += car; d.BaseGm += f.GmMatWin;
            // DS F3 (M7): sprzedaz WLASNA kasy w oknie - recenzja 17.10: bez czesci uslug ludu z zycia (svc) i ludu z dworu (cs), ktore ida prosto do kies ludu (te same liczby co
            // BaseLife / BaseCourt podstawy robocizny), plus sprzedaz, zaplata ludu i wsie - podstawa pomocy od sprzedazy (kasa nie placi od pieniedzy, ktorych nie dostala)
            DsTowns.NoteSales(st, (1 - svc) * life + (1 - cs) * court + sale + fp + f.VillWin);
            d.LabF += lf * rf; d.LabG += lg * rg; d.LabA += la * ra;
            f.VillWin = 0; f.GmMatWin = 0;
            double all = lab + turn + top, k = 1;
            long gold = Math.Max(0, town.Gold);
            if (all > gold && all > 0) { k = gold / all; d.Cut++; }
            bool hwOn = KsWageHands.On && StartCalibration.KsTownApplied(town);   // r4-koszyk: tozsamosc plac (kasa -> kiesy) w miastach z KS - tylko odczyt
            long hwG0 = hwOn ? town.Gold : 0, hwK0 = hwOn ? f.KW + f.KR + f.KP : 0;
            long f4GW1 = 0, f4GW2 = 0;                                           // f4-place-ludu (recenzja): czesc W, ktora NAPRAWDE zeszla z kasy (FromCofferWR) - do tozsamosci linii
            long labPaid = (f4Split || f5Cut) ? FromCofferWR(f, (long)(f4LabW * k), (long)(f4LabR * k), ELabour, out f4GW1) : FromCoffer(f, (long)(lab * k), ELabour);   // f4-place-ludu: czesc W x czynnik (f5-lud: czesc R x kappa R)
            d.LabT += labPaid;
            long hwG1 = hwOn ? town.Gold : 0, hwK1 = hwOn ? f.KW + f.KR + f.KP : 0;
            if (skupLab > 0) d.LabS += (long)(skupLab * k);              // 11c: czesc robocizny od skupu (po przycieciu do kasy) - do linii
            long turnPaid = FromCoffer(f, (long)(turn * k), ETurn);
            d.TurnT += turnPaid;
            long hwG2 = hwOn ? town.Gold : 0, hwK2 = hwOn ? f.KW + f.KR + f.KP : 0;
            long topPaid = (f4Split || f5Cut) ? FromCofferWR(f, (long)(f4TopW * k), (long)(f4TopR * k), ETopUp, out f4GW2) : FromCoffer(f, (long)(top * k), ETopUp);   // f4-place-ludu: czesc W x czynnik (f5-lud: czesc R x kappa R)
            d.TopT += topPaid;
            // f5-lud: tozsamosc linii - czesc R (robocizna + doplata) i obrot P: pelne (czynnik 1) / zlecone kasie / zeszlo z kasy (to samo przyciecie k i zaokraglenie co przelew)
            if (f5Cut) { try { KsFolkIncome.NoteCoffer(1, (long)(f5LabFR * k) + (long)(f5TopFR * k), (long)(f4LabR * k) + (long)(f4TopR * k), Math.Max(0, labPaid - f4GW1) + Math.Max(0, topPaid - f4GW2)); } catch (Exception e) { Stumble("KsFolkIncome.NoteCoffer", e); } }
            if (f5RP && f5P < 1.0 - 1e-9) { try { KsFolkIncome.NoteCoffer(2, (long)(f5TurnF * k), (long)(turn * k), turnPaid); } catch (Exception e) { Stumble("KsFolkIncome.NoteCoffer", e); } }
            // f4-place-ludu (recenzja): pelne = czesc W przy czynniku 1, wedlug reguly = przelew zlecony kasie, zaplacone = czesc W, ktora zeszla z kasy (to samo przyciecie
            // do kasy k i to samo zaokraglenie w dol co przelew) - kontrola linii pelne - zaplacone - zostalo = zlecone - zeszlo (0, gdy kasa zaplacila cale zlecenie)
            if (f4Split) { try { KsFolkWage.NoteCoffer((long)(f4LabFW * k) + (long)(f4TopFW * k), (long)(f4LabW * k) + (long)(f4TopW * k), f4GW1 + f4GW2, f4A, f4B); } catch (Exception e) { Stumble("KsFolkWage.NoteCoffer", e); } }
            if (hwOn) { try { KsWageHands.NotePaid((hwG0 - hwG1) + (hwG2 - town.Gold), (hwK1 - hwK0) + (f.KW + f.KR + f.KP - hwK2), hwF); } catch (Exception e) { Stumble("KsWageHands.NotePaid", e); } }
            // N4 (FolkCraftWageOnSale, tylko licznik linii NE): doplata zaplacona i wedlug starej reguly (wartosc wykonana w oknie, to samo przyciecie do kasy)
            try
            {
                N4TopUp += topPaid;
                if (CraftSaleOn)
                {
                    double f4TopMul = (f4Split || f5Cut) ? (f4TopSw * f4W + (1.0 - f4TopSw) * f5R) * (hwF > 1.0 ? hwF : 1.0) : Math.Max(1.0, hwF);   // f4-place-ludu: ten sam czynnik doplaty co wyzej (wskaznik < 1 bez ciecia - podloga przezycia - doplata zaplacona x1; bez f4 hwF >= 1 - jak dotad); f5-lud: czesc R x kappa R (bez f5 f5R = 1 - jak dotad)
                    double topOld = Math.Max(0, Unit(s.TownFolkCraftWageShare) * CsMadeOf(st) - f.Win[KCraftPaid]) * f4TopMul, allOld = lab + turn + topOld;   // r4-koszyk: ten sam wskaznik plac
                    N4TopUpOld += (long)(topOld * (allOld > gold && allOld > 0 ? gold / allOld : 1));
                }
                else N4TopUpOld += topPaid;
            }
            catch (Exception e) { N4Stumbles++; Stumble("TownLive(N4)", e); }
            // m96-koszyk (TownFolk.M96Koszyk.cs, klucz Ks Folk Wage To Short Classes): placa zatrzymana w kasie tej doby (okno f5 + czesci f4 / f5 placone tutaj:
            // robocizna i doplata wedlug czynnikow 1 wobec zaplaconych, obrot P pelny wobec zaplaconego) do R i P w biedzie
            M96Short(f, ((f4Split || f5Cut) ? (long)((f4LabFW + f5LabFR) * k) + (long)((f4TopFW + f5TopFR) * k) - labPaid - topPaid : 0)
                      + ((f5RP && f5P < 1.0 - 1e-9) ? (long)(f5TurnF * k) - turnPaid : 0));
            // ---- 3. wewnatrz ludu z wczorajszych zakupow: czynsz W 8% / R 13% wydatku (x 1/keep) -> P (plan pkt 0.14), leczenie -> R, sluzba P -> W
            double spW = f.PaidG[0] / KeepW, spR = f.PaidG[1] / KeepR, spP = f.PaidG[2] / KeepP;
            long rentW = (long)Math.Round(RentW * spW), rentR = (long)Math.Round(RentR * spR);
            // E3-G4 (TownGroundRent): 1/3 czynszu W i R do pana miasta (PayLord: oblezenie - zostaje w kiesie, bez zywego pana - do gminy, RentToday - dochod ziemi D),
            // 2/3 do P jak dotad. Klucz wylaczony - caly czynsz do P, co do monety jak dotad
            if (GroundRentOn)
            {
                long gw = rentW / 3, gr = rentR / 3;
                d.GroundRent += PayLord(f, 0, gw, d) + PayLord(f, 1, gr, d);
                rentW -= gw; rentR -= gr;
            }
            d.Rent += Move(f, 0, 2, rentW) + Move(f, 1, 2, rentR);
            d.Heal += Move(f, 0, 1, (long)Math.Round(HlW * spW)) + Move(f, 2, 1, (long)Math.Round(HlP * spP));
            d.Serv += Move(f, 2, 0, (long)Math.Round(ServP * spP));
            Array.Clear(f.PaidG, 0, 3);
            // ---- 4. gmina: 7% stanu - 1/3 farma panu, 2/3 roboty: 75% place W/R po polowie, 25% material do kasy (podstawa robocizny jutro)
            long gx = (long)Math.Round(GminaOut * Math.Max(0, f.KG));
            f.GmWorks = 0;                                             // P2: budzet robot gminy tej doby (TownFolk.Works.cs)
            if (gx > 0)
            {
                long farm = (long)Math.Round(gx * GFarm);
                d.Farm += PayLord(f, 3, farm, d);
                long pub = gx - farm, wages = (long)Math.Round(GLab * pub), mat = pub - wages;
                // P2 (lud buduje miasto, klucz FolkBuildsTown): roboty gminy jako prawdziwe punkty budowy i material zdjety z polki - budzet zostaje w kiesie gminy
                // do BuildFunding.FolkWorks (ta sama doba, po budowach pana); czego budowa nie wezmie, idzie tam na place robot publicznych (W/R po polowie, bez
                // materialu bez sztuki). Klucz wylaczony - krok co do monety jak dotad
                if (WorksRoute) { f.GmWorks = pub; wages = 0; mat = 0; }
                long w1 = wages / 2;
                d.GmWage += Move(f, 3, 0, wages - w1) + Move(f, 3, 1, w1);
                mat = Math.Min(mat, Math.Max(0, f.KG));
                if (mat > 0)
                {
                    town.ChangeGold((int)mat);
                    f.KG -= mat; _ctlOut += mat; d.GmMat += mat; f.GmMatWin += mat;
                }
            }
            // ---- 5. zapas i nadwyzka (potrzeba z wczorajszej kotwicy): W ponad TownFolkReserveDays dni - FolkSurplusSpendShare (15%; dotad 7%) na towar jutro;
            //         R ponad 21, P ponad 30 dni - ten sam udzial: 70% place, 30% towar (decyzja Jeffa 12.10 (3))
            double rdW = Math.Max(1, Math.Min(60, s.TownFolkReserveDays)), sr = SpendRate(s);
            // DS F3: zbytki z prawdziwej nadwyzki - 1/DsFolkSurplusSpendDays dziennie oszczednosci ponad zapas dni zamiast udzialu FolkSurplusSpendShare, rampa DsRampDays od ustalenia firm
            if (DsTowns.FarmsLive && s.DsFolkSurplusSpendDays > 0) { double rp = DsTowns.FolkRamp; sr = (1 - rp) * sr + rp / Math.Min(120, s.DsFolkSurplusSpendDays); }
            f.Offer[0] = f.Offer[1] = f.Offer[2] = 0;
            // P1 (szczebel 4 drabiny nadwyzki): w wojnie i przy potrzebie korony klasa oddaje skarbcowi krolestwa czesc kiesy PONAD zapas dni (PayAid - po robotach gminy,
            // przed oferta na jutro i placami z nadwyzki, ktore licza sie od reszty); wylaczone, pokoj, s = 0 - PayAid zwraca 0 i krok jest co do monety jak przed paczka
            f.DAid = 0; f.DOver = 0; f.DOvC[0] = f.DOvC[1] = f.DOvC[2] = 0;
            // kiesa2 (TownFolk.Hoard.cs): zaraz po daninie P1 tej klasy podatek pana od kiesy ponad poziom normalny (PayHoard; danina tej doby wliczona) - oferta,
            // place z nadwyzki i W7 licza sie od reszty; klucz FolkHoardTax wylaczony - PayHoard zwraca 0 i krok jest co do monety jak przed paczka
            if (f.Need[0] > 0)
            {
                double ov = f.KW - rdW * f.Need[0]; long hx = 0;
                if (ov > 0) { long a0 = PayAid(f, 0, KingdomTreasury.WtW, ov, d); ov -= a0; hx = PayHoard(f, 0, 0, ov, a0, rdW); ov -= hx; f.Offer[0] = (long)(sr * ov); }
                d.Lokata += Math.Max(0, ov);
                HoardW(f, rdW, hx);                                    // kiesa2: kiesa W wobec poziomu normalnego (linia doby)
            }
            if (f.Need[1] > 0)
            {
                double ov = f.KR - RdR * f.Need[1];
                if (ov > 0)
                {
                    long a1 = PayAid(f, 1, KingdomTreasury.WtR, ov, d); ov -= a1; ov -= PayHoard(f, 1, 1, ov, a1, RdR); long lv = (long)(sr * ov), wp = (long)(SurpWage * lv);
                    long wpW = f4H < 1.0 - 1e-9 ? (long)(wp * f4H) : wp;           // f4-place-ludu (b): place W wedlug koszyka - reszta zostaje w kiesie R (oferta P3 liczy sie od nadwyzki)
                    long moved = Move(f, 1, 0, wpW); d.SurpWage += moved; f.Offer[1] = lv - moved;
                    if (wpW < wp) KsFolkWage.NoteSurplus(wp, moved);
                }
            }
            if (f.Need[2] > 0)
            {
                double ov = f.KP - RdP * f.Need[2];
                if (ov > 0)
                {
                    long a2 = PayAid(f, 2, KingdomTreasury.WtP, ov, d); ov -= a2;
                    ov -= PayHoard(f, 2, 2, ov, a2, RdP);
                    long lv = (long)(sr * ov), wp = (long)(SurpWage * lv), toW = (long)(wp * 0.6);
                    long toW4 = f4H < 1.0 - 1e-9 ? (long)(toW * f4H) : toW;         // f4-place-ludu (b): czesc W wedlug koszyka - reszta zostaje w kiesie P (oferta P3 liczy sie od nadwyzki)
                    long movW = Move(f, 2, 0, toW4);
                    long toR = wp - toW;
                    long toR5 = f5Cut ? (long)(toR * f5R) : toR;                     // f5-lud (Ks Folk Income R P): place od P dla R x kappa R - reszta zostaje w kiesie P
                    long movR = Move(f, 2, 1, toR5);
                    long moved = movW + movR;
                    d.SurpWage += moved; f.Offer[2] = lv - moved;
                    if (toW4 < toW) KsFolkWage.NoteSurplus(toW, movW);
                    if (toR5 < toR) { try { KsFolkIncome.NoteHouse(toR, movR); } catch (Exception e) { Stumble("KsFolkIncome.NoteHouse", e); } }
                }
            }
            LadderOffers(f);                                           // P3 (zbytki): oferta na liste dostatku 1/60 nadwyzki po daninie; place R/P wyzej bez zmian
            for (int c = 0; c < 3; c++) d.OffT += f.Offer[c];
            // ---- 6. kotwica na jutro
            double wf, wr; Predict(f, out wf, out wr);
            double r = Scale(f); f.DR = r;
            f.DBook = f.PeopleCal * (PeopleMarket.MarketOn ? BookScale(f) : r);   // GL-2: ludzie ksiegi osady (linia, CSV) - glowy moga isc od ludzi rynku
            Anchor(f, wf, wr);
            NoteHungerDay(f);                                          // E5/C2: doby z rzedu bez chleba z braku pieniedzy
            BreadAnchor(f);                                            // M3: stan "na chlebie" i luka na 2 dni jedzenia (platnosc w kroku korony)
            // ---- liczby swiata
            double hW = f.HW * r, hR = f.HR * r, hP = f.HP * r, hS = f.HS * r, idx = f.Idx > 0 && !double.IsNaN(f.Idx) ? f.Idx : 1.0;
            double B = (hW * BW + hR * BR + hP * BP + (f.Slave ? hS * BS : 0)) * idx;
            d.HW += hW; d.HR += hR; d.HP += hP; d.HS += hS; d.People += f.DBook; d.B += B;
            d.Wg += f.SpendGame; d.Wf += f.SpendFood; d.Wp += wf + wr; d.G0 += f.G0; d.G0a += f.G0a;
            d.MSum += f.MAnc; d.MN++; if (double.IsNaN(d.MMin) || f.MAnc < d.MMin) d.MMin = f.MAnc; if (f.MAnc < 1) d.MLow++;
            double ff = s.TownFolkAnchorBasket ? Clamp01(f.FacFood) : 1, fr = s.TownFolkAnchorBasket ? Clamp01(f.FacRest) : 1;
            d.FfSum += ff; d.FrSum += fr; if (double.IsNaN(d.FfMin) || ff < d.FfMin) d.FfMin = ff; if (double.IsNaN(d.FrMin) || fr < d.FrMin) d.FrMin = fr; if (ff < 1 || fr < 1) d.FacLow++;
            d.KW += f.KW; d.KR += f.KR; d.KP += f.KP; d.KG += f.KG;
            double[] fdc = { FdW, FdR, FdP };
            for (int c = 0; c < 3; c++)
            {
                double N = f.Need[c];
                if (c == 0) { d.NW += N; d.AW += f.AffC[c] * N; d.NFW += N * fdc[c]; d.AFW += f.AffFC[c] * N * fdc[c]; }
                else if (c == 1) { d.NR += N; d.AR += f.AffC[c] * N; d.NFR += N * fdc[c]; d.AFR += f.AffFC[c] * N * fdc[c]; }
                else { d.NP += N; d.AP += f.AffC[c] * N; d.NFP += N * fdc[c]; d.AFP += f.AffFC[c] * N * fdc[c]; }
            }
            if (Math.Min(f.AffFC[0], Math.Min(f.AffFC[1], f.AffFC[2])) < 0.95) d.NFood++;
            if (Math.Min(f.AffC[0], Math.Min(f.AffC[1], f.AffC[2])) < 0.95) d.NFull++;
            f.DB = B; f.DWg = f.SpendGame; f.DM = f.MAnc;
            double needAll = f.Need[0] + f.Need[1] + f.Need[2], nf = f.Need[0] * FdW + f.Need[1] * FdR + f.Need[2] * FdP;
            f.DAff = needAll > 0 ? (f.AffC[0] * f.Need[0] + f.AffC[1] * f.Need[1] + f.AffC[2] * f.Need[2]) / needAll : 1;
            f.DAffF = nf > 0 ? (f.AffFC[0] * f.Need[0] * FdW + f.AffFC[1] * f.Need[1] * FdR + f.AffFC[2] * f.Need[2] * FdP) / nf : 1;
            f.ADay = _liveDay;                                         // E5/C4: liczby tej doby gotowe (okno People of this land)
        }

        private static void CastleLiveDay(Folk f, LDay d)
        {
            var s = Settings.Current;
            var st = f.St; var town = st.Town;
            if (town == null) return;
            d.Castles++;
            if (!CastleLive)
            {
                // zawor zamku 110 wylaczony przy kiesie czynnej: kiesa podzamcza wraca do kasy zamku (stan sprzed B1 dla zamkow), kredyt dworu 0; ponowne wlaczenie - z nadwyzki
                if (f.KW > 0) { town.ChangeGold((int)Math.Min(int.MaxValue, f.KW)); _ctlOut += f.KW; f.KW = 0; _seedC = 0; }
                f.CourtCr = 0; Array.Clear(f.EarnWin, 0, 3); f.Offer[0] = 0; f.FacOk = false; f.HungerDays = 0;   // E5/C2: kiesa nieczynna - licznik glodu od zera
                f.BreadOn = false; f.BreadZero = 0; f.BreadGap = 0;    // M3: bez kiesy podzamcza - bez chleba
                return;
            }
            // ---- 1. pan 3% od zarobku okna; E5/B2 korona 184 na wierzch (scalenie B)
            long eC = f.EarnWin[0];
            f.ZcE[0] = eC; f.ZcE[1] = f.ZcE[2] = 0; f.ZcDay = _liveDay;      // ZC0 (DailyLife): zarobek okna podzamcza tej doby - tylko odczyt
            long tax = (long)Math.Round(eC * TaxC);
            d.CInc += eC;
            Array.Clear(f.EarnWin, 0, 3);
            d.TaxC += PayLord(f, 0, tax, d);
            PayCrown(f, 0, KingdomTreasury.WtCastle, eC);
            // ---- 2. robocizna z kasy zamku od sprzedazy zamku: 75% zycia i zalogi (jedzenie), 80% dworu, sprzedaz okna, towar podzamcza, zakupy wsi
            double svc = Unit(s.TownFolkServiceShare), cs = Unit(s.CourtFolkShare);
            double rf = Unit(s.TownFolkLabourFood), rg = Unit(s.TownFolkLabourGoods), ra = Unit(s.TownFolkLabourArms);
            double life = f.Win[KGarrison] + f.Win[KLife] + f.Win[KLifeCaravan] + f.Win[KLifeBand], court = f.Win[KCourt];
            double lab = (1 - svc) * life * rf + (1 - cs) * court * (0.5 * rf + 0.2 * rg + 0.3 * ra);
            for (int mi = 0; mi < Mixes; mi++) lab += f.Sale[mi] * (MixOf[mi][0] * rf + MixOf[mi][1] * rg + MixOf[mi][2] * ra);
            lab += f.PaidKeep * (0.8 * rf + 0.2 * rg) + f.VillWin * (0.2 * rf + 0.8 * rg);
            if (DsTowns.On) { double dsSale = 0; for (int mi = 0; mi < Mixes; mi++) dsSale += f.Sale[mi]; DsTowns.NoteSales(st, (1 - svc) * life + (1 - cs) * court + dsSale + f.PaidKeep + f.VillWin); }   // DS F3 (M7): sprzedaz WLASNA kasy zamku w oknie (bez czesci ludu - recenzja 17.10)
            f.VillWin = 0;
            d.LabC += FromCoffer(f, (long)Math.Min(lab, Math.Max(0, town.Gold)), ELabour);
            // ---- 5. nadwyzka ponad 10 dni potrzeby: 7% na towar jutro (konsumpcja zamku)
            f.Offer[0] = 0;
            f.DAid = 0; f.DOver = 0; f.DOvC[0] = 0;                    // P1: danina podzamcza z kiesy ponad 10 dni potrzeby - przed oferta (jak w miescie)
            if (f.Need[0] > 0)
            {
                double ov = f.KW - RdC * f.Need[0]; long hx = 0;
                if (ov > 0) { long a0 = PayAid(f, 0, KingdomTreasury.WtCastle, ov, d); ov -= a0; hx = PayHoard(f, 0, 3, ov, a0, RdC); ov -= hx; f.Offer[0] = (long)(SurplusRate * ov); }
                HoardW(f, RdC, hx);                                    // kiesa2: podzamcze po daninie (jak w miescie)
            }
            CastleLadder(f, d);                                        // P3/P5 (zbytki): podzamcze 1/60 nadwyzki - oferta w konsumpcji zamku albo zakup w miescie najblizszym
            d.OffC += f.Offer[0];
            // ---- 6. kotwica (z kredytem dworu)
            double wf, wr; Predict(f, out wf, out wr);
            Anchor(f, wf, wr);
            NoteHungerDay(f);                                          // E5/C2
            BreadAnchor(f);                                            // M3
            if (f.CourtCr <= 0) d.CNoCourt++;
            double ch = CastleHeads(f);                                // GL-3 (P3)
            d.CHeads += ch; d.KC += f.KW; d.CCr += f.CourtCr; d.CNeed += f.Need[0]; d.CAff += f.AffC[0] * f.Need[0]; d.CWg += f.SpendGame;
            f.DB = ch * BC * (f.Idx > 0 ? f.Idx : 1); f.DWg = f.SpendGame; f.DM = f.MAnc; f.DAff = f.AffC[0]; f.DAffF = f.AffFC[0]; f.DR = 1;
            f.ADay = _liveDay;                                         // E5/C4
            // jedzenie zuzyte w konsumpcji zamku (pierscien 7 dob - linia "Jedzenie z targu", B4)
            f.Food7[f.Food7Head] = f.FoodSold; f.Food7Head = (f.Food7Head + 1) % 7; if (f.Food7Fill < 7) f.Food7Fill++;
            f.FoodSold = 0;
        }

        // ============================================================ wsie (R13): zakupy w miescie targowym, oplata stala, prog daniny
        private static double VillHeads(Folk f, bool scaled)
        {
            double h;
            if (scaled && VillPeopleHeads(f, out h)) return h;            // GL-3 (P3): srednia swiata glow BK wsi x u_v (klucz VillageHeadsByPeople)
            return Math.Max(0, f.HW + f.HR) * (scaled ? Scale(f) : 1.0);
        }

        private static bool GroundRentOn { get { var s = Settings.Current; return s != null && s.TownGroundRent; } }

        /// <summary>E3-G4: czynsz gruntowy tej doby do panow miast (Pompy F; -1 - doba nieliczona albo klucz wylaczony).</summary>
        internal static long LastGroundRent { get { var d = _ld; return d != null && GroundRentOn && _liveDay == (int)CampaignTime.Now.ToDays - 1 ? d.GroundRent : -1; } }

        private static void Villages(LDay d)
        {
            var s = Settings.Current;
            // NE3-16 (poprawki): wczorajsze "bez towaru" i ile z nich kupilo potem wozem - do linii; dzis zbior od nowa
            if (_w1NgRan) { _w1NgLastN = _w1NgCur.Count; _w1ServedLastN = _w1Served.Count; }
            _w1NgRan = true; _w1NgCur.Clear(); _w1Served.Clear();
            _vwPrev = _vwCur; _vwCur = new Dictionary<Settlement, Dictionary<ItemCategory, double>>();   // ZC1: zakupy wsi wedlug targu - wczorajsze do wycen wozow, dzisiejsze od zera
            double pph = Math.Max(0f, Math.Min(1f, s.VillagePurchasePerHead));
            if (_villCats == null) _villCats = CatsOf(VillIds, out _villText);
            var shelf = new Dictionary<Settlement, List<EquipmentElement>>();
            foreach (var f in _f.Values)
            {
                if (f.Kind != 2) continue;
                Village vilTax = null; double nvTax = 0;               // obieg2: podatek od nadwyzki wsi po koszyku dnia i P5 (finally - takze przy "continue" bez zakupow)
                Village vilDs = null;                                  // DS (F1): krok 5 - zbytki z nadwyzki ponad skrzynie (finally - takze przy "continue" bez koszyka)
                bool dsBasketCut = false;                              // r4-bkwies (Ks Surplus Skips Basket): wies z koszykiem ZC4 i Ks Village Paid First (jak leakCut) - krok 5 bez listy wsi
                try
                {
                    f.DVNeed = 0; f.DVBuy = 0;                         // E5/C4: zakupy tej doby (okno People of this land)
                    var vil = f.St.Village;
                    if (vil == null) continue;
                    if (!f.Cal) { d.VNoCal++; continue; }
                    d.Villages++;
                    double nv = pph * VillHeads(f, true);
                    vilTax = vil; nvTax = nv;
                    // DS (F1): wies DS - stare wydatki z nadwyzki (15%/d na stara liste, stara drabina P5) x (1 - rampa), po rampie 0; krok 5 w finally
                    bool ds = DsVillage.On(f.St);
                    double oldK = ds ? DsVillage.OldShare(f.St) : 1.0;
                    if (ds) vilDs = vil;
                    // ZC4 (KS etap 2): koszyk wsi ze stanow (to, czego wies nie zjada z wlasnego pola) x lambda - z kiesy wsi w miescie targowym, po kolei drabiny zakupow,
                    // PRZED starym koszykiem dnia (sol, narzedzia, len, garnki, piwo), ktory zostaje x (1 - lambda); poza ZC4 - jak dotad
                    double zl; var zq = StartCalibration.Zc4VillageNeed(vil, out zl);
                    var mk0 = vil.TradeBound ?? vil.Bound;
                    long zSpent = 0;
                    bool zc4 = zq != null && zl > 0 && mk0 != null && mk0.Town != null;
                    dsBasketCut = zc4 && StartCalibration.SwPaidFirst;     // r4-bkwies: ten sam warunek co leakCut nizej (przed drabina - wyjatek drabiny nie zmienia kroku 5)
                    if (zc4)
                    {
                        // KS-R3 H1 (klucz Ks Village Buy Every 2): koszyk co 2. doba za dwie doby (wies wedlug Fnv), ulamki przechodza dalej
                        int every = StartCalibration.SwBuyEvery2 ? 2 : 1;
                        if (every == 1 || ((KsFnv(vil.Settlement.StringId) + (uint)_liveDay) & 1u) == 0u)
                        {
                            long zt0 = Stopwatch.GetTimestamp();
                            zSpent = Zc4BuyVillage(vil, mk0, zq, zl * every, d, every);
                            StartCalibration.Zc4NoteTime(Stopwatch.GetTimestamp() - zt0);
                            Folk mz; if (zSpent > 0 && _f.TryGetValue(mk0, out mz)) mz.VillWin += zSpent;
                        }
                    }
                    else zl = 0;
                    double nvo = nv * (1 - zl);
                    int gold = vil.Gold;
                    double buy1 = Math.Min(nvo, Math.Max(0, gold));
                    // decyzja Jeffa 12.10 (3): 15%/d ponad 15 dni zakupow (dotad 7%). KS recenzja 16.10: ZC4 zastepuje koszyk BV, NIE wydatek z nadwyzki (projekt ZC 8.5) -
                    // bez (1 - lambda): inaczej przy lambda 1 wies wydawala tylko koszyk, a ok. 67 tys. zl/d utargu Essos zostawalo w kiesach wsi (obieg sie zatykal)
                    double extra = SpendRate(s) * Math.Max(0, gold - buy1 - RdV * nv) * oldK;   // DS: x (1 - rampa)
                    // KS-R3 G4 (klucz Ks Village Paid First): wies z koszykiem ZC4 - bez wydatku z nadwyzki na stara liste i bez P5 (luksusy); zostaje stary koszyk x (1 - lambda)
                    // i podatek od nadwyzki (PayHoardVillage) - kiesa idzie na koszyk z Planu (alfa netto liczy tylko to, co zostaje)
                    bool leakCut = zc4 && StartCalibration.SwPaidFirst;
                    if (leakCut) extra = 0;
                    if (ds) DsVillage.NoteOldExtras(extra, 0);
                    double wx = VillWorkOnly() ? extra : 0;            // P5 (zbytki, recenzja WAZNE 2): nadwyzka na koszyk tylko dla soli i narzedzi (len, garnki, piwo - lista)
                    double want = buy1 + extra;
                    d.VNeed += nvo; d.VWant += want;
                    f.DVNeed = nv; f.ADay = _liveDay;                  // E5/C4
                    f.DVBuy = Math.Max(0, zSpent);
                    if (want < 1) continue;
                    var mk = mk0;
                    if (mk == null || mk.Town == null) { d.NoTrade++; continue; }
                    List<EquipmentElement> list;
                    if (!shelf.TryGetValue(mk, out list)) { list = ShelfOf(mk); shelf[mk] = list; }
                    long spent;
                    var gfv = GoodsLedger.Begin(GoodsLedger.FFolkVill, mk);   // KS F0: ksiega towarow na rynek miasta targowego (dotad ramka bez osady - "bez rynku")
                    try { spent = BuyGoods(vil, mk, list, want - wx, d, wx); }
                    finally { GoodsLedger.End(gfv); }
                    // KS-R3 P2: stara lista wsi zastosowanej - wydatek spoza koszyka (alfa netto). Recenzja runda 3 (drobne): tylko bez ciecia starej listy i przy pelnej lambdzie (w rampie
                    // A..D+14 stara lista to jeszcze koszyk starej gospodarki - dotad szla do wydatkow spoza koszyka i zanizala alfe Planow D..D+42)
                    if (spent > 0 && !leakCut && zl >= 0.999 && StartCalibration.AppliedFor(vil)) StartCalibration.KsVilOut(vil.Settlement, spent);
                    f.DVBuy = Math.Max(0, spent) + Math.Max(0, zSpent);    // E5/C4
                    // E3-W1: potrzeba dnia niekupiona w miescie targowym czeka na woz tej wsi w innym miescie (najwyzej W1Days dni potrzeby; PRZED "wsi bez towaru")
                    if (W1On)
                    {
                        double un = Math.Max(0, buy1 - Math.Max(0, spent)), ow;
                        _w1Owed.TryGetValue(vil, out ow);
                        ow = Math.Min(W1Days * nvo, ow + un);
                        if (ow >= 1) _w1Owed[vil] = ow; else _w1Owed.Remove(vil);
                    }
                    if (!leakCut)
                    {
                        int vg0 = vil.Gold;
                        VillageLadder(f, vil, mk, gold, buy1, nv, oldK); // P5 (DS x (1 - rampa)) (zbytki): nadwyzka wsi na liste dostatku w miescie targowym (koszyk dnia wyzej bez zmian; KS recenzja 16.10: nadwyzka poza koszykiem ZC4 - bez (1 - lambda))
                        if (vg0 > vil.Gold && zl >= 0.999 && StartCalibration.AppliedFor(vil)) StartCalibration.KsVilOut(vil.Settlement, vg0 - vil.Gold);   // KS-R3 P2: luksusy wsi zastosowanej - spoza koszyka (pelna lambda)
                    }
                    if (spent <= 0) { d.VNoGoods++; _w1NgCur.Add(vil); continue; }
                    Folk mf; if (_f.TryGetValue(mk, out mf)) mf.VillWin += spent;
                }
                catch (Exception e) { f.ADay = -1; Stumble("Villages(wies)", e); }   // E5/C4: liczby wsi z tej doby niepewne - okno bez liczby
                finally
                {
                    if (vilDs != null) DsSurplus(f, vilDs, nvTax, d, shelf, dsBasketCut);   // DS (F1) krok 5: zbytki tylko z nadwyzki ponad skrzynie (wlasny try)
                    if (vilTax != null && nvTax > 0) PayHoardVillage(f, vilTax, nvTax);   // obieg2: klucz FolkHoardVillages wylaczony - nic (nie rzuca)
                }
            }
        }

        /// <summary>
        /// DS (F1) krok 5 (Villages, wies DS, po koszyku dnia i zaleglosci koszyka): skrzynia = DsVillageChestDays x (koszyk dnia + czynsz dnia); zbytki dnia =
        /// rampa x (kiesa - skrzynia) / DsSurplusSpendDays (DsVillage.SurplusDay) - czesc P5 teraz: 1. lista dostatku na polce miasta targowego (BuyLadder, jak P5);
        /// 2. RESZTA budzetu (recenzja 17.10: dotad tylko gdy lista dostatku nie kupila nic) - lista wsi (BuyGoods; przy VillWorkOnly najpierw sol i narzedzia, jak dawne
        /// 15%/d z nadwyzki); 3. czego nie ma na polkach - ROBOTY GMINY (DsCommunalWorks; DsWorks). Czesc rynku BK odlozona do ticku BK wsi. Ponizej skrzyni nic. Nie rzuca.
        /// r4-bkwies (recenzja): czesc listy wsi przeznaczona na potrzebe JUZ POKRYTA (wies z koszykiem ZC4 - towary koszyka; krok 5 do potrzeby - zapas w domu pelny; plotno
        /// zatrzymane progiem K2) ZOSTAJE W KIESIE (keep, linia DS "zostalo w kiesie (potrzeba pokryta)") - do robot gminy tylko to, co nie znalazlo towaru.
        /// </summary>
        private static void DsSurplus(Folk f, Village vil, double nv, LDay d, Dictionary<Settlement, List<EquipmentElement>> shelf, bool basketCut = false)
        {
            try
            {
                var s = Settings.Current;
                double rent = Math.Max(0f, Math.Min(50f, s.VillageDuesPerHeadYear)) / YearDays * VillHeads(f, true);
                VillNeeds.NoteChest(nv, VillNeeds.ChestBasket(f.St, nv));          // r4-bkwies (B2/M9): licznik linii - skrzynia z koszyka Planu (ChestOf liczy ja sam)
                double chest = DsVillage.ChestOf(f.St, nv, rent);
                double p5 = DsVillage.SurplusDay(f.St, vil.Gold, chest);
                if (p5 < 1) return;
                var mk = vil.TradeBound ?? vil.Bound;
                if (mk == null || mk.Town == null) { DsVillage.NoteP5(0, 0, 0, 0, p5); return; }
                long got = 0;
                if (WardsShopOn && mk.IsTown && _lxCats != null)
                {
                    long pcs = 0;
                    _lxVN++; _lxVWant += p5;                               // linia "Zbytki ludu (P3-P5)": P5 wsi (stara drabina i krok 5 DS razem)
                    if (f.LxSeen != _liveDay) { f.DLxOff = 0; f.DLxBuy = 0; f.LxSeen = _liveDay; }
                    f.DLxOff += p5;
                    long g1 = BuyLadder(vil, null, mk, p5, ref pcs);
                    if (g1 > 0) { got = g1; _lxVBuy += g1; _lxVPcs += pcs; f.DLxBuy += g1; }
                }
                double rest = p5 - got, keep = 0;
                if (rest >= 1)
                {
                    List<EquipmentElement> list;
                    if (shelf == null || !shelf.TryGetValue(mk, out list)) { list = ShelfOf(mk); if (shelf != null) shelf[mk] = list; }
                    bool wo = VillWorkOnly();                                         // jak dawne 15%/d z nadwyzki (wx): sol i narzedzia
                    // r4-bkwies + r4-ceny (jeden klucz Ks Surplus Skips Basket; diagnoza fin80 "sol"): wies z koszykiem ZC4 (Ks Village Paid First) - sol, narzedzia, len, garnki i piwo kupuje drabina
                    // koszyka z limitem potrzeby i ceny; krok 5 nie kupuje ich drugi raz (jedna zasada na jedno zjawisko). Recenzja: czesc, ktora lista wsi dostalaby (podzial
                    // BuyGoods), ZOSTAJE W KIESIE (keep - potrzeba pokryta); reszta (czego lista dostatku nie znalazla na polkach) - jak dotad roboty gminy
                    if (basketCut && VillNeeds.SkipBasketOn) { keep = Math.Min(rest, DsSkipShare(list, wo ? 0 : rest, wo ? rest : 0)); VillNeeds.S5Skip(rest, keep); StartCalibration.R4cNoteSurplusSkip((long)rest); }   // scalenie: licznik linii r4-ceny (krok 5 bez listy wsi)
                    else
                    {
                        // r4-bkwies (Ds Surplus Ledger Frame): ramka ksiegi towarow na rynek miasta targowego (dotad jedyny zakup wsi bez ramki z osada - "bez rynku")
                        var gf5 = VillNeeds.FrameOn ? GoodsLedger.Begin(GoodsLedger.FFolkVill, mk) : null;
                        if (gf5 != null) VillNeeds.S5Frame();
                        long g2;
                        try
                        {
                            // r4-bkwies (Ds Surplus Need Limit): wies bez koszyka ZC4 - sol i narzedzia najwyzej do zapasu potrzeb; odmowione zostaje w kiesie (keep)
                            if (VillNeeds.LimitOn && !basketCut) g2 = DsBuyNeeds(vil, mk, list, wo ? 0 : rest, d, wo ? rest : 0, out keep);
                            else g2 = BuyGoods(vil, mk, list, wo ? 0 : rest, d, wo ? rest : 0);
                        }
                        finally { GoodsLedger.End(gf5); }
                        if (gf5 != null) StartCalibration.R4cNoteSurplusFramed(g2);   // scalenie: licznik linii r4-ceny (zakupy listy z ramka rynku)
                        if (g2 > 0) { got += g2; Folk mf; if (_f.TryGetValue(mk, out mf)) mf.VillWin += g2; }
                    }
                }
                rest = p5 - got - keep;                                           // keep = 0 bez limitu potrzeby i bez pominiecia koszyka (jak dotad)
                long works = 0, wage = 0, mat = 0;
                if (rest >= 1 && s.DsCommunalWorks) works = DsWorks(vil, mk, rest, out wage, out mat);
                if (got > 0) f.DVBuy += got;                                      // E5/C4: zakupy tej doby (towar; roboty gminy - nie towar)
                DsVillage.NoteP5(got, works, wage, mat, rest - works);
                if (keep >= 1) DsVillage.NoteP5Kept(keep);                        // r4-bkwies (recenzja): P5 = towar + roboty + niewydane + zostalo w kiesie (potrzeba pokryta)
            }
            catch (Exception e) { Stumble("DsSurplus", e); }
        }

        /// <summary>
        /// DS (F1) krok 5 - ROBOTY GMINY WSI (DsCommunalWorks, recenzja 17.10): czesc zbytkow, ktorej wies nie wydala na towar (brak na polkach miasta targowego), idzie na
        /// wspolne roboty - fabryka kosciola, mlyn, most, drogi. Ta sama regula co budowa septow z dziesieciny (VillageTithe / TitheVillageAtCart): kiesa wsi -> 3/4 place
        /// ludu miasta targowego (Credit KBuildLabour - murarze i ciesle; zarobek, podatek jutro) i 1/4 material do kasy tego miasta (sprzedaz kupcow MixBuild - podstawa
        /// robocizny; jak material robot gminy - bez zdejmowania towaru z polki). Miasto targowe bez kiesy ludu (albo zamek przy zaworze zamku wylaczonym) - nic (zostaje
        /// w kiesie). W bloku doby ludu (migawka M18 ksiegi - jak dziesiecina i zakupy wsi). Zwraca zdjete z kiesy wsi = wage + mat.
        /// </summary>
        private static long DsWorks(Village vil, Settlement mk, double want, out long wage, out long mat)
        {
            wage = mat = 0;
            if (vil == null || mk == null || mk.Town == null || !(want >= 1)) return 0;
            Folk mf = null;
            _f.TryGetValue(mk, out mf);                                          // TryGetValue - iterujemy _f.Values (jak VillageTithe)
            if (mf == null || mf.Kind > 1 || (mf.Kind == 1 && !CastleLive)) return 0;
            long x = (long)Math.Floor(Math.Min(want, (double)Math.Max(0, vil.Gold)));
            if (x <= 0) return 0;
            // ds-akumulator (DsWorksRealMaterial): gmina robi roboty wlasnymi rekami - kiesa placi tylko PRAWDZIWY material z polki miasta targowego i rzemieslnikow miasta
            // (najwyzej DsWorksWagePerMaterial x material); zlecenie wykonane po budowach pana i ludu tej doby (TownFolk.DsAkum.cs DaWorksRun). Brak materialu - pieniadz zostaje.
            if (DaWorksOn) { DaWorksQueue(vil, null, mk, x, false); return 0; }
            int had = vil.Gold;
            vil.ChangeGold(-(int)Math.Min(int.MaxValue, x));
            long got = Math.Max(0, (long)had - vil.Gold);
            if (got <= 0) return 0;
            wage = (long)Math.Round(got * TitheWage); mat = got - wage;
            if (wage > 0) { Credit(mf, wage, KBuildLabour); _ctlIn += wage; }   // kiesa wsi -> kiesa ludu miasta targowego
            if (mat > 0) { mk.Town.ChangeGold((int)mat); mf.Sale[MixBuild] += mat; }
            return got;
        }

        /// <summary>Towar dla wsi na polce miasta targowego (kategorie sol, narzedzia, len, garnki, piwo) - raz na dobe na targ.</summary>
        private static List<EquipmentElement> ShelfOf(Settlement mk)
        {
            var l = new List<EquipmentElement>();
            var r = mk.ItemRoster;
            if (r == null) return l;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= ShelfKeep || it.ItemCategory == null || !_villCats.Contains(it.ItemCategory)) continue;
                l.Add(el.EquipmentElement);
            }
            return l;
        }

        /// <summary>Towary 2 / K2 (docs/towary-2/ROZPOZNANIE-TOWARY-2.md A2-1, klucz VillageClothAboveReserve): koszyk dnia wsi bierze plotno tylko ponad prog polki miasta
        /// targowego (MaterialOrders.ShelfReserve - ten sam prog co FolkClothAboveReserve dla zbytkow ludu, podzamczy i wsi P5). Wylaczony - 5 sztuk jak dotad.</summary>
        private static bool VillClothOn { get { var s = Settings.Current; return s != null && s.VillageClothAboveReserve; } }
        private static bool ClothHalfOn { get { var s = Settings.Current; return s != null && s.VillageClothAboveReserve && s.VillageClothHalfReserve; } }

        /// <summary>
        /// E3-W1 (VillageCartBringsGoods; uwaga recenzji 1: zakup w chwili, gdy woz jest w miescie, bez okna "21 dob po wizycie"): woz wsi sprzedal plon w miescie
        /// albo zamku innym niz miasto targowe wsi - wies kupuje tam z polki koszyk wsi (sol, narzedzia, len i plotno, garnki, piwo - ta sama lista, progi ShelfKeep
        /// i K2) za zaleglosc koszyka (potrzeba dni niekupiona w miescie targowym, najwyzej 7 dni potrzeby), najwyzej do stanu kiesy. Kiesa wsi -> kasa tej osady
        /// (BuyGoods), towar zuzyty we wsi (ramka ksiegi towarow "zakupy wsi w miastach"); podstawa robocizny targu tej osady (VillWin) jak przy koszyku dnia.
        /// Wyjatek - licznik potkniec (sprzedaz wozu zostaje).
        /// </summary>
        internal static void CartBuys(Settlement st, TaleWorlds.CampaignSystem.Party.MobileParty cart)
        {
            if (st == null || cart == null || !cart.IsVillager) return;
            try
            {
                if (!W1On || !Live || _villCats == null || st.Town == null || st.ItemRoster == null) return;
                var hs = cart.HomeSettlement;
                var vil = hs != null ? hs.Village : null;
                if (vil == null) return;
                var mk = vil.TradeBound ?? vil.Bound;
                if (ReferenceEquals(mk, st)) return;                  // miasto targowe - koszyk dnia kupil tu juz dzis
                double owed;
                if (!_w1Owed.TryGetValue(vil, out owed) || owed < 1) { _w1NoOwed++; return; }
                double budget = Math.Min(owed, Math.Max(0, vil.Gold));
                _w1Tries++;
                if (budget < 1) { _w1NoGold++; return; }
                var list = ShelfOf(st);
                if (list.Count == 0) { _w1NoGoods++; return; }
                long p0 = _w1d.VPieces;
                var gf = GoodsLedger.Begin(GoodsLedger.FFolkVill, st);   // KS F0: na rynek miasta sprzedazy wozu
                long spent;
                try { spent = BuyGoods(vil, st, list, budget, _w1d); }
                finally { GoodsLedger.End(gf); }
                if (spent <= 0) { _w1NoGoods++; return; }
                // poprawki (uwaga recenzji 1 i 2): kiesa wsi -> kasa osady poza tickiem, w srodku okna sprzedazy wozu - nazwane w "Przeplywy osad (przyczyny)" po obu
                // stronach (bez tego reszta kas miast TM1 rosla, a kies wsi TW1 spadala o W1) i przeniesione z kanalu "tabory wsi" do "wsie" w "Kasy zaulkow (m6b)"
                int sp = (int)Math.Min(int.MaxValue, spent);
                MoneyLedger.Note169(MoneyLedger.N169CartBuy, st, sp);
                MoneyLedger.Note169(MoneyLedger.N169CartBuy, vil.Settlement, -sp);
                AlleyProbe.NoteVillageBuy(st.Town, sp);
                owed -= spent;
                if (owed >= 1) _w1Owed[vil] = owed; else _w1Owed.Remove(vil);
                _w1N++; _w1Gold += spent; _w1Pcs += _w1d.VPieces - p0; _w1Vills.Add(vil);
                if (_w1NgCur.Contains(vil)) _w1Served.Add(vil);           // NE3-16: wies bez towaru w miescie targowym kupila wozem
                Folk mf; if (_f.TryGetValue(st, out mf)) mf.VillWin += spent;
            }
            catch (Exception e) { _w1Stumbles++; Stumble("CartBuys", e); }
        }

        /// <summary>Dopisek czesci "wsie" linii "Kiesa ludu": E3-W1 od ostatniej linii (liczniki zerowane) i E3-W1b.</summary>
        private static string W1Note(LDay d)
        {
            double owedSum = 0; foreach (var v in _w1Owed.Values) owedSum += v;
            string t = ", woz kupuje w miescie sprzedazy (E3-W1) " + (W1On ? "tak" : "nie") + ": zakupow " + _w1N + " za " + _w1Gold + " zl, sztuk " + _w1Pcs + ", wsi " + _w1Vills.Count
                       + " (proby " + _w1Tries + ", bez towaru " + _w1NoGoods + ", bez kiesy " + _w1NoGold + ", bez zaleglosci " + _w1NoOwed + ", potkniecia " + _w1Stumbles + "), zaleglosc koszyka wsi "
                       + N0(owedSum) + " zl u " + _w1Owed.Count + " wsi (najwyzej " + W1Days.ToString("0", Inv) + " dni potrzeby)"
                       + ", plotno od polowy progu tam, gdzie tanie (E3-W1b) " + (ClothHalfOn ? "tak" : "nie") + ": wierszy " + d.VClHalf
                       + ", wsi bez towaru po W1 " + (_w1NgLastN >= 0 ? (Math.Max(0, _w1NgLastN - _w1ServedLastN)).ToString(Inv) : "-")
                       + " (poprzedni koszyk dnia: bez towaru " + (_w1NgLastN >= 0 ? _w1NgLastN.ToString(Inv) : "-") + ", z nich kupily potem wozem " + _w1ServedLastN + ")";
            W1Zero();
            return t;
        }

        /// <summary>Zakup wsi w miescie targowym: budzet po rowno na kategorie obecne, cena targu, najwyzej sztuki ponad 5; najpierw zloto (wies -> kasa), potem towar z polki.
        /// K2 (VillageClothAboveReserve): plotno najwyzej ponad prog polki miasta (MaterialOrders.ShelfReserve); podzial budzetu na kategorie bez zmian (plotno na polce ponad 5
        /// sztuk dalej ma swoja czesc), czesc plotna niewydana przez prog zostaje w kiesie wsi - reszta koszyka dostaje dokladnie tyle co dotad (jak udzial wypadlych kategorii
        /// w FolkClothAboveReserve).</summary>
        private static long BuyGoods(Village vil, Settlement mk, List<EquipmentElement> list, double want, LDay d, double workExtra = 0)
        {
            if (list.Count == 0) return 0;
            var town = mk.Town; var roster = mk.ItemRoster;
            bool clOn = VillClothOn;
            var cats = new List<ItemCategory>();
            foreach (var e in list) { var c = e.Item.ItemCategory; if (!cats.Contains(c)) cats.Add(c); }
            double per = want / cats.Count;
            double perW = workExtra > 0 ? workExtra / cats.Count : 0;  // P5 (recenzja WAZNE 2): nadwyzka wsi - sol i narzedzia tyle samo zl na kategorie co bez P5
            long spent = 0;
            foreach (var c in cats)
            {
                double left = per + (perW > 0 && VillWorkCat(c) ? perW : 0);
                bool cloth = clOn && c.StringId == "linen", held = false;   // K2: plotno tylko ponad prog polki
                foreach (var e in list)
                {
                    if (left < 1) break;
                    if (e.Item.ItemCategory != c) continue;
                    int ix = roster.FindIndexOfElement(e);
                    if (ix < 0) continue;
                    int keepRow = ShelfKeep;
                    if (cloth)
                    {
                        int r = MaterialOrders.ShelfReserve(town, e.Item);
                        // E3-W1b (VillageClothHalfReserve): plotno tanie w tym miescie (indeks < 1 - lezy) - prog od polowy zapasu wojska (odziez 150 i zbrojownie maja polowe)
                        if (ClothHalfOn && r > ShelfKeep && town.GetItemCategoryPriceIndex(c) < 1f) { r = Math.Max(ShelfKeep, r / 2); d.VClHalf++; }
                        if (r > ShelfKeep) keepRow = r;
                        d.VClCap += keepRow; d.VClRows++;
                    }
                    int have = roster.GetElementNumber(ix) - keepRow;
                    if (have <= 0) { if (keepRow > ShelfKeep && roster.GetElementNumber(ix) > ShelfKeep) { d.VClHeld++; held = true; } continue; }
                    int price = town.MarketData.GetPrice(e);
                    if (price <= 0) continue;
                    int n = (int)Math.Min(have, Math.Floor(left / price));
                    if (n <= 0) continue;
                    if (keepRow > ShelfKeep && n == have) held = true;   // K2: zakup skonczyl sie na progu polki, nie na budzecie
                    int cost = n * price;
                    int had = vil.Gold;
                    if (cost > had) { n = had / price; cost = n * price; if (n <= 0) return spent; }
                    vil.ChangeGold(-cost);
                    int got = had - vil.Gold;
                    town.ChangeGold(got);
                    roster.AddToCounts(e, -n);
                    spent += got; left -= got; d.VBuy += got; d.VPieces += n;
                    NoteVillBought(mk, c, got);   // ZC1 (CartPriceByDestination): co wsie naprawde kupily w tym miescie - koszyk wycen wozow nastepnej doby
                    VillNeeds.Note(vil, e.Item, n);   // r4-bkwies: zapas potrzeb wsi (koszyk dnia, woz E3-W1, krok 5) - klucze wylaczone: nic
                    if (cloth) d.VClPcs += n;
                }
                if (held && left >= 1) d.VClKept += (long)left;   // K2: czesc plotna niewydana przez prog polki - zostaje w kiesie wsi (licznik)
            }
            return spent;
        }

        /// <summary>R13 (D1): oplata stala dnia wsi dla pana (VillageDuesPerHeadYear rocznie na wolna glowe, YearDays 364) - PopulationLaw zamiast renty 20%.
        /// Ulamek przechodzi na nastepna dobe (najwyzej 1 zl). 0 - wylaczone, nieskalibrowana.</summary>
        internal static int VillageFeeToday(Settlement st)
        {
            try
            {
                if (st == null || !st.IsVillage || !Live) return 0;
                var f = Get(st);
                if (f == null || !f.Cal) return 0;
                double fee = Math.Max(0f, Math.Min(50f, Settings.Current.VillageDuesPerHeadYear)) / YearDays * VillHeads(f, true) + f.VFeeAcc;
                int n = (int)Math.Floor(fee);
                f.VFeeAcc = Math.Max(0, Math.Min(1.0, fee - n));
                return n;
            }
            catch (Exception e) { Stumble("VillageFeeToday", e); return 0; }
        }

        /// <summary>PopulationLaw: oplata stala wsi zaplacona panu (licznik linii "Prawa panskie").</summary>
        internal static void NoteVillageFee(int pay) { if (pay > 0) { _win.VFee += pay; _win.VFeeN++; } }

        /// <summary>KingdomTreasury.Levies (R15a): danina wsi od kiesy ponad 15 dni zakupow (RdV x VillagePurchasePerHead x wolne glowy). 0 - wylaczone.</summary>
        internal static int VillageFloor(Settlement st)
        {
            try
            {
                if (st == null || !st.IsVillage || !Live) return 0;
                var f = Get(st);
                if (f == null || !f.Cal) return 0;
                double x = RdV * Math.Max(0f, Math.Min(1f, Settings.Current.VillagePurchasePerHead)) * VillHeads(f, true);
                return x >= int.MaxValue ? int.MaxValue : (int)x;
            }
            catch (Exception e) { Stumble("VillageFloor", e); return 0; }
        }

        /// <summary>DS (F1, DsVillage - skrzynia, czynsz): koszyk dnia wsi (VillagePurchasePerHead x wolne glowy - ta sama liczba co Villages) i czynsz staly dnia przed
        /// dekretem (VillageDuesPerHeadYear / 364 x wolne glowy - jak VillageFeeToday, bez ruszania ulamka). false - kiesa nieczynna, wies nieskalibrowana. Nie rzuca.</summary>
        internal static bool DsVillageDay(Settlement st, out double nv, out double rent)
        {
            nv = rent = 0;
            try
            {
                if (st == null || !st.IsVillage || !Live) return false;
                Folk f;
                if (!_f.TryGetValue(st, out f) || f == null || f.Kind != 2 || !f.Cal) return false;
                var s = Settings.Current;
                double h = VillHeads(f, true);
                nv = Math.Max(0f, Math.Min(1f, s.VillagePurchasePerHead)) * h;
                rent = Math.Max(0f, Math.Min(50f, s.VillageDuesPerHeadYear)) / YearDays * h;
                return !(double.IsNaN(nv) || double.IsNaN(rent));
            }
            catch (Exception e) { Stumble("DsVillageDay", e); nv = rent = 0; return false; }
        }

        // ============================================================ R5: zapasy startowe
        /// <summary>Nowa kampania (TownPurse.TrimStartGift / CastlePurse.TrimStartGift, mloda kampania): czy zapasy kies z daru.</summary>
        internal static bool SeedTownsDue { get { var s = Settings.Current; return Live && s.TownFolkStartReserves && _seedT == 0; } }
        internal static bool SeedCastlesDue { get { var s = Settings.Current; return CastleLive && s.TownFolkStartReserves && _seedC == 0; } }

        private static bool BkFor(Folk f)
        {
            if (f.Cal || f.BkCal) return !f.NoBk;
            bool ok = ReadBk(f, PopTypes(), null);
            f.BkCal = true;
            if (!ok) _sNoBk++;
            return ok;
        }

        /// <summary>Kiesy W/R/P miasta z daru: dni zapasu x koszyk KL x ludzie BK przy indeksie cen 1.0 (KL 5.3) / keep, najwyzej `gift`; przelew kasa -> kiesy.</summary>
        internal static int SeedTown(Settlement st, int gift)
        {
            try
            {
                if (gift <= 0 || st == null || !st.IsTown || !Live) return 0;
                var f = Get(st);
                if (f == null || !BkFor(f)) return 0;
                double rdW = Math.Max(1, Math.Min(60, Settings.Current.TownFolkReserveDays));
                double bR = f.HR * BR, bP = f.HP * BP;
                if (f.Slave) { double sl = f.HS * BS; bR += sl * OwnR; bP += sl * OwnP; }
                double w = rdW * f.HW * BW / KeepW, r = RdR * bR / KeepR, p = RdP * bP / KeepP, all = w + r + p;
                if (!(all > 0)) return 0;
                double k = Math.Min(1.0, gift / all);
                if (k < 1) _sShort++;
                long sw = (long)(w * k), sr = (long)(r * k), sp = (long)(p * k), tot = sw + sr + sp;
                if (tot <= 0) return 0;
                var town = st.Town;
                int had = town.Gold;
                town.ChangeGold(-(int)tot);
                long got = had - town.Gold;
                if (got < tot) { double q = got / (double)tot; sw = (long)(sw * q); sr = (long)(sr * q); sp = got - sw - sr; }
                f.KW += sw; f.KR += sr; f.KP += sp; _ctlIn += got;
                _sW += sw; _sR += sr; _sP += sp; _sTowns++; _sGift += gift;
                return (int)got;
            }
            catch (Exception e) { Stumble("SeedTown", e); return 0; }
        }

        private static Dictionary<Settlement, List<Folk>> _villBy;

        /// <summary>Wsie, ktorych targiem jest to miasto: do 15 dni zakupow ponad ich kiese, z reszty daru (kasa miasta -> kiesa wsi).</summary>
        internal static int SeedVillages(Settlement st, int left)
        {
            try
            {
                if (left <= 0 || st == null || st.Town == null || !Live) return 0;
                if (_villBy == null)
                {
                    _villBy = new Dictionary<Settlement, List<Folk>>();
                    foreach (var v in Village.All)
                    {
                        if (v == null || v.Settlement == null) continue;
                        var mk = v.TradeBound ?? v.Bound;
                        var vf = Get(v.Settlement);
                        if (mk == null || vf == null) continue;
                        List<Folk> l; if (!_villBy.TryGetValue(mk, out l)) { l = new List<Folk>(); _villBy[mk] = l; }
                        if (!l.Contains(vf)) l.Add(vf);
                    }
                }
                List<Folk> vs;
                if (!_villBy.TryGetValue(st, out vs)) return 0;
                double pph = Math.Max(0f, Math.Min(1f, Settings.Current.VillagePurchasePerHead));
                long used = 0;
                foreach (var vf in vs)
                {
                    if (left - used <= 0) break;
                    var vil = vf.St.Village;
                    if (vil == null || !BkFor(vf)) continue;
                    long want = (long)(RdV * pph * VillHeads(vf, false)) - Math.Max(0, vil.Gold);
                    long give = Math.Min(want, left - used);
                    if (give <= 0) continue;
                    int had = st.Town.Gold;
                    st.Town.ChangeGold(-(int)give);
                    int got = had - st.Town.Gold;
                    if (got <= 0) continue;
                    vil.ChangeGold(got);
                    used += got; _sV += got; _sVills++;
                }
                return (int)used;
            }
            catch (Exception e) { Stumble("SeedVillages", e); return 0; }
        }

        /// <summary>Kiesa podzamcza z daru zamku: 10 dni x 0.18 x ludzie BK podzamcza (indeks 1.0), najwyzej `gift`.</summary>
        internal static int SeedCastle(Settlement st, int gift)
        {
            try
            {
                if (gift <= 0 || st == null || !st.IsCastle || !CastleLive) return 0;
                var f = Get(st);
                if (f == null || !BkFor(f)) return 0;
                long want = (long)(RdC * f.HW * BC);
                long give = Math.Min(want, gift);
                if (give <= 0) return 0;
                if (give < want) _sShort++;
                int had = st.Town.Gold;
                st.Town.ChangeGold(-(int)give);
                long got = had - st.Town.Gold;
                f.KW += got; _ctlIn += got; _sC += got; _sCastles++; _sGift += gift;
                return (int)got;
            }
            catch (Exception e) { Stumble("SeedCastle", e); return 0; }
        }

        /// <summary>Koniec zapasow z daru (wolajacy robi migawke M20): flaga kampanii i linia jednorazowa.</summary>
        internal static void SeedDone(bool towns)
        {
            if (towns) _seedT = 1; else _seedC = 1;
            SeedLine(towns ? "z daru kas miast (nowa kampania)" : "z daru kas zamkow (nowa kampania)");
        }

        private static void SeedLine(string how)
        {
            long all = _sW + _sR + _sP + _sC + _sV;
            string l = "Kiesa ludu (E5/B1): zapasy startowe " + how + " - kiesy W " + _sW + " / R " + _sR + " / P " + _sP + ", podzamcze " + _sC + ", wsie " + _sV
                       + " (" + _sVills + " wsi) = " + all + " (miast " + _sTowns + ", zamkow " + _sCastles + "; dar do podzialu " + _sGift + ", reszta daru przycieta dalej - linie TownPurse/CastlePurse)"
                       + "; osad z darem albo nadwyzka mniejsza niz zapas " + _sShort + ", bez danych BK " + _sNoBk + " | koszyk KL przy indeksie cen 1.0 (W " + (Settings.Current != null ? Settings.Current.TownFolkReserveDays : 10)
                       + " dni, R 21, P 30, podzamcze 10, wsie 15 dni zakupow); migawka ksiegi M20 (kiesa ludu - zapasy startowe).";
            _lineSeed = _lineSeed == null ? l : _lineSeed + " || " + l;
            _sW = _sR = _sP = _sC = _sV = _sGift = 0; _sTowns = _sCastles = _sVills = _sShort = _sNoBk = 0;
        }

        /// <summary>Stary zapis / wlaczenie w trakcie (plan 6.2): kiesy z nadwyzki kas ponad zapas kupcow (D4), najwyzej polowa nadwyzki i najwyzej zapas dni (indeks 1.0);
        /// podzamcze z nadwyzki kasy zamku tak samo; wsie nic. Migawka M20.</summary>
        private static void SeedOld(LDay d)
        {
            var s = Settings.Current;
            if (_seedT != 0 && _seedC != 0) return;
            bool towns = _seedT == 0, castles = _seedC == 0 && CastleLive;
            if (!s.TownFolkStartReserves) { if (towns) _seedT = 3; if (castles) _seedC = 3; return; }
            if (!towns && !castles) return;
            foreach (var f in _f.Values)
            {
                try
                {
                    if (f.Kind == 0 && towns && f.Cal && f.St.Town != null)
                    {
                        long spare = TownPurse.Spare(f.St.Town);
                        int gift = (int)Math.Min(int.MaxValue, spare / 2);
                        if (gift > 0) d.Seed += SeedTown(f.St, gift);
                    }
                    else if (f.Kind == 1 && castles && f.Cal && f.St.Town != null)
                    {
                        long spare = (long)f.St.Town.Gold - CastlePurse.Reserve(f.St.Town);
                        int gift = (int)Math.Min(int.MaxValue, Math.Max(0, spare) / 2);
                        if (gift > 0) d.Seed += SeedCastle(f.St, gift);
                    }
                }
                catch (Exception e) { Stumble("SeedOld(osada)", e); }
            }
            if (towns) _seedT = 2;
            if (castles) _seedC = 2;
            MoneyLedger.Mark(MoneyLedger.MFolkStart);
            SeedLine("z nadwyzki kas ponad zapas kupcow (stary zapis albo kiesa wlaczona w trakcie; najwyzej polowa nadwyzki)");
        }

        private static void OrphanBack(LDay d)
        {
            foreach (var st in Settlement.All)
            {
                if (st == null || !st.IsTown || st.Town == null) continue;
                int x = (int)Math.Min(int.MaxValue, _orphan);
                st.Town.ChangeGold(x);
                _orphan -= x; d.Orphan += x; _ctlOut += x;   // posiadacz H22 -> kasa miasta (kontrola zamknieta)
                break;
            }
        }

        // ============================================================ wylaczenie, posiadacze, zapis
        internal static bool AnyPurse()
        {
            EnsureImported();
            if (_orphan != 0) return true;
            foreach (var f in _f.Values) if (f.KW != 0 || f.KR != 0 || f.KP != 0 || f.KG != 0 || f.CourtCr != 0) return true;
            return false;
        }

        /// <summary>Plan 6.3: kiesa wylaczona (TownFolkLive off, TownFolkMode 0 albo A1 off) - kiesy W/R/P i gmina do kasy miasta, podzamcze do kasy zamku, kredyt dworu 0;
        /// migawka M18 (w bloku). Ponowne wlaczenie - jak stary zapis (zapasy z nadwyzki).</summary>
        internal static void ReturnAll()
        {
            long ret = 0; int n = 0;
            foreach (var f in _f.Values)
            {
                try
                {
                    f.CourtCr = 0; f.FacOk = false; Array.Clear(f.Offer, 0, 3); Array.Clear(f.EarnWin, 0, 3); f.HungerDays = 0;   // E5/C2: licznik glodu od zera
                    long x = f.KW + f.KR + f.KP + f.KG;
                    var t = f.St != null ? f.St.Town : null;
                    if (t == null) { f.KW = f.KR = f.KP = f.KG = 0; continue; }
                    if (x > 0) { t.ChangeGold((int)Math.Min(int.MaxValue, x)); ret += x; n++; }
                    f.KW = f.KR = f.KP = f.KG = 0;
                }
                catch (Exception e) { Stumble("ReturnAll", e); }
            }
            if (_orphan > 0) { foreach (var st in Settlement.All) if (st != null && st.IsTown && st.Town != null) { st.Town.ChangeGold((int)Math.Min(int.MaxValue, _orphan)); ret += _orphan; _orphan = 0; break; } }
            _seedT = _seedC = 0; _ctlStock0 = long.MinValue; _ctlIn = _ctlOut = 0; _win = new LWin();
            MoneyLedger.Mark(MoneyLedger.MFolk);
            if (ret > 0) _lineRet = "Kiesa ludu (E5/B1): WYLACZONA w trakcie - kiesy ludu wrocily do kas osad: " + ret + " zl z " + n + " osad (migawka M18); kredyt dworu wyzerowany; ponowne wlaczenie - zapasy z nadwyzki jak stary zapis.";
        }

        /// <summary>Ksiega pieniadza H22 (kiesy ludu miast: W/R/P + kiesy osad nieznanych przy wczytaniu), H23 (podzamcza), H24 (gminy) - prawdziwe zloto niezaleznie od klucza.</summary>
        internal static long TotalTown()
        {
            if (_pending != null) EnsureImported();
            long t = _orphan;
            for (int i = 0; i < _fk.Count; i++) { var f = _fk[i]; if (f.Kind == 0) t += f.KW + f.KR + f.KP; }
            return t;
        }
        internal static long TotalCastle() { long t = 0; for (int i = 0; i < _fk.Count; i++) { var f = _fk[i]; if (f.Kind == 1) t += f.KW; } return t; }
        internal static long TotalGmina() { long t = 0; for (int i = 0; i < _fk.Count; i++) { var f = _fk[i]; if (f.Kind == 0) t += f.KG; } return t; }

        private static string D(double v) { return double.IsNaN(v) || double.IsInfinity(v) ? "0" : v.ToString("R", Inv); }

        /// <summary>Zapis arm_townfolk: "v1;seed=TC" + na osade "id,rodzaj,cal,HW,HR,HP,HS,PeopleCal,sl,KW,KR,KP,KG,CourtCr,Idx,PredFood,PredRest,HungerDays" (|; pole 17 - E5/C2, starszy zapis bez niego = 0, starsze DLL je pomijaja; pole 18 - K1-3a krok 2: licznik koni roboczych HorseOwed, starszy zapis bez niego = 0). Pusty przy kiesie wylaczonej i pustych kiesach.</summary>
        internal static string Export()
        {
            try
            {
                if (_pending != null) EnsureImported();
                if (!LiveKey && !AnyPurse()) return "";
                var sb = new StringBuilder(64 * 1024);
                sb.Append("v1;seed=").Append(_seedT).Append(_seedC).Append(";orphan=").Append(_orphan);
                foreach (var f in _f.Values)
                {
                    if (f.St == null || f.St.StringId == null) continue;
                    bool money = f.KW != 0 || f.KR != 0 || f.KP != 0 || f.KG != 0 || f.CourtCr != 0;
                    if (!f.Cal && !money) continue;
                    sb.Append('|').Append(f.St.StringId.Replace("|", "").Replace(",", "")).Append(',').Append(f.Kind).Append(',').Append(f.Cal ? 1 : 0).Append(',')
                      .Append(D(f.HW)).Append(',').Append(D(f.HR)).Append(',').Append(D(f.HP)).Append(',').Append(D(f.HS)).Append(',').Append(D(f.PeopleCal)).Append(',').Append(f.Slave ? 1 : 0).Append(',')
                      .Append(f.KW).Append(',').Append(f.KR).Append(',').Append(f.KP).Append(',').Append(f.KG).Append(',').Append(f.CourtCr).Append(',')
                      .Append(D(f.Idx)).Append(',').Append(D(f.PredFood)).Append(',').Append(D(f.PredRest)).Append(',').Append(f.HungerDays)
                      .Append(',').Append(Math.Max(0, Math.Min(WhMax, f.HorseOwed)).ToString("0.####", Inv));   // K1-3a krok 2: pole 18
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        internal static void Import(string data)
        {
            _pending = string.IsNullOrEmpty(data) ? null : data;
        }

        private static void EnsureImported()
        {
            var data = _pending;
            if (data == null) return;
            _pending = null;
            try
            {
                var parts = data.Split('|');
                var head = parts[0].Split(';');
                foreach (var h in head)
                {
                    if (h.StartsWith("seed=", StringComparison.Ordinal) && h.Length >= 7) { _seedT = h[5] - '0'; _seedC = h[6] - '0'; if (_seedT < 0 || _seedT > 3) _seedT = 0; if (_seedC < 0 || _seedC > 3) _seedC = 0; }
                    else if (h.StartsWith("orphan=", StringComparison.Ordinal)) { long o; if (long.TryParse(h.Substring(7), NumberStyles.Integer, Inv, out o)) _orphan += Math.Max(0, o); }
                }
                int lost = 0;
                for (int i = 1; i < parts.Length; i++)
                {
                    try
                    {
                        var a = parts[i].Split(',');
                        if (a.Length < 17) continue;
                        var st = Settlement.Find(a[0]);
                        long kw = L(a[9]), kr = L(a[10]), kp = L(a[11]), kg = L(a[12]), cc = L(a[13]);
                        var f = st != null ? Get(st) : null;
                        if (f == null || f.Kind.ToString(Inv) != a[1] || (f.Kind == 2 && kw + kr + kp + kg != 0))
                        {
                            // osada nieznana albo innego rodzaju: kiesy do kasy osady (gdy jest miastem/zamkiem) albo do najblizszej doby - nic w nicosc
                            long x = Math.Max(0, kw) + Math.Max(0, kr) + Math.Max(0, kp) + Math.Max(0, kg);
                            if (x > 0) { _orphan += x; lost++; }
                            continue;
                        }
                        f.Cal = a[2] == "1";
                        f.HW = Dd(a[3]); f.HR = Dd(a[4]); f.HP = Dd(a[5]); f.HS = Dd(a[6]); f.PeopleCal = Dd(a[7]); f.Slave = a[8] == "1";
                        f.KW = kw; f.KR = kr; f.KP = kp; f.KG = kg; f.CourtCr = Math.Max(0, cc);
                        double idx = Dd(a[14]); if (idx > 0) f.Idx = idx;
                        f.PredFood = Math.Max(0, Dd(a[15])); f.PredRest = Math.Max(0, Dd(a[16]));
                        f.HungerDays = a.Length > 17 ? (int)Math.Max(0, Math.Min(100000, L(a[17]))) : 0;   // E5/C2: zapis sprzed C2 - licznik od zera
                        f.HorseOwed = a.Length > 18 ? Math.Max(0, Math.Min(WhMax, Dd(a[18]))) : 0;   // K1-3a krok 2: zapis sprzed paczki - licznik koni roboczych od zera
                        f.FacOk = false;
                    }
                    catch (Exception e) { Stumble("Import(osada)", e); }
                }
                if (lost > 0) Log.Info("Kiesa ludu (E5/B1): zapis - kiesy " + lost + " osad nieznanych w swiecie (" + _orphan + " zl) pojda do kasy miasta w najblizszej dobie (nic w nicosc).");
            }
            catch (Exception e) { Stumble("Import", e); }
        }

        private static long L(string s) { long v; return long.TryParse(s, NumberStyles.Integer, Inv, out v) ? v : 0; }
        private static double Dd(string s) { double v; return double.TryParse(s, NumberStyles.Float, Inv, out v) && !double.IsNaN(v) && !double.IsInfinity(v) ? v : 0; }

        // ============================================================ linie (TownFolk.Lines po ksiedze pieniadza)
        /// <summary>Linia stanu raz na sesje i przy kazdej zmianie (czynna / nieczynna z powodem) - stawki, przelaczniki, klucze B (P K3).</summary>
        private static void StateLine(bool live)
        {
            var s = Settings.Current;
            if (s == null) return;
            string why = live ? "" : !s.TownFolkLive ? "TownFolkLive 0" : s.TownFolkMode < 1 ? "TownFolkMode 0" : !TownPurse.NoMintNow ? "A1 nie cofa \"zakupow\" (Town Purse Regulator 0, latki 110 albo Town Folk Buy Without Minting)" : "kampania przed GameStarted";
            string key = live ? "on|" + (s.CastlePurseEnabled ? 1 : 0) : "off|" + why;
            if (key == _stateKey) return;
            _stateKey = key;
            if (_better == null) _better = CatsOf(BetterIds, out _betterText);
            if (_villCats == null) _villCats = CatsOf(VillIds, out _villText);
            int slaves = 0; foreach (var f in _f.Values) if (f.Slave && f.Kind < 2) slaves++;
            var sb = new StringBuilder(1500);
            sb.Append("Kiesa ludu (E5/B1): ").Append(live ? "CZYNNA" : "NIECZYNNA (" + why + ")").Append(" (TownFolkLive ").Append(s.TownFolkLive ? 1 : 0).Append(", TownFolkMode ").Append(s.TownFolkMode).Append(")")
              .Append(" | A1: ").Append(TownPurse.NoMintNow ? "tak" : "nie").Append(" | zamki (110): ").Append(s.CastlePurseEnabled ? "tak" : "nie")
              .Append(" | stawki: uslugi ").Append(F2(Unit(s.TownFolkServiceShare))).Append(", dwor ").Append(F2(Unit(s.CourtFolkShare))).Append(", robocizna ").Append(F2(Unit(s.TownFolkLabourFood)))
              .Append("/").Append(F2(Unit(s.TownFolkLabourGoods))).Append("/").Append(F2(Unit(s.TownFolkLabourArms))).Append(", place rzemiosla ").Append(F2(Unit(s.TownFolkCraftWageShare)))
              .Append(", obrot P ").Append(s.TownFolkMerchantDraw.ToString("0.000", Inv)).Append(", zawor miast ").Append(F2(Unit(s.TownFolkMerchantProfitShare))).Append(", zawor zamkow ")
              .Append(F2(Unit(s.TownFolkCastleProfitShare))).Append(", akcyza ").Append(s.TownFolkExcise.ToString("0.000", Inv)).Append(", zapas W ").Append(s.TownFolkReserveDays).Append(" dni")
              .Append(", wies: pan x").Append(F2(Unit(s.VillageLordScale))).Append(", zakupy ").Append(s.VillagePurchasePerHead.ToString("0.00", Inv)).Append(" zl/glowe/d, oplaty ")
              .Append(s.VillageDuesPerHeadYear.ToString("0.0", Inv)).Append(" zl/glowe/rok")
              .Append(" | decyzja 12.10: dopisek pana z utargu wsi ").Append(F2(Unit(s.VillageLordExtraShare)))
              .Append(s.FolkLuxuryList ? ", tempo W 1/" + LxDays(s).ToString("0", Inv) + " (P3), place R/P " : ", tempo nadwyzki ").Append(F2(SpendRate(s)))   // m103 P1: przy P3 oferta W = 1/FolkSurplusSpendDays (LadderOffers), 0.15 to juz tylko place R/P
              .Append("/d (podzamcze ").Append(F2(SurplusRate)).Append("), dziesiecina ").Append(TitheKey ? "tak (wsie " + F2(TitheVillage(s)) + " utargu, lud W/R/P 1/2/3%)" : "nie")
              .Append(", kaplani BK: ").Append(TitheBkState())
              .Append(" | kotwica: ").Append(s.TownFolkAnchorBasket ? "tak" : "nie").Append(", kuchnia pana: ").Append(s.TownFolkCastleCourtFeeds ? "tak" : "nie")
              .Append(", R14: ").Append(s.TownFolkSatisfactionFromBasket ? "tak" : "nie").Append(", zapasy startowe: ").Append(s.TownFolkStartReserves ? "tak" : "nie")
              .Append(" | niewolni: kultury osad z listy I1 (").Append(slaves).Append(" miast i zamkow)")
              .Append(" | kategorie \"lepsze\": ").Append(_betterText).Append(", zakupy wsi: ").Append(_villText)
              // scalenie B (krok E5/B3): wartosci CZYNNE (LordsEvenUp - z wyrownaniem 0.5 i 2/1.5/0.25, bez - stare klucze), nie same klucze
              .Append(" | klucze B: TownRentLordShare ").Append(F2(LordsEvenUp.TownLordShare(s))).Append(" (wyrownanie E5/B3 CastleLordsEvenUp ").Append(LordsEvenUp.Live ? "czynne" : s.CastleLordsEvenUp ? "nieczynne" : "wylaczone")
              .Append("), CrownRentWeight Town/Castle/Village ").Append(LordsEvenUp.WeightTown(s).ToString("0.##", Inv))
              .Append("/").Append(LordsEvenUp.WeightCastle(s).ToString("0.##", Inv)).Append("/").Append(LordsEvenUp.WeightVillage(s).ToString("0.##", Inv))
              .Append(", WarTaxFromFolk (E5/B2) ").Append(s.WarTaxFromFolk ? "ON" : "OFF")
              .Append(", ShipsToPort/ShipTradeThroughPort/ShipPrizesToPort ").Append(s.ShipsToPort ? "ON" : "OFF").Append("/").Append(s.ShipTradeThroughPort ? "ON" : "OFF").Append("/").Append(s.ShipPrizesToPort ? "ON" : "OFF")
              .Append(" | zapasy startowe (flaga kampanii) miasta/zamki ").Append(_seedT).Append("/").Append(_seedC).Append(" (0 jeszcze nie, 1 z daru, 2 z nadwyzki, 3 wylaczone).");
            _lineState = sb.ToString();
        }

        /// <summary>Daily przy kiesie nieczynnej: linia stanu przy zmianie (tylko gdy klucz albo zapis w ogole istnieje).</summary>
        private static void LiveOffLine()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null || !Campaign.Current.GameStarted) return;
            if (_stateKey == null && !s.TownFolkLive) return;
            StateLine(false);
        }

        private static string P1(double part, double whole) { return whole > 0 ? (100.0 * part / whole).ToString("0.0", Inv) + "%" : "-"; }

        private static void BuildLines(int day, LDay d, LWin w, bool partial, bool csvDay)
        {
            long paidT = w.PaidW + w.PaidR + w.PaidP;
            long gameT = w.MadeT, gameC = w.MadeC;
            long unpaid = w.UnpaidT + w.UnpaidC;
            long e = 0; for (int k = 0; k < EKinds; k++) e += w.Earn[k];
            long army = w.Earn[KLife] + w.Earn[KLifeCaravan] + w.Earn[KLifeBand] + w.Earn[KGarrison];
            var sb = new StringBuilder(4096);
            sb.Append("Kiesa ludu: dzien ").Append(day).Append(partial ? " | doba niepelna (pierwsza w sesji)" : "").Append(" | CZYNNA")
              .Append(" | miasta ").Append(d.Towns).Append(": ludzie ksiegi ").Append(N0(d.People)).Append(" (BK z kalibracji x ksiega: W ").Append(N0(d.HW)).Append(" / R ").Append(N0(d.HR))
              .Append(" / P ").Append(N0(d.HP)).Append(d.HS > 0 ? " / niewolni utrzymywani " + N0(d.HS) : "").Append(")")
              .Append(", koszyk ludu ").Append(N0(d.B)).Append(", wydatek gry ").Append(N0(d.Wg)).Append(" (jedzenie ").Append(N0(d.Wf)).Append("; bez kotwicy na jutro ").Append(N0(d.Wp)).Append(")")
              .Append(", budzet gry ").Append(N0(d.G0)).Append(" (po kotwicy i ofercie nadwyzki ").Append(N0(d.G0a)).Append(")")
              .Append(" | kotwica: m srednio ").Append(d.MN > 0 ? F2(d.MSum / d.MN) : "-").Append(" (min ").Append(F2(d.MMin)).Append(", miast z m < 1: ").Append(d.MLow).Append(")")
              .Append(", czynnik jedzenie / reszta srednio ").Append(d.Towns > 0 ? F2(d.FfSum / d.Towns) : "-").Append(" / ").Append(d.Towns > 0 ? F2(d.FrSum / d.Towns) : "-")
              .Append(" (min ").Append(F2(d.FfMin)).Append(" / ").Append(F2(d.FrMin)).Append("), miast z czynnikiem < 1: ").Append(d.FacLow)
              .Append(" | zaplacone przez lud miast ").Append(paidT).Append(" (W ").Append(w.PaidW).Append(" / R ").Append(w.PaidR).Append(" / P ").Append(w.PaidP).Append("; akcyza do gmin ").Append(w.Excise).Append(")")
              .Append(", zamki ").Append(w.PaidC).Append(" (pokryte dworem ").Append(w.Court).Append(")")
              .Append(", zjedzone bez zaplaty ").Append(unpaid).Append(" (").Append((gameT + gameC > 0 ? 100.0 * unpaid / (gameT + gameC) : 0).ToString("0.00", Inv)).Append("% wydatku gry; miasta ").Append(w.UnpaidT)
              .Append(", zamki ").Append(w.UnpaidC).Append("; dokup jedzenia z gminy ").Append(w.GmFood).Append(")")
              .Append(", wydatek gry w konsumpcji: miasta ").Append(gameT).Append(" (").Append(w.Ticks).Append(" tickow), zamki ").Append(gameC).Append(" (").Append(w.TicksC).Append(" tickow), A1 cofnelo calosc (B1 nie obsluzylo) ").Append(w.Fallback)
              .Append(" | dochod ludu miast z zewnatrz i z kas ").Append(e).Append(": wojsko ").Append(army).Append(" (zycie ").Append(w.Earn[KLife]).Append(", karawany ").Append(w.Earn[KLifeCaravan])
              .Append(", bandy ").Append(w.Earn[KLifeBand]).Append(", zalogi ").Append(w.Earn[KGarrison]).Append("), dwor ").Append(w.Earn[KCourt])
              .Append(", budowy ").Append(w.Earn[KBuildLabour]).Append(", kopalnie ").Append(w.Earn[KMine]).Append(", place warsztatow ").Append(w.Earn[KCraftPaid]).Append(" (z kiesy gracza ").Append(w.FromTown).Append(")")
              .Append(", 5% zaworu ").Append(w.ValveT).Append(", w tym praca niewolnych do wlascicieli ").Append(w.SlaveW)
              .Append("; dzis z kas: robocizna ").Append(d.LabT).Append(SkupText(d.LabS, d.Skup)).Append(", obrot P ").Append(d.TurnT).Append(", doplata do 35% ").Append(d.TopT).Append(" (miast przycietych do stanu kasy ").Append(d.Cut).Append(")")
              .Append(" | wewnatrz ludu: czynsz ").Append(d.Rent).Append(" (czynsz gruntowy do panow miast E3-G4 ").Append(GroundRentOn ? d.GroundRent.ToString(Inv) : "wylaczony").Append(')').Append(", leczenie ").Append(d.Heal).Append(", sluzba ").Append(d.Serv).Append(", place z nadwyzki R/P ").Append(d.SurpWage)
              .Append(", roboty gminy (place) ").Append(d.GmWage).Append(WorksRoute ? " (P2: place robot gminy w linii Roboty ludu)" : "")
              .Append(" | podatki panu: W ").Append(d.TaxW).Append(" / R ").Append(d.TaxR).Append(" / P ").Append(d.TaxP).Append(", cech ").Append(d.Guild).Append(", farma gmin ").Append(d.Farm)
              .Append(", wstrzymane w oblezeniu ").Append(d.Held).Append(", bez zywego pana (do gminy) ").Append(d.NoLord)
              .Append(" | kiesy ").Append(d.KW + d.KR + d.KP).Append(" (W ").Append(d.KW).Append(" / R ").Append(d.KR).Append(" / P ").Append(d.KP).Append("), gminy ").Append(d.KG)
              .Append(", zapas dni W ").Append(F1(d.NW > 0 ? d.KW / d.NW : double.NaN)).Append(" / R ").Append(F1(d.NR > 0 ? d.KR / d.NR : double.NaN)).Append(" / P ").Append(F1(d.NP > 0 ? d.KP / d.NP : double.NaN))
              .Append(", lokata (kiesa W ponad zapas dni) ").Append(N0(d.Lokata)).Append(", oferta nadwyzki na jutro ").Append(d.OffT)
              .Append(LadderOn ? " (tempo W 1/" + LxDays(Settings.Current).ToString("0", Inv) + " (P3), place R/P " : " (tempo ").Append(F2(SpendRate(Settings.Current))).Append("/d ponad zapas)")   // m103 P1: przy P3 0.15 to tylko place R/P
              .Append(LadderOn ? " (P3: oferta W 1/" + LxDays(Settings.Current).ToString("0", Inv) + ", R i P " + (1 - SurpWage).ToString("0.0", Inv) + "/" + LxDays(Settings.Current).ToString("0", Inv) + " - linia Zbytki)" : "")
              .Append(" | stac jedzenie W/R/P ").Append(Pct(d.AFW, d.NFW)).Append(" / ").Append(Pct(d.AFR, d.NFR)).Append(" / ").Append(Pct(d.AFP, d.NFP))
              .Append(", pelny W/R/P ").Append(Pct(d.AW, d.NW)).Append(" / ").Append(Pct(d.AR, d.NR)).Append(" / ").Append(Pct(d.AP, d.NP))
              .Append(" (swiat ").Append(Pct(d.AW + d.AR + d.AP, d.NW + d.NR + d.NP)).Append(")")   // recenzja B1 (1-6): prog B1-T8 "swiat >= 98%" (waga potrzeb)
              .Append(", miast ze stac jedzenie < 95%: ").Append(d.NFood).Append(", pelny < 95%: ").Append(d.NFull)
              .Append(" | podzamcze ").Append(d.Castles).Append(": ludzie ").Append(N0(d.CHeads)).Append(", kiesa ").Append(d.KC).Append(", dochod ").Append(w.EarnC)
              .Append(" (15% zaworu ").Append(w.ValveC).Append("), robocizna z kas zamkow ").Append(d.LabC).Append(", zaplacone ").Append(w.PaidC).Append(" (pokryte dworem ").Append(w.Court)
              .Append("), kredyt dworu ").Append(d.CCr).Append(" (przyznany ").Append(w.CourtGrant).Append("; zamkow bez kredytu ").Append(d.CNoCourt).Append("), stac ").Append(P1(d.CAff, d.CNeed))
              .Append(", wydatek gry zamkow ").Append(N0(d.CWg))
              .Append(" | wsie ").Append(d.Villages).Append(": zakupy ").Append(d.VBuy).Append(" z ").Append(N0(d.VNeed)).Append(" (").Append(P1(d.VBuy, d.VNeed)).Append("; chciane z nadwyzka ").Append(N0(d.VWant))
              .Append("; sztuk ").Append(d.VPieces).Append(", wsi bez towaru ").Append(d.VNoGoods).Append(", bez miasta targowego ").Append(d.NoTrade).Append(", nieskalibrowanych ").Append(d.VNoCal).Append(")")
              .Append(", oplaty stale ").Append(w.VFee).Append(" (").Append(w.VFeeN).Append(" wsi)")
              // K2 (Towary 2): plotno w koszyku dnia wsi tylko ponad prog polki miasta targowego
              .Append(", plotno wsi ponad zapas polki (VillageClothAboveReserve) ").Append(VillClothOn ? "tak" : "nie").Append(": wierszy zatrzymanych progiem ").Append(d.VClHeld)
              .Append(", zostalo w kiesach wsi ").Append(d.VClKept).Append(" zl, kupione plotno ").Append(d.VClPcs).Append(" szt. (prog polki srednio ")
              .Append(d.VClRows > 0 ? ((double)d.VClCap / d.VClRows).ToString("0.0", Inv) : "-").Append(" szt.)")
              .Append(W1Note(d))
              .Append(" | zapasy z nadwyzki dzis ").Append(d.Seed).Append(d.Orphan > 0 ? ", kiesy osad nieznanych do kasy " + d.Orphan : "")
              // P1: nowy rodzaj wydatku kies (w kontroli nizej siedzi w "wydatki") - danina z oszczednosci do skarbcow krolestw; kiesa ponad zapas dni przed / po daninie
              .Append(" | danina z oszczednosci do korony (P1) ").Append(d.Aid[0] + d.Aid[1] + d.Aid[2] + d.Aid[3]).Append(" (W ").Append(d.Aid[0]).Append(" / R ").Append(d.Aid[1])
              .Append(" / P ").Append(d.Aid[2]).Append("; podzamcza ").Append(d.Aid[3]).Append("), kiesa ponad zapas dni przed / po: W ").Append(N0(d.Ov0[0])).Append(" / ").Append(N0(d.Ov1[0]))
              .Append(", R ").Append(N0(d.Ov0[1])).Append(" / ").Append(N0(d.Ov1[1])).Append(", P ").Append(N0(d.Ov0[2])).Append(" / ").Append(N0(d.Ov1[2]))
              .Append(", podzamcza ").Append(N0(d.Ov0[3])).Append(" / ").Append(N0(d.Ov1[3]))
              .Append(" | kontrola: zmiana kies ").Append(_lastDStock).Append(" = wplywy ").Append(_lastIn).Append(" - wydatki ").Append(_lastOut).Append(", r = ").Append(double.IsNaN(_lastR) ? "-" : N0(_lastR));
            _lineFolk = sb.ToString();
            _lineFolkPartial = partial;
            double sale = d.BaseFolk + d.BaseVill + d.BaseLife + d.BaseCourt + d.BaseSale + d.BaseCar + d.BaseGm;
            _lineLab = "Podstawa robocizny (E5/B1): dzien " + day + " | sprzedaz kupcow " + N0(sale) + ": towar ludu " + N0(d.BaseFolk) + ", wsie " + N0(d.BaseVill) + ", zolnierze i zalogi (75%) " + N0(d.BaseLife)
                       + ", dwor (80%) " + N0(d.BaseCourt) + ", sprzet AI, odziez, budowy, zamki (Sale) " + N0(d.BaseSale) + ", karawany i gracz " + N0(d.BaseCar) + ", gmina " + N0(d.BaseGm)
                       + " | robocizna " + d.LabT + " (jedzenie " + N0(d.LabF) + " / towar " + N0(d.LabG) + " / bron " + N0(d.LabA) + " przed przycieciem do kas), obrot P " + d.TurnT
                       + ", doplata do 35% " + d.TopT + " | zamki: robocizna " + d.LabC + ".";
            _lineRights = "Prawa panskie (E5/B1): dzien " + day + " | od zarobku ludu miast " + (d.TaxW + d.TaxR + d.TaxP) + " (W " + d.TaxW + " / R " + d.TaxR + " / P " + d.TaxP + "), podzamcza " + d.TaxC
                          + ", farma gmin " + d.Farm + ", oplaty stale wsi " + w.VFee + " (" + w.VFeeN + " wsi); gracz " + d.Player + "; wstrzymane w oblezeniu " + d.Held
                          + "; bez zywego pana do gminy " + d.NoLord + " (" + d.NoLordN + ") | podatek wsi BK x VillageLordScale + VillageLordExtraShare utargu: z " + w.VTaxBase + " zostalo wsiom " + w.VTaxCut
                          + " (po odjeciu dopisku pana z utargu " + w.VTaxAdd + ").";
            _lineTithe = TitheLine(day, d);
            _lineR14 = "Zadowolenie ludu (R14): dzien " + day + " | korekty " + (w.R14Food + w.R14Rest) + " (Food " + w.R14Food + " / reszta " + w.R14Rest + "), BK sam " + w.R14Bk.ToString("0.000", Inv)
                       + " (dodatnia delta BK zastapiona); osad z Afford < 0.98 na jutro: jedzenie " + CountAff(true) + ", reszta " + CountAff(false) + ".";
            if (csvDay)
            {
                var csv = new StringBuilder(64 * 1024);
                foreach (var f in _f.Values)
                {
                    if (f.Kind > 1 || !f.Cal) continue;
                    try { CsvRowLive(csv, day, f); } catch (Exception x) { Stumble("CsvLive", x); }
                }
                if (csv.Length > 0) { try { Log.Csv("kiesa-ludu.csv", CsvHead, csv.ToString()); } catch (Exception x) { Stumble("Csv", x); } }
            }
            foreach (var f in _f.Values) { f.LDayPaid = f.LDayUnpaid = f.LDayCourt = f.LDayIn = f.LDayOut = 0; }
        }

        private static int CountAff(bool food)
        {
            int n = 0;
            foreach (var f in _f.Values) if (f.Kind < 2 && f.FacOk && (food ? f.AffFood : f.AffRest) < 0.98) n++;
            return n;
        }

        // ============================================================ ZC0 (DailyLife): odczyty kiesy ludu do pomiaru Dostatku - bez Get, bez zlota
        /// <summary>ZC0 (DailyLife, pomiar Dostatku - tylko odczyt, bez Get): zarobek okna klas ostatniej doby kiesy czynnej NETTO od podatku pana, cechu R i dziesieciny
        /// (stawki tej klasy: W 1% + 1%, R 2% + 1% + 2%, P 3% + 3%; podzamcze 3%) w zl gry: net[0..2] = W, R, P (zamek - [0] podzamcze). kind: 0 miasto, 1 zamek.
        /// false - kiesa nieczynna, osada spoza kiesy, wies, zamek przy wylaczonym zaworze zamkow albo liczby nie z tej doby. Nie rzuca.</summary>
        internal static bool ZcEarnNet(Settlement st, double[] net, out int kind)
        {
            kind = -1;
            if (net == null || net.Length < 3) return false;
            net[0] = net[1] = net[2] = 0;
            try
            {
                if (st == null || !Live) return false;
                Folk f;
                if (!_f.TryGetValue(st, out f) || f == null || f.Kind < 0 || f.Kind > 1 || f.ZcDay < 0 || f.ZcDay != _liveDay) return false;
                if (f.Kind == 1 && !CastleLive) return false;
                kind = f.Kind;
                if (f.Kind == 1) { net[0] = Math.Max(0, f.ZcE[0]) * (1 - TaxC); return true; }
                net[0] = Math.Max(0, f.ZcE[0]) * (1 - TaxW - TitheW); net[1] = Math.Max(0, f.ZcE[1]) * (1 - TaxR - GuildR - TitheR); net[2] = Math.Max(0, f.ZcE[2]) * (1 - TaxP - TitheP);
                return true;
            }
            catch { net[0] = net[1] = net[2] = 0; kind = -1; return false; }
        }

        /// <summary>ZC0 (DailyLife, pochylenie pasm stanow klasami BK - tylko odczyt, bez Get): glowy BK osady z kalibracji kiesy ludu h[0..3] = W (chlopi i dzierzawcy),
        /// R (rzemieslnicy), P (szlachta BK = patrycjat), S (niewolni). false - osada bez kalibracji albo spoza kiesy. Nie rzuca.</summary>
        internal static bool ZcBkHeads(Settlement st, double[] h)
        {
            if (h == null || h.Length < 4) return false;
            h[0] = h[1] = h[2] = h[3] = 0;
            try
            {
                Folk f;
                if (st == null || !_f.TryGetValue(st, out f) || f == null || !f.Cal || f.NoBk) return false;
                h[0] = Math.Max(0, f.HW); h[1] = Math.Max(0, f.HR); h[2] = Math.Max(0, f.HP); h[3] = Math.Max(0, f.HS);
                return h[0] + h[1] + h[2] + h[3] > 0;
            }
            catch { h[0] = h[1] = h[2] = h[3] = 0; return false; }
        }

        /// <summary>ZC0 (DailyLife, Dostatek wsi - tylko odczyt, bez Get): dziesiecina, ktora wyszla z kiesy wsi w tej dobie kiesy (czesc budowy septow; jalmuzna
        /// i dziesiecina bez kaplana zostaja w kiesie wsi) w zl gry. 0 - wies bez dziesieciny tej doby, kiesa nieczynna albo osada spoza kiesy.</summary>
        internal static double ZcVillageTithe(Settlement st)
        {
            Folk f;
            if (st == null || !Live || _liveDay < 0 || !_f.TryGetValue(st, out f) || f == null || f.Kind != 2 || f.ZcTiDay != _liveDay) return 0;
            return Math.Max(0, f.ZcTiV);
        }

        // ============================================================ ZC6 (DailyLife.Mobility.cs): awans i spadek stanow - kiesy W/R/P miasta (pieniadze ida z ludzmi, kupno ziemi)
        /// <summary>ZC6: stan kies ludu miasta (kiesa czynna) k[0..2] = W, R, P w zl gry - tylko odczyt, bez Get (nie tworzy wpisu). false - kiesa nieczynna, nie miasto,
        /// osada spoza kiesy. Nie rzuca.</summary>
        internal static bool ZcPurses(Settlement st, long[] k)
        {
            if (k == null || k.Length < 3) return false;
            k[0] = k[1] = k[2] = 0;
            try
            {
                if (st == null || !Live) return false;
                if (_pending != null) EnsureImported();
                Folk f;
                if (!_f.TryGetValue(st, out f) || f == null || f.Kind != 0) return false;
                k[0] = Math.Max(0, f.KW); k[1] = Math.Max(0, f.KR); k[2] = Math.Max(0, f.KP);
                return true;
            }
            catch { k[0] = k[1] = k[2] = 0; return false; }
        }

        /// <summary>ZC6: ludzie przechodza miedzy stanami miasta z oszczednosciami - przelew kiesa `from` -> kiesa `to` (0 W, 1 R, 2 P) najwyzej do stanu kiesy platnika,
        /// BEZ zarobku okna (to nie zarobek - podatki panu go nie widza). Wewnatrz ludu osady: suma kies i kontrola zamknieta (wplywy - wydatki) bez zmian. Zwraca przeniesione.</summary>
        internal static long ZcCarry(Settlement st, int from, int to, long x)
        {
            try
            {
                if (x <= 0 || st == null || !Live || OffMain() || from < 0 || from > 2 || to < 0 || to > 2 || from == to) return 0;
                if (_pending != null) EnsureImported();
                Folk f;
                if (!_f.TryGetValue(st, out f) || f == null || f.Kind != 0) return 0;
                long y = Math.Min(x, PurseOf(f, from));
                if (y <= 0) return 0;
                AddPurse(f, from, -y); AddPurse(f, to, y);
                return y;
            }
            catch (Exception e) { Stumble("ZcCarry", e); return 0; }
        }

        /// <summary>ZC6: kupno ziemi - kiesa P miasta -> glowa rodu pana miasta (ChangeHeroGold w try: wyjatek - nic nie schodzi), najwyzej do stanu kiesy P; bez zywego pana
        /// i w oblezeniu - nic (kupna nie ma). Kiesa -> pan jak PayLord (_ctlOut, LDayOut, okna obiegu), ale BEZ RentToday: to jednorazowa sprzedaz ziemi, nie renta
        /// (D "ziemia", budowy i powinnosci jej nie licza). Zwraca zaplacone; lord - kto dostal.</summary>
        internal static long ZcPayLand(Settlement st, long x, out Hero lord)
        {
            lord = null;
            try
            {
                if (x <= 0 || st == null || !Live || OffMain() || st.IsUnderSiege) return 0;
                if (_pending != null) EnsureImported();
                Folk f;
                if (!_f.TryGetValue(st, out f) || f == null || f.Kind != 0) return 0;
                long y = Math.Min(x, PurseOf(f, 2));
                if (y <= 0) return 0;
                var clan = st.OwnerClan;
                var h = clan != null ? clan.Leader : null;
                if (h == null || !h.IsAlive) return 0;
                int ix = (int)Math.Min(int.MaxValue, y);
                try { h.ChangeHeroGold(ix); } catch (Exception e) { Stumble("ZcPayLand", e); return 0; }
                AddPurse(f, 2, -ix);
                _ctlOut += ix; f.LDayOut += ix;
                CirculationWindows.NoteHeroGold(h, ix);
                lord = h;
                return ix;
            }
            catch (Exception e) { Stumble("ZcPayLand", e); return 0; }
        }

        /// <summary>ZC6 (recenzja 16.10): odkup ziemi przy spadku szlachty miasta ("sprzedane majatki") - glowa rodu pana miasta -> kiesa P, najwyzej `lordShare` kiesy pana
        /// (ChangeHeroGold w try: wyjatek - nic nie wchodzi); bez zywego pana i w oblezeniu - nic. Pan -> kiesa jak splata do kiesy (_ctlIn, LDayIn, okna obiegu). Zwraca
        /// zaplacone; lord - kto zaplacil.</summary>
        internal static long ZcLandBack(Settlement st, long x, double lordShare, out Hero lord)
        {
            lord = null;
            try
            {
                if (x <= 0 || st == null || !Live || OffMain() || st.IsUnderSiege || !(lordShare > 0)) return 0;
                if (_pending != null) EnsureImported();
                Folk f;
                if (!_f.TryGetValue(st, out f) || f == null || f.Kind != 0) return 0;
                var clan = st.OwnerClan;
                var h = clan != null ? clan.Leader : null;
                if (h == null || !h.IsAlive) return 0;
                long cap = (long)Math.Floor(Math.Max(0, h.Gold) * Math.Min(1.0, lordShare));
                long y = Math.Min(x, cap);
                if (y <= 0) return 0;
                int ix = (int)Math.Min(int.MaxValue, y);
                try { h.ChangeHeroGold(-ix); } catch (Exception e) { Stumble("ZcLandBack", e); return 0; }
                AddPurse(f, 2, ix);
                _ctlIn += ix; f.LDayIn += ix;
                CirculationWindows.NoteHeroGold(h, -ix);
                lord = h;
                return ix;
            }
            catch (Exception e) { Stumble("ZcLandBack", e); return 0; }
        }

        // ============================================================ E5/C4 (205, PLAN 5.13): zdanie "czy ludzi stac na chleb" w oknie People of this land
        /// <summary>E5/C4: zdanie do okna "People of this land" (PeopleMenu) dla osady, w ktorej stoi gracz - liczby kiesy czynnej z ostatniej doby tej osady:
        /// miasto i zamek DAffF / DAff (te same co kolumny stac_jedzenie / stac_pelny kiesa-ludu.csv i skladniki sum "stac jedzenie" / "pelny" / "stac" linii "Kiesa ludu:"),
        /// wies - zakupy tej doby / potrzeba (skladniki "wsie: zakupy X z Y"). Tylko odczyt: bez Get (nie tworzy wpisu), bez zlota i bez stanu.
        /// null - bez zdania (kiesa nieczynna, zawor zamkow wylaczony, osada spoza ksiegi, wies bez miasta targowego). log - linia do logu z fragmentem linii
        /// "Kiesa ludu:" tej samej doby (sprawdz_logi E5/C4-1 porownuje go z linia doby).</summary>
        internal static string BreadLine(Settlement st, out string log)
        {
            log = null;
            if (st == null || !Live) return null;
            Folk f;
            if (!_f.TryGetValue(st, out f) || f == null || f.Kind < 0 || f.Kind > 2) return null;
            if (f.Kind == 1 && !CastleLive) return null;               // podzamcze nie liczone (zawor 110 wylaczony) - nie ma czego pokazac
            // dzien ostatniej doby kiesy (ten sam co w linii "Kiesa ludu: dzien N" tej doby); liczby osady swieze, gdy policzone w tej dobie, a doba nie starsza niz wczoraj
            int now = (int)CampaignTime.Now.ToDays - 1, day = _liveDay >= 0 ? _liveDay : now;
            string head = "PeopleMenu (E5/C4): dzien " + day + " | " + Clean(st.Name != null ? st.Name.ToString() : st.StringId) + " (" + st.StringId + ", "
                          + (f.Kind == 0 ? "miasto" : f.Kind == 1 ? "zamek" : "wies") + ")";
            var d = _ld;
            if (d == null || _liveDay < 0 || f.ADay != _liveDay || now - _liveDay > 1)
            {
                log = head + " | brak liczb tej doby (ostatnie liczenie osady: " + (f.ADay >= 0 ? "dzien " + f.ADay : "brak w tej sesji") + ") - zdanie 'not reckoned yet'";
                return "Bread: the people's purses have not been reckoned yet - look again tomorrow.";
            }
            if (f.Kind == 2)
            {
                var vil = st.Village;
                var mk = vil != null ? (vil.TradeBound ?? vil.Bound) : null;    // to samo miasto targowe co Villages()
                if (mk == null || mk.Town == null || !(f.DVNeed > 0)) { log = head + " | bez zdania (brak miasta targowego albo potrzeby zakupow 0)"; return null; }
                int v = In100(f.DVBuy / f.DVNeed);
                log = head + " | wies: zakupy " + f.DVBuy + " z " + F2(f.DVNeed) + " (w oknie " + v + " na 100, targ " + Clean(mk.Name != null ? mk.Name.ToString() : mk.StringId)
                      + ") | w linii Kiesa ludu: wsie " + d.Villages + ": zakupy " + d.VBuy + " z " + N0(d.VNeed) + " (" + P1(d.VBuy, d.VNeed);
                string market = mk.Name != null ? mk.Name.ToString() : "the market";
                return "Bread: the villagers grow their own; at the market of " + market + " they bought "
                       + (v >= 100 ? "all the salt, tools, cloth, pots and ale they need." : v + " parts in 100 of the salt, tools, cloth, pots and ale they need.");
            }
            int a = In100(f.DAffF), b = In100(f.DAff);
            string who = f.Kind == 0 ? "townsfolk" : "castle folk";
            log = head + " | stac jedzenie " + F2(f.DAffF) + ", pelny " + F2(f.DAff) + " (w oknie " + a + " / " + b + " na 100; kolumny stac_jedzenie / stac_pelny kiesa-ludu.csv) | w linii Kiesa ludu: "
                  + (f.Kind == 0
                     ? "stac jedzenie W/R/P " + Pct(d.AFW, d.NFW) + " / " + Pct(d.AFR, d.NFR) + " / " + Pct(d.AFP, d.NFP)
                       + ", pelny W/R/P " + Pct(d.AW, d.NW) + " / " + Pct(d.AR, d.NR) + " / " + Pct(d.AP, d.NP) + " (swiat " + Pct(d.AW + d.AR + d.AP, d.NW + d.NR + d.NP) + ")"
                     : "zamkow bez kredytu " + d.CNoCourt + "), stac " + P1(d.CAff, d.CNeed));
            return "Bread: " + a + " in 100 " + who + " can afford their daily bread, " + b + " in 100 their full basket.";
        }

        /// <summary>E5/C4: udzial 0..1 jako "ile na 100" - w dol (99.6% to jeszcze nie wszyscy), NaN - 0.</summary>
        private static int In100(double x) { return double.IsNaN(x) ? 0 : (int)Math.Max(0, Math.Min(100, Math.Floor(100.0 * x + 1e-9))); }

        private static void CsvRowLive(StringBuilder sb, int day, Folk f)
        {
            var st = f.St;
            var k = st.OwnerClan != null ? st.OwnerClan.Kingdom : null;
            double people = f.Kind == 1 ? CastleHeads(f) : f.DBook;     // GL-2: ludzie ksiegi (przy glowach wedlug rynku - nie PeopleCal x DR); podzamcze - glowy
            long gold = st.SettlementComponent != null ? st.SettlementComponent.Gold : 0;
            long res = f.Kind == 0 ? Seal31.TownReserve(st.Town) : CastlePurse.Reserve(st.Town);
            sb.Append(day).Append(';').Append(day + 1).Append(';').Append(st.StringId).Append(';').Append(Clean(st.Name != null ? st.Name.ToString() : st.StringId)).Append(';')
              .Append(f.Kind == 0 ? "miasto" : "zamek").Append(';').Append(Clean(k != null ? k.Name.ToString() : "-")).Append(';')
              .Append(st.Culture != null ? st.Culture.StringId : "").Append(';').Append("-").Append(';')
              .Append(N0(people)).Append(';').Append(N0(f.HW + f.HR + f.HP + f.HS)).Append(';').Append(f.DR.ToString("0.000", Inv)).Append(';')
              .Append(N0(f.HW * f.DR)).Append(';').Append(N0(f.HR * f.DR)).Append(';').Append(N0(f.HP * f.DR)).Append(';').Append(N0(f.HS * f.DR)).Append(';')
              .Append(N0(f.DB)).Append(';').Append(N0(f.DWg)).Append(';').Append(N0(f.SpendFood)).Append(';').Append(F2(f.DM)).Append(';')
              .Append(f.LDayIn).Append(';').Append(f.LDayOut).Append(';').Append(f.KW).Append(';').Append(f.KR).Append(';').Append(f.KP).Append(';')
              .Append(F2(f.DAffF)).Append(';').Append(F2(f.DAff)).Append(';').Append(gold).Append(';').Append(res).Append(';').Append(f.LDayUnpaid).Append(Environment.NewLine);
        }

        /// <summary>TownFolk.Lines: linie B1 tej doby (stan przy zmianie, zapasy startowe, zwrot, robocizna, prawa panskie, R14) - zeruje.</summary>
        private static List<string> TakeLiveLines()
        {
            var l = new List<string>();
            foreach (var x in new[] { _lineState, _lineSeed, _lineRet, _lineLab, _lineRights, _lineR14, _lineC2, _lineTithe }) if (x != null) l.Add(x);
            _lineState = _lineSeed = _lineRet = _lineLab = _lineRights = _lineR14 = _lineTithe = _lineC2 = null;
            TakeP1Lines(l);   // P1: linie pomiarow obiegu kiesy tej doby
            TakeHoardLines(l);   // kiesa2: linia startu sesji i linia doby podatku od nadwyzki
            TakeGuardLines(l);   // obieg2: linie startu sesji, straznika obiegu, podatku od nadwyzki wsi i pozyczek od ludu
            return l;
        }
    }
}
