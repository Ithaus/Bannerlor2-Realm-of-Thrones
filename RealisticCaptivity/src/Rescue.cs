using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace RealisticCaptivity
{
    /// <summary>Odsiecz: druzyna twojego rodu jedzie odbic cie albo wytargowac okup.</summary>
    internal static class Rescue
    {
        /// <summary>Wybiera najsilniejsza druzyne rodu, ktora moze ruszyc na odsiecz.</summary>
        internal static MobileParty FindRescueParty()
        {
            try
            {
                var clan = Clan.PlayerClan;
                if (clan == null) return null;
                MobileParty best = null;
                int bestMen = 0;
                foreach (var wp in clan.WarPartyComponents)
                {
                    var mp = (wp != null) ? wp.MobileParty : null;
                    if (mp == null || mp.IsMainParty || !mp.IsActive) continue;
                    var leader = mp.LeaderHero;
                    if (leader == null || !leader.IsAlive || leader.IsPrisoner) continue;
                    int men = mp.Party.NumberOfHealthyMembers;
                    if (men > bestMen) { bestMen = men; best = mp; }
                }
                return best;
            }
            catch (Exception e) { Log.Error("FindRescueParty", e); return null; }
        }

        /// <summary>Kieruje odsiecz w strone zdobywcy. Zwraca true, gdy juz dojechala.</summary>
        internal static bool DriveTowardCaptor(MobileParty rescuer, PartyBase captor)
        {
            try
            {
                if (rescuer == null || captor == null) return false;
                var s = Settings.Current;

                if (captor.IsSettlement && captor.Settlement != null)
                {
                    rescuer.SetMoveGoToSettlement(captor.Settlement, MobileParty.NavigationType.All, false);
                    float d = rescuer.GetPosition2D.Distance(captor.Settlement.GetPosition2D);
                    return d <= s.RescueArrivalDistance;
                }
                if (captor.IsMobile && captor.MobileParty != null)
                {
                    rescuer.SetMoveEngageParty(captor.MobileParty, MobileParty.NavigationType.All);
                    float d = rescuer.GetPosition2D.Distance(captor.MobileParty.GetPosition2D);
                    return d <= s.RescueArrivalDistance;
                }
                return false;
            }
            catch (Exception e) { Log.Error("DriveTowardCaptor", e); return false; }
        }

        /// <summary>Odbicie sila - tylko wobec bandy bez lorda.</summary>
        internal static bool TryFightRescue(MobileParty rescuer, PartyBase captor)
        {
            try
            {
                var s = Settings.Current;
                int mine = rescuer.Party.NumberOfHealthyMembers;
                int theirs = Math.Max(1, captor.NumberOfHealthyMembers);
                var leader = rescuer.LeaderHero;

                if (mine >= theirs * s.RescueStrengthRatio)
                {
                    Log.Info("Odbicie sila: " + mine + " vs " + theirs + " - sukces.");
                    Log.Player(leader.Name + " rode the brigands down and cut you loose. (" + mine + " against " + theirs + ")");
                    EndCaptivityAction.ApplyByEscape(Hero.MainHero, leader);
                    return true;
                }

                Log.Info("Odbicie sila: " + mine + " vs " + theirs + " - odparte.");
                Log.Player(leader.Name + " came for you with " + mine + " men and was beaten off by " + theirs +
                           ". They will need more swords.", true);
                return false;
            }
            catch (Exception e) { Log.Error("TryFightRescue", e); return false; }
        }

        /// <summary>Negocjacje z lordem. Charm i Leadership wyslannika oraz twoja slawa obnizaja cene.</summary>
        internal static bool TryNegotiate(MobileParty rescuer, Hero captorLord)
        {
            try
            {
                var s = Settings.Current;
                var envoy = rescuer.LeaderHero;
                if (envoy == null || captorLord == null) return false;

                int charm = envoy.GetSkillValue(DefaultSkills.Charm);
                int leadership = envoy.GetSkillValue(DefaultSkills.Leadership);
                float renown = (Clan.PlayerClan != null) ? Clan.PlayerClan.Renown : 0f;
                int relation = envoy.GetRelation(captorLord);

                // 0..1 - jak dobrym jest negocjatorem
                float skillPart = MathF.Min(1f, (charm + leadership) / 400f);
                float renownPart = MathF.Min(1f, renown / 2000f);
                float relationPart = MathF.Min(1f, MathF.Max(0f, (relation + 100) / 200f));
                float quality = skillPart * 0.5f + renownPart * 0.3f + relationPart * 0.2f;
                float discount = MathF.Min(s.NegotiationMaxDiscount, quality * s.NegotiationMaxDiscount);

                var pc = Campaign.Current.PlayerCaptivity;
                if (pc.CurrentRansomAmount <= 0) pc.SetRansomAmount();
                int asked = pc.CurrentRansomAmount;
                int agreed = Math.Max(1, (int)(asked * (1f - discount)));

                Log.Info("Negocjacje: charm=" + charm + " lead=" + leadership + " renown=" + (int)renown +
                         " rel=" + relation + " -> znizka " + (int)(discount * 100) + "%, " + asked + " -> " + agreed);

                if (Hero.MainHero.Gold >= agreed)
                {
                    GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, captorLord, agreed);
                    Log.Player(envoy.Name + " negotiated your release for " + agreed + " gold (" +
                               (int)(discount * 100) + "% off the asking price of " + asked + ").");
                    EndCaptivityAction.ApplyByRansom(Hero.MainHero, envoy);
                    return true;
                }

                // Nie masz calej kwoty - a w lochu jej nie uzbierasz.
                // Wyslannik daje slowo i reczy za reszte: wychodzisz teraz, splacasz ratami.
                if (!s.RansomDebtEnabled)
                {
                    Log.Player(envoy.Name + " talked " + captorLord.Name + " down to " + agreed +
                               " gold, but your coffers are short. Your men wait.", true);
                    return false;
                }

                int paidNow = Math.Max(0, Math.Min(Hero.MainHero.Gold, agreed));
                if (paidNow > 0) GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, captorLord, paidNow);
                int remainder = agreed - paidNow;
                int pledged = (int)(remainder * s.DebtInterest);

                var behavior = CaptivityBehavior.Instance;
                if (behavior != null) behavior.PledgeDebt(pledged, captorLord);

                Log.Info("Negocjacje z dlugiem: zaplacono " + paidNow + ", zareczono " + pledged);
                Log.Player(envoy.Name + " put up his own word for the rest. You paid " + paidNow +
                           " now and owe " + pledged + " gold, taken from your purse day by day.", true);
                EndCaptivityAction.ApplyByRansom(Hero.MainHero, envoy);
                return true;
            }
            catch (Exception e) { Log.Error("TryNegotiate", e); return false; }
        }
    }
}
