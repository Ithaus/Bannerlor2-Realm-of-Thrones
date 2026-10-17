using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Armoury;

// Test portu KS-H (recenzja rundy 3): rdzen decyzji KsFoodCore (ten sam kod co w grze) na stanach doby z symulatora (ksim/sim/r4port.py portdump=1) - porownanie lotow
// i sztuk z decyzjami symulatora tej samej doby. Cena lotu jak w symulatorze (lot_price: indeks (D / (0.1 S + 0.04 V + c0))^0.6, 3 punkty), cena celu z indeksem doby.
static class Program
{
    static double[] Flat(JsonElement a) { var l = new List<double>(); foreach (var r in a.EnumerateArray()) foreach (var x in r.EnumerateArray()) l.Add(x.ValueKind == JsonValueKind.True ? 1 : x.ValueKind == JsonValueKind.False ? 0 : x.GetDouble()); return l.ToArray(); }

    static int Main(string[] args)
    {
        var files = Directory.GetFiles(args[0], "snap_*.json").OrderBy(f => f).ToList();
        double totSim = 0, totCs = 0; int lotsSim = 0, lotsCs = 0, ndays = 0, worstDay = 0; double worst = 0;
        var byK = new Dictionary<int, double[]>();
        var sw = System.Diagnostics.Stopwatch.StartNew(); double msMax = 0;
        foreach (var f in files)
        {
            var j = JsonDocument.Parse(File.ReadAllText(f)).RootElement;
            int nt = j.GetProperty("nt").GetInt32(), nk = j.GetProperty("nk").GetInt32(), day = j.GetProperty("day").GetInt32();
            var mk = j.GetProperty("mk").EnumerateArray().Select(x => x.GetInt32()).ToArray();
            var sh = Flat(j.GetProperty("sh")); var shf = Flat(j.GetProperty("shf"));
            var dayT = Flat(j.GetProperty("days")); var sea = Flat(j.GetProperty("sea")); var war = Flat(j.GetProperty("war"));
            var val = j.GetProperty("val").EnumerateArray().Select(x => x.GetDouble()).ToArray();
            var cat = j.GetProperty("cat").EnumerateArray().Select(x => x.GetInt32()).ToArray();
            var catD = Flat(j.GetProperty("catD")); var catS = Flat(j.GetProperty("catS")); var catX = Flat(j.GetProperty("catV0x"));
            int nc = catD.Length / nt;
            var startIdx = Flat(j.GetProperty("startidx")); var ix = Flat(j.GetProperty("ix"));
            double c0 = j.GetProperty("c0").GetDouble();
            var gold = j.GetProperty("gold").EnumerateArray().Select(x => x.GetDouble()).ToArray();
            var a = new KsFoodCore.In
            {
                NT = nt, NK = nk, Today = day, Mk = mk, Siege = new bool[nt], Review = j.GetProperty("review").EnumerateArray().Select(x => x.GetBoolean()).ToArray(),
                Sh = sh, Inc = Flat(j.GetProperty("inc")), Ix = ix, Dp = Flat(j.GetProperty("dp")), Off = Flat(j.GetProperty("off")), Ts = Flat(j.GetProperty("ts")),
                Gold = (double[])gold.Clone(), Res = j.GetProperty("res").EnumerateArray().Select(x => x.GetDouble()).ToArray(),
                Kx = Flat(j.GetProperty("kx")).Select(x => (float)x).ToArray(),
                Per = j.GetProperty("per").EnumerateArray().Select(x => x.GetBoolean()).ToArray(), Staple = j.GetProperty("staple").EnumerateArray().Select(x => x.GetBoolean()).ToArray(),
                Cand = j.GetProperty("cand").EnumerateArray().Select(r => r.EnumerateArray().Select(x => x.GetInt32()).ToArray()).ToArray(),
                CandFar = j.GetProperty("candfar").EnumerateArray().Select(r => r.EnumerateArray().Select(x => x.GetInt32()).ToArray()).ToArray()
            };
            a.Days = (s, t) => (float)dayT[s * nt + t];
            a.Sea = (s, t) => sea[s * nt + t] > 0.5;
            a.War = (s, t) => war[s * nt + t] > 0.5;
            Func<double, double, double, double> index = (D, S, V) => { double den = 0.1 * S + 0.04 * V + c0; double x = D > 0 ? Math.Pow(D / den, 0.6) : 0.0; return x < 0.1 ? 0.1 : (x > 10.0 ? 10.0 : x); };
            Func<int, int, double> v0 = (s, c) => { double v = catX[s * nc + c]; for (int k = 0; k < nk; k++) if (cat[k] == c) v += Math.Max(0.0, shf[s * nk + k]) * val[k]; return v; };
            a.LotPrice = (s, k, n) =>
            {
                int c = cat[k]; double V0 = v0(s, c), D = catD[s * nc + c], S = catS[s * nc + c], v = val[k];
                Func<int, long> P = m => Math.Max(1L, (long)Math.Ceiling(v * index(D, S, Math.Max(0.0, V0 - m * v)) - 1e-9));
                if (n < 8) { long t = 0; for (int m = 0; m < n; m++) t += P(m); return t; }
                int m1 = n / 8, m2 = n / 2;
                return m1 * P(0) + (m2 - m1) * P(m1) + (n - m2) * P(m2);
            };
            a.DestUnit = (t, k) =>
            {
                double up = val[k] * ix[t * nk + k];
                if (shf[t * nk + k] <= 5) up = Math.Min(up, 1.5 * startIdx[mk[t] * nk + k] * val[k]);
                return Math.Max(1, (int)Math.Ceiling(up - 1e-9));
            };
            var lots = new List<int[]>();
            a.Ship = (s, t, k, n, cost, fr, sea0, L) =>
            {
                if (gold[t] < cost + fr) return 0;
                int take = Math.Min(n, (int)Math.Max(0.0, shf[s * nk + k]) - 5);
                if (take <= 0) return 0;
                if (take < n) { cost = (long)Math.Round(cost * (double)take / n); fr = (long)Math.Round(fr * (double)take / n); n = take; }
                shf[s * nk + k] -= n; gold[t] -= cost + fr; gold[s] += cost;
                lots.Add(new[] { s, t, k, n, (int)cost, (int)fr });
                return n;
            };
            var st = new KsFoodCore.Stats();
            var t0 = sw.Elapsed.TotalMilliseconds;
            KsFoodCore.Decide(a, st);
            double ms = sw.Elapsed.TotalMilliseconds - t0; if (ms > msMax) msMax = ms;
            var simLots = j.GetProperty("lots").EnumerateArray().Select(r => r.EnumerateArray().Select(x => (int)Math.Round(x.GetDouble())).ToArray()).ToList();
            double ps = simLots.Sum(x => x[3]), pc = lots.Sum(x => x[3]);
            totSim += ps; totCs += pc; lotsSim += simLots.Count; lotsCs += lots.Count; ndays++;
            foreach (var x in simLots) { double[] b; if (!byK.TryGetValue(x[2], out b)) byK[x[2]] = b = new double[2]; b[0] += x[3]; }
            foreach (var x in lots) { double[] b; if (!byK.TryGetValue(x[2], out b)) byK[x[2]] = b = new double[2]; b[1] += x[3]; }
            int same = lots.Count(x => simLots.Any(y => y[0] == x[0] && y[1] == x[1] && y[2] == x[2] && y[3] == x[3] && y[4] == x[4] && y[5] == x[5]));
            double dev = ps > 0 ? Math.Abs(pc - ps) / ps : 0; if (dev > worst) { worst = dev; worstDay = day; }
            Console.WriteLine("doba " + day + ": symulator " + simLots.Count + " lotow " + ps + " szt., C# " + lots.Count + " lotow " + pc + " szt. (" + (ps > 0 ? (100 * (pc - ps) / ps).ToString("+0.0;-0.0", CultureInfo.InvariantCulture) : "-") + "%), lotow identycznych " + same + ", czas " + ms.ToString("0.00", CultureInfo.InvariantCulture) + " ms");
        }
        Console.WriteLine("RAZEM: dob " + ndays + ", symulator " + lotsSim + " lotow " + totSim + " szt., C# " + lotsCs + " lotow " + totCs + " szt. (" + (totSim > 0 ? (100 * (totCs - totSim) / totSim).ToString("+0.00;-0.00", CultureInfo.InvariantCulture) : "-") + "%), najgorsza doba " + worstDay + " " + (100 * worst).ToString("0.0", CultureInfo.InvariantCulture) + "%, czas max " + msMax.ToString("0.00", CultureInfo.InvariantCulture) + " ms");
        foreach (var kv in byK.OrderBy(k => k.Key)) Console.WriteLine("  towar " + kv.Key + ": symulator " + kv.Value[0] + ", C# " + kv.Value[1]);
        return Math.Abs(totCs - totSim) <= 0.03 * Math.Max(1, totSim) ? 0 : 2;
    }
}
