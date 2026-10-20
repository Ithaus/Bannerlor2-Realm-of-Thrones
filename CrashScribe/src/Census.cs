using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace CrashScribe
{
    /// <summary>
    /// [AT3] Spis swiata w autotescie (tylko odczyt). Jeff 08.10: pod The Eyrie po roku ~150 ikon
    /// (moneta = karawana, "widly" = druzyna ani lorda, ani karawany) i gra zwalnia. Spis mowi,
    /// ile czego jest na swiecie, kto stoi w osadach i jak dlugo (miedzy spisami), ilu bohaterow,
    /// ile wpisow towarow i dziennika - czyli co roslo przez rok.
    /// </summary>
    internal static class Census
    {
        internal static int Every;   // co ile dob (0 = wylaczony)
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly Dictionary<MobileParty, KeyValuePair<Settlement, double>> Since = new Dictionary<MobileParty, KeyValuePair<Settlement, double>>();
        private static int _count;

        internal static string Kind(MobileParty p)
        {
            if (p == null) return "?";
            if (p.IsMainParty) return "gracz";
            if (p.IsGarrison) return "garnizon";
            if (p.IsMilitia) return "milicja";
            if (p.IsCaravan) return "karawana";
            if (p.IsVillager) return "wiesniacy";
            if (p.IsBandit) return "bandyci";
            if (p.IsPatrolParty) return "patrol";
            if (p.IsLordParty) return p.LeaderHero != null ? "lord" : "lord bez wodza";
            var c = p.PartyComponent;
            return c != null ? "inna: " + c.GetType().Name + " <" + (Blame.ModuleOf(c.GetType()) ?? "?") + ">" : "bez komponentu";
        }

        private static void Add(Dictionary<string, int> d, string k, int n = 1)
        {
            int v; d.TryGetValue(k, out v); d[k] = v + n;
        }

        private static string Top(Dictionary<string, int> d, int n)
        {
            return string.Join(", ", d.OrderByDescending(kv => kv.Value).Take(n).Select(kv => kv.Key + " " + kv.Value).ToArray());
        }

        internal static void Run(string label)
        {
            if (Every <= 0) return;
            try { RunInner(label); }
            catch (Exception e) { Autotest.Fail("Spis", e); }
        }

        private static void RunInner(string label)
        {
            var c = Campaign.Current;
            if (c == null) return;
            _count++;
            double now = Autotest.DayNow();

            // ---- druzyny ----
            var all = MobileParty.All;
            var kinds = new Dictionary<string, int>();
            var kindsIn = new Dictionary<string, int>();
            var kindsStuck = new Dictionary<string, int>();
            var perSettlement = new Dictionary<Settlement, List<MobileParty>>();
            int active = 0, inSettl = 0, disband = 0, aiOff = 0, men = 0;
            var seenNow = new HashSet<MobileParty>();
            for (int i = 0; i < all.Count; i++)
            {
                var p = all[i];
                if (p == null) continue;
                string k = Kind(p);
                Add(kinds, k);
                if (p.IsActive) active++;
                if (p.IsDisbanding) disband++;
                try { if (p.Ai != null && p.Ai.IsDisabled) aiOff++; } catch { }
                try { men += p.MemberRoster != null ? p.MemberRoster.TotalManCount : 0; } catch { }
                var s = p.CurrentSettlement;
                if (s == null || p.IsGarrison || p.IsMilitia || p.IsMainParty) continue;
                inSettl++;
                Add(kindsIn, k);
                List<MobileParty> l;
                if (!perSettlement.TryGetValue(s, out l)) { l = new List<MobileParty>(); perSettlement[s] = l; }
                l.Add(p);
                seenNow.Add(p);
                KeyValuePair<Settlement, double> prev;
                if (Since.TryGetValue(p, out prev) && prev.Key == s)
                {
                    if (now - prev.Value >= 1.0) Add(kindsStuck, k);
                }
                else Since[p] = new KeyValuePair<Settlement, double>(s, now);
            }
            foreach (var p in Since.Keys.Where(x => !seenNow.Contains(x)).ToList()) Since.Remove(p);

            Autotest.Note("SPIS " + label + " (nr " + _count + "): druzyn " + all.Count + " (aktywnych " + active + ", ludzi " + men + ", rozwiazywanych " + disband + ", AI wylaczone " + aiOff + ") | "
                          + Top(kinds, 30), true);
            Autotest.Note("  w osadach (bez garnizonow i milicji): " + inSettl + " | " + Top(kindsIn, 20)
                          + (_count > 1 ? " | stoja w tej samej osadzie od poprzedniego spisu (>= 1 doba): " + kindsStuck.Values.Sum() + " (" + Top(kindsStuck, 12) + ")" : ""), true);

            // ---- osady z najwieksza liczba druzyn ----
            var topS = perSettlement.OrderByDescending(kv => kv.Value.Count).Take(12).ToList();
            var sb = new StringBuilder("  najwiecej druzyn w osadzie: ");
            foreach (var kv in topS)
            {
                var d = new Dictionary<string, int>();
                foreach (var p in kv.Value) Add(d, Kind(p));
                sb.Append(kv.Key.Name).Append(" ").Append(kv.Value.Count).Append(" (").Append(Top(d, 4)).Append(") | ");
            }
            Autotest.Note(sb.ToString().TrimEnd(' ', '|'), true);

            // przyklady: osada gracza + dwie najwieksze
            var focus = new List<Settlement>();
            try { if (MobileParty.MainParty != null && MobileParty.MainParty.CurrentSettlement != null) focus.Add(MobileParty.MainParty.CurrentSettlement); } catch { }
            foreach (var kv in topS) { if (focus.Count >= 3) break; if (!focus.Contains(kv.Key)) focus.Add(kv.Key); }
            foreach (var s in focus)
            {
                List<MobileParty> l;
                if (!perSettlement.TryGetValue(s, out l)) l = new List<MobileParty>();
                int ui = 0;
                try { ui = s.Parties.Count; } catch { }
                Autotest.Note("  " + s.Name + " [" + s.StringId + "]: druzyn w srodku " + ui + " (bez garnizonu i milicji " + l.Count + "); przyklady:", false);
                foreach (var p in l.OrderBy(x => Kind(x)).Take(14))
                {
                    double since = 0;
                    KeyValuePair<Settlement, double> pv;
                    if (Since.TryGetValue(p, out pv)) since = now - pv.Value;
                    Autotest.Note("    " + Describe(p) + " | w osadzie od spisu: " + since.ToString("0.0", Inv) + " d", false);
                }
            }

            // ---- bohaterowie, rody ----
            int alive = 0, kids = 0, prisoners = 0;
            try
            {
                foreach (var h in Hero.AllAliveHeroes) { alive++; if (h.Age < 18f) kids++; if (h.IsPrisoner) prisoners++; }
            }
            catch { }
            int dead = 0;
            try { dead = Hero.DeadOrDisabledHeroes.Count; } catch { }
            int clans = 0, clansGone = 0;
            try { foreach (var cl in Clan.All) { clans++; if (cl.IsEliminated) clansGone++; } } catch { }

            // ---- towary, jency ----
            long settlItems = 0, partyItems = 0, prisonersMen = 0;
            string topRoster = "";
            int topRosterN = 0;
            try
            {
                foreach (var s in Settlement.All)
                {
                    var r = s.Party != null ? s.Party.ItemRoster : null;
                    int n = r != null ? r.Count : 0;
                    settlItems += n;
                    if (n > topRosterN) { topRosterN = n; topRoster = s.Name.ToString(); }
                    try { prisonersMen += s.Party != null && s.Party.PrisonRoster != null ? s.Party.PrisonRoster.TotalManCount : 0; } catch { }
                }
                for (int i = 0; i < all.Count; i++)
                {
                    var p = all[i];
                    if (p == null) continue;
                    try { partyItems += p.ItemRoster != null ? p.ItemRoster.Count : 0; } catch { }
                    try { prisonersMen += p.PrisonRoster != null ? p.PrisonRoster.TotalManCount : 0; } catch { }
                }
            }
            catch { }
            int itemObjs = 0;
            try { itemObjs = MBObjectManager.Instance.GetObjectTypeList<ItemObject>().Count; } catch { }

            // ---- dziennik, bitwy, zadania ----
            string logs = Collections(c.LogEntryHistory);
            int mapEvents = 0, sieges = 0, issues = 0, quests = 0;
            try { mapEvents = c.MapEventManager.MapEvents.Count; } catch { }
            try { sieges = c.SiegeEventManager.SiegeEvents.Count; } catch { }
            try { issues = c.IssueManager.Issues.Count; } catch { }
            try { quests = c.QuestManager.Quests.Count; } catch { }
            int periodic = 0;
            try
            {
                var pe = typeof(Campaign).GetProperty("CustomPeriodicCampaignEvents", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var lst = pe != null ? pe.GetValue(c) as ICollection : null;
                periodic = lst != null ? lst.Count : 0;
            }
            catch { }

            Autotest.Note("  bohaterowie: zywi " + alive + " (dzieci < 18 lat " + kids + ", w niewoli " + prisoners + "), martwi/wylaczeni " + dead
                          + " | rody " + clans + " (wymarle " + clansGone + ") | towary: wpisow w osadach " + settlItems + " (najwiecej " + topRoster + " " + topRosterN + "), w druzynach " + partyItems
                          + ", rodzajow przedmiotow w grze " + itemObjs + " | jency " + prisonersMen
                          + " | bitwy " + mapEvents + ", oblezenia " + sieges + ", sprawy " + issues + ", zadania " + quests + ", zdarzenia okresowe " + periodic
                          + " | dziennik: " + logs, true);
        }

        private static string Describe(MobileParty p)
        {
            var sb = new StringBuilder();
            try
            {
                sb.Append(p.Name).Append(" [").Append(p.StringId).Append("] ").Append(Kind(p));
                try { sb.Append(" | zach ").Append(p.DefaultBehavior).Append("/").Append(p.ShortTermBehavior); } catch { }
                try { sb.Append(" | cel ").Append(p.TargetSettlement != null ? p.TargetSettlement.Name.ToString() : "-"); } catch { }
                try { sb.Append(" | dom ").Append(p.HomeSettlement != null ? p.HomeSettlement.Name.ToString() : "-"); } catch { }
                try { sb.Append(" | ludzi ").Append(p.MemberRoster.TotalManCount); } catch { }
                try { sb.Append(" | wodz ").Append(p.LeaderHero != null ? p.LeaderHero.Name.ToString() : "-"); } catch { }
                try { sb.Append(" | rod ").Append(p.ActualClan != null ? p.ActualClan.Name.ToString() : "-"); } catch { }
                try { sb.Append(" | zloto ").Append(p.PartyTradeGold); } catch { }
                try { sb.Append(" | towarow ").Append(p.ItemRoster.Count); } catch { }
                try { if (p.IsDisbanding) sb.Append(" | ROZWIAZYWANA"); } catch { }
                try { if (p.Ai != null && p.Ai.IsDisabled) sb.Append(" | AI WYLACZONE"); } catch { }
                try { if (p.Ai != null && p.Ai.DoNotMakeNewDecisions) sb.Append(" | bez nowych decyzji"); } catch { }
                try { if (!p.IsActive) sb.Append(" | NIEAKTYWNA"); } catch { }
                try { if (p.IsCurrentlyUsedByAQuest) sb.Append(" | zadanie"); } catch { }
                try { if (p.Army != null) sb.Append(" | armia"); } catch { }
            }
            catch { }
            return sb.ToString();
        }

        /// <summary>Liczebnosc kolekcji w obiekcie (np. LogEntryHistory) - przez odbicie, bez znajomosci nazw.</summary>
        private static string Collections(object o)
        {
            if (o == null) return "-";
            try
            {
                var parts = new List<string>();
                foreach (var f in o.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    var v = f.GetValue(o) as ICollection;
                    if (v != null) parts.Add(f.Name.TrimStart('_') + " " + v.Count);
                }
                return parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "-";
            }
            catch { return "?"; }
        }
    }
}
