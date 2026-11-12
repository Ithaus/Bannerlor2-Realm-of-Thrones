using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 314c - ZOLD WASALA I NAGRODA KROLA (Jeff 01.11: "naleze do krolestwa Polnocy, czemu jak pokonuje wroga nie dostaje za to kasy ... tylko najemnicy
    /// dostaja kontrakty a ja nie"; odpowiedz na propozycje: "tak zrob a i b").
    ///
    /// A. ZOLD KROLEWSKI PO 40 DNIACH SLUZBY (klucz RoyalWarWageEnabled):
    ///  - SLUZBA = dzien, w ktorym przy rozliczeniu korony co najmniej jedna partia wojenna rodu (Clan.WarPartyComponents) jest w armii WLASNEGO krolestwa
    ///    (MobileParty.Army.Kingdom == Clan.Kingdom - armia krola albo jego marszalka, takze partia w drodze na zbiorke), a krolestwo jest w wojnie z innym
    ///    krolestwem (KingdomTreasury.AtWar - ta sama miara co zwrot zoldu). Najemnicy (kontrakt 185 i gra) i rod krola (korona nie placi sama sobie) - bez zoldu.
    ///  - Pierwsze RoyalWarWageFreeDays (40) dni sluzby w roku kalendarzowym (CampaignTime.GetYear - rok Armoury 364 dni, Calendar.cs) rod sluzy za darmo
    ///    (powinnosc lenna); od 41. dnia sluzby korona placi glowie rodu co dobe RoyalWarWagePercent (50%) dziennego zoldu kazdego szeregowego tych partii
    ///    (CharacterObject.TroopWage - ten sam model zoldu, ktory placi rod; bohaterowie poza zoldem). Licznik dni na rod, w zapisie (arm_zold314).
    ///  - SKAD: najpierw reszta wplywow dnia PONAD garnek W5 (recenzja 314c: garnek daniny wojennej W5 i potrzeba W5 z wplywow - jak rezerwa M2b w CrownCallToWar -
    ///    zostaje w wplywach dla W5; CrownIncome.LeftFor, Spent), potem skarbiec PONAD ZAPAS WOJNY i dlugi (KingdomTreasury.TreasurySurplusFor -
    ///    ta sama miara co W5b / f23 / "najpierw skarbiec" 8a: krol placi wojne ze skarbca do zapasu, nigdy z kredytu). Gdy nie starcza - wszystkim rodom
    ///    krolestwa proporcjonalnie mniej (KingdomTreasury.Split), niedoplata przepada (bez dlugu korony). Zloto: skarbiec -> kiesa glowy rodu.
    ///  - ZWROT 70% (CrownWageRefundEnabled) a zold 314c - jeden zloty zoldu korona oddaje najwyzej raz: zold krolewski wyplacony wczoraj jest CIECIEM podstawy
    ///    zwrotu nastepnej doby (RefundCut, jak W5 / W7 / 277 - "korona nie zwraca zoldu, ktory juz oplacila"). Zwrot (pierwszy z wydatkow wojska) biegnie
    ///    przed tym krokiem i nie traci przez niego wplywow dnia (bez tego zold 314c pchalby zwrot do Banku 186). Bogaty rod (f = 0, dzis bez zwrotu) dostaje p.
    ///  - RODZINA W5 (recenzja 314c): zold 314c tej doby jest czescia wlasnego pulapu rodu Pc0 w NeedAidKingdom (PaidToday) - W5, W5b, W6, M1, W5c i W7 (plan W5)
    ///    licza gotowke jako koszt - Pc0, wiec nie placa drugi raz za tych samych ludzi; ClanBudget dolicza go do b.Aid (pulap druzyn) i b.AidCash; bez wpisu do D (recenzja 314x, wzor 277). Gotowka pana lennego 277 tej doby
    ///    (CanonHouses277 placi wczesniej w dobie) zmniejsza nalezny zold 314c. Suma korony i pana za szeregowych w armii: najwyzej koszt rodu przy poziomie
    ///    W5 (gdy W5 doplaca) albo p x zold (gdy nie), plus zwrot 70% f od reszty oplaconej z wlasnej kiesy.
    ///
    /// B. NAGRODA KROLA ZA WIELKIE ZWYCIESTWO (klucz RoyalVictoryRewardEnabled), MapEventEnded:
    ///  - bitwa z wynikiem, w ktorej strona pobita miala co najmniej jedna PARTIE LORDA (IsLordParty, frakcja nie bandycka) z frakcji w wojnie z krolestwem
    ///    zwyciezcy. Bandy, zbojcy, karawany, milicja - nic. "Armia" = co najmniej 2 pobite partie lordow (Army bywa juz rozwiazana przy MapEventEnded).
    ///  - pula krolestwa K = min(RoyalVictoryGoldCap, RoyalVictoryGoldPerEnemy x zdrowi na starcie pobitych partii lordow wrogow K x (armia ? RoyalVictoryArmyMultiplier : 1))
    ///    x udzial partii lordow K w wkladzie zwyciezcow (ContributionToBattle; zero wszedzie - zdrowi na starcie). Wewnatrz K - rod wedlug wkladu; czesc krola
    ///    i najemnikow zostaje w skarbcu. Wplywy: RoyalVictoryInfluencePerLord x pobitych lordow (x mnoznik armii), tym samym kluczem, od razu
    ///    (ChangeClanInfluenceAction - jak wplywy bitwy w grze; wplywy to nie zloto).
    ///  - zloto daru czeka w kolejce (zapis) do rozliczenia korony: ten sam skarbiec co zold (najpierw dary, potem zold), najdluzej RoyalVictoryWaitDays dni,
    ///    potem reszta przepada (linia). Zloto: skarbiec -> kiesa glowy rodu.
    ///  - 316c RAZ NA DOBE (audyt 02.11: ta sama armia Flinta nagrodzona dwa razy w 11 s - poscig za jej resztkami, 20 lordow, 3448 i 744 ludzi, wplywy
    ///    130.8 + 124.5): pobita partia lorda (StringId) liczy sie do nagrody krolestwa K najwyzej raz na dobe - drugi raz tej doby nie daje ani lorda
    ///    (wplywy, prog armii), ani ludzi (zloto: jej ludzie na starcie byli juz w puli pierwszej bitwy - 744 z 3448). Klucz K|partia, bo dwa krolestwa
    ///    sojusznikow w jednej bitwie dostaja kazde swoja nagrode. Zbior w pamieci, czyszczony przy zmianie doby (bez zapisu - wczytanie w polowie doby
    ///    najwyzej pozwoli nagrodzic raz jeszcze; zgubienie gorsze nie jest). Bitwa z samymi juz nagrodzonymi - linia "Nagroda krola (314c) pominieta".
    ///
    /// 431 (audyt ekonomii B4, decyzja Jeffa 4 - klucz RoyalWarWageLoan, CrownWarLoan431.cs): zold A nie jest juz darem dla kazdego - korona daje go tylko rodom
    /// W POTRZEBIE (x f, ta sama miara co renty f7) i jako POZYCZKE (roszczenie KCrownLoan drabiny 168, maly procent, limit, splata po wojnie). Skarbiec, kolejnosc,
    /// PaidToday i ciecie zwrotu bez zmian; klucz wylaczony = wszystko ponizej co do zlotowki jak dotad. Nagroda B (dar za zwyciestwo) bez zmian.
    ///
    /// Gracz i AI na tych samych zasadach. Liczniki KDay.Wage314 / Gift314 ida do rozkladu "Skarbce:" (312e, czesc "nagrody 178"). Linie "Zold krolewski (314c)"
    /// i "Nagroda krola (314c)". Wyjatek per krolestwo / rod / bitwa liczony (potkniecia w linii, pierwszy blad miejsca do logu) - bez globalnego wylacznika.
    /// </summary>
    internal static class CrownWar314
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class Svc { public int Year, Days; public string Kingdom; }   // recenzja 314c: krolestwo, ktoremu rod sluzy - zmiana krolestwa = dni od zera
        private sealed class Gift { public string Clan, Kingdom; public long Left; public int Day; public long Paid; }   // Paid - 326d recenzja: wyplacone dotad (pamiec, bez zapisu; tylko okno lupu)

        private static readonly Dictionary<string, Svc> _svc = new Dictionary<string, Svc>();          // id rodu -> rok i dni sluzby w tym roku
        private static readonly Dictionary<string, long> _paid = new Dictionary<string, long>();       // id rodu -> zold 314c wyplacony w dobie _paidDay (ciecie zwrotu nastepnej doby)
        private static int _paidDay = -1;
        // 431-p (recenzja S1): czesc _paid, ktora jest POZYCZKA korony (klucz Royal War Wage Loan Keeps Refund) - zostaje w PaidToday (gotowka na tych samych ludzi:
        // pulap W5, b.Aid budzetu), ale NIE tnie podstawy zwrotu 70% nastepnej doby (pozyczka nie jest darem - korona nie "oplacila" tego zoldu)
        private static readonly Dictionary<string, long> _lentKeep = new Dictionary<string, long>();
        private static readonly List<Gift> _gifts = new List<Gift>();
        // 326d: dar gracza z ostatnich bitew (bitwa -> wpis kolejki) - tylko odczyt dla okna lupu (SpoilsWindow); bez zapisu, najwyzej 16 pozycji, czyszczone w Reset
        private static readonly List<KeyValuePair<MapEvent, Gift>> _plGiftBy = new List<KeyValuePair<MapEvent, Gift>>();
        private static MapEvent _curMe;

        /// <summary>326d (recenzja): dar krola dla rodu gracza za te bitwe - paid: juz wyplacone (o swicie, gdy skarbiec mial zloto), owed: jeszcze w kolejce,
        /// lapsed: przepadlo (pusty skarbiec przez RoyalVictoryWaitDays dni albo rod poza krolestwem). Tylko odczyt.</summary>
        internal static void PlayerGiftFor(MapEvent me, ref long paid, ref long owed, ref long lapsed)
        {
            if (me == null) return;
            foreach (var kv in _plGiftBy)
            {
                if (!ReferenceEquals(kv.Key, me) || kv.Value == null) continue;
                var g = kv.Value;
                paid += g.Paid;
                if (g.Left > 0) { if (_gifts.Contains(g)) owed += g.Left; else lapsed += g.Left; }
            }
        }

        private static int _stumbles, _importN = -1, _importBad;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static readonly HashSet<string> _rewarded = new HashSet<string>();   // 316c: "krolestwo|partia" nagrodzone w dobie _rewardedDay
        private static int _rewardedDay = -1;

        // liczniki doby (zerowane po linii)
        private static int _dWarK, _dSvc, _dFree, _dPaidN, _dMen, _dShortK, _dBattles, _dGiftClans;
        private static long _dDue, _dPaidAmt, _dFromLeft, _dFromStock, _dCut, _dGiftPaid, _dGiftLapsed, _dGiftQueued, _dInfl, _dKeptW5, _dCut277;
        private static long _dPlWage, _dPlGift; private static int _dPlDays = -1;

        internal static bool WageOn { get { var s = Settings.Current; return s != null && s.RoyalWarWageEnabled; } }
        internal static bool GiftOn { get { var s = Settings.Current; return s != null && s.RoyalVictoryRewardEnabled; } }

        internal static void Reset()
        {
            _svc.Clear(); _paid.Clear(); _lentKeep.Clear(); _paidDay = -1; _gifts.Clear(); _stumbles = 0; _err.Clear(); _importN = -1; _importBad = 0; _rewarded.Clear(); _rewardedDay = -1; _plGiftBy.Clear(); _curMe = null; ZeroDay();
        }

        private static void ZeroDay()
        {
            _dWarK = _dSvc = _dFree = _dPaidN = _dMen = _dShortK = _dBattles = _dGiftClans = 0;
            _dDue = _dPaidAmt = _dFromLeft = _dFromStock = _dCut = _dGiftPaid = _dGiftLapsed = _dGiftQueued = _dInfl = _dKeptW5 = _dCut277 = 0;
            _dPlWage = _dPlGift = 0; _dPlDays = -1;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("CrownWar314." + where, e); } catch { }
        }

        private static string Name(Clan c) { try { return c != null && c.Name != null ? c.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Name(IFaction f) { try { return f != null && f.Name != null ? f.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Name(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "-"; } catch { return "?"; } }

        private static string PlayerClanId() { try { return Clan.PlayerClan != null ? Clan.PlayerClan.StringId : null; } catch { return null; } }

        private static Clan ClanById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var c in Clan.All) if (c != null && c.StringId == id) return c;
            return null;
        }

        private static Kingdom KingdomById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var k in Kingdom.All) if (k != null && k.StringId == id) return k;
            return null;
        }

        private static int FreeDays(Settings s) { return Math.Max(0, Math.Min(364, s.RoyalWarWageFreeDays)); }

        // ------------------------------------------------------------ ciecie zwrotu 70% (KingdomTreasury.WageRefund)
        /// <summary>KingdomTreasury.WageRefund (po cieciach W5 / W7 / 277): ile z zoldu `paid` rodu oplacila juz wczoraj korona zoldem 314c - ta czesc nie jest
        /// podstawa zwrotu (korona nie oddaje dwa razy tego samego zlotego). Zuzywa wykorzystana czesc. 0 bez wczorajszego zoldu 314c. Nie rzuca.</summary>
        internal static long RefundCut(Clan c, long paid)
        {
            try
            {
                long have;
                if (c == null || paid <= 0 || Campaign.Current == null || c.StringId == null) return 0;
                if (_paidDay != (int)CampaignTime.Now.ToDays - 1 || !_paid.TryGetValue(c.StringId, out have) || have <= 0) return 0;
                long keep; _lentKeep.TryGetValue(c.StringId, out keep);   // 431-p: pozyczka korony nie jest podstawa ciecia
                keep = Math.Max(0, Math.Min(keep, have));
                long cut = Math.Min(paid, have - keep);
                if (cut <= 0) return 0;
                _paid[c.StringId] = have - cut;
                _dCut += cut;
                return cut;
            }
            catch (Exception e) { Stumble("RefundCut", e); return 0; }
        }

        /// <summary>Recenzja 314c (rodzina W5): zold 314c wyplacony rodowi DZIS (krok biegnie przed W5 w CrownRents.Daily i przed budzetem 166). 0 - inna doba albo brak. Nie rzuca.</summary>
        internal static long PaidToday(Clan c)
        {
            try
            {
                long v;
                return c != null && c.StringId != null && Campaign.Current != null && _paidDay == (int)CampaignTime.Now.ToDays && _paid.TryGetValue(c.StringId, out v) ? Math.Max(0, v) : 0;
            }
            catch (Exception e) { Stumble("PaidToday", e); return 0; }
        }

        // ------------------------------------------------------------ skarbiec: reszta wplywow dnia, potem nadwyzka ponad zapas wojny
        private sealed class Purse { public long Left, Stock; }

        private static Purse PurseOf(Kingdom k, Dictionary<Kingdom, Purse> cache)
        {
            Purse p;
            if (cache.TryGetValue(k, out p)) return p;
            p = new Purse();
            long left = CrownIncome.LeftFor(k);
            long res;
            double sur = KingdomTreasury.TreasurySurplusFor(k, out res);   // skarbiec - reszta wplywow dnia - zapas wojny - dlugi (Bank, miasta); nigdy z kredytu
            long wallet = Math.Max(0, (long)k.KingdomBudgetWallet);
            // recenzja 314c (wzor M2b, CrownCallToWar): garnek W5 siedzi w tej samej reszcie wplywow dnia - zostaje dla W5 (wieksze z dzisiejszego garnka daniny
            // wojennej i z tego, czego W5 wczoraj potrzebowalo z wplywow: garnek + niepokryta potrzeba przed M1); przy wylaczonym W5 - nic nie zostawiamy
            long keep = 0;
            if (KingdomTreasury.NeedAidOn)
            {
                long pot = 0;
                try { var kd = CrownIncome.DayOf(k); if (kd != null) pot = Math.Max(0, DsTowns.FarmsLive ? DsSeason.WarPot(k, kd) + Math.Max(0, kd.WarAid) : kd.WarAid); } catch (Exception e) { Stumble("PurseOf(garnek W5)", e); }
                keep = Math.Max(0, Math.Min(left, Math.Max(pot, KingdomTreasury.NeedAidFromIncomeYesterday(k))));
            }
            p.Left = Math.Max(0, Math.Min(left - keep, wallet));
            p.Stock = Math.Max(0, Math.Min((long)Math.Floor(sur), wallet - left));   // od calej reszty wplywow - czesc zostawiona dla W5 nie przecieka do nadwyzki
            _dKeptW5 += keep;
            cache[k] = p;
            return p;
        }

        /// <summary>Przelew skarbiec -> kiesa glowy rodu: najpierw z reszty wplywow dnia (Spent), potem z nadwyzki. Zwraca wyplacone (najwyzej `want`).</summary>
        private static int Pay(Kingdom k, Purse p, Hero h, long want, bool wage)
        {
            if (k == null || p == null || h == null || want <= 0) return 0;
            long fromLeft = Math.Min(want, p.Left);
            long fromStock = Math.Min(want - fromLeft, p.Stock);
            long x = Math.Min(fromLeft + fromStock, Math.Max(0, (long)k.KingdomBudgetWallet));
            if (x <= 0) return 0;
            if (x < fromLeft + fromStock) { fromLeft = Math.Min(fromLeft, x); fromStock = x - fromLeft; }
            int ix = (int)Math.Min(int.MaxValue, x);
            k.KingdomBudgetWallet -= ix;                       // najpierw skarbiec, potem odbiorca: skarbiec oddaje dokladnie tyle, ile dostaje glowa rodu
            h.ChangeHeroGold(ix);
            CirculationWindows.NoteHeroGold(h, ix);
            // recenzja 314x (wzor 277): zold NIE idzie do D rodu - miejsce w pulapie daje b.Aid tej doby (ClanBudget: PaidToday w aid), wpis do D liczylby te sama
            // zlotowke drugi raz (W5 widzialby ja w Pc0 i w D i dawalby o caly zold za malo); dar - jednorazowy wplyw (KRansom)
            if (!wage) ClanIncomeBook.NoteInflow(h, ix, ClanIncomeBook.KRansom);
            if (fromLeft > 0) CrownIncome.Spent(k, fromLeft);
            p.Left -= fromLeft; p.Stock -= fromStock;
            _dFromLeft += fromLeft; _dFromStock += fromStock;
            try { var kd = CrownIncome.DayOf(k); if (kd != null) { if (wage) kd.Wage314 += ix; else kd.Gift314 += ix; } } catch { }   // tylko licznik rozkladu "Skarbce:" (312e)
            return ix;
        }

        // ------------------------------------------------------------ rozliczenie doby (blok korony, po zwrocie zoldu i SeizePledged, przed rentami 180)
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            int day = (int)CampaignTime.Now.ToDays;
            var cache = new Dictionary<Kingdom, Purse>();
            try { PayGifts(s, day, cache); } catch (Exception e) { Stumble("Daily(dary)", e); }
            try { PayWages(s, day, cache); } catch (Exception e) { Stumble("Daily(zold)", e); }
            try { Report(s, day); } catch (Exception e) { Stumble("Report", e); }
            finally { ZeroDay(); }
        }

        private static void PayGifts(Settings s, int day, Dictionary<Kingdom, Purse> cache)
        {
            if (_gifts.Count == 0) return;
            int wait = Math.Max(1, Math.Min(120, s.RoyalVictoryWaitDays));
            long plLapsed = 0;   // 326d: przepadle dary gracza tej doby - do komunikatu
            foreach (var g in _gifts.ToArray())
            {
                try
                {
                    var k = KingdomById(g.Kingdom); var c = ClanById(g.Clan);
                    var h = c != null && !c.IsEliminated ? c.Leader : null;
                    if (k == null || k.IsEliminated || c == null || c.Kingdom != k || c.IsUnderMercenaryService || c == k.RulingClan || h == null || !h.IsAlive)
                    {
                        if (g.Clan == PlayerClanId()) plLapsed += g.Left;
                        // nikt nie odbierze (rod poza krolestwem, wymarly) albo rod zostal krolem / najemnikiem (recenzja 314c: ta sama zasada co przy wpisie i w zoldzie)
                        // - dar przepada, zloto zostaje w skarbcu
                        _dGiftLapsed += g.Left; _gifts.Remove(g); continue;
                    }
                    int got = Pay(k, PurseOf(k, cache), h, g.Left, false);
                    if (got > 0)
                    {
                        g.Left -= got; g.Paid += got; _dGiftPaid += got;
                        if (c == Clan.PlayerClan) _dPlGift += got;
                    }
                    if (g.Left <= 0) { _gifts.Remove(g); continue; }
                    if (day - g.Day >= wait) { _dGiftLapsed += g.Left; if (c == Clan.PlayerClan) plLapsed += g.Left; _gifts.Remove(g); }   // skarbiec nie mial zlota przez RoyalVictoryWaitDays dni - reszta przepada
                }
                catch (Exception e) { Stumble("PayGifts", e); }
            }
            // 326d (recenzja): dary moga isc ratami (skarbiec bez zlota) i z kilku bitew naraz - komunikat mowi, ile wplynelo, ile jeszcze czeka i ile przepadlo
            long plOwed = 0;
            try { string pid = PlayerClanId(); foreach (var g in _gifts) if (g.Clan == pid && g.Left > 0) plOwed += g.Left; } catch { }
            if (_dPlGift > 0 || plLapsed > 0)
                Log.Player((_dPlGift > 0 ? "The king's treasury paid " + _dPlGift + " denars of your victory rewards into your purse at dawn" : "The king's treasury paid nothing of your victory rewards at dawn")
                           + (plOwed > 0 ? "; " + plOwed + " still owed, paid when there is gold in it" : "")
                           + (plLapsed > 0 ? "; " + plLapsed + " lapsed (the treasury had no gold in time)" : "") + ".");
        }

        private sealed class Row { public Clan C; public long Base; public int Men; }

        private static void PayWages(Settings s, int day, Dictionary<Kingdom, Purse> cache)
        {
            _paid.Clear(); _lentKeep.Clear(); _paidDay = day;   // wczorajszy zold zuzyty juz przez dzisiejszy zwrot (WageRefund biegnie wczesniej w bloku)
            if (!s.RoyalWarWageEnabled) return;
            int year = CampaignTime.Now.GetYear, free = FreeDays(s);
            double pct = Math.Max(0f, Math.Min(100f, s.RoyalWarWagePercent)) / 100.0;
            bool loan = CrownWarLoan431.On;   // 431: zold tylko rodom w potrzebie i jako pozyczka korony (klucz wylaczony - dar jak dotad)
            bool keepRefund = loan && s.RoyalWarWageLoanKeepsRefund;   // 431-p: pozyczka nie tnie zwrotu 70% i nie przekracza zoldu niepokrytego dzisiejszym zwrotem
            foreach (var k in Kingdom.All)
            {
                if (k == null || k.IsEliminated) continue;
                try
                {
                    if (!KingdomTreasury.AtWar(k)) continue;
                    _dWarK++;
                    var rows = new List<Row>();
                    foreach (var c in k.Clans)
                    {
                        try
                        {
                            if (c == null || c.IsEliminated || c.Leader == null || !c.Leader.IsAlive || c.StringId == null) continue;
                            if (c.IsUnderMercenaryService || c == k.RulingClan) continue;   // najemnicy - kontrakt; krol - sam sobie nie placi
                            long wage = 0; int men = 0; bool serving = false;
                            foreach (var wp in c.WarPartyComponents)
                            {
                                var mp = wp != null ? wp.MobileParty : null;
                                if (mp == null || mp.Army == null || mp.Army.Kingdom != k || mp.MemberRoster == null) continue;
                                serving = true;
                                var r = mp.MemberRoster;
                                for (int i = 0; i < r.Count; i++)
                                {
                                    var ch = r.GetCharacterAtIndex(i);
                                    if (ch == null || ch.IsHero) continue;
                                    int n = r.GetElementNumber(i);
                                    if (n <= 0) continue;
                                    men += n; wage += (long)ch.TroopWage * n;
                                }
                            }
                            if (!serving) continue;
                            Svc v;
                            if (!_svc.TryGetValue(c.StringId, out v)) { v = new Svc { Year = year, Kingdom = k.StringId }; _svc[c.StringId] = v; }
                            if (v.Year != year) { v.Year = year; v.Days = 0; }       // nowy rok kalendarzowy - znow 40 dni bez zaplaty
                            if (v.Kingdom != k.StringId) { v.Kingdom = k.StringId; v.Days = 0; }   // recenzja 314c: nowy pan - 40 dni bez zaplaty winne nowej koronie
                            v.Days++;
                            _dSvc++;
                            if (c == Clan.PlayerClan) _dPlDays = v.Days;
                            if (v.Days <= free) { _dFree++; continue; }
                            long due = (long)Math.Floor(wage * pct);
                            // recenzja 314c: gotowka pana lennego 277 tej doby (CanonHouses277 placi wczesniej w dobie) oplacila juz czesc tych ludzi - jeden zloty raz
                            long c277 = Math.Min(due, (long)Math.Floor(Math.Max(0, CanonHouses277.CashOf(c))));
                            if (c277 > 0) { due -= c277; _dCut277 += c277; }
                            if (due <= 0) continue;
                            if (loan) { due = CrownWarLoan431.Want(c, k, due, year, wage); if (due <= 0) continue; }   // 431: x f potrzeby, limit dlugu, zgoda gracza; 431-p: najwyzej zold niepokryty zwrotem
                            rows.Add(new Row { C = c, Base = due, Men = men });
                        }
                        catch (Exception e) { Stumble("PayWages(rod)", e); }
                    }
                    if (rows.Count == 0) continue;
                    var dueArr = new int[rows.Count];
                    long want = 0;
                    for (int i = 0; i < rows.Count; i++) { dueArr[i] = (int)Math.Min(int.MaxValue, rows[i].Base); want += dueArr[i]; }
                    var p = PurseOf(k, cache);
                    var give = KingdomTreasury.Split(dueArr, p.Left + p.Stock);
                    long got = 0;
                    for (int i = 0; i < rows.Count; i++)
                    {
                        var c = rows[i].C;
                        int x = give[i] > 0 ? Pay(k, p, c.Leader, give[i], true) : 0;
                        _dDue += dueArr[i]; _dMen += rows[i].Men;
                        if (x <= 0) continue;
                        _dPaidN++; _dPaidAmt += x; got += x;
                        if (loan) CrownWarLoan431.Lent(c, k, x);   // 431: wyplacone = dlug rodu wobec tej korony (ksiega drabiny 168)
                        long prev; _paid.TryGetValue(c.StringId, out prev); _paid[c.StringId] = prev + x;
                        if (keepRefund) { long pk; _lentKeep.TryGetValue(c.StringId, out pk); _lentKeep[c.StringId] = pk + x; }
                        if (c == Clan.PlayerClan) _dPlWage += x;
                    }
                    if (got < want) _dShortK++;
                }
                catch (Exception e) { Stumble("PayWages(" + Name(k) + ")", e); }
            }
            if (_dPlWage > 0) Log.Player(loan ? CrownWarLoan431.PlayerDayText(_dPlWage) : "The crown pays your house " + _dPlWage + " denars of war wages for the men serving in the king's army.");
        }

        private static void Report(Settings s, int day)
        {
            if (!s.LogEnabled) return;
            if (!WageOn && !GiftOn && _gifts.Count == 0 && _dGiftPaid == 0 && _dGiftLapsed == 0) return;   // recenzja 314c: linia takze w dobie, w ktorej kolejka sie oproznila
            long pend = 0; foreach (var g in _gifts) pend += g.Left;
            Svc pv = null;
            try
            {
                var pc = Clan.PlayerClan;
                if (pc != null && pc.StringId != null && _svc.TryGetValue(pc.StringId, out pv) && (pc.Kingdom == null || pv.Kingdom != pc.Kingdom.StringId)) pv = null;   // dni u innej korony sie nie licza
            }
            catch { }
            int year = CampaignTime.Now.GetYear;
            var sb = new StringBuilder(600);
            sb.Append("Zold krolewski (314c): dzien ").Append(day)
              .Append(" | zold ").Append(WageOn ? "tak" : "nie").Append(" (").Append(Math.Max(0f, Math.Min(100f, s.RoyalWarWagePercent)).ToString("0.#", Inv)).Append("% zoldu szeregowych, ")
              .Append(FreeDays(s)).Append(" dni w roku bez zaplaty)")
              .Append(" | krolestw w wojnie ").Append(_dWarK)
              .Append(" | rody w armiach ").Append(_dSvc).Append(" (bez zaplaty ").Append(_dFree).Append(", platnych ").Append(_dSvc - _dFree).Append(", wyplacono ").Append(_dPaidN).Append(')')
              .Append(", szeregowych platnych ").Append(_dMen)
              .Append(" | nalezne ").Append(_dDue).Append(", wyplacone ").Append(_dPaidAmt)
              .Append(CrownWarLoan431.On ? " (431: nalezne juz wedlug potrzeby rodu i limitu, wyplacone to POZYCZKA korony - linia 'Pozyczka korony na zold (431)')" : "")
              .Append(" | dary za zwyciestwo: wyplacone ").Append(_dGiftPaid).Append(", w kolejce ").Append(pend).Append(" (pozycji ").Append(_gifts.Count).Append("), przepadlo ").Append(_dGiftLapsed)
              .Append(" | ze skarbca razem ").Append(_dFromLeft + _dFromStock).Append(" (z wplywow dnia ").Append(_dFromLeft).Append(", ponad zapas wojny ").Append(_dFromStock)
              .Append("; wplywy dnia zostawione dla W5 ").Append(_dKeptW5).Append(')')
              .Append(", niedoplata zoldu ").Append(Math.Max(0, _dDue - _dPaidAmt)).Append(" w ").Append(_dShortK).Append(" krolestwach")
              .Append(" | ciecie zwrotu 70% (zold 314c z wczoraj) ").Append(_dCut).Append(", zold 314c mniej o gotowke pana 277 ").Append(_dCut277)
              .Append(" | gracz: dni sluzby ").Append(pv != null && pv.Year == year ? pv.Days : 0).Append('/').Append(FreeDays(s))
              .Append(", zold ").Append(_dPlWage).Append(", dar ").Append(_dPlGift)
              .Append(" | potkniecia ").Append(_stumbles).Append('.');
            if (_importN >= 0) { sb.Append(" Wczytano z zapisu: wpisow ").Append(_importN).Append(" (bledne ").Append(_importBad).Append(")."); _importN = -1; }
            Log.Info(sb.ToString());
        }

        // ------------------------------------------------------------ nagroda krola (MapEventEnded)
        private sealed class Win { public Clan C; public long Contrib; }
        private sealed class Foe { public IFaction F; public int Men; public string Id; }   // 316c: Id - StringId partii (raz na dobe)

        internal static void OnMapEventEnded(MapEvent me)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.RoyalVictoryRewardEnabled || me == null || Campaign.Current == null || !me.HasWinner) return;
                var lose = me.GetMapEventSide(me.DefeatedSide); var win = me.GetMapEventSide(me.WinningSide);
                if (lose == null || win == null) return;
                // pobite partie lordow (bez band - frakcja bandycka, bez karawan, milicji i zalog)
                var foes = new List<Foe>();
                string foeLead = null;
                foreach (var mep in lose.Parties)
                {
                    var mp = mep != null && mep.Party != null ? mep.Party.MobileParty : null;
                    if (mp == null || !mp.IsLordParty) continue;
                    var f = mp.MapFaction;
                    if (f == null || f.IsBanditFaction) continue;
                    foes.Add(new Foe { F = f, Men = Math.Max(0, mep.HealthyManCountAtStart), Id = mp.StringId });
                    if (foeLead == null) { var oc = mp.ActualClan; foeLead = Name(oc != null ? (IFaction)oc : f); }
                }
                if (foes.Count == 0) return;
                // zwyciezcy: partie lordow (i druzyna gracza) wedlug wkladu
                var wins = new List<Win>(); long all = 0, allMen = 0;
                foreach (var mep in win.Parties)
                {
                    var mp = mep != null && mep.Party != null ? mep.Party.MobileParty : null;
                    if (mp == null || !(mp.IsLordParty || mp.IsMainParty)) continue;
                    var c = mp.ActualClan;
                    if (c == null) continue;
                    wins.Add(new Win { C = c, Contrib = Math.Max(0, mep.ContributionToBattle) });
                    all += Math.Max(0, mep.ContributionToBattle); allMen += Math.Max(0, mep.HealthyManCountAtStart);
                }
                if (wins.Count == 0) return;
                bool byMen = all <= 0;                             // nikt nie ma wkladu (np. wrog sie poddal) - wedlug zdrowych na starcie
                if (byMen)
                {
                    int i = 0;
                    foreach (var mep in win.Parties)
                    {
                        var mp = mep != null && mep.Party != null ? mep.Party.MobileParty : null;
                        if (mp == null || !(mp.IsLordParty || mp.IsMainParty) || mp.ActualClan == null) continue;
                        wins[i++].Contrib = Math.Max(0, mep.HealthyManCountAtStart);
                    }
                    all = allMen;
                }
                if (all <= 0) return;
                var kingdoms = new List<Kingdom>();
                foreach (var w in wins) { var k = w.C.Kingdom; if (k != null && !k.IsEliminated && !kingdoms.Contains(k)) kingdoms.Add(k); }
                _dBattles++;
                foreach (var k in kingdoms)
                {
                    try { _curMe = me; RewardKingdom(s, k, foes, wins, all, foeLead); }
                    catch (Exception e) { Stumble("RewardKingdom(" + Name(k) + ")", e); }
                    finally { _curMe = null; }
                }
            }
            catch (Exception e) { Stumble("OnMapEventEnded", e); }
        }

        private static void RewardKingdom(Settings s, Kingdom k, List<Foe> foes, List<Win> wins, long all, string foeLead)
        {
            var day = (int)CampaignTime.Now.ToDays;
            if (day != _rewardedDay) { _rewarded.Clear(); _rewardedDay = day; }   // 316c: nowa doba - partie znow do nagrody
            int lords = 0, dup = 0; long men = 0;
            var fresh = new List<string>();
            foreach (var f in foes)
            {
                if (f.F == null || f.F == (IFaction)k || !k.IsAtWarWith(f.F)) continue;
                string key = f.Id != null ? k.StringId + "|" + f.Id : null;
                if (key != null && _rewarded.Contains(key)) { dup++; continue; }   // 316c: ta partia dala juz dzis nagrode temu krolestwu (poscig za resztkami)
                lords++; men += f.Men;
                if (key != null) fresh.Add(key);
            }
            if (lords == 0)
            {
                var sl = Settings.Current;
                if (dup > 0 && sl != null && sl.LogEnabled)
                    Log.Info("Nagroda krola (314c) pominieta: dzien " + day + " | " + Name(k) + " pokonal " + (foeLead ?? "-") + " | partii juz nagrodzonych dzis " + dup
                             + " (316c: partia lorda raz na dobe), nowych 0.");
                return;
            }
            bool army = lords >= 2;
            double mult = army ? Math.Max(1f, s.RoyalVictoryArmyMultiplier) : 1.0;
            long kc = 0; foreach (var w in wins) if (w.C.Kingdom == k) kc += w.Contrib;
            if (kc <= 0) return;
            foreach (var key in fresh) _rewarded.Add(key);   // 316c: dopiero gdy nagroda naprawde liczona (krolestwo ma wklad)
            double share = (double)kc / all;
            double gold = Math.Min(Math.Max(0, s.RoyalVictoryGoldCap), Math.Max(0f, s.RoyalVictoryGoldPerEnemy) * men * mult) * share;
            double infl = Math.Max(0f, s.RoyalVictoryInfluencePerLord) * lords * mult * share;
            // wklad na rod (rod moze miec kilka partii)
            var byClan = new Dictionary<Clan, long>();
            foreach (var w in wins) { if (w.C.Kingdom != k) continue; long v; byClan.TryGetValue(w.C, out v); byClan[w.C] = v + w.Contrib; }
            long queued = 0; double gave = 0; int n = 0; long kept = 0;
            string pl = "";
            foreach (var kv in byClan)
            {
                var c = kv.Key;
                double part = (double)kv.Value / kc;
                long g = (long)Math.Floor(gold * part);
                float iv = (float)(infl * part);
                if (c.IsEliminated || c.IsUnderMercenaryService || c == k.RulingClan || c.StringId == null) { kept += g; continue; }   // krol i najemnicy - czesc zostaje w skarbcu
                Gift gq = null;
                if (g > 0) { gq = new Gift { Clan = c.StringId, Kingdom = k.StringId, Left = g, Day = day }; _gifts.Add(gq); queued += g; }
                float applied = 0f;
                if (iv >= 0.5f) { try { ChangeClanInfluenceAction.Apply(c, iv); applied = iv; gave += iv; } catch (Exception e) { Stumble("Influence", e); } }
                n++;
                if (c == Clan.PlayerClan)
                {
                    if (gq != null && _curMe != null) { _plGiftBy.Add(new KeyValuePair<MapEvent, Gift>(_curMe, gq)); if (_plGiftBy.Count > 16) _plGiftBy.RemoveAt(0); }   // 326d: okno lupu
                    pl = "; gracz: dar " + g + ", wplywy " + applied.ToString("0.#", Inv);
                    // recenzja 314c: komunikat tylko o tym, co naprawde dano (wplywy ponizej 0.5 nie sa nadawane; dar 0 - bez wzmianki o zlocie)
                    string gTxt = g > 0 ? g + " denars from the treasury of " + Name(k) + " (paid at the next settlement, when there is gold in it)" : null;
                    string iTxt = applied > 0f ? applied.ToString("0.#", Inv) + " influence" : null;
                    if (gTxt != null || iTxt != null)
                        Log.Player("King's reward for the victory over " + foeLead + ": " + (gTxt != null && iTxt != null ? gTxt + " and " + iTxt : (gTxt ?? iTxt)) + ".");
                }
            }
            _dGiftQueued += queued; _dInfl += (long)Math.Round(gave); _dGiftClans += n;
            var st = Settings.Current;
            if (st != null && st.LogEnabled)
                Log.Info("Nagroda krola (314c): dzien " + day + " | " + Name(k) + " pokonal " + (foeLead ?? "-") + " | pobitych partii lordow " + lords + (army ? " (armia)" : "")
                         + ", ich ludzi na starcie " + men + " | udzial krolestwa w wkladzie " + (100 * share).ToString("0", Inv) + "%"
                         + " | dar " + (long)Math.Floor(gold) + " zl (w kolejce do skarbca " + queued + ", czesc krola i najemnikow " + kept + "), wplywy " + gave.ToString("0.#", Inv)
                         + " dla " + n + " rodow" + pl + "; partii juz nagrodzonych dzis " + dup + ".");
        }

        // ------------------------------------------------------------ zapis (SaveText, "arm_zold314")
        private static readonly char[] Bad = { '|', ';', ':', ',' };

        /// <summary>"v1|rod:rok:dni[:krolestwo];...|doba|rod:zl;...|rod:krolestwo:reszta:doba;...|rod:zl;..." (ostatnie pole od 431-p: czesc zoldu doby bedaca pozyczka
        /// korony, ktora nie tnie zwrotu; starsza DLL czyta tylko 5 pierwszych pol, brak pola = pusto)</summary>
        internal static string Export()
        {
            try
            {
                var sb = new StringBuilder(256);
                sb.Append("v1|");
                bool f = true;
                foreach (var kv in _svc) { if (kv.Key.IndexOfAny(Bad) >= 0) continue; if (!f) sb.Append(';'); f = false; sb.Append(kv.Key).Append(':').Append(kv.Value.Year.ToString(Inv)).Append(':').Append(kv.Value.Days.ToString(Inv)); if (kv.Value.Kingdom != null && kv.Value.Kingdom.IndexOfAny(Bad) < 0) sb.Append(':').Append(kv.Value.Kingdom); }
                sb.Append('|').Append(_paidDay.ToString(Inv)).Append('|'); f = true;
                foreach (var kv in _paid) { if (kv.Value <= 0 || kv.Key.IndexOfAny(Bad) >= 0) continue; if (!f) sb.Append(';'); f = false; sb.Append(kv.Key).Append(':').Append(kv.Value.ToString(Inv)); }
                sb.Append('|'); f = true;
                foreach (var g in _gifts) { if (g.Left <= 0 || g.Clan.IndexOfAny(Bad) >= 0 || g.Kingdom.IndexOfAny(Bad) >= 0) continue; if (!f) sb.Append(';'); f = false; sb.Append(g.Clan).Append(':').Append(g.Kingdom).Append(':').Append(g.Left.ToString(Inv)).Append(':').Append(g.Day.ToString(Inv)); }
                if (_lentKeep.Count > 0)
                {
                    sb.Append('|'); f = true;
                    foreach (var kv in _lentKeep) { if (kv.Value <= 0 || kv.Key.IndexOfAny(Bad) >= 0) continue; if (!f) sb.Append(';'); f = false; sb.Append(kv.Key).Append(':').Append(kv.Value.ToString(Inv)); }
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        internal static void Import(string data)
        {
            _svc.Clear(); _paid.Clear(); _lentKeep.Clear(); _paidDay = -1; _gifts.Clear(); _importN = 0; _importBad = 0;
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                var f = data.Split('|');
                if (f.Length < 5 || f[0] != "v1") { _importBad++; return; }
                if (f.Length >= 6) foreach (var p in f[5].Split(';')) { if (p.Length == 0) continue; var x = p.Split(':'); long v; if (x.Length == 2 && long.TryParse(x[1], NumberStyles.Integer, Inv, out v) && v > 0) _lentKeep[x[0]] = v; else _importBad++; }   // 431-p
                foreach (var p in f[1].Split(';')) { if (p.Length == 0) continue; var x = p.Split(':'); int y, d; if ((x.Length == 3 || x.Length == 4) && int.TryParse(x[1], NumberStyles.Integer, Inv, out y) && int.TryParse(x[2], NumberStyles.Integer, Inv, out d)) { _svc[x[0]] = new Svc { Year = y, Days = Math.Max(0, d), Kingdom = x.Length == 4 ? x[3] : null }; _importN++; } else _importBad++; }
                int pd; if (int.TryParse(f[2], NumberStyles.Integer, Inv, out pd)) _paidDay = pd; else _importBad++;
                foreach (var p in f[3].Split(';')) { if (p.Length == 0) continue; var x = p.Split(':'); long v; if (x.Length == 2 && long.TryParse(x[1], NumberStyles.Integer, Inv, out v) && v > 0) { _paid[x[0]] = v; _importN++; } else _importBad++; }
                foreach (var p in f[4].Split(';')) { if (p.Length == 0) continue; var x = p.Split(':'); long v; int d; if (x.Length == 4 && long.TryParse(x[2], NumberStyles.Integer, Inv, out v) && v > 0 && int.TryParse(x[3], NumberStyles.Integer, Inv, out d)) { _gifts.Add(new Gift { Clan = x[0], Kingdom = x[1], Left = v, Day = d }); _importN++; } else _importBad++; }
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
