using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// f7-wegiel (noc 18.10; fin91 Armoury-2026-10-18_06-29-09.log NOP-8 / NRU-8 "kopalnia bez wegla" 93.8 lup/d, bez KS 28.1, fin88 45.4) - WEGLARZE PRZY KOPALNI.
    ///
    /// Przyczyna (liczby z fin91, ostatnie 28 dob pelnego KS 109207-109234):
    ///  - drewna na swiecie jest dosc: "Towary hardwood" 109234 produkcja 6057 / zuzycie 5754 ladunkow/d, zapas 84150 (+303/d), miast bez drewna 3 z 97; dymarki "brak na polce"
    ///    0-141 ladunkow/d - lasu nie brakuje;
    ///  - brakuje ZLOTA kopaln: "brak zlota wsi" 350-451 ladunkow/d (zakup z polki), "pusta kiesa wsi" 784-924 (wozy F4a), kiesy kopaln mediana 2-51 zl, 14-15 z 25 ponizej
    ///    kosztu drewna doby. Akirys [Volantis] sam daje 39.7 z 93.8 lup/d: 191 tys. ludzi, model 55 lup/d (275 ladunkow drewna), utarg 28 dob 704 zl/d (ks-budzety.csv) - kiesa
    ///    idzie na koszyk ludu, drewno ponad las okregu kosztuje ok. 1550 zl/d; kredyt kupcow f5-ruda zatrzymany zaleglym rachunkiem 15085 zl (42 doby, kiesa 0);
    ///  - f5-ruda (Ks Iron Hands Follow Market) podniosla rece przy zelazie o 59% (82.7 -> 131.9 tys.; model lup 297 -> 477/d), a drewna kupionego za zloto nie przybylo -
    ///    "kopalnia bez wegla" 45.4 (fin88) -> 93.8 lup/d. Reszta: kopalnie pustynne (Bir Seif 11.4, Sunmine 11.1, Tylemos 3.9 lup/d) - las 0, drewno tylko z targu i wozami.
    ///  - Plan KS: popyt drewna dymarek (Ws = zakupy z polek w ramce FBloom) i podaz lasu wsi (cala, takze to, co dymarka zdejmuje przed wozem) - nie poprawiane: drewna
    ///    na swiecie jest nadwyzka, wiecej drwali Planu tylko powiekszyloby zapas (opis w CHANGELOG paczki).
    ///
    /// REGULA (klucz Bloomery Colliers, tylko wies z KS zastosowanym - StartCalibration.AppliedFor): w ticku produkcji wsi (Cap, watek gry), gdy drewna w dymarce nie starcza
    /// na model lup doby (x > sufit), bezczynne rece przy zlozu (te, ktore i tak nie wytopia rudy) tna drewno w lesie okregu i pala je na wegiel dla tej dymarki:
    /// r = ladunki drewna z rak jednej lupy gry (StartCalibration.F7CollierRatio: P0 drwala x przydatnosc lasu blokow z zelazem / P0 zelaza x przydatnosc zloza, jedna tabela
    /// swiata; pustynia i step - 0, wtedy jak dotad). Wytop y = najwiecej calkowitych lup, przy ktorych drewno dymarki + drewno weglarzy z rak (x - y) starcza:
    /// y = floor((drewno w dymarce + r x) / (drewno na lupe + r)); weglarze daja d = y x drewno na lupe - drewno w dymarce ladunkow (rece d / r lup), bezczynne
    /// zostaje x - y - d / r (>= 0). Drewno weglarzy idzie prosto do dymarki (poza rosterami, jak las okregu FromWoodlot) i spala sie w tym samym ticku (TickPostfix);
    /// gdy gra wytopi mniej, niz pozwala sufit, reszta drewna zostaje w dymarce na jutro (prawdziwe drewno, prawdziwe rece). Zakup za zloto, las okregu, kredyt kupcow
    /// i wozy ida PIERWSZE (Daily przed tickiem) - weglarze tylko dopelniaja dzienny brak; kopalnia ze zlotem kupuje dalej jak dotad.
    /// Pieniadze: 0 - weglarze to ludzie wsi gorniczej (ich chleb placi kiesa wsi z utargu lup jak gornikow), drewno z lasu okregu (regale lasu w rekach kopalni, jak
    /// prawo gornicze z drewnem na kopalnie). Towar: drewno z rak i ziemi -> wegiel -> lupa (5 ladunkow na lupe, jak kazda lupa); nic z niczego.
    /// Pomiar: "kopalnia bez wegla" (linia "Zelazo (lupy)", NOP-8 / NRU-8, pierscien f5-ruda) liczy odtad tylko rece BEZCZYNNE (bez rak weglarzy - one pracuja przy weglu);
    /// rece weglarzy i lupy wytopione dzieki nim osobno: wstawka "weglarze (f7-wegiel)" linii zelaza i linia doby "Weglarze kopaln (f7-wegiel): dzien N" z tozsamosciami:
    /// rece (lupy modelu) = wytopione + weglarze + bezczynne; drewno dymarek: wczoraj + las + targ + kredyt + wozy + weglarze - spalone = dzis (reszta 0).
    /// Klucz NIE - Cap co do bitu jak dotad (bez weglarzy, "kopalnia bez wegla" = model - sufit). Bez zapisu (r liczone co dobe z KS; drewno w dymarce jak dotad w arm_e6_bloom).
    /// Wyjatki liczone (pierwsze 3 w logu), bez globalnych wylacznikow.
    ///
    /// Recenzja (noc 18.10): (1) hamulec f5-ruda - krok tygodniowy F5IronWeek daje rece (x1.05) tylko kopalni, ktorej dymarka miala drewno; rece weglarzy licza sie
    /// do tej decyzji jak brak drewna (pierscien F5Ring.C, F7GrowShare): kopalnia w duzej czesci weglarnia nie rosnie z rekami (z kazdej dodanej lupy rak tylko r / (5 + r)
    /// to lupy), a zjazd x0.95 przy >= 50% zostaje na samych rekach bezczynnych (weglarze pracuja - kopalnia nie oddaje rak polu za to, ze pali wlasny wegiel);
    /// 10-50% - bez zmian. (2) doba po wczytaniu / wlaczeniu KS: rece blokow KS (b.Hands) liczy StartCalibration.Daily PO Bloomery.Daily, a ticki wsi (DailyTickSettlement)
    /// sa rozlozone po dobie - r kopalni bez danych liczone leniwie w jej ticku (raz na dobe, F7Lazy); kopalnia KS dalej bez danych (rece blokow 0) - tick poza
    /// pierscieniem f5-ruda (bez falszywego "pelnego braku drewna" w oknie 7 dob) i osobny licznik w linii.
    /// </summary>
    internal static partial class Bloomery
    {
        private sealed class F7Mine { public float R, Hpb, LumberL; public bool Ks; public int Try = -1; }   // Try - doba ostatniego leniwego liczenia r (F7Lazy)
        private static readonly Dictionary<Village, F7Mine> _f7 = new Dictionary<Village, F7Mine>();
        // liczniki doby (linia) - zerowane w F7Roll (poczatek NewDay, przed zerowaniem licznikow dymarek, ktore czyta tozsamosc drewna)
        private static int _f7Wood, _f7Stum, _f7StumDay, _f7WithForest, _f7NoForest;
        private static int _f7LazyN, _f7LazyOk;          // recenzja: leniwe liczenie r w ticku (kopalnia KS bez danych) - proby / z danymi
        private static float _f7NoData;                  // recenzja: lupy modelu ponad drewno kopalni KS bez danych (rece blokow 0) - poza pierscieniem f5-ruda
        private static readonly HashSet<Village> _f7NoDataV = new HashSet<Village>();
        private static float _f7Coll, _f7Gain, _f7NoLand, _f7Model, _f7Fired, _f7Idle, _f7People, _f7ForestL;
        private static long _f7BankPrev = -1, _f7Ticks, _f7RefreshTicks;
        private static readonly HashSet<Village> _f7NoLandV = new HashSet<Village>();
        private static readonly Dictionary<Village, float[]> _f7Day = new Dictionary<Village, float[]>();   // wies -> {drewno, rece (lupy), lupy dzieki weglarzom}

        /// <summary>Weglarze czynne: lupa w tej kampanii i klucz Bloomery Colliers (KS zastosowany sprawdza F7Refresh na wies).</summary>
        internal static bool F7On { get { var s = Settings.Current; return _applied && s != null && s.BloomeryColliers; } }

        private static void F7Reset()
        {
            _f7.Clear(); _f7NoLandV.Clear(); _f7Day.Clear(); _f7NoDataV.Clear();
            _f7Wood = _f7Stum = _f7StumDay = _f7WithForest = _f7NoForest = 0;
            _f7LazyN = _f7LazyOk = 0; _f7NoData = 0f;
            _f7Coll = _f7Gain = _f7NoLand = _f7Model = _f7Fired = _f7Idle = _f7People = _f7ForestL = 0f;
            _f7BankPrev = -1; _f7Ticks = 0; _f7RefreshTicks = 0;
        }

        private static void F7Stumble(string where, Exception e)
        {
            _f7Stum++; _f7StumDay++;
            if (_f7Stum <= 3) { try { Log.Error("Bloomery.F7" + where, e); } catch { } }
        }

        /// <summary>Bloomery.Daily (watek gry, raz na dobe): r kazdej kopalni z okregu KS (StartCalibration.F7CollierRatio). Klucz NIE - pamiec pusta (Cap bez weglarzy).
        /// Wyjatek jednej kopalni - ta kopalnia bez weglarzy do jutra (licznik), reszta dalej.</summary>
        private static void F7Refresh()
        {
            if (!F7On) { if (_f7.Count > 0) _f7.Clear(); return; }
            long t0 = Stopwatch.GetTimestamp();
            int forest = 0, none = 0; float lsum = 0f;
            foreach (var kv in _mines)
            {
                var v = kv.Key;
                if (v == null) continue;
                try
                {
                    F7Mine f;
                    if (!_f7.TryGetValue(v, out f) || f == null) { f = new F7Mine(); _f7[v] = f; }
                    float hpb = 0f, ll = 0f;
                    f.Ks = StartCalibration.AppliedFor(v);
                    f.R = f.Ks ? StartCalibration.F7CollierRatio(v, out hpb, out ll) : 0f;
                    f.Hpb = hpb; f.LumberL = ll;
                    if (f.Ks && f.Hpb > 0f) { if (f.R > 0f) { forest++; lsum += ll; } else none++; }   // Hpb 0 - okreg bez rak przy zelazie w KS albo bez danych (pierwsza doba po wczytaniu)
                }
                catch (Exception e) { F7Stumble("Refresh", e); F7Mine f; if (_f7.TryGetValue(v, out f) && f != null) f.R = 0f; }
            }
            _f7WithForest = forest; _f7NoForest = none; _f7ForestL = forest > 0 ? lsum / forest : 0f;
            _f7RefreshTicks += Stopwatch.GetTimestamp() - t0;
        }

        /// <summary>Cap (tick produkcji kopalni v, watek gry): drewna w dymarce starcza na `cap` lup przy modelu `x` (x > cap). Bezczynne rece pala wegiel z lasu okregu:
        /// zwraca nowy sufit y (>= cap); coll - rece weglarzy w lupach modelu. Drewno weglarzy dopisane do dymarki DOPIERO po calym rachunku (wyjatek - bez zmian).</summary>
        private static int F7Colliers(Village v, Mine m, float x, int cap, int wpb, out float coll, out bool noData)
        {
            coll = 0f; noData = false;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                F7Mine f;
                if (v == null || m == null || wpb <= 0) return cap;
                if (!_f7.TryGetValue(v, out f) || f == null || !(f.Hpb > 0f)) f = F7Lazy(v, f);   // recenzja: bez danych (wczytanie, wlaczenie) - r w ticku, raz na dobe
                if (f == null) return cap;
                if (!(f.R > 0f))
                {
                    if (f.Ks && f.Hpb > 0f) { _f7NoLand += x - cap; _f7NoLandV.Add(v); }   // kopalnia KS bez lasu (pustynia, step) - jak dotad
                    else if (f.Ks) { noData = true; _f7NoData += x - cap; _f7NoDataV.Add(v); }   // KS bez danych (rece blokow 0) - poza pierscieniem f5-ruda, osobny licznik
                    return cap;
                }
                int avail = Math.Max(0, m.Bank - _tvReserved * wpb);
                double yf = (avail + (double)f.R * x) / (wpb + (double)f.R);
                int y = (int)Math.Floor(yf + 1e-6);
                int top = (int)Math.Ceiling(x - 1e-4f) - 1;                   // y < x zawsze (avail < wpb x); bezpiecznik na zaokraglenia
                if (y > top) y = top;
                if (y <= cap) return cap;
                int d = y * wpb - avail;
                if (d < 0) d = 0;
                float c = d / f.R;
                if (c > x - y + 1e-3f) return cap;                             // nie wiecej rak przy weglu niz bezczynnych (nie powinno sie zdarzyc)
                m.Bank += d;                                                   // drewno weglarzy prosto do dymarki (poza rosterami - jak las okregu)
                coll = c;
                _f7Wood += d; _f7Coll += c; _f7Gain += y - cap; _f7People += c * f.Hpb;
                float[] a;
                if (!_f7Day.TryGetValue(v, out a)) { a = new float[3]; _f7Day[v] = a; }
                a[0] += d; a[1] += c; a[2] += y - cap;
                return y;
            }
            catch (Exception e) { F7Stumble("Colliers", e); coll = 0f; return cap; }
            finally { _f7Ticks += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>Recenzja (F7Colliers, tick wsi v - watek gry): r kopalni bez wpisu albo bez danych KS (Hpb 0) - najwyzej raz na dobe na kopalnie (Try). Po wczytaniu
        /// i w dobie wlaczenia rece blokow liczy StartCalibration.Daily dopiero po Bloomery.Daily (F7Refresh widzi 0), a ticki wsi sa rozlozone po dobie - pierwszy tick
        /// po StartCalibration.Daily ma juz dane. Wyjatek - wpis bez zmian (licznik w F7Colliers).</summary>
        private static F7Mine F7Lazy(Village v, F7Mine f)
        {
            int today = Today5;
            if (f != null && f.Try == today) return f;
            if (f == null) { f = new F7Mine(); _f7[v] = f; }
            f.Try = today;
            float hpb = 0f, ll = 0f;
            f.Ks = StartCalibration.AppliedFor(v);
            f.R = f.Ks ? StartCalibration.F7CollierRatio(v, out hpb, out ll) : 0f;
            f.Hpb = hpb; f.LumberL = ll;
            if (f.Ks) { _f7LazyN++; if (f.Hpb > 0f) _f7LazyOk++; }
            return f;
        }

        /// <summary>F5Sums i StartCalibration.F5IronWeek (przez F7StarvedGrow): czesc modelu 7 dob BEZ drewna kupionego / z lasu / kredytu / wozow - bez drewna (f)
        /// + rece weglarzy - do decyzji o WZROSCIE rak (prog 10%). Klucz NIE albo bez weglarzy w oknie - f co do bitu.</summary>
        private static float F7GrowShare(F5Ring r, float f)
        {
            if (r == null) return f;
            float sc = r.SumC, sm = r.SumM;
            if (!(sc > 0f) || !(sm > 0.01f)) return f;
            return Math.Max(0f, Math.Min(1f, (r.SumU + sc) / sm));
        }

        /// <summary>StartCalibration.F5IronWeek (recenzja f7-wegiel): starv z F5Starved (bez drewna) -> czesc do decyzji o WZROSCIE (bez drewna + rece weglarzy).
        /// starv < 0 (brak danych, bez dymarki) - bez zmian.</summary>
        internal static float F7StarvedGrow(Village v, float starv)
        {
            if (!(starv >= 0f) || v == null) return starv;
            F5Ring r;
            if (!_f5ring.TryGetValue(v, out r) || r == null || r.N <= 0) return starv;
            return F7GrowShare(r, starv);
        }

        /// <summary>Cap (klucz TAK): rachunek rak doby wszystkich kopaln w lupach modelu - model = wytopione (do sufitu) + weglarze (F7Colliers) + bezczynne.</summary>
        private static void F7Note(float x, float fired, float idle)
        {
            _f7Model += x; _f7Fired += fired; _f7Idle += idle;
        }

        private static long F7Banks() { long n = 0; foreach (var m in _mines.Values) n += m.Bank; return n; }

        /// <summary>Wstawka linii "Zelazo (lupy) (214)" po "lupy bez drewna N" (tylko klucz TAK - przy NIE linia co do znaku jak dotad).</summary>
        private static string F7Segment()
        {
            if (!F7On || !(StartCalibration.Applied || _f7Wood > 0)) return "";   // gra przed KS - linia zelaza jak dotad
            var inv = CultureInfo.InvariantCulture;
            return "; weglarze (f7-wegiel): drewno z lasu okregu " + _f7Wood + " ladunkow, rece " + _f7Coll.ToString("0.#", inv) + " lup (ok. " + _f7People.ToString("0", inv)
                   + " ludzi), wytopione dzieki nim " + _f7Gain.ToString("0.#", inv) + " lup, kopalnie bez lasu bez wegla " + _f7NoLand.ToString("0.#", inv) + " lup";
        }

        /// <summary>Bloomery.NewDay (PIERWSZY krok - liczniki dymarek doby jeszcze nie wyzerowane): linia "Weglarze kopaln (f7-wegiel): dzien N" (klucz TAK i KS zastosowany
        /// albo weglarze dzis) i liczniki od zera.</summary>
        private static void F7Roll()
        {
            try
            {
                if (!_applied) { _f7BankPrev = -1; return; }
                long bank = F7Banks();
                bool on = F7On;
                if (on && (StartCalibration.Applied || _f7Wood > 0))
                {
                    var inv = CultureInfo.InvariantCulture;
                    int today = (int)CampaignTime.Now.ToDays;
                    var sb = new StringBuilder(900);
                    sb.Append("Weglarze kopaln (f7-wegiel): dzien ").Append(today).Append(" | klucz Bloomery Colliers TAK")
                      .Append(" | weglarze dzis w ").Append(_f7Day.Count).Append(" kopalniach: drewno z lasu okregu ").Append(_f7Wood).Append(" ladunkow (rece ")
                      .Append(_f7Coll.ToString("0.0", inv)).Append(" lup przy weglu zamiast przy rudzie, ok. ").Append(_f7People.ToString("0", inv)).Append(" ludzi), wytopione dzieki nim ")
                      .Append(_f7Gain.ToString("0.0", inv)).Append(" lup")
                      .Append(" | kopalnie KS bez lasu (przydatnosc lasu 0 - pustynia, step): bez wegla ").Append(_f7NoLand.ToString("0.0", inv)).Append(" lup w ").Append(_f7NoLandV.Count).Append(" kopalniach")
                      .Append(" | rece przy zlozach (lupy modelu): model ").Append(_f7Model.ToString("0.0", inv)).Append(" = wytopione ").Append(_f7Fired.ToString("0.0", inv))
                      .Append(" + weglarze ").Append(_f7Coll.ToString("0.0", inv)).Append(" + bez wegla ").Append(_f7Idle.ToString("0.0", inv)).Append(" (reszta ")
                      .Append((_f7Model - _f7Fired - _f7Coll - _f7Idle).ToString("0.0", inv)).Append(")");
                    if (_f7BankPrev >= 0)
                        sb.Append(" | drewno dymarek (ladunki): w dymarkach wczoraj ").Append(_f7BankPrev).Append(" + las okregu ").Append(_dWoods).Append(" + targ ").Append(_dBought)
                          .Append(" + kredyt ").Append(_f5Lent).Append(" + wozy ").Append(_cArr).Append(" + weglarze ").Append(_f7Wood).Append(" - spalone ").Append(_dBurnt)
                          .Append(" - w dymarkach dzis ").Append(bank).Append(" = reszta ").Append(_f7BankPrev + _dWoods + _dBought + _f5Lent + _cArr + _f7Wood - _dBurnt - bank);
                    else sb.Append(" | drewno dymarek: kontrola od nastepnej doby (w dymarkach dzis ").Append(bank).Append(")");
                    sb.Append(" | pieniadze i towar: 0 zl - drewno weglarzy z lasu okregu rekami wsi gorniczej prosto do dymarki (bez polki, bez zakupu, bez kredytu); lupy na targ jak dotad");
                    // najwiecej weglarzy (3)
                    var top = new List<KeyValuePair<Village, float[]>>(_f7Day);
                    top.Sort((a, b) => b.Value[0].CompareTo(a.Value[0]));
                    sb.Append(" | najwiecej weglarzy: ");
                    if (top.Count == 0) sb.Append("-");
                    for (int i = 0; i < top.Count && i < 3; i++)
                    {
                        var v = top[i].Key; var a = top[i].Value;
                        F7Mine f; _f7.TryGetValue(v, out f);
                        var mk = v.TradeBound;
                        if (i > 0) sb.Append(", ");
                        sb.Append(v.Name != null ? v.Name.ToString() : "?").Append(" [").Append(mk != null && mk.Name != null ? mk.Name.ToString() : "bez miasta").Append("] ")
                          .Append(a[0].ToString("0", inv)).Append(" ladunkow, rece ").Append(a[1].ToString("0.0", inv)).Append(" lup, +").Append(a[2].ToString("0", inv)).Append(" lup, las x")
                          .Append(f != null ? f.LumberL.ToString("0.00", inv) : "?").Append(", r ").Append(f != null ? f.R.ToString("0.0", inv) : "?");
                    }
                    sb.Append(" | bez danych KS (rece blokow jeszcze nie policzone - doba po wczytaniu albo wlaczeniu): bez wegla ").Append(_f7NoData.ToString("0.0", inv))
                      .Append(" lup w ").Append(_f7NoDataV.Count).Append(" kopalniach (poza pierscieniem f5-ruda); r liczone w ticku: prob ").Append(_f7LazyN).Append(", z danymi ").Append(_f7LazyOk);
                    sb.Append(" | kopalnie KS: z lasem ").Append(_f7WithForest).Append(" (las srednio x").Append(_f7ForestL.ToString("0.00", inv)).Append("), bez lasu ").Append(_f7NoForest)
                      .Append(" | czas ").Append((_f7Ticks * 1000.0 / Stopwatch.Frequency).ToString("0.00", inv)).Append(" ms (odswiezenie ")
                      .Append((_f7RefreshTicks * 1000.0 / Stopwatch.Frequency).ToString("0.00", inv)).Append(" ms)")
                      .Append(" | potkniecia dzis ").Append(_f7StumDay).Append(" (sesja ").Append(_f7Stum).Append(").");
                    Log.Info(sb.ToString());
                }
                _f7BankPrev = _pending == null ? bank : -1;
            }
            catch (Exception e) { F7Stumble("Roll", e); _f7BankPrev = -1; }
            finally
            {
                _f7Wood = _f7StumDay = 0;
                _f7Coll = _f7Gain = _f7NoLand = _f7Model = _f7Fired = _f7Idle = _f7People = 0f;
                _f7Ticks = 0; _f7RefreshTicks = 0;
                _f7NoLandV.Clear(); _f7Day.Clear();
                _f7NoData = 0f; _f7NoDataV.Clear(); _f7LazyN = _f7LazyOk = 0;
            }
        }
    }
}
