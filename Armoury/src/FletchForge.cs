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
                    // unikat imienny NIE wpada z nauki przy kowadle - trzeba
                    // zdobyc oryginal i go rozlozyc albo przetopic
                    if (UniqueGear.Is(item))
                    {
                        Log.Player("This famed piece cannot be worked out at the anvil. Seize the original and study it - melt it down, or take it apart at the smith.", true);
                        return false;
                    }
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
                // zbroja z kuzni jak bron: brama progu odmowila - nic sie nie kulo, wiec ani nauki wzorow, ani okna wyniku
                if (ArmourQuality.Refused) return;
                RangedLore.OnCrafted(item);
                // okno wyniku takze dla pancerza kutego w BK CRAFT: jakosc
                // czytamy z OSTATNIEGO stacka tego itemu w sakwach (rostery
                // dokladaja swieze na koniec)
                try
                {
                    ItemModifier freshMod = null;
                    EquipmentElement exact;
                    bool known = ArmourQuality.Fresh(item, out exact);   // nasza regula wie dokladnie, co wyszlo spod mlota
                    if (known) freshMod = exact.ItemModifier;
                    var bag = TaleWorlds.CampaignSystem.Party.MobileParty.MainParty.ItemRoster;
                    for (int i = bag.Count - 1; i >= 0 && !known; i--)
                    {
                        var el = bag.GetElementCopyAtIndex(i);
                        if (el.EquipmentElement.Item != item) continue;
                        freshMod = el.EquipmentElement.ItemModifier;
                        break;
                    }
                    if (!ForgeClock.On) CraftPopup.Show(item, freshMod, 1);   // wpis 83: okno przy wydaniu z kolejki
                }
                catch { }
            }
            catch { }
        }

        private static int _shieldBills;   // n20-kuznia: tarcze wykute w CRAFT BK z kwitu Recipes.For (od startu procesu; linia startu sesji SmeltTruth)
        private static string _velvetShort;   // n20-kuznia po recenzji: tarcza t5+ w CRAFT BK - brak aksamitu (BkHasMaterialsPostfix -> podpowiedz BkMainActionPostfix)
        private static int _velvetTaken;      // sztuki aksamitu zdjete za tarcze w CRAFT BK od startu procesu

        /// <summary>
        /// BK SpendMaterials zdejmuje skore/len TYLKO po sztywnym ID "leather"/
        /// "linen", a licznik (BkExtraMatPostfix) i BK HasMaterials licza CALA
        /// kategorie handlowa. Kucie pancerza z samych zamiennikow (futra,
        /// hides ROT) wpychalo wiec do sakw UJEMNE wpisy leather/linen i liczby
        /// "sie nie zgadzaly" (Jeff, 26.08). Przejmujemy calosc: twarde surowce
        /// 1:1 jak oryginal, miekkie przez Recipes.Take - kategoria, nigdy minus.
        /// </summary>

        /// <summary>n20-kuznia: kwit tarczy z modelu BK (z naszymi latkami) wobec receptury - do linii startu sesji "Kuznia przetop (n20-kuznia)".</summary>
        internal static string ShieldBillCheck(ItemObject shield)
        {
            try
            {
                if (shield == null) return "brak proby";
                var cfg = Traverse.CreateWithType("BannerKings.BannerKingsConfig").Property("Instance").GetValue();
                var model = cfg != null ? Traverse.Create(cfg).Property("SmithingModel").GetValue() : null;
                var bill = model != null ? Traverse.Create(model).Method("GetCraftingInputForArmor", shield).GetValue<int[]>() : null;
                if (bill == null || bill.Length < 9) return "brak kwitu BK";
                int metal = 0; for (int g = 1; g <= 6; g++) metal += Math.Max(0, bill[g]);
                var r = Recipes.For(shield);
                int rm = 0;
                if (r.Parts != null)
                    foreach (var p in r.Parts)
                        for (int g = 1; g <= 6; g++)
                            if (p.Item != null && p.Item == Recipes.MaterialItem((CraftingMaterials)g)) { rm += p.Count; break; }
                return shield.StringId + " metal " + metal + " (receptura " + rm + "), drewno " + bill[(int)CraftingMaterials.Wood]
                       + (metal == rm ? " = z receptury" : " = kwit BK") + ", wykutych tak tarcz od startu " + _shieldBills
                       + ", aksamit tarcz t5+ (sprawdzany i zdejmowany osobno) zdjety od startu " + _velvetTaken + ", latki tarczy " + _shieldHooks;
            }
            catch (Exception e) { return "wyjatek " + e.GetType().Name; }
        }

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
                // n20-kuznia: drewno tarczy w kg i aksamit - BkShieldSpendPostfix (wpiety takze bez AllowRangedCrafting, gdy ten prefiks nie jest wpiety)
                return false;
            }
            catch (Exception e) { Log.Error("BkSpendMaterials", e); return true; }
        }

        // ------------------------------------------------------------ n20-kuznia po recenzji: tarcza w CRAFT BK - reszta kwitu Recipes.For poza tablica BK
        // Tablica BK ma 11 miejsc (9 surowcow kuzni, skora, len) - aksamitu tarcz t5-t6 (Recipes.For, blok miekkich surowcow) i kg drewna z otwartego ladunku
        // (SmithWood) w niej nie ma. Postfiks SpendMaterials (po oryginale BK albo po naszym BkSpendMaterialsPrefix) zdejmuje je tak jak nasza kuznia
        // (Recipes.TakeMaterials); HasMaterials nie przepuszcza tarczy bez aksamitu, UpdateMainAction mowi graczowi, czego brakuje (BK pokazuje tylko skore i len).
        private static string _shieldHooks = "nie wpiete";

        private static ItemObject BkCurrentItem(object mixin)
        {
            var ac = mixin != null ? Traverse.Create(mixin).Field("armorCrafting").GetValue() : null;
            var cur = ac != null ? Traverse.Create(ac).Property("CurrentItem").GetValue() : null;
            return cur != null ? Traverse.Create(cur).Property("Item").GetValue<ItemObject>() : null;
        }

        private static bool ShieldRecipeOn(ItemObject item)
        {
            return item != null && item.ItemType == ItemObject.ItemTypeEnum.Shield && Settings.Current != null && Settings.Current.BkShieldBillByRecipe;
        }

        /// <summary>Aksamit z receptury tarczy (Recipes.For; 0 ponizej t5 albo bez klucza).</summary>
        private static int ShieldVelvetNeed(ItemObject item)
        {
            if (!ShieldRecipeOn(item)) return 0;
            var v = Recipes.SoftVelvet;
            if (v == null) return 0;
            var r = Recipes.For(item);
            int n = 0;
            if (r.Parts != null) foreach (var p in r.Parts) if (p.Item == v) n += p.Count;
            return n;
        }

        public static void BkShieldSpendPostfix(object __instance)
        {
            try
            {
                var item = BkCurrentItem(__instance);
                if (!ShieldRecipeOn(item)) return;
                var roster = TaleWorlds.CampaignSystem.Party.MobileParty.MainParty.ItemRoster;
                var r = Recipes.For(item);
                string name = item.Name != null ? item.Name.ToString() : "a shield";
                int velvet = ShieldVelvetNeed(item);
                if (velvet > 0)
                {
                    int before = Recipes.CountInInventory(Recipes.SoftVelvet);
                    Recipes.Take(roster, Recipes.SoftVelvet, velvet);
                    _velvetTaken += before - Recipes.CountInInventory(Recipes.SoftVelvet);
                }
                // kwit BK zdjal tylko ladunki drewna do otwarcia; kg schodza z otwartego ladunku tak jak przy kuciu w naszej kuzni (bez tego drewno z niego za darmo)
                if (SmithWood.On && r.WoodKg > 0f)
                {
                    int loads = 0;
                    try
                    {
                        var cfg = Traverse.CreateWithType("BannerKings.BannerKingsConfig").Property("Instance").GetValue();
                        var model = cfg != null ? Traverse.Create(cfg).Property("SmithingModel").GetValue() : null;
                        var bill = model != null ? Traverse.Create(model).Method("GetCraftingInputForArmor", item).GetValue<int[]>() : null;
                        if (bill != null && bill.Length > (int)CraftingMaterials.Wood) loads = Math.Max(0, bill[(int)CraftingMaterials.Wood]);
                    }
                    catch { }
                    SmithWood.Use(r.WoodKg, loads, name);
                }
                _shieldBills++;
                Log.Info("Tarcza w CRAFT BK (n20-kuznia): " + item.StringId + " | kwit z receptury, drewno " + r.WoodKg.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
                         + " kg, aksamit " + velvet + " | tarcz tak wykutych od startu " + _shieldBills + ", aksamitu zdjetego " + _velvetTaken);
            }
            catch (Exception e) { Log.Error("BkShieldSpend", e); }
        }

        public static void BkHasMaterialsPostfix(object __instance, ref bool __result)
        {
            try
            {
                _velvetShort = null;
                if (!__result) return;
                var item = BkCurrentItem(__instance);
                int need = ShieldVelvetNeed(item);
                if (need <= 0) return;
                int have = Recipes.CountInInventory(Recipes.SoftVelvet);
                if (have >= need) return;
                __result = false;
                var v = Recipes.SoftVelvet;
                _velvetShort = "You don't have all required materials! This shield also needs " + need + " " + (v != null && v.Name != null ? v.Name.ToString() : "velvet")
                               + " for its facing - you have " + have + ".";
            }
            catch (Exception e) { Log.Error("BkHasMaterials", e); }
        }

        public static void BkMainActionPostfix(object __instance)
        {
            try
            {
                string text = _velvetShort;
                if (text == null) return;
                var crafting = Traverse.Create(__instance).Field("crafting").GetValue<TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.CraftingVM>();
                if (crafting == null || crafting.MainActionHint == null) return;
                crafting.MainActionHint = new TaleWorlds.Core.ViewModelCollection.Information.BasicTooltipViewModel(() => text);
            }
            catch (Exception e) { Log.Error("BkMainAction", e); }
        }

        /// <summary>
        /// JEDEN KWIT NA CALA KUZNIE (Jeff 01.09: "sprawdz wszystkie pancerze,
        /// moze gdzies przeoczyles"): zakladka CRAFT BK liczyla pancerze
        /// WLASNYM wzorem (90/10 z jednej puli, tier progami Tierf - T3 i nizej
        /// dostawal Iron3, skora/len wg wlasnego widzimisie). Dwa cenniki na
        /// ten sam pancerz = kucie taniej u BK. Podmieniamy wynik na nasze
        /// Prawo Wagi - wyswietlacz, "czy stac" i zdjecie materialow
        /// (BkSpendMaterialsPrefix czyta te sama metode) widza jedna liczbe.
        /// </summary>
        public static void BkArmorBillPostfix(ItemObject item, ref int[] __result)
        {
            try
            {
                if (item == null || __result == null || __result.Length < 11) return;
                // n20-kuznia (klucz BkShieldBillByRecipe): tarcza w CRAFT BK brala kwit BK (BKSmithingModel.GetCraftingInputForArmor: 2 drewna, zelazo Iron4
                // tylko przy physics "shield_metal") - drewniana tarcza bez grama zelaza, a przetop (SmeltTab, Recipes.For z wagi) oddawal z niej sztaby metalu:
                // petla "metal z niczego". Teraz ten sam kwit co nasza kuznia (Recipes.For: okucia z wagi x ClassCostShield, drewno, wegiel, skora/len).
                bool shield = item.ItemType == ItemObject.ItemTypeEnum.Shield && Settings.Current != null && Settings.Current.BkShieldBillByRecipe;
                if (!item.HasArmorComponent && !shield) return;   // bron biala liczy sie po staremu
                var r = Recipes.For(item);
                if (r.Parts == null || r.Parts.Count == 0) return;
                var bill = new int[11];
                foreach (var p in r.Parts)
                {
                    if (p.Item == null || p.Count <= 0) continue;
                    bool hit = false;
                    for (int m = 0; m < 9 && m < bill.Length; m++)
                        if (Recipes.MaterialItem((CraftingMaterials)m) == p.Item)
                        { bill[m] += p.Count; hit = true; break; }
                    if (hit) continue;
                    if (p.Item == Recipes.SoftLeather) bill[9] += p.Count;
                    else if (p.Item == Recipes.SoftLinen) bill[10] += p.Count;
                }
                __result = bill;
            }
            catch (Exception e) { Log.Error("BkArmorBill", e); }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var c = Settings.Current;
                // kwit pancerzy BK ma byc spojny z Prawem Wagi NIEZALEZNIE od
                // tego, czy luczarnia jest wlaczona - patch przed gatingiem
                if (c != null && c.CraftingEnabled)
                {
                    var tModel = QuartermasterLaw.FindType("BannerKings.Models.Vanilla.BKSmithingModel");
                    var mBill = tModel != null ? AccessTools.Method(tModel, "GetCraftingInputForArmor") : null;
                    if (mBill != null)
                    {
                        h.Patch(mBill, postfix: new HarmonyMethod(typeof(FletchForge), "BkArmorBillPostfix"));
                        Log.Info("FletchForge: kwit pancerzy BK podmieniony na Prawo Wagi.");
                    }
                    // n20-kuznia po recenzji: reszta kwitu tarczy (aksamit, kg drewna) - razem z kwitem tarczy, takze bez AllowRangedCrafting (klucz czytany w latkach)
                    try
                    {
                        var tMix = QuartermasterLaw.FindType("BannerKings.UI.Extensions.CraftingMixin");
                        var mSp = tMix != null ? AccessTools.DeclaredMethod(tMix, "SpendMaterials") : null;
                        var mHas = tMix != null ? AccessTools.DeclaredMethod(tMix, "HasMaterials") : null;
                        var mUpd = tMix != null ? AccessTools.DeclaredMethod(tMix, "UpdateMainAction") : null;
                        if (mSp != null) h.Patch(mSp, postfix: new HarmonyMethod(typeof(FletchForge), "BkShieldSpendPostfix"));
                        if (mHas != null) h.Patch(mHas, postfix: new HarmonyMethod(typeof(FletchForge), "BkHasMaterialsPostfix"));
                        if (mUpd != null) h.Patch(mUpd, postfix: new HarmonyMethod(typeof(FletchForge), "BkMainActionPostfix"));
                        _shieldHooks = (mSp != null ? "zdjecie" : "zdjecie BRAK") + ", " + (mHas != null ? "czy stac" : "czy stac BRAK") + ", " + (mUpd != null ? "podpowiedz" : "podpowiedz BRAK");
                    }
                    catch (Exception e2) { _shieldHooks = "wyjatek " + e2.GetType().Name; Log.Error("FletchForge.ApplyAll(tarcza)", e2); }
                }
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
