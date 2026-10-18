using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// f5-lud (noc 17/18.10; Jeff "ma byc gra rano ... z poprawnie dzialajaca ekonomia", STAN-PRAC ### 72 i ### 82; po fali 4) - KAZDA KLASA LUDU MIAST Z KS ZARABIA
    /// WEDLUG SWOJEGO KOSZYKA.
    /// Dowod (fin88, Armoury-2026-10-18_03-13-38.log, 160 dob sciezki Jeffa): lud miast +59.6 tys./d (NAKUM-1a), W 75.7 -> 80.8 dni koszyka mimo f4 - kappa W (f4-place-ludu)
    /// stala na podlodze 0.10 od ok. 30 dob po wlaczeniu KS, bo tnie tylko place z kas i od zamoznych domow, a ok. 230 tys./d dochodu W idzie inna droga (uslugi
    /// zolnierzom, karawanom i bandom 80%, dwor 80%, budowy 60%, warsztaty 70%) - to samo co koszyk W po cenach KS (146 tys./d) i jeszcze polowa. Model f4 (simw3.py) bral
    /// ten dochod za staly 197 tys./d i staly wydatek W 275 tys./d, a wydatek W w grze idzie za kiesa (oferta 1/60 nadwyzki) - wiec przewidywal 29 dni, a W stanely na
    /// ok. 80. R i P rosly (+19 i +17.5 tys./d), bo zatrzymane place W zostawaly w kiesach R/P (174 tys./d) albo w kasach, skad wyplata kupcom M6 (1/28 nadwyzki kas)
    /// oddawala je P i R (M6 15 -> 247 tys./d); trzymala je tylko danina 3%/d. Kotwica KS zmienila tez sklad klas: potrzeba W 240 -> 146, R 48 -> 150, P 8 -> 124 tys./d,
    /// a podzial dochodu (SplitOf) zostal na klasach BK.
    /// Regula (tylko miasta z zastosowana KS i z Ks Labour To Basket - ta sama rodzina co f4 (b)):
    ///  - W2 (klucz Ks Folk Income All Kinds): czesc W kazdego zarobku z zewnatrz (TownFolk.Earn: uslugi, dwor, budowy, kopalnie, place warsztatow) i przelewow gracza przez
    ///    kase (EarnFromTown) x kappa W2 - ta sama regula co kappa f4 (pasmo Ks Labour Basket Low/High Days, najnizej Ks Labour Basket Min, krok Ks Labour Basket Step
    ///    Percent), wlasna pamiec (od 1, bez skoku dochodu przy wgraniu). Place z kas i od domow - kappa f4 jak dotad.
    ///  - R i P (klucz Ks Folk Income R P): czesc R / P kazdego zarobku z zewnatrz, robocizny i doplaty z kasy (R), obrotu (P), wyplaty kupcom M6 (P i R) i plac od P dla R
    ///    z jego nadwyzki x kappa klasy - pasmo Ks Folk Income R/P Low/High Days (21-42 / 30-60 = zapas dni klasy do dwoch razy tyle), najnizej Ks Folk Income Min,
    ///    krok Ks Folk Income Step Percent.
    ///  - PODLOGA PRZEZYCIA: klasa z kiesa ponizej dolnej granicy pasma - pelna stawka od razu; bez miary (kotwica jeszcze nie liczona) - kappa z zapisu bez zmiany;
    ///    klasa bez koszyka w miescie z policzona kotwica (Ks Folk Income Zero Need: obozy Wolnych Ludzi, khalasary - potrzeba 0, a dochod szedl do kiesy, ktorej nie ma
    ///    na co wydac; fin88: 8 miast, W 2.86 mln) - jak ponad pasmem.
    ///  - (c) danina od skarbca R/P (Ks Folk Income Hoard Falls) schodzi z Ds Hoard Due Rate do Ks Labour Basket Hoard Rate razem z kappa klasy (jak W w f4) - rzadki tallage.
    ///  - Przeglad firmy wedlug nadwyzki kasy (Ks Farm Follows Surplus, DsTowns.SurplusReview): kasa ponad 1.5 x zapas kupcow rosnie - firma miasta w gore o przyrost
    ///    kasy na dobe co Ks Farm Surplus Review Days (najwyzej Ks Farm Surplus Review Max firmy), przeglad kwartalny bez obnizki, dopoki nadwyzka trwa.
    /// ZAMKNIETY PIENIADZ: czego lud nie przyjal, zostaje u placacego - zarobek z zewnatrz: wolajacy Earn daje kasie miasta reszte (ten sam protokol co przy kiesie
    /// nieczynnej - Earn zwraca kwote przyjeta); z kasy i M6: nigdy nie schodzi z kasy; od P dla R: zostaje w kiesie P. Kasy oddaja to panom i koronie firma (przeglad
    /// wedlug nadwyzki). Tozsamosci w linii doby: pelne = przyjete + zostalo u placacych (kazda droga).
    /// Linia doby "Dochod ludu wedlug koszyka (f5-lud): dzien N" (TownFolk.DailyLive po linii f4). Zapis arm_ks_dochod: "f5|W2:miasto:k,...|R:...|P:...".
    /// Wyjatek per miasto - licznik potkniec, bez globalnego wylacznika (CLAUDE.md 7).
    /// </summary>
    internal static class KsFolkIncome
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly string[] CName = { "W2", "R", "P" };
        // kappa klas miasta (brak wpisu = 1): [0] W2 - zarobek W poza placami z kas i od domow, [1] R, [2] P
        private static readonly Dictionary<string, float>[] _k = { new Dictionary<string, float>(StringComparer.Ordinal), new Dictionary<string, float>(StringComparer.Ordinal), new Dictionary<string, float>(StringComparer.Ordinal) };
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static int _stum;
        // liczniki doby (DayLine zeruje) - klasy W, R, P
        private static readonly double[] _eFull = new double[3], _ePaid = new double[3];                              // z zewnatrz (Earn): pelne / przyjete
        private static readonly double[] _cFull = new double[3], _cRule = new double[3], _cPaid = new double[3];      // z kas (krok 2 R i P, przelewy gracza): pelne / zlecone / zeszlo
        private static readonly double[] _mFull = new double[2], _mRule = new double[2], _mPaid = new double[2];      // M6: [0] P, [1] R
        private static double _hFull, _hPaid;                                                                          // place od P dla R z nadwyzki
        private static readonly double[] _hbBase = new double[3], _hbTake = new double[3]; private static double _hrR, _hrP; private static int _hrN;   // danina od skarbca
        private static int _dTowns;
        private static readonly int[,] _band = new int[3, 5];                                                          // ponizej / w pasmie / ponad / bez koszyka / bez miary
        private static readonly int[] _kLow = new int[3];
        private static readonly List<float>[] _kap = { new List<float>(), new List<float>(), new List<float>() };
        private static readonly List<float>[] _days = { new List<float>(), new List<float>(), new List<float>() };
        private static readonly double[] _sumK = new double[3], _sumN = new double[3];
        private static long _ticks;
        // przeglad firmy wedlug nadwyzki kasy (DsTowns): doba (DayLine zeruje) i sesja
        private static int _revN, _revNoCut, _revNS; private static double _revAmt, _revAmtS;

        // ------------------------------------------------------------ klucze
        private static Settings S { get { return Settings.Current; } }
        internal static bool AllKindsOn { get { var s = S; return s != null && s.KsLabourToBasket && s.KsFolkIncomeAllKinds; } }
        internal static bool RPOn { get { var s = S; return s != null && s.KsLabourToBasket && s.KsFolkIncomeRP; } }
        internal static bool ZeroOn { get { var s = S; return s != null && s.KsLabourToBasket && s.KsFolkIncomeZeroNeed; } }
        internal static bool HoardOn { get { var s = S; return s != null && s.KsLabourToBasket && s.KsFolkIncomeRP && s.KsFolkIncomeHoardFalls; } }
        internal static bool AnyOn { get { return AllKindsOn || RPOn; } }

        private static double Lo(int c)
        {
            var s = S;
            if (c == 0) return KsFolkWage.Lo;
            if (s == null) return c == 1 ? 21.0 : 30.0;
            return Math.Max(1.0, Math.Min(240.0, c == 1 ? s.KsFolkIncomeRLowDays : s.KsFolkIncomePLowDays));
        }

        private static double Hi(int c)
        {
            var s = S;
            if (c == 0) return KsFolkWage.Hi;
            double lo = Lo(c);
            if (s == null) return lo * 2.0;
            return Math.Max(lo + 1.0, Math.Min(480.0, c == 1 ? s.KsFolkIncomeRHighDays : s.KsFolkIncomePHighDays));
        }

        private static double Min(int c)
        {
            var s = S;
            if (c == 0) return KsFolkWage.Min;
            return s != null ? Math.Max(0.0, Math.Min(1.0, s.KsFolkIncomeMin)) : 0.10;
        }

        private static double Step(int c)
        {
            var s = S;
            if (s == null) return 0.03;
            return Math.Max(0.005, Math.Min(0.2, (c == 0 ? s.KsLabourBasketStepPercent : s.KsFolkIncomeStepPercent) / 100.0));
        }

        internal static void Reset()
        {
            for (int c = 0; c < 3; c++) _k[c].Clear();
            _err.Clear(); _stum = 0; Zero(); _revNS = 0; _revAmtS = 0;
        }

        private static void Zero()
        {
            for (int c = 0; c < 3; c++)
            {
                _eFull[c] = _ePaid[c] = _cFull[c] = _cRule[c] = _cPaid[c] = 0;
                _hbBase[c] = _hbTake[c] = 0; _kLow[c] = 0; _sumK[c] = _sumN[c] = 0;
                _kap[c].Clear(); _days[c].Clear();
                for (int b = 0; b < 5; b++) _band[c, b] = 0;
            }
            _mFull[0] = _mFull[1] = _mRule[0] = _mRule[1] = _mPaid[0] = _mPaid[1] = 0;
            _hFull = _hPaid = 0; _hrR = _hrP = 0; _hrN = 0; _dTowns = 0; _ticks = 0;
            _revN = _revNoCut = 0; _revAmt = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stum++;
            try { if (_err.Add(where)) Log.Error("KsFolkIncome." + where, e); } catch { }
        }

        /// <summary>Regula czynna w tym miescie: klucz (jeden z dwoch), Ks Labour To Basket i KS zastosowany w miescie (ta sama bramka co f4 (b)).</summary>
        internal static bool Active(Settlement st)
        {
            try { return AnyOn && KsFolkWage.Active(st); }
            catch (Exception e) { Stumble("Active", e); return false; }
        }

        private static double KOf(int c, Settlement st)
        {
            float k0;
            return st != null && st.StringId != null && _k[c].TryGetValue(st.StringId, out k0) ? k0 : 1.0;
        }

        /// <summary>
        /// TownFolk.TownLive krok 2 (raz na dobe na miasto, przed placami z kasy): nowe kappy klas W2 / R / P z kies i potrzeb klas (kotwica wczoraj). Podloga przezycia
        /// (kiesa ponizej dolnej granicy - 1 od razu), bez miary - bez zmiany, bez koszyka klasy przy policzonej kotwicy (Ks Folk Income Zero Need) - jak ponad pasmem.
        /// Bez kluczy albo miasto bez reguly - nic (ani licznikow). Nie rzuca.
        /// </summary>
        internal static void Day(Settlement st, long kw, long kr, long kp, double nW, double nR, double nP, bool facOk)
        {
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                if (!Active(st)) return;
                _dTowns++;
                string id = st.StringId ?? "";
                bool zero = ZeroOn && facOk;
                for (int c = 0; c < 3; c++)
                {
                    bool on = c == 0 ? AllKindsOn : RPOn;
                    if (!on) continue;
                    long purse = c == 0 ? kw : c == 1 ? kr : kp;
                    double need = c == 0 ? nW : c == 1 ? nR : nP;
                    bool info = need > 0 && !double.IsNaN(need) && !double.IsInfinity(need);
                    double lo = Lo(c), hi = Hi(c), mn = Min(c);
                    double k = KOf(c, st);
                    int band;
                    if (info)
                    {
                        double d = Math.Max(0L, purse) / need;
                        _days[c].Add((float)Math.Min(d, 99999.0)); _sumK[c] += Math.Max(0L, purse); _sumN[c] += need;
                        if (d < lo) { k = 1.0; band = 0; }                         // podloga przezycia: pelna stawka od razu
                        else
                        {
                            double kt = d >= hi ? mn : 1.0 - (d - lo) / (hi - lo) * (1.0 - mn);
                            double stp = Step(c);
                            k += Math.Max(-stp, Math.Min(stp, kt - k));
                            band = d < hi ? 1 : 2;
                        }
                    }
                    else if (zero)
                    {
                        // klasa bez koszyka w miescie z policzona kotwica - jak ponad pasmem (dochod do kiesy, ktorej nie ma na co wydac)
                        double stp = Step(c);
                        k += Math.Max(-stp, Math.Min(stp, mn - k));
                        band = 3;
                    }
                    else band = 4;                                                  // bez miary (kotwica jeszcze nie liczona) - kappa z zapisu bez zmiany
                    k = Math.Max(mn, Math.Min(1.0, k));
                    if (id.Length > 0) { if (k >= 0.99995) _k[c].Remove(id); else _k[c][id] = (float)k; }
                    _band[c, band]++;
                    _kap[c].Add((float)k);
                    if (k < 0.99995) _kLow[c]++;
                }
                if (HoardOn)
                {
                    var s = S;
                    if (s != null) { _hrR += HoardRateAt(s, KOf(1, st)); _hrP += HoardRateAt(s, KOf(2, st)); _hrN++; }
                }
            }
            catch (Exception e) { Stumble("Day", e); }
            finally { _ticks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>Czynniki klas W2 / R / P w miescie z regula (do Earn i EarnFromTown); false - regula nieczynna albo wszystkie 1 (wolajacy idzie stara droga co do monety).</summary>
        internal static bool Factors(Settlement st, out double w2, out double r, out double p)
        {
            w2 = r = p = 1.0;
            try
            {
                if (!Active(st)) return false;
                if (AllKindsOn) w2 = KOf(0, st);
                if (RPOn) { r = KOf(1, st); p = KOf(2, st); }
                return w2 < 1.0 - 1e-9 || r < 1.0 - 1e-9 || p < 1.0 - 1e-9;
            }
            catch (Exception e) { Stumble("Factors", e); w2 = r = p = 1.0; return false; }
        }

        /// <summary>Czynniki R i P (krok 2 i 5 doby ludu, M6); false - Ks Folk Income R P wylaczony, miasto bez reguly albo oba 1.</summary>
        internal static bool RP(Settlement st, out double r, out double p)
        {
            r = p = 1.0;
            try
            {
                if (!RPOn || !KsFolkWage.Active(st)) return false;
                r = KOf(1, st); p = KOf(2, st);
                return r < 1.0 - 1e-9 || p < 1.0 - 1e-9;
            }
            catch (Exception e) { Stumble("RP", e); r = p = 1.0; return false; }
        }

        /// <summary>Ks Folk Income Zero Need dla kappy W f4 (KsFolkWage.WFactor): klasa W bez koszyka w miescie z KS i policzona kotwica - potrzeba podana jako 1e-9
        /// (kiesa W w dniach ogromna = ponad pasmem); inaczej potrzeba bez zmiany (klucz wylaczony - co do liczby jak dotad).</summary>
        internal static double NeedForF4(Settlement st, bool facOk, double needW)
        {
            try
            {
                if (needW > 0 || !facOk || !ZeroOn || !KsFolkWage.Active(st)) return needW;
                return 1e-9;
            }
            catch (Exception e) { Stumble("NeedForF4", e); return needW; }
        }

        /// <summary>(c) stawka danin od skarbca klasy R (c 1) / P (c 2) w miescie z regula: od Ds Hoard Due Rate (kappa 1) do min(Ds Hoard Due Rate, Ks Labour Basket Hoard Rate)
        /// (kappa najnizej), liniowo z kappa klasy (jak W w f4); -1 - bez zmiany (klucz wylaczony, miasto bez reguly, inna klasa).</summary>
        internal static double HoardRate(Settlement st, int c)
        {
            try
            {
                var s = S;
                if (s == null || (c != 1 && c != 2) || !HoardOn || !KsFolkWage.Active(st)) return -1.0;
                return HoardRateAt(s, KOf(c, st));
            }
            catch (Exception e) { Stumble("HoardRate", e); return -1.0; }
        }

        private static double HoardRateAt(Settings s, double k)
        {
            double a = s.DsHoardDueRate, b = s.KsLabourBasketHoardRate;
            if (double.IsNaN(a)) a = 0; if (double.IsNaN(b)) b = 0;
            double hiR = Math.Max(0.0, Math.Min(0.1, a)), loR = Math.Max(0.0, Math.Min(0.1, Math.Min(a, b)));
            double mn = Math.Max(0.0, Math.Min(1.0, s.KsFolkIncomeMin)), sh = mn >= 1.0 - 1e-9 ? 1.0 : (k - mn) / (1.0 - mn);
            if (double.IsNaN(sh)) sh = 1.0;
            return loR + (hiR - loR) * Math.Max(0.0, Math.Min(1.0, sh));
        }

        // ------------------------------------------------------------ liczniki drog (tylko arytmetyka; wolajacy w swoim try)
        internal static void NoteExt(long fW, long fR, long fP, long aW, long aR, long aP)
        {
            _eFull[0] += fW; _eFull[1] += fR; _eFull[2] += fP; _ePaid[0] += aW; _ePaid[1] += aR; _ePaid[2] += aP;
        }

        internal static void NoteCoffer(int c, long full, long rule, long paid)
        {
            if (c < 0 || c > 2) return;
            _cFull[c] += Math.Max(0, full); _cRule[c] += Math.Max(0, rule); _cPaid[c] += Math.Max(0, paid);
        }

        internal static void NoteM6(long fullP, long fullR, long ruleP, long ruleR, long paidP, long paidR)
        {
            _mFull[0] += fullP; _mFull[1] += fullR; _mRule[0] += ruleP; _mRule[1] += ruleR; _mPaid[0] += paidP; _mPaid[1] += paidR;
        }

        internal static void NoteHouse(long full, long paid) { _hFull += Math.Max(0, full); _hPaid += Math.Max(0, paid); }

        /// <summary>TownFolk.PayHoard (danina od skarbca przy firmach DS): podstawa (kiesa klasy ponad poziom normalny sprzed daniny tej doby) i pobrane - tylko klasy W/R/P
        /// miast z regula (stawka efektywna w linii).</summary>
        internal static void NoteHoard(Settlement st, int c, double basis, long took)
        {
            try
            {
                if (c < 0 || c > 2 || !Active(st)) return;
                if (basis > 0 && !double.IsInfinity(basis)) _hbBase[c] += basis;
                if (took > 0) _hbTake[c] += took;
            }
            catch (Exception e) { Stumble("NoteHoard", e); }
        }

        /// <summary>DsTowns.SurplusReview: podwyzka firmy miasta (zl/rok).</summary>
        internal static void NoteRaise(double perYear) { if (perYear > 0) { _revN++; _revAmt += perYear; _revNS++; _revAmtS += perYear; } }

        /// <summary>DsTowns.Review / Renew: obnizka firmy wstrzymana przy nadwyzce kasy.</summary>
        internal static void NoteNoCut() { _revNoCut++; }

        private static float Med(List<float> v) { if (v.Count == 0) return float.NaN; v.Sort(); return v[v.Count / 2]; }
        private static float MinOf(List<float> v) { if (v.Count == 0) return float.NaN; float m = v[0]; foreach (var x in v) if (x < m) m = x; return m; }
        private static string F(double v, string fmt) { return double.IsNaN(v) || double.IsInfinity(v) ? "-" : v.ToString(fmt, Inv); }
        private static long L(double v) { return (long)Math.Round(v); }

        /// <summary>TownFolk.DailyLive (po linii f4): linia doby. Bez kluczy, bez miast z regula i bez przegladu firm tej doby - nic (log jak dotad).</summary>
        internal static void DayLine(int day)
        {
            try
            {
                var s = S;
                if (s == null) return;
                if (_dTowns == 0 && _revN == 0 && _revNoCut == 0) return;
                long t0 = Stopwatch.GetTimestamp();
                long gold = 0, over = 0; int nT = 0, nOver = 0;
                foreach (var t in Town.AllTowns)
                {
                    try
                    {
                        if (t == null || t.Settlement == null || !StartCalibration.KsTownApplied(t)) continue;
                        nT++; gold += Math.Max(0, t.Gold);
                        double lim = DsTowns.OverReserve * TownPurse.Reserve(t);
                        if (t.Gold > lim) { nOver++; over += (long)Math.Floor(t.Gold - lim); }
                    }
                    catch (Exception e) { Stumble("DayLine.kasa", e); }
                }
                long eF = L(_eFull[0] + _eFull[1] + _eFull[2]), eP = L(_ePaid[0] + _ePaid[1] + _ePaid[2]), eC = eF - eP;
                long cF = L(_cFull[0] + _cFull[1] + _cFull[2]), cQ = L(_cRule[0] + _cRule[1] + _cRule[2]), cP = L(_cPaid[0] + _cPaid[1] + _cPaid[2]), cC = cF - cQ;
                long mF = L(_mFull[0] + _mFull[1]), mQ = L(_mRule[0] + _mRule[1]), mP = L(_mPaid[0] + _mPaid[1]), mC = mF - mQ;
                long hF = L(_hFull), hP = L(_hPaid), hC = hF - hP;
                double hb = _hbBase[0] + _hbBase[1] + _hbBase[2], ht = _hbTake[0] + _hbTake[1] + _hbTake[2];
                double hiR = Math.Max(0.0, Math.Min(0.1, s.DsHoardDueRate)), loR = Math.Max(0.0, Math.Min(0.1, Math.Min(s.DsHoardDueRate, s.KsLabourBasketHoardRate)));
                int rd = Math.Max(7, Math.Min(91, s.KsFarmSurplusReviewDays));
                var sb = new StringBuilder(2000);
                sb.Append("Dochod ludu wedlug koszyka (f5-lud): dzien ").Append(day)
                  .Append(" | klucze: Ks Folk Income All Kinds ").Append(AllKindsOn ? "TAK" : "NIE").Append(", Ks Folk Income R P ").Append(RPOn ? "TAK" : "NIE")
                  .Append(", Ks Folk Income Hoard Falls ").Append(HoardOn ? "TAK" : "NIE").Append(", Ks Folk Income Zero Need ").Append(ZeroOn ? "TAK" : "NIE")
                  .Append(", Ks Farm Follows Surplus ").Append(s.KsFarmFollowsSurplus ? "TAK" : "NIE").Append(" (co ").Append(rd).Append(" dob, najwyzej +")
                  .Append((100 * Math.Max(0.0, Math.Min(1.0, (double)s.KsFarmSurplusReviewMax))).ToString("0", Inv)).Append("%)")
                  .Append(" | pasma dni koszyka KS: W ").Append(Lo(0).ToString("0", Inv)).Append('-').Append(Hi(0).ToString("0", Inv)).Append(" (najnizej x").Append(Min(0).ToString("0.00", Inv))
                  .Append("), R ").Append(Lo(1).ToString("0", Inv)).Append('-').Append(Hi(1).ToString("0", Inv)).Append(", P ").Append(Lo(2).ToString("0", Inv)).Append('-').Append(Hi(2).ToString("0", Inv))
                  .Append(" (najnizej x").Append(Min(1).ToString("0.00", Inv)).Append("), krok W/RP ").Append((100 * Step(0)).ToString("0.0", Inv)).Append('/').Append((100 * Step(1)).ToString("0.0", Inv)).Append("%/d")
                  .Append(" | miasta z regula ").Append(_dTowns).Append(": kappa W2/R/P mediana ");
                for (int c = 0; c < 3; c++) sb.Append(c > 0 ? "/" : "").Append(F(Med(_kap[c]), "0.000"));
                sb.Append(", min ");
                for (int c = 0; c < 3; c++) sb.Append(c > 0 ? "/" : "").Append(F(MinOf(_kap[c]), "0.000"));
                sb.Append(", miast z kappa < 1 ").Append(_kLow[0]).Append('/').Append(_kLow[1]).Append('/').Append(_kLow[2]);
                string[] bn = { "ponizej pasma (pelna stawka)", "w pasmie", "ponad pasmem", "bez koszyka klasy", "bez miary" };
                for (int b = 0; b < 5; b++) sb.Append(b == 0 ? "; " : ", ").Append(bn[b]).Append(' ').Append(_band[0, b]).Append('/').Append(_band[1, b]).Append('/').Append(_band[2, b]);
                sb.Append(" | dni koszyka W/R/P (kiesy / potrzeba miast z regula) ");
                for (int c = 0; c < 3; c++) sb.Append(c > 0 ? "/" : "").Append(F(_sumN[c] > 0 ? _sumK[c] / _sumN[c] : double.NaN, "0.0"));
                sb.Append(", mediana ");
                for (int c = 0; c < 3; c++) sb.Append(c > 0 ? "/" : "").Append(F(Med(_days[c]), "0.0"));
                sb.Append(" | doba: z zewnatrz (uslugi, dwor, budowy, kopalnie, warsztaty) pelne ").Append(eF).Append(" = przyjete ").Append(eP).Append(" + oddane kasom miast ").Append(eC)
                  .Append(" (W ").Append(L(_eFull[0])).Append('/').Append(L(_ePaid[0])).Append(", R ").Append(L(_eFull[1])).Append('/').Append(L(_ePaid[1]))
                  .Append(", P ").Append(L(_eFull[2])).Append('/').Append(L(_ePaid[2])).Append(')')
                  .Append(" | z kas (robocizna i doplata R, obrot P, przelewy gracza) pelne ").Append(cF).Append(" = zlecone ").Append(cQ).Append(" + zostalo w kasach ").Append(cC)
                  .Append(", zaplacone ").Append(cP).Append(" (kontrola zlecone - zaplacone = ").Append(cQ - cP).Append("; W ").Append(L(_cFull[0])).Append('/').Append(L(_cPaid[0]))
                  .Append(", R ").Append(L(_cFull[1])).Append('/').Append(L(_cPaid[1])).Append(", P ").Append(L(_cFull[2])).Append('/').Append(L(_cPaid[2])).Append(')')
                  .Append(" | wyplata kupcom M6 pelna ").Append(mF).Append(" = zlecona ").Append(mQ).Append(" + zostalo w kasach ").Append(mC).Append(", zaplacona ").Append(mP)
                  .Append(" (kontrola zlecona - zaplacona = ").Append(mQ - mP).Append("; P ").Append(L(_mFull[0])).Append('/').Append(L(_mPaid[0])).Append(", R ").Append(L(_mFull[1])).Append('/').Append(L(_mPaid[1])).Append(')')
                  .Append(" | place od P dla R pelne ").Append(hF).Append(" = zaplacone ").Append(hP).Append(" + zostalo u P ").Append(hC)
                  .Append(" | razem niezaplacone ludowi: w kasach miast ").Append(eC + cC + mC).Append(", u P ").Append(hC)
                  .Append(" | danina od skarbca W/R/P w tych miastach: podstawa ").Append(L(hb)).Append(", pobrano ").Append(L(ht)).Append(" (W ").Append(L(_hbTake[0])).Append(" / R ").Append(L(_hbTake[1]))
                  .Append(" / P ").Append(L(_hbTake[2])).Append("), stawka efektywna ").Append(F(hb > 0 ? 100.0 * ht / hb : double.NaN, "0.00")).Append("%/d; stawka R/P dzis srednio ")
                  .Append(F(_hrN > 0 ? 100.0 * _hrR / _hrN : double.NaN, "0.00")).Append('/').Append(F(_hrN > 0 ? 100.0 * _hrP / _hrN : double.NaN, "0.00"))
                  .Append("% (od ").Append((100 * hiR).ToString("0.00", Inv)).Append("% do ").Append((100 * loR).ToString("0.00", Inv)).Append("%)")
                  .Append(" | kasy miast z KS razem ").Append(gold).Append(" (miast ").Append(nT).Append(", ponad 1.5 x zapas kupcow ").Append(nOver).Append(", nadwyzka ponad 1.5 x zapas ").Append(over).Append(')')
                  .Append(" | przeglad firmy wedlug nadwyzki kasy: podwyzek dzis ").Append(_revN).Append(" (+").Append(L(_revAmt)).Append(" zl/rok), od startu sesji ").Append(_revNS)
                  .Append(" (+").Append(L(_revAmtS)).Append(" zl/rok), przeglad zwykly bez obnizki przy nadwyzce dzis ").Append(_revNoCut)
                  .Append(" | czas ").Append(((_ticks + (Stopwatch.GetTimestamp() - t0)) * 1000.0 / Stopwatch.Frequency).ToString("0.00", Inv)).Append(" ms | potkniecia ").Append(_stum).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("DayLine", e); }
            finally { Zero(); _stum = 0; }
        }

        // ------------------------------------------------------------ zapis (klucz arm_ks_dochod)
        /// <summary>"f5|W2:miasto:k,...|R:...|P:..."; null - nic do zapisania (wszystko 1 - klucza nie piszemy, zapis jak dotad).</summary>
        internal static string Export()
        {
            try
            {
                bool any = false;
                for (int c = 0; c < 3; c++) if (_k[c].Count > 0) any = true;
                if (!any) return null;
                var sb = new StringBuilder("f5");
                for (int c = 0; c < 3; c++)
                {
                    sb.Append('|').Append(CName[c]).Append(':');
                    bool first = true;
                    foreach (var kv in _k[c])
                    {
                        if (string.IsNullOrEmpty(kv.Key) || kv.Key.IndexOfAny(new[] { ':', ',', '|' }) >= 0) continue;
                        if (!first) sb.Append(',');
                        first = false;
                        sb.Append(kv.Key).Append(':').Append(Math.Max(0f, Math.Min(1f, kv.Value)).ToString("0.####", Inv));
                    }
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return null; }
        }

        /// <summary>Odczyt arm_ks_dochod; brak klucza (stary zapis, Ironman Jeffa) albo inny format - kappy 1 (schodza krokiem dobowym od pierwszej doby).</summary>
        internal static void Import(string s)
        {
            for (int c = 0; c < 3; c++) _k[c].Clear();
            if (string.IsNullOrEmpty(s) || !s.StartsWith("f5|", StringComparison.Ordinal) || s.Length > 600000) return;
            try
            {
                foreach (var part in s.Split('|'))
                {
                    int c = part.StartsWith("W2:", StringComparison.Ordinal) ? 0 : part.StartsWith("R:", StringComparison.Ordinal) ? 1 : part.StartsWith("P:", StringComparison.Ordinal) ? 2 : -1;
                    if (c < 0) continue;
                    string body = part.Substring(part.IndexOf(':') + 1);
                    if (body.Length == 0) continue;
                    foreach (var ent in body.Split(','))
                    {
                        if (_k[c].Count >= 4000) break;
                        var f = ent.Split(':');
                        float v;
                        if (f.Length != 2 || f[0].Length == 0 || !float.TryParse(f[1], NumberStyles.Float, Inv, out v) || !(v >= 0f && v < 1f)) continue;
                        _k[c][f[0]] = v;
                    }
                }
            }
            catch (Exception e) { for (int c = 0; c < 3; c++) _k[c].Clear(); Stumble("Import", e); }
        }
    }
}
