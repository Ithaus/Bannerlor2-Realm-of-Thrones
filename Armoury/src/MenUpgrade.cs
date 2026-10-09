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
    ///    osady; zamek - najpierw polka zamku, potem miasto handlowe tylko wozem w dzien zamowienia - sklad7, 171 GarrisonCarts).
    ///  - Co: sztuki ludzi ida do koszykow typ x tier wzorca (SwapMath.Assign); w PELNYM koszyku (najpierw braki) najslabsza sztuka
    ///    moze ustapic lepszej z polki: ten sam typ i klasa broni, tier nie wyzszy niz sufit (SwapMath.CeilingTier: tier JEDNOSTKI,
    ///    a z MenUpgradeOneTierUp o jeden wyzej - Jeff 09.10 P1 "tak, jesli go na to stac i jest dostepna"), sila >= stara x (1 + prog)
    ///    albo wyzszy tier, wymog spelniony (ItemReq), z siodla dla jezdzcow, bez unikatow, sprawna. Wybor: najwiecej sily za denara netto.
    ///  - Za ile: cena polki, placi sakiewka ludzi (bez rezerwy na zalegle naprawy) do kasy miasta; lord i pan zalogi NIE doplacaja.
    ///    Stara sztuka od razu do kupca po cenie skupu w tym stanie (nie wiecej niz ma kasa) - pieniadze do sakiewki, sztuka na polke;
    ///    kasa pusta - zostaje w zbrojowni jako zapas (pojdzie z nadwyzkami).
    ///  - sklad8-p (przeglad sklad8, uwaga 1): zakup z polki jak zakupy brakow (AiGear) - w miescie zostawia ostatnia sztuke pasma zbroi
    ///    (ShopReserve, 174b.4), liczy sie w popycie koszyka reguly zlomu (ArmsScrap, tylko AI) i w linii M2 "ZakupyAI wedlug kupujacego" (Measure174b).
    ///  - sklad8-s S4 (MenUpgradeAmmoNeedsSurplus): strzaly i belty tylko na WYZSZY tier (sila = Effectiveness przepuszczala nizszy - 41% wymian
    ///    amunicji w sklad8 schodzilo o tier) i tylko z koszyka bez braku na polce (SupplyDemand.Factor <= 1: cena nie wyzsza od wartosci) -
    ///    zolnierz z pelnym kolczanem nie zabiera ostatnich snopow tym, ktorzy nie maja zadnego (zakupy brakow AiGear, notable dla ochotnikow).
    ///    Jedna regula dla ludzi gracza, lordow i zalog; reszta sprzetu bez zmian.
    /// </summary>
    internal static class MenUpgrade
    {
        internal static bool On { get { var s = Settings.Current; return s != null && s.MenUpgradeGear && MenPurse.On; } }
        internal static bool GarrisonPurseOn { get { var s = Settings.Current; return s != null && s.GarrisonPurseEnabled && MenPurse.On; } }

        private static readonly Dictionary<string, int> _lastDay = new Dictionary<string, int>();
        // liczniki doby (linia "Dozbrajanie: dzien")
        private static int _dStamp = -1, _dLogged;
        private static int _dPlayerN, _dLordN, _dGarN, _dSold, _dKept, _dNoBetter, _dNoMoney, _dNoLift, _dHeld, _dGarWageN, _dGarGapN, _dBattleArmory, _dBattleTemplate, _dGarEmptyN;
        private static long _dPlayerGold, _dLordGold, _dGarGold, _dSoldGold, _dSaved, _dOverCap, _dGarWage, _dGarGapGold, _dGarEmptyGold;
        // K1 (przeglad): obrot w kolko - ta sama partia kupila i sprzedala sztuke tego samego id tej samej doby (autotest: 0)
        private static readonly HashSet<string> _boughtToday = new HashSet<string>(), _soldToday = new HashSet<string>();
        private static int _dChurn, _dWaitCart;   // poprawki sklad7: zamki bez wymiany do dostawy wozu z dozbrajaniem
        // sklad8-s S4: amunicja - wymiany w gore / w dol lub ten sam tier (przy wylaczonym MenUpgradeAmmoNeedsSurplus), koszyki odrzucone: lepszy tylko
        // nizszego lub tego samego tieru, lepszy tylko z koszyka z brakiem na polce (tylko log)
        private static int _dAmmoUp, _dAmmoDown, _dAmmoLowTier, _dAmmoShort;
        // sklad8-s (przeglad, uwaga 6): blad SupplyDemand.Factor w FacOf - potkniecia doby i od startu sesji, pierwszy wyjatek do logu (licz, nie gas)
        private static int _dAmmoFacErr, _ammoFacErrAll; private static bool _ammoFacErrLogged;
        private static readonly List<string> _churnIds = new List<string>();

        internal static void Reset()
        {
            _lastDay.Clear(); _dStamp = -1; ClearDay(); _ammoFacErrAll = 0; _ammoFacErrLogged = false;
        }

        private static void ClearDay()
        {
            _dLogged = 0;
            _dPlayerN = _dLordN = _dGarN = _dSold = _dKept = _dNoBetter = _dNoMoney = _dNoLift = _dHeld = _dGarWageN = _dGarGapN = _dBattleArmory = _dBattleTemplate = _dGarEmptyN = 0;
            _dPlayerGold = _dLordGold = _dGarGold = _dSoldGold = _dSaved = _dOverCap = _dGarWage = _dGarGapGold = _dGarEmptyGold = 0;
            _boughtToday.Clear(); _soldToday.Clear(); _dChurn = 0; _churnIds.Clear(); _dTempSlots = 0; _dBareSlots = 0; _dNoEmerg = 0; _dNothing = 0; _dLordLent = 0; _dWaitCart = 0;
            _dAmmoUp = _dAmmoDown = _dAmmoLowTier = _dAmmoShort = _dAmmoFacErr = 0;
        }

        /// <summary>sklad8-s S4: strzaly i belty - wymiana tylko na wyzszy tier i tylko z nadwyzki polki (wylacznik MenUpgradeAmmoNeedsSurplus).</summary>
        private static bool AmmoRule(ItemObject.ItemTypeEnum t)
        {
            var s = Settings.Current;
            return s != null && s.MenUpgradeAmmoNeedsSurplus && (t == ItemObject.ItemTypeEnum.Arrows || t == ItemObject.ItemTypeEnum.Bolts);
        }

        /// <summary>K1 (przeglad): zakup (buy=true) albo sprzedaz sztuki przez ludzi partii - licznik "kupione i sprzedane te same id tej
        /// samej doby" w linii "Dozbrajanie: dzien" (zakupy brakow, lepsze, nadwyzki; gracz, lordowie, zalogi). Tylko log.</summary>
        internal static void NoteChurn(MobileParty mp, ItemObject it, bool buy)
        {
            try
            {
                if (mp == null || it == null) return;
                Touch();
                string k = (mp.StringId ?? "") + "|" + (it.StringId ?? "");
                if ((buy ? _soldToday : _boughtToday).Contains(k)) { _dChurn++; if (_churnIds.Count < 4 && !_churnIds.Contains(k)) _churnIds.Add(k); }
                (buy ? _boughtToday : _soldToday).Add(k);
            }
            catch { }
        }

        // ------------------------------------------------------------ liczniki z innych miejsc (tylko log)
        internal static void NoteSaved(int saved, int overCap) { Touch(); _dSaved += Math.Max(0, saved); _dOverCap += Math.Max(0, overCap); }
        internal static void NoteGarrisonWage(int amount) { if (amount <= 0) return; Touch(); _dGarWage += amount; _dGarWageN++; }
        internal static void NoteGarrisonGap(int fromPurse) { if (fromPurse <= 0) return; Touch(); _dGarGapGold += fromPurse; _dGarGapN++; }
        internal static void NoteGarrisonEmpty(int purse) { if (purse <= 0) return; Touch(); _dGarEmptyGold += purse; _dGarEmptyN++; }
        internal static void NoteBattle(bool fromArmory) { Touch(); if (fromArmory) _dBattleArmory++; else _dBattleTemplate++; }
        // K1 (przeglad): sloty ludzi zalog wypelnione w bitwie przez DTE "z niczego" i oznaczone jako tymczasowe (nie wracaja do zbrojowni)
        internal static void NoteTempSlots(int n) { if (n <= 0) return; Touch(); _dTempSlots += n; }
        private static int _dTempSlots;
        // K1 (Jeff 09.10, P2): zaloga walczy tylko tym, co ma - sloty wzorca bez sztuki (walcza bez niej) i rozdzielacze bez zestawu awaryjnego DTE
        internal static void NoteBareSlots(int n) { if (n <= 0) return; Touch(); _dBareSlots += n; }
        internal static void NoteNoEmergency() { Touch(); _dNoEmerg++; }
        // K1c (przeglad K1b): straz "nic z niczego" - sloty przydzialow zalog bez pokrycia w zbrojowni zalogi (podmiany strazy CrashScribe)
        internal static void NoteNothingSlots(int n) { if (n <= 0) return; Touch(); _dNothing += n; }
        private static int _dBareSlots, _dNoEmerg, _dNothing;
        // sklad7: sloty ludzi lordow AI wypelnione w bitwie gracza przez DTE z niczego (FillEmptySlots) - pozyczone na bitwe, nie wracaja do zbrojowni lorda
        internal static void NoteLordLent(int n) { if (n <= 0) return; Touch(); _dLordLent += n; }
        private static int _dLordLent;

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
                    // pomiar rynku do autotestu (C5): ile miast nie ma na polce ani jednej zbroi korpusu t3+ (tylko log)
                    int towns = 0, bare = 0;
                    try
                    {
                        foreach (var t in Town.AllTowns)
                        {
                            if (t == null || !t.IsTown || t.Settlement == null || t.Settlement.ItemRoster == null) continue;
                            towns++;
                            var r = t.Settlement.ItemRoster; bool has = false;
                            for (int i = 0; i < r.Count && !has; i++)
                            {
                                var el = r.GetElementCopyAtIndex(i); var it = el.EquipmentElement.Item;
                                has = el.Amount > 0 && it != null && it.ItemType == ItemObject.ItemTypeEnum.BodyArmor && AiGear.TierOf(it) >= 3;
                            }
                            if (!has) bare++;
                        }
                    }
                    catch { }
                    Log.Info("Dozbrajanie: dzien " + _dStamp + " - gracz " + _dPlayerN + "/" + _dPlayerGold + ", lordowie " + _dLordN + "/" + _dLordGold
                             + ", zalogi " + _dGarN + "/" + _dGarGold + " (szt./zloto; zamki czekaja na woz z dozbrajaniem " + _dWaitCart + "); stare sprzedane " + _dSold + " za " + _dSoldGold + " (do zbrojowni " + _dKept + ")"
                             + "; pominiete koszyki: brak lepszej na polce " + _dNoBetter + ", za malo w sakiewce " + _dNoMoney + ", nikt nie udzwignie " + _dNoLift + ", lepsza tylko w rezerwie kramu (174b.4) " + _dHeld
                             + "; odlozone przy wyjazdach " + _dSaved + ", ponad limit na zycie " + _dOverCap
                             + "; zold zalog do sakiewek " + _dGarWage + " (" + _dGarWageN + " zalog), w sakiewkach zalog " + garPurses + " (" + garN + " zalog)"
                             + "; braki zalog z ich sakiewek " + _dGarGapGold + " (" + _dGarGapN + " zakupow), sakiewki pustych zalog do kas osad " + _dGarEmptyGold + " (" + _dGarEmptyN + ")"
                             + "; zalogi w bitwie ze zbrojowni " + _dBattleArmory + " / we wzorcu " + _dBattleTemplate + " (sloty z wzorca jako tymczasowe " + _dTempSlots
                             + ", sloty wzorca bez sztuki - walcza bez " + _dBareSlots + ", bez zestawu awaryjnego DTE " + _dNoEmerg + ", bez pokrycia w zbrojowni zatrzymane " + _dNothing + ")"
                             + "; sloty lordow AI z niczego tylko na bitwe (nie wracaja do zbrojowni) " + _dLordLent
                             + "; amunicja (sklad8-s S4, " + (Settings.Current != null && Settings.Current.MenUpgradeAmmoNeedsSurplus ? "tylko wyzszy tier i z nadwyzki" : "WYLACZONE - jak reszta sprzetu")
                             + "): wymiany w gore " + _dAmmoUp + ", w dol lub ten sam tier " + _dAmmoDown + ", koszyki bez wymiany - lepszy tylko nizszego lub tego samego tieru " + _dAmmoLowTier
                             + ", lepszy tylko w koszyku z brakiem na polce " + _dAmmoShort
                             + ", potkniecia mnoznika polki (blad - wymiana jak bez braku) " + _dAmmoFacErr + " (od startu " + _ammoFacErrAll + ")"
                             + "; kupione i sprzedane te same id tej samej doby " + _dChurn + (_churnIds.Count > 0 ? " (" + string.Join(", ", _churnIds.ToArray()) + ")" : "")
                             + "; miasta bez zbroi korpusu t3+ na polce " + bare + " z " + towns + ".");
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
                // poprawki sklad7: do zamku jedzie juz woz z dozbrajaniem - zadnej wymiany do dostawy, takze z polki zamku (shop1); inaczej ta sama stara
                // sztuka, na ktora juz jedzie nowa, bylaby wymieniona drugi raz, a po dostawie dwie nowe na jeden slot (jedna w nadwyzkach ze strata)
                if (garrison && st.IsCastle && GarrisonCarts.UpgradeInTransit(st)) { Touch(); _dWaitCart++; return; }
                if (!Gate(mp)) return;
                Touch();
                int budget = purse0 - AiWear.OutstandingCost(mp, st.IsTown ? st : null);
                if (budget <= 0) return;
                Settlement shop1 = st, shop2 = null, sellTo = st, cartFrom = null;
                float cartDist = 0f;
                if (st.IsUnderSiege) return;
                if (garrison && st.IsCastle && GarrisonCarts.On && GarrisonCarts.OrderDay(st) && !GarrisonCarts.UpgradeInTransit(st))
                {
                    // sklad7 (scalenie 171 + K1 A4 - jedna droga towaru do zamku): najpierw polka zamku (od razu); z miasta handlowego TYLKO wozem
                    // (171 C2/Z6, GarrisonCarts): w dzien zamowienia zamku i gdy nie jedzie juz poprzednie dozbrajanie. Nowa sztuka dojezdza za 1-4 doby,
                    // stara zostaje w zbrojowni do dostawy (potem idzie z nadwyzkami - GarrisonArmory.SellWeek). Dotad K1 kupowal w miescie od razu
                    // ("sztuke przywozi woz pana bez kosztu") i sprzedawal stara w miescie - teleport w obie strony. Bez wozow (GarrisonGearFromTown
                    // off) - tylko polka zamku, jak zakupy brakow. Stara sztuka z polki zamku - na polke zamku (Z6: zamek zrodlem dla kupcow).
                    string why;
                    var market = GarrisonCarts.MarketFor(st, out cartDist, out why, false);
                    if (market != null) { shop2 = market; cartFrom = market; }
                }
                var cart = cartFrom != null ? new List<GarrisonCarts.Line>() : null;
                var v = Run(mp, new AiKit(mp, arm, garrison), shop1, shop2, sellTo, budget, false, cartFrom, cart);
                if (cart != null && cart.Count > 0)
                {
                    GarrisonCarts.MarkTried(st);
                    GarrisonCarts.Place(st, cartFrom, st.OwnerClan, cart, v.CartGold, cartDist, false, false, v.CartGold);   // cale z sakiewki zalogi
                }
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
                     + (v.Carted > 0 ? ", wozem z miasta " + v.Carted + " szt. za " + v.CartGold + " (stare zostaja do dostawy)" : "")
                     + (v.Kept > 0 ? " (kasa miasta pusta)" : "") + "; sakiewka " + purse0 + " -> " + purse1 + ".");
        }

        // ------------------------------------------------------------ zbrojownie (gracz: roster DTE z ksiega; AI: slownik DTE + AiWear)
        private interface IKit
        {
            List<SwapMath.Piece> Pieces(ItemObject.ItemTypeEnum type);
            bool Add(EquipmentElement el);
            bool TakeOld(SwapMath.Piece p, out EquipmentElement el);
            void PutBackOld(SwapMath.Piece p, EquipmentElement el);
        }

        private sealed class PlayerKit : IKit
        {
            private readonly ItemRoster _a;
            internal PlayerKit(ItemRoster a) { _a = a; }
            // sztuki LUDZI: czesc gracza (ksiega, najgorsze egzemplarze) wypada przez MenTotal - nigdy nie idzie do kupca
            public List<SwapMath.Piece> Pieces(ItemObject.ItemTypeEnum type) { return QuartermasterLaw.KitPieces(_a, type, true); }
            public bool Add(EquipmentElement el) { _a.AddToCounts(el, 1); return true; }
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
            public bool Add(EquipmentElement el)
            {
                if (!AiGear.AddToArmory(_mp, el.Item, 1)) return false;   // K1 (przeglad): DTE odrzucil (czarna lista) - nikt nie placi
                AiWear.NoteBought(_mp, el, 1);   // jak AiGear: sprawna sztuka z polki; K1 (przeglad): takze zaloga (AiWear sledzi jej bitwy)
                return true;
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
        private sealed class Cand { public ItemObject.ItemTypeEnum Type; public int Order; public SwapMath.Slot Slot; public CharacterObject Troop; public int Ceil; }
        private sealed class Ware { public Settlement Shop; public EquipmentElement El; public int Price, Left, Tier; public long Power; public bool HasCls; public WeaponClass Cls; public float Fac = -1f; }   // Fac: sklad8-s S4, mnoznik polki koszyka (-1 = do policzenia)
        private sealed class Visit { public int N, Gold, Sold, SoldGold, Kept, Carted, CartGold; public List<string> Lines = new List<string>(); public List<string> Names = new List<string>(); }

        /// <summary>sklad7: cartFrom/cart - zaloga zamku kupujaca w miescie handlowym: sztuka z polki tego miasta idzie do zamowienia wozem (GarrisonCarts,
        /// linia UpgradeBucket), nie do zbrojowni; stara zostaje do dostawy.</summary>
        private static Visit Run(MobileParty mp, IKit kit, Settlement shop1, Settlement shop2, Settlement sellTo, int budget, bool player,
                                 Settlement cartFrom = null, List<GarrisonCarts.Line> cart = null)
        {
            var v = new Visit();
            var s = Settings.Current;
            int max = Math.Max(0, s.MenUpgradeMaxPerVisit);
            if (max == 0 || budget <= 0 || mp.MemberRoster == null) return v;
            double gain = Math.Max(0f, s.MenUpgradeMinGainPercent) / 100.0;
            bool oneUp = s.MenUpgradeOneTierUp;
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
                    if (sl.Bucket.Full) cands.Add(new Cand { Type = type, Order = ti, Slot = sl, Troop = troops[sl.Group], Ceil = SwapMath.CeilingTier(TroopTier(troops[sl.Group]), oneUp) });
            }
            if (cands.Count == 0) return v;
            // najpierw sloty z najwieksza roznica "sufit jednostki - tier sztuki", potem typy (korpus, bron, tarcza, helm, ...), najslabsze najpierw
            cands.Sort((a, b) =>
            {
                int d = (b.Ceil - b.Slot.Piece.Tier).CompareTo(a.Ceil - a.Slot.Piece.Tier);
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
                int oldSell = sellTo != null ? MenPurse.SellPriceHere(oldEl, sellTo, mp) : 0;   // sklad7: cena polki tej osady (zamek - zamku)
                for (int k = 0; k < c.Slot.N && v.N < max; k++)
                {
                    if (oldP.MenTotal <= 0) break;
                    int why;
                    Settlement heldAt;
                    var w = Pick(c, oldP, oldEl, oldSell, gain, budget, wares, mp, shop1, shop2, player, out why, out heldAt);
                    if (w == null)
                    {
                        if (k == 0) { if (why == 1) _dNoLift++; else if (why == 2) _dNoMoney++; else if (why == 3) _dHeld++; else if (why == 4) _dAmmoShort++; else { _dNoBetter++; if (why == 5) _dAmmoLowTier++; } }   // sklad8-s S4: 4 i 5
                        // sklad8-p (uwaga 1): rezerwa kramu 174b.4 - lepsza sztuka zostala na straganie (licznik "zatrzymane" linii M2, jak AiGear i notable:
                        // gdy wybor nic nie dal, a choc jeden kandydat odpadl na rezerwie)
                        if (heldAt != null) Measure174b.NoteHeld(BuyerOf(mp, player, heldAt, cart != null && heldAt == cartFrom), 1);
                        break;
                    }
                    int price = w.Price;
                    w.Shop.ItemRoster.AddToCounts(w.El, -1); w.Left--;
                    bool byCart = cart != null && cartFrom != null && w.Shop == cartFrom;
                    if (byCart) AddLine(cart, w.El);   // sklad7: do zamowienia wozem - dojedzie do zbrojowni zamku (DTE odrzuci - na polke zamku)
                    else if (!kit.Add(w.El)) { w.Shop.ItemRoster.AddToCounts(w.El, 1); w.Left = 0; continue; }   // K1 (przeglad): sztuka wraca na polke
                    NoteChurn(mp, w.El.Item, true);
                    int paid = MenPurse.Take(mp, price);
                    budget -= price;
                    w.Shop.Town.ChangeGold(paid);
                    if (player) MoneyLedger.Note169(MoneyLedger.N169Kit, w.Shop, paid);   // jak braki gracza (paczka 169: linia kas)
                    else MoneyLedger.Note(MoneyLedger.NGear, w.Shop, paid);               // jak zakupy AiGear (ksiega przeplywow osad)
                    // sklad8-p (uwaga 1): dozbrajanie to zakup z polki jak zakupy brakow - liczy sie w tych samych miarach 174/174b:
                    // popyt koszyka w miescie dla reguly zlomu (ArmsScrap - tylko AI, jak AiGear i notable; gracz nie wchodzi, jak BuyPlayerGaps)
                    // i linia M2 "ZakupyAI wedlug kupujacego" (wszyscy, z gracza - "ludzie gracza (sakiewka)"). Tylko liczniki.
                    if (!player) ArmsScrap.NoteBuy(w.Shop, w.El.Item, 1);
                    Measure174b.NoteBuy(BuyerOf(mp, player, w.Shop, byCart), w.El.Item, 1, paid);
                    v.N++; v.Gold += paid;
                    if (c.Type == ItemObject.ItemTypeEnum.Arrows || c.Type == ItemObject.ItemTypeEnum.Bolts) { if (w.Tier > oldP.Tier) _dAmmoUp++; else _dAmmoDown++; }   // sklad8-s S4 (tylko log)
                    // ceny hurtu (Jeff 09.10 08:00): kazda sztuka po swojej cenie - po zdjeciu sztuki ceny towaru tej polki w tym koszyku, w koszyku
                    // tier nizej (substytucja - sklad8-p) i w tej kategorii licz od nowa (Pick wycenia je przy nastepnym wyborze). Dotad od nowa tylko
                    // kupiona sztuka - inna sztuka tego koszyka szla po cenie sprzed zakupu (polka wieksza o kupione sztuki).
                    Stale(wares, w.Shop, w.El.Item);
                    string ln = c.Type + " t" + oldP.Tier + "->t" + w.Tier + " " + oldP.Id + "->" + (w.El.Item.StringId ?? "?") + " " + paid;
                    if (v.Lines.Count < 6) v.Lines.Add(ln);
                    if (v.Names.Count < 3) v.Names.Add(w.El.Item.Name + " for " + oldEl.Item.Name);
                    if (byCart) { v.Carted++; v.CartGold += paid; continue; }   // stara zostaje, az nowa dojedzie
                    // stara sztuka od razu do kupca (cena skupu w tym stanie, nie wiecej niz ma kasa); kasa pusta - zostaje jako zapas
                    EquipmentElement soldEl;
                    if (sellTo == null || sellTo.Town == null || !kit.TakeOld(oldP, out soldEl)) { v.Kept++; _dKept++; continue; }
                    int unit = MenPurse.SellPriceHere(soldEl, sellTo, mp);
                    if (!SwapMath.MerchantPays(sellTo.Town.Gold, unit)) { kit.PutBackOld(oldP, soldEl); v.Kept++; _dKept++; continue; }
                    sellTo.ItemRoster.AddToCounts(soldEl, 1);
                    Stale(wares, sellTo, soldEl.Item);   // ceny hurtu (09.10): sztuka doszla na polke - ceny tego koszyka i kategorii od nowa
                    sellTo.Town.ChangeGold(-unit);
                    MoneyLedger.Note169(MoneyLedger.N169Surplus, sellTo, -unit);   // paczka 169: linia kas (tylko licznik)
                    MenPurse.Add(mp, unit);
                    budget += unit;
                    NoteChurn(mp, soldEl.Item, false);
                    v.Sold++; v.SoldGold += unit; _dSold++; _dSoldGold += unit;
                    SellByCondition.NoteSale(SellByCondition.Men, soldEl, 1, unit);   // ksiega skupu sprzetu (tylko log)
                }
            }
            return v;
        }

        /// <summary>
        /// Ceny hurtu (09.10): polka sklepu zmienila sie o sztuke "changed" - ceny towaru tej polki do policzenia od nowa (Price = -1, Pick wycenia
        /// kandydata przy nastepnym wyborze). Cena sztuki zalezy od polki tylko przez koszyk typ x tier (SupplyDemand.Stock - mnoznik polki), przez
        /// koszyk o tier wyzej (SupplyDemand.Substitution: gdy na polce nie ma ani jednej sztuki tieru t+1, czesc jego popytu przechodzi na t) i przez
        /// wartosc polki w kategorii (dane rynku gry - InStoreValue kategorii, gdy cena nie idzie od wartosci sztuki); reszta (popyt, surowce,
        /// wartosc sztuki) od zakupu sie nie zmienia - stad ten koszyk, koszyk tier nizej i ta kategoria, nie cala polka.
        /// sklad8-p (uwaga 4): dotad bez koszyka tier nizej - po zakupie ostatniej sztuki t+1 (zwykle inna kategoria niz t) albo po sprzedazy
        /// pierwszej sztuki t+1 do pustego koszyka nastepny wybor sztuki t w tej samej wizycie szedl po starej cenie.
        /// </summary>
        private static void Stale(Dictionary<string, List<Ware>> cache, Settlement shop, ItemObject changed)
        {
            if (cache == null || shop == null || changed == null) return;
            int t = AiGear.TierOf(changed);
            foreach (var list in cache.Values)
                foreach (var w in list)
                {
                    if ((w.Price < 0 && w.Fac < 0f) || w.Shop != shop) continue;
                    var it = w.El.Item;
                    if (it == null) continue;
                    if (it.ItemCategory == changed.ItemCategory || (it.ItemType == changed.ItemType && (w.Tier == t || w.Tier == t - 1))) { w.Price = -1; w.Fac = -1f; }   // sklad8-s S4: mnoznik polki koszyka tez od nowa
                }
        }

        /// <summary>sklad8-p (uwaga 1): kupujacy dozbrajania w liczniku M2 (Measure174b) - te same grupy co zakupy brakow AiGear.TryBuyCore:
        /// ludzie gracza, lord, zaloga miasta, zaloga zamku z polki zamku, zaloga zamku w miescie handlowym (woz).</summary>
        private static int BuyerOf(MobileParty mp, bool player, Settlement shop, bool byCart)
        {
            if (player) return Measure174b.BPlayerMen;
            if (mp == null || !mp.IsGarrison) return Measure174b.BLord;
            if (byCart) return Measure174b.BCastleOrder;
            return shop != null && shop.IsTown ? Measure174b.BTownGarrison : Measure174b.BCastleOwn;
        }

        private static void AddLine(List<GarrisonCarts.Line> cart, EquipmentElement el)
        {
            foreach (var l in cart) if (l.El.Item == el.Item && l.El.ItemModifier == el.ItemModifier) { l.N++; return; }
            cart.Add(new GarrisonCarts.Line { El = el, N = 1, Bucket = GarrisonCarts.UpgradeBucket });
        }

        /// <summary>Najlepsza sztuka z polki dla tego slotu: najpierw shop1 (zamek: polka zamku), potem shop2. why: 0 brak lepszej,
        /// 1 nikt nie udzwignie, 2 za malo w sakiewce, 3 (sklad8-p) lepsza sztuka do udzwigniecia jest, ale to ostatnia sztuka pasma zbroi
        /// w rezerwie kramu (174b.4; heldAt - targ, na ktorym zostala); sklad8-s S4 (amunicja przy MenUpgradeAmmoNeedsSurplus): 4 lepszy kolczan do udzwigniecia
        /// jest tylko w koszyku z brakiem na polce, 5 wyraznie lepszy jest tylko nizszego albo tego samego tieru.</summary>
        private static Ware Pick(Cand c, SwapMath.Piece oldP, EquipmentElement oldEl, int oldSell, double gain, int budget, Dictionary<string, List<Ware>> wares,
                                 MobileParty mp, Settlement shop1, Settlement shop2, bool player, out int why, out Settlement heldAt)
        {
            why = 0; heldAt = null;
            bool better = false, lift = false, priced = false, lowTier = false, shortage = false;
            bool ammoRule = AmmoRule(c.Type);   // sklad8-s S4
            var oldIt = oldEl.Item;
            bool hasCls = oldIt.PrimaryWeapon != null;
            WeaponClass cls = hasCls ? oldIt.PrimaryWeapon.WeaponClass : default(WeaponClass);
            int ceil = c.Ceil;   // K1-A (P1): sufit od tieru jednostki (+1 z MenUpgradeOneTierUp), ten sam dla ludzi gracza, lordow i zalog
            foreach (var shop in new[] { shop1, shop2 })
            {
                if (shop == null || shop.Town == null || shop.ItemRoster == null) continue;
                var list = Wares(wares, c.Type, shop, mp, player);
                Ware best = null; double bestScore = double.MinValue;
                foreach (var w in list)
                {
                    if (w.Left <= 0) continue;
                    if (hasCls != w.HasCls || (hasCls && w.Cls != cls)) continue;                       // miecz za miecz, tarcza za tarcze
                    if (SwapMath.UpgradeVerdict(ceil, oldP.Tier, oldP.Power, w.Tier, w.Power, gain, true, 1, 1) != SwapMath.UpOk) continue;   // do sufitu, wyraznie lepsza
                    // sklad8-s S4: kolczan tylko na WYZSZY tier - "sila" (Effectiveness) przepuszczala nizszy (t3 vlandic -> t1 range): stary szedl do kupca
                    // za 1 zl, a koszyk wzorca zostawal tak samo pusty
                    if (ammoRule && w.Tier <= oldP.Tier) { lowTier = true; continue; }
                    better = true;
                    if (!ItemReq.Meets(c.Troop, w.El.Item) || (c.Slot.Bucket.Mounted && !MountOk(w.El.Item))) continue;   // wymog i bron z siodla
                    lift = true;
                    // sklad8-p (uwaga 1): dozbrajanie kupuje dla oddzialu (lord, zaloga, ludzie gracza z sakiewki) - hurt jak zakupy brakow, wiec
                    // ostatnia sztuka pasma zbroi zostaje na straganie dla kupujacego osobiscie (ShopReserve; MAX = rezerwa nie dotyczy: zamek, nie zbroja)
                    if (ShopReserve.Free(shop, w.El.Item) <= 0) { if (heldAt == null) heldAt = shop; continue; }
                    // sklad8-s S4: kolczan na wymiane tylko z nadwyzki - koszyk polki bez braku (mnoznik polki <= 1, cena nie wyzsza od wartosci); przy braku
                    // ostatnie snopy zostaja dla tych, ktorzy nie maja zadnego (zakupy brakow, notable dla ochotnikow)
                    if (ammoRule && SupplyDemand.Active && FacOf(w) > 1f) { shortage = true; continue; }
                    priced = true;
                    if (w.Price < 0) { try { w.Price = shop.Town.MarketData.GetPrice(w.El, mp, false, shop.Party); } catch { w.Price = 0; } }   // cena dopiero dla kandydata
                    if (SwapMath.UpgradeVerdict(ceil, oldP.Tier, oldP.Power, w.Tier, w.Power, gain, true, w.Price, budget) != SwapMath.UpOk) continue;
                    double score = SwapMath.UpgradeScore(oldP.Power, w.Power, w.Price, oldSell);
                    if (score > bestScore) { bestScore = score; best = w; }
                }
                if (best != null) return best;
            }
            why = !better ? (lowTier ? 5 : 0) : (!lift ? 1 : (!priced && heldAt != null ? 3 : (!priced && shortage ? 4 : 2)));
            return null;
        }

        /// <summary>sklad8-s S4: mnoznik polki koszyka sztuki (SupplyDemand.Factor, kupno) - raz na sztuke towaru, od nowa po zmianie polki (Stale).</summary>
        private static float FacOf(Ware w)
        {
            if (w.Fac < 0f)
            {
                try { float d; int sh; w.Fac = SupplyDemand.Factor(w.Shop, w.El.Item, false, out d, out sh); }
                catch (Exception e)
                {
                    // sklad8-s (przeglad, uwaga 6): jak TownFletchers.Fac - 1 (bez braku), ale z potknieciem w linii "Dozbrajanie" i pierwszym bledem w logu;
                    // dotad warunek "tylko z nadwyzki" znikal po cichu
                    w.Fac = 1f; _dAmmoFacErr++; _ammoFacErrAll++;
                    if (!_ammoFacErrLogged) { _ammoFacErrLogged = true; Log.Error("MenUpgrade.FacOf", e); }
                }
            }
            return w.Fac;
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
                    if (!player && !AiGear.DteTakes(it)) continue;   // poprawki sklad7: zbrojownia AI (DTE) jej nie przyjmie - takze linia wozu do zamku
                    // K1 (przeglad): ksiega gracza jest per id i obejmuje NAJGORSZE egzemplarze - zakup ludzi id, w ktorym gracz ma czesc
                    // (np. Masterwork), przesunalby wlasnosc gracza na zwykly egzemplarz bez slowa; ludzie kupuja inne id
                    if (player && ArmouryBehavior.StockOf(it.StringId) > 0) continue;
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

        /// <summary>K1c (przeglad K1b, P1): sufity zakupu (SwapMath.CeilingTier) ludzi bez sztuki - po jednym na czlowieka, od najwyzszego.
        /// Braki ludzi gracza: k-ty zakup typu najwyzej do k-tego sufitu (rekrut bez zbroi nie kupi plyty, rycerz bez zbroi - do swojego).</summary>
        internal static List<int> CeilingsOf(Dictionary<CharacterObject, int> unfitByTroop, bool oneUp)
        {
            var l = new List<int>();
            if (unfitByTroop != null)
                foreach (var kv in unfitByTroop)
                {
                    int c = SwapMath.CeilingTier(TroopTier(kv.Key), oneUp);
                    for (int i = 0; i < kv.Value; i++) l.Add(c);
                }
            l.Sort((a, b) => b.CompareTo(a));
            return l;
        }

        /// <summary>K1-A (P1): tier jednostki (CharacterObject.Tier, 0..6+); blad - 1.</summary>
        internal static int TroopTier(CharacterObject c)
        {
            try { return c != null ? c.Tier : 1; } catch { return 1; }
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
