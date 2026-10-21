using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>m102-tablica: miejsca na etap E2 (listy gonczne) - przed E2 puste (tablica z zadaniami gry i rozmowa E3).</summary>
    internal static partial class NoticeBoard
    {
        // E2
        private static int AddWatchDialogs(CampaignGameStarter starter) { return 0; }
        private static void WatchConversationEnded() { }
        private static bool WatchOn { get { return false; } }
        private static void ResetWatch() { }
        private static void AddWatchOptions(CampaignGameStarter starter) { }
        private static string WatchKeysText() { return ""; }
        private static bool AnyWatchFree(Settlement st) { return false; }
        private static void WatchInto(Settlement st, List<Entry> list, bool probe) { }
        private static bool WatchValid(Entry e) { return false; }
        private static string WatchCard(Entry e, Settlement st) { return ""; }
        private static ITrackableCampaignObject WatchTarget(Entry e) { return null; }
        private static string BandLabel(MobileParty mp) { return mp != null && mp.Name != null ? mp.Name.ToString() : "the band"; }
        private static void WatchDaily(int day) { }
        private static string WatchProbeText(List<Settlement> towns) { return ""; }
        private static bool WatchAny() { return false; }
        private static void WatchExport(StringBuilder sb) { }
        private static void WatchImport(string part) { }
    }
}
