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
    /// (najwyzej raz na TownMaterialOrderDays dob) w pobliskim niewrogim MIESCIE, ktore ma go ponad prog nadwyzki. Towar wiezie
    /// karawana BK STOJACA w zrodle: kupuje go tam po cenie targu zrodla (SellItemsAction - zwykly handel gry: zloto karawany idzie do kasy
    /// miasta-zrodla, a czesc jako clo do TradeTaxAccumulated pana; w BK ok. 10%), jedzie do zamawiajacego (bandy moga ja rozbic - lup jak w grze)
    /// i tam sprzedaje po cenie targu (kasa miasta ponad rezerwe TownRentFloorGold -> karawana).
    /// Kontrakt tylko przy oczekiwanej marzy >= koszt drogi (CarterPencePerKgPer100 od kg i odleglosci; morze x SeaFreightShare) i ladunku
    /// >= TownMaterialOrderMinLoadKg (recenzja 174: karawana nie jedzie 10 dob dla 2 sztuk lnu); brak karawany w zrodle = brak kontraktu.
    /// Ruda tylko z bliska (TownMaterialOrderRangeOre, 2-3 doby - daleko wozono sztaby, nie rude), reszta do TownMaterialOrderRange. Ile: zapas na
    /// 10 dob zuzycia miasta (wieksze z: zmierzone - srednia ok. 14 dob z warsztatow, strzelarzy i rzemiosla, i szacunek karawan z rak - CaravanBulk)
    /// minus polka minus w drodze; najwyzej wolne miejsce w jukach i kiesa karawany. Zrodlo sprzedaje tylko ponad max(Keep karawan wedlug dawnych
    /// rak, 10 dob wlasnego zuzycia). ZAMKI NIE SA ZRODLEM (recenzja 174): karawany BK do nich nie jezdza (CaravanBulk), a SellItemsAction ze
    /// sprzedajacym zamkiem oddaje calosc zaplaty jako clo (GetVillageTaxRatio(null) - straznik BK 1.0) - zamek oddawalby surowiec za 0 d.
    /// Karawana z kontraktem: rozkaz jazdy do celu; BK nie wybiera jej nowego celu (prefiks BKCaravansBehavior.HourlyTickParty, takze gry
    /// CaravansCampaignBehavior bez BK), ale AI gry zostaje czynne - karawana ucieka przed bandami i wrogami jak kazda (recenzja 174: przy
    /// DoNotMakeNewDecisions gra pomija ucieczke). Bez tej latki - wzor posilkow DTE (Ai.SetDoNotMakeNewDecisions(true)). CaravanBulk, karawany bez
    /// amunicji (172b) i latka wysp ja przepuszczaja; BK ReleaseCaravanFromHold nie zmienia jej celu. Zwolnienie (ladunek zostaje karawanie -
    /// prawdziwy towar): wrogosc celu, oblezenie celu (BK Shipping co godzine kieruje ja wtedy do bezpiecznego miasta - bez zwolnienia
    /// byl ping-pong), rozwiazywanie karawany, cel zmieniony przez innych 2 razy, 30 dob. Druzyna gracza nigdy nie dostaje kontraktu; karawany rodu gracza -
    /// wedlug TownMaterialOrderPlayerCaravans (174b.2, pytanie 2 do Jeffa).
    /// Zapis: "arm_matorders" (SaveText.Sync), po wczytaniu - kontrakt odtworzony albo karawana zwolniona (takze przy rekordzie nieczytelnym).
    /// 174b (docs/PROJEKT-174B-DOWOZ-2026-10-09.md): cel pilnowany co godzine po nocnym obozie (Hourly - spiaca karawana to nie cudzy cel), BK Shipping
    /// zablokowany dla karawan z kontraktem; najpierw trasa (lad albo morze), potem przewoznik dobrany do trasy (karawana ladowa / konwoj), ilosc wedlug
    /// zysku, surowiec z jukow, punkt zamowienia przed zerem; linia nazw miast bez rudy i strzal co 5 dob.
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
            public MobileParty Car; public string CarId; public Settlement Dest, Src; public int Mat, Qty, Day, Paid, Retarget, HoldStreak, PackStart = -1; public float Dist; public bool Naval, Packs;
            public double Start = -1.0;   // poprawka 174b (recenzja): chwila zawarcia (doby z ulamkiem) - dni drogi bez fazy taktu doby; -1 = rekord 9-polowy
        }
        private const int MaxRetarget = 2;   // recenzja 174: cel zmieniony przez innych (BK Shipping, porty) - po 2 przywroceniach zwolnienie, bez ping-pongu
        // 174b.1 (krytyka 15): karawana z kontraktem stawiana na postoj co godzine przez cudzy kod - po tylu kolejnych godzinach ruszania z postoju
        // zwolnienie "postoj wymuszany" (jedna linia z nazwa). Postoj po bitwie albo po oblezeniu to 1 godzina; nocny oboz nie liczy sie wcale.
        private const int MaxHoldStreak = 6;
        internal static bool ShipHooked;     // 174b.1: prefiks BK BKShippingBehavior.RouteCaravanHopByHop wpiety
        private static MethodInfo _shipInvalidate, _shipGetBeh;   // BK InvalidateRedirectCache i Campaign.GetCampaignBehavior<BKShippingBehavior> (instancja przy kazdym wywolaniu)
        private static MethodInfo _bkRelease, _bkGetBeh;          // poprawka 174b (recenzja): BK ReleaseCaravanFromHold i GetCampaignBehavior<BKCaravansBehavior> - pobudka o swicie
        internal static bool HourlyHooked;   // prefiks BK/gry HourlyTickParty wpiety - karawana z kontraktem bez DoNotMakeNewDecisions (ucieka jak kazda)
        private static readonly List<Contract> _contracts = new List<Contract>();
        private static readonly Dictionary<MobileParty, Contract> _byCar = new Dictionary<MobileParty, Contract>();
        private static readonly Dictionary<Town, int[]> _miss = new Dictionary<Town, int[]>();       // cykle "brak surowca" od ostatniej doby
        private static readonly Dictionary<Town, float[]> _useToday = new Dictionary<Town, float[]>(), _useAvg = new Dictionary<Town, float[]>();
        private static readonly Dictionary<Town, int[]> _last = new Dictionary<Town, int[]>();       // doba ostatniego zamowienia surowca
        private static string _pending;
        private static MethodInfo _setMove;
        internal static bool ReleaseHooked;
        // E8/O1 (265, RD8 K6 / O6): te same metody (gra, BK) delegatem zamiast MethodInfo.Invoke - budowa raz, leniwie przy pierwszym uzyciu w kampanii
        // (EnsureFast); null = delegat nie powstal - to miejsce dalej wola przez Invoke. Instancja BK dalej brana przy kazdym wywolaniu (krytyka 174b.1)
        private static volatile bool _fastTried;
        private static readonly object _fastGate = new object();
        private static Action<MobileParty, Settlement, MobileParty.NavigationType, bool> _setMoveFast;
        private static Func<Campaign, object> _bkGetBehFast, _shipGetBehFast;
        private static Action<object, MobileParty> _bkReleaseFast, _shipInvalidateFast;

        private static void EnsureFast()
        {
            if (_fastTried) return;
            lock (_fastGate)   // prefiks BK RouteCaravanHopByHop moze przyjsc z innego watku - znacznik dopiero po zbudowaniu wszystkich
            {
                if (_fastTried) return;
                try
                {
                    if (_setMove == null) _setMove = typeof(MobileParty).GetMethod("SetMoveGoToSettlement", new[] { typeof(Settlement), typeof(MobileParty.NavigationType), typeof(bool) });
                    _setMoveFast = FastCall.Of<Action<MobileParty, Settlement, MobileParty.NavigationType, bool>>(_setMove, "gra MobileParty.SetMoveGoToSettlement");
                    _bkGetBehFast = FastCall.Of<Func<Campaign, object>>(_bkGetBeh, "gra Campaign.GetCampaignBehavior<BKCaravansBehavior>");
                    _bkReleaseFast = FastCall.Open<MobileParty>(_bkRelease, "BK ReleaseCaravanFromHold");
                    _shipGetBehFast = FastCall.Of<Func<Campaign, object>>(_shipGetBeh, "gra Campaign.GetCampaignBehavior<BKShippingBehavior>");
                    _shipInvalidateFast = FastCall.Open<MobileParty>(_shipInvalidate, "BK InvalidateRedirectCache");
                }
                catch (Exception e) { Stumble("EnsureFast", e); }
                _fastTried = true;
            }
        }

        // liczniki doby (linia "Kontrakty surowca (174)")
        private static readonly int[] _dMade = new int[M], _dMadeQty = new int[M];
        private static int _dDone, _dLost, _dLostQty, _dRelHost, _dRel30, _dRelOther, _dNoCar, _dNoSrc, _dNoGain, _dNoRoad, _dRejected, _dPause, _dEnough, _dRetarget, _dKeptTarget, _stumbles, _stumblesAll;
        private static int _dRelSiege, _dRelRetarget, _dRelDisband, _dShortPack, _dSmall, _dHoldMoved;   // recenzja 174
        private static int _dCampHours, _dShipBlocked, _dRelForcedHold;   // 174b.1: godziny kontraktow w obozie, BK Shipping zablokowany, zwolnienia "postoj wymuszany"
        // 174b.2: bez drogi wedlug powodu (wyspa / inna czesc ladu bez portu, droga ladem > zasieg, morze poza zasiegiem albo dlugoscia, brak konwoju w porcie zrodla),
        // kontrakty morzem i z jukow (zawarte, dojechaly), z wyprzedzeniem (punkt zamowienia), audyt ilosci (krytyka 20)
        private static readonly int[] _dNoRoadBy = new int[4];
        private static int _dSea, _dSeaQty, _dPacks, _dPacksQty, _dAhead, _dDoneSea, _dDonePacks, _dAudit, _auditAll;
        private static readonly Dictionary<Town, float[]> _trip = new Dictionary<Town, float[]>();   // dni drogi 3 ostatnich dostaw (miasto x surowiec)
        // poprawka 174b (recenzja, wazna 1): prog nadwyzki zrodla (SourceKeep) zapamietany na jedno wywolanie Daily - w trakcie Daily jest staly (srednie zuzycia
        // policzone przed zamowieniami, rece i warsztaty bez zmian, _trip zmienia tylko dostawa); polke zrodla (have) czytamy zawsze swiezo, bo Place ja zmienia
        private static readonly Dictionary<Town, int[]> _keepDay = new Dictionary<Town, int[]>();
        // poprawka 174b (recenzja): przewoznik stoi w zrodle, ale bez miejsca w jukach albo kiesy (to nie "bez drogi"); karawany zwolnione w nocnym obozie
        // i obudzone o swicie decyzja BK (ReleaseCaravanFromHold) zamiast postoju do najblizszego ticku BK
        private static int _dNoRoom, _dWokeBk;
        private static readonly List<string> _dRetEx = new List<string>();   // 174b.1: przyklady "cel = inne miasto" (karawana -> miasto, gdzie stoi)
        private static long _dGold, _dMargin, _dPaidDest;
        private static double _dDays;
        private static readonly List<string> _dEx = new List<string>();
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        // 174b.0: linia "Miasta bez rudy i strzal (174b)" (pierwsza doba sesji i co 5 dob) - cykle "brak rudy" wedlug miasta (warsztaty i strzelarze)
        // i ruda wywieziona kontraktami wedlug zrodla od ostatniej linii; liczniki tylko do logu
        private static readonly Dictionary<Town, int> _missOreSince = new Dictionary<Town, int>();
        private static readonly Dictionary<Settlement, int> _srcOreSince = new Dictionary<Settlement, int>();
        private static readonly Dictionary<Town, int> _dstOreSince = new Dictionary<Town, int>();   // 174b.2: ruda dostarczona kontraktami wedlug miasta (prog P2 na zapisie)
        private static int _namesSince = -1;
        private static int _dCampSeen;   // 174b.0: przeglad doby zastal karawane z kontraktem w nocnym obozie (stara regula Keep liczy to jako cudzy cel)
        // P3/Z1 (TRIAZ-3 bieg roczny, O2-1 P1): kontrakt wozem kupcow (MaterialCarts) - zrodla z nadwyzka (miasta i zamki) raz na Daily i surowiec,
        // drogi par zrodlo-cel raz na kampanie (mapa sie nie zmienia), powody "bez kontraktu" wedlug surowca (P2c) i wedlug miasta dla rudy (linia nazw)
        private sealed class CartSrc { public Town T; public Settlement St; public int Keep; }
        private static readonly List<CartSrc>[] _cartSrc = new List<CartSrc>[M];
        private static readonly Dictionary<long, float[]> _cartRoad = new Dictionary<long, float[]>();
        private const int WNoSrc = 0, WNoRoad = 1, WEnough = 2, WNoGold = 3, WSmall = 4, WNoGain = 5, WCar = 6, WKinds = 7;
        private static readonly string[] WName = { "brak zrodla z nadwyzka w zasiegu", "bez drogi", "zapas dosc pod te droge", "kasa miasta przy zapasie kupcow", "ladunek za maly",
                                                   "bez zysku", "brak karawany albo miejsca w jukach" };
        private static readonly int[,] _dWhy = new int[M, WKinds];
        private static long _dCartTicks;
        private static int _dCartAsk, _dOreEmptyBefore = -1, _dCartMixed, _dCartMixedQty;   // _dCartMixed: wozy droga ladem i morzem (recenzja P3/Z1)
        private static readonly Dictionary<Town, int> _oreWhy = new Dictionary<Town, int>();
        private static readonly Dictionary<Town, int> _dstOreCartSince = new Dictionary<Town, int>();
        private static readonly Dictionary<Settlement, int> _srcOreCartSince = new Dictionary<Settlement, int>();
        // 282 (F2/F3, linia "Ruda przeplyw (282)"): pamiec doby rudy miasta (prog zrodla = wlasny punkt zamowienia, zuzycie) i liczniki doby rudy - tylko log
        private static readonly Dictionary<Town, float[]> _oreDay = new Dictionary<Town, float[]>();   // [2m] punkt zamowienia surowca m (sztuki), [2m+1] zuzycie dobowe (UseDest); NaN - nie liczone (284: kazdy surowiec)
        private static int _o2Try, _o2Cart, _o2CartQty, _o2CartDays, _o2CarDays, _o2Capped, _o2Enough, _o2Pause, _o2CartDone, _o2CarDone;
        private static long _o2CartCost;
        private static double _o2Profit; private static int _o2ProfitN;
        private static readonly List<string> _o2First = new List<string>();
        private sealed class OrdItem { public Town T; public int M; public bool Ahead; public float Cover; public string Id; }

        internal static bool On { get { var s = Settings.Current; return s != null && s.TownMaterialOrders; } }

        internal static void Reset()
        {
            _items = null; _ix.Clear(); _contracts.Clear(); _byCar.Clear(); _miss.Clear(); _useToday.Clear(); _useAvg.Clear(); _last.Clear(); _pending = null;
            _missOreSince.Clear(); _srcOreSince.Clear(); _dstOreSince.Clear(); _namesSince = -1; _trip.Clear(); _auditAll = 0; _keepDay.Clear();
            MaterialCarts.Reset(M); _cartRoad.Clear(); for (int k = 0; k < M; k++) _cartSrc[k] = null;   // P3/Z1
            _oreWhy.Clear(); _dstOreCartSince.Clear(); _srcOreCartSince.Clear();
            _oreDay.Clear();   // 282
            for (int k = 0; k < M; k++) { _expDay[k] = int.MinValue; _expDest[k].Clear(); }   // plotno 14.10 (poprawka 2): cele wywozu tkacza; K3: osobno dla kazdego surowca
            NewDay(); _stumblesAll = 0; _errSites.Clear();
        }

        private static void NewDay()
        {
            Array.Clear(_dMade, 0, M); Array.Clear(_dMadeQty, 0, M);
            _dDone = _dLost = _dLostQty = _dRelHost = _dRel30 = _dRelOther = _dNoCar = _dNoSrc = _dNoGain = _dNoRoad = _dRejected = _dPause = _dEnough = _dRetarget = _dKeptTarget = _stumbles = 0;
            _dRelSiege = _dRelRetarget = _dRelDisband = _dShortPack = _dSmall = _dHoldMoved = 0; _dCampSeen = 0;
            _dCampHours = _dShipBlocked = _dRelForcedHold = 0; _dRetEx.Clear();
            Array.Clear(_dNoRoadBy, 0, 4); _dSea = _dSeaQty = _dPacks = _dPacksQty = _dAhead = _dDoneSea = _dDonePacks = _dAudit = 0;
            _dNoRoom = _dWokeBk = 0;
            _dGold = _dMargin = _dPaidDest = 0; _dDays = 0; _dEx.Clear();
            Array.Clear(_dWhy, 0, _dWhy.Length); _dCartTicks = 0; _dCartAsk = 0; _dOreEmptyBefore = -1; _dCartMixed = _dCartMixedQty = 0;   // P3/Z1
            _o2Try = _o2Cart = _o2CartQty = _o2CartDays = _o2CarDays = _o2Capped = _o2Enough = _o2Pause = _o2CartDone = _o2CarDone = 0; _o2CartCost = 0; _o2Profit = 0; _o2ProfitN = 0; _o2First.Clear();   // 282
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

        /// <summary>174b.2: sztuk przedmiotu we wszystkich stosach rosteru (GetItemNumber liczy tylko pierwszy stos) - do audytu ilosci kontraktu.</summary>
        private static int Total(ItemRoster r, ItemObject it)
        {
            int n = 0;
            if (r == null || it == null) return 0;
            for (int i = 0; i < r.Count; i++) { var el = r.GetElementCopyAtIndex(i); if (el.EquipmentElement.Item == it && el.Amount > 0) n += el.Amount; }
            return n;
        }

        // ------------------------------------------------------------ sygnal i zuzycie (wolane z warsztatow, strzelarzy i rzemiosla miasta)
        /// <summary>Brak surowca w cyklu (bit 0 ruda, 1 drewno, 2 skora, 3 len/plotno - maska WorkshopLaw i TownFletchers).</summary>
        internal static void NoteMissMask(Town town, int mask, bool names = true)
        {
            if (town == null || mask == 0) return;
            try
            {
                for (int m = 0; m < 4; m++) if ((mask & (1 << m)) != 0) Bump(_miss, town, m);
                if (names && (mask & 1) != 0) { int n; _missOreSince.TryGetValue(town, out n); _missOreSince[town] = n + 1; }   // 174b.0: tylko do linii nazw (174b.3: bez rudy zatrzymanej dla strzelarzy)
            }
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
            return n + MaterialCarts.Incoming(dest, m);   // P3/Z1: ladunek w wozach kupcow - inaczej miasto zamowiloby drugi raz
        }

        internal static bool HasContract(MobileParty mp) { return mp != null && _byCar.Count > 0 && _byCar.ContainsKey(mp); }

        /// <summary>P3/P2a (IronLands - tylko log): zuzycie rudy zmierzone od ostatniego Daily (warsztaty zbrojne, strzelarze, rzemioslo miasta) wedlug miasta.</summary>
        internal static void ForEachOreUseToday(Action<Town, float> f)
        {
            foreach (var kv in _useToday) if (kv.Key != null && kv.Value != null && kv.Value[Ore] > 0f) f(kv.Key, kv.Value[Ore]);
        }

        // ------------------------------------------------------------ doba
        internal static void Daily()
        {
            int day = (int)CampaignTime.Now.ToDays;
            _keepDay.Clear();   // poprawka 174b (recenzja, wazna 1): prog nadwyzki zrodel liczony na nowo raz na Daily
            _oreDay.Clear();    // 282: punkt zamowienia zrodel rudy - tez raz na Daily
            try
            {
                if (!Ready()) return;
                if (_pending != null) ResolvePending("doba przed startem sesji");
                Keep(day);
                // P3/Z1: wozy kupcow z surowcem - dostawy PRZED zamowieniami (takze przy wylaczonym kluczu i wylaczonych kontraktach: oplacone dojezdzaja)
                for (int k = 0; k < M; k++) _cartSrc[k] = null;
                _dOreEmptyBefore = OreEmptyTowns();
                long tc0 = System.Diagnostics.Stopwatch.GetTimestamp();
                try { MaterialCarts.Arrivals(day, OnCartArrived); } catch (Exception e) { Stumble("Daily(wozy)", e); }
                _dCartTicks += System.Diagnostics.Stopwatch.GetTimestamp() - tc0;
                // srednie zuzycia (ok. 14 dob)
                var towns = new HashSet<Town>(_useAvg.Keys); foreach (var t in _useToday.Keys) towns.Add(t);
                foreach (var t in towns)
                {
                    float[] avg; if (!_useAvg.TryGetValue(t, out avg)) { avg = new float[M]; _useAvg[t] = avg; }
                    float[] today; _useToday.TryGetValue(t, out today);
                    for (int m = 0; m < M; m++) avg[m] += ((today != null ? today[m] : 0f) - avg[m]) / 14f;
                }
                _useToday.Clear();
                if (On && Settings.Current.TownMaterialOrderNeediestFirst) OrdersNeediestFirst(day);   // 282 (F2): te same zamowienia, kolejnosc wedlug dni zapasu
                else if (On)
                {
                    foreach (var kv in new List<KeyValuePair<Town, int[]>>(_miss))
                        for (int m = 0; m < M; m++)
                            if (kv.Value[m] > 0 && _items[m] != null) OrderTimed(kv.Key, m, day, false);
                    // 174b.2 (krytyka 16): punkt zamowienia - miasto, ktore surowca uzywa, zamawia, zanim zapas zejdzie do zera: zapas + w drodze < (D + 2) x zuzycie
                    // (D - srednia dob drogi 3 ostatnich dostaw, domyslnie 4); rachunek marzy bez zmian (Order), przerwa TownMaterialOrderDays jak dotad
                    if (Settings.Current.TownMaterialOrderAhead)
                        foreach (var t in Town.AllTowns)
                        {
                            if (t == null || !t.IsTown || t.Owner == null || t.Owner.ItemRoster == null || t.Settlement == null) continue;
                            int[] miss; _miss.TryGetValue(t, out miss);
                            for (int m = 0; m < M; m++)
                            {
                                if (_items[m] == null || (miss != null && miss[m] > 0)) continue;
                                float use = UseDest(t, m);
                                if (use <= 0f) continue;
                                if (t.Owner.ItemRoster.GetItemNumber(_items[m]) + InTransit(t.Settlement, m) < (TravelDays(t, m) + 2f) * use) OrderTimed(t, m, day, true);
                            }
                        }
                }
                _miss.Clear();
            }
            catch (Exception e) { Stumble("Daily", e); }
            string l282 = null;
            try { l282 = Line282(day); } catch (Exception e) { Stumble("Line282", e); }   // 282: przed Line (Line zeruje liczniki doby w NewDay)
            Line(day);
            if (l282 != null) Log.Info(l282);
            try { if (_items != null && (_namesSince < 0 || day % 5 == 0)) NamesLine(day); } catch (Exception e) { Stumble("NamesLine", e); }
        }

        /// <summary>282 (F2, klucz TownMaterialOrderNeediestFirst): te same zamowienia co dotad (miasta z cyklem "brak surowca" wczoraj i punkt zamowienia 174b.2 -
        /// warunek liczony przed zamowieniami: zamowienia innych miast nie zmieniaja polki ani dowozu TEGO miasta, a zrodlo po sprzedazy zostaje nad swoim progiem,
        /// ktory nie jest nizszy od jego punktu zamowienia), ale obslugiwane wedlug surowca (ruda pierwsza - tabela Ids), a w surowcu od najmniejszej liczby dob
        /// zapasu: (polka + w drodze) / zuzycie (UseDest); remis - StringId miasta. Dotad kolejnosc slownika _miss, potem punkt zamowienia - pierwsze zamowienia
        /// doby wyczerpywaly nieliczne zrodla. Zmienia sie tylko kolejnosc.</summary>
        private static void OrdersNeediestFirst(int day)
        {
            var q = new List<OrdItem>();
            foreach (var kv in _miss)
                for (int m = 0; m < M; m++)
                    if (kv.Key != null && kv.Value[m] > 0 && _items[m] != null) q.Add(NewOrd(kv.Key, m, false));
            if (Settings.Current.TownMaterialOrderAhead)
                foreach (var t in Town.AllTowns)
                {
                    if (t == null || !t.IsTown || t.Owner == null || t.Owner.ItemRoster == null || t.Settlement == null) continue;
                    int[] miss; _miss.TryGetValue(t, out miss);
                    for (int m = 0; m < M; m++)
                    {
                        if (_items[m] == null || (miss != null && miss[m] > 0)) continue;
                        float use = UseDest(t, m);
                        if (use <= 0f) continue;
                        if (t.Owner.ItemRoster.GetItemNumber(_items[m]) + InTransit(t.Settlement, m) < (TravelDays(t, m) + 2f) * use) q.Add(NewOrd(t, m, true));
                    }
                }
            q.Sort((a, b) => a.M != b.M ? a.M.CompareTo(b.M) : a.Cover != b.Cover ? a.Cover.CompareTo(b.Cover) : string.CompareOrdinal(a.Id, b.Id));
            var inv = CultureInfo.InvariantCulture;
            foreach (var o in q)
            {
                if (o.M == Ore && _o2First.Count < 6) _o2First.Add((o.T.Name != null ? o.T.Name.ToString() : o.Id) + " " + (o.Cover >= 999f ? "-" : o.Cover.ToString("0.0", inv)) + " d" + (o.Ahead ? " (punkt zamowienia)" : ""));
                OrderTimed(o.T, o.M, day, o.Ahead);
            }
        }

        private static OrdItem NewOrd(Town t, int m, bool ahead)
        {
            int have = t.Owner != null && t.Owner.ItemRoster != null ? t.Owner.ItemRoster.GetItemNumber(_items[m]) : 0;
            int tr = t.Settlement != null ? InTransit(t.Settlement, m) : 0;
            float use = UseDest(t, m);
            float cover = use > 0f ? Math.Min(999f, (have + tr) / use) : (have + tr > 0 ? 999f : 998f);
            return new OrdItem { T = t, M = m, Ahead = ahead, Cover = cover, Id = t.StringId ?? "" };
        }

        /// <summary>282: towar w drodze do miasta (kontrakty karawan 174 + wozy kupcow P3/Z1) - dla kroku f5-ruda (F4, StartCalibration.F5IronWeek).
        /// Przedmiot spoza tabeli albo blad - 0.</summary>
        internal static int InTransitTo(Settlement dest, ItemObject item)
        {
            try
            {
                int m;
                if (dest == null || item == null || !Ready() || !_ix.TryGetValue(item, out m)) return 0;
                return InTransit(dest, m);
            }
            catch (Exception e) { Stumble("InTransitTo", e); return 0; }
        }

        /// <summary>282 (F3): punkt zamowienia miasta-zrodla rudy = ceil((D + 2) x zuzycie) - ten sam warunek, ktorym miasto samo zamawia (174b.2); [1] - zuzycie
        /// (UseDest). Z pamieci doby (_oreDay - czyszczona w Daily i Reset).</summary>
        private static float[] OreDay(Town t) { return MatDay(t, Ore); }

        /// <summary>284: punkt zamowienia i zuzycie dobowe surowca m w miescie ([0], [1]) - ta sama miara co ruda (282 F3), pamiec doby na miasto i surowiec.</summary>
        private static float[] MatDay(Town t, int m)
        {
            float[] a;
            if (!_oreDay.TryGetValue(t, out a)) { a = new float[2 * M]; for (int k = 0; k < a.Length; k++) a[k] = float.NaN; _oreDay[t] = a; }
            if (float.IsNaN(a[2 * m]))
            {
                float use = UseDest(t, m);
                a[2 * m] = (float)Math.Ceiling((TravelDays(t, m) + 2f) * use); a[2 * m + 1] = use;
            }
            return new[] { a[2 * m], a[2 * m + 1] };
        }

        private static bool OreEqOn { get { var s = Settings.Current; return s != null && s.TownMaterialOrderOreEqualCover; } }

        /// <summary>284 (klucz TownMaterialOrderEqualCoverAll): zrodlo do wlasnego punktu zamowienia i rownego zapasu dni - ruda zawsze (282), reszta tabeli przy kluczu.</summary>
        private static bool EqOn(int m)
        {
            var s = Settings.Current;
            return s != null && s.TownMaterialOrderOreEqualCover && (m == Ore || s.TownMaterialOrderEqualCoverAll);
        }

        private static int _eqCappedOther;   // 284: przyciecia rownym zapasem dni poza ruda (linia "Wsad warsztatow (284)")
        internal static int TakeEqCappedOther() { int x = _eqCappedOther; _eqCappedOther = 0; return x; }

        /// <summary>284 (klucz TownMaterialOrderWorkshopInputs, CommodityInputs284): cykl warsztatu towarowego bez wsadu kategorii c - sygnal zamowienia surowca tabeli o tej kategorii.</summary>
        internal static bool NoteMissCategory(Town town, ItemCategory c)
        {
            try
            {
                if (town == null || c == null || !Ready()) return false;
                for (int m = 0; m < M; m++) if (_items[m] != null && _items[m].ItemCategory == c) { Bump(_miss, town, m); return true; }
            }
            catch (Exception e) { Stumble("NoteMissCategory", e); }
            return false;
        }

        /// <summary>284: zuzycie wsadu warsztatu towarowego (udany cykl) w zmierzonym zuzyciu miasta - surowiec tabeli o tej kategorii.</summary>
        internal static bool NoteUseCategory(Town town, ItemCategory c, int n)
        {
            try
            {
                if (town == null || c == null || n <= 0 || !Ready()) return false;
                for (int m = 0; m < M; m++) if (_items[m] != null && _items[m].ItemCategory == c) { NoteUse(town, _items[m], n); return true; }
            }
            catch (Exception e) { Stumble("NoteUseCategory", e); }
            return false;
        }

        /// <summary>282 (F3, klucz TownMaterialOrderOreEqualCover): ile rudy miasto-zrodlo sprzeda na zamowienie miasta dst: ponad wlasny punkt zamowienia (zamiast
        /// progu karawan 174 - suma progow wszystkich miast byla wieksza niz wszystkie polki), i nie wiecej niz rowny zapas dni: po sprzedazy zrodlo ma co najmniej tyle
        /// dob zapasu, ile cel z polka i dowozem: (have - x) / uS >= (haveDst + inTr + x) / uD. Zrodlo bez zuzycia - bez tej granicy. Marza ceny i przerwa 3 dob -
        /// jak dotad (straz przed przerzucaniem tam i z powrotem).</summary>
        private static int MatSellable(Town src, int m, int have, Town dst, int haveDst, int inTrDst)
        {
            var a = MatDay(src, m);
            int sell = have - (int)a[0];
            if (sell <= 0) return sell;
            float uS = a[1], uD = dst != null ? MatDay(dst, m)[1] : 0f;
            if (uS > 0f && uS + uD > 0f)
            {
                double c = Math.Floor(((double)have * uD - (double)(haveDst + inTrDst) * uS) / (uS + uD));
                int cap = c >= int.MaxValue ? int.MaxValue : (int)Math.Max(-1.0, c);
                if (cap < sell) { if (m == Ore) _o2Capped++; else _eqCappedOther++; sell = cap; }
            }
            return sell;
        }

        private static void OrderTimed(Town town, int m, int day, bool ahead)
        {
            if (m == Ore) _o2Try++;   // 282: proby zamowien rudy (linia 282)
            long tc = Cost174.Begin(Cost174.SMoOrder);   // 174b.5 F6 (probka 1/16, tylko log)
            try { Order(town, m, day, ahead); } catch (Exception e) { Stumble("Order", e); }
            finally { Cost174.End(Cost174.SMoOrder, tc); }
        }

        // ------------------------------------------------------------ 174b.0: nazwy miast bez rudy i strzal
        private static int AmmoOn(ItemRoster r, ItemObject.ItemTypeEnum t)
        {
            int n = 0;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (el.Amount > 0 && it != null && it.ItemType == t) n += el.Amount;
            }
            return n;
        }

        private static string Top(List<KeyValuePair<string, int>> l, int k)
        {
            l.Sort((a, b) => b.Value != a.Value ? b.Value.CompareTo(a.Value) : string.CompareOrdinal(a.Key, b.Key));
            var parts = new List<string>();
            for (int i = 0; i < l.Count && i < k; i++) parts.Add(l[i].Key + " " + l[i].Value);
            return parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "-";
        }

        /// <summary>Linia "Miasta bez rudy i strzal (174b)" - pierwsza doba sesji i co 5 dob: nazwy miast bez rudy, bez strzal (i bez beltow - liczba),
        /// czesc wspolna, 10 miast z najwiecej cyklami "brak rudy" od ostatniej linii, 10 z najwiekszym zapasem rudy, zrodla rudy z nadwyzka (zapas /
        /// nadwyzka, ruda wywieziona kontraktami od ostatniej linii, konwoje - karawany ze statkami - stojace w porcie). Jedno przejscie 97 miast.</summary>
        private static void NamesLine(int day)
        {
            var ore = _items[Ore];
            var noOre = new List<string>(); var noArrows = new List<string>(); var both = new List<string>(); int noBolts = 0, towns = 0;
            var stock = new List<KeyValuePair<string, int>>(); var miss = new List<KeyValuePair<string, int>>(); var got = new List<KeyValuePair<string, int>>();
            var srcs = new List<KeyValuePair<int, string>>();
            var emptyT = new List<Town>(); var bigSrc = new List<Settlement>();   // P2 (TRIAZ-3): miasta bez rudy i zrodla-miasta z nadwyzka >= BigSurplus
            int n282 = 0, p282 = 0;   // 282 (F3): zrodla rudy wedlug wlasnego punktu zamowienia (tylko log)
            foreach (var t in Town.AllTowns)
            {
                try
                {
                    if (t == null || !t.IsTown || t.Owner == null || t.Owner.ItemRoster == null || t.Settlement == null) continue;
                    towns++;
                    var r = t.Owner.ItemRoster;
                    string nm = t.Name != null ? t.Name.ToString() : t.Settlement.StringId;
                    int have = ore != null ? r.GetItemNumber(ore) : 0;
                    bool a0 = AmmoOn(r, ItemObject.ItemTypeEnum.Arrows) <= 0;
                    if (AmmoOn(r, ItemObject.ItemTypeEnum.Bolts) <= 0) noBolts++;
                    if (have <= 0) { noOre.Add(nm); emptyT.Add(t); }
                    if (a0) noArrows.Add(nm);
                    if (have <= 0 && a0) both.Add(nm);
                    if (have > 0) stock.Add(new KeyValuePair<string, int>(nm, have));
                    int mo; if (_missOreSince.TryGetValue(t, out mo) && mo > 0) miss.Add(new KeyValuePair<string, int>(nm, mo));
                    int dq; if (_dstOreSince.TryGetValue(t, out dq) && dq > 0) got.Add(new KeyValuePair<string, int>(nm, dq));
                    int keep = SourceKeepDay(t, Ore);
                    int surplus = have - keep;
                    if (have > 0) { int f282 = have - (int)OreDay(t)[0]; if (f282 > 0) { n282++; p282 += f282; } }
                    if (surplus >= BigSurplus) bigSrc.Add(t.Settlement);
                    if (surplus > 0)
                    {
                        int convoys = 0;
                        foreach (var p in t.Settlement.Parties) if (p != null && p.IsCaravan && p.IsActive && !p.HasLandNavigationCapability) convoys++;
                        int outQ; _srcOreSince.TryGetValue(t.Settlement, out outQ);
                        srcs.Add(new KeyValuePair<int, string>(surplus, nm + " " + have + "/" + surplus + ", kontraktami " + outQ + ", konwojow w porcie " + convoys));
                    }
                }
                catch (Exception e) { Stumble("NamesLine(miasto)", e); }
            }
            noOre.Sort(string.CompareOrdinal); noArrows.Sort(string.CompareOrdinal); both.Sort(string.CompareOrdinal);
            srcs.Sort((a, b) => b.Key.CompareTo(a.Key));
            var sp = new List<string>(); for (int i = 0; i < srcs.Count && i < 15; i++) sp.Add(srcs[i].Value);
            Log.Info("Miasta bez rudy i strzal (174b): dzien " + day + " - miast " + towns + "; bez rudy " + noOre.Count + " [" + string.Join(", ", noOre.ToArray()) + "]; bez strzal "
                     + noArrows.Count + " [" + string.Join(", ", noArrows.ToArray()) + "]; bez beltow " + noBolts + "; bez rudy i bez strzal " + both.Count + " [" + string.Join(", ", both.ToArray())
                     + "]; najwiecej cykli \"brak rudy\" (warsztaty i strzelarze) od " + (_namesSince < 0 ? "startu sesji" : "dnia " + _namesSince) + ": " + Top(miss, 10)
                     + "; najwiekszy zapas rudy: " + Top(stock, 10) + "; ruda dostarczona kontraktami od " + (_namesSince < 0 ? "startu sesji" : "dnia " + _namesSince) + " (" + got.Count + " miast): " + Top(got, 200)
                     + "; zrodla rudy z nadwyzka (" + srcs.Count + "; zapas/nadwyzka, ruda wywieziona kontraktami od ostatniej linii, konwoje w porcie): "
                     + (sp.Count > 0 ? string.Join("; ", sp.ToArray()) : "-") + NamesExtra(emptyT, bigSrc, ore)
                     + "; zrodla rudy wedlug punktu zamowienia (282) " + n282 + " (sztuk ponad " + p282 + (OreEqOn ? "" : "; klucz Town Material Order Ore Equal Cover NIE - tylko pomiar") + ").");
            _missOreSince.Clear(); _srcOreSince.Clear(); _dstOreSince.Clear(); _namesSince = day;
            _dstOreCartSince.Clear(); _srcOreCartSince.Clear();
        }

        private const int BigSurplus = 200;

        /// <summary>P2 (TRIAZ-3 O2-1 pomiary b i d, O2-4 - tylko log) - dopisek linii nazw: zamki z ruda (10 najwiekszych), ruda dowieziona i wywieziona wozami
        /// kupcow (P3/Z1) od ostatniej linii, a dla kazdego miasta bez rudy: powod ostatniej odmowy zamowienia oraz droga ladem i rejs do najblizszego
        /// zrodla-miasta z nadwyzka >= BigSurplus (rejs tylko z portu do portu) - z podsumowaniem rejsow wobec 1000 (dzisiejszy TownMaterialOrderSeaMaxRoute)
        /// i 2400 (zasieg / udzial morza). Drogi z pamieci kampanii (CartRoads).</summary>
        private static string NamesExtra(List<Town> emptyT, List<Settlement> bigSrc, ItemObject ore)
        {
            var sb = new StringBuilder();
            try
            {
                var cs = new List<KeyValuePair<string, int>>(); int castleAll = 0;
                foreach (var t in Town.AllCastles)
                {
                    if (t == null || t.Settlement == null || t.Settlement.ItemRoster == null || ore == null) continue;
                    int n = t.Settlement.ItemRoster.GetItemNumber(ore);
                    if (n <= 0) continue;
                    castleAll += n;
                    cs.Add(new KeyValuePair<string, int>(t.Name != null ? t.Name.ToString() : t.Settlement.StringId, n));
                }
                sb.Append("; zamki z ruda ").Append(cs.Count).Append(" (razem ").Append(castleAll).Append("), najwiekszy zapas: ").Append(Top(cs, 10));
                var got = new List<KeyValuePair<string, int>>(); var gone = new List<KeyValuePair<string, int>>();
                foreach (var kv in _dstOreCartSince) if (kv.Key != null && kv.Value > 0) got.Add(new KeyValuePair<string, int>(kv.Key.Name.ToString(), kv.Value));
                foreach (var kv in _srcOreCartSince) if (kv.Key != null && kv.Value > 0) gone.Add(new KeyValuePair<string, int>(kv.Key.Name + (kv.Key.IsCastle ? " (zamek)" : ""), kv.Value));
                sb.Append("; ruda wozami kupcow (P3/Z1) od ostatniej linii: dowieziona (").Append(got.Count).Append(" miast): ").Append(Top(got, 200))
                  .Append("; wywieziona (").Append(gone.Count).Append(" zrodel): ").Append(Top(gone, 200));
                emptyT.Sort((a, b) => string.CompareOrdinal(a.Name.ToString(), b.Name.ToString()));
                int ports = 0, sea1000 = 0, sea2400 = 0, seaFar = 0, seaNone = 0, landIn = 0, mixIn = 0;
                var parts = new List<string>();
                var inv = CultureInfo.InvariantCulture;
                foreach (var t in emptyT)
                {
                    var st = t.Settlement;
                    float bl = -1f, bs = -1f, bm = -1f; Settlement sl = null, ss = null, sm = null;
                    foreach (var src in bigSrc)
                    {
                        if (src == null || src == st) continue;
                        float dL, dS;
                        CartRoads(src, st, true, st.HasPort && src.HasPort, out dL, out dS);
                        if (dL > 0f && (bl < 0f || dL < bl)) { bl = dL; sl = src; }
                        if (dS > 0f && (bs < 0f || dS < bs)) { bs = dS; ss = src; }
                        if (!(dL > 0f)) { float dM = CartRoadMixed(src, st); if (dM > 0f && (bm < 0f || dM < bm)) { bm = dM; sm = src; } }   // recenzja P3/Z1: ladem i morzem
                    }
                    if (bl > 0f && bl <= 600f) landIn++;
                    else if (sl == null && bm > 0f && bm <= 600f) mixIn++;
                    if (st.HasPort) { ports++; if (bs < 0f) seaNone++; else if (bs <= 1000f) sea1000++; else if (bs <= 2400f) sea2400++; else seaFar++; }
                    int w;
                    parts.Add(t.Name + ": " + (_oreWhy.TryGetValue(t, out w) && w >= 0 && w < WKinds ? WName[w] : "bez zamowienia")
                              + ", lad " + (sl != null ? bl.ToString("0", inv) + " (" + sl.Name + ")" : sm != null ? "- (ladem i morzem " + bm.ToString("0", inv) + ", " + sm.Name + ")" : "-")
                              + " / rejs " + (ss != null ? bs.ToString("0", inv) + " (" + ss.Name + ")" : (st.HasPort ? "-" : "bez portu")));
                }
                sb.Append("; miasta bez rudy wobec zrodel-miast z nadwyzka >= ").Append(BigSurplus).Append(" (").Append(bigSrc.Count).Append(" zrodel): droga ladem <= 600 ma ").Append(landIn)
                  .Append(" z ").Append(emptyT.Count).Append("; porty bez rudy ").Append(ports).Append(": rejs <= 1000 ").Append(sea1000).Append(", 1000-2400 ").Append(sea2400)
                  .Append(", > 2400 ").Append(seaFar).Append(", bez zrodla-portu ").Append(seaNone)
                  .Append("; bez drogi ladowej, ale ladem i morzem <= 600 ma ").Append(mixIn)
                  .Append("; powod ostatniej odmowy i najblizsze zrodlo (lad / rejs): [").Append(parts.Count > 0 ? string.Join("; ", parts.ToArray()) : "-").Append(']');
            }
            catch (Exception e) { Stumble("NamesExtra", e); }
            return sb.ToString();
        }

        /// <summary>174b.1: przeglad doby - tylko limit 30 dob i wylacznik (logika celu jest w takcie godzinowym Hourly); zniszczona karawana - jak dotad.</summary>
        private static void Keep(int day)
        {
            for (int i = _contracts.Count - 1; i >= 0; i--)
            {
                var c = _contracts[i];
                try
                {
                    var car = c.Car;
                    if (car == null || !car.IsActive) { Drop(c); _dLost++; _dLostQty += c.Qty; continue; }
                    if (day - c.Day > 30) { Release(c); _dRel30++; continue; }
                    if (!On) { Release(c); _dRelOther++; continue; }
                    if (NightRest.IsCamping(car)) _dCampSeen++;   // 174b.0: pomiar (przeglad doby w godzinie obozu)
                }
                catch (Exception e) { Stumble("Keep", e); }
            }
        }

        /// <summary>
        /// 174b.1 KONTRAKT TRZYMA CEL (docs/PROJEKT-174B rozdz. 3.1). Wolane w delegacie godzinowym ZARAZ PO NightRest.OnHourly (ArmouryBehavior) - stan obozu
        /// tej godziny jest juz ustawiony. Dla kazdego kontraktu w tej kolejnosci: karawany nie ma - przepada; w bitwie - nic; rozwiazywana - zwolnienie;
        /// cel oblegany albo wrogi - zwolnienie; stoi w celu - dostawa; SPI w nocnym obozie swiata - nic (jedna regula: karawana z kontraktem spi jak kazda,
        /// swit odda jej cel - licznik godzin "w obozie"); w obleganym miescie - nic; cel = brak albo postoj (Hold: po bitwie MapEvent.cs:903, zmiana
        /// wlasciciela miasta, oblezenie) - wyjazd z osady i rozkaz jazdy do celu BEZ licznika zmian celu ("ruszona z postoju"; po MaxHoldStreak kolejnych
        /// godzinach - zwolnienie "postoj wymuszany"); cel = INNE miasto - jak dotad: po MaxRetarget przywroceniach zwolnienie "cel zmieniany przez innych",
        /// inaczej rozkaz i "cel przywrocony". Dotad to samo robil raz na dobe Keep (doba w srodku obozu = kazda noc liczona jako cudzy cel) i prefiks BK
        /// raz na dobe (dlawik BKROT).
        /// </summary>
        internal static void Hourly()
        {
            if (_pending != null) { try { ResolvePending("godzina przed startem sesji"); } catch (Exception e) { Stumble("Hourly(ResolvePending)", e); } }
            if (_contracts.Count == 0) return;
            long tc = Cost174.Begin(Cost174.SMoHourly);   // 174b.5 F6 (probka 1/16, tylko log)
            try
            {
                for (int i = _contracts.Count - 1; i >= 0; i--)
                {
                    if (i >= _contracts.Count) continue;   // dostawa / zwolnienie usuwa z listy
                    var c = _contracts[i];
                    try
                    {
                        var car = c.Car;
                        if (car == null || !car.IsActive) { Drop(c); _dLost++; _dLostQty += c.Qty; continue; }
                        if (car.MapEvent != null) continue;
                        if (!On) { Release(c); _dRelOther++; continue; }   // poprawka 174b (recenzja): wylacznik w MCM dziala w ciagu godziny, nie dopiero w przegladzie doby
                        if (car.IsDisbanding) { Release(c); _dRelDisband++; continue; }
                        if (DestLost(car, c)) { Release(c); continue; }
                        if (car.CurrentSettlement == c.Dest) { Deliver(car, c.Dest); continue; }
                        if (NightRest.IsCamping(car)) { _dCampHours++; continue; }
                        if (car.CurrentSettlement != null && car.CurrentSettlement.IsUnderSiege) continue;   // nie wyprowadzamy jej z obleganego miasta prosto do obozu oblegajacych
                        bool idle = car.TargetSettlement == null || car.DefaultBehavior == AiBehavior.Hold;
                        if (!idle && car.TargetSettlement == c.Dest) { c.HoldStreak = 0; continue; }   // jedzie do celu (ucieczka zmienia tylko cel krotkoterminowy)
                        if (idle)
                        {
                            if (++c.HoldStreak > MaxHoldStreak)
                            {
                                Log.Info("Kontrakty surowca (174): " + car.Name + " - postoj wymuszany " + MaxHoldStreak + " godzin z rzedu (cel " + c.Dest.Name
                                         + (car.CurrentSettlement != null ? ", stoi w " + car.CurrentSettlement.Name : ", w polu") + ") - kontrakt zwolniony, ladunek zostaje karawanie.");
                                Release(c); _dRelForcedHold++; continue;
                            }
                            try { if (car.CurrentSettlement != null) LeaveSettlementAction.ApplyForParty(car); } catch (Exception e) { Stumble("Hourly(wyjazd)", e); }   // jak posilki DTE
                            if (Move(car, c.Dest, c.Naval)) _dHoldMoved++; else { Release(c); _dRelOther++; }
                            continue;
                        }
                        // cel = inne miasto
                        c.HoldStreak = 0;
                        if (c.Retarget >= MaxRetarget) { Release(c); _dRelRetarget++; continue; }   // cel zmieniaja inni - karawana zostaje z towarem
                        if (_dRetEx.Count < 3)
                            _dRetEx.Add(car.Name + " -> " + car.TargetSettlement.Name + " zamiast " + c.Dest.Name
                                        + (car.CurrentSettlement != null ? " (w " + car.CurrentSettlement.Name + ")" : car.IsCurrentlyAtSea ? " (na morzu)" : " (w polu)"));
                        try { if (car.CurrentSettlement != null) LeaveSettlementAction.ApplyForParty(car); } catch (Exception e) { Stumble("Hourly(wyjazd)", e); }
                        if (Move(car, c.Dest, c.Naval)) { _dRetarget++; c.Retarget++; } else { Release(c); _dRelOther++; }
                    }
                    catch (Exception e) { Stumble("Hourly", e); }
                }
            }
            finally { Cost174.End(Cost174.SMoHourly, tc); }
        }

        /// <summary>
        /// 174b.2 DOWOZ: NAJPIERW TRASA, POTEM KARAWANA, KTORA NIA POJEDZIE (docs/PROJEKT-174B rozdz. 3.2 i "Krytyka i odpowiedzi" uwagi 8 i 16). Regula
        /// "kontrakt tylko z zyskiem ponad koszt drogi" i wzor oplaty bez zmian. Zrodla z Town.AllTowns (zrodlem i tak moze byc tylko miasto). Dla kazdego
        /// zrodla w zasiegu: droga LADEM (pamiec drog gry, Default; koszt = droga) i MORZEM (oba miasta z portem, wylacznik TownMaterialOrderBySea; pamiec
        /// drog Naval; rejs <= TownMaterialOrderSeaMaxRoute, koszt = rejs x SeaFreightShare <= zasieg). Przewoznik dobrany do trasy: lad - karawana z ladem,
        /// morze - karawana ze statkami (konwoj; w porcie gra liczy mu ladownie - IsCurrentlyAtSea zostaje true, wiec wolne miejsce jak dotad). Towar: zakup
        /// z polki zrodla ponad prog nadwyzki (jak dotad) albo (TownMaterialOrderFromPacks) surowiec, ktory karawana stojaca w zrodle JUZ wiezie - bez zakupu,
        /// marza wobec sprzedazy na miejscu. Ilosc wedlug zysku: q0, a gdy marza q0 <= 0 - q0/2, q0/4, ... dopoki ladunek >= TownMaterialOrderMinLoadKg.
        /// Wygrywa najwieksza marza na kg. Ile zamowic: zapas na max(10, D + 4) dob zuzycia (D - srednia dob drogi 3 ostatnich dostaw do miasta, domyslnie 4;
        /// zuzycie z obecnych rak - krytyka 16) minus polka minus w drodze.
        /// </summary>
        private static void Order(Town town, int m, int day, bool ahead)
        {
            var s = Settings.Current;
            int[] last;
            if (!_last.TryGetValue(town, out last)) { last = new int[M]; for (int k = 0; k < M; k++) last[k] = int.MinValue / 2; _last[town] = last; }
            if (day - last[m] < Math.Max(1, s.TownMaterialOrderDays)) { _dPause++; if (m == Ore) _o2Pause++; return; }
            var item = _items[m];
            var dest = town.Settlement;
            if (dest == null || dest.IsUnderSiege || town.Owner == null || town.Owner.ItemRoster == null) return;
            float use = UseDest(town, m), D = TravelDays(town, m);
            int haveDst = town.Owner.ItemRoster.GetItemNumber(item), inTrDst = InTransit(dest, m);
            int want = (int)Math.Ceiling(Math.Max(10f, D + 4f) * use) - haveDst - inTrDst;
            if (want <= 0) { _dEnough++; if (m == Ore) _o2Enough++; return; }
            bool oreEq = EqOn(m);   // 282 (F3): ruda - zrodlo do wlasnego punktu zamowienia i rownego zapasu dni; 284: kazdy surowiec tabeli (TownMaterialOrderEqualCoverAll)
            bool perDay = s.TownMaterialOrderNeediestFirst;               // 282 (F2): wybor zrodla wedlug zysku na dobe drogi wozu
            float bestScore = 0f;
            // P3/Z1 (O2-1 P1 pkt 2): przy lupie (Bloomery.On - z kopalni wyjezdza zelazo, nie ruda) i kontrakcie wozem zelazo jedzie jak inne surowce
            // (TownMaterialOrderRange), takze kontraktem karawany; klucz wylaczony albo bez dymarek - ruda z bliska jak dotad
            bool byCart = s.TownMaterialOrderByCart;
            float range = Math.Max(1f, m == Ore && !(byCart && Bloomery.On) ? s.TownMaterialOrderRangeOre : s.TownMaterialOrderRange);
            float carter = Math.Max(0f, s.CarterPencePerKgPer100), sea = MBMath.ClampFloat(s.SeaFreightShare, 0f, 1f);
            float kg = Kg(item);
            float minKg = Math.Max(0f, s.TownMaterialOrderMinLoadKg);
            bool seaOn = s.TownMaterialOrderBySea && dest.HasPort;
            float seaMax = Math.Max(1f, s.TownMaterialOrderSeaMaxRoute);
            float seaLine = seaOn ? (sea > 0f ? Math.Min(seaMax, range / sea) : seaMax) : 0f;   // rejs >= linia prosta
            bool packsOn = s.TownMaterialOrderFromPacks;
            var dm = Campaign.Current.Models.MapDistanceModel;
            var pos = dest.GetPosition2D;
            var dstPrice = new Dictionary<int, int>();   // cena srodkowej sztuki w celu wedlug przesuniecia (ta sama dla wszystkich zrodel)
            Settlement bestSrc = null; MobileParty bestCar = null; int bestQ = 0; float bestPerKg = 0f, bestDist = 0f, bestMargin = 0f; bool bestNaval = false, bestPacks = false;
            bool anySrc = false, anyRoute = false, anyCar = false, anySmall = false, landNoCar = false, carNoRoom = false;
            int why = 0;   // bez drogi: 1 wyspa / inna czesc ladu bez portu, 2 droga ladem > zasieg, 3 morze poza zasiegiem albo dlugoscia (4 brak konwoju - nizej)
            foreach (var t in Town.AllTowns)
            {
                var src = t != null ? t.Settlement : null;
                if (src == null || src == dest || !t.IsTown || src.ItemRoster == null) continue;
                float line = pos.Distance(src.GetPosition2D);
                bool ports = seaOn && src.HasPort;
                bool landTry = line <= range, seaTry = ports && line <= seaLine;            // droga i rejs >= linia prosta
                if (!landTry && !seaTry) continue;
                if (src.IsUnderSiege || (src.MapFaction != null && dest.MapFaction != null && FactionManager.IsAtWarAgainstFaction(src.MapFaction, dest.MapFaction))) continue;
                // przewoznicy stojacy w zrodle: lad / morze x zakup (najwiecej wolnego miejsca) / z jukow (najwiecej tego surowca w jukach).
                // Poprawka 174b (recenzja, wazna 1): NAJPIERW przewoznicy (tanie: Eligible, wolne miejsce, juki), dopiero potem prog nadwyzki zrodla (SourceKeep -
                // trzy przejscia CaravanBulk) i trasy. Wolne miejsce liczone dla kazdej karawany, a przy braku nadwyzki przewoznik zakupu jest kasowany nizej -
                // ten sam wybor co dotad.
                MobileParty cL = null, cS = null, pL = null, pS = null; float fL = 0f, fS = 0f; int nL = 0, nS = 0;
                foreach (var p in src.Parties)
                {
                    if (!Eligible(p, dest)) continue;
                    bool land = p.HasLandNavigationCapability, ship = p.HasNavalNavigationCapability;
                    float free = p.InventoryCapacity - p.TotalWeightCarried;   // konwoj w porcie: ladownia (IsCurrentlyAtSea), krytyka 8
                    if (land && free > fL) { fL = free; cL = p; }
                    if (ship && free > fS) { fS = free; cS = p; }
                    if (packsOn)
                    {
                        int inPack = p.ItemRoster.GetItemNumber(item);
                        if (inPack > 0 && inPack * kg >= minKg)
                        {
                            if (land && inPack > nL) { nL = inPack; pL = p; }
                            if (ship && inPack > nS) { nS = inPack; pS = p; }
                        }
                    }
                }
                // zrodlo bez zadnego przewoznika zmienia tylko liczniki "bez kontraktu" (zrodlo, droga, brak karawany); gdy wszystkie trzy sa juz ustawione,
                // niczego nie zmieni - pomijamy je bez progu nadwyzki i bez tras (wynik i liczniki te same)
                if (cL == null && cS == null && pL == null && pS == null && anySrc && anyRoute && landNoCar) continue;
                int have = src.ItemRoster.GetItemNumber(item);
                int surplus = have <= 0 ? 0 : oreEq ? MatSellable(t, m, have, town, haveDst, inTrDst) : have - SourceKeepDay(t, m);
                if (surplus <= 0) { cL = cS = null; fL = fS = 0f; }   // przewoznik zakupu tylko przy nadwyzce (jak dotad)
                if (surplus <= 0 && pL == null && pS == null) continue;   // nie ma czego wiezc
                anySrc = true;
                // trasy
                float dL = -1f, dS = -1f; bool landOk = false, seaOk = false;
                if (landTry)
                {
                    try { dL = dm.GetDistance(src, dest, false, false, MobileParty.NavigationType.Default); } catch { dL = -1f; }
                    landOk = dL >= 0f && dL < CartTownExit.BkLimit && dL <= range;
                }
                if (seaTry)
                {
                    try { dS = dm.GetDistance(src, dest, true, true, MobileParty.NavigationType.Naval); } catch (Exception e) { dS = -1f; Stumble("Order(pamiec drog Naval)", e); }
                    seaOk = dS >= 0f && dS < CartTownExit.BkLimit && dS <= seaMax && dS * sea <= range;
                }
                if (!landOk && !seaOk)
                {
                    int w = seaTry ? 3 : (dL >= 0f && dL < CartTownExit.BkLimit ? 2 : 1);
                    if (w > why) why = w;
                    continue;
                }
                anyRoute = true;
                for (int opt = 0; opt < 4; opt++)
                {
                    bool naval = opt >= 2, packs = (opt & 1) == 1;
                    if (naval ? !seaOk : !landOk) continue;
                    if (packs ? !packsOn : surplus <= 0) continue;
                    var car = naval ? (packs ? pS : cS) : (packs ? pL : cL);
                    if (car == null) { if (!naval) landNoCar = true; continue; }   // morze bez konwoju w porcie - "bez drogi: brak konwoju" (nizej)
                    float d = naval ? dS : dL;
                    int q, pSrc = 0;
                    if (packs) q = Math.Min(want, naval ? nS : nL);
                    else
                    {
                        float free = naval ? fS : fL;
                        if (free < kg) { carNoRoom = true; continue; }   // poprawka 174b (recenzja): przewoznik jest, ale bez miejsca - nie "bez drogi"
                        pSrc = PriceAt(t, item, false, 0);
                        if (pSrc <= 0) { carNoRoom = true; continue; }
                        q = Math.Min(Math.Min(want, surplus), (int)(free / kg));
                        q = Math.Min(q, car.PartyTradeGold / Math.Max(1, pSrc));
                    }
                    if (q <= 0) { carNoRoom = true; continue; }   // bez miejsca albo kiesy na choc jedna sztuke
                    anyCar = true;
                    if (q * kg < minKg) { anySmall = true; continue; }   // recenzja 174: ladunek za maly na dni drogi bez handlu - czekamy, az brak urosnie
                    // ilosc wedlug zysku (krytyka 174b: polowienie, gdy duzy ladunek zbija cene celu ponizej zakupu + oplaty)
                    for (int guard = 0; guard < 8 && q >= 1 && q * kg >= minKg; guard++, q /= 2)
                    {
                        int mid = Math.Max(0, q - 1) / 2;
                        int pDst; if (!dstPrice.TryGetValue(mid, out pDst)) { pDst = PriceAt(town, item, true, mid); dstPrice[mid] = pDst; }
                        int pHere = packs ? PriceAt(t, item, true, mid) : PriceAt(t, item, false, -mid);   // z jukow: wobec sprzedazy TU; zakup: cena srodkowej sztuki zrodla
                        if (pDst <= 0 || pHere <= 0) break;
                        float fee = carter * kg * q * d / 100f * (naval ? sea : 1f);
                        float margin = (float)q * pDst - (float)q * pHere - fee;
                        if (margin <= 0f)
                        {
                            if (_dEx.Count < 3 && q * kg / 2 < minKg) _dEx.Add(town.Name + " - " + src.Name + " " + (int)d + (naval ? " morzem" : "") + (packs ? " z jukow" : "") + " " + Names[m] + " bez zysku (" + pDst + "/" + pHere + ")");
                            continue;
                        }
                        float perKg = margin / (q * kg);
                        float score = perDay ? perKg / Math.Max(1, TradeCarts.DaysFor(d)) : perKg;   // 282 (F2): zysk na kg i dobe drogi (doby z modelu wozow)
                        if (score > bestScore) { bestScore = score; bestPerKg = perKg; bestSrc = src; bestCar = car; bestQ = q; bestDist = d; bestNaval = naval; bestMargin = margin; bestPacks = packs; }
                        break;
                    }
                }
            }
            if (bestSrc == null)
            {
                // P3/Z1 (O2-1 P1): zadna karawana nie wziela zamowienia - drugi przebieg bez przewoznika (woz kupcow; zrodla: miasta i zamki).
                // Udany zakup wozem to kontrakt (przerwa TownMaterialOrderDays, punkt zamowienia); nieudany - liczniki "bez kontraktu" jak dotad
                int w = !anySrc ? WNoSrc : !anyRoute ? WNoRoad : !anyCar ? WCar : anySmall ? WSmall : WNoGain;
                if (byCart)
                {
                    long tc = System.Diagnostics.Stopwatch.GetTimestamp();
                    bool ok = false;
                    _dCartAsk++;
                    try { ok = OrderByCart(town, m, day, s, use, range, carter, sea, kg, minKg, seaOn, seaMax, seaLine, out w); }
                    catch (Exception e) { Stumble("OrderByCart", e); }
                    finally { _dCartTicks += System.Diagnostics.Stopwatch.GetTimestamp() - tc; }
                    if (ok)
                    {
                        last[m] = day;
                        if (ahead) _dAhead++;
                        if (m == Ore) _oreWhy.Remove(town);
                        return;
                    }
                }
                if (w >= 0 && w < WKinds) _dWhy[m, w]++;
                if (m == Ore) _oreWhy[town] = w;
                if (!anySrc) _dNoSrc++;
                else if (!anyRoute) { _dNoRoad++; if (why >= 1 && why <= 4) _dNoRoadBy[why - 1]++; }
                else if (!anyCar)
                {
                    if (landNoCar) _dNoCar++;
                    else if (carNoRoom) _dNoRoom++;                    // poprawka 174b (recenzja): przewoznik jest (lad albo konwoj), ale bez miejsca albo kiesy
                    else { _dNoRoad++; _dNoRoadBy[3]++; }               // trasa tylko morska, a w porcie zrodla nie ma konwoju
                }
                else if (anySmall) _dSmall++;
                else _dNoGain++;
                return;
            }
            last[m] = day;
            if (ahead) _dAhead++;
            if (m == Ore) _oreWhy.Remove(town);
            int made0 = _dMade[Ore];
            Place(bestCar, bestSrc, dest, m, bestQ, bestDist, bestNaval, bestMargin, day, bestPacks);
            if (m == Ore && _dMade[Ore] > made0)
            {
                int dd = TradeCarts.DaysFor(bestDist);   // 282: tylko log (doby drogi i zysk na dobe kontraktu karawany - ta sama miara co woz); tylko kontrakt zawarty
                _o2CarDays += dd; _o2Profit += bestMargin / Math.Max(1, dd); _o2ProfitN++;
            }
        }

        // ------------------------------------------------------------ P3/Z1: kontrakt wozem kupcow (drugi przebieg zamowienia)
        private static int OreEmptyTowns()
        {
            int n = 0;
            var ore = _items != null ? _items[Ore] : null;
            if (ore == null) return -1;
            foreach (var t in Town.AllTowns)
                if (t != null && t.IsTown && t.Owner != null && t.Owner.ItemRoster != null && t.Owner.ItemRoster.GetItemNumber(ore) <= 0) n++;
            return n;
        }

        /// <summary>MaterialCarts.Arrivals: woz dojechal - dni drogi tej dostawy do pamieci miasta (NoteTrip: punkt zamowienia i ilosc nastepnego zamowienia
        /// licza sie z prawdziwej drogi wozu, nie z domyslnych 4 dob) i licznik rudy do linii nazw.</summary>
        private static void OnCartArrived(Settlement dst, int m, int n, int days)
        {
            var t = dst != null ? dst.Town : null;
            if (t == null) return;
            NoteTrip(t, m, Math.Max(1, days));
            if (m == Ore) { int q; _dstOreCartSince.TryGetValue(t, out q); _dstOreCartSince[t] = q + n; _o2CartDone += n; }   // 282: + licznik doby
        }

        /// <summary>Zrodla surowca m dla wozow kupcow - raz na Daily: miasta ponad prog nadwyzki kontraktow (SourceKeepDay) i ZAMKI ponad 10 dob wlasnego
        /// zuzycia (CastleKeep), z nadwyzka co najmniej jednego ladunku minimalnego. Polke zrodla czyta potem kazde zamowienie na swiezo.</summary>
        private static List<CartSrc> CartSources(int m, int minPcs)
        {
            var list = _cartSrc[m];
            if (list != null) return list;
            list = new List<CartSrc>();
            var item = _items[m];
            foreach (var t in Town.AllFiefs)
            {
                try
                {
                    var st = t != null ? t.Settlement : null;
                    if (st == null || st.ItemRoster == null || t.MarketData == null) continue;
                    int have = st.ItemRoster.GetItemNumber(item);
                    if (have < minPcs) continue;
                    int keep = !t.IsTown ? CastleKeep(t, m) : EqOn(m) ? (int)MatDay(t, m)[0] : SourceKeepDay(t, m);   // 282 (F3): ruda - wlasny punkt zamowienia miasta; 284: kazdy surowiec przy kluczu
                    if (have - keep < minPcs) continue;
                    list.Add(new CartSrc { T = t, St = st, Keep = keep });
                }
                catch (Exception e) { Stumble("CartSources", e); }
            }
            _cartSrc[m] = list;
            return list;
        }

        /// <summary>Prog nadwyzki zamku-zrodla: 10 dob wlasnego zuzycia = zmierzone zuzycie (NoteUse - zamek nie ma warsztatow, zwykle 0) + popyt ludnosci
        /// podzamcza na ten towar (budzet dnia BK kategorii / cena targu zamku - ta sama miara co targ przy zamku 5.11). Z pamieci doby (_keepDay; dla
        /// zamkow nikt inny jej nie wypelnia).</summary>
        private static int CastleKeep(Town t, int m)
        {
            int[] a;
            if (!_keepDay.TryGetValue(t, out a)) { a = new int[M]; for (int k = 0; k < M; k++) a[k] = -1; _keepDay[t] = a; }
            if (a[m] < 0)
            {
                double perDay = UseOf(t, m);
                try { perDay += CastleMarket.FolkPerDay(t, _items[m]); } catch (Exception e) { Stumble("CastleKeep", e); }
                a[m] = Math.Max(0, (int)Math.Ceiling(10.0 * perDay));
            }
            return a[m];
        }

        /// <summary>Droga pary zrodlo-cel z pamieci kampanii: lad (pamiec drog gry, Default - jak kontrakt karawany) i morze (oba porty, Naval);
        /// -1 = bez drogi. Odleglosc 0 miedzy roznymi osadami to tez "bez drogi" (woz nie jest partia - nie moze dojechac droga, ktorej nie ma).</summary>
        private static void CartRoads(Settlement src, Settlement dest, bool land, bool seaTry, out float dL, out float dS)
        {
            long key = ((long)src.Id.InternalValue << 32) | dest.Id.InternalValue;
            float[] r;
            if (!_cartRoad.TryGetValue(key, out r)) { r = new[] { float.NaN, float.NaN, float.NaN }; _cartRoad[key] = r; }
            var dm = Campaign.Current.Models.MapDistanceModel;
            if (land && float.IsNaN(r[0]))
            {
                float d;
                try { d = dm.GetDistance(src, dest, false, false, MobileParty.NavigationType.Default); } catch { d = -1f; }
                r[0] = d > 0f && d < CartTownExit.BkLimit ? d : -1f;
            }
            if (seaTry && float.IsNaN(r[1]))
            {
                float d;
                try { d = dm.GetDistance(src, dest, true, true, MobileParty.NavigationType.Naval); } catch (Exception e) { d = -1f; Stumble("CartRoads(pamiec drog Naval)", e); }
                r[1] = d > 0f && d < CartTownExit.BkLimit ? d : -1f;
            }
            dL = land ? r[0] : -1f; dS = seaTry ? r[1] : -1f;
        }

        /// <summary>Recenzja P3/Z1 (O2-1 "Poprawka ostateczna" P1 pkt 2: "lad TradeCarts.Road"): droga LADEM I MORZEM pary bez drogi ladowej - ten sam
        /// rachunek, ktorym jezdza wozy kupcow R3 i wozy dymarek F4a (TradeCarts.Road: pamiec drog gry NavigationType.All, port tam, gdzie osada go ma);
        /// -1 = bez drogi. Potrzebna, gdy zrodlo albo cel nie ma portu (rejs port-port odpada): Pyke (wyspa bez portu, w biegu rocznym 1 572 lup) i
        /// Barrowton (2 442 lup, bez portu) nie mialy inaczej drogi do miast-wysp, a 19 z 55 miast bez zelaza z doby 364 nie ma portu. Koszt jak lad
        /// (bez znizki morza), czas - predkosc wozu na calej drodze. Z pamieci kampanii (trzecie pole _cartRoad).</summary>
        private static float CartRoadMixed(Settlement src, Settlement dest)
        {
            long key = ((long)src.Id.InternalValue << 32) | dest.Id.InternalValue;
            float[] r;
            if (!_cartRoad.TryGetValue(key, out r)) { r = new[] { float.NaN, float.NaN, float.NaN }; _cartRoad[key] = r; }
            if (float.IsNaN(r[2]))
            {
                float d;
                try { d = Campaign.Current.Models.MapDistanceModel.GetDistance(src, dest, src.HasPort, dest.HasPort, MobileParty.NavigationType.All); }
                catch (Exception e) { d = -1f; Stumble("CartRoadMixed(pamiec drog All)", e); }
                r[2] = d > 0f && d < CartTownExit.BkLimit ? d : -1f;
            }
            return r[2];
        }

        /// <summary>
        /// P3/Z1 KONTRAKT WOZEM (TRIAZ-3 O2-1 "Poprawka ostateczna" P1). Te same zasady co kontrakt karawany, bez wymogu przewoznika stojacego w zrodle:
        /// zrodlo niewrogie i nieoblezone, z nadwyzka ponad swoj prog; droga ladem &lt;= zasieg albo morzem (oba porty, rejs &lt;= TownMaterialOrderSeaMaxRoute,
        /// rejs x SeaFreightShare &lt;= zasieg) - wybrana tansza; para bez drogi ladowej: droga ladem i morzem liczona jak lad (CartRoadMixed - recenzja,
        /// jak TradeCarts.Road wozow R3 i F4a); ilosc: zapas na max(10, D + 4) dob zuzycia minus polka minus w drodze, gdzie D = doby
        /// wozu dla TEJ pary (TradeCarts.DaysFor), nie mniej niz ladunek minimalny (woz nie jedzie z mniej niz TownMaterialOrderMinLoadKg - miasto o malym
        /// zuzyciu bierze zapas na dluzej, inaczej nie zamowiloby nigdy), nie wiecej niz nadwyzka zrodla; warunek zysku z oplata drogi jak w kontrakcie
        /// (cena srodkowej sztuki celu - cena srodkowej sztuki zrodla - oplata &gt; 0, polowienie ilosci); wygrywa najwieksza marza na kg.
        /// Zakup: sztuka po sztuce z polki zrodla (ShelfBuy.Take), placi kasa miasta-celu ponad zapas kupcow (TownPurse.BuyFloor) -> kasa zrodla
        /// (miasto albo zamek); oplaty frachtu nikt nie placi (jak R3 i F4a). Towar: MaterialCarts.Ship - na polke celu po dobach drogi.
        /// </summary>
        private static bool OrderByCart(Town town, int m, int day, Settings s, float use, float range, float carter, float sea, float kg, float minKg,
                                        bool seaOn, float seaMax, float seaLine, out int why)
        {
            why = WNoSrc;
            var item = _items[m];
            var dest = town.Settlement;
            int minPcs = Math.Max(1, (int)Math.Ceiling(minKg / kg));
            int haveDst = town.Owner.ItemRoster.GetItemNumber(item), inTr = InTransit(dest, m);
            int spare = Math.Max(0, town.Gold - TownPurse.BuyFloor(town, (int)Math.Max(0f, s.TownRentFloorGold)));   // E5/A1: tylko nadwyzka ponad zapas kupcow
            var pos = dest.GetPosition2D;
            var dstPrice = new Dictionary<int, int>();
            CartSrc best = null; int bestQ = 0, bestDays = 0; float bestPerKg = 0f, bestScore = 0f; bool bestNaval = false, bestMixed = false;
            bool anySrc = false, anyRoute = false, anyWant = false, noGold = false, anySmall = false;
            bool oreEq = EqOn(m), perDay = s.TownMaterialOrderNeediestFirst;   // 282 (F3, F2); 284: rowny zapas dni dla kazdego surowca przy kluczu
            foreach (var c in CartSources(m, minPcs))
            {
                var src = c.St;
                if (src == dest) continue;
                float line = pos.Distance(src.GetPosition2D);
                bool landTry = line <= range, seaTry = seaOn && src.HasPort && line <= seaLine;   // droga i rejs >= linia prosta
                if (!landTry && !seaTry) continue;
                if (src.IsUnderSiege || (src.MapFaction != null && dest.MapFaction != null && FactionManager.IsAtWarAgainstFaction(src.MapFaction, dest.MapFaction))) continue;
                int haveSrc = src.ItemRoster.GetItemNumber(item);
                int surplus = oreEq && c.T.IsTown ? MatSellable(c.T, m, haveSrc, town, haveDst, inTr) : haveSrc - c.Keep;   // polka na swiezo - z tego zrodla kupuja dzis takze inne miasta
                if (surplus < minPcs) continue;
                anySrc = true;
                float dL, dS;
                CartRoads(src, dest, landTry, seaTry, out dL, out dS);
                // recenzja P3/Z1: para bez drogi ladowej (wyspa, inny lad) - droga ladem i morzem jak wozy R3 i F4a (TradeCarts.Road), liczona jak lad;
                // przy wylaczonym TownMaterialOrderBySea - bez niej (zamowienia tylko ladem, jak dotad)
                bool mixed = false;
                if (landTry && !(dL > 0f) && s.TownMaterialOrderBySea) { dL = CartRoadMixed(src, dest); mixed = dL > 0f; }
                bool landOk = dL > 0f && dL <= range, seaOk = dS > 0f && dS <= seaMax && dS * sea <= range;
                if (!landOk && !seaOk) continue;
                anyRoute = true;
                bool naval = seaOk && (!landOk || dS * sea < dL);   // tansza droga: lad = droga, morze = rejs x udzial morza (jak oplata kontraktu)
                float d = naval ? dS : dL;
                int days = TradeCarts.DaysFor(d);
                int want = (int)Math.Ceiling(Math.Max(10f, days + 4f) * use) - haveDst - inTr;
                if (want <= 0) continue;
                anyWant = true;
                int q = Math.Min(Math.Max(want, minPcs), surplus);
                if (q * kg < minKg) { anySmall = true; continue; }
                int p0 = PriceAt(c.T, item, false, 0);
                if (p0 <= 0) continue;
                if ((long)p0 * minPcs > spare) { noGold = true; continue; }   // kasy ponad zapas kupcow nie starcza na ladunek minimalny
                q = Math.Min(q, spare / p0);
                for (int guard = 0; guard < 8 && q >= 1 && q * kg >= minKg; guard++, q /= 2)
                {
                    int mid = Math.Max(0, q - 1) / 2;
                    int pDst; if (!dstPrice.TryGetValue(mid, out pDst)) { pDst = PriceAt(town, item, true, mid); dstPrice[mid] = pDst; }
                    int pHere = PriceAt(c.T, item, false, -mid);
                    if (pDst <= 0 || pHere <= 0) break;
                    float fee = carter * kg * q * d / 100f * (naval ? sea : 1f);
                    float margin = (float)q * pDst - (float)q * pHere - fee;
                    if (margin <= 0f) continue;
                    float perKg = margin / (q * kg);
                    float score = perDay ? perKg / Math.Max(1, days) : perKg;   // 282 (F2): zysk na kg i dobe drogi tego wozu
                    if (score > bestScore) { bestScore = score; bestPerKg = perKg; best = c; bestQ = q; bestDays = days; bestNaval = naval; bestMixed = mixed && !naval; }
                    break;
                }
            }
            if (best == null)
            {
                why = !anySrc ? WNoSrc : !anyRoute ? WNoRoad : !anyWant ? WEnough : noGold ? WNoGold : anySmall ? WSmall : WNoGain;
                return false;
            }
            var shelf = best.St.ItemRoster;
            int at = shelf.FindIndexOfItem(item);
            if (at < 0) { why = WNoSrc; return false; }
            var el = shelf.GetElementCopyAtIndex(at).EquipmentElement;
            var srcTown = best.T;
            int got = 0, cost = 0, first, last;
            try
            {
                got = ShelfBuy.Take(shelf, el, bestQ, spare, () => Math.Max(1, srcTown.MarketData.GetPrice(el, null, false, null)), out cost, out first, out last, 0, false);
                if (got > 0 && cost > 0)
                {
                    town.ChangeGold(-cost);
                    srcTown.ChangeGold(cost);
                    try { TownFolk.OnTownCoin(dest, -cost, TownFolk.BOurs); } catch { }       // E5/0 (195): zakup kasy miasta - czesc ponizej zapasu kupcow (tylko licznik)
                    try { TownFolk.Sale(best.St, cost, TownFolk.MixGoods); } catch { }          // surowiec kupiony od kupcow zrodla (jak wozy dymarek F4a)
                }
            }
            finally
            {
                // sztuki zeszly z polki - woz musi istniec, cokolwiek stalo sie potem
                if (got > 0) MaterialCarts.Ship(best.St, dest, item, m, got, cost, bestDays, bestNaval, day, bestMixed ? Names[m] + " (ladem i morzem)" : Names[m]);
            }
            if (got <= 0) { why = WNoGold; return false; }
            if (bestMixed) { _dCartMixed++; _dCartMixedQty += got; }
            if (m == Ore)
            {
                int so; _srcOreCartSince.TryGetValue(best.St, out so); _srcOreCartSince[best.St] = so + got;
                _o2Cart++; _o2CartQty += got; _o2CartDays += bestDays; _o2CartCost += cost; _o2Profit += bestPerKg * got * kg / Math.Max(1, bestDays); _o2ProfitN++;   // 282: tylko log
            }
            return true;
        }

        /// <summary>174b.2 (krytyka 16): zuzycie dobowe miasta-CELU - wieksze z zmierzonego (srednia ok. 14 dob) i szacunku z OBECNYCH rak (CaravanBulk.UseNow);
        /// prog nadwyzki zrodla liczy dalej UseOf (dawne rece - zrodla kontraktow nie znikaja).</summary>
        private static float UseDest(Town town, int m)
        {
            float[] a; float meas = _useAvg.TryGetValue(town, out a) ? a[m] : 0f;
            float est = 0f;
            try { est = CaravanBulk.UseNow(town, _items[m]); } catch { }
            return Math.Max(meas, est);
        }

        /// <summary>174b.2: srednia dob drogi 3 ostatnich dostaw surowca m do miasta (domyslnie 4 - mediana 2.9 doby i srednia 3.55 z modelu drog B dla 29 miast).</summary>
        private static float TravelDays(Town town, int m)
        {
            float[] r;
            if (!_trip.TryGetValue(town, out r)) return 4f;
            float sum = 0f; int n = 0;
            for (int k = 0; k < 3; k++) { float v = r[m * 3 + k]; if (v > 0f) { sum += v; n++; } }
            return n > 0 ? sum / n : 4f;
        }

        private static void NoteTrip(Town town, int m, float days)
        {
            if (town == null || m < 0 || m >= M) return;
            float[] r;
            if (!_trip.TryGetValue(town, out r)) { r = new float[M * 3]; _trip[town] = r; }
            r[m * 3 + 2] = r[m * 3 + 1]; r[m * 3 + 1] = r[m * 3]; r[m * 3] = Math.Max(0.05f, days);
        }

        private static int SafeKeep(Town t, ItemObject it) { try { return CaravanBulk.KeepFor(t, it); } catch { return 0; } }

        /// <summary>Prog nadwyzki zrodla (ponizej niego miasto nie sprzedaje surowca na kontrakt): dawne rece - prog karawan i 10 dob zuzycia (jak dotad);
        /// 174b.2: nie ponizej wlasnego punktu zamowienia (D + 2 doby zuzycia z obecnych rak) - miasto nie sprzedaje tego, co zaraz samo by zamowilo.</summary>
        private static int SourceKeep(Town t, int m)
        {
            int keep = Math.Max(SafeKeep(t, _items[m]), (int)Math.Ceiling(10f * UseOf(t, m)));
            if (Settings.Current.TownMaterialOrderAhead) keep = Math.Max(keep, (int)Math.Ceiling((TravelDays(t, m) + 2f) * UseDest(t, m)));
            return keep;
        }

        /// <summary>Poprawka 174b (recenzja, wazna 1): SourceKeep zapamietany na jedno wywolanie Daily (czyszczony na poczatku Daily i w Reset) - zamowienia
        /// i linia nazw licza prog nadwyzki miasta najwyzej raz na surowiec (97 x 7), a nie przy kazdym zamowieniu dla kazdego zrodla w zasiegu.</summary>
        private static int SourceKeepDay(Town t, int m)
        {
            int[] a;
            if (!_keepDay.TryGetValue(t, out a)) { a = new int[M]; for (int k = 0; k < M; k++) a[k] = -1; _keepDay[t] = a; }
            if (a[m] < 0) a[m] = Math.Max(0, SourceKeep(t, m));
            return a[m];
        }

        /// <summary>F4a (test zbiorczy 5-8, R1-7): prog nadwyzki miasta-zrodla dla wozow dymarek (Bloomery.Daily) - ta sama regula co kontrakty 174
        /// ("co miasto sprzedaje na zewnatrz": prog karawan, 10 dob zuzycia, wlasny punkt zamowienia), z pamieci doby (SourceKeepDay; Bloomery.Daily biegnie
        /// przed MaterialOrders.Daily, ktore te pamiec czysci - kontrakty licza swoj prog od nowa jak dotad). Surowiec spoza tabeli albo blad -
        /// int.MaxValue (miasto nie ma nic na sprzedaz).</summary>
        internal static int SourceKeepFor(Town t, ItemObject item)
        {
            try
            {
                int m;
                if (t == null || item == null || Settings.Current == null || !Ready() || !_ix.TryGetValue(item, out m)) return int.MaxValue;
                return SourceKeepDay(t, m);
            }
            catch (Exception e) { Stumble("SourceKeepFor", e); return int.MaxValue; }
        }

        // ------------------------------------------------------------ plotno 14.10 (docs/pytania-2026-10-14/RAPORT-plotno.md, poprawki 1 i 2)
        private sealed class ExpDest { public Settlement St; public int Price, Spare; }
        // Towary 2 / K3: pamiec celow raz na dobe OSOBNO dla kazdego surowca (dotad jedna lista - przy tkaczu i garbarzu w tej samej dobie liczylaby cele od nowa
        // przy kazdym wywolaniu na przemian); dla samego plotna wynik bez zmian
        private static readonly int[] _expDay = NewExpDays();
        private static readonly List<ExpDest>[] _expDest = NewExpLists();
        private static int[] NewExpDays() { var a = new int[M]; for (int k = 0; k < M; k++) a[k] = int.MinValue; return a; }
        private static List<ExpDest>[] NewExpLists() { var a = new List<ExpDest>[M]; for (int k = 0; k < M; k++) a[k] = new List<ExpDest>(); return a; }

        /// <summary>
        /// Plotno 14.10 (poprawki 1 i 2): prog polki surowca z tabeli kontraktow, do ktorego miasto trzyma towar dla siebie i na wywoz - zbytki ludu
        /// (FolkClothAboveReserve) kupuja plotno i skore z oferty tylko ponad nim, a tkacz (WeaverExportPrice) tka na wywoz najwyzej do niego.
        /// = prog nadwyzki zrodla kontraktow (SourceKeepDay: prog karawan - 20 dni zuzycia rzemieslnikow, warsztatow i domow, 10 dob zuzycia zmierzonego,
        /// punkt zamowienia - czego miasto nie sprzedaje innym miastom) + pas wywozu tej samej wielkosci, nie mniejszy niz ladunek minimalny
        /// (TownMaterialOrderMinLoadKg - woz i karawana nie jada z mniejszym). Ponizej progu zrodla kupuja wojsko, warsztaty, domy (budzet gry), w pasie -
        /// kontrakty 174, wozy kupcow P3/Z1 i karawany (CaravanBulk), ponad - tez zbytki ludu. Przedmiot spoza tabeli - prog karawan (CaravanBulk.KeepFor);
        /// zamek i miasto bez danych - 0. Wyjatek - 0 (lud kupuje jak dotad; licznik potkniec).
        /// </summary>
        internal static int ShelfReserve(Town t, ItemObject item)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || t == null || item == null || !t.IsTown) return 0;
                int m;
                int sk = Ready() && _ix.TryGetValue(item, out m) ? SourceKeepDay(t, m) : SafeKeep(t, item);
                int load = (int)Math.Ceiling(Math.Max(0f, s.TownMaterialOrderMinLoadKg) / Kg(item));
                return Math.Max(0, sk) + Math.Max(Math.Max(0, sk), load);
            }
            catch (Exception e) { Stumble("ShelfReserve", e); return 0; }
        }

        /// <summary>
        /// Plotno 14.10 (poprawka 2, klucz WeaverExportPrice): ile warta jest sztuka wyrobu z miasta src na wywoz - najwyzsza cena po miastach, ktore przy dzisiejszej
        /// polce zamowilyby ten surowiec (zapas na max(10, D + 4) dob zuzycia celu minus polka minus w drodze > 0 - ta sama ilosc co Order / OrderByCart),
        /// niewrogich, nieoblezonych, w zasiegu kontraktu i wozu (TownMaterialOrderRange: droga ladem; bez niej ladem i morzem jak woz; rejs port-port x SeaFreightShare):
        /// cena srodkowej sztuki braku celu (PriceAt - ta sama co marza kontraktu) minus droga sztuki (CarterPencePerKgPer100 x kg x odleglosc / 100, morze x udzial);
        /// recenzja: tylko cel, ktorego kasa ponad zapas kupcow (TownPurse.BuyFloorPeek) starcza na ladunek minimalny po tej cenie netto (jak OrderByCart).
        /// Droga liczona tylko tu - koszt wsadu i pracy liczy tkacz, kontrakt i woz sprawdzaja swoja marze od ceny zrodla jak dotad. Cele z cenami raz na dobe
        /// (ExpDests), drogi par z pamieci kampanii (CartRoads, CartRoadMixed - te same co wozy). 0 = kontrakty wylaczone (TownMaterialOrders), brak celu albo blad.
        /// dests - celow w zasiegu.
        /// </summary>
        internal static int ExportNet(Town src, ItemObject item, out int dests)
        {
            dests = 0;
            try
            {
                var s = Settings.Current;
                int m;
                if (s == null || !On || src == null || item == null || !src.IsTown || !Ready() || !_ix.TryGetValue(item, out m)) return 0;
                var from = src.Settlement;
                if (from == null || from.IsUnderSiege) return 0;
                int day = (int)CampaignTime.Now.ToDays;
                if (day != _expDay[m]) ExpDests(m, day);
                float range = Math.Max(1f, s.TownMaterialOrderRange);
                float carter = Math.Max(0f, s.CarterPencePerKgPer100), sea = MBMath.ClampFloat(s.SeaFreightShare, 0f, 1f);
                float kg = Kg(item);
                int minPcs = Math.Max(1, (int)Math.Ceiling(Math.Max(0f, s.TownMaterialOrderMinLoadKg) / kg));   // ladunek minimalny w sztukach (jak OrderByCart)
                bool seaAll = s.TownMaterialOrderBySea;
                float seaMax = Math.Max(1f, s.TownMaterialOrderSeaMaxRoute);
                float seaLine = sea > 0f ? Math.Min(seaMax, range / sea) : seaMax;   // rejs >= linia prosta
                var pos = from.GetPosition2D;
                int best = 0;
                foreach (var e in _expDest[m])
                {
                    var dest = e.St;
                    if (dest == from || dest.IsUnderSiege) continue;
                    if (from.MapFaction != null && dest.MapFaction != null && FactionManager.IsAtWarAgainstFaction(from.MapFaction, dest.MapFaction)) continue;
                    float line = pos.Distance(dest.GetPosition2D);
                    bool landTry = line <= range, seaTry = seaAll && dest.HasPort && from.HasPort && line <= seaLine;
                    if (!landTry && !seaTry) continue;
                    float dL, dS;
                    CartRoads(from, dest, landTry, seaTry, out dL, out dS);
                    if (landTry && !(dL > 0f) && seaAll) dL = CartRoadMixed(from, dest);
                    bool landOk = dL > 0f && dL <= range, seaOk = dS > 0f && dS <= seaMax && dS * sea <= range;
                    if (!landOk && !seaOk) continue;
                    float d = seaOk && (!landOk || dS * sea < dL) ? dS * sea : dL;   // tansza droga, jak kontrakt i woz
                    int net = e.Price - (int)Math.Ceiling(carter * kg * d / 100f);
                    if (net <= 0 || (long)net * minPcs > e.Spare) continue;   // recenzja: kasa celu nie starcza na ladunek minimalny (jak OrderByCart: kasa ponad zapas kupcow)
                    dests++;
                    if (net > best) best = net;
                }
                return best;
            }
            catch (Exception e) { Stumble("ExportNet", e); return 0; }
        }

        /// <summary>ExportNet: miasta, ktore przy dzisiejszej polce zamowilyby surowiec m (ilosc jak w Order), z cena srodkowej sztuki braku - raz na dobe
        /// (polki celow w ciagu doby sie zmieniaja; tkacz ocenia z rana, jak cena zbytu karawan CaravanBulk).</summary>
        private static void ExpDests(int m, int day)
        {
            _expDay[m] = day; _expDest[m].Clear();
            var item = _items[m];
            if (item == null) return;
            foreach (var t in Town.AllTowns)
            {
                try
                {
                    var st = t != null ? t.Settlement : null;
                    if (st == null || !t.IsTown || t.Owner == null || t.Owner.ItemRoster == null) continue;
                    float use = UseDest(t, m);
                    if (use <= 0f) continue;
                    int want = (int)Math.Ceiling(Math.Max(10f, TravelDays(t, m) + 4f) * use) - t.Owner.ItemRoster.GetItemNumber(item) - InTransit(st, m);
                    if (want <= 0) continue;
                    int p = PriceAt(t, item, true, Math.Max(0, want - 1) / 2);
                    // recenzja: kasa celu ponad zapas kupcow (TownPurse.BuyFloor bez licznika 169c) - woz kupcow placi z niej zrodlu, karawana kontraktu dostaje z niej
                    // przy dostawie; cel bez kasy nie kupi i nie moze podnosic ceny wywozu tkacza (ExportNet sprawdza ladunek minimalny po cenie netto)
                    int spare = Math.Max(0, t.Gold - TownPurse.BuyFloorPeek(t, (int)Math.Max(0f, Settings.Current.TownRentFloorGold)));
                    if (p > 0 && spare > 0) _expDest[m].Add(new ExpDest { St = st, Price = p, Spare = spare });
                }
                catch (Exception e) { Stumble("ExpDests", e); }
            }
        }

        private static bool Eligible(MobileParty p, Settlement dest)
        {
            if (p == null || !p.IsCaravan || !p.IsActive || p.IsDisbanding || p.MapEvent != null || p.Army != null || !p.IsPartyTradeActive || p.ItemRoster == null || p.Party == null) return false;
            if (p.IsCurrentlyUsedByAQuest || p.Ai == null || p.Ai.DoNotMakeNewDecisions || _byCar.ContainsKey(p)) return false;
            if (p == MobileParty.MainParty) return false;
            // 174b.2 (krytyka 26, pytanie 2 do Jeffa): karawany rodu gracza (prowadzi je AI gry/BK, jak karawany AI) - jedna regula, zysk do ich kiesy;
            // wylacznik TownMaterialOrderPlayerCaravans = false - jak dotad (nigdy)
            if (!Settings.Current.TownMaterialOrderPlayerCaravans && (p.ActualClan == Clan.PlayerClan || (p.Party.Owner != null && p.Party.Owner == Hero.MainHero))) return false;
            // recenzja 174: karawana trzeciej frakcji w wojnie z zamawiajacym nie jedzie do wrogiego miasta
            if (dest != null && dest.MapFaction != null && p.MapFaction != null && FactionManager.IsAtWarAgainstFaction(p.MapFaction, dest.MapFaction)) return false;
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

        /// <summary>Kontrakt: zakup w miescie-zrodle (zwykly handel gry: zloto karawany -> kasa zrodla minus clo pana), wyjazd, rozkaz jazdy do celu;
        /// BK nie wybiera nowego celu do przyjazdu (prefiks HourlyTickParty), a bez tej latki - AI wstrzymane (wzor DTE).</summary>
        private static void Place(MobileParty car, Settlement src, Settlement dest, int m, int q, float dist, bool naval, float margin, int day, bool packs)
        {
            var item = _items[m];
            var srcRoster = src.ItemRoster; var pack = car.ItemRoster;
            int got = 0; long paid = 0;
            int packStart = Total(pack, item);
            if (packs) got = Math.Min(q, packStart);   // 174b.2: surowiec, ktory karawana juz wiezie - bez zakupu (prawdziwy towar, prawdziwa droga)
            for (int guard = 0; guard < 100 && got < q && !packs; guard++)
            {
                int at = srcRoster.FindIndexOfItem(item);
                if (at < 0) break;
                var el = srcRoster.GetElementCopyAtIndex(at);
                if (el.Amount <= 0) break;
                int price = Math.Max(1, src.Town.GetItemPrice(el.EquipmentElement, car, false));
                // recenzja 174: gra liczy cene sztuka po sztuce (polka zrodla maleje - cena rosnie), a GiveGoldAction obcina zaplate do kiesy -
                // przy kiesie na styk (mniej niz cena paczki + 25%) kupujemy po sztuce, inaczej ostatnie sztuki szlyby czesciowo bez zaplaty
                int n = Math.Min(Math.Min(10, q - got), el.Amount);
                if ((long)(price + price / 4 + 1) * n > car.PartyTradeGold) n = car.PartyTradeGold >= price ? 1 : 0;
                if (n <= 0) break;
                int had = pack.GetItemNumber(item), purse = car.PartyTradeGold, packAll = Total(pack, item), shelfAll = Total(srcRoster, item);
                try { SellItemsAction.Apply(src.Town.Owner, car.Party, el, n, src); }
                catch (Exception e) { Stumble("Place(zakup)", e); break; }
                int moved = Total(pack, item) - packAll;   // 174b.2: wszystkie stosy
                if (moved != shelfAll - Total(srcRoster, item)) { _dAudit++; _auditAll++; }   // 174b.2 audyt (krytyka 20): przyrost jukow == ubytek polki zrodla (wszystkie stosy)
                if (moved <= 0) break;
                got += moved; paid += Math.Max(0, purse - car.PartyTradeGold);
            }
            if (got <= 0) { _dNoGain++; return; }
            var c = new Contract { Car = car, CarId = car.StringId, Dest = dest, Src = src, Mat = m, Qty = got, Day = day, Paid = (int)paid, Dist = dist, Naval = naval, Packs = packs, PackStart = packs ? packStart : -1,
                                   Start = CampaignTime.Now.ToDays };
            try { if (car.CurrentSettlement != null) LeaveSettlementAction.ApplyForParty(car); } catch (Exception e) { Stumble("Place(wyjazd)", e); }
            ShipForget(car);   // 174b.1: stary stan "hop-by-hop" BK Shipping sprzed kontraktu nie prowadzi karawany do dawnego celu
            _contracts.Add(c); _byCar[car] = c;
            if (!Move(car, dest, naval)) { Release(c); _dRejected++; return; }   // rozkaz odrzucony (straznik drog) - karawana handluje dalej sama, z ladunkiem
            _dMade[m]++; _dMadeQty[m] += got; _dGold += paid; _dMargin += (long)margin;
            if (naval) { _dSea++; _dSeaQty += got; }
            if (packs) { _dPacks++; _dPacksQty += got; }
            if (m == Ore && !packs) { int so; _srcOreSince.TryGetValue(src, out so); _srcOreSince[src] = so + got; }   // 174b.0: linia nazw (tylko licznik)
            if (_dEx.Count < 3) _dEx.Add(dest.Name + " - " + src.Name + " " + (int)dist + (naval ? " (morzem)" : "") + (packs ? " (z jukow)" : "") + " " + Names[m] + " " + got);
        }

        /// <summary>Rozkaz jazdy przez wejscie metody (z prefiksem BK i jego straznikiem, jak kazdy rozkaz - IslandRoads); true = cel przyjety.</summary>
        private static bool Move(MobileParty car, Settlement dest, bool naval)
        {
            try
            {
                SetHold(car);
                var nav = naval ? MobileParty.NavigationType.Naval : MobileParty.NavigationType.Default;   // 174b.2: konwoj (statki bez ladu) - jak BK ReleaseCaravanFromHold
                bool port = naval && dest.HasPort;
                if (_setMove == null) _setMove = typeof(MobileParty).GetMethod("SetMoveGoToSettlement", new[] { typeof(Settlement), typeof(MobileParty.NavigationType), typeof(bool) });
                EnsureFast();
                if (_setMoveFast != null && FastCall.On) { FastCall.Fast++; _setMoveFast(car, dest, nav, port); }   // E8/O1 (265): to samo wejscie metody (prefiks BK ze straznikiem), wyjatek bez opakowania
                else if (_setMove != null)
                {
                    FastCall.Slow++;
                    try { _setMove.Invoke(car, new object[] { dest, nav, port }); }
                    catch (TargetInvocationException e) { throw e.InnerException ?? e; }
                }
                else car.SetMoveGoToSettlement(dest, nav, port);
                car.RecalculateShortTermBehavior();
                return car.TargetSettlement == dest;
            }
            catch (Exception e) { Stumble("Move", e); return false; }
        }

        /// <summary>Recenzja 174: przy wpietym prefiksie HourlyTickParty karawana z kontraktem ma AI gry czynne (ucieczka przed bandami i wrogami - przy
        /// DoNotMakeNewDecisions gra pomija GetBestInitiativeBehavior, MobilePartyAi.cs:486); bez latki - wzor posilkow DTE (AI wstrzymane).</summary>
        private static void SetHold(MobileParty car)
        {
            if (car == null || car.Ai == null) return;
            bool freeze = !HourlyHooked;
            if (car.Ai.DoNotMakeNewDecisions != freeze) car.Ai.SetDoNotMakeNewDecisions(freeze);
        }

        private static void Drop(Contract c)
        {
            _contracts.Remove(c);
            // 174b.1 (krytyka 6): swit nie odda celu utraconego kontraktu. Poprawka 174b (recenzja): zywa karawana zwolniona w nocnym obozie nie budzi sie na
            // postoju (bez rozkazu czekalaby na tick BK, ktory dlawik BKROT puszcza raz na dobe) - zamiast celu kontraktu swit wola decyzje BK
            // (WakeAtDawn -> ReleaseCaravanFromHold: stary cel, jesli nie jest oblegany ani wrogi, inaczej nowy - jak kazda karawana BK po postoju)
            if (c.Car != null) { _byCar.Remove(c.Car); NightRest.ForgetOrder(c.Car, c.Car.IsActive); }
        }

        /// <summary>Poprawka 174b (recenzja): pobudka o swicie (albo przy nocnym alarmie) karawany, ktorej kontrakt zwolniono w nocnym obozie - decyzja BK
        /// ReleaseCaravanFromHold (ten sam wybor celu, ktory BK robi kazdej karawanie na postoju); bez BK - AI gry przy najblizszym ticku (Rethink).</summary>
        internal static void WakeAtDawn(MobileParty mp)
        {
            try
            {
                if (mp == null || !mp.IsActive || mp.Ai == null || mp.MapEvent != null || mp.CurrentSettlement != null || _byCar.ContainsKey(mp)) return;
                if (_bkRelease != null && _bkGetBeh != null && Campaign.Current != null)
                {
                    EnsureFast();
                    bool fast = _bkGetBehFast != null && _bkReleaseFast != null && FastCall.On;   // E8/O1 (265): obie metody delegatem albo obie przez Invoke
                    if (fast) FastCall.Fast++; else FastCall.Slow++;
                    var beh = fast ? _bkGetBehFast(Campaign.Current) : _bkGetBeh.Invoke(Campaign.Current, null);
                    if (beh != null)
                    {
                        if (fast) { FastCall.Fast++; _bkReleaseFast(beh, mp); } else { FastCall.Slow++; _bkRelease.Invoke(beh, new object[] { mp }); }
                        _dWokeBk++; return;
                    }
                }
                mp.Ai.RethinkAtNextHourlyTick = true;
            }
            catch (Exception e) { Stumble("WakeAtDawn", e); }
        }

        /// <summary>174b.1 (krytyka 1): BK InvalidateRedirectCache na instancji BKShippingBehavior TEJ kampanii (bez zapamietanej instancji - po wczytaniu
        /// innego zapisu w tej samej sesji stara instancja trzymalaby w pamieci cala poprzednia kampanie).</summary>
        private static void ShipForget(MobileParty car)
        {
            try
            {
                if (car == null || _shipInvalidate == null || _shipGetBeh == null || Campaign.Current == null) return;
                EnsureFast();
                bool fast = _shipGetBehFast != null && _shipInvalidateFast != null && FastCall.On;   // E8/O1 (265): obie metody delegatem albo obie przez Invoke
                if (fast) FastCall.Fast++; else FastCall.Slow++;
                var beh = fast ? _shipGetBehFast(Campaign.Current) : _shipGetBeh.Invoke(Campaign.Current, null);
                if (beh != null)
                {
                    if (fast) { FastCall.Fast++; _shipInvalidateFast(beh, car); } else { FastCall.Slow++; _shipInvalidate.Invoke(beh, new object[] { car }); }
                }
            }
            catch (Exception e) { Stumble("ShipForget", e); }
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
            int sold = 0, carried = c.Qty; long got = 0;
            try
            {
                int reserve = (int)Math.Max(0f, Settings.Current.TownRentFloorGold);
                var pack = mp.ItemRoster;
                // recenzja 174: ladunku moglo ubyc po drodze (BK SellGoods i CaravanBulk przy wjezdzie do innego miasta, lup z karawany, ktora przezyla) -
                // to osobna pozycja linii, nie "brak kasy miasta"
                carried = Math.Min(c.Qty, Math.Max(0, Total(pack, item)));   // 174b.2: wszystkie stosy (z jukow moze byc kilka)
                if (carried < c.Qty) _dShortPack += c.Qty - carried;
                for (int guard = 0; guard < 100 && sold < carried && town != null; guard++)
                {
                    int at = pack.FindIndexOfItem(item);
                    if (at < 0) break;
                    var el = pack.GetElementCopyAtIndex(at);
                    if (el.Amount <= 0) break;
                    int spare = town.Gold - TownPurse.BuyFloor(town, reserve);   // E5/A1: miasto tylko z nadwyzki ponad zapas kupcow (TownPaysFromSurplus)
                    int price = Math.Max(1, town.GetItemPrice(el.EquipmentElement, mp, true));
                    int n = Math.Min(Math.Min(10, carried - sold), Math.Min(el.Amount, spare / price));
                    if (n <= 0) break;                                                    // miasto bez kasy ponad rezerwe - reszta zostaje karawanie
                    int had = el.Amount, purse = mp.PartyTradeGold, packAll = Total(pack, item), shelfAll = Total(town.Owner.ItemRoster, item);
                    TownToll.ExemptBegin();                                      // E3-G3: dostawa kontraktu 174 bez myta targowego (marza liczona bez myta, Cost174)
                    CaravanTrade284.Path = CaravanTrade284.PathContract;         // 284: droga sprzedazy w ksiedze zysku
                    try { SellItemsAction.Apply(mp.Party, town.Owner, el, n, st); }
                    catch (Exception e) { Stumble("Deliver(sprzedaz)", e); break; }
                    finally { TownToll.ExemptEnd(); CaravanTrade284.Path = 0; }
                    int moved = packAll - Total(pack, item);   // 174b.2: wszystkie stosy (oprozniony stos zmienia kolejnosc w rosterze)
                    if (moved != Total(town.Owner.ItemRoster, item) - shelfAll) { _dAudit++; _auditAll++; }   // 174b.2 audyt: ubytek jukow == przyrost polki celu (wszystkie stosy)
                    if (moved <= 0) break;
                    sold += moved; got += Math.Max(0, mp.PartyTradeGold - purse);
                }
            }
            catch (Exception e) { Stumble("Deliver", e); }
            _dDone++; _dDays += Math.Max(0.0, CampaignTime.Now.ToDays - c.Day); _dPaidDest += got;
            if (c.PackStart >= 0 && sold > c.PackStart) { _dAudit++; _auditAll++; }   // 174b.2 audyt: z jukow dostarczono nie wiecej, niz bylo w jukach przy zawarciu
            if (c.Naval) _dDoneSea++;
            if (c.Packs) _dDonePacks++;
            if (c.Mat == Ore && sold > 0 && town != null) { int dq; _dstOreSince.TryGetValue(town, out dq); _dstOreSince[town] = dq + sold; _o2CarDone += sold; }   // 282: + licznik doby
            // poprawka 174b (recenzja): dni drogi od chwili zawarcia (Start), nie od doby ucietej do int - bez fazy taktu doby (w sklad6 ok. +0.2 doby);
            // srednia "dob drogi" w linii dnia liczy dalej od c.Day (porownywalna z sklad6)
            NoteTrip(town, c.Mat, (float)(CampaignTime.Now.ToDays - (c.Start >= 0.0 ? c.Start : c.Day)));
            if (sold < carried) _dKeptTarget += carried - sold;
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

        /// <summary>Prefiks BK BKCaravansBehavior.ReleaseCaravanFromHold (wczytanie gry - OnGameLoaded, PRZED OnSessionLaunched; koniec oblezenia; Hold w ticku BK):
        /// karawana z kontraktem jedzie dalej do celu. 174b.1: (krytyka 2) najpierw kontrakty z zapisu - inaczej po wczytaniu _byCar jest pusty i BK dawal
        /// wlasny cel karawanom, ktore zapisano w nocnym obozie albo po bitwie; spiaca w obozie - nic (swit odda cel); (krytyka 9) licznik wedlug tej samej
        /// reguly co Hourly: cel brak albo Hold = "ruszona z postoju", inne miasto = "cel przywrocony" (po MaxRetarget - zwolnienie).</summary>
        public static bool ReleasePrefix(MobileParty __0)
        {
            try
            {
                if (_pending != null) ResolvePending("wczytanie: BK ReleaseCaravanFromHold");
                Contract c;
                if (__0 == null || _byCar.Count == 0 || !_byCar.TryGetValue(__0, out c)) return true;
                if (DestLost(__0, c)) { Release(c); return true; }   // cel oblegany albo wrogi - BK wybiera cel sam
                if (NightRest.IsCamping(__0)) return false;          // spi - swit odda cel
                bool idle = __0.TargetSettlement == null || __0.DefaultBehavior == AiBehavior.Hold;
                bool other = !idle && __0.TargetSettlement != c.Dest;
                if (other && c.Retarget >= MaxRetarget) { Release(c); _dRelRetarget++; return true; }
                if (Move(__0, c.Dest, c.Naval)) { if (idle) _dHoldMoved++; else if (other) { _dRetarget++; c.Retarget++; } return false; }
            }
            catch (Exception e) { Stumble("ReleasePrefix", e); }
            return true;
        }

        /// <summary>174b.1 (krytyka 3): prefiks BK BKShippingBehavior.RouteCaravanHopByHop - KAZDA karawana z kontraktem (takze gdy BK wskazal nasz cel: BK
        /// prowadzi wtedy przez wezel posredni i zapisuje stan hop-by-hop) - false bez zmiany celu; stary stan BK kasowany od razu (InvalidateRedirectCache na
        /// instancji, ktora wola), wiec AdvanceHopByHopWaypoints juz nie wraca. Nasz rozkaz i tak sprawdza droge: lad - straznik BK (Default); morze - rozkaz
        /// NavigationType.Naval + port (Move), jak BK ReleaseCaravanFromHold: konwoj nie ma ladu, wiec rozkaz All/Default bylby dla niego zly (174b.2; projekt 3.2
        /// pkt 2 mowi jeszcze "All" - sprostowanie w CHANGELOG). NIE zmieniac na All.</summary>
        public static bool RouteHopPrefix(object __instance, MobileParty __0, ref bool __result)
        {
            try
            {
                if (__0 == null || _byCar.Count == 0 || !_byCar.ContainsKey(__0)) return true;
                _dShipBlocked++;
                try
                {
                    if (_shipInvalidate != null && __instance != null)
                    {
                        EnsureFast();
                        if (_shipInvalidateFast != null && FastCall.On) { FastCall.Fast++; _shipInvalidateFast(__instance, __0); }   // E8/O1 (265)
                        else { FastCall.Slow++; _shipInvalidate.Invoke(__instance, new object[] { __0 }); }
                    }
                }
                catch (Exception e) { Stumble("RouteHopPrefix(stan BK)", e); }
                __result = false;
                return false;
            }
            catch (Exception e) { Stumble("RouteHopPrefix", e); return true; }
        }

        /// <summary>Cel oblegany albo wrogi karawanie (licznik zwolnien przy okazji).</summary>
        private static bool DestLost(MobileParty car, Contract c)
        {
            if (c.Dest.IsUnderSiege) { _dRelSiege++; return true; }
            if (c.Dest.MapFaction != null && car.MapFaction != null && FactionManager.IsAtWarAgainstFaction(car.MapFaction, c.Dest.MapFaction)) { _dRelHost++; return true; }
            return false;
        }

        /// <summary>Recenzja 174: prefiks BKCaravansBehavior.HourlyTickParty (i gry CaravansCampaignBehavior.HourlyTickParty bez BK) - karawana z kontraktem
        /// nie dostaje od BK nowego celu ani zakupow; AI gry zostaje czynne (ucieczka). 174b.1: TYLKO blokada decyzji BK - ruch do celu i liczniki robi
        /// Hourly co godzine (prefiks za dlawikiem BKROT biegl raz na dobe); cel oblegany albo wrogi - zwolnienie, BK rusza w tej samej godzinie.</summary>
        public static bool HourlyPrefix(MobileParty __0)
        {
            try
            {
                if (_pending != null) ResolvePending("wczytanie: BK HourlyTickParty");
                Contract c;
                if (__0 == null || _byCar.Count == 0 || !_byCar.TryGetValue(__0, out c)) return true;
                if (!__0.IsActive || __0.IsDisbanding) return true;
                if (DestLost(__0, c)) { Release(c); return true; }
                return false;
            }
            catch (Exception e) { Stumble("HourlyPrefix", e); return true; }
        }

        // ------------------------------------------------------------ 282: linia "Ruda przeplyw (282)" (tylko log; czyta tools/sprawdz_logi.py --grupa ruda282)
        private static double Median(List<int> v)
        {
            if (v.Count == 0) return -1;
            v.Sort();
            int n = v.Count;
            return (n & 1) == 1 ? v[n / 2] : (v[n / 2 - 1] + v[n / 2]) / 2.0;
        }

        /// <summary>282: zamowienia rudy tej doby (kolejnosc, zawarte wozem i karawana, odmowy), zrodla rudy wedlug dawnego progu i punktu zamowienia, ceny (pierwsza
        /// sztuka w miescie bez rudy wobec ceny zrodla), miasta bez rudy z zuzyciem i bez, stan karawan (CaravanBulk.Seg282) i zelazo swiata (OreLedger). Jedno przejscie
        /// 97 miast; liczy przed Line (NewDay zeruje liczniki). null = brak przedmiotu rudy.</summary>
        private static string Line282(int day)
        {
            var s = Settings.Current;
            var ore = _items != null ? _items[Ore] : null;
            if (s == null || ore == null) return null;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            var inv = CultureInfo.InvariantCulture;
            int towns = 0, empty = 0, emptyUse = 0, s0 = 0, s1 = 0, p1 = 0;
            var pay = new List<int>(); var sell = new List<int>();
            foreach (var t in Town.AllTowns)
            {
                try
                {
                    if (t == null || !t.IsTown || t.Owner == null || t.Owner.ItemRoster == null || t.Settlement == null) continue;
                    towns++;
                    int have = t.Owner.ItemRoster.GetItemNumber(ore);
                    var a = OreDay(t);
                    if (have <= 0)
                    {
                        empty++;
                        if (a[1] > 0f) emptyUse++;
                        int p = PriceAt(t, ore, true, 0);
                        if (p > 0) pay.Add(p);
                        continue;
                    }
                    if (have > SourceKeepDay(t, Ore)) s0++;
                    int f = have - (int)a[0];
                    if (f > 0)
                    {
                        s1++; p1 += f;
                        int p = PriceAt(t, ore, false, 0);
                        if (p > 0) sell.Add(p);
                    }
                }
                catch (Exception e) { Stumble("Line282(miasto)", e); }
            }
            double x = Median(pay), y = Median(sell);
            int made = _o2Cart + _dMade[Ore], qty = _o2CartQty + _dMadeQty[Ore];
            var sb = new StringBuilder(1600);
            sb.Append("Ruda przeplyw (282): dzien ").Append(day)
              .Append(" | klucze: karawany trzymaja tyle, ile sprzedaja ").Append(s.CaravanBulkFlowGate ? "TAK" : "NIE")
              .Append(", najpierw miasta z najmniejszym zapasem ").Append(s.TownMaterialOrderNeediestFirst ? "TAK" : "NIE")
              .Append(", zrodla rudy do punktu zamowienia ").Append(s.TownMaterialOrderOreEqualCover ? "TAK" : "NIE")
              .Append(", f5 z towarem w drodze ").Append(s.KsIronHandsCountTransit ? "TAK" : "NIE")
              .Append(" | zamowienia rudy: proby ").Append(_o2Try).Append(" (kolejnosc wedlug dni zapasu: [")
              .Append(s.TownMaterialOrderNeediestFirst ? (_o2First.Count > 0 ? string.Join(", ", _o2First.ToArray()) : "-") : "dowolna - klucz NIE").Append("])")
              .Append(", zawarte wozem ").Append(_o2Cart).Append(" / karawana ").Append(_dMade[Ore]).Append(" (sztuk ").Append(qty)
              .Append(", srednio ").Append(made > 0 ? ((double)(_o2CartDays + _o2CarDays) / made).ToString("0.0", inv) : "-").Append(" dob drogi, zysk na dobe drogi srednio ")
              .Append(_o2ProfitN > 0 ? (_o2Profit / _o2ProfitN).ToString("0", inv) : "-").Append(" zl), dowiezione wozem ").Append(_o2CartDone).Append(" / karawana ").Append(_o2CarDone)
              .Append(", odmowy [brak zrodla ").Append(_dWhy[Ore, WNoSrc]).Append(", bez drogi ").Append(_dWhy[Ore, WNoRoad]).Append(", kasa miasta ").Append(_dWhy[Ore, WNoGold])
              .Append(", bez zysku ").Append(_dWhy[Ore, WNoGain]).Append(", ladunek za maly ").Append(_dWhy[Ore, WSmall]).Append(", brak karawany ").Append(_dWhy[Ore, WCar])
              .Append(", zapas dosc ").Append(_o2Enough + _dWhy[Ore, WEnough]).Append(", przerwa ").Append(_o2Pause).Append(']')
              .Append(" | zrodla rudy: prog dawny ").Append(s0).Append(", punkt zamowienia ").Append(s1).Append(" (sztuk ponad ").Append(p1).Append("), przyciete rownym zapasem dni ").Append(_o2Capped)
              .Append(" | ceny: miasta bez rudy placa za pierwsza sztuke ").Append(x >= 0 ? x.ToString("0.0", inv) : "-").Append(" d (mediana ").Append(pay.Count).Append(" miast)")
              .Append(", zrodla sprzedaja ").Append(y >= 0 ? y.ToString("0.0", inv) : "-").Append(" d (mediana ").Append(sell.Count).Append(" zrodel)")
              .Append(", wozy zaplacily srednio ").Append(_o2CartQty > 0 ? ((double)_o2CartCost / _o2CartQty).ToString("0.0", inv) : "-").Append(" d")
              .Append(", stosunek ").Append(x > 0 && y > 0 ? (x / y).ToString("0.00", inv) : "-")
              .Append(" | miasta bez rudy ").Append(empty).Append(" z ").Append(towns).Append(" (z zuzyciem ").Append(emptyUse).Append(", bez zuzycia ").Append(empty - emptyUse).Append(')')
              .Append(" | ").Append(CaravanBulk.Seg282() ?? "karawany ruda: brak (regula CaravanBulk nieczynna)")
              .Append(" | swiat lupy: ").Append(OreLedger.IronWorld282());
            double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            sb.Append(" | czas ").Append(ms.ToString("0.0", inv)).Append(" ms, potkniecia ").Append(_stumbles).Append('.');
            return sb.ToString();
        }

        // ------------------------------------------------------------ linia dnia
        private static void Line(int day)
        {
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                if (!On && _contracts.Count == 0 && _dDone + _dLost + _dRelOther == 0 && !MaterialCarts.Busy) return;
                var sb = new StringBuilder();
                int made = 0; foreach (var n in _dMade) made += n;
                int rel = _dRelHost + _dRelSiege + _dRel30 + _dRelOther + _dRejected + _dRelRetarget + _dRelDisband + _dRelForcedHold;
                sb.Append("Kontrakty surowca (174): dzien ").Append(day).Append(On ? "" : " (WYLACZONE - tylko zwolnienia)").Append(" - zawarto ").Append(made).Append(" [");
                for (int m = 0; m < M; m++) { if (m > 0) sb.Append(", "); sb.Append(Names[m]).Append(' ').Append(_dMadeQty[m]); }
                sb.Append(" sztuk] za ").Append(_dGold).Append(" d towaru (kasa miast-zrodel + clo panow; oczekiwana marza karawan ").Append(_dMargin).Append(" d); dojechalo ").Append(_dDone)
                  .Append(" (srednio ").Append(_dDone > 0 ? (_dDays / _dDone).ToString("0.0", CultureInfo.InvariantCulture) : "-").Append(" dob drogi; miasta zaplacily ").Append(_dPaidDest)
                  .Append(" d; zostalo karawanom z braku kasy miasta ").Append(_dKeptTarget).Append(" szt.; ladunku brak w jukach przy dostawie ").Append(_dShortPack)
                  .Append(" szt. - sprzedany albo zlupiony po drodze), w drodze ").Append(_contracts.Count).Append(", rozbite/pojmane ").Append(_dLost)
                  .Append(" (sztuk ").Append(_dLostQty).Append("), zwolnione ").Append(rel).Append(" (wrogosc ").Append(_dRelHost).Append(", oblezenie celu ").Append(_dRelSiege)
                  .Append(", cel zmieniany przez innych ").Append(_dRelRetarget).Append(", rozwiazana ").Append(_dRelDisband).Append(", 30 dob ").Append(_dRel30)
                  .Append(", rozkaz odrzucony ").Append(_dRejected).Append(", inne ").Append(_dRelOther).Append("); cel przywrocony ").Append(_dRetarget).Append(", ruszona z postoju ").Append(_dHoldMoved)
                  .Append(", w obozie przy przegladzie doby ").Append(_dCampSeen)
                  .Append("; 174b.1: godzin kontraktow w nocnym obozie ").Append(_dCampHours).Append(", BK Shipping zablokowany ").Append(_dShipBlocked)
                  .Append(ShipHooked ? "" : " (BRAK latki)").Append(", zwolnione: postoj wymuszany ").Append(_dRelForcedHold)
                  .Append(", inny cel [").Append(_dRetEx.Count > 0 ? string.Join("; ", _dRetEx.ToArray()) : "-").Append("]")
                  .Append("; 174b.2: morzem zawarto ").Append(_dSea).Append(" (sztuk ").Append(_dSeaQty).Append(", dojechalo ").Append(_dDoneSea).Append("), z jukow zawarto ").Append(_dPacks)
                  .Append(" (sztuk ").Append(_dPacksQty).Append(", dojechalo ").Append(_dDonePacks).Append("), z wyprzedzeniem (punkt zamowienia) ").Append(_dAhead)
                  .Append(", bez drogi [wyspa lub inna czesc ladu bez portu ").Append(_dNoRoadBy[0]).Append(", droga ladem > zasieg ").Append(_dNoRoadBy[1])
                  .Append(", morze poza zasiegiem albo dlugoscia ").Append(_dNoRoadBy[2]).Append(", brak konwoju w porcie zrodla ").Append(_dNoRoadBy[3])
                  .Append("], audyt ilosci: rozjazdy ").Append(_dAudit).Append(" (od wczytania ").Append(_auditAll).Append(")")
                  .Append("; poprawka 174b: zwolnione w nocnym obozie i obudzone o swicie decyzja BK ").Append(_dWokeBk)
                  .Append(HourlyHooked ? " (AI gry czynne - ucieczka jak kazda karawana)" : " (AI wstrzymane - wzor DTE, bez latki HourlyTickParty)")
                  .Append("; bez kontraktu: brak karawany w zrodle ").Append(_dNoCar).Append(", brak zrodla w zasiegu ").Append(_dNoSrc).Append(", bez drogi ").Append(_dNoRoad)
                  .Append(", przewoznik bez miejsca w jukach lub kiesy ").Append(_dNoRoom)
                  .Append(", ladunek ponizej ").Append(Math.Max(0f, s.TownMaterialOrderMinLoadKg).ToString("0", CultureInfo.InvariantCulture)).Append(" kg ").Append(_dSmall)
                  .Append(", bez zysku ").Append(_dNoGain).Append(", zapas i dostawy dosc ").Append(_dEnough).Append(", przerwa ").Append(_dPause)
                  .Append(" [").Append(_dEx.Count > 0 ? string.Join("; ", _dEx.ToArray()) : "-").Append("]");
                // P2c (TRIAZ-3 O2-1 P2): powody "bez kontraktu" osobno dla kazdego surowca - po obu przebiegach (karawana, potem woz kupcow)
                sb.Append("; bez kontraktu wedlug surowca (P3): ");
                bool anyWhy = false;
                for (int m = 0; m < M; m++)
                {
                    int tot = 0; for (int k = 0; k < WKinds; k++) tot += _dWhy[m, k];
                    if (tot == 0) continue;
                    sb.Append(anyWhy ? "; " : "").Append(Names[m]).Append(' ').Append(tot).Append(" (");
                    bool f1 = true;
                    for (int k = 0; k < WKinds; k++) if (_dWhy[m, k] > 0) { sb.Append(f1 ? "" : ", ").Append(WName[k]).Append(' ').Append(_dWhy[m, k]); f1 = false; }
                    sb.Append(')'); anyWhy = true;
                }
                if (!anyWhy) sb.Append('-');
                // P3/Z1: wozy kupcow - zakupy, dostawy, w drodze, kontrola reszty; koszt drugiego przebiegu i dostaw
                MaterialCarts.Segment(sb, On && s.TownMaterialOrderByCart, Names, _items != null ? _items[Ore] : null);
                sb.Append(", drugi przebieg zamowien (woz) ").Append(_dCartAsk).Append(", w tym ladem i morzem ").Append(_dCartMixed).Append(" (sztuk ").Append(_dCartMixedQty)
                  .Append("; para bez drogi ladowej i bez rejsu port-port), czas wozow ")
                  .Append((_dCartTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency).ToString("0.0", CultureInfo.InvariantCulture)).Append(" ms");
                if (_items != null)   // recenzja 174: przed pierwszym Ready() (rudy jeszcze nie ma w MBObjectManager) bez NRE
                {
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
                    sb.Append("; miast bez rudy ").Append(noOre).Append(" z ").Append(towns).Append(", bez lnu ").Append(noFlax).Append(", bez skor surowych ").Append(noHides);
                    if (_dOreEmptyBefore >= 0) sb.Append("; przed dostawami wozow tej doby miast bez rudy bylo ").Append(_dOreEmptyBefore);   // P3/Z1
                }
                sb.Append("; potkniecia ").Append(_stumbles).Append(" (od wczytania ").Append(_stumblesAll).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Line", e); }
            finally { NewDay(); }
        }

        // ------------------------------------------------------------ zapis: karawana|cel|zrodlo|surowiec|ilosc|doba|zaplacone|odleglosc|morzem|juki,przywrocen,postoj,start~
        // Poprawka 174b (recenzja): 10. pole (stan jukow przy zawarciu - -1 = kontrakt z zakupu, licznik przywrocen celu, godziny postoju wymuszanego, chwila
        // zawarcia) - po wczytaniu kontrakt "z jukow" liczy sie jak w biegu (linia, audyt), a limity przywrocen i postoju nie startuja od zera. Odczyt przyjmuje
        // 9 albo 10 pol (zapis sprzed poprawki); starszy DLL na rekordzie 10-polowym zwalnia karawane (FreeBroken / pominiety) - ladunek zostaje karawanie.
        internal static string Export()
        {
            var sb = new StringBuilder();
            try { if (_pending != null) ResolvePending("zapis przed startem sesji"); }
            catch (Exception e) { Stumble("Export(ResolvePending)", e); }
            foreach (var c in _contracts)
            {
                try   // recenzja 174: wyjatek jednego rekordu nie gubi reszty
                {
                    if (c.Car == null || !c.Car.IsActive || string.IsNullOrEmpty(c.Car.StringId) || c.Dest == null) continue;
                    var one = new StringBuilder();
                    one.Append(c.Car.StringId).Append('|').Append(c.Dest.StringId).Append('|').Append(c.Src != null ? c.Src.StringId : "").Append('|').Append(Ids[c.Mat]).Append('|')
                       .Append(c.Qty).Append('|').Append(c.Day).Append('|').Append(c.Paid).Append('|').Append(c.Dist.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(c.Naval ? 1 : 0)
                       .Append('|').Append(c.Packs ? c.PackStart : -1).Append(',').Append(c.Retarget).Append(',').Append(c.HoldStreak).Append(',')
                       .Append((c.Start >= 0.0 ? c.Start : (double)c.Day).ToString("R", CultureInfo.InvariantCulture)).Append('~');
                    sb.Append(one);
                }
                catch (Exception e) { Stumble("Export", e); }
            }
            MaterialCarts.Export(sb, Ids);   // P3/Z1: wozy kupcow z surowcem - rekordy "@w|..." w tym samym kluczu (starszy DLL je pomija)
            return sb.ToString();
        }

        internal static void Import(string s)
        {
            _contracts.Clear(); _byCar.Clear(); MaterialCarts.Clear();
            _pending = string.IsNullOrEmpty(s) ? null : s;
        }

        /// <summary>Recenzja 174: wczytanie klucza arm_matorders rzucilo wyjatek - kontraktow nie ma. Przy wpietym prefiksie HourlyTickParty karawany
        /// nie maja wstrzymanego AI, wiec BK prowadzi je dalej jak kazda; bez latki (wzor DTE) moga stac - linia to mowi.</summary>
        internal static void ImportFailed()
        {
            _contracts.Clear(); _byCar.Clear(); _pending = null; MaterialCarts.Clear();
            Log.Info("Kontrakty surowca (174): klucz zapisu arm_matorders NIECZYTELNY - kontrakty z zapisu pominiete" + (HourlyHooked
                     ? " (karawany handluja dalej same - AI nie bylo wstrzymane)." : " (UWAGA: bez latki HourlyTickParty karawany kontraktowe z zapisu moga stac - AI wstrzymane wzorem DTE)."));
        }

        /// <summary>Recenzja 174: karawana z rekordu, ktorego nie da sie odtworzyc, nie moze zostac z wstrzymanym AI (gra zapisuje DoNotMakeNewDecisions).</summary>
        private static bool FreeBroken(MobileParty car)
        {
            try
            {
                if (car == null || !car.IsActive || car.Ai == null || !car.Ai.DoNotMakeNewDecisions || car.IsCurrentlyUsedByAQuest) return false;
                car.Ai.SetDoNotMakeNewDecisions(false); car.Ai.RethinkAtNextHourlyTick = true;
                return true;
            }
            catch (Exception e) { Stumble("FreeBroken", e); return false; }
        }

        /// <summary>OnSessionLaunched: kontrakty z zapisu na karawany (po StringId); bez karawany - pominiete; rekord nieczytelny przy zywej karawanie -
        /// karawana zwolniona (recenzja 174); wylaczone w MCM - karawana zwolniona.</summary>
        internal static void ResolvePending(string why)
        {
            if (string.IsNullOrEmpty(_pending)) { _pending = null; return; }
            if (!Ready()) return;   // 174b.1: przedmioty jeszcze niegotowe (wczesne wywolanie z prefiksu BK) - napis czeka na nastepna okazje
            var s = _pending;
            _pending = null;
            int ok = 0, gone = 0, freed = 0, broken = 0, cartsOk = 0, cartsBad = 0;
            var byId = new Dictionary<string, MobileParty>();
            foreach (var p in MobileParty.AllCaravanParties) if (p != null && !string.IsNullOrEmpty(p.StringId)) byId[p.StringId] = p;
            var om = MBObjectManager.Instance;
            foreach (var rec in s.Split('~'))
            {
                if (rec.Length == 0) continue;
                if (MaterialCarts.IsRecord(rec)) { if (MaterialCarts.ImportRecord(rec, Ids, _items)) cartsOk++; else cartsBad++; continue; }   // P3/Z1: woz kupcow z surowcem
                MobileParty car = null;
                try
                {
                    var a = rec.Split('|');
                    byId.TryGetValue(a[0], out car);   // najpierw karawana - zly rekord tez zwalnia zywa karawane
                    Settlement dest = null, src = null;
                    int m = -1;
                    bool shape = a.Length == 9 || a.Length == 10;   // poprawka 174b: 10. pole opcjonalne
                    if (shape)
                    {
                        try { dest = om.GetObject<Settlement>(a[1]); } catch { }
                        if (a[2].Length > 0) { try { src = om.GetObject<Settlement>(a[2]); } catch { } }
                        m = Array.IndexOf(Ids, a[3]);
                    }
                    if (car == null || !car.IsActive) { gone++; continue; }
                    if (!shape || dest == null || m < 0) { if (FreeBroken(car)) broken++; else gone++; continue; }
                    int q, d0, paid; float dist;
                    int.TryParse(a[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out q);
                    int.TryParse(a[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out d0);
                    int.TryParse(a[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out paid);
                    float.TryParse(a[7], NumberStyles.Float, CultureInfo.InvariantCulture, out dist);
                    var c = new Contract { Car = car, CarId = a[0], Dest = dest, Src = src, Mat = m, Qty = q, Day = d0, Paid = paid, Dist = dist, Naval = a[8] == "1" };
                    if (a.Length == 10)   // poprawka 174b: juki przy zawarciu, przywrocenia celu, postoj wymuszany, chwila zawarcia; nieczytelne pole = jak rekord 9-polowy
                    {
                        var x = a[9].Split(',');
                        int ps, rt, hs; double st;
                        if (x.Length == 4 && int.TryParse(x[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out ps) && int.TryParse(x[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out rt)
                            && int.TryParse(x[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out hs) && double.TryParse(x[3], NumberStyles.Float, CultureInfo.InvariantCulture, out st))
                        {
                            c.Packs = ps >= 0; c.PackStart = ps >= 0 ? ps : -1; c.Retarget = Math.Max(0, rt); c.HoldStreak = Math.Max(0, hs);
                            if (!double.IsNaN(st) && !double.IsInfinity(st) && st >= 0.0) c.Start = st;
                        }
                    }
                    _contracts.Add(c); _byCar[car] = c;
                    if (!On) { Release(c); freed++; continue; }
                    SetHold(car);   // przy wpietym prefiksie HourlyTickParty AI czynne (takze kontrakt z zapisu starszej wersji 174), bez latki - wzor DTE
                    ok++;
                }
                catch (Exception e)
                {
                    Stumble("ResolvePending", e);
                    if (car != null && !_byCar.ContainsKey(car) && FreeBroken(car)) broken++; else gone++;
                }
            }
            Log.Info("Kontrakty surowca (174): z zapisu (" + why + ") " + ok + " kontraktow w drodze; pominiete (karawany albo celu juz nie ma) " + gone
                     + ", karawany zwolnione - rekord nieczytelny " + broken + ", zwolnione (wylaczone w MCM) " + freed
                     + "; wozy kupcow z surowcem (P3/Z1) w drodze " + cartsOk + " (" + MaterialCarts.InTransitPieces() + " sztuk), rekordy odrzucone " + cartsBad + ".");
        }

        // ------------------------------------------------------------ wpiecie
        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var t = QuartermasterLaw.FindType("BannerKings.Behaviours.BKCaravansBehavior");
                var m = t != null ? AccessTools.Method(t, "ReleaseCaravanFromHold", new[] { typeof(MobileParty) }) : null;
                if (m != null) { h.Patch(m, prefix: new HarmonyMethod(typeof(MaterialOrders), nameof(ReleasePrefix))); ReleaseHooked = true; }
                // poprawka 174b (recenzja): pobudka o swicie karawany zwolnionej w nocnym obozie - ta sama metoda BK, instancja przy kazdym wywolaniu
                try
                {
                    _bkRelease = m;
                    var gb = AccessTools.Method(typeof(Campaign), "GetCampaignBehavior");
                    if (t != null && gb != null && gb.IsGenericMethodDefinition) _bkGetBeh = gb.MakeGenericMethod(t);
                    NightRest.WakeHandler = WakeAtDawn;
                }
                catch (Exception e) { Log.Error("MaterialOrders.ApplyAll(pobudka BK)", e); }
                // recenzja 174: decyzje karawany z kontraktem - zamiast DoNotMakeNewDecisions (gra pomija wtedy ucieczke) prefiks godzinnego ticku BK i gry
                // z BK decyduje tick BK (tick gry wylacza latka BK CaravansCampaignBehavior_HourlyTickParty_Skip), bez BK - tick gry
                try
                {
                    var mh = t != null ? AccessTools.Method(t, "HourlyTickParty", new[] { typeof(MobileParty) })
                                       : AccessTools.Method(typeof(TaleWorlds.CampaignSystem.CampaignBehaviors.CaravansCampaignBehavior), "HourlyTickParty", new[] { typeof(MobileParty) });
                    if (mh != null) { h.Patch(mh, prefix: new HarmonyMethod(typeof(MaterialOrders), nameof(HourlyPrefix))); HourlyHooked = true; }
                }
                catch (Exception e) { Log.Error("MaterialOrders.ApplyAll(HourlyTickParty)", e); }
                // 174b.1 (krytyka 3): BK Shipping prowadzi karawane "hop-by-hop" po wjezdzie do obcego portu i ze starego stanu - prefiks blokuje to karawanom
                // z kontraktem; InvalidateRedirectCache i GetCampaignBehavior<T> zapamietane jako metody, instancja brana przy kazdym wywolaniu (krytyka 1)
                var ts = QuartermasterLaw.FindType("BannerKings.Behaviours.Shipping.BKShippingBehavior");
                try
                {
                    if (ts != null)
                    {
                        var mr = AccessTools.Method(ts, "RouteCaravanHopByHop", new[] { typeof(MobileParty), typeof(Settlement) });
                        if (mr != null && mr.ReturnType == typeof(bool)) { h.Patch(mr, prefix: new HarmonyMethod(typeof(MaterialOrders), nameof(RouteHopPrefix))); ShipHooked = true; }
                        _shipInvalidate = AccessTools.Method(ts, "InvalidateRedirectCache", new[] { typeof(MobileParty) });
                        var g = AccessTools.Method(typeof(Campaign), "GetCampaignBehavior");
                        if (g != null && g.IsGenericMethodDefinition) _shipGetBeh = g.MakeGenericMethod(ts);
                    }
                }
                catch (Exception e) { Log.Error("MaterialOrders.ApplyAll(BK Shipping)", e); }
                Log.Info("MaterialOrders (174.2): kontrakty surowca dla prawdziwych karawan " + (On ? "CZYNNE" : "wylaczone w MCM") + "; BK ReleaseCaravanFromHold (cel po oblezeniu) "
                         + (ReleaseHooked ? "wpiety - karawana z kontraktem jedzie dalej do celu" : (t == null ? "bez BK - nic do wpiecia" : "BRAK metody"))
                         + "; HourlyTickParty karawan (" + (t != null ? "BK" : "gra") + ") " + (HourlyHooked ? "wpiety - karawana z kontraktem bez nowego celu, AI gry czynne (ucieczka)"
                         : "BRAK - AI karawany z kontraktem wstrzymane wzorem DTE (nie ucieka)")
                         + "; 174b.1: BK Shipping RouteCaravanHopByHop " + (ShipHooked ? "wpiety" : (ts == null ? "bez BK Shipping - nic do wpiecia" : "BRAK"))
                         + ", InvalidateRedirectCache " + (_shipInvalidate != null && _shipGetBeh != null ? "znaleziony" : (ts == null ? "bez BK Shipping" : "BRAK"))
                         + "; cel kontraktu pilnowany co godzine (MaterialOrders.Hourly po nocnym obozie); karawana zwolniona w obozie budzona o swicie "
                         + (_bkRelease != null && _bkGetBeh != null ? "decyzja BK (ReleaseCaravanFromHold)" : "przez AI gry (bez BK)") + ".");
            }
            catch (Exception e) { Log.Error("MaterialOrders.ApplyAll", e); }
        }
    }
}
