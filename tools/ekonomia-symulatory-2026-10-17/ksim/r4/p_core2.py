import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from edlib import rep
P = r'C:/Users/GAME/AppData/Local/Temp/claude/C--Users-GAME-Bannerlor2-Realm-of-Thrones/ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3/scratchpad/zb/n-ks/Armoury/src/StartCalibration.Core.cs'
pairs = []
pairs.append(("""        /// <summary>Krok 8 (recenzja 17.10, wazne): pozycje koszyka wsi, ktore okreg moze zrobic W DOMU""",
"""        /// <summary>Recenzja runda 3: czynnik ceny krainy l pozycji koszyka z (gradient skuteczny surowca jedzenia - chleb, placek i piwo jak zboze, wino jak winogrona ...);
        /// rzeczy i towary spoza jedzenia - 1.</summary>
        private static double GradZ(Ctx x, int l, int z)
        {
            int g = z >= 0 && z < KsBasket.N ? KsBasket.RawGood[z] : -1;
            if (l < 0 || x.GradEff == null || g < 0 || !FoodMask[g] || l * KsTab.NG + g >= x.GradEff.Length) return 1.0;
            double v = x.GradEff[l * KsTab.NG + g];
            return v > 0 && !double.IsNaN(v) ? v : 1.0;
        }

        /// <summary>Recenzja runda 3 (wazne): PLON towaru zastepczego 8b/8c z rak pracy domowej - y = kg na jednostke pracy domowej (rece zajec okregu bloku po bloku
        /// x P0 zajecia towaru zastepczego x ziemia bloku: zboze - wheat_farm i ziemia zboza, ryby - fisherman, dziczyzna - hunter, daktyle - date_farm), cap = najwyzej
        /// tyle kg na dobe z wolnej ziemi blokow (zboze, daktyle: wolne ha / (ha na pracujacego - ha domu) x P0 x L; ryby i dziczyzna - bez ziemi). Po KsDeliver.Produce.</summary>
        private static void SubPhys(KsDistrict d, KsPart[] parts, int sub, out double y, out double cap)
        {
            y = 0; cap = double.PositiveInfinity;
            int c, g;
            switch (sub)
            {
                case KsBasket.Grain: c = KsTab.CWheat; g = KsTab.GGrain; break;
                case KsBasket.Fish: c = KsTab.CFisher; g = KsTab.GFish; break;
                case KsBasket.Meat: c = KsTab.CHunter; g = KsTab.GWhaleMeat; break;
                case KsBasket.Date: c = KsTab.CDate; g = KsTab.GDate; break;
                default: return;
            }
            double p0 = 0; foreach (var kv in KsTab.P0[c]) if (kv.Key == g) p0 = kv.Value;
            if (!(p0 > 0) || parts == null) return;
            bool land = c == KsTab.CWheat || c == KsTab.CDate;
            double haX = Math.Max(0.1, KsTab.HaPerWorker[c] - KsTab.HaHome), capKg = 0; bool anyArea = false;
            string type = d.Type;
            foreach (var lb in parts)
            {
                if (lb == null || lb.K == null || lb.C == null || lb.X == null) continue;
                double rsum = 0; if (lb.RF != null) for (int j = 0; j < lb.RF.Length; j++) rsum += lb.RF[j];
                double scale = Math.Max(0.0, 1.0 - rsum), rem = Math.Max(0.0, lb.Hands - lb.WH), occ = 0;
                for (int j = 0; j < lb.C.Length; j++)
                {
                    double xj = lb.X[j] * scale; if (!(xj > 0)) continue;
                    double sb = lb.C[j] != KsTab.CWheat && lb.Lg > 0 ? Math.Min(rem, lb.SubNW) : 0.0;
                    occ += xj * (rem - sb);
                }
                double L = c == KsTab.CWheat ? lb.LGrain(type) : lb.L(c, type);
                if (!(L > 0) || !(occ > 0)) continue;
                y += occ * p0 * L;
                if (land && lb.K.Area > 0) { anyArea = true; capKg += Math.Max(0.0, lb.K.Area - lb.LandUse) / haX * p0 * L; }
            }
            if (land && anyArea) cap = capKg;
        }

        /// <summary>Krok 8 (recenzja 17.10, wazne): pozycje koszyka wsi, ktore okreg moze zrobic W DOMU"""))
# --- ByproductTable: meat incl. livestock, return table additions, no DistHomeKcal credit
pairs.append(("""        private static void TableOf(int a, double[] prod, double[] own, out double mk, out double ow)
        {
            if (TableGoods[a] != KsTab.GMeat) { int it = TableItem[a]; mk = Math.Max(0.0, prod[it] - own[it]); ow = own[it]; return; }
            mk = 0; ow = 0;
            foreach (int it in MeatItems) { mk += Math.Max(0.0, prod[it] - own[it]); ow += own[it]; }
        }""",
"""        private static void TableOf(int a, double[] prod, double[] own, out double mk, out double ow)
        {
            if (TableGoods[a] != KsTab.GMeat) { int it = TableItem[a]; mk = Math.Max(0.0, prod[it] - own[it]); ow = own[it]; return; }
            mk = 0; ow = 0;
            foreach (int it in MeatItems) { mk += Math.Max(0.0, prod[it] - own[it]); ow += own[it]; }
            // recenzja runda 3 (krytyczne): mieso na targu to prawie cale ZYWIEC - uboczny towar gospodarstw zbozowych (swinie, owce, krowy przy chacie), stad mlecznych
            // i owczych (harness: dziczyzna i wieloryb ok. 0, zywiec 100% miesa na targ; cena miesa na podlodze, a rece trzyma zboze, ser i welna). Okreg, ktory wiezie
            // zywiec na targ, ubija go na wlasny stol (KsDeliver.Own: mieso - dziczyzna, wieloryb, potem uboj wlasnego zywca) - mieso = polowa zywca
            foreach (int it in LiveItems) { mk += Math.Max(0.0, prod[it] - own[it]) * KsTab.MeatOfLive; ow += own[it] * KsTab.MeatOfLive; }
        }
        private static readonly int[] LiveItems = { KsDeliver.ICow, KsDeliver.IHog, KsDeliver.ISheep };"""))
pairs.append(("""        private static void ByproductTable(Ctx x, double[] p, double[] xs, double[] S)
        {
            var inp = x.In; var R = x.R; var M = R.Money;
            if (!TableOn || M == null || !M.On || inp.Keep == null || R.Eaten == null) return;""",
"""        private static Dictionary<KsDistrict, double[]> ByproductTable(Ctx x, double[] p, double[] xs, double[] S)
        {
            var inp = x.In; var R = x.R; var M = R.Money;
            if (!TableOn || M == null || !M.On || inp.Keep == null || R.Eaten == null) return null;"""))
pairs.append(("""            var mf = R.MarketFactor;
            var credit = new Dictionary<string, double>(StringComparer.Ordinal);""",
"""            var mf = R.MarketFactor;
            var add = new Dictionary<KsDistrict, double[]>();"""))
pairs.append(("""                        // wlasny stol = keep x mnoznik (NeedR8 / Need0): nowy wlasny = dzisiejszy + av
                        double newOwn = ownL[j][a] + av;
                        d.NeedR8[key] = d.Need0[key] * newOwn / kr[j][a];
                        ownL[j][a] = newOwn; mkt[j][a] -= av;
                        absd += av;
                        double c; credit.TryGetValue(d.Id, out c); credit[d.Id] = c + av * KsDeliver.ItemKcal[TableItem[a]];
                    }
                    R.Eaten[i] += absd; x.Dm[i] += absd;
                    M.TableKg[m] += absd;
                }
            // kcal stolu - czesc pokryta w domu (najwyzej niezaplacona czesc koszyka)
            for (int j = 0; j < dl.Count; j++)
            {
                var d = dl[j]; double c;
                if (!credit.TryGetValue(d.Id, out c) || !(c > 0) || !(fkL[j] > 0)) continue;
                float hk; R.DistHomeKcal.TryGetValue(d.Id, out hk);
                double room = Math.Max(0.0, 1.0 - pkL[j] - hk);
                double add = Math.Min(room, c / fkL[j]);
                if (!(add > 0)) continue;
                R.DistHomeKcal[d.Id] = (float)(hk + add);
                int m = d.Market == 0 ? 0 : 1;
                M.VHomeKcal[m] += add * fkL[j]; M.TableKcal[m] += add * fkL[j];
            }
        }""",
"""                        // wlasny stol = keep x mnoznik (NeedR8 / Need0): nowy wlasny = dzisiejszy + av; potrzeba w gore o tyle samo (lepszy stol - koszyk targu bez zmian)
                        double newOwn = ownL[j][a] + av;
                        double need1 = d.Need0[key] * newOwn / kr[j][a];
                        double[] ad; if (!add.TryGetValue(d, out ad)) { ad = new double[KsTab.NN]; add[d] = ad; M.TableN[m]++; }
                        ad[key] += Math.Max(0.0, need1 - d.NeedR8[key]);
                        d.NeedR8[key] = need1;
                        ownL[j][a] = newOwn; mkt[j][a] -= av;
                        absd += av;
                    }
                    M.TableKg[m] += absd;
                }
            // recenzja runda 3 (wazne): bez zaliczenia kcal stolu do pracy domowej (DistHomeKcal) - jaja, miod i mieso stolu sa w potrzebie i we wlasnym stole po obu
            // stronach (lepszy stol), nie pokrywaja niezaplaconego koszyka. Wolajacy (Solve) liczy krok 8 jeszcze raz od potrzeb przed krokiem 8 + te dodatki
            return add;
        }"""))
rep(P, pairs)
