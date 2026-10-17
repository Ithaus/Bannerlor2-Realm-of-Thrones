import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from edlib import rep
P = r'C:/Users/GAME/AppData/Local/Temp/claude/C--Users-GAME-Bannerlor2-Realm-of-Thrones/ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3/scratchpad/zb/n-ks/Armoury/src/StartCalibration.Zc4.cs'
pairs = []
pairs.append(("""            public readonly int[] Cnt = new int[ZN];
            public readonly double[] QC = new double[3 * ZN];
        }""", """            public readonly int[] Cnt = new int[ZN];
            public readonly double[] QC = new double[3 * ZN];
            // recenzja runda 3 (wazne, H2): liczba wierszy polki, przy ktorej brak towaru (-1) byl sprawdzony - nowy wiersz polki (towar przyjechal wozem wsi, karawana, od pana,
            // z pol mieszczan, z ZC5) = brak sprawdzany od nowa; dotad -1 z mapy doby ufany do jutra, a przy zakupach co 2. doba - do 2 dob
            public readonly int[] IxCnt = new int[ZN];
        }"""))
pairs.append(("""                    for (int z = 0; z < ZN; z++) zt.Ix[z] = -1;
                    if (ro != null)
                        for (int k = 0; k < ro.Count; k++)
                        {
                            var el = ro.GetElementCopyAtIndex(k); var ei = el.EquipmentElement.Item; int zi;
                            if (ei == null || el.EquipmentElement.ItemModifier != null || !_zIx.TryGetValue(ei, out zi) || zt.Ix[zi] >= 0) continue;
                            zt.Ix[zi] = k; cnt[zi] = el.Amount;
                        }""", """                    for (int z = 0; z < ZN; z++) zt.Ix[z] = -1;
                    if (ro != null)
                        for (int k = 0; k < ro.Count; k++)
                        {
                            var el = ro.GetElementCopyAtIndex(k); var ei = el.EquipmentElement.Item; int zi;
                            if (ei == null || el.EquipmentElement.ItemModifier != null || !_zIx.TryGetValue(ei, out zi) || zt.Ix[zi] >= 0) continue;
                            zt.Ix[zi] = k; cnt[zi] = el.Amount;
                        }
                    { int rc = ro != null ? ro.Count : 0; for (int z = 0; z < ZN; z++) zt.IxCnt[z] = rc; }"""))
# P3: real offtake only for food, uncensored on stockout
pairs.append(("""                    if (SwRealOfftake) for (int z = 0; z < ZN; z++) zt.One[z] = ZAdd(z) ? 0.0 : ZOne28(r.St, z);
                    else
                    {
                        double pp; mkPeople.TryGetValue(r.St, out pp);
                        double sh = world > 0 ? pp / world : 0.0;
                        for (int z = 0; z < ZN; z++) zt.One[z] = one[z] * sh;
                    }""", """                    {
                        double pp; mkPeople.TryGetValue(r.St, out pp);
                        double sh = world > 0 ? pp / world : 0.0;
                        // recenzja runda 3 (drobne): prawdziwy odbior tylko dla jedzenia (material budow i narzedzia - 1:1 swiata z budowami, jak dotad); przy pustym straganie
                        // (<= 5 szt.) odbior panow 0 nie obniza popytu ceny - najwyzej 1:1 swiata x udzial ludzi (popyt nie ocenzurowany brakiem)
                        for (int z = 0; z < ZN; z++)
                        {
                            if (ZAdd(z)) { zt.One[z] = 0.0; continue; }
                            double w1 = one[z] * sh;
                            if (SwRealOfftake && ZFood(z)) { double o = ZOne28(r.St, z); zt.One[z] = cnt[z] <= 5 ? Math.Max(o, w1) : o; }
                            else zt.One[z] = w1;
                        }
                    }"""))
# H2 rows
pairs.append(("""        /// <summary>TownFolk.Zc4BuyVillage (koszt): indeks wiersza towaru koszyka z na polce miasta z doby (Zc4Build) albo -1 (wolajacy sprawdza wiersz i szuka sam).</summary>
        internal static int ZRow(Town t, int z)
        {
            ZTown zt;
            return z >= 0 && z < ZN && ZTownOf(t, out zt) ? zt.Ix[z] : -2;   // KS-R3 H2: -1 - towaru nie bylo na polce przy budowie mapy (wolajacy ufa), -2 - miasto poza mapa (szuka sam)
        }""", """        /// <summary>TownFolk.Zc4BuyVillage (koszt): indeks wiersza towaru koszyka z na polce miasta z doby (Zc4Build) albo -1 (wolajacy sprawdza wiersz i szuka sam).
        /// Recenzja runda 3 (H2): -1 tylko, gdy polka ma tyle samo wierszy, co przy ostatnim sprawdzeniu braku (count) - nowy wiersz (towar przyjechal) = -2 (szukaj).</summary>
        internal static int ZRow(Town t, int z, int count = -1)
        {
            ZTown zt;
            if (!(z >= 0 && z < ZN && ZTownOf(t, out zt))) return -2;   // -2 - miasto poza mapa (szuka sam)
            int ix = zt.Ix[z];
            if (ix == -1 && count >= 0 && count != zt.IxCnt[z]) return -2;
            return ix;
        }

        /// <summary>Recenzja runda 3 (H2): wynik szukania wiersza towaru z na polce miasta (ix albo -1) przy count wierszy polki - nastepne wsie tej doby bez szukania.</summary>
        internal static void ZRowSet(Town t, int z, int ix, int count)
        {
            ZTown zt;
            if (!(z >= 0 && z < ZN && ZTownOf(t, out zt))) return;
            zt.Ix[z] = ix; zt.IxCnt[z] = count;
        }

        /// <summary>Recenzja runda 3: towar koszyka ZC4 jest jedzeniem (szczebel drabiny jedzenia) - rynek wsi BK.</summary>
        internal static bool ZFoodItem(ItemObject it)
        {
            if (it == null) return false;
            Zc4EnsureMap();
            int z; return _zIx != null && _zIx.TryGetValue(it, out z) && ZFood(z);
        }

        // recenzja runda 3 (wazne): rynek wsi BK (BkSeal34) u wsi z koszykiem ZC4 - zl i wsie doby wedlug rynku (linia ZC4, NKS-29)
        private static readonly double[] _bkVm = new double[2]; private static readonly HashSet<Village> _bkVv = new HashSet<Village>();
        private static readonly int[] _bkVn = new int[2];
        internal static void NoteBkVillageMarket(Village v, long zl)
        {
            if (v == null || zl <= 0) return;
            int m = MarketOfSettlement(v.Settlement); if (m < 0 || m > 1) return;
            _bkVm[m] += zl; if (_bkVv.Add(v)) _bkVn[m]++;
        }

        /// <summary>Recenzja runda 3 (wazne): wies z koszykiem ZC4 w stanie zastosowanym - budzet koszyka Planu na dobe (zl, ceny startu x cena krainy; rynek wsi BK
        /// tylko z kiesy ponad 7 dob tego budzetu, bez jedzenia).</summary>
        internal static bool Zc4BasketBudget(Village v, out double budgetDay)
        {
            budgetDay = 0;
            double lam; if (Zc4VillageNeed(v, out lam) == null) return false;
            Zc4VillagePaid(v, out budgetDay);
            return true;
        }"""))
# land-priced budget
pairs.append(("""            int m = MarketOfSettlement(v.Settlement); if (m < 0) m = 0;
            Zc4EnsureMap();
            for (int z = 0; z < ZN && z < q.Length; z++)
            {
                var it = _zItems[z]; if (it == null || !(q[z] > 0)) continue;
                budgetDay += q[z] * Math.Max(1, it.Value) * KsCore.StartIndex(ZIds[z], _plan != null ? _plan.P : null, m);
            }
            return q;""", """            int m = MarketOfSettlement(v.Settlement); if (m < 0) m = 0;
            Zc4EnsureMap();
            // recenzja runda 3 (wazne): budzet przy cenie krainy (cena startu x gradient krainy miasta targowego - jak koszyk kroku 8 Planu)
            Settlement mk = null; try { mk = TradeTownOf(v); } catch { mk = null; }
            for (int z = 0; z < ZN && z < q.Length; z++)
            {
                var it = _zItems[z]; if (it == null || !(q[z] > 0)) continue;
                budgetDay += q[z] * Math.Max(1, it.Value) * KsCore.StartIndex(ZIds[z], _plan != null ? _plan.P : null, m) * LandGradZ(mk, z);
            }
            return q;"""))
# ZC4 line: pass1 bought value + BK market
pairs.append(("""            Array.Clear(_p1, 0, _p1.Length); Array.Clear(_p2, 0, _p2.Length); _zvKcalR[0] = _zvKcalR[1] = 0;""",
"""            // recenzja runda 3 (wazne): wartosc kupiona drabiny 1 (koszyk zaplacony z Planu, przy cenach polki) - NKS-13 z drabiny 1, bez drabiny 2
            sb.Append(" | drabina 1 kupiona wartosc: Westeros ").Append(F0(_p1Zl[0])).Append(" zl, Essos ").Append(F0(_p1Zl[1])).Append(" zl");
            sb.Append(" | rynek wsi BK (wsie z koszykiem ZC4: bez jedzenia, z kiesy ponad 7 dob budzetu Planu): Westeros ").Append(F0(_bkVm[0])).Append(" zl w ").Append(_bkVn[0]).Append(" wsiach, Essos ").Append(F0(_bkVm[1])).Append(" zl w ").Append(_bkVn[1]).Append(" wsiach");
            Array.Clear(_p1, 0, _p1.Length); Array.Clear(_p2, 0, _p2.Length); _zvKcalR[0] = _zvKcalR[1] = 0; _p1Zl[0] = _p1Zl[1] = 0; _bkVm[0] = _bkVm[1] = 0; _bkVn[0] = _bkVn[1] = 0; _bkVv.Clear();"""))
pairs.append(("""        internal static void Zc4NotePass1(double want, double noGoods, double noGold) { _p1[0] += want; _p1[1] += noGoods; _p1[2] += noGold; }""",
"""        internal static void Zc4NotePass1(double want, double noGoods, double noGold) { _p1[0] += want; _p1[1] += noGoods; _p1[2] += noGold; }
        private static readonly double[] _p1Zl = new double[2];
        /// <summary>Recenzja runda 3: zl wydane przez wies w drabinie 1 (koszyk zaplacony z Planu) - linia ZC4, NKS-13.</summary>
        internal static void Zc4NotePass1Zl(Settlement mk, double zl) { if (zl <= 0) return; int m = MarketOfSettlement(mk); if (m == 0 || m == 1) _p1Zl[m] += zl; }"""))
rep(P, pairs)
