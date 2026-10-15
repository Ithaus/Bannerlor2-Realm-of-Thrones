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
    /// DREWNO W KUZNI W KILOGRAMACH (Jeff 15.10: "drewno ma wartosc 100 kg, a do mlota trzeba 3 drewna - 300 kg"; wybral poprawke samej kuzni).
    /// Sztuka drewna (hardwood) to u nas ladunek 100 kg (HistoricalPrices: handel, budowy, dymarki zostaja w ladunkach). Kuznia brala ladunki:
    /// bron z ekranu kuzni gry - SmithingModel.GetSmithingCostsForWeaponDesign (MaterialsUsed czesci: trzonek 1-3 "drewna"), nasze przepisy
    /// (Recipes: luki, kusze, tarcze, strzaly - min. 1 ladunek na sztuke).
    /// Teraz: kowal gracza ma otwarty ladunek - zapas w kg (w zapisie, arm_smith_wood_kg). Potrzeba sztuki w kg: bron gry = sztuki drewna
    /// czesci x SmithWoodKgPerPiece (1 kg), nasze przepisy = waga drewna przedmiotu. Kuznia pobiera nowy ladunek (sztuke hardwood) dopiero,
    /// gdy zapas w kg nie starcza; ekran pokazuje wtedy 1 drewno, inaczej 0. Po kuciu linia gracza: ile kg poszlo, ile zostalo.
    /// Klucz SmithWoodKg (false = jak dotad, sztuki = ladunki).
    /// </summary>
    internal static class SmithWood
    {
        internal const float LoadKg = 100f;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal static float OpenKg;                     // zapas otwartego ladunku (kg) - w zapisie
        private static readonly HashSet<System.Reflection.MethodBase> _patched = new HashSet<System.Reflection.MethodBase>();
        private static Harmony _h;
        private static string _hook = "nie wpiete";
        private static int _errors;
        private static int _sUses, _sLoads; private static double _sKg;

        internal static bool On { get { var s = Settings.Current; return s != null && s.SmithWoodKg; } }
        private static float PerPiece { get { var s = Settings.Current; return s == null || float.IsNaN(s.SmithWoodKgPerPiece) ? 1f : Math.Max(0.1f, Math.Min(20f, s.SmithWoodKgPerPiece)); } }

        private static void Stumble(string where, Exception e)
        {
            _errors++;
            if (_errors <= 5 || _errors % 100 == 0) Log.Error("SmithWood." + where + " (potkniecie " + _errors + ")", e);
        }

        /// <summary>Ile ladunkow trzeba otworzyc na kg drewna (0, gdy zapas otwartego ladunku starcza).</summary>
        internal static int LoadsFor(float kg)
        {
            if (kg <= 0f) return 0;
            float lack = kg - Math.Max(0f, OpenKg);
            return lack <= 0.0001f ? 0 : (int)Math.Ceiling(lack / LoadKg - 1e-6);
        }

        /// <summary>Kucie zuzylo kg drewna, a kuznia zdjela loads ladunkow z sakw.</summary>
        internal static void Use(float kg, int loads, string what)
        {
            try
            {
                if (kg <= 0f && loads <= 0) return;
                OpenKg = Math.Max(0f, OpenKg + loads * LoadKg - Math.Max(0f, kg));
                _sUses++; _sLoads += Math.Max(0, loads); _sKg += Math.Max(0f, kg);
                Log.Player("Wood at the anvil: " + kg.ToString("0.#", Inv) + " kg" + (string.IsNullOrEmpty(what) ? "" : " for " + what)
                           + (loads > 0 ? " - a new load of hardwood opened" : "") + "; " + OpenKg.ToString("0.#", Inv) + " kg left of the open load.");
                Log.Info("Drewno w kuzni (kg): " + (what ?? "-") + " " + kg.ToString("0.##", Inv) + " kg, otwartych ladunkow " + loads + ", zapas " + OpenKg.ToString("0.##", Inv)
                         + " kg | od startu sesji: kuc " + _sUses + ", kg " + _sKg.ToString("0.#", Inv) + ", ladunkow " + _sLoads + ".");
            }
            catch (Exception e) { Stumble("Use", e); }
        }

        // ------------------------------------------------------------ bron z ekranu kuzni gry
        private static int RawWood(WeaponDesign d)
        {
            int raw = 0;
            if (d == null || d.UsedPieces == null) return 0;
            foreach (var el in d.UsedPieces)
            {
                if (el == null || !el.IsValid || el.CraftingPiece == null || el.CraftingPiece.MaterialsUsed == null) continue;
                foreach (var m in el.CraftingPiece.MaterialsUsed) if (m.Item1 == CraftingMaterials.Wood) raw += m.Item2;
            }
            return raw;
        }

        /// <summary>Postfiks GetSmithingCostsForWeaponDesign czynnego modelu: drewno = ladunki do otwarcia (0 albo wiecej), nie sztuki czesci.</summary>
        public static void CostPostfix(WeaponDesign weaponDesign, ref int[] __result)
        {
            try
            {
                if (!On || __result == null || __result.Length <= (int)CraftingMaterials.Wood) return;
                int raw = RawWood(weaponDesign);
                if (raw <= 0) return;
                __result[(int)CraftingMaterials.Wood] = -LoadsFor(raw * PerPiece);
            }
            catch (Exception e) { Stumble("CostPostfix", e); }
        }

        public static void SpendPrefix(WeaponDesign weaponDesign, out float[] __state)
        {
            __state = null;
            try
            {
                if (!On) return;
                float kg = RawWood(weaponDesign) * PerPiece;
                if (kg <= 0f) return;
                __state = new float[] { kg, LoadsFor(kg) };
            }
            catch (Exception e) { Stumble("SpendPrefix", e); }
        }

        public static void SpendPostfix(WeaponDesign weaponDesign, float[] __state)
        {
            if (__state == null) return;
            string name = null;
            try { name = weaponDesign != null && weaponDesign.WeaponName != null ? weaponDesign.WeaponName.ToString() : null; } catch { }
            Use(__state[0], (int)__state[1], string.IsNullOrEmpty(name) ? "the handle" : name);
        }

        /// <summary>Start sesji (po McmSettings.Apply): wpiecie na metode czynnego modelu kosztow i SpendMaterials (raz na proces).</summary>
        internal static void SessionStart()
        {
            if (_h == null) _h = new Harmony("com.jeff.armoury.smithwood");
            _sUses = _sLoads = 0; _sKg = 0;
            try
            {
                var parts = new List<string>();
                var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.SmithingModel : null;
                var mc = model != null ? AccessTools.Method(model.GetType(), "GetSmithingCostsForWeaponDesign", new[] { typeof(WeaponDesign) }) : null;
                if (mc != null && !_patched.Contains(mc)) { _h.Patch(mc, postfix: new HarmonyMethod(typeof(SmithWood), nameof(CostPostfix)) { priority = Priority.Last }); _patched.Add(mc); }
                parts.Add(mc != null ? "koszt " + mc.DeclaringType.Name : "koszt BRAK");
                var ms = AccessTools.Method(typeof(CraftingCampaignBehavior), "SpendMaterials", new[] { typeof(WeaponDesign) });
                if (ms != null && !_patched.Contains(ms))
                {
                    _h.Patch(ms, prefix: new HarmonyMethod(typeof(SmithWood), nameof(SpendPrefix)), postfix: new HarmonyMethod(typeof(SmithWood), nameof(SpendPostfix)));
                    _patched.Add(ms);
                }
                parts.Add(ms != null ? "pobranie SpendMaterials" : "pobranie BRAK");
                _hook = string.Join(", ", parts.ToArray());
            }
            catch (Exception e) { _hook = "wyjatek " + e.GetType().Name; Stumble("SessionStart", e); }
            Log.Info("Drewno w kuzni (kg): start sesji | klucz SmithWoodKg " + (On ? "TAK" : "NIE") + ", kg na sztuke drewna czesci " + PerPiece.ToString("0.##", Inv)
                     + ", ladunek " + LoadKg.ToString("0", Inv) + " kg | zapas otwartego ladunku " + OpenKg.ToString("0.##", Inv) + " kg | latki: " + _hook + ".");
        }
    }

    /// <summary>Zapas otwartego ladunku drewna kowala gracza w zapisie.</summary>
    internal sealed class SmithWoodBehavior : CampaignBehaviorBase
    {
        public SmithWoodBehavior() { SmithWood.OpenKg = 0f; }

        public override void RegisterEvents() { }

        public override void SyncData(IDataStore dataStore)
        {
            float kg = SmithWood.OpenKg;
            dataStore.SyncData("arm_smith_wood_kg", ref kg);
            SmithWood.OpenKg = float.IsNaN(kg) || kg < 0f ? 0f : kg;
        }
    }
}
