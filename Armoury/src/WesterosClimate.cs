using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// KLIMAT WESTEROS (Jeff 04.10): w Westeros nie ma miesiecy ani obrotu roku - jest jedna
    /// pora roku, ktora trwa latami ("lato, ktore trwalo dziesiec lat"), a Cytadela oglasza
    /// jej koniec bialym krukiem. Data to "Year 299, Day 256, Summer" (13.10, CalendarText; dawniej "Day N of Summer, 299 AC").
    ///
    /// Decyzje Jeffa: start w lecie (trwa juz ~10 lat, konczy sie w 299 AC), potem jesien
    /// (~rok), pierwsza zima LOSOWO 3-5 lat, dalej pory losowe, wieloletnie.
    ///
    /// Stan: lista odcinkow (dzien startu, pora, dlugosc w dniach) - zapisywana w save,
    /// wiec mozna zapytac o pore roku dowolnej daty z przeszlosci.
    ///
    /// Jak gra sie dowiaduje: CampaignTime.GetSeasonOfYear / GetDayOfSeason / ToSeasons to
    /// malutkie gettery struktury - JIT wkleja je w miejsce wywolania, wiec latka na samym
    /// getterze moze nie dzialac. Zamiast tego TRANSPILER w konkretnych metodach, ktore czytaja
    /// pore roku (pogoda i snieg na mapie i w misjach, dzwiek mapy, data w powiadomieniach,
    /// RealisticBannerlord, StrategicCampaignAI, zdarzenia vanilla, BetterEconomy), podmienia
    /// wywolanie gettera na nasze. Data (CampaignTime.ToString) - prefix.
    /// CELOWO BEZ ZMIAN: BannerKings (laska dworu, swieta religijne, przysiegi) - to cykle
    /// kalendarza, nie pogody; przy wieloletnich porach odpalalyby raz na kilka lat.
    /// </summary>
    internal static class WesterosClimate
    {
        private sealed class Seg { public double Start; public int Season; public double Len; }

        private static readonly List<Seg> _segs = new List<Seg>();
        private static readonly Random _rng = new Random();
        private static bool _initBroken;
        // E8/S (246, PLAN 8.11c; CLAUDE.md 7 "licz potkniecia, nie gas funkcji"): _initBroken gasil klimat (pory roku, zime, 19 metod ekonomii) do konca
        // sesji po JEDNYM wyjatku w Ready(). Teraz to tylko "stoj do nastepnej doby": Daily zdejmuje flage i Ready() probuje jeszcze raz - najwyzej
        // InitStumblesMax prob w kampanii, potem linia ERROR raz na dobe (pory roku jak w grze do wczytania albo nowej kampanii). Ponowienie nie doda
        // drugiego lata: Ready() wychodzi na _segs.Count > 0, zanim cokolwiek doda. Klucz StateHygieneEnabled off = jak dotad (flaga do konca sesji)
        private static int _initStumbles, _initRetries;
        private static Exception _initLast;
        private const int InitStumblesMax = 3;

        internal static bool On { get { var s = Settings.Current; return s != null && s.ClimateEnabled; } }
        private static bool EconOn { get { var s = Settings.Current; return s != null && s.ClimateEnabled && s.ClimateDrivesEconomy; } }

        // PROBA ZIMY (autotest, Jeff 11.10 wariant b - WinterTrial): dlugosc lata od startu i pierwszej jesieni na TE kampanie, tylko w pamieci
        // (-1 = z ustawien). Nie przez Settings.Current: McmSettings.Apply przepisuje ustawienia z MCM co godzine gry, a pierwsza jesien losuje
        // sie dopiero w ok. 1. dobie - wartosc z Settings.Current juz by wtedy nie zyla. Reset (konstruktor ArmouryBehavior) = kazda kampania od nowa.
        private static int _trialSummerLeft = -1, _trialFirstAutumn = -1;

        internal static void Reset() { _segs.Clear(); _initBroken = false; _trialSummerLeft = -1; _trialFirstAutumn = -1; _initStumbles = 0; _initRetries = 0; _initLast = null; }

        /// <summary>E8/S (246): stan startu klimatu do linii "Porzadek stanu (E8/S)" - potkniecia Ready() w tej kampanii, ponowienia, czy pory roku czynne.</summary>
        internal static void HygieneState(out int stumbles, out int retries, out bool live)
        {
            stumbles = _initStumbles; retries = _initRetries; live = _segs.Count > 0;
        }

        /// <summary>E8/S (246): raz na dobe (Daily, przed Ready) - po potknieciu startu klimatu ponowna proba w tej dobie; po InitStumblesMax potknieciach
        /// juz tylko linia ERROR raz na dobe. Bez klucza StateHygieneEnabled nic nie robi (flaga zostaje do konca sesji, jak dotad).</summary>
        private static void RetryInit()
        {
            if (!_initBroken || _segs.Count > 0 || !StateHygiene.On) return;
            if (_initStumbles < InitStumblesMax) { _initBroken = false; _initRetries++; return; }
            Log.Error("WesterosClimate.Init (E8/S: " + _initStumbles + " potkniecia - klimat nie wystartowal, pory roku jak w grze do wczytania albo nowej kampanii)",
                      _initLast ?? new InvalidOperationException("brak zapamietanego wyjatku"));
        }

        /// <summary>WinterTrial (nowa kampania z plikiem-przelacznikiem): lato konczy sie za summerLeft dni od startu, pierwsza jesien trwa
        /// firstAutumn dni (jak Min = Max w ustawieniach). Gdy klimat juz ruszyl w tej kampanii (samo lato startowe - ktos zapytal o pore przed
        /// OnNewGameCreated), start klimatu od nowa z tymi wartosciami. Zwraca opis do logu.</summary>
        internal static string Trial(int summerLeft, int firstAutumn)
        {
            _trialSummerLeft = Math.Max(0, summerLeft);
            _trialFirstAutumn = Math.Max(0, firstAutumn);
            if (_segs.Count == 1) { _segs.Clear(); _initBroken = false; return "klimat ruszyl juz przed proba (samo lato startowe) - start klimatu od nowa z wartosciami proby"; }
            if (_segs.Count > 1) return "UWAGA: klimat ma juz " + _segs.Count + " por roku - proba za pozno, pory bez zmian";
            return "klimat wystartuje przy pierwszym odczycie pory z wartosciami proby";
        }

        // ------------------------------------------------------------ stan
        private static double NowDays()
        {
            return CampaignTime.Now.ToDays;
        }

        private static bool Ready()
        {
            if (_segs.Count > 0) return true;
            if (_initBroken) return false;
            try
            {
                if (Campaign.Current == null) return false;
                double probe;
                try { probe = NowDays(); } catch { return false; }   // zegar kampanii jeszcze nie gotowy - sprobujemy pozniej
                var s = Settings.Current;
                double now = NowDays();
                double soFar = Math.Max(0, s.ClimateSummerDaysSoFar);
                double left = _trialSummerLeft >= 0 ? _trialSummerLeft : Rand(s.ClimateSummerDaysLeftMin, s.ClimateSummerDaysLeftMax);
                _segs.Add(new Seg { Start = now - soFar, Season = (int)CampaignTime.Seasons.Summer, Len = soFar + left });
                Log.Info("Klimat: start - lato trwa juz " + (int)soFar + " dni, skonczy sie za " + (int)left
                         + " dni (dzien kampanii " + (int)now + ")" + (_trialSummerLeft >= 0 ? " - PROBNA ZIMA (autotest), pierwsza jesien " + _trialFirstAutumn + " dni" : "") + ".");
                if (_initStumbles > 0) Log.Info("Klimat (E8/S): start udany po " + _initStumbles + " potknieciach (ponowien " + _initRetries + ") - pory roku czynne.");
                return true;
            }
            catch (Exception e)
            {
                // E8/S (246): licznik potkniec - flaga trzyma juz tylko do nastepnej doby (RetryInit w Daily), nie do konca sesji.
                // Jedna proba = jedno potkniecie: gdyby pore roku czytaly naraz dwa watki gry, drugi w tej samej probie nie liczy drugi raz
                bool first = !_initBroken;
                _initBroken = true;
                if (first)
                {
                    _initStumbles++; _initLast = e;
                    Log.Error("WesterosClimate.Init (potkniecie " + _initStumbles + "/" + InitStumblesMax + ")", e);
                }
                return false;
            }
        }

        private static double Rand(double a, double b)
        {
            if (b < a) { var t = a; a = b; b = t; }
            return a + _rng.NextDouble() * (b - a);
        }

        /// <summary>Dlugosc NASTEPNEJ pory (w dniach) wedle ustawien Jeffa.</summary>
        private static double NextLen(int season, int index)
        {
            var s = Settings.Current;
            double year = Math.Max(28, CampaignTime.DaysInYear);
            // index 1 = pierwsza jesien, index 2 = pierwsza zima
            if (index == 1 && season == (int)CampaignTime.Seasons.Autumn)
                return _trialFirstAutumn >= 0 ? _trialFirstAutumn : Rand(s.ClimateFirstAutumnDaysMin, s.ClimateFirstAutumnDaysMax);
            if (index == 2 && season == (int)CampaignTime.Seasons.Winter)
                return year * Rand(s.ClimateFirstWinterYearsMin, s.ClimateFirstWinterYearsMax);
            switch (season)
            {
                case 0: return year * Rand(s.ClimateSpringYearsMin, s.ClimateSpringYearsMax);
                case 1: return year * Rand(s.ClimateSummerYearsMin, s.ClimateSummerYearsMax);
                case 2: return year * Rand(s.ClimateAutumnYearsMin, s.ClimateAutumnYearsMax);
                default: return year * Rand(s.ClimateWinterYearsMin, s.ClimateWinterYearsMax);
            }
        }

        private static Seg SegAt(double days)
        {
            Seg hit = _segs[0];
            for (int i = 0; i < _segs.Count; i++)
            {
                if (_segs[i].Start <= days) hit = _segs[i];
                else break;
            }
            return hit;
        }

        /// <summary>Raz dziennie: czy minela pora roku? Biale kruki Cytadeli.</summary>
        internal static void Daily()
        {
            if (On) RetryInit();   // E8/S (246): po potknieciu startu klimatu - ponowna proba w tej dobie (najwyzej 3 w kampanii), potem ERROR raz na dobe
            if (!On || !Ready()) return;
            double now = NowDays();
            int guard = 0;
            while (guard++ < 8)
            {
                var last = _segs[_segs.Count - 1];
                if (now < last.Start + last.Len) break;
                int next = (last.Season + 1) % 4;
                var seg = new Seg { Start = last.Start + last.Len, Season = next, Len = Math.Max(7, NextLen(next, _segs.Count)) };
                _segs.Add(seg);
                Log.Info("Klimat: dzien " + (int)now + " - nowa pora " + (CampaignTime.Seasons)next
                         + ", potrwa " + (int)seg.Len + " dni (" + (seg.Len / Math.Max(1, CampaignTime.DaysInYear)).ToString("0.0", CultureInfo.InvariantCulture) + " lat).");
                Log.Player(Raven(next), next == (int)CampaignTime.Seasons.Winter);
            }
        }

        private static string Raven(int season)
        {
            switch (season)
            {
                case 0: return "A white raven from the Citadel: the Conclave declares that winter is over. Spring has come.";
                case 1: return "A white raven from the Citadel: the Conclave declares that summer has come.";
                case 2: return "A white raven from the Citadel: the Conclave declares that summer has ended. Autumn has come.";
                default: return "A white raven from the Citadel: winter has come.";
            }
        }

        internal static string Export()
        {
            if (_segs.Count == 0) return "";
            var sb = new StringBuilder("v1");
            foreach (var g in _segs)
                sb.Append('|').Append(g.Start.ToString("R", CultureInfo.InvariantCulture)).Append(':')
                  .Append(g.Season).Append(':').Append(g.Len.ToString("R", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        internal static void Import(string data)
        {
            try
            {
                if (string.IsNullOrEmpty(data)) return;      // stary save - klimat zacznie sie od lata "teraz"
                var parts = data.Split('|');
                var list = new List<Seg>();
                for (int i = 1; i < parts.Length; i++)
                {
                    var f = parts[i].Split(':');
                    if (f.Length < 3) continue;
                    list.Add(new Seg
                    {
                        Start = double.Parse(f[0], CultureInfo.InvariantCulture),
                        Season = int.Parse(f[1], CultureInfo.InvariantCulture) & 3,
                        Len = double.Parse(f[2], CultureInfo.InvariantCulture)
                    });
                }
                if (list.Count == 0) return;
                _segs.Clear(); _segs.AddRange(list);
                var cur = _segs[_segs.Count - 1];
                Log.Info("Klimat: wczytano " + _segs.Count + " por roku; teraz " + (CampaignTime.Seasons)cur.Season
                         + " od dnia " + (int)cur.Start + ", planowo " + (int)cur.Len + " dni.");
            }
            catch (Exception e) { Log.Error("WesterosClimate.Import", e); }
        }

        // ------------------------------------------------------------ odczyty
        private static CampaignTime.Seasons VanillaSeason(ref CampaignTime t)
        {
            return (CampaignTime.Seasons)((((long)Math.Floor(t.ToSeasons)) % 4 + 4) % 4);
        }

        private static int VanillaDay(ref CampaignTime t)
        {
            int dis = Math.Max(1, CampaignTime.DaysInSeason);
            return (int)(((long)Math.Floor(t.ToDays)) % dis);
        }

        private static bool Live(bool econ)
        {
            if (econ ? !EconOn : !On) return false;
            try { return Ready(); } catch { return false; }
        }

        private static CampaignTime.Seasons Season(ref CampaignTime t, bool econ)
        {
            if (!Live(econ)) return VanillaSeason(ref t);
            return (CampaignTime.Seasons)SegAt(t.ToDays).Season;
        }

        private static int Day(ref CampaignTime t, bool econ)
        {
            if (!Live(econ)) return VanillaDay(ref t);
            double d = t.ToDays;
            return Math.Max(0, (int)Math.Floor(d - SegAt(d).Start));
        }

        /// <summary>Pora + ulamek pory (0..4) - zastepuje ToSeasons w pogodzie:
        /// "IEEERemainder(x,1)" to postep w porze, "x % 4" to postep w roku.</summary>
        private static double Seasons(ref CampaignTime t, bool econ)
        {
            if (!Live(econ)) return t.ToSeasons;
            double d = t.ToDays;
            var g = SegAt(d);
            double f = g.Len > 0 ? (d - g.Start) / g.Len : 0;
            if (f < 0) f = 0; if (f > 0.9999) f = 0.9999;
            return g.Season + f;
        }

        // wywolania wstawiane przez transpiler (ten sam stos co getter: adres struktury)
        public static CampaignTime.Seasons SkySeason(ref CampaignTime t) { try { return Season(ref t, false); } catch { return VanillaSeason(ref t); } }
        public static int SkyDay(ref CampaignTime t) { try { return Day(ref t, false); } catch { return VanillaDay(ref t); } }
        public static double SkySeasons(ref CampaignTime t) { try { return Seasons(ref t, false); } catch { return t.ToSeasons; } }
        public static int SkyDaysInSeason() { try { if (Live(false)) { var n = CampaignTime.Now; var g = SegAt(n.ToDays); return Math.Max(1, (int)g.Len); } } catch { } return CampaignTime.DaysInSeason; }

        public static CampaignTime.Seasons EconSeason(ref CampaignTime t) { try { return Season(ref t, true); } catch { return VanillaSeason(ref t); } }
        public static int EconDay(ref CampaignTime t) { try { return Day(ref t, true); } catch { return VanillaDay(ref t); } }
        public static double EconSeasons(ref CampaignTime t) { try { return Seasons(ref t, true); } catch { return t.ToSeasons; } }
        public static int EconDaysInSeason() { try { if (Live(true)) { var n = CampaignTime.Now; var g = SegAt(n.ToDays); return Math.Max(1, (int)g.Len); } } catch { } return CampaignTime.DaysInSeason; }

        // BetterEconomy.Core.SeasonClock (21-dniowe pory BEE; enum w tej samej kolejnosci co vanilla)
        public static int BeeSeason()
        {
            try { if (Live(true)) return SegAt(NowDays()).Season; } catch { }
            try { int n = (int)CampaignTime.Now.ToDays / 21 % 4; return n < 0 ? n + 4 : n; } catch { return 1; }
        }
        public static int BeeDay()
        {
            try { if (Live(true)) { double d = NowDays(); return Math.Max(0, (int)Math.Floor(d - SegAt(d).Start)); } } catch { }
            try { int n = (int)CampaignTime.Now.ToDays % 21; return n < 0 ? n + 21 : n; } catch { return 0; }
        }

        /// <summary>Do uzytku w Armoury (WinterBite).</summary>
        internal static CampaignTime.Seasons Now() { var t = CampaignTime.Now; return EconSeason(ref t); }

        /// <summary>E4/0 (pomiar, tylko odczyt): pora kalendarza Westeros teraz, dzien pory (od 1) i jej dlugosc w dniach; false - klimat bez stanu
        /// (niczego nie inicjuje - Ready() wola tylko Daily).</summary>
        internal static bool SeasonInfo(out int season, out int day, out int len)
        {
            season = 0; day = 0; len = 0;
            try
            {
                if (_segs.Count == 0) return false;
                double now = NowDays();
                var g = SegAt(now);
                season = g.Season; day = (int)Math.Floor(now - g.Start) + 1; len = (int)Math.Round(g.Len);
                return true;
            }
            catch { return false; }
        }

        // ------------------------------------------------------------ data
        /// <summary>
        /// Pora roku do TEKSTU daty (CalendarText): klimat Westeros, gdy czynny (-1 = data sprzed pierwszego odcinka pory - tekst bez pory);
        /// klimat nieczynny albo bez stanu = pora kalendarza gry. Sam odczyt (start klimatu przez Ready - jak dotad w ToStringPrefix).
        /// </summary>
        internal static int TextSeason(ref CampaignTime t)
        {
            try
            {
                if (!Live(false)) return (int)VanillaSeason(ref t);
                double d = t.ToDays;
                if (d < _segs[0].Start) return -1;
                return SegAt(d).Season;
            }
            catch { return (int)VanillaSeason(ref t); }
        }

        /// <summary>
        /// Numer dnia pory wedle wzoru SPRZED 13.10 (pelne doby od poczatku odcinka pory: floor(d - Start) + 1) - tylko do linii dowodu
        /// "Data na pasku (kalendarz) ... stary wzor dawal M". 0 = klimat bez stanu albo data sprzed historii. Niczego nie inicjuje.
        /// </summary>
        internal static int OldTextDay(ref CampaignTime t)
        {
            try
            {
                if (_segs.Count == 0) return 0;
                double d = t.ToDays;
                if (d < _segs[0].Start) return 0;
                return (int)Math.Floor(d - SegAt(d).Start) + 1;
            }
            catch { return 0; }
        }

        public static bool ToStringPrefix(ref CampaignTime __instance, ref string __result)
        {
            try
            {
                // 13.10 (Jeff): czytelna data "Year 299, Day 256, Autumn" - rok, dzien roku z tickow zegara (zmiana o 0:00) i pora klimatu
                if (CalendarText.On) { __result = CalendarText.Date(__instance, true); return false; }
                if (!Live(false)) return true;
                double d = __instance.ToDays;
                int year = __instance.GetYear;
                if (d < _segs[0].Start)
                {
                    __result = year + " AC";            // przed znana historia (urodziny itp.) - sam rok
                    return false;
                }
                var g = SegAt(d);
                // 13.10 (Jeff: "dzien zmienia sie dopiero kolo 10-12"): g.Start ma ulamek doby = godzine pierwszego odczytu klimatu (nowa kampania
                // ROT: ok. 5:52), wiec floor(d - g.Start) zmienial numer o tej godzinie. Liczymy polnoce zegara: doba kalendarza daty minus
                // doba kalendarza poczatku pory. O godzinie startu pory numer jak dotad; od polnocy do tamtej godziny o 1 wiekszy niz dawal stary wzor.
                int day = (int)(Math.Floor(d) - Math.Floor(g.Start)) + 1;
                string season = GameTexts.FindText("str_season_" + (CampaignTime.Seasons)g.Season).ToString();
                __result = "Day " + day + " of " + season + ", " + year + " AC";
                return false;
            }
            catch { return true; }
        }

        // ------------------------------------------------------------ transpilery
        private static MethodInfo _gSeason, _gDay, _gSeasons, _gDis, _beeCur, _beeDay, _beeProd, _beeBias, _beeCar;

        public static IEnumerable<CodeInstruction> SkyTranspiler(IEnumerable<CodeInstruction> instructions) { return Swap(instructions, false); }
        public static IEnumerable<CodeInstruction> EconTranspiler(IEnumerable<CodeInstruction> instructions) { return Swap(instructions, true); }

        private static IEnumerable<CodeInstruction> Swap(IEnumerable<CodeInstruction> instructions, bool econ)
        {
            var t = typeof(WesterosClimate);
            foreach (var ci in instructions)
            {
                var m = ci.operand as MethodInfo;
                if (m != null && (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt))
                {
                    string rep = null;
                    if (m == _gSeason) rep = econ ? nameof(EconSeason) : nameof(SkySeason);
                    else if (m == _gDay) rep = econ ? nameof(EconDay) : nameof(SkyDay);
                    else if (m == _gSeasons) rep = econ ? nameof(EconSeasons) : nameof(SkySeasons);
                    else if (m == _gDis) rep = econ ? nameof(EconDaysInSeason) : nameof(SkyDaysInSeason);
                    else if (_beeCur != null && m == _beeCur) rep = nameof(BeeSeason);
                    else if (_beeDay != null && m == _beeDay) rep = nameof(BeeDay);
                    else if (_beeProd != null && m == _beeProd) { ci.opcode = OpCodes.Call; ci.operand = AccessTools.Method(typeof(WinterSource), nameof(WinterSource.ProdMult)); }
                    else if (_beeBias != null && m == _beeBias) { ci.opcode = OpCodes.Call; ci.operand = AccessTools.Method(typeof(WinterSource), nameof(WinterSource.FoodBias)); }
                    else if (_beeCar != null && m == _beeCar) { ci.opcode = OpCodes.Call; ci.operand = AccessTools.Method(typeof(WinterSource), nameof(WinterSource.CaravanMult)); }
                    if (rep != null)
                    {
                        ci.opcode = OpCodes.Call;
                        ci.operand = AccessTools.Method(t, rep);
                    }
                }
                yield return ci;
            }
        }

        private static readonly string[] SkyTypes =
        {
            "TaleWorlds.CampaignSystem.GameComponents.DefaultMapWeatherModel",
            "SandBox.View.Map.Managers.MapAudioManager",
            "TaleWorlds.CampaignSystem.SceneInformationPopupTypes.CampaignSceneNotificationHelper",
        };

        private static readonly string[] EconTypes =
        {
            "TaleWorlds.CampaignSystem.CampaignBehaviors.IncidentsCampaignBehaviour",
            "RealisticBannerlord.Systems.Demographics.RealisticProsperityModel",
            "RealisticBannerlord.Systems.Medicine.InfectionBehavior",
            "RealisticBannerlord.Systems.Medicine.RealisticHealingModel",
            "RealisticBannerlord.Systems.Seasons.RealisticFoodConsumptionModel",
            "RealisticBannerlord.Systems.Seasons.RealisticPartyMoraleModel",
            "RealisticBannerlord.Systems.Seasons.RealisticPartySpeedModel",
            "RealisticBannerlord.Systems.Seasons.WeatherEventBehavior",
            "StrategicCampaignAI145.StrategicAiHelpers",
            "BetterEconomy.Behaviors.CaravanCampaignBehavior",
            "BetterEconomy.Behaviors.PopulationCampaignBehavior",
            "BetterEconomy.Behaviors.StatsCampaignBehavior",
            "BetterEconomy.Models.BEE_ItemPriceFactorModel",
            "BetterEconomy.Models.BEE_PartySpeedModel",
            "BetterEconomy.Models.BEE_SettlementEconomyModel",
            "BetterEconomy.Models.BEE_VillageProductionCalculatorModel",
            "BetterEconomy.UI.SettlementDetailView",
        };

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var ct = typeof(CampaignTime);
                _gSeason = AccessTools.PropertyGetter(ct, "GetSeasonOfYear");
                _gDay = AccessTools.PropertyGetter(ct, "GetDayOfSeason");
                _gSeasons = AccessTools.PropertyGetter(ct, "ToSeasons");
                _gDis = AccessTools.PropertyGetter(ct, "DaysInSeason");
                var bee = AccessTools.TypeByName("BetterEconomy.Core.SeasonClock");
                if (bee != null)
                {
                    _beeCur = AccessTools.PropertyGetter(bee, "Current");
                    _beeDay = AccessTools.PropertyGetter(bee, "DayInSeason");
                }
                var sp = AccessTools.TypeByName("BetterEconomy.Config.SeasonalityProfile");
                if (sp != null) { _beeProd = AccessTools.Method(sp, "VillageProductionMult"); _beeBias = AccessTools.Method(sp, "FoodPriceBias"); _beeCar = AccessTools.Method(sp, "CaravanSpeedMult"); }

                var ts = AccessTools.Method(ct, "ToString");
                if (ts != null) h.Patch(ts, prefix: new HarmonyMethod(typeof(WesterosClimate), nameof(ToStringPrefix)));

                int sky = PatchTypes(h, SkyTypes, nameof(SkyTranspiler), out var skyMiss);
                int econ = PatchTypes(h, EconTypes, nameof(EconTranspiler), out var econMiss);
                // 13.10: data w powiadomieniach ze scena - prefiks CalendarText.FormalPrefix (wylacznik off = oryginal z transpilerem jak dotad)
                MethodInfo formal = null;
                try
                {
                    var nh = AccessTools.TypeByName("TaleWorlds.CampaignSystem.SceneInformationPopupTypes.CampaignSceneNotificationHelper");
                    formal = nh != null ? AccessTools.Method(nh, "GetFormalDayAndSeasonText", new[] { typeof(CampaignTime) }) : null;
                    if (formal != null) h.Patch(formal, prefix: new HarmonyMethod(typeof(CalendarText), nameof(CalendarText.FormalPrefix)));
                }
                catch (Exception e) { formal = null; Log.Error("WesterosClimate.ApplyAll (data w powiadomieniach)", e); }
                Log.Info("Data (kalendarz): latka daty w powiadomieniach ze scena " + (formal != null ? "wpieta" : "BRAK GetFormalDayAndSeasonText")
                         + " (wylacznik ReadableDateEnabled czytany przy kazdym tekscie).");
                Log.Info("WesterosClimate: data " + (ts != null ? "wpieta" : "BRAK ToString")
                         + ", niebo (pogoda/dzwiek/powiadomienia) metod " + sky
                         + ", gospodarka (RBL/SCA/BEE/zdarzenia) metod " + econ
                         + (skyMiss.Count + econMiss.Count > 0 ? ", brak typow: " + string.Join(", ", skyMiss.ToArray()) + " " + string.Join(", ", econMiss.ToArray()) : "")
                         + ". BEE SeasonClock " + (bee != null ? "znaleziony" : "brak") + ".");
            }
            catch (Exception e) { Log.Error("WesterosClimate.ApplyAll", e); }
        }

        private static int PatchTypes(Harmony h, string[] names, string transpiler, out List<string> missing)
        {
            missing = new List<string>();
            int n = 0;
            var tr = new HarmonyMethod(typeof(WesterosClimate), transpiler);
            foreach (var name in names)
            {
                Type t = null;
                try { t = AccessTools.TypeByName(name); } catch { }
                if (t == null) { missing.Add(name.Substring(name.LastIndexOf('.') + 1)); continue; }
                n += PatchType(h, t, tr, 0);
            }
            return n;
        }

        private static int PatchType(Harmony h, Type t, HarmonyMethod tr, int depth)
        {
            int n = 0;
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            MethodInfo[] ms;
            try { ms = t.GetMethods(all); } catch { return 0; }
            foreach (var m in ms)
            {
                try
                {
                    if (m.IsAbstract || m.ContainsGenericParameters || m.GetMethodBody() == null) continue;
                    if (!Reads(m)) continue;
                    h.Patch(m, transpiler: tr);
                    n++;
                }
                catch (Exception e) { Log.Info("WesterosClimate: nie wpieto " + t.Name + "." + m.Name + ": " + e.Message); }
            }
            if (depth < 3)
            {
                Type[] nested;
                try { nested = t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic); } catch { nested = new Type[0]; }
                foreach (var nt in nested)
                    if (!nt.ContainsGenericParameters) n += PatchType(h, nt, tr, depth + 1);
            }
            return n;
        }

        private static bool Reads(MethodInfo m)
        {
            foreach (var kv in PatchProcessor.ReadMethodBody(m))
            {
                var op = kv.Value as MethodInfo;
                if (op == null) continue;
                if (op == _gSeason || op == _gDay || op == _gSeasons || op == _gDis
                    || (_beeCur != null && op == _beeCur) || (_beeDay != null && op == _beeDay)
                    || (_beeProd != null && op == _beeProd) || (_beeBias != null && op == _beeBias) || (_beeCar != null && op == _beeCar)) return true;
            }
            return false;
        }
    }
}
