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
        }
        private const int MaxRetarget = 2;   // recenzja 174: cel zmieniony przez innych (BK Shipping, porty) - po 2 przywroceniach zwolnienie, bez ping-pongu
        // 174b.1 (krytyka 15): karawana z kontraktem stawiana na postoj co godzine przez cudzy kod - po tylu kolejnych godzinach ruszania z postoju
        // zwolnienie "postoj wymuszany" (jedna linia z nazwa). Postoj po bitwie albo po oblezeniu to 1 godzina; nocny oboz nie liczy sie wcale.
        private const int MaxHoldStreak = 6;
        internal static bool ShipHooked;     // 174b.1: prefiks BK BKShippingBehavior.RouteCaravanHopByHop wpiety
        private static MethodInfo _shipInvalidate, _shipGetBeh;   // BK InvalidateRedirectCache i Campaign.GetCampaignBehavior<BKShippingBehavior> (instancja przy kazdym wywolaniu)
        internal static bool HourlyHooked;   // prefiks BK/gry HourlyTickParty wpiety - karawana z kontraktem bez DoNotMakeNewDecisions (ucieka jak kazda)
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
        private static int _dRelSiege, _dRelRetarget, _dRelDisband, _dShortPack, _dSmall, _dHoldMoved;   // recenzja 174
        private static int _dCampHours, _dShipBlocked, _dRelForcedHold;   // 174b.1: godziny kontraktow w obozie, BK Shipping zablokowany, zwolnienia "postoj wymuszany"
        // 174b.2: bez drogi wedlug powodu (wyspa / inna czesc ladu bez portu, droga ladem > zasieg, morze poza zasiegiem albo dlugoscia, brak konwoju w porcie zrodla),
        // kontrakty morzem i z jukow (zawarte, dojechaly), z wyprzedzeniem (punkt zamowienia), audyt ilosci (krytyka 20)
        private static readonly int[] _dNoRoadBy = new int[4];
        private static int _dSea, _dSeaQty, _dPacks, _dPacksQty, _dAhead, _dDoneSea, _dDonePacks, _dAudit, _auditAll;
        private static readonly Dictionary<Town, float[]> _trip = new Dictionary<Town, float[]>();   // dni drogi 3 ostatnich dostaw (miasto x surowiec)
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

        internal static bool On { get { var s = Settings.Current; return s != null && s.TownMaterialOrders; } }

        internal static void Reset()
        {
            _items = null; _ix.Clear(); _contracts.Clear(); _byCar.Clear(); _miss.Clear(); _useToday.Clear(); _useAvg.Clear(); _last.Clear(); _pending = null;
            _missOreSince.Clear(); _srcOreSince.Clear(); _dstOreSince.Clear(); _namesSince = -1; _trip.Clear(); _auditAll = 0;
            NewDay(); _stumblesAll = 0; _errSites.Clear();
        }

        private static void NewDay()
        {
            Array.Clear(_dMade, 0, M); Array.Clear(_dMadeQty, 0, M);
            _dDone = _dLost = _dLostQty = _dRelHost = _dRel30 = _dRelOther = _dNoCar = _dNoSrc = _dNoGain = _dNoRoad = _dRejected = _dPause = _dEnough = _dRetarget = _dKeptTarget = _stumbles = 0;
            _dRelSiege = _dRelRetarget = _dRelDisband = _dShortPack = _dSmall = _dHoldMoved = 0; _dCampSeen = 0;
            _dCampHours = _dShipBlocked = _dRelForcedHold = 0; _dRetEx.Clear();
            Array.Clear(_dNoRoadBy, 0, 4); _dSea = _dSeaQty = _dPacks = _dPacksQty = _dAhead = _dDoneSea = _dDonePacks = _dAudit = 0;
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
            Line(day);
            try { if (_items != null && (_namesSince < 0 || day % 5 == 0)) NamesLine(day); } catch (Exception e) { Stumble("NamesLine", e); }
        }

        private static void OrderTimed(Town town, int m, int day, bool ahead)
        {
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
                    if (have <= 0) noOre.Add(nm);
                    if (a0) noArrows.Add(nm);
                    if (have <= 0 && a0) both.Add(nm);
                    if (have > 0) stock.Add(new KeyValuePair<string, int>(nm, have));
                    int mo; if (_missOreSince.TryGetValue(t, out mo) && mo > 0) miss.Add(new KeyValuePair<string, int>(nm, mo));
                    int dq; if (_dstOreSince.TryGetValue(t, out dq) && dq > 0) got.Add(new KeyValuePair<string, int>(nm, dq));
                    int keep = SourceKeep(t, Ore);
                    int surplus = have - keep;
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
                     + (sp.Count > 0 ? string.Join("; ", sp.ToArray()) : "-") + ".");
            _missOreSince.Clear(); _srcOreSince.Clear(); _dstOreSince.Clear(); _namesSince = day;
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
            if (day - last[m] < Math.Max(1, s.TownMaterialOrderDays)) { _dPause++; return; }
            var item = _items[m];
            var dest = town.Settlement;
            if (dest == null || dest.IsUnderSiege || town.Owner == null || town.Owner.ItemRoster == null) return;
            float use = UseDest(town, m), D = TravelDays(town, m);
            int want = (int)Math.Ceiling(Math.Max(10f, D + 4f) * use) - town.Owner.ItemRoster.GetItemNumber(item) - InTransit(dest, m);
            if (want <= 0) { _dEnough++; return; }
            float range = Math.Max(1f, m == Ore ? s.TownMaterialOrderRangeOre : s.TownMaterialOrderRange);
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
            bool anySrc = false, anyRoute = false, anyCar = false, anySmall = false, landNoCar = false;
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
                int have = src.ItemRoster.GetItemNumber(item);
                int surplus = have > 0 ? have - SourceKeep(t, m) : 0;
                // przewoznicy stojacy w zrodle: lad / morze x zakup (najwiecej wolnego miejsca) / z jukow (najwiecej tego surowca w jukach)
                MobileParty cL = null, cS = null, pL = null, pS = null; float fL = 0f, fS = 0f; int nL = 0, nS = 0;
                foreach (var p in src.Parties)
                {
                    if (!Eligible(p, dest)) continue;
                    bool land = p.HasLandNavigationCapability, ship = p.HasNavalNavigationCapability;
                    if (surplus > 0)
                    {
                        float free = p.InventoryCapacity - p.TotalWeightCarried;   // konwoj w porcie: ladownia (IsCurrentlyAtSea), krytyka 8
                        if (land && free > fL) { fL = free; cL = p; }
                        if (ship && free > fS) { fS = free; cS = p; }
                    }
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
                        if (free < kg) continue;
                        pSrc = PriceAt(t, item, false, 0);
                        if (pSrc <= 0) continue;
                        q = Math.Min(Math.Min(want, surplus), (int)(free / kg));
                        q = Math.Min(q, car.PartyTradeGold / Math.Max(1, pSrc));
                    }
                    if (q <= 0) continue;
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
                        if (perKg > bestPerKg) { bestPerKg = perKg; bestSrc = src; bestCar = car; bestQ = q; bestDist = d; bestNaval = naval; bestMargin = margin; bestPacks = packs; }
                        break;
                    }
                }
            }
            if (bestSrc == null)
            {
                if (!anySrc) _dNoSrc++;
                else if (!anyRoute) { _dNoRoad++; if (why >= 1 && why <= 4) _dNoRoadBy[why - 1]++; }
                else if (!anyCar) { if (landNoCar) _dNoCar++; else { _dNoRoad++; _dNoRoadBy[3]++; } }   // trasa tylko morska, a w porcie zrodla nie ma konwoju
                else if (anySmall) _dSmall++;
                else _dNoGain++;
                return;
            }
            last[m] = day;
            if (ahead) _dAhead++;
            Place(bestCar, bestSrc, dest, m, bestQ, bestDist, bestNaval, bestMargin, day, bestPacks);
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
            var c = new Contract { Car = car, CarId = car.StringId, Dest = dest, Src = src, Mat = m, Qty = got, Day = day, Paid = (int)paid, Dist = dist, Naval = naval, Packs = packs, PackStart = packs ? packStart : -1 };
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
            if (c.Car != null) { _byCar.Remove(c.Car); NightRest.ForgetOrder(c.Car); }   // 174b.1 (krytyka 6): swit nie odda celu utraconego kontraktu
        }

        /// <summary>174b.1 (krytyka 1): BK InvalidateRedirectCache na instancji BKShippingBehavior TEJ kampanii (bez zapamietanej instancji - po wczytaniu
        /// innego zapisu w tej samej sesji stara instancja trzymalaby w pamieci cala poprzednia kampanie).</summary>
        private static void ShipForget(MobileParty car)
        {
            try
            {
                if (car == null || _shipInvalidate == null || _shipGetBeh == null || Campaign.Current == null) return;
                var beh = _shipGetBeh.Invoke(Campaign.Current, null);
                if (beh != null) _shipInvalidate.Invoke(beh, new object[] { car });
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
                    int spare = town.Gold - reserve;
                    int price = Math.Max(1, town.GetItemPrice(el.EquipmentElement, mp, true));
                    int n = Math.Min(Math.Min(10, carried - sold), Math.Min(el.Amount, spare / price));
                    if (n <= 0) break;                                                    // miasto bez kasy ponad rezerwe - reszta zostaje karawanie
                    int had = el.Amount, purse = mp.PartyTradeGold, packAll = Total(pack, item), shelfAll = Total(town.Owner.ItemRoster, item);
                    try { SellItemsAction.Apply(mp.Party, town.Owner, el, n, st); }
                    catch (Exception e) { Stumble("Deliver(sprzedaz)", e); break; }
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
            if (c.Mat == Ore && sold > 0 && town != null) { int dq; _dstOreSince.TryGetValue(town, out dq); _dstOreSince[town] = dq + sold; }
            NoteTrip(town, c.Mat, (float)(CampaignTime.Now.ToDays - c.Day));
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
        /// instancji, ktora wola), wiec AdvanceHopByHopWaypoints juz nie wraca. Nasz rozkaz i tak sprawdza droge (straznik BK dla ladu, morze - All).</summary>
        public static bool RouteHopPrefix(object __instance, MobileParty __0, ref bool __result)
        {
            try
            {
                if (__0 == null || _byCar.Count == 0 || !_byCar.ContainsKey(__0)) return true;
                _dShipBlocked++;
                try { if (_shipInvalidate != null && __instance != null) _shipInvalidate.Invoke(__instance, new object[] { __0 }); } catch (Exception e) { Stumble("RouteHopPrefix(stan BK)", e); }
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
                  .Append(HourlyHooked ? " (AI gry czynne - ucieczka jak kazda karawana)" : " (AI wstrzymane - wzor DTE, bez latki HourlyTickParty)")
                  .Append("; bez kontraktu: brak karawany w zrodle ").Append(_dNoCar).Append(", brak zrodla w zasiegu ").Append(_dNoSrc).Append(", bez drogi ").Append(_dNoRoad)
                  .Append(", ladunek ponizej ").Append(Math.Max(0f, s.TownMaterialOrderMinLoadKg).ToString("0", CultureInfo.InvariantCulture)).Append(" kg ").Append(_dSmall)
                  .Append(", bez zysku ").Append(_dNoGain).Append(", zapas i dostawy dosc ").Append(_dEnough).Append(", przerwa ").Append(_dPause)
                  .Append(" [").Append(_dEx.Count > 0 ? string.Join("; ", _dEx.ToArray()) : "-").Append("]");
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
                }
                sb.Append("; potkniecia ").Append(_stumbles).Append(" (od wczytania ").Append(_stumblesAll).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Line", e); }
            finally { NewDay(); }
        }

        // ------------------------------------------------------------ zapis: karawana|cel|zrodlo|surowiec|ilosc|doba|zaplacone|odleglosc|morzem~
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
                       .Append(c.Qty).Append('|').Append(c.Day).Append('|').Append(c.Paid).Append('|').Append(c.Dist.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(c.Naval ? 1 : 0).Append('~');
                    sb.Append(one);
                }
                catch (Exception e) { Stumble("Export", e); }
            }
            return sb.ToString();
        }

        internal static void Import(string s)
        {
            _contracts.Clear(); _byCar.Clear();
            _pending = string.IsNullOrEmpty(s) ? null : s;
        }

        /// <summary>Recenzja 174: wczytanie klucza arm_matorders rzucilo wyjatek - kontraktow nie ma. Przy wpietym prefiksie HourlyTickParty karawany
        /// nie maja wstrzymanego AI, wiec BK prowadzi je dalej jak kazda; bez latki (wzor DTE) moga stac - linia to mowi.</summary>
        internal static void ImportFailed()
        {
            _contracts.Clear(); _byCar.Clear(); _pending = null;
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
            int ok = 0, gone = 0, freed = 0, broken = 0;
            var byId = new Dictionary<string, MobileParty>();
            foreach (var p in MobileParty.AllCaravanParties) if (p != null && !string.IsNullOrEmpty(p.StringId)) byId[p.StringId] = p;
            var om = MBObjectManager.Instance;
            foreach (var rec in s.Split('~'))
            {
                if (rec.Length == 0) continue;
                MobileParty car = null;
                try
                {
                    var a = rec.Split('|');
                    byId.TryGetValue(a[0], out car);   // najpierw karawana - zly rekord tez zwalnia zywa karawane
                    Settlement dest = null, src = null;
                    int m = -1;
                    if (a.Length == 9)
                    {
                        try { dest = om.GetObject<Settlement>(a[1]); } catch { }
                        if (a[2].Length > 0) { try { src = om.GetObject<Settlement>(a[2]); } catch { } }
                        m = Array.IndexOf(Ids, a[3]);
                    }
                    if (car == null || !car.IsActive) { gone++; continue; }
                    if (a.Length != 9 || dest == null || m < 0) { if (FreeBroken(car)) broken++; else gone++; continue; }
                    int q, d0, paid; float dist;
                    int.TryParse(a[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out q);
                    int.TryParse(a[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out d0);
                    int.TryParse(a[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out paid);
                    float.TryParse(a[7], NumberStyles.Float, CultureInfo.InvariantCulture, out dist);
                    var c = new Contract { Car = car, CarId = a[0], Dest = dest, Src = src, Mat = m, Qty = q, Day = d0, Paid = paid, Dist = dist, Naval = a[8] == "1" };
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
                     + ", karawany zwolnione - rekord nieczytelny " + broken + ", zwolnione (wylaczone w MCM) " + freed + ".");
        }

        // ------------------------------------------------------------ wpiecie
        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var t = QuartermasterLaw.FindType("BannerKings.Behaviours.BKCaravansBehavior");
                var m = t != null ? AccessTools.Method(t, "ReleaseCaravanFromHold", new[] { typeof(MobileParty) }) : null;
                if (m != null) { h.Patch(m, prefix: new HarmonyMethod(typeof(MaterialOrders), nameof(ReleasePrefix))); ReleaseHooked = true; }
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
                         + "; cel kontraktu pilnowany co godzine (MaterialOrders.Hourly po nocnym obozie).");
            }
            catch (Exception e) { Log.Error("MaterialOrders.ApplyAll", e); }
        }
    }
}
