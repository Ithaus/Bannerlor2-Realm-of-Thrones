using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// PRZETOP W ZAKLADCE SMELT (kuznia 1:1, krok 3). Vanilla lista przetopu
    /// pokazuje tylko bron z projektem (IsCraftedWeapon), a wyjscie liczy
    /// z WeaponDesign - pancerz nie mial tam wstepu, u nas zyl osobny
    /// tygiel w menu miasta. Trzy latki daja jeden wspolny przetop:
    ///  - SmeltingVM.RefreshList (postfix): dokladamy metalowe pancerze/tarcze
    ///    z sakw do listy (ten sam SmeltingItemVM, te same callbacki VM przez
    ///    delegaty); luki/kusze wyjete 03.09 - drewna sie nie przetapia;
    ///  - SmithingModel.GetSmeltingOutputForItem (postfix): dla przedmiotu
    ///    bez WeaponDesign wyjscie liczy nasza receptura (SmeltYield, udzial
    ///    rosnie ze Smithing) - panel materialow pokazuje prawde;
    ///  - CraftingCampaignBehavior.DoSmelting (prefix): przedmiot bez
    ///    WeaponDesign idzie nasza sciezka (materialy, XP, stamina, nauka
    ///    wzorow do RangedLore) - oryginal wolal item.WeaponDesign.Template
    ///    i na pancerzu skonczyloby sie NullReference.
    /// </summary>
    internal static class SmeltTab
    {
        /// <summary>Nasz kandydat do tygla: metalowy pancerz/tarcza z niepustym odzyskiem (luki/kusze wyjete - drewno).</summary>
        internal static bool OurSmeltable(ItemObject it)
        {
            try
            {
                if (it == null || it.WeaponDesign != null) return false;   // bron z projektem robi vanilla
                // LUKI/KUSZE POZA TYGLEM (Jeff 03.09: "lukow i kusz nie da sie
                // melted, to przeciez drewno!") - drewna sie nie przetapia.
                // Nauka wzorow luków zostaje droga rozbiorki u kowala (TakeApart),
                // a SmeltYield i tak oddawal z nich tylko zelazne okucia.
                bool armour = (it.HasArmorComponent || it.ItemType == ItemObject.ItemTypeEnum.Shield)
                              && Recipes.IsMetalwork(it);
                if (!armour) return false;
                return YieldFor(it).Count > 0;
            }
            catch { return false; }
        }

        /// <summary>Odzysk wedle naszej receptury - ten sam co w starym tyglu.</summary>
        internal static List<Recipes.Part> YieldFor(ItemObject it)
        {
            var s = Settings.Current;
            var r = Recipes.For(it);
            int skill = Hero.MainHero.GetSkillValue(DefaultSkills.Crafting);
            float share = MathF.Min(0.9f, s.SmeltingReturnShare + skill * s.SmeltingSkillBonus);
            return Recipes.SmeltYield(r, share);
        }

        private static int MaterialIndex(ItemObject mat)
        {
            for (int i = 0; i < 9; i++)
                if (Recipes.MaterialItem((CraftingMaterials)i) == mat) return i;
            return -1;
        }

        /// <summary>Wyjscie przetopu dla przedmiotow bez WeaponDesign - z naszej receptury.</summary>
        public static void OutputPostfix(ItemObject item, ref int[] __result)
        {
            try
            {
                if (item == null || item.WeaponDesign != null || __result == null) return;
                if (!OurSmeltable(item)) return;
                foreach (var p in YieldFor(item))
                {
                    int idx = MaterialIndex(p.Item);
                    if (idx >= 0 && idx < __result.Length) __result[idx] += p.Count;
                }
            }
            catch (Exception e) { Log.Error("SmeltTab.Output", e); }
        }

        /// <summary>Przetop przedmiotu bez WeaponDesign - nasza sciezka zamiast vanilla (tam czeka NullReference).</summary>
        public static bool DoSmeltingPrefix(CraftingCampaignBehavior __instance, Hero currentCraftingHero, EquipmentElement equipmentElement)
        {
            try
            {
                var item = equipmentElement.Item;
                if (item == null || item.WeaponDesign != null) return true;

                var model = Campaign.Current.Models.SmithingModel;
                var roster = MobileParty.MainParty.ItemRoster;

                // STRAZNIK UJEMNYCH SAKW (Jeff 27.08: "discarduje armour i znika
                // caly ekwipunek"). Bez eventu przetopu (nizej) lista Smelt nie
                // odswiezala sie i po zdjeciu OSTATNIEJ sztuki pozycja dalej
                // byla klikalna - AddToCounts(-1) na nieistniejacym wpisie robil
                // UJEMNY stos, a ekran ekwipunku z takim rosterem pokazuje pustke
                // we WSZYSTKICH kategoriach. Nie ma sztuki = nie ma przetopu.
                int idx = roster.FindIndexOfElement(equipmentElement);
                if (idx < 0 || roster.GetElementNumber(idx) <= 0)
                {
                    Log.Info("Przetop (Smelt): pozycji " + item.StringId + " juz nie ma w sakwach - klik zignorowany.");
                    return false;
                }

                var outp = model.GetSmeltingOutputForItem(item);
                for (int i = 8; i >= 0; i--)
                    if (outp[i] != 0) roster.AddToCounts(model.GetCraftingMaterialItem((CraftingMaterials)i), outp[i]);
                roster.AddToCounts(equipmentElement, -1);
                currentCraftingHero.AddSkillXp(DefaultSkills.Crafting, model.GetSkillXpForSmelting(item));
                int cost = model.GetEnergyCostForSmelting(item, currentCraftingHero);
                __instance.SetHeroCraftingStamina(currentCraftingHero, __instance.GetHeroCraftingStamina(currentCraftingHero) - cost);
                // zamiast vanillowych badan czesci: przetop nieznanego wzoru uczy go
                // w calosci, znanego - punkty i losowe odkrycie nie wyzej niz jego tier
                RangedLore.OnSmelted(item);   // unikaty tez: OnSmelted -> Learn -> LearnUnique
                // event jak w vanilli - na nim UI odswieza liste przetopu
                // (bez niego martwe pozycje zostawaly klikalne)
                try
                {
                    var disp = CampaignEventDispatcher.Instance;
                    var mEv = AccessTools.Method(disp.GetType(), "OnEquipmentSmeltedByHero");
                    if (mEv != null) mEv.Invoke(disp, new object[] { currentCraftingHero, equipmentElement });
                }
                catch { }
                Log.Info("Przetop (zakladka Smelt): " + item.StringId);
                return false;
            }
            catch (Exception e) { Log.Error("SmeltTab.DoSmelting", e); return true; }
        }

        /// <summary>
        /// ROZEBRANA LEGENDA ODDAJE KOMPLET SWOICH CZESCI (Jeff 29.08: "musze
        /// moc smelt Brightroar i dostac od razu wszystkie czesci, bo jest
        /// jedna na swiecie - zeby nie bylo, ze czesci nie dostane i nie zloze").
        /// Czesci legend sa ukryte w projektowniku (LegendaryLaw.LockLegendPieces),
        /// wiec jedyna droga do odkucia legendy to POSIADAC i rozebrac oryginal:
        /// wtedy wszystkie jej czesci zostaja odkryte i odblokowane.
        /// </summary>
        public static void DoSmeltingPostfix(CraftingCampaignBehavior __instance, Hero currentCraftingHero, EquipmentElement equipmentElement)
        {
            try
            {
                var item = equipmentElement.Item;
                if (item == null || item.WeaponDesign == null) return;
                if (!LegendaryLaw.IsLegend(item)) return;
                var design = item.WeaponDesign;
                var template = AccessTools.Field(typeof(WeaponDesign), "Template").GetValue(design) as CraftingTemplate;
                var fHide = AccessTools.Field(typeof(CraftingPiece), "<IsHiddenOnDesigner>k__BackingField");
                var mOpen = AccessTools.Method(typeof(CraftingCampaignBehavior), "OpenPart");
                int opened = 0;
                foreach (var el in design.UsedPieces)
                {
                    var piece = el != null ? el.CraftingPiece : null;
                    if (piece == null) continue;
                    if (fHide != null && piece.IsHiddenOnDesigner) fHide.SetValue(piece, false);
                    if (mOpen != null && template != null)
                        try { mOpen.Invoke(__instance, new object[] { piece, template, true }); } catch { }
                    opened++;
                }
                if (opened > 0)
                {
                    Log.Player("The " + item.Name + " is broken down to its bones - all " + opened
                               + " parts are yours, and yours alone, to reforge.", true);
                    Log.Info("SmeltTab: legenda " + item.StringId + " rozebrana - " + opened + " czesci odkrytych i odblokowanych.");
                }
            }
            catch (Exception e) { Log.Error("SmeltTab.DoSmeltingPostfix", e); }
        }

        /// <summary>Lista przetopu: za vanillowymi broniami dokladamy nasze pancerze i luki.</summary>
        public static void RefreshListPostfix(object __instance)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.CraftingEnabled) return;
                var list = Traverse.Create(__instance).Property("SmeltableItemList").GetValue() as System.Collections.IList;
                if (list == null) return;
                var tVm = __instance.GetType();
                var mSel = AccessTools.Method(tVm, "OnItemSelection");
                var mLock = AccessTools.Method(tVm, "ProcessLockItem");
                var mIsLocked = AccessTools.Method(tVm, "IsItemLocked");
                var tItem = QuartermasterLaw.FindType(
                    "TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.Smelting.SmeltingItemVM");
                if (mSel == null || mLock == null || tItem == null) return;

                var dSelType = typeof(Action<>).MakeGenericType(tItem);
                var dLockType = typeof(Action<,>).MakeGenericType(tItem, typeof(bool));
                var dSel = Delegate.CreateDelegate(dSelType, __instance, mSel);
                var dLock = Delegate.CreateDelegate(dLockType, __instance, mLock);
                var ctor = AccessTools.Constructor(tItem,
                    new[] { typeof(EquipmentElement), dSelType, dLockType, typeof(bool), typeof(int) });
                if (ctor == null) { Log.Info("SmeltTab: brak konstruktora SmeltingItemVM - lista bez pancerzy."); return; }

                var roster = MobileParty.MainParty.ItemRoster;
                int added = 0;
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (it == null || el.Amount <= 0 || it.IsCraftedWeapon) continue;
                    if (!OurSmeltable(it)) continue;
                    bool locked = false;
                    try { if (mIsLocked != null) locked = (bool)mIsLocked.Invoke(__instance, new object[] { el.EquipmentElement }); }
                    catch { }
                    list.Add(ctor.Invoke(new object[] { el.EquipmentElement, dSel, dLock, locked, el.Amount }));
                    added++;
                }
                if (added > 0) Log.Info("SmeltTab: dolozono " + added + " pozycji (pancerze) do listy przetopu.");
            }
            catch (Exception e) { Log.Error("SmeltTab.RefreshList", e); }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.CraftingEnabled) { Log.Info("SmeltTab: wylaczone."); return; }

                var tVm = QuartermasterLaw.FindType(
                    "TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.Smelting.SmeltingVM");
                var mRefresh = tVm != null ? AccessTools.Method(tVm, "RefreshList") : null;
                if (mRefresh != null)
                    h.Patch(mRefresh, postfix: new HarmonyMethod(typeof(SmeltTab), "RefreshListPostfix"));

                var mDo = AccessTools.Method(typeof(CraftingCampaignBehavior), "DoSmelting");
                if (mDo != null)
                    h.Patch(mDo, prefix: new HarmonyMethod(typeof(SmeltTab), "DoSmeltingPrefix"),
                                 postfix: new HarmonyMethod(typeof(SmeltTab), "DoSmeltingPostfix"));

                // wyjscie przetopu: kazdy model dziedziczacy po SmithingModel (RBM/BK moga podmieniac)
                int outs = 0;
                var post = new HarmonyMethod(typeof(SmeltTab), "OutputPostfix");
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException rtle) { types = rtle.Types; }
                    catch { continue; }
                    foreach (var t in types)
                    {
                        if (t == null || t.IsAbstract) continue;
                        if (!typeof(TaleWorlds.CampaignSystem.ComponentInterfaces.SmithingModel).IsAssignableFrom(t)) continue;
                        var m = t.GetMethod("GetSmeltingOutputForItem", BindingFlags.Public | BindingFlags.NonPublic |
                                                                        BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        if (m == null || m.IsAbstract) continue;
                        try { h.Patch(m, postfix: post); outs++; } catch { }
                    }
                }
                Log.Info("SmeltTab: zakladka Smelt przyjmuje pancerze i luki (lista=" + (mRefresh != null)
                         + ", przetop=" + (mDo != null) + ", wyjscie w " + outs + " modelach).");
            }
            catch (Exception e) { Log.Error("SmeltTab.ApplyAll", e); }
        }
    }
}
