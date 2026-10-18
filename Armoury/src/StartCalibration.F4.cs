using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// f4-pomiary (noc 17/18.10, koncowy przeglad ekonomii, pakiet P6) - SAME POMIARY reguly zmiany zajec A7 i regali pod KS. Zadna liczba gry sie nie zmienia:
    /// nie ma tu zapisu do blokow, Held, rak, RF ani cen. Klucze Measure Craft Rule (A7) i Measure Regale Shelf (regale) - wylaczone = kod jak przed paczka.
    ///  - DIAGNOZA A7 (linia "KS: A7 diagnoza (f4-pomiary)") w kazdej dobie kroku A7 (co 7 dob od D) i raz w dobie D: ten sam dochod rodziny na pracujacego (Income /
    ///    IncomeV z Rule.cs, ta sama lista kandydatow bloku i ten sam prog x1.25 x max(0.02, dochod biezacy)) przy pieciu zestawach cen:
    ///    G - ceny gry (srednia 28 dob miasta targowego - to, co widzi regula), B - indeks 1 (wartosc bazowa), T - ceny startu (cena cienia Planu w 0.7-1.5, to, na co
    ///    gra jest ustawiona w D), S1 - wzor reguly przy cenach cienia Planu, S2 - wzor Planu przy cenach cienia (wartosc kg Planu z konmi, zywiec jako mieso i skory,
    ///    bez gradientu krain) z jego premia mapy. Pytanie przegladu: czy przy cenach startu regula zgadza sie z Planem - jesli nie, blad siedzi w Income (jednostki,
    ///    KCost, renta, wartosc zywca i koni), a nie w cenach gry. Do tego rozklad dochodu biezacego i najlepszego innego (G), bloki na podlodze 0.02, sklad dochodu
    ///    (L x v, K, renta) i 10 najczestszych przejsc "z -> na" z rekami (G), po 5 dla B i S2.
    ///  - REGALE WOBEC POLKI (linia "KS: regale wobec polki (f4-pomiary)") w dobie cyklu regali (co 28 dob od D): obok dzisiejszego progu indeksu (1.2 / 0.8) liczy,
    ///    co mowi polka miasta targowego: polka &lt; T* (wiecej rak) albo &gt; 2 T* (mniej rak); T* - sol i daktyle: cel polki KS (T* x popyt zaplacony, ta sama miara co
    ///    kupcy zywnosci KS-H), ruda i drewno: zapas rzemiosla miasta (CaravanBulk.TargetFor), reszta - bez celu.
    /// Koszt: jeden przebieg po blokach w dobie kroku (wartosci zajec na rynek i zestaw cen raz - IncomeV z pamiecia wiersza), polka miasta raz na cykl.
    /// </summary>
    internal static partial class StartCalibration
    {
        private static bool F4A7On { get { var s = Settings.Current; return s != null && s.MeasureCraftRule; } }
        private static bool F4RegOn { get { var s = Settings.Current; return s != null && s.MeasureRegaleShelf; } }
        private static int _f4A7Day = -1, _f4A7St;

        /// <summary>Jeden zestaw cen diagnozy A7: liczniki blokow, rak, przejsc (klucz zajecie x 64 + zajecie -> [bloki, rece]).</summary>
        private sealed class F4Var
        {
            public int N, Press, Floor, Neg, Agree, NoAlt; public double Hands, PressHands;
            public readonly Dictionary<int, double[]> Tr = new Dictionary<int, double[]>();
            public void Note(int cur, int best, double ic, double ib, double hands, double thr)
            {
                N++; Hands += hands;
                if (ic <= 0) Neg++;
                if (ic <= 0.02) Floor++;
                if (best < 0) { NoAlt++; Agree++; return; }
                if (ib <= ic) Agree++;
                if (ib >= thr * Math.Max(0.02, ic))
                {
                    Press++; PressHands += hands;
                    double[] t; int k = cur * 64 + best;
                    if (!Tr.TryGetValue(k, out t)) { t = new double[2]; Tr[k] = t; }
                    t[0] += 1; t[1] += hands;
                }
            }
        }

        /// <summary>Wolane z RuleDaily (doba kroku A7 albo doba D, klucz Measure Craft Rule) - przed petla reguly; niczego nie zmienia.</summary>
        private static void F4A7Diag(int today, int d0, bool rule, bool step)
        {
            if (_f4A7Day == today) return;
            _f4A7Day = today;
            // f5-koszt (klucz Ks Cost Fast2): ten sam rachunek i ta sama linia (poza czasem) - na watku glownym przeglad blokow, L i renta x ha kazdego kandydata raz i zestaw G
            // (dochody zapamietane w bloku dla reguly A7 tej doby), reszta pomiaru i linia na watku z puli (StartCalibration.F5Koszt.cs). Wylaczony - stary rachunek co do linii.
            if (SwCostFast2) F4A7Async(today, d0, rule, step);
            else Log.Info(F4A7Old(today, d0, rule, step));
        }

        /// <summary>Diagnoza A7 - stary rachunek (paczka f4-pomiary bez zmian; zwraca linie zamiast ja pisac - f5-koszt: klucz Ks Cost Fast2 wylaczony i kontrola F5Check).</summary>
        private static string F4A7Old(int today, int d0, bool rule, bool step)
        {
            long t0 = Stopwatch.GetTimestamp();
            int st0 = _f4A7St;
            EnsureValues();
            var plan = _plan;
            double[] P = plan != null && plan.Error == null && plan.P != null && plan.P.Length == 2 * KsTab.NG ? plan.P : null;
            // zestawy cen na towary listy KS (ItemIds) wedlug rynku
            var one = new double[ItemCount]; for (int i = 0; i < ItemCount; i++) one[i] = 1.0;
            var pxT = new double[2][]; var pxS = new double[2][];
            double[][] vPlan = null;
            if (P != null)
            {
                for (int m = 0; m < 2; m++)
                {
                    pxT[m] = new double[ItemCount]; pxS[m] = new double[ItemCount];
                    for (int i = 0; i < ItemCount; i++) { pxT[m][i] = KsCore.StartIndex(ItemIds[i], P, m); pxS[m][i] = KsCore.ShadowIndex(ItemIds[i], P, m); }
                }
                if (plan.Val != null && plan.Val.Length == KsTab.NG)
                {
                    var mvp = KsCore.CraftValue(plan.Val);   // wartosc kg Planu (konie HorseVal, zywiec jako mieso i skory) - ta sama funkcja co Plan
                    vPlan = new double[2][];
                    for (int m = 0; m < 2; m++)
                    {
                        vPlan[m] = new double[KsTab.NC];
                        for (int c = 0; c < KsTab.NC; c++) { double s = 0; int o = c * KsTab.NG; for (int g = 0; g < KsTab.NG; g++) { double v = mvp[o + g]; if (v != 0) s += v * P[m * KsTab.NG + g]; } vPlan[m][c] = s; }
                    }
                }
            }
            double inert = _planIn != null ? Math.Max(1.0, _planIn.Inert) : 1.25;
            var rows = new Dictionary<double[], double[]>();          // pamiec wiersza IncomeV na zestaw cen (ta sama co ks-koszt T2)
            Func<double[], double[]> rowOf = px =>
            {
                double[] r;
                if (!rows.TryGetValue(px, out r)) { r = new double[KsTab.NC]; for (int c = 0; c < r.Length; c++) r[c] = double.NaN; rows[px] = r; }
                return r;
            };
            var vG = new F4Var(); var vB = new F4Var(); var vT = new F4Var(); var vS1 = new F4Var(); var vS2 = new F4Var();
            int planAgree = 0, planN = 0;
            var curG = new List<double>(16000); var bestG = new List<double>(16000); var ratioG = new List<double>(16000);
            double lvC = 0, kC = 0, rC = 0, lvB = 0, kB = 0, rB = 0;
            int inTrans = 0, locked = 0, noCand = 0, back = 0, noPx = 0;
            foreach (var ld in _live.Values)
            {
                if (ld == null || ld.St == null || ld.K == null || ld.B == null) continue;
                try
                {
                    if (LandBackOf(ld.St)) { back++; continue; }
                    Town mt = ld.TF ? ld.T : (ld.V != null && ld.V.TradeBound != null ? ld.V.TradeBound.Town : (ld.V != null && ld.V.Bound != null ? ld.V.Bound.Town : null));
                    double[] px = null;
                    bool hasPx = mt != null && _px.TryGetValue(mt, out px) && px != null;
                    int m = ld.Market == 0 ? 0 : 1;
                    string type = ld.K.Type;
                    foreach (var b in ld.B)
                    {
                        if (b == null || b.K == null || b.C == null || b.X == null || b.X.Length == 0) continue;
                        if (b.Target >= 0) { inTrans++; continue; }
                        if (KsWorksLock(ld, b)) { locked++; continue; }
                        if (b.Cand == null || b.Cand.Count < 2) { noCand++; continue; }
                        int ci = 0; for (int i = 1; i < b.X.Length; i++) if (b.X[i] > b.X[ci]) ci = i;
                        int cur = b.C[ci];
                        double hands = b.Hands * b.X[ci];
                        // G: ceny gry - dokladnie to, co liczy regula (ten sam wiersz IncomeV, kandydaci, prog)
                        if (hasPx)
                        {
                            var row = rowOf(px);
                            double ic = IncomeV(b, type, cur, px, row), ib = double.NegativeInfinity; int best = -1;
                            foreach (int c in b.Cand) { if (c == cur) continue; double v = IncomeV(b, type, c, px, row); if (v > ib) { ib = v; best = c; } }
                            vG.Note(cur, best, ic, ib, hands, SwitchRatio);
                            curG.Add(ic);
                            if (best >= 0)
                            {
                                bestG.Add(ib); ratioG.Add(ib / Math.Max(0.02, ic));
                                lvC += b.L(cur, type) * row[cur]; kC += KsTab.KCost[cur]; rC += b.Rent * KsTab.Ha(cur, b.K);
                                lvB += b.L(best, type) * row[best]; kB += KsTab.KCost[best]; rB += b.Rent * KsTab.Ha(best, b.K);
                            }
                        }
                        else noPx++;
                        F4Eval(vB, b, type, cur, hands, one, rowOf(one));
                        if (P != null)
                        {
                            F4Eval(vT, b, type, cur, hands, pxT[m], rowOf(pxT[m]));
                            F4Eval(vS1, b, type, cur, hands, pxS[m], rowOf(pxS[m]));
                            if (vPlan != null)
                            {
                                // S2: wzor Planu (KsCore.Shares) przy cenach cienia - L x sum(MV Planu x P) - K - renta x ha, podloga 0.02; wybor Planu z premia mapy
                                double ic = F4PlanInc(b, type, cur, vPlan[m]), ib = double.NegativeInfinity; int best = -1, pick = cur;
                                double uMax = Math.Max(0.02, ic) * (F4Start(ld, b, cur) ? inert : 1.0);
                                foreach (int c in b.Cand)
                                {
                                    if (c == cur) continue;
                                    double v = F4PlanInc(b, type, c, vPlan[m]);
                                    if (v > ib) { ib = v; best = c; }
                                    double u = Math.Max(0.02, v) * (F4Start(ld, b, c) ? inert : 1.0);
                                    if (u > uMax) { uMax = u; pick = c; }
                                }
                                vS2.Note(cur, best, ic, ib, hands, SwitchRatio);
                                planN++; if (pick == cur) planAgree++;
                            }
                        }
                    }
                }
                catch (Exception e) { _f4A7St++; Stumble("F4A7Diag(okreg)", e); }
            }
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            var sb = new StringBuilder(3000);
            sb.Append(PK).Append("A7 diagnoza (f4-pomiary) dzien ").Append(today).Append(" | D ").Append(d0).Append(" (D+").Append(today - d0).Append(") ")
              .Append(step ? "krok reguly A7" : "doba D (srednie cen gry sprzed polek startu)")
              .Append(" | regula: ").Append(rule ? "TAK (Start Calibration Switch Rule)" : "WYLACZONA - zajecia zamrozone na Planie wlaczenia (sam pomiar nacisku)")
              .Append(" | blokow ocenionych ").Append(vB.N).Append(" (pominiete: w przebudowie ").Append(inTrans).Append(", prace okregu ").Append(locked).Append(", bez kandydatow ").Append(noCand)
              .Append(", okregi krain wroconych ").Append(back).Append(", bez cen miasta targowego ").Append(noPx).Append(')');
            sb.Append(" | ceny gry (srednia 28 dob miasta targowego): ocenionych ").Append(vG.N).Append(", dochod biezacy q10/med/q90 ").Append(Q3(curG))
              .Append(", <= 0 ").Append(vG.Neg).Append(", na podlodze 0.02 ").Append(vG.Floor).Append("; najlepsze inne q10/med/q90 ").Append(Q3(bestG))
              .Append("; iloraz najlepsze/max(0.02, biezace) med ").Append(Qx(ratioG, 0.5)).Append(" q90 ").Append(Qx(ratioG, 0.9));
            F4VarText(sb, "; ", vG); sb.Append(", biezace najlepsze ").Append(vG.Agree).Append(" (").Append(Pc(vG.Agree, vG.N)).Append("%)");
            sb.Append(" | przy indeksie 1 (wartosc bazowa)"); F4VarText(sb, ": ", vB); sb.Append(", na podlodze ").Append(vB.Floor).Append(", <= 0 ").Append(vB.Neg);
            if (P != null)
            {
                sb.Append(" | przy cenach startu (cien Planu doby ").Append(_planDay).Append(" w 0.7-1.5)"); F4VarText(sb, ": ", vT); sb.Append(", na podlodze ").Append(vT.Floor);
                sb.Append(" | regula przy cenach cienia Planu (zywiec po cenie cienia zwierzecia, konie 0 - Plan liczy zywiec jako mieso i skory, konie z wartoscia)"); F4VarText(sb, ": ", vS1); sb.Append(", na podlodze ").Append(vS1.Floor);
                if (vPlan != null)
                {
                    sb.Append(" | wzor Planu przy cenach cienia (bez gradientu krain)"); F4VarText(sb, ": ", vS2);
                    sb.Append(", zajecie biezace = wybor Planu z premia mapy x").Append(inert.ToString("0.00", Inv)).Append(' ').Append(planAgree).Append(" z ").Append(planN).Append(" (").Append(Pc(planAgree, planN)).Append("%)");
                }
                else sb.Append(" | wzor Planu: brak wartosci kg Planu");
            }
            else sb.Append(" | ceny startu i cienia: brak Planu bez bledu");
            sb.Append(" | sklad dochodu (suma blokow, ceny gry): biezace L*v ").Append(lvC.ToString("0", Inv)).Append(", koszt K ").Append(kC.ToString("0", Inv)).Append(", renta ").Append(rC.ToString("0", Inv))
              .Append("; najlepsze inne L*v ").Append(lvB.ToString("0", Inv)).Append(", koszt K ").Append(kB.ToString("0", Inv)).Append(", renta ").Append(rB.ToString("0", Inv));
            sb.Append(" | przejscia (ceny gry, top 10, blokow/rak): ").Append(F4Top(vG, 10));
            sb.Append(" | przejscia przy indeksie 1 (top 5): ").Append(F4Top(vB, 5));
            if (vPlan != null) sb.Append(" | przejscia wzoru Planu (top 5): ").Append(F4Top(vS2, 5));
            sb.Append(" | czas ").Append(ms.ToString("0.0", Inv)).Append(" ms, potkniecia ").Append(_f4A7St - st0).Append('.');
            return sb.ToString();
        }

        private static void F4Eval(F4Var var, LiveBlock b, string type, int cur, double hands, double[] px, double[] row)
        {
            double ic = IncomeV(b, type, cur, px, row), ib = double.NegativeInfinity; int best = -1;
            foreach (int c in b.Cand) { if (c == cur) continue; double v = IncomeV(b, type, c, px, row); if (v > ib) { ib = v; best = c; } }
            var.Note(cur, best, ic, ib, hands, SwitchRatio);
        }

        private static double F4PlanInc(LiveBlock b, string type, int c, double[] vRow)
        {
            return Math.Max(1e-6, b.L(c, type)) * vRow[c] - KsTab.KCost[c] - b.Rent * KsTab.Ha(c, b.K);
        }

        /// <summary>Zajecie startowe bloku (premia mapy Planu - KsCore.Pairs: nie pola mieszczan, Start = nazwa zajecia).</summary>
        private static bool F4Start(LiveDist ld, LiveBlock b, int c) { return !ld.TF && b.K != null && b.K.Start == KsTab.Crafts[c]; }

        private static void F4VarText(StringBuilder sb, string sep, F4Var v)
        {
            sb.Append(sep).Append("nacisk (>= x").Append(SwitchRatio.ToString("0.00", Inv)).Append(") ").Append(v.Press).Append(" blokow (").Append(Pc(v.Press, v.N)).Append("%), rak ")
              .Append(v.PressHands.ToString("0", Inv)).Append(" (").Append(v.Hands > 0 ? (100.0 * v.PressHands / v.Hands).ToString("0.0", Inv) : "-").Append("%)");
        }

        private static string Pc(int a, int n) { return n > 0 ? (100.0 * a / n).ToString("0.0", Inv) : "-"; }

        private static string F4Top(F4Var v, int k)
        {
            if (v.Tr.Count == 0) return "-";
            var l = new List<KeyValuePair<int, double[]>>(v.Tr);
            l.Sort((x, y) => y.Value[0] != x.Value[0] ? y.Value[0].CompareTo(x.Value[0]) : y.Value[1].CompareTo(x.Value[1]));
            var sb = new StringBuilder(400);
            for (int i = 0; i < l.Count && i < k; i++)
            {
                int a = l[i].Key / 64, b = l[i].Key % 64;
                if (i > 0) sb.Append("; ");
                sb.Append(a >= 0 && a < KsTab.NC ? KsTab.Crafts[a] : "?").Append("->").Append(b >= 0 && b < KsTab.NC ? KsTab.Crafts[b] : "?").Append(' ')
                  .Append(l[i].Value[0].ToString("0", Inv)).Append('/').Append(l[i].Value[1].ToString("0", Inv));
            }
            return sb.ToString();
        }

        private static string Q3(List<double> v)
        {
            if (v.Count == 0) return "-/-/-";
            v.Sort();
            return Qs(v, 0.1) + "/" + Qs(v, 0.5) + "/" + Qs(v, 0.9);
        }

        private static string Qx(List<double> v, double q)
        {
            if (v.Count == 0) return "-";
            v.Sort();
            return Qs(v, q);
        }

        private static string Qs(List<double> sorted, double q) { return sorted[(int)(q * (sorted.Count - 1))].ToString("0.00", Inv); }

        /// <summary>Linia produkcji KS (RuleText): jawny stan zajec - regula A7 wylaczona = zajecia zamrozone na Planie wlaczenia (klucz Measure Craft Rule).</summary>
        private static string F4FrozenText(bool rule)
        {
            if (!F4A7On || rule) return "";
            int d0 = DDay;
            return "zajecia zamrozone - regula A7 wylaczona (Start Calibration Switch Rule), " + (d0 >= 0 ? "D+" + (Today() - d0) + " dob" : "przed D") + " | ";
        }

        // ============================================================ regale wobec polki (klucz Measure Regale Shelf)
        private static readonly Dictionary<Town, sbyte[]> _f4Shelf = new Dictionary<Town, sbyte[]>();
        private static readonly int[,] _f4Reg = new int[KsTab.NG, 10];       // [towar, 0 wpisy, 1 w gore, 2 w dol, 3 polka < T*, 4 > 2T*, 5 pomiedzy, 6 bez celu, 7 w gore przy > 2T*, 8 w dol przy < T*, 9 zgodne z polka]
        private static readonly double[] _f4RegHands = new double[KsTab.NG];
        private static int _f4RegSt;
        private static int _f4RegNoMap;                                       // recenzja: wpisy soli i daktyli "bez celu", bo mapy koszyka ZC4 jeszcze nie ma (pierwsza doba po wczytaniu)

        private static void F4RegBegin()
        {
            _f4Shelf.Clear(); Array.Clear(_f4Reg, 0, _f4Reg.Length); Array.Clear(_f4RegHands, 0, _f4RegHands.Length); _f4RegSt = 0; _f4RegNoMap = 0;
        }

        /// <summary>Stan polki miasta targowego dla towaru regale g: 0 polka &lt; T*, 1 pomiedzy, 2 polka &gt; 2 T*, 3 bez celu, 4 bez celu z braku mapy koszyka ZC4
        /// (sol i daktyle w pierwszej dobie po wczytaniu - RuleDaily biegnie przed Zc4Build, a mapa nie jest w zapisie; w innych dobach mapa z wczoraj = mapa na dzis,
        /// ta sama, z ktorej korzysta reszta KS w ciagu doby) (pamiec cyklu).</summary>
        private static int F4ShelfState(Town mt, int g)
        {
            if (mt == null || g < 0 || g >= KsTab.NG) return 3;
            sbyte[] a;
            if (!_f4Shelf.TryGetValue(mt, out a)) { a = new sbyte[KsTab.NG]; for (int i = 0; i < a.Length; i++) a[i] = -1; _f4Shelf[mt] = a; }
            if (a[g] >= 0) return a[g];
            int r = 3;
            try
            {
                int ii = GoodToItem[g];
                ItemObject it = ii >= 0 && _items != null ? _items[ii] : null;
                if (it != null && mt.Settlement != null && mt.Settlement.ItemRoster != null)
                {
                    double T = 0;
                    bool zc = g == KsTab.GSalt || g == KsTab.GDate;
                    if (zc && _zt.Count == 0) r = 4;                                  // recenzja: brak mapy ZC4 - osobny licznik (nie "towar bez celu")
                    else if (g == KsTab.GSalt) T = ZStallTarget(mt, ZSalt);
                    else if (g == KsTab.GDate) T = ZStallTarget(mt, ZDate);
                    else if (g == KsTab.GIron || g == KsTab.GWood) T = CaravanBulk.TargetFor(mt, it);
                    if (T > 0.5)
                    {
                        int n = mt.Settlement.ItemRoster.GetItemNumber(it);
                        r = n < T ? 0 : (n > 2 * T ? 2 : 1);
                    }
                }
            }
            catch (Exception e) { _f4RegSt++; Stumble("F4ShelfState", e); r = 3; }
            a[g] = (sbyte)r;
            return r;
        }

        /// <summary>RegaleCycle (przed zmiana RF, klucz Measure Regale Shelf): decyzja progu indeksu obok miary polki - tylko liczniki.</summary>
        private static void F4RegNote(LiveBlock b, int j, int g, double ix, Town mt)
        {
            if (g < 0 || g >= KsTab.NG) return;
            int st = F4ShelfState(mt, g);
            if (st == 4) { _f4RegNoMap++; st = 3; }                              // w sumach linii - "bez celu" (sumy bez zmian), osobno w dopisku
            bool up = ix > 1.2, down = !up && ix < 0.8;
            _f4Reg[g, 0]++;
            if (up) _f4Reg[g, 1]++; else if (down) _f4Reg[g, 2]++;
            _f4Reg[g, 3 + st]++;
            if (up && st == 2) _f4Reg[g, 7]++;
            if (down && st == 0) _f4Reg[g, 8]++;
            if ((up && st == 0) || (down && st == 2) || (!up && !down && st == 1)) _f4Reg[g, 9]++;   // zgodne: w gore przy pustej, w dol przy pelnej, bez zmiany pomiedzy
            if (b.RF != null && j < b.RF.Length) _f4RegHands[g] += b.Hands * b.RF[j];
        }

        private static void F4RegLine(int today)
        {
            var t = new int[10];
            for (int g = 0; g < KsTab.NG; g++) for (int k = 0; k < 10; k++) t[k] += _f4Reg[g, k];
            int withT = t[3] + t[4] + t[5];
            var sb = new StringBuilder(1600);
            sb.Append(PK).Append("regale wobec polki (f4-pomiary) dzien ").Append(today).Append(" | cykl co ").Append(RegCycle).Append(" dob od D ").Append(DDay)
              .Append(" | prog dzisiejszy: indeks towaru w miescie targowym > 1.2 - rece +10%, < 0.8 - rece -10% | miara polki (tylko pomiar): sol i daktyle - cel T* x popyt zaplacony (KS-H), ruda i drewno - zapas rzemiosla miasta (CaravanBulk), reszta bez celu")
              .Append(" | razem: wpisow ").Append(t[0]).Append(", w gore ").Append(t[1]).Append(", w dol ").Append(t[2]).Append(", bez zmiany ").Append(t[0] - t[1] - t[2])
              .Append("; polka < T* ").Append(t[3]).Append(", > 2T* ").Append(t[4]).Append(", pomiedzy ").Append(t[5]).Append(", bez celu ").Append(t[6])
              .Append("; w gore przy polce > 2T* ").Append(t[7]).Append(", w dol przy polce < T* ").Append(t[8]).Append("; zgodne z polka (w gore przy < T*, w dol przy > 2T*, bez zmiany pomiedzy) ").Append(t[9]).Append(" z ").Append(withT).Append(" wpisow z celem")
              .Append("; w tym bez celu z braku mapy koszyka ZC4 (sol i daktyle, pierwsza doba po wczytaniu) ").Append(_f4RegNoMap)
              .Append(" | wedlug towaru (wpisow gora/dol, polka <T*/>2T*/pomiedzy/bez celu, w gore przy pelnej, w dol przy pustej, rece przy zlozu):");
            bool first = true;
            for (int g = 0; g < KsTab.NG; g++)
            {
                if (_f4Reg[g, 0] == 0) continue;
                sb.Append(first ? " " : "; ").Append(KsTab.Goods[g]).Append(' ').Append(_f4Reg[g, 0]).Append(' ').Append(_f4Reg[g, 1]).Append('/').Append(_f4Reg[g, 2]).Append(' ')
                  .Append(_f4Reg[g, 3]).Append('/').Append(_f4Reg[g, 4]).Append('/').Append(_f4Reg[g, 5]).Append('/').Append(_f4Reg[g, 6]).Append(' ')
                  .Append(_f4Reg[g, 7]).Append(' ').Append(_f4Reg[g, 8]).Append(' ').Append(_f4RegHands[g].ToString("0", Inv));
                first = false;
            }
            if (first) sb.Append(" -");
            sb.Append(" | miast z polka ").Append(_f4Shelf.Count).Append(", potkniecia ").Append(_f4RegSt).Append('.');
            Log.Info(sb.ToString());
        }
    }
}
