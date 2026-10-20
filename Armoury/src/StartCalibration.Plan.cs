using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// KS - PLAN (projekt rozdz. 11.1): zrzut na watku glownym (ludzie okregow z kontami uchodzcow, stany ZC, koszyki w kg z KS-K, ziemia z pliku, udzialy osad
    /// ksiegi wiosek, zuzycie 1:1 z ksiegi towarow - srednia 28 dob, las wsi i lodzie NavalDLC jako podaz spoza tabeli), rachunek na watku roboczym (KsCore.Solve -
    /// same tablice, bez obiektow gry; cieply start z poprzedniego Planu), wynik odbierany w nastepnej dobie: linie "Kalibracja startu (KS): plan doba N" i "KS: ...",
    /// plik ks-plan.csv (okreg x towar). Gra bez zmian.
    /// </summary>
    internal static partial class StartCalibration
    {
        private const string CsvName = "ks-plan.csv";
        private const string CsvHeader = "dzien;okreg_id;okreg;rodzaj;rynek;kraina;typ;ludzie;rece_pole;towar;produkcja_kg_d;zjedzone_w_okregu_kg_d;na_targ_kg_d;na_targ_szt_d;glowne_zajecie;udzial_glownego;blokow";
        // towary planu potrzebne do popytu (1:1, wsady, zbytki) poza towarami KS
        private static readonly string[] PlanItemIds = { "bread", "pie", "beer", "wine", "oil", "mead", "linen", "felt", "leather", "tools", "pottery", "planks", "velvet", "jewelry", "meat", "hides" };

        // recenzja 17.10 (wazne): Plan do wlaczenia i Plany w oknie D..D+28 licza podaz targu 1.03 x popyt targu (polki startu odbudowane z prawdziwej nadwyzki rak -
        // praca domowa oddaje czesc rak na targ; po D+28 Plan bez marginesu, praca domowa wraca w nastepnym Planie). Test portu: 1.04 dawal zboze Westeros 1.06 na targu
        // (poza 105%), 1.03 - 1.04-1.05; Essos bez pracy domowej do oddania - bez marginesu (linia: "brak rak do oddania")
        private const double ShelfMargin = 1.03; private const int ShelfMarginDays = 28;
        private static KsResult _plan;                 // ostatni Plan (wynik) i jego wejscie
        private static KsInput _planIn;
        private static int _planDay = -1;
        private static Thread _worker;
        private static volatile KsResult _wOut;
        private static KsInput _wIn;                    // wejscie w toku (watek glowny)
        private static int _wDay;
        private static volatile bool _wBusy;
        private static int _gen;
        private static string _waitNote;
        // srednia 28 dob liczb ksiegi towarow (zuzycie 1:1, produkcja wsi, las wsi, lodzie)
        private static readonly Dictionary<string, double[]> _ema = new Dictionary<string, double[]>(StringComparer.Ordinal);
        private static int _emaDays, _emaLastDay = -1;
        private static int _wEma, _planEma;              // doby ksiegi towarow w sredniej Planu w toku i ostatniego gotowego (recenzja 16.10: wlaczenie tylko z dojrzala ksiega)
        private static readonly Dictionary<string, double[]> _ledgerTmp = new Dictionary<string, double[]>(StringComparer.Ordinal);

        private static void PlanReset()
        {
            Interlocked.Increment(ref _gen);
            _plan = null; _planIn = null; _planDay = -1; _wOut = null; _wIn = null; _wBusy = false; _waitNote = null;   // _worker zostaje - PlanStart czeka, az stary watek sie skonczy
            _ema.Clear(); _emaDays = 0; _emaLastDay = -1; _wEma = _planEma = 0;
            M96LenReset();                                             // m96-len: srednia zakupow plotna przez wsie od zera
        }

        private static void PlanCancel() { Interlocked.Increment(ref _gen); }

        /// <summary>Srednia ksiegi towarow (zwykla przez 28 dob, potem wykladnicza 1/28) - raz na dobe, liczby doby wczorajszej.</summary>
        private static void LedgerEma()
        {
            int d = GoodsLedger.KsTake(_ledgerTmp);
            if (d < 0 || d == _emaLastDay) return;
            _emaLastDay = d;
            _emaDays++;
            double w = 1.0 / Math.Min(28, _emaDays);
            foreach (var kv in _ema) { if (!_ledgerTmp.ContainsKey(kv.Key)) for (int i = 0; i < kv.Value.Length; i++) kv.Value[i] *= 1.0 - w; }
            foreach (var kv in _ledgerTmp)
            {
                double[] a;
                if (!_ema.TryGetValue(kv.Key, out a)) { a = new double[GoodsLedger.KsN]; _ema[kv.Key] = a; for (int i = 0; i < a.Length; i++) a[i] = kv.Value[i]; continue; }
                for (int i = 0; i < a.Length; i++) a[i] += (kv.Value[i] - a[i]) * w;
            }
        }

        private static double Ema(string id, int k) { double[] a; return _ema.TryGetValue(id, out a) && a != null && k < a.Length ? a[k] : 0.0; }

        private static ItemObject ItemOf(string id)
        {
            try { var om = MBObjectManager.Instance; return om != null ? om.GetObject<ItemObject>(id) : null; } catch { return null; }
        }

        private static bool IsFishType(string t) { return t == "fisherman" || t == "whaler" || t == "walrus_hunter"; }
        private static readonly HashSet<string> WestLands = new HashSet<string>(StringComparer.Ordinal)
        { "battania", "freefolk", "nightswatch", "skagosi", "sturgia", "river", "vale", "vlandia", "crownlands", "stormlands", "dragonstone", "reach", "aserai" };

        // ============================================================ zrzut (watek glowny)
        private sealed class SnapInfo { public int Vil, NoLand, NoRec, Towns, TF, SideIn, SideOut, Dup, Budgets; public double Woodlot, Boats; public bool Layout; public int Zc; }

        private static KsInput BuildInput(int day, out string note, out SnapInfo info)
        {
            note = null; info = new SnapInfo();
            var recs = new List<DailyLife.KsRec>(900);
            int zcDay;
            if (!DailyLife.KsSnapshot(recs, day + 1, out zcDay)) { note = "brak stanow ZC z tej doby (Daily Life Measure wylaczony albo przed kalibracja ksiegi ludzi)"; return null; }
            info.Zc = zcDay;
            var s = Settings.Current;
            var inp = new KsInput { Sigma = DailyLife.KsSigma, Inert = Math.Max(1.0, Math.Min(2.0, s != null ? s.StartCalibrationMapInertia : 1.25f)), Beta = 10.0, Ksk = true };
            // recenzja 16.10 (3): bez poprawek ZC1 (SilkToWeavers wylaczony; srebro - bez klucza) mieszczanie jedza jedwab i rude srebra wprost - popyt Planu z tego zuzycia
            inp.SilkTownDirect = s == null || !s.SilkToWeavers;
            inp.SilverTownDirect = true;
            // krok 8 (pieniadze, poprawka po autotescie 16.10): budzet wsi z utargu wozow (udzial kiesy wsi x cena wozu / cena polki x (1 - zapas na oplaty)),
            // koszyk z wlasnego pola (udzialy stanow ZC), budzety klas miast z zarobku netto (28 dob)
            inp.Money = true; inp.Keep = _keep;
            inp.ClothDemand = SwClothDemand;                          // KS r4 P8 (r4-ceny): plotno, sukno, skora z odziezy wojska i warsztatow zbrojnych w popycie Planu
            SyncColdLand(); inp.SelfFeed = SelfFeedMode(); inp.SelfFeedOwn = SelfFeedOwnOnly();   // r4-polnoc: zimna ziemia (tabela swiata) i najpierw wyzywic siebie (prog Planu)
            FeedSwiatInput(inp);                                      // feed-swiat: jedzenie podstawowe, krok progu, rynek musi sie oczyscic (cieply start z poprzedniego Planu)
            double va = VilAlphaNow(); inp.VilAlpha[0] = inp.VilAlpha[1] = va; _alphaDsN = 0;   // KS r4: licznik wsi z alfa z praw DS (VilAlphaFor)
            // towary: waga, wartosc, zuzycie 1:1 (srednia ksiegi towarow)
            var ids = new List<string>(KsTab.Goods); ids.AddRange(PlanItemIds); ids.Add("WhaleMeat");
            foreach (var id in ids)
            {
                if (inp.Items.ContainsKey(id)) continue;
                var it = ItemOf(id);
                if (it == null) continue;
                inp.Items[id] = new KsItem
                {
                    Kg = it.Weight > 0.01f ? it.Weight : 10.0, Val = Math.Max(0, it.Value), Army = Ema(id, GoodsLedger.KsArmy), Cast = Ema(id, GoodsLedger.KsCastle),
                    Court = Ema(id, GoodsLedger.KsCourt), Bud = Ema(id, GoodsLedger.KsBuild), Ws = Ema(id, GoodsLedger.KsShop), Cons = Ema(id, GoodsLedger.KsCons), Town = Ema(id, GoodsLedger.KsTown),
                    Cloth = Ema(id, GoodsLedger.KsCloth)                  // KS r4 P8: odziez wojska, warsztaty zbrojne, naprawy kowali (popyt 1:1 przy kluczu Ks Cloth Demand)
                };
            }
            M96LenApply(inp, day);                                    // m96-len: plotno kupowane przez wsie w miastach (E5/B1) ponad koszyk zaplacony - popyt 1:1 plotna
            uint lay;
            info.Layout = LayoutMatches(out lay);
            bool fields = s == null || s.StartCalibrationTownFields;
            var side = new Dictionary<string, double>(StringComparer.Ordinal);
            var woodKg = 0.0; KsItem wi; if (inp.Items.TryGetValue("hardwood", out wi)) woodKg = wi.Kg * inp.Sigma;
            var seenV = new HashSet<Village>();   // ROT-Map: dwie osady na jednym Village (Kurbok/Guntak, Thupak/Volug) - okreg raz, pierwszy w Settlement.All (jak ksiega wiosek)
            var tst = new List<Settlement>(120); var distMk = new Dictionary<string, Settlement>(StringComparer.Ordinal);   // KS-R3 krok 5: miasta wejscia i miasto targowe okregu
            foreach (var r in recs)
            {
                try
                {
                    if (r.Castle) continue;
                    var bask = DailyLife.KsBasket(r.Cul, true);
                    bool oil = DailyLife.OilProf(r.ProfId), north = DailyLife.NorthFur(r.ProfId);
                    string land = r.Cul ?? "?";
                    if (r.Town)
                    {
                        var t = new KsSettlement { Id = r.St.StringId, Land = land, People = r.L };
                        LandDist tl;
                        bool hasLand = _landTown.TryGetValue(r.St.StringId ?? "", out tl);
                        t.Market = hasLand ? tl.Market : (WestLands.Contains(land) ? 0 : 1);
                        KsCore.NeedsOf(r.N, bask, oil, north, true, true, r.L, t.Need0);
                        var bud = TownBudget(r.St);
                        if (bud != null) { t.ClassNeed = ClassNeeds(r.N, bask, oil, north); t.Budget = bud; info.Budgets++; }
                        inp.Towns.Add(t); tst.Add(r.St);
                        info.Towns++;
                        if (fields && hasLand)
                        {
                            double wf = KsCore.TownFieldHands(r.N);
                            if (wf > 0)
                            {
                                var d = new KsDistrict { Id = r.St.StringId + "#pola", Type = "town_fields", Land = land, Market = tl.Market, People = 0, Wf = wf, Area = tl.Area, TownFields = true };
                                for (int k = 0; k < 20; k++)
                                    d.Blocks.Add(new KsBlock { Uid = tl.Main[k].Uid, Share = 0.05, Area = tl.Area * 0.05, Main = true, Block0 = false, Start = "garden", O = tl.Main[k].O });
                                inp.Districts.Add(d); distMk[d.Id] = r.St;
                                info.TF++;
                            }
                        }
                        continue;
                    }
                    var v = r.St.Village;
                    LandDist ld;
                    if (v == null || r.St.StringId == null || !_landVil.TryGetValue(r.St.StringId, out ld)) { info.NoLand++; continue; }
                    if (!seenV.Add(v)) { info.Dup++; continue; }
                    string type = v.VillageType != null ? v.VillageType.StringId : "wheat_farm";
                    double away = 0; try { away = PeopleBook.RefugeesOf(v); } catch { away = 0; }
                    double all = Math.Max(0.0, r.L) + Math.Max(0.0, away);
                    var ppl = new double[6];
                    for (int e = 0; e < 6; e++) ppl[e] = all * r.Share[e];
                    // sw-uchodzcy (RaidRefugeesToTowns): uchodzcy w miescie pobytu jedza w miescie (biedota miasta w KsSnapshot) - ludzie i potrzeba okregu bez nich; rece okregu
                    // (VillageHands z ppl x mnoznik rak spustoszenia) bez zmian. Wylaczone: awayE = away (co do liczby jak dotad)
                    double awayE = away; try { awayE = RaidRefugees.AwayInRegion(v); } catch { awayE = away; }
                    double allE = Math.Max(0.0, r.L) + Math.Max(0.0, awayE);
                    var pplE = ppl;
                    if (allE != all) { pplE = new double[6]; for (int e = 0; e < 6; e++) pplE[e] = allE * r.Share[e]; }
                    var kd = new KsDistrict { Id = r.St.StringId, Type = type, Land = land, Market = ld.Market, People = allE, Area = ld.Area };
                    // recenzja 16.10 (drobne): zloza, ktore RockGold w grze tnie do 0 (Skala: srebro i zloto, Zachod: zloto) - L = 0 w Planie
                    try { if (RockGold.Blocks(v, "silver")) kd.NoReg |= 1L << KsTab.CSilver; if (RockGold.Blocks(v, "gold_ore")) kd.NoReg |= 1L << KsTab.CGoldM; } catch { }
                    KsCore.NeedsOf(pplE, bask, oil, north, true, false, allE, kd.Need0);
                    kd.PplHome = new double[6]; for (int e = 0; e < 6; e++) kd.PplHome[e] = Math.Max(0.0, r.L) * r.Share[e];
                    kd.Bask = bask; kd.Oil = oil;
                    double hands = 1.0;
                    try { hands = Devastation.YieldFactor(v) * FreeHands.YieldFactor(v); } catch { hands = 1.0; }
                    kd.Wf = KsCore.VillageHands(ppl) * hands;
                    kd.Alpha = VilAlphaFor(v);                            // recenzja 17.10: alfa tej wsi z pomiaru (dekret pana); -1 - alfa rynku
                    BuildBlocks(kd, ld, v, type, info.Layout, side, info);
                    inp.Districts.Add(kd);
                    try { distMk[kd.Id] = TradeTownOf(v); } catch { }
                    info.Vil++;
                    // las wsi - podaz drewna spoza tabeli (wies bez drwali)
                    double wood = VillageWoodlot.KsPiecesPerDay(v) * woodKg;
                    if (wood > 0) { AddExo(inp, land, KsTab.GWood, wood); info.Woodlot += wood; }
                }
                catch (Exception e) { info.NoRec++; Stumble("BuildInput(osada)", e); }
            }
            // lodzie rybackie NavalDLC - ryby spoza tabeli, wedlug ludzi krainy
            KsItem fi;
            double boats = Ema("fish", GoodsLedger.KsFishBoats) * (inp.Items.TryGetValue("fish", out fi) ? fi.Kg : 10.0) * inp.Sigma;
            if (boats > 0)
            {
                var lp = new Dictionary<string, double>(StringComparer.Ordinal); double tot = 0;
                foreach (var d in inp.Districts) { double v0; lp.TryGetValue(d.Land, out v0); lp[d.Land] = v0 + d.People; tot += d.People; }
                foreach (var t in inp.Towns) { double v0; lp.TryGetValue(t.Land, out v0); lp[t.Land] = v0 + t.People; tot += t.People; }
                if (tot > 0) foreach (var kv in lp) AddExo(inp, kv.Key, KsTab.GFish, boats * kv.Value / tot);
                info.Boats = boats;
            }
            if (_plan != null && _plan.Error == null && _plan.P != null)
            {
                inp.WarmPrices = (double[])_plan.P.Clone();
                inp.WarmRent = new Dictionary<string, double>(StringComparer.Ordinal);
                for (int b = 0; b < _plan.NB; b++) if (_plan.Blocks[b].Uid != null) inp.WarmRent[_plan.Blocks[b].Uid] = _plan.Rent[b];
                inp.RoundsFirst = _plan.OpenAbsM[0] + _plan.OpenAbsM[1] > 0 ? 6000 : 3000;   // recenzja 16.10 (drobne): poprzedni Plan niezbiezny - wiecej rund rownowagi; n22f-plany: stara miara (bez pasma zapasu polek - jak dotad)
                if (_plan.DistHome != null && _plan.DistHome.Count > 0) inp.WarmHome = new Dictionary<string, double>(_plan.DistHome, StringComparer.Ordinal);   // krok 8: cieply start pracy domowej
            }
            if (inp.Districts.Count == 0) { note = "zaden okreg z ziemia z pliku (plik ziemi nie pasuje do wsi gry)"; return null; }
            // recenzja 17.10: krok 8 z pomiaru (alfa wsi i budzety miast) - Plan bez pomiaru nie zastepuje pracy domowej i czesci zaplaconej zapisanych w okregach;
            // margines popytu targu (zapas na polki): Plan do wlaczenia i Plany do D+28 - praca domowa oddaje rece na targ, az podaz targu = 1.03 x popyt targu
            inp.MoneyMeasured = MoneyMeasuredNow(info.Towns, info.Budgets);
            inp.MarketMargin = _state != StateApplied || (DDay >= 0 && day < DDay + ShelfMarginDays) ? ShelfMargin : 1.0;
            try { R3PlanInputs(inp, day, tst, distMk); } catch (Exception e) { Stumble("R3PlanInputs", e); }   // KS-R3: krok 5, P2, P3, P4
            inp.CheapBasket = SwCheapBasket;   // r4-koszyk: drabina tanszego koszyka klas miast (klucz Ks Cheap Basket)
            EnsureItems();                                        // C2: tabele towarow gotowe przed plikiem ks-plan.csv na watku roboczym
            return inp;
        }

        // ============================================================ linia rynkow (recenzja 16.10: odbior KS-M na rynek - Westeros, Essos)
        private static readonly Dictionary<string, long[]> _mkTmp = new Dictionary<string, long[]>(StringComparer.Ordinal);
        private static int _mkLineDay = -1;
        private static readonly string[] MarketExtra = { "WhaleMeat", "bread", "pie", "beer", "wine", "oil", "mead", "pottery", "tools", "linen", "felt", "leather" };

        /// <summary>Linia "KS: rynki dzien N": produkcja / zuzycie / zapas miast doby z ksiegi towarow wedlug rynku osady (0 Westeros, 1 Essos; bez rynku - partie poza
        /// osada i zrodla bez ramki) dla towarow KS i przetworow; null - brak liczb (ksiega wylaczona albo podzial od jutra).</summary>
        private static string MarketLine()
        {
            int d = GoodsLedger.KsMarketTake(_mkTmp);
            if (d < 0 || d == _mkLineDay || _mkTmp.Count == 0) return null;
            _mkLineDay = d;
            var sb = new StringBuilder(5000);
            // KS-R3: czwarta liczba - jedzenie w wozach KS-H w drodze do miast rynku (NKS-6: zapas miast + w drodze)
            var tr = new Dictionary<string, long[]>(StringComparer.Ordinal);
            try { KsFoodCarts.ForEachInTransit((it, n, mk) => { if (it == null || mk < 0 || mk > 1) return; long[] a0; if (!tr.TryGetValue(it.StringId, out a0)) { a0 = new long[2]; tr[it.StringId] = a0; } a0[mk] += n; }); } catch { }
            sb.Append(PK).Append("rynki dzien ").Append(d).Append(" (ksiega towarow wedlug rynku osady; produkcja/zuzycie/zapas miast/w drodze KS-H w sztukach)");
            var ids = new List<string>(ItemIds); foreach (var x in MarketExtra) if (!ids.Contains(x)) ids.Add(x);
            for (int m = 0; m < 3; m++)
            {
                sb.Append(m == 0 ? " | Westeros:" : (m == 1 ? " | Essos:" : " | bez rynku (produkcja/zuzycie):"));
                bool first = true;
                foreach (var id in ids)
                {
                    long[] a;
                    if (!_mkTmp.TryGetValue(id, out a) || a == null || a.Length < 8) continue;
                    long p = a[m], c = a[3 + m];
                    if (p == 0 && c == 0 && (m == 2 || a[6 + m] == 0)) continue;
                    sb.Append(first ? " " : ", ").Append(id).Append(' ').Append(p).Append('/').Append(c);
                    if (m < 2) { long[] t0; sb.Append('/').Append(a[6 + m]).Append('/').Append(tr.TryGetValue(id, out t0) ? t0[m] : 0); }
                    first = false;
                }
                if (first) sb.Append(" -");
            }
            // KS r4 P10 (r4-ceny, pomiar NKS-5u): z czego jest "bez rynku" - wies je swoje z magazynu (G3, wedlug rynku wsi) i stracone w drodze KS-H; reszta - nieprzypisane
            {
                bool f4 = true;
                foreach (var id in ids)
                {
                    long[] a;
                    if (!_mkTmp.TryGetValue(id, out a) || a == null || a.Length < 11 || (a[8] == 0 && a[9] == 0 && a[10] == 0)) continue;
                    if (f4) sb.Append(" | bez rynku z tego (wies je swoje Westeros/Essos/stracone w drodze KS-H):");
                    sb.Append(f4 ? " " : ", ").Append(id).Append(' ').Append(a[8]).Append('/').Append(a[9]).Append('/').Append(a[10]);
                    f4 = false;
                }
            }
            sb.Append('.');
            return sb.ToString();
        }

        private static void AddExo(KsInput inp, string land, int g, double kg)
        {
            double[] a;
            if (!inp.ExoLand.TryGetValue(land, out a)) { a = new double[KsTab.NG]; inp.ExoLand[land] = a; }
            a[g] += kg;
        }

        /// <summary>Bloki okregu wsi: 20 blokow wsi gry (udzial wsi gry / 20, blok 0 - punkt wsi; typ rybacki przy morzu - brzeg) i wioski poboczne z udzialem osad
        /// z ksiegi wiosek (gdy uklad zgodny z plikiem ziemi); udzialy do 1, ziemia bloku = ziemia okregu x udzial.</summary>
        private static void BuildBlocks(KsDistrict kd, LandDist ld, Village v, string type, bool layout, Dictionary<string, double> side, SnapInfo info)
        {
            double main = 1.0;
            bool hs = false;
            if (layout) { try { hs = VillageBook.KsShares(v, side, out main); } catch { hs = false; } }
            if (!hs) { main = 1.0; side.Clear(); }
            for (int k = 0; k < 20; k++)
            {
                var row = ld.Main[k];
                var o = row.O;
                if (k == 0 && IsFishType(type) && o.DSea <= 12 && o.Kind != "coast") { o = o.Clone(); o.Kind = "coast"; }
                kd.Blocks.Add(new KsBlock { Uid = row.Uid, Share = main / 20.0, Main = true, Block0 = k == 0, Start = type, O = o });
            }
            foreach (var row in ld.Side)
            {
                double sh;
                if (!side.TryGetValue(row.Uid, out sh) || !(sh > 0)) { if (info != null) info.SideOut++; continue; }
                kd.Blocks.Add(new KsBlock { Uid = row.Uid, Share = sh, Main = false, Start = row.Start, O = row.O });
                if (info != null) info.SideIn++;
            }
            double tot = 0; foreach (var b in kd.Blocks) tot += b.Share;
            if (!(tot > 0)) tot = 1;
            foreach (var b in kd.Blocks) { b.Share /= tot; b.Area = ld.Area * b.Share; }
        }

        // ============================================================ watek roboczy
        private static void PlanStart(int day)
        {
            if (_wBusy || (_worker != null && _worker.IsAlive)) return;   // stary watek (poprzednia sesja) jeszcze konczy - nowy Plan w kolejnej dobie
            string note; SnapInfo info;
            var sw = Stopwatch.StartNew();
            KsInput inp = null;
            try { inp = BuildInput(day, out note, out info); }
            catch (Exception e) { Stumble("BuildInput", e); return; }
            double snapMs = sw.Elapsed.TotalMilliseconds;
            if (inp == null)
            {
                if (_waitNote != note) { _waitNote = note; Log.Info(PK + "Plan czeka - " + note + "."); }
                return;
            }
            _waitNote = null;
            _lastPlanDay = day;
            int gen = Volatile.Read(ref _gen);
            _wIn = inp; _wDay = day; _wBusy = true; _wEma = _emaDays;
            // KS-R3 C2: pliki ks-plan.csv i ks-budzety.csv formatowane na watku roboczym (nazwy osad i liczby pomiaru - zrzut tutaj)
            bool csv = _plans + 1 == 1 || (_plans + 1) % 4 == 0;
            Dictionary<string, string> names = null; List<object[]> budRows = null;
            if (csv)
            {
                names = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var d in inp.Districts)
                {
                    try { var st = Settlement.Find(d.TownFields ? d.Id.Replace("#pola", "") : d.Id); if (st != null && st.Name != null) names[d.Id] = st.Name.ToString().Replace(';', ','); } catch { }
                }
                try { budRows = BudgetsRows(day, inp); } catch (Exception e) { Stumble("BudgetsRows", e); }
            }
            int csvDay = day;
            bool fastNt = SwCostFast;                                 // ks-koszt T1: sumy "na targ" odbioru na watku roboczym (klucz Ks Cost Fast)
            _snapNote = "zrzut " + snapMs.ToString("0", Inv) + " ms (wsie " + info.Vil + ", bez ziemi " + info.NoLand + ", druga osada tej samej wsi gry " + info.Dup + ", potkniecia " + info.NoRec + ", miasta " + info.Towns + ", pola mieszczan "
                        + info.TF + ", wioski poboczne w blokach " + info.SideIn + ", poza (osady 0 albo uklad) " + info.SideOut + ", uklad wiosek " + (info.Layout ? "zgodny" : "NIEZGODNY")
                        + ", stany ZC z doby " + info.Zc + ", ksiega towarow " + _emaDays + " dob, las wsi " + (info.Woodlot / 1000).ToString("0", Inv) + " t/d, lodzie " + (info.Boats / 1000).ToString("0.0", Inv) + " t/d"
                        + (inp.WarmPrices != null ? ", cieply start" : ", start zimny") + "; krok 8: alfa wsi rynku " + inp.VilAlpha[0].ToString("0.000", Inv) + " (wsi z alfa z praw DS " + _alphaDsN + "; " + MoneyNote() + "), budzety klas miast " + info.Budgets + "/" + info.Towns
                        + ", z pomiaru " + (inp.MoneyMeasured ? "TAK" : "NIE") + ", margines popytu targu x" + inp.MarketMargin.ToString("0.00", Inv)
                        + (inp.WarmHome != null ? ", praca domowa z poprzedniego Planu" : "") + ")";
            try
            {
                var t = new Thread(() =>
                {
                    KsResult r = null;
                    try { r = KsCore.Solve(inp, () => gen != Volatile.Read(ref _gen)); }
                    catch (Exception e) { r = new KsResult { Error = e.GetType().Name + ": " + e.Message }; }
                    if (fastNt && r != null && r.Error == null) { try { PlanMarketSums(r, inp, () => gen != Volatile.Read(ref _gen)); } catch { r.NtMk = null; } }   // ks-koszt T1 (blad - odbior liczy na watku glownym)
                    if (csv && r != null && r.Error == null)
                    {
                        try { int rows; r.CsvPlan = PlanCsvText(csvDay, r, inp, names, out rows); r.CsvPlanRows = rows; } catch (Exception e) { r.CsvPlan = null; r.CsvPlanRows = -1; r.CorrLine = (r.CorrLine ?? "") + " [ks-plan.csv: " + e.GetType().Name + "]"; }
                        if (budRows != null) { var rowsB = BudgetsText(csvDay, budRows); var sb = new StringBuilder(rowsB.Count * 64); foreach (var x in rowsB) sb.Append(x).Append(Environment.NewLine); r.CsvBudgets = sb.ToString(); r.CsvBudgetRows = rowsB.Count; }
                    }
                    try { if (gen == Volatile.Read(ref _gen)) _wOut = r; } catch { }
                    _wBusy = false;
                });
                t.IsBackground = true;
                t.Priority = ThreadPriority.BelowNormal;
                t.Name = "Armoury KS plan";
                _worker = t;
                t.Start();
            }
            catch (Exception e) { _wBusy = false; Stumble("PlanStart", e); }
        }
        private static string _snapNote = "";

        /// <summary>Wynik Planu z watku roboczego (watek glowny): linie i plik. Pierwszy Plan zapisu - doba w zapisie.</summary>
        private static void PlanCollect(int day)
        {
            var r = _wOut;
            if (r == null) return;
            _wOut = null;
            var inp = _wIn; _wIn = null;
            if (inp == null) return;
            if (r.Error == null) { _plan = r; _planIn = inp; _planDay = _wDay; _plans++; _planEma = _wEma; if (_firstPlanDay < 0) _firstPlanDay = _wDay; TownPaidFromPlan(r); }   // Plan z bledem nie zastepuje dobrego (cieply start, wlaczenie)
            MarketFactorCommit(r);                                 // recenzja runda 3: czynnik sterowania rynku tylko z Planu bez bledu
            // recenzja 16.10 (drobne): stan zastosowany - lepszy stol (NeedMult) z nowego Planu co 7 dob, nie zamrozony z dnia wlaczenia
            long tc = T0Now();                                     // ks-koszt T0: pod-czasy odbioru Planu
            if (r.Error == null && _state == StateApplied) { try { RefreshNeedMult(r, inp); } catch (Exception e) { Stumble("RefreshNeedMult", e); } }
            tc = T0Lap(T0CNeed, tc);
            try { PlanLines(day, r, inp); } catch (Exception e) { Stumble("PlanLines", e); }
            tc = T0Lap(T0CLines, tc);
            // ks-plan.csv: pierwszy Plan zapisu, potem co 4. (ok. 1.5 MB na Plan - 120 dob = 5 planow w pliku); KS-R3 C2: tekst z watku roboczego
            if (r.Error == null && r.CsvPlan != null)
            {
                try { string path = Log.Csv(CsvName, CsvHeader, r.CsvPlan); Log.Info(PK + CsvName + " " + (path != null ? r.CsvPlanRows + " wierszy (doba " + _wDay + ", tekst z watku roboczego)" : "NIE ZAPISANY (log wylaczony albo brak katalogu sesji)") + "."); }
                catch (Exception e) { Stumble("PlanCsv", e); }
            }
            // recenzja 17.10 (wazne): zmierzone budzety klas miast i alfa wsi tego Planu - wejscie testu portu (harness kmoney) zamiast budzetow z umowy
            if (r.Error == null && r.CsvBudgets != null && LogGate.Lines)
            {
                try { string path = Log.Csv(BudgetCsvName, BudgetCsvHeader, r.CsvBudgets); Log.Info(PK + BudgetCsvName + " " + (path != null ? r.CsvBudgetRows + " wierszy (doba " + _wDay + "; " + MoneyNote() + ")" : "NIE ZAPISANY (log wylaczony albo brak katalogu sesji)") + "."); }
                catch (Exception e) { Stumble("BudgetsCsv", e); }
            }
            T0Lap(T0CCsv, tc);
            // KS-R3: gradient krain z Planu (cieply start, zapis) - tylko Plan bez bledu z krokiem 5
            if (r.Error == null && r.Grad != null) _grad = r.Grad;
            T4Bump();                                             // ks-koszt T4: nowy Plan i gradient - pamiec progow od zera
        }

        private static string F2(double x) { return double.IsNaN(x) || double.IsInfinity(x) ? "-" : x.ToString("0.00", Inv); }
        private static string F0(double x) { return double.IsNaN(x) || double.IsInfinity(x) ? "-" : Math.Round(x).ToString("0", Inv); }

        // towary, ktorych stosunek nie musi miescic sie w pasmie (projekt rozdz. 4.1/14: uboczne gospodarstw i rzeczy bez osobnych rak)
        private static readonly HashSet<string> BandExceptions = new HashSet<string>(StringComparer.Ordinal) { "carrot", "apple", "fur", "honey", "spice", "whale_oil", "walrus_tusk", "Egg", "hides" };

        private static void PlanLines(int day, KsResult r, KsInput inp)
        {
            if (r.Error != null)
            {
                Log.Info(P + "plan doba " + _wDay + " | BLAD rachunku (" + r.Error + ") po " + r.Rounds + " rundach, " + (r.Ms / 1000).ToString("0.0", Inv) + " s | " + _snapNote + " | gra bez zmian.");
                return;
            }
            int nv = 0, nt = 0; foreach (var d in inp.Districts) if (d.TownFields) nt++; else nv++;
            var sb = new StringBuilder(4000);
            sb.Append(P).Append("plan doba ").Append(_wDay).Append(" | plan nr ").Append(_plans).Append(" tego zapisu (od doby ").Append(_firstPlanDay).Append(")")
              .Append(", ksiega towarow ").Append(_wEma).Append(" dob w sredniej (wlaczenie wymaga >= ").Append(MinLedgerDays).Append(_wEma >= MinLedgerDays ? " - Plan dojrzaly)" : " - Plan NIEDOJRZALY: popyt 1:1 z za krotkiej ksiegi)")
              .Append(" | rund ").Append(r.Rounds).Append(" (pierwsza ").Append(r.RoundsFirst).Append(", lepszy stol R8 ").Append(r.R8Rounds).Append(", R8b ").Append(r.R8bRounds).Append(")")
              .Append(", czas ").Append((r.Ms / 1000).ToString("0.0", Inv)).Append(" s (watek roboczy)")
              .Append(" | okregi ").Append(r.NDist).Append(" (wsie ").Append(nv).Append(", pola mieszczan ").Append(nt).Append("), bloki ").Append(r.NB).Append(", pary blok-zajecie ").Append(r.NP)
              .Append(" | sigma ").Append(inp.Sigma.ToString("0", Inv)).Append(", premia mapy ").Append(inp.Inert.ToString("0.00", Inv));
            for (int m = 0; m < 2; m++)
            {
                double dk = 0; var out_ = new List<string>();
                for (int g = 0; g < KsTab.NG; g++)
                {
                    double D = r.D[m * KsTab.NG + g], S = r.S[m * KsTab.NG + g];
                    dk += D;
                    if (D < 1000) continue;
                    double q = MarketQ(r, m, g);
                    if (q < 0.95 || q > 1.05) out_.Add(KsTab.Goods[g] + " " + F2(q) + (BandExceptions.Contains(KsTab.Goods[g]) ? "*" : ""));
                }
                sb.Append(" | ").Append(r.Markets[m]).Append(": popyt ").Append(F0(dk / 1000)).Append(" t/d, poza pasmem 0.95-1.05 ").Append(r.Eaten != null ? "NA TARG ((podaz - zjedzone w okregach) / (popyt - zjedzone))" : "(podaz / popyt)")
                  .Append(": ").Append(out_.Count > 0 ? string.Join(", ", out_.ToArray()) : "brak");
            }
            sb.Append(" (* uboczne/domowe - wyjatki projektu)");
            sb.Append(" | lepszy stol R8b: Westeros x").Append(r.Table[0].ToString("0.000", Inv)).Append(", Essos x").Append(r.Table[1].ToString("0.000", Inv));
            sb.Append(" | ziemia pelna w ").Append((100 * r.LandBind).ToString("0", Inv)).Append("% blokow, renta srednia ").Append(r.RentMean.ToString("0.0000", Inv)).Append(" d/ha/d (max ").Append(r.RentMax.ToString("0.000", Inv)).Append(")");
            sb.Append(" | rece mln: dom ").Append((r.HomeLabour / 1e6).ToString("0.00", Inv)).Append(", zboze wlasne ").Append((r.OwnGrainLabour / 1e6).ToString("0.00", Inv))
              .Append(", pola mieszczan ").Append((r.TownFieldHands / 1e6).ToString("0.00", Inv));
            sb.Append(" | niezbiezne: ").Append(r.Open.Count > 0 ? string.Join(", ", r.Open.ToArray()) : "brak");
            sb.Append(" | na granicy ceny 0.05/5: ").Append(r.Bounds.Count > 0 ? string.Join(", ", r.Bounds.ToArray()) : "brak");
            sb.Append(" | ").Append(_snapNote).Append(" | gra bez zmian (Plan).");
            Log.Info(sb.ToString());
            try { MoneyLines(r); } catch (Exception e) { Stumble("MoneyLines", e); }
            try { FeedLineLog(r); } catch (Exception e) { Stumble("FeedLine", e); }   // r4-polnoc: wyzywienie krain (NKS-30)
            // KS-R3: linie kroku 5 (korytarze i krainy - tekst z watku roboczego) i sterowania rynku (P4 - liczone przy zrzucie)
            if (r.CorrLine != null) Log.Info(r.CorrLine.Replace("{DOBA}", _wDay.ToString(Inv)));
            if (_ctlLine != null) Log.Info(_ctlLine);
            if (_gapLine != null) Log.Info(_gapLine);              // KS r4 (r4-ceny): luka zapasu P4 i popyt na zapas polek w rownowadze
            for (int m = 0; m < 2; m++)
            {
                var b = new StringBuilder(1500);
                b.Append(PK).Append("bilans ").Append(r.Markets[m]).Append(" (plan doba ").Append(_wDay).Append(", podaz/popyt; popyt t/d):");
                bool first = true;
                for (int g = 0; g < KsTab.NG; g++)
                {
                    double D = r.D[m * KsTab.NG + g];
                    if (D < 100) continue;
                    b.Append(first ? " " : ", ").Append(KsTab.Goods[g]).Append(' ').Append(F2(r.S[m * KsTab.NG + g] / D)).Append(" (").Append(F0(D / 1000)).Append(')');
                    first = false;
                }
                if (r.Eaten != null)
                {
                    // recenzja 17.10 (wazne): bilans NA TARG - zjedzone w okregach z wlasnego pola jest po obu stronach stosunku podaz/popyt i chowa brak na targu
                    b.Append(" | na targ (podaz - zjedzone w okregach) / (popyt - zjedzone):");
                    first = true;
                    for (int g = 0; g < KsTab.NG; g++)
                    {
                        double D = r.D[m * KsTab.NG + g];
                        if (D < 100) continue;
                        b.Append(first ? " " : ", ").Append(KsTab.Goods[g]).Append(' ').Append(F2(MarketQ(r, m, g)));
                        first = false;
                    }
                    first = true;
                }
                b.Append(" | dzis z mapy (zajecia startowe x tabela):");
                first = true;
                for (int g = 0; g < KsTab.NG; g++)
                {
                    double D = r.D0[m * KsTab.NG + g];
                    if (D < 100) continue;
                    b.Append(first ? " " : ", ").Append(KsTab.Goods[g]).Append(' ').Append(F2(r.SA[m * KsTab.NG + g] / D));
                    first = false;
                }
                b.Append('.');
                Log.Info(b.ToString());
                var c = new StringBuilder(1200);
                c.Append(PK).Append("ceny cienia ").Append(r.Markets[m]).Append(" (plan doba ").Append(_wDay).Append(", wzgledem wartosci bazowej; regale = 1):");
                first = true;
                for (int g = 0; g < KsTab.NG; g++)
                {
                    if (r.D[m * KsTab.NG + g] < 100 || KsTab.RegGoodMask[g]) continue;
                    c.Append(first ? " " : ", ").Append(KsTab.Goods[g]).Append(' ').Append(F2(r.P[m * KsTab.NG + g]));
                    first = false;
                }
                c.Append('.');
                Log.Info(c.ToString());
            }
            var w = new StringBuilder(1500);
            w.Append(PK).Append("zajecia (plan doba ").Append(_wDay).Append(", tys. rak):");
            var order = new List<int>(); for (int c = 0; c < KsTab.NC; c++) if (r.CraftLabour[c] >= 500) order.Add(c);
            order.Sort((a, b) => r.CraftLabour[b].CompareTo(r.CraftLabour[a]));
            for (int i = 0; i < order.Count; i++) w.Append(i == 0 ? " " : ", ").Append(KsTab.Crafts[order[i]]).Append(' ').Append(F0(r.CraftLabour[order[i]] / 1000));
            w.Append(" | regale (rece przy zlozu za cena):");
            bool f2 = true;
            for (int c = 0; c < KsTab.NC; c++) if (r.RegLabour[c] > 0) { w.Append(f2 ? " " : ", ").Append(KsTab.Crafts[c]).Append(' ').Append(F0(r.RegLabour[c])); f2 = false; }
            w.Append(" | ").Append(string.Join("; ", r.RegInfo.ToArray())).Append('.');
            Log.Info(w.ToString());
            // na targ (plan) wobec dzisiejszej produkcji wsi z ksiegi towarow - w sztukach gry na dobe
            long tNt = T0Now();
            double[] mk, tf, own; double kcal, need; int nd;
            if (r.NtMk != null)
            {
                // ks-koszt T1: sumy z watku roboczego (ten sam rachunek co nizej, liczony po Solve) - odbior bez 20 ms na watku glownym
                mk = r.NtMk; tf = r.NtTf; own = r.NtOwn; kcal = r.NtKcal; need = r.NtNeed; nd = r.NtNd; _t0Worker = true;
            }
            else
            {
            mk = new double[ItemCount]; tf = new double[ItemCount]; own = new double[ItemCount]; kcal = 0; need = 0; nd = 0;
            var live = BuildLive(r, inp, false);
            foreach (var ld in live)
            {
                var res = ComputeDistrict(ld, false, null);
                if (res == null) continue;
                nd++;
                for (int i = 0; i < ItemCount; i++) { if (ld.TF) tf[i] += res.Market[i]; else { mk[i] += res.Market[i]; own[i] += res.Own[i]; } }
                kcal += res.OwnKcal; need += res.KcalNeed;
            }
            }
            _t0[T0CMk] += T0Now() - tNt;
            var qb = new StringBuilder(2000);
            qb.Append(PK).Append("na targ (plan doba ").Append(_wDay).Append(", szt. gry/d = kg / (sigma x kg sztuki); wsie + pola mieszczan) wobec wsi dzis (ksiega towarow, srednia ").Append(_emaDays).Append(" dob):");
            bool f3 = true;
            for (int i = 0; i < ItemCount; i++)
            {
                if (mk[i] + tf[i] < 0.5 && Ema(ItemIds[i], GoodsLedger.KsVil) < 0.5) continue;
                qb.Append(f3 ? " " : ", ").Append(ItemIds[i]).Append(' ').Append(F0(mk[i])).Append(tf[i] >= 0.5 ? "+" + F0(tf[i]) : "").Append(" (dzis ").Append(F0(Ema(ItemIds[i], GoodsLedger.KsVil))).Append(')');
                f3 = false;
            }
            qb.Append(" | okregi zjadaja same: kcal z wlasnego pola i domu ").Append(need > 0 ? (100 * kcal / need).ToString("0", Inv) : "-").Append("% potrzeby (okregow ").Append(nd).Append(").");
            Log.Info(qb.ToString());
        }

        /// <summary>Stosunek NA TARG towaru g rynku m: (podaz - zjedzone w okregach) / (popyt - zjedzone); bez kroku 8 - podaz / popyt.</summary>
        private static double MarketQ(KsResult r, int m, int g)
        {
            int i = m * KsTab.NG + g;
            double D = r.D[i], S = r.S[i], E = r.Eaten != null && i < r.Eaten.Length ? Math.Max(0.0, r.Eaten[i]) : 0.0;
            if (E >= D) E = 0.0;
            return D - E > 1e-9 ? (S - E) / (D - E) : (D > 0 ? S / D : 0.0);
        }

        /// <summary>KS-R3 C2: tekst ks-plan.csv (watek roboczy - bez obiektow gry; nazwy osad ze zrzutu na watku glownym).</summary>
        private static string PlanCsvText(int day, KsResult r, KsInput inp, Dictionary<string, string> names, out int rowsOut)
        {
            // recenzja runda 3 (drobne): watek roboczy - bez leniwego zaladowania tabel towarow z gry (MBObjectManager); tabele zerowane (nowa sesja) - bez pliku
            rowsOut = -1;
            if (_itemIx == null || _items == null || _itemKg == null) return null;
            var live = BuildLive(r, inp, false);
            var byId = new Dictionary<string, KsDistrict>(StringComparer.Ordinal);
            foreach (var d in inp.Districts) byId[d.Id] = d;
            var sb = new StringBuilder(live.Count * 600);
            int rows = 0;
            foreach (var ld in live)
            {
                var res = ComputeDistrict(ld, false, null);
                if (res == null) continue;
                KsDistrict kd; byId.TryGetValue(ld.Id, out kd);
                string name = ld.Id;
                string nm0; if (names != null && names.TryGetValue(ld.Id, out nm0) && nm0 != null) name = nm0;
                // glowne zajecie okregu (najwiecej rak)
                var lab = new double[KsTab.NC]; double tot = 0;
                foreach (var b in ld.B) if (b != null && b.C != null) for (int j = 0; j < b.C.Length; j++) { lab[b.C[j]] += b.X[j] * b.Hands; tot += b.X[j] * b.Hands; }
                int best = 0; for (int c = 1; c < KsTab.NC; c++) if (lab[c] > lab[best]) best = c;
                string head = day + ";" + ld.Id + ";" + name + ";" + (ld.TF ? "pola mieszczan" : "wies") + ";" + r.Markets[ld.Market] + ";" + (kd != null ? kd.Land : "?") + ";" + (kd != null ? kd.Type : "?") + ";"
                              + F0(kd != null ? kd.People : 0) + ";" + F0(kd != null ? kd.Wf : 0) + ";";
                string tail = ";" + KsTab.Crafts[best] + ";" + (tot > 0 ? (lab[best] / tot).ToString("0.000", Inv) : "-") + ";" + ld.B.Length;
                for (int i = 0; i < ItemCount; i++)
                {
                    if (res.Prod[i] < 0.5 && res.Own[i] < 0.5) continue;
                    double mkg = res.MarketKg[i];
                    sb.Append(head).Append(ItemIds[i]).Append(';').Append(F0(res.Prod[i])).Append(';').Append(F0(res.Own[i])).Append(';').Append(F0(mkg)).Append(';')
                      .Append(res.Market[i].ToString("0.00", Inv)).Append(tail).Append(Environment.NewLine);
                    rows++;
                }
            }
            rowsOut = rows;
            return sb.ToString();
        }
    }
}
