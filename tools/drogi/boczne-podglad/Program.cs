// f11-drogi: PODGLAD PLANU BOCZNYCH DROG POZA GRA - ten sam plan co w grze (Armoury/src/MapSideRoads.Plan.cs) na plikach z repo i ROT.
// Po co: liczby planu (drogi, dlugosc, stemple, czas) i cele zdjec autotestu dla dowolnego ukladu wiosek (v6, v7 ...) bez uruchamiania gry.
// W grze wioski ukladu bierze ksiega wiosek (VillageBook), wsie gry, miasta i zamki - gra; tu: plik ukladu i ROT-Map/ModuleData/settlements.xml
// (polozenie osad z pliku ROT - w grze to samo, bez przesuniec obrazkow wiosek przez widok).
//
// Uruchomienie z katalogu repo (dotnet 8):
//   dotnet run -c Release --project tools/drogi/boczne-podglad -- [--uklad plik.tsv] [--osady settlements.xml] [--wyj katalog] [Klucz wartosc ...]
//   klucze jak w pliku-przelaczniku autotestu (MaxHop 16, NearExtra 3 ...); wynik: <wyj>/roads.tsv, stamps.tsv, nodes.tsv, obst.tsv i linia planu.
//   f19-drogi: domyslnie regula "kazda wioska ukladu" (AllVillages 1, jak klucz MapSideRoadsAllVillages w grze); AllVillages 0 = plan f11.
// Obrazek: python -I tools/drogi/boczne-podglad/rysuj.py <wyj> <srodek x> <srodek y> <bok jedn.> <plik.png> [piksele na jedn.] (wymaga Pillow).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Armoury;

internal static class Podglad
{
    private static int Main(string[] a)
    {
        var inv = CultureInfo.InvariantCulture;
        string md = Path.Combine("Armoury", "ModuleData");
        string uklad = Path.Combine(md, "arm_map_villages.tsv");
        string osady = @"C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\ROT-Map\ModuleData\settlements.xml";
        string wyj = Path.Combine("tools", "drogi", "boczne-podglad", "wynik");
        var p = new SideRoadPlan.Params();
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] == "--uklad" && i + 1 < a.Length) { uklad = a[++i]; continue; }
            if (a[i] == "--osady" && i + 1 < a.Length) { osady = a[++i]; continue; }
            if (a[i] == "--wyj" && i + 1 < a.Length) { wyj = a[++i]; continue; }
            if (i + 1 < a.Length)
            {
                var f = typeof(SideRoadPlan.Params).GetField(a[i]);
                if (f != null)
                {
                    if (f.FieldType == typeof(float)) f.SetValue(p, float.Parse(a[i + 1], inv)); else f.SetValue(p, int.Parse(a[i + 1], inv));
                    i++;
                    continue;
                }
            }
            Console.WriteLine("nieznany argument: " + a[i]);
            return 2;
        }
        var g = SideRoadPlan.ParseGrid(File.ReadAllText(Path.Combine(md, "arm_map_side_roads_grid.tsv")));
        var m = SideRoadPlan.ParseMainRoads(File.ReadAllText(Path.Combine(md, "arm_map_roads_geo.tsv")));
        var nodes = new List<SideRoadPlan.Node>();
        var obst = new List<SideRoadPlan.Obstacle>();
        var cols = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(uklad))
        {
            if (line.StartsWith("#id\t", StringComparison.Ordinal)) { var h = line.Substring(1).Split('\t'); for (int i = 0; i < h.Length; i++) cols[h[i]] = i; continue; }
            if (line.Length == 0 || line[0] == '#') continue;
            var c = line.Split('\t');
            int ix = cols.ContainsKey("x") ? cols["x"] : 3, iy = cols.ContainsKey("y") ? cols["y"] : 4, iu = cols.ContainsKey("uid") ? cols["uid"] : 9;
            var nd = new SideRoadPlan.Node { X = float.Parse(c[ix], inv), Y = float.Parse(c[iy], inv), Id = c[iu], Game = false };
            // f19: nazwa i obrys obrazka (jak MapVillageRecord w grze: name, front_deg, half_len, half_wid)
            if (cols.ContainsKey("name") && cols["name"] < c.Length) nd.Name = c[cols["name"]];
            float fv;
            if (cols.ContainsKey("front_deg") && cols["front_deg"] < c.Length && float.TryParse(c[cols["front_deg"]], NumberStyles.Float, inv, out fv)) nd.FrontDeg = fv;
            if (cols.ContainsKey("half_len") && cols["half_len"] < c.Length && float.TryParse(c[cols["half_len"]], NumberStyles.Float, inv, out fv)) nd.Hl = fv;
            if (cols.ContainsKey("half_wid") && cols["half_wid"] < c.Length && float.TryParse(c[cols["half_wid"]], NumberStyles.Float, inv, out fv)) nd.Hw = fv;
            nodes.Add(nd);
        }
        int layoutN = nodes.Count;
        string sx = File.ReadAllText(osady);
        foreach (Match mm in Regex.Matches(sx, @"<Settlement\s[^>]*>"))
        {
            string s = mm.Value;
            var px = Regex.Match(s, "\\sposX=\"([^\"]+)\""); var py = Regex.Match(s, "\\sposY=\"([^\"]+)\"");
            if (!px.Success || !py.Success) continue;
            int end = sx.IndexOf("</Settlement>", mm.Index + mm.Length, StringComparison.Ordinal);
            string body = end > 0 ? sx.Substring(mm.Index, end - mm.Index) : "";
            float x = float.Parse(px.Groups[1].Value, inv), y = float.Parse(py.Groups[1].Value, inv);
            string id = Regex.Match(s, "\\sid=\"([^\"]+)\"").Groups[1].Value;
            string nm = Regex.Replace(Regex.Match(s, "\\sname=\"([^\"]+)\"").Groups[1].Value, "\\{=[^}]*\\}", "");   // f19: nazwa wsi gry do logu
            if (body.Contains("<Village")) nodes.Add(new SideRoadPlan.Node { X = x, Y = y, Id = id, Game = true, Name = nm });
            else if (body.Contains("<Town")) obst.Add(new SideRoadPlan.Obstacle { X = x, Y = y });
        }
        Console.WriteLine("siatka " + g.Nx + " x " + g.Ny + " (lad " + g.Land + "), trakty: lamanych " + m.Lines + ", odcinkow " + m.Segments + "; wioski ukladu " + layoutN + " (" + uklad + "), wsie gry "
                          + (nodes.Count - layoutN) + ", miasta i zamki " + obst.Count + " | " + p.Text());
        var r = SideRoadPlan.Build(g, m, nodes, obst, p);
        Console.WriteLine(r.Text());
        Console.WriteLine("miejsca z najgestsza siecia: " + SideRoadPlan.DenseSpots(r, 8));
        // f22-drogi-perf: podzial na komorki i oczekiwane decale w scenie (ten sam kod co w grze - MapSideRoads.ChunkPlan.cs; najdalej 160 jedn. od kamery jak domyslny klucz)
        var cg = MapSideRoads.GroupChunks(r, nodes, MapSideRoads.ChunkCapDefault);
        if (cg != null)
            Console.WriteLine("komorki f22: " + cg.Cells + " po " + cg.Cell.ToString("0", inv) + " jedn. (na komorke srednio " + cg.PerAvg.ToString("0", inv) + ", mediana " + cg.PerMed + ", p90 " + cg.PerP90
                              + ", najwiecej " + cg.PerMax + "); " + cg.Expect);
        Directory.CreateDirectory(wyj);
        using (var w = new StreamWriter(Path.Combine(wyj, "roads.tsv")))
            for (int k = 0; k < r.Roads.Count; k++)
            {
                var rd = r.Roads[k];
                for (int i = 0; i < rd.X.Length; i++) w.WriteLine(k + "\t" + i + "\t" + rd.X[i].ToString("0.###", inv) + "\t" + rd.Y[i].ToString("0.###", inv) + "\t" + rd.Join + "\t" + rd.Smooth + "\t" + rd.Pass + "\t" + rd.Node);
            }
        using (var w = new StreamWriter(Path.Combine(wyj, "stamps.tsv")))
            foreach (var s in r.Stamps)
                w.WriteLine(s.X.ToString("0.###", inv) + "\t" + s.Y.ToString("0.###", inv) + "\t" + s.Yaw.ToString("0.###", inv) + "\t" + s.Across.ToString("0.###", inv) + "\t"
                            + s.Along.ToString("0.###", inv) + "\t" + s.Alpha.ToString("0.##", inv) + "\t" + s.Road);
        // f19: stan wioski (SideRoadPlan.St*: 1 droga, 2 obrazek na farbie, 3 styk z siecia, 4 bez trasy, 5 wies gry przy trakcie, 6 wies gry z droga, 7 ostroga,
        // 8 wies gry - wezel bez wlasnej drogi, 9 wies gry bez trasy, 10 ponad limit drog, 11 laczka przy trakcie); plan f11 - stan z drog planu (1 / 0)
        var st = r.States;
        if (st == null) { st = new byte[nodes.Count]; foreach (var rd in r.Roads) st[rd.Node] = 1; }
        using (var w = new StreamWriter(Path.Combine(wyj, "nodes.tsv")))
            for (int k = 0; k < nodes.Count; k++)
            {
                var n = nodes[k];
                w.WriteLine(n.X.ToString("0.###", inv) + "\t" + n.Y.ToString("0.###", inv) + "\t" + (n.Game ? 1 : 0) + "\t" + n.Id + "\t" + st[k] + "\t" + (n.Name ?? ""));
            }
        int noLane = 0;
        for (int k = 0; k < nodes.Count; k++) if (!nodes[k].Game && st[k] != 1 && st[k] != 11) noLane++;
        Console.WriteLine("wioski ukladu bez rysowanej drogi (kazdy powod): " + noLane);
        using (var w = new StreamWriter(Path.Combine(wyj, "obst.tsv")))
            foreach (var o in obst) w.WriteLine(o.X.ToString("0.###", inv) + "\t" + o.Y.ToString("0.###", inv));
        Console.WriteLine("wynik: " + Path.GetFullPath(wyj));
        return 0;
    }
}
