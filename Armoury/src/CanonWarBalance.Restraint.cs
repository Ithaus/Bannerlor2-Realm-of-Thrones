using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// 274 - WETA OBLEZEN: SIEDZIBY (obie strony, zawsze do Krwawych Godow) i POWSCIAGLIWOSC SILNIEJSZEGO (r poza 1.5 po 3 dobach - tylko cele frontu SCA, 55).
    /// Postfiks (priorytet 50) CampaignEventDispatcher.AiHourlyTick - po ArmsFirst p9 (VeryLow), przed LordEscort 223 i CanonWarWatch 264 (Last): ocena kazdego
    /// BesiegeSettlement (takze "zbierz armie") na twierdze drugiej strony = -1 (metoda CrossingLaw: ponizej startu wyboru gry), gdy cel to siedziba albo - dla
    /// powsciaganej strony - cel poza frontem. Ta jedna latka obejmuje gre, ROT (AIThinkPatch) i StrategicCampaignAI145 (model ocen celow - jedyna czynna droga SCA
    /// w zainstalowanej wersji). Trwajace oblezenie nie jest zdejmowane (druzyna albo jej armia juz oblega cel; dolaczenie do oblezenia swojej strony - bez weta).
    /// BK: postfiks BKArmyBehavior.FindArmyObjective - cel oblezenia (ArmyTypes.Besieger) z wetem -> brak celu (BK nie zaklada armii tej doby). Bez sztucznych strat.
    /// </summary>
    internal static partial class CanonWarBalance
    {
        /// <summary>Postfiks (priorytet 50) CampaignEventDispatcher.AiHourlyTick: weta oblezen. Nie rzuca.</summary>
        public static void VetoPostfix(MobileParty party, PartyThinkParams partyThinkParams)
        {
            if (!_active || party == null || partyThinkParams == null) return;
            try
            {
                if (!party.IsLordParty || party.IsMainParty) return;
                int side = SideOf(party.MapFaction);
                if (side == 0) return;
                var scores = partyThinkParams.AIBehaviorScores;
                if (scores == null || scores.Count == 0) return;
                if (CanonWarClock.OverNow()) return;
                var k = party.MapFaction as Kingdom;
                bool restrained = _restrain == side && RestraintOn;
                Settlement own = party.BesiegedSettlement ?? (party.Army != null && party.Army.LeaderParty != null ? party.Army.LeaderParty.BesiegedSettlement : null);
                for (int i = 0; i < scores.Count; i++)
                {
                    var e = scores[i];
                    if (e.Item1.AiBehavior != AiBehavior.BesiegeSettlement || e.Item2 <= -1f) continue;
                    var st = e.Item1.Party as Settlement;
                    if (st == null) continue;
                    int ts = SideOf(st.MapFaction);
                    bool other = ts != 0 && ts != side;
                    bool running = ReferenceEquals(st, own) || RunningOwn(st, side);
                    // recenzja 274 (1): weto siedziby tylko, gdy jest w rekach swojej strony domowej; strona domowa odbija swoja siedzibe bez weta
                    int kind = VetoKind(other, running, SeatHeld(st), restrained, IsFrontTarget(k, st), SeatHomeOf(st) == side);
                    if (kind == 0) continue;
                    var d = e.Item1;
                    partyThinkParams.SetBehaviorScore(in d, -1f);
                    if (kind == 1) _dVetoSeat++; else _dVetoFront++;
                    // recenzja 274 (M4): tekst tylko, gdy linia pojdzie (raz na klucz i dobe) - bez skladania napisu przy kazdym wecie w mysli AI
                    if (Say("weto|" + kind + "|" + st.StringId + "|" + (k != null ? k.StringId : "-")))
                        Log.Info(Tag + "weto | dzien " + Today + " | " + (kind == 1 ? "siedziba" : "za frontem") + " " + Nm(st) + " (" + st.StringId + ", " + Nm(st.MapFaction) + ") - "
                             + Nm(party.MapFaction) + " (strona " + SideName(side) + ", pierwszy " + Nm(party) + (e.Item1.WillGatherArmy ? ", zbiorka armii" : "") + ", ocena gry " + F2(e.Item2) + ")"
                             + (kind == 1 ? " - siedziby bez nowych oblezen do Krwawych Godow." : " - przewaga " + SideName(side) + " ponad 1.5: nowe oblezenia tylko celow frontu (SCA 55)."));
                }
            }
            catch (Exception e) { Stumble("VetoPostfix", e); }
        }

        /// <summary>Oblezenie tej osady juz trwa i oblega je strona partii (dolaczenie - nie nowe oblezenie).</summary>
        private static bool RunningOwn(Settlement st, int side)
        {
            try
            {
                var ev = st.SiegeEvent;
                if (ev == null || ev.BesiegerCamp == null || ev.BesiegerCamp.LeaderParty == null) return false;
                return SideOf(ev.BesiegerCamp.LeaderParty.MapFaction) == side;
            }
            catch { return false; }
        }

        /// <summary>Weto celu (siedziba / za frontem) dla krolestwa - BK i autotest.</summary>
        internal static int VetoFor(Kingdom k, Settlement st)
        {
            if (!_active || k == null || st == null || CanonWarClock.OverNow()) return 0;
            int side = SideOf(k);
            if (side == 0) return 0;
            int ts = SideOf(st.MapFaction);
            return VetoKind(ts != 0 && ts != side, RunningOwn(st, side), SeatHeld(st), _restrain == side && RestraintOn, IsFrontTarget(k, st), SeatHomeOf(st) == side);
        }

        // ================================================================ BK: cel armii
        private static string HookBk(Harmony h)
        {
            try
            {
                var t = AccessTools.TypeByName("BannerKings.Behaviours.BKArmyBehavior");
                if (t == null) return "BRAK (brak BannerKings BKArmyBehavior)";
                var m = AccessTools.Method(t, "FindArmyObjective", new[] { typeof(MobileParty), typeof(Kingdom) });
                if (m == null) return "BRAK (FindArmyObjective)";
                if (m.ReturnType != typeof(ValueTuple<Settlement, Army.ArmyTypes>?)) return "BRAK (sygnatura FindArmyObjective: " + m.ReturnType.Name + ")";
                h.Patch(m, postfix: new HarmonyMethod(typeof(CanonWarBalance), nameof(BkObjectivePostfix)));
                return "TAK";
            }
            catch (Exception e) { Stumble("HookBk", e); return "BLAD (" + e.GetType().Name + ")"; }
        }

        /// <summary>Postfiks BKArmyBehavior.FindArmyObjective(MobileParty, Kingdom): cel oblezenia z wetem -> null (BK nie zaklada armii). Nie rzuca.</summary>
        public static void BkObjectivePostfix(MobileParty __0, Kingdom __1, ref ValueTuple<Settlement, Army.ArmyTypes>? __result)
        {
            if (!_active || !__result.HasValue) return;
            try
            {
                var v = __result.Value;
                if (v.Item2 != Army.ArmyTypes.Besieger || v.Item1 == null) return;
                int kind = VetoFor(__1, v.Item1);
                if (kind == 0) return;
                __result = null;
                _dVetoBk++;
                if (Say("wetobk|" + v.Item1.StringId + "|" + (__1 != null ? __1.StringId : "-")))
                    Log.Info(Tag + "weto | dzien " + Today + " | BK cel armii " + (kind == 1 ? "siedziba " : "za frontem ") + Nm(v.Item1) + " (" + v.Item1.StringId + ") - " + Nm(__1) + " (pierwszy "
                         + Nm(__0) + ") - BK nie zaklada armii z tym celem.");
            }
            catch (Exception e) { Stumble("BkObjectivePostfix", e); }
        }
    }
}
