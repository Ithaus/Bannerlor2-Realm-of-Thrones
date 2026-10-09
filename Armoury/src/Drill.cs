using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// MUSZTRA (docs/PROJEKT-MUSZTRA-2026-10-09.md, wersja 2 po krytyce; audyt 13 Z14a, Z14b; decyzje Jeffa 09.10 03:45 pkt 1-2, 04:00 pkt 1-2;
    /// MUSZTRA-j - rozdz. 14 "Jeden wzor", decyzje Jeffa 09.10 07:10 pkt 1-2).
    /// JEDEN WZOR dla gracza, rodu gracza, lordow AI i glow rodow AI - pod wzor podpinaja sie dane danego lorda (Jeff 07:10: "jest jedna zasada, wzor dla
    /// wszystkich"). Doswiadczenie oddzialu partii lorda na czlowieka na dobe: XP = (B x L x D x S + P) x A, a w dobie, ktora zaczela sie od switu z dlugiem
    /// snu, albo przy glodzie XP = 0 (takze perki - "wcale", zapas sie nie zuzywa).
    ///  B - 10 + 2 x tier dla kazdego (RuleBase) - takze glowy rodu AI: baza glowy rodu gry i BK (15 + 3 x tier) nie dziala; w bitwie 0 jak w grze.
    ///      Baza gry (GameBase - ten sam predykat co gra i BK) sluzy juz tylko do wydzielenia perkow P z wyniku modelu, do pomiaru "baza gry" i do
    ///      wylaczonego DrillLawAi (cofniecie: plaski trening gry).
    ///  L - dowodca: Przywodztwo / 170 w granicach 0.5-1.5 (bez dowodcy 0.5).
    ///  D - dzien: 1.5 postoj (mniej niz 4 godziny ruchu z 24), 0.9 marsz. Godzina postoju = RestHour: osada, oboz obleznikow albo ruch <= 0.35 jedn./h
    ///      (czesc wspolna z ksiega snu NightRest.OnHourly i T10 R2 - obie wolaja RestHour od scalenia grupa11; sen dolicza po swojej stronie morze,
    ///      sluzbe ROT i oboz swiata - to zasady snu, nie postoju); godzina niezaobserwowana = ruch (wczytanie nie daje postoju). Maski wszystkich partii
    ///      lordow ida do zapisu (gracz - pole M=, pozostali od MUSZTRA-jp - segment T=), wiec po wczytaniu kazda partia liczy D z wlasnych godzin.
    /// Kogo wzor nie obejmuje: zalogi, bandy, karawany; Inni (umarli - nie spia, nie znaja zmeczenia: zostaja przy treningu gry); partia nieaktywna
    /// (poza mapa: niewola, rejs BK, sluzba ROT) przy wzorze nie cwiczy wcale - Z14a jak dotad, AI przy DrillLawAi od MUSZTRA-jp (bez obserwacji nie ma D).
    ///  Dlug snu (kara "noc bez snu = nastepny dzien bez cwiczen", Jeff 07:10 pkt 2) - DLUG O OSTATNIM SWICIE, NightRest.DawnDebtOf: gracz z jego
    ///      ksiegi (DawnDebt ustawiany w SettleNight), kazda inna partia lorda z ksiegi snu AI T10 (AiSleep.DawnDebt, SettleAi) - ten sam dlug, ktory
    ///      zabiera predkosc i morale; rozni sie tylko chwila odczytu (predkosc i morale - dlug biezacy, dzien cwiczen - swit), niezaleznie od godziny ticku.
    ///  S - zapas do cwiczen: 1 + 0.10 x uB x kB + 0.10 x uZ x kZ; u = zapas / pelny (pelny = sztuka na 3 ludzi), k = 1.5 z perkiem kwatermistrza
    ///      (Giving Hands - bron, Paid in Promise - zbroja).
    ///  P - perki gry i BK w treningu (wynik modelu ponad baze gry); A - udzial uzbrojonych z 171 (ArmsDrill).
    /// Z14a (DrillLaw): partie, ktorym gra nie daje bazy (gracz, rod gracza, lordowie w armii gracza) - caly wzor. AI poza Z14a: caly wzor przy DrillLawAi
    /// (Z14b, domyslnie TAK od MUSZTRA-j); wylaczony = plaska baza gry x [S przy DrillStockAi] + P. Kara (XP = 0 przy glodzie albo dlugu o swicie, takze
    /// perki) przy DrillPenaltyAi (domyslnie wlaczona - "takie same kary jak gracz", Jeff 09.10 04:00 i ponownie) albo DrillLawAi. Zapas gracza (DrillStock przy DrillLaw i Z1): sprzet
    /// wyrzucony na zwyklym ekranie ekwipunku, zostawiony na ekranie lupu gry i trofea Spoils zostawione przy "Leave" (do 2 x pelny na grupe) + nadwyzka
    /// ludzi w zbrojowni DTE; AI: tylko nadwyzka zbrojowni ponad komplet (tabor - lup i zaopatrzenie BK - sie nie liczy, jak sakwy gracza). Zapas zuzywa
    /// sie (sztuka w uzyciu sluzy 200 dni cwiczen), zlom (polowa rudy sztuki) odkupuja kowale miasta przy wizycie, zaplata dla ludzi (AI: trzecia lordowi);
    /// zapasu nie da sie wyjac i nie wraca do sakw.
    /// Liczniki tylko w ticku treningu partii (MobilePartyTrainingBehavior.OnDailyTickParty), linie dnia o polnocy.
    /// WARUNEK SCALENIA K1 - SPELNIONY w scaleniu sklad8 (sklad7 z K1 + grupa11): PlayerSurplus i AiSurplus licza nadwyzke funkcja nadwyzki K1 A9
    /// (MenPurse.PlayerSurplusPlan / AiSurplusPlan / AiMeleeLeft - po dopasowaniu, ponad komplet + SurplusKeepPercent), ta sama co sprzedaz nadwyzek
    /// kupcom - musztra nie zuzywa sztuk, ktore K1 uznaje za potrzebne, wiec zakupy brakow ich nie odkupuja (bez petli kupna i zuzycia na koszt lordow).
    /// Autotest po scaleniu: zakupy AI ("ZakupyAI") wobec zuzycia musztry ("Musztra AI: zuzyto").
    /// </summary>
    internal static class Drill
    {
        // ------------------------------------------------------------ stale (projekt rozdz. 2)
        private const float RestDay = 1.5f, MarchDay = 0.9f, LeadNorm = 170f, LeadMin = 0.5f, LeadMax = 1.5f;
        private const float StockBonus = 0.10f, PerkMult = 1.5f, WearDays = 200f;
        internal const float RestStep = 0.35f;            // jedna stala dla musztry, ksiegi snu gracza (NightRest.OnHourly) i ksiegi AI T10 R2 (RestMoveLimit)
        private const int RestBelowHours = 4, MenPerPiece = 3, IntakeSets = 2, AllHours = 0xFFFFFF;
        private const int GW = 0, GA = 1;                 // grupy zapasu: bron (z tarczami), zbroje
        // MUSZTRA-j: doby t1 -> t6 przy bazie wzoru x1 (koszty awansu BK TroopUpgradeXp 3.0: 900/1650/2700/3900/5100, projekt rozdz. 2 i 14.3) - tylko do linii
        private const double PromoteDays = 900.0 / 12 + 1650.0 / 14 + 2700.0 / 16 + 3900.0 / 18 + 5100.0 / 20;
        internal const int SrcDiscard = 0, SrcLoot = 1, SrcTrophies = 2, SrcAutotest = 3;
        private static readonly string[] SrcName = { "wyrzucone", "lup", "trofea", "autotest" };

        /// <summary>Zapas gracza czynny: DrillStock, ale tylko przy DrillLaw (bez musztry zapas nic nie daje, wiec niczego nie przyjmuje) i przy Z1
        /// (DonationXpOff): zapas zastepuje XP za oddany sprzet - przy wylaczonym Z1 gra daje XP za oddanie, wiec zapas nie przyjmuje (albo XP, albo zapas).
        /// Jeden warunek dla przyjecia z ekranow 1-3, S i zuzycia gracza, napisow Spoils i opisow perkow.</summary>
        internal static bool StockOn { get { var s = Settings.Current; return s != null && s.DrillStock && s.DrillLaw && DonationXpLaw.On; } }

        /// <summary>Napisy Spoils "Leave" o zapasie: tylko gdy zapas czynny i prefiks Leave naprawde wpiety (inaczej trofea zostaja na polu jak dotad).</summary>
        internal static bool LeaveOn { get { return StockOn && SpoilsSeal.DrillLeaveWired; } }

        /// <summary>Kara AI (glod, dlug snu - XP 0 z perkami, bez zuzycia) przy DrillPenaltyAi albo przy calej regule Z14b.</summary>
        private static bool PenaltyAi(Settings s) { return s.DrillPenaltyAi || s.DrillLawAi; }

        /// <summary>Cokolwiek z musztry wlaczone (godziny ruchu i tick partii pracuja tylko wtedy - albo gdy czeka zlom do sprzedania).</summary>
        private static bool Active(Settings s) { return s != null && (s.DrillLaw || s.DrillStockAi || s.DrillLawAi || s.DrillPenaltyAi || s.DrillLog); }

        // ------------------------------------------------------------ stan
        private sealed class Track { internal Vec2 Pos; internal long Stamp = -1; internal int Mask = AllHours; }
        private static readonly Dictionary<MobileParty, Track> _tr = new Dictionary<MobileParty, Track>();
        private static int _pendingMainMask = -1;
        // MUSZTRA-jp (recenzja 1): maski godzin ruchu WSZYSTKICH partii lordow poza graczem z zapisu (segment "T=" napisu arm_drill): id -> maska, wiek
        // (godziny od ostatniej obserwacji w chwili zapisu); odtwarzane w SessionStart jak maska gracza - po wczytaniu kazda partia liczy postoj
        // i marsz z wlasnych danych, nie "cala doba marszu" (godziny spoza zapisu dalej = ruch)
        private static readonly Dictionary<string, KeyValuePair<int, int>> _pendingMasks = new Dictionary<string, KeyValuePair<int, int>>();

        private sealed class Acc { internal float WW, WA, Ore; }   // liczniki zuzycia (bron, zbroje) i zlom czekajacy na kowali (sztuki ludzi)
        private static readonly Dictionary<string, Acc> _acc = new Dictionary<string, Acc>();
        private static readonly ItemRoster _stock = new ItemRoster();         // zapas od gracza (wlasnosc ludzi)

        internal sealed class Ctx
        {
            internal MobileParty Party; internal double At;
            internal bool Main, NoBase, Rest, Hungry, Sleepless, Zero, Off, StockOn, ArmsGate, PerkW, PerkA, Counted;
            internal bool ClanHead;                        // MUSZTRA-j: glowa rodu AI, ktorej gra (i BK) daje 15 + 3 x tier - tylko do linii (wzor jej tego nie daje)
            internal int Men, Lead = -1, Moved, Debt, Full, GivenW, GivenA, SurW, SurA;
            internal int DebtNow;                          // MUSZTRA-j: Debt = dlug o ostatnim swicie (kara); DebtNow = dlug biezacy (predkosc, morale) - tylko do linii
            internal float L = LeadMin, D = MarchDay, S = 1f;
            internal int StockW { get { return GivenW + SurW; } }
            internal int StockA { get { return GivenA + SurA; } }
        }
        private static Ctx _ctx;                           // kontekst partii w biezacym ticku (L, D, S liczone raz na partie)

        // element w toku (Shape -> Done; watek glowny)
        // _eB - baza naprawde uzyta (wzor albo plaska baza gry), _eGB - baza gry naprawde dana (pomiar), _eRB - baza wzoru (pomiar, takze przy wylaczonym Z14b)
        private static bool _e, _eNoModel; private static Ctx _eC; private static float _eB, _eGB, _eRB, _eP, _ePre, _eGame; private static int _eN; private static CharacterObject _eCh;
        internal static bool ElemArmsGate { get { return _e && _eC != null && _eC.ArmsGate; } }
        /// <summary>MUSZTRA-j: dzien kary partii AI (wynik 0) - ArmsDrill liczy udzial uzbrojonych tylko do pomiaru "XP gry po broni" (gra kary nie zna).</summary>
        internal static bool ElemOffWithGameXp { get { return _e && _eC != null && _eC.Off && !_eC.NoBase && _eGame > 0f; } }

        // tick treningu (latka MobilePartyTrainingBehavior.OnDailyTickParty)
        private static bool _tickHooked;
        private static MobileParty _tick;
        private static readonly Dictionary<CharacterObject, int> _room = new Dictionary<CharacterObject, int>();
        private static long _xpBefore;

        // ekrany zapasu
        private static int _opening;                       // 0 - nic, 1 + Src - otwierany ekran z bialej listy
        private static InventoryLogic _screen; private static int _screenKind;
        private static bool _hookDiscard, _hookLoot, _hookInit;

        // autotest i zapis
        private static int _autotest = -1, _dailyN, _feedTries;
        private static bool _fed;
        private static string _pendingStock, _importNote;
        private static int _importRejected;
        private static bool _maskSegSeen;                  // MUSZTRA-jp: w zapisie byl segment T= (maski partii lordow) - tylko do linii startowej

        // ------------------------------------------------------------ liczniki doby
        private sealed class PlayerRec
        {
            internal Ctx C; internal int Day = -1;
            internal double B, P, Pre, Fin, ShN, N; internal long Computed, Accepted = -1, Cut;   // ShN, N - udzial uzbrojonych wazony liczba ludzi (A niezaleznie)
        }
        private static PlayerRec _pr, _prLast;
        private static int _msgDay = -1;                   // MUSZTRA-m: doba ostatniej linii musztry gracza w grze (raz na dobe)
        private static readonly int[] _in = new int[4];
        private static int _noRoom, _pWornW, _pWornA, _pWornGiven, _pNoMetal, _pSoldU, _pSoldGold, _givenYday = -1;
        private static float _pOreAdd;
        private static int _cParties, _cMen, _cRest, _cOff; private static double _cXp, _cL;
        private static int _aParties, _aMen, _aNoLead, _aFullStock, _aNoStock, _aWornW, _aWornA, _aNoMetal, _aSoldU, _aSoldGold, _aSoldLord, _aSoldPurse, _aOff;
        private static float _aOreAdd, _oreLost;
        private static double _aW, _aWL, _aWD, _aWS, _aWLD, _aWLDS, _aWRest, _aWMarch, _aWHungry, _aWSleep, _aGame, _aRule, _aPenalty, _aWn, _aWLDn;
        // MUSZTRA-j (jeden wzor): baza wzoru, baza gry glow rodow, perki P, XP po broni "gra" (baza gry + perki, bez kary - jak dzis w grze) i "teraz"
        // (wynik czynnej regule), ludzio-dni; elementy, w ktorych model nie dal bazy; sen od switu: partie z dlugiem o swicie, w tym splacone przed
        // treningiem, oraz kontrola "dlug teraz > dlug o swicie" (ma byc 0; wszystkie partie lordow poza graczem)
        private static double _aRuleBase, _aHeadBase, _aPerks, _aXpGame, _aXpNow, _aManN;
        private static int _aNoModel, _aSleepDawn, _aSleepPaid, _sleepNowAbove;
        private static readonly double[] _aWT = new double[3];   // razem (z zapasem i dniami kary) przy progu postoju 4 / 8 / 12 h
        private static readonly double[] _aWTn = new double[3];  // dowodca x dzien BEZ zapasu, tylko dni bez kary, przy progu 4 / 8 / 12 h (prog Z14b)
        private static readonly int[] Thresholds = { 4, 8, 12 };
        private static readonly int[] _hb = new int[4];          // godziny ruchu w dobie: 0 / 1-3 / 4-11 / 12+
        private static readonly List<int> _leads = new List<int>();
        private sealed class KAcc
        {
            internal string Name; internal int PartyDays; internal double W, WLD, WS, Wn;
            internal double XpG, XpN, ManN, RB, HB;               // MUSZTRA-j: jak _aXpGame, _aXpNow, _aManN, _aRuleBase, _aHeadBase - wedlug krolestw
            internal readonly double[] WTn = new double[3];       // jak _aWTn - wedlug krolestw
            internal readonly int[] Hb = new int[4];
        }
        private static readonly Dictionary<string, KAcc> _k = new Dictionary<string, KAcc>();
        private static int _stumbles, _errDay = -1;
        private static readonly HashSet<string> _errWhere = new HashSet<string>();
        private static long _ticks;
        private static int _clk; private static long _clk0;   // zegar kosztu: liczy tylko wejscie zewnetrzne (zagniezdzone wolania - raz)

        internal static void Reset()
        {
            _tr.Clear(); _pendingMainMask = -1; _pendingMasks.Clear(); _acc.Clear(); _stock.Clear(); _ctx = null; _e = false; _eC = null; _tick = null; _room.Clear();
            _screen = null; _opening = 0; _dailyN = 0; _feedTries = 0; _fed = false; _pendingStock = null; _importNote = null; _importRejected = 0; _maskSegSeen = false;
            _pr = null; _prLast = null; _msgDay = -1; _givenYday = -1; ClearDay(); _k.Clear(); _stumbles = 0; _errDay = -1; _errWhere.Clear(); _clk = 0;
            _ore = null;   // przedmioty gry sa tworzone na nowo przy kazdej grze - nie trzymac obiektu z poprzedniej kampanii
        }

        private static void ClearDay()
        {
            Array.Clear(_in, 0, _in.Length); _noRoom = _pWornW = _pWornA = _pWornGiven = _pNoMetal = _pSoldU = _pSoldGold = 0; _pOreAdd = 0f;
            _cParties = _cMen = _cRest = _cOff = 0; _cXp = _cL = 0;
            _aParties = _aMen = _aNoLead = _aFullStock = _aNoStock = _aWornW = _aWornA = _aNoMetal = _aSoldU = _aSoldGold = _aSoldLord = _aSoldPurse = _aOff = 0;
            _aOreAdd = 0f; _oreLost = 0f;
            _aW = _aWL = _aWD = _aWS = _aWLD = _aWLDS = _aWRest = _aWMarch = _aWHungry = _aWSleep = _aGame = _aRule = _aPenalty = _aWn = _aWLDn = 0;
            _aRuleBase = _aHeadBase = _aPerks = _aXpGame = _aXpNow = _aManN = 0; _aNoModel = _aSleepDawn = _aSleepPaid = _sleepNowAbove = 0;
            Array.Clear(_aWT, 0, _aWT.Length); Array.Clear(_aWTn, 0, _aWTn.Length); Array.Clear(_hb, 0, _hb.Length); _leads.Clear(); _ticks = 0;
        }

        /// <summary>Zegar kosztu musztry (T1): Clk na wejsciu kazdej metody wolanej z zewnatrz (godzina, model treningu, tick partii, ekrany, miasto,
        /// linie dnia), Unclk w finally; zagniezdzone wejscia licza sie raz (licznik glebokosci, watek glowny).</summary>
        private static void Clk() { if (_clk++ == 0) _clk0 = Stopwatch.GetTimestamp(); }
        private static void Unclk() { if (_clk > 0 && --_clk == 0) _ticks += Stopwatch.GetTimestamp() - _clk0; }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try
            {
                int d = (int)CampaignTime.Now.ToDays;
                if (d != _errDay) { _errDay = d; _errWhere.Clear(); }
                if (_errWhere.Add(where)) Log.Error("Drill." + where, e);
            }
            catch { }
        }

        private static float Clamp(float v, float lo, float hi) { return v < lo ? lo : (v > hi ? hi : v); }
        private static string F2(double v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }
        private static string F3(double v) { return v.ToString("0.000", CultureInfo.InvariantCulture); }
        private static string F1(double v) { return v.ToString("0.0", CultureInfo.InvariantCulture); }
        private static int Pop(int m) { int n = 0; while (m != 0) { m &= m - 1; n++; } return n; }

        // ------------------------------------------------------------ grupy i sztuki zapasu
        private static int GroupOf(ItemObject it)
        {
            if (it == null) return -1;
            switch (it.ItemType)
            {
                case ItemObject.ItemTypeEnum.OneHandedWeapon:
                case ItemObject.ItemTypeEnum.TwoHandedWeapon:
                case ItemObject.ItemTypeEnum.Polearm:
                case ItemObject.ItemTypeEnum.Bow:
                case ItemObject.ItemTypeEnum.Crossbow:
                case ItemObject.ItemTypeEnum.Thrown:
                case ItemObject.ItemTypeEnum.Shield:
                    return GW;
                case ItemObject.ItemTypeEnum.BodyArmor:
                case ItemObject.ItemTypeEnum.HeadArmor:
                case ItemObject.ItemTypeEnum.LegArmor:
                case ItemObject.ItemTypeEnum.HandArmor:
                    return GA;
            }
            return -1;
        }

        private static readonly ItemObject.ItemTypeEnum[] StockTypes =
        {
            ItemObject.ItemTypeEnum.OneHandedWeapon, ItemObject.ItemTypeEnum.TwoHandedWeapon, ItemObject.ItemTypeEnum.Polearm, ItemObject.ItemTypeEnum.Bow,
            ItemObject.ItemTypeEnum.Crossbow, ItemObject.ItemTypeEnum.Thrown, ItemObject.ItemTypeEnum.Shield,
            ItemObject.ItemTypeEnum.BodyArmor, ItemObject.ItemTypeEnum.HeadArmor, ItemObject.ItemTypeEnum.LegArmor, ItemObject.ItemTypeEnum.HandArmor
        };

        /// <summary>Sztuka do zapasu: bron/zbroja z grup, bez unikatow i sztuk zakazanych w bitwie (ArmsPricing.IsUnique - z QuartermasterLaw.BarredInBattle,
        /// pamiec na przedmiot), bez wyrobow gracza (osobne obiekty zapisu).</summary>
        private static bool Eligible(ItemObject it)
        {
            if (GroupOf(it) < 0) return false;
            try { return !it.IsCraftedByPlayer && !ArmsPricing.IsUnique(it); }
            catch (Exception e) { Stumble("Eligible", e); return false; }
        }

        private static int Men(MobileParty mp)
        {
            try { var r = mp != null ? mp.MemberRoster : null; return r != null ? Math.Max(0, r.TotalManCount - r.TotalHeroes) : 0; }
            catch (Exception e) { Stumble("Men", e); return 0; }
        }

        private static int Full(int men) { return men > 0 ? (men + MenPerPiece - 1) / MenPerPiece : 0; }

        private static void GivenCounts(out int w, out int a)
        {
            w = 0; a = 0;
            for (int i = 0; i < _stock.Count; i++)
            {
                var el = _stock.GetElementCopyAtIndex(i);
                if (el.Amount <= 0) continue;
                int g = GroupOf(el.EquipmentElement.Item);
                if (g == GW) w += el.Amount; else if (g == GA) a += el.Amount;
            }
        }

        private static Acc AccOf(MobileParty mp, bool create)
        {
            if (mp == null || mp.StringId == null) return null;
            Acc a;
            if (!_acc.TryGetValue(mp.StringId, out a) && create) { a = new Acc(); _acc[mp.StringId] = a; }
            return a;
        }

        // ------------------------------------------------------------ godziny ruchu (maska 24 bitow: 1 = godzina ruchu)
        /// <summary>Godzina postoju: osada, oboz obleznikow albo ruch najwyzej 0.35 jedn. od poprzedniej godziny (ten sam prog i ta sama granica co
        /// NightRest.OnHourly: tam "ruszyl sie" = krok > 0.35). To czesc wspolna z ksiega snu gracza i T10 R2 - od scalenia grupa11 obie wolaja te funkcje,
        /// a sen dolicza po swojej stronie morze (SleepAtSeaFree - zaloga spi na wachty), sluzbe ROT (o snie decyduje lord) i oboz swiata T10. Dla musztry to nie postoj:
        /// partia na morzu plynie, a w sluzbie ROT idzie z lordem - ruch jest w kroku. Oboz obleznikow stoi, wiec miesci sie tez w kroku.</summary>
        internal static bool RestHour(MobileParty mp, float step)
        {
            return mp == null || mp.CurrentSettlement != null || mp.BesiegerCamp != null || step <= RestStep;
        }

        internal static void Hourly()
        {
            Clk();
            try
            {
                if (Campaign.Current == null || !Active(Settings.Current)) return;
                long now = (long)Math.Floor(CampaignTime.Now.ToHours);
                var main = MobileParty.MainParty;
                bool mainSeen = false;
                var all = MobileParty.AllLordParties;
                if (all != null)
                    for (int i = 0; i < all.Count; i++) { var mp = all[i]; if (mp == main) mainSeen = true; Observe(mp, now); }
                if (!mainSeen && main != null) Observe(main, now);
                if (now % 24 == 0)
                {
                    var dead = new List<MobileParty>();
                    foreach (var kv in _tr) if (kv.Key == null || now - kv.Value.Stamp > 48) dead.Add(kv.Key);
                    foreach (var mp in dead) _tr.Remove(mp);
                }
            }
            catch (Exception e) { Stumble("Hourly", e); }
            finally { Unclk(); }
        }

        private static void Observe(MobileParty mp, long now)
        {
            try
            {
                if (mp == null || !mp.IsActive) return;
                var pos = mp.GetPosition2D;
                Track t;
                if (!_tr.TryGetValue(mp, out t))
                {
                    t = new Track();   // pierwsza obserwacja: cala doba jako ruch (bez darmowego postoju)
                    if (mp == MobileParty.MainParty && _pendingMainMask >= 0) { t.Mask = _pendingMainMask & AllHours; _pendingMainMask = -1; }
                    t.Pos = pos; t.Stamp = now; _tr[mp] = t;
                    return;
                }
                long gap = now - t.Stamp;
                if (gap <= 0) return;
                if (gap > 1) { int k = (int)Math.Min(24, gap - 1); t.Mask = ((t.Mask << k) | ((1 << k) - 1)) & AllHours; }   // przegapione godziny = ruch
                bool rest = RestHour(mp, pos.Distance(t.Pos));
                t.Mask = ((t.Mask << 1) | (rest ? 0 : 1)) & AllHours;
                t.Pos = pos; t.Stamp = now;
            }
            catch (Exception e) { Stumble("Observe", e); }
        }

        internal static int MovedHours(MobileParty mp)
        {
            Track t;
            return mp != null && _tr.TryGetValue(mp, out t) ? Pop(t.Mask) : 24;
        }

        // ------------------------------------------------------------ kontekst partii (raz na tick)
        /// <summary>Partie, ktorym gra nie daje bazy treningu (DefaultPartyTrainingModel.cs:21): armia gracza albo wlasciciel z rodu gracza.</summary>
        internal static bool NoGameBase(MobileParty mp)
        {
            try
            {
                if (mp == null) return false;
                if (mp.Army != null && mp.Army.LeaderParty == MobileParty.MainParty) return true;
                var o = mp.Party != null ? mp.Party.Owner : null;
                return o != null && o.Clan == Clan.PlayerClan;
            }
            catch (Exception e) { Stumble("NoGameBase", e); return false; }
        }

        /// <summary>MUSZTRA-j - B wzoru: 10 + 2 x tier dla kazdego (gracz, rod gracza, lordowie AI i glowy rodow AI - decyzja Jeffa 07:10 pkt 1); bitwe
        /// (B = 0) rozstrzyga Shape.</summary>
        private static float RuleBase(CharacterObject ch) { return 10f + 2f * ch.Tier; }

        /// <summary>Glowa rodu wedlug gry: przywodca partii jest przywodca swojego rodu (DefaultPartyTrainingModel.cs:23, BKPartyTrainningModel.cs:43).</summary>
        private static bool ClanHead(MobileParty mp)
        {
            var h = mp != null ? mp.LeaderHero : null;
            return h != null && mp.ActualClan != null && h == mp.ActualClan.Leader;
        }

        /// <summary>MUSZTRA-j - baza, ktora daje model gry (DefaultPartyTrainingModel.cs:21-29) i BK (BKPartyTrainningModel.cs:41-49), ten sam predykat:
        /// 0 bez bazy gry (Z14a: armia gracza, rod gracza) i w bitwie; glowa rodu 15 + 3 x tier; reszta 10 + 2 x tier. Wzor tej bazy NIE daje - sluzy tylko
        /// do wydzielenia perkow P z wyniku modelu, do pomiaru ("baza gry", "glowy rodow") i do wylaczonego DrillLawAi (plaski trening gry).</summary>
        private static float GameBase(MobileParty mp, CharacterObject ch, bool noBase)
        {
            if (noBase || mp.MapEvent != null) return 0f;
            int t = ch.Tier;
            return ClanHead(mp) ? 15f + 3f * t : 10f + 2f * t;
        }

        /// <summary>MUSZTRA-j - dlug snu partii o OSTATNIM SWICIE (decyzja Jeffa 07:10 pkt 2: "noc bez snu = nastepny dzien bez cwiczen", od switu do switu,
        /// niezaleznie od godziny ticku treningu): NightRest.DawnDebtOf - gracz z jego ksiegi, kazda inna partia lorda (takze doczepiona do armii gracza - idzie
        /// z nim noca, wiec jej ksiega liczy te same nieprzespane noce) z ksiegi snu AI T10. Ta sama ksiega i ten sam dlug, ktory zabiera predkosc i morale
        /// (te licza dlug biezacy - splata zdejmuje kare marszu od reki, a dzien cwiczen jest juz stracony).</summary>
        private static int SleepDebt(MobileParty mp)
        {
            try { return NightRest.DawnDebtOf(mp); }
            catch (Exception e) { Stumble("SleepDebt", e); return 0; }   // blad odczytu = partia bez kary snu - musi byc widac w potknieciach
        }

        /// <summary>Dlug biezacy (NightRest.DebtOf - predkosc i morale) - tylko do linii: "splacone przed treningiem" i kontrola "teraz > o swicie".
        /// MUSZTRA-jp (recenzja 3): partie AI za ta sama bramka co dlug o swicie (NightRest.AiDebtLive, jak DawnDebtOf) - po wylaczeniu dlugu AI w MCM
        /// slownik kar zyje jeszcze do najblizszego ticku ksiegi (do 1 h), a kontrola pokazalaby falszywy BLAD.</summary>
        private static int SleepDebtNow(MobileParty mp)
        {
            try
            {
                if (mp != null && mp != MobileParty.MainParty && !NightRest.AiDebtLive(Settings.Current)) return 0;
                return NightRest.DebtOf(mp);
            }
            catch (Exception e) { Stumble("SleepDebtNow", e); return 0; }
        }

        private static Ctx CtxOf(MobileParty mp, bool noBase, Settings s)
        {
            double now = CampaignTime.Now.ToHours;
            if (_ctx != null && _ctx.Party == mp && Math.Abs(_ctx.At - now) < 1e-6) return _ctx;
            var c = new Ctx { Party = mp, At = now, Main = mp == MobileParty.MainParty, NoBase = noBase };
            try
            {
                c.Men = Men(mp);
                c.Full = Full(c.Men);
                var h = mp.LeaderHero;
                if (h != null) { c.Lead = h.GetSkillValue(DefaultSkills.Leadership); c.L = Clamp(c.Lead / LeadNorm, LeadMin, LeadMax); }
                c.Moved = MovedHours(mp);
                c.Rest = c.Moved < RestBelowHours;
                c.Hungry = mp.Party != null && mp.Party.IsStarving;
                c.Debt = SleepDebt(mp);                    // MUSZTRA-j: dlug o ostatnim swicie - o dniu cwiczen decyduje swit
                c.DebtNow = SleepDebtNow(mp);
                c.Sleepless = c.Debt >= 1;
                c.ClanHead = !c.Main && !noBase && ClanHead(mp);
                c.Zero = c.Hungry || c.Sleepless;
                c.Off = c.Zero && (c.Main || c.NoBase || PenaltyAi(s));   // kara naprawde zastosowana: Z14a zawsze, AI przy DrillPenaltyAi albo Z14b
                c.D = c.Zero ? 0f : (c.Rest ? RestDay : MarchDay);
                c.StockOn = c.Main ? StockOn : s.DrillStockAi;
                bool measure = c.StockOn || (!c.Main && !c.NoBase && s.DrillLog);   // AI przy wylaczonym zapasie: S tylko do linii pomiaru
                if (measure && c.Men > 0)
                {
                    if (c.Main) CountPlayerStock(c); else CountAiStock(c, mp);
                    c.PerkW = mp.HasPerk(DefaultPerks.Steward.GivingHands);
                    c.PerkA = mp.HasPerk(DefaultPerks.Steward.PaidInPromise, true);
                    float uB = Math.Min(1f, c.StockW / (float)c.Full), uZ = Math.Min(1f, c.StockA / (float)c.Full);
                    c.S = 1f + StockBonus * uB * (c.PerkW ? PerkMult : 1f) + StockBonus * uZ * (c.PerkA ? PerkMult : 1f);
                }
                c.ArmsGate = c.Main ? s.DrillNeedsArmsPlayer : (AiGear.On && s.PartyDrillNeedsArms);
            }
            catch (Exception e) { Stumble("CtxOf", e); }
            _ctx = c;
            return c;
        }

        // ------------------------------------------------------------ zapas: liczenie
        private static void CountPlayerStock(Ctx c)
        {
            int w, a; GivenCounts(out w, out a); c.GivenW = w; c.GivenA = a;
            var sur = PlayerSurplus();
            foreach (var kv in sur) { int g = TypeGroup((int)kv.Key); if (g == GW) c.SurW += kv.Value.Count; else if (g == GA) c.SurA += kv.Value.Count; }
        }

        private sealed class Sur { internal int Count; internal readonly List<EquipmentElement> Cand = new List<EquipmentElement>(); }

        /// <summary>Nadwyzka ludzi w zbrojowni DTE gracza wedlug typu. Scalenie sklad8 (warunek scalenia K1): funkcja nadwyzki K1 A9
        /// (MenPurse.PlayerSurplusPlan) - ta sama, co sprzedaz nadwyzek kupcom: po dopasowaniu, ponad komplet + SurplusKeepPercent, bez czesci gracza
        /// (ksiega wkladow na egzemplarze). Kandydaci - egzemplarze z Sell > 0; do zapasu i do liczby tylko sztuki zdatne (Eligible): unikat albo sztuka
        /// zakazana w bitwie nie uzbroi czlowieka. Dotad: sztuki typu minus wklady minus potrzeba (po typie, bez zapasu procentowego).</summary>
        private static Dictionary<ItemObject.ItemTypeEnum, Sur> PlayerSurplus()
        {
            var res = new Dictionary<ItemObject.ItemTypeEnum, Sur>();
            var armory = QuartermasterLaw.DteArmory();
            if (armory == null) return res;
            foreach (var type in StockTypes)
            {
                var pieces = MenPurse.PlayerSurplusPlan(armory, type);
                if (pieces == null) continue;
                Sur sr = null;
                foreach (var p in pieces)
                {
                    if (p.Sell <= 0) continue;
                    var el = QuartermasterLaw.ElOf(p);
                    if (el.Item == null || !Eligible(el.Item)) continue;
                    if (sr == null) sr = new Sur();
                    sr.Count += p.Sell;
                    sr.Cand.Add(el);
                }
                if (sr != null) res[type] = sr;
            }
            return res;
        }

        private sealed class AiSur
        {
            internal readonly Dictionary<int, int> ByType = new Dictionary<int, int>(); internal int Melee = -1;
            internal readonly HashSet<ItemObject> Cand = new HashSet<ItemObject>();   // scalenie sklad8: przedmioty, ktore K1 oddalby kupcowi (Sell > 0)
        }

        /// <summary>Nadwyzka zbrojowni AI wedlug typu (bron biala grupa przy AiAnyMeleeWhenShort). Scalenie sklad8 (warunek scalenia K1): funkcja nadwyzki
        /// K1 A9 (MenPurse.AiSurplusPlan i AiMeleeLeft) - ta sama, co sprzedaz nadwyzek lordow kupcom: po dopasowaniu (bron po slotach wzorca), ponad komplet
        /// + SurplusKeepPercent. Liczone i brane tylko sztuki zdatne do zapasu (Eligible). Dotad: po typie ponad komplet, bez zapasu procentowego.</summary>
        private static AiSur AiSurplus(MobileParty mp, Dictionary<ItemObject, int> arm)
        {
            var res = new AiSur();
            if (mp == null || arm == null || arm.Count == 0) return res;
            var roster = mp.MemberRoster;
            int meleeLeft = MenPurse.AiMeleeLeft(arm, roster);   // int.MaxValue = bez grupy broni bialej
            bool group = meleeLeft != int.MaxValue;
            if (group) res.Melee = 0;
            foreach (var type in StockTypes)
            {
                var pieces = MenPurse.AiSurplusPlan(arm, roster, type);
                if (pieces == null) continue;
                int n = 0;
                foreach (var p in pieces)
                {
                    if (p.Sell <= 0) continue;
                    var it = QuartermasterLaw.ElOf(p).Item;
                    if (it == null || !Eligible(it)) continue;
                    n += p.Sell;
                    res.Cand.Add(it);
                }
                if (n <= 0) continue;
                if (group && AiGear.Melee((int)type)) res.Melee += n;
                else res.ByType[(int)type] = n;
            }
            if (group) res.Melee = Math.Min(res.Melee, Math.Max(0, meleeLeft));
            return res;
        }

        private static Dictionary<ItemObject, int> ArmoryOf(MobileParty mp)
        {
            var d = AiGear.Armories(); Dictionary<ItemObject, int> a;
            return d != null && mp != null && d.TryGetValue(mp.Id, out a) ? a : null;
        }

        /// <summary>Zapas AI = tylko nadwyzka zbrojowni (sprzet ludzi, ktorego nikt nie nosi) - ta sama regula co nadwyzka ludzi gracza. Tabor sie NIE
        /// liczy: to lup lorda do sprzedania i zaopatrzenie BK (PartySupplies kupuje do taboru bron i tarcze MeleeWeapons2/3, Shield2/3 i sam je zuzywa,
        /// BK PartySupplies.cs:118-126, 312-330) - jak sakwy gracza; lord nic z niego nie oddaje, wiec nie ma premii i nie ma podwojnego zuzycia.</summary>
        private static void CountAiStock(Ctx c, MobileParty mp)
        {
            var sur = AiSurplus(mp, ArmoryOf(mp));
            if (sur.Melee > 0) c.SurW += sur.Melee;
            foreach (var kv in sur.ByType)
            {
                int g = TypeGroup(kv.Key);
                if (g == GW) c.SurW += kv.Value; else if (g == GA) c.SurA += kv.Value;
            }
        }

        private static int TypeGroup(int type)
        {
            switch ((ItemObject.ItemTypeEnum)type)
            {
                case ItemObject.ItemTypeEnum.OneHandedWeapon: case ItemObject.ItemTypeEnum.TwoHandedWeapon: case ItemObject.ItemTypeEnum.Polearm:
                case ItemObject.ItemTypeEnum.Bow: case ItemObject.ItemTypeEnum.Crossbow: case ItemObject.ItemTypeEnum.Thrown: case ItemObject.ItemTypeEnum.Shield:
                    return GW;
                case ItemObject.ItemTypeEnum.BodyArmor: case ItemObject.ItemTypeEnum.HeadArmor: case ItemObject.ItemTypeEnum.LegArmor: case ItemObject.ItemTypeEnum.HandArmor:
                    return GA;
            }
            return -1;
        }

        // ------------------------------------------------------------ wynik modelu (ArmsDrill.TrainingPostfix)
        /// <summary>
        /// Nowy wynik przed udzialem uzbrojonych: 0 - nie dotyczy (171 jak dotad), 1 - AI (wedlug jednego wzoru albo plaskiej bazy gry przy wylaczonym
        /// DrillLawAi; 171 Gated dalej), 2 - Z14a (udzial wedlug ElemArmsGate). Wolane z zewnetrznego modelu (ArmsDrill pilnuje _tDepth).
        /// MUSZTRA-j: jedna galaz dla wszystkich - XP = B x L x D x S + P (albo 0 w dniu kary). Ktory model (gra czy BK) i z jaka baza policzyl trening,
        /// nie ma znaczenia: z jego wyniku bierzemy tylko perki P = wynik - baza gry (Z14a: baza gry 0, P = caly wynik - jak dotad).
        /// </summary>
        internal static int Shape(MobileParty mp, TroopRosterElement el, ref ExplainedNumber res)
        {
            _e = false;
            Clk();
            try
            {
                var s = Settings.Current;
                var ch = el.Character;
                if (s == null || mp == null || ch == null || ch.IsHero || ch.Culture == null || el.Number <= 0 || !mp.IsLordParty) return 0;
                bool noBase = NoGameBase(mp);
                if (noBase) { if (!s.DrillLaw) return 0; }
                else if (!s.DrillStockAi && !s.DrillLawAi && !s.DrillPenaltyAi && !s.DrillLog) return 0;
                // Inni (umarli) - swiadomy wyjatek od jednego wzoru: nie spia, nie znaja zmeczenia (Undead.cs, Jeff: "Inni moga nie spac i nie maja staminy"),
                // wiec zostaja przy treningu gry (wynik modelu bez zmian, 171 ich nie bramkuje)
                if (Undead.Party(mp)) return 0;
                bool law = noBase || s.DrillLawAi;                     // Z14a zawsze (DrillLaw sprawdzone wyzej), AI przy Z14b (domyslnie TAK)
                // MUSZTRA-jp (recenzja 4a): jedna regula dla partii nieaktywnej (poza mapa: niewola, rejs BK, sluzba ROT) przy wzorze - Z14a i AI przy Z14b:
                // nie cwiczy wcale (XP 0). Gra i tak daje perki tylko partii aktywnej (DefaultPartyTrainingModel.cs:31-87), a D bez obserwacji nie istnieje
                // (Observe pomija nieaktywne - maska zamrozona). Dawniej tylko Z14a; AI dostawalo caly wzor z zamrozonej maski. Wylaczony Z14b - jak dotad.
                if (law && !mp.IsActive) { if (res.ResultNumber != 0f) res = new ExplainedNumber(0f); return 0; }
                var c = CtxOf(mp, noBase, s);
                float game = res.ResultNumber;
                float gb = GameBase(mp, ch, noBase);                   // baza gry (0 / 15 + 3 x tier / 10 + 2 x tier) - tylko do wydzielenia P i pomiaru
                // model nie dal bazy (wyjatek BK w TryCatch): perki 0 (nic z niczego - nie zgadujemy perkow z niepelnego wyniku), baza ze wzoru (przy law)
                // albo to, co model dal (wylaczony Z14b); licznik "model bez bazy" w linii AI - oczekiwane 0
                bool noModel = game < gb - 0.01f;
                float P = noModel ? 0f : game - gb;                    // perki gry i BK
                float gbGiven = noModel ? Math.Max(0f, game) : gb;     // baza gry naprawde dana (pomiar; plaska regula przy wylaczonym DrillLawAi)
                float rb = mp.MapEvent == null ? RuleBase(ch) : 0f;    // B wzoru; w bitwie gra nie daje bazy - wzor tez nie
                float B = law ? rb : gbGiven;
                float sk = c.StockOn ? c.S : 1f;                       // S tylko przy czynnym zapasie (gracz StockOn, AI DrillStockAi)
                // kara (glod, dlug o swicie): Z14a zawsze, AI przy DrillPenaltyAi albo Z14b - niezalezna od pomiaru L x D (c.Off)
                float pre = c.Off ? 0f : B * (law ? c.L * c.D : 1f) * sk + P;
                if (pre < 0f) pre = 0f;
                if (Math.Abs(pre - game) > 0.0001f) res = new ExplainedNumber(pre);   // opisy gubimy swiadomie (jak 171) - treningu nikt nie oglada
                _e = true; _eC = c; _eB = B; _eGB = gbGiven; _eRB = rb; _eNoModel = noModel; _eP = P; _ePre = pre; _eGame = game; _eN = el.Number; _eCh = ch;
                return noBase ? 2 : 1;
            }
            catch (Exception e) { Stumble("Shape", e); _e = false; return 0; }
            finally { Unclk(); }
        }

        /// <summary>Po udziale uzbrojonych: liczniki doby (tylko w ticku treningu partii).</summary>
        internal static void Done(MobileParty mp, float final, float share)
        {
            if (!_e) return;
            _e = false;
            Clk();
            try
            {
                var c = _eC;
                if (c == null || c.Party != mp || (_tickHooked && _tick != mp)) return;
                int n = _eN;
                if (!c.Counted) { c.Counted = true; CountParty(c); }
                if (c.Main)
                {
                    var r = _pr;
                    if (r == null || r.C != c) return;
                    r.B += _eB * n; r.P += _eP * n; r.Pre += _ePre * n; r.Fin += final * n; r.ShN += share * n; r.N += n;
                    int add = TaleWorlds.Library.MathF.Round(final * n);   // ta sama liczba, ktora gra doda do rosteru (MobilePartyTrainingBehavior.cs:49)
                    r.Computed += add;
                    int room;
                    // gra: Xp = min(Xp + dodane, limit) - przyjete = min(dodane, limit - Xp); ponad limitem juz przed dodaniem (np. po stratach) wychodzi ujemne
                    if (_eCh != null && _room.TryGetValue(_eCh, out room)) { int acc = Math.Min(room, add); r.Cut += add - acc; _room[_eCh] = room - acc; }
                }
                else if (c.NoBase) _cXp += final * n;
                else
                {
                    _aGame += _eGB * n;                                // baza gry naprawde dana (jak przed MUSZTRA-j)
                    if (c.Off) _aPenalty += _eGame * n;               // XP gry (baza + perki, przed udzialem broni) zabrane kara glodu albo snu
                    else _aRule += (_ePre - _eP) * n;                  // czesc bazowa po czynnej regule (dni bez kary)
                    // MUSZTRA-j: jeden wzor wobec gry - baza wzoru, glowy rodow, perki, XP po broni gra (bez kary, jak dzis w grze) i teraz, ludzio-dni
                    double xg = _eGame * share * n;
                    _aRuleBase += _eRB * n; _aPerks += _eP * n; _aXpGame += xg; _aXpNow += final * n; _aManN += n;
                    if (c.ClanHead) _aHeadBase += _eGB * n;
                    if (_eNoModel) _aNoModel++;
                    var k = KOf(mp);
                    if (k != null) { k.XpG += xg; k.XpN += final * n; k.ManN += n; k.RB += _eRB * n; if (c.ClanHead) k.HB += _eGB * n; }
                    if (_eGB > 0f)
                    {
                        double w = _eGB * n;
                        _aW += w; _aWL += w * c.L; _aWS += w * c.S;
                        float d = c.Zero ? 0f : (c.Rest ? RestDay : MarchDay);
                        _aWD += w * d; _aWLD += w * c.L * d; _aWLDS += w * c.L * d * c.S;
                        for (int i = 0; i < Thresholds.Length; i++) { float dt = c.Zero ? 0f : (c.Moved < Thresholds[i] ? RestDay : MarchDay); _aWT[i] += w * c.L * dt * c.S; }
                        if (c.Hungry) _aWHungry += w; else if (c.Sleepless) _aWSleep += w; else if (c.Rest) _aWRest += w; else _aWMarch += w;
                        if (k != null) { k.W += w; k.WLD += w * c.L * d; k.WS += w * c.S; }
                        if (!c.Zero)
                        {
                            // prog Z14b: dowodca x dzien BEZ zapasu, tylko dni bez kary (kara dziala w obu wariantach - z Z14b i bez - wiec sie skraca)
                            _aWn += w; _aWLDn += w * c.L * d;
                            if (k != null) k.Wn += w;
                            for (int i = 0; i < Thresholds.Length; i++)
                            {
                                double v = w * c.L * (c.Moved < Thresholds[i] ? RestDay : MarchDay);
                                _aWTn[i] += v;
                                if (k != null) k.WTn[i] += v;
                            }
                        }
                    }
                }
            }
            catch (Exception e) { Stumble("Done", e); }
            finally { Unclk(); }
        }

        private static int Bucket(int moved) { return moved <= 0 ? 0 : (moved < 4 ? 1 : (moved < 12 ? 2 : 3)); }

        private static KAcc KOf(MobileParty mp)
        {
            try
            {
                var kd = mp.MapFaction as Kingdom;
                string id = kd != null ? kd.StringId : "-";
                KAcc k;
                if (!_k.TryGetValue(id, out k)) { k = new KAcc { Name = kd != null ? kd.Name.ToString() : "bez krolestwa" }; _k[id] = k; }
                return k;
            }
            catch (Exception e) { Stumble("KOf", e); return null; }
        }

        private static void CountParty(Ctx c)
        {
            var mp = c.Party;
            if (c.Main) return;
            if (c.DebtNow > c.Debt) _sleepNowAbove++;   // MUSZTRA-j kontrola: dlug rosnie tylko o swicie (SettleNight, SettleAi) - ma byc 0
            if (c.NoBase) { _cParties++; _cMen += c.Men; _cL += c.L; if (c.Rest && !c.Zero) _cRest++; if (c.Off) _cOff++; return; }
            _aParties++; _aMen += c.Men; if (c.Off) _aOff++;
            if (c.Sleepless) { _aSleepDawn++; if (c.DebtNow == 0) _aSleepPaid++; }   // sen od switu; splacone przed treningiem - przed MUSZTRA-j moglyby cwiczyc
            if (c.Lead >= 0) _leads.Add(c.Lead); else _aNoLead++;
            int b = Bucket(c.Moved); _hb[b]++;
            if (c.Full > 0) { if (c.StockW >= c.Full && c.StockA >= c.Full) _aFullStock++; else if (c.StockW + c.StockA == 0) _aNoStock++; }
            var k = KOf(mp);
            if (k != null) { k.PartyDays++; k.Hb[b]++; }
        }

        // ------------------------------------------------------------ tick treningu partii (latka gry)
        public static void TickPrefix(MobileParty mobileParty)
        {
            _tick = mobileParty;
            if (mobileParty == null || mobileParty != MobileParty.MainParty) return;
            Clk();
            try
            {
                _pr = null;
                var s = Settings.Current;
                if (s == null || !s.DrillLaw || !mobileParty.IsLordParty || Undead.Party(mobileParty)) return;   // bez musztry gracza - zadnej petli po rosterze
                // krytyka 6: limit XP oddzialu (PartyBase.OnXpChanged: Number x najwyzszy koszt awansu; t6 - 0) liczony PRZED dodaniem
                _room.Clear(); _xpBefore = 0;
                var r = mobileParty.MemberRoster;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    if (el.Character == null || el.Character.IsHero) continue;
                    _xpBefore += el.Xp;
                    int max = 0;
                    var ups = el.Character.UpgradeTargets;
                    if (ups != null) for (int u = 0; u < ups.Length; u++) { int cost = el.Character.GetUpgradeXpCost(mobileParty.Party, u); if (cost > max) max = cost; }
                    _room[el.Character] = el.Number * max - el.Xp;   // moze byc ujemne (Xp ponad limit sprzed treningu - gra przytnie przy dodaniu)
                }
                _pr = new PlayerRec { Day = (int)CampaignTime.Now.ToDays };
                _ctx = null;
                _pr.C = CtxOf(mobileParty, NoGameBase(mobileParty), s);
            }
            catch (Exception e) { Stumble("TickPrefix", e); }
            finally { Unclk(); }
        }

        public static void TickPostfix(MobileParty mobileParty)
        {
            Clk();
            try
            {
                var s = Settings.Current;
                if (mobileParty == null || s == null) return;
                var c = _ctx;
                bool mine = c != null && c.Party == mobileParty && Math.Abs(c.At - CampaignTime.Now.ToHours) < 1e-6;
                if (mobileParty == MobileParty.MainParty && _pr != null)
                {
                    long after = 0;
                    var r = mobileParty.MemberRoster;
                    for (int i = 0; i < r.Count; i++) { var el = r.GetElementCopyAtIndex(i); if (el.Character != null && !el.Character.IsHero) after += el.Xp; }
                    _pr.Accepted = after - _xpBefore;
                    var pc = _pr.C;
                    if (pc != null && pc.Counted)
                    {
                        _prLast = _pr;   // trening gracza wedlug musztry odbyl sie
                        // wiadomosc w grze w dzien bez cwiczen. MUSZTRA-jp (recenzja 8, 17a): dzien stracony przez sen oglasza juz swit (NightRest.SettleNightCore -
                        // "No drill today ...", splata w ciagu dnia - "Drill resumes at the next dawn"), wiec tu tylko glod; glod i sen naraz - oba powody
                        if (pc.Off && pc.Men > 0 && pc.Hungry)
                            Log.Player(pc.Sleepless ? "Your men were too hungry to drill today, and they met the dawn short of sleep besides - nobody learned anything, training perks included."
                                                    : "Your men were too hungry to drill today - nobody learned anything, training perks included.", true);
                        else if (!pc.Off && pc.Men > 0 && s.DrillDailyMessage) DayMessage(_pr, pc);   // MUSZTRA-m: dzien cwiczen - wynik i czynniki wzoru
                    }
                    _pr = null;
                }
                if (!Active(s) && _acc.Count == 0) return;           // musztra wylaczona i nic nie czeka - bez zuzycia i bez kowali
                if (mine && c.Counted) Wear(c, s);
                var st = mobileParty.CurrentSettlement;
                if (st != null && st.IsTown) SellScrap(mobileParty, st);
            }
            catch (Exception e) { Stumble("TickPostfix", e); }
            finally { Unclk(); }
        }

        /// <summary>MUSZTRA-m (decyzja Jeffa 09.10 08:20): codzienna linia w grze o musztrze druzyny gracza - wynik dnia i czynniki wzoru z tych samych
        /// pomiarow co linia "Musztra (gracz)" w logu. Tylko w dniu cwiczen (dzien stracony oglaszaja juz istniejace zdania: glod - TickPostfix, sen - swit
        /// NightRest), raz na dobe (_msgDay), przy DrillDailyMessage. XP = przyjete przez roster (to, co ludzie naprawde dostali); uciete limitem awansu
        /// - osobno, gdy > 0. Perki P i udzial uzbrojonych A - srednie na czlowieka (wazone liczba ludzi w oddzialach).</summary>
        private static void DayMessage(PlayerRec r, Ctx c)
        {
            try
            {
                if (r == null || c == null || r.Day == _msgDay) return;
                _msgDay = r.Day;
                double men = r.N > 0 ? r.N : c.Men;
                long xp = Math.Max(0L, r.Accepted);
                double sk = c.StockOn ? c.S : 1.0;
                double armed = r.N > 0 ? r.ShN / r.N : 1.0;
                double perks = men > 0 ? r.P / men : 0;
                var sb = new StringBuilder("Drill today: ").Append(xp).Append(" XP (").Append(F1(men > 0 ? xp / men : 0)).Append(" per man) - Leadership x").Append(F2(c.L))
                    .Append(c.Rest ? ", at rest x" : ", marching x").Append(F2(c.D)).Append(", drill kit x").Append(F2(sk))
                    .Append(", armed ").Append(Pct(armed, 1.0)).Append(", perks +").Append(F1(perks)).Append(" per man");
                if (r.Cut > 0) sb.Append("; ").Append(r.Cut).Append(" XP lost - men are waiting to be upgraded");
                Log.Player(sb.Append('.').ToString());
            }
            catch (Exception e) { Stumble("DayMessage", e); }
        }

        public static Exception TickFinalizer(Exception __exception) { _tick = null; _room.Clear(); return __exception; }

        // ------------------------------------------------------------ zuzycie i zlom
        private static void Wear(Ctx c, Settings s)
        {
            if (!c.StockOn || c.Men <= 0 || c.Full <= 0 || c.Off) return;   // dzien kary (glod, sen) - nikt nie cwiczy, nic sie nie zuzywa
            var mp = c.Party;
            bool law = c.NoBase || s.DrillLawAi;
            float dw = law ? c.D : 1f;                     // AI bez Z14b cwiczy plasko jak w grze; od MUSZTRA-j Z14b domyslnie TAK - zuzycie AI x D (1.5 / 0.9)
            if (dw <= 0f) return;
            var a = AccOf(mp, true);
            if (a == null) return;
            int inW = Math.Min(c.StockW, c.Full), inA = Math.Min(c.StockA, c.Full);
            a.WW += inW * dw / WearDays; a.WA += inA * dw / WearDays;
            int nW = (int)a.WW, nA = (int)a.WA;
            a.WW -= nW; a.WA -= nA;
            if (nW + nA <= 0) return;
            var gf = GoodsLedger.Begin(GoodsLedger.FDrill, mp);
            try
            {
                for (int g = 0; g < 2; g++)
                {
                    int n = g == GW ? nW : nA;
                    for (int k = 0; k < n; k++)
                    {
                        if (!(c.Main ? WearPlayerOne(g, a) : WearAiOne(mp, g, a))) break;
                        if (c.Main) { if (g == GW) _pWornW++; else _pWornA++; }
                        else { if (g == GW) _aWornW++; else _aWornA++; }
                    }
                }
            }
            catch (Exception e) { Stumble("Wear", e); }
            finally { GoodsLedger.End(gf); }
        }

        private static float Yield() { var s = Settings.Current; return s != null ? MBMath.ClampFloat(s.OldStockScrapYield, 0f, 1f) : 0.5f; }

        /// <summary>Metal zuzytej sztuki -> zlom czekajacy na kowali (ruda = ruda sztuki x OldStockScrapYield, jak zlom 174).</summary>
        private static float Scrap(ItemObject it, Acc a, bool player)
        {
            float ore = 0f;
            try { float d; var need = WorkshopLaw.Needs(it, out d); if (need != null && need.Length > 0 && need[0] > 0f) ore = need[0] * Yield(); }
            catch (Exception e) { Stumble("Scrap", e); ore = 0f; }   // wyjatek to nie "bez metalu" - musi byc w potknieciach
            if (ore <= 0f) { if (player) _pNoMetal++; else _aNoMetal++; return 0f; }
            a.Ore += ore;
            if (player) _pOreAdd += ore; else _aOreAdd += ore;
            return ore;
        }

        /// <summary>Gracz: najgorsza sztuka grupy - najpierw zapas od gracza, potem nadwyzka ludzi w zbrojowni DTE (nigdy ponizej kompletu, nigdy wklady gracza).</summary>
        private static bool WearPlayerOne(int g, Acc a)
        {
            int best = -1; int bv = int.MaxValue;
            for (int i = 0; i < _stock.Count; i++)
            {
                var el = _stock.GetElementCopyAtIndex(i);
                if (el.Amount <= 0 || GroupOf(el.EquipmentElement.Item) != g) continue;
                int v = el.EquipmentElement.ItemValue;
                if (v < bv) { bv = v; best = i; }
            }
            if (best >= 0)
            {
                var el = _stock.GetElementCopyAtIndex(best);
                _stock.AddToCounts(el.EquipmentElement, -1);
                _pWornGiven++;
                Scrap(el.EquipmentElement.Item, a, true);
                return true;
            }
            var armory = QuartermasterLaw.DteArmory();
            if (armory == null) return false;
            EquipmentElement pick = default(EquipmentElement); bool found = false; bv = int.MaxValue;
            foreach (var kv in PlayerSurplus())
            {
                if (kv.Value.Count <= 0 || TypeGroup((int)kv.Key) != g) continue;
                foreach (var el in kv.Value.Cand)   // scalenie sklad8: egzemplarze, ktore K1 oddalby kupcowi (bez czesci gracza)
                {
                    int v = el.ItemValue;
                    if (v < bv) { bv = v; pick = el; found = true; }
                }
            }
            if (!found) return false;
            armory.AddToCounts(pick, -1);
            Scrap(pick.Item, a, true);
            return true;
        }

        /// <summary>AI: najgorsza sztuka grupy z nadwyzki zbrojowni (sztuki ludzi; stan z AiWear). Tabor lorda nietkniety (nie jest zapasem - CountAiStock).</summary>
        private static bool WearAiOne(MobileParty mp, int g, Acc a)
        {
            var arm = ArmoryOf(mp);
            if (arm == null || arm.Count == 0) return false;
            var sur = AiSurplus(mp, arm);
            ItemObject pick = null; int pv = int.MaxValue;
            foreach (var kv in arm)
            {
                var it = kv.Key;
                if (it == null || kv.Value <= 0 || GroupOf(it) != g || !Eligible(it) || !sur.Cand.Contains(it)) continue;   // scalenie sklad8: tylko to, co K1 oddalby kupcowi
                int ty = (int)it.ItemType;
                bool ok;
                if (sur.Melee >= 0 && AiGear.Melee(ty)) ok = sur.Melee > 0;
                else { int ex; ok = sur.ByType.TryGetValue(ty, out ex) && ex > 0; }
                if (!ok) continue;
                if (it.Value < pv) { pv = it.Value; pick = it; }
            }
            if (pick == null) return false;
            // stan sztuki PRZED zmiana zbrojowni (jak MenPurse.SellArmorySurplus): Sync w TakeCondition widzi pelny stan, zdejmuje jeden zapis obitej
            // sztuki i Known - 1; potem zbrojownia - 1, wiec zapis stanu i Known zostaja zgodne ze zbrojownia (bez darmowej naprawy i fantomu po bitwie)
            try { AiWear.TakeCondition(mp, pick); } catch (Exception e) { Stumble("TakeCondition", e); }
            int cnt = arm[pick] - 1;
            if (cnt > 0) arm[pick] = cnt; else arm.Remove(pick);
            Scrap(pick, a, false);
            return true;
        }

        private static ItemObject _ore;
        private static ItemObject Ore()
        {
            if (_ore == null) { try { _ore = MBObjectManager.Instance.GetObject<ItemObject>("iron"); } catch { } }
            return _ore;
        }

        internal static void OnEntered(MobileParty mp, Settlement st, Hero h)
        {
            Clk();
            try { if (mp != null && st != null && st.IsTown) SellScrap(mp, st); }
            catch (Exception e) { Stumble("OnEntered", e); }
            finally { Unclk(); }
        }

        /// <summary>Kowale miasta odkupuja zlom z cwiczen: cale ladunki rudy na polke (ramka FDrill, ksiega rudy), kasa miasta placi po cenie skupu,
        /// najwyzej tyle, ile ma. Zlom to zawsze sztuki ludzi (zapas od gracza i nadwyzka zbrojowni; tabor lorda nie jest zapasem): gracz - sakiewka
        /// ludzi; AI - trzecia lordowi, reszta sakiewce (ta sama regula co MenPurse.SellArmorySurplus, takze w partiach rodu gracza).</summary>
        private static void SellScrap(MobileParty mp, Settlement st)
        {
            var a = AccOf(mp, false);
            if (a == null || st == null || st.Town == null || st.ItemRoster == null) return;
            bool player = mp == MobileParty.MainParty;
            int menU = (int)a.Ore;
            if (menU <= 0) return;
            var ore = Ore();
            if (ore == null) return;
            Hero lord = player ? Hero.MainHero : mp.LeaderHero;
            if (lord == null || !lord.IsAlive) return;
            var s = Settings.Current;
            int unit = Math.Max(1, MenPurse.SellPrice(new EquipmentElement(ore), st, mp));
            int can = Math.Max(0, st.Town.Gold) / unit;
            int n = Math.Min(menU, can);
            if (n <= 0) return;
            var gf = GoodsLedger.Begin(GoodsLedger.FDrill, st.Town);
            try { st.ItemRoster.AddToCounts(ore, n); }
            finally { GoodsLedger.End(gf); }
            OreLedger.NoteDrillScrap(ore, n);
            int pay = unit * n;
            st.Town.ChangeGold(-pay);
            MoneyLedger.Note169(MoneyLedger.N169Surplus, st, -pay);   // paczka 169: linia kas (tylko licznik)
            if (player)
            {
                if (MenPurse.On) MenPurse.Add(mp, pay); else Hero.MainHero.ChangeHeroGold(pay);
                _pSoldU += n; _pSoldGold += pay;
                Log.Player("The smiths of " + st.Name + " bought " + n + " loads of scrap iron from your men's worn drill kit for " + pay + " denars - "
                           + (MenPurse.On ? "the coin went to the men's purse." : "the coin is yours."));
            }
            else
            {
                int third = MenPurse.On ? (int)Math.Round(pay * MBMath.ClampFloat(s.LordLootThirdPercent, 0f, 100f) / 100f) : pay;
                if (third > 0)
                {
                    lord.ChangeHeroGold(third);
                    ClanIncomeBook.NoteInflow(lord, third, ClanIncomeBook.KThird);   // paczka 169: D rodu (tylko licznik) - cala kwota, ktora dostal lord
                }
                if (pay - third > 0) MenPurse.Add(mp, pay - third);
                _aSoldU += n; _aSoldGold += pay; _aSoldLord += Math.Max(0, third); _aSoldPurse += Math.Max(0, pay - third);
            }
            a.Ore -= n;
        }

        internal static void OnPartyDestroyed(MobileParty mp, PartyBase destroyer)
        {
            try
            {
                if (mp == null) return;
                _tr.Remove(mp);
                if (mp == MobileParty.MainParty || mp.StringId == null) return;
                Acc a;
                if (_acc.TryGetValue(mp.StringId, out a)) { _oreLost += a.Ore; _acc.Remove(mp.StringId); }
            }
            catch (Exception e) { Stumble("OnPartyDestroyed", e); }
        }

        // ------------------------------------------------------------ przyjecie do zapasu (ekrany 1-3)
        /// <summary>Przyjecie do zapasu gracza: bron i zbroje z listy, najtansze najpierw, do 2 x pelny na grupe; przyjete schodza z listy.</summary>
        internal static int Accept(ItemRoster from, int src)
        {
            Clk();
            try
            {
                var main = MobileParty.MainParty;
                if (!StockOn || from == null || from.Count == 0 || main == null) return 0;   // bez musztry albo bez Z1 zapas nic nie przyjmuje (rzeczy jak dotad)
                var cand = new List<ItemRosterElement>();
                for (int i = 0; i < from.Count; i++)
                {
                    var el = from.GetElementCopyAtIndex(i);
                    if (el.Amount > 0 && Eligible(el.EquipmentElement.Item)) cand.Add(el);
                }
                if (cand.Count == 0) return 0;
                int men = Men(main), full = Full(men), cap = IntakeSets * full;
                int w, a; GivenCounts(out w, out a);
                var cur = new[] { w, a };
                cand.Sort((x, y) => x.EquipmentElement.ItemValue.CompareTo(y.EquipmentElement.ItemValue));
                int taken = 0, noRoom = 0;
                foreach (var el in cand)
                {
                    int g = GroupOf(el.EquipmentElement.Item);
                    int t = Math.Min(Math.Max(0, cap - cur[g]), el.Amount);
                    if (t > 0) { from.AddToCounts(el.EquipmentElement, -t); _stock.AddToCounts(el.EquipmentElement, t); cur[g] += t; taken += t; }
                    noRoom += el.Amount - t;
                }
                if (src >= 0 && src < _in.Length) _in[src] += taken;
                _noRoom += noRoom;
                string rest = src == SrcTrophies ? "stay on the field" : (src == SrcAutotest ? "go back to the baggage" : "are lost");
                // stan laczny (od Ciebie + zapasowa bron ludzi w zbrojowni) wobec pelnego, osobno wolne miejsce na to, co dajesz
                int surW = 0, surA = 0;
                if (men > 0)
                    foreach (var kv in PlayerSurplus()) { int g = TypeGroup((int)kv.Key); if (g == GW) surW += kv.Value.Count; else if (g == GA) surA += kv.Value.Count; }
                int roomW = Math.Max(0, cap - cur[GW]), roomA = Math.Max(0, cap - cur[GA]);
                if (men <= 0)
                    Log.Player("Your men took nothing into their drill stock - you have no soldiers to drill. " + noRoom + " pieces " + rest + ".", true);
                else
                    Log.Player("Your men took " + taken + " pieces into their drill stock. They now drill with " + (cur[GW] + surW) + " weapons and " + (cur[GA] + surA)
                               + " pieces of armour, their spare kit in the armoury included (a full set is " + full + " of each - one per three men). They will take "
                               + roomW + " more weapons and " + roomA + " more pieces of armour from you." + (noRoom > 0 ? " " + noRoom + " pieces " + rest + " - no room." : ""),
                               noRoom > 0 && taken == 0);
                Log.Info("Musztra: zapas - przyjeto " + taken + " szt. (" + SrcName[Math.Max(0, Math.Min(SrcName.Length - 1, src))] + "), bez miejsca " + noRoom
                         + "; zapas od gracza: bron " + cur[GW] + ", zbroje " + cur[GA] + "; z nadwyzka ludzi: bron " + (cur[GW] + surW) + ", zbroje " + (cur[GA] + surA)
                         + " (pelny " + full + ", limit od gracza " + cap + " na grupe, wolne " + roomW + "/" + roomA + ").");
                return taken;
            }
            catch (Exception e) { Stumble("Accept", e); return 0; }
            finally { Unclk(); }
        }

        /// <summary>Spoils "Leave" (SpoilsSeal): trofea zostawione na polu - bron i zbroje do zapasu.</summary>
        internal static void AcceptTrophies(ItemRoster r) { Accept(r, SrcTrophies); }

        /// <summary>Podpowiedz Spoils "Leave" przy czynnym zapasie (LeaveOn): ile sztuk ludzie jeszcze przyjma; reszta zostaje na polu bez XP (Z1).</summary>
        internal static string TrophyTip()
        {
            int room = 0;
            try
            {
                int full = Full(Men(MobileParty.MainParty)), w, a; GivenCounts(out w, out a);
                room = Math.Max(0, IntakeSets * full - w) + Math.Max(0, IntakeSets * full - a);
            }
            catch (Exception e) { Stumble("TrophyTip", e); }
            return "Remaining {COUNT} items: your men keep up to " + room + " pieces of arms and armour for their drill stock, the rest stay on the field.";
        }

        // ekran 1 (zwykly ekwipunek "Discard") i 2 (lup gry): prefiks + finalizer na metodzie otwierajacej, postfiks na InventoryLogic.Initialize
        public static void OpenDiscardPrefix() { _opening = 1 + SrcDiscard; }
        public static void OpenLootPrefix() { _opening = 1 + SrcLoot; }
        public static Exception OpenFinalizer(Exception __exception) { _opening = 0; return __exception; }
        public static void InitPostfix(InventoryLogic __instance) { if (_opening > 0) { _screen = __instance; _screenKind = _opening - 1; } }

        /// <summary>Zdarzenie gry OnItemsDiscardedByPlayer (InventoryLogic.DoneLogic): przyjecie tylko z ekranu z bialej listy - lewa lista tego ekranu.</summary>
        internal static void OnDiscarded(ItemRoster roster)
        {
            Clk();
            try
            {
                var logic = _screen; int kind = _screenKind;
                _screen = null;
                if (logic == null || roster == null) return;
                var left = logic.GetElementsInRoster(InventoryLogic.InventorySide.OtherInventory) as ItemRoster;
                if (!ReferenceEquals(left, roster)) return;
                if (kind == SrcDiscard && Game.Current != null && Game.Current.CheatMode) return;   // tryb oszustw: lewa strona = 10 sztuk wszystkiego
                Accept(roster, kind);
            }
            catch (Exception e) { Stumble("OnDiscarded", e); }
            finally { Unclk(); }
        }

        // ------------------------------------------------------------ wpiecie
        internal static void ApplyAll(Harmony h)
        {
            if (h == null) return;
            try
            {
                var m = AccessTools.Method(typeof(MobilePartyTrainingBehavior), "OnDailyTickParty", new[] { typeof(MobileParty) });
                if (m != null)
                {
                    h.Patch(m, prefix: new HarmonyMethod(typeof(Drill), nameof(TickPrefix)), postfix: new HarmonyMethod(typeof(Drill), nameof(TickPostfix)),
                            finalizer: new HarmonyMethod(typeof(Drill), nameof(TickFinalizer)));
                    _tickHooked = true;
                }
            }
            catch (Exception e) { Log.Error("Drill.ApplyAll(tick)", e); }
            try
            {
                MethodInfo init = null;
                foreach (var mi in typeof(InventoryLogic).GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (mi.Name != "Initialize") continue;
                    var ps = mi.GetParameters();
                    if (ps.Length > 3 && ps[1].ParameterType == typeof(ItemRoster) && ps[3].ParameterType == typeof(bool) && ps[3].Name == "isTrading") { init = mi; break; }
                }
                if (init != null) { h.Patch(init, postfix: new HarmonyMethod(typeof(Drill), nameof(InitPostfix))); _hookInit = true; }
                var fin = new HarmonyMethod(typeof(Drill), nameof(OpenFinalizer));
                var od = AccessTools.Method(typeof(Helpers.InventoryScreenHelper), "OpenInventoryPresentation");
                if (od != null && _hookInit) { h.Patch(od, prefix: new HarmonyMethod(typeof(Drill), nameof(OpenDiscardPrefix)), finalizer: fin); _hookDiscard = true; }
                var ol = AccessTools.Method(typeof(Helpers.InventoryScreenHelper), "OpenScreenAsLoot", new[] { typeof(Dictionary<PartyBase, ItemRoster>) });
                if (ol != null && _hookInit) { h.Patch(ol, prefix: new HarmonyMethod(typeof(Drill), nameof(OpenLootPrefix)), finalizer: fin); _hookLoot = true; }
            }
            catch (Exception e) { Log.Error("Drill.ApplyAll(ekrany)", e); }
            Log.Info("Drill: musztra - trening gry (tick partii) " + (_tickHooked ? "wpiety" : "NIE WPIETY (bez zuzycia zapasu i kontroli gracza)") + ", ekrany zapasu "
                     + ((_hookDiscard ? 1 : 0) + (_hookLoot ? 1 : 0)) + "/2 (zwykly ekwipunek " + (_hookDiscard ? "tak" : "NIE") + ", lup gry " + (_hookLoot ? "tak" : "NIE") + ").");
        }

        // ------------------------------------------------------------ start sesji: zapis, opisy perkow, linia startowa
        internal static void SessionStart()
        {
            var s = Settings.Current;
            if (s == null) return;
            string imp = ResolveImport();
            try { if (StockOn) PerkTexts(); } catch (Exception e) { Stumble("PerkTexts", e); }
            try
            {
                // maska gracza z zapisu od razu (pierwszy trening po wczytaniu moze przyjsc przed pierwsza pelna godzina)
                var main = MobileParty.MainParty;
                if (main != null && _pendingMainMask >= 0 && !_tr.ContainsKey(main))
                {
                    _tr[main] = new Track { Pos = main.GetPosition2D, Stamp = (long)Math.Floor(CampaignTime.Now.ToHours), Mask = _pendingMainMask & AllHours };
                    _pendingMainMask = -1;
                }
            }
            catch (Exception e) { Stumble("SessionStart(maska)", e); }
            string masks = RestoreMasks();
            int w, a; GivenCounts(out w, out a);
            string spoils = SpoilsSeal.Present ? (SpoilsSeal.DrillLeaveWired ? "Spoils 1/1 (trofea przy Leave)" : "Spoils 0/1 - NIE WPIETE (trofea jak dotad)") : "Spoils nieobecny";
            Log.Info("Musztra: start - ekrany zapasu wpiete " + ((_hookDiscard ? 1 : 0) + (_hookLoot ? 1 : 0)) + "/2 + " + spoils + "; trening gry " + (_tickHooked ? "wpiety" : "NIE WPIETY")
                     + "; Z14a (Drill Law) " + On(s.DrillLaw) + ", bron gracza " + On(s.DrillNeedsArmsPlayer) + ", zapas gracza " + On(s.DrillStock)
                     + (s.DrillStock && !StockOn ? " (NIECZYNNY - wymaga Drill Law i Donation Xp Off)" : "") + ", zapas AI " + On(s.DrillStockAi)
                     + ", kara AI glod/sen (Drill Penalty Ai) " + On(s.DrillPenaltyAi) + (PenaltyAi(s) ? (s.DrillPenaltyAi ? "" : " (czynna przez Z14b)") : " - AI cwiczy glodne i niewyspane")
                     + "; jeden wzor (MUSZTRA-j): " + (s.DrillLawAi ? "baza 10 + 2 x tier dla wszystkich (baza glowy rodu gry/BK 15 + 3 x tier wylaczona), Z14b (Drill Law Ai) TAK"
                                                   : "Z14b (Drill Law Ai) nie - AI plasko z baza gry (glowa rodu 15 + 3 x tier), wzor tylko u Ciebie i Z14a")
                     + "; niewyspanie: dlug snu o ostatnim swicie (noc bez snu = dzien bez cwiczen od switu do switu; ta sama ksiega NightRest co kara predkosci"
                     + " i morale, ktore licza dlug biezacy): gracz " + On(s.NightRestEnabled)
                     + ", AI " + (NightRest.AiDebtLive(s) ? "TAK" : "nie - AI bez dlugu snu (Night Rest / Ai Camps At Night / Ai Sleep Debt, rowne godziny obozu"
                                  + " (Camp Start = Camp End) albo DLL na sucho): lordowie w armii gracza i partie rodu gracza (Z14a) tez bez kary snu w musztrze"
                                  + (s.NightRestEnabled ? ", gracz z kara" : ""))
                     + ", Z14b (Drill Law Ai) " + On(s.DrillLawAi) + "; " + masks + "; stale: postoj x" + F2(RestDay) + " (ruch < " + RestBelowHours + " h z 24, godzina postoju: osada, oboz, <= "
                     + F2(RestStep) + " jedn./h), marsz x" + F2(MarchDay) + ", dowodca Przywodztwo/" + (int)LeadNorm + " [" + F2(LeadMin) + "-" + F2(LeadMax) + "], zapas +"
                     + (int)(StockBonus * 100) + "% za grupe (perk x" + F2(PerkMult) + "), sztuka sluzy " + (int)WearDays + " dni cwiczen, zlom x" + F2(Yield())
                     + "; zapas od gracza: bron " + w + ", zbroje " + a + (imp != null ? "; " + imp : "") + ".");
        }

        private static string On(bool b) { return b ? "TAK" : "nie"; }

        /// <summary>MUSZTRA-jp (recenzja 1): maski godzin ruchu partii lordow z zapisu (segment T= napisu arm_drill) - jak maska gracza: pozycja z chwili
        /// wczytania (= z chwili zapisu), Stamp = biezaca godzina minus wiek z zapisu (godziny nieobserwowane przed zapisem zostaja nieobserwowane - Observe
        /// policzy je jako ruch, jak bez zapisu). Partii, ktorej juz nie ma, nie ma czego odtwarzac. Stary zapis (bez segmentu): partie AI jak dotad -
        /// pierwsza doba po wczytaniu liczy sie jako marsz.</summary>
        private static string RestoreMasks()
        {
            int had = _pendingMasks.Count, n = 0;
            try
            {
                if (had > 0)
                {
                    long now = (long)Math.Floor(CampaignTime.Now.ToHours);
                    var main = MobileParty.MainParty;
                    var all = MobileParty.AllLordParties;
                    if (all != null)
                        for (int i = 0; i < all.Count; i++)
                        {
                            var mp = all[i];
                            if (mp == null || mp == main || mp.StringId == null || _tr.ContainsKey(mp)) continue;
                            KeyValuePair<int, int> v;
                            if (!_pendingMasks.TryGetValue(mp.StringId, out v)) continue;
                            _tr[mp] = new Track { Pos = mp.GetPosition2D, Stamp = now - v.Value, Mask = v.Key & AllHours };
                            n++;
                        }
                }
            }
            catch (Exception e) { Stumble("RestoreMasks", e); }
            finally { _pendingMasks.Clear(); }
            if (!_maskSegSeen)
                return "maski godzin ruchu partii lordow: brak w zapisie (nowa gra albo zapis sprzed MUSZTRA-jp - partie AI bez historii ruchu, pierwsza doba jako marsz)";
            return "maski godzin ruchu partii lordow z zapisu: odtworzone " + n + " z " + had + (had - n > 0 ? " (partii juz nie ma albo juz obserwowane: " + (had - n) + ")" : "")
                   + ", reszta partii bez maski (cala doba ruchu w zapisie albo brak obserwacji) - jak dotad";
        }

        private const string GivingHandsText = "Weapons and shields in your men's drill stock (gear you discard or leave on the field, and their spare arms) count 50% more in their daily drill.";
        private const string PaidInPromiseText = "Armour in your men's drill stock (gear you discard or leave on the field, and their spare armour) counts 50% more in their daily drill.";

        /// <summary>Nowe opisy czesci kwatermistrza obu perkow (druga polowa bez zmian): PrimaryDescription / SecondaryDescription i zmienne STR1/STR2 opisu.</summary>
        private static void PerkTexts()
        {
            int n = 0;
            var gh = DefaultPerks.Steward.GivingHands;
            var pp = DefaultPerks.Steward.PaidInPromise;
            var pPrim = AccessTools.Property(typeof(PerkObject), "PrimaryDescription");
            var pSec = AccessTools.Property(typeof(PerkObject), "SecondaryDescription");
            if (gh != null && pPrim != null)
            {
                var t = new TextObject(GivingHandsText);
                pPrim.SetValue(gh, t, null);
                if (gh.Description != null) gh.Description.SetTextVariable("STR1", t);
                n++;
            }
            if (pp != null && pSec != null)
            {
                var t = new TextObject(PaidInPromiseText);
                pSec.SetValue(pp, t, null);
                if (pp.Description != null) pp.Description.SetTextVariable("STR2", t);
                n++;
            }
            Log.Info("Musztra: opisy perkow kwatermistrza podmienione " + n + "/2 (Giving Hands - bron, Paid in Promise - zbroja w zapasie cwiczebnym).");
        }

        // ------------------------------------------------------------ zapis
        internal static string Export()
        {
            try
            {
                var inv = CultureInfo.InvariantCulture;
                var sb = new StringBuilder("v1");
                int mask = -1; Track t;
                var main = MobileParty.MainParty;
                if (main != null && _tr.TryGetValue(main, out t)) mask = t.Mask;
                else if (_pendingMainMask >= 0) mask = _pendingMainMask;
                sb.Append("|M=").Append(mask).Append("|F=").Append(_fed ? 1 : 0).Append("|S=");
                int pcs = 0; bool first = true;
                for (int i = 0; i < _stock.Count; i++)
                {
                    var el = _stock.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (el.Amount <= 0 || it == null || !Safe(it.StringId)) continue;
                    var mod = el.EquipmentElement.ItemModifier;
                    string mid = mod != null && Safe(mod.StringId) ? mod.StringId : "";
                    if (!first) sb.Append(';');
                    first = false;
                    sb.Append(it.StringId).Append(',').Append(mid).Append(',').Append(el.Amount);
                    pcs += el.Amount;
                }
                sb.Append("|A=");
                first = true; float oreMain = 0f; int parties = 0;
                foreach (var kv in _acc)
                {
                    var a = kv.Value;
                    if (!Safe(kv.Key) || (a.WW <= 0f && a.WA <= 0f && a.Ore <= 0f)) continue;
                    if (!first) sb.Append(';');
                    first = false;
                    // format v1 bez zmian (5 pol): piate pole (dawniej zlom z taboru lorda) zawsze 0 - tabor nie jest juz zapasem
                    sb.Append(kv.Key).Append(',').Append(a.WW.ToString("0.###", inv)).Append(',').Append(a.WA.ToString("0.###", inv)).Append(',')
                      .Append(a.Ore.ToString("0.###", inv)).Append(",0");
                    parties++;
                    if (main != null && kv.Key == main.StringId) oreMain = a.Ore;
                }
                // MUSZTRA-jp (recenzja 1): maski godzin ruchu pozostalych partii lordow - "id,maska,wiek" (wiek = godziny od ostatniej obserwacji, < 24).
                // Pomijane: gracz (pole M=, format bez zmian), maska calej doby ruchu (= brak maski) i obserwacja starsza niz doba. Stary DLL segment
                // pomija (Import czyta tylko znane przedrostki). Napis idzie przez SaveText.Sync ("arm_drill", ArmouryBehavior) - kawalki po 8000 zn.
                sb.Append("|T=");
                first = true; int masks = 0;
                long nowH = (long)Math.Floor(CampaignTime.Now.ToHours);
                foreach (var kv in _tr)
                {
                    var mp = kv.Key; var tr = kv.Value;
                    if (mp == null || mp == main || tr == null || !Safe(mp.StringId)) continue;
                    long age = nowH - tr.Stamp;
                    int m = tr.Mask & AllHours;
                    if (tr.Stamp < 0 || age < 0 || age >= 24 || m == AllHours) continue;
                    if (!first) sb.Append(';');
                    first = false;
                    sb.Append(mp.StringId).Append(',').Append(m.ToString(inv)).Append(',').Append(age.ToString(inv));
                    masks++;
                }
                int w, ar; GivenCounts(out w, out ar);
                Log.Info("Musztra: zapis - zapas od gracza " + pcs + " szt. (bron " + w + ", zbroje " + ar + "), godzin ruchu gracza " + (mask >= 0 ? Pop(mask).ToString() : "-")
                         + " z 24, maski godzin ruchu partii lordow AI " + masks + " (z " + Math.Max(0, _tr.Count - (main != null && _tr.ContainsKey(main) ? 1 : 0)) + " obserwowanych), napis "
                         + sb.Length + " zn., zlom gracza czeka " + F2(oreMain) + " rudy, partii z licznikami " + parties + ", zasilenie autotestu " + (_fed ? "tak" : "nie") + ".");
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return null; }
        }

        private static bool Safe(string id) { return !string.IsNullOrEmpty(id) && id.IndexOfAny(new[] { ',', ';', '|', '=' }) < 0; }

        internal static void Import(string v)
        {
            try
            {
                _pendingStock = null; _importNote = null; _importRejected = 0; _pendingMasks.Clear(); _maskSegSeen = false;
                if (string.IsNullOrEmpty(v)) { _importNote = "z zapisu: brak klucza (stary zapis) - zapas pusty, godziny ruchu nieznane (ruch)"; return; }
                var inv = CultureInfo.InvariantCulture;
                foreach (var part in v.Split('|'))
                {
                    if (part.StartsWith("M=")) { int m; if (int.TryParse(part.Substring(2), NumberStyles.Integer, inv, out m) && m >= 0) _pendingMainMask = m & AllHours; }
                    else if (part.StartsWith("T="))
                    {
                        // MUSZTRA-jp: maski partii lordow "id,maska,wiek" - odtwarzane w SessionStart (RestoreMasks); zle wpisy pomijane
                        _maskSegSeen = true;
                        foreach (var x in part.Substring(2).Split(';'))
                        {
                            var f = x.Split(',');
                            int m, age;
                            if (f.Length != 3 || f[0].Length == 0 || !int.TryParse(f[1], NumberStyles.Integer, inv, out m) || !int.TryParse(f[2], NumberStyles.Integer, inv, out age)
                                || m < 0 || age < 0 || age >= 24) continue;
                            _pendingMasks[f[0]] = new KeyValuePair<int, int>(m & AllHours, age);
                        }
                    }
                    else if (part.StartsWith("F=")) _fed = part.Substring(2) == "1";
                    else if (part.StartsWith("S=")) _pendingStock = part.Substring(2);
                    else if (part.StartsWith("A="))
                    {
                        foreach (var e in part.Substring(2).Split(';'))
                        {
                            var f = e.Split(',');
                            if (f.Length != 5 || f[0].Length == 0) continue;
                            float ww, wa, om, ol;
                            if (!float.TryParse(f[1], NumberStyles.Float, inv, out ww) || !float.TryParse(f[2], NumberStyles.Float, inv, out wa)
                                || !float.TryParse(f[3], NumberStyles.Float, inv, out om) || !float.TryParse(f[4], NumberStyles.Float, inv, out ol)) continue;
                            _acc[f[0]] = new Acc { WW = ww, WA = wa, Ore = om + Math.Max(0f, ol) };   // zlom z taboru ze starszych zapisow probnych - do zlomu ludzi
                        }
                    }
                }
            }
            catch (Exception e) { Stumble("Import", e); }
        }

        /// <summary>Zapas z zapisu dopiero przy starcie sesji (przedmioty i modyfikatory pewnie wczytane); nieznane id pominiete i policzone.</summary>
        private static string ResolveImport()
        {
            string note = _importNote;
            _importNote = null;
            var raw = _pendingStock;
            _pendingStock = null;
            if (raw == null) return note;
            int pcs = 0;
            try
            {
                foreach (var e in raw.Split(';'))
                {
                    if (e.Length == 0) continue;
                    var f = e.Split(',');
                    int n;
                    if (f.Length != 3 || !int.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out n) || n <= 0) { _importRejected++; continue; }
                    ItemObject it = null; ItemModifier mod = null;
                    try { it = MBObjectManager.Instance.GetObject<ItemObject>(f[0]); } catch { }
                    if (f[1].Length > 0) { try { mod = MBObjectManager.Instance.GetObject<ItemModifier>(f[1]); } catch { } }
                    if (it == null || !Eligible(it) || (f[1].Length > 0 && mod == null)) { _importRejected += n; continue; }
                    _stock.AddToCounts(new EquipmentElement(it, mod), n);
                    pcs += n;
                }
            }
            catch (Exception ex) { Stumble("ResolveImport", ex); }
            int w, a; GivenCounts(out w, out a);
            float oreMain = 0f; Acc am;
            var main = MobileParty.MainParty;
            if (main != null && _acc.TryGetValue(main.StringId ?? "", out am)) oreMain = am.Ore;
            return "z zapisu: zapas " + pcs + " szt. (bron " + w + ", zbroje " + a + "), godzin ruchu gracza " + (_pendingMainMask >= 0 ? Pop(_pendingMainMask).ToString() : "-")
                   + " z 24, zlom gracza czeka " + F2(oreMain) + " rudy, partii z licznikami " + _acc.Count + ", zasilenie autotestu " + (_fed ? "tak" : "nie")
                   + ", odrzucono " + _importRejected + " szt.";
        }

        // ------------------------------------------------------------ autotest: zasilenie zapasu (krytyka 7)
        private static bool AutotestActive()
        {
            if (_autotest >= 0) return _autotest == 1;
            _autotest = 0;
            try
            {
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name != "CrashScribe") continue;
                    Type t = asm.GetType("CrashScribe.Autotest", false);
                    FieldInfo f = t != null ? t.GetField("Active", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) : null;
                    if (f != null && f.FieldType == typeof(bool) && (bool)f.GetValue(null)) _autotest = 1;
                    break;
                }
            }
            catch { _autotest = 0; }
            return _autotest == 1;
        }

        /// <summary>Tylko w autotescie (CrashScribe.Autotest.Active), co dobe od doby 2, dopoki zapas od gracza nie jest pelny w obu grupach (flaga w zapisie):
        /// brakujace do pelnego (sztuka na 3 ludzi) sztuki broni i zbroi z taboru gracza, reszta kupiona najtaniej z polki miasta za zloto gracza, gdy druzyna
        /// stoi w miescie (handel - kasa miasta dostaje zaplate); ta sama funkcja przyjecia co ekrany; czego zapas nie przyjmie - wraca do taboru.
        /// Bez ludzi (nowa kampania: gracz sam) nie ma czego zasilac - proba nastepnej doby; flaga dopiero przy pelnym zapasie.</summary>
        private static void AutotestFeed()
        {
            var main = MobileParty.MainParty;
            if (!StockOn || main == null || _fed || !AutotestActive()) return;
            _feedTries++;
            int men = Men(main), full = Full(men);
            int w0, a0; GivenCounts(out w0, out a0);
            int[] want = { Math.Max(0, full - w0), Math.Max(0, full - a0) };
            if (full > 0 && want[GW] + want[GA] == 0) { _fed = true; Log.Info("Musztra (autotest): zapas od gracza pelny (bron " + w0 + ", zbroje " + a0 + " na pelny " + full + ") - zasilanie zakonczone."); return; }
            var st = main.CurrentSettlement;
            bool town = st != null && st.IsTown && st.Town != null && st.ItemRoster != null;
            if (full <= 0)
            {
                if (_feedTries == 1 || _feedTries % 10 == 0)
                    Log.Info("Musztra (autotest): zasilenie zapasu - proba " + _feedTries + ": druzyna bez zolnierzy (ludzi " + men + "), nic do zasilenia; ponowie w nastepnej dobie.");
                return;
            }
            var tmp = new ItemRoster();
            int[] got = new int[2];
            int fromBag = 0, bought = 0, gold = 0;
            var bag = main.ItemRoster;
            var bagEls = new List<ItemRosterElement>();
            for (int i = 0; i < bag.Count; i++) { var el = bag.GetElementCopyAtIndex(i); if (el.Amount > 0 && Eligible(el.EquipmentElement.Item)) bagEls.Add(el); }
            foreach (var el in bagEls)
            {
                int g = GroupOf(el.EquipmentElement.Item);
                int t = Math.Min(want[g] - got[g], el.Amount);
                if (t <= 0) continue;
                bag.AddToCounts(el.EquipmentElement, -t); tmp.AddToCounts(el.EquipmentElement, t); got[g] += t; fromBag += t;
            }
            if (town)
            {
                for (int g = 0; g < 2; g++)
                {
                    while (got[g] < want[g])
                    {
                        int best = -1, bp = int.MaxValue;
                        var shelf = st.ItemRoster;
                        for (int i = 0; i < shelf.Count; i++)
                        {
                            var el = shelf.GetElementCopyAtIndex(i);
                            if (el.Amount <= 0 || GroupOf(el.EquipmentElement.Item) != g || !Eligible(el.EquipmentElement.Item)) continue;
                            int p = st.Town.MarketData.GetPrice(el.EquipmentElement, main, false, st.Party);
                            if (p > 0 && p < bp) { bp = p; best = i; }
                        }
                        if (best < 0 || Hero.MainHero.Gold < bp) break;
                        var pick = shelf.GetElementCopyAtIndex(best).EquipmentElement;
                        shelf.AddToCounts(pick, -1);
                        GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, st, bp, true);
                        tmp.AddToCounts(pick, 1); got[g]++; bought++; gold += bp;
                    }
                }
            }
            int taken = fromBag + bought > 0 ? Accept(tmp, SrcAutotest) : 0, back = 0;
            for (int i = 0; i < tmp.Count; i++) { var el = tmp.GetElementCopyAtIndex(i); if (el.Amount > 0) { bag.AddToCounts(el.EquipmentElement, el.Amount); back += el.Amount; } }
            int w1, a1; GivenCounts(out w1, out a1);
            if (w1 >= full && a1 >= full) _fed = true;
            if (fromBag + bought > 0 || _feedTries == 1 || _feedTries % 10 == 0 || _fed)
                Log.Info("Musztra (autotest): zasilenie zapasu - proba " + _feedTries + ", ludzi " + men + " (pelny " + full + "): z taboru " + fromBag + " szt., kupione " + bought
                         + " szt. za " + gold + " d " + (town ? "w " + st.Name : "(poza miastem - bez zakupu)") + ", zloto gracza " + Hero.MainHero.Gold + "; przyjeto " + taken
                         + ", wraca do taboru " + back + "; zapas od gracza: bron " + w1 + ", zbroje " + a1 + (_fed ? " - PELNY, koniec zasilania." : " - ponowie w nastepnej dobie."));
        }

        // ------------------------------------------------------------ linie dnia
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null) return;
            long t0 = Stopwatch.GetTimestamp();
            Clk();
            int day = (int)CampaignTime.Now.ToDays;
            try
            {
                _dailyN++;
                if (_dailyN >= 2 && !_fed) { try { AutotestFeed(); } catch (Exception e) { Stumble("AutotestFeed", e); } }
                int w, a; GivenCounts(out w, out a);
                int given = w + a;
                int inToday = _in[0] + _in[1] + _in[2] + _in[3];
                string bal;
                if (_givenYday < 0) bal = "bilans zapasu od Ciebie od jutra (dzis " + given + ")";
                else
                {
                    int expect = _givenYday + inToday - _pWornGiven;
                    bal = "bilans zapasu od Ciebie " + (expect == given ? "ZGODNY" : "NIEZGODNY") + " (wczoraj " + _givenYday + " + przyjete " + inToday + " - zuzyte " + _pWornGiven + " = dzis " + given
                          + (expect == given ? "" : ", oczekiwane " + expect) + ")";
                }
                _givenYday = given;
                if (s.DrillLog)
                {
                    PlayerLine(day, s, w, a, bal);
                    AiLine(day, s);
                    if (day % 5 == 0) KingdomLine(day, s);
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally
            {
                Unclk();
                long dc = Stopwatch.GetTimestamp() - t0;   // koszt linii dnia (i zasilenia) wchodzi do kosztu nastepnej doby - linia jest juz wypisana
                ClearDay(); _ticks = dc; _stumbles = 0;
            }
        }

        private static void PlayerLine(int day, Settings s, int w, int a, string bal)
        {
            var sb = new StringBuilder("Musztra (gracz): dzien ").Append(day).Append(" - ");
            var r = _prLast;
            var c = r != null ? r.C : null;
            if (r == null || c == null || r.Day < day - 1)
                sb.Append(s.DrillLaw ? "brak treningu wedlug musztry w tej dobie" : "musztra gracza wylaczona (Drill Law)");
            else
            {
                double perHead = c.Men > 0 ? r.Computed / (double)c.Men : 0;
                double sk = c.StockOn ? c.S : 1;
                double prod = c.Off ? 0 : r.B * c.L * c.D * sk;
                double aMen = r.N > 0 ? r.ShN / r.N : 1;           // A niezaleznie: udzial uzbrojonych z 171 wazony liczba ludzi (nie Fin/Pre)
                double check = c.Off ? 0 : (prod + r.P) * aMen;    // kontrola przyblizona: A rozne w oddzialach, wiec +-kilka %
                sb.Append("ludzi ").Append(c.Men).Append("; B ").Append(F1(r.B)).Append(" x dowodca ").Append(F3(c.L)).Append(" (Przywodztwo ").Append(c.Lead).Append(") x dzien ")
                  .Append(F2(c.D)).Append(" (").Append(c.Hungry ? "GLOD - bez cwiczen" : (c.Sleepless ? "DLUG SNU O SWICIE " + c.Debt + ", teraz " + c.DebtNow + " - bez cwiczen" : (c.Rest ? "postoj" : "marsz")))
                  .Append(", ruch ").Append(c.Moved).Append(" h z 24) x zapas ").Append(F3(sk)).Append(" = ").Append(F1(prod));
                if (c.Off) sb.Append("; perki P ").Append(F1(r.P)).Append(" -> 0 (glod/sen - perki tez 0)");
                else sb.Append("; + perki P ").Append(F1(r.P)).Append(" = ").Append(F1(r.Pre));
                sb.Append("; bron A ").Append(F3(aMen)).Append(" (sr. wazona ludzmi").Append(c.ArmsGate ? "" : ", wylaczone").Append("); (B x L x D x S + P) x A = ").Append(F1(check))
                  .Append(" wobec XP wyliczone ").Append(r.Computed).Append(" (").Append(Pct(Math.Abs(check - r.Computed), Math.Max(1, r.Computed))).Append(" roznicy; ")
                  .Append(F1(perHead)).Append(" na glowe); przyjete przez roster ").Append(r.Accepted).Append(", uciete limitem awansu ").Append(r.Cut)
                  .Append(" -> ").Append(r.Accepted == r.Computed - r.Cut ? "ZGODNE" : "NIEZGODNE (roznica " + (r.Computed - r.Cut - r.Accepted) + ")");
                sb.Append("; zapas: bron ").Append(c.StockW).Append('/').Append(c.Full).Append(" (od Ciebie ").Append(c.GivenW).Append(", nadwyzka ludzi ").Append(c.SurW)
                  .Append("), zbroje ").Append(c.StockA).Append('/').Append(c.Full).Append(" (od Ciebie ").Append(c.GivenA).Append(", nadwyzka ").Append(c.SurA)
                  .Append("), Giving Hands ").Append(c.PerkW ? "tak" : "nie").Append(", Paid in Promise ").Append(c.PerkA ? "tak" : "nie")
                  .Append(c.StockOn ? "" : " (zapas wylaczony)");
            }
            float ore = 0f; Acc am;
            var main = MobileParty.MainParty;
            if (main != null && main.StringId != null && _acc.TryGetValue(main.StringId, out am)) ore = am.Ore;
            sb.Append("; przyjeto ").Append(_in[0] + _in[1] + _in[2] + _in[3]).Append(" (wyrzucone ").Append(_in[SrcDiscard]).Append(", lup ").Append(_in[SrcLoot])
              .Append(", trofea ").Append(_in[SrcTrophies]).Append(", autotest ").Append(_in[SrcAutotest]).Append("), bez miejsca ").Append(_noRoom)
              .Append("; zuzyto ").Append(_pWornW + _pWornA).Append(" (bron ").Append(_pWornW).Append(", zbroje ").Append(_pWornA).Append(", w tym od Ciebie ").Append(_pWornGiven)
              .Append("; bez metalu ").Append(_pNoMetal).Append("), zlom +").Append(F2(_pOreAdd)).Append(" rudy, czeka ").Append(F2(ore)).Append(", sprzedano ").Append(_pSoldU)
              .Append(" ladunkow za ").Append(_pSoldGold).Append(" d; ").Append(bal).Append("; zapas od Ciebie: bron ").Append(w).Append(", zbroje ").Append(a);
            sb.Append("; partie rodu i armii: ").Append(_cParties);
            if (_cParties > 0) sb.Append(" (ludzi ").Append(_cMen).Append(", XP ").Append((long)_cXp).Append(", dowodca sr. x").Append(F2(_cL / _cParties)).Append(", na postoju ").Append(_cRest)
                                 .Append(", bez cwiczen - glod/sen ").Append(_cOff).Append(')');
            sb.Append("; potkniecia ").Append(_stumbles).Append('.');
            Log.Info(sb.ToString());
        }

        private static string Pct(double part, double all) { return all > 0 ? ((int)Math.Round(100 * part / all)).ToString() + "%" : "-"; }
        private static string X(double num, double den) { return den > 0 ? "x" + F2(num / den) : "-"; }

        private static void AiLine(int day, Settings s)
        {
            var inv = CultureInfo.InvariantCulture;
            string lead = "-";
            if (_leads.Count > 0)
            {
                _leads.Sort();
                int n = _leads.Count;
                Func<double, int> P = q => _leads[Math.Max(0, Math.Min(n - 1, (int)Math.Ceiling(q * n) - 1))];
                lead = "mediana " + _leads[n / 2] + ", p10 " + P(0.1) + ", p90 " + P(0.9);
            }
            float ore = 0f;
            var main = MobileParty.MainParty;
            foreach (var kv in _acc) if (main == null || kv.Key != main.StringId) ore += kv.Value.Ore;
            var sb = new StringBuilder("Musztra AI: dzien ").Append(day).Append(" - partii ").Append(_aParties).Append(", ludzi ").Append(_aMen)
              .Append("; wazone baza gry (").Append(((long)_aW).ToString(inv)).Append(" XP): dowodca ").Append(X(_aWL, _aW)).Append(" (Przywodztwo ").Append(lead)
              .Append(", bez dowodcy ").Append(_aNoLead).Append("), dzien ").Append(X(_aWD, _aW)).Append(" (postoj ").Append(Pct(_aWRest, _aW)).Append(", marsz ").Append(Pct(_aWMarch, _aW))
              // MUSZTRA-jp (recenzja 2/10): etykieta "sen " bez zmian (regexy SCR\jedenwzor\parse.py, swit.py); "od switu" mowi segment "sen od switu: partii N"
              .Append(", glod ").Append(Pct(_aWHungry, _aW)).Append(", sen ").Append(Pct(_aWSleep, _aW)).Append("), dowodca x dzien ").Append(X(_aWLD, _aW))
              .Append(s.DrillLawAi ? " (Z14b CZYNNA)" : " (Z14b WYLACZONA - pomiar)").Append("; zapas ").Append(X(_aWS, _aW)).Append(s.DrillStockAi ? " (CZYNNY" : " (wylaczony - pomiar")
              .Append("; pelny u ").Append(_aFullStock).Append(" partii, pusty u ").Append(_aNoStock).Append("); razem ").Append(X(_aWLDS, _aW))
              .Append("; razem przy progu postoju 4/8/12 h: ").Append(X(_aWT[0], _aW)).Append('/').Append(X(_aWT[1], _aW)).Append('/').Append(X(_aWT[2], _aW))
              .Append("; PROG Z14b - dowodca x dzien bez zapasu, dni bez kary (").Append(Pct(_aWn, _aW)).Append(" wagi): ").Append(X(_aWLDn, _aWn))
              .Append(", przy progu postoju 4/8/12 h: ").Append(X(_aWTn[0], _aWn)).Append('/').Append(X(_aWTn[1], _aWn)).Append('/').Append(X(_aWTn[2], _aWn))
              .Append("; godziny ruchu w dobie (partie): 0 h ").Append(_hb[0]).Append(", 1-3 h ").Append(_hb[1]).Append(", 4-11 h ").Append(_hb[2]).Append(", 12+ h ").Append(_hb[3])
              .Append("; kara glod/sen ").Append(PenaltyAi(s) ? "CZYNNA" : "WYLACZONA").Append(" (partii bez cwiczen ").Append(_aOff).Append(", zabrane XP gry ")
              .Append(((long)_aPenalty).ToString(inv)).Append(")")
              .Append("; XP: baza gry ").Append(((long)_aGame).ToString(inv)).Append(", po czynnej regule (dni bez kary, czesc bazowa) ").Append(((long)_aRule).ToString(inv));
            // MUSZTRA-j: jeden wzor wobec gry (po segmencie "XP:" - wczesniejsze segmenty bez zmian, porownywalne z kopia-grupa11)
            string now = s.DrillLawAi ? "wzor" : "czynna regula (Z14b wylaczona)";
            sb.Append("; JEDEN WZOR: baza wzoru 10 + 2 x tier ").Append(((long)_aRuleBase).ToString(inv)).Append(" (").Append(X(_aRuleBase, _aGame))
              .Append(" bazy gry; glowy rodow ").Append(Pct(_aHeadBase, _aGame)).Append(" bazy gry), perki P ").Append(((long)_aPerks).ToString(inv))
              .Append(", model bez bazy (elementy) ").Append(_aNoModel)
              .Append("; XP po broni (171): gra ").Append(((long)_aXpGame).ToString(inv)).Append(" -> ").Append(now).Append(' ').Append(((long)_aXpNow).ToString(inv))
              .Append(" (").Append(X(_aXpNow, _aXpGame)).Append("), na glowe na dobe gra ").Append(_aManN > 0 ? F1(_aXpGame / _aManN) : "-").Append(" -> ")
              .Append(_aManN > 0 ? F1(_aXpNow / _aManN) : "-").Append(" (ludzio-dni ").Append(((long)_aManN).ToString(inv)).Append(")")
              .Append("; sen od switu: partii ").Append(_aSleepDawn).Append(" (w tym splacone przed treningiem ").Append(_aSleepPaid)
              .Append(" - dawniej moglyby cwiczyc), dlug teraz > dlug o swicie (takze Z14a) ").Append(_sleepNowAbove).Append(_sleepNowAbove > 0 ? " - BLAD (ma byc 0)" : "");
            sb.Append("; zuzyto ").Append(_aWornW + _aWornA).Append(" szt. (bron ").Append(_aWornW).Append(", zbroje ").Append(_aWornA).Append("; bez metalu ").Append(_aNoMetal)
              .Append("), zlom +").Append(F1(_aOreAdd)).Append(" rudy, czeka razem ").Append(F1(ore)).Append(", sprzedano ").Append(_aSoldU).Append(" ladunkow za ").Append(_aSoldGold)
              .Append(" d (lordowie ").Append(_aSoldLord).Append(", sakiewki ").Append(_aSoldPurse).Append("), przepadlo z rozbitymi ").Append(F1(_oreLost))
              .Append("; potkniecia ").Append(_stumbles).Append("; koszt ").Append((_ticks * 1000.0 / Stopwatch.Frequency).ToString("0.0", inv))
              .Append(" ms (godziny ruchu, model treningu, tick partii z zuzyciem, kowale, ekrany zapasu, linie dnia poprzedniej polnocy).");
            Log.Info(sb.ToString());
        }

        /// <summary>Co 5 dob: wedlug krolestw - "dowodca x dzien" i zapas z ostatnich dob, godziny ruchu, oraz stan armii AI dzis (sredni tier, t3+, konni);
        /// MUSZTRA-j: XP na glowe na dobe gra -> wzor, glowy rodow, t1 -> t6 przy tym tempie (z dob od ostatniej linii).</summary>
        private static void KingdomLine(int day, Settings s)
        {
            string now = s.DrillLawAi ? "wzor" : "czynna regula";
            var men = new Dictionary<string, long[]>();   // id -> [ludzi, suma tierow, t3+, konni, partii]
            var names = new Dictionary<string, string>();
            foreach (var mp in MobileParty.AllLordParties)
            {
                try
                {
                    if (mp == null || !mp.IsActive || mp.LeaderHero == null || mp == MobileParty.MainParty || NoGameBase(mp) || Undead.Party(mp) || mp.MemberRoster == null) continue;
                    var kd = mp.MapFaction as Kingdom;
                    string id = kd != null ? kd.StringId : "-";
                    if (!names.ContainsKey(id)) names[id] = kd != null ? kd.Name.ToString() : "bez krolestwa";
                    long[] v;
                    if (!men.TryGetValue(id, out v)) { v = new long[5]; men[id] = v; }
                    v[4]++;
                    var r = mp.MemberRoster;
                    for (int i = 0; i < r.Count; i++)
                    {
                        var el = r.GetElementCopyAtIndex(i);
                        var ch = el.Character;
                        if (ch == null || ch.IsHero || el.Number <= 0) continue;
                        v[0] += el.Number; v[1] += (long)el.Number * ch.Tier; if (ch.Tier >= 3) v[2] += el.Number; if (ch.IsMounted) v[3] += el.Number;
                    }
                }
                catch (Exception e) { Stumble("KingdomLine(partia)", e); }
            }
            var keys = new List<string>(men.Keys);
            keys.Sort((x, y) => men[y][0].CompareTo(men[x][0]));
            var sb = new StringBuilder("Musztra AI wedlug krolestw: dzien ").Append(day).Append(" (czynniki z dob od ostatniej linii) - ");
            bool first = true;
            foreach (var id in keys)
            {
                var v = men[id];
                KAcc k; _k.TryGetValue(id, out k);
                if (!first) sb.Append(" | ");
                first = false;
                sb.Append(names[id]).Append(": partii ").Append(v[4]).Append(", ludzi ").Append(v[0])
                  .Append(", dowodca x dzien ").Append(k != null ? X(k.WLD, k.W) : "-").Append(", zapas ").Append(k != null ? X(k.WS, k.W) : "-")
                  .Append(", prog Z14b (bez zapasu, dni bez kary) 4/8/12 h: ").Append(k != null ? X(k.WTn[0], k.Wn) + "/" + X(k.WTn[1], k.Wn) + "/" + X(k.WTn[2], k.Wn) : "-");
                if (k != null) sb.Append(", godziny ruchu 0/1-3/4-11/12+: ").Append(k.Hb[0]).Append('/').Append(k.Hb[1]).Append('/').Append(k.Hb[2]).Append('/').Append(k.Hb[3]);
                sb.Append(", sredni tier ").Append(v[0] > 0 ? F2(v[1] / (double)v[0]) : "-").Append(", t3+ ").Append(Pct(v[2], v[0])).Append(", konni ").Append(Pct(v[3], v[0]));
                // MUSZTRA-j (po "konni" - wczesniejsze pola bez zmian): XP na glowe na dobe po broni gra -> wzor, udzial glow rodow w bazie gry, t1 -> t6 przy tym tempie
                if (k != null && k.ManN > 0)
                    sb.Append(", XP na glowe na dobe (po broni) gra ").Append(F1(k.XpG / k.ManN)).Append(" -> ").Append(now).Append(' ').Append(F1(k.XpN / k.ManN))
                      .Append(" (").Append(X(k.XpN, k.XpG)).Append("), glowy rodow ").Append(Pct(k.HB, k.W)).Append(" bazy gry, t1->t6 przy tym tempie gra ")
                      .Append(Days(k.XpG, k.RB)).Append(" -> ").Append(now).Append(' ').Append(Days(k.XpN, k.RB)).Append(" dob");
            }
            Log.Info(sb.ToString());
            _k.Clear();
        }

        /// <summary>MUSZTRA-j: doby t1 -> t6 przy tempie "XP / baza wzoru" (koszty BK, PromoteDays; z perkami i udzialem broni, bez bitew).</summary>
        private static string Days(double xp, double ruleBase)
        {
            if (xp <= 0 || ruleBase <= 0) return "-";
            return (PromoteDays / (xp / ruleBase)).ToString("0", CultureInfo.InvariantCulture);
        }
    }
}
