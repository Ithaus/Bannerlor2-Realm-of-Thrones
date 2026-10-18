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
    /// KS - REGULA ZMIANY ZAJEC PO WLACZENIU (A7, projekt KS rozdz. 3.1 i 11.2, dopisek Jeffa do Prac okregu) i REGALE (rozdz. 3.2):
    ///  - co 7 dob OD DOBY D (koniec rampy podazy; recenzja 16.10 - nie od wlaczenia: ceny rampy to jeszcze dzisiejsze braki) kazdy blok liczy dochod rodziny na pracujacego
    ///    kazdego swojego zajecia przy 28-dobowym indeksie cen miasta targowego wsi (srednia zerowana w dobie D, po polkach startu):
    ///    L x suma(P0 x wartosc kg x indeks) - K - renta ziemi z Planu x ha (ta sama funkcja co Plan, ceny gry zamiast cen cienia); zywiec po indeksie zywca (cow, hog,
    ///    sheep - to sprzedaje wies; wartosc kg jak w Planie: mieso i skory z uboju), konie poza KS (lancuch gry) - bez wartosci;
    ///  - HAMULEC JEFFA: zmiana, gdy inne zajecie daje >= 1.25 x dochodu biezacego (najwiekszy udzial rak bloku) bez przerwy przez 60 dob; najwyzej JEDNA czesc okregu
    ///    (blok wsi gry albo wioska poboczna) w przebudowie naraz; najwyzej 5% rak pola regionu (warownia i jej wsie) na sezon (91 dob); licznik regionu i srednie cen
    ///    w zapisie (arm_ks) - wczytanie nie daje nowego budzetu ani sredniej z jednej doby;
    ///  - ZIEMIA (recenzja 16.10): zmiana nie zaczyna sie i rece nie przechodza, gdy ziemia zajeta bloku (dom, zboze wlasne, rece x ha na pracujacego) przekroczylaby jego
    ///    powierzchnie (owce 120 ha, bydlo 18 ha na pracujacego) - produkcja tylko z rak NA ziemi;
    ///  - KOSZT PRZEBUDOWY: 28 dob kosztu K nowego zajecia za przenoszone rece (w zl gry: / sigma), placony co dobe z kiesy wsi do kasy miasta targowego (rzemieslnicy
    ///    i kupcy miasta robia sprzet) - najwyzej czwarta czesc kiesy na dobe; doba bez zaplaty = przebudowa stoi (postep proporcjonalny do zaplaty);
    ///  - OKRES PRZEJSCIOWY: rece przechodza liniowo przez 60 dob (winnice, gaje, daktyle, sady - 364 doby): stary towar gasnie, nowy rosnie;
    ///  - REGALE: co 28 dob od D rece przy zlozu +10% przy indeksie towaru w miescie > 1.2, -10% przy < 0.8 (sufit - polowa rak bloku poza domem, dno 0).
    ///  - Klucz Start Calibration Switch Rule wylaczony (domyslnie od recenzji 16.10 - ceny startu nie sa cenami cienia): sam POMIAR nacisku (licznik 60 dob, linia),
    ///    bez zmian zajec; regale dzialaja.
    /// Pola mieszczan - ta sama regula bez kosztu w zlocie (zloza i sprzet miasta). Pieniadz: tylko przelew kiesa wsi -> kasa miasta (MoneyLedger MKs).
    /// </summary>
    internal static partial class StartCalibration
    {
        private const double SwitchRatio = 1.25;
        private const int HoldDays = 60, StepDays = 7, CostDays = 28, Season = 91, RegCycle = 28;
        private const double RegionShare = 0.05;
        private const double CeilIx = 9.5, FloorIx = 0.11;                 // indeks na suficie / podlodze wzoru ceny gry (0.1 - 10)
        private static readonly Dictionary<Town, double[]> _px = new Dictionary<Town, double[]>();
        private static readonly Dictionary<Town, double[]> _pxNow = new Dictionary<Town, double[]>();   // ostatni odczyt (sufit / podloga w linii cen)
        private static readonly Dictionary<Town, int> _pxN = new Dictionary<Town, int>();
        private static Dictionary<string, KeyValuePair<int, double[]>> _pxLoad;   // srednie z zapisu (id miasta) - do pierwszego odczytu sesji
        private static int _pxDays, _pxDay = -1;
        private static ItemObject _hides;
        private static double[] _mv, _valKg;
        private static readonly Dictionary<string, double> _regionMoved = new Dictionary<string, double>(StringComparer.Ordinal);
        private static int _regionSeason = -1;
        // liczniki do linii (od ostatniej linii)
        private static int _rPress, _rMaxHeld, _rStarted, _rDone, _rStall, _rBudget, _rBusy, _rUp, _rDown, _rActive, _rCancel, _rWould, _rLand, _rLandStall; private static long _rPaid;
        private static int _rWinStarted;                                   // zmiany zaczete od doby D (nks: prawdziwe zmiany A7 w oknie <= 1% wiosek)

        private static void RuleReset()
        {
            _px.Clear(); _pxNow.Clear(); _pxN.Clear(); _pxLoad = null; _pxDays = 0; _pxDay = -1; _hides = null; _mv = null; _valKg = null;
            _regionMoved.Clear(); _regionSeason = -1; _rFeed = 0;
            _rPress = _rMaxHeld = _rStarted = _rDone = _rStall = _rBudget = _rBusy = _rUp = _rDown = _rActive = _rCancel = _rWould = _rLand = _rLandStall = 0; _rPaid = 0; _rWinStarted = 0;
            _pxRestartDay = -1; _pxRestartEnd = -1;               // KS r4 P7 (r4-ceny): restart srednich cen od nowa przy wlaczeniu
        }

        /// <summary>Doba D (po polkach startu): srednie cen miast od zera - A7 i regale licza od D na cenach po rampie (recenzja 16.10).</summary>
        private static void RulePxReset() { _px.Clear(); _pxNow.Clear(); _pxN.Clear(); _pxLoad = null; _pxDays = 0; _pxDay = -1; }

        /// <summary>Indeksy cen miast dla towarow KS (zwykla srednia przez 4 odczyty, potem wykladnicza 1/4). Stan zastosowany: co dobe, miasto co 7 dob na zmiane
        /// (ok. 28 dob; poprawka po autotescie 16.10 - koszt doby KS, dotad co 2 doby i 14 odczytow). Plan i Uzbrojony (recenzja 16.10 - koszt): tylko w doby linii cen
        /// (co 7 dob), srednia z 4 odczytow - sama linia "KS: ceny miast".</summary>
        private static void PricesDaily(int today)
        {
            if (_pxDay == today) return;
            bool applied = _state == StateApplied;
            if (!applied && !(LogGate.Lines && (today - 1) % 7 == 0)) return;
            _pxDay = today;
            EnsureItems();
            if (_hides == null) _hides = ItemOf("hides");
            _pxDays++;
            try
            {
                PxResolveLoad();
                int k = 0;
                foreach (var t in Town.AllTowns)
                {
                    if (t == null || t.Settlement == null) continue;
                    double[] a;
                    bool fresh = !_px.TryGetValue(t, out a);
                    // koszt doby KS (poprawka po autotescie 16.10: co 2 -> co 7 dob): miasto co 7 dob na zmiane (srednia z 4 odczytow = ok. 28 dob), nowe miasto od razu
                    if (applied && !fresh && ((k++ + today) % 7) != 0) continue;
                    if (fresh) { a = new double[ItemCount]; _px[t] = a; }
                    double[] now;
                    if (!_pxNow.TryGetValue(t, out now)) { now = new double[ItemCount]; _pxNow[t] = now; }
                    int nu; _pxN.TryGetValue(t, out nu); nu++; _pxN[t] = nu;
                    double w = 1.0 / Math.Min(4, nu);
                    for (int i = 0; i < ItemCount; i++)
                    {
                        var it = _items[i];
                        double ix = 1.0;
                        if (it != null && it.ItemCategory != null) { try { ix = t.GetItemCategoryPriceIndex(it.ItemCategory); } catch { ix = 1.0; } }
                        if (!(ix > 0) || double.IsInfinity(ix) || double.IsNaN(ix)) ix = 1.0;
                        now[i] = ix;
                        a[i] = fresh ? ix : a[i] + (ix - a[i]) * w;
                    }
                }
            }
            catch (Exception e) { Stumble("PricesDaily", e); }
        }

        /// <summary>Srednie cen z zapisu -> miasta tej sesji (raz, przy pierwszym odczycie po wczytaniu).</summary>
        private static void PxResolveLoad()
        {
            var ld = _pxLoad; if (ld == null) return;
            _pxLoad = null;
            foreach (var t in Town.AllTowns)
            {
                if (t == null || t.Settlement == null || t.Settlement.StringId == null) continue;
                KeyValuePair<int, double[]> kv;
                if (!ld.TryGetValue(t.Settlement.StringId, out kv) || kv.Value == null || kv.Value.Length != ItemCount) continue;
                _px[t] = (double[])kv.Value.Clone(); _pxN[t] = kv.Key;
            }
        }

        /// <summary>Indeks ceny towaru KS g w miescie (srednia): mieso z uboju - indeks miesa, skory - skor, zywiec - zywca, kon - 1.</summary>
        private static double Px(double[] px, int g)
        {
            if (g == KsTab.GMeat) return px[IMeat];
            if (g == KsTab.GHides) return px[KsDeliver.IHides];
            int it = GoodToItem[g];
            return it >= 0 ? px[it] : 1.0;
        }

        private static void EnsureValues()
        {
            if (_mv != null) return;
            EnsureItems();
            var val = new double[KsTab.NG];
            for (int g = 0; g < KsTab.NG; g++)
            {
                ItemObject it = null;
                string id = KsTab.Goods[g];
                if (g == KsTab.GHides) it = _hides ?? ItemOf("hides");
                else if (g == KsTab.GMeat) it = _items[IMeat];
                else { int i = GoodToItem[g]; it = i >= 0 ? _items[i] : ItemOf(id); }
                val[g] = it != null ? Math.Max(0, it.Value) / Math.Max(1.0, it.Weight) : 1.0;
            }
            val[KsTab.GHorse] = 0.0;              // recenzja 16.10: konie robi lancuch gry (poza KS) - rece stadnin nie zarabiaja na koniach w regule
            _valKg = val;
            _mv = RuleCraftValue(val);
        }

        /// <summary>Wartosc towarow zajecia na pracujacego przy ziemi 1 [c * NG + g] dla reguly w grze: jak KsCore.CraftValue, ale zywiec zostaje zywcem (indeks cow / hog / sheep
        /// - to wies sprzedaje na targu), z wartoscia kg Planu (polowa miesa i skory z uboju).</summary>
        private static double[] RuleCraftValue(double[] val)
        {
            var mv = new double[KsTab.NC * KsTab.NG];
            for (int c = 0; c < KsTab.NC; c++)
                foreach (var kv in KsTab.P0[c])
                {
                    int g = kv.Key; double r = kv.Value;
                    if (g == KsTab.GCow || g == KsTab.GHog || g == KsTab.GSheep) mv[c * KsTab.NG + g] += r * (KsTab.MeatOfLive * val[KsTab.GMeat] + KsTab.HideOfLive(g) * val[KsTab.GHides]);
                    else if (g == KsTab.GWhaleMeat) mv[c * KsTab.NG + KsTab.GMeat] += r * val[KsTab.GMeat];
                    else mv[c * KsTab.NG + g] += r * val[g];
                }
            return mv;
        }

        /// <summary>Dochod rodziny na pracujacego zajecia c w bloku przy indeksach cen miasta (regula 3.1 przy cenach gry).</summary>
        private static double Income(LiveBlock b, string type, int c, double[] px)
        {
            double L = b.L(c, type);
            double v = 0; int o = c * KsTab.NG;
            for (int g = 0; g < KsTab.NG; g++) { double m = _mv[o + g]; if (m != 0) v += m * Px(px, g); }
            return L * v - KsTab.KCost[c] - b.Rent * KsTab.Ha(c, b.K);      // r4-polnoc: ha wedlug strefy bloku (bez klucza Ks Cold Land - HaPerWorker[c])
        }

        private static bool Tree(int c) { return c == KsTab.CVine || c == KsTab.COlive || c == KsTab.CDate || c == KsTab.CCitrus; }

        private static int IndexOfCraft(LiveBlock b, int c) { if (b.C != null) for (int i = 0; i < b.C.Length; i++) if (b.C[i] == c) return i; return -1; }

        private static void RuleDaily(int today)
        {
            var s = Settings.Current;
            bool rule = s != null && s.StartCalibrationSwitchRule;
            EnsureValues();
            int season = today / Season;
            if (season != _regionSeason) { _regionSeason = season; _regionMoved.Clear(); }
            int d0 = DDay;                                                    // recenzja 16.10: A7 i regale od D (nie od wlaczenia)
            bool step = d0 >= 0 && today > d0 && (today - d0) % StepDays == 0;
            bool reg = d0 >= 0 && today > d0 && (today - d0) % RegCycle == 0;
            double sigma = Math.Max(1, DailyLife.KsSigma);
            int active = 0;
            foreach (var ld in _live.Values)
            {
                if (ld.St == null || ld.K == null || ld.B == null) continue;
                if (LandBackOf(ld.St)) continue;                         // ks-arm: kraina wrocona przez bramke krainy - bez reguly zmiany zajec i regali
                try
                {
                    Town mt = ld.TF ? ld.T : (ld.V.TradeBound != null ? ld.V.TradeBound.Town : (ld.V.Bound != null ? ld.V.Bound.Town : null));
                    double[] px;
                    if (mt == null || !_px.TryGetValue(mt, out px)) continue;
                    string type = ld.K.Type;
                    bool busy = false;
                    foreach (var b in ld.B) if (b != null && b.Target >= 0) { busy = true; break; }
                    string region = ld.TF ? ld.St.StringId : (ld.V.Bound != null ? ld.V.Bound.StringId : ld.St.StringId);
                    foreach (var b in ld.B)
                    {
                        if (b == null || b.K == null || b.C == null) continue;
                        if (b.Target >= 0) { Progress(ld, b, today, mt); active++; continue; }
                        if (reg && b.RC != null) RegaleCycle(b, px);
                        if (KsWorksLock(ld, b)) { b.Held = 0; continue; }   // inwest: czesc z pracami okregu nie zmienia zajecia (projekt A7)
                        if (!step || b.Cand == null || b.Cand.Count < 2) continue;
                        int ci = 0; for (int i = 1; i < b.X.Length; i++) if (b.X[i] > b.X[ci]) ci = i;
                        int cur = b.C[ci];
                        double incCur = Income(b, type, cur, px), incBest = double.NegativeInfinity; int best = -1;
                        foreach (int c in b.Cand) { if (c == cur) continue; double v = Income(b, type, c, px); if (v > incBest) { incBest = v; best = c; } }
                        if (best < 0 || incBest < SwitchRatio * Math.Max(0.02, incCur)) { b.Held = 0; continue; }
                        b.Held += StepDays; _rPress++; if (b.Held > _rMaxHeld) _rMaxHeld = b.Held;
                        if (b.Held < HoldDays) continue;
                        if (!rule) { _rWould++; continue; }                  // klucz wylaczony - sam pomiar nacisku (zmiana bylaby tu)
                        if (busy) { _rBusy++; continue; }
                        if (FeedKeeps(ld, cur, best)) { _rFeed++; continue; }   // r4-polnoc: najpierw wyzywic siebie - czesc przy jedzeniu nie idzie na zajecie tylko na targ
                        // ziemia bloku: po zmianie rak X0 z cur na best ziemia zajeta nie moze przekroczyc powierzchni bloku
                        double area = BlockArea(ld, b);
                        double after = b.LandUse + b.X[ci] * (b.LandOf(best, type) - b.LandOf(cur, type));
                        if (area > 0 && after > area * 1.02 && after > b.LandUse) { _rLand++; continue; }
                        double hands = b.Hands * b.X[ci];
                        double rh, moved;
                        _regionHands.TryGetValue(region ?? "-", out rh); _regionMoved.TryGetValue(region ?? "-", out moved);
                        if (moved + hands > RegionShare * rh) { _rBudget++; continue; }
                        int T = Tree(best) ? 364 : 60;
                        b.Cur = (sbyte)cur; b.Target = (sbyte)best; b.TStart = today; b.TEnd = today + T; b.X0 = b.X[ci]; b.H0 = (float)hands;
                        b.Cost = ld.TF ? 0f : (float)(CostDays * KsTab.KCost[best] * hands / sigma); b.Paid = 0f;
                        _regionMoved[region ?? "-"] = moved + hands;
                        if (IndexOfCraft(b, best) < 0)
                        {
                            var nc = new byte[b.C.Length + 1]; var nx = new float[b.X.Length + 1];
                            Array.Copy(b.C, nc, b.C.Length); Array.Copy(b.X, nx, b.X.Length); nc[b.C.Length] = (byte)best; nx[b.X.Length] = 0f;
                            b.C = nc; b.X = nx;
                        }
                        busy = true; _rStarted++; _rWinStarted++; active++;
                        if (_rStarted <= 12) _startNotes.Add((ld.St.Name != null ? ld.St.Name.ToString() : ld.Id) + " " + KsTab.Crafts[cur] + "->" + KsTab.Crafts[best] + " (" + Math.Round(hands) + " rak, " + T + " dob, " + Math.Round(b.Cost) + " zl)");
                    }
                }
                catch (Exception e) { Stumble("RuleDaily(okreg)", e); }
            }
            _rActive = active;
        }
        private static readonly List<string> _startNotes = new List<string>();

        /// <summary>Powierzchnia bloku (ha): ziemia okregu x udzial bloku (ostatni rachunek doby).</summary>
        private static double BlockArea(LiveDist ld, LiveBlock b) { return b.K != null ? b.K.Area : 0.0; }

        /// <summary>Doba przebudowy: zaplata dzisiejszej czesci kosztu (kiesa wsi -> kasa miasta targowego, najwyzej 1/4 kiesy), postep proporcjonalny do zaplaty, rece
        /// z zajecia biezacego do nowego; koniec - gdy stare zajecie nie ma juz rak. Ziemia bloku pelna (ludzi przybylo) - rece nie przechodza (recenzja 16.10).</summary>
        private static void Progress(LiveDist ld, LiveBlock b, int today, Town mt)
        {
            int ci = IndexOfCraft(b, b.Cur), ti = IndexOfCraft(b, b.Target);
            if (ci < 0 || ti < 0 || today > b.TEnd + 364) { b.Target = -1; b.Cur = -1; b.Held = 0; b.TStart = b.TEnd = -1; b.Cost = b.Paid = b.X0 = b.H0 = 0f; _rCancel++; return; }   // rok po terminie bez zaplaty - przebudowa porzucona (rece zostaja tam, gdzie doszly)
            string type = ld.K != null ? ld.K.Type : null;
            double area = BlockArea(ld, b);
            if (type != null && area > 0 && b.LandUse > area * 1.02 && b.LandOf(b.Target, type) > b.LandOf(b.Cur, type)) { _rLandStall++; return; }
            int T = Math.Max(1, b.TEnd - b.TStart);
            double progress = 1.0;
            if (b.Cost > 0 && !ld.TF && ld.V != null)
            {
                double due = b.Cost / T;
                int ask = (int)Math.Min(Math.Ceiling(Math.Max(0.0, Math.Min(due, b.Cost - b.Paid))), Math.Max(0, ld.V.Gold) / 4);
                int got = 0;
                if (ask > 0 && mt != null)
                {
                    int had = ld.V.Gold;
                    ld.V.ChangeGold(-ask);
                    got = had - ld.V.Gold;
                    if (got > 0)
                    {
                        int th = mt.Gold;
                        mt.ChangeGold(got);
                        _mVil += got; _mTown += mt.Gold - th;
                    }
                    else got = 0;
                }
                b.Paid += got; _rPaid += got;
                progress = due > 0 ? Math.Min(1.0, got / due) : 1.0;
                if (b.Paid >= b.Cost - 0.5) progress = 1.0;
                if (progress < 1.0) _rStall++;
            }
            double dx = Math.Min(b.X[ci], b.X0 / T * progress);
            b.X[ci] -= (float)dx; b.X[ti] += (float)dx;
            if (b.X[ci] <= 1e-4f)
            {
                // koniec przebudowy: stare zajecie bez rak znika z bloku
                var nc = new List<byte>(); var nx = new List<float>();
                for (int i = 0; i < b.C.Length; i++) if (i != ci || b.X[i] > 1e-4f) { nc.Add(b.C[i]); nx.Add(b.X[i]); }
                double s = 0; foreach (var x in nx) s += x;
                for (int i = 0; i < nx.Count; i++) nx[i] = (float)(nx[i] / (s > 0 ? s : 1));
                b.C = nc.ToArray(); b.X = nx.ToArray();
                b.Target = -1; b.Cur = -1; b.Held = 0; b.TStart = b.TEnd = -1; b.Cost = b.Paid = b.X0 = b.H0 = 0f;
                _rDone++;
            }
        }

        private static void RegaleCycle(LiveBlock b, double[] px)
        {
            double sum = 0;
            for (int j = 0; j < b.RC.Length; j++)
            {
                int g = KsTab.RegGood(b.RC[j]);
                double ix = Px(px, g);
                if (ix > 1.2) { b.RF[j] *= 1.1f; _rUp++; }
                else if (ix < 0.8) { b.RF[j] *= 0.9f; _rDown++; }
                sum += b.RF[j];
            }
            double cap = 0.5 * Math.Max(0.0, 1.0 - b.HomeFrac);
            if (sum > cap && sum > 0) { float k = (float)(cap / sum); for (int j = 0; j < b.RF.Length; j++) b.RF[j] *= k; }
        }

        // kontrola pieniedzy KS od ostatniej linii: przebudowa (kiesy wsi -> kasy miast), pola mieszczan (kasy miast -> kiesy ludu)
        private static long _mVil, _mTown, _mTfTown, _mTfFolk;

        /// <summary>Linia co 7 dob: wedlug rynku (Westeros, Essos) mediana 28-dobowego indeksu cen miast dla towarow KS i odsetek miast na suficie / podlodze wzoru ceny
        /// w ostatnim odczycie (pomiar "ceny blisko startu" - progi nks NKS-7).</summary>
        private static string PriceLine(int day)
        {
            var sb = new StringBuilder(2600);
            sb.Append(PK).Append("ceny miast dzien ").Append(day).Append(" (mediana indeksu 28 dob, miast ").Append(_px.Count).Append(", dob sredniej ").Append(Math.Min(28, _pxDays)).Append("):");
            var vals = new List<double>(_px.Count);
            bool first = true;
            for (int i = 0; i < ItemCount; i++)
            {
                vals.Clear();
                foreach (var a in _px.Values) if (a != null && i < a.Length) vals.Add(a[i]);
                if (vals.Count == 0) continue;
                sb.Append(first ? " " : ", ").Append(ItemIds[i]).Append(' ').Append(Med(vals).ToString("0.00", Inv));
                first = false;
            }
            for (int m = 0; m < 2; m++)
            {
                sb.Append(m == 0 ? " | Westeros:" : " | Essos:");
                first = true;
                for (int i = 0; i < ItemCount; i++)
                {
                    vals.Clear(); int ceil = 0, floor = 0, n = 0;
                    foreach (var kv in _px)
                    {
                        if (kv.Value == null || TownMarket(kv.Key) != m) continue;
                        vals.Add(kv.Value[i]);
                        double[] now; if (_pxNow.TryGetValue(kv.Key, out now) && now != null) { n++; if (now[i] >= CeilIx) ceil++; else if (now[i] <= FloorIx) floor++; }
                    }
                    if (vals.Count == 0) continue;
                    sb.Append(first ? " " : ", ").Append(ItemIds[i]).Append(' ').Append(Med(vals).ToString("0.00", Inv)).Append(" (s").Append(n > 0 ? (100 * ceil / n).ToString(Inv) : "-")
                      .Append(" p").Append(n > 0 ? (100 * floor / n).ToString(Inv) : "-").Append(')');
                    first = false;
                }
            }
            sb.Append(" | (s/p - % miast na suficie / podlodze wzoru ceny w ostatnim odczycie).");
            return sb.ToString();
        }

        private static double Med(List<double> v)
        {
            v.Sort();
            return v.Count % 2 == 1 ? v[v.Count / 2] : 0.5 * (v[v.Count / 2 - 1] + v[v.Count / 2]);
        }

        /// <summary>Kontrola pieniedzy KS (od ostatniej linii): ile zeszlo z kies wsi i weszlo do kas miast przy przebudowie, ile zeszlo z kas miast i weszlo do kies ludu
        /// za plon pol mieszczan - roznice musza byc 0 (nic z niczego).</summary>
        private static string MoneyText()
        {
            string t = "kontrola pieniedzy KS: przebudowa kiesy wsi -" + _mVil + " / kasy miast +" + _mTown + " (roznica " + (_mVil - _mTown) + "), pola mieszczan kasy miast -" + _mTfTown
                       + " / kiesy ludu +" + _mTfFolk + " (roznica " + (_mTfTown - _mTfFolk) + ")";
            _mVil = _mTown = _mTfTown = _mTfFolk = 0;
            return t;
        }

        /// <summary>Dopisek linii produkcji: nacisk na zmiane, przebudowy, regale (liczniki od ostatniej linii).</summary>
        private static string RuleText()
        {
            var s = Settings.Current;
            bool rule = s != null && s.StartCalibrationSwitchRule;
            var sb = new StringBuilder(700);
            sb.Append("nacisk A7 na zmiane (co ").Append(StepDays).Append(" dob od D ").Append(DDay).Append(", inne zajecie >= x").Append(SwitchRatio.ToString("0.00", Inv)).Append("): przegladow ponad progiem ").Append(_rPress)
              .Append(", najdluzej ").Append(_rMaxHeld).Append(" dob, po ").Append(HoldDays).Append(" dobach (zmiana bylaby, klucz ").Append(rule ? "TAK" : "WYLACZONY - sam pomiar").Append(") ").Append(_rWould)
              .Append(" | zmiany zajec: zaczete ").Append(_rStarted).Append(" (od D razem ").Append(_rWinStarted).Append("), w toku ").Append(_rActive).Append(", skonczone ").Append(_rDone)
              .Append(", wstrzymane (inna czesc okregu w przebudowie) ").Append(_rBusy).Append(", (5% rak regionu w sezonie) ").Append(_rBudget).Append(", (ziemia bloku) ").Append(_rLand)
              .Append(", przejscia stojace na ziemi ").Append(_rLandStall)
              .Append(", dob bez pelnej zaplaty ").Append(_rStall).Append(", porzucone (rok po terminie) ").Append(_rCancel).Append(", zaplacone z kies wsi do kas miast ").Append(_rPaid).Append(" zl")
              .Append(_startNotes.Count > 0 ? " (" + string.Join("; ", _startNotes.ToArray()) + ")" : "")
              .Append(" | regale (co ").Append(RegCycle).Append(" dob od D): rece przy zlozu w gore ").Append(_rUp).Append(", w dol ").Append(_rDown)
              .Append(FeedRuleText());                                  // r4-polnoc
            _rPress = _rMaxHeld = _rStarted = _rDone = _rStall = _rBudget = _rBusy = _rUp = _rDown = _rCancel = _rWould = _rLand = _rLandStall = 0; _rPaid = 0; _startNotes.Clear();
            return sb.ToString();
        }

        // ============================================================ pamiec reguly w zapisie (recenzja 16.10): rece regionu w sezonie i srednie cen miast
        // sezon~region:rece,...~zmiany od D~miasto:n:indeksy (2 znaki base36 na towar, indeks x 100, 0..12.95)!miasto...
        private static void RuleExport(StringBuilder sb)
        {
            sb.Append(_regionSeason).Append('~');
            bool f = true;
            foreach (var kv in _regionMoved) { if (!f) sb.Append(','); f = false; sb.Append(Clean(kv.Key)).Append(':').Append(Math.Round(kv.Value).ToString("0", Inv)); }
            sb.Append('~').Append(_rWinStarted).Append('~');
            f = true;
            foreach (var kv in _px)
            {
                if (kv.Key == null || kv.Key.Settlement == null || kv.Value == null) continue;
                int n; _pxN.TryGetValue(kv.Key, out n);
                if (!f) sb.Append('!'); f = false;
                sb.Append(Clean(kv.Key.Settlement.StringId)).Append(':').Append(n).Append(':');
                for (int i = 0; i < kv.Value.Length; i++) sb.Append(E36(Math.Max(0, Math.Min(1295, (int)Math.Round(kv.Value[i] * 100))), 2));
            }
        }

        /// <summary>Odczyt pamieci reguly z zapisu ks3; zwraca opis do linii startu. Srednie cen trafiaja do miast przy pierwszym odczycie cen sesji.</summary>
        private static string RuleImport(string t)
        {
            try
            {
                if (string.IsNullOrEmpty(t)) return "pusta";
                var p = t.Split('~');
                if (p.Length < 4) return "nieznany format - od zera";
                int season; if (int.TryParse(p[0], NumberStyles.Integer, Inv, out season)) _regionSeason = season;
                _regionMoved.Clear();
                if (p[1].Length > 0)
                    foreach (var e in p[1].Split(','))
                    {
                        int c = e.LastIndexOf(':'); double v;
                        if (c > 0 && double.TryParse(e.Substring(c + 1), NumberStyles.Float, Inv, out v)) _regionMoved[e.Substring(0, c)] = v;
                    }
                int.TryParse(p[2], NumberStyles.Integer, Inv, out _rWinStarted);
                var load = new Dictionary<string, KeyValuePair<int, double[]>>(StringComparer.Ordinal);
                if (p[3].Length > 0)
                    foreach (var e in p[3].Split('!'))
                    {
                        var q = e.Split(':');
                        int n;
                        if (q.Length != 3 || !int.TryParse(q[1], NumberStyles.Integer, Inv, out n) || q[2].Length != 2 * ItemCount) continue;
                        var a = new double[ItemCount]; bool ok = true;
                        for (int i = 0; i < ItemCount && ok; i++) { int v = D36(q[2], 2 * i, 2); if (v < 0) ok = false; else a[i] = Math.Max(0.01, v / 100.0); }
                        if (ok) load[q[0]] = new KeyValuePair<int, double[]>(n, a);
                    }
                _pxLoad = load.Count > 0 ? load : null;
                return "sezon " + _regionSeason + ", regionow z przeniesionymi rekami " + _regionMoved.Count + ", srednie cen " + load.Count + " miast, zmian od D " + _rWinStarted;
            }
            catch (Exception e) { Stumble("RuleImport", e); return "blad odczytu - od zera"; }
        }
    }
}
