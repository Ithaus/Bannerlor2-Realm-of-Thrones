using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// M3 (pieniadze roku 2, docs/pieniadze-rok-2/PROJEKT-WYBRANY.md 3.5; klucz BreadForThePoor) - CHLEB DLA GLODNYCH MIAST. Bieg dwuletni: na koniec roku 2 7 miast
    /// nie stac na jedzenie (Highgarden, Oldtown, Braavos ...), chleb swiata 89.7% przy celu 98% i spada co kwartal (BD 4.3). Regula leczy skutek tym samym wzorem
    /// w kazdej osadzie, bez zakladania przyczyny biedy.
    ///  - WEJSCIE (jedna definicja glodu - zasada 0): osada z glodem z braku pieniedzy wedlug Hunger.Of / MoneyHungry (Afford jedzenia na jutro &lt; 0.95 przez 7 dob
    ///    z rzedu, kampania &gt;= 120 dob). Licznik dob glodu biegnie od startu kampanii (prog wieku jest tylko w MoneyHungry), wiec osada glodna od tygodnia wchodzi
    ///    juz w pierwszej kotwicy z wiekiem 120 (recenzja - nie "120 + 7"; ta sama chwila, w ktorej zaczyna sie kara zadowolenia E5/C2).
    ///    WYJSCIE (histereza): 7 kotwic z rzedu, w ktorych lud sam ma na BreadDays dni jedzenia. Stan - pamiec sesji (bez pola zapisu).
    ///  - LUKA (w kotwicy doby, TownLive / CastleLiveDay po NoteHungerDay): miasto - klasy W/R/P: max(0, BreadDays x potrzeba dzienna klasy x udzial jedzenia - kiesa
    ///    klasy); podzamcze - max(0, BreadDays x potrzeba x udzial jedzenia - (kiesa + kredyt dworu)). BreadDays = 2: chleb wplywa w kroku korony PO kotwicy, wiec
    ///    trafia do kotwicy nastepnej doby - z celem 1 dnia osada skakalaby co drugi dzien (pelna / glodna); drugi dzien to zapas na to opoznienie (w rownowadze
    ///    koszt = dzienny brak, jednorazowo +1 dzien jedzenia na osade - zostaje w kiesie ludu).
    ///  - PLATNIK: (1) korona krolestwa osady z reszty wplywow dnia PO zwrocie zoldu i po W5 / M1, PRZED rentami 180 (BreadCrown z CrownRents.Daily; krolestwa bez
    ///    kroku rent - BreadRest zaraz po rentach) - w tym kroku nigdy z zapasu skarbca (zapas - krok 3), nigdy z kredytu (wydatek przed zwrotem pchalby korone w wojnie do Banku); (2) zastepczo pan
    ///    osady (glowa rodu wlasciciela) - tylko ponad rezerwe wojny R budzetu 166 i tylko z tej czesci tempa skrzyni (G - R) / WarChestDays, ktorej nie bierze zold
    ///    (recenzja: w sumie nie szybciej niz skrzynia) - w wojnie 1 - WarChestToWages (0.2, i o tyle mniej skrzyni sprzetu tej doby - ClanBudget), w pokoju
    ///    1 - PeaceChestShare x WarChestToWages (0.68); NIGDY gracz; dluznik i rod w zajeciu - nie (kiese bierze wierzyciel); (3) m105 G2 (klucz Crown Hoard Feeds Hungry,
    ///    recenzja R8a - wyjatek od "nigdy z zapasu skarbca" z punktu 1): reszta luki PO panie - skarbiec krolestwa osady, tylko ponad zapas wojny + reszte wplywow dnia
    ///    + dlugi (KingdomTreasury.HoardRoom: limit doby 1/Crown Surplus Aid Days nadwyzki, wspolny z rachunkami KS-H; krolestwo gracza-wladcy 0; nigdy z kredytu);
    ///    lenno gracza-wasala tez (placi korona, gracz nigdy z kiesy - ta sama regula co rachunki KS-H, KsLordFood.HoardBills); (4) nikt - luka zostaje (licznik).
    ///    Kiesa klasy jest jedna (recenzja): lud wydaje ja jak zawsze - najpierw jedzenie, potem reszta koszyka (Anchor) - wiec chleb w rownowadze pokrywa caly dzienny
    ///    brak koszyka (potrzeba - zarobek), nie sam brak jedzenia. Wydzielonej "kiesy chleba" nie ma (drugi stan i druga regula na te sama kiese - zasada 0).
    ///  - KSIEGA: skarbiec (glowa) -> kiesy ludu osady: kontrola zamknieta kies (_ctlIn, linia "Kiesa ludu" r = 0), 165 (KDay.Bread; chleb z zapasu skarbca z kroku 3
    ///    bez KDay.Bread i bez CrownIncome.Spent - to nie wplywy dnia), "Obieg". Przelew miedzy
    ///    posiadaczami ksiegi pieniadza - nie zrodlo.
    /// Linia doby "Chleb biedoty (M3): dzien N". Wyjatek per osada - licznik potkniec TownFolk (CLAUDE.md 7). Watek glowny (blok doby).
    /// </summary>
    internal static partial class TownFolk
    {
        private const double BreadDays = 2.0;
        private static readonly CultureInfo BrInv = CultureInfo.InvariantCulture;

        // indeks doby: osady z luka wedlug krolestwa wlasciciela (null - rod bez krolestwa) i krolestwa obsluzone przez krok korony
        private static int _brIdxDay = int.MinValue;
        private static readonly Dictionary<Kingdom, List<Folk>> _brIdx = new Dictionary<Kingdom, List<Folk>>();
        private static readonly List<Folk> _brNoK = new List<Folk>();
        private static readonly HashSet<Kingdom> _brDone = new HashSet<Kingdom>();
        private static readonly Dictionary<Hero, long> _brLordToday = new Dictionary<Hero, long>();
        private static readonly Dictionary<Kingdom, long[]> _brByK = new Dictionary<Kingdom, long[]>();   // [korona, pan, nie stac nikogo]
        // recenzja: chleb pana tej doby wedlug rodu (ClanBudget zdejmuje go ze skrzyni sprzetu tej doby w wojnie) i podzial pan w wojnie / w pokoju (linia)
        private static readonly Dictionary<Clan, long> _brClanToday = new Dictionary<Clan, long>();
        private static int _brClanDay = int.MinValue;
        private static long _brLordWar, _brLordPeace;
        // liczniki doby (linia) i dnia ksiegi ("Obieg")
        private static int _brIn, _brOutN, _brLordN, _brNoneN, _brBelowR, _brPlayerPurse, _brCrownK;
        private static long _brCrown, _brLord, _brNone;
        // m105 G2 (Crown Hoard Feeds Hungry): chleb ze skarbca ponad zapas wojny tej doby (w _brCrown - platnik to skarbiec), osady, krolestwa, luka przy wolnym limicie
        // (oczekiwane 0), skarbiec ponizej zapasu wojny po wyplacie (oczekiwane 0), kontrola skarbiec - kiesy ludu (zmierzone)
        private static long _brHoard, _brHoardLeft, _brHoardTook; private static int _brHoardN, _brHoardBelow;
        private static readonly HashSet<Kingdom> _brHoardK = new HashSet<Kingdom>();
        private static double _brMinGR = double.NaN;
        internal static long LastBreadCrown, LastBreadLord;
        // komunikat gracza raz na 7 dob (pamiec sesji)
        private static int _brPlWeek = -1;
        private static long _brPlFief, _brPlFiefCrown, _brPlKingBread, _brPlKingRent;
        private static readonly HashSet<string> _brPlNames = new HashSet<string>();

        private static bool BreadKey { get { var s = Settings.Current; return s != null && s.BreadForThePoor; } }

        private static void ResetBread()
        {
            _brIdxDay = int.MinValue; _brIdx.Clear(); _brNoK.Clear(); _brDone.Clear(); _brLordToday.Clear(); _brByK.Clear();
            _brClanToday.Clear(); _brClanDay = int.MinValue; _brLordWar = _brLordPeace = 0;
            _brIn = _brOutN = _brLordN = _brNoneN = _brBelowR = _brPlayerPurse = _brCrownK = 0; _brCrown = _brLord = _brNone = 0; _brMinGR = double.NaN;
            _brHoard = _brHoardLeft = _brHoardTook = 0; _brHoardN = _brHoardBelow = 0; _brHoardK.Clear();   // m105 G2
            LastBreadCrown = LastBreadLord = 0;
            _brPlWeek = -1; _brPlFief = _brPlFiefCrown = _brPlKingBread = _brPlKingRent = 0; _brPlNames.Clear();
        }

        /// <summary>Kotwica doby (TownLive / CastleLiveDay po NoteHungerDay): wejscie / wyjscie stanu "na chlebie" i luka na BreadDays dni jedzenia. Nie rzuca.</summary>
        private static void BreadAnchor(Folk f)
        {
            try
            {
                f.BreadGap = 0; f.BreadPaid = 0; f.BreadCrown = 0; f.BreadLord = 0; f.BreadGapC[0] = f.BreadGapC[1] = f.BreadGapC[2] = 0;
                if (!BreadKey || f.Kind > 1 || !f.FacOk) { if (f.BreadOn) { f.BreadOn = false; f.BreadZero = 0; } return; }
                if (!f.BreadOn && MoneyHungry(f.St)) { f.BreadOn = true; f.BreadZero = 0; _brIn++; }
                if (!f.BreadOn) return;
                var s = Settings.Current;
                long gap = 0;
                if (f.Kind == 1)
                {
                    double L = Math.Max(0, f.KW) + (s.TownFolkCastleCourtFeeds ? Math.Max(0, f.CourtCr) : 0);
                    double v = BreadDays * f.Need[0] * FdC - L;
                    long g = v > 0 ? (long)Math.Ceiling(v) : 0;   // NaN i ujemne - 0
                    f.BreadGapC[0] = g; gap = g;
                }
                else
                {
                    for (int c = 0; c < 3; c++)
                    {
                        double fd = c == 0 ? FdW : c == 1 ? FdR : FdP;
                        double v = BreadDays * f.Need[c] * fd - Math.Max(0, PurseOf(f, c));
                        long g = v > 0 ? (long)Math.Ceiling(v) : 0;   // NaN i ujemne - 0
                        f.BreadGapC[c] = g; gap += g;
                    }
                }
                if (gap < 1)
                {
                    // histereza: lud sam ma na dwa dni jedzenia - po 7 takich kotwicach z rzedu osada schodzi z chleba
                    if (++f.BreadZero >= Hunger.MoneyDays) { f.BreadOn = false; f.BreadZero = 0; _brOutN++; }
                    return;
                }
                f.BreadZero = 0;
                f.BreadGap = gap; f.BreadDay = _liveDay;
            }
            catch (Exception e) { Stumble("BreadAnchor", e); }
        }

        /// <summary>Przelew chleba do kies osady (platnik zaplacil juz `x`): klasy wedlug luk (miasto) albo kiesa podzamcza; kontrola zamknieta kies.</summary>
        private static void BreadCredit(Folk f, long x)
        {
            long left = x;
            if (f.Kind == 1) f.KW += left;
            else
            {
                for (int c = 0; c < 3 && left > 0; c++)
                {
                    long due = Math.Max(0, f.BreadGapC[c]);
                    long y = Math.Min(due, left);
                    if (y <= 0) continue;
                    AddPurse(f, c, y); f.BreadGapC[c] -= y; left -= y;
                }
                if (left > 0) f.KW += left;   // zaokraglenia - do wyrobnikow
            }
            f.LDayIn += x; f.BreadPaid += x;
            _ctlIn += x;
        }

        private static void BreadIndex()
        {
            if (_brIdxDay == _liveDay) return;
            _brIdxDay = _liveDay;
            _brIdx.Clear(); _brNoK.Clear(); _brDone.Clear(); _brLordToday.Clear(); _brByK.Clear();
            _brLordN = _brNoneN = _brBelowR = _brPlayerPurse = _brCrownK = 0; _brCrown = _brLord = _brNone = 0; _brMinGR = double.NaN; _brLordWar = _brLordPeace = 0;
            _brHoard = _brHoardLeft = _brHoardTook = 0; _brHoardN = _brHoardBelow = 0; _brHoardK.Clear();   // m105 G2: doba
            if (!BreadKey || !Live) return;
            foreach (var f in _f.Values)
            {
                if (f.Kind > 1 || f.BreadDay != _liveDay || f.BreadGap <= 0 || f.St == null) continue;
                var c = f.St.OwnerClan;
                var k = c != null ? c.Kingdom : null;
                if (k == null || k.IsEliminated) { _brNoK.Add(f); continue; }
                List<Folk> l;
                if (!_brIdx.TryGetValue(k, out l)) { l = new List<Folk>(); _brIdx[k] = l; }
                l.Add(f);
            }
            foreach (var l in _brIdx.Values) l.Sort((a, b) => b.BreadGap.CompareTo(a.BreadGap));   // najwieksza luka pierwsza (wieksze miasto)
        }

        /// <summary>Recenzja M3 (ClanBudget.Daily - po kroku korony tej samej doby): chleb, ktory glowa rodu `c` zaplacila dzis za glodne osady rodu. 0 - nic, inna doba
        /// albo klucz wylaczony. Tylko odczyt.</summary>
        internal static long BreadLordToday(Clan c)
        {
            long v;
            return c != null && Campaign.Current != null && _brClanDay == (int)CampaignTime.Now.ToDays && _brClanToday.TryGetValue(c, out v) ? Math.Max(0, v) : 0;
        }

        /// <summary>Recenzja M3: czesc tempa skrzyni (G - R) / WarChestDays, ktorej nie bierze zold - z niej (i tylko z niej) pan placi chleb. W wojnie 1 - WarChestToWages
        /// (reszta to skrzynia sprzetu 0.2 - ClanBudget zdejmuje z niej zaplacony chleb), w pokoju 1 - PeaceChestShare x WarChestToWages.
        /// TP (16.10): to samo tempo ogranicza splate dlugu osady wobec gracza z kiesy rodu (TourneyPay.ClanSpare, minus chleb tego rodu dzis).</summary>
        internal static double BreadLordShare(Settings s, bool war)
        {
            double w = Math.Max(0f, Math.Min(1f, s.WarChestToWages));
            double used = war ? w : Math.Max(0f, Math.Min(1f, s.PeaceChestShare)) * w;
            return Math.Max(0, 1 - used);
        }

        private static long[] BreadRow(Kingdom k)
        {
            long[] r;
            if (!_brByK.TryGetValue(k, out r)) { r = new long[3]; _brByK[k] = r; }
            return r;
        }

        /// <summary>
        /// M3 - krok korony krolestwa `k` (CrownRents.Daily w petli krolestw: po W5 / M1, PRZED podzialem rent; krolestwa bez kroku rent - BreadRest): luki osad tego
        /// krolestwa z reszty wplywow dnia (CrownIncome.LeftFor / Spent). Raz na krolestwo na dobe. Nie rzuca.
        /// </summary>
        internal static long BreadCrown(Kingdom k)
        {
            long paidK = 0;
            try
            {
                if (k == null || !BreadKey || !Live) return 0;
                BreadIndex();
                if (!_brDone.Add(k)) return 0;
                List<Folk> list;
                if (!_brIdx.TryGetValue(k, out list)) return 0;
                var day = CrownIncome.DayOf(k);
                if (day == null) return 0;                                // 165 wylaczone - korona nie ma "reszty wplywow"; zostaje pan zastepczo
                for (int i = 0; i < list.Count; i++)
                {
                    var f = list[i];
                    try
                    {
                        long need = f.BreadGap - f.BreadPaid;
                        if (need <= 0) continue;
                        long avail = CrownIncome.LeftFor(k);
                        if (avail <= 0) break;
                        int ix = (int)Math.Min(int.MaxValue, Math.Min(need, avail));
                        k.KingdomBudgetWallet -= ix;                      // najpierw skarbiec, potem kiesa ludu (arytmetyka - nie rzuca)
                        BreadCredit(f, ix);
                        CrownIncome.Spent(k, ix);
                        day.Bread += ix; paidK += ix; f.BreadCrown += ix;
                    }
                    catch (Exception e) { Stumble("BreadCrown(osada)", e); }
                }
            }
            catch (Exception e) { Stumble("BreadCrown", e); }
            finally { if (paidK > 0) { _brCrown += paidK; _brCrownK++; BreadRow(k)[0] += paidK; } }
            return paidK;
        }

        /// <summary>
        /// M3 - zaraz po rentach 180 (ArmouryBehavior, przed linia W5 i przed CrownIncome.End): korona krolestw bez kroku rent, pan osady zastepczo, licznik "nie stac
        /// nikogo", linia doby i komunikat gracza. Nie rzuca.
        /// </summary>
        internal static void BreadRest()
        {
            LastBreadCrown = LastBreadLord = 0;
            var s = Settings.Current;
            if (s == null || Campaign.Current == null || !Live) return;
            try
            {
                BreadIndex();
                var ks = new List<Kingdom>(_brIdx.Keys);
                foreach (var k in ks) if (!_brDone.Contains(k)) BreadCrown(k);   // krolestwa bez rodow z lennem albo renty wylaczone - ta sama regula po rentach
                var all = new List<Folk>(_brNoK);
                foreach (var l in _brIdx.Values) all.AddRange(l);
                float wcd = Math.Max(1f, s.WarChestDays);
                foreach (var f in all)
                {
                    try
                    {
                        long need = f.BreadGap - f.BreadPaid;
                        if (need <= 0) continue;
                        var c = f.St.OwnerClan;
                        var h = c != null ? c.Leader : null;
                        double G, R; bool war;
                        if (c != null && c != Clan.PlayerClan && h != null && h.IsAlive && h != Hero.MainHero && ClanBudget.ChestOf(c, out G, out R, out war))
                        {
                            long gNow = ClanIncomeBook.FamilyGoldOf(c);
                            long done; _brLordToday.TryGetValue(h, out done);
                            // recenzja: pan w sumie nie szybciej niz skrzynia - chleb tylko z czesci tempa skrzyni (G - R) / WarChestDays, ktorej nie bierze zold (wojna 0.2, pokoj 0.68)
                            double rate = BreadLordShare(s, war) * Math.Max(0, gNow - R) / wcd - done;
                            long x = (long)Math.Floor(Math.Min(Math.Min((double)need, rate), Math.Min((double)h.Gold, gNow - R)));
                            if (x > 0)
                            {
                                if (R > 0) { double gr = gNow / R; if (double.IsNaN(_brMinGR) || gr < _brMinGR) _brMinGR = gr; }
                                int ix = (int)Math.Min(int.MaxValue, x);
                                h.ChangeHeroGold(-ix);                    // najpierw pan, potem kiesa ludu
                                BreadCredit(f, ix);
                                CirculationWindows.NoteHeroGold(h, -ix);  // paczka 169b: glowa poza swiatem (Disabled) - tylko licznik
                                _brLordToday[h] = done + ix; _brLord += ix; f.BreadLord += ix; need -= ix;
                                int today0 = (int)CampaignTime.Now.ToDays;
                                if (_brClanDay != today0) { _brClanToday.Clear(); _brClanDay = today0; }
                                long cd; _brClanToday.TryGetValue(c, out cd); _brClanToday[c] = cd + ix;   // ClanBudget: o tyle mniej skrzyni sprzetu tej doby (wojna)
                                if (war) _brLordWar += ix; else _brLordPeace += ix;
                                if (c.Kingdom != null) BreadRow(c.Kingdom)[1] += ix;
                                if (ClanIncomeBook.FamilyGoldOf(c) < R) _brBelowR++;   // kontrola: pan po wyplacie nie ponizej rezerwy wojny (oczekiwane 0)
                            }
                        }
                        if (h == Hero.MainHero && f.BreadLord > 0) _brPlayerPurse++;    // kontrola: kiesa gracza nigdy (oczekiwane 0)
                        // m105 G2 (Crown Hoard Feeds Hungry): reszta luki PO koronie z wplywow dnia i PO panie - skarbiec krolestwa osady ponad zapas wojny (limit doby wspolny
                        // z rachunkami KS-H, KingdomTreasury.HoardRoom); najpierw skarbiec, potem kiesa ludu. Krolestwo gracza, rod bez krolestwa - nie
                        if (need > 0 && s.CrownHoardFeedsHungry && c != null && c.Kingdom != null && !c.Kingdom.IsEliminated)
                        {
                            var k = c.Kingdom;
                            long room = KingdomTreasury.HoardRoom(k);
                            long x = Math.Min(need, room);
                            if (x > 0)
                            {
                                int ix = (int)Math.Min(int.MaxValue, x);
                                long w0 = k.KingdomBudgetWallet;
                                k.KingdomBudgetWallet -= ix;                      // najpierw skarbiec, potem kiesa ludu (arytmetyka - nie rzuca)
                                long took = w0 - k.KingdomBudgetWallet;
                                BreadCredit(f, ix);
                                KingdomTreasury.HoardUse(k, ix);
                                _brCrown += ix; _brHoard += ix; _brHoardTook += took; _brHoardN++; _brHoardK.Add(k);
                                f.BreadCrown += ix; BreadRow(k)[0] += ix; need -= ix;
                                if (KingdomTreasury.HoardBelow(k)) _brHoardBelow++;
                            }
                            if (need > 0 && KingdomTreasury.HoardRoom(k) > 0) _brHoardLeft += need;   // kontrola: luka zostala mimo wolnego limitu skarbca (oczekiwane 0)
                        }
                        if (need > 0) { _brNone += need; _brNoneN++; if (c != null && c.Kingdom != null) BreadRow(c.Kingdom)[2] += need; }
                    }
                    catch (Exception e) { Stumble("BreadRest(osada)", e); }
                }
                _brLordN = _brLordToday.Count;
                LastBreadCrown = _brCrown; LastBreadLord = _brLord;
                BreadPlayer(s);
                if (s.LogEnabled) BreadLine(s, all);
            }
            catch (Exception e) { Stumble("BreadRest", e); }
            finally { _brIn = 0; _brOutN = 0; }
        }

        private static void BreadPlayer(Settings s)
        {
            try
            {
                var pc = Clan.PlayerClan;
                if (pc == null) return;
                int today = (int)CampaignTime.Now.ToDays;
                if (_brPlWeek < 0 || today < _brPlWeek) _brPlWeek = today;
                foreach (var l in _brIdx.Values)
                    foreach (var f in l)
                        if (f.St != null && f.St.OwnerClan == pc && f.BreadPaid > 0) { _brPlFief += f.BreadPaid; _brPlFiefCrown += f.BreadCrown; _brPlNames.Add(f.St.Name != null ? f.St.Name.ToString() : f.St.StringId); }
                var pk = pc.Kingdom;
                if (pk != null && pk.RulingClan == pc)
                {
                    var d = CrownIncome.DayOf(pk);
                    if (d != null) { _brPlKingBread += d.Bread; _brPlKingRent += d.NeedAidRent; }
                }
                if (today - _brPlWeek < 7) return;
                if (_brPlFief > 0)
                    Log.Player("The poor of " + string.Join(", ", new List<string>(_brPlNames).ToArray()) + " could not afford their bread; "
                               + (_brPlFiefCrown >= _brPlFief ? "the crown" : "the crown and others") + " paid " + _brPlFief + " denars for their food this past week.");
                if (_brPlKingBread > 0 || _brPlKingRent > 0)
                    Log.Player("This past week your crown paid " + _brPlKingRent + " denars of wages to field companies before rents and " + _brPlKingBread + " denars for bread in hungry towns.");
                _brPlWeek = today; _brPlFief = _brPlFiefCrown = _brPlKingBread = _brPlKingRent = 0; _brPlNames.Clear();
            }
            catch (Exception e) { Stumble("BreadPlayer", e); }
        }

        private static void BreadLine(Settings s, List<Folk> all)
        {
            int on = 0, onT = 0, onC = 0, gapN = 0; long gap = 0;
            foreach (var f in _f.Values) if (f.Kind <= 1 && f.BreadOn) { on++; if (f.Kind == 0) onT++; else onC++; }
            foreach (var f in all) { gap += f.BreadGap; gapN++; }
            all.Sort((a, b) => b.BreadGap.CompareTo(a.BreadGap));
            var top = new List<string>();
            for (int i = 0; i < all.Count && i < 5; i++)
            {
                var f = all[i];
                top.Add((f.St.Name != null ? f.St.Name.ToString() : f.St.StringId) + " " + f.BreadGap + " (korona " + f.BreadCrown + ", pan " + f.BreadLord + ")");
            }
            var rows = new List<KeyValuePair<long, string>>();
            foreach (var kv in _brByK)
            {
                long t = kv.Value[0] + kv.Value[1] + kv.Value[2];
                rows.Add(new KeyValuePair<long, string>(t, (kv.Key.Name != null ? kv.Key.Name.ToString() : kv.Key.StringId) + " " + kv.Value[0] + "/" + kv.Value[1] + "/" + kv.Value[2]));
            }
            rows.Sort((a, b) => b.Key.CompareTo(a.Key));
            var rt = new List<string>(); foreach (var r in rows) rt.Add(r.Value);
            var sb = new StringBuilder(1024);
            sb.Append("Chleb biedoty (M3): dzien ").Append((int)CampaignTime.Now.ToDays)
              .Append(" | Bread For The Poor ").Append(s.BreadForThePoor ? "tak" : "nie (chleb 0 - jak przed paczka)")
              .Append("; wejscie: glod z braku pieniedzy E5/C2 (Afford jedzenia < ").Append(Hunger.MoneyAfford.ToString("0.00", BrInv)).Append(" przez ").Append(Hunger.MoneyDays)
              .Append(" dob, kampania >= ").Append(((int)Hunger.FromDay).ToString(BrInv)).Append(" dob), wyjscie: ").Append(Hunger.MoneyDays).Append(" kotwic bez luki, cel ")
              .Append(BreadDays.ToString("0", BrInv)).Append(" dni jedzenia")
              .Append(" | osad na chlebie ").Append(on).Append(" (miasta ").Append(onT).Append(" / podzamcza ").Append(onC).Append("), weszlo dzis ").Append(_brIn).Append(", wyszlo dzis ").Append(_brOutN)
              .Append(" | luka ").Append(gap).Append(" zl w ").Append(gapN).Append(" osadach")
              .Append(" | zaplacono ").Append(_brCrown + _brLord).Append(": korona ").Append(_brCrown).Append(" (krolestw ").Append(_brCrownK).Append(", z reszty wplywow dnia przed rentami")
              .Append(s.CrownHoardFeedsHungry ? "; w tym ze skarbca ponad zapas wojny " + _brHoard.ToString(BrInv) : "").Append(')')
              .Append(", pan zastepczo ").Append(_brLord).Append(" (panow ").Append(_brLordN).Append("; najnizsze G/R placacego pana ")
              .Append(double.IsNaN(_brMinGR) ? "-" : _brMinGR.ToString("0.00", BrInv)).Append("), nie stac nikogo ").Append(_brNone).Append(" zl w ").Append(_brNoneN).Append(" osadach")
              .Append(" | kontrola: pan po wyplacie ponizej rezerwy wojny ").Append(_brBelowR).Append(" (oczekiwane 0), kiesa gracza ").Append(_brPlayerPurse).Append(" (oczekiwane 0)")
              // recenzja: pan w wojnie placi z czesci tempa skrzyni bez zoldu (o tyle mniej skrzyni sprzetu tej doby), w pokoju - z czesci bez skrzyni pokoju Z.1
              .Append(" | pan w wojnie ").Append(_brLordWar).Append(" zl (do ").Append(BreadLordShare(s, true).ToString("0.00", BrInv)).Append(" tempa skrzyni - ze skrzyni sprzetu)")
              .Append(", w pokoju ").Append(_brLordPeace).Append(" zl (do ").Append(BreadLordShare(s, false).ToString("0.00", BrInv)).Append(" tempa skrzyni)")
              .Append(" | top: ").Append(top.Count > 0 ? string.Join(", ", top.ToArray()) : "-")
              .Append(" | na krolestwo (korona/pan/nie stac nikogo): ").Append(rt.Count > 0 ? string.Join(", ", rt.ToArray()) : "-");
            // m105 G2: segment na koncu linii (regexy R2_M3 / R2_M3L bez zmian); klucz wylaczony - linia jak w WGRANIU 102
            if (s.CrownHoardFeedsHungry)
                sb.Append(" | m105 skarbiec korony (Crown Hoard Feeds Hungry TAK): ze skarbca ").Append(_brHoard).Append(" zl (krolestw ").Append(_brHoardK.Count).Append(", osad ")
                  .Append(_brHoardN).Append("), nie stac nikogo po skarbcu ").Append(_brNone).Append(" zl (w tym przy wolnym limicie skarbca ").Append(_brHoardLeft)
                  .Append(" zl - oczekiwane 0), kontrola skarbiec -").Append(_brHoardTook).Append(" + kiesy ludu ").Append(_brHoard).Append(" = ").Append(_brHoard - _brHoardTook)
                  .Append(", ponizej zapasu wojny po wyplacie ").Append(_brHoardBelow).Append(", limit doby 1/")
                  .Append(KingdomTreasury.CsDays(s).ToString("0", BrInv)).Append(" nadwyzki skarbca (wspolny z rachunkami KS-H)");
            sb.Append('.');
            Log.Info(sb.ToString());
        }
    }
}
