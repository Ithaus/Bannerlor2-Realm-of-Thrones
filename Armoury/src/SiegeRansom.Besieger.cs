using System.Text;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// Okup zamiast szturmu - gracz-oblegajacy (O3). W O2 puste: gracz-oblegajacy nie ma opcji okupu, szturmuje jak w grze.
    /// </summary>
    internal static partial class SiegeRansom
    {
        internal static bool PlayerBesOn { get { return false; } }
        private static bool BesAskPending { get { return false; } }
        private static void BesMenuTick() { }
        private static void BesiegerDone(Deal d, Settlement st, long pool, long[] took, CitySack.Rec pr) { }
        private static bool DayAnyBes { get { return false; } }
        private static void DayPartBes(StringBuilder sb) { }
        private static void ZeroDayBes() { }
        private static void ResetBes() { }
        private static void EnsureHooksBes() { }
    }
}
