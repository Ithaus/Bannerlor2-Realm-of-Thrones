using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// KS - KROK 8 "PIENIADZE" (poprawka po autotescie 16.10; projekt rozdz. 9 i 11.1 krok 8 - dotad nie bylo go w kodzie). Pomiar na watku glownym do zrzutu Planu i liczby
    /// stanu zastosowanego:
    ///  - UDZIAL KIESY WSI W UTARGU WOZOW (VillageTakings.SplitToday, srednia 28 dob) x cena wozu / cena polki (0.90: kara handlu 6% przy sprzedazy taboru do miasta,
    ///    1.2% przy zakupie wsi, zaokraglenia ceny) x (1 - 5% na oplaty stale, daniny i dziesiecine) = alfa wsi Planu;
    ///  - ZAROBEK NETTO KLAS MIAST W/R/P (TownFolk.ZcEarnNet - po podatku pana, cechu i dziesiecinie; srednia 28 dob) x 0.90 (czynsz, leczenie, sluzba) = budzet koszyka klasy;
    ///  - wynik Planu: praca domowa okregow wsi (KsDistrict.HomeWork - KsDeliver.Produce), czesc zaplacona koszyka wsi i miast (KsBasket.Ids) - koszyk ZC4 w grze tylko zaplacony;
    ///  - linie "KS: pieniadze planu" i "KS: koszyk niezaplacony"; plik ks-miasta.csv (co 7 dob, wiersz na miasto - polka, ceny, zakupy wsi, kiesy wsi).
    /// Zloto nie powstaje: krok 8 tylko liczy, kto za co zaplaci (praca domowa - te same rece i ziemia, nigdy na targ).
    /// </summary>
    internal static partial class StartCalibration
    {
        private const double WagonRatio = 0.90, VilReserve = 0.05, TownKeep = 0.90, VilShareDefault = 0.55;
        private static readonly Dictionary<Settlement, double[]> _earn = new Dictionary<Settlement, double[]>();   // [W, R, P, liczba odczytow]
        private static double _vilShare = -1; private static int _vilShareN, _moneyDay = -1;
        private static Dictionary<string, float[]> _townPaid = new Dictionary<string, float[]>(StringComparer.Ordinal);   // czesc zaplacona koszyka miasta (z Planu; w zapisie)

        private static void MoneyReset()
        {
            _earn.Clear(); _vilShare = -1; _vilShareN = 0; _moneyDay = -1;
            _townPaid = new Dictionary<string, float[]>(StringComparer.Ordinal);
            _ztWeek.Clear(); _vilPurse.Clear(); _csvTownDay = -1;
        }

        /// <summary>Raz na dobe (StartCalibration.Daily, przed VillageTakings.Daily): srednie 28 dob udzialu kiesy wsi w utargu wozow i zarobku netto klas miast.</summary>
        private static void MoneyEma(int today)
        {
            if (_moneyDay == today) return;
            _moneyDay = today;
            long handed, village;
            VillageTakings.SplitToday(out handed, out village);
            if (handed > 1000)
            {
                double r = Math.Max(0.0, Math.Min(1.0, (double)village / handed));
                _vilShareN++;
                double w = 1.0 / Math.Min(28, _vilShareN);
                _vilShare = _vilShare < 0 ? r : _vilShare + (r - _vilShare) * w;
            }
            var net = new double[3];
            foreach (var t in Town.AllTowns)
            {
                if (t == null || !t.IsTown || t.Settlement == null) continue;
                int kind;
                if (!TownFolk.ZcEarnNet(t.Settlement, net, out kind) || kind != 0) continue;
                double[] a;
                if (!_earn.TryGetValue(t.Settlement, out a)) { a = new double[4]; _earn[t.Settlement] = a; }
                a[3]++;
                double w = 1.0 / Math.Min(28, a[3]);
                for (int c = 0; c < 3; c++) a[c] = a[3] <= 1 ? net[c] : a[c] + (net[c] - a[c]) * w;
            }
        }

        /// <summary>Alfa wsi Planu: udzial kiesy wsi w utargu (srednia; bez pomiaru 0.55) x cena wozu / cena polki x (1 - zapas).</summary>
        private static double VilAlphaNow()
        {
            double sh = _vilShareN >= 3 && _vilShare > 0 ? _vilShare : VilShareDefault;
            return Math.Max(0.05, Math.Min(0.95, sh * WagonRatio * (1.0 - VilReserve)));
        }

        private static string MoneyNote()
        {
            return "udzial kiesy wsi w utargu " + (_vilShareN >= 3 && _vilShare > 0 ? _vilShare.ToString("0.000", Inv) + " z " + Math.Min(28, _vilShareN) + " dob" : "bez pomiaru - " + VilShareDefault.ToString("0.00", Inv))
                   + " x cena wozu/polki " + WagonRatio.ToString("0.00", Inv) + " x (1 - zapas " + VilReserve.ToString("0.00", Inv) + ")";
        }

        /// <summary>Budzet koszyka klas W/R/P miasta (zl gry na dobe) z zarobku netto (srednia >= 7 odczytow) x czesc na koszyk; null - bez pomiaru (Plan: placi cale).</summary>
        private static double[] TownBudget(Settlement st)
        {
            double[] a;
            if (st == null || !_earn.TryGetValue(st, out a) || a == null || a[3] < 7) return null;
            return new[] { Math.Max(0.0, a[0]) * TownKeep, Math.Max(0.0, a[1]) * TownKeep, Math.Max(0.0, a[2]) * TownKeep };
        }

        /// <summary>Potrzeby klas W/R/P miasta (kg/d, NeedsOf jednego stanu, bez opalu): W - wyrobnicy i chlopi miasta, R - rzemieslnicy + 40% niewolnych, P - kupcy i szlachta
        /// + 60% niewolnych (jak kiesa ludu i koszt klas ZC4).</summary>
        private static double[][] ClassNeeds(double[] ppl, KsCore.Basket[] bask, bool oil, bool north)
        {
            var cn = new double[3][];
            for (int c = 0; c < 3; c++) cn[c] = new double[KsTab.NN];
            var one = new double[6]; var n = new double[KsTab.NN];
            for (int e = 0; e < 6; e++)
            {
                if (!(ppl[e] > 0)) continue;
                Array.Clear(one, 0, 6); one[e] = ppl[e];
                KsCore.NeedsOf(one, bask, oil, north, true, false, ppl[e], n);
                int c = (e == KsTab.ELabour || e == KsTab.EPeasant) ? 0 : e == KsTab.ECraft ? 1 : (e == KsTab.EMerchant || e == KsTab.EGentry) ? 2 : -1;
                for (int k = 0; k < KsTab.NN; k++)
                {
                    if (c >= 0) cn[c][k] += n[k];
                    else { cn[1][k] += 0.4 * n[k]; cn[2][k] += 0.6 * n[k]; }
                }
            }
            return cn;
        }

        /// <summary>Czesc zaplacona koszyka miasta (KsBasket.Ids) z Planu albo z zapisu; null - cale.</summary>
        private static float[] TownPaidOf(Settlement st)
        {
            float[] p;
            return st != null && st.StringId != null && _townPaid.TryGetValue(st.StringId, out p) ? p : null;
        }

        /// <summary>Wynik Planu: czesc zaplacona koszyka miast do pamieci (gra - Zc4Build).</summary>
        private static void TownPaidFromPlan(KsResult r)
        {
            if (r == null || r.TownPaid == null || r.TownPaid.Count == 0) return;
            _townPaid = new Dictionary<string, float[]>(r.TownPaid, StringComparer.Ordinal);
        }

        // ============================================================ linie Planu
        private static void MoneyLines(KsResult r)
        {
            var M = r.Money;
            if (M == null || !M.On) { Log.Info(PK + "pieniadze planu (krok 8) doba " + _wDay + " | BEZ kroku 8 (brak koszykow wsi i budzetow miast w zrzucie) - popyt Planu = potrzeba."); return; }
            var sb = new StringBuilder(1800);
            sb.Append(PK).Append("pieniadze planu (krok 8) doba ").Append(_wDay).Append(" | rund punktu stalego ").Append(M.Iter).Append(" (zl gry na dobe przy cenach startu):");
            string[] cls = { "W", "R", "P" };
            for (int m = 0; m < 2; m++)
            {
                double drift = M.VBudget[m] - M.VPaid[m];
                sb.Append(" | ").Append(r.Markets[m]).Append(": wsie ").Append(M.VN[m]).Append(" (koszyk ponad budzet ").Append(M.VShort[m]).Append("), sprzedaz wozow ").Append(F0(M.VSales[m]))
                  .Append(", budzet ").Append(F0(M.VBudget[m])).Append(", koszyk ").Append(F0(M.VBasket[m])).Append(", zaplacony ").Append(F0(M.VPaid[m]))
                  .Append(" (").Append(M.VBasket[m] > 0 ? (100 * M.VPaid[m] / M.VBasket[m]).ToString("0", Inv) : "-").Append("%), robione w domu ").Append(F0(M.VHome[m]))
                  .Append(", rece w pracy domowej ").Append(M.VHands[m] > 0 ? (100 * M.VHomeHands[m] / M.VHands[m]).ToString("0.0", Inv) : "-").Append("%")
                  .Append(", kiesy wsi (budzet - zaplacone) ").Append(drift >= 0 ? "+" : "").Append(F0(drift))
                  .Append("; miasta:");
                for (int c = 0; c < 3; c++)
                {
                    double b = M.TBasket[m * 3 + c], pd = M.TPaid[m * 3 + c];
                    sb.Append(' ').Append(cls[c]).Append(' ').Append(b > 0 ? (100 * pd / b).ToString("0.0", Inv) : "-").Append("% (budzet ").Append(F0(M.TBudget[m * 3 + c])).Append(" / koszyk ").Append(F0(b)).Append(')');
                }
                sb.Append("; nadwyzka targu w cenach cienia ").Append(F0(M.Excess[m])).Append(", wspolna praca domowa ").Append(M.Global[m].ToString("0.000", Inv));
            }
            sb.Append(" | praca domowa razem ").Append((r.HomeWorkLabour / 1e6).ToString("0.00", Inv)).Append(" mln rak (te same rece i ziemia, nigdy na targ).");
            Log.Info(sb.ToString());
            var ub = new StringBuilder(1500);
            ub.Append(PK).Append("koszyk niezaplacony (plan doba ").Append(_wDay).Append(", szt. gry/d; wsie - robione w domu, miasta - odpada z popytu):");
            double sigma = Math.Max(1, DailyLife.KsSigma);
            for (int m = 0; m < 2; m++)
            {
                ub.Append(" | ").Append(r.Markets[m]).Append(" wsie:");
                bool f = true;
                for (int z = 0; z < ZN; z++)
                {
                    double pcs = M.UnpaidV[m * ZN + z] / (sigma * ZKg(z));
                    if (pcs < 0.5) continue;
                    ub.Append(f ? " " : ", ").Append(ZIds[z]).Append(' ').Append(F0(pcs)); f = false;
                }
                if (f) ub.Append(" -");
                ub.Append("; miasta:");
                f = true;
                for (int z = 0; z < ZN; z++)
                {
                    double pcs = M.UnpaidT[m * ZN + z] / (sigma * ZKg(z));
                    if (pcs < 0.5) continue;
                    ub.Append(f ? " " : ", ").Append(ZIds[z]).Append(' ').Append(F0(pcs)); f = false;
                }
                if (f) ub.Append(" -");
            }
            ub.Append('.');
            Log.Info(ub.ToString());
        }

        // ============================================================ ks-miasta.csv (co 7 dob, stan zastosowany)
        private const string TownCsvName = "ks-miasta.csv";
        private const string TownCsvHeader = "dzien;miasto_id;miasto;rynek;lambda;ludzie;indeks_jedzenia_mediana;jedzenie_na_suficie;jedzenie_na_podlodze;zboze_indeks;zboze_polka_szt;zboze_popyt_zaplacony_szt_d;zboze_dni_polki;"
                                           + "czesc_zaplacona_koszyka_miasta;wsie_chciane_szt_7d;wsie_kupione_szt_7d;wsie_bez_kiesy_szt_7d;wsie_bez_towaru_szt_7d;wsie_w_innym_miescie_szt_7d;kiesy_wsi_zl;kiesy_wsi_zmiana_7d_zl";
        private static readonly Dictionary<Settlement, double[]> _ztWeek = new Dictionary<Settlement, double[]>();   // [chciane, kupione, bez kiesy, bez towaru, w innym miescie]
        private static readonly Dictionary<Settlement, long> _vilPurse = new Dictionary<Settlement, long>();
        private static int _csvTownDay = -1;
        private static readonly int[] FoodItems = { IGrain, KsDeliver.IFish, KsDeliver.ICheese, KsDeliver.IButter, KsDeliver.IOlives, KsDeliver.IGrape, KsDeliver.IDate, KsDeliver.ISalt, IMeat, KsDeliver.IOrange };

        /// <summary>TownFolk.Zc4BuyVillage: zakupy wsi wedlug miasta targowego (sztuki: chciane, kupione, bez kiesy, bez towaru, kupione w innym miescie).</summary>
        internal static void Zc4NoteTown(Settlement mk, double want, double got, double unGold, double noGoods, double elsewhere)
        {
            _zvElse += elsewhere;
            if (mk == null) return;
            double[] a;
            if (!_ztWeek.TryGetValue(mk, out a)) { a = new double[5]; _ztWeek[mk] = a; }
            a[0] += want; a[1] += got; a[2] += unGold; a[3] += noGoods; a[4] += elsewhere;
        }

        /// <summary>Plik ks-miasta.csv co 7 dob (od D): wiersz na miasto - indeksy cen jedzenia KS (mediana, na suficie / podlodze), polka zboza w dniach zaplaconego popytu,
        /// czesc zaplacona koszyka, zakupy wsi z ostatnich 7 dob, kiesy wsi tego targu i ich zmiana.</summary>
        private static void TownsCsv(int day)
        {
            if (day % 7 != 0 || day == _csvTownDay || !LogGate.Lines) return;
            _csvTownDay = day;
            EnsureItems(); Zc4EnsureMap();
            var purse = new Dictionary<Settlement, long>();
            foreach (var v in Village.All)
            {
                if (v == null || v.Settlement == null) continue;
                var mk = v.TradeBound ?? v.Bound;
                if (mk == null) continue;
                long g0; purse.TryGetValue(mk, out g0); purse[mk] = g0 + Math.Max(0, v.Gold);
            }
            var map = _zt;
            var sb = new StringBuilder(map.Count * 260);
            var ix = new List<double>(12);
            int rows = 0;
            foreach (var t in Town.AllTowns)
            {
                try
                {
                    if (t == null || !t.IsTown || t.Settlement == null) continue;
                    var st = t.Settlement;
                    ZTown zt; map.TryGetValue(st, out zt);
                    ix.Clear(); int ceil = 0, floor = 0;
                    foreach (int i in FoodItems)
                    {
                        var it = _items[i]; if (it == null || it.ItemCategory == null) continue;
                        double x = 1.0; try { x = t.GetItemCategoryPriceIndex(it.ItemCategory); } catch { x = 1.0; }
                        ix.Add(x); if (x >= CeilIx) ceil++; else if (x <= FloorIx) floor++;
                    }
                    double med = ix.Count > 0 ? Med(ix) : 0;
                    var gi = _items[IGrain];
                    double gx = 1.0; try { if (gi != null) gx = t.GetItemCategoryPriceIndex(gi.ItemCategory); } catch { gx = 1.0; }
                    int gs = gi != null && st.ItemRoster != null ? st.ItemRoster.GetItemNumber(gi) : 0;
                    double gd = zt != null ? zt.Lam * (zt.Q[ZGrain] + zt.QV[ZGrain] + zt.Der[ZGrain]) + zt.One[ZGrain] : 0;
                    var tp = TownPaidOf(st);
                    double tpf = 1; if (tp != null) { tpf = 0; foreach (var x in tp) tpf += x; tpf /= tp.Length; }
                    double[] w; _ztWeek.TryGetValue(st, out w);
                    long pv; purse.TryGetValue(st, out pv);
                    long p0; bool had = _vilPurse.TryGetValue(st, out p0);
                    string name = st.Name != null ? st.Name.ToString().Replace(';', ',') : st.StringId;
                    sb.Append(day).Append(';').Append(st.StringId).Append(';').Append(name).Append(';').Append(TownMarket(t) == 0 ? "Westeros" : "Essos").Append(';')
                      .Append(zt != null ? zt.Lam.ToString("0.00", Inv) : "-").Append(';').Append(zt != null ? F0(zt.People) : "-").Append(';').Append(med.ToString("0.00", Inv)).Append(';')
                      .Append(ceil).Append(';').Append(floor).Append(';').Append(gx.ToString("0.00", Inv)).Append(';').Append(gs).Append(';').Append(gd.ToString("0.0", Inv)).Append(';')
                      .Append(gd > 0 ? (gs / gd).ToString("0.0", Inv) : "-").Append(';').Append(tpf.ToString("0.000", Inv)).Append(';')
                      .Append(w != null ? F0(w[0]) : "0").Append(';').Append(w != null ? F0(w[1]) : "0").Append(';').Append(w != null ? F0(w[2]) : "0").Append(';')
                      .Append(w != null ? F0(w[3]) : "0").Append(';').Append(w != null ? F0(w[4]) : "0").Append(';').Append(pv).Append(';').Append(had ? (pv - p0).ToString(Inv) : "-")
                      .Append(Environment.NewLine);
                    rows++;
                }
                catch (Exception e) { Stumble("TownsCsv(miasto)", e); }
            }
            _ztWeek.Clear();
            _vilPurse.Clear(); foreach (var kv in purse) _vilPurse[kv.Key] = kv.Value;
            string path = Log.Csv(TownCsvName, TownCsvHeader, sb.ToString());
            Log.Info(PK + TownCsvName + " " + (path != null ? rows + " wierszy (doba " + day + ")" : "NIE ZAPISANY (log wylaczony albo brak katalogu sesji)") + ".");
        }

        /// <summary>Kiesy wsi zastosowanych razem (zl) - linia ZC4 (NKS-13: dryf kies wsi w oknie odbioru).</summary>
        private static long AppliedVillagePurses()
        {
            long s = 0;
            var r = _rates;
            foreach (var v in r.Keys) if (v != null) s += Math.Max(0, v.Gold);
            return s;
        }
    }
}
