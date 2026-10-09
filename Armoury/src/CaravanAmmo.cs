using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Extensions;
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
    /// DefaultItemCategories.Arrows), wiec strzaly i belty to jedna kategoria (z RBM takze jego kamienie do procy - Type="SlingStones").
    /// Drogi, ktorymi karawana bierze towar do taborow (dekompilacje gry, BK, BEE, BKROTPatch, DTE, ROT, Spoils of War, AIInfluence):
    ///  1) BK BKCaravansBehavior.BuyGoods - przy kazdej decyzji karawany (HourlyTickPartyImpl) i w kursach wstepnych nowej gry
    ///     (DoInitialTradeRuns): 5 kategorii z najwieksza CalculateBuyValue, potem BuyCategory (znow CalculateBuyValue; ponizej 7 nic).
    ///     Jedna droga dla WSZYSTKICH karawan - notabli, rodow lordow i rodu gracza (BK ich nie rozroznia; rozkaz gracza "zaopatruj
    ///     miasto w zywnosc" to tylko filtr kategorii przed ta sama wycena). ZAMKNIETA: postfiks CalculateBuyValue - kategoria amunicji
    ///     ma wycene 0, wiec nie wchodzi do piatki i BuyCategory nic nie kupuje. Blokada obejmuje cala kategorie (wycena BK jest po
    ///     kategorii) - z RBM takze kamienie do procy.
    ///  2) BK BKPartyBehavior.OnSettlementLeft ("gabka"): przy wyjezdzie z miasta kazdy towar tanszy niz 33% wartosci, do 80% udzwigu -
    ///     bez wyceny kategorii, wiec latka 1 go nie lapie (snopy sprzedane miastu tanieja na jego polce). ZAMKNIETA: prefiks zapisuje
    ///     pozycje amunicji z polki miasta w kolejnosci polki (te sama, w ktorej BK kupuje) i ile ich bylo w taborach; postfiks oddaje
    ///     dokupione od konca polki, a zloto z kasy miasta do kiesy karawany. BK wycenial kazda pozycje na polce pomniejszonej o
    ///     poprzednie, wiec zwrot ostatniej pierwszy odtwarza dokladnie ten stan - kazdy zwrot ma cene zakupu (wzor CaravanBulk.LeftPostfix).
    ///  3) Gra CaravansCampaignBehavior.BuyGoods (kategorie IsTradeGood albo IsAnimal) - martwa: BK wylacza HourlyTickParty i
    ///     DoInitialTradeRuns gry (CaravansCampaignBehavior_HourlyTickParty_Skip, _DoInitialTradeRuns_Skip). Sprzedaz gry i BK przy
    ///     wjezdzie zostaje - pomaga oprozniac tabory.
    ///  4) BEE CaravanCampaignBehavior.TryDeliverContract przenosi towar ze wsi albo z miasta prosto na polke miasta docelowego, nie przez
    ///     tabor; towary specjalnosci (SpecialtyGoods) i popyt wojskowy BEE nie maja amunicji; kontrakty zbrojeniowe BEE
    ///     (TownEconomyCampaignBehavior.TryGetArmamentContractForCaravan -> FindBestArmoryStockItem -> IsArmoryOutputItem) wykluczaja
    ///     Arrows, Bolts i SlingStones po ItemType - nic do zamykania.
    ///  5) Reszta: DTE dosypuje karawanom posilkow (DoNotMakeNewDecisions - nie handluja) jedzenie, juczne i po sztuce losowego sprzetu -
    ///     to nie handel (sprawa "z niczego" poza 172b, tu tylko liczona w linii dnia); Spoils, ROT, BKROTPatch, AIInfluence - zadnych
    ///     zakupow do taborow karawan.
    /// Cel podrozy BK (GetTradeScoreForTown) dolicza miastu "oplaca sie tu kupic" po kategoriach (CalculateTownBuyScoreForCategory) -
    /// tania amunicja na polce (stary zapas zrzucony w miescie, miasta strzelarzy 172) ciagnelaby karawany tam, gdzie i tak jej nie kupia.
    /// Postfiks tej metody: dla kategorii amunicji wynik 0 (jedno porownanie w tablicy po indeksie kategorii). Ocena "oplaca sie tu
    /// sprzedac" zostaje - prowadzi stary zapas do miast, w ktorych amunicja jest droga (brakuje jej).
    /// Amunicja juz lezaca w taborach (stary zapis, lupy): przy kazdym wjezdzie karawany handlowej do miasta idzie na polke tego miasta
    /// przez SellItemsAction - cena rynkowa sztuka po sztuce, zloto z kasy miasta do kiesy karawany, tylko z kasy ponad rezerwe na renty
    /// (TownRentFloorGold, jak przy surowcach CaravanBulk); czego miasto nie udzwignie, idzie w nastepnym miescie. Nic nie powstaje, nic
    /// nie znika. Tylko przedmioty typu amunicji (Arrows, Bolts, SlingStones, Bullets) z kategorii amunicji. Nasz sluchacz wjazdu idzie
    /// po CaravanBulk (surowce, ktorych miastu brakuje, maja pierwszenstwo do kasy miasta), ale przed sprzedaza BK i gry (zdarzenia gry
    /// wolaja sluchaczy od ostatnio dopisanego). Zostaje: amunicja w karawanach posilkow DTE (nie handluja) - liczona
    /// w linii dnia.
    /// Czego NIE ruszamy: zakupy amunicji przez lordow i zalogi (AiGear, GarrisonArmory), strzelarze 172 (TownFletchers), flaga towaru
    /// kategorii (od niej zaleza ceny, popyt i budzety BK).
    /// Wylacznik CaravansNoAmmoTrade (domyslnie wlaczony): wylaczony = karawany handluja amunicja jak w BK; linia dnia zostaje.
    /// </summary>
    internal static class CaravanAmmo
    {
        private const float BkBuyFloor = 7f;   // BKCaravansBehavior.BuyCategory: wycena ponizej 7 - BK nic nie kupuje

        private static readonly HashSet<ItemCategory> _cats = new HashSet<ItemCategory>();   // kategorie amunicji tej kampanii
        private static bool[] _ammoIx;          // indeks w ItemCategories.All -> kategoria amunicji (dla oceny celu BK)
        private static bool _resolved;
        private static bool _wiredValue, _wiredLeft, _wiredGoods, _wiredScore;
        private static bool _overSeen;          // w biezacym BuyGoods amunicja juz przekroczyla prog (liczymy decyzje, nie wyceny)

        private static int _dZeroed, _dOver, _dScore, _dSponge, _dSales, _dPoor, _dStumbles, _stumblesAll;
        private static readonly int[] _dSold = new int[3];   // [0] strzaly, [1] belty, [2] kamienie do procy i kule
        private static long _dRefund, _dGot;
        private static readonly HashSet<Town> _dTowns = new HashSet<Town>();
        private static readonly HashSet<string> _errSeen = new HashSet<string>();

        private static void NewDay()
        {
            _dZeroed = 0; _dOver = 0; _dScore = 0; _dSponge = 0; _dSales = 0; _dPoor = 0; _dStumbles = 0;
            _dSold[0] = 0; _dSold[1] = 0; _dSold[2] = 0; _dRefund = 0; _dGot = 0; _dTowns.Clear();
        }

        /// <summary>Nowa gra albo wczytanie: kategorie i liczniki poprzedniej kampanii nie przeciekaja do nastepnej.</summary>
        internal static void Reset()
        {
            _cats.Clear(); _ammoIx = null; _resolved = false; _overSeen = false; _stumblesAll = 0; _errSeen.Clear();
            NewDay();
        }

        /// <summary>Potkniecie liczone (nie gasi reguly); pierwszy blad z danego miejsca idzie do logu raz na kampanie.</summary>
        private static void Stumble(string where, Exception e)
        {
            _dStumbles++; _stumblesAll++;
            if (_errSeen.Add(where)) Log.Error("CaravanAmmo." + where, e);
        }

        /// <summary>Kategorie amunicji: kategoria strzal gry i kazda kategoria przedmiotu typu Arrows albo Bolts. Liczone raz na kampanie,
        /// gdy przedmioty sa juz wczytane. Przy okazji tablica indeksow ItemCategories.All dla oceny celu BK.</summary>
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
            _ammoIx = null;
            try
            {
                var all = Campaign.Current != null ? ItemCategories.All : null;
                if (all != null)
                {
                    var ix = new bool[all.Count];
                    for (int i = 0; i < ix.Length; i++) ix[i] = all[i] != null && _cats.Contains(all[i]);
                    _ammoIx = ix;
                }
            }
            catch (Exception e) { _ammoIx = null; Stumble("Ready(ItemCategories.All)", e); }
            _resolved = true;
            return _cats.Count > 0;
        }

        /// <summary>Rodzaj amunicji po typie przedmiotu: 0 strzaly, 1 belty, 2 kamienie do procy i kule; -1 = nie amunicja.</summary>
        private static int Kind(ItemObject it)
        {
            switch (it.ItemType)
            {
                case ItemObject.ItemTypeEnum.Arrows: return 0;
                case ItemObject.ItemTypeEnum.Bolts: return 1;
                case ItemObject.ItemTypeEnum.SlingStones:
                case ItemObject.ItemTypeEnum.Bullets: return 2;
                default: return -1;
            }
        }

        /// <summary>Amunicja w rozumieniu 172b: przedmiot typu amunicji z kategorii amunicji. Co innego w tej kategorii (dzis nic) ma tylko
        /// blokade wyceny BK (po kategorii), taborow nie ruszamy.</summary>
        private static bool IsAmmo(ItemObject it) { return it != null && it.ItemCategory != null && Kind(it) >= 0 && _cats.Contains(it.ItemCategory); }

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
                   && (mp.Ai == null || !mp.Ai.DoNotMakeNewDecisions || MaterialOrders.HasContract(mp));   // 174.2: karawana z kontraktem surowca handluje
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
        /// <summary>Prefiks BKCaravansBehavior.BuyGoods: nowa decyzja zakupu karawany - licznik "ponad progiem" liczy ja najwyzej raz.</summary>
        public static void BuyGoodsPrefix() { _overSeen = false; }

        /// <summary>Postfiks BKCaravansBehavior.CalculateBuyValue: kategoria amunicji nie ma dla karawany wyceny zakupu.</summary>
        public static void BuyValuePostfix(ItemCategory __0, ref float __result)
        {
            if (__result <= 0f || __0 == null) return;
            try
            {
                if (!On() || !_cats.Contains(__0)) return;
                // ta sama decyzja wycenia amunicje dwa razy (piatka MaxElements5, potem BuyCategory) - liczymy decyzje, nie wyceny;
                // bez latki BuyGoods znacznik sie nie zeruje, wiec liczymy kazda wycene ponad progiem
                if (__result >= BkBuyFloor && !(_wiredGoods && _overSeen)) { _dOver++; _overSeen = true; }
                __result = 0f;
                _dZeroed++;
            }
            catch (Exception e) { Stumble("BuyValuePostfix", e); }
        }

        /// <summary>Postfiks BKCaravansBehavior.CalculateTownBuyScoreForCategory (ocena miasta jako celu): amunicji karawana tu nie kupi,
        /// wiec nie dolicza miastu "oplaca sie tu kupic". Najtansze wyjscia najpierw - metoda leci na miasto x kategorie x decyzje.</summary>
        public static void TownBuyScorePostfix(int __1, ref float __result)
        {
            if (__result <= 0f) return;
            var ix = _ammoIx;
            if (ix == null || __1 < 0 || __1 >= ix.Length || !ix[__1]) return;
            try
            {
                if (!On()) return;
                __result = 0f;
                _dScore++;
            }
            catch (Exception e) { Stumble("TownBuyScorePostfix", e); }
        }

        // ------------------------------------------------------------ 2) gabka BK przy wyjezdzie
        /// <summary>Prefiks BKPartyBehavior.OnSettlementLeft: pozycje amunicji z polki miasta w kolejnosci polki (w tej kolejnosci kupuje
        /// gabka) i ile kazdej bylo w taborach przed gabka. Polka bez amunicji = gabka nie ma czego dokupic, postfiks nic nie robi.</summary>
        public static void LeftPrefix(MobileParty __0, Settlement __1, out List<(EquipmentElement, int)> __state)
        {
            __state = null;
            if (__0 == null || __1 == null || !__0.IsCaravan || __1.Town == null || __0.ItemRoster == null) return;   // ta sama bramka co w gabce BK
            long tc = Cost174.Begin(Cost174.SCaravanAmmo);   // 174b.5 F6 (probka 1/16, tylko log)
            try
            {
                if (!On()) return;
                var shelf = __1.Town.Owner.ItemRoster;     // BK iteruje settlement.Party.ItemRoster - ten sam spis
                if (shelf == null) return;
                var pack = __0.ItemRoster;
                List<(EquipmentElement, int)> list = null;
                for (int i = 0; i < shelf.Count; i++)
                {
                    var el = shelf.GetElementCopyAtIndex(i);
                    if (el.Amount <= 0 || !IsAmmo(el.EquipmentElement.Item)) continue;
                    if (list == null) list = new List<(EquipmentElement, int)>();
                    list.Add((el.EquipmentElement, CountOf(pack, el.EquipmentElement)));
                }
                __state = list;
            }
            catch (Exception e) { __state = null; Stumble("LeftPrefix", e); }
            finally { Cost174.End(Cost174.SCaravanAmmo, tc); }
        }

        /// <summary>Postfiks gabki: amunicja, ktora BK dokupil, wraca na polke miasta, a zloto z kasy miasta do kiesy karawany.</summary>
        public static void LeftPostfix(MobileParty __0, Settlement __1, List<(EquipmentElement, int)> __state)
        {
            if (__state == null) return;
            try
            {
                var town = __1.Town;
                var pack = __0.ItemRoster;
                var shelf = town.Owner.ItemRoster;
                // od konca polki: BK wycenial pozycje k na polce bez pozycji 0..k-1 (cena zalezy od zapasu calej kategorii), wiec gdy
                // zwracamy od ostatniej, przy kazdym zwrocie polka jest dokladnie taka, jak przy jej zakupie - zwrot = zaplata BK
                for (int k = __state.Count - 1; k >= 0; k--)
                {
                    var (what, before) = __state[k];
                    int extra = CountOf(pack, what) - before;
                    if (extra <= 0) continue;
                    pack.AddToCounts(what, -extra);
                    shelf.AddToCounts(what, extra);
                    int refund = Math.Min(town.Gold, (int)(extra * (float)town.GetItemPrice(what, __0, false)));   // to samo wyrazenie co w BK
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
            long tc = Cost174.Begin(Cost174.SCaravanAmmo);   // 174b.5 F6 (probka 1/16, tylko log)
            try
            {
                if (st.Town == null || !On() || !Trades(mp)) return;
                var ammo = Ammo(mp.ItemRoster);
                if (ammo != null) Unload(mp, st.Town, ammo);
            }
            catch (Exception e) { Stumble("OnEntered", e); }
            finally { Cost174.End(Cost174.SCaravanAmmo, tc); }
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
            bool poor = false;                                            // ta wizyta zostawila amunicje z braku kasy miasta (licznik raz na wizyte)
            foreach (var (what, _) in ammo)
            {
                int spare = town.Gold - reserve;
                if (spare <= 0) { poor = true; break; }                   // miasto bez zlota ponad rezerwe nie kupuje - reszta w nastepnym miescie
                int at = pack.FindIndexOfElement(what);
                if (at < 0) continue;
                var el = pack.GetElementCopyAtIndex(at);
                if (el.Amount <= 0) continue;
                int price = Math.Max(1, town.GetItemPrice(what, mp, true));
                int n = Math.Min(el.Amount, spare / price);
                if (n <= 0) { poor = true; continue; }
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
                    _dSold[Kind(what.Item)] += moved;                     // Ammo() przepuszcza tylko Kind >= 0
                    _dGot += got;
                    _dTowns.Add(town);
                    if (done == null) done = new List<(EquipmentElement, int)>();
                    done.Add((what, moved));
                }
                if (broke) break;                                         // po bledzie akcji gry nie handlujemy dalej w tej wizycie
            }
            if (poor) _dPoor++;
            if (done == null) return;
            _dSales++;
            try { CampaignEventDispatcher.Instance.OnCaravanTransactionCompleted(mp, town, done); } catch { }   // jak BK po sprzedazy: dymek nad miastem
        }

        // ------------------------------------------------------------ linie logu
        /// <summary>Linia startowa sesji: kategorie amunicji (strzaly, belty, kamienie i kule, inne przedmioty w tych kategoriach) i latki.</summary>
        internal static void SessionStart()
        {
            try
            {
                _resolved = false;      // przedmioty tej kampanii
                var s = Settings.Current;
                string on = s != null && s.CaravansNoAmmoTrade ? "WLACZONE" : "WYLACZONE (CaravansNoAmmoTrade)";
                if (!Ready()) { Log.Info("Karawany bez amunicji (172b): " + on + " - BRAK kategorii amunicji (przedmioty niewczytane?)."); return; }
                var n = new int[4];     // strzaly, belty, kamienie i kule, inne (nie amunicja)
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (it == null || it.ItemCategory == null || !_cats.Contains(it.ItemCategory)) continue;
                    int k = Kind(it);
                    n[k >= 0 ? k : 3]++;
                }
                var names = new List<string>();
                foreach (var c in _cats) names.Add(c.StringId);
                Log.Info("Karawany bez amunicji (172b): " + on + " - kategorie amunicji: " + string.Join(", ", names.ToArray())
                         + " (przedmioty: strzaly " + n[0] + ", belty " + n[1] + ", kamienie do procy i kule " + n[2]
                         + ", inne " + n[3] + " - te tylko bez wyceny zakupu BK, taborow nie ruszamy)"
                         + "; latki BK: wycena zakupu karawan (CalculateBuyValue) " + (_wiredValue ? "wpieta" : "BRAK")
                         + ", licznik decyzji (BuyGoods) " + (_wiredGoods ? "wpiety" : "BRAK")
                         + ", ocena celu (CalculateTownBuyScoreForCategory) " + (_wiredScore ? "wpieta" : "BRAK")
                         + (_ammoIx == null ? " (BRAK tablicy kategorii)" : "")
                         + ", gabka przy wyjezdzie (OnSettlementLeft) " + (_wiredLeft ? "wpieta" : "BRAK")
                         + "; amunicja z taborow na polke miasta przy wjezdzie (kasa miasta ponad rezerwe "
                         + (s != null ? ((int)Math.Max(0f, s.TownRentFloorGold)).ToString() : "?") + " d).");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        /// <summary>Linia dnia: zablokowane zakupy, ocena celu, cofnieta gabka, sprzedane z taborow, amunicja w taborach razem (raz na dobe).</summary>
        internal static void Daily()
        {
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                int day = (int)CampaignTime.Now.ToDays - 1;
                int[] held = new int[3];
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
                        if (mp.Ai != null && mp.Ai.DoNotMakeNewDecisions && !MaterialOrders.HasContract(mp)) idle += here;
                    }
                }
                var sb = new StringBuilder();
                sb.Append("Karawany bez amunicji (172b): dzien ").Append(day);
                if (!s.CaravansNoAmmoTrade) sb.Append(" - WYLACZONE (CaravansNoAmmoTrade), karawany handluja amunicja jak w BK");
                else
                {
                    sb.Append(" - zablokowane zakupy amunicji przez karawany: decyzje BK z amunicja ponad progiem zakupu ").Append(_dOver)
                      .Append(" (gorna granica - kupilby, gdyby weszla do piatki; wyceny wyzerowane ").Append(_dZeroed).Append(")")
                      .Append("; ocena celu bez 'tu kupie amunicje' ").Append(_dScore).Append(" wycen miast")
                      .Append("; gabka BK cofnieta ").Append(_dSponge).Append(" snopow (zwrot ").Append(_dRefund).Append(" d)")
                      .Append("; sprzedane z taborow do miast: strzaly ").Append(_dSold[0]).Append(", belty ").Append(_dSold[1]);
                    if (_dSold[2] > 0) sb.Append(", kamienie i kule ").Append(_dSold[2]);
                    sb.Append(" za ").Append(_dGot).Append(" d w ").Append(_dTowns.Count).Append(" miastach (wizyt ze sprzedaza ").Append(_dSales)
                      .Append(", wizyt z amunicja zostawiona z braku kasy miasta ").Append(_dPoor).Append(")");
                }
                sb.Append("; w taborach razem: strzaly ").Append(held[0]).Append(", belty ").Append(held[1]);
                if (held[2] > 0) sb.Append(", kamienie i kule ").Append(held[2]);
                sb.Append(" w ").Append(carriers).Append(" z ").Append(caravans).Append(" karawan");
                if (idle > 0) sb.Append(" (w tym w karawanach bez handlu, np. posilki DTE: ").Append(idle).Append(")");
                if (!_wiredValue || !_wiredLeft || !_wiredGoods || !_wiredScore)
                    sb.Append("; BRAK latki BK: ").Append(!_wiredValue ? "CalculateBuyValue " : "").Append(!_wiredGoods ? "BuyGoods " : "")
                      .Append(!_wiredScore ? "CalculateTownBuyScoreForCategory " : "").Append(!_wiredLeft ? "OnSettlementLeft" : "");
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

        /// <summary>Jedna latka we wlasnym try - wyjatek Harmony przy jednej nie gasi pozostalych.</summary>
        private static bool Hook(Harmony h, MethodInfo m, string prefix, string postfix, string what)
        {
            try
            {
                h.Patch(m, prefix: prefix != null ? new HarmonyMethod(typeof(CaravanAmmo), prefix) : null,
                           postfix: postfix != null ? new HarmonyMethod(typeof(CaravanAmmo), postfix) : null);
                return true;
            }
            catch (Exception e) { Log.Error("CaravanAmmo.ApplyAll(" + what + ")", e); return false; }
        }

        private static MethodInfo Find(Type t, string name, params Type[] args)
        {
            try { return t != null ? AccessTools.Method(t, name, args) : null; }
            catch (Exception e) { Log.Error("CaravanAmmo.ApplyAll(" + name + ")", e); return null; }
        }

        /// <summary>Latki na prywatne metody BK; parametry po pozycji, typy sprawdzane. Kazda latka osobno - brak albo blad jednej nie
        /// gasi pozostalych.</summary>
        internal static void ApplyAll(Harmony h)
        {
            Type bkc = null, bkp = null;
            try
            {
                bkc = AccessTools.TypeByName("BannerKings.Behaviours.BKCaravansBehavior");
                bkp = AccessTools.TypeByName("BannerKings.Behaviours.BKPartyBehavior");
            }
            catch (Exception e) { Log.Error("CaravanAmmo.ApplyAll(typy BK)", e); }
            var value = Find(bkc, "CalculateBuyValue", typeof(ItemCategory), typeof(Town), typeof(float), typeof(float));
            var goods = Find(bkc, "BuyGoods", typeof(MobileParty), typeof(Town));
            var score = Find(bkc, "CalculateTownBuyScoreForCategory", typeof(TownMarketData), typeof(int));
            var left = Find(bkp, "OnSettlementLeft", typeof(MobileParty), typeof(Settlement));
            // licznik decyzji przed wycena: gdy wycena sie wepnie, a BuyGoods nie, BuyValuePostfix liczy kazda wycene ponad progiem
            if (Fits(goods, typeof(void), typeof(MobileParty), typeof(Town)))
                _wiredGoods = Hook(h, goods, nameof(BuyGoodsPrefix), null, "BuyGoods");
            if (Fits(value, typeof(float), typeof(ItemCategory), typeof(Town), typeof(float), typeof(float)))
                _wiredValue = Hook(h, value, null, nameof(BuyValuePostfix), "CalculateBuyValue");
            if (Fits(score, typeof(float), typeof(TownMarketData), typeof(int)))
                _wiredScore = Hook(h, score, null, nameof(TownBuyScorePostfix), "CalculateTownBuyScoreForCategory");
            if (Fits(left, typeof(void), typeof(MobileParty), typeof(Settlement)))
                _wiredLeft = Hook(h, left, nameof(LeftPrefix), nameof(LeftPostfix), "OnSettlementLeft");
            Log.Info("CaravanAmmo (172b): latki BK - wycena zakupu karawan (BKCaravansBehavior.CalculateBuyValue) " + (_wiredValue ? "wpieta" : "BRAK albo inna sygnatura")
                     + ", licznik decyzji (BKCaravansBehavior.BuyGoods) " + (_wiredGoods ? "wpiety" : "BRAK albo inna sygnatura")
                     + ", ocena celu (BKCaravansBehavior.CalculateTownBuyScoreForCategory) " + (_wiredScore ? "wpieta" : "BRAK albo inna sygnatura")
                     + ", gabka przy wyjezdzie (BKPartyBehavior.OnSettlementLeft) " + (_wiredLeft ? "wpieta" : "BRAK albo inna sygnatura")
                     + "; sprzedaz amunicji z taborow przy wjezdzie do miasta - wlasny sluchacz (bez BK).");
        }
    }
}
