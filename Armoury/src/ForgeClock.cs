using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// JEDEN ZEGAR KUZNI (wpis 83; Jeff 05.10: "klikam wykuj miecz, pokazuje X godzin - powinno wykuwac sie po X godzinach;
    /// wykuje 10 x 7 h, to powinienem dostawac co 7 godzin miecz, i nie kuje sie, jak mnie nie ma w miescie"; "godziny
    /// z ekranu BK"; "noc - tak").
    ///
    /// Bylo: trzy zegary. Pancerz w zakladce CRAFT (BK) wpadal do sakw od razu, BK sumowal godziny (stamina / 6) i po
    /// zamknieciu ekranu kazal je odsiedziec naraz (StartCraftingMenu); bron (vanilla) - to samo PLUS nasz projekt "van"
    /// z WeaponDaysPerTier, ktory kul dalej pod nieobecnosc (ForgeWorksWithoutYou) - czas liczony dwa razy.
    ///
    /// Teraz: Craft = material, stamina i wynik przy kowadle (jak bylo), a sztuka idzie do kolejki tego kowala z godzinami
    /// z ekranu BK (zuzyta stamina / 6). Kolejka kuje po kolei, sztuka wychodzi, gdy jest gotowa, tylko gdy jestes w tej
    /// osadzie (ForgeOnlyWhileThere), z przerwa nocna 23-5 (WorkshopNightRest). Menu odsiadki BK wylaczone.
    /// </summary>
    internal static class ForgeClock
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.ForgeOneClock; } }

        /// <summary>BK: "Hours spent for all actions" = zuzyta stamina / 6.</summary>
        internal static float HoursOf(int stamina) { return Math.Max(0.25f, stamina / 6f); }

        // ------------------------------------------------------------ zakladka CRAFT (BK ExecuteMainActionBK)
        private static Hero _hero; private static int _stam; private static ItemObject _item; private static int _count;

        public static void CraftPre(object __instance)
        {
            _hero = null; _item = null; _stam = 0; _count = 0;
            try
            {
                if (!On) return;
                var tr = Traverse.Create(__instance);
                try
                {
                    var vm = tr.Field("crafting").GetValue();
                    var hv = vm != null ? Traverse.Create(vm).Property("CurrentCraftingHero").GetValue() : null;
                    if (hv != null) _hero = Traverse.Create(hv).Property("Hero").GetValue<Hero>();
                }
                catch { }
                if (_hero == null) _hero = Hero.MainHero;
                _stam = Forge.Stamina(_hero);
                bool armor = false;
                try { armor = tr.Property("IsInArmorMode").GetValue<bool>(); } catch { }
                if (!armor) return;
                var ac = tr.Field("armorCrafting").GetValue();
                var cur = ac != null ? Traverse.Create(ac).Property("CurrentItem").GetValue() : null;
                _item = cur != null ? Traverse.Create(cur).Property("Item").GetValue<ItemObject>() : null;
                if (_item != null) _count = MobileParty.MainParty.ItemRoster.GetItemNumber(_item);
            }
            catch { }
        }

        /// <summary>Pancerz kuty droga BK: swieza sztuka z sakw do kolejki kowala.</summary>
        public static void CraftPost()
        {
            try
            {
                if (!On || _item == null || FletchForge.RangedType(_item)) return;   // strzeleckie kolejkuje Forge.Smith
                var bag = MobileParty.MainParty.ItemRoster;
                // zbroja z kuzni jak bron: nasza regula wie, ze sie udalo i CO wyszlo spod mlota - na lawe idzie dokladnie ta sztuka
                // (z jej stanem). GetItemNumber liczy tylko PIERWSZY stos tej zbroi w sakwach: sztuka w innym stanie niz pierwszy
                // stos (np. druga kopia z kuzni) wygladalaby na "spartaczona" i zostawala w sakwach od razu, bez godzin kowala
                EquipmentElement exact;
                bool ours = ArmourQuality.Fresh(_item, out exact) && bag.FindIndexOfElement(exact) >= 0;
                if (!ours && bag.GetItemNumber(_item) <= _count) return;               // spartaczone - nic nie przybylo
                EquipmentElement fresh = default(EquipmentElement); bool found = false;
                if (ours) { fresh = exact; found = true; }
                for (int i = bag.Count - 1; i >= 0 && !found; i--)
                {
                    var el = bag.GetElementCopyAtIndex(i);
                    if (el.EquipmentElement.Item == _item && el.Amount > 0) { fresh = el.EquipmentElement; found = true; break; }
                }
                if (!found) return;
                int spent = Math.Max(0, _stam - Forge.Stamina(_hero));
                bag.AddToCounts(fresh, -1);
                Queue(_item, fresh.ItemModifier, 1, HoursOf(spent));
            }
            catch (Exception e) { Log.Error("ForgeClock.CraftPost", e); }
            finally { _item = null; }
        }

        /// <summary>Wyrob do kolejki kowala w tej osadzie; wydanie w AdvanceProjects, gdy godziny miną.</summary>
        internal static void Queue(ItemObject item, ItemModifier mod, int count, float hours)
        {
            var here = TaleWorlds.CampaignSystem.Settlements.Settlement.CurrentSettlement;
            var b = ArmouryBehavior.Instance;
            if (b == null || here == null)
            {
                MobileParty.MainParty.ItemRoster.AddToCounts(new EquipmentElement(item, mod), Math.Max(1, count));
                return;
            }
            b.StartProject(item, 1, hours / 24f, here, "bk", mod != null ? mod.StringId : "", Math.Max(1, count));
            float queued = b.ProjectHoursLeftHere(here);
            Log.Player("On the bench: " + (count > 1 ? count + " x " : "") + item.Name + " - " + Project.TimeLabel(hours / 24f)
                       + " of work. Your queue here: " + Project.TimeLabel(queued / 24f) + ". The work goes on only while you stay in "
                       + here.Name + "; each piece comes off the anvil when it is done.");
        }

        /// <summary>BK StartCraftingMenu: zamiast odsiadki wszystkich godzin naraz - nasze czekanie przy kowadle.</summary>
        public static bool SkipBkWait()
        {
            if (!On) return true;
            try
            {
                var b = ArmouryBehavior.Instance;
                var here = TaleWorlds.CampaignSystem.Settlements.Settlement.CurrentSettlement;
                if (b != null && b.HasProjectsHere(here)) GameMenu.SwitchToMenu("arm_project_wait");
            }
            catch (Exception e) { Log.Error("ForgeClock.SkipBkWait", e); }
            return false;
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var tMixin = QuartermasterLaw.FindType("BannerKings.UI.Extensions.CraftingMixin");
                var mMain = tMixin != null ? AccessTools.Method(tMixin, "ExecuteMainActionBK") : null;
                if (mMain != null)
                    h.Patch(mMain, prefix: new HarmonyMethod(typeof(ForgeClock), nameof(CraftPre)) { priority = Priority.First },
                                   postfix: new HarmonyMethod(typeof(ForgeClock), nameof(CraftPost)) { priority = Priority.Last });
                var tAct = QuartermasterLaw.FindType("BannerKings.Behaviours.BKSettlementActions");
                var mWait = tAct != null ? AccessTools.Method(tAct, "StartCraftingMenu") : null;
                if (mWait != null)
                    h.Patch(mWait, prefix: new HarmonyMethod(typeof(ForgeClock), nameof(SkipBkWait)) { priority = Priority.Last });
                Log.Info("ForgeClock: jeden zegar kuzni " + (On ? "CZYNNY" : "wylaczony w MCM") + " - kucie CRAFT " + (mMain != null ? "wpiete" : "BRAK")
                         + ", odsiadka BK " + (mWait != null ? "zastapiona kolejka" : "BRAK") + ".");
            }
            catch (Exception e) { Log.Error("ForgeClock.ApplyAll", e); }
        }
    }
}
