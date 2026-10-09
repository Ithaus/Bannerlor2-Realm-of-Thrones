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
    ///    to naprawa skutku bledu DTE (ten sprzet byl oplacony i lezal w zalogach przed zapisem), nie dosypka. sklad7: takze zalogi gracza (od K1
    ///    zaloga walczy tylko tym, co ma - bez dorobku bronilaby sie nago).
    ///  - A7 (Z2): lord zostawia ludzi w zalodze albo ich zabiera, rozwiazana partia wchodzi do zalogi - sprzet idzie z ludzmi miedzy zbrojowniami
    ///    DTE (gra przenosi samych ludzi); ludzie rozwiazanej partii, ktorzy odchodza, biora swoje komplety, a tabor ponad nie sprzedaje sie dla rodu.
    ///  - C10 (Z9): raz w tygodniu zaloga AI sprzedaje to, co ma ponad potrzebe swoich ludzi (i ludzi na patrolach BK) oraz zapas, na polke
    ///    wlasnej osady po cenie skupu. sklad7 (scalenie K1): sprzedaje MenPurse.SellArmorySurplus - ta sama regula co nadwyzki partii lorda
    ///    (po dopasowaniu, K1 A9): trzecia panu, reszta do sakiewki zalogi (bez sakiewki - wszystko panu); zalogi gracza w systemie (InSystem).
    /// sklad7: JEDEN zapis zbrojowni zalog (klucz arm_garrisonarmory) - ten plik; K1c GarrisonKit.ExportArmories/RestoreArmories usuniete (LegacyK1c czyta ich format).
    /// Recenzja kodu 171: (1) zbrojownie partii lordow bez wodza / rozwiazywanych chroni AiGear.KeepGarrisonArmory (DTE kasowal je w dobie czekania na
    /// rozwiazanie, wiec A7 pkt 2 przenosil 0 szt.) i zapisuje Export (rekord "@partia" - DTE ich nie zapisuje); (2) stan sztuk (zapis obitych AiWear) idzie
    /// z ludzmi takze do i z zalog, C10 sprzedaje ze stanem; (3) lord zabierajacy ludzi zostawia sprzet ludzi zalogi na patrolach BK.
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
            _patrols = null; _patrolsDay = -1;
        }

        private static int _dKeptLeaderless;   // recenzja 171: partie lordow bez wodza / rozwiazywane, ktorych zbrojowni DTE nie skasowal (wywolania GC - raz na partie na dobe)

        /// <summary>Z AiGear.KeepGarrisonArmory - tylko licznik.</summary>
        internal static void NoteKeptLeaderless() { _dKeptLeaderless++; }

        private static void ClearDay()
        {
            _dLeftMen = _dLeftPcs = _dTakenMen = _dTakenPcs = _dDisbandIn = _dDisbandInPcs = _dDisbandGone = _dDisbandGoneKit = _dDisbandSold = _dDisbandGold = 0;
            _dQueue = _dSoldGarrisons = _dSoldPcs = _dSoldGold = _dPatrolCounted = 0;
            _dKeptLeaderless = 0;
            _dScrToMen = _dScrToPcs = _dScrFromMen = _dScrFromPcs = _dScrPlus = _dScrBook = _dScrRefused = 0;
            _dScrToPartyMen = _dScrToPartyPcs = _dScrFromPartyMen = _dScrFromPartyPcs = _dNewPartyMen = _dNewPartyPcs = 0;
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
        internal static Dictionary<int, int> NeedByType(MobileParty mp) { return NeedByType(mp != null ? mp.MemberRoster : null); }

        /// <summary>sklad7: to samo dla rosteru (zaloga razem z ludzmi na patrolach BK - MenPurse.SellArmorySurplus).</summary>
        internal static Dictionary<int, int> NeedByType(TroopRoster roster)
        {
            var need = new Dictionary<int, int>();
            if (roster == null) return need;
            for (int i = 0; i < roster.Count; i++) { var el = roster.GetElementCopyAtIndex(i); AddNeed(need, el.Character, el.Number); }
            return need;
        }

        /// <summary>
        /// sklad7 (scalenie 171 + K1) - JEDNA regula "kto jest w systemie zbrojenia zalog" (zakupy brakow i lepszego, zamowienia zamku, nadwyzki, cwiczenia
        /// wedlug broni, autowerbunek z kompletem): zaloga AI zawsze; zaloga gracza - gdy ma sakiewke z zoldu (K1, GarrisonPurseEnabled - Jeff 09.10 K:
        /// "dotyczy jego druzyny i jego zalog") albo GarrisonBuysGearPlayer (doplata z Twojej kiesy). Dotad 171 wpuszczal zalogi gracza tylko przy
        /// GarrisonBuysGearPlayer, a K1 kupowal im z sakiewki - dwie reguly dla tej samej zalogi.
        /// </summary>
        internal static bool InSystem(Settlement st)
        {
            var s = Settings.Current;
            return st == null || st.OwnerClan != Clan.PlayerClan || (s != null && (s.GarrisonBuysGearPlayer || MenUpgrade.GarrisonPurseOn));
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
        internal static int MoveKits(MobileParty from, MobileParty to, Dictionary<CharacterObject, int> moved, string why, Dictionary<int, int> extraLeft = null)
        {
            if (from == null || to == null || moved == null || moved.Count == 0) return 0;
            var arm = ArmoryOf(from);
            if (arm == null || arm.Count == 0) return 0;
            var needLeft = NeedByType(from);   // roster dawcy PO przeniesieniu
            // recenzja 171: ludzie zalogi na patrolu BK - ich sprzet lezy w zbrojowni zalogi i zostaje dla nich (jak w C10)
            AddExtra(needLeft, extraLeft);
            var pool = new Dictionary<ItemObject, int>();
            foreach (var kv in arm)
                if (kv.Key != null && kv.Value > 0 && SupplyDemand.Equipmentish(kv.Key) && !ArmsPricing.IsUnique(kv.Key)) pool[kv.Key] = kv.Value;
            var taken = PlanKits(pool, moved, needLeft);
            int pcs = 0;
            bool synced = false;
            foreach (var kv in taken)
            {
                if (kv.Value <= 0 || !AiGear.AddToArmory(to, kv.Key, kv.Value)) continue;
                int c; arm.TryGetValue(kv.Key, out c);
                // recenzja 171: stan sztuk idzie z nimi (zapis obitych - takze zalog), przed zdjeciem ze zbrojowni dawcy; spis dawcy raz na przeniesienie
                try { AiWear.MoveWorn(from, to, kv.Key, kv.Value, c, !synced); synced = true; } catch (Exception e) { Stumble("MoveWorn", e); }
                c -= kv.Value;
                if (c > 0) arm[kv.Key] = c; else arm.Remove(kv.Key);
                pcs += kv.Value;
            }
            return pcs;
        }

        private static void AddExtra(Dictionary<int, int> need, Dictionary<int, int> extra)
        {
            if (need == null || extra == null) return;
            foreach (var kv in extra) { int v; need.TryGetValue(kv.Key, out v); need[kv.Key] = v + kv.Value; }
        }

        /// <summary>
        /// A7 (wydzielone w poprawkach sklad7 - ta sama regula dla sciezek AI i ekranu druzyny gracza): ktore sztuki z puli dawcy (przedmiot -> ile,
        /// juz po odsianiu tego, czego nie wolno ruszyc) ida z przeniesionymi ludzmi. needLeft - potrzeba po typach ludzi, ktorzy zostaja u dawcy.
        /// </summary>
        private static Dictionary<ItemObject, int> PlanKits(Dictionary<ItemObject, int> pool, Dictionary<CharacterObject, int> moved, Dictionary<int, int> needLeft)
        {
            var taken = new Dictionary<ItemObject, int>();
            if (pool == null || pool.Count == 0 || moved == null || moved.Count == 0) return taken;
            if (needLeft == null) needLeft = new Dictionary<int, int>();
            var needMoved = new Dictionary<int, int>();
            foreach (var kv in moved) AddNeed(needMoved, kv.Key, kv.Value);
            var have = new Dictionary<int, int>();
            var byType = new Dictionary<int, List<ItemObject>>();
            var avail = new Dictionary<ItemObject, int>();
            foreach (var kv in pool)
            {
                if (kv.Key == null || kv.Value <= 0) continue;
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
            return taken;
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
            bool synced = false;
            foreach (var kv in arm.ToList())
            {
                if (kv.Key == null || kv.Value <= 0) continue;
                if (AiGear.AddToArmory(to, kv.Key, kv.Value))
                {
                    try { AiWear.MoveWorn(from, to, kv.Key, kv.Value, kv.Value, !synced); synced = true; } catch (Exception e) { Stumble("MoveWorn", e); }   // recenzja 171: caly zapis obitych
                    pcs += kv.Value; arm.Remove(kv.Key);
                }
            }
            arm.Clear();
            return pcs;
        }

        /// <summary>
        /// Nadwyzka ponad potrzebe (po typach) i zapas keepPercent na polke targu po cenie skupu, najwyzej tyle, ile kasa udzwignie; zloto do payee.
        /// Najgorsze sztuki najpierw (tier, potem wartosc), bez koni i rzedow (Stajnia) i bez unikatow. Sztuka w stanie z ksiegi AiWear (partia lorda
        /// i - recenzja 171 - zaloga: zakupy, dostawy wozem, przeniesienia i autowerbunek zapisuja jej obite); zaloga przy wylaczonym AiWear - sprawna.
        /// sklad7: juz TYLKO tabor rozwiazanej partii, ktorej ludzie odchodza (A7, keepPercent 0, zloto dla rodu). Nadwyzki zalog sprzedaje
        /// MenPurse.SellArmorySurplus (SellWeek) - ta sama regula co nadwyzki lordow.
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
            bool wornBook = mp.IsLordParty || (mp.IsGarrison && AiWear.BookOn);   // recenzja 171: zaloga tez sprzedaje ze stanem z zapisu (obita nie idzie jako sprawna)
            bool synced = false;
            bool meleeGroup = AiGear.SubstituteMeleeOn;   // 174 pytanie 2a: bron biala liczona razem (sztuka zastepcza innego typu nie jest nadwyzka)
            int meleeLeft = meleeGroup ? AiGear.MeleeGroupExtra(have, needByType, keepPercent) : int.MaxValue;
            foreach (var hk in have.ToList())
            {
                int nd = 0; if (needByType != null) needByType.TryGetValue(hk.Key, out nd);
                int keep = (int)Math.Ceiling(nd * (1f + Math.Max(0f, keepPercent) / 100f));
                int extra = hk.Value - keep;
                bool grp = meleeGroup && AiGear.Melee(hk.Key);
                if (grp) { extra = Math.Min(extra, meleeLeft); meleeLeft -= Math.Max(0, extra); }
                if (extra <= 0) continue;
                var items = arm.Where(kv => kv.Key != null && kv.Value > 0 && (int)kv.Key.ItemType == hk.Key && SupplyDemand.Equipmentish(kv.Key) && !MenPurse.HorseKind(kv.Key) && !ArmsPricing.IsUnique(kv.Key))
                               .OrderBy(kv => kv.Key.Tier).ThenBy(kv => kv.Key.Value).Select(kv => kv.Key).ToList();
                bool broke = false;
                foreach (var it in items)
                {
                    int cnt; if (!arm.TryGetValue(it, out cnt)) continue;
                    while (cnt > 0 && extra > 0)
                    {
                        var el = wornBook ? new EquipmentElement(it, AiWear.TakeCondition(mp, it, !synced)) : new EquipmentElement(it);
                        synced = true;   // spis zbrojowni raz na sprzedaz (nie przy kazdej sztuce)
                        int unit = Math.Max(1, market.Town.MarketData.GetPrice(el, mp, true, market.Party));
                        if (market.Town.Gold < unit)
                        {
                            if (wornBook) { try { AiWear.NoteBought(mp, el, 1); } catch { } }   // sztuka zostaje - jej stan wraca do zapisu
                            broke = true; break;
                        }
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
                int pcs = MoveKits(g, mobileParty, moved, "take", PatrolNeed(settlement));
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
            int moves = _dLeftMen + _dTakenMen + _dDisbandIn + _dDisbandGone + _dQueue + _stumbles + _dScrToMen + _dScrFromMen + _dScrToPartyMen + _dScrFromPartyMen + _dNewPartyMen;
            if (moves > 0 || (s != null && (s.KitMovesWithMen || s.GarrisonSellsSurplus)))
                Log.Info("Zbrojownie zalog (171): dzien " + today + " - komplet z ludzmi: lordowie zostawili w zalogach " + _dLeftMen + " ludzi (" + _dLeftPcs + " szt.), zabrali z zalog "
                         + _dTakenMen + " ludzi (" + _dTakenPcs + " szt.), ekran druzyny gracza (poprawki sklad7): do zalog " + _dScrToMen + " ludzi (" + _dScrToPcs + " szt.), z zalog "
                         + _dScrFromMen + " ludzi (" + _dScrFromPcs + " szt.; pominiete: na plus zostaja w druzynie " + _dScrPlus + " szt., id z Twoja czescia w ksiedze " + _dScrBook
                         + ", DTE nie przyjal " + _dScrRefused + " szt.; sklad7b: do partii towarzyszy/rodu/lordow " + _dScrToPartyMen + " ludzi (" + _dScrToPartyPcs
                         + " szt.), z partii " + _dScrFromPartyMen + " ludzi (" + _dScrFromPartyPcs + " szt.), nowe partie rodu " + _dNewPartyMen + " ludzi (" + _dNewPartyPcs
                         + " szt.)), rozwiazane partie do zalog " + _dDisbandIn + " (" + _dDisbandInPcs + " szt.), rozwiazane - ludzie odeszli "
                         + _dDisbandGone + " (komplety z ludzmi " + _dDisbandGoneKit + " szt., tabor sprzedany " + _dDisbandSold + " szt. za " + _dDisbandGold + " zl); nadwyzki zalog: sprzedalo "
                         + _dSoldGarrisons + " z " + _dQueue + " zalog w kolejce, " + _dSoldPcs + " szt. za " + _dSoldGold + " zl (kasy osad -> trzecia panom, reszta sakiewkom zalog; w tym ludzie na patrolach BK policzeni "
                         + _dPatrolCounted + " zalogi); partie lordow bez wodza albo rozwiazywane - zbrojownia zachowana " + _dKeptLeaderless + "; potkniecia " + _stumbles + ".");
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
                    if (!InSystem(st)) continue;   // sklad7: zaloga gracza w systemie, gdy ma sakiewke (K1) albo GarrisonBuysGearPlayer
                    if (Undead.Party(g) || st.IsUnderSiege || g.MapEvent != null) continue;
                    var payee = st.OwnerClan != null ? st.OwnerClan.Leader : null;
                    if (payee != null && !payee.IsAlive) payee = null;
                    // bez sakiewki zalogi cale zloto dostaje pan (171) - bez pana nie ma komu sprzedac; z sakiewka trzecia panu, reszta zalodze (K1 A9)
                    if ((payee == null && !MenUpgrade.GarrisonPurseOn) || st.ItemRoster == null || st.Town == null) continue;
                    var arm = ArmoryOf(g);
                    if (arm == null || arm.Count == 0) continue;
                    _dQueue++;
                    // ludzie zalogi na patrolu BK - sprzet zostal w twierdzy (GarrisonPartyComponent.CreateParty), nie sprzedajemy go
                    TroopRoster roster = g.MemberRoster;
                    if (patrols == null) patrols = PatrolsToday();
                    List<MobileParty> pl;
                    if (patrols.TryGetValue(st, out pl) && pl.Count > 0)
                    {
                        roster = WithPatrols(g.MemberRoster, pl);
                        _dPatrolCounted++;
                    }
                    // sklad7 (scalenie 171 C10 + K1 A9): JEDNA sprzedaz nadwyzek zalogi - regula K1 (po dopasowaniu: najpierw sztuki, ktorych nikt
                    // nie udzwignie, potem najgorsze ponad komplet + zapas; ta sama co u lordow AI), w kolejce i na polke wlasnej osady z 171
                    int gold;
                    bool mine = payee != null && payee == Hero.MainHero;
                    int before = mine ? Hero.MainHero.Gold : 0;
                    int sold = MenPurse.SellGarrisonSurplus(g, st, payee, roster, out gold);
                    if (sold > 0) { _dSoldGarrisons++; _dSoldPcs += sold; _dSoldGold += gold; }
                    // poprawki sklad7: Twoja kiesa nie zmienia sie bez slowa (raz na tydzien na zaloge - kolejka C10)
                    if (sold > 0 && mine)
                        Log.Player("Your garrison of " + st.Name + " sold " + sold + " spare pieces of kit for " + gold + " denars; your third: " + Math.Max(0, Hero.MainHero.Gold - before) + ".");
                }
                // poprawki sklad7: jedno miejsce w logu na dobe (dotad osobno dla kazdej zalogi, a MenPurse.SellGarrisonSurplus logowal jeszcze raz sam) - reszta w liczniku potkniec
                catch (Exception e) { Stumble("SellWeek(zaloga)", e); }
            }
        }

        /// <summary>sklad7: roster zalogi razem z ludzmi jej patroli BK (bez bohaterow) - do dopasowania nadwyzek (ich sprzet lezy w twierdzy).</summary>
        private static TroopRoster WithPatrols(TroopRoster garrison, List<MobileParty> patrols)
        {
            var r = TroopRoster.CreateDummyTroopRoster();
            var all = new List<TroopRoster> { garrison };
            foreach (var p in patrols) if (p != null && p.MemberRoster != null) all.Add(p.MemberRoster);
            foreach (var src in all)
            {
                if (src == null) continue;
                for (int i = 0; i < src.Count; i++)
                {
                    var el = src.GetElementCopyAtIndex(i);
                    if (el.Character == null || el.Character.IsHero || el.Number <= 0) continue;
                    r.AddToCounts(el.Character, el.Number);
                }
            }
            return r;
        }

        /// <summary>Recenzja 171: potrzeba (po typach) ludzi zalogi tej osady na patrolach BK - dla A7 (lord zabiera ludzi); mapa patroli raz na dobe.</summary>
        private static Dictionary<int, int> PatrolNeed(Settlement st)
        {
            if (st == null || _bkPatrolType == null) return null;
            List<MobileParty> pl;
            if (!PatrolsToday().TryGetValue(st, out pl) || pl.Count == 0) return null;
            var need = new Dictionary<int, int>();
            foreach (var p in pl) { var pn = NeedByType(p); foreach (var kv in pn) { int v; need.TryGetValue(kv.Key, out v); need[kv.Key] = v + kv.Value; } }
            return need;
        }

        private static Dictionary<Settlement, List<MobileParty>> _patrols;
        private static int _patrolsDay = -1;

        /// <summary>Mapa patroli BK (osada -> patrole) liczona leniwie raz na dobe (jeden przeglad MobileParty.All) - wspolna dla C10 i A7.</summary>
        private static Dictionary<Settlement, List<MobileParty>> PatrolsToday()
        {
            int d = (int)CampaignTime.Now.ToDays;
            if (_patrols == null || _patrolsDay != d) { _patrols = Patrols(); _patrolsDay = d; }
            return _patrols;
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

        // ------------------------------------------------------------ poprawki sklad7 (uwaga krytyczna): ekran druzyny gracza
        // Gracz przenosi ludzi miedzy druzyna a zaloga ekranem druzyny: "Manage garrison" (PartyScreenHelper.OpenScreenAsManageTroopsAndPrisoners - lewa
        // strona to zywy roster zalogi) i "Donate troops" do zalogi cudzej twierdzy (OpenScreenAsDonateGarrisonWithCurrentSettlement - lewa strona pusta,
        // gra dopisuje ludzi do zalogi w funkcji Done). Sciezki A7 (Leave/Take/Disband) sa tylko AI, a DTE nie ma latki na ekran druzyny - dotad posilki
        // stawaly w zalodze bez sprzetu (przy GarrisonFightsWithArmoryOnly walczyly nago), ich komplety zostawaly w zbrojowni druzyny (MenPurse sprzedawal je
        // w miescie jako nadwyzke), a ludzie zabrani z zalogi zostawiali komplety (SellWeek sprzedawal je, trzecia dla Ciebie). Teraz ta sama regula A7
        // (PlanKits) w obie strony: czesc LUDZI zbrojowni druzyny DTE (ArmyArmory.Armory; Twoja ksiega nietknieta - KitPieces/MenTotal, Twoja czesc to
        // najgorsze egzemplarze id) <-> slownik zbrojowni zalogi. Konie i rzedy - Stajnia (bez zmian). Rostery ekranu sa zywe (partie zmienione przed Done),
        // wiec stan "przed" bierzemy z PartyScreenLogic._initialData (kopie rosterow z otwarcia ekranu albo z ostatniego Done).
        // sklad7b (Jeff 09.10 07:35, (3) "tak"): ta sama regula dla partii Twoich towarzyszy i rodu ("Manage Troops" - OpenScreenAsManageTroops
        // i OpenScreenAsManageTroopsAndPrisoners z partia po lewej), dla lorda innego rodu ("Donate Troops" - OpenScreenAsDonateTroops, lewa strona
        // to zywy roster jego partii) i dla nowej partii rodu (OpenScreenAsCreateClanPartyForHero - ludzie przechodza w funkcji zamkniecia ekranu,
        // po DoneLogic). Dotad ludzie szli do partii towarzysza bez kompletow (a od sklad7b walczyliby bez nich), komplety zostawaly w Twojej
        // zbrojowni i szly do kupca jako nadwyzka ludzi; ludzie zabrani z partii zostawiali tam swoje.
        private static System.Reflection.FieldInfo _fInitial;
        private static bool _screenHooked, _createHooked;
        private static int _dScrToMen, _dScrToPcs, _dScrFromMen, _dScrFromPcs, _dScrPlus, _dScrBook, _dScrRefused;
        private static int _dScrToPartyMen, _dScrToPartyPcs, _dScrFromPartyMen, _dScrFromPartyPcs, _dNewPartyMen, _dNewPartyPcs;

        private sealed class ScreenState { internal MobileParty G; internal Settlement St; internal bool Garrison; internal string Name; internal Dictionary<CharacterObject, int> GBefore, MainBefore; }

        /// <summary>sklad7b: partia po lewej stronie ekranu druzyny, z ktora ludzie wymieniaja komplety - partia lorda (towarzysze i partie rodu gracza,
        /// lord innego rodu przy "Donate Troops"); bez umarlych (sprzet umarlych poza gospodarka).</summary>
        private static bool PartyTakesKit(MobileParty mp)
        {
            return mp != null && mp != MobileParty.MainParty && mp.IsActive && mp.IsLordParty && mp.LeaderHero != null && !mp.IsGarrison && !Undead.Party(mp);
        }

        /// <summary>Prefiks PartyScreenLogic.DoneLogic: zaloga po lewej (albo "Donate troops") - stan rosterow przed ekranem. sklad7b: takze partia
        /// lorda po lewej (towarzysze i partie rodu gracza - "Manage Troops"; lord innego rodu - "Donate Troops").</summary>
        public static void ScreenDonePrefix(PartyScreenLogic __instance, ref object __state)
        {
            __state = null;
            try
            {
                if (__instance == null || _fInitial == null || !KitMoves) return;
                if (MobileParty.MainParty == null || __instance.RightOwnerParty != PartyBase.MainParty) return;
                var left = __instance.LeftOwnerParty;
                MobileParty g = null; bool live = false;
                var lm = left != null && left.IsMobile ? left.MobileParty : null;
                if (lm != null && (lm.IsGarrison || PartyTakesKit(lm))) { g = lm; live = true; }
                else if (left == null)
                {
                    var gsm = Game.Current != null ? Game.Current.GameStateManager : null;
                    var ps = gsm != null ? gsm.ActiveState as TaleWorlds.CampaignSystem.GameState.PartyState : null;
                    var cur = Settlement.CurrentSettlement;
                    if (ps != null && ps.IsDonating && ps.PartyScreenMode == Helpers.PartyScreenHelper.PartyScreenMode.TroopsManage && cur != null && cur.Town != null)
                        g = cur.Town.GarrisonParty;
                }
                if (g == null || g.MemberRoster == null) return;
                var init = _fInitial.GetValue(__instance) as PartyScreenData;
                if (init == null || init.RightMemberRoster == null || (live && init.LeftMemberRoster == null)) return;
                bool gar = g.IsGarrison;
                var st = gar ? (g.CurrentSettlement ?? g.HomeSettlement) : null;   // sklad7b: osada (patrole BK) tylko dla zalogi
                string name = gar ? (st != null ? "the garrison of " + st.Name : "the garrison") : (g.Name != null ? g.Name.ToString() : g.StringId);
                __state = new ScreenState { G = g, St = st, Garrison = gar, Name = name, GBefore = Snapshot(live ? init.LeftMemberRoster : g.MemberRoster), MainBefore = Snapshot(init.RightMemberRoster) };
            }
            catch (Exception e) { __state = null; Stumble("ScreenDonePrefix", e); }
        }

        /// <summary>Postfiks DoneLogic (tylko zatwierdzony ekran): komplety ida z ludzmi w obie strony.</summary>
        public static void ScreenDonePostfix(bool __result, object __state)
        {
            var ss = __state as ScreenState;
            if (!__result || ss == null) return;
            try
            {
                var main = MobileParty.MainParty;
                var g = ss.G;
                if (main == null || main.MemberRoster == null || g == null || g.MemberRoster == null) return;
                var gNow = Snapshot(g.MemberRoster); var mNow = Snapshot(main.MemberRoster);
                var toG = Crossed(ss.MainBefore, mNow, ss.GBefore, gNow);
                var toMain = Crossed(ss.GBefore, gNow, ss.MainBefore, mNow);
                string name = ss.Name ?? g.StringId;
                if (toG.Count > 0)
                {
                    int men = 0; foreach (var v in toG.Values) men += v;
                    int pcs = KitsToGarrison(g, toG);
                    if (ss.Garrison) { _dScrToMen += men; _dScrToPcs += pcs; } else { _dScrToPartyMen += men; _dScrToPartyPcs += pcs; }
                    if (pcs > 0) Log.Player(men + " men joined " + name + " and took " + pcs + " pieces of their own kit from your stores.");
                    else if (ss.Garrison)
                        Log.Player(men + " men joined " + name + " with no kit of their own in your stores"
                                   + (GarrisonKit.BareOn ? " - in battle the garrison fights only with what its stores hold." : "."), true);
                    else NoKitMessage(men, name, g);
                }
                if (toMain.Count > 0)
                {
                    int men = 0; foreach (var v in toMain.Values) men += v;
                    int pcs = KitsToMain(g, ss.St, toMain);
                    if (ss.Garrison) { _dScrFromMen += men; _dScrFromPcs += pcs; } else { _dScrFromPartyMen += men; _dScrFromPartyPcs += pcs; }
                    Log.Player(men + " men left " + name + (pcs > 0 ? " and brought " + pcs + " pieces of their kit to your stores." : " - its stores held no kit of theirs."));
                }
            }
            catch (Exception e) { Stumble("ScreenDonePostfix", e); }
        }

        /// <summary>sklad7b: nowa partia rodu (Clan -> Parties -> Create new party: PartyScreenHelper.OpenScreenAsCreateClanPartyForHeroPartyScreenClosed
        /// tworzy partie i przenosi do niej ludzi z Twojej druzyny PO DoneLogic, na rosterach-kopiach ekranu) - stan Twojej druzyny przed.</summary>
        public static void CreatePartyPrefix(out Dictionary<CharacterObject, int> __state)
        {
            __state = null;
            try { var main = MobileParty.MainParty; if (main != null && KitMoves) __state = Snapshot(main.MemberRoster); }
            catch (Exception e) { __state = null; Stumble("CreatePartyPrefix", e); }
        }

        /// <summary>sklad7b: po utworzeniu partii rodu - ludzie, ktorzy z Twojej druzyny przeszli do niej, biora swoje komplety (ta sama regula co
        /// ekran druzyny: KitsToGarrison - czesc LUDZI Twojej zbrojowni, bez Twojej ksiegi, koni i sztuk na plus). Bez tego nowa partia nie ma
        /// zbrojowni wcale (LevyGold: bez darmowego kompletu DTE), a od sklad7b jej ludzie walczylby w Twoich bitwach bez niczego.</summary>
        public static void CreatePartyPostfix(TroopRoster __1, bool __6, Dictionary<CharacterObject, int> __state)
        {
            if (__6 || __state == null || __1 == null) return;
            try
            {
                var main = MobileParty.MainParty;
                if (main == null) return;
                Hero hero = null;
                for (int i = 0; i < __1.Count; i++) { var ch = __1.GetCharacterAtIndex(i); if (ch != null && ch.IsHero && ch.HeroObject != null) hero = ch.HeroObject; }
                var np = hero != null ? hero.PartyBelongedTo : null;
                if (np == null || np == main || np.MemberRoster == null) return;
                var moved = Crossed(__state, Snapshot(main.MemberRoster), new Dictionary<CharacterObject, int>(), Snapshot(np.MemberRoster));
                if (moved.Count == 0) return;
                int men = 0; foreach (var v in moved.Values) men += v;
                int pcs = KitsToGarrison(np, moved);
                _dNewPartyMen += men; _dNewPartyPcs += pcs;
                string name = np.Name != null ? np.Name.ToString() : np.StringId;
                if (pcs > 0) Log.Player(men + " men joined " + name + " and took " + pcs + " pieces of their own kit from your stores.");
                else NoKitMessage(men, name, np);
            }
            catch (Exception e) { Stumble("CreatePartyPostfix", e); }
        }

        /// <summary>sklad7b-p (uwaga 17): ludzie przeszli do partii lorda bez kompletu z Twojej zbrojowni. Partia bez zadnej sztuki (nowa partia rodu,
        /// pusta zbrojownia) - wprost, ze beda walczyc golymi rekami, dopoki czegos nie kupi; partia z zapasem - ze walcza tym, co ma jej zbrojownia.</summary>
        private static void NoKitMessage(int men, string name, MobileParty p)
        {
            if (!GarrisonKit.OwnKitOn) { Log.Player(men + " men joined " + name + " with no kit of their own in your stores.", true); return; }
            var arm = ArmoryOf(p);
            int have = 0;
            if (arm != null) foreach (var v in arm.Values) if (v > 0) have += v;
            if (have <= 0)
                Log.Player(men + " men joined " + name + " with no kit of their own in your stores - the party has no arms yet, so they will fight bare-handed until it buys some.", true);
            else
                Log.Player(men + " men joined " + name + " with no kit of their own in your stores - in battle they fight only with what that party's stores hold.", true);
        }

        /// <summary>Ludzie (oddzial -> ilu), ktorzy ubyli po stronie A i przybyli po stronie B - min z obu (awans i werbunek jencow to nie przeniesienie).</summary>
        private static Dictionary<CharacterObject, int> Crossed(Dictionary<CharacterObject, int> aBefore, Dictionary<CharacterObject, int> aNow,
                                                              Dictionary<CharacterObject, int> bBefore, Dictionary<CharacterObject, int> bNow)
        {
            var d = new Dictionary<CharacterObject, int>();
            foreach (var kv in aBefore)
            {
                int an, bb, bn; aNow.TryGetValue(kv.Key, out an); bBefore.TryGetValue(kv.Key, out bb); bNow.TryGetValue(kv.Key, out bn);
                int c = Math.Min(kv.Value - an, bn - bb);
                if (c > 0) d[kv.Key] = c;
            }
            return d;
        }

        /// <summary>Druzyna -> zaloga: komplety z czesci LUDZI zbrojowni druzyny (bez Twojej ksiegi, koni, unikatow i sztuk z modyfikatorem na plus -
        /// zbrojownia AI nie zna stanow na plus, sztuka stracilaby wartosc; zostaja w druzynie jako zapas ludzi). Obita idzie obita (AiWear). Zwraca sztuki.
        /// sklad7b: g to kazda partia ze zbrojownia DTE - zaloga albo partia lorda (towarzysze, rod gracza, "Donate Troops", nowa partia rodu).
        /// sklad7b-p: przy TroopsFightWithOwnKitOnly takze kon i rzad jezdzca (kon jest sztuka zbrojowni, jak w MoveKits AI) - inaczej ten sam kon
        /// zostawal u Ciebie, a jezdziec u AI jechal na koniu pozyczonym ze wzorca (jeden kon z niczego).</summary>
        private static int KitsToGarrison(MobileParty g, Dictionary<CharacterObject, int> moved)
        {
            var armory = QuartermasterLaw.DteArmory();
            var main = MobileParty.MainParty;
            if (armory == null || main == null || QuartermasterEscrow.Active) return 0;
            var pool = new Dictionary<ItemObject, int>();
            var copies = new Dictionary<ItemObject, List<SwapMath.Piece>>();
            bool horses = GarrisonKit.OwnKitOn;   // sklad7b-p (uwagi 3 i 15): kon i rzad ida z jezdzcem (jak MoveKits AI); wylaczone - Stajnia, jak dotad
            foreach (var type in QuartermasterLaw.KitTypes)
            {
                if (!horses && (type == ItemObject.ItemTypeEnum.Horse || type == ItemObject.ItemTypeEnum.HorseHarness)) continue;   // konie i rzedy - Stajnia
                foreach (var p in QuartermasterLaw.KitPieces(armory, type, true))   // Own = Twoja ksiega na najgorszych egzemplarzach id
                {
                    var el = QuartermasterLaw.ElOf(p); var it = el.Item; var m = el.ItemModifier;
                    if (it == null || p.MenTotal <= 0 || !SupplyDemand.Equipmentish(it) || ArmsPricing.IsUnique(it)) continue;
                    if (m != null && m.PriceMultiplier > 1f) { _dScrPlus += p.MenTotal; continue; }
                    int v; pool.TryGetValue(it, out v); pool[it] = v + p.MenTotal;
                    List<SwapMath.Piece> l; if (!copies.TryGetValue(it, out l)) copies[it] = l = new List<SwapMath.Piece>(); l.Add(p);
                }
            }
            var taken = PlanKits(pool, moved, NeedByType(main.MemberRoster));   // roster druzyny PO ekranie
            int pcs = 0;
            foreach (var kv in taken)
            {
                List<SwapMath.Piece> l;
                if (kv.Value <= 0 || !copies.TryGetValue(kv.Key, out l)) continue;
                if (!AiGear.AddToArmory(g, kv.Key, kv.Value)) { _dScrRefused += kv.Value; continue; }   // DTE nie przyjal - zostaje w druzynie
                l.Sort((a, b) => SwapMath.WorseFirst(b, a));   // najlepsze egzemplarze ludzi najpierw (Twoja czesc to najgorsze)
                int left = kv.Value;
                foreach (var p in l)
                {
                    if (left <= 0) break;
                    int k = Math.Min(left, p.MenTotal);
                    if (k <= 0) continue;
                    var el = QuartermasterLaw.ElOf(p);
                    armory.AddToCounts(el, -k);
                    try { AiWear.NoteBought(g, el, k); } catch { }   // obita zostaje obita w zalodze
                    left -= k;
                }
                pcs += kv.Value - left;
            }
            return pcs;
        }

        /// <summary>sklad7b: takze partia lorda -> druzyna (st = null: bez patroli BK).
        /// Zaloga -> druzyna: komplety ludzi zabranych z zalogi (regula A7; sprzet ludzi na patrolach BK zostaje) do zbrojowni druzyny jako czesc
        /// LUDZI, obite ze stanem (udzial obitych jak AiWear.MoveWorn). Id, w ktorym masz czesc w ksiedze, zostaje w zalodze - ta sama regula co zakupy
        /// ludzi (dopisany egzemplarz przesunalby Twoja czesc na gorszy). Bez koni i rzedow (Stajnia) - sklad7b-p: przy TroopsFightWithOwnKitOnly
        /// jezdziec wraca z koniem i rzedem, jesli jego partia je ma. Zwraca sztuki.</summary>
        private static int KitsToMain(MobileParty g, Settlement st, Dictionary<CharacterObject, int> moved)
        {
            var armory = QuartermasterLaw.DteArmory();
            var arm = ArmoryOf(g);
            if (armory == null || arm == null || arm.Count == 0 || QuartermasterEscrow.Active) return 0;
            var needLeft = NeedByType(g.MemberRoster);   // roster zalogi PO ekranie
            AddExtra(needLeft, PatrolNeed(st));
            var pool = new Dictionary<ItemObject, int>();
            bool horses = GarrisonKit.OwnKitOn;   // sklad7b-p (uwagi 3 i 15): jezdziec wraca ze swoim koniem i rzedem, jesli jego partia je ma
            foreach (var kv in arm)
            {
                var it = kv.Key;
                if (it == null || kv.Value <= 0 || !SupplyDemand.Equipmentish(it) || ArmsPricing.IsUnique(it) || (!horses && MenPurse.HorseKind(it))) continue;
                if (ArmouryBehavior.StockOf(it.StringId) > 0) { _dScrBook++; continue; }
                pool[it] = kv.Value;
            }
            var taken = PlanKits(pool, moved, needLeft);
            int pcs = 0;
            bool synced = false;
            foreach (var kv in taken)
            {
                int c;
                if (kv.Value <= 0 || !arm.TryGetValue(kv.Key, out c) || c <= 0) continue;
                int n = Math.Min(kv.Value, c);
                var worn = AiWear.TakeWornShare(g, kv.Key, n, c, !synced);   // PRZED zdjeciem ze slownika (jak MoveWorn)
                synced = true;
                int w = 0;
                foreach (var x in worn) { if (x.Value <= 0 || w + x.Value > n) continue; armory.AddToCounts(new EquipmentElement(kv.Key, x.Key), x.Value); w += x.Value; }
                if (n - w > 0) armory.AddToCounts(new EquipmentElement(kv.Key), n - w);
                if (c - n > 0) arm[kv.Key] = c - n; else arm.Remove(kv.Key);
                pcs += n;
            }
            return pcs;
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
            int garrisons = 0, pcs = 0, badId = 0;   // poprawki sklad7: id ze znakami separatorow zapisu (',', '~', '>') - pomijane i liczone (jak K1c Sep)
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null || !st.IsFortification || st.Town == null || st.Town.GarrisonParty == null) continue;
                    Dictionary<ItemObject, int> arm;
                    if (!dict.TryGetValue(st.Town.GarrisonParty.Id, out arm) || arm == null || arm.Count == 0) continue;
                    if (BadId(st.StringId)) { foreach (var v in arm.Values) if (v > 0) badId += v; continue; }
                    bool first = true;
                    foreach (var kv in arm)
                    {
                        if (kv.Key == null || kv.Value <= 0 || kv.Key.StringId == null) continue;
                        if (BadId(kv.Key.StringId)) { badId += kv.Value; continue; }
                        sb.Append(first ? st.StringId + ">" : ",").Append(kv.Key.StringId).Append(':').Append(kv.Value);
                        first = false; pcs += kv.Value;
                    }
                    if (!first) { sb.Append('~'); garrisons++; }
                }
                catch (Exception e) { Stumble("Export", e); }
            }
            // recenzja 171: partie lordow, ktorych DTE nie zapisuje (bez wodza, rozwiazywane - ShouldPersistParty), a ktore zyja dalej (czekaja na
            // rozwiazanie albo na nowego wodza) - rekord "@StringId partii>..."; bez tego ich sprzet ginal przy wczytaniu jak zbrojownie zalog
            int parties = 0, ppcs = 0;
            foreach (var mp in MobileParty.AllLordParties)
            {
                try
                {
                    if (mp == null || !mp.IsActive || mp.IsMainParty || mp.StringId == null || DteSaves(mp)) continue;
                    Dictionary<ItemObject, int> arm;
                    if (!dict.TryGetValue(mp.Id, out arm) || arm == null || arm.Count == 0) continue;
                    if (BadId(mp.StringId)) { foreach (var v in arm.Values) if (v > 0) badId += v; continue; }
                    bool first = true;
                    foreach (var kv in arm)
                    {
                        if (kv.Key == null || kv.Value <= 0 || kv.Key.StringId == null) continue;
                        if (BadId(kv.Key.StringId)) { badId += kv.Value; continue; }
                        sb.Append(first ? "@" + mp.StringId + ">" : ",").Append(kv.Key.StringId).Append(':').Append(kv.Value);
                        first = false; ppcs += kv.Value;
                    }
                    if (!first) { sb.Append('~'); parties++; }
                }
                catch (Exception e) { Stumble("Export(partia)", e); }
            }
            Log.Info("Zbrojownie zalog (171): zapis - " + garrisons + " zalog, " + pcs + " szt.; partie lordow bez wodza albo rozwiazywane (DTE ich nie zapisuje) " + parties + ", " + ppcs + " szt."
                     + "; pominiete (id ze znakami separatorow zapisu) " + badId + " szt.");
            return sb.ToString();
        }

        private static readonly char[] SepChars = { ',', '~', '>' };
        private static bool BadId(string id) { return string.IsNullOrEmpty(id) || id.IndexOfAny(SepChars) >= 0; }

        /// <summary>
        /// Poprawki sklad7: przedmiot z zapisu - po StringId, a gdy go nie ma - wyrob kowala po kodzie wzoru (ta sama droga co DTE
        /// ArmyArmory.ResolveArmoryItem i dawny K1c RestoreArmories). Od K1 B6 gracz moze oddac zalodze wlasny wyrob kowala.
        /// </summary>
        private static ItemObject ResolveItem(MBObjectManager om, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            ItemObject it = null;
            try { it = om.GetObject<ItemObject>(id); } catch { }
            if (it == null) try { it = ItemObject.GetCraftedItemObjectFromHashedCode(id); } catch { }
            return it;
        }

        /// <summary>Warunek DTE ShouldPersistParty (EveryoneCampaignBehavior): partia z zywym, czynnym wodzem-bohaterem (nie graczem), wlascicielem, ludzmi i nie rozwiazywana.</summary>
        private static bool DteSaves(MobileParty mp)
        {
            var l = mp.LeaderHero;
            if (l == null || l.CharacterObject == null || !l.CharacterObject.IsHero || l.CharacterObject.IsPlayerCharacter || l.IsHumanPlayerCharacter || !l.IsPartyLeader || !l.IsAlive || !l.IsActive) return false;
            var o = mp.Owner;
            if (o == null || o.CharacterObject == null || !o.CharacterObject.IsHero || o.CharacterObject.IsPlayerCharacter || o.IsHumanPlayerCharacter || !o.IsActive || !o.IsAlive) return false;
            var r = mp.MemberRoster;
            return r != null && r.TotalHeroes > 0 && r.TotalManCount > 0 && !mp.IsDisbanding;
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
            if (p != null && !p.StartsWith("v1|", StringComparison.Ordinal) && p.IndexOf('>') < 0 && p.IndexOf(';') > 0) { LegacyK1c(p, dict, why); return; }   // sklad7: zapis z DLL probnej K1c
            if (p == null || !p.StartsWith("v1|", StringComparison.Ordinal)) { OldSave(why); return; }
            if (s == null || !s.GarrisonArmorySurvivesSave) { Log.Info("Zbrojownie zalog (171): zapis ma zbrojownie zalog, ale Garrison Armory Survives Save wylaczone - nie ustawiam (" + why + ")."); return; }
            var om = MBObjectManager.Instance;
            int garrisons = 0, pcs = 0, noGarrison = 0, replaced = 0, unknown = 0, parties = 0, ppcs = 0, noParty = 0;
            Dictionary<string, MobileParty> lordParties = null;   // recenzja 171: rekordy "@partia" (bez wodza / rozwiazywane) - mapa raz, tylko gdy sa
            foreach (var rec in p.Substring(3).Split('~'))
            {
                if (rec.Length == 0) continue;
                try
                {
                    int gt = rec.IndexOf('>'); if (gt <= 0) continue;
                    bool party = rec[0] == '@';
                    MobileParty g = null;
                    if (party)
                    {
                        if (lordParties == null)
                        {
                            lordParties = new Dictionary<string, MobileParty>();
                            foreach (var lp in MobileParty.AllLordParties) if (lp != null && lp.StringId != null && !lordParties.ContainsKey(lp.StringId)) lordParties[lp.StringId] = lp;
                        }
                        lordParties.TryGetValue(rec.Substring(1, gt - 1), out g);
                        if (g == null || !g.IsActive) { noParty++; continue; }
                    }
                    else
                    {
                        Settlement st = null; try { st = om.GetObject<Settlement>(rec.Substring(0, gt)); } catch { }
                        g = st != null && st.Town != null ? st.Town.GarrisonParty : null;
                        if (g == null) { noGarrison++; continue; }
                    }
                    Dictionary<ItemObject, int> arm;
                    if (dict.TryGetValue(g.Id, out arm) && arm != null && arm.Count > 0) { foreach (var v in arm.Values) if (v > 0) replaced += v; arm.Clear(); }
                    int got = 0;
                    foreach (var tok in rec.Substring(gt + 1).Split(','))
                    {
                        int c = tok.LastIndexOf(':'); if (c <= 0) continue;
                        int n; if (!int.TryParse(tok.Substring(c + 1), out n) || n <= 0) continue;
                        var it = ResolveItem(om, tok.Substring(0, c));
                        if (it == null) { unknown += n; continue; }
                        if (AiGear.AddToArmory(g, it, n)) got += n;
                    }
                    if (got > 0) { if (party) { parties++; ppcs += got; } else { garrisons++; pcs += got; } }
                }
                catch (Exception e) { Stumble("Restore", e); }
            }
            Log.Info("Zbrojownie zalog (171): po wczytaniu przywrocone " + garrisons + " zalog, " + pcs + " szt. (bez zalogi " + noGarrison + ", zastapione z DTE " + replaced
                     + " szt., nieznane przedmioty " + unknown + "); partie lordow bez wodza albo rozwiazywane " + parties + ", " + ppcs + " szt. (partii juz nie ma " + noParty + ").");
        }

        /// <summary>
        /// C9a: zapis sprzed 171 - zalogi raz uzupelnione do wzorca swoich ludzi (ta sama regula co ColdStart - decyzja Jeffa 7 "dorobek startowy zostaje").
        /// sklad7 (scalenie K1): TAKZE zalogi gracza. Od K1 (Jeff 09.10 B, GarrisonFightsWithArmoryOnly) zaloga w bitwie walczy tylko tym, co ma w zbrojowni,
        /// a w zapisie sprzed tej wersji zbrojownie wszystkich zalog sa puste (DTE ich nie zapisywal; zalogi gracza nic nie kupowaly) - bez dorobku zalogi
        /// Jeffa bronilyby sie nago przy pierwszym szturmie, ktory toczy osobiscie (dotad walczyly w pelnym wzorcu za darmo). Jedna regula gracz/AI.
        /// Inni - bez zmian (nie kupuja sprzetu).
        /// </summary>
        private static void OldSave(string why)
        {
            var s = Settings.Current;
            if (s == null || !s.GarrisonArmoryRestoreOldSave)
            {
                Log.Info("Zbrojownie zalog (171): stary zapis - brak klucza, odtworzenie wylaczone w MCM; zalogi bez zbrojowni do pierwszego zapisu z 171.");
                return;
            }
            int garrisons = 0, pcs = 0, seen = 0, mine = 0, minePcs = 0;
            foreach (var st in Settlement.All)
            {
                try
                {
                    if (st == null || !st.IsFortification || st.Town == null || st.Town.GarrisonParty == null) continue;
                    var g = st.Town.GarrisonParty;
                    if (Undead.Party(g)) continue;
                    bool player = st.OwnerClan == Clan.PlayerClan;
                    seen++;
                    int n = ColdStart.FillToTemplate(g);
                    if (n > 0) { garrisons++; pcs += n; if (player) { mine++; minePcs += n; } }
                }
                catch (Exception e) { Stumble("OldSave", e); }
            }
            Log.Info("Zbrojownie zalog (171): stary zapis bez zbrojowni zalog (" + why + ") - jednorazowy dorobek startowy (regula ColdStart, wzorce ich ludzi): " + garrisons + " zalog z " + seen
                     + ", " + pcs + " szt.; w tym zalogi gracza " + mine + ", " + minePcs + " szt. (sklad7: zaloga walczy tylko tym, co ma - bez dorobku bronilaby sie nago).");
            // poprawki sklad7: swiadome rozszerzenie decyzji 7 (dorobek startowy) na trwajaca kampanie - Jeff ma to zobaczyc w grze, nie tylko w logu
            if (mine > 0)
                Log.Player("Your " + mine + " garrisons received the kit of their men once (" + minePcs + " pieces), like the stores of a new campaign - from now on a garrison fights only with what its stores hold.");
        }

        /// <summary>sklad7: zapis z DLL probnej K1c (ten sam klucz, format "osada,przedmiot,ile;") - te zbrojownie odtwarzamy jak v1 (zastepujac to, co dal DTE).</summary>
        private static void LegacyK1c(string p, Dictionary<MBGUID, Dictionary<ItemObject, int>> dict, string why)
        {
            var om = MBObjectManager.Instance;
            var done = new HashSet<MobileParty>();
            int garrisons = 0, pcs = 0, noGarrison = 0, replaced = 0, unknown = 0;
            foreach (var rec in p.Split(';'))
            {
                try
                {
                    var a = rec.Split(','); int n;
                    if (a.Length != 3 || a[0].Length == 0 || a[1].Length == 0 || !int.TryParse(a[2], out n) || n <= 0) continue;
                    Settlement st = null; try { st = om.GetObject<Settlement>(a[0]); } catch { }
                    var g = st != null && st.Town != null ? st.Town.GarrisonParty : null;
                    if (g == null) { noGarrison++; continue; }
                    if (done.Add(g))
                    {
                        Dictionary<ItemObject, int> arm;
                        if (dict.TryGetValue(g.Id, out arm) && arm != null && arm.Count > 0) { foreach (var v in arm.Values) if (v > 0) replaced += v; arm.Clear(); }
                        garrisons++;
                    }
                    var it = ResolveItem(om, a[1]);
                    if (it == null) { unknown += n; continue; }
                    if (AiGear.AddToArmory(g, it, n)) pcs += n;
                }
                catch (Exception e) { Stumble("LegacyK1c", e); }
            }
            Log.Info("Zbrojownie zalog (171): zapis w dawnym formacie K1c (" + why + ") - przywrocone " + garrisons + " zalog, " + pcs + " szt. (bez zalogi " + noGarrison
                     + ", zastapione z DTE " + replaced + " szt., nieznane przedmioty " + unknown + ").");
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
            try
            {
                // poprawki sklad7: ekran druzyny gracza ("Manage garrison", "Donate troops") - komplety z ludzmi jak w sciezkach AI
                if (!_screenHooked)
                {
                    _fInitial = AccessTools.Field(typeof(PartyScreenLogic), "_initialData");
                    var m = AccessTools.Method(typeof(PartyScreenLogic), "DoneLogic", new[] { typeof(bool) });
                    if (m != null && _fInitial != null)
                    {
                        h.Patch(m, prefix: new HarmonyMethod(typeof(GarrisonArmory), nameof(ScreenDonePrefix)), postfix: new HarmonyMethod(typeof(GarrisonArmory), nameof(ScreenDonePostfix)));
                        _screenHooked = true;
                    }
                }
            }
            catch (Exception e) { Log.Error("GarrisonArmory.ApplyAll(Screen)", e); }
            try
            {
                // sklad7b: nowa partia rodu - ludzie przechodza w funkcji zamkniecia ekranu (po DoneLogic)
                if (!_createHooked)
                {
                    var m = AccessTools.Method(typeof(Helpers.PartyScreenHelper), "OpenScreenAsCreateClanPartyForHeroPartyScreenClosed");
                    if (m != null)
                    {
                        h.Patch(m, prefix: new HarmonyMethod(typeof(GarrisonArmory), nameof(CreatePartyPrefix)), postfix: new HarmonyMethod(typeof(GarrisonArmory), nameof(CreatePartyPostfix)));
                        _createHooked = true;
                    }
                }
            }
            catch (Exception e) { Log.Error("GarrisonArmory.ApplyAll(CreateParty)", e); }
            try { _bkPatrolType = AccessTools.TypeByName("BannerKings.Components.GarrisonPartyComponent"); } catch { _bkPatrolType = null; }
            Log.Info("GarrisonArmory: komplet z ludzmi - zostawienie " + (_leaveHooked ? "wpiete" : "BRAK") + ", zabranie " + (_takeHooked ? "wpiete" : "BRAK")
                     + ", rozwiazanie partii " + (_disbandHooked ? "wpiete" : "BRAK") + ", ekran druzyny gracza (poprawki sklad7; sklad7b: takze partie towarzyszy, rodu i \"Donate Troops\") "
                     + (_screenHooked ? "wpiety" : "BRAK") + ", nowa partia rodu (sklad7b) " + (_createHooked ? "wpieta" : "BRAK")
                     + "; patrole BK: typ " + (_bkPatrolType != null ? "znaleziony" : "brak") + ".");
        }
    }
}
