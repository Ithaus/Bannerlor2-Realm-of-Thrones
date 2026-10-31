using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// PACZKA 306a - PASMO CENY MIEDZY RYNKAMI OD KOSZTU PRZEWOZU (klucz PriceBandByCarriage; Jeff 31.10: "karawany ... aby dzialaly i zarabialy ... ekonomia niemal
    /// odbicie prawdziwej ekonomii w sredniowieczu").
    /// Dowod (log Jeffa Armoury-2026-10-30_21-04-45.log): 'Caravan of Bjorn' w dobie 108847 kupila 7 szt. za 10 297 zl (ksiega towarow tej doby: aksamit "karawany kupily 6",
    /// bizuteria "kupily 1" - jedyne zakupy tych towarow w swiecie; rynek w linii 259 94 637 = wartosc x srednia indeksu 97 miast, ok. 13.4k/szt.), a w 108850 sprzedala 5 szt.
    /// za 118 842 zl (ok. 23.8k/szt.) - zysk 110 177 zl w dobe (cala reszta 173 dob karawany: 79 tys.). Aksamit (4 000 d za bele 10 kg): kupno przy indeksie ok. 0.37,
    /// sprzedaz ok. 5.9. Indeks aksamitu w miastach (287): p25-p75 2.41-10.00, mediana 9.9, u wytworcow (4 tkalnie) 0.78; bizuteria 4.05-10.00; przyprawy 4.86-10.00.
    /// To nie blad wyceny kupna/sprzedazy (obie z tego samego GetBasePriceFactor gry) ani kara dowodcy 139b (+-1.5%). Przyczyna - wzor gry
    /// (D / (0.1 S + 0.04 I + 2))^0.6 w granicach 0.1-10 JEDNYCH dla kazdego towaru handlowego: bela aksamitu to ok. 20 dob popytu miasta, wiec jedna bela na polce = indeks
    /// ok. 0.5, pusta polka = sufit 10 - dwudziestokrotny skok od jednej sztuki, a karawany AI kategorie zamyka bramka 284 (STOI), wiec nikt tej roznicy nie wyrownuje.
    /// Prawdziwa ekonomia: roznica ceny tego samego towaru miedzy rynkami nie trwa ponad koszt przewozu i zarobek kupca (prawo jednej ceny) - kupiec wykupi tani towar i
    /// zawiezie tam, gdzie drogi. Przewoz wazy tym mniej, im drozszy kilogram: aksamit, bizuteria, przyprawy, futra miedzy rynkami 2-4 x, zboze, sol, drewno, glina - wiele razy
    /// (to samo rozumowanie, co sufit x10 rudy w RawPrice: ruda wieziona ok. 700 km).
    /// Regula (jedna dla gracza i AI, w cenie gry - kazda droga ceny przez nia przechodzi): towar handlowy NIE-jedzenie ma indeks w pasmie [1/k, k], gdzie
    ///   k = min(10, (1 + marza kupca) x (1 + przewoz wozem na droge / cena kilograma)), przewoz 1.5 d za tone na mile (woz, Anglia XIV w. - jak RawPrice), droga PriceBandHaulKm.
    /// Domyslnie marza 1.0 (kupiec dalekiego handlu bierze ok. x2 na ryzyko, myto, czas i zysk) i 2 000 km: aksamit 0.50-2.01, bizuteria 0.50-2.02, przyprawy 0.47-2.14,
    /// futra 0.46-2.19, welna 0.41-2.45, narzedzia 0.34-2.93, ruda zelaza 0.29-3.49, skory surowe 0.17-5.73, sol / drewno / glina / kamien - pasmo gry 0.1-10.
    /// Jedzenie bez zmian (psuje sie, nie jedzie daleko - cena glodu jest miejscowa; wlasne kopie wzoru KsFoodCarts / KsFoodCore licza je jak dotad).
    /// Nic z niczego: zmienia sie tylko cena, za ktora zloto przechodzi miedzy kasa miasta, kiesa karawany, gracza i warsztatu. Kupno i odsprzedaz w miejscu dalej na stracie
    /// (ta sama podstawa, kara handlowa w obie strony). Klucz NIE - indeks dokladnie jak dotad (postfiks nic nie robi).
    /// Linia doby (LogGate, po "Ceny na sztuke (299b)"): "Pasmo cen od przewozu (306a): dzien" - pasma, indeks miast bez pasma i z pasmem, przyciete wyceny; grupa pasmo306a.
    /// </summary>
    internal static class PriceBand306
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        /// <summary>Przewoz wozem: 1.5 d za tone na mile angielska (szacunek, Anglia XIV w. - jak RawPrice) = 0.000932 d za kg na km.</summary>
        internal const double CarriagePerKgKm = 1.5 / 1000.0 / 1.609344;
        internal const double GameCap = 10.0;

        private sealed class Snap
        {
            internal Campaign For;
            internal float Margin;
            internal int Km;
            internal Dictionary<ItemCategory, float> K;           // tylko kategorie z pasmem wezszym niz gra
            internal List<KeyValuePair<ItemCategory, string>> Rows; // opis do linii: kategoria -> "id vpk d/kg lo-hi"
            internal int Food, Wide;                                 // jedzenie (bez pasma), przewoz ponad x10 (pasmo gry)
        }

        private static volatile Snap _snap;
        private static readonly object _lock = new object();
        [ThreadStatic] private static bool _raw;                    // odczyt indeksu bez pasma (linia doby: przed / po)
        private static long _floorN, _ceilN;
        private static int _st, _models;
        private static bool _err, _errLine;
        private static readonly List<string> _names = new List<string>();

        internal static bool On { get { var s = Settings.Current; return s != null && s.PriceBandByCarriage; } }

        internal static float MarginOf(Settings s)
        {
            float m = s != null ? s.PriceBandMerchantMargin : 1f;
            if (!(m >= 0.25f)) return 0.25f;                        // takze NaN
            return m > 9f ? 9f : m;
        }

        internal static int KmOf(Settings s)
        {
            int km = s != null ? s.PriceBandHaulKm : 2000;
            return km < 100 ? 100 : km > 6000 ? 6000 : km;
        }

        /// <summary>k pasma dla ceny kilograma vpk (d/kg) - czysty rachunek (self-test w sprawdz_logi pasmo306a liczy ten sam wzor).</summary>
        internal static double KFor(double vpk, double margin, int km)
        {
            double carry = vpk > 0.0 ? CarriagePerKgKm * km / vpk : double.PositiveInfinity;
            double k = (1.0 + margin) * (1.0 + carry);
            return k >= GameCap || double.IsNaN(k) ? GameCap : k;
        }

        internal static void Reset()
        {
            _snap = null; _err = false; _errLine = false;
            System.Threading.Interlocked.Exchange(ref _floorN, 0); System.Threading.Interlocked.Exchange(ref _ceilN, 0);
            System.Threading.Interlocked.Exchange(ref _st, 0);
        }

        /// <summary>Migawka pasm kampanii - po przeliczeniu cen historycznych (wartosc i waga przedmiotow ostateczne); zmiana suwakow = nowa migawka.</summary>
        private static Snap Get(Settings s)
        {
            var sn = _snap;
            var camp = Campaign.Current;
            if (camp == null) return null;
            float m = MarginOf(s); int km = KmOf(s);
            if (sn != null && ReferenceEquals(sn.For, camp) && sn.Margin == m && sn.Km == km) return sn;
            if (HistoricalPrices.On && !HistoricalPrices.Applied) return null;   // ticki startowe przed przeliczeniem - bez pasma (wartosci jeszcze gry)
            lock (_lock)
            {
                sn = _snap;
                if (sn != null && ReferenceEquals(sn.For, camp) && sn.Margin == m && sn.Km == km) return sn;
                sn = Build(camp, m, km);
                _snap = sn;
                return sn;
            }
        }

        private static Snap Build(Campaign camp, float m, int km)
        {
            var rep = new Dictionary<ItemCategory, ItemObject>();
            var objs = MBObjectManager.Instance != null ? MBObjectManager.Instance.GetObjectTypeList<ItemObject>() : null;
            if (objs != null)
            {
                foreach (var it in objs)
                {
                    var c = it != null ? it.ItemCategory : null;
                    if (c == null || !c.IsTradeGood || it.Value <= 0) continue;
                    ItemObject have;
                    if (!rep.TryGetValue(c, out have)) { rep[c] = it; continue; }
                    if (have.StringId == c.StringId) continue;
                    // przedmiot o StringId kategorii; inaczej najtanszy kilogram (najszersze pasmo - regula nie ciasniejsza niz towar kategorii)
                    if (it.StringId == c.StringId || Vpk(it) < Vpk(have)) rep[c] = it;
                }
            }
            var sn = new Snap { For = camp, Margin = m, Km = km, K = new Dictionary<ItemCategory, float>(), Rows = new List<KeyValuePair<ItemCategory, string>>() };
            var rows = new List<KeyValuePair<double, KeyValuePair<ItemCategory, string>>>();
            foreach (var kv in rep)
            {
                var it = kv.Value;
                if (it.IsFood) { sn.Food++; continue; }
                double vpk = Vpk(it);
                double k = KFor(vpk, m, km);
                if (k >= GameCap - 1e-6) { sn.Wide++; continue; }
                sn.K[kv.Key] = (float)k;
                rows.Add(new KeyValuePair<double, KeyValuePair<ItemCategory, string>>(k, new KeyValuePair<ItemCategory, string>(kv.Key,
                    kv.Key.StringId + " " + vpk.ToString(vpk >= 100 ? "0" : "0.0", Inv) + " d/kg " + (1.0 / k).ToString("0.00", Inv) + "-" + k.ToString("0.00", Inv))));
            }
            rows.Sort((a, b) => a.Key.CompareTo(b.Key));
            foreach (var r in rows) sn.Rows.Add(r.Value);
            return sn;
        }

        private static double Vpk(ItemObject it)
        {
            float w = it.Weight;
            return w > 0.01f ? Math.Max(1, it.Value) / (double)w : double.PositiveInfinity;   // bez wagi - przewoz nic nie wazy
        }

        // ------------------------------------------------------------ latka
        /// <summary>Postfiks GetBasePriceFactor kazdego modelu, ktory ja deklaruje (gra, BEE - te same, co RawPrice.FactorPrefix). Przyciecie jest idempotentne (model w modelu -
        /// to samo pasmo); liczniki tylko w najbardziej zewnetrznym wywolaniu (RawPrice.Depth).</summary>
        public static void BandPostfix(ItemCategory __0, ref float __result)
        {
            if (_raw) return;
            try
            {
                if (__0 == null || !__0.IsTradeGood) return;
                var s = Settings.Current;
                if (s == null || !s.PriceBandByCarriage) return;
                var sn = Get(s);
                if (sn == null) return;
                float k;
                if (!sn.K.TryGetValue(__0, out k)) return;
                float lo = 1f / k;
                bool outer = RawPrice.Depth <= 1;
                if (__result < lo) { __result = lo; if (outer) System.Threading.Interlocked.Increment(ref _floorN); }
                else if (__result > k) { __result = k; if (outer) System.Threading.Interlocked.Increment(ref _ceilN); }
            }
            catch (Exception e)
            {
                System.Threading.Interlocked.Increment(ref _st);
                if (!_err) { _err = true; Log.Error("PriceBand306.BandPostfix", e); }   // raz na kampanie; cena zostaje wedle gry
            }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                foreach (var t in RawPrice.ModelsDeclaring(typeof(TradeItemPriceFactorModel), "GetBasePriceFactor"))
                {
                    try
                    {
                        var m = t.GetMethod("GetBasePriceFactor", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        var p = m != null ? m.GetParameters() : null;
                        if (m == null || m.ReturnType != typeof(float) || p.Length != 6 || p[0].ParameterType != typeof(ItemCategory) || p[4].ParameterType != typeof(bool)) continue;
                        h.Patch(m, postfix: new HarmonyMethod(typeof(PriceBand306), nameof(BandPostfix)) { priority = Priority.Last });
                        _models++; _names.Add(t.Name);
                    }
                    catch (Exception e) { Log.Error("PriceBand306.ApplyAll (" + t.FullName + ")", e); }
                }
                var s = Settings.Current;
                Log.Info("Pasmo cen od przewozu (306a): klucz Price Band By Carriage " + (s != null && s.PriceBandByCarriage ? "TAK" : "NIE") + ", marza kupca "
                         + MarginOf(s).ToString("0.00", Inv) + ", droga " + KmOf(s) + " km - postfiks GetBasePriceFactor wpiety w " + _models + " modelach cen [" + string.Join(", ", _names.ToArray())
                         + "]; pasma licza sie w kampanii po przeliczeniu cen historycznych; stan co dobe pomiaru w linii \"Pasmo cen od przewozu (306a): dzien\".");
            }
            catch (Exception e) { Log.Error("PriceBand306.ApplyAll", e); }
        }

        // ------------------------------------------------------------ linia doby
        /// <summary>Linia doby pomiaru (LogGate.Lines): pasma, indeks miast bez pasma -> z pasmem (min / p25 / p50 / p75 / max, rozpietosc max/min) dla 8 kategorii
        /// o najwiekszej rozpietosci bez pasma, miasta-kategorie przy podlodze / suficie, przyciete wyceny od ostatniej linii, potkniecia. Tylko odczyt.</summary>
        internal static void Daily()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || Campaign.Current == null) return;
                int day = (int)CampaignTime.Now.ToDays;
                long fl = System.Threading.Interlocked.Exchange(ref _floorN, 0), ce = System.Threading.Interlocked.Exchange(ref _ceilN, 0);
                int st = System.Threading.Interlocked.Exchange(ref _st, 0);
                var sb = new StringBuilder(3000);
                sb.Append("Pasmo cen od przewozu (306a): dzien ").Append(day).Append(" | klucz ").Append(s.PriceBandByCarriage ? "TAK" : "NIE")
                  .Append(", marza kupca ").Append(MarginOf(s).ToString("0.00", Inv)).Append(", droga ").Append(KmOf(s)).Append(" km, przewoz 1.5 d za tone na mile = ")
                  .Append((CarriagePerKgKm * KmOf(s)).ToString("0.00", Inv)).Append(" d/kg na droge | latka wpieta w ").Append(_models).Append(" modelach");
                Snap sn = GetForLine(s);   // przy kluczu NIE - pasma, ktore wzialby klucz (sam rachunek)
                bool prev;
                if (sn == null) { sb.Append(" | pasma: brak (ceny historyczne jeszcze nie przeliczone)"); Log.Info(sb.Append(" | potkniecia ").Append(st).Append('.').ToString()); return; }
                sb.Append(" | kategorii z pasmem ").Append(sn.K.Count).Append(" (jedzenie bez pasma ").Append(sn.Food).Append(", przewoz ponad x10 - pasmo gry ").Append(sn.Wide).Append("): ");
                for (int i = 0; i < sn.Rows.Count; i++) { if (i > 0) sb.Append(", "); sb.Append(sn.Rows[i].Value); }
                // indeks miast bez pasma i z pasmem
                var stats = new List<KeyValuePair<double, string>>();
                int atLo = 0, atHi = 0, cells = 0;
                foreach (var kv in sn.K)
                {
                    var c = kv.Key; float k = kv.Value, lo = 1f / k;
                    var raw = new List<float>(); var band = new List<float>();
                    foreach (var t in Town.AllTowns)
                    {
                        if (t == null || !t.IsTown || t.MarketData == null) continue;
                        float r;
                        prev = _raw; _raw = true;
                        try { r = t.GetItemCategoryPriceIndex(c); } finally { _raw = prev; }
                        float b = r < lo ? lo : r > k ? k : r;
                        raw.Add(r); band.Add(b); cells++;
                        if (r < lo) atLo++; else if (r > k) atHi++;
                    }
                    if (raw.Count < 2) continue;
                    float rMin = PriceWalk299.Q(raw, 0.0), rMax = PriceWalk299.Q(raw, 1.0), bMin = PriceWalk299.Q(band, 0.0), bMax = PriceWalk299.Q(band, 1.0);
                    double spread = rMin > 0f ? rMax / rMin : 0.0;
                    stats.Add(new KeyValuePair<double, string>(spread, c.StringId + " " + Five(raw) + " (x" + spread.ToString("0.0", Inv) + ") -> " + Five(band)
                        + " (x" + (bMin > 0f ? bMax / bMin : 0f).ToString("0.0", Inv) + ")"));
                }
                stats.Sort((a, b) => b.Key.CompareTo(a.Key));
                sb.Append(" | miasta (indeks bez pasma -> z pasmem: min/p25/p50/p75/max, rozpietosc max/min; 8 najszerszych): ");
                for (int i = 0; i < stats.Count && i < 8; i++) { if (i > 0) sb.Append(" ; "); sb.Append(stats[i].Value); }
                if (stats.Count == 0) sb.Append('-');
                sb.Append(" | miasto-kategorii ").Append(cells).Append(": teraz przy podlodze ").Append(atLo).Append(", przy suficie ").Append(atHi)
                  .Append(" | wycen przycietych od ostatniej linii").Append(s.PriceBandByCarriage ? "" : " (klucz NIE - 0)").Append(": do podlogi ").Append(fl).Append(", do sufitu ").Append(ce)
                  .Append(" | potkniecia ").Append(st).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e)
            {
                if (!_errLine) { _errLine = true; Log.Error("PriceBand306.Daily", e); }
            }
        }

        /// <summary>Migawka do linii takze przy kluczu NIE (pasma, ktore wzialby klucz) - bez zapisu do _snap, gdy klucz NIE i migawki brak.</summary>
        private static Snap GetForLine(Settings s)
        {
            if (HistoricalPrices.On && !HistoricalPrices.Applied) return null;
            return Get(s);
        }

        private static string Five(List<float> l)
        {
            return R(PriceWalk299.Q(l, 0.0)) + "/" + R(PriceWalk299.Q(l, 0.25)) + "/" + R(PriceWalk299.Q(l, 0.5)) + "/" + R(PriceWalk299.Q(l, 0.75)) + "/" + R(PriceWalk299.Q(l, 1.0));
        }

        private static string R(float v) { return float.IsNaN(v) ? "-" : v.ToString("0.00", Inv); }
    }
}
