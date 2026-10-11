using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// E4/7 (164b-1, PLAN 4.17 prog; plan kodu docs/etap4/PLAN-KODU-E47.md rozdz. 6): WOLNE RECE w mezczyznach (R5, U4, U19) - jedna definicja dla linii, hamulca
    /// poboru, plonu, menu osady i PeopleLedger. Na region (warownia OutlawLaw): a = (S + K_m) / (0.27 x L_ref), S = sluzba i jency z regionu (konta SLUZBA/JENCY ksiegi
    /// pochodzenia, bez taborow i bez ""), K_m = max(0, polegli mezczyzni + w niewoli BK + na Murze + 0.27 x zmarli cywile - 0.27 x narodziny netto) od siewu
    /// (niewola i Mur - recenzja E4/7 R1-2, do konta NIEWOLNI E4/9: jeniec z regionu wypadal z S i nie trafial do K_m), L_ref = ludzie regionu w dniu siewu
    /// (dom + sluzba + jency). Prog f wedlug krainy (kultura regionu z dnia kalibracji): zwykla 20% (FreeHandsShare), Zelazne Wyspy (sturgia) 20% + niewolni BK do +20%,
    /// Wolni Ludzie 100% (bez hamulca i bez spadku plonu), Dar Nocnej Strazy 20% tylko do plonu (Straz bierze rekruta z calego Westeros - bez hamulca); okno zniw
    /// (dni 196-252 roku 364-dniowego, lato albo jesien Westeros) - f x FreeHandsHarvestFactor.
    /// Plon (R9 b): L = 1 przy a &lt;= f, inaczej max(0.5, ((1 - min(a, 0.9)) / (1 - f)) ^ FreeHandsElasticity) - mnozy towary wsi (MaterialLaw.ProdPostfix, obok
    /// czynnika spustoszenia E4/5) i zywnosc wsi (FoodPostfix). Hamulec (Jeff 11.10): przy a &gt;= f puste miejsce u notabla sie nie wypelnia (Levy.ProbabilityPostfix).
    /// Chec (Levy.Willingness): nadwyzka rak z ksiegi max(0, f - a) x min(1, dom / dom w dniu siewu) zamiast BK WorkforceExcess.
    /// Cache doby podmieniany jedna referencja (odczyt z watkow modeli bez zamka). Przed siewem / wylaczone - wszystko 1 i bez hamulca.
    /// Poprawka po przegladzie scalenia E4/6 + E4/7: plon spichlerza E4/6 (Famine: 1.2 x ludzie x q, q z modelu zywnosci wsi - z FoodPostfix) idzie juz za liczba
    /// ludzi, wiec tam L liczy sie bez czlonow 0.27 x zmarli cywile i 0.27 x narodziny (LPeople, FamineFix). Model gry (poziom hearth + 1, progi 200/600) liczby
    /// ludzi nie widzi - towary i zywnosc wsi gry dalej z pelnym K_m (R9 b, "smierc bez skutku" dotad).
    /// </summary>
    internal static class FreeHands
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        /// <summary>Okno zniw (P R5: ok. 8 tygodni) w roku 364-dniowym; przy innej dlugosci roku - proporcjonalnie.</summary>
        internal const int HarvestFrom = 196, HarvestTo = 252;

        internal sealed class Cell
        {
            public string Id, Realm; public Settlement Node;
            public double S, Km, A, F, L, LPeople, Excess, LRef, Dom, DomRef, FallenMen, Civil, Born, Enslaved, ToWall;
            public bool Brake, BrakeLand, YieldOnly;
        }

        private static volatile Dictionary<string, Cell> _cells = new Dictionary<string, Cell>();
        private static volatile bool _harvest;
        private static volatile bool _foodOn;      // FoodPostfix wpiety (ApplyFood, raz na proces - nie zerowane w Reset)
        private static double _ironF = 0.2; private static long _ironSlaves = -1;
        private static int _over;
        private static readonly Dictionary<Settlement, string> _realm = new Dictionary<Settlement, string>();

        // liczniki do linii "Pobor:" (Levy.Flush) - losowania odmowione przez ksiege i pula BK = 0 przy wolnych rekach
        private static int _refused, _bkZero, _exN, _stumbles;
        private static double _exSum;
        private static readonly Dictionary<string, int> _refusedBy = new Dictionary<string, int>(), _bkZeroBy = new Dictionary<string, int>();
        private static readonly HashSet<string> _refusedReg = new HashSet<string>(), _bkZeroReg = new HashSet<string>();
        private static readonly object Gate = new object();
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static readonly TextObject _txtHands = new TextObject("{=!}Armoury: hands away at war (men under arms and the fallen)");

        private static void Stumble(string where, Exception e)
        {
            lock (Gate) _stumbles++;
            try { lock (Gate) if (!_err.Add(where)) return; Log.Error("FreeHands." + where, e); } catch { }
        }

        internal static void Reset()
        {
            _cells = new Dictionary<string, Cell>(); _harvest = false; _ironF = 0.2; _ironSlaves = -1; _over = 0;
            _realm.Clear();
            lock (Gate) { _refused = _bkZero = _exN = 0; _exSum = 0; _refusedBy.Clear(); _bkZeroBy.Clear(); _refusedReg.Clear(); _bkZeroReg.Clear(); _stumbles = 0; _err.Clear(); }
        }

        private static Settings S { get { return Settings.Current; } }
        private static bool Live { get { var s = S; return s != null && s.PeopleOriginEnabled && PeopleBook.OriginLive; } }

        // ------------------------------------------------------------ kraina regionu (6.1)
        /// <summary>Kraina regionu = kultura z dnia kalibracji (miasto - wlasna, zamek - pierwszej wsi zamku); w ostatecznosci migawka sesji E4/1 (Measure40.CultureAtStart).</summary>
        internal static string RealmOf(Settlement region)
        {
            if (region == null) return "?";
            string c;
            if (_realm.TryGetValue(region, out c)) return c;
            c = null;
            try
            {
                if (region.IsTown) c = PeopleBook.CultureAtCalibration(region);
                else if (region.BoundVillages != null && region.BoundVillages.Count > 0 && region.BoundVillages[0].Settlement != null)
                    c = PeopleBook.CultureAtCalibration(region.BoundVillages[0].Settlement);
                if (string.IsNullOrEmpty(c) || c == "?") c = Measure40.CultureAtStart(region);
            }
            catch (Exception e) { Stumble("RealmOf", e); }
            if (string.IsNullOrEmpty(c)) c = "?";
            _realm[region] = c;
            return c;
        }

        /// <summary>Okno zniw: dzien roku w [196; 252] (rok 364; inny rok - proporcjonalnie) i pora Westeros lato albo jesien (P R5: w latach zimy bez okna).</summary>
        internal static bool HarvestWindow()
        {
            try
            {
                int y = Math.Max(28, CampaignTime.DaysInYear);      // rok gry (Armoury: 364 dni - LongYearTimeModel), jak PopulationLaw.YearDays
                if (y <= 0) return false;
                int d = (int)CampaignTime.Now.ToDays % y; if (d < 0) d += y;
                int from = (int)Math.Round(HarvestFrom * (double)y / 364.0), to = (int)Math.Round(HarvestTo * (double)y / 364.0);
                if (d < from || d > to) return false;
                var season = WesterosClimate.Now();
                return season == CampaignTime.Seasons.Summer || season == CampaignTime.Seasons.Autumn;
            }
            catch (Exception e) { Stumble("HarvestWindow", e); return false; }
        }

        /// <summary>Prog f krainy (bez okna zniw). brake - kraina ma hamulec; yieldOnly - prog tylko do plonu (Dar).</summary>
        private static double FOf(string realm, out bool brake, out bool yieldOnly)
        {
            var s = S;
            double share = s != null ? Math.Max(0.0, Math.Min(1.0, s.FreeHandsShare)) : 0.2;
            brake = true; yieldOnly = false;
            if (realm == "freefolk") { brake = false; return 1.0; }
            if (realm == "nightswatch") { brake = false; yieldOnly = true; return share; }
            if (realm == "sturgia") return Math.Min(1.0, _ironF);
            return share;
        }

        // ------------------------------------------------------------ doba (PeopleBook.OriginDaily, po uzgodnieniu)
        internal static void Update(Dictionary<string, PeopleBook.RegionView> views)
        {
            if (views == null || !Live) { _cells = new Dictionary<string, Cell>(); _over = 0; return; }
            var s = S;
            bool harvest = HarvestWindow();
            double hf = Math.Max(0.0, Math.Min(1.0, s.FreeHandsHarvestFactor));
            double e = Math.Max(0.0, Math.Min(2.0, s.FreeHandsElasticity));
            // Zelazne Wyspy: f = 0.20 + min(0.20, niewolni / (0.27 x ludzie krainy)) - niewolni BK z 169c tej doby (do E4/9)
            double share = Math.Max(0.0, Math.Min(1.0, s.FreeHandsShare));
            double ironPeople = 0; foreach (var v in views.Values) if (v.Realm == "sturgia") ironPeople += v.Dom;
            long sl = 0; bool hasSl = false;
            try { hasSl = Measure169c.LinesOn && Measure169c.SlavesOf("sturgia", (int)CampaignTime.Now.ToDays, out sl); } catch (Exception ex) { Stumble("Update(169c)", ex); }
            _ironSlaves = hasSl ? sl : -1;
            _ironF = share + (hasSl && ironPeople > 0 ? Math.Min(Math.Max(0.0, s.FreeHandsIronIslandsExtraMax), sl / (PeopleBook.MenShare * ironPeople)) : 0.0);
            var cells = new Dictionary<string, Cell>();
            int over = 0;
            foreach (var v in views.Values)
            {
                try
                {
                    var c = new Cell { Id = v.Id, Node = v.Node, Realm = v.Realm, S = v.S + v.J, LRef = v.LRef, Dom = v.Dom, DomRef = v.DomRef, FallenMen = v.FallenMen, Civil = v.Civil, Born = v.Born,
                                       Enslaved = v.Enslaved, ToWall = v.ToWall };
                    bool brake, yieldOnly;
                    double f = FOf(v.Realm, out brake, out yieldOnly);
                    if (harvest && f < 0.999) f *= hf;
                    c.F = f; c.BrakeLand = brake; c.YieldOnly = yieldOnly;
                    // recenzja E4/7 R1-2 (luka wzoru P R5 do E4/9): jency z regionu sprzedani w niewole BK i wyslani na Mur nie wroca do DOM - jak polegli (wszyscy jency to mezczyzni);
                    // bez tego sprzedaz jencow krainy "oddawala" jej wolne rece i zdejmowala hamulec. E4/9 (konto NIEWOLNI w S) musi stad niewole zdjac.
                    c.Km = Math.Max(0.0, v.FallenMen + v.Enslaved + v.ToWall + PeopleBook.MenShare * v.Civil - PeopleBook.MenShare * v.Born);
                    double men = PeopleBook.MenShare * v.LRef;
                    c.A = men > 0.0 ? (c.S + c.Km) / men : 0.0;
                    // plon (R9 b)
                    if (f >= 0.999 || e <= 0.0 || c.A <= f || men <= 0.0) c.L = 1.0;
                    else c.L = Math.Max(0.5, Math.Pow((1.0 - Math.Min(c.A, 0.9)) / (1.0 - f), e));
                    if (double.IsNaN(c.L) || double.IsInfinity(c.L)) c.L = 1.0;
                    // przeglad scalenia E4/6 + E4/7: L plonu spichlerza (Famine) - zmarli cywile i narodziny zmieniaja juz liczbe ludzi w plonie (1.2 x ludzie x q),
                    // tu zostaje tylko ubytek rak bez zmiany liczby gab w domu: sluzba i jency (S), polegli, niewola, Mur
                    double aP = men > 0.0 ? (c.S + Math.Max(0.0, v.FallenMen + v.Enslaved + v.ToWall)) / men : 0.0;
                    if (f >= 0.999 || e <= 0.0 || aP <= f || men <= 0.0) c.LPeople = 1.0;
                    else c.LPeople = Math.Max(0.5, Math.Pow((1.0 - Math.Min(aP, 0.9)) / (1.0 - f), e));
                    if (double.IsNaN(c.LPeople) || double.IsInfinity(c.LPeople)) c.LPeople = 1.0;
                    c.Brake = brake && men > 0.0 && c.A >= f;
                    if (c.A > f && f < 0.999 && men > 0.0 && !yieldOnly) over++;
                    double home = v.DomRef > 0.0 ? Math.Min(1.0, v.Dom / v.DomRef) : 1.0;
                    c.Excess = Math.Max(0.0, f - c.A) * home;
                    cells[v.Id] = c;
                }
                catch (Exception ex) { Stumble("Update(region)", ex); }
            }
            _cells = cells; _harvest = harvest; _over = over;
        }

        private static Cell CellOf(Settlement s)
        {
            if (s == null) return null;
            var region = s.IsVillage && s.Village != null && s.Village.Bound != null ? s.Village.Bound : ((s.IsTown || s.IsCastle) ? s : null);
            if (region == null) { try { region = OutlawLaw.RegionFor(s); } catch { return null; } }
            if (region == null || region.StringId == null) return null;
            var d = _cells; Cell c;
            return d != null && d.TryGetValue(region.StringId, out c) ? c : null;
        }

        /// <summary>Czynnik plonu L wsi (1 = bez zmiany). Z latek modeli (watki AI) - sam odczyt cache.</summary>
        internal static float YieldFactor(Village v)
        {
            if (v == null || v.Settlement == null) return 1f;
            try
            {
                var c = CellOf(v.Settlement);
                return c == null || c.L >= 1.0 ? 1f : (float)c.L;
            }
            catch (Exception e) { Stumble("YieldFactor", e); return 1f; }
        }

        /// <summary>Przeglad scalenia E4/6 + E4/7: mnoznik q plonu spichlerza (Famine.Daily, Famine.FeedNow) = LPeople / L, ktore wlasnie pomnozyl FoodPostfix
        /// (ten sam cache, ten sam watek glowny) - w plonie spichlerza zmarli cywile i narodziny dzialaja tylko przez liczbe ludzi. 1 = bez zmiany (brak latki
        /// zywnosci, brak komorki, przed siewem). Nie rzuca.</summary>
        internal static double FamineFix(Village v)
        {
            if (!_foodOn || v == null || v.Settlement == null) return 1.0;
            try
            {
                var c = CellOf(v.Settlement);
                if (c == null) return 1.0;
                double used = c.L >= 1.0 ? 1.0 : (double)(float)c.L;     // dokladnie to, co dal YieldFactor do FoodPostfix
                double r = c.LPeople / Math.Max(1e-3, used);
                return double.IsNaN(r) || double.IsInfinity(r) || r <= 0.0 ? 1.0 : r;
            }
            catch (Exception e) { Stumble("FamineFix", e); return 1.0; }
        }

        /// <summary>Hamulec: kraina bez wolnych rak nie wystawia nowego wojska (a &gt;= f; Wolni Ludzie i Dar bez hamulca). Klucz FreeHandsBrake.</summary>
        internal static bool Braked(Settlement s)
        {
            var st = S;
            if (st == null || !st.FreeHandsBrake || !Live) return false;
            var c = CellOf(s);
            return c != null && c.Brake;
        }

        /// <summary>Nadwyzka rak z ksiegi (0..1) dla checi do sluzby; false - brak danych (przed siewem) - wolajacy bierze BK WorkforceExcess jak dotad.</summary>
        internal static bool Excess(Settlement s, out float ex)
        {
            ex = 0f;
            if (!Live) return false;
            var c = CellOf(s);
            if (c == null || !(c.LRef > 0.0)) return false;
            ex = (float)Math.Max(0.0, Math.Min(1.0, c.Excess));
            lock (Gate) { _exN++; _exSum += ex; }
            return true;
        }

        /// <summary>Obciazenie a i prog f regionu (PeopleLedger U19); false - brak danych.</summary>
        internal static bool AF(Settlement region, out double a, out double f)
        {
            a = 0; f = 0;
            if (!Live) return false;
            var c = CellOf(region);
            if (c == null || !(c.LRef > 0.0)) return false;
            a = c.A; f = c.F;
            return true;
        }

        internal static Cell Of(Settlement region) { return Live ? CellOf(region) : null; }
        internal static bool HarvestNow { get { return _harvest; } }

        /// <summary>Levy.ProbabilityPostfix: losowanie pustego miejsca odmowione przez ksiege (licznik wedlug krainy).</summary>
        internal static void NoteRefused(Settlement s)
        {
            try
            {
                var c = CellOf(s);
                lock (Gate)
                {
                    _refused++;
                    string k = c != null ? c.Realm : "?";
                    int v; _refusedBy.TryGetValue(k, out v); _refusedBy[k] = v + 1;
                    if (c != null) _refusedReg.Add(c.Id);
                }
            }
            catch (Exception e) { Stumble("NoteRefused", e); }
        }

        /// <summary>Raz na dobe (Levy.Flush): pula BK = 0 przy wolnych rekach - notabl pomijany przez gre, zanim zapyta model (U17, drugi hamulec).</summary>
        private static void ScanBkZero()
        {
            if (!Live) return;
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null || st.Notables == null || st.Notables.Count == 0 || !(st.IsVillage || st.IsTown || st.IsCastle)) continue;
                    var c = CellOf(st);
                    if (c == null || c.A >= c.F) continue;
                    float mp;
                    if (!Levy.BkManpower(st, out mp) || mp > 0f) continue;
                    lock (Gate)
                    {
                        _bkZero++;
                        int v; _bkZeroBy.TryGetValue(c.Realm, out v); _bkZeroBy[c.Realm] = v + 1;
                        _bkZeroReg.Add(c.Id);
                    }
                }
                catch (Exception e) { Stumble("ScanBkZero", e); }
            }
        }

        private static string By(Dictionary<string, int> d)
        {
            if (d.Count == 0) return "-";
            return string.Join(", ", d.OrderByDescending(kv => kv.Value).Take(8).Select(kv => kv.Key + " " + kv.Value).ToArray());
        }

        /// <summary>Dopisek do linii "Pobor:" (U17): nadwyzka rak z ksiegi, odmowione przez ksiege i pula BK = 0 przy wolnych rekach - osobno, wedlug krain. Zeruje liczniki.</summary>
        internal static string LevySegment()
        {
            if (!Live) return "";
            try { ScanBkZero(); } catch (Exception e) { Stumble("LevySegment(skan)", e); }
            var sb = new StringBuilder(" | E4/7: nadwyzka rak z ksiegi srednio ");
            lock (Gate)
            {
                sb.Append(_exN > 0 ? (_exSum / _exN).ToString("0.000", Inv) : "-").Append(" (dotad BK WorkforceExcess)")
                  .Append(" | odmowione przez ksiege (a >= f) ").Append(_refused).Append(" losowan w ").Append(_refusedReg.Count).Append(" regionach (krainy: ").Append(By(_refusedBy)).Append(')')
                  .Append(" | pula BK = 0 przy wolnych rekach ").Append(_bkZero).Append(" osad w ").Append(_bkZeroReg.Count).Append(" regionach (krainy: ").Append(By(_bkZeroBy)).Append(')')
                  .Append(" | hamulec ksiegi: ").Append(S != null && S.FreeHandsBrake ? "TAK" : "NIE (Free Hands Brake)")
                  .Append(" | okno zniw ").Append(_harvest ? "TAK" : "NIE");
                _refused = _bkZero = _exN = 0; _exSum = 0; _refusedBy.Clear(); _bkZeroBy.Clear(); _refusedReg.Clear(); _bkZeroReg.Clear();
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------ linia doby
        internal static void Line(int day)
        {
            if (!Live) return;
            var s = S;
            var cells = _cells;
            var ci = Inv;
            double share = Math.Max(0.0, Math.Min(1.0, s.FreeHandsShare));
            var sb = new StringBuilder(2500);
            sb.Append("Wolne rece (E4/7): dzien ").Append(day).Append(" | prog: zwykly ").Append((share * 100).ToString("0", ci)).Append("%, Zelazne Wyspy ").Append((Math.Min(1.0, _ironF) * 100).ToString("0.#", ci))
              .Append("% (niewolni BK ").Append(_ironSlaves >= 0 ? _ironSlaves.ToString(ci) : "brak danych 169c").Append("), Wolni Ludzie 100%, Straz bez progu (Dar - tylko plon)")
              .Append("; okno zniw (dni ").Append(HarvestFrom).Append('-').Append(HarvestTo).Append(", lato/jesien): ").Append(_harvest ? "TAK - prog x" + s.FreeHandsHarvestFactor.ToString("0.##", ci) : "NIE");
            double aw = 0, lw = 0, minL = 1.0; string minLName = "-"; int lowL = 0, brake = 0;
            var list = new List<Cell>();
            foreach (var c in cells.Values)
            {
                if (!(c.LRef > 0.0)) continue;
                list.Add(c);
                double men = PeopleBook.MenShare * c.LRef;
                aw += c.A * men; lw += men;
                if (c.L < 1.0) { lowL++; if (c.L < minL) { minL = c.L; minLName = Name(c.Node); } }
                if (c.Brake) brake++;
            }
            sb.Append(" | swiat: a srednio ").Append(lw > 0 ? (100.0 * aw / lw).ToString("0.00", ci) : "-").Append("% mezczyzn, regionow ponad progiem ").Append(_over).Append(" z ").Append(list.Count)
              .Append(" (hamulec - bez nowych ochotnikow: ").Append(brake).Append(s.FreeHandsBrake ? "" : ", hamulec wylaczony").Append("), L: min ").Append(minL.ToString("0.000", ci)).Append(" (").Append(minLName)
              .Append("), regionow z L < 1 ").Append(lowL);
            list.Sort((a, b) => b.A.CompareTo(a.A));
            sb.Append(" | top 8:");
            for (int i = 0; i < list.Count && i < 8; i++)
            {
                var c = list[i];
                sb.Append(i > 0 ? ";" : "").Append(' ').Append(Name(c.Node)).Append(" (").Append(c.Realm).Append(") a = (S + K_m) / (0.27 x L_ref) = ").Append((100 * c.A).ToString("0.00", ci))
                  .Append("% [S ").Append(c.S.ToString("0", ci)).Append(", K_m = polegli ").Append(c.FallenMen.ToString("0", ci)).Append(" + niewola ").Append(c.Enslaved.ToString("0", ci))
                  .Append(" + Mur ").Append(c.ToWall.ToString("0", ci)).Append(" + 0.27 x cywile ").Append(c.Civil.ToString("0", ci))
                  .Append(" - 0.27 x narodziny ").Append(c.Born.ToString("0", ci)).Append(" = ").Append(c.Km.ToString("0", ci)).Append(", L_ref ").Append(c.LRef.ToString("0", ci))
                  .Append("], prog ").Append((100 * c.F).ToString("0.#", ci)).Append("%").Append(c.YieldOnly ? " (tylko plon)" : (c.BrakeLand ? "" : " (bez hamulca)"))
                  .Append(", L ").Append(c.L.ToString("0.000", ci)).Append(" (spichlerz ").Append(c.LPeople.ToString("0.000", ci)).Append(')').Append(c.Brake ? ", HAMULEC" : "");
            }
            double lref = 0; foreach (var c in list) lref += c.LRef;
            sb.Append(" | siew dzien ").Append(PeopleBook.SowDay).Append(", L_ref swiata ").Append((lref / 1e6).ToString("0.000", ci)).Append(" mln");
            int st; lock (Gate) st = _stumbles;
            if (st > 0) sb.Append(" | potkniecia ").Append(st);
            sb.Append('.');
            Log.Info(sb.ToString());
        }

        private static string Name(Settlement s) { try { return s != null && s.Name != null ? s.Name.ToString() : "?"; } catch { return "?"; } }

        // ------------------------------------------------------------ zywnosc wsi (R9 b)
        /// <summary>DefaultVillageProductionCalculatorModel.CalculateDailyFoodProductionAmount: zywnosc wsi x L (obok zimy WinterBite i spustoszenia E4/5 - mnoza sie).</summary>
        public static void FoodPostfix(Village __0, ref float __result)
        {
            if (!(__result > 0f)) return;
            float m = YieldFactor(__0);
            if (m != 1f) __result *= m;
        }

        /// <summary>MaterialLaw.ProdPostfix: czynnik L towarow wsi (ten sam wzor mnozenia co wpis 98 - czynnik x suma dotychczasowych).</summary>
        internal static TextObject HandsText { get { return _txtHands; } }

        internal static bool ApplyFood(Harmony h)
        {
            try
            {
                var food = AccessTools.Method(typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultVillageProductionCalculatorModel), "CalculateDailyFoodProductionAmount", new[] { typeof(Village) });
                if (food == null || food.ReturnType != typeof(float)) return false;
                h.Patch(food, postfix: new HarmonyMethod(typeof(FreeHands), nameof(FoodPostfix)));
                _foodOn = true;
                return true;
            }
            catch (Exception e) { Log.Error("FreeHands.ApplyFood", e); return false; }
        }
    }
}
