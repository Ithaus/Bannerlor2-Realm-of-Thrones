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
                }
                Log.Info("BkSupplyTemper: sakwy AI ograniczone (dni=" + (Settings.Current != null ? Settings.Current.BkSupplyDaysCap : 4)
                         + ", sufit sztuk=" + (Settings.Current != null ? Settings.Current.BkSupplyMaxPieces : 15)
                         + ", czapka w " + capped + " modelach potrzeb).");
            }
            catch (Exception e) { Log.Error("BkSupplyTemper.ApplyAll", e); }
        }
    }
}

