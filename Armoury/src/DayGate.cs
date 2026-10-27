using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// 280 (B2) - TAKT DOBY DOKLADNIE RAZ NA NUMER DNIA (Jeff 27.10 "sprawdz w logu ile osob pochlonela wojna ... i czy straty sa odejmowane od liczby ludnosci" ->
    /// "tak napraw wszystko"). Dowod: logi Jeffa - podwojne doby 108829 i 108836, brak doby 108986 (po wczytaniu).
    /// PRZYCZYNA (blad gry, dekompilacja Campaign.cs 1.4.8): Campaign.CreateCampaignEvents przy kazdym wczytaniu tworzy _dailyTickEvent od nowa (pole bez zapisu)
    /// z initialWait = (Now - CampaignStartTime) % doba - czyli UPLYNIETY ulamek doby zamiast pozostalego. Nastepny takt wypada wiec o zlej porze: ta sama doba
    /// drugi raz (W &lt; 1 doba) albo doba pominieta (W &gt; 1 doba). W nowej grze ulamek = 0 -> initialWait 1 doba -> takty o porze START KAMPANII + k dob (pora kanoniczna).
    /// Ten sam takt (Campaign.DailyTick) wola tez WeeklyTick - tydzien byl podwojny albo pominiety razem z doba.
    /// P1 (przyczyna, SessionStart - OnSessionLaunched po McmSettings.Apply, PRZED pierwszym tyknieciem mapy; zapis wczytany juz w SyncData): NextTriggerTime taktu
    /// doby = max(C(L + 1), Now + 1 tyk), gdzie C(d) = d x doba + (start kampanii mod doba), L = ostatnia doba moda z zapisu ("arm_daygate"; zapis sprzed 280 -
    /// ArmsScrap 174b.6 "ostatnia doba"). C(L + 1) lezy zawsze w dniu L + 1 - bez podwojenia i bez luki; doba, ktorej pora juz minela, odpala zaraz po wczytaniu.
    /// P2 (straznik, prefiks First na Campaign.DailyTick): zapisuje numer dnia i chwile kazdego taktu; PIERWSZY takt po wczytaniu z tym samym numerem dnia co L
    /// pomija dla wszystkich sluchaczy (jak gra bez wczytania; przy dzialajacym P1 nie zachodzi), luke tylko zglasza; takt spozniony wraca na pore kanoniczna.
    /// W trakcie sesji P2 niczego nie pomija (kilka taktow w klatce przy przewinieciu czasu to prawdziwe nadrabianie gry) - tylko licznik i linia.
    /// Takt godzinowy ma ten sam blad gry (initialWait2) - NIE poprawiany (linia podaje tylko przesuniecie).
    /// Wylacznik MCM DailyTickOncePerDay (wylaczony: gra jak dotad; L i chwila taktu dalej zapisywane). Zapis "arm_daygate" = "1|lastDay|lastTickTicks|transitionDay".
    /// Linie: "Doba gry po wczytaniu (280)" (raz na sesje) i "Doba moda (280)" (pierwszy takt po wczytaniu, w sesji tylko anomalie).
    /// </summary>
    internal static class DayGate
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ---------------------------------------------------------------- stan w zapisie
        private static int _lastDay = -1;           // numer dnia ostatniego taktu doby (int)Now.ToDays
        private static long _lastTickTicks = -1;    // chwila ostatniego taktu (tyki gry)
        private static int _transitionDay = -1;     // dzien pierwszej sesji z 280 (nowa kampania albo pierwsze wczytanie)
        // ---------------------------------------------------------------- stan sesji (bez zapisu)
        private static bool _hasKey, _imported;     // klucz w zapisie; SyncData wczytal zapis (nie nowa kampania)
        private static int _savedLastDay = -1;      // L z zapisu (do linii i straznika pierwszego taktu)
        private static long _savedTickTicks = -1;
        private static string _src = "brak";
        private static bool _armed;                 // pierwszy takt po starcie sesji jeszcze przed nami
        private static long _sessionTicks0 = -1;    // chwila startu sesji (godziny "od wczytania" w ksiedze)
        private static int _ticks, _repeats, _gaps, _stumbles;
        private static string _patchNote = "nie wpieta";

        // odbicie (raz na proces)
        private static FieldInfo _fDaily, _fHourly, _fNext, _fNum, _fTpd, _fTph;
        private static ConstructorInfo _ctor;
        private static bool _reflTried;
        private static string _reflMissing;

        internal static bool On { get { var s = Settings.Current; return s != null && s.DailyTickOncePerDay; } }
        /// <summary>Ta sesja wczytala zapis (SyncData), a nie zaczela nowej kampanii.</summary>
        internal static bool Loaded { get { return _imported; } }
        internal static int TransitionDay { get { return _transitionDay; } }

        internal static void Reset()
        {
            _lastDay = -1; _lastTickTicks = -1; _transitionDay = -1;
            _hasKey = _imported = false; _savedLastDay = -1; _savedTickTicks = -1; _src = "brak";
            _armed = false; _sessionTicks0 = -1; _ticks = _repeats = _gaps = _stumbles = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 3) { try { Log.Error("DayGate." + where, e); } catch { } }
        }

        // ---------------------------------------------------------------- zapis
        internal static string Export()
        {
            return "1|" + _lastDay.ToString(Inv) + "|" + _lastTickTicks.ToString(Inv) + "|" + _transitionDay.ToString(Inv);
        }

        /// <summary>SyncData przy wczytaniu (s == null - stary zapis bez klucza: L z ArmsScrap w SessionStart).</summary>
        internal static void Import(string s)
        {
            _imported = true; _hasKey = false; _savedLastDay = -1; _savedTickTicks = -1;
            if (string.IsNullOrEmpty(s)) return;
            try
            {
                var a = s.Split('|');
                if (a.Length < 4 || a[0] != "1") return;
                int d; long t; int tr;
                if (!int.TryParse(a[1], NumberStyles.Integer, Inv, out d) || !long.TryParse(a[2], NumberStyles.Integer, Inv, out t) || !int.TryParse(a[3], NumberStyles.Integer, Inv, out tr)) return;
                _lastDay = d; _lastTickTicks = t; _transitionDay = tr;
                _savedLastDay = d; _savedTickTicks = t;
                _hasKey = d >= 0;
            }
            catch (Exception e) { Stumble("Import", e); }
        }

        // ---------------------------------------------------------------- odbicie
        private static bool Refl()
        {
            if (_reflTried) return _reflMissing == null;
            _reflTried = true;
            try
            {
                _fDaily = AccessTools.Field(typeof(Campaign), "_dailyTickEvent");
                _fHourly = AccessTools.Field(typeof(Campaign), "_hourlyTickEvent");
                _fNext = AccessTools.Field(typeof(MBCampaignEvent), "NextTriggerTime");
                _fNum = AccessTools.Field(typeof(CampaignTime), "_numTicks");
                _fTpd = AccessTools.Field(typeof(CampaignTime), "TimeTicksPerDay");
                _fTph = AccessTools.Field(typeof(CampaignTime), "TimeTicksPerHour");
                _ctor = AccessTools.Constructor(typeof(CampaignTime), new[] { typeof(long) });
                var miss = new StringBuilder();
                if (_fDaily == null) miss.Append("Campaign._dailyTickEvent ");
                if (_fNext == null) miss.Append("MBCampaignEvent.NextTriggerTime ");
                if (_fNum == null) miss.Append("CampaignTime._numTicks ");
                if (_fTpd == null) miss.Append("CampaignTime.TimeTicksPerDay ");
                if (_fTph == null) miss.Append("CampaignTime.TimeTicksPerHour ");
                if (_ctor == null) miss.Append("CampaignTime(long) ");
                _reflMissing = miss.Length > 0 ? miss.ToString().Trim() : null;
            }
            catch (Exception e) { _reflMissing = "wyjatek " + e.GetType().Name; Stumble("Refl", e); }
            return _reflMissing == null;
        }

        private static long Ticks(CampaignTime t) { return (long)_fNum.GetValue(t); }
        private static long Tpd { get { return (long)_fTpd.GetValue(null); } }
        private static long Tph { get { return (long)_fTph.GetValue(null); } }
        private static object Time(long ticks) { return _ctor.Invoke(new object[] { ticks }); }

        /// <summary>Faza kanoniczna: reszta startu kampanii modulo doba (0..doba-1).</summary>
        private static long Phase(long tpd)
        {
            long st = Ticks(Campaign.Current.Models.CampaignTimeModel.CampaignStartTime);
            long s = st % tpd;
            return s < 0 ? s + tpd : s;
        }

        private static long Canon(long day, long tpd, long s) { return day * tpd + s; }

        private static string Hm(long ticks, long tpd, long tph)
        {
            long inDay = ticks % tpd; if (inDay < 0) inDay += tpd;
            long h = inDay / tph, m = (inDay % tph) * 60 / tph;
            return h.ToString("00", Inv) + ":" + m.ToString("00", Inv);
        }

        private static string Hours(long dt, long tph) { return ((double)dt / tph).ToString("0.00", Inv); }

        /// <summary>Godziny gry od startu tej sesji (wczytania) do teraz; -1 = nieznane.</summary>
        internal static double HoursSinceSessionStart()
        {
            try
            {
                if (_sessionTicks0 < 0 || !Refl() || Campaign.Current == null) return -1.0;
                return (double)(Ticks(CampaignTime.Now) - _sessionTicks0) / Tph;
            }
            catch { return -1.0; }
        }

        // ---------------------------------------------------------------- P2: wpiecie
        internal static void Apply(Harmony h)
        {
            try
            {
                var m = AccessTools.Method(typeof(Campaign), "DailyTick", new[] { typeof(MBCampaignEvent), typeof(object[]) });
                if (m == null) { _patchNote = "BRAK Campaign.DailyTick(MBCampaignEvent, object[])"; Log.Info("Doba moda (280): " + _patchNote + " - straznik taktu doby nie wpiety."); return; }
                h.Patch(m, prefix: new HarmonyMethod(typeof(DayGate), nameof(DailyPrefix)) { priority = Priority.First });
                _patchNote = "wpieta";
            }
            catch (Exception e) { _patchNote = "blad wpiecia (" + e.GetType().Name + ")"; Log.Error("DayGate.Apply", e); }
        }

        // ---------------------------------------------------------------- P1: start sesji
        /// <summary>OnSessionLaunched, zaraz po McmSettings.Apply (przed pierwszym tyknieciem mapy): takt doby na pore kanoniczna i linia "Doba gry po wczytaniu (280)".</summary>
        internal static void SessionStart()
        {
            _armed = true; _ticks = _repeats = _gaps = 0;
            try
            {
                if (Campaign.Current == null) return;
                if (!Refl())
                {
                    _sessionTicks0 = -1;
                    Log.Info("Doba gry po wczytaniu (280): BRAK pola (" + _reflMissing + ") - takt doby gry bez zmian, zostaje tylko straznik pierwszego taktu (latka " + _patchNote + ").");
                    return;
                }
                long tpd = Tpd, tph = Tph;
                if (tpd <= 0 || tph <= 0) { Log.Info("Doba gry po wczytaniu (280): BRAK pola (doba " + tpd + " tykow) - takt doby gry bez zmian."); return; }
                long now = Ticks(CampaignTime.Now);
                _sessionTicks0 = now;
                long s = Phase(tpd);
                int today = (int)(now / tpd);
                // L: arm_daygate -> ArmsScrap 174b.6 (zapis sprzed 280) -> brak (nowa kampania albo zapis bez obu)
                int L = -1;
                if (_hasKey) { L = _savedLastDay; _src = "arm_daygate"; }
                else if (_imported)
                {
                    try { L = ArmsScrap.SavedLastDay(); } catch (Exception e) { Stumble("SessionStart(ArmsScrap)", e); L = -1; }
                    _src = L >= 0 ? "ArmsScrap 174b.6" : "brak";
                    _savedLastDay = L;
                }
                else _src = "nowa kampania";
                if (_transitionDay < 0) _transitionDay = today;
                var ev = _fDaily.GetValue(Campaign.Current) as MBCampaignEvent;
                if (ev == null) { Log.Info("Doba gry po wczytaniu (280): BRAK pola (Campaign._dailyTickEvent = null) - takt doby gry bez zmian."); return; }
                long next0 = Ticks((CampaignTime)_fNext.GetValue(ev));
                // pierwszy C(d) > Now (jak nowa gra)
                long dd = (now - s) / tpd; if (now - s < 0) dd--;
                long firstCanon = Canon(dd, tpd, s); if (firstCanon <= now) firstCanon += tpd;
                long target; string bad = "";
                if (L >= 0)
                {
                    target = Math.Max(Canon(L + 1, tpd, s), now + 1);
                    // L po dzisiejszym dniu = zapis niespojny (takt doby nie moze byc w przyszlosci) - pierwsza pora kanoniczna od teraz; C(L + 1) dalej niz doba od teraz jest
                    // poprawne (ostatni takt przed zapisem przed pora kanoniczna dnia L - odstep do 2 dob, bez podwojenia)
                    if (L > today) { bad = " [L z zapisu " + L + " lezy po dzisiejszym dniu " + today + " - pora kanoniczna od teraz]"; target = firstCanon; }
                }
                else target = firstCanon;
                string status;
                if (!On) status = "klucz wylaczony";
                else if (target == next0) status = "bez zmian";
                else { _fNext.SetValue(ev, Time(target)); status = "poprawione"; }
                // takt godzinowy: przesuniecie fazy wobec startu kampanii (gra, bez zmian)
                string hourly = "-";
                try
                {
                    var hv = _fHourly != null ? _fHourly.GetValue(Campaign.Current) as MBCampaignEvent : null;
                    if (hv != null)
                    {
                        long hn = Ticks((CampaignTime)_fNext.GetValue(hv));
                        long st = Ticks(Campaign.Current.Models.CampaignTimeModel.CampaignStartTime);
                        long off = (hn - st) % tph; if (off < 0) off += tph;
                        hourly = (off * 60 / tph).ToString(Inv);
                    }
                }
                catch (Exception e) { Stumble("SessionStart(godzinowy)", e); }
                var sb = new StringBuilder(700);
                sb.Append("Doba gry po wczytaniu (280): dzien ").Append(today).Append(" (godz. ").Append(Hm(now, tpd, tph)).Append(_imported ? ")" : "; start nowej kampanii)")
                  .Append(" | ostatnia doba moda z zapisu ").Append(L >= 0 ? L.ToString(Inv) : "-").Append(" (zrodlo: ").Append(_src).Append(")")
                  .Append(", ostatni takt przed zapisem ").Append(_hasKey && _savedTickTicks >= 0 ? "dzien " + (_savedTickTicks / tpd).ToString(Inv) + " o " + Hm(_savedTickTicks, tpd, tph) : "nieznany")
                  .Append(" | gra ustawila takt doby za ").Append(Hours(next0 - now, tph)).Append(" h (initialWait = uplyniety ulamek doby - blad gry Campaign.CreateCampaignEvents)")
                  .Append(", Armoury: takt doby ").Append(L >= 0 ? (L + 1).ToString(Inv) : (target / tpd).ToString(Inv)).Append(" o ").Append(Hm(target, tpd, tph))
                  .Append(" (pora kanoniczna od startu kampanii ").Append(Hm(s, tpd, tph)).Append("), za ").Append(Hours(target - now, tph)).Append(" h [").Append(status).Append("]").Append(bad)
                  .Append(" | takt godzinowy przesuniety o ").Append(hourly).Append(" min (gra, bez zmian)")
                  .Append(" | straznik pierwszego taktu (Campaign.DailyTick) ").Append(_patchNote).Append(", 280 od dnia ").Append(_transitionDay)
                  .Append(" | klucz Daily Tick Once Per Day ").Append(On ? "tak" : "nie").Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        // ---------------------------------------------------------------- P2: straznik
        /// <summary>Prefiks First na Campaign.DailyTick (przed wszystkimi sluchaczami DailyTickEvent i WeeklyTick). false = takt pominiety (tylko pierwszy po wczytaniu, D == L).</summary>
        public static bool DailyPrefix()
        {
            try
            {
                if (Campaign.Current == null || !Refl()) return true;
                long tpd = Tpd, tph = Tph;
                if (tpd <= 0 || tph <= 0) return true;
                long now = Ticks(CampaignTime.Now);
                int D = (int)CampaignTime.Now.ToDays;
                int prev = _lastDay;
                long prevT = _lastTickTicks;
                if (_armed)
                {
                    _armed = false;
                    long s = Phase(tpd);
                    int L = _savedLastDay;
                    var ev = _fDaily.GetValue(Campaign.Current) as MBCampaignEvent;
                    string kind, fix = "";
                    bool skip = false;
                    if (L < 0) kind = _imported ? "bez ostatniej doby z zapisu" : "nowa kampania";
                    else if (D == L) { kind = On ? "POWTORZONA - pominieta dla wszystkich sluchaczy" : "POWTORZONA (klucz wylaczony - gra jak dotad)"; skip = On; _repeats++; }
                    else if (D == L + 1) kind = "kolejna";
                    else if (D > L + 1) { kind = "LUKA " + (L + 1) + ".." + (D - 1); _gaps++; }
                    else kind = "wstecz (dzien przed L " + L + ")";
                    // takt spozniony albo o zlej porze: CheckUpdate doda TriggerPeriod po sluchaczach -> nastepny takt na C(D + 1)
                    if (On && ev != null)
                    {
                        long next = Ticks((CampaignTime)_fNext.GetValue(ev));
                        long want = Canon(D + 1, tpd, s) - Ticks(ev.TriggerPeriod);
                        if (next != want) { _fNext.SetValue(ev, Time(want)); fix = " | spozniona albo o zlej porze (" + Hm(now, tpd, tph) + "): pora kanoniczna przywrocona od dnia " + (D + 1); }
                    }
                    long ref0 = _savedTickTicks >= 0 ? _savedTickTicks : -1;
                    Log.Info("Doba moda (280): dzien " + D + " - pierwsza doba " + (_imported ? "po wczytaniu" : "nowej kampanii") + ", odstep " + (ref0 >= 0 ? Hours(now - ref0, tph) + " h" : "nieznany") + " od ostatniej doby przed zapisem ("
                             + (L >= 0 ? L.ToString(Inv) : "-") + ", zrodlo " + _src + "), takt o " + Hm(now, tpd, tph) + " | " + kind + fix + ".");
                    if (skip) return false;
                }
                else if (prev >= 0)
                {
                    if (D == prev) { _repeats++; Log.Info("Doba moda (280): dzien " + D + " - POWTORZONA w sesji (odstep " + Hours(now - prevT, tph) + " h; przewiniecie czasu - gra nadrabia, bez pomijania), powtorzen w sesji " + _repeats + "."); }
                    else if (D > prev + 1) { _gaps++; Log.Info("Doba moda (280): dzien " + D + " - LUKA " + (prev + 1) + ".." + (D - 1) + " w sesji (odstep " + Hours(now - prevT, tph) + " h), luk w sesji " + _gaps + "."); }
                }
                _lastDay = D; _lastTickTicks = now; _ticks++;
            }
            catch (Exception e) { Stumble("DailyPrefix", e); }
            return true;
        }
    }
}
