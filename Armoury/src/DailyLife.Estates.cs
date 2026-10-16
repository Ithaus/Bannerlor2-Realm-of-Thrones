using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// ZC0 - STANY (projekt docs/PROJEKT-ZYCIE-CODZIENNE-2026-10-16.md rozdz. 5). Stany to podzial ludzi DOM kazdej osady z naszej ksiegi ludzi
    /// (PopulationLaw.PeopleOf: wies hearth x k_v, miasto stan ksiegi; zamek 0 - jego ludzie sa w jego wsiach) - ksiega zostaje jedynym licznikiem:
    ///  - pasma startowe wedlug kultury osady z dnia kalibracji (PeopleBook.CultureAtCalibration) i rodzaju osady (rozdz. 5.2; Bands);
    ///  - pochylenie klasami BK (rozdz. 5.2 "pasma pochylone udzialem BK osady"): udzial rzemieslnikow i kupcow x clamp(udzial wyzszych klas BK osady
    ///    (rzemieslnicy + szlachta BK z kalibracji kiesy ludu) / srednia rodzaju osady wazona ludzmi, 0.7-1.3); reszte bierze stan "ostatni" osady
    ///    (biedota w miescie, chlopi we wsi) - suma udzialow zawsze 1, wiec suma stanow = ksiega co do czlowieka;
    ///  - konto NIEWOLNI ksiegi (PeopleBook "nw:", region trzymania) dopisane do niewolnych warowni regionu (bez regionu - do swiata);
    ///  - wielkie rody z bohaterow gry (rozdz. 5.1, 16): zywi bohaterowie rodow z lennem x 3 (krewni, dwor) - ponad ksiege, w lennach rodu po rowno.
    /// Region = warownia + jej wsie: wies do regionu przez Village.Bound (poprawka New Ibbish z rozdz. 13 - TYLKO w pomiarze: dwie wsie, ktorych lista
    /// BoundVillages warowni nie zna, licza sie do regionu przez Bound; liczba i ludzie takich wsi w linii). Wies bez Bound - region OutlawLaw.RegionFor.
    /// Tylko odczyt gry - nic w grze nie zmienia.
    /// </summary>
    internal static partial class DailyLife
    {
        private const int KTown = 0, KCastle = 1, KVillage = 2;

        private sealed class Rec
        {
            public Settlement St; public int Kind;
            public string Id, Name, Cul; public int Prof; public bool West, KnownCul;
            public Rec Region, Market;                       // region (warownia; miasto i zamek - sam); wies: miasto targowe (TradeBound albo Bound) albo null
            public bool OutsideBound;                        // wies spoza BoundVillages swojej warowni (New Ibbish)
            public List<Rec> Villages;                       // warownia: wsie regionu (przez Bound)
            public readonly double[] Share = new double[6];  // udzialy stanow ksiegi
            public bool Tilted; public double Upper = double.NaN;
            public double L;                                 // ludzie ksiegi doby
            public readonly double[] N = new double[Estates];       // ludzie stanow doby (6 = wielkie rody)
            public double Thralls;                           // konto NIEWOLNI ksiegi trzymane w tym regionie (w N[ESlave])
            public readonly double[] Y = new double[Estates];       // dochod doby, prawdziwe zl/d
            public readonly double[] YAvg = new double[Estates];    // srednia biezaca dochodu (do 28 dob, pamiec sesji)
            public int YDays;
            public double K = double.NaN;                    // koszyk przetrwania po cenach lokalnych, zl/os/d
            public double Fed = 1.0;                         // wies: czesc gospodarzy najedzonych ze spichlerza (1 - brak, Famine)
            public double CastleIn;                          // zamek: dochod podzamcza doby (prawdziwe zl) - do rzemieslnikow wsi regionu
            public double[] Px;                              // miasto: ceny za kg towarow dzis
            public double D = double.NaN; public int DSrc = -1;     // 0 wlasne, 1 regionu, 2 krolestwa, 3 swiata
            public double D0 = double.NaN;                   // D bez wielkich rodow (dochod rodow 1:1 brutto - nie do progow)
            public readonly double[] DE = new double[Estates];
            public bool NoEarn;                              // miasto bez liczb kiesy ludu tej doby: W/R/P bez wpisu do sredniej, D z regionu
            public double Nv, YSum, YK, NvH, YKH;            // doba linii: ludzie z liczbami dochodu, suma Y, suma Y/K (H - bez rodow)
            public string KingdomId, KingdomName;
            public double Clans;                             // wielkie rody: ile rodow ma tu lenno (ulamek przy kilku lennach)
        }

        private static List<Rec> _recs;
        private static readonly Dictionary<Settlement, Rec> _byS = new Dictionary<Settlement, Rec>();
        private static readonly Dictionary<string, Rec> _byHold = new Dictionary<string, Rec>(StringComparer.Ordinal);   // id warowni -> osada (konto NIEWOLNI)
        private static readonly Dictionary<Kingdom, string> _kName = new Dictionary<Kingdom, string>();                   // nazwy krolestw (pamiec do spisu osad)
        private static long _cacheSig = -1; private static int _cacheDay = int.MinValue;
        private static int _outsideN; private static double _outsideL; private static string _outsideNames = "-";
        private static int _unknownCul, _noRegion, _noMarket;

        /// <summary>Sygnal spisu: liczba miast, zamkow i wsi gry (kryjowki i inne osady modow nie licza sie - nie przebudowuja spisu).</summary>
        private static long SettlementSignal()
        {
            try
            {
                long t = Town.AllTowns != null ? Town.AllTowns.Count : 0, c = Town.AllCastles != null ? Town.AllCastles.Count : 0, v = Village.All != null ? Village.All.Count : 0;
                return (t << 40) | (c << 20) | v;
            }
            catch (Exception e) { Stumble("SettlementSignal", e); return -2; }
        }

        /// <summary>Spis osad (miasta, zamki, wsie) - raz na sesje, od nowa co 28 dob albo przy innej liczbie miast, zamkow i wsi. Region, miasto targowe,
        /// kultura z kalibracji. Srednia dochodu: z poprzedniego spisu, a w pierwszym spisie po wczytaniu - z zapisu (arm_dailylife v2).</summary>
        private static void EnsureRecs(int day)
        {
            long n = SettlementSignal();
            if (_recs != null && n == _cacheSig && day - _cacheDay < 28) return;
            var recs = new List<Rec>(900);
            var old = _byS.Count > 0 ? new Dictionary<Settlement, Rec>(_byS) : null;   // srednia biezaca dochodu przechodzi do nowego spisu (bez skoku co 28 dob)
            var saved = old == null ? _saved : null;
            int restored = 0;
            _byS.Clear(); _byHold.Clear(); _kName.Clear();
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null) continue;
                    int kind = st.IsTown ? KTown : st.IsCastle ? KCastle : st.IsVillage ? KVillage : -1;
                    if (kind < 0 || (kind == KVillage && st.Village == null) || (kind != KVillage && st.Town == null)) continue;
                    var r = new Rec { St = st, Kind = kind, Id = st.StringId ?? "?", Name = Clean(st.Name != null ? st.Name.ToString() : st.StringId) };
                    r.Cul = kind == KCastle ? (st.Culture != null ? st.Culture.StringId : "?") : PeopleBook.CultureAtCalibration(st);
                    bool known;
                    r.Prof = ProfOf(r.Cul, out known); r.KnownCul = known;
                    r.West = r.Cul != null && PopulationLaw.WesterosCultures.Contains(r.Cul);
                    Rec o;
                    double[] sv;
                    if (old != null && old.TryGetValue(st, out o) && o != null && o.Kind == kind) { Array.Copy(o.YAvg, r.YAvg, Estates); r.YDays = o.YDays; }
                    else if (saved != null && saved.TryGetValue(r.Id, out sv) && sv != null && sv.Length > Estates)
                    { Array.Copy(sv, r.YAvg, Estates); r.YDays = Math.Max(0, Math.Min(AvgDays, (int)sv[Estates])); restored++; }
                    recs.Add(r); _byS[st] = r;
                    if (kind != KVillage && st.StringId != null) _byHold[st.StringId] = r;
                }
                catch (Exception e) { Stumble("EnsureRecs(osada)", e); }
            }
            int unk = 0, noReg = 0, noMk = 0, outN = 0; var outNames = new List<string>();
            foreach (var r in recs)
            {
                try
                {
                    if (r.Kind != KVillage) { r.Region = r; r.Market = r.Kind == KTown ? r : null; continue; }
                    if (!r.KnownCul) unk++;
                    var v = r.St.Village;
                    Settlement reg = v.Bound;
                    if (reg == null || !(reg.IsTown || reg.IsCastle)) { try { reg = OutlawLaw.RegionFor(r.St); } catch (Exception e) { reg = null; Stumble("RegionFor", e); } }
                    Rec rr;
                    r.Region = reg != null && _byS.TryGetValue(reg, out rr) ? rr : null;
                    if (r.Region == null) noReg++;
                    else
                    {
                        if (r.Region.Villages == null) r.Region.Villages = new List<Rec>();
                        r.Region.Villages.Add(r);
                        bool listed = false;
                        var bv = r.Region.St.BoundVillages;
                        if (bv != null) for (int i = 0; i < bv.Count; i++) if (ReferenceEquals(bv[i], v)) { listed = true; break; }
                        if (!listed) { r.OutsideBound = true; outN++; outNames.Add(r.Name.Replace('(', '[').Replace(')', ']')); }
                    }
                    Settlement mk = v.TradeBound ?? v.Bound;
                    r.Market = mk != null && mk.IsTown && _byS.TryGetValue(mk, out rr) ? rr : null;
                    if (r.Market == null) noMk++;
                }
                catch (Exception e) { Stumble("EnsureRecs(region)", e); }
            }
            foreach (var r in recs) if (r.Kind == KTown && !r.KnownCul) unk++;
            _recs = recs; _cacheSig = n; _cacheDay = day;
            if (saved != null) { _restoredN = restored; _saved = null; }   // srednia z zapisu przeszla do spisu (reszta wpisow - osady, ktorych juz nie ma)
            _unknownCul = unk; _noRegion = noReg; _noMarket = noMk; _outsideN = outN;
            _outsideNames = outNames.Count > 0 ? string.Join(", ", outNames.Count > 6 ? outNames.GetRange(0, 6).ToArray() : outNames.ToArray()) + (outNames.Count > 6 ? ", ..." : "") : "-";
        }

        private static string Clean(string s) { return string.IsNullOrEmpty(s) ? "?" : s.Replace(';', ',').Replace('|', '/').Replace('"', '\'').Replace('\r', ' ').Replace('\n', ' '); }

        // ------------------------------------------------------------ ludzie stanow doby
        private static double _book, _bookVil, _bookTown, _thrBook, _thrNoRegion, _estSum, _houses;
        private static int _houseClans, _houseHeroes, _tiltN, _bkN;
        private static readonly Dictionary<string, double> _thrHold = new Dictionary<string, double>(StringComparer.Ordinal);
        private static readonly double[] _bkH = new double[4];
        private static readonly double[] _upSum = new double[3], _upW = new double[3];

        /// <summary>Ludzie ksiegi i stanow kazdej osady na dzis: PeopleOf, pasma, pochylenie BK, konto niewolnych, wielkie rody. Nie rzuca.
        /// Suma stanow ksiegi = ksiega z budowy (udzialy sumuja sie do 1) - to kontrola arytmetyki; ksiege wobec linii "Ludnosc" sprawdza sprawdz_logi NZC0-2.</summary>
        private static void PeopleDay()
        {
            _book = _bookVil = _bookTown = _estSum = _houses = 0; _houseClans = _houseHeroes = 0; _tiltN = _bkN = 0; _outsideL = 0;
            Array.Clear(_upSum, 0, 3); Array.Clear(_upW, 0, 3);
            // 1. ludzie ksiegi i klasy BK z kalibracji kiesy ludu
            foreach (var r in _recs)
            {
                Array.Clear(r.N, 0, Estates); Array.Clear(r.Y, 0, Estates); r.Thralls = 0; r.L = 0; r.Upper = double.NaN; r.Clans = 0;
                if (r.Kind == KCastle) continue;
                try
                {
                    double p = PopulationLaw.PeopleOf(r.St);
                    r.L = p > 0 && !double.IsInfinity(p) && !double.IsNaN(p) ? p : 0;
                }
                catch (Exception e) { r.L = 0; Stumble("PeopleOf", e); }
                if (r.Kind == KVillage) { _bookVil += r.L; if (r.OutsideBound) _outsideL += r.L; } else _bookTown += r.L;
                try
                {
                    if (TownFolk.ZcBkHeads(r.St, _bkH))
                    {
                        double h = _bkH[0] + _bkH[1] + _bkH[2] + _bkH[3];
                        if (h > 0) { r.Upper = (_bkH[1] + _bkH[2]) / h; _upSum[r.Kind] += r.Upper * r.L; _upW[r.Kind] += r.L; _bkN++; }
                    }
                }
                catch (Exception e) { Stumble("ZcBkHeads", e); }
            }
            // 2. pasma x pochylenie BK; ludzie stanow
            foreach (var r in _recs)
            {
                if (r.Kind == KCastle) continue;
                bool town = r.Kind == KTown;
                Bands(r.Cul, town, r.Share);
                r.Tilted = false;
                if (!double.IsNaN(r.Upper) && _upW[r.Kind] > 0)
                {
                    double mean = _upSum[r.Kind] / _upW[r.Kind];
                    if (mean > 0)
                    {
                        double f = Math.Max(0.7, Math.Min(1.3, r.Upper / mean));
                        int rest = town ? ELabour : EPeasant;
                        double add = (r.Share[ECraft] + r.Share[EMerchant]) * (f - 1.0);
                        if (r.Share[rest] - add >= 0)
                        {
                            r.Share[ECraft] *= f; r.Share[EMerchant] *= f; r.Share[rest] -= add;
                            r.Tilted = Math.Abs(f - 1.0) > 1e-9; if (r.Tilted) _tiltN++;
                        }
                    }
                }
                for (int e = 0; e < 6; e++) r.N[e] = r.L * r.Share[e];
            }
            // 3. konto NIEWOLNI ksiegi - region trzymania
            _thrBook = 0; _thrNoRegion = 0;
            try { PeopleBook.ThrallsByHold(_thrHold); } catch (Exception e) { _thrHold.Clear(); Stumble("ThrallsByHold", e); }
            foreach (var kv in _thrHold)
            {
                if (!(kv.Value > 0)) continue;
                _thrBook += kv.Value;
                Rec r;
                if (kv.Key.Length == 0 || !_byHold.TryGetValue(kv.Key, out r)) { _thrNoRegion += kv.Value; continue; }
                r.Thralls += kv.Value; r.N[ESlave] += kv.Value;
            }
            _book = _bookVil + _bookTown + _thrBook;
            foreach (var r in _recs) for (int e = 0; e < 6; e++) _estSum += r.N[e];
            _estSum += _thrNoRegion;
            // 4. wielkie rody z bohaterow (ponad ksiege)
            try
            {
                foreach (var c in Clan.All)
                {
                    if (c == null || c.IsEliminated || c.IsBanditFaction) continue;
                    var fiefs = c.Fiefs;
                    if (fiefs == null || fiefs.Count == 0) continue;
                    int alive = 0;
                    var hs = c.Heroes;
                    if (hs != null) for (int i = 0; i < hs.Count; i++) { var h = hs[i]; if (h != null && h.IsAlive) alive++; }
                    if (alive <= 0) continue;
                    double heads = 3.0 * alive, inc = 0;
                    try { inc = ClanIncomeBook.StableIncome(c); } catch (Exception e) { inc = 0; Stumble("StableIncome", e); }
                    if (!Fin(inc)) { Stumble("StableIncome-NaN", null, c.StringId); inc = 0; }
                    inc = Math.Max(0.0, inc);
                    int places = 0;
                    for (int i = 0; i < fiefs.Count; i++) { var t = fiefs[i]; if (t != null && t.Settlement != null && _byS.ContainsKey(t.Settlement)) places++; }
                    if (places == 0) continue;
                    _houseClans++; _houseHeroes += alive; _houses += heads;
                    for (int i = 0; i < fiefs.Count; i++)
                    {
                        var t = fiefs[i];
                        Rec r;
                        if (t == null || t.Settlement == null || !_byS.TryGetValue(t.Settlement, out r)) continue;
                        r.N[EHouse] += heads / places; r.Y[EHouse] += inc / places; r.Clans += 1.0 / places;
                    }
                }
            }
            catch (Exception e) { Stumble("Rody", e); }
            // 5. krolestwo regionu (wlasciciel warowni dzis)
            foreach (var r in _recs)
            {
                var reg = r.Region != null ? r.Region.St : r.St;
                Kingdom k = null;
                try { k = reg != null && reg.OwnerClan != null ? reg.OwnerClan.Kingdom : null; } catch (Exception e) { k = null; Stumble("Kingdom", e); }
                r.KingdomId = k != null ? k.StringId ?? "?" : "-";
                string nm;
                if (k == null) nm = "bez krolestwa";
                else if (!_kName.TryGetValue(k, out nm)) { nm = k.Name != null ? Clean(k.Name.ToString()) : r.KingdomId; _kName[k] = nm; }   // TextObject.ToString raz na krolestwo (spis od nowa co 28 dob)
                r.KingdomName = nm;
            }
        }
    }
}
