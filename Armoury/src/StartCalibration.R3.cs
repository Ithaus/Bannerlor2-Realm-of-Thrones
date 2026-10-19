using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// KS-R3 (17.10, "kupcy i krainy" - runda 3 po autotescie 120 dob 2026-10-17_01-15-30): stan gry kalibracji startu dla rzeczy rundy 3 (linie, zapis ks4).
    ///  - KLUCZE (MCM, domyslnie TAK, dzialaja tylko w KS; wylaczony = zachowanie rundy 3 tej czesci): Ks Food Carts (KS-H), Ks Land Gradient (krok 5 - gradient krainy),
    ///    Ks Alpha Net (alfa netto wsi i krok 8b), Ks Village Paid First (drabina wsi: najpierw zaplacone z Planu, ciecie starej listy), Ks Own Food First (wies je swoj
    ///    towar z magazynu, gdy kupila mniej niz zaplacone kcal), Ks Perish Reach (regula zasiegu psujacych sie), Ks Real Offtake (popyt ceny miasta z prawdziwego odbioru
    ///    panow i dworow, wsad ZC5 do mocy przetworow, zapas wsadu ZC5 6 dni i wyrob 4 dni), Ks Market Control (sterowanie rynku po D+14 i regula nadmiaru krainy),
    ///    Ks Village Buy Every 2 (zakupy wsi co 2. doba), Ks Granary Release (podloga spichlerza schodzi, gdy wies kupuje i je; liczenie kcal piwa i lepszego stolu).
    ///  - ODBIOR MIASTA (KsTownTake): ramki ksiegi towarow w miescie (panowie kupili - porzucili na polce, dwory, mieszczanie, wsie, wsad ZC5) - srednia 28 dob odbioru
    ///    panow i dworow (popyt ceny P3) i 7 dob calego odbioru polki (zamowienie KS-H);
    ///  - KASA 28 DOB miast (przepustowosc korytarza przed D), STEROWANIE RYNKU (P4: zuzycie i brak zaplaconego popytu 28 dob, czynnik popytu targu w Planie),
    ///    NADMIAR KRAINY (polki krainy > 2 x cel i rosna - ciecie 10% na Plan), GRADIENT KRAIN (z Planu), PODLOGA SPICHLERZA wsi (G6), WIES JE SWOJE (G3),
    ///    WYDATKI WSI SPOZA KOSZYKA i FRACHT (alfa netto P2).
    /// Wszystko liczy tylko przy kluczu Start Calibration; w grze Jeffa bez arm_ks nic sie nie zmienia.
    /// </summary>
    internal static partial class StartCalibration
    {
        // ------------------------------------------------------------ klucze rundy 3
        private static Settings S3 { get { return Settings.Current; } }
        internal static bool SwFoodCarts { get { var s = S3; return s == null || s.KsFoodCarts; } }
        internal static bool SwLandGradient { get { var s = S3; return s == null || s.KsLandGradient; } }
        internal static bool SwAlphaNet { get { var s = S3; return s == null || s.KsAlphaNet; } }
        internal static bool SwHomeTradePrice { get { var s = S3; return s == null || (s.KsAlphaNet && s.KsHomeWorkTradePrices); } }   // f26-wolni
        internal static bool SwPaidFirst { get { var s = S3; return s == null || s.KsVillagePaidFirst; } }
        internal static bool SwOwnFirst { get { var s = S3; return s == null || s.KsOwnFoodFirst; } }
        internal static bool SwPerishReach { get { var s = S3; return s == null || s.KsPerishReach; } }
        internal static bool SwRealOfftake { get { var s = S3; return s == null || s.KsRealOfftake; } }
        internal static bool SwMarketControl { get { var s = S3; return s == null || s.KsMarketControl; } }
        /// <summary>f14-rynek (klucz Ks Ctl Own Food): sterowanie rynku liczy "wies je swoje" (G3) rynku wsi w zuzyciu - podaz targu Planu, ktora nie doszla do straganu.</summary>
        internal static bool SwCtlOwnFood { get { var s = S3; return s == null || s.KsCtlOwnFood; } }
        /// <summary>f19-e2 (klucz Ks Ctl Own Same Food, przy Ks Ctl Own Food): w zuzyciu sterowania tylko "wies je swoje" w miejsce braku TEGO SAMEGO towaru (min(brak, swoje)),
        /// nie zastepcze kcal za inny brakujacy towar (te sa juz brakiem tamtego towaru).</summary>
        internal static bool SwCtlOwnSameFood { get { var s = S3; return s == null || s.KsCtlOwnSameFood; } }
        internal static bool SwBuyEvery2 { get { var s = S3; return s == null || s.KsVillageBuyEvery2; } }
        internal static bool SwGranaryRelease { get { var s = S3; return s == null || s.KsGranaryRelease; } }

        /// <summary>Doba A (poczatek rampy) w stanie zastosowanym albo -1.</summary>
        internal static int RampDayA { get { return Applied ? _rampDay : -1; } }
        internal static int TownMarketOf(Town t) { return TownMarket(t); }

        private static void R3Reset()
        {
            _tk = null; _tkN = 0; _tkDay = -1; _gold28 = null; _goldN = 0;
            Array.Clear(_c28, 0, _c28.Length); Array.Clear(_u28, 0, _u28.Length); _cN = 0; _cDay = -1; Array.Clear(_uDay, 0, _uDay.Length); _mf = null; _cRestart = -1;
            _landShelfPrev.Clear(); _landCut.Clear(); _landGlut.Clear(); _grad = null; _ctlLine = null; T4Bump();
            _floor.Clear(); _ownKr.Clear(); _bKcal.Clear(); _floorBack = 0; _ownLong = 0; _ownStreak.Clear();
            _vilOut.Clear(); _vilOutDay.Clear(); _vilFrDay.Clear(); _townLand.Clear(); _castleTown.Clear();
            _ownDay = new double[6]; _shelfLineDay = -1; _csvGoodsDay = -1; _outMkDay[0] = _outMkDay[1] = _frMkDay[0] = _frMkDay[1] = 0;
            Array.Clear(_p2, 0, _p2.Length); Array.Clear(_p2Mk, 0, _p2Mk.Length); _mfStage = null; _r3Pending = null; _r3Keep = null;
            _ownCov.Clear(); Array.Clear(_ovDay, 0, _ovDay.Length); Array.Clear(_ovTown, 0, _ovTown.Length); Array.Clear(_ov28, 0, _ov28.Length);   // f14-rynek (recenzja)
            Array.Clear(_ownIn28, 0, _ownIn28.Length); Array.Clear(_ownAll28, 0, _ownAll28.Length);   // f19-e2
            _ownN = 0;   // scalenie f19 (recenzja f19-e2): licznik dob srednich "od wczytania" (_ov28, _ownIn28, _ownAll28)
        }

        // ------------------------------------------------------------ kraina miasta (kultura z kalibracji - jak Plan)
        private static readonly Dictionary<Settlement, string> _townLand = new Dictionary<Settlement, string>();

        /// <summary>Kraina miasta (kultura z kalibracji, jak kraina Planu) - korytarze KS-H.</summary>
        internal static string TownLandOf(Settlement st)
        {
            if (st == null) return "?";
            string l;
            if (_townLand.TryGetValue(st, out l)) return l;
            try { l = PeopleBook.CultureAtCalibration(st); } catch { l = null; }
            if (string.IsNullOrEmpty(l)) try { l = st.Culture != null ? st.Culture.StringId : null; } catch { l = null; }
            l = l ?? "?";
            _townLand[st] = l;
            return l;
        }

        // ------------------------------------------------------------ odbior miasta (KsTownTake): [miasto KS-H x ZN]
        // KS-R3 (port symulatora 17.10, runda 3): TkP2 - drabina 2 wsi (ponad plan, z nadmiaru polki) - odejmowana od odbioru 7 dob zamowienia KS-H (zostaje w zuzyciu rynku)
        private const int TkLord = 0, TkDisc = 1, TkCourt = 2, TkCons = 3, TkVill = 4, TkZc5 = 5, TkP2 = 6, TkN = 7;
        private static double[] _tk, _one28, _off7;   // _tk [TkN][nt * ZN] doba; srednie [nt * ZN]
        private static int _tkN, _tkDay = -1, _tkNt;

        private static bool TakeEnsure()
        {
            int nt = KsFoodCarts.TownCount;
            if (nt <= 0) return false;
            if (_tk == null || _tkNt != nt)
            {
                _tkNt = nt; _tk = new double[TkN * nt * ZN];
                if (_one28 == null || _one28.Length != nt * ZN) { _one28 = new double[nt * ZN]; _off7 = new double[nt * ZN]; _tkN = 0; }
            }
            return true;
        }

        /// <summary>GoodsLedger (ramki z osada-miastem; LordDiscard): odbior polki miasta doby wedlug rodzaju. Tylko watek glowny, tylko przy kluczu.</summary>
        internal static void KsTakeNote(Settlement st, ItemObject it, int code, int n)
        {
            try
            {
                if (n <= 0 || st == null || it == null || !KeyOn || !st.IsTown) return;
                Zc4EnsureMap();
                int z; if (_zIx == null || !_zIx.TryGetValue(it, out z)) return;
                int t = KsFoodCarts.IndexOf(st); if (t < 0 || !TakeEnsure()) return;
                _tk[(code * _tkNt + t) * ZN + z] += n;
            }
            catch { }
        }

        /// <summary>KS-R3 (port symulatora 17.10, runda 3): drabina 2 wsi (TownFolk.ZcTake2) - sztuki kupione ponad plan z nadmiaru polki miasta st (poza zamowieniem KS-H).</summary>
        internal static void KsTakeP2(Settlement st, ItemObject it, int n) { KsTakeNote(st, it, TkP2, n); }

        /// <summary>Raz na dobe (Daily): srednia 28 dob odbioru panow (kupione - porzucone na polce) i dworow, srednia 7 dob calego odbioru polki.</summary>
        private static void TakeDaily(int today)
        {
            if (_tkDay == today || !TakeEnsure()) return;
            _tkDay = today;
            _tkN++;
            double w28 = 1.0 / Math.Min(28, _tkN), w7 = 1.0 / Math.Min(7, _tkN);
            int nt = _tkNt, n = nt * ZN;
            for (int i = 0; i < n; i++)
            {
                double lord = _tk[TkLord * n + i] - _tk[TkDisc * n + i], court = _tk[TkCourt * n + i];
                double one = Math.Max(0.0, lord) + court;
                // KS-R3 (port symulatora 17.10, runda 3): bez drabiny 2 wsi - zakupy ponad plan z nadmiaru polki zrodla podnosily jego zamowienie Dz (odbior 7 dob), a z nim
                // zapas zrodla 1.25 x T* x Dz: miasto z nadwyzka nie mialo oferty dla kupcow (symulator: ryby, ser, maslo Westeros - "bez nadwyzki" w 95% prob)
                double vill = Math.Max(0.0, _tk[TkVill * n + i] - _tk[TkP2 * n + i]);
                double all = one + _tk[TkCons * n + i] + vill + _tk[TkZc5 * n + i];
                _one28[i] += (one - _one28[i]) * w28; _off7[i] += (all - _off7[i]) * w7;
                // recenzja runda 3 (wazne): drabina 2 wsi (ponad plan, z oszczednosci) poza zuzyciem sterowania rynku (C28) - wedlug rynku i towaru Planu
                double p2 = _tk[TkP2 * n + i];
                if (p2 > 0) { int mk = KsFoodCarts.MarketOfIndex(i / ZN), g = CtlGood(i % ZN); if ((mk == 0 || mk == 1) && g >= 0) _p2Mk[mk * KsTab.NG + g] += p2; }
            }
            Array.Clear(_tk, 0, _tk.Length);
        }

        /// <summary>Odbior 7 dob polki miasta (szt./d, pozycja koszyka z) - zamowienie KS-H.</summary>
        internal static double ZOff7(Settlement st, int z)
        {
            int t = KsFoodCarts.IndexOf(st);
            return t >= 0 && _off7 != null && t * ZN + z < _off7.Length && z >= 0 ? _off7[t * ZN + z] : 0.0;
        }

        /// <summary>Odbior 28 dob panow (kupione - porzucone na polce) i dworow w miescie (szt./d) - popyt ceny P3.</summary>
        internal static double ZOne28(Settlement st, int z)
        {
            int t = KsFoodCarts.IndexOf(st);
            return t >= 0 && _one28 != null && t * ZN + z < _one28.Length && z >= 0 ? _one28[t * ZN + z] : 0.0;
        }

        // ------------------------------------------------------------ kasa 28 dob miast (krok 5 przed D)
        private static double[] _gold28; private static int _goldN, _goldDay = -1;
        private static void GoldDaily(int today)
        {
            if (_goldDay == today) return;
            int nt = KsFoodCarts.TownCount; if (nt <= 0) return;
            _goldDay = today;
            if (_gold28 == null || _gold28.Length != nt) { _gold28 = new double[nt]; _goldN = 0; }
            _goldN++;
            double w = 1.0 / Math.Min(28, _goldN);
            foreach (var t in Town.AllTowns)
            {
                if (t == null || t.Settlement == null) continue;
                int i = KsFoodCarts.IndexOf(t.Settlement); if (i < 0) continue;
                _gold28[i] = _goldN <= 1 ? Math.Max(0, t.Gold) : _gold28[i] + (Math.Max(0, t.Gold) - _gold28[i]) * w;
            }
        }

        // ------------------------------------------------------------ sterowanie rynku (P4)
        private static readonly double[] _c28 = new double[2 * KsTab.NG], _u28 = new double[2 * KsTab.NG];
        private static readonly double[] _uDay = new double[2 * KsBasket.N];      // brak zaplaconego popytu doby (szt.): wsie drabina 1, mieszczanie, wsad ZC5
        private static int _cN, _cDay = -1;
        private static readonly double[] _p2Mk = new double[2 * KsTab.NG];          // recenzja runda 3: sztuki drabiny 2 wsi doby wedlug rynku i towaru Planu (poza C28)
        private static double[] _mfStage;                                          // recenzja runda 3 (drobne): czynnik nowego Planu - do _mf dopiero przy Planie bez bledu
        internal const int CtlEnd = 49;                                            // recenzja runda 3 (wazne): sterowanie rynku i ciecie nadmiaru krain tylko w oknie startu D+14..D+49
        // KS-R3 (port symulatora 17.10, runda 1): srednie zuzycia i braku od nowa w dobie konca rampy popytu (A + 28 = D + 14) - dotad srednia 28 dob z doby D+14 miala
        // w sobie 14 dob starej gospodarki przed D i 14 dob rampy (runda 3: zuzycie zboza przed D 6.4k/d Westeros i 3k/d Essos wobec 11k i 10k po rampie) - pierwsze
        // Plany sterowania ciely podaz o 7% na Plan; doba restartu w zapisie (R=)
        private static int _cRestart = -1;
        private const int CtlMinDays = 5;                 // najmniej tyle dob pelnej lambdy w srednich przed pierwsza zmiana czynnika
        private const double CtlStep = 0.07, CtlGap = 0.05; // krok czynnika na Plan i luka polek (bez zmian; runda 1 symulatora: 10% / 10% - zboze Westeros 0.947 przy polkach celu w D)
        private static double[] _mf;                                                  // czynnik popytu targu [2 * NG] (zapis)
        private static string _ctlLine;
        private static readonly Dictionary<string, long[]> _ctlTmp = new Dictionary<string, long[]>(StringComparer.Ordinal);

        /// <summary>Brak zaplaconego popytu (szt.) rynku m i pozycji z: wies - drabina 1 bez towaru, mieszczanie - polka ponizej koszyka, ZC5 - brak wsadu.</summary>
        internal static void KsUnmet(int m, int z, double pcs)
        {
            if (!(pcs > 0) || m < 0 || m > 1 || z < 0 || z >= ZN) return;
            _uDay[m * ZN + z] += pcs;
        }

        /// <summary>Towar Planu (KsTab) pozycji koszyka do sterowania rynku (surowiec jedzenia; mieso wieloryba - mieso); -1 - poza.</summary>
        private static int CtlGood(int z)
        {
            switch (z)
            {
                case ZGrain: return KsTab.GGrain; case ZFish: return KsTab.GFish; case ZMeat: case ZWhale: return KsTab.GMeat; case ZCheese: return KsTab.GCheese;
                case ZButter: return KsTab.GButter; case ZEgg: return KsTab.GEgg; case ZApple: return KsTab.GApple; case ZCarrot: return KsTab.GCarrot; case ZOrange: return KsTab.GOrange;
                case ZGrape: return KsTab.GGrape; case ZOlives: return KsTab.GOlives; case ZDate: return KsTab.GDate; case ZHoney: return KsTab.GHoney; case ZSalt: return KsTab.GSalt;
                default: return -1;
            }
        }

        /// <summary>Raz na dobe: srednie 28 dob zuzycia na rynek (ksiega towarow, linia "KS: rynki") i braku zaplaconego popytu wedlug towaru jedzenia Planu.</summary>
        private static void ControlDaily(int today)
        {
            if (_cDay == today) return;
            int d = GoodsLedger.KsMarketTake(_ctlTmp);
            if (d < 0) { Array.Clear(_uDay, 0, _uDay.Length); Array.Clear(_ovDay, 0, _ovDay.Length); return; }
            _cDay = today;
            int rampEnd = _state == StateApplied && DDay >= 0 ? DDay + (RampDemand - RampSupply) : -1;
            if (rampEnd >= 0 && today >= rampEnd && _cRestart != rampEnd) { _cN = 0; _cRestart = rampEnd; }   // tylko doby z pelnym popytem koszyka (lambda 1)
            _cN++;
            double w = 1.0 / Math.Min(28, _cN);
            var c = new double[2 * KsTab.NG]; var u = new double[2 * KsTab.NG];
            bool own = SwCtlOwnFood;
            bool same = own && SwCtlOwnSameFood;                                      // f19-e2 (klucz Ks Ctl Own Same Food)
            var ownAll = new double[2]; var ownIn = new double[2];                   // f19-e2: wies je swoje W/E - wszystko (MkOwn) i czesc w zuzyciu sterowania (szt.)
            foreach (var kv in _ctlTmp)
            {
                int z = Array.IndexOf(ZIds, kv.Key); if (z < 0) continue;
                int g = CtlGood(z); if (g < 0 || kv.Value == null || kv.Value.Length < 8) continue;
                for (int m = 0; m < 2; m++) c[m * KsTab.NG + g] += Math.Max(0, kv.Value[3 + m]);
                // f14-rynek (klucz Ks Ctl Own Food): wies je swoje (G3) rynku wsi - KsMarketTake [8 + m] (MkOwn: z magazynu wsi, poza straganem). Podaz targu Planu (dm) ma ten
                // towar w sobie (wies miala go sprzedac i kupic koszyk na targu), a zuzycie targu nie - bez niego sterowanie widzialo nadmiar tam, gdzie wies zjadla swoje
                // f19-e2 (klucz Ks Ctl Own Same Food): G3 (TownFolk.OwnFirst) je z magazynu wedlug braku KCAL w stalej kolejnosci (ziarno, ryby, mieso, ser ...), niezaleznie
                // od tego, czego zabraklo na straganie - wies rybacka bez ziarna na targu je swoje ryby. Cale MkOwn ryb w zuzyciu ryb liczylo te same kcal drugi raz (brak ziarna
                // jest juz w braku ziarna), a czynnik ryb rosl przy rosnacych polkach ryb (fin93: ryby Westeros x1.07-1.13, polki 0.93 -> 3.68 T*). Z kluczem do zuzycia idzie
                // tylko czesc pokrywajaca brak TEGO SAMEGO towaru - nizej, z _ovDay (KsOwnCovered: min(brak, swoje) wedlug pozycji koszyka wsi)
                if (own && kv.Value.Length >= 10)
                    for (int m = 0; m < 2; m++)
                    {
                        double o = Math.Max(0, kv.Value[8 + m]);
                        ownAll[m] += o;
                        if (!same) { c[m * KsTab.NG + g] += o; ownIn[m] += o; }
                    }
            }
            // f14-rynek (recenzja, Ks Ctl Own Food): brak drabiny 1 wsi pokryty swoim tym samym towarem (G3) jest juz w zuzyciu (wyzej) - bez niego w braku rynku
            // f19-e2 (Ks Ctl Own Same Food): ta sama czesc (odjeta od braku) idzie do zuzycia - zuzycie + brak tego towaru bez zmiany, kcal zastepcze nie wchodza nigdzie drugi raz
            var ovMk = new double[2];
            for (int m = 0; m < 2; m++) for (int z = 0; z < ZN; z++)
            {
                int g = CtlGood(z); if (g < 0) continue;
                double ud = _uDay[m * ZN + z];
                if (own)
                {
                    double cut = Math.Min(ud, Math.Max(0.0, _ovDay[m * ZN + z])); ud -= cut; ovMk[m] += cut;
                    if (same) { c[m * KsTab.NG + g] += cut; ownIn[m] += cut; }
                }
                u[m * KsTab.NG + g] += ud;
            }
            Array.Clear(_uDay, 0, _uDay.Length); Array.Clear(_ovDay, 0, _ovDay.Length);
            for (int i = 0; i < c.Length; i++) c[i] = Math.Max(0.0, c[i] - _p2Mk[i]);       // bez drabiny 2 wsi (jednorazowe wydawanie oszczednosci)
            Array.Clear(_p2Mk, 0, _p2Mk.Length);
            for (int i = 0; i < c.Length; i++) { _c28[i] = _cN <= 1 ? c[i] : _c28[i] + (c[i] - _c28[i]) * w; _u28[i] = _cN <= 1 ? u[i] : _u28[i] + (u[i] - _u28[i]) * w; }
            Array.Clear(_ovTown, 0, _ovTown.Length);
            try { R4cUnmetDaily(w, _cN <= 1); } catch (Exception e) { Stumble("R4cUnmetDaily", e); }   // KS r4 C1 (r4-ceny, klucz Ks Ctl Per Town): brak miasto po miescie
            // scalenie f19 (recenzja f19-e2): srednie linii sterowania (tylko pomiar, poza zapisem) z WLASNYM licznikiem dob od wczytania - _cN wraca z zapisu (do 28),
            // wiec po wczytaniu srednia ruszala od 0 z waga 1/28 (po 7 dobach ok. 22% prawdziwej wartosci); teraz pierwsza doba po wczytaniu ustawia wartosc, dalej zwykla
            // srednia, od 28 doby srednia kroczaca 1/28 jak sterowanie. Sterowanie (_c28/_u28, czynnik) bez zmian
            _ownN++;
            double wo = 1.0 / Math.Min(28, _ownN);
            for (int m = 0; m < 2; m++) { double v = SwCtlPerTown ? _ovTown[m] : ovMk[m]; _ov28[m] = _ownN <= 1 ? v : _ov28[m] + (v - _ov28[m]) * wo; }
            for (int m = 0; m < 2; m++)   // f19-e2: srednie do linii sterowania (tylko pomiar)
            {
                _ownIn28[m] = _ownN <= 1 ? ownIn[m] : _ownIn28[m] + (ownIn[m] - _ownIn28[m]) * wo;
                _ownAll28[m] = _ownN <= 1 ? ownAll[m] : _ownAll28[m] + (ownAll[m] - _ownAll28[m]) * wo;
            }
        }

        // ------------------------------------------------------------ f14-rynek (recenzja): brak wsi pokryty swoim tym samym towarem (klucz Ks Ctl Own Food)
        // Sterowanie liczy "wies je swoje" (G3, MkOwn) w zuzyciu rynku. Czesc braku drabiny 1 wsi (TownFolk b.Miss - koszyk zaplacony bez towaru na polce miasta
        // targowego i w miastach obok), ktora wies pokryla z magazynu TYM SAMYM towarem, nie jest juz brakiem: bez odjecia potrzeba wsi liczy sie dwa razy - raz jako
        // zjedzone swoje, raz jako brak (C1: popyt zaplacony - odbior 7 dob w miastach z polka < 0.5 T*; bez C1: brak drabiny 1 z KsUnmet). Inny towar (np. brak sera,
        // zjedzone ziarno) zostaje brakiem - na targu naprawde zabraklo sera. Tylko liczby sterowania - bez zlota i towaru.
        private sealed class OwnCov { public Settlement Mk; public float[] PerDay; public int Day, Days; }
        private static readonly Dictionary<Village, OwnCov> _ownCov = new Dictionary<Village, OwnCov>();   // wies -> pokryte swoim z ostatnich zakupow (szt./d wedlug pozycji)
        private static readonly double[] _ovDay = new double[2 * KsBasket.N];   // to samo wedlug rynku od ostatniej doby sterowania (szt.) - odejmowane od _uDay
        private static readonly double[] _ovTown = new double[2], _ov28 = new double[2];   // odjete od braku doby (szt./d) i srednia 28 dob - linia sterowania
        private static readonly double[] _ownIn28 = new double[2], _ownAll28 = new double[2];   // f19-e2: wies je swoje W/E w zuzyciu sterowania i cale (srednie z dob od wczytania, najwyzej 28)
        private static int _ownN;   // scalenie f19 (recenzja f19-e2): doby srednich _ov28 / _ownIn28 / _ownAll28 od wczytania (poza zapisem)

        /// <summary>TownFolk.Zc4BuyVillage (G3, kazde zakupy z koszykiem zaplaconym): brak drabiny 1 (miss) i zjedzone swoje (own, null - nic) wedlug pozycji koszyka;
        /// pokryte = min(brak, swoje) tego samego towaru jedzenia. Zakupy co `days` dob - na dobe dzielone przez days. Nie rzuca.</summary>
        internal static void KsOwnCovered(Village v, Settlement mk, int[] miss, int[] own, int days)
        {
            if (v == null) return;
            try
            {
                OwnCov c = null;
                if (mk != null && miss != null && own != null)
                {
                    int m = MarketOfSettlement(mk), dd = Math.Max(1, days);
                    for (int z = 0; z < ZN && z < miss.Length && z < own.Length; z++)
                    {
                        int ov = Math.Min(miss[z], own[z]);
                        if (ov <= 0 || !ZFood(z)) continue;
                        if (c == null) c = new OwnCov { Mk = mk, PerDay = new float[ZN], Day = Today(), Days = dd };
                        c.PerDay[z] = (float)ov / dd;
                        if ((m == 0 || m == 1) && SwMarketControl) _ovDay[m * ZN + z] += ov;   // te same warunki co KsUnmet braku drabiny 1 (TownFolk)
                    }
                }
                if (c == null) _ownCov.Remove(v); else _ownCov[v] = c;
            }
            catch { }
        }

        /// <summary>R4cUnmetDaily (C1): pokryte swoim wedlug miasta targowego wsi (szt./d) - wpisy ostatnich zakupow; starsze niz ich `Days` dob (wies nie kupuje) - usuwane.</summary>
        private static Dictionary<Settlement, double[]> OwnCoveredByTown(int today)
        {
            Dictionary<Settlement, double[]> r = null; List<Village> old = null;
            foreach (var kv in _ownCov)
            {
                var c = kv.Value;
                if (c == null || c.Mk == null || c.PerDay == null || today - c.Day > c.Days) { (old ?? (old = new List<Village>())).Add(kv.Key); continue; }
                if (r == null) r = new Dictionary<Settlement, double[]>();
                double[] a; if (!r.TryGetValue(c.Mk, out a)) { a = new double[ZN]; r[c.Mk] = a; }
                for (int z = 0; z < ZN && z < c.PerDay.Length; z++) a[z] += c.PerDay[z];
            }
            if (old != null) foreach (var v in old) _ownCov.Remove(v);
            return r;
        }

        /// <summary>Czynnik popytu targu na Plan (BuildInput): do D+14 - 1.03 (polki); od D+14 (klucz Ks Market Control) k = (zuzycie + brak) x (1 + luka) / popyt targu Planu,
        /// luka = (cel polek - polki) / (28 x (zuzycie + brak)) w +-5%, krok najwyzej +-7% na Plan, 0.85-1.10. Regula nadmiaru krainy: polki krainy > 2 x cel i wiecej niz
        /// tydzien temu - ciecie 10% na Plan (do 0.5), ponizej 1.5 x celu - powrot o 10% na Plan; potrojna waga w poprawce G '+'.</summary>
        private static double[] MarketControl(int day, KsInput inp)
        {
            if (!SwMarketControl) { _ctlLine = null; _landGlut.Clear(); return null; }      // bez klucza - margines popytu targu jak runda 3 (MarketMargin)
            _mfStage = null; _gapLine = null;
            // recenzja runda 3 (wazne): planista tylko w oknie startu - po D+49 czynnik wraca do 1 przez 2 Plany, ciecie nadmiaru krain tez schodzi do 1; bez tego co tydzien
            // rece pracy domowej i koszyki szlyby za sterowaniem bez kosztu i czasu zmiany (decyzja Jeffa 16.10 - zmiana powoli i z kosztem).
            // f4-pomiary (noc 17/18.10, przeglad ekonomii): po D+49 rece zajec przesuwa TYLKO regula A7 (StartCalibration.Rule.cs), a ta dziala jedynie z kluczem
            // Start Calibration Switch Rule - domyslnie WYLACZONYM od recenzji 16.10. Przy kluczu wylaczonym zajecia wiosek pod KS zostaja na Planie wlaczenia (Plan po
            // wlaczeniu zmienia juz tylko lepszy stol i prace domowa, najwyzej 0.02 rak na Plan); podaz reaguje wtedy tylko cenami i rozwozem KS-H, nie rekami. Nacisk A7
            // i jego diagnoze pokazuja linie "KS: produkcja" (zajecia zamrozone) i "KS: A7 diagnoza (f4-pomiary)". Napis linii ponizej ("dalej regula zysku i ceny") zostaje
            // bez zmian (paczka f4 zmienia tu tylko komentarz) - opisuje stan z kluczem wlaczonym.
            bool after = _state == StateApplied && DDay >= 0 && day > DDay + CtlEnd;
            bool ctl = !after && _state == StateApplied && DDay >= 0 && day >= DDay + 14 && _cRestart >= 0 && _cN >= CtlMinDays && _plan != null && _plan.Error == null && _plan.D != null;
            var mf = new double[2 * KsTab.NG];
            var sb = new StringBuilder(1500);
            sb.Append(PK).Append("sterowanie rynku doba ").Append(day).Append(ctl ? " | czynnik popytu targu z zuzycia i braku (" + Math.Min(28, _cN) + " dob od konca rampy " + _cRestart + ")"
                : after ? " | po D+" + CtlEnd + ": koniec sterowania - czynnik wraca do 1 (2 Plany), dalej regula zysku i ceny" : " | do D+14 + " + CtlMinDays + " dob pelnej lambdy albo bez klucza Ks Market Control: czynnik 1.03 (polki startu)"
                  + (R4cGapPhase(day) ? "; klucz Ks Gap Margin - jedzenie z luki zapasu polek (linia 'KS: luka zapasu')" : ""));
            for (int i = 0; i < mf.Length; i++) mf[i] = ShelfMargin;
            if (after)
            {
                var st = new double[2 * KsTab.NG];
                for (int i = 0; i < st.Length; i++)
                {
                    double k0 = _mf != null ? _mf[i] : 1.0, k = 1.0 + (k0 - 1.0) * 0.5;
                    if (Math.Abs(k - 1.0) < 0.01) k = 1.0;
                    st[i] = k; mf[i] = k;
                }
                _mfStage = st;
                LandGlutWeekly(day, false, true);
                _ctlLine = sb.Append('.').ToString();
                return mf;
            }
            if (!ctl)
            {
                // KS r4 P4 (r4-ceny, klucz Ks Gap Margin): przed sterowaniem czynnik jedzenia z luki zapasu polek miast (Plan wlaczenia od razu, potem najwyzej 7% na Plan)
                if (R4cGapPhase(day)) { try { R4cGapFactors(day, inp, mf); } catch (Exception e) { Stumble("R4cGapFactors", e); } }
                _ctlLine = sb.Append('.').ToString(); LandGlutWeekly(day, false); return mf;
            }
            var basis = _mf;
            if (basis == null) { basis = new double[2 * KsTab.NG]; for (int i = 0; i < basis.Length; i++) basis[i] = ShelfMargin; }
            var stage = (double[])basis.Clone();
            // cel polek (T* x popyt zaplacony miast) i polki rynku (szt.)
            var hs = new double[2 * KsTab.NG]; var h = new double[2 * KsTab.NG];
            foreach (var zt in _zt.Values)
            {
                if (zt == null || zt.St == null) continue;
                int m = zt.Market == 0 ? 0 : 1;
                for (int z = 0; z < ZN; z++)
                {
                    int g = CtlGood(z); if (g < 0) continue;
                    hs[m * KsTab.NG + g] += ZTstar(m, z) * (zt.Q[z] + zt.QV[z] + zt.Der[z] + zt.One[z]);
                    h[m * KsTab.NG + g] += zt.Cnt[z];
                    if (SwGapMargin) { int inc = 0; try { inc = KsFoodCarts.Incoming(zt.St, z); } catch { inc = 0; } h[m * KsTab.NG + g] += Math.Max(0, inc); }   // KS r4 P4: towar w drodze dojdzie na polki
                }
            }
            double sig = Math.Max(1, DailyLife.KsSigma);
            for (int m = 0; m < 2; m++)
            {
                sb.Append(m == 0 ? " | Westeros:" : " | Essos:");
                foreach (int g in KsCore.Food)
                {
                    int i = m * KsTab.NG + g;
                    KsItem it; double kg = inp.Items.TryGetValue(KsTab.Goods[g], out it) && it != null && it.Kg > 0 ? it.Kg : 10.0;
                    double dm = Math.Max(0.0, _plan.D[i] - (_plan.Eaten != null && i < _plan.Eaten.Length ? _plan.Eaten[i] : 0.0)) / (sig * kg);
                    // KS-R3 (port symulatora 17.10, runda 1): brak zaplaconego popytu liczy sie tylko, dopoki polki rynku sa ponizej celu (h < hs) - przy polkach ponad celem
                    // brak na pustych straganach to rozwoz (KS-H), nie rece: dotad brak z miast bez zboza trzymal podaz zboza Essos przy nadmiarze polek (S/C 1.14 w oknie)
                    double uf = hs[i] > 0 ? Math.Max(0.0, Math.Min(1.0, 1.0 - h[i] / hs[i])) : 1.0;
                    double cu = _c28[i] + _u28[i] * uf;
                    if (SwCtlPerTown) cu = _c28[i] + _ut28[i];          // KS r4 C1 (r4-ceny): brak w miastach z polka < 0.5 T* (miasto po miescie), nie przez polki calego rynku
                    double k = basis[i];
                    double gMax = CtlGap, hi = 1.10;
                    R4cBounds(day, basis[i], ref gMax, ref hi);           // KS r4 C3 (Ks Gap Margin): do D+28 luka +-25% i sufit 1.25, potem sufit schodzi do 1.10 krokiem
                    if (dm > 1.0 && cu > 1.0)
                    {
                        double gap = Math.Max(-gMax, Math.Min(gMax, (hs[i] - h[i]) / (28.0 * cu)));
                        double tgt = cu * (1.0 + gap) / dm;
                        k = Math.Max(basis[i] * (1.0 - CtlStep), Math.Min(basis[i] * (1.0 + CtlStep), tgt));
                        // KS r4 C2 (Ks Ctl Per Town): ponad 25% popytu zaplaconego przy polkach < 0.5 T* - bez ciecia (rozwoz, nie rece)
                        if (k < basis[i] && R4cNoCut(i)) { k = basis[i]; if (_c2Held.Count < 30) _c2Held.Add((m == 0 ? "W " : "E ") + KsTab.Goods[g] + " " + (100 * _lowSh[i]).ToString("0", Inv) + "%"); }
                        k = Math.Max(0.85, Math.Min(hi, k));
                    }
                    else if (SwGapMargin) k = Math.Max(0.85, Math.Min(hi, k));   // KS r4 C3: sufit takze dla towaru bez liczb (czynnik z luki P4 nie zostaje 1.25 po D+28)
                    stage[i] = k; mf[i] = k;
                    sb.Append(' ').Append(KsTab.Goods[g]).Append(' ').Append(F0(_c28[i])).Append('/').Append(F0(SwCtlPerTown ? _ut28[i] : _u28[i])).Append('/').Append(F0(dm)).Append(" x").Append(k.ToString("0.000", Inv));
                }
            }
            bool sameF = SwCtlOwnFood && SwCtlOwnSameFood;   // f19-e2 (Ks Ctl Own Same Food)
            sb.Append(!SwCtlOwnFood ? " (towar: zuzycie bez drabiny 2 wsi / brak / podaz targu Planu szt./d, czynnik)"
                : sameF ? " (towar: zuzycie bez drabiny 2 wsi + wies je swoje G3 tylko w miejsce braku tego samego towaru (Ks Ctl Own Same Food) / brak bez czesci pokrytej swoim tym samym towarem / podaz targu Planu szt./d, czynnik)"
                : " (towar: zuzycie bez drabiny 2 wsi + wies je swoje G3 (Ks Ctl Own Food) / brak bez czesci pokrytej swoim tym samym towarem / podaz targu Planu szt./d, czynnik)");
            if (SwCtlOwnFood) sb.Append(" | brak wsi pokryty swoim tym samym towarem (G3, w zuzyciu - odjety od braku): W ").Append(F0(_ov28[0])).Append(", E ").Append(F0(_ov28[1])).Append(" szt./d (srednia z ").Append(Math.Min(28, _ownN)).Append(" dob od wczytania, najwyzej 28)");   // f14-rynek (recenzja); scalenie f19: liczba dob sredniej
            // f19-e2: ile "wies je swoje" weszlo do zuzycia sterowania wobec calego (z kluczem Ks Ctl Own Same Food reszta to kcal zastepcze za inny brakujacy towar)
            if (SwCtlOwnFood) sb.Append(" | wies je swoje w zuzyciu sterowania (").Append(sameF ? "tylko w miejsce braku tego samego towaru, Ks Ctl Own Same Food" : "cale, bez Ks Ctl Own Same Food")
                                .Append("): W ").Append(F0(_ownIn28[0])).Append(" z ").Append(F0(_ownAll28[0])).Append(", E ").Append(F0(_ownIn28[1])).Append(" z ").Append(F0(_ownAll28[1]))
                                .Append(" szt./d (srednia z ").Append(Math.Min(28, _ownN)).Append(" dob od wczytania, najwyzej 28)");
            sb.Append(R4cCtlNote(day));                            // KS r4 (r4-ceny): C1-C3
            _mfStage = stage;                                  // do _mf przy Planie bez bledu (PlanCollect)
            LandGlutWeekly(day, true);
            int nc = 0; foreach (var kv in _landCut) foreach (var v in kv.Value) if (v < 0.999) nc++;
            sb.Append(" | nadmiar krain: ciecie ").Append(nc).Append(" (kraina x towar)").Append(R4cGlutNote()).Append('.');
            _ctlLine = sb.ToString();
            return mf;
        }

        private static readonly Dictionary<string, double[]> _landShelfPrev = new Dictionary<string, double[]>(StringComparer.Ordinal);
        private static readonly Dictionary<string, double[]> _landCut = new Dictionary<string, double[]>(StringComparer.Ordinal);
        private static readonly Dictionary<string, bool[]> _landGlut = new Dictionary<string, bool[]>(StringComparer.Ordinal);

        /// <summary>Regula nadmiaru krainy (raz na Plan): polki krainy (kultura miasta) wobec celu T* x popyt zaplacony i wobec Planu sprzed tygodnia.</summary>
        private static void LandGlutWeekly(int day, bool on, bool decay = false)
        {
            T4Bump();                                            // ks-koszt T4: ciecie krain zmienia sie tutaj (w miejscu) - pamiec progow drabiny 2 od zera
            _landGlut.Clear();
            if (decay)
            {
                // recenzja runda 3: po oknie startu ciecie nadmiaru krain schodzi do 1 przez 2 Plany
                var keys = new List<string>(_landCut.Keys);
                foreach (var key in keys)
                {
                    var cut = _landCut[key]; bool any = false;
                    for (int g = 0; g < cut.Length; g++) { if (cut[g] < 1.0) { cut[g] = 1.0 - (1.0 - cut[g]) * 0.5; if (cut[g] > 0.99) cut[g] = 1.0; } if (cut[g] < 0.999) any = true; }
                    if (!any) _landCut.Remove(key);
                }
                return;
            }
            if (!on) return;
            var sh = new Dictionary<string, double[]>(StringComparer.Ordinal); var tg = new Dictionary<string, double[]>(StringComparer.Ordinal);
            var lmk = new Dictionary<string, int>(StringComparer.Ordinal);                     // KS r4 P6: rynek krainy (miasta krainy)
            double[] med = null; try { if (SwLandGlutGuard) med = R4cMedRatio(); } catch (Exception e) { Stumble("R4cMedRatio", e); }
            foreach (var zt in _zt.Values)
            {
                if (zt == null || zt.St == null) continue;
                string l = TownLandOf(zt.St);
                double[] a, b;
                if (!sh.TryGetValue(l, out a)) { a = new double[KsTab.NG]; sh[l] = a; b = new double[KsTab.NG]; tg[l] = b; } else b = tg[l];
                int m = zt.Market == 0 ? 0 : 1;
                if (!lmk.ContainsKey(l)) lmk[l] = m;
                for (int z = 0; z < ZN; z++)
                {
                    int g = CtlGood(z); if (g < 0) continue;
                    a[g] += zt.Cnt[z]; b[g] += ZTstar(m, z) * (zt.Q[z] + zt.QV[z] + zt.Der[z] + zt.One[z]);
                }
            }
            foreach (var kv in sh)
            {
                double[] prev; _landShelfPrev.TryGetValue(kv.Key, out prev);
                double[] cut; if (!_landCut.TryGetValue(kv.Key, out cut)) { cut = new double[KsTab.NG]; for (int g = 0; g < KsTab.NG; g++) cut[g] = 1.0; }
                var glut = new bool[KsTab.NG]; bool any = false, anyCut = false;
                var t = tg[kv.Key];
                foreach (int g in KsCore.Food)
                {
                    double s = kv.Value[g], target = t[g];
                    int lm0; lmk.TryGetValue(kv.Key, out lm0);
                    if (target > 1.0 && s > 2.0 * target + 50 && prev != null && s > prev[g])
                    {
                        // KS r4 P6 (r4-ceny, klucz Ks Land Glut Guard): ciecie tylko, gdy mediana miast rynku ma polke celu - inaczej nadmiar to rozwoz (bez ciecia)
                        if (!R4cGlutHeld(kv.Key, lm0, g, med)) { glut[g] = true; any = true; cut[g] = Math.Max(0.5, cut[g] * 0.9); }
                    }
                    else if (target > 1.0 && s < 1.5 * target && cut[g] < 1.0) cut[g] = Math.Min(1.0, cut[g] / 0.9);
                    if (cut[g] < 0.999) anyCut = true;
                }
                if (any) _landGlut[kv.Key] = glut;
                if (anyCut) _landCut[kv.Key] = cut; else _landCut.Remove(kv.Key);
                _landShelfPrev[kv.Key] = (double[])kv.Value.Clone();
            }
        }

        /// <summary>Recenzja runda 3 (drobne): czynnik popytu targu nowego Planu do stanu - tylko Plan bez bledu (PlanCollect); Plan przerwany albo z bledem - bez zmiany.</summary>
        private static void MarketFactorCommit(KsResult r)
        {
            if (r != null && r.Error == null && _mfStage != null) _mf = _mfStage;
            _mfStage = null;
        }

        /// <summary>Recenzja runda 3 (wazne): czynnik ceny krainy miasta targowego mk dla pozycji koszyka z (gradient krainy z Planu x ciecie nadmiaru krainy); 1 - bez gradientu.</summary>
        internal static double LandGradZ(Settlement mk, int z)
        {
            if (mk == null || z < 0 || z >= ZN) return 1.0;
            int g = KsBasket.RawGood[z]; if (g < 0) return 1.0;
            var gr = _plan != null && _plan.Error == null ? _plan.Grad : _grad;
            if (gr == null && _landCut.Count == 0) return 1.0;
            string land = TownLandOf(mk);
            double v = 1.0; double[] a, c;
            if (gr != null && gr.TryGetValue(land, out a) && a != null && g < a.Length && a[g] > 0 && !double.IsNaN(a[g])) v = a[g];
            if (_landCut.TryGetValue(land, out c) && c != null && g < c.Length && c[g] > 0 && c[g] < 1.0) v *= c[g];
            return v;
        }

        // ------------------------------------------------------------ miasto targowe okregu (krok 5)
        private static readonly Dictionary<Settlement, Settlement> _castleTown = new Dictionary<Settlement, Settlement>();

        /// <summary>Miasto targowe wsi (TradeBound, inaczej Bound); wies zamku - najblizsze miasto zamku (pamiec sesji).</summary>
        private static Settlement TradeTownOf(Village v)
        {
            if (v == null) return null;
            var mk = v.TradeBound ?? v.Bound;
            if (mk == null || mk.IsTown) return mk;
            Settlement t;
            if (_castleTown.TryGetValue(mk, out t)) return t;
            float best = float.MaxValue;
            try
            {
                var pos = mk.GetPosition2D;
                foreach (var tw in Town.AllTowns)
                {
                    if (tw == null || !tw.IsTown || tw.Settlement == null) continue;
                    float d = pos.Distance(tw.Settlement.GetPosition2D);
                    if (d < best) { best = d; t = tw.Settlement; }
                }
            }
            catch { }
            _castleTown[mk] = t;
            return t;
        }

        // ------------------------------------------------------------ gradient krain (z Planu, w zapisie)
        private static Dictionary<string, double[]> _grad;

        /// <summary>KS-R3 wejscie Planu (BuildInput, watek glowny): klucze kroku 5, miasto targowe okregow, tabela dob miast KS-H, wojna wlascicieli, kasa 28 dob, dowiezione
        /// korytarzami, gradient poprzedniego Planu, ciecie i nadmiar krain, czynnik popytu targu, odbior panow i dworow w miescie (kg/d).</summary>
        private static void R3PlanInputs(KsInput inp, int day, List<Settlement> tst, Dictionary<string, Settlement> distMk)
        {
            inp.Corridors = SwLandGradient; inp.Reach = SwPerishReach; inp.HomeSub = SwAlphaNet;
            inp.HomeTradePrice = SwHomeTradePrice;                    // f26-wolni: czesc pokryta pracy domowej takze w cenach handlu kroku 8
            inp.ApplyPlan = _state != StateApplied;
            inp.AfterD = _state == StateApplied && DDay >= 0 && day >= DDay;
            int nt = inp.Towns.Count;
            if (tst == null || tst.Count != nt) return;
            var idx = new Dictionary<Settlement, int>();
            for (int i = 0; i < nt; i++) { if (tst[i] == null) continue; idx[tst[i]] = i; _townLand[tst[i]] = inp.Towns[i].Land ?? "?"; }
            inp.DistTown = new Dictionary<string, int>(StringComparer.Ordinal);
            if (distMk != null) foreach (var kv in distMk) { int t; if (kv.Value != null && idx.TryGetValue(kv.Value, out t)) inp.DistTown[kv.Key] = t; }
            var kix = new int[nt];
            for (int i = 0; i < nt; i++) kix[i] = KsFoodCarts.IndexOf(tst[i]);
            inp.TownDays = new float[nt * nt]; inp.TownSea = new bool[nt * nt]; inp.TownWar = new bool[nt * nt]; inp.TownGold28 = new double[nt];
            for (int a = 0; a < nt; a++)
            {
                if (_gold28 != null && kix[a] >= 0 && kix[a] < _gold28.Length) inp.TownGold28[a] = _gold28[kix[a]];
                else if (tst[a] != null && tst[a].Town != null) inp.TownGold28[a] = Math.Max(0, tst[a].Town.Gold);
                for (int b = 0; b < nt; b++)
                {
                    int k = a * nt + b;
                    inp.TownDays[k] = -1f;
                    if (a == b || inp.Towns[a].Market != inp.Towns[b].Market || kix[a] < 0 || kix[b] < 0) continue;
                    inp.TownDays[k] = KsFoodCarts.Days(kix[a], kix[b]);
                    inp.TownSea[k] = KsFoodCarts.Sea(kix[a], kix[b]);
                    try { inp.TownWar[k] = tst[a].MapFaction != null && tst[b].MapFaction != null && FactionManager.IsAtWarAgainstFaction(tst[a].MapFaction, tst[b].MapFaction); } catch { }
                }
            }
            inp.CorrDelivered = KsFoodCarts.DeliveredSnapshot();
            KupcyPlanInputs(inp, tst);                                // KS-R4 r4-kupcy H7: zapas kupcow miast i klucz korytarzy na kredyt
            var gp = _plan != null && _plan.Error == null && _plan.Grad != null ? _plan.Grad : _grad;
            if (gp != null) { inp.GradPrev = new Dictionary<string, double[]>(StringComparer.Ordinal); foreach (var kv in gp) if (kv.Value != null) inp.GradPrev[kv.Key] = (double[])kv.Value.Clone(); }
            inp.MarketFactor = MarketControl(day, inp);
            try { R4cGapInputs(inp, day); } catch (Exception e) { Stumble("R4cGapInputs", e); }   // KS r4 P4 (r4-ceny, Ks Gap Margin): sufit czynnika kroku 8 1.25 (popyt na zapas tylko w kroku 8)
            if (_landCut.Count > 0) { inp.LandCut = new Dictionary<string, double[]>(StringComparer.Ordinal); foreach (var kv in _landCut) inp.LandCut[kv.Key] = (double[])kv.Value.Clone(); }
            if (_landGlut.Count > 0) { inp.LandGlut = new Dictionary<string, bool[]>(StringComparer.Ordinal); foreach (var kv in _landGlut) inp.LandGlut[kv.Key] = (bool[])kv.Value.Clone(); }
            // odbior panow i dworow w miescie (P3) w kg Planu na dobe wedlug towaru jedzenia
            if (_one28 != null)
            {
                Zc4EnsureMap();
                double sig = Math.Max(1, inp.Sigma);
                inp.TownOne = new Dictionary<string, double[]>(StringComparer.Ordinal);
                for (int i = 0; i < nt; i++)
                {
                    if (kix[i] < 0) continue;
                    var a = new double[KsTab.NG]; bool any = false;
                    for (int z = 0; z < ZN; z++)
                    {
                        int g = CtlGood(z); if (g < 0) continue;
                        double v = _one28[kix[i] * ZN + z]; if (!(v > 0)) continue;
                        a[g] += v * sig * _zKg[z]; any = true;
                    }
                    if (any) inp.TownOne[inp.Towns[i].Id] = a;
                }
            }
        }

        // ------------------------------------------------------------ podloga spichlerza (G6), wies je swoje (G3), kcal kupione
        private static readonly Dictionary<Village, int[]> _floor = new Dictionary<Village, int[]>();   // [wlaczona 1/0, dob dobrych z rzedu, dob zlych z rzedu]
        private static readonly Dictionary<Village, double> _ownKr = new Dictionary<Village, double>();   // kcal zjedzone z magazynu (G3) na dobe (srednia 2 dob - zakupy co 2. doba)
        private static readonly Dictionary<Village, double> _bKcal = new Dictionary<Village, double>();   // kcal kupione na dobe (srednia 2 dob - zakupy co 2. doba)
        private static int _floorBack, _ownLong;
        private static readonly Dictionary<Village, int> _ownStreak = new Dictionary<Village, int>();
        private static double[] _ownDay = new double[6];                                                   // [wsi, szt., kcal, kcal koszyka, -, -] od ostatniej linii

        /// <summary>TownFolk.Zc4BuyVillage (G3): wies zjadla z magazynu kcal (szt.) - racje spichlerza i linia "KS: wies je swoje".</summary>
        internal static void KsOwnEaten(Village v, double kcal, long pcs, double basketKcal)
        {
            if (v == null || !(kcal > 0)) return;
            _ownDay[0]++; _ownDay[1] += pcs; _ownDay[2] += kcal; _ownDay[3] += Math.Max(0.0, basketKcal);
            int m = MarketOfSettlement(v.Settlement);
            if (m == 0 || m == 1) _zvKcalR[m] += kcal;
        }

        /// <summary>TownFolk.Zc4BuyVillage (G3, kazde zakupy): kcal zjedzone z magazynu za `days` dob (0 - nic) - srednia na dobe (racje G6, seria NKS-25).</summary>
        internal static void KsOwnDay(Village v, double kcal, int days)
        {
            if (v == null) return;
            double perDay = Math.Max(0.0, kcal) / Math.Max(1, days), o;
            _ownKr[v] = _ownKr.TryGetValue(v, out o) ? 0.5 * o + 0.5 * perDay : perDay;
        }

        /// <summary>TownFolk.Zc4BuyVillage: kcal kupione przez wies (zakup za `days` dob) - srednia na dobe (racje G6, pokrycie).</summary>
        internal static void KsBoughtKcal(Village v, double kcal, int days)
        {
            if (v == null) return;
            double perDay = Math.Max(0.0, kcal) / Math.Max(1, days), o;
            _bKcal[v] = _bKcal.TryGetValue(v, out o) ? 0.5 * o + 0.5 * perDay : perDay;
        }

        /// <summary>Raz na dobe (LiveDaily, wsie zastosowane): licznik podlogi spichlerza - schodzi po 14 dobach z rzedu (kupione + swoje + dom >= 95% koszyka i kupione
        /// >= 80% zaplaconych kcal), wraca po 3 dobach z racjami < 90% potrzeby. Racje wsi z podloga zdjeta - z kupionymi i zjedzonymi z magazynu.</summary>
        private static void FloorDaily(int day)
        {
            // NKS-25: wies je swoje ponad 30% koszyka - doby z rzedu (wsi z seria >= 14 dob w linii "KS: wies je swoje")
            _ownLong = 0;
            foreach (var ld in _live.Values)
            {
                if (ld == null || ld.TF || ld.V == null) continue;
                double own0; _ownKr.TryGetValue(ld.V, out own0);
                int sk; _ownStreak.TryGetValue(ld.V, out sk);
                sk = ld.WantKcal > 0 && own0 > 0.30 * ld.WantKcal ? sk + 1 : 0;
                if (sk > 0) _ownStreak[ld.V] = sk; else _ownStreak.Remove(ld.V);
                if (sk >= 14) _ownLong++;
            }
            if (!SwGranaryRelease) return;
            int on = 0, n = 0; double kcalFloor = 0, kcalNeed = 0;
            foreach (var ld in _live.Values)
            {
                if (ld == null || ld.TF || ld.V == null || ld.Res == null) continue;
                n++;
                int[] fl; if (!_floor.TryGetValue(ld.V, out fl)) { fl = new[] { 1, 0, 0 }; _floor[ld.V] = fl; }
                double bk; _bKcal.TryGetValue(ld.V, out bk);
                double own; _ownKr.TryGetValue(ld.V, out own);
                double want = ld.WantKcal, paid = ld.PaidKcal;
                double cov = want > 0 ? (bk + own + ld.HomeKcal) / want : 1.0, bp = paid > 0 ? bk / paid : 1.0;
                double need = ld.Res.KcalNeed;
                double rat = need > 0 ? (ld.Res.OwnKcal + ld.HomeKcal + bk + own) / need : 1.0;
                if (fl[0] == 1)
                {
                    if (cov >= 0.95 && bp >= 0.80) fl[1]++; else fl[1] = 0;
                    if (fl[1] >= 14) { fl[0] = 0; fl[1] = 0; fl[2] = 0; }
                }
                else
                {
                    if (rat < 0.90) fl[2]++; else fl[2] = 0;
                    if (fl[2] >= 3) { fl[0] = 1; fl[1] = 0; fl[2] = 0; _floorBack++; }
                }
                if (fl[0] == 1) { on++; kcalFloor += Math.Max(0.0, need - (ld.Res.OwnKcal + ld.HomeKcal + bk + own)); }
                kcalNeed += need;
                // racje spichlerza wsi (G6): z kupionymi i zjedzonymi z magazynu
                ld.Rations = need > 0 ? ld.PeopleHome * Math.Min(1.5, rat) : -1;
            }
            if (LogGate.Lines)
            {
                // r4-bkwies (diagnoza fin80, NKS-26): przy Ks Granary No Floor "wsi z podloga" to STAN licznika G6, a kcal - brak schodzacy z zapasu spichlerza, nie podloga dana;
                // podloga dana (wzor gry), bez podlogi i podloga awaryjna (z niczego) - dopisek GranText
                var sf = Settings.Current; bool nf = sf == null || sf.KsGranaryNoFloor;
                Log.Info(PK + "podloga spichlerza dzien " + day + " | wsi z podloga " + on + " z " + n + " (" + (n > 0 ? (100.0 * on / n).ToString("0.0", Inv) : "-") + "%), "
                         + (nf ? "kcal braku (z zapasu spichlerza) " : "kcal podlogi ")
                         + (kcalNeed > 0 ? (100.0 * kcalFloor / kcalNeed).ToString("0.00", Inv) : "-") + "% potrzeby wsi, powroty " + _floorBack + " (sesja)" + GranText(nf) + " | zejscie: 14 dob kupione + swoje + dom >= 95% koszyka"
                         + " i kupione >= 80% zaplaconych kcal; powrot: 3 doby racji < 90% potrzeby.");
            }
        }

        /// <summary>Podloga spichlerza wsi wlaczona (G6: klucz wylaczony albo wies bez licznika - zawsze).</summary>
        private static bool FloorOn(Village v)
        {
            if (!SwGranaryRelease || v == null) return true;
            int[] fl; return !_floor.TryGetValue(v, out fl) || fl[0] == 1;
        }

        private static void OwnLine(int day)
        {
            if (!LogGate.Lines) { Array.Clear(_ownDay, 0, _ownDay.Length); return; }
            Log.Info(PK + "wies je swoje dzien " + day + " | wsi " + F0(_ownDay[0]) + ", szt. " + F0(_ownDay[1]) + ", kcal " + (_ownDay[3] > 0 ? (100.0 * _ownDay[2] / _ownDay[3]).ToString("0.0", Inv) : "-")
                     + "% koszyka tych wsi (magazyn wsi, najwyzej polowa towaru na dobe, do braku kcal zaplaconych z Planu; klucz Ks Own Food First " + (SwOwnFirst ? "TAK" : "NIE") + ")"
                     + " | wsi > 30% koszyka 14 dob z rzedu: " + _ownLong + ".");
            Array.Clear(_ownDay, 0, _ownDay.Length);
        }

        // ------------------------------------------------------------ wydatki wsi spoza koszyka i fracht (alfa netto P2)
        private static readonly Dictionary<Village, double[]> _vilOut = new Dictionary<Village, double[]>();   // [wydatki spoza koszyka, fracht] srednia 28 dob (zl/d)
        private static readonly Dictionary<Village, long> _vilOutDay = new Dictionary<Village, long>(), _vilFrDay = new Dictionary<Village, long>();

        /// <summary>Kiesa wsi zaplacila poza koszykiem ZC4 (danina wojenna, rynek wsi BK, oplata stala, stara lista - tylko wies zastosowana, podatek od nadwyzki).</summary>
        internal static void KsVilOut(Settlement st, long x)
        {
            if (x <= 0 || st == null || !st.IsVillage || st.Village == null || !KeyOn) return;
            long o; _vilOutDay.TryGetValue(st.Village, out o); _vilOutDay[st.Village] = o + x;
        }

        /// <summary>KS-H: fracht wozow ladem do kiesy wsi (wplyw - alfa netto).</summary>
        internal static void KsVilFreight(Village v, long x)
        {
            if (x <= 0 || v == null) return;
            long o; _vilFrDay.TryGetValue(v, out o); _vilFrDay[v] = o + x;
        }

        /// <summary>MoneyEma: srednie 28 dob wydatkow spoza koszyka i frachtu wsi (waga jak utarg wsi).</summary>
        private static readonly double[] _outMkDay = new double[2], _frMkDay = new double[2];   // wydatki spoza koszyka i fracht wsi doby wedlug rynku (linia ZC4, NKS-13)

        private static void VilOutDaily(double wv)
        {
            _outMkDay[0] = _outMkDay[1] = _frMkDay[0] = _frMkDay[1] = 0;
            foreach (var kv in _vilOutDay) { int m = MarketOfSettlement(kv.Key.Settlement); if (m == 0 || m == 1) _outMkDay[m] += kv.Value; }
            foreach (var kv in _vilFrDay) { int m = MarketOfSettlement(kv.Key.Settlement); if (m == 0 || m == 1) _frMkDay[m] += kv.Value; }
            foreach (var kv in _vilOutDay) { double[] a; if (!_vilOut.TryGetValue(kv.Key, out a)) { a = new double[2]; _vilOut[kv.Key] = a; } }
            foreach (var kv in _vilFrDay) { double[] a; if (!_vilOut.TryGetValue(kv.Key, out a)) { a = new double[2]; _vilOut[kv.Key] = a; } }
            foreach (var kv in _vilOut)
            {
                long o, f; _vilOutDay.TryGetValue(kv.Key, out o); _vilFrDay.TryGetValue(kv.Key, out f);
                kv.Value[0] += (o - kv.Value[0]) * wv; kv.Value[1] += (f - kv.Value[1]) * wv;
            }
            _vilOutDay.Clear(); _vilFrDay.Clear();
        }

        // ------------------------------------------------------------ popyt zaplacony miasta (KS-H, sterowanie, polki)
        /// <summary>Popyt zaplacony miasta przy lambda 1 (szt./d, pozycja koszyka z): lud miasta + wsie + wsad ZC5 + odbior panow i dworow (P3); 0 - poza ZC4.</summary>
        internal static double ZDp(Town t, int z)
        {
            if (t == null || t.Settlement == null || z < 0 || z >= ZN) return 0.0;
            var map = _zt; ZTown zt;
            if (map.Count == 0 || !map.TryGetValue(t.Settlement, out zt) || zt == null) return 0.0;
            double v = zt.Q[z] + zt.QV[z] + zt.Der[z] + zt.One[z];
            return v > 0 && !double.IsNaN(v) ? v : 0.0;
        }

        /// <summary>Dni polki celu T* pozycji koszyka z na rynku m: 7 x cena startu^(-1/0.6).</summary>
        internal static double ZTstar(int m, int z)
        {
            // pamiec doby (drabina 2 wsi pyta o cel polki tysiace razy na dobe): ten sam Plan i ta sama doba - te same dni
            int mm = m == 0 ? 0 : 1;
            if (z < 0 || z >= ZN) return ShelfDays(1.0);
            var P = _plan != null ? _plan.P : null;
            int today = Today();
            if (_tsDay != today || !ReferenceEquals(_tsPlan, P)) { _tsDay = today; _tsPlan = P; for (int i = 0; i < _tsCache.Length; i++) _tsCache[i] = -1; }
            double v = _tsCache[mm * ZN + z];
            if (v < 0) { v = ShelfDays(KsCore.StartIndex(ZIds[z], P, mm)); _tsCache[mm * ZN + z] = v; }
            return v;
        }
        private static readonly double[] _tsCache = new double[2 * KsBasket.N];
        private static int _tsDay = -1; private static double[] _tsPlan;

        // ------------------------------------------------------------ linie i plik
        private static int _shelfLineDay = -1, _csvGoodsDay = -1;
        private static readonly int[] ShelfGoods = { ZGrain, ZFish, ZMeat, ZCheese, ZButter, ZOlives, ZSalt, ZGrape };

        /// <summary>Linia "KS: polki miast dzien N" (stan zastosowany, od D): dni polki / T* (q10/med/q90), miast z polka < 0.5 dnia i < 2 dni, na suficie / podlodze ceny.</summary>
        private static void ShelfLine(int day)
        {
            if (_shelfLineDay == day || !LogGate.Lines || DDay < 0 || day < DDay) return;
            _shelfLineDay = day;
            var sb = new StringBuilder(1500);
            sb.Append(PK).Append("polki miast dzien ").Append(day).Append(" (dni polki = polka / popyt zaplacony przy lambda 1; q10/med/q90 dni/cel T*)");
            var v = new List<double>(120);
            for (int m = 0; m < 2; m++)
            {
                sb.Append(m == 0 ? " | Westeros:" : " | Essos:");
                foreach (int z in ShelfGoods)
                {
                    v.Clear(); int lo = 0, lo2 = 0, ceil = 0, floor = 0, n = 0;
                    var it = _zItems != null ? _zItems[z] : null;
                    double T = ZTstar(m, z);
                    foreach (var zt in _zt.Values)
                    {
                        if (zt == null || zt.St == null || zt.Market != m) continue;
                        double dp = zt.Q[z] + zt.QV[z] + zt.Der[z] + zt.One[z]; if (!(dp > 0.5)) continue;
                        double d = zt.Cnt[z] / dp;
                        n++; v.Add(d / Math.Max(0.1, T));
                        if (d < 0.5) lo++; if (d < 2) lo2++;
                        if (it != null && it.ItemCategory != null) { float x = 1f; try { x = zt.T.GetItemCategoryPriceIndex(it.ItemCategory); } catch { } if (x >= CeilIx) ceil++; else if (x <= FloorIx) floor++; }
                    }
                    if (n == 0) continue;
                    v.Sort();
                    sb.Append(' ').Append(ZIds[z]).Append(' ').Append(v[(int)(0.1 * (n - 1))].ToString("0.00", Inv)).Append('/').Append(v[(n - 1) / 2].ToString("0.00", Inv)).Append('/').Append(v[(int)(0.9 * (n - 1))].ToString("0.00", Inv))
                      .Append(" (T* ").Append(T.ToString("0.0", Inv)).Append(" d; < 0.5 d ").Append((100 * lo / n).ToString(Inv)).Append("%, < 2 d ").Append((100 * lo2 / n).ToString(Inv)).Append("%, sufit/podloga ")
                      .Append((100 * ceil / n).ToString(Inv)).Append('/').Append((100 * floor / n).ToString(Inv)).Append("%; miast ").Append(n).Append(')');
                }
            }
            sb.Append(" | w drodze KS-H ").Append(KsFoodCarts.InTransitPieces()).Append(" szt.");
            Log.Info(sb.ToString());
        }

        private const string GoodsCsvName = "ks-miasta-towary.csv";
        private const string GoodsCsvHeader = "dzien;miasto_id;rynek;towar;indeks;polka_szt;popyt_zaplacony_szt_d;odbior_7d_szt_d;w_drodze_szt;kx;cel_dni";

        /// <summary>Plik ks-miasta-towary.csv co 7 dob (od D): miasto x zboze, ryby, mieso, ser, maslo, oliwki, sol, winogrona - indeks, polka, popyt zaplacony, odbior 7 dob,
        /// w drodze KS-H, kx.</summary>
        private static void GoodsCsv(int day)
        {
            if (day % 7 != 0 || day == _csvGoodsDay || !LogGate.Lines) return;
            _csvGoodsDay = day;
            var sb = new StringBuilder(_zt.Count * 8 * 70);
            int rows = 0;
            foreach (var zt in _zt.Values)
            {
                if (zt == null || zt.St == null) continue;
                int ti = KsFoodCarts.IndexOf(zt.St);
                foreach (int z in ShelfGoods)
                {
                    var it = _zItems != null ? _zItems[z] : null;
                    float x = 1f; if (it != null && it.ItemCategory != null) { try { x = zt.T.GetItemCategoryPriceIndex(it.ItemCategory); } catch { } }
                    int k = Array.IndexOf(KsFoodCarts.Z, z);
                    sb.Append(day).Append(';').Append(zt.St.StringId).Append(';').Append(zt.Market == 0 ? "Westeros" : "Essos").Append(';').Append(ZIds[z]).Append(';').Append(x.ToString("0.00", Inv)).Append(';')
                      .Append(zt.Cnt[z]).Append(';').Append((zt.Q[z] + zt.QV[z] + zt.Der[z] + zt.One[z]).ToString("0.0", Inv)).Append(';').Append(ZOff7(zt.St, z).ToString("0.0", Inv)).Append(';')
                      .Append(KsFoodCarts.Incoming(zt.St, z)).Append(';').Append((k >= 0 ? KsFoodCarts.Kx(ti, k) : 1.0).ToString("0.00", Inv)).Append(';').Append(ZTstar(zt.Market, z).ToString("0.0", Inv)).Append(Environment.NewLine);
                    rows++;
                }
            }
            string path = Log.Csv(GoodsCsvName, GoodsCsvHeader, sb.ToString());
            Log.Info(PK + GoodsCsvName + " " + (path != null ? rows + " wierszy (doba " + day + ")" : "NIE ZAPISANY (log wylaczony albo brak katalogu sesji)") + ".");
        }

        // ------------------------------------------------------------ zapis ks4: pole dodatkowe (spakowane) - sekcje "klucz=wartosc" po liniach

        private static string ExportExtra()
        {
            var sb = new StringBuilder(16 * 1024);
            // G: gradient krain (kraina:g36:wartosc x 1000 w 3 znakach base36,...)
            if (_grad != null && _grad.Count > 0)
            {
                sb.Append("G=");
                bool f = true;
                foreach (var kv in _grad)
                {
                    if (kv.Value == null) continue;
                    foreach (int g in KsCore.Food)
                    {
                        if (g >= kv.Value.Length || Math.Abs(kv.Value[g] - 1.0) < 0.0005) continue;
                        if (!f) sb.Append(','); f = false;
                        sb.Append(Clean(kv.Key)).Append(':').Append(B36[g]).Append(E36(Math.Max(0, Math.Min(46655, (int)Math.Round(kv.Value[g] * 1000))), 3));
                    }
                }
                sb.Append('\n');
            }
            if (_mf != null) { sb.Append("M="); for (int i = 0; i < _mf.Length; i++) { if (i > 0) sb.Append(','); sb.Append(_mf[i].ToString("0.####", Inv)); } sb.Append('\n'); }
            if (_landCut.Count > 0)
            {
                sb.Append("L="); bool f = true;
                foreach (var kv in _landCut) foreach (int g in KsCore.Food) if (kv.Value[g] < 0.999) { if (!f) sb.Append(','); f = false; sb.Append(Clean(kv.Key)).Append(':').Append(g).Append(':').Append(kv.Value[g].ToString("0.###", Inv)); }
                sb.Append('\n');
            }
            if (_landShelfPrev.Count > 0)
            {
                sb.Append("P="); bool f = true;
                foreach (var kv in _landShelfPrev) foreach (int g in KsCore.Food) if (kv.Value[g] > 0) { if (!f) sb.Append(','); f = false; sb.Append(Clean(kv.Key)).Append(':').Append(g).Append(':').Append(F0(kv.Value[g])); }
                sb.Append('\n');
            }
            if (_cRestart >= 0) sb.Append("R=").Append(_cRestart).Append('\n');
            sb.Append("C=").Append(_cN);
            for (int i = 0; i < _c28.Length; i++) if (_c28[i] > 0 || _u28[i] > 0) sb.Append(',').Append(i).Append(':').Append(_c28[i].ToString("0.#", Inv)).Append(':').Append(_u28[i].ToString("0.#", Inv));
            sb.Append('\n');
            string kx = KsFoodCarts.ExportKx(); if (!string.IsNullOrEmpty(kx)) sb.Append("K=").Append(kx).Append('\n');
            string dd = KsFoodCarts.ExportDays(); if (!string.IsNullOrEmpty(dd)) sb.Append("D=").Append(dd).Append('\n');
            if (_floor.Count > 0)
            {
                sb.Append("F="); bool f = true;
                foreach (var kv in _floor) { if (kv.Key == null || kv.Key.Settlement == null || (kv.Value[0] == 1 && kv.Value[1] == 0 && kv.Value[2] == 0)) continue; if (!f) sb.Append(','); f = false; sb.Append(Clean(kv.Key.Settlement.StringId)).Append(':').Append(kv.Value[0]).Append(':').Append(kv.Value[1]).Append(':').Append(kv.Value[2]); }
                sb.Append('\n');
            }
            if (_vilOut.Count > 0)
            {
                sb.Append("O="); bool f = true;
                foreach (var kv in _vilOut) { if (kv.Key == null || kv.Key.Settlement == null || !(kv.Value[0] > 0.01 || kv.Value[1] > 0.01)) continue; if (!f) sb.Append(','); f = false; sb.Append(Clean(kv.Key.Settlement.StringId)).Append(':').Append(kv.Value[0].ToString("0.#", Inv)).Append(':').Append(kv.Value[1].ToString("0.#", Inv)); }
                sb.Append('\n');
            }
            if (_one28 != null)
            {
                // T: odbior miast (srednie) - miasto KS-H:pozycja:28 dob:7 dob (tylko niezerowe)
                sb.Append("T=").Append(_tkN).Append(':').Append(_tkNt);
                for (int i = 0; i < _one28.Length; i++) if (_one28[i] > 0.05 || _off7[i] > 0.05) sb.Append(',').Append(i).Append(':').Append(_one28[i].ToString("0.#", Inv)).Append(':').Append(_off7[i].ToString("0.#", Inv));
                sb.Append('\n');
            }
            if (_gold28 != null) { sb.Append("A=").Append(_goldN); foreach (var g in _gold28) sb.Append(',').Append(F0(g)); sb.Append('\n'); }
            // recenzja runda 3 (wazne): srednie dowozu korytarzy i rurociagu KS-H (Plan po wczytaniu), serie "wies je swoje" (NKS-25)
            { string h = KsFoodCarts.ExportEma(); if (!string.IsNullOrEmpty(h)) sb.Append("H=").Append(h).Append('\n'); }
            if (_ownStreak.Count > 0)
            {
                sb.Append("S="); bool f = true;
                foreach (var kv in _ownStreak) { if (kv.Key == null || kv.Key.Settlement == null || kv.Value <= 0) continue; if (!f) sb.Append(','); f = false; sb.Append(Clean(kv.Key.Settlement.StringId)).Append(':').Append(kv.Value); }
                sb.Append('\n');
            }
            return sb.Length > 0 ? Pack(sb.ToString()) : "";
        }

        /// <summary>Odczyt pola dodatkowego ks4 (po ResolvePending; obiekty gry juz sa). Zly wpis - pominiety.</summary>
        private static void ImportExtra(string packed)
        {
            T4Bump();                                            // ks-koszt T4: gradient i ciecie krain z zapisu - pamiec progow od zera
            if (string.IsNullOrEmpty(packed)) return;
            string s;
            try { s = Unpack(packed); } catch (Exception e) { Stumble("ImportExtra(rozpakowanie)", e); return; }
            foreach (var line in s.Split('\n'))
            {
                if (line.Length < 2 || line[1] != '=') continue;
                string v = line.Substring(2);
                try
                {
                    switch (line[0])
                    {
                        case 'G':
                            _grad = new Dictionary<string, double[]>(StringComparer.Ordinal);
                            foreach (var e in v.Split(','))
                            {
                                int c = e.LastIndexOf(':'); if (c <= 0 || e.Length - c - 1 != 4) continue;
                                string land = e.Substring(0, c); int g = B36.IndexOf(e[c + 1]); int x = D36(e, c + 2, 3);
                                if (g < 0 || g >= KsTab.NG || x < 0) continue;
                                double[] a; if (!_grad.TryGetValue(land, out a)) { a = new double[KsTab.NG]; for (int k = 0; k < a.Length; k++) a[k] = 1.0; _grad[land] = a; }
                                a[g] = Math.Max(KsCore.CorrMinG, Math.Min(KsCore.CorrMaxG, x / 1000.0));
                            }
                            break;
                        case 'M':
                            {
                                var p = v.Split(','); if (p.Length != 2 * KsTab.NG) break;
                                var mf = new double[p.Length];
                                for (int i = 0; i < p.Length; i++) { double d; mf[i] = double.TryParse(p[i], NumberStyles.Float, Inv, out d) && d >= 0.85 && d <= 1.10 ? d : ShelfMargin; }
                                _mf = mf;
                                break;
                            }
                        case 'L':
                            foreach (var e in v.Split(','))
                            {
                                var p = e.Split(':'); int g; double d;
                                if (p.Length != 3 || !int.TryParse(p[1], out g) || g < 0 || g >= KsTab.NG || !double.TryParse(p[2], NumberStyles.Float, Inv, out d)) continue;
                                double[] a; if (!_landCut.TryGetValue(p[0], out a)) { a = new double[KsTab.NG]; for (int k = 0; k < a.Length; k++) a[k] = 1.0; _landCut[p[0]] = a; }
                                a[g] = Math.Max(0.5, Math.Min(1.0, d));
                            }
                            break;
                        case 'P':
                            foreach (var e in v.Split(','))
                            {
                                var p = e.Split(':'); int g; double d;
                                if (p.Length != 3 || !int.TryParse(p[1], out g) || g < 0 || g >= KsTab.NG || !double.TryParse(p[2], NumberStyles.Float, Inv, out d)) continue;
                                double[] a; if (!_landShelfPrev.TryGetValue(p[0], out a)) { a = new double[KsTab.NG]; _landShelfPrev[p[0]] = a; }
                                a[g] = d;
                            }
                            break;
                        case 'C':
                            {
                                var p = v.Split(',');
                                int.TryParse(p[0], out _cN); _cN = Math.Max(0, Math.Min(28, _cN));
                                for (int i = 1; i < p.Length; i++)
                                {
                                    var q = p[i].Split(':'); int k; double c, u;
                                    if (q.Length == 3 && int.TryParse(q[0], out k) && k >= 0 && k < _c28.Length && double.TryParse(q[1], NumberStyles.Float, Inv, out c) && double.TryParse(q[2], NumberStyles.Float, Inv, out u)) { _c28[k] = c; _u28[k] = u; }
                                }
                                break;
                            }
                        case 'R': { int rd; if (int.TryParse(v, out rd)) _cRestart = rd; break; }
                        case 'K': KsFoodCarts.ImportKx(v); break;
                        case 'D': KsFoodCarts.ImportDays(v); break;
                        case 'F':
                            foreach (var e in v.Split(','))
                            {
                                var p = e.Split(':'); int a0, a1, a2;
                                if (p.Length != 4 || !int.TryParse(p[1], out a0) || !int.TryParse(p[2], out a1) || !int.TryParse(p[3], out a2)) continue;
                                var st = Settlement.Find(p[0]); if (st == null || st.Village == null) continue;
                                _floor[st.Village] = new[] { a0 == 0 ? 0 : 1, Math.Max(0, a1), Math.Max(0, a2) };
                            }
                            break;
                        case 'O':
                            foreach (var e in v.Split(','))
                            {
                                var p = e.Split(':'); double o, f;
                                if (p.Length != 3 || !double.TryParse(p[1], NumberStyles.Float, Inv, out o) || !double.TryParse(p[2], NumberStyles.Float, Inv, out f)) continue;
                                var st = Settlement.Find(p[0]); if (st == null || st.Village == null) continue;
                                _vilOut[st.Village] = new[] { Math.Max(0.0, o), Math.Max(0.0, f) };
                            }
                            break;
                        case 'T':
                            {
                                var p = v.Split(',');
                                var h = p[0].Split(':'); int n, nt;
                                if (h.Length != 2 || !int.TryParse(h[0], out n) || !int.TryParse(h[1], out nt) || nt != KsFoodCarts.TownCount || nt <= 0) break;
                                _one28 = new double[nt * ZN]; _off7 = new double[nt * ZN]; _tkN = Math.Max(0, Math.Min(28, n));
                                for (int i = 1; i < p.Length; i++)
                                {
                                    var q = p[i].Split(':'); int k; double a, b;
                                    if (q.Length == 3 && int.TryParse(q[0], out k) && k >= 0 && k < _one28.Length && double.TryParse(q[1], NumberStyles.Float, Inv, out a) && double.TryParse(q[2], NumberStyles.Float, Inv, out b)) { _one28[k] = a; _off7[k] = b; }
                                }
                                break;
                            }
                        case 'H': KsFoodCarts.ImportEma(v); break;
                        case 'S':
                            foreach (var e in v.Split(','))
                            {
                                var p = e.Split(':'); int a0;
                                if (p.Length != 2 || !int.TryParse(p[1], out a0) || a0 <= 0) continue;
                                var st = Settlement.Find(p[0]); if (st == null || st.Village == null) continue;
                                _ownStreak[st.Village] = a0;
                            }
                            break;
                        case 'A':
                            {
                                var p = v.Split(','); int n;
                                if (!int.TryParse(p[0], out n) || p.Length - 1 != KsFoodCarts.TownCount) break;
                                _gold28 = new double[p.Length - 1]; _goldN = Math.Max(0, Math.Min(28, n));
                                for (int i = 1; i < p.Length; i++) { double g; if (double.TryParse(p[i], NumberStyles.Float, Inv, out g)) _gold28[i - 1] = g; }
                                break;
                            }
                    }
                }
                catch (Exception e) { Stumble("ImportExtra(" + line[0] + ")", e); }
            }
        }

        // ------------------------------------------------------------ drabina 2 wsi (linia ZC4; INFO): [kupione, nie wystawione (polka ponizej celu), bez kiesy ponad zapas]
        private static readonly double[] _p2 = new double[3];
        internal static void Zc4NotePass2(double got, double notOffered, double noGold) { _p2[0] += got; _p2[1] += notOffered; _p2[2] += noGold; }

        /// <summary>Cel polki pozycji z miasta t (szt.): T* x popyt zaplacony przy lambda 1.</summary>
        internal static double ZStallTarget(Town t, int z)
        {
            if (t == null) return 0.0;
            return ZTstar(TownMarket(t), z) * ZDp(t, z);
        }

        // KS-R3 (port symulatora 17.10, runda 3): drabina 2 wsi kupuje tylko nadmiar polki ponad 1.5 x cel (cena najmniej ok. 1/4 ponizej startu) - pas 1.25-1.5 x cel
        // zostaje kupcom zywnosci KS-H (ich zrodlo trzyma 1.25 x T* x Dz i sprzedaje polowe reszty na dobe). Dotad drabina 2 brala wszystko ponad 1.0 x cel PRZED KS-H
        // (zakupy wsi w TownFolk.Daily, KS-H w SupplyDemand.DailyTrade): wsie przy miastach z nadwyzka zjadaly z oszczednosci towar, ktory kupcy wiezliby do miast pustych
        // (symulator, okno D+22..D+49: mediany cen w pasmie 12 -> 17 z 26 towarow, miasta z ziarnem < 2 dni 18% -> 13%, kiesy wsi -0.39/-0.55 -> -0.28/-0.43 %/d)
        // recenzja runda 3 (drobne): zamiast stalego pasa 1.5 x celu - cena: drabina 2 kupuje, dopoki cena polki jest najwyzej P2Res x ceny startu krainy, i najwyzej do polki,
        // przy ktorej cena wzoru doszlaby do P2Res x startu (dni polki przy indeksie P2Res x startu: 0.8^(-1/0.6) = ok. 1.45 x cel)
        internal const double P2Res = 0.8;

        /// <summary>Prog drabiny 2 wsi w miescie t (szt.): dni polki przy indeksie P2Res x startu (krainy) x popyt zaplacony.</summary>
        internal static double ZPass2Floor(Town t, int z)
        {
            if (t == null || z < 0 || z >= ZN) return 0.0;
            int m = TownMarket(t);
            double I = KsCore.StartIndex(ZIds[z], _plan != null ? _plan.P : null, m == 0 ? 0 : 1) * LandGradZ(t.Settlement, z);
            return ShelfDays(P2Res * I) * ZDp(t, z);
        }

        /// <summary>Cena rezerwacji drabiny 2 (zl za sztuke): P2Res x cena startu krainy.</summary>
        internal static double ZPass2Price(Town t, int z)
        {
            var it = z >= 0 && z < ZN ? ZItem(z) : null; if (t == null || it == null) return 0.0;
            int m = TownMarket(t);
            return P2Res * Math.Max(1, it.Value) * KsCore.StartIndex(ZIds[z], _plan != null ? _plan.P : null, m == 0 ? 0 : 1) * LandGradZ(t.Settlement, z);
        }
    }
}
