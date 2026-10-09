using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// PACZKA 174.2 - ZAMOWIENIA SUROWCA: KONTRAKT DLA PRAWDZIWEJ KARAWANY (docs/PROJEKT-174-PRODUKCJA-UZBROJENIA-2026-10-09.md rozdz. 3.3; decyzja
    /// Jeffa 05.10: "karawany rozwoza surowce masowe tam, gdzie brakuje - prawdziwe partie na mapie, nie niewidzialny przerzut").
    /// A171: 38 miast bez rudy, 30 z nadwyzka, w miescie bez rudy pierwszy ladunek po 36-37 dobach; warsztat bierze surowiec tylko z polki
    /// wlasnego miasta; proba zmiany celu karawan BK nie zmienila liczby miast bez rudy (CaravanBulk.cs:73-76).
    /// Regula: miasto, w ktorym wczoraj warsztaty zbrojne, strzelarze (172) albo rzemioslo miasta (148) odpuscili cykl z braku surowca m, zamawia go
    /// (najwyzej raz na TownMaterialOrderDays dob) w pobliskim niewrogim miescie albo zamku, ktore ma go ponad prog nadwyzki. Towar wiezie
    /// karawana BK STOJACA w zrodle: kupuje go tam po cenie targu zrodla (SellItemsAction - zloto karawany do kasy zrodla), jedzie do zamawiajacego
    /// (bandy moga ja rozbic - lup jak w grze) i tam sprzedaje po cenie targu (kasa miasta ponad rezerwe TownRentFloorGold -> karawana).
    /// Kontrakt tylko przy oczekiwanej marzy >= koszt drogi (CarterPencePerKgPer100 od kg i odleglosci; morze x SeaFreightShare); brak karawany
    /// w zrodle = brak kontraktu. Ruda tylko z bliska (TownMaterialOrderRangeOre, 2-3 doby - daleko wozono sztaby, nie rude), reszta do
    /// TownMaterialOrderRange. Ile: zapas na 10 dob zuzycia miasta (wieksze z: zmierzone - srednia ok. 14 dob z warsztatow, strzelarzy i rzemiosla,
    /// i szacunek karawan z rak - CaravanBulk) minus polka minus w drodze; najwyzej wolne miejsce w jukach i kiesa karawany.
    /// Zrodlo sprzedaje tylko ponad max(Keep karawan wedlug dawnych rak, 10 dob wlasnego zuzycia); zamek - caly zapas (nie ma warsztatow).
    /// Karawana z kontraktem: Ai.SetDoNotMakeNewDecisions(true) i rozkaz jazdy (wzor posilkow DTE) do przyjazdu; CaravanBulk, karawany bez amunicji
    /// (172b) i latka wysp ja przepuszczaja; BK ReleaseCaravanFromHold (koniec oblezenia) nie zmienia jej celu. Wrogosc celu albo 30 dob -
    /// zwolnienie (ladunek zostaje karawanie - prawdziwy towar). Karawana gracza (i jego rodu) nigdy nie dostaje kontraktu.
    /// Zapis: "arm_matorders" (SaveText.Sync), po wczytaniu - kontrakt odtworzony albo karawana zwolniona.
    /// </summary>
    internal static class MaterialOrders
    {
        private static readonly string[] Ids = { "iron", "hardwood", "leather", "linen", "flax", "hides", "wool" };
        private static readonly string[] Names = { "ruda", "drewno", "skora", "plotno", "len", "skory surowe", "welna" };
        private const int M = 7, Ore = 0;
        private static ItemObject[] _items;
        private static readonly Dictionary<ItemObject, int> _ix = new Dictionary<ItemObject, int>();

        private sealed class Contract
        {
            public MobileParty Car; public string CarId; public Settlement Dest, Src; public int Mat, Qty, Day, Paid; public float Dist; public bool Naval;
        }
        private static readonly List<Contract> _contracts = new List<Contract>();
        private static readonly Dictionary<MobileParty, Contract> _byCar = new Dictionary<MobileParty, Contract>();
        private static readonly Dictionary<Town, int[]> _miss = new Dictionary<Town, int[]>();       // cykle "brak surowca" od ostatniej doby
        private static readonly Dictionary<Town, float[]> _useToday = new Dictionary<Town, float[]>(), _useAvg = new Dictionary<Town, float[]>();
        private static readonly Dictionary<Town, int[]> _last = new Dictionary<Town, int[]>();       // doba ostatniego zamowienia surowca
        private static string _pending;
        private static MethodInfo _setMove;
        internal static bool ReleaseHooked;

        // liczniki doby (linia "Kontrakty surowca (174)")
        private static readonly int[] _dMade = new int[M], _dMadeQty = new int[M];
        private static int _dDone, _dLost, _dLostQty, _dRelHost, _dRel30, _dRelOther, _dNoCar, _dNoSrc, _dNoGain, _dNoRoad, _dRejected, _dPause, _dEnough, _dRetarget, _dKeptTarget, _stumbles, _stumblesAll;
        private static long _dGold, _dMargin, _dPaidDest;
        private static double _dDays;
        private static readonly List<string> _dEx = new List<string>();
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        internal static bool On { get { var s = Settings.Current; return s != null && s.TownMaterialOrders; } }

        internal static void Reset()
        {
            _items = null; _ix.Clear(); _contracts.Clear(); _byCar.Clear(); _miss.Clear(); _useToday.Clear(); _useAvg.Clear(); _last.Clear(); _pending = null;
            NewDay(); _stumblesAll = 0; _errSites.Clear();
        }

        private static void NewDay()
        {
            Array.Clear(_dMade, 0, M); Array.Clear(_dMadeQty, 0, M);
            _dDone = _dLost = _dLostQty = _dRelHost = _dRel30 = _dRelOther = _dNoCar = _dNoSrc = _dNoGain = _dNoRoad = _dRejected = _dPause = _dEnough = _dRetarget = _dKeptTarget = _stumbles = 0;
            _dGold = _dMargin = _dPaidDest = 0; _dDays = 0; _dEx.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesAll++;
            if (_errSites.Add(where)) Log.Error("MaterialOrders." + where, e);
        }

        private static bool Ready()
        {
            if (_items != null) return true;
            var om = MBObjectManager.Instance;
            if (om == null) return false;
            var a = new ItemObject[M];
            for (int i = 0; i < M; i++) { try { a[i] = om.GetObject<ItemObject>(Ids[i]); } catch { } }
            if (a[Ore] == null) return false;
            _ix.Clear();
            for (int i = 0; i < M; i++) if (a[i] != null) _ix[a[i]] = i;
            _items = a;
            return true;
        }

        private static float Kg(ItemObject it) { return it != null && it.Weight > 0.05f ? it.Weight : 10f; }

        // ------------------------------------------------------------ sygnal i zuzycie (wolane z warsztatow, strzelarzy i rzemiosla miasta)
        /// <summary>Brak surowca w cyklu (bit 0 ruda, 1 drewno, 2 skora, 3 len/plotno - maska WorkshopLaw i TownFletchers).</summary>
        internal static void NoteMissMask(Town town, int mask)
        {
            if (town == null || mask == 0) return;
            try { for (int m = 0; m < 4; m++) if ((mask & (1 << m)) != 0) Bump(_miss, town, m); }
            catch (Exception e) { Stumble("NoteMissMask", e); }
        }

        internal static void NoteMiss(Town town, ItemObject item)
        {
            try { int m; if (town != null && item != null && Ready() && _ix.TryGetValue(item, out m)) Bump(_miss, town, m); }
            catch (Exception e) { Stumble("NoteMiss", e); }
        }

        private static void Bump(Dictionary<Town, int[]> d, Town t, int m)
        {
            int[] a; if (!d.TryGetValue(t, out a)) { a = new int[M]; d[t] = a; }
            a[m]++;
        }

        internal static void NoteUse(Town town, ItemObject item, int n)
        {
            try
            {
                int m;
                if (town == null || item == null || n <= 0 || !Ready() || !_ix.TryGetValue(item, out m)) return;
                float[] a; if (!_useToday.TryGetValue(town, out a)) { a = new float[M]; _useToday[town] = a; }
                a[m] += n;
            }
            catch (Exception e) { Stumble("NoteUse", e); }
        }

        /// <summary>Zuzycie dobowe surowca w miescie: wieksze z zmierzonego (srednia ok. 14 dob) i szacunku karawan z rak (CaravanBulk, dawne rece).</summary>
        private static float UseOf(Town town, int m)
        {
            float[] a; float meas = _useAvg.TryGetValue(town, out a) ? a[m] : 0f;
            float est = 0f;
            try { est = CaravanBulk.UseFor(town, _items[m]); } catch { }
            return Math.Max(meas, est);
        }

        private static int InTransit(Settlement dest, int m)
        {
            int n = 0;
            foreach (var c in _contracts) if (c.Dest == dest && c.Mat == m) n += c.Qty;
            return n;
        }

        internal static bool HasContract(MobileParty mp) { return mp != null && _byCar.Count > 0 && _byCar.ContainsKey(mp); }

        // ------------------------------------------------------------ doba
        internal static void Daily()
        {
            int day = (int)CampaignTime.Now.ToDays;
            try
            {
                if (!Ready()) return;
                if (_pending != null) ResolvePending("doba przed startem sesji");
                Keep(day);
                // srednie zuzycia (ok. 14 dob)
                var towns = new HashSet<Town>(_useAvg.Keys); foreach (var t in _useToday.Keys) towns.Add(t);
                foreach (var t in towns)
                {
                    float[] avg; if (!_useAvg.TryGetValue(t, out avg)) { avg = new float[M]; _useAvg[t] = avg; }
                    float[] today; _useToday.TryGetValue(t, out today);
                    for (int m = 0; m < M; m++) avg[m] += ((today != null ? today[m] : 0f) - avg[m]) / 14f;
                }
                _useToday.Clear();
                if (On)
                    foreach (var kv in new List<KeyValuePair<Town, int[]>>(_miss))
                        for (int m = 0; m < M; m++)
                            if (kv.Value[m] > 0 && _items[m] != null)
                                try { Order(kv.Key, m, day); } catch (Exception e) { Stumble("Order", e); }
                _miss.Clear();
            }
            catch (Exception e) { Stumble("Daily", e); }
            Line(day);
        }

        /// <summary>Kontrakty w drodze: zniszczone, wrogosc, 30 dob, cel zmieniony (przywrocony).</summary>
        private static void Keep(int day)
        {
            for (int i = _contracts.Count - 1; i >= 0; i--)
            {
                var c = _contracts[i];
                try
                {
                    var car = c.Car;
                    if (car == null || !car.IsActive) { Drop(c); _dLost++; continue; }
                    if (car.MapEvent != null) continue;
                    if (c.Dest.MapFaction != null && car.MapFaction != null && FactionManager.IsAtWarAgainstFaction(car.MapFaction, c.Dest.MapFaction)) { Release(c); _dRelHost++; continue; }
                    if (day - c.Day > 30) { Release(c); _dRel30++; continue; }
                    if (!On) { Release(c); _dRelOther++; continue; }
                    if (car.CurrentSettlement == c.Dest) { Deliver(car, c.Dest); continue; }   // stoi w celu (wjazd przed zapisem / bez zdarzenia)
                    if (car.TargetSettlement != c.Dest)
                    {
                        try { if (car.CurrentSettlement != null) LeaveSettlementAction.ApplyForParty(car); } catch (Exception e) { Stumble("Keep(wyjazd)", e); }   // jak posilki DTE
                        if (Move(car, c.Dest, c.Naval)) _dRetarget++; else { Release(c); _dRelOther++; }
                    }
                }
                catch (Exception e) { Stumble("Keep", e); }
            }
        }

        private static void Order(Town town, int m, int day)
        {
            var s = Settings.Current;
            int[] last;
            if (!_last.TryGetValue(town, out last)) { last = new int[M]; for (int k = 0; k < M; k++) last[k] = int.MinValue / 2; _last[town] = last; }
            if (day - last[m] < Math.Max(1, s.TownMaterialOrderDays)) { _dPause++; return; }
            var item = _items[m];
            var dest = town.Settlement;
            if (dest == null || dest.IsUnderSiege || town.Owner == null || town.Owner.ItemRoster == null) return;
            int want = (int)Math.Ceiling(10f * UseOf(town, m)) - town.Owner.ItemRoster.GetItemNumber(item) - InTransit(dest, m);
            if (want <= 0) { _dEnough++; return; }
            float range = Math.Max(1f, m == Ore ? s.TownMaterialOrderRangeOre : s.TownMaterialOrderRange);
            float carter = Math.Max(0f, s.CarterPencePerKgPer100), sea = MBMath.ClampFloat(s.SeaFreightShare, 0f, 1f);
            float kg = Kg(item);
            var dm = Campaign.Current.Models.MapDistanceModel;
            var pos = dest.GetPosition2D;
            Settlement bestSrc = null; MobileParty bestCar = null; int bestQ = 0; float bestPerKg = 0f, bestDist = 0f, bestMargin = 0f; bool bestNaval = false;
            bool anySrc = false, anyCar = false, anyRoad = false;
            foreach (var src in Settlement.All)
            {
                if (src == null || src == dest || (!src.IsTown && !src.IsCastle) || src.Town == null || src.ItemRoster == null) continue;
                if (pos.Distance(src.GetPosition2D) > range) continue;                        // droga >= linia prosta
                if (src.IsUnderSiege || (src.MapFaction != null && dest.MapFaction != null && FactionManager.IsAtWarAgainstFaction(src.MapFaction, dest.MapFaction))) continue;
                int have = src.ItemRoster.GetItemNumber(item);
                if (have <= 0) continue;
                int keep = src.IsTown ? Math.Max(SafeKeep(src.Town, item), (int)Math.Ceiling(10f * UseOf(src.Town, m))) : 0;   // zamek: caly zapas
                int surplus = have - keep;
                if (surplus <= 0) continue;
                anySrc = true;
                // karawana stojaca w zrodle: nie gracza, handlujaca, bez kontraktu i bez rozkazu DTE, z najwiekszym wolnym miejscem
                MobileParty car = null; float carFree = 0f;
                foreach (var p in src.Parties)
                {
                    if (!Eligible(p)) continue;
                    float free = p.InventoryCapacity - p.TotalWeightCarried;
                    if (free > carFree) { carFree = free; car = p; }
                }
                if (car == null || carFree < kg) continue;
                anyCar = true;
                float d = -1f; bool naval = false;
                try { d = dm.GetDistance(src, dest, false, false, MobileParty.NavigationType.Default); } catch { d = -1f; }
                if (!(d >= 0f && d < CartTownExit.BkLimit))
                {
                    d = -1f;
                    if (car.HasNavalNavigationCapability && src.HasPort && dest.HasPort)
                    {
                        try { d = dm.GetDistance(src, dest, true, true, MobileParty.NavigationType.All); naval = true; } catch { d = -1f; }
                    }
                }
                if (!(d >= 0f && d < CartTownExit.BkLimit) || d > range) continue;
                anyRoad = true;
                int pSrc = PriceAt(src.Town, item, false, 0);
                if (pSrc <= 0) continue;
                int q = Math.Min(Math.Min(want, surplus), (int)(carFree / kg));
                q = Math.Min(q, car.PartyTradeGold / Math.Max(1, pSrc));
                if (q <= 0) continue;
                // zysk po dostawie: srodkowa sztuka w celu (polka rosnie) i w zrodle (polka maleje) - prawdziwy model cen; oplata od kg i odleglosci
                int pDst = PriceAt(town, item, true, Math.Max(0, q - 1) / 2);
                int pBuy = PriceAt(src.Town, item, false, -(Math.Max(0, q - 1) / 2));
                if (pDst <= 0 || pBuy <= 0) continue;
                float fee = carter * kg * q * d / 100f * (naval ? sea : 1f);
                float margin = (float)q * pDst - (float)q * pBuy - fee;
                if (margin <= 0f) { if (_dEx.Count < 3) _dEx.Add(town.Name + " - " + src.Name + " " + (int)d + " " + Names[m] + " bez zysku (" + pDst + "/" + pBuy + ")"); continue; }
                float perKg = margin / (q * kg);
                if (perKg > bestPerKg) { bestPerKg = perKg; bestSrc = src; bestCar = car; bestQ = q; bestDist = d; bestNaval = naval; bestMargin = margin; }
            }
            if (bestSrc == null)
            {
                if (!anySrc) _dNoSrc++; else if (!anyCar) _dNoCar++; else if (!anyRoad) _dNoRoad++; else _dNoGain++;
                return;
            }
            last[m] = day;
            Place(bestCar, bestSrc, dest, m, bestQ, bestDist, bestNaval, bestMargin, day);
        }

        private static int SafeKeep(Town t, ItemObject it) { try { return CaravanBulk.KeepFor(t, it); } catch { return 0; } }

        private static bool Eligible(MobileParty p)
        {
            if (p == null || !p.IsCaravan || !p.IsActive || p.MapEvent != null || p.Army != null || !p.IsPartyTradeActive || p.ItemRoster == null || p.Party == null) return false;
            if (p.IsCurrentlyUsedByAQuest || p.Ai == null || p.Ai.DoNotMakeNewDecisions || _byCar.ContainsKey(p)) return false;
            if (p == MobileParty.MainParty || p.ActualClan == Clan.PlayerClan || (p.Party.Owner != null && p.Party.Owner == Hero.MainHero)) return false;   // karawana gracza nigdy
            return true;
        }

        /// <summary>Cena jednej sztuki w miescie (selling: karawana sprzedaje miastu) przy polce przesunietej o shift sztuk - ten sam model cen
        /// co CaravanBulk.Fetch (bez partii); 0 = brak wyceny.</summary>
        private static int PriceAt(Town town, ItemObject item, bool selling, int shift)
        {
            try
            {
                var cat = item.ItemCategory;
                var market = town.MarketData;
                var model = Campaign.Current.Models.TradeItemPriceFactorModel;
                if (cat == null || market == null || model == null) return 0;
                var d = market.GetCategoryData(cat);
                float inStore = Math.Max(0f, d.InStoreValue + shift * HistoricalPrices.ShelfWorth(item));
                return Math.Max(1, model.GetPrice(new EquipmentElement(item), null, null, selling, inStore, d.Supply, d.Demand));
            }
            catch (Exception e) { Stumble("PriceAt", e); return 0; }
        }

        /// <summary>Kontrakt: zakup w zrodle (zloto karawany -> kasa zrodla), wyjazd, rozkaz jazdy do celu, AI karawany wstrzymane do przyjazdu.</summary>
        private static void Place(MobileParty car, Settlement src, Settlement dest, int m, int q, float dist, bool naval, float margin, int day)
        {
            var item = _items[m];
            var srcRoster = src.ItemRoster; var pack = car.ItemRoster;
            int got = 0; long paid = 0;
            for (int guard = 0; guard < 100 && got < q; guard++)
            {
                int at = srcRoster.FindIndexOfItem(item);
                if (at < 0) break;
                var el = srcRoster.GetElementCopyAtIndex(at);
                if (el.Amount <= 0) break;
                int price = Math.Max(1, src.Town.GetItemPrice(el.EquipmentElement, car, false));
                int n = Math.Min(Math.Min(10, q - got), Math.Min(el.Amount, car.PartyTradeGold / price));
                if (n <= 0) break;
                int had = pack.GetItemNumber(item), purse = car.PartyTradeGold;
                try { SellItemsAction.Apply(src.Town.Owner, car.Party, el, n, src); }
                catch (Exception e) { Stumble("Place(zakup)", e); break; }
                int moved = pack.GetItemNumber(item) - had;
                if (moved <= 0) break;
                got += moved; paid += Math.Max(0, purse - car.PartyTradeGold);
            }
            if (got <= 0) { _dNoGain++; return; }
            var c = new Contract { Car = car, CarId = car.StringId, Dest = dest, Src = src, Mat = m, Qty = got, Day = day, Paid = (int)paid, Dist = dist, Naval = naval };
            try { if (car.CurrentSettlement != null) LeaveSettlementAction.ApplyForParty(car); } catch (Exception e) { Stumble("Place(wyjazd)", e); }
            _contracts.Add(c); _byCar[car] = c;
            if (!Move(car, dest, naval)) { Release(c); _dRejected++; return; }   // rozkaz odrzucony (straznik drog) - karawana handluje dalej sama, z ladunkiem
            _dMade[m]++; _dMadeQty[m] += got; _dGold += paid; _dMargin += (long)margin;
            if (_dEx.Count < 3) _dEx.Add(dest.Name + " - " + src.Name + " " + (int)dist + (naval ? " (morzem)" : "") + " " + Names[m] + " " + got);
        }

        /// <summary>Rozkaz jazdy przez wejscie metody (z prefiksem BK i jego straznikiem, jak kazdy rozkaz - IslandRoads); true = cel przyjety.</summary>
        private static bool Move(MobileParty car, Settlement dest, bool naval)
        {
            try
            {
                car.Ai.SetDoNotMakeNewDecisions(true);
                var nav = naval ? MobileParty.NavigationType.All : MobileParty.NavigationType.Default;
                bool port = naval && dest.HasPort;
                if (_setMove == null) _setMove = typeof(MobileParty).GetMethod("SetMoveGoToSettlement", new[] { typeof(Settlement), typeof(MobileParty.NavigationType), typeof(bool) });
                if (_setMove != null)
                {
                    try { _setMove.Invoke(car, new object[] { dest, nav, port }); }
                    catch (TargetInvocationException e) { throw e.InnerException ?? e; }
                }
                else car.SetMoveGoToSettlement(dest, nav, port);
                car.RecalculateShortTermBehavior();
                return car.TargetSettlement == dest;
            }
            catch (Exception e) { Stumble("Move", e); return false; }
        }

        private static void Drop(Contract c)
        {
            _contracts.Remove(c);
            if (c.Car != null) _byCar.Remove(c.Car);
        }

        /// <summary>Zwolnienie: AI karawany wraca (BK wybierze cel przy najblizszym ticku), ladunek zostaje jej.</summary>
        private static void Release(Contract c)
        {
            Drop(c);
            try { if (c.Car != null && c.Car.IsActive && c.Car.Ai != null) { c.Car.Ai.SetDoNotMakeNewDecisions(false); c.Car.Ai.RethinkAtNextHourlyTick = true; } }
            catch (Exception e) { Stumble("Release", e); }
        }

        // ------------------------------------------------------------ przyjazd, zniszczenie, BK
        /// <summary>CampaignEvents.SettlementEntered (sluchacz dopisany PO CaravanBulk - wolany PRZED nim): dostawa kontraktu.</summary>
        internal static void OnEntered(MobileParty mp, Settlement st, Hero hero)
        {
            if (mp == null || st == null || _byCar.Count == 0 || !mp.IsCaravan) return;
            try { Contract c; if (_byCar.TryGetValue(mp, out c) && c.Dest == st) Deliver(mp, st); }
            catch (Exception e) { Stumble("OnEntered", e); }
        }

        private static void Deliver(MobileParty mp, Settlement st)
        {
            Contract c;
            if (!_byCar.TryGetValue(mp, out c)) return;
            var town = st.Town; var item = _items[c.Mat];
            int sold = 0; long got = 0;
            try
            {
                int reserve = (int)Math.Max(0f, Settings.Current.TownRentFloorGold);
                var pack = mp.ItemRoster;
                for (int guard = 0; guard < 100 && sold < c.Qty && town != null; guard++)
                {
                    int at = pack.FindIndexOfItem(item);
                    if (at < 0) break;
                    var el = pack.GetElementCopyAtIndex(at);
                    if (el.Amount <= 0) break;
                    int spare = town.Gold - reserve;
                    int price = Math.Max(1, town.GetItemPrice(el.EquipmentElement, mp, true));
                    int n = Math.Min(Math.Min(10, c.Qty - sold), Math.Min(el.Amount, spare / price));
                    if (n <= 0) break;                                                    // miasto bez kasy ponad rezerwe - reszta zostaje karawanie
                    int had = el.Amount, purse = mp.PartyTradeGold;
                    try { SellItemsAction.Apply(mp.Party, town.Owner, el, n, st); }
                    catch (Exception e) { Stumble("Deliver(sprzedaz)", e); break; }
                    int moved = had - pack.GetItemNumber(item);
                    if (moved <= 0) break;
                    sold += moved; got += Math.Max(0, mp.PartyTradeGold - purse);
                }
            }
            catch (Exception e) { Stumble("Deliver", e); }
            _dDone++; _dDays += Math.Max(0.0, CampaignTime.Now.ToDays - c.Day); _dPaidDest += got;
            if (sold < c.Qty) _dKeptTarget += c.Qty - sold;
            Release(c);
        }

        internal static void OnPartyDestroyed(MobileParty mp, PartyBase destroyer)
        {
            try
            {
                Contract c;
                if (mp == null || _byCar.Count == 0 || !_byCar.TryGetValue(mp, out c)) return;
                _dLost++; _dLostQty += c.Qty;
                Drop(c);
            }
            catch (Exception e) { Stumble("OnPartyDestroyed", e); }
        }

        /// <summary>Prefiks BK BKCaravansBehavior.ReleaseCaravanFromHold (koniec oblezenia, karawana bez rozkazu): karawana z kontraktem jedzie dalej do celu.</summary>
        public static bool ReleasePrefix(MobileParty __0)
        {
            try
            {
                Contract c;
                if (__0 == null || _byCar.Count == 0 || !_byCar.TryGetValue(__0, out c)) return true;
                if (Move(__0, c.Dest, c.Naval)) { _dRetarget++; return false; }
            }
            catch (Exception e) { Stumble("ReleasePrefix", e); }
            return true;
        }

        // ------------------------------------------------------------ linia dnia
        private static void Line(int day)
        {
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                if (!On && _contracts.Count == 0 && _dDone + _dLost + _dRelOther == 0) return;
                var sb = new StringBuilder();
                int made = 0; foreach (var n in _dMade) made += n;
                sb.Append("Kontrakty surowca (174): dzien ").Append(day).Append(On ? "" : " (WYLACZONE - tylko zwolnienia)").Append(" - zawarto ").Append(made).Append(" [");
                for (int m = 0; m < M; m++) { if (m > 0) sb.Append(", "); sb.Append(Names[m]).Append(' ').Append(_dMadeQty[m]); }
                sb.Append(" sztuk] za ").Append(_dGold).Append(" d towaru (oczekiwana marza karawan ").Append(_dMargin).Append(" d); dojechalo ").Append(_dDone)
                  .Append(" (srednio ").Append(_dDone > 0 ? (_dDays / _dDone).ToString("0.0", CultureInfo.InvariantCulture) : "-").Append(" dob drogi; miasta zaplacily ").Append(_dPaidDest)
                  .Append(" d; zostalo karawanom z braku kasy miasta ").Append(_dKeptTarget).Append(" szt.), w drodze ").Append(_contracts.Count).Append(", rozbite/pojmane ").Append(_dLost)
                  .Append(" (sztuk ").Append(_dLostQty).Append("), zwolnione ").Append(_dRelHost + _dRel30 + _dRelOther + _dRejected).Append(" (wrogosc ").Append(_dRelHost).Append(", 30 dob ").Append(_dRel30)
                  .Append(", rozkaz odrzucony ").Append(_dRejected).Append(", inne ").Append(_dRelOther).Append("); cel przywrocony ").Append(_dRetarget)
                  .Append("; bez kontraktu: brak karawany w zrodle ").Append(_dNoCar).Append(", brak zrodla w zasiegu ").Append(_dNoSrc).Append(", bez drogi ").Append(_dNoRoad)
                  .Append(", bez zysku ").Append(_dNoGain).Append(", zapas i dostawy dosc ").Append(_dEnough).Append(", przerwa ").Append(_dPause)
                  .Append(" [").Append(_dEx.Count > 0 ? string.Join("; ", _dEx.ToArray()) : "-").Append("]");
                int noOre = 0, noFlax = 0, noHides = 0, towns = 0;
                foreach (var t in Town.AllTowns)
                {
                    if (t == null || !t.IsTown || t.Owner == null || t.Owner.ItemRoster == null) continue;
                    towns++;
                    var r = t.Owner.ItemRoster;
                    if (_items[Ore] != null && r.GetItemNumber(_items[Ore]) <= 0) noOre++;
                    if (_items[4] != null && r.GetItemNumber(_items[4]) <= 0) noFlax++;
                    if (_items[5] != null && r.GetItemNumber(_items[5]) <= 0) noHides++;
                }
                sb.Append("; miast bez rudy ").Append(noOre).Append(" z ").Append(towns).Append(", bez lnu ").Append(noFlax).Append(", bez skor surowych ").Append(noHides)
                  .Append("; potkniecia ").Append(_stumbles).Append(" (od wczytania ").Append(_stumblesAll).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Line", e); }
            finally { NewDay(); }
        }

        // ------------------------------------------------------------ zapis: karawana|cel|zrodlo|surowiec|ilosc|doba|zaplacone|odleglosc|morzem~
        internal static string Export()
        {
            try
            {
                if (_pending != null) ResolvePending("zapis przed startem sesji");
                var sb = new StringBuilder();
                foreach (var c in _contracts)
                {
                    if (c.Car == null || !c.Car.IsActive || string.IsNullOrEmpty(c.Car.StringId) || c.Dest == null) continue;
                    sb.Append(c.Car.StringId).Append('|').Append(c.Dest.StringId).Append('|').Append(c.Src != null ? c.Src.StringId : "").Append('|').Append(Ids[c.Mat]).Append('|')
                      .Append(c.Qty).Append('|').Append(c.Day).Append('|').Append(c.Paid).Append('|').Append(c.Dist.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(c.Naval ? 1 : 0).Append('~');
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        internal static void Import(string s)
        {
            _contracts.Clear(); _byCar.Clear();
            _pending = string.IsNullOrEmpty(s) ? null : s;
        }

        /// <summary>OnSessionLaunched: kontrakty z zapisu na karawany (po StringId); bez karawany - pominiete; wylaczone w MCM - karawana zwolniona.</summary>
        internal static void ResolvePending(string why)
        {
            var s = _pending;
            _pending = null;
            if (string.IsNullOrEmpty(s) || !Ready()) return;
            int ok = 0, gone = 0, freed = 0;
            var byId = new Dictionary<string, MobileParty>();
            foreach (var p in MobileParty.AllCaravanParties) if (p != null && !string.IsNullOrEmpty(p.StringId)) byId[p.StringId] = p;
            var om = MBObjectManager.Instance;
            foreach (var rec in s.Split('~'))
            {
                if (rec.Length == 0) continue;
                try
                {
                    var a = rec.Split('|');
                    if (a.Length != 9) { gone++; continue; }
                    MobileParty car; byId.TryGetValue(a[0], out car);
                    Settlement dest = null, src = null;
                    try { dest = om.GetObject<Settlement>(a[1]); } catch { }
                    if (a[2].Length > 0) { try { src = om.GetObject<Settlement>(a[2]); } catch { } }
                    int m = Array.IndexOf(Ids, a[3]);
                    if (car == null || !car.IsActive || dest == null || m < 0) { gone++; continue; }
                    int q, d0, paid; float dist;
                    int.TryParse(a[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out q);
                    int.TryParse(a[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out d0);
                    int.TryParse(a[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out paid);
                    float.TryParse(a[7], NumberStyles.Float, CultureInfo.InvariantCulture, out dist);
                    var c = new Contract { Car = car, CarId = a[0], Dest = dest, Src = src, Mat = m, Qty = q, Day = d0, Paid = paid, Dist = dist, Naval = a[8] == "1" };
                    _contracts.Add(c); _byCar[car] = c;
                    if (!On) { Release(c); freed++; continue; }
                    if (car.Ai != null && !car.Ai.DoNotMakeNewDecisions) car.Ai.SetDoNotMakeNewDecisions(true);
                    ok++;
                }
                catch (Exception e) { Stumble("ResolvePending", e); gone++; }
            }
            Log.Info("Kontrakty surowca (174): z zapisu (" + why + ") " + ok + " kontraktow w drodze; pominiete (karawany albo celu juz nie ma) " + gone + ", zwolnione (wylaczone w MCM) " + freed + ".");
        }

        // ------------------------------------------------------------ wpiecie
        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var t = QuartermasterLaw.FindType("BannerKings.Behaviours.BKCaravansBehavior");
                var m = t != null ? AccessTools.Method(t, "ReleaseCaravanFromHold", new[] { typeof(MobileParty) }) : null;
                if (m != null) { h.Patch(m, prefix: new HarmonyMethod(typeof(MaterialOrders), nameof(ReleasePrefix))); ReleaseHooked = true; }
                Log.Info("MaterialOrders (174.2): kontrakty surowca dla prawdziwych karawan " + (On ? "CZYNNE" : "wylaczone w MCM") + "; BK ReleaseCaravanFromHold (cel po oblezeniu) "
                         + (ReleaseHooked ? "wpiety - karawana z kontraktem jedzie dalej do celu" : (t == null ? "bez BK - nic do wpiecia" : "BRAK metody")) + ".");
            }
            catch (Exception e) { Log.Error("MaterialOrders.ApplyAll", e); }
        }
    }
}
