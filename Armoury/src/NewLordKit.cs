using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// 159 - "POZYCZKA + MNIEJSZE WOJSKO" (Jeff 26.10 ok. 00:10: "dopadlem w ostatniej bitwie znowu armie bez wyposazenia czemu przeciwnik biega z armia bez
    /// sprzetu?!"; decyzja "Pozyczka + mniejsze": lord najpierw kupuje, co moze - z pozyczka Banku Zelaznego do limitu - i nie bierze ludzi, ktorych nie ubierze;
    /// regula 12.10: bez sztucznych doplat, brak pieniedzy = pozyczka albo mniejsze wojsko, nic z niczego). Sledztwo (nadzy-raport): gra odtwarza pobitego albo
    /// wykupionego lorda nowa druzyna z szablonu klanu (HeroSpawnCampaignBehavior.SpawnLordParty -> LordPartyComponent.InitializeLordPartyProperties ->
    /// InitializeMobilePartyAroundPosition), E4/8 bierze tych ludzi z wolnych rak wsi bez dobytku, LevyGold.PartyCreatedPrefix blokuje darmowy komplet DTE,
    /// a sklad7b (TroopsFightWithOwnKitOnly) puszcza ich w bitwie gracza w tunice z kijem; p9 tylko wstrzymuje ich atak, dozbrajanie jest wolniejsze niz
    /// naplyw nagich druzyn (20/23/10 na dobe, ok. 25% partii lordow ponizej 50% gotowosci).
    ///
    /// (1) NOWA DRUZYNA (klucz NewLordPartyArmedMenOnly; ArmsFromPeople.SpawnPost - droga E4/8, przed rachunkiem szablonu i poborem z DOM): Plan -
    ///     miasto = najblizsze wazne miasto wyjazdu p9 (ArmsFirst.NearestTown: przyjazne, bez oblezenia, droga ladowa, AiArmsTripMaxDays, ta sama strona Muru);
    ///     wycena kompletow ludzi szablonu na JEGO polce (AiGear.QuoteUnits - filtry i kolejnosc BuyLoop, bez zakupu); fundusz = zloto wodza ponad AiGearGoldReserve,
    ///     najwyzej przydzial sprzetu rodu 166 (GearCapAny) plus nadwyzka kies rodziny ponad rezerwe wojny R (NeedOf), plus miejsce pozyczki Banku (LoanRoom - zasady
    ///     drabiny 168: DebtLadder.CanBorrow, IronBank.Limit - dlug, wolny kapital ponad BankFreeCapitalFloor). 159b (regula Jeffa 26.10 ok. 11:20: "lord moze miec
    ///     nieuzbrojonych ale takich co nie maja broni dajmy max 10% armii"): najpierw JEDNA bron na czlowieka (po jednym czlowieku najtansza dostepna pierwsza bron
    ///     jego wzorca - WeaponOpts: bron biala albo rzucana, luk ze strzalami, kusza z beltami; x zapas Margin w funduszu) - kazdy, kto dostaje bron, idzie;
    ///     potem reszta kompletu wzietych, ile starczy funduszu (kolejnosc zakupow AiGear.Order); ludzie bez broni najwyzej UnarmedMenAllowedPercent partii
    ///     (AllowedWith), reszta szablonu zostaje we wsi (zdjeta przed poborem - z ksiegi ludzi nic nie schodzi). Brak miasta w zasiegu - nikt.
    ///     Recenzja 159b: krok "jeszcze jeden uzbrojony" liczony jak CountArms (ArmStep: wszystkie sloty typu, uzbrojeni = max po opcjach, nie suma), a gotowy plan
    ///     sprawdzony ta sama miara na sztukach wyceny (PlanCheck) - bez broni ponad % wedlug niej tez zostaja we wsi.
    ///     Sztuki NIE przenosza sie: druzyna jedzie do tego miasta (ArmsFirst.TripTown - miasto planu; CanAfford przez AiGear.BudgetPreview widzi fundusz planu)
    ///     i kupuje z polki ta sama droga AiGear (VisitBudget w TryBuyCore): przy pierwszej wizycie w miescie budzet = zloto ponad rezerwe (jak wyzej) + sakiewka
    ///     ludzi + pozyczka na brak wobec wyceny TEJ polki (IronBank.Lend - prawdziwa pozyczka z oplata i oprocentowaniem, splata drabiny 168; zloto Banku ->
    ///     glowa rodu -> wodz partii przelewem GiveGoldAction), limit sztuk wizyty = sztuki wyceny. 159b: w tej wizycie najpierw pierwsza bron dla ludzi bez broni
    ///     (FirstWeapons - przebieg BuyLoop przed siodlami i reszta brakow), dopiero potem reszta. Placi wodz, zloto do kasy miasta (AiGear) - nic z niczego.
    ///     Plan wazny 2 x AiArmsTripMaxDays + 2 doby; bez wizyty - wygasa (bez pozyczki). Plan i zegary w zapisie (arm_159_kit).
    /// (2) LUDZIE BEZ SPRZETU DO DOMU (klucz UnarmedMenSentHome): niegotowa partia lorda AI (ArmedStrength Low), ktora nie moze sie dozbroic - ArmsFirst.ThinkPostfix
    ///     zapisuje (NoteStuck) od kiedy i czemu nie ma wyjazdu (nie stac / brak miasta / nie stac w miescie / 2 wizyty bez postepu) - po UnarmedMenSentHomeDays
    ///     dobach (albo zwolniona z czekania p9) - 159b: odsyla tylko ludzi BEZ ZADNEJ BRONI ponad UnarmedMenAllowedPercent partii (ExcessOf; brak zbroi przy broni
    ///     nie odsyla - korpus tylko w logu). "Bez broni" jak bitwa (CountArms): bron wzorca ze zbrojowni DTE (dopasowanie jak GarrisonKit.FillPercent, DTE ubiera
    ///     ludzi po kolei), potem podloga broni GearFit (GearFitWeaponFloor) - bron biala z reszty puli; kij z domu (HomeArms) sie nie liczy. Nadwyzka na typy
    ///     proporcjonalnie do ludzi bez broni (Share), do domu droga zwolnionych 166 (PeopleBook.Take,
    ///     ClanBudget.ToVillagePop159 z przyczyna M166 - ludnosc BK wsi rodu, znani pochodzeniem do DOM swojego regionu, weterani E4/10 - karczma w linii 159, nie
    ///     w linii 166). Wies bez danych BK - nikt nie odchodzi (licznik). Tylko przy czynnych zakupach AI i p9.
    ///     Sakiewki ludzi i zbrojownia zostaja w partii (jak przepelnienie W4c). Bez: armii, bitwy, oblezenia, morza, rozwiazywanej partii, rodu gracza, Strazy, umarlych.
    ///     Recenzja 159b (klucz UnarmedCapAllLordParties): takze partia GOTOWA wedlug p9 - bez broni ponad % i nie stac na pierwsza bron przez UnarmedMenSentHomeDays
    ///     dob - nadwyzka do domu (OverCap); zwykla wizyta lorda AI z bronia ponad % kupuje najpierw pierwsza bron dla nadwyzki (FirstNeed, AiGear.TryBuyCore).
    ///     159c (druga recenzja 159b, 2 uwagi): (a) zegar OverCap biegnie, dopoki nadwyzka bez broni nie maleje (zapis "partia,odH,go"; od nowa tylko przy postepie -
    ///     nadwyzka mniejsza niz przy ostatnim sprawdzeniu), po UnarmedMenSentHomeDays bez postepu nadwyzka do domu bez wzgledu na pieniadze; partia, ktorej p9 nie
    ///     wysyla (gotowa albo zwolniona z czekania), a ktora stac na pierwsza bron dla nadwyzki wedlug wyceny polki, dostaje wyjazd do ArmsFirst.NearestTown
    ///     (ArmsFirst.StartCapTrip - ta sama mysl godzinowa co p9, bez stanu "niegotowa", najwyzej raz na UnarmedMenSentHomeDays dob; w drodze i na postoju zegar
    ///     czeka). (b) "stac go na pierwsza bron" = prawdziwa wycena (QuoteOf: FirstWeapons z limitem nadwyzki na polce miasta, w ktorym partia stoi, albo
    ///     najblizszego wyjazdu p9, dla min(nadwyzka, FirstUnit) ludzi x Margin) wobec AiGear.BudgetPreview - w OverCap, u zwolnionej z czekania p9 i w galezi
    ///     "nie stac" mysli p9 (FirstOk: niegotowa, ktorej nie stac na pelne dozbrojenie, a stac na pierwsza bron dla nadwyzki - wyjazd zamiast "nie stac");
    ///     p9 dalej liczy pelne dozbrajanie swoim CanAfford / MinNeed. Wycena raz na dobe na partie (pamiec _quotes, od nowa po zakupie pierwszej broni).
    ///     159c recenzja 3: postep zegara OverCap = nadwyzka ponizej NAJMNIEJSZEJ od startu zegara (werbunek nie podnosi poprzeczki - "werbunek i jeden polegly"
    ///     nie zeruje zegara); zegar z zapisu po terminie - pierwsza doba po wczytaniu raz wyjazd po pierwsza bron zamiast odsylki, gdy stac (wyjazdy bez zapisu);
    ///     wycena bez sztuk dla nadwyzki - wytchnienie miasta (nastepnej doby kolejne); FirstOk tylko przy UnarmedCapAllLordParties (bez niego wizyta nie kupuje
    ///     najpierw pierwszej broni - FirstNeed).
    /// (3) p9 bez zmian. P0: linia "Bitwa gracza (159 P0)" (kazda bitwa gracza - start spotkania i misja - stan kazdej partii lorda AI) i linia doby "Nowe druzyny i sprzet (159)" (plany,
    ///     pozyczki, zakupy, odeslani, spis partii bez wpisu DTE i z R = 0 wedlug wieku partii). Potkniecia liczone, bez wylacznikow.
    /// </summary>
    internal static class NewLordKit
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const double Margin = 1.10;      // zapas wyceny: zakup ShelfBuy wycenia kazda sztuke od nowa (cena rosnie z ubytkiem polki), wycena - cena pierwszej sztuki
        private const int RepeatDays = 14;       // ponowne odeslanie tej samej partii w tylu dobach - licznik "w kolko" (werbunek -> odeslanie)
        private static readonly string[] WhyCodes = { ArmsFirst.WhyPoor, ArmsFirst.WhyNoTown, ArmsFirst.WhyNoBudgetTown, ArmsFirst.WhyNoGain, "?" };

        private sealed class KitPlan
        {
            internal MobileParty P; internal Settlement Town; internal string TownId;
            internal int Men, Template, Pieces, Day; internal long Cost, Own, Loan; internal bool Done;
            internal int VFirstMen, VFirstPcs; internal long VFirstGold;   // 159b: pierwsza bron w wizycie (tylko przyklad w logu, bez zapisu)
        }
        private sealed class Stuck { internal double SinceH; internal int Why; }

        // recenzja 159 (uwaga 1): plan powstaje w komponencie partii, PRZED CampaignObjectManager.AddMobileParty (dopiero tam Id), a MBObjectBase.GetHashCode = Id.GetHashCode
        // (MBGUID: (int)_internalValue, przed nadaniem 0) - slownik z domyslnym porownaniem zapisywal plan z haszem 0 i zaden pozniejszy odczyt (haszem prawdziwego Id)
        // go nie znajdowal. Plany po referencji obiektu partii (hasz niezalezny od Id); zapis i zapis z wczytania - po StringId jak dotad
        private sealed class RefEq : IEqualityComparer<MobileParty>
        {
            internal static readonly RefEq I = new RefEq();
            public bool Equals(MobileParty a, MobileParty b) { return ReferenceEquals(a, b); }
            public int GetHashCode(MobileParty o) { return RuntimeHelpers.GetHashCode(o); }
        }
        private static readonly Dictionary<MobileParty, KitPlan> _plans = new Dictionary<MobileParty, KitPlan>(RefEq.I);
        private static readonly Dictionary<string, KitPlan> _savedPlans = new Dictionary<string, KitPlan>(StringComparer.Ordinal);
        private static readonly Dictionary<MobileParty, Stuck> _stuck = new Dictionary<MobileParty, Stuck>();
        private static readonly Dictionary<string, Stuck> _savedStuck = new Dictionary<string, Stuck>(StringComparer.Ordinal);
        private static readonly Dictionary<MobileParty, int> _born = new Dictionary<MobileParty, int>();
        private static readonly Dictionary<string, int> _savedBorn = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly Dictionary<MobileParty, int> _lastHome = new Dictionary<MobileParty, int>();
        private static WeakReference _lastBattle;
        private static string _lastBattleWhere;   // recenzja 159 (uwaga 5): linia raz na bitwe I zrodlo (start spotkania - bez dolaczajacych z okolicy; misja - pelny sklad)
        private static int _importN = -1, _importBad;

        // liczniki doby (linia "Nowe druzyny i sprzet (159)")
        private static int _dPlanParties, _dOff, _dStart, _dLate, _dNoGear, _dNoTown, _dZero, _dStumbles, _stumbles;
        private static int _dFreeKit, _dNoP9;   // recenzja 159 (uwagi 3, 7): bez planu - darmowy komplet DTE wlaczony (NoFreeKitForNewParties NIE), p9 wylaczone
        private static long _dTemplate, _dTaken, _dNoTownMen, _dPlanPieces, _dPlanCost, _dPlanOwn, _dPlanLoan;
        private static int _dVisits, _dVisitEmpty, _dLoans, _dExpired, _dDestroyed, _dLoanCapped;   // _dLoanCapped - recenzja (uwaga 2): pozyczka przycieta do planu
        private static long _dLoanSum, _dVisitSpent, _dVisitPieces, _dLoanLeft, _dVisitOwn;
        private static readonly Dictionary<string, int> _dLoanNo = new Dictionary<string, int>(StringComparer.Ordinal);
        private static int _dHomeParties, _dHomeNothing, _dHomeRepeat, _dHomeSkipBusy, _dHomeSkipPlayer, _dHomeSkipOther;
        private static long _dHomeMen, _dHomeWounded, _dHomeNoVillage, _dHomeTavern;   // _dHomeTavern - recenzja (uwaga 6): z odeslanych staneli w karczmie (E4/10)
        // 159b (regula 10%): plan - z bronia / bez broni w ramach % / dozwolone, pierwsza bron (szt., zl), bez korpusu u wzietych z bronia (info);
        // wizyta - pierwsza bron dla ludzi bez broni; regula 2 - sprawdzone partie: ludzie, bez broni po puli, z puli, w ramach %, ponad %, bez korpusu (info)
        private static long _dArmed, _dUnarmed, _dAllowed, _dFirstPieces, _dFirstCost, _dPlanNoBody;
        private static long _dVisitFirstMen, _dVisitFirstPieces, _dVisitFirstGold;
        private static int _dHomeChecked;
        private static long _dHomeAll, _dHomeNoW, _dHomePool, _dHomeKeep, _dHomeOver, _dHomeNoBody;
        // recenzja 159b: plan - przycieci miara bitwy (PlanCheck); wizyty - kolejne przebiegi pierwszej broni (FirstAgain); uwaga 2 - partie gotowe wedlug p9
        // (UnarmedCapAllLordParties): sprawdzone, ponad %, stac (pierwsza bron przy wizycie), zegar biegnie, w ruchu (bitwa/oblezenie/morze...), do domu (partie);
        // zwykle wizyty z pierwsza bronia ponad % (wizyty, ludzi, szt., zl)
        private static long _dPlanCut, _dFirstRounds;
        private static int _dCapChecked, _dCapOver, _dCapWait, _dCapBusy, _dCapHome;
        private static long _dCapVisits, _dCapVisitMen, _dCapVisitPieces, _dCapVisitGold;
        // 159c (druga recenzja 159b): zegar ponad % - od kiedy (h) bez postepu i nadwyzka przy ostatnim sprawdzeniu (-1 - z zapisu sprzed 159c, nieznana)
        // 159c recenzja 3: Go - NAJMNIEJSZA nadwyzka od startu zegara (postep = ponizej niej); Loaded - wpis z zapisu, jeszcze bez sprawdzenia terminu (bez zapisu)
        private sealed class Over { internal double SinceH; internal int Go = -1; internal bool Loaded; }
        private static readonly Dictionary<MobileParty, Over> _over = new Dictionary<MobileParty, Over>();               // partia, ktorej p9 nie wysyla, z bronia ponad %
        private static readonly Dictionary<string, Over> _savedOver = new Dictionary<string, Over>(StringComparer.Ordinal);
        // 159c: wycena pierwszej broni dla nadwyzki (QuoteOf) - raz na dobe na partie; Town null - brak miasta w zasiegu
        private const int FirstUnit = 10;   // "stac go" = pierwsza bron dla min(nadwyzka, 10) ludzi (jak p9 PiecesUnit: zakup rozlozony na wizyty, reszta - zegar)
        private sealed class Quote { internal int Day, Go, Men, Budget; internal long Cost; internal Settlement Town; }
        private static readonly Dictionary<MobileParty, Quote> _quotes = new Dictionary<MobileParty, Quote>(RefEq.I);
        // 159c liczniki doby (2b): zwolnione z czekania p9 w OverCap, zegar od nowa po postepie, zegar czeka (wyjazd w toku), wyjazdy po pierwsza bron i powody
        // bez wyjazdu, zdarzenia wyjazdu (ArmsFirst: dojechaly, po postoju, za dlugo w drodze, przerwane), wyceny (liczba, takty Stopwatch)
        private static int _dCapFree, _dCapProgress, _dCapTripping, _dCapTrips, _dCapNoTown, _dCapNoShelf, _dCapPoor, _dCapSpaced, _dQuotes, _dCapLoadTrip;
        private static long _dQuoteTicks;
        private static readonly int[] _dCapTripEv = new int[4];
        internal const int CapArrived = 0, CapDone = 1, CapLate = 2, CapEnded = 3;
        private static readonly HashSet<MobileParty> _dLowFirst = new HashSet<MobileParty>(), _dLowFirstNoTrip = new HashSet<MobileParty>();
        private static readonly List<string> _dEx = new List<string>();
        private static readonly HashSet<string> _err = new HashSet<string>();

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            try { if (_err.Add(where)) Log.Error("NewLordKit." + where, e); } catch { }
        }

        private static readonly int[] _dExN = new int[4];   // przyklady na rodzaj: 0 plan, 1 wizyta, 2 do domu, 3 wyjazd po pierwsza bron (159c) (po 2)
        private const int ExPlan = 0, ExVisit = 1, ExHome = 2, ExCap = 3;
        private static void Ex(int kind, string s) { if (kind >= 0 && kind < _dExN.Length && _dExN[kind] < 2) { _dExN[kind]++; _dEx.Add(s); } }

        internal static void Reset()
        {
            _plans.Clear(); _savedPlans.Clear(); _stuck.Clear(); _savedStuck.Clear(); _born.Clear(); _savedBorn.Clear(); _lastHome.Clear(); _over.Clear(); _savedOver.Clear(); _quotes.Clear();
            _lastBattle = null; _lastBattleWhere = null; _importN = -1; _importBad = 0; _stumbles = 0; _err.Clear();
            ClearDay();
        }

        private static void ClearDay()
        {
            _dPlanParties = _dOff = _dStart = _dLate = _dNoGear = _dNoTown = _dZero = _dStumbles = 0;
            _dFreeKit = _dNoP9 = 0;
            _dTemplate = _dTaken = _dNoTownMen = _dPlanPieces = _dPlanCost = _dPlanOwn = _dPlanLoan = 0;
            _dVisits = _dVisitEmpty = _dLoans = _dExpired = _dDestroyed = _dLoanCapped = 0;
            _dLoanSum = _dVisitSpent = _dVisitPieces = _dLoanLeft = _dVisitOwn = 0;
            _dLoanNo.Clear();
            _dHomeParties = _dHomeNothing = _dHomeRepeat = _dHomeSkipBusy = _dHomeSkipPlayer = _dHomeSkipOther = 0;
            _dHomeMen = _dHomeWounded = _dHomeNoVillage = _dHomeTavern = 0;
            _dArmed = _dUnarmed = _dAllowed = _dFirstPieces = _dFirstCost = _dPlanNoBody = 0;
            _dVisitFirstMen = _dVisitFirstPieces = _dVisitFirstGold = 0;
            _dHomeChecked = 0;
            _dHomeAll = _dHomeNoW = _dHomePool = _dHomeKeep = _dHomeOver = _dHomeNoBody = 0;
            _dPlanCut = _dFirstRounds = 0;
            _dCapChecked = _dCapOver = _dCapWait = _dCapBusy = _dCapHome = 0;
            _dCapVisits = _dCapVisitMen = _dCapVisitPieces = _dCapVisitGold = 0;
            _dCapFree = _dCapProgress = _dCapTripping = _dCapTrips = _dCapNoTown = _dCapNoShelf = _dCapPoor = _dCapSpaced = _dQuotes = _dCapLoadTrip = 0;
            _dQuoteTicks = 0; Array.Clear(_dCapTripEv, 0, _dCapTripEv.Length); _dLowFirst.Clear(); _dLowFirstNoTrip.Clear();
            _dEx.Clear(); Array.Clear(_dExN, 0, _dExN.Length);
            AiGear.QuoteStumbles = 0;
        }

        private static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }
        internal static int HomeDays(Settings s) { return Clamp(s.UnarmedMenSentHomeDays, 1, 30); }

        // ------------------------------------------------------------ 159b: regula Jeffa 10% (26.10 ok. 11:20 "max 10% armii" - tylko ludzie bez broni)
        /// <summary>159b: odsetek partii, ktory moga stanowic ludzie bez zadnej broni - UnarmedMenAllowedPercent przyciete do 0..50 (NaN - 0).</summary>
        internal static double AllowedPct(Settings s)
        {
            float v = s != null ? s.UnarmedMenAllowedPercent : 0f;
            if (float.IsNaN(v) || float.IsInfinity(v)) return 0.0;
            return Math.Max(0.0, Math.Min(50.0, v));
        }

        private static string PctTxt(double pct) { return pct.ToString("0.#", Inv); }

        /// <summary>159b: ilu ludzi bez broni moze dojsc do `armed` uzbrojonych, zeby bez broni bylo najwyzej pct% partii: u / (a + u) &lt;= p  &lt;=&gt;  u &lt;= a p / (100 - p).
        /// 0% - zero (nikt bez broni).</summary>
        private static int AllowedWith(long armed, double pct)
        {
            if (armed <= 0 || pct <= 0) return 0;
            return (int)Math.Min(int.MaxValue, Math.Floor(armed * pct / (100.0 - pct) + 1e-9));
        }

        /// <summary>159b: ilu z `unarmed` ludzi bez broni w partii `men` ludzi odeslac, zeby bez broni zostalo najwyzej pct%: po odeslaniu x (u - x) &lt;= p (m - x) / 100
        /// &lt;=&gt; x &gt;= (100 u - p m) / (100 - p). 0% - wszyscy bez broni (ta sama granica co AllowedWith przy planie).</summary>
        private static int ExcessOf(int unarmed, int men, double pct)
        {
            if (unarmed <= 0) return 0;
            double x = Math.Ceiling((100.0 * unarmed - pct * men) / (100.0 - pct) - 1e-9);
            return (int)Math.Max(0.0, Math.Min(unarmed, x));
        }

        // 159b: typy broni, ktore same uzbrajaja (GearFit.Armed: bron biala i rzucana); luk i kusza - tylko z amunicja
        private static bool SelfArms(ItemObject.ItemTypeEnum t)
        {
            return t == ItemObject.ItemTypeEnum.OneHandedWeapon || t == ItemObject.ItemTypeEnum.TwoHandedWeapon || t == ItemObject.ItemTypeEnum.Polearm
                   || t == ItemObject.ItemTypeEnum.Thrown;
        }

        /// <summary>159b: mozliwe "pierwsze bronie" czlowieka typu ch - koszyki NeedKey (AiGear.NeedKey, jak KitBuckets - pierwszy zestaw bojowy) broni wzorca ze slotow
        /// 0-3: bron biala i rzucana na typ, luk ze strzalami i kusza z beltami razem (sam luk nie uzbraja - GearFit.Armed). Pusta - wzorzec bez broni.
        /// Recenzja 159b (uwaga 1): opcja to WSZYSTKIE sloty 0-3 jej typu (dwa kolczany, dwa peki oszczepow, dwie bronie drzewcowe - po sztuce na slot), jak miara
        /// reguly (2): CountArms liczy wpisy MenOf MenPerSlot (ludzie x QuartermasterLaw.SlotsOfType), a DTE (PartyEquipmentDistributor.AssignWeaponByWeaponClass -
        /// petla po ludziach, w niej sloty 0-3) daje pierwszemu czlowiekowi sztuke na kazdy jego slot. Dotad jedna sztuka na opcje: 20 lucznikow z dwoma slotami
        /// strzal dostawalo w planie 20 lukow i 20 kolczanow (20 "z bronia"), a CountArms (i bitwa) - 10 z lukiem i strzalami, 10 z samym lukiem.</summary>
        private static List<int[]> WeaponOpts(CharacterObject ch, bool oneUp)
        {
            var l = new List<int[]>();
            if (ch == null || ch.IsHero) return l;
            int kn = Math.Max(1, GoldenKits.Count(ch));
            Equipment eq = GoldenKits.Kit(ch, 0, kn);
            if (eq == null) return l;
            var byType = new Dictionary<ItemObject.ItemTypeEnum, List<int>>();
            var order = new List<ItemObject.ItemTypeEnum>();
            for (int sl = 0; sl < 4; sl++)
            {
                var it = eq[(EquipmentIndex)sl].Item;
                if (it == null || !SupplyDemand.Equipmentish(it)) continue;
                var t = it.ItemType;
                List<int> ks;
                if (!byType.TryGetValue(t, out ks)) { byType[t] = ks = new List<int>(); order.Add(t); }
                ks.Add(AiGear.NeedKey(ch, it, oneUp));
            }
            foreach (var t in order) if (SelfArms(t)) l.Add(byType[t].ToArray());
            AddPair(l, byType, ItemObject.ItemTypeEnum.Bow, ItemObject.ItemTypeEnum.Arrows);
            AddPair(l, byType, ItemObject.ItemTypeEnum.Crossbow, ItemObject.ItemTypeEnum.Bolts);
            return l;
        }

        private static void AddPair(List<int[]> l, Dictionary<ItemObject.ItemTypeEnum, List<int>> byType, ItemObject.ItemTypeEnum arm, ItemObject.ItemTypeEnum ammo)
        {
            List<int> a, b;
            if (!byType.TryGetValue(arm, out a) || !byType.TryGetValue(ammo, out b)) return;
            var o = new List<int>(a); o.AddRange(b);
            l.Add(o.ToArray());
        }

        /// <summary>
        /// Recenzja 159b (uwaga 1): oddzial w kroku "jeszcze jeden uzbrojony" (plan (a) i FirstWeapons) liczony jak CountArms: Cov - ludzie z bronia wzorca na typ
        /// ArmTypes, Armed = ArmedOf(Cov) (max po bron biala / rzucana / luk ze strzalami / kusza z beltami - ci sami pierwsi ludzie, nie suma), Left - ilu jeszcze
        /// mozna uzbroic. Dotad krok liczyl czlowieka uzbrojonego jedna opcja kupiona dla niego: 6 ludzi z bronia jednoreczna (polka sie skonczyla) i 4 z drzewcem
        /// to w planie 10 uzbrojonych, a w CountArms (i w bitwie - DTE daje obie sztuki pierwszym ludziom) max(6, 4) = 6.
        /// </summary>
        private sealed class ArmStep { internal CharacterObject Ch; internal int Left, Armed; internal int[] Cov; internal List<int[]> Opts; }

        private static int TypeIdx(int key) { return Array.IndexOf(ArmTypes, (ItemObject.ItemTypeEnum)(key / 10)); }

        /// <summary>Recenzja 159b: koszyki kroku - opcja o uzbraja czlowieka Armed + 1, gdy kazdy jej typ jest u Armed + 1 ludzi: na typ brakujacy ludzie x kazdy
        /// jego slot w opcji (ten sam koszyk kilka razy - kolejne sztuki). Pusta - opcja nic nie daje.</summary>
        private static List<int> StepKeys(ArmStep g, int[] o)
        {
            var l = new List<int>();
            int target = g.Armed + 1;
            foreach (var k in o)
            {
                int ti = TypeIdx(k);
                if (ti < 0) continue;
                for (int j = Math.Max(0, target - g.Cov[ti]); j > 0; j--) l.Add(k);
            }
            return l;
        }

        private static void StepApply(ArmStep g, int[] o)
        {
            int target = g.Armed + 1;
            foreach (var k in o) { int ti = TypeIdx(k); if (ti >= 0 && g.Cov[ti] < target) g.Cov[ti] = target; }
            g.Armed = Math.Max(target, ArmedOf(g.Cov)); g.Left--;
        }

        /// <summary>159b: cena kolejnych sztuk koszykow kroku (units - wycena QuoteUnits, pos - zuzyte; ten sam koszyk kilka razy - kolejne sztuki); -1 - pusty krok
        /// albo sztuk koszyka juz nie ma na polce.</summary>
        private static long KeysCost(List<int> keys, Dictionary<int, List<int>> units, Dictionary<int, int> pos)
        {
            if (keys == null || keys.Count == 0) return -1;
            long c = 0;
            var off = new Dictionary<int, int>();
            foreach (var k in keys)
            {
                List<int> u;
                if (!units.TryGetValue(k, out u)) return -1;
                int p0, o; pos.TryGetValue(k, out p0); off.TryGetValue(k, out o);
                if (p0 + o >= u.Count) return -1;
                c += u[p0 + o];
                off[k] = o + 1;
            }
            return c;
        }

        /// <summary>Recenzja 159b: zuzycie sztuk kroku; items/trial - przedmiot zuzytej sztuki do zbrojowni na probe planu (CountArms; null - bez).</summary>
        private static void KeysTake(List<int> keys, Dictionary<int, int> pos, Dictionary<int, List<ItemObject>> items, Dictionary<ItemObject, int> trial)
        {
            foreach (var k in keys) { int p0; pos.TryGetValue(k, out p0); TrialAdd(trial, items, k, p0); pos[k] = p0 + 1; }
        }

        private static void TrialAdd(Dictionary<ItemObject, int> trial, Dictionary<int, List<ItemObject>> items, int k, int j)
        {
            List<ItemObject> l;
            if (trial == null || items == null || !items.TryGetValue(k, out l) || j < 0 || j >= l.Count || l[j] == null) return;
            int v; trial.TryGetValue(l[j], out v); trial[l[j]] = v + 1;
        }
        private static int Today { get { return (int)CampaignTime.Now.ToDays; } }
        private static string Nm(MobileParty mp) { try { return mp != null && mp.Name != null ? mp.Name.ToString() : "?"; } catch { return "?"; } }
        private static string Nm(Settlement st) { try { return st != null && st.Name != null ? st.Name.ToString() : "-"; } catch { return "?"; } }

        // ------------------------------------------------------------ zdarzenia
        internal static void RegisterEvents(CampaignBehaviorBase owner)
        {
            CampaignEvents.MobilePartyCreated.AddNonSerializedListener(owner, OnCreated);
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(owner, OnDestroyed);
            CampaignEvents.MapEventStarted.AddNonSerializedListener(owner, OnMapEventStarted);
        }

        private static void OnCreated(MobileParty mp)
        {
            try { if (mp != null && mp.IsLordParty && !mp.IsMainParty) _born[mp] = Today; } catch (Exception e) { Stumble("OnCreated", e); }
        }

        private static void OnDestroyed(MobileParty mp, PartyBase pb)
        {
            try
            {
                if (mp == null) return;
                KitPlan p;
                if (_plans.TryGetValue(mp, out p)) { if (!p.Done) _dDestroyed++; _plans.Remove(mp); }
                _stuck.Remove(mp); _born.Remove(mp); _lastHome.Remove(mp); _over.Remove(mp); _quotes.Remove(mp);
            }
            catch (Exception e) { Stumble("OnDestroyed", e); }
        }

        private static void OnMapEventStarted(MapEvent me, PartyBase a, PartyBase d)
        {
            try { if (me != null && (a == PartyBase.MainParty || d == PartyBase.MainParty)) BattleLine(me, "start spotkania"); } catch (Exception e) { Stumble("OnMapEventStarted", e); }
        }

        // ------------------------------------------------------------ fundusz i pozyczka
        /// <summary>Zloto wodza na sprzet nowych ludzi: ponad AiGearGoldReserve, najwyzej przydzial sprzetu rodu 166 (takze przed pierwszym budzetem sesji - GearCapAny)
        /// plus nadwyzka kies rodziny ponad rezerwe wojny R (NeedOf). Rod bez budzetu - cale zloto ponad rezerwe. Tylko odczyt.</summary>
        private static int OwnFund(Hero lord, Settings s)
        {
            if (lord == null || !lord.IsAlive) return 0;
            int above = Math.Max(0, lord.Gold - Math.Max(0, s.AiGearGoldReserve));
            var c = lord.Clan;
            int gear;
            if (c == null || !ClanBudget.GearCapAny(c, out gear)) return above;
            double cap = Math.Max(0, gear), G, R, D; int streak;
            if (ClanBudget.NeedOf(c, out G, out R, out D, out streak) && G > R) cap += G - R;
            return (int)Math.Min(above, Math.Min(int.MaxValue, cap));
        }

        /// <summary>Miejsce pozyczki Banku na sprzet - zasady drabiny 168 (DebtLadder.CanBorrow: bez zajecia, zaleglosci, karencji, bankructwa, korony w zaleglosci, wolny
        /// kapital Banku), IronBank.Limit (przy drabinie DebtLadder.Limit) minus dlug, najwyzej wolny kapital ponad BankFreeCapitalFloor. Tylko rody krolestw (jak
        /// Bank przed drabina), bez gracza i mniejszych frakcji. why - powod 0 (do linii). Tylko odczyt.</summary>
        private static int LoanRoom(Clan c, out string why)
        {
            why = null;
            var s = Settings.Current;
            if (s == null || !s.NewLordPartyKitLoan) { why = "pozyczka wylaczona (MCM)"; return 0; }
            if (!IronBank.On || !DebtLadder.On) { why = "Bank albo drabina dlugu wylaczone"; return 0; }
            if (c == null || c == Clan.PlayerClan || c.Leader == null || c.Kingdom == null || c.IsMinorFaction || c.IsEliminated) { why = "rod poza krolestwem (Bank pozycza rodom krolestw)"; return 0; }
            string w;
            if (!DebtLadder.CanBorrow(c, out w))
            {
                var d0 = IronBank.DebtOf(c, false);
                if (DebtLadder.IsSeized(c)) why = "rod w zajeciu";
                else if (DebtLadder.StageOf(c) == "zaleglosc") why = "zaleglosc rat";
                else if (d0 != null && d0.Defaulted) why = "bankrut Banku";
                else if (CrownBorrow.CrownInArrears(c)) why = "korona w zaleglosci w Banku";
                else if (IronBank.FreeCapital < Math.Max(0, s.BankFreeCapitalFloor)) why = "Bank ponizej progu wolnego kapitalu";
                else why = "karencja po zajeciu";
                return 0;
            }
            var d = IronBank.DebtOf(c, false);
            double room = IronBank.Limit(c) - (d != null ? d.Principal : 0);
            double free = IronBank.FreeCapital - Math.Max(0, s.BankFreeCapitalFloor);
            if (room < 1) { why = "na limicie Banku"; return 0; }
            if (free < 1) { why = "Bank ponizej progu wolnego kapitalu"; return 0; }
            return (int)Math.Max(0, Math.Min(int.MaxValue, Math.Min(room, free)));
        }

        /// <summary>Pozyczka na brak: kapital Banku -> kiesa glowy rodu (IronBank.Lend - oplata, oprocentowanie, splata drabiny 168), potem glowa -> wodz partii
        /// (GiveGoldAction, gdy wodz nie jest glowa). Zwraca zloto, ktore dotarlo do wodza.</summary>
        private static int Borrow(Clan c, Hero lord, int amount, out string why)
        {
            int room = LoanRoom(c, out why);
            int ask = Math.Min(amount, room);
            if (ask <= 0) return 0;
            int got = IronBank.Lend(c, ask, "sprzet nowej druzyny (159)");
            if (got <= 0) { why = "Bank nie wyplacil (limit albo kapital)"; return 0; }
            if (lord == null || lord == c.Leader) return got;
            int before = lord.Gold;
            try { GiveGoldAction.ApplyBetweenCharacters(c.Leader, lord, got, true); } catch (Exception e) { Stumble("Borrow(przelew)", e); }
            return Math.Max(0, lord.Gold - before);
        }

        private static void NoLoan(string why) { if (string.IsNullOrEmpty(why)) return; int v; _dLoanNo.TryGetValue(why, out v); _dLoanNo[why] = v + 1; }

        // ------------------------------------------------------------ (1) plan przy tworzeniu (ArmsFromPeople.SpawnPost, E4/8)
        private sealed class Kind { internal CharacterObject Ch; internal int Count, Taken; internal List<int> Buckets; internal List<int[]> Opts; }

        /// <summary>
        /// Wolane z ArmsFromPeople.SpawnPost (droga E4/8, przed rachunkiem szablonu): ile ludzi kazdego typu z dopisanych przez gre wziac. null - bez zmian (klucz
        /// wylaczony, start gry, okno nowej gry ColdStart, zakupy AI wylaczone, p9 wylaczone, darmowy komplet DTE wlaczony, rod gracza, umarli). Pusty slownik - nikt (brak miasta w zasiegu albo nic nie da
        /// sie ubrac). Partia nie ma jeszcze Id (CampaignObjectManager.AddMobileParty po komponencie) - plan po referencji obiektu partii (RefEq; hasz MBObjectBase to Id).
        /// </summary>
        internal static Dictionary<CharacterObject, int> Plan(MobileParty mp, Hero owner, Dictionary<CharacterObject, int> added)
        {
            var s = Settings.Current;
            if (s == null || added == null || mp == null || owner == null) return null;
            if (!s.NewLordPartyArmedMenOnly) { _dOff++; return null; }
            if (Campaign.Current == null || !Campaign.Current.GameStarted) { _dStart++; return null; }   // nowa gra - swiat dostaje komplet DTE (LevyGold)
            if (mp.IsMainParty || owner.Clan == Clan.PlayerClan || Undead.Party(mp)) return null;
            if (ColdStart.LateWindowFor(mp)) { _dLate++; return null; }   // okno nowej gry: dorobek stuleci (ColdStart.LateLord)
            if (!AiGear.On) { _dNoGear++; return null; }                   // nikt nie kupuje - jak dotad
            // recenzja 159 (uwaga 7): wyjazd do miasta planu prowadzi p9 (ArmsFirst.ThinkPostfix -> TripTown) - p9 wylaczone, sklad jak dotad
            if (!s.AiArmsBeforeBattle) { _dNoP9++; return null; }
            // recenzja 159 (uwaga 3): darmowy komplet DTE wlaczony (LevyGold.PartyCreatedPrefix przepuszcza OnMobilePartyCreated) - kazdy wziety dostanie sprzet rekruta
            if (!s.NoFreeKitForNewParties) { _dFreeKit++; return null; }
            var keep = new Dictionary<CharacterObject, int>();
            int template = 0;
            foreach (var v in added.Values) template += Math.Max(0, v);
            if (template <= 0) return null;
            _dPlanParties++; _dTemplate += template;
            var lord = mp.LeaderHero ?? owner;
            bool failed;
            var town = ArmsFirst.NearestTown(mp, out failed);
            if (failed) { _dPlanParties--; _dTemplate -= template; _dStumbles++; _stumbles++; return null; }   // wyjatek przy szukaniu miasta - sklad bez zmian (nie "brak miasta")
            if (town == null)
            {
                _dNoTown++; _dNoTownMen += template;
                Ex(ExPlan, Nm(mp) + ": szablon " + template + " -> 0 (brak miasta w zasiegu " + ArmsFirst.TripDays(s) + " dob)");
                return keep;
            }
            bool oneUp = s.MenUpgradeOneTierUp;
            var kinds = new List<Kind>();
            var need = new Dictionary<int, int>();
            var lifters = new Dictionary<int, List<CharacterObject>>();
            int other = 0;
            foreach (var kv in added)
            {
                var ch = kv.Key;
                if (ch == null || kv.Value <= 0) continue;
                if (ch.IsHero || PeopleBook.IsWight(ch)) { keep[ch] = kv.Value; other += kv.Value; continue; }   // bez kompletu z polki - jak dotad
                var b = AiGear.KitBuckets(ch, oneUp);
                kinds.Add(new Kind { Ch = ch, Count = kv.Value, Buckets = b, Opts = WeaponOpts(ch, oneUp) });
                foreach (var k in b)
                {
                    int n; need.TryGetValue(k, out n); need[k] = n + kv.Value;
                    List<CharacterObject> l;
                    if (!lifters.TryGetValue(k, out l)) lifters[k] = l = new List<CharacterObject>();
                    if (!l.Contains(ch)) l.Add(ch);
                }
            }
            var items = new Dictionary<int, List<ItemObject>>();   // recenzja 159b (uwaga 1): przedmioty sztuk wyceny - zbrojownia na probe (CountArms)
            var units = AiGear.QuoteUnits(town, mp, need, lifters, items);
            if (units == null) { _dPlanParties--; _dTemplate -= template; _dStumbles++; _stumbles++; return null; }   // wycena nieudana - sklad bez zmian
            var trial = new Dictionary<ItemObject, int>();
            int own = OwnFund(lord, s);
            string why;
            int room = LoanRoom(lord.Clan, out why);
            double fund = (double)own + room;
            double pct = AllowedPct(s);
            var pos = new Dictionary<int, int>();
            long spent = 0; int pieces = 0, armed = 0;
            // 159b (a) najpierw bron (regula Jeffa 26.10): po jednym czlowieku najtanszy krok "jeszcze jeden uzbrojony" wsrod wszystkich typow (WeaponOpts) -
            // najwiecej uzbrojonych za fundusz; kazdy, kto dostaje bron, idzie. Najtanszy nie miesci sie w funduszu (x Margin) - zaden inny tez nie.
            // Recenzja 159b (uwaga 1): krok liczony jak CountArms (ArmStep: pokrycie na typ, wszystkie sloty typu, uzbrojeni = max po opcjach, nie suma)
            var firstUsed = new Dictionary<int, int>();
            var steps = new List<ArmStep>();
            foreach (var kd in kinds) steps.Add(new ArmStep { Ch = kd.Ch, Left = kd.Count, Cov = new int[ArmTypes.Length], Opts = kd.Opts });
            while (true)
            {
                int best = -1; int[] bo = null; List<int> bk = null; long bc = long.MaxValue;
                for (int i = 0; i < steps.Count; i++)
                {
                    var g = steps[i];
                    if (g.Left <= 0) continue;
                    foreach (var o in g.Opts) { var ks = StepKeys(g, o); long c = KeysCost(ks, units, pos); if (c >= 0 && c < bc) { best = i; bo = o; bk = ks; bc = c; } }
                }
                if (best < 0 || (spent + bc) * Margin > fund + 1e-6) break;
                KeysTake(bk, pos, items, trial);
                foreach (var k in bk) { int v; firstUsed.TryGetValue(k, out v); firstUsed[k] = v + 1; }
                StepApply(steps[best], bo);
                kinds[best].Taken++; armed++; spent += bc; pieces += bk.Count;
            }
            long firstCost = spent; int firstPieces = pieces;
            // (b) reszta kompletu wzietych z bronia, ile starczy funduszu - kolejnosc zakupow AiGear (typy Order: korpus pierwszy, tiery 6..1); sztuka za droga
            // na reszte funduszu pominieta, tansza dalej w kolejce moze wejsc (BuyLoop bierze kazdego kandydata w budzecie)
            var rest = new Dictionary<int, int>();
            long bodyMen = 0, bodyBought = 0;
            foreach (var kd in kinds)
            {
                if (kd.Taken <= 0) continue;
                bool body = false;
                foreach (var k in kd.Buckets)
                {
                    int v; rest.TryGetValue(k, out v); rest[k] = v + kd.Taken;
                    if (k / 10 == (int)ItemObject.ItemTypeEnum.BodyArmor) body = true;
                }
                if (body) bodyMen += kd.Taken;
            }
            foreach (var kv in firstUsed) { int v; if (rest.TryGetValue(kv.Key, out v)) rest[kv.Key] = Math.Max(0, v - kv.Value); }
            foreach (var type in AiGear.Order)
                for (int t = 6; t >= 1; t--)
                {
                    int k = (int)type * 10 + t, d;
                    List<int> u;
                    if (!rest.TryGetValue(k, out d) || d <= 0 || !units.TryGetValue(k, out u)) continue;
                    int p0; pos.TryGetValue(k, out p0);
                    for (int j = p0; j < u.Count && d > 0; j++)
                    {
                        if ((spent + u[j]) * Margin > fund + 1e-6) continue;
                        spent += u[j]; pieces++; d--;
                        TrialAdd(trial, items, k, j);
                        if (type == ItemObject.ItemTypeEnum.BodyArmor) bodyBought++;
                    }
                }
            // (c) ludzie bez broni - najwyzej pct% partii (AllowedWith; 0% - nikt); z typow z najwieksza reszta szablonu. Reszta zostaje we wsi
            int maxU = AllowedWith(armed, pct), unarmed = 0;
            while (unarmed < maxU)
            {
                int bi = -1, br = 0;
                for (int i = 0; i < kinds.Count; i++) { int r = kinds[i].Count - kinds[i].Taken; if (r > br) { bi = i; br = r; } }
                if (bi < 0) break;
                kinds[bi].Taken++; unarmed++;
            }
            // (d) recenzja 159b (uwaga 1): sprawdzenie miara reguly (2) - CountArms na wzietych i sztukach wyceny (PlanCheck); ludzie bez broni ponad pct% wedlug
            // tej miary zostaja we wsi. Wyjatek - liczby planu jak wyzej (bez przyciecia)
            int cut = 0;
            try { cut = PlanCheck(kinds, trial, pct, ref armed, ref unarmed); }
            catch (Exception e) { Stumble("Plan(CountArms)", e); }
            maxU = AllowedWith(armed, pct);
            int taken = 0;
            foreach (var kd in kinds) { keep[kd.Ch] = kd.Taken; taken += kd.Taken; }
            long noBody = Math.Max(0, bodyMen - bodyBought);
            _dTaken += taken + other; _dArmed += armed; _dUnarmed += unarmed; _dAllowed += maxU; _dPlanCut += cut;
            long plannedLoan = Math.Max(0, Math.Min(room, (long)Math.Ceiling(spent * Margin) - own));
            if (armed > 0 && pieces > 0)
            {
                _plans[mp] = new KitPlan { P = mp, Town = town, TownId = town.StringId, Men = taken, Template = template, Pieces = pieces, Day = Today, Cost = spent, Own = own, Loan = plannedLoan };
                _dPlanPieces += pieces; _dPlanCost += spent; _dPlanOwn += Math.Min(own, (long)Math.Ceiling(spent * Margin)); _dPlanLoan += plannedLoan;
                _dFirstPieces += firstPieces; _dFirstCost += firstCost; _dPlanNoBody += noBody;
            }
            if (taken + other <= 0) _dZero++;
            if (room <= 0) NoLoan(why);
            Ex(ExPlan, Nm(mp) + ": szablon " + template + " -> wzieci " + (taken + other) + " (z bronia " + armed + ", bez broni " + unarmed + " z dozwolonych " + maxU
               + " = " + PctTxt(pct) + "% - miara bitwy CountArms, przyciete nia " + cut + "; zostali we wsi bez broni ponad % " + (template - taken - other) + "; bez zbroi korpusu u wzietych z bronia (info) " + noBody
               + ") (miasto " + Nm(town) + ", wycena " + pieces + " szt. za " + spent + " zl - najpierw bron " + firstPieces + " szt. za " + firstCost + " zl, wlasne " + own
               + ", miejsce pozyczki " + room + (room <= 0 && why != null ? " - " + why : "") + ")");
            return keep;
        }

        /// <summary>
        /// Recenzja 159b (uwaga 1): plan sprawdzony ta sama miara co regula (2) - CountArms na ludziach wzietych (TroopRoster na probe) i sztukach wyceny planu
        /// (zbrojownia na probe: pierwsza bron i reszta kompletu - przedmioty z QuoteUnits). Krok (a) planu liczy czlowieka uzbrojonego sztukami kupionymi DLA
        /// niego, a CountArms (jak DTE) rozdaje sztuki typu po oddzialach od najwyzszego skilla (MenOf) na kazdy slot wzorca: sztuka kupiona dla pospolitego
        /// ruszenia z sama bronia jednoreczna trafia na slot jednoreczny zbrojnego, ktory juz ma drzewce - i pospolity jest bez broni. Ludzie bez broni ponad
        /// pct% (ExcessOf) zostaja we wsi, na typy jak w SendHome (Share); do 3 przebiegow (przyciecie ludzi bez broni nie zabiera broni innym). Zwraca
        /// przycietych; armed / unarmed - wedlug miary (bez bohaterow, wightow i umarlych - jak regula 2).
        /// </summary>
        private static int PlanCheck(List<Kind> kinds, Dictionary<ItemObject, int> trial, double pct, ref int armed, ref int unarmed)
        {
            var tr = TroopRoster.CreateDummyTroopRoster();
            var byCh = new Dictionary<CharacterObject, Kind>();
            foreach (var kd in kinds) if (kd.Taken > 0 && kd.Ch != null) { tr.AddToCounts(kd.Ch, kd.Taken); byCh[kd.Ch] = kd; }
            if (byCh.Count == 0) return 0;
            int cut = 0;
            var a = CountArms(tr, trial);
            for (int round = 0; round < 3; round++)
            {
                int go = ExcessOf(a.NoWeapon, a.Men, pct);
                if (go <= 0) break;
                int c0 = cut;
                foreach (var kv in Share(a.NoW, a.NoWeapon, go))
                {
                    Kind kd;
                    if (kv.Value <= 0 || !byCh.TryGetValue(kv.Key, out kd)) continue;
                    int n = Math.Min(kv.Value, kd.Taken);
                    if (n <= 0) continue;
                    kd.Taken -= n; tr.AddToCounts(kd.Ch, -n); cut += n;
                }
                if (cut == c0) break;
                a = CountArms(tr, trial);
            }
            armed = a.Men - a.NoWeapon; unarmed = a.NoWeapon;
            return cut;
        }

        // ------------------------------------------------------------ (1) wizyta w miescie (AiGear.TryBuyCore, BudgetPreview, ArmsFirst.TripTown)
        private static KitPlan Get(MobileParty mp)
        {
            if (mp == null) return null;
            KitPlan p;
            if (_plans.TryGetValue(mp, out p)) return p;
            if (_savedPlans.Count == 0) return null;
            string id = mp.StringId;
            if (id == null || !_savedPlans.TryGetValue(id, out p)) return null;
            _savedPlans.Remove(id);
            p.P = mp;
            if (p.Town == null && p.TownId != null) { try { p.Town = Settlement.Find(p.TownId); } catch { } }
            _plans[mp] = p;
            return p;
        }

        /// <summary>Nowa druzyna z planem jeszcze bez wizyty w miescie (klucz wlaczony).</summary>
        internal static bool Active(MobileParty mp)
        {
            try { var s = Settings.Current; if (s == null || !s.NewLordPartyArmedMenOnly) return false; var p = Get(mp); return p != null && !p.Done; }
            catch (Exception e) { Stumble("Active", e); return false; }
        }

        /// <summary>ArmsFirst.TripTown: miasto, w ktorym wyceniono komplet (null - bez planu).</summary>
        internal static Settlement PlanTown(MobileParty mp) { return Active(mp) ? Get(mp).Town : null; }

        /// <summary>ArmsFirst.CanAfford: prog "stac go" dla nowej druzyny z planem - wycena planu x Margin (0 - bez planu).</summary>
        internal static int PlanNeed(MobileParty mp)
        {
            try { if (!Active(mp)) return 0; var p = Get(mp); return (int)Math.Min(int.MaxValue, Math.Max(1L, (long)Math.Ceiling(p.Cost * Margin))); }
            catch (Exception e) { Stumble("PlanNeed", e); return 0; }
        }

        /// <summary>AiGear.BudgetPreview (ArmsFirst.CanAfford): zloto ponad rezerwe i miejsce pozyczki - bez brania pozyczki.</summary>
        internal static int PreviewFund(MobileParty mp, Hero lord, Settings s)
        {
            try { string why; return (int)Math.Min(int.MaxValue, (long)OwnFund(lord, s) + LoanRoom(lord != null ? lord.Clan : null, out why)); }
            catch (Exception e) { Stumble("PreviewFund", e); return 0; }
        }

        /// <summary>
        /// AiGear.TryBuyCore (lord AI w miescie, przed petla zakupow): budzet wizyty z planem - braki zbrojowni wycenione na tej polce (QuoteUnits) x Margin;
        /// zloto ponad rezerwe (OwnFund) i sakiewka ludzi, brak - pozyczka Banku (Borrow; najwyzej wycena planu x Margin minus zloto i sakiewka). -1 - bez planu, nie miasto albo polka bez sztuk (zwykly zakup; plan
        /// zostaje do nastepnego miasta albo wygasniecia). pieces - limit sztuk wizyty (sztuki wyceny). 159b: firstNeed / firstLift - pierwsza bron dla ludzi bez broni
        /// (FirstWeapons; null - nikogo albo nic na polce) - TryBuyCore kupuje ja pierwszym przebiegiem BuyLoop, przed siodlami i reszta brakow.
        /// </summary>
        internal static int VisitBudget(MobileParty mp, Settlement st, Hero lord, Settings s, int budget0, int purse, out int pieces,
                                        out Dictionary<int, int> firstNeed, out Dictionary<int, List<CharacterObject>> firstLift)
        {
            pieces = 0; firstNeed = null; firstLift = null;
            try
            {
                var p = Get(mp);
                if (p == null || p.Done || st == null || !st.IsTown || lord == null) return -1;
                var all = AiGear.Armories();
                Dictionary<ItemObject, int> arm = null;
                if (all != null) all.TryGetValue(mp.Id, out arm);
                var lifters = new Dictionary<int, List<CharacterObject>>();
                var need = AiGear.Deficit(mp, arm, null, lifters);
                var units = AiGear.QuoteUnits(st, mp, need, lifters);
                if (units == null) return -1;   // wycena nieudana - zwykly zakup, plan czeka
                long est = 0;
                foreach (var l in units.Values) { pieces += l.Count; foreach (var u in l) est += u; }
                if (pieces <= 0) { _dVisitEmpty++; return -1; }
                p.Done = true;
                _dVisits++;
                // 159b: najpierw jedna bron na czlowieka bez broni (regula Jeffa 26.10) - bez tego BuyLoop (korpus pierwszy w AiGear.Order) wydalby budzet planu
                // "najpierw bron" na zbroje, a ludzie wzieci za bron zostaliby bez niej
                try
                {
                    int fm = FirstWeapons(mp, st, arm, s.MenUpgradeOneTierUp, true, AllowedPct(s), out firstNeed, out firstLift);
                    p.VFirstMen = fm; _dVisitFirstMen += fm;
                }
                catch (Exception e) { Stumble("VisitBudget(bron)", e); firstNeed = null; firstLift = null; }
                int own = OwnFund(lord, s);
                long want = (long)Math.Ceiling(est * Margin);
                long shortfall = want - own - Math.Max(0, purse);
                // recenzja 159 (uwaga 2): pozyczka najwyzej na komplet ludzi PLANU (wycena planu x Margin, p.Cost jeszcze z planu) - braki ludzi dobranych po drodze
                // (werbunek z wsi) placi zwykly budzet AiGear jak u kazdego lorda
                long planCap = (long)Math.Ceiling(p.Cost * Margin) - own - Math.Max(0, purse);
                if (shortfall > 0 && shortfall > planCap) { shortfall = planCap; _dLoanCapped++; }
                int got = 0; string why = null;
                if (shortfall > 0) got = Borrow(lord.Clan, lord, (int)Math.Min(int.MaxValue, shortfall), out why);
                if (got > 0) { _dLoans++; _dLoanSum += got; }
                else if (shortfall > 0) NoLoan(why);
                p.Loan = got; p.Own = own; p.Cost = est;
                _dVisitOwn += Math.Min(own, want);
                return (int)Math.Min(int.MaxValue, Math.Min(want, (long)own + Math.Max(0, purse) + got));
            }
            catch (Exception e) { Stumble("VisitBudget", e); return -1; }
        }

        /// <summary>AiGear.TryBuyCore po zakupach wizyty z planem: liczniki (wydane, sztuki, reszta pozyczki w kiesie wodza) i koniec planu.</summary>
        internal static void VisitDone(MobileParty mp, Settlement st, int spent, int pieces, int budget)
        {
            try
            {
                KitPlan p;
                if (mp == null || !_plans.TryGetValue(mp, out p)) return;
                _dVisitSpent += Math.Max(0, spent); _dVisitPieces += Math.Max(0, pieces);
                long left = Math.Max(0L, Math.Min(p.Loan, (long)budget - spent));
                _dLoanLeft += left;
                Ex(ExVisit, Nm(mp) + " w " + Nm(st) + ": kupione " + pieces + " szt. za " + spent + " zl (najpierw bron dla ludzi bez broni: ludzi " + p.VFirstMen + ", kupione "
                   + p.VFirstPcs + " szt. za " + p.VFirstGold + " zl; wycena " + p.Cost + ", wlasne " + p.Own + ", pozyczka " + p.Loan
                   + (left > 0 ? ", z pozyczki zostalo w kiesie " + left : "") + "; ludzi " + p.Men + " z szablonu " + p.Template + ")");
                _plans.Remove(mp);
            }
            catch (Exception e) { Stumble("VisitDone", e); }
        }

        /// <summary>159b: AiGear.TryBuyCore po pierwszym przebiegu (pierwsza bron dla ludzi bez broni) wizyty z planem - sztuki i zloto (tylko log). Recenzja 159b:
        /// plan=false - zwykla wizyta z bronia ponad % (uwaga 2); round &gt; 0 - kolejny przebieg po zmierzeniu od nowa (uwaga 1).</summary>
        internal static void NoteFirst(MobileParty mp, int pieces, int gold, bool plan, int round)
        {
            try
            {
                if (round > 0) _dFirstRounds++;
                if (pieces > 0 && mp != null) _quotes.Remove(mp);   // 159c: kupil pierwsza bron - wycena "stac go" od nowa
                if (!plan) { _dCapVisitPieces += Math.Max(0, pieces); _dCapVisitGold += Math.Max(0, gold); return; }
                _dVisitFirstPieces += Math.Max(0, pieces); _dVisitFirstGold += Math.Max(0, gold);
                KitPlan p;
                if (mp != null && _plans.TryGetValue(mp, out p)) { p.VFirstPcs += Math.Max(0, pieces); p.VFirstGold += Math.Max(0, gold); }
            }
            catch (Exception e) { Stumble("NoteFirst", e); }
        }

        /// <summary>Recenzja 159b (uwaga 2): zwykla wizyta lorda AI z bronia ponad % - liczniki (wizyty, ludzi do uzbrojenia; tylko log).</summary>
        internal static void NoteCapVisit(int men) { _dCapVisits++; _dCapVisitMen += Math.Max(0, men); }

        /// <summary>Recenzja 159b (uwaga 2): regula "bez broni najwyzej X%" obejmuje kazda partie lorda AI (klucz UnarmedCapAllLordParties), nie tylko niegotowe p9;
        /// miara CountArms ma sens tylko przy TroopsFightWithOwnKitOnly (bez niego bitwa daje wzorzec) i przy zakupach AI.</summary>
        private static bool CapAllOn(Settings s) { return s != null && s.UnarmedCapAllLordParties && GarrisonKit.OwnKitOn && AiGear.On; }

        /// <summary>Recenzja 159b (uwaga 2): partia, ktorej dotyczy regula X% poza p9 - lord AI w regule DTE (GarrisonKit.LordRule), bez rodu gracza, Strazy i umarlych.</summary>
        private static bool CapParty(MobileParty mp)
        {
            return mp != null && mp.IsActive && !mp.IsMainParty && GarrisonKit.LordRule(mp) && mp.ActualClan != Clan.PlayerClan && !CrownGifts.IsWatchParty(mp) && !Undead.Party(mp);
        }

        /// <summary>
        /// Recenzja 159b: pierwsza bron do kupienia teraz (AiGear.TryBuyCore) - plan=true: kolejny przebieg wizyty z planem (wszyscy bez broni, uwaga 1: sztuki
        /// poprzedniego przebiegu mogly trafic do wyzszych oddzialow - miara od nowa na zbrojowni po zakupie); plan=false: zwykla wizyta lorda AI (uwaga 2) - tylko
        /// nadwyzka bez broni ponad UnarmedMenAllowedPercent (ExcessOf), przy kluczu UnarmedCapAllLordParties. Bez zakupu przed siodlami i korpusem (AiGear.Order)
        /// lord wydawal budzet na zbroje, a ludzie bez broni zostawali ponad %. Zwraca ludzi (0 - need/lift null). Tylko odczyt, nie rzuca.
        /// </summary>
        internal static int FirstNeed(MobileParty mp, Settlement st, bool plan, out Dictionary<int, int> need, out Dictionary<int, List<CharacterObject>> lift)
        {
            need = null; lift = null;
            try
            {
                var s = Settings.Current;
                if (s == null || mp == null || st == null || st.Town == null) return 0;
                if (!plan && (!CapAllOn(s) || !CapParty(mp) || Active(mp))) return 0;
                var all = AiGear.Armories();
                Dictionary<ItemObject, int> arm = null;
                if (all != null) all.TryGetValue(mp.Id, out arm);
                int men = FirstWeapons(mp, st, arm, s.MenUpgradeOneTierUp, plan, AllowedPct(s), out need, out lift);
                if (men <= 0 || need == null || need.Count == 0) { need = null; lift = null; return 0; }
                return men;
            }
            catch (Exception e) { Stumble("FirstNeed", e); need = null; lift = null; return 0; }
        }

        /// <summary>
        /// 159b: pierwsza bron dla ludzi bez broni (CountArms - jak bitwa) na polce tego miasta: na czlowieka najtanszy krok jego wzorca (WeaponOpts),
        /// wycena QuoteUnits tylko z oddzialami tych ludzi (udzwig, kultura - Lift). need - koszyki NeedKey pierwszego przebiegu zakupu, lift - oddzialy koszyka.
        /// Zwraca liczbe ludzi, dla ktorych na polce jest pierwsza bron. Tylko odczyt. Recenzja 159b: all=false - najwyzej nadwyzka ponad pct% (ExcessOf; zwykla
        /// wizyta, uwaga 2), all=true - wszyscy bez broni (wizyta z planem); krok ArmStep na pokryciu CountArms, opcje po wszystkich slotach typu (uwaga 1).
        /// </summary>
        private static int FirstWeapons(MobileParty mp, Settlement st, Dictionary<ItemObject, int> arm, bool oneUp, bool all, double pct, out Dictionary<int, int> need,
                                        out Dictionary<int, List<CharacterObject>> lift)
        {
            int excess;
            return FirstWeapons(mp, st, arm, oneUp, all, pct, out need, out lift, null, out excess);
        }

        /// <summary>159c: jak wyzej; costs (opcjonalnie) - cena kolejnych krokow "jeszcze jeden uzbrojony" (najtanszy najpierw - suma pierwszych k = pierwsza bron
        /// dla k ludzi, bez zapasu Margin); excess - nadwyzka bez broni ponad pct% (ExcessOf, miara CountArms).</summary>
        private static int FirstWeapons(MobileParty mp, Settlement st, Dictionary<ItemObject, int> arm, bool oneUp, bool all, double pct, out Dictionary<int, int> need,
                                        out Dictionary<int, List<CharacterObject>> lift, List<long> costs, out int excess)
        {
            need = null; lift = null; excess = 0;
            var a = CountArms(mp.MemberRoster, arm);
            if (a.NoWeapon <= 0) return 0;
            excess = ExcessOf(a.NoWeapon, a.Men, pct);
            int limit = all ? int.MaxValue : excess;
            if (limit <= 0) return 0;
            // recenzja 159b (uwaga 1): oddzial z pokryciem CountArms (Cov na typ - luk bez strzal dostaje same strzaly, wolne sloty pierwszych ludzi najpierw);
            // kandydaci wyceny - najwiecej tyle sztuk na koszyk, ile wymaga uzbrojenie wszystkich ludzi bez broni oddzialu ta opcja
            var cand = new Dictionary<int, int>();
            var cl = new Dictionary<int, List<CharacterObject>>();
            var steps = new List<ArmStep>();
            foreach (var kv in a.NoW)
            {
                var o = WeaponOpts(kv.Key, oneUp);
                if (o.Count == 0) continue;
                int[] c0;
                var cov = a.Cov != null && a.Cov.TryGetValue(kv.Key, out c0) ? (int[])c0.Clone() : new int[ArmTypes.Length];
                var g = new ArmStep { Ch = kv.Key, Left = kv.Value, Cov = cov, Armed = ArmedOf(cov), Opts = o };
                steps.Add(g);
                foreach (var opt in o)
                    foreach (var k in opt)
                    {
                        int ti = TypeIdx(k);
                        int n = ti >= 0 ? Math.Max(0, g.Armed + g.Left - g.Cov[ti]) : 0;
                        if (n <= 0) continue;
                        int v; cand.TryGetValue(k, out v); cand[k] = v + n;
                        List<CharacterObject> l;
                        if (!cl.TryGetValue(k, out l)) cl[k] = l = new List<CharacterObject>();
                        if (!l.Contains(kv.Key)) l.Add(kv.Key);
                    }
            }
            if (cand.Count == 0) return 0;
            var units = AiGear.QuoteUnits(st, mp, cand, cl);
            if (units == null || units.Count == 0) return 0;
            var pos = new Dictionary<int, int>();
            var nd = new Dictionary<int, int>();
            var lf = new Dictionary<int, List<CharacterObject>>();
            int men = 0;
            while (men < limit)
            {
                ArmStep bg = null; int[] bo = null; List<int> bk = null; long bc = long.MaxValue;
                foreach (var g in steps)
                {
                    if (g.Left <= 0) continue;
                    foreach (var o in g.Opts) { var ks = StepKeys(g, o); long c = KeysCost(ks, units, pos); if (c >= 0 && c < bc) { bg = g; bo = o; bk = ks; bc = c; } }
                }
                if (bg == null) break;
                KeysTake(bk, pos, null, null); StepApply(bg, bo); men++;
                if (costs != null) costs.Add(bc);
                foreach (var k in bk)
                {
                    int v; nd.TryGetValue(k, out v); nd[k] = v + 1;
                    List<CharacterObject> l;
                    if (!lf.TryGetValue(k, out l)) lf[k] = l = new List<CharacterObject>();
                    if (!l.Contains(bg.Ch)) l.Add(bg.Ch);
                }
            }
            if (men <= 0) return 0;
            need = nd; lift = lf;
            return men;
        }

        // ------------------------------------------------------------ 159c: "stac go na pierwsza bron dla nadwyzki" - prawdziwa wycena
        /// <summary>
        /// 159c (druga recenzja 159b, uwaga 2): wycena pierwszej broni dla nadwyzki bez broni ponad UnarmedMenAllowedPercent (FirstWeapons z limitem nadwyzki - te same
        /// kroki i ta sama polka co zakup wizyty AiGear (capVisit)) w miescie town, a bez niego - ArmsFirst.NearestTown (miasto, w ktorym partia stoi, gdy wazne -
        /// odleglosc 0 - albo najblizsze wazne miasto wyjazdu p9: przyjazne, bez oblezenia, droga ladowa, AiArmsTripMaxDays, ta sama strona Muru, bez wytchnienia).
        /// Cost - pierwsza bron dla min(nadwyzka, FirstUnit) ludzi x Margin; Budget - AiGear.BudgetPreview(partia, miasto) (wzor TryBuyCore z rezerwa napraw tego
        /// miasta; u nowej druzyny z planem - z miejscem pozyczki 159). Dotad prog p9 CanAfford: najmniej 300 zl (MinNeed), a pierwsza bron kosztuje 2-3 zl za sztuke
        /// (autotest 26.10: 1192 szt. za 3452 zl). Raz na dobe na partie (_quotes; od nowa po zakupie pierwszej broni - NoteFirst, i po odeslaniu); wpis przed
        /// liczeniem - wyjatek nie powtarza wyceny co godzine. Bez miasta w zasiegu - Town null (nikt nie wycenia, "nie stac"). Tylko odczyt.
        /// </summary>
        private static Quote QuoteOf(MobileParty mp, Settlement town)
        {
            int today = Today;
            Quote q;
            if (_quotes.TryGetValue(mp, out q) && q.Day == today && (town == null || ReferenceEquals(q.Town, town))) return q;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            q = new Quote { Day = today };
            _quotes[mp] = q;
            _dQuotes++;
            try
            {
                var s = Settings.Current;
                if (s == null) return q;
                bool failed = false;
                q.Town = town ?? ArmsFirst.NearestTown(mp, out failed);
                if (failed) { _dStumbles++; _stumbles++; }
                if (q.Town == null) return q;
                var all = AiGear.Armories();
                Dictionary<ItemObject, int> arm = null;
                if (all != null) all.TryGetValue(mp.Id, out arm);
                var costs = new List<long>();
                Dictionary<int, int> nd; Dictionary<int, List<CharacterObject>> lf; int go;
                q.Men = FirstWeapons(mp, q.Town, arm, s.MenUpgradeOneTierUp, false, AllowedPct(s), out nd, out lf, costs, out go);
                q.Go = go;
                int k = Math.Min(costs.Count, Math.Min(Math.Max(1, go), FirstUnit));
                long c = 0;
                for (int i = 0; i < k; i++) c += costs[i];
                q.Cost = (long)Math.Ceiling(c * Margin);
                q.Budget = AiGear.BudgetPreview(mp, q.Town);
            }
            catch (Exception e) { Stumble("QuoteOf", e); q.Men = 0; }
            finally { _dQuoteTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0; }
            return q;
        }

        private static bool QuoteOk(Quote q) { return q != null && q.Town != null && q.Men > 0 && q.Budget > 0 && q.Budget >= q.Cost; }

        /// <summary>
        /// 159c: ArmsFirst.ThinkPostfix (galaz "nie stac" p9) i TripTown(firstOnly): niegotowa partia, ktorej nie stac na pelne dozbrojenie (p9 CanAfford), stac na
        /// pierwsza bron dla nadwyzki bez broni (QuoteOf; town null - miasto wyjazdu p9) - wyjazd zamiast "nie stac" (bez tego NoteStuck "nie stac" i po
        /// UnarmedMenSentHomeDays nadwyzka do domu, choc kilkadziesiat zl by ja uzbroilo). Tylko przy czynnej regule (2) (HomeOn) i partii, ktorej ona dotyczy
        /// (CapParty: bez rodu gracza, Strazy, umarlych); inaczej false - p9 jak dotad. Nie rzuca.
        /// </summary>
        internal static bool FirstOk(MobileParty mp, Settlement town)
        {
            try
            {
                var s = Settings.Current;
                // 159c recenzja 3: tylko przy UnarmedCapAllLordParties - bez niego zwykla wizyta nie kupuje najpierw pierwszej broni (FirstNeed: CapAllOn), wyjazd
                // "po pierwsza bron" wydalby budzet na zbroje (AiGear.Order), a ludzie bez broni zostaliby ponad %
                if (!HomeOn(s) || !CapAllOn(s) || !CapParty(mp)) return false;
                return QuoteOk(QuoteOf(mp, town));
            }
            catch (Exception e) { Stumble("FirstOk", e); return false; }
        }

        /// <summary>159c: ArmsFirst.ThinkPostfix - niegotowa partia z wyjazdem po pierwsza bron zamiast "nie stac" (trip - jest wyjazd; tylko licznik doby).</summary>
        internal static void NoteLowFirst(MobileParty mp, bool trip)
        {
            if (mp == null) return;
            _dLowFirst.Add(mp);
            if (!trip) _dLowFirstNoTrip.Add(mp); else _dLowFirstNoTrip.Remove(mp);
        }

        /// <summary>159c: ArmsFirst - zdarzenie wyjazdu po pierwsza bron (CapArrived / CapDone / CapLate / CapEnded; tylko licznik doby).</summary>
        internal static void CapTripEvent(int kind) { if (kind >= 0 && kind < _dCapTripEv.Length) _dCapTripEv[kind]++; }

        /// <summary>Regula (2) czynna: klucz UnarmedMenSentHome, walka wlasnym sprzetem, zakupy AI i p9 (recenzja 159, uwaga 7).</summary>
        private static bool HomeOn(Settings s) { return s != null && s.UnarmedMenSentHome && GarrisonKit.OwnKitOn && AiGear.On && s.AiArmsBeforeBattle; }

        // ------------------------------------------------------------ (2) niegotowa partia, ktora nie moze sie dozbroic
        /// <summary>ArmsFirst.ThinkPostfix (watek glowny, co godzine dla niegotowych): why != null - mysl nie dala wyjazdu (od kiedy - pierwsza taka mysl, takze
        /// z zapisu); null - jest wyjazd (koniec stania).</summary>
        internal static void NoteStuck(MobileParty mp, string why)
        {
            try
            {
                if (mp == null) return;
                if (why == null) { _stuck.Remove(mp); return; }
                int code = Array.IndexOf(WhyCodes, why); if (code < 0) code = WhyCodes.Length - 1;
                Stuck st;
                if (_stuck.TryGetValue(mp, out st)) { st.Why = code; return; }
                st = new Stuck { SinceH = CampaignTime.Now.ToHours, Why = code };
                Stuck sv;
                if (_savedStuck.Count > 0 && mp.StringId != null && _savedStuck.TryGetValue(mp.StringId, out sv)) { _savedStuck.Remove(mp.StringId); st.SinceH = Math.Min(st.SinceH, sv.SinceH); }
                _stuck[mp] = st;
            }
            catch (Exception e) { Stumble("NoteStuck", e); }
        }

        // 159b: bron wzorca liczona jak bitwa (GearFit.Armed: bron biala, rzucana, luk ze strzalami, kusza z beltami); indeksy uzywane w CountArms
        private static readonly ItemObject.ItemTypeEnum[] ArmTypes =
        {
            ItemObject.ItemTypeEnum.OneHandedWeapon, ItemObject.ItemTypeEnum.TwoHandedWeapon, ItemObject.ItemTypeEnum.Polearm, ItemObject.ItemTypeEnum.Thrown,
            ItemObject.ItemTypeEnum.Bow, ItemObject.ItemTypeEnum.Arrows, ItemObject.ItemTypeEnum.Crossbow, ItemObject.ItemTypeEnum.Bolts
        };

        private static bool MeleeType(ItemObject.ItemTypeEnum t)
        {
            return t == ItemObject.ItemTypeEnum.OneHandedWeapon || t == ItemObject.ItemTypeEnum.TwoHandedWeapon || t == ItemObject.ItemTypeEnum.Polearm;
        }

        /// <summary>159b: wynik CountArms. Reg - ludzie typu (bez bohaterow, wightow, umarlych; z rannymi), NoW - bez zadnej broni po puli (tylko typy z brakiem),
        /// Men / NoWeapon - sumy, FromPool - uzbrojeni bronia biala z reszty puli (podloga broni), NoBody / BodyMen - bez zbroi korpusu / z korpusem we wzorcu (info).</summary>
        private sealed class Arms
        {
            internal readonly Dictionary<CharacterObject, int> Reg = new Dictionary<CharacterObject, int>();
            internal readonly Dictionary<CharacterObject, int> NoW = new Dictionary<CharacterObject, int>();
            internal int Men, NoWeapon, FromPool, NoBody, BodyMen;
            internal Dictionary<CharacterObject, int[]> Cov;   // recenzja 159b: ludzie z bronia wzorca na typ ArmTypes (krok (1), przed podloga) - FirstWeapons
        }

        /// <summary>Recenzja 159b: uzbrojeni bronia wzorca z pokrycia na typ ArmTypes - jak CountArms (ci sami pierwsi ludzie, nie suma).</summary>
        private static int ArmedOf(int[] c)
        {
            if (c == null) return 0;
            int armed = 0;
            for (int ti = 0; ti < 4; ti++) armed = Math.Max(armed, c[ti]);   // bron biala i rzucana
            armed = Math.Max(armed, Math.Min(c[4], c[5]));                  // luk ze strzalami
            armed = Math.Max(armed, Math.Min(c[6], c[7]));                  // kusza z beltami
            return armed;
        }

        /// <summary>
        /// 159b (regula Jeffa 26.10 "max 10% armii" - tylko ludzie BEZ BRONI; uwaga 7 drugiego agenta: liczyc jak bitwa). Na typ ludzi, wszyscy z rannymi:
        /// (1) bron wzorca ze zbrojowni DTE - na kazdy typ z ArmTypes to samo dopasowanie co GarrisonKit.FillPercent (MenUpgrade.AiPieces, MenOf MenPerSlot - kazda bron
        /// i amunicja po slotach wzorca; SwapMath.Fit, MeetsFitOf), ludzie z bronia typu = ceil(pokryte sloty / sloty na czlowieka), najwyzej ludzie oddzialu. DTE ubiera
        /// ludzi po kolei i kazdy bierze sztuke na kazdy swoj slot 0-3 (PartyEquipmentDistributor.AssignWeaponByWeaponClass - petla po ludziach, w niej sloty), wiec
        /// pokryci roznymi typami to ci sami pierwsi ludzie: uzbrojeni = max(bron biala, rzucana, min(luk, strzaly), min(kusza, belty)) - nie suma (dotad Lacking
        /// sumowal typy i liczyl bron 1 na czlowieka - druga sztuka tego samego typu u pierwszych ludzi zostawala w "puli").
        /// (2) podloga broni (GearFit.FloorPostfix, klucz GearFitWeaponFloor; Priority.Last po DTE i strazach): czlowiek bez broni bierze bron biala z RESZTY puli
        /// (sztuki niewydane przez wzorce) - GearFit.FloorTakes (te same przejscia i wykluczenia, bez siodla); typy od najwyzszego tieru (DTE: Assignments od najlepszego),
        /// sztuki od najwyzszego wymogu (podloga bierze najtrudniejsza, ktora udzwignie). Klucz wylaczony - bez kroku (2): losowej broni DTE dla nieuzbrojonych i strazy
        /// CrashScribe nie da sie przewidziec. Kij z domu (HomeArms, m119) sie nie liczy. Korpus (info): MenTemplate jak dotad. Tylko odczyt.
        /// </summary>
        private static Arms CountArms(TroopRoster r, Dictionary<ItemObject, int> arm)
        {
            var a = new Arms();
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var ch = el.Character;
                if (ch == null || ch.IsHero || el.Number <= 0 || PeopleBook.IsWight(ch) || Undead.Character(ch)) continue;
                int v; a.Reg.TryGetValue(ch, out v); a.Reg[ch] = v + el.Number; a.Men += el.Number;
            }
            if (a.Men <= 0) return a;
            List<CharacterObject> troops; int[] idx; SkillObject skill;
            // korpus (info, jak dotad Lacking): tylko oddzialy z korpusem we wzorcu
            {
                var pieces = MenUpgrade.AiPieces(arm, ItemObject.ItemTypeEnum.BodyArmor);
                QuartermasterLaw.MenOf(r, ItemObject.ItemTypeEnum.BodyArmor, pieces, out troops, out idx, out skill, QuartermasterLaw.MenTemplate, false);
                if (idx.Length > 0)
                {
                    var unfit = new bool[idx.Length];
                    if (pieces.Count > 0) SwapMath.Fit(idx, troops.Count, QuartermasterLaw.MeetsFitOf(troops), pieces, unfit);
                    else for (int m = 0; m < unfit.Length; m++) unfit[m] = true;
                    for (int m = 0; m < idx.Length; m++)
                    {
                        int g = idx[m]; int n;
                        if (g < 0 || g >= troops.Count || troops[g] == null || !a.Reg.TryGetValue(troops[g], out n) || n <= 0) continue;
                        a.BodyMen++; if (unfit[m]) a.NoBody++;
                    }
                }
            }
            // (1) bron wzorca
            var cov = new Dictionary<CharacterObject, int[]>();
            var pool = new List<SwapMath.Piece>();
            for (int ti = 0; ti < ArmTypes.Length; ti++)
            {
                var type = ArmTypes[ti];
                var pieces = MenUpgrade.AiPieces(arm, type);
                QuartermasterLaw.MenOf(r, type, pieces, out troops, out idx, out skill, QuartermasterLaw.MenPerSlot, false);
                if (idx.Length > 0 && pieces.Count > 0)
                {
                    var unfit = new bool[idx.Length];
                    SwapMath.Fit(idx, troops.Count, QuartermasterLaw.MeetsFitOf(troops), pieces, unfit);
                    var tot = new int[troops.Count]; var cv = new int[troops.Count];
                    for (int m = 0; m < idx.Length; m++) { int g = idx[m]; if (g < 0 || g >= troops.Count) continue; tot[g]++; if (!unfit[m]) cv[g]++; }
                    for (int g = 0; g < troops.Count; g++)
                    {
                        var ch = troops[g]; int n;
                        if (ch == null || cv[g] <= 0 || !a.Reg.TryGetValue(ch, out n) || n <= 0) continue;   // bohaterowie, wighty, umarli - poza regula
                        int mult = Math.Max(1, (tot[g] + n - 1) / n);   // MenOf: wpisow = ludzie x sloty typu
                        int[] c;
                        if (!cov.TryGetValue(ch, out c)) cov[ch] = c = new int[ArmTypes.Length];
                        c[ti] = Math.Min(n, (cv[g] + mult - 1) / mult);
                    }
                }
                if (MeleeType(type)) foreach (var p in pieces) if (!p.Barred && p.Total - p.Used > 0) pool.Add(p);   // niewydane przez wzorce - pula podlogi
            }
            a.Cov = cov;
            foreach (var kv in a.Reg)
            {
                int[] c; int armed = 0;
                if (cov.TryGetValue(kv.Key, out c)) armed = ArmedOf(c);   // bron biala, rzucana, luk ze strzalami, kusza z beltami - max, nie suma
                int nw = kv.Value - Math.Min(kv.Value, armed);
                if (nw > 0) a.NoW[kv.Key] = nw;
            }
            // (2) podloga broni z reszty puli
            if (GearFit.FloorOn && pool.Count > 0 && a.NoW.Count > 0)
            {
                pool.Sort((x, y) => y.Req.CompareTo(x.Req));
                var left = new int[pool.Count];
                for (int i = 0; i < pool.Count; i++) left[i] = pool[i].Total - pool[i].Used;
                var order = new List<CharacterObject>(a.NoW.Keys);
                order.Sort((x, y) => { int d = y.Tier.CompareTo(x.Tier); return d != 0 ? d : string.CompareOrdinal(x.StringId, y.StringId); });
                foreach (var ch in order)
                {
                    int need = a.NoW[ch];
                    for (int i = 0; i < pool.Count && need > 0; i++)
                    {
                        if (left[i] <= 0 || !GearFit.FloorTakes(ch, QuartermasterLaw.ElOf(pool[i]).Item)) continue;
                        int t = Math.Min(need, left[i]);
                        left[i] -= t; need -= t; a.FromPool += t;
                    }
                    if (need > 0) a.NoW[ch] = need; else a.NoW.Remove(ch);
                }
            }
            foreach (var v in a.NoW.Values) a.NoWeapon += v;
            return a;
        }

        /// <summary>159b: nadwyzka `go` ludzi bez broni na typy proporcjonalnie do ich ludzi bez broni (najwieksze reszty; na typ najwyzej jego bez broni).</summary>
        private static Dictionary<CharacterObject, int> Share(Dictionary<CharacterObject, int> noW, int total, int go)
        {
            var res = new Dictionary<CharacterObject, int>();
            if (go <= 0 || total <= 0) return res;
            if (go >= total) { foreach (var kv in noW) res[kv.Key] = kv.Value; return res; }
            var rem = new List<KeyValuePair<CharacterObject, long>>();
            int given = 0;
            foreach (var kv in noW)
            {
                long num = (long)go * kv.Value;
                int q = (int)(num / total);
                res[kv.Key] = q; given += q;
                rem.Add(new KeyValuePair<CharacterObject, long>(kv.Key, num % total));
            }
            rem.Sort((x, y) => { int d = y.Value.CompareTo(x.Value); return d != 0 ? d : string.CompareOrdinal(x.Key.StringId, y.Key.StringId); });
            for (int i = 0; i < rem.Count && given < go; i++)
            {
                var ch = rem[i].Key;
                if (res[ch] < noW[ch]) { res[ch] = res[ch] + 1; given++; }
            }
            return res;
        }

        /// <summary>Odsyla do domu ludzi bez broni ponad pct% partii (opis klasy, (2); 159b - miara CountArms jak bitwa, nadwyzka ExcessOf, podzial Share; brak zbroi
        /// przy broni nie odsyla). Zwraca odeslanych.</summary>
        private static int SendHome(MobileParty mp, string why, double days, double pct)
        {
            var all = AiGear.Armories();
            Dictionary<ItemObject, int> arm = null;
            if (all != null) all.TryGetValue(mp.Id, out arm);
            int needS, usedS;
            int r0 = GarrisonKit.FillPercent(mp, arm, null, false, out needS, out usedS);   // tylko do przykladu (R)
            var r = mp.MemberRoster;
            var a = CountArms(r, arm);
            int go = ExcessOf(a.NoWeapon, a.Men, pct);
            _dHomeChecked++; _dHomeAll += a.Men; _dHomeNoW += a.NoWeapon; _dHomePool += a.FromPool; _dHomeKeep += a.NoWeapon - go; _dHomeOver += go; _dHomeNoBody += a.NoBody;
            if (go <= 0) { _dHomeNothing++; return 0; }
            var lack = Share(a.NoW, a.NoWeapon, go);
            var types = new List<string>();
            {
                var top = new List<KeyValuePair<CharacterObject, int>>(lack);
                top.Sort((x, y) => y.Value.CompareTo(x.Value));
                for (int i = 0; i < top.Count && types.Count < 3; i++)
                {
                    var ch = top[i].Key; if (top[i].Value <= 0) continue;
                    string nm; try { nm = ch.Name != null ? ch.Name.ToString() : ch.StringId; } catch { nm = "?"; }
                    int nw, n; a.NoW.TryGetValue(ch, out nw); a.Reg.TryGetValue(ch, out n);
                    types.Add(nm + " " + top[i].Value + "/" + nw + "/" + n);
                }
            }
            var village = ClanBudget.OverflowVillage(mp);
            if (village == null) { _dHomeNoVillage += go; return 0; }   // bez wsi z danymi BK nikt nie odchodzi (nikt nie znika - Z6)
            int gone = 0, wounded = 0, tavern = 0;
            foreach (var kv in lack)
            {
                var ch = kv.Key;
                int idx = r.FindIndexOfTroop(ch);
                if (idx < 0 || kv.Value <= 0) continue;
                int num = r.GetElementNumber(idx), wnd = r.GetElementWoundedNumber(idx);
                int n = Math.Min(kv.Value, num);
                if (n <= 0) continue;
                int w = wnd > 0 ? Math.Min(wnd, (int)Math.Round(n * (double)wnd / num)) : 0;
                // E4/7: udzialy pochodzenia tuz PRZED dopisaniem do wsi, roster po (jak ClanBudget.ReleaseFrom; wies nie przyjela - Untake, ludzie zostaja)
                PeopleBook.Shares org = null;
                try { if (PeopleBook.OriginLive) org = PeopleBook.Take(r, n, PeopleBook.FRelease166); } catch (Exception e) { Stumble("SendHome.Take", e); org = null; }
                bool ok = false;
                int tav = 0;   // recenzja 159 (uwaga 6): karczma E4/10 odeslanych - w linii 159, nie w "z tego do karczmy" linii 166 (ci ludzie nie sa w jej "zwolnieni: do wsi")
                try { ok = ClanBudget.ToVillagePop159(village, ch, n, org, mp, out tav); } catch (Exception e) { Stumble("SendHome.ToVillagePop", e); ok = false; }
                if (!ok)
                {
                    try { if (org != null) PeopleBook.Untake(r, org, PeopleBook.FRelease166); } catch (Exception e) { Stumble("SendHome.Untake", e); }
                    _dHomeNoVillage += n;
                    continue;
                }
                r.AddToCounts(ch, -n, false, -w);
                gone += n; wounded += w; tavern += Math.Max(0, tav);
            }
            if (gone <= 0) return 0;
            _dHomeParties++; _dHomeMen += gone; _dHomeWounded += wounded; _dHomeTavern += tavern;
            int last;
            if (_lastHome.TryGetValue(mp, out last) && Today - last <= RepeatDays) _dHomeRepeat++;
            _lastHome[mp] = Today;
            _quotes.Remove(mp);   // 159c: sklad sie zmienil - wycena "stac go" od nowa
            try { ArmedStrength.MarkDirty(mp); } catch { }
            Ex(ExHome, Nm(mp) + ": ludzi " + a.Men + ", bez broni (po puli zbrojowni) " + a.NoWeapon + " (z puli uzbrojeni " + a.FromPool + "), w ramach " + PctTxt(pct)
               + "% zostaje " + (a.NoWeapon - go) + ", do domu " + gone + " z " + go + " ponad % (" + why + " od " + days.ToString("0.0", Inv) + " dob; bez zbroi korpusu (info) "
               + a.NoBody + " z " + a.BodyMen + ", R " + r0 + "%; wies " + Nm(village) + (tavern > 0 ? ", do karczmy " + tavern : "") + "; typy (do domu/bez broni/ludzi): "
               + string.Join(", ", types.ToArray()) + ")");
            return gone;
        }

        /// <summary>
        /// Recenzja 159b (uwaga 2; regula Jeffa 26.10 "max 10% armii" - tylko ludzie bez broni): partia lorda AI GOTOWA wedlug p9 (R - odsetek slotow wzorca, takze
        /// zbroi - nie widzi ludzi bez broni w dobrze opancerzonej partii; dotad regula (2) jej nie sprawdzala). Raz na dobe CountArms (ta sama miara co SendHome).
        /// 159c (druga recenzja 159b, uwaga 1 - dotad zegar znikal kazdego dnia, gdy p9 CanAfford, wiec partia z pieniedzmi, ktora nie trafia na polke z pierwsza
        /// bronia, trzymala ponad % bez konca): takze partia ZWOLNIONA z czekania p9 (freed - p9 jej nie wysyla). Bez broni ponad pct% (ExcessOf) - zegar (w zapisie):
        /// biegnie, dopoki nadwyzka nie maleje; ponizej najmniejszej od startu zegara (zakup, polegli; recenzja 3 - nie "mniejsza niz wczoraj") - od nowa;
        /// wieksza (werbunek) - biegnie dalej. Po
        /// UnarmedMenSentHomeDays dobach bez postepu nadwyzka do domu (SendHome) BEZ WZGLEDU na pieniadze (jak p9 "2 wizyty bez postepu" u niegotowych). Do tego czasu,
        /// gdy stac ja na pierwsza bron dla nadwyzki wedlug wyceny polki (QuoteOf - min(nadwyzka, FirstUnit) ludzi x Margin wobec BudgetPreview) - wyjazd do tego
        /// miasta (ArmsFirst.StartCapTrip: mysl godzinowa p9, bez stanu "niegotowa"; najwyzej raz na UnarmedMenSentHomeDays dob; nie przy wyjezdzie p9); w drodze
        /// i na postoju zegar czeka (koniec wyjazdu: postoj StayHours albo za dlugo w drodze). Kupuje zwykla wizyta AiGear (najpierw pierwsza bron ponad %).
        /// Bez: armii (zegar od nowa, jak stanie p9), planu w toku, wpisu DTE, rodu gracza, Strazy, umarlych; bitwa, oblezenie, morze, rozwiazywana - zegar czeka.
        /// </summary>
        private static void OverCap(MobileParty mp, bool hasEntry, double now, double wait, double pct, bool freed)
        {
            if (!CapParty(mp) || mp.Army != null || Active(mp) || !hasEntry) { DropOver(mp); return; }
            if (!ArmedStrength.Computed(mp)) return;   // po wczytaniu p9 liczy partie kolejka - zegar z zapisu zostaje
            if (mp.MapEvent != null || mp.SiegeEvent != null || mp.BesiegedSettlement != null || mp.IsCurrentlyAtSea || mp.IsDisbanding || mp.LeaderHero == null
                || (mp.CurrentSettlement != null && mp.CurrentSettlement.IsUnderSiege))
            { _dCapBusy++; return; }
            var all = AiGear.Armories();
            Dictionary<ItemObject, int> arm = null;
            if (all != null) all.TryGetValue(mp.Id, out arm);
            var a = CountArms(mp.MemberRoster, arm);
            _dCapChecked++;
            if (freed) _dCapFree++;
            int go = ExcessOf(a.NoWeapon, a.Men, pct);
            if (go <= 0) { DropOver(mp); return; }
            _dCapOver++;
            Over o;
            if (!_over.TryGetValue(mp, out o)) _over[mp] = o = new Over { SinceH = now, Go = go };
            else if (o.Go >= 0 && go < o.Go) { o.SinceH = now; o.Go = go; _dCapProgress++; }   // postep - ponizej najmniejszej od startu zegara: od nowa
            else if (o.Go < 0) o.Go = go;   // zegar z zapisu sprzed 159c - nadwyzka nieznana: od teraz najmniejsza; wieksza (werbunek) nie podnosi poprzeczki (recenzja 3)
            if (ArmsFirst.CapTripActive(mp)) { _dCapTripping++; return; }   // w drodze po pierwsza bron albo na postoju - zegar czeka do konca wyjazdu
            if (now - o.SinceH >= wait)
            {
                // 159c recenzja 3: zegar z zapisu po terminie - wyjazdy po pierwsza bron nie ida do zapisu (Reset przed wczytaniem), wiec partia przerwana w drodze
                // dostaje raz wyjazd zamiast odsylki, gdy stac ja na pierwsza bron (jak bez wczytania: zakup = postep, inaczej odsylka po koncu wyjazdu)
                if (o.Loaded)
                {
                    o.Loaded = false;
                    if (!ArmsFirst.CapTripRecent(mp, now, wait))
                    {
                        var lq = QuoteOf(mp, null);
                        if (QuoteOk(lq) && ArmsFirst.StartCapTrip(mp, lq.Town, now))
                        {
                            _dCapTrips++; _dCapLoadTrip++;
                            Ex(ExCap, Nm(mp) + ": po wczytaniu zegar po terminie - wyjazd po pierwsza bron do " + Nm(lq.Town) + " zamiast odsylki (ponad " + PctTxt(pct) + "% " + go
                               + ", za min(" + go + ", " + FirstUnit + ") ludzi " + lq.Cost + " zl z zapasem, budzet " + lq.Budget + ")");
                            return;
                        }
                    }
                }
                int gone = SendHome(mp, (freed ? "zwolniona z czekania p9" : "gotowa wedlug p9") + ", bez broni ponad % bez postepu", (now - o.SinceH) / 24.0, pct);
                if (gone > 0) _dCapHome++;
                _over.Remove(mp);   // od nowa: po odeslaniu (albo gdy nie bylo kogo / wsi) zegar liczy sie znowu przy nastepnej nadwyzce
                return;
            }
            o.Loaded = false;
            _dCapWait++;
            // wyjazd po pierwsza bron dla nadwyzki (najwyzej raz na `wait` godzin na partie)
            if (ArmsFirst.CapTripRecent(mp, now, wait)) { _dCapSpaced++; return; }
            var q = QuoteOf(mp, null);
            if (q.Town == null) { _dCapNoTown++; return; }
            if (q.Men <= 0) { _dCapNoShelf++; ArmsFirst.CoolTown(mp, q.Town); return; }   // 159c recenzja 3: wytchnienie miasta - nastepnej doby wycena w kolejnym
            if (!QuoteOk(q)) { _dCapPoor++; return; }
            if (!ArmsFirst.StartCapTrip(mp, q.Town, now)) { _dCapSpaced++; return; }
            _dCapTrips++;
            Ex(ExCap, Nm(mp) + ": wyjazd po pierwsza bron do " + Nm(q.Town) + " (" + (freed ? "zwolniona z czekania p9" : "gotowa wedlug p9") + "; ludzi " + a.Men
               + ", bez broni " + a.NoWeapon + ", ponad " + PctTxt(pct) + "% " + go + "; na polce pierwsza bron dla " + q.Men + ", za min(" + go + ", " + FirstUnit + ") ludzi "
               + q.Cost + " zl z zapasem, budzet " + q.Budget + "; zegar od " + ((now - o.SinceH) / 24.0).ToString("0.0", Inv) + " dob)");
        }

        /// <summary>159c: koniec zegara ponad % i wyjazdu po pierwsza bron (partia poza regula, w armii, bez nadwyzki, p9 ja przejmuje).</summary>
        private static void DropOver(MobileParty mp)
        {
            _over.Remove(mp);
            try { ArmsFirst.ForgetCap(mp); } catch (Exception e) { Stumble("DropOver", e); }
        }

        // ------------------------------------------------------------ doba
        private static int AgeBucket(MobileParty mp, int today)
        {
            int d;
            if (_born.TryGetValue(mp, out d) || (mp.StringId != null && _savedBorn.TryGetValue(mp.StringId, out d)))
            {
                int a = today - d;
                return a < 1 ? 0 : a < 3 ? 1 : a < 7 ? 2 : 3;
            }
            return 4;
        }

        /// <summary>Globalna doba (ArmouryBehavior, po ArmedStrength.Daily): pamiec z zapisu do partii, wygasle plany, stanie, regula (2), spis P0 i linia doby.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) { ClearDay(); return; }
            int today = Today;
            double now = CampaignTime.Now.ToHours;
            int lords = 0, noEntry = 0, r0 = 0, stuckNow = 0, stuckShort = 0;
            var byAge = new int[5, 3];
            var whyN = new int[WhyCodes.Length];
            var worst = new List<KeyValuePair<MobileParty, int>>();   // P0 doby: niegotowe poza armia (R) - 3 najgorsze opisane jak w bitwie gracza
            _d268Body = _d268BodyNew = 0;
            try
            {
                var all = AiGear.Armories();
                _quotes.Clear();   // 159c: wyceny pierwszej broni - z nowej doby (pamiec najwyzej doba)
                // (a) doba powstania z zapisu i wygasle plany
                var lordList = new List<MobileParty>();
                foreach (var mp in MobileParty.AllLordParties)
                {
                    if (mp == null || !mp.IsActive || mp.IsMainParty) continue;
                    lordList.Add(mp);
                    int d;
                    if (_savedBorn.Count > 0 && mp.StringId != null && _savedBorn.TryGetValue(mp.StringId, out d) && !_born.ContainsKey(mp)) _born[mp] = d;
                    Get(mp);   // plan z zapisu do partii (gdy byl)
                    Stuck sv;
                    if (_savedStuck.Count > 0 && mp.StringId != null && _savedStuck.TryGetValue(mp.StringId, out sv) && !_stuck.ContainsKey(mp)) _stuck[mp] = sv;   // zegar z zapisu
                    Over ov;
                    if (_savedOver.Count > 0 && mp.StringId != null && _savedOver.TryGetValue(mp.StringId, out ov) && !_over.ContainsKey(mp)) _over[mp] = ov;   // recenzja 159b: zegar ponad % z zapisu
                }
                _savedBorn.Clear(); _savedPlans.Clear(); _savedStuck.Clear(); _savedOver.Clear();   // reszta z zapisu: partii juz nie ma
                List<MobileParty> ro = null;
                foreach (var mp in _over.Keys) if (mp == null || !mp.IsActive) { if (ro == null) ro = new List<MobileParty>(); ro.Add(mp); }
                if (ro != null) foreach (var mp in ro) _over.Remove(mp);
                int maxAge = 2 * ArmsFirst.TripDays(s) + 2;
                List<MobileParty> rm = null;
                foreach (var kv in _plans)
                {
                    var p = kv.Value; var mp = kv.Key;
                    if (mp == null || !mp.IsActive) { if (rm == null) rm = new List<MobileParty>(); rm.Add(mp); _dDestroyed++; continue; }
                    if (p.Done || today - p.Day > maxAge) { if (rm == null) rm = new List<MobileParty>(); rm.Add(mp); if (!p.Done) _dExpired++; }
                }
                if (rm != null) foreach (var mp in rm) _plans.Remove(mp);
                // (b) stanie: tylko partie dalej niegotowe, poza armia i czynne
                List<MobileParty> rs = null;
                foreach (var kv in _stuck)
                {
                    var mp = kv.Key;
                    var info = mp != null && mp.IsActive ? ArmedStrength.InfoOf(mp) : null;
                    // po wczytaniu p9 liczy partie kolejka (najwyzej 400 na godzine) - zegar partii jeszcze nie policzonej zostaje
                    bool gone = mp == null || !mp.IsActive || mp.Army != null || (ArmedStrength.Computed(mp) && (info == null || !info.Low));
                    if (gone) { if (rs == null) rs = new List<MobileParty>(); rs.Add(mp); }
                }
                if (rs != null) foreach (var mp in rs) _stuck.Remove(mp);
                // (c) spis P0 i regula (2)
                // recenzja 159 (uwaga 7): regula (2) tylko przy czynnych zakupach AI i p9 (zakupy AI wylaczone = nikt nie kupuje, "nie stac" nie znaczy braku pieniedzy;
                // p9 wylaczone = bez wyjazdow po sprzet) - inaczej jak dotad
                bool home = HomeOn(s);
                double wait = HomeDays(s) * 24.0;
                double pct = AllowedPct(s);   // 159b: bez broni najwyzej tyle % partii
                bool capAll = home && CapAllOn(s);   // recenzja 159b (uwaga 2): regula X% takze dla partii gotowych wedlug p9
                foreach (var mp in lordList)
                {
                    try
                    {
                        if (!mp.IsActive || !GarrisonKit.LordRule(mp)) { DropOver(mp); continue; }
                        lords++;
                        int ab = AgeBucket(mp, today);
                        bool hasEntry = all != null && all.ContainsKey(mp.Id);
                        var info = ArmedStrength.InfoOf(mp);
                        bool zero = !hasEntry || (info != null && info.R == 0);
                        byAge[ab, 0]++;
                        if (!hasEntry) { noEntry++; byAge[ab, 1]++; }
                        if (zero) { r0++; byAge[ab, 2]++; }
                        if (info != null && info.Low && info.ByBody) { _d268Body++; if (ab <= 2) _d268BodyNew++; }   // 268: niegotowe (takze) przez zbroje na tulow
                        if (info == null || !info.Low)
                        {
                            // recenzja 159b (uwaga 2): partia gotowa wedlug p9 (R liczy sloty, takze zbroje - dobrze opancerzona z 30% ludzi bez broni nie jest
                            // niegotowa) - regula X% wprost: bez broni ponad % bez postepu przez UnarmedMenSentHomeDays dob - nadwyzka do domu; 159c: stac na pierwsza
                            // bron dla nadwyzki (wycena polki) - wyjazd po nia (OverCap)
                            if (capAll) OverCap(mp, hasEntry, now, wait, pct, false); else DropOver(mp);
                            continue;
                        }
                        if (mp.Army == null && mp.ActualClan != Clan.PlayerClan) worst.Add(new KeyValuePair<MobileParty, int>(mp, info.R));
                        Stuck st;
                        bool isStuck = _stuck.TryGetValue(mp, out st);
                        if (isStuck) { stuckNow++; whyN[Math.Max(0, Math.Min(WhyCodes.Length - 1, st.Why))]++; }
                        bool due = isStuck && now - st.SinceH >= wait;
                        if (!due && info.Free && !isStuck)
                        {
                            // zwolniona z czekania p9 (AiArmsWaitMaxDays bez dozbrojenia) - do domu tylko, gdy dalej jej nie stac (ArmsFirst.CanAfford, jak powrot do dozbrajania).
                            // 159c (druga recenzja 159b, uwaga 2): p9 jej nie wysyla - z kluczem UnarmedCapAllLordParties, gdy stac ja na pelne dozbrojenie (p9 CanAfford) albo
                            // na pierwsza bron dla nadwyzki wedlug wyceny polki (FirstOk), regula X% jak u gotowej (OverCap: zegar bez postepu, wyjazd po pierwsza bron);
                            // nie stac na zadne - do domu od razu jak dotad
                            int fb, fn;
                            bool can = ArmsFirst.CanAfford(mp, info.Short, null, out fb, out fn, info.ByBody);
                            if (capAll && (can || FirstOk(mp, null))) { OverCap(mp, hasEntry, now, wait, pct, true); continue; }
                            due = !can;
                        }
                        DropOver(mp);   // niegotowa - regula (2) jak dotad (zegar stania p9; wyjazd p9 przejmuje partie)
                        if (!due) { if (isStuck) stuckShort++; continue; }
                        if (!home) continue;
                        if (mp.ActualClan == Clan.PlayerClan) { _dHomeSkipPlayer++; continue; }
                        if (CrownGifts.IsWatchParty(mp) || Undead.Party(mp)) { _dHomeSkipOther++; continue; }
                        if (mp.Army != null || mp.MapEvent != null || mp.SiegeEvent != null || mp.BesiegedSettlement != null || mp.IsCurrentlyAtSea || mp.IsDisbanding
                            || mp.LeaderHero == null || (mp.CurrentSettlement != null && mp.CurrentSettlement.IsUnderSiege))
                        { _dHomeSkipBusy++; continue; }
                        string why = info.Free ? "zwolniona z czekania p9" : WhyCodes[Math.Max(0, Math.Min(WhyCodes.Length - 1, st.Why))];
                        double days = isStuck ? (now - st.SinceH) / 24.0 : ArmedStrength.WaitDaysOf(mp);
                        int gone = SendHome(mp, why, days, pct);
                        if (gone > 0 || isStuck) _stuck.Remove(mp);   // od nowa: po odeslaniu (albo gdy nie bylo kogo) zegar liczy sie znowu przy nastepnym braku
                    }
                    catch (Exception e) { Stumble("Daily(partia)", e); }
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            string p0 = "";
            try
            {
                worst.Sort((a, b) => a.Value != b.Value ? a.Value.CompareTo(b.Value) : b.Key.MemberRoster.TotalManCount.CompareTo(a.Key.MemberRoster.TotalManCount));
                var ds = new List<string>();
                for (int i = 0; i < worst.Count && i < 3; i++) { var mp = worst[i].Key; if (mp != null && mp.IsActive) ds.Add(Describe(mp, "doba")); }
                p0 = ds.Count > 0 ? string.Join(" || ", ds.ToArray()) : "brak";
            }
            catch (Exception e) { Stumble("Daily(P0)", e); }
            try { Line(s, today, lords, noEntry, r0, byAge, stuckNow, stuckShort, whyN, p0); } catch (Exception e) { Stumble("Line", e); }
            ClearDay();
        }

        private static string Age3(int[,] a, int i) { return a[i, 0] + "/" + a[i, 1] + "/" + a[i, 2]; }

        private static int _d268Body, _d268BodyNew;   // 268: spis doby - partie niegotowe (takze) przez zbroje na tulow, z tego nowe druzyny (wiek do 7 dob)

        private static void Line(Settings s, int today, int lords, int noEntry, int r0, int[,] byAge, int stuckNow, int stuckShort, int[] whyN, string p0)
        {
            var sb = new StringBuilder(1800);
            sb.Append("Nowe druzyny i sprzet (159): dzien ").Append(today)
              .Append(" | klucze: New Lord Party Armed Men Only ").Append(s.NewLordPartyArmedMenOnly ? "TAK" : "NIE")
              .Append(", New Lord Party Kit Loan ").Append(s.NewLordPartyKitLoan ? "TAK" : "NIE")
              .Append(", Unarmed Men Sent Home ").Append(s.UnarmedMenSentHome ? "TAK po " + HomeDays(s) + " dobach" : "NIE")
              .Append(", Unarmed Men Allowed ").Append(PctTxt(AllowedPct(s))).Append("%")
              .Append(", Unarmed Cap All Lord Parties ").Append(s.UnarmedCapAllLordParties ? "TAK" : "NIE")
              .Append(GearFit.FloorOn ? "" : " (Gear Fit Weapon Floor NIE - bez broni liczone bez puli)")
              .Append(GarrisonKit.OwnKitOn ? "" : " (TroopsFightWithOwnKitOnly NIE - regula 2 nieczynna)")
              .Append(AiGear.On ? "" : " (AI Buys Gear NIE - plan i regula 2 nieczynne)")
              .Append(s.AiArmsBeforeBattle ? "" : " (AI Arms Before Battle NIE - plan i regula 2 nieczynne)")
              .Append(s.NoFreeKitForNewParties ? "" : " (No Free Kit For New Parties NIE - darmowy komplet DTE, plan nieczynny)");
            sb.Append(" | (1) NOWE DRUZYNY z planem: ").Append(_dPlanParties).Append(" partii, szablon ").Append(_dTemplate).Append(" ludzi -> wzieci ").Append(_dTaken)
              .Append(" (z bronia ").Append(_dArmed).Append(", bez broni w ramach % ").Append(_dUnarmed).Append(" z dozwolonych ").Append(_dAllowed).Append(")")
              .Append(", zostali we wsi (bez broni ponad % albo bez miasta) ").Append(_dTemplate - _dTaken).Append(_dTemplate > 0 ? " (" + (100.0 * (_dTemplate - _dTaken) / _dTemplate).ToString("0", Inv) + "%)" : "")
              .Append(", bez miasta w zasiegu ").Append(_dNoTown).Append(" partii (").Append(_dNoTownMen).Append(" ludzi), bez nikogo ").Append(_dZero)
              .Append("; wycena planow ").Append(_dPlanPieces).Append(" szt. za ").Append(_dPlanCost).Append(" zl (najpierw bron ").Append(_dFirstPieces).Append(" szt. za ").Append(_dFirstCost)
              .Append(" zl; x").Append(Margin.ToString("0.00", Inv))
              .Append(": wlasne ").Append(_dPlanOwn).Append(", pozyczka planowana ").Append(_dPlanLoan).Append("), bez zbroi korpusu u wzietych z bronia (info) ").Append(_dPlanNoBody)
              .Append(", przycieci miara bitwy (CountArms na sztukach planu) ").Append(_dPlanCut)
              .Append("; bez planu: klucz wylaczony ").Append(_dOff).Append(", start gry ").Append(_dStart).Append(", okno nowej gry (ColdStart) ").Append(_dLate)
              .Append(", zakupy AI wylaczone ").Append(_dNoGear).Append(", p9 wylaczone ").Append(_dNoP9).Append(", darmowy komplet DTE ").Append(_dFreeKit)
              .Append(" | WIZYTY z planem: ").Append(_dVisits).Append(" (polka bez sztuk - zwykly zakup, plan czeka ").Append(_dVisitEmpty).Append(")")
              .Append(", kupione ").Append(_dVisitPieces).Append(" szt. za ").Append(_dVisitSpent).Append(" zl (wodz -> kasy miast), z tego wlasne do ").Append(_dVisitOwn)
              .Append("; najpierw bron dla ludzi bez broni: ludzi ").Append(_dVisitFirstMen).Append(", kupione ").Append(_dVisitFirstPieces).Append(" szt. za ").Append(_dVisitFirstGold).Append(" zl")
              .Append(" (kolejne przebiegi po zmierzeniu od nowa, wszystkie wizyty ").Append(_dFirstRounds).Append(")")
              .Append("; POZYCZKI Banku ").Append(_dLoans).Append(" na ").Append(_dLoanSum).Append(" zl (Bank -> glowa rodu -> wodz; drabina 168 splaca), z pozyczki zostalo w kiesach ").Append(_dLoanLeft)
              .Append(", pozyczka przycieta do wyceny planu ").Append(_dLoanCapped)
              .Append("; bez pozyczki (przy planie albo przy wizycie, wedlug powodu): ");
            if (_dLoanNo.Count == 0) sb.Append("-");
            else { bool f = true; foreach (var kv in _dLoanNo) { if (!f) sb.Append(", "); f = false; sb.Append(kv.Key).Append(' ').Append(kv.Value); } }
            sb.Append("; plany wygasle bez wizyty ").Append(_dExpired).Append(", partie zniszczone przed wizyta ").Append(_dDestroyed).Append(", plany w toku ").Append(_plans.Count + _savedPlans.Count);
            string pt = PctTxt(AllowedPct(s));
            sb.Append(" | (2) DO DOMU (ludzie bez broni ponad ").Append(pt).Append("% partii): partii ").Append(_dHomeParties).Append(", ludzi ").Append(_dHomeMen).Append(" (rannych ").Append(_dHomeWounded)
              .Append(", z tego do karczmy (E4/10) ").Append(_dHomeTavern).Append(")")
              .Append("; sprawdzone partie ").Append(_dHomeChecked).Append(": ludzi ").Append(_dHomeAll).Append(", bez broni (po puli zbrojowni) ").Append(_dHomeNoW)
              .Append(" (z puli uzbrojeni ").Append(_dHomePool).Append("), w ramach ").Append(pt).Append("% zostaja ").Append(_dHomeKeep).Append(", ponad % ").Append(_dHomeOver)
              .Append(", bez zbroi korpusu (info - nie odsyla) ").Append(_dHomeNoBody)
              .Append("; wies bez danych BK - zostali ").Append(_dHomeNoVillage).Append(", nic do odeslania (bez broni najwyzej ").Append(pt).Append("%) ").Append(_dHomeNothing)
              .Append(", ta sama partia znowu w ").Append(RepeatDays).Append(" dobach ").Append(_dHomeRepeat)
              .Append("; pominiete: armia/bitwa/oblezenie/morze/rozwiazywana ").Append(_dHomeSkipBusy).Append(", rod gracza ").Append(_dHomeSkipPlayer).Append(", Straz/umarli ").Append(_dHomeSkipOther)
              .Append("; stoja (nie moga sie dozbroic) ").Append(stuckNow).Append(" partii, krocej niz ").Append(HomeDays(s)).Append(" dob ").Append(stuckShort).Append(" [");
            for (int i = 0; i < WhyCodes.Length; i++) { if (i > 0) sb.Append(", "); sb.Append(WhyCodes[i]).Append(' ').Append(whyN[i]); }
            sb.Append("]");
            // recenzja 159b (uwaga 2): regula X% dla partii gotowych wedlug p9 (OverCap) i zwykle wizyty z pierwsza bronia ponad % (do domu - w licznikach (2) wyzej).
            // 159c: takze zwolnione z czekania p9; zegar bez postepu, wyjazdy po pierwsza bron (ArmsFirst.StartCapTrip), wycena polki zamiast progu p9 (QuoteOf)
            double qms = _dQuoteTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            sb.Append(" | (2b) GOTOWE wedlug p9 (i zwolnione z czekania) - bez broni ponad ").Append(pt).Append("%: sprawdzone ").Append(_dCapChecked)
              .Append(" (z tego zwolnione z czekania p9 ").Append(_dCapFree).Append("), ponad % ").Append(_dCapOver)
              .Append(" (zegar biegnie, krocej niz ").Append(HomeDays(s)).Append(" dob bez postepu ").Append(_dCapWait).Append(" - z tego od nowa po postepie (nadwyzka ponizej najmniejszej od startu zegara) ").Append(_dCapProgress)
              .Append("; w drodze po pierwsza bron albo na postoju - zegar czeka ").Append(_dCapTripping)
              .Append("; do domu po ").Append(HomeDays(s)).Append(" dobach bez postepu partii ").Append(_dCapHome).Append("), w bitwie/oblezeniu/na morzu (zegar czeka) ").Append(_dCapBusy)
              .Append(", zegarow w toku ").Append(_over.Count + _savedOver.Count)
              .Append("; WYJAZDY po pierwsza bron (159c): nowe ").Append(_dCapTrips).Append(" (z tego po wczytaniu zamiast odsylki ").Append(_dCapLoadTrip).Append(")")
              .Append(", bez wyjazdu: brak miasta w zasiegu ").Append(_dCapNoTown)
              .Append(", na polce nic dla nich (wytchnienie miasta) ").Append(_dCapNoShelf).Append(", nie stac wedlug wyceny polki ").Append(_dCapPoor)
              .Append(", wyjazd w ostatnich ").Append(HomeDays(s)).Append(" dobach albo p9 w drodze ").Append(_dCapSpaced)
              .Append("; dojechaly ").Append(_dCapTripEv[CapArrived]).Append(", po postoju ").Append(_dCapTripEv[CapDone]).Append(", za dlugo w drodze ").Append(_dCapTripEv[CapLate])
              .Append(", przerwane (armia, p9, miasto niewazne, bez nadwyzki) ").Append(_dCapTripEv[CapEnded]).Append(", w toku ").Append(ArmsFirst.CapTripCount)
              .Append("; NIEGOTOWE p9 bez pieniedzy na pelne dozbrojenie, ktore stac na pierwsza bron dla nadwyzki (wyjazd zamiast \"nie stac\") ").Append(_dLowFirst.Count)
              .Append(" partii (bez miasta albo polki mimo to ").Append(_dLowFirstNoTrip.Count).Append(")")
              .Append("; wyceny pierwszej broni ").Append(_dQuotes).Append(" (").Append(qms.ToString("0.0", Inv)).Append(" ms)")
              .Append("; zwykle wizyty z pierwsza bronia ponad %: ").Append(_dCapVisits).Append(", ludzi ").Append(_dCapVisitMen).Append(", kupione ").Append(_dCapVisitPieces)
              .Append(" szt. za ").Append(_dCapVisitGold).Append(" zl");
            sb.Append(" | SPIS (P0): partie lordow AI ").Append(lords).Append(", bez wpisu DTE ").Append(noEntry).Append(", R = 0 (z brakiem wpisu) ").Append(r0)
              .Append("; wedlug wieku partii (wszystkie/bez wpisu/R=0): < 1 doby ").Append(Age3(byAge, 0)).Append(", 1-3 ").Append(Age3(byAge, 1)).Append(", 3-7 ").Append(Age3(byAge, 2))
              .Append(", 7+ ").Append(Age3(byAge, 3)).Append(", sprzed wczytania (wiek nieznany) ").Append(Age3(byAge, 4))
              .Append("; niegotowe przez zbroje na tulow (268): wszystkie ").Append(_d268Body).Append(", nowe druzyny niegotowe przez zbroje (wiek do 7 dob) ").Append(_d268BodyNew);
            sb.Append(" | P0 niegotowe poza armia - 3 najnizsze R (stan po regule 2): ").Append(p0);
            sb.Append(" | przyklady: ").Append(_dEx.Count > 0 ? string.Join("; ", _dEx.ToArray()) : "brak");
            sb.Append(" | zapis: wczytane ").Append(_importN < 0 ? "brak klucza" : _importN.ToString()).Append(_importBad > 0 ? " (bledne " + _importBad + ")" : "")
              .Append(" | potkniecia ").Append(_dStumbles).Append(" (sesja ").Append(_stumbles).Append(", wycena ").Append(AiGear.QuoteStumbles).Append(")").Append('.');
            Log.Info(sb.ToString());
        }

        // ------------------------------------------------------------ P0: bitwa gracza
        /// <summary>Raz na bitwe gracza i zrodlo: start spotkania z gracza (MapEventStarted - partie dolaczajace z okolicy dochodza dopiero potem, w
        /// PlayerEncounter.CheckNearbyPartiesToJoinPlayerMapEvent; bez misji - jedyna linia) i misja DTE (pelny sklad bitwy): stan kazdej partii lorda AI tej bitwy.
        /// Tylko log, nie rzuca.</summary>
        internal static void BattleLine(MapEvent me, string where)
        {
            try
            {
                if (me == null) return;
                var last = _lastBattle != null ? _lastBattle.Target : null;
                if (ReferenceEquals(last, me) && string.Equals(_lastBattleWhere, where, StringComparison.Ordinal)) return;
                _lastBattle = new WeakReference(me); _lastBattleWhere = where;
                var parts = new List<string>();
                BattleSideEnum my = BattleSideEnum.None;
                try { my = PartyBase.MainParty != null ? PartyBase.MainParty.Side : BattleSideEnum.None; } catch { }
                foreach (var pb in me.InvolvedParties)
                {
                    var mp = pb != null ? pb.MobileParty : null;
                    if (mp == null || mp.IsMainParty || !mp.IsLordParty) continue;
                    try { parts.Add(Describe(mp, pb.Side == my ? "sojusznik" : "wrog")); }
                    catch (Exception e) { Stumble("BattleLine(partia)", e); }
                }
                if (parts.Count == 0) return;
                Log.Info("Bitwa gracza (159 P0, " + where + "): partie lordow AI " + parts.Count + " || " + string.Join(" || ", parts.ToArray()) + ".");
            }
            catch (Exception e) { Stumble("BattleLine", e); }
        }

        private static string Describe(MobileParty mp, string side)
        {
            var s = Settings.Current;
            var all = AiGear.Armories();
            Dictionary<ItemObject, int> arm = null;
            bool entry = all != null && all.TryGetValue(mp.Id, out arm);
            int needS, usedS, bodyM, bodyF;
            int r = GarrisonKit.FillPercent(mp, arm, null, true, out needS, out usedS, out bodyM, out bodyF);   // 268: takze zbroja na tulow teraz
            var info = ArmedStrength.InfoOf(mp);
            int r9, b9, c9; bool l9;
            bool k9 = ArmedStrength.StateOf(mp, out r9, out b9, out l9, out c9);   // 268: B, na ktorym p9 oparlo ostatnia decyzje
            string state = info == null ? "nie niegotowa (albo niepoliczona)" : info.Low ? (info.Free ? "NIEGOTOWA, zwolniona z czekania" : "NIEGOTOWA") : "gotowa (F " + info.F.ToString("0.00", Inv) + ")";
            double w = ArmedStrength.WaitDaysOf(mp);
            int b, need;
            bool can = ArmsFirst.CanAfford(mp, info != null ? info.Short : 0, null, out b, out need, info != null && info.ByBody);
            Stuck st;
            string stuck = _stuck.TryGetValue(mp, out st) ? WhyCodes[Math.Max(0, Math.Min(WhyCodes.Length - 1, st.Why))] + " od " + ((CampaignTime.Now.ToHours - st.SinceH) / 24.0).ToString("0.0", Inv) + " dob" : "-";
            var lord = mp.LeaderHero;
            int d;
            string born = _born.TryGetValue(mp, out d) || (mp.StringId != null && _savedBorn.TryGetValue(mp.StringId, out d))
                          ? "dzien " + d + " (wiek " + (Today - d) + " dob)" : "sprzed wczytania";
            string ai = mp.DefaultBehavior + (mp.TargetSettlement != null ? " -> " + Nm(mp.TargetSettlement) : mp.TargetParty != null ? " -> " + Nm(mp.TargetParty) : "") + ", krotko " + mp.ShortTermBehavior;
            var p = Get(mp);
            string plan = p != null && !p.Done ? "miasto " + Nm(p.Town) + ", ludzi " + p.Men + " z " + p.Template + ", wycena " + p.Cost + " zl" : "-";
            return Nm(mp) + " [" + side + "] ludzi " + mp.MemberRoster.TotalManCount + " (rannych " + mp.MemberRoster.TotalWounded + ")"
                   + " | R teraz " + r + "% (" + usedS + "/" + needS + " slotow), wpis DTE " + (entry ? "TAK" : "NIE") + ", p9: " + state
                   + (w >= 0 ? ", czeka " + w.ToString("0.0", Inv) + " dob" : "")
                   + " | zbroja na tulow (268): B teraz " + ArmedStrength.BodyPercent(bodyM, bodyF) + "% (" + bodyF + "/" + bodyM + " ludzi), B p9 " + (k9 ? b9 + "% (R p9 " + r9 + "%)" : "-")
                   + ", niegotowa przez zbroje " + (info != null && info.Low && info.ByBody ? "TAK" : "NIE")
                   + " | cel p9 " + ArmsFirst.TargetName(mp) + ", stoi: " + stuck + " | zloto wodza " + (lord != null ? lord.Gold.ToString(Inv) : "-")
                   + ", budzet AiGear " + b + " z potrzebnych " + need + (can ? "" : " - nie stac")
                   + " | w armii " + (mp.Army != null ? "TAK (" + Nm(mp.Army.LeaderParty) + ")" : "NIE") + " | powstala " + born + " | AI " + ai + " | plan 159: " + plan;
        }

        // ------------------------------------------------------------ zapis (arm_159_kit)
        private static readonly char[] Bad = { ',', ';', '|' };
        private static bool IdOk(string id) { return !string.IsNullOrEmpty(id) && id.IndexOfAny(Bad) < 0; }

        /// <summary>"1|plany|stanie|doby powstania": plan "partia,miasto,ludzi,szablon,sztuk,wycena,wlasne,pozyczka,dzien"; stanie "partia,odH,kod"; powstanie
        /// "partia,dzien". Razem z wpisami z zapisu jeszcze nie przypietymi do partii. Recenzja 159b: "|zegar ponad %" - "partia,odH" (OverCap); 159c - "partia,odH,go"
        /// (nadwyzka przy ostatnim sprawdzeniu; nieznana - bez trzeciego pola).</summary>
        internal static string Export()
        {
            var sb = new StringBuilder("1|");
            bool first = true;
            foreach (var kv in _plans)
            {
                var p = kv.Value; var id = kv.Key != null ? kv.Key.StringId : null;
                if (p.Done || !IdOk(id) || (p.TownId != null && !IdOk(p.TownId))) continue;
                if (!first) sb.Append(';'); first = false;
                AppendPlan(sb, id, p);
            }
            foreach (var kv in _savedPlans)
            {
                if (!IdOk(kv.Key)) continue;
                if (!first) sb.Append(';'); first = false;
                AppendPlan(sb, kv.Key, kv.Value);
            }
            sb.Append('|');
            first = true;
            foreach (var kv in _stuck)
            {
                var id = kv.Key != null ? kv.Key.StringId : null;
                if (!IdOk(id)) continue;
                if (!first) sb.Append(';'); first = false;
                sb.Append(id).Append(',').Append(kv.Value.SinceH.ToString("0.##", Inv)).Append(',').Append(kv.Value.Why);
            }
            foreach (var kv in _savedStuck)
            {
                if (!IdOk(kv.Key)) continue;
                if (!first) sb.Append(';'); first = false;
                sb.Append(kv.Key).Append(',').Append(kv.Value.SinceH.ToString("0.##", Inv)).Append(',').Append(kv.Value.Why);
            }
            sb.Append('|');
            first = true;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kv in _born)
            {
                var id = kv.Key != null && kv.Key.IsActive ? kv.Key.StringId : null;
                if (!IdOk(id) || !seen.Add(id)) continue;
                if (!first) sb.Append(';'); first = false;
                sb.Append(id).Append(',').Append(kv.Value);
            }
            foreach (var kv in _savedBorn)
            {
                if (!IdOk(kv.Key) || !seen.Add(kv.Key)) continue;
                if (!first) sb.Append(';'); first = false;
                sb.Append(kv.Key).Append(',').Append(kv.Value);
            }
            // recenzja 159b (uwaga 2): czwarta sekcja - zegar "bez broni ponad %" partii gotowych wedlug p9 "partia,odH" (starsza wersja czyta trzy sekcje - pomija).
            // 159c: "partia,odH,go" - 159b czyta dwa pierwsze pola (c.Length >= 2), trzecie pomija
            sb.Append('|');
            first = true;
            seen.Clear();
            foreach (var kv in _over)
            {
                var id = kv.Key != null && kv.Key.IsActive ? kv.Key.StringId : null;
                if (!IdOk(id) || kv.Value == null || !seen.Add(id)) continue;
                if (!first) sb.Append(';'); first = false;
                AppendOver(sb, id, kv.Value);
            }
            foreach (var kv in _savedOver)
            {
                if (!IdOk(kv.Key) || kv.Value == null || !seen.Add(kv.Key)) continue;
                if (!first) sb.Append(';'); first = false;
                AppendOver(sb, kv.Key, kv.Value);
            }
            return sb.ToString();
        }

        private static void AppendOver(StringBuilder sb, string id, Over o)
        {
            sb.Append(id).Append(',').Append(o.SinceH.ToString("0.##", Inv));
            if (o.Go >= 0) sb.Append(',').Append(o.Go.ToString(Inv));
        }

        private static void AppendPlan(StringBuilder sb, string id, KitPlan p)
        {
            sb.Append(id).Append(',').Append(p.TownId ?? "").Append(',').Append(p.Men).Append(',').Append(p.Template).Append(',').Append(p.Pieces).Append(',')
              .Append(p.Cost).Append(',').Append(p.Own).Append(',').Append(p.Loan).Append(',').Append(p.Day);
        }

        /// <summary>Wczytanie (po Reset z konstruktora zachowania): brak klucza albo zly format - pusto (plany i zegary od nowa).</summary>
        internal static void Import(string v)
        {
            _savedPlans.Clear(); _savedStuck.Clear(); _savedBorn.Clear(); _savedOver.Clear(); _importN = -1; _importBad = 0;
            if (string.IsNullOrEmpty(v) || !v.StartsWith("1|", StringComparison.Ordinal)) return;
            _importN = 0;
            var sec = v.Substring(2).Split('|');
            try
            {
                if (sec.Length > 0)
                    foreach (var row in sec[0].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var c = row.Split(',');
                        int men, tpl, pcs, day; long cost, own, loan;
                        if (c.Length < 9 || c[0].Length == 0 || !int.TryParse(c[2], NumberStyles.Integer, Inv, out men) || !int.TryParse(c[3], NumberStyles.Integer, Inv, out tpl)
                            || !int.TryParse(c[4], NumberStyles.Integer, Inv, out pcs) || !long.TryParse(c[5], NumberStyles.Integer, Inv, out cost)
                            || !long.TryParse(c[6], NumberStyles.Integer, Inv, out own) || !long.TryParse(c[7], NumberStyles.Integer, Inv, out loan)
                            || !int.TryParse(c[8], NumberStyles.Integer, Inv, out day)) { _importBad++; continue; }
                        _savedPlans[c[0]] = new KitPlan { TownId = c[1].Length > 0 ? c[1] : null, Men = men, Template = tpl, Pieces = pcs, Cost = cost, Own = own, Loan = loan, Day = day };
                        _importN++;
                    }
                if (sec.Length > 1)
                    foreach (var row in sec[1].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var c = row.Split(',');
                        double h; int why;
                        if (c.Length < 3 || c[0].Length == 0 || !double.TryParse(c[1], NumberStyles.Float, Inv, out h) || !int.TryParse(c[2], NumberStyles.Integer, Inv, out why) || h < 0)
                        { _importBad++; continue; }
                        _savedStuck[c[0]] = new Stuck { SinceH = h, Why = Math.Max(0, Math.Min(WhyCodes.Length - 1, why)) };
                        _importN++;
                    }
                if (sec.Length > 2)
                    foreach (var row in sec[2].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var c = row.Split(',');
                        int day;
                        if (c.Length < 2 || c[0].Length == 0 || !int.TryParse(c[1], NumberStyles.Integer, Inv, out day)) { _importBad++; continue; }
                        _savedBorn[c[0]] = day;
                        _importN++;
                    }
                if (sec.Length > 3)   // recenzja 159b: zegar "bez broni ponad %" (zapis bez tej sekcji - pusto); 159c: trzecie pole - nadwyzka (wpis 159b bez niego - nieznana)
                    foreach (var row in sec[3].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var c = row.Split(',');
                        double h; int go = -1;
                        if (c.Length < 2 || c[0].Length == 0 || !double.TryParse(c[1], NumberStyles.Float, Inv, out h) || h < 0) { _importBad++; continue; }
                        if (c.Length >= 3 && (!int.TryParse(c[2], NumberStyles.Integer, Inv, out go) || go < 0)) { go = -1; _importBad++; }
                        _savedOver[c[0]] = new Over { SinceH = h, Go = go, Loaded = true };
                        _importN++;
                    }
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
