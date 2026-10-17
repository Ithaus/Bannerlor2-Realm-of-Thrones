import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from edlib import rep
P = r'C:/Users/GAME/AppData/Local/Temp/claude/C--Users-GAME-Bannerlor2-Realm-of-Thrones/ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3/scratchpad/zb/n-ks/Armoury/src/StartCalibration.Core.cs'
pairs = []
# Solve: no separate rerun - table inside MoneyStep
pairs.append(("""                // krok 8: pieniadze - popyt tylko zaplacony, praca domowa wsi, ktorych nie stac na koszyk (projekt rozdz. 9, 11.1)
                if (inp.Money) { MoneyStep(x, ref p, ref xs, ref S, cancel, inp.MoneyIter); if (cancel != null && cancel()) { R.Error = "przerwane"; return R; } }
                // KS-R3 (port symulatora 17.10, runda 3) krok 8d: lepszy stol po pieniadzach - ubocze ponad popyt zaplacony targu zostaje na stole okregu (klucz Ks Alpha Net).
                // Recenzja runda 3 (wazne): jedno liczenie - lepszy stol (potrzeba i wlasny stol w gore o to samo, koszyk targu bez zmian), BEZ zaliczenia kcal do pracy domowej;
                // potem krok 8 jeszcze raz od potrzeb przed krokiem 8 + stol 8d - sprzedaz wsi (budzet) bez tego, co zostaje na stole, i czesc zaplacona z tej sprzedazy
                Dictionary<KsDistrict, double[]> tableAdd = null;
                if (inp.Money && inp.HomeSub)
                {
                    double[] tkg = { 0, 0 }; int[] tn = { 0, 0 };
                    for (int tr = 0; tr < Math.Max(1, TableRounds); tr++)
                    {
                        var add = ByproductTable(x, p, xs, S);
                        if (add == null || add.Count == 0 || !R4TableRerun) break;
                        // stol kolejnej rundy dodaje sie do poprzedniego (krok 8 od potrzeb przed krokiem 8 + caly stol)
                        if (tableAdd == null) tableAdd = add;
                        else foreach (var kv in add) { double[] a0; if (!tableAdd.TryGetValue(kv.Key, out a0)) tableAdd[kv.Key] = kv.Value; else for (int k = 0; k < KsTab.NN; k++) a0[k] += kv.Value[k]; }
                        for (int m = 0; m < NM; m++) { tkg[m] += R.Money.TableKg[m]; tn[m] = Math.Max(tn[m], R.Money.TableN[m]); }
                        RestoreNeeds(x, needSnap, tableAdd);
                        MoneyStep(x, ref p, ref xs, ref S, cancel, MoneyIterAgain);
                        if (cancel != null && cancel()) { R.Error = "przerwane"; return R; }
                        if (R.Money != null) { R.Money.TableKg = (double[])tkg.Clone(); R.Money.TableN = (int[])tn.Clone(); }
                    }
                }""",
"""                // krok 8: pieniadze - popyt tylko zaplacony, praca domowa wsi, ktorych nie stac na koszyk (projekt rozdz. 9, 11.1). KS-R3 krok 8d (lepszy stol po pieniadzach,
                // klucz Ks Alpha Net) - w punkcie stalym kroku 8 (recenzja runda 3: jedno liczenie - potrzeba i wlasny stol w gore o to samo, bez zaliczenia do pracy domowej;
                // kolejne rundy kroku 8 licza sprzedaz wsi bez tego, co zostaje na stole; osobny ponowny krok 8 gubil podzial pracy domowej wsi na wlasna i poprawke rynku -
                // Essos winogrona i pomarancze 0.6-1.7 w harnessie)
                x.TableAdd = null;
                if (inp.Money) { MoneyStep(x, ref p, ref xs, ref S, cancel, inp.MoneyIter, inp.HomeSub && R4TableRerun); if (cancel != null && cancel()) { R.Error = "przerwane"; return R; } }
                var tableAdd = x.TableAdd;"""))
pairs.append(("""                            var tk = R.Money; RestoreNeeds(x, needSnap, tableAdd);
                            MoneyStep(x, ref p, ref xs, ref S, cancel, inp.MoneyIter);
                            if (R.Money != null && tk != null) { R.Money.TableKg = tk.TableKg; R.Money.TableN = tk.TableN; }""",
"""                            var tk = R.Money; RestoreNeeds(x, needSnap, tableAdd);
                            MoneyStep(x, ref p, ref xs, ref S, cancel, inp.MoneyIter, false);
                            if (R.Money != null && tk != null) { R.Money.TableKg = tk.TableKg; R.Money.TableN = tk.TableN; }"""))
pairs.append(("""        private static void MoneyStep(Ctx x, ref double[] p, ref double[] xs, ref double[] S, Func<bool> cancel, int iters)""",
"""        private static void MoneyStep(Ctx x, ref double[] p, ref double[] xs, ref double[] S, Func<bool> cancel, int iters, bool table)"""))
pairs.append(("""            for (int it = 0; it <= iters; it++)
            {
                bool last = it == iters;
                for (int m = 0; m < NM; m++)
                {
                    for (int z = 0; z < NZ; z++) pz[m * NZ + z] = ZlPerKg(inp, KsBasket.Ids[z], StartIndex(KsBasket.Ids[z], p, m));""",
"""            // krok 8d w punkcie stalym: od tej rundy (ubocze nad popytem zaplaconym targu ostatniej rownowagi zostaje na stole okregu), najwyzej TableRounds razy co 2 rundy
            int tableFrom = Math.Max(1, iters - 2 * Math.Max(1, TableRounds) - 2), tableDone = 0;
            for (int it = 0; it <= iters; it++)
            {
                bool last = it == iters;
                if (table && !last && it >= tableFrom && tableDone < Math.Max(1, TableRounds) && ((it - tableFrom) % 2) == 0)
                {
                    R.Eaten = (double[])eatPrev.Clone();
                    var add = ByproductTable(x, p, xs, S);
                    tableDone++;
                    if (add != null)
                    {
                        if (x.TableAdd == null) x.TableAdd = new Dictionary<KsDistrict, double[]>();
                        foreach (var kv in add) { double[] a0; if (!x.TableAdd.TryGetValue(kv.Key, out a0)) x.TableAdd[kv.Key] = (double[])kv.Value.Clone(); else for (int k = 0; k < KsTab.NN; k++) a0[k] += kv.Value[k]; }
                    }
                }
                for (int m = 0; m < NM; m++)
                {
                    for (int z = 0; z < NZ; z++) pz[m * NZ + z] = ZlPerKg(inp, KsBasket.Ids[z], StartIndex(KsBasket.Ids[z], p, m));"""))
pairs.append(("""        private sealed partial class Ctx
        {
            public KsInput In; public KsResult R;""", """        private sealed partial class Ctx
        {
            public Dictionary<KsDistrict, double[]> TableAdd;          // recenzja runda 3: krok 8d - dodatki lepszego stolu do potrzeb (druga runda gradientu od potrzeb + stol)
            public KsInput In; public KsResult R;"""))
rep(P, pairs)
