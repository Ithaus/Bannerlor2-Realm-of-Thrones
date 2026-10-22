using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// m106-wozy W2 (22.10, diagnoza scratchpad wozy/diagnoza-wozow.md P3; Jeff: gospodarka z podazy i popytu, z biezacego zysku, start neutralny, bez sztywnych mnoznikow):
    /// PAMIEC TRAS WOZOW WSI i BRAMA WROGA.
    /// 1) Village Cart Route Risk - przezywalnosc trasy z ostatnich kursow: para "region (warownia wsi) | miasto" -> {dojechalo A, kursow N} (przyjazd = sprzedaz w miescie celu,
    ///    MarketCarts.SellPrefix; strata = woz rozbity przez sprawce w kursie do miasta, MarketRoad.OnPartyDestroyed; rozwiazanie bez sprawcy niczego nie liczy). Wiesci
    ///    gasna: A i N x 0.5^(doby/56) przy kazdym odczycie i zapisie. Przezywalnosc s = (A + 2 x sw) / (N + 2), sw - swiat z tym samym wygasaniem (bez danych 1): bez wiesci
    ///    s = sw dla wszystkich miast wozu, wiec wybor taki sam jak bez pamieci (start neutralny), zla trasa po wygasnieciu wraca do sw. MarketCarts.Choose mnozy przez s tylko
    ///    wynik wyboru (rezerwacje zlota G4, wiesc z drogi i linie - czysta wycena). Zapis arm_cart_risk (SaveText); klucz wylaczony - brak aktualizacji i wagi, napis z zapisu
    ///    oddawany bez zmian; brak klucza w zapisie - pamiec pusta.
    /// 2) Village Cart Enemy Gate - kandydat odrzucony, gdy droga do niego biegnie pod brama wrogiej twierdzy F (zamek albo miasto frakcji w wojnie z frakcja wozu):
    ///    d(zrodlo, F) + d(F, miasto) - d(zrodlo, miasto) &lt;= max(2, 2% d) w pamieci drog gry (MapDistanceModel, droga ladowa); wstepnie d(zrodlo, F) &lt; d. Wynik dla wozu
    ///    w domu pamietany (wies, miasto, wersja wojen - wojna, pokoj, zmiana wlasciciela osady, zmiana krolestwa rodu). Woz w polu - liczony na biezaco.
    /// Zloto i towar sie nie ruszaja - tylko wybor celu.
    /// </summary>
    internal static class CartRisk
    {
        internal const double HalfDays = 56.0;          // polowa wagi wiesci po 56 dobach (ok. dwa miesiace)
        internal const double Prior = 2.0;              // start neutralny: dwa kursy "jak swiat"
        internal const int MaxPairs = 1500;             // najwyzej tyle par w zapisie (zostaja te z najwiekszym N)
        internal const int MaxAge = 364;                // para bez kursu od roku - przycieta
        private const string Fmt = "m106r1";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class Rec { public double A, N; public int Day, Touch; }
        private sealed class Trip { public string Key; public Settlement To; public int Day; }
        private sealed class Memo { public int Ver; public Settlement F; }

        // ------------------------------------------------------------ stan (pamiec tras w zapisie; kursy w toku i brama - pamiec sesji)
        private static readonly Dictionary<string, Rec> _pairs = new Dictionary<string, Rec>(StringComparer.Ordinal);
        private static readonly Rec _world = new Rec();
        private static readonly Dictionary<MobileParty, Trip> _trips = new Dictionary<MobileParty, Trip>();
        private static string _raw;                     // napis z zapisu (klucz wylaczony - zapisany z powrotem bez zmian)
        private static int _loaded = -1;                // par wczytanych z zapisu (-1 - brak klucza w zapisie)
        private static int _warVer, _fortVer = -1;
        private static readonly Dictionary<IFaction, List<Settlement>> _forts = new Dictionary<IFaction, List<Settlement>>();
        private static readonly Dictionary<Settlement, Dictionary<Settlement, Memo>> _memo = new Dictionary<Settlement, Dictionary<Settlement, Memo>>();
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        // ------------------------------------------------------------ liczniki doby (linia "Wozy (m106): dzien")
        private static int _arr, _lost, _lostBand, _lostEnemy, _lostTrip, _stumbles;
        private static readonly Dictionary<string, int> _lostBy = new Dictionary<string, int>(StringComparer.Ordinal);
        private static long _ticks;

        internal static bool On { get { var s = Settings.Current; return s != null && s.VillageCartRouteRisk; } }
        internal static bool GateOn { get { var s = Settings.Current; return s != null && s.VillageCartEnemyGate; } }
        internal static int Loaded { get { return _loaded; } }

        internal static void Reset()
        {
            _pairs.Clear(); _world.A = _world.N = 0; _world.Day = _world.Touch = 0;
            _trips.Clear(); _raw = null; _loaded = -1;
            _warVer = 0; _fortVer = -1; _forts.Clear(); _memo.Clear(); _errSites.Clear();
            ClearDay();
        }

        private static void ClearDay()
        {
            _arr = _lost = _lostBand = _lostEnemy = _lostTrip = _stumbles = 0;
            _lostBy.Clear(); _ticks = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errSites.Add(where)) Log.Error(where, e);
        }

        private static int Today() { try { return (int)CampaignTime.Now.ToDays; } catch { return 0; } }

        private static string Clean(string s) { return string.IsNullOrEmpty(s) ? "?" : s.Replace('|', '_').Replace(':', '_').Replace('!', '_').Replace('~', '_'); }

        /// <summary>Klucz pary: region wsi (StringId warowni - Bound) | miasto. Region daje wiecej danych niz pojedyncza wies (1-3 sasiadki tej samej drogi).</summary>
        private static string Key(Settlement home, Settlement to)
        {
            if (home == null || to == null) return null;
            var v = home.Village;
            var reg = v != null && v.Bound != null ? v.Bound.StringId : home.StringId;
            return Clean(reg) + "|" + Clean(to.StringId);
        }

        private static void Decay(Rec r, int today)
        {
            if (today > r.Day)
            {
                double f = Math.Pow(0.5, (today - r.Day) / HalfDays);
                r.A *= f; r.N *= f;
            }
            r.Day = today;
        }

        /// <summary>Przezywalnosc swiata (wygasanie jak par); bez danych 1.</summary>
        private static double WorldSurvival(int today)
        {
            Decay(_world, today);
            return (_world.A + 1e-9) / (_world.N + 1e-9);
        }

        /// <summary>Przezywalnosc trasy region wsi -> miasto (0..1): (A + 2 sw) / (N + 2); bez danych o parze - sw. Klucz wylaczony - 1 (kazda droga bezpieczna, jak dotad).</summary>
        internal static double Survival(Settlement home, Town to)
        {
            if (!On || to == null) return 1.0;
            try
            {
                int today = Today();
                double sw = WorldSurvival(today);
                string k = Key(home, to.Settlement);
                Rec r;
                if (k == null || !_pairs.TryGetValue(k, out r)) return sw;
                Decay(r, today);
                double s = (r.A + Prior * sw) / (r.N + Prior);
                return s > 0 && !double.IsNaN(s) ? Math.Min(1.0, s) : sw;
            }
            catch (Exception e) { Stumble("CartRisk.Survival", e); return 1.0; }
        }

        private static void Bump(string k, double a, int today)
        {
            Rec r;
            if (!_pairs.TryGetValue(k, out r)) { r = new Rec { Day = today }; _pairs[k] = r; }
            Decay(r, today); r.A += a; r.N += 1.0; r.Touch = today;
            Decay(_world, today); _world.A += a; _world.N += 1.0; _world.Touch = today;
        }

        /// <summary>MarketCarts.Choose po wyborze: kurs w toku (pamiec sesji). Klucz wylaczony - nic.</summary>
        internal static void Start(MobileParty cart, Settlement home, Town to)
        {
            if (cart == null || to == null || !On) return;
            try
            {
                string k = Key(home, to.Settlement);
                if (k == null) return;
                _trips[cart] = new Trip { Key = k, To = to.Settlement, Day = Today() };
            }
            catch (Exception e) { Stumble("CartRisk.Start", e); }
        }

        /// <summary>MarketCarts.SellPrefix: woz sprzedaje w osadzie `at`. Przyjazd liczy sie tylko w miescie celu kursu (postoj w zamku po drodze - kurs trwa dalej).</summary>
        internal static void Arrive(MobileParty cart, Settlement at)
        {
            if (cart == null || at == null) return;
            try
            {
                Trip t;
                if (!_trips.TryGetValue(cart, out t) || t.To != at) return;
                _trips.Remove(cart);
                if (!On) return;
                Bump(t.Key, 1.0, Today());
                _arr++;
            }
            catch (Exception e) { Stumble("CartRisk.Arrive", e); }
        }

        /// <summary>MarketRoad.OnPartyDestroyed: woz wsi zniknal. Ze sprawca - licznik linii (bandy / wrogowie, frakcja wsi przez frakcje sprawcy); w kursie do miasta i z kluczem -
        /// strata trasy (N + 1). Bez sprawcy (rozwiazanie) - tylko koniec kursu.</summary>
        internal static void Lost(MobileParty party, PartyBase destroyer)
        {
            if (party == null || !party.IsVillager) return;
            try
            {
                Trip t;
                bool trip = _trips.TryGetValue(party, out t);
                if (trip) _trips.Remove(party);
                if (destroyer == null) return;
                _lost++;
                var dm = destroyer.MobileParty;
                bool band = dm != null && dm.IsBandit;
                if (band) _lostBand++; else _lostEnemy++;
                string a = FName(party.MapFaction ?? (party.HomeSettlement != null ? party.HomeSettlement.MapFaction : null));
                string b = FName(destroyer.MapFaction);
                string key = a + " przez " + b;
                int n; _lostBy.TryGetValue(key, out n); _lostBy[key] = n + 1;
                if (trip && On) { Bump(t.Key, 0.0, Today()); _lostTrip++; }
            }
            catch (Exception e) { Stumble("CartRisk.Lost", e); }
        }

        private static string FName(IFaction f)
        {
            try { return f != null && f.Name != null ? f.Name.ToString().Replace(',', ' ').Replace('(', ' ').Replace(')', ' ').Trim() : "?"; } catch { return "?"; }
        }

        // ------------------------------------------------------------ brama wroga
        internal static void OnWar(IFaction a, IFaction b, DeclareWarAction.DeclareWarDetail d) { _warVer++; }
        internal static void OnPeace(IFaction a, IFaction b, MakePeaceAction.MakePeaceDetail d) { _warVer++; }
        internal static void OnOwner(Settlement st, bool open, Hero newOwner, Hero oldOwner, Hero capturer, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail d) { _warVer++; }
        internal static void OnClanKingdom(Clan clan, Kingdom oldK, Kingdom newK, ChangeKingdomAction.ChangeKingdomActionDetail d, bool notify) { _warVer++; }

        /// <summary>Wrogie twierdze frakcji (zamki i miasta frakcji w wojnie z nia) - od nowa przy zmianie wersji wojen.</summary>
        private static List<Settlement> Forts(IFaction fac)
        {
            if (_fortVer != _warVer) { _forts.Clear(); _fortVer = _warVer; }
            List<Settlement> l;
            if (_forts.TryGetValue(fac, out l)) return l;
            l = new List<Settlement>();
            foreach (var st in Settlement.All)
            {
                if (st == null || !st.IsFortification) continue;
                var f = st.MapFaction;
                if (f != null && f != fac && f.IsAtWarWith(fac)) l.Add(st);
            }
            _forts[fac] = l;
            return l;
        }

        /// <summary>Wroga twierdza, pod ktorej brama biegnie droga wozu do miasta `to` (d - droga ladowa zrodlo -> miasto, z MarketCarts.Choose); null - brak albo klucz wylaczony.
        /// Zrodlo: osada, w ktorej woz stoi (`at`), albo woz w polu. Woz w domu - wynik z pamieci (wersja wojen).</summary>
        internal static Settlement GateBlocked(MobileParty cart, Settlement home, Settlement at, Settlement to, float d)
        {
            if (!GateOn || cart == null || to == null || !(d > 0f) || d >= CartTownExit.BkLimit) return null;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var fac = cart.MapFaction ?? (home != null ? home.MapFaction : null);
                if (fac == null) return null;
                bool memo = at != null && at == home;
                Dictionary<Settlement, Memo> m = null; Memo mm;
                if (memo)
                {
                    if (!_memo.TryGetValue(home, out m)) { m = new Dictionary<Settlement, Memo>(); _memo[home] = m; }
                    if (m.TryGetValue(to, out mm) && mm.Ver == _warVer) return mm.F;
                }
                Settlement hit = null;
                var forts = Forts(fac);
                if (forts.Count > 0)
                {
                    var model = Campaign.Current.Models.MapDistanceModel;
                    double tol = Math.Max(2.0, 0.02 * d), best = double.MaxValue;
                    foreach (var f in forts)
                    {
                        if (f == to || f == at) continue;
                        float a;
                        if (at != null) a = model.GetDistance(at, f, false, false, MobileParty.NavigationType.Default);
                        else { float lr; a = model.GetDistance(cart, f, false, MobileParty.NavigationType.Default, out lr); }
                        if (!(a >= 0f) || a >= d) continue;                               // wstepnie: twierdza blizej niz cel
                        float b = model.GetDistance(f, to, false, false, MobileParty.NavigationType.Default);
                        if (!(b >= 0f) || b >= CartTownExit.BkLimit) continue;
                        double det = a + b - d;
                        if (det <= tol && det < best) { best = det; hit = f; }
                    }
                }
                if (memo) m[to] = new Memo { Ver = _warVer, F = hit };
                return hit;
            }
            catch (Exception e) { Stumble("CartRisk.GateBlocked", e); return null; }
            finally { _ticks += Stopwatch.GetTimestamp() - t0; }
        }

        // ------------------------------------------------------------ linia doby (segment "ryzyko trasy")
        /// <summary>Segment linii "Wozy (m106): dzien" - pamiec tras i straty doby; zeruje liczniki doby. riskChanged - wybory zmienione przez ryzyko (MarketCarts).</summary>
        internal static void DayText(StringBuilder sb, int riskChanged, out double gateMs)
        {
            gateMs = _ticks * 1000.0 / Stopwatch.Frequency;
            int today = Today();
            int withData = 0;
            var worst = new List<KeyValuePair<double, string>>();
            double sw = 1.0;
            try
            {
                sw = WorldSurvival(today);
                foreach (var kv in _pairs)
                {
                    var r = kv.Value;
                    Decay(r, today);
                    if (r.N < 1.0) continue;
                    withData++;
                    if (r.N < 2.0) continue;
                    double s = (r.A + Prior * sw) / (r.N + Prior);
                    worst.Add(new KeyValuePair<double, string>(s, kv.Key.Replace("|", "->") + " s=" + s.ToString("0.00", Inv) + " n=" + r.N.ToString("0.0", Inv)));
                }
                worst.Sort((x, y) => x.Key.CompareTo(y.Key));
            }
            catch (Exception e) { Stumble("CartRisk.DayText", e); }
            var top = new List<KeyValuePair<string, int>>(_lostBy);
            top.Sort((x, y) => y.Value != x.Value ? y.Value.CompareTo(x.Value) : string.CompareOrdinal(x.Key, y.Key));
            sb.Append(" | ryzyko trasy").Append(On ? "" : " - WYLACZONE").Append(": par ").Append(_pairs.Count).Append(" (z danymi ").Append(withData).Append(")")
              .Append(", dojechalo ").Append(_arr).Append(", rozbite ").Append(_lost).Append(" (bandy ").Append(_lostBand).Append(", wrogowie ").Append(_lostEnemy)
              .Append(", w kursie do miasta ").Append(_lostTrip).Append("; top: ");
            if (top.Count == 0) sb.Append('-');
            for (int i = 0; i < top.Count && i < 8; i++) sb.Append(i > 0 ? ", " : "").Append(top[i].Key).Append(' ').Append(top[i].Value);
            sb.Append("), przezywalnosc swiata ").Append(sw.ToString("0.000", Inv)).Append(", wybor zmieniony przez ryzyko ").Append(riskChanged).Append(", najgorsze trasy ");
            if (worst.Count == 0) sb.Append('-');
            for (int i = 0; i < worst.Count && i < 3; i++) sb.Append(i > 0 ? ", " : "").Append(worst[i].Value);
            ClearDay();
        }

        /// <summary>CartGuard.Daily przy wszystkich kluczach W2 wylaczonych (bez linii doby): liczniki doby od zera, kursy w toku - porzadki jak co dobe.</summary>
        internal static void SkipDay() { Tidy(); ClearDay(); }

        internal static int StumblesDay { get { return _stumbles; } }
        internal static double WorldNow() { try { return WorldSurvival(Today()); } catch { return 1.0; } }

        // ------------------------------------------------------------ zapis (arm_cart_risk)
        // m106r1~doba zapisu~Aw10:Nw10~region|miasto:A10:N10:doba kursu!... (A i N x 10, wygaszone na dobe zapisu)
        /// <summary>Napis do zapisu. Klucz wylaczony - napis wczytany bez zmian (null - nic nie zapisujemy). Przycina pary: N po wygaszeniu &lt; 0.1, bez kursu od 364 dob,
        /// ponad 1500 (zostaja z najwiekszym N). Linia "Wozy (m106): zapis pamieci tras".</summary>
        internal static string Export()
        {
            if (!On) return _raw;
            try
            {
                int today = Today();
                Decay(_world, today);
                var keep = new List<KeyValuePair<string, Rec>>(_pairs.Count);
                int cut = 0;
                foreach (var kv in _pairs)
                {
                    var r = kv.Value;
                    Decay(r, today);
                    if (r.N < 0.1 || today - r.Touch > MaxAge) { cut++; continue; }
                    keep.Add(kv);
                }
                if (keep.Count > MaxPairs)
                {
                    keep.Sort((x, y) => y.Value.N.CompareTo(x.Value.N));
                    cut += keep.Count - MaxPairs;
                    keep.RemoveRange(MaxPairs, keep.Count - MaxPairs);
                }
                var sb = new StringBuilder(64 + keep.Count * 40);
                sb.Append(Fmt).Append('~').Append(today).Append('~').Append(E10(_world.A)).Append(':').Append(E10(_world.N)).Append('~');
                for (int i = 0; i < keep.Count; i++)
                {
                    if (i > 0) sb.Append('!');
                    var r = keep[i].Value;
                    sb.Append(keep[i].Key).Append(':').Append(E10(r.A)).Append(':').Append(E10(r.N)).Append(':').Append(r.Touch);
                }
                string s = sb.ToString();
                Log.Info("Wozy (m106): zapis pamieci tras arm_cart_risk - par " + keep.Count + ", znakow " + s.Length + ", przyciete " + cut + ".");
                return s;
            }
            catch (Exception e) { Stumble("CartRisk.Export", e); return _raw; }
        }

        private static string E10(double v) { return Math.Max(0L, (long)Math.Round(v * 10.0)).ToString(Inv); }

        /// <summary>Odczyt zapisu (zawsze - takze przy wylaczonym kluczu; napis zostaje do oddania bez zmian). Zly format - pamiec pusta.</summary>
        internal static void Import(string t)
        {
            _pairs.Clear(); _world.A = _world.N = 0; _world.Day = _world.Touch = 0; _trips.Clear();
            _raw = t; _loaded = -1;
            if (string.IsNullOrEmpty(t)) return;
            try
            {
                var p = t.Split('~');
                if (p.Length < 4 || p[0] != Fmt) { _loaded = 0; return; }
                int day; if (!int.TryParse(p[1], NumberStyles.Integer, Inv, out day)) { _loaded = 0; return; }
                var w = p[2].Split(':');
                double a, n;
                if (w.Length == 2 && double.TryParse(w[0], NumberStyles.Float, Inv, out a) && double.TryParse(w[1], NumberStyles.Float, Inv, out n))
                { _world.A = a / 10.0; _world.N = n / 10.0; _world.Day = day; _world.Touch = day; }
                int cnt = 0;
                if (p[3].Length > 0)
                    foreach (var e in p[3].Split('!'))
                    {
                        // klucz zawiera "|", pola liczb na koncu (ostatnie trzy dwukropki)
                        int c3 = e.LastIndexOf(':'); if (c3 <= 0) continue;
                        int c2 = e.LastIndexOf(':', c3 - 1); if (c2 <= 0) continue;
                        int c1 = e.LastIndexOf(':', c2 - 1); if (c1 <= 0) continue;
                        double ra, rn; int touch;
                        if (!double.TryParse(e.Substring(c1 + 1, c2 - c1 - 1), NumberStyles.Float, Inv, out ra) || !double.TryParse(e.Substring(c2 + 1, c3 - c2 - 1), NumberStyles.Float, Inv, out rn)
                            || !int.TryParse(e.Substring(c3 + 1), NumberStyles.Integer, Inv, out touch)) continue;
                        _pairs[e.Substring(0, c1)] = new Rec { A = ra / 10.0, N = rn / 10.0, Day = day, Touch = touch };
                        cnt++;
                    }
                _loaded = cnt;
            }
            catch (Exception e) { _pairs.Clear(); _loaded = 0; Stumble("CartRisk.Import", e); }
        }

        /// <summary>Porzadki dobowe: kursy wozow nieczynnych albo starszych niz 60 dob (woz nie dojechal i nie zginal - gra albo BK zmienily cel).</summary>
        internal static void Tidy()
        {
            try
            {
                if (_trips.Count == 0) return;
                int today = Today();
                List<MobileParty> gone = null;
                foreach (var kv in _trips) if (kv.Key == null || !kv.Key.IsActive || today - kv.Value.Day > 60) (gone ?? (gone = new List<MobileParty>())).Add(kv.Key);
                if (gone != null) foreach (var mp in gone) _trips.Remove(mp);
            }
            catch (Exception e) { Stumble("CartRisk.Tidy", e); }
        }
    }
}
