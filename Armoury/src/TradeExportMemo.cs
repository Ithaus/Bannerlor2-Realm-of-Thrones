using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// E8/O3 (267, PLAN 8.17; RD8 K4 / O3) - WYWOZ KUPCOW: CELE W ZASIEGU Z PAMIECI. Optymalizacja: te same pary zrodlo-cel, sztuki i zloto (R9), mniej pracy.
    /// Wzor Z33: wywoz to jedna klatka 608 ms (doba 1) -> 312.5 ms (doba 120); ok. 4-5 tys. wyborow celu na dobe, kazdy przechodzil wszystkie 227 osad
    /// (odleglosc do kazdej) i dla osad w zasiegu liczyl od nowa popyt i mnoznik ceny. Dwie rzeczy, jeden wylacznik TradeExportFast (off = kod sprzed paczki):
    ///  1. cele w zasiegu kazdego zrodla z pamieci Z KLUCZEM: zbior osad w kolejnosci spisu (te same obiekty), ich pozycje i zasieg z ustawien sa sprawdzane
    ///     przy kazdym wywozie (227 odczytow); inna osada, pozycja albo zasieg = pamiec od zera. Kolejnosc celow jak w spisie (te same remisy przy
    ///     "profit &lt;= bestProfit"). Zamki przy GarrisonCarts, oblezenia, biedni odbiorcy i towar w drodze (TradeCarts.Incoming) zostaja w petli - pamiec
    ///     od nich nie zalezy (dlatego GarrisonCarts.On nie jest czescia klucza: konie jada do zamkow, bron nie - filtr zalezy od koszyka);
    ///  2. popyt osady (SupplyDemand.Demand) i mnoznik ceny (ArmsPricing.Multiplier) raz na osade w jednym koszyku (typ x tier) - zaleza od osady, koszyka,
    ///     dobrobytu, zamowien i wskaznika surowcow, a w czasie wywozu nic ich nie zmienia (zamowienia dopisuja tylko zakupy AI, kowal gracza i ochotnicy;
    ///     wskaznik surowcow i premie liczy ArmsPricing.Daily). Ta sama pamiec sluzy zrodlu (nadwyzka, mnoznik jego polki).
    /// R9: co 64. wybor celu liczony tez stara droga (petla w SupplyDemand.DailyTradeCore bez zmian) - porownanie celu, liczby sztuk do wziecia, drogi i zysku,
    /// oba czasy; co 64. popyt zrodla z pamieci liczony tez na zywo. Rozjazd = wynik starej drogi, licznik, najwyzej 3 linie "ROZJAZD", reszta wywozu tej doby
    /// stara droga i pamiec celow od zera przy nastepnym wywozie. Wyjatek w nowej drodze = potkniecie (licznik, jedna linia Log.Error) i stara droga dla tego
    /// jednego wywolania - bez globalnego wylacznika. Linia doby (sam log) "Wywoz kupcow (E8/O3)". Nic w zapisie, zadnego zlota ani sztuki; tylko watek gry
    /// (takt doby). Stan czyszczony w SupplyDemand.ResetOrders (nowa kampania = nowe obiekty osad).
    /// </summary>
    internal static class TradeExportMemo
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.TradeExportFast; } }

        // ------------------------------------------------------------ pamiec celow w zasiegu (klucz: osady w kolejnosci spisu, pozycje, zasieg)
        private static Settlement[] _places;
        private static Vec2[] _pos;
        private static float _range;
        private static int[][] _near;        // zrodlo i -> indeksy celow w zasiegu, rosnaco (kolejnosc spisu)
        private static float[][] _nearDist;  // odleglosc do kazdego z nich (ta sama liczba, ktora liczyla petla)
        private static int _pairs, _builds;

        // ------------------------------------------------------------ stan jednego wywozu i jednego koszyka
        private static Dictionary<int, int>[] _stockAt;   // spis polek tej doby wedlug indeksu osady (te same slowniki co w DailyTradeCore)
        private static float[] _dem, _mul;
        private static int[] _demAt, _mulAt;              // numer koszyka, w ktorym liczba powstala
        private static int _serial;
        private static ItemObject _probe;
        // 299b po recenzji (koszt): wiersz pamieci polki kazdego celu raz na wywoz doby (ShelfMemory.Row), miejsce koszyka raz na koszyk, udzial od razu raz na wywoz
        // (299c: RawPrice.Share - waga polki teraz w S_eff; tempo pamieci nie wchodzi do wyceny)
        private static float[][] _memRow;
        private static int _slot = -1;
        private static float _share;
        private static bool _ready, _dayOff;

        // wynik nowej drogi dla jednego wyboru celu (miedzy PickStart a PickEnd)
        private static Settlement _fBest;
        private static float _fProfit, _fDist, _fIdx;
        private static int _fCap;
        private static long _t1;

        // liczniki doby (sam log)
        private static int _dMem, _dPickNew, _dPickOld, _dDemHit, _dDemNew, _dMulHit, _dMulNew;
        private static int _dProbePick, _dProbeTarget, _dProbePieces, _dProbeSrc, _tPickN, _tSrcN;
        private static long _dProbePaid, _tPickOld, _tPickNew, _tSrcOld, _tSrcNew;
        private static bool _dRan;
        private static int _dPlaces, _dMoved, _dDeals, _dStuck, _dEvals;
        private static long _dPaid;
        private static float _dRange;
        private static double _dMs;
        // liczniki sesji
        private static int _picks, _srcReads, _mismatch, _misTarget, _misPieces, _misSrc, _mismatchLogged, _stumbles;
        private static bool _errLogged;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (!_errLogged) { _errLogged = true; Log.Error("TradeExportMemo." + where, e); }   // raz na sesje; reszta w liczniku potkniec
        }

        /// <summary>Nazwa osady do linii ROZJAZD - nigdy wyjatek (wyjatek przy skladaniu opisu zamienilby rozjazd w potkniecie).</summary>
        private static string Nm(Settlement s)
        {
            try { return s == null ? "brak" : s.Name != null ? s.Name.ToString() : "?"; } catch { return "?"; }
        }

        /// <summary>kind: 0 cel, 1 sztuki albo droga, 2 zrodlo (popyt albo mnoznik ceny zrodla).</summary>
        private static void Mismatch(int kind, string detail)
        {
            _mismatch++;
            if (kind == 0) _misTarget++; else if (kind == 1) _misPieces++; else _misSrc++;
            _dayOff = true; _places = null;   // reszta wywozu tej doby stara droga; pamiec celow od zera przy nastepnym wywozie
            if (_mismatchLogged >= 3) return;
            _mismatchLogged++;
            Log.Info("Wywoz kupcow (E8/O3): ROZJAZD " + (kind == 0 ? "celu" : kind == 1 ? "sztuk albo drogi" : "zrodla") + " - " + detail
                     + " - wziety wynik starej drogi, reszta wywozu tej doby stara droga, pamiec celow od zera przy nastepnym wywozie; po 3 liniach dalsze rozjazdy tylko w liczniku linii doby.");
        }

        /// <summary>Nowa gra / wczytanie (z SupplyDemand.ResetOrders): pamiec i liczniki od zera.</summary>
        internal static void Reset()
        {
            _places = null; _pos = null; _near = null; _nearDist = null; _pairs = 0; _builds = 0; _range = 0f;
            _stockAt = null; _dem = null; _mul = null; _demAt = null; _mulAt = null; _probe = null; _ready = false; _dayOff = false; _fBest = null;
            _memRow = null; _slot = -1;
            _picks = _srcReads = _mismatch = _misTarget = _misPieces = _misSrc = _mismatchLogged = _stumbles = 0; _errLogged = false;
            ClearDay();
        }

        private static void ClearDay()
        {
            _dMem = _dPickNew = _dPickOld = _dDemHit = _dDemNew = _dMulHit = _dMulNew = 0;
            _dProbePick = _dProbeTarget = _dProbePieces = _dProbeSrc = _tPickN = _tSrcN = 0;
            _dProbePaid = _tPickOld = _tPickNew = _tSrcOld = _tSrcNew = 0L;
            _dRan = false; _dPlaces = _dMoved = _dDeals = _dStuck = _dEvals = 0; _dPaid = 0L; _dRange = 0f; _dMs = 0.0;
        }

        // ------------------------------------------------------------ 1. cele w zasiegu z pamieci
        /// <summary>Poczatek wywozu doby, po spisie polek. true = nowa droga gotowa (pamiec celow sprawdzona z kluczem albo zbudowana od nowa).
        /// false = klucz wylaczony albo potkniecie - caly wywoz tej doby stara droga.</summary>
        internal static bool Begin(List<Settlement> places, Dictionary<Settlement, Dictionary<int, int>> stock, float range)
        {
            _ready = false; _dayOff = false; _fBest = null; _dMem = 0;
            if (!On) return false;
            try
            {
                int n = places.Count;
                bool same = _places != null && _places.Length == n && _range.Equals(range);
                for (int i = 0; same && i < n; i++)
                {
                    var p = places[i].GetPosition2D;
                    if (!ReferenceEquals(_places[i], places[i]) || !p.x.Equals(_pos[i].x) || !p.y.Equals(_pos[i].y)) same = false;
                }
                if (same) _dMem = 1;
                else { _places = null; Build(places, range); _dMem = 2; _builds++; }
                if (_stockAt == null || _stockAt.Length != n)
                {
                    _stockAt = new Dictionary<int, int>[n];
                    _dem = new float[n]; _mul = new float[n]; _demAt = new int[n]; _mulAt = new int[n];
                }
                for (int i = 0; i < n; i++) _stockAt[i] = stock[places[i]];
                if (_memRow == null || _memRow.Length != n) _memRow = new float[n][];
                for (int i = 0; i < n; i++) _memRow[i] = ShelfMemory.Row(places[i]);
                _share = (float)RawPrice.Share;
                _ready = true;
                return true;
            }
            catch (Exception e) { Stumble("Begin", e); _places = null; _ready = false; return false; }
        }

        private static void Build(List<Settlement> places, float range)
        {
            int n = places.Count;
            var pl = places.ToArray();
            var pos = new Vec2[n];
            for (int i = 0; i < n; i++) pos[i] = pl[i].GetPosition2D;
            var near = new int[n][];
            var nd = new float[n][];
            var ti = new List<int>();
            var td = new List<float>();
            int pairs = 0;
            for (int i = 0; i < n; i++)
            {
                ti.Clear(); td.Clear();
                var srcPos = pos[i];
                for (int j = 0; j < n; j++)
                {
                    if (ReferenceEquals(pl[j], pl[i])) continue;   // jak "dst == src" w petli
                    float dist = srcPos.Distance(pos[j]);
                    if (dist > range) continue;                    // ten sam warunek co w petli (odleglosc rowna zasiegowi zostaje)
                    ti.Add(j); td.Add(dist);
                }
                near[i] = ti.ToArray(); nd[i] = td.ToArray(); pairs += ti.Count;
            }
            _pos = pos; _near = near; _nearDist = nd; _range = range; _pairs = pairs;
            _places = pl;   // na koncu: wyjatek w trakcie budowy zostawia pamiec pusta
        }

        // ------------------------------------------------------------ 2. popyt i mnoznik ceny raz na osade w koszyku
        /// <summary>Poczatek koszyka (typ x tier): liczby poprzedniego koszyka przestaja byc wazne.</summary>
        internal static void Bucket(ItemObject probe) { _serial++; _probe = probe; _slot = ShelfMemory.Slot(probe); }

        private static float Dem(int i)
        {
            if (_demAt[i] == _serial) { _dDemHit++; return _dem[i]; }
            float d = SupplyDemand.Demand(_places[i], _probe);
            _dem[i] = d; _demAt[i] = _serial; _dDemNew++;
            return d;
        }

        private static float Mul(int i)
        {
            if (_mulAt[i] == _serial) { _dMulHit++; return _mul[i]; }
            float m = ArmsPricing.Multiplier(_places[i], _probe);
            _mul[i] = m; _mulAt[i] = _serial; _dMulNew++;
            return m;
        }

        /// <summary>Popyt zrodla w koszyku (nadwyzka i mnoznik polki zrodla) z pamieci koszyka; co 64. odczyt z pamieci liczony tez na zywo (R9: wynik i czas).</summary>
        internal static float SrcDemand(int si, Settlement src, ItemObject probe)
        {
            if (!_ready || _dayOff) return SupplyDemand.Demand(src, probe);
            try
            {
                bool hit = _demAt[si] == _serial;
                float d = Dem(si);
                if (hit && (++_srcReads & 63) == 0)
                {
                    long t0 = Stopwatch.GetTimestamp();
                    float live = SupplyDemand.Demand(src, probe);
                    long t1 = Stopwatch.GetTimestamp();
                    Dem(si); _dDemHit--;   // odczyt pamieci jeszcze raz - tylko pomiar czasu nowej drogi (bez licznika trafien)
                    long t2 = Stopwatch.GetTimestamp();
                    _tSrcOld += t1 - t0; _tSrcNew += t2 - t1; _tSrcN++;
                    _dProbeSrc++;
                    if (!live.Equals(d))
                    {
                        Mismatch(2, "popyt " + Nm(src) + " na " + (probe != null ? probe.StringId : "?") + ": z pamieci " + d + ", na zywo " + live);
                        return live;
                    }
                }
                return d;
            }
            catch (Exception e) { Stumble("SrcDemand", e); return SupplyDemand.Demand(src, probe); }
        }

        // ------------------------------------------------------------ wybor celu
        /// <summary>Wybor celu dla zrodla si w koszyku key nowa droga. Zwraca: 0 - nie liczono (wolajacy liczy stara droga), 1 - wynik nowej drogi gotowy
        /// (oddaje go PickEnd; petla starej drogi nie biegnie), 2 - probka R9: wynik nowej drogi zapamietany, wolajacy liczy tez stara droge, PickEnd porownuje.
        /// srcIdx: mnoznik ceny u zrodla - przy 1 z pamieci koszyka, przy 0 i 2 na zywo (stara droga).</summary>
        internal static int PickStart(int si, Settlement src, ItemObject probe, int key, bool noCastlesHere, bool carts, HashSet<Settlement> poor, Settings c,
                                      float elast, float srcFactor, float perDist, out float srcIdx)
        {
            if (_ready && !_dayOff)
            {
                try
                {
                    bool sample = (++_picks & 63) == 0;
                    long t0 = sample ? Stopwatch.GetTimestamp() : 0L;
                    float fIdx = srcFactor * Mul(si);
                    PickCore(si, key, noCastlesHere, carts, poor, c, elast, fIdx, perDist);
                    if (!sample) { _dPickNew++; srcIdx = fIdx; return 1; }
                    _fIdx = fIdx;
                    _t1 = Stopwatch.GetTimestamp(); _tPickNew += _t1 - t0;
                    srcIdx = srcFactor * ArmsPricing.Multiplier(src, probe);
                    return 2;
                }
                catch (Exception e) { Stumble("PickStart", e); _fBest = null; }
            }
            _dPickOld++;
            srcIdx = srcFactor * ArmsPricing.Multiplier(src, probe);
            return 0;
        }

        /// <summary>Ta sama petla co w SupplyDemand.DailyTradeCore, tylko po celach w zasiegu z pamieci (w kolejnosci spisu) i z popytem / mnoznikiem z pamieci koszyka.</summary>
        private static void PickCore(int si, int key, bool noCastlesHere, bool carts, HashSet<Settlement> poor, Settings c, float elast, float srcIdx, float perDist)
        {
            Settlement best = null; float bestProfit = 0f; int bestCap = 0; float bestDist = 0f;
            float minF = MBMath.ClampFloat(c.SupplyDemandMinFactor, 0.01f, 1f), maxF = Math.Max(1f, c.SupplyDemandMaxFactor);
            var near = _near[si]; var nd = _nearDist[si];
            bool anyPoor = poor.Count > 0;
            for (int q = 0; q < near.Length; q++)
            {
                int j = near[q];
                var dst = _places[j];
                if (anyPoor && poor.Contains(dst)) continue;
                if (noCastlesHere && dst.IsCastle) continue;
                if (carts && dst.IsUnderSiege) continue;
                float dist = nd[q];
                int dh; _stockAt[j].TryGetValue(key, out dh);
                if (carts) dh += TradeCarts.Incoming(dst, key);
                float dd = Dem(j);
                float dstShelf = SupplyDemand.DstShelfRow(_memRow[j], _slot, _share, dd, dh, elast, minF, maxF);   // 299b: ta sama liczba co stara petla (DstShelf - pamiec polki celu)
                float profit = dstShelf * Mul(j) - srcIdx - perDist * dist;
                if (profit <= bestProfit) continue;
                best = dst; bestProfit = profit; bestCap = Math.Max(1, (int)Math.Ceiling(dd) - dh); bestDist = dist;
            }
            _fBest = best; _fProfit = bestProfit; _fCap = bestCap; _fDist = bestDist;
        }

        /// <summary>Po petli starej drogi (albo zamiast niej): pk 1 - oddaje wynik nowej drogi; pk 2 - probka R9: porownuje wynik starej drogi (w parametrach)
        /// z nowa; rozjazd = zostaje wynik starej drogi.</summary>
        internal static void PickEnd(int pk, Settlement src, ItemObject probe, float srcIdx, ref Settlement best, ref float bestProfit, ref int bestCap, ref float bestDist)
        {
            if (pk == 1) { best = _fBest; bestProfit = _fProfit; bestCap = _fCap; bestDist = _fDist; _fBest = null; return; }
            if (pk != 2) return;
            try
            {
                _tPickOld += Stopwatch.GetTimestamp() - _t1; _tPickN++;
                _dProbePick++; _dPickOld++;
                if (best != null) _dProbeTarget++;
                string who = Nm(src) + " / " + (probe != null ? probe.StringId : "?");
                if (!srcIdx.Equals(_fIdx))
                    Mismatch(2, "mnoznik ceny zrodla " + who + ": z pamieci " + _fIdx + ", na zywo " + srcIdx);
                else if (!ReferenceEquals(best, _fBest))
                    Mismatch(0, who + ": z pamieci " + Nm(_fBest) + " (zysk " + _fProfit + "), stara droga " + Nm(best) + " (zysk " + bestProfit + ")");
                else if (bestCap != _fCap || !bestDist.Equals(_fDist) || !bestProfit.Equals(_fProfit))
                    Mismatch(1, who + " -> " + Nm(best) + ": z pamieci sztuk " + _fCap + ", droga " + _fDist + ", zysk " + _fProfit
                                + "; stara droga sztuk " + bestCap + ", droga " + bestDist + ", zysk " + bestProfit);
            }
            catch (Exception e) { Stumble("PickEnd", e); }
            _fBest = null;
        }

        /// <summary>Po zdjeciu sztuk dla wyboru z probki (pk 2): ile sztuk i zlota poszlo za wyborem policzonym obiema drogami (sam licznik).</summary>
        internal static void SampleDeal(int pieces, long gold) { _dProbePieces += pieces; _dProbePaid += gold; }

        /// <summary>Koniec wywozu doby: liczby do linii (sam licznik).</summary>
        internal static void NoteDay(int places, float range, int moved, int deals, long paid, int stuck, int evals, double ms)
        {
            _dRan = true; _dPlaces = places; _dRange = range; _dMoved = moved; _dDeals = deals; _dPaid = paid; _dStuck = stuck; _dEvals = evals; _dMs = ms;
        }

        // ------------------------------------------------------------ linia doby
        private static string Us(long oldT, long newT, int n)
        {
            if (n <= 0) return "- / -";
            double us = 1000000.0 / Stopwatch.Frequency;
            var inv = CultureInfo.InvariantCulture;
            return (oldT * us / n).ToString("0.000", inv) + " / " + (newT * us / n).ToString("0.000", inv);
        }

        /// <summary>E8/O3 (267): linia doby "Wywoz kupcow (E8/O3)" (z konca SupplyDemand.DailyTrade) - sam log.</summary>
        internal static void Daily()
        {
            try
            {
                var s = Settings.Current;
                if (s != null && Campaign.Current != null)
                {
                    var inv = CultureInfo.InvariantCulture;
                    Log.Info("Wywoz kupcow (E8/O3): dzien " + ((int)CampaignTime.Now.ToDays - 1)
                             + " | TradeExportFast " + (s.TradeExportFast ? "[WL]" : "[wyl]")
                             + " | " + (_dRan ? "osad " + _dPlaces + ", zasieg " + _dRange.ToString("0.#", inv)
                                               : "wywozu tej doby nie bylo (prawo podazy i popytu wylaczone w MCM, udzial wywozu 0 albo blad wywozu)")
                             + " | pamiec celow: " + (_dMem == 1 ? "z pamieci" : _dMem == 2 ? "zbudowana od nowa" : "nieuzywana")
                             + ", budowana od startu sesji " + _builds + ", par zrodlo-cel w zasiegu " + (_places != null ? _pairs : 0)
                             + " | wybor celu: nowa droga " + _dPickNew + " / stara droga " + _dPickOld
                             + " | popyt osady w koszyku z pamieci " + _dDemHit + " / liczony " + _dDemNew
                             + " | mnoznik ceny z pamieci " + _dMulHit + " / liczony " + _dMulNew
                             + " | probka 1/64 (stara i nowa droga): wybor celu " + _dProbePick + " (z celem " + _dProbeTarget + ", wywiezione po nich " + _dProbePieces
                             + " szt. za " + _dProbePaid + " zl), popyt zrodla " + _dProbeSrc
                             + ", rozjazdow od startu sesji " + _mismatch + " (cel " + _misTarget + ", sztuki albo droga " + _misPieces + ", zrodlo " + _misSrc + ")"
                             + " | czas w probce, stara / nowa droga (us): wybor celu " + Us(_tPickOld, _tPickNew, _tPickN) + ", popyt zrodla " + Us(_tSrcOld, _tSrcNew, _tSrcN)
                             + " | wywoz tej doby: " + _dMoved + " szt. w " + _dDeals + " transakcjach, zaplacone " + _dPaid + " zl, bez odbiorcy " + _dStuck
                             + " szt., wycen " + _dEvals + ", czas wywozu " + _dMs.ToString("0.0", inv) + " ms"
                             + " | potkniecia od startu sesji " + _stumbles + ".");
                }
            }
            catch (Exception e) { Log.Error("TradeExportMemo.Daily", e); }
            ClearDay();
            // spis polek tej doby i wzorzec koszyka nie sa juz potrzebne (pamiec celow zostaje)
            if (_stockAt != null) Array.Clear(_stockAt, 0, _stockAt.Length);
            if (_memRow != null) Array.Clear(_memRow, 0, _memRow.Length);
            _probe = null; _slot = -1; _ready = false; _fBest = null;
        }
    }
}
