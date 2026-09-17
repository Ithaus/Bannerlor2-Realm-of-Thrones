using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// PRAWO DEZERCJI (Jeff 17.09: "300 ludzi, pelne wyzywienie, morale 100, tier 5-6 -
    /// taka armia nie dezerteruje; dezercja gdy brak zywnosci albo zoldu i niskie
    /// morale: tier 1 przy morale 80 (zaciagnieci sila, chca dac dyla kiedy moga),
    /// tier 2 przy 75 itd.").
    ///
    /// VANILLA (DefaultPartyDesertionModel, dekompilacja 17.09): dezercja z morale
    /// dopiero PONIZEJ 10 (prog jeden dla wszystkich, szansa wedle poziomu jednostki),
    /// a poza tym z zoldu (limit wyplat) i z przepelnienia partii (25% nadwyzki
    /// ponad limit dziennie, min. 1). Czyli w vanilla morale 60 nikogo nie rusza,
    /// a morale 9 rozgania wszystkich po rowno.
    ///
    /// TUTAJ: kazdy tier ma WLASNY prog morale: tier 1 = DesertionMoraleTier1 (80),
    /// kazdy tier wyzej o DesertionMoraleStepPerTier (5) nizej, nie ponizej
    /// DesertionMoraleFloor (40): t1 80, t2 75, t3 70, t4 65, t5 60, t6 55, t7 50.
    /// Ponizej progu ze stosu odchodzi dziennie DesertionPercentPerMoralePoint (1%)
    /// za kazdy punkt morale ponizej progu, najwyzej DesertionDailyCapPercent (25%).
    /// Przyklad: rekruci t1 przy morale 60 traca 20% dziennie, elita t6 przy 50 - 5%.
    /// Najedzona i oplacona armia (morale wysokie) nie traci nikogo. Glod i brak
    /// zoldu dzialaja jak w vanilla: obnizaja morale (kary z DefaultPartyMoraleModel)
    /// i po progach robia swoje; zold ponad limit wyplat i przepelnienie partii -
    /// bez zmian, wprost z vanilla (prywatna metoda bazowa przez refleksje).
    /// Bohaterowie nie dezerteruja. Domyslnie prawo obejmuje partie gracza i jego
    /// klanu; AI zostaje przy vanilla (DesertionLawForAi = false) - lordowie AI
    /// chodza z morale 50-70 i przy tych progach wykrwawialiby sie z rekrutow.
    /// GetMoraleThresholdForTroopDesertion zostaje 10 (vanilla): czyta je bazowa
    /// formula dla AI i podpowiedz morale - podniesienie go rozpedziloby dezercje AI.
    /// </summary>
    internal sealed class TierDesertionModel : DefaultPartyDesertionModel
    {
        private static System.Reflection.MethodInfo _mWage;
        private static bool _wageMissingLogged;

        internal static void Install(CampaignGameStarter starter)
        {
            try
            {
                var s = Settings.Current;
                if (starter == null || s == null || !s.DesertionLawEnabled) { Log.Info("DesertionLaw: wylaczone - dezercja wedle gry (morale ponizej 10)."); return; }
                starter.AddModel(new TierDesertionModel());
                Log.Info("DesertionLaw: model dodany - progi morale " + Table() + ", " + s.DesertionPercentPerMoralePoint.ToString("0.0")
                         + "%/pkt ponizej progu, sufit " + s.DesertionDailyCapPercent + "%/dzien, AI: " + (s.DesertionLawForAi ? "tak" : "vanilla") + ".");
            }
            catch (Exception e) { Log.Error("DesertionLaw.Install", e); }
        }

        internal static string Table()
        {
            var parts = new List<string>();
            for (int t = 1; t <= 7; t++) parts.Add("t" + t + "<" + ThresholdFor(t));
            return string.Join(" ", parts.ToArray());
        }

        /// <summary>Prog morale, ponizej ktorego jednostka tego tieru zaczyna uciekac.</summary>
        internal static int ThresholdFor(int tier)
        {
            var s = Settings.Current;
            int t1 = s != null ? s.DesertionMoraleTier1 : 80;
            int step = s != null ? s.DesertionMoraleStepPerTier : 5;
            int floor = s != null ? s.DesertionMoraleFloor : 40;
            if (tier < 1) tier = 1;
            int th = t1 - step * (tier - 1);
            if (th < floor) th = floor;
            if (th > t1) th = t1;
            return th;
        }

        private static bool Governs(MobileParty mp)
        {
            try
            {
                if (mp == null) return false;
                if (mp.IsMainParty) return true;
                var s = Settings.Current;
                if (mp.LeaderHero != null && mp.LeaderHero.Clan != null && mp.LeaderHero.Clan == Clan.PlayerClan) return true;
                if (mp.ActualClan != null && mp.ActualClan == Clan.PlayerClan) return true;
                return s != null && s.DesertionLawForAi;
            }
            catch { return false; }
        }

        private static float ChanceFor(float morale, CharacterObject ch)
        {
            if (ch == null || ch.IsHero) return 0f;
            int th = ThresholdFor(ch.Tier);
            if (morale >= th) return 0f;
            var s = Settings.Current;
            float perPoint = s != null ? s.DesertionPercentPerMoralePoint : 1f;
            float cap = (s != null ? s.DesertionDailyCapPercent : 25) / 100f;
            float c = (th - morale) * perPoint / 100f;
            if (c < 0f) c = 0f;
            if (c > cap) c = cap;
            return c;
        }

        public override float GetDesertionChanceForTroop(MobileParty mobileParty, in TroopRosterElement troopRosterElement)
        {
            if (!Governs(mobileParty)) return base.GetDesertionChanceForTroop(mobileParty, in troopRosterElement);
            try { return ChanceFor(mobileParty.Morale, troopRosterElement.Character); } catch { return 0f; }
        }

        public override TroopRoster GetTroopsToDesert(MobileParty mobileParty)
        {
            if (!Governs(mobileParty)) return base.GetTroopsToDesert(mobileParty);
            var roster = TroopRoster.CreateDummyTroopRoster();
            try
            {
                float morale = mobileParty.Morale;
                var detail = new List<string>();
                var members = mobileParty.MemberRoster;
                for (int i = 0; i < members.Count; i++)
                {
                    var el = members.GetElementCopyAtIndex(i);
                    var ch = el.Character;
                    if (ch == null || ch.IsHero || el.Number <= 0) continue;
                    float c = ChanceFor(morale, ch);
                    if (c <= 0f) continue;
                    int n = MBRandom.RoundRandomized(el.Number * c);
                    if (n > el.Number) n = el.Number;
                    if (n <= 0) continue;
                    // ranni i zdrowi odchodza proporcjonalnie (jak liczy to vanilla)
                    int wounded = el.WoundedNumber > 0 ? MBRandom.RoundRandomized(n * (el.WoundedNumber / (float)el.Number)) : 0;
                    if (wounded > el.WoundedNumber) wounded = el.WoundedNumber;
                    if (wounded > n) wounded = n;
                    roster.AddToCounts(ch, n, false, wounded);
                    detail.Add(ch.Name + " x" + n + " (t" + ch.Tier + ", prog " + ThresholdFor(ch.Tier) + ")");
                }
                int byMorale = roster.TotalManCount;

                // zold ponad limit wyplat i przepelnienie partii - wprost z vanilla
                if (_mWage == null) _mWage = AccessTools.Method(typeof(DefaultPartyDesertionModel), "GetTroopsToDesertDueToWageAndPartySize");
                if (_mWage != null) _mWage.Invoke(this, new object[] { mobileParty, roster });
                else if (!_wageMissingLogged) { _wageMissingLogged = true; Log.Info("DesertionLaw: brak GetTroopsToDesertDueToWageAndPartySize - zold/limit partii nie licza sie do dezercji."); }
                int byWage = roster.TotalManCount - byMorale;

                if (roster.TotalManCount > 0)
                {
                    Log.Info("DesertionLaw: " + mobileParty.Name + " morale " + morale.ToString("0") + " - dezercja " + roster.TotalManCount
                             + " (morale " + byMorale + (byWage > 0 ? ", zold/limit partii " + byWage : "") + ")"
                             + (detail.Count > 0 ? ": " + string.Join(", ", detail.ToArray()) : "") + ".");
                    if (mobileParty.IsMainParty)
                    {
                        if (byMorale > 0)
                            Log.Player(byMorale + " men slipped away in the night - morale " + morale.ToString("0") + " is below their threshold ("
                                       + string.Join(", ", detail.ToArray()) + ").", true);
                        if (byWage > 0)
                            Log.Player(byWage + " men left over pay or lack of room in the party.", true);
                    }
                }
            }
            catch (Exception e) { Log.Error("DesertionLaw.GetTroopsToDesert", e); }
            return roster;
        }
    }

    /// <summary>Slad w logu przy wczytaniu: ktory model dezercji naprawde rzadzi
    /// (inny mod moglby dodac swoj po naszym) i z jakimi progami.</summary>
    internal sealed class DesertionLaw : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSession);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnSession(CampaignGameStarter starter)
        {
            try
            {
                var m = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.PartyDesertionModel : null;
                string who = m != null ? m.GetType().FullName : "(brak)";
                bool ours = m is TierDesertionModel;
                Log.Info("DesertionLaw: czynny model dezercji = " + who + (ours ? " (nasz; progi " + TierDesertionModel.Table() + ")" : " (NIE NASZ - prawo dezercji nie dziala)"));
                var mp = MobileParty.MainParty;
                if (mp != null)
                    Log.Info("DesertionLaw: partia gracza " + mp.MemberRoster.TotalManCount + " ludzi, limit " + mp.Party.PartySizeLimit
                             + ", morale " + mp.Morale.ToString("0") + ", glod: " + (mp.Party.IsStarving ? "TAK" : "nie")
                             + (mp.BesiegerCamp != null ? ", OBLEGA" : "") + ".");
            }
            catch (Exception e) { Log.Error("DesertionLaw.OnSession", e); }
        }
    }
}
