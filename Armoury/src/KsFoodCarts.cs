using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// KS-R3 G1 (17.10, "kupcy i krainy") - KS-H "KUPCY ZYWNOSCI": miasto, ktoremu brakuje jedzenia, kupuje je w miescie tego samego rynku, ktore ma nadwyzke, i wiezie
    /// wozem (doby drogi wozu wsi; na wyspy ladem i morzem przez porty) - jak hurt kupcow TradeCarts (215), ale dla jedzenia. Czynne tylko przy zastosowanej kalibracji
    /// startu (od doby A = D-14 - rurociag napelnia sie w rampie) i kluczu Ks Food Carts; wozy w drodze dojezdzaja zawsze.
    ///  - TOWARY: zboze, ryby, mieso i mieso wieloryba, ser, maslo, jaja, jablka, marchew, pomarancze, winogrona, oliwki, daktyle, miod, sol, przyprawy. Chleba, placka,
    ///    piwa, wina, oliwy i miodu pitnego nie wozi (robi je ZC5 na miejscu), ani zywca, koni i broni. Psujace sie najwyzej 7 dob drogi, reszta 30; bez psucia w drodze.
    ///  - DOBY: droga ladowa (MapDistanceModel) / tempo wozu wsi (MarketCarts.PerDay); para morzem, gdy drogi ladowej nie ma albo jest dluzsza niz 1.15 x droga ladem
    ///    i morzem. Tylko pary tego samego rynku; tabela liczona w stanie Plan najwyzej 200 wywolan na dobe (w zapisie arm_ks z ukladem miast); do czasu - prosta x 1.25.
    ///  - DECYZJA (cel co 2. doba wedlug Fnv): zamowienie Dz = max(popyt zaplacony miasta przy lambda 1, odbior 7 dob) x kx (kx: doba z pusta polka x1.10, do 2.5,
    ///    inaczej x0.98, od 1); polka celu T* = 7 x cena startu^(-1/0.6) dni; zrodlo sprzedaje najwyzej polowe nadwyzki ponad 1.25 x T* x Dz + 5 szt. na dobe (bez
    ///    oblezonych i wrogich celowi); kandydaci - 12 najblizszych miast rynku; koszt na miejscu c = indeks zrodla x (1 + tau x doby), tau 0.010 ladem / 0.005 morzem;
    ///    zrodla od najtanszego, dopoki indeks celu >= 1.03 x c, najwyzej 3; ilosc q = min(oferta, (T* + doby + 2) x Dz - polka z droga), najmniej max(5, Dz/2).
    ///  - ZLOTO (kazda moneta ma platnika i odbiorce): kasa celu -> kasa zrodla (towar, przy wyjezdzie, sztuka po sztuce - cena pierwszej sztuki dla 1/8 lotu, przy 1/8
    ///    do polowy, przy polowie reszta; 1 lot na 64 liczony sztuka po sztuce - blad w linii), fracht = wartosc x tau x doby z kasy celu: woz ladem - kiesy wsi
    ///    miasta zrodla (wedlug ognisk, najwyzej 8 wsi, reszta najwiekszej; raz na zrodlo i dobe), morzem - kiesa wyrobnikow miasta zrodla (zaloga); najwyzej 15%
    ///    kasy celu ponad zapas kupcow na dobe (reszta czeka - licznik). Powrot 30 dob po terminie albo przy utracie celu: towar na polke zrodla, zwrot zaplaty z kasy
    ///    zrodla (najwyzej kasa), frachtu nie. Cel oblezony - woz czeka.
    ///  - KSIEGI: towar w drodze - posiadacz "kupcy zywnosci w drodze" (GoodsLedger), ramki bez ksiegowania (przewoz); zloto - pozycja MKsH "Przeplywy osad".
    ///  - KONTROLA DOBY: w drodze wczoraj + kupione - dowiezione - zawrocone - odrzucone = w drodze dzis (reszta 0); zloto: - cel + zrodla + furmani + zalogi + zwroty = 0.
    /// </summary>
    internal static class KsFoodCarts
    {
        // pozycje koszyka ZC4 (KsBasket) przewozone przez KS-H
        internal static readonly int[] Z =
        {
            KsBasket.Grain, KsBasket.Fish, KsBasket.Meat, KsBasket.Whale, KsBasket.Cheese, KsBasket.Butter, KsBasket.Egg, KsBasket.Apple, KsBasket.Carrot, KsBasket.Orange,
            KsBasket.Grape, KsBasket.Olives, KsBasket.Date, KsBasket.Honey, KsBasket.Salt, KsBasket.Spice
        };
        internal const int NZ = 16;
        private static readonly bool[] Per = { false, true, true, true, false, true, true, true, true, true, true, false, true, false, false, false };
        // towar Planu (KsTab) pozycji - do korytarzy (przyprawy - -1)
        private static readonly int[] PlanG = { KsTab.GGrain, KsTab.GFish, KsTab.GMeat, KsTab.GMeat, KsTab.GCheese, KsTab.GButter, KsTab.GEgg, KsTab.GApple, KsTab.GCarrot, KsTab.GOrange,
                                                KsTab.GGrape, KsTab.GOlives, KsTab.GDate, KsTab.GHoney, KsTab.GSalt, -1 };
        private const double TauLand = 0.010, TauSea = 0.005, Margin = 0.03, SrcKeep = 1.25, OfferShare = 0.5, KxUp = 1.10, KxDown = 0.98, KxMax = 2.5, CofferShare = 0.15;
        private const int MaxSrc = 3, NCand = 12, Keep = 5, ReturnDays = 30, CallsPerDay = 200;
        private const double MaxDur = 30.0, MaxPer = 7.0;

        internal static bool SwitchOn { get { var s = Settings.Current; return s == null || s.KsFoodCarts; } }

        // ------------------------------------------------------------ miasta (Town.AllTowns wedlug StringId) i tabela dob
        private static Town[] _t; private static Settlement[] _st; private static int _nt; private static uint _tHash;
        private static Dictionary<Settlement, int> _ix;
        private static int[] _mk;
        private static float[] _days;                 // [nt * nt] -2 nie liczone, -1 brak drogi albo inny rynek
        private static bool[] _sea;
        private static int[] _pairs;                  // pary tego samego rynku (a * nt + b, a < b) w stalej kolejnosci
        private static int _cursor, _pairsDone;
        private static int[][] _cand;                 // 12 najblizszych miast rynku wedlug dob
        private static bool _candDirty = true;
        private static string _pendingDays;
        private static float[] _kx;                   // [nt * NZ] sterowanie brakiem
        private static string _pendingKx;
        private static Dictionary<int, List<KeyValuePair<Village, float>>> _vilOf;   // zrodlo -> wsie (ogniska), najwyzej 8 + reszta najwiekszej
        private static int _vilDay = -1;

        private sealed class Cart { internal Settlement Src, Dst; internal int Z, N, DayBought, DayDue; internal long Paid, Freight; internal bool Sea; }
        private static readonly List<Cart> _carts = new List<Cart>();
        private static int[] _inc;                    // [nt * NZ] w drodze do celu
        private static string _pending;
        private static long _prevTransit = -1;
        // srednie (korytarze: dowiezione 28 dob wedlug krain; rurociag rampy: wyslane 7 dob i doby wedlug rynku i pozycji)
        private static readonly Dictionary<string, double> _delEma = new Dictionary<string, double>(StringComparer.Ordinal);
        private static readonly Dictionary<string, double> _delDay = new Dictionary<string, double>(StringComparer.Ordinal);
        private static int _delN;
        private static readonly double[] _shipEma = new double[2 * NZ], _daysEma = new double[2 * NZ], _shipDay = new double[2 * NZ], _shipDaysDay = new double[2 * NZ];
        private static int _shipN;

        // liczniki doby (linia)
        private static int _dLots, _dLand, _dSea, _dArrCarts, _dBack, _dWaitSiege, _dStum, _dSample;
        private static long _dBought, _dGold, _dFreight, _dFrV, _dFrP, _dArr, _dBackPcs, _dRefund, _dDrop, _dPoor, _dOrdered, _dLost;
        private static long _gDst, _gSrc, _gCarters, _gCrews, _gRefSrc, _gRefDst;
        private static readonly long[] _dByZ = new long[NZ];
        private static double _dSampleErr, _dSampleMax; private static long _dTicks;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static int _ranDay = -1;

        internal static void Reset()
        {
            _t = null; _st = null; _nt = 0; _tHash = 0; _ix = null; _mk = null; _days = null; _sea = null; _pairs = null; _cursor = _pairsDone = 0; _cand = null; _candDirty = true;
            _pendingDays = null; _kx = null; _pendingKx = null; _vilOf = null; _vilDay = -1;
            _carts.Clear(); _inc = null; _pending = null; _prevTransit = -1;
            _delEma.Clear(); _delDay.Clear(); _delN = 0; Array.Clear(_shipEma, 0, _shipEma.Length); Array.Clear(_daysEma, 0, _daysEma.Length); _shipN = 0;
            Array.Clear(_shipDay, 0, _shipDay.Length); Array.Clear(_shipDaysDay, 0, _shipDaysDay.Length);
            ClearDay(); _err.Clear(); _ranDay = -1;
        }

        private static void ClearDay()
        {
            _dLots = _dLand = _dSea = _dArrCarts = _dBack = _dWaitSiege = _dStum = _dSample = 0;
            _dBought = _dGold = _dFreight = _dFrV = _dFrP = _dArr = _dBackPcs = _dRefund = _dDrop = _dPoor = _dOrdered = _dLost = 0;
            _gDst = _gSrc = _gCarters = _gCrews = _gRefSrc = _gRefDst = 0;
            Array.Clear(_dByZ, 0, NZ); _dSampleErr = _dSampleMax = 0; _dTicks = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _dStum++;
            try { if (_err.Add(where)) Log.Error("KsFoodCarts." + where, e); } catch { }
        }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }
        private static uint Fnv(string s) { uint h = 2166136261; if (s != null) unchecked { foreach (char c in s) h = (h ^ c) * 16777619; } return h; }

        /// <summary>Miasta wedlug StringId (raz na sesje; zmiana listy - tabela dob od nowa).</summary>
        private static bool EnsureTowns()
        {
            if (_t != null) return _nt > 0;
            var l = new List<Town>();
            foreach (var t in Town.AllTowns) if (t != null && t.IsTown && t.Settlement != null && t.Settlement.StringId != null) l.Add(t);
            if (l.Count == 0) return false;                       // swiat jeszcze bez miast (wczytywanie) - sprobuje przy nastepnym wywolaniu
            l.Sort((a, b) => string.CompareOrdinal(a.Settlement.StringId, b.Settlement.StringId));
            _nt = l.Count; _t = l.ToArray(); _st = new Settlement[_nt]; _ix = new Dictionary<Settlement, int>(); _mk = new int[_nt];
            uint h = 2166136261;
            for (int i = 0; i < _nt; i++)
            {
                _st[i] = _t[i].Settlement; _ix[_st[i]] = i; _mk[i] = StartCalibration.TownMarketOf(_t[i]);
                unchecked { foreach (char c in _st[i].StringId) h = (h ^ c) * 16777619; h = (h ^ (uint)_mk[i]) * 16777619; }
            }
            _tHash = h;
            _days = new float[_nt * _nt]; _sea = new bool[_nt * _nt];
            var pairs = new List<int>();
            for (int a = 0; a < _nt; a++)
                for (int b = 0; b < _nt; b++)
                {
                    int k = a * _nt + b;
                    _days[k] = a == b || _mk[a] != _mk[b] ? -1f : -2f;
                    if (a < b && _mk[a] == _mk[b]) pairs.Add(k);
                }
            _pairs = pairs.ToArray(); _cursor = 0; _pairsDone = 0;
            _inc = new int[_nt * NZ]; _kx = new float[_nt * NZ]; for (int i = 0; i < _kx.Length; i++) _kx[i] = 1f;
            foreach (var c in _carts) AddInc(c, +1);
            if (_pendingDays != null) { string p = _pendingDays; _pendingDays = null; ImportDaysNow(p); }
            if (_pendingKx != null) { string p = _pendingKx; _pendingKx = null; ImportKxNow(p); }
            _candDirty = true;
            return _nt > 0;
        }

        internal static int TownCount { get { EnsureTowns(); return _nt; } }
        internal static int IndexOf(Settlement s) { int i; return s != null && EnsureTowns() && _ix.TryGetValue(s, out i) ? i : -1; }
        internal static bool DaysComplete { get { return EnsureTowns() && _pairsDone >= _pairs.Length; } }
        internal static int DaysPairs { get { EnsureTowns(); return _pairs != null ? _pairs.Length : 0; } }
        internal static int DaysPairsDone { get { EnsureTowns(); return _pairsDone; } }

        /// <summary>Doby wozem miedzy miastami (indeksy KS-H): tabela, do czasu - prosta x 1.25 / tempo wozu; -1 - inny rynek albo brak drogi.</summary>
        internal static float Days(int a, int b)
        {
            if (!EnsureTowns() || a < 0 || b < 0 || a >= _nt || b >= _nt || a == b) return -1f;
            float d = _days[a * _nt + b];
            if (d >= -1.5f) return d;
            // nie liczone jeszcze: prosta x 1.25 / tempo wozu
            try
            {
                double per = Math.Max(1.0, MarketCarts.PerDay(Settings.Current));
                float st = _st[a].GetPosition2D.Distance(_st[b].GetPosition2D);
                return (float)Math.Max(1.0, Math.Ceiling(1.25 * st / per));
            }
            catch { return -1f; }
        }

        internal static bool Sea(int a, int b) { return EnsureTowns() && a >= 0 && b >= 0 && a < _nt && b < _nt && _sea[a * _nt + b]; }

        /// <summary>Stan Plan i zastosowany (StartCalibration.Daily): do 200 wywolan MapDistanceModel na dobe - nastepne pary tabeli dob.</summary>
        internal static void DaysTick()
        {
            try
            {
                if (!EnsureTowns() || _pairsDone >= _pairs.Length) return;
                var dm = Campaign.Current != null ? Campaign.Current.Models.MapDistanceModel : null;
                if (dm == null) return;
                double per = Math.Max(1.0, MarketCarts.PerDay(Settings.Current));
                int calls = 0, scan = 0;
                while (calls < CallsPerDay && _pairsDone < _pairs.Length && scan++ < _pairs.Length)
                {
                    int k = _pairs[_cursor % _pairs.Length]; _cursor++;
                    int a = k / _nt, b = k % _nt;
                    if (_days[k] >= -1.5f) { continue; }
                    float dl = -1f, da = -1f;
                    try { dl = dm.GetDistance(_st[a], _st[b], false, false, MobileParty.NavigationType.Default); } catch { dl = -1f; }
                    calls++;
                    bool landOk = dl >= 0f && dl < CartTownExit.BkLimit;
                    try { da = dm.GetDistance(_st[a], _st[b], _st[a].HasPort, _st[b].HasPort, MobileParty.NavigationType.All); } catch { da = -1f; }
                    calls++;
                    bool allOk = da >= 0f && da < CartTownExit.BkLimit;
                    bool sea = !landOk || (allOk && dl > 1.15f * da);
                    float road = sea ? (allOk ? da : -1f) : dl;
                    float days = road >= 0f ? (float)Math.Max(1.0, Math.Ceiling(road / per)) : -1f;
                    _days[k] = days; _days[b * _nt + a] = days; _sea[k] = sea && days >= 0f; _sea[b * _nt + a] = _sea[k];
                    _pairsDone++;
                    _candDirty = true;
                }
            }
            catch (Exception e) { Stumble("DaysTick", e); }
        }

        // tabela dob w zapisie arm_ks (KS-R3 ks4): hash ukladu miast | liczba par | doby*2+morze w 2 znakach base36 dla par po kolei ("--" - nie liczone)
        private const string B36 = "0123456789abcdefghijklmnopqrstuvwxyz";
        internal static string ExportDays()
        {
            if (_t == null) return _pendingDays ?? "";
            var sb = new StringBuilder(_pairs.Length * 2 + 32);
            sb.Append(_tHash.ToString("X8")).Append(':').Append(_pairs.Length).Append(':');
            foreach (int k in _pairs)
            {
                float d = _days[k];
                if (d < -1.5f) { sb.Append("--"); continue; }
                int v = d < 0f ? 1295 : Math.Min(1294, (int)d * 2 + (_sea[k] ? 1 : 0));
                sb.Append(B36[v / 36]).Append(B36[v % 36]);
            }
            return sb.ToString();
        }

        internal static void ImportDays(string s) { _pendingDays = string.IsNullOrEmpty(s) ? null : s; if (_t != null && _pendingDays != null) { string p = _pendingDays; _pendingDays = null; ImportDaysNow(p); } }

        private static void ImportDaysNow(string s)
        {
            try
            {
                var p = s.Split(':');
                uint h; int n;
                if (p.Length < 3 || !uint.TryParse(p[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out h) || h != _tHash || !int.TryParse(p[1], out n) || n != _pairs.Length || p[2].Length != 2 * n) return;
                int done = 0;
                for (int i = 0; i < n; i++)
                {
                    string c = p[2].Substring(2 * i, 2);
                    if (c == "--") continue;
                    int v = B36.IndexOf(c[0]) * 36 + B36.IndexOf(c[1]);
                    if (v < 0) continue;
                    int k = _pairs[i], a = k / _nt, b = k % _nt;
                    float d = v >= 1295 ? -1f : v / 2; bool sea = v < 1295 && (v & 1) != 0;
                    _days[k] = d; _days[b * _nt + a] = d; _sea[k] = sea; _sea[b * _nt + a] = sea;
                    done++;
                }
                _pairsDone = done; _candDirty = true;
            }
            catch (Exception e) { Stumble("ImportDays", e); }
        }

        // sterowanie brakiem kx w zapisie: hash ukladu miast:znak na pozycje ((kx - 1) x 20, 0..30)
        internal static string ExportKx()
        {
            if (_t == null || _kx == null) return _pendingKx ?? "";
            var sb = new StringBuilder(_kx.Length + 16);
            sb.Append(_tHash.ToString("X8")).Append(':');
            foreach (var v in _kx) sb.Append(B36[Math.Max(0, Math.Min(30, (int)Math.Round((v - 1f) * 20f)))]);
            return sb.ToString();
        }

        internal static void ImportKx(string s) { _pendingKx = string.IsNullOrEmpty(s) ? null : s; if (_t != null && _pendingKx != null) { string p = _pendingKx; _pendingKx = null; ImportKxNow(p); } }

        private static void ImportKxNow(string s)
        {
            try
            {
                var p = s.Split(':'); uint h;
                if (p.Length < 2 || !uint.TryParse(p[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out h) || h != _tHash || p[1].Length != _kx.Length) return;
                for (int i = 0; i < _kx.Length; i++) { int v = B36.IndexOf(p[1][i]); if (v >= 0) _kx[i] = 1f + v / 20f; }
            }
            catch (Exception e) { Stumble("ImportKx", e); }
        }

        /// <summary>12 najblizszych miast rynku (wedlug dob) dla kazdego miasta.</summary>
        private static void EnsureCand()
        {
            if (!_candDirty && _cand != null) return;
            _candDirty = false;
            _cand = new int[_nt][];
            var l = new List<KeyValuePair<float, int>>(_nt);
            for (int t = 0; t < _nt; t++)
            {
                l.Clear();
                for (int s = 0; s < _nt; s++) { if (s == t || _mk[s] != _mk[t]) continue; float d = Days(s, t); if (d >= 0f) l.Add(new KeyValuePair<float, int>(d, s)); }
                l.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : a.Value.CompareTo(b.Value));
                var c = new int[Math.Min(NCand, l.Count)];
                for (int i = 0; i < c.Length; i++) c[i] = l[i].Value;
                _cand[t] = c;
            }
        }

        // ------------------------------------------------------------ w drodze
        private static void AddInc(Cart c, int sign)
        {
            if (_inc == null || c == null || c.Dst == null) return;
            int t; if (!_ix.TryGetValue(c.Dst, out t)) return;
            int k = t * NZ + c.Z;
            _inc[k] = Math.Max(0, _inc[k] + sign * c.N);
        }

        /// <summary>Sztuki pozycji koszyka z w drodze do miasta (ZC4 / rampa / linia miast).</summary>
        internal static int Incoming(Settlement dst, int z)
        {
            int t = IndexOf(dst); if (t < 0 || _inc == null) return 0;
            int k = Array.IndexOf(Z, z); if (k < 0) return 0;
            return _inc[t * NZ + k];
        }

        internal static long InTransitPieces() { long n = 0; foreach (var c in _carts) n += c.N; return n; }

        /// <summary>GoodsLedger (posiadacz "kupcy zywnosci w drodze") i rampa: kazda sztuka w drodze (przedmiot, ile, rynek celu).</summary>
        internal static void ForEachInTransit(Action<ItemObject, int, int> f)
        {
            foreach (var c in _carts)
            {
                if (c.N <= 0) continue;
                var it = StartCalibration.ZItem(Z[c.Z]); if (it == null) continue;
                int t = IndexOf(c.Dst);
                f(it, c.N, t >= 0 ? _mk[t] : -1);
            }
        }

        /// <summary>Sterowanie brakiem kx miasta (indeks KS-H) i pozycji KS-H k.</summary>
        internal static double Kx(int t, int k) { return _kx != null && t >= 0 && t * NZ + k < _kx.Length ? _kx[t * NZ + k] : 1.0; }

        /// <summary>Rurociag rampy (P5): spodziewany towar w drodze rynku m i pozycji koszyka z = wyslane (srednia 7 dob) x srednie doby.</summary>
        internal static double Pipeline(int m, int z)
        {
            int k = Array.IndexOf(Z, z); if (k < 0 || m < 0 || m > 1) return 0;
            return _shipEma[m * NZ + k] * _daysEma[m * NZ + k];
        }

        /// <summary>Plan (CorrDelivered): dowiezione wozami KS-H wedlug krain (srednia 28 dob, szt./d): "zrodlo|cel|towar KsTab".</summary>
        internal static Dictionary<string, double> DeliveredSnapshot() { return new Dictionary<string, double>(_delEma, StringComparer.Ordinal); }

        // ------------------------------------------------------------ doba (SupplyDemand.DailyTrade, po TradeCarts.Arrivals; okno MoneyLedger MTrade / MKsH)
        /// <summary>Dostawy, czekanie pod oblezeniem, powroty (30 dob po terminie albo utrata celu). Dziala takze przy wylaczonym kluczu (oplacone wozy dojezdzaja).</summary>
        internal static void Arrivals()
        {
            if (_pending != null) ResolvePending("doba");
            int today = Today();
            if (_ranDay != today) { _ranDay = today; }
            if (_carts.Count == 0) return;
            EnsureTowns();
            var done = new List<Cart>();
            foreach (var c in _carts)
            {
                try
                {
                    if (today < c.DayDue) continue;
                    bool srcOk = c.Src != null && c.Src.ItemRoster != null;
                    bool dstOk = c.Dst != null && c.Dst.IsTown && c.Dst.ItemRoster != null;
                    if (dstOk && c.Src != null)
                    {
                        try { if (c.Src.MapFaction != null && c.Dst.MapFaction != null && FactionManager.IsAtWarAgainstFaction(c.Src.MapFaction, c.Dst.MapFaction)) dstOk = false; } catch { }
                    }
                    if (!dstOk && !srcOk) { _dLost += c.N; AddInc(c, -1); c.N = 0; done.Add(c); continue; }
                    if (!dstOk || (today > c.DayDue + ReturnDays && srcOk)) { Back(c); done.Add(c); continue; }
                    if (c.Dst.IsUnderSiege) { _dWaitSiege++; continue; }
                    var it = StartCalibration.ZItem(Z[c.Z]);
                    if (it == null) { Back(c); done.Add(c); continue; }
                    var gf = GoodsLedger.Begin(GoodsLedger.FKsH, c.Dst);
                    try { c.Dst.ItemRoster.AddToCounts(it, c.N); }
                    finally { GoodsLedger.End(gf); }
                    StartCalibration.ZRowDirty(c.Dst, it);              // wiersz polki z mapy ZC4 nieaktualny (H2) - zakupy wsi szukaja go same
                    AddInc(c, -1);
                    _dArr += c.N; _dArrCarts++;
                    int g = PlanG[c.Z];
                    if (g >= 0)
                    {
                        string key = StartCalibration.TownLandOf(c.Src) + "|" + StartCalibration.TownLandOf(c.Dst) + "|" + g.ToString(CultureInfo.InvariantCulture);
                        double v; _delDay.TryGetValue(key, out v); _delDay[key] = v + c.N;
                    }
                    c.N = 0;
                    done.Add(c);
                }
                catch (Exception e) { Stumble("Arrivals", e); }
            }
            foreach (var c in done) _carts.Remove(c);
        }

        /// <summary>Powrot wozu: towar na polke zrodla, zwrot zaplaty z kasy zrodla do kasy celu (najwyzej kasa zrodla), frachtu nie.</summary>
        private static void Back(Cart c)
        {
            AddInc(c, -1);
            var it = StartCalibration.ZItem(Z[c.Z]);
            if (it != null && c.Src != null && c.Src.ItemRoster != null && c.N > 0)
            {
                var gf = GoodsLedger.Begin(GoodsLedger.FKsH, c.Src);
                try { c.Src.ItemRoster.AddToCounts(it, c.N); }
                finally { GoodsLedger.End(gf); }
                _dBackPcs += c.N;
            }
            else _dLost += c.N;
            _dBack++;
            c.N = 0;
            if (c.Src == null || c.Src.Town == null || c.Dst == null || c.Dst.Town == null) return;
            long r = Math.Min(c.Paid, Math.Max(0, c.Src.Town.Gold));
            if (r <= 0) return;
            int h0 = c.Src.Town.Gold;
            c.Src.Town.ChangeGold(-(int)r);
            long got = h0 - c.Src.Town.Gold;
            if (got <= 0) return;
            int d0 = c.Dst.Town.Gold;
            c.Dst.Town.ChangeGold((int)got);
            _gRefSrc += got; _gRefDst += c.Dst.Town.Gold - d0; _dRefund += got;
        }

        /// <summary>Decyzje doby: zakupy jedzenia dla miast z brakiem (cel co 2. doba). Tylko stan zastosowany od doby A, klucz Ks Food Carts.</summary>
        internal static void Daily()
        {
            if (!SwitchOn || !StartCalibration.Applied || StartCalibration.RampDayA < 0 || Today() < StartCalibration.RampDayA) return;
            long t0 = Stopwatch.GetTimestamp();
            var pendFreight = new Dictionary<int, long>();
            try
            {
                if (!EnsureTowns()) return;
                EnsureCand();
                int today = Today(), nt = _nt;
                var items = new ItemObject[NZ]; var cats = new ItemCategory[NZ];
                var ixOf = new Dictionary<ItemObject, int>();
                for (int k = 0; k < NZ; k++) { items[k] = StartCalibration.ZItem(Z[k]); cats[k] = items[k] != null ? items[k].ItemCategory : null; if (items[k] != null && !ixOf.ContainsKey(items[k])) ixOf[items[k]] = k; }
                var sh = new int[nt * NZ]; var ix = new float[nt * NZ]; var dz = new double[nt * NZ]; var dp = new double[nt * NZ];
                var budget = new double[nt]; var siege = new bool[nt];
                for (int i = 0; i < nt; i++)
                {
                    var t = _t[i]; var st = _st[i];
                    siege[i] = st.IsUnderSiege;
                    var ro = st.ItemRoster;
                    if (ro != null)
                        for (int r = 0; r < ro.Count; r++)
                        {
                            var el = ro.GetElementCopyAtIndex(r); int k;
                            if (el.Amount > 0 && el.EquipmentElement.ItemModifier == null && el.EquipmentElement.Item != null && ixOf.TryGetValue(el.EquipmentElement.Item, out k)) sh[i * NZ + k] += el.Amount;
                        }
                    for (int k = 0; k < NZ; k++)
                    {
                        float x = 1f;
                        if (cats[k] != null) { try { x = t.GetItemCategoryPriceIndex(cats[k]); } catch { x = 1f; } }
                        ix[i * NZ + k] = x > 0f && !float.IsInfinity(x) ? x : 1f;
                        double p = StartCalibration.ZDp(t, Z[k]);
                        dp[i * NZ + k] = p;
                        double off = StartCalibration.ZOff7(st, Z[k]);
                        double kxv = _kx[i * NZ + k];
                        // sterowanie brakiem (codziennie): pusta polka przy popycie - x1.10 (do 2.5), inaczej x0.98 (od 1)
                        if (p > 0 && sh[i * NZ + k] <= Keep + 1) kxv = Math.Min(KxMax, kxv * KxUp); else kxv = Math.Max(1.0, kxv * KxDown);
                        _kx[i * NZ + k] = (float)kxv;
                        dz[i * NZ + k] = Math.Max(p, off) * kxv;
                    }
                    long res = 0; try { res = Seal31.TownReserve(t); } catch { res = 0; }
                    budget[i] = CofferShare * Math.Max(0, t.Gold - res);
                }
                var offer = new double[nt * NZ];
                var tstar = new double[2 * NZ];
                for (int m = 0; m < 2; m++) for (int k = 0; k < NZ; k++) tstar[m * NZ + k] = StartCalibration.ZTstar(m, Z[k]);
                for (int i = 0; i < nt; i++)
                    for (int k = 0; k < NZ; k++)
                    {
                        if (siege[i]) continue;
                        double sur = sh[i * NZ + k] - SrcKeep * tstar[_mk[i] * NZ + k] * dz[i * NZ + k] - Keep;
                        if (sur > 1) offer[i * NZ + k] = Math.Floor(OfferShare * sur);
                    }
                var dst = new List<int>(nt);
                var cand = new List<KeyValuePair<double, int>>(NCand);
                for (int k = 0; k < NZ; k++)
                {
                    if (items[k] == null) continue;
                    double lim = Per[k] ? MaxPer : MaxDur;
                    dst.Clear();
                    for (int i = 0; i < nt; i++)
                    {
                        if (siege[i] || (Fnv(_st[i].StringId) & 1u) != (uint)(today & 1)) continue;
                        double d = dz[i * NZ + k]; if (!(d > 0)) continue;
                        double H = sh[i * NZ + k] + _inc[i * NZ + k];
                        if (H >= (tstar[_mk[i] * NZ + k] + lim + 2) * d) continue;
                        dst.Add(i);
                    }
                    dst.Sort((a, b) => ix[b * NZ + k].CompareTo(ix[a * NZ + k]));
                    foreach (int t in dst)
                    {
                        double d = dz[t * NZ + k], T = tstar[_mk[t] * NZ + k];
                        double Hc = sh[t * NZ + k] + _inc[t * NZ + k];
                        cand.Clear();
                        var ct = _cand[t];
                        for (int ci = 0; ci < ct.Length; ci++)
                        {
                            int s = ct[ci];
                            if (offer[s * NZ + k] < 1 || siege[s]) continue;
                            float L = Days(s, t); if (!(L >= 0f) || L > lim) continue;
                            bool war = false;
                            try { war = _st[s].MapFaction != null && _st[t].MapFaction != null && FactionManager.IsAtWarAgainstFaction(_st[s].MapFaction, _st[t].MapFaction); } catch { war = false; }
                            if (war) continue;
                            double tau = _sea[s * nt + t] ? TauSea : TauLand;
                            double c = ix[s * NZ + k] * (1.0 + tau * L);
                            if (ix[t * NZ + k] < c * (1.0 + Margin)) continue;
                            cand.Add(new KeyValuePair<double, int>(c, s));
                        }
                        if (cand.Count == 0) continue;
                        cand.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : a.Value.CompareTo(b.Value));
                        int used = 0;
                        // KS-R3 (port symulatora 17.10, runda 1): licznik NKS-22 raz na cel x towar x dobe - zamowione = pierwsza ilosc z kandydata, kasa celu za mala = czego
                        // z niej nie kupiono, gdy kasa zatrzymala choc jeden lot (dotad kazdy kandydat dopisywal ten sam brak jeszcze raz: 58-66% zamowionych w symulatorze)
                        long ord0 = -1, got0 = 0; bool poorHit = false;
                        foreach (var kv in cand)
                        {
                            if (used >= MaxSrc) break;
                            int s = kv.Value; float L = Days(s, t);
                            double want = (T + L + 2) * d - Hc;
                            double q = Math.Min(offer[s * NZ + k], want);
                            if (q < Math.Max(5.0, 0.5 * d)) continue;
                            int n = (int)Math.Floor(q);
                            if (ord0 < 0) ord0 = n;
                            bool sea = _sea[s * nt + t];
                            double tau = sea ? TauSea : TauLand;
                            long cost = LotPrice(_t[s], items[k], n, false);
                            long fr = (long)Math.Round(cost * tau * L);
                            if (cost + fr > budget[t])
                            {
                                int nf = cost + fr > 0 ? (int)Math.Floor(n * budget[t] / (double)(cost + fr)) : 0;
                                poorHit = true;
                                if (nf < Math.Max(5.0, 0.5 * d)) continue;
                                n = nf;
                                cost = LotPrice(_t[s], items[k], n, false); fr = (long)Math.Round(cost * tau * L);
                                if (cost + fr > budget[t] + 1) continue;
                            }
                            if (n <= 0 || cost <= 0) continue;
                            if ((Fnv(_st[s].StringId + _st[t].StringId) + (uint)today + (uint)k) % 64u == 0u) Sample(_t[s], items[k], n, cost);
                            int got = Ship(s, t, k, items[k], n, cost, fr, sea, L, today, pendFreight);
                            if (got <= 0) continue;
                            got0 += got;
                            used++;
                            offer[s * NZ + k] -= got; sh[s * NZ + k] -= got; Hc += got; budget[t] -= cost + fr;
                        }
                        if (ord0 > 0) { _dOrdered += ord0; if (poorHit) _dPoor += Math.Max(0, ord0 - got0); }
                    }
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally
            {
                try { PayCarters(pendFreight); } catch (Exception e) { Stumble("PayCarters", e); }
                _dTicks += Stopwatch.GetTimestamp() - t0;
            }
        }

        /// <summary>Cena lotu n sztuk z polki zrodla (kupno): sztuka po sztuce przy polce zmniejszanej o kazda sztuke - trzy ceny (pierwsza dla 1/8, przy 1/8 do polowy,
        /// przy polowie reszta), exact - kazda sztuka (probka 1/64).</summary>
        private static long LotPrice(Town src, ItemObject it, int n, bool exact)
        {
            if (n <= 0 || src == null || it == null) return 0;
            var model = Campaign.Current.Models.TradeItemPriceFactorModel;
            var md = src.MarketData;
            var cat = it.ItemCategory;
            var d = md.GetCategoryData(cat);
            int wv = Math.Max(1, HistoricalPrices.ShelfWorth(it));
            var el = new EquipmentElement(it, null, null, false);
            Func<int, long> P = k => Math.Max(1, model.GetPrice(el, null, null, false, Math.Max(0, d.InStoreValue - k * wv), d.Supply, d.Demand));
            if (exact || n < 8)
            {
                long t = 0;
                for (int k = 0; k < n; k++) t += P(k);
                return t;
            }
            int m1 = n / 8, m2 = n / 2;
            return m1 * P(0) + (m2 - m1) * P(m1) + (n - m2) * P(m2);
        }

        private static void Sample(Town src, ItemObject it, int n, long approx)
        {
            try
            {
                long ex = LotPrice(src, it, n, true);
                if (ex <= 0) return;
                double e = Math.Abs(approx - ex) / (double)ex;
                _dSample++; _dSampleErr += e; if (e > _dSampleMax) _dSampleMax = e;
            }
            catch (Exception e) { Stumble("Sample", e); }
        }

        /// <summary>Zakup i wyjazd: towar z polki zrodla (ramka bez ksiegowania - towar idzie do posiadacza "w drodze"), kasa celu -> kasa zrodla (towar), fracht z kasy celu
        /// (ladem - do wsi zrodla na koniec doby; morzem - kiesa wyrobnikow zrodla od razu). Zwraca wyslane sztuki.</summary>
        private static int Ship(int s, int t, int k, ItemObject it, int n, long cost, long fr, bool sea, float L, int today, Dictionary<int, long> pendFreight)
        {
            var src = _st[s]; var dstS = _st[t]; var srcT = _t[s]; var dstT = _t[t];
            var ro = src.ItemRoster;
            if (ro == null || dstT.Gold < cost + fr) return 0;
            int before = ro.GetItemNumber(it);
            int take = Math.Min(n, Math.Max(0, before - Keep));
            if (take <= 0) return 0;
            if (take < n) { cost = (long)Math.Round(cost * (double)take / n); fr = (long)Math.Round(fr * (double)take / n); n = take; }
            var gf = GoodsLedger.Begin(GoodsLedger.FKsH, src);
            try { ro.AddToCounts(it, -n); }
            finally { GoodsLedger.End(gf); }
            int removed = before - ro.GetItemNumber(it);
            if (removed != n) { _dDrop += Math.Max(0, n - removed); n = Math.Max(0, removed); if (n <= 0) return 0; }
            // zloto: towar
            int d0 = dstT.Gold; dstT.ChangeGold(-(int)cost); long paidG = d0 - dstT.Gold;
            int s0 = srcT.Gold; srcT.ChangeGold((int)paidG); long gotS = srcT.Gold - s0;
            _gDst += paidG; _gSrc += gotS;
            // fracht
            long frPaid = 0;
            if (fr > 0)
            {
                if (sea)
                {
                    int f0 = dstT.Gold;
                    long crew = TownFolk.KsCrewPay(dstT, src, fr);
                    if (crew > 0) { frPaid = f0 - dstT.Gold; _gCrews += crew; }
                    else
                    {
                        // kiesa ludu nieczynna - zaloga to kupcy portu zrodla (kasa zrodla)
                        f0 = dstT.Gold; dstT.ChangeGold(-(int)fr); frPaid = f0 - dstT.Gold;
                        int c0 = srcT.Gold; srcT.ChangeGold((int)frPaid); _gCrews += srcT.Gold - c0;
                    }
                    _gDst += frPaid; _dFrP += frPaid;
                }
                else
                {
                    int f0 = dstT.Gold; dstT.ChangeGold(-(int)fr); frPaid = f0 - dstT.Gold;
                    _gDst += frPaid; _dFrV += frPaid;
                    long pv; pendFreight.TryGetValue(s, out pv); pendFreight[s] = pv + frPaid;
                }
            }
            var c = new Cart { Src = src, Dst = dstS, Z = k, N = n, DayBought = today, DayDue = today + (int)Math.Max(1f, L), Paid = paidG, Freight = frPaid, Sea = sea };
            _carts.Add(c);
            AddInc(c, +1);
            _dLots++; if (sea) _dSea++; else _dLand++;
            _dBought += n; _dGold += paidG; _dFreight += frPaid; _dByZ[k] += n;
            int m = _mk[t];
            _shipDay[m * NZ + k] += n; _shipDaysDay[m * NZ + k] += n * Math.Max(1f, L);
            return n;
        }

        /// <summary>Fracht wozow ladem do kies wsi miasta zrodla (wedlug ognisk, najwyzej 8 wsi, reszta najwiekszej) - raz na zrodlo i dobe; bez wsi - kasa zrodla.</summary>
        private static void PayCarters(Dictionary<int, long> pend)
        {
            if (pend.Count == 0) return;
            int today = Today();
            if (_vilOf == null || _vilDay < 0 || today - _vilDay >= 7) BuildVillages(today);
            foreach (var kv in pend)
            {
                long x = kv.Value; if (x <= 0) continue;
                List<KeyValuePair<Village, float>> vl;
                if (_vilOf == null || !_vilOf.TryGetValue(kv.Key, out vl) || vl == null || vl.Count == 0)
                {
                    var tt = _t[kv.Key]; int g0 = tt.Gold; tt.ChangeGold((int)x); _gCarters += tt.Gold - g0;
                    continue;
                }
                double tot = 0; foreach (var p in vl) tot += p.Value;
                long left = x;
                for (int i = 1; i < vl.Count; i++)
                {
                    long y = tot > 0 ? (long)Math.Floor(x * vl[i].Value / tot) : 0;
                    if (y <= 0) continue;
                    int g0 = vl[i].Key.Gold; vl[i].Key.ChangeGold((int)y); long got = vl[i].Key.Gold - g0;
                    _gCarters += got; left -= got;
                    StartCalibration.KsVilFreight(vl[i].Key, got);
                }
                if (left > 0)
                {
                    var v0 = vl[0].Key; int g0 = v0.Gold; v0.ChangeGold((int)left); long got = v0.Gold - g0;
                    _gCarters += got; StartCalibration.KsVilFreight(v0, got);
                }
            }
        }

        /// <summary>Wsie miast zrodel (TradeBound = miasto): najwieksza pierwsza; 8 najwiekszych wedlug ognisk, ich ogniska + reszta do najwiekszej (wagi).</summary>
        private static void BuildVillages(int today)
        {
            _vilDay = today;
            var all = new Dictionary<int, List<KeyValuePair<Village, float>>>();
            foreach (var v in Village.All)
            {
                if (v == null || v.Settlement == null || v.TradeBound == null) continue;
                int t; if (!_ix.TryGetValue(v.TradeBound, out t)) continue;
                List<KeyValuePair<Village, float>> l; if (!all.TryGetValue(t, out l)) { l = new List<KeyValuePair<Village, float>>(); all[t] = l; }
                l.Add(new KeyValuePair<Village, float>(v, Math.Max(0f, v.Hearth)));
            }
            _vilOf = new Dictionary<int, List<KeyValuePair<Village, float>>>();
            foreach (var kv in all)
            {
                var l = kv.Value;
                l.Sort((a, b) => b.Value.CompareTo(a.Value));
                float rest = 0; for (int i = 8; i < l.Count; i++) rest += l[i].Value;
                var top = new List<KeyValuePair<Village, float>>();
                for (int i = 0; i < l.Count && i < 8; i++) top.Add(new KeyValuePair<Village, float>(l[i].Key, l[i].Value + (i == 0 ? rest : 0f)));
                _vilOf[kv.Key] = top;
            }
        }

        /// <summary>Koniec SupplyDemand.DailyTrade: linia doby (od A albo z wozami w drodze), kontrola towaru i zlota, srednie korytarzy i rurociagu.</summary>
        internal static void Flush()
        {
            long now = InTransitPieces();
            try
            {
                // srednie: dowiezione wedlug krain (28 dob), wyslane i doby wedlug rynku i pozycji (7 dob)
                _delN++;
                double w = 1.0 / Math.Min(28, _delN);
                var keys = new List<string>(_delEma.Keys);
                foreach (var k in keys) { double v; _delDay.TryGetValue(k, out v); _delEma[k] += (v - _delEma[k]) * w; if (_delEma[k] < 0.01 && v <= 0) _delEma.Remove(k); }
                foreach (var kv in _delDay) if (!_delEma.ContainsKey(kv.Key)) _delEma[kv.Key] = kv.Value * w;
                _delDay.Clear();
                _shipN++;
                double w7 = 1.0 / Math.Min(7, _shipN);
                for (int i = 0; i < _shipEma.Length; i++)
                {
                    _shipEma[i] += (_shipDay[i] - _shipEma[i]) * w7;
                    if (_shipDay[i] > 0) _daysEma[i] += (_shipDaysDay[i] / _shipDay[i] - _daysEma[i]) * (_daysEma[i] <= 0 ? 1.0 : w7);
                }
                Array.Clear(_shipDay, 0, _shipDay.Length); Array.Clear(_shipDaysDay, 0, _shipDaysDay.Length);
                bool active = StartCalibration.Applied && SwitchOn && StartCalibration.RampDayA >= 0 && Today() >= StartCalibration.RampDayA;
                if (!active && _dBought + _dArr + _dBackPcs + _dLost == 0 && now == 0) { _prevTransit = now; ClearDay(); return; }
                if (!LogGate.Lines) { _prevTransit = now; ClearDay(); return; }
                var inv = CultureInfo.InvariantCulture;
                var sb = new StringBuilder(1500);
                sb.Append("Kupcy zywnosci (KS-H) dzien ").Append(Today() - 1).Append(" | ").Append(active ? "CZYNNE" : (SwitchOn ? "poza stanem zastosowanym (tylko dowoz oplaconych)" : "WYLACZONE kluczem Ks Food Carts (tylko dowoz oplaconych)"))
                  .Append(" | wozow w ").Append(_dLots).Append(" (lad ").Append(_dLand).Append(", morze ").Append(_dSea).Append("), kupione ").Append(_dBought).Append(" szt. za ").Append(_dGold).Append(" zl (");
                bool f = true;
                for (int k = 0; k < NZ; k++) { if (_dByZ[k] <= 0) continue; sb.Append(f ? "" : ", ").Append(KsBasket.Ids[Z[k]]).Append(' ').Append(_dByZ[k]); f = false; }
                if (f) sb.Append('-');
                sb.Append("), fracht ").Append(_dFreight).Append(" zl (wsie ").Append(_dFrV).Append(", porty ").Append(_dFrP).Append("), dowiezione ").Append(_dArr).Append(", zawrocone ").Append(_dBackPcs)
                  .Append(" (zwrot ").Append(_dRefund).Append(" zl), czeka pod oblezeniem ").Append(_dWaitSiege).Append(", kasa celu za mala ").Append(_dPoor).Append(" szt. (zamowione ").Append(_dOrdered).Append(')')
                  .Append(" | w drodze ").Append(now).Append(" szt. w ").Append(_carts.Count).Append(" wozach");
                if (_prevTransit >= 0)
                {
                    long rest = _prevTransit + _dBought - _dArr - _dBackPcs - _dLost - _dDrop - now;
                    long gold = -_gDst + _gSrc + _gCarters + _gCrews + (_gRefDst - _gRefSrc);
                    sb.Append(" | kontrola towaru reszta ").Append(rest).Append(" (bez celu ").Append(_dLost).Append(", odrzucone przy zaladunku ").Append(_dDrop).Append("), zlota reszta ").Append(gold)
                      .Append(" (cel -").Append(_gDst).Append(", zrodla +").Append(_gSrc).Append(", furmani +").Append(_gCarters).Append(", zalogi +").Append(_gCrews).Append(", zwroty ").Append(_gRefDst).Append('/').Append(_gRefSrc).Append(')');
                }
                else sb.Append(" | kontrola od nastepnej doby");
                sb.Append(" | probka wyceny 1/64 ").Append(_dSample > 0 ? (100.0 * _dSampleErr / _dSample).ToString("0.00", inv) + "% (max " + (100.0 * _dSampleMax).ToString("0.00", inv) + "%, lotow " + _dSample + ")" : "-")
                  .Append(" | tabela dob ").Append(_pairsDone).Append('/').Append(_pairs != null ? _pairs.Length : 0)
                  .Append(" | czas ").Append((_dTicks * 1000.0 / Stopwatch.Frequency).ToString("0.00", inv)).Append(" ms | potkniecia ").Append(_dStum).Append('.');
                if (Today() % 7 == 0 && _delEma.Count > 0)
                {
                    var l = new List<KeyValuePair<string, double>>(_delEma);
                    l.Sort((a, b) => b.Value.CompareTo(a.Value));
                    sb.Append(" | korytarze (dowiezione, srednia 28 dob, szt./d):");
                    int n = 0;
                    foreach (var kv in l)
                    {
                        if (kv.Value < 1.0 || n >= 120) break;
                        var p = kv.Key.Split('|'); int g;
                        string gn = p.Length == 3 && int.TryParse(p[2], out g) && g >= 0 && g < KsTab.NG ? KsTab.Goods[g] : "?";
                        sb.Append(n == 0 ? " " : ", ").Append(p.Length > 1 ? p[0] + ">" + p[1] : kv.Key).Append(' ').Append(gn).Append(' ').Append(kv.Value.ToString("0", inv));
                        n++;
                    }
                }
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Log.Error("KsFoodCarts.Flush", e); }
            _prevTransit = now;
            ClearDay();
        }

        // ------------------------------------------------------------ zapis wozow (arm_ks_kupcy): zrodlo|cel|towar|ile|doba zakupu|termin|zaplacone|fracht|morze~
        internal static string Export()
        {
            if (_pending != null) ResolvePending("zapis przed startem sesji");
            var sb = new StringBuilder(_carts.Count * 48);
            foreach (var c in _carts)
            {
                if (c.Src == null || c.Dst == null || c.N <= 0) continue;
                sb.Append(c.Src.StringId).Append('|').Append(c.Dst.StringId).Append('|').Append(KsBasket.Ids[Z[c.Z]]).Append('|').Append(c.N).Append('|').Append(c.DayBought).Append('|')
                  .Append(c.DayDue).Append('|').Append(c.Paid).Append('|').Append(c.Freight).Append('|').Append(c.Sea ? 1 : 0).Append('~');
            }
            return sb.ToString();
        }

        internal static void Import(string s) { _carts.Clear(); _prevTransit = -1; _pending = string.IsNullOrEmpty(s) ? null : s; }

        internal static void ResolvePending(string why)
        {
            var s = _pending; _pending = null;
            if (string.IsNullOrEmpty(s)) return;
            var om = MBObjectManager.Instance;
            int ok = 0, lost = 0; long pcs = 0;
            foreach (var rec in s.Split('~'))
            {
                if (rec.Length == 0) continue;
                try
                {
                    var a = rec.Split('|'); if (a.Length < 9) { lost++; continue; }
                    Settlement src = null, dst = null;
                    try { src = om.GetObject<Settlement>(a[0]); dst = om.GetObject<Settlement>(a[1]); } catch { }
                    int z = Array.IndexOf(KsBasket.Ids, a[2]), k = z >= 0 ? Array.IndexOf(Z, z) : -1;
                    int n, d0, d1; long paid, fr;
                    if (src == null || dst == null || k < 0 || !int.TryParse(a[3], out n) || n <= 0 || !int.TryParse(a[4], out d0) || !int.TryParse(a[5], out d1)
                        || !long.TryParse(a[6], out paid) || !long.TryParse(a[7], out fr)) { lost++; continue; }
                    var c = new Cart { Src = src, Dst = dst, Z = k, N = n, DayBought = d0, DayDue = d1, Paid = Math.Max(0, paid), Freight = Math.Max(0, fr), Sea = a[8] == "1" };
                    _carts.Add(c);
                    if (_t != null) AddInc(c, +1);
                    ok++; pcs += n;
                }
                catch (Exception e) { Stumble("ResolvePending", e); lost++; }
            }
            Log.Info("Kupcy zywnosci (KS-H): z zapisu (" + why + ") " + ok + " wozow w drodze (" + pcs + " szt.; odrzucone bez osady albo towaru " + lost + ").");
        }
    }
}
