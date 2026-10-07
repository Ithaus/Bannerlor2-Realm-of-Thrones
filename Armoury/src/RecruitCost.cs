using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// CENA WERBUNKU HISTORYCZNIE (Jeff 04.10: "przelicz te pensje i to wszystko tak, aby mialo sens historyczny").
    /// Zold juz jest historyczny (t1 2 d ... t6 21 d dziennie wobec piechura 2 d, rycerza 24 d), ale cena werbunku
    /// byla w skali gry: wedle poziomu jednostki 10/20/50/100/200/400/600/1000/1500 (DefaultPartyWageModel:216) -
    /// tier 6 za 600 = prawie miesiac zoldu, przy broni i zbroi juz w pensach.
    /// Historycznie przy zaciagu placono zaliczke/"prest" - kilka do kilkunastu dni zoldu.
    /// Teraz: cena = dzienny zold jednostki (z modelu, z mnoznikiem BK) x `RecruitCostDays` (10), najemnicy x2;
    /// doplata za konia jak w grze (150/500, poza "bez sprzetu"); doplaty z praw BK zostaja proporcjonalnie.
    /// Postfix na kazdym modelu PartyWageModel (licznik zagniezdzenia - liczymy raz, na zewnatrz).
    ///
    /// KON PO CENIE TARGU (paczka 143, Jeff 07.10; HorsesAtMarketPrice): doplata za konia nie jest juz stala gry 150/500, tylko
    /// cena konia tej jednostki (miejsce Horse jej sprzetu) na targu osady, w ktorej sie werbuje (Stables.MarketPrice - ten sam
    /// targ, na ktorym notabl kupil konia ochotnikowi, VolunteerKit). Miejsce werbunku: AI - osada z latki CheckRecruiting
    /// (takze przy wjezdzie do osady, zanim druzyna w niej stanie) i TickAutoRecruitmentGarrisonChange (garnizon - jego miasto
    /// albo zamek); poza nimi gracz - Settlement.CurrentSettlement, AI - osada, w ktorej stoi kupujacy. Brak osady albo konia
    /// (okup, koszt awansu "bez sprzetu") - stala gry jak dotad. Zloto: AI placi (gra), LevyGold oddaje te sama kwote notablowi
    /// (liczy ja w tej samej chwili i w tym samym miejscu), gracz placi notablowi przez BK - nic sie nie liczy dwa razy.
    /// </summary>
    internal static class RecruitCost
    {
        [ThreadStatic] private static int _depth;
        [ThreadStatic] private static Settlement _where;     // paczka 143: osada werbunku AI (latki miejsca), null = poza nimi
        private static int _samples;                          // paczka 143: kilka pierwszych wycen konia do logu (na uruchomienie gry)
        private static readonly TextObject _txt = new TextObject("{=!}Prest money (days of pay)");

        public static void Prefix() { _depth++; }
        public static Exception Finalizer(Exception __exception) { if (_depth > 0) _depth--; return __exception; }

        public static void Postfix(CharacterObject __0, Hero __1, bool __2, ref ExplainedNumber __result)
        {
            if (_depth > 1) return;
            try
            {
                var s = Settings.Current;
                if (s == null || !s.HistoricalRecruitCost || __0 == null || __0.IsHero) return;
                int wage = Campaign.Current.Models.PartyWageModel.GetCharacterWage(__0);
                float days = Math.Max(0f, s.RecruitCostDays);
                bool merc = __0.Occupation == Occupation.Mercenary || __0.Occupation == Occupation.Gangster || __0.Occupation == Occupation.CaravanGuard;
                if (merc) days *= 2f;
                float target = Math.Max(1f, wage * days);
                if (!__2 && __0.IsMounted) target += HorseCost(__0, __1);
                float mult = 1f + __result.SumOfFactors;
                if (mult <= 0.01f) return;
                float baseNow = __result.ResultNumber / mult;
                __result.Add(target - baseNow, _txt);
            }
            catch { }
        }

        /// <summary>Paczka 143: doplata za konia rekruta - cena jego konia na targu osady werbunku (HorsesAtMarketPrice), inaczej
        /// stala gry (DefaultPartyWageModel: 150 ponizej poziomu 26, wyzej 500).</summary>
        internal static float HorseCost(CharacterObject troop, Hero buyer)
        {
            float flat = troop.Level < 26 ? 150f : 500f;
            var s = Settings.Current;
            if (s == null || !s.HorsesAtMarketPrice) return flat;
            int p = 0; ItemObject item = null; Settlement where = null;
            try
            {
                var eq = troop.Equipment;
                var horse = eq != null ? eq[EquipmentIndex.Horse] : EquipmentElement.Invalid;
                if (horse.Item == null) { var fb = troop.FirstBattleEquipment; if (fb != null) horse = fb[EquipmentIndex.Horse]; }
                item = horse.Item;
                if (item == null) return flat;
                where = WhereRecruited(buyer);
                p = Stables.MarketPrice(where, new EquipmentElement(item));
            }
            catch { return flat; }
            if (p <= 0) return flat;                          // brak osady albo targu - stala gry
            if (_samples < 8)
            {
                _samples++;
                try
                {
                    Log.Info("RecruitCost: kon rekruta " + troop.StringId + " (" + item.StringId + ", wartosc " + item.Value + ") w " + where.Name
                             + " - cena targu " + p + " zamiast stalej " + (int)flat + (buyer != null ? " (kupuje " + buyer.Name + ")" : "") + ".");
                }
                catch { }
            }
            return p;
        }

        /// <summary>Osada werbunku: z latki miejsca (AI), gracz - ta, w ktorej jest; AI - ta, w ktorej stoi jego druzyna.</summary>
        internal static Settlement WhereRecruited(Hero buyer)
        {
            if (_where != null) return _where;
            if (buyer == null) return null;
            if (buyer == Hero.MainHero) return Settlement.CurrentSettlement ?? buyer.CurrentSettlement;
            return buyer.CurrentSettlement;
        }

        /// <summary>RecruitmentCampaignBehavior.CheckRecruiting(MobileParty, Settlement): werbunek AI w tej osadzie (co godzine i przy
        /// wjezdzie - wtedy druzyna jeszcze w niej nie stoi). Poprzednia osada w __state, przywracana w finalizerze.</summary>
        public static void WherePrefix(Settlement __1, out Settlement __state) { __state = _where; _where = __1; }

        /// <summary>GarrisonRecruitmentCampaignBehavior.TickAutoRecruitmentGarrisonChange(Town): garnizon werbuje w swoim miescie
        /// albo zamku (gra liczy cene z wodzem klanu wlasciciela, ktory moze byc gdziekolwiek).</summary>
        public static void GarrisonWherePrefix(Town __0, out Settlement __state) { __state = _where; _where = __0 != null ? __0.Settlement : null; }

        public static Exception WhereFinalizer(Exception __exception, Settlement __state) { _where = __state; return __exception; }

        internal static void ApplyAll(Harmony h)
        {
            int n = 0;
            var seen = new HashSet<Type>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (var t in types)
                {
                    try
                    {
                        if (t == null || t.IsAbstract || !typeof(TaleWorlds.CampaignSystem.ComponentInterfaces.PartyWageModel).IsAssignableFrom(t)) continue;
                        var m = t.GetMethod("GetTroopRecruitmentCost", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (m == null || m.DeclaringType != t || seen.Contains(t)) continue;
                        seen.Add(t);
                        h.Patch(m, prefix: new HarmonyMethod(typeof(RecruitCost), nameof(Prefix)) { priority = Priority.First },
                                   postfix: new HarmonyMethod(typeof(RecruitCost), nameof(Postfix)) { priority = Priority.Last },
                                   finalizer: new HarmonyMethod(typeof(RecruitCost), nameof(Finalizer)));
                        n++;
                    }
                    catch { }
                }
            }
            Log.Info("RecruitCost: cena werbunku = dni zoldu w " + n + " modelach.");
            int w = 0;
            try
            {
                var cr = AccessTools.Method(typeof(RecruitmentCampaignBehavior), "CheckRecruiting", new[] { typeof(MobileParty), typeof(Settlement) });
                if (cr != null)
                {
                    h.Patch(cr, prefix: new HarmonyMethod(typeof(RecruitCost), nameof(WherePrefix)), finalizer: new HarmonyMethod(typeof(RecruitCost), nameof(WhereFinalizer)));
                    w++;
                }
                var gr = AccessTools.Method(typeof(GarrisonRecruitmentCampaignBehavior), "TickAutoRecruitmentGarrisonChange", new[] { typeof(Town) });
                if (gr != null)
                {
                    h.Patch(gr, prefix: new HarmonyMethod(typeof(RecruitCost), nameof(GarrisonWherePrefix)), finalizer: new HarmonyMethod(typeof(RecruitCost), nameof(WhereFinalizer)));
                    w++;
                }
            }
            catch (Exception e) { Log.Error("RecruitCost.Where", e); }
            Log.Info("RecruitCost: kon rekruta po cenie targu (HorsesAtMarketPrice) - miejsce werbunku AI wpiete w " + w + "/2 metodach (CheckRecruiting, TickAutoRecruitmentGarrisonChange).");
        }
    }
}
