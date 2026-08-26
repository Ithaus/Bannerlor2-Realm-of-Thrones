using System;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CrashScribe
{
    /// <summary>Gdzie bylismy, kiedy sie posypalo. Bez tego raport jest polowa raportu.</summary>
    internal static class GameState
    {
        internal static string Describe()
        {
            var sb = new StringBuilder();
            try
            {
                var c = Campaign.Current;
                if (c == null) return "  (outside a campaign - main menu or loading)";

                try { sb.AppendLine("  Date           : " + CampaignTime.Now); } catch { }

                var h = Hero.MainHero;
                if (h != null)
                {
                    try { sb.AppendLine("  Hero           : " + h.Name + "  (prisoner: " + h.IsPrisoner + ")"); } catch { }
                    try { sb.AppendLine("  Gold           : " + h.Gold); } catch { }
                }

                try
                {
                    var s = Settlement.CurrentSettlement;
                    sb.AppendLine("  Settlement     : " + (s != null ? s.Name + " [" + s.StringId + "]" : "(in the field)"));
                    if (s != null && s.Town != null)
                    {
                        sb.AppendLine("  Parties in it  : " + s.Parties.Count + ", heroes without party: " + s.HeroesWithoutParty.Count);
                        try
                        {
                            var g = c.TournamentManager != null ? c.TournamentManager.GetTournamentGame(s.Town) : null;
                            sb.AppendLine("  Tournament     : " + (g != null ? g.GetType().Name + " (prize: " +
                                (g.Prize != null ? g.Prize.Name.ToString() : "-") + ")" : "(none)"));
                        }
                        catch { }
                    }
                }
                catch { }

                try
                {
                    var mp = MobileParty.MainParty;
                    if (mp != null)
                    {
                        sb.AppendLine("  Party          : " + mp.MemberRoster.TotalManCount + " men, " +
                                       mp.PrisonRoster.TotalManCount + " prisoners");
                        sb.AppendLine("  Position       : " + mp.GetPosition2D);
                        sb.AppendLine("  Army           : " + (mp.Army != null ? mp.Army.Name.ToString() : "(none)"));
                        sb.AppendLine("  Battle         : " + (mp.MapEvent != null ? mp.MapEvent.EventType.ToString() : "(none)"));
                    }
                }
                catch { }

                try
                {
                    var menu = c.CurrentMenuContext;
                    sb.AppendLine("  Menu           : " + (menu != null && menu.GameMenu != null ? menu.GameMenu.StringId : "(none)"));
                }
                catch { }

                try { sb.AppendLine("  Tournament mdl : " + (c.Models != null && c.Models.TournamentModel != null ? c.Models.TournamentModel.GetType().FullName : "?")); } catch { }
            }
            catch (Exception e) { sb.AppendLine("  (could not describe game state: " + e.Message + ")"); }
            return sb.ToString().TrimEnd();
        }
    }
}
