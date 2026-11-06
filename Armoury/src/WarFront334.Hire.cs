using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 334 B5 - W4 274: NAJEMNICY ZA ZLOTO SLABSZEJ STRONY (klucz CanonWarBalanceHireCompanies). Wola CanonWarBalance.Daily, gdy dzwignie 274 sa wlaczone, slabszy
    /// ma mniej niz silniejszy / 1.2 (CalmBand - jak W2) - czyli dopoki stosunek nie wroci do 1/1.2.
    ///  Kto: krolestwo rdzenia slabszej strony (korona albo Polnoc - symetrycznie), nie pod rzadami gracza; kontrakt 185 czynny (MercContract.On), bez tego nikt nie
    ///  placi kompanii ze skarbca. Co: wolna kompania najemna (rod najemny bez krolestwa, nie banda, nie w wojnie z krolestwem, glowa zywa i wolna, co najmniej 1
    ///  druzyna w polu) - najwieksza, na ktora stac: nadwyzka skarbca ponad zapas wojny (KingdomTreasury.TreasurySurplusFor - ta sama miara co W5b) >= kontrakt
    ///  185 (MercContractFactor x dzienny zold kompanii) x MercReviewDays (okres przegladu 185) - skarbiec nie schodzi ponizej zapasu wojny przez caly okres.
    ///  Najem droga gry ChangeKingdomAction.ApplyByJoinFactionAsMercenary; umowe i wyplaty z wplywow dnia robi 185 od nastepnej doby (zadatek BK dla kompanii AI
    ///  wylaczony przez 185 W3-A). Najwyzej 1 kompania na 7 dob (SpawnEveryDays 274). Zloto ani ludzie nie powstaja - ludzie juz sa w kompanii.
    ///  Linia 6 "najem kompanii" (zdarzenie) i segment linii doby.
    /// </summary>
    internal static partial class WarFront334
    {
        private static int _dHireN;
        private static string _dHireWhy = "-";

        private static void ZeroHireDay() { _dHireN = 0; _dHireWhy = "-"; }

        private static void CompanyOf(Clan c, List<Settlement> forts, float r2, out long wage, out int men, out int parties, out bool near)
        {
            wage = 0; men = 0; parties = 0; near = false;
            var wps = c.WarPartyComponents;
            if (wps == null) return;
            foreach (var wp in wps)
            {
                var mp = wp != null ? wp.MobileParty : null;
                if (mp == null || !mp.IsActive) continue;
                parties++;
                wage += Math.Max(0, mp.TotalWage);
                if (!near) { var p = mp.GetPosition2D; for (int i = 0; i < forts.Count && !near; i++) if (p.DistanceSquared(forts[i].GetPosition2D) <= r2) near = true; }
                if (mp.MemberRoster != null) men += mp.MemberRoster.TotalManCount;
            }
        }

        /// <summary>CanonWarBalance.Daily (dzwignie TAK, slabszy ponizej silniejszy / 1.2): najem jednej kompanii dla krolestwa k slabszej strony. Nie rzuca.</summary>
        internal static void HireDaily(Kingdom k, int today)
        {
            try
            {
                if (k == null) return;
                if (!HireOn) { _dHireWhy = "klucz NIE"; return; }
                if (_lastHireDay >= 0 && today - _lastHireDay < CanonWarBalance.SpawnEveryDays) { _dHireWhy = "ostatni najem d." + _lastHireDay + " (co " + CanonWarBalance.SpawnEveryDays + " dob)"; return; }
                if (k.Leader == Hero.MainHero) { _dHireWhy = "gracz rzadzi " + Nm(k); return; }
                if (!MercContract.On) { _dHireWhy = "kontrakt 185 nieczynny"; return; }
                var s = S;
                long reserve;
                double sur = KingdomTreasury.TreasurySurplusFor(k, out reserve);
                double factor = Math.Max(0.0, s.MercContractFactor);
                int review = Math.Max(1, s.MercReviewDays);
                Clan best = null; long bestK = 0; int bestMen = 0;
                // kompania w zasiegu krolestwa: druzyna w 5 x sredniej odleglosci dwoch najblizszych miast (prog zasiegu gry AiMilitaryBehavior) od jego twierdzy - nie z Essos
                float rng = 5f * Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.All);
                float r2 = rng * rng;
                var forts = new List<Settlement>();
                foreach (var st in k.Settlements) if (st != null && st.IsFortification) forts.Add(st);
                foreach (var c in Clan.All)
                {
                    if (c == null || c.IsEliminated || !c.IsMinorFaction || !c.IsClanTypeMercenary || c.Kingdom != null || c.IsBanditFaction || c == Clan.PlayerClan) continue;
                    if (c.Leader == null || !c.Leader.IsAlive || c.Leader.IsPrisoner) continue;
                    bool war;
                    try { war = FactionManager.IsAtWarAgainstFaction(c, k); } catch { war = true; }
                    if (war) continue;
                    long wage; int men, parties; bool near;
                    CompanyOf(c, forts, r2, out wage, out men, out parties, out near);
                    if (parties == 0 || men <= 0 || wage <= 0 || !near) continue;
                    long K = (long)Math.Ceiling(factor * wage);
                    if (K * review > sur) continue;
                    if (men > bestMen) { best = c; bestK = K; bestMen = men; }
                }
                if (best == null) { _dHireWhy = "brak kompanii na nadwyzke " + F0(sur) + " (zapas wojny " + reserve + ")"; return; }
                long t0 = k.KingdomBudgetWallet;
                ChangeKingdomAction.ApplyByJoinFactionAsMercenary(best, k, default(CampaignTime), 50, false);
                if (best.Kingdom != k) { _dHireWhy = "gra nie przyjela " + Nm(best); return; }
                _lastHireDay = today; _hiredN++; _dHireN++; _hireGoldK += bestK;
                _dHireWhy = "najeto " + Nm(best);
                long t1 = k.KingdomBudgetWallet;
                Log.Info(Tag + "najem kompanii | dzien " + today + " | " + Nm(k) + " -> " + Nm(best) + " (ludzi " + bestMen + "), kontrakt 185 K " + bestK + "/doba x " + review
                         + " dob = " + (bestK * review) + " | skarbiec przed/po " + t0 + "/" + t1 + ", zapas wojny " + reserve + ", nadwyzka " + F0(sur)
                         + " | razem najmow " + _hiredN + ", suma K " + _hireGoldK + " - ludzie juz byli w kompanii, place kontrakt 185 z wplywow dnia.");
            }
            catch (Exception e) { Stumble("HireDaily", e); _dHireWhy = "blad"; }
        }

        /// <summary>Linia doby (segment linii 6): najmy dzis / razem.</summary>
        private static void HireLine(int today)
        {
            Log.Info(Tag + "dzien " + today + " | najem kompanii: klucz " + Key01(HireOn) + ", dzis " + _dHireN + " (" + _dHireWhy + "), razem " + _hiredN + ", suma K " + _hireGoldK
                     + ", ostatni d." + _lastHireDay + ".");
        }
    }
}
