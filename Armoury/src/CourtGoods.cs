using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// E3-P1 (noc-ekonomia3 16.10, czesc 2; klucze CourtBuysGoods, CrownCourtGoods, CrownCourtShare) - DWOR PANA I DWOR KROLA KUPUJA Z POLKI.
    /// Dowod [L] (test noc-ekonomia2 21-25-46 d109195): "Budzet rodow (166)" - dwor do kas siedzib ok. 244 tys./d, w tym sprzet przeterminowany 137-141 tys., wlasny dwor
    /// ok. 106 tys.; "Kiesa ludu" - dochod ludu miast z dworu 30 884 zl/d. Dwor placi kasie siedziby samym zlotem (ClanBudget.Court: glowa -> kasa siedziby, 20% do kiesy
    /// ludu) - z polki nie schodzi ani jedna sztuka, a pieniadz zostaje w kasach i kiesach ludu bez zakupu (kryterium 1: panowie maja co kupic).
    /// Regula (a), klucz CourtBuysGoods: wlasna czesc dworu rodu (bez przeterminowanego sprzetu E5/A2) w siedzibie-MIESCIE - do 60% kupuje towar z polki tej siedziby:
    /// jedzenie i napoje 50% (wino, piwo, miod pitny, ser, maslo, mieso, ryby, chleb, placek, miod, jablka, pomarancze, oliwa, jaja, daktyle), sukno 20% (plotno ponad
    /// zapas wojska MaterialOrders.ShelfReserve, aksamit, futra), zbytki 30% (klejnoty, srebro, korzenie, garnki); czego grupa nie wyda, przechodzi do nastepnej.
    /// Tylko kategorie, ktore w tym miescie NIE sa drogie (indeks <= 1.5 - uwaga recenzji: dwor nie zabiera ludowi rzadkiego jedzenia; rzadkie jedzenie zastepuje to,
    /// co lezy), tylko ponad 5 sztuk (ShelfKeep wsi), najwyzej 1/4 wiersza na dobe (wszystkie dwory tej siedziby razem), sztuka najwyzej 4 x wartosc (ShelfBuy.Take z cap,
    /// kazda sztuka po swojej cenie). Zamki bez zmian. Biegnie po konsumpcji ludu tej doby (TownFolk.Daily krok 54, ClanBudget 123).
    /// Regula (b), klucz CrownCourtGoods: korona po chlebie M3 i po G5, przed podzialem rent (CrownRents.Daily) - CrownCourtShare (5%) puli rent na te sama liste w stolicy
    /// (ClanBudget.Seat rodu panujacego, tylko miasto). Czego nie wyda, zostaje w puli rent. Zwrot zoldu, W5, M1, M3 i zapas - nietkniete (biegna wczesniej).
    /// Kto placi, kto dostaje: (a) glowa rodu -> kasa siedziby (ta sama kwota co dotad, czesc za towar); 20% dla ludu (CourtFolkShare) tylko od czesci "sluzba" - ClanBudget.Court.
    /// (b) skarbiec -> kasa stolicy z reszty wplywow dnia (CrownIncome.Spent). Towar schodzi z polki (ksiega towarow - ramka FCourt "dwory (E3-P1)", ujscie).
    /// Nic w zapisie (limit wiersza - pamiec doby). Wyjatek - potkniecie (licznik), zakup przerwany na sztukach juz zdjetych (oplacone).
    /// Regula (c), f6-zwrot (klucz LapsedGearBuysGoods; przeglad ekonomii 18.10, uwaga "przydzial sprzetu do kas siedzib bez towaru", razem ze zwrotem zoldu wedlug
    /// potrzeby - decyzja Jeffa 18.10): przeterminowany przydzial sprzetu (CourtFull, E5/A2) placi kasie siedziby-MIASTA tylko za towar NIEZYWNOSCIOWY z jej polki
    /// (grupy sukno 40% i zbytki 60% - te same towary co (a) bez jedzenia), tylko ponad zapas polki (MaterialOrders.ShelfReserve: prog karawan i zuzycie warsztatow / wojska,
    /// nie mniej niz 5 sztuk), te same bramki ceny (indeks <= 1.5, sztuka do 4 x wartosc) i ten sam limit 1/4 wiersza na dobe (wspolny z (a) dla wszystkich dworow siedziby -
    /// w ramach rodu (a) kupuje przed (c), dwor placony pierwszy; miedzy rodami - kolejnosc rodow w ClanBudget.Daily). Wolajacy (ClanBudget.Court) przelewa kasie tylko koszt zdjetego towaru, reszta zostaje w kiesie glowy; zamek - nic (bez polki). Ta sama ramka
    /// ksiegi towarow (dwory E3-P1), osobny licznik w linii doby.
    /// </summary>
    internal static class CourtGoods
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const float LordShare = 0.6f;
        private const int Keep = 5;
        private const float IndexMax = 1.5f, PriceCap = 4f, RowShare = 0.25f;
        private static readonly string[] GName = { "jedzenie i napoje", "sukno", "zbytki" };
        private static readonly float[] GShare = { 0.5f, 0.2f, 0.3f };
        private static readonly Dictionary<string, int> _group = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "wine", 0 }, { "beer", 0 }, { "mead", 0 }, { "cheese", 0 }, { "butter", 0 }, { "meat", 0 }, { "fish", 0 }, { "bread", 0 }, { "pie", 0 }, { "honey", 0 },
            { "apple", 0 }, { "orange", 0 }, { "oil", 0 }, { "Egg", 0 }, { "date_fruit", 0 },
            { "linen", 1 }, { "velvet", 1 }, { "fur", 1 },
            { "jewelry", 2 }, { "silver", 2 }, { "spice", 2 }, { "pottery", 2 },
        };

        // limit wiersza na dobe: osada -> (sztuka -> ile jeszcze wolno zdjac dzis)
        private static readonly Dictionary<Settlement, Dictionary<ItemObject, int>> _allow = new Dictionary<Settlement, Dictionary<ItemObject, int>>();
        private static int _allowDay = -1;
        private static readonly HashSet<string> _err = new HashSet<string>();

        private sealed class Side
        {
            public long Want, Gold; public int Pieces, Buyers, Seats; public long[] GWant = new long[3], GGold = new long[3]; public int[] GPcs = new int[3];
            public int Dear, AtKeep, RowCap; public readonly HashSet<Settlement> SeatSet = new HashSet<Settlement>();
        }
        private static readonly Side _lord = new Side(), _crown = new Side(), _gear = new Side();   // f6-zwrot: _gear - przeterminowany przydzial sprzetu (regula c)
        private static readonly SortedDictionary<string, long[]> _byItem = new SortedDictionary<string, long[]>(StringComparer.Ordinal);   // id -> [szt., zl]
        private static int _dStumbles, _sessStumbles;
        internal static long LastLord, LastCrown, LastGear;   // do linii "Budzet rodow (166)" i "Obieg" (kontrola ksiegi - poprzednia doba); f6: LastGear - regula (c)
        private static bool _startDone;

        internal static bool LordOn { get { var s = Settings.Current; return s != null && s.CourtBuysGoods; } }
        internal static bool CrownOn { get { var s = Settings.Current; return s != null && s.CrownCourtGoods && s.CrownCourtShare > 0f; } }

        internal static void Reset()
        {
            _allow.Clear(); _allowDay = -1; _err.Clear(); _sessStumbles = 0; LastLord = LastCrown = LastGear = 0; _startDone = false;
            ZeroDay();
        }

        private static void ZeroSide(Side x)
        {
            x.Want = x.Gold = 0; x.Pieces = x.Buyers = x.Seats = 0; x.Dear = x.AtKeep = x.RowCap = 0; x.SeatSet.Clear();
            for (int i = 0; i < 3; i++) { x.GWant[i] = 0; x.GGold[i] = 0; x.GPcs[i] = 0; }
        }

        private static void ZeroDay() { ZeroSide(_lord); ZeroSide(_crown); ZeroSide(_gear); _byItem.Clear(); _dStumbles = 0; }

        private static void Stumble(string where, Exception e)
        {
            _dStumbles++; _sessStumbles++;
            try { if (_err.Add(where)) Log.Error("CourtGoods." + where, e); } catch { }
        }

        private sealed class RowC { public EquipmentElement El; public int Have; public float Ix; }

        /// <summary>
        /// Zdejmuje z polki siedziby-miasta towar dworu za najwyzej `budget` (koszt wedlug cen polki, sztuka po sztuce) - zloto NIE przechodzi tu (wolajacy placi kasie
        /// cala kwote jak dotad). Zwraca koszt zdjetego towaru (0..budget). Nie rzuca.
        /// </summary>
        private static long Buy(Settlement seat, long budget, Side side, bool nonFood = false)   // f6-zwrot: nonFood - regula (c), bez grupy jedzenia, tylko ponad zapas polki
        {
            if (seat == null || !seat.IsTown || seat.Town == null || budget <= 0) return 0;
            var town = seat.Town;
            var roster = seat.ItemRoster;
            if (roster == null) return 0;
            int day = (int)CampaignTime.Now.ToDays;
            if (day != _allowDay) { _allow.Clear(); _allowDay = day; }
            Dictionary<ItemObject, int> allow;
            if (!_allow.TryGetValue(seat, out allow)) { allow = new Dictionary<ItemObject, int>(); _allow[seat] = allow; }
            side.Want += budget; side.Buyers++; if (side.SeatSet.Add(seat)) side.Seats++;
            long spent = 0, carry = 0;
            var gf = GoodsLedger.Begin(GoodsLedger.FCourt, seat);
            try
            {
                for (int g = nonFood ? 1 : 0; g < 3; g++)
                {
                    // f6 (c): sukno i zbytki w proporcji 0.2 : 0.3 (40% / 60%); (a) i (b) - udzialy jak dotad (ten sam rachunek co do zlotowki)
                    double gs = nonFood ? GShare[g] / (1.0 - GShare[0]) : (double)GShare[g];
                    long gb = (long)(budget * gs) + carry; carry = 0;
                    if (g == 2) gb = Math.Max(0, budget - spent);          // ostatnia grupa - cala reszta (bez zgubionych zaokraglen)
                    side.GWant[g] += gb;
                    var rows = new List<RowC>();
                    for (int i = 0; i < roster.Count; i++)
                    {
                        var el = roster.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        int gi;
                        if (it == null || !it.IsTradeGood || el.Amount <= 0 || it.StringId == null || !_group.TryGetValue(it.StringId, out gi) || gi != g) continue;
                        float ix = 1f;
                        try { ix = town.GetItemCategoryPriceIndex(it.ItemCategory); } catch { ix = 1f; }
                        if (!(ix > 0f) || float.IsInfinity(ix)) ix = 1f;
                        if (ix > IndexMax) { side.Dear++; continue; }   // drogi tu - lud ma brak (uwaga recenzji): dwor nie bierze
                        int keep = Keep;
                        if (nonFood) keep = Math.Max(Keep, MaterialOrders.ShelfReserve(town, it));                  // f6 (c): kazdy towar tylko ponad zapas polki (prog karawan, zuzycie)
                        else if (it.StringId == "linen") keep = Math.Max(Keep, MaterialOrders.ShelfReserve(town, it));   // plotno tylko ponad zapas wojska
                        int have = el.Amount - keep;
                        if (have <= 0) { side.AtKeep++; continue; }
                        rows.Add(new RowC { El = el.EquipmentElement, Have = have, Ix = ix });
                    }
                    rows.Sort((a, b) => a.Ix.CompareTo(b.Ix));             // najpierw to, co najtansze wobec zwyklej ceny - tego najwiecej lezy
                    for (int r = 0; r < rows.Count && gb > 0; r++)
                    {
                        var row = rows[r];
                        int left;
                        if (!allow.TryGetValue(row.El.Item, out left))
                        {
                            int at = roster.FindIndexOfElement(row.El);
                            left = at >= 0 ? (int)Math.Floor(roster.GetElementNumber(at) * RowShare) : 0;
                        }
                        int n = Math.Min(row.Have, left);
                        if (n <= 0) { side.RowCap++; allow[row.El.Item] = 0; continue; }
                        long rb = gb / (rows.Count - r);
                        var el = row.El;
                        int p0 = 0;
                        try { p0 = town.MarketData.GetPrice(el, null, false, null); } catch { p0 = 0; }
                        if (p0 <= 0) continue;
                        if (rb < p0 && gb >= p0) rb = p0;                      // drogi wiersz (aksamit, klejnoty) - co najmniej jedna sztuka, jesli grupa ja udzwignie
                        int cap = (int)Math.Min(int.MaxValue, Math.Max(1.0, PriceCap * (double)Math.Max(1, el.Item.Value)));
                        int cost, first, last;
                        int got = ShelfBuy.Take(roster, el, n, (int)Math.Min(int.MaxValue, rb), () => town.MarketData.GetPrice(el, null, false, null), out cost, out first, out last, p0, false, cap);
                        allow[row.El.Item] = left - got;
                        if (got <= 0) continue;
                        spent += cost; gb -= cost;
                        side.Gold += cost; side.Pieces += got; side.GGold[g] += cost; side.GPcs[g] += got;
                        long[] bi; string id = el.Item.StringId ?? "?";
                        if (!_byItem.TryGetValue(id, out bi)) { bi = new long[2]; _byItem[id] = bi; }
                        bi[0] += got; bi[1] += cost;
                    }
                    carry = Math.Max(0, gb);
                }
            }
            catch (Exception e) { Stumble("Buy", e); }
            finally { GoodsLedger.End(gf); }
            return Math.Max(0, Math.Min(budget, spent));
        }

        /// <summary>(a) ClanBudget.Court, przed przelewem glowa -> kasa siedziby: towar za najwyzej 60% wlasnej czesci dworu. Zwraca koszt towaru (czesc wplaty, nie dodatek).</summary>
        internal static long Lord(Settlement seat, long ownCourtPaid)
        {
            if (!LordOn || ownCourtPaid <= 0 || seat == null || !seat.IsTown) return 0;
            try { return Buy(seat, (long)(LordShare * ownCourtPaid), _lord); }
            catch (Exception e) { Stumble("Lord", e); return 0; }
        }

        /// <summary>(c) f6-zwrot - CourtFull.Goods (z ClanBudget.Court, po (a), przed przelewem glowa -> kasa siedziby): towar niezywnosciowy ponad zapas polki za najwyzej
        /// `lapsedPaid` (czesc wplaty dworu z przeterminowanego sprzetu). Zwraca koszt zdjetego towaru (0..lapsedPaid) - tylko tyle wolajacy przelewa kasie. Zamek - 0.
        /// Klucz sprawdza wolajacy (CourtFull.GoodsOnly). Nie rzuca.</summary>
        internal static long Gear(Settlement seat, long lapsedPaid)
        {
            if (lapsedPaid <= 0 || seat == null || !seat.IsTown) return 0;
            try { return Buy(seat, lapsedPaid, _gear, true); }
            catch (Exception e) { Stumble("Gear", e); return 0; }
        }

        /// <summary>
        /// (b) CrownRents.Daily, po M3 i G5, przed podzialem rent: dwor krolewski w stolicy (siedziba rodu panujacego, tylko miasto) za CrownCourtShare puli rent.
        /// Skarbiec -> kasa stolicy (najpierw skarbiec, potem kasa - arytmetyka), CrownIncome.Spent. Zwraca zaplacone.
        /// </summary>
        internal static long Crown(Kingdom k, long pool)
        {
            if (!CrownOn || k == null || pool <= 0) return 0;
            try
            {
                var rc = k.RulingClan;
                if (rc == null) return 0;
                var seat = ClanBudget.Seat(rc);
                if (seat == null || !seat.IsTown || seat.Town == null) return 0;
                var s = Settings.Current;
                long budget = (long)(pool * (double)Math.Max(0f, Math.Min(0.5f, s.CrownCourtShare)));
                budget = Math.Min(budget, Math.Max(0, CrownIncome.LeftFor(k)));
                if (budget <= 0) return 0;
                long cost = Buy(seat, budget, _crown);
                if (cost <= 0) return 0;
                int ix = (int)Math.Min(int.MaxValue, cost);
                k.KingdomBudgetWallet -= ix;                               // najpierw skarbiec, potem kasa stolicy (arytmetyka - nie rzuca)
                seat.Town.ChangeGold(ix);
                CrownIncome.Spent(k, ix);
                CrownIncome.NoteCourt(k, ix);
                try { TownFolk.Sale(seat, ix, TownFolk.MixGoods); } catch { }   // sprzedaz kupcow z polki (tylko licznik - robocizna handlu, jak kazda sprzedaz z polki)
                return ix;
            }
            catch (Exception e) { Stumble("Crown", e); return 0; }
        }

        private static string SideText(string name, Side x)
        {
            var sb = new StringBuilder(260);
            sb.Append(name).Append(": kupujacych ").Append(x.Buyers).Append(" w ").Append(x.Seats).Append(" siedzibach, chciane ").Append(x.Want).Append(" zl, kupione ").Append(x.Gold).Append(" zl (")
              .Append(x.Want > 0 ? (100.0 * x.Gold / x.Want).ToString("0.0", Inv) : "0.0").Append("%), sztuk ").Append(x.Pieces).Append(" [");
            for (int g = 0; g < 3; g++) sb.Append(g > 0 ? "; " : "").Append(GName[g]).Append(" ").Append(x.GWant[g]).Append(" / ").Append(x.GGold[g]).Append(" zl / ").Append(x.GPcs[g]).Append(" szt.");
            sb.Append("]; pominiete wiersze: drogie tu (indeks > 1.5 - lud ma brak) ").Append(x.Dear).Append(", przy zapasie polki ").Append(x.AtKeep).Append(", limit 1/4 wiersza ").Append(x.RowCap);
            return sb.ToString();
        }

        /// <summary>Linia startu (z Daily przy pierwszej dobie sesji).</summary>
        private static void SessionStart()
        {
            _startDone = true;
            var s = Settings.Current;
            Log.Info("Dwor - towar z polki (E3-P1): start sesji | klucz Court Buys Goods " + (s != null && s.CourtBuysGoods ? "tak" : "nie") + ", Crown Court Goods " + (s != null && s.CrownCourtGoods ? "tak" : "nie")
                     + ", Lapsed Gear Buys Goods (f6 - sprzet niewydany tylko za towar niezywnosciowy ponad zapas polki) " + (CourtFull.GoodsOnly ? "tak" : "nie")
                     + ", Crown Court Share " + (s != null ? s.CrownCourtShare.ToString("0.00", Inv) : "-")
                     + " | pan: do " + (LordShare * 100).ToString("0", Inv) + "% wlasnego dworu w siedzibie-miescie; krol: czesc puli rent w stolicy | grupy jedzenie 50% / sukno 20% / zbytki 30%, tylko indeks <= 1.5, ponad 5 szt. (plotno ponad zapas wojska), 1/4 wiersza na dobe, sztuka do 4 x wartosc"
                     + " | ramka ksiegi towarow 'dwory (E3-P1)' | potkniecia 0.");
        }

        /// <summary>Raz na dobe (ArmouryBehavior, po ClanBudget.Daily): linia "Dwor - towar z polki (E3-P1): dzien N" (zeruje liczniki).</summary>
        internal static void Daily()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || Campaign.Current == null) return;
                if (!_startDone) SessionStart();
                LastLord = _lord.Gold; LastCrown = _crown.Gold; LastGear = _gear.Gold;
                var items = new List<KeyValuePair<long, string>>();
                foreach (var kv in _byItem) items.Add(new KeyValuePair<long, string>(kv.Value[1], kv.Key + " " + kv.Value[0] + " szt. / " + kv.Value[1] + " zl"));
                items.Sort((a, b) => b.Key.CompareTo(a.Key));
                var top = new List<string>();
                for (int i = 0; i < items.Count && i < 12; i++) top.Add(items[i].Value);
                Log.Info("Dwor - towar z polki (E3-P1): dzien " + ((int)CampaignTime.Now.ToDays - 1) + " | " + (s.CourtBuysGoods || s.CrownCourtGoods || CourtFull.GoodsOnly ? "CZYNNE" : "WYLACZONE (MCM)")
                         + " | " + SideText("pan (Court Buys Goods " + (s.CourtBuysGoods ? "tak" : "nie") + ")", _lord)
                         + " | " + SideText("korona (Crown Court Goods " + (s.CrownCourtGoods ? "tak" : "nie") + ")", _crown)
                         + " | " + SideText("sprzet niewydany -> dwor tylko za towar niezywnosciowy ponad zapas polki (f6, Lapsed Gear Buys Goods " + (CourtFull.GoodsOnly ? "tak" : "nie") + ")", _gear)   // f6-zwrot (c)
                         + " | wedlug towaru: " + (top.Count > 0 ? string.Join(", ", top.ToArray()) : "-")
                         + " | zloto: pan -> kasy siedzib (czesc dworu za towar - ta sama kwota co dotad, lud bierze 20% tylko od sluzby) " + _lord.Gold + ", skarbce -> kasy stolic " + _crown.Gold
                         + ", sprzet niewydany -> kasy siedzib tylko za towar (f6; lud bez udzialu, reszta w kiesach glow) " + _gear.Gold
                         + " | wplyw na skrzynie: lud siedzib mniej o 20% x towar pana (" + (long)(_lord.Gold * (double)CourtFull.FolkShare) + "), kasy siedzib wiecej o to samo; korony -" + _crown.Gold + ", kasy stolic +" + _crown.Gold
                         + " | potkniecia " + _dStumbles + " (sesja " + _sessStumbles + ").");
            }
            catch (Exception e) { Log.Error("CourtGoods.Daily", e); }
            finally { ZeroDay(); }
        }
    }
}
