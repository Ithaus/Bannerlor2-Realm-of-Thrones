using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// KSIEGA WOJNY (Jeff 31.08, "rob"): dwa prawa pieniadza i miecza.
    /// 1) ZALEGLY ZOLD = DEZERCJE. Vanilla juz karze morale za niewyplacony
    ///    zold (HasUnpaidWages) - my dokladamy druga polowe prawdy: po okresie
    ///    laski (2 dni) armia zaczyna sie ROZCHODZIC, dziennie 0.5% ludzi za
    ///    kazdy dzien zwloki, ELITY PIERWSZE (najwyzszy tier odchodzi
    ///    najszybciej - najemnik zna swoja cene). AI placi te sama cene, ale
    ///    o polowe lagodniej (biedni lordowie BK nie moga stopniec globalnie).
    ///    Garnizony i umarli poza prawem (osada placi; trup zoldu nie bierze).
    /// 2) SZTURM ZOSTAWIA KRATER. Miasto/zamek wziete obleczeniem traci
    ///    prosperity (dom. -15%) i lojalnosc (-15) - zdobycz jest zdobycza
    ///    ZRUJNOWANA, ktora trzeba odbudowac, nie darmowa nagroda.
    /// </summary>
    internal static class WarLedger
    {
        private static readonly Dictionary<MobileParty, int> _unpaidDays = new Dictionary<MobileParty, int>();

        internal static void OnDaily()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.WagesDueEnabled) return;

                var seen = new List<MobileParty>();
                foreach (var mp in MobileParty.All)
                {
                    if (mp == null || !mp.IsActive || !mp.IsLordParty) continue;
                    if (Undead.Party(mp)) continue;
                    bool unpaid = false;
                    try { unpaid = mp.HasUnpaidWages > 0f; } catch { }
                    if (!unpaid) { _unpaidDays.Remove(mp); continue; }

                    int d;
                    _unpaidDays.TryGetValue(mp, out d);
                    _unpaidDays[mp] = ++d;
                    seen.Add(mp);
                    int over = d - Math.Max(0, s.WagesGraceDays);
                    if (over <= 0)
                    {
                        if (mp == MobileParty.MainParty)
                            Log.Player("The war chest is empty - the men grumble. Pay them, or they will start walking.", true);
                        continue;
                    }

                    float pct = Math.Max(0f, s.WagesDesertPercentPerDay) / 100f * over;
                    if (mp != MobileParty.MainParty) pct *= 0.5f;      // AI lagodniej - swiat nie moze stopniec
                    int men = mp.MemberRoster.TotalManCount;
                    float exp = men * pct;
                    int leave = (int)exp;
                    if (MBRandom.RandomFloat < exp - leave) leave++;
                    if (leave <= 0) continue;

                    int gone = DesertElitesFirst(mp, leave);
                    if (gone > 0 && mp == MobileParty.MainParty)
                        Log.Player("Unpaid and unbound: " + gone + " men desert in the night - the best-paid first.", true);
                    else if (gone > 0)
                        Log.Info("WarLedger: " + mp.StringId + " traci " + gone + " ludzi (zold niewyplacony " + d + " dni).");
                }
                if (_unpaidDays.Count > seen.Count + 50)
                {
                    var drop = new List<MobileParty>();
                    foreach (var kv in _unpaidDays)
                        if (kv.Key == null || !kv.Key.IsActive || !seen.Contains(kv.Key)) drop.Add(kv.Key);
                    foreach (var k in drop) _unpaidDays.Remove(k);
                }
            }
            catch (Exception e) { Log.Error("WarLedger.OnDaily", e); }
        }

        /// <summary>Najemnik zna swoja cene: dezerteruja od najwyzszego tieru.</summary>
        private static int DesertElitesFirst(MobileParty mp, int count)
        {
            int gone = 0;
            try
            {
                var roster = mp.MemberRoster;
                while (count > 0)
                {
                    int best = -1, bestTier = -1;
                    for (int i = 0; i < roster.Count; i++)
                    {
                        var ch = roster.GetCharacterAtIndex(i);
                        if (ch == null || ch.IsHero) continue;
                        int n = roster.GetElementNumber(i);
                        if (n <= 0) continue;
                        if (ch.Tier > bestTier) { bestTier = ch.Tier; best = i; }
                    }
                    if (best < 0) break;
                    var c = roster.GetCharacterAtIndex(best);
                    int take = Math.Min(count, roster.GetElementNumber(best));
                    roster.AddToCounts(c, -take);
                    gone += take; count -= take;
                }
            }
            catch { }
            return gone;
        }

        /// <summary>Miasto wziete obleczeniem: prosperity -15%, lojalnosc -15.</summary>
        internal static void OnOwnerChanged(Settlement st, bool openToClaim, Hero newOwner, Hero oldOwner,
                                            Hero capturer, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.SackScarEnabled) return;
                if (detail != ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege) return;
                var town = st != null ? st.Town : null;
                if (town == null) return;
                float cut = MBMath.ClampFloat(Math.Max(0, s.SackProsperityCutPercent) / 100f, 0f, 0.6f);
                float before = town.Prosperity;
                town.Prosperity = Math.Max(0f, town.Prosperity * (1f - cut));
                town.Loyalty = Math.Max(0f, town.Loyalty - Math.Max(0, s.SackLoyaltyHit));
                Log.Info("WarLedger: " + st.StringId + " wziete obleczeniem - prosperity " + (int)before
                         + " -> " + (int)town.Prosperity + ", lojalnosc -" + s.SackLoyaltyHit + ".");
                if (newOwner != null && newOwner.Clan == Clan.PlayerClan)
                    Log.Player(st.Name + " is yours - but the sack has left it bleeding: prosperity and loyalty are down. Rebuild what you broke.", true);
            }
            catch (Exception e) { Log.Error("WarLedger.OnOwnerChanged", e); }
        }
    }
}
