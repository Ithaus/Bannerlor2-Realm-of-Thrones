using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// WEGIEL BRONI Z EKRANU KUZNI W KILOGRAMACH (noc 16.10, poprawka po recenzji N2; klucz SmithCoalKg).
    /// DOWOD (kod gry): DefaultSmithingModel.GetSmithingCostsForWeaponDesign konczy sie "array[8]--" - kazda bron wykuta na ekranie kuzni gry bierze 1 bryle
    /// wegla; GetSmeltingOutputForItem (AddSmeltingReductions: quantities[8]--) - przetop broni tez 1 bryle. BKSmithingModel kosztu nie nadpisuje, przetop nadpisuje
    /// (wola baze i przycina tylko metal). Do N2 bryla wazyla 0.5 kg (klucz CharcoalWeight), od N2 - waga gry 5 kg: ladunek drewna (100 kg) daje 4 bryly, czyli
    /// wegiel na 4 bronie zamiast 40 - 10x wiecej drewna na kucie broni, a zbroja z naszych przepisow liczy wegiel w kg na kg zelaza (Recipes.CoalLumpScale).
    /// REGULA (jak drewno w kuzni, SmithWood): w palenisku kowala gracza zarzy sie otwarta bryla - zapas w kg (zapis arm_smith_coal_kg w SmithWoodBehavior).
    /// Sztuka broni wykuta albo przetopiona na ekranie kuzni = PieceKg (0.5 kg, dawna bryla) x bryly gry. Kuznia bierze nowa bryle z sakw dopiero, gdy zapas
    /// nie starcza; ekran pokazuje wtedy 1 wegiel, inaczej 0 (ten sam koszt czyta gra przy pobraniu - SpendMaterials, DoSmelting). Przetop rudy i sztab
    /// (MaterialLaw, receptury z wag) i zbroja z naszych przepisow (Recipes) - bez zmian (cale bryly).
    /// Zamknieta ekonomia: bryla schodzi z sakw gracza jak dotad (tylko rzadziej); kg z otwartej bryly to wegiel tej samej bryly - nic z niczego.
    /// Wylaczony klucz: jak gra (1 bryla 5 kg na bron). Bryla 0.5 kg (CharcoalWeightKg 0.5) - regula daje 1 bryle na bron, czyli tez jak gra.
    /// </summary>
    internal static class SmithCoal
    {
        internal const float PieceKg = 0.5f;               // kg wegla na bryle gry w koszcie broni i przetopu (dawna bryla 0.5 kg - ten sam wegiel co przed N2)
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal static float OpenKg;                      // zapas otwartej bryly w palenisku (kg) - w zapisie
        private static readonly HashSet<System.Reflection.MethodBase> _patched = new HashSet<System.Reflection.MethodBase>();
        private static Harmony _h;
        private static string _hook = "nie wpiete";
        private static int _errors, _logged;
        internal static string CheckText = "nie sprawdzone";
        /// <summary>Linia doby NightEcon (zeruje NightEcon): kuc i przetopow z weglem z otwartej bryly, kg, bryl wzietych z sakw, podpowiedzi ekranu, potkniecia.</summary>
        internal static int DayUses, DayLumps, ScreenHints, DayStumbles; internal static double DayKg;

        internal static bool On { get { var s = Settings.Current; return s != null && s.SmithCoalKg; } }

        internal static float LumpKg
        {
            get
            {
                try { var it = Recipes.MaterialItem(CraftingMaterials.Charcoal); return it != null && it.Weight > 0.05f ? it.Weight : PieceKg; }
                catch { return PieceKg; }
            }
        }

        private static string F(double v) { return v.ToString("0.##", Inv); }

        private static void Stumble(string where, Exception e)
        {
            _errors++; DayStumbles++;
            if (_errors <= 5 || _errors % 100 == 0) Log.Error("SmithCoal." + where + " (potkniecie " + _errors + ")", e);
        }

        /// <summary>Ile bryl trzeba wziac z sakw na kg wegla (0, gdy zapas otwartej bryly starcza).</summary>
        internal static int LumpsFor(float kg)
        {
            if (kg <= 0f) return 0;
            float lack = kg - Math.Max(0f, OpenKg);
            return lack <= 0.0001f ? 0 : (int)Math.Ceiling(lack / Math.Max(0.05f, LumpKg) - 1e-6);
        }

        /// <summary>Kucie / przetop spalily kg wegla, a kuznia wziela lumps bryl z sakw.</summary>
        internal static void Use(float kg, int lumps, string what)
        {
            try
            {
                if (kg <= 0f && lumps <= 0) return;
                OpenKg = Math.Max(0f, OpenKg + lumps * LumpKg - Math.Max(0f, kg));
                DayUses++; DayLumps += Math.Max(0, lumps); DayKg += Math.Max(0f, kg);
                if (lumps > 0)
                    Log.Player("Charcoal at the forge: a new " + F(LumpKg) + " kg lump lit" + (string.IsNullOrEmpty(what) ? "" : " for " + what) + "; "
                               + OpenKg.ToString("0.#", Inv) + " kg left in the fire.");
                if (_logged < 20 || lumps > 0)
                {
                    _logged++;
                    Log.Info("Wegiel w kuzni (kg): " + (what ?? "-") + " " + F(kg) + " kg, bryl z sakw " + lumps + ", zapas " + F(OpenKg) + " kg (bryla " + F(LumpKg) + " kg).");
                }
            }
            catch (Exception e) { Stumble("Use", e); }
        }

        // ------------------------------------------------------------ bron z ekranu kuzni gry
        /// <summary>Bryly wegla, ktore gra liczy wykuciu broni: wegiel czesci projektu + 1 (DefaultSmithingModel: "array[8]--").</summary>
        private static int RawForge(WeaponDesign d)
        {
            int raw = 1;
            if (d == null || d.UsedPieces == null) return raw;
            foreach (var el in d.UsedPieces)
            {
                if (el == null || !el.IsValid || el.CraftingPiece == null || el.CraftingPiece.MaterialsUsed == null) continue;
                foreach (var m in el.CraftingPiece.MaterialsUsed) if (m.Item1 == CraftingMaterials.Charcoal) raw += m.Item2;
            }
            return raw;
        }

        /// <summary>Bryly wegla, ktore gra bierze przy przetopie broni (AddSmeltingReductions: 1 minus wegiel oddany z czesci); 0 - przetop bez wegla.</summary>
        private static int RawSmelt(ItemObject it)
        {
            if (it == null || it.WeaponDesign == null || it.WeaponDesign.UsedPieces == null) return 0;
            int back = 0;
            foreach (var el in it.WeaponDesign.UsedPieces)
            {
                if (el == null || !el.IsValid || el.CraftingPiece == null || el.CraftingPiece.MaterialsUsed == null) continue;
                foreach (var m in el.CraftingPiece.MaterialsUsed) if (m.Item1 == CraftingMaterials.Charcoal) back += m.Item2;
            }
            return Math.Max(0, 1 - back);
        }

        /// <summary>Postfiks Priority.Last GetSmithingCostsForWeaponDesign (metoda klasy deklarujacej - DefaultSmithingModel): wegiel = bryly do wziecia z sakw.</summary>
        public static void CostPostfix(ref int[] __result)
        {
            try
            {
                int c = (int)CraftingMaterials.Charcoal;
                if (!On || __result == null || __result.Length <= c || __result[c] >= 0) return;
                __result[c] = -LumpsFor(-__result[c] * PieceKg);
            }
            catch (Exception e) { Stumble("CostPostfix", e); }
        }

        public static void SpendPrefix(WeaponDesign weaponDesign, out float[] __state)
        {
            __state = null;
            try
            {
                if (!On || weaponDesign == null) return;
                float kg = RawForge(weaponDesign) * PieceKg;
                __state = new float[] { kg, LumpsFor(kg) };
            }
            catch (Exception e) { Stumble("SpendPrefix", e); }
        }

        public static void SpendPostfix(WeaponDesign weaponDesign, float[] __state)
        {
            if (__state == null) return;
            string name = null;
            try { name = weaponDesign != null && weaponDesign.WeaponName != null ? weaponDesign.WeaponName.ToString() : null; } catch { }
            Use(__state[0], (int)__state[1], string.IsNullOrEmpty(name) ? "the blade" : name);
        }

        // ------------------------------------------------------------ przetop broni (zakladka Smelt)
        /// <summary>Postfiks Priority.Last GetSmeltingOutputForItem czynnego modelu (metoda klasy deklarujacej - BKSmithingModel albo DefaultSmithingModel; po
        /// SmeltTab.OutputPostfix): wegiel przetopu broni = bryly do wziecia z sakw. Pancerze (bez WeaponDesign) - nasza receptura, bez zmian.</summary>
        public static void SmeltPostfix(ItemObject item, ref int[] __result)
        {
            try
            {
                int c = (int)CraftingMaterials.Charcoal;
                if (!On || item == null || item.WeaponDesign == null || __result == null || __result.Length <= c || __result[c] >= 0) return;
                __result[c] = -LumpsFor(-__result[c] * PieceKg);
            }
            catch (Exception e) { Stumble("SmeltPostfix", e); }
        }

        public static void DoSmeltPrefix(EquipmentElement equipmentElement, out float[] __state)
        {
            __state = null;
            try
            {
                var it = equipmentElement.Item;
                if (!On || it == null || it.WeaponDesign == null) return;
                float kg = RawSmelt(it) * PieceKg;
                if (kg <= 0f) return;
                __state = new float[] { kg, LumpsFor(kg) };
            }
            catch (Exception e) { Stumble("DoSmeltPrefix", e); }
        }

        /// <summary>Postfiks DoSmelting: przetop naprawde biegl (__runOriginal - prefiks ValyrianBlades / SmeltTab moze go zatrzymac) - kg z otwartej bryly.</summary>
        public static void DoSmeltPostfix(EquipmentElement equipmentElement, float[] __state, bool __runOriginal)
        {
            if (__state == null || !__runOriginal) return;
            string name = null;
            try { name = equipmentElement.Item != null && equipmentElement.Item.Name != null ? equipmentElement.Item.Name.ToString() : null; } catch { }
            Use(__state[0], (int)__state[1], "smelting " + (string.IsNullOrEmpty(name) ? "a weapon" : name));
        }

        // ------------------------------------------------------------ podpowiedz licznika wegla na ekranie kuzni (jak SmithWood.ScreenPostfix)
        internal static string OpenText() { return "Lump burning in the forge: " + Math.Max(0f, OpenKg).ToString("0.#", Inv) + " kg of charcoal left"; }

        public static void ScreenPostfix(TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.CraftingVM __instance)
        {
            try
            {
                if (!On || __instance == null) return;
                var list = __instance.PlayerCurrentMaterials;
                if (list == null) return;
                foreach (var vm in list)
                {
                    if (vm == null || vm.ResourceMaterial != CraftingMaterials.Charcoal) continue;
                    string text = (vm.ResourceName ?? "Charcoal") + "\n" + OpenText() + " (a weapon forged or smelted here burns " + F(PieceKg)
                                  + " kg; a new " + F(LumpKg) + " kg lump is taken from your bags only when the fire runs short)";
                    var cur = vm.ResourceHint != null && vm.ResourceHint.HintText != null ? vm.ResourceHint.HintText.ToString() : null;
                    if (cur == text) return;
                    vm.ResourceHint = new TaleWorlds.Core.ViewModelCollection.Information.HintViewModel(new TaleWorlds.Localization.TextObject("{=!}" + text));
                    ScreenHints++;
                    return;
                }
            }
            catch (Exception e) { Stumble("ScreenPostfix", e); }
        }

        // ------------------------------------------------------------ start sesji
        private static System.Reflection.MethodInfo Declared(object model, string name, Type arg)
        {
            var m = model != null ? AccessTools.Method(model.GetType(), name, new[] { arg }) : null;
            // pulapka 15.10 (SmithWood): metoda z klasy bazowej znaleziona przez typ pochodny - latka wpieta, ale nie wolana; bierzemy metode klasy deklarujacej
            if (m != null && m.DeclaringType != null && m.DeclaringType != m.ReflectedType) m = AccessTools.DeclaredMethod(m.DeclaringType, name, new[] { arg }) ?? m;
            return m;
        }

        private static void Patch(System.Reflection.MethodBase m, HarmonyMethod pre, HarmonyMethod post)
        {
            if (m == null || _patched.Contains(m)) return;
            _h.Patch(m, prefix: pre, postfix: post);
            _patched.Add(m);
        }

        /// <summary>Samosprawdzenie: koszt wegla pierwszej broni z projektem przy zapasie 1 kg (ma byc 0) i 0 kg (ma byc -1); przetop tak samo. Zapas potem jak byl.</summary>
        private static void SelfCheck()
        {
            float keep = OpenKg;
            try
            {
                var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.SmithingModel : null;
                if (model == null) { CheckText = "brak modelu kuzni"; return; }
                ItemObject probe = null;
                foreach (var it in TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                    if (it != null && it.WeaponDesign != null && it.WeaponDesign.UsedPieces != null && RawForge(it.WeaponDesign) == 1 && RawSmelt(it) == 1) { probe = it; break; }
                if (probe == null) { CheckText = "brak broni z projektem do proby"; return; }
                int c = (int)CraftingMaterials.Charcoal;
                OpenKg = 1f;
                var a1 = model.GetSmithingCostsForWeaponDesign(probe.WeaponDesign); var b1 = model.GetSmeltingOutputForItem(probe);
                OpenKg = 0f;
                var a0 = model.GetSmithingCostsForWeaponDesign(probe.WeaponDesign); var b0 = model.GetSmeltingOutputForItem(probe);
                CheckText = probe.StringId + ": wykucie przy zapasie 1 kg " + (a1 != null && a1.Length > c ? a1[c].ToString(Inv) : "?") + ", przy 0 kg " + (a0 != null && a0.Length > c ? a0[c].ToString(Inv) : "?")
                            + "; przetop przy 1 kg " + (b1 != null && b1.Length > c ? b1[c].ToString(Inv) : "?") + ", przy 0 kg " + (b0 != null && b0.Length > c ? b0[c].ToString(Inv) : "?");
            }
            catch (Exception e) { CheckText = "wyjatek " + e.GetType().Name; Stumble("SelfCheck", e); }
            finally { OpenKg = keep; }
        }

        internal static string Hook { get { return _hook; } }

        /// <summary>Start sesji (po SmithWood.SessionStart): latki na metody czynnego modelu (klasy deklarujace), SpendMaterials, DoSmelting i ekran kucia (raz na proces).</summary>
        internal static void SessionStart()
        {
            if (_h == null) _h = new Harmony("com.jeff.armoury.smithcoal");
            DayUses = DayLumps = ScreenHints = DayStumbles = 0; DayKg = 0; _logged = 0;
            var parts = new List<string>();
            try
            {
                var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.SmithingModel : null;
                var mc = Declared(model, "GetSmithingCostsForWeaponDesign", typeof(WeaponDesign));
                Patch(mc, null, new HarmonyMethod(typeof(SmithCoal), nameof(CostPostfix)) { priority = Priority.Last });
                parts.Add(mc != null ? "koszt " + mc.DeclaringType.Name : "koszt BRAK");
                var ms = AccessTools.Method(typeof(CraftingCampaignBehavior), "SpendMaterials", new[] { typeof(WeaponDesign) });
                Patch(ms, new HarmonyMethod(typeof(SmithCoal), nameof(SpendPrefix)), new HarmonyMethod(typeof(SmithCoal), nameof(SpendPostfix)));
                parts.Add(ms != null ? "pobranie SpendMaterials" : "pobranie BRAK");
                var mo = Declared(model, "GetSmeltingOutputForItem", typeof(ItemObject));
                Patch(mo, null, new HarmonyMethod(typeof(SmithCoal), nameof(SmeltPostfix)) { priority = Priority.Last });
                parts.Add(mo != null ? "przetop " + mo.DeclaringType.Name : "przetop BRAK");
                var md = AccessTools.Method(typeof(CraftingCampaignBehavior), "DoSmelting", new[] { typeof(Hero), typeof(EquipmentElement) });
                Patch(md, new HarmonyMethod(typeof(SmithCoal), nameof(DoSmeltPrefix)), new HarmonyMethod(typeof(SmithCoal), nameof(DoSmeltPostfix)));
                parts.Add(md != null ? "pobranie DoSmelting" : "pobranie przetopu BRAK");
                try
                {
                    var mu = AccessTools.DeclaredMethod(typeof(TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.CraftingVM), "UpdateCurrentMaterialsAvailable");
                    Patch(mu, null, new HarmonyMethod(typeof(SmithCoal), nameof(ScreenPostfix)));
                    parts.Add(mu != null ? "ekran kucia wpiete" : "ekran kucia BRAK");
                }
                catch (Exception e2) { parts.Add("ekran kucia wyjatek " + e2.GetType().Name); Stumble("SessionStart(ekran)", e2); }
            }
            catch (Exception e) { parts.Add("wyjatek " + e.GetType().Name); Stumble("SessionStart", e); }
            _hook = string.Join(", ", parts.ToArray());
            SelfCheck();
            Log.Info("Wegiel w kuzni (kg): start sesji | klucz Smith Coal Kg " + (On ? "TAK" : "NIE") + ", " + F(PieceKg) + " kg wegla na bryle gry w koszcie broni i przetopu, bryla "
                     + F(LumpKg) + " kg | zapas otwartej bryly " + F(OpenKg) + " kg | latki: " + _hook + " | samosprawdzenie: " + CheckText + ".");
        }
    }
}
