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

        /// <summary>324: dowoz 322c doby `today` (0 - inna doba albo nic). Tylko watek glowny. Nie rzuca.</summary>
        internal static void Last324(int today, out int pieces, out int piecesOut, out int townsOut)
        {
            bool ok = _w324Day == today;
            pieces = ok ? _w324Pieces : 0; piecesOut = ok ? _w324PiecesOut : 0; townsOut = ok ? _w324TownsOut : 0;
        }

        private static void Reset322()
        {
            _w322SessPieces = _w322SessRations = 0.0; _w322SessGold = 0;
            _w322Stum = _w322StumAll = 0; _w322OffLogged = false;
            _w324Day = int.MinValue; _w324Pieces = _w324PiecesOut = _w324TownsOut = 0;
        }

        private static void Stumble322(string where, Exception e)
        {
            _w322Stum++; _w322StumAll++;
            Stumble(where, e);
        }

        /// <summary>Ile racji wies moze dzis sprzedac na polke (zapas ponad rezerwe rozlozony na reszte zimy) i ile racji to jedna sztuka; false - wies nie sprzedaje
        /// (licznik przyczyny). Wyjatek leci do wolajacego.
        /// 324: `bal` (klucz Winter Grain From Winter Balance) - wolne z bilansu zimy: max(0, zapas - floor - left x def) / left racji na dobe
        /// (StartCalibration.WinterBalance324) zamiast zapasu ponad stala rezerwe / spread; `kprWorld` (klucz Winter Grain All Towns, inaczej NaN) - kcal racji wsi bez
        /// rachunku KS (licznik kw).</summary>
        private static bool W322Village(Village v, int today, double keepDays, double spread, double kgSigma, bool bal, int left, double kprWorld, out double dayR, out double rpp, ref int atFloor, ref int noKcal, ref int notToday, ref int kw)
        {
            dayR = 0.0; rpp = 0.0;
            if (v == null || v.Settlement == null || v.VillageState != Village.VillageStates.Normal || v.Settlement.IsUnderRaid) return false;
            double home, harv, food0, vcap; float u; int d;
            if (!Famine.DayOf(v, out home, out harv, out food0, out vcap, out u, out d) || d != today || u > 0f || !(home > 0.0)) { notToday++; return false; }
            double food;
            if (!Famine.FoodOf(v, out food)) { notToday++; return false; }   // zapas po p6 tej doby
            double spare, sp;
            if (bal && left > 0)
            {
                // 324: bilans zimy - zapas ponad prog racjonowania i ponad brak wlasnego stolu do wiosny (left x def), rozlozony na reszte zimy
                double def = StartCalibration.WinterDeficit324(v);
                if (double.IsNaN(def)) def = Math.Max(0.0, home - harv);           // wies poza KS: plon Famine bez zakupow
                var s0 = Settings.Current;
                double floor = Math.Max(0, s0 != null ? s0.FamineRationDays : 90) * home;
                spare = food - floor - left * def; sp = left;
            }
            else { spare = food - keepDays * home; sp = spread; }
            if (!(spare > 0.0)) { atFloor++; return false; }
            double k = StartCalibration.KcalPerRation322(v);
            if (double.IsNaN(k) && !double.IsNaN(kprWorld)) { k = kprWorld; kw++; }   // 324: wies bez rachunku KS - kcal racji sredni swiata
            if (double.IsNaN(k)) { noKcal++; return false; }
            rpp = W322KcalGrain * kgSigma / k;
            if (!(rpp > 0.0) || double.IsInfinity(rpp)) { noKcal++; return false; }
            dayR = spare / sp;
            return true;
        }

        /// <summary>322c: doba (TownGranary.Daily, po p6, przed zapisem ksiegi dnia). Linia "Zboze zima (322c): ...". Wyjatek per miasto liczony.</summary>
        private static void Winter322(Settings s, int today, bool live, Dictionary<Village, double> soldV)
        {
            if (s == null) return;
            if (!s.WinterGrainToMarket)
            {
                if (!_w322OffLogged && s.LogEnabled) { _w322OffLogged = true; Log.Info("Zboze zima (322c): WYLACZONE (Winter Grain To Market w MCM) - spichlerze wsi tylko do zapasu jedzenia miasta (p6), jak dotad."); }
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
            double kprW = all324 && applied ? StartCalibration.KcalPerRationWorld324(today) : double.NaN;
            int outTowns = 0, outFed = 0, kwVills = 0; long outPieces = 0;
            var model = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.TradeItemPriceFactorModel : null;
            var gcat = grain != null ? grain.ItemCategory : null;
            int wv = grain != null ? Math.Max(1, HistoricalPrices.ShelfWorth(grain)) : 1;   // paczka 121: jednostka polki = waga sztuki

            int towns = 0, besieged = 0, walkers = 0, noKs = 0, sig = 0, sigShelf = 0, sigIx = 0, full = 0, noGold = 0, fedTowns = 0, fedVills = 0, noPrice = 0, noVill = 0;
            int atFloor = 0, noKcal = 0, notToday = 0, tiny = 0;
            long pieces = 0, gold = 0, townOut = 0, vilIn = 0, shelfIn = 0; double rations = 0.0, barnDrop = 0.0, firstSum = 0.0, lastSum = 0.0, rppSum = 0.0, wantSum = 0.0; int rppN = 0;
            var after = new List<double>();
            if (live && applied && grain != null && inWinter)
            {
                var cand = new List<Village>(); var dayR = new List<double>(); var rppL = new List<double>(); var giveN = new List<int>(); var pp = new List<int>();
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
                        bool lowShelf = (shelf + inc) < W322ShelfLowDays * dp, hiIx = ix >= trigIx;
                        if (!lowShelf && !hiIx) continue;
                        sig++; if (lowShelf) sigShelf++; if (hiIx) sigIx++;
                        double want = Math.Min(tstar * dp - shelf - inc, W322DayCapDays * dp);
                        if (want < 1.0) { full++; continue; }
                        if (model == null || gcat == null || town.MarketData == null) { noPrice++; continue; }
                        long spend = Math.Max(0, TownPurse.SpendableTown(town));   // tylko nadwyzka ponad zapas kupcow (jak p6) - bez zaleglosci firmy DS i pozyczek rady
                        // cena od polki bez pulapu: sztuka k po cenie sprzedajacego przy polce powiekszonej o k sztuk (jak tabory wsi - MarketCarts); polka z chwili przed dostawa
                        var md = town.MarketData.GetCategoryData(gcat);
                        var el = new EquipmentElement(grain);
                        pp.Clear();
                        long cum = 0; int maxN = (int)Math.Min(100000.0, Math.Floor(want));
                        for (int k = 0; k < maxN; k++)
                        {
                            int p = Math.Max(1, model.GetPrice(el, null, null, true, md.InStoreValue + (float)k * wv, md.Supply, md.Demand));
                            if (cum + p > spend) break;
                            cum += p; pp.Add(p);
                        }
                        long wantN = pp.Count;
                        if (wantN < 1) { noGold++; continue; }
                        wantSum += wantN;
                        // wsie miasta: ile kazda moze dac dzis (cale sztuki)
                        cand.Clear(); dayR.Clear(); rppL.Clear(); giveN.Clear();
                        var bv = st.BoundVillages;
                        long avail = 0;
                        if (bv != null)
                            for (int i = 0; i < bv.Count; i++)
                            {
                                double r, rpp;
                                if (!W322Village(bv[i], today, keepDays, spread, kgSigma, bal324, left, kprW, out r, out rpp, ref atFloor, ref noKcal, ref notToday, ref kwVills)) continue;
                                int a = (int)Math.Min(int.MaxValue, Math.Floor(r / rpp));
                                if (a < 1) { tiny++; continue; }
                                cand.Add(bv[i]); dayR.Add(a); rppL.Add(rpp); giveN.Add(0); avail += a;
                            }
                        if (avail < 1) { noVill++; continue; }
                        long need = Math.Min(wantN, avail), given = 0;
                        for (int i = 0; i < cand.Count; i++) { int g = (int)Math.Min(dayR[i], Math.Floor(need * dayR[i] / avail)); giveN[i] = g; given += g; }
                        for (int i = 0; i < cand.Count && given < need; i++) { int g = (int)Math.Min(dayR[i] - giveN[i], need - given); if (g > 0) { giveN[i] += g; given += g; } }
                        bool any = false; int kAt = 0;   // kAt - ile sztuk miasto juz dzis przyjelo (nastepna sztuka idzie dalej w dol krzywej)
                        int tg0 = town.Gold, sh0 = st.ItemRoster.GetItemNumber(grain);
                        for (int i = 0; i < cand.Count; i++)
                        {
                            int n = giveN[i];
                            if (n < 1) continue;
                            var v = cand[i]; double rpp = rppL[i];
                            try
                            {
                                double f0; bool hasF = Famine.FoodOf(v, out f0);
                                double took = Famine.TributeTake(v, n * rpp);
                                if (!(took > 0.0)) continue;
                                int got = (int)Math.Floor(took / rpp + 1e-9);
                                if (got > n) got = n;
                                double back = took - got * rpp;
                                if (back > 1e-6 && !Famine.TributeGive(v, back)) back = 0.0;   // ulamek ponad cale sztuki wraca do spichlerza (spichlerz jest - wlasnie z niego zdjeto)
                                double net = took - back;
                                if (!(net > 0.0)) continue;
                                { double was; soldV.TryGetValue(v, out was); soldV[v] = was + net; }   // od razu - bramka krainy (zanim cos rzuci dalej)
                                double f1; if (hasF && Famine.FoodOf(v, out f1)) barnDrop += f0 - f1;
                                rations += net;
                                if (got < 1) continue;   // wszystko wrocilo (nie powinno sie zdarzyc) - net 0
                                var gf = GoodsLedger.Begin(GoodsLedger.FZc1, st);
                                if (gf != null) gf.Info = "zboze ze spichlerza wsi (322c)";
                                try { st.ItemRoster.AddToCounts(grain, got); }
                                finally { GoodsLedger.End(gf); }
                                long pay = 0;
                                for (int k = 0; k < got; k++) pay += pp[Math.Min(pp.Count - 1, kAt + k)];
                                long left0 = spend - (tg0 - town.Gold);
                                if (pay > left0) pay = Math.Max(0, left0);
                                int vg0 = v.Gold;
                                if (pay > 0) { town.ChangeGold(-(int)pay); v.ChangeGold((int)pay); }
                                vilIn += v.Gold - vg0;
                                if (!any) { fedTowns++; firstSum += pp[Math.Min(pp.Count - 1, kAt)]; any = true; if (outZ) outFed++; }
                                if (outZ) outPieces += got;
                                kAt += got;
                                pieces += got; gold += pay; fedVills++; rppSum += rpp; rppN++;
                            }
                            catch (Exception e) { Stumble322("Winter322(wies)", e); }
                        }
                        if (any)
                        {
                            townOut += tg0 - town.Gold;
                            lastSum += pp[Math.Max(0, Math.Min(pp.Count - 1, kAt - 1))];
                            int sh1 = st.ItemRoster.GetItemNumber(grain);
                            shelfIn += sh1 - sh0;
                            after.Add((sh1 + inc) / dp);
                        }
                    }
                    catch (Exception e) { Stumble322("Winter322(miasto)", e); }
                }
            }
            _w322SessPieces += pieces; _w322SessRations += rations; _w322SessGold += gold;
            _w324Day = today; _w324Pieces = (int)Math.Min(int.MaxValue, pieces); _w324PiecesOut = (int)Math.Min(int.MaxValue, outPieces); _w324TownsOut = outFed;
            if (s.LogEnabled)
            {
                after.Sort();
                var sb = new StringBuilder(1400);
                sb.Append("Zboze zima (322c): dzien ").Append(today)
                  .Append(!live ? " | SPICHLERZE WSI NIECZYNNE (Famine albo p6) - nic" : !applied ? " | KS NIE ZASTOSOWANY (brak popytu zaplaconego ZC4) - nic" : grain == null ? " | BRAK towaru grain - nic" : !inWinter ? " | POZA ZIMA - nic" : " | CZYNNE")
                  .Append(" | zima ").Append(winter ? "TAK" : "NIE").Append(" (do konca ").Append(left).Append(" dni; wies rozklada zapas ponad rezerwe na ").Append(F(spread, "0")).Append(" dni)")
                  .Append(bal324 ? " | rezerwa wsi z bilansu zimy (324): prog racjonowania Famine + brak wlasnego stolu x dni do konca zimy, wolne rozlozone na reszte zimy"
                                 : " | rezerwa wsi " + F(keepDays, "0") + " dni wlasnej potrzeby (klucz " + keepKey + ", bramka krainy " + StartCalibration.LandGateFloorDays + ")")
                  .Append(" | 324 caly swiat ").Append(all324 ? "TAK" : "NIE").Append(": miasta poza ZC4 z popytem z modelu gry ").Append(outTowns).Append(", zasilone ").Append(outFed).Append(", ").Append(outPieces)
                  .Append(" szt.; wsie z kcal racji swiata ").Append(kwVills).Append(" (").Append(F(kprW, "0")).Append(" kcal)")
                  .Append(" | miast ").Append(towns).Append(" (oblezone ").Append(besieged).Append(", Innych ").Append(walkers).Append(", poza ZC4 ").Append(noKs).Append(')')
                  .Append(" | z sygnalem ").Append(sig).Append(" (polka < ").Append(F(W322ShelfLowDays, "0.0")).Append(" dnia popytu ").Append(sigShelf).Append(", indeks >= ").Append(F(trigIx, "0.0")).Append(' ').Append(sigIx)
                  .Append("; polka pelna do celu ").Append(full).Append(", bez kasy ").Append(noGold).Append(", bez wsi z zapasem ").Append(noVill).Append(", bez wyceny ").Append(noPrice).Append(')')
                  .Append(" | chcialy ").Append(F(wantSum, "0")).Append(" szt. | na polki dzis ").Append(pieces).Append(" szt. zboza (").Append(F(rations, "0")).Append(" racji, racji na sztuke srednio ")
                  .Append(rppN > 0 ? F(rppSum / rppN, "0.0") : "-").Append("; sigma ").Append(sigma).Append(", kg sztuki ").Append(F(kg, "0.##")).Append(") z ").Append(fedVills).Append(" wsi do ").Append(fedTowns).Append(" miast")
                  .Append(" | zloto miasto->wies ").Append(gold).Append(" zl (cena srednio ").Append(pieces > 0 ? F(gold / (double)pieces, "0.#") : "-").Append(" zl/szt. bez pulapu, sztuka po sztuce w dol krzywej polki; pierwsza sztuka srednio ")
                  .Append(fedTowns > 0 ? F(firstSum / fedTowns, "0.#") : "-").Append(" zl, ostatnia srednio ").Append(fedTowns > 0 ? F(lastSum / fedTowns, "0.#") : "-").Append(" zl; wartosc ").Append(grain != null ? grain.Value : 0).Append(" zl)")
                  .Append(" | wsie: na rezerwie ").Append(atFloor).Append(", bez rachunku KS (kcal racji) ").Append(noKcal).Append(", bez migawki doby/glod/bez spichlerza ").Append(notToday).Append(", ponizej sztuki dzis ").Append(tiny)
                  .Append(" | polka po dostawie (dni popytu, z KS-H): mediana ").Append(after.Count > 0 ? F(after[(after.Count - 1) / 2], "0.00") : "-")
                  .Append(" | kontrola: racje zdjete ").Append(F(rations, "0.##")).Append(" = ubytek stodol wsi ").Append(F(barnDrop, "0.##"))
                  .Append("; polki +").Append(shelfIn).Append(" = sztuki ").Append(pieces)
                  .Append("; zloto: kasy miast -").Append(townOut).Append(", kiesy wsi +").Append(vilIn).Append(", zaplata ").Append(gold)
                  .Append("; od wczytania ").Append(F(_w322SessPieces, "0")).Append(" szt., ").Append(F(_w322SessRations, "0")).Append(" racji, ").Append(_w322SessGold).Append(" zl")
                  .Append(" | potkniecia ").Append(_w322Stum).Append(" (razem ").Append(_w322StumAll).Append(')')
                  .Append(" | czas ").Append(((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency).ToString("0.0", Inv)).Append(" ms.");
                Log.Info(sb.ToString());
            }
            _w322Stum = 0;
        }
    }
}
