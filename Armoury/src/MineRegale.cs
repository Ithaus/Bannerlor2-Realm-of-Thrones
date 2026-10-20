using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// R3-09f (POMYSLY-35 pkt 30, noc 20/21.10; Jeff 20.10: "28 - nie, 29 - nie, pozostale tak") - REGALE GORNICZE: udzial pana w sprzedazy kruszcu.
    /// Zrodlo: audyt 12 D-13 (docs/audyt-2026-10-09/12-DOCHODY-RODOW.md:209, krytyka pkt 24) i 15 (lista "bez zmian" - regale do pomiaru): pan wsi gorniczej
    /// bral z utargu za rude ten sam podatek co ze zboza (BK 70% x VillageLordScale 0.5 + VillageLordExtraShare 0.1 = 45% przy zwyklym dekrecie); "Lannister >> inni"
    /// sie nie dzialo (dochod z ziemi na rod Westerlands 1 530 - mniej niz Polnoc 1 952 i Dolina 1 966). Historycznie korona albo pan brali 1/10-1/4 wartosci kruszcu
    /// (Kutna Hora - urbura 1/8 w Ius regale montanorum ok. 1300; stannaries Kornwalii, Bergzehnt 1/10).
    ///
    /// REGULA (klucz MineRegale, stawka MineRegaleShare = 1/8 - urbura z Kutnej Hory, srodek przedzialu audytu):
    ///  1. Sprzedaz wozu wsi (SellGoodsForTradeAction.ApplyInternal - miasto, a przy CastleMarket takze zamek): prefiks First zapisuje ladunek wozu i cene kazdej
    ///     pozycji w tej osadzie (Town.GetItemPrice - ta sama cena, od ktorej liczy gra), gdy woz wiezie kruszec (iron - lupa albo ruda, silver, gold_ore) albo ma juz
    ///     otwarte konto kursu; postfiks First (przed zwrotem nadplaty MarketCarts i zakupami wsi przez woz - te zmieniaja tylko zloto) liczy sprzedane sztuki z roznicy
    ///     ladunku: wartosc kruszcu i wartosc CALEGO sprzedanego ladunku po tych cenach - na konto wozu (kilka postojow przed powrotem - sumy; recenzja: sprzedaz
    ///     bez kruszcu, np. jedzenie w zamku przed miastem, wchodzi do wartosci ladunku, inaczej udzial kruszcu w utargu wychodzil za duzy).
    ///  2. Powrot wozu do wsi MACIERZYSTEJ (VillageTakings.EnterPostfix, po 112 i F4b, przed pracami okregu, wioska gracza i daninami DS): oddany utarg x udzial
    ///     kruszcu w sprzedanym ladunku = utarg za kruszec; minus rachunek za drewno dymarki pokryty w tym oknie (F4b: d = min(rachunek, utarg), ta sama kolejnosc co
    ///     podatek pana - pan bierze od tego, co zostalo po drewnie) = podstawa; regale = MineRegaleShare x podstawa. Placi czesc wsi z TEGO utargu: najwyzej przyrost
    ///     kiesy wsi w oknie (nigdy z oszczednosci sprzed powrotu). Zloto: kiesa wsi -> licznik podatku wsi (TradeTaxAccumulated - ten sam licznik, z ktorego pan
    ///     zbiera swoj podatek; majatki BK nie biora z regale udzialu - liczy je BK tylko od swojego podatku). Nic nie powstaje, nic nie znika.
    ///  3. ZWOLNIENIE ("Freiung" - pan zwalnial kopalnie w biedzie z dziesieciny): kopalnia z dymarka (Bloomery), ktora ma dlug u kupcow za drewno (f5-ruda) - nic;
    ///     inaczej regale tylko z kiesy ponad zapas na drewno dymarki (potrzeba ponad las okregu x cena drewna w miescie handlowym - ta sama miara co "kiesy kopaln
    ///     ponizej kosztu drewna doby" w linii zelaza): WoodDaysCredit = 2 doby, gdy kupcy moga dac drewno na kredyt (Bloomery Wood Credit - do 7 dob potrzeby, a przy
    ///     dlugu regale i tak 0), WoodDays = 7 dob bez kredytu. Recenzja: 7 dob przy kredycie zdejmowalo regale z prawie kazdej kopalni zelaza (Akirys: doba drewna
    ///     ok. 1 800 zl wobec 2-3 tys. zl z powrotu) - pomysl dzialalby tylko dla srebra. Kopalnie srebra i zlota bez dymarki - bez zapasu.
    /// DS (daniny wsi, DsVillage.DuesAtCart) biegnie PO regale i bierze dziesiecine od calego utargu, a udzial korony od czesci wsi netto (przyrost kiesy) -
    /// regale nie jest liczone drugi raz; czynsz staly i renta wsi liczone od kiesy, ktora juz nie ma regale. Kopalnie BK w miastach (polowa ceny rudy jako danina)
    /// to inna droga (warsztat miasta) - tu tylko wozy wsi. Wioska poboczna gracza (SideVillageFief.AtCart po r4-wioska - tylko licznik linii; naleznosc Due osobno) - bez zmian.
    /// KS: SplitToday i utarg wedlug wsi (alfa rynku, alfa netto, alfa z udzialu kiesy) widza czesc wsi pomniejszona o regale (VillageTakings.RegaleTaken); alfa Z PRAW
    /// DS (StartCalibration.VilAlphaFor -> DsVillage.BudgetShare) liczy udzial wsi wzorem z praw, wiec dostaje udzial regale w utargu wsi osobno (KsShareOfTakings:
    /// srednia 28 dob regale pobranego tej wsi, ta sama waga co utarg wsi w alfie okregu; recenzja: bez tego Plan dawal kopalniom koszyk ok. 30% za duzy).
    /// Zapis arm_regale (SaveText): "2|idWozu~kruszec~ladunek~doba|...|@idWsi~zl/d|#doby" - konta wozow w drodze (kruszec sprzedany, jeszcze nie w domu) i srednia
    /// regale wsi dla KS; wersja "1" (konta) czytana; brak klucza (stary zapis Jeffa) = pusto, woz bez konta oddaje utarg jak dotad. Konto bez sprzedazy 30 dob
    /// (woz wrocil bez zlota w sakwie albo okno 112 nie otwarte) - zamkniete w linii doby. Klucz NIE - nic sie nie zapisuje na konta i nic nie pobiera.
    /// Linia doby "Regale gornicze (R3-09f): dzien N" z kontrola z realnych zmian (kiesy wsi przed/po, liczniki wsi przed/po). Potkniecia liczone per miejsce.
    /// </summary>
    internal static class MineRegale
    {
        private const int WoodDays = 7;                 // zapas na drewno dymarki bez kredytu kupcow - jak sufit kredytu kupcow f5 (7 dob potrzeby)
        private const int WoodDaysCredit = 2;           // zapas przy kredycie kupcow f5 (reszte do nastepnego wozu kryje kredyt; przy dlugu regale 0)
        private const int ExpireDays = 30;              // konto wozu bez sprzedazy tyle dob - zamkniete (kurs skonczony bez rozliczenia)
        private const double ShareMax = 0.5;            // przyciecie stawki z MCM
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class Acc { public long Ore, All; public int Day = -1; }
        // id wozu -> wartosc kruszcu i calego sprzedanego ladunku od ostatniego powrotu (lock - sprzedaz wolana takze spoza naszego ticku)
        private static readonly Dictionary<string, Acc> _acc = new Dictionary<string, Acc>(StringComparer.Ordinal);
        private static readonly object Gate = new object();
        private static Campaign _camp;
        private static bool _fromImport;               // konta wczytane z zapisu, gdy Campaign.Current jeszcze nie bylo - pierwsza kampania je przejmuje

        // KS (recenzja, wazne): srednia 28 dob regale pobranego wedlug wsi (id osady -> zl/d, doby bez powrotu = 0) i pobrane od ostatniej doby; watek gry
        private static readonly Dictionary<string, double> _vEma = new Dictionary<string, double>(StringComparer.Ordinal);
        private static readonly Dictionary<string, long> _vDay = new Dictionary<string, long>(StringComparer.Ordinal);
        private static readonly List<string> _keyBuf = new List<string>();
        private static int _vN;

        internal sealed class Snap
        {
            public EquipmentElement[] El; public int[] N, P; public byte[] K;
        }

        // ------------------------------------------------------------ liczniki doby (linia)
        private static int _sN, _sNoOreN; private static long _sOre, _sAll, _sNoOreZl; private static readonly long[] _sPcs = new long[4];
        private static int _rN, _rFreeDebt, _rFreeWood, _rNoPart, _rForeign, _rCreditRes;
        private static long _rHanded, _rOre, _rBill, _rBase, _rDue, _rTaken, _rFreeDebtZl, _rFreeWoodZl, _rNoPartZl, _rPurseOut, _rTaxIn;
        // grupy: 0 - kopalnie z dymarka (zelazo), 1 - inne (srebro, zloto, ruda bez dymarki)
        private static readonly int[] _gN = new int[2];
        private static readonly long[] _gDue = new long[2], _gTaken = new long[2];
        private static readonly Dictionary<Clan, long> _rClan = new Dictionary<Clan, long>();
        private static readonly Dictionary<MBObjectBase, long> _rKing = new Dictionary<MBObjectBase, long>();   // krolestwo (albo rod bez krolestwa) wsi
        private static int _stum, _stumSess;
        private static readonly HashSet<string> _errOnce = new HashSet<string>(StringComparer.Ordinal);
        private static bool _hooked;
        // okno powrotu: rachunek za drewno przed F4b (Open -> AtCart, ten sam woz)
        private static VillageTakings.Entry _open; private static int _openBill;

        private static void ClearDay()
        {
            _sN = _sNoOreN = 0; _sOre = _sAll = _sNoOreZl = 0; Array.Clear(_sPcs, 0, _sPcs.Length);
            _rN = _rFreeDebt = _rFreeWood = _rNoPart = _rForeign = _rCreditRes = 0;
            _rHanded = _rOre = _rBill = _rBase = _rDue = _rTaken = _rFreeDebtZl = _rFreeWoodZl = _rNoPartZl = _rPurseOut = _rTaxIn = 0;
            Array.Clear(_gN, 0, 2); Array.Clear(_gDue, 0, 2); Array.Clear(_gTaken, 0, 2);
            _rClan.Clear(); _rKing.Clear(); _stum = 0;
        }

        private static void ClearSession()
        {
            lock (Gate) _acc.Clear();
            _vEma.Clear(); _vDay.Clear(); _vN = 0;
            _open = null; _openBill = 0; _stumSess = 0; _errOnce.Clear();
            ClearDay();
        }

        /// <summary>Nowa kampania albo wczytanie (inny obiekt Campaign): konta wozow od zera. Bez wpiecia w konstruktor ArmouryBehavior (scalanie galezi).</summary>
        private static void EnsureCampaign()
        {
            var c = Campaign.Current;
            if (ReferenceEquals(c, _camp)) return;
            if (_fromImport && _camp == null) { _camp = c; _fromImport = false; return; }
            _camp = c; _fromImport = false;
            ClearSession();
        }

        private static void Stumble(string where, Exception e)
        {
            _stum++; _stumSess++;
            try { if (_errOnce.Add(where)) Log.Error("MineRegale." + where, e); } catch { }
        }

        /// <summary>Klucz Mine Regale i okno powrotu wozu czynne (latka VillageTakings wpieta i Village Takings Whole) - inaczej konta sie nie zbieraja.</summary>
        internal static bool On { get { var s = Settings.Current; return s != null && s.MineRegale && VillageTakings.CartHookLive; } }

        private static double Share { get { var s = Settings.Current; double r = s != null ? s.MineRegaleShare : 0.0; return r > 0 ? Math.Min(ShareMax, r) : 0.0; } }

        /// <summary>1 iron (lupa przy Bloomery albo ruda), 2 silver, 3 gold_ore, 0 - nie kruszec.</summary>
        private static byte KindOf(ItemObject it)
        {
            if (it == null) return 0;
            string id = it.StringId;
            if (id == "iron") return 1;
            if (id == "silver") return 2;
            if (id == "gold_ore") return 3;
            return 0;
        }

        // ------------------------------------------------------------ 1. sprzedaz wozu w osadzie
        /// <summary>SellGoodsForTradeAction.ApplyInternal(Settlement, MobileParty, detail) - prefiks First. Tylko odczyt; migawka dla wozu wsi z kruszcem w jukach
        /// albo z otwartym kontem kursu (sprzedaz bez kruszcu na kolejnym postoju).</summary>
        public static void SellPrefix(Settlement __0, MobileParty __1, out Snap __state)
        {
            __state = null;
            try
            {
                if (__1 == null || __0 == null || !__1.IsVillager || !On) return;
                var town = __0.Town;
                if (town == null) return;
                var hs = __1.HomeSettlement;
                if (hs == null || hs.Village == null) return;
                var r = __1.ItemRoster;
                int n = r.Count;
                if (n <= 0) return;
                bool any = false;
                for (int i = 0; i < n && !any; i++) if (KindOf(r.GetElementCopyAtIndex(i).EquipmentElement.Item) != 0) any = true;
                if (!any)
                {
                    string pid = __1.StringId;
                    if (string.IsNullOrEmpty(pid)) return;
                    lock (Gate) any = _acc.ContainsKey(pid);
                    if (!any) return;
                }
                var s = new Snap { El = new EquipmentElement[n], N = new int[n], P = new int[n], K = new byte[n] };
                for (int i = 0; i < n; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    s.El[i] = el.EquipmentElement; s.N[i] = el.Amount; s.K[i] = KindOf(it);
                    int p = 0;
                    try { p = town.GetItemPrice(el.EquipmentElement, __1, true); } catch { p = 0; }
                    if (p <= 0 && it != null) p = Math.Max(0, it.Value);
                    s.P[i] = p;
                }
                __state = s;
            }
            catch (Exception e) { __state = null; Stumble("SellPrefix", e); }
        }

        /// <summary>Postfiks First (przed MarketCarts.SellPostfix: zwrot nadplaty i zakupy wsi przez woz zmieniaja zloto, nie sprzedane sztuki): sprzedane = ladunek
        /// przed - po; wartosc kruszcu i calego sprzedanego ladunku po cenach z prefiksu na konto wozu. Sprzedaz bez kruszcu (woz z kruszcem w jukach albo z kontem) -
        /// tylko wartosc ladunku (konto z kruszcem 0 zamyka sie przy powrocie bez regale).</summary>
        public static void SellPostfix(MobileParty __1, Snap __state)
        {
            if (__state == null || __1 == null) return;
            try
            {
                EnsureCampaign();
                var r = __1.ItemRoster;
                long ore = 0, all = 0;
                long p1 = 0, p2 = 0, p3 = 0;
                for (int i = 0; i < __state.El.Length; i++)
                {
                    int idx = r.FindIndexOfElement(__state.El[i]);
                    int now = idx >= 0 ? r.GetElementNumber(idx) : 0;
                    int sold = __state.N[i] - now;
                    if (sold <= 0) continue;
                    long v = (long)sold * __state.P[i];
                    all += v;
                    byte k = __state.K[i];
                    if (k != 0) { ore += v; if (k == 1) p1 += sold; else if (k == 2) p2 += sold; else p3 += sold; }
                }
                if (all <= 0) return;
                string id = __1.StringId;
                if (string.IsNullOrEmpty(id)) return;
                int today = (int)CampaignTime.Now.ToDays;
                lock (Gate)
                {
                    Acc a;
                    if (!_acc.TryGetValue(id, out a)) { a = new Acc(); _acc[id] = a; }
                    a.Ore += ore; a.All += all; a.Day = today;
                }
                if (ore > 0) { _sN++; _sOre += ore; _sAll += all; _sPcs[1] += p1; _sPcs[2] += p2; _sPcs[3] += p3; }
                else { _sNoOreN++; _sNoOreZl += all; }
            }
            catch (Exception e) { Stumble("SellPostfix", e); }
        }

        // ------------------------------------------------------------ 2. powrot wozu do wsi
        /// <summary>VillageTakings.EnterPostfix PRZED F4b (CoalFirst): rachunek za drewno kopalni (bez licznikow linii zelaza) - d = min(rachunek, utarg) liczy AtCart.</summary>
        internal static void Open(VillageTakings.Entry st)
        {
            _open = null; _openBill = 0;
            try
            {
                if (st == null || st.Here != null || st.Home == null || !On) return;
                _open = st;
                _openBill = Bloomery.RegaleBill(st.Home);
            }
            catch (Exception e) { _open = null; _openBill = 0; Stumble("Open", e); }
        }

        /// <summary>VillageTakings.EnterPostfix PO F4b, przed pracami okregu, wioska gracza i daninami DS: regale z czesci wsi utargu za kruszec (kiesa wsi -> licznik pana).
        /// Woz w obcej wsi - konto zamkniete bez regale (BK rozlicza podatek we wsi wejscia, DS i F4b tez pomijaja taki powrot). Wlasny try - nie rzuca.</summary>
        internal static void AtCart(VillageTakings.Entry st)
        {
            int bill = ReferenceEquals(_open, st) ? _openBill : 0;
            _open = null; _openBill = 0;
            try
            {
                if (st == null || st.Party == null || st.Home == null) return;
                EnsureCampaign();
                string id = st.Party.StringId;
                if (string.IsNullOrEmpty(id)) return;
                Acc a;
                lock (Gate)
                {
                    if (!_acc.TryGetValue(id, out a)) return;
                    _acc.Remove(id);
                }
                if (!On) return;
                int handed = st.Gold - st.Party.PartyTradeGold;
                if (handed <= 0 || a.All <= 0 || a.Ore <= 0) return;
                if (st.Here != null) { _rForeign++; return; }
                var home = st.Home;
                int g = Bloomery.RegaleIsMine(home) ? 0 : 1;
                double sh = Math.Min(1.0, (double)a.Ore / a.All);
                long oreGold = (long)Math.Floor(handed * sh);
                long d = Math.Min((long)Math.Max(0, bill), (long)handed);
                long baseZl = Math.Max(0L, oreGold - d);
                long due = (long)Math.Floor(baseZl * Share);
                _rN++; _rHanded += handed; _rOre += oreGold; _rBill += Math.Min(d, oreGold); _rBase += baseZl; _rDue += due;
                _gN[g]++; _gDue[g] += due;
                if (due <= 0) return;
                long taken = TakeDue(st, home, due);
                _gTaken[g] += taken;
            }
            catch (Exception e) { Stumble("AtCart", e); }
        }

        /// <summary>Pobranie naleznego `due` z czesci wsi tego utargu; zwraca, ile weszlo na licznik pana. Kazdy zl naleznego trafia do pobranych albo do jednego
        /// rodzaju zwolnienia (kontrola "nalezne = pobrane + zwolnione"); kontrola przelewu z realnych zmian kiesy i licznika wsi.</summary>
        private static long TakeDue(VillageTakings.Entry st, Village home, long due)
        {
            long grown = (long)home.Gold - st.HomeGold;          // czesc wsi tego utargu w kiesie (BK + 112 + zwrot F4b)
            if (grown <= 0) { _rNoPart++; _rNoPartZl += due; return 0; }
            long reserve; bool credit;
            bool owes = Bloomery.RegaleWoodGuard(home, WoodDays, WoodDaysCredit, out reserve, out credit);
            if (owes) { _rFreeDebt++; _rFreeDebtZl += due; return 0; }
            if (credit && reserve > 0) _rCreditRes++;
            long room = Math.Min(grown, Math.Max(0L, (long)home.Gold - Math.Max(0L, reserve)));
            long take = Math.Min(due, Math.Min(room, (long)int.MaxValue));
            if (take < due)
            {
                long cut = due - Math.Max(0L, take);
                if (room < grown) { _rFreeWood++; _rFreeWoodZl += cut; } else { _rNoPart++; _rNoPartZl += cut; }
            }
            if (take <= 0) return 0;
            int had = home.Gold;
            home.ChangeGold(-(int)take);
            long outZl = (long)had - home.Gold;                  // zdjete naprawde z kiesy
            long got = Math.Max(0L, Math.Min(take, outZl));
            int taxHad = home.TradeTaxAccumulated;
            if (got > 0) home.TradeTaxAccumulated = taxHad + (int)got;
            long taxIn = (long)home.TradeTaxAccumulated - taxHad;  // weszlo naprawde na licznik pana
            _rPurseOut += outZl; _rTaxIn += taxIn;
            long taken = Math.Max(0L, Math.Min(got, taxIn));
            if (taken < take) { _rNoPart++; _rNoPartZl += take - taken; }   // kiesa albo licznik przyjely mniej (cudza latka) - reszta naleznego jak "ponad czesc wsi"
            if (taken <= 0) return 0;
            _rTaken += taken;
            VillageTakings.RegaleTaken(home, taken);                // SplitToday i utarg wedlug wsi: czesc wsi utargu doby mniejsza o regale
            try
            {
                var hs = home.Settlement;
                if (hs != null)
                {
                    string sid = hs.StringId;
                    if (!string.IsNullOrEmpty(sid)) { long x; _vDay.TryGetValue(sid, out x); _vDay[sid] = x + taken; }
                    var c = hs.OwnerClan;
                    if (c != null) { long x; _rClan.TryGetValue(c, out x); _rClan[c] = x + taken; }
                    var f = hs.MapFaction as MBObjectBase;
                    if (f != null) { long x; _rKing.TryGetValue(f, out x); _rKing[f] = x + taken; }
                }
            }
            catch (Exception e) { Stumble("AtCart(rod)", e); }
            return taken;
        }

        // ------------------------------------------------------------ KS: alfa z praw DS
        /// <summary>StartCalibration.VilAlphaFor (galaz alfy z praw DS, watek glowny BuildInput): udzial regale w oddanym utargu wsi = srednia 28 dob regale pobranego
        /// tej wsi / srednia 28 dob jej utargu (`handedPerDay`, ta sama waga). 0 - klucz wylaczony, brak regale albo brak utargu. Nie rzuca.</summary>
        internal static double KsShareOfTakings(Village v, double handedPerDay)
        {
            try
            {
                if (v == null || v.Settlement == null || !(handedPerDay > 0) || !On) return 0.0;
                EnsureCampaign();
                double e;
                if (!_vEma.TryGetValue(v.Settlement.StringId ?? "", out e) || !(e > 0)) return 0.0;
                return Math.Min(1.0, e / handedPerDay);
            }
            catch (Exception ex) { Stumble("KsShareOfTakings", ex); return 0.0; }
        }

        /// <summary>Raz na dobe (Daily): srednia 28 dob regale wedlug wsi z pobranego od ostatniej doby (ta sama waga 1/min(28, doby) co utarg wsi KS).</summary>
        private static void RollVillages()
        {
            _vN++;
            double w = 1.0 / Math.Min(28, _vN);
            _keyBuf.Clear();
            foreach (var k in _vEma.Keys) _keyBuf.Add(k);
            foreach (var k in _vDay.Keys) if (!_vEma.ContainsKey(k)) _keyBuf.Add(k);
            for (int i = 0; i < _keyBuf.Count; i++)
            {
                string k = _keyBuf[i];
                double e; _vEma.TryGetValue(k, out e);
                long d; _vDay.TryGetValue(k, out d);
                e += (d - e) * w;
                if (d <= 0 && e < 0.05) _vEma.Remove(k); else _vEma[k] = e;
            }
            _keyBuf.Clear();
            _vDay.Clear();
        }

        // ------------------------------------------------------------ zapis (arm_regale)
        internal static string Export()
        {
            try
            {
                EnsureCampaign();
                var sb = new StringBuilder(256);
                sb.Append('2');
                lock (Gate)
                    foreach (var kv in _acc)
                    {
                        if (kv.Value == null || kv.Value.Ore < 0 || kv.Value.All <= 0 || !SafeId(kv.Key)) continue;
                        sb.Append('|').Append(kv.Key).Append('~').Append(kv.Value.Ore.ToString(Inv)).Append('~').Append(kv.Value.All.ToString(Inv)).Append('~').Append(kv.Value.Day.ToString(Inv));
                    }
                foreach (var kv in _vEma)
                {
                    if (!(kv.Value > 0) || !SafeId(kv.Key)) continue;
                    sb.Append("|@").Append(kv.Key).Append('~').Append(kv.Value.ToString("0.###", Inv));
                }
                sb.Append("|#").Append(_vN.ToString(Inv));
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return null; }
        }

        private static bool SafeId(string id) { return !string.IsNullOrEmpty(id) && id.IndexOf('|') < 0 && id.IndexOf('~') < 0 && id[0] != '@' && id[0] != '#'; }

        internal static void Import(string s)
        {
            // wczytanie = nowa sesja kampanii: konta od zera (bez wpiecia w konstruktor ArmouryBehavior), potem konta z zapisu
            ClearSession();
            _camp = Campaign.Current;
            _fromImport = _camp == null;
            if (string.IsNullOrEmpty(s)) return;
            try
            {
                var parts = s.Split('|');
                if (parts.Length == 0 || (parts[0] != "1" && parts[0] != "2")) return;     // nieznana wersja - pusto (woz bez konta oddaje utarg jak dotad)
                lock (Gate)
                    for (int i = 1; i < parts.Length; i++)
                    {
                        string p = parts[i];
                        if (string.IsNullOrEmpty(p)) continue;
                        if (p[0] == '#') { int n; if (int.TryParse(p.Substring(1), NumberStyles.Integer, Inv, out n) && n >= 0) _vN = n; continue; }
                        var f = p.Split('~');
                        if (p[0] == '@')
                        {
                            double e;
                            if (f.Length >= 2 && f[0].Length > 1 && double.TryParse(f[1], NumberStyles.Float, Inv, out e) && e > 0 && e < 1e9) _vEma[f[0].Substring(1)] = e;
                            continue;
                        }
                        if (f.Length < 3 || string.IsNullOrEmpty(f[0])) continue;
                        long o, a; int d = -1;
                        if (!long.TryParse(f[1], NumberStyles.Integer, Inv, out o) || !long.TryParse(f[2], NumberStyles.Integer, Inv, out a) || o < 0 || a <= 0) continue;
                        if (f.Length > 3 && !int.TryParse(f[3], NumberStyles.Integer, Inv, out d)) d = -1;
                        _acc[f[0]] = new Acc { Ore = o, All = a, Day = d };
                    }
            }
            catch (Exception e) { Stumble("Import", e); }
        }

        // ------------------------------------------------------------ latki
        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(SellGoodsForTradeAction), "ApplyInternal");
                var ps = m != null ? m.GetParameters() : null;
                if (ps == null || ps.Length < 2 || ps[0].ParameterType != typeof(Settlement) || ps[1].ParameterType != typeof(MobileParty))
                {
                    Log.Info("Regale gornicze (R3-09f): BRAK SellGoodsForTradeAction.ApplyInternal(Settlement, MobileParty, ..) - regale nieczynne (utarg wsi jak dotad).");
                    return;
                }
                h.Patch(m, prefix: new HarmonyMethod(typeof(MineRegale), nameof(SellPrefix)) { priority = Priority.First },
                           postfix: new HarmonyMethod(typeof(MineRegale), nameof(SellPostfix)) { priority = Priority.First });
                _hooked = true;
                var s = Settings.Current;
                Log.Info("Regale gornicze (R3-09f): latka wpieta (SellGoodsForTradeAction.ApplyInternal - prefiks First / postfiks First: ladunek wozow wsi z kruszcem; rozliczenie przy powrocie wozu - VillageTakings.EnterPostfix po F4b); klucz Mine Regale "
                         + (s != null && s.MineRegale ? "WLACZONY" : "wylaczony") + ", stawka " + Share.ToString("0.000", Inv) + " wartosci kruszcu (po rachunku za drewno), zwolnienie: dlug za drewno albo kiesa ponizej zapasu drewna dymarki ("
                         + WoodDaysCredit + " doby przy kredycie kupcow, " + WoodDays + " dob bez)"
                         + (s != null && !s.VillageTakingsWhole ? "; UWAGA: Village Takings Whole wylaczone - bez okna powrotu wozu regale nieczynne" : "") + ".");
            }
            catch (Exception e) { Log.Error("MineRegale.ApplyAll", e); }
        }

        private static string Pct(long part, long whole) { return whole > 0 ? (100.0 * part / whole).ToString("0", Inv) + "%" : "-"; }

        private static string NameOf(MBObjectBase o)
        {
            try
            {
                var c = o as Clan; if (c != null) return c.Name != null ? c.Name.ToString() : c.StringId;
                var k = o as Kingdom; if (k != null) return k.Name != null ? k.Name.ToString() : k.StringId;
                return o != null ? o.StringId : "?";
            }
            catch { return "?"; }
        }

        // ------------------------------------------------------------ raz na dobe
        /// <summary>ArmouryBehavior.OnDailyTick (po VillageTakings.Daily i DsVillage.Daily): linia doby, srednia regale wsi dla KS i zerowanie licznikow; konta wozow,
        /// ktorych juz nie ma na mapie (woz rozbity albo rozwiazany - jego sakwa poszla droga 112 / 3.2), i konta bez sprzedazy ExpireDays dob - zamkniete.</summary>
        internal static void Daily()
        {
            try
            {
                if (Campaign.Current == null) return;
                EnsureCampaign();
                var s = Settings.Current;
                int today = (int)CampaignTime.Now.ToDays;
                int day = today - 1;
                int open = 0, gone = 0, expired = 0; long openOre = 0;
                lock (Gate)
                {
                    if (_acc.Count > 0)
                    {
                        var live = new HashSet<string>(StringComparer.Ordinal);
                        foreach (var p in MobileParty.AllVillagerParties) if (p != null && p.IsActive && p.StringId != null) live.Add(p.StringId);
                        List<string> drop = null;
                        foreach (var kv in _acc)
                        {
                            bool old = false;
                            if (kv.Value.Day < 0) kv.Value.Day = today;      // konto z zapisu bez doby - zegar od dzis
                            else old = today - kv.Value.Day > ExpireDays;
                            if (!live.Contains(kv.Key)) { if (drop == null) drop = new List<string>(); drop.Add(kv.Key); gone++; }
                            else if (old) { if (drop == null) drop = new List<string>(); drop.Add(kv.Key); expired++; }
                        }
                        if (drop != null) foreach (var k in drop) _acc.Remove(k);
                    }
                    open = _acc.Count;
                    foreach (var kv in _acc) openOre += kv.Value.Ore;
                }
                RollVillages();
                int ksN = 0; double ksZl = 0;
                foreach (var kv in _vEma) { ksN++; ksZl += kv.Value; }
                var sb = new StringBuilder(1024);
                sb.Append("Regale gornicze (R3-09f): dzien ").Append(day).Append(" | ");
                if (!_hooked) sb.Append("latka sprzedazy nie jest wpieta - nieczynne");
                else if (s == null || !s.MineRegale) sb.Append("WYLACZONE w ustawieniach (Mine Regale) - pan bierze z kruszcu tyle co ze zboza");
                else if (!VillageTakings.CartHookLive) sb.Append("NIECZYNNE - brak okna powrotu wozu (Village Takings Whole wylaczone albo latka powrotu wozu nie wpieta)");
                else sb.Append("CZYNNE, stawka ").Append(Share.ToString("0.000", Inv)).Append(" wartosci kruszcu po rachunku za drewno");
                sb.Append(" | sprzedaze wozow z kruszcem ").Append(_sN).Append(": zelazo ").Append(_sPcs[1]).Append(", srebro ").Append(_sPcs[2]).Append(", zloto ").Append(_sPcs[3])
                  .Append(" sztuk, kruszec ").Append(_sOre).Append(" zl z ladunku ").Append(_sAll).Append(" zl (ceny osady sprzedazy); sprzedaze bez kruszcu w kursie z kruszcem ")
                  .Append(_sNoOreN).Append(" (").Append(_sNoOreZl).Append(" zl - w wartosci ladunku)");
                sb.Append(" | powroty z kruszcem ").Append(_rN).Append(": utarg ").Append(_rHanded).Append(" zl, z tego za kruszec ").Append(_rOre).Append(", rachunek za drewno (F4b) ")
                  .Append(_rBill).Append(", podstawa ").Append(_rBase).Append(", regale nalezne ").Append(_rDue).Append(", pobrane ").Append(_rTaken)
                  .Append(" (kiesa wsi -> licznik pana); zwolnione: dlug za drewno ").Append(_rFreeDebt).Append(" (").Append(_rFreeDebtZl).Append(" zl), zapas na drewno (")
                  .Append(WoodDaysCredit).Append(" doby przy kredycie kupcow - powrotow ").Append(_rCreditRes).Append(", ").Append(WoodDays).Append(" dob bez) ").Append(_rFreeWood)
                  .Append(" (").Append(_rFreeWoodZl).Append(" zl), ponad czesc wsi z utargu ").Append(_rNoPart).Append(" (").Append(_rNoPartZl).Append(" zl); woz w obcej wsi ").Append(_rForeign);
                sb.Append(" | wedlug kopaln: z dymarka (zelazo) powrotow ").Append(_gN[0]).Append(", nalezne ").Append(_gDue[0]).Append(", pobrane ").Append(_gTaken[0])
                  .Append(", zwolnione ").Append(_gDue[0] - _gTaken[0]).Append(" (").Append(Pct(_gDue[0] - _gTaken[0], _gDue[0])).Append("); inne (srebro, zloto, ruda bez dymarki) powrotow ")
                  .Append(_gN[1]).Append(", nalezne ").Append(_gDue[1]).Append(", pobrane ").Append(_gTaken[1]).Append(", zwolnione ").Append(_gDue[1] - _gTaken[1])
                  .Append(" (").Append(Pct(_gDue[1] - _gTaken[1], _gDue[1])).Append(")");
                sb.Append(" | kontrola: kiesy wsi -").Append(_rPurseOut).Append(", liczniki panow +").Append(_rTaxIn).Append(", reszta ").Append(_rTaxIn - _rPurseOut)
                  .Append("; nalezne = pobrane + zwolnione: reszta ").Append(_rDue - _rTaken - _rFreeDebtZl - _rFreeWoodZl - _rNoPartZl);
                AppendTop(sb, " | najwiecej (rody): ", _rClan);
                AppendTop(sb, " | wedlug krolestw: ", _rKing);
                sb.Append(" | KS (alfa z praw DS): wsi z regale w sredniej 28 dob ").Append(ksN).Append(", razem ").Append(ksZl.ToString("0", Inv)).Append(" zl/d");
                sb.Append(" | konta wozow w drodze ").Append(open).Append(" (kruszec ").Append(openOre).Append(" zl), zamkniete bez wozu ").Append(gone)
                  .Append(", przeterminowane (").Append(ExpireDays).Append(" dob bez sprzedazy) ").Append(expired);
                if (_stum > 0) sb.Append(" | potkniecia ").Append(_stum).Append(" (sesja ").Append(_stumSess).Append(", pierwsze z kazdego miejsca w logu)");
                sb.Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Log.Error("MineRegale.Daily", e); }
            finally { ClearDay(); }
        }

        private static void AppendTop<T>(StringBuilder sb, string head, Dictionary<T, long> d) where T : MBObjectBase
        {
            if (d.Count <= 0) return;
            var top = new List<KeyValuePair<T, long>>(d);
            top.Sort((x, y) => y.Value.CompareTo(x.Value));
            sb.Append(head);
            for (int i = 0; i < top.Count && i < 4; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(NameOf(top[i].Key)).Append(' ').Append(top[i].Value);
            }
        }
    }
}
