using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// ZC0 - KOSZYKI W KG (projekt docs/PROJEKT-ZYCIE-CODZIENNE-2026-10-16.md rozdz. 6 i 8.1; liczby jak w synteza/bilans_zc.py, z ktorego wyszly tabele projektu).
    /// Koszyk stanu x kraina [kg albo l na osobe na dobe, srednia z dziecmi]: baza "Zachodu" (BASE), mnozniki profilu krainy (PROF) z gornym limitem dodatku (CAP),
    /// na poludniu wino zamiast piwa, zboze dopelnia kcal stanu (TARGET). Udzial z targu (Mk): miasto 100% poza chlopami miejskimi, wies wedlug MK_PEAS / MK_GENT,
    /// "dom" (straczki, ogrod, mleko) nigdy z targu. Koszyk przetrwania (Allen "bare bones"): 0.47 kg zboza, 0.055 kg straczkow, 0.014 kg miesa, 0.008 kg tluszczu,
    /// 0.01 kg soli, 1 kg plotna na rok, w miescie 1 kg opalu - po cenach LOKALNYCH: miasto - wartosc x indeks targu, wies - cena u bramy miasta targowego (0.6 x).
    /// Tylko liczby w pamieci (bez zapisu) - nic w grze nie zmienia.
    /// </summary>
    internal static partial class DailyLife
    {
        // ------------------------------------------------------------ grupy koszyka (kolejnosc jak w bilans_zc.py)
        internal const int GG = 0, GH = 1, GM = 2, GF = 3, GD = 4, GV = 5, GO = 6, GS = 7, GHn = 8, GSp = 9, GBeer = 10, GWine = 11, GMead = 12, Groups = 13;
        private static readonly string[] GroupName = { "zboze", "dom", "mieso", "ryby", "nabial", "owoce i warzywa", "oliwa", "sol", "miod", "przyprawy", "piwo (tys. l)", "wino (tys. l)", "miod pitny (tys. l)" };
        private static readonly double[] Kcal = { 3300, 1200, 2200, 1100, 2000, 450, 8800, 0, 3000, 3000, 400, 750, 1000 };
        private static bool IsDrink(int g) { return g >= GBeer; }

        // ------------------------------------------------------------ stany (ksiega: 0..5; wielkie rody ponad ksiege - z bohaterow)
        internal const int ESlave = 0, EPeasant = 1, ELabour = 2, ECraft = 3, EMerchant = 4, EGentry = 5, EHouse = 6, Estates = 7;
        private static readonly string[] EstateName = { "niewolni", "chlopi", "biedota", "rzemieslnicy", "kupcy", "szlachta", "wielkie rody" };

        //                      G     H     M      F      D     V     O      S      Hn     Sp      beer  wine  mead
        private static readonly double[][] Base =
        {
            new[] { 0.53, 0.07, 0.012, 0.010, 0.01, 0.03, 0.004, 0.010, 0.0,   0.0,    0.10, 0.0,  0.0   },   // niewolni
            new[] { 0.52, 0.18, 0.025, 0.020, 0.06, 0.06, 0.004, 0.013, 0.002, 0.0,    0.30, 0.01, 0.003 },   // chlopi
            new[] { 0.50, 0.08, 0.030, 0.025, 0.03, 0.05, 0.005, 0.012, 0.002, 0.0,    0.35, 0.01, 0.003 },   // biedota miast
            new[] { 0.48, 0.09, 0.070, 0.040, 0.05, 0.08, 0.008, 0.015, 0.004, 0.0003, 0.55, 0.04, 0.01  },   // rzemieslnicy
            new[] { 0.42, 0.06, 0.200, 0.100, 0.08, 0.14, 0.015, 0.020, 0.008, 0.003,  0.90, 0.35, 0.02  },   // kupcy
            new[] { 0.55, 0.04, 0.330, 0.170, 0.08, 0.12, 0.020, 0.028, 0.015, 0.006,  1.80, 0.35, 0.05  },   // szlachta
            new[] { 0.55, 0.03, 0.550, 0.280, 0.10, 0.15, 0.030, 0.040, 0.020, 0.020,  2.00, 1.00, 0.05  },   // wielkie rody
        };
        private static double[] _target;   // kcal stanu (z Base)

        // profile krain: mnozniki ryby, mieso, nabial, owoce, oliwa, piwo, wino, miod pitny | podzial owocow: jablka, marchew, pomarancze, winogrona, oliwki, daktyle
        private sealed class Prof { public string Id; public double[] Mul; public double[] Vs; }
        private static readonly Prof[] Profs =
        {
            new Prof { Id = "polnoc",      Mul = new[] { 2.0, 1.3, 1.3, 0.5, 0.2, 1.0, 0.1, 1.5 }, Vs = new[] { 0.40, 0.55, 0.0, 0.0, 0.0, 0.05 } },
            new Prof { Id = "wyspy",       Mul = new[] { 15.0, 1.0, 1.0, 0.3, 0.2, 1.0, 0.2, 1.0 }, Vs = new[] { 0.40, 0.55, 0.0, 0.0, 0.0, 0.05 } },
            new Prof { Id = "dorzecze",    Mul = new[] { 1.3, 1.0, 1.3, 1.0, 0.3, 1.2, 0.3, 0.8 }, Vs = new[] { 0.45, 0.45, 0.0, 0.10, 0.0, 0.0 } },
            new Prof { Id = "zachod",      Mul = new[] { 1.6, 1.0, 1.0, 1.0, 0.6, 1.0, 0.6, 0.8 }, Vs = new[] { 0.40, 0.40, 0.0, 0.15, 0.05, 0.0 } },
            new Prof { Id = "reach",       Mul = new[] { 1.0, 1.1, 0.9, 1.6, 0.8, 0.6, 2.0, 0.8 }, Vs = new[] { 0.35, 0.25, 0.05, 0.20, 0.15, 0.0 } },
            new Prof { Id = "dorne",       Mul = new[] { 1.3, 0.6, 0.5, 2.0, 2.0, 0.2, 1.8, 0.3 }, Vs = new[] { 0.05, 0.15, 0.30, 0.15, 0.25, 0.10 } },
            new Prof { Id = "wm_polnoc",   Mul = new[] { 5.0, 1.0, 1.0, 0.8, 0.5, 1.2, 0.8, 0.8 }, Vs = new[] { 0.40, 0.45, 0.0, 0.10, 0.05, 0.0 } },
            new Prof { Id = "wm_wnetrze",  Mul = new[] { 0.8, 1.3, 1.1, 1.0, 0.8, 1.2, 0.8, 0.8 }, Vs = new[] { 0.40, 0.40, 0.0, 0.15, 0.05, 0.0 } },
            new Prof { Id = "wm_poludnie", Mul = new[] { 2.0, 0.8, 0.7, 1.5, 1.8, 0.2, 2.0, 0.2 }, Vs = new[] { 0.10, 0.20, 0.15, 0.20, 0.30, 0.05 } },
            new Prof { Id = "wschod",      Mul = new[] { 1.3, 0.8, 0.7, 1.6, 1.5, 0.3, 1.2, 0.2 }, Vs = new[] { 0.05, 0.20, 0.10, 0.15, 0.20, 0.30 } },
            new Prof { Id = "letnie",      Mul = new[] { 12.0, 0.4, 0.3, 2.5, 0.5, 0.0, 1.0, 0.5 }, Vs = new[] { 0.20, 0.20, 0.40, 0.10, 0.0, 0.10 } },
            new Prof { Id = "step",        Mul = new[] { 0.2, 2.5, 3.0, 0.2, 0.1, 0.0, 0.0, 0.5 }, Vs = new[] { 0.30, 0.50, 0.0, 0.0, 0.0, 0.20 } },
        };
        private const int ProfDefault = 3;   // "zachod" - kultura spoza tabeli (liczona w linii)
        private static readonly int[] MulGroup = { GF, GM, GD, GV, GO, GBeer, GWine, GMead };
        private static readonly Dictionary<int, double> Cap = new Dictionary<int, double> { { GF, 0.30 }, { GM, 0.30 }, { GD, 0.35 }, { GV, 0.25 }, { GO, 0.03 }, { GBeer, 1.0 }, { GWine, 1.0 }, { GMead, 0.3 } };
        private static readonly Dictionary<string, int> Land2Prof = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "battania", 0 }, { "freefolk", 0 }, { "nightswatch", 0 }, { "skagosi", 1 }, { "sturgia", 1 }, { "dragonstone", 1 },
            { "river", 2 }, { "vale", 2 }, { "vlandia", 3 }, { "crownlands", 3 }, { "stormlands", 3 }, { "reach", 4 }, { "aserai", 5 },
            { "empire", 6 }, { "nord", 6 }, { "ibbenese", 6 }, { "pentoshi", 6 }, { "norvos", 7 }, { "qohorik", 7 },
            { "volantine", 8 }, { "lyseni", 8 }, { "myrish", 8 }, { "tyroshi", 8 }, { "valyrian", 8 },
            { "ghiscari", 9 }, { "qartheen", 9 }, { "yiti", 9 }, { "summer", 10 }, { "khuzait", 11 }, { "sarnor", 11 },
        };
        private static readonly Dictionary<string, double> IsleFish = new Dictionary<string, double>(StringComparer.Ordinal) { { "lyseni", 1.6 }, { "tyroshi", 1.6 } };

        // udzial z targu (reszta: wlasne pole, spichlerz, majatek, warzone w domu)
        private static readonly double[] MkPeas = { 0.05, 0, 0.20, 0.25, 0.10, 0.10, 0.50, 1.0, 0.20, 1.0, 0.10, 0.10, 0.30 };
        private static readonly double[] MkGent = { 0.30, 0, 0.40, 0.80, 0.30, 0.30, 1.00, 1.0, 0.50, 1.0, 0.30, 0.70, 0.50 };

        /// <summary>Udzial grupy g z targu dla stanu e (town = miasto; zamek i wies - jak wies).</summary>
        private static double Mk(int e, bool town, int g)
        {
            if (g == GH) return 0.0;
            if (e == EHouse) return 1.0;
            if (town && e != EPeasant) return 1.0;
            if (e == EPeasant || e == ECraft || e == EMerchant) return MkPeas[g];
            if (e == EGentry) return MkGent[g];
            if (e == ESlave) return g == GS ? 1.0 : 0.05;
            return 1.0;
        }

        // koszyk (stan, profil, ryby wysp) - liczony raz na sesje, tablice nigdy nie zmieniane po zbudowaniu
        private static readonly Dictionary<string, double[][]> _basket = new Dictionary<string, double[][]>(StringComparer.Ordinal);

        private static int ProfOf(string cul, out bool known)
        {
            int p = ProfDefault;
            known = cul != null && Land2Prof.TryGetValue(cul, out p);
            return known ? p : ProfDefault;
        }

        /// <summary>Koszyk 7 stanow dla kultury (kraina) - kg (napoje l) na osobe na dobe w 13 grupach.</summary>
        private static double[][] BasketOf(string cul)
        {
            string key = cul ?? "";
            double[][] b;
            if (_basket.TryGetValue(key, out b)) return b;
            if (_target == null)
            {
                _target = new double[Estates];
                for (int e = 0; e < Estates; e++) { double t = 0; for (int g = 0; g < Groups; g++) t += Base[e][g] * Kcal[g]; _target[e] = t; }
            }
            bool known;
            var pr = Profs[ProfOf(cul, out known)];
            double fish;
            if (cul == null || !IsleFish.TryGetValue(cul, out fish)) fish = 1.0;
            b = new double[Estates][];
            for (int e = 0; e < Estates; e++)
            {
                var bs = Base[e];
                var o = (double[])bs.Clone();
                for (int i = 0; i < MulGroup.Length; i++)
                {
                    int g = MulGroup[i];
                    double m = pr.Mul[i];
                    if (g == GF) m *= fish;
                    double v = bs[g] * m, cap;
                    if (m > 1.0 && Cap.TryGetValue(g, out cap)) v = Math.Min(v, bs[g] + cap);
                    o[g] = v;
                }
                if (pr.Mul[6] >= 1.5 && pr.Mul[5] < 1.0) o[GWine] += Math.Min(0.25, (bs[GBeer] - o[GBeer]) * 0.6);   // poludnie: wino rozcienczone zamiast piwa
                double rest = 0;
                for (int g = 1; g < Groups; g++) rest += o[g] * Kcal[g];
                o[GG] = Math.Max(0.25, (_target[e] - rest) / Kcal[GG]);
                b[e] = o;
            }
            _basket[key] = b;
            return b;
        }

        // ------------------------------------------------------------ pasma stanow (rozdz. 5.2; niewolni zabieraja swoj udzial z calej reszty)
        private static readonly Dictionary<string, double[]> SlaveBand = new Dictionary<string, double[]>(StringComparer.Ordinal)
        {
            { "volantine", new[] { 0.70, 0.30 } }, { "ghiscari", new[] { 0.55, 0.35 } }, { "qartheen", new[] { 0.40, 0.20 } }, { "valyrian", new[] { 0.30, 0.25 } },
            { "lyseni", new[] { 0.35, 0.20 } }, { "myrish", new[] { 0.35, 0.20 } }, { "tyroshi", new[] { 0.35, 0.20 } }, { "qohorik", new[] { 0.25, 0.12 } },
            { "norvos", new[] { 0.15, 0.08 } }, { "khuzait", new[] { 0.10, 0.10 } }, { "sturgia", new[] { 0.06, 0.05 } },
        };
        private static readonly HashSet<string> FreeCity = new HashSet<string>(StringComparer.Ordinal) { "empire", "pentoshi", "nord", "ibbenese", "yiti", "summer", "sarnor" };

        /// <summary>Udzialy 6 stanow ksiegi (bez rodow) w osadzie kultury cul: town = miasto, inaczej wies. Suma = 1.</summary>
        private static void Bands(string cul, bool town, double[] sh)
        {
            Array.Clear(sh, 0, sh.Length);
            double[] sv;
            double s = cul != null && SlaveBand.TryGetValue(cul, out sv) ? (town ? sv[0] : sv[1]) : 0.0;
            if (town)
            {
                if (cul == "crownlands") { sh[EPeasant] = 0.02; sh[ELabour] = 0.57; sh[ECraft] = 0.30; sh[EMerchant] = 0.07; sh[EGentry] = 0.04; }
                else if (cul != null && (FreeCity.Contains(cul) || (SlaveBand.ContainsKey(cul) && cul != "sturgia" && cul != "khuzait")))
                { sh[EPeasant] = 0.02; sh[ELabour] = 0.53; sh[ECraft] = 0.33; sh[EMerchant] = 0.10; sh[EGentry] = 0.02; }
                else { sh[EPeasant] = 0.02; sh[ELabour] = 0.60; sh[ECraft] = 0.30; sh[EMerchant] = 0.06; sh[EGentry] = 0.02; }
            }
            else
            {
                if (cul == "freefolk") { sh[EPeasant] = 0.99; sh[ECraft] = 0.005; sh[EMerchant] = 0.0; sh[EGentry] = 0.005; }
                else if (cul == "khuzait") { sh[EPeasant] = 0.975; sh[ECraft] = 0.005; sh[EMerchant] = 0.0; sh[EGentry] = 0.02; }
                else if (cul == "reach") { sh[EPeasant] = 0.948; sh[ECraft] = 0.03; sh[EMerchant] = 0.002; sh[EGentry] = 0.02; }
                else if (cul == "battania" || cul == "skagosi" || cul == "nightswatch") { sh[EPeasant] = 0.958; sh[ECraft] = 0.03; sh[EMerchant] = 0.002; sh[EGentry] = 0.01; }
                else { sh[EPeasant] = 0.953; sh[ECraft] = 0.03; sh[EMerchant] = 0.002; sh[EGentry] = 0.015; }
            }
            for (int e = 1; e < 6; e++) sh[e] *= 1.0 - s;
            sh[ESlave] = s;
        }

        // ------------------------------------------------------------ towary do cen (indeks w tablicy cen osady)
        private const int IGrain = 0, IBread = 1, IPie = 2, IMeat = 3, IFish = 4, ICheese = 5, IButter = 6, IEgg = 7, IApple = 8, ICarrot = 9, IOrange = 10, IGrape = 11,
                          IOlives = 12, IDate = 13, IOil = 14, ISalt = 15, IHoney = 16, ISpice = 17, IBeer = 18, IWine = 19, IMead = 20, ILinen = 21, IWood = 22, Items = 23;
        private static readonly string[] ItemIds = { "grain", "bread", "pie", "meat", "fish", "cheese", "butter", "Egg", "apple", "carrot", "orange", "grape",
                                                     "olives", "date_fruit", "oil", "salt", "honey", "spice", "beer", "wine", "mead", "linen", "hardwood" };
        // d za kg (napoje: za litr) po wartosci bazowej - gdy towaru nie ma w grze (tabela rozdz. 1 projektu)
        private static readonly double[] FallbackKg = { 0.3, 0.6, 1.0, 0.9, 1.0, 1.3, 2.4, 0.8, 0.2, 0.2, 1.0, 0.3, 0.5, 6.0, 4.0, 0.2, 1.4, 27.0, 0.3, 1.4, 1.0, 10.0, 0.04 };
        private static readonly int[] FruitItem = { IApple, ICarrot, IOrange, IGrape, IOlives, IDate };
        // rzeczy nie-jedzenie (tylko waga podzialu gotowki miedzy stany - zwykly koszyk po cenach bazowych): plotno, sukno, skora, narzedzia, garnki, swiatlo, futro, aksamit, klejnoty (g)
        private static readonly string[] NfIds = { "linen", "felt", "leather", "tools", "pottery", "whale_oil", "fur", "velvet", "jewelry" };
        private static readonly double[] NfFallbackKg = { 10.0, 20.0, 4.0, 4.0, 0.8, 2.4, 20.0, 400.0, 240.0 };
        private static readonly double[][] NonFood =
        {
            new[] { 0.8, 0.2, 0.2, 0.0, 0.3, 0.0, 0.0, 0.0, 0.0 },
            new[] { 1.2, 0.8, 0.5, 0.4, 1.0, 0.3, 0.02, 0.0, 0.0 },
            new[] { 1.0, 0.5, 0.4, 0.1, 0.8, 0.3, 0.0, 0.0, 0.0 },
            new[] { 1.5, 1.2, 0.8, 1.0, 1.2, 0.8, 0.03, 0.001, 0.3 },
            new[] { 3.0, 4.0, 1.5, 0.3, 2.5, 3.0, 0.3, 0.05, 5.0 },
            new[] { 4.0, 6.0, 3.0, 0.5, 3.0, 5.0, 0.8, 0.10, 10.0 },
            new[] { 6.0, 10.0, 4.0, 0.5, 5.0, 15.0, 2.0, 0.60, 60.0 },
        };
        private static readonly double[] FirewoodKg = { 1.0, 1.5, 1.0, 1.5, 2.5, 4.0, 6.0 };

        private static ItemObject[] _items;          // towary cen (null = brak w grze - cena z FallbackKg)
        private static double[] _pxBase;             // d za kg po wartosci bazowej (indeks 1.0)
        private static double[] _usual;              // zwykly koszyk stanu po cenach bazowych (zl/os/d) - waga podzialu gotowki osady miedzy stany
        private static int _itemsMissing;

        private static void EnsureItems()
        {
            if (_items != null) return;
            var items = new ItemObject[Items];
            var bas = new double[Items];
            int miss = 0;
            var om = MBObjectManager.Instance;
            for (int i = 0; i < Items; i++)
            {
                ItemObject it = null;
                try { it = om != null ? om.GetObject<ItemObject>(ItemIds[i]) : null; } catch { it = null; }
                if (it == null || it.ItemCategory == null) { miss++; it = null; }
                items[i] = it;
                bas[i] = it != null ? KgPrice(it, 1.0, FallbackKg[i]) : FallbackKg[i];
            }
            var nf = new double[NfIds.Length];
            for (int j = 0; j < NfIds.Length; j++)
            {
                ItemObject it = null;
                try { it = om != null ? om.GetObject<ItemObject>(NfIds[j]) : null; } catch { it = null; }
                nf[j] = it != null ? KgPrice(it, 1.0, NfFallbackKg[j]) : NfFallbackKg[j];
            }
            nf[8] /= 1000.0;   // klejnoty w gramach
            // zwykly koszyk stanu (Zachod, ceny bazowe): jedzenie + rzeczy na rok / 364 + opal - tylko waga podzialu gotowki (rozdz. 6.1 "koszt koszykow")
            var b = BasketOf("vlandia");
            var vs = Profs[3].Vs;
            var usual = new double[Estates];
            for (int e = 0; e < Estates; e++)
            {
                double food = 0;
                for (int g = 0; g < Groups; g++) food += b[e][g] * GroupPrice(bas, g, false, vs);
                double nfc = 0;
                for (int j = 0; j < NfIds.Length; j++) nfc += NonFood[e][j] / 364.0 * nf[j];
                usual[e] = food + nfc + FirewoodKg[e] * bas[IWood];
            }
            _pxBase = bas; _usual = usual; _itemsMissing = miss;
            _items = items;
        }

        /// <summary>d za kg (napoje: za litr - 10 kg sztuki = 10 l) przy indeksie idx. Waga 0 - kg z tabeli (wartosc / d za kg z tabeli).</summary>
        private static double KgPrice(ItemObject it, double idx, double fallback)
        {
            double w = it.Weight;
            if (!(w > 0.01) || double.IsInfinity(w)) w = it.Value > 0 && fallback > 0 ? it.Value / fallback : 10.0;
            double v = Math.Max(0, it.Value) * idx / w;
            return v > 0 && !double.IsInfinity(v) ? v : fallback * idx;
        }

        /// <summary>Ceny za kg 23 towarow w miescie - wartosc x indeks kategorii targu (TownMarketData.GetPriceFactor). Brak towaru - cena z tabeli x 1.</summary>
        private static void TownPrices(Town town, double[] px, Dictionary<ItemCategory, double> idxMemo)
        {
            idxMemo.Clear();
            for (int i = 0; i < Items; i++)
            {
                var it = _items[i];
                if (it == null || town == null) { px[i] = _pxBase[i]; continue; }
                double ix;
                if (!idxMemo.TryGetValue(it.ItemCategory, out ix))
                {
                    try { ix = town.GetItemCategoryPriceIndex(it.ItemCategory); } catch { ix = 1.0; _priceStumbles++; }
                    if (!(ix > 0.0) || double.IsInfinity(ix) || double.IsNaN(ix)) ix = 1.0;
                    idxMemo[it.ItemCategory] = ix;
                }
                px[i] = KgPrice(it, ix, FallbackKg[i]);
            }
        }

        /// <summary>Cena za kg grupy koszyka: zboze mieszczan (poza chlopami miejskimi) - chleb 45%, placek 10%, ziarno 45% (rozdz. 8.1); "dom" wyceniony jak marchew;
        /// nabial - ser 50%, maslo 25%, jaja 25%; owoce wedlug profilu krainy.</summary>
        private static double GroupPrice(double[] px, int g, bool townMix, double[] vs)
        {
            switch (g)
            {
                case GG: return townMix ? 0.45 * px[IBread] + 0.10 * px[IPie] + 0.45 * px[IGrain] : px[IGrain];
                case GH: return px[ICarrot];
                case GM: return px[IMeat];
                case GF: return px[IFish];
                case GD: return 0.5 * px[ICheese] + 0.25 * px[IButter] + 0.25 * px[IEgg];
                case GV:
                    {
                        double s = 0;
                        for (int i = 0; i < 6; i++) s += vs[i] * px[FruitItem[i]];
                        return s;
                    }
                case GO: return px[IOil];
                case GS: return px[ISalt];
                case GHn: return px[IHoney];
                case GSp: return px[ISpice];
                case GBeer: return px[IBeer];
                case GWine: return px[IWine];
                default: return px[IMead];
            }
        }

        /// <summary>Koszyk przetrwania na osobe na dobe przy cenach px (bez mnoznika bramy); town - z 1 kg opalu.</summary>
        private static double SurvivalBasket(double[] px, bool town)
        {
            double k = 0.47 * px[IGrain] + 0.055 * px[ICarrot] + 0.014 * px[IMeat] + 0.008 * px[IOil] + 0.01 * px[ISalt] + px[ILinen] / 364.0;
            if (town) k += px[IWood];
            return k;
        }

        /// <summary>Grupa koszyka towaru gry (Spizarnia: targ dzis) albo -1.</summary>
        private static int GroupOfItem(string id)
        {
            switch (id)
            {
                case "grain": case "bread": case "pie": return GG;
                case "meat": case "WhaleMeat": return GM;
                case "fish": return GF;
                case "cheese": case "butter": case "Egg": return GD;
                case "apple": case "orange": case "carrot": case "grape": case "olives": case "date_fruit": return GV;
                case "oil": return GO;
                case "salt": return GS;
                case "honey": return GHn;
                case "spice": return GSp;
                case "beer": return GBeer;
                case "wine": return GWine;
                case "mead": return GMead;
                default: return -1;
            }
        }
    }
}
