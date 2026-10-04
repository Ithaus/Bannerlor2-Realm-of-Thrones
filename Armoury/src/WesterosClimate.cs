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
    /// jej koniec bialym krukiem. Data to "Day N of Summer, 299 AC".
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

        internal static bool On { get { var s = Settings.Current; return s != null && s.ClimateEnabled; } }
        private static bool EconOn { get { var s = Settings.Current; return s != null && s.ClimateEnabled && s.ClimateDrivesEconomy; } }

        internal static void Reset() { _segs.Clear(); _initBroken = false; }

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
                double left = Rand(s.ClimateSummerDaysLeftMin, s.ClimateSummerDaysLeftMax);
                _segs.Add(new Seg { Start = now - soFar, Season = (int)CampaignTime.Seasons.Summer, Len = soFar + left });
                Log.Info("Klimat: start - lato trwa juz " + (int)soFar + " dni, skonczy sie za " + (int)left
                         + " dni (dzien kampanii " + (int)now + ").");
                return true;
            }
            catch (Exception e) { _initBroken = true; Log.Error("WesterosClimate.Init", e); return false; }
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
                return Rand(s.ClimateFirstAutumnDaysMin, s.ClimateFirstAutumnDaysMax);
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

        // ------------------------------------------------------------ data
        public static bool ToStringPrefix(ref CampaignTime __instance, ref string __result)
        {
            try
            {
                if (!Live(false)) return true;
                double d = __instance.ToDays;
                int year = __instance.GetYear;
                if (d < _segs[0].Start)
                {
                    __result = year + " AC";            // przed znana historia (urodziny itp.) - sam rok
                    return false;
                }
                var g = SegAt(d);
                int day = (int)Math.Floor(d - g.Start) + 1;
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
