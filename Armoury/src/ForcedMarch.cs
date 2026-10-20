using System;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// R3-08c - MARSZ FORSOWNY GRACZA (Jeff 20.10: "28 - nie, 29 - nie, pozostale tak"; docs/POMYSLY-35-2026-10-20.md pkt 23; audyt 07 tabela 4.4 N-4).
    /// Rozkaz tylko dla gracza: klawisz O (panel obozu) albo pytanie "Night Falls" - "Forced march". Klucz MCM ForcedMarch (domyslnie tak), wymaga NightRestEnabled
    /// (bez ksiegi snu marsz bylby darmowy).
    ///   - predkosc druzyny (i armii, ktora gracz prowadzi) +ForcedMarchSpeedPercent (25%) - od WYNIKU lancucha modeli, raz na lancuch (NightRest.SpeedPostfix,
    ///     przed kara dlugu snu - kara liczy sie od predkosci z premia, wiec dlug 1 zjada prawie caly zysk: 1.25 x 0.75 = 0.94);
    ///   - sen sie nie liczy: godzina z rozkazem nie dodaje odpoczynku doby (NightRest.OnHourly), postoj w nocy nie stawia obozu automatycznego,
    ///     nie ma pytania o nocleg ani obozu z rozkazu "always make camp";
    ///   - dlug snu rosnie co 12 h zamiast raz na dobe: swit liczy dobe jak zawsze (bez snu = dlug +1), a do tego kazde 24 h marszu forsownego = jeszcze
    ///     jedna noc dlugu. Przy marszu bez przerwy daje to 2 noce na dobe (co 12 h); krotszy zryw zostawia "zmeczenie" (licznik godzin), ktore przechodzi na
    ///     nastepny marsz forsowny i schodzi o 2 h za kazda dobe zwyklego wypoczynku (baza snu i zero godzin marszu forsownego) - nie ma darmowych zrywow
    ///     przez wylaczanie przed 24 h. Kary dlugu juz sa (NightRest: predkosc -25/-40/-90%, morale -25/-40/-95%); dlug 3 = zapasc, rozkaz sie konczy.
    ///   Koniec sam: oboz (klawisz O, menu, oboz BK), sen w menu, wejscie do osady, oboz obleznikow, 3 h postoju bez ruchu, niewola, sluzba ROT,
    ///   doczepienie do cudzej armii, morze, dlug 3, Nieumarli, smierc bohatera, wylaczenie klucza albo ksiegi snu.
    ///   Stan w zapisie (arm_forcedmarch przez SaveText.Sync): wlaczony, licznik zmeczenia, godziny w biezacej dobie.
    ///   Recenzja 20.10: zanik zmeczenia liczony w galezi switu NightRest (Dawn) z odpoczynkiem doby PO doliczeniu ostatniej godziny nocy (5-6) i snem
    ///   w menu (jak sleptBase w SettleNightCore) - wczesniej zwykla noc 0-6 dawala tu 5 h i zanik nigdy nie zachodzil.
    /// Watki: SpeedBonus wolany z liczenia predkosci (rownolegle) czyta tylko pole volatile i ustawienia, bez alokacji; reszta z watku glownego.
    /// Log: "Marsz forsowny (R3-08c):" - wlaczenie, koniec (powod), krok dlugu i linia switu gracza z licznikami doby.
    /// </summary>
    internal static class ForcedMarch
    {
        private const float DebtHours = 24f;      // godziny marszu forsownego na jedna dodatkowa noc dlugu (razem ze switem: co 12 h przy marszu bez przerwy)
        private const float FadePerDay = 2f;      // o tyle godzin schodzi zmeczenie za dobe zwyklego wypoczynku
        private const int StillHoursEnd = 3;      // tyle godzin z rzedu bez ruchu (poza bitwa) = rozkaz sie konczy (stoja i nie spia - sam koszt)
        private static volatile bool _on;
        private static int _stillH;               // godziny z rzedu bez ruchu w trakcie rozkazu (bez zapisu)
        private static float _hours;              // zmeczenie marszem forsownym (godziny do nastepnego kroku dlugu)
        private static int _dayHours;             // godziny marszu forsownego w biezacej dobie (swit-swit)
        private static int _lastDawnDay = -1;
        // liczniki doby do linii switu
        private static int _dStarts, _dEnds, _dSteps;
        private static string _dEndWhy = "";
        private static int _stumbles;
        private static readonly TextObject _txt = new TextObject("{=!}Forced march");

        internal static bool Active { get { return _on; } }

        private static bool KeyOn(Settings s) { return s != null && s.ForcedMarch && s.NightRestEnabled; }

        private static float Pct(Settings s) { return s == null ? 0f : Math.Max(0, Math.Min(50, s.ForcedMarchSpeedPercent)) / 100f; }

        private static string F1(float v) { return v.ToString("0.#", CultureInfo.InvariantCulture); }

        private static void Msg(string t, Color c)
        {
            try { InformationManager.DisplayMessage(new InformationMessage(t, c)); } catch { }
        }

        internal static void Reset()
        {
            _on = false; _hours = 0f; _dayHours = 0; _lastDawnDay = -1; _stillH = 0;
            _dStarts = 0; _dEnds = 0; _dSteps = 0; _dEndWhy = "";
        }

        /// <summary>Czy rozkaz ma sie pokazac w panelu O / pytaniu Night Falls (klucz i ksiega snu wlaczone).</summary>
        internal static bool Offerable() { return KeyOn(Settings.Current); }

        /// <summary>Podpowiedz w panelu: co daje i co kosztuje, z biezacym zmeczeniem.</summary>
        internal static string Hint()
        {
            var s = Settings.Current;
            int pct = (int)Math.Round(Pct(s) * 100f);
            string tired = _hours >= 0.5f ? " The men already carry " + F1(_hours) + "h of forced-march weariness." : "";
            if (_on)
                return "Let the men sleep again. Weariness so far: " + F1(_hours) + "h of the " + F1(DebtHours) + "h that cost one more night's sleep; it fades with proper rest.";
            return "+" + pct + "% speed. No one sleeps while it lasts, and every " + F1(DebtHours) + "h of it costs one more sleepless night on top of the nights they miss." + tired;
        }

        /// <summary>Panel O: przelacznik.</summary>
        internal static void Toggle(string where)
        {
            if (_on) Stop("rozkaz gracza (" + where + ")", true);
            else Start(where);
        }

        /// <summary>Wlaczenie (opcja inquiry - bezpieczna sciezka UI; bez menu gry).</summary>
        internal static void Start(string where)
        {
            try
            {
                var s = Settings.Current;
                var mp = MobileParty.MainParty;
                if (!KeyOn(s) || mp == null || _on) return;
                string why = Blocker(mp);
                if (why != null)
                {
                    Msg("No forced march now - " + why + ".", Colors.Yellow);
                    Log.Info("Marsz forsowny (R3-08c): odmowa (" + where + ") - " + why + ", dlug " + NightRest.Debt + ".");
                    return;
                }
                NightRest.StrikeCampForMarch();   // oboz zwiniety raz, przy zmianie stanu (nie co klatke)
                _stillH = 0;
                _on = true;
                _dStarts++;
                int pct = (int)Math.Round(Pct(s) * 100f);
                Msg("Forced march! The column pushes on at +" + pct + "% speed. No one sleeps while it lasts, and every " + F1(DebtHours)
                    + "h of it costs one more night's sleep." + (_hours >= 0.5f ? " (Weariness already carried: " + F1(_hours) + "h.)" : ""), Colors.Yellow);
                Log.Info("Marsz forsowny (R3-08c): wlaczony (" + where + ") - predkosc +" + pct + "%, zmeczenie " + F1(_hours) + "/" + F1(DebtHours) + " h, dlug " + NightRest.Debt + ".");
            }
            catch (Exception e) { Log.Error("ForcedMarch.Start", e); }
        }

        /// <summary>Co nie pozwala zaczac (null = mozna). Dlug 3 = zapasc - ludzie nie ida dalej.</summary>
        private static string Blocker(MobileParty mp)
        {
            if (TaleWorlds.CampaignSystem.PlayerCaptivity.IsCaptive) return "you are a captive";
            if (NightRest.EnlistedNow) return "your lord decides the marches";
            if (mp.CurrentSettlement != null) return "the column is inside a settlement";
            if (mp.BesiegerCamp != null) return "the army is encamped for a siege";
            if (mp.IsCurrentlyAtSea) return "the crews already sail in watches";
            if (mp.AttachedTo != null) return "you march in another lord's army";
            if (NightRest.SleepingNow) return "the men are asleep";
            if (Undead.Party(mp)) return "the dead do not tire";
            if (NightRest.Debt >= 3) return "the men can go no further";
            return null;
        }

        private static void Stop(string why, bool tell)
        {
            if (!_on) return;
            _on = false;
            _dEnds++;
            _dEndWhy = _dEndWhy.Length == 0 ? why : (_dEndWhy.Length < 200 ? _dEndWhy + ", " + why : _dEndWhy);
            if (tell) Msg("The forced march ends." + (_hours >= 0.5f ? " The men carry " + F1(_hours) + "h of its weariness; it fades with proper rest." : ""), Colors.White);
            Log.Info("Marsz forsowny (R3-08c): koniec - " + why + "; zmeczenie " + F1(_hours) + "/" + F1(DebtHours) + " h, dlug " + NightRest.Debt + ".");
        }

        /// <summary>Recenzja 20.10: wyjscia NightRest.OnHourly przed ksiega gracza (ksiega snu wylaczona, bohater martwy, Nieumarli) - rozkaz schodzi bez
        /// komunikatu; inaczej premia predkosci zostawala bez kosztu (Hour juz nie przychodzi). Bez rozkazu nic nie robi (wolane co godzine).</summary>
        internal static void Cancel(string why)
        {
            if (!_on) return;
            try { Stop(why, false); } catch (Exception e) { _on = false; Log.Error("ForcedMarch.Cancel", e); }
        }

        /// <summary>
        /// NightRest.OnHourly (ksiega gracza, przed dodaniem odpoczynku godziny): konce same, licznik zmeczenia i krok dlugu.
        /// movedH = godzina z ruchem (ten sam pomiar co ksiega snu, z tickiem nadrabianym). Zwraca true, gdy ta godzina jest godzina marszu forsownego
        /// (odpoczynek sie nie liczy). Zanik zmeczenia i linia switu - Dawn (galaz switu NightRest, po doliczeniu godziny).
        /// </summary>
        internal static bool Hour(Settings s, MobileParty mp, bool movedH)
        {
            try
            {
                if (_on && !KeyOn(s)) Stop("klucz Forced March albo Night Rest wylaczony", true);
                if (_on && mp != null)
                {
                    // recenzja 20.10: kolumna stoi (gracz zapomnial wylaczyc) - stoja i nie spia, sam koszt; bitwa i spotkanie nie licza sie jako postoj
                    if (movedH || mp.MapEvent != null) _stillH = 0; else _stillH++;
                    string why = null;
                    if (NightRest.PlayerCamped) why = "oboz";
                    else if (NightRest.SleepingNow) why = "sen w menu";
                    else if (mp.CurrentSettlement != null) why = "osada " + mp.CurrentSettlement.Name;
                    else if (mp.BesiegerCamp != null) why = "oboz obleznikow";   // recenzja 20.10: oboz oblezenia to postoj - bez snu dlug rosl do 3
                    else if (_stillH >= StillHoursEnd) why = StillHoursEnd + " h postoju bez ruchu";
                    else if (TaleWorlds.CampaignSystem.PlayerCaptivity.IsCaptive) why = "niewola";
                    else if (NightRest.EnlistedNow) why = "sluzba ROT";
                    else if (mp.AttachedTo != null) why = "cudza armia";
                    else if (mp.IsCurrentlyAtSea) why = "morze";
                    else if (Undead.Party(mp)) why = "Nieumarli";
                    else if (NightRest.Debt >= 3) why = "dlug 3 (zapasc o swicie)";
                    if (why != null && _stillH >= StillHoursEnd) Msg("The column has stood still for " + _stillH + " hours.", Colors.White);
                    if (why != null) Stop(why, true);
                }
                bool counted = false;
                if (_on)
                {
                    counted = true;
                    _hours += 1f; _dayHours++;
                    if (_hours + 0.001f >= DebtHours)
                    {
                        _hours = Math.Max(0f, _hours - DebtHours);
                        int before = NightRest.Debt;
                        NightRest.Debt = Math.Min(3, NightRest.Debt + 1);
                        NightRest.DawnDebt = Math.Max(NightRest.DawnDebt, NightRest.Debt);   // jak noc bez snu (SettleDeferred): dzien bez musztry
                        _dSteps++;
                        int d = NightRest.Debt;
                        Log.Info("Marsz forsowny (R3-08c): " + F1(DebtHours) + " h marszu forsownego - dodatkowa noc dlugu, dlug " + before + " -> " + d + ".");
                        if (d >= 3)
                        {
                            Msg("The forced march breaks the men - they collapse where they stand.", Colors.Red);
                            Stop("dlug 3 (zapasc)", false);
                            try { if (mp != null && mp.CurrentSettlement == null && mp.MapEvent == null) mp.SetMoveModeHold(); } catch { }
                        }
                        else Msg("A whole day of forced march - the men owe one more night's sleep (sleep debt " + d + ").", Colors.Yellow);
                    }
                }
                return counted;
            }
            catch (Exception e)
            {
                _stumbles++;
                if (_stumbles <= 3 || _stumbles % 100 == 0) Log.Error("ForcedMarch.Hour (potkniecie " + _stumbles + ")", e);
                return _on;
            }
        }

        /// <summary>
        /// Galaz switu NightRest.OnHourly (raz na swit, PRZED SettleNight): restDay = odpoczynek doby z doliczona godzina, ktora wlasnie minela (tick 6:00
        /// liczy godzine 5-6 - T1), sleeping = sen w menu przez swit (jak sleptBase w SettleNightCore). Zanik zmeczenia: doba bez marszu forsownego i z przespana
        /// baza. Linia switu gracza z licznikami doby.
        /// </summary>
        internal static void Dawn(Settings s, float restDay, bool sleeping)
        {
            try
            {
                int day = (int)CampaignTime.Now.ToDays;
                if (day == _lastDawnDay) return;
                _lastDawnDay = day;
                float baza = s != null ? Math.Max(1f, s.SleepHoursNeeded) : 6f;
                bool slept = sleeping || restDay + 0.01f >= baza;   // zapas 0.01 h na zaokraglenie float (jak SettleNightCore)
                float before = _hours;
                if (!_on && _dayHours == 0 && slept && _hours > 0f) _hours = Math.Max(0f, _hours - FadePerDay);
                if (_dayHours > 0 || _dStarts + _dEnds + _dSteps > 0 || before > 0f || _on)
                    Log.Info("Marsz forsowny (R3-08c): swit gracza dzien " + day + " | wlaczony " + (_on ? "tak" : "nie") + " | godzin marszu forsownego w dobie " + _dayHours
                             + " | zmeczenie " + F1(before) + " -> " + F1(_hours) + "/" + F1(DebtHours) + " h | odpoczynek doby " + F1(restDay) + " h z bazy " + F1(baza)
                             + (sleeping ? " (sen w menu)" : "") + " | dlug " + NightRest.Debt
                             + " | wlaczenia " + _dStarts + ", konce " + _dEnds + (_dEndWhy.Length > 0 ? " (" + _dEndWhy + ")" : "") + ", kroki dlugu " + _dSteps
                             + " | potkniecia " + _stumbles + ".");
                _dayHours = 0; _dStarts = 0; _dEnds = 0; _dSteps = 0; _dEndWhy = "";
            }
            catch (Exception e)
            {
                _stumbles++;
                if (_stumbles <= 3 || _stumbles % 100 == 0) Log.Error("ForcedMarch.Dawn (potkniecie " + _stumbles + ")", e);
            }
        }

        /// <summary>
        /// NightRest.SpeedPostfix (partia gracza, zewnetrzny poziom lancucha modeli): +% od wyniku, jak premia karczmy (TownInns.SpeedBonus) - sufit
        /// lancucha podniesiony o ten sam procent. Wolane rownolegle: tylko odczyty, bez alokacji (staly TextObject).
        /// </summary>
        internal static void SpeedBonus(ref ExplainedNumber r)
        {
            if (!_on) return;
            try
            {
                var s = Settings.Current;
                if (!KeyOn(s)) return;
                float pct = Pct(s);
                float add = r.ResultNumber * pct;
                if (add <= 0f) return;
                float lim = r.LimitMaxValue;
                float f = 1f + r.SumOfFactors;
                r.Add(f > 0.01f ? add / f : add, _txt);
                if (lim > 0f && lim < float.MaxValue * 0.5f) r.LimitMax(lim * (1f + pct) + 0.001f);
            }
            catch { _stumbles++; }
        }

        // ------------------------------------------------------------ zapis (arm_forcedmarch, SaveText.Sync)
        internal static string Export()
        {
            return (_on ? "1" : "0") + ";" + _hours.ToString("0.###", CultureInfo.InvariantCulture) + ";" + _dayHours.ToString(CultureInfo.InvariantCulture);
        }

        internal static void Import(string data)
        {
            try
            {
                Reset();
                if (string.IsNullOrEmpty(data)) return;
                var p = data.Split(';');
                _on = p.Length > 0 && p[0] == "1";
                float hh;
                if (p.Length > 1 && float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out hh)) _hours = Math.Max(0f, Math.Min(DebtHours, hh));
                int dh;
                if (p.Length > 2 && int.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out dh)) _dayHours = Math.Max(0, Math.Min(48, dh));
                if (_on) Log.Info("Marsz forsowny (R3-08c): wczytany zapis - rozkaz trwa, zmeczenie " + F1(_hours) + "/" + F1(DebtHours) + " h.");
            }
            catch (Exception e) { Log.Error("ForcedMarch.Import", e); }
        }
    }

    internal static partial class NightRest
    {
        /// <summary>R3-08c: wlaczenie marszu forsownego zwija oboz gracza (raz, przy zmianie stanu - jak ruch w OnHourly).</summary>
        internal static void StrikeCampForMarch()
        {
            try
            {
                var mp = MobileParty.MainParty;
                if (PlayerCamped && mp != null) { Tent(mp, false); PlayerCamped = false; }
            }
            catch (Exception e) { Log.Error("NightRest.StrikeCampForMarch", e); }
        }
    }
}
