using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// E4/9 (192, PLAN 4.15-4.17, 4.19; plan kodu docs/etap4/PLAN-KODU-E49.md): KRAINY KANONU - czesc ksiegi ludzi (U23: wlasciciel stanu = PeopleBook).
    ///  - konto NIEWOLNI ("nw:" w "arm_people"): region trzymania (warownia osady niewoli) x region pochodzenia -> ludzie (double; "" dozwolone). Wejscie: sprzedaz jencow
    ///    i patrol BK w krainie z niewola (EnslaveShares -> EnslaveAt), bilans otwarcia NK - Zelazne Wyspy 27 tys. jako czesc 500 tys. tabeli (SeedThralls z Calibrate,
    ///    "ns:"). Bez narodzin, bez wyzwolen; smierc ThrallYearlyLossPercent/rok (ThrallsDaily - TDead regionu pochodzenia). Konto liczy sie do rownania regionu POCHODZENIA;
    ///    trzymani w krainie podnosza prog wolnych rak Zelaznych Wysp (FreeHands);
    ///  - konto MUR ("mw:"): ludzie przy Murze poza sluzba - skazancy I1 (WallShares) i bracia, ktorzy po rozbiciu, przepelnieniu, rozwiazaniu albo zwolnieniu wrocili
    ///    na Mur zamiast do domu (WatchHome - sluzba dozywotnia U5). Pierwsi w kolejce rekruta Strazy (WatchLaw.Levy: Mur, pule wyrzutkow Westeros, ludnosc Westeros);
    ///  - liczniki Strazy (slowniki skladu KWatch: wejscia, wyjscia, uzgodnienie, znikniecie, zmiana rodzaju partii) - kontrola linii "Straz z Westeros (E4/9)";
    ///  - konto UCHODZCY -> pojmani z rabunku (RefugeesToCaptives, ThrallLaw.CapturePrefix).
    /// Kazde wejscie we wlasnym try, potkniecia liczone (OStumble, bez globalnych wylacznikow). Watek glowny kampanii (sprzedaz, patrol, koniec rabunku, doba).
    /// </summary>
    internal static partial class PeopleBook
    {
        // ================================================================ stan w zapisie
        private static readonly Dictionary<string, Dictionary<string, double>> _thr = new Dictionary<string, Dictionary<string, double>>();   // trzymanie -> pochodzenie -> ludzie
        private static readonly Dictionary<string, double> _wall = new Dictionary<string, double>();                                            // pochodzenie -> ludzie przy Murze
        private static bool _thrSeeded;           // bilans otwarcia niewolnych zalozony (NK z E4/9); stary zapis - false (prog Zelaznych z BK 169c)
        private static double _thrOpening;        // ile dal bilans otwarcia

        // ================================================================ doba i sesja (bez zapisu)
        private static double _tSale, _tPatrol, _tDead, _tOutside, _tSeedDay;
        private static readonly HashSet<string> _tSaleHolds = new HashSet<string>();
        private static readonly Dictionary<string, double> _tDeadRealm = new Dictionary<string, double>();
        private static double _t0;                // stan konta NIEWOLNI z konca poprzedniej doby (kontrola linii "Niewolni")
        private static int _canonBad;             // wpisy nie zapisane (znaki w id)
        private static string _canonLoadNote;     // linia "z zapisu" (raz, w pierwszej dobie po wczytaniu)
        // Straz: liczniki slownikow KWatch (kontrola) i drogi na Mur
        private static double _wIn, _wOut, _wQ, _wGone, _wDeadAll, _wDeadKnown, _wWoodsN, _wWoodsO, _wConvicts, _wWallOut, _wHomeNotWall;
        private static readonly double[] _wOutBy = new double[FKinds];
        internal const int WvH3 = 0, WvW4c = 1, WvDisband = 2, WvR6 = 3, Wv166 = 4, WvMilitia = 5, WvOther = 6, WVia = 7;
        private static readonly double[] _wBack = new double[WVia];
        private static double _wS0 = double.NaN;
        private static Dictionary<PartyBase, double> _wPrevAll;
        private static HashSet<PartyBase> _wPrevWatch;

        private static void ResetCanon()
        {
            _thr.Clear(); _wall.Clear(); _thrSeeded = false; _thrOpening = 0;
            _tSale = _tPatrol = _tDead = _tOutside = _tSeedDay = 0; _tSaleHolds.Clear(); _tDeadRealm.Clear(); _t0 = 0;
            _canonBad = 0; _canonLoadNote = null;
            _wIn = _wOut = _wQ = _wGone = _wDeadAll = _wDeadKnown = _wWoodsN = _wWoodsO = _wConvicts = _wWallOut = _wHomeNotWall = 0;
            Array.Clear(_wOutBy, 0, _wOutBy.Length); Array.Clear(_wBack, 0, WVia);
            _wS0 = double.NaN; _wPrevAll = null; _wPrevWatch = null;
            LastWatch = default(WatchDay);
        }

        // ================================================================ zapis ("|ns:1;<bilans>|nw:<trzymanie>=<pochodzenie>:<n>;...,...|mw:<pochodzenie>=<n>,...")
        private static void ExportCanon(StringBuilder sb)
        {
            int bad = 0;
            if (_thrSeeded) sb.Append("|ns:1;").Append(N3(_thrOpening));
            bool first = true;
            foreach (var h in _thr)
            {
                double sum = 0; foreach (var v in h.Value.Values) sum += v;
                if (sum <= Eps) continue;
                string hk = h.Key.Length == 0 ? "~" : h.Key;
                if (hk != "~" && BadKey(hk)) { bad++; continue; }
                sb.Append(first ? "|nw:" : ",").Append(hk).Append('=');
                first = false;
                bool f2 = true;
                foreach (var o in h.Value)
                {
                    if (o.Value <= Eps) continue;
                    string ok = o.Key.Length == 0 ? "~" : o.Key;
                    if (ok != "~" && BadKey(ok)) { bad++; continue; }
                    sb.Append(f2 ? "" : ";").Append(ok).Append(':').Append(N3(o.Value));
                    f2 = false;
                }
            }
            first = true;
            foreach (var w in _wall)
            {
                if (w.Value <= Eps) continue;
                string ok = w.Key.Length == 0 ? "~" : w.Key;
                if (ok != "~" && BadKey(ok)) { bad++; continue; }
                sb.Append(first ? "|mw:" : ",").Append(ok).Append('=').Append(N3(w.Value));
                first = false;
            }
            _canonBad = bad;
        }

        /// <summary>Import: jedna sekcja E4/9 ("ns:", "nw:", "mw:"); false = nie nasza. Smieci (NaN, ujemne, nieskonczone) - wpis pominiety.</summary>
        private static bool ImportCanonPart(string part)
        {
            string p2 = part.Substring(0, 3);
            if (p2 != "ns:" && p2 != "nw:" && p2 != "mw:") return false;
            try
            {
                string body = part.Substring(3);
                if (p2 == "ns:")
                {
                    var a = body.Split(';');
                    _thrSeeded = a.Length > 0 && a[0] == "1";
                    _thrOpening = Num(a, 1);
                }
                else if (p2 == "nw:")
                {
                    foreach (var item in body.Split(','))
                    {
                        int eq = item.IndexOf('=');
                        if (eq <= 0) continue;
                        string hold = item.Substring(0, eq); if (hold == "~") hold = "";
                        Dictionary<string, double> d = null;
                        foreach (var kv in item.Substring(eq + 1).Split(';'))
                        {
                            int c = kv.LastIndexOf(':');
                            if (c <= 0) continue;
                            double n; if (!double.TryParse(kv.Substring(c + 1), NumberStyles.Float, Inv, out n) || !(n > 0.0) || double.IsInfinity(n)) continue;
                            string o = kv.Substring(0, c); if (o == "~") o = "";
                            if (d == null && !_thr.TryGetValue(hold, out d)) { d = new Dictionary<string, double>(); _thr[hold] = d; }
                            double v; d.TryGetValue(o, out v); d[o] = v + n;
                        }
                    }
                }
                else
                {
                    foreach (var item in body.Split(','))
                    {
                        int eq = item.LastIndexOf('=');
                        if (eq <= 0) continue;
                        double n; if (!double.TryParse(item.Substring(eq + 1), NumberStyles.Float, Inv, out n) || !(n > 0.0) || double.IsInfinity(n)) continue;
                        string o = item.Substring(0, eq); if (o == "~") o = "";
                        double v; _wall.TryGetValue(o, out v); _wall[o] = v + n;
                    }
                }
            }
            catch (Exception e) { OStumble("ImportCanonPart(" + p2 + ")", e); }
            return true;
        }

        private static void CanonAfterImport()
        {
            double t = ThrallsTotal(), w = WallTotal;
            _t0 = t;
            var origins = new HashSet<string>();
            foreach (var h in _thr.Values) foreach (var o in h.Keys) if (o.Length > 0) origins.Add(o);
            _canonLoadNote = "Krainy kanonu (E4/9): z zapisu - niewolni " + N0(t) + " w " + _thr.Count + " regionach trzymania (z " + origins.Count + " regionow pochodzenia), bilans otwarcia "
                             + (_thrSeeded ? "TAK (" + N0(_thrOpening) + ")" : "BRAK (stary zapis - prog Zelaznych z BK 169c)") + ", Mur " + N0(w) + "; nie zapisane " + _canonBad + ".";
        }

        /// <summary>WatchLaw.Daily: linia "z zapisu" (raz po wczytaniu) - kopiuje i zeruje.</summary>
        internal static string TakeCanonLoadNote() { var s = _canonLoadNote; _canonLoadNote = null; return s; }

        // ================================================================ konto NIEWOLNI
        internal static bool ThrallsSeeded { get { return _thrSeeded; } }
        internal static double ThrallsOpening { get { return _thrOpening; } }

        internal static double ThrallsTotal()
        {
            double t = 0; foreach (var h in _thr.Values) foreach (var v in h.Values) t += v;
            return t;
        }

        /// <summary>E4/9 (5.1): s ludzi do konta NIEWOLNI w regionie trzymania (osada niewoli); pochodzenie z udzialow (z "" wlacznie). via: 0 sprzedaz, 1 patrol BK.</summary>
        private static void EnslaveAt(Settlement holdRegion, Shares s, int via)
        {
            if (s == null || !(s.Total > Eps)) return;
            string h = RegKey(holdRegion);
            Dictionary<string, double> d;
            if (!_thr.TryGetValue(h, out d)) { d = new Dictionary<string, double>(); _thr[h] = d; }
            foreach (var kv in s.M) { if (!(kv.Value > 0.0)) continue; double v; d.TryGetValue(kv.Key, out v); d[kv.Key] = v + kv.Value; }
            if (via == 1) _tPatrol += s.Total; else { _tSale += s.Total; _tSaleHolds.Add(h); }
        }

        private static void ThrallOutside(double n) { if (n > 0.0) _tOutside += n; }

        /// <summary>E4/9 (5.2): smierc niewolnych - rate = udzial dzienny (ThrallYearlyLossPercent / 100 / dni roku) na kazda komorke; TDead regionu pochodzenia
        /// (rownanie). Bez narodzin. Zwraca zmarlych.</summary>
        internal static double ThrallsDaily(double rate)
        {
            if (!(rate > 0.0) || double.IsInfinity(rate) || _thr.Count == 0) return 0.0;
            double dead = 0.0;
            try
            {
                foreach (var hold in _thr.Keys.ToList())
                {
                    var d = _thr[hold];
                    string realm = "?";
                    try { var node = Node(hold); realm = node != null ? FreeHands.RealmOf(node) : "?"; } catch { realm = "?"; }
                    double dh = 0.0;
                    foreach (var o in d.Keys.ToList())
                    {
                        double t = d[o], x = t * rate;
                        double left = t - x;
                        if (left < Eps) { x = t; d.Remove(o); } else d[o] = left;
                        dh += x;
                        if (o.Length > 0 && _sown) Rg(o).TDead += x;
                    }
                    if (d.Count == 0) _thr.Remove(hold);
                    dead += dh;
                    double v; _tDeadRealm.TryGetValue(realm, out v); _tDeadRealm[realm] = v + dh;
                }
            }
            catch (Exception e) { OStumble("ThrallsDaily", e); }
            _tDead += dead;
            return dead;
        }

        /// <summary>
        /// E4/9 (5.4, U15b): bilans otwarcia NK - `total` niewolnych Zelaznych Wysp (czesc 500 tys. tabeli, ktora PopulationLaw.Calibrate wlasnie odjela od ludzi wsi
        /// i miast wysp - to samo wywolanie). Rozklad na regiony wysp wedlug ludzi (wsie i miasta kultury sturgia), pochodzenie = region wysp. Brak regionow (nie
        /// powinno sie zdarzyc - Calibrate odejmuje tylko przy wsiach wysp) - komorka bez regionu (stan zostaje w ksiedze). Nie rzuca.
        /// </summary>
        internal static void SeedThralls(double total)
        {
            if (!(total > 0.0) || double.IsInfinity(total)) return;
            var w = new Dictionary<string, double>(); double all = 0.0;
            var names = new Dictionary<string, string>();
            try
            {
                foreach (var st in Settlement.All)
                {
                    try
                    {
                        if (st == null || st.Culture == null || st.Culture.StringId != "sturgia" || !(st.IsVillage || st.IsTown)) continue;
                        var reg = OutlawLaw.RegionFor(st);
                        string k = RegKey(reg);
                        if (k.Length == 0) continue;
                        double p = PopulationLaw.PeopleOf(st);
                        if (!(p > 0.0) || double.IsInfinity(p)) p = 1.0;
                        double v; w.TryGetValue(k, out v); w[k] = v + p; all += p;
                        if (!names.ContainsKey(k)) names[k] = reg.Name != null ? reg.Name.ToString() : k;
                    }
                    catch (Exception e) { OStumble("SeedThralls(osada)", e); }
                }
            }
            catch (Exception e) { OStumble("SeedThralls", e); }
            var parts = new List<string>();
            if (all > 0.0)
            {
                foreach (var kv in w.OrderByDescending(x => x.Value))
                {
                    double n = total * kv.Value / all;
                    Dictionary<string, double> d;
                    if (!_thr.TryGetValue(kv.Key, out d)) { d = new Dictionary<string, double>(); _thr[kv.Key] = d; }
                    double v; d.TryGetValue(kv.Key, out v); d[kv.Key] = v + n;
                    parts.Add(names[kv.Key] + " " + N0(n));
                }
            }
            else
            {
                Dictionary<string, double> d;
                if (!_thr.TryGetValue("", out d)) { d = new Dictionary<string, double>(); _thr[""] = d; }
                double v; d.TryGetValue("", out v); d[""] = v + total;
                parts.Add("bez regionu (brak osad kultury sturgia) " + N0(total));
            }
            _thrSeeded = true; _thrOpening = total; _tSeedDay += total;
            try
            {
                var s = Settings.Current;
                float scale = s != null ? Math.Max(0f, s.PopulationScale) : 1f;
                Log.Info("Niewolni (E4/9): bilans otwarcia nowej kampanii - Zelazne Wyspy " + N0(total) + " (czesc tabeli " + N0(500000.0 * scale) + ": wsie i miasta wysp "
                         + N0(500000.0 * scale - total) + ") w " + Math.Max(1, w.Count) + " regionach: " + string.Join(", ", parts.ToArray()) + ".");
            }
            catch { }
        }

        /// <summary>Niewolni trzymani w regionach danej krainy (kultura regionu z dnia kalibracji) - prog wolnych rak Zelaznych Wysp, linie.</summary>
        internal static double ThrallsHeldInRealm(string realm)
        {
            double t = 0.0;
            try
            {
                foreach (var h in _thr)
                {
                    if (h.Key.Length == 0) continue;
                    var node = Node(h.Key);
                    if (node == null || FreeHands.RealmOf(node) != realm) continue;
                    foreach (var v in h.Value.Values) t += v;
                }
            }
            catch (Exception e) { OStumble("ThrallsHeldInRealm", e); }
            return t;
        }

        /// <summary>Niewolni trzymani w regionie (id warowni).</summary>
        private static double ThrallsHeldIn(string regionId)
        {
            Dictionary<string, double> d;
            if (regionId == null || !_thr.TryGetValue(regionId, out d)) return 0.0;
            double t = 0.0; foreach (var v in d.Values) t += v;
            return t;
        }

        /// <summary>Linia "Niewolni": stan wedlug krainy trzymania i wedlug pochodzenia (top), bez pochodzenia.</summary>
        internal static void ThrallsByRealm(Dictionary<string, double> held, Dictionary<string, double> origin, out double unknown)
        {
            unknown = 0.0;
            try
            {
                foreach (var h in _thr)
                {
                    string realm = "?";
                    if (h.Key.Length > 0) { var node = Node(h.Key); realm = node != null ? FreeHands.RealmOf(node) : "?"; }
                    foreach (var o in h.Value)
                    {
                        if (held != null) { double v; held.TryGetValue(realm, out v); held[realm] = v + o.Value; }
                        if (o.Key.Length == 0) { unknown += o.Value; continue; }
                        if (origin != null)
                        {
                            var on = Node(o.Key); string orr = on != null ? FreeHands.RealmOf(on) : "?";
                            double v; origin.TryGetValue(orr, out v); origin[orr] = v + o.Value;
                        }
                    }
                }
            }
            catch (Exception e) { OStumble("ThrallsByRealm", e); }
        }

        internal struct ThrallDay { public double T, T0, Sale, Patrol, Dead, Outside, Seed; public int SaleHolds; public Dictionary<string, double> DeadRealm; public double Check { get { return T - T0 - (Sale + Patrol + Seed - Dead); } } }

        /// <summary>ThrallLaw.Daily (po ThrallsDaily): odcinek doby konta NIEWOLNI - kopiuje i zeruje liczniki; stan do kontroli jutra.</summary>
        internal static ThrallDay TakeThrallDay()
        {
            var g = new ThrallDay { T = ThrallsTotal(), T0 = _t0, Sale = _tSale, Patrol = _tPatrol, Dead = _tDead, Outside = _tOutside, Seed = _tSeedDay, SaleHolds = _tSaleHolds.Count,
                                    DeadRealm = new Dictionary<string, double>(_tDeadRealm) };
            _t0 = g.T;
            _tSale = _tPatrol = _tDead = _tOutside = _tSeedDay = 0; _tSaleHolds.Clear(); _tDeadRealm.Clear();
            return g;
        }

        // ================================================================ konto MUR
        internal static double WallTotal { get { double t = 0; foreach (var v in _wall.Values) t += v; return t; } }

        private static void WallAddOne(string k, double m)
        {
            if (!(m > 0.0) || double.IsInfinity(m)) return;
            k = k ?? "";
            double v; _wall.TryGetValue(k, out v); _wall[k] = v + m;
        }

        /// <summary>Ludzie z udzialami do konta MUR (z "" wlacznie). Zwraca, ilu dopisano.</summary>
        private static double WallAdd(Shares s)
        {
            if (s == null) return 0.0;
            foreach (var kv in s.M) WallAddOne(kv.Key, kv.Value);
            return s.Total;
        }

        /// <summary>WatchLaw.Levy krok 1: do n ludzi z konta MUR proporcjonalnie do skladu (z "" - brat bez pochodzenia wraca jako ""), bez limitu dziennego.</summary>
        internal static Shares WallTake(double n)
        {
            var r = new Shares();
            if (!(n > Eps) || double.IsInfinity(n) || _wall.Count == 0) return r;
            try
            {
                double tot = WallTotal;
                if (!(tot > Eps)) return r;
                double take = Math.Min(n, tot), f = take / tot;
                foreach (var k in _wall.Keys.ToList())
                {
                    double m = f >= 1.0 - 1e-12 ? _wall[k] : _wall[k] * f;
                    r.Add(k, m);
                    double left = _wall[k] - m;
                    if (left > Eps) _wall[k] = left; else _wall.Remove(k);
                }
                _wWallOut += r.Total;
            }
            catch (Exception e) { OStumble("WallTake", e); }
            return r;
        }

        /// <summary>
        /// E4/9 (4.4, U5 - sluzba dozywotnia): czesc udzialow zdjeta ze sluzby Strazy (s.W) - do konta MUR (bez Move, bez hearth; z "" wlacznie), zdjeta z `s`
        /// (w s zostaja ludzie spoza Strazy). Przy wylaczonym WatchLaw - nic (licznik "do domu z hearth"), wolajacy prowadzi ich jak dotad. Zwraca umieszczonych.
        /// </summary>
        private static double WatchHome(Shares s, int cause, bool arrive)
        {
            if (s == null || !(s.W > Eps) || !(s.Total > Eps)) return 0.0;
            if (!WatchLaw.Live) { _wHomeNotWall += Math.Min(s.W, s.Total); s.W = 0.0; return 0.0; }
            double f = Math.Min(1.0, s.W / s.Total), placed = 0.0;
            foreach (var k in s.M.Keys.ToList())
            {
                double m = s.M[k] * f;
                if (!(m > 0.0)) continue;
                WallAddOne(k, m); placed += m;
                double left = s.M[k] - m;
                if (left > Eps) s.M[k] = left; else s.M.Remove(k);
            }
            s.Total = 0.0; foreach (var x in s.M.Values) s.Total += x;
            s.W = 0.0;
            _dest[OWall] += placed;
            int via = cause == MBackH3 ? WvH3 : cause == MBackW4c ? WvW4c : cause == MBackDisband ? (arrive ? WvR6 : WvDisband) : cause == MBack166 ? Wv166
                      : cause == MBackMilitia ? WvMilitia : WvOther;
            _wBack[via] += placed;
            return placed;
        }

        // ================================================================ Straz: kto sluzy, liczniki slownikow
        /// <summary>Roster sluzby Strazy: partia lorda i zaloga Strazy (KWatch), gracz w Strazy, milicja osady Strazy (frakcja milicji = frakcja osady - ten sam test co
        /// rekrut milicji przez WatchLaw.Levy). Karawany nie: wlasciciel z frakcji Strazy nie znaczy, ze ludzie sa ze Strazy (rekrut karawany - wedlug osady startu).</summary>
        private static bool WatchServes(PartyBase owner, int kind)
        {
            if (kind == KWatch) return true;
            if (kind != KPlayer && kind != KMilitia) return false;
            if (owner == null || !owner.IsMobile || owner.MobileParty == null) return false;
            return CrownGifts.IsWatch(owner.MobileParty.MapFaction);
        }

        /// <summary>E4/9 K8 krok 1: wpis partii do linii diagnostyki (najwyzej 12 wpisow).</summary>
        private static void KindNote(ref StringBuilder sb, ref int n, PartyBase pb, string what, double before, double now)
        {
            try
            {
                if (n >= 12) return;
                if (sb == null) sb = new StringBuilder(512); else sb.Append("; ");
                string nm = pb != null && pb.MobileParty != null && pb.MobileParty.Name != null ? pb.MobileParty.Name.ToString() : (pb != null && pb.Name != null ? pb.Name.ToString() : "?");
                string fac = pb != null && pb.MapFaction != null && pb.MapFaction.StringId != null ? pb.MapFaction.StringId : "-";
                sb.Append(nm).Append(" [").Append(fac).Append("] ").Append(what).Append(' ').Append(N3(before)).Append(" -> ").Append(N3(now));
                n++;
            }
            catch { }
        }

        /// <summary>E4/9 K9 (diagnoza biegu zbiorczego 329f2c7): ten sam predykat co znacznik W w Take - rodzaj partii z pamieci doby (KindOf) + WatchServes, czyli
        /// takze milicja osady Strazy i partia, ktora zmienila frakcje w trakcie doby. Dla regionu "do lasu" (WatchLaw.DeserterRegion) przy czynnym pochodzeniu.</summary>
        internal static bool ServesWatch(MobileParty mp)
        {
            try { return mp != null && mp.Party != null && WatchServes(mp.Party, KindOf(mp)); }
            catch { return false; }
        }

        private static void WatchOut(double take, int cause)
        {
            _wOut += take;
            if (cause >= 0 && cause < FKinds) _wOutBy[cause] += take;
        }

        private static void WatchDied(Shares s)
        {
            double f = s.Total > Eps ? Math.Min(1.0, s.W / s.Total) : 0.0;
            _wDeadAll += s.Total * f; _wDeadKnown += s.Known * f;
        }

        private static void WatchToWoods(Settlement region, double men)
        {
            try { if (region != null && FreeHands.RealmOf(region) == WatchLaw.NorthRealm) _wWoodsN += men; else _wWoodsO += men; }
            catch { _wWoodsO += men; }
        }

        /// <summary>ReconcileRoster (Z9): nadwyzka rosteru Strazy - rekrut przez WatchLaw.Levy (kanal "uzgodnienie"); udzialy do slownika, wejscia nazwane FWatch*.
        /// Zwraca, ilu dopisano (Levy zawsze pokrywa calosc - reszta jako "" bez pokrycia).</summary>
        private static double ReconcileWatch(Org o, double add)
        {
            var lv = WatchLaw.Levy(add, WatchLaw.KReconcile);
            double g = 0.0;
            g += AddPart(o, lv.Wall, FWatchWall);
            g += AddPart(o, lv.Pool, FWatchPool);
            g += AddPart(o, lv.People, FWatchLevy);
            g += AddPart(o, lv.None, FNoneWatchLeft);
            return g;
        }

        private static double AddPart(Org o, Shares s, int cause)
        {
            if (o == null || s == null || !(s.Total > Eps)) return 0.0;
            foreach (var kv in s.M) if (kv.Value > 0.0) OrgAdd(o, kv.Key, kv.Value);
            _fIn[cause] += s.Total; _gAll += s.Total; _wIn += s.Total;
            return s.Total;
        }

        /// <summary>Odcinek doby Strazy (zamkniety w PeopleBook.Daily przed koncem doby ksiegi - rodzaj partii z tej samej pamieci doby co ruch).</summary>
        internal struct WatchDay
        {
            public bool Has, First;
            public double S, S0, Known, Unknown, Par, Gar;
            public double In, Out, Q, Gone, KindIn, KindOut;
            public double DeadAll, DeadKnown, Plague, Captured, Routed, Deserted, WoodsN, WoodsO;
            public double Convicts, WallOut, WallNow, HomeNotWall;
            public double[] Back;
            public double Check { get { return First ? 0.0 : S - S0 - (In - Out - Q - Gone) - KindIn + KindOut; } }
        }
        internal static WatchDay LastWatch;

        /// <summary>PeopleBook.Daily, po uzgodnieniu i liniach, PRZED OriginEndDay (rodzaj partii z pamieci tej doby): stan slownikow Strazy, kontrola, zerowanie licznikow.</summary>
        private static void CanonEndDay()
        {
            var g = new WatchDay { Has = true, Back = new double[WVia] };
            StringBuilder kd = null; int kdN = 0, kdNew = 0; double kdNewMen = 0;   // E4/9 K8 krok 1 (diagnostyka, tylko log): partie ze zmiana rodzaju i nowe partie Strazy
            try
            {
                if (_sown && OriginOn)
                {
                    var cur = new Dictionary<PartyBase, double>(_mem.Count);
                    var watch = new HashSet<PartyBase>();
                    foreach (var kv in _mem)
                    {
                        var pb = kv.Key; var o = kv.Value;
                        if (pb == null || o == null) continue;
                        cur[pb] = o.Sum;
                        if (!pb.IsMobile || pb.MobileParty == null || KindOf(pb.MobileParty) != KWatch) continue;
                        watch.Add(pb);
                        double u; o.M.TryGetValue("", out u);
                        g.S += o.Sum; g.Unknown += u; g.Known += o.Sum - u;
                        if (pb.MobileParty.IsGarrison) g.Gar += o.Sum; else g.Par += o.Sum;
                    }
                    if (_wPrevAll != null && _wPrevWatch != null)
                    {
                        foreach (var pb in _wPrevWatch)
                        {
                            bool still = true;
                            try { still = pb.MobileParty != null && KindOf(pb.MobileParty) == KWatch; } catch { still = true; }
                            if (!still)
                            {
                                double v; if (_wPrevAll.TryGetValue(pb, out v)) g.KindOut += v;
                                double nowV; cur.TryGetValue(pb, out nowV);
                                KindNote(ref kd, ref kdN, pb, "wyszla ze Strazy", v, nowV);
                            }
                        }
                        foreach (var pb in watch)
                            if (!_wPrevWatch.Contains(pb))
                            {
                                double v, nowV; cur.TryGetValue(pb, out nowV);
                                if (_wPrevAll.TryGetValue(pb, out v)) { g.KindIn += v; KindNote(ref kd, ref kdN, pb, "weszla do Strazy", v, nowV); }
                                else { kdNew++; kdNewMen += nowV; if (kdN < 8) KindNote(ref kd, ref kdN, pb, "nowa (bez wczoraj)", 0.0, nowV); }
                            }
                    }
                    _wPrevAll = cur; _wPrevWatch = watch;
                    g.S0 = _wS0; g.First = double.IsNaN(_wS0);
                    _wS0 = g.S;
                }
                else { g.First = true; _wS0 = double.NaN; _wPrevAll = null; _wPrevWatch = null; }
            }
            catch (Exception e) { OStumble("CanonEndDay", e); g.First = true; _wS0 = double.NaN; }
            g.In = _wIn; g.Out = _wOut; g.Q = _wQ; g.Gone = _wGone; g.DeadAll = _wDeadAll; g.DeadKnown = _wDeadKnown;
            g.Plague = _wOutBy[FDiePlague]; g.Captured = _wOutBy[FCaptured]; g.Routed = _wOutBy[FRouted]; g.Deserted = _wOutBy[FDesert];
            g.WoodsN = _wWoodsN; g.WoodsO = _wWoodsO; g.Convicts = _wConvicts; g.WallOut = _wWallOut; g.WallNow = WallTotal; g.HomeNotWall = _wHomeNotWall;
            Array.Copy(_wBack, g.Back, WVia);
            LastWatch = g;
            // E4/9 K8 krok 1 (diagnoza biegu zbiorczego 329f2c7: reszta kontroli Strazy 24 w jedynej dobie ze zmiana rodzaju i nowa partia naraz): przy KindIn/KindOut
            // != 0 - partie (wczoraj / dzis w slowniku), nowe partie Strazy i reszta kontroli. Tylko log; liczby kontroli bez zmian
            try
            {
                var st = Settings.Current;
                if (kd != null && (g.KindIn != 0.0 || g.KindOut != 0.0) && st != null && st.LogEnabled)
                    Log.Info("Straz K8 (E4/9 diagnostyka): dzien " + (int)CampaignTime.Now.ToDays + " | zmiana rodzaju: weszlo " + N3(g.KindIn) + ", wyszlo " + N3(g.KindOut)
                             + "; wejscia doby " + N3(g.In) + ", wyjscia " + N3(g.Out) + "; nowe partie Strazy " + kdNew + " (" + N3(kdNewMen) + " ludzi w slowniku)"
                             + "; reszta kontroli " + N3(g.Check) + " | partie (wczoraj -> dzis): " + kd.ToString());
            }
            catch (Exception e) { OStumble("CanonEndDay(K8)", e); }
            _wIn = _wOut = _wQ = _wGone = _wDeadAll = _wDeadKnown = _wWoodsN = _wWoodsO = _wConvicts = _wWallOut = _wHomeNotWall = 0;
            Array.Clear(_wOutBy, 0, _wOutBy.Length); Array.Clear(_wBack, 0, WVia);
        }

        // ================================================================ rownanie regionu (Z11) i odcinek "Ludzie (ksiega)"
        private static void CanonByRegion(Dictionary<string, RegionNum> now)
        {
            foreach (var h in _thr.Values)
                foreach (var o in h)
                {
                    if (o.Key.Length == 0 || !(o.Value > 0.0)) continue;
                    RegionNum x; now.TryGetValue(o.Key, out x); x.T += o.Value; now[o.Key] = x;
                }
            foreach (var w in _wall)
            {
                if (w.Key.Length == 0 || !(w.Value > 0.0)) continue;
                RegionNum x; now.TryGetValue(w.Key, out x); x.W += w.Value; now[w.Key] = x;
            }
        }

        private static string CanonSegment()
        {
            double t = ThrallsTotal(), w = WallTotal;
            var origins = new HashSet<string>();
            foreach (var h in _thr.Values) foreach (var o in h.Keys) if (o.Length > 0) origins.Add(o);
            double tRef = 0, tDead = 0, wRef = 0;
            foreach (var r in _rg.Values) { tRef += r.TRef; tDead += r.TDead; wRef += r.WRef; }
            return " | E4/9: NIEWOLNI " + N0(t) + " (z regionow pochodzenia " + origins.Count + ", w regionach trzymania " + _thr.Count + "; stan dnia siewu " + N0(tRef) + ", zmarli od siewu " + N0(tDead)
                   + "), Mur " + N0(w) + " (dnia siewu " + N0(wRef) + "), rownanie z T i W";
        }

        // ================================================================ WatchLaw.Levy - ludnosc Westeros (Z13)
        /// <summary>WatchLaw.Levy krok 3: do n ludzi z wsi regionu wedlug ludzi (podloga 50 hearth, Move DOM -> SLUZBA 1/k, MWatchLevy). 0 przy !LevyLive.</summary>
        internal static double LevyWatchPeople(Settlement region, double n)
        {
            if (region == null || !(n > Eps) || !LevyLive) return 0.0;
            try { return FromVillages(region, n, MWatchLevy, true); }
            catch (Exception e) { OStumble("LevyWatchPeople", e); return 0.0; }
        }

        // ================================================================ konto UCHODZCY -> pojmani z rabunku (U14, U22)
        /// <summary>
        /// E4/9 (6.2 pkt 4): n uchodzcow wsi (konto UCHODZCY) idzie do lochu najezdzcy jako jency - zdejmuje min(n, poza domem); hearth bez zmian (sa poza wsia).
        /// Licznik doby "pojmani" w kontroli konta (obie linie). Wolajacy dopisuje ich do lochu (GiveNew z pochodzeniem = region wsi). Zwraca, ilu zeszlo. Nie rzuca.
        /// </summary>
        internal static double RefugeesToCaptives(Village v, double n)
        {
            if (v == null || !(n > 0.0) || double.IsInfinity(n) || _refCount == 0) return 0.0;
            try
            {
                string id = VillageId(v);
                if (id == null) return 0.0;
                lock (RefGate)
                {
                    Ref r;
                    if (!_ref.TryGetValue(id, out r) || !(r.Away > 0.0)) return 0.0;
                    double t = Math.Min(n, r.Away);
                    r.Away -= t; _dRefCapt += t;
                    if (!(r.Away > 1e-9)) { _dRefCapt += Math.Max(0.0, r.Away); r.Away = 0.0; _refCount--; }   // reszta ponizej 1e-9 razem z nimi (kontrola konta)
                    return t;
                }
            }
            catch (Exception e) { RefStumble("RefugeesToCaptives", e); return 0.0; }
        }

        /// <summary>Cofniecie RefugeesToCaptives (loch nie przyjal - brak postaci albo wyjatek): ludzie wracaja na konto, licznik pojmanych w dol.</summary>
        internal static void RefugeesBack(Village v, double n)
        {
            if (v == null || !(n > 0.0) || double.IsInfinity(n)) return;
            try
            {
                string id = VillageId(v);
                if (id == null) return;
                lock (RefGate)
                {
                    Ref r;
                    if (!_ref.TryGetValue(id, out r)) _ref[id] = r = new Ref();
                    bool had = r.Away > 0.0;
                    r.Away += n; _dRefCapt -= n;
                    if (!had && r.Away > 0.0) _refCount++;
                }
            }
            catch (Exception e) { RefStumble("RefugeesBack", e); }
        }
    }
}
