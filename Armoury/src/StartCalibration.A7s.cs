using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// f5-a7 (noc 17/18.10, fala 5; koncowy przeglad ekonomii 18.10: "pod KS zajecia wiosek i rece sa zamrozone", "A7 nie jest gotowa - 92% blokow chce zmiany,
    /// sygnal z surowych indeksow gry (sufit 10)") - SYGNAL REGULY ZMIANY ZAJEC (A7) i jej pomiar. Hamulec Jeffa bez zmian (StartCalibration.Rule.cs): x1.25 przez 60 dob,
    /// jedna czesc okregu naraz, 5% rak regionu na sezon, ziemia bloku, najpierw wyzywic siebie, przebudowa z prawdziwego materialu (VillageCrafts.PayRebuild), okres
    /// przejsciowy 60 / 364 doby. Zmienia sie TYLKO to, czym regula mierzy "lepiej placi":
    ///  - CENA SYGNALU towaru g w miescie targowym t: cena cienia Planu rynku (P, ta sama, przy ktorej Plan ustawil zajecia; 0.05-5) x gradient krainy miasta (cena krainy
    ///    po przewozie z Planu, x ciecie nadmiaru krainy - ta sama co LandGradZ) x odchylenie rynku miasta: (srednia 28 dob indeksu gry towaru / indeks startu z P) wzgledem
    ///    mediany tego ilorazu po towarach pola miasta, obciete do 1/(1+pasmo)..1+pasmo (klucz Ks Switch Market Band, 0.15). Sufit wzoru gry (10) i ogolny poziom cen
    ///    nie przesuwaja rak; zostaje wzgledny brak albo nadmiar towaru w miescie. Srednia wykladnicza 4 krokow (co 7 dob, ok. 28 dob) - chwilowy brak nie przestawia wsi;
    ///  - DOCHOD RODZINY na pracujacego zajecia c: L x suma(wartosc kg Planu x cena sygnalu) - koszt K - renta ziemi bloku x ha (wzor Planu - KsCore.Shares; zywiec jako
    ///    mieso i skory, konie z wartoscia Planu - stadniny ich nie traca, mieso wieloryba jako mieso). Przy cenach Planu bez gradientu ten wzor dawal w fin88 nacisk 5.9-7.9%
    ///    blokow (3.7-5.3% rak) wobec 92% przy indeksach gry (linia "KS: A7 diagnoza (f4-pomiary)", wariant S2);
    ///  - Held liczony nowym sygnalem od zera (pierwszy krok z kluczem: Held wszystkich blokow = 0 - 60 dob nacisku tego sygnalu, nie starego);
    ///  - zmiany naprawde z kluczem Ks Switch Signal Rule (albo dawnym Start Calibration Switch Rule) - wartosc domyslna starego klucza zapisana w MCM Jeffa nie zmienilaby
    ///    sie (CLAUDE.md, pulapka Armoury.json), wiec nowy klucz;
    ///  - droga bez KS (zajecia wiosek pobocznych, VillageCrafts.Rule): ta sama funkcja dochodu przy kluczu Side Village Craft Ks Signal (jedna regula na zjawisko).
    /// POMIAR: linia "KS: A7 regula zajec (f5-a7)" w kazdej dobie kroku A7 (nacisk, trzymane 60 dob, zmiany sezonu z rekami wedlug rynku i regionu, przejscia, przebudowa:
    /// kiesy wsi = kasy targow + lud miast, material zaplacony = koszt zdjetego towaru, sztuki = zmiana polek), "KS: A7 sezon zamkniety (f5-a7)" przy zmianie sezonu,
    /// wariant "sygnal KS (f5-a7)" w diagnozie f4-pomiary. Zapis arm_ks_a7 (srednie cen sygnalu miast, rodzaj Held, liczniki sezonu; straznik rozmiaru) - brak klucza
    /// (stary zapis) = srednia od pierwszego kroku i Held od zera; starsza DLL klucz pomija.
    /// Pieniadz: tylko przebudowa (kiesa wsi -> kasa targu za material zdjety z polki + lud miasta za prace) - ta sama droga co dotad; nic z niczego.
    /// Wyjatki per miasto i okreg liczone (potkniecia) - bez globalnych wylacznikow.
    /// </summary>
    internal static partial class StartCalibration
    {
        // ------------------------------------------------------------ klucze
        /// <summary>f5-a7: regula A7 mierzy dochod sygnalem KS (klucz Ks Switch Signal).</summary>
        internal static bool A7sOn { get { var s = Settings.Current; return s != null && s.KsSwitchSignal; } }

        /// <summary>f5-a7: regula A7 naprawde zmienia zajecia - dawny klucz albo sygnal KS z kluczem Ks Switch Signal Rule (klucze f5 wylaczone = Start Calibration Switch Rule).</summary>
        internal static bool A7RuleOn { get { var s = Settings.Current; return s != null && (s.StartCalibrationSwitchRule || (s.KsSwitchSignal && s.KsSwitchSignalRule)); } }

        private static double A7sBand { get { var s = Settings.Current; double b = s != null ? s.KsSwitchMarketBand : 0.15; if (!(b >= 0)) b = 0; return Math.Min(0.5, b); } }

        private const int A7sAvg = 4;               // srednia wykladnicza 4 krokow (co 7 dob - ok. 28 dob)
        private const int A7sSaveMax = 65536;       // straznik rozmiaru arm_ks_a7 (znakow); ponad - zapis bez srednich cen
        private const string A7sVer = "a7s1";
        private const string A7sP = "KS: ";

        // ------------------------------------------------------------ stan (zapis arm_ks_a7)
        private static readonly Dictionary<Town, double[]> _sig = new Dictionary<Town, double[]>();   // cena sygnalu [NG], srednia
        private static readonly Dictionary<Town, int> _sigN = new Dictionary<Town, int>();
        private static Dictionary<string, KeyValuePair<int, double[]>> _sigLoad;                     // z zapisu (id miasta) - do pierwszego odczytu
        private static int _sigDay = -1, _sigPlanDay = -1;
        private static int _heldKind = -1;          // Held liczony: 1 sygnalem KS, 0 indeksem gry, -1 nieznane (stary zapis / nowa kampania)
        private sealed class A7sSeason { public int S = -1, Blocks; public double Hands; public readonly int[] MkB = new int[2]; public readonly double[] MkH = new double[2]; public readonly Dictionary<int, double[]> Tr = new Dictionary<int, double[]>(); }
        private static A7sSeason _a7sSea = new A7sSeason();

        // ------------------------------------------------------------ pamiec sesji
        private static double[] _sigMv;             // wartosc kg Planu na pracujacego [c * NG + g] (konie HorseVal)
        private static Dictionary<string, double> _sigRent; private static object _sigRentFor;   // ZW: uid -> renta bloku Planu
        private static readonly Dictionary<double[], double[]> _sigV = new Dictionary<double[], double[]>();   // krok: wartosc towarow zajecia na wiersz ceny (pamiec)
        private static ItemObject[] _a7sMat;
        private static readonly Dictionary<string, int[]> _a7sLast = new Dictionary<string, int[]>(StringComparer.Ordinal);   // blok -> [z, na, doba] ostatniej zmiany (powroty)
        // liczniki od ostatniej linii kroku
        private static int _a7sN, _a7sNoSig, _a7sPress, _a7sHeld, _a7sStarted, _a7sDone, _a7sCancel, _a7sFlip, _a7sSt, _a7sNoShelf, _a7sNoMat, _a7sHeldZero, _a7sDays, _a7sSaveOver;
        private static double _a7sHands, _a7sPressH, _a7sHeldH, _a7sMsSum, _a7sMsMax;
        private static long _a7sVil, _a7sTown, _a7sFolk, _a7sMatG, _a7sOld, _a7sPcs, _a7sDrop;
        private static readonly List<string> _a7sNotes = new List<string>();

        private static void A7sReset()
        {
            _sig.Clear(); _sigN.Clear(); _sigLoad = null; _sigDay = -1; _sigPlanDay = -1; _heldKind = -1; _a7sSea = new A7sSeason();
            _sigMv = null; _sigRent = null; _sigRentFor = null; _sigV.Clear(); _a7sMat = null; _a7sLast.Clear();
            A7sZero(); _a7sSaveOver = 0;
        }

        private static void A7sZero()
        {
            _a7sN = _a7sNoSig = _a7sPress = _a7sHeld = _a7sStarted = _a7sDone = _a7sCancel = _a7sFlip = _a7sSt = _a7sNoShelf = _a7sNoMat = _a7sHeldZero = _a7sDays = 0;
            _a7sHands = _a7sPressH = _a7sHeldH = _a7sMsSum = _a7sMsMax = 0;
            _a7sVil = _a7sTown = _a7sFolk = _a7sMatG = _a7sOld = _a7sPcs = _a7sDrop = 0;
            _a7sNotes.Clear();
        }

        // ------------------------------------------------------------ cena sygnalu
        private static double[] A7sPlanP()
        {
            var pl = _plan;
            return pl != null && pl.Error == null && pl.P != null && pl.P.Length == 2 * KsTab.NG ? pl.P : null;
        }

        private static void A7sEnsureMv()
        {
            if (_sigMv != null) return;
            EnsureValues();
            var v = (double[])_valKg.Clone();
            v[KsTab.GHorse] = KsTab.HorseVal;                    // jak Plan (KsCore.Prepare): kon 120 d - stadniny licza konie jak Plan, ktory je tam postawil
            _sigMv = KsCore.CraftValue(v);
        }

        /// <summary>Gradient krainy dla towaru g (Plan albo zapis, x ciecie nadmiaru krainy) - ta sama regula co LandGradZ.</summary>
        private static double A7sGrad(string land, int g)
        {
            var gr = _plan != null && _plan.Error == null ? _plan.Grad : _grad;
            double v = 1.0; double[] a, c;
            if (gr != null && land != null && gr.TryGetValue(land, out a) && a != null && g < a.Length && a[g] > 0 && !double.IsNaN(a[g]) && !double.IsInfinity(a[g])) v = a[g];
            if (land != null && _landCut.TryGetValue(land, out c) && c != null && g < c.Length && c[g] > 0 && c[g] < 1.0) v *= c[g];
            return v;
        }

        /// <summary>Cena sygnalu miasta t dzis (bez sredniej): P rynku x gradient krainy x odchylenie rynku wzgledem mediany towarow pola miasta (pasmo).</summary>
        private static double[] A7sInst(Town t, double[] P, double band)
        {
            int m = TownMarket(t) == 0 ? 0 : 1;
            string land = t.Settlement != null ? TownLandOf(t.Settlement) : "?";
            double[] px; _px.TryGetValue(t, out px);
            var ratio = new double[KsTab.NG];
            for (int g = 0; g < KsTab.NG; g++) ratio[g] = double.NaN;
            double med = double.NaN;
            if (px != null && band > 0)
            {
                var vals = new List<double>(KsTab.NG);
                for (int g = 0; g < KsTab.NG; g++)
                {
                    if (KsTab.RegGoodMask[g] || g == KsTab.GHorse) continue;           // zloza (regale) i konie (lancuch gry) - bez rynku w sygnale
                    double ix = Px(px, g), rf = KsCore.StartIndex(KsTab.Goods[g], P, m);
                    if (ix > 0 && rf > 0 && !double.IsInfinity(ix)) { ratio[g] = ix / rf; vals.Add(ratio[g]); }
                }
                if (vals.Count >= 3) med = Med(vals);
            }
            double lo = 1.0 / (1.0 + band), hi = 1.0 + band;
            var row = new double[KsTab.NG];
            for (int g = 0; g < KsTab.NG; g++)
            {
                double p = P[m * KsTab.NG + g];
                if (!(p > 0) || double.IsNaN(p) || double.IsInfinity(p)) p = 1.0;
                p = Math.Max(0.05, Math.Min(5.0, p));
                double dev = 1.0;
                if (!double.IsNaN(ratio[g]) && med > 0) dev = Math.Max(lo, Math.Min(hi, ratio[g] / med));
                row[g] = p * A7sGrad(land, g) * dev;
            }
            return row;
        }

        /// <summary>Srednia ceny sygnalu wszystkich miast - raz na 7 dob (wspolna dla A7 i ZW), tylko z Planem bez bledu (bez Planu - zostaje srednia z wczoraj / z zapisu).</summary>
        private static void A7sUpdate(int today)
        {
            if (_sigDay >= 0 && today >= _sigDay && today - _sigDay < StepDays) return;
            var P = A7sPlanP();
            if (P == null) return;
            A7sResolveLoad();
            double band = A7sBand;
            foreach (var t in Town.AllTowns)
            {
                if (t == null || t.Settlement == null) continue;
                try
                {
                    var inst = A7sInst(t, P, band);
                    double[] a; int n;
                    _sigN.TryGetValue(t, out n);
                    if (!_sig.TryGetValue(t, out a) || a == null || a.Length != KsTab.NG) { _sig[t] = inst; n = 0; }
                    else { double w = 1.0 / Math.Min(A7sAvg, n + 1); for (int g = 0; g < KsTab.NG; g++) a[g] += (inst[g] - a[g]) * w; }
                    _sigN[t] = Math.Min(999, n + 1);
                }
                catch (Exception e) { _a7sSt++; Stumble("A7sUpdate(miasto)", e); }
            }
            _sigDay = today; _sigPlanDay = _planDay;
            _sigV.Clear();
        }

        private static void A7sResolveLoad()
        {
            var ld = _sigLoad; if (ld == null) return;
            _sigLoad = null;
            foreach (var t in Town.AllTowns)
            {
                if (t == null || t.Settlement == null || t.Settlement.StringId == null || _sig.ContainsKey(t)) continue;
                KeyValuePair<int, double[]> kv;
                if (!ld.TryGetValue(t.Settlement.StringId, out kv) || kv.Value == null || kv.Value.Length != KsTab.NG) continue;
                _sig[t] = (double[])kv.Value.Clone(); _sigN[t] = kv.Key;
            }
        }

        private static double[] A7sRow(Town t)
        {
            if (t == null) return null;
            if (_sigLoad != null) A7sResolveLoad();
            double[] a;
            return _sig.TryGetValue(t, out a) ? a : null;
        }

        /// <summary>Pamiec wartosci towarow zajecia przy wierszu ceny (suma raz na miasto i zajecie w kroku - ta sama dla reguly i diagnozy).</summary>
        private static double[] A7sVRow(double[] row)
        {
            double[] v;
            if (!_sigV.TryGetValue(row, out v)) { v = new double[KsTab.NC]; for (int c = 0; c < v.Length; c++) v[c] = double.NaN; _sigV[row] = v; }
            return v;
        }

        /// <summary>Dochod rodziny na pracujacego zajecia c bloku przy cenie sygnalu (wzor Planu); rent - renta ziemi bloku (A7sRent).</summary>
        private static double A7sIncome(LiveBlock b, string type, int c, double[] row, double[] vRow, double rent)
        {
            double v = vRow[c];
            if (double.IsNaN(v))
            {
                v = 0; int o = c * KsTab.NG;
                for (int g = 0; g < KsTab.NG; g++) { double m = _sigMv[o + g]; if (m != 0) v += m * row[g]; }
                vRow[c] = v;
            }
            return b.L(c, type) * v - KsTab.KCost[c] - rent * KsTab.Ha(c, b.K);
        }

        /// <summary>Renta ziemi bloku do sygnalu: z biezacego Planu (ta sama doba cen cienia co P), bez wpisu - renta z Planu wlaczenia (b.Rent).</summary>
        private static double A7sRent(LiveBlock b)
        {
            double r;
            string uid = b.K != null ? b.K.Uid : b.Uid;
            return A7sRentTry(uid, out r) ? r : b.Rent;
        }

        // ------------------------------------------------------------ krok A7 (RuleDaily)
        /// <summary>Doba kroku A7 z kluczem: srednia cen sygnalu, Held od zera przy pierwszym kroku tego sygnalu (60 dob nacisku nowego sygnalu, nie starego).</summary>
        private static void A7sStepBegin(int today)
        {
            A7sEnsureMv();
            A7sUpdate(today);
            if (_heldKind != 1)
            {
                int z = 0;
                foreach (var ld in _live.Values)
                {
                    if (ld == null || ld.B == null) continue;
                    foreach (var b in ld.B) if (b != null && b.Held > 0) { b.Held = 0; z++; }
                }
                _a7sHeldZero += z; _heldKind = 1;
            }
        }

        /// <summary>Klucz sygnalu wylaczony: Held liczy dawny indeks gry (bez zerowania - droga jak przed paczka).</summary>
        private static void A7sOff() { if (_heldKind == 1) _heldKind = 0; }

        private static void A7sNoteEval(LiveBlock b, int ci) { _a7sN++; _a7sHands += b.Hands * b.X[ci]; }

        private static void A7sNotePress(LiveBlock b, int ci)
        {
            double h = b.Hands * b.X[ci];
            _a7sPress++; _a7sPressH += h;
            if (b.Held >= HoldDays) { _a7sHeld++; _a7sHeldH += h; }
        }

        /// <summary>Zmiana zaczeta: sezon (bloki, rece, rynek, przejscie), powroty A->B->A w 364 dobach, komunikat gracza (jego wies).</summary>
        private static void A7sNoteStart(LiveDist ld, LiveBlock b, int cur, int best, double hands, Town mt, int T, int today)
        {
            try
            {
                _a7sStarted++;
                int m = ld.Market == 0 ? 0 : 1;
                var sea = _a7sSea;
                if (sea.S != _regionSeason) { sea = _a7sSea = new A7sSeason { S = _regionSeason }; }
                sea.Blocks++; sea.Hands += hands; sea.MkB[m]++; sea.MkH[m] += hands;
                double[] tr; int k = cur * 64 + best;
                if (!sea.Tr.TryGetValue(k, out tr)) { if (sea.Tr.Count < 200) { tr = new double[2]; sea.Tr[k] = tr; } }
                if (tr != null) { tr[0] += 1; tr[1] += hands; }
                string key = (b.Uid ?? (b.K != null ? b.K.Uid : null) ?? "?") + "@" + (ld.Id ?? "?");
                int[] last;
                if (_a7sLast.TryGetValue(key, out last) && last[0] == best && last[1] == cur && today - last[2] <= 364) _a7sFlip++;
                _a7sLast[key] = new[] { cur, best, today };
                if (_a7sNotes.Count < 8) _a7sNotes.Add((ld.St != null && ld.St.Name != null ? ld.St.Name.ToString() : ld.Id) + " " + KsTab.Crafts[cur] + "->" + KsTab.Crafts[best] + " " + Math.Round(hands));
                A7sTellPlayer(ld, b, cur, best, mt, T);
            }
            catch (Exception e) { _a7sSt++; Stumble("A7sNoteStart", e); }
        }

        /// <summary>Komunikat gracza (po angielsku) - tylko wies gracza albo wies jego twierdzy; AI zmienia sie tak samo, bez komunikatu.</summary>
        private static void A7sTellPlayer(LiveDist ld, LiveBlock b, int cur, int best, Town mt, int T)
        {
            var pc = Clan.PlayerClan;
            if (pc == null || ld.TF || ld.V == null || ld.V.Settlement == null) return;
            bool mine = ld.V.Settlement.OwnerClan == pc || (ld.V.Bound != null && ld.V.Bound.OwnerClan == pc);
            if (!mine) return;
            string vn = ld.V.Name != null ? ld.V.Name.ToString() : ld.Id;
            string a = VillageCrafts.NameOf(KsTab.Crafts[cur]), z = VillageCrafts.NameOf(KsTab.Crafts[best]);
            string tn = mt != null && mt.Name != null ? mt.Name.ToString() : "the market";
            Log.Player(vn + ": part of its fields is turning from " + a + " to " + z + " - " + z + " sells better at " + tn + ". The change takes about " + T + " days; the village pays about "
                       + Math.Max(0, (int)Math.Round(b.Cost)).ToString(Inv) + " denars for timber, tools and the town's craftsmen as it goes.");
        }

        /// <summary>Rata przebudowy (Progress): ta sama droga VillageCrafts.PayRebuild, do tego pomiar - zloto z kies wsi / do kas targu i ludu, material, sztuki = zmiana polek.</summary>
        private static long A7sPay(Village v, Town mt, int ask)
        {
            long vil0 = _tally.Vil, town0 = _tally.Town, folk0 = _tally.Folk, mat0 = _tally.Mat, old0 = _tally.Old; int pcs0 = _tally.Pcs, nm0 = _tally.NoMat;
            int[] before = A7sShelf(mt);
            long got = VillageCrafts.PayRebuild(v, mt, ask, _tally);
            int[] after = A7sShelf(mt);
            _a7sVil += _tally.Vil - vil0; _a7sTown += _tally.Town - town0; _a7sFolk += _tally.Folk - folk0; _a7sMatG += _tally.Mat - mat0; _a7sOld += _tally.Old - old0;
            _a7sPcs += _tally.Pcs - pcs0; _a7sNoMat += _tally.NoMat - nm0;
            if (before != null && after != null) for (int k = 0; k < before.Length; k++) _a7sDrop += before[k] - after[k];
            else _a7sNoShelf++;
            return got;
        }

        private static int[] A7sShelf(Town mt)
        {
            try
            {
                if (mt == null || mt.Settlement == null || mt.Settlement.ItemRoster == null) return null;
                if (_a7sMat == null) _a7sMat = BuildFunding.DsWorksItems();
                var r = mt.Settlement.ItemRoster; var a = new int[_a7sMat.Length];
                for (int k = 0; k < a.Length; k++) a[k] = _a7sMat[k] != null ? r.GetItemNumber(_a7sMat[k]) : 0;
                return a;
            }
            catch (Exception e) { _a7sSt++; Stumble("A7sShelf", e); return null; }
        }

        private static void A7sTime(long t0)
        {
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            _a7sDays++; _a7sMsSum += ms; if (ms > _a7sMsMax) _a7sMsMax = ms;
        }

        // ------------------------------------------------------------ linie
        private static void A7sTotals(out int blocks, out double world)
        {
            blocks = 0; world = 0;
            foreach (var ld in _live.Values)
            {
                if (ld == null || ld.B == null || ld.St == null || LandBackOf(ld.St)) continue;
                foreach (var b in ld.B) if (b != null && b.C != null) blocks++;
            }
            foreach (var v in _regionHands.Values) world += v;
        }

        private static string A7sRegionText(out double maxShare, out int nReg)
        {
            maxShare = 0; nReg = 0;
            var l = new List<KeyValuePair<string, double>>();
            foreach (var kv in _regionMoved)
            {
                if (!(kv.Value > 0)) continue;
                double rh; _regionHands.TryGetValue(kv.Key, out rh);
                double sh = rh > 0 ? kv.Value / rh : 1.0;
                nReg++; if (sh > maxShare) maxShare = sh;
                l.Add(new KeyValuePair<string, double>(kv.Key, sh));
            }
            l.Sort((x, y) => y.Value.CompareTo(x.Value));
            var sb = new StringBuilder(200);
            for (int i = 0; i < l.Count && i < 3; i++)
            {
                string nm = l[i].Key;
                try { var st = Settlement.Find(l[i].Key); if (st != null && st.Name != null) nm = st.Name.ToString(); } catch { }
                sb.Append(i == 0 ? "" : ", ").Append(nm).Append(' ').Append((100 * l[i].Value).ToString("0.00", Inv)).Append('%');
            }
            return l.Count > 0 ? sb.ToString() : "-";
        }

        private static string A7sTop(Dictionary<int, double[]> tr, int k)
        {
            if (tr == null || tr.Count == 0) return "-";
            var l = new List<KeyValuePair<int, double[]>>(tr);
            l.Sort((x, y) => y.Value[0] != x.Value[0] ? y.Value[0].CompareTo(x.Value[0]) : y.Value[1].CompareTo(x.Value[1]));
            var sb = new StringBuilder(300);
            for (int i = 0; i < l.Count && i < k; i++)
            {
                int a = l[i].Key / 64, b = l[i].Key % 64;
                sb.Append(i == 0 ? "" : "; ").Append(a >= 0 && a < KsTab.NC ? KsTab.Crafts[a] : "?").Append("->").Append(b >= 0 && b < KsTab.NC ? KsTab.Crafts[b] : "?").Append(' ')
                  .Append(l[i].Value[0].ToString("0", Inv)).Append('/').Append(l[i].Value[1].ToString("0", Inv));
            }
            return sb.ToString();
        }

        private static string A7sSeasonText(A7sSeason sea, int blocks, double world)
        {
            return "zaczete " + sea.Blocks + " blokow (" + (blocks > 0 ? (100.0 * sea.Blocks / blocks).ToString("0.00", Inv) : "-") + "% blokow " + blocks + "), rak " + sea.Hands.ToString("0", Inv)
                   + " (" + (world > 0 ? (100.0 * sea.Hands / world).ToString("0.00", Inv) : "-") + "% rak pola swiata " + world.ToString("0", Inv) + "); Westeros " + sea.MkB[0] + " blokow / rak "
                   + sea.MkH[0].ToString("0", Inv) + ", Essos " + sea.MkB[1] + " / " + sea.MkH[1].ToString("0", Inv);
        }

        /// <summary>Linia doby kroku A7 (klucz Ks Switch Signal) - liczniki od ostatniej linii, sezon od poczatku sezonu.</summary>
        private static void A7sStepLine(int today, int d0, bool rule)
        {
            int blocks; double world; A7sTotals(out blocks, out world);
            double maxShare; int nReg; string regTop = A7sRegionText(out maxShare, out nReg);
            var sea = _a7sSea.S == _regionSeason ? _a7sSea : new A7sSeason { S = _regionSeason };
            int ds = today - _regionSeason * Season;
            var sb = new StringBuilder(2400);
            sb.Append(A7sP).Append("A7 regula zajec (f5-a7) dzien ").Append(today).Append(" | D ").Append(d0).Append(" (D+").Append(today - d0).Append(")")
              .Append(" | sygnal: ceny KS (cena cienia Planu doby ").Append(_sigPlanDay).Append(" x gradient krainy x rynek miasta +-").Append((100 * A7sBand).ToString("0", Inv))
              .Append("% wzgledem mediany, srednia ").Append(A7sAvg).Append(" krokow, miast ").Append(_sig.Count).Append(")")
              .Append(" | regula: ").Append(rule ? "TAK (" + (Settings.Current != null && Settings.Current.StartCalibrationSwitchRule ? "Start Calibration Switch Rule" : "Ks Switch Signal Rule") + ")" : "WYLACZONA - sam pomiar")
              .Append(" | ocenionych ").Append(_a7sN).Append(" blokow, rak ").Append(_a7sHands.ToString("0", Inv)).Append(", bez sygnalu miasta ").Append(_a7sNoSig)
              .Append("; nacisk (>= x").Append(SwitchRatio.ToString("0.00", Inv)).Append(") ").Append(_a7sPress).Append(" blokow (").Append(Pc(_a7sPress, _a7sN)).Append("%), rak ").Append(_a7sPressH.ToString("0", Inv))
              .Append(" (").Append(_a7sHands > 0 ? (100.0 * _a7sPressH / _a7sHands).ToString("0.0", Inv) : "-").Append("%)")
              .Append("; trzymane >= ").Append(HoldDays).Append(" dob ").Append(_a7sHeld).Append(" blokow (").Append(Pc(_a7sHeld, _a7sN)).Append("%), rak ").Append(_a7sHeldH.ToString("0", Inv))
              .Append(_a7sHeldZero > 0 ? "; Held wyzerowane (pierwszy krok sygnalu KS) " + _a7sHeldZero + " blokow" : "")
              .Append(" | sezon ").Append(_regionSeason).Append(" (doba sezonu ").Append(ds).Append('/').Append(Season).Append("): ").Append(A7sSeasonText(sea, blocks, world))
              .Append("; najwiekszy udzial regionu ").Append((100 * maxShare).ToString("0.00", Inv)).Append("% (prog ").Append((100 * RegionShare).ToString("0", Inv)).Append("%), regionow ze zmiana ").Append(nReg)
              .Append(", top: ").Append(regTop)
              .Append(" | od linii: zaczete ").Append(_a7sStarted).Append(", w toku ").Append(_rActive).Append(", skonczone ").Append(_a7sDone).Append(", porzucone ").Append(_a7sCancel)
              .Append(", powroty (A->B->A w 364 dobach) ").Append(_a7sFlip)
              .Append(_a7sNotes.Count > 0 ? " (" + string.Join("; ", _a7sNotes.ToArray()) + ")" : "")
              .Append(" | przejscia sezonu (top 8, blokow/rak): ").Append(A7sTop(sea.Tr, 8))
              .Append(" | przebudowa od linii: kiesy wsi -").Append(_a7sVil).Append(" / kasy targow +").Append(_a7sTown).Append(" / lud miast +").Append(_a7sFolk)
              .Append(" (roznica ").Append(_a7sVil - _a7sTown - _a7sFolk).Append("); material zaplacony ").Append(_a7sMatG).Append(" zl (do kas targow ").Append(_a7sTown - _a7sOld)
              .Append(", roznica ").Append(_a7sMatG - (_a7sTown - _a7sOld)).Append("), sztuk ").Append(_a7sPcs).Append(", zmiana polek ").Append(_a7sDrop).Append(" (roznica ").Append(_a7sPcs - _a7sDrop)
              .Append(", bez odczytu polki ").Append(_a7sNoShelf).Append("); droga dawna bez towaru ").Append(_a7sOld).Append("; dob bez materialu albo kiesy ").Append(_a7sNoMat)
              .Append(" | czas reguly ").Append(_a7sDays > 0 ? (_a7sMsSum / _a7sDays).ToString("0.00", Inv) : "-").Append(" ms/d (najwiecej ").Append(_a7sMsMax.ToString("0.00", Inv)).Append(", dob ").Append(_a7sDays)
              .Append("), potkniecia ").Append(_a7sSt).Append('.');
            Log.Info(sb.ToString());
            A7sZero();
        }

        /// <summary>Zmiana sezonu (RuleDaily, przed wyzerowaniem rak regionu): linia sezonu zamknietego - zaczete bloki i rece wedlug rynku, najwiekszy udzial regionu, przejscia.</summary>
        private static void A7sSeasonClose(int oldSeason)
        {
            if (oldSeason < 0) return;
            try
            {
                int blocks; double world; A7sTotals(out blocks, out world);
                double maxShare; int nReg; string regTop = A7sRegionText(out maxShare, out nReg);
                var sea = _a7sSea.S == oldSeason ? _a7sSea : new A7sSeason { S = oldSeason };
                Log.Info(A7sP + "A7 sezon zamkniety (f5-a7) sezon " + oldSeason + " | " + A7sSeasonText(sea, blocks, world) + "; najwiekszy udzial regionu " + (100 * maxShare).ToString("0.00", Inv)
                         + "% (prog " + (100 * RegionShare).ToString("0", Inv) + "%), regionow ze zmiana " + nReg + ", top: " + regTop + " | przejscia (top 10, blokow/rak): " + A7sTop(sea.Tr, 10)
                         + " | regula " + (A7RuleOn ? "TAK" : "WYLACZONA - sam pomiar") + ".");
            }
            catch (Exception e) { _a7sSt++; Stumble("A7sSeasonClose", e); }
            _a7sSea = new A7sSeason();
        }

        // ------------------------------------------------------------ droga bez KS (zajecia wiosek pobocznych, VillageCrafts.Rule)
        /// <summary>ZW (klucze Ks Switch Signal i Side Village Craft Ks Signal): sygnal KS gotowy dla wioski z miastem targowym mt (Plan bez bledu albo srednia z zapisu).
        /// Watek glowny, nie rzuca.</summary>
        internal static bool SideSignalReady(Town mt, int today)
        {
            var s = Settings.Current;
            if (s == null || !s.KsSwitchSignal || !s.SideVillageCraftKsSignal || mt == null) return false;
            try { A7sEnsureMv(); A7sUpdate(today); return A7sRow(mt) != null; }
            catch (Exception e) { _a7sSt++; Stumble("SideSignalReady", e); return false; }
        }

        /// <summary>ZW: dochod rodziny na pracujacego zajecia c wioski pobocznej (wiersz ziemi row) przy cenie sygnalu miasta mt - ta sama funkcja co A7 (przydatnosc ziemi KsTab.Land2,
        /// wartosc kg Planu, koszt K, renta bloku Planu wioski x ha strefy). Po SideSignalReady; nie rzuca.</summary>
        internal static double SideSignalIncome(Town mt, LandRow row, int c, string dtype)
        {
            try
            {
                var r = A7sRow(mt);
                if (r == null || row == null || c < 0 || c >= KsTab.NC) return 0.0;
                A7sEnsureMv();
                double v = 0; int o = c * KsTab.NG;
                for (int g = 0; g < KsTab.NG; g++) { double m = _sigMv[o + g]; if (m != 0) v += m * r[g]; }
                double L = row.O != null ? KsTab.Land2(c, row.O, dtype) : 0.0;
                return L * v - KsTab.KCost[c] - A7sRentOf(row.Uid) * KsTab.Ha(c, row.O != null ? row.O.Zone : null);
            }
            catch (Exception e) { _a7sSt++; Stumble("SideSignalIncome", e); return 0.0; }
        }

        private static double A7sRentOf(string uid) { double r; return A7sRentTry(uid, out r) ? r : 0.0; }

        /// <summary>Renta bloku uid z biezacego Planu bez bledu (pamiec na Plan); false - brak Planu albo bloku.</summary>
        private static bool A7sRentTry(string uid, out double r)
        {
            r = 0.0;
            var pl = _plan;
            if (uid == null || pl == null || pl.Error != null || pl.Blocks == null || pl.Rent == null) return false;
            if (!ReferenceEquals(_sigRentFor, pl) || _sigRent == null)
            {
                var d = new Dictionary<string, double>(StringComparer.Ordinal);
                for (int b = 0; b < pl.Blocks.Length && b < pl.Rent.Length; b++) { var kb = pl.Blocks[b]; if (kb != null && kb.Uid != null && !d.ContainsKey(kb.Uid)) d[kb.Uid] = pl.Rent[b]; }
                _sigRent = d; _sigRentFor = pl;
            }
            if (!_sigRent.TryGetValue(uid, out r)) return false;
            if (!(r > 0) || double.IsNaN(r) || double.IsInfinity(r)) r = 0.0;
            return true;
        }

        // ------------------------------------------------------------ zapis arm_ks_a7
        // a7s1|doba sredniej|doba Planu|rodzaj Held|sezon~bloki~rece~Wb~Wh~Eb~Eh~przejscia (k:n:h,...)|miasto:n:ceny (3 znaki base36 na towar, cena x 1000)!miasto...
        internal static string ExportA7s()
        {
            try
            {
                if (_sig.Count == 0 && _sigLoad == null && _heldKind < 0 && _a7sSea.Blocks == 0) return null;   // nic do zapisania - bez klucza (zapis jak przed paczka)
                var sb = new StringBuilder(16000);
                sb.Append(A7sVer).Append('|').Append(_sigDay).Append('|').Append(_sigPlanDay).Append('|').Append(_heldKind).Append('|');
                var sea = _a7sSea;
                sb.Append(sea.S).Append('~').Append(sea.Blocks).Append('~').Append(sea.Hands.ToString("0", Inv)).Append('~').Append(sea.MkB[0]).Append('~').Append(sea.MkH[0].ToString("0", Inv))
                  .Append('~').Append(sea.MkB[1]).Append('~').Append(sea.MkH[1].ToString("0", Inv)).Append('~');
                bool f = true;
                foreach (var kv in sea.Tr) { if (!f) sb.Append(','); f = false; sb.Append(kv.Key).Append(':').Append(kv.Value[0].ToString("0", Inv)).Append(':').Append(kv.Value[1].ToString("0", Inv)); }
                sb.Append('|');
                int head = sb.Length;
                f = true;
                if (_sigLoad != null) A7sResolveLoad();
                foreach (var kv in _sig)
                {
                    if (kv.Key == null || kv.Key.Settlement == null || kv.Value == null || kv.Value.Length != KsTab.NG) continue;
                    int n; _sigN.TryGetValue(kv.Key, out n);
                    if (!f) sb.Append('!'); f = false;
                    sb.Append(Clean(kv.Key.Settlement.StringId)).Append(':').Append(n).Append(':');
                    for (int g = 0; g < KsTab.NG; g++) sb.Append(E36(Math.Max(0, Math.Min(46655, (int)Math.Round(kv.Value[g] * 1000))), 3));
                }
                if (sb.Length > A7sSaveMax) { sb.Length = head; _a7sSaveOver++; }   // straznik: bez srednich cen (Held i sezon zostaja)
                return sb.ToString();
            }
            catch (Exception e) { Stumble("ExportA7s", e); return null; }
        }

        internal static void ImportA7s(string t)
        {
            try
            {
                if (string.IsNullOrEmpty(t)) return;                     // stary zapis - od zera
                var p = t.Split('|');
                if (p.Length < 6 || p[0] != A7sVer) return;
                int.TryParse(p[1], NumberStyles.Integer, Inv, out _sigDay);
                int.TryParse(p[2], NumberStyles.Integer, Inv, out _sigPlanDay);
                int.TryParse(p[3], NumberStyles.Integer, Inv, out _heldKind);
                var q = p[4].Split('~');
                if (q.Length >= 8)
                {
                    var sea = new A7sSeason();
                    int.TryParse(q[0], NumberStyles.Integer, Inv, out sea.S); int.TryParse(q[1], NumberStyles.Integer, Inv, out sea.Blocks);
                    double.TryParse(q[2], NumberStyles.Float, Inv, out sea.Hands);
                    int.TryParse(q[3], NumberStyles.Integer, Inv, out sea.MkB[0]); double.TryParse(q[4], NumberStyles.Float, Inv, out sea.MkH[0]);
                    int.TryParse(q[5], NumberStyles.Integer, Inv, out sea.MkB[1]); double.TryParse(q[6], NumberStyles.Float, Inv, out sea.MkH[1]);
                    if (q[7].Length > 0)
                        foreach (var e in q[7].Split(','))
                        {
                            var r = e.Split(':'); int k; double n, h;
                            if (r.Length == 3 && int.TryParse(r[0], NumberStyles.Integer, Inv, out k) && double.TryParse(r[1], NumberStyles.Float, Inv, out n) && double.TryParse(r[2], NumberStyles.Float, Inv, out h) && sea.Tr.Count < 200)
                                sea.Tr[k] = new[] { n, h };
                        }
                    _a7sSea = sea;
                }
                var load = new Dictionary<string, KeyValuePair<int, double[]>>(StringComparer.Ordinal);
                if (p[5].Length > 0)
                    foreach (var e in p[5].Split('!'))
                    {
                        var r = e.Split(':'); int n;
                        if (r.Length != 3 || !int.TryParse(r[1], NumberStyles.Integer, Inv, out n) || r[2].Length != 3 * KsTab.NG) continue;
                        var a = new double[KsTab.NG]; bool ok = true;
                        for (int g = 0; g < KsTab.NG && ok; g++) { int v = D36(r[2], 3 * g, 3); if (v < 0) ok = false; else a[g] = v / 1000.0; }
                        if (ok) load[r[0]] = new KeyValuePair<int, double[]>(n, a);
                    }
                _sigLoad = load.Count > 0 ? load : null;
            }
            catch (Exception e) { Stumble("ImportA7s", e); _sigLoad = null; }
        }
    }
}
