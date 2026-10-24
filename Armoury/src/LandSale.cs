using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// m113-zakup - KUPNO ZIEMI OD RODOW W POTRZEBIE (wioska poboczna B1, tytul Lordship wsi gry B2, zamek B3). Klucze LandSales, LandSalesSideVillages,
    /// LandSalesLordships, LandSalesCastles, LandSalesSellBack (+ liczby w Settings, grupa "Land sales (m113)").
    /// Jeff (pierwsza prosba, 24.10): "kupno wioski za dlugi lordow ... zamek to beda ogromne sumy, bo malo kto chce sprzedac rodowa siedzibe, ale wioske tak".
    /// Jeff (doprecyzowanie 24.10 rano): "kosztuje tyle, ile kosztuje i zarabia ... nie za grosze ... mozna obnizyc, jak dlugi sa za duze, ale nie nadmiernie".
    /// Decyzja Jeffa "[D] 05.10 lord nigdy nie traci lenna" dalej obowiazuje dla drabiny dlugu (DebtLadder): wierzyciel bierze dochod, nie ziemie. Sprzedaz tutaj jest
    /// DOBROWOLNA, przez rod w potrzebie, i NIGDY nie jest szczeblem drabiny dlugu (zaden szczebel jej nie wola; rod sam decyduje w rozmowie / ofercie).
    ///
    /// DOWOD HISTORYCZNY (lata zakupu = cena ziemi / roczny dochod netto ziemi):
    ///  - G. Clark (koszt kapitalu w rolnictwie sredniowiecznej Anglii): ziemia w XIII-XV w. szla za ok. 10-20 lat dochodu (stopa zwrotu z ziemi ok. 5-10% rocznie),
    ///    https://www.medievalists.net/2012/02/the-cost-of-capital-and-medieval-agricultural-technique/
    ///    i po Czarnej Smierci - cena ziemi wzgledem czynszu (ok. 1300-1500), https://www.medievalists.net/2010/09/25/microbes-and-markets-was-the-black-death-an-economic-revolution/
    ///  - Sir John Fastolf (list do Howysa i Barkera, XV w.): kupno dobr liczone wedlug lat dochodu ("years' purchase") - ziemia kupowana za kilkanascie lat czynszu,
    ///    https://medievalfamilylife.amdigital.co.uk/Documents/Detail/sir-john-fastolf-to-sir-thomas-howys-and-william-barker/594796
    ///  Stad 14-20 lat wedlug stosunku (n36, ponizej), dolna granica 10 lat (LandYearsFloor), w wojnie x0.85 (ryzyko - nizsza cena), upust za dlugi najwyzej 15%.
    ///  Jeff (24.10, po "cena = 14 lat dochodu, historycznie 10-20, typowo 20"): "daj cene zalezna od relacji miedzy 14 a 20 lat, jesli relacja jest za niska nikt nie
    ///  sprzeda, im wyzsza tym lepsza cena".
    ///
    /// ZASADY:
    ///  Z1 SPRZEDAWCA. Wioska poboczna: posiadacz podatku okregu H = BK GetActualOwner(wies) (tytul de iure, gdy jego frakcja = frakcja wsi; inaczej glowa rodu pana zamku;
    ///     odbiciem: TitleManager.GetTitle, FeudalTitle.deJure; bez BK - glowa rodu pana zamku). H zywy i glowa rodu - inaczej "its tax goes to H, who cannot sell for his house".
    ///     Lordship (B2): glowa rodu, ktora trzyma tytul de iure. Zamek (B3): glowa Town.OwnerClan, ktora trzyma de iure tytul BK zamku (przy BK).
    ///     Nigdy: rod gracza, wyeliminowany, bandyci, rody pomniejsze i w sluzbie najemnej, Straz i Wolni Ludzie (bez zoldu), rod panujacy (zamki). Rod w zajeciu (D3/D4) moze.
    ///  Z2 POTRZEBA (ClanBudget.NeedOf - G, R, D i seria dob ponad pulapem zoldu; nie wyklucza dluznikow ani zajetych): N1 dluznik 168, N2 dlug okupu, N3 G < R, N4 seria
    ///     >= Budget Hysteresis Days. Wioska poboczna - N1, N2, G < 0.5 R albo N4 (recenzja: samo G < R - rezerwa wojny - nie wystarcza); Lordship - N1, N2 albo
    ///     G < 0.5 R; zamek - zajecie / wyprzedaz albo N1 z dlugiem >= 91 D przy D > 0 (recenzja: sama zaleglosc jednej raty i D = 0 to nie gleboki dlug).
    ///     Przerwa: jedna sprzedaz rodu na 28 dob, jeden zamek na 364 doby (arm_land113).
    ///  Z3 BRAMKI: gracz glowa rodu; bez wojny z krolestwem sprzedawcy; sprzedawca nie w bitwie i nie w niewoli; relacja >= -10 / 0 / 10. Wioska: stoi albo uszkodzona
    ///     (VillageBook), wies gry nie rabowana, nie gracza, gracz nie jest panem ani posiadaczem podatku okregu (przysiolki tak). Lordship: gracz wasalem (nie najemnikiem)
    ///     krolestwa sprzedawcy i wsi; sprzedawca zostaje z co najmniej jednym innym tytulem de iure (zostaje rycerzem BK); wies nie rabowana. Zamek: to samo krolestwo, gracz
    ///     wasalem; sprzedawca zostaje z innym miastem albo zamkiem; nie ROT_castle60 / ROT_castle61; bez oblezenia i bitwy; tytuly BK zamku i kazdej wsi (albo bez BK);
    ///     dochod netto > 0.
    ///  Z4 CENA = round100(NetDay x dni roku (364 przy kalendarzu Jeffa) x lata x premia x (1 - upust)). Lata wedlug stosunku glowy, ktora decyduje, do gracza (n36,
    ///     ta sama liczba co brama Z3; YearsFor): 20 (LandYearsWorst) przy progu rodzaju, liniowo do 14 (LandYearsOfPurchase - najlepsze) przy progu + 60
    ///     (LandYearsRelationSpan) i wyzej - wioska -10 -> 20 lat, +50 -> 14; Lordship 0 -> 20, 60 -> 14; zamek 10 -> 20, 70 -> 14; ponizej progu nikt nie sprzedaje.
    ///     Odkup (Z7) odwrotnie - przyjaciel placi wiecej: 14 przy progu (i ponizej), 20 przy progu + 60, ale nie wiecej lat, niz gracz zaplacil za te wioske (PaidYears). Potem w wojnie krolestwa osady z wielkim krolestwem x0.85;
    ///     lata x (1 - upust) >= 10. Premia: wioska 1, Lordship 1, zamek 1.5, zamek-siedziba (HomeSettlement albo siedziba z ksiegi rodow kanonu) 2.0.
    ///     Upust = 0.15 x clamp((q - 0.5) / 1.5, 0, 1), q = dlug / max(1, G + 91 D).
    ///     NetDay: wioska - SideVillageFief.DueFor (wartosc dnia x H x stawka pana) x poprawka na srednia roku (n36 SideYearFix: przy pelnym roku w ksiedze
    ///     LandValue.SeasonFixFullYear - rok VT okregu / ostatnie 7 dob sesji; inaczej model: (1 - 0.25 x ciecie zimy wsi) / VillageCrafts.Now; clamp 0.25-4); Lordship - YearMean VT wsi gry; zamek - YearMean renty zamku (zawor + cla) + renty wsi zamku + VT wsi,
    ///     ktorych Lordship trzyma sprzedawca, minus straz (0.5 x norma zalogi zamku krolestwa x sredni zold czlowieka zalogi; zapas - zold rekruta tier 2 kultury).
    ///     Renta korony NIE wchodzi (to nie dochod ziemi). Recenzja: Lordship i zamek - co najmniej 28 dob pomiaru (kazda liczona wies tez); minus udzialy wiosek
    ///     pobocznych GRACZA w tych okregach, ktore placi sprzedawca (po kupnie przestaja plynac - to czesc kupowanego podatku; n36: na podstawie roku jak cena wioski);
    ///     minus powinnosci korony od ziemi (KingdomTreasury.DuesRateOf) wedlug KUPUJACEGO (n36: rod panujacy, najemnik, bez krolestwa - brak; wioska poboczna tylko
    ///     z SideVillageDuesInLordD i podstawa "ziemia").
    ///  Z5 PIENIADZE: cala cena w kiesie gracza. Umowa w kolejce - wykonanie w nastepnym TickEvent bez rozmowy; wszystko (z cena) sprawdzane od nowa, roznica - umowa
    ///     przepada z komunikatem. (a) GiveGoldAction gracz -> glowa sprzedawcy (InPay tylko wokol tego przelewu); zaplacone = kiesa gracza mniejsza dokladnie o cene;
    ///     (b) przeniesienie (Z6) - o powodzeniu decyduje STAN po nim (Moved: wioska gracza / de iure gracza / zamek gracza), nie wyjatek ani wynik wywolania;
    ///     (c) nie przeszlo - zwrot glowa -> gracz (pod InPay, najwyzej tyle, ile glowa ma - bez zlota z niczego), "transakcja cofnieta"; (d) DebtLadder.LandProceeds -
    ///     wierzyciele biora swoje z ceny (tylko przy wlaczonej drabinie i rodzie w niej). Ksiega rodow: cena NIE jest
    ///     wplywem (ClanIncomeBook TodayLand - poza jednorazowymi, D169 i D stalym; klucz LandPriceNotWindfall) - Bank i KW nie rosna od sprzedazy ziemi.
    ///  Z6 PRZENIESIENIE: wioska - SideVillageFief.AddOwned (pierwsza albo dodatkowa); Lordship - TitleManager.InheritTitle(sprzedawca, gracz, tytul) i sprawdzenie de iure
    ///     (nigdy GrantTitle); zamek - ChangeOwnerOfSettlementAction.ApplyByBarter (BK AddBarter przenosi Barony i Lordship wsi sprzedawcy; zapas InheritTitle),
    ///     ConsiderAndUpdateHomeSettlement obu rodow, liczba zalogi, siedziby i ostrzezenie Kroniki "own:".
    ///  Z7 ODKUP (LandSalesSellBack): wioska gracza po 182 dobach do posiadacza podatku okregu (platnik PayerOf - ta sama regula co sprzedawca; placi glowa jego rodu),
    ///     ta sama cena Z4 z upustem wedlug dlugow GRACZA i latami odkupu ze stosunku kupujacego (przyjaciel placi wiecej); rod placi, gdy TourneyPay.ClanSpare >= cena i nie jest dluznikiem; pierwsza wioska tylko z pusta skrzynia domu.
    ///  Recenzja: sprzedawca wioski pobocznej = rod platnika (SideVillageFief.PayerOf - klucz SideVillagePayerIsTaxHolder przelacza sprzedawce i platnika razem);
    ///     platnik rycerz BK (nie glowa rodu) - sprzedaje glowa jego rodu, podatek dalej bierze rycerz.
    ///  Z8 AI NIE KUPUJE ZIEMI w tej paczce: (1) AI nie ma jeszcze pojecia majatku poza lennami - wioska poboczna AI nie istnieje w modelu gry (nikt by jej nie widzial
    ///     ani nie liczyl), (2) budzet 166 nie ma pozycji "kapital na ziemie" - zakupy zjadalyby rezerwe wojny i pulap zoldu, (3) handel lennami miedzy AI zmienia mape
    ///     wplywow i BK (demesne, rycerze) bez kontroli gracza - to osobny projekt z testem dlugim.
    /// Zapis: arm_land113 "ls1|c=rod:ostatnia_sprzedaz:ostatni_zamek,...|d=rodzaj:id:doba:cena:lata100:upust1000:rod,..." (nieznane czesci pominiete); arm_landval (LandValue);
    /// arm_side_fief2 (SideVillageFief). Wlasnosc zamkow i tytulow - w zapisie gry i BK. Linie logu "Ziemia (m113): ...". Potkniecia liczone na miejsce; nic nie rzuca
    /// z rozmow, menu i tickow.
    /// </summary>
    internal static partial class LandSale
    {
        internal const string P = "Ziemia (m113): ";
        internal const int KSide = 1, KLord = 2, KCastle = 3;
        internal const string AutoFlagName = "armoury-ziemia.flag";
        private const string SaveVer = "ls1";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly string[] Twins = { "ROT_castle60", "ROT_castle61" };

        /// <summary>Z5: przelew ceny ziemi w toku (gracz -> sprzedawca, zwrot, odkup) - ClanIncomeBook.OnEvent liczy go jako TodayLand (nie dochod).</summary>
        internal static bool InPay;
        internal static bool NotWindfall { get { var s = Settings.Current; return s == null || s.LandPriceNotWindfall; } }
        internal static bool On { get { var s = Settings.Current; return s != null && s.LandSales; } }

        // ------------------------------------------------------------ stan kampanii (arm_land113)
        private sealed class Cd { public int LastSale = -100000, LastCastle = -100000; }
        private sealed class Deal { public int Kind, Day, Years100, Disc1000; public string Id = "", Seller = ""; public long Price; }
        private static readonly Dictionary<string, Cd> _cd = new Dictionary<string, Cd>(StringComparer.Ordinal);
        private static readonly List<Deal> _deals = new List<Deal>();

        // ------------------------------------------------------------ sesja
        private static readonly Dictionary<string, int> _st = new Dictionary<string, int>(StringComparer.Ordinal);   // potkniecia na miejsce
        private static int _stAll, _stDay;
        private static Offer _queued;
        private static int _dBuilt, _dDeals, _dDropped, _dRefund, _dAsk, _dWindows, _dSellBack;
        private static int _dryDay = -1000;
        private static bool _dryDone, _testDone;
        // autotest
        private static bool _forceNeed, _forceFail, _bypassResale, _testLift;   // test n36: _testLift - bramy, ktore gracz zmieni w grze, nie zamykaja oferty (LiftGates)

        internal static void Reset()
        {
            _cd.Clear(); _deals.Clear();
            _st.Clear(); _stAll = _stDay = 0; _queued = null; InPay = false;
            _dBuilt = _dDeals = _dDropped = _dRefund = _dAsk = _dWindows = _dSellBack = 0;
            _dryDay = -1000; _dryDone = false; _testDone = false;
            _forceNeed = _forceFail = _bypassResale = _testLift = false;
            _distRecs = null; _distFrom = null;
            TalkReset();
        }

        internal static void Stumble(string where, Exception e)
        {
            _stAll++; _stDay++;
            int n; _st.TryGetValue(where, out n); _st[where] = n + 1;
            try { if (n == 0 || (n + 1) % 200 == 0) Log.Error("LandSale." + where + " (potkniecie " + (n + 1) + " w tym miejscu)", e); } catch { }
        }

        private static int Today() { return (int)CampaignTime.Now.ToDays; }
        private static string Txt(TaleWorlds.Localization.TextObject t) { try { return t != null ? t.ToString() : "?"; } catch { return "?"; } }
        internal static long Round100(double x) { if (double.IsNaN(x) || x <= 0) return 0; return (long)(Math.Round(x / 100.0, MidpointRounding.AwayFromZero) * 100); }

        // ============================================================ oferta
        internal sealed class Offer
        {
            public int Kind; public string Id = "", Name = "", DistId = "";
            public Settlement St;            // wies gry (wioska poboczna, Lordship) albo zamek
            public Clan Seller; public Hero Head;
            public bool Ok; public string Why = "";   // powod po angielsku, gdy nie na sprzedaz
            public double NetDay, SeasonFix = 1, Years, WarF = 1, Premium = 1, Disc, Q, G, R, D; public int Days, Streak; public long Price, Owed;
            public string DiscWhy = "", NeedEn = "", NeedPl = "-", Pl = "", Kinds = "";
            public bool Need;
            // zamek
            public double CastleLr, VilRent, VilVt, Guard, GuardMen, GuardWage; public int Garrison, VilSeller; public bool Seat;
            public object Title;             // tytul BK (Lordship)
            // odkup
            public bool SellBack;
            // recenzja m113
            public Hero Payer;               // wioska poboczna: posiadacz podatku okregu (platnik udzialu; rycerz BK - glowa rodu sprzedaje)
            public double OwnDues, DuesRate; // udzialy wiosek pobocznych gracza placone przez sprzedawce (dziennie) i stawka powinnosci korony od ziemi
            public string FixNote = "-";     // poprawka pory roku (log)
            public bool Hide;                // menu: powod, ktorego gracz nie zmieni (ustawienia, brak tytulu, BK, juz gracza) - opcja ukryta
            public int TestRel = int.MinValue; public Kingdom TestJoin;   // test n36 (_testLift): prog stosunku do spelnienia i krolestwo sprzedawcy do wasalstwa
            public int Rel, RelMin; public double YearsRel;              // n36 cena wedlug stosunku: stosunek glowy do gracza, prog rodzaju, lata z krzywej (przed wojna i upustem)
            public double YearsPaid;                                     // recenzja n36: odkup - lata, ktore gracz zaplacil za te wioske (PaidYears; sufit lat odkupu)
            public string Sig { get { return Kind + ":" + Id + ":" + Price; } }
        }

        internal static string He(Hero h) { return h != null && h.IsFemale ? "she" : "he"; }
        internal static string His(Hero h) { return h != null && h.IsFemale ? "her" : "his"; }
        internal static string Him(Hero h) { return h != null && h.IsFemale ? "herself" : "himself"; }

        /// <summary>Recenzja m113: udzialy dnia wiosek pobocznych GRACZA w okregu v, ktore dzis placi payer (po przejsciu podatku okregu do gracza przestaja plynac - F3).
        /// Nie rzuca.</summary>
        private static double OwnDuesIn(Village v, Hero payer)
        {
            double own = 0;
            try
            {
                if (v == null || v.Settlement == null || payer == null || SideVillageFief.PayerOf(v) != payer) return 0;
                foreach (var w in SideVillageFief.Owned())
                    if (w != null && string.Equals(w.Dist, v.Settlement.StringId, StringComparison.Ordinal)) { string y; own += Math.Max(0, SideVillageFief.DueFor(v, w.Uid, out y)); }
                // recenzja n36 (M3): DueFor to dzien dzisiejszy (zima, spustoszenie) - na podstawe roku jak NetDay (YearMean), ta sama poprawka co cena wioski
                if (own > 0) { string fn; own *= SideYearFix(v, v.Settlement, out fn); }
            }
            catch (Exception e) { Stumble("OwnDuesIn", e); }
            return own;
        }

        /// <summary>Recenzja m113 (n36 M4): powinnosci korony od ziemi wedlug KUPUJACEGO (po kupnie to on placi je z tego dochodu) - NetDay netto. Rod panujacy, najemnik
        /// i rod bez krolestwa nie placa. Wioska poboczna tylko z kluczem SideVillageDuesInLordD i podstawa "ziemia" (CrownIncome.On i CrownDuesFromLand) - inaczej
        /// jej wyplaty nie sa w podstawie powinnosci. Odkup: kupujacy = o.Seller. Raz w Price dla kazdego rodzaju. Nie rzuca.</summary>
        private static void CrownDues(Offer o)
        {
            o.DuesRate = 0;
            try
            {
                var s = Settings.Current;
                Clan buyer = o.SellBack ? o.Seller : Clan.PlayerClan;
                var k = buyer != null ? buyer.Kingdom : null;
                if (s == null || k == null || k.RulingClan == buyer || buyer.IsUnderMercenaryService) return;
                if (o.Kind == KSide && !(s.SideVillageDuesInLordD && CrownIncome.On && s.CrownDuesFromLand)) return;
                o.DuesRate = Math.Max(0, Math.Min(0.5, KingdomTreasury.DuesRateOf(k)));
                o.NetDay *= 1 - o.DuesRate;
            }
            catch (Exception e) { o.DuesRate = 0; Stumble("CrownDues", e); }
        }

        /// <summary>Recenzja n36 (M1): poprawka dzisiejszej naleznosci wioski pobocznej okregu v na srednia roku. Pelny rok w ksiedze (LandValue.SeasonFixFullYear) - pomiar;
        /// inaczej model: rok (jedna pora z czterech to zima z cieciem tej wsi) / dzis (VillageCrafts.Now: zima x spustoszenie x rece wojny - to samo, co w DueFor).
        /// Nigdy oba naraz (zima liczona raz). clamp 0.25-4. Nie rzuca.</summary>
        private static double SideYearFix(Village v, Settlement st, out string note)
        {
            try
            {
                double f; if (LandValue.SeasonFixFullYear(st, out f, out note)) return f;
                double now = Math.Max(0.05, VillageCrafts.Now(v));
                double yy = 1.0 - 0.25 * WinterBite.VillageCutInWinter(v);
                f = Math.Max(0.25, Math.Min(4.0, yy / now));
                note = f.ToString("0.###", Inv) + " (model: rok " + yy.ToString("0.###", Inv) + " / dzis " + now.ToString("0.###", Inv) + "; " + note + ")";
                return f;
            }
            catch (Exception e) { Stumble("SideYearFix", e); note = "1 (blad)"; return 1; }
        }

        // ------------------------------------------------------------ wioski ukladu wedlug okregu (raz na liste ksiegi)
        private static Dictionary<string, List<MapVillageRecord>> _distRecs; private static object _distFrom;
        internal static List<MapVillageRecord> RecsOf(string distId)
        {
            var l = VillageBook.LayoutRecords();
            if (l == null || distId == null) return null;
            if (_distRecs == null || !ReferenceEquals(_distFrom, l))
            {
                var d = new Dictionary<string, List<MapVillageRecord>>(StringComparer.Ordinal);
                foreach (var r in l)
                {
                    if (r == null || string.IsNullOrEmpty(r.Uid) || string.IsNullOrEmpty(r.VillageId)) continue;
                    List<MapVillageRecord> x; if (!d.TryGetValue(r.VillageId, out x)) { x = new List<MapVillageRecord>(); d[r.VillageId] = x; }
                    x.Add(r);
                }
                _distRecs = d; _distFrom = l;
            }
            List<MapVillageRecord> y; return _distRecs.TryGetValue(distId, out y) ? y : null;
        }

        // ------------------------------------------------------------ Z1
        internal static bool NeverSeller(Clan c, bool castle)
        {
            if (c == null || c == Clan.PlayerClan || c.IsEliminated || c.IsBanditFaction || c.IsMinorFaction || c.IsUnderMercenaryService) return true;
            if (CrownGifts.IsWatch(c.Kingdom) || CrownGifts.IsFreeFolk(c.Kingdom)) return true;   // Straz, Wolni Ludzie - rody bez zoldu (b.Zero)
            if (castle && c.Kingdom != null && c.Kingdom.RulingClan == c) return true;
            return false;
        }

        private static string HouseOf(Clan c) { try { return c != null ? MapVillagesView.HouseName(c) : "a house"; } catch { return "a house"; } }
        private static string NameOf(Hero h) { return h != null && h.Name != null ? h.Name.ToString() : "its lord"; }

        private static bool AtWarMajor(IFaction f)
        {
            if (f == null) return false;
            var l = f.FactionsAtWarWith;
            if (l != null) for (int i = 0; i < l.Count; i++) { var o = l[i]; if (o != null && o.IsKingdomFaction) return true; }
            return false;
        }

        // ------------------------------------------------------------ Z2
        /// <summary>Potrzeba rodu c dla rodzaju kind (Z2). why - slowami gracza; pl - po polsku do logu. Autotest moze wymusic potrzebe (linia).</summary>
        private static bool NeedFor(Clan c, int kind, Offer o)
        {
            var s = Settings.Current;
            double G, R, D; int streak;
            ClanBudget.NeedOf(c, out G, out R, out D, out streak);
            o.G = G; o.R = R; o.D = D; o.Streak = streak;
            o.Owed = DebtLadder.OwedOf(c);
            bool n1 = DebtLadder.IsDebtor(c), n2 = DebtLadder.RansomDebtOf(c) > 0, n3 = G < R, n3h = G < 0.5 * R, n4 = streak >= Math.Max(1, s.BudgetHysteresisDays);
            var en = new List<string>(); var pl = new List<string>();
            if (n1) { en.Add("my house owes " + o.Owed.ToString("N0", Inv) + " to the Iron Bank and others, and the creditors will not wait"); pl.Add("N1 dluznik (" + o.Owed + ")"); }
            if (n2) { en.Add("we still owe ransoms"); pl.Add("N2 okup"); }
            if (n3) { en.Add("our chest holds less than a house must keep for war"); pl.Add("N3 G " + G.ToString("0", Inv) + " < R " + R.ToString("0", Inv)); }
            if (n4) { en.Add("we pay more men than our income bears"); pl.Add("N4 seria " + streak); }
            string stage = kind == KCastle ? DebtLadder.StageOf(c) : null;
            bool deep = DeepDebt(o.Owed, D);
            bool need = NeedKind(kind, n1, n2, n3h, n4, stage, deep);
            if (kind == KCastle) pl.Add("szczebel " + stage + ", gleboki dlug " + (deep ? "tak" : "nie") + " (" + o.Owed + " >= " + Math.Max(1, s.LandDeepDebtDays) + " x D " + D.ToString("0.#", Inv) + ")");
            if (!need && _forceNeed) { need = true; pl.Add("POTRZEBA WYMUSZONA (autotest)"); }
            o.Need = need;
            o.NeedEn = en.Count > 0 ? Cap(string.Join("; ", en.ToArray())) + "." : "";
            o.NeedPl = pl.Count > 0 ? string.Join(", ", pl.ToArray()) : "bez potrzeby";
            return need;
        }

        /// <summary>Recenzja m113: gleboki dlug (zamek) - dochod znany (D > 0) i dlug co najmniej LandDeepDebtDays dob dochodu.</summary>
        private static bool DeepDebt(long owed, double D) { var s = Settings.Current; return D > 0 && owed >= Math.Max(1, s != null ? s.LandDeepDebtDays : 91) * D; }

        /// <summary>Z2: potrzeba wedlug rodzaju (recenzja: wioska - N1, N2, G < 0.5 R albo N4; Lordship - N1, N2, G < 0.5 R; zamek - zajecie, wyprzedaz albo N1 z glebokim dlugiem).</summary>
        private static bool NeedKind(int kind, bool n1, bool n2, bool n3h, bool n4, string stage, bool deep)
        {
            if (kind == KSide) return n1 || n2 || n3h || n4;
            if (kind == KLord) return n1 || n2 || n3h;
            return stage == "zajecie" || stage == "wyprzedaz" || (n1 && deep);
        }

        private static string Cap(string s) { return string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1); }

        private static string CooldownWhy(Clan c, int kind)
        {
            var s = Settings.Current;
            Cd cd;
            if (c == null || c.StringId == null || !_cd.TryGetValue(c.StringId, out cd)) return null;
            int t = Today();
            if (t - cd.LastSale < Math.Max(0, s.LandSellerCooldownDays)) return HouseOf(c) + " sold land only " + (t - cd.LastSale) + " days ago and will not sell again so soon.";
            if (kind == KCastle && t - cd.LastCastle < Math.Max(0, s.LandCastleCooldownDays)) return HouseOf(c) + " parted with a castle this year already.";
            return null;
        }

        // ------------------------------------------------------------ Z3 (wspolne)
        private static string CommonGate(Hero head, int relMin)
        {
            var me = Hero.MainHero; var pc = Clan.PlayerClan;
            if (me == null || pc == null || pc.Leader != me) return "Only the head of your house can buy land.";
            if (head == null || !head.IsAlive) return "There is no one to sell it.";
            if (head.MapFaction != null && pc.MapFaction != null && FactionManager.IsAtWarAgainstFaction(head.MapFaction, pc.MapFaction)) return "You are at war with " + NameOf(head) + "'s realm.";
            if (head.IsPrisoner) return NameOf(head) + " is a prisoner - " + He(head) + " cannot sell land now.";
            var mp = head.PartyBelongedTo;
            if (mp != null && mp.MapEvent != null) return NameOf(head) + " is in battle.";
            int rel = (int)head.GetRelationWithPlayer();
            if (rel < relMin) return NameOf(head) + " does not think well enough of you to sell you land (relation " + rel + ", needs " + relMin + ").";
            return null;
        }

        // ------------------------------------------------------------ Z4
        /// <summary>Z3/Z4: prog stosunku rodzaju (ponizej nikt nie sprzedaje - CommonGate) i poczatek krzywej lat.</summary>
        internal static int RelMinOf(int kind)
        {
            var s = Settings.Current;
            return kind == KCastle ? s.LandRelationCastle : kind == KLord ? s.LandRelationLordship : s.LandRelationSideVillage;
        }

        /// <summary>n36 cena wedlug stosunku (Jeff 24.10: "daj cene zalezna od relacji miedzy 14 a 20 lat, jesli relacja jest za niska nikt nie sprzeda, im wyzsza
        /// tym lepsza cena"). Lata przed wojna, upustem i premia. Kupno: LandYearsWorst (20) przy progu rodzaju, liniowo do LandYearsOfPurchase (14 - najlepsze)
        /// przy progu + LandYearsRelationSpan (60) i wyzej; ponizej progu oferty nie ma (brama), tu najgorsze. Odkup odwrotnie - przyjaciel placi wiecej:
        /// 14 przy progu (i ponizej), 20 przy progu + 60 i wyzej (inaczej niski stosunek nagradzalby gracza); sufit odkupu (PaidYears) - w Price. Nie rzuca.</summary>
        internal static double YearsFor(int rel, int relMin, bool sellBack)
        {
            var s = Settings.Current;
            double best = Math.Max(1, s.LandYearsOfPurchase), worst = Math.Max(best, s.LandYearsWorst);
            double k = Math.Max(0, Math.Min(1, (rel - (double)relMin) / Math.Max(1, s.LandYearsRelationSpan)));
            return sellBack ? best + (worst - best) * k : worst - (worst - best) * k;
        }

        private static void Price(Offer o, Clan debtor, IFaction holdingFaction)
        {
            var s = Settings.Current;
            CrownDues(o);   // n36 (M4): powinnosci korony wedlug kupujacego - raz, dla kazdego rodzaju
            // n36: stosunek glowy, ktora decyduje (sprzedawca; przy odkupie - kupujacy), do gracza - ta sama liczba co brama CommonGate
            o.RelMin = RelMinOf(o.Kind);
            o.Rel = o.Head != null ? (int)o.Head.GetRelationWithPlayer() : o.RelMin;
            o.YearsRel = YearsFor(o.Rel, o.RelMin, o.SellBack);
            // recenzja n36: odkup nie wiecej lat, niz gracz zaplacil za te wioske - inaczej kupno u przyjaciela za 14 lat i odkup przez tego samego za 20 = zysk co 182 doby
            if (o.SellBack) { o.YearsPaid = PaidYears(o.Id); if (o.YearsRel > o.YearsPaid) o.YearsRel = o.YearsPaid; }
            double years = o.YearsRel;
            o.WarF = AtWarMajor(holdingFaction) ? Math.Max(0.1f, Math.Min(1f, s.LandYearsAtWar)) : 1.0;
            double ye = years * o.WarF;
            double floor = Math.Max(1, s.LandYearsFloor);
            if (ye < floor) ye = floor;
            double G = o.G, D = o.D; long owed = o.Owed;
            if (o.SellBack)
            {
                double r, dd; int st;
                ClanBudget.NeedOf(debtor, out G, out r, out dd, out st);
                G = ClanIncomeBook.FamilyGoldOf(debtor); D = DebtLadder.DebtD(debtor); owed = DebtLadder.OwedOf(debtor);
            }
            o.Q = owed / Math.Max(1.0, G + 91.0 * Math.Max(0, D));
            double from = s.LandDebtRatioFrom, full = Math.Max(from + 0.01f, s.LandDebtRatioFull);
            double k = owed > 0 ? Math.Max(0, Math.Min(1, (o.Q - from) / (full - from))) : 0;
            o.Disc = Math.Max(0, Math.Min(0.9f, s.LandDebtDiscountMax)) * k;
            if (ye * (1 - o.Disc) < floor) o.Disc = Math.Max(0, 1 - floor / ye);
            o.Years = ye;
            o.DiscWhy = o.Disc > 0 ? (o.SellBack ? "your house" : HouseOf(debtor)) + " owes " + owed.ToString("N0", Inv) + ", " + o.Q.ToString("0.00", Inv) + " times its means (gold and 91 days of income)"
                                   : (owed > 0 ? "the debts are not large against the house's means" : "the house has no debts");
            int dpy = IronBank.DaysPerYearNow();
            double raw = o.NetDay * dpy * ye * o.Premium * (1 - o.Disc);
            o.Price = Round100(raw);
            o.Pl = "NetDay " + o.NetDay.ToString("0.####", Inv) + (o.OwnDues > 0 ? " (po udzialach wiosek gracza " + o.OwnDues.ToString("0.##", Inv) + ")" : "")
                   + (o.DuesRate > 0 ? " (po powinnosciach korony " + (100 * o.DuesRate).ToString("0.#", Inv) + "%)" : "")
                   + " x " + dpy + " x lata " + ye.ToString("0.####", Inv) + " (stosunek " + o.Rel + ", prog " + o.RelMin + (o.SellBack ? ", odkup: przyjaciel placi wiecej, nie wiecej niz zaplacone " + o.YearsPaid.ToString("0.##", Inv) : "")
                   + " -> lata " + o.YearsRel.ToString("0.##", Inv) + ")" + (o.WarF < 1 ? " (wojna x" + o.WarF.ToString("0.##", Inv) + ")" : "")
                   + " x premia " + o.Premium.ToString("0.##", Inv) + " x (1 - upust " + o.Disc.ToString("0.####", Inv) + ", q " + o.Q.ToString("0.###", Inv) + ") = " + raw.ToString("0.##", Inv)
                   + " -> round100 " + o.Price;
        }

        /// <summary>Lata zwrotu ceny z dochodu (do okna).</summary>
        internal static double Payback(Offer o) { return o.NetDay > 0 ? o.Price / (o.NetDay * IronBank.DaysPerYearNow()) : 0; }

        // ============================================================ budowa ofert
        /// <summary>Oferta wioski pobocznej uid (Z1-Z4). Zawsze zwraca obiekt; Ok - na sprzedaz. Nie rzuca.</summary>
        internal static Offer BuildSide(string uid)
        {
            var o = new Offer { Kind = KSide, Id = uid ?? "", Kinds = "side village" };
            _dBuilt++;
            try
            {
                var s = Settings.Current;
                if (!On || !s.LandSalesSideVillages) { o.Why = "Side villages are not for sale (mod settings)."; o.Hide = true; return o; }
                var rec = SideVillageVisit.Rec(uid);
                var st = rec != null ? Settlement.Find(rec.VillageId) : null;
                var v = st != null ? st.Village : null;
                if (v == null) { o.Why = "This village is not known."; o.Hide = true; return o; }
                o.St = st; o.DistId = st.StringId; o.Name = string.IsNullOrEmpty(rec.Name) ? uid : rec.Name;
                var lord = v.Bound != null ? v.Bound.OwnerClan : null;
                // recenzja m113: sprzedawca = rod platnika udzialu (PayerOf) - jedna regula dla sprzedazy i dla tego, kto potem placi
                Hero h = SideVillageFief.PayerOf(v);
                o.Payer = h;
                if (SideVillageFief.IsMine(uid)) { o.Why = "This village is yours already."; o.Hide = true; return o; }
                bool keyOn = s.SideVillagePayerIsTaxHolder;
                if ((h != null && (h == Hero.MainHero || h.Clan == Clan.PlayerClan)) || ((!keyOn || h == null) && lord == Clan.PlayerClan)) { o.Why = "Its tax is yours already."; o.Hide = true; return o; }
                if (h == null || !h.IsAlive || h.Clan == null) { o.Why = "No lord takes its tax - there is no one to sell it."; return o; }
                Hero head = h.Clan.Leader;   // rycerz BK - sprzedaje glowa jego rodu (pieniadze, potrzeba i wierzyciele rodu), podatek dalej bierze rycerz
                o.Head = head; o.Seller = h.Clan;
                if (head == null || !head.IsAlive) { o.Why = "Its tax goes to " + NameOf(h) + ", whose house has no head to sell it."; return o; }
                if (NeverSeller(h.Clan, false)) { o.Why = HouseOf(h.Clan) + " does not sell land."; return o; }
                string g = CommonGate(head, s.LandRelationSideVillage);
                if (g != null) { o.Why = g; return o; }
                if (v.VillageState == Village.VillageStates.BeingRaided || v.VillageState == Village.VillageStates.Looted) { o.Why = Txt(st.Name) + " lies raided - nobody sells land now."; return o; }
                int state = PartState(v, uid);
                if (state != VillageBook.SStanding && state != VillageBook.SDamaged) { o.Why = state < 0 ? "This village is not known to the books yet." : "This village lies burned or empty."; return o; }
                string why;
                double due = SideVillageFief.DueFor(v, uid, out why);
                o.SeasonFix = SideYearFix(v, st, out o.FixNote);   // n36 (M1): srednia roku - pomiar przy pelnym roku, inaczej model zimy
                o.Days = Math.Max(1, LandValue.SlotDays(st));
                o.NetDay = due * o.SeasonFix;
                if (!(o.NetDay > 0)) { o.Why = "It earns its lord nothing today - no price can be set."; return o; }
                if (!NeedFor(h.Clan, KSide, o)) { o.Why = HouseOf(h.Clan) + " is not in need - a house sells land only when it must."; return o; }
                string cw = CooldownWhy(h.Clan, KSide); if (cw != null) { o.Why = cw; return o; }
                o.Premium = 1;
                Price(o, h.Clan, st.MapFaction);
                if (o.Price <= 0 || o.Price > int.MaxValue) { o.Why = "No price can be set."; return o; }
                o.Ok = true;
            }
            catch (Exception e) { o.Ok = false; o.Why = "Not known today."; Stumble("BuildSide", e); }
            return o;
        }

        private static int PartState(Village v, string uid)
        {
            try
            {
                var pv = new List<VillageBook.PartView>();
                if (!VillageBook.PartsOf(v, pv)) return -1;
                foreach (var p in pv) if (p != null && !p.Main && string.Equals(p.Uid, uid, StringComparison.Ordinal)) return p.State;
            }
            catch (Exception e) { Stumble("PartState", e); }
            return -1;
        }

        private static string VassalGate(Clan seller, IFaction holding)
        {
            var pc = Clan.PlayerClan;
            var k = pc != null ? pc.Kingdom : null;
            if (k == null || pc.IsUnderMercenaryService) return "Only a vassal of the realm can hold its titles - you are not one.";
            if (seller == null || seller.Kingdom != k || holding != k) return "Only a vassal of the same realm can buy it.";
            return null;
        }

        /// <summary>Z3 Lordship i zamku: CommonGate, potem VassalGate. Test n36 (_testLift, tylko autotest ziemi): bramy, ktore gracz zmieni w grze - stosunek
        /// ze sprzedawca (o.TestRel = prog) i wasal krolestwa sprzedawcy (o.TestJoin) - nie zamykaja oferty; test je spelnia i buduje oferte od nowa bez _testLift.
        /// Zostaja: wojna, niewola, bitwa, ziemia poza krolestwem sprzedawcy, gracz wladca albo wasal innego krolestwa z lennami.</summary>
        private static string LiftGates(Offer o, Hero head, int relMin, Clan seller, IFaction holding)
        {
            if (!_testLift) return CommonGate(head, relMin) ?? VassalGate(seller, holding);
            string g = CommonGate(head, int.MinValue);
            if (g != null) return g;
            if ((int)head.GetRelationWithPlayer() < relMin) o.TestRel = relMin;
            g = VassalGate(seller, holding);
            if (g == null) return null;
            var pc = Clan.PlayerClan; var k = seller != null ? seller.Kingdom : null;
            if (pc == null || k == null || k.IsEliminated || holding != k) return g;
            if (pc.Kingdom != null && pc.Kingdom != k && !pc.IsUnderMercenaryService && (pc.Kingdom.RulingClan == pc || pc.Settlements.Count > 0)) return g;
            o.TestJoin = k;
            return null;
        }

        /// <summary>B2: oferta tytulu Lordship wsi gry stId (Z1-Z4). Nie rzuca.</summary>
        internal static Offer BuildLord(string stId)
        {
            var o = new Offer { Kind = KLord, Id = stId ?? "", Kinds = "lordship" };
            _dBuilt++;
            try
            {
                var s = Settings.Current;
                if (!On || !s.LandSalesLordships) { o.Why = "Lordships are not for sale (mod settings)."; o.Hide = true; return o; }
                var st = Settlement.Find(stId);
                var v = st != null ? st.Village : null;
                if (v == null) { o.Why = "This village is not known."; o.Hide = true; return o; }
                o.St = st; o.DistId = st.StringId; o.Name = Txt(st.Name);
                if (!LandBk.Ready) { o.Why = "It holds no title that could pass by sale."; o.Hide = true; return o; }
                var t = LandBk.Title(st);
                var dj = LandBk.DeJure(t);
                o.Title = t; o.Head = dj; o.Seller = dj != null ? dj.Clan : null;
                if (t == null) { o.Why = "It holds no title that could pass by sale."; o.Hide = true; return o; }
                if (dj == Hero.MainHero || (dj != null && dj.Clan == Clan.PlayerClan)) { o.Why = "Its lordship is yours already."; o.Hide = true; return o; }
                if (dj == null || !dj.IsAlive || dj.Clan == null || dj.Clan.Leader != dj) { o.Why = "Its lordship is held by " + NameOf(dj) + ", who cannot sell for " + His(dj) + " house."; return o; }
                if (NeverSeller(dj.Clan, false)) { o.Why = HouseOf(dj.Clan) + " does not sell land."; return o; }
                string g = LiftGates(o, dj, s.LandRelationLordship, dj.Clan, st.MapFaction);
                if (g != null) { o.Why = g; return o; }
                if (LandBk.DeJureCount(dj) < 2) { o.Why = NameOf(dj) + " holds no other title - " + He(dj) + " will not sell " + His(dj) + " last one."; return o; }
                if (v.VillageState != Village.VillageStates.Normal) { o.Why = o.Name + " lies raided - nobody sells land now."; return o; }
                int d;
                o.NetDay = LandValue.YearMean(st, LandValue.VT, out d); o.Days = d;
                if (d < 28) { o.Why = "Its tax has been watched only " + d + (d == 1 ? " day" : " days") + " - no fair price yet (ask again later)."; return o; }
                if (!(o.NetDay > 0)) { o.Why = "Its tax is not known yet - no price can be set."; return o; }
                // recenzja m113: udzialy wiosek pobocznych gracza w tym okregu placi dzis sprzedawca z tego podatku - po kupnie przestaja plynac (F3), cena liczy tylko reszte
                o.OwnDues = OwnDuesIn(v, dj);
                o.NetDay = Math.Max(0, o.NetDay - o.OwnDues);
                if (!(o.NetDay > 0)) { o.Why = "Its tax would bring you nothing beyond what your own villages there already pay you."; return o; }
                if (!NeedFor(dj.Clan, KLord, o)) { o.Why = HouseOf(dj.Clan) + " is not in need - a house sells land only when it must."; return o; }
                string cw = CooldownWhy(dj.Clan, KLord); if (cw != null) { o.Why = cw; return o; }
                o.Premium = 1;
                Price(o, dj.Clan, st.MapFaction);
                if (o.Price <= 0 || o.Price > int.MaxValue) { o.Why = "No price can be set."; return o; }
                o.Ok = true;
            }
            catch (Exception e) { o.Ok = false; o.Why = "Not known today."; Stumble("BuildLord", e); }
            return o;
        }

        /// <summary>B3: oferta zamku stId (Z1-Z4). Nie rzuca.</summary>
        internal static Offer BuildCastle(string stId)
        {
            var o = new Offer { Kind = KCastle, Id = stId ?? "", Kinds = "castle" };
            _dBuilt++;
            try
            {
                var s = Settings.Current;
                if (!On || !s.LandSalesCastles) { o.Why = "Castles are not for sale (mod settings)."; return o; }
                var st = Settlement.Find(stId);
                if (st == null || !st.IsCastle || st.Town == null) { o.Why = "This castle is not known."; return o; }
                o.St = st; o.DistId = st.StringId; o.Name = Txt(st.Name);
                var c = st.Town.OwnerClan; var h = c != null ? c.Leader : null;
                o.Seller = c; o.Head = h;
                if (c == Clan.PlayerClan) { o.Why = "This castle is yours already."; return o; }
                if (h == null || !h.IsAlive) { o.Why = "There is no one to sell it."; return o; }
                if (NeverSeller(c, true)) { o.Why = c != null && c.Kingdom != null && c.Kingdom.RulingClan == c ? "The crown does not sell its castles." : HouseOf(c) + " does not sell land."; return o; }
                if (Array.IndexOf(Twins, st.StringId) >= 0) { o.Why = "This castle is not for sale."; return o; }
                // recenzja m113: twierdza trzymana tymczasowo (glosowanie krolestwa o panu po zdobyciu / wygasnieciu rodu) - zmiana pana skasowalaby decyzje krolestwa
                if (st.Town.IsOwnerUnassigned) { o.Why = "The realm has not yet decided who holds this castle - it cannot be sold."; return o; }
                string g = LiftGates(o, h, s.LandRelationCastle, c, st.MapFaction);
                if (g != null) { o.Why = g; return o; }
                int others = 0;
                var fs = c.Fiefs;
                if (fs != null) for (int i = 0; i < fs.Count; i++) if (fs[i] != null && fs[i].Settlement != st) others++;
                if (others < 1) { o.Why = "It is the last seat of " + HouseOf(c) + " - no house sells its only home."; return o; }
                if (st.SiegeEvent != null || (st.Party != null && st.Party.MapEvent != null)) { o.Why = "The castle is under siege or in battle."; return o; }
                if (LandBk.Ready)
                {
                    var t = LandBk.Title(st);
                    if (t == null) { o.Why = "It holds no title that could pass by sale."; return o; }
                    if (LandBk.DeJure(t) != h) { o.Why = "Its title is held by " + NameOf(LandBk.DeJure(t)) + " - its lord cannot sell it."; return o; }
                    foreach (var bv in st.BoundVillages)
                        if (bv != null && bv.Settlement != null && LandBk.Title(bv.Settlement) == null) { o.Why = Txt(bv.Settlement.Name) + " holds no title that could pass by sale - the castle cannot change hands."; return o; }
                }
                // Z4 dochod netto zamku (recenzja m113: kazda liczona czesc z co najmniej 28 dob pomiaru)
                int d;
                o.CastleLr = LandValue.YearMean(st, LandValue.LR, out d); o.Days = d;
                int dmin = d;
                o.VilRent = 0; o.VilVt = 0; o.VilSeller = 0; o.OwnDues = 0;
                foreach (var bv in st.BoundVillages)
                {
                    if (bv == null || bv.Settlement == null) continue;
                    int dv;
                    o.VilRent += LandValue.YearMean(bv.Settlement, LandValue.LR, out dv); dmin = Math.Min(dmin, dv);
                    Hero vh = LandBk.Ready ? LandBk.DeJure(LandBk.Title(bv.Settlement)) : h;
                    if (vh == h) { o.VilVt += LandValue.YearMean(bv.Settlement, LandValue.VT, out dv); o.VilSeller++; dmin = Math.Min(dmin, dv); }
                    o.OwnDues += OwnDuesIn(bv, h);   // udzialy wiosek gracza, ktore placi sprzedawca - po kupnie przestaja plynac
                }
                if (dmin < 28) { o.Why = "Its income has been watched only " + dmin + (dmin == 1 ? " day" : " days") + " - no fair price yet (ask again later)."; return o; }
                var gp = st.Town.GarrisonParty;
                o.Garrison = gp != null && gp.MemberRoster != null ? gp.MemberRoster.TotalManCount : 0;
                double norm = CrownRents.CastleNormMen(st.MapFaction as Kingdom);
                if (!(norm > 0)) norm = o.Garrison;
                double wage = 0;
                if (gp != null && gp.MemberRoster != null && gp.MemberRoster.TotalRegulars > 0) wage = (double)Math.Max(0, gp.TotalWage) / gp.MemberRoster.TotalRegulars;
                if (!(wage > 0)) wage = Tier2Wage(st.Culture);
                o.GuardMen = norm; o.GuardWage = wage;
                o.Guard = Math.Max(0f, Math.Min(1f, s.LandCastleGuardShare)) * norm * wage;
                o.NetDay = o.CastleLr + o.VilRent + o.VilVt - o.Guard - o.OwnDues;
                if (!(o.NetDay > 0)) { o.Why = "It earns nothing above the cost of its guard - no price can be set."; return o; }
                if (!NeedFor(c, KCastle, o)) { o.Why = HouseOf(c) + " is not deep in debt - a house sells a castle only when it must."; return o; }
                string cw = CooldownWhy(c, KCastle); if (cw != null) { o.Why = cw; return o; }
                o.Seat = st == c.HomeSettlement || CanonSeat(c, st);
                o.Premium = o.Seat ? Math.Max(1f, s.LandSeatPremium) : Math.Max(1f, s.LandCastlePremium);
                Price(o, c, st.MapFaction);
                if (o.Price <= 0 || o.Price > int.MaxValue) { o.Why = "No price can be set."; return o; }
                o.Ok = true;
            }
            catch (Exception e) { o.Ok = false; o.Why = "Not known today."; Stumble("BuildCastle", e); }
            return o;
        }

        private static bool CanonSeat(Clan c, Settlement st)
        {
            try { var h = CanonHouses.OfClan(c); return h != null && CanonHouses.SeatIs(h, st.StringId); }
            catch { return false; }
        }

        private static double Tier2Wage(CultureObject cul)
        {
            try
            {
                var b = cul != null ? cul.BasicTroop : null;
                CharacterObject t2 = b != null && b.UpgradeTargets != null && b.UpgradeTargets.Length > 0 ? b.UpgradeTargets[0] : b;
                if (t2 != null) return Math.Max(1, Campaign.Current.Models.PartyWageModel.GetCharacterWage(t2));
            }
            catch (Exception e) { Stumble("Tier2Wage", e); }
            return 6;
        }

        /// <summary>Oferta wedlug rodzaju i id (ponowne sprawdzenie w kolejce).</summary>
        internal static Offer Build(int kind, string id) { return kind == KSide ? BuildSide(id) : kind == KLord ? BuildLord(id) : BuildCastle(id); }

        /// <summary>Wszystko, co glowa rodu h moglaby sprzedac (rozmowa): wioski poboczne okregow, w ktorych h bierze podatek, jego tytuly Lordship, zamki rodu.</summary>
        internal static List<Offer> OffersOf(Hero h)
        {
            var l = new List<Offer>();
            if (h == null || h.Clan == null) return l;
            try
            {
                var s = Settings.Current;
                var c = h.Clan;
                if (s.LandSalesSideVillages)
                    foreach (var v in Village.All)
                    {
                        if (v == null || v.Settlement == null) continue;
                        Hero ah = SideVillageFief.PayerOf(v);   // recenzja m113: ta sama regula co BuildSide; rycerz rodu - sprzedaje glowa rodu
                        if (ah == null || ah.Clan != c) continue;
                        var recs = RecsOf(v.Settlement.StringId);
                        if (recs == null) continue;
                        foreach (var r in recs) if (!SideVillageFief.IsMine(r.Uid)) l.Add(BuildSide(r.Uid));
                    }
                if (s.LandSalesLordships && LandBk.Ready)
                    foreach (var t in LandBk.AllDeJure(h))
                    {
                        var f = LandBk.Fief(t);
                        if (f != null && f.IsVillage) l.Add(BuildLord(f.StringId));
                    }
                if (s.LandSalesCastles && c.Fiefs != null)
                    for (int i = 0; i < c.Fiefs.Count; i++) { var f = c.Fiefs[i]; if (f != null && f.IsCastle && f.Settlement != null) l.Add(BuildCastle(f.Settlement.StringId)); }
            }
            catch (Exception e) { Stumble("OffersOf", e); }
            return l;
        }

        /// <summary>Rozmowa: glowa rodu, ktora moglaby cos sprzedac (tanio: rod z lennami albo tytulami BK). Nie rzuca.</summary>
        internal static bool CouldSell(Hero h)
        {
            try
            {
                if (!On || h == null || h.Clan == null || h.Clan.Leader != h || h == Hero.MainHero || NeverSeller(h.Clan, false)) return false;
                if (h.Clan.Fiefs != null && h.Clan.Fiefs.Count > 0) return true;
                return LandBk.Ready && LandBk.DeJureCount(h) > 0;
            }
            catch { return false; }
        }

        // ============================================================ Z5: kolejka i wykonanie
        internal static void Queue(Offer o)
        {
            if (o == null) return;
            _queued = o;
            Log.Info(P + "umowa w kolejce - " + KindPl(o.Kind) + " " + o.Name + " (" + o.Id + "), sprzedawca " + NameOf(o.Head) + " (" + (o.Seller != null ? o.Seller.StringId : "-") + "), cena " + o.Price + " (stosunek " + o.Rel + ", prog " + o.RelMin + ", lata " + o.YearsRel.ToString("0.##", Inv) + " -> " + o.Years.ToString("0.##", Inv) + ")"
                     + (o.SellBack ? " (ODKUP przez posiadacza podatku)" : "") + ".");
        }

        private static string KindPl(int k) { return k == KSide ? "wioska poboczna" : k == KLord ? "Lordship" : "zamek"; }

        /// <summary>CampaignEvents.TickEvent: umowa z kolejki (bez rozmowy), okno ofert po rozmowie. Nie rzuca.</summary>
        internal static void Tick()
        {
            try
            {
                TalkTick();
                var q = _queued;
                if (q == null) return;
                var cm = Campaign.Current != null ? Campaign.Current.ConversationManager : null;
                if (cm != null && (cm.IsConversationInProgress || cm.IsConversationFlowActive)) return;
                _queued = null;
                if (q.SellBack) ExecuteSellBack(q); else Execute(q);
            }
            catch (Exception e) { _queued = null; InPay = false; Stumble("Tick", e); }
        }

        private static void Drop(Offer q, string en, string pl)
        {
            _dDropped++;
            Log.Info(P + "umowa PRZEPADLA - " + KindPl(q.Kind) + " " + q.Name + " (" + q.Id + "): " + pl + ".");
            Log.Player("The sale of " + q.Name + " fell through: " + en, true);
        }

        private static void Execute(Offer q)
        {
            var me = Hero.MainHero;
            var o = Build(q.Kind, q.Id);   // Z5: wszystko od nowa, z cena
            if (!o.Ok) { Drop(q, o.Why, "warunek juz nie spelniony (" + o.Why + ")"); return; }
            if (o.Price != q.Price || o.Head != q.Head) { Drop(q, "the price is now " + o.Price.ToString("N0", Inv) + ", not " + q.Price.ToString("N0", Inv) + " - ask again.", "cena albo sprzedawca inne (" + q.Price + " -> " + o.Price + ")"); return; }
            if (me == null || me.Gold < o.Price) { Drop(q, "you do not have " + o.Price.ToString("N0", Inv) + " in your purse.", "za malo zlota gracza (" + (me != null ? me.Gold : 0) + " < " + o.Price + ")"); return; }
            var head = o.Head; var seller = o.Seller;
            long w0 = WorldGold(), pg0 = me.Gold, hg0 = head.Gold;
            long owed0 = DebtLadder.OwedOf(seller);
            long cred = 0; string tw = "";
            // (a) recenzja m113: InPay tylko wokol przelewu ceny (sluchacze zdarzen innych modow przy przeniesieniu nie licza sie jako cena ziemi);
            // zaplacone = kiesa gracza mniejsza dokladnie o cene (wyjatek sluchacza PO przelewie nie gubi zaplaty)
            InPay = true;
            try { GiveGoldAction.ApplyBetweenCharacters(me, head, (int)o.Price, true); }
            catch (Exception e) { Stumble("Execute(a)", e); }
            finally { InPay = false; }
            long paid = pg0 - me.Gold;
            if (paid != o.Price)
            {
                long back = paid > 0 ? Refund(head, me, paid, "Execute(zwrot a)") : 0;
                _dRefund++;
                Log.Info(P + "transakcja cofnieta przy platnosci - " + KindPl(o.Kind) + " " + o.Name + " (" + o.Id + "): zaplacono " + paid + " z " + o.Price + ", zwrot " + back
                         + (paid > 0 && back < paid ? ", brak zwrotu " + (paid - back) : "") + " (gracz " + pg0 + " -> " + me.Gold + ", sprzedawca " + hg0 + " -> " + head.Gold + ").");
                Log.Player("The sale of " + o.Name + " could not be completed.", true);
                return;
            }
            // (b) o powodzeniu decyduje stan po przeniesieniu (Transfer zwraca Moved)
            bool moved;
            if (_forceFail) { _forceFail = false; moved = false; tw = "wymuszony blad przeniesienia (autotest)"; }
            else moved = Transfer(o, out tw);
            if (!moved)                                                                                // (c)
            {
                long back = Refund(head, me, o.Price, "Execute(zwrot c)");
                _dRefund++;
                Log.Info(P + "transakcja cofnieta - " + KindPl(o.Kind) + " " + o.Name + " (" + o.Id + "): " + tw + "; zwrot " + back + " z " + o.Price + (back < o.Price ? ", brak zwrotu " + (o.Price - back) : "")
                         + " (gracz " + pg0 + " -> " + me.Gold + ", sprzedawca " + hg0 + " -> " + head.Gold + ").");
                Log.Player("The sale of " + o.Name + " could not be completed - " + (back >= o.Price ? "your " + o.Price.ToString("N0", Inv) + " were given back." : back.ToString("N0", Inv) + " of your " + o.Price.ToString("N0", Inv) + " were given back."), true);
                return;
            }
            try { cred = DebtLadder.LandProceeds(seller, o.Price); }                                   // (d)
            catch (Exception e) { Stumble("Execute(d)", e); }
            DealDone();
            int t = Today();
            Cd cd; if (!_cd.TryGetValue(seller.StringId, out cd)) { cd = new Cd(); _cd[seller.StringId] = cd; }
            cd.LastSale = t; if (o.Kind == KCastle) cd.LastCastle = t;
            AddDeal(o, t);
            _dDeals++;
            long w1 = WorldGold();
            long one, real; bool ib = ClanIncomeBook.OnceToday(seller, out one, out real);
            Log.Info(P + "KUPNO - " + KindPl(o.Kind) + " " + o.Name + " (" + o.Id + ", okreg " + o.DistId + ") od " + NameOf(head) + " (" + seller.StringId + ") za " + o.Price
                     + " | " + o.Pl + " | dochod mierzony " + o.Days + " dob, poprawka pory roku " + (o.Kind == KSide ? o.FixNote : o.SeasonFix.ToString("0.###", Inv))
                     + (o.Payer != null && o.Payer != head ? " | podatek bierze " + NameOf(o.Payer) + " (ten sam rod)" : "") + " | potrzeba: " + o.NeedPl
                     + " | gracz " + pg0 + " -> " + me.Gold + ", glowa sprzedawcy " + hg0 + " -> " + head.Gold + " (wierzycielom " + cred + ", dlug " + owed0 + " -> " + DebtLadder.OwedOf(seller) + ")"
                     + " | pieniadz swiata " + w0 + " -> " + w1 + " (roznica " + (w1 - w0) + ") | jednorazowe sprzedawcy dzis " + (ib ? one + " (od platnikow " + real + ")" : "brak ksiegi") + " - cena poza nimi" + (tw.Length > 0 ? " | " + tw : "") + ".");
            string where = cred > 0 ? " " + Cap(His(head)) + " creditors took " + cred.ToString("N0", Inv) + " of it." : "";
            Log.Player("You bought " + o.Name + " from " + NameOf(head) + " for " + o.Price.ToString("N0", Inv) + "." + where);
            AfterDealMenu(o);
        }

        private static long WorldGold() { try { return MoneyLedger.WorldNow(); } catch { return 0; } }

        /// <summary>Recenzja m113: zwrot from -> to najwyzej tyle, ile from ma (Hero.Gold nie schodzi ponizej 0 - inaczej zwrot tworzylby zloto z niczego); pod InPay.
        /// Zwraca, ile to dostal. Nie rzuca.</summary>
        private static long Refund(Hero from, Hero to, long amount, string where)
        {
            if (from == null || to == null || amount <= 0) return 0;
            long t0 = to.Gold;
            int back = (int)Math.Max(0, Math.Min(amount, (long)from.Gold));
            if (back <= 0) return 0;
            InPay = true;
            try { GiveGoldAction.ApplyBetweenCharacters(from, to, back, true); }
            catch (Exception e) { Stumble(where, e); }
            finally { InPay = false; }
            return to.Gold - t0;
        }

        /// <summary>Recenzja m113: czy ziemia oferty jest juz gracza (stan po przeniesieniu - jedyna miara powodzenia). Nie rzuca.</summary>
        private static bool Moved(Offer o)
        {
            try
            {
                if (o == null) return false;
                if (o.Kind == KSide) return SideVillageFief.IsMine(o.Id);
                if (o.Kind == KLord) return o.Title != null && LandBk.DeJure(o.Title) == Hero.MainHero;
                return o.St != null && o.St.OwnerClan == Clan.PlayerClan;
            }
            catch { return false; }
        }

        /// <summary>Z6: przeniesienie wlasnosci i sprawdzenie. false - nie powiodlo sie (why po polsku). Nie rzuca.</summary>
        private static bool Transfer(Offer o, out string why)
        {
            why = "";
            try
            {
                if (o.Kind == KSide)
                {
                    var rec = SideVillageVisit.Rec(o.Id);
                    var w = SideVillageFief.AddOwned(o.Id, o.DistId, o.Name, o.Price, o.Seller != null ? o.Seller.StringId : "", Today());
                    if (w == null || !SideVillageFief.IsMine(o.Id)) { why = "wioska nie przeszla do gracza"; return false; }
                    why = "wioski gracza " + SideVillageFief.OwnedCount + (rec != null && !string.IsNullOrEmpty(rec.Hamlet) ? " (przysiolek)" : "");
                    return true;
                }
                if (o.Kind == KLord)
                {
                    bool r = LandBk.Inherit(o.Head, Hero.MainHero, o.Title);   // wynik pominiety: BK moze rzucic PO zmianie de iure - decyduje stan
                    if (LandBk.DeJure(o.Title) != Hero.MainHero) { why = "tytul de iure nie jest gracza po InheritTitle" + (r ? "" : " (wyjatek InheritTitle)"); return false; }
                    why = "tytul de iure gracza" + (r ? "" : " (wyjatek InheritTitle PO przeniesieniu)") + ", sprzedawcy zostaje tytulow " + LandBk.DeJureCount(o.Head);
                    return true;
                }
                // zamek
                var st = o.St; var head = o.Head; var seller = o.Seller;
                var gp = st.Town.GarrisonParty;
                int g0 = gp != null && gp.MemberRoster != null ? gp.MemberRoster.TotalManCount : -1;
                string home0s = seller.HomeSettlement != null ? seller.HomeSettlement.StringId : "-", home0p = Clan.PlayerClan.HomeSettlement != null ? Clan.PlayerClan.HomeSettlement.StringId : "-";
                // recenzja m113: wyjatek sluchacza OnSettlementOwnerChanged (inne mody) leci PO ustawieniu pana - decyduje stan, reszta we wlasnym try
                string bx = "";
                try { ChangeOwnerOfSettlementAction.ApplyByBarter(Hero.MainHero, st); }
                catch (Exception e) { Stumble("Transfer(ApplyByBarter)", e); bx = "wyjatek ApplyByBarter " + e.GetType().Name; }
                if (st.OwnerClan != Clan.PlayerClan) { why = "ApplyByBarter - zamek nie jest gracza" + (bx.Length > 0 ? " (" + bx + ")" : ""); return false; }
                try
                {
                    int k = 0, m = 0, all = 0;
                    if (LandBk.Ready)
                    {
                        var ct = LandBk.Title(st);
                        all++;
                        if (LandBk.DeJure(ct) == Hero.MainHero) k++;
                        else if (LandBk.DeJure(ct) == head && LandBk.Inherit(head, Hero.MainHero, ct)) m++;
                        foreach (var bv in st.BoundVillages)
                        {
                            if (bv == null || bv.Settlement == null) continue;
                            var t = LandBk.Title(bv.Settlement);
                            var dj = LandBk.DeJure(t);
                            if (dj == Hero.MainHero) { k++; all++; }
                            else if (dj == head) { all++; if (LandBk.Inherit(head, Hero.MainHero, t)) m++; }
                        }
                    }
                    try { seller.ConsiderAndUpdateHomeSettlement(); } catch (Exception e) { Stumble("Transfer(HomeSettlement sprzedawcy)", e); }
                    try { Clan.PlayerClan.ConsiderAndUpdateHomeSettlement(); } catch (Exception e) { Stumble("Transfer(HomeSettlement gracza)", e); }
                    gp = st.Town.GarrisonParty;
                    int g1 = gp != null && gp.MemberRoster != null ? gp.MemberRoster.TotalManCount : -1;
                    string home1s = seller.HomeSettlement != null ? seller.HomeSettlement.StringId : "-", home1p = Clan.PlayerClan.HomeSettlement != null ? Clan.PlayerClan.HomeSettlement.StringId : "-";
                    string dem = LandBk.DemesneLine(Hero.MainHero);
                    Log.Info(P + "zamek " + o.Name + " (" + st.StringId + ") gracza | tytuly BK: AddBarter " + k + ", zapas InheritTitle " + m + " (z " + all + ")"
                             + " | zaloga " + g0 + " -> " + g1 + " | HomeSettlement sprzedawcy " + home0s + " -> " + home1s + ", gracza " + home0p + " -> " + home1p
                             + " | demesne BK " + dem + (ChronicleOwnTarget(st.StringId) ? " | Kronika own: cel kanonu - pominie" : "") + ".");
                    why = "zamek gracza, tytuly BK " + (k + m) + "/" + all + (bx.Length > 0 ? " (" + bx + " PO przeniesieniu)" : "");
                }
                catch (Exception e) { Stumble("Transfer(po zamku)", e); why = "zamek gracza; wyjatek PO przeniesieniu " + e.GetType().Name; }
                return true;
            }
            catch (Exception e) { Stumble("Transfer", e); bool mv = Moved(o); why = "wyjatek " + e.GetType().Name + (mv ? " PO przeniesieniu" : ""); return mv; }
        }

        /// <summary>Kronika: osada jest celem akcji own: w danych Kroniki (Chronicle.Data) - Kronika pominie osade gracza (Res.Skip). Tylko odczyt; nie rzuca.</summary>
        private static bool ChronicleOwnTarget(string stId)
        {
            try
            {
                if (!Chronicle.Data.Loaded) return false;
                string key = "own:" + stId + ":";
                foreach (var w in Chronicle.Data.Windows)
                {
                    if (w == null) continue;
                    if (HasOwn(w.Main, key)) return true;
                    foreach (var r in w.Variants) if (HasOwn(r, key)) return true;
                }
            }
            catch { }
            return false;
        }

        private static bool HasOwn(Chronicle.Data.RowDef r, string key)
        {
            if (r == null) return false;
            foreach (var e in r.Effects) if (e != null && e.IndexOf(key, StringComparison.Ordinal) >= 0) return true;
            foreach (var op in r.Options) if (op != null) foreach (var e in op.Effects) if (e != null && e.IndexOf(key, StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        private static void AddDeal(Offer o, int t)
        {
            _deals.Add(new Deal { Kind = o.SellBack ? -o.Kind : o.Kind, Id = o.Id, Day = t, Price = o.Price, Years100 = (int)Math.Round(o.Years * 100), Disc1000 = (int)Math.Round(o.Disc * 1000), Seller = o.Seller != null ? o.Seller.StringId : "" });
            while (_deals.Count > 200) _deals.RemoveAt(0);
        }

        /// <summary>recenzja n36: lata, ktore gracz zaplacil za wioske poboczna uid - ostatnie kupno w _deals (lata x (1 - upust); premia wioski 1), o ile po nim
        /// nie bylo odkupu; brak zapisu (pierwsza wioska, zapis wypadl z 200 ostatnich) - LandYearsOfPurchase, jak odkup przed n36. Sufit lat odkupu. Nie rzuca.</summary>
        internal static double PaidYears(string uid)
        {
            double flat = Math.Max(1, Settings.Current.LandYearsOfPurchase);
            for (int i = _deals.Count - 1; i >= 0; i--)
            {
                var d = _deals[i];
                if (d == null || d.Id != uid) continue;
                if (d.Kind == -KSide) break;   // odkupiona po ostatnim kupnie - wioska wrocila do gracza inna droga
                if (d.Kind == KSide && d.Years100 > 0) return Math.Max(0.01, d.Years100 / 100.0 * (1 - Math.Max(0, Math.Min(1000, d.Disc1000)) / 1000.0));
            }
            return flat;
        }

        // ============================================================ Z7: odkup
        /// <summary>Odkup wioski gracza uid przez posiadacza podatku okregu (Z7). Oferta z Ok i powodem. Nie rzuca.</summary>
        internal static Offer BuildSellBack(string uid)
        {
            var o = new Offer { Kind = KSide, Id = uid ?? "", Kinds = "side village", SellBack = true };
            try
            {
                var s = Settings.Current;
                if (!On || !s.LandSalesSellBack) { o.Why = "Selling villages back is switched off (mod settings)."; return o; }
                var w = SideVillageFief.Find(uid);
                if (w == null) { o.Why = "This village is not yours."; return o; }
                o.Name = w.Name; o.DistId = w.Dist;
                var st = Settlement.Find(w.Dist); var v = st != null ? st.Village : null;
                if (v == null) { o.Why = "Its district is not known."; return o; }
                o.St = st;
                int held = Today() - w.Day0;
                if (held < Math.Max(0, s.LandResaleDays) && !_bypassResale) { o.Why = "You have held it only " + held + " days - a lord buys back land only after " + s.LandResaleDays + "."; return o; }
                // recenzja m113: kupujacy = rod platnika (PayerOf - po odkupie to on przestaje placic); placi glowa rodu (rycerz BK - glowa jego rodu)
                Hero ph = SideVillageFief.PayerOf(v);
                Hero h = ph != null && ph.Clan != null ? ph.Clan.Leader : null;
                o.Payer = ph; o.Head = h; o.Seller = h != null ? h.Clan : null;   // tu: kupujacy
                if (ph == null || h == null || !h.IsAlive || h.Clan == null || h == Hero.MainHero || h.Clan == Clan.PlayerClan) { o.Why = "There is no lord to buy it back."; return o; }
                if (NeverSeller(h.Clan, false)) { o.Why = HouseOf(h.Clan) + " does not buy land."; return o; }
                if (Clan.PlayerClan.MapFaction != null && h.MapFaction != null && FactionManager.IsAtWarAgainstFaction(h.MapFaction, Clan.PlayerClan.MapFaction)) { o.Why = "You are at war with " + NameOf(h) + "'s realm."; return o; }
                if (DebtLadder.IsDebtor(h.Clan)) { o.Why = NameOf(h) + " is in debt " + Him(h) + " and cannot buy land."; return o; }
                if (ReferenceEquals(w, FirstOwn()) && !SideVillageHome.ChestEmpty()) { o.Why = "Empty the chest of your house there first."; return o; }
                string why;
                double due = SideVillageFief.DueFor(v, uid, out why);
                o.SeasonFix = SideYearFix(v, st, out o.FixNote);   // n36 (M1): jak BuildSide
                o.Days = Math.Max(1, LandValue.SlotDays(st));
                o.NetDay = due * o.SeasonFix;
                if (!(o.NetDay > 0)) { o.Why = "It earns nothing today - no price can be set."; return o; }
                o.Premium = 1;
                Price(o, Clan.PlayerClan, st.MapFaction);
                if (o.Price <= 0 || o.Price > int.MaxValue) { o.Why = "No price can be set."; return o; }
                Hero hh; double R; bool old;
                int spare = TourneyPay.ClanSpare(h.Clan, false, out hh, out R, out old);
                if (spare < o.Price) { o.Why = NameOf(h) + " cannot spare " + o.Price.ToString("N0", Inv) + " (" + He(h) + " can spare " + Math.Max(0, spare).ToString("N0", Inv) + ")."; return o; }
                o.Ok = true;
            }
            catch (Exception e) { o.Ok = false; o.Why = "Not known today."; Stumble("BuildSellBack", e); }
            return o;
        }

        private static SideVillageFief.Own FirstOwn() { var l = SideVillageFief.Owned(); return l.Count > 0 ? l[0] : null; }

        private static void ExecuteSellBack(Offer q)
        {
            var me = Hero.MainHero;
            var o = BuildSellBack(q.Id);
            if (!o.Ok) { Drop(q, o.Why, "odkup: warunek juz nie spelniony (" + o.Why + ")"); return; }
            if (o.Price != q.Price || o.Head != q.Head) { Drop(q, "the price is now " + o.Price.ToString("N0", Inv) + " - ask again.", "odkup: cena albo kupujacy inne (" + q.Price + " -> " + o.Price + ")"); return; }
            var buyer = o.Head;
            long w0 = WorldGold(), pg0 = me.Gold, bg0 = buyer.Gold, cred = 0;
            string why = "";
            // recenzja m113 (lustro Execute): InPay tylko wokol przelewu; zaplacone - kiesa gracza wieksza dokladnie o cene; o wyniku decyduje stan (IsMine)
            InPay = true;
            try { GiveGoldAction.ApplyBetweenCharacters(buyer, me, (int)o.Price, true); }
            catch (Exception e) { Stumble("ExecuteSellBack(a)", e); }
            finally { InPay = false; }
            long got = me.Gold - pg0;
            if (got != o.Price)
            {
                long back = got > 0 ? Refund(me, buyer, got, "ExecuteSellBack(zwrot a)") : 0;
                _dRefund++;
                Log.Info(P + "transakcja cofnieta przy platnosci - odkup " + o.Name + " (" + o.Id + "): zaplacono " + got + " z " + o.Price + ", zwrot " + back + ".");
                Log.Player("The sale of " + o.Name + " could not be completed.", true);
                return;
            }
            bool removed = false;
            try { removed = SideVillageFief.RemoveOwned(o.Id, out why); }
            catch (Exception e) { Stumble("ExecuteSellBack(b)", e); why = "wyjatek " + e.GetType().Name; }
            if (!removed && SideVillageFief.IsMine(o.Id))
            {
                long back = Refund(me, buyer, o.Price, "ExecuteSellBack(zwrot c)");
                _dRefund++;
                Log.Info(P + "transakcja cofnieta - odkup " + o.Name + " (" + o.Id + "): " + why + "; zwrot " + back + " z " + o.Price + (back < o.Price ? ", brak zwrotu " + (o.Price - back) : "") + ".");
                Log.Player("The sale of " + o.Name + " could not be completed: " + why, true);
                return;
            }
            try { cred = DebtLadder.LandProceeds(Clan.PlayerClan, o.Price); }
            catch (Exception e) { Stumble("ExecuteSellBack(d)", e); }
            DealDone();
            AddDeal(o, Today());
            _dSellBack++;
            Log.Info(P + "ODKUP - " + o.Name + " (" + o.Id + ") wraca do " + NameOf(buyer) + " (" + (buyer.Clan != null ? buyer.Clan.StringId : "-") + ") za " + o.Price + " | " + o.Pl
                     + " | gracz " + pg0 + " -> " + me.Gold + " (wierzycielom " + cred + "), kupujacy " + bg0 + " -> " + buyer.Gold + " | pieniadz swiata " + w0 + " -> " + WorldGold() + ".");
            Log.Player(NameOf(buyer) + " bought " + o.Name + " back from you for " + o.Price.ToString("N0", Inv) + "." + (cred > 0 ? " Your creditors took " + cred.ToString("N0", Inv) + " of it." : ""));
            AfterSellBackMenu(o);
        }

        // ============================================================ doba
        /// <summary>OnDailyTick (po SideVillageFief.Daily): linia na sucho raz na sesje i co 28 dob, linia doby z ruchem albo co 7 dob. Nie rzuca.</summary>
        internal static void Daily()
        {
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                int t = Today();
                if (On && VillageBook.Active && (!_dryDone || t - _dryDay >= 28)) { _dryDone = true; _dryDay = t; DryRun(); }
                bool act = _dBuilt > 0 || _dDeals > 0 || _dDropped > 0 || _dRefund > 0 || _dAsk > 0 || _dSellBack > 0 || _stDay > 0;
                if (act || t % 7 == 0)
                {
                    var sb = new StringBuilder(300);
                    sb.Append(P).Append("dzien ").Append(t - 1).Append(" | klucze: sprzedaz ").Append(s.LandSales ? "WL" : "WYL").Append(" (wioski ").Append(s.LandSalesSideVillages ? "WL" : "WYL")
                      .Append(", Lordship ").Append(s.LandSalesLordships ? "WL" : "WYL").Append(", zamki ").Append(s.LandSalesCastles ? "WL" : "WYL").Append(", odkup ").Append(s.LandSalesSellBack ? "WL" : "WYL")
                      .Append("), cena nie dochodem ").Append(s.LandPriceNotWindfall ? "WL" : "WYL").Append(", BK ").Append(LandBk.Ready ? "tak" : "BRAK")
                      .Append(" | ksiega wartosci ziemi: ").Append(LandValue.LastNote)
                      .Append(" | ofert policzonych ").Append(_dBuilt).Append(", pytan ").Append(_dAsk).Append(", okien ").Append(_dWindows).Append(", kupna ").Append(_dDeals).Append(", odkupy ").Append(_dSellBack)
                      .Append(", przepadle ").Append(_dDropped).Append(", cofniete ").Append(_dRefund)
                      .Append(" | wioski gracza ").Append(SideVillageFief.OwnedCount).Append(", transakcji w zapisie ").Append(_deals.Count)
                      .Append(" | potkniecia doby ").Append(_stDay).Append(", sesji ").Append(_stAll).Append(" (ksiega wartosci ").Append(LandValue.Stumbles).Append(")");
                    if (_st.Count > 0) { sb.Append(" ["); bool f = true; foreach (var kv in _st) { if (!f) sb.Append(", "); f = false; sb.Append(kv.Key).Append(' ').Append(kv.Value); } sb.Append(']'); }
                    sb.Append('.');
                    Log.Info(sb.ToString());
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { _dBuilt = _dDeals = _dDropped = _dRefund = _dAsk = _dWindows = _dSellBack = 0; _stDay = 0; }
        }

        /// <summary>n36: stosunek glowy rodu h do gracza; int.MinValue - brak glowy albo rod gracza. Nie rzuca.</summary>
        private static int RelOf(Hero h)
        {
            try { return h != null && h.IsAlive && h != Hero.MainHero && h.Clan != Clan.PlayerClan ? (int)h.GetRelationWithPlayer() : int.MinValue; }
            catch { return int.MinValue; }
        }

        /// <summary>Linia na sucho: rozklad cen wszystkich wiosek pobocznych (bez bramek gracza i potrzeby - sama cena Z4 bez upustu: DueFor x poprawka pory roku x dni roku
        /// x lata (n36: z krzywej stosunku glowy rodu platnika do gracza, z wojna) - jeden PartsOf na okreg), Lordship i zamki (mediana; lata z krzywej stosunku de iure
        /// / pana zamku), 5 cen z rachunkiem do sprawdzenia recznie. Ponizej progu - cena przy najgorszych latach z dopiskiem "nie sprzeda". Tylko log.</summary>
        internal static void DryRun()
        {
            try
            {
                var s = Settings.Current;
                var recs = VillageBook.LayoutRecords();
                if (recs == null) return;
                int d0 = SideVillageFief.DueForDistricts;
                int dpy = IronBank.DaysPerYearNow();
                var prices = new List<long>(); var samples = new List<string>();
                var lines = new List<KeyValuePair<long, string>>();
                // n36: stosunek glowy rodu platnika okregu - raz na okreg (PayerOf jak BuildSide); int.MinValue - bez sprzedawcy (lata najgorsze)
                var relD = new Dictionary<string, int>(StringComparer.Ordinal);
                int sMin = RelMinOf(KSide), lMin = RelMinOf(KLord), cMin = RelMinOf(KCastle), below = 0, belowL = 0, belowC = 0;
                Func<int, int, string> relNote = (rel, min) => rel == int.MinValue ? "bez sprzedawcy" : "stosunek " + rel + (rel < min ? " < prog " + min + " - nie sprzeda" : "");
                foreach (var r in recs)
                {
                    try
                    {
                        if (r == null || string.IsNullOrEmpty(r.Uid)) continue;
                        var st = Settlement.Find(r.VillageId);
                        var v = st != null ? st.Village : null;
                        if (v == null) continue;
                        string why;
                        double due = SideVillageFief.DueFor(v, r.Uid, out why);
                        if (!(due > 0)) continue;
                        string fn; double fix = SideYearFix(v, st, out fn);
                        int rel;
                        if (!relD.TryGetValue(r.VillageId, out rel)) { Hero ph = SideVillageFief.PayerOf(v); rel = RelOf(ph != null && ph.Clan != null ? ph.Clan.Leader : null); relD[r.VillageId] = rel; }
                        if (rel != int.MinValue && rel < sMin) below++;
                        double yr = YearsFor(rel == int.MinValue ? sMin : rel, sMin, false);
                        double ye = yr * (AtWarMajor(st.MapFaction) ? Math.Max(0.1f, Math.Min(1f, s.LandYearsAtWar)) : 1.0);
                        if (ye < Math.Max(1, s.LandYearsFloor)) ye = Math.Max(1, s.LandYearsFloor);
                        double net = due * fix, raw = net * dpy * ye;
                        long p = Round100(raw);
                        prices.Add(p);
                        lines.Add(new KeyValuePair<long, string>(p, r.Uid + " (" + (string.IsNullOrEmpty(r.Name) ? "?" : r.Name) + ", " + r.VillageId + "): DueFor " + due.ToString("0.####", Inv) + " x pora roku " + fix.ToString("0.####", Inv)
                                                                     + " = " + net.ToString("0.####", Inv) + " x " + dpy + " x " + ye.ToString("0.####", Inv) + " (" + relNote(rel, sMin) + " -> lata " + yr.ToString("0.##", Inv) + ")"
                                                                     + " = " + raw.ToString("0.##", Inv) + " -> " + p));
                    }
                    catch (Exception e) { Stumble("DryRun(wioska)", e); }
                }
                prices.Sort(); lines.Sort((a, b) => a.Key.CompareTo(b.Key));
                var lp = new List<long>(); var cp = new List<long>();
                foreach (var v in Village.All)
                {
                    try
                    {
                        if (v == null || v.Settlement == null) continue;
                        int d; double vt = LandValue.YearMean(v.Settlement, LandValue.VT, out d);
                        if (!(vt > 0)) continue;
                        Hero lh = LandBk.Ready ? LandBk.DeJure(LandBk.Title(v.Settlement)) : (v.Bound != null && v.Bound.OwnerClan != null ? v.Bound.OwnerClan.Leader : null);
                        int rel = RelOf(lh); if (rel != int.MinValue && rel < lMin) belowL++;
                        lp.Add(Round100(vt * dpy * YearsFor(rel == int.MinValue ? lMin : rel, lMin, false)));
                    }
                    catch (Exception e) { Stumble("DryRun(Lordship)", e); }
                }
                foreach (var st in Settlement.All)
                {
                    try
                    {
                        if (st == null || !st.IsCastle) continue;
                        int d; double lr = LandValue.YearMean(st, LandValue.LR, out d);
                        foreach (var bv in st.BoundVillages) if (bv != null && bv.Settlement != null) { int dv; lr += LandValue.YearMean(bv.Settlement, LandValue.LR, out dv) + LandValue.YearMean(bv.Settlement, LandValue.VT, out dv); }
                        if (!(lr > 0)) continue;
                        int rel = RelOf(st.Town != null && st.Town.OwnerClan != null ? st.Town.OwnerClan.Leader : null); if (rel != int.MinValue && rel < cMin) belowC++;
                        cp.Add(Round100(lr * dpy * YearsFor(rel == int.MinValue ? cMin : rel, cMin, false) * Math.Max(1f, s.LandCastlePremium)));
                    }
                    catch (Exception e) { Stumble("DryRun(zamek)", e); }
                }
                lp.Sort(); cp.Sort();
                // recenzja m113: ile rodow AI przechodzi kazda miare potrzeby (Z2) - czy ziemia jest na sprzedaz "za dlugi", a nie wszedzie
                int nh = 0, c1 = 0, c2 = 0, c3 = 0, c3h = 0, c4 = 0, cs = 0, cl = 0, cc = 0;
                foreach (var c in Clan.All)
                {
                    try
                    {
                        if (NeverSeller(c, false) || c.Leader == null || !c.Leader.IsAlive) continue;
                        nh++;
                        double G, R, D; int stk;
                        ClanBudget.NeedOf(c, out G, out R, out D, out stk);
                        bool n1 = DebtLadder.IsDebtor(c), n2 = DebtLadder.RansomDebtOf(c) > 0, n3 = G < R, n3h = G < 0.5 * R, n4 = stk >= Math.Max(1, s.BudgetHysteresisDays);
                        if (n1) c1++; if (n2) c2++; if (n3) c3++; if (n3h) c3h++; if (n4) c4++;
                        if (NeedKind(KSide, n1, n2, n3h, n4, null, false)) cs++;
                        if (NeedKind(KLord, n1, n2, n3h, n4, null, false)) cl++;
                        if (NeedKind(KCastle, n1, n2, n3h, n4, DebtLadder.StageOf(c), DeepDebt(DebtLadder.OwedOf(c), D))) cc++;
                    }
                    catch (Exception e) { Stumble("DryRun(potrzeba)", e); }
                }
                var sb = new StringBuilder(1500);
                sb.Append(P).Append("na sucho (dzien ").Append(Today()).Append(", rok ").Append(dpy).Append(" dob, lata wedlug stosunku ").Append(YearsFor(0, 0, false).ToString("0.##", Inv)).Append(" przy progu -> ")
                  .Append(YearsFor(int.MaxValue / 2, 0, false).ToString("0.##", Inv)).Append(" przy progu + ").Append(Math.Max(1, s.LandYearsRelationSpan)).Append(" (progi: wioska ").Append(sMin)
                  .Append(", Lordship ").Append(lMin).Append(", zamek ").Append(cMin).Append("; odkup odwrotnie), ksiega wartosci ").Append(LandValue.DaysMeasured).Append(" dob)")
                  .Append(" | wioski poboczne z cena ").Append(prices.Count).Append(" z ").Append(recs.Count).Append(" (stosunek ponizej progu - nie sprzeda: ").Append(below).Append("): ").Append(Dist(prices))
                  .Append(" | Lordship (sam VT x lata ze stosunku de iure, bez upustu) ").Append(lp.Count).Append(" (ponizej progu ").Append(belowL).Append("): ").Append(Dist(lp))
                  .Append(" | zamki (renta + wsie, bez strazy, lata ze stosunku pana, x premia ").Append(s.LandCastlePremium.ToString("0.##", Inv)).Append(") ").Append(cp.Count).Append(" (ponizej progu ").Append(belowC).Append("): ").Append(Dist(cp))
                  .Append(" | potrzeba rodow AI (").Append(nh).Append("): N1 dluznik ").Append(c1).Append(", N2 okup ").Append(c2).Append(", N3 G<R ").Append(c3).Append(" (G<0.5R ").Append(c3h)
                  .Append("), N4 seria ").Append(c4).Append(" -> wioski ").Append(cs).Append(", Lordship ").Append(cl).Append(", zamki ").Append(cc)
                  .Append(" | koszt: okregow PartsOf ").Append(SideVillageFief.DueForDistricts - d0);
                if (lines.Count > 0)
                {
                    sb.Append(" | 5 cen do sprawdzenia: ");
                    for (int i = 0; i < 5 && lines.Count > 0; i++) { int k = (int)Math.Round(i * (lines.Count - 1) / 4.0); sb.Append(i > 0 ? "; " : "").Append(lines[k].Value); }
                }
                sb.Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("DryRun", e); }
        }

        private static string Dist(List<long> p)
        {
            if (p.Count == 0) return "brak";
            Func<double, long> q = f => p[Math.Max(0, Math.Min(p.Count - 1, (int)Math.Round(f * (p.Count - 1))))];
            double sum = 0; foreach (var x in p) sum += x;
            return "min " + p[0].ToString("N0", Inv) + ", p10 " + q(0.1).ToString("N0", Inv) + ", mediana " + q(0.5).ToString("N0", Inv) + ", p90 " + q(0.9).ToString("N0", Inv)
                   + ", max " + p[p.Count - 1].ToString("N0", Inv) + ", srednia " + (sum / p.Count).ToString("N0", Inv);
        }

        // ============================================================ zapis (arm_land113)
        private static string Clean(string s) { return (s ?? "").Replace("|", "/").Replace(":", "/").Replace(",", "/").Replace("=", "/"); }

        /// <summary>Zapis arm_land113. Recenzja m113: + "t=rod:kwota,..." - cena ziemi tej doby w ksiedze rodow (ClanIncomeBook.TodayLand zyje w pamieci, a Today jest
        /// w zapisie; bez tego cena sprzed zapisu bylaby po wczytaniu jednorazowym wplywem). Starsza DLL pomija nieznana czesc.</summary>
        internal static string Export()
        {
            try
            {
                var tl = new List<KeyValuePair<string, long>>();
                ClanIncomeBook.ForEachTodayLand((id, a) => tl.Add(new KeyValuePair<string, long>(id, a)));
                return Serialize(_cd, _deals, tl);
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        private static string Serialize(Dictionary<string, Cd> cds, List<Deal> deals, List<KeyValuePair<string, long>> tl)
        {
            if (cds.Count == 0 && deals.Count == 0 && tl.Count == 0) return "";
            var sb = new StringBuilder(64 + 40 * cds.Count + 60 * deals.Count + 30 * tl.Count);
            sb.Append(SaveVer).Append("|c=");
            bool f = true;
            foreach (var kv in cds) { if (!f) sb.Append(','); f = false; sb.Append(Clean(kv.Key)).Append(':').Append(kv.Value.LastSale.ToString(Inv)).Append(':').Append(kv.Value.LastCastle.ToString(Inv)); }
            sb.Append("|d=").Append(DealsTextOf(deals));
            if (tl.Count > 0)
            {
                sb.Append("|t=");
                f = true;
                foreach (var kv in tl) { if (!f) sb.Append(','); f = false; sb.Append(Clean(kv.Key)).Append(':').Append(kv.Value.ToString(Inv)); }
            }
            return sb.ToString();
        }

        internal static string DealsText() { return DealsTextOf(_deals); }

        private static string DealsTextOf(List<Deal> deals)
        {
            var sb = new StringBuilder(60 * deals.Count);
            bool f = true;
            foreach (var d in deals)
            {
                if (!f) sb.Append(','); f = false;
                sb.Append(d.Kind.ToString(Inv)).Append(':').Append(Clean(d.Id)).Append(':').Append(d.Day.ToString(Inv)).Append(':').Append(d.Price.ToString(Inv)).Append(':')
                  .Append(d.Years100.ToString(Inv)).Append(':').Append(d.Disc1000.ToString(Inv)).Append(':').Append(Clean(d.Seller));
            }
            return sb.ToString();
        }

        /// <summary>Odczyt napisu arm_land113 do podanych zbiorow (bez stanu zywego). false - napis nieznany.</summary>
        private static bool Parse(string data, Dictionary<string, Cd> cds, List<Deal> deals, List<KeyValuePair<string, long>> tl, out int bad)
        {
            bad = 0;
            var f = data.Split('|');
            if (f.Length == 0 || f[0] != SaveVer) return false;
            for (int i = 1; i < f.Length; i++)
            {
                if (f[i].StartsWith("c=", StringComparison.Ordinal))
                {
                    foreach (var e in f[i].Substring(2).Split(','))
                    {
                        if (e.Length == 0) continue;
                        var p = e.Split(':'); int a, b;
                        if (p.Length < 3 || p[0].Length == 0 || !int.TryParse(p[1], NumberStyles.Integer, Inv, out a) || !int.TryParse(p[2], NumberStyles.Integer, Inv, out b)) { bad++; continue; }
                        cds[p[0]] = new Cd { LastSale = a, LastCastle = b };
                    }
                }
                else if (f[i].StartsWith("d=", StringComparison.Ordinal))
                {
                    foreach (var e in f[i].Substring(2).Split(','))
                    {
                        if (e.Length == 0) continue;
                        var p = e.Split(':'); int k, day, y, dc; long pr;
                        if (p.Length < 7 || !int.TryParse(p[0], NumberStyles.Integer, Inv, out k) || !int.TryParse(p[2], NumberStyles.Integer, Inv, out day) || !long.TryParse(p[3], NumberStyles.Integer, Inv, out pr)
                            || !int.TryParse(p[4], NumberStyles.Integer, Inv, out y) || !int.TryParse(p[5], NumberStyles.Integer, Inv, out dc)) { bad++; continue; }
                        deals.Add(new Deal { Kind = k, Id = p[1], Day = day, Price = pr, Years100 = y, Disc1000 = dc, Seller = p[6] });
                    }
                }
                else if (f[i].StartsWith("t=", StringComparison.Ordinal))
                {
                    foreach (var e in f[i].Substring(2).Split(','))
                    {
                        if (e.Length == 0) continue;
                        var p = e.Split(':'); long a;
                        if (p.Length < 2 || p[0].Length == 0 || !long.TryParse(p[1], NumberStyles.Integer, Inv, out a) || a <= 0) { bad++; continue; }
                        tl.Add(new KeyValuePair<string, long>(p[0], a));
                    }
                }
                // inne czesci (nowsza DLL) - pominiete
            }
            return true;
        }

        /// <summary>Autotest (T8): napis -> zbiory na boku -> napis; stan zywy (i ksiega rodow) nietkniety. Nie rzuca.</summary>
        internal static string RoundTrip(string data)
        {
            try
            {
                if (string.IsNullOrEmpty(data)) return "";
                var cds = new Dictionary<string, Cd>(StringComparer.Ordinal); var deals = new List<Deal>(); var tl = new List<KeyValuePair<string, long>>(); int bad;
                if (!Parse(data, cds, deals, tl, out bad)) return "?";
                return Serialize(cds, deals, tl);
            }
            catch (Exception e) { Stumble("RoundTrip", e); return "?"; }
        }

        /// <summary>Odczyt arm_land113 (PO arm_clanincome - ksiega rodow juz wczytana: "t=" wraca do TodayLand).</summary>
        internal static void Import(string data)
        {
            _cd.Clear(); _deals.Clear();
            if (string.IsNullOrEmpty(data)) return;
            try
            {
                var tl = new List<KeyValuePair<string, long>>(); int bad;
                if (!Parse(data, _cd, _deals, tl, out bad)) { _cd.Clear(); _deals.Clear(); Log.Info(P + "zapis arm_land113 nieznany (" + data.Length + " znakow) - pominiety."); return; }
                foreach (var kv in tl) ClanIncomeBook.RestoreLand(kv.Key, kv.Value);
                Log.Info(P + "zapis arm_land113 - rodow z przerwa " + _cd.Count + ", transakcji " + _deals.Count + ", cena ziemi tej doby w ksiedze rodow " + tl.Count + (bad > 0 ? ", zle wpisy " + bad : "") + ".");
            }
            catch (Exception e) { _cd.Clear(); _deals.Clear(); Stumble("Import", e); }
        }

        /// <summary>Przy kazdym zapisie: linia do recznego przywrocenia po zejsciu na starsza DLL (ta nie zapisze arm_side_fief2 ani arm_land113).</summary>
        internal static void SaveLine(string export2)
        {
            try
            {
                if (string.IsNullOrEmpty(export2) && _deals.Count == 0) return;
                Log.Info(P + "zapis - wioski gracza 1 + " + Math.Max(0, SideVillageFief.OwnedCount - (SideVillageFief.HasFief ? 1 : 0)) + ": " + (string.IsNullOrEmpty(export2) ? "-" : export2) + "; transakcje: " + DealsText());
            }
            catch (Exception e) { Stumble("SaveLine", e); }
        }
    }

    /// <summary>
    /// m113-zakup: odbicie BK (BannerKings.BannerKingsConfig.Instance.TitleManager) dla kupna ziemi - tytul osady, posiadacz de iure, tytuly de iure bohatera, dziedziczenie
    /// tytulu (InheritTitle - nigdy GrantTitle), posiadacz podatku wsi (jak BK VillageExtensions.GetActualOwner) i demesne BK. Bez BK - Ready false, wszystko null / 0.
    /// Nie rzuca (wyjatek BK - potkniecie LandSale).
    /// </summary>
    internal static class LandBk
    {
        private static bool _tried;
        private static PropertyInfo _pInst, _pTm, _pStab;
        private static FieldInfo _fTm;
        private static MethodInfo _mGetTitle, _mAllDeJure, _mInherit, _mCurDem, _mDemLimit, _mWeight;
        private static PropertyInfo _pDeJure, _pFief;

        private static void Resolve()
        {
            if (_tried) return;
            _tried = true;
            try
            {
                var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                if (cfgT == null) return;
                _pInst = AccessTools.Property(cfgT, "Instance");
                _pTm = AccessTools.Property(cfgT, "TitleManager");
                if (_pTm == null) _fTm = AccessTools.Field(cfgT, "TitleManager");
                _pStab = AccessTools.Property(cfgT, "StabilityModel");
                var tmT = AccessTools.TypeByName("BannerKings.Managers.TitleManager");
                var ftT = AccessTools.TypeByName("BannerKings.Managers.Titles.FeudalTitle");
                if (tmT == null || ftT == null) return;
                _mGetTitle = AccessTools.Method(tmT, "GetTitle", new[] { typeof(Settlement) });
                _mAllDeJure = AccessTools.Method(tmT, "GetAllDeJure", new[] { typeof(Hero) });
                _mInherit = AccessTools.Method(tmT, "InheritTitle", new[] { typeof(Hero), typeof(Hero), ftT });
                _pDeJure = AccessTools.Property(ftT, "deJure");
                _pFief = AccessTools.Property(ftT, "Fief");
                var smT = _pStab != null ? _pStab.PropertyType : null;
                if (smT != null)
                {
                    _mCurDem = AccessTools.Method(smT, "CalculateCurrentDemesne", new[] { typeof(Clan), typeof(bool) });
                    _mDemLimit = AccessTools.Method(smT, "CalculateDemesneLimit", new[] { typeof(Hero), typeof(bool) });
                    _mWeight = AccessTools.Method(smT, "GetSettlementDemesneWight", new[] { typeof(Settlement) });
                }
            }
            catch (Exception e) { LandSale.Stumble("LandBk.Resolve", e); }
        }

        private static object Cfg() { try { return _pInst != null ? _pInst.GetValue(null, null) : null; } catch { return null; } }
        private static object Tm()
        {
            Resolve();
            var c = Cfg();
            if (c == null) return null;
            try { return _pTm != null ? _pTm.GetValue(c, null) : _fTm != null ? _fTm.GetValue(c) : null; } catch { return null; }
        }

        internal static bool Ready { get { Resolve(); return _mGetTitle != null && _pDeJure != null && Tm() != null; } }

        internal static object Title(Settlement st)
        {
            if (st == null) return null;
            var tm = Tm();
            if (tm == null || _mGetTitle == null) return null;
            try { return _mGetTitle.Invoke(tm, new object[] { st }); }
            catch (Exception e) { LandSale.Stumble("LandBk.Title", e); return null; }
        }

        internal static Hero DeJure(object t)
        {
            if (t == null || _pDeJure == null) return null;
            try { return _pDeJure.GetValue(t, null) as Hero; } catch { return null; }
        }

        internal static Settlement Fief(object t)
        {
            if (t == null || _pFief == null) return null;
            try { return _pFief.GetValue(t, null) as Settlement; } catch { return null; }
        }

        internal static List<object> AllDeJure(Hero h)
        {
            var l = new List<object>();
            var tm = Tm();
            if (h == null || tm == null || _mAllDeJure == null) return l;
            try { var e = _mAllDeJure.Invoke(tm, new object[] { h }) as IEnumerable; if (e != null) foreach (var o in e) if (o != null) l.Add(o); }
            catch (Exception ex) { LandSale.Stumble("LandBk.AllDeJure", ex); }
            return l;
        }

        internal static int DeJureCount(Hero h) { return AllDeJure(h).Count; }

        internal static bool Inherit(Hero from, Hero to, object t)
        {
            var tm = Tm();
            if (from == null || to == null || t == null || tm == null || _mInherit == null) return false;
            try { _mInherit.Invoke(tm, new object[] { from, to, t }); return true; }
            catch (Exception e) { LandSale.Stumble("LandBk.Inherit", e); return false; }
        }

        /// <summary>Jak BK VillageExtensions.GetActualOwner: glowa rodu pana zamku, chyba ze tytul wsi ma posiadacza de iure z frakcji wsi. null - bez BK (wolajacy: zapas).</summary>
        internal static Hero ActualOwner(Village v)
        {
            if (v == null || v.Settlement == null || !Ready) return null;
            try
            {
                var st = v.Settlement;
                Hero res = st.OwnerClan != null ? st.OwnerClan.Leader : null;
                var dj = DeJure(Title(st));
                if (dj != null && st.MapFaction == dj.MapFaction) res = dj;
                return res;
            }
            catch (Exception e) { LandSale.Stumble("LandBk.ActualOwner", e); return null; }
        }

        /// <summary>"obecne/limit" demesne BK rodu gracza (po zakupie - zamek juz liczony). "-" bez BK.</summary>
        internal static string DemesneLine(Hero h)
        {
            try
            {
                var c = Cfg();
                if (h == null || c == null || _pStab == null || _mCurDem == null || _mDemLimit == null) return "-";
                var sm = _pStab.GetValue(c, null);
                var cur = (ExplainedNumber)_mCurDem.Invoke(sm, new object[] { h.Clan, false });
                var lim = (ExplainedNumber)_mDemLimit.Invoke(sm, new object[] { h, false });
                return cur.ResultNumber.ToString("0.##", CultureInfo.InvariantCulture) + "/" + lim.ResultNumber.ToString("0.##", CultureInfo.InvariantCulture);
            }
            catch (Exception e) { LandSale.Stumble("LandBk.Demesne", e); return "-"; }
        }

        /// <summary>Demesne BK rodu gracza PO zakupie osady st (obecne + waga osady) i limit. false - bez BK.</summary>
        internal static bool DemesneAfter(Hero h, Settlement st, out float after, out float limit)
        {
            after = 0; limit = 0;
            try
            {
                var c = Cfg();
                if (h == null || c == null || _pStab == null || _mCurDem == null || _mDemLimit == null) return false;
                var sm = _pStab.GetValue(c, null);
                var cur = (ExplainedNumber)_mCurDem.Invoke(sm, new object[] { h.Clan, false });
                var lim = (ExplainedNumber)_mDemLimit.Invoke(sm, new object[] { h, false });
                float w = st != null ? (st.IsTown ? 2f : st.IsCastle ? 1f : 0.5f) : 0f;   // BK GetSettlementDemesneWight (bez perku - liczy wlasciciela osady)
                after = cur.ResultNumber + w; limit = lim.ResultNumber;
                return true;
            }
            catch (Exception e) { LandSale.Stumble("LandBk.DemesneAfter", e); return false; }
        }
    }
}
