using System;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// NOCLEG. Jeff (30.08, nowa zasada): wojsko ma SPAC - baza 6 godzin na dobe
    /// (w obozie, pod dachem, na postoju). Dlug snu eskaluje:
    ///   1 zarwana noc  - predkosc -25%, morale -25%; odespanie 6+3 = 9 h;
    ///   2 zarwane noce - kolumna sie slania (predkosc -40%, morale -40%),
    ///                    odespanie kosztuje 6+9 = 15 h;
    ///   3 zarwane noce - wojsko ZASYPIA gdzie stoi (partia staje, predkosc -90%,
    ///                    morale -95%), odespanie 6+15 = 21 h (wzor odsetek:
    ///                    3*(2*dlug-1) godzin ponad baze).
    /// Dlug spada do ZERA dopiero po przespaniu calej sumy (baza + odsetki);
    /// przespanie samej bazy nie dolicza nowego dlugu, ale stary zostaje.
    /// Do tego szybki oboz: klawisz O na mapie stawia oboz BannerKings od reki,
    /// a w menu obozu jest "Bed down until dawn" - spisz do switu z paskiem.
    /// W sluzbie ROT (SLUZBA) dlugu nie liczymy - o marszach decyduje lord.
    /// Umarli (Undead) dlugu nie znaja wcale.
    /// </summary>
    internal static partial class NightRest   // T10: czesc AI (ksiega snu AI, powody nocnego marszu) w NightMarch.cs
    {
        // dlug snu 0..5 i przespane godziny biezacej nocy
        internal static int Debt;
        // MUSZTRA-j (decyzja Jeffa 09.10 07:10 pkt 2, "noc bez snu = nastepny dzien bez cwiczen", od switu): dlug gracza zaraz po ostatnim swicie
        // (koniec SettleNight) - czyta tylko musztra (DawnDebtOf); predkosc i morale licza dlug biezacy (Debt). Jak AiSleep.DawnDebt w ksiedze AI.
        internal static int DawnDebt;
        private static float _restTonight;
        private static bool _credited;                  // dzisiejszy sen juz rozliczony (od reki, nie o swicie)
        private static Vec2 _lastPos;
        private static bool _hadPos;
        private static CampaignTime _sleepUntil = CampaignTime.Zero;
        private static string _sleepReturn = "camp";
        // sen w menu: WLASNY licznik, niezalezny od _restTonight - swit zeruje
        // _restTonight o 6:00 i pasek snu startowal OD NOWA w srodku nocy
        // (Jeff: "pasek raz i za chwile ponownie"); do tego flaga, zeby swit
        // nie doliczal dlugu komus, kto wlasnie spi
        private static bool _sleeping;
        private static CampaignTime _sleepStart = CampaignTime.Zero;
        // POPUP ZMIERZCHU (Jeff 31.08): 0 = pytaj, 1 = zawsze oboz, 2 = nie pytaj
        internal static int CampPromptMode;
        private static int _promptDay = -1;
        private static float _menuRest;
        private static float _menuTarget = 5f;
        private static float _menuBase;   // stan _restTonight w chwili polozenia sie

        // kary % za dlug 0..3 (Jeff 30.08: juz PIERWSZA zarwana noc boli -25%;
        // morale procentowo przez AddFactor - "spada o 95%", nie o 95 punktow)
        private static readonly int[] SpdPenalty = { 0, 25, 40, 90 };
        private static readonly int[] MorPenalty = { 0, 25, 40, 95 };

        /// <summary>Pora dnia po angielsku - do zegarka snu.</summary>
        private static string DayPart(int h)
        {
            if (h >= 5 && h < 7) return "dawn";
            if (h >= 7 && h < 11) return "morning";
            if (h >= 11 && h < 14) return "midday";
            if (h >= 14 && h < 18) return "afternoon";
            if (h >= 18 && h < 21) return "dusk";
            return "night";
        }

        /// <summary>Ile godzin snu zamyka rachunek przy biezacym dlugu:
        /// baza + 3*(2*dlug-1) odsetek (dlug 1: +3h, dlug 2: +9h, dlug 3: +15h).</summary>
        internal static float NeededHours()
        {
            var s = Settings.Current;
            float baza = s != null ? Math.Max(1f, s.SleepHoursNeeded) : 6f;
            return baza + (Debt > 0 ? 3f * (2 * Debt - 1) : 0f);
        }

        // ------------------------------------------------------------ rachunek nocy
        internal static void OnHourly()
        {
            try
            {
                var s = Settings.Current;
                MasterSwitch(s);   // T10 poprawka recenzji: przelacznik glowny wylaczony w trakcie gry - jednorazowo wszystko na stare
                if (s == null || !s.NightRestEnabled) return;
                // T10 (uwaga krytyki 9, A07 3.10): swiat AI nie zalezy od stanu gracza - ksiega snu AI, oboz splaty
                // dlugu, oboz swiata i bandy biegna PRZED wyjsciami gracza (martwy gracz, gracz-Nieumarly);
                // ponizsze wyjscia dotycza juz tylko ksiegi gracza
                try { AiHourly(s, CampaignTime.Now.GetHourOfDay); } catch (Exception e) { Log.Error("NightRest.AiHourly", e); }
                var mp = MobileParty.MainParty;
                if (mp == null || Hero.MainHero == null || !Hero.MainHero.IsAlive) return;
                // umarli nie spia: armia Innych nie zna dlugu snu
                if (Undead.Party(mp) || Undead.Character(Hero.MainHero.CharacterObject))
                { Debt = 0; DawnDebt = 0; _restTonight = 0f; return; }

                var pos = mp.GetPosition2D;
                // grupa11: krok godziny i prog "ruszyl sie" wspolne z musztra i ksiega AI (Drill.RestStep, Drill.RestHour);
                // bez poprzedniego odczytu krok 0 - godzina postoju jak dotad
                float step = _hadPos ? pos.Distance(_lastPos) : 0f;
                bool moved = step > Drill.RestStep;
                _lastPos = pos; _hadPos = true;
                if (moved && PlayerCamped)
                {
                    // ruszyl sie = oboz zwinal (takze oboz BK); namiot NIE moze
                    // jechac po mapie (Jeff to widzial) - schodzi wizerunek, nie tylko flaga
                    Tent(mp, false);
                    PlayerCamped = false;
                }

                int h = CampaignTime.Now.GetHourOfDay;
                // T1: tick o godzinie h liczy godzine, ktora wlasnie minela - tick
                // konca obozu (6:00) liczy godzine 5-6 jako noc (inaczej oboz 0-6
                // dawal 5.6 h i dlug); sufit 12, zeby zle ustawienie nie robilo nocy z calej doby
                // poprawka recenzji: rowne godziny (brak obozu swiata) nie przesuwaja swita gracza - wtedy stary swit 6:00
                int dawn = PlayerDawn;
                bool night = h >= 21 || h <= Math.Min(dawn, 12);
                // grupa11: czesc wspolna z musztra (osada, oboz obleznikow, krok <= 0.35 jedn.) - jedna funkcja Drill.RestHour;
                // morze i sluzba ROT to zasady snu (nie postoju) - dochodza tylko tutaj
                bool resting = Drill.RestHour(mp, step)
                               || (s.SleepAtSeaFree && mp.IsCurrentlyAtSea)
                               || RotEnlisted();
                // spac mozna O KAZDEJ porze - noc liczy sie w calosci, dzien slabiej
                // (gwar obozu, upal, swiatlo); rachunek zamyka sie o swicie
                if (resting) _restTonight += night ? 1f : MBMath.ClampFloat(s.DayRestFactor, 0.1f, 1f);

                // NAMIOT SAM SIE STAWIA (Jeff 31.08: "jak spie, to czasami sie
                // nie uruchamia") - nocny postoj w polu to oboz, takze bez menu
                if (InCamp(h) && resting && !PlayerCamped && s.CampTentIcon
                    && mp.CurrentSettlement == null && mp.MapEvent == null
                    && mp.AttachedTo == null && !mp.IsCurrentlyAtSea)
                {
                    Tent(mp, true);
                    PlayerCamped = true;
                }

                // ROZLICZENIE OD REKI (Jeff: "sen ma byc aktualizowany zaraz po
                // wypoczynku, nie czekac do przeliczenia") - gdy tylko godziny
                // snu sie uzbieraja, dlug schodzi natychmiast; swit juz tego
                // nie liczy drugi raz
                CreditRest(s);

                // ZMIERZCH (Jeff 31.08: "jade i zapada noc - popup czy rozbijamy
                // oboz, z auto-obozem albo wylaczeniem"). Pytamy TYLKO kolumne
                // w ruchu na czystej mapie; wybor trybu zyje w save.
                // JENIEC NIE DOWODZI (Jeff 01.09: "jako jeniec decydowalem
                // o noclegu bandytow - to nie moja sprawa") - w niewoli zero
                // pytan o oboz i zero auto-obozu
                if (h == CampStart && CampStart != CampEnd && moved && _promptDay != (int)CampaignTime.Now.ToDays
                    && s.NightfallPromptEnabled && CampPromptMode != 2 && !_sleeping && !PlayerCamped
                    && !TaleWorlds.CampaignSystem.PlayerCaptivity.IsCaptive
                    && mp.CurrentSettlement == null && mp.MapEvent == null
                    && mp.AttachedTo == null && !mp.IsCurrentlyAtSea
                    && PlayerEncounter.Current == null && !RotEnlisted())
                {
                    _promptDay = (int)CampaignTime.Now.ToDays;
                    if (CampPromptMode == 1)
                    {
                        Msg("Night falls - the column makes camp.", Colors.White);
                        MakeCampNow();
                    }
                    else NightfallAsk();
                }

                if (h == dawn)
                {
                    // T10 (uwaga krytyki 5, P10): linia ksiegi gracza o swicie - sam log, SettleNight bez zmian
                    int debtBefore = Debt; float restBefore = _restTonight; bool sleepingNow = _sleeping, creditedBefore = _credited;
                    SettleNight(s);
                    LogPlayerDawn(s, restBefore, debtBefore, sleepingNow, creditedBefore);
                }
                // T10: AiNightCamp i AiBanditRest przeniesione do AiHourly (przed wyjscia gracza)
            }
            catch (Exception e) { Log.Error("NightRest.OnHourly", e); }
        }

        /// <summary>Splata dlugu od reki - dopiero gdy uzbiera sie CALA suma
        /// (baza + odsetki wg NeededHours); wtedy dlug schodzi do zera.</summary>
        private static void CreditRest(Settings s)
        {
            if (_credited || _restTonight < NeededHours()) return;
            _credited = true;
            if (Debt > 0)
            {
                Debt = 0;   // odespali baze i wszystkie odsetki naraz
                Msg("The debt is paid in full - the men wake fresh again.", Colors.Green);
            }
        }

        // ------------------------------------------------------------ swiat tez spi
        private static readonly System.Collections.Generic.List<MobileParty> _tented =
            new System.Collections.Generic.List<MobileParty>();
        // wszyscy, ktorzy TEJ nocy poszli spac (z namiotem czy bez) - z tej listy
        // czesty refresh dobiera namioty wokol gracza
        private static readonly System.Collections.Generic.List<MobileParty> _camping =
            new System.Collections.Generic.List<MobileParty>();
        private static DateTime _lastTentRefresh = DateTime.MinValue;

        // ---- T1 (noc 08/09.10): JEDNA NOC OBOZU dla calego swiata. Jeff: "jak armia
        // idzie, to trzeba rozbic oboz - daj godziny od 24 do 6 rano". Godziny z MCM
        // (CampStartHour / CampEndHour, domyslnie 0 i 6), rowne godziny = brak obozu
        // (bez tego 0/0 uspiloby swiat na 24 h w trwajacej kampanii).
        internal static int CampStart { get { var s = Settings.Current; return Mod24(s != null ? s.CampStartHour : 0); } }
        internal static int CampEnd { get { var s = Settings.Current; return Mod24(s != null ? s.CampEndHour : 6); } }
        private static int Mod24(int h) { return ((h % 24) + 24) % 24; }
        /// <summary>Swit rachunku snu gracza: koniec obozu, a przy rownych godzinach (swiat nie obozuje) stary swit 6:00.</summary>
        internal static int PlayerDawn { get { int st = CampStart, e = CampEnd; return st == e ? 6 : e; } }

        /// <summary>Czy tick o godzinie h jest godzina obozu (s..e-1, takze przez polnoc; s == e = nigdy).</summary>
        internal static bool InCamp(int h)
        {
            int st = CampStart, e = CampEnd, x = Mod24(h);
            if (st == e) return false;
            return st < e ? (x >= st && x < e) : (x >= st || x < e);
        }

        // straznik snu i zdejmowanie namiotow chodza na zegarze GRY (co 0.1 h), nie
        // na sekundach realnych - przy x8 sekunda to ok. 2 h gry i obudzony jechal
        // z namiotem do nastepnego przegladu (audyt 07: sr. 1.29, maks. 5.69 jedn.)
        private const double GuardStepHours = 0.1;
        private static CampaignTime _lastTentDrop = CampaignTime.Zero;
        // pozycja przy postawieniu namiotu AI - kto odjechal > 0.3 jedn., traci namiot
        private static readonly System.Collections.Generic.Dictionary<MobileParty, Vec2> _tentPos =
            new System.Collections.Generic.Dictionary<MobileParty, Vec2>();
        // liczniki godziny (log w linii "AiNightCamp"): obudzenia wedlug rodzaju i zjazd
        private static float _wokenDriftSum, _wokenDriftMax;
        private static int _wokenHold, _wokenOurOrder, _wokenOther;
        private static int _tentDropsHour; private static float _tentDropMax;
        // stoper: czas HoldSleepers i DropMovedTents w ms na godzine gry
        private static int _holdCalls, _dropCalls; private static double _holdMsSum, _holdMsMax, _dropMsSum, _dropMsMax;
        // poprawki recenzji: faktyczny odstep przegladow w h gry (zjazd zalezy od niego, nie tylko od kroku 0.1),
        // potkniecia per partia (dlawiony log) i linia ustawien obozu przy kazdej zmianie
        private static double _holdDhMax, _dropDhMax;
        private static int _guardStumbles;
        private static int _cfgSig = -1;

        /// <summary>Nowa gra albo wczytanie (konstruktor ArmouryBehavior): stan nocy jednej
        /// kampanii nie przecieka do drugiej; zegary od zera = straznik rusza od reki.</summary>
        internal static void ResetWorld()
        {
            try
            {
                _camping.Clear(); _tented.Clear(); _bedPos.Clear(); _stillPos.Clear(); _tentPos.Clear(); _orders.Clear();
                _lastHoldSweep = CampaignTime.Zero; _lastTentDrop = CampaignTime.Zero; _lastTentRefresh = DateTime.MinValue;
                _cfgSig = -1;
                // T10 poprawka recenzji: pozycja gracza z poprzedniej kampanii nie liczy sie jako ruch - pierwsza godzina po wczytaniu
                // to postoj, tak samo dla gracza i dla partii AI (ksiega AI: brak poprzedniego odczytu = postoj)
                _hadPos = false;
                ResetHourCounters();
                ResetAi();   // T10: ksiega snu AI, snu dluznikow, alarmy, wstrzymani w osadach
            }
            catch { }
        }

        private static void ResetHourCounters()
        {
            _wokenHour = 0; _wokenDriftSum = 0f; _wokenDriftMax = 0f; _wokenHold = 0; _wokenOurOrder = 0; _wokenOther = 0;
            _tentDropsHour = 0; _tentDropMax = 0f;
            _holdCalls = 0; _dropCalls = 0; _holdMsSum = 0; _holdMsMax = 0; _dropMsSum = 0; _dropMsMax = 0;
            _holdDhMax = 0; _dropDhMax = 0;
        }

        /// <summary>Potkniecie na jednej partii w straznikach: liczymy, log pierwsze 3 i co setne (bez gaszenia straznika).</summary>
        private static void GuardStumble(string where, MobileParty mp, Exception e)
        {
            _guardStumbles++;
            if (_guardStumbles <= 3 || _guardStumbles % 100 == 0)
            {
                string who = "?";
                try { who = mp != null ? mp.StringId : "null"; } catch { }
                Log.Error(where + " (partia " + who + ", potkniecie " + _guardStumbles + " w sesji)", e);
            }
        }

        /// <summary>Linia ustawien obozu swiata - przy pierwszym ticku i przy kazdej zmianie (MCM / Armoury.json).</summary>
        private static void LogCampConfig(Settings s)
        {
            int st = CampStart, e = CampEnd;
            int sig = st * 10000 + e * 100 + (s.AiCampsAtNight ? 10 : 0) + (s.ArmyLeadersAlwaysCamp ? 1 : 0)
                      + Math.Max(0, Math.Min(95, s.AiCampSkipPercent)) * 1000000;
            // T10: nowe przelaczniki i progi w tym samym podpisie (zmiana w MCM = nowa linia)
            sig = sig * 31 + (s.AiNightMarchByReason ? 1 : 0) * 8 + (s.AiSleepDebt ? 1 : 0) * 4 + (s.AiNightReliefWider ? 1 : 0) * 2 + (DryBuild ? 1 : 0);
            sig = sig * 31 + (int)Math.Round(s.AiNightsAwakeInChase * 100f) * 7 + (int)Math.Round(s.AiCampDangerRadius * 10f);
            if (sig == _cfgSig) return;
            _cfgSig = sig;
            Log.Info("NightRest: oboz swiata " + (st == e ? "WYLACZONY (rowne godziny " + st + "/" + e + ")" : st + ":00-" + e + ":00")
                     + " (AiCampsAtNight=" + s.AiCampsAtNight + ", ArmyLeadersAlwaysCamp=" + s.ArmyLeadersAlwaysCamp
                     + ", AiCampSkipPercent=" + s.AiCampSkipPercent + ", swit gracza " + PlayerDawn + ":00, krok straznika "
                     + GuardStepHours.ToString("0.00", CultureInfo.InvariantCulture) + " h gry)."
                     + " T10: AiNightMarchByReason=" + s.AiNightMarchByReason + ", AiSleepDebt=" + s.AiSleepDebt
                     + ", AiNightReliefWider=" + s.AiNightReliefWider + ", AiNightsAwakeInChase="
                     + s.AiNightsAwakeInChase.ToString("0.##", CultureInfo.InvariantCulture) + ", promien alarmu "
                     + s.AiCampDangerRadius.ToString("0.#", CultureInfo.InvariantCulture) + " jedn., poscig do " + CampLen()
                     + " h marszu, odsiecz do " + (2 * CampLen()) + " h, oboz splaty dlugu 1 od " + DebtCampHour + ":00"
                     + (DryBuild ? " - DLL NA SUCHO (T10_DRY): tylko log, zachowanie i kary jak w T1" : "")
                     + (!DryBuild && !s.AiNightMarchByReason ? " - powody nocnego marszu tylko w logu (na sucho)" : "")
                     + (!DryBuild && !s.AiSleepDebt ? " - ksiega snu AI tylko w logu (na sucho)" : "") + ".");
        }

        private static string HourCountersText()
        {
            var ci = CultureInfo.InvariantCulture;
            return " Zjazd obudzonych: sr. " + (_wokenHour > 0 ? _wokenDriftSum / _wokenHour : 0f).ToString("0.00", ci)
                   + ", maks. " + _wokenDriftMax.ToString("0.00", ci)
                   + "; rodzaj: Hold bez rozkazu " + _wokenHold + ", rozkaz jak nasz zapamietany " + _wokenOurOrder
                   + ", inny rozkaz " + _wokenOther
                   + ". Namioty zdjete po zjezdzie: " + _tentDropsHour + " (maks. zjazd " + _tentDropMax.ToString("0.00", ci) + ")."
                   + " Stoper: HoldSleepers " + _holdCalls + " x, sr. " + (_holdCalls > 0 ? _holdMsSum / _holdCalls : 0).ToString("0.000", ci)
                   + " ms, maks. " + _holdMsMax.ToString("0.000", ci) + " ms; DropMovedTents " + _dropCalls + " x, sr. "
                   + (_dropCalls > 0 ? _dropMsSum / _dropCalls : 0).ToString("0.000", ci) + " ms, maks. " + _dropMsMax.ToString("0.000", ci) + " ms."
                   + " Odstep przegladow maks.: HoldSleepers " + _holdDhMax.ToString("0.000", ci) + " h, DropMovedTents "
                   + _dropDhMax.ToString("0.000", ci) + " h gry. Potkniecia straznikow w sesji: " + _guardStumbles + ".";
        }

        private static double MsSince(long t0)
        {
            return (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }

        /// <summary>Zegar gry: true, gdy od ostatniego przegladu minelo >= 0.1 h gry
        /// albo roznica jest ujemna (wczytany wczesniejszy zapis / nowa kampania).</summary>
        private static bool GuardDue(ref CampaignTime last, ref double dhMax)
        {
            var now = CampaignTime.Now;
            double dh = (now - last).ToHours;
            if (dh >= GuardStepHours || dh < 0)
            {
                // odstep > 1 h = pierwszy przeglad nocy / po wczytaniu - nie jest odstepem straznika
                if (dh <= 1.0 && dh > dhMax) dhMax = dh;
                last = now; return true;
            }
            return false;
        }

        /// <summary>
        /// T1: namioty AI z kolumn, ktore ruszyly - co 0.1 h GRY (koniec "jadacego
        /// namiotu"). Kazda partia z _tented, ktora odjechala > 0.3 jedn. od miejsca
        /// postawienia namiotu, traci namiot (odswiezenie namiotow postawi go od nowa,
        /// jesli straznik polozy ja spac w nowym miejscu). Lista _tented <= AiTentCap.
        /// </summary>
        private static void DropMovedTents()
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                for (int i = _tented.Count - 1; i >= 0; i--)
                {
                    var t = _tented[i];
                    try
                    {
                        if (t == null || t == MobileParty.MainParty) continue;
                        Vec2 at;
                        if (!_tentPos.TryGetValue(t, out at)) continue;
                        float moved = t.GetPosition2D.Distance(at);
                        if (moved <= 0.3f) continue;
                        Tent(t, false);
                        _tented.RemoveAt(i);
                        _tentDropsHour++;
                        if (moved > _tentDropMax) _tentDropMax = moved;
                    }
                    catch (Exception e)
                    {
                        // jedna partia nie urywa przegladu; zepsuta wypada z listy, zeby nie potykac sie co 0.1 h
                        GuardStumble("DropMovedTents", t, e);
                        try { if (i < _tented.Count && _tented[i] == t) { _tented.RemoveAt(i); if (t != null) Tent(t, false); } } catch { }
                    }
                }
            }
            catch (Exception e) { Log.Error("DropMovedTents", e); }
            double ms = MsSince(t0);
            _dropCalls++; _dropMsSum += ms; if (ms > _dropMsMax) _dropMsMax = ms;
        }
        // ---- koniec T1 (pola i pomocnicze)

        // TWARDY SEN (Jeff 20.09: "AI i bandy nie spia, namiot sie porusza").
        // Ai.DisableForHours() gasi tylko WLASNE myslenie partii - nie blokuje
        // ruchu do celu, ktory ktos nada z zewnatrz. StrategicCampaignAI co
        // 4 godziny (takze 0:00 i 4:00) wola SetMove* na wodzach armii, gdy
        // TargetSettlement != cel - a po naszym Hold jest null, wiec rozkaz
        // pada ZAWSZE i kolumna jedzie cala noc z namiotem na plecach.
        // AIInfluence (zaciemniony) tez wola SetMove*. Dlatego: pozycja z chwili
        // polozenia sie + straznik co sekunde, ktory kladzie z powrotem kazdego,
        // kto dostal rozkaz albo sie przesunal, i liczy, kto go budzil.
        private static readonly System.Collections.Generic.Dictionary<MobileParty, Vec2> _bedPos =
            new System.Collections.Generic.Dictionary<MobileParty, Vec2>();
        private static CampaignTime _lastHoldSweep = CampaignTime.Zero;   // T1: zegar GRY, nie realny
        private static int _wokenHour;      // obudzeni cudza reka od ostatniego taktu godzinowego
        private static int _wokenSession;   // razem w sesji (do dlawienia logu)

        /// <summary>Straznik snu: kazdy spiacy, ktory ma cudzy rozkaz albo
        /// zjechal z legowiska, wraca na Hold. Wolane z OnTick co 0.1 h gry (T1).</summary>
        private static void HoldSleepers()
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                for (int i = _camping.Count - 1; i >= 0; i--)
                {
                    var mp = _camping[i];
                    try
                    {
                        if (mp == null || !mp.IsActive) { _camping.RemoveAt(i); continue; }
                        if (mp.MapEvent != null || mp.CurrentSettlement != null) continue;   // bitwa/osada - nie nasza sprawa
                        Vec2 bed;
                        if (!_bedPos.TryGetValue(mp, out bed)) { bed = mp.GetPosition2D; _bedPos[mp] = bed; }
                        float drift = mp.GetPosition2D.Distance(bed);
                        bool ordered = mp.DefaultBehavior != TaleWorlds.CampaignSystem.Party.AiBehavior.Hold
                                       || mp.TargetSettlement != null || mp.TargetParty != null;
                        if (!ordered && drift <= 0.3f) continue;

                        string what = mp.DefaultBehavior.ToString()
                            + (mp.TargetSettlement != null ? " -> " + mp.TargetSettlement.Name : "")
                            + (mp.TargetParty != null ? " -> " + mp.TargetParty.Name : "");
                        // T1: rodzaj obudzenia (przed SetMoveModeHold) - sam zjazd na Hold,
                        // rozkaz taki jak nasz zapamietany przed snem, albo inny cudzy rozkaz
                        NightOrder mine;
                        if (!ordered) _wokenHold++;
                        else if (_orders.TryGetValue(mp, out mine) && mine != null && mine.Behavior == mp.DefaultBehavior
                                 && mine.Settlement == mp.TargetSettlement && mine.Party == mp.TargetParty) _wokenOurOrder++;
                        else _wokenOther++;
                        _wokenDriftSum += drift; if (drift > _wokenDriftMax) _wokenDriftMax = drift;
                        mp.Ai.DisableForHours(1);
                        mp.SetMoveModeHold();
                        _bedPos[mp] = mp.GetPosition2D;   // spi tam, gdzie go zlapalismy
                        _wokenHour++; _wokenSession++;
                        // pierwsze trzy w calosci, potem co dwudziesty - liczniki biegna zawsze
                        if (_wokenSession <= 3 || _wokenSession % 20 == 0)
                            Log.Info("AiNightCamp: obudzony cudza reka " + mp.Name + " (rozkaz " + what
                                     + ", zjechal " + drift.ToString("0.00", CultureInfo.InvariantCulture)
                                     + (mp.Army != null ? ", wodz armii" : "")
                                     + ") - klade z powrotem. Razem w sesji: " + _wokenSession + ".");
                    }
                    catch (Exception e)
                    {
                        // jedna partia nie urywa przegladu; zepsuta wypada z listy (noc odnowi ja o pelnej godzinie)
                        GuardStumble("HoldSleepers", mp, e);
                        try { if (i < _camping.Count && _camping[i] == mp) _camping.RemoveAt(i); } catch { }
                    }
                }
            }
            catch (Exception e) { Log.Error("HoldSleepers", e); }
            double ms = MsSince(t0);
            _holdCalls++; _holdMsSum += ms; if (ms > _holdMsMax) _holdMsMax = ms;
        }

        /// <summary>
        /// NAMIOTY WOKOL GRACZA, ODSWIEZANE CZESTO (Jeff: "mijam obozy, a stoi
        /// konik - ma byc namiot"). Stary przydzial szedl raz na godzine GRY,
        /// wiec podjezdzajac do spiacego obozu w pol godziny widziales figurke.
        /// Teraz: co ~2 sekundy REALNE zdejmujemy namioty poza zasiegiem
        /// i obudzonym, a spiacym w zasiegu stawiamy - do limitu AiTentCap.
        /// Wizerunki ruszamy TYLKO przy zmianie stanu (pulapka z CLAUDE.md).
        /// </summary>
        private static void RefreshNearbyTents(Settings s)
        {
            try
            {
                if (!s.CampTentIcon || MobileParty.MainParty == null) return;
                var me = MobileParty.MainParty.GetPosition2D;
                float radius = MathF.Max(5f, s.AiTentRadius);
                int hh = CampaignTime.Now.GetHourOfDay;
                bool nightNow = InCamp(hh);   // T1: jedna noc obozu
                for (int i = _tented.Count - 1; i >= 0; i--)
                {
                    var t = _tented[i];
                    bool asleep = t != null && t.Ai != null && t.Ai.IsDisabled;
                    bool still = t != null && nightNow && IsStill(t);
                    bool drop = t == null || !t.IsActive || t.MapEvent != null
                                || (!asleep && !still)
                                || t.GetPosition2D.Distance(me) > radius + 3f;
                    if (drop) { Tent(t, false); _tented.RemoveAt(i); }
                }
                foreach (var mp in _camping)
                {
                    if (_tented.Count >= Math.Max(0, s.AiTentCap)) break;
                    if (mp == null || !mp.IsActive || mp.MapEvent != null) continue;
                    if (mp.Ai == null || !mp.Ai.IsDisabled) continue;      // obudzony = zadnego namiotu
                    if (_tented.Contains(mp)) continue;
                    if (mp.GetPosition2D.Distance(me) > radius) continue;
                    Tent(mp, true);
                    _tented.Add(mp);
                }
                // NOCA kazda STOJACA kolumna w zasiegu wyglada jak oboz (Jeff
                // 31.08: "inni stoja konik lub piechur jakby nic; promien ~100")
                // - takze te, ktore zatrzymalo vanilla AI, nie nasz nocleg
                if (nightNow)
                {
                    foreach (var mp in MobileParty.All)
                    {
                        if (_tented.Count >= Math.Max(0, s.AiTentCap)) break;
                        if (mp == null || !mp.IsActive || mp == MobileParty.MainParty) continue;
                        if (!mp.IsLordParty && !mp.IsCaravan) continue;
                        if (mp.CurrentSettlement != null || mp.MapEvent != null || mp.BesiegerCamp != null) continue;
                        if (mp.AttachedTo != null || mp.IsCurrentlyAtSea) continue;
                        if (_tented.Contains(mp)) continue;
                        if (mp.GetPosition2D.Distance(me) > radius) continue;
                        if (!IsStill(mp)) continue;
                        if (Undead.Party(mp)) continue;                    // umarli nie obozuja
                        Tent(mp, true);
                        _tented.Add(mp);
                    }
                }
                PruneStill(me, radius);
            }
            catch (Exception e) { Log.Error("RefreshNearbyTents", e); }
        }

        // czy partia stoi w miejscu: porownanie z pozycja z poprzedniego
        // odswiezenia namiotow (co ~2 s realne) - zadnych dodatkowych tickow.
        // T1 (uwaga S3): prog liczony od uplywu czasu GRY od pomiaru - "stoi" =
        // przesuniecie < 0.1 jedn. na godzine gry (najmniej 0.01 jedn.); przy x8
        // 2 s realne to kilka godzin gry i staly prog 0.08 przepuszczal wolne kolumny
        // poprawka recenzji: pomiar blizej niz 0.05 h gry od poprzedniego (pauza, dwa odswiezenia
        // w jednej klatce) nie jest pomiarem - zwraca poprzedni werdykt i nie nadpisuje bazy
        private const double StillMinHours = 0.05;
        private struct StillMark { public Vec2 Pos; public CampaignTime At; public bool Still; }
        private static readonly System.Collections.Generic.Dictionary<MobileParty, StillMark> _stillPos =
            new System.Collections.Generic.Dictionary<MobileParty, StillMark>();

        private static bool IsStill(MobileParty mp)
        {
            try
            {
                var pos = mp.GetPosition2D;
                var now = CampaignTime.Now;
                StillMark prev;
                bool known = _stillPos.TryGetValue(mp, out prev);
                double dh = known ? (now - prev.At).ToHours : -1;
                if (known && dh >= 0 && dh < StillMinHours) return prev.Still;   // za krotko - poprzedni werdykt
                bool still = false;                                // pierwszy pomiar, wczytanie albo stary pomiar - nie stoi
                if (known && dh >= 0 && dh <= 6)
                    still = prev.Pos.Distance(pos) < Math.Max(0.01f, 0.1f * (float)dh);
                _stillPos[mp] = new StillMark { Pos = pos, At = now, Still = still };
                return still;
            }
            catch { return false; }
        }

        private static void PruneStill(Vec2 me, float radius)
        {
            try
            {
                if (_stillPos.Count < 400) return;
                var drop = new System.Collections.Generic.List<MobileParty>();
                foreach (var kv in _stillPos)
                    if (kv.Key == null || !kv.Key.IsActive || kv.Key.GetPosition2D.Distance(me) > radius * 2f)
                        drop.Add(kv.Key);
                foreach (var k in drop) _stillPos.Remove(k);
            }
            catch { }
        }

        /// <summary>
        /// ROZKAZ ZAPAMIETANY NA NOC. Stare obozowanie wolalo SetMoveModeHold(),
        /// ktore KASUJE rozkaz lorda - o swicie AI zaczynalo myslec od zera,
        /// a przy dwoch podobnie kuszacych celach chodzilo tam i z powrotem
        /// (Jeff w armii: "bezsensowne lazenie"). Teraz rozkaz zapisujemy przed
        /// snem i oddajemy go o switku - lord budzi sie z tym, po co wyszedl.
        /// </summary>
        private sealed class NightOrder
        {
            public TaleWorlds.CampaignSystem.Party.AiBehavior Behavior;
            public TaleWorlds.CampaignSystem.Settlements.Settlement Settlement;
            public MobileParty Party;
        }

        private static readonly System.Collections.Generic.Dictionary<MobileParty, NightOrder> _orders =
            new System.Collections.Generic.Dictionary<MobileParty, NightOrder>();

        private static void RememberOrder(MobileParty mp)
        {
            try
            {
                if (mp == null || _orders.ContainsKey(mp)) return;
                _orders[mp] = new NightOrder
                {
                    Behavior = mp.DefaultBehavior,
                    Settlement = mp.TargetSettlement,
                    Party = mp.TargetParty
                };
            }
            catch { }
        }

        private static void GiveOrderBack(MobileParty mp)
        {
            try
            {
                NightOrder o;
                if (mp == null || !_orders.TryGetValue(mp, out o)) return;
                _orders.Remove(mp);
                ApplyOrder(mp, o, false);
            }
            catch { }
        }

        /// <summary>Oddaje zapamietany rozkaz (T10: wspolne dla obozu swiata i snu dluznikow; inSettlement = takze partii
        /// w osadzie - dluznik zwolniony w osadzie, gdy rozkaz zapamietano w polu).</summary>
        private static void ApplyOrder(MobileParty mp, NightOrder o, bool inSettlement)
        {
            try
            {
                if (mp == null || o == null) return;
                if (!mp.IsActive || mp.MapEvent != null || (mp.CurrentSettlement != null && !inSettlement)) return;
                var nav = MobileParty.NavigationType.Default;
                var st = o.Settlement; var tp = o.Party;
                // T10 poprawka recenzji: rozkaz sprzed snu (do ok. 29 h przy snie dlugu) moze byc juz niewazny - pokoj, osada przeszla na nasza
                // strone, cel juz nie wrog. Gra nie sprawdza wojny przy wejsciu w oblezenie (EncounterManager.StartSettlementEncounter), wiec
                // niewazny rozkaz nie wraca - AI decyduje od nowa
                var mf = mp.MapFaction;
                bool stWar = st != null && mf != null && st.MapFaction != null && st.MapFaction != mf && mf.IsAtWarWith(st.MapFaction);
                switch (o.Behavior)
                {
                    case TaleWorlds.CampaignSystem.Party.AiBehavior.GoToSettlement:
                        if (st != null) mp.SetMoveGoToSettlement(st, nav, false); break;
                    case TaleWorlds.CampaignSystem.Party.AiBehavior.BesiegeSettlement:
                        if (st != null && stWar) mp.SetMoveBesiegeSettlement(st, nav); break;
                    case TaleWorlds.CampaignSystem.Party.AiBehavior.RaidSettlement:
                        if (st != null && stWar) mp.SetMoveRaidSettlement(st, nav, false); break;
                    case TaleWorlds.CampaignSystem.Party.AiBehavior.DefendSettlement:
                        if (st != null && st.MapFaction == mf) mp.SetMoveDefendSettlement(st, false, nav); break;
                    case TaleWorlds.CampaignSystem.Party.AiBehavior.PatrolAroundPoint:
                        if (st != null) mp.SetMovePatrolAroundSettlement(st, nav, false); break;
                    case TaleWorlds.CampaignSystem.Party.AiBehavior.EngageParty:
                        if (tp != null && tp.IsActive && Hostile(mp, tp)) mp.SetMoveEngageParty(tp, nav); break;
                    case TaleWorlds.CampaignSystem.Party.AiBehavior.EscortParty:
                        if (tp != null && tp.IsActive) mp.SetMoveEscortParty(tp, nav, false); break;
                    case TaleWorlds.CampaignSystem.Party.AiBehavior.GoAroundParty:
                        if (tp != null && tp.IsActive) mp.SetMoveGoAroundParty(tp, nav); break;
                    default: break;   // Hold/None i reszta - niech AI zdecyduje na swiezo
                }
            }
            catch (Exception e) { AiStumble("ApplyOrder", mp, e); }   // T10 poprawka recenzji: liczone, nie polykane
        }

        /// <summary>
        /// CALY SWIAT OBOZUJE. Godzina obozu (T1: CampStartHour-CampEndHour, domyslnie 0-6): partie lordow i karawany
        /// staja na nocleg (AI upione na godzine, wznawiane co tick nocy),
        /// z namiotem na mapie. NIE staja: scigani i uciekajacy (wrog w poblizu
        /// lub rozkaz ucieczki), armie w akcji (bitwa/oblezenie), zeglujacy,
        /// bandyci (nocni lowcy) i umarli - Inni nie znaja snu. O swicie
        /// namioty znikaja i AI budzi sie samo.
        /// </summary>
        private static void AiNightCamp(Settings s, int h)
        {
            try
            {
                try { LogCampConfig(s); } catch { }   // poprawka recenzji: jakie godziny naprawde obowiazuja
                bool night = InCamp(h);   // T1: ticki 0..5 -> spia 0:00-6:00, o 6:00 pobudka
                if (!s.AiCampsAtNight || !night)
                {
                    // T1: obudzenia z ostatniej godziny obozu (np. 5-6) - osobna linia, nie "AiNightCamp";
                    // poprawka recenzji: takze gdy byly same przeglady (stoper ostatniej godziny w logu)
                    if (_wokenHour > 0 || _tentDropsHour > 0 || _holdCalls > 0 || _dropCalls > 0)
                        Log.Info("NightRest swit " + h + ":00: obudzonych cudza reka od poprzedniej godziny: " + _wokenHour + "." + HourCountersText());
                    if (_wokenHour > 0 || _tentDropsHour > 0 || _holdCalls > 0 || _dropCalls > 0) ResetHourCounters();
                    _tentPos.Clear();
                    if (_tented.Count > 0)
                    {
                        foreach (var mp in _tented) Tent(mp, false);
                        _tented.Clear();
                    }
                    _camping.Clear();
                    _bedPos.Clear();
                    // SWIT: kazdy uspiony lord dostaje z powrotem swoj rozkaz
                    // (T10: snu dluznikow ta galaz nie rusza - osobny slownik _debtSleep; lordowie wstrzymani w osadzie maja
                    // tylko AI wylaczone na godzine - rozkaz nietkniety, wiec nie ma czego oddawac)
                    if (_orders.Count > 0)
                    {
                        var wake = new System.Collections.Generic.List<MobileParty>(_orders.Keys);
                        foreach (var mp in wake) GiveOrderBack(mp);
                        _orders.Clear();
                    }
                    _townHold.Clear();
                    return;
                }

                // zagrozenia: partie lordow i bandytow (pogon nie zna pory snu) - stara sciezka T1 (karawany, bandy,
                // lordowie przy wylaczonym AiNightMarchByReason); T10 dla lordow szuka zagrozen przez lokator mapy (AlarmThreat)
                var threats = new System.Collections.Generic.List<MobileParty>();
                foreach (var mp in MobileParty.All)
                {
                    if (mp == null || !mp.IsActive) continue;
                    if (mp.IsLordParty || mp.IsBandit) threats.Add(mp);
                }

                // T10: kto spal w chwili ticku (migawka - _camping zmienia sie w petli), tryb i liczniki godziny
                var campSet = new System.Collections.Generic.HashSet<MobileParty>(_camping);
                bool byReason = ByReason(s);
                bool debtOn = DebtOn(s);
                var tally = new NightTally();
                foreach (var mp in MobileParty.All)
                {
                    if (mp == null || !mp.IsActive || mp == MobileParty.MainParty) continue;
                    // bandyci obozuja TYLKO za przelacznikiem (Ai Bandits Camp Too,
                    // domyslnie OFF) - to wlaczenie ich hurtem polozylo gre 25.08,
                    // wiec wraca ostroznie i bez namiotow ponad limit
                    if (!mp.IsLordParty && !mp.IsCaravan && !(s.AiBanditsCampToo && mp.IsBandit)) continue;
                    AiSleep e = null;
                    if (mp.IsLordParty) _ai.TryGetValue(mp, out e);
                    // T10: lord w osadzie w godzinie obozu bez powodu nie wyjezdza (spi pod dachem - jak gracz, ktory nie rusza noca)
                    if (mp.IsLordParty && byReason && mp.CurrentSettlement != null && mp.MapEvent == null)
                    {
                        TownNight(mp, s, e, debtOn, campSet, tally);
                        continue;
                    }
                    if (mp.CurrentSettlement != null || mp.MapEvent != null || mp.BesiegerCamp != null)
                    {
                        if (e != null) Mark(e, StSkip);
                        continue;
                    }
                    if (mp.IsLordParty) _townHold.Remove(mp);   // wyjechal z osady (cudzy rozkaz) - dalej jak kazdy w polu
                    // T10: dluznik spi pod ksiega dlugu (AiDebtCamp) - oboz swiata go nie rusza
                    if (mp.IsLordParty && _debtSleep.ContainsKey(mp))
                    {
                        tally.DebtField++;
                        if (e != null) Mark(e, StDebt);
                        continue;
                    }
                    // T10: samotny lord i wodz armii - nocny marsz tylko z powodu (R1)
                    if (mp.IsLordParty && byReason)
                    {
                        LordNight(mp, s, e, campSet, debtOn, tally);
                        continue;
                    }

                    // ---- stara sciezka T1 (bez zmian w dzialaniu); dla lordow T10 liczy obok "na sucho", co by bylo ----
                    // nie kazda kolumna staje - czesc maszeruje przez cala noc
                    // (deterministycznie per partia i noc, zeby nie migotalo co godzine)
                    // T1 (uwaga S6): WODZ ARMII nie korzysta z pomijania - armia staje
                    // zawsze, chyba ze wrog blisko albo poscig / ucieczka (nizej)
                    // poprawka recenzji: wlasny wylacznik ArmyLeadersAlwaysCamp (wylaczony = wodz losuje jak kazdy lord)
                    bool armyLeader = s.ArmyLeadersAlwaysCamp && mp.Army != null && mp.Army.LeaderParty == mp;
                    // T10 na sucho: co powiedzialaby regula powodow - liczone PRZED skutkami starej sciezki (rozkaz spiacego jeszcze w _orders)
                    NReason dryR = NReason.None; bool dryAlarm = false, dryApplies = false;
                    if (mp.IsLordParty) { try { dryApplies = DryClassify(mp, s, e, campSet, out dryR, out dryAlarm); } catch (Exception ex) { dryApplies = false; AiStumble("DryClassify", mp, ex); } }
                    string cause = null;
                    if (!armyLeader && s.AiCampSkipPercent > 0 &&
                        (mp.Id.InternalValue + (uint)CampaignTime.Now.ToDays) % 100u
                            < (uint)Math.Max(0, Math.Min(95, s.AiCampSkipPercent))) cause = "los";
                    else if (mp.IsCurrentlyAtSea) cause = "morze";
                    else if (mp.Army != null && mp.Army.LeaderParty != mp) cause = "armia";   // eskorta idzie za wodzem
                    else if (Undead.Party(mp)) cause = "umarli";                                // Inni maszeruja noca
                    else
                    {
                        string stb = mp.ShortTermBehavior.ToString();
                        if (stb.StartsWith("Flee")) cause = "ucieczka";                          // ucieczka i pogon
                        else if (stb.StartsWith("Engage")) cause = "poscig";
                    }
                    if (cause == null)
                    {
                        bool danger = false;
                        var mf = mp.MapFaction;
                        foreach (var t in threats)
                        {
                            if (t == mp || t.MapFaction == mf) continue;
                            bool hostile = t.IsBandit || (mf != null && t.MapFaction != null && mf.IsAtWarWith(t.MapFaction));
                            if (!hostile) continue;
                            if (mp.GetPosition2D.Distance(t.GetPosition2D) <= s.AiCampDangerRadius) { danger = true; break; }
                        }
                        if (danger)
                        {
                            if (_tented.Contains(mp)) { Tent(mp, false); _tented.Remove(mp); }
                            _camping.Remove(mp);
                            _bedPos.Remove(mp);
                            GiveOrderBack(mp);          // alarm w nocy - rozkaz wraca od reki
                            cause = "wrog";             // wrog blisko - zwijaja sie i ida
                        }
                    }
                    if (mp.IsLordParty)
                    {
                        if (dryApplies) DryCount(mp, cause, dryR, dryAlarm, tally);
                        if (e != null) Mark(e, cause == null ? StSlept : (cause == "los" ? StOldLot : StOldOther));
                    }
                    else if (mp.IsCaravan && cause == "los") tally.CaravansLot++;
                    if (cause != null) continue;

                    RememberOrder(mp);              // po co wyszedl - zapisane przed snem
                    mp.Ai.DisableForHours(1);       // spia godzine; nocny tick odnowi
                    mp.SetMoveModeHold();
                    if (!_camping.Contains(mp)) { _camping.Add(mp); _bedPos[mp] = mp.GetPosition2D; }
                }
                // namioty wokol gracza (czesciej odswieza je OnTick - tu tylko takt godzinowy)
                RefreshNearbyTents(s);
                if (s.CampTentIcon) ReassertTents();   // konie nie wracaja na namioty

                // RAPORT NOCY: ilu spi i ilu trzeba bylo klasc z powrotem
                int lords = 0, caravans = 0, bandits = 0, armies = 0;
                foreach (var mp in _camping)
                {
                    if (mp == null) continue;
                    if (mp.IsCaravan) caravans++; else if (mp.IsBandit) bandits++; else lords++;
                    if (mp.Army != null && mp.Army.LeaderParty == mp) armies++;
                }
                Log.Info("AiNightCamp: " + h + ":00 - spi " + _camping.Count + " (lordow " + lords
                         + ", karawan " + caravans + ", band " + bandits + ", w tym wodzow armii " + armies
                         + "); obudzonych cudza reka od poprzedniej godziny: " + _wokenHour + "." + HourCountersText());
                ResetHourCounters();   // T1: wszystkie liczniki godziny (byl sam _wokenHour)
                try { LogNightTally(h, byReason, tally); } catch (Exception e2) { Log.Error("AiNightCamp.T10", e2); }
            }
            catch (Exception e) { Log.Error("AiNightCamp", e); }
        }

        /// <summary>
        /// NATURY BAND (Jeff: "niektorzy poluja w dzien, inni w nocy, roznie").
        /// Kazda banda ma stala nature (z jej Id, nie zmienia sie): trzy na
        /// cztery to NOCNI lowcy - za dnia (10-16) leza w ukryciu, noca chodza;
        /// jedna na cztery to DZIENNI - poluja w sloncu, a klada sie noca
        /// (23-5). Miedzy oknami (swit, wieczor) wszyscy sa na nogach.
        /// Spoczynek = AI wstrzymane na godzine, bez namiotow (chowaja sie,
        /// nie obozuja). Pogon, ucieczka i wrogi lord w poblizu uniewazniaja
        /// drzemke. Nocnym sprzyja krotszy nocny zasieg wzroku podroznych.
        /// </summary>
        private static void AiBanditRest(Settings s, int h)
        {
            try
            {
                if (!s.BanditsRestByDay) return;
                bool dayWindow = h >= 10 && h <= 16;      // spia nocni lowcy
                bool nightWindow = InCamp(h);             // spia dzienni lowcy (T1: godziny obozu)
                if (!dayWindow && !nightWindow) return;

                var lords = new System.Collections.Generic.List<MobileParty>();
                foreach (var t in MobileParty.All)
                    if (t != null && t.IsActive && t.IsLordParty) lords.Add(t);

                foreach (var mp in MobileParty.All)
                {
                    if (mp == null || !mp.IsActive || !mp.IsBandit) continue;
                    if (mp.CurrentSettlement != null || mp.MapEvent != null) continue;
                    if (Undead.Party(mp)) continue;
                    string stb = mp.ShortTermBehavior.ToString();
                    if (stb.StartsWith("Flee") || stb.StartsWith("Engage")) continue;   // pogon nie zna pory
                    bool dayHunter = mp.Id.InternalValue % 4u == 0u;   // stala natura bandy
                    if (dayWindow && dayHunter) continue;              // dzienny wlasnie poluje
                    if (nightWindow && !dayHunter) continue;           // nocny wlasnie poluje
                    bool danger = false;
                    foreach (var t in lords)
                        if (mp.GetPosition2D.Distance(t.GetPosition2D) <= s.AiCampDangerRadius) { danger = true; break; }
                    if (danger) continue;
                    mp.Ai.DisableForHours(1);
                    mp.SetMoveModeHold();
                }
            }
            catch (Exception e) { Log.Error("AiBanditRest", e); }
        }

        // ---------------------------------------------------- namiot na mapie
        // ZASADA z CLAUDE.md: licz potkniecia, nie gas funkcji. Stary _tentBroken
        // gasil namioty CALEMU swiatu po jednej wywrotce na jednej partii
        // (Jeff: "kiedys dzialalo, potem sie popsulo"). Teraz: 3 wywrotki
        // Z RZEDU wylaczaja, kazdy sukces zeruje licznik.
        private static int _tentStrikes;
        private const int TentStrikesMax = 3;
        // ile dzieci mial wizerunek gracza tuz po postawieniu namiotu - gdy
        // silnik mapy odbuduje figurke (menu, pauza, odswiezenie widoku),
        // liczba sie zmienia i namiot trzeba postawic OD NOWA (Jeff 27.08:
        // "jak jest oboz, to nie ma ikony namiotu")
        private static int _tentChildren = -1;
        private static DateTime _lastTentAssert = DateTime.MinValue;

        /// <summary>Gracz stoi obozem TERAZ - dla bitwy w obozie (CampScene). Stan niezalezny od ikony.</summary>
        internal static bool PlayerCamped;

        internal static void Tent(MobileParty mp, bool on)
        {
            try
            {
                if (mp != null && mp == MobileParty.MainParty)
                {
                    PlayerCamped = on;
                    if (on) _campPos = mp.GetPosition2D;
                }
                // T1: miejsce namiotu AI - DropMovedTents zdejmuje go, gdy kolumna odjedzie
                else if (mp != null)
                {
                    if (on) _tentPos[mp] = mp.GetPosition2D; else _tentPos.Remove(mp);
                }
                var s = Settings.Current;
                if (_tentStrikes >= TentStrikesMax || mp == null || s == null || !s.CampTentIcon) return;
                var tMgr = QuartermasterLaw.FindType("SandBox.View.Map.Managers.MobilePartyVisualManager");
                var cur = tMgr != null ? tMgr.GetProperty("Current",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static) : null;
                object mgr = cur != null ? cur.GetValue(null, null) : null;
                if (mgr == null) return;
                var getVis = mgr.GetType().GetMethod("GetPartyVisual");
                object vis = getVis != null ? getVis.Invoke(mgr, new object[] { mp.Party }) : null;
                var pStrat = vis != null ? vis.GetType().GetProperty("StrategicEntity") : null;
                object strat = pStrat != null ? pStrat.GetValue(vis, null) : null;
                if (strat == null) return;
                var tEnt = strat.GetType();
                var removeAll = tEnt.GetMethod("RemoveAllChildren");
                if (on)
                {
                    // "map_icon_siege_camp_tent" to MULTIMESH, nie prefab - stad
                    // czerwone TEMP z Instantiate. Silnik ma na to GOTOWA metode,
                    // ktora stawia namiot Z CHORAGWIA klanu - wolamy ja wprost.
                    var mTent = vis.GetType().GetMethod("AddTentEntityForParty",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (mTent == null) { _tentStrikes = TentStrikesMax; Log.Info("Tent: brak AddTentEntityForParty - namioty wylaczone."); return; }
                    if (removeAll != null) removeAll.Invoke(strat, null);
                    mTent.Invoke(vis, new object[] { strat, mp.Party, false });
                    // figurki jezdzca i konia zyja OSOBNO od ikony - chowamy je,
                    // zeby kon nie stal na namiocie (tak samo robi oboz BK)
                    ShowAgentFigures(vis, false);
                    // odcisk palca: po tej liczbie dzieci straznik w OnTick poznaje,
                    // ze silnik odbudowal wizerunek i namiot zniknal
                    if (mp == MobileParty.MainParty)
                        _tentChildren = ChildCountOf(strat);
                }
                else
                {
                    if (mp == MobileParty.MainParty) _tentChildren = -1;
                    ShowAgentFigures(vis, true);
                    // wlasciwy reset silnika: czysci ikone i kaze odbudowac figurke
                    var mClear = vis.GetType().GetMethod("ClearVisualMemory",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (mClear != null) mClear.Invoke(vis, null);
                    else
                    {
                        if (removeAll != null) removeAll.Invoke(strat, null);
                        try { mp.Party.SetVisualAsDirty(); } catch { }
                    }
                }
                _tentStrikes = 0;   // udalo sie - licznik potkniec od zera
            }
            catch (Exception e)
            {
                _tentStrikes++;
                Log.Error("Tent (potkniecie " + _tentStrikes + "/" + TentStrikesMax + ")", e);
                if (_tentStrikes >= TentStrikesMax) Log.Info("Tent: " + TentStrikesMax + " wywrotki z rzedu - namioty wylaczone do konca sesji.");
            }
        }

        private static int ChildCountOf(object strat)
        {
            try
            {
                var p = strat.GetType().GetProperty("ChildCount");
                if (p != null) return (int)p.GetValue(strat, null);
                var m = strat.GetType().GetMethod("GetChildCount");
                if (m != null) return (int)m.Invoke(strat, null);
            }
            catch { }
            return -1;
        }

        /// <summary>
        /// STRAZNIK NAMIOTU GRACZA. Silnik mapy przy odswiezeniach widoku (menu,
        /// pauza, wczytanie) potrafi odbudowac wizerunek partii - namiot znika,
        /// wraca konik. Co pare sekund sprawdzamy odcisk palca (liczbe dzieci
        /// encji) i gdy sie nie zgadza, stawiamy namiot od nowa. To NIE jest
        /// robota co klatke (pulapka z CLAUDE.md) - raz na 5 s i tylko przy
        /// realnej zmianie.
        /// </summary>
        internal static void ReassertPlayerTent()
        {
            try
            {
                if (!PlayerCamped || _tentChildren < 0) return;
                var s = Settings.Current;
                if (s == null || !s.CampTentIcon || _tentStrikes >= TentStrikesMax) return;
                if ((DateTime.Now - _lastTentAssert).TotalSeconds < 5.0) return;
                _lastTentAssert = DateTime.Now;

                var tMgr = QuartermasterLaw.FindType("SandBox.View.Map.Managers.MobilePartyVisualManager");
                var cur = tMgr != null ? tMgr.GetProperty("Current",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static) : null;
                object mgr = cur != null ? cur.GetValue(null, null) : null;
                if (mgr == null) return;
                var getVis = mgr.GetType().GetMethod("GetPartyVisual");
                object vis = getVis != null ? getVis.Invoke(mgr, new object[] { MobileParty.MainParty.Party }) : null;
                var pStrat = vis != null ? vis.GetType().GetProperty("StrategicEntity") : null;
                object strat = pStrat != null ? pStrat.GetValue(vis, null) : null;
                if (strat == null) return;
                int now = ChildCountOf(strat);
                if (now == _tentChildren) return;   // namiot stoi - nic nie ruszamy
                Log.Info("Tent: silnik odbudowal wizerunek (dzieci " + _tentChildren + " -> " + now + ") - stawiam namiot od nowa.");
                Tent(MobileParty.MainParty, true);
            }
            catch { }
        }

        /// <summary>Figurki czlowieka, konia i mulow karawany - widoczne albo nie.</summary>
        private static void ShowAgentFigures(object vis, bool visible)
        {
            try
            {
                string[] props = { "HumanAgentVisuals", "MountAgentVisuals", "CaravanMountAgentVisuals" };
                foreach (var name in props)
                {
                    try
                    {
                        var p = vis.GetType().GetProperty(name);
                        object av = p != null ? p.GetValue(vis, null) : null;
                        if (av == null) continue;
                        var mGet = av.GetType().GetMethod("GetEntity");
                        object ent = mGet != null ? mGet.Invoke(av, null) : null;
                        if (ent == null) continue;
                        var mVis = ent.GetType().GetMethod("SetVisibilityExcludeParents");
                        if (mVis != null) mVis.Invoke(ent, new object[] { visible });
                    }
                    catch { }
                }
            }
            catch { }
        }

        /// <summary>Silnik potrafi przywrocic figurki w nocy - obozujacym chowamy je co godzine.</summary>
        private static void ReassertTents()
        {
            try
            {
                var tMgr = QuartermasterLaw.FindType("SandBox.View.Map.Managers.MobilePartyVisualManager");
                var cur = tMgr != null ? tMgr.GetProperty("Current",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static) : null;
                object mgr = cur != null ? cur.GetValue(null, null) : null;
                if (mgr == null) return;
                var getVis = mgr.GetType().GetMethod("GetPartyVisual");


                foreach (var mp in _tented)
                {
                    try
                    {
                        if (mp == null || !mp.IsActive) continue;
                        object vis = getVis != null ? getVis.Invoke(mgr, new object[] { mp.Party }) : null;
                        if (vis != null) ShowAgentFigures(vis, false);
                    }
                    catch { }
                }
            }
            catch { }
        }

        /// <summary>Swit ksiegi gracza. MUSZTRA-j: na kazdej sciezce (sluzba ROT -> 0, przespana baza -> dlug bez zmian, dlug +1) na koncu dlug o swicie
        /// = dlug po rozliczeniu (jak SettleAi: e.DawnDebt = e.Debt) - od tego switu do nastepnego musztra czyta DawnDebt.</summary>
        private static void SettleNight(Settings s)
        {
            try { SettleNightCore(s); }
            finally { DawnDebt = Debt; }
        }

        private static void SettleNightCore(Settings s)
        {
            // splata calego dlugu idzie OD REKI (CreditRest, prog NeededHours);
            // swit zamyka dobe: kto nie przespal nawet BAZY, temu rosnie dlug.
            // KTO WLASNIE SPI (_sleeping), ten dlugu nie dostaje - dospi po swicie.
            // Przespana baza przy niesplaconych odsetkach: dlug STOI w miejscu.
            float baza = Math.Max(1f, s.SleepHoursNeeded);
            float sleptH = _restTonight;
            bool sleptBase = _sleeping || _restTonight >= baza;
            bool paidInFull = _credited;
            _restTonight = 0f;
            _credited = false;
            if (RotEnlisted()) { Debt = 0; return; }   // w sluzbie spisz, kiedy kaza

            if (sleptBase)
            {
                // zegarek dla spiacych BEZ menu (postoj noca): ile nocy przespane.
                // CZAPKA DO POTRZEBY (Jeff 31.08: "pracuje w miescie, a on mysli
                // ze spalem 18h") - caly dzien w osadzie naliczal sie jak sen;
                // czlowiek spi ile trzeba (baza+odsetki), reszta doby to zycie
                // i praca, nie drzemka. Dlug i splata licza sie jak dotad.
                if (!_sleeping && sleptH >= 1f)
                {
                    float shown = Math.Min(sleptH, NeededHours());
                    Msg("The night gave the men about " + shown.ToString("0.#") + "h of sleep.", Colors.White);
                }
                if (Debt > 0 && !paidInFull)
                    Msg("The men slept, but old weariness lingers - a full rest takes "
                        + (int)Math.Ceiling(NeededHours()) + " hours.", Colors.Yellow);
                return;
            }

            Debt = Math.Min(3, Debt + 1);
            if (Debt == 1)
                Msg("The men marched through the night. One sleepless night - speed -" + SpdPenalty[1] + "%, morale -"
                    + MorPenalty[1] + "%; paying it back will take " + (int)Math.Ceiling(NeededHours()) + " hours of rest.", Colors.Yellow);
            else if (Debt == 2)
                Msg("Second night without sleep - the column staggers (speed -" + SpdPenalty[2] + "%, morale -"
                    + MorPenalty[2] + "%). A full rest now takes " + (int)Math.Ceiling(NeededHours()) + " hours.", Colors.Red);
            else
            {
                Msg("Third sleepless night - the company collapses where it stands (speed -" + SpdPenalty[3]
                    + "%, morale -" + MorPenalty[3] + "%). They need " + (int)Math.Ceiling(NeededHours())
                    + " hours of rest.", Colors.Red);
                // wojsko ZASYPIA: kolumna staje w miejscu (raz, przy zapasci -
                // jesli gracz mimo to pogna dalej, powlecze sie na 10% predkosci)
                try
                {
                    var mp = MobileParty.MainParty;
                    if (mp != null && mp.CurrentSettlement == null && mp.MapEvent == null)
                        mp.SetMoveModeHold();
                }
                catch { }
            }
        }

        private static void Msg(string t, Color c)
        {
            try { InformationManager.DisplayMessage(new InformationMessage(t, c)); } catch { }
        }

        // ------------------------------------------------------------ kary
        // T10 (R2, ta sama kara co gracz): dlug partii AI z ksiegi snu AI (slownik _aiPenalty, tylko dlug > 0).
        // Predkosc liczona ROWNOLEGLE (CampaignTickCacheDataStore.RealTick -> TWParallel) - slownik nigdy nie jest
        // zmieniany po publikacji: ksiega buduje NOWY obiekt w ticku godzinowym (watek glowny) i podmienia referencje.
        private static int DebtFor(MobileParty mobileParty)
        {
            if (mobileParty == MobileParty.MainParty) return Debt;
            var pen = _aiPenalty;
            int d;
            return pen != null && pen.Count > 0 && pen.TryGetValue(mobileParty, out d) ? d : 0;
        }

        /// <summary>
        /// grupa11 - JEDNO ZRODLO PRAWDY "KTO SPAL": dlug snu partii, ktory naprawde dziala - ten sam, ktory zabiera predkosc i morale
        /// (SpeedPostfix, MoralePostfix): gracz - jego ksiega (Debt), kazda inna partia lorda - ksiega snu AI T10 (R2), takze lordowie
        /// doczepieni do armii gracza (ida z nim noca, wiec ich ksiega liczy te same nieprzespane noce). 0 przy wylaczonym NightRestEnabled,
        /// przy wylaczonym AiSleepDebt / obozie swiata i w DLL na sucho (wtedy AI dlugu nie ma). MUSZTRA-j: kare musztry liczy juz dlug o swicie
        /// (DawnDebtOf ponizej); stad musztra bierze tylko dlug biezacy do linii (kontrola). Czyta podmieniany w calosci slownik kar - bezpieczne z kazdego watku.
        /// </summary>
        internal static int DebtOf(MobileParty mp)
        {
            if (mp == null) return 0;
            var s = Settings.Current;
            if (s == null || !s.NightRestEnabled) return 0;   // przelacznik glowny wylaczony = kary nie dzialaja, dlugu nie ma (jak SpeedPostfix)
            return DebtFor(mp);
        }

        /// <summary>
        /// MUSZTRA-j - dlug snu partii O OSTATNIM SWICIE (decyzja Jeffa 09.10 07:10 pkt 2: "noc bez snu = nastepny dzien bez cwiczen", od switu do switu,
        /// niezaleznie od godziny ticku treningu; gracz i AI tak samo). Czyta TYLKO musztra (Drill.SleepDebt). Gracz - DawnDebt (koniec SettleNight),
        /// kazda inna partia lorda - ksiega snu AI T10 (AiSleep.DawnDebt, ustawiany w SettleAi). Ta sama ksiega i ten sam dlug co DebtOf (predkosc i morale),
        /// rozni sie tylko chwila odczytu: splata w ciagu dnia zdejmuje kare marszu od reki, a dzien cwiczen jest juz stracony. 0 w tych samych warunkach
        /// co DebtOf: wylaczony NightRestEnabled, ksiega AI bez czynnego dlugu (AiDebtLive), a takze w chwili miedzy wlaczeniem dlugu AI w MCM a pierwszym
        /// tickiem ksiegi (_debtWasOn == false - slownik kar jest wtedy jeszcze pusty, ResetDebts zeruje DawnDebt dopiero w tym ticku).
        /// UWAGA: czyta slownik ksiegi _ai (zmieniany w ticku godzinowym) - TYLKO z watku glownego (tick treningu partii jest na glownym). Nie wolac
        /// z predkosci (liczona rownolegle) - ta czyta podmieniany w calosci slownik kar (DebtFor).
        /// </summary>
        internal static int DawnDebtOf(MobileParty mp)
        {
            if (mp == null) return 0;
            var s = Settings.Current;
            if (s == null || !s.NightRestEnabled) return 0;
            if (mp == MobileParty.MainParty) return DawnDebt;
            if (!AiDebtLive(s) || _debtWasOn == false) return 0;
            AiSleep e;
            return _ai.TryGetValue(mp, out e) && e != null ? e.DawnDebt : 0;
        }

        internal static void SpeedPostfix(MobileParty mobileParty, ref ExplainedNumber __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.NightRestEnabled || mobileParty == null) return;
                int d = DebtFor(mobileParty);
                if (d < 1) return;
                if (!SpeedDepth.OutermostFinal) return;          // lancuch modeli (ROT->RB->vanilla): kara tylko raz
                // kara od WYNIKU (po suficie kolumny MarchPace - nasz postfix
                // biegnie ostatni), nie od bazy: -25% ma byc widoczne takze
                // w wolnej, objuczonej kolumnie. Vanillowy LimitMin(1) trzyma.
                // T10 (uwaga krytyki 5): Add() doklada do BAZY, ktora gra mnozy potem przez (1 + suma wspolczynnikow) -
                // wpis "-cut" zdejmowal cut x (1 + suma), np. -28.5% zamiast -25% przy +14%. Dzielimy jak MarchPace,
                // wiec kara jest dokladnie z tablicy (gracz i AI).
                // poprawka recenzji: DLL na sucho (T10_DRY) zostawia stary wzor - P0 porownuje sie z baza T1 bez innej kary gracza
                float cut = __result.ResultNumber * SpdPenalty[Math.Min(3, d)] / 100f;
                float f = DryBuild ? 1f : 1f + __result.SumOfFactors;
                if (cut > 0f) __result.Add(f > 0.01f ? -cut / f : -cut, _txtSleepless);
            }
            catch { }
        }

        internal static void MoralePostfix(MobileParty mobileParty, ref ExplainedNumber __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.NightRestEnabled || mobileParty == null) return;
                int d = DebtFor(mobileParty);
                if (d < 1) return;
                if (!SpeedDepth.OutermostMorale) return;         // lancuch modeli morale: kara tylko raz
                // procentowo ("morale spada o 95%"), nie punktowo - przy zapasci
                // z bazowego ~50 zostaje ~2-3, ponizej progu dezercji: spiacego
                // wojska pilnowac trzeba jak ognia
                // T10: wspolczynnik razy (1 + suma wspolczynnikow) - kara rowno z tablicy takze przy innych mnoznikach (jak Rations)
                float f = DryBuild ? 1f : 1f + __result.SumOfFactors;   // na sucho stary wzor (jak wyzej)
                float pct = MorPenalty[Math.Min(3, d)] / 100f;
                __result.AddFactor(f > 0.01f ? -pct * f : -pct, _txtSleepless);
            }
            catch { }
        }

        private static readonly TextObject _txtSleepless = new TextObject("{=!}Sleepless nights");

        // ------------------------------------------------------------ klawisz O
        private static bool _keyDown;
        private static bool _askOpen;

        private static Vec2 _campPos;

        internal static void OnTick(float dt)
        {
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                if (Campaign.Current != null) MasterSwitch(s);   // T10 poprawka recenzji: NightRestEnabled wylaczony w trakcie gry
                // grupa11-p: ksiega snu AI z zapisu od pierwszej klatki po wczytaniu (kary AI od reki, jak dlug gracza) - raz, potem _aiPending == null
                if (Campaign.Current != null) AiImportNow(s);

                // straznik co klatke: namiot nie jezdzi po mapie - gracz ruszyl,
                // wizerunek schodzi od reki (tick godzinowy bywal o godzine za pozno)
                if (PlayerCamped && Campaign.Current != null && MobileParty.MainParty != null
                    && MobileParty.MainParty.GetPosition2D.Distance(_campPos) > 0.25f)
                {
                    Tent(MobileParty.MainParty, false);
                    PlayerCamped = false;
                }

                // namiot gracza wraca, gdy silnik odbuduje wizerunek (raz na 5 s)
                if (PlayerCamped && Campaign.Current != null) ReassertPlayerTent();

                // mijane obozy dostaja namiot OD RAZU (refresh co ~2 s realne),
                // nie dopiero na godzinnym ticku kampanii
                if (s.AiCampsAtNight && s.CampTentIcon && Campaign.Current != null && _camping.Count > 0
                    && (DateTime.Now - _lastTentRefresh).TotalSeconds > 2.0)
                {
                    _lastTentRefresh = DateTime.Now;
                    int hh = CampaignTime.Now.GetHourOfDay;
                    if (InCamp(hh)) RefreshNearbyTents(s);
                }

                // T1: straznik snu co 0.1 h GRY (byl co ~1 s realna): cudze rozkazy
                // i przesuniecia wracaja na Hold; ujemna roznica = wczytanie - od reki
                if (s.AiCampsAtNight && Campaign.Current != null && _camping.Count > 0
                    && GuardDue(ref _lastHoldSweep, ref _holdDhMax))
                {
                    int hs = CampaignTime.Now.GetHourOfDay;
                    if (InCamp(hs)) HoldSleepers();
                }

                // T1: namioty z kolumn, ktore ruszyly, schodza co 0.1 h GRY
                if (s.AiCampsAtNight && s.CampTentIcon && Campaign.Current != null && _tented.Count > 0
                    && GuardDue(ref _lastTentDrop, ref _dropDhMax))
                    DropMovedTents();

                // T10: straznik snu dluznikow (o kazdej godzinie - oboz splaty od 20:00, sen ciagly przez dzien)
                // i rozstrzygniecie alarmow (ucieczka albo z powrotem spac) - co 0.1 h GRY
                // poprawka recenzji: tylko przy wlaczonym przelaczniku glownym (wylaczony = MasterSwitch zwolnil wszystkich); stoper do linii switu
                if (s.NightRestEnabled && Campaign.Current != null && _debtSleep.Count > 0 && GuardDue(ref _lastDebtSweep, ref _debtDhMax))
                {
                    long tg = System.Diagnostics.Stopwatch.GetTimestamp();
                    HoldDebtSleepers();
                    GuardTime(tg);
                }
                if (s.NightRestEnabled && Campaign.Current != null && _alarmed.Count > 0 && GuardDue(ref _lastAlarmSweep, ref _alarmDhMax))
                {
                    long tg = System.Diagnostics.Stopwatch.GetTimestamp();
                    GuardAlarmed();
                    GuardTime(tg);
                }

                if (!s.QuickCampKey || _askOpen) return;
                bool down = Input.IsKeyDown(InputKey.O);
                bool pressed = down && !_keyDown;
                _keyDown = down;
                if (!pressed) return;

                if (Campaign.Current == null || MobileParty.MainParty == null) return;
                var st = Game.Current != null && Game.Current.GameStateManager != null
                    ? Game.Current.GameStateManager.ActiveState as MapState : null;
                if (st == null || st.AtMenu) return;                        // tylko czysta mapa
                if (MobileParty.MainParty.CurrentSettlement != null) return;
                if (MobileParty.MainParty.IsCurrentlyAtSea) return;          // na pokladzie nie ma gdzie wbic palika
                if (PlayerEncounter.Current != null || MobileParty.MainParty.MapEvent != null) return;

                // NIE otwieramy menu prosto z ticku - to klablo gre (GameMenuVM
                // tykal w pol zbudowanego kontekstu). Pytajka jak w decyzji BK:
                // jej callback biegnie bezpieczna sciezka UI, ta sama co u nich.
                _askOpen = true;
                // PANEL OBOZOWY (Jeff 31.08: "moge zmienic zdanie - to nie moze
                // byc stale"): klawisz O poza rozbiciem obozu przestawia nocna
                // polityke W KAZDEJ CHWILI - aktualny tryb oznaczony.
                string cur0 = CampPromptMode == 0 ? " (current)" : "";
                string cur1 = CampPromptMode == 1 ? " (current)" : "";
                string cur2 = CampPromptMode == 2 ? " (current)" : "";
                var copts = new System.Collections.Generic.List<InquiryElement>
                {
                    new InquiryElement(0, "Make camp here", null, true,
                        "Pitch the tents on this spot. Breaking camp leaves the party disorganized for a while."),
                    new InquiryElement(1, "Nightfall orders: ask me at dusk" + cur0, null, true,
                        "Each night on the march the column asks whether to make camp."),
                    new InquiryElement(2, "Nightfall orders: always make camp" + cur1, null, true,
                        "At dusk the column camps on its own - no questions."),
                    new InquiryElement(3, "Nightfall orders: never ask" + cur2, null, true,
                        "March freely at night; camp only when you press O.")
                };
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    "Camp", "Pitch the tents - or set the standing orders for nightfall.",
                    copts, true, 1, 1, "Choose", GameTexts.FindText("str_cancel").ToString(),
                    delegate (System.Collections.Generic.List<InquiryElement> sel)
                    {
                        _askOpen = false;
                        try
                        {
                            if (sel == null || sel.Count == 0) return;
                            switch ((int)sel[0].Identifier)
                            {
                                case 0: MakeCampNow(); break;
                                case 1: CampPromptMode = 0; Msg("Nightfall orders: the column will ask at dusk.", Colors.White); break;
                                case 2: CampPromptMode = 1; Msg("Nightfall orders: the column will camp at dusk on its own.", Colors.White); break;
                                case 3: CampPromptMode = 2; Msg("Nightfall orders: no questions - march at will.", Colors.White); break;
                            }
                        }
                        catch (Exception e) { Log.Error("NightRest.CampPanel", e); }
                    },
                    delegate (System.Collections.Generic.List<InquiryElement> _) { _askOpen = false; }));
            }
            catch { }
        }

        private static void NightfallAsk()
        {
            try
            {
                var opts = new System.Collections.Generic.List<InquiryElement>
                {
                    new InquiryElement(0, "Make camp", null, true,
                        "Pitch the tents here and bed down for the night."),
                    new InquiryElement(1, "March on", null, true,
                        "Ride through the darkness - the sleep debt will follow."),
                    new InquiryElement(2, "Always make camp at nightfall", null, true,
                        "From now on the column camps at dusk on its own - no more asking."),
                    new InquiryElement(3, "Never ask again", null, true,
                        "March freely at night; you can still camp with the O key.")
                };
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    "Night Falls",
                    "Darkness gathers over the column and the road ahead disappears. Make camp?",
                    opts, true, 1, 1, "Choose", "Later",
                    delegate (System.Collections.Generic.List<InquiryElement> sel)
                    {
                        try
                        {
                            if (sel == null || sel.Count == 0) return;
                            switch ((int)sel[0].Identifier)
                            {
                                case 0: MakeCampNow(); break;
                                case 2: CampPromptMode = 1; MakeCampNow(); break;
                                case 3: CampPromptMode = 2;
                                        Msg("The nightfall question will trouble you no more.", Colors.White); break;
                            }
                        }
                        catch (Exception ex) { Log.Error("NightfallAsk.Selected", ex); }
                    },
                    delegate (System.Collections.Generic.List<InquiryElement> _) { }), true);
            }
            catch (Exception e) { Log.Error("NightfallAsk", e); }
        }

        /// <summary>Jedna sciezka obozu: BK jesli jest, inaczej nasz wlasny.</summary>
        internal static void MakeCampNow()
        {
            try
            {
                // OBOZ = STOP (Jeff 31.08: "spimy i idziemy jednoczesnie - bug").
                // Auto-oboz wchodzil w sen bez zdjecia rozkazu ruchu i ludzik
                // maszerowal przez cala noc z paskiem snu na ekranie.
                var mp = MobileParty.MainParty;
                if (mp != null) mp.SetMoveModeHold();
                if (!TryBkCamp()) OwnCamp();
            }
            catch (Exception e) { Log.Error("MakeCampNow", e); }
        }

        /// <summary>Wlasny oboz Armoury - gdy oboz BannerKings nie istnieje w tej kampanii.</summary>
        private static void OwnCamp()
        {
            try
            {
                GameMenu.ActivateGameMenu("arm_camp_wait");
                Tent(MobileParty.MainParty, true);
                Msg("You pitch camp.", Colors.White);
            }
            catch (Exception e) { Log.Error("NightRest.OwnCamp", e); }
        }

        private static bool TryBkCamp()
        {
            try
            {
                var t = QuartermasterLaw.FindType("BannerKings.Behaviours.Camping.BKCampingBehavior");
                if (t == null) return false;
                var get = typeof(Campaign).GetMethod("GetCampaignBehavior").MakeGenericMethod(t);
                var beh = get.Invoke(Campaign.Current, null);
                var m = t.GetMethod("MakeCamp", BindingFlags.Public | BindingFlags.Instance);
                if (beh == null || m == null) return false;
                m.Invoke(beh, new object[] { MobileParty.MainParty });
                // przez Tent(), nie gola flage: Tent ustawia TAKZE _campPos.
                // Stare `PlayerCamped = true` zostawialo _campPos z POPRZEDNIEGO
                // obozu i straznik w OnTick (dystans > 0.25) zwijal namiot
                // W NASTEPNEJ KLATCE - Jeff: "czasami nie tworzy sie namiot".
                Tent(MobileParty.MainParty, true);
                Msg("You pitch camp.", Colors.White);
                return true;
            }
            catch (Exception e) { Log.Error("NightRest.TryBkCamp", e); return false; }
        }

        // ------------------------------------------------------------ menu obozu
        internal static void AddMenus(CampaignGameStarter starter)
        {
            try
            {
                // zwykly postoj ("camp"): polozyc sie spac - o kazdej porze
                starter.AddGameMenuOption("camp", "arm_sleep_opt",
                    "{=!}Bed down and sleep", SleepOptionCondition,
                    delegate (MenuCallbackArgs a) { _sleepReturn = "camp"; GameMenu.SwitchToMenu("arm_sleep_wait"); }, false, 1);

                // WLASNY oboz (klawisz O, gdy obozu BK nie ma w kampanii).
                // ZWYKLE menu, nie "wait": oboz to twarda PAUZA - czas rusza
                // dopiero po wybraniu snu (arm_sleep_wait)
                starter.AddGameMenu("arm_camp_wait",
                    "{=!}You are encamped. The fires are lit, the men tend the horses and the wind worries the tents.",
                    delegate (MenuCallbackArgs a) { SetCampBackground(a); });
                starter.AddGameMenuOption("arm_camp_wait", "arm_camp_sleep",
                    "{=!}Bed down and sleep", SleepOptionCondition,
                    delegate (MenuCallbackArgs a) { _sleepReturn = "arm_camp_wait"; GameMenu.SwitchToMenu("arm_sleep_wait"); }, false, 1);
                starter.AddGameMenuOption("arm_camp_wait", "arm_camp_break",
                    "{=!}Break camp and move on",
                    delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                    delegate (MenuCallbackArgs a)
                    {
                        // zwijanie obozu to chwila krzatania, nie pol dnia:
                        // dezorganizacja rowno GODZINE (vanillowy model dawalby kilka)
                        try
                        {
                            MobileParty.MainParty.SetDisorganized(true);
                            var f = AccessTools.Field(typeof(MobileParty), "_disorganizedUntilTime");
                            if (f != null) f.SetValue(MobileParty.MainParty, CampaignTime.HoursFromNow(1f));
                        }
                        catch { }
                        Tent(MobileParty.MainParty, false);
                        Msg("The camp is struck - an hour to form the column, then we march.", Colors.Yellow);
                        GameMenu.ExitToLast();
                    }, true, 9);

                starter.AddWaitGameMenu("arm_sleep_wait",
                    "{=!}{ARM_SLEEP_TEXT}",
                    SleepInit, delegate (MenuCallbackArgs a) { return true; }, null, SleepTick,
                    GameMenu.MenuAndOptionType.WaitMenuShowProgressAndHoursOption);
                starter.AddGameMenuOption("arm_sleep_wait", "arm_sleep_stop",
                    "{=!}Rouse the men early",
                    delegate (MenuCallbackArgs a) { a.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                    delegate (MenuCallbackArgs a) { LeaveSleep(); }, true, 9);

                // oboz BannerKings pilnuje wlasnego menu co tick - tam wystarczy STAC
                starter.AddGameMenuOption("bk_camping_wait_menu", "arm_sleep_bk",
                    "{=!}Bed down for the night", SleepOptionCondition,
                    delegate (MenuCallbackArgs a)
                    {
                        Msg("The men bed down. Keep the camp standing through the night and they wake rested.", Colors.White);
                    }, false, 1);
                Log.Info("Nocleg: klawisz O + spanie w menu obozu dodane (dlug snu " + Debt + ").");
            }
            catch (Exception e) { Log.Error("NightRest.AddMenus", e); }
        }

        /// <summary>
        /// WYJSCIE ZE SNU. NIE wolno przelaczac menu (SwitchToMenu) z wnetrza opcji
        /// menu oczekiwania - VM menu jest w polowie klatki i konczy sie to
        /// NullReference w GameMenuVM.OnFrameTick, czyli CTD w obozie
        /// (Jeff: "jak klikam Rouse the men early wywala gre"). Wychodzimy tak,
        /// jak od zawsze dziala sasiedni, dzialajacy przycisk "Break camp".
        /// </summary>
        private static void LeaveSleep()
        {
            try
            {
                _sleeping = false;
                // PASEK JEST PRAWDA O TYM SNIE. Rachunek doby (_restTonight)
                // nalicza sie pelnymi godzinnymi tickami i gubi brzegi (pierwsza
                // godzina po przyjezdzie pada na regule "moved", niepelne godziny
                // nie istnieja w tickach) - pasek mowil "wyspani", a swit liczyl
                // 4/5 i dawal dlug (Jeff 27.08: "przespalem noc i rano mam, ze
                // men nie spali"). Po pobudce dopisujemy przespane godziny
                // i od razu splacamy dlug.
                _restTonight = Math.Max(_restTonight, _menuBase + _menuRest);
                var s = Settings.Current;
                if (s != null) CreditRest(s);
                // ZEGAREK SNU (Jeff 31.08): po pobudce liczby - ile zegara
                // uplynelo, ile snu sie NALICZYLO (dzien liczy sie slabiej)
                // i jaka byla jakosc; do tego przypomnienie o wiszacym dlugu
                try
                {
                    float clockH = (float)(CampaignTime.Now - _sleepStart).ToHours;
                    float effH = _menuRest;
                    int hh = CampaignTime.Now.GetHourOfDay;
                    string when = "It is " + hh + ":00, " + DayPart(hh) + ". ";
                    if (clockH < 0.5f)
                        Msg(when + "You barely closed your eyes - no rest to speak of.", Colors.White);
                    else
                    {
                        float q = clockH > 0.1f ? MBMath.ClampFloat(effH / clockH, 0f, 1f) : 1f;
                        string line;
                        if (q >= 0.9f)
                            line = when + "You slept " + clockH.ToString("0.#") + "h of sound night sleep.";
                        else if (q >= 0.7f)
                            line = when + "You slept " + clockH.ToString("0.#") + "h - fair rest, part of it by daylight ("
                                 + effH.ToString("0.#") + "h counted).";
                        else
                            line = when + "You slept " + clockH.ToString("0.#") + "h of fitful daylight sleep - it counted for "
                                 + effH.ToString("0.#") + "h.";
                        if (Debt > 0)
                            line += " Old weariness lingers - a full rest takes " + (int)Math.Ceiling(NeededHours()) + "h.";
                        Msg(line, Debt > 0 ? Colors.Yellow : Colors.White);
                    }
                }
                catch { }
                // pobudka zwija namiot - chyba ze WLASNY oboz dalej stoi
                // (wtedy namiot nalezy do obozu, zwinie go "Break camp")
                if (_sleepReturn != "arm_camp_wait")
                    Tent(MobileParty.MainParty, false);
                GameMenu.ExitToLast();
            }
            catch (Exception e) { Log.Error("NightRest.LeaveSleep", e); }
        }

        private static bool SleepOptionCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Wait;
            var s = Settings.Current;
            return s != null && s.NightRestEnabled;
        }

        /// <summary>Tlo menu obozu: grafika kultury (jak u armii), zapasowo ogolna - koniec z czerwonym "temp".</summary>
        private static void SetCampBackground(MenuCallbackArgs args)
        {
            try
            {
                string mesh = null;
                // Jeff 04.10: "czy te obrazki przy obozie musza byc z jezdzcami - nie ma obrazka obozu?"
                // 0 = grafika kultury (stare), 1 = oboz pod murami (vanilla), 2-10 = obrazki ROT z zycia w armii
                int pick = Settings.Current != null ? Settings.Current.CampBackground : 1;
                if (pick == 1) mesh = "wait_besieging";
                else if (pick == 2) mesh = "bg_enlistment";
                else if (pick >= 3 && pick <= 10) mesh = "bg_enlistment" + (pick - 1);
                if (mesh == null)
                try
                {
                    var f = Hero.MainHero != null ? Hero.MainHero.MapFaction : null;
                    if (f != null && f.Culture != null) mesh = f.Culture.EncounterBackgroundMesh;
                }
                catch { }
                if (string.IsNullOrEmpty(mesh)) mesh = "wait_fallback";
                args.MenuContext.SetBackgroundMeshName(mesh);
            }
            catch { try { args.MenuContext.SetBackgroundMeshName("wait_fallback"); } catch { } }
        }

        private static void SleepInit(MenuCallbackArgs args)
        {
            try
            {
                SetCampBackground(args);
                // SEN = NAMIOT NA MAPIE (Jeff: "jak spimy, ikona konia/czlowieka
                // ma sie zmienic w namiot"). Stawiamy raz, przy wejsciu w sen -
                // nie co klatke (pulapka wizerunkow z CLAUDE.md)
                if (MobileParty.MainParty != null && MobileParty.MainParty.CurrentSettlement == null)
                {
                    MobileParty.MainParty.SetMoveModeHold();   // sen = postoj, zaden marsz w tle
                    Tent(MobileParty.MainParty, true);
                }
                var s = Settings.Current;
                float needed = NeededHours();                  // baza + odsetki dlugu (do 21 h)
                // bezpiecznik: nikt nie spi wiecznie - ale przy dlugu snu suma
                // godzin ZEGARA bywa wieksza niz godzin SNU (dzien liczy sie slabiej)
                _sleepUntil = CampaignTime.HoursFromNow(Math.Max(14f, needed * 1.8f + 2f));
                _sleeping = true;
                _sleepStart = CampaignTime.Now;
                _menuRest = 0f;
                _menuBase = _restTonight;
                // cel snu: ile jeszcze brakuje DO wyspania - pasek liczy wlasne
                // godziny i NIE cofa sie, gdy swit wyzeruje rachunek doby
                _menuTarget = Math.Max(0.5f, needed - _restTonight);
                int h = CampaignTime.Now.GetHourOfDay;
                bool night = h >= 21 || h <= 5;
                MBTextManager.SetTextVariable("ARM_SLEEP_TEXT",
                    _restTonight >= needed
                        ? "The men have already slept their fill today - but a little more never hurt."
                        : ("The men bed down and sleep" + (night ? "." : " - by daylight the rest comes slower.")));
                args.MenuContext.GameMenu.StartWait();
            }
            catch (Exception e) { Log.Error("NightRest.SleepInit", e); }
        }

        private static void SleepTick(MenuCallbackArgs args, CampaignTime dt)
        {
            try
            {
                var s = Settings.Current;
                // wlasne godziny snu: noc pelna stawka, dzien slabiej - te same
                // zasady co rachunek doby w OnHourly, ale bez jego zerowania
                int h = CampaignTime.Now.GetHourOfDay;
                bool night = h >= 21 || h <= 5;
                float day = s != null ? MBMath.ClampFloat(s.DayRestFactor, 0.1f, 1f) : 0.5f;
                _menuRest += (float)dt.ToHours * (night ? 1f : day);
                if (_menuRest >= _menuTarget || (float)(_sleepUntil - CampaignTime.Now).ToHours <= 0.02f)
                {
                    LeaveSleep();
                    return;
                }
                args.MenuContext.GameMenu.SetProgressOfWaitingInMenu(Math.Min(1f, _menuRest / _menuTarget));
            }
            catch (Exception e) { Log.Error("NightRest.SleepTick", e); }
        }

        // ------------------------------------------------------------ sluzba ROT
        private static bool RotEnlisted()
        {
            try
            {
                var t = QuartermasterLaw.FindType("ROT.SubModule");
                var p = t != null ? t.GetProperty("EnlistmentBehavior", BindingFlags.Public | BindingFlags.Static) : null;
                object beh = p != null ? p.GetValue(null, null) : null;
                if (beh == null && t != null)
                {
                    var f = t.GetField("EnlistmentBehavior", BindingFlags.Public | BindingFlags.Static);
                    beh = f != null ? f.GetValue(null) : null;
                }
                if (beh == null) return false;
                var pe = beh.GetType().GetProperty("IsEnlisted", BindingFlags.Public | BindingFlags.Instance);
                return pe != null && pe.GetValue(beh, null) is bool b && b;
            }
            catch { return false; }
        }

        // ------------------------------------------------------------ save
        internal static string Export()
        {
            // MUSZTRA-j: piate pole - dlug o swicie (stary DLL czyta pola 0-3 i piate pomija; napis idzie przez SaveText.Sync - ArmouryBehavior "arm_nightrest")
            return Debt.ToString(CultureInfo.InvariantCulture) + ";" +
                   _restTonight.ToString(CultureInfo.InvariantCulture) + ";" +
                   (_credited ? "1" : "0") + ";" +
                   CampPromptMode.ToString(CultureInfo.InvariantCulture) + ";" +
                   DawnDebt.ToString(CultureInfo.InvariantCulture);
        }

        internal static void Import(string data)
        {
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                var parts = data.Split(';');
                if (parts.Length > 0) int.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out Debt);
                if (parts.Length > 1) float.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out _restTonight);
                _credited = parts.Length > 2 && parts[2] == "1";   // stary zapis (2 pola) = false
                CampPromptMode = 0;
                if (parts.Length > 3) int.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out CampPromptMode);
                if (CampPromptMode < 0 || CampPromptMode > 2) CampPromptMode = 0;
                Debt = Math.Max(0, Math.Min(3, Debt));   // stara skala szla do 5 - przytnij
                // MUSZTRA-j: dlug o swicie z piatego pola; stary zapis (bez pola) - dlug o swicie = dlug (jak ksiega AI: ResolveImport)
                int dd;
                DawnDebt = parts.Length > 4 && int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out dd) ? Math.Max(0, Math.Min(3, dd)) : Debt;
            }
            catch { }
        }

        // ------------------------------------------------------------ patche
        /// <summary>Tick ekranu mapy chodzi TAKZE na pauzie - klawisz O dziala w miejscu.</summary>
        internal static void MapFramePostfix()
        {
            try { OnTick(0f); } catch { }
        }

        internal static void ApplyAll(Harmony harmony)
        {
            try
            {
                int spd = PatchModels(harmony, typeof(PartySpeedModel), "CalculateFinalSpeed", "SpeedPostfix");
                int mor = PatchModels(harmony, typeof(PartyMoraleModel), "GetEffectivePartyMorale", "MoralePostfix");

                // CampaignEvents.TickEvent staje razem z pauza - a gracz w miejscu
                // to pauza wlasnie. Dopinamy sie do klatki ekranu mapy (chodzi zawsze).
                var tMap = QuartermasterLaw.FindType("SandBox.View.Map.MapScreen");
                var mTick = tMap != null ? AccessTools.Method(tMap, "OnFrameTick") : null;
                if (mTick != null)
                    harmony.Patch(mTick, postfix: new HarmonyMethod(typeof(NightRest).GetMethod(
                        "MapFramePostfix", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public)));

                Log.Info("Nocleg: kary snu wpiete (predkosc w " + spd + ", morale w " + mor + " modelach), "
                         + "klawisz O na klatce mapy: " + (mTick != null) + ".");
            }
            catch (Exception e) { Log.Error("NightRest.ApplyAll", e); }
        }

        private static int PatchModels(Harmony harmony, Type baseType, string method, string postfixName)
        {
            var post = new HarmonyMethod(typeof(NightRest).GetMethod(
                postfixName, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public))
            { priority = Priority.Last };
            int done = 0;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException rtle) { types = rtle.Types; }
                catch { continue; }
                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract || !baseType.IsAssignableFrom(t)) continue;
                    try
                    {
                        var m = t.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic |
                                                    BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        if (m == null || m.IsAbstract) continue;
                        harmony.Patch(m, postfix: post);
                        done++;
                    }
                    catch { }
                }
            }
            return done;
        }
    }
}
