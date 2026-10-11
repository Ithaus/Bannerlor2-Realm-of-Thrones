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
    /// E4/8 (164b-2, PLAN 4.7, 4.9; plan kodu docs/etap4/PLAN-KODU-E48.md rozdz. 10): czesc ksiegi ludzi dla wojska z ludzi, wedrowcow BK i miast.
    ///  - konto W DRODZE (ARoad): wedrowcy i eskorta konwojow BK (slowniki skladu KRoad - poza S wolnych rak, w rownaniu regionu jak TABOR) i ludzie miedzy wsia
    ///    a miastem przy migracji R2 (dla Move liczy sie tylko znak: z DOM / do DOM);
    ///  - ArriveAt: udzialy pochodzenia schodzace z rosteru do DOM osady s (wies +n/k, miasto +n stanu, zamek - najslabsza wies regionu); udzialy innych regionow -
    ///    HomeKnown (E4/7, do regionu pochodzenia), "" - Note bez hearth; przy migrate (wedrowcy u celu) wszyscy znani do s, a miedzy regionami MigOut/MigIn (rg:);
    ///  - TownShift: zmiana stanu miasta wedlug przyczyny (E4/8) - razem z licznikami w TakeLevy/BackToTown (E4/7) i TownDie (E4/6) daje kontrole linii "Miasta";
    ///  - TakeFromRegionVillages: migracja R2 ze wsi miasta (stan Normal, podloga 50 hearth, proporcjonalnie do ludzi);
    ///  - koniec partii bez bitwy (ArmsPartyGone - przed "resztka w nicosc" E4/7): wedrowcy u celu / w domu, eskorta konwoju do domu, partia rozwiazana -> DOM pochodzenia (R6);
    ///  - odniesienia miast "tr:" w "arm_people" (R2: udzial miejski regionu z pierwszej doby z czynnym R2).
    /// Ten sam warunek LevyLive dla zdjecia z DOM i powrotu (symetria E4/7). Kazde cialo we wlasnym try, potkniecia liczone (U24); nie stoi miedzy zdjeciem a dopisaniem
    /// ludzi w kodzie gry (zdejmowanie udzialow ze slownika - po fakcie).
    /// </summary>
    internal static partial class PeopleBook
    {
        /// <summary>Konto W DRODZE (E4/8): wedrowcy BK i migranci ze wsi do miasta.</summary>
        internal const int ARoad = 7;

        private static bool RoadOn { get { var s = Settings.Current; return s != null && s.TravellersFromPeople; } }

        // ---------------------------------------------------------------- stan miast: ruch wedlug przyczyny (bez zapisu - linia "Miasta (E4/8 R2)" czyta i zeruje)
        private static readonly double[] _townMv = new double[MKinds];      // pod Gate: zmiana stanu miast wedlug przyczyny (+ przybylo, - ubylo)
        private static double _townSet;                                     // pod Gate: zalozenie (+) i porzucenie (-) stanu - nie ruch ludzi
        private static int _townZero;                                       // pod Gate: TownShift obciety na zerze
        private static double _townZeroMen;

        // ---------------------------------------------------------------- odniesienia miast R2 ("tr:" w zapisie): id miasta -> [ludzie miasta, ludzie jego wsi] z pierwszej doby R2
        private static readonly Dictionary<string, double[]> _tr = new Dictionary<string, double[]>();

        // ---------------------------------------------------------------- doba (kontrola linii E4/8): ruch DOM przyczynami E4/8 z konca doby ksiegi
        internal static readonly double[] ArmsDayMen = new double[MKinds];

        private static void ResetArms()
        {
            lock (Gate) { Array.Clear(_townMv, 0, MKinds); _townSet = 0; _townZero = 0; _townZeroMen = 0; _tr.Clear(); }
            Array.Clear(ArmsDayMen, 0, MKinds);
        }

        /// <summary>PeopleBook.Daily, przed zerowaniem licznikow doby: ruch DOM przyczynami E4/8 (ludzie) - linia "Wojsko z ludzi (E4/8)" porownuje go ze swoimi licznikami.</summary>
        private static void ArmsCloseDay()
        {
            lock (Gate) Array.Copy(_mMen, ArmsDayMen, MKinds);
        }

        // ---------------------------------------------------------------- zapis tr:
        private static void ExportArms(StringBuilder sb)
        {
            List<KeyValuePair<string, double[]>> list;
            lock (Gate) list = _tr.ToList();
            bool first = true;
            foreach (var kv in list)
            {
                if (BadId(kv.Key) || kv.Value == null || kv.Value.Length < 2) continue;
                sb.Append(first ? "|tr:" : ",").Append(kv.Key).Append('=').Append(kv.Value[0].ToString("R", Inv)).Append(';').Append(kv.Value[1].ToString("R", Inv));
                first = false;
            }
        }

        private static void ImportArms(string body)
        {
            if (string.IsNullOrEmpty(body)) return;
            foreach (var item in body.Split(','))
            {
                int eq = item.LastIndexOf('=');
                if (eq <= 0) continue;
                var a = item.Substring(eq + 1).Split(';');
                if (a.Length < 2) continue;
                double p, v;
                if (!double.TryParse(a[0], NumberStyles.Float, Inv, out p) || !double.TryParse(a[1], NumberStyles.Float, Inv, out v)) continue;
                if (double.IsNaN(p) || double.IsInfinity(p) || p < 0.0 || double.IsNaN(v) || double.IsInfinity(v) || v < 0.0) continue;     // smieci - zalozy sie od nowa
                lock (Gate) _tr[item.Substring(0, eq)] = new[] { p, v };
            }
        }

        /// <summary>Odniesienie R2 miasta (ludzie miasta, ludzie jego wsi z pierwszej doby R2). false = brak.</summary>
        internal static bool TownRef(Settlement town, out double refPeople, out double refVillages)
        {
            refPeople = refVillages = 0.0;
            if (town == null || town.StringId == null) return false;
            lock (Gate)
            {
                double[] a;
                if (!_tr.TryGetValue(town.StringId, out a) || a == null || a.Length < 2) return false;
                refPeople = a[0]; refVillages = a[1];
                return true;
            }
        }

        internal static void SetTownRef(Settlement town, double refPeople, double refVillages)
        {
            if (town == null || BadId(town.StringId) || double.IsNaN(refPeople) || double.IsNaN(refVillages)) return;
            lock (Gate) _tr[town.StringId] = new[] { Math.Max(0.0, refPeople), Math.Max(0.0, refVillages) };
        }

        /// <summary>Suma odniesien (udzial miejski swiata z dnia odniesienia) i ich liczba.</summary>
        internal static int TownRefSum(out double refPeople, out double refVillages)
        {
            refPeople = refVillages = 0.0;
            lock (Gate)
            {
                foreach (var a in _tr.Values) { if (a == null || a.Length < 2) continue; refPeople += a[0]; refVillages += a[1]; }
                return _tr.Count;
            }
        }

        // ---------------------------------------------------------------- stan miasta (Z4)
        /// <summary>
        /// Zmiana stanu miasta (E4/4 "t:") o `men` z przyczyny `cause` (E4/8: karawany, wedrowcy, milicja, nowe druzyny, migracja, narodziny i zgony; powroty).
        /// Nie ponizej 0 (reszta - licznik "miasto na zerze"). count - ruch w liczniku ksiegi ("Ludzie (ksiega)"); migracja R2 liczy go raz po stronie wsi (Move).
        /// Zwraca zmiane faktycznie zrobiona (0 - miasto bez stanu). Nie rzuca.
        /// </summary>
        internal static double TownShift(Settlement town, double men, int cause, bool count)
        {
            if (town == null || town.StringId == null || double.IsNaN(men) || double.IsInfinity(men) || men == 0.0 || cause < 0 || cause >= MKinds) return 0.0;
            double applied = 0.0;
            try
            {
                lock (Gate)
                {
                    float p;
                    if (!_town.TryGetValue(town.StringId, out p)) return 0.0;
                    double want = (double)p + men;
                    if (want < 0.0) { _townZero++; _townZeroMen += -want; want = 0.0; }
                    float set = (float)want;
                    _town[town.StringId] = set;
                    applied = (double)set - p;
                    _townMv[cause] += applied;
                }
            }
            catch (Exception e) { Stumble("TownShift", e); return 0.0; }
            if (count && applied != 0.0) Count(cause == MTownNatural ? applied : Math.Abs(applied), cause, 0.0);
            return applied;
        }

        /// <summary>Linia "Miasta (E4/8 R2)": ruch stanu miast wedlug przyczyny od ostatniego odczytu, zalozenie/porzucenie stanu, obciecia na zerze (kopiuje i zeruje).</summary>
        internal static void TakeTownMoves(double[] dst, out double set, out int zero, out double zeroMen)
        {
            lock (Gate)
            {
                for (int i = 0; i < MKinds && i < dst.Length; i++) dst[i] = _townMv[i];
                Array.Clear(_townMv, 0, MKinds);
                set = _townSet; zero = _townZero; zeroMen = _townZeroMen;
                _townSet = 0; _townZero = 0; _townZeroMen = 0;
            }
        }

        /// <summary>Z11: przyrost naturalny miasta (netto, moze byc ujemny) - narodziny regionu (rownanie krain, K_m wolnych rak), jak NoteBorn wsi.</summary>
        internal static void NoteBornTown(Settlement town, double men)
        {
            if (town == null || double.IsNaN(men) || double.IsInfinity(men) || men == 0.0 || !_sown) return;
            try
            {
                var region = OutlawLaw.RegionFor(town);
                if (region == null || region.StringId == null) return;
                Rg(region.StringId).Born += men;
            }
            catch (Exception e) { OStumble("NoteBornTown", e); }
        }

        // ---------------------------------------------------------------- migracja R2
        /// <summary>
        /// R2: do `want` ludzi ze wsi miasta (BoundVillages w stanie Normal), proporcjonalnie do ludzi w domu, kazda nie ponizej 50 hearth (jak OutlawLaw.TakeFrom
        /// i pobor E4/7) - PeopleBook.Move (DOM -> W DRODZE, hearth -m/k). Wolajacy dopisuje ich do stanu miasta (TownShift bez drugiego licznika). Uchodzcy E4/5
        /// sa poza hearth - nie sa zrodlem. Wies bez przelicznika ludzi - pomijana. Zwraca, ilu wyszlo. Nie rzuca.
        /// </summary>
        internal static double TakeFromRegionVillages(Settlement town, double want, int cause)
        {
            if (town == null || !(want > 0.0) || double.IsInfinity(want) || town.BoundVillages == null || !PeopleUnit.On) return 0.0;
            double got = 0.0;
            try
            {
                var vs = new List<Village>();
                double all = 0.0;
                foreach (var v in town.BoundVillages)
                {
                    if (v == null || v.Settlement == null || v.VillageState != Village.VillageStates.Normal || !(v.Hearth > 50f)) continue;
                    vs.Add(v); all += PopulationLaw.PeopleOf(v.Settlement);
                }
                if (vs.Count == 0) return 0.0;
                if (all > 0.0)
                    foreach (var v in vs)
                    {
                        double share = PopulationLaw.PeopleOf(v.Settlement) / all;
                        if (share > 0.0) got += FromVillageTo(v, want * share, cause);
                    }
                if (want - got > Eps)
                    foreach (var v in vs.OrderByDescending(x => x.Hearth))
                    {
                        if (want - got <= Eps) break;
                        got += FromVillageTo(v, want - got, cause);
                    }
            }
            catch (Exception e) { OStumble("TakeFromRegionVillages", e); }
            return got;
        }

        private static double FromVillageTo(Village v, double n, int cause)
        {
            if (v == null || !(n > 0.0)) return 0.0;
            bool unit;
            float per = PeopleUnit.PerMan(v, PeopleUnit.GamePerMan, out unit);
            if (!unit || !(per > 0f)) return 0.0;
            double can = Math.Max(0.0, (v.Hearth - 50.0) / per);
            double t = Math.Min(can, n);
            if (t <= Eps) return 0.0;
            double h = Move(v, ADom, ARoad, t, cause, PeopleUnit.GamePerMan);
            return h < 0.0 ? t : 0.0;
        }

        // ---------------------------------------------------------------- powrot do DOM osady (10.3)
        internal struct Arrive { public double Here, Other, None, Mig; }

        /// <summary>
        /// Udzialy `sh` (juz zdjete ze slownika rosteru) do DOM: czesc regionu osady s (albo wszystkie znane przy migrate) - do s; inne regiony - HomeKnown (E4/7);
        /// "" i nieumieszczeni - Note(causeNone, bez hearth). Przy migrate udzial z regionu innego niz s: MigOut regionu pochodzenia, MigIn regionu s (rownanie 10.5).
        /// !LevyLive - wszystko Note (wyjscie nie zdjelo nikogo z DOM - symetria). Nie rzuca.
        /// </summary>
        internal static Arrive ArriveAt(Settlement s, Shares sh, int from, int causeKnown, int causeNone, bool migrate)
        {
            var a = new Arrive();
            if (sh == null || !(sh.Total > Eps)) return a;
            try
            {
                if (!LevyLive || s == null)
                {
                    a.None = sh.Total;
                    Note(a.None, causeNone, 0.0);
                    return a;
                }
                var reg = OutlawLaw.RegionFor(s);
                string kS = RegKey(reg);
                foreach (var kv in sh.M.ToList())
                {
                    double m = kv.Value;
                    if (!(m > Eps)) continue;
                    if (kv.Key.Length == 0) { a.None += m; continue; }
                    if (kS.Length > 0 && (kv.Key == kS || migrate))
                    {
                        double p = 0.0;
                        try { p = PlaceAt(s, reg, m, from, causeKnown); } catch (Exception e) { OStumble("ArriveAt(osada)", e); p = 0.0; }
                        a.Here += p;
                        if (p > Eps && kv.Key != kS) { Rg(kv.Key).MigOut += p; Rg(kS).MigIn += p; a.Mig += p; }
                        if (m - p > Eps) a.None += m - p;
                    }
                    else
                    {
                        var one = new Shares(); one.Add(kv.Key, m);
                        double p = HomeKnown(one, causeKnown);
                        a.Other += p;
                        if (m - p > Eps) a.None += m - p;
                    }
                }
                _dest[OHomeKnown] += a.Here;
                if (a.None > Eps) { Note(a.None, causeNone, 0.0); _dest[OHomeLegacy] += a.None; }
            }
            catch (Exception e) { OStumble("ArriveAt", e); }
            return a;
        }

        /// <summary>m ludzi do DOM osady s: wies - Move (+m/k), miasto ze stanem - TownShift (i splata dlugu poboru z miasta, TownBack E4/7), zamek i reszta -
        /// najslabsza wies regionu (OutlawLaw.WeakestVillage, jak HomeKnown). Zwraca umieszczonych.</summary>
        private static double PlaceAt(Settlement s, Settlement reg, double m, int from, int cause)
        {
            if (s == null || !(m > Eps)) return 0.0;
            if (s.IsVillage && s.Village != null)
            {
                Move(s.Village, from, ADom, m, cause, PeopleUnit.GamePerMan);
                return m;
            }
            if (s.IsTown && s.Town != null)
            {
                float p;
                if (TryTown(s, out p))
                {
                    double t = TownShift(s, m, cause, true);
                    if (t > 0.0 && reg != null && reg.StringId != null)
                    {
                        RegRef r;
                        if (_rg.TryGetValue(reg.StringId, out r)) { double owed = r.TownOut - r.TownBack; if (owed > Eps) r.TownBack += Math.Min(owed, t); }
                    }
                    if (t > 0.0) return t;
                }
            }
            var v = OutlawLaw.WeakestVillage(s.IsTown || s.IsCastle ? s : (reg ?? s));
            if (v == null) { _homeNoVillage += m; return 0.0; }
            Move(v, from, ADom, m, cause, PeopleUnit.GamePerMan);
            return m;
        }

        // ---------------------------------------------------------------- koniec partii bez bitwy (10.4, 8.3)
        /// <summary>
        /// MobilePartyDestroyed (z OnPartyDestroyed, PRZED OriginPartyGone - slownik jest): (2) wedrowcy BK - u celu do DOM osady wejscia (migracja miedzy regionami),
        /// zniszczeni w drodze bez bitwy - do domu; konwoje BK - eskorta do domu, niewolnicy w lochu - licznik (E4/9); (3) partia ze slownikiem rozwiazana bez bitwy
        /// (bez niszczyciela, bez bitwy i oblezenia) - DOM pochodzenia (R6; bez band - pula OutlawLaw, bez gracza). Bitwa - okna E4/7 (slownik od wyjazdu). Nie rzuca.
        /// </summary>
        private static void ArmsPartyGone(MobileParty mp, PartyBase destroyer)
        {
            if (mp == null || mp.Party == null || !OriginLive) return;
            int kind = KindOf(mp);
            Org o; bool has = _mem.TryGetValue(mp.Party, out o) && o != null && o.Sum > Eps;
            if (kind == KRoad)
            {
                Org po;
                if (_pris.TryGetValue(mp.Party, out po) && po != null && po.Sum > Eps)
                {
                    var sp = Take(mp.PrisonRoster, po.Sum, FRoadEnd);
                    ArmsFromPeople.NoteSlavesEnd(sp.Total);
                }
                if (!has || destroyer != null || mp.MapEvent != null) return;      // bitwa: polegli/jency/rozbici - okna E4/7; resztka - OriginPartyGone
                bool cargo = ArmsFromPeople.IsBkCargo(mp);
                var cur = mp.CurrentSettlement; var home = mp.HomeSettlement;
                var sh = Take(mp.MemberRoster, o.Sum, FRoadEnd);
                if (!cargo && cur != null)
                {
                    var a = ArriveAt(cur, sh, ARoad, cur == home ? MRoadBack : MRoadIn, MRoadNone, true);
                    ArmsFromPeople.NoteRoadEnd(cur == home ? 1 : (a.Mig > Eps ? 3 : 2), a);
                }
                else
                {
                    var a = ArriveAt(home ?? cur, sh, ARoad, MRoadBack, MRoadNone, false);
                    ArmsFromPeople.NoteRoadEnd(cargo ? 5 : 4, a);
                }
                return;
            }
            if (!has || kind < 0 || kind == KBand || kind == KPlayer || destroyer != null || mp.MapEvent != null || mp.SiegeEvent != null) return;
            if (!ArmsFromPeople.CaravansOn) return;          // R6 dla rozwiazanych bez bitwy - razem z karawanami (K4); wylaczone: resztka w nicosc jak w E4/7
            var home2 = mp.HomeSettlement;
            if (home2 == null) { try { home2 = OutlawLaw.RegionAt(mp.Position.ToVec2()); } catch { home2 = null; } }
            var sh2 = Take(mp.MemberRoster, o.Sum, FDisbandHome);
            var a2 = ArriveAt(home2, sh2, AService, MBackDisband, MSoldier, false);
            ArmsFromPeople.NoteDisbandHome(kind, a2);
        }

        /// <summary>Partia ma slownik skladu KRoad (wedrowcy/konwoj BK z ksiega) - ArmsFromPeople przed zdjeciem z DOM.</summary>
        internal static bool IsRoadParty(MobileParty mp) { try { return mp != null && KindOf(mp) == KRoad; } catch { return false; } }

        /// <summary>Wedrowcy i konwoje BK teraz w drodze: ludzie w slownikach KRoad (razem i znani).</summary>
        internal static void RoadNow(out double all, out double known, out int parties)
        {
            all = known = 0.0; parties = 0;
            if (!OriginLive) return;
            foreach (var kv in _mem)
            {
                var pb = kv.Key;
                if (pb == null || !pb.IsMobile || pb.MobileParty == null || KindOf(pb.MobileParty) != KRoad) continue;
                parties++; all += kv.Value.Sum;
                double u; kv.Value.M.TryGetValue("", out u);
                known += kv.Value.Sum - u;
            }
        }

        /// <summary>Odcinek "Ludzie (ksiega)" (10.5): migracja R19 od siewu i w drodze teraz.</summary>
        private static string ArmsSegment()
        {
            double mi = 0, mo = 0; int regs = 0;
            foreach (var r in _rg.Values) { mi += r.MigIn; mo += r.MigOut; if (r.MigIn > Eps || r.MigOut > Eps) regs++; }
            double road = 0; foreach (var v in Views()) road += v.Road;
            return " | E4/8: migracja R19 od siewu: wyszlo " + N0(mo) + ", przyszlo " + N0(mi) + " (regionow z migracja " + regs + "), w drodze teraz z pochodzeniem " + N0(road);
        }
    }
}
