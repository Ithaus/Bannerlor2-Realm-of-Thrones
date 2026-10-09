using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 171 ZBROJOWNIE ZALOG (docs/paczki/171-zbrojenie-zalog.md, C9, C9a, A7, C10; Jeff 08.10: "jak znika to zamykamy").
    ///  - C9 (Z7): Dynamic Troop Equipment NIE zapisuje zbrojowni garnizonow (ShouldPersistParty: tylko partie z wodzem-bohaterem) - po kazdym
    ///    wczytaniu zalogi staly puste, a panowie kupowali wszystko od nowa (log 10-54-00: 490-870 tys. zl dziennie przez 12 dob, ok. 7.8 mln,
    ///    kapital Banku 700 -> 70 tys.). Armoury zapisuje zbrojownie wszystkich zalog (takze gracza) i po wczytaniu ustawia je z zapisu.
    ///  - C9a: pierwsze wczytanie zapisu sprzed 171 (brak klucza) - zalogi AI raz uzupelnione do wzorca swoich ludzi (ColdStart.FillToTemplate):
    ///    to naprawa skutku bledu DTE (ten sprzet byl oplacony i lezal w zalogach przed zapisem), nie dosypka; zalogi gracza bez zmian.
    ///  - A7 (Z2): lord zostawia ludzi w zalodze albo ich zabiera, rozwiazana partia wchodzi do zalogi - sprzet idzie z ludzmi miedzy zbrojowniami
    ///    DTE (gra przenosi samych ludzi); ludzie rozwiazanej partii, ktorzy odchodza, biora swoje komplety, a tabor ponad nie sprzedaje sie dla rodu.
    ///  - C10 (Z9): raz w tygodniu zaloga AI sprzedaje to, co ma ponad potrzebe swoich ludzi (i ludzi na patrolach BK) oraz zapas, na polke
    ///    wlasnej osady po cenie skupu; zloto dostaje pan (ta sama regula co nadwyzki partii lorda - MenPurse).
    /// Flagi wpiecia latek na caly proces (Reset jest raz na kampanie).
    /// </summary>
    internal static class GarrisonArmory
    {
        private static Harmony _h;
        private static bool _applied, _leaveHooked, _takeHooked, _disbandHooked;
        private static Type _bkPatrolType;
        private static bool _loaded, _restored;
        private static string _pending;
        // liczniki doby (linia "Zbrojownie zalog (171): dzien")
        private static int _dLeftMen, _dLeftPcs, _dTakenMen, _dTakenPcs, _dDisbandIn, _dDisbandInPcs, _dDisbandGone, _dDisbandGoneKit, _dDisbandSold, _dDisbandGold;
        private static int _dQueue, _dSoldGarrisons, _dSoldPcs, _dSoldGold, _dPatrolCounted, _stumbles, _errDay = -1;
        private static readonly HashSet<string> _errWhere = new HashSet<string>();

        internal static void Reset()
        {
            _loaded = false; _restored = false; _pending = null;
            ClearDay(); _stumbles = 0; _errDay = -1; _errWhere.Clear();
        }

        private static void ClearDay()
        {
            _dLeftMen = _dLeftPcs = _dTakenMen = _dTakenPcs = _dDisbandIn = _dDisbandInPcs = _dDisbandGone = _dDisbandGoneKit = _dDisbandSold = _dDisbandGold = 0;
            _dQueue = _dSoldGarrisons = _dSoldPcs = _dSoldGold = _dPatrolCounted = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try
            {
                int d = (int)CampaignTime.Now.ToDays;
                if (d != _errDay) { _errDay = d; _errWhere.Clear(); }
                if (_errWhere.Add(where)) Log.Error("GarrisonArmory." + where, e);
            }
            catch { }
        }

        private static bool KitMoves { get { var s = Settings.Current; return s != null && s.KitMovesWithMen && AiGear.On && AiGear.Armories() != null; } }

        private static int TierOf(ItemObject it) { try { return Math.Max(1, Math.Min(6, (int)it.Tier + 1)); } catch { return 1; } }

        private static Dictionary<ItemObject, int> ArmoryOf(MobileParty mp)
        {
            var d = AiGear.Armories(); Dictionary<ItemObject, int> a;
            return d != null && mp != null && d.TryGetValue(mp.Id, out a) ? a : null;
        }

        // ------------------------------------------------------------ potrzeby po typach (sl. 0-11: z koniem i rzedem - kon jest wlasnoscia zolnierza, 160)
        private static void AddNeed(Dictionary<int, int> need, CharacterObject ch, int n)
        {
            if (ch == null || ch.IsHero || n <= 0) return;
            Equipment eq = null; try { eq = ch.Equipment; } catch { }
            if (eq == null) return;
            for (int sl = 0; sl < (int)EquipmentIndex.NumEquipmentSetSlots; sl++)
            {
                var it = eq[(EquipmentIndex)sl].Item;
                if (it == null || !SupplyDemand.Equipmentish(it)) continue;
                int k = (int)it.ItemType; int v; need.TryGetValue(k, out v); need[k] = v + n;
            }
        }

        /// <summary>Typ (ItemType) -> sztuk wzorcow ludzi partii (bez bohaterow).</summary>
        internal static Dictionary<int, int> NeedByType(MobileParty mp)
        {
            var need = new Dictionary<int, int>();
            var roster = mp != null ? mp.MemberRoster : null;
            if (roster == null) return need;
            for (int i = 0; i < roster.Count; i++) { var el = roster.GetElementCopyAtIndex(i); AddNeed(need, el.Character, el.Number); }
            return need;
        }

        private static Dictionary<CharacterObject, int> Snapshot(TroopRoster roster)
        {
            var d = new Dictionary<CharacterObject, int>();
            if (roster == null) return d;
            for (int i = 0; i < roster.Count; i++)
            {
                var el = roster.GetElementCopyAtIndex(i);
                if (el.Character == null || el.Character.IsHero || el.Number <= 0) continue;
                int v; d.TryGetValue(el.Character, out v); d[el.Character] = v + el.Number;
            }
            return d;
        }

        private static Dictionary<CharacterObject, int> Gone(Dictionary<CharacterObject, int> before, TroopRoster after)
        {
            var now = Snapshot(after);
            var moved = new Dictionary<CharacterObject, int>();
            foreach (var kv in before) { int a; now.TryGetValue(kv.Key, out a); if (kv.Value > a) moved[kv.Key] = kv.Value - a; }
            return moved;
        }

        /// <summary>
        /// A7: sprzet przeniesionych ludzi - najpierw ile sztuk kazdego TYPU im sie nalezy (udzial proporcjonalny, gdy zbrojownia ma braki - nie wiemy,
        /// ktorym ludziom brakuje), potem ktore sztuki (od najwyzszego tieru: przedmiot ze wzorca, ten sam tier, wyzsze rosnaco, nizsze malejaco).
        /// Zapas ponad komplety zostaje u dawcy (lord sprzeda w miescie, zaloga w C10).
        /// </summary>
        internal static int MoveKits(MobileParty from, MobileParty to, Dictionary<CharacterObject, int> moved, string why)
        {
            if (from == null || to == null || moved == null || moved.Count == 0) return 0;
            var arm = ArmoryOf(from);
            if (arm == null || arm.Count == 0) return 0;
            var needMoved = new Dictionary<int, int>();
            foreach (var kv in moved) AddNeed(needMoved, kv.Key, kv.Value);
            var needLeft = NeedByType(from);   // roster dawcy PO przeniesieniu
            var have = new Dictionary<int, int>();
            var byType = new Dictionary<int, List<ItemObject>>();
            var avail = new Dictionary<ItemObject, int>();
            foreach (var kv in arm)
            {
                if (kv.Key == null || kv.Value <= 0 || !SupplyDemand.Equipmentish(kv.Key) || ArmsPricing.IsUnique(kv.Key)) continue;
                int t = (int)kv.Key.ItemType; int v; have.TryGetValue(t, out v); have[t] = v + kv.Value;
                List<ItemObject> l; if (!byType.TryGetValue(t, out l)) byType[t] = l = new List<ItemObject>(); l.Add(kv.Key);
                avail[kv.Key] = kv.Value;
            }
            // co ludzie nosza: typ -> (przedmiot wzorca, ilu ludzi), od najwyzszego tieru
            var wanted = new Dictionary<int, List<KeyValuePair<ItemObject, int>>>();
            foreach (var kv in moved)
            {
                Equipment eq = null; try { eq = kv.Key != null ? kv.Key.Equipment : null; } catch { }
                if (eq == null || kv.Value <= 0) continue;
                for (int sl = 0; sl < (int)EquipmentIndex.NumEquipmentSetSlots; sl++)
                {
                    var it = eq[(EquipmentIndex)sl].Item;
                    if (it == null || !SupplyDemand.Equipmentish(it)) continue;
                    int t = (int)it.ItemType;
                    List<KeyValuePair<ItemObject, int>> l; if (!wanted.TryGetValue(t, out l)) wanted[t] = l = new List<KeyValuePair<ItemObject, int>>();
                    l.Add(new KeyValuePair<ItemObject, int>(it, kv.Value));
                }
            }
            var taken = new Dictionary<ItemObject, int>();
            foreach (var nk in needMoved)
            {
                int type = nk.Key, nm = nk.Value, h, nl;
                if (nm <= 0 || !have.TryGetValue(type, out h) || h <= 0) continue;
                needLeft.TryGetValue(type, out nl);
                int share = h >= nm + nl ? nm : (int)Math.Round(h * (double)nm / Math.Max(1, nm + nl));
                share = Math.Max(0, Math.Min(share, Math.Min(nm, h)));
                if (share <= 0) continue;
                List<KeyValuePair<ItemObject, int>> w; List<ItemObject> cands;
                if (!wanted.TryGetValue(type, out w) || !byType.TryGetValue(type, out cands)) continue;
                w.Sort((a, b) => TierOf(b.Key).CompareTo(TierOf(a.Key)));
                foreach (var e in w)
                {
                    int want = e.Value;
                    while (want > 0 && share > 0)
                    {
                        var pick = Pick(avail, cands, e.Key);
                        if (pick == null) break;
                        int c = Math.Min(Math.Min(want, share), avail[pick]);
                        avail[pick] -= c; want -= c; share -= c;
                        int tv; taken.TryGetValue(pick, out tv); taken[pick] = tv + c;
                    }
                    if (share <= 0) break;
                }
            }
            int pcs = 0;
            foreach (var kv in taken)
            {
                if (kv.Value <= 0 || !AiGear.AddToArmory(to, kv.Key, kv.Value)) continue;
                int c; arm.TryGetValue(kv.Key, out c); c -= kv.Value;
                if (c > 0) arm[kv.Key] = c; else arm.Remove(kv.Key);
                if (to.IsLordParty) AiWear.NoteSound(to, kv.Key, kv.Value);   // sztuki z zalogi wchodza jako sprawne (zalogi nie maja zapisu zuzycia)
                pcs += kv.Value;
            }
            return pcs;
        }

        /// <summary>Sztuka dla przedmiotu wzorca: dokladnie ten, potem ten sam tier, wyzsze rosnaco, nizsze malejaco.</summary>
        private static ItemObject Pick(Dictionary<ItemObject, int> avail, List<ItemObject> cands, ItemObject want)
        {
            int v;
            if (want != null && avail.TryGetValue(want, out v) && v > 0) return want;
            int t = TierOf(want);
            for (int pass = 0; pass < 3; pass++)
            {
                ItemObject best = null; int bt = pass == 2 ? 0 : 99;
                foreach (var it in cands)
                {
                    if (!avail.TryGetValue(it, out v) || v <= 0) continue;
                    int ti = TierOf(it);
                    if (pass == 0) { if (ti == t) return it; }
                    else if (pass == 1) { if (ti > t && ti < bt) { bt = ti; best = it; } }
                    else { if (ti < t && ti > bt) { bt = ti; best = it; } }
                }
                if (best != null) return best;
            }
            return null;
        }

        /// <summary>A7: cala zbrojownia from -> to (rozwiazana partia: jej tabor zostaje w twierdzy, nadwyzke zaloga sprzeda w C10).</summary>
        internal static int MoveAll(MobileParty from, MobileParty to, string why)
        {
            var arm = ArmoryOf(from);
            if (arm == null || arm.Count == 0 || to == null) return 0;
            int pcs = 0;
            foreach (var kv in arm.ToList())
            {
                if (kv.Key == null || kv.Value <= 0) continue;
                if (AiGear.AddToArmory(to, kv.Key, kv.Value)) { pcs += kv.Value; arm.Remove(kv.Key); }
            }
            arm.Clear();
            return pcs;
        }

        /// <summary>
        /// Nadwyzka ponad potrzebe (po typach) i zapas keepPercent na polke targu po cenie skupu, najwyzej tyle, ile kasa udzwignie; zloto do payee.
        /// Najgorsze sztuki najpierw (tier, potem wartosc), bez koni i rzedow (Stajnia) i bez unikatow. Partia lorda (rozwiazana) - sztuka w stanie
        /// z ksiegi AiWear; zaloga - sprawna (zalogi nie maja zapisu zuzycia, jak przy zakupach).
        /// </summary>
        internal static int SellSurplus(MobileParty mp, Dictionary<int, int> needByType, Settlement market, Hero payee, float keepPercent, string why, out int gold)
        {
            gold = 0;
            if (mp == null || market == null || market.Town == null || market.ItemRoster == null || payee == null || !payee.IsAlive) return 0;
            var arm = ArmoryOf(mp);
            if (arm == null || arm.Count == 0) return 0;
            var have = new Dictionary<int, int>();
            foreach (var kv in arm)
            {
                if (kv.Key == null || kv.Value <= 0 || !SupplyDemand.Equipmentish(kv.Key) || MenPurse.HorseKind(kv.Key) || ArmsPricing.IsUnique(kv.Key)) continue;
                int t = (int)kv.Key.ItemType; int v; have.TryGetValue(t, out v); have[t] = v + kv.Value;
            }
            int sold = 0;
            bool wornBook = mp.IsLordParty;
            foreach (var hk in have.ToList())
            {
                int nd = 0; if (needByType != null) needByType.TryGetValue(hk.Key, out nd);
                int keep = (int)Math.Ceiling(nd * (1f + Math.Max(0f, keepPercent) / 100f));
                int extra = hk.Value - keep;
                if (extra <= 0) continue;
                var items = arm.Where(kv => kv.Key != null && kv.Value > 0 && (int)kv.Key.ItemType == hk.Key && SupplyDemand.Equipmentish(kv.Key) && !MenPurse.HorseKind(kv.Key) && !ArmsPricing.IsUnique(kv.Key))
                               .OrderBy(kv => kv.Key.Tier).ThenBy(kv => kv.Key.Value).Select(kv => kv.Key).ToList();
                bool broke = false;
                foreach (var it in items)
                {
                    int cnt; if (!arm.TryGetValue(it, out cnt)) continue;
                    while (cnt > 0 && extra > 0)
                    {
                        var el = wornBook ? new EquipmentElement(it, AiWear.TakeCondition(mp, it)) : new EquipmentElement(it);
                        int unit = Math.Max(1, market.Town.MarketData.GetPrice(el, mp, true, market.Party));
                        if (market.Town.Gold < unit) { broke = true; break; }
                        cnt--; extra--;
                        market.ItemRoster.AddToCounts(el, 1);
                        market.Town.ChangeGold(-unit);
                        payee.ChangeHeroGold(unit);
                        sold++; gold += unit;
                    }
                    if (cnt > 0) arm[it] = cnt; else arm.Remove(it);
                    if (broke || extra <= 0) break;
                }
                if (broke) break;
            }
            return sold;
        }

        // ------------------------------------------------------------ A7: latki gry
        public static void LeavePrefix(MobileParty mobileParty, out Dictionary<CharacterObject, int> __state)
        {
            __state = null;
            try { if (mobileParty != null && KitMoves) __state = Snapshot(mobileParty.MemberRoster); }
            catch (Exception e) { __state = null; Stumble("LeavePrefix", e); }
        }

        public static void LeavePostfix(MobileParty mobileParty, Settlement settlement, Dictionary<CharacterObject, int> __state)
        {
            if (__state == null) return;
            try
            {
                var to = settlement != null && settlement.Town != null ? settlement.Town.GarrisonParty : null;   // gra tworzy zaloge w metodzie
                if (to == null) return;
                var moved = Gone(__state, mobileParty.MemberRoster);
                if (moved.Count == 0) return;
                int pcs = MoveKits(mobileParty, to, moved, "leave");
                foreach (var v in moved.Values) _dLeftMen += v;
                _dLeftPcs += pcs;
            }
            catch (Exception e) { Stumble("LeavePostfix", e); }
        }

        public static void TakePrefix(Settlement settlement, out Dictionary<CharacterObject, int> __state)
        {
            __state = null;
            try
            {
                var g = settlement != null && settlement.Town != null ? settlement.Town.GarrisonParty : null;
                if (g != null && KitMoves) __state = Snapshot(g.MemberRoster);
            }
            catch (Exception e) { __state = null; Stumble("TakePrefix", e); }
        }

        public static void TakePostfix(MobileParty mobileParty, Settlement settlement, Dictionary<CharacterObject, int> __state)
        {
            if (__state == null || mobileParty == null) return;
            try
            {
                var g = settlement != null && settlement.Town != null ? settlement.Town.GarrisonParty : null;
                if (g == null) return;
                var moved = Gone(__state, g.MemberRoster);
                if (moved.Count == 0) return;
                int pcs = MoveKits(g, mobileParty, moved, "take");
                foreach (var v in moved.Values) _dTakenMen += v;
                _dTakenPcs += pcs;
            }
            catch (Exception e) { Stumble("TakePostfix", e); }
        }

        /// <summary>A7: rozwiazanie partii (DisbandPartyCampaignBehavior.OnPartyDisbanded - jedyne wejscie do obu scalen, przed zniszczeniem partii).</summary>
        public static void DisbandPrefix(MobileParty disbandParty, Settlement relatedSettlement, out bool __state)
        {
            __state = false;
            try
            {
                if (disbandParty == null || disbandParty.IsCustomParty || !KitMoves) return;   // partie zadan/modow - gra tez ich nie scala
                var roster = disbandParty.MemberRoster;
                // ten sam warunek co gra (OnPartyDisbanded + MergeDisbandPartyToFortification)
                bool toGarrison = relatedSettlement != null && relatedSettlement.IsFortification && !relatedSettlement.IsUnderSiege
                                  && roster != null && roster.TotalManCount > 0 && disbandParty.MapFaction == relatedSettlement.MapFaction;
                __state = toGarrison;
                if (toGarrison) return;
                var arm = ArmoryOf(disbandParty);
                if (arm == null || arm.Count == 0) return;
                // ludzie odchodza (milicja wsi, obca twierdza, wies zlupiona, osada null): ich komplety odchodza z nimi, tabor ponad nie - na targ dla rodu
                int before = 0; foreach (var v in arm.Values) if (v > 0) before += v;
                var market = MarketForDisband(relatedSettlement, disbandParty);
                var clan = disbandParty.ActualClan;
                var payee = clan != null && !clan.IsEliminated ? clan.Leader : null;
                int gold = 0, sold = 0;
                if (market != null && payee != null && payee.IsAlive)
                    sold = SellSurplus(disbandParty, NeedByType(disbandParty), market, payee, 0f, "rozwiazana", out gold);
                _dDisbandGone++; _dDisbandGoneKit += Math.Max(0, before - sold); _dDisbandSold += sold; _dDisbandGold += gold;
            }
            catch (Exception e) { Stumble("DisbandPrefix", e); }
        }

        public static void DisbandPostfix(MobileParty disbandParty, Settlement relatedSettlement, bool __state)
        {
            if (!__state) return;
            try
            {
                var g = relatedSettlement != null && relatedSettlement.Town != null ? relatedSettlement.Town.GarrisonParty : null;   // gra tworzy ja w scaleniu
                if (g == null) return;
                int pcs = MoveAll(disbandParty, g, "rozwiazana");
                _dDisbandIn++; _dDisbandInPcs += pcs;
            }
            catch (Exception e) { Stumble("DisbandPostfix", e); }
        }

        /// <summary>Targ dla taboru rozwiazanej partii: miasto - ono; zamek - jego miasto handlowe; wies - jej miasto (albo miasto handlowe jej zamku); w wojnie - brak.</summary>
        private static Settlement MarketForDisband(Settlement st, MobileParty mp)
        {
            if (st == null) return null;
            Settlement m = null;
            if (st.IsTown) m = st;
            else if (st.IsCastle) m = ArmyClothing.MarketTown(st);
            else if (st.IsVillage && st.Village != null && st.Village.Bound != null)
            {
                var b = st.Village.Bound;
                m = b.IsTown ? b : (b.IsCastle ? ArmyClothing.MarketTown(b) : null);
            }
            if (m == null || m.Town == null) return null;
            var f = mp != null ? mp.MapFaction : null;
            if (f != null && m.MapFaction != null && FactionManager.IsAtWarAgainstFaction(f, m.MapFaction)) return null;
            return m;
        }

        // ------------------------------------------------------------ C10 + linia dnia
        internal static void Daily()
        {
            int today = (int)CampaignTime.Now.ToDays;
            try { SellWeek(today); } catch (Exception e) { Stumble("SellWeek", e); }
            var s = Settings.Current;
            int moves = _dLeftMen + _dTakenMen + _dDisbandIn + _dDisbandGone + _dQueue + _stumbles;
            if (moves > 0 || (s != null && (s.KitMovesWithMen || s.GarrisonSellsSurplus)))
                Log.Info("Zbrojownie zalog (171): dzien " + today + " - komplet z ludzmi: lordowie zostawili w zalogach " + _dLeftMen + " ludzi (" + _dLeftPcs + " szt.), zabrali z zalog "
                         + _dTakenMen + " ludzi (" + _dTakenPcs + " szt.), rozwiazane partie do zalog " + _dDisbandIn + " (" + _dDisbandInPcs + " szt.), rozwiazane - ludzie odeszli "
                         + _dDisbandGone + " (komplety z ludzmi " + _dDisbandGoneKit + " szt., tabor sprzedany " + _dDisbandSold + " szt. za " + _dDisbandGold + " zl); nadwyzki zalog: sprzedalo "
                         + _dSoldGarrisons + " z " + _dQueue + " zalog w kolejce, " + _dSoldPcs + " szt. za " + _dSoldGold + " zl (kasy osad -> panowie; w tym ludzie na patrolach BK policzeni "
                         + _dPatrolCounted + " zalogi); potkniecia " + _stumbles + ".");
            ClearDay(); _stumbles = 0;
        }

        private static void SellWeek(int today)
        {
            var s = Settings.Current;
            if (s == null || !s.GarrisonSellsSurplus || !AiGear.On || AiGear.Armories() == null) return;
            Dictionary<Settlement, List<MobileParty>> patrols = null;
            foreach (var st in Settlement.All)
            {
                if (st == null || !st.IsFortification || st.Town == null) continue;
                var g = st.Town.GarrisonParty;
                if (g == null || (int)(g.Id.InternalValue % 7) != today % 7) continue;   // kazda zaloga raz na 7 dob (jak DTE GarbageCollectEquipments)
                try
                {
                    if (st.OwnerClan == Clan.PlayerClan && !s.GarrisonBuysGearPlayer) continue;
                    if (Undead.Party(g) || st.IsUnderSiege || g.MapEvent != null) continue;
                    var payee = st.OwnerClan != null ? st.OwnerClan.Leader : null;
                    if (payee == null || !payee.IsAlive || st.ItemRoster == null) continue;
                    var arm = ArmoryOf(g);
                    if (arm == null || arm.Count == 0) continue;
                    _dQueue++;
                    var need = NeedByType(g);
                    // ludzie zalogi na patrolu BK - sprzet zostal w twierdzy (GarrisonPartyComponent.CreateParty), nie sprzedajemy go
                    if (patrols == null) patrols = Patrols();
                    List<MobileParty> pl;
                    if (patrols.TryGetValue(st, out pl) && pl.Count > 0)
                    {
                        foreach (var p in pl) { var pn = NeedByType(p); foreach (var kv in pn) { int v; need.TryGetValue(kv.Key, out v); need[kv.Key] = v + kv.Value; } }
                        _dPatrolCounted++;
                    }
                    int gold;
                    int sold = SellSurplus(g, need, st, payee, s.SurplusKeepPercent, "zaloga", out gold);
                    if (sold > 0) { _dSoldGarrisons++; _dSoldPcs += sold; _dSoldGold += gold; }
                }
                catch (Exception e) { Stumble("SellWeek(" + st.StringId + ")", e); }
            }
        }

        private static Dictionary<Settlement, List<MobileParty>> Patrols()
        {
            var d = new Dictionary<Settlement, List<MobileParty>>();
            if (_bkPatrolType == null) return d;
            try
            {
                foreach (var p in MobileParty.All)
                {
                    if (p == null || !p.IsActive || p.PartyComponent == null || p.PartyComponent.GetType() != _bkPatrolType) continue;
                    var home = p.HomeSettlement;
                    if (home == null) continue;
                    List<MobileParty> l; if (!d.TryGetValue(home, out l)) d[home] = l = new List<MobileParty>(); l.Add(p);
                }
            }
            catch (Exception e) { Stumble("Patrols", e); }
            return d;
        }

        // ------------------------------------------------------------ C9: zapis
        /// <summary>v1| + osada>przedmiot:ile,...~ ; v1|off - zapis bez zbrojowni (wylacznik); id = StringId, bez modyfikatorow (DTE ich nie trzyma).</summary>
        internal static string Export()
        {
            if (!_restored) Restore("zapis przed startem sesji");
            var s = Settings.Current;
            var dict = AiGear.Armories();
            if (s == null || !s.GarrisonArmorySurvivesSave || dict == null) return "v1|off";
            var sb = new StringBuilder("v1|");
            int garrisons = 0, pcs = 0;
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null || !st.IsFortification || st.Town == null || st.Town.GarrisonParty == null) continue;
                    Dictionary<ItemObject, int> arm;
                    if (!dict.TryGetValue(st.Town.GarrisonParty.Id, out arm) || arm == null || arm.Count == 0) continue;
                    bool first = true;
                    foreach (var kv in arm)
                    {
                        if (kv.Key == null || kv.Value <= 0 || kv.Key.StringId == null) continue;
                        sb.Append(first ? st.StringId + ">" : ",").Append(kv.Key.StringId).Append(':').Append(kv.Value);
                        first = false; pcs += kv.Value;
                    }
                    if (!first) { sb.Append('~'); garrisons++; }
                }
                catch (Exception e) { Stumble("Export", e); }
            }
            Log.Info("Zbrojownie zalog (171): zapis - " + garrisons + " zalog, " + pcs + " szt.");
            return sb.ToString();
        }

        /// <summary>Z SyncData (wczytanie): tylko zapamietanie; null = brak klucza (zapis sprzed 171).</summary>
        internal static void Import(string s) { _loaded = true; _pending = s; }

        /// <summary>Z OnSessionLaunched (DTE skonczyl OnGameLoaded): zbrojownie zalog z zapisu albo - stary zapis - jednorazowe odtworzenie zalog AI (C9a).</summary>
        internal static void Restore(string why)
        {
            if (_restored) return;
            _restored = true;
            if (!_loaded) return;   // nowa gra - zbrojownie daje ColdStart
            var p = _pending; _pending = null;
            var s = Settings.Current;
            var dict = AiGear.Armories();
            if (dict == null) { Log.Info("Zbrojownie zalog (171): Dynamic Troop Equipment niedostepny - bez odtworzenia (" + why + ")."); return; }
            if (p == "v1|off") { Log.Info("Zbrojownie zalog (171): zapis zrobiony bez zbrojowni zalog (wylaczone w MCM przy zapisie) - jak przed 171."); return; }
            if (p == null || !p.StartsWith("v1|", StringComparison.Ordinal)) { OldSave(why); return; }
            if (s == null || !s.GarrisonArmorySurvivesSave) { Log.Info("Zbrojownie zalog (171): zapis ma zbrojownie zalog, ale Garrison Armory Survives Save wylaczone - nie ustawiam (" + why + ")."); return; }
            var om = MBObjectManager.Instance;
            int garrisons = 0, pcs = 0, noGarrison = 0, replaced = 0, unknown = 0;
            foreach (var rec in p.Substring(3).Split('~'))
            {
                if (rec.Length == 0) continue;
                try
                {
                    int gt = rec.IndexOf('>'); if (gt <= 0) continue;
                    Settlement st = null; try { st = om.GetObject<Settlement>(rec.Substring(0, gt)); } catch { }
                    var g = st != null && st.Town != null ? st.Town.GarrisonParty : null;
                    if (g == null) { noGarrison++; continue; }
                    Dictionary<ItemObject, int> arm;
                    if (dict.TryGetValue(g.Id, out arm) && arm != null && arm.Count > 0) { foreach (var v in arm.Values) if (v > 0) replaced += v; arm.Clear(); }
                    int got = 0;
                    foreach (var tok in rec.Substring(gt + 1).Split(','))
                    {
                        int c = tok.LastIndexOf(':'); if (c <= 0) continue;
                        int n; if (!int.TryParse(tok.Substring(c + 1), out n) || n <= 0) continue;
                        ItemObject it = null; try { it = om.GetObject<ItemObject>(tok.Substring(0, c)); } catch { }
                        if (it == null) { unknown += n; continue; }
                        if (AiGear.AddToArmory(g, it, n)) got += n;
                    }
                    if (got > 0) { garrisons++; pcs += got; }
                }
                catch (Exception e) { Stumble("Restore", e); }
            }
            Log.Info("Zbrojownie zalog (171): po wczytaniu przywrocone " + garrisons + " zalog, " + pcs + " szt. (bez zalogi " + noGarrison + ", zastapione z DTE " + replaced
                     + " szt., nieznane przedmioty " + unknown + ").");
        }

        /// <summary>C9a: zapis sprzed 171 - zalogi AI raz uzupelnione do wzorca swoich ludzi (zalogi gracza i Innych bez zmian).</summary>
        private static void OldSave(string why)
        {
            var s = Settings.Current;
            if (s == null || !s.GarrisonArmoryRestoreOldSave)
            {
                Log.Info("Zbrojownie zalog (171): stary zapis - brak klucza, odtworzenie wylaczone w MCM; zalogi bez zbrojowni do pierwszego zapisu z 171.");
                return;
            }
            int garrisons = 0, pcs = 0, seen = 0;
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null || !st.IsFortification || st.Town == null || st.Town.GarrisonParty == null) continue;
                    var g = st.Town.GarrisonParty;
                    if (st.OwnerClan == Clan.PlayerClan || Undead.Party(g)) continue;
                    seen++;
                    int n = ColdStart.FillToTemplate(g);
                    if (n > 0) { garrisons++; pcs += n; }
                }
                catch (Exception e) { Stumble("OldSave", e); }
            }
            Log.Info("Zbrojownie zalog (171): stary zapis bez zbrojowni zalog (" + why + ") - odtworzone po bledzie DTE " + garrisons + " zalog AI z " + seen + ", " + pcs
                     + " szt. (wzorce ich ludzi, jednorazowo; zalogi gracza bez zmian).");
        }

        // ------------------------------------------------------------ wpiecie (flagi na caly proces)
        internal static void ApplyAll(Harmony h)
        {
            if (_applied || h == null) return;
            _applied = true;
            _h = h;
            try
            {
                if (!_leaveHooked)
                {
                    var m = AccessTools.Method(typeof(GarrisonTroopsCampaignBehavior), "LeaveTroopsToGarrison", new[] { typeof(MobileParty), typeof(Settlement), typeof(int), typeof(bool) });
                    if (m != null) { h.Patch(m, prefix: new HarmonyMethod(typeof(GarrisonArmory), nameof(LeavePrefix)), postfix: new HarmonyMethod(typeof(GarrisonArmory), nameof(LeavePostfix))); _leaveHooked = true; }
                }
            }
            catch (Exception e) { Log.Error("GarrisonArmory.ApplyAll(Leave)", e); }
            try
            {
                if (!_takeHooked)
                {
                    var m = AccessTools.Method(typeof(GarrisonTroopsCampaignBehavior), "TakeTroopsFromGarrison", new[] { typeof(MobileParty), typeof(Settlement), typeof(int), typeof(bool) });
                    if (m != null) { h.Patch(m, prefix: new HarmonyMethod(typeof(GarrisonArmory), nameof(TakePrefix)), postfix: new HarmonyMethod(typeof(GarrisonArmory), nameof(TakePostfix))); _takeHooked = true; }
                }
            }
            catch (Exception e) { Log.Error("GarrisonArmory.ApplyAll(Take)", e); }
            try
            {
                if (!_disbandHooked)
                {
                    var m = AccessTools.Method(typeof(DisbandPartyCampaignBehavior), "OnPartyDisbanded", new[] { typeof(MobileParty), typeof(Settlement) });
                    if (m != null) { h.Patch(m, prefix: new HarmonyMethod(typeof(GarrisonArmory), nameof(DisbandPrefix)), postfix: new HarmonyMethod(typeof(GarrisonArmory), nameof(DisbandPostfix))); _disbandHooked = true; }
                }
            }
            catch (Exception e) { Log.Error("GarrisonArmory.ApplyAll(Disband)", e); }
            try { _bkPatrolType = AccessTools.TypeByName("BannerKings.Components.GarrisonPartyComponent"); } catch { _bkPatrolType = null; }
            Log.Info("GarrisonArmory: komplet z ludzmi - zostawienie " + (_leaveHooked ? "wpiete" : "BRAK") + ", zabranie " + (_takeHooked ? "wpiete" : "BRAK")
                     + ", rozwiazanie partii " + (_disbandHooked ? "wpiete" : "BRAK") + "; patrole BK: typ " + (_bkPatrolType != null ? "znaleziony" : "brak") + ".");
        }
    }
}
