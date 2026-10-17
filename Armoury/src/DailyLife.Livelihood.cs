using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// ZC0 - DOSTATEK (EN "Livelihood") i DOCHOD NA GLOWE (projekt docs/PROJEKT-ZYCIE-CODZIENNE-2026-10-16.md rozdz. 9), pomiar na sucho.
    /// D(e,s) = Y(e,s) / (L(e,s) x K_min(s)). Osada: suma Y / (ludzie x K) - jedno K, wiec to srednia D stanow wazona ludzmi. Region (warownia + jej wsie),
    /// krolestwo, swiat i stan na calym swiecie - srednia D osad wazona LUDZMI: suma (Y / K osady) / suma ludzi (K rozne miedzy osadami: miasto z opalem,
    /// wies u bramy - suma Y / suma L x K wazylaby miasta prawie 2 x). Dochod na glowe = suma Y / suma ludzi x 364.
    /// Y - dochod stanu na dobe w PRAWDZIWYCH zl (srednia biezaca: zwykla do 28 dob, potem wykladnicza 1/28; w zapisie - DailyLife.Export):
    ///  - lud miast: zarobek okna klas kiesy czynnej (TownFolk EarnWin, ostatnia doba ludu) NETTO od podatku pana, cechu i dziesieciny x sigma:
    ///    W - biedota i chlopi miejscy (wedlug ludzi), R - rzemieslnicy, P - kupcy i szlachta miejska (wedlug ludzi x zwykly koszyk stanu); z R i P racja
    ///    niewolnych miasta (ludzie x K, R 40% / P 60% - projekt 5.1), najwyzej do zera (reszta "niepokryte" w linii). Miasto bez liczb kiesy tej doby -
    ///    W/R/P bez wpisu do sredniej (doba brakujaca, nie zero), D miasta z regionu, ludzie W/R/P poza sumami D i dochodu;
    ///  - podzamcze (zamek): zarobek okna kiesy podzamcza netto x sigma - rzemieslnicy wsi regionu zamku (wedlug ludzi);
    ///  - wies: utarg taborow, ktory wszedl do kiesy wsi (MoneyLedger, powrot taboru; juz bez podatku pana) minus renta wsi tej doby (PopulationLaw.RentOf),
    ///    dziesiecina, ktora wyszla z kiesy wsi (TownFolk.ZcVillageTithe - czesc budowy septow; jalmuzna i bez kaplana zostaje we wsi) i danina wojenna
    ///    z oszczednosci (KingdomTreasury.Levies -> NoteVillageLevy) x sigma - BEZ przyciecia dobowego (renta co dobe, tabor co kilka dob) - chlopi,
    ///    rzemieslnicy i kupcy wsi (wedlug ludzi x zwykly koszyk stanu); majatki BK z utargu (EstateIncome187 / BK AccumulateTradeTax) x sigma - szlachta wsi;
    ///    plon zjedzony z wlasnego pola: ludzie x czesc najedzona (1 - brak spichlerza Famine) x stol z pola w kg (koszyk x (1 - udzial z targu)) x cena u bramy
    ///    (0.6 x cena miasta targowego) - chlopi, rzemieslnicy, kupcy i szlachta wsi; Y stanu doby (gotowka + plon) przyciete do >= 0;
    ///  - niewolni: racja od wlasciciela - D = 1 z definicji (Y = L x K);
    ///  - wielkie rody: dochod rodu (ClanIncomeBook D, 1:1 BRUTTO - sa w grze 1:1) po rowno na lenna rodu; D rodow nie jest porownywalne z "x20" projektu 6.1
    ///    (linia pokazuje tez mediane regionow bez rodow, plik - D osady bez rodow).
    /// K_min - koszyk przetrwania po cenach lokalnych (DailyLife.Basket.cs): miasto - wartosc x indeks targu (z opalem), wies - u bramy miasta targowego (0.6 x),
    /// zamek - srednia wsi regionu wazona ludzmi. Osada bez ludzi ksiegi (zamek, miasto krainy bez udzialu miast) - D regionu (dalej krolestwa, swiata).
    /// Liczba nieskonczona albo NaN w dochodzie - 0 i potkniecie (srednia nie zostaje NaN na sesje). Tylko liczby w pamieci, w zapisie i w logu - pieniadze,
    /// towar, ceny, dobrobyt i AI bez zmian.
    /// ZC7 (klucz DailyLifeWarScar, DailyLife.War.cs): wies - uchodzcy okregu poza domem (PeopleBook "u:") licza sie w mianowniku D stanow wolnych (chlopi, rzemieslnicy,
    /// kupcy, szlachta - wedlug ludzi w domu) BEZ dochodu (Rec.AwayE): okreg z polowa ludzi w ucieczce ma polowe Dostatku, a nie wiecej (dochod ludzi w domu
    /// na mniej glow). Ludzie stanow (N), suma stanow = ksiega i liczby ludzi w liniach - bez zmian (sami ludzie w domu).
    /// </summary>
    internal static partial class DailyLife
    {
        private const double Gate = 0.6;      // cena u bramy miasta targowego (rozdz. 9 K_min, rozdz. 9 Y "plon")
        private const int AvgDays = 28;
        private const double SlaveR = 0.4;    // racja niewolnych miasta: R 40% / P 60% (projekt 5.1, jak kiesa ludu)

        private static double[] _pxWorld;                 // mediana cen miast za kg (wies bez miasta targowego)
        private static readonly Dictionary<ItemCategory, double> _idxMemo = new Dictionary<ItemCategory, double>();
        private static readonly double[] _net = new double[3];
        private static Dictionary<Settlement, double[]> _winTaken;

        // liczby doby (do linii)
        private static double _inKept, _inRent, _inEst, _inW, _inR, _inP, _inCastle, _inHouse, _inReturns, _castleLost, _inTithe, _inLevy, _slavePaid, _slaveUnpaid, _vilNeg;
        private static int _inTowns, _inCastles, _noEarn;
        // swiat: ludzie (udzialy), ludzie z liczbami dochodu (_dn), suma Y, suma Y/K; H - bez wielkich rodow
        private static double _yWorld, _ykWorld, _nWorld, _dnWorld, _ykWorldH, _dnWorldH, _dWorld, _medPeas, _medTownK, _medVilK, _regMin, _regMed, _regMax, _regMed0;
        private static readonly double[] _yE = new double[Estates], _ykE = new double[Estates], _nE = new double[Estates], _dnE = new double[Estates];
        private static readonly double[] _yW = new double[2], _ykW = new double[2], _nW = new double[2], _dnW = new double[2];   // 0 Westeros, 1 Essos
        private static readonly double[,] _nWE = new double[2, Estates];
        private static int _dOwn, _dReg, _dKing, _dWorldN, _dNone, _dNoEarn, _regions, _medDays;
        private static bool _warm;
        private static readonly Dictionary<Rec, double[]> _reg = new Dictionary<Rec, double[]>();        // warownia -> [suma Y/K, ludzie, suma Y/K bez rodow, ludzie bez rodow]
        // krolestwo -> [Y, Y/K, ludzie, niewolni, chlopi, mieszczanie, szlachta, rody, ludzie z dochodem, Y/K bez rodow, ludzie z dochodem bez rodow]
        private const int KY = 0, KYK = 1, KN = 2, KSlave = 3, KPeas = 4, KBurgh = 5, KGent = 6, KHouse = 7, KNv = 8, KYKH = 9, KNvH = 10, KLen = 11;
        private static readonly Dictionary<string, double[]> _king = new Dictionary<string, double[]>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> _kingName = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly List<KeyValuePair<double, double>> _wv = new List<KeyValuePair<double, double>>();
        private static readonly int[] _daysHist = new int[AvgDays + 1];

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

        /// <summary>Stan e osady r ma dzis liczby dochodu (miasto bez liczb kiesy: W/R/P - nie).</summary>
        private static bool HasY(Rec r, int e) { return !(r.NoEarn && e >= EPeasant && e <= EGentry); }

        /// <summary>Dochod stanow doby (prawdziwe zl) i srednia biezaca (zwykla do 28 dob, potem wykladnicza 1/28).</summary>
        private static void IncomeDay()
        {
            _inKept = _inRent = _inEst = _inW = _inR = _inP = _inCastle = _inHouse = _inReturns = _castleLost = _inTithe = _inLevy = _slavePaid = _slaveUnpaid = _vilNeg = 0;
            _inTowns = _inCastles = _noEarn = 0;
            double sig = _sigma;
            var win = _winTaken;
            foreach (var r in _recs)
            {
                try
                {
                    r.CastleIn = 0; r.Fed = 1.0; r.NoEarn = false;
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
                            if (r.N[ESlave] > 0 && r.K > 0) OwnersFeedSlaves(r);
                        }
                        else if (r.L > 0) { _noEarn++; r.NoEarn = true; }
                    }
                    else if (r.Kind == KCastle)
                    {
                        int kind;
                        if (TownFolk.ZcEarnNet(r.St, _net, out kind) && kind == 1) { _inCastles++; _inCastle += _net[0]; r.CastleIn = _net[0] * sig; }
                    }
                    else
                    {
                        double kept = 0, est = 0, levy = 0, rentPay = 0, tithe = 0;
                        double[] w;
                        if (win != null && win.TryGetValue(r.St, out w)) { kept = w[0]; est = w[2]; _inReturns += w[3]; levy = w[4]; }
                        long pay, avail;
                        try { PopulationLaw.RentOf(r.St, out pay, out avail); rentPay = Math.Max(0, pay); } catch (Exception e) { rentPay = 0; Stumble("RentOf", e); }
                        try { tithe = Math.Max(0, TownFolk.ZcVillageTithe(r.St)); } catch (Exception e) { tithe = 0; Stumble("ZcVillageTithe", e); }
                        _inKept += kept; _inEst += est; _inRent += rentPay; _inTithe += tithe; _inLevy += levy;
                        // bez przyciecia dobowego: renta i dziesiecina co dobe, tabor co kilka dob - przyciete dopiero Y stanu doby (gotowka + plon)
                        double cash = (kept - rentPay - tithe - levy) * sig;
                        double h = 0;
                        try { h = Famine.HungerOf(r.St.Village); } catch (Exception e) { h = 0; Stumble("HungerOf", e); }
                        if (!Fin(h)) { Stumble("HungerOf-NaN", null, r.Id); h = 0; }
                        r.Fed = 1.0 - Math.Max(0.0, Math.Min(1.0, h));
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
                        for (int e = EPeasant; e <= EGentry; e++) if (r.Y[e] < 0) { _vilNeg -= r.Y[e]; r.Y[e] = 0; }
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
            // niewolni: racja od wlasciciela (D = 1); straznik liczb; srednia biezaca
            foreach (var r in _recs)
            {
                if (r.N[ESlave] > 0 && r.K > 0) r.Y[ESlave] = r.N[ESlave] * r.K;
                int dn = Math.Min(AvgDays, r.YDays + 1);
                for (int e = 0; e < Estates; e++)
                {
                    if (!Fin(r.Y[e])) { Stumble("Y-NaN", null, r.Id + " stan " + EstateName[e]); r.Y[e] = 0; }
                    if (!Fin(r.YAvg[e])) { Stumble("YAvg-NaN", null, r.Id + " stan " + EstateName[e]); r.YAvg[e] = 0; }
                    if (!HasY(r, e)) continue;                        // miasto bez liczb kiesy: W/R/P - doba brakujaca, nie zero
                    r.YAvg[e] += (r.Y[e] - r.YAvg[e]) / dn;
                }
                if (!r.NoEarn) r.YDays = dn;
            }
        }

        /// <summary>Racja niewolnych miasta (ludzie x K) z dochodu wlascicieli: R 40%, P 60% (kupcy i szlachta wedlug dochodu) - najwyzej do zera.</summary>
        private static void OwnersFeedSlaves(Rec r)
        {
            double ration = r.N[ESlave] * r.K;
            double c = Math.Max(0.0, Math.Min(r.Y[ECraft], SlaveR * ration));
            r.Y[ECraft] -= c;
            double pm = Math.Max(0.0, r.Y[EMerchant]), pg = Math.Max(0.0, r.Y[EGentry]), ps = pm + pg;
            double p = Math.Max(0.0, Math.Min(ps, (1.0 - SlaveR) * ration));
            if (ps > 0) { r.Y[EMerchant] -= p * pm / ps; r.Y[EGentry] -= p * pg / ps; }
            _slavePaid += c + p; _slaveUnpaid += Math.Max(0.0, ration - c - p);
        }

        /// <summary>Kwota y do dwoch stanow wedlug ludzi (cost = x zwykly koszyk stanu).</summary>
        private static void Split(Rec r, double y, int a, int b, bool cost)
        {
            if (!(y > 0)) return;
            double wa = r.N[a] * (cost ? _usual[a] : 1.0), wb = r.N[b] * (cost ? _usual[b] : 1.0), s = wa + wb;
            if (!(s > 0)) return;
            r.Y[a] += y * wa / s; r.Y[b] += y * wb / s;
        }

        /// <summary>Dostatek osad, regionow, krolestw, swiata i stanow (w dobie linii).</summary>
        private static void LivelihoodDay()
        {
            _reg.Clear(); _king.Clear(); _kingName.Clear();
            _yWorld = _ykWorld = _nWorld = _dnWorld = _ykWorldH = _dnWorldH = 0;
            Array.Clear(_yE, 0, Estates); Array.Clear(_ykE, 0, Estates); Array.Clear(_nE, 0, Estates); Array.Clear(_dnE, 0, Estates);
            Array.Clear(_yW, 0, 2); Array.Clear(_ykW, 0, 2); Array.Clear(_nW, 0, 2); Array.Clear(_dnW, 0, 2); Array.Clear(_nWE, 0, _nWE.Length);
            Array.Clear(_daysHist, 0, _daysHist.Length);
            int people = 0, low = 0;
            foreach (var r in _recs)
            {
                double k = r.K > 0 ? r.K : _medTownK;
                double n = 0, nv = 0, y = 0, yk = 0, ykH = 0, nvH = 0;
                for (int e = 0; e < Estates; e++)
                {
                    double ne = r.N[e];
                    n += ne;
                    if (!(ne > 0) || !HasY(r, e)) continue;
                    double ye = r.YAvg[e];
                    double nd = ne + r.AwayE[e];                  // ZC7: uchodzcy okregu w mianowniku Dostatku bez dochodu (0 bez klucza DailyLifeWarScar)
                    nv += nd; y += ye; yk += ye / k;
                    if (e != EHouse) { nvH += nd; ykH += ye / k; }
                }
                r.Nv = nv; r.YSum = y; r.YK = yk; r.YKH = ykH; r.NvH = nvH;
                if (r.Region != null)
                {
                    double[] a;
                    if (!_reg.TryGetValue(r.Region, out a)) { a = new double[4]; _reg[r.Region] = a; }
                    a[0] += yk; a[1] += nv; a[2] += ykH; a[3] += nvH;
                }
                if (!(n > 0)) continue;
                people++; if (r.YDays < WarmDays) low++;
                _daysHist[Math.Max(0, Math.Min(AvgDays, r.YDays))]++;
                int w = r.West ? 0 : 1;
                _nWorld += n; _nW[w] += n;
                _yWorld += y; _ykWorld += yk; _dnWorld += nv; _ykWorldH += ykH; _dnWorldH += nvH;
                _yW[w] += y; _ykW[w] += yk; _dnW[w] += nv;
                for (int e = 0; e < Estates; e++)
                {
                    _nE[e] += r.N[e]; _nWE[w, e] += r.N[e];
                    if (r.N[e] > 0 && HasY(r, e)) { _yE[e] += r.YAvg[e]; _ykE[e] += r.YAvg[e] / k; _dnE[e] += r.N[e] + r.AwayE[e]; }
                }
                double[] kg;
                if (!_king.TryGetValue(r.KingdomId, out kg)) { kg = new double[KLen]; _king[r.KingdomId] = kg; _kingName[r.KingdomId] = r.KingdomName; }
                kg[KY] += y; kg[KYK] += yk; kg[KN] += n; kg[KSlave] += r.N[ESlave]; kg[KPeas] += r.N[EPeasant]; kg[KBurgh] += r.N[ELabour] + r.N[ECraft] + r.N[EMerchant];
                kg[KGent] += r.N[EGentry]; kg[KHouse] += r.N[EHouse]; kg[KNv] += nv; kg[KYKH] += ykH; kg[KNvH] += nvH;
            }
            if (_thrNoRegion > 0)
            {
                // konto NIEWOLNI bez regionu trzymania: racja od wlasciciela (D = 1, Y/K = ludzie) po medianie koszyka wsi
                double yk = _thrNoRegion * _medVilK;
                _yWorld += yk; _ykWorld += _thrNoRegion; _nWorld += _thrNoRegion; _dnWorld += _thrNoRegion; _ykWorldH += _thrNoRegion; _dnWorldH += _thrNoRegion;
                _yE[ESlave] += yk; _ykE[ESlave] += _thrNoRegion; _nE[ESlave] += _thrNoRegion; _dnE[ESlave] += _thrNoRegion;
            }
            _dWorld = Div(_ykWorld, _dnWorld);
            _warm = people > 0 && 2 * low > people;
            { int acc = 0; _medDays = 0; for (int d = 0; d <= AvgDays; d++) { acc += _daysHist[d]; if (2 * acc >= people) { _medDays = d; break; } } }
            // osady
            _dOwn = _dReg = _dKing = _dWorldN = _dNone = _dNoEarn = 0;
            var regD = new List<double>(260); var regD0 = new List<double>(260);
            foreach (var kv in _reg)
            {
                if (kv.Value[1] > 0) { double d = kv.Value[0] / kv.Value[1]; if (Fin(d)) regD.Add(d); }
                if (kv.Value[3] > 0) { double d = kv.Value[2] / kv.Value[3]; if (Fin(d)) regD0.Add(d); }
            }
            _regions = regD.Count;
            regD.Sort();
            _regMin = regD.Count > 0 ? regD[0] : double.NaN; _regMax = regD.Count > 0 ? regD[regD.Count - 1] : double.NaN; _regMed = regD.Count > 0 ? Median(regD) : double.NaN;
            _regMed0 = regD0.Count > 0 ? Median(regD0) : double.NaN;
            double dWorld0 = Div(_ykWorldH, _dnWorldH);
            _wv.Clear();
            foreach (var r in _recs)
            {
                double k = r.K > 0 ? r.K : _medTownK;
                for (int e = 0; e < Estates; e++) r.DE[e] = r.N[e] > 0 && k > 0 && HasY(r, e) ? r.YAvg[e] / ((r.N[e] + r.AwayE[e]) * k) : double.NaN;   // ZC7: z uchodzcami okregu
                r.D = double.NaN; r.D0 = double.NaN; r.DSrc = -1;
                double own = Div(r.YK, r.Nv);
                if (r.Kind != KCastle && !r.NoEarn && r.L > 0 && Fin(own)) { r.D = own; r.D0 = Div(r.YKH, r.NvH); r.DSrc = 0; _dOwn++; }
                else
                {
                    double[] a; double[] kg; double d;
                    if (r.Region != null && _reg.TryGetValue(r.Region, out a) && a[1] > 0 && Fin(d = a[0] / a[1])) { r.D = d; r.D0 = Div(a[2], a[3]); r.DSrc = 1; _dReg++; }
                    else if (_king.TryGetValue(r.KingdomId ?? "-", out kg) && kg[KNv] > 0 && Fin(d = kg[KYK] / kg[KNv])) { r.D = d; r.D0 = Div(kg[KYKH], kg[KNvH]); r.DSrc = 2; _dKing++; }
                    else if (Fin(_dWorld)) { r.D = _dWorld; r.D0 = dWorld0; r.DSrc = 3; _dWorldN++; }
                    else _dNone++;
                    if (r.NoEarn) _dNoEarn++;                        // miasto bez liczb kiesy - D zapozyczone (NZC0-5 liczy je jako bez wlasnego D)
                }
                if (r.Kind == KVillage && r.N[EPeasant] > 0 && Fin(r.DE[EPeasant])) _wv.Add(new KeyValuePair<double, double>(r.DE[EPeasant], r.N[EPeasant]));
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
            catch (Exception e) { kg = 10.0; Stumble("KgOfItem", e); }
            _kgOf[id] = kg;
            return kg;
        }
    }
}
