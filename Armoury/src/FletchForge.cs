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
                // liczniki odswieza postfix (biegnie takze przy prefixie false)
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
        /// LICZNIKI NA ZYWO (Jeff: "w kuzni nie odswieza sie ilosc surowcow
        /// i stamina, musze sie przeklikac"). Vanilla przelicza panel dolny
        /// (zelazo, drewno, wegiel, stamina) TYLKO po wlasnych akcjach -
        /// nasze kucie schodzi z sakw bezposrednio, wiec ekran nie wiedzial.
        /// CraftingVM.UpdateAll robi caly komplet: materialy, stamina,
        /// umiejetnosci, dostepnosc przycisku.
        /// </summary>
        private static void RefreshCraftScreen(object mixin)
        {
            try
            {
                var tr = Traverse.Create(mixin);
                var craftingVm = tr.Field("crafting").GetValue();
                if (craftingVm != null)
                    try { Traverse.Create(craftingVm).Method("UpdateAll").GetValue(); } catch { }
                try { tr.Method("OnRefresh").GetValue(); } catch { }
            }
            catch (Exception e) { Log.Error("RefreshCraftScreen", e); }
        }

        /// <summary>
        /// Pancerz wykuty droga Banner Kings tez UCZY. Bez tego platnerz nigdy
        /// nie odkrylby wzoru tieru 2, bo punkty nauki zbieralo tylko nasze
        /// wlasne kucie. Postfix biegnie po KAZDEJ robocie (takze gdy prefix
        /// przejal strzeleckie) - tu tez odswiezamy liczniki ekranu.
        /// </summary>
        public static void BkCraftPostfix(object __instance)
        {
            RefreshCraftScreen(__instance);
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

        /// <summary>
        /// BK SpendMaterials zdejmuje skore/len TYLKO po sztywnym ID "leather"/
        /// "linen", a licznik (BkExtraMatPostfix) i BK HasMaterials licza CALA
        /// kategorie handlowa. Kucie pancerza z samych zamiennikow (futra,
        /// hides ROT) wpychalo wiec do sakw UJEMNE wpisy leather/linen i liczby
        /// "sie nie zgadzaly" (Jeff, 26.08). Przejmujemy calosc: twarde surowce
        /// 1:1 jak oryginal, miekkie przez Recipes.Take - kategoria, nigdy minus.
        /// </summary>
        public static bool BkSpendMaterialsPrefix(object __instance)
        {
            try
            {
                var tr = Traverse.Create(__instance);
                var ac = tr.Field("armorCrafting").GetValue();
                if (ac == null) return true;
                var cur = Traverse.Create(ac).Property("CurrentItem").GetValue();
                if (cur == null) return true;
                var item = Traverse.Create(cur).Property("Item").GetValue<ItemObject>();
                if (item == null) return true;

                var cfg = Traverse.CreateWithType("BannerKings.BannerKingsConfig").Property("Instance").GetValue();
                var model = cfg != null ? Traverse.Create(cfg).Property("SmithingModel").GetValue() : null;
                var bill = model != null ? Traverse.Create(model).Method("GetCraftingInputForArmor", item).GetValue<int[]>() : null;
                if (bill == null || bill.Length < 11) return true;   // nie poznajemy rachunku - niech liczy oryginal

                var roster = TaleWorlds.CampaignSystem.Party.MobileParty.MainParty.ItemRoster;
                for (int l = 0; l < 9; l++)
                    if (bill[l] != 0)
                        roster.AddToCounts(Recipes.MaterialItem((CraftingMaterials)l), -bill[l]);
                if (bill[9] > 0) Recipes.Take(roster, Recipes.SoftLeather, bill[9]);
                if (bill[10] > 0) Recipes.Take(roster, Recipes.SoftLinen, bill[10]);
                return false;
            }
            catch (Exception e) { Log.Error("BkSpendMaterials", e); return true; }
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

                // zdjecie materialow za pancerz BK: kategoria zamiast sztywnego ID
                var tMixinSpend = QuartermasterLaw.FindType("BannerKings.UI.Extensions.CraftingMixin");
                var mSpend = tMixinSpend != null ? AccessTools.Method(tMixinSpend, "SpendMaterials") : null;
                if (mSpend != null)
                    h.Patch(mSpend, prefix: new HarmonyMethod(typeof(FletchForge), "BkSpendMaterialsPrefix"));

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
