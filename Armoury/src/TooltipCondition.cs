using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;

namespace Armoury
{
    /// <summary>
    /// Tooltip przedmiotu mowi PELNA prawde o zuzyciu - to, o co prosil Jeff:
    /// osobna sekcja "Condition X%" i kazda zmieniona statystyka jako
    /// "baza -> teraz (-roznica)". Zadnego zgadywania, co zabral stan.
    /// </summary>
    [HarmonyPatch(typeof(TooltipRefresherCollection), "RefreshItemTooltip")]
    internal static class TooltipConditionPatch
    {
        private static void Postfix(PropertyBasedTooltipVM propertyBasedTooltipVM, object[] args)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.ShowConditionPercent) return;
                var elN = args != null && args.Length > 0 ? args[0] as EquipmentElement? : null;
                if (elN == null) return;
                var el = elN.Value;
                var item = el.Item;
                var m = el.ItemModifier;
                if (item == null || m == null || ArmouryBehavior.NoState(item)) return;   // E8/C: NoState (bron miotana ze stanem dalej go pokazuje)
                float pm = ConditionScaling.ConditionOf(m);   // wpis 95: stan lupu Spoils wedlug ich skali
                if (pm >= 0.999f || pm <= 0f) return;
                int pct = Math.Max(1, (int)Math.Round(pm * 100f));

                var vm = propertyBasedTooltipVM;
                vm.AddProperty(" ", " ");
                vm.AddProperty(" ", " ", 0, TooltipProperty.TooltipPropertyFlags.RundownSeperator);
                vm.AddProperty("Condition", GearState.CondText(m, pct));   // m110-stan: slowo stanu zamiast procentu

                if (item.HasArmorComponent)
                {
                    var a = item.ArmorComponent;
                    if (item.Type == ItemObject.ItemTypeEnum.HorseHarness)
                        Row(vm, "Horse Armor", a.BodyArmor, el.GetModifiedMountBodyArmor());
                    else
                    {
                        Row(vm, "Head Armor", a.HeadArmor, el.GetModifiedHeadArmor());
                        Row(vm, "Body Armor", a.BodyArmor, el.GetModifiedBodyArmor());
                        Row(vm, "Leg Armor", a.LegArmor, el.GetModifiedLegArmor());
                        Row(vm, "Arm Armor", a.ArmArmor, el.GetModifiedArmArmor());
                    }
                }
                if (item.HasWeaponComponent && item.WeaponComponent.PrimaryWeapon != null)
                {
                    var w = item.WeaponComponent.PrimaryWeapon;
                    Row(vm, "Swing Damage", w.SwingDamage, el.GetModifiedSwingDamageForUsage(0));
                    Row(vm, "Thrust Damage", w.ThrustDamage, el.GetModifiedThrustDamageForUsage(0));
                    Row(vm, "Swing Speed", w.SwingSpeed, el.GetModifiedSwingSpeedForUsage(0));
                    Row(vm, "Thrust Speed", w.ThrustSpeed, el.GetModifiedThrustSpeedForUsage(0));
                    Row(vm, "Missile Damage", w.MissileDamage, el.GetModifiedMissileDamageForUsage(0));
                    Row(vm, "Missile Speed", w.MissileSpeed, el.GetModifiedMissileSpeedForUsage(0));
                    Row(vm, "Hit Points", w.MaxDataValue, el.GetModifiedMaximumHitPointsForUsage(0));
                }
            }
            catch (Exception e) { Log.Error("TooltipCondition", e); }
        }

        /// <summary>Wiersz "24 -> 12 (-12)" - tylko gdy stan cos faktycznie zabral.</summary>
        private static void Row(PropertyBasedTooltipVM vm, string name, int baseVal, int nowVal)
        {
            try
            {
                if (baseVal <= 0 || nowVal == baseVal) return;
                int d = nowVal - baseVal;
                vm.AddProperty(name, baseVal + " → " + nowVal + "  (" + (d > 0 ? "+" : "") + d + ")");
            }
            catch { }
        }
    }

    /// <summary>
    /// Panel przedmiotu w EKWIPUNKU (ten z obrazkiem, w ktorym RBM dopisuje
    /// "RBM Stats") - dopisujemy wlasna sekcje "Armoury Stats":
    /// stan w punktach (3/100) i kazda zmieniona statystyke jako baza -> teraz (-roznica).
    /// </summary>
    [HarmonyPatch(typeof(TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.ItemMenuVM), "SetItem")]
    internal static class ArmouryStatsPatch
    {
        private static System.Reflection.FieldInfo _targetField;

        private static void Postfix(TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.ItemMenuVM __instance,
                                    [HarmonyArgument("item")] TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.SPItemVM spItem, BasicCharacterObject character)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.ShowConditionPercent) return;
                if (_targetField == null)
                    _targetField = AccessTools.Field(typeof(TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.ItemMenuVM), "_targetItem");
                var itemVm = _targetField != null ? _targetField.GetValue(__instance) as TaleWorlds.Core.ViewModelCollection.ItemVM : null;
                if (itemVm == null) return;
                var el = itemVm.ItemRosterElement.EquipmentElement;
                var item = el.Item;
                if (item == null) return;
                // sekcja pokazuje sie dla KAZDEJ zbroi i broni - nowka ma 100/100 (New);
                // amunicja (strzaly/belty) jest POZA systemem zuzycia - bez sekcji
                if (!item.HasArmorComponent && !item.HasWeaponComponent) return;
                if (ArmouryBehavior.NoState(item)) return;   // E8/C: NoState
                var m = el.ItemModifier;
                if (GearState.Handles(item) && GearState.Of(m) >= 0)
                {
                    // m110-stan (GearFourStates): stan slowem - gorszy z (stan modyfikatora, stan z ksiegi zuzycia sztuki na grzbiecie); wytrzymalosc w punktach
                    // tylko, gdy znana dokladnie (ksiega) albo sztuka sprawna bez ksiegi
                    int st = GearState.Of(m);
                    float dEx = -1f;
                    // m110-stan krok 2 (WornExactHpProtection): dokladna wytrzymalosc tylko sztuki noszonej w bitwie przez te postac (gracz albo towarzysz) - sakwy,
                    // kupiec i stroj cywilny bez niej (dotad WornEffective szukal po samym przedmiocie u gracza: kopia w sakwach dostawala HP sztuki z grzbietu)
                    bool exact = ExactOn();
                    if (exact) { try { dEx = WornD(spItem, character, el); } catch { dEx = -1f; } }
                    else try { var beh0 = ArmouryBehavior.Instance; if (beh0 != null) dEx = beh0.WornEffective(item); } catch { dEx = -1f; }
                    if (dEx >= 0f) st = Math.Max(st, GearState.OfCondition(dEx));
                    var list0 = __instance.TargetItemProperties;
                    if (list0 == null) return;
                    Add(list0, " ", " ");
                    Add(list0, "Armoury Stats", " ");
                    Add(list0, "Condition", GearState.En[st] + (st == GearState.Sound && m == null && (dEx < 0f || dEx >= 99.95f) ? "  (New)" : ""));
                    if (item.HasArmorComponent)
                    {
                        int pool0 = ArmouryBehavior.ArmorPool(item);
                        if (pool0 > 0 && dEx >= 0f) Add(list0, "Durability", (int)Math.Round(pool0 * Math.Min(100f, dEx) / 100.0) + " / " + pool0);
                        else if (pool0 > 0 && st == GearState.Sound) Add(list0, "Durability", pool0 + " / " + pool0);
                    }
                    // m110-stan krok 2: sztuka noszona z dokladna wytrzymaloscia (takze sprawna, d < 100) - pancerz i obrazenia jak w bitwie (HeroHp.Exact)
                    bool exRows = exact && dEx >= 0f && dEx < 99.95f;
                    if (!exRows && (m == null || m.PriceMultiplier >= 0.999f)) return;   // bez modyfikatora stanu - bez rozpiski strat
                    if (item.HasArmorComponent)
                    {
                        var a0 = item.ArmorComponent;
                        if (item.Type == ItemObject.ItemTypeEnum.HorseHarness)
                            Delta(list0, "Horse Armor", a0.BodyArmor, Ex(exRows, a0.BodyArmor, el.GetModifiedMountBodyArmor(), m, dEx));
                        else
                        {
                            Delta(list0, "Head Armor", a0.HeadArmor, Ex(exRows, a0.HeadArmor, el.GetModifiedHeadArmor(), m, dEx));
                            Delta(list0, "Body Armor", a0.BodyArmor, Ex(exRows, a0.BodyArmor, el.GetModifiedBodyArmor(), m, dEx));
                            Delta(list0, "Leg Armor", a0.LegArmor, Ex(exRows, a0.LegArmor, el.GetModifiedLegArmor(), m, dEx));
                            Delta(list0, "Arm Armor", a0.ArmArmor, Ex(exRows, a0.ArmArmor, el.GetModifiedArmArmor(), m, dEx));
                        }
                    }
                    if (item.HasWeaponComponent && item.WeaponComponent.PrimaryWeapon != null)
                    {
                        var w0 = item.WeaponComponent.PrimaryWeapon;
                        Delta(list0, "Swing Damage", w0.SwingDamage, Ex(exRows, w0.SwingDamage, el.GetModifiedSwingDamageForUsage(0), m, dEx));
                        Delta(list0, "Thrust Damage", w0.ThrustDamage, Ex(exRows, w0.ThrustDamage, el.GetModifiedThrustDamageForUsage(0), m, dEx));
                        Delta(list0, "Swing Speed", w0.SwingSpeed, el.GetModifiedSwingSpeedForUsage(0));
                        Delta(list0, "Thrust Speed", w0.ThrustSpeed, el.GetModifiedThrustSpeedForUsage(0));
                        Delta(list0, "Missile Damage", w0.MissileDamage, Ex(exRows, w0.MissileDamage, el.GetModifiedMissileDamageForUsage(0), m, dEx));
                        Delta(list0, "Missile Speed", w0.MissileSpeed, el.GetModifiedMissileSpeedForUsage(0));
                        Delta(list0, "Hit Points", w0.MaxDataValue, el.GetModifiedMaximumHitPointsForUsage(0));
                    }
                    return;
                }
                float pm = m != null ? m.PriceMultiplier : 1f;
                bool damaged = m != null && pm < 0.999f && pm > 0f;
                int pct = damaged ? Math.Max(1, (int)Math.Round(pm * 100f)) : 100;
                // ta sztuka na Twoim grzbiecie ma WLASNY rachunek w ksiedze zuzycia -
                // i to on jest prawda, a nie etykieta, ktora dochodzi dopiero przy progu
                try
                {
                    var beh = ArmouryBehavior.Instance;
                    float ledger = beh != null ? beh.WornCondition(item) : -1f;
                    if (ledger >= 0f && ledger < pct) { pct = Math.Max(1, (int)Math.Round(ledger)); damaged = pct < 100; }
                }
                catch { }

                var list = __instance.TargetItemProperties;
                if (list == null) return;
                Add(list, " ", " ");
                Add(list, "Armoury Stats", " ");
                Add(list, "Condition", pct + " / 100" + (damaged ? "" : "  (New)"));
                if (item.HasArmorComponent)
                {
                    int pool = ArmouryBehavior.ArmorPool(item);
                    if (pool > 0)
                    {
                        int cur = (int)Math.Round(pool * pct / 100.0);
                        Add(list, "Durability", cur + " / " + pool);
                    }
                }
                if (!damaged) return;   // nowka - bez rozpiski strat

                if (item.HasArmorComponent)
                {
                    var a = item.ArmorComponent;
                    if (item.Type == ItemObject.ItemTypeEnum.HorseHarness)
                        Delta(list, "Horse Armor", a.BodyArmor, el.GetModifiedMountBodyArmor());
                    else
                    {
                        Delta(list, "Head Armor", a.HeadArmor, el.GetModifiedHeadArmor());
                        Delta(list, "Body Armor", a.BodyArmor, el.GetModifiedBodyArmor());
                        Delta(list, "Leg Armor", a.LegArmor, el.GetModifiedLegArmor());
                        Delta(list, "Arm Armor", a.ArmArmor, el.GetModifiedArmArmor());
                    }
                }
                if (item.HasWeaponComponent && item.WeaponComponent.PrimaryWeapon != null)
                {
                    var w = item.WeaponComponent.PrimaryWeapon;
                    Delta(list, "Swing Damage", w.SwingDamage, el.GetModifiedSwingDamageForUsage(0));
                    Delta(list, "Thrust Damage", w.ThrustDamage, el.GetModifiedThrustDamageForUsage(0));
                    Delta(list, "Swing Speed", w.SwingSpeed, el.GetModifiedSwingSpeedForUsage(0));
                    Delta(list, "Thrust Speed", w.ThrustSpeed, el.GetModifiedThrustSpeedForUsage(0));
                    Delta(list, "Missile Damage", w.MissileDamage, el.GetModifiedMissileDamageForUsage(0));
                    Delta(list, "Missile Speed", w.MissileSpeed, el.GetModifiedMissileSpeedForUsage(0));
                    Delta(list, "Hit Points", w.MaxDataValue, el.GetModifiedMaximumHitPointsForUsage(0));
                }
            }
            catch (Exception e) { Log.Error("ArmouryStats", e); }
        }

        /// <summary>m110-stan krok 2: panel przy kluczu WornExactHpProtection (z GearFourStates i ConditionScalesStats - jak w bitwie); NIE - panel jak 6f3e0314.</summary>
        private static bool ExactOn()
        {
            var s = Settings.Current;
            return s != null && s.WornExactHpProtection && s.ConditionScalesStats && GearState.On;
        }

        /// <summary>m110-stan krok 2: wartosc wiersza z dokladnego HP (HeroHp.Exact - ta sama funkcja co w bitwie); wylaczone albo baza 0 - wartosc gry.</summary>
        private static int Ex(bool on, int baseVal, int now, ItemModifier m, float d)
        {
            return on && baseVal > 0 ? HeroHp.Exact(baseVal, now, m, d) : now;
        }

        /// <summary>m110-stan krok 2: dokladna wytrzymalosc (procent puli, przycieta do przedzialu stanu jak w bitwie) sztuki noszonej w bitwie przez te postac -
        /// panel ekwipunku po stronie rynsztunku bojowego (gniazdo z ItemType), ten sam przedmiot i modyfikator co w BattleEquipment bohatera; -1 - sakwy,
        /// kupiec, stroj cywilny, inna sztuka albo ksiega jej nie zna.</summary>
        private static float WornD(TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.SPItemVM vm, BasicCharacterObject character, EquipmentElement el)
        {
            if (vm == null || el.Item == null || vm.InventorySide != TaleWorlds.CampaignSystem.Inventory.InventoryLogic.InventorySide.BattleEquipment) return -1f;
            int slot = (int)vm.ItemType;
            if (slot < 0 || slot >= 12) return -1f;
            var co = character as TaleWorlds.CampaignSystem.CharacterObject;
            var hero = co != null ? co.HeroObject : null;
            if (hero == null || TaleWorlds.CampaignSystem.Campaign.Current == null) return -1f;
            var heq = hero.BattleEquipment;
            if (heq == null || ReferenceEquals(heq, TaleWorlds.CampaignSystem.Campaign.Current.DeadBattleEquipment)) return -1f;
            var hel = heq[slot];
            if (hel.Item != el.Item || !ReferenceEquals(hel.ItemModifier, el.ItemModifier)) return -1f;
            if (ArmouryBehavior.NoWear(el.Item) || !GearState.Handles(el.Item)) return -1f;
            int st = GearState.Of(el.ItemModifier);
            if (st < 0) return -1f;
            float d;
            if (hero == TaleWorlds.CampaignSystem.Hero.MainHero)
            {
                var beh = ArmouryBehavior.Instance;
                if (beh == null || !beh.WornExactD(slot, out d)) return -1f;
            }
            else return -1f;
            bool clipped;
            return HeroHp.Clip(d, st, out clipped);
        }

        private static void Add(
            TaleWorlds.Library.MBBindingList<TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.ItemMenuTooltipPropertyVM> list,
            string name, string value)
        {
            list.Add(new TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.ItemMenuTooltipPropertyVM(name, value, 0, false, null, null, false));
        }

        private static void Delta(
            TaleWorlds.Library.MBBindingList<TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.ItemMenuTooltipPropertyVM> list,
            string name, int baseVal, int nowVal)
        {
            if (baseVal <= 0 || nowVal == baseVal) return;
            int d = nowVal - baseVal;
            Add(list, name, baseVal + " → " + nowVal + "  (" + (d > 0 ? "+" : "") + d + ")");
        }
    }
}
