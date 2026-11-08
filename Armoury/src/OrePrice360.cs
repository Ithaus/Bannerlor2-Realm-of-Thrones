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
    /// 360d (Jeff 08.11: "czemu nie liczy sie kruszcow na wozach - powinno liczyc sie cale srebro w obiegu gospodarki aby wycena byla prawidlowa") - CENA SREBRA
    /// I RUDY ZLOTA WIDZI CALY ZAPAS W OBIEGU.
    ///
    /// Przyczyna: pamiec podazy miasta (S, ItemConsumptionBehavior.UpdateSupplyAndDemand -> GetSupplyDemandForCategory: S = 0.85 S + 0.15 polka) zna tylko polke
    /// miasta. Srebro: polki ok. 700-1000 szt., a w obiegu ok. 25-28 tys. (wozy wsi 23.8 tys. - at360, 109653) - indeks ok. 3, cena miasta nie spada, choc swiat
    /// wydobywa 3 razy wiecej, niz zuzywa.
    ///
    /// Zmiana (klucz Ore Price Sees All Stock, OreStockInPrice): w postfiksie tempa pamieci podazy (RawPrice.SupplyPacePostfix, wywolanie dobowe gry) podaz
    /// kategorii srebra i rudy zlota dostaje udzial miasta w zapasie rynku POZA polkami miast: X_t = O_m x D_t / suma D rynku (O_m - wsie, zamki, wozy wsi,
    /// juki karawan, wozy surowcow rynku Westeros/Essos w wartosci polki - OreStock360.Census raz na dobe; D_t - popyt kategorii miasta). Rozdzial wedlug popytu,
    /// nie odleglosci (decyzja Jeffa 31.10 "cena od polki, nie od odleglosci"); wlasna polka miasta liczy sie dalej osobno, cudze polki nie wchodza do X
    /// (bez liczenia dwa razy). Pamiec jak 299b: S_pelne = podaz bazowa (gra / 299b) + tempo x X, czyli S idzie do polka + X tempem pamieci.
    /// Bez skokow: S_nowe = najwieksze S z [baza, S_pelne], przy ktorym czynnik ceny gry (z prefiksami 299/299c i pasmem 306a) to co najmniej 0.95 x czynnik
    /// przy wczorajszej podazy - cena spada najwyzej 5% na dobe; wzrost bez limitu. Straznik: tylko wywolanie dobowe (wartosc polki i podaz = dane rynku miasta) -
    /// sondy (FoodSubstitute, HarvestFairs, NoMakerBudget, PriceWalk299b) dodatku nie dostaja.
    /// Cena widziana = cena placona: S jest w danych rynku gry (ekran handlu, karawany, wozy, mennica, _px KS czytaja to samo) i w zapisie gry - bez nowego
    /// klucza zapisu. Zmienia sie tylko cena - zadna moneta ani sztuka nie powstaje. Klucz NIE - podaz jak dotad; S wraca do polki tempem pamieci.
    /// </summary>
    internal static class OrePrice360
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int Mt = OreStock360.Metals;
        private const double MaxDrop = 0.95;
        private const int Steps = 16;

        internal static bool On { get { var s = Settings.Current; return s != null && s.OreStockInPrice; } }

        private static Campaign _camp;
        private static readonly ItemCategory[] _cat = new ItemCategory[Mt];
        private static int _day = int.MinValue;
        private static readonly long[,,] _w = new long[Mt, 2, OreStock360.HN];
        private static bool _wOk;
        private static readonly double[,] _out = new double[Mt, 2];               // O_m: wartosc zapasu rynku poza polkami
        private static readonly Dictionary<Town, double[]> _x = new Dictionary<Town, double[]>();   // X_t wedlug kruszcu (wartosc polki)
        // liczniki od ostatniej linii [kruszec, rynek]
        private static readonly int[,] _nAdj = new int[Mt, 2], _nClip = new int[Mt, 2];
        private static readonly double[,] _sumX = new double[Mt, 2], _sumAdd = new double[Mt, 2], _sumFull = new double[Mt, 2];
        private static int _stum;
        private static bool _err;

        private static void ResetIfNew()
        {
            var c = Campaign.Current;
            if (ReferenceEquals(c, _camp)) return;
            _camp = c; _day = int.MinValue; _wOk = false; _x.Clear(); _err = false; _stum = 0;
            for (int m = 0; m < Mt; m++) _cat[m] = null;
            Clear();
        }

        private static void Clear()
        {
            Array.Clear(_nAdj, 0, _nAdj.Length); Array.Clear(_nClip, 0, _nClip.Length);
            Array.Clear(_sumX, 0, _sumX.Length); Array.Clear(_sumAdd, 0, _sumAdd.Length); Array.Clear(_sumFull, 0, _sumFull.Length);
        }

        private static ItemCategory CatOf(int m)
        {
            if (_cat[m] != null) return _cat[m];
            var it = OreStock360.ItemOf(m);
            _cat[m] = it != null ? it.ItemCategory : null;
            return _cat[m];
        }

        private static void Stumble(Exception e)
        {
            _stum++;
            if (!_err) { _err = true; try { Log.Error("OrePrice360.Supply", e); } catch { } }   // raz na kampanie; reszta w liczniku linii
        }

        /// <summary>Spis raz na dobe (pierwsze wywolanie dobowe w nowej dobie): O_m wedlug kruszcu i rynku, X_t kazdego miasta wedlug popytu kategorii.</summary>
        private static void EnsureDay()
        {
            int d = (int)CampaignTime.Now.ToDays;
            if (d == _day) return;
            _day = d;
            _x.Clear();
            Array.Clear(_out, 0, _out.Length);
            _wOk = OreStock360.Census(_w);
            if (!_wOk) return;
            var towns = new List<Town>();
            foreach (var t in Town.AllTowns) if (t != null && t.IsTown && t.Settlement != null && t.MarketData != null) towns.Add(t);
            for (int m = 0; m < Mt; m++)
            {
                var it = OreStock360.ItemOf(m); var cat = CatOf(m);
                if (it == null || cat == null) continue;
                double pw = Math.Max(0, HistoricalPrices.ShelfWorthOf(cat, it.Value));   // waga sztuki na polce (InStoreValue), jak w danych rynku
                for (int mk = 0; mk < 2; mk++) _out[m, mk] = OreStock360.Sum(_w, m, mk, OreStock360.HVil) * pw;
                var sd = new double[2]; var nt = new int[2];
                foreach (var t in towns)
                {
                    int mk = StartCalibration.TownMarketOf(t); if (mk != 0 && mk != 1) continue;
                    sd[mk] += Math.Max(0f, t.MarketData.GetCategoryData(cat).Demand); nt[mk]++;
                }
                foreach (var t in towns)
                {
                    int mk = StartCalibration.TownMarketOf(t); if ((mk != 0 && mk != 1) || nt[mk] == 0 || !(_out[m, mk] > 0)) continue;
                    double share = sd[mk] > 0 ? Math.Max(0f, t.MarketData.GetCategoryData(cat).Demand) / sd[mk] : 1.0 / nt[mk];
                    double[] a; if (!_x.TryGetValue(t, out a)) { a = new double[Mt]; _x[t] = a; }
                    a[m] = _out[m, mk] * share;
                }
            }
        }

        /// <summary>RawPrice.SupplyPacePostfix (po tempie pamieci 299b, MemoryLive): dodatek zapasu rynku poza polkami do podazy kategorii srebra i rudy zlota.
        /// inStore/oldSupply - argumenty wywolania (straznik wywolania dobowego), pace - tempo pamieci podazy. Nie rzuca.</summary>
        internal static void Supply(Town t, ItemCategory cat, float inStore, float oldSupply, double pace, ref ValueTuple<float, float> res)
        {
            try
            {
                if (t == null || cat == null || !On) return;
                ResetIfNew();
                int m0 = -1;
                for (int m = 0; m < Mt; m++) if (ReferenceEquals(cat, CatOf(m))) { m0 = m; break; }
                if (m0 < 0) return;
                var md = t.MarketData; if (md == null) return;
                var cd = md.GetCategoryData(cat);
                if (cd.InStoreValue != inStore || cd.Supply != oldSupply) return;   // sonda, nie wywolanie dobowe
                EnsureDay();
                double[] xa;
                if (!_wOk || !_x.TryGetValue(t, out xa)) return;
                double x = 0;
                for (int m = 0; m < Mt; m++) if (ReferenceEquals(cat, CatOf(m))) x += xa[m];   // srebro i ruda zlota w jednej kategorii - jeden dodatek
                if (!(x > 0)) return;
                var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.TradeItemPriceFactorModel : null;
                if (model == null) return;
                float b0 = res.Item1, dn = res.Item2;
                double full = b0 + Math.Max(0.0, pace) * x;
                double sNew; bool clip = false;
                PriceBand306.Quiet++;                                     // 360r-c/d: wyceny probne nie licza sie do linii pasma 306a
                try
                {
                    double lim = MaxDrop * model.GetBasePriceFactor(cat, inStore, oldSupply, dn, false, 0);
                    if (model.GetBasePriceFactor(cat, inStore, (float)full, dn, false, 0) >= lim) sNew = full;
                    else
                    {
                        clip = true;
                        if (model.GetBasePriceFactor(cat, inStore, b0, dn, false, 0) < lim) sNew = b0;   // gra sama zbija cene ponad limit - bez dodatku
                        else
                        {
                            double lo = b0, hi = full;
                            for (int i = 0; i < Steps; i++)
                            {
                                double mid = 0.5 * (lo + hi);
                                if (model.GetBasePriceFactor(cat, inStore, (float)mid, dn, false, 0) >= lim) lo = mid; else hi = mid;
                            }
                            sNew = lo;
                        }
                    }
                }
                finally { PriceBand306.Quiet--; }
                res = new ValueTuple<float, float>(Math.Max(0.1f, (float)sNew), dn);
                int mk = StartCalibration.TownMarketOf(t);
                if (mk == 0 || mk == 1)
                {
                    _nAdj[m0, mk]++; if (clip) _nClip[m0, mk]++;
                    _sumX[m0, mk] += x; _sumAdd[m0, mk] += sNew - b0; _sumFull[m0, mk] += full - b0;
                }
            }
            catch (Exception e) { Stumble(e); }
        }

        /// <summary>Raz na dobe (ArmouryBehavior, po linii pasma 306a): linia "Kruszec w cenie (360d)" w dobie pomiaru (LogGate.Lines), liczniki od zera co dobe.</summary>
        internal static void Daily(bool line)
        {
            try
            {
                ResetIfNew();
                if (line && On) Line();
            }
            catch (Exception e) { Stumble(e); }
            finally { Clear(); _stum = 0; }
        }

        private static void Line()
        {
            var sb = new StringBuilder(1800);
            int day = (int)CampaignTime.Now.ToDays - 1;
            sb.Append("Kruszec w cenie (360d): dzien ").Append(day).Append(" | klucz TAK | spis z dnia ").Append(_day == int.MinValue ? "-" : _day.ToString(Inv)).Append(_wOk ? " OK" : " BLAD/brak - bez dodatku")
              .Append(" | podaz miasta = pamiec gry + tempo x udzial miasta (wedlug popytu) w zapasie rynku poza polkami; spadek ceny najwyzej ").Append(((1 - MaxDrop) * 100).ToString("0", Inv)).Append("%/d");
            for (int m = 0; m < Mt; m++)
            {
                var cat = CatOf(m);
                for (int mk = 0; mk < 2; mk++)
                {
                    sb.Append(" | ").Append(OreStock360.MetalId[m]).Append(mk == 0 ? " Westeros" : " Essos").Append(": polki ").Append(_w[m, mk, OreStock360.HShelf])
                      .Append(" szt., poza polkami ").Append(OreStock360.Sum(_w, m, mk, OreStock360.HVil)).Append(" (");
                    for (int h = OreStock360.HVil; h < OreStock360.HN; h++) sb.Append(h > OreStock360.HVil ? ", " : "").Append(OreStock360.HolderName[h]).Append(' ').Append(_w[m, mk, h]);
                    int n = _nAdj[m, mk];
                    sb.Append("), wartosc poza polkami ").Append(_out[m, mk].ToString("0", Inv)).Append(", miast z dodatkiem ").Append(n);
                    if (n > 0)
                        sb.Append(", srednie X ").Append((_sumX[m, mk] / n).ToString("0", Inv)).Append(", dodane do podazy srednio ").Append((_sumAdd[m, mk] / n).ToString("0.0", Inv))
                          .Append(" z ").Append((_sumFull[m, mk] / n).ToString("0.0", Inv)).Append(", przyciete limitem ").Append(_nClip[m, mk]);
                    // mediana indeksu ceny kategorii w miastach rynku (ta sama cena, co ekran handlu)
                    if (cat != null)
                    {
                        var ix = new List<double>();
                        foreach (var t in Town.AllTowns)
                        {
                            if (t == null || !t.IsTown || StartCalibration.TownMarketOf(t) != mk) continue;
                            try { ix.Add(t.GetItemCategoryPriceIndex(cat)); } catch { }
                        }
                        if (ix.Count > 0) { ix.Sort(); double med = ix.Count % 2 == 1 ? ix[ix.Count / 2] : 0.5 * (ix[ix.Count / 2 - 1] + ix[ix.Count / 2]); sb.Append(", mediana indeksu ").Append(med.ToString("0.00", Inv)); }
                    }
                }
            }
            sb.Append(" | potkniecia ").Append(_stum).Append(" (spis ").Append(OreStock360.Stumbles).Append(")").Append('.');
            Log.Info(sb.ToString());
        }
    }
}
