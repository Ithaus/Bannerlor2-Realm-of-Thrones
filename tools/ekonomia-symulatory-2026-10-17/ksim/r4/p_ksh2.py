import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from edlib import rep
P = r'C:/Users/GAME/AppData/Local/Temp/claude/C--Users-GAME-Bannerlor2-Realm-of-Thrones/ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3/scratchpad/zb/n-ks/Armoury/src/KsFoodCarts.cs'
pairs = []
# ---------------- Arrivals
pairs.append(("""                    if (!dstOk && !srcOk) { _dLost += c.N; AddInc(c, -1); c.N = 0; done.Add(c); continue; }
                    if (!dstOk || (today > c.DayDue + ReturnDays && srcOk)) { Back(c); done.Add(c); continue; }
                    if (c.Dst.IsUnderSiege) { _dWaitSiege++; continue; }
                    var it = StartCalibration.ZItem(Z[c.Z]);
                    if (it == null) { Back(c); done.Add(c); continue; }
                    var gf = GoodsLedger.Begin(GoodsLedger.FKsH, c.Dst);
                    try { c.Dst.ItemRoster.AddToCounts(it, c.N); }
                    finally { GoodsLedger.End(gf); }
                    StartCalibration.ZRowDirty(c.Dst, it);              // wiersz polki z mapy ZC4 nieaktualny (H2) - zakupy wsi szukaja go same
                    AddInc(c, -1);
                    _dArr += c.N; _dArrCarts++;
                    int g = PlanG[c.Z];
                    if (g >= 0)
                    {
                        string key = StartCalibration.TownLandOf(c.Src) + "|" + StartCalibration.TownLandOf(c.Dst) + "|" + g.ToString(CultureInfo.InvariantCulture);
                        double v; _delDay.TryGetValue(key, out v); _delDay[key] = v + c.N;
                    }
                    c.N = 0;
                    done.Add(c);""",
"""                    if (!dstOk && !srcOk)
                    {
                        // recenzja runda 3 (drobne): towar wozu bez celu i bez zrodla - ujscie w ksiedze towarow (dotad znikal "bez wyjasnienia")
                        GoodsLedger.NoteKsLoss(StartCalibration.ZItem(Z[c.Z]), c.N, "woz bez celu i zrodla");
                        _dLost += c.N; AddInc(c, -1); c.N = 0; done.Add(c); continue;
                    }
                    if (!dstOk || (today > c.DayDue + ReturnDays && srcOk)) { Back(c); done.Add(c); continue; }
                    if (c.Dst.IsUnderSiege) { _dWaitSiege++; continue; }
                    var it = StartCalibration.ZItem(Z[c.Z]);
                    if (it == null) { Back(c); done.Add(c); continue; }
                    AddInc(c, -1);
                    int n = c.N;
                    // recenzja runda 3 (drobne): psucie w drodze - psujace sie traca 1.5% sztuk na dobe drogi (ujscie)
                    if (Per[c.Z] && n > 0)
                    {
                        int dd = Math.Max(1, c.DayDue - c.DayBought);
                        int sp = Math.Min(n, (int)Math.Floor(n * Math.Min(0.5, SpoilPerDay * dd)));
                        if (sp > 0) { GoodsLedger.NoteKsLoss(it, sp, "psucie w drodze"); _dSpoil += sp; n -= sp; }
                    }
                    // recenzja runda 3 (wazne): wynik kupca lotu - wartosc u celu (cena polki celu dzis) wobec zaplaty i frachtu
                    try
                    {
                        int up = DestUnit(c.Dst.Town, c.Z, it);
                        long val = (long)n * Math.Max(0, up), cost = c.Paid + c.Freight;
                        _dMarVal += val; _dMarCost += cost; _dMarLots++; if (val < cost) _dMarLoss++;
                        if (cost > 0) _dMarPct.Add((val - cost) / (double)cost);
                    }
                    catch { }
                    if (n > 0)
                    {
                        var gf = GoodsLedger.Begin(GoodsLedger.FKsH, c.Dst);
                        try { c.Dst.ItemRoster.AddToCounts(it, n); }
                        finally { GoodsLedger.End(gf); }
                        StartCalibration.ZRowDirty(c.Dst, it);          // wiersz polki z mapy ZC4 nieaktualny (H2) - zakupy wsi szukaja go same
                    }
                    _dArr += n; _dArrCarts++;
                    int g = PlanG[c.Z];
                    if (g >= 0)
                    {
                        string key = StartCalibration.TownLandOf(c.Src) + "|" + StartCalibration.TownLandOf(c.Dst) + "|" + g.ToString(CultureInfo.InvariantCulture);
                        double v; _delDay.TryGetValue(key, out v); _delDay[key] = v + n;
                    }
                    c.N = 0;
                    done.Add(c);"""))
# ---------------- Back: goods matching the refund
pairs.append(("""        private static void Back(Cart c)
        {
            AddInc(c, -1);
            var it = StartCalibration.ZItem(Z[c.Z]);
            if (it != null && c.Src != null && c.Src.ItemRoster != null && c.N > 0)
            {
                var gf = GoodsLedger.Begin(GoodsLedger.FKsH, c.Src);
                try { c.Src.ItemRoster.AddToCounts(it, c.N); }
                finally { GoodsLedger.End(gf); }
                _dBackPcs += c.N;
            }
            else _dLost += c.N;
            _dBack++;
            c.N = 0;
            if (c.Src == null || c.Src.Town == null || c.Dst == null || c.Dst.Town == null) return;
            long r = Math.Min(c.Paid, Math.Max(0, c.Src.Town.Gold));
            if (r <= 0) return;
            int h0 = c.Src.Town.Gold;
            c.Src.Town.ChangeGold(-(int)r);
            long got = h0 - c.Src.Town.Gold;
            if (got <= 0) return;
            int d0 = c.Dst.Town.Gold;
            c.Dst.Town.ChangeGold((int)got);
            _gRefSrc += got; _gRefDst += c.Dst.Town.Gold - d0; _dRefund += got;
        }""",
"""        private static void Back(Cart c)
        {
            AddInc(c, -1);
            var it = StartCalibration.ZItem(Z[c.Z]);
            _dBack++;
            // zwrot zaplaty z kasy zrodla (najwyzej kasa zrodla), frachtu nie
            long got = 0;
            if (c.Src != null && c.Src.Town != null && c.Dst != null && c.Dst.Town != null)
            {
                long r = Math.Min(c.Paid, Math.Max(0, c.Src.Town.Gold));
                if (r > 0)
                {
                    int h0 = c.Src.Town.Gold;
                    c.Src.Town.ChangeGold(-(int)r);
                    got = h0 - c.Src.Town.Gold;
                    if (got > 0) { int d0 = c.Dst.Town.Gold; c.Dst.Town.ChangeGold((int)got); _gRefSrc += got; _gRefDst += c.Dst.Town.Gold - d0; _dRefund += got; }
                    else got = 0;
                }
            }
            // recenzja runda 3 (drobne): zrodlo odbiera tylko towar odpowiadajacy oddanej zaplacie (dotad caly towar i czesc zaplaty przy pustej kasie); reszta - strata wozu
            int nBack = c.N;
            if (c.Paid > 0) nBack = (int)Math.Min(c.N, Math.Floor(c.N * (double)got / c.Paid + 1e-9));
            if (it != null && c.Src != null && c.Src.ItemRoster != null && nBack > 0)
            {
                var gf = GoodsLedger.Begin(GoodsLedger.FKsH, c.Src);
                try { c.Src.ItemRoster.AddToCounts(it, nBack); }
                finally { GoodsLedger.End(gf); }
                _dBackPcs += nBack;
            }
            else nBack = 0;
            int lost = c.N - nBack;
            if (lost > 0) { GoodsLedger.NoteKsLoss(it, lost, "zawrocony woz bez zwrotu zaplaty"); _dRetLost += lost; }
            c.N = 0;
        }

        /// <summary>Recenzja runda 3: cena sztuki towaru KS-H k na polce miasta celu (cena doby z mapy ZC4, inaczej gra; 0 - brak).</summary>
        private static int DestUnit(Town t, int k, ItemObject it)
        {
            if (t == null || it == null) return 0;
            int p = StartCalibration.ZPrice(t, Z[k]);
            if (p <= 0) { try { p = t.MarketData.GetPrice(it, null, false, null); } catch { p = 0; } }
            return Math.Max(0, p);
        }"""))
rep(P, pairs)
