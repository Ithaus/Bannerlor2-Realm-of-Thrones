using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// AUDYTOR KOWALSTWA. Jeff: "jak robie smelt, nie trace staminy".
    /// Model liczy koszty (smelt 10, refine 6, kucie 15+), vanilla je pobiera,
    /// zaden mod ich nie tnie - a mimo to licznik stoi. Wiec przy KAZDEJ akcji
    /// mierzymy stamine przed i po: jesli splynela - wypisujemy ile; jesli NIE
    /// splynela - pobieramy koszt SAMI wedle modelu i tez wypisujemy. Zadna
    /// darmowa robota przy piecu juz sie nie przemknie, a log nazwie winnego.
    /// </summary>
    internal static class SmithAudit
    {
        [ThreadStatic] private static int _before;
        [ThreadStatic] private static bool _tracking;

        private static CraftingCampaignBehavior Beh()
        {
            try { return Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>(); }
            catch { return null; }
        }

        // ---------------------------------------------------------------- smelt
        internal static void SmeltPrefix(Hero currentCraftingHero)
        {
            try
            {
                var b = Beh();
                _tracking = b != null && currentCraftingHero != null;
                if (_tracking) _before = b.GetHeroCraftingStamina(currentCraftingHero);
            }
            catch { _tracking = false; }
        }

        internal static void SmeltPostfix(Hero currentCraftingHero, EquipmentElement equipmentElement)
        {
            Settle(currentCraftingHero, "Smelting",
                delegate (Hero h)
                {
                    var item = equipmentElement.Item;
                    return item != null ? Campaign.Current.Models.SmithingModel.GetEnergyCostForSmelting(item, h) : 10;
                });
        }

        // ---------------------------------------------------------------- refine
        // DIAGNOZA THAMASKENE (Jeff 27.08: "przekuwam, ale sie nie dodaje"):
        // kazda rafinacja zapisuje formule i stan wyniku w sakwach PRZED i PO -
        // log rozstrzyga, czy DoRefinement w ogole biegnie, czy dodaje,
        // i czy ktos zjada wynik po nas.
        [ThreadStatic] private static ItemObject _refOutItem;
        [ThreadStatic] private static int _refOutWant;
        [ThreadStatic] private static int _refOutBefore;

        internal static void RefinePrefix(Hero hero, TaleWorlds.CampaignSystem.Crafting.RefiningFormula refineFormula)
        {
            try
            {
                var b = Beh();
                _tracking = b != null && hero != null;
                if (_tracking) _before = b.GetHeroCraftingStamina(hero);
                _refOutItem = null;
                var model = Campaign.Current != null ? Campaign.Current.Models.SmithingModel : null;
                if (model != null)
                {
                    _refOutItem = model.GetCraftingMaterialItem(refineFormula.Output);
                    _refOutWant = refineFormula.OutputCount;
                    _refOutBefore = _refOutItem != null
                        ? TaleWorlds.CampaignSystem.Party.MobileParty.MainParty.ItemRoster.GetItemNumber(_refOutItem) : -1;
                }
            }
            catch { _tracking = false; }
        }

        internal static void RefinePostfix(Hero hero)
        {
            Settle(hero, "Refining", delegate { return 6; });
            try
            {
                if (_refOutItem == null) { Log.Info("Rafinacja: model nie dal itemu wyniku (NULL) - to jest blad."); return; }
                int after = TaleWorlds.CampaignSystem.Party.MobileParty.MainParty.ItemRoster.GetItemNumber(_refOutItem);
                Log.Info("Rafinacja: " + _refOutItem.StringId + " x" + _refOutWant
                         + " | w sakwach " + _refOutBefore + " -> " + after
                         + (after <= _refOutBefore ? "  <-- WYNIK NIE DOSZEDL!" : ""));
            }
            catch (Exception e) { Log.Error("RefinePostfix.Diag", e); }
        }

        // ---------------------------------------------------------------- forge
        internal static void ForgePrefix(Hero hero)
        {
            try
            {
                var b = Beh();
                _tracking = b != null && hero != null;
                if (_tracking) _before = b.GetHeroCraftingStamina(hero);
            }
            catch { _tracking = false; }
        }

        internal static void ForgePostfix(Hero hero, ref ItemObject __result)
        {
            var made = __result;
            Settle(hero, "Forging",
                delegate (Hero h)
                {
                    return made != null ? Campaign.Current.Models.SmithingModel.GetEnergyCostForSmithing(made, h) : 15;
                });
        }

        /// <summary>
        /// Rozliczenie: stamina faktycznie splynela? Dobrze - pokaz ile.
        /// Nie splynela, choc kosztowac miala? Pobierz koszt recznie i pokaz,
        /// z dopiskiem [enforced] - w logu od razu widac, ze cos ja gubilo.
        /// </summary>
        private static void Settle(Hero hero, string what, Func<Hero, int> costOf)
        {
            try
            {
                if (!_tracking || hero == null) return;
                _tracking = false;
                var s = Settings.Current;
                if (s == null) return;
                var b = Beh();
                if (b == null) return;

                int after = b.GetHeroCraftingStamina(hero);
                int drop = _before - after;
                bool enforced = false;
                if (drop <= 0 && s.EnforceStaminaCosts)
                {
                    int cost = Math.Max(1, costOf(hero));
                    b.SetHeroCraftingStamina(hero, Math.Max(0, after - cost));
                    drop = cost;
                    after = b.GetHeroCraftingStamina(hero);
                    enforced = true;
                }
                if (drop > 0 && s.StaminaCostMessages && hero == Hero.MainHero)
                    InformationManager.DisplayMessage(new InformationMessage(
                        what + ": -" + drop + " stamina (" + after + " left)" + (enforced ? "  [enforced]" : ""),
                        Colors.Gray));
                if (enforced)
                    Log.Info("SmithAudit: " + what + " NIE pobralo staminy - wymuszam koszt " + drop + " (bylo " + _before + ").");
            }
            catch (Exception e) { Log.Error("SmithAudit.Settle", e); }
        }

        // ---------------------------------------------------------------- XP: smelt bez sufitu to winda
        /// <summary>Przetop drogiego miecza = 0.02 x wartosc = setki XP. Ten sam sufit co za kucie.</summary>
        internal static void SmeltXpPostfix(ItemObject item, ref int __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.WeaponXpFromValueCapped || item == null) return;
                int tier = Recipes.Grade(item);
                int cap = Math.Max(25, s.WeaponXpCapPerTier * tier / 2);   // przetop uczy POLOWE tego, co kucie
                if (__result > cap) __result = cap;
            }
            catch { }
        }

        /// <summary>Rafinacja: 0.3 x wartosc wsadu - sufit plaski.</summary>
        internal static void RefineXpPostfix(ref int __result)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.WeaponXpFromValueCapped) return;
                if (__result > s.RefineXpCap) __result = s.RefineXpCap;
            }
            catch { }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var t = typeof(CraftingCampaignBehavior);
                int done = 0;
                var pairs = new[]
                {
                    new { M = "DoSmelting",                        Pre = "SmeltPrefix",  Post = "SmeltPostfix"  },
                    new { M = "DoRefinement",                      Pre = "RefinePrefix", Post = "RefinePostfix" },
                    new { M = "CreateCraftedWeaponInFreeBuildMode", Pre = "ForgePrefix",  Post = "ForgePostfix"  },
                };
                foreach (var p in pairs)
                {
                    var m = AccessTools.Method(t, p.M);
                    if (m == null) { Log.Info("SmithAudit: brak " + p.M); continue; }
                    h.Patch(m,
                        prefix: new HarmonyMethod(typeof(SmithAudit).GetMethod(p.Pre, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public)),
                        postfix: new HarmonyMethod(typeof(SmithAudit).GetMethod(p.Post, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public)));
                    done++;
                }

                // sufity XP na przetop i rafinacje - w KAZDEJ implementacji modelu
                var xpS = new HarmonyMethod(typeof(SmithAudit).GetMethod("SmeltXpPostfix", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public));
                var xpR = new HarmonyMethod(typeof(SmithAudit).GetMethod("RefineXpPostfix", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public));
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException r) { types = r.Types; }
                    catch { continue; }
                    foreach (var ty in types)
                    {
                        if (ty == null || ty.IsAbstract || !typeof(SmithingModel).IsAssignableFrom(ty)) continue;
                        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
                        var ms = ty.GetMethod("GetSkillXpForSmelting", flags);
                        if (ms != null && !ms.IsAbstract) { h.Patch(ms, postfix: xpS); done++; }
                        var mr = ty.GetMethod("GetSkillXpForRefining", flags);
                        if (mr != null && !mr.IsAbstract) { h.Patch(mr, postfix: xpR); done++; }
                    }
                }
                Log.Info("SmithAudit: kazda akcja przy piecu rozliczana ze staminy, XP z sufitem (" + done + " latek).");
            }
            catch (Exception e) { Log.Error("SmithAudit.ApplyAll", e); }
        }
    }
}
