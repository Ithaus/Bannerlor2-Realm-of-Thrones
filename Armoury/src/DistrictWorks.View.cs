using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// m115 (24.10, Jeff: "skoro wioksa mi placi to ma byc na zielon w inocme i na drugim zdjeicuy" - zakladka Fiefs pokazuje "paid to you 29 in the last 16 days",
    /// a panel Finance ekranu rodu "Total Income 0", podpowiedz zlota "Total -60"): linie prac gracza w wyniku modelu finansow - TYLKO WIDOK (klucz WorksIncomeShown).
    /// Pieniadze dalej ida przy powrocie wozow (OnCartReturn), nie z modelu finansow - bez podwojnej wyplaty. Kwota = to, co ksiega naprawde wyplacila:
    /// Paid28 / max(7, doby historii), suma zaokraglona raz i podzielona na wioski metoda najwiekszej reszty (jak wyplata w OnCartReturn). Bez linii: wojna
    /// z krolestwem okregu, prace wstrzymane, wszystko spalone. Liczone raz na godzine gry albo po wyplacie (ViewDirty); nie rzuca (potkniecie liczone).
    /// Wpiecie: SideVillageFief.IncomeViewPostfix (tylko przy FinanceBox.In - panel Finance) i GoldTipPostfix (podpowiedz zlota, z opisami, bez pobrania).
    /// </summary>
    internal static partial class DistrictWorks
    {
        internal struct ViewLine { public string Name; public int Amt; public TextObject Txt; }

        private static int _vwHour = int.MinValue, _vwStamp, _vwSeen = -1;
        private static List<ViewLine> _vw = new List<ViewLine>();
        private static int _vwTotal, _vwDays;
        private static int _vwSince;                                 // 305e: prace w widoku liczone od zakupu (klucz WorksPaidSinceBuy) - linia "widok finansow"
        private static TextObject _vwSumTxt;
        // liczniki doby (linia "widok finansow" w DayLine): linie dodane do panelu Finance i do podpowiedzi zlota, potkniecia widoku
        internal static int _dVwBox, _dVwTip, _vwStumble;

        /// <summary>305e: dzielnik sredniej na dobe jednej pracy w widoku. Klucz WorksPaidSinceBuy i prace mlodsze niz historia - doby od zakupu (najmniej 1);
        /// inaczej jak m115: max(7, doby historii). Ten sam wzor w FiefsShotTest (oczekiwanie z ksiegi).</summary>
        internal static int ViewDays(MyWork w)
        {
            if (w == null) return 7;
            return w.SinceBuy ? Math.Max(1, w.PaidDaysN) : Math.Max(7, w.PaidDaysN);
        }

        /// <summary>305e: zaokraglenie sumy widoku. Klucz WorksPaidSinceBuy - od zera (0.5 -> 1), a suma w (0, 1) jako 1; NIE - jak m115 (Math.Round, do parzystej).</summary>
        internal static long ViewRound(double sum)
        {
            var s = Settings.Current;
            if (s == null || !s.WorksPaidSinceBuy) return (long)Math.Round(sum);
            long t = (long)Math.Round(sum, MidpointRounding.AwayFromZero);
            if (t <= 0 && sum > 1e-9) t = 1;
            return t;
        }

        /// <summary>m115: wyplata, wczytanie albo nowa kampania - widok liczony od nowa przy nastepnym odczycie.</summary>
        internal static void ViewDirty() { _vwStamp++; }

        /// <summary>m115: linie prac gracza do widoku finansow (srednia wyplaconego na dobe wedlug wioski). Zwraca sume; 0 - brak linii albo blad.</summary>
        internal static int ViewWorks(out List<ViewLine> lines, out int days)
        {
            lines = _vw; days = _vwDays;
            // po recenzji m115: godzina i znacznik przed try - blad tez zapamietany na godzine (podpowiedz zlota wola to kilka razy na sekunde na mapie)
            int hour = int.MinValue, stamp = _vwStamp;
            try
            {
                if (Campaign.Current == null) { lines = null; days = 0; return 0; }
                hour = (int)CampaignTime.Now.ToHours;
                if (hour == _vwHour && _vwSeen == _vwStamp) return _vwTotal;
                stamp = _vwStamp;
                var raw = new List<KeyValuePair<string, double>>();
                var paid = new List<double>();                               // wyplacone w znanych dobach wedlug wioski (napis, jak w zakladce Fiefs)
                var idx = new Dictionary<string, int>(StringComparer.Ordinal);
                var young = new List<int>();                                 // 305e: najmlodsze prace wioski liczone od zakupu (doby; 0 - brak)
                int d = 7, h = 28, since = 0;
                foreach (var w in PlayerWorks())
                {
                    if (w == null || w.WarWith != null || w.Paused != null || !(w.Eff > 1e-6)) continue;
                    int dn = ViewDays(w);                                    // 305e: od zakupu przy kluczu WorksPaidSinceBuy; inaczej max(7, doby historii)
                    if (!w.SinceBuy) d = Math.Max(d, dn);
                    double x = w.Paid28 / dn;
                    if (!(x > 0)) continue;
                    if (!w.SinceBuy) h = Math.Min(h, Math.Max(1, w.PaidDaysN));   // 305e: napis "spread over 7 days" tylko dla historii krotszej od wczytania
                    else since++;
                    string n = w.Village ?? "?";
                    int i;
                    if (idx.TryGetValue(n, out i)) { raw[i] = new KeyValuePair<string, double>(n, raw[i].Value + x); paid[i] += w.Paid28; }
                    else { idx[n] = i = raw.Count; raw.Add(new KeyValuePair<string, double>(n, x)); paid.Add(w.Paid28); young.Add(0); }
                    if (w.SinceBuy && (young[i] == 0 || dn < young[i])) young[i] = dn;
                }
                // suma zaokraglona raz, na wioski metoda najwiekszej reszty (jak OnCartReturn) - linie sumuja sie dokladnie do sumy
                double sum = 0; foreach (var kv in raw) sum += kv.Value;
                long total = ViewRound(sum);                                 // 305e: przy kluczu WorksPaidSinceBuy 0.5 -> 1 i suma w (0, 1) jako 1
                var whole = new long[raw.Count]; long left = total;
                for (int i = 0; i < whole.Length; i++) { whole[i] = Math.Max(0L, (long)Math.Floor(raw[i].Value + 1e-9)); left -= whole[i]; }
                var ord = new List<int>(); for (int i = 0; i < whole.Length; i++) ord.Add(i);
                ord.Sort((a, b) => (raw[b].Value - whole[b]).CompareTo(raw[a].Value - whole[a]));
                for (int k = 0; k < ord.Count && left > 0; k++, left--) whole[ord[k]]++;
                var res = new List<ViewLine>();
                int sumOut = 0;
                for (int i = 0; i < whole.Length; i++)
                {
                    if (whole[i] <= 0) continue;
                    int a = (int)Math.Min(int.MaxValue, whole[i]);
                    // po recenzji m115: historia krotsza niz 7 dob (zapis sprzed m112, kilka dob od wczytania) - napis uczciwy: ile wyplacono w ilu znanych dobach,
                    // kwota rozlozona na 7 dob (doby przed wczytaniem = 0); zakladka Fiefs pokazuje te same doby
                    string why = young[i] > 0
                        ? "average since you paid for the works " + young[i] + (young[i] == 1 ? " day ago" : " days ago")   // 305e
                        : h < 7
                        ? "paid " + Math.Round(paid[i]).ToString("N0", CultureInfo.InvariantCulture) + " in the last " + h + (h == 1 ? " day" : " days") + ", shown spread over 7 days"
                        : "average of the last " + d + " days";
                    res.Add(new ViewLine { Name = raw[i].Key, Amt = a, Txt = new TextObject("{=!}Village works: " + raw[i].Key + " (" + why + ", paid when the carts return)") });
                    sumOut += a;
                }
                _vwSumTxt = res.Count > 0 ? new TextObject("{=!}Village works (" + res.Count + " villages, average paid when the carts return)") : null;
                _vw = res; _vwTotal = sumOut; _vwDays = d; _vwHour = hour; _vwSeen = stamp; _vwSince = since;
                lines = _vw; days = _vwDays;
                return _vwTotal;
            }
            catch (Exception e)
            {
                _vwStumble++; Stumble("ViewWorks", e);
                if (hour != int.MinValue) { _vw = new List<ViewLine>(); _vwTotal = 0; _vwSumTxt = null; _vwDays = 0; _vwHour = hour; _vwSeen = stamp; }
                lines = null; days = 0; return 0;
            }
        }

        /// <summary>po recenzji m115 (autotest Fiefs): wiersze arm_works z zapisu jeszcze niewczytane (ImportNow dopiero w pierwszej dobie po wczytaniu).</summary>
        internal static bool ImportPending { get { lock (Gate) return _pending != null; } }

        /// <summary>m115: linie prac gracza dopisane do wyniku modelu finansow (tylko widok). details albo najwyzej 3 wioski - linia na wioske, inaczej jedna linia.
        /// Zwraca liczbe dodanych linii.</summary>
        internal static int AddView(ref ExplainedNumber r, bool details)
        {
            try
            {
                List<ViewLine> l; int d;
                int total = ViewWorks(out l, out d);
                if (total <= 0 || l == null || l.Count == 0) return 0;
                if (details || l.Count <= 3)
                {
                    foreach (var x in l) r.Add(x.Amt, x.Txt);
                    return l.Count;
                }
                if (_vwSumTxt == null) return 0;
                r.Add(total, _vwSumTxt);
                return 1;
            }
            catch (Exception e) { _vwStumble++; Stumble("AddView", e); return 0; }
        }

        /// <summary>m115: linia doby widoku finansow (gdy sa prace gracza z wyplata albo linie w widoku); zeruje liczniki doby widoku.</summary>
        private static void ViewDayLine()
        {
            try
            {
                List<ViewLine> l; int d;
                int total = ViewWorks(out l, out d);
                int n = l != null ? l.Count : 0;
                int box = _dVwBox, tip = _dVwTip, st = _vwStumble;
                _dVwBox = _dVwTip = 0;
                if (total <= 0 && box == 0 && tip == 0 && st == 0) return;
                var s = Settings.Current;
                Log.Info(P + "widok finansow (m115, klucz Works Income Shown " + (s != null && s.WorksIncomeShown ? "TAK" : "WYL") + "): prace gracza " + total + "/dobe (wioski " + n + ", dni " + d
                         + "), linie panelu " + box + ", linie podpowiedzi " + tip + ", potkniecia " + st + " | panel Finance " + FinanceBox.State
                         + " | 305e: od zakupu (klucz Works Paid Since Buy " + (s != null && s.WorksPaidSinceBuy ? "TAK" : "NIE") + ") prac " + _vwSince + ".");
            }
            catch (Exception e) { _vwStumble++; Stumble("ViewDayLine", e); }
        }
    }
}
