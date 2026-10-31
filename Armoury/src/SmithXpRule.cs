using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.CraftingSystem;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 305d JEDNA ZASADA XP ZA PRACE KOWALA (klucz SmithXpOneRule; Jeff 31.10: "czy nie za duzo xp dostaje za luki a za malo za wykucie mieczy").
    /// DOWOD (log 30.10, Armoury-2026-10-30_21-04-45): kuznia 54 348 surowego XP, z tego luki ok. 27 700 (51%) - Forge.Smith placi ProjectXp x 0.6 = tier^2 x 100 x 0.6
    /// (240/540/960 za t2/t3/t4), choc luk kosztuje 17/26/35 staminy; wlasny miecz 1 XP (gra zaokragla 0.02 x 12 d. do 0, HistoricalPrices skalowal zero - SmithXpRoundFix);
    /// pancerz z CRAFT BK 0.02 x cena (CraftingMixin.ExecuteMainActionBK -> GetSkillXpForSmithingInFreeBuildMode); zlecenie 0.25 x cena projektu (CraftingOrder.GetOrderExperience) bez sufitu.
    /// ZASADA: XP = stamina pracy x SmithXpPerStaminaTier (7) x Recipes.Grade (1..6) x krzywa nauki Forge.Learning (ta sama co ProjectXp), sufit XpCapPerTier x tier.
    /// 7 dobrane tak, ze luki zostaja: 17x7x2 = 238, 26x7x3 = 546, 35x7x4 = 980 (bylo 240/540/960).
    /// GDZIE: Forge.Smith (luki, kusze, amunicja - stamina przepisu, bez AmmoXpMul: stamina amunicji juz jest 0.05 x tier x 25), wolne kucie broni gry
    /// (GetSkillXpForSmithingInFreeBuildMode: stamina SmithingModel.GetEnergyCostForSmithing - to samo, co gra pobiera), pancerz z CRAFT BK (ta sama metoda wolana
    /// przez BK; stamina BannerKingsConfig.Instance.SmithingModel.CalculateArmorStamina - z nasza latka TrueArmourCost.RangedStamina, czyli tyle, ile BK pobiera),
    /// zlecenie (GetSkillXpForSmithingInCraftingOrderMode = praca, CraftOrders8.OrderXpPostfix = 0.25 x praca x proporcja gry przy niespelnionym).
    /// Kowal: bohater z CreateCraftedWeaponInFreeBuildMode / InCraftingOrderMode (argument 0) albo z ekranu BK (PatternGate.SmithOf), inaczej gracz.
    /// XP godzinowe za bron "van" (ArmouryBehavior) przy zasadzie pominiete - praca zaplacona przy kowadle (jak "bk").
    /// Postfiksy Priority.HigherThanNormal: po HistoricalPrices.XpPostfix (High), przed sufitem WeaponXpPatch (Normal) - sufit dalej tnie; dla pancerza przy zasadzie
    /// sufit XpCapPerTier (1300 x tier) zamiast WeaponXpCapPerTier (500 x tier). Postfiks podstawia wartosc (nie mnozy), wiec dwa wywolania przez lancuch modeli daja to samo.
    /// Potkniecia liczone (bez wylacznika): przy wyjatku zostaje wynik gry. Linia startu "Kowalstwo - jedna zasada XP (305d): wpiete N/N".
    /// </summary>
    internal static class SmithXpRule
    {
        internal const string P = "Kowalstwo - jedna zasada XP (305d): ";
        private static int _stumbles, _want, _wired;
        [ThreadStatic] private static Hero _smith;
        // doba: liczba podstawien i suma XP pracy wedlug drogi
        internal static int DayFree, DayArmour, DayOrder, DayOrderBonus;
        internal static double DayFreeXp, DayArmourXp, DayOrderXp, DayOrderBonusXp;

        internal static bool On { get { var s = Settings.Current; return s != null && s.SmithXpOneRule; } }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 3 || _stumbles % 100 == 0) Log.Error("SmithXpRule." + where + " (potkniecie " + _stumbles + ")", e);
        }

        internal static int Stumbles { get { return _stumbles; } }

        /// <summary>Slowo zrodla do linii dobowej SmithXp (luk / kusza / amunicja / pancerz / miecz).</summary>
        internal static string SourceOf(ItemObject item)
        {
            if (item == null) return "inne";
            switch (item.ItemType)
            {
                case ItemObject.ItemTypeEnum.Bow: return "luk";
                case ItemObject.ItemTypeEnum.Crossbow: return "kusza";
                case ItemObject.ItemTypeEnum.Arrows:
                case ItemObject.ItemTypeEnum.Bolts: return "amunicja";
            }
            if (item.WeaponDesign != null || item.IsCraftedWeapon) return "miecz";
            return "pancerz";
        }

        /// <summary>XP pracy: stamina x stawka x tier x nauka (Forge.Learning od umiejetnosci kowala i trudnosci przepisu), sufit XpCapPerTier x tier.</summary>
        internal static float WorkXp(int stamina, int grade, int difficulty, Hero hero)
        {
            var s = Settings.Current;
            if (s == null) return 0f;
            int g = Math.Max(1, Math.Min(6, grade));
            int skill = 0;
            try { skill = (hero ?? Hero.MainHero).GetSkillValue(DefaultSkills.Crafting); } catch { }
            float xp = Math.Max(0, stamina) * (float)Math.Max(0, s.SmithXpPerStaminaTier) * g * Forge.Learning(skill, difficulty);
            float cap = Math.Max(50, s.XpCapPerTier * g);
            return Math.Min(cap, xp);
        }

        private static Hero Smith() { return _smith ?? Hero.MainHero; }

        // ------------------------------------------------------------ stamina sztuki
        private static MethodInfo _bkStam;
        private static PropertyInfo _bkInst, _bkModel;
        private static bool _bkLooked;

        private static int ArmourStamina(ItemObject item, Hero hero)
        {
            if (!_bkLooked)
            {
                _bkLooked = true;
                try
                {
                    var tc = QuartermasterLaw.FindType("BannerKings.BannerKingsConfig");
                    var tm = QuartermasterLaw.FindType("BannerKings.Models.Vanilla.BKSmithingModel");
                    _bkInst = tc != null ? AccessTools.Property(tc, "Instance") : null;
                    _bkModel = tc != null ? AccessTools.Property(tc, "SmithingModel") : null;
                    _bkStam = tm != null ? AccessTools.DeclaredMethod(tm, "CalculateArmorStamina", new[] { typeof(ItemObject), typeof(Hero) }) : null;
                }
                catch (Exception e) { Stumble("ArmourStamina(szukanie)", e); }
            }
            if (_bkStam != null && _bkInst != null && _bkModel != null)
            {
                var cfg = _bkInst.GetValue(null, null);
                var model = cfg != null ? _bkModel.GetValue(cfg, null) : null;
                if (model != null) return (int)_bkStam.Invoke(model, new object[] { item, hero });
            }
            return Recipes.For(item).Stamina;   // bez BK: nasz przepis (ta sama liczba, ktora daje latka TrueArmourCost.RangedStamina)
        }

        private static int WeaponStamina(ItemObject item, Hero hero)
        {
            var m = Campaign.Current != null ? Campaign.Current.Models.SmithingModel : null;
            if (m == null) return 15;
            // recenzja 305d: perk Practical Smith (-50% staminy w DefaultSmithingModel) nie moze ciac XP za prace - luki z Forge.Smith licza stamine
            // przepisu bez perka. Przy perku liczymy wzor gry bez perka: 10 + 5 x Tier, a gdy model to nie czysty DefaultSmithingModel (BK) -
            // mnoznik klasy BK (x1.2 jednoreczne miecz/topor/buzdygan, x1.5 dwureczne) i zakres BK 15..max staminy kowala (dekompilacja BKSmithingModel).
            if (hero == null || !hero.GetPerkValue(TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks.Crafting.PracticalSmith))
                return m.GetEnergyCostForSmithing(item, hero);
            int st = 10 + 5 * (int)item.Tier;
            if (m.GetType() != typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultSmithingModel))
            {
                var w = item.WeaponComponent != null ? item.WeaponComponent.PrimaryWeapon : null;
                if (w != null)
                {
                    var c = w.WeaponClass;
                    if (c == WeaponClass.TwoHandedSword || c == WeaponClass.TwoHandedAxe || c == WeaponClass.TwoHandedMace) st = (int)(st * 1.5f);
                    else if (c == WeaponClass.OneHandedSword || c == WeaponClass.OneHandedAxe || c == WeaponClass.Mace) st = (int)(st * 1.2f);
                }
                int max = 100 + TaleWorlds.Library.MathF.Round(hero.GetSkillValue(DefaultSkills.Crafting) * 0.5f);   // CraftingCampaignBehavior.GetMaxHeroCraftingStamina
                st = TaleWorlds.Library.MBMath.ClampInt(st, 15, max);
            }
            return st;
        }

        /// <summary>XP pracy za sztuke z kuzni gry albo z CRAFT BK (bez czesci zlecenia).</summary>
        internal static float PieceXp(ItemObject item, Hero hero, out bool armour)
        {
            armour = !(item.WeaponDesign != null || item.IsCraftedWeapon);
            int st = armour ? ArmourStamina(item, hero) : WeaponStamina(item, hero);
            int diff = 0;
            try { diff = Recipes.For(item).Difficulty; } catch { }
            return WorkXp(st, Recipes.Grade(item), diff, hero);
        }

        // ------------------------------------------------------------ kontekst kowala
        public static void HeroPrefix(Hero __0) { _smith = __0; }
        public static Exception HeroFinalizer(Exception __exception) { _smith = null; return __exception; }
        public static void BkPrefix(object __instance) { try { _smith = PatternGate.SmithOf(__instance); } catch { _smith = null; } }

        // ------------------------------------------------------------ postfiksy modelu
        public static void FreeBuildPostfix(ItemObject item, ref int __result)
        {
            if (item == null) return;
            SmithXp.Tag(SourceOf(item) == "miecz" ? "miecz" : "pancerz");   // zrodlo do linii dobowej takze przy kluczu NIE
            if (!On) return;
            try
            {
                bool armour;
                float xp = PieceXp(item, Smith(), out armour);
                __result = (int)Math.Round(xp);
                if (armour) { DayArmour++; DayArmourXp += __result; } else { DayFree++; DayFreeXp += __result; }
            }
            catch (Exception e) { Stumble("FreeBuildPostfix", e); }
        }

        public static void OrderModePostfix(ItemObject item, ref int __result)
        {
            SmithXp.Tag("zlecenie");
            if (!On || item == null) return;
            try
            {
                bool armour;
                __result = (int)Math.Round(PieceXp(item, Smith(), out armour));
                DayOrder++; DayOrderXp += __result;
            }
            catch (Exception e) { Stumble("OrderModePostfix", e); }
        }

        /// <summary>CraftOrders8.OrderXpPostfix przy zasadzie: premia zlecenia = 0.25 x praca sztuki x proporcja gry (1 przy spelnionym, 0.5 x kara tieru przy
        /// niespelnionym - odczytana z wyniku gry wobec 0.25 x wartosc teoretyczna projektu). false = wyjatek, zostaje stara droga.</summary>
        internal static bool OrderBonus(CraftingOrder order, ItemObject crafted, ref float __result)
        {
            try
            {
                if (order == null || crafted == null) return false;
                var design = order.PreCraftedWeaponDesignItem;
                float full = design != null ? Campaign.Current.Models.TradeItemPriceFactorModel.GetTheoreticalMaxItemMarketValue(design) * 0.25f : 0f;
                float share = full > 0f ? Math.Max(0f, Math.Min(1f, __result / full)) : 1f;
                bool armour;
                __result = 0.25f * PieceXp(crafted, Smith(), out armour) * share;
                SmithXp.Tag("zlecenie");
                DayOrderBonus++; DayOrderBonusXp += __result;
                return true;
            }
            catch (Exception e) { Stumble("OrderBonus", e); return false; }
        }

        /// <summary>Same slowa zrodla do linii dobowej (nic nie zmieniaja): przetop i rafinacja.</summary>
        public static void SmeltTagPostfix() { SmithXp.Tag("przetop"); }
        public static void RefineTagPostfix() { SmithXp.Tag("rafinacja"); }

        // ------------------------------------------------------------ wpiecie (SubModuleMain, po HistoricalPrices.ApplyAll)
        internal static void ApplyAll(Harmony h)
        {
            _want = 0; _wired = 0;
            var fin = new HarmonyMethod(typeof(SmithXpRule), nameof(HeroFinalizer));
            try
            {
                var t = typeof(CraftingCampaignBehavior);
                foreach (var name in new[] { "CreateCraftedWeaponInFreeBuildMode", "CreateCraftedWeaponInCraftingOrderMode" })
                {
                    _want++;
                    var m = AccessTools.DeclaredMethod(t, name);
                    if (m != null) { h.Patch(m, prefix: new HarmonyMethod(typeof(SmithXpRule), nameof(HeroPrefix)), finalizer: fin); _wired++; }
                }
            }
            catch (Exception e) { Stumble("ApplyAll(kowal)", e); }
            try
            {
                var tMixin = QuartermasterLaw.FindType("BannerKings.UI.Extensions.CraftingMixin");
                if (tMixin != null)
                {
                    _want++;
                    var m = AccessTools.DeclaredMethod(tMixin, "ExecuteMainActionBK");
                    if (m != null) { h.Patch(m, prefix: new HarmonyMethod(typeof(SmithXpRule), nameof(BkPrefix)) { priority = Priority.First }, finalizer: fin); _wired++; }
                }
            }
            catch (Exception e) { Stumble("ApplyAll(BK)", e); }
            // kazda konkretna implementacja modelu kowalstwa (jak WeaponXpPatch) - metody zadeklarowane w tej klasie
            int impl = 0;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException r) { types = r.Types; }
                catch { continue; }
                foreach (var ty in types)
                {
                    if (ty == null || ty.IsAbstract || !typeof(SmithingModel).IsAssignableFrom(ty)) continue;
                    try
                    {
                        var mf = AccessTools.DeclaredMethod(ty, "GetSkillXpForSmithingInFreeBuildMode", new[] { typeof(ItemObject) });
                        if (mf != null && !mf.IsAbstract) { _want++; h.Patch(mf, postfix: new HarmonyMethod(typeof(SmithXpRule), nameof(FreeBuildPostfix)) { priority = Priority.HigherThanNormal }); _wired++; impl++; }
                        var mo = AccessTools.DeclaredMethod(ty, "GetSkillXpForSmithingInCraftingOrderMode", new[] { typeof(ItemObject) });
                        if (mo != null && !mo.IsAbstract) { _want++; h.Patch(mo, postfix: new HarmonyMethod(typeof(SmithXpRule), nameof(OrderModePostfix)) { priority = Priority.HigherThanNormal }); _wired++; }
                        var ms = AccessTools.DeclaredMethod(ty, "GetSkillXpForSmelting", new[] { typeof(ItemObject) });
                        if (ms != null && !ms.IsAbstract) { _want++; h.Patch(ms, postfix: new HarmonyMethod(typeof(SmithXpRule), nameof(SmeltTagPostfix)) { priority = Priority.Last }); _wired++; }
                        var mr = AccessTools.DeclaredMethod(ty, "GetSkillXpForRefining");
                        if (mr != null && !mr.IsAbstract) { _want++; h.Patch(mr, postfix: new HarmonyMethod(typeof(SmithXpRule), nameof(RefineTagPostfix)) { priority = Priority.Last }); _wired++; }
                    }
                    catch (Exception e) { Stumble("ApplyAll(" + ty.FullName + ")", e); }
                }
            }
            if (impl == 0) _want += 2;   // brak jakiejkolwiek implementacji - linia pokaze braki
            Log.Info(P + "wpiete " + _wired + "/" + _want + " (kowal z kuzni gry i z CRAFT BK, wolne kucie, zlecenie, slowa przetopu i rafinacji w " + impl + " modelach kowalstwa) | klucz SmithXpOneRule "
                     + (On ? "TAK" : "NIE") + ", SmithXpPerStaminaTier " + (Settings.Current != null ? Settings.Current.SmithXpPerStaminaTier : 0)
                     + ", SmithXpRoundFix " + (Settings.Current != null && Settings.Current.SmithXpRoundFix ? "TAK" : "NIE") + " | potkniecia " + _stumbles + ".");
        }

        internal static void ResetDay()
        {
            DayFree = DayArmour = DayOrder = DayOrderBonus = 0;
            DayFreeXp = DayArmourXp = DayOrderXp = DayOrderBonusXp = 0;
        }
    }
}
