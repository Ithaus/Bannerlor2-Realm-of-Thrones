using System.Text;
using TaleWorlds.CampaignSystem.Siege;

namespace Armoury
{
    /// <summary>
    /// Okup zamiast szturmu - miejsca dzialania (O0: sam pomiar, nic nie dziala). O1 (okup AI z AI), O2 (gracz-obronca) i O3 (gracz-oblegajacy) zastepuja
    /// te metody wlasnymi; w O0 gra szturmuje zawsze jak dotad.
    /// </summary>
    internal static partial class SiegeRansom
    {
        internal static bool AiOn { get { return false; } }
        internal static bool PlayerDefOn { get { return false; } }
        internal static bool PlayerBesOn { get { return false; } }
        internal static int TruceDays { get { return 0; } }

        private static bool Hold(Siege s) { return false; }
        private static string Decide(Siege s, Judge j) { return "szturm (gra)"; }
        private static void DropDeals(SiegeEvent ev, string why) { }
        private static void RestoreFailed(Siege s) { }
        private static void DailyMore() { }
        private static bool DayAny { get { return false; } }
        private static void DayPart(StringBuilder sb) { }
        private static void ZeroDayMore() { }
        private static void ResetMore() { }
        private static void EnsureHooksMore() { }
    }
}
