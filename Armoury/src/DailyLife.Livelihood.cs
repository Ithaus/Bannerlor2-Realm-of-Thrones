using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// ZC0 - DOSTATEK (EN "Livelihood") i DOCHOD NA GLOWE (projekt docs/PROJEKT-ZYCIE-CODZIENNE-2026-10-16.md rozdz. 9), pomiar na sucho.
    /// D(e,s) = Y(e,s) / (L(e,s) x K_min(s)); osada, region (warownia + jej wsie), krolestwo i swiat - srednia stanow wazona ludzmi (= suma Y / suma L x K).
    /// Y - dochod stanu na dobe w PRAWDZIWYCH zl (srednia biezaca do 28 dob, pamiec sesji):
    ///  - lud miast: zarobek okna klas kiesy czynnej (TownFolk EarnWin, ostatnia doba ludu) NETTO od podatku pana, cechu i dziesieciny x sigma:
    ///    W - biedota i chlopi miejscy (wedlug ludzi), R - rzemieslnicy, P - kupcy i szlachta miejska (wedlug ludzi x zwykly koszyk stanu);
    ///  - podzamcze (zamek): zarobek okna kiesy podzamcza netto x sigma - rzemieslnicy wsi regionu zamku (wedlug ludzi);
    ///  - wies: utarg taborow, ktory wszedl do kiesy wsi (MoneyLedger, powrot taboru) minus renta wsi tej doby (PopulationLaw.RentOf) x sigma - chlopi, rzemieslnicy
    ///    i kupcy wsi (wedlug ludzi x zwykly koszyk stanu); majatki BK z utargu (EstateIncome187 / BK AccumulateTradeTax) x sigma - szlachta wsi;
    ///    plon zjedzony z wlasnego pola: ludzie x czesc najedzona (1 - brak spichlerza Famine) x stol z pola w kg (koszyk x (1 - udzial z targu)) x cena u bramy
    ///    (0.6 x cena miasta targowego) - chlopi, rzemieslnicy, kupcy i szlachta wsi;
    ///  - niewolni: racja od wlasciciela - D = 1 z definicji (Y = L x K);
    ///  - wielkie rody: dochod rodu (ClanIncomeBook D, 1:1 - sa w grze 1:1) po rowno na lenna rodu.
    /// K_min - koszyk przetrwania po cenach lokalnych (DailyLife.Basket.cs): miasto - wartosc x indeks targu (z opalem), wies - u bramy miasta targowego (0.6 x),
    /// zamek - srednia wsi regionu wazona ludzmi. Osada bez ludzi ksiegi (zamek, miasto krainy bez udzialu miast) - D regionu (dalej krolestwa, swiata).
    /// Tylko liczby w pamieci i w logu - pieniadze, towar, ceny, dobrobyt i AI bez zmian.
    /// </summary>
    internal static partial class DailyLife
    {
        private const double Gate = 0.6;      // cena u bramy miasta targowego (rozdz. 9 K_min, rozdz. 9 Y "plon")
        private const int AvgDays = 28;

        private static double[] _pxWorld;                 // mediana cen miast za kg (wies bez miasta targowego)
        private static readonly Dictionary<ItemCategory, double> _idxMemo = new Dictionary<ItemCategory, double>();
        private static readonly double[] _net = new double[3];
        private static Dictionary<Settlement, double[]> _winTaken;

        // liczby doby (do linii)
        private static double _inKept, _inRent, _inEst, _inW, _inR, _inP, _inCastle, _inHouse, _inReturns, _castleLost;
        private static int _inTowns, _inCastles, _noEarn;
        private static double _yWorld, _nkWorld, _nWorld, _dWorld, _medPeas, _medTownK, _medVilK, _regMin, _regMed, _regMax;
        private static readonly double[] _yE = new double[Estates], _nkE = new double[Estates], _nE = new double[Estates];
        private static readonly double[] _yW = new double[2], _nkW = new double[2], _nW = new double[2];   // 0 Westeros, 1 Essos
        private static readonly double[,] _nWE = new double[2, Estates];
        private static int _dOwn, _dReg, _dKing, _dWorldN, _dNone, _regions;
        private static readonly Dictionary<Rec, double[]> _reg = new Dictionary<Rec, double[]>();        // warownia -> [Y, N x K, N, L]
        private static readonly Dictionary<string, double[]> _king = new Dictionary<string, double[]>(StringComparer.Ordinal);   // krolestwo -> [Y, NK, N, niewolni, chlopi, mieszczanie, szlachta, rody]
        private static readonly Dictionary<string, string> _kingName = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly List<KeyValuePair<double, double>> _wv = new List<KeyValuePair<double, double>>();

        /// <summary>Ceny miast i koszyk przetrwania kazdej osady na dzis.</summary>
        private static void PricesDay()
        {
            var lists = new List<double>[Items];
            for (int i = 0; i < Items; i++) lists[i] = new List<double>(120);
            foreach (var r in _recs)
            {
                if (r.Kind != KTown) continue;
                if (r.Px == null) r.Px = new double[Items];
                try { TownPrices(r.St.Town, r.Px, _idxMemo); }
                catch (Exception e) { Stumble("TownPrices", e); Array.Copy(_pxBase, r.Px, Items); }
                for (int i = 0; i < Items; i++) lists[i].Add(r.Px[i]);
            }
            if (_pxWorld == null) _pxWorld = new double[Items];
            for (int i = 0; i < Items; i++) _pxWorld[i] = lists[i].Count > 0 ? Median(lists[i]) : _pxBase[i];
            var tk = new List<double>(120); var vk = new List<double>(600);
            foreach (var r in _recs)
            {
                if (r.Kind == KTown) { r.K = SurvivalBasket(r.Px, true); tk.Add(r.K); }
                else if (r.Kind == KVillage) { r.K = Gate * SurvivalBasket(r.Market != null && r.Market.Px != null ? r.Market.Px : _pxWorld, false); vk.Add(r.K); }
            }
            _medTownK = tk.Count > 0 ? Median(tk) : SurvivalBasket(_pxWorld, true);
            _medVilK = vk.Count > 0 ? Median(vk) : Gate * SurvivalBasket(_pxWorld, false);
            // zamek (konto niewolnych, wielkie rody): kupuje na targu - koszyk miast targowych wsi regionu wazony ludzmi wsi (brak - mediana miast)
            foreach (var r in _recs)
            {
                if (r.Kind != KCastle) continue;
                double s = 0, w = 0;
                if (r.Villages != null)
                    foreach (var v in r.Villages)
                    {
                        if (!(v.L > 0)) continue;
                        double k = v.Market != null && v.Market.K > 0 ? v.Market.K : _medTownK;
                        s += v.L * k; w += v.L;
                    }
                r.K = w > 0 ? s / w : _medTownK;
            }
        }

        /// <summary>Dochod stanow doby (prawdziwe zl) i srednia biezaca do 28 dob.</summary>
        private static void IncomeDay()
        {
            _inKept = _inRent = _inEst = _inW = _inR = _inP = _inCastle = _inHouse = _inReturns = _castleLost = 0; _inTowns = _inCastles = _noEarn = 0;
            double sig = _sigma;
            var win = _winTaken;
            foreach (var r in _recs)
            {
                try
                {
                    r.CastleIn = 0; r.Fed = 1.0;
                    _inHouse += r.Y[EHouse];
                    if (r.Kind == KTown)
                    {
                        int kind;
                        if (r.L > 0 && TownFolk.ZcEarnNet(r.St, _net, out kind) && kind == 0)
                        {
                            _inTowns++; _inW += _net[0]; _inR += _net[1]; _inP += _net[2];
                            Split(r, _net[0] * sig, EPeasant, ELabour, false);
                            r.Y[ECraft] += _net[1] * sig;
                            Split(r, _net[2] * sig, EMerchant, EGentry, true);
                        }
                        else if (r.L > 0) _noEarn++;
                    }
                    else if (r.Kind == KCastle)
                    {
                        int kind;
                        if (TownFolk.ZcEarnNet(r.St, _net, out kind) && kind == 1) { _inCastles++; _inCastle += _net[0]; r.CastleIn = _net[0] * sig; }
                    }
                    else
                    {
                        double kept = 0, est = 0, rentPay = 0;
                        double[] w;
                        if (win != null && win.TryGetValue(r.St, out w)) { kept = w[0]; est = w[2]; _inReturns += w[3]; }
                        long pay, avail;
                        try { PopulationLaw.RentOf(r.St, out pay, out avail); rentPay = Math.Max(0, pay); } catch { rentPay = 0; }
                        _inKept += kept; _inEst += est; _inRent += rentPay;
                        double cash = Math.Max(0.0, kept - rentPay) * sig;
                        try { r.Fed = 1.0 - Math.Max(0.0, Math.Min(1.0, Famine.HungerOf(r.St.Village))); } catch { r.Fed = 1.0; }
                        // gotowka kiesy wsi: chlopi, rzemieslnicy i kupcy wsi wedlug ludzi x zwykly koszyk stanu
                        double wP = r.N[EPeasant] * _usual[EPeasant], wC = r.N[ECraft] * _usual[ECraft], wM = r.N[EMerchant] * _usual[EMerchant], ws = wP + wC + wM;
                        if (ws > 0) { r.Y[EPeasant] += cash * wP / ws; r.Y[ECraft] += cash * wC / ws; r.Y[EMerchant] += cash * wM / ws; }
                        r.Y[EGentry] += Math.Max(0.0, est) * sig;
                        // plon zjedzony z wlasnego pola - cena u bramy
                        var px = r.Market != null && r.Market.Px != null ? r.Market.Px : _pxWorld;
                        var b = BasketOf(r.Cul);
                        var vs = Profs[r.Prof].Vs;
                        for (int e = EPeasant; e <= EGentry; e++)
                        {
                            if (e == ELabour || !(r.N[e] > 0)) continue;
                            double own = 0;
                            for (int g = 0; g < Groups; g++) own += b[e][g] * (1.0 - Mk(e, false, g)) * GroupPrice(px, g, false, vs);
                            r.Y[e] += r.N[e] * r.Fed * own * Gate;
                        }
                    }
                }
                catch (Exception e) { Stumble("IncomeDay(osada)", e); }
            }
            // podzamcze -> rzemieslnicy wsi regionu zamku
            foreach (var r in _recs)
            {
                if (r.Kind != KCastle || !(r.CastleIn > 0)) continue;
                double c = 0;
                if (r.Villages != null) foreach (var v in r.Villages) c += v.N[ECraft];
                if (!(c > 0)) { _castleLost += r.CastleIn; continue; }
                foreach (var v in r.Villages) if (v.N[ECraft] > 0) v.Y[ECraft] += r.CastleIn * v.N[ECraft] / c;
            }
            // niewolni: racja od wlasciciela (D = 1); srednia biezaca
            foreach (var r in _recs)
            {
                if (r.N[ESlave] > 0 && r.K > 0) r.Y[ESlave] = r.N[ESlave] * r.K;
                r.YDays = Math.Min(AvgDays, r.YDays + 1);
                for (int e = 0; e < Estates; e++) r.YAvg[e] += (r.Y[e] - r.YAvg[e]) / r.YDays;
            }
        }

        /// <summary>Kwota y do dwoch stanow wedlug ludzi (cost = x zwykly koszyk stanu).</summary>
        private static void Split(Rec r, double y, int a, int b, bool cost)
        {
            if (!(y > 0)) return;
            double wa = r.N[a] * (cost ? _usual[a] : 1.0), wb = r.N[b] * (cost ? _usual[b] : 1.0), s = wa + wb;
            if (!(s > 0)) return;
            r.Y[a] += y * wa / s; r.Y[b] += y * wb / s;
        }

        /// <summary>Dostatek osad, regionow, krolestw, swiata i stanow.</summary>
        private static void LivelihoodDay()
        {
            _reg.Clear(); _king.Clear(); _kingName.Clear();
            _yWorld = _nkWorld = _nWorld = 0;
            Array.Clear(_yE, 0, Estates); Array.Clear(_nkE, 0, Estates); Array.Clear(_nE, 0, Estates);
            Array.Clear(_yW, 0, 2); Array.Clear(_nkW, 0, 2); Array.Clear(_nW, 0, 2); Array.Clear(_nWE, 0, _nWE.Length);
            foreach (var r in _recs)
            {
                double n = 0, y = 0;
                for (int e = 0; e < Estates; e++) { n += r.N[e]; y += r.YAvg[e]; }
                double k = r.K > 0 ? r.K : _medTownK;
                if (r.Region != null)
                {
                    double[] a;
                    if (!_reg.TryGetValue(r.Region, out a)) { a = new double[4]; _reg[r.Region] = a; }
                    a[0] += y; a[1] += n * k; a[2] += n; a[3] += r.L;
                }
                if (!(n > 0)) continue;
                int w = r.West ? 0 : 1;
                _yWorld += y; _nkWorld += n * k; _nWorld += n;
                _yW[w] += y; _nkW[w] += n * k; _nW[w] += n;
                for (int e = 0; e < Estates; e++) { _yE[e] += r.YAvg[e]; _nkE[e] += r.N[e] * k; _nE[e] += r.N[e]; _nWE[w, e] += r.N[e]; }
                double[] kg;
                if (!_king.TryGetValue(r.KingdomId, out kg)) { kg = new double[8]; _king[r.KingdomId] = kg; _kingName[r.KingdomId] = r.KingdomName; }
                kg[0] += y; kg[1] += n * k; kg[2] += n; kg[3] += r.N[ESlave]; kg[4] += r.N[EPeasant]; kg[5] += r.N[ELabour] + r.N[ECraft] + r.N[EMerchant]; kg[6] += r.N[EGentry]; kg[7] += r.N[EHouse];
            }
            if (_thrNoRegion > 0)
            {
                // konto NIEWOLNI bez regionu trzymania: racja od wlasciciela (D = 1) po medianie koszyka wsi
                double yk = _thrNoRegion * _medVilK;
                _yWorld += yk; _nkWorld += yk; _nWorld += _thrNoRegion;
                _yE[ESlave] += yk; _nkE[ESlave] += yk; _nE[ESlave] += _thrNoRegion;
            }
            _dWorld = _nkWorld > 0 ? _yWorld / _nkWorld : double.NaN;
            // osady
            _dOwn = _dReg = _dKing = _dWorldN = _dNone = 0;
            var regD = new List<double>(260);
            foreach (var kv in _reg) if (kv.Value[1] > 0 && kv.Value[2] > 0) regD.Add(kv.Value[0] / kv.Value[1]);
            _regions = regD.Count;
            regD.Sort();
            _regMin = regD.Count > 0 ? regD[0] : double.NaN; _regMax = regD.Count > 0 ? regD[regD.Count - 1] : double.NaN; _regMed = regD.Count > 0 ? Median(regD) : double.NaN;
            _wv.Clear();
            foreach (var r in _recs)
            {
                double n = 0, y = 0;
                for (int e = 0; e < Estates; e++) { n += r.N[e]; y += r.YAvg[e]; }
                double k = r.K > 0 ? r.K : _medTownK;
                for (int e = 0; e < Estates; e++) r.DE[e] = r.N[e] > 0 && k > 0 ? r.YAvg[e] / (r.N[e] * k) : double.NaN;
                r.D = double.NaN; r.DSrc = -1;
                if (r.Kind != KCastle && r.L > 0 && n > 0 && k > 0) { r.D = y / (n * k); r.DSrc = 0; _dOwn++; }
                else
                {
                    double[] a; double[] kg;
                    if (r.Region != null && _reg.TryGetValue(r.Region, out a) && a[1] > 0 && a[2] > 0) { r.D = a[0] / a[1]; r.DSrc = 1; _dReg++; }
                    else if (_king.TryGetValue(r.KingdomId ?? "-", out kg) && kg[1] > 0) { r.D = kg[0] / kg[1]; r.DSrc = 2; _dKing++; }
                    else if (!double.IsNaN(_dWorld)) { r.D = _dWorld; r.DSrc = 3; _dWorldN++; }
                    else _dNone++;
                }
                if (r.Kind == KVillage && r.N[EPeasant] > 0 && !double.IsNaN(r.DE[EPeasant])) _wv.Add(new KeyValuePair<double, double>(r.DE[EPeasant], r.N[EPeasant]));
            }
            _medPeas = WeightedMedian(_wv);
        }

        private static double Median(List<double> l)
        {
            if (l == null || l.Count == 0) return double.NaN;
            var a = new List<double>(l); a.Sort();
            int n = a.Count;
            return n % 2 == 1 ? a[n / 2] : 0.5 * (a[n / 2 - 1] + a[n / 2]);
        }

        private static double WeightedMedian(List<KeyValuePair<double, double>> l)
        {
            if (l == null || l.Count == 0) return double.NaN;
            l.Sort((x, y) => x.Key.CompareTo(y.Key));
            double tot = 0; foreach (var kv in l) tot += kv.Value;
            if (!(tot > 0)) return double.NaN;
            double acc = 0;
            foreach (var kv in l) { acc += kv.Value; if (acc >= 0.5 * tot) return kv.Key; }
            return l[l.Count - 1].Key;
        }

        // ------------------------------------------------------------ Spizarnia: potrzeba w kg wedlug stanu i krainy wobec towaru targu w kg (rozdz. 7, 12 pkt 5)
        private static readonly double[] _need = new double[Groups], _field = new double[Groups], _mkt = new double[Groups], _act = new double[Groups];
        private static double _kcal, _actTownU, _actVilU, _actCourtU;
        private static int _actDay = -1;
        private static readonly Dictionary<string, long[]> _civil = new Dictionary<string, long[]>(StringComparer.Ordinal);
        private static readonly Dictionary<string, double> _kgOf = new Dictionary<string, double>(StringComparer.Ordinal);

        private static void PantryDay()
        {
            Array.Clear(_need, 0, Groups); Array.Clear(_field, 0, Groups); Array.Clear(_mkt, 0, Groups); Array.Clear(_act, 0, Groups);
            _kcal = 0;
            foreach (var r in _recs)
            {
                bool town = r.Kind == KTown;
                double[][] b = null;
                for (int e = 0; e < Estates; e++)
                {
                    double n = r.N[e];
                    if (!(n > 0)) continue;
                    if (b == null) b = BasketOf(r.Cul);
                    double fed = r.Kind == KVillage && e != EHouse ? r.Fed : 1.0;
                    for (int g = 0; g < Groups; g++)
                    {
                        double q = n * b[e][g];
                        if (!(q > 0)) continue;
                        double m = q * Mk(e, town, g);
                        _need[g] += q; _mkt[g] += m; _field[g] += (q - m) * fed;
                        _kcal += q * Kcal[g];
                    }
                }
            }
            if (_thrNoRegion > 0)
            {
                var b = BasketOf("vlandia");
                for (int g = 0; g < Groups; g++) { double q = _thrNoRegion * b[ESlave][g], m = q * Mk(ESlave, false, g); _need[g] += q; _mkt[g] += m; _field[g] += q - m; _kcal += q * Kcal[g]; }
            }
            // targ dzis: towar ludu z ksiegi towarow (mieszczanie i wsie x sigma, dwory 1:1)
            _actTownU = _actVilU = _actCourtU = 0;
            try { _actDay = GoodsLedger.CivilEaten(_civil); } catch (Exception e) { _actDay = -1; _civil.Clear(); Stumble("CivilEaten", e); }
            foreach (var kv in _civil)
            {
                int g = GroupOfItem(kv.Key);
                if (g < 0 || kv.Value == null || kv.Value.Length < 3) continue;
                double kg = KgOfItem(kv.Key);
                _actTownU += kv.Value[0]; _actVilU += kv.Value[1]; _actCourtU += kv.Value[2];
                _act[g] += ((double)kv.Value[0] + kv.Value[1]) * kg * _sigma + kv.Value[2] * kg;
            }
        }

        private static double KgOfItem(string id)
        {
            double kg;
            if (_kgOf.TryGetValue(id, out kg)) return kg;
            kg = 10.0;
            try
            {
                var it = TaleWorlds.ObjectSystem.MBObjectManager.Instance != null ? TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemObject>(id) : null;
                if (it != null && it.Weight > 0.01f) kg = it.Weight;
            }
            catch { kg = 10.0; }
            _kgOf[id] = kg;
            return kg;
        }
    }
}
