using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// MNIEJSZE SAKWY AI (Jeff 28.08: "tyle ile do napraw, zadnych setek").
    /// Banner Kings kaze kazdej partii AI trzymac 10 DNI zapasu kazdego dobra
    /// per zolnierz - setki partii x 10 dni = wykupione targi (stad "nigdzie
    /// nie ma alkoholu ani skory"). Dwa ciecia popytu U ZRODLA:
    ///  - PostInitialize: AI trzyma BkSupplyDaysCap dni (domyslnie JEDEN) -
    ///    kupuja na biezaco, jak braknie, jada do miasta;
    ///  - czapka na kazdy Calculate*Need: zapas zadnego dobra nie przekroczy
    ///    BkSupplyMaxPieces sztuk, chocby armia miala 300 ludzi.
    /// Gracz kupuje recznie - jego nie ruszamy. Dosypki podazy (browar,
    /// garbarnia) COFNIETE na zadanie Jeffa 28.08 - po scieciu popytu
    /// vanillowa produkcja ma wystarczyc.
    /// </summary>
    internal static class BkSupplyTemper
    {
        private static int _tempered;

        public static void PostInitPostfix(object __instance)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || c.BkSupplyDaysCap <= 0) return;
                var tr = HarmonyLib.Traverse.Create(__instance);
                bool auto = false;
                try { auto = tr.Property("AutoBuying").GetValue<bool>(); } catch { }
                if (!auto) return;   // gracz zaopatruje sie sam
                int days = tr.Property("DaysOfProvision").GetValue<int>();
                int capNow = WinterBite.SupplyDaysCapNow();
                if (days <= capNow) return;
                tr.Property("DaysOfProvision").SetValue(capNow);
                if (++_tempered == 1)
                    Log.Info("BkSupplyTemper: zapasy AI sciete do " + capNow + " dni (BK chcial " + days + "; jesienia cap rosnie).");
            }
            catch { }
        }

        /// <summary>
        /// SUFIT NA SZTUKI (Jeff 28.08: "NIE na glowe - po co im SETKI tego?
        /// tyle, ile potrzebuja do napraw"). BK liczy potrzeby per zolnierz
        /// (300 ludzi = 300 stawek dziennie) - czapka na WYNIK kazdego modelu
        /// potrzeb: dzienna potrzeba partii AI nie przekroczy
        /// BkSupplyMaxPieces / BkSupplyDaysCap, wiec CALY zapas nigdy nie
        /// przekroczy BkSupplyMaxPieces sztuk danego dobra. Gracz nietkniety.
        /// </summary>
        public static void NeedCapPostfix(object __0, ref TaleWorlds.CampaignSystem.ExplainedNumber __result)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || c.BkSupplyMaxPieces <= 0) return;
                bool auto = false;
                try { auto = HarmonyLib.Traverse.Create(__0).Property("AutoBuying").GetValue<bool>(); } catch { }
                if (!auto) return;   // gracz kupuje recznie - bez czapki
                float perDay = (float)c.BkSupplyMaxPieces / Math.Max(1, c.BkSupplyDaysCap > 0 ? c.BkSupplyDaysCap : 4);
                __result.LimitMax(perDay);
            }
            catch { }
        }

        // ------------------------------------------------------------ 150: tekstylia zaopatrzenia BK = 0 (jedna regula odziezy wojska)
        // BK PartySupplies (dekompilacja BannerKings.dll): potrzeba tekstyliow (kategorie welna, plotno, len) rosnie co dobe o wynik
        // BKPartyNeedsModel.CalculateClothNeed (0.01 sztuki na zolnierza x PartySuppliesFactor), zapisana w ClothNeed (w zapisie gry,
        // do 0.3 x ludzi); BuyItems kupuje ja w osadzie z kiesy lorda (zloto w nicosc - osada nic nie dostaje), ConsumeItems zjada
        // z jukow partii, takze gracza. Przy odziezy wojska (ArmyClothing) wynik = 0 dla KAZDEJ partii, a zapisana potrzeba (stary
        // zapis) zerowana w tej samej chwili - inaczej BK kupilby i zjadl ja jeszcze raz obok naszej reguly.
        internal static bool ClothHooked, ClothResetReady;
        private static System.Reflection.MethodInfo _clothGet, _clothSet;

        public static void ClothZeroPostfix(object __0, ref TaleWorlds.CampaignSystem.ExplainedNumber __result)
        {
            try
            {
                if (!ArmyClothing.On) return;
                __result.LimitMin(0f);
                __result.LimitMax(0f);
                if (__0 == null || _clothGet == null || _clothSet == null) return;
                float v = (float)_clothGet.Invoke(__0, null);
                if (v == 0f) return;
                _clothSet.Invoke(__0, new object[] { 0f });
                ArmyClothing.BkClothZeroed++;
            }
            catch (Exception e) { ArmyClothing.Stumble("BkSupplyTemper.ClothZeroPostfix", e); }
        }

        // ------------------------------------------------------------ 172: strzaly zaopatrzenia BK = 0 dla partii AI (jedna droga amunicji)
        // BK PartySupplies: ArrowsNeed rosnie co dobe o BKPartyNeedsModel.CalculateArrowsNeed (0.003 x PartySuppliesFactor snopa na lucznika),
        // BuyItems kupuje kategorie "arrows" z polki osady za zloto lorda (zloto w nicosc), ConsumeItems(ArrowsNeed x 2) niszczy z jukow.
        // Amunicje partii AI liczy Armoury: zakupy AiGear wedlug wzorcow, zuzycie w bitwie i odzysk. Przy czynnych strzelarzach (TownFletchers)
        // i zakupach AI wynik = 0 i zapisana potrzeba zerowana (jak tekstylia 150); partia gracza - bez zmian (BK jak dotad).
        internal static bool ArrowsHooked, ArrowsResetReady;
        private static System.Reflection.MethodInfo _arrowsGet, _arrowsSet, _partyGet;

        public static void ArrowsZeroPostfix(object __0, ref TaleWorlds.CampaignSystem.ExplainedNumber __result)
        {
            try
            {
                if (__0 == null || !TownFletchers.BkArrowsClosed) return;
                var party = _partyGet != null ? _partyGet.Invoke(__0, null) as TaleWorlds.CampaignSystem.Party.MobileParty : null;
                if (party == null || party == TaleWorlds.CampaignSystem.Party.MobileParty.MainParty) return;
                __result.LimitMin(0f);
                __result.LimitMax(0f);
                bool reset = false;
                if (ArrowsResetReady)
                {
                    float v = (float)_arrowsGet.Invoke(__0, null);
                    if (v != 0f) { _arrowsSet.Invoke(__0, new object[] { 0f }); reset = true; }
                }
                TownFletchers.NoteBk(reset);
            }
            catch { }
        }

        internal static void ApplyAll(HarmonyLib.Harmony h)
        {
            try
            {
                var t = QuartermasterLaw.FindType("BannerKings.Behaviours.PartyNeeds.PartySupplies");
                var m = t != null ? HarmonyLib.AccessTools.Method(t, "PostInitialize") : null;
                if (m == null) { Log.Info("BkSupplyTemper: BK PartySupplies nieobecne."); return; }
                h.Patch(m, postfix: new HarmonyLib.HarmonyMethod(typeof(BkSupplyTemper), "PostInitPostfix"));

                // czapka na kazdy model potrzeb BK (Calculate*Need)
                int capped = 0;
                var tModel = QuartermasterLaw.FindType("BannerKings.Models.BKModels.BKPartyNeedsModel");
                if (tModel != null)
                {
                    var capPost = new HarmonyLib.HarmonyMethod(typeof(BkSupplyTemper), "NeedCapPostfix");
                    foreach (var mm in tModel.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
                    {
                        if (!mm.Name.StartsWith("Calculate") || !mm.Name.EndsWith("Need")) continue;
                        if (mm.ReturnType != typeof(TaleWorlds.CampaignSystem.ExplainedNumber)) continue;
                        try { h.Patch(mm, postfix: capPost); capped++; } catch { }
                    }
                    // 150: tekstylia = 0 przy odziezy wojska - po czapce (Priority.Last), takze dla partii gracza
                    try
                    {
                        var cloth = HarmonyLib.AccessTools.Method(tModel, "CalculateClothNeed");
                        if (cloth != null && cloth.ReturnType == typeof(TaleWorlds.CampaignSystem.ExplainedNumber))
                        {
                            h.Patch(cloth, postfix: new HarmonyLib.HarmonyMethod(typeof(BkSupplyTemper), "ClothZeroPostfix") { priority = HarmonyLib.Priority.Last });
                            ClothHooked = true;
                        }
                        _clothGet = HarmonyLib.AccessTools.PropertyGetter(t, "ClothNeed");
                        _clothSet = HarmonyLib.AccessTools.PropertySetter(t, "ClothNeed");
                        ClothResetReady = _clothGet != null && _clothSet != null && _clothGet.ReturnType == typeof(float);
                        if (!ClothResetReady) { _clothGet = null; _clothSet = null; }
                    }
                    catch (Exception e) { Log.Error("BkSupplyTemper.ApplyAll(CalculateClothNeed)", e); }
                    // 172: strzaly = 0 dla partii AI przy czynnych strzelarzach - po czapce (Priority.Last)
                    try
                    {
                        var arrows = HarmonyLib.AccessTools.Method(tModel, "CalculateArrowsNeed");
                        _partyGet = HarmonyLib.AccessTools.PropertyGetter(t, "Party");
                        if (arrows != null && arrows.ReturnType == typeof(TaleWorlds.CampaignSystem.ExplainedNumber) && _partyGet != null)
                        {
                            h.Patch(arrows, postfix: new HarmonyLib.HarmonyMethod(typeof(BkSupplyTemper), "ArrowsZeroPostfix") { priority = HarmonyLib.Priority.Last });
                            ArrowsHooked = true;
                        }
                        _arrowsGet = HarmonyLib.AccessTools.PropertyGetter(t, "ArrowsNeed");
                        _arrowsSet = HarmonyLib.AccessTools.PropertySetter(t, "ArrowsNeed");
                        ArrowsResetReady = _arrowsGet != null && _arrowsSet != null && _arrowsGet.ReturnType == typeof(float);
                    }
                    catch (Exception e) { Log.Error("BkSupplyTemper.ApplyAll(CalculateArrowsNeed)", e); }
                }
                Log.Info("BkSupplyTemper: tekstylia zaopatrzenia BK (CalculateClothNeed) = 0 przy odziezy wojska (150, MCM Army Clothing Enabled) - "
                         + (ClothHooked ? "wpiete" : "BRAK latki (BK kupi tekstylia jak dotad)") + ", zerowanie zapisanej potrzeby ClothNeed " + (ClothResetReady ? "wpiete" : "BRAK") + ".");
                Log.Info("BkSupplyTemper: strzaly zaopatrzenia BK (CalculateArrowsNeed) = 0 dla partii AI przy czynnych strzelarzach (172) - "
                         + (ArrowsHooked ? "wpiete" : "BRAK latki (BK kupi i zuzyje strzaly jak dotad)") + ", zerowanie zapisanej potrzeby ArrowsNeed " + (ArrowsResetReady ? "wpiete" : "BRAK") + ".");
                Log.Info("BkSupplyTemper: sakwy AI ograniczone (dni=" + (Settings.Current != null ? Settings.Current.BkSupplyDaysCap : 4)
                         + ", sufit sztuk=" + (Settings.Current != null ? Settings.Current.BkSupplyMaxPieces : 15)
                         + ", czapka w " + capped + " modelach potrzeb).");
            }
            catch (Exception e) { Log.Error("BkSupplyTemper.ApplyAll", e); }
        }
    }
}

