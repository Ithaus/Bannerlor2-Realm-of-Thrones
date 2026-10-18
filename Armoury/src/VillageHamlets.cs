using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// PRZYSIOLKI NA PUSTKOWIACH (paczka sw-przysiolki, noc 17/18.10; Jeff 16.10: "daj wioski w miejscach odludnych, tez jacys ludzie zyja - teraz masa natkanych wiosek
    /// i masa pustego miejsca"; STAN-PRAC ### 52 W2). Uklad wiosek v6 (ModuleData\arm_map_villages.tsv, generator tools/wioski-v6/v6_przysiolki.py; klucz VillageLayoutV6):
    /// te same 1957 wiosek i uid co v5, 238 wiosek z najgestszych skupisk przeniesionych na pustkowia TEGO SAMEGO okregu jako przysiolki 5-8 osad, zwolnione osady do
    /// pozostalych wiosek okregu (suma osad wiosek okregu = v5, wiec wies gry i ludzie okregu bez zmian - ludzi nikt nie tworzy, n okregu idzie z ludzi gry).
    /// Ten plik NIE zmienia ksiegi wiosek ani widoku (one czytaja plik ukladu jak dotad); robi trzy rzeczy:
    ///  1. SessionStart - linia sprawdzenia ukladu (raz na sesje): crc, wiosek i okregow, porownanie z v5 (arm_map_villages_v5.tsv: liczba wiosek i suma osad na okreg),
    ///     najblizsza sasiadka i skupiska v6 / v5, przysiolki na ladzie (sciana siatki nawigacyjnej gry pod punktem - IsLandGroup jak widok), odleglosc od miast i zamkow;
    ///  2. OverlayKs - KS (kalibracja startu; StartCalibration.HamletLandOverlay z SessionStart ponizej - po McmSettings.Apply, PRZED StartCalibration.SessionStart,
    ///     wiec przed ResolveLive i pierwszym Planem): wiersz S pliku ziemi arm_ks_land.tsv o uid przysiolka dostaje ziemie punktu
    ///     przysiolka z kolumn ks_* ukladu v6 (teren, miejsce, obrazek, odleglosc od morza / rzeki / gor) i zajecie startowe z tej samej tabeli swiata (KsTab.LandRaw;
    ///     generator liczy ja kopia ks_ziemia.py); strefa klimatu, rynek, powierzchnia okregu i udzial osad (KsShares z ksiegi) bez zmian - przysiolek nie dostaje ziemi
    ///     ani ludzi z niczego, tylko jego ludzie pracuja tam, gdzie stoja. Klucz VillageHamletKsLand, tylko uklad v6; inny uklad albo klucz wylaczony - wiersze z pliku
    ///     (oryginaly zapamietane i przywracane przy kazdym starcie sesji KS - plik ziemi jest raz na proces);
    ///  3. Daily - co 7 dob linia: ludzie w domu przysiolkow (ksiega wiosek - ten sam podzial co dymek), udzial w wioskach, spalone przysiolki, nakladka KS.
    /// Zajecie przysiolka poza KS: tylko opis (lore i nazwa) - produkcje wsi gry licza modele gry / BK; obrazek "fishing" rybakow wchodzi w HamletKinds (ryby zamiast czesci
    /// zboza okregu - zamknieta tozsamosc zboze zdjete = ryby dodane, jak dla kazdej wioski rybackiej). Wyjatki liczone per miejsce (potkniecia, pierwszy blad do logu),
    /// bez globalnych wylacznikow. Nic w zapisie gry (stan przysiolkow to stan ksiegi wiosek po uid).
    /// </summary>
    internal static class VillageHamlets
    {
        private const string P = "Przysiolki (sw-przysiolki): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly HashSet<string> Terrs = new HashSet<string>(StringComparer.Ordinal) { "snieg", "piasek", "las", "step", "gory", "bagno", "trawa" };

        // sesja
        private static readonly HashSet<string> _hamlets = new HashSet<string>(StringComparer.Ordinal);
        private static int _lastDay = -1, _stumbles, _stumblesAll;
        private static bool _started;
        private static readonly HashSet<string> _errSites = new HashSet<string>(StringComparer.Ordinal);
        // nakladka KS: oryginaly wierszy ziemi (wiersz -> ziemia i zajecie z pliku) - plik ziemi jest raz na proces, nakladka raz na sesje
        private static readonly Dictionary<StartCalibration.LandRow, KeyValuePair<KsTab.Land, string>> _orig = new Dictionary<StartCalibration.LandRow, KeyValuePair<KsTab.Land, string>>();
        private static string _ksNote = "nakladka KS jeszcze nie liczona (KS start sesji)";
        private static int _ksRows;

        internal static void Reset()
        {
            _hamlets.Clear();
            _lastDay = -1; _stumbles = 0; _stumblesAll = 0; _started = false;
            lock (_errSites) _errSites.Clear();
            _ksNote = "nakladka KS jeszcze nie liczona (KS start sesji)";
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesAll++;
            bool first;
            lock (_errSites) first = _errSites.Add(where);
            if (first) Log.Error(where, e);
        }

        private static string F(double v, string f) { return v.ToString(f, Inv); }

        // ------------------------------------------------------------ 1. linia sprawdzenia ukladu (raz na sesje, po VillageBook.PlacePendingAll) i nakladka KS
        internal static void SessionStart()
        {
            if (_started) return;
            _started = true;
            try { StartCalibration.HamletLandOverlay(); } catch (Exception e) { Stumble("VillageHamlets.SessionStart(KS)", e); }   // nakladka ziemi KS przed StartCalibration.SessionStart
            SessionLine();
        }

        private static void SessionLine()
        {
            var sw = Stopwatch.StartNew();
            try
            {
                int ver = MapVillageData.LayoutVersion;
                var recs = VillageBook.LayoutRecords();
                uint crc;
                bool has = VillageBook.LayoutCrcNow(out crc) && recs != null;
                var sb = new StringBuilder(1024);
                sb.Append(P).Append("start sesji | uklad v").Append(ver).Append(" plik ").Append(has ? VillageBook.LayoutFile : MapVillageData.LayoutFileName)
                  .Append(" crc ukladu ").Append(has ? crc.ToString("X8") : "BRAK").Append(MapVillageData.ChoiceNote.Length > 0 ? " (" + MapVillageData.ChoiceNote + ")" : "");
                if (!has)
                {
                    sb.Append(" | plik ukladu nie wczytany - wioski i przysiolki nieczynne | czas ").Append(F(sw.Elapsed.TotalMilliseconds, "0")).Append(" ms.");
                    Log.Info(sb.ToString());
                    return;
                }
                var hams = new List<MapVillageRecord>();
                foreach (var r in recs) if (!string.IsNullOrEmpty(r.Hamlet)) { hams.Add(r); _hamlets.Add(r.Uid); }
                int withKs = 0;
                foreach (var r in hams) if (!string.IsNullOrEmpty(r.KsStart)) withKs++;
                var by6 = MapVillageData.ByDistrict(recs);
                sb.Append(" | wiosek ").Append(recs.Count).Append(", przysiolkow ").Append(hams.Count).Append(" (z ziemia KS ").Append(withKs).Append("), okregow z wioskami ").Append(by6.Count);
                // porownanie z v5 (tylko przy v6 - plik _v5 modulu)
                List<MapVillageRecord> v5 = null;
                if (ver >= 6)
                {
                    try
                    {
                        string p5 = MapVillageData.DataPathFor(MapVillageData.FileNameV5);
                        if (p5 != null && System.IO.File.Exists(p5))
                        {
                            var f5 = MapVillageData.Parse(System.IO.File.ReadAllText(p5, Encoding.UTF8));
                            if (f5.MissingColumns.Length == 0 && f5.Records.Count > 0) v5 = f5.Records;
                        }
                    }
                    catch (Exception e) { Stumble("VillageHamlets.SessionStart(v5)", e); }
                }
                if (v5 != null)
                {
                    var by5 = MapVillageData.ByDistrict(v5);
                    int diffN = 0, diffS = 0; long s6 = 0, s5 = 0;
                    foreach (var r in recs) s6 += r.Settlements;
                    foreach (var r in v5) s5 += r.Settlements;
                    var ids = new HashSet<string>(by6.Keys, StringComparer.Ordinal); ids.UnionWith(by5.Keys);
                    foreach (var id in ids)
                    {
                        List<MapVillageRecord> a, b;
                        by6.TryGetValue(id, out a); by5.TryGetValue(id, out b);
                        int na = a != null ? a.Count : 0, nb = b != null ? b.Count : 0;
                        if (na != nb) diffN++;
                        long sa = 0, sbb = 0;
                        if (a != null) foreach (var r in a) sa += r.Settlements;
                        if (b != null) foreach (var r in b) sbb += r.Settlements;
                        if (sa != sbb) diffS++;
                    }
                    var u5 = new HashSet<string>(StringComparer.Ordinal); foreach (var r in v5) u5.Add(r.Uid);
                    int uidMiss = 0; foreach (var r in recs) if (!u5.Contains(r.Uid)) uidMiss++;
                    sb.Append(" | v5 (").Append(MapVillageData.FileNameV5).Append(" crc ").Append(CrcOf(v5).ToString("X8")).Append("): wiosek ").Append(v5.Count).Append(", okregow ").Append(by5.Count)
                      .Append(", okregow z inna liczba wiosek ").Append(diffN).Append(", z inna suma osad ").Append(diffS).Append(", uid spoza v5 ").Append(uidMiss)
                      .Append(", osad wiosek v6 ").Append(s6).Append(" / v5 ").Append(s5);
                }
                else sb.Append(ver >= 6 ? " | v5: BRAK pliku " + MapVillageData.FileNameV5 + " (bez porownania)" : " | v5: uklad tej sesji v" + ver + " (bez porownania)");
                // najblizsza sasiadka i skupiska
                double mn, p10, med; int crowd;
                NearStats(recs, out mn, out p10, out med, out crowd);
                sb.Append(" | najblizsza sasiadka v").Append(ver).Append(" min ").Append(F(mn, "0.00")).Append(" p10 ").Append(F(p10, "0.00")).Append(" med ").Append(F(med, "0.00"));
                if (v5 != null)
                {
                    double mn5, p105, med5; int crowd5;
                    NearStats(v5, out mn5, out p105, out med5, out crowd5);
                    sb.Append(", v5 min ").Append(F(mn5, "0.00")).Append(" p10 ").Append(F(p105, "0.00")).Append(" med ").Append(F(med5, "0.00"))
                      .Append(" | skupiska (>=3 sasiadow w 10 jedn.) v6 ").Append(crowd).Append(", v5 ").Append(crowd5);
                }
                else sb.Append(" | skupiska (>=3 sasiadow w 10 jedn.) ").Append(crowd);
                // przysiolki na ladzie (siatka gry) i odleglosci od warowni
                if (hams.Count > 0)
                {
                    int land = 0, noMesh = 0;
                    double dTown = double.MaxValue, dCastle = double.MaxValue;
                    var towns = new List<Vec2>(); var castles = new List<Vec2>();
                    foreach (var st in Settlement.All)
                    {
                        try
                        {
                            if (st == null) continue;
                            if (st.IsTown) towns.Add(st.GetPosition2D); else if (st.IsCastle) castles.Add(st.GetPosition2D);
                        }
                        catch (Exception e) { Stumble("VillageHamlets.SessionStart(osada)", e); }
                    }
                    var ms = Campaign.Current != null ? Campaign.Current.MapSceneWrapper : null;
                    var occ = new SortedDictionary<string, int>(StringComparer.Ordinal);
                    foreach (var r in hams)
                    {
                        int k; occ.TryGetValue(r.Hamlet, out k); occ[r.Hamlet] = k + 1;
                        try
                        {
                            if (ms != null)
                            {
                                var cv = new CampaignVec2(new Vec2(r.X, r.Y), true);
                                PathFaceRecord f = ms.GetFaceIndex(in cv);
                                if (!f.IsValid()) noMesh++;
                                else if (MapVillageData.IsLandGroup(f.FaceGroupIndex)) land++;
                            }
                        }
                        catch (Exception e) { Stumble("VillageHamlets.SessionStart(lad)", e); }
                        foreach (var t in towns) { double d = Math.Sqrt((t.x - r.X) * (t.x - r.X) + (t.y - r.Y) * (t.y - r.Y)); if (d < dTown) dTown = d; }
                        foreach (var c in castles) { double d = Math.Sqrt((c.x - r.X) * (c.x - r.X) + (c.y - r.Y) * (c.y - r.Y)); if (d < dCastle) dCastle = d; }
                    }
                    sb.Append(" | przysiolki na ladzie ").Append(ms != null ? land.ToString(Inv) : "?").Append('/').Append(hams.Count).Append(" (sciana ladowa siatki gry")
                      .Append(noMesh > 0 ? ", poza siatka " + noMesh : "").Append("), od miasta min ").Append(towns.Count > 0 ? F(dTown, "0.0") : "-")
                      .Append(", od zamku min ").Append(castles.Count > 0 ? F(dCastle, "0.0") : "-").Append(" | zajecia: ");
                    bool first = true;
                    foreach (var kv in occ) { sb.Append(first ? "" : ", ").Append(kv.Key).Append(' ').Append(kv.Value); first = false; }
                }
                var s = Settings.Current;
                sb.Append(" | klucze Village Layout V6 ").Append(s == null || s.VillageLayoutV6 ? "TAK" : "NIE").Append(", Village Hamlet Ks Land ").Append(s == null || s.VillageHamletKsLand ? "TAK" : "NIE")
                  .Append(" | potkniecia ").Append(_stumbles).Append(" | czas ").Append(F(sw.Elapsed.TotalMilliseconds, "0")).Append(" ms.");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("VillageHamlets.SessionStart", e); }
        }

        /// <summary>Crc ukladu "uid:osady;" w kolejnosci pliku - ten sam napis co VillageBook.LayoutCrcOf i generator.</summary>
        private static uint CrcOf(List<MapVillageRecord> recs)
        {
            var sb = new StringBuilder(recs.Count * 40);
            foreach (var r in recs) sb.Append(r.Uid).Append(':').Append(r.Settlements.ToString(Inv)).Append(';');
            return MapVillageData.Crc32(Encoding.UTF8.GetBytes(sb.ToString()));
        }

        /// <summary>Najblizsza sasiadka (inne wioski poboczne): min, 10%, mediana; skupiska = wioski z co najmniej 3 sasiadkami w 10 jedn. (siatka 10 jedn.; raz na sesje).</summary>
        private static void NearStats(List<MapVillageRecord> recs, out double mn, out double p10, out double med, out int crowd)
        {
            mn = p10 = med = 0.0; crowd = 0;
            int n = recs.Count;
            if (n < 2) return;
            const double cell = 10.0;
            var grid = new Dictionary<long, List<int>>();
            Func<double, double, long> key = (x, y) => ((long)Math.Floor(x / cell) << 32) ^ (long)(uint)(int)Math.Floor(y / cell);
            for (int i = 0; i < n; i++)
            {
                long k = key(recs[i].X, recs[i].Y);
                List<int> l; if (!grid.TryGetValue(k, out l)) grid[k] = l = new List<int>();
                l.Add(i);
            }
            var nn = new double[n];
            for (int i = 0; i < n; i++)
            {
                double x = recs[i].X, y = recs[i].Y, best = double.MaxValue;
                int gx = (int)Math.Floor(x / cell), gy = (int)Math.Floor(y / cell), near10 = 0;
                for (int rr = 1; rr <= 40; rr++)
                {
                    for (int ix = gx - rr; ix <= gx + rr; ix++)
                        for (int iy = gy - rr; iy <= gy + rr; iy++)
                        {
                            if (rr > 1 && ix > gx - rr && ix < gx + rr && iy > gy - rr && iy < gy + rr) continue;   // tylko nowy pierscien
                            List<int> l;
                            if (!grid.TryGetValue(((long)ix << 32) ^ (long)(uint)iy, out l)) continue;
                            foreach (int j in l)
                            {
                                if (j == i) continue;
                                double d = Math.Sqrt((recs[j].X - x) * (recs[j].X - x) + (recs[j].Y - y) * (recs[j].Y - y));
                                if (d < best) best = d;
                                if (rr == 1 && d <= 10.0) near10++;
                            }
                        }
                    if (best <= rr * cell) break;                // po pierscieniu rr kazda nieznaleziona wioska jest dalej niz rr x cell
                }
                nn[i] = best;
                if (near10 >= 3) crowd++;
            }
            Array.Sort(nn);
            mn = nn[0]; p10 = nn[n / 10]; med = n % 2 == 1 ? nn[n / 2] : 0.5 * (nn[n / 2 - 1] + nn[n / 2]);
        }

        // ------------------------------------------------------------ 2. KS: ziemia punktu przysiolka (StartCalibration.HamletLandOverlay, po EnsureLand)
        internal static void OverlayKs(Dictionary<string, StartCalibration.LandDist> vil)
        {
            int restored = 0;
            try
            {
                foreach (var kv in _orig) { kv.Key.O = kv.Value.Key; kv.Key.Start = kv.Value.Value; restored++; }
                _orig.Clear();
                _ksRows = 0;
                var s = Settings.Current;
                bool key = s == null || s.VillageHamletKsLand;
                int ver = MapVillageData.LayoutVersion;
                var hams = VillageBook.HamletRecords();
                string why = null;
                if (vil == null) why = "brak pliku ziemi KS - nic";
                else if (!key) why = "klucz Village Hamlet Ks Land NIE - przysiolki w KS na ziemi starego miejsca wioski (wiersze pliku)";
                else if (ver < 6) why = "uklad v" + ver + " - wiersze pliku ziemi bez zmian";
                else if (hams.Count == 0) why = "uklad bez przysiolkow (kolumna hamlet) - wiersze pliku ziemi bez zmian";
                if (why != null)
                {
                    _ksNote = why + (restored > 0 ? " (przywrocone " + restored + " wierszy z poprzedniej sesji)" : "");
                    Log.Info(P + "KS ziemia przysiolkow | " + _ksNote + ".");
                    return;
                }
                var byUid = new Dictionary<string, MapVillageRecord>(StringComparer.Ordinal);
                int noLand = 0, badTerr = 0, badCraft = 0, badDist = 0;
                foreach (var r in hams)
                {
                    if (string.IsNullOrEmpty(r.KsStart)) { noLand++; continue; }
                    if (!Terrs.Contains(r.KsTerr)) { badTerr++; continue; }
                    if (!KsTab.CraftIndex.ContainsKey(r.KsStart)) { badCraft++; continue; }
                    byUid[r.Uid] = r;
                }
                var matched = new HashSet<string>(StringComparer.Ordinal);
                var byStart = new SortedDictionary<string, int>(StringComparer.Ordinal);
                var terrFrom = new SortedDictionary<string, int>(StringComparer.Ordinal);
                foreach (var d in vil.Values)
                {
                    foreach (var row in d.Side)
                    {
                        try
                        {
                            MapVillageRecord r;
                            if (row == null || row.Uid == null || !byUid.TryGetValue(row.Uid, out r)) continue;
                            if (!string.Equals(row.District, r.VillageId, StringComparison.Ordinal)) { badDist++; continue; }
                            var o = row.O != null ? row.O.Clone() : new KsTab.Land();
                            string pair = (row.O != null ? row.O.Terr : "?") + ">" + r.KsTerr;
                            o.Terr = r.KsTerr; o.Kind = r.KsKind; o.Model = r.KsModel; o.DSea = r.KsDSea; o.DRiver = r.KsDRiver; o.DMount = r.KsDMount; o.Main = false;
                            _orig[row] = new KeyValuePair<KsTab.Land, string>(row.O, row.Start);
                            row.O = o; row.Start = r.KsStart;
                            matched.Add(r.Uid);
                            int k; byStart.TryGetValue(r.KsStart, out k); byStart[r.KsStart] = k + 1;
                            terrFrom.TryGetValue(pair, out k); terrFrom[pair] = k + 1;
                        }
                        catch (Exception e) { Stumble("VillageHamlets.OverlayKs(wiersz)", e); }
                    }
                }
                _ksRows = matched.Count;
                var sb = new StringBuilder(512);
                sb.Append("uklad v").Append(ver).Append(": przysiolkow ").Append(hams.Count).Append(", nadpisane wiersze S ziemi ").Append(_ksRows)
                  .Append(" (zajecie startowe: ");
                bool first = true;
                foreach (var kv in byStart) { sb.Append(first ? "" : ", ").Append(kv.Key).Append(' ').Append(kv.Value); first = false; }
                sb.Append("), bez wiersza w pliku ziemi ").Append(byUid.Count - matched.Count).Append(", bez ziemi w ukladzie ").Append(noLand).Append(", zly teren ").Append(badTerr)
                  .Append(", zle zajecie ").Append(badCraft).Append(", inny okreg ").Append(badDist);
                sb.Append(" | teren stary>nowy: ");
                first = true; int shown = 0;
                foreach (var kv in terrFrom) { if (shown++ >= 12) break; sb.Append(first ? "" : ", ").Append(kv.Key).Append(' ').Append(kv.Value); first = false; }
                sb.Append(" | strefa, rynek, ha okregu i udzial osad (KsShares) bez zmian - ziemia i ludzie nie z niczego").Append(restored > 0 ? " | przywrocone z poprzedniej sesji " + restored : "");
                _ksNote = sb.ToString();
                Log.Info(P + "KS ziemia przysiolkow | " + _ksNote + ".");
            }
            catch (Exception e) { Stumble("VillageHamlets.OverlayKs", e); }
        }

        // ------------------------------------------------------------ 3. linia co 7 dob
        internal static void Daily()
        {
            try
            {
                if (Campaign.Current == null) return;
                int today = (int)CampaignTime.Now.ToDays;
                if (_lastDay >= 0 && today - _lastDay < 7) return;
                _lastDay = today;
                if (!_started) SessionStart();
                var sw = Stopwatch.StartNew();
                double hHome = 0, hSet = 0, sideHome = 0, mainHome = 0; int burned = 0, dists = 0;
                bool ok = _hamlets.Count > 0 && VillageBook.HamletPeople(_hamlets, out hHome, out hSet, out sideHome, out mainHome, out burned, out dists);
                var sb = new StringBuilder(512);
                sb.Append(P).Append("dzien ").Append(today).Append(" | uklad v").Append(MapVillageData.LayoutVersion).Append(", przysiolkow ").Append(_hamlets.Count);
                if (ok)
                {
                    double all = sideHome + mainHome;
                    sb.Append(" w ").Append(dists).Append(" okregach | ludzie w domu: przysiolki ").Append(F(hHome, "0")).Append(" (osad ").Append(F(hSet, "0.0")).Append("), wioski poboczne ")
                      .Append(F(sideHome, "0")).Append(", wsie gry ").Append(F(mainHome, "0")).Append(", udzial przysiolkow w ludziach okregow ").Append(all > 0 ? F(100.0 * hHome / all, "0.00") : "0").Append('%')
                      .Append(" | spalone przysiolki ").Append(burned);
                }
                else sb.Append(_hamlets.Count == 0 ? " | uklad bez przysiolkow" : " | ksiega wiosek nieczynna");
                sb.Append(" | KS: ").Append(_ksNote).Append(" | potkniecia ").Append(_stumbles).Append(" (sesja ").Append(_stumblesAll).Append(") | czas ").Append(F(sw.Elapsed.TotalMilliseconds, "0.0")).Append(" ms.");
                _stumbles = 0;
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("VillageHamlets.Daily", e); }
        }
    }
}
