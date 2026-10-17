using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// KS - REGULA ZMIANY ZAJEC PO WLACZENIU (A7, projekt KS rozdz. 3.1 i 11.2, dopisek Jeffa do Prac okregu) i REGALE (rozdz. 3.2):
    ///  - co 7 dob (od doby wlaczenia) kazdy blok liczy dochod rodziny na pracujacego kazdego swojego zajecia przy 28-dobowym indeksie cen miasta targowego wsi:
    ///    L x suma(P0 x wartosc kg x indeks) - K - renta ziemi z Planu x ha (ta sama funkcja co Plan, ceny gry zamiast cen cienia);
    ///  - HAMULEC JEFFA: zmiana, gdy inne zajecie daje >= 1.25 x dochodu biezacego (najwiekszy udzial rak bloku) bez przerwy przez 60 dob; najwyzej JEDNA czesc okregu
    ///    (blok wsi gry albo wioska poboczna) w przebudowie naraz; najwyzej 5% rak pola regionu (warownia i jej wsie) na sezon (91 dob);
    ///  - KOSZT PRZEBUDOWY: 28 dob kosztu K nowego zajecia za przenoszone rece (w zl gry: / sigma), placony co dobe z kiesy wsi do kasy miasta targowego (rzemieslnicy
    ///    i kupcy miasta robia sprzet) - najwyzej czwarta czesc kiesy na dobe; doba bez zaplaty = przebudowa stoi (postep proporcjonalny do zaplaty);
    ///  - OKRES PRZEJSCIOWY: rece przechodza liniowo przez 60 dob (winnice, gaje, daktyle, sady - 364 doby): stary towar gasnie, nowy rosnie;
    ///  - REGALE: co 28 dob od wlaczenia rece przy zlozu +10% przy indeksie towaru w miescie > 1.2, -10% przy < 0.8 (sufit - polowa rak bloku poza domem, dno 0).
    /// Pola mieszczan - ta sama regula bez kosztu w zlocie (zloza i sprzet miasta). Pieniadz: tylko przelew kiesa wsi -> kasa miasta (MoneyLedger MKs).
    /// </summary>
    internal static partial class StartCalibration
    {
        private const double SwitchRatio = 1.25;
        private const int HoldDays = 60, StepDays = 7, CostDays = 28, Season = 91, RegCycle = 28;
        private const double RegionShare = 0.05;
        private static readonly Dictionary<Town, double[]> _px = new Dictionary<Town, double[]>();
        private static int _pxDays, _pxDay = -1;
        private static ItemObject _hides;
        private static double[] _mv, _valKg;
        private static readonly Dictionary<string, double> _regionMoved = new Dictionary<string, double>(StringComparer.Ordinal);
        private static int _regionSeason = -1;
        // liczniki do linii (od ostatniej linii)
        private static int _rPress, _rMaxHeld, _rStarted, _rDone, _rStall, _rBudget, _rBusy, _rUp, _rDown, _rActive, _rCancel; private static long _rPaid;

        private static void RuleReset()
        {
            _px.Clear(); _pxDays = 0; _pxDay = -1; _hides = null; _mv = null; _valKg = null;
            _regionMoved.Clear(); _regionSeason = -1;
            _rPress = _rMaxHeld = _rStarted = _rDone = _rStall = _rBudget = _rBusy = _rUp = _rDown = _rActive = _rCancel = 0; _rPaid = 0;
        }

        /// <summary>28-dobowe indeksy cen miast dla towarow KS (zwykla srednia przez 28 dob, potem wykladnicza 1/28) i srednia ksiegi towarow - raz na dobe.</summary>
        private static void PricesDaily(int today)
        {
            if (_pxDay == today) return;
            _pxDay = today;
            try { LedgerEma(); } catch (Exception e) { Stumble("LedgerEma", e); }
            EnsureItems();
            if (_hides == null) _hides = ItemOf("hides");
            _pxDays++;
            double w = 1.0 / Math.Min(28, _pxDays);
            try
            {
                foreach (var t in Town.AllTowns)
                {
                    if (t == null || t.Settlement == null) continue;
                    double[] a;
                    bool fresh = !_px.TryGetValue(t, out a);
                    if (fresh) { a = new double[ItemCount + 1]; _px[t] = a; }
                    for (int i = 0; i <= ItemCount; i++)
                    {
                        var it = i < ItemCount ? _items[i] : _hides;
                        double ix = 1.0;
                        if (it != null && it.ItemCategory != null) { try { ix = t.GetItemCategoryPriceIndex(it.ItemCategory); } catch { ix = 1.0; } }
                        if (!(ix > 0) || double.IsInfinity(ix) || double.IsNaN(ix)) ix = 1.0;
                        a[i] = fresh ? ix : a[i] + (ix - a[i]) * w;
                    }
                }
            }
            catch (Exception e) { Stumble("PricesDaily", e); }
        }

        /// <summary>Indeks ceny towaru KS g w miescie (srednia): mieso z uboju - indeks miesa, skory - skor, kon - 1.</summary>
        private static double Px(double[] px, int g)
        {
            if (g == KsTab.GMeat) return px[IMeat];
            if (g == KsTab.GHides) return px[ItemCount];
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
            val[KsTab.GHorse] = KsTab.HorseVal;
            _valKg = val;
            _mv = KsCore.CraftValue(val);
        }

        /// <summary>Dochod rodziny na pracujacego zajecia c w bloku przy indeksach cen miasta (regula 3.1 przy cenach gry).</summary>
        private static double Income(LiveBlock b, string type, int c, double[] px)
        {
            double L = KsCore.LandEff(b.K, c, type);
            double v = 0; int o = c * KsTab.NG;
            for (int g = 0; g < KsTab.NG; g++) { double m = _mv[o + g]; if (m != 0) v += m * Px(px, g); }
            return L * v - KsTab.KCost[c] - b.Rent * KsTab.HaPerWorker[c];
        }

        private static bool Tree(int c) { return c == KsTab.CVine || c == KsTab.COlive || c == KsTab.CDate || c == KsTab.CCitrus; }

        private static int IndexOfCraft(LiveBlock b, int c) { if (b.C != null) for (int i = 0; i < b.C.Length; i++) if (b.C[i] == c) return i; return -1; }

        private static void RuleDaily(int today)
        {
            var s = Settings.Current;
            bool rule = s == null || s.StartCalibrationSwitchRule;
            EnsureValues();
            int season = today / Season;
            if (season != _regionSeason) { _regionSeason = season; _regionMoved.Clear(); }
            bool step = _applyDay >= 0 && today > _applyDay && (today - _applyDay) % StepDays == 0;
            bool reg = _applyDay >= 0 && today > _applyDay && (today - _applyDay) % RegCycle == 0;
            double sigma = Math.Max(1, DailyLife.KsSigma);
            int active = 0;
            foreach (var ld in _live.Values)
            {
                if (ld.St == null || ld.K == null || ld.B == null) continue;
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
                        if (!step || !rule || b.Cand == null || b.Cand.Count < 2) continue;
                        int ci = 0; for (int i = 1; i < b.X.Length; i++) if (b.X[i] > b.X[ci]) ci = i;
                        int cur = b.C[ci];
                        double incCur = Income(b, type, cur, px), incBest = double.NegativeInfinity; int best = -1;
                        foreach (int c in b.Cand) { if (c == cur) continue; double v = Income(b, type, c, px); if (v > incBest) { incBest = v; best = c; } }
                        if (best < 0 || incBest < SwitchRatio * Math.Max(0.02, incCur)) { b.Held = 0; continue; }
                        b.Held += StepDays; _rPress++; if (b.Held > _rMaxHeld) _rMaxHeld = b.Held;
                        if (b.Held < HoldDays) continue;
                        if (busy) { _rBusy++; continue; }
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
                        busy = true; _rStarted++; active++;
                        if (_rStarted <= 12) _startNotes.Add((ld.St.Name != null ? ld.St.Name.ToString() : ld.Id) + " " + KsTab.Crafts[cur] + "->" + KsTab.Crafts[best] + " (" + Math.Round(hands) + " rak, " + T + " dob, " + Math.Round(b.Cost) + " zl)");
                    }
                }
                catch (Exception e) { Stumble("RuleDaily(okreg)", e); }
            }
            _rActive = active;
        }
        private static readonly List<string> _startNotes = new List<string>();

        /// <summary>Doba przebudowy: zaplata dzisiejszej czesci kosztu (kiesa wsi -> kasa miasta targowego, najwyzej 1/4 kiesy), postep proporcjonalny do zaplaty, rece
        /// z zajecia biezacego do nowego; koniec - gdy stare zajecie nie ma juz rak.</summary>
        private static void Progress(LiveDist ld, LiveBlock b, int today, Town mt)
        {
            int ci = IndexOfCraft(b, b.Cur), ti = IndexOfCraft(b, b.Target);
            if (ci < 0 || ti < 0 || today > b.TEnd + 364) { b.Target = -1; b.Cur = -1; b.Held = 0; b.TStart = b.TEnd = -1; b.Cost = b.Paid = b.X0 = b.H0 = 0f; _rCancel++; return; }   // rok po terminie bez zaplaty - przebudowa porzucona (rece zostaja tam, gdzie doszly)
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

        /// <summary>Linia co 7 dob: mediana 28-dobowego indeksu cen miast dla towarow KS (pomiar "ceny blisko 1" - progi nks).</summary>
        private static string PriceLine(int day)
        {
            var sb = new StringBuilder(1200);
            sb.Append(PK).Append("ceny miast dzien ").Append(day).Append(" (mediana indeksu 28 dob, miast ").Append(_px.Count).Append(", dob sredniej ").Append(Math.Min(28, _pxDays)).Append("):");
            var vals = new List<double>(_px.Count);
            bool first = true;
            for (int i = 0; i <= ItemCount; i++)
            {
                vals.Clear();
                foreach (var a in _px.Values) if (a != null && i < a.Length) vals.Add(a[i]);
                if (vals.Count == 0) continue;
                vals.Sort();
                double med = vals.Count % 2 == 1 ? vals[vals.Count / 2] : 0.5 * (vals[vals.Count / 2 - 1] + vals[vals.Count / 2]);
                sb.Append(first ? " " : ", ").Append(i < ItemCount ? ItemIds[i] : "hides").Append(' ').Append(med.ToString("0.00", Inv));
                first = false;
            }
            sb.Append('.');
            return sb.ToString();
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
            var sb = new StringBuilder(600);
            sb.Append("nacisk na zmiane (co ").Append(StepDays).Append(" dob, inne zajecie >= x").Append(SwitchRatio.ToString("0.00", Inv)).Append("): przegladow ponad progiem ").Append(_rPress)
              .Append(", najdluzej ").Append(_rMaxHeld).Append(" dob | zmiany zajec: zaczete ").Append(_rStarted).Append(", w toku ").Append(_rActive).Append(", skonczone ").Append(_rDone)
              .Append(", wstrzymane (inna czesc okregu w przebudowie) ").Append(_rBusy).Append(", (5% rak regionu w sezonie) ").Append(_rBudget)
              .Append(", dob bez pelnej zaplaty ").Append(_rStall).Append(", porzucone (rok po terminie) ").Append(_rCancel).Append(", zaplacone z kies wsi do kas miast ").Append(_rPaid).Append(" zl")
              .Append(_startNotes.Count > 0 ? " (" + string.Join("; ", _startNotes.ToArray()) + ")" : "")
              .Append(" | regale (co ").Append(RegCycle).Append(" dob): rece przy zlozu w gore ").Append(_rUp).Append(", w dol ").Append(_rDown);
            _rPress = _rMaxHeld = _rStarted = _rDone = _rStall = _rBudget = _rBusy = _rUp = _rDown = _rCancel = 0; _rPaid = 0; _startNotes.Clear();
            return sb.ToString();
        }
    }
}
