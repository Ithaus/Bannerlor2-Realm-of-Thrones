using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
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
    /// ZC2 (projekt rozdz. 14 "ZC2 stany w ksiedze + widok", klucz DailyLifeEstates): STANY W KSIEDZE I W ZAPISIE. Udzialy 6 stanow ksiegi kazdego miasta i wsi
    /// (w milionowych, suma dokladnie 1 000 000) siedza w ksiedze stanow (_eb) i w zapisie (arm_estates, SaveText.Sync - kawalki ponizej limitu napisu):
    ///  - zasiew: osada bez wpisu (stary zapis - wszystkie 798 w pierwszej dobie pomiaru z kluczem; nowa kampania - pierwsza doba po kalibracji ksiegi ludzi;
    ///    osada nowa w spisie) dostaje udzialy ZC0 (pasma x pochylenie klasami BK tej doby) zaokraglone do milionowych - w pamieci od razu te same liczby
    ///    co w zapisie, wiec zapis + wczytanie daje te same stany co do czlowieka (suma kontrolna w liniach zapisu, wczytania i doby);
    ///  - dalej udzialy STOJA (ZC2 nikogo nie przesuwa; awans i spadek - ZC6, blizna wojny - ZC7): ludzie stanu = ludzie ksiegi osady x udzial; konto
    ///    NIEWOLNI i wielkie rody jak w ZC0 (z ksiegi i z bohaterow kazdej doby);
    ///  - pamiec Dostatku: co 7 dob D osady bez wielkich rodow (D0, setne) w 5 tygodniach (strzalka "w 28 dob" w widoku; zamek - D regionu) - tez w zapisie.
    /// Klucz wylaczony (albo DailyLifeMeasure wylaczony): stany jak ZC0 z pasm co dobe, ksiega stanow nietknieta i zapisywana dalej (wlaczenie wraca do niej).
    /// Zapis bez klucza arm_estates (zapis sprzed ZC2) albo w nieznanym formacie - zasiew przy pierwszej dobie pomiaru (linia "Stany (ZC2): ... zasiano").
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
            // ZC7 (DailyLife.War.cs): wies - uchodzcy okregu poza domem (PeopleBook "u:") w mianowniku Dostatku bez dochodu, wedlug stanow wolnych (chlopi, rzemieslnicy,
            // kupcy, szlachta - wedlug ludzi w domu); poza ksiega stanow (N - ludzie w domu, suma stanow = ksiega)
            public double Away;
            public readonly double[] AwayE = new double[Estates];
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
                    if (old != null && old.TryGetValue(st, out o) && o != null && o.Kind == kind) { Array.Copy(o.YAvg, r.YAvg, Estates); r.YDays = o.YDays; r.D0 = o.D0; r.DSrc = o.DSrc; }   // ZC7: Dostatek ostatniego pomiaru do blizny przed LivelihoodDay
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
                r.Away = 0; Array.Clear(r.AwayE, 0, Estates);   // ZC7: uchodzcy w mianowniku - WarPeople po PeopleDay (klucz DailyLifeWarScar)
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
            // 2. udzialy stanow: ZC2 (klucz DailyLifeEstates) - z ksiegi stanow (arm_estates); osada bez wpisu - pasma x pochylenie BK jak ZC0 i ZASIEW do ksiegi
            //    (stary zapis: wszystkie osady w pierwszej dobie). Bez klucza - jak ZC0: pasma x pochylenie BK co dobe, ksiega stanow nietknieta.
            bool book = EstatesOn;
            int seedTowns = 0, seedVils = 0, seedTilt = 0, seedNoBk = 0;
            foreach (var r in _recs)
            {
                if (r.Kind == KCastle) continue;
                bool town = r.Kind == KTown;
                EBook eb = null;
                if (book && SafeId(r.Id) && _eb.TryGetValue(r.Id, out eb) && eb.P != null)
                {
                    for (int e = 0; e < 6; e++) r.Share[e] = eb.P[e] / (double)PpmAll;
                    r.Tilted = eb.Tilted; if (r.Tilted) _tiltN++;
                    for (int e = 0; e < 6; e++) r.N[e] = r.L * r.Share[e];
                    continue;
                }
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
                if (book && SafeId(r.Id))
                {
                    // ZC2: zasiew - udzialy w milionowych (suma dokladnie 1 000 000), w pamieci od razu te same liczby co w zapisie (zapis + wczytanie = te same stany)
                    if (eb == null) { eb = new EBook(); _eb[r.Id] = eb; }
                    eb.P = Quantize(r.Share, town ? ELabour : EPeasant);
                    eb.SeedDay = _eDay; eb.Tilted = r.Tilted;
                    for (int e = 0; e < 6; e++) r.Share[e] = eb.P[e] / (double)PpmAll;
                    if (town) seedTowns++; else seedVils++;
                    if (r.Tilted) seedTilt++;
                    if (double.IsNaN(r.Upper)) seedNoBk++;
                }
                for (int e = 0; e < 6; e++) r.N[e] = r.L * r.Share[e];
            }
            if (seedTowns + seedVils > 0) SeedNote(seedTowns, seedVils, seedTilt, seedNoBk);
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

        // ============================================================ ZC2: ksiega stanow (udzialy w zapisie arm_estates) i pamiec Dostatku
        private const string PE = "Stany (ZC2): ";
        // v4|bazowa doba zasiewu|5 dob pamieci D|ZC7 od doby,ostatnia doba z czlonem dobrobytu,doba czlonow|id:p0,...,p5:doba zasiewu:pochylenie:d0,...,d4:bbb:blizna:dobrobyt:wolnosc;...
        // (v1 - bez pol bbb, blizna i dalszych, v2 - bez pola blizna i dalszych, v3 - bez naglowka ZC i pol dobrobyt, wolnosc - czytane)
        private const string EVersion = "v4";
        private const int PpmAll = 1000000;        // udzialy w milionowych
        private const int RingN = 5;               // pamiec Dostatku: 5 tygodni (strzalka "w 28 dob" ma zawsze wpis sprzed >= 28 dob)

        /// <summary>Wpis ksiegi stanow osady: udzialy 6 stanow ksiegi (null - jeszcze nie zasiane; zamek - zawsze null, sama pamiec D), doba zasiewu,
        /// pochylenie BK przy zasiewie, D0 osady w dobach _ringDays (setne; NaN - brak).</summary>
        private sealed class EBook
        {
            public int[] P;
            public int SeedDay = -1;
            public bool Tilted;
            public readonly float[] Ring = { float.NaN, float.NaN, float.NaN, float.NaN, float.NaN };
            // ZC6 (DailyLife.Mobility.cs): przeglady tygodniowe z rzedu z Dostatkiem ponizej progu spadku - rzemieslnicy, kupcy, szlachta (0-9; zapis v2, pole "bbb")
            public readonly byte[] Below = new byte[3];
            // ZC6, pamiec sesji (widok "w ostatnich 28 dobach"): ludzie przesunieci w 4 ostatnich przegladach (przeglad x przejscie), doby przegladow, przybysze od przegladu
            public float[] Mv; public int[] MvDay; public float MigAcc;
            // ZC7 (DailyLife.War.cs; zapis v3, pole "blizna"): epizod wojny osady - ostatnie zdarzenie (najazd na wies, zlupienie miasta), poczatek epizodu (pierwsze
            // zdarzenie po 30 dobach spokoju), rodzaj (1 najazd, 2 grabiez, 3 spustoszenie), Dostatek osady przed epizodem i najnizszy od niego (setne), udzial
            // trafionych ludzi w epizodzie; dlug blizny w milionowych: wies [0] rzemieslnicy -> chlopi, miasto [0] kupcy -> rzemieslnicy, [1] rzemieslnicy -> biedota
            public int ScarDay = -1, ScarFrom = -1, ScarKind;
            public float ScarD = float.NaN, ScarMin = float.NaN, ScarF;
            public readonly int[] Debt = new int[2];
            public bool Rep7;                                // pamiec sesji: epizod juz w linii "d+7"
            // ZC6 dobrobyt (DailyLife.Prosperity.cs; zapis v4, pole "dobrobyt" - takze zamki): kotwica warowni - D0 regionu w dniu wlaczenia klucza, suma naszych
            // czlonow od kotwicy, czlon ostatniej doby, kotwica z uchodzcami ZC7 w mianowniku
            public double PD0 = double.NaN, PCum;
            public float PTerm;
            public bool PWar;
            // ZC6 wyzwolenie przez zdobywce (DailyLife.Mobility.cs; zapis v4, pole "wolnosc"): 0 - jeszcze nie widziany, 1 - region byl pod wladca z niewola od wlaczenia
            // (zdobycie przez wladce bez niewoli wyzwala), 2 - od wlaczenia tylko pod wladca bez niewoli (stan zastany - bez wyzwalania)
            public byte FreeBase;
        }

        private static readonly Dictionary<string, EBook> _eb = new Dictionary<string, EBook>(StringComparer.Ordinal);
        private static readonly int[] _ringDays = { -1, -1, -1, -1, -1 };
        private static int _eDay = -1;                              // doba pomiaru (zasiew)
        private static int _eSeedDay, _eSeedSess, _eSeedTiltSess, _eSeedNoBkSess;   // zasiane w tej dobie / od wczytania
        private static int _eLoadN, _eLoadP, _eLoadBad, _eLoadBadP;
        private static int _eLoadPros, _eLoadBadPros, _eLoadFree;   // v4: kotwice dobrobytu (zle pola), znana wolnosc regionu
        private static bool _eLoaded;
        private static uint _eLoadHash;
        private static string _eLoadNote;
        private static int _eSaves;

        internal static bool EstatesOn { get { var s = Settings.Current; return s != null && s.DailyLifeMeasure && s.DailyLifeEstates; } }

        /// <summary>Z Reset (konstruktor ArmouryBehavior): ksiega stanow i licznik widoku od nowa - zapis wczytuje ImportEstates po konstruktorze.</summary>
        private static void ResetEstates()
        {
            _eb.Clear();
            for (int i = 0; i < RingN; i++) _ringDays[i] = -1;
            _eDay = -1; _eSeedDay = _eSeedSess = _eSeedTiltSess = _eSeedNoBkSess = 0;
            _eLoadN = _eLoadP = _eLoadBad = _eLoadBadP = 0; _eLoaded = false; _eLoadHash = 0; _eLoadNote = null; _eSaves = 0;
            ResetView();
            ResetMobility();                                // ZC6: liczniki awansu i dobrobytu sesji (stan kampanii - pole "bbb" w arm_estates)
            ResetWar();                                     // ZC7: uderzenia czekajace na dobe, liczniki blizny sesji (stan kampanii - pole "blizna" w arm_estates)
        }

        /// <summary>Udzialy (suma 1) -> milionowe z suma dokladnie 1 000 000 (reszta zaokraglen do stanu rest; gdyby rest mial za malo - do najwiekszego).</summary>
        private static int[] Quantize(double[] sh, int rest)
        {
            var p = new int[6];
            long s = 0;
            for (int e = 0; e < 6; e++)
            {
                double v = sh[e];
                long q = Fin(v) && v > 0 ? (long)Math.Round(v * PpmAll) : 0;
                if (q > PpmAll) q = PpmAll;
                p[e] = (int)q; s += q;
            }
            long d = PpmAll - s;
            if (d != 0)
            {
                int at = rest;
                if (p[at] + d < 0) { at = 0; for (int e = 1; e < 6; e++) if (p[e] > p[at]) at = e; }
                p[at] = (int)Math.Max(0, p[at] + d);
            }
            s = 0; for (int e = 0; e < 6; e++) s += p[e];
            if (s != PpmAll) { Array.Clear(p, 0, 6); p[rest] = PpmAll; }   // udzialy zdegenerowane - wszyscy w stanie "ostatnim"
            return p;
        }

        /// <summary>Linia zdarzenia zasiewu (zawsze, nie tylko w dobie linii - zdarzenie rzadkie: stary zapis, nowa kampania, nowa osada).</summary>
        private static void SeedNote(int towns, int vils, int tilt, int noBk)
        {
            _eSeedDay += towns + vils; _eSeedSess += towns + vils; _eSeedTiltSess += tilt; _eSeedNoBkSess += noBk;
            try
            {
                Log.Info(PE + "dzien " + _eDay + " | zasiano " + (towns + vils) + " osad (miasta " + towns + ", wsie " + vils + ") z pasm x klasy BK (pochylonych " + tilt + ", bez klas BK " + noBk
                         + ") - od teraz stany w ksiedze i w zapisie (arm_estates), " + (MobOn || FreeOn || WarOn ? "udzialy przesuwaja: " + MoversText() : "udzialy stoja (awans ZC6 i blizna ZC7 wylaczone)")
                         + " | " + (_eLoaded ? "zapis mial ksiege stanow " + _eLoadP + " osad" : "zapis bez ksiegi stanow (sprzed ZC2) albo nowa kampania")
                         + " | ksiega stanow teraz " + CountP() + " osad, zapis ok. " + F1(EstimateKb()) + " KB | " + GameEffectText() + ".");
            }
            catch (Exception e) { Stumble("SeedNote", e); }
        }

        /// <summary>Kto przesuwa udzialy stanow (klucze czynne): awans i spadek ZC6, wyzwolenie przez zdobywce, blizna wojny ZC7.</summary>
        private static string MoversText()
        {
            var l = new List<string>(3);
            if (MobOn) l.Add("awans i spadek (ZC6)");
            if (FreeOn) l.Add("wyzwolenie przez zdobywce (ZC6)");
            if (WarOn) l.Add("blizna wojny (ZC7)");
            return l.Count > 0 ? string.Join(", ", l.ToArray()) : "nikt";
        }

        /// <summary>Skutek w grze kluczy ZC6 / ZC7 (zamiast stalego "gra bez zmian"): ZC2 sam to etykiety i widok.</summary>
        private static string GameEffectText()
        {
            var l = new List<string>(4);
            if (MobOn) l.Add("awans i spadek stanow z pieniedzmi ludzi, oplata cechowa i kupno ziemi do panow (Mobility)");
            if (FreeOn) l.Add("wyzwolenia przez zdobywce (Freedom)");
            if (ProsOn) l.Add("dobrobyt warowni za Dostatkiem regionu (Prosperity Link)");
            if (WarOn) l.Add("blizna wojny w stanach i Dostatku (War Scar)");
            return l.Count == 0 ? "gra bez zmian (etykiety ludzi i widok - zloto, towar, ceny, dobrobyt, AI nietkniete)"
                                : "ZC2 to etykiety i widok, ale w grze dzialaja: " + string.Join("; ", l.ToArray());
        }

        private static int CountP() { int n = 0; foreach (var kv in _eb) if (kv.Value.P != null) n++; return n; }

        private static double EstimateKb() { return _eb.Count * 60.0 / 1024.0; }

        // ------------------------------------------------------------ suma kontrolna udzialow (niezalezna od kolejnosci: suma FNV-1a wpisow)
        private static uint EntryHash(string id, int[] p)
        {
            uint h = 2166136261u;
            for (int i = 0; i < id.Length; i++) { h ^= id[i]; h *= 16777619u; }
            for (int e = 0; e < 6; e++)
            {
                uint v = (uint)p[e];
                for (int b = 0; b < 4; b++) { h ^= (v >> (8 * b)) & 0xFF; h *= 16777619u; }
            }
            return h;
        }

        private static uint EstatesHash()
        {
            uint s = 0;
            foreach (var kv in _eb) if (kv.Value.P != null) s += EntryHash(kv.Key, kv.Value.P);
            return s;
        }

        private static string Hex(uint h) { return h.ToString("X8", Inv); }

        // ------------------------------------------------------------ pamiec Dostatku (co 7 dob)
        /// <summary>Doba z widokiem (po LivelihoodDay): co 7 dob (doba podzielna przez 7) D0 kazdej osady do pamieci (setne - te same liczby co w zapisie).</summary>
        private static void RingDay(int day)
        {
            if (day % 7 != 0 || _ringDays[0] == day) return;
            for (int i = RingN - 1; i > 0; i--) _ringDays[i] = _ringDays[i - 1];
            _ringDays[0] = day;
            foreach (var kv in _eb)
            {
                var g = kv.Value.Ring;
                for (int i = RingN - 1; i > 0; i--) g[i] = g[i - 1];
                g[0] = float.NaN;
            }
            foreach (var r in _recs)
            {
                if (!SafeId(r.Id)) continue;
                EBook eb;
                if (!_eb.TryGetValue(r.Id, out eb)) { eb = new EBook(); _eb[r.Id] = eb; }
                eb.Ring[0] = Fin(r.D0) && Math.Abs(r.D0) < 1e6 ? (float)(Math.Round(r.D0 * 100.0) / 100.0) : float.NaN;
            }
        }

        /// <summary>D0 osady sprzed ok. 28 dob z pamieci: najmlodszy wpis sprzed >= 28 dob, a bez niego najstarszy sprzed >= 14 dob. false - brak.</summary>
        private static bool PastD0(Rec r, out double d, out int days)
        {
            d = double.NaN; days = -1;
            EBook eb;
            if (r == null || r.Id == null || !_eb.TryGetValue(r.Id, out eb)) return false;
            int pick = -1;
            for (int i = 0; i < RingN; i++) if (_ringDays[i] >= 0 && _viewDay - _ringDays[i] >= 28) { pick = i; break; }
            if (pick < 0) for (int i = RingN - 1; i >= 0; i--) if (_ringDays[i] >= 0 && _viewDay - _ringDays[i] >= 14) { pick = i; break; }
            if (pick < 0) return false;
            float v = eb.Ring[pick];
            if (float.IsNaN(v) || float.IsInfinity(v)) return false;
            d = v; days = _viewDay - _ringDays[pick];
            return true;
        }

        // ------------------------------------------------------------ zapis arm_estates
        internal static string ExportEstates()
        {
            WarFlushSave();                                 // ZC7: uderzenia od switu (rabunek w toku) do ksiegi przed zapisem - zapis wczytany daje te same liczby
            if (_eb.Count == 0) return "";
            var sb = new StringBuilder(_eb.Count * 72 + 96);
            int n = 0, np = 0, nr = 0, npr = 0;
            try
            {
                // bazowa doba zasiewu = najczestsza (wpis z ta doba nie powtarza jej)
                var cnt = new Dictionary<int, int>();
                foreach (var kv in _eb) if (kv.Value.P != null) { int c; cnt.TryGetValue(kv.Value.SeedDay, out c); cnt[kv.Value.SeedDay] = c + 1; }
                int seedBase = -1, best = 0;
                foreach (var kv in cnt) if (kv.Value > best) { best = kv.Value; seedBase = kv.Key; }
                sb.Append(EVersion).Append('|').Append(seedBase.ToString(Inv)).Append('|');
                for (int i = 0; i < RingN; i++) { if (i > 0) sb.Append(','); sb.Append(_ringDays[i].ToString(Inv)); }
                sb.Append('|');
                // v4: naglowek ZC - doba wlaczenia ZC7 (uchodzcy w mianowniku: 28 dob bez licznikow spadku wsi), ostatnia doba z czlonem dobrobytu, doba czlonow w zapisie
                int termDay = _pTerm != null && _pTerm.Count > 0 ? _pTermDay : (_pTermSaved != null ? _pTermSavedDay : -1);
                sb.Append(_warSeedDay.ToString(Inv)).Append(',').Append(_pLastOn.ToString(Inv)).Append(',').Append((termDay == int.MinValue ? -1 : termDay).ToString(Inv)).Append('|');
                foreach (var kv in _eb)
                {
                    var b = kv.Value;
                    if (!SafeId(kv.Key)) continue;
                    bool ring = false;
                    for (int i = 0; i < RingN; i++) if (!float.IsNaN(b.Ring[i])) { ring = true; break; }
                    bool pros = Fin(b.PD0) && b.PD0 > 0;
                    if (b.P == null && !ring && !pros) continue;
                    if (n > 0) sb.Append(';');
                    sb.Append(kv.Key).Append(':');
                    if (b.P != null)
                    {
                        for (int e = 0; e < 6; e++) { if (e > 0) sb.Append(','); sb.Append(b.P[e].ToString(Inv)); }
                        np++;
                    }
                    sb.Append(':');
                    if (b.P != null && b.SeedDay != seedBase) sb.Append(b.SeedDay.ToString(Inv));
                    sb.Append(':');
                    if (b.P != null) sb.Append(b.Tilted ? '1' : '0');
                    sb.Append(':');
                    for (int i = 0; i < RingN; i++)
                    {
                        if (i > 0) sb.Append(',');
                        float v = b.Ring[i];
                        if (!float.IsNaN(v) && !float.IsInfinity(v)) sb.Append(((long)Math.Round(v * 100.0)).ToString(Inv));
                    }
                    sb.Append(':');
                    if (b.P != null && (b.Below[0] | b.Below[1] | b.Below[2]) != 0)
                        for (int i = 0; i < 3; i++) sb.Append((char)('0' + Math.Min((byte)9, b.Below[i])));   // ZC6: tygodnie ponizej progu spadku
                    sb.Append(':');
                    if (b.P != null) AppendScar(sb, b);              // ZC7: epizod wojny i dlug blizny (puste - bez wojny)
                    sb.Append(':');
                    if (pros) { AppendPros(sb, b); npr++; }          // ZC6: kotwica dobrobytu warowni (puste - bez kotwicy)
                    sb.Append(':');
                    if (b.P != null && (b.FreeBase == 1 || b.FreeBase == 2)) sb.Append((char)('0' + b.FreeBase));   // ZC6: wladca z niewola od wlaczenia (1) / zastany bez niewoli (2)
                    if (ring) nr++;
                    n++;
                }
            }
            catch (Exception e) { Stumble("ExportEstates", e); }
            string s = sb.ToString();
            _eSaves++;
            try
            {
                int parts = s.Length <= 8000 ? 1 : (s.Length + 7999) / 8000;
                Log.Info(PE + "zapis | dzien " + ((int)CampaignTime.Now.ToDays).ToString(Inv) + " | osad " + n + " (z udzialami " + np + ", z pamiecia Dostatku " + nr + ") | "
                         + F1(s.Length / 1024.0) + " KB (" + s.Length + " znakow, kawalkow " + parts + ") | suma kontrolna " + Hex(EstatesHash()) + " | zapis nr " + _eSaves + " w tej sesji"
                         + " | v4: kotwice dobrobytu " + npr + ", ZC7 od dnia " + _warSeedDay + ", czlon dobrobytu ostatnio dnia " + _pLastOn + ".");
            }
            catch { }
            return s;
        }

        internal static void ImportEstates(string data)
        {
            _eb.Clear();
            for (int i = 0; i < RingN; i++) _ringDays[i] = -1;
            _eLoaded = false; _eLoadN = _eLoadP = _eLoadBad = _eLoadBadP = 0; _eLoadHash = 0; _eLoadNote = null;
            _eLoadBadScar = 0; _eLoadVer = null; _eLoadPros = _eLoadBadPros = _eLoadFree = 0;
            _warSeedDay = -1; _pLastOn = -1; _pTermSaved = null; _pTermSavedDay = int.MinValue;   // v1-v3: ZC7 i kotwice dobrobytu od pierwszej doby z kluczem
            if (string.IsNullOrEmpty(data))
            {
                _eLoadNote = "brak klucza arm_estates (zapis sprzed ZC2 albo kampania bez klucza) - stany zasiane w pierwszej dobie pomiaru z kluczem";
                try { Log.Info(PE + "wczytanie | " + _eLoadNote + "."); } catch { }
                return;
            }
            try
            {
                bool v4 = data.StartsWith(EVersion + "|", StringComparison.Ordinal);
                var a = data.Split(new[] { '|' }, v4 ? 5 : 4);
                int seedBase;
                bool v1 = a.Length == 4 && a[0] == "v1", v2 = a.Length == 4 && a[0] == "v2", v3 = a.Length == 4 && a[0] == "v3";   // v1 - bez pol ZC6 i ZC7, v2 - bez blizny, v3 - bez dobrobytu i wolnosci
                v4 = v4 && a.Length == 5;
                string ver = v1 ? "v1" : v2 ? "v2" : v3 ? "v3" : EVersion;
                int fields = v1 ? 5 : v2 ? 6 : v3 ? 7 : 9;
                if (!(v1 || v2 || v3 || v4) || !int.TryParse(a[1], NumberStyles.Integer, Inv, out seedBase))
                {
                    _eLoadNote = "arm_estates w nieznanym formacie - pominiety, stany zasiane od nowa";
                    Log.Info(PE + "wczytanie | " + _eLoadNote + ".");
                    return;
                }
                var rd = a[2].Split(',');
                if (rd.Length == RingN)
                    for (int i = 0; i < RingN; i++) { int x; _ringDays[i] = int.TryParse(rd[i], NumberStyles.Integer, Inv, out x) ? x : -1; }
                if (v4)
                {
                    var hz = a[3].Split(',');
                    int x;
                    if (hz.Length >= 1 && int.TryParse(hz[0], NumberStyles.Integer, Inv, out x)) _warSeedDay = x;
                    if (hz.Length >= 2 && int.TryParse(hz[1], NumberStyles.Integer, Inv, out x)) _pLastOn = x;
                    if (hz.Length >= 3 && int.TryParse(hz[2], NumberStyles.Integer, Inv, out x) && x >= 0) _pTermSavedDay = x;
                }
                string ents = v4 ? a[4] : a[3];
                if (ents.Length > 0)
                    foreach (var ent in ents.Split(';'))
                    {
                        if (ent.Length == 0) continue;
                        var p = ent.Split(':');
                        if (p.Length != fields || !SafeId(p[0])) { _eLoadBad++; continue; }
                        var b = new EBook();
                        if (p[1].Length > 0)
                        {
                            var q = p[1].Split(',');
                            var pp = new int[6];
                            long sum = 0; bool ok = q.Length == 6;
                            for (int e = 0; ok && e < 6; e++)
                            {
                                int x;
                                if (!int.TryParse(q[e], NumberStyles.Integer, Inv, out x) || x < 0 || x > PpmAll) ok = false;
                                else { pp[e] = x; sum += x; }
                            }
                            if (ok && sum == PpmAll)
                            {
                                b.P = pp;
                                int sd;
                                b.SeedDay = p[2].Length == 0 ? seedBase : int.TryParse(p[2], NumberStyles.Integer, Inv, out sd) ? sd : seedBase;
                                b.Tilted = p[3] == "1";
                            }
                            else _eLoadBadP++;                       // zle udzialy - osada zasiana od nowa
                        }
                        var g = p[4].Split(',');
                        if (g.Length == RingN)
                            for (int i = 0; i < RingN; i++)
                            {
                                long x;
                                b.Ring[i] = g[i].Length > 0 && long.TryParse(g[i], NumberStyles.Integer, Inv, out x) ? (float)(x / 100.0) : float.NaN;
                            }
                        if (!v1 && b.P != null && p[5].Length == 3)
                            for (int i = 0; i < 3; i++) { int x = p[5][i] - '0'; b.Below[i] = (byte)(x >= 0 && x <= 9 ? x : 0); }   // ZC6: tygodnie ponizej progu spadku
                        if (fields >= 7 && b.P != null && p[6].Length > 0 && !ParseScar(p[6], b)) _eLoadBadScar++;   // ZC7: epizod wojny i dlug blizny
                        if (fields >= 9)
                        {
                            if (p[7].Length > 0) { if (ParsePros(p[7], b, p[0])) _eLoadPros++; else _eLoadBadPros++; }   // ZC6: kotwica dobrobytu (zle pole - kotwica od nowa)
                            if (b.P != null && p[8].Length == 1 && (p[8][0] == '1' || p[8][0] == '2')) { b.FreeBase = (byte)(p[8][0] - '0'); _eLoadFree++; }
                        }
                        _eb[p[0]] = b;
                        _eLoadN++; if (b.P != null) _eLoadP++;
                    }
                if (_pTermSavedDay == int.MinValue) _pTermSaved = null;
                _eLoaded = true; _eLoadHash = EstatesHash(); _eLoadVer = ver;
                Log.Info(PE + "wczytanie | format " + ver + " | osad " + _eLoadN + " (z udzialami " + _eLoadP + ", pominiete wpisy " + _eLoadBad + ", zle udzialy - zasiew od nowa " + _eLoadBadP
                         + ") | pamiec Dostatku z dob " + string.Join(",", Array.ConvertAll(_ringDays, x => x.ToString(Inv))) + " | suma kontrolna " + Hex(_eLoadHash)
                         + " | v4: kotwice dobrobytu " + _eLoadPros + " (zle " + _eLoadBadPros + "), czlony doby " + (_pTermSaved != null ? _pTermSaved.Count : 0) + " (dnia " + (_pTermSavedDay == int.MinValue ? -1 : _pTermSavedDay)
                         + "), wolnosc znana " + _eLoadFree + ", ZC7 od dnia " + _warSeedDay + ", czlon dobrobytu ostatnio dnia " + _pLastOn + ".");
            }
            catch (Exception e)
            {
                _eb.Clear(); for (int i = 0; i < RingN; i++) _ringDays[i] = -1; _eLoaded = false;
                _warSeedDay = -1; _pLastOn = -1; _pTermSaved = null; _pTermSavedDay = int.MinValue;
                _eLoadNote = "blad odczytu arm_estates - stany zasiane od nowa";
                Stumble("ImportEstates", e);
            }
        }

        /// <summary>OnSessionLaunched (po SessionStart ZC0): linia startu ZC2 - klucz, ksiega stanow z zapisu, widok.</summary>
        internal static void EstatesSessionStart()
        {
            try
            {
                var s = Settings.Current;
                Log.Info(PE + "start sesji | klucz Daily Life Estates " + (s != null && s.DailyLifeEstates ? "tak" : "WYLACZONE") + " (Daily Life Measure " + (KeyOn ? "tak" : "WYLACZONE") + ")"
                         + (EstatesOn ? "" : " - stany jak ZC0 z pasm co dobe, ksiega stanow nietknieta, bez widoku")
                         + " | ksiega stanow z zapisu: " + (_eLoaded ? _eLoadP + " osad z udzialami, " + _eLoadN + " wpisow, suma kontrolna " + Hex(_eLoadHash) : (_eLoadNote ?? "brak"))
                         + " | widok: dymki osad gry (miasta, zamki, wsie: " + TipWhere + ") i wiosek pobocznych, People of this land, Look over the district, Realm ledger (menu miasta i zamku)"
                         + " i Estates and livelihood (menu wsi, miasta i zamku) - okna ShowInquiry / ShowMultiSelectionInquiry, bez GameMenu.SwitchToMenu | " + GameEffectText() + ".");
            }
            catch (Exception e) { Stumble("EstatesSessionStart", e); }
        }

        /// <summary>Linia doby ZC2 (doba linii): pokrycie ksiegi stanow, zasiew, swiat wedlug stanow, suma kontrolna, pamiec Dostatku, widok.</summary>
        private static string EstatesLine(int day)
        {
            int towns = 0, vils = 0, castles = 0, tP = 0, vP = 0, miss = 0;
            foreach (var r in _recs)
            {
                if (r.Kind == KCastle) { castles++; continue; }
                EBook eb;
                bool has = SafeId(r.Id) && _eb.TryGetValue(r.Id, out eb) && eb.P != null;
                if (r.Kind == KTown) { towns++; if (has) tP++; } else { vils++; if (has) vP++; }
                if (!has) miss++;
            }
            int all = towns + vils + castles, cov = tP + vP + castles;
            int weeks = 0; for (int i = 0; i < RingN; i++) if (_ringDays[i] >= 0) weeks++;
            double burgh = _nE[ELabour] + _nE[ECraft] + _nE[EMerchant];
            var sb = new StringBuilder(900);
            sb.Append(PE).Append("dzien ").Append(day).Append(" | stany w ksiedze ").Append(cov).Append('/').Append(all).Append(" osad (miasta ").Append(tP).Append('/').Append(towns)
              .Append(", wsie ").Append(vP).Append('/').Append(vils).Append(" z udzialami; zamki ").Append(castles).Append(" - ludzie w ich wsiach; bez udzialow ").Append(miss).Append(')')
              .Append(" | zasiano dzis ").Append(_eSeedDay).Append(", od wczytania ").Append(_eSeedSess).Append(" (pochylonych BK ").Append(_eSeedTiltSess).Append(", bez klas BK ").Append(_eSeedNoBkSess)
              .Append("), z zapisu ").Append(_eLoadP).Append(" (zle udzialy ").Append(_eLoadBadP).Append(')')
              .Append(" | swiat (ksiega): chlopi ").Append(N0(_nE[EPeasant])).Append(", mieszczanie ").Append(N0(burgh)).Append(" (biedota ").Append(N0(_nE[ELabour])).Append(", rzemieslnicy ")
              .Append(N0(_nE[ECraft])).Append(", kupcy ").Append(N0(_nE[EMerchant])).Append("), szlachta ").Append(N0(_nE[EGentry])).Append(", wielkie rody ").Append(N0(_nE[EHouse]))
              .Append(", niewolni ").Append(N0(_nE[ESlave]))
              .Append(" | suma kontrolna ").Append(Hex(EstatesHash()))
              .Append(" | pamiec Dostatku: tygodni ").Append(weeks).Append(" (ostatni dzien ").Append(_ringDays[0]).Append(')')
              .Append(" | widok od ostatniej linii: ").Append(ViewText());
            sb.Append('.');
            return sb.ToString();
        }
    }
}
