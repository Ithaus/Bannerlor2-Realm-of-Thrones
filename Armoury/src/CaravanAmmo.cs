using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// KARAWANY BEZ AMUNICJI (paczka 172b; DECYZJA JEFFA 09.10: karawany nie handluja strzalami ani beltami).
    /// Dowod (autotest 171+172, doba 40): w taborach karawan 4 157 strzal i 1 764 beltow, na polkach miast 239 i 406 (95% i 81%
    /// amunicji w karawanach); 48 z 97 miast bez strzal; awanse lucznikow u notabli cofniete w 66-88%.
    /// Przyczyna: BK uczynil kategorie "arrows" towarem handlowym (BKItemCategories.cs:122, InitializeObject(true, ...)). Te kategorie
    /// dostaje kazda amunicja bez item_category w XML - a takiej nie ma w zadnym module (DefaultItemCategorySelector: IsAmmo ->
    /// DefaultItemCategories.Arrows), wiec strzaly i belty to jedna kategoria.
    /// Drogi, ktorymi karawana bierze towar do taborow (dekompilacje gry, BK, BEE, BKROTPatch, DTE, ROT, Spoils of War, AIInfluence):
    ///  1) BK BKCaravansBehavior.BuyGoods - przy kazdej decyzji karawany (HourlyTickPartyImpl) i w kursach wstepnych nowej gry
    ///     (DoInitialTradeRuns): 5 kategorii z najwieksza CalculateBuyValue, potem BuyCategory (znow CalculateBuyValue; ponizej 7 nic).
    ///     Jedna droga dla WSZYSTKICH karawan - notabli, rodow lordow i rodu gracza (BK ich nie rozroznia; rozkaz gracza "zaopatruj
    ///     miasto w zywnosc" to tylko filtr kategorii przed ta sama wycena). ZAMKNIETA: postfiks CalculateBuyValue - kategoria amunicji
    ///     ma wycene 0, wiec nie wchodzi do piatki i BuyCategory nic nie kupuje.
    ///  2) BK BKPartyBehavior.OnSettlementLeft ("gabka"): przy wyjezdzie z miasta kazdy towar tanszy niz 33% wartosci, do 80% udzwigu -
    ///     bez wyceny kategorii, wiec latka 1 go nie lapie (snopy sprzedane miastu tanieja na jego polce). ZAMKNIETA: prefiks zapisuje
    ///     amunicje w taborach, postfiks oddaje to, co gabka dokupila, na polke miasta, a zloto z kasy miasta do kiesy karawany (ta sama
    ///     cena - polka wraca do stanu sprzed gabki; wzor CaravanBulk.LeftPostfix).
    ///  3) Gra CaravansCampaignBehavior.BuyGoods (kategorie IsTradeGood albo IsAnimal) - martwa: BK wylacza HourlyTickParty i
    ///     DoInitialTradeRuns gry (CaravansCampaignBehavior_HourlyTickParty_Skip, _DoInitialTradeRuns_Skip). Sprzedaz gry i BK przy
    ///     wjezdzie zostaje - pomaga oprozniac tabory.
    ///  4) BEE CaravanCampaignBehavior.TryDeliverContract przenosi towar ze wsi albo z miasta prosto na polke miasta docelowego, nie przez
    ///     tabor; towary specjalnosci (SpecialtyGoods) i popyt wojskowy BEE nie maja amunicji - nic do zamykania.
    ///  5) Reszta: DTE dosypuje karawanom posilkow (DoNotMakeNewDecisions - nie handluja) jedzenie, juczne i po sztuce losowego sprzetu -
    ///     to nie handel (sprawa "z niczego" poza 172b, tu tylko liczona w linii dnia); Spoils, ROT, BKROTPatch, AIInfluence - zadnych
    ///     zakupow do taborow karawan.
    /// Amunicja juz lezaca w taborach (stary zapis, lupy): przy kazdym wjezdzie karawany handlowej do miasta idzie na polke tego miasta
    /// przez SellItemsAction - cena rynkowa sztuka po sztuce, zloto z kasy miasta do kiesy karawany, tylko z kasy ponad rezerwe na renty
    /// (TownRentFloorGold, jak przy surowcach CaravanBulk); czego miasto nie udzwignie, idzie w nastepnym miescie. Nic nie powstaje, nic
    /// nie znika. Nasz sluchacz wjazdu idzie przed sprzedaza BK i gry (zdarzenia gry wolaja sluchaczy od ostatnio dopisanego).
    /// Czego NIE ruszamy: zakupy amunicji przez lordow i zalogi (AiGear, GarrisonArmory), strzelarze 172 (TownFletchers), flaga towaru
    /// kategorii (od niej zaleza ceny, popyt i budzety BK), wynik celu BK (GetTradeScoreForTown liczy jeszcze "oplaca sie tu kupic
    /// strzaly" - ograniczony przez 0.3 x wartosc amunicji na polce, przy prawie pustych polkach maly; latka tam szlaby na kazde miasto
    /// x kategorie x karawane przy kazdej decyzji - za drogo jak na ten efekt).
    /// Wylacznik CaravansNoAmmoTrade (domyslnie wlaczony): wylaczony = karawany handluja amunicja jak w BK; linia dnia zostaje.
    /// </summary>
    internal static class CaravanAmmo
    {
        private const float BkBuyFloor = 7f;   // BKCaravansBehavior.BuyCategory: wycena ponizej 7 - BK nic nie kupuje

        private static readonly HashSet<ItemCategory> _cats = new HashSet<ItemCategory>();   // kategorie amunicji tej kampanii
        private static bool _resolved;
        private static bool _wiredValue, _wiredLeft;
        // wspolny znacznik "przed gabka nie bylo amunicji" (lista zawsze pusta, nigdy nie zmieniana)
        private static readonly List<(EquipmentElement, int)> NoneBefore = new List<(EquipmentElement, int)>();

        private static int _dZeroed, _dOver, _dSponge, _dSales, _dPoor, _dStumbles, _stumblesAll;
        private static readonly int[] _dSold = new int[2];   // [0] strzaly, [1] belty
        private static long _dRefund, _dGot;
        private static readonly HashSet<Town> _dTowns = new HashSet<Town>();
        private static readonly HashSet<string> _errSeen = new HashSet<string>();

        private static void NewDay()
        {
            _dZeroed = 0; _dOver = 0; _dSponge = 0; _dSales = 0; _dPoor = 0; _dStumbles = 0;
            _dSold[0] = 0; _dSold[1] = 0; _dRefund = 0; _dGot = 0; _dTowns.Clear();
        }

        /// <summary>Nowa gra albo wczytanie: kategorie i liczniki poprzedniej kampanii nie przeciekaja do nastepnej.</summary>
        internal static void Reset()
        {
            _cats.Clear(); _resolved = false; _stumblesAll = 0; _errSeen.Clear();
            NewDay();
        }

        /// <summary>Potkniecie liczone (nie gasi reguly); pierwszy blad z danego miejsca idzie do logu raz na kampanie.</summary>
        private static void Stumble(string where, Exception e)
        {
            _dStumbles++; _stumblesAll++;
            if (_errSeen.Add(where)) Log.Error("CaravanAmmo." + where, e);
        }

        /// <summary>Kategorie amunicji: kategoria strzal gry i kazda kategoria przedmiotu typu Arrows albo Bolts. Liczone raz na kampanie,
        /// gdy przedmioty sa juz wczytane.</summary>
        private static bool Ready()
        {
            if (_resolved) return _cats.Count > 0;
            var om = MBObjectManager.Instance;
            if (om == null) return false;
            var items = om.GetObjectTypeList<ItemObject>();
            if (items == null || items.Count == 0) return false;      // przedmioty jeszcze nie wczytane - sprobujemy przy nastepnym wywolaniu
            _cats.Clear();
            try { if (DefaultItemCategories.Arrows != null) _cats.Add(DefaultItemCategories.Arrows); } catch { }
            foreach (var it in items)
                if (it != null && it.ItemCategory != null && (it.ItemType == ItemObject.ItemTypeEnum.Arrows || it.ItemType == ItemObject.ItemTypeEnum.Bolts))
                    _cats.Add(it.ItemCategory);
            _resolved = true;
            return _cats.Count > 0;
        }

        private static bool IsAmmo(ItemObject it) { return it != null && it.ItemCategory != null && _cats.Contains(it.ItemCategory); }
        private static int Kind(ItemObject it) { return it.ItemType == ItemObject.ItemTypeEnum.Bolts ? 1 : 0; }

        private static bool On()
        {
            var s = Settings.Current;
            return s != null && s.CaravansNoAmmoTrade && Ready();
        }

        /// <summary>Karawana, ktora naprawde handluje (ten sam warunek co CaravanBulk): przed startem kampanii SellItemsAction nie przenosi
        /// zlota karawan; karawany posilkow DTE (DoNotMakeNewDecisions) nie handluja.</summary>
        private static bool Trades(MobileParty mp)
        {
            return Campaign.Current != null && Campaign.Current.GameStarted
                   && mp.IsActive && mp.IsPartyTradeActive && mp.MapEvent == null && mp.ItemRoster != null && mp.Party != null
                   && (mp.Ai == null || !mp.Ai.DoNotMakeNewDecisions);
        }

        /// <summary>Amunicja w taborach (pozycja z modyfikatorem osobno). null = brak.</summary>
        private static List<(EquipmentElement, int)> Ammo(ItemRoster r)
        {
            List<(EquipmentElement, int)> list = null;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                if (el.Amount <= 0 || !IsAmmo(el.EquipmentElement.Item)) continue;
                if (list == null) list = new List<(EquipmentElement, int)>();
                list.Add((el.EquipmentElement, el.Amount));
            }
            return list;
        }

        private static int CountOf(ItemRoster r, EquipmentElement what)
        {
            int at = r.FindIndexOfElement(what);
            return at < 0 ? 0 : r.GetElementNumber(at);
        }

        // ------------------------------------------------------------ 1) wycena zakupu BK
        /// <summary>Postfiks BKCaravansBehavior.CalculateBuyValue: kategoria amunicji nie ma dla karawany wyceny zakupu.</summary>
        public static void BuyValuePostfix(ItemCategory __0, ref float __result)
        {
            if (__result <= 0f || __0 == null) return;
            try
            {
                if (!On() || !_cats.Contains(__0)) return;
                if (__result >= BkBuyFloor) _dOver++;
                __result = 0f;
                _dZeroed++;
            }
            catch (Exception e) { Stumble("BuyValuePostfix", e); }
        }

        // ------------------------------------------------------------ 2) gabka BK przy wyjezdzie
        /// <summary>Prefiks BKPartyBehavior.OnSettlementLeft: stan amunicji w taborach przed gabka.</summary>
        public static void LeftPrefix(MobileParty __0, Settlement __1, out List<(EquipmentElement, int)> __state)
        {
            __state = null;
            if (__0 == null || __1 == null || !__0.IsCaravan || __1.Town == null || __0.ItemRoster == null) return;   // ta sama bramka co w gabce BK
            try
            {
                if (!On()) return;
                __state = Ammo(__0.ItemRoster) ?? NoneBefore;
            }
            catch (Exception e) { __state = null; Stumble("LeftPrefix", e); }
        }

        /// <summary>Postfiks gabki: amunicja, ktora BK dokupil, wraca na polke miasta, a zloto z kasy miasta do kiesy karawany.</summary>
        public static void LeftPostfix(MobileParty __0, Settlement __1, List<(EquipmentElement, int)> __state)
        {
            if (__state == null) return;
            try
            {
                var now = Ammo(__0.ItemRoster);
                if (now == null) return;
                var town = __1.Town;
                var pack = __0.ItemRoster;
                var shelf = town.Owner.ItemRoster;
                foreach (var (what, n) in now)
                {
                    int before = 0;
                    foreach (var b in __state) if (b.Item1.IsEqualTo(what)) { before = b.Item2; break; }
                    int extra = n - before;
                    if (extra <= 0) continue;
                    pack.AddToCounts(what, -extra);
                    shelf.AddToCounts(what, extra);
                    // polka wrocila do stanu sprzed gabki, wiec cena jest ta sama, ktora BK pomnozyl przez liczbe sztuk
                    int refund = Math.Min(town.Gold, (int)(extra * (float)town.GetItemPrice(what, __0, false)));
                    if (refund > 0) { town.ChangeGold(-refund); __0.PartyTradeGold += refund; _dRefund += refund; }
                    _dSponge += extra;
                }
            }
            catch (Exception e) { Stumble("LeftPostfix", e); }
        }

        // ------------------------------------------------------------ amunicja z taborow na polke miasta
        /// <summary>Sluchacz SettlementEntered: karawana handlowa wjezdza do miasta z amunicja - sprzedaje ja temu miastu.</summary>
        internal static void OnEntered(MobileParty mp, Settlement st, Hero hero)
        {
            if (mp == null || st == null || !mp.IsCaravan || !st.IsTown) return;   // tanie wyjscie: zdarzenie pada dla kazdej partii i kazdego bohatera
            try
            {
                if (st.Town == null || !On() || !Trades(mp)) return;
                var ammo = Ammo(mp.ItemRoster);
                if (ammo != null) Unload(mp, st.Town, ammo);
            }
            catch (Exception e) { Stumble("OnEntered", e); }
        }

        /// <summary>Sprzedaz miastu po cenie rynkowej (SellItemsAction: cena liczona sztuka po sztuce, kasa miasta placi kiesie karawany),
        /// tylko z kasy ponad rezerwe na renty. Cena spada z kazda sztuka na polce, wiec cena pierwszej jest gorna granica zaplaty.</summary>
        private static void Unload(MobileParty mp, Town town, List<(EquipmentElement, int)> ammo)
        {
            var s = Settings.Current;
            var pack = mp.ItemRoster;
            var shelf = town.Owner.ItemRoster;
            int reserve = (int)Math.Max(0f, s.TownRentFloorGold);
            List<(EquipmentElement, int)> done = null;
            foreach (var (what, _) in ammo)
            {
                int spare = town.Gold - reserve;
                if (spare <= 0) { _dPoor++; break; }      // miasto bez zlota ponad rezerwe nie kupuje - reszta w nastepnym miescie
                int at = pack.FindIndexOfElement(what);
                if (at < 0) continue;
                var el = pack.GetElementCopyAtIndex(at);
                if (el.Amount <= 0) continue;
                int price = Math.Max(1, town.GetItemPrice(what, mp, true));
                int n = Math.Min(el.Amount, spare / price);
                if (n <= 0) { _dPoor++; continue; }
                int purse = mp.PartyTradeGold, had = el.Amount;
                bool broke = false;
                try { SellItemsAction.Apply(mp.Party, town.Owner, el, n, town.Settlement); }
                catch (Exception e) { broke = true; Stumble("Unload(SellItemsAction)", e); }
                int moved = had - CountOf(pack, what);                    // liczymy to, co faktycznie zeszlo z taborow
                int got = Math.Max(0, mp.PartyTradeGold - purse);
                if (broke && moved > 0 && got == 0)                       // towar poszedl, zloto nie - cofamy
                {
                    try { shelf.AddToCounts(what, -moved); pack.AddToCounts(what, moved); moved = 0; }
                    catch (Exception e) { Stumble("Unload(cofniecie)", e); }
                }
                if (moved > 0)
                {
                    _dSold[Kind(what.Item)] += moved;
                    _dGot += got;
                    _dTowns.Add(town);
                    if (done == null) done = new List<(EquipmentElement, int)>();
                    done.Add((what, moved));
                }
                if (broke) break;                                         // po bledzie akcji gry nie handlujemy dalej w tej wizycie
            }
            if (done == null) return;
            _dSales++;
            try { CampaignEventDispatcher.Instance.OnCaravanTransactionCompleted(mp, town, done); } catch { }   // jak BK po sprzedazy: dymek nad miastem
        }

        // ------------------------------------------------------------ linie logu
        /// <summary>Linia startowa sesji: kategorie amunicji (strzaly, belty, inne przedmioty w tych kategoriach) i stan latek.</summary>
        internal static void SessionStart()
        {
            try
            {
                _resolved = false;      // przedmioty tej kampanii
                var s = Settings.Current;
                string on = s != null && s.CaravansNoAmmoTrade ? "WLACZONE" : "WYLACZONE (CaravansNoAmmoTrade)";
                if (!Ready()) { Log.Info("Karawany bez amunicji (172b): " + on + " - BRAK kategorii amunicji (przedmioty niewczytane?)."); return; }
                int arrows = 0, bolts = 0, other = 0;
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (!IsAmmo(it)) continue;
                    if (it.ItemType == ItemObject.ItemTypeEnum.Arrows) arrows++;
                    else if (it.ItemType == ItemObject.ItemTypeEnum.Bolts) bolts++;
                    else other++;
                }
                var names = new List<string>();
                foreach (var c in _cats) names.Add(c.StringId);
                Log.Info("Karawany bez amunicji (172b): " + on + " - kategorie amunicji: " + string.Join(", ", names.ToArray())
                         + " (przedmioty: strzaly " + arrows + ", belty " + bolts + ", inne " + other + ")"
                         + "; latki BK: wycena zakupu karawan (CalculateBuyValue) " + (_wiredValue ? "wpieta" : "BRAK")
                         + ", gabka przy wyjezdzie (OnSettlementLeft) " + (_wiredLeft ? "wpieta" : "BRAK")
                         + "; amunicja z taborow na polke miasta przy wjezdzie (kasa miasta ponad rezerwe "
                         + (s != null ? ((int)Math.Max(0f, s.TownRentFloorGold)).ToString() : "?") + " d).");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        /// <summary>Linia dnia: zablokowane zakupy, cofnieta gabka, sprzedane z taborow, amunicja w taborach razem (raz na dobe).</summary>
        internal static void Daily()
        {
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                int day = (int)CampaignTime.Now.ToDays - 1;
                int[] held = new int[2];
                int carriers = 0, caravans = 0, idle = 0;
                if (Ready())
                {
                    foreach (var mp in MobileParty.AllCaravanParties)
                    {
                        if (mp == null || mp.ItemRoster == null) continue;
                        caravans++;
                        var r = mp.ItemRoster;
                        int here = 0;
                        for (int i = 0; i < r.Count; i++)
                        {
                            var el = r.GetElementCopyAtIndex(i);
                            var it = el.EquipmentElement.Item;
                            if (el.Amount <= 0 || !IsAmmo(it)) continue;
                            held[Kind(it)] += el.Amount;
                            here += el.Amount;
                        }
                        if (here <= 0) continue;
                        carriers++;
                        if (mp.Ai != null && mp.Ai.DoNotMakeNewDecisions) idle += here;
                    }
                }
                var sb = new StringBuilder();
                sb.Append("Karawany bez amunicji (172b): dzien ").Append(day);
                if (!s.CaravansNoAmmoTrade) sb.Append(" - WYLACZONE (CaravansNoAmmoTrade), karawany handluja amunicja jak w BK");
                else
                    sb.Append(" - zakupy amunicji przez karawany zablokowane: wyceny BK wyzerowane ").Append(_dZeroed)
                      .Append(" (w tym powyzej progu zakupu BK ").Append(_dOver).Append(")")
                      .Append("; gabka BK cofnieta ").Append(_dSponge).Append(" snopow (zwrot ").Append(_dRefund).Append(" d)")
                      .Append("; sprzedane z taborow do miast: strzaly ").Append(_dSold[0]).Append(", belty ").Append(_dSold[1])
                      .Append(" za ").Append(_dGot).Append(" d w ").Append(_dTowns.Count).Append(" miastach (wizyt ").Append(_dSales)
                      .Append(", kasa miasta ponizej rezerwy ").Append(_dPoor).Append(" razy)");
                sb.Append("; w taborach razem: strzaly ").Append(held[0]).Append(", belty ").Append(held[1])
                  .Append(" w ").Append(carriers).Append(" z ").Append(caravans).Append(" karawan");
                if (idle > 0) sb.Append(" (w tym w karawanach bez handlu, np. posilki DTE: ").Append(idle).Append(")");
                if (!_wiredValue || !_wiredLeft)
                    sb.Append("; BRAK latki BK: ").Append(!_wiredValue ? "CalculateBuyValue " : "").Append(!_wiredLeft ? "OnSettlementLeft" : "");
                sb.Append("; potkniecia ").Append(_dStumbles).Append(" (od startu ").Append(_stumblesAll).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { NewDay(); }
        }

        // ------------------------------------------------------------ latki
        private static bool Fits(MethodInfo m, Type ret, params Type[] args)
        {
            if (m == null || m.IsStatic || m.ReturnType != ret) return false;
            var p = m.GetParameters();
            if (p.Length != args.Length) return false;
            for (int i = 0; i < p.Length; i++) if (p[i].ParameterType != args[i]) return false;
            return true;
        }

        /// <summary>Latki na dwie prywatne metody BK; parametry po pozycji, typy sprawdzane. Kazda latka osobno - brak jednej nie gasi drugiej.</summary>
        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var bkc = AccessTools.TypeByName("BannerKings.Behaviours.BKCaravansBehavior");
                var bkp = AccessTools.TypeByName("BannerKings.Behaviours.BKPartyBehavior");
                var value = bkc != null ? AccessTools.Method(bkc, "CalculateBuyValue") : null;
                var left = bkp != null ? AccessTools.Method(bkp, "OnSettlementLeft") : null;
                if (Fits(value, typeof(float), typeof(ItemCategory), typeof(Town), typeof(float), typeof(float)))
                {
                    h.Patch(value, postfix: new HarmonyMethod(typeof(CaravanAmmo), nameof(BuyValuePostfix)));
                    _wiredValue = true;
                }
                if (Fits(left, typeof(void), typeof(MobileParty), typeof(Settlement)))
                {
                    h.Patch(left, prefix: new HarmonyMethod(typeof(CaravanAmmo), nameof(LeftPrefix)), postfix: new HarmonyMethod(typeof(CaravanAmmo), nameof(LeftPostfix)));
                    _wiredLeft = true;
                }
                Log.Info("CaravanAmmo (172b): latki BK - wycena zakupu karawan (BKCaravansBehavior.CalculateBuyValue) " + (_wiredValue ? "wpieta" : "BRAK albo inna sygnatura")
                         + ", gabka przy wyjezdzie (BKPartyBehavior.OnSettlementLeft) " + (_wiredLeft ? "wpieta" : "BRAK albo inna sygnatura")
                         + "; sprzedaz amunicji z taborow przy wjezdzie do miasta - wlasny sluchacz (bez BK).");
            }
            catch (Exception e) { Log.Error("CaravanAmmo.ApplyAll", e); }
        }
    }
}
