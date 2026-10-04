using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace Armoury
{
    /// <summary>
    /// SKARBIEC KROLESTWA BEZ ZLOTA Z NICZEGO (Jeff 04.10: "jak z niczego dosypuje? ... tak, wylacz").
    /// Vanilla ClanVariablesCampaignBehavior.DailyTickClan, przy rozliczeniu rodu krola:
    ///   - skarbiec < 2 mln -> +1000 dziennie, z niczego;
    ///   - skarbiec < 1 mln -> losowanie 0.5% (ponizej 100 tys.: 1%) dziennie na +100/200/400 tys., z niczego.
    /// Transpiler zeruje te stale (ldc.i4 1000 / 100000 / 200000 / 400000 w tej jednej metodzie).
    /// Zostaje prawdziwy przeplyw: rody AI z ponad 100 tys. wplacaja 1% nadwyzki dziennie
    /// (DefaultClanFinanceModel), BK swoje wplaty; zapomogi dla biednych rodow placi skarbiec z tego,
    /// co w nim faktycznie jest. Startowe 2 mln na poczatek kampanii zostaja.
    /// </summary>
    internal static class KingdomTreasury
    {
        private static int _swapped;

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var s = Settings.Current;
            bool on = s == null || s.NoFreeKingdomGold;
            foreach (var ci in instructions)
            {
                if (on && ci.opcode == OpCodes.Ldc_I4 && ci.operand is int)
                {
                    int v = (int)ci.operand;
                    if (v == 1000 || v == 100000 || v == 200000 || v == 400000) { ci.operand = 0; _swapped++; }
                }
                yield return ci;
            }
        }

        // ------------------------------------------------------------ powinnosci wasali
        // Jeff 04.10: "prawdziwe zasilanie skarbca - kazdy rod oddaje koronie czesc dochodu, tak jak bylo historycznie".
        // Historycznie (Anglia XIII-XIV w.): w pokoju wasal dawal glownie sluzbe i okazjonalne "pomoce" - male
        // pieniadze; wielkie podatki (pietnastka i dziesiecina od ruchomosci, tarczowe zamiast sluzby) korona
        // dostawala na WOJNE. Stad dwie stawki od dziennego dochodu rodu (model finansow + nasze renty od ludnosci):
        // pokoj `CrownDuesPeacePercent` (2%), wojna krolestwa `CrownDuesWarPercent` (10%). Placa rody wasalne, takze
        // gracza; nie rod krola (to jego skarbiec), nie najemnicy. Zloto od glowy rodu do skarbca krolestwa.
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || !s.CrownDuesEnabled || TaleWorlds.CampaignSystem.Campaign.Current == null) return;
            try
            {
                long total = 0; int payers = 0; int playerPaid = 0;
                var model = TaleWorlds.CampaignSystem.Campaign.Current.Models.ClanFinanceModel;
                foreach (var c in TaleWorlds.CampaignSystem.Clan.All)
                {
                    if (c == null || c.IsEliminated || c.Kingdom == null || c.Leader == null || !c.Leader.IsAlive) continue;
                    if (c.IsUnderMercenaryService || c == c.Kingdom.RulingClan || c.IsBanditFaction) continue;
                    float income = 0f;
                    try { income = model.CalculateClanIncome(c, false, false, false).ResultNumber; } catch { }
                    int rent; PopulationLaw.RentToday.TryGetValue(c, out rent);
                    income += rent;
                    if (income <= 0f) continue;
                    bool war = false;
                    try { foreach (var k in TaleWorlds.CampaignSystem.Kingdom.All) if (k != c.Kingdom && !k.IsEliminated && c.Kingdom.IsAtWarWith(k)) { war = true; break; } } catch { }
                    float rate = (war ? s.CrownDuesWarPercent : s.CrownDuesPeacePercent) / 100f;
                    int pay = (int)Math.Min(income * rate, Math.Max(0, c.Leader.Gold));
                    if (pay <= 0) continue;
                    c.Leader.ChangeHeroGold(-pay);
                    c.Kingdom.KingdomBudgetWallet += pay;
                    total += pay; payers++;
                    if (c == TaleWorlds.CampaignSystem.Clan.PlayerClan) playerPaid = pay;
                }
                Log.Info("Korona: dzien " + (int)TaleWorlds.CampaignSystem.CampaignTime.Now.ToDays + " - powinnosci wasali " + total + " zl od " + payers + " rodow do skarbcow krolestw"
                         + (playerPaid > 0 ? " (rod gracza " + playerPaid + ")" : "") + ".");
            }
            catch (Exception e) { Log.Error("KingdomTreasury.Daily", e); }
        }

        // ROT ROTCoreBehavior.DailyTickClan: kazdy rod AI z kiesa <= 10 000 dostaje CODZIENNIE Tier x 5000 z niczego
        // (audyt 04.10, spis stalych modow) - dlatego nikt nie bankrutowal ani nie pozyczal w Banku Zelaznym.
        public static bool RotBailoutPrefix() { var s = Settings.Current; return s == null || !s.NoRotClanBailout; }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var m = AccessTools.Method(typeof(ClanVariablesCampaignBehavior), "DailyTickClan");
                if (m != null) h.Patch(m, transpiler: new HarmonyMethod(typeof(KingdomTreasury), nameof(Transpiler)));
                var rot = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTCoreBehavior");
                var rm = rot != null ? AccessTools.Method(rot, "DailyTickClan") : null;
                if (rm != null) h.Patch(rm, prefix: new HarmonyMethod(typeof(KingdomTreasury), nameof(RotBailoutPrefix)));
                Log.Info("KingdomTreasury: zapomoga ROT dla biednych rodow " + (rm != null ? "przechwycona (MCM No Rot Clan Bailout)" : "BRAK ROTCoreBehavior.DailyTickClan") + ".");
                Log.Info("KingdomTreasury: zloto z niczego do skarbca krolestw " + (m != null ? "wylaczone (podmienionych stalych " + _swapped + ", oczekiwane 4)" : "BRAK DailyTickClan") + ".");
            }
            catch (Exception e) { Log.Error("KingdomTreasury.ApplyAll", e); }
        }
    }
}
