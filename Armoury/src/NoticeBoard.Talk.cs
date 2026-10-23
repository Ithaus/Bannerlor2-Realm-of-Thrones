using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// m102-tablica E3 (projekt pkt 6.2) - ROZMOWA Z TABLICY I "YOUR TASKS".
    ///  (1) Linia otwierajaca: gracz przyszedl z tablicy ("Go and see" / "Talk now" zapamietuja osobe) - w glownych opcjach rozmowy "I read your notice on the board in X."
    ///      (hero_main_options -> issue_offer, priorytet 111 - ta sama para stanow co linia gry "hero_give_issue" 110, bez skutku; dalej kwestie zadania z gry). Warunki jak
    ///      w grze (LordConversationsCampaignBehavior: nie jeniec, gracz i rozmowca nie na tratwie, sprawa wolna). Klucz NoticeBoardOpening. Osoba zapomniana przy koncu
    ///      rozmowy z nia (ConversationEnded) i przy wyjezdzie z miasta.
    ///  (2) Slowa zleceniodawcy na karcie: IssueBriefByIssueGiver (oczyszczone jak opis; z tokenami - opis gry).
    ///  (3) "Your tasks" na gorze tablicy: zadania gracza w toku (QuestManager.Quests, IsOngoing, najwyzej 8) - co, dla kogo, kwota obiecana (QuestPay8.PromisedReward),
    ///      ile dni (QuestDueTime), gdzie zleceniodawca; karta z flaga na zleceniodawcy.
    /// Licznik "z tablicy do zadania": rozmowa z osoba z tablicy skonczona, a jej sprawa juz nie wolna (przyjeta albo wyslani ludzie).
    /// </summary>
    internal static partial class NoticeBoard
    {
        private const int MaxTasks = 8;
        private static Hero _fromBoard;
        private static Settlement _fromBoardTown;
        private static IssueBase _fromBoardIssue;

        private static bool OpeningOn { get { var s = Settings.Current; return s != null && s.NoticeBoardOpening; } }

        private static void ResetTalk() { _fromBoard = null; _fromBoardTown = null; _fromBoardIssue = null; }

        private static string TalkKeysText() { var s = Settings.Current; return ", Opening " + YN(s != null && s.NoticeBoardOpening); }

        private static void MarkFromBoard(Hero h, Settlement st)
        {
            _fromBoard = h; _fromBoardTown = st;
            _fromBoardIssue = h != null ? h.Issue : null;
        }

        /// <summary>Linie rozmowy (OnAfterSessionLaunched - przed pierwsza rozmowa). Zwraca liczbe wpietych linii (do linii "wpieta").</summary>
        private static int AddDialogs(CampaignGameStarter starter)
        {
            int n = 0;
            try
            {
                starter.AddPlayerLine("arm_board_issue", "hero_main_options", "issue_offer", "{=!}I read your notice on the board in {ARM_BOARD_TOWN}.", BoardIssueCond, null, 111);
                n++;
            }
            catch (Exception e) { Stumble("AddDialogs(linia tablicy)", e); }
            n += AddWatchDialogs(starter);
            n += AddHeadmanDialogs(starter);
            return n;
        }

        private static bool BoardIssueCond()
        {
            try
            {
                if (!OpeningOn || _fromBoard == null) return false;
                var h = Hero.OneToOneConversationHero;
                if (h == null || h != _fromBoard || h.IsPrisoner) return false;
                var mp = MobileParty.MainParty;
                if (mp != null && mp.IsInRaftState) return false;
                var cp = MobileParty.ConversationParty;
                if (cp != null && cp.IsInRaftState) return false;
                var issue = h.Issue;
                if (issue == null || !issue.IsOngoingWithoutQuest) return false;
                MBTextManager.SetTextVariable("ARM_BOARD_TOWN", _fromBoardTown != null ? _fromBoardTown.Name.ToString() : "town");
                return true;
            }
            catch (Exception e) { Stumble("BoardIssueCond", e); return false; }
        }

        /// <summary>CampaignEvents.ConversationEnded: osoba z tablicy - licznik "z tablicy do zadania" i zapomnienie; kapitan strazy (E2) - flaga rozmowy zdjeta.</summary>
        internal static void OnConversationEnded(IEnumerable<CharacterObject> chars)
        {
            try
            {
                WatchConversationEnded();
                if (_fromBoard == null || chars == null) return;
                bool with = false;
                foreach (var c in chars) if (c != null && c.IsHero && c.HeroObject == _fromBoard) { with = true; break; }
                if (!with) return;
                var issue = _fromBoardIssue;
                if (issue != null && !issue.IsOngoingWithoutQuest && (issue.IsSolvingWithQuest || issue.IsSolvingWithAlternative || issue.IssueQuest != null))
                {
                    _dToQuest++;
                    Log.Info(P + "z tablicy do zadania - " + _fromBoard.StringId + " (" + (_fromBoardTown != null ? _fromBoardTown.StringId : "?") + ")");
                }
                ResetTalk();
            }
            catch (Exception e) { Stumble("OnConversationEnded", e); ResetTalk(); }
        }

        /// <summary>CampaignEvents.OnSettlementLeftEvent: gracz wyjechal z miasta tablicy - osoba z tablicy zapomniana.</summary>
        internal static void OnSettlementLeft(MobileParty mp, Settlement st)
        {
            try { if (mp != null && mp == MobileParty.MainParty && _fromBoard != null) ResetTalk(); } catch { }
        }

        private static string BriefOf(IssueBase issue)
        {
            if (issue == null) return null;
            return Clean(SafeText(() => issue.IssueBriefByIssueGiver), DescMax);
        }

        // ------------------------------------------------------------ "Your tasks"

        private static void TasksInto(Settlement st, List<Entry> list)
        {
            try
            {
                var qm = Campaign.Current != null ? Campaign.Current.QuestManager : null;
                if (qm == null || qm.Quests == null) return;
                int n = 0;
                foreach (var q in qm.Quests)
                {
                    if (n >= MaxTasks) break;
                    try
                    {
                        if (q == null || !q.IsOngoing) continue;
                        if (IsOurQuest(q)) continue;   // m109-list: list gonczy z zadaniem stoi na tablicy jako list ("you took it up") - bez drugiego wpisu
                        var e = new Entry { K = Kind.Task, Quest = q, Hero = q.QuestGiver, Group = 0 };
                        e.Reward = QuestPay8.PromisedReward(q);
                        string title = Clean(SafeText(() => q.Title), 100) ?? "A task";
                        string due = TaskDue(q, true);
                        string giver = q.QuestGiver != null ? NameOf(q.QuestGiver) : null;
                        e.Where = q.QuestGiver != null ? WhereOf(q.QuestGiver, st, null, false) : "";
                        e.Line = "Your task: " + title + (giver != null ? " - for " + giver : "") + (e.Reward > 0 ? " - " + e.Reward.ToString("N0", Inv) : "") + (due != null ? " - " + due : "")
                                 + (e.Where.Length > 0 ? " - " + e.Where : "");
                        list.Add(e);
                        n++;
                    }
                    catch (Exception ex) { Stumble("TasksInto(zadanie)", ex); }
                }
            }
            catch (Exception e) { Stumble("TasksInto", e); }
        }

        private static string TaskDue(QuestBase q, bool shortForm)
        {
            try
            {
                if (q.IsRemainingTimeHidden || q.QuestDueTime == CampaignTime.Never) return shortForm ? null : "There is no set time.";
                double d = (q.QuestDueTime - CampaignTime.Now).ToDays;
                int n = Math.Max(0, (int)Math.Round(d));
                if (shortForm) return n <= 0 ? "due now" : n + (n == 1 ? " day left" : " days left");
                return n <= 0 ? "Time: it is due now." : "Time: " + n + (n == 1 ? " day" : " days") + " left.";
            }
            catch { return null; }
        }

        private static void TasksText(StringBuilder sb, List<Entry> list)
        {
            int t = 0;
            foreach (var e in list) if (e.K == Kind.Task) t++;
            if (t == 0) return;
            sb.Append("\n\nYour tasks in hand (").Append(t).Append(") are pinned at the top - pick one to see who gave it and to mark them on the map.");
        }

        private static string TaskCard(Entry e, Settlement st)
        {
            var q = e.Quest;
            var g = q.QuestGiver;
            var sb = new StringBuilder();
            string title = Clean(SafeText(() => q.Title), 120) ?? "A task";
            sb.Append("YOUR TASK: ").Append(title.ToUpperInvariant());
            if (g != null)
            {
                e.Where = WhereOf(g, st, null, false);
                sb.Append("\nGiven by ").Append(NameOf(g)).Append(", ").Append(RoleOf(g, null)).Append(". ").Append(He(g, true)).Append(" is ").Append(e.Where).Append('.');
            }
            e.Reward = QuestPay8.PromisedReward(q);
            if (e.Reward > 0) sb.Append("\n\nPromised pay: ").Append(e.Reward.ToString("N0", Inv)).Append(" denars.");
            string due = TaskDue(q, false);
            if (due != null) sb.Append('\n').Append(due);
            return sb.ToString();
        }
    }
}
