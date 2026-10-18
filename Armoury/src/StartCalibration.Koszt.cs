using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

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
        private static void T0Begin() { Array.Clear(_t0, 0, T0N); _t0Redo = 0; _t0Worker = false; _t13Skip = 0; _kMain = Environment.CurrentManagedThreadId; }
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
              .Append(" | reszta ").Append(Math.Max(0.0, totalMs - main - live).ToString("0.00", Inv)).Append(" ms | okregow przeliczonych ").Append(_t0Redo)
              .Append(" | klucze: Ks Cost Fast ").Append(SwCostFast ? "TAK" : "NIE").Append(", Ks Town Fields Every2 ").Append(SwTownFieldsEvery2 ? "TAK (pola mieszczan bez dostawy dzis " + _t13Skip + ")" : "NIE").Append('.');
            return sb.ToString();
        }

        private static string T0F(int slot) { return T0Ms(_t0[slot]).ToString("0.00", Inv); }

        // ============================================================ klucz paczki
        /// <summary>ks-koszt: klucz Ks Cost Fast (domyslnie TAK) - szybkie drogi T1-T12 (te same wyniki co do bitu); wylaczony = stare drogi co do linii kodu.</summary>
        internal static bool SwCostFast { get { var s = Settings.Current; return s == null || s.KsCostFast; } }

        // ============================================================ T1: "na targ" odbioru Planu na watku roboczym
        /// <summary>T1 (watek roboczy, po KsCore.Solve): sumy linii "KS: na targ (plan doba N)" - BuildLive + ComputeDistrict(bez czynnikow) kazdego okregu, w tej samej
        /// kolejnosci co PlanLines na watku glownym (te same liczby co do bitu: te same wejscia - wynik Planu, jego wejscie, tabele swiata; PlanCsvText liczy to samo
        /// na tym watku od rundy 3). Tabele towarow niegotowe (nowa sesja) - nic (odbior liczy jak dotad na watku glownym). Nie rzuca na zewnatrz (wolajacy lapie).
        /// Recenzja ks-koszt: tabela kg sztuki zrzucona raz na wejsciu (BuildInput robi EnsureItems na watku glownym przed startem watku) i podana do ComputeDistrict -
        /// watek roboczy nigdy nie laduje tabel towarow z gry (LiveReset przy wczytaniu kampanii w trakcie Planu zeruje je na watku glownym); Plan anulowany (nowa sesja,
        /// PlanReset - gen) - koniec bez sum (wynik i tak odrzucony).</summary>
        private static void PlanMarketSums(KsResult r, KsInput inp, Func<bool> cancelled)
        {
            if (r == null) return;
            r.NtMk = null;
            var kg = _itemKg;
            if (inp == null || r.Error != null || _itemIx == null || _items == null || kg == null) return;
            var mk = new double[ItemCount]; var tf = new double[ItemCount]; var own = new double[ItemCount]; double kcal = 0, need = 0; int nd = 0;
            var live = BuildLive(r, inp, false);
            foreach (var ld in live)
            {
                if (cancelled != null && cancelled()) return;
                var res = ComputeDistrict(ld, false, null, kg);
                if (res == null) continue;
                nd++;
                for (int i = 0; i < ItemCount; i++) { if (ld.TF) tf[i] += res.Market[i]; else { mk[i] += res.Market[i]; own[i] += res.Own[i]; } }
                kcal += res.OwnKcal; need += res.KcalNeed;
            }
            if (cancelled != null && cancelled()) return;
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
            if (_kMain < 0 || Environment.CurrentManagedThreadId != _kMain) return false;   // inny watek: bez pamieci i bez ruszania stanu pamieci watku glownego
            _t4On = false;
            if (!SwCostFast) return false;
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

        /// <summary>T4: pamiec czynna i ten watek to watek glowny (inny watek - nigdy pamiec, takze gdy glowny wlasnie ja wlaczyl).</summary>
        private static bool T4Here() { return _t4On && Environment.CurrentManagedThreadId == _kMain; }

        private static double[] T4Row(int n) { var a = new double[n]; for (int i = 0; i < n; i++) a[i] = double.NaN; return a; }

        /// <summary>T4: KsCore.StartIndex(ZIds[z], P, m) z pamieci (bez pamieci - wprost).</summary>
        private static double T4StartIndex(int m, int z)
        {
            if (!T4Here() || z < 0 || z >= ZN) return KsCore.StartIndex(ZIds[z], _plan != null ? _plan.P : null, m);
            var a = _t4Si[m == 0 ? 0 : 1];
            double v = a[z];
            if (double.IsNaN(v)) { v = KsCore.StartIndex(ZIds[z], _plan != null ? _plan.P : null, m); a[z] = v; }
            return v;
        }

        /// <summary>T4: LandGradZ(mk, z) z pamieci (bez pamieci - wprost).</summary>
        private static double T4LandGrad(Settlement mk, int z)
        {
            if (!T4Here() || z < 0 || z >= ZN) return LandGradZ(mk, z);
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
            if (!T4Here() || t == null) return null;
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

        // ============================================================ T13: pola mieszczan co 2. doba (klucz Ks Town Fields Every2, domyslnie NIE - nie bit w bit)
        private static bool SwTownFieldsEvery2 { get { var s = Settings.Current; return s != null && s.KsTownFieldsEvery2; } }

        /// <summary>T13: doba bez dostawy pol mieszczan (parzystosc Fnv okregu i doby) - plon doby x beta do ulamka (Frac) jak w DeliverTownFields, bez sztuk na polce
        /// i bez zaplaty; nastepnej doby dostawa dwoch dob naraz (kazda sztuka placona jak dotad). true - doba bez dostawy (wolajacy pomija DeliverTownFields).
        /// Te same warunki co DeliverTownFields (miasto, polka, oblezenie - wtedy nic nie przybywa).</summary>
        private static bool T13Accrue(LiveDist ld, DistResult res, int today)
        {
            if (_items == null || res == null || ((Fnv(ld.Id) + (uint)today) & 1u) == 0u) return false;     // doba dostawy (albo bez tabel - dostawa jak dotad)
            var town = ld.T;
            if (town == null || town.Settlement == null || town.Settlement.ItemRoster == null || ld.St.IsUnderSiege) return true;
            int m = ld.Market == 0 ? 0 : 1;
            for (int i = 0; i < ItemCount; i++)
            {
                if (_items[i] == null) continue;
                ld.Frac[i] += res.Market[i] * Beta(m, i);
            }
            _t13Skip++;
            return true;
        }
        private static int _t13Skip;

        // ============================================================ T6: pozycja koszyka towaru KS (RampZ) z tablicy
        private static int[] _rampZTab;
        /// <summary>T6: RampZ(i) dla kazdego towaru KS (Array.IndexOf(ZIds, ItemIds[i]) - stale tablice nazw) policzone raz.</summary>
        private static int[] RampZTab()
        {
            var a = _rampZTab;
            if (a != null) return a;
            a = new int[ItemCount];
            for (int i = 0; i < ItemCount; i++) a[i] = RampZ(i);
            _rampZTab = a;
            return a;
        }

        // ============================================================ T7: spis polek mapy koszyka (Zc4Build)
        private static bool[] _t7Types; private static ItemObject[] _t7For;

        /// <summary>T7: typy przedmiotow (ItemTypeEnum) pozycji koszyka - przedmiot innego typu nie jest kluczem _zIx (bron, zbroja, konie wierzchowe ...).</summary>
        private static bool[] T7Types()
        {
            var items = _zItems;
            if (items == null) return null;
            if (_t7Types != null && ReferenceEquals(_t7For, items)) return _t7Types;
            int max = 0;
            foreach (var it in items) if (it != null && (int)it.ItemType > max) max = (int)it.ItemType;
            var t = new bool[max + 1];
            foreach (var it in items) if (it != null && (int)it.ItemType >= 0) t[(int)it.ItemType] = true;
            _t7Types = t; _t7For = items;
            return t;
        }

        /// <summary>T7: spis polki miasta do mapy koszyka - ten sam wynik co petla w Zc4Build (pierwszy wiersz bez modyfikatora kazdej pozycji, ilosc), kopia wiersza tylko
        /// dla przedmiotow typu pozycji koszyka (GetItemAtIndex bez kopii).</summary>
        private static void T7Scan(TaleWorlds.CampaignSystem.Roster.ItemRoster ro, int[] ixOut, int[] cnt, bool[] types)
        {
            int n = ro.Count;
            for (int k = 0; k < n; k++)
            {
                var ei = ro.GetItemAtIndex(k);
                if (ei == null) continue;
                int ty = (int)ei.ItemType;
                if (ty < 0 || ty >= types.Length || !types[ty]) continue;          // nie jest kluczem _zIx (typ spoza pozycji koszyka)
                var el = ro.GetElementCopyAtIndex(k); int zi;
                if (el.EquipmentElement.ItemModifier != null || !_zIx.TryGetValue(ei, out zi) || ixOut[zi] >= 0) continue;
                ixOut[zi] = k; cnt[zi] = el.Amount;
            }
        }

        /// <summary>Kontrola T7: petla spisu polki z Zc4Build bez zmian (do porownania).</summary>
        private static void T7ScanOld(TaleWorlds.CampaignSystem.Roster.ItemRoster ro, int[] ixOut, int[] cnt)
        {
            for (int k = 0; k < ro.Count; k++)
            {
                var el = ro.GetElementCopyAtIndex(k); var ei = el.EquipmentElement.Item; int zi;
                if (ei == null || el.EquipmentElement.ItemModifier != null || !_zIx.TryGetValue(ei, out zi) || ixOut[zi] >= 0) continue;
                ixOut[zi] = k; cnt[zi] = el.Amount;
            }
        }

        // ============================================================ T8/T10: ilosci pozycji koszyka na polce jednym przegladem
        /// <summary>T8/T10: ilosc kazdej pozycji koszyka na polce ro z semantyka ItemRoster.GetItemNumber (pierwszy wiersz z tym przedmiotem, kazdy modyfikator; brak - 0),
        /// jednym przegladem polki zamiast jednego szukania liniowego na pozycje. cnt[z] wedlug pierwszej pozycji przedmiotu w _zIx (CountZ). null - mapa pozycji niegotowa.</summary>
        internal static int[] ShelfFirst(TaleWorlds.CampaignSystem.Roster.ItemRoster ro)
        {
            var types = T7Types();
            if (ro == null || types == null || _zIx == null) return null;
            var cnt = new int[ZN]; var seen = new bool[ZN];
            int n = ro.Count;
            for (int k = 0; k < n; k++)
            {
                var ei = ro.GetItemAtIndex(k);
                if (ei == null) continue;
                int ty = (int)ei.ItemType;
                if (ty < 0 || ty >= types.Length || !types[ty]) continue;
                int zi;
                if (!_zIx.TryGetValue(ei, out zi) || seen[zi]) continue;
                seen[zi] = true; cnt[zi] = ro.GetElementNumber(k);
            }
            return cnt;
        }

        /// <summary>T8/T10: ro.GetItemNumber(ZItem(z)) z przegladu ShelfFirst (przedmiot spoza _zIx - szukanie jak dotad).</summary>
        internal static int CountZ(TaleWorlds.CampaignSystem.Roster.ItemRoster ro, int z, int[] cnt)
        {
            var it = _zItems[z];
            if (it == null) return 0;
            int zi;
            if (cnt != null && _zIx.TryGetValue(it, out zi)) return cnt[zi];
            return ro.GetItemNumber(it);
        }

        // ============================================================ T8: przetwory ZC5 - pamiec jednej przerobki miasta (TownCraftsDay)
        /// <summary>T8 (klucz Ks Cost Fast): (1) sprzedane wczoraj wedlug kategorii jednym przejsciem po Town.SoldItems (ta sama suma co SoldYesterday); (2) ilosc na polce
        /// z semantyka GetItemNumber (pierwszy wiersz z przedmiotem) z zapamietanym indeksem wiersza - wazny, dopoki polka nie zmienia liczby wierszy (wszystkie zmiany polki
        /// w przerobce ida przez Add - przy zmianie liczby wierszy pamiec od zera; AddToCounts przesuwa wiersze tylko przy usunieciu albo dopisaniu wiersza).</summary>
        private sealed class KcMemo
        {
            private readonly TaleWorlds.CampaignSystem.Roster.ItemRoster _ro; private readonly Town _t;
            private Dictionary<ItemCategory, int> _sold; private bool _soldBad;
            private readonly Dictionary<ItemObject, int> _ix = new Dictionary<ItemObject, int>();
            private int _cnt = -1;
            internal KcMemo(TaleWorlds.CampaignSystem.Roster.ItemRoster ro, Town t) { _ro = ro; _t = t; }

            internal int Sold(ItemCategory c)
            {
                if (_sold == null && !_soldBad)
                {
                    try
                    {
                        var d = new Dictionary<ItemCategory, int>();
                        var l = _t.SoldItems;
                        if (l != null) foreach (var x in l) { if (x.Category == null || x.Number <= 0) continue; int n0; d.TryGetValue(x.Category, out n0); d[x.Category] = n0 + x.Number; }
                        _sold = d;
                    }
                    catch { _soldBad = true; }
                }
                if (_soldBad) return SoldYesterday(_t, c);
                if (c == null) return 0;
                int n; return _sold.TryGetValue(c, out n) ? n : 0;
            }

            internal int Num(ItemObject it)
            {
                if (it == null) return _ro.GetItemNumber(it);
                if (_ro.Count != _cnt) { _ix.Clear(); _cnt = _ro.Count; }
                int i;
                if (!_ix.TryGetValue(it, out i)) { i = _ro.FindIndexOfItem(it); _ix[it] = i; }
                int v = i >= 0 ? _ro.GetElementNumber(i) : 0;
                if ((++_kcSampleN & 63) == 0) { if (_ro.GetItemNumber(it) == v) _kcOk++; else _kcBad++; }   // kontrola (probka 1/64): to samo co GetItemNumber
                return v;
            }

            internal void Add(ItemObject it, int n)
            {
                int c0 = _ro.Count;
                _ro.AddToCounts(it, n);
                if (_ro.Count != c0) { _ix.Clear(); _cnt = _ro.Count; }
            }
        }

        /// <summary>T5: mapa koszyka miasta t dla zakupow jednej wsi (null - poza ZC4: ZRow -2, ZPrice 0) - to samo, co ZTownOf w ZRow, ZRowSet i ZPrice.</summary>
        internal static ZTown ZTownFor(Town t)
        {
            ZTown zt;
            return ZTownOf(t, out zt) ? zt : null;
        }
    }

    internal static partial class StartCalibration
    {
        // ============================================================ kontrola szybkich drog (tylko odczyt; co 7 dob, poza czasem doby KS)
        private static int _kcSampleN, _kcOk, _kcBad;              // T8: probka 1/64 ilosci polki przetworow wobec GetItemNumber
        private static string _t1Check = "-";                      // T1: porownanie sum "na targ" (watek roboczy / glowny) - raz na proces
        private static bool _t1Checked;

        private static bool SameD(double a, double b) { return BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b); }

        /// <summary>T1: pierwszy odbior Planu z sumami z watku roboczego (raz na proces) - te same sumy policzone na watku glownym, porownanie co do bitu.</summary>
        private static void T1Check(KsResult r, KsInput inp)
        {
            if (_t1Checked || r == null || r.NtMk == null || inp == null || !LogGate.Lines) return;
            _t1Checked = true;
            try
            {
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
                int ok = 0, bad = 0;
                for (int i = 0; i < ItemCount; i++) { if (SameD(mk[i], r.NtMk[i])) ok++; else bad++; if (SameD(tf[i], r.NtTf[i])) ok++; else bad++; if (SameD(own[i], r.NtOwn[i])) ok++; else bad++; }
                if (SameD(kcal, r.NtKcal)) ok++; else bad++;
                if (SameD(need, r.NtNeed)) ok++; else bad++;
                if (nd == r.NtNd) ok++; else bad++;
                _t1Check = ok + "/" + bad + " (Plan doby " + _wDay + ")";
            }
            catch (Exception e) { _t1Check = "blad " + e.GetType().Name; }
        }

        /// <summary>Kontrola co 7 dob (stan zastosowany, linie, klucz Ks Cost Fast): szybkie drogi wobec starych na tych samych danych - tylko odczyt (pamieci T4 i tak licza
        /// te same funkcje). Linia "KS: koszt - kontrola szybkich drog dzien N" (zgodne/rozne; NKS-9k: rozne = 0).</summary>
        private static void KCheck(int today, int day)
        {
            if (!SwCostFast || _state != StateApplied || !LogGate.Lines || (today % 7) != 3) return;
            try
            {
                int a1 = 0, b1 = 0, a2 = 0, b2 = 0, a3 = 0, b3 = 0, a4 = 0, b4 = 0, a5 = 0, b5 = 0, a6 = 0, b6 = 0, a7 = 0, b7 = 0;
                var types = T7Types(); var map = _zt;
                Zc4EnsureMap();
                T4Begin();
                foreach (var zt in map.Values)
                {
                    var ro = zt != null && zt.St != null ? zt.St.ItemRoster : null;
                    if (ro == null) continue;
                    // T10: ilosci pozycji jednym przegladem
                    var sc = ShelfFirst(ro);
                    for (int z = 0; z < ZN; z++) { var it = _zItems[z]; if (it == null) continue; if (CountZ(ro, z, sc) == ro.GetItemNumber(it)) a1++; else b1++; }
                    // T7: spis polki
                    if (types != null)
                    {
                        var i1 = new int[ZN]; var i2 = new int[ZN]; var c1 = new int[ZN]; var c2 = new int[ZN];
                        for (int z = 0; z < ZN; z++) { i1[z] = -1; i2[z] = -1; }
                        T7Scan(ro, i1, c1, types); T7ScanOld(ro, i2, c2);
                        for (int z = 0; z < ZN; z++) { if (i1[z] == i2[z] && c1[z] == c2[z]) a2++; else b2++; }
                    }
                    // T4: progi drabiny 2
                    if (zt.T != null)
                    {
                        var row = T4P2Row(zt.T);
                        foreach (int z in ZLadder)
                        {
                            if (SameD(ZPass2PriceT4(zt.T, z, row), ZPass2Price(zt.T, z))) a3++; else b3++;
                            if (SameD(ZPass2FloorT4(zt.T, z, row), ZPass2Floor(zt.T, z))) a3++; else b3++;
                        }
                    }
                }
                // T4: budzet koszyka wsi (pamiec doby wobec wprost)
                foreach (var kv in _zvp)
                {
                    var v = kv.Key; var q = kv.Value; if (v == null || q == null) continue;
                    int m = MarketOfSettlement(v.Settlement); if (m < 0) m = 0;
                    Settlement mk = null; try { mk = TradeTownOf(v); } catch { mk = null; }
                    double bf = 0, bs = 0;
                    for (int z = 0; z < ZN && z < q.Length; z++)
                    {
                        var it = _zItems[z]; if (it == null || !(q[z] > 0)) continue;
                        bf += q[z] * Math.Max(1, it.Value) * T4StartIndex(m, z) * T4LandGrad(mk, z);
                        bs += q[z] * Math.Max(1, it.Value) * KsCore.StartIndex(ZIds[z], _plan != null ? _plan.P : null, m) * LandGradZ(mk, z);
                    }
                    if (SameD(bf, bs)) a4++; else b4++;
                }
                // T3: drogi wies -> miasto z pamieci sesji wobec gry
                TownFolk.KsNtCheck(300, out a5, out b5);
                // T2: dochod zajecia z wiersza miasta wobec Income (probka okregow)
                int nd = 0;
                EnsureValues();
                foreach (var ld in _live.Values)
                {
                    if (nd >= 150) break;
                    if (ld == null || ld.TF || ld.V == null || ld.K == null || ld.B == null) continue;
                    var mt = ld.V.TradeBound != null ? ld.V.TradeBound.Town : (ld.V.Bound != null ? ld.V.Bound.Town : null);
                    double[] px; if (mt == null || !_px.TryGetValue(mt, out px)) continue;
                    var vRow = new double[KsTab.NC]; for (int c0 = 0; c0 < vRow.Length; c0++) vRow[c0] = double.NaN;
                    nd++;
                    foreach (var b in ld.B)
                    {
                        if (b == null || b.K == null || b.C == null || b.Cand == null) continue;
                        foreach (int c in b.Cand) { if (SameD(IncomeV(b, ld.K.Type, c, px, vRow), Income(b, ld.K.Type, c, px))) a6++; else b6++; }
                    }
                }
                // T1: sumy "na targ" ostatniego Planu z watku roboczego wobec watku glownego (raz na proces)
                T1Check(_plan, _planIn);
                // T6: pozycja koszyka towaru z tablicy
                var rz = RampZTab();
                for (int i = 0; i < ItemCount; i++) { if (rz[i] == RampZ(i)) a7++; else b7++; }
                int ok = a1 + a2 + a3 + a4 + a5 + a6 + a7 + _kcOk, bad = b1 + b2 + b3 + b4 + b5 + b6 + b7 + _kcBad;
                Log.Info(PK + "koszt - kontrola szybkich drog dzien " + day + " | zgodne " + ok + ", rozne " + bad + " (zgodne/rozne: ilosci polki T10 " + a1 + "/" + b1 + ", spis polek T7 " + a2 + "/" + b2
                         + ", progi drabiny 2 T4 " + a3 + "/" + b3 + ", budzet koszyka wsi T4 " + a4 + "/" + b4 + ", drogi wies-miasto T3 " + a5 + "/" + b5 + ", regula A7 T2 " + a6 + "/" + b6
                         + ", pozycje rampy T6 " + a7 + "/" + b7 + ", przetwory T8 (probka 1/64 od ostatniej kontroli) " + _kcOk + "/" + _kcBad + ") | na targ Planu T1 (raz na proces) " + _t1Check
                         + " | tylko odczyt - szybkie drogi wobec starych na tych samych danych.");
                _kcOk = _kcBad = 0;
            }
            catch (Exception e) { Stumble("KCheck", e); }
        }
    }

    /// <summary>
    /// ks-koszt (T0 dla KS-H): pod-czasy kupcow zywnosci KS-H (KsFoodCarts) - przyjazdy, wstep doby (spis polek, popyt, kasy), decyzje KsFoodCore (w nich - probka 1/8
    /// wywolan: cena lotu, wartosc u celu, indeks ceny, wysylka), fracht wsi. Linia "Kupcy zywnosci (KS-H) koszt dzien N" po linii doby KS-H (tylko log; tylko przy liniach).
    /// </summary>
    internal static class KsCostKsh
    {
        internal static long Arr, Setup, Decide, Pay;              // tiki Stopwatch od ostatniej linii
        internal static long LotT, DvT, IxT, ShipT;               // tiki probek (x 8)
        internal static int LotN, DvN, IxN, ShipN, ArrCarts;      // wywolania
        internal static bool On { get { return LogGate.Lines; } }

        /// <summary>Czas wywolania z probka co 8. (tiki x 8); n - licznik wywolan.</summary>
        internal static bool SampleNow(ref int n) { return (++n & 7) == 0; }

        internal static string Line(int day, double dayMs)
        {
            double f = 1000.0 / Stopwatch.Frequency;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            double dec = Decide * f, inner = (LotT + DvT + IxT + ShipT) * f;
            var sb = new StringBuilder(500);
            sb.Append("Kupcy zywnosci (KS-H) koszt dzien ").Append(day).Append(" | czas doby ").Append(dayMs.ToString("0.00", inv)).Append(" ms (NKS-28)")
              .Append(" | pod-czasy ms: wstep ").Append((Setup * f).ToString("0.00", inv)).Append(", decyzje ").Append(dec.ToString("0.00", inv))
              .Append(" (probka 1/8: ceny_lotow ").Append((LotT * f).ToString("0.00", inv)).Append(" w ").Append(LotN).Append(", wartosc_u_celu ").Append((DvT * f).ToString("0.00", inv)).Append(" w ").Append(DvN)
              .Append(", indeks_ceny ").Append((IxT * f).ToString("0.00", inv)).Append(" w ").Append(IxN).Append(", wysylka ").Append((ShipT * f).ToString("0.00", inv)).Append(" w ").Append(ShipN)
              .Append(", reszta_decyzji ").Append(Math.Max(0.0, dec - inner).ToString("0.00", inv)).Append("), fracht_wsi ").Append((Pay * f).ToString("0.00", inv))
              .Append(" | poza czasem doby: przyjazdy ").Append((Arr * f).ToString("0.00", inv)).Append(" ms (wozow ").Append(ArrCarts).Append(").");
            Clear();
            return sb.ToString();
        }

        internal static void Clear() { Arr = Setup = Decide = Pay = LotT = DvT = IxT = ShipT = 0; LotN = DvN = IxN = ShipN = ArrCarts = 0; }
    }

    internal sealed partial class KsResult
    {
        // ks-koszt T1: sumy "na targ" odbioru Planu policzone na watku roboczym (null - odbior liczy je na watku glownym, jak dotad)
        public double[] NtMk, NtTf, NtOwn; public double NtKcal, NtNeed; public int NtNd;
    }
}
