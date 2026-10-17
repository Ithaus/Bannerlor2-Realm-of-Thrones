import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from edlib import rep
P = r'C:/Users/GAME/AppData/Local/Temp/claude/C--Users-GAME-Bannerlor2-Realm-of-Thrones/ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3/scratchpad/zb/n-ks/Armoury/src/TownFolk.Ks.cs'
pairs = []
pairs.append(("""            int ix = StartCalibration.ZRow(b.T, z);
            if (ix == -1 && StartCalibration.SwBuyEvery2) return 0;
            if (ix < 0 || ix >= b.R.Count || !ReferenceEquals(b.R.GetElementCopyAtIndex(ix).EquipmentElement.Item, it) || b.R.GetElementCopyAtIndex(ix).EquipmentElement.ItemModifier != null) ix = b.R.FindIndexOfElement(e);
            if (ix < 0) return 0;""", """            // recenzja runda 3 (wazne, H2): -1 tylko przy tej samej liczbie wierszy polki co przy ostatnim sprawdzeniu braku (nowy wiersz - towar przyjechal - szukany);
            // wynik szukania zapisany w mapie doby (nastepne wsie bez szukania)
            int rc = b.R.Count;
            int ix = StartCalibration.ZRow(b.T, z, rc);
            if (ix == -1 && StartCalibration.SwBuyEvery2) return 0;
            if (ix < 0 || ix >= rc || !ReferenceEquals(b.R.GetElementCopyAtIndex(ix).EquipmentElement.Item, it) || b.R.GetElementCopyAtIndex(ix).EquipmentElement.ItemModifier != null)
            {
                ix = b.R.FindIndexOfElement(e);
                StartCalibration.ZRowSet(b.T, z, ix, rc);
            }
            if (ix < 0) return 0;"""))
pairs.append(("""            b.Spent += paid; b.Pcs += k; b.Got += k; b.Rg[rung] += k;
            b.KcalG += k * StartCalibration.ZKg(z) * KsBasket.Kcal[z]; b.ZlG += paid;
            b.D.VBuy += paid; b.D.VPieces += k;
            NoteVillBought(b.Mk, e.Item.ItemCategory, paid);
            return k;
        }""", """            b.Spent += paid; b.Pcs += k; b.Got += k; b.Rg[rung] += k;
            b.KcalG += k * StartCalibration.ZKg(z) * KsBasket.Kcal[z]; b.ZlG += paid;
            b.D.VBuy += paid; b.D.VPieces += k;
            if (b.P1) StartCalibration.Zc4NotePass1Zl(b.Mk, paid);        // recenzja runda 3: wartosc kupiona drabiny 1 (NKS-13)
            NoteVillBought(b.Mk, e.Item.ItemCategory, paid);
            return k;
        }"""))
pairs.append(("""            // KS-R3 (port symulatora 17.10, runda 2): drabina 2 w miastach obok (F2) - kcal i wartosc chciane juz policzone w miescie targowym (bez drugiego liczenia)
            public bool Alt;""", """            // KS-R3 (port symulatora 17.10, runda 2): drabina 2 w miastach obok (F2) - kcal i wartosc chciane juz policzone w miescie targowym (bez drugiego liczenia)
            public bool Alt;
            public bool P1;                                              // recenzja runda 3: drabina 1 koszyka zaplaconego z Planu (wartosc kupiona do NKS-13)"""))
pairs.append(("""            if (price <= 0 || have <= 0) { b.P2Shelf += n; return 0; }
            int above = (int)Math.Floor(have + ShelfKeep - StartCalibration.ZPass2Floor(b.T, z));   // have = polka - 5; nad 1.5 x celem polki miasta (KS-R3 runda 3)""",
"""            if (price <= 0 || have <= 0) { b.P2Shelf += n; return 0; }
            // recenzja runda 3 (drobne): drabine 2 racjonuje CENA - wies z oszczednosciami kupuje ponad plan, dopoki cena polki dzis jest najwyzej 0.8 x ceny startu (krainy)
            // i najwyzej do polki, przy ktorej cena doszlaby do 0.8 x startu (dni polki przy indeksie 0.8 x startu x popyt zaplacony - ok. 1.45 x cel); bez stalego pasa dla kupcow
            if (price > StartCalibration.ZPass2Price(b.T, z)) { b.P2Shelf += n; return 0; }
            int above = (int)Math.Floor(have + ShelfKeep - StartCalibration.ZPass2Floor(b.T, z));   // have = polka - 5"""))
pairs.append(("""            ZcLadder(b, vil, mk, n, resGS);
            if (qp != null)
            {""", """            b.P1 = qp != null;
            ZcLadder(b, vil, mk, n, resGS);
            b.P1 = false;
            if (qp != null)
            {"""))
rep(P, pairs)
