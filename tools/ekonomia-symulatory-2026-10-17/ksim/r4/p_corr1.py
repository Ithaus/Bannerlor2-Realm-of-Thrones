import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from edlib import rep
H = os.path.dirname(os.path.abspath(__file__))
P = r'C:/Users/GAME/AppData/Local/Temp/claude/C--Users-GAME-Bannerlor2-Realm-of-Thrones/ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3/scratchpad/zb/n-ks/Armoury/src/StartCalibration.Corridors.cs'
s = open(P, encoding='utf-8', newline='').read()
a = s.index('        /// <summary>KROK 5 na rownowadze (xs, ceny p)')
e0 = '            try { tgraw = TownPerish(x, p, prod); } catch (Exception te) { tgraw = null; x.TownPerishDone = false; R.Kraina.Add("blad przewozu miast " + te.GetType().Name); }\n'
b = s.index(e0) + len(e0)
new = open(os.path.join(H, 'corr_step.cs.txt'), encoding='utf-8').read()
s = s[:a] + new + s[b:]
open(P, 'w', encoding='utf-8', newline='').write(s)
pairs = []
pairs.append(("""            R.GradDMax = dmax; R.GradMax = gmax; R.GradDBig = dbig;
            // regula zasiegu psujacych sie
            if (inp.Reach) ReachRule(x, prod);
            R.CorrMs += sw.Elapsed.TotalMilliseconds;""", """            R.GradDMax = dmax; R.GradMax = gmax; R.GradDBig = dbig;
            // regula zasiegu psujacych sie - przed krokiem 8 (Solve, recenzja runda 3)
            R.CorrMs += sw.Elapsed.TotalMilliseconds;"""))
pairs.append(("""        internal const double CorrTauLand = 0.010, CorrTauSea = 0.005, CorrMargin = 0.03, CorrMaxG = 1.5, CorrMinG = 0.6, CorrDurableDays = 30.0, CorrPerishDays = 7.0;""",
"""        // recenzja runda 3 (wazne): przewoz ladem drogi, morzem tani - woz 2.5% wartosci na dobe, statek 0.25% na dobe (dotad 1.0% / 0.5%: morze tylko 2 x taniej niz woz;
        // w sredniowieczu woda 10-20 x taniej). Te same stale co KS-H (KsFoodCarts). Kasa importera: 15% kasy 28 dob na dobe (jak KS-H)
        internal const double CorrTauLand = 0.025, CorrTauSea = 0.0025, CorrMargin = 0.03, CorrMaxG = 1.5, CorrMinG = 0.6, CorrDurableDays = 30.0, CorrPerishDays = 7.0, CorrCofferDay = 0.15;"""))
rep(P, pairs)
