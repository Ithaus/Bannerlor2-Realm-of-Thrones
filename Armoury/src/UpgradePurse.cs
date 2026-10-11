using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// E3/5 (paczka 164a-4) - OPLATA ZA AWANS DO SAKIEWKI LUDZI (projekt etapu 3, rozdz. 3; regula R4). Wylacznik UpgradeCostToPurse.
    /// Gra: AI - PartyUpgraderCampaignBehavior.ApplyEffects -> GiveGoldAction(Owner zywy, inaczej wodz zywy -> nic) z obcieciem do kiesy (gra sprawdza
    /// kiese wodza, a placi Owner - krytyka W8); gracz - ekran druzyny: koszt awansow w PartyGoldChangeAmount, przy zatwierdzeniu
    /// GiveGoldAction(nic, gracz, kwota ujemna) - w nicosc (PartyUpgraderCampaignBehavior pomija gracza - krytyka W2). Ksiega: "awanse" w nicosc
    /// 10-12 tys./d (+ gracz).
    /// Teraz (jedna regula dla AI i gracza): to, co NAPRAWDE zeszlo z kiesy platnika (migawka przed/po), idzie do sakiewki ludzi tej partii
    /// (MenPurse - zolnierz kupuje w miescie sprzet swojego stopnia, spojne z K1/175):
    ///  - zaloga - do sakiewki najwyzej do limitu K1 (MenGearSaveDays x zold - sakiewka, jak SoldierPay.GarrisonShare; krytyka D5), reszta do kasy
    ///    osady zalogi (jak czesc zoldu zalog "do kasy");
    ///  - banda, Inni, partia bez sakiewki (MenPurse wylaczone) - kasa najblizszego miasta;
    ///  - gracz: prefiks DoneLogic liczy koszt awansow tego ekranu z historii awansow ekranu (UpgradedTroopsHistory - zerowana przy zatwierdzeniu,
    ///    przywracana przy Reset/anulowaniu razem z danymi ekranu), postfiks: przy zatwierdzeniu bez DoNotApplyGoldTransactions do sakiewki jego
    ///    ludzi min(koszt awansow, spadek kiesy gracza).
    /// Ksiega: zdarzenie gry w nicosc zostaje (okno O05 / "inne"), a obok nazwana pozycja "minus zloto oddane odbiorcom przez 164a" - netto 0.
    /// Bez zmian: Stables (kon za awans), OutlawLaw (awanse band u pasera), MenUpgrade (dozbrajanie z sakiewki), RotLeaks37 (doplata przy
    /// podmianie ROT - zostaje w nicosc, poza projektem E3/5).
    /// </summary>
    internal static class UpgradePurse
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.UpgradeCostToPurse; } }
        private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }

        private static long _aiPaid, _aiPurse, _aiGarPurse, _aiGarCoffers, _aiTown, _aiLost, _plCost, _plGive;
        private static int _aiN, _plN;

        public struct UpState { public bool On; public Hero Payer; public int G0; }

        /// <summary>Prefiks ApplyEffects (priorytet nizszy niz okno O05): platnik jak w grze (Owner zywy, inaczej wodz zywy) i jego kiesa przed.</summary>
        public static void ApplyEffectsPre(PartyBase __0, out UpState __state)
        {
            __state = default(UpState);
            try
            {
                if (!On || !Live || __0 == null) return;
                var payer = __0.Owner != null && __0.Owner.IsAlive ? __0.Owner : (__0.LeaderHero != null && __0.LeaderHero.IsAlive ? __0.LeaderHero : null);
                if (payer == null) return;
                __state.On = true; __state.Payer = payer; __state.G0 = payer.Gold;
            }
            catch (Exception e) { __state.On = false; Seal31.Stumble("UpgradePurse.ApplyEffectsPre", e); }
        }

        public static void ApplyEffectsPost(PartyBase __0, UpState __state)
        {
            if (!__state.On) return;
            try
            {
                long paid = (long)__state.G0 - __state.Payer.Gold;
                if (paid <= 0) return;
                Route(__0 != null ? __0.MobileParty : null, (int)Math.Min(int.MaxValue, paid));
            }
            catch (Exception e) { Seal31.Stumble("UpgradePurse.ApplyEffectsPost", e); }
        }

        private static void Route(MobileParty mp, int paid)
        {
            _aiPaid += paid; _aiN++;
            if (mp == null) { _aiLost += paid; return; }
            bool noPurse = !MenPurse.On || mp.IsBandit || OutlawLaw.IsOutlawParty(mp) || Undead.Party(mp);
            if (noPurse) { Seal31.AddWorld(Seal31.MUpgradeAi, ToTown(VillageTakings.NearestTown(mp), paid, true)); return; }
            if (mp.IsGarrison)
            {
                long room = 0;
                if (MenUpgrade.GarrisonPurseOn)
                {
                    int wage = 0; try { wage = mp.TotalWage; } catch { wage = 0; }
                    room = (long)Math.Max(0, Settings.Current.MenGearSaveDays) * Math.Max(0, wage) - MenPurse.Get(mp);
                }
                int toPurse = (int)Math.Max(0L, Math.Min(paid, room));
                if (toPurse > 0) { MenPurse.Add(mp, toPurse); _aiGarPurse += toPurse; }
                int rest = paid - toPurse, routed = 0;
                if (rest > 0) routed = ToTown(mp.CurrentSettlement ?? mp.HomeSettlement ?? VillageTakings.NearestTown(mp), rest, false);
                Seal31.AddWorld(Seal31.MUpgradeAi, toPurse + routed);
                return;
            }
            MenPurse.Add(mp, paid); _aiPurse += paid;
            Seal31.AddWorld(Seal31.MUpgradeAi, paid);
        }

        /// <summary>Kasa osady (miasto, zamek, wies; poza GiveGoldAction - licznik Note169). Zwraca wplacone; brak osady - 0 (zloto w nicosc jak w grze, licznik).</summary>
        private static int ToTown(Settlement st, int x, bool town)
        {
            if (x <= 0) return 0;
            if (st == null || st.SettlementComponent == null || (st.Town == null && !st.IsVillage)) { _aiLost += x; return 0; }
            st.SettlementComponent.ChangeGold(x);
            MoneyLedger.Note169(MoneyLedger.N169Other, st, x);
            if (town) _aiTown += x; else _aiGarCoffers += x;
            return x;
        }

        // ------------------------------------------------------------ gracz - ekran druzyny
        public struct PlState { public bool On; public long Cost; public int G0; }

        public static void DonePre(PartyScreenLogic __instance, out PlState __state)
        {
            __state = default(PlState);
            try
            {
                if (!On || !MenPurse.On || !Live || __instance == null || __instance.DoNotApplyGoldTransactions || Hero.MainHero == null) return;
                var data = __instance.CurrentData;
                var hist = data != null ? data.UpgradedTroopsHistory : null;
                if (hist == null || hist.Count == 0) return;
                long cost = 0;
                foreach (var t in hist)
                {
                    if (t == null || t.Item1 == null || t.Item2 == null || t.Item3 <= 0) continue;
                    var tg = t.Item1.UpgradeTargets;
                    int idx = tg != null ? Array.IndexOf(tg, t.Item2) : -1;
                    if (idx < 0) continue;
                    cost += (long)t.Item1.GetUpgradeGoldCost(PartyBase.MainParty, idx) * t.Item3;
                }
                if (cost <= 0) return;
                __state.On = true; __state.Cost = cost; __state.G0 = Hero.MainHero.Gold;
            }
            catch (Exception e) { __state.On = false; Seal31.Stumble("UpgradePurse.DonePre", e); }
        }

        public static void DonePost(bool __result, PlState __state)
        {
            if (!__state.On || !__result) return;
            try
            {
                long dec = (long)__state.G0 - Hero.MainHero.Gold;
                long give = Math.Min(__state.Cost, dec);
                _plCost += __state.Cost;
                if (give <= 0 || MobileParty.MainParty == null) return;
                MenPurse.Add(MobileParty.MainParty, (int)Math.Min(int.MaxValue, give));
                Seal31.AddWorld(Seal31.MUpgradePlayer, give);
                _plGive += give; _plN++;
                Log.Player("The " + give + " denars you paid for promotions go to your men's purse - they will buy the kit of their new rank in town.");
            }
            catch (Exception e) { Seal31.Stumble("UpgradePurse.DonePost", e); }
        }

        // ------------------------------------------------------------ wpiecia, linia, porzadki
        internal static void ApplyAll()
        {
            Seal31.Patch("awanse AI do sakiewki (PartyUpgraderCampaignBehavior.ApplyEffects)", AccessTools.Method(typeof(PartyUpgraderCampaignBehavior), "ApplyEffects"), typeof(UpgradePurse),
                         nameof(ApplyEffectsPre), nameof(ApplyEffectsPost), null);
            Seal31.Patch("awanse gracza do sakiewki (PartyScreenLogic.DoneLogic)", AccessTools.Method(typeof(PartyScreenLogic), "DoneLogic", new[] { typeof(bool) }), typeof(UpgradePurse),
                         nameof(DonePre), nameof(DonePost), null);
        }

        internal static string Part()
        {
            return (On ? "" : "WYLACZONE (Upgrade Cost To Purse) - ") + "AI zaplacilo " + _aiPaid + " (" + _aiN + " awansow): do sakiewek partii " + _aiPurse + ", zalogi - do sakiewki (limit K1) " + _aiGarPurse
                   + " i do kasy osady " + _aiGarCoffers + ", bandy/Inni/bez sakiewki - do najblizszego miasta " + _aiTown + ", bez odbiorcy (prog 0) " + _aiLost
                   + "; gracz (ekran druzyny): koszt awansow " + _plCost + ", do sakiewki jego ludzi " + _plGive + " (" + _plN + " zatwierdzen)";
        }

        internal static void ClearDay() { _aiPaid = _aiPurse = _aiGarPurse = _aiGarCoffers = _aiTown = _aiLost = _plCost = _plGive = 0; _aiN = _plN = 0; }
    }
}
