using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Armoury;

// port5 (r4-kupcy H8): rdzen KsFoodCore na stanach doby z port4 (snap_*.json). Tryb "off" - wszystkie nowe flagi wylaczone (porownanie bit w bit ze stara wersja: lista
// lotow do pliku), tryb "on" (tylko nowa wersja) - flagi H2/H3/H4/H5/ByNeed/Famine (informacyjnie: wolumen, zablokowane).
// Uzycie (z katalogu tego pliku): dotnet build -c Release -v q --nologo, potem bin/Release/net8.0/p.exe <snap> <wyjscie.txt> <tryb>;
// <snap> = D:/Backup-Bannerlord/scratch-2026-10-17/kmoney/port4/snap (9 stanow port4). Tryby:
//  off     - flagi rundy 4 wyl. (wzorzec: ten sam program zbudowany bez NEWCORE na KsFoodCore.cs z efbbf0dc - 1251 lotow, bajt w bajt);
//  lazy    - jak off, indeks ceny na zadanie (T9) - bajt w bajt z off;
//  on      - flagi wl. (H2, H3, H4 z j0, H5 przy miejscu kredytu 0.5 x kasa, kolejka wedlug potrzeby, glod) - porownanie z portem Pythona r5core.py (lot w lot);
//  onold   - jak on, ale H4 bez j0 (przed recenzja) + licznik lotow 2. i 3. doby z rachunkiem ponizej kosztu + frachtu;
//  ontight - jak on, miejsce kredytu 0.02 x kasa i co 7. cel z zaleglym (galezie H5) - porownanie z r5core.py --tight (linie "ovd" tez).
static class Program
{
    static double[] Flat(JsonElement a) { var l = new List<double>(); foreach (var r in a.EnumerateArray()) foreach (var x in r.EnumerateArray()) l.Add(x.ValueKind == JsonValueKind.True ? 1 : x.ValueKind == JsonValueKind.False ? 0 : x.GetDouble()); return l.ToArray(); }

    static bool shipped; static long ixMiss; static double decMs; static long laterLots, lossLots, lossGold;
    static int Main(string[] args)
    {
        string mode = args.Length > 2 ? args[2] : "off";
        var files = Directory.GetFiles(args[0], "snap_*.json").OrderBy(f => f).ToList();
        var outp = new StringBuilder();
        long totPcs = 0, totLots = 0, totOrd = 0, totPoor = 0, totSrc = 0, totNoP = 0, totFar = 0; int famReq = 0, famReqK = 0; long famOrd = 0, famGot = 0;
        var byK = new long[16]; var poorK = new long[16];
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
                Sh = sh, Inc = Flat(j.GetProperty("inc")), Ix = (double[])ix.Clone(), Dp = Flat(j.GetProperty("dp")), Off = Flat(j.GetProperty("off")), Ts = Flat(j.GetProperty("ts")),
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
            bool onold = mode == "onold", tight = mode == "ontight"; bool on = mode == "on" || onold || tight; shipped = false;
            var sentTK = new Dictionary<long, long>();
#if NEWCORE
            if (on)
            {
                a.FarAlways = true; a.RelKeep = true; a.ByNeed = true; a.Famine = true; a.Consign = true;
                a.SrcRoom = gold.Select(g => Math.Max(0.0, (tight ? 0.02 : 0.5) * g)).ToArray();
                a.DstOverdue = new bool[nt];
                if (tight) for (int i = 0; i < nt; i++) a.DstOverdue[i] = i % 7 == 3;   // r5core: galaz miejsca kredytu i zaleglego celu
                a.FamineGood = new bool[nk]; for (int k = 0; k < Math.Min(4, nk); k++) a.FamineGood[k] = true;
                a.DestValue = (t, k, j0, n) =>
                {
                    int c = cat[k]; double V0 = v0(t, c), D = catD[t * nc + c], S = catS[t * nc + c], v = val[k];
                    double p0 = v * index(D, S, V0), A0 = 0.1 * S + 0.04 * V0 + c0, cc = 0.04 * v, cap = 3.0 * startIdx[mk[t] * nk + k] * v;
                    double sum = KsFoodCore.DestIntegralFrom(p0, A0, cc, cap, onold ? 0 : j0, n);
                    return (long)Math.Floor(sum * (1.0 - KsFoodCore.Margin));
                };
                // T9: indeks na zadanie (NaN) - ten sam wynik
                var full = (double[])a.Ix.Clone();
                for (int i = 0; i < a.Ix.Length; i++) a.Ix[i] = double.NaN;
                a.IxAt = (t, k) => { if (shipped) ixMiss++; return full[t * nk + k]; };
            }
            else if (mode == "lazy")
            {
                var full = (double[])a.Ix.Clone();
                for (int i = 0; i < a.Ix.Length; i++) a.Ix[i] = double.NaN;
                a.IxAt = (t, k) => { if (shipped) ixMiss++; return full[t * nk + k]; };
            }
#endif
            var lots = new List<long[]>();
            a.Ship = (s, t, k, n, cost, fr, sea0, L) =>
            {
                shipped = true;
                if (!on && gold[t] < cost + fr) return 0;
                if (on && gold[s] < fr) return 0;
                int take = Math.Min(n, (int)Math.Max(0.0, shf[s * nk + k]) - 5);
                if (take <= 0) return 0;
                if (take < n) { cost = (long)Math.Round(cost * (double)take / n); fr = (long)Math.Round(fr * (double)take / n); n = take; }
                shf[s * nk + k] -= n;
                if (!on) { gold[t] -= cost + fr; gold[s] += cost; } else gold[s] -= fr;
                lots.Add(new long[] { s, t, k, n, cost, fr });
                if (on)
                {
                    long key = (long)t * 1000 + k; long j0; sentTK.TryGetValue(key, out j0);
                    int c = cat[k]; double V0 = v0(t, c), D = catD[t * nc + c], S = catS[t * nc + c], v = val[k];
                    double p0 = v * index(D, S, V0), A0 = 0.1 * S + 0.04 * V0 + c0, cc = 0.04 * v, cap = 3.0 * startIdx[mk[t] * nk + k] * v;
                    long bill = (long)Math.Floor(KsFoodCore.DestIntegralFrom(p0, A0, cc, cap, (int)j0, n) * (1.0 - KsFoodCore.Margin));
                    if (j0 > 0) { laterLots++; if (bill < cost + fr) { lossLots++; lossGold += cost + fr - bill; } }
                    sentTK[key] = j0 + n;
                }
                return n;
            };
            var st = new KsFoodCore.Stats();
            var swd = System.Diagnostics.Stopwatch.StartNew();
            for (int rep = 0; rep < (mode == "time" || mode == "timeon" ? 0 : 0); rep++) { }
            KsFoodCore.Decide(a, st);
            decMs += swd.Elapsed.TotalMilliseconds;
            foreach (var x in lots) { outp.Append(day).Append(' ').Append(string.Join(" ", x)).Append('\n'); totPcs += x[3]; byK[x[2]] += x[3]; }
            if (tight) outp.Append("ovd ").Append(day).Append(' ').Append(st.Overdue).Append(' ').Append(st.SrcCap).Append((char)10);
            outp.Append("stats ").Append(day).Append(' ').Append(st.Ordered).Append(' ').Append(st.Poor).Append(' ').Append(st.NoProfit).Append(' ').Append(st.Far).Append(' ').Append(st.ResLots).Append(' ').Append(st.ResGold)
                .Append(' ').Append(string.Join(",", st.CofShort.Select(v => v.ToString("R", CultureInfo.InvariantCulture)))).Append(" kx ").Append(string.Join(",", a.Kx.Select(v => v.ToString("R", CultureInfo.InvariantCulture)))).Append('\n');
            totLots += lots.Count; totOrd += st.Ordered; totPoor += st.Poor; totNoP += st.NoProfit; totFar += st.Far;
#if NEWCORE
            totSrc += st.SrcCap; famReq += st.FamineReq; famReqK += st.FamineReqK; famOrd += st.FamineOrd; famGot += st.FamineGot;
            for (int k = 0; k < nk && k < 16; k++) poorK[k] += st.PoorByK[k];
#endif
        }
        File.WriteAllText(args[1], outp.ToString());
        Console.WriteLine($"tryb {mode}: dob {files.Count}, lotow {totLots}, szt. {totPcs}, zamowione {totOrd}, kasa celu za mala {totPoor}, zrodlo bez miejsca {totSrc}, bez zysku {totNoP}, z dalszych {totFar}, glod celow {famReq} (zamowien {famReqK}) zam. {famOrd} wysl. {famGot}, indeks po pierwszym locie {ixMiss}, czas Decide srednio {decMs / Math.Max(1, files.Count):0.00} ms");
        Console.WriteLine($"  loty 2. i 3. doby do (cel, towar): {laterLots}, z rachunkiem (za sztukami wczesniejszych lotow) ponizej kosztu + frachtu: {lossLots} ({lossGold} zl)");
        Console.WriteLine("  szt. wedlug towaru: " + string.Join(" ", byK));
        Console.WriteLine("  zablokowane z braku pieniedzy wedlug towaru: " + string.Join(" ", poorK));
        return 0;
    }
}
