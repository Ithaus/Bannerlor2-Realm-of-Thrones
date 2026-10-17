import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from edlib import rep
H = os.path.dirname(os.path.abspath(__file__))
P = r'C:/Users/GAME/AppData/Local/Temp/claude/C--Users-GAME-Bannerlor2-Realm-of-Thrones/ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3/scratchpad/zb/n-ks/Armoury/src/KsFoodCarts.cs'
s = open(P, encoding='utf-8', newline='').read()
a = s.index('        /// <summary>Decyzje doby: zakupy jedzenia dla miast z brakiem')
b = s.index('        /// <summary>Cena lotu n sztuk z polki zrodla')
s = s[:a] + open(os.path.join(H, 'ksh_daily.cs.txt'), encoding='utf-8').read() + s[b:]
open(P, 'w', encoding='utf-8', newline='').write(s)
pairs = []
pairs.append(("""        private static void Sample(Town src, ItemObject it, int n, long approx)
        {
            try
            {
                long ex = LotPrice(src, it, n, true);""",
"""        private static void Sample(Town src, ItemObject it, int n)
        {
            try
            {
                long approx = LotPrice(src, it, n, false);
                long ex = LotPrice(src, it, n, true);"""))
pairs.append(("""            var ro = src.ItemRoster;
            if (ro == null || dstT.Gold < cost + fr) return 0;
            int before = ro.GetItemNumber(it);
            int take = Math.Min(n, Math.Max(0, before - Keep));
            if (take <= 0) return 0;
            if (take < n) { cost = (long)Math.Round(cost * (double)take / n); fr = (long)Math.Round(fr * (double)take / n); n = take; }
            var gf = GoodsLedger.Begin(GoodsLedger.FKsH, src);
            try { ro.AddToCounts(it, -n); }
            finally { GoodsLedger.End(gf); }
            int removed = before - ro.GetItemNumber(it);
            if (removed != n) { _dDrop += Math.Max(0, n - removed); n = Math.Max(0, removed); if (n <= 0) return 0; }""",
"""            var ro = src.ItemRoster;
            if (ro == null || dstT.Gold < cost + fr) return 0;
            // recenzja runda 3 (drobne): sztuki bez modyfikatora (jak polka w Daily), cena i fracht za sztuki naprawde zdjete z polki
            var el = new EquipmentElement(it, null, null, false);
            int ei = ro.FindIndexOfElement(el), before = ei >= 0 ? ro.GetElementNumber(ei) : 0;
            int take = Math.Min(n, Math.Max(0, before - Keep));
            if (take <= 0) return 0;
            if (take < n) { cost = (long)Math.Round(cost * (double)take / n); fr = (long)Math.Round(fr * (double)take / n); n = take; }
            var gf = GoodsLedger.Begin(GoodsLedger.FKsH, src);
            try { ro.AddToCounts(el, -n); }
            finally { GoodsLedger.End(gf); }
            int ea = ro.FindIndexOfElement(el), after = ea >= 0 ? ro.GetElementNumber(ea) : 0;
            int removed = before - after;
            if (removed != n)
            {
                _dDrop += Math.Max(0, n - removed);                  // zamowione, a nie zdjete z polki (nie weszlo do drogi - poza kontrola towaru)
                if (removed <= 0) return 0;
                cost = (long)Math.Round(cost * (double)removed / n); fr = (long)Math.Round(fr * (double)removed / n); n = removed;
            }"""))
# Flush: goods rest, new sections, EMA
pairs.append(("""                    long rest = _prevTransit + _dBought - _dArr - _dBackPcs - _dLost - _dDrop - now;""",
"""                    // recenzja runda 3 (drobne): bez odrzuconych przy zaladunku (nie weszly do drogi); psucie i strata zawroconych - ujscia ksiegi towarow
                    long rest = _prevTransit + _dBought - _dArr - _dBackPcs - _dLost - _dSpoil - _dRetLost - now;"""))
pairs.append(("""                    sb.Append(" | kontrola towaru reszta ").Append(rest).Append(" (bez celu ").Append(_dLost).Append(", odrzucone przy zaladunku ").Append(_dDrop).Append("), zlota reszta ").Append(gold)""",
"""                    sb.Append(" | kontrola towaru reszta ").Append(rest).Append(" (bez celu ").Append(_dLost).Append(", psucie w drodze ").Append(_dSpoil).Append(", zawrocone bez zwrotu zaplaty ").Append(_dRetLost)
                      .Append("; odrzucone przy zaladunku ").Append(_dDrop).Append(" - nie weszly do drogi), zlota reszta ").Append(gold)"""))
pairs.append(("""                sb.Append(" | z dalszych miast (12 najblizszych bez nadwyzki) ").Append(_dFar).Append(" wozow, z zapasu kupcow (polka ponizej T* x popyt) ").Append(_dRes).Append(" wozow, ").Append(_dResGold).Append(" zl");
                sb.Append(" | probka wyceny 1/64 ").Append(_dSample > 0 ? (100.0 * _dSampleErr / _dSample).ToString("0.00", inv) + "% (max " + (100.0 * _dSampleMax).ToString("0.00", inv) + "%, lotow " + _dSample + ")" : "-")""",
"""                sb.Append(" | z dalszych miast (12 najblizszych bez nadwyzki) ").Append(_dFar).Append(" wozow, z zapasu kupcow (polka zboza i soli ponizej T* x popyt, najwyzej do polowy zapasu) ").Append(_dRes).Append(" wozow, ").Append(_dResGold).Append(" zl");
                // recenzja runda 3 (wazne): wynik kupcow (loty dowiezione: wartosc u celu przy cenie polki dzis wobec zaplaty i frachtu) i loty pominiete bez zysku
                _dMarPct.Sort();
                sb.Append(" | kupcy: dowiezione loty ").Append(_dMarLots).Append(", wartosc u celu ").Append(_dMarVal).Append(" zl, zaplata + fracht ").Append(_dMarCost).Append(" zl, wynik ").Append(_dMarVal - _dMarCost)
                  .Append(" zl, mediana marzy ").Append(_dMarPct.Count > 0 ? (100.0 * _dMarPct[_dMarPct.Count / 2]).ToString("0.0", inv) + "%" : "-").Append(", ze strata ").Append(_dMarLoss).Append(", pominiete bez zysku ").Append(_dNoProfit);
                // recenzja runda 3 (wazne): kasy miast z pusta polka (cel KS-H dzis) wobec zapasu kupcow
                _dCofShort.Sort();
                sb.Append(" | kasy miast z brakiem / zapas kupcow: ").Append(_dCofShort.Count > 0 ? "q10 " + _dCofShort[(int)(0.1 * (_dCofShort.Count - 1))].ToString("0.00", inv) + ", mediana " + _dCofShort[(_dCofShort.Count - 1) / 2].ToString("0.00", inv) + " (" + _dCofShort.Count + " miast)" : "-");
                sb.Append(" | probka wyceny 1/64 ").Append(_dSample > 0 ? (100.0 * _dSampleErr / _dSample).ToString("0.00", inv) + "% (max " + (100.0 * _dSampleMax).ToString("0.00", inv) + "%, lotow " + _dSample + ", pierwsze " + SampleCap + " szt., czas " + (_dSampleTicks * 1000.0 / Stopwatch.Frequency).ToString("0.00", inv) + " ms poza czasem)" : "-")"""))
# Export: size line
pairs.append(("""                sb.Append(c.Src.StringId).Append('|').Append(c.Dst.StringId).Append('|').Append(KsBasket.Ids[Z[c.Z]]).Append('|').Append(c.N).Append('|').Append(c.DayBought).Append('|')
                  .Append(c.DayDue).Append('|').Append(c.Paid).Append('|').Append(c.Freight).Append('|').Append(c.Sea ? 1 : 0).Append('~');
            }
            return sb.ToString();
        }""",
"""                sb.Append(c.Src.StringId).Append('|').Append(c.Dst.StringId).Append('|').Append(KsBasket.Ids[Z[c.Z]]).Append('|').Append(c.N).Append('|').Append(c.DayBought).Append('|')
                  .Append(c.DayDue).Append('|').Append(c.Paid).Append('|').Append(c.Freight).Append('|').Append(c.Sea ? 1 : 0).Append('~');
            }
            string res = sb.ToString();
            // recenzja runda 3 (drobne): rozmiar arm_ks_kupcy w linii (NKS-20)
            try
            {
                int today = Today();
                if (res.Length > 0 && today != _exportLogged)
                {
                    _exportLogged = today;
                    Log.Info("KS: zapis arm_ks_kupcy doba " + today + " | " + (res.Length / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " KB (wozow " + _carts.Count + ", paczek SaveText " + ((res.Length + 7999) / 8000) + ").");
                }
            }
            catch { }
            return res;
        }
        private static int _exportLogged = -1;

        // recenzja runda 3 (wazne): srednie dowozu korytarzy (CorrDelivered Planu po D) i rurociagu rampy w zapisie (arm_ks_r3, sekcja H) - Plan pierwszej doby po wczytaniu
        // nie widzial dowozu (kazdy korytarz 20 szt./d, krainy w tryb "kraina", gradient skakal o ok. 25%)
        internal static string ExportEma()
        {
            var sb = new StringBuilder(_delEma.Count * 32 + 512);
            sb.Append(_delN).Append(';').Append(_shipN).Append(';');
            bool f = true;
            foreach (var kv in _delEma) { if (kv.Value < 0.01) continue; if (!f) sb.Append(','); f = false; sb.Append(kv.Key.Replace('|', '>')).Append('=').Append(kv.Value.ToString("0.##", CultureInfo.InvariantCulture)); }
            sb.Append(';');
            f = true;
            for (int i = 0; i < _shipEma.Length; i++)
            {
                if (_shipEma[i] <= 0 && _daysEma[i] <= 0) continue;
                if (!f) sb.Append(','); f = false;
                sb.Append(i).Append(':').Append(_shipEma[i].ToString("0.##", CultureInfo.InvariantCulture)).Append(':').Append(_daysEma[i].ToString("0.##", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        internal static void ImportEma(string s)
        {
            try
            {
                if (string.IsNullOrEmpty(s)) return;
                var p = s.Split(';'); if (p.Length < 4) return;
                int dn, sn; if (!int.TryParse(p[0], out dn) || !int.TryParse(p[1], out sn)) return;
                _delEma.Clear();
                foreach (var e in p[2].Split(','))
                {
                    int q = e.LastIndexOf('='); double v;
                    if (q <= 0 || !double.TryParse(e.Substring(q + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) continue;
                    _delEma[e.Substring(0, q).Replace('>', '|')] = Math.Max(0.0, v);
                }
                Array.Clear(_shipEma, 0, _shipEma.Length); Array.Clear(_daysEma, 0, _daysEma.Length);
                foreach (var e in p[3].Split(','))
                {
                    var q = e.Split(':'); int i; double a, b;
                    if (q.Length != 3 || !int.TryParse(q[0], out i) || i < 0 || i >= _shipEma.Length || !double.TryParse(q[1], NumberStyles.Float, CultureInfo.InvariantCulture, out a) || !double.TryParse(q[2], NumberStyles.Float, CultureInfo.InvariantCulture, out b)) continue;
                    _shipEma[i] = Math.Max(0.0, a); _daysEma[i] = Math.Max(0.0, b);
                }
                _delN = Math.Max(0, Math.Min(28, dn)); _shipN = Math.Max(0, Math.Min(7, sn));
            }
            catch (Exception e) { Stumble("ImportEma", e); }
        }"""))
pairs.append(("""            _delEma.Clear(); _delDay.Clear(); _delN = 0; Array.Clear(_shipEma, 0, _shipEma.Length); Array.Clear(_daysEma, 0, _daysEma.Length); _shipN = 0;""",
"""            _delEma.Clear(); _delDay.Clear(); _delN = 0; Array.Clear(_shipEma, 0, _shipEma.Length); Array.Clear(_daysEma, 0, _daysEma.Length); _shipN = 0; _exportLogged = -1; _daysTicks = 0; _daysCalls = 0;"""))
rep(P, pairs)
