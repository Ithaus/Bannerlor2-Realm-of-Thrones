using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    internal static partial class StartCalibration
    {
        /// <summary>322c: kcal jednej racji spichlerza wsi (pelna potrzeba kcal ludzi w domu / ludzie w domu) z ostatniego rachunku okregu KS - ta sama miara,
        /// ktora KS zamienia kcal czesci domowej na racje (ld.Rations). NaN - stan nie zastosowany, wies poza KS albo brak rachunku. Tylko watek glowny. Nie rzuca.</summary>
        internal static double KcalPerRation322(Village v)
        {
            try
            {
                if (v == null || v.Settlement == null || v.Settlement.StringId == null || !Applied) return double.NaN;
                LiveDist ld;
                if (!_live.TryGetValue(v.Settlement.StringId, out ld) || ld == null || ld.TF || ld.Res == null) return double.NaN;
                double k = ld.PeopleHome > 0 ? ld.Res.KcalNeed / ld.PeopleHome : double.NaN;
                return k >= 800.0 && k <= 6000.0 ? k : double.NaN;
            }
            catch { return double.NaN; }
        }

        /// <summary>322c: popyt zaplacony miasta na zboze (szt./d przy lambda 1 - ZDp) i dni polki celu T* jego rynku (ZTstar). false - stan nie zastosowany
        /// albo miasto poza ZC4. Tylko watek glowny. Wyjatek leci do wolajacego.</summary>
        internal static bool Grain322(Town t, out double dp, out double tstar)
        {
            dp = 0.0; tstar = 0.0;
            if (t == null || !Applied) return false;
            dp = ZDp(t, ZGrain);
            if (!(dp > 0.0)) return false;
            tstar = ZTstar(TownMarket(t), ZGrain);
            return tstar > 0.0 && !double.IsNaN(tstar) && !double.IsInfinity(tstar);
        }
    }

    /// <summary>
    /// 322c ZBOZE ZIMA (Jeff 03.11 "3 - ok", audyt ekonomii pkt 2): w zimie polki zboza w 97% miast Westeros sa puste przy cenie na suficie x10, a spichlerze wsi
    /// (Famine, E4/6) maja zapas na ok. 630 dni - p6 sprzedaje go tylko do zapasu jedzenia miasta (Town.FoodStocks) i tylko przy niedoborze. Teraz (klucz
    /// WinterGrainToMarket, razem z TownGranaryFromVillages):
    ///  1. SYGNAL MIASTA (jedna regula dla wszystkich miast): polka zboza (z tym, co wiezie KS-H) ponizej pol doby popytu zaplaconego KS (ZDp) albo indeks ceny
    ///     zboza miasta >= WinterGrainTriggerIndex (sufit 10). Miasto chce najwyzej do celu polki KS (T* dni popytu) i najwyzej 2 doby popytu na dobe (bez zalania).
    ///     Miasto oblezone, Innych albo poza ZC4 (KS nie zastosowany) - nic.
    ///  2. WIES (te same warunki co p6: stan Normal, bez najazdu, migawka Famine tej doby, gospodarze bez glodu): sprzedaje tylko zapas ponad rezerwe -
    ///     max(WinterGrainKeepDays, prog bramki krainy KS z budzetem straty: 364 + 91 = 455) dni wlasnej potrzeby - i rozklada go na reszte zimy (najmniej 91 dni):
    ///     na dobe najwyzej (zapas - rezerwa) / max(91, dni do konca zimy). Spichlerz nie schodzi przez to ponizej rezerwy; zima i tak zjada swoje.
    ///     Kilka wsi jednego miasta - podzial wedlug tego, ile kazda moze dac (reszta po kolei).
    ///  3. ZAMIANA (miara KS): sztuka zboza na targu = sigma x kg sztuki zboza (KS: szt. gry = kg / (sigma x kg sztuki)), 3300 kcal/kg; racja = kcal potrzeby
    ///     czlowieka w domu z rachunku okregu KS (StartCalibration.KcalPerRation322). Racji na sztuke = 3300 x sigma x kg / kcal racji. Wies bez rachunku KS - nic.
    ///  4. ZAMKNIETA EKONOMIA: racje schodza ze spichlerza wsi (Famine.TributeTake; ulamek ponad cale sztuki wraca TributeGive), cale sztuki zboza wchodza na polke
    ///     miasta (ItemRoster, ramka ksiegi towarow ZC1 "zboze ze spichlerza wsi (322c)"); kasa miasta placi kiesie wsi cene polki BEZ PULAPU (recenzja 322c:
    ///     pulap 3x wartosci dawal kasie miasta marze do x10 - wbrew "cena od polki"), sztuka po sztuce w dol krzywej polki jak tabory wsi (MarketCarts
    ///     SellPostfixCore): sztuka k = TradeItemPriceFactorModel.GetPrice(strona sprzedajacego, polka z chwili przed dostawa + k x ShelfWorth), tylko z nadwyzki kasy ponad zapas
    ///     kupcow (TownPurse.SpendableTown - jak p6 i inne nasze zakupy; z calej kasy miasto wpadaloby w zaleglosci firmy DS i pozyczki rady w Banku - TownDebt). Zloto: ChangeGold para w pozycji MFolk ksiegi pieniadza (jak p6). Racje w ksiedze dnia p6 (soldV) - bramka krainy KS liczy je jako targ.
    ///  5. TYLKO ZIMA (recenzja 322c): poza zima (albo bez dni do konca zimy) nic - linia "POZA ZIMA - nic"; poza zima reszta 1/91 dziennie
    ///     wysuszylaby spichlerze przed zima.
    /// Klucz wylaczony - nic (p6 bez zmian). Stan bez zapisu. Potkniecia liczone per miejsce (bez globalnych wylacznikow, CLAUDE.md 7).
    /// </summary>
    internal static partial class TownGranary
    {
        private const double W322DayCapDays = 2.0;        // miasto: najwyzej tyle dob popytu zaplaconego na dobe
        private const double W322ShelfLowDays = 0.5;      // sygnal: polka (z KS-H w drodze) ponizej tylu dob popytu zaplaconego
        private const double W322SpreadMin = 91.0;        // wies rozklada zapas ponad rezerwe na reszte zimy, najmniej na tyle dni (jedna pora)
        private const double W322KcalGrain = 3300.0;      // kcal na kg zboza (KsDeliver.ItemKcal, KsTab.FoodKcal)
        private static double _w322SessPieces, _w322SessRations; private static long _w322SessGold;
        private static int _w322Stum, _w322StumAll;
        private static bool _w322OffLogged;
        // 324: dowoz ostatniej doby do linii "Glod zimy (324)" (StartCalibration.Line324): sztuki na polki, w tym do miast poza ZC4 i ich liczba
        private static int _w324Day = int.MinValue, _w324Pieces, _w324PiecesOut, _w324TownsOut;
        private static int _w324PiecesLand, _w324PiecesCastle; private static long _w324Cred;   // 324 runda 2: z krainy, z wsi zamkow, na rachunek wsi (zl)

        /// <summary>324: dowoz 322c doby `today` (0 - inna doba albo nic). Tylko watek glowny. Nie rzuca.</summary>
        internal static void Last324(int today, out int pieces, out int piecesOut, out int townsOut)
        {
            bool ok = _w324Day == today;
            pieces = ok ? _w324Pieces : 0; piecesOut = ok ? _w324PiecesOut : 0; townsOut = ok ? _w324TownsOut : 0;
        }

        /// <summary>324 runda 2: z dowozu doby `today` - sztuki z krainy (drugi poziom), z wsi zamkow i zloto zapisane na rachunki wsi. Tylko watek glowny. Nie rzuca.</summary>
        internal static void Last324b(int today, out int landPieces, out int castlePieces, out long credit)
        {
            bool ok = _w324Day == today;
            landPieces = ok ? _w324PiecesLand : 0; castlePieces = ok ? _w324PiecesCastle : 0; credit = ok ? _w324Cred : 0;
        }

        private static void Reset322()
        {
            _w322SessPieces = _w322SessRations = 0.0; _w322SessGold = 0;
            _w322Stum = _w322StumAll = 0; _w322OffLogged = false;
            _w324Day = int.MinValue; _w324Pieces = _w324PiecesOut = _w324TownsOut = 0;
            _w324PiecesLand = _w324PiecesCastle = 0; _w324Cred = 0;
            _w324ShelfAfter.Clear(); _w324KomDay = int.MinValue;
        }

        private static void Stumble322(string where, Exception e)
        {
            _w322Stum++; _w322StumAll++;
            Stumble(where, e);
        }

        /// <summary>324 runda 2: wies - zrodlo zboza 322c tej doby (cale sztuki do oddania dzis, racji na sztuke, region miasta).</summary>
        private sealed class W324Src { internal Village V; internal Town Home; internal int Avail, Rate, Given; internal double Rpp; internal bool Castle; }   // 325: Rate - dzisiejsza rata wsi (szt.), Given - oddane dzis

        /// <summary>324 runda 2: miasto z sygnalem tej doby - popyt (cennik sztuka po sztuce), kasa i kredyt, przyjete sztuki.</summary>
        private sealed class W324Req
        {
            internal Town T; internal Settlement St; internal double Dp, Hunger; internal int Inc, Sh0, Tg0, KAt, Ord, Cap2; internal bool OutZ, Any;   // 325: Cap2 - dawny limit dobowy (2 doby popytu, szt.)
            internal readonly List<int> Pp = new List<int>(); internal long Spend, CashLeft, CredLeft, Cash, Cred, Kom; internal string Land;   // runda 3: Kom - wartosc oddana w komis
        }

        /// <summary>324 runda 2: wiersz wsi - wolne racji na dobe (`x` - bilans zimy z psuciem, moze byc ujemny; bez bilansu - zapas ponad stala rezerwe / spread),
        /// racji na sztuke i czy moze dzis sprzedawac. false - wies bez wiersza (licznik przyczyny). Wyjatek leci do wolajacego.</summary>
        private static bool W324Row(Village v, int today, double keepDays, double spread, double kgSigma, bool bal, int left, double e324, double k324, double kprWorld,
                                    out double x, out double rpp, out bool sells, ref int notToday, ref int noKcal, ref int kw, ref int noBal)
        {
            x = 0.0; rpp = 0.0; sells = false;
            if (v == null || v.Settlement == null) return false;
            double home, harv, food0, vcap; float u; int d;
            if (!Famine.DayOf(v, out home, out harv, out food0, out vcap, out u, out d) || d != today || !(home > 0.0)) { notToday++; return false; }
            double food;
            if (bal && left > 0)
            {
                // 324: bilans zimy z psuciem - zapas ponad prog racjonowania (x e^{rho L}) i ponad brak wlasnego stolu do wiosny (def x (e^{rho L} - 1) / rho)
                double h4, def, floor; bool nb;
                if (!StartCalibration.WinterBalance324(v, today, out food, out h4, out def, out floor, out nb)) { if (nb) noBal++; else notToday++; return false; }
                x = StartCalibration.WinterFree324(food, floor, def, e324, k324);
            }
            else
            {
                if (!Famine.FoodOf(v, out food)) { notToday++; return false; }   // zapas po p6 tej doby
                x = (food - keepDays * home) / spread;                           // jak dotad: zapas ponad stala rezerwe rozlozony na reszte zimy (racji na dobe)
            }
            sells = v.VillageState == Village.VillageStates.Normal && !v.Settlement.IsUnderRaid && !(u > 0f);
            if (!sells) return true;
            double k = StartCalibration.KcalPerRation322(v);
            if (double.IsNaN(k) && !double.IsNaN(kprWorld)) { k = kprWorld; kw++; }   // 324: wies bez rachunku KS - kcal racji sredni swiata
            if (double.IsNaN(k)) { noKcal++; sells = false; return true; }
            rpp = W322KcalGrain * kgSigma / k;
            if (!(rpp > 0.0) || double.IsInfinity(rpp)) { noKcal++; sells = false; }
            return true;
        }

        /// <summary>Podzial `need` sztuk miedzy zrodla wedlug tego, ile kazde moze dac (reszta po kolei). Zwraca sztuki na zrodlo.</summary>
        private static int[] W324Alloc(List<W324Src> src, long need)
        {
            var g = new int[src.Count];
            long avail = 0; foreach (var c in src) avail += Math.Max(0, c.Avail);
            if (avail < 1 || need < 1) return g;
            need = Math.Min(need, avail);
            long given = 0;
            for (int i = 0; i < src.Count; i++) { int a = Math.Max(0, src[i].Avail); int n = (int)Math.Min(a, Math.Floor(need * (double)a / avail)); g[i] = n; given += n; }
            for (int i = 0; i < src.Count && given < need; i++) { int n = (int)Math.Min(Math.Max(0, src[i].Avail) - g[i], need - given); if (n > 0) { g[i] += n; given += n; } }
            return g;
        }

        private static bool AtWar324(Settlement a, Settlement b)
        {
            try { return a != null && b != null && a.MapFaction != null && b.MapFaction != null && FactionManager.IsAtWarAgainstFaction(a.MapFaction, b.MapFaction); } catch { return false; }
        }

        /// <summary>322c: doba (TownGranary.Daily, po p6, przed zapisem ksiegi dnia). Linia "Zboze zima (322c): ...". Wyjatek per miasto liczony.
        /// 324 runda 2: zrodla = region miasta targowego (klucz Winter Grain Trade Region: wsie z Village.TradeBound = miasto - jego i zamkow; runda 3: region tylko wybiera zrodla, kazda wies sprzedaje swoja nadwyzke),
        /// drugi poziom - kraina (Winter Grain Land Tier), sygnal ponizej celu T* (Winter Grain Fill To Target), kredyt wsi (Winter Grain Village Credit).</summary>
        private static void Winter322(Settings s, int today, bool live, Dictionary<Village, double> soldV)
        {
            if (s == null) return;
            // 324 runda 3: komis rozliczany co dobe (sprzedane z utargu, termin - zwrot albo rachunek wsi), takze poza zima i przy wylaczonych kluczach
            try { W324Consign(s, today); } catch (Exception e) { Stumble322("Winter322(komis)", e); }
            if (!s.WinterGrainToMarket)
            {
                if (!_w322OffLogged && s.LogEnabled) { _w322OffLogged = true; Log.Info("Zboze zima (322c): WYLACZONE (Winter Grain To Market w MCM) - spichlerze wsi tylko do zapasu jedzenia miasta (p6), jak dotad."); }
                W324ShelfMark(today);
                return;
            }
            _w322OffLogged = false;
            long t0 = Stopwatch.GetTimestamp();
            var grain = DefaultItems.Grain;
            bool applied = StartCalibration.Applied;
            int sigma = Math.Max(1, DailyLife.KsSigma);
            double kg = grain != null && grain.Weight > 0.01f ? grain.Weight : 10.0;   // jak StartCalibration.EnsureItems
            double kgSigma = kg * sigma;
            int keepKey = Math.Max(0, Math.Min(728, s.WinterGrainKeepDays));
            double keepDays = Math.Max(keepKey, StartCalibration.LandGateFloorDays);
            float trigIx = Math.Max(1f, Math.Min(10f, s.WinterGrainTriggerIndex));
            bool winter = WinterBite.WinterNow();
            int left = 0;
            { int season, sday, len; if (winter && WesterosClimate.SeasonInfo(out season, out sday, out len) && season == (int)CampaignTime.Seasons.Winter && len > 0) left = Math.Max(0, len - sday + 1); }
            double spread = Math.Max(W322SpreadMin, left);
            bool inWinter = winter && left > 0;   // recenzja 322c: tylko zima (poza zima nic - bez rozkladu 1/91 przez caly rok)
            bool bal324 = s.WinterGrainFromWinterBalance, all324 = s.WinterGrainAllTowns;   // 324: wolne z bilansu zimy; miasta poza ZC4 i wsie bez rachunku KS
            // 324 runda 2: region miasta targowego (tylko z bilansem zimy), kraina, sygnal ponizej celu, kredyt wsi (runda 3: komis)
            bool reg324 = s.WinterGrainTradeRegion && bal324, land324 = s.WinterGrainLandTier, fill324 = s.WinterGrainFillToTarget, cred324 = s.WinterGrainVillageCredit;
            // runda 3: reszta ponad gotowke - 2 komis (Winter Grain Consign), 1 rachunek wsi w pulapie (Winter Grain Village Credit), 0 tylko gotowka
            bool kom324 = s.WinterGrainConsign; int payMode = kom324 ? 2 : cred324 ? 1 : 0;
            // 325 (Winter Grain Fill Now): polka ponizej celu T* - wsie zrodla moga dowiezc cala luke do celu z CALEGO wolnego zapasu (x_v), nie z dziennej raty x_v x k;
            // ilosc dalej najwyzej cel polki T* x dp - polka (limit dobowy 2 x dp zdjety). Bilans liczy sie co dobe od nowa z biezacego zapasu - wczesniejsze napelnienie
            // samo zmniejsza pozniejsze raty; zapas wsi po oddaniu calego x_v = prog racjonowania x E + def / k (WinterNeed324) - plan bramki krainy.
            bool fillNow = s.WinterGrainFillNow;
            long fillPcs = 0, fillOverRate = 0, fillOver2 = 0;
            double rho324 = bal324 ? StartCalibration.Rho324() : 0.0, e324 = 1.0;
            double k324 = bal324 && left > 0 ? StartCalibration.WinterK324(left, rho324, out e324) : 0.0;
            double kprW = all324 && applied ? StartCalibration.KcalPerRationWorld324(today) : double.NaN;
            int outTowns = 0, outFed = 0, kwVills = 0, noBal = 0; long outPieces = 0;
            var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.TradeItemPriceFactorModel : null;
            var gcat = grain != null ? grain.ItemCategory : null;
            int wv = grain != null ? Math.Max(1, HistoricalPrices.ShelfWorth(grain)) : 1;   // paczka 121: jednostka polki = waga sztuki

            int towns = 0, besieged = 0, walkers = 0, noKs = 0, sig = 0, sigShelf = 0, sigIx = 0, full = 0, noGold = 0, fedTowns = 0, fedVills = 0, noPrice = 0, noVill = 0;
            int atFloor = 0, noKcal = 0, notToday = 0, tiny = 0;
            long pieces = 0, gold = 0, townOut = 0, vilIn = 0, shelfIn = 0; double rations = 0.0, barnDrop = 0.0, firstSum = 0.0, lastSum = 0.0, rppSum = 0.0, wantSum = 0.0; int rppN = 0;
            // 324 runda 2: liczniki regionu, krainy i kredytu
            int regions = 0, regVills = 0, regNeg = 0, landFedTowns = 0, credTowns = 0, credFail = 0, komTowns = 0; long pcsTown = 0, pcsCastle = 0, pcsLand = 0, credGold = 0, credCapSum = 0, komGold = 0, komPcs = 0;
            double regFreeR = 0.0, regNetR = 0.0;
            var after = new List<double>();
            var srcOfAll = new Dictionary<Town, List<W324Src>>();   // 325: zrodla doby (do pomiaru 'z zapasu ponad rate')
            if (live && applied && grain != null && inWinter)
            {
                // 1. miasta z sygnalem (popyt, cennik sztuka po sztuce, kasa i kredyt)
                var reqs = new List<W324Req>();
                foreach (var town in Town.AllTowns)
                {
                    try
                    {
                        var st = town != null ? town.Settlement : null;
                        if (st == null || !st.IsTown || st.ItemRoster == null) continue;
                        towns++;
                        if (OthersRise.IsWhiteWalkerClan(st.MapFaction as Clan)) { walkers++; continue; }
                        if (st.IsUnderSiege) { besieged++; continue; }
                        double dp, tstar;
                        bool outZ = false;
                        if (!StartCalibration.Grain322(town, out dp, out tstar))
                        {
                            // 324 (Winter Grain All Towns): miasto poza ZC4 - popyt dobowy z modelu jedzenia miasta w grze (spozycie Food jak p6), cel polki 2 doby
                            if (!all324) { noKs++; continue; }
                            dp = Cons(town); tstar = W322DayCapDays; outZ = true;
                        }
                        if (dp < 0.5) { noKs++; continue; }
                        if (outZ) outTowns++;
                        int shelf = st.ItemRoster.GetItemNumber(grain);
                        int inc = KsFoodCarts.Incoming(st, StartCalibration.ZGrain);
                        float ix = 1f;
                        try { if (grain.ItemCategory != null) ix = town.GetItemCategoryPriceIndex(grain.ItemCategory); } catch { ix = 1f; }
                        // 324 runda 2 (Winter Grain Fill To Target): sygnal = polka ponizej celu T* (bez hustawki wokol pol doby); NIE - ponizej pol doby popytu
                        bool lowShelf = (shelf + inc) < (fill324 ? tstar : W322ShelfLowDays) * dp, hiIx = ix >= trigIx;
                        if (!lowShelf && !hiIx) continue;
                        sig++; if (lowShelf) sigShelf++; if (hiIx) sigIx++;
                        double gap = tstar * dp - shelf - inc;
                        double want = fillNow ? gap : Math.Min(gap, W322DayCapDays * dp);   // 325: pelna luka do celu (dawny limit dobowy 2 doby popytu tylko przy NIE)
                        if (want < 1.0) { full++; continue; }
                        if (model == null || gcat == null || town.MarketData == null) { noPrice++; continue; }
                        long spend = Math.Max(0, TownPurse.SpendableTown(town));   // nadwyzka ponad zapas kupcow (jak p6) - bez zaleglosci firmy DS i pozyczek rady
                        // R3-2: gotowka tylko z nadwyzki ponad zapas kupcow (zapas kupcow placi zaleglosci DS i raty TownDebt) - reszta komis albo rachunek wsi
                        long cash = Math.Min(spend, (long)Math.Max(0, town.Gold)), cap = 0;
                        if (payMode == 2) cap = long.MaxValue / 4;   // R3-3: komis - bez pulapu kwoty (stary dlug za jedzenie go nie blokuje); ilosc: cel polki T* x popyt i 2 doby popytu
                        else if (payMode == 1)
                        {
                            // rachunek wsi (komis wylaczony): dlug miasta za jedzenie najwyzej rata 322e x Ks Consign Rate Release Days; bez pomiaru zarobku - bez rachunku (R3-3)
                            long rate;
                            if (KsFoodCredit.RateOf(st, out rate)) cap = Math.Max(0L, rate * (long)KsFoodCredit.RateReleaseDays - KsFoodCredit.FoodDebtOf(st));
                            credCapSum += cap;
                        }
                        // cena od polki bez pulapu: sztuka k po cenie sprzedajacego przy polce powiekszonej o k sztuk (jak tabory wsi - MarketCarts); polka z chwili przed dostawa
                        var md = town.MarketData.GetCategoryData(gcat);
                        var el = new EquipmentElement(grain);
                        var rq = new W324Req { T = town, St = st, Dp = dp, Inc = inc, OutZ = outZ, Ord = reqs.Count, Spend = spend, CashLeft = cash, CredLeft = cap, Cap2 = (int)Math.Min(int.MaxValue, Math.Floor(W322DayCapDays * dp)) };
                        long cum = 0, budget = cash + cap; int maxN = (int)Math.Min(100000.0, Math.Floor(want));
                        for (int k = 0; k < maxN; k++)
                        {
                            int p = Math.Max(1, model.GetPrice(el, null, null, true, md.InStoreValue + (float)k * wv, md.Supply, md.Demand));
                            if (cum + p > budget) break;
                            cum += p; rq.Pp.Add(p);
                        }
                        if (rq.Pp.Count < 1) { noGold++; continue; }
                        wantSum += rq.Pp.Count;
                        rq.Hunger = 1.0 - (shelf + inc) / Math.Max(1e-9, tstar * dp);
                        try { rq.Land = StartCalibration.TownLandOf(st); } catch { rq.Land = null; }
                        if (rq.Land == "?") rq.Land = null;   // R3-5: kraina nieznana - bez drugiego poziomu
                        rq.Tg0 = town.Gold; rq.Sh0 = shelf;
                        reqs.Add(rq);
                    }
                    catch (Exception e) { Stumble322("Winter322(miasto)", e); }
                }

                // 2. zrodla: region miasta (TradeBound - wsie miasta i zamkow; NIE - wsie miasta), wolne na dobe z bilansu zimy kazdej wsi osobno (runda 3)
                var srcOf = srcOfAll;
                if (reqs.Count > 0)
                {
                    var members = new Dictionary<Town, List<Village>>();
                    if (reg324)
                    {
                        foreach (var v in Village.All)
                        {
                            try
                            {
                                if (v == null || v.Settlement == null) continue;
                                var tb = v.TradeBound ?? v.Bound;
                                var tt = tb != null ? tb.Town : null;
                                if (tt == null || tb == null || !tb.IsTown) continue;
                                if (AtWar324(v.Settlement, tb)) continue;               // wies w wojnie z miastem targowym - nie jego region
                                List<Village> l; if (!members.TryGetValue(tt, out l)) members[tt] = l = new List<Village>();
                                l.Add(v);
                            }
                            catch (Exception e) { Stumble322("Winter322(region)", e); }
                        }
                    }
                    else
                        foreach (var town in Town.AllTowns)
                        {
                            var st = town != null ? town.Settlement : null;
                            if (st == null || !st.IsTown || st.BoundVillages == null) continue;
                            members[town] = new List<Village>(st.BoundVillages);
                        }
                    // kraina: tylko regiony krain miast z sygnalem (drugi poziom) - reszta niepotrzebna
                    var lands = new HashSet<string>(StringComparer.Ordinal); var own = new HashSet<Town>();
                    foreach (var rq in reqs) { own.Add(rq.T); if (land324 && rq.Land != null) lands.Add(rq.Land); }
                    foreach (var kv in members)
                    {
                        var town = kv.Key;
                        try
                        {
                            if (!own.Contains(town))
                            {
                                if (!land324 || town.Settlement == null) continue;
                                string ln; try { ln = StartCalibration.TownLandOf(town.Settlement); } catch { ln = null; }
                                if (ln == null || ln == "?" || !lands.Contains(ln)) continue;
                            }
                            var rows = new List<W324Src>(); var xs = new List<double>();
                            double sumX = 0.0, sumPos = 0.0;
                            foreach (var v in kv.Value)
                            {
                                try
                                {
                                    double x, rpp; bool sells;
                                    if (!W324Row(v, today, keepDays, spread, kgSigma, bal324, left, e324, k324, kprW, out x, out rpp, out sells, ref notToday, ref noKcal, ref kwVills, ref noBal)) continue;
                                    regVills++;
                                    sumX += x; if (x > 0) sumPos += x; else regNeg++;
                                    if (!(x > 0)) { atFloor++; continue; }
                                    if (!sells) continue;
                                    rows.Add(new W324Src { V = v, Home = town, Rpp = rpp, Castle = v.Bound != town.Settlement }); xs.Add(x);
                                }
                                catch (Exception e) { Stumble322("Winter322(wies)", e); }
                            }
                            regions++;
                            // racji na dobe: z bilansem zimy - x (racje ponad plan) x k (rozlozenie do wiosny z psuciem); bez bilansu - x to juz racje na dobe.
                            // R3-1: region tylko do wyboru zrodel - kazda wies sprzedaje SWOJA nadwyzke max(0, x_v) x k (nie ma dowozu wies -> wies, wiec brak biedniejszej
                            // wsi nie zmniejsza sprzedazy sasiadek; jej brak liczy jej wlasny bilans i popyt na polce). Suma x regionu (netto) - tylko do linii.
                            double perDayX = bal324 && left > 0 ? k324 : 1.0;
                            regFreeR += sumPos * perDayX; regNetR += Math.Max(0.0, sumX) * perDayX;
                            var list = new List<W324Src>();
                            for (int i = 0; i < rows.Count; i++)
                            {
                                double r = xs[i] * perDayX;
                                int rate = (int)Math.Min(int.MaxValue, Math.Floor(r / rows[i].Rpp));
                                // 325: caly wolny zapas wsi - z bilansem zimy x (racje ponad plan do wiosny), bez bilansu rata x dni rozkladu (zapas ponad stala rezerwe)
                                double whole = bal324 && left > 0 ? xs[i] : xs[i] * spread;
                                int a = fillNow ? (int)Math.Min(int.MaxValue, Math.Max(rate, Math.Floor(whole / rows[i].Rpp))) : rate;
                                if (a < 1) { tiny++; continue; }
                                rows[i].Avail = a; rows[i].Rate = rate; list.Add(rows[i]);
                            }
                            if (list.Count > 0) srcOf[town] = list;
                        }
                        catch (Exception e) { Stumble322("Winter322(region)", e); }
                    }
                }

                // 3. dostawa: najpierw wlasny region kazdego miasta, potem (klucz) kraina - z tego, co zostalo w regionach innych miast tej krainy; najglodniejsze najpierw
                reqs.Sort((a, b) => a.Hunger != b.Hunger ? b.Hunger.CompareTo(a.Hunger) : a.Ord.CompareTo(b.Ord));
                for (int pass = 0; pass < (land324 ? 2 : 1); pass++)
                    foreach (var rq in reqs)
                    {
                        try
                        {
                            long need = rq.Pp.Count - rq.KAt;
                            if (need < 1) continue;
                            var src = new List<W324Src>();
                            if (pass == 0) { List<W324Src> l; if (srcOf.TryGetValue(rq.T, out l)) foreach (var c in l) if (c.Avail > 0) src.Add(c); }
                            else
                                foreach (var kv in srcOf)
                                {
                                    if (kv.Key == rq.T || kv.Key.Settlement == null) continue;
                                    if (rq.Land == null) break;   // R3-5
                                    string ln; try { ln = StartCalibration.TownLandOf(kv.Key.Settlement); } catch { ln = null; }
                                    if (ln == null || ln == "?" || ln != rq.Land) continue;
                                    foreach (var c in kv.Value) if (c.Avail > 0 && !AtWar324(c.V.Settlement, rq.St)) src.Add(c);
                                }
                            if (src.Count == 0) continue;
                            var g = W324Alloc(src, need);
                            bool landAny = false;
                            for (int i = 0; i < src.Count; i++)
                            {
                                if (g[i] < 1) continue;
                                try
                                {
                                    int got = W324Give(rq, src[i], g[i], pass == 1, today, payMode, soldV, ref rations, ref barnDrop, ref vilIn, ref credGold, ref credFail, ref komGold, ref komPcs);
                                    if (got < 1) continue;
                                    if (!rq.Any) { fedTowns++; firstSum += rq.Pp[Math.Min(rq.Pp.Count - 1, rq.KAt - got)]; rq.Any = true; if (rq.OutZ) outFed++; }
                                    if (rq.OutZ) outPieces += got;
                                    if (pass == 1) { pcsLand += got; landAny = true; } else if (src[i].Castle) pcsCastle += got; else pcsTown += got;
                                    pieces += got; fedVills++; rppSum += src[i].Rpp; rppN++;
                                    src[i].Given += got;
                                }
                                catch (Exception e) { Stumble322("Winter322(wies)", e); }
                            }
                            if (landAny) landFedTowns++;
                        }
                        catch (Exception e) { Stumble322("Winter322(miasto)", e); }
                    }
                foreach (var rq in reqs)
                {
                    try
                    {
                        if (!rq.Any) { noVill++; continue; }
                        if (fillNow) { fillPcs += rq.KAt; fillOver2 += Math.Max(0, rq.KAt - rq.Cap2); }
                        gold += rq.Cash; townOut += rq.Tg0 - rq.T.Gold;
                        if (rq.Cred > 0) credTowns++;
                        if (rq.Kom > 0) komTowns++;
                        lastSum += rq.Pp[Math.Max(0, Math.Min(rq.Pp.Count - 1, rq.KAt - 1))];
                        int sh1 = rq.St.ItemRoster.GetItemNumber(grain);
                        shelfIn += sh1 - rq.Sh0;
                        after.Add((sh1 + rq.Inc) / rq.Dp);
                    }
                    catch (Exception e) { Stumble322("Winter322(miasto)", e); }
                }
            }
            if (fillNow) foreach (var kv in srcOfAll) foreach (var c in kv.Value) fillOverRate += Math.Max(0, c.Given - c.Rate);
            W324ShelfMark(today);   // runda 3: polka po dostawach - podstawa sprzedanych komisu jutro
            _w322SessPieces += pieces; _w322SessRations += rations; _w322SessGold += gold;
            _w324Day = today; _w324Pieces = (int)Math.Min(int.MaxValue, pieces); _w324PiecesOut = (int)Math.Min(int.MaxValue, outPieces); _w324TownsOut = outFed;
            _w324PiecesLand = (int)Math.Min(int.MaxValue, pcsLand); _w324PiecesCastle = (int)Math.Min(int.MaxValue, pcsCastle); _w324Cred = credGold;
            if (s.LogEnabled)
            {
                after.Sort();
                long vOpen, vPaid; int vTowns, vPaidDay; KsFoodCredit.VillageStats(out vOpen, out vTowns, out vPaid, out vPaidDay);
                long kPcs, kVal; int kTowns; KsFoodCredit.ConsignStats(out kPcs, out kVal, out kTowns);
                var sb = new StringBuilder(2000);
                sb.Append("Zboze zima (322c): dzien ").Append(today)
                  .Append(!live ? " | SPICHLERZE WSI NIECZYNNE (Famine albo p6) - nic" : !applied ? " | KS NIE ZASTOSOWANY (brak popytu zaplaconego ZC4) - nic" : grain == null ? " | BRAK towaru grain - nic" : !inWinter ? " | POZA ZIMA - nic" : " | CZYNNE")
                  .Append(" | zima ").Append(winter ? "TAK" : "NIE").Append(" (do konca ").Append(left).Append(" dni; wies rozklada zapas ponad rezerwe na ").Append(F(spread, "0")).Append(" dni)")
                  .Append(bal324 ? " | rezerwa wsi z bilansu zimy (324): prog racjonowania Famine + brak wlasnego stolu do konca zimy, z psuciem f23 (rho " + F(rho324, "0.0000000") + "/d, e^(rho x dni) " + F(e324, "0.000") + ", efektywnie " + F(k324 > 0 ? 1.0 / k324 : double.NaN, "0") + " dni)"
                                 : " | rezerwa wsi " + F(keepDays, "0") + " dni wlasnej potrzeby (klucz " + keepKey + ", bramka krainy " + StartCalibration.LandGateFloorDays + ")")
                  .Append(" | 324 caly swiat ").Append(all324 ? "TAK" : "NIE").Append(": miasta poza ZC4 z popytem z modelu gry ").Append(outTowns).Append(", zasilone ").Append(outFed).Append(", ").Append(outPieces)
                  .Append(" szt.; wsie z kcal racji swiata ").Append(kwVills).Append(" (").Append(F(kprW, "0")).Append(" kcal)")
                  .Append(" | miast ").Append(towns).Append(" (oblezone ").Append(besieged).Append(", Innych ").Append(walkers).Append(", poza ZC4 ").Append(noKs).Append(')')
                  .Append(" | z sygnalem ").Append(sig).Append(" (polka < ").Append(fill324 ? "cel T*" : F(W322ShelfLowDays, "0.0") + " dnia").Append(" popytu ").Append(sigShelf).Append(", indeks >= ").Append(F(trigIx, "0.0")).Append(' ').Append(sigIx)
                  .Append("; polka pelna do celu ").Append(full).Append(", bez kasy i kredytu ").Append(noGold).Append(", bez wsi z zapasem ").Append(noVill).Append(", bez wyceny ").Append(noPrice).Append(')')
                  .Append(" | 325 napelnienie do celu teraz ").Append(fillNow ? "TAK" : "NIE").Append(": napelnienie do celu dzis ").Append(fillPcs).Append(" szt. (z zapasu ponad rate ").Append(fillOverRate)
                  .Append(" szt.; ponad dawny limit 2 dob popytu ").Append(fillOver2).Append(" szt.)")
                  .Append(" | chcialy ").Append(F(wantSum, "0")).Append(" szt. | na polki dzis ").Append(pieces).Append(" szt. zboza (").Append(F(rations, "0")).Append(" racji, racji na sztuke srednio ")
                  .Append(rppN > 0 ? F(rppSum / rppN, "0.0") : "-").Append("; sigma ").Append(sigma).Append(", kg sztuki ").Append(F(kg, "0.##")).Append(") z ").Append(fedVills).Append(" wsi do ").Append(fedTowns).Append(" miast")
                  .Append(" | zloto miasto->wies ").Append(gold).Append(" zl (cena srednio ").Append(pieces > 0 ? F((gold + credGold + komGold) / (double)pieces, "0.#") : "-").Append(" zl/szt. bez pulapu, sztuka po sztuce w dol krzywej polki; pierwsza sztuka srednio ")
                  .Append(fedTowns > 0 ? F(firstSum / fedTowns, "0.#") : "-").Append(" zl, ostatnia srednio ").Append(fedTowns > 0 ? F(lastSum / fedTowns, "0.#") : "-").Append(" zl; wartosc ").Append(grain != null ? grain.Value : 0).Append(" zl)")
                  .Append(" | 324 region ").Append(reg324 ? "TAK (wsie z miastem targowym; kazda wies sprzedaje swoja nadwyzke)" : "NIE (wsie miasta)").Append(": regionow ").Append(regions).Append(", wsi z bilansem ").Append(regVills).Append(" (bez nadwyzki ").Append(regNeg)
                  .Append("), wolne regionow ").Append(F(regFreeR, "0")).Append(" racji/d (suma nadwyzek wsi; netto z brakiem biedniejszych ").Append(F(regNetR, "0")).Append(" - nie liczy sie); sztuki z wsi miasta ").Append(pcsTown).Append(", z wsi zamkow ").Append(pcsCastle)
                  .Append(" | 324 kraina ").Append(land324 ? "TAK" : "NIE").Append(": ").Append(pcsLand).Append(" szt. z regionow innych miast krainy do ").Append(landFedTowns).Append(" miast")
                  .Append(" | 324 kredyt wsi ").Append(cred324 ? (kom324 ? "TAK (zastapiony komisem)" : "TAK") : "NIE").Append(": na rachunek wsi dzis ").Append(credGold).Append(" zl (").Append(credTowns).Append(" miast; pulap z raty 322e razem ").Append(credCapSum)
                  .Append(", nieudane ").Append(credFail).Append("); rachunki wsi otwarte ").Append(vOpen).Append(" zl (").Append(vTowns).Append(" miast), splacone wsiom w PayDaily doby ")
                  .Append(vPaidDay == int.MinValue ? "-" : vPaidDay.ToString(Inv)).Append(' ').Append(vPaid).Append(" zl")
                  .Append(" | 324 komis ").Append(kom324 ? "TAK" : "NIE").Append(": wydane w komis dzis ").Append(komPcs).Append(" szt. / ").Append(komGold).Append(" zl (").Append(komTowns).Append(" miast)")
                  .Append("; sprzedane z komisu ").Append(_w324KomSoldPcs).Append(" szt. / ").Append(_w324KomSoldVal).Append(" zl: splacone wsiom z utargu ").Append(_w324KomPaid)
                  .Append(" zl, na rachunek wsi ").Append(_w324KomSoldBilled).Append(" zl (kasa bez utargu); po terminie bez sztuk na polce ").Append(_w324KomBilledPcs).Append(" szt. / ").Append(_w324KomBilled - _w324KomSoldBilled).Append(" zl na rachunek wsi")
                  .Append("; zwrocone po terminie ").Append(KsFoodCredit.RateReleaseDays > 0 ? KsFoodCredit.RateReleaseDays : KsFoodCredit.OverdueDays).Append(" dni do stodol ").Append(_w324KomBackPcs).Append(" szt. / ").Append(_w324KomBack).Append(" zl")
                  .Append(" (bez kcal racji ").Append(_w324KomNoRpp).Append("); w komisie ").Append(kPcs).Append(" szt. / ").Append(kVal).Append(" zl (").Append(kTowns).Append(" miast)")
                  .Append("; kontrola: sprzedane ").Append(_w324KomSoldVal).Append(" = kiesy wsi ").Append(_w324KomPaid).Append(" + rachunki wsi ").Append(_w324KomSoldBilled).Append(" (reszta ")
                  .Append(_w324KomSoldVal - _w324KomPaid - _w324KomSoldBilled).Append(')')
                  .Append(" | wsie: na rezerwie ").Append(atFloor).Append(", bez rachunku KS (kcal racji) ").Append(noKcal).Append(", bez migawki doby/glod/bez spichlerza ").Append(notToday).Append(", bez bilansu po wczytaniu ").Append(noBal).Append(", ponizej sztuki dzis ").Append(tiny)
                  .Append(" | polka po dostawie (dni popytu, z KS-H): mediana ").Append(after.Count > 0 ? F(after[(after.Count - 1) / 2], "0.00") : "-")
                  .Append(" | kontrola: racje zdjete ").Append(F(rations, "0.##")).Append(" = ubytek stodol wsi ").Append(F(barnDrop, "0.##"))
                  .Append("; polki +").Append(shelfIn).Append(" = sztuki ").Append(pieces)
                  .Append("; zloto: kasy miast -").Append(townOut).Append(", kiesy wsi +").Append(vilIn).Append(", zaplata ").Append(gold).Append("; wartosc dostaw ").Append(gold + credGold + komGold).Append(" = gotowka ").Append(gold).Append(" + rachunki wsi ").Append(credGold).Append(" + komis ").Append(komGold)
                  .Append("; od wczytania ").Append(F(_w322SessPieces, "0")).Append(" szt., ").Append(F(_w322SessRations, "0")).Append(" racji, ").Append(_w322SessGold).Append(" zl")
                  .Append(" | potkniecia ").Append(_w322Stum).Append(" (razem ").Append(_w322StumAll).Append(')')
                  .Append(" | czas ").Append(((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency).ToString("0.0", Inv)).Append(" ms.");
                Log.Info(sb.ToString());
            }
            _w322Stum = 0;
        }

        /// <summary>324 runda 2/3: dostawa `n` sztuk ze spichlerza wsi `c` na polke miasta `rq` - racje schodza ze stodoly (TributeTake, ulamek wraca), cale sztuki na polke
        /// (ramka ksiegi), zaplata sztuka po sztuce z cennika miasta: najpierw cale sztuki z nadwyzki kasy ponad zapas kupcow (gotowka, kasa miasta -> kiesa wsi, z pomiarem),
        /// reszta (runda 3, `mode`): 2 - komis (KsFoodCredit.OpenConsign: miasto odda wsi cene dostawy z utargu sprzedanych sztuk), 1 - rachunek wsi w pulapie kredytu
        /// (CredLeft), 0 - nic (tylko gotowka). Komis albo rachunek otwierany PRZED polozeniem towaru na polce (R3-4: bez towaru bez zaplaty i bez rachunku ponad pulap);
        /// gdy sie nie otworzy - te sztuki wracaja do stodoly. Zwraca sztuki na polce. Wyjatek leci do wolajacego.</summary>
        private static int W324Give(W324Req rq, W324Src c, int n, bool land, int today, int mode, Dictionary<Village, double> soldV,
                                    ref double rations, ref double barnDrop, ref long vilIn, ref long credGold, ref int credFail, ref long komGold, ref long komPcs)
        {
            n = Math.Min(n, Math.Min(c.Avail, rq.Pp.Count - rq.KAt));
            if (n < 1) return 0;
            // R3-4: ile sztuk miasto pokryje - cale sztuki z gotowki (nadwyzka ponad zapas kupcow), potem komis (bez pulapu kwoty - ilosc ogranicza cel polki
            // i 2 doby popytu w cenniku) albo rachunek wsi do pulapu
            long cv = 0, rv = 0; int m1 = 0;
            while (m1 < n && cv + rq.Pp[rq.KAt + m1] <= rq.CashLeft) { cv += rq.Pp[rq.KAt + m1]; m1++; }
            int m = m1;
            if (mode == 2) m = n;
            else if (mode == 1) while (m < n) { long p = rq.Pp[rq.KAt + m]; if (rv + p > rq.CredLeft) break; rv += p; m++; }
            n = m;
            if (n < 1) return 0;
            var v = c.V; var st = rq.St; var town = rq.T; double rpp = c.Rpp;
            double f0; bool hasF = Famine.FoodOf(v, out f0);
            double took = Famine.TributeTake(v, n * rpp);
            if (!(took > 0.0)) return 0;
            int got = (int)Math.Floor(took / rpp + 1e-9);
            if (got > n) got = n;
            // podzial przyjetych sztuk: gotowka (pierwsze m1) i reszta (komis albo rachunek) - cennik sztuka po sztuce
            int cashPcs = Math.Min(m1, got), restPcs = got - cashPcs;
            long cashPay = 0, restPay = 0;
            for (int k = 0; k < cashPcs; k++) cashPay += rq.Pp[rq.KAt + k];
            for (int k = cashPcs; k < got; k++) restPay += rq.Pp[rq.KAt + k];
            if (restPcs > 0)
            {
                bool opened = v.Settlement != null && (mode == 2 ? KsFoodCredit.OpenConsign(v.Settlement, st, restPcs, restPay, today)
                                                               : mode == 1 && restPay <= rq.CredLeft && KsFoodCredit.OpenVillage(v.Settlement, st, restPay, today));
                if (opened)
                {
                    if (mode == 2) { komGold += restPay; komPcs += restPcs; rq.Kom += restPay; }
                    else { rq.CredLeft -= restPay; rq.Cred += restPay; credGold += restPay; }
                }
                else { credFail++; got = cashPcs; restPcs = 0; restPay = 0; }   // te sztuki wracaja do stodoly (ponizej, razem z ulamkiem)
            }
            // gotowka: kasa miasta -> kiesa wsi (z pomiarem) - PRZED zwrotem do stodoly (325, recenzja R3 pkt 2: sztuki bez zaplaty wracaja do stodoly, nie na rachunek w trybie 0)
            int vg0 = v.Gold, tg0 = town.Gold;
            if (cashPay > 0 && cashPcs > 0) { town.ChangeGold(-(int)Math.Min(int.MaxValue, cashPay)); v.ChangeGold((int)(tg0 - town.Gold)); }
            long paidCash = tg0 - town.Gold;
            long shortfall = cashPcs > 0 ? cashPay - paidCash : 0;
            if (shortfall > 0)
            {
                // czego kasa nie dala (nie powinno sie zdarzyc - nadwyzka <= kasa): tryb 1/2 - rachunek wsi; tryb 0 (tylko gotowka) albo rachunek sie nie otworzyl -
                // nieoplacone sztuki wracaja do stodoly, a nadplata ponad cale oplacone sztuki wraca z kiesy wsi do kasy miasta
                if (mode != 0 && v.Settlement != null && KsFoodCredit.OpenVillage(v.Settlement, st, shortfall, today)) { rq.Cred += shortfall; credGold += shortfall; }
                else
                {
                    credFail++;
                    int kp = 0; long pre = 0;
                    while (kp < cashPcs && pre + rq.Pp[rq.KAt + kp] <= paidCash) { pre += rq.Pp[rq.KAt + kp]; kp++; }
                    long refund = paidCash - pre;
                    if (refund > 0) { int vb = v.Gold; v.ChangeGold(-(int)Math.Min(int.MaxValue, refund)); long r2 = vb - v.Gold; if (r2 > 0) town.ChangeGold((int)r2); paidCash -= r2; }
                    got -= cashPcs - kp; cashPcs = kp;
                }
            }
            vilIn += v.Gold - vg0;
            rq.CashLeft -= paidCash; rq.Cash += paidCash;
            double back = took - got * rpp;
            if (back > 1e-6 && !Famine.TributeGive(v, back)) back = 0.0;   // ulamek i sztuki bez zaplaty wracaja do spichlerza (spichlerz jest - wlasnie z niego zdjeto)
            double net = took - back;
            if (net > 0.0) { double was; soldV.TryGetValue(v, out was); soldV[v] = was + net; }   // od razu - bramka krainy (zanim cos rzuci dalej)
            double f1; if (hasF && Famine.FoodOf(v, out f1)) barnDrop += f0 - f1;
            if (net > 0.0) rations += net;
            c.Avail -= got;
            if (got < 1) return 0;
            var gf = GoodsLedger.Begin(GoodsLedger.FZc1, st);
            if (gf != null) gf.Info = land ? "zboze ze spichlerza wsi krainy (324)" : "zboze ze spichlerza wsi (322c)";
            try { st.ItemRoster.AddToCounts(DefaultItems.Grain, got); }
            finally { GoodsLedger.End(gf); }
            rq.KAt += got;
            return got;
        }

        // ------------------------------------------------------------ 324 runda 3: komis zboza wsi (KsFoodCredit konto Kom)
        private static readonly Dictionary<Settlement, KeyValuePair<int, int>> _w324ShelfAfter = new Dictionary<Settlement, KeyValuePair<int, int>>();   // miasto -> (doba, polka zboza po dostawie)
        private static int _w324KomDay = int.MinValue; private static long _w324KomSoldPcs, _w324KomSoldVal, _w324KomPaid, _w324KomBilled, _w324KomBackPcs, _w324KomBack, _w324KomBilledPcs, _w324KomSoldBilled;
        private static int _w324KomTowns, _w324KomNoRpp;

        /// <summary>324 runda 3: komis tej doby (przed dostawami 322c, takze poza zima i przy wylaczonym kluczu - komis trzeba rozliczyc). SPRZEDANE sztuki komisu miasta:
        /// polka teraz S0, polka po wczorajszej dostawie S1, sztuk w komisie K - sprzedane = max(K - S0 (wiecej komisu niz polki byc nie moze), (S1 - S0) x K / S1
        /// (spadek polki dzielony proporcjonalnie na komis i reszte)); bez S1 (pierwsza doba po wczytaniu) - tylko pierwsza czesc. Termin: Ks Consign Rate Release Days
        /// (0 - Ks Consign Overdue Days). Zwrot: sztuki z polki (najwyzej polka) -> racje do stodoly wsi (racji na sztuke jak przy dostawie). Nie rzuca.</summary>
        private static void W324Consign(Settings s, int today)
        {
            _w324KomDay = today; _w324KomSoldPcs = _w324KomSoldVal = _w324KomPaid = _w324KomBilled = _w324KomBackPcs = _w324KomBack = _w324KomBilledPcs = _w324KomSoldBilled = 0; _w324KomTowns = _w324KomNoRpp = 0;
            Dictionary<Settlement, int> kt;
            try { kt = KsFoodCredit.ConsignByTown(); } catch (Exception e) { Stumble322("Winter322(komis)", e); return; }
            if (kt.Count == 0) { _w324ShelfAfter.Clear(); return; }
            var grain = DefaultItems.Grain;
            if (grain == null) return;
            int rel = KsFoodCredit.RateReleaseDays; if (rel <= 0) rel = KsFoodCredit.OverdueDays;
            int sigma = Math.Max(1, DailyLife.KsSigma);
            double kgSigma = (grain.Weight > 0.01f ? grain.Weight : 10.0) * sigma;
            double kprW = StartCalibration.Applied ? StartCalibration.KcalPerRationWorld324(today) : double.NaN;
            long d0Sold = KsFoodCredit.DKomSoldPcs, d0Val = KsFoodCredit.DKomSoldVal, d0Paid = KsFoodCredit.DKomPaid, d0Bil = KsFoodCredit.DKomBilled, d0BackP = KsFoodCredit.DKomBackPcs, d0Back = KsFoodCredit.DKomBack, d0BilP = KsFoodCredit.DKomBilledPcs, d0SB = KsFoodCredit.DKomSoldBilled;
            foreach (var kv in kt)
            {
                try
                {
                    var st = kv.Key; if (st == null || st.ItemRoster == null) continue;
                    _w324KomTowns++;
                    int K = kv.Value, s0 = st.ItemRoster.GetItemNumber(grain);
                    double sold = Math.Max(0, K - s0);
                    KeyValuePair<int, int> pr;
                    if (_w324ShelfAfter.TryGetValue(st, out pr) && pr.Key == today - 1 && pr.Value > 0 && pr.Value > s0)
                        sold = Math.Max(sold, (pr.Value - s0) * (double)Math.Min(K, pr.Value) / pr.Value);
                    int soldN = (int)Math.Max(0, Math.Min(K, Math.Floor(sold + 1e-9)));
                    Func<Settlement, int, int> back = (vs, want) =>
                    {
                        var v = vs != null ? vs.Village : null;
                        if (v == null || want < 1) return 0;
                        double k = StartCalibration.KcalPerRation322(v);
                        if (double.IsNaN(k)) k = kprW;
                        double rpp = !double.IsNaN(k) && k > 0 ? W322KcalGrain * kgSigma / k : double.NaN;
                        if (!(rpp > 0) || double.IsInfinity(rpp)) { _w324KomNoRpp++; return 0; }
                        int r = Math.Min(want, st.ItemRoster.GetItemNumber(grain));
                        if (r < 1) return 0;
                        var gf = GoodsLedger.Begin(GoodsLedger.FZc1, st);
                        if (gf != null) gf.Info = "zboze z komisu wraca do spichlerza wsi (324)";
                        try { st.ItemRoster.AddToCounts(grain, -r); }
                        finally { GoodsLedger.End(gf); }
                        if (Famine.TributeGive(v, r * rpp)) return r;
                        var gf2 = GoodsLedger.Begin(GoodsLedger.FZc1, st);                 // stodola nie przyjela - sztuki wracaja na polke (rozliczenie rachunkiem wsi)
                        if (gf2 != null) gf2.Info = "zboze z komisu wraca do spichlerza wsi (324)";
                        try { st.ItemRoster.AddToCounts(grain, r); }
                        finally { GoodsLedger.End(gf2); }
                        return 0;
                    };
                    KsFoodCredit.ConsignSettle(st, soldN, today, rel, back);
                }
                catch (Exception e) { Stumble322("Winter322(komis)", e); }
            }
            _w324KomSoldPcs = KsFoodCredit.DKomSoldPcs - d0Sold; _w324KomSoldVal = KsFoodCredit.DKomSoldVal - d0Val; _w324KomPaid = KsFoodCredit.DKomPaid - d0Paid;
            _w324KomBilled = KsFoodCredit.DKomBilled - d0Bil; _w324KomBackPcs = KsFoodCredit.DKomBackPcs - d0BackP; _w324KomBack = KsFoodCredit.DKomBack - d0Back; _w324KomBilledPcs = KsFoodCredit.DKomBilledPcs - d0BilP; _w324KomSoldBilled = KsFoodCredit.DKomSoldBilled - d0SB;
        }

        /// <summary>324 runda 3: polka zboza po dostawach doby `today` miast z komisem (podstawa sprzedanych jutro). Nie rzuca.</summary>
        private static void W324ShelfMark(int today)
        {
            try
            {
                _w324ShelfAfter.Clear();
                var grain = DefaultItems.Grain; if (grain == null) return;
                foreach (var kv in KsFoodCredit.ConsignByTown())
                    if (kv.Key != null && kv.Key.ItemRoster != null) _w324ShelfAfter[kv.Key] = new KeyValuePair<int, int>(today, kv.Key.ItemRoster.GetItemNumber(grain));
            }
            catch (Exception e) { Stumble322("Winter322(komis)", e); }
        }
    }
}
