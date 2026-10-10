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
    /// TUTAJ: kazdy tier ma WLASNY prog morale: tier 1 = DesertionMoraleTier1 (25),
    /// kazdy tier wyzej o DesertionMoraleStepPerTier (3) nizej, nie ponizej
    /// DesertionMoraleFloor (10): t1 25, t2 22, t3 19, t4 16, t5 13, t6 10, t7 10.
    /// (Jeff 17.09 po pierwszej wersji 80/5/40: "za agresywnie - dezercja tylko
    /// ponizej morale 25 i w dol".) Powyzej 25 nikt nie odchodzi.
    /// Ponizej progu ze stosu odchodzi dziennie DesertionPercentPerMoralePoint (1%)
    /// za kazdy punkt morale ponizej progu, najwyzej DesertionDailyCapPercent (25%).
    /// Przyklad: rekruci t1 przy morale 15 traca 10% dziennie, elita t6 przy 5 - 5%.
    /// Najedzona i oplacona armia (morale wysokie) nie traci nikogo. Glod i brak
    /// zoldu dzialaja jak w vanilla: obnizaja morale (kary z DefaultPartyMoraleModel)
    /// i po progach robia swoje; zold ponad limit wyplat i przepelnienie partii -
    /// bez zmian, wprost z vanilla (prywatna metoda bazowa przez refleksje).
    /// Bohaterowie nie dezerteruja. Prawo obejmuje partie gracza i jego klanu, a od
    /// paczki 183 (projekt etapu 2, krok C3; [D] 04:25 C "dezercja wedlug poziomu takze
    /// u AI - TAK") takze AI - partie lordow, zalogi i karawany, jedna regula
    /// (DesertionLawForAi; wylaczone - AI przy vanilla). Inni (Undead) zostaja przy
    /// grze. Prog projektu: dezercja AI z morale i zaleglego zoldu najwyzej bieg
    /// bazowy + 50% (linia 169c) - wiecej znaczy, ze morale AI jest za niskie (naprawiamy
    /// morale, nie dezercje). AI: bez linii na partie - liczniki doby w linii
    /// "Dezercja AI (183)" (WarLedger.OnDaily).
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
            int t1 = s != null ? s.DesertionMoraleTier1 : 25;
            int step = s != null ? s.DesertionMoraleStepPerTier : 3;
            int floor = s != null ? s.DesertionMoraleFloor : 10;
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
                return s != null && s.DesertionLawForAi && !Undead.Party(mp);   // 183: AI jak gracz; Inni (trup nie ucieka) - gra
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

        /// <summary>183: partia AI pod prawem dezercji (nie gracz i nie jego rod) - bez linii na partie, liczniki doby.</summary>
        private static bool IsAi(MobileParty mp)
        {
            try { return mp != null && !mp.IsMainParty && (mp.ActualClan == null || mp.ActualClan != Clan.PlayerClan) && (mp.LeaderHero == null || mp.LeaderHero.Clan != Clan.PlayerClan); }
            catch { return false; }
        }

        private static void NoteAi(MobileParty mp, TroopRoster roster, int byMorale, float morale)
        {
            int total = roster.TotalManCount;
            if (total <= 0) return;
            int kind = mp.IsLordParty ? 0 : mp.IsGarrison ? 1 : 2;
            DesertionAi.Parties[kind]++;
            DesertionAi.Morale[kind] += byMorale; DesertionAi.Wage[kind] += total - byMorale;
            if (byMorale <= 0) return;
            if (morale < DesertionAi.MinMorale) DesertionAi.MinMorale = morale;
            bool hungry = false; try { hungry = mp.Party != null && mp.Party.IsStarving; } catch { }
            if (hungry) DesertionAi.Hungry += byMorale;
            if (kind == 0) DesertionAi.NoteMoraleLines(mp, byMorale);   // 183d: skladniki morale (diagnoza dezercji AI)
        }

        public override float GetDesertionChanceForTroop(MobileParty mobileParty, in TroopRosterElement troopRosterElement)
        {
            if (!Governs(mobileParty)) return base.GetDesertionChanceForTroop(mobileParty, in troopRosterElement);
            try { return ChanceFor(mobileParty.Morale, troopRosterElement.Character); } catch { return 0f; }
        }

        public override TroopRoster GetTroopsToDesert(MobileParty mobileParty)
        {
            if (!Governs(mobileParty))
            {
                var vr = base.GetTroopsToDesert(mobileParty);
                // W4a: Inni (poza prawem dezercji - gra, glod) liczeni osobno do linii 183; poza progiem 183 (to nie ludzie)
                try { if (vr != null && vr.TotalManCount > 0 && Undead.Party(mobileParty)) { DesertionAi.UndeadParties++; DesertionAi.UndeadMen += vr.TotalManCount; } } catch { }
                return vr;
            }
            var roster = TroopRoster.CreateDummyTroopRoster();
            try
            {
                float morale = mobileParty.Morale;
                bool ai = IsAi(mobileParty);                 // 183: AI - liczniki doby wedlug tieru zamiast opisu partii
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
                    if (ai) { int t = ch.Tier; if (t <= 2) DesertionAi.T12 += n; else if (t <= 4) DesertionAi.T34 += n; else DesertionAi.T5 += n; }
                    else detail.Add(ch.Name + " x" + n + " (t" + ch.Tier + ", prog " + ThresholdFor(ch.Tier) + ")");
                }
                int byMorale = roster.TotalManCount;

                // zold ponad limit wyplat i przepelnienie partii - wprost z vanilla
                if (_mWage == null) _mWage = AccessTools.Method(typeof(DefaultPartyDesertionModel), "GetTroopsToDesertDueToWageAndPartySize");
                if (_mWage != null) _mWage.Invoke(this, new object[] { mobileParty, roster });
                else if (!_wageMissingLogged) { _wageMissingLogged = true; Log.Info("DesertionLaw: brak GetTroopsToDesertDueToWageAndPartySize - zold/limit partii nie licza sie do dezercji."); }
                int byWage = roster.TotalManCount - byMorale;

                if (ai) { try { NoteAi(mobileParty, roster, byMorale, morale); } catch { } }   // 183: AI - tylko liczniki doby (bez linii na partie)
                else if (roster.TotalManCount > 0)
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

    /// <summary>183: liczniki doby dezercji AI wedlug prawa dezercji (linia "Dezercja AI (183)" w WarLedger.OnDaily).</summary>
    internal static class DesertionAi
    {
        // [rodzaj]: 0 partie lordow, 1 zalogi, 2 karawany i inne
        internal static readonly int[] Parties = new int[3], Morale = new int[3], Wage = new int[3];
        internal static int T12, T34, T5, Hungry; internal static float MinMorale = float.MaxValue;
        // 183d: suma skladnikow morale (opis modelu morale gry) w partiach lordow AI z dezercja z morale; waga = 1 partia
        internal static readonly Dictionary<string, float> MoraleLines = new Dictionary<string, float>();
        internal static readonly Dictionary<string, int> MoraleLinesN = new Dictionary<string, int>();
        internal static int MoraleParties, MoraleMen;
        // W4a: dezercja Innych (model gry - poza prawem dezercji), tylko do linii 183
        internal static int UndeadParties, UndeadMen;
        internal static void NoteMoraleLines(MobileParty mp, int men)
        {
            try
            {
                var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.PartyMoraleModel : null;
                if (model == null) return;
                var en = model.GetEffectivePartyMorale(mp, true);
                MoraleParties++; MoraleMen += men;
                foreach (var ln in en.GetLines())
                {
                    string k = ln.name ?? "?";
                    float v; MoraleLines.TryGetValue(k, out v); MoraleLines[k] = v + ln.number;
                    int c; MoraleLinesN.TryGetValue(k, out c); MoraleLinesN[k] = c + 1;
                }
            }
            catch { }
        }
        internal static void Clear()
        {
            Array.Clear(Parties, 0, 3); Array.Clear(Morale, 0, 3); Array.Clear(Wage, 0, 3);
            T12 = T34 = T5 = Hungry = 0; MinMorale = float.MaxValue;
            MoraleLines.Clear(); MoraleLinesN.Clear(); MoraleParties = MoraleMen = 0;
            UndeadParties = UndeadMen = 0;
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
