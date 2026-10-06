using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Armoury
{
    /// <summary>
    /// KSIEGA KRÓLESTW (wpis 77; Jeff 05.10: "jak sie maja finanse lordow i krolow po zmianach budowy - utrzymaja sie czy
    /// zbankrutuja?"). Tylko log - nic nie zmienia. Codziennie dla kazdego krolestwa: skarbiec (KingdomBudgetWallet) i zmiana
    /// od wczoraj, kiesa krola i jego bilans dzienny (model finansow rodu), suma kies rodow, ile rodow biednych (< FinanceLedgerPoor)
    /// i ile z dziennym bilansem na minusie, wojsko (ludzie w partiach rodow), czy w wojnie. Podsumowanie w glownym logu
    /// ("Skarbce:"), szczegoly - najbiedniejsze rody - w Logs/<sesja>/finanse.log ("Finanse:").
    /// </summary>
    internal static class KingdomLedger
    {
        private static readonly Dictionary<Kingdom, int> _lastWallet = new Dictionary<Kingdom, int>();
        private static readonly Dictionary<Clan, int> _lastGold = new Dictionary<Clan, int>();

        internal static void Reset() { _lastWallet.Clear(); _lastGold.Clear(); }

        internal static void Daily()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.FinanceLedgerEnabled) return;
                int day = (int)CampaignTime.Now.ToDays;
                var model = Campaign.Current.Models.ClanFinanceModel;
                foreach (var k in Kingdom.All)
                {
                    if (k == null || k.IsEliminated) continue;
                    int wallet = k.KingdomBudgetWallet, lastW;
                    bool hadW = _lastWallet.TryGetValue(k, out lastW);
                    _lastWallet[k] = wallet;
                    bool war = false;
                    try { foreach (var o in Kingdom.All) if (o != k && !o.IsEliminated && k.IsAtWarWith(o)) { war = true; break; } } catch { }

                    long sumGold = 0; int clans = 0, poor = 0, minus = 0, men = 0;
                    var rows = new List<KeyValuePair<Clan, int>>();
                    var bal = new Dictionary<Clan, int>();
                    foreach (var c in k.Clans)
                    {
                        if (c == null || c.IsEliminated || c.Leader == null) continue;
                        clans++;
                        int g = c.Gold;
                        sumGold += g;
                        if (g < s.FinanceLedgerPoor) poor++;
                        // wpis 87 (audyt pkt 16): bilans = zmiana kiesy od wczoraj - model finansow nie zna naszych przeplywow
                        int last0; bool had0 = _lastGold.TryGetValue(c, out last0);
                        int b = had0 ? g - last0 : 0;
                        bal[c] = b;
                        if (had0 && b < 0) minus++;
                        try { foreach (var wp in c.WarPartyComponents) if (wp != null && wp.MobileParty != null && wp.MobileParty.MemberRoster != null) men += wp.MobileParty.MemberRoster.TotalManCount; } catch { }
                        rows.Add(new KeyValuePair<Clan, int>(c, g));
                    }
                    var ruler = k.RulingClan;
                    int rg = ruler != null ? ruler.Gold : 0, rb = 0;
                    if (ruler != null) bal.TryGetValue(ruler, out rb);
                    Log.Info("Skarbce: dzien " + day + " - " + k.Name + (war ? " (WOJNA)" : "") + ": skarbiec " + wallet + (hadW ? " (" + Sign(wallet - lastW) + ")" : "")
                             + KingdomTreasury.RefundNote(k)
                             + ", krol " + (ruler != null ? ruler.Name.ToString() : "-") + " kiesa " + rg + " bilans " + Sign(rb) + "/dzien"
                             + "; rodow " + clans + ", kiesy razem " + sumGold + ", biednych (<" + s.FinanceLedgerPoor + ") " + poor + ", na minusie " + minus
                             + "; wojsko rodow " + men + " ludzi.");
                    rows.Sort((a, b) => a.Value.CompareTo(b.Value));
                    var parts = new List<string>();
                    foreach (var r in rows.Take(Math.Max(0, s.FinanceLedgerPoorest)))
                    {
                        int last; bool had = _lastGold.TryGetValue(r.Key, out last);
                        int b; bal.TryGetValue(r.Key, out b);
                        parts.Add(r.Key.Name + " " + r.Value + (had ? " (" + Sign(r.Value - last) + ")" : "") + " bilans " + Sign(b));
                    }
                    if (parts.Count > 0) Log.Info("Finanse: dzien " + day + " - " + k.Name + " najbiedniejsze rody: " + string.Join(", ", parts.ToArray()) + ".");
                    foreach (var r in rows) _lastGold[r.Key] = r.Value;
                }
            }
            catch (Exception e) { Log.Error("KingdomLedger", e); }
        }

        private static string Sign(int v) { return v >= 0 ? "+" + v : v.ToString(); }
    }
}
