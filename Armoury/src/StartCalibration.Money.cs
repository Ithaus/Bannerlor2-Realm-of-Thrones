using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// KS - KROK 8 "PIENIADZE" (poprawka po autotescie 16.10; projekt rozdz. 9 i 11.1 krok 8). Pomiar na watku glownym do zrzutu Planu i liczby stanu zastosowanego:
    ///  - ALFA OKREGU (recenzja 17.10, wazne - dotad jedna liczba swiata z udzialu kiesy w utargu wozow, nakladana na wartosc CALEJ nadwyzki KS przy cenach startu):
    ///    alfa wsi = udzial kiesy tej wsi w jej utargu (srednia 28 dob z VillageTakings: tabory, sprzedaz z magazynu wsi, sakwy rozbitych taborow - dekret pana 50/70/90%
    ///    widac wprost; wies z malym utargiem - udzial swiata) x utarg swiata / wartosc bazowa produkcji wsi swiata (ksiega towarow, zrodlo "wsie", wartosc sztuki - ile
    ///    utargu naprawde przynosi towar wsi: czesc sprzedana i ceny) x (1 - 5% na oplaty stale, daniny i dziesiecine); Plan mnozy ja przez wartosc BAZOWA sprzedazy
    ///    okregu (ta sama podstawa);
    ///  - ZAROBEK NETTO KLAS MIAST W/R/P (TownFolk.ZcEarnNet - po podatku pana, cechu i dziesiecinie; srednia 28 dob) x 0.90 (czynsz, leczenie, sluzba) = budzet koszyka klasy;
    ///  - w zapisie stanu zastosowanego (recenzja 17.10): srednie alfy (swiat, wsie) i zarobku miast - pierwszy Plan po wczytaniu liczy z pomiaru; Plan bez pomiaru
    ///    (MoneyMeasured = false) nie zastepuje pracy domowej i czesci zaplaconej zapisanych w okregach;
    ///  - wynik Planu: praca domowa okregow wsi (KsDistrict.HomeWork - KsDeliver.Produce), czesc zaplacona koszyka wsi i miast (KsBasket.Ids), czesc kcal robiona w domu;
    ///  - linie "KS: pieniadze planu" i "KS: koszyk niezaplacony"; plik ks-miasta.csv (co 7 dob, wiersz na miasto - polka, ceny, zakupy wsi, kiesy wsi).
    /// Zloto nie powstaje: krok 8 tylko liczy, kto za co zaplaci (praca domowa - te same rece i ziemia, nigdy na targ).
    /// </summary>
    internal static partial class StartCalibration
    {
        private const double WagonRatio = 0.90, VilReserve = 0.05, TownKeep = 0.90, VilShareDefault = 0.55;
        private const double VilTakeMin = 300.0;   // wies z utargiem 28 dob ponizej - udzial swiata (za malo powrotow taborow)
        private static readonly Dictionary<Settlement, double[]> _earn = new Dictionary<Settlement, double[]>();   // [W, R, P, liczba odczytow]
        private static double _vilShare = -1; private static int _vilShareN, _moneyDay = -1;
        private static Dictionary<string, float[]> _townPaid = new Dictionary<string, float[]>(StringComparer.Ordinal);   // czesc zaplacona koszyka miasta (z Planu; w zapisie)
        // recenzja 17.10: srednie 28 dob utargu wedlug wsi [oddane, do kiesy] (zl/d), utargu swiata i wartosci bazowej produkcji wsi swiata (ksiega towarow) - alfa okregu
        private static readonly Dictionary<Village, double[]> _vilTake = new Dictionary<Village, double[]>();
        private static double _phiHand, _phiProd; private static int _vtDays, _phiN, _phiLedgerDay = -1;
        private static readonly Dictionary<string, int> _valCache = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly double[] _inflowDay = new double[2];      // wplyw do kies wsi doby wedlug rynku (linia ZC4; NKS-13 - budzet Planu wobec pomiaru)

        private static void MoneyReset()
        {
            _earn.Clear(); _vilShare = -1; _vilShareN = 0; _moneyDay = -1;
            _townPaid = new Dictionary<string, float[]>(StringComparer.Ordinal);
            _ztWeek.Clear(); _vilPurse.Clear(); _csvTownDay = -1;
            _vilTake.Clear(); _phiHand = _phiProd = 0; _vtDays = _phiN = 0; _phiLedgerDay = -1; _valCache.Clear(); _inflowDay[0] = _inflowDay[1] = 0;
            _earlyDone = false;
        }

        /// <summary>Raz na dobe (StartCalibration.Daily, przed VillageTakings.Daily): srednie 28 dob udzialu kiesy wsi w utargu (swiat i kazda wies), utargu swiata wobec
        /// wartosci bazowej produkcji wsi (ksiega towarow) i zarobku netto klas miast.</summary>
        private static readonly double[] _regDay = new double[6], _regEma = new double[6];   // recenzja runda 3: utarg / panowie wedlug krain importu, eksportu, reszty
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
            // utarg wedlug wsi (srednia 28 dob, dni bez powrotu = 0) i wplyw doby wedlug rynku
            _vtDays++;
            double wv = 1.0 / Math.Min(28, _vtDays);
            var seen = new HashSet<Village>();
            _inflowDay[0] = _inflowDay[1] = 0;
            T4Begin();                                          // ks-koszt T4: gradient krainy miasta targowego z pamieci doby (klucz Ks Cost Fast; bez pamieci - wprost)
            VillageTakings.ForEachVillageToday((v, h, k) =>
            {
                double[] a;
                if (!_vilTake.TryGetValue(v, out a)) { a = new double[2]; _vilTake[v] = a; }
                a[0] += (h - a[0]) * wv; a[1] += (k - a[1]) * wv;
                seen.Add(v);
                int m = MarketOfSettlement(v.Settlement);
                if (m >= 0 && m < 2) _inflowDay[m] += k;
                // recenzja runda 3 (drobne, INFO): utarg wozow i czesc panow (oddane - do kiesy) wedlug krain importu / eksportu zboza (gradient krainy z Planu)
                try
                {
                    Settlement mk = TradeTownOf(v); double gz = T4LandGrad(mk, ZGrain);
                    int c = gz > 1.02 ? 0 : (gz < 0.98 ? 1 : 2);
                    _regDay[c * 2] += h; _regDay[c * 2 + 1] += Math.Max(0, h - k);
                }
                catch { }
            });
            for (int i = 0; i < 6; i++) { _regEma[i] = _vtDays <= 1 ? _regDay[i] : _regEma[i] + (_regDay[i] - _regEma[i]) / 7.0; _regDay[i] = 0; }
            if (today % 7 == 0 && LogGate.Lines && KeyOn)
                Log.Info(PK + "utarg wsi wedlug krain dzien " + (today - 1) + " (srednia 7 dob; kraina importu zboza: gradient zboza Planu > 1.02, eksportu < 0.98) | import: utarg " + F0(_regEma[0]) + " zl/d, panowie " + F0(_regEma[1])
                         + " | eksport: utarg " + F0(_regEma[2]) + ", panowie " + F0(_regEma[3]) + " | reszta: utarg " + F0(_regEma[4]) + ", panowie " + F0(_regEma[5]) + " (INFO - Jeff przed odbiorem: KS-H przenosi zawor kas miast)" + ".");
            foreach (var kv in _vilTake) if (!seen.Contains(kv.Key)) { kv.Value[0] *= 1.0 - wv; kv.Value[1] *= 1.0 - wv; }
            VilOutDaily(wv);                                    // KS-R3 P2: wydatki wsi spoza koszyka i fracht KS-H (ta sama waga co utarg)
            // utarg swiata wobec wartosci bazowej produkcji wsi (ksiega towarow - doba wczorajsza, raz na nowa dobe ksiegi)
            int ld = _emaLastDay;
            if (ld >= 0 && ld != _phiLedgerDay && _ledgerTmp.Count > 0)
            {
                _phiLedgerDay = ld;
                double pv = 0;
                foreach (var kv in _ledgerTmp)
                {
                    if (kv.Value == null || kv.Value.Length <= GoodsLedger.KsVil) continue;
                    double n = kv.Value[GoodsLedger.KsVil];
                    if (!(n > 0)) continue;
                    int val;
                    if (!_valCache.TryGetValue(kv.Key, out val)) { var it = ItemOf(kv.Key); val = it != null ? Math.Max(0, it.Value) : 0; _valCache[kv.Key] = val; }
                    pv += n * val;
                }
                long hall = VillageTakings.HandedAllToday;
                if (pv > 1000 && hall > 0)
                {
                    _phiN++;
                    double w = 1.0 / Math.Min(28, _phiN);
                    if (_phiN == 1) { _phiHand = hall; _phiProd = pv; }
                    else { _phiHand += (hall - _phiHand) * w; _phiProd += (pv - _phiProd) * w; }
                }
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

        /// <summary>Utarg swiata / wartosc bazowa produkcji wsi swiata (srednie 28 dob; bez pomiaru - cena wozu / cena polki 0.90).</summary>
        private static double PhiNow() { return _phiN >= 7 && _phiProd > 0 ? Math.Max(0.05, Math.Min(3.0, _phiHand / _phiProd)) : WagonRatio; }

        /// <summary>Udzial kiesy wsi w utargu swiata (srednia; bez pomiaru 0.55).</summary>
        private static double ShareNow() { return _vilShareN >= 3 && _vilShare > 0 ? _vilShare : VilShareDefault; }

        /// <summary>Alfa wsi Planu (domyslna rynku): udzial kiesy wsi swiata x utarg / wartosc bazowa produkcji wsi x (1 - zapas).</summary>
        private static double VilAlphaNow() { return Math.Max(0.02, Math.Min(0.95, ShareNow() * PhiNow() * (1.0 - VilReserve))); }

        /// <summary>Alfa okregu wsi v (recenzja 17.10): udzial kiesy TEJ wsi w jej utargu (28 dob; dekret pana wprost) x utarg / wartosc bazowa produkcji wsi swiata x (1 - zapas);
        /// wies z utargiem 28 dob ponizej 300 zl - -1 (Plan bierze alfe rynku).</summary>
        internal static int _alphaDsN;   // KS r4: wsi z alfa z praw DS w ostatnim Planie (licznik linii)
        private static double VilAlphaFor(Village v)
        {
            double[] a;
            if (v == null || !_vilTake.TryGetValue(v, out a) || a == null || _vtDays < 7 || a[0] * Math.Min(28, _vtDays) < VilTakeMin || !(a[0] > 0)) return -1;
            if (v.Settlement != null && DsVillage.On(v.Settlement))
            {
                // KS runda 4 (17.10, STAN-PRAC 68/72): ALFA Z PRAW DS - budzet koszyka = czesc wsi z oddanego utargu po panu, dziesiecinie (polowa wraca jako jalmuzna)
                // i udziale korony w wojnie (DsVillage.BudgetShare) minus czynsz staly dnia; bez poborow resztowych, wiec bez petli alfa -> 0 (runda 3: 0.47 -> 0.00 w 5 Planach)
                double baseDs = a[0] / Math.Max(1e-6, PhiNow());
                _alphaDsN++;
                return Math.Max(0.0, Math.Min(0.95, VilBasketMoney(v, a) / Math.Max(1e-6, baseDs)));   // m103 wsie: licznik w VilBasketMoney (te same dzialania)
            }
            if (SwAlphaNet)
            {
                // KS-R3 P2 (klucz Ks Alpha Net): ALFA NETTO = 0.95 x (wplyw do kiesy + fracht KS-H - wydatki spoza koszyka: danina wojenna, rynek wsi BK, oplata stala,
                // stara lista, podatek od nadwyzki) / wartosc bazowa produkcji wsi (utarg / Phi) - Plan daje wsi tylko pieniadze, ktore naprawde zostaja na koszyk
                double baseV = a[0] / Math.Max(1e-6, PhiNow());
                return Math.Max(0.0, Math.Min(0.95, VilBasketMoney(v, a) / Math.Max(1e-6, baseV)));   // m103 wsie: licznik w VilBasketMoney (te same dzialania)
            }
            double sh = Math.Max(0.0, Math.Min(1.0, a[1] / a[0]));
            return Math.Max(0.0, Math.Min(0.95, sh * PhiNow() * (1.0 - VilReserve)));
        }

        /// <summary>Pieniadze wsi v na koszyk na dobe (zl/d, srednie 28 dob utargu a = [oddane, do kiesy]) - LICZNIK ALFY Planu (VilAlphaFor dzieli go przez wartosc bazowa):
        /// wies DS - (1 - zapas) x (udzial z praw DS x oddane - czynsz staly) (alfa z praw DS: bez poborow od oszczednosci - danina od skarbca i podatek od nadwyzki biora
        /// z zapasu, nie z dochodu); klucz Ks Alpha Net - 0.95 x (do kiesy + fracht KS-H - wydatki spoza koszyka); inaczej (1 - zapas) x do kiesy (najwyzej oddane).
        /// m103 wsie (21.10): jedna funkcja dla Planu i dla budzetu drabiny 1 w grze (TownFolk.ShelfBudget - Zc4VillageNetDay).</summary>
        private static double VilBasketMoney(Village v, double[] a)
        {
            double a0 = a != null ? a[0] : 0.0, a1 = a != null ? a[1] : 0.0;
            if (v != null && v.Settlement != null && DsVillage.On(v.Settlement))
            {
                double shDs = DsVillage.BudgetShare(v, v.Settlement.MapFaction as Kingdom, MineRegale.KsShareOfTakings(v, a0));   // R3-09f: regale gornicze poza modelem podatku
                double rentDs = DsVillage.RentPerDay(v);
                return (1.0 - VilReserve) * Math.Max(0.0, shDs * a0 - rentDs);
            }
            if (SwAlphaNet)
            {
                double[] o; if (v == null || !_vilOut.TryGetValue(v, out o)) o = null;
                double inflow = a1 + (o != null ? o[1] : 0.0), outflow = o != null ? o[0] : 0.0;
                return 0.95 * Math.Max(0.0, inflow - outflow);
            }
            return (1.0 - VilReserve) * Math.Max(0.0, Math.Min(a1, a0));
        }

        /// <summary>m103 wsie (klucz Ks Village Basket At Shelf; TownFolk.ShelfBudget): pieniadze wsi v na koszyk z biezacego utargu (zl/d) - ten sam licznik co alfa
        /// Planu tej wsi (VilBasketMoney), liczony na srednich 28 dob dzis. false - bez pomiaru (mniej niz 7 dob utargu w sesji i w zapisie). Wies bez wpisu utargu przy
        /// pomiarze - 0 (od 28 dob nic nie oddala; zostaje ziarno i sol). Bez progu VilTakeMin: tu kwota, nie udzial (prog chroni tylko stosunek alfy). Nie rzuca.</summary>
        internal static bool Zc4VillageNetDay(Village v, out double net)
        {
            net = 0;
            try
            {
                if (v == null || _vtDays < 7) return false;
                double[] a; _vilTake.TryGetValue(v, out a);
                net = VilBasketMoney(v, a);
                if (double.IsNaN(net) || double.IsInfinity(net) || net < 0) { net = 0; return false; }
                return true;
            }
            catch (Exception e) { Stumble("Zc4VillageNetDay", e); net = 0; return false; }
        }

        /// <summary>KS-R3 P2: wydatki spoza koszyka i fracht wsi swiata (srednie 28 dob, zl/d) - linia pieniedzy Planu.</summary>
        private static void VilOutTotals(out double outflow, out double freight, out int n)
        {
            outflow = freight = 0; n = 0;
            foreach (var kv in _vilOut) { if (kv.Value == null) continue; outflow += kv.Value[0]; freight += kv.Value[1]; n++; }
        }

        /// <summary>Krok 8 Planu z pomiaru: >= 7 dob utargu wsi i jego wartosci bazowej, >= 3 doby udzialu kiesy, budzety >= 90% miast.</summary>
        private static bool MoneyMeasuredNow(int towns, int budgets) { return _vtDays >= 7 && _phiN >= 7 && _vilShareN >= 3 && towns > 0 && budgets >= 0.9 * towns; }

        private static string MoneyNote()
        {
            int nv = 0; foreach (var kv in _vilTake) if (kv.Value != null && kv.Value[0] * Math.Min(28, Math.Max(1, _vtDays)) >= VilTakeMin) nv++;
            return "udzial kiesy wsi w utargu swiata " + (_vilShareN >= 3 && _vilShare > 0 ? _vilShare.ToString("0.000", Inv) + " z " + Math.Min(28, _vilShareN) + " dob" : "bez pomiaru - " + VilShareDefault.ToString("0.00", Inv))
                   + " x utarg / wartosc bazowa produkcji wsi " + (_phiN >= 7 && _phiProd > 0 ? PhiNow().ToString("0.000", Inv) + " z " + Math.Min(28, _phiN) + " dob (" + F0(_phiHand) + " / " + F0(_phiProd) + " zl/d)" : "bez pomiaru - " + WagonRatio.ToString("0.00", Inv))
                   + " x (1 - zapas " + VilReserve.ToString("0.00", Inv) + "); wsi z wlasnym udzialem " + nv + " (utarg 28 dob >= " + VilTakeMin.ToString("0", Inv) + " zl)";
        }

        /// <summary>Budzet koszyka klas W/R/P miasta (zl gry na dobe) z zarobku netto (srednia >= 7 odczytow) x czesc na koszyk; null - bez pomiaru (Plan: placi cale).</summary>
        private static double[] TownBudget(Settlement st)
        {
            double[] a;
            if (st == null || !_earn.TryGetValue(st, out a) || a == null || a[3] < 7) return null;
            return new[] { Math.Max(0.0, a[0]) * TownKeep, Math.Max(0.0, a[1]) * TownKeep, Math.Max(0.0, a[2]) * TownKeep };
        }

        // ============================================================ pomiar pieniedzy w zapisie stanu zastosowanego (recenzja 17.10, wazne)
        // wpisy cial okregow "@$M~..." (swiat), "@$V~wies~oddane~do kiesy", "@$E~miasto~W~R~P~n" - mniej niz 7 pol: starsza DLL czyta "@" jako czesc zaplacona miasta i pomija
        private static void MoneyStateExport(StringBuilder sb, ref bool firstD)
        {
            if (!firstD) sb.Append(';');
            firstD = false;
            sb.Append("@$M~").Append(_vtDays).Append('~').Append(_phiN).Append('~').Append(_phiHand.ToString("0.#", Inv)).Append('~').Append(_phiProd.ToString("0.#", Inv))
              .Append('~').Append(_vilShareN).Append(',').Append(_vilShare.ToString("0.#####", Inv));
            foreach (var kv in _vilTake)
            {
                var v = kv.Key; var a = kv.Value;
                if (v == null || v.Settlement == null || v.Settlement.StringId == null || a == null || !(a[0] > 0.01 || a[1] > 0.01)) continue;
                sb.Append(';').Append("@$V~").Append(Clean(v.Settlement.StringId)).Append('~').Append(a[0].ToString("0.##", Inv)).Append('~').Append(a[1].ToString("0.##", Inv));
            }
            foreach (var kv in _earn)
            {
                var st = kv.Key; var a = kv.Value;
                if (st == null || st.StringId == null || a == null) continue;
                sb.Append(';').Append("@$E~").Append(Clean(st.StringId)).Append('~').Append(a[0].ToString("0.#", Inv)).Append('~').Append(a[1].ToString("0.#", Inv)).Append('~')
                  .Append(a[2].ToString("0.#", Inv)).Append('~').Append(((int)Math.Min(28, a[3])).ToString(Inv));
            }
        }

        /// <summary>Odczyt wpisu "@$..." (ImportLive). Nie rzuca na zly wpis - pomija go.</summary>
        private static void MoneyStateImport(string[] f)
        {
            try
            {
                if (f == null || f.Length < 2) return;
                if (f[0] == "@$M" && f.Length >= 6)
                {
                    int.TryParse(f[1], NumberStyles.Integer, Inv, out _vtDays); int.TryParse(f[2], NumberStyles.Integer, Inv, out _phiN);
                    double.TryParse(f[3], NumberStyles.Float, Inv, out _phiHand); double.TryParse(f[4], NumberStyles.Float, Inv, out _phiProd);
                    var q = f[5].Split(',');
                    if (q.Length == 2) { int.TryParse(q[0], NumberStyles.Integer, Inv, out _vilShareN); double vs; if (double.TryParse(q[1], NumberStyles.Float, Inv, out vs)) _vilShare = vs; }
                    _vtDays = Math.Max(0, Math.Min(28, _vtDays)); _phiN = Math.Max(0, Math.Min(28, _phiN)); _vilShareN = Math.Max(0, Math.Min(28, _vilShareN));
                }
                else if (f[0] == "@$V" && f.Length >= 4)
                {
                    var st = Settlement.Find(f[1]); var v = st != null ? st.Village : null;
                    double h, k;
                    if (v != null && double.TryParse(f[2], NumberStyles.Float, Inv, out h) && double.TryParse(f[3], NumberStyles.Float, Inv, out k) && h >= 0 && k >= 0) _vilTake[v] = new[] { h, k };
                }
                else if (f[0] == "@$E" && f.Length >= 6)
                {
                    var st = Settlement.Find(f[1]);
                    double w, r, p; int n;
                    if (st != null && st.IsTown && double.TryParse(f[2], NumberStyles.Float, Inv, out w) && double.TryParse(f[3], NumberStyles.Float, Inv, out r) && double.TryParse(f[4], NumberStyles.Float, Inv, out p)
                        && int.TryParse(f[5], NumberStyles.Integer, Inv, out n) && n > 0)
                        _earn[st] = new[] { w, r, p, Math.Min(28, n) };
                }
            }
            catch (Exception e) { Stumble("MoneyStateImport", e); }
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

        /// <summary>Wynik Planu: czesc zaplacona koszyka miast do pamieci (gra - Zc4Build). Recenzja 17.10: w stanie zastosowanym tylko z Planu z dojrzala ksiega i krokiem 8
        /// z pomiaru (inaczej zostaje czesc z zapisu albo z poprzedniego Planu).</summary>
        private static void TownPaidFromPlan(KsResult r)
        {
            if (r == null || r.TownPaid == null || r.TownPaid.Count == 0) return;
            if (_state == StateApplied && (!r.MoneyMeasured || _wEma < MinLedgerDays)) return;
            _townPaid = new Dictionary<string, float[]>(r.TownPaid, StringComparer.Ordinal);
            _townExtra = new Dictionary<string, float[]>(r.TownExtra ?? new Dictionary<string, float[]>(), StringComparer.Ordinal);   // r4-koszyk: dokupiona podstawa (ten sam Plan)
        }

        private static string P1(double x) { return double.IsNaN(x) || double.IsInfinity(x) ? "-" : x.ToString("0.0", Inv); }

        // ============================================================ linie Planu
        private static void MoneyLines(KsResult r)
        {
            var M = r.Money;
            if (M == null || !M.On) { Log.Info(PK + "pieniadze planu (krok 8) doba " + _wDay + " | BEZ kroku 8 (brak koszykow wsi i budzetow miast w zrzucie) - popyt Planu = potrzeba."); return; }
            var sb = new StringBuilder(2600);
            sb.Append(PK).Append("pieniadze planu (krok 8) doba ").Append(_wDay).Append(" | rund punktu stalego ").Append(M.Iter)
              .Append(", zbiezny Westeros ").Append(M.Conv[0] ? "TAK" : "NIE").Append(M.NoHands[0] ? " (brak rak do oddania)" : "").Append(", Essos ").Append(M.Conv[1] ? "TAK" : "NIE").Append(M.NoHands[1] ? " (brak rak do oddania)" : "")
              .Append(" | z pomiaru ").Append(r.MoneyMeasured ? "TAK" : "NIE (wynik nie zastepuje zapisanego w okregach)").Append(", margines popytu targu x").Append(M.Margin.ToString("0.00", Inv))
              .Append(" (zl gry na dobe; sprzedaz i budzet w wartosci bazowej, koszyk przy cenach startu):");
            string[] cls = { "W", "R", "P" };
            for (int m = 0; m < 2; m++)
            {
                double cut = M.VSalesNoHw[m] > 0 ? 100.0 * (1.0 - M.VSalesB[m] / M.VSalesNoHw[m]) : 0.0;
                double fk = Math.Max(1e-9, M.VFullKcal[m]);
                sb.Append(" | ").Append(r.Markets[m]).Append(": wsie ").Append(M.VN[m]).Append(" (koszyk ponad budzet ").Append(M.VShort[m]).Append("), sprzedaz wsi na targ ").Append(F0(M.VSalesB[m]))
                  .Append(" (konie ").Append(F0(M.VHorse[m])).Append("; bez pracy domowej ").Append(F0(M.VSalesNoHw[m])).Append(", mniej o ").Append(P1(cut)).Append("%)")
                  .Append(", budzet ").Append(F0(M.VBudget[m])).Append(" (alfa ").Append(M.AlphaMin[m].ToString("0.00", Inv)).Append('/').Append(M.AlphaMed[m].ToString("0.00", Inv)).Append('/').Append(M.AlphaMax[m].ToString("0.00", Inv))
                  .Append(", z pomiaru wsi ").Append(M.AlphaN[m]).Append(')')
                  .Append(", koszyk ").Append(F0(M.VBasket[m])).Append(", zaplacony ").Append(F0(M.VPaid[m]))
                  .Append(" (").Append(M.VBasket[m] > 0 ? (100 * M.VPaid[m] / M.VBasket[m]).ToString("0", Inv) : "-").Append("%), robione w domu ").Append(F0(M.VHome[m]))
                  .Append(", rece w pracy domowej ").Append(M.VHands[m] > 0 ? (100 * M.VHomeHands[m] / M.VHands[m]).ToString("0.0", Inv) : "-").Append("%")
                  .Append(", kcal koszyka: niezaplacone ").Append(P1(100 * M.VUnpaidKcal[m] / fk)).Append("%, robione w domu ").Append(P1(100 * M.VHomeKcal[m] / fk)).Append("%, bez pokrycia ")
                  .Append(P1(100 * Math.Max(0.0, M.VUnpaidKcal[m] - M.VHomeKcal[m]) / fk)).Append("%")
                  .Append("; miasta:");
                for (int c = 0; c < 3; c++)
                {
                    double b = M.TBasket[m * 3 + c], pd = M.TPaid[m * 3 + c];
                    sb.Append(' ').Append(cls[c]).Append(' ').Append(b > 0 ? (100 * pd / b).ToString("0.0", Inv) : "-").Append("% (budzet ").Append(F0(M.TBudget[m * 3 + c])).Append(" / koszyk ").Append(F0(b)).Append(')');
                }
                sb.Append("; nadwyzka targu w cenach cienia ").Append(F0(M.Excess[m])).Append(" (").Append(P1(100 * M.ExcessRel[m])).Append("% popytu targu), poprawka rynku G srednio ").Append(M.Global[m].ToString("0.000", Inv))
                  .Append(", zmiana sum w ostatniej rundzie ").Append(M.DTot[m] >= 0 ? P1(100 * M.DTot[m]) + "%" : "-").Append(" (okregi ze zmiana pracy domowej >= 0.05: ").Append(M.DHwN[m]).Append(')');
                // KS-R3: kcal zastepcze kroku 8b (wlasny towar za pozycje spoza HomeMask), Plan bez pokrycia (cel W <= 3.0%, E <= 3.5%), czynnik popytu targu, regula zasiegu
                sb.Append("; KS-R3: kcal zastepcze 8b ").Append(P1(100 * M.VSubKcal[m] / fk)).Append("%, Plan bez pokrycia ").Append(P1(100 * Math.Max(0.0, M.VUnpaidKcal[m] - M.VHomeKcal[m]) / fk))
                  .Append("% (cel ").Append(m == 0 ? "3.0" : "3.5").Append("%), czynnik popytu targu ").Append(M.MfMin[m].ToString("0.000", Inv)).Append('-').Append(M.MfMax[m].ToString("0.000", Inv))
                  .Append(", wsie z wycietymi psujacymi sie ").Append(M.ReachCut[m])
                  // KS-R3 (port symulatora 17.10, runda 3) krok 8d: ubocze (jaja, miod, marchew, jablka, dziczyzna) ponad popyt zaplacony targu zostaje na stole okregu
                  .Append(", stol 8d ").Append(F0(M.TableKg[m])).Append(" kg Planu/d w ").Append(M.TableN[m]).Append(" okregach (lepszy stol: potrzeba i wlasny stol w gore, bez zaliczenia do pracy domowej; krok 8 liczony ponownie)")
                  // recenzja runda 3: towar zastepczy 8b/8c z plonu rak (okregi, gdzie parytet wartosci dawal wiecej niz plon; kcal zastepcze bez wolnej ziemi)
                  .Append(", 8b z plonu: parytet wartosci > plon w ").Append(M.SubPhysOver[m]).Append(" okregach, kcal bez ziemi ").Append(P1(100 * M.SubLandShort[m] / fk)).Append('%');
            }
            double vo, vf; int vn; VilOutTotals(out vo, out vf, out vn);
            sb.Append(" | alfa ").Append(SwAlphaNet ? "NETTO (wplyw + fracht KS-H - wydatki spoza koszyka)" : "z utargu (klucz Ks Alpha Net wylaczony)").Append(": wydatki wsi spoza koszyka ").Append(F0(vo))
              .Append(" zl/d, fracht KS-H do wsi ").Append(F0(vf)).Append(" zl/d (wsi z liczbami ").Append(vn).Append(')');
            // recenzja runda 3 (wazne): Plan bez pokrycia wedlug krainy (kraina miasta targowego okregu; najwiecej kcal bez pokrycia)
            if (M.LandKcal != null && M.LandKcal.Count > 0)
            {
                var ll = new List<KeyValuePair<string, double[]>>(M.LandKcal);
                ll.Sort((a, b) => b.Value[1].CompareTo(a.Value[1]));
                sb.Append(" | Plan bez pokrycia wedlug krainy (kcal bez pokrycia / kcal koszyka):");
                for (int i = 0; i < ll.Count && i < 10; i++)
                {
                    var kv = ll[i]; if (!(kv.Value[1] > 0)) break;
                    sb.Append(i == 0 ? " " : ", ").Append(kv.Value[2] == 0 ? "W " : "E ").Append(kv.Key).Append(' ').Append(P1(100 * kv.Value[1] / Math.Max(1.0, kv.Value[0]))).Append("% (")
                      .Append((kv.Value[1] / 1e6).ToString("0", Inv)).Append(" mln kcal)");
                }
            }
            sb.Append(" | praca domowa razem ").Append((r.HomeWorkLabour / 1e6).ToString("0.00", Inv)).Append(" mln rak (te same rece i ziemia, nigdy na targ)")
              .Append(" | utarg wozow i podatek panow od niego ida za sprzedaza wsi na targ (wyzej: mniej o ... wobec tych samych zajec bez pracy domowej).");
            Log.Info(sb.ToString());
            try { P9Line(r); } catch (Exception e) { Stumble("P9Line", e); }   // r4-koszyk: podzial P9 koszyka klas miast (kcal jedzenia / rzeczy), sklad kcal, dokupiona podstawa
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

        // ============================================================ ks-budzety.csv (recenzja 17.10: pomiar pieniedzy Planu do testu portu)
        private const string BudgetCsvName = "ks-budzety.csv";
        private const string BudgetCsvHeader = "dzien;osada_id;rodzaj;rynek;budzet_W_zl_d;budzet_R_zl_d;budzet_P_zl_d;odczytow;alfa;utarg_28d_zl_d;do_kiesy_28d_zl_d";

        /// <summary>Wiersz na miasto (budzety klas W/R/P z zarobku netto x 0.90 - to, co dostaje Plan) i na wies (alfa Planu, srednie 28 dob utargu i wplywu do kiesy) -
        /// plik do testu portu kroku 8 (harness: budzety miast i alfa okregow z gry zamiast scenariusza). KS-R3 C2: zrzut liczb na watku glownym (PlanStart),
        /// tekst wierszy na watku roboczym (BudgetsText).</summary>
        private static List<object[]> BudgetsRows(int day, KsInput inp)
        {
            if (!LogGate.Lines || inp == null) return null;
            var raw = new List<object[]>(inp.Towns.Count + inp.Districts.Count);
            foreach (var t in inp.Towns)
            {
                if (t.Budget == null || t.Budget.Length < 3) continue;
                var st = Settlement.Find(t.Id); double[] a = null; if (st != null) _earn.TryGetValue(st, out a);
                raw.Add(new object[] { t.Id, 0, t.Market, t.Budget[0], t.Budget[1], t.Budget[2], a != null ? (int)a[3] : -1, -1.0, -1.0, -1.0 });
            }
            foreach (var d in inp.Districts)
            {
                if (d.TownFields) continue;
                var st = Settlement.Find(d.Id); double[] a = null; if (st != null && st.Village != null) _vilTake.TryGetValue(st.Village, out a);
                raw.Add(new object[] { d.Id, 1, d.Market, 0.0, 0.0, 0.0, -1, d.Alpha, a != null ? a[0] : -1.0, a != null ? a[1] : -1.0 });
            }
            return raw;
        }

        /// <summary>Wiersze ks-budzety.csv z liczb zrzutu (czysty tekst).</summary>
        private static List<string> BudgetsText(int day, List<object[]> raw)
        {
            var rows = new List<string>(raw.Count);
            foreach (var x in raw)
            {
                string id = (string)x[0]; int kind = (int)x[1], mk = (int)x[2];
                if (kind == 0)
                    rows.Add(day + ";" + id + ";miasto;" + (mk == 0 ? "Westeros" : "Essos") + ";" + F0((double)x[3]) + ";" + F0((double)x[4]) + ";" + F0((double)x[5]) + ";" + ((int)x[6] >= 0 ? ((int)x[6]).ToString(Inv) : "-") + ";;;");
                else
                {
                    double al = (double)x[7], a0 = (double)x[8], a1 = (double)x[9];
                    rows.Add(day + ";" + id + ";wies;" + (mk == 0 ? "Westeros" : "Essos") + ";;;;;" + (al >= 0 ? al.ToString("0.0000", Inv) : "-") + ";" + (a0 >= 0 ? a0.ToString("0.0", Inv) : "-") + ";" + (a1 >= 0 ? a1.ToString("0.0", Inv) : "-"));
                }
            }
            return rows;
        }

        // ============================================================ ks-miasta.csv (co 7 dob, stan zastosowany)
        private const string TownCsvName = "ks-miasta.csv";
        private const string TownCsvHeader = "dzien;miasto_id;miasto;rynek;lambda;ludzie;indeks_jedzenia_mediana;jedzenie_na_suficie;jedzenie_na_podlodze;zboze_indeks;zboze_polka_szt;zboze_popyt_zaplacony_szt_d;zboze_dni_polki;"
                                           + "czesc_zaplacona_koszyka_miasta;wsie_chciane_szt_7d;wsie_kupione_szt_7d;wsie_bez_kiesy_szt_7d;wsie_bez_towaru_szt_7d;wsie_w_innym_miescie_szt_7d;kiesy_wsi_zl;kiesy_wsi_zmiana_7d_zl;kraina;zboze_w_drodze_szt";
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
                      .Append(';').Append(TownLandOf(st)).Append(';').Append(KsFoodCarts.Incoming(st, ZGrain))   // KS-R3: kraina miasta (NKS-23 - krainy bez ujscia) i zboze w drodze KS-H
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
