using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// DANINY SREDNIOWIECZNE (DS) - KROK F3: KORONA (projekt rozdz. 6.2, 7, 8, 10.3; decyzja Jeffa 17.10: "mozliwe ze bedziemy musieli nieco zawyzyc niektore realne podatki,
    /// aby byla kasa na armie ... lub oprzec tez o pozyczki od mieszczan, kupcow lub bankow"). Czynne przy ustalonych firmach (DsTowns.FarmsLive). Miasta i zamki - DsTowns.cs.
    ///  - K2 NADANIE WOJENNE g (0, 0.5, 1, 1.5, 2 - krok DsGrantStep, najwyzej DsGrantMax): sesja w dni kwartalu (doba gry podzielna przez 91), w dobie ustalenia firm
    ///    (sesja przelaczenia - bez kroku), po DsGrantSessionDays dobach nowej wojny (pierwsze nadanie wojny - bez kroku) i po DsGrantSessionDays dobach pokoju (g = 0).
    ///    Prognoza z dob wojny w ostatnich 28 (pamiec sesji): POTRZEBA = zwrot zoldu nalezny + potrzeba W5 + dary + kontrakty + raty 186 + raty reparacji + wezwania
    ///    + DsGrantRentsKeep x zwykle renty 180 (srednia z okna pomiaru firm) + 1/91 luki do zapasu wojny; ZWYKLE WPLYWY = firmy korony + clo + pomoc lennikow + 1/9 +
    ///    inne + 1/450 zapasu (w oknie pomiaru clo i pomoc lennikow liczone tak, jak beda w wojnie: x DsCustomsWarMultiplier, x DsVassalAidWarPercent / CrownDuesWarPercent);
    ///    PLON(g) = min(DsAidesMax, g x DsAidesRate) x sprzedaz kas + min(pulap, g x DsTallageRate) x zarobek wazony (W 0.5 / R 1 / P 1.5 / podzamcze 1) + g x DsMovablesRate
    ///    x oszczednosci ponad zapas / 91 + g x czesc wsi W4. g = najmniejsze z siatki pokrywajace (potrzeba - zwykle wplywy), w wojnie najmniej jeden krok; zmiana
    ///    najwyzej o krok na kwartal; polityka War Tax +1 krok (najwyzej DsGrantMax). g stale do nastepnej sesji - bez petli z kiesami.
    ///  - M8 podatek od zarobku 184: s = min(pulap, g x DsTallageRate) (KingdomTreasury.WarTaxDaily - zamiast kroku tygodnia; pulap DsTallageMax / z War Tax DsTallageMaxPolicy).
    ///  - M9 DZIESIATA OD RUCHOMOSCI LUDU (TownFolk.PayAid zamiast P1): po sesji wojennej klasa W/R/P miasta i podzamcze dostaja wymiar g x DsMovablesRate x kiesa ponad
    ///    zapas dni (przy pierwszym poborze po sesji) plus niezaplacona reszta poprzedniego; 13 rat co 7 dob (dzien raty osady), najwyzej kiesa ponad zapas dni;
    ///    oblezenie / zdobycie - rata darowana. Kiesa klasy -> skarbiec (wplyw dnia przez okno MovWin w CrownIncome.Begin).
    ///  - K3 garnek W5 = wplywy wojenne dnia: dziesiata (M9, kiesy i kasy) + 184 + pomoc od sprzedazy (M7) + W4 - zamiast P1 (KingdomTreasury.NeedAidKingdom).
    ///  - K5 brak na zwrot zoldu (KingdomTreasury.WageRefund, przed Bankiem 186): skarbiec ponad zapas wojny (TreasuryForRefund; bez zlota winnego Bankowi i miastom),
    ///    potem zaliczka mieszczan pod nadanie (L1, TownFolk.Anticipate - limit AnticipationRoom: niezebrane raty dziesiatej + prognoza W4 do konca kwartalu), na koncu Bank.
    ///    Zwrot zoldu nietkniety i dalej pierwszy z wydatkow wojska; kolejnosc wydatkow korony bez zmian.
    /// Linie: "Daniny korony (DS): dzien N" (co dobe, krolestwa) i "Korona: nadanie wojenne (DS): dzien N" (w dobie sesji). Zapis w arm_daniny_miasta (DsTowns.Export):
    /// g~krolestwo~g~numer~doba~pokoj~wojna~zwykle renty~suma~n i v~osada~klasa~krolestwo~reszta~rata~numer. Brak = zera.
    /// </summary>
    internal static class DsSeason
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int Ring = 28, MinDays = 7, PostRampSample = 7;   // fin74: sesja 'po rampie' 7 dob po koncu rampy F3, srednia z 7 ostatnich probek
        private static readonly double[] Wt = { 0.5, 1.0, 1.5, 1.0 };   // wagi klas 184: W, R, P, podzamcze

        private sealed class GK
        {
            public string Name = "";
            public double G; public int Serial, Day = -1, Peace, War;
            public double URentSum; public int URentN; public double URent = double.NaN;
            public readonly double[] RNeed = new double[Ring], RInc = new double[Ring], RS = new double[Ring], RE = new double[Ring], RM = new double[Ring], RV = new double[Ring], RRent = new double[Ring];
            public readonly double[] ROth = new double[Ring];   // fin75: 'inne' (d.Other + trzecie 178) osobno - w prognozie mediana (jednorazowe lupy / wyrownania nie zawyzaja)
            public readonly bool[] RW = new bool[Ring]; public int H, F;
            public double DSales, DEarn, DMov, DVil;          // bazy doby (zerowane po probce)
            public long DAides, DW4, DCust;                   // Levies (zerowane w LeviesBegin)
            public long MovWin;                               // dziesiata od ostatniego CrownIncome.End (wplyw dnia)
            public long DMovPaid, DTreas, DAnt; public double DMovRemit, DMovShort, DMovAssess;
            public double LNeed, LInc, LY, LG0; public int LDays;
        }
        private sealed class Mov { public string K; public double Left, Per, Cur; public int Serial; }   // Cur - wymiar biezacej sesji nadania (limit L1, recenzja 17.10)

        private static readonly Dictionary<string, GK> _k = new Dictionary<string, GK>();
        private static readonly Dictionary<string, Mov> _mov = new Dictionary<string, Mov>();   // "osada~klasa" -> dziesiata ludu
        private static int _stumbles, _stumblesSess;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static double _movLost;

        internal static void Reset() { _k.Clear(); _mov.Clear(); _stumbles = _stumblesSess = 0; _err.Clear(); _movLost = 0; }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesSess++;
            try { if (_err.Add(where)) Log.Error("DsSeason." + where, e); } catch { }
        }

        internal static bool Live { get { return DsTowns.FarmsLive; } }
        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }
        private static double Cl(double v, double lo, double hi) { return double.IsNaN(v) ? lo : (v < lo ? lo : (v > hi ? hi : v)); }
        private static double StepOf(Settings s) { double x = Cl(s.DsGrantStep, 0.1, 1.0); return x; }
        private static double MaxOf(Settings s) { return Math.Max(StepOf(s), Cl(s.DsGrantMax, 0.5, 4.0)); }
        private static int SessionDays(Settings s) { return Math.Max(1, Math.Min(91, s.DsGrantSessionDays)); }

        private static Kingdom KOf(Settlement st)
        {
            var c = st != null ? st.OwnerClan : null;
            var k = c != null ? c.Kingdom : null;
            return k != null && !k.IsEliminated ? k : null;
        }

        private static GK GkOf(Kingdom k, bool create)
        {
            if (k == null || k.StringId == null) return null;
            GK g;
            if (_k.TryGetValue(k.StringId, out g)) return g;
            if (!create || k.StringId.IndexOf('|') >= 0 || k.StringId.IndexOf('~') >= 0) return null;
            g = new GK();
            _k[k.StringId] = g;
            return g;
        }

        private static bool WarTaxPolicy(Kingdom k) { try { return k != null && k.ActivePolicies.Contains(DefaultPolicies.WarTax); } catch { return false; } }

        // ============================================================ odczyty
        /// <summary>Nadanie g krolestwa (0 poza F3 albo bez nadania).</summary>
        internal static double GrantOf(Kingdom k)
        {
            if (!Live || k == null) return 0;
            var g = GkOf(k, false);
            return g != null ? g.G : 0;
        }

        /// <summary>Nadanie g i numer sesji wojennej (wymiar dziesiatej: nowy numer = nowy wymiar).</summary>
        internal static double GrantNow(Kingdom k, out int serial)
        {
            serial = -1;
            if (!Live || k == null) return 0;
            var g = GkOf(k, false);
            if (g == null) return 0;
            serial = g.Serial;
            return g.G;
        }

        /// <summary>Pulap podatku od zarobku (M8): DsTallageMax, z polityka War Tax DsTallageMaxPolicy.</summary>
        internal static double TallageCap(Kingdom k, Settings s)
        {
            double a = Cl(s.DsTallageMax, 0, 0.3), b = Cl(s.DsTallageMaxPolicy, 0, 0.3);
            return WarTaxPolicy(k) ? Math.Max(a, b) : a;
        }

        /// <summary>M8: stawka s podatku 184 krolestwa z nadania - min(pulap, g x DsTallageRate); 0 poza F3 albo bez nadania.</summary>
        internal static double TallageS(Kingdom k)
        {
            var s = Settings.Current;
            if (!Live || s == null || k == null) return 0;
            double g = GrantOf(k);
            if (!(g > 0)) return 0;
            return Math.Round(Math.Min(TallageCap(k, s), g * Cl(s.DsTallageRate, 0, 0.1)), 6);
        }

        // ============================================================ bazy doby i wplywy (liczniki)
        internal static void NoteSales(Settlement st, double x) { try { if (!(x > 0) || !DsTowns.On) return; var g = GkOf(KOf(st), true); if (g != null) g.DSales += x; } catch (Exception e) { Stumble("NoteSales", e); } }

        /// <summary>TownFolk.PayCrown: zarobek okna klasy (podstawa 184) - zarobek wazony W 0.5 / R 1 / P 1.5 / podzamcze 1 (prognoza nadania).</summary>
        internal static void NoteEarn(Settlement st, int cls, double earned)
        {
            try
            {
                if (cls < 0 || cls >= Wt.Length || !(earned > 0) || double.IsInfinity(earned) || !DsTowns.On) return;
                var g = GkOf(KOf(st), true); if (g != null) g.DEarn += earned * Wt[cls];
            }
            catch (Exception e) { Stumble("NoteEarn", e); }
        }

        /// <summary>TownFolk.PayAid i DsTowns (kasa): oszczednosci ponad zapas - podstawa dziesiatej (prognoza nadania).</summary>
        internal static void NoteMovBase(Settlement st, double ov) { try { if (!(ov > 0) || double.IsInfinity(ov) || !DsTowns.On) return; var g = GkOf(KOf(st), true); if (g != null) g.DMov += ov; } catch (Exception e) { Stumble("NoteMovBase", e); } }

        /// <summary>DsVillage (przy wozie i od markietanow): udzial korony W4 na jednostke g (czesc wsi po dziesiecinie x DsWarShareVillage x rampa F1) - prognoza nadania.</summary>
        internal static void NoteVillageBase(Kingdom k, double perG) { try { if (!(perG > 0) || double.IsInfinity(perG) || !DsTowns.On) return; var g = GkOf(k, true); if (g != null) g.DVil += perG; } catch (Exception e) { Stumble("NoteVillageBase", e); } }

        /// <summary>KingdomTreasury.Levies (poczatek): liczniki W4 i cla tej doby od zera (pomoc od sprzedazy - takze zaleglosc z dnia raty w PopulationLaw - zeruje Daily).</summary>
        internal static void LeviesBegin() { foreach (var g in _k.Values) { g.DW4 = 0; g.DCust = 0; } }
        internal static void NoteW4(Kingdom k, long x) { if (x <= 0 || !DsTowns.On) return; var g = GkOf(k, true); if (g != null) g.DW4 += x; }
        internal static void NoteCustoms(Kingdom k, long x) { if (x <= 0 || !DsTowns.On) return; var g = GkOf(k, true); if (g != null) g.DCust += x; }
        internal static void NoteAides(Kingdom k, long x) { if (x <= 0) return; var g = GkOf(k, true); if (g != null) g.DAides += x; }

        /// <summary>Dziesiata od ruchomosci zaplacona do skarbca (kiesa albo kasa -> skarbiec juz przelane) - okno wplywu dnia 165.</summary>
        internal static void NoteMovables(Kingdom k, long x) { if (x <= 0) return; var g = GkOf(k, true); if (g != null) { g.MovWin += x; g.DMovPaid += x; } }

        /// <summary>CrownIncome.Begin: dziesiata od ostatniego End (wplyw dnia, "nasze").</summary>
        internal static long MovWindow(Kingdom k) { var g = GkOf(k, false); return g != null ? Math.Max(0, g.MovWin) : 0; }

        /// <summary>CrownIncome.End: okno wplywu dnia dziesiatej zamkniete (jak okno 184).</summary>
        internal static void WindowClear() { foreach (var g in _k.Values) g.MovWin = 0; }

        /// <summary>K3: garnek W5 krolestwa - wplywy wojenne dnia DS (dziesiata, 184, pomoc od sprzedazy, W4).</summary>
        internal static long WarPot(Kingdom k, CrownIncome.KDay d)
        {
            if (k == null || d == null) return 0;
            var g = GkOf(k, false);
            return Math.Max(0, d.DsMov) + Math.Max(0, d.WarTax) + (g != null ? g.DAides + g.DW4 : 0);
        }

        /// <summary>L1: raty nadania wplacone dzis (dziesiata + W4) - pierwsze na splate zaliczek mieszczan.</summary>
        internal static long InstalmentsToday(Kingdom k)
        {
            var d = CrownIncome.DayOf(k);
            var g = GkOf(k, false);
            return (d != null ? Math.Max(0, d.DsMov) : 0) + (g != null ? Math.Max(0, g.DW4) : 0);
        }

        // ============================================================ K5: skarbiec ponad zapas wojny i limit zaliczki
        /// <summary>K5 krok 1 (KingdomTreasury.WageRefund, przed zaliczka i Bankiem): ile brakujacego zwrotu pokryje skarbiec ponad zapas wojny - bez reszty wplywow dnia
        /// (juz w "have"). Recenzja 17.10: czynne przy firmach F3 ALBO przy samym F1 (DuesMedieval); dlugi NIE sa odejmowane od skarbca (dlug to roszczenie, a zwrot ze
        /// skarbca kosztuje mniej niz Bank na 20-30%) - poduszka tylko na raty Banku z najblizszych 28 dob. Tylko rachunek i licznik; wyplaca WageRefund.</summary>
        internal static long TreasuryForRefund(Kingdom k, long gap)
        {
            try
            {
                var s = Settings.Current;
                if (!(Live || DsVillage.Live) || s == null || !s.DsRefundTreasuryFirst || k == null || gap <= 0) return 0;
                long res = KingdomTreasury.WarReserveFor(k);
                if (res < 0) return 0;
                double free = (double)k.KingdomBudgetWallet - Math.Max(0, CrownIncome.LeftFor(k)) - res - CrownBorrow.InstalmentsAhead(k, Ring);
                long x = (long)Math.Floor(Math.Min((double)gap, free));
                if (x <= 0) return 0;
                var g = GkOf(k, Live); if (g != null) g.DTreas += x;     // przy samym F1 bez stanu F3 (zapis arm_daniny_miasta bez wpisow)
                var d = CrownIncome.DayOf(k); if (d != null) d.DsTreas += x;
                if (!Live) DsVillage.NoteK5(x);
                return x;
            }
            catch (Exception e) { Stumble("TreasuryForRefund", e); return 0; }
        }

        internal static void NoteAnticipation(Kingdom k, long x)
        {
            if (x <= 0) return;
            var g = GkOf(k, true); if (g != null) g.DAnt += x;
            var d = CrownIncome.DayOf(k); if (d != null) d.DsAnt += x;
        }

        /// <summary>L1 limit (przed odjeciem dlugu zaliczek): niezebrana dziesiata od ruchomosci krolestwa (kiesy i kasy) z wymiaru BIEZACEJ sesji nadania (recenzja 17.10:
        /// reszty z poprzednich kwartalow nie sa podstawa zaliczki - moga nigdy nie wplynac) + prognoza W4 do konca kwartalu.</summary>
        internal static double AnticipationRoom(Kingdom k)
        {
            try
            {
                if (!Live || k == null || k.StringId == null) return 0;
                var g = GkOf(k, false);
                int serial = g != null ? g.Serial : -1;
                double left = 0;
                foreach (var m in _mov.Values) if (m.K == k.StringId && m.Serial == serial && m.Left > 0) left += Math.Min(m.Left, Math.Max(0, m.Cur));
                left += DsTowns.MovLeftOf(k.StringId, serial);
                if (g != null && g.G > 0 && g.F > 0)
                {
                    double v = 0; for (int i = 0; i < g.F; i++) v += g.RV[i];
                    v /= g.F;
                    left += g.G * v * (DsTowns.Quarter - Today % DsTowns.Quarter);
                }
                return left;
            }
            catch (Exception e) { Stumble("AnticipationRoom", e); return 0; }
        }

        // ============================================================ M9 dziesiata od ruchomosci ludu (TownFolk.PayAid)
        /// <summary>
        /// TownFolk.PayAid przy ustalonych firmach (zamiast daniny P1): `ov` - kiesa klasy ponad zapas dni, `avail` - stan kiesy. Po sesji wojennej - wymiar g x DsMovablesRate x ov
        /// (plus niezaplacona reszta poprzedniego), w dzien raty osady 1/13 wymiaru, najwyzej ov i kiesa. Zwraca CALE zloto, ktore wolajacy zdejmuje z kiesy klasy; skarbiec
        /// dostaje to samo (ostatni krok). Oblezenie / zdobycie - rata darowana. Nie rzuca.
        /// </summary>
        internal static long FolkMovables(Settlement st, int cls, double ov, long avail)
        {
            try
            {
                var s = Settings.Current;
                if (!Live || s == null || st == null || st.StringId == null || cls < 0 || cls > 3 || !(ov >= 1) || double.IsInfinity(ov)) return 0;
                var k = KOf(st);
                string key = st.StringId + "~" + cls.ToString(Inv);
                Mov m; _mov.TryGetValue(key, out m);
                if (m != null && (k == null || m.K != k.StringId)) { _movLost += m.Left; _mov.Remove(key); m = null; }   // osada zmienila krolestwo - wymiar starej korony przepada
                if (k == null) return 0;
                var g = GkOf(k, false);
                if (g != null && g.G > 0 && g.Serial > 0 && (m == null || m.Serial != g.Serial))
                {
                    double a = g.G * Cl(s.DsMovablesRate, 0, 0.3) * ov;
                    if (m == null) { m = new Mov { K = k.StringId }; _mov[key] = m; }
                    m.Left += a; m.Per = m.Left / DsTowns.Instalments; m.Cur = a; m.Serial = g.Serial;
                    g.DMovAssess += a;
                }
                if (m == null || m.Left < 1 || !DsTowns.PayDay(st)) return 0;
                double inst = Math.Min(m.Per, m.Left);
                if (DsTowns.RemitOf(st) != 0)
                {
                    m.Left -= inst; m.Cur = Math.Max(0, m.Cur - inst); if (g != null) g.DMovRemit += inst;
                    if (m.Left < 1) _mov.Remove(key);
                    return 0;
                }
                // recenzja 17.10: w przejsciu F3 rata x rampa (P1 x (1 - rampa) dalej bierze ze starej drogi) - reszta raty darowana, nie dlug
                double rn = DsTowns.NewShare;
                if (rn < 1)
                {
                    double off = inst * (1 - rn);
                    m.Left -= off; m.Cur = Math.Max(0, m.Cur - off); inst -= off; if (g != null) g.DMovRemit += off;
                    if (m.Left < 1) { _mov.Remove(key); return 0; }
                }
                long pay = (long)Math.Floor(Math.Min(inst, Math.Min(ov, (double)Math.Max(0, avail))));
                if (pay > int.MaxValue) pay = int.MaxValue;
                if (g != null && pay + 0.5 < inst) g.DMovShort += inst - pay;
                if (pay <= 0) return 0;
                m.Left -= pay; m.Cur = Math.Max(0, m.Cur - pay);
                if (m.Left < 1) _mov.Remove(key);
                k.KingdomBudgetWallet += (int)pay;                   // odbiorca: skarbiec (platnik - kiesa klasy u wolajacego, to samo zloto) - ostatni krok
                NoteMovables(k, pay);
                return pay;
            }
            catch (Exception e) { Stumble("FolkMovables", e); return 0; }
        }

        /// <summary>
        /// ds-akumulator (recenzja; klucz DsHoardDue, TownFolk.PayHoard): srednia doby raty M9 klasy `cls` osady - rata (Per, najwyzej Left) x rampa F3 (jak w FolkMovables)
        /// / 7 dob (raty co 7 dob, DsTowns.PayDay). Wymiar czynny (Left >= 1) tej samej korony, bez darowania (oblezenie / zdobycie - RemitOf); inaczej 0. Tylko odczyt
        /// (nic nie zmienia). Danina od skarbca ludu liczy te warstwe oszczednosci jako zaplacona co dzien, nie tylko w dzien raty. Nie rzuca.
        /// </summary>
        internal static double FolkMovablesDay(Settlement st, int cls)
        {
            try
            {
                if (!Live || st == null || st.StringId == null || cls < 0 || cls > 3) return 0;
                Mov m;
                if (!_mov.TryGetValue(st.StringId + "~" + cls.ToString(Inv), out m) || m == null || m.Left < 1) return 0;
                var k = KOf(st);
                if (k == null || m.K != k.StringId || DsTowns.RemitOf(st) != 0) return 0;
                double inst = Math.Min(m.Per, m.Left) * Cl(DsTowns.NewShare, 0, 1);
                return inst > 0 ? inst / 7.0 : 0;
            }
            catch (Exception e) { Stumble("FolkMovablesDay", e); return 0; }
        }

        // ============================================================ wylaczenie F3
        /// <summary>DsTowns.DayStart (F3 wylaczone): nadania do zera, niezebrana dziesiata ludu przepada (roszczenie, nie pieniadz).</summary>
        internal static void Off()
        {
            foreach (var g in _k.Values) { g.G = 0; g.MovWin = 0; }
            foreach (var m in _mov.Values) _movLost += m.Left;
            _mov.Clear();
        }

        // ============================================================ doba (ArmouryBehavior: po krokach rent i W5, przed WarTaxDaily - DayOf czynne)
        private static string N0(double v) { return double.IsNaN(v) || double.IsInfinity(v) ? "0" : Math.Round(v).ToString("0", Inv); }
        private static string G2(double v) { return v.ToString("0.0#", Inv); }

        /// <summary>Bieg fin74 (17.10): srednia z OSTATNICH 'last' probek wojny (pierscien od glowy H) - sesja 'po rampie' liczy zwykle wplywy juz bez starych poborow
        /// (P1, zawor, podatek od skarbca ludu), ktore w oknie pomiaru firm zawyzaly prognoze (np. King's Landing: 85k prognozy wobec ok. 25k po rampie, g zostalo 0.5).</summary>
        /// <summary>fin75: mediana z ostatnich 'last' probek wojny (pierscien od glowy H); brak - 0.</summary>
        private static double MedWar(double[] a, GK g, int last)
        {
            var v = new List<double>(Ring);
            for (int j = 1; j <= Math.Min(last, g.F); j++) { int i = (g.H - j + Ring * 2) % Ring; if (g.RW[i]) v.Add(a[i]); }
            if (v.Count == 0) return 0;
            v.Sort();
            int m = v.Count / 2;
            return v.Count % 2 == 1 ? v[m] : 0.5 * (v[m - 1] + v[m]);
        }

        private static double AvgLast(double[] a, GK g, int last, out int used)
        {
            double t = 0; used = 0;
            for (int j = 1; j <= Math.Min(last, g.F); j++) { int i = (g.H - j + Ring * 2) % Ring; if (!g.RW[i]) continue; t += a[i]; used++; }
            return used > 0 ? t / used : 0;
        }

        private static double Avg(double[] a, bool[] w, int n, bool warOnly, out int used)
        {
            double t = 0; used = 0;
            for (int i = 0; i < n; i++) { if (warOnly && !w[i]) continue; t += a[i]; used++; }
            return used > 0 ? t / used : 0;
        }

        private static double Yield(Kingdom k, Settings s, double g, double S, double E, double M, double V)
        {
            double aid = Math.Min(Cl(s.DsAidesMax, 0, 0.3), g * Cl(s.DsAidesRate, 0, 0.1)) * S;
            double tal = Math.Min(TallageCap(k, s), g * Cl(s.DsTallageRate, 0, 0.1)) * E;
            double mov = g * Cl(s.DsMovablesRate, 0, 0.3) * M / DsTowns.Quarter;
            return aid + tal + mov + g * V;
        }

        /// <summary>Probka doby krolestwa do pierscienia 28 dob: potrzeba, zwykle wplywy, bazy plonu nadania (pamiec sesji).</summary>
        private static void Sample(Kingdom k, GK g, CrownIncome.KDay d, bool war, bool live, Settings s)
        {
            double urent = g.URent;
            if (double.IsNaN(urent))
            {
                if (g.URentN > 0) urent = g.URentSum / g.URentN;
                else { int n; urent = Avg(g.RRent, g.RW, g.F, false, out n); if (n == 0) urent = d.Rent; }
            }
            long res = KingdomTreasury.WarReserveFor(k);
            double gap = res > 0 ? Math.Max(0, res - (double)k.KingdomBudgetWallet) / DsTowns.Quarter : 0;
            double need = d.RefundDue + KingdomTreasury.DsW5Need(k) + d.GiftOut + d.Contract + d.LoanOut + d.RepOut + d.CallToWar + Cl(s.DsGrantRentsKeep, 0, 1) * Math.Max(0, urent) + gap;
            long farmT, farmC; TownPurse.CrownToday.TryGetValue(k, out farmT); CastlePurse.CrownToday.TryGetValue(k, out farmC);
            long va; KingdomTreasury.DuesToday.TryGetValue(k, out va);
            double cust = g.DCust, vav = va;
            if (!live && war)
            {
                cust *= Math.Max(1.0, (double)s.DsCustomsWarMultiplier);
                double w0 = Math.Max(0.01, (double)s.CrownDuesWarPercent);
                vav *= Math.Max(0, (double)s.DsVassalAidWarPercent) / w0;
            }
            double oth = Ransom178.ThirdsOf(k) + d.Other;
            // f19-e3 (klucz Ks Farm Catch Up One Off): gersuma korony tej doby (jednorazowa, w TownPurse.CrownToday) - do 'inne' (mediana), nie do firm (srednia)
            long gersC = Math.Max(0L, Math.Min(farmT, DsTowns.GersumaCrownOf(k)));
            farmT -= gersC; oth += gersC;
            double inc = farmT + farmC + cust + vav + d.Release;   // fin75: bez 'inne' (osobno w ROth, mediana w sesji)
            int h = g.H;
            g.ROth[h] = oth;
            g.RNeed[h] = need; g.RInc[h] = inc; g.RS[h] = g.DSales; g.RE[h] = g.DEarn; g.RM[h] = g.DMov; g.RV[h] = g.DVil; g.RRent[h] = d.Rent; g.RW[h] = war;
            g.H = (g.H + 1) % Ring; if (g.F < Ring) g.F++;
        }

        private static string Session(Kingdom k, GK g, bool war, bool free, string why, Settings s, int last = 0)
        {
            double g0 = g.G, g1;
            int n = 0;
            double need = 0, inc = 0, y1 = 0;
            if (!war) g1 = 0;
            else
            {
                double step = StepOf(s), max = MaxOf(s);
                need = last > 0 ? AvgLast(g.RNeed, g, last, out n) : Avg(g.RNeed, g.RW, g.F, true, out n);
                inc = last > 0 ? AvgLast(g.RInc, g, last, out n) : Avg(g.RInc, g.RW, g.F, true, out n);
                inc += MedWar(g.ROth, g, last > 0 ? last : Ring);   // fin75: 'inne' mediana z dob wojny (jednorazowy lup 793k w King's Landing dal srednia 149k)
                int n2;
                double S = last > 0 ? AvgLast(g.RS, g, last, out n2) : Avg(g.RS, g.RW, g.F, true, out n2), E = last > 0 ? AvgLast(g.RE, g, last, out n2) : Avg(g.RE, g.RW, g.F, true, out n2),
                       M = last > 0 ? AvgLast(g.RM, g, last, out n2) : Avg(g.RM, g.RW, g.F, true, out n2), V = last > 0 ? AvgLast(g.RV, g, last, out n2) : Avg(g.RV, g.RW, g.F, true, out n2);
                y1 = Yield(k, s, 1.0, S, E, M, V);
                if (n < MinDays && !free) g1 = Math.Max(step, g0);                 // za malo dob pomiaru (po wczytaniu) - nadanie bez zmian
                else
                {
                    double deficit = need - inc;
                    g1 = max;
                    for (double x = step; x <= max + 1e-9; x += step) if (Yield(k, s, x, S, E, M, V) >= deficit) { g1 = x; break; }
                    if (!free && g0 > 0) g1 = Math.Max(g0 - step, Math.Min(g0 + step, g1));
                    if (WarTaxPolicy(k)) g1 = Math.Min(max, g1 + step);
                    g1 = Math.Max(step, Math.Min(max, g1));
                }
            }
            g1 = Math.Round(g1, 3);
            g.LG0 = g0; g.LNeed = need; g.LInc = inc; g.LY = y1; g.LDays = n;
            g.G = g1; g.Day = Today;
            if (war && g1 > 0) g.Serial++;
            if (!war && !(g0 > 0)) return null;   // pokoj bez nadania - bez wiersza
            return g.Name + " " + why + ": g " + G2(g0) + " -> " + G2(g1) + (WarTaxPolicy(k) && war ? " (War Tax +krok)" : "") + (war ? ", potrzeba " + N0(need) + ", zwykle wplywy " + N0(inc)
                   + ", brak " + N0(need - inc) + ", plon nadania przy g=1 " + N0(y1) + " (dob wojny w pomiarze " + n + ")" : ", pokoj");
        }

        /// <summary>ArmouryBehavior (po CrownRents/W5 i LordsEvenUp, przed WarTaxDaily - CrownIncome.DayOf czynne): probki doby, zwykle renty przy przelaczeniu, sesje nadan
        /// (kwartal, przelaczenie, nowa wojna, pokoj), zaliczki po kwartale do zwyklego dlugu, linie "Daniny korony (DS)" i "Korona: nadanie wojenne (DS)".</summary>
        internal static void Daily()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || Campaign.Current == null) return;
                if (!DsTowns.On) { foreach (var g0 in _k.Values) { g0.DSales = g0.DEarn = g0.DMov = g0.DVil = 0; g0.DAides = 0; } _stumbles = 0; return; }
                int today = Today, day = today - 1, sd = SessionDays(s);
                bool live = Live, sw = DsTowns.SwitchDue, quarter = today % DsTowns.Quarter == 0;
                if (sw)
                    foreach (var g0 in _k.Values) if (g0.URentN > 0) { g0.URent = g0.URentSum / g0.URentN; g0.URentSum = 0; g0.URentN = 0; }
                var rows = new List<KeyValuePair<double, string>>();
                var sess = new List<string>();
                double wNeed = 0, wInc = 0, wFarm = 0, wCust = 0, wVa = 0, wAid = 0, wTal = 0, wMov = 0, wW4 = 0, wTr = 0, wAnt = 0, wBank = 0, wRefG = 0, wRefD = 0;
                int warK = 0, gK0 = 0, gK1 = 0, gK2 = 0;
                foreach (var k in Kingdom.All)
                {
                    try
                    {
                        if (k == null || k.IsEliminated || k.StringId == null) continue;
                        var g = GkOf(k, true);
                        if (g == null) continue;
                        g.Name = k.Name != null ? k.Name.ToString() : k.StringId;
                        bool war = KingdomTreasury.AtWar(k);
                        if (war) { g.War++; g.Peace = 0; warK++; } else { g.Peace++; g.War = 0; }
                        var d = CrownIncome.DayOf(k);
                        if (d != null) Sample(k, g, d, war, live, s);
                        if (DsTowns.InWindow && d != null) { g.URentSum += d.Rent; g.URentN++; }
                        if (live)
                        {
                            if (quarter) { try { TownFolk.AntToNormal(k); } catch (Exception e) { Stumble("Daily(zaliczki)", e); } }
                            bool peaceEnd = g.G > 0 && !war && g.Peace >= sd;
                            bool warStart = !(g.G > 0) && war && g.War >= sd && DsTowns.Quarter - today % DsTowns.Quarter > sd;   // dzien kwartalu blisko - nadanie w nim (bez dwoch wymiarow dziesiatej naraz)
                            int rampD = Math.Max(0, Math.Min(364, s.DsRampDays));
                            bool postRamp = DsTowns.FixDay >= 0 && today == DsTowns.FixDay + rampD + PostRampSample;   // fin74: sesja swobodna po rampie, z probek po rampie
                            string why = sw ? "przelaczenie" : quarter ? "kwartal" : postRamp ? "po rampie" : peaceEnd ? "pokoj " + g.Peace + " dob" : warStart ? "nowa wojna " + g.War + " dob" : null;
                            if (why != null) { string line = Session(k, g, war, sw || postRamp || !(g.G > 0), why, s, postRamp ? PostRampSample : 0); if (line != null) sess.Add(line); }
                        }
                        if (g.G <= 0) gK0++; else if (g.G < 1.25) gK1++; else gK2++;
                        if (d != null && (war || g.G > 0 || d.DsTreas > 0 || d.DsAnt > 0))
                        {
                            long farmT, farmC; TownPurse.CrownToday.TryGetValue(k, out farmT); CastlePurse.CrownToday.TryGetValue(k, out farmC);
                            long va; KingdomTreasury.DuesToday.TryGetValue(k, out va);
                            int last = (g.H + Ring - 1) % Ring;
                            double need = g.F > 0 ? g.RNeed[last] : 0, inc = g.F > 0 ? g.RInc[last] + g.ROth[last] : 0;   // linia: zwykle wplywy doby z 'inne'
                            long warIn = Math.Max(0, d.DsMov) + Math.Max(0, d.WarTax) + g.DAides + g.DW4;
                            double gapD = need - inc - warIn;
                            wNeed += need; wInc += inc; wFarm += farmT + farmC; wCust += g.DCust; wVa += va; wAid += g.DAides; wTal += d.WarTax; wMov += d.DsMov; wW4 += g.DW4;
                            wTr += d.DsTreas; wAnt += d.DsAnt; wBank += d.LoanIn; wRefG += d.RefundGiven; wRefD += d.RefundDue;
                            var sb1 = new StringBuilder(220);
                            sb1.Append(g.Name).Append(": g ").Append(G2(g.G)).Append(war ? "" : " (pokoj)")
                               .Append(", firmy ").Append(farmT + farmC).Append(", clo ").Append(g.DCust).Append(", pomoc lennikow ").Append(va)
                               .Append(", pomoc od sprzedazy ").Append(g.DAides).Append(", 184 ").Append(d.WarTax).Append(", dziesiata ").Append(d.DsMov).Append(", W4 ").Append(g.DW4)
                               .Append(", inne ").Append(d.Other + Ransom178.ThirdsOf(k)).Append(", zapas 1/450 ").Append(d.Release)
                               .Append(" | potrzeba ").Append(N0(need)).Append(", luka ").Append(N0(gapD))
                               .Append(" | zwrot ").Append(d.RefundGiven).Append('/').Append(d.RefundDue)
                               .Append(d.DsTreas > 0 ? ", ze skarbca ponad zapas " + d.DsTreas : "").Append(d.DsAnt > 0 ? ", zaliczka mieszczan " + d.DsAnt : "")
                               .Append(d.LoanIn > 0 ? ", Bank " + d.LoanIn : "").Append(", renty ").Append(d.Rent);
                            if (g.DMovAssess > 0 || g.DMovRemit > 0 || g.DMovShort > 0)
                                sb1.Append(" | dziesiata ludu: wymiar ").Append(N0(g.DMovAssess)).Append(", darowane ").Append(N0(g.DMovRemit)).Append(", niezaplacone ").Append(N0(g.DMovShort));
                            rows.Add(new KeyValuePair<double, string>(need, sb1.ToString()));
                        }
                    }
                    catch (Exception e) { Stumble("Daily(krolestwo)", e); }
                }
                DsTowns.SwitchDue = false;
                if (s.LogEnabled)
                {
                    if (sess.Count > 0)
                        Log.Info("Korona: nadanie wojenne (DS): dzien " + day + " | sesje " + sess.Count + " (siatka g co " + G2(StepOf(s)) + " do " + G2(MaxOf(s)) + "; plon: pomoc od sprzedazy min("
                                 + (100 * Cl(s.DsAidesMax, 0, 0.3)).ToString("0.#", Inv) + "%, g x " + (100 * Cl(s.DsAidesRate, 0, 0.1)).ToString("0.#", Inv) + "%), 184 min(pulap, g x "
                                 + (100 * Cl(s.DsTallageRate, 0, 0.1)).ToString("0.#", Inv) + "%), dziesiata g x " + (100 * Cl(s.DsMovablesRate, 0, 0.3)).ToString("0.#", Inv)
                                 + "% oszczednosci ponad zapas na kwartal, W4 g x czesc wsi) | " + string.Join(" ; ", sess.ToArray()) + ".");
                    if (live || DsTowns.InWindow)
                    {
                        rows.Sort((x, y) => y.Key.CompareTo(x.Key));
                        var txt = new List<string>(); foreach (var r in rows) txt.Add(r.Value);
                        double mLeft = 0; foreach (var m in _mov.Values) mLeft += m.Left;
                        var sb = new StringBuilder(4096);
                        sb.Append("Daniny korony (DS): dzien ").Append(day).Append(live ? "" : " | POMIAR (stare zasady - liczby nadania tylko do prognozy)")
                          .Append(" | krolestw w wojnie ").Append(warK).Append("; g: 0 - ").Append(gK0).Append(", do 1 - ").Append(gK1).Append(", ponad 1 - ").Append(gK2)
                          .Append(" | swiat: firmy korony ").Append(N0(wFarm)).Append(", clo ").Append(N0(wCust)).Append(", pomoc lennikow ").Append(N0(wVa))
                          .Append(", pomoc od sprzedazy ").Append(N0(wAid)).Append(", 184 ").Append(N0(wTal)).Append(", dziesiata ").Append(N0(wMov)).Append(", W4 ").Append(N0(wW4))
                          .Append(" | potrzeba ").Append(N0(wNeed)).Append(", zwykle wplywy ").Append(N0(wInc))
                          .Append(" | zwrot ").Append(N0(wRefG)).Append('/').Append(N0(wRefD)).Append(" (").Append(wRefD > 0 ? (100.0 * wRefG / wRefD).ToString("0.0", Inv) + "%" : "-").Append(")")
                          .Append(", brak zwrotu pokryty: skarbiec ponad zapas ").Append(N0(wTr)).Append(", zaliczki mieszczan ").Append(N0(wAnt)).Append(", Bank ").Append(N0(wBank))
                          .Append(" | dziesiata ludu niezebrana ").Append(N0(mLeft)).Append(" (").Append(_mov.Count).Append(" klaso-osad), przepadla ").Append(N0(_movLost))
                          .Append(" | na krolestwo: ").Append(txt.Count > 0 ? string.Join(" ; ", txt.ToArray()) : "-");
                        if (_stumbles > 0) sb.Append(" | potkniecia ").Append(_stumbles).Append(" (w sesji ").Append(_stumblesSess).Append(")");
                        sb.Append('.');
                        Log.Info(sb.ToString());
                    }
                }
                foreach (var g0 in _k.Values) { g0.DSales = g0.DEarn = g0.DMov = g0.DVil = 0; g0.DAides = 0; g0.DMovPaid = g0.DTreas = g0.DAnt = 0; g0.DMovRemit = g0.DMovShort = g0.DMovAssess = 0; }
                _movLost = 0; _stumbles = 0;
                // krolestwa, ktorych juz nie ma - poza stanem
                var gone = new List<string>();
                foreach (var kv in _k) { bool live2 = false; foreach (var k in Kingdom.All) if (k != null && !k.IsEliminated && k.StringId == kv.Key) { live2 = true; break; } if (!live2) gone.Add(kv.Key); }
                foreach (var id in gone) _k.Remove(id);
            }
            catch (Exception e) { Stumble("Daily", e); }
        }

        // ============================================================ zapis (czesc arm_daniny_miasta)
        private static bool BadId(string id) { return string.IsNullOrEmpty(id) || id.IndexOf('|') >= 0 || id.IndexOf('~') >= 0; }

        internal static void Export(StringBuilder sb)
        {
            try
            {
                foreach (var kv in _k)
                {
                    var g = kv.Value;
                    if (BadId(kv.Key)) continue;
                    sb.Append("|g~").Append(kv.Key).Append('~').Append(g.G.ToString("R", Inv)).Append('~').Append(g.Serial.ToString(Inv)).Append('~').Append(g.Day.ToString(Inv))
                      .Append('~').Append(g.Peace.ToString(Inv)).Append('~').Append(g.War.ToString(Inv)).Append('~').Append(double.IsNaN(g.URent) ? "-" : Math.Round(g.URent, 2).ToString("R", Inv))
                      .Append('~').Append(Math.Round(g.URentSum, 2).ToString("R", Inv)).Append('~').Append(g.URentN.ToString(Inv));
                }
                foreach (var kv in _mov)
                {
                    var m = kv.Value;
                    int t = kv.Key.LastIndexOf('~');
                    if (t <= 0 || m.Left < 1 || BadId(m.K)) continue;
                    string st = kv.Key.Substring(0, t), cls = kv.Key.Substring(t + 1);
                    if (BadId(st)) continue;
                    sb.Append("|v~").Append(st).Append('~').Append(cls).Append('~').Append(m.K).Append('~').Append(Math.Round(m.Left, 2).ToString("R", Inv)).Append('~')
                      .Append(Math.Round(m.Per, 2).ToString("R", Inv)).Append('~').Append(m.Serial.ToString(Inv)).Append('~').Append(Math.Round(m.Cur, 2).ToString("R", Inv));
                }
            }
            catch (Exception e) { Stumble("Export", e); }
        }

        /// <summary>DsTowns.Import: wpis "g~..." albo "v~..." (inne rodzaje pomija).</summary>
        internal static void ImportPart(string[] a)
        {
            try
            {
                double d; int n;
                if (a[0] == "g" && a.Length >= 10 && !BadId(a[1]))
                {
                    var g = new GK();
                    if (double.TryParse(a[2], NumberStyles.Float, Inv, out d) && !double.IsNaN(d) && !double.IsInfinity(d)) g.G = Math.Max(0, Math.Min(4, d));
                    if (int.TryParse(a[3], NumberStyles.Integer, Inv, out n)) g.Serial = Math.Max(0, n);
                    if (int.TryParse(a[4], NumberStyles.Integer, Inv, out n)) g.Day = n;
                    if (int.TryParse(a[5], NumberStyles.Integer, Inv, out n)) g.Peace = Math.Max(0, n);
                    if (int.TryParse(a[6], NumberStyles.Integer, Inv, out n)) g.War = Math.Max(0, n);
                    if (a[7] != "-" && double.TryParse(a[7], NumberStyles.Float, Inv, out d) && d >= 0 && !double.IsInfinity(d)) g.URent = d;
                    if (double.TryParse(a[8], NumberStyles.Float, Inv, out d) && d >= 0 && !double.IsInfinity(d)) g.URentSum = d;
                    if (int.TryParse(a[9], NumberStyles.Integer, Inv, out n)) g.URentN = Math.Max(0, n);
                    _k[a[1]] = g;
                }
                else if (a[0] == "v" && a.Length >= 7 && !BadId(a[1]) && !BadId(a[3]))
                {
                    var m = new Mov { K = a[3] };
                    if (!double.TryParse(a[4], NumberStyles.Float, Inv, out m.Left) || !(m.Left >= 1) || double.IsInfinity(m.Left)) return;
                    if (!double.TryParse(a[5], NumberStyles.Float, Inv, out m.Per) || !(m.Per > 0) || double.IsInfinity(m.Per)) m.Per = m.Left / DsTowns.Instalments;
                    if (int.TryParse(a[6], NumberStyles.Integer, Inv, out n)) m.Serial = n;
                    if (!(a.Length >= 8 && double.TryParse(a[7], NumberStyles.Float, Inv, out m.Cur) && m.Cur >= 0 && !double.IsInfinity(m.Cur))) m.Cur = m.Left;   // starszy zapis - cala reszta
                    _mov[a[1] + "~" + a[2]] = m;
                }
            }
            catch (Exception e) { Stumble("ImportPart", e); }
        }
    }
}
