using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// E6/R1 (214, PLAN 6.1, R1/R2; projekt etapu 6 v2 rozdz. 3 "E6/R1"): WYTOP PRZY KOPALNI. Tylko nowa kampania (flaga w zapisie
    /// arm_e6_bloom, wzor CamelLaw) i klucz BloomeryAtMine; stary zapis - ruda 100 kg jak dotad (stara ruda stalaby sie lupami).
    ///  - Sztuka "iron" to lupa "Iron Bloom": metal z ladunku rudy (100 kg x WorkshopCrudeKgPerOre / 10 = 15 kg surowki), cena = surowka
    ///    + wegiel dymarki za kg (HistoricalPrices.Apply krok 0 i 1). Liczba sztuk z kopalni bez zmian: wszystko, co LICZY sztuki
    ///    (wydobycie MaterialLaw.ProdPostfix, kopalnie BK WorkshopLaw.BulkLineSkip, place cyklu WorkshopTrade.BulkFactor, zapas startowy
    ///    StartStock, przelicznik popytu), bierze HistoricalPrices.CountScale - lupa liczy sie jak ladunek rudy, nie wedlug wagi 15 kg
    ///    (pulapka K13:227-229: BulkScale dalby 6.7 x zelaza). Receptury (WorkshopLaw.Needs, MendMaterial) licza metal przez CountKg.
    ///  - Dymarka pali PRAWDZIWE drewno (v2, KRYTYKA K1-01): WoodPerBloom ladunkow (WorkshopWoodPerOre x 100 kg rudy / ladunek drewna = 5)
    ///    na lupe. Skad: (1) las wsi okregu kopalni (VillageWoodlot wsi z tym samym TradeBound) - zdjety z wozu na targ, zanim pojedzie
    ///    (FromWoodlot; dymarka trzyma najwyzej 2 doby potrzeby); (2) reszta - woz wsi gorniczej kupuje drewno z polki miasta handlowego
    ///    za zloto wsi po cenie targu, raz na dobe (Daily), na NASTEPNA dobe (R15 - zadnego przerzutu w tej samej chwili); (3) brak drewna -
    ///    w ticku produkcji wsi (TickGoodProduction) model daje najwyzej tyle lup, na ile starczy drewna w dymarce (Cap - sufit LimitMax
    ///    w ProdPostfix), reszta rudy niewydobyta ("kopalnia bez wegla", nic z niczego). Po ticku: drewno spalone = WoodPerBloom x lupy
    ///    dopisane wsi (roznica magazynu przed i po ticku) - tozsamosc z linii.
    ///  - Kuznia miasta pali juz tylko wegiel kowalski (WorkshopLaw.Needs bez czlonu dymarki, MendMaterial bez drewna dymarki); kopalnia BK
    ///    w miescie/zamku (linia bez wsadu) wytapia z drewnem z polki swojego miasta (TownMineBlocked). Kuznia gracza: 1 lupa -> sztaby
    ///    surowki wedlug masy (MaterialLaw.RefinePostfix). Zlom (ArmsScrap, Drill) wraca w lupach sam - WorkshopLaw.Needs liczy w lupach.
    ///  - Stan w zapisie: flaga, drewno w dymarce i potrzeba doby kazdej kopalni (ladunki). Drewno dymarki jest poza rosterami: wchodzi
    ///    z lasu okregu (nie wchodzi do zapasu) albo z polki miasta (ujscie zapasu - ksiega drewna "dymarki przy kopalniach", ksiega towarow
    ///    ramka FBloom) i wychodzi tylko w ogniu dymarki przy lupie.
    /// F4a (test zbiorczy 5-8, bieg 120 dob: R1-7 kopalnia bez wegla 36%, R1-8 miasta bez zelaza 45): WOZ KOPALNI PO DREWNO Z DALSZYCH MIAST. Las okregu
    /// dawal 44% potrzeby, polka wlasnego miasta handlowego 16% - drewno lezalo w miastach bez kopalni. Daily: (a) przyjazdy wozow, (b) zakup z wlasnej
    /// polki jak dotad, (c) zamowienie wozem. Ile: od NIEDOBORU doby (potrzeba - srednia 7 dob lasu okregu i zakupu z wlasnej polki; las, ktorego pelna
    /// dymarka nie przyjela, liczy sie jak przyjety - woz nie wypiera darmowego lasu) x (doby drogi + 1) minus wozy, ktore dojada do tego dnia; nie wiecej,
    /// niz zmiesci dymarka w dniu przyjazdu (2 x potrzeba - doba lasu - przewidywany stan; recenzja: liczone do ostatniego wozu w drodze, a po wozie
    /// kupionym do sufitu z dalszego, tanszego zrodla blizsze zrodla karmia ogien do jego przyjazdu). Skad: miasta w zasiegu BloomeryCartRange (droga), niewrogie
    /// frakcji wsi, nieoblezone, tylko nadwyzka ponad prog kontraktow 174 (MaterialOrders.SourceKeepFor), najtansze, potem najblizsze. Cena: kazdy ladunek
    /// po swojej (ShelfBuy.Take), budzet = zloto wsi, bez kredytu; zloto wies -> kasa miasta-zrodla. Droga: ladem do wsi, wyspa - ladem i morzem do portu
    /// miasta handlowego kopalni + doba (TradeCarts.Road / DaysFor, predkosc wozu wsi). PRZEWOZ BEZ OPLATY - kosztem jest czas drogi, jak wozy wsi
    /// i TradeCarts (fracht kontraktow 174 nie dotyczy). Wies nie-Normal - woz czeka, po 30 dobach zawraca (drewno na polke zrodla z ujemnym wpisem
    /// ksiag, zwrot zlota z kasy zrodla najwyzej do jej stanu). Drewno w drodze jest poza rosterami jak drewno w dymarce (ujscie zapasu w chwili zakupu).
    /// Zapis: po rekordach kopalni znacznik "@w" i rekordy wozow (zrodlo:kopalnia:dobaZakupu:dobaPrzyjazdu:ile:zaplacone); brak sekcji = brak wozow.
    /// R1-7b: ShelfUse - srednia ok. 14 dob drewna kupionego przez dymarki z polki miasta handlowego kopalni (krok b i kopalnia BK w miescie) - popyt
    /// drewna miasta w CaravanBulk (cel zapasu, prog nadwyzki, cena) przy kluczu BloomeryWoodDemand; nie w zapisie (liczy sie od pierwszej pelnej doby).
    /// F4b (test zbiorczy 5-8, R1-12 P3; klucz BloomeryCoalBeforeLordShare): RACHUNEK ZA DREWNO MA PIERWSZENSTWO W UTARGU. Pan bral dzialke (45%) takze
    /// od tej czesci ceny lupy, ktora wies wydala na drewno - kopalnia na drewnie z targu nie zarabiala nic i "brak zlota wsi" rosl do 166 ladunkow/d.
    /// Mine.Bill = zloto wsi wydane na drewno (wlasna polka i woz), jeszcze nie odliczone od utargu; rosnie przy zaplacie, maleje o zwrot zawroconego wozu,
    /// nie wiekszy niz zakupy drewna z ostatnich 14 dob (BillDays - woz, ktory nie wraca, nie odklada rachunku bez konca). Rozliczenie robi
    /// VillageTakings.EnterPostfix przy powrocie wozu do wsi macierzystej (BillOf / BillSettled): pan dostaje podatek od (utarg - rachunek), roznica
    /// przechodzi z licznika podatku wsi do jej kiesy - zloto nie powstaje ani nie znika. Zapis: sekcja "@b" (wies:rachunek) po wozach; rekord kopalni
    /// zostaje 3-polowy (starsza wersja pominie sekcje, dymarek nie straci). Pomiar w linii: wstawka "rachunek za drewno (F4b)" z kontrola rachunku.
    /// Koszt: ticki O(1) na wies, zakup raz na dobe po kopalniach (ok. 26), okregi z cache przebudowywanym raz na dobe; wozy - kopalnie z niedoborem x
    /// miasta z nadwyzka w zasiegu raz na dobe (drogi z pamieci), czas w linii.
    /// Wyjatki liczone per miejsce (pierwsze 3 do logu) - bez globalnych wylacznikow (CLAUDE.md 7).
    /// </summary>
    internal static class Bloomery
    {
        private static readonly TextObject _txtNoCoal = new TextObject("{=!}Armoury: bloomery short of wood");

        internal static bool NewCampaign;
        private static ItemObject _iron, _wood;
        private static bool _applied, _wired;
        private static float _scale = 1f;
        private static int _wpb = -1;

        private const int Window = 7;          // F4a: doby sredniej "drewno na miejscu" (las okregu + wlasna polka)
        private const int BillDays = 14;       // F4b [S]: rachunek za drewno najwyzej tyle, ile wies wydala na drewno w ostatnich 14 dobach
        private const string BillMark = "@b";
        private sealed class Mine
        {
            public int Bank, Want;
            // F4b: rachunek za drewno - zloto wsi wydane na drewno, jeszcze nie odliczone od utargu przed dzialka pana (w zapisie, sekcja "@b").
            // Nie w zapisie: zakupy doby i ich pierscien 14 dob (sufit rachunku), pomiar doby do linii
            public int Bill, BuyDay, BuyAt, BuyN;
            public readonly int[] Buys = new int[BillDays];
            public int Asked, Got, NoGold, Price;   // doba: ladunki chciane z wlasnej polki, kupione (polka + woz), nie kupione z braku zlota, cena drewna w miescie handlowym
            /// <summary>F4b: koniec doby zakupow - probka pierscienia; rachunek ponad zakupy ostatnich 14 dob wygasa (dopiero przy pelnym pierscieniu: po
            /// wczytaniu jest pusty, rachunek z zapisu zyje wiec co najmniej 14 dob). Klucz wylaczony - rachunku nie prowadzimy. Zwraca, ile wygaslo.</summary>
            public int RollBill(bool on)
            {
                Buys[BuyAt] = BuyDay; BuyAt = (BuyAt + 1) % BillDays; if (BuyN < BillDays) BuyN++; BuyDay = 0;
                int cut = 0;
                if (!on) { cut = Bill; Bill = 0; return cut; }
                if (BuyN < BillDays) return 0;
                long cap = 0; for (int i = 0; i < BillDays; i++) cap += Buys[i];
                if (Bill > cap) { cut = Bill - (int)cap; Bill = (int)cap; }
                return cut;
            }
            // F4a (nie w zapisie - od pierwszej pelnej doby po wczytaniu): drewno na miejscu od ostatniego Daily i z ostatnich 7 dob
            public float LocDay, ForDay;       // las okregu (przyjety + odeslany przez pelna dymarke) + zakup z wlasnej polki; sam las
            public readonly float[] Loc = new float[Window], For = new float[Window];
            public int LocN, LocAt;
            public float Unwon;                // lupy niewytopione tej doby (linia per kopalnia)
            public float LocAvg { get { if (LocN <= 0) return 0f; float a = 0f; for (int i = 0; i < LocN; i++) a += Loc[i]; return a / LocN; } }
            public float ForAvg { get { if (LocN <= 0) return 0f; float a = 0f; for (int i = 0; i < LocN; i++) a += For[i]; return a / LocN; } }
            public void Push() { Loc[LocAt] = LocDay; For[LocAt] = ForDay; LocAt = (LocAt + 1) % Window; if (LocN < Window) LocN++; LocDay = 0f; ForDay = 0f; }
        }
        private static readonly Dictionary<Village, Mine> _mines = new Dictionary<Village, Mine>();

        // F4a: wozy kopaln z drewnem z dalszych miast (w drodze - poza rosterami, jak drewno w dymarce)
        private const int CartMaxDays = 30;
        private const string CartMark = "@w";
        private sealed class Cart { public Settlement Src; public Village Mine; public int DayBought, DayDue, N, Paid; }
        private sealed class Road { public Settlement Mk; public float Dist; public bool Port; }
        private struct Source { public Town Town; public Settlement St; public int Keep; }
        private struct Cand { public Town Town; public Settlement St; public int Keep, Price, Days; public float Dist; }
        private static readonly List<Cart> _carts = new List<Cart>();
        private static readonly Dictionary<long, Road> _roads = new Dictionary<long, Road>();   // zrodlo x kopalnia -> droga (mapa sie nie zmienia; miasto handlowe - tak)
        private static bool _dayOne = true;    // pierwsze Daily po wczytaniu / starcie: doba niepelna - bez probki, bez zamowien, bez sredniej ShelfUse
        private static long _cPrev = -1;       // ladunki w drodze przy poprzedniej linii (-1 = pierwsza linia sesji)
        private static int _cCarts, _cBought, _cGold, _cDaysSum, _cArrCarts, _cArr, _cArrDays, _cWait, _cBack, _cBackPcs, _cRefund, _cLost, _cNoSurplus, _cNoGold, _cSources = -1;
        private static long _cTicks;

        // F4a (R1-7b): drewno naprawde kupione przez dymarki z polki miasta handlowego kopalni - srednia ok. 14 dob. Slownik podmieniany w calosci raz na
        // dobe (czytaja go latki cen przez CaravanBulk.Use)
        private static volatile Dictionary<Town, float> _shelfUse;
        private static readonly Dictionary<Town, int> _shelfDay = new Dictionary<Town, int>();
        private static int _shelfN;
        private static Dictionary<Settlement, List<Village>> _byMarket;
        private static string _pending;

        // liczniki doby (tylko log)
        private static int _dBlooms, _dBurnt, _dWoods, _dBought, _dGold, _dShortShelf, _dShortGold, _dNoMarket, _dTicked, _dOver;
        private static int _dTownMine, _dTownMineWood, _dTownMineSkip;
        private static float _dUnwon;
        private static int _stumbles, _stumblesDay;
        // F4b: liczniki doby rachunku za drewno (tylko log) i stan "otwarty wczoraj" do kontroli (-1 = pierwsza linia sesji)
        private static int _bReturns, _bBackN;
        private static long _bHanded, _bSettled, _bBack, _bAdded, _bCut, _bRefund, _bPrev = -1;

        [ThreadStatic] private static Village _tv;
        // recenzja R1: kopalnia BK w miescie - drewno z polki dopiero po cyklu, ktory naprawde dal lupy (postfiks cyklu; gra moze odrzucic cykl PO prefiksie)
        [ThreadStatic] private static Workshop _pw;
        [ThreadStatic] private static int _pNeed, _pPrice, _pCount;
        private static bool _cycleWired;
        private static int _dTownMineRej;
        [ThreadStatic] private static int _tvBefore, _tvReserved;
        [ThreadStatic] private static float _tvModel;

        /// <summary>Nowa gra albo wczytanie (konstruktor ArmouryBehavior): flaga, kopalnie, liczniki od zera.</summary>
        internal static void Reset()
        {
            NewCampaign = false; _iron = null; _wood = null; _applied = false; _scale = 1f; _wpb = -1; _pw = null;
            _mines.Clear(); _byMarket = null; _pending = null;
            _carts.Clear(); _roads.Clear(); _dayOne = true; _shelfUse = null; _shelfDay.Clear(); _shelfN = 0;
            NewDay(); _stumbles = 0; _cPrev = -1; _bPrev = -1;
        }

        internal static void OnNewGame() { NewCampaign = true; }

        /// <summary>Lupa obowiazuje w tej sesji (waga i cena ustawione przez HistoricalPrices.Apply).</summary>
        internal static bool On { get { return _applied; } }

        internal static bool IsBloom(ItemObject it) { return _applied && it != null && ReferenceEquals(it, _iron); }

        /// <summary>Ile starych sztuk gry (10 kg rudy) liczy jedna lupa = HistBulkUnitFactor z chwili Apply (ladunek, z ktorego ja wytopiono).</summary>
        internal static float Scale { get { return _scale; } }

        /// <summary>HistoricalPrices.Apply (krok 0): klucz, nowa kampania, latka ticku produkcji wsi i postfiks modelu wsi (bez niego nie ma sufitu wegla).</summary>
        internal static bool CanApply()
        {
            var s = Settings.Current;
            return s != null && s.BloomeryAtMine && NewCampaign && _wired && MaterialLaw.ProdModels > 0;
        }

        internal static void MarkApplied(ItemObject iron, float scale)
        {
            _iron = iron; _scale = Math.Max(1f, scale); _applied = true; _wpb = -1;
            try
            {
                var setter = AccessTools.PropertySetter(typeof(ItemObject), "Name");
                if (setter != null) setter.Invoke(iron, new object[] { new TextObject("{=!}Iron Bloom{@Plural}iron blooms{\\@}", null) });
            }
            catch (Exception e) { Stumble("Name", e); }
        }

        /// <summary>Linia startowa (HistoricalPrices.Apply po kroku 0).</summary>
        internal static void StartLine(bool bulk)
        {
            try
            {
                var s = Settings.Current;
                string st;
                if (_applied) st = "CZYNNE - \"iron\" to lupa " + (_iron != null ? _iron.Weight.ToString("0.#", CultureInfo.InvariantCulture) : "?") + " kg (metal z ladunku rudy, liczona jak ladunek: x" + _scale.ToString("0.#", CultureInfo.InvariantCulture)
                                  + "), drewna na lupe " + WoodPerBloom() + " ladunkow; kopalni z zapisu " + (_pending != null ? "do rozwiazania" : _mines.Count.ToString());
                else if (s == null || !s.BloomeryAtMine) st = "WYLACZONE (klucz Bloomery At Mine) - ruda 100 kg jak dotad";
                else if (!NewCampaign) st = "NIEAKTYWNE - stary zapis (kampania sprzed paczki): ruda 100 kg jak dotad";
                else if (!bulk) st = "NIEAKTYWNE - przelicznik ladunku 1 (ceny historyczne wylaczone albo HistBulkUnitFactor = 1)";
                else st = "NIEAKTYWNE - brak latki " + (!_wired ? "ticku produkcji wsi (TickGoodProduction)" : "modelu produkcji wsi (MaterialLaw.ProdPostfix)");
                Log.Info("Bloomery (E6/R1, 214): " + st + ".");
            }
            catch (Exception e) { Stumble("StartLine", e); }
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            if (_stumbles <= 3) Log.Error("Bloomery." + where, e);
        }

        private static ItemObject Wood()
        {
            if (_wood == null) { try { _wood = MBObjectManager.Instance.GetObject<ItemObject>("hardwood"); } catch { } }
            return _wood;
        }

        /// <summary>Ladunki drewna na jedna lupe: WorkshopWoodPerOre (kg drewna na kg rudy) x kg rudy w lupie / waga ladunku drewna (5 przy 100 kg).</summary>
        internal static int WoodPerBloom()
        {
            if (_wpb >= 0) return _wpb;
            var s = Settings.Current; var w = Wood();
            if (s == null || w == null || _iron == null) return 0;
            float kg = Math.Max(0f, s.WorkshopWoodPerOre) * HistoricalPrices.CountKg(_iron);
            _wpb = kg > 0f ? Math.Max(1, (int)Math.Ceiling(kg / Math.Max(0.1f, w.Weight) - 1e-3f)) : 0;
            return _wpb;
        }

        // ------------------------------------------------------------ zapis
        internal static string Export()
        {
            if (!NewCampaign) return "";
            var sb = new StringBuilder("nk");
            if (_pending != null) { sb.Append('|').Append(_pending); return sb.ToString(); }   // wczytane, jeszcze nie rozwiazane - oddajemy jak bylo
            foreach (var kv in _mines)
            {
                var st = kv.Key != null ? kv.Key.Settlement : null;
                if (st == null || st.StringId == null) continue;
                sb.Append('|').Append(st.StringId).Append(':').Append(kv.Value.Bank).Append(':').Append(kv.Value.Want);
            }
            // F4a: wozy w drodze - osobna sekcja po znaczniku (rekord kopalni zostaje 3-polowy; starsza wersja liczy te rekordy jako nieczytelne)
            if (_carts.Count > 0)
            {
                sb.Append('|').Append(CartMark);
                foreach (var c in _carts)
                {
                    var ms = c.Mine != null ? c.Mine.Settlement : null;
                    if (c.Src == null || c.Src.StringId == null || ms == null || ms.StringId == null || c.N <= 0) continue;
                    sb.Append('|').Append(c.Src.StringId).Append(':').Append(ms.StringId).Append(':').Append(c.DayBought).Append(':').Append(c.DayDue)
                      .Append(':').Append(c.N).Append(':').Append(c.Paid);
                }
            }
            // F4b: rachunki za drewno - osobna sekcja po znaczniku (wies:rachunek), tylko otwarte; rekord kopalni zostaje 3-polowy
            bool bills = false;
            foreach (var kv in _mines)
            {
                var st = kv.Key != null ? kv.Key.Settlement : null;
                if (st == null || st.StringId == null || kv.Value.Bill <= 0) continue;
                if (!bills) { sb.Append('|').Append(BillMark); bills = true; }
                sb.Append('|').Append(st.StringId).Append(':').Append(kv.Value.Bill);
            }
            return sb.ToString();
        }

        internal static void Import(string s)
        {
            NewCampaign = s != null && s.StartsWith("nk", StringComparison.Ordinal);
            _mines.Clear(); _byMarket = null; _carts.Clear(); _cPrev = -1; _bPrev = -1;
            _pending = NewCampaign && s.Length > 3 ? s.Substring(3) : null;
        }

        private static void ResolvePending()
        {
            var p = _pending;
            if (p == null) return;
            _pending = null;
            int ok = 0, bad = 0, cOk = 0, cBad = 0, cPcs = 0, bOk = 0, bBad = 0;
            long bSum = 0;
            bool carts = false;   // F4a: po znaczniku - rekordy wozow (6 pol); brak znacznika = brak wozow
            bool bills = false;   // F4b: po znaczniku - rachunki za drewno (2 pola: wies, rachunek) kopaln wczytanych wyzej; brak znacznika = brak rachunkow
            var inv = CultureInfo.InvariantCulture;
            foreach (var rec in p.Split('|'))
            {
                try
                {
                    if (rec == CartMark) { carts = true; bills = false; continue; }
                    if (rec == BillMark) { bills = true; carts = false; continue; }
                    var f = rec.Split(':');
                    if (bills)
                    {
                        Mine bm;
                        var bs = f.Length == 2 ? Settlement.Find(f[0]) : null;
                        if (bs == null || bs.Village == null || !_mines.TryGetValue(bs.Village, out bm)) { bBad++; continue; }
                        bm.Bill = Math.Max(0, int.Parse(f[1], inv));
                        bOk++; bSum += bm.Bill;
                        continue;
                    }
                    if (carts)
                    {
                        if (f.Length != 6) { cBad++; continue; }
                        var src = Settlement.Find(f[0]); var ms = Settlement.Find(f[1]);
                        int n = int.Parse(f[4], inv);
                        if (src == null || ms == null || ms.Village == null || n <= 0) { cBad++; continue; }
                        _carts.Add(new Cart { Src = src, Mine = ms.Village, DayBought = int.Parse(f[2], inv), DayDue = int.Parse(f[3], inv), N = n, Paid = Math.Max(0, int.Parse(f[5], inv)) });
                        cOk++; cPcs += n;
                        continue;
                    }
                    if (f.Length != 3) { bad++; continue; }
                    var st = Settlement.Find(f[0]);
                    if (st == null || st.Village == null) { bad++; continue; }
                    _mines[st.Village] = new Mine { Bank = Math.Max(0, int.Parse(f[1], CultureInfo.InvariantCulture)), Want = Math.Max(0, int.Parse(f[2], CultureInfo.InvariantCulture)) };
                    ok++;
                }
                catch { if (bills) bBad++; else if (carts) cBad++; else bad++; }
            }
            _byMarket = null;
            Log.Info("Bloomery (E6/R1, 214): z zapisu " + ok + " kopalni (drewno w dymarkach, potrzeba doby)" + (bad > 0 ? ", nieczytelnych " + bad : "")
                     + "; wozy z drewnem w drodze (F4a) " + cOk + " (" + cPcs + " ladunkow)" + (cBad > 0 ? ", nieczytelnych " + cBad : "")
                     + "; rachunki za drewno (F4b) " + bOk + " (" + bSum + " zl)" + (bBad > 0 ? ", nieczytelnych " + bBad : "") + ".");
        }

        // ------------------------------------------------------------ tick produkcji wsi
        /// <summary>Prefiks VillageGoodProductionCampaignBehavior.TickGoodProduction(Village, bool), Priority.First (przed prefiksem BK): wies w ticku
        /// i stan lup w jej magazynie. Rozdanie startowe (initialProductionForTowns) - bez dymarki (zapas startowy gry, StartStock).</summary>
        public static void TickPrefix(Village __0, bool __1)
        {
            _tv = null;
            if (!_applied || __1 || __0 == null) return;
            try
            {
                if (_pending != null) ResolvePending();
                var r = __0.Settlement != null ? __0.Settlement.ItemRoster : null;
                if (r == null) return;
                _tvBefore = r.GetItemNumber(_iron); _tvReserved = 0; _tvModel = 0f;
                _tv = __0;
            }
            catch (Exception e) { _tv = null; Stumble("TickPrefix", e); }
        }

        /// <summary>Postfiks tego samego ticku: lupy dopisane wsi (roznica magazynu) spalaja WoodPerBloom ladunkow drewna dymarki; potrzeba doby
        /// = WoodPerBloom x wynik modelu bez sufitu (zaokraglony w gore).</summary>
        public static void TickPostfix(Village __0, bool __1)
        {
            var v = _tv;
            if (v == null || !ReferenceEquals(v, __0)) return;
            try
            {
                int made = Math.Max(0, v.Settlement.ItemRoster.GetItemNumber(_iron) - _tvBefore);
                Mine m;
                if (!_mines.TryGetValue(v, out m))
                {
                    if (made <= 0 && _tvModel <= 0f) return;
                    m = new Mine(); _mines[v] = m; _byMarket = null;
                }
                int wpb = WoodPerBloom();
                int burn = made * wpb;
                if (burn > m.Bank) { _dOver += wpb > 0 ? (burn - m.Bank + wpb - 1) / wpb : 0; burn = m.Bank; }   // nie powinno sie zdarzyc: sufit w modelu rezerwuje drewno
                m.Bank -= burn;
                m.Want = wpb * (int)Math.Ceiling(_tvModel - 1e-4f);
                _dBlooms += made; _dBurnt += burn; _dTicked++;
            }
            catch (Exception e) { Stumble("TickPostfix", e); }
        }

        public static Exception TickFinalizer(Exception __exception) { _tv = null; return __exception; }

        /// <summary>MaterialLaw.ProdPostfix (ostatni krok, przed ksiega): w ticku produkcji tej wsi lupy najwyzej tyle, na ile starczy drewna w dymarce
        /// (LimitMax, liczba calkowita - gra i BK nie dorzuca ulamka ponad nia). Drewno zarezerwowane na wczesniejsze wywolania w tym samym ticku
        /// (np. mineral dwa razy na liscie BK) odejmuje sie - lup nigdy wiecej niz drewna.</summary>
        internal static void Cap(Village village, ItemObject item, ref ExplainedNumber r)
        {
            var v = _tv;
            if (v == null || !ReferenceEquals(village, v) || !ReferenceEquals(item, _iron)) return;
            try
            {
                float x = r.ResultNumber;
                if (!(x > 0f)) return;
                _tvModel += x;
                int wpb = WoodPerBloom();
                if (wpb <= 0) return;
                Mine m;
                int bank = _mines.TryGetValue(village, out m) ? m.Bank : 0;
                int cap = Math.Max(0, bank / wpb - _tvReserved);
                if (x > cap)
                {
                    _dUnwon += x - cap;
                    if (m != null) m.Unwon += x - cap;   // F4a: linia per kopalnia
                    r.LimitMax(Math.Min((float)cap, r.LimitMaxValue), _txtNoCoal);
                    _tvReserved += cap;
                }
                else _tvReserved += (int)Math.Ceiling(x - 1e-4f);
            }
            catch (Exception e) { Stumble("Cap", e); }
        }

        // ------------------------------------------------------------ las okregu
        private static List<Village> MinesOf(Settlement market)
        {
            if (_byMarket == null)
            {
                var d = new Dictionary<Settlement, List<Village>>();
                foreach (var v in _mines.Keys)
                {
                    var mk = v != null ? v.TradeBound : null;
                    if (mk == null) continue;
                    List<Village> l;
                    if (!d.TryGetValue(mk, out l)) d[mk] = l = new List<Village>();
                    l.Add(v);
                }
                _byMarket = d;
            }
            List<Village> res;
            return _byMarket.TryGetValue(market, out res) ? res : null;
        }

        /// <summary>VillageWoodlot.TickPostfix: z n ladunkow lasu wsi v ile idzie do dymarek kopalni jej okregu (TradeBound) - najpierw tam, gdzie
        /// najwiekszy brak do 2 x potrzeby doby. Reszta jedzie na targ jak dotad.</summary>
        internal static int FromWoodlot(Village v, int n)
        {
            if (!_applied || n <= 0 || v == null) return 0;
            try
            {
                if (_pending != null) ResolvePending();
                var mk = v.TradeBound;
                if (mk == null) return 0;
                var list = MinesOf(mk);
                if (list == null) return 0;
                int left = n;
                while (left > 0)
                {
                    Mine best = null; int room = 0;
                    foreach (var mv in list)
                    {
                        Mine m;
                        if (!_mines.TryGetValue(mv, out m)) continue;
                        int rm = 2 * m.Want - m.Bank;
                        if (rm > room) { room = rm; best = m; }
                    }
                    if (best == null) break;
                    int take = Math.Min(left, room);
                    best.Bank += take; left -= take;
                    best.LocDay += take; best.ForDay += take;   // F4a: drewno na miejscu (las okregu)
                }
                // F4a: las, ktorego pelne dymarki okregu nie przyjely (jedzie na targ jak dotad), liczy sie do miary "na miejscu" jak przyjety (wedlug potrzeby
                // kopaln) - inaczej woz z dalszego miasta zapelnialby dymarke, las by sie nie miescil, niedobor rosl i woz wypieral darmowy las na stale
                if (left > 0)
                {
                    int wants = 0;
                    foreach (var mv in list) { Mine m; if (_mines.TryGetValue(mv, out m)) wants += m.Want; }
                    if (wants > 0)
                        foreach (var mv in list)
                        {
                            Mine m;
                            if (!_mines.TryGetValue(mv, out m) || m.Want <= 0) continue;
                            float part = (float)left * m.Want / wants;
                            m.LocDay += part; m.ForDay += part;
                        }
                }
                int d = n - left;
                _dWoods += d;
                return d;
            }
            catch (Exception e) { Stumble("FromWoodlot", e); return 0; }
        }

        // ------------------------------------------------------------ zakup z polki miasta handlowego (raz na dobe)
        /// <summary>ArmouryBehavior.OnDailyTick (przed OreLedger.Daily): woz kazdej kopalni kupuje brakujace drewno do potrzeby doby z polki miasta
        /// handlowego za zloto wsi po cenie targu (zloto wies -> kasa miasta, towar z polki do dymarki) - drewno na nastepna dobe.
        /// F4a: przed tym przyjazdy wozow z dalszych miast (a), po tym probka doby "drewno na miejscu" i zamowienia wozem (c), na koncu srednia ShelfUse.</summary>
        internal static void Daily()
        {
            if (!_applied) return;
            try
            {
                if (_pending != null) ResolvePending();
                _byMarket = null;   // TradeBound wsi zamkowych gra przestawia (wojny, zmiana wlasciciela) - okregi od nowa raz na dobe
                var wood = Wood();
                if (wood == null) return;
                var el = new EquipmentElement(wood);
                int today = (int)CampaignTime.Now.ToDays;
                bool coal = CoalFirst;   // F4b: zaplata za drewno dopisuje sie do rachunku kopalni
                long t0 = Stopwatch.GetTimestamp();
                try { CartArrivals(wood, today); } catch (Exception e) { Stumble("Daily(przyjazdy wozow)", e); }   // (a) dziala takze przy zasiegu 0 (oplacone wozy dojezdzaja)
                _cTicks += Stopwatch.GetTimestamp() - t0;
                foreach (var kv in _mines)
                {
                    try
                    {
                        var v = kv.Key; var m = kv.Value;
                        int want = m.Want - m.Bank;
                        if (want <= 0 || v == null) continue;
                        m.Asked = want;           // F4b: pomiar doby (linia)
                        var mk = v.TradeBound;
                        var town = mk != null ? mk.Town : null;
                        var shelf = mk != null ? mk.ItemRoster : null;
                        if (town == null || shelf == null) { _dNoMarket += want; continue; }
                        int n = Math.Min(want, shelf.GetItemNumber(wood));
                        if (n < want) _dShortShelf += want - Math.Max(0, n);
                        if (n <= 0) continue;
                        int price = Math.Max(0, town.MarketData.GetPrice(el));
                        m.Price = price;
                        if (price > 0)
                        {
                            int afford = Math.Max(0, v.Gold) / price;
                            if (afford < n) { _dShortGold += n - afford; m.NoGold += n - afford; n = afford; }
                        }
                        if (n <= 0) continue;
                        int paid = 0;
                        var gf = GoodsLedger.Begin(GoodsLedger.FBloom, town);
                        try
                        {
                            if (price > 0)
                            {
                                int had = v.Gold;
                                v.ChangeGold(-n * price);
                                paid = had - v.Gold;
                                town.ChangeGold(paid);
                            }
                            shelf.AddToCounts(wood, -n);
                        }
                        finally { GoodsLedger.End(gf); }
                        m.Bank += n;
                        m.LocDay += n;            // F4a: drewno na miejscu (wlasna polka)
                        NoteShelf(town, n);       // F4a (R1-7b): popyt drewna miasta handlowego - to, co dymarka naprawde kupila
                        _dBought += n; _dGold += paid;
                        m.Got += n; m.BuyDay += paid;
                        if (coal && paid > 0) { m.Bill += paid; _bAdded += paid; }   // F4b: rachunek za drewno - do odliczenia od utargu przed dzialka pana
                        OreLedger.NoteBloomeryWood(wood, n);
                        if (paid > 0) { try { TownFolk.Sale(mk, paid, TownFolk.MixGoods); } catch { } }   // drewno kupione od kupcow miasta (tylko licznik)
                    }
                    catch (Exception e) { Stumble("Daily(kopalnia)", e); }
                }
                // F4a: probka doby "drewno na miejscu" (las od poprzedniego Daily + zakup z wlasnej polki przed chwila). Pierwsze Daily po wczytaniu - doba
                // niepelna (las tylko od wczytania): bez probki i bez zamowien
                bool full = !_dayOne; _dayOne = false;
                foreach (var m in _mines.Values) { if (full) m.Push(); else { m.LocDay = 0f; m.ForDay = 0f; } }
                t0 = Stopwatch.GetTimestamp();
                try { if (full) CartOrders(wood, el, today); } catch (Exception e) { Stumble("Daily(zamowienia wozow)", e); }   // (c)
                _cTicks += Stopwatch.GetTimestamp() - t0;
                // F4b: koniec doby zakupow drewna (kroki b i c) - rachunek ponad zakupy ostatnich 14 dob wygasa; klucz wylaczony - rachunki do zera
                foreach (var m in _mines.Values) _bCut += m.RollBill(coal);
                try { RollShelfUse(full); } catch (Exception e) { Stumble("Daily(ShelfUse)", e); }
            }
            catch (Exception e) { Stumble("Daily", e); }
        }

        // ------------------------------------------------------------ F4a: woz kopalni po drewno z dalszych miast
        private static long InTransit() { long n = 0; foreach (var c in _carts) n += c.N; return n; }

        /// <summary>(a) Przyjazdy: woz w dniu przyjazdu oddaje drewno dymarce; wies nie-Normal - czeka; po 30 dobach od zakupu (liczonych do czekania) albo bez
        /// kopalni - zawraca (wzor TradeCarts.Arrivals).</summary>
        private static void CartArrivals(ItemObject wood, int today)
        {
            for (int i = _carts.Count - 1; i >= 0; i--)
            {
                var c = _carts[i];
                try
                {
                    if (today < c.DayDue) continue;
                    var v = c.Mine; Mine m = null;
                    bool dstOk = v != null && v.Settlement != null && _mines.TryGetValue(v, out m);
                    bool srcOk = c.Src != null && c.Src.ItemRoster != null;
                    bool late = today - c.DayBought > Math.Max(CartMaxDays, c.DayDue - c.DayBought);
                    if (!dstOk && !srcOk) { _cLost += c.N; _carts.RemoveAt(i); continue; }   // nie ma dokad ani skad (tylko po zlym zapisie)
                    if (!dstOk || (late && srcOk)) { CartBack(c, wood); _carts.RemoveAt(i); continue; }
                    if (v.VillageState != Village.VillageStates.Normal && !late) { _cWait++; continue; }
                    int n = c.N; c.N = 0;
                    m.Bank += n;
                    _cArr += n; _cArrCarts++; _cArrDays += today - c.DayBought;
                    _carts.RemoveAt(i);
                }
                catch (Exception e) { Stumble("Daily(woz)", e); }   // woz zostaje w drodze - sprobuje nazajutrz
            }
        }

        /// <summary>Zawrocenie: drewno na polke zrodla (ramka FBloom i wpis ujemny ksiegi drewna - zakup byl ujsciem), zwrot zaplaty z kasy zrodla do wsi,
        /// nie wiecej niz kasa zrodla (reszta zostaje w kasie zrodla - to samo zloto, ktore wies zaplacila; wzor TradeCarts.Back).</summary>
        private static void CartBack(Cart c, ItemObject wood)
        {
            int n = c.N;
            var src = c.Src; var town = src != null ? src.Town : null;
            if (n > 0 && src != null && src.ItemRoster != null)
            {
                var gf = GoodsLedger.Begin(GoodsLedger.FBloom, town);
                try { src.ItemRoster.AddToCounts(wood, n); }
                finally { GoodsLedger.End(gf); }
                c.N = 0;
                OreLedger.NoteBloomeryWood(wood, -n);
            }
            _cBack++; _cBackPcs += n - c.N;
            int r = town != null && c.Mine != null ? Math.Min(c.Paid, Math.Max(0, town.Gold)) : 0;
            if (r > 0)
            {
                town.ChangeGold(-r);
                c.Mine.ChangeGold(r);
                _cRefund += r;
                // F4b: zwrocone zloto nie jest juz kosztem drewna - rachunek kopalni maleje o zwrot (inaczej wies mialaby zwrot i mniejsza dzialke pana)
                Mine bm;
                if (_mines.TryGetValue(c.Mine, out bm) && bm.Bill > 0) { int x = Math.Min(bm.Bill, r); bm.Bill -= x; _bRefund += x; }
            }
            c.Paid = 0;
        }

        /// <summary>Droga wozu ze zrodla do kopalni (jednostki mapy; -1 = bez drogi): ladem prosto do wsi; bez drogi ladowej (wyspa) - ladem i morzem do
        /// portu miasta handlowego kopalni (port = true: dolicza sie doba z miasta do wsi). Z pamieci - mapa sie nie zmienia.</summary>
        private static float RoadTo(Settlement src, Village v, Settlement mk, out bool port)
        {
            long key = ((long)src.Id.InternalValue << 32) | v.Settlement.Id.InternalValue;
            Road r;
            if (!_roads.TryGetValue(key, out r) || !ReferenceEquals(r.Mk, mk))
            {
                r = new Road { Mk = mk };
                r.Dist = TradeCarts.Road(src, v.Settlement, -1f, true);
                if (r.Dist < 0f && mk != null && !ReferenceEquals(mk, src))
                {
                    r.Dist = TradeCarts.Road(src, mk, -1f);
                    r.Port = r.Dist >= 0f;
                }
                _roads[key] = r;
            }
            port = r.Port;
            return r.Dist;
        }

        /// <summary>Miasta z nadwyzka drewna ponad prog kontraktow 174 - raz na dobe (prog z pamieci doby MaterialOrders).</summary>
        private static List<Source> Sources(ItemObject wood, int minLoad)
        {
            var list = new List<Source>();
            foreach (var t in Town.AllTowns)
            {
                var st = t != null ? t.Settlement : null;
                if (st == null || !t.IsTown || st.ItemRoster == null) continue;
                int have = st.ItemRoster.GetItemNumber(wood);
                if (have < minLoad) continue;
                int keep = MaterialOrders.SourceKeepFor(t, wood);
                if (have - keep < minLoad) continue;
                list.Add(new Source { Town = t, St = st, Keep = keep });
            }
            _cSources = list.Count;
            return list;
        }

        /// <summary>Miejsce w dymarce na nowy woz z przyjazdem w dniu due (ladunki; moze wyjsc ujemne). Stan przewidywany bez nowego wozu, doba po dobie:
        /// las okregu (do sufitu 2 x potrzeba), ogien bierze potrzebe doby, potem wozy z przyjazdem tego dnia; miejsce = sufit wozu (2 x potrzeba - doba
        /// lasu) - stan po przyjazdach dnia due.
        /// Recenzja F4a: gdy PO dniu due dojezdzaja jeszcze inne wozy (dalsze zrodlo zamowione wczesniej), liczy sie najciasniejszy dzien az do ostatniego
        /// z nich - nowy woz moze dowiezc tylko tyle, ile ogien spali przed ich przyjazdem (fed: ogien, ktorego bez nowego wozu by zabraklo, dolicza sie
        /// do miejsca). Bez tego blizsze zrodlo dowozilo do sufitu, a pozniejszy woz trafial na pelna dymarke i wypieral las okregu.</summary>
        private static float Room(Mine m, List<Cart> mine, int today, int due)
        {
            float want = m.Want, top = 2f * want, forest = Math.Min(m.ForAvg, want), lim = top - forest, b = m.Bank;
            int last = due;
            foreach (var c in mine) if (c.DayDue > last) last = c.DayDue;
            float room = lim, fed = 0f;
            bool seen = false;
            for (int d = today + 1; d <= last; d++)
            {
                if (b < top) b = Math.Min(top, b + forest);
                if (d > due && b < want) fed += want - b;
                b = Math.Max(0f, b - want);
                foreach (var c in mine) if (c.DayDue == d || (d == today + 1 && c.DayDue <= today)) b += c.N;
                if (d < due) continue;
                float r = lim - b + fed;
                if (!seen || r < room) { room = r; seen = true; }
            }
            return room;
        }

        /// <summary>(c) Zamowienia: kopalnia z niedoborem doby (potrzeba - srednia 7 dob drewna na miejscu) kupuje wozem z miast z nadwyzka w zasiegu.
        /// Zamowienie pod zrodlo o drodze T dob = niedobor x (T + 1) - wozy, ktore dojada do dnia przyjazdu, nie wiecej niz miejsce w dymarce tego dnia
        /// (2 x potrzeba - doba lasu - przewidywany stan) i nie mniej niz drewno na jedna lupe. Kolejnosc: najtansze, potem najblizsze; nastepne zrodlo
        /// tylko wtedy, gdy poprzedniemu zabraklo nadwyzki albo jego woz trafilby na pelna dymarke. Recenzja F4a: po wozie kupionym do sufitu pytane sa
        /// jeszcze zrodla o KROTSZEJ drodze (karmia ogien do przyjazdu tamtego wozu; miejsce liczy Room do ostatniego wozu w drodze - bez przepelnienia).
        /// Bez zamowien: zasieg 0, wies nie-Normal, potrzeba 0, brak probki.</summary>
        private static void CartOrders(ItemObject wood, EquipmentElement el, int today)
        {
            var s = Settings.Current;
            float range = s != null ? s.BloomeryCartRange : 0f;
            _cSources = -1;
            if (!(range > 0f)) return;
            int wpb = WoodPerBloom();
            if (wpb <= 0) return;
            bool coal = CoalFirst;   // F4b
            List<Source> srcs = null;
            var cands = new List<Cand>();
            var mine = new List<Cart>();
            foreach (var kv in _mines)
            {
                try
                {
                    var v = kv.Key; var m = kv.Value;
                    if (v == null || v.Settlement == null || m.Want <= 0 || m.LocN <= 0) continue;
                    if (v.VillageState != Village.VillageStates.Normal) continue;
                    float x = m.Want - m.LocAvg;   // niedobor doby
                    if (!(x > 0f)) continue;
                    int cx = Math.Max(1, (int)Math.Ceiling(x - 1e-3f));
                    mine.Clear();
                    int all = 0, far = today + 1;
                    foreach (var c in _carts) if (ReferenceEquals(c.Mine, v) && c.N > 0) { mine.Add(c); all += c.N; if (c.DayDue > far) far = c.DayDue; }
                    if ((int)Math.Ceiling(x * (far - today + 1) - 1e-3f) - all < wpb) continue;   // nawet pod najdluzsza droge wozow w drodze niczego nie brakuje
                    if (srcs == null) srcs = Sources(wood, wpb);
                    var mk = v.TradeBound;
                    var pos = v.Settlement.GetPosition2D; var fac = v.Settlement.MapFaction;
                    cands.Clear();
                    foreach (var sc in srcs)
                    {
                        if (ReferenceEquals(sc.St, mk)) continue;                    // wlasne miasto handlowe - krok (b)
                        if (pos.Distance(sc.St.GetPosition2D) > range) continue;    // droga >= linia prosta
                        if (sc.St.IsUnderSiege) continue;
                        if (fac != null && sc.St.MapFaction != null && FactionManager.IsAtWarAgainstFaction(sc.St.MapFaction, fac)) continue;
                        if (sc.St.ItemRoster.GetItemNumber(wood) - sc.Keep < wpb) continue;   // nadwyzke wykupily dzis inne kopalnie
                        bool port;
                        float d = RoadTo(sc.St, v, mk, out port);
                        if (d < 0f || d > range) continue;
                        int price = Math.Max(0, sc.Town.MarketData.GetPrice(el));
                        if (price <= 0) continue;
                        cands.Add(new Cand { Town = sc.Town, St = sc.St, Keep = sc.Keep, Price = price, Dist = d, Days = TradeCarts.DaysFor(d) + (port ? 1 : 0) });
                    }
                    cands.Sort((a, b) => a.Price != b.Price ? a.Price.CompareTo(b.Price) : a.Dist.CompareTo(b.Dist));
                    int rest = -1, nearer = int.MaxValue; bool noGold = false, roomFull = false;
                    foreach (var c in cands)
                    {
                        if (c.Days >= nearer) continue;                              // recenzja F4a: po wozie do sufitu pytamy juz tylko zrodla o krotszej drodze
                        int due = today + c.Days, arr = 0;
                        foreach (var k in mine) if (k.DayDue <= due) arr += k.N;
                        int need = (int)Math.Ceiling(x * (c.Days + 1) - 1e-3f) - arr;
                        if (need < wpb) { if (rest < 0) rest = 0; break; }          // pod te droge niczego nie brakuje - dalszych zrodel nie pytamy
                        if (rest < 0) rest = need;
                        int room = (int)Math.Floor(Room(m, mine, today, due));
                        // w dniu przyjazdu z tego zrodla dymarka bedzie pelna (np. wlasnie dojezdza woz z dalszego miasta) - to zrodlo pomijamy, ale dalsze
                        // (inny dzien przyjazdu) pytamy: przerwanie tutaj zostawialo dalszy strumien bez zamowien na tydzien (symulacja reguly)
                        if (room < wpb) { roomFull = true; continue; }
                        int n = Math.Min(Math.Min(need, room), c.St.ItemRoster.GetItemNumber(wood) - c.Keep);
                        if (n < wpb) continue;
                        if (v.Gold < c.Price) { noGold = true; break; }
                        var town = c.Town;
                        int got = 0, paid = 0, cost = 0, first, last;
                        var gf = GoodsLedger.Begin(GoodsLedger.FBloom, town);
                        try
                        {
                            got = ShelfBuy.Take(c.St.ItemRoster, el, n, Math.Max(0, v.Gold), () => Math.Max(0, town.MarketData.GetPrice(el)), out cost, out first, out last, c.Price, false);
                            if (got > 0 && cost > 0)
                            {
                                int had = v.Gold;
                                v.ChangeGold(-cost);
                                paid = had - v.Gold;
                                town.ChangeGold(paid);
                            }
                        }
                        finally
                        {
                            GoodsLedger.End(gf);
                            if (got > 0)   // drewno zeszlo z polki - woz musi istniec, cokolwiek stalo sie potem
                            {
                                var cart = new Cart { Src = c.St, Mine = v, DayBought = today, DayDue = due, N = got, Paid = paid };
                                _carts.Add(cart); mine.Add(cart);
                                _cCarts++; _cBought += got; _cGold += paid; _cDaysSum += c.Days;
                                m.Got += got; m.BuyDay += paid;
                                if (coal && paid > 0) { m.Bill += paid; _bAdded += paid; }   // F4b: rachunek obejmuje takze zakupy wozem
                                OreLedger.NoteBloomeryWood(wood, got);
                            }
                        }
                        if (got > 0 && paid > 0) { try { TownFolk.Sale(c.St, paid, TownFolk.MixGoods); } catch { } }   // drewno kupione od kupcow miasta-zrodla (tylko licznik)
                        rest -= got;
                        if (got >= need) break;                                      // potrzeba pod te droge pokryta
                        if (got < n)
                        {
                            // mniej niz zamowione: kiesa wsi (nastepny ladunek drozszy niz zloto wsi) albo polka (wyjatek zdarzenia polki) - wtedy nastepne zrodlo
                            if (Math.Max(0, town.MarketData.GetPrice(el)) > v.Gold) { noGold = true; break; }
                            continue;
                        }
                        // sufit dymarki w dniu przyjazdu z tego zrodla. Recenzja F4a: dalszych zrodel nie pytamy (wypieralyby las), ale zrodla o KROTSZEJ
                        // drodze tak - ich woz dojedzie wczesniej i nakarmi ogien do przyjazdu tego wozu (Room liczy miejsce do ostatniego wozu w drodze).
                        // Przerwanie tutaj zostawialo kopalnie bez drewna na cala droge tanszego, dalszego zrodla, choc blizsze mialo nadwyzke
                        if (n == room && room < need) { roomFull = true; nearer = c.Days; continue; }
                    }
                    // niedobor doby bez wozu (najwyzej niedobor jednej doby - zamowienie pod droge liczy kilka dob naraz)
                    if (rest < 0) rest = (int)Math.Ceiling(x * (far - today + 1) - 1e-3f) - all;   // zadnego zrodla z nadwyzka w zasiegu
                    if (rest >= wpb && !roomFull)
                    {
                        if (noGold) { _cNoGold += Math.Min(cx, rest); m.NoGold += Math.Min(cx, rest); } else _cNoSurplus += Math.Min(cx, rest);
                    }
                }
                catch (Exception e) { Stumble("Daily(zamowienie wozu)", e); }
            }
        }

        // ------------------------------------------------------------ F4b: rachunek za drewno przed dzialka pana
        /// <summary>F4b: rachunek za drewno jest prowadzony i rozliczany (lupa czynna i klucz BloomeryCoalBeforeLordShare).</summary>
        internal static bool CoalFirst { get { var s = Settings.Current; return _applied && s != null && s.BloomeryCoalBeforeLordShare; } }

        /// <summary>VillageTakings.EnterPostfix (woz wsi oddal utarg we wsi macierzystej): otwarty rachunek za drewno tej kopalni; 0 - wies bez dymarki,
        /// klucz wylaczony albo nic do odliczenia. Utarg wozow kopaln liczony do linii takze przy wylaczonym kluczu.</summary>
        internal static int BillOf(Village v, int handed)
        {
            if (!_applied || v == null || handed <= 0) return 0;
            try
            {
                if (_pending != null) ResolvePending();
                Mine m;
                if (!_mines.TryGetValue(v, out m)) return 0;
                _bReturns++; _bHanded += handed;
                return CoalFirst ? Math.Max(0, m.Bill) : 0;
            }
            catch (Exception e) { Stumble("BillOf", e); return 0; }
        }

        /// <summary>VillageTakings.EnterPostfix po rozliczeniu: `d` utargu pokrylo rachunek (maleje o d), `back` zlota przeszlo z licznika podatku wsi
        /// do jej kiesy (pan dostal mniej dokladnie o tyle). Zlota tu nie ruszamy - tylko rachunek i liczniki linii.</summary>
        internal static void BillSettled(Village v, int d, long back)
        {
            Mine m;
            if (v == null || d <= 0 || !_mines.TryGetValue(v, out m)) return;
            if (d > m.Bill) d = m.Bill;
            m.Bill -= d;
            _bSettled += d; _bBack += back;
            if (back > 0) _bBackN++;
        }

        private static long BillsOpen() { long n = 0; foreach (var m in _mines.Values) n += m.Bill; return n; }

        /// <summary>F4b: wstawka "rachunek za drewno (F4b)" linii "Zelazo (lupy) (214)" - utarg oddany przez wozy kopaln (dokladny, z powrotow do wsi),
        /// ile z niego pokrylo rachunek, ile zlota przeszlo z licznikow panow do kies, rachunek otwarty i jego kontrola (reszta 0), kiesy kopaln
        /// (min / mediana / suma; ile ponizej kosztu drewna doby = potrzeba ponad las okregu x cena w miescie handlowym) i 3 kopalnie z najwiekszym
        /// brakiem zlota tej doby. Tylko log.</summary>
        private static void CoalSegment(StringBuilder line)
        {
            try { var sb = new StringBuilder(512); CoalSegment2(sb); line.Append(sb); }   // cala wstawka albo nic
            catch (Exception e) { Stumble("IronSegment(F4b)", e); }
        }

        private static void CoalSegment2(StringBuilder sb)
        {
            var s = Settings.Current;
            bool key = s != null && s.BloomeryCoalBeforeLordShare, whole = s != null && s.VillageTakingsWhole;
            long open = 0, gsum = 0; int openN = 0, below = 0, needy = 0;
            var golds = new List<int>();
            var top = new List<KeyValuePair<Village, Mine>>();
            var wood = Wood();
            foreach (var kv in _mines)
            {
                var v = kv.Key; var m = kv.Value;
                if (m.Bill > 0) { open += m.Bill; openN++; }
                if (v == null) continue;
                int g = v.Gold;
                golds.Add(g); gsum += g;
                if (m.NoGold > 0) top.Add(kv);
                float need = m.Want - Math.Min(m.ForAvg, m.Want);
                if (!(need > 0f)) continue;
                int price = m.Price;
                if (price <= 0 && wood != null)   // tej doby kopalnia nie pytala o cene (dymarka pelna albo pusta polka) - cena z targu teraz
                {
                    var mk = v.TradeBound; var town = mk != null ? mk.Town : null;
                    if (town != null) price = Math.Max(0, town.MarketData.GetPrice(new EquipmentElement(wood)));
                    m.Price = price;
                }
                needy++;
                if (g < need * price) below++;
            }
            golds.Sort();
            int cnt = golds.Count;
            long med = cnt == 0 ? 0 : (cnt % 2 == 1 ? golds[cnt / 2] : ((long)golds[cnt / 2 - 1] + golds[cnt / 2]) / 2);
            sb.Append("; rachunek za drewno (F4b): ").Append(!key ? "WYLACZONE (Bloomery Coal Before Lord Share)" : (whole ? "CZYNNE" : "NIECZYNNE (Village Takings Whole wylaczone - bez okna powrotu wozu)"))
              .Append(", powroty wozow kopaln ").Append(_bReturns).Append(" z utargiem ").Append(_bHanded).Append(" zl, z tego na rachunek ").Append(_bSettled)
              .Append(" zl, z licznikow panow do kies wsi ").Append(_bBack).Append(" zl (").Append(_bBackN).Append(" powrotow), rachunek otwarty ").Append(open)
              .Append(" zl w ").Append(openN).Append(" kopalniach");
            if (_bPrev >= 0)
                sb.Append("; kontrola rachunku: otwarty wczoraj ").Append(_bPrev).Append(" + drewno kupione ").Append(_bAdded).Append(" - na rachunek ").Append(_bSettled)
                  .Append(" - wygasle ponad ").Append(BillDays).Append(" dob zakupow ").Append(_bCut).Append(" - zwrot zawroconych wozow ").Append(_bRefund)
                  .Append(" - otwarty dzis = reszta ").Append(_bPrev + _bAdded - _bSettled - _bCut - _bRefund - open);
            else sb.Append("; kontrola rachunku od nastepnej doby");
            sb.Append("; kiesy kopaln: min ").Append(cnt > 0 ? golds[0] : 0).Append(" / mediana ").Append(med).Append(" / suma ").Append(gsum).Append(" zl w ").Append(cnt)
              .Append(" kopalniach, ponizej kosztu drewna doby ").Append(below).Append(" z ").Append(needy).Append(" (potrzeba ponad las okregu po cenie miasta handlowego)");
            top.Sort((a, b) => b.Value.NoGold.CompareTo(a.Value.NoGold));
            sb.Append("; najwiecej bez zlota: ");
            if (top.Count == 0) sb.Append("-");
            for (int i = 0; i < top.Count && i < 3; i++)
            {
                var v = top[i].Key; var m = top[i].Value;
                if (i > 0) sb.Append(", ");
                sb.Append(v.Name != null ? v.Name.ToString() : "?").Append(" (kiesa ").Append(v.Gold).Append(", drewno w miescie handlowym po ").Append(m.Price)
                  .Append(" zl, chciane z jego polki ").Append(m.Asked).Append(", kupione z polki i wozem ").Append(m.Got).Append(", bez zlota ").Append(m.NoGold).Append(")");
            }
        }

        // ------------------------------------------------------------ F4a (R1-7b): popyt drewna miasta handlowego kopalni
        /// <summary>Ladunki drewna na dobe, ktore dymarki naprawde kupuja z polki tego miasta (krok b - wlasne miasto handlowe kopalni wsi; kopalnia BK
        /// w miescie): srednia z pelnych dob od wczytania, od 14. doby srednia kroczaca 1/14. Czyta CaravanBulk.HandUse (cel zapasu, prog nadwyzki, cena).
        /// Zakupy wozem w dalszych miastach tu NIE wchodza: prog nadwyzki zrodla to 20 dob jego zuzycia (CaravanBulkStockDays 10 x CaravanBulkSurplusFactor 2),
        /// wiec zrodlo, z ktorego wozy biora 60 ladunkow na dobe, musialoby trzymac ponad 1200 ladunkow, zanim sprzeda nastepny - woz dusilby sam siebie.</summary>
        internal static float ShelfUse(Town town)
        {
            var d = _shelfUse; float v;
            return d != null && town != null && d.TryGetValue(town, out v) ? v : 0f;
        }

        private static void NoteShelf(Town town, int n)
        {
            if (town == null || n <= 0) return;
            int v; _shelfDay.TryGetValue(town, out v); _shelfDay[town] = v + n;
        }

        private static void RollShelfUse(bool full)
        {
            if (!full) { _shelfDay.Clear(); return; }   // doba niepelna (pierwsze Daily po wczytaniu) - bez pomiaru
            _shelfN++;
            float k = 1f / Math.Min(_shelfN, 14);       // pierwszy pomiar = srednia (nie start od zera), potem srednia z dob, od 14. kroczaca
            var old = _shelfUse; var nu = new Dictionary<Town, float>();
            if (old != null)
                foreach (var kv in old)
                {
                    int t; _shelfDay.TryGetValue(kv.Key, out t);
                    float a = kv.Value + (t - kv.Value) * k;
                    if (a > 0.01f) nu[kv.Key] = a;
                }
            foreach (var kv in _shelfDay)
            {
                if (old != null && old.ContainsKey(kv.Key)) continue;
                float a = kv.Value * k;
                if (a > 0.01f) nu[kv.Key] = a;
            }
            _shelfDay.Clear();
            _shelfUse = nu;
        }

        // ------------------------------------------------------------ kopalnia BK w miescie / zamku
        /// <summary>WorkshopLaw.CyclePrefix po BulkLineSkip: linia bez wsadu dajaca lupy (kopalnia BK w miescie albo zamku) wytapia z drewnem z polki
        /// tej osady - warsztat kupuje WoodPerBloom x sztuki ladunkow po cenie targu (kasa warsztatu -> kasa miasta). Brak drewna albo kapitalu -
        /// cykl nie idzie (true). Linia drewna i reszta warsztatow bez zmian (false).
        /// Recenzja R1: gra po prefiksie moze cykl jeszcze odrzucic (CanNotableWorkshopProduceThisCycle: utarg <= place cyklu, kasa miasta < utarg) -
        /// zakup idzie wiec w postfiksie cyklu (CyclePostfix), tylko gdy cykl dal lupy; tu tylko sprawdzenie i rezerwacja. Bez postfiksu - zakup od razu (jak dotad).</summary>
        internal static bool TownMineBlocked(WorkshopType.Production p, Workshop w)
        {
            _pw = null;
            if (!_applied || w == null) return false;
            try
            {
                if (p.Inputs != null && p.Inputs.Count > 0) return false;
                if (p.Outputs == null || p.Outputs.Count == 0) return false;
                int count = 0;
                foreach (var o in p.Outputs)
                {
                    if (o.Item1 == null || o.Item1.StringId != "iron") return false;   // kategoria "iron" (wyrob losowany z kategorii - WorkshopLaw.RandomItemPostfix daje lupe zamiast sztabki)
                    count += Math.Max(1, o.Item2);
                }
                int wpb = WoodPerBloom();
                if (wpb <= 0) return false;
                var st = w.Settlement; var town = st != null ? st.Town : null; var shelf = st != null ? st.ItemRoster : null; var wood = Wood();
                if (town == null || shelf == null || wood == null) { _dTownMineSkip++; return true; }
                int need = wpb * count;
                int price = Math.Max(0, town.MarketData.GetPrice(new EquipmentElement(wood)));
                if (shelf.GetItemNumber(wood) < need || w.Capital < need * price) { _dTownMineSkip++; return true; }
                if (_cycleWired) { _pw = w; _pNeed = need; _pPrice = price; _pCount = count; return false; }
                BuyTownMineWood(w, st, town, shelf, wood, need, price, count);
                return false;
            }
            catch (Exception e) { Stumble("TownMineBlocked", e); return false; }
        }

        /// <summary>Postfiks WorkshopsCampaignBehavior.TickOneProductionCycleForNotableWorkshop: cykl kopalni BK zarezerwowany w TownMineBlocked dal lupy
        /// (__result) - dopiero teraz drewno z polki i zaplata; cykl odrzucony przez gre - nic (licznik).</summary>
        public static void CyclePostfix(Workshop __1, bool __result)
        {
            var w = _pw; _pw = null;
            if (w == null || !ReferenceEquals(w, __1)) return;
            if (!__result) { _dTownMineRej++; return; }
            try
            {
                var st = w.Settlement; var town = st != null ? st.Town : null; var shelf = st != null ? st.ItemRoster : null; var wood = Wood();
                if (town == null || shelf == null || wood == null) return;
                int need = Math.Min(_pNeed, Math.Max(0, shelf.GetItemNumber(wood)));
                int wpb = WoodPerBloom();
                if (need < _pNeed && wpb > 0) _dOver += (_pNeed - need + wpb - 1) / wpb;   // nie powinno sie zdarzyc: w cyklu nikt nie zdejmuje drewna
                if (need > 0) BuyTownMineWood(w, st, town, shelf, wood, need, _pPrice, _pCount);
            }
            catch (Exception e) { Stumble("CyclePostfix", e); }
        }

        private static void BuyTownMineWood(Workshop w, Settlement st, Town town, ItemRoster shelf, ItemObject wood, int need, int price, int count)
        {
            var gf = GoodsLedger.Begin(GoodsLedger.FBloom, town);
            try
            {
                shelf.AddToCounts(wood, -need);
                if (price > 0)
                {
                    w.ChangeGold(-need * price);
                    town.ChangeGold(need * price);
                }
            }
            finally { GoodsLedger.End(gf); }
            if (price > 0)
            {
                try { MoneyLedger.Note(MoneyLedger.NShop, st, need * price); } catch { }
                try { TownFolk.Sale(st, need * price, TownFolk.MixGoods); } catch { }
            }
            OreLedger.NoteBloomeryWood(wood, need);
            NoteShelf(town, need);   // F4a (R1-7b): popyt drewna miasta - kopalnia BK pali z jego polki
            _dTownMine += count; _dTownMineWood += need;
        }

        // ------------------------------------------------------------ linia (OreLedger)
        /// <summary>Wstawka linii "Zelazo (lupy) (214)" po "wsie dopisaly a lup (...)": lupy z tickow kopalni (kontrola z OnItemProduced), drewno dymarek,
        /// kopalnia bez wegla, kopalnie BK w miastach.</summary>
        internal static string IronSegment(int villagesProduced)
        {
            var inv = CultureInfo.InvariantCulture;
            int bank = 0, want = 0, mines = 0, empty = 0;
            foreach (var kv in _mines) { mines++; bank += kv.Value.Bank; want += kv.Value.Want; if (kv.Value.Want > 0 && kv.Value.Bank < kv.Value.Want) empty++; }
            var sb = new StringBuilder();
            sb.Append(" | dymarki (E6/R1): lupy z tickow kopalni ").Append(_dBlooms).Append(" (kontrola wsie - ticki = ").Append(villagesProduced - _dBlooms)
              .Append("; kopalni z tickiem ").Append(_dTicked).Append(", znanych ").Append(mines)
              .Append("); wegiel dymarek: spalone ").Append(_dBurnt).Append(" ladunkow drewna = ").Append(WoodPerBloom()).Append(" x lupy (roznica ").Append(_dBurnt - WoodPerBloom() * _dBlooms)
              .Append("), las okregu ").Append(_dWoods).Append(", targ ").Append(_dBought).Append(" za ").Append(_dGold).Append(" zl (brak na polce ").Append(_dShortShelf)
              .Append(", brak zlota wsi ").Append(_dShortGold).Append(", bez miasta handlowego ").Append(_dNoMarket).Append(")")
              .Append("; kopalnia bez wegla ").Append(_dUnwon.ToString("0.#", inv)).Append(" lup (model ponad drewno w dymarce)")
              .Append(", lupy bez drewna ").Append(_dOver)
              .Append("; w dymarkach ").Append(bank).Append(" ladunkow przy potrzebie doby ").Append(want).Append(" (kopalni ponizej potrzeby ").Append(empty).Append(")")
              .Append("; kopalnie BK w miastach: lupy ").Append(_dTownMine).Append(", drewno ").Append(_dTownMineWood).Append(", bez drewna/kapitalu ").Append(_dTownMineSkip).Append(", cykl odrzucony przez gre ").Append(_dTownMineRej);
            // F4b: wstawka rachunku PRZED wstawka kopaln (koniec tamtej czyta narzedzie progow); wlasny try - jej wyjatek nie gasi reszty linii
            try { CartSegment(sb); CoalSegment(sb); MineSegment(sb); } catch (Exception e) { Stumble("IronSegment(F4a)", e); }
            sb.Append("; potkniecia dzis ").Append(_stumblesDay).Append(" (od wczytania ").Append(_stumbles).Append(") |");
            return sb.ToString();
        }

        /// <summary>F4a: wstawka "wozy dymarek (F4a)" - zakupy wozem z dalszych miast, przyjazdy, w drodze, niedobor doby bez wozu i kontrola
        /// "w drodze wczoraj + kupione - dojechalo - zawrocone - bez celu = w drodze dzis" (reszta 0).</summary>
        private static void CartSegment(StringBuilder sb)
        {
            var inv = CultureInfo.InvariantCulture;
            var s = Settings.Current;
            float range = s != null ? s.BloomeryCartRange : 0f;
            long now = InTransit();
            sb.Append("; wozy dymarek (F4a): ").Append(range > 0f ? "CZYNNE zasieg " + range.ToString("0", inv) : "WYLACZONE (Bloomery Cart Range 0 - tylko dowoz oplaconych)")
              .Append(", wozem kupione ").Append(_cBought).Append(" ladunkow za ").Append(_cGold).Append(" zl w ").Append(_cCarts).Append(" wozach (srednio ")
              .Append(_cCarts > 0 ? ((double)_cDaysSum / _cCarts).ToString("0.0", inv) : "0").Append(" dob drogi; miast z nadwyzka ").Append(_cSources < 0 ? "-" : _cSources.ToString(inv))
              .Append("), dojechalo ").Append(_cArr).Append(" w ").Append(_cArrCarts).Append(" wozach (srednio ")
              .Append(_cArrCarts > 0 ? ((double)_cArrDays / _cArrCarts).ToString("0.0", inv) : "0").Append(" dob od zakupu), czeka ").Append(_cWait)
              .Append(" wozow, zawrocone ").Append(_cBackPcs).Append(" w ").Append(_cBack).Append(" wozach (zwrot ").Append(_cRefund).Append(" zl), w drodze wozem ").Append(now)
              .Append(" w ").Append(_carts.Count).Append(" wozach; niedobor doby bez wozu: bez nadwyzki w zasiegu ").Append(_cNoSurplus).Append(", pusta kiesa wsi ").Append(_cNoGold);
            if (_cPrev >= 0)
                sb.Append("; kontrola wozow: w drodze wczoraj ").Append(_cPrev).Append(" + kupione - dojechalo - zawrocone - bez celu ").Append(_cLost)
                  .Append(" - w drodze dzis = reszta ").Append(_cPrev + _cBought - _cArr - _cBackPcs - _cLost - now);
            else sb.Append("; kontrola wozow od nastepnej doby");
            sb.Append("; czas wozow ").Append((_cTicks * 1000.0 / Stopwatch.Frequency).ToString("0.0", inv)).Append(" ms");
        }

        /// <summary>F4a (R1-8, R1-7b): 5 kopaln z najwieksza liczba lup niewytopionych tej doby (wies [miasto handlowe], drewno w dymarce / potrzeba doby,
        /// drewno na miejscu srednio, w drodze wozem, zloto wsi) i miasta handlowe kopaln ponizej potrzeby - tylko log.</summary>
        private static void MineSegment(StringBuilder sb)
        {
            var inv = CultureInfo.InvariantCulture;
            var top = new List<KeyValuePair<Village, Mine>>();
            var low = new List<string>();
            foreach (var kv in _mines)
            {
                if (kv.Key == null) continue;
                if (kv.Value.Unwon > 0.05f) top.Add(kv);
                if (kv.Value.Want > 0 && kv.Value.Bank < kv.Value.Want)
                {
                    var mk = kv.Key.TradeBound;
                    string nm = mk != null && mk.Name != null ? mk.Name.ToString() : "bez miasta";
                    if (!low.Contains(nm)) low.Add(nm);
                }
            }
            top.Sort((a, b) => b.Value.Unwon.CompareTo(a.Value.Unwon));
            sb.Append("; najbardziej bez wegla: ");
            if (top.Count == 0) sb.Append("-");
            for (int i = 0; i < top.Count && i < 5; i++)
            {
                var v = top[i].Key; var m = top[i].Value;
                int road = 0; foreach (var c in _carts) if (ReferenceEquals(c.Mine, v)) road += c.N;
                var mk = v.TradeBound;
                if (i > 0) sb.Append(", ");
                sb.Append(v.Name != null ? v.Name.ToString() : "?").Append(" [").Append(mk != null && mk.Name != null ? mk.Name.ToString() : "bez miasta").Append("] ")
                  .Append(m.Unwon.ToString("0.#", inv)).Append(" lup (dymarka ").Append(m.Bank).Append("/").Append(m.Want).Append(", na miejscu srednio ")
                  .Append(m.LocAvg.ToString("0", inv)).Append(", wozem w drodze ").Append(road).Append(", zloto wsi ").Append(v.Gold).Append(")");
            }
            low.Sort(StringComparer.Ordinal);
            sb.Append("; miasta handlowe kopaln ponizej potrzeby: ").Append(low.Count == 0 ? "-" : string.Join(", ", low));
        }

        /// <summary>Wstawka linii "Drewno:" w zuzyciu: drewno dymarek przy kopalniach (targ = ujscie zapasu; las okregu nie wchodzi do zapasu).</summary>
        internal static string WoodSegment(int fromShelves)
        {
            return ", dymarki przy kopalniach (214) " + fromShelves + " (las okregu poza zapasem " + _dWoods + ")";
        }

        /// <summary>Wstawka linii "Zelazo (lupy) (214)": przetop - zlom wraca w lupach (ArmsScrap, Drill przez WorkshopLaw.Needs).</summary>
        internal static string SmeltSegment(int scrap, int drill)
        {
            return "; przetop: zlom (174) " + scrap + ", zlom z cwiczen " + drill + ", wraki - metal napraw kowali (135), kuznia gracza - w \"bez wyjasnienia\"";
        }

        internal static void NewDay()
        {
            _dBlooms = 0; _dBurnt = 0; _dWoods = 0; _dBought = 0; _dGold = 0; _dShortShelf = 0; _dShortGold = 0; _dNoMarket = 0; _dTicked = 0; _dOver = 0;
            _dTownMine = 0; _dTownMineWood = 0; _dTownMineSkip = 0; _dTownMineRej = 0; _dUnwon = 0f; _stumblesDay = 0;
            // F4a: liczniki wozow i lupy niewytopione per kopalnia; stan "w drodze" do kontroli nastepnej doby
            _cCarts = 0; _cBought = 0; _cGold = 0; _cDaysSum = 0; _cArrCarts = 0; _cArr = 0; _cArrDays = 0; _cWait = 0; _cBack = 0; _cBackPcs = 0; _cRefund = 0; _cLost = 0;
            _cNoSurplus = 0; _cNoGold = 0; _cTicks = 0;
            foreach (var m in _mines.Values) { m.Unwon = 0f; m.Asked = 0; m.Got = 0; m.NoGold = 0; m.Price = 0; }
            _cPrev = InTransit();
            // F4b: liczniki doby rachunku za drewno; stan "otwarty" do kontroli nastepnej doby
            _bReturns = 0; _bBackN = 0; _bHanded = 0; _bSettled = 0; _bBack = 0; _bAdded = 0; _bCut = 0; _bRefund = 0;
            _bPrev = BillsOpen();
        }

        // ------------------------------------------------------------ wpiecie
        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var tb = AccessTools.TypeByName("TaleWorlds.CampaignSystem.CampaignBehaviors.VillageGoodProductionCampaignBehavior");
                var tick = tb != null ? AccessTools.Method(tb, "TickGoodProduction", new[] { typeof(Village), typeof(bool) }) : null;
                if (tick == null) { Log.Info("Bloomery (E6/R1, 214): BRAK VillageGoodProductionCampaignBehavior.TickGoodProduction(Village, bool) - wytop przy kopalni NIEAKTYWNY (ruda jak dotad)."); return; }
                h.Patch(tick, prefix: new HarmonyMethod(typeof(Bloomery), nameof(TickPrefix)) { priority = Priority.First },
                              postfix: new HarmonyMethod(typeof(Bloomery), nameof(TickPostfix)),
                              finalizer: new HarmonyMethod(typeof(Bloomery), nameof(TickFinalizer)));
                _wired = true;
                // recenzja R1: zakup drewna kopalni BK w miescie po cyklu, ktory dal lupy (bez tego wpiecia - zakup w prefiksie jak dotad)
                try
                {
                    var wsb = AccessTools.TypeByName("TaleWorlds.CampaignSystem.CampaignBehaviors.WorkshopsCampaignBehavior");
                    var cyc = wsb != null ? AccessTools.Method(wsb, "TickOneProductionCycleForNotableWorkshop") : null;
                    if (cyc != null) { h.Patch(cyc, postfix: new HarmonyMethod(typeof(Bloomery), nameof(CyclePostfix))); _cycleWired = true; }
                }
                catch (Exception e) { Log.Error("Bloomery.ApplyAll(cykl)", e); }
                Log.Info("Bloomery (E6/R1, 214): tick produkcji wsi wpiety (prefiks Priority.First przed BK, postfiks, finalizer) - lupa i dymarka tylko w nowej kampanii przy kluczu Bloomery At Mine.");
            }
            catch (Exception e) { Log.Error("Bloomery.ApplyAll", e); }
        }
    }
}
