using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// m98-plotki (Jeff 20.10 15:25: "za kazde wypite piwo moge dostac tyle plotek, ile moja charyzma ... jak wypije za duzo piwa, mam minus do predkosci i musze dospac,
    /// i nastepny dzien kac") - KUFLE GRACZA, PIJANSTWO I KAC. Tylko gracz (bohater gracza i jego partia na mapie). Klucze: InnGossipNeedsDrink (kufel przed plotkami -
    /// Rumours.Drink.cs), GossipPerDrinkByCharm (plotek na kufel), DrinkDrunkHangover (pijanstwo i kac; wymaga NightRestEnabled - kary ida przez latki predkosci
    /// i morale NightRest, a odsypianie przez ksiege snu NightRest).
    ///  - PROG UPICIA: DrunkAfterTankards (3) + 1 przy Wytrzymalosci 5 + 1 przy Atletyce 150 (3-5 kufli); kufle liczone od ostatniego odespania (4 h odpoczynku
    ///    po ostatnim kuflu zeruje licznik, gdy gracz nie jest pijany). Karczmarz nie naleje ponad prog + 3 (MaxCups).
    ///  - PIJANY (kufel ponad prog): predkosc na mapie -DrunkSpeedPenaltyPercent (15%) za pierwszy kufel ponad prog, +5% za kazdy nastepny, najwyzej 2 x (30%);
    ///    do odespania: trzeba odpoczac SleepHoursNeeded (6 h) + 1 h na kufel ponad prog, liczone od ostatniego kufla tym samym odpoczynkiem co ksiega snu NightRest
    ///    (noc 1/h, dzien DayRestFactor, sen w karczmie x SleepFactor). Dlug snu rosnie o te godziny: NightRest.NeededHours dolicza ExtraSleepH, wiec sen w menu
    ///    trwa dluzej (gracz "dosypia"), a splata starego dlugu wymaga i tych godzin. Recenzja (technika): pobudka z menu snu wyrownuje odpoczynek z paskiem snu
    ///    (OnSleepInit / OnWake - pelny pasek = odespane od razu; tiki godzinowe gubily brzegi i gracz budzil sie pijany). Kowadlo: NightRest.NeededHoursFresh.
    ///  - KAC (po odespaniu): predkosc -HangoverSpeedPenaltyPercent (10%) i morale -HangoverMoralePenaltyPercent (10%, czynnik jak kara snu) do zmierzchu (18:00)
    ///    dnia, w ktorym gracz odespal (odespane od poludnia - 6 h). Kufel w czasie kaca zdejmuje kac (pijany zamiast skacowanego). Komunikat: po menu snu "You wake
    ///    with a hangover", odespane w postoju lub obozie na mapie - "The ale wears off, but a hangover follows". Recenzja (gra): pijany albo skacowany - premia
    ///    "wypoczety w lozku" karczmy (TownInns.SpeedBonus / MoraleBonus) wstrzymana (SpoilsRest), dotad kac i premia znosily sie do ok. 0.
    ///  - Kary liczone na watku glownym (kufel, godzina, wczytanie) do pol _spdPct / _morPct (int - odczyt w latkach z dowolnego watku jest bezpieczny).
    ///  - Ksiega uslyszanych plotek: miejsce (karczma miasta - id miasta, zajazd - uid) -> skrot tozsamosci wiesci (Rumours Item.Id: turniej = miasto + doba
    ///    ogloszenia, swaty = bohater, banda = partia + miejsce + spalone wsie, wojsko = partia + cel, karawana = partia + cel, ceny = towar + dwa miasta, wiesc
    ///    wojenna = rodzaj + doba + strony; recenzja gra - dotad skrot tekstu) -> doba; ta sama wiesc w tym miejscu nie wraca; wpisy starsze niz HeardDays (10)
    ///    odpadaja; najwyzej HeardPlaces miejsc i HeardPerPlace wpisow.
    ///  - Zapis "arm_drink" (SaveText.Sync, v1); brak klucza = trzezwy, nic nie uslyszane.
    ///  - Log "Kufle (m98): " - start sesji (klucze, krzywa Uroku, prog), kazdy kufel (Rumours.Drink), upicie, odespanie, kac, linia doby; tools/sprawdz_logi.py --grupa m98plotki.
    /// Wyjatek - licznik potkniec (pierwszy do Log.Error), nic nie gasimy.
    /// </summary>
    internal static class InnDrink
    {
        internal const string P = "Kufle (m98): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const int HeardDays = 10, HeardPlaces = 400, HeardPerPlace = 60, CupsPastLimit = 3, ResetRestH = 4;

        internal static bool On { get { var s = Settings.Current; return s != null && s.InnGossip && s.InnGossipNeedsDrink; } }
        internal static bool ByCharm { get { var s = Settings.Current; return s != null && s.GossipPerDrinkByCharm; } }
        // m109-przesluch (pkt 4c): plotek na kufel wedlug miary perswazji (Persuasion.Measure - ta sama co przy przesluchaniu jencow), nie samego Uroku
        internal static bool ByPersuasion { get { var s = Settings.Current; return ByCharm && s != null && s.GossipPerDrinkByPersuasion; } }
        internal static bool DrunkOn { get { var s = Settings.Current; return s != null && s.InnGossip && s.InnGossipNeedsDrink && s.DrinkDrunkHangover && s.NightRestEnabled; } }

        // ------------------------------------------------------------ stan gracza (zapis arm_drink)
        private static int _cups, _over;
        private static bool _drunk, _hangMsg;
        private static float _slept;
        private static long _hangUntil;
        private static volatile int _spdPct, _morPct;
        private static volatile bool _isHang;
        // recenzja (technika, wazne): odpoczynek od kufla w chwili polozenia sie w menu snu (-1 = brak snu w menu) - pobudka wyrownuje _slept z paskiem snu
        // (pasek liczy ulamki godzin, a tiki godzinowe gubia brzegi - ta sama zasada co NightRest.LeaveSleep "pasek jest prawda o tym snie")
        private static float _lieSlept = -1f;
        private static readonly Dictionary<string, Dictionary<uint, int>> _heard = new Dictionary<string, Dictionary<uint, int>>(StringComparer.Ordinal);
        private static string _importNote = "brak klucza (stary zapis albo nowa kampania)";

        // liczniki doby i sesji
        internal static int DCups, DItems, DTourn, DMatch, DTournFalse, DMatchFalse, DRefused, DNoMoney, DNothingNew;
        internal static long DPaid, DFolk, DCoffer, DVillage, DTax, DCrown, DDiff;
        private static int _dDrunk, _dSober, _dHang, _dDrunkH, _dHangH, _dBadH, _dStumbles, _stumbles;
        private static bool _errLogged;

        private static readonly TextObject TxtDrunk = new TextObject("{=!}Drunk");
        private static readonly TextObject TxtHang = new TextObject("{=!}Hangover");

        internal static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            if (!_errLogged) { _errLogged = true; Log.Error("InnDrink." + where, e); }
        }

        internal static void Reset()
        {
            _cups = _over = 0; _drunk = _hangMsg = false; _slept = 0f; _hangUntil = 0; _spdPct = _morPct = 0; _isHang = false; _lieSlept = -1f;
            _heard.Clear(); _stumbles = 0; _errLogged = false; _importNote = "brak klucza (stary zapis albo nowa kampania)";
            DayReset();
        }

        private static void DayReset()
        {
            DCups = DItems = DTourn = DMatch = DTournFalse = DMatchFalse = DRefused = DNoMoney = DNothingNew = 0;
            DPaid = DFolk = DCoffer = DVillage = DTax = DCrown = DDiff = 0;
            _dDrunk = _dSober = _dHang = _dDrunkH = _dHangH = _dBadH = _dStumbles = 0;
        }

        private static long NowMin() { return (long)CampaignTime.Now.ToMinutes; }
        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }
        internal static int Cups { get { return _cups; } }
        internal static bool Drunk { get { return _drunk; } }
        internal static bool Hungover { get { return _hangUntil > 0; } }

        /// <summary>Recenzja (gra, wazne): kara kufli trwa (pijany albo kac) - premia "wypoczety w lozku" (TownInns.SpeedBonus / MoraleBonus) w tym czasie nie dziala:
        /// jedna zasada - kto zapil noc, temu lozko nie daje wypoczynku, dopoki kac nie minie. Pola int - odczyt z latek z dowolnego watku bezpieczny.</summary>
        internal static bool SpoilsRest { get { return _spdPct > 0 || _morPct > 0; } }

        // ------------------------------------------------------------ progi
        /// <summary>Prog upicia gracza: DrunkAfterTankards + 1 przy Wytrzymalosci 5 + 1 przy Atletyce 150 (najmniej 1).</summary>
        internal static int Threshold()
        {
            var s = Settings.Current;
            int t = s != null ? Math.Max(1, Math.Min(10, s.DrunkAfterTankards)) : 3;
            try
            {
                var me = Hero.MainHero;
                if (me != null)
                {
                    if (me.GetAttributeValue(DefaultCharacterAttributes.Endurance) >= 5) t++;
                    if (me.GetSkillValue(DefaultSkills.Athletics) >= 150) t++;
                }
            }
            catch { }
            return t;
        }

        /// <summary>Najwiecej kufli, jakie naleje karczmarz od ostatniego odespania (prog + 3). Klucz pijanstwa wylaczony - bez limitu.</summary>
        internal static int MaxCups() { return DrunkOn ? Threshold() + CupsPastLimit : int.MaxValue; }

        /// <summary>Plotek na kufel wedlug Uroku: ponizej 50 - 1, 50-109 - 2, 110-169 - 3, 170-229 - 4, od 230 - wszystkie (pool). Klucz wylaczony - wszystkie.</summary>
        internal static int PerDrink(int charm, int pool)
        {
            if (pool <= 0) return 0;
            if (!ByCharm || charm >= 230) return pool;
            return Math.Min(pool, 1 + Math.Max(0, (charm + 10) / 60));
        }

        internal static int Charm()
        {
            try { var me = Hero.MainHero; return me != null ? me.GetSkillValue(DefaultSkills.Charm) : 0; } catch { return 0; }
        }

        /// <summary>m109-przesluch (pkt 4c): "Urok" kufla - przy ByPersuasion miara perswazji gracza wobec ludzi karczmy (kultura miasta ceny kufla; p - skladniki),
        /// inaczej sam Urok jak dotad (p puste).</summary>
        internal static int Talk(CultureObject folk, out Persuasion.Parts p)
        {
            p = new Persuasion.Parts { LeaderPerk = "-" };
            if (!ByPersuasion) return Charm();
            p = Persuasion.Measure(Hero.MainHero, folk);
            return p.Total;
        }

        private static int DrunkPct(int over)
        {
            var s = Settings.Current;
            int b = s != null ? Math.Max(0, Math.Min(50, s.DrunkSpeedPenaltyPercent)) : 15;
            return Math.Min(2 * b, b + 5 * Math.Max(0, over - 1));
        }

        /// <summary>Godziny odpoczynku do odespania (od ostatniego kufla): baza snu + 1 h na kufel ponad prog.</summary>
        private static float NeedH()
        {
            var s = Settings.Current;
            float baza = s != null ? Math.Max(1f, s.SleepHoursNeeded) : 6f;
            return baza + Math.Max(1, _over);
        }

        /// <summary>NightRest.NeededHours (gracz): godziny ponad baze i odsetki, zeby sen w menu (cel = potrzeba - odpoczynek doby) trwal do odespania kufli.
        /// Trzezwy albo klucz wylaczony - 0.</summary>
        internal static float ExtraSleepH(float need, float restTonight)
        {
            try
            {
                if (!_drunk || !DrunkOn) return 0f;
                float rem = NeedH() - _slept;
                return Math.Max(0f, rem - need + Math.Max(0f, restTonight));
            }
            catch { return 0f; }
        }

        /// <summary>Kary teraz - watek glowny (kufel, godzina, wczytanie, odespanie).</summary>
        private static void Recompute()
        {
            var s = Settings.Current;
            if (!DrunkOn || s == null) { _spdPct = 0; _morPct = 0; _isHang = false; return; }
            if (_drunk) { _spdPct = DrunkPct(_over); _morPct = 0; _isHang = false; }
            else if (_hangUntil > 0)
            {
                _spdPct = Math.Max(0, Math.Min(50, s.HangoverSpeedPenaltyPercent));
                _morPct = Math.Max(0, Math.Min(50, s.HangoverMoralePenaltyPercent));
                _isHang = true;
            }
            else { _spdPct = 0; _morPct = 0; _isHang = false; }
        }

        // ------------------------------------------------------------ latki (NightRest.SpeedPostfix / MoralePostfix, partia gracza, raz na lancuch modeli)
        internal static void SpeedPenalty(ref ExplainedNumber r, bool dry)
        {
            int pct = _spdPct;
            if (pct <= 0) return;
            float cut = r.ResultNumber * pct / 100f;
            float f = dry ? 1f : 1f + r.SumOfFactors;   // jak kara snu: Add do bazy dzielone przez (1 + suma wspolczynnikow) - kara dokladnie z procentu
            if (cut > 0f) r.Add(f > 0.01f ? -cut / f : -cut, _isHang ? TxtHang : TxtDrunk);
        }

        internal static void MoralePenalty(ref ExplainedNumber r, bool dry)
        {
            int pct = _morPct;
            if (pct <= 0) return;
            float f = dry ? 1f : 1f + r.SumOfFactors;
            float p = pct / 100f;
            r.AddFactor(f > 0.01f ? -p * f : -p, TxtHang);
        }

        // ------------------------------------------------------------ kufel (Rumours.Drink - po zaplacie)
        /// <summary>Kufel wypity: licznik, odpoczynek od zera; ponad prog - pijany (komunikat). Zwraca opis stanu do linii logu.</summary>
        internal static string OnTankard()
        {
            _cups++;
            _slept = 0f;
            if (!DrunkOn) return "kufli " + _cups + " (pijanstwo wylaczone)";
            int th = Threshold();
            string state;
            if (_cups > th)
            {
                bool was = _drunk;
                _drunk = true;
                _over = Math.Max(_over, _cups - th);
                bool cured = _hangUntil > 0;
                _hangUntil = 0; _hangMsg = false;
                Recompute();
                if (!was) _dDrunk++;
                int need = (int)Math.Ceiling(NeedH());
                Msg(was ? "You are drunker still - your party moves " + _spdPct + "% slower until you sleep it off (" + need + " hours of rest)."
                        : "You are drunk - your party moves " + _spdPct + "% slower on the map until you sleep it off (" + need + " hours of rest)."
                          + (cured ? " At least the hangover is gone." : ""), Colors.Yellow);
                state = "kufli " + _cups + " z progu " + th + " -> PIJANY (ponad prog " + _over + ", predkosc -" + _spdPct + "%, odespac " + NeedH().ToString("0.0", Inv) + " h"
                        + (cured ? ", kac zdjety" : "") + ")";
                Log.Info(P + (was ? "pijany dalej" : "upicie") + " - kufli " + _cups + " (prog " + th + ", Wytrzymalosc/Atletyka gracza), ponad prog " + _over
                         + ", kara predkosci -" + _spdPct + "%, odespac " + NeedH().ToString("0.0", Inv) + " h odpoczynku (dlug snu +" + Math.Max(1, _over) + " h), dzien " + Today + ".");
            }
            else
            {
                if (_cups == th) Msg("One more tankard and you will be drunk.", Colors.White);
                state = "kufli " + _cups + " z progu " + th + (_hangUntil > 0 ? " (kac trwa)" : "");
            }
            return state;
        }

        private static void Msg(string text, Color c)
        {
            try { InformationManager.DisplayMessage(new InformationMessage(text, c)); } catch { }
        }

        // ------------------------------------------------------------ godzina (NightRest.OnHourly - odpoczynek tej godziny; ArmouryBehavior - stan)
        /// <summary>Odpoczynek tej godziny z ksiegi snu NightRest (noc 1, dzien DayRestFactor, 0 w marszu); sen w menu karczmy x SleepFactor jak pasek snu.</summary>
        internal static void RestHour(float gain, bool sleepingMenu)
        {
            try
            {
                if (!On || (!_drunk && _cups == 0) || gain <= 0f) return;
                float g = gain;
                if (sleepingMenu) g *= Math.Max(1f, TownInns.SleepFactor());
                _slept += g;
                if (_drunk && _slept + 0.01f >= NeedH()) Sober();
                else if (!_drunk && _cups > 0 && _slept >= ResetRestH)
                {
                    Log.Info(P + "licznik kufli wyzerowany po " + _slept.ToString("0.0", Inv) + " h odpoczynku (bylo " + _cups + ", trzezwy), dzien " + Today + ".");
                    _cups = 0; _slept = 0f;
                }
            }
            catch (Exception e) { Stumble("RestHour", e); }
        }

        private static void Sober(string how = null)
        {
            var s = Settings.Current;
            int over = _over;
            float slept = _slept;
            _drunk = false; _cups = 0; _over = 0; _slept = 0f;
            var now = CampaignTime.Now;
            long nm = NowMin();
            int h = now.GetHourOfDay;
            long until = h < 12 ? nm - nm % 60 + (18 - h) * 60L : nm + 6 * 60L;
            _hangUntil = until; _hangMsg = true;
            _dSober++; _dHang++;
            Recompute();
            Log.Info(P + "odespane - odpoczynek " + slept.ToString("0.0", Inv) + " h (ponad prog bylo " + over + ") o " + h + ":00, dzien " + Today
                     + " -> KAC do " + ((until / 60) % 24) + ":00 (" + ((until - nm) / 60.0).ToString("0.0", Inv) + " h): predkosc -" + _spdPct + "%, morale -" + _morPct + "%"
                     + (how != null ? ", " + how + " - komunikat od razu" : NightRest.SleepingNow ? ", komunikat po pobudce" : ", komunikat bez pobudki (postoj / oboz na mapie)") + ".");
        }

        /// <summary>Komunikat kaca: po pobudce z menu snu "You wake with a hangover"; odespane w postoju lub obozie na mapie (bez menu snu - recenzja gra:
        /// komunikat "wake" o 2:00 w obozie) - "The ale wears off".</summary>
        private static void HangMsg(bool wake)
        {
            _hangMsg = false;
            if (_hangUntil <= 0) return;
            string until = ((_hangUntil / 60) % 24) == 18 ? "dusk." : ((_hangUntil / 60) % 24) + ":00.";
            // m110-sam: sam bohater - bez zdania o morale ludzi; sami towarzysze - morale druzyny
            Msg((wake ? "You wake with a hangover - your head pounds: your party moves " : "The ale wears off, but a hangover follows - your party moves ")
                + _spdPct + "% slower" + PartyWords.Pick(" and the men's morale is " + _morPct + "% lower", " and your morale is " + _morPct + "% lower", " and your party's morale is " + _morPct + "% lower") + " until " + until, Colors.Yellow);
        }

        // ------------------------------------------------------------ sen w menu (NightRest.SleepInit / LeaveSleep / EndSleepQuiet)
        /// <summary>NightRest.SleepInit: odpoczynek od kufla w chwili polozenia sie (ponowne wejscie w menu snu - od nowa, jak _menuRest).</summary>
        internal static void OnSleepInit()
        {
            try { _lieSlept = On && (_drunk || _cups > 0) ? _slept : -1f; }
            catch (Exception e) { Stumble("OnSleepInit", e); }
        }

        /// <summary>Recenzja (technika, wazne): pobudka z menu snu (LeaveSleep) albo sen przerwany (EndSleepQuiet). Pasek snu (menuRest, z SleepFactor karczmy) jest
        /// prawda o tym snie: _slept = max(_slept, odpoczynek przy polozeniu sie + pasek); pijany i odespane - trzezwieje teraz (kac od pobudki), nie w nastepnym
        /// tiku godzinowym, ktorego w marszu juz nie ma. Potem komunikat kaca po pobudce (takze odespane tikiem w czasie snu). Linia logu "pobudka".</summary>
        internal static void OnWake(float menuRest, bool full, string how)
        {
            try
            {
                float lie = _lieSlept;
                _lieSlept = -1f;
                if (On && lie >= 0f && (_drunk || _cups > 0))
                {
                    float before = _slept;
                    _slept = Math.Max(_slept, lie + Math.Max(0f, menuRest));
                    bool was = _drunk;
                    float need = NeedH();
                    if (_drunk && DrunkOn && _slept + 0.01f >= need) Sober(how);
                    else if (!_drunk && _cups > 0 && _slept >= ResetRestH)
                    {
                        Log.Info(P + "licznik kufli wyzerowany po " + _slept.ToString("0.0", Inv) + " h odpoczynku (bylo " + _cups + ", trzezwy, " + how + "), dzien " + Today + ".");
                        _cups = 0; _slept = 0f;
                    }
                    if (was)
                        Log.Info(P + "pobudka (" + how + ") - pasek " + menuRest.ToString("0.0", Inv) + " h, pelny pasek " + (full ? "tak" : "nie") + ", odpoczynek od kufla "
                                 + lie.ToString("0.0", Inv) + " + pasek -> " + Math.Max(before, lie + Math.Max(0f, menuRest)).ToString("0.0", Inv) + " h z " + need.ToString("0.0", Inv) + " h -> "
                                 + (_drunk ? "dalej pijany" : "odespane") + ", dzien " + Today + ".");
                }
                if (_hangMsg) HangMsg(true);
            }
            catch (Exception e) { Stumble("OnWake", e); }
        }

        /// <summary>Godzina gry (po NightRest.OnHourly): koniec kaca, komunikat kaca po pobudce, klucz wylaczony - stan zdjety, liczniki godzin kary.</summary>
        internal static void Hourly()
        {
            try
            {
                if (!DrunkOn)
                {
                    if (_drunk || _hangUntil > 0)
                    {
                        Log.Info(P + "pijanstwo wylaczone (DrinkDrunkHangover / NightRestEnabled / InnGossipNeedsDrink) - stan zdjety (pijany " + (_drunk ? "tak" : "nie") + ", kac " + (_hangUntil > 0 ? "tak" : "nie") + ").");
                        _drunk = false; _over = 0; _hangUntil = 0; _hangMsg = false;
                    }
                    Recompute();
                    return;
                }
                if (_hangMsg && !NightRest.SleepingNow) HangMsg(false);   // odespane bez menu snu (postoj, oboz na mapie); po menu snu komunikat dal juz OnWake
                if (_hangUntil > 0 && NowMin() >= _hangUntil)
                {
                    _hangUntil = 0;
                    Recompute();
                    if (!NightRest.SleepingNow) Msg("Your head clears - the hangover is gone.", Colors.Green);
                    Log.Info(P + "koniec kaca, dzien " + Today + " " + CampaignTime.Now.GetHourOfDay + ":00.");
                }
                Recompute();
                if (_spdPct > 0 || _morPct > 0)
                {
                    if (_drunk) _dDrunkH++;
                    else if (_hangUntil > 0) _dHangH++;
                    else _dBadH++;   // nie powinno sie zdarzyc: kara bez stanu (prog sprawdz_logi 0)
                }
            }
            catch (Exception e) { Stumble("Hourly", e); }
        }

        // ------------------------------------------------------------ ksiega uslyszanych plotek
        internal static bool Heard(string place, uint h)
        {
            Dictionary<uint, int> d;
            return place != null && _heard.TryGetValue(place, out d) && d.ContainsKey(h);
        }

        internal static int HeardCount(string place)
        {
            Dictionary<uint, int> d;
            return place != null && _heard.TryGetValue(place, out d) ? d.Count : 0;
        }

        internal static void MarkHeard(string place, uint h)
        {
            if (string.IsNullOrEmpty(place) || place.IndexOfAny(new[] { ',', ';', '|', ':' }) >= 0) return;
            Dictionary<uint, int> d;
            if (!_heard.TryGetValue(place, out d))
            {
                if (_heard.Count >= HeardPlaces) return;
                d = new Dictionary<uint, int>();
                _heard[place] = d;
            }
            if (d.Count >= HeardPerPlace && !d.ContainsKey(h))
            {
                uint oldK = 0; int oldD = int.MaxValue;
                foreach (var kv in d) if (kv.Value < oldD) { oldD = kv.Value; oldK = kv.Key; }
                d.Remove(oldK);
            }
            d[h] = Today;
        }

        // ------------------------------------------------------------ doba, start sesji
        internal static void Daily(int day)
        {
            try
            {
                int pruned = 0, today = Today;
                var emptyPlaces = new List<string>();
                foreach (var kv in _heard)
                {
                    var old = new List<uint>();
                    foreach (var e in kv.Value) if (today - e.Value > HeardDays) old.Add(e.Key);
                    foreach (var k in old) kv.Value.Remove(k);
                    pruned += old.Count;
                    if (kv.Value.Count == 0) emptyPlaces.Add(kv.Key);
                }
                foreach (var k in emptyPlaces) _heard.Remove(k);
                int entries = 0;
                foreach (var kv in _heard) entries += kv.Value.Count;
                if (!On && DCups == 0 && !_drunk && _hangUntil == 0) { DayReset(); return; }
                Log.Info(P + "dzien " + day + " | klucze: kufel " + (On ? "tak" : "WYLACZONE") + ", Urok " + (ByCharm ? "tak" : "WYLACZONE") + ", pijanstwo " + (DrunkOn ? "tak" : "WYLACZONE")
                         + " | kufle " + DCups + ", zaplacone " + DPaid + " (karczmy miast: kiesa ludu " + DFolk + ", kasa miasta " + DCoffer + "; zajazdy: kiesy wsi " + DVillage
                         + ", podatek panow " + DTax + ", korona " + DCrown + "), kontrola obiegu (prog 0): roznica " + DDiff
                         + " | plotek uslyszanych " + DItems + " (turnieje " + DTourn + ", nieprawdziwe " + DTournFalse + "; swaty " + DMatch + ", nieprawdziwe " + DMatchFalse + ")"
                         + ", odmowy: bez pieniedzy " + DNoMoney + ", karczmarz nie nalal " + DRefused + ", nic nowego " + DNothingNew
                         + " | upicia " + _dDrunk + ", odespania " + _dSober + ", kace " + _dHang + ", godziny kary: pijany " + _dDrunkH + ", kac " + _dHangH + ", bez stanu " + _dBadH
                         + " | teraz: kufli " + _cups + ", pijany " + (_drunk ? "tak (ponad prog " + _over + ", odpoczynek " + _slept.ToString("0.0", Inv) + "/" + NeedH().ToString("0.0", Inv) + " h)" : "nie")
                         + ", kac " + (_hangUntil > 0 ? "do " + ((_hangUntil / 60) % 24) + ":00" : "nie") + ", kara predkosci " + _spdPct + "%, morale " + _morPct + "%"
                         + " | pula: " + Rumours.WorldPoolText()
                         + " | ksiega uslyszanych: miejsc " + _heard.Count + ", wpisow " + entries + ", usuniete stare " + pruned
                         + " | potkniecia dzis " + _dStumbles + " (od wczytania " + _stumbles + ").");
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { DayReset(); }
        }

        internal static void SessionStart()
        {
            try
            {
                // kac z przyszlosci dalej niz 24 h (zly zapis) albo juz miniony - zdjety
                long nm = NowMin();
                if (_hangUntil > 0 && (_hangUntil - nm > 24 * 60 || _hangUntil <= nm)) { _importNote += " (kac z zapisu poza oknem - zdjety)"; _hangUntil = 0; _hangMsg = false; }
                Recompute();
                var s = Settings.Current;
                int ch = Charm();
                Log.Info(P + "start sesji | klucze: Inn Gossip Needs Drink " + (On ? "tak" : "WYLACZONE") + ", Gossip Per Drink By Charm " + (ByCharm ? "tak" : "WYLACZONE")
                         + ", Gossip Tournaments " + (s != null && s.GossipTournaments ? "tak" : "WYLACZONE") + ", Gossip Matchmaking " + (s != null && s.GossipMatchmaking ? "tak" : "WYLACZONE")
                         + ", Drink Drunk Hangover " + (DrunkOn ? "tak" : "WYLACZONE") + (s != null && !s.NightRestEnabled ? " (NightRestEnabled wylaczone)" : "")
                         + " | krzywa Uroku (plotek na kufel): 20 -> " + PerDrink(20, 99) + ", 80 -> " + PerDrink(80, 99) + ", 150 -> " + PerDrink(150, 99) + ", 200 -> " + PerDrink(200, 99)
                         + ", 250 -> " + (PerDrink(250, 99) >= 99 ? "wszystkie" : PerDrink(250, 99).ToString(Inv)) + "; gracz Urok " + ch + " -> " + (PerDrink(ch, 99) >= 99 ? "wszystkie" : PerDrink(ch, 99).ToString(Inv))
                         + " | prog upicia gracza " + Threshold() + " (baza " + (s != null ? s.DrunkAfterTankards : 3) + ", +1 Wytrzymalosc 5, +1 Atletyka 150), karczmarz nalewa do " + (DrunkOn ? MaxCups().ToString(Inv) : "-")
                         + ", kara pijanego " + DrunkPct(1) + "-" + DrunkPct(CupsPastLimit) + "%, kac predkosc " + (s != null ? s.HangoverSpeedPenaltyPercent : 10) + "% / morale " + (s != null ? s.HangoverMoralePenaltyPercent : 10) + "%"
                         + " | stan z zapisu: " + _importNote + " | potkniecia " + _stumbles + ".");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        // ------------------------------------------------------------ zapis "arm_drink"
        /// <summary>"v1|kufle,pijany,ponad,odpoczynek,koniec_kaca,komunikat|miejsce:skrot.doba,skrot.doba;..." - nic do zapisu (klucz wylaczony i stan pusty) = "".</summary>
        internal static string Export()
        {
            try
            {
                if (!On && _cups == 0 && !_drunk && _hangUntil == 0 && _heard.Count == 0) return "";
                var sb = new StringBuilder("v1|");
                sb.Append(_cups.ToString(Inv)).Append(',').Append(_drunk ? '1' : '0').Append(',').Append(_over.ToString(Inv)).Append(',').Append(_slept.ToString("0.00", Inv))
                  .Append(',').Append(_hangUntil.ToString(Inv)).Append(',').Append(_hangMsg ? '1' : '0').Append('|');
                int n = 0;
                foreach (var kv in _heard)
                {
                    if (kv.Value.Count == 0) continue;
                    if (n++ > 0) sb.Append(';');
                    sb.Append(kv.Key).Append(':');
                    int i = 0;
                    foreach (var e in kv.Value)
                    {
                        if (i++ > 0) sb.Append(',');
                        sb.Append(e.Key.ToString(Inv)).Append('.').Append(e.Value.ToString(Inv));
                    }
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        internal static void Import(string data)
        {
            _cups = _over = 0; _drunk = _hangMsg = false; _slept = 0f; _hangUntil = 0; _heard.Clear();
            if (string.IsNullOrEmpty(data)) { _importNote = "brak klucza (stary zapis albo nowa kampania)"; Recompute(); return; }
            try
            {
                var parts = data.Split('|');
                if (parts.Length < 3 || parts[0] != "v1") { _importNote = "nieznany format - pominiety"; Recompute(); return; }
                var a = parts[1].Split(',');
                int cups, over; float slept; long hang;
                if (a.Length >= 6 && int.TryParse(a[0], NumberStyles.Integer, Inv, out cups) && int.TryParse(a[2], NumberStyles.Integer, Inv, out over)
                    && float.TryParse(a[3], NumberStyles.Float, Inv, out slept) && long.TryParse(a[4], NumberStyles.Integer, Inv, out hang))
                {
                    _cups = Math.Max(0, Math.Min(50, cups)); _drunk = a[1] == "1"; _over = Math.Max(0, Math.Min(20, over)); _slept = Math.Max(0f, Math.Min(48f, slept));
                    _hangUntil = Math.Max(0, hang); _hangMsg = a[5] == "1";   // czas kaca sprawdzany na starcie sesji (CheckTimes - zegar gry juz wczytany)
                }
                int bad = 0, entries = 0;
                foreach (var pl in parts[2].Split(';'))
                {
                    if (pl.Length == 0) continue;
                    int c = pl.IndexOf(':');
                    if (c <= 0) { bad++; continue; }
                    string key = pl.Substring(0, c);
                    if (_heard.Count >= HeardPlaces) break;
                    var d = new Dictionary<uint, int>();
                    foreach (var e in pl.Substring(c + 1).Split(','))
                    {
                        int dot = e.IndexOf('.');
                        uint h; int day;
                        if (dot <= 0 || !uint.TryParse(e.Substring(0, dot), NumberStyles.Integer, Inv, out h) || !int.TryParse(e.Substring(dot + 1), NumberStyles.Integer, Inv, out day)) { bad++; continue; }
                        if (d.Count < HeardPerPlace) { d[h] = day; entries++; }
                    }
                    if (d.Count > 0) _heard[key] = d;
                }
                _importNote = "kufli " + _cups + ", pijany " + (_drunk ? "tak (ponad prog " + _over + ")" : "nie") + ", kac " + (_hangUntil > 0 ? "tak" : "nie")
                              + ", uslyszane: miejsc " + _heard.Count + ", wpisow " + entries + (bad > 0 ? ", zlych wpisow " + bad : "");
            }
            catch (Exception e) { _cups = _over = 0; _drunk = false; _hangUntil = 0; _heard.Clear(); _importNote = "blad odczytu - pominiety"; Stumble("Import", e); }
            Recompute();
        }
    }
}
