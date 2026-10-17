import os
HERE = os.path.dirname(os.path.abspath(__file__))
p = os.path.join(HERE, 'CatchK.cs'); s = open(p, encoding='utf-8').read()
def rep(a, b):
    global s
    assert s.count(a) == 1, a[:60]
    s = s.replace(a, b)
rep('''                SharesK(x, p, rr, xs, K);
                SupplyK(x, xs, S, Svil, st.Dck);
                Array.Clear(dem, 0, dem.Length);''', '''                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                SharesK(x, p, rr, xs, K);
                long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
                SupplyK(x, xs, S, Svil, st.Dck);
                long t2 = System.Diagnostics.Stopwatch.GetTimestamp();
                Prof[0] += t1 - t0; Prof[1] += t2 - t1;
                Array.Clear(dem, 0, dem.Length);''')
rep('''                Flows(KI, st, outS, p);''', '''                long t3 = System.Diagnostics.Stopwatch.GetTimestamp();
                Flows(KI, st, outS, p);
                long t4 = System.Diagnostics.Stopwatch.GetTimestamp(); Prof[2] += t4 - t3; Prof[4] += t3 - t2;''')
rep('''                if (it % 50 == 49)
                {
                    bool bad = false; double mt = 0;
                    for (int i = 0; i < n; i++)
                    {
                        if (!actL[i]) continue;''', '''                Prof[3] += System.Diagnostics.Stopwatch.GetTimestamp() - t4;
                if (it % 50 == 49)
                {
                    bool bad = false; double mt = 0;
                    for (int i = 0; i < n; i++)
                    {
                        if (!actL[i]) continue;''')
rep('''        private static void PairsK(Ctx x)''', '''        internal static readonly long[] Prof = new long[6];

        private static void PairsK(Ctx x)''')
open(p, 'w', encoding='utf-8', newline='\n').write(s)
p = os.path.join(HERE, 'ProgramK.cs'); s = open(p, encoding='utf-8').read()
a = '''        var O = R.KOut;'''
b = '''        var O = R.KOut;
        { double fq = System.Diagnostics.Stopwatch.Frequency / 1000.0; Console.WriteLine("profil rownowagi ms: udzialy " + (KsCore.Prof[0] / fq).ToString("0") + ", podaz " + (KsCore.Prof[1] / fq).ToString("0") + ", renta " + (KsCore.Prof[4] / fq).ToString("0") + ", korytarze " + (KsCore.Prof[2] / fq).ToString("0") + ", ceny " + (KsCore.Prof[3] / fq).ToString("0") + " | Solve razem " + R.Ms.ToString("0")); }'''
assert s.count(a) == 1; s = s.replace(a, b)
open(p, 'w', encoding='utf-8', newline='\n').write(s)
print('ok')
