using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// E4/8 (164b-2, projekt etapu 4 R2, DOK:524 i 9c; plan kodu docs/etap4/PLAN-KODU-E48.md rozdz. 9): MIASTA ZYJA. Stan miasta (E4/4 "t:") dostaje:
    ///  - przyrost naturalny = stopa wsi jego regionu (te same W i U, bez premii gry) minus TownGrowthPenaltyPercent (1 pkt - Londyn 1580-1650: ok. 0.87 urodzenia
    ///    na zgon); netto do narodzin regionu (rownanie krain, K_m wolnych rak);
    ///  - migracje z wlasnych wsi (stan Normal, podloga 50 hearth), gdy region nie gloduje i miasto nie jest oblegane: ubytek naturalny i luka do udzialu miejskiego
    ///    regionu w ok. TownMigrationRefillDays (364; 0 = tylko ubytek naturalny - wersja DOK). Udzial miejski = odniesienie "tr:" z pierwszej doby R2 (miasto rosnie
    ///    i maleje razem ze swoimi wsiami); pobor z miasta (E4/7), karawany i wedrowcy otwieraja luke, ktora wsie wypelniaja w ok. rok (Jeff P2).
    /// Zamki (k = 0) - bez R2. Glod mieszczan - E4/6 (TownDie). Raz na dobe z PopulationLaw.Daily (po TownsDaily, przed rentami). Wymaga Town People Frozen.
    /// Wyjatek na jednym miescie - potkniecie, reszta liczy dalej.
    /// </summary>
    internal static class TownGrowth
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const double Eps = 1e-6;

        /// <summary>R2 liczylo dzis (TownGrowthEnabled + TownPeopleFrozen + kalibracja + przyrost naturalny wsi E4/4).</summary>
        internal static bool Active { get; private set; }

        // doba (bez zapisu)
        private static int _towns, _migTowns, _hStarve, _hSiege, _hShort, _hUnit, _stumbles, _stumblesDay;
        private static double _people, _nat, _mig, _mig1, _mig2, _shortMen, _rateW, _rateVW, _rateP;
        private static double _gMin = double.NaN, _gMax = double.NaN; private static string _gMinName = "-", _gMaxName = "-";
        private static double _last = double.NaN;               // stan miast przy poprzedniej linii (NaN - pierwsza doba sesji)
        private static readonly HashSet<string> _err = new HashSet<string>();
        // menu (9.6): ostatnia doba miasta - naturalne i migracja
        private static readonly Dictionary<string, double[]> _lastOf = new Dictionary<string, double[]>();

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            try { if (_err.Add(where)) Log.Error("TownGrowth." + where, e); } catch { }
        }

        internal static void Reset()
        {
            Active = false;
            ZeroDay();
            _stumbles = 0; _last = double.NaN; _err.Clear(); _lastOf.Clear();
        }

        private static void ZeroDay()
        {
            _towns = _migTowns = _hStarve = _hSiege = _hShort = _hUnit = _stumblesDay = 0;
            _people = _nat = _mig = _mig1 = _mig2 = _shortMen = _rateW = _rateVW = _rateP = 0;
            _gMin = _gMax = double.NaN; _gMinName = _gMaxName = "-";
        }

        private static int YearDays() { return Math.Max(28, CampaignTime.DaysInYear); }

        private static double VillagePeople(Settlement town)
        {
            double p = 0;
            if (town.BoundVillages != null)
                foreach (var v in town.BoundVillages) if (v != null && v.Settlement != null) p += PopulationLaw.PeopleOf(v.Settlement);
            return p;
        }

        private static string Name(Settlement s) { try { return s != null && s.Name != null ? s.Name.ToString() : "?"; } catch { return "?"; } }

        /// <summary>PopulationLaw.Daily (po TownsDaily, przed rentami): narodziny i zgony kazdego miasta ze stanem, potem migracja ze wsi.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            Active = s != null && s.TownGrowthEnabled && PopulationLaw.TownsFrozen && PopulationLaw.Calibrated && PopulationLaw.GrowthOn;     // stopa miasta = stopa wsi (E4/4)
            if (!Active) return;
            int yd = YearDays();
            int refill = Math.Max(0, s.TownMigrationRefillDays);
            var seen = new HashSet<Settlement>();
            foreach (var st in Settlement.All)
            {
                if (st == null || !st.IsTown || st.Town == null || !seen.Add(st)) continue;     // ROT: Settlement.All podaje duplikaty
                try { OneTown(st, yd, refill); }
                catch (Exception e) { Stumble("Daily(" + (st.StringId ?? "?") + ")", e); }
            }
        }

        private static void OneTown(Settlement st, int yd, int refill)
        {
            float p0;
            if (!PeopleBook.TryTown(st, out p0)) return;
            PopulationLaw.TownRateParts parts;
            float g = PopulationLaw.TownRate(st, out parts);
            double nat = (double)p0 * g / 100.0 / yd;
            double natApplied = PeopleBook.TownShift(st, nat, PeopleBook.MTownNatural, true);
            PeopleBook.NoteBornTown(st, natApplied);
            _towns++; _people += p0; _nat += natApplied;
            _rateW += (double)g * p0; _rateVW += (double)parts.Village * p0; _rateP += (double)parts.Penalty * p0;
            if (double.IsNaN(_gMin) || g < _gMin) { _gMin = g; _gMinName = Name(st); }
            if (double.IsNaN(_gMax) || g > _gMax) { _gMax = g; _gMaxName = Name(st); }
            // odniesienie udzialu miejskiego (tr:) - leniwie z pierwszej doby R2
            double vp = VillagePeople(st);
            double refP, refV;
            if (!PeopleBook.TownRef(st, out refP, out refV)) { refP = p0; refV = vp; PeopleBook.SetTownRef(st, refP, refV); }
            double target = refV > Eps ? refP * vp / refV : refP;
            double got = 0.0;
            // migracja - tylko przy jednostce ludzi, bez glodu regionu i bez oblezenia
            bool starving = parts.Starving || st.IsStarving || parts.H > Famine.ThresholdNow;
            if (!PeopleUnit.On) _hUnit++;
            else if (starving) _hStarve++;
            else if (st.IsUnderSiege) _hSiege++;
            else
            {
                double loss = Math.Max(0.0, -natApplied), want;
                if (refill <= 0) want = loss;
                else
                {
                    double gap = target - ((double)p0 + natApplied);
                    want = gap <= 0.0 ? 0.0 : Math.Min(gap, loss + gap / refill);
                }
                if (want > Eps)
                {
                    got = PeopleBook.TakeFromRegionVillages(st, want, PeopleBook.MTownMigrate);
                    if (got > 0.0) got = PeopleBook.TownShift(st, got, PeopleBook.MTownMigrate, false);     // ruch liczony raz - po stronie wsi (Move)
                    if (got > Eps)
                    {
                        _migTowns++; _mig += got; double a = Math.Min(got, loss); _mig1 += a; _mig2 += got - a;
                        // recenzja R1-6: czesc "do udzialu miejskiego" dopelnia luke po poborze E4/7 - splaca dlug miasta (TownBack), zeby powracajacy zolnierze E4/7
                        // szli do wsi, ktore daly migrantow (czesc na ubytek naturalny zastepuje zmarlych mieszczan - nie dlug)
                        if (got - a > Eps) PeopleBook.RepayTownDebt(OutlawLaw.RegionFor(st), got - a);
                    }
                    if (want - got > 1e-3) { _hShort++; _shortMen += want - got; }
                }
            }
            if (st.StringId != null) _lastOf[st.StringId] = new[] { natApplied, got, (double)(int)CampaignTime.Now.ToDays };   // ZC6: doba liczenia (MigratedToday)
        }

        /// <summary>ZC6 (DailyLife.Mobility - etykieta migracji: przybysze ze wsi do biedoty miasta): ludzie, ktorzy przyszli dzis ze wsi regionu do miasta (E4/8 R2,
        /// ta sama doba gry). 0 - R2 nie liczylo dzis tego miasta, brak stanu albo bez migracji. Tylko odczyt.</summary>
        internal static double MigratedToday(Settlement town)
        {
            if (town == null || town.StringId == null || !Active) return 0;
            double[] a;
            if (!_lastOf.TryGetValue(town.StringId, out a) || a == null || a.Length < 3 || (int)a[2] != (int)CampaignTime.Now.ToDays) return 0;
            return a[1] > 0 && !double.IsNaN(a[1]) && !double.IsInfinity(a[1]) ? a[1] : 0;
        }

        /// <summary>Menu "People of this land" (9.6): mieszczanie i rok w dzisiejszym tempie (narodziny minus zgony, przybysze ze wsi). false - brak stanu albo R2 nie liczylo.</summary>
        internal static bool MenuOf(Settlement town, out double people, out double natYear, out double migYear)
        {
            people = natYear = migYear = 0;
            if (town == null || town.StringId == null || !Active) return false;
            float p;
            if (!PeopleBook.TryTown(town, out p)) return false;
            people = p;
            double[] a;
            if (_lastOf.TryGetValue(town.StringId, out a) && a != null && a.Length >= 2) { natYear = a[0] * YearDays(); migYear = a[1] * YearDays(); }
            return true;
        }

        /// <summary>Linia "Miasta (E4/8 R2)" (po "Wojsko z ludzi (E4/8)") i zerowanie licznikow doby; bez logu - tylko zerowanie (kontrola liczy od nastepnej linii).</summary>
        internal static void Line(int day, bool log)
        {
            var mv = new double[PeopleBook.MKinds];
            double set, zeroMen; int zero;
            PeopleBook.TakeTownMoves(mv, out set, out zero, out zeroMen);
            double x = PeopleBook.TownSum;
            double y = _last;
            _last = x;
            if (!log) { ZeroDay(); return; }
            try
            {
                double n = mv[PeopleBook.MTownNatural], m = mv[PeopleBook.MTownMigrate];
                double p = mv[PeopleBook.MLevyTown], pb = mv[PeopleBook.MBackTown], c = mv[PeopleBook.MCaravanOut] + mv[PeopleBook.MCaravanRefill];
                double w1 = mv[PeopleBook.MRoadOut], w2 = mv[PeopleBook.MRoadIn] + mv[PeopleBook.MRoadBack];
                double l1 = mv[PeopleBook.MLevyMilitia], l2 = mv[PeopleBook.MBackMilitia], sp = mv[PeopleBook.MLevySpawn], f = mv[PeopleBook.MFamineTown];
                double vs = mv[PeopleBook.MVetStart];      // scalenie calosci etapu 4: siew weteranow E4/10 ze stanu miast (raz na kampanie)
                double sk = mv[PeopleBook.MSackTown];      // L6 lupu miast: zabici przez BK przy zdobyciu (CitySack.People.cs)
                double all = 0; foreach (var v in mv) all += v;
                double other = all - (n + m + p + pb + c + w1 + w2 + l1 + l2 + sp + f + vs + sk);
                double world = 0; int towns = PeopleBook.TownCount;
                double vill = 0;
                foreach (var v in Village.All) { try { if (v != null && v.Settlement != null) vill += PopulationLaw.PeopleOf(v.Settlement); } catch { } }
                double refP, refV; int refs = PeopleBook.TownRefSum(out refP, out refV);
                world = x + vill;
                var sb = new StringBuilder(1600);
                sb.Append("Miasta (E4/8 R2): dzien ").Append(day).Append(Active ? "" : " | R2 WYLACZONE (Town Growth Enabled / Town People Frozen / Natural Growth Enabled) - stan miast bez narodzin i migracji")
                  .Append(" | miast ").Append(towns).Append(" ze stanem, ludzi ").Append(x.ToString("0", Inv))
                  .Append(double.IsNaN(y) ? " (pierwsza doba sesji - kontrola od jutra)" : " (wczoraj " + y.ToString("0", Inv) + ", zmiana " + Sg(x - y) + ")");
                double gW = _people > 0 ? _rateW / _people : 0, gV = _people > 0 ? _rateVW / _people : 0, gP = _people > 0 ? _rateP / _people : 0;
                sb.Append(" | naturalne ").Append(Sg(n)).Append(" (stopa srednia ").Append(gW.ToString("0.00", Inv)).Append("% rocznie = wies ").Append(gV.ToString("0.00", Inv)).Append("% - kara ")
                  .Append(gP.ToString("0.0#", Inv)).Append(" pkt; najnizsza ").Append(double.IsNaN(_gMin) ? "-" : _gMin.ToString("0.00", Inv) + "% " + _gMinName)
                  .Append(", najwyzsza ").Append(double.IsNaN(_gMax) ? "-" : _gMax.ToString("0.00", Inv) + "% " + _gMaxName).Append(')')
                  .Append(" | migracja ze wsi +").Append(m.ToString("0.#", Inv)).Append(" (ubytek naturalny ").Append(_mig1.ToString("0.#", Inv)).Append(", do udzialu miejskiego ").Append(_mig2.ToString("0.#", Inv))
                  .Append(") w ").Append(_migTowns).Append(" miastach, bez migracji: glod ").Append(_hStarve).Append(", oblezenie ").Append(_hSiege).Append(", wsie bez nadwyzki ").Append(_hShort)
                  .Append(" (brakowalo ").Append(_shortMen.ToString("0.#", Inv)).Append("), jednostka ludzi wylaczona ").Append(_hUnit)
                  .Append(" | inne ruchy stanu miast: pobor i karczmy (E4/7) ").Append(Sg(p)).Append(" / powroty z poboru ").Append(Sg(pb)).Append(", karawany ").Append(Sg(c))
                  .Append(", wedrowcy BK ").Append(Sg(w1)).Append(" / ").Append(Sg(w2)).Append(", milicja ").Append(Sg(l1)).Append(" / ").Append(Sg(l2)).Append(", nowe druzyny ").Append(Sg(sp))
                  .Append(", glod (E4/6) ").Append(Sg(f)).Append(vs != 0 ? ", siew weteranow (E4/10) " + Sg(vs) : "").Append(sk != 0 ? ", zdobycie (L6) " + Sg(sk) : "").Append(", inne powroty (R6) ").Append(Sg(other)).Append(", zalozenie/porzucenie stanu ").Append(Sg(set))
                  .Append(", miasto na zerze ").Append(zero).Append(zero > 0 ? " (" + zeroMen.ToString("0.#", Inv) + " ludzi)" : "");
                sb.Append(" | kontrola: X - Y - (wszystkie ruchy + zalozenie) = ").Append(double.IsNaN(y) ? "-" : (x - y - all - set).ToString("0.00", Inv));
                double u = world > 0 ? 100.0 * x / world : 0, u0 = refP + refV > 0 ? 100.0 * refP / (refP + refV) : 0;
                sb.Append(" | udzial miejski swiata dzis ").Append(u.ToString("0.00", Inv)).Append("% (odniesienie ").Append(u0.ToString("0.00", Inv)).Append("%, odniesien tr: ").Append(refs).Append(')');
                // E4 fixB (P1-3, tylko log): udzial swiata wyzej ma w mianowniku WSZYSTKIE wsie (z wsiami zamkow), a odniesienie tr: - tylko wsie miast (OneTown/SetTownRef),
                // wiec tych dwoch liczb nie wolno porownywac (blad planu E48:436). Ponizej obie strony z JEDNEJ petli po tych samych miastach (stan i odniesienie tr:
                // naraz; _tr nie jest czyszczone przy DropTowns, wiec bez TownRefSum): X = miasta / (miasta + ich wsie dzis), start = to samo z odniesienia,
                // luka = suma max(0, cel - stan), cel jak w OneTown (refP x wsie dzis / refV). Udzial swiata zostaje jako INFO.
                double oP = 0, oV = 0, oRP = 0, oRV = 0, oT = 0, oGap = 0; int oN = 0;
                var oSeen = new HashSet<Settlement>();
                foreach (var st in Settlement.All)
                {
                    if (st == null || !st.IsTown || st.Town == null || !oSeen.Add(st)) continue;     // ROT: Settlement.All podaje duplikaty (jak Daily)
                    try
                    {
                        float tp; double rP, rV;
                        if (!PeopleBook.TryTown(st, out tp) || !PeopleBook.TownRef(st, out rP, out rV)) continue;
                        double vi = VillagePeople(st);
                        double goal = rV > Eps ? rP * vi / rV : rP;
                        oP += tp; oV += vi; oRP += rP; oRV += rV; oT += goal; oGap += Math.Max(0.0, goal - tp); oN++;
                    }
                    catch (Exception e) { Stumble("Line(wlasne wsie)", e); }
                }
                double uo = oP + oV > 0 ? 100.0 * oP / (oP + oV) : 0, uo0 = oRP + oRV > 0 ? 100.0 * oRP / (oRP + oRV) : 0;
                sb.Append(" | miasta wobec wlasnych wsi dzis ").Append(uo.ToString("0.00", Inv)).Append("% (start ").Append(uo0.ToString("0.00", Inv)).Append("%, miast ").Append(oN)
                  .Append("), luka do udzialu ").Append(oGap.ToString("0", Inv)).Append(" ludzi (").Append((oT > 0 ? 100.0 * oGap / oT : 0).ToString("0.00", Inv)).Append("% celu)");
                sb.Append(" | potkniecia ").Append(_stumblesDay).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Line", e); }
            ZeroDay();
        }

        private static string Sg(double v) { return (v >= 0 ? "+" : "") + v.ToString("0.#", Inv); }
    }
}
