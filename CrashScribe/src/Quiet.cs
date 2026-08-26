using System;
using HarmonyLib;

namespace CrashScribe
{
    /// <summary>
    /// Wyciszenie falszywego alarmu ROT-a. ROT sprawdza, czy jego modele sa
    /// "na wierzchu" i krzyczy na czerwono "move ROT below BKROTPatch", gdy
    /// widzi nad soba modele BKROTPatch. Tyle ze BKROTPatch to dedykowany
    /// lacznik BK+ROT - CELOWO nadpisuje modele ROT-a i sam odtwarza jego
    /// logike (bonusy bogow w morale itd.). Przestawienie kolejnosci wedle
    /// komunikatu wylaczyloby lacznik. Alarm dotyczacy BKROTPatch pomijamy;
    /// gdyby modele przykryl KTOKOLWIEK INNY, ostrzezenie ROT-a przejdzie.
    /// </summary>
    internal static class Quiet
    {
        internal static void Install(Harmony harmony)
        {
            try
            {
                var rotSub = Type.GetType("ROT.SubModule, ROT");
                if (rotSub == null) return;
                var validate = AccessTools.Method(rotSub, "ValidateGameModel");
                if (validate == null) { Scribe.Line("Quiet: ROT.ValidateGameModel not found."); return; }
                harmony.Patch(validate, prefix: new HarmonyMethod(typeof(Quiet), "SkipKnownFalseAlarm"));
                Scribe.Line("Quiet: ROT load-order nag filtered for BKROTPatch.");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Quiet.Install", null); } catch { } }

            // BKROTPatch dopisuje linie do logu przy KAZDYM odczycie populacji osady
            // (setki tysiecy linii na godzine, 30 MB logu w 40 minut, ciagle pisanie
            // na dysk z glownego watku). Wyciszamy jego diagnostyke, ale zachowujemy
            // to, po co powstala: naprawe zepsutej wartosci (ujemna -> 200000).
            try
            {
                var diag = Type.GetType("BKROTPatch.Patches.PopulationData_TotalPop_Diagnostic_Patch, BKROTPatch");
                if (diag != null)
                {
                    var post = AccessTools.Method(diag, "Postfix");
                    if (post != null)
                    {
                        harmony.Patch(post, prefix: new HarmonyMethod(typeof(Quiet), "HealQuietly"));
                        Scribe.Line("Quiet: BKROT TotalPop log-spam silenced (healing kept).");
                    }
                }
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Quiet.Install(BKROT)", null); } catch { } }

            // RealisticBannerlord "Advanced Supplies" - Jeff wylacza to recznie
            // przy KAZDYM uruchomieniu, bo przelacznik nie trzyma. Wycinamy na stale:
            // dzienny tick nigdy nie startuje, a "braku zaopatrzenia" nie ma nigdy.
            try
            {
                var sup = Type.GetType("RealisticBannerlord.Systems.Supplies.AdvancedSuppliesBehavior, RealisticBannerlord");
                if (sup != null)
                {
                    var tick = AccessTools.Method(sup, "OnDailyTickParty");
                    if (tick != null) harmony.Patch(tick, prefix: new HarmonyMethod(typeof(Quiet), "SkipSupplies"));
                    var outOf = AccessTools.Method(sup, "IsPartyOutOfSupplies");
                    if (outOf != null) harmony.Patch(outOf, prefix: new HarmonyMethod(typeof(Quiet), "NeverOut"));
                    Scribe.Line("Quiet: RB Advanced Supplies wyciete na stale (tick=" + (tick != null) + ", outOf=" + (outOf != null) + ").");
                }
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "Quiet.Install(RBSupplies)", null); } catch { } }
        }

        /// <summary>
        /// Zastepuje diagnostyczny postfix BKROTPatch: zadnego Debug.Print,
        /// ale uszkodzona populacja (ujemna) dalej jest naprawiana jak w oryginale.
        /// __1 = drugi parametr oryginalu (ref int __result z PopulationData.TotalPop).
        /// </summary>
        public static bool HealQuietly(ref int __1)
        {
            if (__1 == int.MinValue || __1 < 0) __1 = 200000;
            return false;
        }

        /// <summary>System zaopatrzenia RB wyciety na stale - tick nigdy nie rusza.</summary>
        public static bool SkipSupplies() { return false; }

        /// <summary>Nikt nigdy nie jest "bez zaopatrzenia" - zadnych kar morale z tego systemu.</summary>
        public static bool NeverOut(ref bool __result) { __result = false; return false; }

        public static bool SkipKnownFalseAlarm(object model)
        {
            try
            {
                var gm = model as TaleWorlds.Core.GameModel;
                if (gm == null) return true;
                var asm = gm.GetType().Assembly.GetName().Name;
                if (asm == "BKROTPatch") return false;   // znany, zamierzony uklad - bez krzyku
                return true;
            }
            catch { return true; }
        }
    }
}
