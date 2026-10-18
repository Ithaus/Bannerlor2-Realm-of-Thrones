using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// ks-koszt (noc 17/18.10, PLAN-KS-RUNDA4 rozdz. 9, T0): POD-CZASY DOBY KS. Doba KS (StartCalibration.Daily, "czas doby KS" w linii doby) mierzona po kawalku:
    /// ksiega towarow i pomiary (LedgerEma, GateCollect, MoneyEma), odbior miast i kasa 28 dob, sterowanie rynku, indeksy cen, odbior Planu (w nim: na targ, lepszy stol,
    /// linie Planu, pliki), bramki (klucze, bramka krainy), doba stanu zastosowanego (stany ZC, okregi przeliczone i bez przeliczenia, rampa, pola mieszczan, regula A7,
    /// mapa koszyka ZC4, polki i pliki, linie logu), start Planu (zrzut). Linia "KS: koszt doby (T0) dzien N" zaraz po linii doby (tylko log - zadnej zmiany gry;
    /// stoper ok. 25 ns na odczyt, ok. 40 odczytow na dobe). Grupa nks w tools/sprawdz_logi.py (NKS-9t, INFO: mediany pod-czasow dob zastosowanych).
    /// </summary>
    internal static partial class StartCalibration
    {
        // T0: pola pod-czasow (tiki Stopwatch, zerowane na poczatku doby KS)
        private const int T0Ledger = 0, T0Take = 1, T0Ctl = 2, T0Prices = 3, T0Collect = 4, T0Keys = 5, T0Snap = 6, T0Redo = 7, T0Reuse = 8, T0Ramp = 9, T0Fields = 10,
                          T0Rule = 11, T0Map = 12, T0Shelf = 13, T0Lines = 14, T0Start = 15, T0CMk = 16, T0CNeed = 17, T0CLines = 18, T0CCsv = 19, T0Live = 20, T0N = 21;
        private static readonly long[] _t0 = new long[T0N];
        private static int _t0Redo;                         // okregi przeliczone dzis (LiveDaily) - do linii T0
        private static bool _t0Worker;                      // T1: "na targ" odbioru Planu z watku roboczego (TAK) albo liczone na watku glownym (NIE)

        private static long T0Now() { return Stopwatch.GetTimestamp(); }

        /// <summary>T0: poczatek doby KS - pod-czasy od zera; watek glowny (pamieci T4 tylko na nim).</summary>
        private static void T0Begin() { Array.Clear(_t0, 0, T0N); _t0Redo = 0; _t0Worker = false; _kMain = Environment.CurrentManagedThreadId; }
        private static int _kMain = -1;                     // watek glowny (doba KS) - pamieci ks-koszt tylko na nim; inny watek - rachunek bez pamieci (jak dotad)

        /// <summary>T0: czas od q do teraz do pola slot; zwraca teraz (nastepny odcinek).</summary>
        private static long T0Lap(int slot, long q) { long n = Stopwatch.GetTimestamp(); _t0[slot] += n - q; return n; }

        private static double T0Ms(long ticks) { return ticks * 1000.0 / Stopwatch.Frequency; }

        /// <summary>T0: start Planu (zrzut na watku glownym) z pod-czasem.</summary>
        private static void T0PlanStart(int day) { long q = T0Now(); try { PlanStart(day); } finally { _t0[T0Start] += T0Now() - q; } }

        /// <summary>T0: doba stanu zastosowanego z pod-czasem calosci (wnetrze - pola LiveDaily).</summary>
        private static void T0LiveDaily(int today) { long q = T0Now(); try { LiveDaily(today); } finally { _t0[T0Live] += T0Now() - q; } }

        /// <summary>T0: linia "KS: koszt doby (T0) dzien N" - pod-czasy w ms (format: nazwa liczba, przecinki; reszta = razem - suma pol glownych).</summary>
        private static string T0Line(int day, double totalMs)
        {
            var sb = new StringBuilder(700);
            sb.Append(PK).Append("koszt doby (T0) dzien ").Append(day).Append(" | razem ").Append(totalMs.ToString("0.00", Inv)).Append(" ms (stan ").Append(_state == StateApplied ? "ZASTOSOWANY" : "Plan").Append(')');
            double live = T0Ms(_t0[T0Live]);
            double inner = 0; for (int s = T0Snap; s <= T0Lines; s++) inner += T0Ms(_t0[s]);
            double main = 0; foreach (int s in new[] { T0Ledger, T0Take, T0Ctl, T0Prices, T0Collect, T0Keys, T0Start }) main += T0Ms(_t0[s]);
            sb.Append(" | pod-czasy ms: ksiega ").Append(T0F(T0Ledger)).Append(", odbior ").Append(T0F(T0Take)).Append(", sterowanie ").Append(T0F(T0Ctl)).Append(", ceny ").Append(T0F(T0Prices))
              .Append(", plan_odbior ").Append(T0F(T0Collect)).Append(", bramki ").Append(T0F(T0Keys)).Append(", plan_start ").Append(T0F(T0Start))
              .Append(", zastosowany ").Append(live.ToString("0.00", Inv))
              .Append(" | w dobie zastosowanej ms: stany ").Append(T0F(T0Snap)).Append(", okregi_przeliczone ").Append(T0F(T0Redo)).Append(", okregi_bez_przeliczenia ").Append(T0F(T0Reuse))
              .Append(", rampa ").Append(T0F(T0Ramp)).Append(", pola_mieszczan ").Append(T0F(T0Fields)).Append(", regula_a7 ").Append(T0F(T0Rule)).Append(", mapa_koszyka ").Append(T0F(T0Map))
              .Append(", polki_pliki ").Append(T0F(T0Shelf)).Append(", linie_logu ").Append(T0F(T0Lines)).Append(", inne_zastosowany ").Append(Math.Max(0.0, live - inner).ToString("0.00", Inv))
              .Append(" | odbior Planu ms: na_targ ").Append(T0F(T0CMk)).Append(" (watek roboczy ").Append(_t0Worker ? "TAK" : "NIE").Append("), lepszy_stol ").Append(T0F(T0CNeed))
              .Append(", linie_planu ").Append(Math.Max(0.0, T0Ms(_t0[T0CLines] - _t0[T0CMk])).ToString("0.00", Inv)).Append(", pliki_planu ").Append(T0F(T0CCsv))
              .Append(" | reszta ").Append(Math.Max(0.0, totalMs - main - live).ToString("0.00", Inv)).Append(" ms | okregow przeliczonych ").Append(_t0Redo).Append('.');
            return sb.ToString();
        }

        private static string T0F(int slot) { return T0Ms(_t0[slot]).ToString("0.00", Inv); }

        // ============================================================ klucz paczki
        /// <summary>ks-koszt: klucz Ks Cost Fast (domyslnie TAK) - szybkie drogi T1-T12 (te same wyniki co do bitu); wylaczony = stare drogi co do linii kodu.</summary>
        internal static bool SwCostFast { get { var s = Settings.Current; return s == null || s.KsCostFast; } }

        // ============================================================ T1: "na targ" odbioru Planu na watku roboczym
        /// <summary>T1 (watek roboczy, po KsCore.Solve): sumy linii "KS: na targ (plan doba N)" - BuildLive + ComputeDistrict(bez czynnikow) kazdego okregu, w tej samej
        /// kolejnosci co PlanLines na watku glownym (te same liczby co do bitu: te same wejscia - wynik Planu, jego wejscie, tabele swiata; PlanCsvText liczy to samo
        /// na tym watku od rundy 3). Tabele towarow niegotowe (nowa sesja) - nic (odbior liczy jak dotad na watku glownym). Nie rzuca na zewnatrz (wolajacy lapie).</summary>
        private static void PlanMarketSums(KsResult r, KsInput inp)
        {
            if (r == null) return;
            r.NtMk = null;
            if (inp == null || r.Error != null || _itemIx == null || _items == null || _itemKg == null) return;
            var mk = new double[ItemCount]; var tf = new double[ItemCount]; var own = new double[ItemCount]; double kcal = 0, need = 0; int nd = 0;
            var live = BuildLive(r, inp, false);
            foreach (var ld in live)
            {
                var res = ComputeDistrict(ld, false, null);
                if (res == null) continue;
                nd++;
                for (int i = 0; i < ItemCount; i++) { if (ld.TF) tf[i] += res.Market[i]; else { mk[i] += res.Market[i]; own[i] += res.Own[i]; } }
                kcal += res.OwnKcal; need += res.KcalNeed;
            }
            r.NtTf = tf; r.NtOwn = own; r.NtKcal = kcal; r.NtNeed = need; r.NtNd = nd;
            r.NtMk = mk;                                          // ostatnie - znacznik gotowych sum
        }

        // ============================================================ T4: wersjonowana pamiec progow drabiny 2 i budzetu koszyka wsi
        // ZPass2Price / ZPass2Floor (miasto x pozycja), StartIndex (rynek x pozycja) i LandGradZ (miasto targowe x pozycja) licza sie z tych samych wejsc przez cala dobe:
        // Plan (_plan: ceny cienia P, gradient), _grad, ciecie nadmiaru krain (_landCut - LandGlutWeekly przy starcie Planu), mapa koszyka ZC4 (_zt: popyt zaplacony ZDp -
        // Zc4Build raz na dobe), wartosc towaru. Pamiec pamieta te wejscia (referencje + licznik _t4Ver przy zmianie _landCut i mapy w miejscu) i dobe; inna wartosc
        // ktoregokolwiek - od zera. Te same wywolania funkcji co dotad, tylko raz na dobe (te same liczby co do bitu). Tylko watek glowny (_kMain).
        private static int _t4Ver, _t4SeenVer = -1, _t4Day = int.MinValue;
        private static object _t4Map, _t4Plan, _t4Grad;
        private static readonly Dictionary<Town, double[]> _t4P2 = new Dictionary<Town, double[]>();           // [z] ZPass2Price, [ZN + z] ZPass2Floor (NaN - nie liczone)
        private static readonly double[][] _t4Si = { new double[KsBasket.N], new double[KsBasket.N] };        // StartIndex [rynek][z]
        private static readonly Dictionary<Settlement, double[]> _t4Lg = new Dictionary<Settlement, double[]>(); // LandGradZ [miasto targowe][z]
        private static double[] _t4LgNull;                                                                      // LandGradZ(null, z) (= 1)
        private static bool _t4On;                                                                              // pamiec czynna w tej chwili (klucz + watek glowny)

        /// <summary>T4: wejscia pamieci zmienione (Zc4Build, Zc4Off, Zc4Reset, LandGlutWeekly, R3Reset, odczyt zapisu) - pamiec od zera przy nastepnym uzyciu.</summary>
        internal static void T4Bump() { unchecked { _t4Ver++; } _t4On = false; }

        /// <summary>T4: poczatek uzycia (zakupy jednej wsi, budzet koszyka wsi) - klucz, watek glowny i wersja wejsc; false - rachunek bez pamieci (jak dotad).</summary>
        internal static bool T4Begin()
        {
            _t4On = false;
            if (!SwCostFast || _kMain < 0 || Environment.CurrentManagedThreadId != _kMain) return false;
            int today = Today();
            var map = _zt; var plan = _plan; var grad = _grad;
            if (_t4SeenVer != _t4Ver || _t4Day != today || !ReferenceEquals(_t4Map, map) || !ReferenceEquals(_t4Plan, plan) || !ReferenceEquals(_t4Grad, grad))
            {
                _t4SeenVer = _t4Ver; _t4Day = today; _t4Map = map; _t4Plan = plan; _t4Grad = grad;
                _t4P2.Clear(); _t4Lg.Clear(); _t4LgNull = null;
                for (int m = 0; m < 2; m++) for (int z = 0; z < ZN; z++) _t4Si[m][z] = double.NaN;
            }
            _t4On = true;
            return true;
        }

        private static double[] T4Row(int n) { var a = new double[n]; for (int i = 0; i < n; i++) a[i] = double.NaN; return a; }

        /// <summary>T4: KsCore.StartIndex(ZIds[z], P, m) z pamieci (bez pamieci - wprost).</summary>
        private static double T4StartIndex(int m, int z)
        {
            if (!_t4On || z < 0 || z >= ZN) return KsCore.StartIndex(ZIds[z], _plan != null ? _plan.P : null, m);
            var a = _t4Si[m == 0 ? 0 : 1];
            double v = a[z];
            if (double.IsNaN(v)) { v = KsCore.StartIndex(ZIds[z], _plan != null ? _plan.P : null, m); a[z] = v; }
            return v;
        }

        /// <summary>T4: LandGradZ(mk, z) z pamieci (bez pamieci - wprost).</summary>
        private static double T4LandGrad(Settlement mk, int z)
        {
            if (!_t4On || z < 0 || z >= ZN) return LandGradZ(mk, z);
            double[] a;
            if (mk == null) { a = _t4LgNull; if (a == null) { a = T4Row(ZN); _t4LgNull = a; } }
            else if (!_t4Lg.TryGetValue(mk, out a)) { a = T4Row(ZN); _t4Lg[mk] = a; }
            double v = a[z];
            if (double.IsNaN(v)) { v = LandGradZ(mk, z); a[z] = v; }
            return v;
        }

        /// <summary>T4: wiersz pamieci progow drabiny 2 miasta t (po T4Begin, wazny do konca zakupow jednej wsi); null - bez pamieci.</summary>
        internal static double[] T4P2Row(Town t)
        {
            if (!_t4On || t == null) return null;
            double[] a; if (!_t4P2.TryGetValue(t, out a)) { a = T4Row(2 * ZN); _t4P2[t] = a; }
            return a;
        }

        /// <summary>T4: ZPass2Price(t, z) z wiersza pamieci miasta (null - wprost).</summary>
        internal static double ZPass2PriceT4(Town t, int z, double[] row)
        {
            if (row == null || z < 0 || z >= ZN) return ZPass2Price(t, z);
            double v = row[z];
            if (double.IsNaN(v)) { v = ZPass2Price(t, z); row[z] = v; }
            return v;
        }

        /// <summary>T4: ZPass2Floor(t, z) z wiersza pamieci miasta (null - wprost).</summary>
        internal static double ZPass2FloorT4(Town t, int z, double[] row)
        {
            if (row == null || z < 0 || z >= ZN) return ZPass2Floor(t, z);
            double v = row[ZN + z];
            if (double.IsNaN(v)) { v = ZPass2Floor(t, z); row[ZN + z] = v; }
            return v;
        }

        /// <summary>T5: mapa koszyka miasta t dla zakupow jednej wsi (null - poza ZC4: ZRow -2, ZPrice 0) - to samo, co ZTownOf w ZRow, ZRowSet i ZPrice.</summary>
        internal static ZTown ZTownFor(Town t)
        {
            ZTown zt;
            return ZTownOf(t, out zt) ? zt : null;
        }
    }

    internal sealed partial class KsResult
    {
        // ks-koszt T1: sumy "na targ" odbioru Planu policzone na watku roboczym (null - odbior liczy je na watku glownym, jak dotad)
        public double[] NtMk, NtTf, NtOwn; public double NtKcal, NtNeed; public int NtNd;
    }
}
