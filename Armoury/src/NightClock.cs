using System;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// ZEGAR KSIEGI SNU (noc 15/16.10, klucz NightLedgerClock; Jeff 15.10: "nie pokazuje obozu, nie rozbija, raz kara jest, raz nie").
    /// Tick godzinowy gry biegnie w pierwszej klatce PO swoim terminie, z biezacym czasem (gra MBCampaignEvent.CheckUpdate: while NextTriggerTime.IsPast
    /// RunHandlers(Now); NextTriggerTime += 1 h), a faza terminu jest ustawiana przy wczytaniu (Campaign.CreateCampaignEvents, initialWait2 = ulamek
    /// godziny). Przy fazie :55-:04 i szybkim czasie zegar w chwili ticku raz wypada przed, raz za pelna godzina: godzina 6:00 bywala niewidoczna
    /// (swit bez rozliczenia, potem odpoczynek z 2-6 dob naraz - log 10-56-30: 17 pominietych switow na 44 doby), a noc obozu swiata miewala 5 tikow
    /// zamiast 6 (dlug 1 dla 203-423 partii AI naraz). Godzina ksiegi = godzina TERMINU ticku (Campaign._hourlyTickEvent.NextTriggerTime - w handlerze
    /// to termin biezacego ticku, += dopiero po RunHandlers): termin rosnie dokladnie o 1 h, wiec kazda godzina doby trafia raz, przy kazdej fazie
    /// i predkosci. Przy dobrej fazie (:05-:54) godzina terminu = godzina zegara - zachowanie bez zmian.
    /// Zapas (odbicie nie dziala, termin poza zakresem): licznik +1 h na tick od pierwszego odczytu (ponowne zakotwiczenie przy rozjezdzie z zegarem).
    /// Klucz wylaczony = godzina z zegara gry (stare zachowanie). Bez zapisu - stan sesji (Reset przy nowej grze i wczytaniu: NightRest.ResetWorld).
    /// Straznicy w OnTick (CampHolds, IsCamping, SleepKind, RefreshNearbyTents) zostaja na zegarze gry (F7 trzyma oboz do ticku switu).
    /// </summary>
    internal static class NightClock
    {
        internal static int Hour;                 // godzina doby ksiegi (0..23) - ustawiana na poczatku NightRest.OnHourly
        internal static double NomH = -1;         // godzina ksiegi w godzinach kampanii (termin ticku; zapas - licznik; klucz wylaczony - zegar)
        internal static bool CatchUp;             // tick nadrabiany w tej samej klatce (zegar < 0.5 h od poprzedniego ticku) - jak T10-R R7
        internal static string Source = "zegar";  // "gra" (termin ticku), "licznik" (zapas), "zegar" (klucz wylaczony) - do linii
        private static double _clockH = -1, _lastClockH = -1;
        private static double _fallH = -1;        // licznik zapasowy (godziny kampanii)
        private static FieldInfo _fEvent, _fNext;
        private static bool _tried;
        private static bool _logged;
        private static int _stumbles, _badTerm;   // potkniecia (wyjatki) i terminy poza zakresem - w sesji
        // od ostatniego switu ksiegi AI (linia "NocnyMarsz: swit dnia"): tikow, z godzina zegara inna niz ksiegi, z licznika zapasowego
        private static int _ticks, _diff, _fallTicks;
        private static long _lastDawnDay = -1;

        /// <summary>Nowa gra albo wczytanie: zegar ksiegi od zera (odbicie zostaje - pola gry sa te same w calym procesie).</summary>
        internal static void Reset()
        {
            Hour = 0; NomH = -1; CatchUp = false; Source = "zegar";
            _clockH = -1; _lastClockH = -1; _fallH = -1;
            _logged = false; _stumbles = 0; _badTerm = 0;
            _ticks = 0; _diff = 0; _fallTicks = 0; _lastDawnDay = -1;
        }

        /// <summary>Raz na tick godzinowy (NightRest.OnHourly, przed ksiega AI i gracza): godzina ksiegi tego ticku.</summary>
        internal static void Tick(Settings s)
        {
            double nowH = -1;
            int clockHour = 0;
            try
            {
                var now = CampaignTime.Now;
                nowH = now.ToHours;
                clockHour = now.GetHourOfDay;
                CatchUp = _lastClockH >= 0 && nowH >= _lastClockH && nowH - _lastClockH < 0.5;
                _lastClockH = nowH; _clockH = nowH;
                double lagMin = -1;
                if (s == null || !s.NightLedgerClock)
                {
                    Hour = clockHour; NomH = nowH; Source = "zegar"; _fallH = -1;
                }
                else
                {
                    CampaignTime term;
                    if (ReadTerm(now, out term))
                    {
                        NomH = term.ToHours; Hour = term.GetHourOfDay; Source = "gra"; _fallH = NomH;
                        lagMin = (now - term).ToHours * 60.0;
                    }
                    else
                    {
                        // zapas: kazdy tick to jedna godzina od ostatniego odczytu; rozjazd z zegarem (wczytanie, dluga przerwa) - od zegara
                        double next = _fallH < 0 ? nowH : _fallH + 1.0;
                        if (nowH - next > 3.0 || next - nowH > 1.5) next = nowH;
                        _fallH = next; NomH = next; Hour = Mod24(next); Source = "licznik"; _fallTicks++;
                    }
                }
                _ticks++;
                if (Hour != clockHour) _diff++;
                if (!_logged) { _logged = true; LogStart(s, nowH, lagMin); }
            }
            catch (Exception e)
            {
                // potkniecie liczone, nie gasi ksiegi: ta godzina z zegara (stare zachowanie)
                _stumbles++;
                if (_stumbles <= 3 || _stumbles % 100 == 0) Log.Error("NightClock.Tick (potkniecie " + _stumbles + " w sesji)", e);
                try { if (nowH < 0) { nowH = CampaignTime.Now.ToHours; clockHour = CampaignTime.Now.GetHourOfDay; } } catch { }
                Hour = clockHour; NomH = nowH; Source = "zegar";
            }
        }

        /// <summary>Termin biezacego ticku godzinowego gry: wiarygodny, gdy minal (termin &lt;= teraz) i nie dalej niz 24 h wstecz.</summary>
        private static bool ReadTerm(CampaignTime now, out CampaignTime term)
        {
            term = CampaignTime.Zero;
            if (!_tried)
            {
                _tried = true;
                _fEvent = AccessTools.Field(typeof(Campaign), "_hourlyTickEvent");
                _fNext = AccessTools.Field(typeof(MBCampaignEvent), "NextTriggerTime");
                if (_fEvent == null || _fNext == null)
                    Log.Info("Nocleg (zegar ksiegi): brak pola gry " + (_fEvent == null ? "Campaign._hourlyTickEvent" : "MBCampaignEvent.NextTriggerTime")
                             + " - godzina ksiegi z licznika tikow (zapas).");
            }
            if (_fEvent == null || _fNext == null || Campaign.Current == null) return false;
            var ev = _fEvent.GetValue(Campaign.Current) as MBCampaignEvent;
            if (ev == null) return false;
            object v = _fNext.GetValue(ev);
            if (!(v is CampaignTime)) return false;
            var t = (CampaignTime)v;
            double dh = (now - t).ToHours;
            if (dh < 0 || dh >= 24.0) { _badTerm++; return false; }
            term = t;
            return true;
        }

        private static int Mod24(double h)
        {
            long x = (long)Math.Floor(h);
            return (int)(((x % 24) + 24) % 24);
        }

        /// <summary>Numer nocy obozu: dzien kampanii, w ktorym zaczela sie noc obozu biezacej godziny ksiegi (noc 22-6 przez polnoc = jeden numer).</summary>
        internal static long NightId(int campStart)
        {
            return (long)Math.Floor((Math.Floor(NomH) - campStart) / 24.0);
        }

        /// <summary>Godzina:minuty z godzin kampanii (do linii).</summary>
        internal static string HM(double h)
        {
            if (h < 0) return "-";
            double m = (h - Math.Floor(h)) * 60.0;
            int mm = Math.Min(59, (int)Math.Floor(m + 1e-6));
            return Mod24(h).ToString("00", CultureInfo.InvariantCulture) + ":" + mm.ToString("00", CultureInfo.InvariantCulture);
        }

        private static void LogStart(Settings s, double nowH, double lagMin)
        {
            var ci = CultureInfo.InvariantCulture;
            string what;
            if (s == null || !s.NightLedgerClock)
                what = "wylaczona (NightLedgerClock nie) - godzina z zegara gry, stare zachowanie (zegar " + HM(nowH) + ")";
            else if (Source == "gra")
                what = "TAK (termin " + HM(NomH) + ", zegar " + HM(nowH) + ", opoznienie " + Math.Max(0.0, lagMin).ToString("0", ci) + " min)";
            else
                what = "NIE - zapas: licznik tikow (" + (_fEvent == null || _fNext == null ? "brak pola gry" : "termin poza zakresem " + _badTerm + " x")
                       + "; godzina ksiegi " + HM(NomH) + ", zegar " + HM(nowH) + ")";
            Log.Info("Nocleg (zegar ksiegi): start sesji | godzina ksiegi z terminu ticku godzinowego gry " + what
                     + " | klucze: NightLedgerClock " + YesNo(s != null && s.NightLedgerClock) + ", PlayerSleepGuard " + YesNo(s != null && s.PlayerSleepGuard)
                     + ", NightMarchNotice " + YesNo(s != null && s.NightMarchNotice) + ".");
        }

        private static string YesNo(bool b) { return b ? "TAK" : "NIE"; }

        /// <summary>Kawalek linii switu ksiegi AI: zrodlo i stan zegara od poprzedniego switu; liczniki od zera po wypisaniu.</summary>
        internal static string DawnText()
        {
            long day = NomH >= 0 ? (long)Math.Floor(Math.Floor(NomH) / 24.0) : -1;
            string gap = _lastDawnDay < 0 || day < _lastDawnDay ? "-" : Math.Max(0L, day - _lastDawnDay - 1).ToString(CultureInfo.InvariantCulture);
            string t = " | zegar ksiegi (" + Source + "): tick switu termin " + HM(NomH) + ", zegar " + HM(_clockH)
                       + "; tikow z godzina zegara inna niz ksiegi " + _diff + " z " + _ticks
                       + (_fallTicks > 0 ? " (z licznika zapasowego " + _fallTicks + ")" : "")
                       + "; dob bez switu od poprzedniego " + gap
                       + "; potkniecia zegara w sesji " + _stumbles + ", terminy poza zakresem " + _badTerm;
            _lastDawnDay = day;
            _ticks = 0; _diff = 0; _fallTicks = 0;
            return t;
        }
    }
}
