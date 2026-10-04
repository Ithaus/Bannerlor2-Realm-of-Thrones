using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// POBOR OD LUDNOSCI - krok 2: WYZSZY TIER = KTOS, KTO KUPIL SPRZET (Jeff 04.10: "a co, jak sa ludzie
    /// wyzszego tieru, skad maja sprzet - przeciez jest zycie swiata, sa inni zbrojni nie w sluzbie,
    /// co moga kupic sprzet i miec swoj"). docs/PLAN-POBOR.md pkt 2.3.
    ///
    /// Vanilla co dzien awansuje ochotnika w puli notabla (UpdateVolunteersOfNotablesInSettlement:
    /// log2(moc/tier) x 1%, do tieru 4) - za darmo, sprzet z niczego (DTE potem wklada do zbrojowni
    /// werbujacego caly komplet szablonu).
    ///
    /// Teraz: prefix zapamietuje pule kazdego notabla, postfix znajduje awanse (X -> Y, Y z celow
    /// awansu X). Dla kazdego: czesci kompletu Y, ktorych X nie mial (zbroja, bron, kon) - notabl kupuje
    /// je na targu miasta regionu (ten sam typ, tier nie nizszy, najtansze), placac miastu ze swojego
    /// zlota; towar schodzi z polki. Brak towaru albo zlota = awans cofniety (zostaje X).
    /// Swiezy ochotnik tieru 1 przychodzi z wlasnym dobytkiem (odzienie, narzedzie) - bez zakupu.
    /// Komplet DTE przy werbunku to wtedy ten zakupiony sprzet (przybliznie - DTE losuje rownowazne sztuki).
    /// </summary>
    internal static class VolunteerKit
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.VolunteerKitEnabled; } }

        private static int _bought, _reverted, _gold, _pieces, _dayStamp = -1;
        private static readonly Dictionary<Settlement, Settlement> _market = new Dictionary<Settlement, Settlement>();

        internal static void Reset() { _market.Clear(); _bought = _reverted = _gold = _pieces = 0; _dayStamp = -1; }

        public static void Prefix(Settlement settlement, out Dictionary<Hero, CharacterObject[]> __state)
        {
            __state = null;
            try
            {
                if (!On || settlement == null) return;
                __state = new Dictionary<Hero, CharacterObject[]>();
                foreach (var n in settlement.Notables)
                    if (n != null && n.VolunteerTypes != null) __state[n] = (CharacterObject[])n.VolunteerTypes.Clone();
            }
            catch { __state = null; }
        }

        public static void Postfix(Settlement settlement, Dictionary<Hero, CharacterObject[]> __state)
        {
            if (__state == null) return;
            try
            {
                int day = (int)CampaignTime.Now.ToDays;
                if (_dayStamp != day) { Flush(); _dayStamp = day; }
                var market = MarketOf(settlement);
                foreach (var kv in __state)
                {
                    var n = kv.Key;
                    var after = n.VolunteerTypes;
                    if (after == null) continue;
                    // roznica multizbiorow: co zniknelo (before) i co przybylo (after)
                    var gone = kv.Value.Where(c => c != null).ToList();
                    var came = new List<int>();
                    for (int i = 0; i < after.Length; i++)
                    {
                        var c = after[i];
                        if (c == null) continue;
                        int j = gone.IndexOf(c);
                        if (j >= 0) gone.RemoveAt(j); else came.Add(i);
                    }
                    foreach (int i in came)
                    {
                        var y = after[i];
                        var x = gone.FirstOrDefault(g => g.UpgradeTargets != null && g.UpgradeTargets.Contains(y));
                        if (x == null) continue;                          // nowy ochotnik (tier 1) - wlasny dobytek
                        gone.Remove(x);
                        if (!Buy(n, market, x, y)) { after[i] = x; _reverted++; }
                        else _bought++;
                    }
                }
            }
            catch (Exception e) { Log.Error("VolunteerKit", e); }
        }

        private static Settlement MarketOf(Settlement st)
        {
            if (st == null) return null;
            if (st.IsTown) return st;
            Settlement m;
            if (_market.TryGetValue(st, out m)) return m;
            var b = st.IsVillage && st.Village != null ? st.Village.Bound : st;
            if (b != null && b.IsTown) m = b;
            else
            {
                var p = (b ?? st).GetPosition2D;
                float best = float.MaxValue;
                foreach (var t in Settlement.All)
                {
                    if (t == null || !t.IsTown) continue;
                    float d = p.DistanceSquared(t.GetPosition2D);
                    if (d < best) { best = d; m = t; }
                }
            }
            _market[st] = m;
            return m;
        }

        /// <summary>Czesci kompletu Y, ktorych X nie nosil.</summary>
        private static List<ItemObject> Missing(CharacterObject x, CharacterObject y)
        {
            var have = new HashSet<ItemObject>();
            var eqx = x.FirstBattleEquipment;
            for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++) { var it = eqx[i].Item; if (it != null) have.Add(it); }
            var need = new List<ItemObject>();
            var eqy = y.FirstBattleEquipment;
            for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
            {
                var it = eqy[i].Item;
                if (it == null || have.Contains(it)) continue;
                if (it.ItemType == ItemObject.ItemTypeEnum.Arrows || it.ItemType == ItemObject.ItemTypeEnum.Bolts) continue;   // amunicja - zuzywalna, dokupuje pan
                need.Add(it);
            }
            return need;
        }

        private static bool Buy(Hero notable, Settlement market, CharacterObject x, CharacterObject y)
        {
            var need = Missing(x, y);
            if (need.Count == 0) return true;
            if (market == null || market.Town == null || market.ItemRoster == null) return false;
            var roster = market.ItemRoster;
            var picks = new List<EquipmentElement>();
            var taken = new Dictionary<int, int>();
            int total = 0;
            foreach (var it in need)
            {
                int best = -1, bestPrice = int.MaxValue;
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    var cand = el.EquipmentElement.Item;
                    if (cand == null || cand.ItemType != it.ItemType || cand.Tier < it.Tier) continue;
                    if (cand.ItemType == ItemObject.ItemTypeEnum.Horse && cand.HorseComponent != null && cand.HorseComponent.IsPackAnimal) continue;
                    int used; taken.TryGetValue(i, out used);
                    if (el.Amount - used <= 0) continue;
                    int price;
                    try { price = market.Town.MarketData.GetPrice(el.EquipmentElement, null, false, market.Party); } catch { price = cand.Value; }
                    if (price < bestPrice) { bestPrice = price; best = i; }
                }
                if (best < 0) return false;                               // nie ma czego kupic
                int u; taken.TryGetValue(best, out u); taken[best] = u + 1;
                picks.Add(roster.GetElementCopyAtIndex(best).EquipmentElement);
                total += bestPrice;
            }
            if (notable.Gold < total) return false;                       // nie stac go
            foreach (var e in picks) roster.AddToCounts(e, -1);
            GiveGoldAction.ApplyForCharacterToSettlement(notable, market, total, true);
            _gold += total; _pieces += picks.Count;
            return true;
        }

        private static void Flush()
        {
            if (_dayStamp < 0 || _bought + _reverted == 0) return;
            Log.Info("Ochotnicy: dzien " + _dayStamp + " - awanse z kupionym sprzetem " + _bought + " (" + _pieces + " szt. za " + _gold
                     + " zl z kiesy notabli do miast), cofniete (brak towaru albo zlota) " + _reverted + ".");
            _bought = _reverted = _gold = _pieces = 0;
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var m = AccessTools.Method(typeof(RecruitmentCampaignBehavior), "UpdateVolunteersOfNotablesInSettlement");
                if (m != null) h.Patch(m, prefix: new HarmonyMethod(typeof(VolunteerKit), nameof(Prefix)), postfix: new HarmonyMethod(typeof(VolunteerKit), nameof(Postfix)));
                Log.Info("VolunteerKit: awans ochotnika tylko z kupionym sprzetem " + (m != null ? "wpiety" : "BRAK UpdateVolunteersOfNotablesInSettlement") + ".");
            }
            catch (Exception e) { Log.Error("VolunteerKit.ApplyAll", e); }
        }
    }
}
