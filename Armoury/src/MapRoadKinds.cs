using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// m96-valyria (Jeff 20.10: "Rob drogi Valyrii i trakty Essos"; PLAN 7.5 "drogi do wiosek i Valyrii"; audyt 08 rozdz. 2 i 3.8) - RODZAJE DROG ESSOS.
    ///
    /// DOWOD (stan przed zmiana, liczby z plikow i logu 12-25-19 WGRANIA 95): trakty moda to farba ROT (arm_map_roads_geo.tsv, 10016 jedn.), w Essos 2797 jedn.;
    /// po kulturze najblizszej twierdzy: stepy Dothrakow (khuzait) 0 jedn. farby, Ibben / Yi Ti / Qarth 0, Sarnor 161 (Kyth - Saath, 166 jedn. lamanej 45) -
    /// "gesty graf twierdz na stepach" z audytu 3.8 to prototyp z 08.10, nie gra. W stepie sa tylko drogi boczne do wiosek (plan f19, +7.5% "Side road")
    /// - poziom szlaku karawan juz dzis. Wszystkie trakty Essos, takze drogi Valyrii, daja dzis to samo +15% co zwykly trakt Westeros.
    /// LORE (awoiaf Valyrian roads / Demon road): drogi Valyrii z topionego kamienia, proste, pol stopy nad gruntem, od osrodka handlu do osrodka:
    /// demon road Volantis - Mantarys - (ruiny Bhorash) - Meereen z odnoga do Tolos; Pentos - Ghoyan Drohe - Norvos - Qohor.
    ///
    /// DANE: ModuleData/arm_map_roads_valyria.tsv (tools/drogi/drogi_valyria.py): odcinki farby V (droga Valyrii - najkrotsza trasa po farbie miedzy miastami
    /// trasy; 823 jedn.), C (szlak karawan - cala lamana farby w glebi Essos: co najmniej polowa jej dlugosci ma najblizsza twierdze kultury khuzait / sarnor /
    /// ibbenese / yiti / qartheen; v2 po recenzji - v1 brala odcinki osobno i ciela Kyth - Saath; 195.5 jedn.) i B
    /// (most Valyrii przez przerwe farby do 8 jedn. - lamane sondy urywaja sie pod miastami i przy brodach; 7 mostow, 26.8 jedn.). Bez wiersza = trakt.
    /// STRAZNIK: naglowek roads_md5 = md5 tekstu pliku drog (bez '\r'); inny plik drog = rodzaje nieczynne z powodem w linii startu (wszystko trakt).
    ///
    /// GRA: MapRoads.Build pyta Order o kolejnosc odcinkow siatki - V, mosty, trakty, szlaki (w komorce wygrywa pierwszy trafiony odcinek, wiec droga Valyrii
    /// wygrywa na rozstajach). Klasa odcinka w Grid.Cls; rodzaj czynny liczy Eff z kluczy (wylaczony klucz = trakt, most = nic - jak przed zmiana).
    ///  - droga Valyrii: +max(MapValyrianRoadSpeedPercent (25), MapRoadSpeedPercent)% jako wpis "Valyrian road" (co najmniej jak trakt);
    ///  - szlak karawan: +MapSideRoadSpeedPercent (7.5)% jako wpis "Caravan trail" (jak droga boczna; klucz MapEssosCaravanTrails);
    ///  - KindAt bez zmian (2 dla kazdej farby i mostu Valyrii) - bloto (MapMud), audyty i "na trakcie" w AI drog licza je jak dotad; AiRoadRoutes bierze
    ///    koszt z KindClassAt / ClassOfPoint (te same procenty), mosty Valyrii sa krawedziami sieci AI (przy wlaczonym kluczu na starcie budowy sieci).
    ///  - pola (p6-pola, RectHit) i szubienice (rozstaje z pliku) bez zmian - most to nie farba.
    /// WYGLAD: farba ROT to warstwa terrain.bin - gra nie daje zmiany jej koloru w biegu; ciemniejszy pas wymagalby osobnych stempli decali na trasach V (system
    /// komorek drog bocznych) i zdjec w autotescie - NIE w tej zmianie (opis w docs/noc-1920/m96-valyria-CHANGELOG.md).
    /// POMIAR (tylko log): linia startu "Drogi Valyrii (VR): start sesji" i doby "Drogi Valyrii (VR): dzien N" (spis partio-godzin z MapRoads.Hourly, liczenia
    /// predkosci z SpeedRoad - liczniki bez blokad, przyblizone jak w MapRoads). Nic w zapisie gry.
    /// </summary>
    internal static class MapRoadKinds
    {
        internal const string FileName = "arm_map_roads_valyria.tsv";
        private const string P = "Drogi Valyrii (VR): ";
        internal const byte Trakt = 0, Valyrian = 1, Caravan = 2, Bridge = 3;   // klasa odcinka siatki (Grid.Cls)
        private const int MaxPercent = 50;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal static readonly TextObject TxtValyrian = new TextObject("{=!}Valyrian road");
        internal static readonly TextObject TxtCaravan = new TextObject("{=!}Caravan trail");

        private static string _note = "nie wczytany";
        private static int _rowsV, _rowsC, _rowsB, _bad, _miss;
        private static int _segV, _segC, _segB;
        private static double _lenV, _lenC, _lenB;

        // liczniki z watkow roboczych (przyblizone, bez blokad - jak MapRoads) i spis godzinny (watek glowny)
        internal static long SpeedVal, SpeedCar;
        private static int _hN, _hV, _hC, _hT;
        private static int _stumbles;
        internal static volatile string AiNote = "siec AI drog jeszcze niezbudowana";   // AiRoadRoutes po budowie sieci (watek glowny)

        /// <summary>Klucze: droga Valyrii (MapValyrianRoads) / szlaki karawan (MapEssosCaravanTrails). Bez ustawien - wylaczone (jak przed zmiana).</summary>
        internal static bool ValOn(Settings s) { return s != null && s.MapValyrianRoads; }
        internal static bool CarOn(Settings s) { return s != null && s.MapEssosCaravanTrails; }

        /// <summary>Rodzaj czynny odcinka farby: Valyrian / Caravan przy wlaczonym kluczu, inaczej Trakt; most bez klucza - 255 (poza droga, jak przed zmiana).</summary>
        internal static int Eff(Settings s, byte cls)
        {
            if (cls == Trakt) return Trakt;
            if (cls == Valyrian) return ValOn(s) ? Valyrian : Trakt;
            if (cls == Bridge) return ValOn(s) ? Valyrian : 255;
            if (cls == Caravan) return CarOn(s) ? Caravan : Trakt;
            return Trakt;
        }

        /// <summary>Premia drogi Valyrii (ulamek): co najmniej premia traktu (MapRoadSpeedPercent przycietej - traktPct), MapValyrianRoadSpeedPercent 0..50.</summary>
        internal static float ValPct(Settings s, float traktPct)
        {
            int p = s != null ? s.MapValyrianRoadSpeedPercent : 0;
            float v = (p < 0 ? 0 : p > MaxPercent ? MaxPercent : p) / 100f;
            return v > traktPct ? v : traktPct;
        }

        // ------------------------------------------------------------------ dane (watek glowny, MapRoads.Load)
        /// <summary>Kolejnosc i klasy odcinkow siatki MapRoads (wolane z MapRoads.Build, watek glowny, raz na wczytanie): ids = droga punktu (plik posortowany
        /// po road, seq), sa = indeks punktu KONCA kazdego odcinka farby. Wynik: ord[k] >= 0 - odcinek farby sa[ord[k]], ord[k] &lt; 0 - most -(ord+1) (wspolrzedne
        /// w br: ax, ay, bx, by, pol-szerokosc), cls[k] - klasa. Kolejnosc: V, mosty, trakty, szlaki. null = bez rodzajow (brak pliku, inny plik drog, blad) -
        /// siatka jak przed zmiana. Plik czytany zawsze (niezaleznie od kluczy) - klucze dzialaja w biegu (Eff).</summary>
        internal static int[] Order(string roadsPath, List<int> ids, List<float> xs, List<float> ys, List<float> ws, List<int> sa, float halfMin, float halfMax,
                                    out byte[] cls, out float[] br)
        {
            cls = null; br = null;
            _rowsV = _rowsC = _rowsB = _bad = _miss = 0; _segV = _segC = _segB = 0; _lenV = _lenC = _lenB = 0;
            try
            {
                string path = MapVillageData.DataPathFor(FileName);
                if (path == null || !File.Exists(path)) { _note = "BRAK pliku ModuleData\\" + FileName + " - wszystkie drogi to trakty (jak przed zmiana)"; return null; }
                string md5 = Md5Text(roadsPath);
                string want = null;
                int version = 0;
                var rows = new List<string[]>(64);
                foreach (string raw in File.ReadAllText(path, Encoding.UTF8).Replace("\r", "").Split('\n'))
                {
                    if (raw.Length == 0) continue;
                    if (raw[0] == '#')
                    {
                        string h = raw.TrimStart('#').Trim();
                        if (h.StartsWith("version:", StringComparison.Ordinal)) int.TryParse(h.Substring(8).Trim(), NumberStyles.Integer, Inv, out version);
                        else if (h.StartsWith("roads_md5:", StringComparison.Ordinal)) want = h.Substring(10).Trim();
                        continue;
                    }
                    string[] c = raw.Split('\t');
                    if (c.Length < 7) { _bad++; continue; }
                    rows.Add(c);
                }
                if (version < 1) { _note = "plik " + FileName + " bez wersji - wszystkie drogi to trakty"; return null; }
                if (want == null || md5 == null || !string.Equals(want, md5, StringComparison.OrdinalIgnoreCase))
                {
                    _note = "plik " + FileName + " z INNEGO pliku drog (roads_md5 " + (want ?? "-") + ", " + MapRoads.FileName + " ma " + (md5 ?? "-")
                            + ") - rodzaje nieczynne, wszystkie drogi to trakty (dane do odswiezenia: python -I tools/drogi/drogi_valyria.py)";
                    return null;
                }
                // punkt (road, seq) -> indeks punktu: plik drog posortowany po road, seq; seq = indeks w lamanej (jak drogi_plik_modu.py)
                var first = new Dictionary<int, int>(400);
                var count = new Dictionary<int, int>(400);
                int n = ids.Count;
                for (int i = 0; i < n; i++)
                {
                    int id = ids[i];
                    if (i == 0 || ids[i - 1] != id) { if (!first.ContainsKey(id)) first[id] = i; count[id] = 0; }
                    count[id] = count[id] + 1;
                }
                var pcls = new byte[n];   // klasa odcinka konczacego sie w punkcie i
                var bridges = new List<float>(64);
                foreach (var c in rows)
                {
                    try
                    {
                        int road = int.Parse(c[0], Inv), s0 = int.Parse(c[1], Inv), s1 = int.Parse(c[2], Inv);
                        string kind = c[3];
                        int f, cn;
                        if (!first.TryGetValue(road, out f) || !count.TryGetValue(road, out cn) || s0 < 0 || s0 >= cn) { _miss++; continue; }
                        if (kind == "B")
                        {
                            int r2 = int.Parse(c[5], Inv), q2 = int.Parse(c[6], Inv);
                            int f2, cn2;
                            if (!first.TryGetValue(r2, out f2) || !count.TryGetValue(r2, out cn2) || q2 < 0 || q2 >= cn2) { _miss++; continue; }
                            int a = f + s0, b = f2 + q2;
                            float half = (ws[a] + ws[b]) * 0.25f;
                            if (!(half >= halfMin)) half = halfMin; else if (half > halfMax) half = halfMax;
                            bridges.Add(xs[a]); bridges.Add(ys[a]); bridges.Add(xs[b]); bridges.Add(ys[b]); bridges.Add(half);
                            _rowsB++;
                            _lenB += Math.Sqrt((xs[b] - xs[a]) * (xs[b] - xs[a]) + (ys[b] - ys[a]) * (ys[b] - ys[a]));
                            continue;
                        }
                        byte k = kind == "V" ? Valyrian : kind == "C" ? Caravan : (byte)255;
                        if (k == 255 || s1 <= s0 || s1 >= cn) { _bad++; continue; }
                        if (k == Valyrian) _rowsV++; else _rowsC++;
                        for (int q = s0 + 1; q <= s1; q++) pcls[f + q] = k;
                    }
                    catch { _bad++; }
                }
                // kolejnosc: V, mosty, trakty, szlaki
                int m = sa.Count, nb = bridges.Count / 5;
                var ord = new int[m + nb];
                cls = new byte[m + nb];
                int w = 0;
                for (int pass = 0; pass < 4; pass++)
                {
                    if (pass == 1)
                    {
                        for (int b = 0; b < nb; b++) { ord[w] = -(b + 1); cls[w] = Bridge; w++; }
                        continue;
                    }
                    byte want2 = pass == 0 ? Valyrian : pass == 2 ? Trakt : Caravan;
                    for (int k = 0; k < m; k++)
                    {
                        int i = sa[k];
                        if (pcls[i] != want2) continue;
                        ord[w] = k; cls[w] = want2; w++;
                        double l = Math.Sqrt((xs[i] - xs[i - 1]) * (xs[i] - xs[i - 1]) + (ys[i] - ys[i - 1]) * (ys[i] - ys[i - 1]));
                        if (want2 == Valyrian) { _segV++; _lenV += l; } else if (want2 == Caravan) { _segC++; _lenC += l; }
                    }
                }
                _segB = nb;
                br = bridges.ToArray();
                _note = "plik " + FileName + " v" + version + ": wierszy V " + _rowsV + ", C " + _rowsC + ", mosty " + _rowsB + ", zle " + _bad + ", poza plikiem drog " + _miss
                        + " | drogi Valyrii " + _segV + " odcinkow (" + _lenV.ToString("0.0", Inv) + " jedn.) + mosty " + _segB + " (" + _lenB.ToString("0.0", Inv) + " jedn.), szlaki karawan "
                        + _segC + " odcinkow (" + _lenC.ToString("0.0", Inv) + " jedn.); plik drog zgodny (md5 " + md5 + ")";
                return ord;
            }
            catch (Exception e) { cls = null; br = null; _note = "blad wczytania " + FileName + " (" + e.GetType().Name + ") - wszystkie drogi to trakty"; Stumble("Order", e); return null; }
        }

        /// <summary>p6-drogi (AiRoadRoutes.Build, watek glowny): klasa odcinka konczacego sie w punkcie (road, seq) oraz mosty jako pary punktow - z tego samego
        /// pliku i tym samym straznikiem co Order. null = bez rodzajow.</summary>
        internal static byte[] PointClasses(string roadsPath, List<int> ids, out List<int> bridgeA, out List<int> bridgeB)
        {
            bridgeA = null; bridgeB = null;
            try
            {
                string path = MapVillageData.DataPathFor(FileName);
                if (path == null || !File.Exists(path)) return null;
                string md5 = Md5Text(roadsPath), want = null;
                var rows = new List<string[]>(64);
                foreach (string raw in File.ReadAllText(path, Encoding.UTF8).Replace("\r", "").Split('\n'))
                {
                    if (raw.Length == 0) continue;
                    if (raw[0] == '#')
                    {
                        string h = raw.TrimStart('#').Trim();
                        if (h.StartsWith("roads_md5:", StringComparison.Ordinal)) want = h.Substring(10).Trim();
                        continue;
                    }
                    string[] c = raw.Split('\t');
                    if (c.Length >= 7) rows.Add(c);
                }
                if (want == null || md5 == null || !string.Equals(want, md5, StringComparison.OrdinalIgnoreCase)) return null;
                var first = new Dictionary<int, int>(400);
                var count = new Dictionary<int, int>(400);
                int n = ids.Count;
                for (int i = 0; i < n; i++)
                {
                    int id = ids[i];
                    if (i == 0 || ids[i - 1] != id) { if (!first.ContainsKey(id)) first[id] = i; count[id] = 0; }
                    count[id] = count[id] + 1;
                }
                var pcls = new byte[n];
                bridgeA = new List<int>(16); bridgeB = new List<int>(16);
                foreach (var c in rows)
                {
                    try
                    {
                        int road = int.Parse(c[0], Inv), s0 = int.Parse(c[1], Inv), s1 = int.Parse(c[2], Inv);
                        int f, cn;
                        if (!first.TryGetValue(road, out f) || !count.TryGetValue(road, out cn) || s0 < 0 || s0 >= cn) continue;
                        if (c[3] == "B")
                        {
                            int r2 = int.Parse(c[5], Inv), q2 = int.Parse(c[6], Inv), f2, cn2;
                            if (!first.TryGetValue(r2, out f2) || !count.TryGetValue(r2, out cn2) || q2 < 0 || q2 >= cn2) continue;
                            bridgeA.Add(f + s0); bridgeB.Add(f2 + q2);
                            continue;
                        }
                        byte k = c[3] == "V" ? Valyrian : c[3] == "C" ? Caravan : (byte)255;
                        if (k == 255 || s1 <= s0 || s1 >= cn) continue;
                        for (int q = s0 + 1; q <= s1; q++) pcls[f + q] = k;
                    }
                    catch { }
                }
                return pcls;
            }
            catch (Exception e) { Stumble("PointClasses", e); bridgeA = null; bridgeB = null; return null; }
        }

        /// <summary>md5 tekstu pliku (UTF-8, bez '\r' - git na tym komputerze zamienia konce linii) - jak roads_md5 w generatorze.</summary>
        private static string Md5Text(string path)
        {
            try
            {
                if (path == null || !File.Exists(path)) return null;
                byte[] b = Encoding.UTF8.GetBytes(File.ReadAllText(path, Encoding.UTF8).Replace("\r", ""));
                using (var md = MD5.Create())
                {
                    byte[] h = md.ComputeHash(b);
                    var sb = new StringBuilder(32);
                    for (int i = 0; i < h.Length; i++) sb.Append(h[i].ToString("x2", Inv));
                    return sb.ToString();
                }
            }
            catch { return null; }
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 3) Log.Error("MapRoadKinds." + where, e);
        }

        // ------------------------------------------------------------------ pomiar (watek glowny)
        /// <summary>MapRoads.Hourly (doba pomiaru): partia ladowa w ruchu - eff rodzaj czynny farby pod nia (-1 poza farba / droga boczna).</summary>
        internal static void Census(int eff)
        {
            _hN++;
            if (eff == Valyrian) _hV++;
            else if (eff == Caravan) _hC++;
            else if (eff == Trakt) _hT++;
        }

        /// <summary>MapRoads.SessionStart (po wczytaniu siatki): linia startu - klucze, procenty, dane.</summary>
        internal static void SessionLine(Settings s, bool gridReady)
        {
            if (s == null) return;
            _hN = _hV = _hC = _hT = 0; SpeedVal = 0; SpeedCar = 0;
            AiNote = "siec AI drog jeszcze niezbudowana";
            float tp = Math.Max(0, Math.Min(MaxPercent, s.MapRoadSpeedPercent)) / 100f;
            Log.Info(P + "start sesji - klucz MapValyrianRoads " + (ValOn(s) ? "tak" : "nie") + " (premia " + (ValPct(s, tp) * 100f).ToString("0", Inv) + "%, MapValyrianRoadSpeedPercent "
                     + s.MapValyrianRoadSpeedPercent.ToString(Inv) + ", trakt " + (tp * 100f).ToString("0", Inv) + "%), klucz MapEssosCaravanTrails " + (CarOn(s) ? "tak" : "nie")
                     + " (premia jak droga boczna " + s.MapSideRoadSpeedPercent.ToString("0.0", Inv) + "%) | " + _note + " | siatka drog " + (gridReady ? "wczytana" : "NIE")
                     + " | stan " + (!gridReady ? "NIECZYNNE (premia drog nieczynna - patrz Drogi (DR))" : _segV + _segC + _segB == 0 ? "NIECZYNNE (bez danych)" : "CZYNNE") + ".");
        }

        /// <summary>Takt doby (po MapRoads.Daily): w dobie linii (LogGate.Lines) linia "Drogi Valyrii (VR): dzien N" za dobe spisu; potem zerowanie. Doba bez linii
        /// (ciche logi) - jak MapRoads.Daily: spis zostaje (zbiera tylko w dobie pomiaru), liczniki predkosci od zera.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (!LogGate.Lines) { SpeedVal = 0; SpeedCar = 0; return; }
            try
            {
                if (s != null && s.MapRoadSpeedBonus && Campaign.Current != null)
                {
                    int day = (int)CampaignTime.Now.ToDays - 1;
                    long sv = Interlocked.Read(ref SpeedVal), sc = Interlocked.Read(ref SpeedCar);
                    Log.Info(P + "dzien " + day.ToString(Inv) + " | klucze: Valyria " + (ValOn(s) ? "tak" : "nie") + ", szlaki " + (CarOn(s) ? "tak" : "nie")
                             + " | partio-godzin w marszu " + _hN + ": na drodze Valyrii " + _hV + " (" + Pct(_hV, _hN) + "%), na szlaku karawan " + _hC + " (" + Pct(_hC, _hN)
                             + "%), na zwyklym trakcie " + _hT + " (" + Pct(_hT, _hN) + "%) | liczenia predkosci z premia: Valyria ok. " + sv + ", szlak ok. " + sc
                             + " | odcinkow V " + _segV + " + mosty " + _segB + ", C " + _segC + Audit(s, day) + " | " + AiNote + " | potkniecia " + _stumbles + ".");
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            _hN = _hV = _hC = _hT = 0; SpeedVal = 0; SpeedCar = 0;
        }

        /// <summary>Audyt w takcie linii (watek glowny, raz na dobe linii): do AuditMax partii AI w marszu na drodze Valyrii i na szlaku karawan - premia
        /// rzeczywista z modelu wprost (bez premii drogi / z premia, bez zapisu do pamieci partii) i wpis w podpowiedzi. Oczekiwane: Valyria = ValPct, szlak = premia
        /// drogi bocznej (poza sufitem kolumny i podloga gry).</summary>
        private static string Audit(Settings s, int day)
        {
            var sb = new StringBuilder(200);
            try
            {
                var all = TaleWorlds.CampaignSystem.Party.MobileParty.All;
                var main = TaleWorlds.CampaignSystem.Party.MobileParty.MainParty;
                // m103 P1 (VR-3): start listy przesuwany z doba - dotad co dobe pierwsze partie MobileParty.All, czyli tygodniami ta sama partia (98-160: 62 probki 1.01%)
                int cnt = all.Count, off = cnt > 0 ? (int)((((long)day * 7919L) % cnt + cnt) % cnt) : 0;
                for (int want = 4; want <= 5; want++)
                {
                    int n = 0, lines = 0, seen = 0, low = 0, lowFloor = 0, lowCap = 0, lowOther = 0;
                    double sum = 0, mn = double.MaxValue, mx = double.MinValue, sumFloor = 0;
                    StringBuilder ex = null;
                    string txt = (want == 4 ? TxtValyrian : TxtCaravan).ToString();
                    for (int k = 0; k < cnt && n < AuditMax; k++)
                    {
                        var mp = all[(off + k) % cnt];
                        if (mp == null || mp == main || !mp.IsActive || mp.AttachedTo != null || mp.IsCurrentlyAtSea || mp.IsTransitionInProgress || !mp.IsMoving) continue;
                        var p = mp.Position;
                        if (MapRoads.KindClassAt(p.X, p.Y) != want) continue;
                        seen++;
                        float r0, r1, lmin, lmax; bool hl;
                        if (!MapRoads.ProbeFor(mp, txt, out r0, out r1, out hl, out lmin, out lmax) || !(r0 > 0.0001f)) continue;
                        double b = r1 / r0 - 1.0;
                        n++; sum += b; if (b < mn) mn = b; if (b > mx) mx = b; if (hl) lines++;
                        // m103 recenzja (VR-3): podloga liczona dla KAZDEJ probki (jak AuditList w MapRoads.cs) - wynik bez premii na LimitMin znieksztalca premie takze,
                        // gdy z premia wyszedl ponad podloge (b miedzy 5% a pelna premia); narzedzie odejmuje od sredniej wszystkie probki z podlogi
                        bool atFloor = r0 <= lmin + 0.001f;
                        if (atFloor || b < 0.05)
                        {
                            // m103 P1 (VR-3): probka z podlogi albo premia ponizej 5% - id partii i powod: podloga gry (wynik bez premii na LimitMin - premia ginie w modelu), sufit kolumny (LimitMax), inne
                            low++;
                            string why;
                            if (atFloor) { lowFloor++; sumFloor += b; why = "podloga gry"; }
                            else if (lmax < float.MaxValue * 0.5f && r1 >= lmax - 0.0015f) { lowCap++; why = "sufit kolumny"; }
                            else { lowOther++; why = "inne"; }
                            if (low <= 3)
                            {
                                if (ex == null) ex = new StringBuilder(120); else ex.Append("; ");
                                ex.Append((mp.StringId ?? "?").Replace('|', '/')).Append(' ').Append((100.0 * b).ToString("0.00", Inv)).Append("% ").Append(why)
                                  .Append(" (bez premii ").Append(r0.ToString("0.000", Inv)).Append(", z premia ").Append(r1.ToString("0.000", Inv)).Append(')');
                            }
                        }
                    }
                    sb.Append(want == 4 ? " | audyt Valyria: " : " | audyt szlak: ").Append(n).Append(" partii");
                    if (n > 0) sb.Append(", premia rzeczywista sr. ").Append((100.0 * sum / n).ToString("0.00", Inv)).Append("% (min ").Append((100.0 * mn).ToString("0.00", Inv))
                                  .Append(", maks ").Append((100.0 * mx).ToString("0.00", Inv)).Append("), z wpisem '").Append(txt).Append("' ").Append(lines);
                    sb.Append(", oczekiwane ").Append(want == 4 ? (100f * MapRoads.ValPctNow(s)).ToString("0.0", Inv) : (100f * MapRoads.CarPctNow(s)).ToString("0.0", Inv)).Append('%');
                    // m103 P1 (VR-3): dopisek za "oczekiwane" (regex VR-1/VR-3 bez zmian; srednia wyzej dalej ze wszystkich probek - narzedzie odejmuje probki z podlogi)
                    if (low > 0) sb.Append(", ponizej 5% lub na podlodze: ").Append(low)   // m103 recenzja: na podlodze = wszystkie probki z podlogi (takze z premia >= 5%).Append(" (na podlodze predkosci gry ").Append(lowFloor).Append(", sr. ")
                                   .Append(lowFloor > 0 ? (100.0 * sumFloor / lowFloor).ToString("0.00", Inv) : "0.00").Append("%; sufit kolumny ").Append(lowCap)
                                   .Append("; inne ").Append(lowOther).Append(") [").Append(ex != null ? ex.ToString() : "").Append(']');
                }
            }
            catch (Exception e) { Stumble("Audit", e); sb.Append(" | audyt: blad"); }
            return sb.ToString();
        }

        private const int AuditMax = 6;

        private static string Pct(int a, int b) { return b > 0 ? (100.0 * a / b).ToString("0.0", Inv) : "-"; }
    }
}
