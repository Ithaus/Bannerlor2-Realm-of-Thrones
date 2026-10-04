using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// DOCHOD Z LENN OD LUDNOSCI (Jeff 04.10: "to nie jest jedna wioska, to symbol - trzeba liczyc
    /// po ludnosci, ktora podalem dla kazdego krolestwa/regionu"). Ludnosc krain z
    /// docs/POPULACJA-WESTEROS-ESSOS.md (moje estymacje, fan-estymacje dla Essos z zapleczem).
    ///
    /// Dzis gra daje 2-26 pensow na mieszkanca rocznie (Reach najmniej, Zelazne Wyspy najwiecej),
    /// bo liczy od liczby osad. Historycznie panowie i korona brali ok. 40 pensow na glowe rocznie
    /// (Anglia ok. 1300: ~200-240 d PKB na glowe - ESTYMACJA).
    ///
    /// Raz na kampanie (zapis w save) dla kazdej krainy (kultura osady) liczymy: ilu ludzi na punkt
    /// hearth wsi i ilu na punkt dobrobytu miasta, tak zeby suma dala ludnosc krainy (czesc miejska
    /// wedle udzialu miast). Potem wies ma hearth x k ludzi - spalona wies ma mniej ludzi i daje mniej,
    /// rosnaca daje wiecej. Dochod: podatek miasta (CalculateTownTax) i dochod wsi BK
    /// (CalculateVillageTaxFromIncome) = ludzie x `PopulationRentPerHead` / dni roku. Zamki bez zmian
    /// (ich dochod to wsie). Cla od handlu bez zmian.
    /// </summary>
    internal static class PopulationLaw
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.PopulationRentEnabled; } }

        private sealed class Region { public float Pop; public float Urban; public Region(float p, float u) { Pop = p; Urban = u; } }

        // kultura osady ROT -> ludnosc krainy i udzial miast (ESTYMACJE, docs/POPULACJA-WESTEROS-ESSOS.md)
        private static readonly Dictionary<string, Region> Table = new Dictionary<string, Region>
        {
            { "battania",   new Region(3000000f, 0.08f) },  // Polnoc
            { "river",      new Region(3500000f, 0.08f) },  // Dorzecze
            { "reach",      new Region(8000000f, 0.10f) },  // Reach (Oldtown)
            { "aserai",     new Region(1500000f, 0.08f) },  // Dorne
            { "vlandia",    new Region(4000000f, 0.08f) },  // Westerlands (Lannisport)
            { "vale",       new Region(3000000f, 0.06f) },  // Dolina
            { "stormlands", new Region(2500000f, 0.05f) },  // Krainy Burzy
            { "sturgia",    new Region(500000f,  0.05f) },  // Zelazne Wyspy
            { "crownlands", new Region(600000f,  0.75f) },  // Krolewska Przystan i Duskendale
            { "dragonstone",new Region(850000f,  0.05f) },  // reszta Ziem Korony
            { "freefolk",   new Region(150000f,  0f) },
            { "nightswatch",new Region(20000f,   0f) },
            { "skagosi",    new Region(50000f,   0f) },
            { "volantine",  new Region(5000000f, 0.25f) },
            { "empire",     new Region(2500000f, 0.32f) },  // Braavos
            { "norvos",     new Region(1500000f, 0.40f) },
            { "pentoshi",   new Region(1500000f, 0.33f) },
            { "qohorik",    new Region(1500000f, 0.33f) },
            { "tyroshi",    new Region(1000000f, 0.40f) },
            { "myrish",     new Region(1000000f, 0.40f) },
            { "lyseni",     new Region(1000000f, 0.40f) },
            { "nord",       new Region(800000f,  0.31f) },  // Lorath
            { "ghiscari",   new Region(3000000f, 0.40f) },  // Zatoka Niewolnicza
            { "qartheen",   new Region(4000000f, 0.37f) },
            { "khuzait",    new Region(750000f,  0f) },     // Dothrakowie
            { "sarnor",     new Region(100000f,  0.20f) },
            { "ibbenese",   new Region(350000f,  0.20f) },
            { "summer",     new Region(750000f,  0.10f) },
            { "yiti",       new Region(100000f,  0.20f) },
            { "valyrian",   new Region(50000f,   0f) },
        };

        // kultura -> [ludzi na punkt hearth wsi, ludzi na punkt dobrobytu miasta]
        private static readonly Dictionary<string, float[]> _k = new Dictionary<string, float[]>();

        internal static void Reset() { _k.Clear(); RentToday.Clear(); }

        private static void Calibrate()
        {
            if (_k.Count > 0) return;
            float scale = Math.Max(0f, Settings.Current.PopulationScale);
            var hearth = new Dictionary<string, float>();
            var prosp = new Dictionary<string, float>();
            foreach (var s in Settlement.All)
            {
                if (s == null || s.Culture == null) continue;
                string c = s.Culture.StringId;
                if (!Table.ContainsKey(c)) continue;
                if (s.IsVillage && s.Village != null) { float v; hearth.TryGetValue(c, out v); hearth[c] = v + Math.Max(1f, s.Village.Hearth); }
                else if (s.IsTown && s.Town != null) { float v; prosp.TryGetValue(c, out v); prosp[c] = v + Math.Max(1f, s.Town.Prosperity); }
            }
            var parts = new List<string>();
            foreach (var kv in Table)
            {
                float h, p;
                hearth.TryGetValue(kv.Key, out h); prosp.TryGetValue(kv.Key, out p);
                float pop = kv.Value.Pop * scale;
                float urban = p > 0f ? kv.Value.Urban : 0f;
                if (h <= 0f) urban = p > 0f ? 1f : 0f;                      // kraina bez wsi - wszystko w miastach
                float kv0 = h > 0f ? pop * (1f - urban) / h : 0f;
                float kv1 = p > 0f ? pop * urban / p : 0f;
                _k[kv.Key] = new[] { kv0, kv1 };
                parts.Add(kv.Key + " " + (pop / 1e6f).ToString("0.00", CultureInfo.InvariantCulture) + "M (wies " + (int)kv0 + "/hearth, miasto " + (int)kv1 + "/dobrobyt)");
            }
            Log.Info("PopulationLaw: kalibracja ludnosci - " + string.Join("; ", parts.ToArray()) + ".");
        }

        internal static float PeopleOf(Settlement s)
        {
            if (s == null || s.Culture == null) return 0f;
            Calibrate();
            float[] k;
            if (!_k.TryGetValue(s.Culture.StringId, out k)) return 0f;
            if (s.IsVillage && s.Village != null) return Math.Max(0f, s.Village.Hearth) * k[0];
            if (s.IsTown && s.Town != null) return Math.Max(0f, s.Town.Prosperity) * k[1];
            return 0f;
        }

        private static float DailyRent(float people)
        {
            int days = Math.Max(28, CampaignTime.DaysInYear);
            return people * Math.Max(0f, Settings.Current.PopulationRentPerHead) / days;
        }

        // ------------------------------------------------------------ renty jako przeplyw
        // Audyt 04.10: podmiana podatku w modelach (a) nie wpinala sie (BK ma dwie wersje
        // CalculateVillageTaxFromIncome - AmbiguousMatch wywracal cale ApplyAll), (b) bylaby zlotem
        // z niczego (~7 mln/dzien). Teraz renta to PRZEPLYW: raz dziennie pan bierze z kasy wsi/miasta
        // to, co mu sie nalezy wedle ludnosci, najwyzej `PopulationRentMaxShare` tego, co osada ma.
        // Podatki gry (BK/vanilla) zostaja, jak byly. Spalona albo lupiona wies nie placi.
        internal static void ApplyAll(Harmony h)
        {
            Log.Info("PopulationLaw: renty od ludnosci jako przeplyw z kasy osad do panow " + (On ? "CZYNNE" : "wylaczone w MCM") + " (bez latek modeli podatku).");
        }

        /// <summary>Renty zaplacone dzis kazdemu rodowi (do powinnosci wobec korony).</summary>
        internal static readonly Dictionary<Clan, int> RentToday = new Dictionary<Clan, int>();

        // ------------------------------------------------------------ podatek ludnosci miasta BK -> renta (wpis 49)
        [ThreadStatic] private static int _taxDepth;
        public static void TownTaxPrefix() { _taxDepth++; }
        public static Exception TownTaxFinalizer(Exception __exception) { if (_taxDepth > 0) _taxDepth--; return __exception; }
        public static void TownTaxPostfix(bool __1, ref TaleWorlds.CampaignSystem.ExplainedNumber __result)
        {
            if (_taxDepth > 1) return;
            try
            {
                var s = Settings.Current;
                if (s == null || !On || !s.RentReplacesTownTax) return;
                __result = new TaleWorlds.CampaignSystem.ExplainedNumber(0f, __1, _txtRent);
            }
            catch { }
        }
        private static readonly TaleWorlds.Localization.TextObject _txtRent = new TaleWorlds.Localization.TextObject("{=!}Town rents are paid from the town purse (see daily rents)");

        internal static void ApplyTownTax(HarmonyLib.Harmony h)
        {
            int n = 0;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch (System.Reflection.ReflectionTypeLoadException e) { types = e.Types; } catch { continue; }
                foreach (var t in types)
                {
                    try
                    {
                        if (t == null || t.IsAbstract || !typeof(TaleWorlds.CampaignSystem.ComponentInterfaces.SettlementTaxModel).IsAssignableFrom(t)) continue;
                        foreach (var m in t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
                        {
                            if (m.Name != "CalculateTownTax" || m.IsAbstract) continue;
                            var ps = m.GetParameters();
                            if (ps.Length != 2 || ps[0].ParameterType != typeof(Town) || ps[1].ParameterType != typeof(bool)) continue;
                            h.Patch(m, prefix: new HarmonyLib.HarmonyMethod(typeof(PopulationLaw), nameof(TownTaxPrefix)) { priority = HarmonyLib.Priority.First },
                                       postfix: new HarmonyLib.HarmonyMethod(typeof(PopulationLaw), nameof(TownTaxPostfix)) { priority = HarmonyLib.Priority.Last },
                                       finalizer: new HarmonyLib.HarmonyMethod(typeof(PopulationLaw), nameof(TownTaxFinalizer)));
                            n++;
                        }
                    }
                    catch { }
                }
            }
            Log.Info("PopulationLaw: podatek ludnosci miasta (Walled Demesnes) zastapiony renta z kasy miasta w " + n + " modelach podatku.");
        }

        internal static void Daily()
        {
            if (!On) return;
            try
            {
                var s = Settings.Current;
                float share = Math.Max(0f, Math.Min(1f, s.PopulationRentMaxShare));
                var pop = new Dictionary<string, float>();
                var due = new Dictionary<string, float>();
                var paid = new Dictionary<string, float>();
                RentToday.Clear();
                long totalPaid = 0, totalDue = 0;
                foreach (var st in Settlement.All)
                {
                    if (st == null || st.Culture == null || !(st.IsVillage || st.IsTown)) continue;
                    float p = PeopleOf(st);
                    if (p <= 0f) continue;
                    string c = st.Culture.StringId;
                    float v; pop.TryGetValue(c, out v); pop[c] = v + p;
                    float rent = DailyRent(p);
                    due.TryGetValue(c, out v); due[c] = v + rent;
                    totalDue += (long)rent;
                    try
                    {
                        if (st.IsVillage && (st.Village.VillageState == Village.VillageStates.Looted || st.Village.VillageState == Village.VillageStates.BeingRaided)) continue;
                        var lord = st.OwnerClan != null ? st.OwnerClan.Leader : null;
                        if (lord == null || !lord.IsAlive) continue;
                        int gold = st.SettlementComponent != null ? st.SettlementComponent.Gold : 0;
                        // Wpis 49 (Jeff 04.10: "tak" - jedno zrodlo dochodu z ziemi): podatek ludnosci miasta BK ("Walled Demesnes",
                        // z niczego) wylaczony (TownTaxPostfix); pan bierze z MIASTA czesc kasy ponad prog bogactwa kupcow
                        // (BK BKProsperityModel: kasa < 20 000 = do -2 dobrobytu dziennie) - miasto nie bankrutuje i nie traci dobrobytu.
                        // Wies placi czesc swojej kiesy (PopulationRentMaxShare, 0.2 - wczesniej 0.5 oproznialo wsie w kilka dni).
                        float takeShare = share;
                        if (st.IsTown && st.Town != null)
                        {
                            gold = Math.Max(0, gold - (int)Math.Max(0f, s.TownRentFloorGold));
                            takeShare = Math.Max(0f, Math.Min(1f, s.TownRentShare));
                        }
                        int pay = (int)Math.Min(rent, gold * takeShare);
                        if (pay <= 0) continue;
                        GiveGoldAction.ApplyForSettlementToCharacter(st, lord, pay, true);
                        { int r0; RentToday.TryGetValue(st.OwnerClan, out r0); RentToday[st.OwnerClan] = r0 + pay; }
                        paid.TryGetValue(c, out v); paid[c] = v + pay;
                        totalPaid += pay;
                    }
                    catch { }
                }
                var parts = pop.OrderByDescending(kv => kv.Value).Select(kv =>
                {
                    float pd; paid.TryGetValue(kv.Key, out pd);
                    return kv.Key + " " + (kv.Value / 1e6f).ToString("0.00", CultureInfo.InvariantCulture) + "M " + (int)pd + "/" + (int)due[kv.Key];
                });
                Log.Info("Ludnosc: dzien " + (int)CampaignTime.Now.ToDays + " | " + (pop.Values.Sum() / 1e6f).ToString("0.0", CultureInfo.InvariantCulture)
                         + " mln | renty zaplacone " + totalPaid + " z naleznych " + totalDue + " zl (z kasy osad) | kraina ludnosc zaplacone/nalezne: " + string.Join(", ", parts.ToArray()) + ".");
            }
            catch (Exception e) { Log.Error("PopulationLaw.Daily", e); }
        }

        internal static string Export()
        {
            if (_k.Count == 0) return "";
            var sb = new StringBuilder("v1");
            foreach (var kv in _k)
                sb.Append('|').Append(kv.Key).Append('=').Append(kv.Value[0].ToString("R", CultureInfo.InvariantCulture))
                  .Append(':').Append(kv.Value[1].ToString("R", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        internal static void Import(string data)
        {
            try
            {
                _k.Clear();
                if (string.IsNullOrEmpty(data)) return;     // stary save - kalibracja z obecnego stanu swiata
                foreach (var part in data.Split('|').Skip(1))
                {
                    int eq = part.IndexOf('='); int col = part.LastIndexOf(':');
                    if (eq <= 0 || col <= eq) continue;
                    float a, b;
                    if (float.TryParse(part.Substring(eq + 1, col - eq - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out a)
                        && float.TryParse(part.Substring(col + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out b))
                        _k[part.Substring(0, eq)] = new[] { a, b };
                }
            }
            catch (Exception e) { Log.Error("PopulationLaw.Import", e); }
        }
    }
}
