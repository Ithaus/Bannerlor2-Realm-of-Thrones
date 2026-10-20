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
    ///     pozycji w tej osadzie (Town.GetItemPrice - ta sama cena, od ktorej liczy gra), tylko gdy woz wiezie kruszec (iron - lupa albo ruda, silver, gold_ore);
    ///     postfiks First (przed zwrotem nadplaty MarketCarts i zakupami wsi przez woz - te zmieniaja tylko zloto) liczy sprzedane sztuki z roznicy ladunku:
    ///     wartosc kruszcu i wartosc calego sprzedanego ladunku po tych cenach - na konto wozu (kilka miast przed powrotem - sumy).
    ///  2. Powrot wozu do wsi MACIERZYSTEJ (VillageTakings.EnterPostfix, po 112 i F4b, przed pracami okregu, wioska gracza i daninami DS): oddany utarg x udzial
    ///     kruszcu w sprzedanym ladunku = utarg za kruszec; minus rachunek za drewno dymarki pokryty w tym oknie (F4b: d = min(rachunek, utarg), ta sama kolejnosc co
    ///     podatek pana - pan bierze od tego, co zostalo po drewnie) = podstawa; regale = MineRegaleShare x podstawa. Placi czesc wsi z TEGO utargu: najwyzej przyrost
    ///     kiesy wsi w oknie (nigdy z oszczednosci sprzed powrotu). Zloto: kiesa wsi -> licznik podatku wsi (TradeTaxAccumulated - ten sam licznik, z ktorego pan
    ///     zbiera swoj podatek; majatki BK nie biora z regale udzialu - liczy je BK tylko od swojego podatku). Nic nie powstaje, nic nie znika.
    ///  3. ZWOLNIENIE ("Freiung" - pan zwalnial kopalnie w biedzie z dziesieciny): kopalnia z dymarka (Bloomery), ktora ma dlug u kupcow za drewno (f5-ruda) - nic;
    ///     inaczej regale tylko z kiesy ponad zapas na drewno dymarki na WoodDays dob (potrzeba ponad las okregu x cena drewna w miescie handlowym - ta sama miara co
    ///     "kiesy kopaln ponizej kosztu drewna doby" w linii zelaza). Kopalnie srebra i zlota bez dymarki - bez zapasu.
    /// DS (daniny wsi, DsVillage.DuesAtCart) biegnie PO regale i bierze dziesiecine od calego utargu, a udzial korony od czesci wsi netto (przyrost kiesy) -
    /// regale nie jest liczone drugi raz; czynsz staly i renta wsi liczone od kiesy, ktora juz nie ma regale. Kopalnie BK w miastach (polowa ceny rudy jako danina)
    /// to inna droga (warsztat miasta) - tu tylko wozy wsi. Wioska poboczna gracza (SideVillageFief.AtCart po r4-wioska - tylko licznik linii; naleznosc Due osobno) - bez zmian.
    /// KS (alfa okregu) i SplitToday widza czesc wsi pomniejszona o regale (VillageTakings.RegaleTaken).
    /// Zapis arm_regale (SaveText): "1|idWozu~wartoscKruszcu~wartoscLadunku|..." - woz w drodze z kruszcem sprzedanym, a jeszcze nie w domu; brak klucza (stary zapis
    /// Jeffa) = pusto, woz bez konta oddaje utarg jak dotad. Klucz NIE - nic sie nie zapisuje na konta i nic nie pobiera (utarg bit w bit jak przed paczka).
    /// Linia doby "Regale gornicze (R3-09f): dzien N" z kontrola "kiesy wsi - X, liczniki panow + X, reszta 0". Potkniecia liczone per miejsce (pierwsze do logu).
    /// </summary>
    internal static class MineRegale
    {
        private const int WoodDays = 7;                 // zapas na drewno dymarki - jak sufit kredytu kupcow f5 (F5CreditDays 7)
        private const double ShareMax = 0.5;            // przyciecie stawki z MCM
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class Acc { public long Ore, All; public int Day; }
        // id wozu -> wartosc kruszcu i calego sprzedanego ladunku od ostatniego powrotu (watek gry; lock - sprzedaz wolana takze spoza naszego ticku)
        private static readonly Dictionary<string, Acc> _acc = new Dictionary<string, Acc>(StringComparer.Ordinal);
        private static readonly object Gate = new object();
        private static Campaign _camp;
        private static bool _fromImport;               // konta wczytane z zapisu, gdy Campaign.Current jeszcze nie bylo - pierwsza kampania je przejmuje

        internal sealed class Snap
        {
            public EquipmentElement[] El; public int[] N, P; public byte[] K;
        }

        // ------------------------------------------------------------ liczniki doby (linia)
        private static int _sN; private static long _sOre, _sAll; private static readonly long[] _sPcs = new long[4];
        private static int _rN, _rFreeDebt, _rFreeWood, _rNoPart, _rForeign;
        private static long _rHanded, _rOre, _rBill, _rBase, _rDue, _rTaken, _rFreeDebtZl, _rFreeWoodZl, _rNoPartZl, _rPurseOut, _rTaxIn;
        private static readonly Dictionary<Clan, long> _rClan = new Dictionary<Clan, long>();
        private static int _stum, _stumSess;
        private static readonly HashSet<string> _errOnce = new HashSet<string>(StringComparer.Ordinal);
        private static bool _hooked;
        // okno powrotu: rachunek za drewno przed F4b (Open -> AtCart, ten sam woz)
        private static VillageTakings.Entry _open; private static int _openBill;

        private static void ClearDay()
        {
            _sN = 0; _sOre = _sAll = 0; Array.Clear(_sPcs, 0, _sPcs.Length);
            _rN = _rFreeDebt = _rFreeWood = _rNoPart = _rForeign = 0;
            _rHanded = _rOre = _rBill = _rBase = _rDue = _rTaken = _rFreeDebtZl = _rFreeWoodZl = _rNoPartZl = _rPurseOut = _rTaxIn = 0;
            _rClan.Clear(); _stum = 0;
        }

        /// <summary>Nowa kampania albo wczytanie (inny obiekt Campaign): konta wozow od zera. Bez wpiecia w konstruktor ArmouryBehavior (scalanie galezi).</summary>
        private static void EnsureCampaign()
        {
            var c = Campaign.Current;
            if (ReferenceEquals(c, _camp)) return;
            if (_fromImport && _camp == null) { _camp = c; _fromImport = false; return; }
            _camp = c; _fromImport = false;
            lock (Gate) _acc.Clear();
            _open = null; _openBill = 0; _stumSess = 0; _errOnce.Clear();
            ClearDay();
        }

        private static void Stumble(string where, Exception e)
        {
            _stum++; _stumSess++;
            try { if (_errOnce.Add(where)) Log.Error("MineRegale." + where, e); } catch { }
        }

        internal static bool On { get { var s = Settings.Current; return s != null && s.MineRegale && s.VillageTakingsWhole; } }

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
        /// <summary>SellGoodsForTradeAction.ApplyInternal(Settlement, MobileParty, detail) - prefiks First. Tylko odczyt; migawka tylko dla wozu wsi z kruszcem.</summary>
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
                if (!any) return;
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
        /// przed - po; wartosc kruszcu i calego sprzedanego ladunku po cenach z prefiksu na konto wozu.</summary>
        public static void SellPostfix(MobileParty __1, Snap __state)
        {
            if (__state == null || __1 == null) return;
            try
            {
                EnsureCampaign();
                var r = __1.ItemRoster;
                long ore = 0, all = 0;
                for (int i = 0; i < __state.El.Length; i++)
                {
                    int idx = r.FindIndexOfElement(__state.El[i]);
                    int now = idx >= 0 ? r.GetElementNumber(idx) : 0;
                    int sold = __state.N[i] - now;
                    if (sold <= 0) continue;
                    long v = (long)sold * __state.P[i];
                    all += v;
                    byte k = __state.K[i];
                    if (k != 0) { ore += v; _sPcs[k] += sold; }
                }
                if (ore <= 0 || all <= 0) return;
                string id = __1.StringId;
                if (string.IsNullOrEmpty(id)) return;
                int today = (int)CampaignTime.Now.ToDays;
                lock (Gate)
                {
                    Acc a;
                    if (!_acc.TryGetValue(id, out a)) { a = new Acc(); _acc[id] = a; }
                    a.Ore += ore; a.All += all; a.Day = today;
                }
                _sN++; _sOre += ore; _sAll += all;
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
                double sh = Math.Min(1.0, (double)a.Ore / a.All);
                long oreGold = (long)Math.Floor(handed * sh);
                long d = Math.Min((long)Math.Max(0, bill), (long)handed);
                long baseZl = Math.Max(0L, oreGold - d);
                long due = (long)Math.Floor(baseZl * Share);
                _rN++; _rHanded += handed; _rOre += oreGold; _rBill += Math.Min(d, oreGold); _rBase += baseZl; _rDue += due;
                if (due <= 0) return;
                long grown = (long)home.Gold - st.HomeGold;          // czesc wsi tego utargu w kiesie (BK + 112 + zwrot F4b)
                if (grown <= 0) { _rNoPart++; _rNoPartZl += due; return; }
                long reserve; bool owes = Bloomery.RegaleWoodGuard(home, WoodDays, out reserve);
                if (owes) { _rFreeDebt++; _rFreeDebtZl += due; return; }
                long room = Math.Min(grown, Math.Max(0L, (long)home.Gold - Math.Max(0L, reserve)));
                long take = Math.Min(due, room);
                if (take < due)
                {
                    long cut = due - take;
                    if (room < grown) { _rFreeWood++; _rFreeWoodZl += cut; } else { _rNoPart++; _rNoPartZl += cut; }
                }
                if (take <= 0 || take > int.MaxValue) return;
                int had = home.Gold;
                home.ChangeGold(-(int)take);
                long got = Math.Max(0L, (long)had - home.Gold);       // zdjete naprawde z kiesy - tyle i tylko tyle na licznik pana
                if (got > take) got = take;
                if (got < take) { _rNoPart++; _rNoPartZl += take - got; }   // kiesa oddala mniej (cudza latka) - reszta naleznego jak "ponad czesc wsi"
                if (got <= 0) return;
                home.TradeTaxAccumulated += (int)got;
                _rTaken += got; _rPurseOut += got; _rTaxIn += got;
                VillageTakings.RegaleTaken(home, got);                 // KS / SplitToday: czesc wsi utargu doby mniejsza o regale
                try
                {
                    var c = home.Settlement != null ? home.Settlement.OwnerClan : null;
                    if (c != null) { long x; _rClan.TryGetValue(c, out x); _rClan[c] = x + got; }
                }
                catch (Exception e) { Stumble("AtCart(rod)", e); }
            }
            catch (Exception e) { Stumble("AtCart", e); }
        }

        // ------------------------------------------------------------ zapis (arm_regale)
        internal static string Export()
        {
            try
            {
                EnsureCampaign();
                var sb = new StringBuilder(256);
                sb.Append('1');
                lock (Gate)
                    foreach (var kv in _acc)
                    {
                        if (kv.Value == null || kv.Value.Ore <= 0 || kv.Value.All <= 0 || kv.Key.IndexOf('|') >= 0 || kv.Key.IndexOf('~') >= 0) continue;
                        sb.Append('|').Append(kv.Key).Append('~').Append(kv.Value.Ore.ToString(Inv)).Append('~').Append(kv.Value.All.ToString(Inv)).Append('~').Append(kv.Value.Day.ToString(Inv));
                    }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return null; }
        }

        internal static void Import(string s)
        {
            // wczytanie = nowa sesja kampanii: konta od zera (bez wpiecia w konstruktor ArmouryBehavior), potem konta z zapisu
            lock (Gate) _acc.Clear();
            _open = null; _openBill = 0; _stumSess = 0; _errOnce.Clear();
            ClearDay();
            _camp = Campaign.Current;
            _fromImport = _camp == null;
            if (string.IsNullOrEmpty(s)) return;
            try
            {
                var parts = s.Split('|');
                if (parts.Length == 0 || parts[0] != "1") return;     // nieznana wersja - pusto (woz bez konta oddaje utarg jak dotad)
                lock (Gate)
                    for (int i = 1; i < parts.Length; i++)
                    {
                        var f = parts[i].Split('~');
                        if (f.Length < 3 || string.IsNullOrEmpty(f[0])) continue;
                        long o, a; int d = 0;
                        if (!long.TryParse(f[1], NumberStyles.Integer, Inv, out o) || !long.TryParse(f[2], NumberStyles.Integer, Inv, out a) || o <= 0 || a <= 0) continue;
                        if (f.Length > 3) int.TryParse(f[3], NumberStyles.Integer, Inv, out d);
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
                Log.Info("Regale gornicze (R3-09f): latka wpieta (SellGoodsForTradeAction.ApplyInternal - prefiks First / postfiks First: ladunek kruszcu wozow wsi; rozliczenie przy powrocie wozu - VillageTakings.EnterPostfix po F4b); klucz Mine Regale "
                         + (s != null && s.MineRegale ? "WLACZONY" : "wylaczony") + ", stawka " + Share.ToString("0.000", Inv) + " wartosci kruszcu (po rachunku za drewno), zwolnienie: dlug za drewno albo kiesa ponizej " + WoodDays + " dob drewna dymarki"
                         + (s != null && !s.VillageTakingsWhole ? "; UWAGA: Village Takings Whole wylaczone - bez okna powrotu wozu regale nieczynne" : "") + ".");
            }
            catch (Exception e) { Log.Error("MineRegale.ApplyAll", e); }
        }

        // ------------------------------------------------------------ raz na dobe
        /// <summary>ArmouryBehavior.OnDailyTick (po VillageTakings.Daily i DsVillage.Daily): linia doby i zerowanie licznikow; konta wozow, ktorych juz nie ma na mapie,
        /// zamkniete (woz rozbity albo rozwiazany - jego sakwa poszla droga 112 / 3.2, regale nie ma od czego wziac).</summary>
        internal static void Daily()
        {
            try
            {
                if (Campaign.Current == null) return;
                EnsureCampaign();
                var s = Settings.Current;
                int day = (int)CampaignTime.Now.ToDays - 1;
                int open = 0, gone = 0; long openOre = 0;
                lock (Gate)
                {
                    if (_acc.Count > 0)
                    {
                        var live = new HashSet<string>(StringComparer.Ordinal);
                        foreach (var p in MobileParty.AllVillagerParties) if (p != null && p.IsActive && p.StringId != null) live.Add(p.StringId);
                        List<string> drop = null;
                        foreach (var kv in _acc) if (!live.Contains(kv.Key)) { if (drop == null) drop = new List<string>(); drop.Add(kv.Key); }
                        if (drop != null) foreach (var k in drop) { _acc.Remove(k); gone++; }
                    }
                    open = _acc.Count;
                    foreach (var kv in _acc) openOre += kv.Value.Ore;
                }
                var sb = new StringBuilder(768);
                sb.Append("Regale gornicze (R3-09f): dzien ").Append(day).Append(" | ");
                if (!_hooked) sb.Append("latka sprzedazy nie jest wpieta - nieczynne");
                else if (s == null || !s.MineRegale) sb.Append("WYLACZONE w ustawieniach (Mine Regale) - pan bierze z kruszcu tyle co ze zboza");
                else if (!s.VillageTakingsWhole) sb.Append("NIECZYNNE - Village Takings Whole wylaczone (bez okna powrotu wozu)");
                else sb.Append("CZYNNE, stawka ").Append(Share.ToString("0.000", Inv)).Append(" wartosci kruszcu po rachunku za drewno");
                sb.Append(" | sprzedaze wozow z kruszcem ").Append(_sN).Append(": zelazo ").Append(_sPcs[1]).Append(", srebro ").Append(_sPcs[2]).Append(", zloto ").Append(_sPcs[3])
                  .Append(" sztuk, kruszec ").Append(_sOre).Append(" zl z ladunku ").Append(_sAll).Append(" zl (ceny osady sprzedazy)");
                sb.Append(" | powroty z kruszcem ").Append(_rN).Append(": utarg ").Append(_rHanded).Append(" zl, z tego za kruszec ").Append(_rOre).Append(", rachunek za drewno (F4b) ")
                  .Append(_rBill).Append(", podstawa ").Append(_rBase).Append(", regale nalezne ").Append(_rDue).Append(", pobrane ").Append(_rTaken)
                  .Append(" (kiesa wsi -> licznik pana); zwolnione: dlug za drewno ").Append(_rFreeDebt).Append(" (").Append(_rFreeDebtZl).Append(" zl), zapas na drewno ")
                  .Append(WoodDays).Append(" dob ").Append(_rFreeWood).Append(" (").Append(_rFreeWoodZl).Append(" zl), ponad czesc wsi z utargu ").Append(_rNoPart).Append(" (")
                  .Append(_rNoPartZl).Append(" zl); woz w obcej wsi ").Append(_rForeign);
                sb.Append(" | kontrola: kiesy wsi -").Append(_rPurseOut).Append(", liczniki panow +").Append(_rTaxIn).Append(", reszta ").Append(_rTaxIn - _rPurseOut)
                  .Append("; nalezne = pobrane + zwolnione: reszta ").Append(_rDue - _rTaken - _rFreeDebtZl - _rFreeWoodZl - _rNoPartZl);
                if (_rClan.Count > 0)
                {
                    var top = new List<KeyValuePair<Clan, long>>(_rClan);
                    top.Sort((x, y) => y.Value.CompareTo(x.Value));
                    sb.Append(" | najwiecej: ");
                    for (int i = 0; i < top.Count && i < 3; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        string n = "?"; try { n = top[i].Key.Name != null ? top[i].Key.Name.ToString() : top[i].Key.StringId; } catch { }
                        sb.Append(n).Append(' ').Append(top[i].Value);
                    }
                }
                sb.Append(" | konta wozow w drodze ").Append(open).Append(" (kruszec ").Append(openOre).Append(" zl), zamkniete bez wozu ").Append(gone);
                if (_stum > 0) sb.Append(" | potkniecia ").Append(_stum).Append(" (sesja ").Append(_stumSess).Append(", pierwsze z kazdego miejsca w logu)");
                sb.Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Log.Error("MineRegale.Daily", e); }
            finally { ClearDay(); }
        }
    }
}
