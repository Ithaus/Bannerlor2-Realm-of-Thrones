using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// Natywne kucie broni placi XP z ceny wyrobu (0.02 x wartosc), a ceny kutych mieczy
    /// potrafia isc w setki tysiecy. Bez sufitu jeden miecz daje wiecej nauki niz miesiac
    /// przy kowadle. Scinamy to do poziomu, ktory pasuje do naszych zbroi.
    /// </summary>
    internal static class WeaponXpPatch
    {
        internal static void Postfix(ItemObject item, ref int __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.WeaponXpFromValueCapped) return;
                if (item == null) return;
                int tier = Recipes.Grade(item);
                int cap = MathF.Max(50, s.WeaponXpCapPerTier * tier);
                if (__result > cap) __result = cap;
            }
            catch (Exception e) { Log.Error("WeaponXpPatch", e); }
        }

        private static readonly string[] Targets =
        {
            "GetSkillXpForSmithingInFreeBuildMode",
            "GetSkillXpForSmithingInCraftingOrderMode",
        };

        /// <summary>Lapiemy kazda konkretna implementacje modelu kowalstwa, takze z innych modow.</summary>
        internal static void ApplyAll(Harmony harmony)
        {
            var post = new HarmonyMethod(typeof(WeaponXpPatch).GetMethod(
                "Postfix", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public));

            int done = 0;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException rtle) { types = rtle.Types; }
                catch { continue; }

                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract || !typeof(SmithingModel).IsAssignableFrom(t)) continue;
                    foreach (var name in Targets)
                    {
                        try
                        {
                            var m = t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic |
                                                      BindingFlags.Instance | BindingFlags.DeclaredOnly);
                            if (m == null || m.IsAbstract) continue;
                            harmony.Patch(m, postfix: post);
                            done++;
                            Log.Info("Sufit XP za bron: " + t.FullName + "." + name);
                        }
                        catch (Exception e) { Log.Error("WeaponXpPatch.ApplyAll(" + t.FullName + ")", e); }
                    }
                }
            }
            if (done == 0) Log.Error("WeaponXpPatch: nie znaleziono zadnej implementacji SmithingModel.", null);
        }
    }

    /// <summary>
    /// Podloga zlomu: kupiec nigdy nie placi mniej niz X% CZYSTEJ wartosci przedmiotu.
    /// Zbita "Mangled" zbroja to wciaz metal i skora - sam material ma cene.
    /// Lapiemy KAZDA implementacje TradeItemPriceFactorModel.GetPrice (vanilla,
    /// BetterEconomy, cokolwiek), wiec podloga trzyma niezaleznie od tego,
    /// ktory mod liczy ceny i co mnozy po drodze.
    /// </summary>
    internal static class ScrapFloorPatch
    {
        internal static void Postfix(EquipmentElement itemRosterElement, bool isSelling, ref int __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || s.MinSellPercentOfValue <= 0) return;
                if (!isSelling) return;                              // tylko gdy TY sprzedajesz
                var item = itemRosterElement.Item;
                if (item == null || item.Value <= 0) return;
                int floor = (int)((float)item.Value * s.MinSellPercentOfValue / 100f);
                if (floor < 1) floor = 1;
                if (__result < floor) __result = floor;
            }
            catch (Exception e) { Log.Error("ScrapFloorPatch", e); }
        }

        internal static void ApplyAll(Harmony harmony)
        {
            var post = new HarmonyMethod(typeof(ScrapFloorPatch).GetMethod(
                "Postfix", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public));

            int done = 0;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException rtle) { types = rtle.Types; }
                catch { continue; }

                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract || !typeof(TradeItemPriceFactorModel).IsAssignableFrom(t)) continue;
                    try
                    {
                        var m = t.GetMethod("GetPrice", BindingFlags.Public | BindingFlags.NonPublic |
                                                        BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        if (m == null || m.IsAbstract) continue;
                        harmony.Patch(m, postfix: post);
                        done++;
                        Log.Info("Podloga zlomu: " + t.FullName + ".GetPrice");
                    }
                    catch (Exception e) { Log.Error("ScrapFloorPatch.ApplyAll(" + t.FullName + ")", e); }
                }
            }
            if (done == 0) Log.Error("ScrapFloorPatch: nie znaleziono zadnej implementacji TradeItemPriceFactorModel.", null);
        }
    }

    /// <summary>
    /// Uczciwy koszt zbroi w warsztacie Banner Kings:
    /// 1. MATERIALY wg reguly Jeffa - "ile daje pancerza, tyle materialu, plus tier".
    ///    BK liczyl z wagi (skorzany pancerz 44 ochrony = 1 skora, bo 5 kg / 10).
    ///    Teraz: suma punktow ochrony / ArmorPointsPerMaterial x bonus za tier,
    ///    rozlozona na wlasciwe surowce (skora+len / len / zelazo+podszewka).
    /// 2. STAWKA KUZNI - BK slusznie liczy za godzine przy kowadle (50/h bazowo
    ///    + prosperity + tier klanu), ale za slono; BkForgeHourlyMultiplier tnie stawke.
    /// </summary>
    internal static class TrueArmourCost
    {
        internal static void MaterialsPostfix(ItemObject item, ref int[] __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.BkTrueMaterials) return;
                if (item == null || __result == null || __result.Length < 11) return;

                // STRZELECKIE w zakladce CRAFT Banner Kings (widok 3D jak przy
                // pancerzach): kwit bierzemy z naszych receptur luczarskich
                // i przekladamy na tablice BK (0-5 zelaza, 6 drewno, 7 wegiel,
                // 9 skora, 10 len)
                if (item.ItemType == ItemObject.ItemTypeEnum.Bow || item.ItemType == ItemObject.ItemTypeEnum.Crossbow
                    || item.ItemType == ItemObject.ItemTypeEnum.Arrows || item.ItemType == ItemObject.ItemTypeEnum.Bolts)
                {
                    var rr = Recipes.For(item);
                    Clear(__result);
                    foreach (var part in rr.Parts)
                    {
                        if (part.Item == null || part.Count <= 0) continue;
                        int idx = BkMaterialIndex(part.Item);
                        if (idx >= 0 && idx < 11) __result[idx] += part.Count;
                    }
                    return;
                }

                if (!item.HasArmorComponent) return;
                int u0 = Recipes.ArmourUnits(item);
                if (u0 <= 0) return;

                // POLOWA dawnych rachunkow (Jeff) siedzi juz w Recipes.ArmourUnits -
                // ten sam wynik widzi zakladka CRAFT, nasze menu, naprawa i tygiel
                int u = Math.Max(1, u0);

                // miekkie surowce (skora+len RAZEM) na sztuce: najwyzej tyle na tier,
                // reszta rachunku przechodzi w zelazo (okucia, nity, sprzaczki)
                int tier = Recipes.Grade(item);
                int softCap = Math.Max(1, tier * Math.Max(1, s.SoftMaterialPerTier));

                var mat = item.ArmorComponent.MaterialType;
                if (mat == ArmorComponent.ArmorMaterialTypes.Cloth)
                {
                    Clear(__result);
                    int soft = Math.Min(u, softCap);
                    __result[10] = soft;                                           // len
                    int rest = u - soft;
                    if (rest > 0) __result[2] = rest;                              // reszta w zelazo
                }
                else if (mat == ArmorComponent.ArmorMaterialTypes.Leather)
                {
                    Clear(__result);
                    int soft = Math.Min(u, softCap);
                    int leather = Math.Max(1, (int)Math.Ceiling(soft * 0.67f));
                    if (leather > soft) leather = soft;
                    __result[9] = leather;                                         // skora
                    if (soft - leather > 0) __result[10] = soft - leather;         // len na podszycie
                    int rest = u - soft;
                    if (rest > 0) __result[2] = rest;                              // reszta w zelazo
                }
                else if (mat == ArmorComponent.ArmorMaterialTypes.Chainmail || mat == ArmorComponent.ArmorMaterialTypes.Plate)
                {
                    // zachowaj gatunki zelaza wybrane przez BK wg tieru, przeskaluj ilosci
                    int main = -1, second = -1;
                    for (int i = 0; i < 9; i++)
                    {
                        if (__result[i] <= 0) continue;
                        if (main < 0 || __result[i] > __result[main]) { second = main; main = i; }
                        else if (second < 0 || __result[i] > __result[second]) second = i;
                    }
                    if (main < 0) main = 2;                                        // awaryjnie Iron3
                    bool linedLeather = __result[9] > 0;
                    Clear(__result);
                    int mainN = Math.Max(1, (int)Math.Ceiling(u * 0.9f));
                    int secN = Math.Max(0, u - mainN);
                    __result[main] = mainN;
                    if (second >= 0 && secN > 0) __result[second] = secN;
                    int lining = Math.Min(Math.Max(1, u / 6), softCap);            // wyscielka pod metal
                    if (linedLeather) __result[9] = lining; else __result[10] = lining;
                }
            }
            catch (Exception e) { Log.Error("TrueArmourCost.Materials", e); }
        }

        /// <summary>Ktora przegrodka tablicy BK trzyma ten surowiec (0-7 vanilla, 9 skora, 10 len).</summary>
        private static int BkMaterialIndex(ItemObject mat)
        {
            try
            {
                for (int i = 0; i <= 7; i++)
                    if (Recipes.MaterialItem((CraftingMaterials)i) == mat) return i;
                var id = (mat.StringId ?? "").ToLowerInvariant();
                if (id.Contains("leather") || id.Contains("hide") || id.Contains("fur")) return 9;
                if (id.Contains("linen") || id.Contains("flax") || id.Contains("wool") || id.Contains("cotton")) return 10;
            }
            catch { }
            return -1;
        }

        /// <summary>Trudnosc strzeleckich w zakladce BK = nasz prog umiejetnosci.</summary>
        internal static void RangedDifficulty(ItemObject __0, ref int __result)
        {
            try
            {
                var item = __0;
                if (item == null) return;
                var t = item.ItemType;
                if (t != ItemObject.ItemTypeEnum.Bow && t != ItemObject.ItemTypeEnum.Crossbow
                    && t != ItemObject.ItemTypeEnum.Arrows && t != ItemObject.ItemTypeEnum.Bolts) return;
                __result = Recipes.For(item).SkillNeeded;
            }
            catch { }
        }

        /// <summary>Stamina strzeleckich w zakladce BK = nasza receptura.</summary>
        internal static void RangedStamina(ItemObject __0, ref int __result)
        {
            try
            {
                var item = __0;
                if (item == null) return;
                var t = item.ItemType;
                if (t != ItemObject.ItemTypeEnum.Bow && t != ItemObject.ItemTypeEnum.Crossbow
                    && t != ItemObject.ItemTypeEnum.Arrows && t != ItemObject.ItemTypeEnum.Bolts) return;
                __result = Recipes.For(item).Stamina;
            }
            catch { }
        }

        internal static void HourlyPostfix(ref ExplainedNumber __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                float m = s.BkForgeHourlyMultiplier;
                if (m > 0.01f && Math.Abs(m - 1f) > 0.01f)
                    __result.AddFactor(m - 1f, new TextObject("{=arm_fair_rent}Fair rent"));
                // dniowka oplacona - godziny NIE kosztuja NIC (twarde zero,
                // zadnej arytmetyki mnoznikow, ktora moglaby to zepsuc)
                if (s.ForgeDayPassEnabled && DayPass.ActiveHere())
                    __result = new ExplainedNumber(0f, false, new TextObject("{=arm_day_paid}Paid for the day"));
            }
            catch { }
        }

        /// <summary>
        /// Prefix na BKSettlementActions.StartCraftingMenu: zanim godziny zaczna
        /// lecec, kupujesz DNIOWKE - i do polnocy kuznia jest Twoja.
        /// </summary>
        internal static void DayPassPrefix()
        {
            try { DayPass.EnsureBought(); } catch (Exception e) { Log.Error("DayPassPrefix", e); }
        }

        /// <summary>Czy gracz stoi wlasnie w menu "pracujesz w kuzni" Banner Kings?</summary>
        internal static bool InForgeMenu()
        {
            try
            {
                var camp = TaleWorlds.CampaignSystem.Campaign.Current;
                var mc = camp != null ? camp.CurrentMenuContext : null;
                var gm = mc != null ? mc.GameMenu : null;
                if (gm == null) return false;
                return gm.StringId == "bannerkings_wait_crafting"
                    || gm.StringId == "arm_project_wait"
                    || gm.StringId == "arm_work_wait";
            }
            catch { return false; }
        }

        /// <summary>
        /// OSTATNIA ZAPORA na godzinowke: kazda platnosc gracza dla osady,
        /// zrobiona W TRAKCIE menu kucia, jest oplata za kuznie. Dniowka
        /// aktywna -> oplata idzie do kosza. Dniowki nie ma (np. menu wrocilo
        /// z save'a bokiem, z pominieciem StartCraftingMenu) -> kupujemy ja
        /// TERAZ i te godzinowke tez wyrzucamy. "You paid 25" ginie u zrodla.
        /// </summary>
        internal static bool SwallowForgeFee(Hero giverHero, TaleWorlds.CampaignSystem.Settlements.Settlement settlement, int amount)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.ForgeDayPassEnabled) return true;
                if (DayPass.Charging) return true;               // to nasza wlasna zaplata za dniowke
                if (amount <= 0 || giverHero != Hero.MainHero) return true;
                if (!InForgeMenu()) return true;
                if (!DayPass.ActiveHere()) DayPass.EnsureBought();
                return !DayPass.ActiveHere();                    // oplacona doba = zero oplat godzinowych
            }
            catch { return true; }
        }

        /// <summary>
        /// Mlot w dloni to nie drzemka: podczas pracy w kuzni stamina kowalska
        /// NIE regeneruje sie ani o punkt. Odpocznij po robocie, nie w jej trakcie.
        /// A OBOZ to wlasnie odpoczynek: vanilla regeneruje tylko pod dachem
        /// osady - u nas takze przy ognisku wlasnego obozu i we snie.
        /// </summary>
        internal static void NoRestAtWork(Hero hero, ref int __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || hero != Hero.MainHero) return;
                if (s.ForgeWorkNoRest && InForgeMenu()) { __result = 0; return; }

                // odpoczynek w obozie liczy sie jak kwatera w osadzie
                if (InCampMenu() && __result < 5) __result = 5;
            }
            catch { }
        }

        /// <summary>Czy gracz wlasnie obozuje (nasze menu obozu, sen, oboz BK)?</summary>
        internal static bool InCampMenu()
        {
            try
            {
                var menu = Campaign.Current != null && Campaign.Current.CurrentMenuContext != null
                           && Campaign.Current.CurrentMenuContext.GameMenu != null
                    ? Campaign.Current.CurrentMenuContext.GameMenu.StringId : "";
                return menu == "arm_camp_wait" || menu == "arm_sleep_wait" || menu == "bk_camping_wait_menu" || menu == "camp";
            }
            catch { return false; }
        }

        /// <summary>
        /// SEDNO bledu "spie w obozie, a stamina kowalska stoi na 5%":
        /// vanilla w HourlyTick odhacza regeneracje TYLKO bohaterom, ktorzy
        /// maja CurrentSettlement != null - w polu petla POMIJA czlowieka,
        /// zanim w ogole spyta o stawke (wiec sama latka na stawke nic nie
        /// dawala). Dokladamy wlasna godzinowa rate w OBOZIE: gracz i jego
        /// towarzysze odzyskuja stamine przy ognisku tak, jak pod dachem.
        /// </summary>
        private static int _lastStamLog = -1;

        internal static void CampForgeRest(object __instance)
        {
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                var main = Hero.MainHero;
                if (main == null) return;
                if (main.CurrentSettlement != null) return;   // pod dachem osady vanilla liczy sama
                if (InForgeMenu()) return;                    // przy mlocie sie nie odpoczywa

                // OBOZ i SEN daja pelne tempo; zwykly marsz - polowe. Wczesniej
                // liczyl sie WYLACZNIE oboz i wystarczylo, ze Jeff spal innym
                // menu, zeby stamina staniela w miejscu na cala kampanie.
                bool camped = InCampMenu();

                var t = __instance.GetType();
                var getS = AccessTools.Method(t, "GetHeroCraftingStamina");
                var setS = AccessTools.Method(t, "SetHeroCraftingStamina");
                var getM = AccessTools.Method(t, "GetMaxHeroCraftingStamina");
                var rate = AccessTools.Method(t, "GetStaminaHourlyRecoveryRate");
                if (getS == null || setS == null || getM == null) return;

                var party = main.PartyBelongedTo;
                foreach (var hero in Hero.AllAliveHeroes)
                {
                    if (hero == null) continue;
                    if (hero != main && (party == null || hero.PartyBelongedTo != party)) continue;
                    if (hero.CurrentSettlement != null) continue;

                    int cur = (int)getS.Invoke(__instance, new object[] { hero });
                    int max = (int)getM.Invoke(__instance, new object[] { hero });
                    if (cur >= max) continue;

                    int r = 0;
                    try { if (rate != null) r = (int)rate.Invoke(__instance, new object[] { hero }); } catch { }
                    int want = camped ? MathF.Round(s.ForgeStaminaCampRate) : MathF.Round(s.ForgeStaminaMarchRate);
                    if (r < want) r = want;
                    if (r <= 0) continue;
                    int now = Math.Min(max, cur + r);
                    setS.Invoke(__instance, new object[] { hero, now });
                    if (hero == main && s.StaminaCostMessages && _lastStamLog != now)
                    {
                        _lastStamLog = now;
                        Log.Info("Stamina kowalska " + hero.Name + ": " + cur + " -> " + now + " / " + max
                                 + (camped ? " (oboz/sen)" : " (marsz)") + ".");
                    }
                }
            }
            catch (Exception e) { Log.Error("CampForgeRest", e); }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var t = Type.GetType("BannerKings.Models.Vanilla.BKSmithingModel, BannerKings");
                if (t == null) { Log.Info("TrueArmourCost: BannerKings nieobecny."); return; }
                var m1 = AccessTools.Method(t, "GetCraftingInputForArmor");
                if (m1 != null) h.Patch(m1, postfix: new HarmonyMethod(typeof(TrueArmourCost), "MaterialsPostfix"));
                var m2 = AccessTools.Method(t, "GetSmithingHourlyPrice");
                if (m2 != null) h.Patch(m2, postfix: new HarmonyMethod(typeof(TrueArmourCost), "HourlyPostfix"));
                var tAct = Type.GetType("BannerKings.Behaviours.BKSettlementActions, BannerKings");
                var m3 = tAct != null ? AccessTools.Method(tAct, "StartCraftingMenu") : null;
                if (m3 != null)
                {
                    h.Patch(m3, prefix: new HarmonyMethod(typeof(TrueArmourCost), "DayPassPrefix"));
                    Log.Info("TrueArmourCost: kuznia wynajmowana NA DOBE, platne z gory.");
                }
                // zapora na godzinowke: platnosci gracza w menu kucia lapiemy u zrodla
                var m4 = AccessTools.Method(typeof(TaleWorlds.CampaignSystem.Actions.GiveGoldAction), "ApplyForCharacterToSettlement");
                if (m4 != null)
                {
                    h.Patch(m4, prefix: new HarmonyMethod(typeof(TrueArmourCost), "SwallowForgeFee"));
                    Log.Info("TrueArmourCost: godzinowka w oplaconej kuzni idzie do kosza (zapora GiveGold).");
                }
                // praca w kuzni to nie odpoczynek - zero regeneracji staminy przy mlocie
                var tCcb = AccessTools.TypeByName("TaleWorlds.CampaignSystem.CampaignBehaviors.CraftingCampaignBehavior");
                var m5 = tCcb != null ? AccessTools.Method(tCcb, "GetStaminaHourlyRecoveryRate") : null;
                if (m5 != null)
                {
                    h.Patch(m5, postfix: new HarmonyMethod(typeof(TrueArmourCost), "NoRestAtWork"));
                    Log.Info("TrueArmourCost: stamina NIE regeneruje w trakcie pracy w kuzni.");
                }
                // strzeleckie w zakladce CRAFT BannerKings: trudnosc i stamina
                // wg naszych receptur (widok 3D + wartosci jak przy pancerzach)
                var tBkSmith = QuartermasterLaw.FindType("BannerKings.Models.Vanilla.BKSmithingModel");
                var mDiff = tBkSmith != null ? AccessTools.Method(tBkSmith, "CalculateArmorDifficulty") : null;
                if (mDiff != null) h.Patch(mDiff, postfix: new HarmonyMethod(typeof(TrueArmourCost), "RangedDifficulty"));
                var mStam = tBkSmith != null ? AccessTools.Method(tBkSmith, "CalculateArmorStamina") : null;
                if (mStam != null) h.Patch(mStam, postfix: new HarmonyMethod(typeof(TrueArmourCost), "RangedStamina"));

                // vanilla regeneruje stamine kowalska TYLKO w osadzie (HourlyTick
                // pomija ludzi w polu) - w obozie dokladamy ja sami
                var m6 = tCcb != null ? AccessTools.Method(tCcb, "HourlyTick", new Type[0]) : null;
                if (m6 != null)
                {
                    h.Patch(m6, postfix: new HarmonyMethod(typeof(TrueArmourCost), "CampForgeRest"));
                    Log.Info("TrueArmourCost: stamina kowalska regeneruje takze w obozie (sen przy ognisku).");
                }
                Log.Info("TrueArmourCost: materialy wg pancerza+tier (" + (m1 != null) + "), stawka kuzni x"
                         + Settings.Current.BkForgeHourlyMultiplier + " (" + (m2 != null) + ").");
            }
            catch (Exception e) { Log.Error("TrueArmourCost.ApplyAll", e); }
        }

        private static void Clear(int[] a) { for (int i = 0; i < a.Length; i++) a[i] = 0; }
    }

    /// <summary>
    /// Oszczep to nie karabin snajperski. Vanilla: rozrzut broni miotanej =
    /// (100 - celnosc) x 0.001 - przy celnosci 93 to laser; z konia w galopie
    /// 3/3 trafien. Mnozymy rozrzut kazdej broni MIOTANEJ, a z siodla
    /// dokladamy drugi mnoznik. Lukow i kusz nie ruszamy - maja swoje zasady.
    /// </summary>
    internal static class ThrownWobblePatch
    {
        internal static void Postfix(TaleWorlds.MountAndBlade.Agent agent, WeaponComponentData weapon, ref float __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.ThrownWobbleEnabled || weapon == null) return;
                if (weapon.RelevantSkill != TaleWorlds.Core.DefaultSkills.Throwing) return;
                float f = Math.Max(1f, s.ThrownInaccuracyFactor);
                if (agent != null && agent.HasMount) f *= Math.Max(1f, s.ThrownMountedInaccuracyFactor);
                __result = Math.Max(__result, 0.002f) * f;
            }
            catch { }
        }

        internal static void ApplyAll(Harmony harmony)
        {
            var post = new HarmonyMethod(typeof(ThrownWobblePatch).GetMethod(
                "Postfix", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public));
            var baseT = Type.GetType("TaleWorlds.MountAndBlade.AgentStatCalculateModel, TaleWorlds.MountAndBlade");
            if (baseT == null) { Log.Info("ThrownWobble: brak AgentStatCalculateModel."); return; }

            int done = 0;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException rtle) { types = rtle.Types; }
                catch { continue; }

                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract || !baseT.IsAssignableFrom(t)) continue;
                    try
                    {
                        var m = t.GetMethod("GetWeaponInaccuracy", BindingFlags.Public | BindingFlags.NonPublic |
                                                                   BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        if (m == null || m.IsAbstract) continue;
                        harmony.Patch(m, postfix: post);
                        done++;
                        Log.Info("Rozrzut miotanych: " + t.FullName + ".GetWeaponInaccuracy");
                    }
                    catch (Exception e) { Log.Error("ThrownWobble.ApplyAll(" + t.FullName + ")", e); }
                }
            }
            if (done == 0) Log.Info("ThrownWobble: zadnego modelu nie zlatano.");
        }
    }

    /// <summary>
    /// Regula Jeffa: STAN przedmiotu skaluje jego statystyki wprost.
    /// Zbroja w stanie 50% chroni polowa, w stanie 3% jest zlomem;
    /// ostrze w stanie 60% tnie za 60%. Zamiast osobnych, XML-owych
    /// odjemnikow kazdy niszczacy modyfikator (PriceMultiplier < 1)
    /// mnozy pancerz i obrazenia przez swoj procent stanu.
    /// Dodatnie modyfikatory (Masterwork) zostaja po staremu.
    /// </summary>
    internal static class ConditionScaling
    {
        /// <summary>
        /// Krzywa Jeffa - NIE liniowa: male zuzycie prawie nie boli, glebokie
        /// bije mocno, ale nigdy do zera. kara = Max x (zuzycie)^wykladnik:
        /// stan 99% ~ -0.5%, 90% ~ -6%, 50% ~ -41%, 10% ~ -80%, 1% ~ -89%.
        /// </summary>
        internal static float StatFactor(float pm)
        {
            var s = Settings.Current;
            float wear = 1f - pm;
            if (wear <= 0f) return 1f;
            float maxPen = Math.Max(0f, Math.Min(1f, s.ConditionPenaltyMax / 100f));
            float exp = Math.Max(1f, s.ConditionPenaltyExponent);
            float pen = maxPen * (float)Math.Pow(wear, exp);
            return Math.Max(0.05f, 1f - pen);
        }

        [HarmonyPatch(typeof(ItemModifier), "ModifyArmor")]
        internal static class ArmorPatch
        {
            private static void Postfix(ItemModifier __instance, int armorValue, ref int __result)
            {
                try
                {
                    var s = Settings.Current;
                    if (s == null || !s.ConditionScalesStats) return;
                    float pm = __instance != null ? __instance.PriceMultiplier : 1f;
                    if (pm >= 0.999f || pm <= 0f) return;
                    __result = Math.Max(1, (int)Math.Floor(armorValue * StatFactor(pm)));
                }
                catch { }
            }
        }

        [HarmonyPatch(typeof(ItemModifier), "ModifyDamage")]
        internal static class DamagePatch
        {
            private static void Postfix(ItemModifier __instance, int baseDamage, ref int __result)
            {
                try
                {
                    var s = Settings.Current;
                    if (s == null || !s.ConditionScalesStats) return;
                    float pm = __instance != null ? __instance.PriceMultiplier : 1f;
                    if (pm >= 0.999f || pm <= 0f) return;
                    __result = Math.Max(1, (int)Math.Floor(baseDamage * StatFactor(pm)));
                }
                catch { }
            }
        }
    }

    /// <summary>
    /// Dniowka w kuzni. Kowal nie stoi ze stoperem: placisz Z GORY za DOBE
    /// i do polnocy palenisko jest Twoje - godzina czy dwadziescia trzy,
    /// ta sama moneta. Cena = stawka godzinowa kowala (prosperity, tier klanu,
    /// perki - liczy ja Banner Kings) x ForgeDayHours. Dniowka kasuje tez
    /// nasza oplate za projekt zbroi w tym samym miescie tego samego dnia.
    /// </summary>
    internal static class DayPass
    {
        // "settlementId" -> do kiedy oplacono (w dniach kampanii). DOBA = 24 h
        // OD ZAPLATY, nie do polnocy - robota zaczeta wieczorem nie zaczyna
        // nagle liczyc godzin po 24:00 (to byl blad, za ktory Jeff placil po 25).
        private static readonly System.Collections.Generic.Dictionary<string, double> _paid =
            new System.Collections.Generic.Dictionary<string, double>();

        /// <summary>Podniesiona, gdy to MY placimy dniowke - zapora GiveGold ma przepuscic.</summary>
        internal static bool Charging;

        /// <summary>Oplacone doby -> jeden string do save'a ("id:do;id2:do2").</summary>
        internal static string Export()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                double now = TaleWorlds.CampaignSystem.CampaignTime.Now.ToDays;
                foreach (var kv in _paid)
                {
                    if (kv.Value <= now) continue;               // wygasle zostawiamy w przeszlosci
                    if (sb.Length > 0) sb.Append(';');
                    sb.Append(kv.Key).Append(':').Append(kv.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
                return sb.ToString();
            }
            catch { return ""; }
        }

        internal static void Import(string data)
        {
            try
            {
                _paid.Clear();
                if (string.IsNullOrEmpty(data)) return;
                foreach (var part in data.Split(';'))
                {
                    int i = part.LastIndexOf(':');
                    if (i <= 0) continue;
                    double until;
                    if (double.TryParse(part.Substring(i + 1), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out until))
                        _paid[part.Substring(0, i)] = until;
                }
            }
            catch { }
        }

        internal static bool ActiveHere()
        {
            try
            {
                var s = TaleWorlds.CampaignSystem.Settlements.Settlement.CurrentSettlement;
                if (s == null) return false;
                double until;
                return _paid.TryGetValue(s.StringId, out until)
                       && TaleWorlds.CampaignSystem.CampaignTime.Now.ToDays < until;
            }
            catch { return false; }
        }

        /// <summary>Stawka godzinowa kowala wg BK (po naszym mnozniku), bez skladnika dniowki.</summary>
        private static float HourlyRate(TaleWorlds.CampaignSystem.Settlements.Settlement s)
        {
            try
            {
                var model = TaleWorlds.CampaignSystem.Campaign.Current.Models.SmithingModel;
                var m = model.GetType().GetMethod("GetSmithingHourlyPrice");
                if (m != null)
                {
                    var en = m.Invoke(model, new object[] { s, Hero.MainHero });
                    var rn = en.GetType().GetProperty("ResultNumber");
                    if (rn != null) return Math.Max(5f, (float)rn.GetValue(en, null));
                }
            }
            catch { }
            return 25f;   // bez BK: skromna stawka
        }

        internal static void EnsureBought()
        {
            var c = Settings.Current;
            if (c == null || !c.ForgeDayPassEnabled) return;
            var s = TaleWorlds.CampaignSystem.Settlements.Settlement.CurrentSettlement;
            if (s == null || ActiveHere()) return;

            int rate = (int)Math.Ceiling(HourlyRate(s) * Math.Max(1f, c.ForgeDayHours));
            _paid[s.StringId] = TaleWorlds.CampaignSystem.CampaignTime.Now.ToDays + 1.0;   // pelna doba od TERAZ
            try
            {
                Charging = true;
                TaleWorlds.CampaignSystem.Actions.GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, s, rate);
            }
            catch { }
            finally { Charging = false; }
            TaleWorlds.Library.InformationManager.DisplayMessage(new TaleWorlds.Library.InformationMessage(
                "The forge is yours for a full day - " + rate + " gold, paid up front.",
                TaleWorlds.Library.Colors.Yellow));
            Log.Info("DayPass: kuznia w " + s.Name + " oplacona na 24h za " + rate + ".");
        }
    }

    /// <summary>
    /// Stan przedmiotu PO LUDZKU: procent w nazwie zamiast zgadywania,
    /// co znaczy "Battered" czy "Mangled". 100% = nowka, 1% = wrak.
    /// Procent liczymy z mnoznika wartosci modyfikatora stanu (to ta sama
    /// skala, ktora tnie cene i statystyki). Nowe rzeczy nie dostaja dopisku,
    /// dodatnie modyfikatory (Masterwork itd.) tez zostaja w spokoju.
    /// </summary>
    [HarmonyPatch(typeof(EquipmentElement), "GetModifiedItemName")]
    internal static class ConditionTagPatch
    {
        private static void Postfix(ref EquipmentElement __instance, ref TaleWorlds.Localization.TextObject __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.ShowConditionPercent || __result == null) return;
                if (ArmouryBehavior.NoWear(__instance.Item)) return;   // amunicja bez dopiskow stanu
                var m = __instance.ItemModifier;
                if (m == null) return;
                float pm = m.PriceMultiplier;
                if (pm >= 0.999f || pm <= 0f) return;          // nowe albo lepsze niz nowe - bez dopisku
                int pct = (int)Math.Round(pm * 100f);
                if (pct < 1) pct = 1;
                // ZADNYCH slow "Battered/Loose/Mangled" - przedmiot to przedmiot,
                // a stan to liczba: czysta nazwa bazowa + procent zuzycia. Lupy tez.
                string baseName = __instance.Item != null && __instance.Item.Name != null
                    ? __instance.Item.Name.ToString()
                    : __result.ToString();
                __result = new TaleWorlds.Localization.TextObject("{=!}" + baseName + " (" + pct + "%)");
            }
            catch { }
        }
    }
}
