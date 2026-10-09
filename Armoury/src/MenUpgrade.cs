using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// K1-A - DOZBRAJANIE ZA SWOJE (Jeff 09.10: "oni dostaja zold i biora lupy, za ktore kupuja sprzet i sie lepiej dozbrajaja";
    /// 05.10: "wojsko sklada lepszy sprzet ... wydaje na ... polepszenie sprzetu"). docs/paczki/K1-dozbrajanie.md A4-A8.
    ///  - Kto: ludzie gracza, ludzie lordow AI (w MIESCIE, przy wjezdzie i raz na dobe postoju) i zalogi (raz na dobe, targ swojej
    ///    osady; zamek - najpierw polka zamku, potem najblizsze miasto handlowe).
    ///  - Co: sztuki ludzi ida do koszykow typ x tier wzorca (SwapMath.Assign); w PELNYM koszyku (najpierw braki) najslabsza sztuka
    ///    moze ustapic lepszej z polki: ten sam typ i klasa broni, tier nie wyzszy niz tier koszyka, sila >= stara x (1 + prog) albo
    ///    wyzszy tier, wymog spelniony, z siodla dla jezdzcow, bez unikatow, sprawna. Wybor: najwiecej sily za denara netto.
    ///  - Za ile: cena polki, placi sakiewka ludzi (bez rezerwy na zalegle naprawy) do kasy miasta; lord i pan zalogi NIE doplacaja.
    ///    Stara sztuka od razu do kupca po cenie skupu w tym stanie (nie wiecej niz ma kasa) - pieniadze do sakiewki, sztuka na polke;
    ///    kasa pusta - zostaje w zbrojowni jako zapas (pojdzie z nadwyzkami).
    /// </summary>
    internal static class MenUpgrade
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.MenUpgradeGear && MenPurse.On; } }
        internal static bool GarrisonPurseOn { get { var s = Settings.Current; return s != null && s.GarrisonPurseEnabled && MenPurse.On; } }

        private static readonly Dictionary<string, int> _lastDay = new Dictionary<string, int>();
        // liczniki doby (linia "Dozbrajanie: dzien")
        private static int _dStamp = -1, _dLogged;
        private static int _dPlayerN, _dLordN, _dGarN, _dSold, _dKept, _dNoBetter, _dNoMoney, _dNoLift, _dGarWageN, _dGarGapN, _dBattleArmory, _dBattleTemplate, _dGarEmptyN;
        private static long _dPlayerGold, _dLordGold, _dGarGold, _dSoldGold, _dSaved, _dOverCap, _dGarWage, _dGarGapGold, _dGarEmptyGold;

        internal static void Reset()
        {
            _lastDay.Clear(); _dStamp = -1; ClearDay();
        }

        private static void ClearDay()
        {
            _dLogged = 0;
            _dPlayerN = _dLordN = _dGarN = _dSold = _dKept = _dNoBetter = _dNoMoney = _dNoLift = _dGarWageN = _dGarGapN = _dBattleArmory = _dBattleTemplate = _dGarEmptyN = 0;
            _dPlayerGold = _dLordGold = _dGarGold = _dSoldGold = _dSaved = _dOverCap = _dGarWage = _dGarGapGold = _dGarEmptyGold = 0;
        }

        // ------------------------------------------------------------ liczniki z innych miejsc (tylko log)
        internal static void NoteSaved(int saved, int overCap) { Touch(); _dSaved += Math.Max(0, saved); _dOverCap += Math.Max(0, overCap); }
        internal static void NoteGarrisonWage(int amount) { if (amount <= 0) return; Touch(); _dGarWage += amount; _dGarWageN++; }
        internal static void NoteGarrisonGap(int fromPurse) { if (fromPurse <= 0) return; Touch(); _dGarGapGold += fromPurse; _dGarGapN++; }
        internal static void NoteGarrisonEmpty(int purse) { if (purse <= 0) return; Touch(); _dGarEmptyGold += purse; _dGarEmptyN++; }
        internal static void NoteBattle(bool fromArmory) { Touch(); if (fromArmory) _dBattleArmory++; else _dBattleTemplate++; }

        /// <summary>Nowa doba: linia poprzedniej. Wolane przy kazdym liczniku i z DailyTickEvent (linia codziennie).</summary>
        internal static void Touch()
        {
            try
            {
                int d = (int)CampaignTime.Now.ToDays;
                if (_dStamp == d) return;
                if (_dStamp >= 0)
                {
                    long garPurses = 0; int garN = 0;
                    try
                    {
                        foreach (var st in Settlement.All)
                        {
                            if (st == null || st.Town == null) continue;
                            var g = st.Town.GarrisonParty;
                            if (g == null) continue;
                            int v = MenPurse.Get(g);
                            if (v > 0) { garPurses += v; garN++; }
                        }
                    }
                    catch { }
                    Log.Info("Dozbrajanie: dzien " + _dStamp + " - gracz " + _dPlayerN + "/" + _dPlayerGold + ", lordowie " + _dLordN + "/" + _dLordGold
                             + ", zalogi " + _dGarN + "/" + _dGarGold + " (szt./zloto); stare sprzedane " + _dSold + " za " + _dSoldGold + " (do zbrojowni " + _dKept + ")"
                             + "; pominiete koszyki: brak lepszej na polce " + _dNoBetter + ", za malo w sakiewce " + _dNoMoney + ", nikt nie udzwignie " + _dNoLift
                             + "; odlozone przy wyjazdach " + _dSaved + ", ponad limit na zycie " + _dOverCap
                             + "; zold zalog do sakiewek " + _dGarWage + " (" + _dGarWageN + " zalog), w sakiewkach zalog " + garPurses + " (" + garN + " zalog)"
                             + "; braki zalog z ich sakiewek " + _dGarGapGold + " (" + _dGarGapN + " zakupow), sakiewki pustych zalog do kas osad " + _dGarEmptyGold + " (" + _dGarEmptyN + ")"
                             + "; zalogi w bitwie ze zbrojowni " + _dBattleArmory + " / we wzorcu " + _dBattleTemplate + ".");
                }
                ClearDay();
                _dStamp = d;
            }
            catch (Exception e) { Log.Error("MenUpgrade.Touch", e); }
        }

        private static bool Gate(MobileParty mp)
        {
            int day = (int)CampaignTime.Now.ToDays, last;
            string k = mp.StringId ?? "";
            if (_lastDay.TryGetValue(k, out last) && last == day) return false;
            _lastDay[k] = day;
            return true;
        }

        internal static void Forget(MobileParty mp) { try { if (mp != null && mp.StringId != null) _lastDay.Remove(mp.StringId); } catch { } }

        // ------------------------------------------------------------ wejscia
        /// <summary>Druzyna gracza w miescie (wjazd albo doba postoju) - po nadwyzkach, naprawach i brakach (A5).</summary>
        internal static void ForPlayer(Settlement st)
        {
            try
            {
                if (!On || st == null || !st.IsTown || st.Town == null) return;
                var main = MobileParty.MainParty;
                var armory = QuartermasterLaw.DteArmory();
                if (main == null || armory == null || QuartermasterEscrow.Active) return;
                if (!Gate(main)) return;
                Touch();
                int purse0 = MenPurse.Get(main);
                int budget = purse0 - TroopSelfMend.OutstandingCost(st);   // naprawy maja pierwszenstwo (wpis 84)
                if (budget <= 0) return;
                var v = Run(main, new PlayerKit(armory), st, null, st, budget, true);
                if (v.N <= 0) return;
                _dPlayerN += v.N; _dPlayerGold += v.Gold;
                Log.Player("Your men bought " + v.N + " better pieces with their own coin for " + v.Gold + " denars (" + string.Join(", ", v.Names.ToArray())
                           + (v.N > v.Names.Count ? ", ..." : "") + "); the old ones fetched " + v.SoldGold + "." + (v.Kept > 0 ? " " + v.Kept + " old pieces stay in the stores - the merchants had no coin." : ""));
                LogVisit("gracz", st, v, purse0, MenPurse.Get(main));
            }
            catch (Exception e) { Log.Error("MenUpgrade.ForPlayer", e); }
        }

        /// <summary>Lord AI w miescie albo zaloga w swojej osadzie (AiGear.TryBuy po brakach).</summary>
        internal static void ForAi(MobileParty mp, Settlement st)
        {
            try
            {
                if (!On || mp == null || st == null || st.Town == null || mp.IsMainParty || !mp.IsActive || mp.MapEvent != null) return;
                bool garrison = mp.IsGarrison && mp.CurrentSettlement == st;
                if (garrison) { if (!GarrisonPurseOn) return; }
                else if (!mp.IsLordParty || !st.IsTown || mp.LeaderHero == null || !mp.LeaderHero.IsAlive) return;
                if (FactionManager.IsAtWarAgainstFaction(mp.MapFaction, st.MapFaction)) return;
                var all = AiGear.Armories();
                Dictionary<ItemObject, int> arm;
                if (all == null || !all.TryGetValue(mp.Id, out arm) || arm == null || arm.Count == 0) return;
                int purse0 = MenPurse.Get(mp);
                if (purse0 <= 0) return;
                if (!Gate(mp)) return;
                Touch();
                int budget = purse0 - AiWear.OutstandingCost(mp, st.IsTown ? st : null);
                if (budget <= 0) return;
                Settlement shop1 = st, shop2 = null, sellTo = st;
                if (garrison && st.IsCastle)
                {
                    // A4: zamek - najpierw polka zamku, potem najblizsze miasto handlowe (ta sama regula co odziez wojska, ArmyClothing.MarketTown)
                    var market = ArmyClothing.MarketTown(st);
                    if (market != null && (market.IsUnderSiege || FactionManager.IsAtWarAgainstFaction(mp.MapFaction, market.MapFaction))) market = null;
                    shop2 = market; sellTo = market;
                }
                if (st.IsUnderSiege) return;
                var v = Run(mp, new AiKit(mp, arm, garrison), shop1, shop2, sellTo, budget, false);
                if (v.N <= 0) return;
                if (garrison) { _dGarN += v.N; _dGarGold += v.Gold; } else { _dLordN += v.N; _dLordGold += v.Gold; }
                var s = Settings.Current;
                if (_dLogged < Math.Max(0, s.AiGearLogPerDay)) { _dLogged++; LogVisit(garrison ? "zaloga " + st.Name : (mp.LeaderHero != null ? mp.LeaderHero.Name.ToString() : mp.StringId), st, v, purse0, MenPurse.Get(mp)); }
            }
            catch (Exception e) { Log.Error("MenUpgrade.ForAi", e); }
        }

        private static void LogVisit(string who, Settlement st, Visit v, int purse0, int purse1)
        {
            Log.Info("Dozbrajanie: " + who + " w " + st.Name + " - lepsze " + v.N + " szt. za " + v.Gold + " (" + string.Join("; ", v.Lines.ToArray())
                     + (v.N > v.Lines.Count ? "; ..." : "") + "), stare sprzedane " + v.Sold + " za " + v.SoldGold + ", stare do zbrojowni " + v.Kept
                     + (v.Kept > 0 ? " (kasa miasta pusta)" : "") + "; sakiewka " + purse0 + " -> " + purse1 + ".");
        }

        // ------------------------------------------------------------ zbrojownie (gracz: roster DTE z ksiega; AI: slownik DTE + AiWear)
        private interface IKit
        {
            List<SwapMath.Piece> Pieces(ItemObject.ItemTypeEnum type);
            void Add(EquipmentElement el);
            bool TakeOld(SwapMath.Piece p, out EquipmentElement el);
            void PutBackOld(SwapMath.Piece p, EquipmentElement el);
        }

        private sealed class PlayerKit : IKit
        {
            private readonly ItemRoster _a;
            internal PlayerKit(ItemRoster a) { _a = a; }
            // sztuki LUDZI: czesc gracza (ksiega, najgorsze egzemplarze) wypada przez MenTotal - nigdy nie idzie do kupca
            public List<SwapMath.Piece> Pieces(ItemObject.ItemTypeEnum type) { return QuartermasterLaw.KitPieces(_a, type, true); }
            public void Add(EquipmentElement el) { _a.AddToCounts(el, 1); }
            public bool TakeOld(SwapMath.Piece p, out EquipmentElement el)
            {
                el = QuartermasterLaw.ElOf(p);
                if (el.Item == null || p.MenTotal <= 0) return false;
                int i = _a.FindIndexOfElement(el);
                if (i < 0 || _a.GetElementNumber(i) <= 0) return false;
                _a.AddToCounts(el, -1); p.Total--;
                return true;
            }
            public void PutBackOld(SwapMath.Piece p, EquipmentElement el) { _a.AddToCounts(el, 1); p.Total++; }
        }

        private sealed class AiKit : IKit
        {
            private readonly MobileParty _mp; private readonly Dictionary<ItemObject, int> _arm; private readonly bool _garrison;
            internal AiKit(MobileParty mp, Dictionary<ItemObject, int> arm, bool garrison) { _mp = mp; _arm = arm; _garrison = garrison; }
            public List<SwapMath.Piece> Pieces(ItemObject.ItemTypeEnum type) { return AiPieces(_arm, type); }
            public void Add(EquipmentElement el)
            {
                AiGear.AddToArmory(_mp, el.Item, 1);
                if (!_garrison) AiWear.NoteBought(_mp, el, 1);   // jak AiGear: sprawna sztuka z polki
            }
            public bool TakeOld(SwapMath.Piece p, out EquipmentElement el)
            {
                el = default(EquipmentElement);
                var it = QuartermasterLaw.ElOf(p).Item;
                int cnt;
                if (it == null || !_arm.TryGetValue(it, out cnt) || cnt <= 0) return false;
                var mod = AiWear.TakeCondition(_mp, it);       // do kupca idzie najgorsza obita (wpis 85)
                if (cnt > 1) _arm[it] = cnt - 1; else _arm.Remove(it);
                el = new EquipmentElement(it, mod);
                p.Total--;
                return true;
            }
            public void PutBackOld(SwapMath.Piece p, EquipmentElement el)
            {
                int cnt; _arm.TryGetValue(el.Item, out cnt); _arm[el.Item] = cnt + 1;
                AiWear.PutBack(_mp, el.Item, el.ItemModifier);
                p.Total++;
            }
        }

        /// <summary>Sztuki zbrojowni AI (slownik DTE, bez stanu - stan prowadzi AiWear) jako egzemplarze SwapMath; bez koni i rzedow.</summary>
        internal static List<SwapMath.Piece> AiPieces(Dictionary<ItemObject, int> arm, ItemObject.ItemTypeEnum type)
        {
            var list = new List<SwapMath.Piece>();
            if (arm == null) return list;
            foreach (var kv in arm)
            {
                var it = kv.Key;
                if (it == null || kv.Value <= 0 || it.ItemType != type || !SupplyDemand.Equipmentish(it) || MenPurse.HorseKind(it)) continue;
                list.Add(QuartermasterLaw.PieceOf(new EquipmentElement(it), kv.Value));
            }
            return list;
        }

        // ------------------------------------------------------------ koszyki typ x tier wzorca (A7)
        /// <summary>Koszyki typu: kazdy slot wzorca (bron 0-3, pancerz, plaszcz) z tym typem - tier sztuki wzorca; w koszyku oddzialy
        /// (grupy) od najwyzszego skilla; Mounted - ktos z koszyka jezdzi.</summary>
        internal static List<SwapMath.Bucket> Buckets(TroopRoster roster, ItemObject.ItemTypeEnum type, out List<CharacterObject> troops)
        {
            troops = new List<CharacterObject>();
            var byTier = new Dictionary<int, SwapMath.Bucket>();
            var idx = new Dictionary<CharacterObject, int>();
            try
            {
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    var c = el.Character;
                    if (c == null || c.IsHero || el.Number <= 0) continue;
                    Equipment eq = null;
                    try { eq = c.Equipment; } catch { }
                    if (eq == null) continue;
                    for (int sl = 0; sl < 10; sl++)
                    {
                        var it = eq[(EquipmentIndex)sl].Item;
                        if (it == null || it.ItemType != type) continue;
                        int g;
                        if (!idx.TryGetValue(c, out g)) { g = troops.Count; troops.Add(c); idx[c] = g; }
                        int t = AiGear.TierOf(it);
                        SwapMath.Bucket b;
                        if (!byTier.TryGetValue(t, out b)) byTier[t] = b = new SwapMath.Bucket { Tier = t };
                        b.Men.Add(new KeyValuePair<int, int>(g, el.Number));
                        b.Size += el.Number;
                        if (c.IsMounted) b.Mounted = true;
                    }
                }
            }
            catch (Exception e) { Log.Error("MenUpgrade.Buckets", e); }
            var skill = QuartermasterLaw.SkillOfType(type);
            var tr = troops;
            var list = new List<SwapMath.Bucket>(byTier.Values);
            foreach (var b in list)
                b.Men.Sort((a, c) => { int d = tr[c.Key].GetSkillValue(skill).CompareTo(tr[a.Key].GetSkillValue(skill)); return d != 0 ? d : a.Key.CompareTo(c.Key); });
            return list;
        }

        // ------------------------------------------------------------ silnik
        private sealed class Cand { public ItemObject.ItemTypeEnum Type; public int Order; public SwapMath.Slot Slot; public CharacterObject Troop; }
        private sealed class Ware { public Settlement Shop; public EquipmentElement El; public int Price, Left, Tier; public long Power; public bool HasCls; public WeaponClass Cls; }
        private sealed class Visit { public int N, Gold, Sold, SoldGold, Kept; public List<string> Lines = new List<string>(); public List<string> Names = new List<string>(); }

        private static Visit Run(MobileParty mp, IKit kit, Settlement shop1, Settlement shop2, Settlement sellTo, int budget, bool player)
        {
            var v = new Visit();
            var s = Settings.Current;
            int max = Math.Max(0, s.MenUpgradeMaxPerVisit);
            if (max == 0 || budget <= 0 || mp.MemberRoster == null) return v;
            double gain = Math.Max(0f, s.MenUpgradeMinGainPercent) / 100.0;
            var cands = new List<Cand>();
            for (int ti = 0; ti < AiGear.Order.Length; ti++)
            {
                var type = AiGear.Order[ti];
                List<CharacterObject> troops;
                var buckets = Buckets(mp.MemberRoster, type, out troops);
                if (buckets.Count == 0) continue;
                var pieces = kit.Pieces(type);
                if (pieces.Count == 0) continue;
                // najpierw braki (Jeff 14.09): tylko PELNE koszyki maja kandydata do wymiany
                foreach (var sl in SwapMath.Assign(buckets, pieces, QuartermasterLaw.MeetsOf(troops)))
                    if (sl.Bucket.Full) cands.Add(new Cand { Type = type, Order = ti, Slot = sl, Troop = troops[sl.Group] });
            }
            if (cands.Count == 0) return v;
            // najpierw koszyki z najwieksza roznica "tier koszyka - tier sztuki", potem typy (korpus, bron, tarcza, helm, ...), najslabsze najpierw
            cands.Sort((a, b) =>
            {
                int d = (b.Slot.Bucket.Tier - b.Slot.Piece.Tier).CompareTo(a.Slot.Bucket.Tier - a.Slot.Piece.Tier);
                if (d != 0) return d;
                d = a.Order.CompareTo(b.Order);
                return d != 0 ? d : SwapMath.WorseFirst(a.Slot.Piece, b.Slot.Piece);
            });
            var wares = new Dictionary<string, List<Ware>>();   // (typ, polka) -> towar; polke przegladamy raz na typ
            foreach (var c in cands)
            {
                if (v.N >= max || budget <= 0) break;
                var oldP = c.Slot.Piece;
                var oldEl = QuartermasterLaw.ElOf(oldP);
                if (oldEl.Item == null) continue;
                int oldSell = sellTo != null ? MenPurse.SellPrice(oldEl, sellTo, mp) : 0;
                for (int k = 0; k < c.Slot.N && v.N < max; k++)
                {
                    if (oldP.MenTotal <= 0) break;
                    int why;
                    var w = Pick(c, oldP, oldEl, oldSell, gain, budget, wares, mp, shop1, shop2, player, out why);
                    if (w == null)
                    {
                        if (k == 0) { if (why == 1) _dNoLift++; else if (why == 2) _dNoMoney++; else _dNoBetter++; }
                        break;
                    }
                    int price = w.Price;
                    w.Shop.ItemRoster.AddToCounts(w.El, -1); w.Left--;
                    kit.Add(w.El);
                    int paid = MenPurse.Take(mp, price);
                    budget -= price;
                    w.Shop.Town.ChangeGold(paid);
                    if (player) MoneyLedger.Note169(MoneyLedger.N169Kit, w.Shop, paid);   // jak braki gracza (paczka 169: linia kas)
                    else MoneyLedger.Note(MoneyLedger.NGear, w.Shop, paid);               // jak zakupy AiGear (ksiega przeplywow osad)
                    v.N++; v.Gold += paid;
                    try { w.Price = w.Shop.Town.MarketData.GetPrice(w.El, mp, false, w.Shop.Party); } catch { }
                    string ln = c.Type + " t" + oldP.Tier + "->t" + w.Tier + " " + oldP.Id + "->" + (w.El.Item.StringId ?? "?") + " " + paid;
                    if (v.Lines.Count < 6) v.Lines.Add(ln);
                    if (v.Names.Count < 3) v.Names.Add(w.El.Item.Name + " for " + oldEl.Item.Name);
                    // stara sztuka od razu do kupca (cena skupu w tym stanie, nie wiecej niz ma kasa); kasa pusta - zostaje jako zapas
                    EquipmentElement soldEl;
                    if (sellTo == null || sellTo.Town == null || !kit.TakeOld(oldP, out soldEl)) { v.Kept++; _dKept++; continue; }
                    int unit = MenPurse.SellPrice(soldEl, sellTo, mp);
                    if (!SwapMath.MerchantPays(sellTo.Town.Gold, unit)) { kit.PutBackOld(oldP, soldEl); v.Kept++; _dKept++; continue; }
                    sellTo.ItemRoster.AddToCounts(soldEl, 1);
                    sellTo.Town.ChangeGold(-unit);
                    MoneyLedger.Note169(MoneyLedger.N169Surplus, sellTo, -unit);   // paczka 169: linia kas (tylko licznik)
                    MenPurse.Add(mp, unit);
                    budget += unit;
                    v.Sold++; v.SoldGold += unit; _dSold++; _dSoldGold += unit;
                    SellByCondition.NoteSale(SellByCondition.Men, soldEl, 1, unit);   // ksiega skupu sprzetu (tylko log)
                }
            }
            return v;
        }

        /// <summary>Najlepsza sztuka z polki dla tego slotu: najpierw shop1 (zamek: polka zamku), potem shop2. why: 0 brak lepszej,
        /// 1 nikt nie udzwignie, 2 za malo w sakiewce.</summary>
        private static Ware Pick(Cand c, SwapMath.Piece oldP, EquipmentElement oldEl, int oldSell, double gain, int budget, Dictionary<string, List<Ware>> wares,
                                 MobileParty mp, Settlement shop1, Settlement shop2, bool player, out int why)
        {
            why = 0;
            bool better = false, lift = false;
            var oldIt = oldEl.Item;
            bool hasCls = oldIt.PrimaryWeapon != null;
            WeaponClass cls = hasCls ? oldIt.PrimaryWeapon.WeaponClass : default(WeaponClass);
            int bucketTier = c.Slot.Bucket.Tier;
            foreach (var shop in new[] { shop1, shop2 })
            {
                if (shop == null || shop.Town == null || shop.ItemRoster == null) continue;
                var list = Wares(wares, c.Type, shop, mp, player);
                Ware best = null; double bestScore = double.MinValue;
                foreach (var w in list)
                {
                    if (w.Left <= 0) continue;
                    if (hasCls != w.HasCls || (hasCls && w.Cls != cls)) continue;                       // miecz za miecz, tarcza za tarcze
                    if (SwapMath.UpgradeVerdict(bucketTier, oldP.Tier, oldP.Power, w.Tier, w.Power, gain, true, 1, 1) != SwapMath.UpOk) continue;   // w swoim stopniu, wyraznie lepsza
                    better = true;
                    if (!ItemReq.Meets(c.Troop, w.El.Item) || (c.Slot.Bucket.Mounted && !MountOk(w.El.Item))) continue;   // wymog i bron z siodla
                    lift = true;
                    if (w.Price < 0) { try { w.Price = shop.Town.MarketData.GetPrice(w.El, mp, false, shop.Party); } catch { w.Price = 0; } }   // cena dopiero dla kandydata
                    if (SwapMath.UpgradeVerdict(bucketTier, oldP.Tier, oldP.Power, w.Tier, w.Power, gain, true, w.Price, budget) != SwapMath.UpOk) continue;
                    double score = SwapMath.UpgradeScore(oldP.Power, w.Power, w.Price, oldSell);
                    if (score > bestScore) { bestScore = score; best = w; }
                }
                if (best != null) return best;
            }
            why = !better ? 0 : (!lift ? 1 : 2);
            return null;
        }

        private static List<Ware> Wares(Dictionary<string, List<Ware>> cache, ItemObject.ItemTypeEnum type, Settlement shop, MobileParty mp, bool player)
        {
            string key = (int)type + "|" + (shop.StringId ?? "");
            List<Ware> list;
            if (cache.TryGetValue(key, out list)) return list;
            cache[key] = list = new List<Ware>();
            try
            {
                var r = shop.ItemRoster;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (it == null || el.Amount <= 0 || it.ItemType != type) continue;
                    if (ArmsPricing.IsUnique(it) || QuartermasterLaw.BarredInBattle(it)) continue;
                    var m = el.EquipmentElement.ItemModifier;
                    // sprawna: gracz - bez modyfikatora < 1; AI - bez modyfikatora (zbrojownia AI nie zna stanow na plus, sztuka stracilaby wartosc)
                    if (player ? (m != null && m.PriceMultiplier < 1f) : m != null) continue;
                    var pw = it.PrimaryWeapon;
                    list.Add(new Ware { Shop = shop, El = el.EquipmentElement, Price = -1, Left = el.Amount, Tier = AiGear.TierOf(it), Power = RangedRank.Key(it),
                                        HasCls = pw != null, Cls = pw != null ? pw.WeaponClass : default(WeaponClass) });
                }
            }
            catch (Exception e) { Log.Error("MenUpgrade.Wares", e); }
            return list;
        }

        /// <summary>Jak CrashScribe Mends.MountOk / DTE IsSuitableForMount: bron bez "RequiresNoMount" i bez "CantReloadOnHorseback".</summary>
        internal static bool MountOk(ItemObject it)
        {
            try
            {
                if (it == null || !it.HasWeaponComponent || it.Weapons == null) return true;
                foreach (var w in it.Weapons)
                {
                    if (w == null) continue;
                    if (MBItem.GetItemUsageSetFlags(w.ItemUsage).HasAnyFlag((ItemObject.ItemUsageSetFlags)2)) return false;   // RequiresNoMount
                    if (w.WeaponFlags.HasAnyFlag(WeaponFlags.CantReloadOnHorseback)) return false;
                }
            }
            catch { }
            return true;
        }
    }
}
