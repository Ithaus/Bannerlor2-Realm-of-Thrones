using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace Armoury
{
    /// <summary>
    /// R3-11b (POMYSLY-35 nr 35, Jeff 20.10 "pozostale tak"; klucz WorkshopPriceForgetsStartup) - ROZRUCH RYNKU NIE TRZYMA CENY WARSZTATU.
    /// Dowod [L] (Armoury-2026-10-19_20-56-20.log, nowa kampania, przyklad Lannisport w linii "Warsztaty towarowe"): kuznia zarobila ok. 5 190 d w dobach 0-30
    /// (pamiec roku 167.4 d na dobe w 30. dobie), a w dobach 31-119 srednio ok. 27.5 d - pamiec roku (srednia wszystkich prawdziwych dob od startu) trzymala cene
    /// kupna 56 796 w 60. dobie i 39 342 w 119. (z sama doba po rozruchu ok. 25 500 i 22 700). Audyt 07.10 (DROBNE 2): garbarnia ze skokiem 7 dob x3.6 / x2.1 / x1.6
    /// w 30. / 60. / 100. dobie; poprawka 120 (pamiec roku z prawdziwych dob) zbila to tylko o ok. 10%.
    /// Regula: pierwsze StartupDays (30) dob kampanii to rozruch rynku (polki puste albo przepelnione - chleb x7.6, garnki x10 w 1. dobie).
    ///  - W ROZRUCHU warsztat pamieta dodatkowo wyniki swoich ostatnich WeekDays (7) dob (pole Week; pamieci miesiaca Avg i roku Slow ida jak dotad) i cena
    ///    idzie za tym tygodniem: sprzedajacy liczy zysk ze sredniej tych 7 dob, notabl odkupujacy od gracza - z najgorszej z trzech (tydzien, miesiac, rok),
    ///    wiec kupno i odsprzedaz w tej samej chwili dalej nie daja zysku. Skok przestaje liczyc sie w cenie tydzien po tym, jak sie skonczyl (recenzja:
    ///    w dobach ok. 8-29 garbarnia z audytu kosztowala x5-x17 za duzo, bo obie pamieci to wtedy srednia wszystkich dob od startu).
    ///  - PIERWSZA DOBA warsztatu po rozruchu: obie pamieci zostaja z samych ostatnich 7 dob rozruchu (Avg = Slow = srednia tygodnia, Mem = liczba tych dob),
    ///    dalej zwykle reguly (lepsza / gorsza z pamieci miesiaca i roku). Cena przechodzi z rozruchu bez skoku, a pamiec roku nie trzyma dob skoku.
    ///    Warsztat bez tygodnia w pamieci (nie powinno sie zdarzyc) - pamieci od nowa jak doba 1 kampanii (Mem = 0: punkt wyjscia z oczekiwanego wyniku).
    ///  - Znacznik Warm, ktory dotrwal ponad GraceDays (7) dob po rozruchu (klucz wylaczony w dobie przejscia), tylko znika - pamieci bez zmian (recenzja:
    ///    wlaczenie klucza po miesiacach nie kasuje dobrych dob po rozruchu).
    /// Licznik dob warsztatu (Days) zostaje; pole Mem = doby w pamieci (dzielnik srednich, okno ceny, RecOf - "tracacy od 60 dob w obu pamieciach" zmiany
    /// branzy i "zmierzony" F4 licza sie od 7 ostatnich dob rozruchu).
    /// Kogo dotyczy: kazdy warsztat pod paczka warsztatow (bez ukrytych rzemieslnikow), ktorego pamiec zlapala choc jedna dobe rozruchu (znacznik Warm i tydzien
    /// w zapisie arm_wstrade jako pola na koncu - starsza DLL je pomija; tydzien tylko w rozruchu). Zapis bez znacznika (kampania Jeffa) - nic sie nie zmienia.
    /// Zamknieta ekonomia: zmienia sie tylko liczba w wycenie (cena kupna i odkupu), zadne zloto ani sztuka nie powstaje i nie znika.
    /// Linia "Warsztaty - rozruch rynku (R3-11b)": w rozruchu porownanie cen z tygodnia z cenami z pamieci (stary wzor), w dobie przejscia (zawsze) i przez
    /// rok po nim porownanie z pamieciami "bez reguly" liczonymi obok starym wzorem tylko w sesji, w ktorej minal rozruch (sam log, nic w zapisie).
    /// Ciche logi w grze: co QuietLogEveryDays dob. Wyjatek per warsztat - licznik potkniec (funkcji nie gasimy). Watek glowny (ticki dobowe miast, linia doby).
    /// </summary>
    internal static class WorkshopStartup
    {
        internal const int StartupDays = 30;        // rozruch rynku: miesiac (ok. tyle trwal skok kuzni Lannisportu w tescie 19.10, garbarni z audytu - 7 dob)
        internal const int WeekDays = 7;            // w rozruchu cena z wynikow tylu ostatnich dob; po rozruchu pamieci zostaja z tylu dob
        private const int GraceDays = 7;            // znacznik rozruchu starszy niz tyle dob po rozruchu - tylko zdjety (pamieci bez zmian)
        private const int CompareDays = 364;        // porownanie "bez reguly" w logu - rok kalendarza Armoury po rozruchu
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class Shadow { public string Type; public float Avg, Slow; public int Mem, Since; }
        private static readonly Dictionary<Workshop, Shadow> _shadow = new Dictionary<Workshop, Shadow>();
        private static readonly HashSet<Workshop> _warmToday = new HashSet<Workshop>();   // warsztaty rozliczone dzis w rozruchu (do porownania w linii)
        private static readonly HashSet<string> _errOnce = new HashSet<string>();
        private static int _dWarm, _dReseed, _sReseed, _dWeekless, _dLate, _sLate, _dStumbles, _sStumbles;
        private static bool _idleLine;              // linia "nic do zrobienia" raz na sesje

        internal static bool On { get { var s = Settings.Current; return s != null && s.WorkshopPriceForgetsStartup; } }

        /// <summary>Nowa gra / wczytanie (z WorkshopTrade.Reset): stan sesji.</summary>
        internal static void Reset()
        {
            _shadow.Clear(); _warmToday.Clear(); _errOnce.Clear();
            _dWarm = _dReseed = _sReseed = _dWeekless = _dLate = _sLate = _dStumbles = _sStumbles = 0;
            _idleLine = false;
        }

        private static void Stumble(string where, Exception e)
        {
            _dStumbles++; _sStumbles++;
            if (_errOnce.Add(where)) Log.Error("WorkshopStartup." + where, e);
        }

        /// <summary>Wiek kampanii w dobach (CampaignStartTime modelu czasu - jak ColdStart, CastlePurse, Hunger); -1 bez kampanii.</summary>
        private static double Age()
        {
            var c = Campaign.Current;
            return c != null ? (CampaignTime.Now - c.Models.CampaignTimeModel.CampaignStartTime).ToDays : -1.0;
        }

        // ------------------------------------------------------------ tydzien rozruchu: week[0] = liczba dob, week[1..7] = pierscien wynikow doby

        private static void Push(ref int[] week, int x)
        {
            if (week == null || week.Length != WeekDays + 1) week = new int[WeekDays + 1];   // raz na warsztat na rozruch
            week[1 + (Math.Max(0, week[0]) % WeekDays)] = x;
            week[0] = Math.Max(0, week[0]) + 1;
        }

        /// <summary>Srednia ostatnich (najwyzej 7) dob tygodnia; false = tygodnia brak. Bez alokacji.</summary>
        private static bool WeekMean(int[] week, out float mean, out int n)
        {
            mean = 0f; n = 0;
            if (week == null || week.Length != WeekDays + 1 || week[0] <= 0) return false;
            n = Math.Min(week[0], WeekDays);
            long sum = 0;
            for (int i = 1; i <= n; i++) sum += week[i];
            mean = (float)sum / n;
            return true;
        }

        /// <summary>
        /// Z WorkshopTrade.Build: w rozruchu (warsztat ze znacznikiem i tygodniem, klucz wlaczony) zysk do ceny z ostatnich 7 dob - sprzedajacy wedle sredniej
        /// tygodnia, notabl (sellerAsks = false) wedle najgorszej z trzech (tydzien, miesiac, rok). false = zwykla regula (lepsza / gorsza z dwoch pamieci).
        /// </summary>
        internal static bool WeekProfit(bool warm, int[] week, bool sellerAsks, float avg, float slow, out float profit, out int window)
        {
            profit = 0f; window = 0;
            float mean; int n;
            if (!warm || !On || !WeekMean(week, out mean, out n)) return false;
            if (Age() >= StartupDays + GraceDays) return false;           // znacznik po terminie (klucz wylaczony w przejsciu) - zwykla regula; BeforeDay go zdejmie
            profit = sellerAsks ? mean : Math.Min(mean, Math.Min(avg, slow));
            window = n;                                                     // okno w rozmowie: "over the last N days" (notabl - nie widac)
            return true;
        }

        /// <summary>Zapis (WorkshopTrade.Export): pole tygodnia "n,x1,...,x7" albo null (poza rozruchem nie zapisujemy).</summary>
        internal static string WeekField(bool warm, int[] week)
        {
            if (!warm || week == null || week.Length != WeekDays + 1 || week[0] <= 0) return null;
            var sb = new StringBuilder();
            for (int i = 0; i < week.Length; i++) { if (i > 0) sb.Append(','); sb.Append(week[i].ToString(Inv)); }
            return sb.ToString();
        }

        /// <summary>Wczytanie (WorkshopTrade.Get): pole tygodnia z zapisu; zly zapis = null (warsztat bez tygodnia - cena wedle pamieci jak dotad).</summary>
        internal static int[] ParseWeek(string s)
        {
            try
            {
                if (string.IsNullOrEmpty(s)) return null;
                var f = s.Split(',');
                if (f.Length != WeekDays + 1) return null;
                var w = new int[WeekDays + 1];
                for (int i = 0; i < f.Length; i++) if (!int.TryParse(f[i], NumberStyles.Integer, Inv, out w[i])) return null;
                return w[0] > 0 ? w : null;
            }
            catch (Exception e) { Stumble("ParseWeek", e); return null; }
        }

        /// <summary>
        /// Z WorkshopTrade.CloseDay PRZED wpisaniem dzisiejszego wyniku `x` (watek glowny, ticki dobowe miast). W rozruchu - znacznik `warm` i wynik doby do
        /// tygodnia. Pierwsza doba po rozruchu z `warm` - pamieci z samych dob tygodnia (Avg = Slow = srednia, Mem = ich liczba; CloseDay dopisze dzis
        /// zwyklym wzorem), a dotychczasowe pamieci ida dalej obok starym wzorem do porownania w logu. Znacznik po terminie (GraceDays po rozruchu) - tylko
        /// zdjety. Przy wylaczonym kluczu nic (porownanie juz zaczete liczy sie dalej).
        /// </summary>
        internal static void BeforeDay(Workshop shop, string type, ref bool warm, ref int[] week, ref int mem, ref float avg, ref float slow, int x, int span, int yearSpan)
        {
            try
            {
                Shadow sh;
                if (shop != null && _shadow.TryGetValue(shop, out sh))
                {
                    if (sh.Type != type) _shadow.Remove(shop);          // zmiana branzy: porownanie traci sens
                    else Step(sh, x, span, yearSpan);
                }
                if (shop == null) return;
                double age = Age();
                if (age < 0.0) return;
                if (age >= StartupDays + GraceDays)
                {
                    // klucz byl wylaczony w dobie przejscia: za pozno na pamiec od nowa (zgubilaby doby po rozruchu) - znacznik i tydzien znikaja
                    if (warm || week != null) { warm = false; week = null; _dLate++; _sLate++; }
                    return;
                }
                if (!On) return;
                if (age < StartupDays) { warm = true; Push(ref week, x); _warmToday.Add(shop); _dWarm++; return; }
                if (!warm) return;
                if (mem > 0)
                {
                    sh = new Shadow { Type = type, Avg = avg, Slow = slow, Mem = mem, Since = (int)age };
                    Step(sh, x, span, yearSpan);
                    _shadow[shop] = sh;
                }
                float mean; int n;
                if (WeekMean(week, out mean, out n)) { avg = mean; slow = mean; mem = n; }
                else { avg = 0f; slow = 0f; mem = 0; _dWeekless++; }
                warm = false; week = null;
                _dReseed++; _sReseed++;
            }
            catch (Exception e) { Stumble("BeforeDay", e); }
        }

        /// <summary>Stary wzor CloseDay (pamiec od startu kampanii) - tylko do porownania w logu.</summary>
        private static void Step(Shadow sh, int x, int span, int yearSpan)
        {
            sh.Avg += (x - sh.Avg) / Math.Min(sh.Mem + 2, span);
            sh.Slow += (x - sh.Slow) / Math.Min(sh.Mem + 1, yearSpan);
            sh.Mem++;
        }

        private static void ClearDay() { _dWarm = 0; _dReseed = 0; _dWeekless = 0; _dLate = 0; _dStumbles = 0; _warmToday.Clear(); }

        private static int Median(List<int> l) { if (l.Count == 0) return 0; l.Sort(); return l[l.Count / 2]; }

        /// <summary>Raz na dobe (DailyTickEvent, za wczorajsze ticki miast): linia "Warsztaty - rozruch rynku (R3-11b)".</summary>
        internal static void Daily()
        {
            try
            {
                if (!WorkshopTrade.On || Campaign.Current == null) return;
                double age = Age();
                int ageDay = (int)Math.Floor(age);
                // porownanie najwyzej rok po rozruchu warsztatu; warsztat bez wlasciciela / bez typu - poza porownaniem
                if (_shadow.Count > 0)
                {
                    List<Workshop> drop = null;
                    foreach (var kv in _shadow)
                        if (kv.Key == null || kv.Key.WorkshopType == null || ageDay - kv.Value.Since > CompareDays) (drop ?? (drop = new List<Workshop>())).Add(kv.Key);
                    if (drop != null) foreach (var w in drop) _shadow.Remove(w);
                }
                bool any = _dWarm > 0 || _dReseed > 0 || _dLate > 0 || _shadow.Count > 0;
                if (!any)
                {
                    if (age >= StartupDays && !_idleLine)
                    {
                        _idleLine = true;
                        Log.Info("Warsztaty - rozruch rynku (R3-11b): dzien kampanii " + ageDay + " | regula " + (On ? "CZYNNA" : "WYLACZONA (MCM)")
                                 + " - w tej sesji zaden warsztat nie ma w pamieci dob rozruchu ani porownania (kampania po rozruchu, zapis sprzed reguly albo pamieci"
                                 + " juz przestawione przed tym wczytaniem); znaczniki zdjete po terminie w sesji " + _sLate + "; potkniecia " + _sStumbles + ".");
                    }
                    return;
                }
                if (_dReseed == 0 && _dLate == 0 && !LogGate.Lines) return;
                var sb = new StringBuilder();
                sb.Append("Warsztaty - rozruch rynku (R3-11b): dzien kampanii ").Append(ageDay).Append(" | regula ").Append(On ? "CZYNNA" : "WYLACZONA (MCM)")
                  .Append(" (pierwsze ").Append(StartupDays).Append(" dob kampanii: cena z ostatnich ").Append(WeekDays)
                  .Append(" dob; pierwsza doba po rozruchu: pamieci warsztatu tylko z ").Append(WeekDays).Append(" ostatnich dob rozruchu)")
                  .Append(" | w rozruchu dzis ").Append(_dWarm).Append(" warsztatow; pamiec od nowa dzis ").Append(_dReseed).Append(", w sesji ").Append(_sReseed);
                if (_dWeekless > 0) sb.Append(" (bez tygodnia - od zera: ").Append(_dWeekless).Append(')');
                if (_dLate > 0 || _sLate > 0) sb.Append("; znacznik zdjety po terminie bez zmiany pamieci dzis ").Append(_dLate).Append(", w sesji ").Append(_sLate);
                if (_warmToday.Count > 0) sb.Append(" | ").Append(StartupText());
                if (_shadow.Count > 0) sb.Append(" | ").Append(CompareText());
                sb.Append(" | potkniecia dzis ").Append(_dStumbles).Append(", w sesji ").Append(_sStumbles).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ClearDay(); }
        }

        /// <summary>Porownanie cen: z regula (WorkshopTrade.QuoteFor) i bez niej (ten sam rachunek z zyskiem starego wzoru) - sam log.</summary>
        private sealed class Cmp
        {
            private readonly List<int> _with = new List<int>(), _no = new List<int>();
            private long _sumWith, _sumNo; private int _lower25, _higher; private float _bestGap; private string _example;

            internal void Add(Workshop w, string type, WorkshopTrade.Quote q, float oldProfit, int oldDays, string what)
            {
                int daysInYear = Math.Max(1, CampaignTime.DaysInYear);
                int going = (int)Math.Round(Math.Max(0f, oldProfit) * (1f - q.Tax) * daysInYear * q.Years);
                int old = Math.Max(going, q.Equip) + q.Capital;
                _with.Add(q.Price); _no.Add(old);
                _sumWith += q.Price; _sumNo += old;
                if (q.Price < 0.75 * old) _lower25++;
                if (q.Price > old) _higher++;
                float gap = old - q.Price;
                if (gap > _bestGap)
                {
                    _bestGap = gap;
                    _example = (w.Settlement.Name != null ? w.Settlement.Name.ToString() : w.Settlement.StringId) + " " + type + " " + q.Price + " zamiast " + old
                               + " (zysk do ceny " + q.Profit.ToString("0.#", Inv) + " d na dobe z " + q.Window + " " + what + ", bez reguly " + oldProfit.ToString("0.#", Inv) + " z " + oldDays + " dob)";
                }
            }

            internal string Text(string head)
            {
                return head + " w " + _with.Count + " warsztatach: z regula mediana " + Median(_with) + ", bez reguly " + Median(_no)
                       + "; razem " + _sumWith + " wobec " + _sumNo + (_sumWith > 0 ? " (bez reguly x" + ((double)_sumNo / _sumWith).ToString("0.00", Inv) + ")" : "")
                       + "; taniej o ponad 25%: " + _lower25 + ", drozej niz bez reguly: " + _higher
                       + (_example != null ? "; najwieksza roznica: " + _example : "");
            }
        }

        private static bool Priced(Workshop w)
        {
            return w != null && w.WorkshopType != null && !w.WorkshopType.IsHidden && w.Owner != null && w.Settlement != null;
        }

        /// <summary>W rozruchu: cena z tygodnia wobec ceny z lepszej z pamieci (miesiac, rok - w rozruchu ida jak dotad, wiec to stary wzor).</summary>
        private static string StartupText()
        {
            var c = new Cmp();
            foreach (var w in _warmToday)
            {
                try
                {
                    if (!Priced(w)) continue;
                    float avg, slow; int days;
                    if (!WorkshopTrade.RecOf(w, out avg, out slow, out days)) continue;
                    c.Add(w, w.WorkshopType.StringId ?? "?", WorkshopTrade.QuoteFor(w, Hero.MainHero), Math.Max(avg, slow), days, "ostatnich dob");
                }
                catch (Exception e) { Stumble("StartupText", e); }
            }
            return c.Text("cena kupna dla gracza w rozruchu (z tygodnia)");
        }

        /// <summary>Po rozruchu: cena z pamieci od 7 ostatnich dob rozruchu wobec pamieci starego wzoru liczonych obok - warsztaty po rozruchu w tej sesji.</summary>
        private static string CompareText()
        {
            var c = new Cmp();
            foreach (var kv in _shadow)
            {
                var w = kv.Key; var sh = kv.Value;
                try
                {
                    if (!Priced(w) || (w.WorkshopType.StringId ?? "?") != sh.Type) continue;
                    // sprzedajacy wedle lepszej z dwoch pamieci - jak Build
                    c.Add(w, sh.Type, WorkshopTrade.QuoteFor(w, Hero.MainHero), Math.Max(sh.Avg, sh.Slow), sh.Mem, "dob pamieci po rozruchu");
                }
                catch (Exception e) { Stumble("CompareText", e); }
            }
            return c.Text("cena kupna dla gracza po rozruchu");
        }
    }
}
