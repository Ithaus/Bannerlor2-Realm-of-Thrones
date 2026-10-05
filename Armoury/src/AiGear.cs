using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// ZAKUPY ARMII AI (Jeff 04.10: "nie dostaja nic za darmo"; "popyt wzrasta, bo armia bedzie
    /// potrzebowala kupic, aby wyposazyc wojsko - lordowie AI, gracz - realnie, nie sztucznie";
    /// "lord mowi: piekna zbroja, ale biore te tansza, chroni minimalnie gorzej, a jest o polowe
    /// tansza - nie potrzebuje zbroi dla krola"). docs/MODEL-MATERIALOW.md, krok 6.
    ///
    /// Dynamic Troop Equipment (dekompilacja 1.4.7.2, EveryoneCampaignBehavior) dawal AI za darmo:
    ///  - codziennie AllocateRandomEquipmentToPartyArmory (sztuki ze wzorcow, z lenn, od rodu) -> WYLACZONE;
    ///  - co tydzien MoveRosterToArmory: bron/zbroje z taboru do zbrojowni I PELNA Value w zlocie
    ///    dla lorda (drukarka pieniedzy) -> WYLACZONE; sztuki zostaja w taborze i lord sprzedaje je
    ///    w miescie zwyklym PartiesSellLoot;
    ///  - komplet przy werbunku (OnTroopRecruited) -> ZOSTAJE na przelaczniku AiRecruitsBringKit
    ///    (pobor przychodzi z wlasnym sprzetem).
    /// W zamian lord AI w miescie/zamku (wejscie + raz dziennie, gdy stoi) liczy braki zbrojowni
    /// wzgledem wzorcow swoich ludzi (koszyki typ x tier, bez koni - te ma Stajnia) i KUPUJE z polki
    /// po cenie rynku (prawo podazy i popytu + wycena), placac miastu ze swojego zlota, najwyzej
    /// AiGearBudgetPercent zlota na wizyte, zostawiajac AiGearGoldReserve. Wybor: tier ten sam albo
    /// o jeden nizszy, najlepszy stosunek skutecznosci (Effectiveness) do ceny; unikaty pomija
    /// (prestiz to nie ochrona). Kolejnosc: korpus, bron, tarcza, helm, amunicja, nogi, rece, plaszcz.
    /// </summary>
    internal static class AiGear
    {
        internal static void Reset() { _lastDay.Clear(); }
        private static Type _dte;
        private static FieldInfo _armories;
        private static MethodInfo _add;
        private static bool _looked;
        private static readonly Dictionary<MobileParty, int> _lastDay = new Dictionary<MobileParty, int>();
        private static int _dayPieces, _dayGold, _dayVisits, _dayLogged, _dayStamp = -1;

        internal static bool On { get { var s = Settings.Current; return s != null && s.AiBuysGear; } }

        private static bool Look()
        {
            if (_looked) return _dte != null && _armories != null && _add != null;
            _looked = true;
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = asm.GetType("DynamicTroopEquipmentReupload.EveryoneCampaignBehavior");
                    if (t != null) { _dte = t; break; }
                }
                if (_dte != null)
                {
                    _armories = _dte.GetField("PartyArmories", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    _add = _dte.GetMethod("AddItemToPartyArmory", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                }
            }
            catch { }
            return _dte != null && _armories != null && _add != null;
        }

        // ------------------------------------------------------------ latki DTE
        public static bool SkipWhenBuying() { return !On; }

        public static bool RecruitKitPrefix(Hero recruiterHero)
        {
            var s = Settings.Current;
            if (s == null || s.AiRecruitsBringKit) return true;
            if (recruiterHero == null || recruiterHero == Hero.MainHero) return true;
            return false;
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                if (!Look()) { Log.Info("AiGear: Dynamic Troop Equipment nie znaleziony - zakupy AI wylaczone."); return; }
                var skip = new HarmonyMethod(typeof(AiGear), nameof(SkipWhenBuying));
                var a = AccessTools.Method(_dte, "AllocateRandomEquipmentToPartyArmory");
                var m = AccessTools.Method(_dte, "MoveRosterToArmory");
                var r = AccessTools.Method(_dte, "OnTroopRecruited");
                if (a != null) h.Patch(a, prefix: skip);
                if (m != null) h.Patch(m, prefix: skip);
                if (r != null) h.Patch(r, prefix: new HarmonyMethod(typeof(AiGear), nameof(RecruitKitPrefix)));
                Log.Info("AiGear: latki DTE - darmowy przydzial " + (a != null ? "wpiety" : "BRAK") + ", doplata z taboru "
                         + (m != null ? "wpieta" : "BRAK") + ", komplet przy werbunku " + (r != null ? "wpiety" : "BRAK")
                         + "; zakupy AI " + (On ? "CZYNNE" : "wylaczone w MCM") + ".");
            }
            catch (Exception e) { Log.Error("AiGear.ApplyAll", e); }
        }

        // ------------------------------------------------------------ zakupy
        internal static void OnSettlementEntered(MobileParty mp, Settlement st, Hero hero) { TryBuy(mp, st); }

        internal static void OnDailyTickParty(MobileParty mp)
        {
            try { if (mp != null && mp.CurrentSettlement != null) TryBuy(mp, mp.CurrentSettlement); } catch { }
        }

        private static readonly ItemObject.ItemTypeEnum[] Order =
        {
            ItemObject.ItemTypeEnum.BodyArmor, ItemObject.ItemTypeEnum.OneHandedWeapon, ItemObject.ItemTypeEnum.TwoHandedWeapon,
            ItemObject.ItemTypeEnum.Polearm, ItemObject.ItemTypeEnum.Bow, ItemObject.ItemTypeEnum.Crossbow,
            ItemObject.ItemTypeEnum.Shield, ItemObject.ItemTypeEnum.HeadArmor, ItemObject.ItemTypeEnum.Arrows,
            ItemObject.ItemTypeEnum.Bolts, ItemObject.ItemTypeEnum.Thrown, ItemObject.ItemTypeEnum.LegArmor,
            ItemObject.ItemTypeEnum.HandArmor, ItemObject.ItemTypeEnum.Cape
        };

        // wpis 79: dla ColdStart (dorobek stuleci w zbrojowniach)
        internal static Dictionary<MBGUID, Dictionary<ItemObject, int>> Armories() { return Look() ? _armories.GetValue(null) as Dictionary<MBGUID, Dictionary<ItemObject, int>> : null; }
        internal static int Bucket(ItemObject it) { return (int)it.ItemType * 10 + TierOf(it); }
        internal static bool AddToArmory(MobileParty mp, ItemObject it, int n)
        {
            try { if (!Look() || n <= 0) return false; _add.Invoke(null, new object[] { mp.Id, it, n }); return true; } catch { return false; }
        }

        /// <summary>wpis 84: potrzeby partii w koszykach typ x tier (komplety ludzi, bez koni).</summary>
        internal static Dictionary<int, int> NeedBuckets(MobileParty mp)
        {
            var need = new Dictionary<int, int>();
            var roster = mp.MemberRoster;
            for (int i = 0; i < roster.Count; i++)
            {
                var el = roster.GetElementCopyAtIndex(i);
                var ch = el.Character;
                if (ch == null || ch.IsHero || el.Number <= 0) continue;
                Equipment eq = null;
                try { eq = ch.Equipment; } catch { }
                if (eq == null) continue;
                for (int sl = 0; sl < 10; sl++)
                {
                    var it = eq[(EquipmentIndex)sl].Item;
                    if (it == null || !SupplyDemand.Equipmentish(it)) continue;
                    int k = Bucket(it); int n; need.TryGetValue(k, out n); need[k] = n + el.Number;
                }
            }
            return need;
        }

        private static int TierOf(ItemObject it)
        {
            try { return Math.Max(1, Math.Min(6, (int)it.Tier + 1)); } catch { return 1; }
        }

        private static void TryBuy(MobileParty mp, Settlement st)
        {
            try
            {
                if (!On || mp == null || st == null || !Look()) return;
                // wpis 57 (Jeff 04.10: "a co z garnizonem, skad oni maja miec bron?"): garnizon kupuje brakujacy sprzet na targu
                // SWOJEJ osady, placi pan osady ze swojej kiesy, zloto idzie do kasy miasta (jak u partii lorda)
                var s0 = Settings.Current;
                bool garrison = mp.IsGarrison && mp.CurrentSettlement == st && s0.GarrisonBuysGear;
                Hero payer = garrison ? (st.OwnerClan != null ? st.OwnerClan.Leader : null) : mp.LeaderHero;
                if (garrison && payer == Hero.MainHero && !s0.GarrisonBuysGearPlayer) return;
                if (mp.IsMainParty || (!mp.IsLordParty && !garrison) || payer == null || !payer.IsAlive || !mp.IsActive || mp.MapEvent != null) return;
                if ((!st.IsTown && !st.IsCastle) || st.ItemRoster == null || st.Town == null) return;
                if (FactionManager.IsAtWarAgainstFaction(mp.MapFaction, st.MapFaction)) return;
                int day = (int)CampaignTime.Now.ToDays;
                int last;
                if (_lastDay.TryGetValue(mp, out last) && last == day) return;
                _lastDay[mp] = day;
                if (_dayStamp != day) { FlushDay(); _dayStamp = day; }

                var s = Settings.Current;
                var lord = payer;
                int reserve = Math.Max(0, s.AiGearGoldReserve);
                int budget = (int)((lord.Gold - reserve) * Math.Max(0f, Math.Min(100f, s.AiGearBudgetPercent)) / 100f);
                // wpis 84: ludzie dokupuja braki ze swojej sakiewki (lup), dopiero potem kiesa lorda
                int purse = garrison ? 0 : MenPurse.Get(mp);
                budget = Math.Max(0, budget) + purse;
                if (budget <= 0) return;

                var all = _armories.GetValue(null) as Dictionary<MBGUID, Dictionary<ItemObject, int>>;
                Dictionary<ItemObject, int> armory = null;
                if (all != null) all.TryGetValue(mp.Id, out armory);

                // potrzeby wedle wzorcow (bez koni) i stan zbrojowni - koszyki typ*10+tier
                var need = new Dictionary<int, int>();
                var roster = mp.MemberRoster;
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    var ch = el.Character;
                    if (ch == null || ch.IsHero || el.Number <= 0) continue;
                    Equipment eq = null;
                    try { eq = ch.Equipment; } catch { }
                    if (eq == null) continue;
                    for (int sl = 0; sl < 10; sl++)
                    {
                        var it = eq[(EquipmentIndex)sl].Item;
                        if (it == null || !SupplyDemand.Equipmentish(it)) continue;
                        int k = (int)it.ItemType * 10 + TierOf(it);
                        int n; need.TryGetValue(k, out n); need[k] = n + el.Number;
                    }
                }
                if (armory != null)
                    foreach (var kv in armory)
                    {
                        if (kv.Key == null || kv.Value <= 0) continue;
                        int k = (int)kv.Key.ItemType * 10 + TierOf(kv.Key);
                        int n; if (need.TryGetValue(k, out n)) need[k] = n - kv.Value;
                    }

                int spent = 0, pieces = 0;
                int maxPieces = Math.Max(1, s.AiGearMaxPiecesPerVisit);
                var bought = new List<string>();
                var shelf = st.ItemRoster;
                foreach (var type in Order)
                {
                    for (int t = 6; t >= 1; t--)
                    {
                        int k = (int)type * 10 + t;
                        int deficit;
                        if (!need.TryGetValue(k, out deficit) || deficit <= 0) continue;
                        // kandydaci: ten sam typ, tier t albo t-1, bez unikatow; najlepsza skutecznosc do ceny
                        while (deficit > 0 && pieces < maxPieces && spent < budget)
                        {
                            int bestI = -1; float bestScore = 0f; int bestPrice = 0;
                            for (int i = 0; i < shelf.Count; i++)
                            {
                                var el = shelf.GetElementCopyAtIndex(i);
                                var it = el.EquipmentElement.Item;
                                if (el.Amount <= 0 || it == null || it.ItemType != type) continue;
                                int ti = TierOf(it);
                                if (ti != t && ti != t - 1) continue;
                                if (ArmsPricing.IsUnique(it)) continue;
                                int price = st.Town.MarketData.GetPrice(el.EquipmentElement, mp, false, st.Party);
                                if (price <= 0 || price > budget - spent) continue;
                                float eff = it.Effectiveness > 0f ? it.Effectiveness : 1f;
                                float score = eff / price;
                                if (score > bestScore) { bestScore = score; bestI = i; bestPrice = price; }
                            }
                            if (bestI < 0) break;
                            var pick = shelf.GetElementCopyAtIndex(bestI);
                            int n = Math.Min(deficit, pick.Amount);
                            n = Math.Min(n, maxPieces - pieces);
                            n = Math.Min(n, (budget - spent) / bestPrice);
                            if (n <= 0) break;
                            shelf.AddToCounts(pick.EquipmentElement, -n);
                            _add.Invoke(null, new object[] { mp.Id, pick.EquipmentElement.Item, n });
                            int cost = bestPrice * n, fromPurse = garrison ? 0 : MenPurse.Take(mp, cost);
                            lord.ChangeHeroGold(-(cost - fromPurse));
                            st.Town.ChangeGold(bestPrice * n);
                            spent += bestPrice * n; pieces += n; deficit -= n;
                            if (bought.Count < 6) bought.Add(pick.EquipmentElement.Item.StringId + " " + bestPrice);
                        }
                        need[k] = deficit;   // wpis 67: co zostalo niezaspokojone
                    }
                }
                // wpis 67: czego nie bylo na polce - zamowienie w tym miescie (najwyzej po 10 na rodzaj z jednej wizyty)
                // wpis 81: tylko gdy na polce NIE MA zadnej sztuki tego typu i tieru (t albo t-1) - "za drogie" to nie brak
                // towaru, tylko brak zlota kupca; i jeden kupiec raz na SupplyDemandOrderRepeatDays (garnizon liczy co dzien)
                foreach (var kv in need)
                {
                    if (kv.Value <= 0) continue;
                    var ty = (ItemObject.ItemTypeEnum)(kv.Key / 10); int tr = kv.Key % 10; bool onShelf = false;
                    for (int i = 0; i < shelf.Count && !onShelf; i++)
                    {
                        var el = shelf.GetElementCopyAtIndex(i); var it = el.EquipmentElement.Item;
                        if (el.Amount > 0 && it != null && it.ItemType == ty && !ArmsPricing.IsUnique(it)) { int ti = TierOf(it); onShelf = ti == tr || ti == tr - 1; }
                    }
                    if (!onShelf) SupplyDemand.NoteUnmetOnce(mp, st, ty, tr, Math.Min(10, kv.Value));
                }
                if (pieces <= 0) return;
                _dayPieces += pieces; _dayGold += spent; _dayVisits++; if (garrison) { _dayGarrison++; _dayGarrisonGold += spent; }
                if (_dayLogged < Math.Max(0, s.AiGearLogPerDay))
                {
                    _dayLogged++;
                    Log.Info("ZakupyAI: " + (garrison ? "garnizon " + st.Name + " (placi " + lord.Name + ")" : lord.Name.ToString()) + " (" + mp.MemberRoster.TotalManCount + " ludzi) w " + st.Name + ": " + pieces
                             + " szt. za " + spent + " (budzet " + budget + ", zloto " + (lord.Gold + spent) + " -> " + lord.Gold + "); np. "
                             + string.Join(", ", bought.ToArray()) + ".");
                }
            }
            catch (Exception e) { Log.Error("AiGear.TryBuy", e); }
        }

        private static int _dayGarrison, _dayGarrisonGold;

        private static void FlushDay()
        {
            if (_dayStamp >= 0 && (_dayVisits > 0))
                Log.Info("ZakupyAI: dzien " + _dayStamp + " - " + _dayVisits + " wizyt, " + _dayPieces + " szt. kupionych za " + _dayGold + " zlota; w tym garnizony " + _dayGarrison + " zakupow za " + _dayGarrisonGold + ".");
            _dayPieces = 0; _dayGold = 0; _dayVisits = 0; _dayLogged = 0; _dayGarrison = 0; _dayGarrisonGold = 0;
        }
    }
}
