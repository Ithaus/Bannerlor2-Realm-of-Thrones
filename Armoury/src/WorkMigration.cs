using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 435-L10 - LANCUCH PODAZY (LP), krok 10: MIGRACJA ZA PRACA. Projekt PROJEKT-LANCUCH-po-krytyce (11.11) II.9, II.10 (wiersz migracji), II.13-II.17, wiersz L10.
    /// Polecenie Jeffa (11.11): "ludzie migruja i ida tam gdzie jest praca". Klucz Work Migration (NIE = nic nie jest liczone ani przenoszone, pamiec zamrozona w zapisie).
    ///
    /// REGULA (raz na 7 dob na region - region = warownia wsi, Village.Bound; ok. 1/7 regionow na dobe):
    ///  - ZRODLO: wies gry w stanie Normal, nie rabowana, warownia nie oblezona, hearth > 50, a srednie m okregu (czynnik sprzetu L3, wazony rekami) z 28 dob
    ///    < 0.90 albo glod gospodarzy ponad prog glodu (Famine - jak 433-p: racjonowanie to nie nedza).
    ///  - ILU: najwyzej Work Migration Year Share (1%) ludzi zrodla na rok (x 7 / 364 na przebieg; x2 przy glodzie); poza glodem najwyzej rece bez sprzetu okregu
    ///    (potrzeba L3); hearth zrodla nie schodzi ponizej 50 (zadna wies ponizej podlogi).
    ///  - BEZPIECZNIK ZBOZA (poza glodem): odejscie jest proporcjonalne (wszystkie czesci okregu, takze zboze), wiec plon na glowe sie nie zmienia - warunek to okreg,
    ///    ktory po wyjsciu dalej ma 1.2 x wlasnej potrzeby: plon doby spichlerza >= 1.2 x ludzie w domu albo zapas >= 1.2 x prog przednowka (Famine Ration Days;
    ///    w krainie z zima 2 x tyle). Bez migawki spichlerza: poza zima tak, w zimie nie. Przy glodzie odejscie zmniejsza glod - bez bezpiecznika.
    ///  - CEL (kolejno): (1) wies tego samego regionu z bezczynnym sprzetem, (2) taka wies w promieniu 250 z droga ladowa (pamiec drog gry, ta sama strona morza),
    ///    (3) wlasne miasto targowe wsi, gdy wskaznik plac za brak rak > 1 (KsWageHands.Factor). Wies celu przyjmuje najwyzej tylu ludzi, ilu wyposazy bezczynny sprzet
    ///    (VillageGear.MigOf: przybysi rozkladaja sie na czesci jak ludzie w domu; sprzet czekajacy na uchodzcow nie liczy sie), bez glodu, w stanie Normal.
    ///  - KSIEGA LUDZI: PeopleBook.Move(zrodlo, DOM -> POZA DOMEM, MWorkOut) i Move(cel, POZA DOMEM -> DOM, MWorkIn) w tej samej chwili (hearth po kazdej stronie
    ///    wlasnym k_v); miasto - TownShift(+n, MWorkIn). Miedzy regionami - MigOut / MigIn rownania regionu (PeopleBook.NoteWorkRegion). Uchodzcy, niewolni, szlachta,
    ///    zolnierze - poza regula (ruszamy tylko konto DOM).
    ///  - PIENIADZ: migrant zabiera min(kiesa wsi ponad skrzynie / ludzie, 28 dni koszyka wsi na czlowieka) - kiesa wsi zrodla -> kiesa wsi celu (Village.ChangeGold)
    ///    albo kiesa W miasta (TownFolk.WorkMigPurseIn). Przelew posiadacz -> posiadacz, bez kosztu drogi; ksiega pieniadza widzi go przez stan posiadaczy.
    ///  - GRACZ: wiesc (angielski tekst) tylko o jego wsiach, najwyzej raz na 28 dob na wies i 3 na dobe.
    /// Pamiec m okregow (arm_lpm: "lpm1|wies~m28~dob;..."); brak klucza - pamiec od zera (28 dob do pierwszego zrodla z m). Kolejnosc doby: PO OutlawLaw.Daily (bieda
    /// do lasu bierze z tej samej puli ludzi - obie reguly pilnuja podlogi 50), PRZED VillageGear.BeforeProduction (rece okregow tej doby) i ksiega ludzi (OthersRise).
    /// Watek glowny. Wyjatki per wies liczone (potkniecia), bez globalnego wylacznika.
    /// </summary>
    internal static class WorkMigration
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const string P = "LP migracja (435-L10): ";
        private const string Ver = "lpm1";
        private const double MDays = 28.0;          // srednia ruchoma m okregu
        private const double MLow = 0.90;           // prog zrodla
        private const double Radius = 250.0;        // cel (2): promien
        private const double GrainSafe = 1.2;       // bezpiecznik zboza
        private const double MinMen = 0.5;          // mniej - przebieg pominiety (okruchy)
        private const int PurseDays = 28;           // migrant zabiera najwyzej tyle dni koszyka wsi
        private const int NewsDays = 28;            // wiesc o wsi gracza najwyzej raz na tyle dob
        private const int NewsMax = 3;              // i najwyzej tyle wiesci na dobe

        private sealed class M { public double Avg; public int Days; }
        private static readonly Dictionary<string, M> _m = new Dictionary<string, M>(StringComparer.Ordinal);
        private static readonly object Gate = new object();
        private static string _loadNote = "nowa kampania albo zapis bez arm_lpm (pamiec m od zera)";
        private static int _loadBad;

        // sesja
        private static int _day = int.MinValue, _st, _stAll;
        private static bool _startLine, _offLine, _deadLine;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static readonly Dictionary<string, double> _mNow = new Dictionary<string, double>(StringComparer.Ordinal);
        private static readonly List<VillageBook.PartView> _book = new List<VillageBook.PartView>();
        private static readonly Dictionary<Settlement, double[]> _news = new Dictionary<Settlement, double[]>();   // wies gracza -> [wyszli, weszli] czekajace na wiesc
        private static readonly Dictionary<Settlement, int> _newsDay = new Dictionary<Settlement, int>();
        private static readonly HashSet<Village> _arrived = new HashSet<Village>();                                  // doba: wsie, ktore dzis przyjely ludzi (nie sa zrodlem tej doby)

        // doba
        private static int _dReg, _dSrcM, _dSrcHunger, _dToV1, _dToV2, _dToT, _hState, _hFloor, _hGrain, _hNoTarget, _hSmall, _hNoData, _dBelow50, _dBack, _dMoves;
        private static double _dOut, _dInV1, _dInV2, _dInT, _dHOut, _dHIn, _dNeedSrc, _dBackMen;
        private static long _dGoldSrc, _dGoldV, _dGoldT;

        private static void ZeroDay()
        {
            _dReg = _dSrcM = _dSrcHunger = _dToV1 = _dToV2 = _dToT = _hState = _hFloor = _hGrain = _hNoTarget = _hSmall = _hNoData = _dBelow50 = _dBack = _dMoves = 0;
            _dOut = _dInV1 = _dInV2 = _dInT = _dHOut = _dHIn = _dNeedSrc = _dBackMen = 0;
            _dGoldSrc = _dGoldV = _dGoldT = 0;
            _st = 0;
            _arrived.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            _st++; _stAll++;
            try { if (_err.Add(where)) Log.Error("WorkMigration." + where, e); } catch { }
        }

        /// <summary>Konstruktor ArmouryBehavior: stan jednej kampanii nie przecieka do drugiej.</summary>
        internal static void Reset()
        {
            lock (Gate) { _m.Clear(); _loadBad = 0; _loadNote = "nowa kampania albo zapis bez arm_lpm (pamiec m od zera)"; }
            _day = int.MinValue; _stAll = 0; _startLine = _offLine = _deadLine = false; _err.Clear(); _mNow.Clear(); _news.Clear(); _newsDay.Clear();
            ZeroDay();
        }

        private static string F1(double v) { return v.ToString("0.0", Inv); }
        private static string F2(double v) { return v.ToString("0.00", Inv); }
        private static string F3(double v) { return v.ToString("0.000", Inv); }
        private static string Name(Settlement s) { try { return s != null && s.Name != null ? s.Name.ToString() : "?"; } catch { return "?"; } }

        private static bool Hungry(Village v) { try { return Famine.HungerOf(v) > Famine.ThresholdNow; } catch { return false; } }

        private static bool Standing(Village v)
        {
            if (v == null || v.Settlement == null || v.VillageState != Village.VillageStates.Normal || v.Settlement.IsUnderRaid) return false;
            return v.Bound == null || !v.Bound.IsUnderSiege;
        }

        private static bool Road(Village v) { try { return TownFolk.DaMarketLive(v.TradeBound ?? v.Bound); } catch { return false; } }

        // ============================================================ doba
        /// <summary>ArmouryBehavior.OnDailyTick PO OutlawLaw.Daily (i liniach 433), PRZED ksiega ludzi i VillageGear.BeforeProduction. Nie rzuca.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            int today = (int)CampaignTime.Now.ToDays;
            if (_day == today) return;
            _day = today;
            if (!s.WorkMigration)
            {
                if (_startLine && !_offLine && s.LogEnabled) { _offLine = true; try { Log.Info(P + "dzien " + today + " | klucz Work Migration wylaczony - nikt nie migruje, pamiec m zamrozona w zapisie."); } catch { } }
                return;
            }
            _offLine = false;
            long t0 = Stopwatch.GetTimestamp();
            ZeroDay();
            if (!_startLine)
            {
                _startLine = true;
                if (s.LogEnabled)
                {
                    try
                    {
                        Log.Info(P + "start | Work Migration TAK, Work Migration Year Share " + F3(YearShare(s)) + " (x7/364 na przebieg regionu co 7 dob, x2 przy glodzie) | zrodlo: m okregu z "
                                 + MDays + " dob < " + F2(MLow) + " albo glod, hearth > 50 | cel: wies regionu z bezczynnym sprzetem, wies w promieniu " + Radius + " z droga ladowa, miasto targowe "
                                 + "z wskaznikiem plac za brak rak > 1 | wymaga: Village Gear " + (VillageGear.On ? "TAK" : "NIE") + ", People Unit " + (PeopleUnit.On ? "TAK" : "NIE")
                                 + ", kalibracja ludzi " + (PopulationLaw.Calibrated ? "TAK" : "NIE") + " | zapis: " + _loadNote + ".");
                    }
                    catch (Exception e) { Stumble("StartLine", e); }
                }
            }
            if (!VillageGear.On || !PeopleUnit.On || !PopulationLaw.Calibrated)
            {
                if (!_deadLine && s.LogEnabled)
                {
                    _deadLine = true;
                    try { Log.Info(P + "dzien " + today + " | NIECZYNNA: wymaga Village Gear (sprzet wsi), People Unit Enabled (jednostka ludzi) i skalibrowanej ludnosci - nikt nie migruje (linia raz na sesje)."); } catch { }
                }
                return;
            }
            int mN = 0, mLow = 0;
            try { mN = UpdateM(today, out mLow); } catch (Exception e) { Stumble("UpdateM", e); }
            double share = YearShare(s);
            var regions = new Dictionary<Settlement, List<Village>>();
            var all = new List<Village>();
            var seen = new HashSet<Village>();
            try
            {
                foreach (var v in Village.All)
                {
                    if (v == null || v.Settlement == null || v.Settlement.StringId == null || !seen.Add(v)) continue;   // ROT: Village.All podaje duplikaty
                    all.Add(v);
                    var r = v.Bound;
                    if (r == null || r.StringId == null) continue;
                    if ((today + (int)((uint)r.StringId.GetHashCode() % 7u)) % 7 != 0) continue;
                    List<Village> l;
                    if (!regions.TryGetValue(r, out l)) { l = new List<Village>(); regions[r] = l; }
                    l.Add(v);
                }
            }
            catch (Exception e) { Stumble("Daily(spis)", e); }
            var capLeft = new Dictionary<Village, double>();
            foreach (var kv in regions)
            {
                _dReg++;
                try { Region(kv.Key, kv.Value, all, capLeft, share, today); }
                catch (Exception e) { Stumble("Region(" + (kv.Key.StringId ?? "?") + ")", e); }
            }
            try { News(today); } catch (Exception e) { Stumble("News", e); }
            try { Line(s, today, mN, mLow, (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency); } catch (Exception e) { Stumble("Line", e); }
        }

        private static double YearShare(Settings s) { double x = s != null ? s.WorkMigrationYearShare : 0.01; return double.IsNaN(x) ? 0.0 : Math.Max(0.0, Math.Min(0.10, x)); }

        /// <summary>Srednia ruchoma m okregow (28 dob) z ostatniej doby sprzetu. Zwraca liczbe okregow w pamieci; mLow - ponizej progu z pelnymi 28 dobami.</summary>
        private static int UpdateM(int today, out int mLow)
        {
            mLow = 0;
            VillageGear.MigDistrictM(_mNow, today);
            lock (Gate)
            {
                foreach (var kv in _mNow)
                {
                    M r;
                    if (!_m.TryGetValue(kv.Key, out r)) { r = new M { Avg = kv.Value, Days = 0 }; _m[kv.Key] = r; }
                    else r.Avg += (kv.Value - r.Avg) / MDays;
                    if (r.Days < 9999) r.Days++;
                }
                foreach (var r in _m.Values) if (r.Days >= (int)MDays && r.Avg < MLow) mLow++;
                return _m.Count;
            }
        }

        private static bool LowM(Village v)
        {
            lock (Gate) { M r; return _m.TryGetValue(v.Settlement.StringId, out r) && r.Days >= (int)MDays && r.Avg < MLow; }
        }

        /// <summary>Bezpiecznik zboza zrodla (poza glodem). true - wolno odejsc.</summary>
        private static bool GrainOk(Village v, int today)
        {
            bool winter = false;
            try { winter = WinterBite.WinterNow() && WinterRealm.OfVillage(v) > 0f; } catch { winter = false; }
            double home, harvest, food, cap; float u; int day;
            if (!Famine.DayOf(v, out home, out harvest, out food, out cap, out u, out day) || day < today - 2 || !(home > 0)) return !winter;
            if (harvest >= GrainSafe * home) return true;
            return food / home >= GrainSafe * CraftUnblock.RationDays * (winter ? 2.0 : 1.0);
        }

        /// <summary>Wies moze przyjac ludzi (cel 1 i 2): ilu jeszcze dzis (pamiec dnia - kilku zrodel do jednego celu nie przepelnia). 0 - nie.</summary>
        private static double CapOf(Village t, Village src, Dictionary<Village, double> capLeft, int today)
        {
            if (t == null || t == src) return 0;
            double c;
            if (capLeft.TryGetValue(t, out c)) return c;
            c = 0;
            try
            {
                if (Standing(t) && !Hungry(t) && VillageBook.PartsOf(t, _book))
                {
                    double need, cap;
                    if (VillageGear.MigOf(t, _book, today, Road(t), out need, out cap) && need <= 1e-6) c = cap;   // wies z rekami bez sprzetu nie jest celem
                }
            }
            catch (Exception e) { Stumble("CapOf", e); c = 0; }
            capLeft[t] = c;
            return c;
        }

        private static void Region(Settlement region, List<Village> vs, List<Village> all, Dictionary<Village, double> capLeft, double share, int today)
        {
            foreach (var src in vs)
            {
                try { Source(src, region, vs, all, capLeft, share, today); }
                catch (Exception e) { Stumble("Source", e); }
            }
        }

        private static void Source(Village src, Settlement region, List<Village> vs, List<Village> all, Dictionary<Village, double> capLeft, double share, int today)
        {
            bool hungry = Hungry(src);
            bool lowM = LowM(src);
            if (!hungry && !lowM) return;
            if (_arrived.Contains(src)) return;                                    // przyjela dzis ludzi - nie oddaje ich tej samej doby
            if (!Standing(src)) { _hState++; return; }
            if (!(src.Hearth > 50f)) { _hFloor++; return; }
            if (!VillageBook.PartsOf(src, _book)) { _hNoData++; return; }
            double need, capSrc;
            if (!VillageGear.MigOf(src, _book, today, Road(src), out need, out capSrc)) { _hNoData++; return; }
            if (hungry) _dSrcHunger++; else _dSrcM++;
            double people = PopulationLaw.PeopleOf(src.Settlement);
            if (!(people > 0)) { _hNoData++; return; }
            double want = people * share * 7.0 / 364.0 * (hungry ? 2.0 : 1.0);
            if (!hungry) want = Math.Min(want, need);
            bool unit;
            float per = PeopleUnit.PerMan(src, PeopleUnit.GamePerMan, out unit);
            if (!unit || !(per > 0f)) { _hNoData++; return; }
            double can = ((double)src.Hearth - 50.0) / per - 1e-3;
            if (can < want) { want = Math.Max(0.0, can); if (want < MinMen) { _hFloor++; return; } }
            if (!hungry && !GrainOk(src, today)) { _hGrain++; return; }
            if (want < MinMen) { _hSmall++; return; }
            _dNeedSrc += need;
            // pieniadz migranta: min(kiesa ponad skrzynie / ludzie, 28 dni koszyka wsi / ludzie) na czlowieka
            double perMan = 0, chest = 0;
            try
            {
                double nv, rent;
                chest = DsVillage.Chest(src);
                if (TownFolk.DsVillageDay(src.Settlement, out nv, out rent))
                    perMan = Math.Max(0.0, Math.Min(Math.Max(0.0, src.Gold - chest) / people, PurseDays * Math.Max(0.0, nv) / people));
            }
            catch (Exception e) { Stumble("Purse", e); perMan = 0; }
            if (double.IsNaN(perMan) || double.IsInfinity(perMan)) perMan = 0;
            double left = want;
            // (1) wies regionu z bezczynnym sprzetem - najwiecej miejsca najpierw
            foreach (var t in vs.Where(x => x != src).OrderByDescending(x => CapOf(x, src, capLeft, today)).ToList())
            {
                if (left < MinMen) break;
                double c = CapOf(t, src, capLeft, today);
                if (c < MinMen) break;
                left -= ToVillage(src, t, Math.Min(left, c), perMan, chest, capLeft, 1);
            }
            // (2) wies w promieniu 250 z droga ladowa, ta sama strona morza - najblizsza najpierw
            if (left >= MinMen)
            {
                var p = src.Settlement.GetPosition2D;
                bool essos = DothrakiArms.EssosOf(src.Settlement);
                var near = new List<KeyValuePair<double, Village>>();
                foreach (var t in all)
                {
                    if (t == src || t.Bound == region || t.Settlement == null) continue;
                    double d = p.Distance(t.Settlement.GetPosition2D);
                    if (d > Radius) continue;
                    near.Add(new KeyValuePair<double, Village>(d, t));
                }
                foreach (var kv in near.OrderBy(x => x.Key))
                {
                    if (left < MinMen) break;
                    var t = kv.Value;
                    if (CapOf(t, src, capLeft, today) < MinMen) continue;
                    if (DothrakiArms.EssosOf(t.Settlement) != essos) continue;
                    float road;
                    try { road = CartGuard.DistSS(src.Settlement, t.Settlement); } catch { continue; }
                    if (CartGuard.NoLandRoad(road)) continue;
                    left -= ToVillage(src, t, Math.Min(left, CapOf(t, src, capLeft, today)), perMan, chest, capLeft, 2);
                }
            }
            // (3) wlasne miasto targowe z brakiem rak
            if (left >= MinMen)
            {
                var town = src.TradeBound;
                float tp;
                if (town != null && town.IsTown && town.Town != null && !town.IsUnderSiege && !town.IsStarving && KsWageHands.Factor(town) > 1.0 + 1e-6 && PeopleBook.TryTown(town, out tp))
                    left -= ToTown(src, town, left, perMan, chest);
            }
            if (left > want - 1e-9) _hNoTarget++;
        }

        private static long PurseOf(Village src, double men, double perMan, double chest)
        {
            if (!(perMan > 0) || !(men > 0)) return 0;
            double g = Math.Floor(perMan * men);
            g = Math.Min(g, Math.Max(0.0, src.Gold - chest));
            return g > 0 && !double.IsNaN(g) ? (long)Math.Min(int.MaxValue / 2, g) : 0;
        }

        /// <summary>Ludzie zrodla -> DOM wsi celu (MWorkOut / MWorkIn), kiesa za nimi. Zwraca przeniesionych.</summary>
        private static double ToVillage(Village src, Village dst, double n, double perMan, double chest, Dictionary<Village, double> capLeft, int kind)
        {
            if (!(n >= MinMen)) return 0;
            double hOut = PeopleBook.Move(src, PeopleBook.ADom, PeopleBook.AOutside, n, PeopleBook.MWorkOut, PeopleUnit.GamePerMan);
            if (!(hOut < 0)) { _hNoData++; return 0; }
            double hIn = PeopleBook.Move(dst, PeopleBook.AOutside, PeopleBook.ADom, n, PeopleBook.MWorkIn, PeopleUnit.GamePerMan);
            _dOut += n; _dHOut += hOut; _dHIn += hIn; _dMoves++;
            if (kind == 1) { _dInV1 += n; _dToV1++; } else { _dInV2 += n; _dToV2++; }
            if (src.Hearth < 50f - 1e-3f) _dBelow50++;
            _arrived.Add(dst);
            capLeft[dst] = Math.Max(0.0, (capLeft.ContainsKey(dst) ? capLeft[dst] : 0.0) - n);
            try { PeopleBook.NoteWorkRegion(src.Settlement, dst.Settlement, n); } catch (Exception e) { Stumble("NoteWorkRegion", e); }
            long g = PurseOf(src, n, perMan, chest);
            if (g > 0)
            {
                int had = src.Gold; long got = 0;
                try
                {
                    src.ChangeGold(-(int)g);
                    got = had - src.Gold;
                    if (got > 0)
                    {
                        int dh = dst.Gold;
                        dst.ChangeGold((int)got);
                        long put = dst.Gold - dh;
                        _dGoldSrc += got; _dGoldV += put;
                    }
                }
                catch (Exception e)
                {
                    try { long now = had - src.Gold; if (now > 0) src.ChangeGold((int)now); } catch { }   // wyjatek - zloto wraca do kiesy zrodla
                    Stumble("ToVillage(kiesa)", e);
                }
            }
            NoteNews(src.Settlement, n, 0); NoteNews(dst.Settlement, 0, n);
            return n;
        }

        /// <summary>Ludzie zrodla -> stan miasta targowego (MWorkOut / MWorkIn), kiesa do kiesy W miasta. Zwraca przeniesionych.</summary>
        private static double ToTown(Village src, Settlement town, double n, double perMan, double chest)
        {
            if (!(n >= MinMen)) return 0;
            double hOut = PeopleBook.Move(src, PeopleBook.ADom, PeopleBook.AOutside, n, PeopleBook.MWorkOut, PeopleUnit.GamePerMan);
            if (!(hOut < 0)) { _hNoData++; return 0; }
            double applied = PeopleBook.TownShift(town, n, PeopleBook.MWorkIn, true);
            if (applied < n - 1e-6)
            {
                // miasto nie przyjelo (stan znikl miedzy odczytem a ruchem) - reszta wraca do domu zrodla
                double back = n - Math.Max(0.0, applied);
                double hb = PeopleBook.Move(src, PeopleBook.AOutside, PeopleBook.ADom, back, PeopleBook.MWorkIn, PeopleUnit.GamePerMan);
                _dBack++; _dBackMen += back; _dHIn += hb;
            }
            double moved = Math.Max(0.0, applied);
            _dOut += n; _dHOut += hOut; _dMoves++;
            if (src.Hearth < 50f - 1e-3f) _dBelow50++;
            if (!(moved > 0)) return 0;
            _dInT += moved; _dToT++;
            try { PeopleBook.NoteWorkRegion(src.Settlement, town, moved); } catch (Exception e) { Stumble("NoteWorkRegion", e); }
            long g = PurseOf(src, moved, perMan, chest);
            if (g > 0)
            {
                int had = src.Gold;
                long got = TownFolk.WorkMigPurseIn(town, src, g);
                long gone = had - src.Gold;
                _dGoldSrc += gone; _dGoldT += got;
            }
            NoteNews(src.Settlement, moved, 0);
            return moved;
        }

        // ============================================================ wiesci gracza
        private static void NoteNews(Settlement st, double left, double came)
        {
            try
            {
                if (st == null || st.OwnerClan == null || st.OwnerClan != Clan.PlayerClan) return;
                double[] a;
                if (!_news.TryGetValue(st, out a)) { a = new double[2]; _news[st] = a; }
                a[0] += left; a[1] += came;
            }
            catch (Exception e) { Stumble("NoteNews", e); }
        }

        private static void News(int today)
        {
            if (_news.Count == 0) return;
            int n = 0;
            foreach (var st in _news.Keys.ToList())
            {
                if (n >= NewsMax) break;
                int last;
                if (_newsDay.TryGetValue(st, out last) && today - last < NewsDays) continue;
                var a = _news[st];
                int left = (int)Math.Round(a[0]), came = (int)Math.Round(a[1]);
                _news.Remove(st);
                if (st.OwnerClan != Clan.PlayerClan || (left < 1 && came < 1)) continue;
                string txt;
                if (left >= 1 && came >= 1) txt = left + " people left " + Name(st) + " to find work elsewhere, and " + came + " came there looking for work.";
                else if (left >= 1) txt = left + (left == 1 ? " person" : " people") + " left " + Name(st) + " to find work elsewhere: the village has more hands than tools.";
                else txt = came + (came == 1 ? " person" : " people") + " came to " + Name(st) + " looking for work: its idle tools have hands again.";
                Log.Player(txt);
                _newsDay[st] = today; n++;
            }
        }

        // ============================================================ linia
        private static void Line(Settings s, int today, int mN, int mLow, double ms)
        {
            bool events = _dMoves > 0 || _st > 0 || _dBelow50 > 0;
            if (!s.LogEnabled || !(LogGate.Lines || events)) return;
            double bookOut, bookIn;
            PeopleBook.WorkMen(out bookOut, out bookIn);
            double inAll = _dInV1 + _dInV2 + _dInT + _dBackMen;
            var sb = new StringBuilder(900);
            sb.Append(P).Append("dzien ").Append(today)
              .Append(" | regionow ").Append(_dReg).Append(" (zrodel: z m28 < ").Append(F2(MLow)).Append(' ').Append(_dSrcM).Append(", z glodu ").Append(_dSrcHunger)
              .Append(", rece bez sprzetu zrodel ").Append(F1(_dNeedSrc)).Append(')')
              .Append(" | wyszlo ").Append(F1(_dOut)).Append(" ludzi (hearth ").Append(F3(_dHOut)).Append(") - do wsi regionu ").Append(F1(_dInV1)).Append(" (").Append(_dToV1)
              .Append("), do wsi w promieniu ").Append(F1(_dInV2)).Append(" (").Append(_dToV2).Append("), do miast ").Append(F1(_dInT)).Append(" (").Append(_dToT).Append(')')
              .Append(_dBack > 0 ? ", wrocilo do domu (miasto nie przyjelo) " + F1(_dBackMen) : "")
              .Append(" | weszlo ").Append(F1(inAll)).Append(" (hearth wsi ").Append(F3(_dHIn)).Append(')')
              .Append(" | zloto z kies zrodel ").Append(_dGoldSrc).Append(" = do kies wsi ").Append(_dGoldV).Append(" + do kies W miast ").Append(_dGoldT)
              .Append(" | wstrzymane: stan wsi ").Append(_hState).Append(", podloga 50 ").Append(_hFloor).Append(", bezpiecznik zboza ").Append(_hGrain).Append(", brak celu ").Append(_hNoTarget)
              .Append(", za malo ").Append(_hSmall).Append(", bez danych ").Append(_hNoData)
              .Append(" | pamiec m28: okregow ").Append(mN).Append(", ponizej ").Append(F2(MLow)).Append(' ').Append(mLow)
              .Append(" | kontrola ludzi (wyszli - weszli) ").Append(F3(_dOut - inAll))
              .Append(", ksiega (MWorkOut - MWorkIn) ").Append(F3(bookOut - bookIn)).Append(", ksiega - linia ").Append(F3(bookOut - _dOut))
              .Append(", zlota ").Append(_dGoldSrc - _dGoldV - _dGoldT).Append(", wsi ponizej 50 hearth po wyjsciu ").Append(_dBelow50)
              .Append(" | koszt ").Append(ms.ToString("0.00", Inv)).Append(" ms | potkniecia ").Append(_st).Append(" (sesja ").Append(_stAll).Append(')');
            Log.Info(sb.ToString());
        }

        // ============================================================ zapis
        private static bool IdOk(string id) { return !string.IsNullOrEmpty(id) && id.IndexOf('|') < 0 && id.IndexOf(';') < 0 && id.IndexOf('~') < 0; }

        /// <summary>Zapis arm_lpm; "" - bez stanu (klucz nie jest pisany).</summary>
        internal static string Export()
        {
            try
            {
                lock (Gate)
                {
                    if (_m.Count == 0) return "";
                    var sb = new StringBuilder(_m.Count * 32 + 8);
                    sb.Append(Ver).Append('|');
                    bool f = true;
                    foreach (var kv in _m)
                    {
                        if (!IdOk(kv.Key) || kv.Value == null) continue;
                        if (!f) sb.Append(';');
                        f = false;
                        sb.Append(kv.Key).Append('~').Append(kv.Value.Avg.ToString("0.####", Inv)).Append('~').Append(kv.Value.Days.ToString(Inv));
                    }
                    return sb.ToString();
                }
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        /// <summary>Wczytanie (po Reset z konstruktora): brak klucza - pamiec od zera; zly wpis pominiety i policzony.</summary>
        internal static void Import(string data)
        {
            try
            {
                lock (Gate)
                {
                    _m.Clear(); _loadBad = 0;
                    if (string.IsNullOrEmpty(data)) { _loadNote = "zapis bez arm_lpm (pamiec m od zera)"; return; }
                    int bar = data.IndexOf('|');
                    if (bar <= 0 || data.Substring(0, bar) != Ver) { _loadNote = "arm_lpm w nieznanej wersji - pominiety (pamiec m od zera)"; return; }
                    string body = data.Substring(bar + 1);
                    if (body.Length > 0)
                        foreach (var e in body.Split(';'))
                        {
                            var a = e.Split('~');
                            double avg; int days;
                            if (a.Length < 3 || !IdOk(a[0]) || !double.TryParse(a[1], NumberStyles.Float, Inv, out avg) || double.IsNaN(avg) || double.IsInfinity(avg)
                                || !int.TryParse(a[2], NumberStyles.Integer, Inv, out days) || days < 0 || _m.ContainsKey(a[0]))
                            { _loadBad++; continue; }
                            _m[a[0]] = new M { Avg = Math.Max(0.0, Math.Min(1.0, avg)), Days = Math.Min(9999, days) };
                        }
                    _loadNote = "wczytano arm_lpm: okregow " + _m.Count + (_loadBad > 0 ? ", zlych wpisow " + _loadBad : "");
                }
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
