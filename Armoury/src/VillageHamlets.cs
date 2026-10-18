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
    /// te same 1957 wiosek i uid co v5, 215 wiosek z najgestszych skupisk przeniesionych na pustkowia TEGO SAMEGO okregu (z dala od drog i tras) jako przysiolki 5-8 osad, zwolnione osady do
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
    /// f10-uklad (18.10, Jeff: "zwieksz dwukrotnie minimalna odleglosc wioski od wioski", "wykorzystaj mase terenu"): uklad v7 (tools/wioski-v7/v7_uklad.py, klucz
    /// VillageLayoutV7) - linia startu porownuje z ukladem poprzednim (v7 z v6: plik _v6 albo kolumny v6_*; v6 z v5 jak dotad), druga linia "uklad v7 (f10-uklad)"
    /// liczy odleglosc minimalna (naglowek pliku min_distance) wioska-wioska i wioska-wies gry, przeniesione wobec v6 na ladzie; nakladka KS bierze kazdy wiersz
    /// z ziemia w kolumnach ks_* (v7: kazda przeniesiona wioska; v6: przysiolki - te same wiersze co dotad).
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
            _lastDay = -1; _stumbles = 0; _stumblesAll = 0; _started = false; _prev = null;
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
                // porownanie z ukladem poprzednim: v6 z v5 (plik _v5 modulu); f10-uklad: v7 z v6 (plik _v6) - zmienna v5 = "uklad poprzedni" (nazwa jak dotad)
                int pv = ver >= 7 ? 6 : 5;
                string pfile = pv == 6 ? MapVillageData.FileNameV6 : MapVillageData.FileNameV5;
                List<MapVillageRecord> v5 = null;
                if (ver >= 6)
                {
                    try
                    {
                        string p5 = MapVillageData.DataPathFor(pfile);
                        if (p5 != null && System.IO.File.Exists(p5))
                        {
                            var f5 = MapVillageData.Parse(System.IO.File.ReadAllText(p5, Encoding.UTF8));
                            if (f5.MissingColumns.Length == 0 && f5.Records.Count > 0) v5 = f5.Records;
                        }
                    }
                    catch (Exception e) { Stumble("VillageHamlets.SessionStart(v" + pv + ")", e); }
                }
                // recenzja: wiersz v5 kazdej wioski w samym pliku v6 (kolumny v5_*) - ksiega przenosi nim zapis v5 po uid bez pliku _v5; tu jego crc (= v5) i zapas porownania
                // f10-uklad: plik v7 niesie tez wiersz v6 (kolumny v6_*) - zapis v6 Jeffa po uid bez pliku _v6
                List<MapVillageRecord> v5e = null, v6e = null;
                if (ver >= 6) { try { v5e = MapVillageData.V5Of(recs); } catch (Exception e) { Stumble("VillageHamlets.SessionStart(v5_*)", e); } }
                if (ver >= 7) { try { v6e = MapVillageData.V6Of(recs); } catch (Exception e) { Stumble("VillageHamlets.SessionStart(v6_*)", e); } }
                string v5src = pfile;
                var pcols = pv == 6 ? v6e : v5e;
                if (v5 == null && pcols != null) { v5 = pcols; v5src = "kolumny v" + pv + "_* pliku ukladu"; }
                _prev = v5;
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
                    sb.Append(" | v").Append(pv).Append(" (").Append(v5src).Append(" crc ").Append(CrcOf(v5).ToString("X8")).Append("): wiosek ").Append(v5.Count).Append(", okregow ").Append(by5.Count)
                      .Append(", okregow z inna liczba wiosek ").Append(diffN).Append(", z inna suma osad ").Append(diffS).Append(", uid spoza v").Append(pv).Append(' ').Append(uidMiss)
                      .Append(", osad wiosek v").Append(ver).Append(' ').Append(s6).Append(" / v").Append(pv).Append(' ').Append(s5);
                }
                else sb.Append(ver >= 6 ? " | v" + pv + ": BRAK pliku " + pfile + " (bez porownania)" : " | v5: uklad tej sesji v" + ver + " (bez porownania)");
                if (ver >= 6) sb.Append(" | kolumny v5_* (zapis v5 po uid bez pliku _v5): crc ").Append(v5e != null ? CrcOf(v5e).ToString("X8") : "BRAK");
                if (ver >= 7) sb.Append(" | kolumny v6_* (zapis v6 po uid bez pliku _v6): crc ").Append(v6e != null ? CrcOf(v6e).ToString("X8") : "BRAK");
                // najblizsza sasiadka i skupiska
                double mn, p10, med; int crowd;
                NearStats(recs, out mn, out p10, out med, out crowd);
                sb.Append(" | najblizsza sasiadka v").Append(ver).Append(" min ").Append(F(mn, "0.00")).Append(" p10 ").Append(F(p10, "0.00")).Append(" med ").Append(F(med, "0.00"));
                if (v5 != null)
                {
                    double mn5, p105, med5; int crowd5;
                    NearStats(v5, out mn5, out p105, out med5, out crowd5);
                    sb.Append(", v").Append(pv).Append(" min ").Append(F(mn5, "0.00")).Append(" p10 ").Append(F(p105, "0.00")).Append(" med ").Append(F(med5, "0.00"))
                      .Append(" | skupiska (>=3 sasiadow w 10 jedn.) v").Append(ver).Append(' ').Append(crowd).Append(", v").Append(pv).Append(' ').Append(crowd5);
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
                sb.Append(" | klucze Village Layout V7 ").Append(s == null || s.VillageLayoutV7 ? "TAK" : "NIE")
                  .Append(", Village Layout V6 ").Append(s == null || s.VillageLayoutV6 ? "TAK" : "NIE").Append(", Village Hamlet Ks Land ").Append(s == null || s.VillageHamletKsLand ? "TAK" : "NIE")
                  .Append(" | potkniecia ").Append(_stumbles).Append(" | czas ").Append(F(sw.Elapsed.TotalMilliseconds, "0")).Append(" ms.");
                Log.Info(sb.ToString());
                if (ver >= 7) V7Line(recs, _prev);
            }
            catch (Exception e) { Stumble("VillageHamlets.SessionStart", e); }
        }

        private static List<MapVillageRecord> _prev;   // uklad poprzedni tej sesji (linia startu) - tylko do linii v7, zerowany po niej

        /// <summary>f10-uklad: linia ukladu v7 (raz na sesje, po linii startu): odleglosc minimalna z naglowka pliku (min_distance generatora = 2 x v6), wioska-wioska
        /// i wioska-wies gry (min, p10, mediana, wiosek ponizej odleglosci), pary wsi gry ponizej (dane gry), przeniesione wobec v6 (polozenie z pliku _v6 albo kolumn
        /// v6_*): ile, przesuniecie, na ladzie wedlug siatki gry, od miast i zamkow; wiersze z ziemia KS (ks_*). Nie rzuca.</summary>
        private static void V7Line(List<MapVillageRecord> recs, List<MapVillageRecord> prev)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                float dmin = VillageBook.LayoutMinDist;
                var side = new List<Vec2>(recs.Count);
                foreach (var r in recs) side.Add(new Vec2(r.X, r.Y));
                var mains = new List<Vec2>(); var towns = new List<Vec2>(); var castles = new List<Vec2>();
                var seenV = new HashSet<Village>();
                foreach (var st in Settlement.All)
                {
                    try
                    {
                        if (st == null) continue;
                        if (st.IsVillage) { if (st.Village != null && seenV.Add(st.Village)) mains.Add(st.GetPosition2D); }
                        else if (st.IsTown) towns.Add(st.GetPosition2D);
                        else if (st.IsCastle) castles.Add(st.GetPosition2D);
                    }
                    catch (Exception e) { Stumble("VillageHamlets.V7Line(osada)", e); }
                }
                double[] ss = Nearest(side, side, true), sm = Nearest(side, mains, false), mm = Nearest(mains, mains, true);
                double dd = dmin > 0f ? dmin : 0.0;
                int ssBelow = 0, smBelow = 0, mmBelow = 0;
                foreach (var v in ss) if (dd > 0 && v < dd - 1e-3) ssBelow++;
                foreach (var v in sm) if (dd > 0 && v < dd - 1e-3) smBelow++;
                foreach (var v in mm) if (dd > 0 && v < dd - 1e-3) mmBelow++;
                var sb = new StringBuilder(768);
                sb.Append(P).Append("uklad v7 (f10-uklad) | odleglosc minimalna ").Append(dmin > 0f ? F(dmin, "0.00") + " (naglowek pliku)" : "BRAK naglowka min_distance")
                  .Append(" | wioska-wioska ").Append(Stat(ss)).Append(", wiosek ponizej odleglosci ").Append(ssBelow)
                  .Append(" | wioska-wies gry ").Append(Stat(sm)).Append(", wiosek ponizej odleglosci ").Append(smBelow)
                  .Append(" | wsie gry ponizej odleglosci (dane gry) ").Append(mmBelow).Append(" z ").Append(mains.Count);
                if (prev != null && prev.Count == recs.Count)
                {
                    var byUid = new Dictionary<string, MapVillageRecord>(StringComparer.Ordinal);
                    foreach (var r in prev) byUid[r.Uid] = r;
                    var shifts = new List<double>();
                    int land = 0, noMesh = 0, moved = 0;
                    double dTown = double.MaxValue, dCastle = double.MaxValue;
                    var ms = Campaign.Current != null ? Campaign.Current.MapSceneWrapper : null;
                    foreach (var r in recs)
                    {
                        MapVillageRecord o;
                        if (!byUid.TryGetValue(r.Uid, out o)) continue;
                        double sh = Math.Sqrt((r.X - o.X) * (r.X - o.X) + (r.Y - o.Y) * (r.Y - o.Y));
                        if (sh < 0.5) continue;
                        moved++; shifts.Add(sh);
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
                        catch (Exception e) { Stumble("VillageHamlets.V7Line(lad)", e); }
                        foreach (var t in towns) { double d = Math.Sqrt((t.x - r.X) * (t.x - r.X) + (t.y - r.Y) * (t.y - r.Y)); if (d < dTown) dTown = d; }
                        foreach (var c in castles) { double d = Math.Sqrt((c.x - r.X) * (c.x - r.X) + (c.y - r.Y) * (c.y - r.Y)); if (d < dCastle) dCastle = d; }
                    }
                    shifts.Sort();
                    sb.Append(" | przeniesione wobec v6 ").Append(moved).Append(" z ").Append(recs.Count)
                      .Append(" (przesuniecie mediana ").Append(shifts.Count > 0 ? F(shifts[shifts.Count / 2], "0.0") : "-").Append(", max ").Append(shifts.Count > 0 ? F(shifts[shifts.Count - 1], "0.0") : "-")
                      .Append("), na ladzie ").Append(ms != null ? land.ToString(Inv) : "?").Append('/').Append(moved).Append(" (sciana ladowa siatki gry").Append(noMesh > 0 ? ", poza siatka " + noMesh : "")
                      .Append("), od miasta min ").Append(moved > 0 && towns.Count > 0 ? F(dTown, "0.0") : "-").Append(", od zamku min ").Append(moved > 0 && castles.Count > 0 ? F(dCastle, "0.0") : "-");
                }
                else sb.Append(" | przeniesione wobec v6: bez ukladu v6 (brak pliku _v6 i kolumn v6_*)");
                int ksn = 0;
                foreach (var r in recs) if (!string.IsNullOrEmpty(r.KsStart)) ksn++;
                sb.Append(" | wiosek z ziemia KS (ks_*) ").Append(ksn).Append(" | potkniecia ").Append(_stumbles).Append(" | czas ").Append(F(sw.Elapsed.TotalMilliseconds, "0")).Append(" ms.");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("VillageHamlets.V7Line", e); }
            finally { _prev = null; }
        }

        private static string Stat(double[] a)
        {
            if (a == null || a.Length == 0) return "-";
            var s = (double[])a.Clone();
            Array.Sort(s);
            int n = s.Length;
            double med = n % 2 == 1 ? s[n / 2] : 0.5 * (s[n / 2 - 1] + s[n / 2]);
            return "min " + F(s[0], "0.00") + " p10 " + F(s[n / 10], "0.00") + " med " + F(med, "0.00");
        }

        /// <summary>f10-uklad: odleglosc kazdego punktu a do najblizszego punktu b (self - te same listy, bez siebie); siatka 16 jedn., pierscienie do 40.</summary>
        private static double[] Nearest(List<Vec2> a, List<Vec2> b, bool self)
        {
            var outd = new double[a.Count];
            if (a.Count == 0 || b.Count == 0) return outd;
            const double cell = 16.0;
            var grid = new Dictionary<long, List<int>>();
            for (int j = 0; j < b.Count; j++)
            {
                long k = ((long)Math.Floor(b[j].x / cell) << 32) ^ (long)(uint)(int)Math.Floor(b[j].y / cell);
                List<int> l; if (!grid.TryGetValue(k, out l)) grid[k] = l = new List<int>();
                l.Add(j);
            }
            for (int i = 0; i < a.Count; i++)
            {
                double x = a[i].x, y = a[i].y, best = double.MaxValue;
                int gx = (int)Math.Floor(x / cell), gy = (int)Math.Floor(y / cell);
                for (int rr = 0; rr <= 40; rr++)
                {
                    for (int ix = gx - rr; ix <= gx + rr; ix++)
                        for (int iy = gy - rr; iy <= gy + rr; iy++)
                        {
                            if (rr > 0 && ix > gx - rr && ix < gx + rr && iy > gy - rr && iy < gy + rr) continue;
                            List<int> l;
                            if (!grid.TryGetValue(((long)ix << 32) ^ (long)(uint)iy, out l)) continue;
                            foreach (int j in l)
                            {
                                if (self && j == i) continue;
                                double d = Math.Sqrt((b[j].x - x) * (b[j].x - x) + (b[j].y - y) * (b[j].y - y));
                                if (d < best) best = d;
                            }
                        }
                    if (best <= rr * cell) break;
                }
                outd[i] = best == double.MaxValue ? 1e6 : best;
            }
            return outd;
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
        /// <summary>Nakladka ziemi przysiolkow na wiersze S pliku ziemi. fileCrc - crc danych pliku ziemi (EnsureLand). Zwraca crc ziemi KS tej sesji: bez nakladki = fileCrc,
        /// z nakladka = crc32(fileCrc + podpis nadpisanych wierszy) - recenzja: zapis KS zastosowany / uzbrojony przy innej nakladce (klucz Village Hamlet Ks Land
        /// przelaczony, inny uklad) idzie droga "inny plik ziemi" (stan pominiety, Plan od nowa), zamiast cicho przebudowac bloki przysiolkow na innej ziemi.</summary>
        internal static uint OverlayKs(Dictionary<string, StartCalibration.LandDist> vil, uint fileCrc)
        {
            OverlayRows(vil);
            uint eff = fileCrc;
            try
            {
                if (_orig.Count == 0) eff = fileCrc;
                else
                {
                    var rows = new List<StartCalibration.LandRow>(_orig.Keys);
                    rows.Sort((a, b) => string.CompareOrdinal(a.Uid, b.Uid));
                    var sb = new StringBuilder(rows.Count * 64 + 16);
                    sb.Append(fileCrc.ToString("X8")).Append("|przysiolki:");
                    foreach (var r in rows)
                    {
                        var o = r.O;
                        sb.Append(r.Uid).Append(':').Append(r.Start ?? "-").Append(':').Append(o != null ? o.Terr : "-").Append(':').Append(o != null ? o.Kind : "-").Append(':')
                          .Append(o != null ? o.Model : "-").Append(':').Append(o != null ? o.DSea.ToString("R", Inv) : "-").Append(':').Append(o != null ? o.DRiver.ToString("R", Inv) : "-")
                          .Append(':').Append(o != null ? o.DMount.ToString("R", Inv) : "-").Append(';');
                    }
                    eff = MapVillageData.Crc32(Encoding.UTF8.GetBytes(sb.ToString()));
                }
            }
            catch (Exception e)
            {
                // podpis nie policzony - nakladka zdjeta (wiersze z pliku), crc pliku: stan zgodny z tym, co KS naprawde czyta
                Stumble("VillageHamlets.OverlayKs(crc)", e);
                foreach (var kv in _orig) { kv.Key.O = kv.Value.Key; kv.Key.Start = kv.Value.Value; }
                _orig.Clear(); _ksRows = 0; eff = fileCrc;
                _ksNote = "blad podpisu nakladki - nakladka zdjeta, wiersze pliku ziemi";
            }
            if (vil != null)
                Log.Info(P + "KS crc ziemi | crc ziemi KS tej sesji " + eff.ToString("X8") + (eff == fileCrc ? " = plik " : " (z nakladka przysiolkow, wierszy " + _orig.Count + "; plik ") + fileCrc.ToString("X8")
                         + (eff == fileCrc ? " (bez nakladki)" : ")") + " | zapis KS zastosowany albo uzbrojony przy innej nakladce (klucz Village Hamlet Ks Land, uklad wiosek) idzie droga 'inny plik ziemi': stan pominiety, Plan od nowa.");
            return eff;
        }

        private static void OverlayRows(Dictionary<string, StartCalibration.LandDist> vil)
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
                var hamsAll = VillageBook.HamletRecords();
                // f10-uklad: kazdy wiersz z ziemia w kolumnach ks_* (v7 - kazda przeniesiona wioska i przysiolki v6, ktore zostaly; v6 - przysiolki, jak dotad)
                var hams = VillageBook.KsLandRecords();
                string why = null;
                if (vil == null) why = "brak pliku ziemi KS - nic";
                else if (!key) why = "klucz Village Hamlet Ks Land NIE - przysiolki w KS na ziemi starego miejsca wioski (wiersze pliku)";
                else if (ver < 6) why = "uklad v" + ver + " - wiersze pliku ziemi bez zmian";
                else if (hams.Count == 0) why = "uklad bez ziemi KS w kolumnach ks_* - wiersze pliku ziemi bez zmian";
                if (why != null)
                {
                    _ksNote = why + (restored > 0 ? " (przywrocone " + restored + " wierszy z poprzedniej sesji)" : "");
                    Log.Info(P + "KS ziemia przysiolkow | " + _ksNote + ".");
                    return;
                }
                var byUid = new Dictionary<string, MapVillageRecord>(StringComparer.Ordinal);
                int noLand = 0, badTerr = 0, badCraft = 0, badDist = 0;
                foreach (var r in hamsAll) if (string.IsNullOrEmpty(r.KsStart)) noLand++;      // przysiolek bez ziemi KS w ukladzie (zly wiersz ks_*)
                foreach (var r in hams)
                {
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
                sb.Append("uklad v").Append(ver).Append(": przysiolkow ").Append(hamsAll.Count).Append(", wiosek z ziemia w ukladzie (ks_*) ").Append(hams.Count)
                  .Append(", nadpisane wiersze S ziemi ").Append(_ksRows)
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
