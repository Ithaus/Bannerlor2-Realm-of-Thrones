using System.Text;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// Okup zamiast szturmu - sciezki gracza (O2 gracz-obronca, O3 gracz-oblegajacy). W O1 puste: gracz-obronca (pan osady albo w osadzie) dostaje regule AI
    /// i wiadomosc (P11 - jak po 24 h bez odpowiedzi), gracz-oblegajacy nie ma opcji okupu.
    /// </summary>
    internal static partial class SiegeRansom
    {
        private static string DecidePlayer(Siege s, Judge j) { return null; }
        private static void StepAsk(Deal d) { }
        private static void BesiegerDone(Deal d, Settlement st, long pool, long[] took, CitySack.Rec pr) { }
        private static bool DayAnyPlayer { get { return false; } }
        private static void DayPartPlayer(StringBuilder sb) { }
        private static void ZeroDayPlayer() { }
        private static void ResetPlayer() { }
        private static void EnsureHooksPlayer() { }
    }
}
