import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from edlib import rep
P = r'C:/Users/GAME/AppData/Local/Temp/claude/C--Users-GAME-Bannerlor2-Realm-of-Thrones/ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3/scratchpad/zb/n-ks/Armoury/src/StartCalibration.R3.cs'
pairs = []
# TkP2 per market
pairs.append(("""                double vill = Math.Max(0.0, _tk[TkVill * n + i] - _tk[TkP2 * n + i]);
                double all = one + _tk[TkCons * n + i] + vill + _tk[TkZc5 * n + i];
                _one28[i] += (one - _one28[i]) * w28; _off7[i] += (all - _off7[i]) * w7;
            }
            Array.Clear(_tk, 0, _tk.Length);""", """                double vill = Math.Max(0.0, _tk[TkVill * n + i] - _tk[TkP2 * n + i]);
                double all = one + _tk[TkCons * n + i] + vill + _tk[TkZc5 * n + i];
                _one28[i] += (one - _one28[i]) * w28; _off7[i] += (all - _off7[i]) * w7;
                // recenzja runda 3 (wazne): drabina 2 wsi (ponad plan, z oszczednosci) poza zuzyciem sterowania rynku (C28) - wedlug rynku i towaru Planu
                double p2 = _tk[TkP2 * n + i];
                if (p2 > 0) { int mk = KsFoodCarts.MarketOfIndex(i / ZN), g = CtlGood(i % ZN); if ((mk == 0 || mk == 1) && g >= 0) _p2Mk[mk * KsTab.NG + g] += p2; }
            }
            Array.Clear(_tk, 0, _tk.Length);"""))
pairs.append(("""        private static int _cN, _cDay = -1;""", """        private static int _cN, _cDay = -1;
        private static readonly double[] _p2Mk = new double[2 * KsTab.NG];          // recenzja runda 3: sztuki drabiny 2 wsi doby wedlug rynku i towaru Planu (poza C28)
        private static double[] _mfStage;                                          // recenzja runda 3 (drobne): czynnik nowego Planu - do _mf dopiero przy Planie bez bledu
        internal const int CtlEnd = 49;                                            // recenzja runda 3 (wazne): sterowanie rynku i ciecie nadmiaru krain tylko w oknie startu D+14..D+49"""))
pairs.append(("""            for (int m = 0; m < 2; m++) for (int z = 0; z < ZN; z++) { int g = CtlGood(z); if (g >= 0) u[m * KsTab.NG + g] += _uDay[m * ZN + z]; }
            Array.Clear(_uDay, 0, _uDay.Length);""", """            for (int m = 0; m < 2; m++) for (int z = 0; z < ZN; z++) { int g = CtlGood(z); if (g >= 0) u[m * KsTab.NG + g] += _uDay[m * ZN + z]; }
            Array.Clear(_uDay, 0, _uDay.Length);
            for (int i = 0; i < c.Length; i++) c[i] = Math.Max(0.0, c[i] - _p2Mk[i]);       // bez drabiny 2 wsi (jednorazowe wydawanie oszczednosci)
            Array.Clear(_p2Mk, 0, _p2Mk.Length);"""))
# MarketControl window + staging
pairs.append(("""            if (!SwMarketControl) { _ctlLine = null; _landGlut.Clear(); return null; }      // bez klucza - margines popytu targu jak runda 3 (MarketMargin)
            bool ctl = _state == StateApplied && DDay >= 0 && day >= DDay + 14 && _cRestart >= 0 && _cN >= CtlMinDays && _plan != null && _plan.Error == null && _plan.D != null;
            var mf = new double[2 * KsTab.NG];
            var sb = new StringBuilder(1500);
            sb.Append(PK).Append("sterowanie rynku doba ").Append(day).Append(ctl ? " | czynnik popytu targu z zuzycia i braku (" + Math.Min(28, _cN) + " dob od konca rampy " + _cRestart + ")" : " | do D+14 + " + CtlMinDays + " dob pelnej lambdy albo bez klucza Ks Market Control: czynnik 1.03 (polki startu)");
            for (int i = 0; i < mf.Length; i++) mf[i] = ShelfMargin;
            if (!ctl) { _ctlLine = sb.Append('.').ToString(); LandGlutWeekly(day, false); return mf; }
            if (_mf == null) { _mf = new double[2 * KsTab.NG]; for (int i = 0; i < _mf.Length; i++) _mf[i] = ShelfMargin; }""",
"""            if (!SwMarketControl) { _ctlLine = null; _landGlut.Clear(); return null; }      // bez klucza - margines popytu targu jak runda 3 (MarketMargin)
            _mfStage = null;
            // recenzja runda 3 (wazne): planista tylko w oknie startu - po D+49 czynnik wraca do 1 przez 2 Plany (rece przesuwa dalej regula zysku i ceny), ciecie nadmiaru krain
            // tez schodzi do 1; bez tego co tydzien rece pracy domowej i koszyki szlyby za sterowaniem bez kosztu i czasu zmiany (decyzja Jeffa 16.10 - zmiana powoli i z kosztem)
            bool after = _state == StateApplied && DDay >= 0 && day > DDay + CtlEnd;
            bool ctl = !after && _state == StateApplied && DDay >= 0 && day >= DDay + 14 && _cRestart >= 0 && _cN >= CtlMinDays && _plan != null && _plan.Error == null && _plan.D != null;
            var mf = new double[2 * KsTab.NG];
            var sb = new StringBuilder(1500);
            sb.Append(PK).Append("sterowanie rynku doba ").Append(day).Append(ctl ? " | czynnik popytu targu z zuzycia i braku (" + Math.Min(28, _cN) + " dob od konca rampy " + _cRestart + ")"
                : after ? " | po D+" + CtlEnd + ": koniec sterowania - czynnik wraca do 1 (2 Plany), dalej regula zysku i ceny" : " | do D+14 + " + CtlMinDays + " dob pelnej lambdy albo bez klucza Ks Market Control: czynnik 1.03 (polki startu)");
            for (int i = 0; i < mf.Length; i++) mf[i] = ShelfMargin;
            if (after)
            {
                var st = new double[2 * KsTab.NG];
                for (int i = 0; i < st.Length; i++)
                {
                    double k0 = _mf != null ? _mf[i] : 1.0, k = 1.0 + (k0 - 1.0) * 0.5;
                    if (Math.Abs(k - 1.0) < 0.01) k = 1.0;
                    st[i] = k; mf[i] = k;
                }
                _mfStage = st;
                LandGlutWeekly(day, false, true);
                _ctlLine = sb.Append('.').ToString();
                return mf;
            }
            if (!ctl) { _ctlLine = sb.Append('.').ToString(); LandGlutWeekly(day, false); return mf; }
            var basis = _mf;
            if (basis == null) { basis = new double[2 * KsTab.NG]; for (int i = 0; i < basis.Length; i++) basis[i] = ShelfMargin; }
            var stage = (double[])basis.Clone();"""))
pairs.append(("""                    double k = _mf[i];
                    if (dm > 1.0 && cu > 1.0)
                    {
                        double gap = Math.Max(-CtlGap, Math.Min(CtlGap, (hs[i] - h[i]) / (28.0 * cu)));
                        double tgt = cu * (1.0 + gap) / dm;
                        k = Math.Max(_mf[i] * (1.0 - CtlStep), Math.Min(_mf[i] * (1.0 + CtlStep), tgt));
                        k = Math.Max(0.85, Math.Min(1.10, k));
                    }
                    _mf[i] = k; mf[i] = k;""", """                    double k = basis[i];
                    if (dm > 1.0 && cu > 1.0)
                    {
                        double gap = Math.Max(-CtlGap, Math.Min(CtlGap, (hs[i] - h[i]) / (28.0 * cu)));
                        double tgt = cu * (1.0 + gap) / dm;
                        k = Math.Max(basis[i] * (1.0 - CtlStep), Math.Min(basis[i] * (1.0 + CtlStep), tgt));
                        k = Math.Max(0.85, Math.Min(1.10, k));
                    }
                    stage[i] = k; mf[i] = k;"""))
pairs.append(("""            sb.Append(" (towar: zuzycie / brak / podaz targu Planu szt./d, czynnik)");
            LandGlutWeekly(day, true);""", """            sb.Append(" (towar: zuzycie bez drabiny 2 wsi / brak / podaz targu Planu szt./d, czynnik)");
            _mfStage = stage;                                  // do _mf przy Planie bez bledu (PlanCollect)
            LandGlutWeekly(day, true);"""))
pairs.append(("""        private static void LandGlutWeekly(int day, bool on)
        {
            _landGlut.Clear();
            if (!on) return;""", """        private static void LandGlutWeekly(int day, bool on, bool decay = false)
        {
            _landGlut.Clear();
            if (decay)
            {
                // recenzja runda 3: po oknie startu ciecie nadmiaru krain schodzi do 1 przez 2 Plany
                var keys = new List<string>(_landCut.Keys);
                foreach (var key in keys)
                {
                    var cut = _landCut[key]; bool any = false;
                    for (int g = 0; g < cut.Length; g++) { if (cut[g] < 1.0) { cut[g] = 1.0 - (1.0 - cut[g]) * 0.5; if (cut[g] > 0.99) cut[g] = 1.0; } if (cut[g] < 0.999) any = true; }
                    if (!any) _landCut.Remove(key);
                }
                return;
            }
            if (!on) return;"""))
# committed factor at PlanCollect: helper
pairs.append(("""        // ------------------------------------------------------------ miasto targowe okregu (krok 5)""", """        /// <summary>Recenzja runda 3 (drobne): czynnik popytu targu nowego Planu do stanu - tylko Plan bez bledu (PlanCollect); Plan przerwany albo z bledem - bez zmiany.</summary>
        private static void MarketFactorCommit(KsResult r)
        {
            if (r != null && r.Error == null && _mfStage != null) _mf = _mfStage;
            _mfStage = null;
        }

        /// <summary>Recenzja runda 3 (wazne): czynnik ceny krainy miasta targowego mk dla pozycji koszyka z (gradient krainy z Planu x ciecie nadmiaru krainy); 1 - bez gradientu.</summary>
        internal static double LandGradZ(Settlement mk, int z)
        {
            if (mk == null || z < 0 || z >= ZN) return 1.0;
            int g = KsBasket.RawGood[z]; if (g < 0) return 1.0;
            var gr = _plan != null && _plan.Error == null ? _plan.Grad : _grad;
            if (gr == null && _landCut.Count == 0) return 1.0;
            string land = TownLandOf(mk);
            double v = 1.0; double[] a, c;
            if (gr != null && gr.TryGetValue(land, out a) && a != null && g < a.Length && a[g] > 0 && !double.IsNaN(a[g])) v = a[g];
            if (_landCut.TryGetValue(land, out c) && c != null && g < c.Length && c[g] > 0 && c[g] < 1.0) v *= c[g];
            return v;
        }

        // ------------------------------------------------------------ miasto targowe okregu (krok 5)"""))
# pass 2: price rationing
pairs.append(("""        internal const double P2ShelfK = 1.5;

        /// <summary>Prog drabiny 2 wsi w miescie t (szt.): P2ShelfK x cel polki (KS-R3 runda 3).</summary>
        internal static double ZPass2Floor(Town t, int z) { return P2ShelfK * ZStallTarget(t, z); }""",
"""        // recenzja runda 3 (drobne): zamiast stalego pasa 1.5 x celu - cena: drabina 2 kupuje, dopoki cena polki jest najwyzej P2Res x ceny startu krainy, i najwyzej do polki,
        // przy ktorej cena wzoru doszlaby do P2Res x startu (dni polki przy indeksie P2Res x startu: 0.8^(-1/0.6) = ok. 1.45 x cel)
        internal const double P2Res = 0.8;

        /// <summary>Prog drabiny 2 wsi w miescie t (szt.): dni polki przy indeksie P2Res x startu (krainy) x popyt zaplacony.</summary>
        internal static double ZPass2Floor(Town t, int z)
        {
            if (t == null || z < 0 || z >= ZN) return 0.0;
            int m = TownMarket(t);
            double I = KsCore.StartIndex(ZIds[z], _plan != null ? _plan.P : null, m == 0 ? 0 : 1) * LandGradZ(t.Settlement, z);
            return ShelfDays(P2Res * I) * ZDp(t, z);
        }

        /// <summary>Cena rezerwacji drabiny 2 (zl za sztuke): P2Res x cena startu krainy.</summary>
        internal static double ZPass2Price(Town t, int z)
        {
            var it = z >= 0 && z < ZN ? ZItem(z) : null; if (t == null || it == null) return 0.0;
            int m = TownMarket(t);
            return P2Res * Math.Max(1, it.Value) * KsCore.StartIndex(ZIds[z], _plan != null ? _plan.P : null, m == 0 ? 0 : 1) * LandGradZ(t.Settlement, z);
        }"""))
# extras: H (KS-H EMAs) and S (own streak)
pairs.append(("""            if (_gold28 != null) { sb.Append("A=").Append(_goldN); foreach (var g in _gold28) sb.Append(',').Append(F0(g)); sb.Append('\\n'); }
            return sb.Length > 0 ? Pack(sb.ToString()) : "";""", """            if (_gold28 != null) { sb.Append("A=").Append(_goldN); foreach (var g in _gold28) sb.Append(',').Append(F0(g)); sb.Append('\\n'); }
            // recenzja runda 3 (wazne): srednie dowozu korytarzy i rurociagu KS-H (Plan po wczytaniu), serie "wies je swoje" (NKS-25)
            { string h = KsFoodCarts.ExportEma(); if (!string.IsNullOrEmpty(h)) sb.Append("H=").Append(h).Append('\\n'); }
            if (_ownStreak.Count > 0)
            {
                sb.Append("S="); bool f = true;
                foreach (var kv in _ownStreak) { if (kv.Key == null || kv.Key.Settlement == null || kv.Value <= 0) continue; if (!f) sb.Append(','); f = false; sb.Append(Clean(kv.Key.Settlement.StringId)).Append(':').Append(kv.Value); }
                sb.Append('\\n');
            }
            return sb.Length > 0 ? Pack(sb.ToString()) : "";"""))
pairs.append(("""                        case 'A':
                            {
                                var p = v.Split(','); int n;""", """                        case 'H': KsFoodCarts.ImportEma(v); break;
                        case 'S':
                            foreach (var e in v.Split(','))
                            {
                                var p = e.Split(':'); int a0;
                                if (p.Length != 2 || !int.TryParse(p[1], out a0) || a0 <= 0) continue;
                                var st = Settlement.Find(p[0]); if (st == null || st.Village == null) continue;
                                _ownStreak[st.Village] = a0;
                            }
                            break;
                        case 'A':
                            {
                                var p = v.Split(','); int n;"""))
# F section: drop default entries (floor on, counters 0)
pairs.append(("""                foreach (var kv in _floor) { if (kv.Key == null || kv.Key.Settlement == null) continue; if (!f) sb.Append(','); f = false;""",
"""                foreach (var kv in _floor) { if (kv.Key == null || kv.Key.Settlement == null || (kv.Value[0] == 1 && kv.Value[1] == 0 && kv.Value[2] == 0)) continue; if (!f) sb.Append(','); f = false;"""))
pairs.append(("""            Array.Clear(_p2, 0, _p2.Length);
        }""", """            Array.Clear(_p2, 0, _p2.Length); Array.Clear(_p2Mk, 0, _p2Mk.Length); _mfStage = null; _r3Pending = null; _r3Keep = null;
        }"""))
rep(P, pairs)
