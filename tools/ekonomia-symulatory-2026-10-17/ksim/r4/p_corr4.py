import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from edlib import rep
P = r'C:/Users/GAME/AppData/Local/Temp/claude/C--Users-GAME-Bannerlor2-Realm-of-Thrones/ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3/scratchpad/zb/n-ks/Armoury/src/StartCalibration.Corridors.cs'
pairs = []
pairs.append(("""              .Append(", przepustowosc ").Append(x.In.AfterD ? "po D (1.2 x dowoz KS-H, najmniej 20 szt./d)" : "przed D (polowa kasy 28 dob importera na towar w drodze)").Append(", rund kroku 5 ").Append(R.CorrRounds)""",
"""              .Append(", przepustowosc ").Append(x.In.AfterD ? "po D (1.2 x dowoz KS-H, najmniej 20 szt./d)" : "przed D (budzet kasy importera: 15% kasy 28 dob na dobe, polowa kasy na towar w drodze, dzielony wedlug wartosci braku)")
              .Append(", popyt krain zaplacony (po kroku 8), tau lad ").Append(CorrTauLand.ToString("0.000", inv)).Append(" morze ").Append(CorrTauSea.ToString("0.0000", inv)).Append("/d").Append(", rund kroku 5 ").Append(R.CorrRounds)"""))
pairs.append(("""            if (big.Count > 80) sb.Append(", inne ").Append(big.Count - 80);""",
"""            if (big.Count > 80) sb.Append(", inne ").Append(big.Count - 80);
            // recenzja runda 3 (NKS-24): po D przewoz bez sufitu dowozu KS-H (budzet kasy importera) - wzorzec do porownania z dowozem
            if (x.In.AfterD)
            {
                sb.Append(" | korytarze bez sufitu dowozu >= 20 szt./d:");
                var fb = R.LandFlowFree.FindAll(k => k.Pcs >= 20.0);
                fb.Sort((a, b) => b.Pcs.CompareTo(a.Pcs));
                if (fb.Count == 0) sb.Append(" -");
                for (int i = 0; i < fb.Count && i < 80; i++)
                {
                    var k = fb[i];
                    sb.Append(i == 0 ? " " : ", ").Append(k.From).Append('>').Append(k.To).Append(' ').Append(KsTab.Goods[k.G]).Append(' ').Append(k.Pcs.ToString("0", inv)).Append(" (").Append(k.Days.ToString("0", inv)).Append(k.Sea ? " d morzem)" : " d)");
                }
            }"""))
rep(P, pairs)
P2 = r'C:/Users/GAME/AppData/Local/Temp/claude/C--Users-GAME-Bannerlor2-Realm-of-Thrones/ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3/scratchpad/zb/n-ks/Armoury/src/StartCalibration.Core.cs'
rep(P2, [("""        public List<KsFlow> LandFlow = new List<KsFlow>();""", """        public List<KsFlow> LandFlow = new List<KsFlow>();
        public List<KsFlow> LandFlowFree = new List<KsFlow>();       // recenzja runda 3 (NKS-24): po D przewoz bez sufitu dowozu KS-H (budzet kasy importera)""")])
