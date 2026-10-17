using System;
using System.Collections.Generic;

namespace Armoury
{
    /// <summary>
    /// KS (kalibracja startu, docs/PROJEKT-KALIBRACJA-STARTU-2026-10-16.md rozdz. 2-3) - JEDNA TABELA WYDAJNOSCI SWIATA i reguly ziemi.
    /// Port 1:1 tabel modelu v2 (scratchpad roz/kalibracja: ks_tabele.py + ks_tabele2.py, wariant zalecany "strefa, KS-K, premia mapy 1.25"):
    /// P0 [kg na pracujacego na dobe przy ziemi 1.0, zwykly rok], koszt K [d na pracujacego na dobe], ha na pracujacego, LAMBDA stanow, przydatnosc ziemi
    /// (strefa x teren x woda), regale (zloza, las na sprzedaz, oazy, ogrody przypraw). Zadnego czynnika na okreg, kraine ani towar.
    /// Bez typow gry - czysty C# (ten sam plik kompiluje test portu wobec modelu Pythona).
    /// </summary>
    internal static class KsTab
    {
        // ------------------------------------------------------------ towary modelu (kolejnosc jak GOODS w ks_model2.py)
        internal static readonly string[] Goods =
        {
            "grain", "fish", "meat", "cheese", "butter", "Egg", "apple", "carrot", "orange", "grape", "olives", "date_fruit", "salt", "honey", "spice",
            "wool", "flax", "cotton", "hides", "hardwood", "clay", "limestone", "marble", "iron", "silver", "gold_ore", "fur", "whale_oil", "walrus_tusk",
            "cow", "hog", "sheep", "WhaleMeat", "horse"
        };
        internal const int NG = 34;
        internal const int GGrain = 0, GFish = 1, GMeat = 2, GCheese = 3, GButter = 4, GEgg = 5, GApple = 6, GCarrot = 7, GOrange = 8, GGrape = 9, GOlives = 10,
                           GDate = 11, GSalt = 12, GHoney = 13, GSpice = 14, GWool = 15, GFlax = 16, GCotton = 17, GHides = 18, GWood = 19, GClay = 20, GLime = 21,
                           GMarble = 22, GIron = 23, GSilver = 24, GGold = 25, GFur = 26, GWhaleOil = 27, GTusk = 28, GCow = 29, GHog = 30, GSheep = 31,
                           GWhaleMeat = 32, GHorse = 33;
        internal static readonly Dictionary<string, int> GoodIndex = Index(Goods);

        // ------------------------------------------------------------ potrzeby osady (klucze needs_of modelu; kg albo l na dobe)
        internal static readonly string[] NeedKeys =
        {
            "G", "H", "meat", "fish", "cheese", "butter", "Egg", "apple", "carrot", "orange", "grape", "olives", "date_fruit", "oil", "salt", "honey", "spice",
            "beer", "wine", "mead", "plotno", "sukno", "skora", "narzedzia", "garnki", "swiatlo", "futro", "jedwab", "klejnoty_g", "swiatlo_olej", "swiatlo_tran", "drewno"
        };
        internal const int NN = 32;
        internal const int NG_ = 0, NH = 1, NMeat = 2, NFish = 3, NCheese = 4, NButter = 5, NEgg = 6, NApple = 7, NCarrot = 8, NOrange = 9, NGrape = 10, NOlives = 11,
                           NDate = 12, NOil = 13, NSalt = 14, NHoney = 15, NSpice = 16, NBeer = 17, NWine = 18, NMead = 19, NPlotno = 20, NSukno = 21, NSkora = 22,
                           NNarz = 23, NGarnki = 24, NSwiatlo = 25, NFutro = 26, NJedwab = 27, NKlejn = 28, NSwOlej = 29, NSwTran = 30, NDrewno = 31;
        internal static readonly Dictionary<string, int> NeedIndex = Index(NeedKeys);
        // owoce koszyka (kolejnosc FRUITS modelu = podzial _vs profilu krainy)
        internal static readonly int[] FruitNeed = { NApple, NCarrot, NOrange, NGrape, NOlives, NDate };
        internal static readonly int[] FruitGood = { GApple, GCarrot, GOrange, GGrape, GOlives, GDate };

        // ------------------------------------------------------------ stany i rece (LAMBDA ks_tabele.py)
        internal const int ESlave = 0, EPeasant = 1, ELabour = 2, ECraft = 3, EMerchant = 4, EGentry = 5, Est = 6;   // jak DailyLife (bez wielkich rodow)
        internal static readonly double[] Lambda = { 0.65, 0.50, 0.50, 0.50, 0.0, 0.0 };
        internal static readonly double[] TownFieldShare = { 0.5, 1.0, 0.2, 0.0, 0.0, 0.0 };   // pola mieszczan: chlopi 1.0, niewolni 0.5, wyrobnicy 0.2

        // ------------------------------------------------------------ warstwa dom, zboze wlasne, przetwory (ks_tabele.py)
        internal const double PH = 4.0, PHEgg = 0.25, PHHoney = 0.03, PGrainSub = 3.0, BeerGrain = 0.40, WineGrape = 1.30, OilOlive = 5.0, MeadHoney = 0.35;
        internal const double LinenFlax = 1.25, FeltWool = 1.45, LeatherHide = 2.4, ToolsIron = 1.2, PotClay = 1.2, JewelSilver = 25.0, VelvetSilk = 1.2;
        internal const double MeatOfLive = 0.50;
        internal static double HideOfLive(int g) { return g == GCow ? 0.08 : g == GSheep ? 0.10 : g == GHog ? 0.05 : 0.0; }
        internal const double HaHome = 0.3, HaOwnGrain = 4.0, HaPerCell = 807.0, FirewoodTown = 1.5;
        internal const double HorseVal = 120.0, HorseGameDay = 150.0;
        internal const double Garum = 1637.0 / 20.0 * 37.0 * 10.0;   // GARUM modelu (sigma 37 w liczbie - jak w ks_model2.py)
        internal static readonly HashSet<string> GarumLands = new HashSet<string>(StringComparer.Ordinal) { "lyseni", "volantine", "myrish", "tyroshi", "pentoshi" };

        // NONFOOD (ks_dane.py): plotno, sukno, skora, narzedzia, garnki, swiatlo, futro, jedwab, klejnoty (g) na osobe na rok wedlug stanu
        internal static readonly double[][] NonFood =
        {
            new[] { 0.8, 0.2, 0.2, 0.0, 0.3, 0.0, 0.0, 0.0, 0 },      // niewolni
            new[] { 1.2, 0.8, 0.5, 0.4, 1.0, 0.3, 0.02, 0.0, 0 },     // chlopi
            new[] { 1.0, 0.5, 0.4, 0.1, 0.8, 0.3, 0.0, 0.0, 0 },      // wyrobnicy
            new[] { 1.5, 1.2, 0.8, 1.0, 1.2, 0.8, 0.03, 0.001, 0.3 }, // rzemieslnicy
            new[] { 3.0, 4.0, 1.5, 0.3, 2.5, 3.0, 0.3, 0.05, 5 },     // kupcy
            new[] { 4.0, 6.0, 3.0, 0.5, 3.0, 5.0, 0.8, 0.10, 10 },    // szlachta
        };
        internal static readonly int[] NfNeed = { NPlotno, NSukno, NSkora, NNarz, NGarnki, NSwiatlo, NFutro, NJedwab, NKlejn };

        // ------------------------------------------------------------ zajecia: P0 v2 (kolejnosc kluczy P0 modelu)
        internal static readonly string[] Crafts =
        {
            "wheat_farm", "fisherman", "whaler", "walrus_hunter", "vineyard", "olive_trees", "citrus", "garden", "spice_garden", "quarry", "lime_pit", "date_farm",
            "flax_plant", "silk_plant", "sheep_farm", "cattle_farm", "cattle_farm:maslo", "swine_farm", "lumberjack", "trapper", "clay_mine", "salt_mine", "iron_mine",
            "silver_mine", "gold_mine", "europe_horse_ranch", "steppe_horse_ranch", "desert_horse_ranch", "battanian_horse_ranch", "sturgian_horse_ranch",
            "vlandian_horse_ranch", "sea_salt", "hunter"
        };
        internal static readonly int NC = 33;
        internal static readonly Dictionary<string, int> CraftIndex = Index(Crafts);
        internal static readonly int CWheat = 0, CFisher = 1, CWhaler = 2, CWalrus = 3, CVine = 4, COlive = 5, CCitrus = 6, CGarden = 7, CSpice = 8, CQuarry = 9,
                                     CLime = 10, CDate = 11, CFlax = 12, CSilk = 13, CSheep = 14, CCattle = 15, CCattleB = 16, CSwine = 17, CLumber = 18, CTrapper = 19,
                                     CClay = 20, CSalt = 21, CIron = 22, CSilver = 23, CGoldM = 24, CHorse0 = 25, CHorse5 = 30, CSeaSalt = 31, CHunter = 32;

        /// <summary>P0[c] = pary (towar, kg/prac./d) - v2 po recenzji (ryby 4, owce -20%, sad 4, futra 0.20, gaj 8, mysliwy).</summary>
        internal static readonly KeyValuePair<int, double>[][] P0 = BuildP0();

        private static KeyValuePair<int, double>[][] BuildP0()
        {
            var p = new KeyValuePair<int, double>[33][];
            Func<object[], KeyValuePair<int, double>[]> R = a =>
            {
                var r = new KeyValuePair<int, double>[a.Length / 2];
                for (int i = 0; i < r.Length; i++) r[i] = new KeyValuePair<int, double>(GoodIndex[(string)a[2 * i]], Convert.ToDouble(a[2 * i + 1]));
                return r;
            };
            p[CWheat] = R(new object[] { "grain", 3.0, "hog", 0.15, "sheep", 0.08, "cow", 0.10, "apple", 0.03, "carrot", 0.05, "Egg", 0.06, "honey", 0.02 });
            p[CFisher] = R(new object[] { "fish", 4.0 });
            p[CWhaler] = R(new object[] { "WhaleMeat", 3.0, "whale_oil", 0.6, "fish", 1.0 });
            p[CWalrus] = R(new object[] { "walrus_tusk", 0.1, "WhaleMeat", 1.0, "fish", 2.0 });
            p[CVine] = R(new object[] { "grape", 8.0 });
            p[COlive] = R(new object[] { "olives", 8.0, "orange", 0.3 });
            p[CCitrus] = R(new object[] { "orange", 4.0 });
            p[CGarden] = R(new object[] { "apple", 4.0, "carrot", 5.0 });
            p[CSpice] = R(new object[] { "spice", 1.0 });
            p[CQuarry] = R(new object[] { "marble", 150.0 });
            p[CLime] = R(new object[] { "limestone", 300.0 });
            p[CDate] = R(new object[] { "date_fruit", 8.0, "spice", 0.01 });
            p[CFlax] = R(new object[] { "flax", 1.5 });
            p[CSilk] = R(new object[] { "cotton", 0.025 });
            p[CSheep] = R(new object[] { "sheep", 2.4, "wool", 0.4, "cheese", 0.2, "butter", 0.064 });
            p[CCattle] = R(new object[] { "cow", 0.8, "cheese", 1.2, "butter", 0.32, "Egg", 0.1 });
            p[CCattleB] = R(new object[] { "cow", 0.8, "cheese", 0.4, "butter", 0.64, "Egg", 0.1 });
            p[CSwine] = R(new object[] { "hog", 5.0, "Egg", 0.1, "cheese", 0.05 });
            p[CLumber] = R(new object[] { "hardwood", 400.0, "honey", 0.10 });
            p[CTrapper] = R(new object[] { "fur", 0.20 });
            p[CClay] = R(new object[] { "clay", 800.0 });
            p[CSalt] = R(new object[] { "salt", 60.0 });
            p[CIron] = R(new object[] { "iron", 2.0 });
            p[CSilver] = R(new object[] { "silver", 15.0 });
            p[CGoldM] = R(new object[] { "gold_ore", 0.3 });
            for (int h = CHorse0; h <= CHorse5; h++) p[h] = R(new object[] { "horse", 0.01, "sheep", 0.6, "cheese", 0.1 });
            p[CSeaSalt] = R(new object[] { "salt", 50.0 });
            p[CHunter] = R(new object[] { "WhaleMeat", 1.2, "fur", 0.03 });
            return p;
        }

        /// <summary>K: d na pracujacego na dobe (ks_tabele2.py K_COST); brak = 0.</summary>
        internal static readonly double[] KCost = BuildK();
        private static double[] BuildK()
        {
            var k = new double[33];
            k[CWheat] = 0.10; k[CGarden] = 0.05; k[CVine] = 0.30; k[COlive] = 0.65; k[CCitrus] = 0.50; k[CFlax] = 0.10; k[CSilk] = 0.30; k[CSheep] = 1.70;
            k[CCattle] = 0.60; k[CCattleB] = 0.60; k[CSwine] = 0.65; k[CFisher] = 0.85; k[CWhaler] = 1.60; k[CWalrus] = 1.00; k[CTrapper] = 0.10;
            k[CSpice] = 0.30; k[CDate] = 0.60;
            for (int h = CHorse0; h <= CHorse5; h++) k[h] = 0.60;
            k[CHunter] = 0.10;
            return k;
        }

        /// <summary>ha na pracujacego (ks_tabele2.py HA_PER_WORKER); brak = 0 (las dziki, morze, zloza).</summary>
        internal static readonly double[] HaPerWorker = BuildHa();
        private static double[] BuildHa()
        {
            var a = new double[33];
            a[CWheat] = 4.0; a[CGarden] = 1.0; a[CVine] = 1.2; a[COlive] = 3.5; a[CCitrus] = 1.0; a[CFlax] = 1.4; a[CSilk] = 1.0; a[CSpice] = 1.0; a[CDate] = 0.7;
            a[CSheep] = 120.0; a[CCattle] = 18.0; a[CCattleB] = 18.0;
            for (int h = CHorse0; h <= CHorse5; h++) a[h] = 25.0;
            return a;
        }

        /// <summary>Regale (ks_tabele2.py REGALE, REG_GOOD): zloza, las na sprzedaz, oazy, ogrody przypraw - rece ida za cena (prawo pana).</summary>
        internal static readonly int[] Regale = { CSalt, CSeaSalt, CClay, CQuarry, CLime, CSilver, CGoldM, CLumber, CIron, CDate, CSpice };
        internal static readonly bool[] IsRegale = BuildRegMask();
        private static bool[] BuildRegMask() { var m = new bool[33]; foreach (var c in Regale) m[c] = true; return m; }
        internal static int RegGood(int c)
        {
            if (c == CSalt || c == CSeaSalt) return GSalt;
            if (c == CClay) return GClay;
            if (c == CQuarry) return GMarble;
            if (c == CLime) return GLime;
            if (c == CSilver) return GSilver;
            if (c == CGoldM) return GGold;
            if (c == CLumber) return GWood;
            if (c == CIron) return GIron;
            if (c == CDate) return GDate;
            if (c == CSpice) return GSpice;
            return -1;
        }
        /// <summary>Towary regale (REG_G = sorted(set(REG_GOOD.values()))) - ceny cienia stoja na 1.</summary>
        internal static readonly bool[] RegGoodMask = BuildRegGoods();
        private static bool[] BuildRegGoods() { var m = new bool[NG]; foreach (var c in Regale) m[RegGood(c)] = true; return m; }
        /// <summary>Kolejnosc towarow regale w kroku regale (sorted - alfabetycznie, jak Python sorted na napisach).</summary>
        internal static readonly int[] RegGoodsSorted = BuildRegSorted();
        private static int[] BuildRegSorted()
        {
            var l = new List<string>();
            foreach (var c in Regale) { string g = Goods[RegGood(c)]; if (!l.Contains(g)) l.Add(g); }
            l.Sort(StringComparer.Ordinal);
            var r = new int[l.Count];
            for (int i = 0; i < r.Length; i++) r[i] = GoodIndex[l[i]];
            return r;
        }

        // ------------------------------------------------------------ ziemia (ks_tabele.py ZW, TW, water_mult, land; ks_model2.py land2)
        internal sealed class Land
        {
            public string Zone = "3-umiark", Terr = "trawa", Kind = "village", Model = "village";
            public double DSea = 255, DRiver = 255, DMount = 255;
            public bool Main;
            public Land Clone() { return (Land)MemberwiseClone(); }
        }

        private static readonly string[] CropNames = { "wheat_farm", "flax_plant", "silk_plant", "vineyard", "olive_trees", "date_farm", "cattle_farm", "sheep_farm", "swine_farm", "lumberjack", "trapper" };
        private static readonly bool[] IsCrop = BuildCropMask();
        private static bool[] BuildCropMask() { var m = new bool[33]; foreach (var n in CropNames) m[CraftIndex[n]] = true; return m; }

        private static Dictionary<string, Dictionary<string, double>> T2(params object[] a)
        {
            var d = new Dictionary<string, Dictionary<string, double>>(StringComparer.Ordinal);
            for (int i = 0; i < a.Length; i += 2)
            {
                var row = new Dictionary<string, double>(StringComparer.Ordinal);
                var b = (object[])a[i + 1];
                for (int j = 0; j < b.Length; j += 2) row[(string)b[j]] = Convert.ToDouble(b[j + 1]);
                d[(string)a[i]] = row;
            }
            return d;
        }
        private static readonly Dictionary<string, Dictionary<string, double>> ZW = T2(
            "1-arktyka", new object[] { "wheat_farm", 0.25, "cattle_farm", 0.4, "sheep_farm", 0.6, "swine_farm", 0.3, "lumberjack", 1.0, "trapper", 1.0 },
            "2-wyspy", new object[] { "wheat_farm", 0.35, "flax_plant", 0.2, "cattle_farm", 0.5, "sheep_farm", 1.0, "swine_farm", 0.3, "lumberjack", 0.4, "trapper", 0.4 },
            "2-zimno", new object[] { "wheat_farm", 1.0, "flax_plant", 0.6, "cattle_farm", 0.8, "sheep_farm", 1.0, "swine_farm", 0.7, "lumberjack", 1.0, "trapper", 1.0 },
            "3-umiark", new object[] { "wheat_farm", 1.0, "flax_plant", 0.8, "vineyard", 0.35, "cattle_farm", 1.0, "sheep_farm", 0.9, "swine_farm", 0.8, "lumberjack", 1.0, "trapper", 0.8 },
            "4-cieplo", new object[] { "wheat_farm", 1.0, "flax_plant", 0.6, "silk_plant", 0.3, "vineyard", 1.0, "olive_trees", 0.3, "cattle_farm", 0.8, "sheep_farm", 0.7, "swine_farm", 0.6, "lumberjack", 0.8, "trapper", 0.4 },
            "5-srodz", new object[] { "wheat_farm", 1.0, "silk_plant", 0.7, "vineyard", 1.0, "olive_trees", 1.0, "date_farm", 0.4, "cattle_farm", 0.4, "sheep_farm", 0.8, "swine_farm", 0.3, "lumberjack", 0.4 },
            "6-goraco", new object[] { "wheat_farm", 0.6, "silk_plant", 1.0, "vineyard", 0.6, "olive_trees", 1.0, "date_farm", 1.0, "cattle_farm", 0.2, "sheep_farm", 0.7 },
            "7-tropik", new object[] { "wheat_farm", 0.4, "silk_plant", 1.0, "date_farm", 0.8 });
        private static readonly Dictionary<string, Dictionary<string, double>> TW = T2(
            "snieg", new object[] { "wheat_farm", 0, "flax_plant", 0, "silk_plant", 0, "vineyard", 0, "olive_trees", 0, "date_farm", 0, "cattle_farm", 0.3, "swine_farm", 0.3, "sheep_farm", 0.6, "trapper", 1.3 },
            "piasek", new object[] { "wheat_farm", 0.3, "flax_plant", 0, "vineyard", 0.3, "olive_trees", 1.0, "date_farm", 1.3, "cattle_farm", 0.2, "swine_farm", 0, "sheep_farm", 0.6, "lumberjack", 0, "trapper", 0 },
            "step", new object[] { "wheat_farm", 0.8, "flax_plant", 0.5, "sheep_farm", 1.3, "cattle_farm", 0.8, "swine_farm", 0.5, "lumberjack", 0, "trapper", 0 },
            "las", new object[] { "wheat_farm", 0.6, "lumberjack", 1.5, "trapper", 1.3, "swine_farm", 1.3, "vineyard", 0.5, "olive_trees", 0.5, "date_farm", 0, "silk_plant", 0.3 },
            "gory", new object[] { "wheat_farm", 0.5, "flax_plant", 0.4, "silk_plant", 0.3, "sheep_farm", 1.3, "vineyard", 1.2, "olive_trees", 0.9, "date_farm", 0.3, "cattle_farm", 0.7 },
            "bagno", new object[] { "wheat_farm", 0.5, "vineyard", 0, "olive_trees", 0, "sheep_farm", 0.5 });
        private static readonly Dictionary<string, double> HuntZone = new Dictionary<string, double>(StringComparer.Ordinal)
        { { "1-arktyka", 1.0 }, { "2-zimno", 0.8 }, { "2-wyspy", 0.3 }, { "3-umiark", 0.4 }, { "4-cieplo", 0.3 }, { "5-srodz", 0.2 }, { "6-goraco", 0.2 }, { "7-tropik", 0.4 } };
        private static readonly Dictionary<string, double> HuntTerr = new Dictionary<string, double>(StringComparer.Ordinal)
        { { "las", 1.2 }, { "snieg", 0.8 }, { "gory", 0.7 }, { "step", 0.6 }, { "bagno", 0.6 }, { "trawa", 0.3 }, { "piasek", 0.1 } };

        private static double Get(Dictionary<string, Dictionary<string, double>> t, string a, string b, double def)
        {
            Dictionary<string, double> r; double v;
            if (a == null || !t.TryGetValue(a, out r)) return def;
            return b != null && r.TryGetValue(b, out v) ? v : def;
        }

        internal static bool Coast(Land o) { return o.Kind == "coast" || o.DSea <= 3 || o.Model == "fishing"; }
        internal static bool River(Land o) { return o.Kind == "river" || o.Kind == "bridge" || o.Kind == "ford" || o.DRiver <= 3; }
        private static bool HotZone(string z) { return z == "5-srodz" || z == "6-goraco" || z == "7-tropik"; }

        private static double WaterMult(string t, Land o)
        {
            double m = 1.0;
            if (River(o))
            {
                if (t == "flax_plant") m *= 1.3; else if (t == "silk_plant") m *= 1.5; else if (t == "cattle_farm") m *= 1.2; else if (t == "wheat_farm") m *= 1.1; else if (t == "date_farm") m *= 1.3;
                if (t == "wheat_farm" && HotZone(o.Zone)) m *= 1.5;
            }
            else
            {
                if ((t == "silk_plant" || t == "date_farm") && (o.Zone == "5-srodz" || o.Zone == "6-goraco")) m *= 0.4;
                if (t == "wheat_farm" && o.Terr == "piasek") m *= 0.3;
            }
            if (Coast(o) && t == "olive_trees") m *= 1.2;
            return m;
        }

        private static bool IsMine(int c) { return c == CIron || c == CSilver || c == CSalt || c == CClay; }
        private static bool IsFish(int c) { return c == CFisher || c == CWhaler || c == CWalrus; }
        private static bool IsMineType(string t) { return t == "iron_mine" || t == "silver_mine" || t == "clay_mine"; }

        /// <summary>Przydatnosc (= czynnik wydajnosci) zajecia c na ziemi o w okregu typu dtype (ks_tabele.land). 0 = nie da sie.</summary>
        private static double LandRaw(int c, Land o, string dtype)
        {
            string t = Crafts[c];
            if (c == CCattleB) t = "cattle_farm";
            int ct = CraftIndex[t];
            if (IsCrop[ct])
            {
                double w = Get(ZW, o.Zone, t, 0.0) * Get(TW, o.Terr, t, 1.0) * WaterMult(t, o);
                if (o.Model == "fishing" && (t == "wheat_farm" || t == "flax_plant" || t == "silk_plant" || t == "vineyard" || t == "olive_trees" || t == "date_farm" || t == "cattle_farm" || t == "swine_farm"))
                    w *= 0.5;
                return Math.Min(1.5, w);
            }
            if (IsFish(c))
            {
                if (c != CFisher && dtype != t) return 0.0;
                double b = Coast(o) ? 1.0 : (River(o) ? 0.4 : 0.0);
                if (o.Zone == "2-wyspy" && b > 0) b *= 1.2;
                return b;
            }
            if (IsMine(c))
            {
                if (dtype != t) return 0.0;
                if (c == CIron || c == CSilver) return (o.Terr == "gory" || o.DMount <= 2 || o.Main) ? 1.0 : 0.0;
                return 1.0;
            }
            if (c == CGarden)
            {
                double zf = o.Zone == "1-arktyka" ? 0.0 : o.Zone == "2-zimno" ? 0.8 : o.Zone == "3-umiark" ? 1.0 : o.Zone == "4-cieplo" ? 1.0 : o.Zone == "5-srodz" ? 0.6
                          : o.Zone == "6-goraco" ? 0.3 : o.Zone == "7-tropik" ? 0.2 : 0.0;
                double tf = o.Terr == "snieg" ? 0 : o.Terr == "piasek" ? 0.2 : o.Terr == "step" ? 0.6 : o.Terr == "las" ? 0.7 : o.Terr == "gory" ? 0.6 : o.Terr == "bagno" ? 0.5 : 1.0;
                return Math.Min(1.5, zf * tf * (River(o) ? 1.1 : 1.0) * (o.Model == "fishing" ? 0.5 : 1.0));
            }
            if (c == CCitrus)
            {
                if (o.Model == "fishing") return 0.0;
                double zf = o.Zone == "5-srodz" ? 0.8 : (o.Zone == "6-goraco" || o.Zone == "7-tropik") ? 1.0 : 0.0;
                double tf = o.Terr == "snieg" ? 0 : o.Terr == "las" ? 0.5 : o.Terr == "bagno" ? 0 : 1.0;
                return Math.Min(1.5, zf * tf * ((River(o) || Coast(o)) ? 1.0 : 0.4));
            }
            if (c == CSpice)
            {
                double zf = o.Zone == "7-tropik" ? 1.0 : o.Zone == "6-goraco" ? 0.6 : o.Zone == "5-srodz" ? 0.3 : 0.0;
                return zf * ((River(o) || Coast(o) || o.Zone == "7-tropik") ? 1.0 : 0.3) * (o.Terr == "snieg" ? 0 : 1.0);
            }
            if (c == CGoldM) return dtype == "silver_mine" && (o.Terr == "gory" || o.DMount <= 2 || o.Main || River(o)) ? 1.0 : 0.0;
            if (c == CLime) return IsMineType(dtype) || o.Terr == "gory" ? 1.0 : 0.0;
            if (c == CQuarry) return IsMineType(dtype) && (o.Terr == "gory" || o.DMount <= 1 || o.Main) ? 1.0 : 0.0;
            if (c == CSeaSalt) return Coast(o) && (o.Zone == "5-srodz" || o.Zone == "6-goraco") && (o.Terr == "piasek" || o.Terr == "step" || o.Terr == "trawa") ? 0.8 : 0.0;
            if (c >= CHorse0 && c <= CHorse5)
                return (dtype == t && (o.Terr == "trawa" || o.Terr == "step" || o.Terr == "las" || o.Terr == "gory")) || (dtype == t && o.Main) ? 1.0 : 0.0;
            return 0.0;
        }

        private static readonly HashSet<string> FieldSet = new HashSet<string>(StringComparer.Ordinal)
        { "wheat_farm", "flax_plant", "silk_plant", "cattle_farm", "cattle_farm:maslo", "garden", "citrus", "vineyard", "swine_farm" };

        /// <summary>land2 modelu v2: mysliwy (strefa x teren), dolina rzeki w goracu (piasek przy rzece = trawa dla zajec pola), reszta - land.</summary>
        internal static double Land2(int c, Land o, string dtype)
        {
            if (c == CHunter)
            {
                double z, t;
                if (o.Zone == null || !HuntZone.TryGetValue(o.Zone, out z)) z = 0.3;
                if (o.Terr == null || !HuntTerr.TryGetValue(o.Terr, out t)) t = 0.3;
                return Math.Min(1.5, z * t);
            }
            if (HotZone(o.Zone) && o.Terr == "piasek" && River(o) && FieldSet.Contains(Crafts[c]))
            {
                var o2 = o.Clone(); o2.Terr = "trawa";
                return LandRaw(c, o2, dtype);
            }
            return LandRaw(c, o, dtype);
        }

        internal static double OwnGrainShare(double lg) { return Math.Min(1.0, Math.Max(0.0, (lg - 0.2) / 0.4)); }

        private static Dictionary<string, int> Index(string[] a)
        {
            var d = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < a.Length; i++) d[a[i]] = i;
            return d;
        }
    }
}
