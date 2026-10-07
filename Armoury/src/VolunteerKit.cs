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

        private static int _bought, _reverted, _gold, _pieces, _extraMissing, _dayStamp = -1;
        private static readonly Dictionary<Settlement, Settlement> _market = new Dictionary<Settlement, Settlement>();

        internal static void Reset() { SupplyDemand.ResetOrders(); _market.Clear(); _bought = _reverted = _gold = _pieces = _extraMissing = 0; _dayStamp = -1; }

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
                        else { _bought++; RecruitKit.OnUpgrade(n, x, y, new List<EquipmentElement>(_lastBought)); }
                    }
                    // wpis 92: ochotnik zniknal z puli bez awansu (gra go podmienila) - jego kupione rzeczy wracaja na targ
                    foreach (var g in gone) if (g != null && g.Tier >= 2) RecruitKit.OnVanished(n, g, market);
                }
            }
            catch (Exception e) { Log.Error("VolunteerKit", e); }
        }

        /// <summary>Targ osady: miasto - swoj; wies - miasto, do ktorego nalezy, inaczej najblizsze miasto; zamek - najblizsze miasto.
        /// Paczka 143: na tym samym targu wycenia sie konia rekruta i konia od hodowcy (Stables.MarketPrice) - notabl kupuje tu konia ochotnikowi.</summary>
        internal static Settlement MarketOf(Settlement st)
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
                bool ammo = it.ItemType == ItemObject.ItemTypeEnum.Arrows || it.ItemType == ItemObject.ItemTypeEnum.Bolts;
                if (ammo && !(Settings.Current.VolunteerKitKeyOnly && y.IsRanged)) continue;   // amunicja - zuzywalna, dokupuje pan (strzelec: 1 kolczan kluczowy, wpis 78)
                need.Add(it);
            }
            return need;
        }

        // wpis 76 (Jeff 05.10: "awans przy najwazniejszej rzeczy: notabl kupuje glowna bron i zbroje (albo luk u strzelca),
        // helm i tarcze dokupuje pozniej pan z zakupow AI ... zeby nie biegali boso"): o awansie decyduja czesci KLUCZOWE -
        // zbroja korpusu, glowna bron (luk/kusza u strzelca, inaczej pierwsza bron biala kompletu) i kon u jezdnego.
        // Reszta (helm, tarcza, buty, rekawice, plaszcz, rzad konski, bron zapasowa) - notabl dokupuje, jesli jest na targu
        // i starczy zlota; brak nie cofa awansu, idzie jako zamowienie dla warsztatow (wyglad zolnierza daje komplet szablonu).
        private static bool IsKey(CharacterObject y, ItemObject it, ref bool mainTaken, ref bool ammoTaken)
        {
            switch (it.ItemType)
            {
                case ItemObject.ItemTypeEnum.BodyArmor:
                case ItemObject.ItemTypeEnum.Horse:
                case ItemObject.ItemTypeEnum.HorseHarness: return true;   // wpis 78: jezdny - kon i rzad
                case ItemObject.ItemTypeEnum.Arrows:
                case ItemObject.ItemTypeEnum.Bolts:
                    if (y.IsRanged && !ammoTaken) { ammoTaken = true; return true; }   // wpis 78: strzelec - jeden kolczan
                    return false;
                case ItemObject.ItemTypeEnum.Bow:
                case ItemObject.ItemTypeEnum.Crossbow:
                    if (y.IsRanged && !mainTaken) { mainTaken = true; return true; }
                    return false;
                case ItemObject.ItemTypeEnum.OneHandedWeapon:
                case ItemObject.ItemTypeEnum.TwoHandedWeapon:
                case ItemObject.ItemTypeEnum.Polearm:
                    if (!y.IsRanged && !mainTaken) { mainTaken = true; return true; }
                    return false;
                default: return false;
            }
        }

        private static readonly List<EquipmentElement> _lastBought = new List<EquipmentElement>();   // wpis 92: dla kompletu rekruta

        private static bool Buy(Hero notable, Settlement market, CharacterObject x, CharacterObject y)
        {
            _lastBought.Clear();
            var all = Missing(x, y);
            if (all.Count == 0) return true;
            var need = all; var extra = new List<ItemObject>();
            if (Settings.Current.VolunteerKitKeyOnly)
            {
                need = new List<ItemObject>(); bool main = false, ammoT = false;
                foreach (var it in all) if (IsKey(y, it, ref main, ref ammoT)) need.Add(it); else extra.Add(it);
            }
            if (need.Count == 0 && extra.Count == 0) return true;
            if (market == null || market.Town == null || market.ItemRoster == null) return need.Count == 0;   // bez targu: tylko gdy nic kluczowego nie trzeba
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
                    if (ArmsPricing.IsUnique(cand)) continue;   // wpis 87 (audyt pkt 6): unikat nie znika w puli ochotnikow
                    if (cand.ItemType == ItemObject.ItemTypeEnum.Horse && cand.HorseComponent != null && cand.HorseComponent.IsPackAnimal) continue;
                    int used; taken.TryGetValue(i, out used);
                    if (el.Amount - used <= 0) continue;
                    int price;
                    try { price = market.Town.MarketData.GetPrice(el.EquipmentElement, null, false, market.Party); } catch { price = cand.Value; }
                    if (price < bestPrice) { bestPrice = price; best = i; }
                }
                if (best < 0) { SupplyDemand.NoteUnmetOnce(notable, market, it.ItemType, (int)it.Tier + 1, 1f); Why(it.ItemType + " t" + ((int)it.Tier + 1)); return false; }   // nie ma czego kupic - zamowienie (wpis 67)
                int u; taken.TryGetValue(best, out u); taken[best] = u + 1;
                picks.Add(roster.GetElementCopyAtIndex(best).EquipmentElement);
                total += bestPrice;
            }
            if (notable.Gold < total) { Why("zloto notabla (" + notable.Gold + " < " + total + ")"); return false; }   // nie stac go
            foreach (var e in picks) roster.AddToCounts(e, -1);
            _lastBought.AddRange(picks);
            if (total > 0) GiveGoldAction.ApplyForCharacterToSettlement(notable, market, total, true);
            _gold += total; _pieces += picks.Count;
            // dodatki: najtansze z targu, jesli sa i starczy zlota; brak nie cofa awansu
            foreach (var it in extra)
            {
                int best = -1, bestPrice = int.MaxValue;
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    var cand = el.EquipmentElement.Item;
                    if (cand == null || el.Amount <= 0 || cand.ItemType != it.ItemType || cand.Tier < it.Tier) continue;
                    if (ArmsPricing.IsUnique(cand)) continue;
                    if (cand.ItemType == ItemObject.ItemTypeEnum.Horse && cand.HorseComponent != null && cand.HorseComponent.IsPackAnimal) continue;
                    int price;
                    try { price = market.Town.MarketData.GetPrice(el.EquipmentElement, null, false, market.Party); } catch { price = cand.Value; }
                    if (price < bestPrice) { bestPrice = price; best = i; }
                }
                if (best < 0 || notable.Gold < bestPrice)
                {
                    _extraMissing++;
                    if (best < 0) SupplyDemand.NoteUnmetOnce(notable, market, it.ItemType, (int)it.Tier + 1, 1f);
                    WhyExtra(it.ItemType + " t" + ((int)it.Tier + 1));
                    continue;
                }
                var pe = roster.GetElementCopyAtIndex(best).EquipmentElement;
                roster.AddToCounts(pe, -1);
                _lastBought.Add(pe);
                GiveGoldAction.ApplyForCharacterToSettlement(notable, market, bestPrice, true);
                _gold += bestPrice; _pieces++;
            }
            return true;
        }

        // wpis 70: diagnoza cofnietych awansow - czego brakowalo
        private static readonly Dictionary<string, int> _why = new Dictionary<string, int>();
        private static readonly Dictionary<string, int> _whyExtra = new Dictionary<string, int>();
        private static void WhyExtra(string k) { int n; _whyExtra.TryGetValue(k, out n); _whyExtra[k] = n + 1; }
        private static void Why(string k) { if (k.StartsWith("zloto")) k = "zloto notabla"; int n; _why.TryGetValue(k, out n); _why[k] = n + 1; }

        private static void Flush()
        {
            if (_dayStamp < 0 || _bought + _reverted == 0) return;
            Log.Info("Ochotnicy: dzien " + _dayStamp + " - awanse z kupionym sprzetem " + _bought + " (" + _pieces + " szt. za " + _gold
                     + " zl z kiesy notabli do miast), cofniete (brak towaru albo zlota) " + _reverted + "; dodatkow nie dokupiono " + _extraMissing + ".");
            if (_whyExtra.Count > 0)
            {
                var l2 = new List<KeyValuePair<string, int>>(_whyExtra); l2.Sort((a, b) => b.Value.CompareTo(a.Value));
                var p2 = new List<string>(); for (int i = 0; i < l2.Count && i < 10; i++) p2.Add(l2[i].Key + " x" + l2[i].Value);
                Log.Info("Ochotnicy (diagnoza): dodatki bez zakupu (awans zostaje) - " + string.Join(", ", p2.ToArray()) + ".");
                _whyExtra.Clear();
            }
            if (_why.Count > 0)
            {
                var l = new List<KeyValuePair<string, int>>(_why); l.Sort((x, y) => y.Value.CompareTo(x.Value));
                var parts = new List<string>(); for (int i = 0; i < l.Count && i < 10; i++) parts.Add(l[i].Key + " x" + l[i].Value);
                Log.Info("Ochotnicy (diagnoza): powody cofniec - " + string.Join(", ", parts.ToArray()) + ".");
                _why.Clear();
            }
            _bought = _reverted = _gold = _pieces = _extraMissing = 0;
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
