using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 435-L3 - LANCUCH PODAZY (LP), krok 3: SPRZET PODSTAWOWY WSI. Projekt PROJEKT-LANCUCH-po-krytyce (11.11) rozdz. II.1-II.5, II.11, II.12, wiersz L3. Polecenie Jeffa:
    /// "inwestycja + ludzie daje 100% efektu, sami ludzie bez np. narzedzi pracuja mniej wydajnie, a niektore galezie wrecz wymagaja inwestycji, np. kopalnie".
    /// Klucz Village Gear (NIE = m = 1 wszedzie, stan w zapisie zamrozony, kod co do bitu jak przed 435-L3).
    ///
    /// REGULA: produkcja zajecia czesci okregu = dotychczasowa x m x (1 + b); b - dodatek prac okregu (DistrictWorks, bez zmian),
    ///   m = phi + (1 - phi) x min(1, K / H) - nigdy ponad 1 (krytyka B4: nadmiar sprzetu jest bezczynny, nie daje premii za odejscie ludzi);
    ///   H - ludzie czesci w domu (ksiega wiosek), srednia ruchoma 7 dob (tabory i pobor nie szarpia m);
    ///   K - sprzet czesci w jednostkach "wyposazony czlowiek w domu" (ta sama miara co H; rece zajec to staly ulamek ludzi);
    ///   phi - wydajnosc reki bez sprzetu wedlug rodzaju zajecia (tabela nizej; stado i stadniny poza LP: m = 1 - ich kapitalem jest stado).
    ///   Stosunek K / H jest jeden na czesc (kwantowany co 0.01; od 0.98 w gore liczony jak 1 - pas martwy na tabory, pobor i podzial ludzi miedzy czesci); m liczy sie na zajecie czesci (blok KS ma mieszanke zajec, wioska w przebudowie dwa).
    /// BILANS OTWARCIA (jedyny sprzet z niczego, jawny - linia i znacznik w zapisie): czesc bez wpisu dostaje K0 = (ludzie w domu + poza domem) x (1 - L x osady puste
    ///   z rabunku / osady); przez 7 dob od wpisu K0 jest domykane do tej samej liczby (wozy i pobor w drodze w dobie wpisu), o ile nie przybylo osad pustych z rabunku.
    ///   Skutek: m = 1 wszedzie tam, gdzie ludzie sa w domu - produkcja swiata w dobie wlaczenia jak dzien wczesniej.
    /// SKAD SPRZET: z PRAWDZIWEGO materialu robot gminy (TownFolk.DaRunOne - zlecenie z kroku 5 doby wsi, ta sama kolejka, te same pieniadze i te same sztuki z polki
    ///   miasta targowego co dotad): material zaplacony przez wies (polka targu + wytworcy 363 bez frachtu) jest najpierw sprzetem dla rak bez sprzetu (priorytet a),
    ///   dK = material / (KM x D x wartosc dnia czlowieka), reszta - roboty gminy bez skutku jak dotad. Zloto i sztuki plyna DOKLADNIE jak przed zmiana (ksiegi robot gminy
    ///   bez zmian); zmienia sie tylko kolejnosc zlecen (wsie z rekami bez sprzetu pierwsze do polki) i to, ze material ma skutek. Brak materialu = brak jednostek.
    /// OGIEN: ta sama regula co prace okregu (DistrictWorks.Diff) - osady oproznione rabunkiem zabieraja L x K x (nowe puste / stojace przed); reszta sprzetu zostaje
    ///   bezczynna i czeka na ludzi. Glod niczego nie niszczy.
    /// BEZ ZUZYCIA (L5), bez zmian w przebudowie zajec (L4: sprzet idzie za czescia, nie za zajeciem), bez fundatorow (L7). Pola zapisu "zaliczka", "stare zajecie",
    ///   "K stare" i doba zuzycia sa w formacie lp1 od razu (puste) - nastepne kroki nie zmieniaja formatu.
    /// DROGA PRODUKCJI: pod KS (okreg zywy z pelna stawka) - StartCalibration.GearKs w rachunku okregu; bez KS - plan zajec VillageCrafts (BuildPlan). Okreg bez drogi
    ///   (rampa KS, zajecia wiosek nieczynne) - m = 1 i bez potrzeby sprzetu (sprzet zamrozony, jak prace okregu bez drogi).
    /// Zapis arm_lp ("lp1|doba wlaczenia|doba zuzycia|czesci..."; zly wpis pominiety i policzony; brak klucza = bilans otwarcia od nowa; starsza DLL ignoruje klucz).
    /// Watek glowny (stan pod Gate); produkcja czyta jedna podmieniana referencje. Wyjatki per okreg liczone (potkniecia), bez globalnego wylacznika.
    /// </summary>
    internal static class VillageGear
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const string P = "Sprzet wsi (LP, 435-L3): ";
        private const string SaveVer = "lp1";
        private const int OpenDays = 7;                   // domykanie bilansu otwarcia czesci (wozy i pobor w drodze)
        private const double HsDays = 7.0;                // srednia ruchoma rak
        private const double Tolerance = 0.98;            // stosunek K / H od tej liczby w gore liczony jak 1 (pas martwy 2% rak)
        private static readonly object Gate = new object();

        // ============================================================ tabela rodzajow (projekt II.2; liczby startowe do kalibracji - decyzja Jeffa 3)
        internal const int GPits = 0, GTrees = 1, GBoats = 2, GWoodD = 3, GWoodM = 4, GGrain = 5, GFields = 6, GOut = 7, GN = 8;
        internal static readonly string[] GName = { "SZYBY", "SADY", "LODZIE", "LASY-D", "LASY-M", "POLA-Z", "POLA", "poza LP (stado, stadniny)" };
        internal static readonly double[] GPhi = { 0.15, 0.20, 0.25, 0.50, 0.70, 0.70, 0.60, 1.0 };     // wydajnosc reki bez sprzetu
        internal static readonly double[] GDays = { 90, 60, 45, 14, 14, 14, 14, 0 };                   // cena jednostki w dniach wartosci produkcji czlowieka
        internal static readonly double[] GWear = { 0.12, 0.03, 0.10, 0.12, 0.08, 0.06, 0.06, 0 };     // zuzycie na rok (L5 - tu nieuzywane)

        internal static int GroupOf(string craft)
        {
            switch (craft)
            {
                case "iron_mine": case "silver_mine": case "gold_mine": case "salt_mine": case "clay_mine": case "quarry": case "lime_pit": case "sea_salt": return GPits;
                case "vineyard": case "olive_trees": case "date_farm": case "citrus": return GTrees;
                case "fisherman": case "whaler": case "walrus_hunter": return GBoats;
                case "lumberjack": return GWoodD;
                case "trapper": case "hunter": return GWoodM;
                case "wheat_farm": return GGrain;
                case "flax_plant": case "silk_plant": case "garden": case "spice_garden": return GFields;
                default: return GOut;
            }
        }

        private static int[] _gOf;
        /// <summary>Rodzaj LP zajecia (indeks KsTab); GOut - poza LP albo spoza tabeli.</summary>
        internal static int GroupOfCraft(int c)
        {
            var g = _gOf;
            if (g == null)
            {
                g = new int[KsTab.Crafts.Length];
                for (int i = 0; i < g.Length; i++) g[i] = DistrictWorks.KindOfCraft(KsTab.Crafts[i]) < 0 ? GOut : GroupOf(KsTab.Crafts[i]);
                _gOf = g;
            }
            return c >= 0 && c < g.Length ? g[c] : GOut;
        }

        internal static bool On { get { var s = Settings.Current; return s != null && s.VillageGear; } }

        // ============================================================ stan (zapis arm_lp)
        private sealed class Rec
        {
            public string Dist, Part; public int Craft = -1, OldCraft = -1, Day0 = -1; public double K = -1, Adv, Hs, OldK, SeenBr = -1, SeenBf = -1, Br0;
            // pamiec sesji (wycena co 7 dob, stan doby)
            public int ValDay = int.MinValue; public double Unit, UnitMat, L, WLp, Ratio = 1, Need; public bool Path;
        }
        private static readonly Dictionary<string, List<Rec>> _byDist = new Dictionary<string, List<Rec>>(StringComparer.Ordinal);
        private static int _onDay = -1, _wearDay = -1;
        private static string _loadNote = "nowa kampania albo zapis bez arm_lp (bilans otwarcia w pierwszej dobie klucza)";
        private static int _loadBad;

        // opublikowane dla produkcji: wies gry -> czesc -> stosunek K / H (tylko czesci ponizej 1). Jedna referencja podmieniana (odczyt bez zamka).
        private static volatile Dictionary<Village, Dictionary<string, float>> _pub = new Dictionary<Village, Dictionary<string, float>>();

        // sesja
        private static int _main = -1, _preDay = int.MinValue, _st, _stAll;
        private static bool _valuedAll, _startLine;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static double _prevSum = double.NaN; private static int _prevSumDay = int.MinValue;
        // liczniki od ostatniej linii
        private static double _dBuyU, _dBuyGold, _dMatGold, _dOpenU, _dOpenVal, _dBurnU, _dKsKg;
        private static int _dBuyN, _dMatN, _dOpenN, _dBurnN, _dFirst, _dNoPrice, _dChanged, _dValued, _dKsN, _dOff;
        private static readonly List<VillageBook.PartView> _book = new List<VillageBook.PartView>();
        private static readonly List<VillageCrafts.PartCraft> _parts = new List<VillageCrafts.PartCraft>();

        /// <summary>Nowa kampania / przed wczytaniem: stan i liczniki od zera (ArmouryBehavior - konstruktor).</summary>
        internal static void Reset()
        {
            lock (Gate)
            {
                _byDist.Clear(); _onDay = -1; _wearDay = -1; _loadBad = 0;
                _loadNote = "nowa kampania albo zapis bez arm_lp (bilans otwarcia w pierwszej dobie klucza)";
                _pub = new Dictionary<Village, Dictionary<string, float>>();
                _main = Environment.CurrentManagedThreadId; _preDay = int.MinValue; _st = _stAll = 0; _valuedAll = false; _startLine = false; _err.Clear();
                _prevSum = double.NaN; _prevSumDay = int.MinValue; _gOf = null;
                ZeroDay();
            }
        }

        private static void ZeroDay()
        {
            _dBuyU = _dBuyGold = _dMatGold = _dOpenU = _dOpenVal = _dBurnU = _dKsKg = 0;
            _dBuyN = _dMatN = _dOpenN = _dBurnN = _dFirst = _dNoPrice = _dChanged = _dValued = _dKsN = _dOff = 0; _st = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            bool first;
            lock (Gate) { _st++; _stAll++; first = _err.Add(where); }
            if (first) { try { Log.Error("VillageGear." + where, e); } catch { } }
        }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }
        private static string F1(double v) { return v.ToString("0.0", Inv); }
        private static string F2(double v) { return v.ToString("0.00", Inv); }
        private static string N0(double v) { return Math.Round(v).ToString("0", Inv); }
        private static string D(double v) { return v.ToString("0.####", Inv); }
        private static bool Num(string s, out double v) { return double.TryParse(s, NumberStyles.Float, Inv, out v) && !double.IsNaN(v) && !double.IsInfinity(v); }

        // ============================================================ odczyty dla produkcji (dowolny watek - jedna referencja)
        /// <summary>Okreg ma czesc z niedoborem sprzetu (opublikowana). Sam odczyt.</summary>
        internal static bool Any(Village v)
        {
            if (v == null) return false;
            var m = _pub; return m.Count > 0 && m.ContainsKey(v) && On;
        }

        /// <summary>Stosunek K / H czesci (wioska po uid, wies gry - null); 1 - bez niedoboru, klucz wylaczony albo okreg bez drogi produkcji. Sam odczyt.</summary>
        internal static double RatioOf(Village v, string uid)
        {
            if (v == null) return 1.0;
            var m = _pub; Dictionary<string, float> d; float k;
            if (m.Count == 0 || !m.TryGetValue(v, out d) || d == null || !d.TryGetValue(DistrictWorks.PartKey(uid), out k) || !On) return 1.0;
            return k >= 0f && k < 1f ? k : 1.0;
        }

        /// <summary>Czynnik sprzetu zajecia przy stosunku K / H czesci: phi + (1 - phi) x stosunek; zajecie poza LP - 1.</summary>
        internal static double MAt(int craft, double ratio)
        {
            if (!(ratio < 1.0)) return 1.0;
            int g = GroupOfCraft(craft);
            if (g == GOut) return 1.0;
            return GPhi[g] + (1.0 - GPhi[g]) * Math.Max(0.0, ratio);
        }

        /// <summary>Czynnik sprzetu m zajecia `craft` czesci okregu (1 - bez niedoboru). Sam odczyt, nie rzuca.</summary>
        internal static double MOf(Village v, string uid, int craft)
        {
            try { double r = RatioOf(v, uid); return r < 1.0 ? MAt(craft, r) : 1.0; }
            catch { return 1.0; }
        }

        /// <summary>StartCalibration.GearKs: kg/d zdjete z produkcji okregu pod KS czynnikiem sprzetu (sam licznik linii).</summary>
        internal static void NoteKs(double kg) { if (kg > 0) lock (Gate) { _dKsKg += kg; _dKsN++; } }

        /// <summary>Droga produkcji czynnika sprzetu: pod KS okreg zywy z pelna stawka (GearKs w rachunku okregu), bez KS plan zajec wiosek (VillageCrafts.BuildPlan).</summary>
        private static bool PathOf(Village v)
        {
            try
            {
                if (StartCalibration.KsDistrictLive(v)) return StartCalibration.FullFor(v);
                return VillageCrafts.On && !StartCalibration.AppliedFor(v);
            }
            catch { return false; }
        }

        // ============================================================ wycena czesci (co 7 dob na zmiane; nowa czesc i pierwsza doba sesji - od razu)
        /// <summary>Cena jednostki sprzetu czesci: D x wartosc dnia czlowieka (zajecia LP czesci, cena miasta targowego, bez zimy i bez czynnika sprzetu), czesc materialowa
        /// KM (DistrictWorks.KM), strata ognia L (DistrictWorks.KL) - wagi wedlug wartosci dnia zajec. Czesc bez ludzi albo bez wartosci - poprzednia wycena zostaje.</summary>
        private static void Value(Village v, List<Rec> list, int today)
        {
            bool ks;
            if (!VillageCrafts.PartsOf(v, _parts, out ks)) return;
            double winter = 0;
            try { winter = WinterBite.VillageCut(v); } catch { winter = 0; }
            if (!(winter >= 0)) winter = 0; if (winter > 0.95) winter = 0.95;
            foreach (var pc in _parts)
            {
                if (pc == null) continue;
                Rec rec = Find(list, DistrictWorks.PartKey(pc.Uid));
                if (rec == null) continue;
                string uid = pc.Main ? null : pc.Uid;
                double all = 0, lp = 0, price = 0, mat = 0, lw = 0;
                for (int c = 0; c < pc.CraftVal.Length && c < KsTab.Crafts.Length; c++)
                {
                    double vc = pc.CraftVal[c];
                    if (!(vc > 0)) continue;
                    int rg = KsTab.RegGood(c);
                    double wq = rg >= 0 && WinterBite.SparesOn && WinterBite.SparedId(KsTab.Goods[rg]) ? 0.0 : winter;
                    double v0 = vc / Math.Max(0.05, MOf(v, uid, c)) / Math.Max(0.05, 1.0 - wq);
                    all += v0;
                    int g = GroupOfCraft(c), kind = DistrictWorks.KindOfCraft(KsTab.Crafts[c]);
                    if (g == GOut || kind < 0) continue;
                    lp += v0; price += GDays[g] * v0; mat += DistrictWorks.KM[kind] * GDays[g] * v0; lw += DistrictWorks.KL[kind] * v0;
                }
                rec.Craft = pc.Craft; rec.ValDay = today;
                if (all > 0 && !double.IsNaN(all) && !double.IsInfinity(all))
                {
                    rec.WLp = lp / all;
                    if (lp > 0) rec.L = lw / lp;
                    double H = pc.Book != null ? pc.Book.Home : 0;
                    if (H >= 1 && lp > 0) { rec.Unit = price / H; rec.UnitMat = mat / H; }
                }
                else
                {
                    // bez wartosci dnia (czesc pusta, zima bez plonu): rodzaj z zajecia czesci, cena z poprzedniej wyceny
                    int g = GroupOfCraft(pc.Craft), kind = pc.Craft >= 0 && pc.Craft < KsTab.Crafts.Length ? DistrictWorks.KindOfCraft(KsTab.Crafts[pc.Craft]) : -1;
                    rec.WLp = g == GOut || kind < 0 ? 0.0 : 1.0;
                    if (kind >= 0) rec.L = DistrictWorks.KL[kind];
                }
            }
        }

        private static Rec Find(List<Rec> list, string part)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].Part == part) return list[i];
            return null;
        }

        // ============================================================ doba (przed produkcja)
        /// <summary>ArmouryBehavior.OnDailyTick zaraz po DistrictWorks.BeforeProduction (po VillageBook.Daily, przed StartCalibration.Daily i VillageCrafts.Daily): bilans
        /// otwarcia nowych czesci, ogien, srednia rak, stosunek K / H i potrzeba sprzetu kazdej czesci; okregi ze zmienionym stosunkiem oznaczone do przeliczenia
        /// (KS, plan zajec, wycena prac - jak zmiana b). Linia doby. Nie rzuca.</summary>
        internal static void BeforeProduction()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            int today = Today();
            if (_preDay == today) return;
            _preDay = today;
            if (_main >= 0 && Environment.CurrentManagedThreadId != _main) { _dOff++; return; }
            if (!s.VillageGear || !VillageBook.Active) { Unpublish(); return; }
            long t0 = Stopwatch.GetTimestamp();
            int parts = 0, low = 0, dist = 0, noPath = 0;
            double sumHs = 0, sumNeed = 0, idle = 0, value = 0, minR = 1, wM = 0, wH = 0;
            var gH = new double[GN]; var gNeed = new double[GN];
            var pub = new Dictionary<Village, Dictionary<string, float>>();
            try
            {
                lock (Gate)
                {
                    bool first = !_valuedAll; _valuedAll = true;
                    if (_onDay < 0) _onDay = today;
                    foreach (var v in Village.All)
                    {
                        try
                        {
                            if (v == null || v.Settlement == null || v.Settlement.StringId == null) continue;
                            if (!VillageBook.PartsOf(v, _book) || _book.Count < 1) continue;
                            string id = v.Settlement.StringId;
                            List<Rec> list;
                            if (!_byDist.TryGetValue(id, out list)) { list = new List<Rec>(); _byDist[id] = list; }
                            bool val = first || (today + (int)((uint)id.GetHashCode() % 7u)) % 7 == 0;
                            foreach (var p in _book)
                            {
                                string part = DistrictWorks.PartKey(p.Uid);
                                Rec r0 = Find(list, part);
                                if (r0 == null) { list.Add(new Rec { Dist = id, Part = part, Day0 = today }); val = true; }
                                else if (r0.ValDay == int.MinValue) val = true;
                            }
                            if (val) { Value(v, list, today); _dValued++; }
                            bool path = PathOf(v);
                            dist++; if (!path) noPath++;
                            Dictionary<string, float> d = null;
                            foreach (var p in _book)
                            {
                                Rec rec = Find(list, DistrictWorks.PartKey(p.Uid));
                                if (rec == null) continue;
                                double brShare = p.Nw > 1e-9 ? Math.Min(1.0, p.Br / p.Nw) : 0.0;
                                double target = Math.Max(0.0, (p.Home + p.Away) * (1.0 - Math.Max(0.0, Math.Min(1.0, rec.L)) * brShare));
                                if (rec.K < 0)
                                {
                                    // bilans otwarcia czesci
                                    rec.K = target; rec.Hs = p.Home; rec.SeenBr = p.Br; rec.SeenBf = p.Bf; rec.Br0 = p.Br; rec.Day0 = today;
                                    _dOpenU += target; _dOpenN++; _dOpenVal += target * rec.Unit;
                                }
                                else
                                {
                                    if (rec.SeenBr >= 0)
                                    {
                                        // ogien: osady oproznione rabunkiem od ostatniego odczytu (ta sama regula co prace okregu)
                                        double sBefore = Math.Max(0.0, p.Nw - rec.SeenBr - Math.Max(0.0, rec.SeenBf)), dR = p.Br - rec.SeenBr;
                                        if (dR > 1e-6 && sBefore > 1e-6 && rec.K > 0)
                                        {
                                            double lost = Math.Max(0.0, Math.Min(1.0, rec.L)) * rec.K * Math.Min(1.0, dR / sBefore);
                                            if (lost > 0) { rec.K -= lost; _dBurnU += lost; _dBurnN++; }
                                        }
                                    }
                                    else rec.Br0 = p.Br;
                                    rec.SeenBr = p.Br; rec.SeenBf = p.Bf;
                                    if (today - rec.Day0 < OpenDays && p.Br <= rec.Br0 + 1e-6 && target > rec.K + 1e-9)
                                    {
                                        // domkniecie bilansu otwarcia (ludzie z wozow i poboru wrocili do domu)
                                        _dOpenU += target - rec.K; _dOpenVal += (target - rec.K) * rec.Unit; rec.K = target;
                                    }
                                    rec.Hs += (p.Home - rec.Hs) / HsDays;
                                    if (!(rec.Hs >= 0)) rec.Hs = Math.Max(0.0, p.Home);
                                }
                                rec.Path = path;
                                bool act = path && rec.WLp > 1e-9;
                                double ratio = rec.Hs >= 1.0 ? Math.Min(1.0, rec.K / rec.Hs) : 1.0;
                                // pas martwy: do 2% rak bez sprzetu nie liczy sie (tabory, pobor, podzial ludzi okregu miedzy czesci wedlug osad - bez migotania m i przeliczen
                                // okregu co dobe); ponizej - stosunek co 0.01, a zakup uzupelnia do pelna (histereza)
                                ratio = ratio >= Tolerance ? 1.0 : Math.Round(ratio * 100.0) / 100.0;
                                rec.Ratio = act ? ratio : 1.0;
                                rec.Need = act && ratio < 1.0 ? Math.Max(0.0, rec.Hs - rec.K) : 0.0;
                                if (rec.Ratio < 1.0) { if (d == null) d = new Dictionary<string, float>(StringComparer.Ordinal); d[rec.Part] = (float)rec.Ratio; low++; if (rec.Ratio < minR) minR = rec.Ratio; }
                                parts++; sumHs += rec.Hs; sumNeed += rec.Need; idle += Math.Max(0.0, rec.K - rec.Hs); value += rec.K * rec.Unit;
                                int g = GroupOfCraft(rec.Craft);
                                gH[g] += rec.Hs; gNeed[g] += rec.Need;
                                wH += rec.Hs; wM += rec.Hs * MAt(rec.Craft, rec.Ratio);
                            }
                            if (d != null) pub[v] = d;
                        }
                        catch (Exception e) { Stumble("BeforeProduction(okreg)", e); }
                    }
                }
                // publikacja: okregi ze zmienionym stosunkiem - rachunek KS, plan zajec i wycena prac tej doby od nowa (jak zmiana b prac okregu)
                var old = _pub;
                _pub = pub;
                foreach (var kv in pub) if (Changed(old, kv.Key, kv.Value)) { _dChanged++; DistrictWorks.NoteGearChanged(kv.Key); }
                foreach (var kv in old) if (!pub.ContainsKey(kv.Key)) { _dChanged++; DistrictWorks.NoteGearChanged(kv.Key); }
            }
            catch (Exception e) { Stumble("BeforeProduction", e); }
            try { Line(today, s, dist, noPath, parts, low, sumHs, sumNeed, idle, value, minR, wH > 0 ? wM / wH : 1.0, gH, gNeed, (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency); }
            catch (Exception e) { Stumble("Line", e); }
        }

        private static bool Changed(Dictionary<Village, Dictionary<string, float>> old, Village v, Dictionary<string, float> now)
        {
            Dictionary<string, float> was;
            if (!old.TryGetValue(v, out was) || was == null || was.Count != now.Count) return true;
            foreach (var kv in now) { float x; if (!was.TryGetValue(kv.Key, out x) || Math.Abs(x - kv.Value) > 1e-4f) return true; }
            return false;
        }

        /// <summary>Klucz wylaczony (albo ksiega wiosek nieczynna): czynnik zdjety - okregi, ktore go mialy, do przeliczenia; stan zamrozony.</summary>
        private static void Unpublish()
        {
            try
            {
                var old = _pub;
                if (old.Count == 0) return;
                _pub = new Dictionary<Village, Dictionary<string, float>>();
                foreach (var v in old.Keys) DistrictWorks.NoteGearChanged(v);
                _prevSum = double.NaN; _prevSumDay = int.MinValue;
                Log.Info(P + "klucz Village Gear wylaczony (albo ksiega wiosek nieczynna) - czynnik sprzetu zdjety z " + old.Count + " okregow (m = 1), stan w zapisie zamrozony.");
            }
            catch (Exception e) { Stumble("Unpublish", e); }
        }

        // ============================================================ zakup: material robot gminy (TownFolk.DaRunOne)
        /// <summary>Potrzeba sprzetu okregu w zlocie materialu (rece bez sprzetu x material jednostki) z ostatniej doby; 0 - brak potrzeby albo klucz wylaczony.</summary>
        internal static double NeedGold(Village v)
        {
            if (v == null || v.Settlement == null || !On) return 0.0;
            try
            {
                lock (Gate)
                {
                    List<Rec> list;
                    if (!_byDist.TryGetValue(v.Settlement.StringId, out list)) return 0.0;
                    double g = 0;
                    foreach (var r in list) if (r.Need > 0 && r.UnitMat > 1e-6) g += r.Need * r.UnitMat;
                    return g;
                }
            }
            catch { return 0.0; }
        }

        /// <summary>TownFolk.DaWorksRunCore: zlecenia robot wsi z rekami bez sprzetu postawione przed pozostalymi (sam licznik).</summary>
        internal static void NoteFirst(int n) { if (n > 0) lock (Gate) _dFirst += n; }

        /// <summary>TownFolk.DaRunOne (roboty gminy wsi, nie septy), po zaplacie: `matGold` - PRAWDZIWY material zaplacony przez wies (polka targu + wytworcy 363 bez
        /// frachtu). Najpierw sprzet dla rak bez sprzetu czesci okregu (wedlug potrzeby w zlocie materialu), reszta - roboty gminy jak dotad (bez skutku). Zadnego zlota:
        /// zaplata juz przeszla dotychczasowa droga. Zwraca zloto materialu zaliczone na sprzet. Watek glowny, nie rzuca.</summary>
        internal static long OnWorksMaterial(Village v, long matGold)
        {
            if (matGold <= 0 || v == null || v.Settlement == null || !On) return 0;
            try
            {
                if (_main >= 0 && Environment.CurrentManagedThreadId != _main) { _dOff++; return 0; }
                lock (Gate)
                {
                    _dMatGold += matGold; _dMatN++;
                    List<Rec> list;
                    if (!_byDist.TryGetValue(v.Settlement.StringId, out list)) return 0;
                    double total = 0; bool noPrice = false;
                    foreach (var r in list)
                    {
                        if (!(r.Need > 0)) continue;
                        if (r.UnitMat > 1e-6) total += r.Need * r.UnitMat; else noPrice = true;
                    }
                    if (!(total > 0)) { if (noPrice) _dNoPrice++; return 0; }
                    double use = Math.Min(matGold, total), units = 0;
                    foreach (var r in list)
                    {
                        if (!(r.Need > 0) || !(r.UnitMat > 1e-6)) continue;
                        double dk = Math.Min(r.Need, use * (r.Need * r.UnitMat / total) / r.UnitMat);
                        if (!(dk > 0)) continue;
                        r.K += dk; r.Need -= dk; units += dk;
                    }
                    _dBuyU += units; _dBuyGold += use; _dBuyN++;
                    return (long)Math.Round(use);
                }
            }
            catch (Exception e) { Stumble("OnWorksMaterial", e); return 0; }
        }

        // ============================================================ linia doby
        private static void Line(int today, Settings s, int dist, int noPath, int parts, int low, double sumHs, double sumNeed, double idle, double value,
                                 double minR, double mAvg, double[] gH, double[] gNeed, double ms)
        {
            double sum = 0; int recs = 0;
            lock (Gate) foreach (var l in _byDist.Values) foreach (var r in l) if (r.K >= 0) { sum += r.K; recs++; }
            bool log = s.LogEnabled;
            if (log && !_startLine)
            {
                _startLine = true;
                var t = new StringBuilder(600);
                t.Append(P).Append("start | klucz Village Gear TAK | doba wlaczenia ").Append(_onDay).Append(", zuzycie: brak (L5; doba ").Append(_wearDay).Append(") | zapis: ").Append(_loadNote)
                 .Append(_loadBad > 0 ? ", zlych wpisow " + _loadBad : "").Append(" | regula: produkcja zajecia czesci x m, m = phi + (1 - phi) x min(1, K / H); H = ludzie czesci w domu (srednia 7 dob), ")
                 .Append("K = sprzet w jednostkach 'wyposazony czlowiek'; stosunek kwantowany co 0.01, od 0.98 w gore liczony jak 1 | tabela (phi; cena jednostki w dniach wartosci produkcji czlowieka):");
                for (int g = 0; g < GOut; g++) t.Append(' ').Append(GName[g]).Append(' ').Append(F2(GPhi[g])).Append("; ").Append(N0(GDays[g])).Append(g < GOut - 1 ? "," : "");
                t.Append(" | stado i stadniny poza LP (m = 1) | sprzet tylko z prawdziwego materialu robot gminy (te same zlecenia i przelewy co dotad; wsie z rekami bez sprzetu pierwsze w kolejce)")
                 .Append(" | bilans otwarcia: K0 = ludzie w domu + poza domem, minus strata ognia osad pustych z rabunku; domykany ").Append(OpenDays).Append(" dob | bez zuzycia (L5), przebudowa zajec bez zmian (L4).");
                Log.Info(t.ToString());
            }
            if (log && _dOpenN > 0)
                Log.Info(P + "bilans otwarcia sprzetu: dzien " + today + " | nowych czesci " + _dOpenN + ", jednostek " + N0(_dOpenU) + " (z domknieciem wczesniejszych), wartosc " + N0(_dOpenVal)
                         + " zl (cena jednostki z wyceny czesci; czesc bez wyceny liczona 0) | jedyny sprzet z niczego - znacznik: wpis czesci w zapisie arm_lp.");
            if (log && LogGate.Lines)
            {
                var sb = new StringBuilder(1200);
                sb.Append(P).Append("dzien ").Append(today).Append(" | okregow ").Append(dist).Append(" (bez drogi produkcji - m = 1: ").Append(noPath).Append("), czesci ").Append(parts)
                  .Append(", sprzet K ").Append(N0(sum)).Append(" jedn. (wart. ").Append(N0(value)).Append(" zl), rece H ").Append(N0(sumHs)).Append(", bez sprzetu ").Append(N0(sumNeed))
                  .Append(" (").Append(sumHs > 0 ? F2(100.0 * sumNeed / sumHs) : "-").Append("%), bezczynny sprzet ").Append(N0(idle))
                  .Append(" | czesci z m < 1: ").Append(low).Append(", najnizszy stosunek K/H ").Append(F2(minR)).Append(", m srednie wazone rekami ").Append(mAvg.ToString("0.0000", Inv))
                  .Append(" | wedlug rodzaju (rece / bez sprzetu):");
                for (int g = 0; g < GN; g++) sb.Append(' ').Append(GName[g]).Append(' ').Append(N0(gH[g])).Append('/').Append(N0(gNeed[g])).Append(g < GN - 1 ? "," : "");
                sb.Append(" | od poprzedniej linii: material robot gminy ").Append(N0(_dMatGold)).Append(" zl w ").Append(_dMatN).Append(" zleceniach, z tego na sprzet ").Append(N0(_dBuyGold))
                  .Append(" zl = ").Append(F1(_dBuyU)).Append(" jedn. w ").Append(_dBuyN).Append(" okregach, reszta ").Append(N0(_dMatGold - _dBuyGold)).Append(" zl roboty bez skutku (jak dotad)")
                  .Append("; zlecen wsi z potrzeba sprzetu postawionych pierwszych ").Append(_dFirst).Append(", potrzeba bez wyceny ").Append(_dNoPrice)
                  .Append(" | otwarcie ").Append(F1(_dOpenU)).Append(" jedn. (").Append(_dOpenN).Append(" czesci), spalone ").Append(F1(_dBurnU)).Append(" jedn. (").Append(_dBurnN).Append(" czesci)")
                  .Append(" | produkcja: okregow ze zmienionym m (przeliczone: KS, plan zajec, wycena prac) ").Append(_dChanged).Append(", pod KS zdjete ").Append(F1(_dKsKg)).Append(" kg/d w ")
                  .Append(_dKsN).Append(" rachunkach okregow, wycenionych okregow ").Append(_dValued);
                if (_prevSumDay == today - 1 && !double.IsNaN(_prevSum))
                    sb.Append(" | kontrola sprzetu: dzis - (wczoraj + kupione + otwarcie - spalone) = ").Append(F2(sum - (_prevSum + _dBuyU + _dOpenU - _dBurnU)));
                else sb.Append(" | kontrola sprzetu: brak wczorajszej sumy (pierwsza doba sesji albo klucza)");
                sb.Append(" | kontrola zlota: sprzet nie rusza zlota (material zaplacony w linii 'Akumulator kies'); material - (sprzet + roboty bez skutku) = 0")
                  .Append(" | wpisow w zapisie ").Append(recs).Append(" | koszt ").Append(F2(ms)).Append(" ms | poza watkiem ").Append(_dOff).Append(" | potkniecia ").Append(_st).Append(" (sesja ").Append(_stAll).Append(").");
                Log.Info(sb.ToString());
            }
            _prevSum = sum; _prevSumDay = today;
            lock (Gate) ZeroDay();
        }

        // ============================================================ menu okregu (linia informacyjna)
        /// <summary>DistrictMenu: "Gear: X of Y hands equipped (Z% yield). Missing: ..." - tylko przy kluczu i okregu z droga produkcji; null - nic. Nie rzuca.</summary>
        internal static string HeadText(Settlement st)
        {
            try
            {
                if (st == null || st.Village == null || st.StringId == null || !On) return null;
                double hs = 0, with = 0, wm = 0, worst = 0; int kind = -1;
                lock (Gate)
                {
                    List<Rec> list;
                    if (!_byDist.TryGetValue(st.StringId, out list)) return null;
                    foreach (var r in list)
                    {
                        if (!r.Path || !(r.WLp > 1e-9) || r.K < 0 || !(r.Hs >= 1.0)) continue;
                        hs += r.Hs; with += Math.Min(r.K, r.Hs); wm += r.Hs * MAt(r.Craft, r.Ratio);
                        if (r.Need > worst && r.Craft >= 0 && r.Craft < KsTab.Crafts.Length) { int k = DistrictWorks.KindOfCraft(KsTab.Crafts[r.Craft]); if (k >= 0) { worst = r.Need; kind = k; } }
                    }
                }
                if (!(hs >= 1.0)) return null;
                string t = "Gear: " + Math.Round(with).ToString("N0", Inv) + " of " + Math.Round(hs).ToString("N0", Inv) + " hands equipped (" + Math.Round(100.0 * wm / hs).ToString("0", Inv) + "% yield).";
                if (kind >= 0 && worst >= 0.5) t += " Missing: " + DistrictWorks.KindName[kind] + ".";
                return t;
            }
            catch (Exception e) { Stumble("HeadText", e); return null; }
        }

        // ============================================================ zapis (arm_lp)
        private static bool IdOk(string x) { return !string.IsNullOrEmpty(x) && x.IndexOf('~') < 0 && x.IndexOf(';') < 0 && x.IndexOf('|') < 0; }
        private static string CraftId(int c) { return c >= 0 && c < KsTab.Crafts.Length ? KsTab.Crafts[c] : "-"; }
        private static int CraftIx(string id) { int c; return id != null && id != "-" && KsTab.CraftIndex.TryGetValue(id, out c) ? c : -1; }

        /// <summary>Zapis: "lp1|doba wlaczenia|doba zuzycia|okreg~czesc~zajecie~K~zaliczka~H~stare zajecie~K stare~doba wpisu~puste rabunek~puste glod~puste rabunek przy wpisie;...".
        /// Bez wpisow i bez doby wlaczenia - "" (nic do wczytania).</summary>
        internal static string Export()
        {
            try
            {
                lock (Gate)
                {
                    if (_onDay < 0 && _byDist.Count == 0) return "";
                    var sb = new StringBuilder(_byDist.Count * 320 + 32);
                    sb.Append(SaveVer).Append('|').Append(_onDay).Append('|').Append(_wearDay).Append('|');
                    bool f = true;
                    foreach (var l in _byDist.Values)
                        foreach (var r in l)
                        {
                            if (r.K < 0 || !IdOk(r.Dist) || !IdOk(r.Part)) continue;
                            if (!f) sb.Append(';'); f = false;
                            sb.Append(r.Dist).Append('~').Append(r.Part).Append('~').Append(CraftId(r.Craft)).Append('~').Append(D(r.K)).Append('~').Append(D(r.Adv)).Append('~').Append(D(r.Hs))
                              .Append('~').Append(CraftId(r.OldCraft)).Append('~').Append(D(r.OldK)).Append('~').Append(r.Day0).Append('~').Append(D(r.SeenBr)).Append('~').Append(D(r.SeenBf))
                              .Append('~').Append(D(r.Br0));
                        }
                    return sb.ToString();
                }
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        /// <summary>Wczytanie (po Reset z konstruktora): brak klucza albo pusty - stan pusty (bilans otwarcia w pierwszej dobie klucza); zly wpis pominiety i policzony.</summary>
        internal static void Import(string data)
        {
            try
            {
                lock (Gate)
                {
                    _byDist.Clear(); _onDay = -1; _wearDay = -1; _loadBad = 0;
                    if (string.IsNullOrEmpty(data)) { _loadNote = "zapis bez arm_lp (bilans otwarcia w pierwszej dobie klucza)"; return; }
                    var p = data.Split('|');
                    if (p.Length < 4 || p[0] != SaveVer) { _loadNote = "arm_lp w nieznanej wersji '" + (p.Length > 0 ? p[0] : "") + "' - pominiety (bilans otwarcia od nowa)"; return; }
                    int d;
                    if (int.TryParse(p[1], NumberStyles.Integer, Inv, out d)) _onDay = d;
                    if (int.TryParse(p[2], NumberStyles.Integer, Inv, out d)) _wearDay = d;
                    int n = 0;
                    if (p[3].Length > 0)
                        foreach (var e in p[3].Split(';'))
                        {
                            var a = e.Split('~');
                            double k, adv, hs, ok, sbr, sbf, br0; int day0;
                            if (a.Length < 12 || !IdOk(a[0]) || !IdOk(a[1]) || !Num(a[3], out k) || !Num(a[4], out adv) || !Num(a[5], out hs) || !Num(a[7], out ok)
                                || !int.TryParse(a[8], NumberStyles.Integer, Inv, out day0) || !Num(a[9], out sbr) || !Num(a[10], out sbf) || !Num(a[11], out br0) || k < 0 || hs < 0)
                            { _loadBad++; continue; }
                            List<Rec> list;
                            if (!_byDist.TryGetValue(a[0], out list)) { list = new List<Rec>(); _byDist[a[0]] = list; }
                            if (Find(list, a[1]) != null) { _loadBad++; continue; }
                            list.Add(new Rec { Dist = a[0], Part = a[1], Craft = CraftIx(a[2]), K = k, Adv = Math.Max(0.0, adv), Hs = hs, OldCraft = CraftIx(a[6]), OldK = Math.Max(0.0, ok), Day0 = day0,
                                               SeenBr = sbr, SeenBf = sbf, Br0 = Math.Max(0.0, br0) });
                            n++;
                        }
                    _loadNote = "arm_lp wczytany: czesci " + n + ", doba wlaczenia " + _onDay;
                }
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
