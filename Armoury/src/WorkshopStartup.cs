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
    /// Regula: pierwsze StartupDays (30) dob kampanii to rozruch rynku (polki puste albo przepelnione - chleb x7.6, garnki x10 w 1. dobie). W rozruchu pamieci
    /// warsztatu (miesiac Avg, rok Slow) ida jak dotad i cena idzie za biezacym zarobkiem. Pierwsza doba warsztatu po rozruchu zaczyna obie pamieci od nowa -
    /// tak samo jak doba 1 nowej kampanii (pamiec miesiaca od oczekiwanego wyniku wedle cen juz ulozonego targu, pamiec roku z samych prawdziwych dob), wiec
    /// skok z rozruchu nie siedzi w cenie miesiacami. Licznik dob warsztatu (Days) zostaje; nowe pole Mem = doby w pamieci (dzielnik srednich, okno ceny, RecOf -
    /// "tracacy od 60 dob w obu pamieciach" zmiany branzy i "zmierzony" F4 licza sie od konca rozruchu).
    /// Kogo dotyczy: kazdy warsztat pod paczka warsztatow (bez ukrytych rzemieslnikow), ktorego pamiec zlapala choc jedna dobe rozruchu (znacznik Warm, w zapisie
    /// arm_wstrade jako dodatkowe pole - starsza DLL go pomija). Zapis bez znacznika (kampania Jeffa) - nic sie nie zmienia. Zamknieta ekonomia: zmienia sie tylko
    /// liczba w wycenie (cena kupna i odkupu), zadne zloto ani sztuka nie powstaje i nie znika.
    /// Linia "Warsztaty - rozruch rynku (R3-11b)": w rozruchu, w dobie przejscia (zawsze) i przez rok po nim porownanie cen kupna z regula i bez (pamieci "bez
    /// reguly" liczone obok starym wzorem tylko w sesji, w ktorej minal rozruch - sam log, nic w zapisie). Ciche logi w grze: co QuietLogEveryDays dob.
    /// Wyjatek per warsztat - licznik potkniec (funkcji nie gasimy).
    /// </summary>
    internal static class WorkshopStartup
    {
        internal const int StartupDays = 30;        // rozruch rynku: miesiac (ok. tyle trwal skok kuzni Lannisportu w tescie 19.10, garbarni z audytu - 7 dob)
        private const int CompareDays = 364;        // porownanie "bez reguly" w logu - rok kalendarza Armoury po rozruchu
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class Shadow { public string Type; public float Avg, Slow; public int Mem, Since; }
        private static readonly Dictionary<Workshop, Shadow> _shadow = new Dictionary<Workshop, Shadow>();
        private static readonly HashSet<string> _errOnce = new HashSet<string>();
        private static int _dWarm, _dReseed, _sReseed, _dStumbles, _sStumbles;
        private static bool _idleLine;              // linia "nic do zrobienia" raz na sesje

        internal static bool On { get { var s = Settings.Current; return s != null && s.WorkshopPriceForgetsStartup; } }

        /// <summary>Nowa gra / wczytanie (z WorkshopTrade.Reset): stan sesji.</summary>
        internal static void Reset()
        {
            _shadow.Clear(); _errOnce.Clear();
            _dWarm = _dReseed = _sReseed = _dStumbles = _sStumbles = 0;
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

        /// <summary>
        /// Z WorkshopTrade.CloseDay PRZED wpisaniem dzisiejszego wyniku `x` (watek glowny, ticki dobowe miast). W rozruchu - znacznik `warm`. Pierwsza doba
        /// po rozruchu z `warm` - pamieci od nowa (mem = 0: CloseDay bierze punkt wyjscia sredniej miesiaca z oczekiwanego wyniku, jak w dobie 1), a dotychczasowe
        /// pamieci ida dalej obok starym wzorem do porownania w logu. Przy wylaczonym kluczu nic (porownanie juz zaczete liczy sie dalej).
        /// </summary>
        internal static void BeforeDay(Workshop shop, string type, ref bool warm, ref int mem, ref float avg, ref float slow, int x, int span, int yearSpan)
        {
            try
            {
                Shadow sh;
                if (shop != null && _shadow.TryGetValue(shop, out sh))
                {
                    if (sh.Type != type) _shadow.Remove(shop);          // zmiana branzy: porownanie traci sens
                    else Step(sh, x, span, yearSpan);
                }
                if (!On || shop == null) return;
                double age = Age();
                if (age < 0.0) return;
                if (age < StartupDays) { warm = true; _dWarm++; return; }
                if (!warm) return;
                if (mem > 0)
                {
                    sh = new Shadow { Type = type, Avg = avg, Slow = slow, Mem = mem, Since = (int)age };
                    Step(sh, x, span, yearSpan);
                    _shadow[shop] = sh;
                }
                warm = false; mem = 0; avg = 0f; slow = 0f;
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

        private static void ClearDay() { _dWarm = 0; _dReseed = 0; _dStumbles = 0; }

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
                bool any = _dWarm > 0 || _dReseed > 0 || _shadow.Count > 0;
                if (!any)
                {
                    if (age >= StartupDays && !_idleLine)
                    {
                        _idleLine = true;
                        Log.Info("Warsztaty - rozruch rynku (R3-11b): dzien kampanii " + ageDay + " | regula " + (On ? "CZYNNA" : "WYLACZONA (MCM)")
                                 + " - zaden warsztat nie ma w pamieci dob rozruchu (kampania po rozruchu albo zapis sprzed reguly), ceny bez zmian; potkniecia " + _sStumbles + ".");
                    }
                    return;
                }
                if (_dReseed == 0 && !LogGate.Lines) return;
                var sb = new StringBuilder();
                sb.Append("Warsztaty - rozruch rynku (R3-11b): dzien kampanii ").Append(ageDay).Append(" | regula ").Append(On ? "CZYNNA" : "WYLACZONA (MCM)")
                  .Append(" (pierwsze ").Append(StartupDays).Append(" dob kampanii: pamiec ceny jak dotad, pierwsza doba po rozruchu zaczyna pamieci warsztatu od nowa)")
                  .Append(" | w rozruchu dzis ").Append(_dWarm).Append(" warsztatow; pamiec od nowa dzis ").Append(_dReseed).Append(", w sesji ").Append(_sReseed);
                if (_shadow.Count > 0) sb.Append(" | ").Append(CompareText());
                sb.Append(" | potkniecia dzis ").Append(_dStumbles).Append(", w sesji ").Append(_sStumbles).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ClearDay(); }
        }

        /// <summary>Cena kupna dla gracza z regula (WorkshopTrade.QuoteFor) i bez niej (ten sam rachunek z pamieciami starego wzoru) - warsztaty po rozruchu w tej sesji.</summary>
        private static string CompareText()
        {
            var withRule = new List<int>(); var noRule = new List<int>();
            long sumWith = 0, sumNo = 0; int lower25 = 0, higher = 0;
            float bestGap = 0f; string example = null;
            int daysInYear = Math.Max(1, CampaignTime.DaysInYear);
            foreach (var kv in _shadow)
            {
                var w = kv.Key; var sh = kv.Value;
                try
                {
                    if (w == null || w.WorkshopType == null || w.WorkshopType.IsHidden || w.Owner == null || w.Settlement == null) continue;
                    if ((w.WorkshopType.StringId ?? "?") != sh.Type) continue;
                    var q = WorkshopTrade.QuoteFor(w, Hero.MainHero);
                    float oldProfit = Math.Max(sh.Avg, sh.Slow);          // sprzedajacy wedle lepszej z dwoch pamieci - jak Build
                    int going = (int)Math.Round(Math.Max(0f, oldProfit) * (1f - q.Tax) * daysInYear * q.Years);
                    int old = Math.Max(going, q.Equip) + q.Capital;
                    withRule.Add(q.Price); noRule.Add(old);
                    sumWith += q.Price; sumNo += old;
                    if (old > q.Price * 1.25) lower25++;
                    if (q.Price > old) higher++;
                    float gap = old - q.Price;
                    if (gap > bestGap)
                    {
                        bestGap = gap;
                        example = (w.Settlement.Name != null ? w.Settlement.Name.ToString() : w.Settlement.StringId) + " " + sh.Type + " " + q.Price + " zamiast " + old
                                  + " (zysk do ceny " + q.Profit.ToString("0.#", Inv) + " d na dobe z " + q.Window + " dob po rozruchu, bez reguly " + oldProfit.ToString("0.#", Inv) + " z " + sh.Mem + " dob)";
                    }
                }
                catch (Exception e) { Stumble("CompareText", e); }
            }
            int n = withRule.Count;
            return "cena kupna dla gracza w " + n + " warsztatach po rozruchu: z regula mediana " + Median(withRule) + ", bez reguly " + Median(noRule)
                   + "; razem " + sumWith + " wobec " + sumNo + (sumWith > 0 ? " (bez reguly x" + ((double)sumNo / sumWith).ToString("0.00", Inv) + ")" : "")
                   + "; taniej o ponad 25%: " + lower25 + ", drozej niz bez reguly: " + higher
                   + (example != null ? "; najwieksza roznica: " + example : "");
        }
    }
}
