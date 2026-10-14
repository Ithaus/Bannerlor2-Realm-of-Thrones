using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// BUDOWY ZA PIENIADZE (wpis 73; Jeff 05.10: "budowle ... nie wymagaja kasy, a powinny wymagac kasy tak jak materialow";
    /// "10% dochodu dziennie, w wojnie 0, chyba ze mury - tak"; "osobno [od skarbca krola] i dzialaj").
    /// Dziennik budow (wpis 69): 209 z 227 osad z moca 0 - BK liczy budowe z sily roboczej tylko przy polityce "Construction",
    /// robocizna byla darmowa, materialy BK za grosze. Historycznie pan najmowal murarzy, ciesli i robotnikow za dniowke i kupowal
    /// kamien, wapno, drewno; budowa szla tak szybko, jak pieniadze (docs/AUDYT-BUDOWY-SKARBIEC.md).
    /// Codziennie, dla kazdego miasta i zamku z budowa: pan placi BuildIncomeShare (10%) swojego dziennego dochodu (model finansow
    /// rodu + renty), dzielone na jego lenna z budowa; w wojnie tylko budowle wojskowe (mury, wieze, koszary). 25% na materialy
    /// kupione z targu (wapien, drewno, glina, narzedzia, marmur - do kasy miasta), 75% na place i wozy (do kasy osady) - bez
    /// materialow nie ma za co pracowac (najwyzej 3 pensy pracy na pens materialu). Punkty budowy = zaplacone / cena punktu
    /// (wojskowe 48 d, cywilne 24 d). Moc budowy gry (BK, vanilla) zastapiona oplacona praca; materialy BK wylaczone.
    /// </summary>
    internal static partial class BuildFunding
    {
        private static readonly Dictionary<Town, float> _funded = new Dictionary<Town, float>();
        private static readonly TextObject _txt = new TextObject("{=!}Paid works (wages and materials from the owner's purse)");
        private static readonly string[] MatIds = { "limestone", "hardwood", "clay", "tools", "marble", "planks" };
        internal static bool On { get { var s = Settings.Current; return s != null && s.PaidConstruction; } }

        internal static void Reset() { _funded.Clear(); _near.Clear(); FolkReset(); WoodReset(); }   // P2 recenzja: takze stan robot ludu sesji (BuildFunding.Folk.cs); K1: liczniki drewna

        // ------------------------------------------------------------ Towary 2 / K1 (docs/towary-2/ROZPOZNANIE-TOWARY-2.md A7-10, klucz BuildWoodAboveCraftStock)
        // Budowy braly 65% drewna swiata (1 350 ladunkow/d, linia "Drewno") i kupowaly je do 0 sztuk na polce w ticku dobowym, przed rzemioslem miasta - warsztaty
        // zbrojne tracily 304 rozpoczecia/d na drewno, 29 miast bez drewna. Regula: budowa (pan i lud) bierze drewno z polki targu tylko ponad zapas rzemiosla miasta
        // = CaravanBulk.TargetFor (ten sam zapas, ktory dopelniaja karawany: CaravanBulkStockDays dob zuzycia kuzni, dymarek, strzelarzy, linii towarowych i domow,
        // bez budow; nie mniej niz 40 ladunkow). Ponizej - budzet materialow idzie na wapien, gline, narzedzia, marmur, deski albo zostaje w kiesie (budowa czeka).
        private static readonly HashSet<Settlement> _woodHeldLord = new HashSet<Settlement>(), _woodHeldFolk = new HashSet<Settlement>();
        private static long _woodHeldLordPcs, _woodHeldFolkPcs;
        private static ItemObject _woodIt;
        internal static bool WoodHoldOn { get { var s = Settings.Current; return s != null && s.BuildWoodAboveCraftStock; } }
        private static void WoodReset() { _woodHeldLord.Clear(); _woodHeldFolk.Clear(); _woodHeldLordPcs = _woodHeldFolkPcs = 0; _woodIt = null; }

        /// <summary>K1: ile sztuk materialu budowa zostawia na polce targu dla rzemiosla miasta - dla drewna zapas rzemiosla (CaravanBulk.TargetFor), dla reszty 0;
        /// klucz wylaczony albo blad - 0 (jak dotad). folk - licznik robot ludu (P2), inaczej pana. Licznik: raz na targ i dobe, sztuki zostawione = min(polka, zapas).</summary>
        internal static int WoodHold(Settlement market, ItemObject it, bool folk)
        {
            try
            {
                if (!WoodHoldOn || market == null || market.Town == null || it == null || it.StringId != "hardwood") return 0;
                int keep = CaravanBulk.TargetFor(market.Town, it);
                if (keep <= 0) return 0;
                int shelf = market.ItemRoster != null ? market.ItemRoster.GetItemNumber(it) : 0;
                if (shelf > 0)
                {
                    if (folk) { if (_woodHeldFolk.Add(market)) _woodHeldFolkPcs += Math.Min(shelf, keep); }
                    else if (_woodHeldLord.Add(market)) _woodHeldLordPcs += Math.Min(shelf, keep);
                }
                return keep;
            }
            catch (Exception e) { Log.Error("BuildFunding.WoodHold", e); return 0; }
        }

        /// <summary>K1: dopisek linii "Roboty ludu (P2)" - po zakupach pana i ludu tej doby: targi, na ktorych budowy zostawily drewno rzemioslu (sztuki zostawione
        /// przy pierwszym zakupie doby), zapas rzemiosla miast razem i srednio, drewno na polkach miast, miasta ponizej zapasu i bez drewna. Liczniki doby zerowane tu.</summary>
        internal static string WoodTail()
        {
            var sb = new System.Text.StringBuilder(320);
            try
            {
                if (_woodIt == null) _woodIt = MBObjectManager.Instance.GetObject<ItemObject>("hardwood");
                long target = 0, shelf = 0; int towns = 0, below = 0, none = 0;
                if (_woodIt != null)
                    foreach (var t in Town.AllTowns)
                    {
                        if (t == null || !t.IsTown || t.Owner == null || t.Owner.ItemRoster == null) continue;
                        int tg = CaravanBulk.TargetFor(t, _woodIt), n = t.Owner.ItemRoster.GetItemNumber(_woodIt);
                        towns++; target += tg; shelf += n;
                        if (n < tg) below++;
                        if (n <= 0) none++;
                    }
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                sb.Append(" | drewno budow ponad zapas rzemiosla (BuildWoodAboveCraftStock) ").Append(WoodHoldOn ? "tak" : "nie").Append(": budowy zostawily drewno rzemioslu - pan na ")
                  .Append(_woodHeldLord.Count).Append(" targach (").Append(_woodHeldLordPcs).Append(" szt.), lud na ").Append(_woodHeldFolk.Count).Append(" targach (").Append(_woodHeldFolkPcs)
                  .Append(" szt.); zapas rzemiosla miast razem ").Append(target).Append(" szt. (srednio ").Append(towns > 0 ? ((double)target / towns).ToString("0.0", inv) : "-")
                  .Append("), drewno na polkach miast ").Append(shelf).Append(" szt., miast ponizej zapasu rzemiosla ").Append(below).Append(", bez drewna ").Append(none).Append(" z ").Append(towns);
            }
            catch (Exception e) { Log.Error("BuildFunding.WoodTail", e); }
            _woodHeldLord.Clear(); _woodHeldFolk.Clear(); _woodHeldLordPcs = _woodHeldFolkPcs = 0;
            return sb.ToString();
        }

        // ------------------------------------------------------------ moc budowy = oplacona praca
        [ThreadStatic] private static int _depth;
        public static void PowerPrefix() { _depth++; }
        public static Exception PowerFinalizer(Exception __exception) { if (_depth > 0) _depth--; return __exception; }
        // wpis 80 (audyt 05.10, pkt 2): nadpisujemy moc TYLKO osad objetych rozliczeniem. Lenna gracza przy wylaczonym
        // PaidConstructionPlayer ida po staremu (model gry/BK) - dotad dostawaly 0 i staly na zawsze.
        private static bool Covered(Town town)
        {
            var st = town.Settlement;
            if (st == null || st.OwnerClan == null) return false;
            return st.OwnerClan != Clan.PlayerClan || Settings.Current.PaidConstructionPlayer;
        }

        // wpis 80: przyspieszenie zlotem (vanilla BoostBuildingProcessWithGold zabiera zloto gracza) - dotad nasz postfiks je
        // gubil i zloto znikalo bez skutku. Dodajemy dzienna premie jak w grze/BK: GetBoostAmount x min(1, wplata / 500|250).
        private static float Boost(Town town)
        {
            try
            {
                if (town.BoostBuildingProcess <= 0) return 0f;
                int amount = Campaign.Current.Models.BuildingConstructionModel.GetBoostAmount(town);
                return amount * Math.Min(1f, town.BoostBuildingProcess / (town.IsCastle ? 250f : 500f));
            }
            catch { return 0f; }
        }

        private static readonly TextObject _boostTxt = new TextObject("Craftsmen services");

        public static void PowerPostfix(Town town, bool includeDescriptions, ref ExplainedNumber __result)
        {
            if (_depth > 1 || !On || town == null) return;
            try
            {
                // P2 (BuildFunding.Folk.cs): punkty robot ludu na czolo kolejki tej doby - druga pozycja dymku; lenno gracza bez PaidConstructionPlayer - dopisane
                // do mocy gry (projekt B rozdz. 6), reszta jak dotad
                float fl = FolkHead(town);   // recenzja (6): tylko gdy czolem jest dalej budowla, za ktora lud zaplacil
                if (fl > 0f && _folkTxt == null) _folkTxt = new TextObject("{=!}Town works (paid by the townsfolk)");
                if (!Covered(town)) { if (fl > 0f) __result.Add(fl, _folkTxt); return; }
                float f; _funded.TryGetValue(town, out f);
                var r = new ExplainedNumber(f, includeDescriptions, _txt);
                float b = Boost(town);
                if (b > 0f) r.Add(b, _boostTxt);
                if (fl > 0f) r.Add(fl, _folkTxt);
                __result = r;
            }
            catch { }
        }
        public static void PowerIntPostfix(Town town, ref int __result)
        {
            if (_depth > 1 || !On || town == null) return;
            float fl = FolkHead(town);   // P2: punkty robot ludu (jak wyzej; recenzja 6 - tylko na budowle, za ktora lud zaplacil)
            if (!Covered(town)) { if (fl > 0f) __result += (int)fl; return; }
            float f; _funded.TryGetValue(town, out f); __result = (int)(f + fl);
        }

        // ------------------------------------------------------------ materialy BK wylaczone
        public static bool SkipIfOn() { return !On; }

        // wpis 88 (audyt pkt 14, kod BK RunMines): miasto placi za urobek kopalni num zlota DO NIKOGO, a panu idzie 0.5 x num
        // przez podatek - polowa znikala. Druga polowa to place gornikow, wydane w tym samym miescie: wraca do kasy miasta.
        // Do tego miningRevenues zerowalo sie tylko przy dzialajacej kopalni - stara kwota zostawala w podatku na zawsze.
        public static void MinesPrefix(object __instance, Town town)
        {
            try
            {
                var d = HarmonyLib.Traverse.Create(__instance).Field("miningRevenues").GetValue() as System.Collections.IDictionary;
                if (d != null && town != null && d.Contains(town)) d[town] = 0;
            }
            catch { }
        }
        public static void MineRevenuePostfix(Town town, int revenue)
        {
            try { if (town != null && revenue > 0 && Settings.Current.MineWagesStayInTown) { int folk = TownFolk.Earn(town.Settlement, revenue, TownFolk.KMine); town.ChangeGold(revenue - folk); CirculationWindows.NoteMineFolk(folk); } } catch { }   // E5/B1: place gornikow do kiesy ludu (W), bez kiesy - do kasy jak dotad; recenzja B1 (1-3): czesc ludu do okna O20 (wlasny try)
            try { if (town != null && revenue > 0 && Settings.Current.MineWagesStayInTown) CirculationWindows.NoteMineWages(revenue); } catch { }   // paczka 169: okno O20 (tylko licznik)
            try { if (town != null && revenue > 0 && Settings.Current.MineWagesStayInTown) TownFolk.Note(town.Settlement, revenue, TownFolk.KMine); } catch { }   // E5/0 (195): place gornikow (tylko licznik)
        }

        // wpis 86 (audyt pkt 3): BK zeruje materialExpenses na POCZATKU RunMaterials, a my pomijamy cala metode - na save sprzed
        // wpisu 73 stary koszt materialow zostawal na zawsze i KeptTownLines odejmowal go co dzien od podatku miasta (zloto znikalo)
        public static bool SkipMaterials(object __instance)
        {
            if (!On) return true;
            try
            {
                var d = HarmonyLib.Traverse.Create(__instance).Field("materialExpenses").GetValue() as System.Collections.IDictionary;
                if (d != null && d.Count > 0) d.Clear();
            }
            catch { }
            return false;
        }
        public static void EmptyMaterials(ref List<(ItemObject, int)> __result) { if (On) __result = new List<(ItemObject, int)>(); }

        // ------------------------------------------------------------ codzienne rozliczenie
        private static int _paidTowns, _stalledNoMat, _warSkipped; private static long _spent, _toPurses, _toMarkets; private static double _points;

        internal static void Daily()
        {
            if (!On || Campaign.Current == null) return;
            _funded.Clear();
            _paidTowns = _stalledNoMat = _warSkipped = 0; _spent = _toPurses = _toMarkets = 0; _points = 0; _wageIdxSum = 0f; _wageIdxN = 0;
            try
            {
                var s = Settings.Current;
                // ile lenn z budowa ma kazdy rod
                var count = new Dictionary<Clan, int>();
                var jobs = new List<KeyValuePair<Settlement, TaleWorlds.CampaignSystem.Settlements.Buildings.Building>>();
                var civilWar = new List<KeyValuePair<Settlement, TaleWorlds.CampaignSystem.Settlements.Buildings.Building>>();   // N5-E: budowle cywilne w wojnie dopuszczone z dala od frontu
                foreach (var st in Settlement.All)
                {
                    if (st == null || st.Town == null || (!st.IsTown && !st.IsCastle) || st.OwnerClan == null || st.IsUnderSiege) continue;
                    var q = st.Town.BuildingsInProgress;
                    var b = q != null && q.Count > 0 ? q.Peek() : null;
                    if (b == null || b.BuildingType == null || b.BuildingType.IsDailyProject) continue;
                    if (st.OwnerClan == Clan.PlayerClan && !s.PaidConstructionPlayer) continue;
                    // wpis 86 (audyt pkt 8): wojna wstrzymuje cywilne JUZ TU - nie zabieraja dzialki murom
                    bool warNow = false;
                    try { if (st.OwnerClan.Kingdom != null) foreach (var k in Kingdom.All) if (k != st.OwnerClan.Kingdom && !k.IsEliminated && st.OwnerClan.Kingdom.IsAtWarWith(k)) { warNow = true; break; } } catch { }
                    if (warNow && !b.BuildingType.IsMilitaryProject)
                    {
                        // N5-E (poprawka 5 po biegu rocznym): o budowli cywilnej w wojnie decyduje prawdziwe zagrozenie warowni, nie flaga wojny krolestwa
                        // (WarFront.CivilAllowed: spokoj wokol warowni od CivilWorksCalmDays dob i luz pana w pulapie zoldu). Klucz wylaczony - false, jak dotad
                        if (!WarFront.CivilAllowed(st)) { _warSkipped++; continue; }
                        civilWar.Add(new KeyValuePair<Settlement, TaleWorlds.CampaignSystem.Settlements.Buildings.Building>(st, b));
                        continue;
                    }
                    jobs.Add(new KeyValuePair<Settlement, TaleWorlds.CampaignSystem.Settlements.Buildings.Building>(st, b));
                    int n; count.TryGetValue(st.OwnerClan, out n); count[st.OwnerClan] = n + 1;
                }
                // N5-E: mury pierwsze (wpis 86: cywilne nie zabieraja dzialki murom) - rod z budowa wojskowa w toku dzieli przydzial tylko miedzy nie, jak dotad;
                // cywilne w wojnie ida tylko u rodu bez budowy wojskowej (jego przydzial budow lezal dotad bez uzytku)
                if (civilWar.Count > 0)
                {
                    var wallClans = new HashSet<Clan>(count.Keys);
                    foreach (var kv in civilWar)
                    {
                        var clan = kv.Key.OwnerClan;
                        if (wallClans.Contains(clan)) { _warSkipped++; WarFront.NoteWallsFirst(); continue; }
                        jobs.Add(kv); WarFront.NoteGo();
                        int nc; count.TryGetValue(clan, out nc); count[clan] = nc + 1;
                    }
                }
                var income = new Dictionary<Clan, float>();
                var model = Campaign.Current.Models.ClanFinanceModel;
                foreach (var kv in jobs)
                {
                    var st = kv.Key; var b = kv.Value; var clan = st.OwnerClan; var lord = clan.Leader;
                    if (lord == null || !lord.IsAlive) continue;
                    bool military = b.BuildingType.IsMilitaryProject;
                    float inc;
                    if (!income.TryGetValue(clan, out inc))
                    {
                        try { inc = model.CalculateClanIncome(clan, false, false, false).ResultNumber; } catch { inc = 0f; }
                        int rent; PopulationLaw.RentToday.TryGetValue(clan, out rent); inc += rent;
                        income[clan] = inc;
                    }
                    float budget = Math.Max(0f, inc) * Math.Max(0f, s.BuildIncomeShare) / Math.Max(1, count[clan]);
                    float share;
                    // 166: przydzial budow budzetu rodu (0.10 D); w wojnie tylko wojskowe - cywilne odciete wyzej (recenzja C1, W5: Jeff 05.10 "chyba ze mury - tak")
                    if (ClanBudget.BuildShare(clan, out share)) budget = share / Math.Max(1, count[clan]);
                    budget = Math.Min(budget, Math.Max(0, lord.Gold));
                    if (budget < 1f) continue;
                    float ppp = military ? Math.Max(1f, s.BuildPencePerPointMilitary) : Math.Max(1f, s.BuildPencePerPointCivil);
                    // materialy z targu (miasto - swoj, zamek - najblizsze miasto)
                    var market = st.IsTown ? st : NearestTown(st);
                    float matBudget = budget * MBMath.ClampFloat(s.BuildMaterialShare, 0f, 1f);
                    int matSpent = BuyMaterials(market, matBudget);
                    // wpis 86 (audyt pkt 15): towar jest, tylko drozszy niz dzienny budzet - jedna najtansza sztuka, jesli pana stac
                    if (matBudget > 0f && matSpent <= 0) matSpent = BuyOneCheapest(market, Math.Max(0, lord.Gold));
                    if (matBudget > 0f && matSpent <= 0) { _stalledNoMat++; continue; }   // nie ma z czego budowac
                    float labour = Math.Min(budget - matBudget, matSpent * Math.Max(0f, (1f - s.BuildMaterialShare) / Math.Max(0.01f, s.BuildMaterialShare)));
                    int labourI = MBRandom.RoundRandomized(labour);
                    // poprawka po audycie TOWARY 3: material drozszy niz budzet (BuyOneCheapest do calej kiesy) + robocizna z budzetu moglo dac
                    // wiecej niz lord ma - ChangeHeroGold obcina kiese do 0, a kasy dostawaly pelne kwoty (zloto z niczego); robota najwyzej z reszty kiesy
                    labourI = Math.Min(labourI, Math.Max(0, lord.Gold - matSpent));
                    lord.ChangeHeroGold(-(matSpent + labourI));
                    CirculationWindows.NoteHeroGold(lord, -(matSpent + labourI));   // paczka 169b: glowa poza swiatem - zloto weszlo do swiata (tylko licznik)
                    if (market != null && market.Town != null)
                    {
                        market.Town.ChangeGold(matSpent);
                        ClanIncomeBook.NoteOwnPaid(market, clan, matSpent);   // 110-p (K8): "wlasne" D stalego - materialy kupione w miescie rodu (tylko licznik; obca osada - nic)
                        try { TownFolk.Sale(market, matSpent, TownFolk.MixBuild); } catch { }   // E5/0 (195): material budow - sprzedaz kupcow (tylko licznik)
                    }
                    int folk = TownFolk.Earn(st, labourI, TownFolk.KBuildLabour);   // E5/B1 (R11, KL 1.3): place budowy do kiesy ludu W60/R40 (zamek - podzamcze); arytmetyka
                    st.Town.ChangeGold(labourI - folk);          // place murarzy, robotnikow, woznic - do kasy osady (E5/B1: do kiesy ludu)
                    ClanIncomeBook.NoteOwnPaid(st, clan, labourI - folk);   // 110-p (K8): "wlasne" D stalego - place budowy w kasie wlasnego zamku albo miasta wracaja zaworem (E5/B1: place w kiesie ludu nie wracaja)
                    try { TownFolk.Note(st, labourI, TownFolk.KBuildLabour); } catch { }   // E5/0 (195): place budowy (tylko licznik)
                    float pts = PointsFor(matSpent, labourI, ppp, market);
                    _wageIdxSum += WageIdx(market); _wageIdxN++;
                    _funded[st.Town] = pts;
                    _paidTowns++; _spent += matSpent + labourI; _toMarkets += matSpent; _toPurses += labourI; _points += pts;
                }
            }
            catch (Exception e) { Log.Error("BuildFunding", e); }
            Log.Info("Budowy oplacone: dzien " + (int)CampaignTime.Now.ToDays + " - osad " + _paidTowns + ", wydano " + _spent + " (place i wozy do kas osad " + _toPurses
                     + ", materialy z targow " + _toMarkets + "), punktow budowy " + (int)_points + " (place wedle miast targowych: sredni poziom plac "
                     + (_wageIdxN > 0 ? (_wageIdxSum / _wageIdxN).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "-") + ")"
                     + "; wstrzymane: brak materialow " + _stalledNoMat + ", wojna (budowle cywilne) " + _warSkipped + "." + WarFront.LineTail());
            // P2 (lud buduje miasto): drugi platnik - roboty ludu PO budowach pana (pan pierwszy na targu); ta sama ramka ksiegi towarow "budowy" (wolajacy) i pozycja MBuild
            try { FolkWorks(); } catch (Exception e) { Log.Error("BuildFunding.FolkWorks", e); }
        }

        /// <summary>
        /// Punkty budowy z zaplaty (Jeff 07.10: koszty w miescie z dobrobytu i stawek historycznych): cena punktu ppp liczona przy
        /// zwyklej dniowce; pens plac kupuje tyle pracy, ile dniowka w miescie targowym, z ktorego sa robotnicy (TownWage - dla zamku
        /// najblizsze miasto, jak przy materialach) - w bogatym miescie mniej, w biednym wiecej. Materialy - po cenie targu jak dotad.
        /// </summary>
        internal static float PointsFor(int matSpent, int labour, float ppp, Settlement market)
        {
            return (matSpent + labour / WageIdx(market)) / Math.Max(1f, ppp);
        }

        private static float WageIdx(Settlement market)
        {
            var s = Settings.Current;
            return s != null && s.BuildWagesByTown ? Math.Max(0.1f, TownWage.Index(market)) : 1f;
        }

        private static float _wageIdxSum; private static int _wageIdxN;

        private static int BuyOneCheapest(Settlement market, int purse)
        {
            if (market == null || market.Town == null || market.ItemRoster == null) return 0;
            ItemObject best = null; int bp = int.MaxValue;
            foreach (var id in MatIds)
            {
                var it = MBObjectManager.Instance.GetObject<ItemObject>(id);
                if (it == null || market.ItemRoster.GetItemNumber(it) - WoodHold(market, it, false) <= 0) continue;   // K1: drewno tylko ponad zapas rzemiosla miasta (klucz wylaczony - 0)
                int price = Math.Max(1, market.Town.GetItemPrice(it, null, false));
                int cap = CapOf(it);   // P2: sufit ceny materialu takze dla pana (klucz FolkBuildsTown; wylaczony - 0)
                if (cap > 0 && price > cap) continue;
                if (price < bp) { bp = price; best = it; }
            }
            if (best == null || bp > purse) return 0;
            market.ItemRoster.AddToCounts(best, -1);
            OreLedger.NoteBuild(best, 1);   // ksiega rudy i drewna: pozycja "budowy" (tylko licznik)
            return bp;
        }

        private static int BuyMaterials(Settlement market, float budget)
        {
            if (market == null || market.Town == null || market.ItemRoster == null || budget < 1f) return 0;
            int spent = 0;
            var roster = market.ItemRoster;
            for (int pass = 0; pass < 3 && spent < budget; pass++)
                foreach (var id in MatIds)
                {
                    var it = MBObjectManager.Instance.GetObject<ItemObject>(id);
                    if (it == null) continue;
                    int have = roster.GetItemNumber(it) - WoodHold(market, it, false);   // K1: drewno tylko ponad zapas rzemiosla miasta (klucz wylaczony - 0)
                    if (have <= 0) continue;
                    int price = Math.Max(1, market.Town.GetItemPrice(it, null, false));
                    int cap = CapOf(it);   // P2: sztuka najwyzej BuildMaterialPriceCap x wartosc (obaj platnicy; klucz FolkBuildsTown wylaczony - 0, bez sufitu)
                    if (cap > 0 && price > cap) { if (pass == 0) _capLord++; continue; }
                    int n = Math.Min(have, (int)((budget - spent) / price / (3 - pass)) + (pass == 2 ? 0 : 1));
                    n = Math.Min(n, (int)((budget - spent) / price));
                    if (n <= 0) continue;
                    // ceny hurtu (Jeff 09.10 08:00): n jak dotad (podzial budzetu na przejscia wedle ceny pierwszej sztuki), ale kazda sztuka po swojej
                    // cenie - wycena od nowa po zdjeciu poprzedniej (ShelfBuy); partia konczy sie, gdy kolejna sztuka nie miesci sie w budzecie
                    var mel = new EquipmentElement(it);
                    int cost, first, last;
                    n = ShelfBuy.Take(roster, mel, n, (int)(budget - spent), () => Math.Max(1, market.Town.GetItemPrice(mel, null, false)), out cost, out first, out last, price, true, cap);
                    if (n <= 0) continue;
                    OreLedger.NoteBuild(it, n);   // ksiega rudy i drewna: pozycja "budowy" (tylko licznik)
                    spent += cost;
                    if (spent >= budget) break;
                }
            return spent;
        }

        /// <summary>P1 (pomiar obiegu kiesy ludu, TYLKO ODCZYT - nic nie kupuje i nie zmienia): czolo kolejki budow osady - false, gdy nic nie czeka albo czeka projekt
        /// dzienny. military - budowla wojskowa; matPieces - sztuki materialu budow (MatIds) na polce targu osady (miasto - swoj, zamek - najblizsze miasto,
        /// ten sam targ co przy zakupie w Daily).</summary>
        internal static bool QueueHead(Settlement st, out bool military, out int matPieces)
        {
            military = false; matPieces = 0;
            if (st == null || st.Town == null) return false;
            var q = st.Town.BuildingsInProgress;
            var b = q != null && q.Count > 0 ? q.Peek() : null;
            if (b == null || b.BuildingType == null || b.BuildingType.IsDailyProject) return false;
            military = b.BuildingType.IsMilitaryProject;
            var market = st.IsTown ? st : NearestTown(st);
            if (market != null && market.ItemRoster != null)
                foreach (var id in MatIds)
                {
                    var it = MBObjectManager.Instance.GetObject<ItemObject>(id);
                    if (it != null) matPieces += Math.Max(0, market.ItemRoster.GetItemNumber(it));
                }
            return true;
        }

        private static readonly Dictionary<Settlement, Settlement> _near = new Dictionary<Settlement, Settlement>();
        internal static Settlement NearestTown(Settlement st)   // P5 (TownFolk.CastleLadder): podzamcze kupuje z listy dostatku w tym samym miescie co material budow zamku
        {
            Settlement m;
            if (_near.TryGetValue(st, out m)) return m;
            float best = float.MaxValue;
            var p = st.GetPosition2D;
            foreach (var t in Settlement.All)
            {
                if (t == null || !t.IsTown) continue;
                float d = p.DistanceSquared(t.GetPosition2D);
                if (d < best) { best = d; m = t; }
            }
            _near[st] = m;
            return m;
        }

        internal static void ApplyAll(Harmony h)
        {
            int n = 0;
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); } catch (System.Reflection.ReflectionTypeLoadException e) { types = e.Types; } catch { continue; }
                    foreach (var t in types)
                    {
                        try
                        {
                            if (t == null || t.IsAbstract || !typeof(TaleWorlds.CampaignSystem.ComponentInterfaces.BuildingConstructionModel).IsAssignableFrom(t)) continue;
                            var m = t.GetMethod("CalculateDailyConstructionPower", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
                            if (m != null)
                            {
                                h.Patch(m, prefix: new HarmonyMethod(typeof(BuildFunding), nameof(PowerPrefix)) { priority = Priority.First },
                                           postfix: new HarmonyMethod(typeof(BuildFunding), nameof(PowerPostfix)) { priority = Priority.Last },
                                           finalizer: new HarmonyMethod(typeof(BuildFunding), nameof(PowerFinalizer)));
                                n++;
                            }
                            var m2 = t.GetMethod("CalculateDailyConstructionPowerWithoutBoost", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
                            if (m2 != null)
                                h.Patch(m2, prefix: new HarmonyMethod(typeof(BuildFunding), nameof(PowerPrefix)) { priority = Priority.First },
                                            postfix: new HarmonyMethod(typeof(BuildFunding), nameof(PowerIntPostfix)) { priority = Priority.Last },
                                            finalizer: new HarmonyMethod(typeof(BuildFunding), nameof(PowerFinalizer)));
                        }
                        catch { }
                    }
                }
                var bkb = AccessTools.TypeByName("BannerKings.Behaviours.BKBuildingsBehavior");
                int off = 0;
                if (bkb != null)
                    foreach (var name in new[] { "RunMaterials", "OnBuildingChanged" })
                    {
                        var m = AccessTools.Method(bkb, name);
                        if (m != null) { h.Patch(m, prefix: new HarmonyMethod(typeof(BuildFunding), name == "RunMaterials" ? nameof(SkipMaterials) : nameof(SkipIfOn))); off++; }
                    }
                if (bkb != null)
                {
                    var rm = AccessTools.Method(bkb, "RunMines");
                    var ar = AccessTools.Method(bkb, "AddRevenue");
                    if (rm != null) h.Patch(rm, prefix: new HarmonyMethod(typeof(BuildFunding), nameof(MinesPrefix)));
                    if (ar != null) h.Patch(ar, postfix: new HarmonyMethod(typeof(BuildFunding), nameof(MineRevenuePostfix)));
                    Log.Info("BuildFunding: kopalnie BK - place gornikow wracaja do kasy miasta (" + (ar != null) + "), stary dochod kopalni zerowany (" + (rm != null) + ").");
                }
                var bkm = AccessTools.TypeByName("BannerKings.Models.Vanilla.BKConstructionModel");
                var gm = bkm != null ? AccessTools.Method(bkm, "GetMaterialRequirements") : null;
                if (gm != null) { h.Patch(gm, postfix: new HarmonyMethod(typeof(BuildFunding), nameof(EmptyMaterials))); off++; }
                Log.Info("BuildFunding: budowy za pieniadze pana - moc budowy w " + n + " modelach, materialy BK wylaczone w " + off + "/3 miejscach.");
            }
            catch (Exception e) { Log.Error("BuildFunding.ApplyAll", e); }
        }
    }
}
