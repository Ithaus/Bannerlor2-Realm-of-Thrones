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
    /// (CalculateVillageTaxFromIncome) = ludzie x `PopulationRentPerHead` / dni roku. Dochod zamkow bez zmian
    /// (ich dochod to wsie). Cla od handlu bez zmian.
    /// Uwaga (projekt etapu 2 rozdz. 6): "bez zmian" dotyczy DOCHODU zamku, nie jego kasy - kase zamku sciagal do celu regulator gry
    /// (DefaultSettlementEconomyModel.GetTownGoldChange, opis w SoldierPay przy tarczy zoldu); od paczki 110 (CastlePurse) regulator zamku
    /// dziala tylko w dol (dosypka do zapasu), a nadwyzke ponad zapas kupcow zdejmuje zawor zamku - do pana (dopisane do RentToday).
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

        internal static void Reset()
        {
            _k.Clear(); RentToday.Clear(); ZeroDay169(); _kTab.Clear(); _kTabDay = -1;
            // wpis 87 (audyt pkt 4): BK tworzy nowe PopulationManager/PolicyManager przy kazdej grze - stare referencje dawaly
            // dekret podatkowy zawsze Standard i pomijaly autonomie po wczytaniu drugiego save'a bez restartu
            _bkResolved = false; _popMgr = null; _popData = null; _polResolved = false; _policyMgr = null; _getPolicy = null;
        }

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

        // ------------------------------------------------------------ 174.3: ludnosc z tabeli krain (suma swiata stala)
        // PeopleOf liczy ludzi z hearth wsi i dobrobytu przez wspolczynnik z pierwszej kalibracji (_k, w zapisie) - rosnie z hearth i dobrobytem:
        // w A171 swiat 52.5 -> 59.0 mln w 39 dob, Lotus Bay 0.75 -> 2.2 mln (audyt 09.10: "nowego modelu nie wolno na niej oprzec").
        // TablePeopleOf: ten sam wzor, ale wspolczynnik liczony co dobe z dzisiejszych sum hearth i dobrobytu kultury - suma swiata = tabela krain
        // x PopulationScale zawsze; zmienia sie tylko podzial miedzy osady. _k i PeopleOf bez zmian (renty, ksiega ludzi).
        private static readonly Dictionary<string, float[]> _kTab = new Dictionary<string, float[]>();
        private static int _kTabDay = -1;
        internal static readonly HashSet<string> WesterosCultures = new HashSet<string> { "battania", "river", "reach", "aserai", "vlandia", "vale", "stormlands", "sturgia", "crownlands", "dragonstone", "freefolk", "nightswatch", "skagosi" };

        internal static float TablePeopleOf(Settlement s)
        {
            if (s == null || s.Culture == null) return 0f;
            int day = (int)CampaignTime.Now.ToDays;
            if (day != _kTabDay || _kTab.Count == 0)
            {
                _kTab.Clear();
                float scale = Math.Max(0f, Settings.Current.PopulationScale);
                var hearth = new Dictionary<string, float>(); var prosp = new Dictionary<string, float>();
                foreach (var x in Settlement.All)
                {
                    if (x == null || x.Culture == null) continue;
                    string c = x.Culture.StringId;
                    if (!Table.ContainsKey(c)) continue;
                    if (x.IsVillage && x.Village != null) { float v; hearth.TryGetValue(c, out v); hearth[c] = v + Math.Max(1f, x.Village.Hearth); }
                    else if (x.IsTown && x.Town != null) { float v; prosp.TryGetValue(c, out v); prosp[c] = v + Math.Max(1f, x.Town.Prosperity); }
                }
                foreach (var kv in Table)
                {
                    float h, p;
                    hearth.TryGetValue(kv.Key, out h); prosp.TryGetValue(kv.Key, out p);
                    float pop = kv.Value.Pop * scale;
                    float urban = p > 0f ? kv.Value.Urban : 0f;
                    if (h <= 0f) urban = p > 0f ? 1f : 0f;
                    _kTab[kv.Key] = new[] { h > 0f ? pop * (1f - urban) / h : 0f, p > 0f ? pop * urban / p : 0f };
                }
                _kTabDay = day;
            }
            float[] k;
            if (!_kTab.TryGetValue(s.Culture.StringId, out k)) return 0f;
            if (s.IsVillage && s.Village != null) return Math.Max(1f, s.Village.Hearth) * k[0];
            if (s.IsTown && s.Town != null) return Math.Max(1f, s.Town.Prosperity) * k[1];
            return 0f;
        }

        /// <summary>174.3: ludnosc z tabeli krain razem (kontrola linii "Rece (174)").</summary>
        internal static float TableTotal()
        {
            float scale = Math.Max(0f, Settings.Current.PopulationScale), sum = 0f;
            foreach (var kv in Table) sum += kv.Value.Pop * scale;
            return sum;
        }

        /// <summary>Czy ludnosc jest juz skalibrowana (ksiega "Ludzie:" czyta PeopleOf tylko wtedy - sama kalibracji nie wywoluje).</summary>
        internal static bool Calibrated { get { return _k.Count > 0; } }

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

        /// <summary>Paczka 169 (tylko log): renty dzis - wsie i zawor miast osobno, renta wsi kazdego rodu (zajecie na sucho, rozdz. 8).
        /// Zerowane tam, gdzie RentToday, i przez MoneyLedger.ClearLast169() na poczatku bloku.</summary>
        internal static long DayVillageRent, DayTownRent;
        internal static readonly Dictionary<Clan, int> RentVillageToday = new Dictionary<Clan, int>();

        internal static void ZeroDay169() { DayVillageRent = 0; DayTownRent = 0; RentVillageToday.Clear(); _rentBy.Clear(); }

        // 169c (tylko log): renta tej doby na osade i kasa, z ktorej ja wzieto (miasto: ponad prog) - czesc "wlasne" D stalego
        private static readonly Dictionary<Settlement, long[]> _rentBy = new Dictionary<Settlement, long[]>();
        internal static void RentOf(Settlement st, out long pay, out long avail)
        {
            long[] v;
            if (st != null && _rentBy.TryGetValue(st, out v)) { pay = v[0]; avail = v[1]; } else { pay = 0; avail = 0; }
        }

        // ------------------------------------------------------------ podatek ludnosci miasta BK -> renta (wpis 49)
        [ThreadStatic] private static int _taxDepth;
        public static void TownTaxPrefix() { _taxDepth++; }
        public static Exception TownTaxFinalizer(Exception __exception) { if (_taxDepth > 0) _taxDepth--; return __exception; }
        public static void TownTaxPostfix(Town __0, bool __1, ref TaleWorlds.CampaignSystem.ExplainedNumber __result)
        {
            if (_taxDepth > 1) return;
            try
            {
                var s = Settings.Current;
                if (s == null || !On || !s.RentReplacesTownTax) return;
                // wpis 51 (ZRODLA-DOCHODU.md C1): zerujemy TYLKO podatki klas ludnosci i cla od konsumpcji (z niczego);
                // zostaje to, co ktos naprawde placi: podatek od cudzych warsztatow (BK pobiera go od wlascicieli), dochod
                // kopalni i koszt materialow budow - inaczej te pieniadze znikaly w nicosc, a pan nie placil za budowy
                float kept = KeptTownLines(__0);
                __result = new TaleWorlds.CampaignSystem.ExplainedNumber(kept, __1, _txtRent);
            }
            catch { }
        }
        private static readonly TaleWorlds.Localization.TextObject _txtRent = new TaleWorlds.Localization.TextObject("{=!}Town rents are paid from the town purse (see daily rents)");

        private static object _bkCfg; private static System.Reflection.MethodInfo _wsTax, _mining, _materials, _popData; private static Type _bldT; private static bool _bkResolved;

        /// <summary>Linie podatku miasta BK placone przez kogos (warsztaty cudzych wlascicieli, kopalnie, materialy budow) x autonomia.</summary>
        private static float KeptTownLines(Town town)
        {
            try
            {
                if (town == null) return 0f;
                if (!_bkResolved)
                {
                    _bkResolved = true;
                    var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                    _bkCfg = cfgT != null ? AccessTools.Property(cfgT, "Instance").GetValue(null, null) : null;
                    var cfm = _bkCfg != null ? AccessTools.Property(cfgT, "ClanFinanceModel").GetValue(_bkCfg, null) : null;
                    if (cfm != null) { _wsTax = AccessTools.Method(cfm.GetType(), "GetWorkshopTaxes", new[] { typeof(TaleWorlds.CampaignSystem.Settlements.Workshops.Workshop) }); _wsModel = cfm; }
                    _bldT = AccessTools.TypeByName("BannerKings.Behaviours.BKBuildingsBehavior");
                    if (_bldT != null) { _mining = AccessTools.Method(_bldT, "GetMiningRevenue", new[] { typeof(Town) }); _materials = AccessTools.Method(_bldT, "GetMaterialExpenses", new[] { typeof(Town) }); }
                    var pm = _bkCfg != null ? AccessTools.Property(cfgT, "PopulationManager").GetValue(_bkCfg, null) : null;
                    if (pm != null) { _popMgr = pm; _popData = AccessTools.Method(pm.GetType(), "GetPopData", new[] { typeof(Settlement) }); }
                }
                float sum = 0f;
                var leader = town.OwnerClan != null ? town.OwnerClan.Leader : null;
                if (_wsTax != null)
                    foreach (var w in town.Workshops)
                        if (w != null && w.Owner != null && w.Owner != leader)
                            sum += Convert.ToSingle(_wsTax.Invoke(_wsModel, new object[] { w }));
                if (_bldT != null && Campaign.Current != null)
                {
                    if (_getBeh == null) _getBeh = typeof(Campaign).GetMethod("GetCampaignBehavior").MakeGenericMethod(_bldT);
                    var get = _getBeh;
                    var beh = get.Invoke(Campaign.Current, null);
                    if (beh != null)
                    {
                        if (_mining != null) sum += Convert.ToSingle(_mining.Invoke(beh, new object[] { town }));
                        if (_materials != null) sum -= Convert.ToSingle(_materials.Invoke(beh, new object[] { town }));
                    }
                }
                if (_popData != null)
                {
                    var pd = _popData.Invoke(_popMgr, new object[] { town.Settlement });
                    if (pd != null) { float aut = Traverse.Create(pd).Property("Autonomy").GetValue<float>(); if (aut > 0f) sum *= Math.Max(0f, 1f - 0.6f * aut); }
                }
                return sum;
            }
            catch { return 0f; }
        }
        private static object _wsModel, _popMgr; private static System.Reflection.MethodInfo _getBeh;

        private static System.Reflection.MethodInfo _getPolicy; private static object _policyMgr; private static bool _polResolved;
        /// <summary>Mnoznik renty z dekretu podatkowego BK osady (1 gdy brak BK albo dekretu).</summary>
        internal static float TaxDecree(Settlement st)
        {
            try
            {
                if (!_polResolved)
                {
                    _polResolved = true;
                    var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                    var cfg = cfgT != null ? AccessTools.Property(cfgT, "Instance").GetValue(null, null) : null;
                    _policyMgr = cfg != null ? AccessTools.Property(cfgT, "PolicyManager").GetValue(cfg, null) : null;
                    if (_policyMgr != null) _getPolicy = AccessTools.Method(_policyMgr.GetType(), "GetPolicy", new[] { typeof(Settlement), typeof(string) });
                }
                if (_getPolicy == null || st == null) return 1f;
                var pol = _getPolicy.Invoke(_policyMgr, new object[] { st, "tax" });
                if (pol == null) return 1f;
                var type = Traverse.Create(pol).Property("Policy").GetValue();
                string name = type != null ? type.ToString() : "Standard";
                var s = Settings.Current;
                if (name == "Low") return Math.Max(0f, s.RentTaxLow);
                if (name == "High") return Math.Max(0f, s.RentTaxHigh);
                if (name == "Exemption") return Math.Max(0f, s.RentTaxExemption);
                return 1f;
            }
            catch { return 1f; }
        }

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
            RentToday.Clear();   // wpis 86 (audyt pkt 7): przy wylaczonych rentach nie zostaja stare kwoty w dochodzie
            ZeroDay169();        // paczka 169 (tylko log)
            if (!On) return;
            try
            {
                var s = Settings.Current;
                float share = Math.Max(0f, Math.Min(1f, s.PopulationRentMaxShare));
                var pop = new Dictionary<string, float>();
                var due = new Dictionary<string, float>();
                var paid = new Dictionary<string, float>();
                RentToday.Clear();
                ZeroDay169();
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
                        // wpis 54: dekret podatkowy lenna BK (Low/Standard/High/Exemption) steruje renta
                        float decree = TaxDecree(st);
                        int pay = (int)Math.Min(rent * decree, gold * takeShare * decree);
                        if (pay <= 0) continue;
                        GiveGoldAction.ApplyForSettlementToCharacter(st, lord, pay, true);
                        _rentBy[st] = new long[] { pay, gold };   // 169c (tylko licznik)
                        { int r0; RentToday.TryGetValue(st.OwnerClan, out r0); RentToday[st.OwnerClan] = r0 + pay; }
                        if (st.IsVillage) { DayVillageRent += pay; int v0; RentVillageToday.TryGetValue(st.OwnerClan, out v0); RentVillageToday[st.OwnerClan] = v0 + pay; }   // paczka 169 (tylko licznik)
                        else DayTownRent += pay;
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
