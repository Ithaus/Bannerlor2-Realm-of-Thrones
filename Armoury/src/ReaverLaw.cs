using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// ZELAZNE WYSPY Z RABUNKU, WOLNI LUDZIE BEZ ZOLDU (decyzja Jeffa 14.10; docs/wyspy-wolni/PROJEKT-WYSPY-WOLNI.md) - czesc wspolna najezdzcow.
    /// Najezdzcy nowych regul: krolestwa ReaverRealms = Zelazne Wyspy ("sturgia") i Wolni Ludzie ("freefolk"); Dothrakowie ("khuzait") poza regulami
    /// (Jeff o nich nie decydowal - zostaja na W-1, lup w D).
    ///  - WW0 (bez klucza, sam pomiar): postfiks Low na CampaignEventDispatcher.AiHourlyTick (ten sam punkt co H1 Strazy - WatchLaw.AiTickPostfix, inna frakcja)
    ///    liczy dla partii lordow AI najezdzcow decyzje AI: ile mysli z opcja rabunku, ile opcji, najlepszy wynik rabunku wobec najlepszego wyniku innej opcji,
    ///    ile razy rabunek byl na gorze listy; raz na dobe stan partii (armia, oblezenie, rabunek, obrona, postoj), dni jedzenia (prog gry
    ///    NeededFoodsInDaysThresholdForRaid czytany z modelu) i wrogowie w wojnie. Raz na sesje progi zasiegu najazdu (5 x srednia odleglosc dwoch najblizszych
    ///    miast - AiMilitaryBehavior.GetDistanceScoreForRaiding) i odleglosci Pyke -> Flint's Finger, Seagard, Lannisport, Greenshield (Wyspy Tarczowe).
    ///    Linie: "Najezdzcy - decyzje AI (WW0)" (doba) i "Najezdzcy - zasieg (WW0)" (raz na sesje). Po co: dlaczego Wyspy po d405 i Wolni Ludzie po Q1 nie
    ///    rabuja - przyczyn nie zakladamy, poprawka dopiero po tescie.
    /// Wyjatek per partia - licznik potkniec (bez globalnych wylacznikow, CLAUDE.md 7). Stan tylko w pamieci sesji; Reset w konstruktorze ArmouryBehavior.
    /// </summary>
    internal static class ReaverLaw
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const string IronId = "sturgia", FreeId = "freefolk";
        private const int GIron = 0, GFree = 1, Groups = 2;
        private static readonly string[] GName = { "Iron Islands", "Free Folk" };

        /// <summary>Grupa najezdzcow frakcji: 0 Zelazne Wyspy, 1 Wolni Ludzie, -1 inna (takze Dothrakowie).</summary>
        internal static int GroupOf(IFaction f)
        {
            if (f == null || f.StringId == null) return -1;
            if (f.StringId == IronId) return GIron;
            if (f.StringId == FreeId) return GFree;
            return -1;
        }

        internal static bool IsReaver(IFaction f) { return GroupOf(f) >= 0; }
        internal static bool IsFreeFolk(IFaction f) { return f != null && f.StringId == FreeId; }

        // ---------------------------------------------------------------- WW0: liczniki decyzji AI (od ostatniej linii doby)
        private sealed class Think
        {
            public int Thinks, WithRaid, Options, RaidTop;
            public readonly HashSet<MobileParty> PartiesWithRaid = new HashSet<MobileParty>();
            public readonly HashSet<Settlement> Villages = new HashSet<Settlement>();
            public readonly List<float> RaidBest = new List<float>(), OtherBest = new List<float>();
            public void Clear() { Thinks = WithRaid = Options = RaidTop = 0; PartiesWithRaid.Clear(); Villages.Clear(); RaidBest.Clear(); OtherBest.Clear(); }
        }
        private static readonly Think[] _think = { new Think(), new Think() };
        private const int MaxSamples = 4096;

        private static bool _patched, _rangeLogged;
        private static string _missing;
        private static MethodBase _target;
        private static int _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();

        internal static void Reset()
        {
            for (int g = 0; g < Groups; g++) _think[g].Clear();
            _rangeLogged = false; _stumbles = 0;
            lock (_err) _err.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            bool first;
            lock (_err) first = _err.Add(where);
            if (first) { try { Log.Error("ReaverLaw." + where, e); } catch { } }
        }

        // ---------------------------------------------------------------- wpiecie (SubModuleMain - po WatchLaw.ApplyAll; klasa gry bez pol statycznych z tekstow)
        internal static void ApplyAll(Harmony h)
        {
            if (h == null || _patched) return;
            try
            {
                var m = AccessTools.Method(typeof(CampaignEventDispatcher), "AiHourlyTick", new[] { typeof(MobileParty), typeof(PartyThinkParams) });
                _target = m;
                if (m == null) { _missing = "CampaignEventDispatcher.AiHourlyTick"; return; }
                h.Patch(m, postfix: new HarmonyMethod(typeof(ReaverLaw), nameof(AiTickPostfix)) { priority = Priority.Low });
                _patched = true;
            }
            catch (Exception e) { _missing = "CampaignEventDispatcher.AiHourlyTick (" + e.GetType().Name + ")"; Log.Error("ReaverLaw.ApplyAll", e); }
            Log.Info("Najezdzcy (WW0): postfiks decyzji AI (CampaignEventDispatcher.AiHourlyTick, Low) " + (_patched ? "WPIETY" : "BRAK (" + (_missing ?? "?") + ")") + ".");
        }

        /// <summary>
        /// Postfiks Low (po wszystkich odbiorcach zdarzenia - opcje gry, BK, ROT, NavalDLC sa juz na liscie): partie lordow AI najezdzcow. WW0 - tylko liczy.
        /// </summary>
        public static void AiTickPostfix(MobileParty party, PartyThinkParams partyThinkParams)
        {
            if (party == null || partyThinkParams == null) return;
            try
            {
                if (!party.IsLordParty || party.IsMainParty) return;
                int g = GroupOf(party.MapFaction);
                if (g < 0) return;
                var list = partyThinkParams.AIBehaviorScores;
                var t = _think[g];
                t.Thinks++;
                if (list == null || list.Count == 0) return;
                float bestRaid = float.MinValue, bestOther = float.MinValue;
                int nRaid = 0;
                foreach (var it in list)
                {
                    if (it.Item1.AiBehavior == AiBehavior.RaidSettlement)
                    {
                        nRaid++;
                        if (it.Item2 > bestRaid) bestRaid = it.Item2;
                        var st = it.Item1.Party as Settlement;
                        if (st != null && t.Villages.Count < MaxSamples) t.Villages.Add(st);
                    }
                    else if (it.Item2 > bestOther) bestOther = it.Item2;
                }
                if (nRaid <= 0) return;
                t.WithRaid++; t.Options += nRaid; t.PartiesWithRaid.Add(party);
                if (t.RaidBest.Count < MaxSamples) { t.RaidBest.Add(bestRaid); t.OtherBest.Add(bestOther == float.MinValue ? -1f : bestOther); }
                if (bestRaid > bestOther) t.RaidTop++;
            }
            catch (Exception e) { Stumble("AiTickPostfix", e); }
        }

        /// <summary>Dni jedzenia partii (jak MobileParty.GetNumDaysForFoodToLast, bez dzielenia przez 0) przy `more` ludziach wiecej niz teraz (zuzycie na
        /// czlowieka z FoodChange partii); partia bez zuzycia - 999.</summary>
        internal static float FoodDays(MobileParty mp, int more)
        {
            if (mp == null || mp.ItemRoster == null) return 0f;
            double use = Math.Max(0f, -mp.FoodChange);
            if (!(use > 1e-6)) return 999f;
            if (more > 0)
            {
                int men = mp.MemberRoster != null ? mp.MemberRoster.TotalManCount : 0;
                int pris = mp.PrisonRoster != null ? mp.PrisonRoster.TotalManCount : 0;
                int all = men + pris;
                if (all > 0) use = use * (all + more) / all;
            }
            return (float)(mp.ItemRoster.TotalFood / use);
        }

        // ---------------------------------------------------------------- linie doby
        private static string F(double v, string f) { return v.ToString(f, Inv); }

        private static double Median(List<float> xs)
        {
            if (xs == null || xs.Count == 0) return double.NaN;
            var a = new List<float>(xs); a.Sort();
            int n = a.Count;
            return (n % 2) == 1 ? a[n / 2] : 0.5 * (a[n / 2 - 1] + a[n / 2]);
        }

        /// <summary>ArmouryBehavior.OnDailyTick po DistrictRaid.Daily: linie WW0 (decyzje AI i - raz na sesje - zasieg), zerowanie licznikow doby.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            try
            {
                if (s == null || Campaign.Current == null || !s.LogEnabled) return;
                if (!_rangeLogged) { _rangeLogged = true; try { RangeLine(); } catch (Exception e) { Stumble("RangeLine", e); } }
                DecisionLine();
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { for (int g = 0; g < Groups; g++) _think[g].Clear(); }
        }

        private static void DecisionLine()
        {
            int day = (int)CampaignTime.Now.ToDays - 1;
            float need = 8f;
            try { need = Campaign.Current.Models.MobilePartyAIModel.NeededFoodsInDaysThresholdForRaid; } catch { need = 8f; }
            var sb = new StringBuilder(1400);
            sb.Append("Najezdzcy - decyzje AI (WW0): dzien ").Append(day).Append(" | prog jedzenia do najazdu (gra) ").Append(F(need, "0.#")).Append(" dni");
            for (int g = 0; g < Groups; g++)
            {
                int parties = 0, army = 0, siege = 0, raid = 0, defend = 0, hold = 0, hungry = 0;
                var food = new List<float>();
                Kingdom k = null;
                foreach (var kk in Kingdom.All) if (kk != null && !kk.IsEliminated && GroupOf(kk) == g) { k = kk; break; }
                foreach (var mp in MobileParty.All)
                {
                    try
                    {
                        if (mp == null || !mp.IsActive || !mp.IsLordParty || mp.IsMainParty || GroupOf(mp.MapFaction) != g) continue;
                        parties++;
                        if (mp.Army != null) army++;
                        if (mp.BesiegedSettlement != null || mp.SiegeEvent != null) siege++;
                        else if ((mp.MapEvent != null && mp.MapEvent.IsRaid) || mp.DefaultBehavior == AiBehavior.RaidSettlement) raid++;
                        else if (mp.DefaultBehavior == AiBehavior.DefendSettlement) defend++;
                        else if (mp.DefaultBehavior == AiBehavior.Hold || mp.DefaultBehavior == AiBehavior.None) hold++;
                        float fd = FoodDays(mp, 0);
                        if (food.Count < MaxSamples) food.Add(fd);
                        if (fd < need) hungry++;
                    }
                    catch (Exception e) { Stumble("DecisionLine(partia)", e); }
                }
                food.Sort();
                int enemies = 0;
                try
                {
                    if (k != null && k.FactionsAtWarWith != null)
                        foreach (var f in k.FactionsAtWarWith) if (f != null && f.IsKingdomFaction && f.Leader != null) enemies++;
                }
                catch (Exception e) { Stumble("DecisionLine(wojny)", e); }
                var t = _think[g];
                double mr = Median(t.RaidBest), mo = Median(t.OtherBest);
                sb.Append(" | ").Append(GName[g]).Append(": partii ").Append(parties).Append(" (armia ").Append(army).Append(", oblezenie ").Append(siege)
                  .Append(", rabunek ").Append(raid).Append(", obrona ").Append(defend).Append(", postoj ").Append(hold).Append(')')
                  .Append(", jedzenie min/med ").Append(food.Count > 0 ? F(food[0], "0") : "-").Append('/').Append(food.Count > 0 ? F(food[food.Count / 2], "0") : "-")
                  .Append(" dni (< prog: ").Append(hungry).Append(')')
                  .Append(", mysli ").Append(t.Thinks).Append(", z opcja rabunku ").Append(t.WithRaid).Append(" (partii ").Append(t.PartiesWithRaid.Count)
                  .Append(", opcji ").Append(t.Options).Append(", wsi ").Append(t.Villages.Count).Append(')')
                  .Append(", wynik rabunku/innej (mediana) ").Append(double.IsNaN(mr) ? "-" : F(mr, "0.000")).Append('/').Append(double.IsNaN(mo) ? "-" : F(mo, "0.000"))
                  .Append(", rabunek na gorze ").Append(t.RaidTop)
                  .Append("; wrogow w wojnie ").Append(enemies);
            }
            sb.Append(" | postfiks ").Append(_patched ? "wpiety" : "BRAK").Append(" | potkniecia ").Append(_stumbles).Append('.');
            Log.Info(sb.ToString());
        }

        private static string Dist(Settlement a, Settlement b)
        {
            try
            {
                if (a == null || b == null) return "-";
                float d = Campaign.Current.Models.MapDistanceModel.GetDistance(a, b, a.HasPort, b.HasPort, MobileParty.NavigationType.All);
                return d >= 0f && d < 1e7f ? F(d, "0") : "-";
            }
            catch { return "?"; }
        }

        private static void RangeLine()
        {
            var c = Campaign.Current;
            float dd = c.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.Default);
            float dn = c.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.Naval);
            float da = c.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.All);
            var pyke = Settlement.Find("town_S3");
            string[] ids = { "ROT_town34", "ROT_town33", "town_V9", "ROT_town27_village2" };
            var sb = new StringBuilder(400);
            sb.Append("Najezdzcy - zasieg (WW0): srednia odleglosc dwoch najblizszych miast Default/Naval/All ").Append(F(dd, "0.0")).Append('/').Append(F(dn, "0.0")).Append('/').Append(F(da, "0.0"))
              .Append(", prog najazdu (5x) ").Append(F(5 * dd, "0")).Append('/').Append(F(5 * dn, "0")).Append('/').Append(F(5 * da, "0"))
              .Append(" | ").Append(pyke != null ? pyke.Name.ToString() : "Pyke (brak town_S3)").Append(" -> ");
            for (int i = 0; i < ids.Length; i++)
            {
                var st = Settlement.Find(ids[i]);
                if (i > 0) sb.Append(", ");
                sb.Append(st != null ? st.Name.ToString() : ids[i]).Append(' ').Append(Dist(pyke, st));
            }
            sb.Append(" (droga MapDistanceModel, All).");
            Log.Info(sb.ToString());
        }
    }
}
