using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// LUCZARNIA W ZAKLADCE CRAFT (Banner Kings), wedle slow Jeffa: luki, kusze,
    /// strzaly i belty kuja sie tam, gdzie pancerze - z pelnym widokiem 3D
    /// wyrobu i wartosciami. LISTE dokladac bedzie ForgeView (jego filtr
    /// kategorii musi widziec strzeleckie w swojej pelnej liscie) - tu siedzi
    /// tylko PRZEJECIE KUCIA: przycisk Craft dla wyrobu strzeleckiego biegnie
    /// przez nasze zasady (wzory RangedLore, legendy, materialy z receptur,
    /// serie amunicji, jakosc, nauka) i obciaza TEGO kowala, ktory stoi przy
    /// kowadle (takze towarzysza), nie zawsze gracza.
    /// Karty w oknie "Choose what to craft" przy BRONI zostaly WYCIETE na
    /// zyczenie Jeffa ("usun z broni, bo to myli").
    /// </summary>
    internal static class FletchForge
    {
        internal static bool RangedType(ItemObject it)
        {
            if (it == null) return false;
            var t = it.ItemType;
            return t == ItemObject.ItemTypeEnum.Bow || t == ItemObject.ItemTypeEnum.Crossbow
                || t == ItemObject.ItemTypeEnum.Arrows || t == ItemObject.ItemTypeEnum.Bolts;
        }

        /// <summary>
        /// Prefix na CraftingMixin.ExecuteMainActionBK: strzelecki wyrob kuje sie
        /// naszymi zasadami - BK-owa droga zostaje dla pancerzy.
        /// </summary>
        public static bool BkCraftPrefix(object __instance)
        {
            try
            {
                var tr = Traverse.Create(__instance);
                bool armorMode = false;
                try { armorMode = tr.Property("IsInArmorMode").GetValue<bool>(); } catch { }
                if (!armorMode) return true;
                var ac = tr.Field("armorCrafting").GetValue();
                if (ac == null) return true;
                var cur = Traverse.Create(ac).Property("CurrentItem").GetValue();
                if (cur == null) return true;
                var item = Traverse.Create(cur).Property("Item").GetValue<ItemObject>();

                // WZOR NIEODKRYTY - kowadlo milczy. Dotyczy TAKZE pancerzy
                // (Jeff: "pancerze tak samo, dopiero uczysz sie pancerzy").
                // Blokada stoi PRZED podzialem na strzeleckie i BK-owe, wiec
                // zamknietego wzoru nie wykujesz zadna droga.
                if (!RangedLore.KnownOf(item))
                {
                    int school = RangedLore.SchoolOf(item);
                    int kn, tot; RangedLore.CountSchool(school, out kn, out tot);
                    Log.Player("You have not worked out this pattern yet - the craft itself will teach you. ("
                               + kn + "/" + tot + " patterns known)", true);
                    return false;
                }

                if (!RangedType(item)) return true;

                // kowal przy kowadle: ten, ktorego wybrano w ekranie (moze byc towarzysz)
                Hero smith = null;
                object heroVm = null;
                try
                {
                    var craftingVm = tr.Field("crafting").GetValue();
                    heroVm = Traverse.Create(craftingVm).Property("CurrentCraftingHero").GetValue();
                    if (heroVm != null) smith = Traverse.Create(heroVm).Property("Hero").GetValue<Hero>();
                }
                catch { }

                Forge.Smith(item, smith);                 // nasze zasady od poczatku do konca
                try { if (heroVm != null) Traverse.Create(heroVm).Method("RefreshStamina").GetValue(); } catch { }
                try { tr.Method("OnRefresh").GetValue(); } catch { }
                return false;
            }
            catch (Exception e) { Log.Error("FletchForge.BkCraft", e); return true; }
        }

        /// <summary>
        /// Licznik skory/lnu na dole ekranu kuzni BK liczy TYLKO vanilla
        /// przedmiot po sztywnym ID - a ROT ma wlasne odmiany ("masz len,
        /// a pokazuje 0" - Jeff). Po zbudowaniu liczymy od nowa CALA
        /// kategoria handlowa.
        /// </summary>
        public static void BkExtraMatPostfix(object __instance)
        {
            try
            {
                var tr = Traverse.Create(__instance);
                var mat = tr.Property("Material").GetValue<ItemObject>();
                if (mat == null || mat.ItemCategory == null) return;
                var id = (mat.StringId ?? "").ToLowerInvariant();
                if (id != "linen" && id != "leather") return;
                int n = 0;
                var roster = TaleWorlds.CampaignSystem.Party.MobileParty.MainParty.ItemRoster;
                for (int i = 0; i < roster.Count; i++)
                {
                    var it = roster[i].EquipmentElement.Item;
                    if (it != null && it.ItemCategory == mat.ItemCategory) n += roster[i].Amount;
                }
                tr.Property("ResourceAmount").SetValue(n);
            }
            catch { }
        }

        /// <summary>
        /// Pancerz wykuty droga Banner Kings tez UCZY. Bez tego platnerz nigdy
        /// nie odkrylby wzoru tieru 2, bo punkty nauki zbieralo tylko nasze
        /// wlasne kucie. Postfix biegnie po udanej robocie BK.
        /// </summary>
        public static void BkCraftPostfix(object __instance)
        {
            try
            {
                var tr = Traverse.Create(__instance);
                bool armorMode = false;
                try { armorMode = tr.Property("IsInArmorMode").GetValue<bool>(); } catch { }
                if (!armorMode) return;
                var ac = tr.Field("armorCrafting").GetValue();
                if (ac == null) return;
                var cur = Traverse.Create(ac).Property("CurrentItem").GetValue();
                if (cur == null) return;
                var item = Traverse.Create(cur).Property("Item").GetValue<ItemObject>();
                if (item == null || RangedType(item)) return;      // strzeleckie licza sie w Forge.Smith
                RangedLore.OnCrafted(item);
            }
            catch { }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.CraftingEnabled || !c.AllowRangedCrafting)
                { Log.Info("FletchForge: wylaczone."); return; }

                // licznik lnu/skory na dole ekranu: liczy cala kategorie, nie jeden ID
                var tExtra = QuartermasterLaw.FindType("BannerKings.UI.Crafting.ExtraMaterialItemVM");
                var ctorExtra = tExtra != null ? AccessTools.Constructor(tExtra, new[] { typeof(ItemObject) }) : null;
                if (ctorExtra != null)
                    h.Patch(ctorExtra, postfix: new HarmonyMethod(typeof(FletchForge), "BkExtraMatPostfix"));

                var tMixin = QuartermasterLaw.FindType("BannerKings.UI.Extensions.CraftingMixin");
                var mMain = tMixin != null ? AccessTools.Method(tMixin, "ExecuteMainActionBK") : null;
                if (mMain != null)
                    h.Patch(mMain,
                        prefix: new HarmonyMethod(typeof(FletchForge), "BkCraftPrefix"),
                        postfix: new HarmonyMethod(typeof(FletchForge), "BkCraftPostfix"));
                Log.Info("FletchForge: kucie strzeleckich w zakladce CRAFT przejete (" + (mMain != null) + ").");
            }
            catch (Exception e) { Log.Error("FletchForge.ApplyAll", e); }
        }
    }
}
