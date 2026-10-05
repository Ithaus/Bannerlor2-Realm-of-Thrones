using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// BUDOWY ZA PIENIADZE (wpis 73; Jeff 05.10: "budowle ... nie wymagaja kasy, a powinny wymagac kasy tak jak materialow";
    /// "10% dochodu dziennie, w wojnie 0, chyba ze mury - tak"; "osobno [od skarbca krola] i dzialaj").
    /// Dziennik budow (wpis 69): 209 z 227 osad z moca 0 - BK liczy budowe z sily roboczej tylko przy polityce "Construction",
    /// robocizna byla darmowa, materialy BK za grosze. Historycznie pan najmowal murarzy, ciesli i robotnikow za dniowke i kupowal
    /// kamien, wapno, drewno; budowa szla tak szybko, jak pieniadze (docs/AUDYT-BUDOWY-SKARBIEC.md).
    /// Codziennie, dla kazdego miasta i zamku z budowa: pan placi BuildIncomeShare (10%) swojego dziennego dochodu (model finansow
    /// rodu + renty), dzielone na jego lenna z budowa; w wojnie tylko budowle wojskowe (mury, wieze, koszary). 25% na materialy
    /// kupione z targu (wapien, drewno, glina, narzedzia, marmur - do kasy miasta), 75% na place i wozy (do kasy osady) - bez
    /// materialow nie ma za co pracowac (najwyzej 3 pensy pracy na pens materialu). Punkty budowy = zaplacone / cena punktu
    /// (wojskowe 48 d, cywilne 24 d). Moc budowy gry (BK, vanilla) zastapiona oplacona praca; materialy BK wylaczone.
    /// </summary>
    internal static class BuildFunding
    {
        private static readonly Dictionary<Town, float> _funded = new Dictionary<Town, float>();
        private static readonly TextObject _txt = new TextObject("{=!}Paid works (wages and materials from the owner's purse)");
        private static readonly string[] MatIds = { "limestone", "hardwood", "clay", "tools", "marble", "planks" };
        internal static bool On { get { var s = Settings.Current; return s != null && s.PaidConstruction; } }

        internal static void Reset() { _funded.Clear(); }

        // ------------------------------------------------------------ moc budowy = oplacona praca
        [ThreadStatic] private static int _depth;
        public static void PowerPrefix() { _depth++; }
        public static Exception PowerFinalizer(Exception __exception) { if (_depth > 0) _depth--; return __exception; }
        public static void PowerPostfix(Town town, bool includeDescriptions, ref ExplainedNumber __result)
        {
            if (_depth > 1 || !On || town == null) return;
            try
            {
                float f; _funded.TryGetValue(town, out f);
                __result = new ExplainedNumber(f, includeDescriptions, _txt);
            }
            catch { }
        }
        public static void PowerIntPostfix(Town town, ref int __result)
        {
            if (_depth > 1 || !On || town == null) return;
            float f; _funded.TryGetValue(town, out f); __result = (int)f;
        }

        // ------------------------------------------------------------ materialy BK wylaczone
        public static bool SkipIfOn() { return !On; }
        public static void EmptyMaterials(ref List<(ItemObject, int)> __result) { if (On) __result = new List<(ItemObject, int)>(); }

        // ------------------------------------------------------------ codzienne rozliczenie
        private static int _paidTowns, _stalledNoMat, _warSkipped; private static long _spent, _toPurses, _toMarkets; private static double _points;

        internal static void Daily()
        {
            if (!On || Campaign.Current == null) return;
            _funded.Clear();
            _paidTowns = _stalledNoMat = _warSkipped = 0; _spent = _toPurses = _toMarkets = 0; _points = 0;
            try
            {
                var s = Settings.Current;
                // ile lenn z budowa ma kazdy rod
                var count = new Dictionary<Clan, int>();
                var jobs = new List<KeyValuePair<Settlement, TaleWorlds.CampaignSystem.Settlements.Buildings.Building>>();
                foreach (var st in Settlement.All)
                {
                    if (st == null || st.Town == null || (!st.IsTown && !st.IsCastle) || st.OwnerClan == null || st.IsUnderSiege) continue;
                    var q = st.Town.BuildingsInProgress;
                    var b = q != null && q.Count > 0 ? q.Peek() : null;
                    if (b == null || b.BuildingType == null || b.BuildingType.IsDailyProject) continue;
                    if (st.OwnerClan == Clan.PlayerClan && !s.PaidConstructionPlayer) continue;
                    jobs.Add(new KeyValuePair<Settlement, TaleWorlds.CampaignSystem.Settlements.Buildings.Building>(st, b));
                    int n; count.TryGetValue(st.OwnerClan, out n); count[st.OwnerClan] = n + 1;
                }
                var income = new Dictionary<Clan, float>();
                var model = Campaign.Current.Models.ClanFinanceModel;
                foreach (var kv in jobs)
                {
                    var st = kv.Key; var b = kv.Value; var clan = st.OwnerClan; var lord = clan.Leader;
                    if (lord == null || !lord.IsAlive) continue;
                    bool military = b.BuildingType.IsMilitaryProject;
                    bool war = false;
                    try { if (clan.Kingdom != null) foreach (var k in Kingdom.All) if (k != clan.Kingdom && !k.IsEliminated && clan.Kingdom.IsAtWarWith(k)) { war = true; break; } } catch { }
                    if (war && !military) { _warSkipped++; continue; }
                    float inc;
                    if (!income.TryGetValue(clan, out inc))
                    {
                        try { inc = model.CalculateClanIncome(clan, false, false, false).ResultNumber; } catch { inc = 0f; }
                        int rent; PopulationLaw.RentToday.TryGetValue(clan, out rent); inc += rent;
                        income[clan] = inc;
                    }
                    float budget = Math.Max(0f, inc) * Math.Max(0f, s.BuildIncomeShare) / Math.Max(1, count[clan]);
                    budget = Math.Min(budget, Math.Max(0, lord.Gold));
                    if (budget < 1f) continue;
                    float ppp = military ? Math.Max(1f, s.BuildPencePerPointMilitary) : Math.Max(1f, s.BuildPencePerPointCivil);
                    // materialy z targu (miasto - swoj, zamek - najblizsze miasto)
                    var market = st.IsTown ? st : NearestTown(st);
                    float matBudget = budget * MBMath.ClampFloat(s.BuildMaterialShare, 0f, 1f);
                    int matSpent = BuyMaterials(market, matBudget);
                    if (matBudget > 0f && matSpent <= 0) { _stalledNoMat++; continue; }   // nie ma z czego budowac
                    float labour = Math.Min(budget - matBudget, matSpent * Math.Max(0f, (1f - s.BuildMaterialShare) / Math.Max(0.01f, s.BuildMaterialShare)));
                    int labourI = MBRandom.RoundRandomized(labour);
                    lord.ChangeHeroGold(-(matSpent + labourI));
                    if (market != null && market.Town != null) market.Town.ChangeGold(matSpent);
                    st.Town.ChangeGold(labourI);                 // place murarzy, robotnikow, woznic - do kasy osady
                    float pts = (matSpent + labourI) / ppp;
                    _funded[st.Town] = pts;
                    _paidTowns++; _spent += matSpent + labourI; _toMarkets += matSpent; _toPurses += labourI; _points += pts;
                }
            }
            catch (Exception e) { Log.Error("BuildFunding", e); }
            Log.Info("Budowy oplacone: dzien " + (int)CampaignTime.Now.ToDays + " - osad " + _paidTowns + ", wydano " + _spent + " (place i wozy do kas osad " + _toPurses
                     + ", materialy z targow " + _toMarkets + "), punktow budowy " + (int)_points + "; wstrzymane: brak materialow " + _stalledNoMat + ", wojna (budowle cywilne) " + _warSkipped + ".");
        }

        private static int BuyMaterials(Settlement market, float budget)
        {
            if (market == null || market.Town == null || market.ItemRoster == null || budget < 1f) return 0;
            int spent = 0;
            var roster = market.ItemRoster;
            for (int pass = 0; pass < 3 && spent < budget; pass++)
                foreach (var id in MatIds)
                {
                    var it = MBObjectManager.Instance.GetObject<ItemObject>(id);
                    if (it == null) continue;
                    int have = roster.GetItemNumber(it);
                    if (have <= 0) continue;
                    int price = Math.Max(1, market.Town.GetItemPrice(it, null, false));
                    int n = Math.Min(have, (int)((budget - spent) / price / (3 - pass)) + (pass == 2 ? 0 : 1));
                    n = Math.Min(n, (int)((budget - spent) / price));
                    if (n <= 0) continue;
                    roster.AddToCounts(it, -n);
                    spent += n * price;
                    if (spent >= budget) break;
                }
            return spent;
        }

        private static readonly Dictionary<Settlement, Settlement> _near = new Dictionary<Settlement, Settlement>();
        private static Settlement NearestTown(Settlement st)
        {
            Settlement m;
            if (_near.TryGetValue(st, out m)) return m;
            float best = float.MaxValue;
            var p = st.GetPosition2D;
            foreach (var t in Settlement.All)
            {
                if (t == null || !t.IsTown) continue;
                float d = p.DistanceSquared(t.GetPosition2D);
                if (d < best) { best = d; m = t; }
            }
            _near[st] = m;
            return m;
        }

        internal static void ApplyAll(Harmony h)
        {
            int n = 0;
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); } catch (System.Reflection.ReflectionTypeLoadException e) { types = e.Types; } catch { continue; }
                    foreach (var t in types)
                    {
                        try
                        {
                            if (t == null || t.IsAbstract || !typeof(TaleWorlds.CampaignSystem.ComponentInterfaces.BuildingConstructionModel).IsAssignableFrom(t)) continue;
                            var m = t.GetMethod("CalculateDailyConstructionPower", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
                            if (m != null)
                            {
                                h.Patch(m, prefix: new HarmonyMethod(typeof(BuildFunding), nameof(PowerPrefix)) { priority = Priority.First },
                                           postfix: new HarmonyMethod(typeof(BuildFunding), nameof(PowerPostfix)) { priority = Priority.Last },
                                           finalizer: new HarmonyMethod(typeof(BuildFunding), nameof(PowerFinalizer)));
                                n++;
                            }
                            var m2 = t.GetMethod("CalculateDailyConstructionPowerWithoutBoost", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
                            if (m2 != null)
                                h.Patch(m2, prefix: new HarmonyMethod(typeof(BuildFunding), nameof(PowerPrefix)) { priority = Priority.First },
                                            postfix: new HarmonyMethod(typeof(BuildFunding), nameof(PowerIntPostfix)) { priority = Priority.Last },
                                            finalizer: new HarmonyMethod(typeof(BuildFunding), nameof(PowerFinalizer)));
                        }
                        catch { }
                    }
                }
                var bkb = AccessTools.TypeByName("BannerKings.Behaviours.BKBuildingsBehavior");
                int off = 0;
                if (bkb != null)
                    foreach (var name in new[] { "RunMaterials", "OnBuildingChanged" })
                    {
                        var m = AccessTools.Method(bkb, name);
                        if (m != null) { h.Patch(m, prefix: new HarmonyMethod(typeof(BuildFunding), nameof(SkipIfOn))); off++; }
                    }
                var bkm = AccessTools.TypeByName("BannerKings.Models.Vanilla.BKConstructionModel");
                var gm = bkm != null ? AccessTools.Method(bkm, "GetMaterialRequirements") : null;
                if (gm != null) { h.Patch(gm, postfix: new HarmonyMethod(typeof(BuildFunding), nameof(EmptyMaterials))); off++; }
                Log.Info("BuildFunding: budowy za pieniadze pana - moc budowy w " + n + " modelach, materialy BK wylaczone w " + off + "/3 miejscach.");
            }
            catch (Exception e) { Log.Error("BuildFunding.ApplyAll", e); }
        }
    }
}
