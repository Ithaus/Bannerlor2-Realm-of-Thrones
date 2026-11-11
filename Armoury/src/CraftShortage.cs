using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 435-L6 - LANCUCH PODAZY (LP), krok 6: NIEDOBOR STERUJE REKAMI. Projekt: PROJEKT-LANCUCH-po-krytyce (11.11), rozdz. II.7 (uzycie N), II.19 wiersz L6.
    /// Polecenie Jeffa: "warsztaty i produkcja maja sie zmieniac tak, jak jest zapotrzebowanie na swiecie; ludzie przechodza z pracy w X (zmalalo zapotrzebowanie)
    /// na Y (bo brakuje w regionie / swiecie Y)".
    ///
    /// Dotad zmiana zajec patrzyla tylko na CENE (ZW: przydatnosc x sqrt(indeks cen) albo dochod przy sygnale KS; A7: dochod rodziny), a cena bywa sprzeczna z
    /// niedoborem: towar, ktorego nie ma na polce (drewno, glina, len, sol), nie ma tez sprzedazy, wiec jego indeks nie rosnie tak, jak rosnie kolejka chetnych.
    /// Tablica N (SupplySignal, L1: towar x miasto targowe; N = max(1, S_stos, S_kolejka) &lt;= 2, bez ceny) mierzy wlasnie te kolejke i pusty stos.
    ///
    ///  1. Klucz Crafts Follow Shortage (NIE = co do bitu jak dotad): w dobie kroku reguly zmiany zajec PRZYCHOD zajecia mnozy sie przez czynnik
    ///     f = sqrt(N towarow zajecia w miescie targowym wsi), wagi towarow = stawka x wartosc (te same co indeks cen zajecia ZW). f w zakresie 1 .. sqrt(2).
    ///     - A7 pod KS (StartCalibration.Rule): dochod = L x wartosc x f - K - renta (koszt i renta bez czynnika) - we wszystkich trzech rachunkach dochodu
    ///       (sygnal KS, ceny gry z wierszem miasta, ceny gry); przy czynniku nie bierze dochodow z diagnozy f5 (sa bez czynnika);
    ///     - ZW bez KS (VillageCrafts.Rule): przy sygnale KS ten sam wzor dochodu, bez sygnalu wynik przydatnosc x sqrt(indeks) x f.
    ///     Caly hamulec Jeffa bez zmian: prog x1.25 przez 60 dob, jedna czesc okregu naraz, 5% rak regionu na sezon, ziemia, wyzywienie, bezpiecznik zboza (L2),
    ///     zmiana przez sprzet (L4). Zboze: N = 2 w miescie, ktorego wsie gloduja - glod ciagnie rece DO zboza.
    ///  2. Klucz Village Order Shortage Boost (1 = jak dotad): wytworca zamowien 363 (drewno, glina, wapien, marmur), ktorego material ma N &gt;= 1.5 w jego miescie
    ///     targowym, dostaje pulap rak pola x mnoznik (najwyzej 0.25 rak pola - twardy sufit 363 zostaje); narastanie +1% rak pola na przeliczenie bez zmian.
    ///
    /// Oba klucze czytaja tablice z POPRZEDNIEJ doby pomiaru i wymagaja klucza Supply Chain Log (bez pomiaru N = 1 - stan jak dotad). Pamiec N jest sesyjna: po
    /// wczytaniu srednie licza sie od pierwszej doby sesji (zmiana zajecia i tak wymaga 60 dob nacisku bez przerwy).
    /// Ten krok nie dodaje zadnego przelewu zlota, ludzi ani towaru i niczego nie zapisuje (stan = liczniki linii). Watek glowny; wyjatek = potkniecie liczone
    /// w linii, bez globalnego wylacznika (CLAUDE.md 7).
    /// </summary>
    internal static class CraftShortage
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const int SysZw = 0, SysKs = 1;
        private static readonly string[] SysName = { "ZW (wioski bez KS)", "A7 (pod KS)" };
        private const double Sqrt2 = 1.4142135623730951;

        internal static bool KeyOn { get { var s = Settings.Current; return s != null && s.CraftsFollowShortage; } }
        /// <summary>Czynnik niedoboru w regule zmiany zajec czynny: klucz i pomiar N (Supply Chain Log).</summary>
        internal static bool On { get { return KeyOn && SupplySignal.On; } }

        // czynniki zajec na miasto - pamiec na jedna opublikowana tablice N (nowa tablica = nowa pamiec)
        private static SupplySignal.NTable _for;
        private static readonly Dictionary<Town, double[]> _rows = new Dictionary<Town, double[]>();
        private static readonly double[] None = new double[0];

        // liczniki ostatniego kroku reguly (na droge): oceny z czynnikiem, wynik oceny, zaczete
        private const int CEval = 0, CBoth = 1, CSig = 2, CLost = 3, CNone = 4, CFach = 5, CStart = 6, CStartSig = 7, CN = 8;
        private static readonly int[][] _c = { new int[CN], new int[CN] };
        private static readonly double[][] _h = { new double[CN], new double[CN] };
        private static readonly int[] _stepDay = { int.MinValue, int.MinValue };
        private static readonly HashSet<Town>[] _towns = { new HashSet<Town>(), new HashSet<Town>() };
        private static readonly Dictionary<int, int> _to = new Dictionary<int, int>();   // zaczete dzieki niedoborowi wedlug zajecia docelowego (sesja)
        private static int _sessStartSig, _sessStart;
        private static int _st;
        private static readonly HashSet<string> _err = new HashSet<string>();

        private static void Stumble(string where, Exception e)
        {
            Interlocked.Increment(ref _st);
            try { lock (_err) { if (!_err.Add(where)) return; } Log.Error("CraftShortage." + where, e); } catch { }
        }

        /// <summary>Nowa kampania / przed wczytaniem: pamiec i liczniki od zera.</summary>
        internal static void Reset()
        {
            _for = null; _rows.Clear();
            for (int i = 0; i < 2; i++) { Array.Clear(_c[i], 0, CN); Array.Clear(_h[i], 0, CN); _stepDay[i] = int.MinValue; _towns[i].Clear(); }
            _to.Clear(); _sessStartSig = _sessStart = 0; _st = 0;
            lock (_err) _err.Clear();
        }

        /// <summary>SupplySignal.Daily przy wylaczonym pomiarze: pamiec czynnikow od zera (tablicy N nie ma).</summary>
        internal static void ZeroLine() { _for = null; _rows.Clear(); }

        /// <summary>
        /// Czynnik przychodu na zajecie (indeks KsTab) dla wsi z miastem targowym `mt`: sqrt(N towarow zajecia), 1 .. sqrt(2). null - klucz wylaczony, brak tablicy N
        /// albo zadne zajecie w tym miescie nie ma niedoboru (rachunek reguly co do bitu jak dotad). Watek glowny, nie rzuca.
        /// </summary>
        internal static double[] Row(Town mt)
        {
            try
            {
                if (mt == null || mt.Settlement == null) return null;
                var tab = SupplySignal.Table;
                if (tab == null) return null;
                if (!ReferenceEquals(tab, _for)) { _for = tab; _rows.Clear(); }
                double[] r;
                if (_rows.TryGetValue(mt, out r)) return ReferenceEquals(r, None) ? null : r;
                r = new double[KsTab.NC];
                bool any = false;
                for (int c = 0; c < r.Length; c++)
                {
                    double n = VillageCrafts.CraftShortageN(tab, mt.Settlement, c);
                    double f = n > 1.0 + 1e-9 && !double.IsNaN(n) && !double.IsInfinity(n) ? Math.Min(Sqrt2, Math.Sqrt(n)) : 1.0;
                    r[c] = f;
                    if (f > 1.0) any = true;
                }
                _rows[mt] = any ? r : None;
                return any ? r : null;
            }
            catch (Exception e) { Stumble("Row", e); return null; }
        }

        private static void StepOf(int sys)
        {
            int d = (int)CampaignTime.Now.ToDays;
            if (_stepDay[sys] == d) return;
            _stepDay[sys] = d; Array.Clear(_c[sys], 0, CN); Array.Clear(_h[sys], 0, CN); _towns[sys].Clear();
        }

        /// <summary>Regula zmiany zajec: czesc oceniona z czynnikiem niedoboru (sam licznik; pierwsza ocena doby zeruje liczniki kroku tej drogi).</summary>
        internal static void NoteEval(int sys)
        {
            try { StepOf(sys); _c[sys][CEval]++; } catch (Exception e) { Stumble("NoteEval", e); }
        }

        /// <summary>Wynik oceny: `adj` - nacisk z czynnikiem, `raw` - nacisk bylby takze bez czynnika; `hands` - rece czesci przy zajeciu biezacym (sam licznik).</summary>
        internal static void Note(int sys, bool adj, bool raw, double hands)
        {
            try
            {
                int k = adj ? (raw ? CBoth : CSig) : (raw ? CLost : CNone);
                _c[sys][k]++; if (hands > 0) _h[sys][k] += hands;
            }
            catch (Exception e) { Stumble("Note", e); }
        }

        /// <summary>A7: ocena z czynnikiem zakonczona regula fachu wsi (blok trzymany przy zajeciu typu albo nacisk powrotu) - poza rachunkiem nacisku niedoboru.</summary>
        internal static void NoteFach(int sys) { try { _c[sys][CFach]++; } catch (Exception e) { Stumble("NoteFach", e); } }

        /// <summary>Zmiana zajecia zaczeta w miescie z czynnikiem niedoboru; `raw` false - bez czynnika nie byloby w tej dobie nacisku (zmiana dzieki niedoborowi).</summary>
        internal static void NoteStart(int sys, int to, bool raw, double hands)
        {
            try
            {
                _c[sys][CStart]++; _h[sys][CStart] += Math.Max(0, hands); _sessStart++;
                if (raw) return;
                _c[sys][CStartSig]++; _h[sys][CStartSig] += Math.Max(0, hands); _sessStartSig++;
                int n; _to.TryGetValue(to, out n); _to[to] = n + 1;
            }
            catch (Exception e) { Stumble("NoteStart", e); }
        }

        /// <summary>
        /// Zamowienia 363 (StartCalibration.OrderKs): mnoznik pulapu rak pola wytworcy - klucz Village Order Shortage Boost, gdy material `it` ma N &gt;= 1.5 w miescie
        /// targowym wsi `v`; 1 - klucz 1, brak pomiaru N albo brak niedoboru. Sam odczyt opublikowanej tablicy; nie rzuca.
        /// </summary>
        internal static double OrderBoost(Village v, ItemObject it)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || v == null || it == null || !(s.VillageOrderShortageBoost > 1f)) return 1.0;
                var tab = SupplySignal.Table;
                if (tab == null) return 1.0;
                var mk = v.TradeBound != null ? v.TradeBound : v.Bound;
                return SupplySignal.TownN(tab, mk, it.ItemCategory) >= 1.5 ? Math.Min(8.0, (double)s.VillageOrderShortageBoost) : 1.0;
            }
            catch (Exception e) { Stumble("OrderBoost", e); return 1.0; }
        }

        /// <summary>Poczatek linii "LP niedobor": czy N czymkolwiek steruje.</summary>
        internal static string UsedText()
        {
            var s = Settings.Current;
            bool a = On, b = s != null && s.VillageOrderShortageBoost > 1f;
            if (!a && !b) return "pomiar; N czyta tylko kolejnosc zlecen sprzetu wsi (L5) - klucze L6 (Crafts Follow Shortage, Village Order Shortage Boost) w stanie 'jak dotad'";
            return "435-L6: N steruje" + (a ? " regula zmiany zajec (Crafts Follow Shortage TAK)" : "") + (a && b ? " i" : "") + (b ? " pulapem zamowien 363 (Village Order Shortage Boost)" : "")
                   + "; reguly czytaja tablice z poprzedniej doby";
        }

        /// <summary>Koniec linii "LP niedobor" (SupplySignal.ShortageText, co dobe): uzycie N - liczniki ostatniego kroku reguly obu drog, zamowienia 363, kontrola ocen.
        /// `tab` - tablica tej doby (czynnik swiata wedlug zajec). Nie rzuca.</summary>
        internal static string LineText(SupplySignal.NTable tab)
        {
            var sb = new StringBuilder(900);
            try
            {
                var s = Settings.Current;
                sb.Append("uzycie N (435-L6): Crafts Follow Shortage ").Append(KeyOn ? "TAK" : "NIE (jak dotad)");
                if (KeyOn)
                {
                    for (int y = 0; y < 2; y++)
                    {
                        var c = _c[y]; var h = _h[y];
                        sb.Append(" | ").Append(SysName[y]).Append(": ");
                        if (_stepDay[y] == int.MinValue) { sb.Append("jeszcze bez oceny z czynnikiem w tej sesji (krok co 7 dob; miasto bez niedoboru liczone jak dotad)"); continue; }
                        sb.Append("ostatni krok z czynnikiem doba ").Append(_stepDay[y]).Append(", ocen z czynnikiem ").Append(c[CEval])
                          .Append(", nacisk z czynnikiem i bez ").Append(c[CBoth]).Append(", tylko dzieki niedoborowi ").Append(c[CSig]).Append(" (rak ").Append(h[CSig].ToString("0", Inv))
                          .Append("), zdjety niedoborem ").Append(c[CLost]).Append(" (rak ").Append(h[CLost].ToString("0", Inv)).Append("), bez nacisku ").Append(c[CNone]);
                        if (y == SysKs) sb.Append(", regula fachu wsi ").Append(c[CFach]);
                        sb.Append("; zaczete zmiany ").Append(c[CStart]).Append(" (rak ").Append(h[CStart].ToString("0", Inv)).Append("), z tego dzieki niedoborowi ").Append(c[CStartSig])
                          .Append(" (rak ").Append(h[CStartSig].ToString("0", Inv)).Append(')')
                          .Append("; kontrola ocen - wynikow = ").Append(c[CEval] - c[CBoth] - c[CSig] - c[CLost] - c[CNone] - c[CFach]);
                    }
                    sb.Append(" | sesja: zmian zaczetych przy czynniku ").Append(_sessStart).Append(", dzieki niedoborowi ").Append(_sessStartSig);
                    if (_to.Count > 0)
                    {
                        var l = new List<KeyValuePair<int, int>>(_to);
                        l.Sort((a, b) => b.Value.CompareTo(a.Value));
                        sb.Append(" (do:");
                        for (int i = 0; i < l.Count && i < 8; i++)
                            sb.Append(i == 0 ? " " : ", ").Append(l[i].Key >= 0 && l[i].Key < KsTab.Crafts.Length ? KsTab.Crafts[l[i].Key] : "?").Append(' ').Append(l[i].Value);
                        sb.Append(')');
                    }
                    if (!SupplySignal.On) sb.Append(" | UWAGA: pomiar N wylaczony - czynnik 1 wszedzie");
                }
                // czynnik swiata (sam S_stos - towar na suficie regulatora wsi) wedlug zajec: to dostaje kazda wies bez wzgledu na miasto
                if (tab != null)
                {
                    int shown = 0;
                    sb.Append(" | czynnik przychodu z samego stosu swiata wedlug zajec (jutro, kazde miasto):");
                    for (int c = 0; c < KsTab.NC && shown < 10; c++)
                    {
                        double n = VillageCrafts.CraftShortageN(tab, null, c);
                        if (!(n > 1.0 + 1e-9)) continue;
                        sb.Append(shown == 0 ? " " : ", ").Append(KsTab.Crafts[c]).Append(" x").Append(Math.Min(Sqrt2, Math.Sqrt(n)).ToString("0.00", Inv));
                        shown++;
                    }
                    if (shown == 0) sb.Append(" -");
                    sb.Append("; miast z komorka N > 1: ").Append(tab.Cell.Count).Append(", miast z glodem wsi (zboze x1.41): ").Append(tab.Hungry.Count);
                }
                double boost = s != null ? s.VillageOrderShortageBoost : 1f;
                sb.Append(" | zamowienia 363: Village Order Shortage Boost x").Append(boost.ToString("0.00", Inv));
                if (!(boost > 1.0)) sb.Append(" (jak dotad)");
                else
                {
                    int n; double over;
                    StartCalibration.OrderBoostStats(out n, out over);
                    sb.Append(", wytworcow z podniesionym pulapem przy ostatnim rachunku ").Append(n).Append(", rece na zamowienie ponad dawny pulap ").Append(over.ToString("0", Inv))
                      .Append(" (twardy sufit 25% rak pola bez zmian)");
                }
                sb.Append(" | L6 nie dodaje zadnego przelewu zlota, ludzi ani towaru | potkniecia L6 (sesja) ").Append(_st);
            }
            catch (Exception e) { Stumble("LineText", e); sb.Append(" [blad linii uzycia N]"); }
            return sb.ToString();
        }
    }
}
