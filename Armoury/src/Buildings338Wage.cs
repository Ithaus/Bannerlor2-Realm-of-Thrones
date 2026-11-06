using System;
using System.Collections.Generic;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace Armoury
{
    /// <summary>
    /// 338 grupa 1 - ZOLD ZALOG (Barracks miasta -5/-10/-15%, Barracks zamku -10/-20/-30%, Castellan's Office -10/-20/-30%, projekt Slacken Garrison -5%).
    /// Gra czyta GarrisonWageReduction tylko w DefaultPartyWageModel.GetTotalWage:147; czynny lancuch BKROT -> ROT -> NavalDLC -> BEE -> BK tej metody nie wola,
    /// a kopia BK (BKPartyWageModel:208-214) bierze tylko wartosci > 0 (w grze sa ujemne) - efekt martwy (at337: 213 twierdz, ok. 31 400 zl/d).
    /// Naprawa: w zewnetrznym GetTotalWage zalogi (MountedWage.TotalPostfix - ten sam licznik "wywolanie wewnetrzne tej samej partii" co WW-J/288/289)
    /// town.AddEffectOfBuildings(GarrisonWageReduction) - jak czysta gra: AddFactor do tego samego wyniku, projekt dzienny tylko biezacy, bez dzielenia przez 100.
    /// Straznik dubla: budynek z wartoscia > 0 (BK poprawil znak - BK liczy sam) - pomijamy, licznik "pominiete (BK liczy)".
    /// Limit zlota zalog AI (Settlement.GarrisonWagePaymentLimit, UpdateClanSettlementsPaymentLimit) x (zold z budynkami / bez) tej twierdzy z ostatniej doby:
    /// ta sama liczba ludzi, oszczednosc zostaje w kiesie pana (wzor 337 p. 3.2(a)). Rod gracza bez skalowania (limit to jego wybor).
    /// Scalanie z 337: opcja B GarrisonBuildingWage337 to ten sam efekt - zostaje tylko 338; skalowanie limitu 337 (b = FullTotalWage, bez 337 i bez 338) i 338
    /// nie moga oba mnozyc limitu tej samej twierdzy (CHANGELOG 338).
    /// </summary>
    internal static class Buildings338Wage
    {
        [ThreadStatic] private static bool _skip;   // linia doby: zold bez budynkow 338
        private static int _calls, _bkCounts, _defCounts;
        [ThreadStatic] private static MobileParty _defRan;   // partia, dla ktorej biegl DefaultPartyWageModel.GetTotalWage (gra liczy budynki sama :147) - straznik dubla
        private static int _limScaled;
        private static readonly Dictionary<Settlement, float> _scale = new Dictionary<Settlement, float>();
        private static readonly object Gate = new object();
        private static bool _hooked;

        internal static void SessionStart()
        {
            lock (Gate) _scale.Clear();
            _calls = _bkCounts = _defCounts = _limScaled = 0;
            if (_hooked) { Buildings338.Hook("zold: limit AI wpiety"); return; }
            var m = AccessTools.Method(typeof(ClanVariablesCampaignBehavior), "UpdateClanSettlementsPaymentLimit", new[] { typeof(Clan) });
            if (m == null) { Buildings338.Hook("zold: BRAK UpdateClanSettlementsPaymentLimit"); return; }
            // recenzja 338 (techniczna W1): PO ClanBudget.GarLimitPostfix (com.jeff.armoury, Priority.Last) - skalujemy min(limit gry, cel pokojowy budzetu);
            // cel pokojowy ClanBudget liczy zold zalogi bez 338 (RawWage), wiec obie skale sa w tym samym zlocie "bez budynkow" i x s dziala raz
            Buildings338.H.Patch(m, postfix: new HarmonyMethod(typeof(Buildings338Wage), nameof(LimitPostfix)) { priority = Priority.Last, after = new[] { "com.jeff.armoury" } });
            var d = AccessTools.Method(typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel), "GetTotalWage");
            if (d != null && d.GetParameters().Length > 0 && d.GetParameters()[0].ParameterType == typeof(MobileParty))
                Buildings338.H.Patch(d, prefix: new HarmonyMethod(typeof(Buildings338Wage), nameof(DefaultPrefix)));
            _hooked = true;
            Buildings338.Hook("zold: limit AI wpiety");
        }

        /// <summary>BK liczy efekt sam (ktorys budynek ma GarrisonWageReduction > 0 - BK bierze tylko dodatnie) - wtedy 338 nic nie dodaje.</summary>
        private static bool BkCounts(Town t)
        {
            foreach (var b in t.Buildings)
            {
                if (b == null || b.BuildingType == null || b.CurrentLevel <= 0) continue;
                if (b.BuildingType.GetBaseBuildingEffectAmount(BuildingEffectEnum.GarrisonWageReduction, b.CurrentLevel) > 0f) return true;
            }
            return false;
        }

        /// <summary>Straznik dubla (recenzja 338): gdy ktorys model lancucha zawola DefaultPartyWageModel.GetTotalWage, gra liczy budynki sama - Apply ich nie dolicza.</summary>
        public static void DefaultPrefix(MobileParty __0) { _defRan = __0; }

        /// <summary>Zold zalogi bez budynkow 338 (ClanBudget: cel pokojowy i srednia wojenna w zlocie "bez budynkow").</summary>
        internal static int RawWage(MobileParty mp)
        {
            if (mp == null) return 0;
            bool was = _skip; _skip = true;
            try { return mp.TotalWage; } finally { _skip = was; }
        }

        /// <summary>Z MountedWage.TotalPostfix - zewnetrzne GetTotalWage partii (nie linia "przed" WW-J, nie wywolanie wewnetrzne tej samej partii).</summary>
        internal static void Apply(MobileParty g, ref ExplainedNumber r)
        {
            var def = _defRan; _defRan = null;
            if (_skip || g == null || !g.IsGarrison || !Buildings338.WageOn) return;
            try
            {
                if (def == g) { Interlocked.Increment(ref _defCounts); return; }   // Default biegl wewnatrz tego wywolania - budynki juz policzone
                if (CrownGifts.IsUnpaidParty(g)) return;   // Straz / Wolni Ludzie - zold 0
                var st = g.CurrentSettlement;
                var t = st != null ? st.Town : null;
                if (t == null || !(r.ResultNumber > 0f)) return;
                if (BkCounts(t)) { Interlocked.Increment(ref _bkCounts); return; }
                t.AddEffectOfBuildings(BuildingEffectEnum.GarrisonWageReduction, ref r);   // jak DefaultPartyWageModel:147
                Interlocked.Increment(ref _calls);
            }
            catch (Exception e) { Buildings338.Stumble(Buildings338.GWage, "Wage.Apply", e); }
        }

        public static void LimitPostfix(Clan __0)
        {
            try
            {
                if (__0 == null || __0 == Clan.PlayerClan || __0.Fiefs == null || !Buildings338.WageOn) return;
                // ten sam warunek co gra (limit przeliczony tylko dla rodow krolestw i buntownikow) - inaczej mnozylibysmy stary limit co dobe
                if (__0.MapFaction == null || (!__0.IsRebelClan && !__0.MapFaction.IsKingdomFaction)) return;
                int maxLim = Campaign.Current.Models.PartyWageModel.MaxWagePaymentLimit;
                lock (Gate)
                {
                    if (_scale.Count == 0) return;
                    foreach (var f in __0.Fiefs)
                    {
                        var st = f != null ? f.Settlement : null;
                        float s;
                        if (st == null || !_scale.TryGetValue(st, out s)) continue;
                        int lim = st.GarrisonWagePaymentLimit;
                        if (lim <= 0 || lim >= maxLim) continue;   // "bez limitu" zostaje bez limitu
                        int nl = (int)(lim * s);
                        if (nl < lim) { st.SetGarrisonWagePaymentLimit(nl); _limScaled++; }
                    }
                }
            }
            catch (Exception e) { Buildings338.Stumble(Buildings338.GWage, "Wage.LimitPostfix", e); }
        }

        internal static void Daily(int day)
        {
            int n = 0, tb = 0, cb = 0, off = 0, sl = 0;
            long before = 0, after = 0, aiMen = 0;
            var scale = new Dictionary<Settlement, float>();
            bool on = Buildings338.WageOn;
            foreach (var mp in MobileParty.All)
            {
                try
                {
                    if (mp == null || !mp.IsActive || !mp.IsGarrison || CrownGifts.IsUnpaidParty(mp)) continue;
                    var st = mp.CurrentSettlement; var t = st != null ? st.Town : null;
                    if (t == null || mp.MemberRoster == null || mp.MemberRoster.TotalManCount <= 0) continue;
                    bool player = mp.Owner != null && mp.Owner.Clan == Clan.PlayerClan;
                    if (!player) aiMen += mp.MemberRoster.TotalRegulars;
                    var x = new ExplainedNumber(1f);
                    t.AddEffectOfBuildings(BuildingEffectEnum.GarrisonWageReduction, ref x);
                    if (!(x.ResultNumber < 1f)) continue;
                    n++;
                    if (Buildings338.Level(t, DefaultBuildingTypes.SettlementBarracks.StringId) > 0 && t.IsTown) tb++;
                    if (Buildings338.Level(t, DefaultBuildingTypes.CastleBarracks.StringId) > 0 && t.IsCastle) cb++;
                    if (Buildings338.Level(t, DefaultBuildingTypes.CastleCastallansOffice.StringId) > 0) off++;
                    var cur = t.CurrentDefaultBuilding;
                    if (cur != null && cur.BuildingType == DefaultBuildingTypes.CastleDailySlackenGarrison) sl++;
                    if (!on) continue;
                    int a = mp.TotalWage, b;
                    bool was = _skip; _skip = true;
                    try { b = mp.TotalWage; } finally { _skip = was; }
                    before += b; after += a;
                    if (!player && b > 0 && a < b) scale[st] = (float)a / b;
                }
                catch (Exception e) { Buildings338.Stumble(Buildings338.GWage, "Wage.Daily", e); }
            }
            int lim;
            lock (Gate) { _scale.Clear(); foreach (var kv in scale) _scale[kv.Key] = kv.Value; lim = _limScaled; _limScaled = 0; }
            Log.Info("Budynki (338) zold: dzien " + day + " | twierdz " + n + " z budynkiem (miasto Barracks " + tb + ", zamek Barracks " + cb + ", biuro " + off + ", Slacken " + sl + ")"
                     + " | zold przed " + before + " po " + after + " oszczednosc " + (before - after) + " zl/d"
                     + " | limit AI x s: " + scale.Count + " twierdz, skalowan " + lim
                     + " | pominiete (BK liczy) " + (Interlocked.Exchange(ref _bkCounts, 0) + Interlocked.Exchange(ref _defCounts, 0)) + " | wywolan " + Interlocked.Exchange(ref _calls, 0)
                     + " | zalogi AI " + aiMen + " ludzi" + Buildings338.Tail(Buildings338.GWage));
        }
    }
}
