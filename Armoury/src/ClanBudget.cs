using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// PACZKA 166 + 162m - BUDZET RODU I DWOR MINIMALNY (projekt etapu 2, krok C1; PLAN 2.7, 2.8, czesc 2.9). Wylacznik ClanBudgetEnabled
    /// (z nim AiWageLimitDesertionOff i 165 - "165 nigdy bez 166"). Tylko rody AI w krolestwie (wasale; najemnicy - 185); gracz bez budzetu i bez
    /// dworu ([D] 08.10).
    ///
    /// Raz na dobe (po Banku, przed ksiega rodow - na D z wczoraj, czesc "ziemia" z dzisiejszych lenn), dla kazdego rodu AI:
    ///  - D = D staly (169c: ziemia + korona + kontrakt + majatek; u najezdzcow - Zelazne Wyspy, Dothrakowie, Wolni Ludzie - takze lup), mieszany z D z
    ///    zapisu, dopoki pomiar ma mniej niz 28 dob (pierscienie D stalego zyja tylko w sesji); start kampanii D = G/60. G = kiesy rodziny (glowa +
    ///    dorosli). R = max(20 000; 20 x D) + 5 000 x doroslych.
    ///  - pulap zoldu (partie + zalogi + karawany): pokoj 0.28 D (+ Z.1: 0.4 x skrzynia wojenna przy G > R), wojna 0.60 D + 0.8 (G - R)/45 przy G > R;
    ///    bieda: pokoj G < 60 D -> x (1 - 0.1 (1 - G/60D)),
    ///    wojna G < R -> x (0.8 + 0.2 G/R), G < 0.25 R -> x 0.5. Dwor i wyzywienie 0.35/0.20 D, sprzet 0.17 D (+0.2 (G-R)/45 w wojnie), budowy 0.10 D
    ///    (w wojnie tylko na mury, wieze i koszary - recenzja C1, W5).
    ///  - zalogi: w wojnie pulap gry bez zmian i finansowane pierwsze; w pokoju cel 50% zalogi wojennej (srednia z dob wojny), najwyzej 80% pulapu.
    ///    Partie dostaja reszte - SetWagePaymentLimit (postfiks MakeClanFinancialEvaluation; dzialaja hamulce gry: werbunek i awanse ponad limit).
    ///  - ZWOLNIENIA zamiast dezercji gry z limitu zoldu (AiWageLimitDesertionOff): zold > 1.10 x pulap przez 3 doby -> codziennie 15% nadwyzki ludzi:
    ///    najpierw zalogi ponad cel pokojowy (pokoj), potem najemnicy z karczmy, potem najnizszy tier. Ludzie do ludnosci BK najblizszej wsi rodu (zaloga -
    ///    wsi tej twierdzy), czesc sakiewki partii do kiesy tej wsi; bez wsi z danymi BK nikt nie jest zwalniany (nikt nie znika - Z6).
    ///    P3/W2 (bieg roczny 364 doby, TRIAZ-3 O1-3; klucz ReleaseAcrossTiers): po najemnikach z karczmy NIE "najnizszy tier", tylko losowanie czlowieka z
    ///    calej partii (kazdy ma rowna szanse, DrawAcross) - zwalniany kosztuje srednio tyle, co sredni czlowiek partii, wiec za ten sam zold odchodzi
    ///    mniej glow. Ta sama regula dla partii i zalog. Klucz wylaczony = najnizszy tier najpierw (stan sprzed).
    ///  - Straz bez zoldu (182): pulap w ludziach = pulap zoldu / nominalny zold czlowieka; werbunek, nowe partie i przyrost zalog Strazy staja na nim.
    ///  - nowa partia rodu: nie, gdy wolne miejsce w pulapie partii < 30 ludzi x sredni zold (rod bez zadnej partii - tak).
    ///  - KIESA RODZINY: dorosly czlonek AI z kiesa ponad 5 000 dopelnia kiese glowy do max(5 000; koszt dnia rodu), sam nie schodzi ponizej 5 000.
    ///    Recenzja C1 (C1-G1): IronBankFamilyPays (T5) dziala dalej przed Bankiem - prog Banku (10/20 dni zoldu) jest wyzszy niz cel kiesy rodziny.
    ///  - SPRZET: zakupy AI (AiGear) placone przez pana najwyzej z niewydanego przydzialu sprzetu (0.17 D na dobe, najwyzej 30 dni).
    ///  - BUDOWY (BuildFunding): z przydzialu budow budzetu (0.10 D w pokoju i w wojnie - w wojnie BuildFunding finansuje tylko mury, wieze i koszary,
    ///    Jeff 05.10 "w wojnie 0, chyba ze mury - tak"; recenzja C1, W5) zamiast 10% dochodu modelu.
    ///  - 162m DWOR: glowa -> kasa siedziby raz na dobe (udzial "dwor i wyzywienie" minus jedzenie partii - szacunek: zuzycie dnia x cena zboza);
    ///    siedziba = Clan.HomeSettlement (wies -> jej miasto albo zamek; bez - najblizsze miasto krolestwa). Kasa MIASTA: znacznik tarczy dworu
    ///    (SoldierPay.HoldCourt) - regulator gry go nie kasuje, schodzi tylko z zaworem renty i dania wojenna, nigdy ponad nadwyzke kasy.
    ///  - E5/A2 (162) DWOR PELNY: przydzial sprzetu niewydany przez AiGearDaysCap dni dochodzi do wplaty dworu (CourtFull - regula i ciecia tam).
    /// Platnik -> odbiorca: glowa -> kasa siedziby (dwor); czlonek -> glowa (kiesa rodziny); sakiewka zwolnionych -> kiesa wsi; ludzie -> ludnosc BK wsi.
    /// </summary>
    internal static class ClanBudget
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int Days28 = 28;
        private static readonly string[] Raiders = { "sturgia", "khuzait", "freefolk" };   // Zelazne Wyspy, Dothrakowie, Wolni Ludzie - lup w D (W-1)

        internal static bool On { get { var s = Settings.Current; return s != null && s.ClanBudgetEnabled; } }

        internal sealed class B
        {
            public double D, D0 = -1, G, R, F, Cap, PartyCap, Household, Gear, Build, GarTarget, GearLeft, Nominal;
            public bool War, Zero, Today;
            public long WPar, WGar, WCar; public int MenPar, MenGar, Adults, Streak, MenCap = -1;
            // test 120 dob (wojsko w wojnie): pulap bez udzialu dworu, udzial dworu dostepny dla zoldu w wojnie, siedziba i jedzenie partii (szacunek) dnia
            public double CapBase, CourtRoom, Food; public Settlement Seat;
            // 168: pulap bez kredytu wojennego, miejsce KW w pulapie i wyplata dnia; dluznik (dwor -50%, budowy 0); zajecie (D bez dochodu wsi)
            public double CapNoKw, KwRoom, SeizedLand; public long KwLent; public bool Debtor, Seized;
            public double Lapsed;   // E5/A2 (162): przeterminowany dzis przydzial sprzetu do wplaty dworu (CourtFull.Lapse -> Take -> Paid)
            public double Aid;      // W5: przydzial korony tej doby (doplata do zoldu wedlug potrzeby: gotowka + miejsce na krok werbunku) - JUZ doliczony do Cap i PartyCap; W7 - razem z podniesieniem z poboru stawki wojennej pana
            public double AidCash;  // W5 (recenzja): w tym GOTOWKA korony tej doby - zold druzyn, ktorego rod nie placi sam (dwor i niewydany sprzet mu nie ustepuja); W7 - razem z gotowka poboru pana
        }

        private static readonly Dictionary<Clan, B> _b = new Dictionary<Clan, B>();
        private static readonly Dictionary<string, double[]> _saved = new Dictionary<string, double[]>();   // id rodu -> [D, streak, przydzial sprzetu] z zapisu
        private static readonly Dictionary<string, float> _warGar = new Dictionary<string, float>();        // id twierdzy -> zold zalogi w wojnie (srednia)
        private static readonly Dictionary<Settlement, int> _garTarget = new Dictionary<Settlement, int>(); // dzis (pokoj): cel zoldu zalogi
        private static int _day = -1;

        // liczniki doby (linia "Budzet rodow (166)" i "Obieg")
        internal static long LastCourt, LastCourtTown, LastFood, LastFamily, LastReleasedPurse, LastBuildShare, LastGearShare;
        internal static long LastReleasedPurseVet;     // E5/B6 (201): sakiewki zwolnionych 166, ktorzy staneli w karczmie - do puli weteranow (VetPurse)
        internal static int LastReleasedMen, LastReleasedPar, LastReleasedGar, LastNoVillage, LastFamilyN;
        // E4/9: zwolnieni 166 z partii i zalog Strazy (oczekiwane 0 - WatchNoReleases) i rodow w zajeciu (DLUG 4 - linia "Dlugi (168)") - doba
        internal static int LastReleasedWatch, LastReleasedSeized;
        private static int _dRelWatch, _dRelSeized;
        /// <summary>E4/9: pulap 166 calej Strazy w ludziach (linia "Straz z Westeros"); -1 - brak.</summary>
        internal static int WatchPoolCap { get { return _watchPoolCap; } }
        private static long _dCourt, _dCourtTown, _dFood, _dFamily, _dRelPurse, _dBuild, _dGear, _dGearSpent, _dCapSum, _dWageSum, _dD;
        private static int _dRelMen, _dRelPar, _dRelGar, _dNoVillage, _dFamilyN, _dOver, _dStreak, _dPeace, _dWar, _dClans, _dPoor, _dVanished;
        private static int _dRelTavern;     // E4/10 (W1): zwolnieni 166, ktorzy staneli w karczmie (z _dRelMen)
        // Z.1 (pomiar): rody AI w pokoju (bez najemnikow) - pulap, w tym skrzynia pokoju (ile rodow), zold naliczony, ludzie w partiach i zalogach
        private static long _dPzCap, _dPzChest, _dPzWage; private static int _dPzN, _dPzChestN, _dPzMenPar, _dPzMenGar;
        // P3/W2 (pomiar): zwolnieni 166 wedlug tieru (t1-2 / t3-4 / t5+) - ludzie i ich zold dzienny; rody w wojnie, ktorym zalogi i karawany zajely
        // caly pulap (na partie 0), i ile z nich nie ma juz ludzi w partiach
        private static readonly int[] _dRelTier = new int[3];
        private static readonly long[] _dRelTierWage = new long[3];
        private static int _dGarEatsCap, _dGarEatsCapEmpty;
        private static long _dRelPurseVet;  // E5/B6 (201): ich czesc sakiewki partii - do puli weteranow miasta karczmy (VetPurse.FromRelease)
        // E5/B6 (201): miasta karczm i ilu zwolnionych w nich stanelo w JEDNYM ReleaseFrom (zbierane w ToVillage tylko przy _relTavOn, czytane przy sakiewce)
        private static readonly List<KeyValuePair<Settlement, int>> _relTav = new List<KeyValuePair<Settlement, int>>();
        private static bool _relTavOn;
        private static int _dDesertOff, _dSpawnBlock, _dRecruitBlock, _dGarBlock, _dGarLimited, _dPartyLimited, _dWatchMen, _dWatchCap = -1;
        // 2 (werbunek Strazy, 10.10): JEDEN pulap w ludziach dla calej Strazy (jeden zakon, jedna kiesa). Dotad pulap liczony osobno na rod: rody Strazy
        // bez dochodu mialy pulap ok. 0 i ich druzyny nie werbowaly (werbunek wstrzymany 50-200/d przy Strazy 1.5-2 tys. wobec pulapu calosci 3-5 tys.),
        // a rod ponad wlasnym pulapem zwalnial ludzi mimo luzu calej Strazy. Pula = suma pulapow rodow Strazy z rozliczenia doby (ta sama liczba co w linii 166).
        private static readonly List<Clan> _watchClans = new List<Clan>();
        // recenzja 2 (R1): ludzie Strazy, ktorych zwolnienia moga ruszyc (partie lordow poza bitwa/oblezeniem; zalog na Murze nie tniemy) - podzial nadwyzki puli
        private static readonly Dictionary<Clan, int> _watchRel = new Dictionary<Clan, int>();
        private static int _watchRelSum;
        private static int _watchPoolCap = -1, _watchPoolMen;
        private static string _watchDetail = "";
        // test 120 dob (wojsko w wojnie): dwor ustepuje zoldowi (udzial dostepny, zold go zajal, u ilu rodow); limity partii z wolnego miejsca rodu
        private static long _dCourtRoom, _dCourtYield; private static int _dCourtRoomN, _dCourtYieldN, _dPartyOverCap;
        // 168: kredyt wojenny w pulapie (miejsce i wyplata), dluznicy, zajete wsie
        private static long _dKwRoom, _dKwLent, _dSeizedLand; private static int _dKwRoomN, _dKwLentN, _dDebtorN, _dSeizedN;
        internal static long LastKwLent, LastKwRoom; internal static int LastKwLentN, LastKwRoomN;
        private static int _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static int _importN = -1, _importBad;

        internal static void Reset()
        {
            _b.Clear(); _saved.Clear(); _warGar.Clear(); _garTarget.Clear(); _day = -1; _stumbles = 0; _err.Clear(); _importN = -1; _importBad = 0;
            CourtFull.Reset();   // E5/A2 (recenzja): potkniecia, bledy i zapas "wlasnego" miedzy kampaniami
            ZeroLast(); ClearDay();
            // recenzja C1 (C1-G4): uchwyty BK i obiekty poprzedniej sesji (jak LosersFlee.Reset, PopulationLaw.Reset "wpis 87") - BK tworzy przy kazdym
            // wczytaniu nowy PopulationManager kluczowany obiektami Settlement; stary menedzer nie zna nowych osad, wiec po wczytaniu innego zapisu w tym
            // samym procesie nikt nie bylby zwalniany (a dezercja gry z limitu jest wylaczona). BkResolve jest leniwe - podepnie sie do nowego menedzera.
            // Latki Harmony (_hooksTried, _harmony, _wired, _missing) zyja przez caly proces - zostaja.
            _bkTried = false; _popMgr = null; _getPopData = null; _fromSoldiers = null; _populated = null;
            _grain = null; _grainTried = false; _spawnUsed.Clear(); _spawnDay = -1;
            _watchClans.Clear(); _watchPoolCap = -1; _watchPoolMen = 0; _watchDetail = ""; _watchRel.Clear(); _watchRelSum = 0;   // 2: pula Strazy
        }

        internal static void ZeroLast()
        {
            LastCourt = LastCourtTown = LastFood = LastFamily = LastReleasedPurse = LastBuildShare = LastGearShare = 0;
            LastReleasedPurseVet = 0;
            LastReleasedMen = LastReleasedPar = LastReleasedGar = LastNoVillage = LastFamilyN = 0;
            LastReleasedWatch = LastReleasedSeized = 0; _dRelWatch = _dRelSeized = 0;
            LastKwLent = LastKwRoom = 0; LastKwLentN = LastKwRoomN = 0;
        }

        private static void ClearDay()
        {
            _dCourt = _dCourtTown = _dFood = _dFamily = _dRelPurse = _dBuild = _dGear = _dCapSum = _dWageSum = _dD = 0;
            _dRelMen = _dRelPar = _dRelGar = _dNoVillage = _dFamilyN = _dOver = _dStreak = _dPeace = _dWar = _dClans = _dPoor = _dVanished = 0;
            _dRelTavern = 0; _dRelPurseVet = 0;
            _dPzCap = _dPzChest = _dPzWage = 0; _dPzN = _dPzChestN = _dPzMenPar = _dPzMenGar = 0;   // Z.1
            for (int i = 0; i < 3; i++) { _dRelTier[i] = 0; _dRelTierWage[i] = 0; }   // P3/W2
            _dGarEatsCap = _dGarEatsCapEmpty = 0;
            _dGarLimited = _dPartyLimited = 0; _dWatchMen = 0; _dWatchCap = -1;
            _dCourtRoom = _dCourtYield = 0; _dCourtRoomN = _dCourtYieldN = _dPartyOverCap = 0;
            _dKwRoom = _dKwLent = _dSeizedLand = 0; _dKwRoomN = _dKwLentN = _dDebtorN = _dSeizedN = 0;
            CourtFull.ClearDay();   // E5/A2: liczniki doby sprzetu niewydanego -> dwor (takze Reset miedzy kampaniami)
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("ClanBudget." + where, e); } catch { }
        }

        // ------------------------------------------------------------ kto ma budzet
        private static bool Eligible(Clan c)
        {
            return c != null && c != Clan.PlayerClan && !c.IsEliminated && !c.IsBanditFaction && c.Leader != null && c.Leader.IsAlive && c.Kingdom != null
                   && (!c.IsUnderMercenaryService || MercContract.On) && c.StringId != null && !c.StringId.StartsWith("bk_courtiers_", StringComparison.Ordinal)
                   && !ClanIncomeBook.IsUndeadClan(c);
        }

        /// <summary>Rod z budzetem policzonym dzis (latki gry pytaja o to w rozliczeniu rodu, werbunku i dezercji).</summary>
        private static B Of(Clan c)
        {
            B b;
            return c != null && On && _b.TryGetValue(c, out b) && b.Today ? b : null;
        }

        private static Clan OwnerOf(MobileParty mp)
        {
            if (mp == null) return null;
            if (mp.IsGarrison) { var st = mp.CurrentSettlement ?? mp.HomeSettlement; return st != null ? st.OwnerClan : null; }
            return mp.ActualClan;
        }

        private static bool AtWar(Clan c)
        {
            var f = c.MapFaction;
            if (f == null) return false;
            var list = f.FactionsAtWarWith;
            if (list != null) for (int i = 0; i < list.Count; i++) { var o = list[i]; if (o != null && o.IsKingdomFaction) return true; }
            return false;
        }

        // ------------------------------------------------------------ jedna formula pulapu (gra i linia "Budzet rodow (na sucho)")
        /// <summary>Pulap zoldu rodu (partie + zalogi + karawany) i jego skladniki - ta sama formula dla budzetu w grze i dla przebiegu na sucho.</summary>
        internal static double Ceiling(double D, double G, bool war, int adults, out double R, out double f, out double chest)
        {
            var s = Settings.Current;
            D = Math.Max(0, D);
            R = Math.Max(Math.Max(0f, s.WarReserveFloor), Math.Max(0f, s.WarReserveDays) * D) + Math.Max(0f, s.WarReservePerAdult) * Math.Max(1, adults);
            chest = 0; f = 1;
            if (war)
            {
                if (G > R) chest = Math.Max(0f, s.WarChestToWages) * (G - R) / Math.Max(1f, s.WarChestDays);
                else if (R > 0)
                {
                    f = 0.8 + 0.2 * Math.Max(0, G) / R;
                    if (G < Math.Max(0f, s.PovertyDeepShare) * R) f = Math.Min(f, Math.Max(0f, s.PovertyDeepFactor));
                }
                return Math.Max(0f, s.WarWageShare) * D * f + chest;
            }
            double rp = Math.Max(0f, s.ReserveDaysPeace) * D;
            if (rp > 0 && G < rp) f = 1 - 0.1 * (1 - Math.Max(0, G) / rp);
            // Z.1 (bieg dwuletni 14.10, 132 rody z wojny do pokoju: ludzie w druzynach 38 125 -> 1 486, kiesy 26.4 -> 29.0 mln): w pokoju D spada do ok. 1/3
            // (bez zwrotu 70% zoldu i z mniejszymi rentami), a pulap 0.28 D zjadaly zalogi (do 80%) i karawany - na druzyny 0. Zloto rodu ponad rezerwe wojny
            // idzie w czesci na zold takze w pokoju: PeaceChestShare x skrzynia wojenna (ta sama formula, ta sama rezerwa R - kiesa nie schodzi przez to ponizej R).
            // Klucz 0 = pulap pokoju jak dotad (sam udzial D).
            if (G > R && s.PeaceChestShare > 0f) chest = Math.Max(0f, s.PeaceChestShare) * Math.Max(0f, s.WarChestToWages) * (G - R) / Math.Max(1f, s.WarChestDays);
            return Math.Max(0f, s.PeaceWageShare) * D * f + chest;
        }

        /// <summary>Pulap zoldu policzony dzis w grze (dla linii na sucho - jedna liczba); false - rod bez budzetu.</summary>
        internal static bool TryCap(Clan c, out double cap)
        {
            var b = Of(c); cap = b != null ? b.Cap : 0; return b != null;
        }

        /// <summary>P1 (pomiar obiegu kiesy ludu, TYLKO ODCZYT): zloto glow rodow z budzetem policzonym dzis wedlug wypelnienia pulapu zoldu - n / gold (5 pol):
        /// [0] na pulapie (zold partii + zalog + karawan >= 95% pulapu), [1] z luzem, [2] z luzem i w wojnie (czesc [1]), [3] bez pulapu (Straz bez zoldu albo
        /// pulap 0), [4] kompanie najemne z umowa 185 (osobno, poza [0]-[3]). Linia "Rody z luzem pulapu (P1)" - nowy zbiornik zlota po daninie z oszczednosci.</summary>
        internal static void SlackGold(int[] n, long[] gold)
        {
            if (n == null || gold == null || n.Length < 5 || gold.Length < 5) return;
            foreach (var kv in _b)
            {
                var c = kv.Key; var b = kv.Value;
                if (c == null || b == null || !b.Today || c.IsEliminated || c.Leader == null) continue;
                long g = Math.Max(0, c.Leader.Gold);
                // recenzja W5: pomiar na pulapie WLASNYM i zoldzie, ktory rod placi sam (bez przydzialu i gotowki korony) - rod z doplata zostaje "na pulapie",
                // a nie przechodzi do "z luzem" przez miejsce na werbunek dane przez korone; bez doplaty (Aid = 0) co do rodu jak dotad
                double capOwn = b.Cap - b.Aid;
                int i = c.IsUnderMercenaryService ? 4 : (b.Zero || !(capOwn > 0)) ? 3 : (b.WPar + b.WGar + b.WCar - b.AidCash >= 0.95 * capOwn ? 0 : 1);
                n[i]++; gold[i] += g;
                if (i == 1 && b.War) { n[2]++; gold[2] += g; }
            }
        }

        /// <summary>P3/W1: budzet wlaczony, ale w tej sesji jeszcze nie policzony (pierwsza doba po wczytaniu albo po starcie kampanii - CrownRents.Daily
        /// biegnie przed ClanBudget.Daily). Wtedy AtMeans nie ma na czym stanac i wolajacy trzyma stan z zapisu.</summary>
        internal static bool Pending { get { return On && _day < 0; } }

        /// <summary>P3/W1 (bieg roczny 364 doby, TRIAZ-3 O1-2 "Poprawka ostateczna" A) - ROD NA MIARE SRODKOW: zadna czynna druzyna rodu nie przekracza progu,
        /// od ktorego gra wzywa do armii (ArmyManagementCalculationModel.AIMobilePartySizeRatioToCallToArmy = 0.6 - ta sama liczba co w grze i w BK, bez
        /// nowego parametru), a pulap partii z ostatniego budzetu (pulap minus zalogi i karawany) nie starcza na oplacenie druzyn do tego progu:
        /// potrzeba = prog x suma limitow druzyn x zold na czlowieka (zold partii z rycerzami 179 / ludzie przy co najmniej 20 ludziach, inaczej srednia
        /// gry); rod bez druzyny - MinNewPartyMen (30) x zold na czlowieka, ta sama liczba co regula "nowa partia" 166. Budzet z wczoraj (CrownRents.Daily
        /// biegnie przed ClanBudget.Daily). false: rod bez budzetu (gracz, wylaczone 166, budzet jeszcze nie policzony), Straz bez zoldu, najemnik.
        /// fillMax - najwyzsze wypelnienie druzyny rodu (-1 bez druzyny) - pomiar do CSV, liczony zawsze. Tylko odczyt.</summary>
        internal static bool AtMeans(Clan c, out float fillMax)
        {
            fillMax = -1f;
            try
            {
                var s = Settings.Current;
                if (c == null || s == null || Campaign.Current == null) return false;
                long limit = 0, men = 0, wage = 0; int parties = 0;
                var wps = c.WarPartyComponents;
                if (wps != null)
                    for (int i = 0; i < wps.Count; i++)
                    {
                        var mp = wps[i] != null ? wps[i].MobileParty : null;
                        if (mp == null || !mp.IsActive || !mp.IsLordParty || mp.Party == null) continue;
                        float fill = mp.PartySizeRatio;
                        if (fill > fillMax) fillMax = fill;
                        parties++;
                        limit += Math.Max(0, mp.Party.PartySizeLimit);
                        men += mp.MemberRoster != null ? mp.MemberRoster.TotalRegulars : 0;
                        wage += Math.Max(0, mp.TotalWage) + GentryService.KnightWage(mp);
                    }
                var b = Of(c);
                if (b == null || b.Zero || c.IsUnderMercenaryService) return false;
                float gate = Campaign.Current.Models.ArmyManagementCalculationModel.AIMobilePartySizeRatioToCallToArmy;
                if (fillMax > gate) return false;                       // gra moze te druzyne wezwac - zwykly wymog sluzby
                double perMan = men >= 20 ? (double)wage / men : Math.Max(1f, Campaign.Current.AverageWage);
                double need = parties > 0 ? (double)gate * limit * perMan : Math.Max(0, s.MinNewPartyMen) * perMan;
                return b.PartyCap - b.Aid < need;   // W5: pulap WLASNY (bez doplaty korony) - rod z doplata zostaje "na miare srodkow" tak, jak bylby bez niej
            }
            catch (Exception e) { Stumble("AtMeans", e); return false; }
        }

        /// <summary>W5 (KingdomTreasury.NeedAidKingdom - krok korony biegnie przed ClanBudget.Daily, wiec budzet z wczoraj): pulap WLASNY druzyn rodu =
        /// pulap partii z ostatniego budzetu bez przydzialu korony (pulap z dworem ustepujacym zoldowi i kredytem wojennym, minus zalogi i karawany).
        /// false: rod bez budzetu policzonego (gracz, 166 wylaczone, pierwsza doba sesji), Straz bez zoldu, kompania najemna, rod w pokoju. Tylko odczyt.</summary>
        internal static bool PartyCapOwn(Clan c, out double partyCap)
        {
            partyCap = 0;
            try
            {
                var b = Of(c);
                if (b == null || b.Zero || !b.War || c.IsUnderMercenaryService) return false;
                partyCap = Math.Max(0, b.PartyCap - b.Aid);
                return true;
            }
            catch (Exception e) { Stumble("PartyCapOwn", e); return false; }
        }

        /// <summary>W6 (KingdomTreasury.Scutage.cs - krok korony biegnie przed ClanBudget.Daily, wiec budzet z wczoraj): pulap WLASNY druzyn rodu do podstawy
        /// tarczowego = pulap partii z ostatniego budzetu bez przydzialu korony (W5), bez udzialu dworu dopisanego do pulapu w wojnie (rod z luzem placi dwor
        /// w calosci - to nie jest zaoszczedzony zold) i bez miejsca kredytu wojennego; reserve = rezerwa wojenna R z tego samego budzetu. false: rod bez
        /// budzetu policzonego (gracz, 166 wylaczone, pierwsza doba sesji), Straz bez zoldu, kompania najemna, rod w pokoju, dluznik albo rod w zajeciu
        /// (168 - jego kiese bierze wierzyciel). Tylko odczyt.</summary>
        internal static bool ScutageCap(Clan c, out double partyCap, out double reserve)
        {
            partyCap = 0; reserve = 0;
            try
            {
                var b = Of(c);
                if (b == null || b.Zero || !b.War || c.IsUnderMercenaryService || b.Debtor || b.Seized) return false;
                partyCap = Math.Max(0, b.PartyCap - b.Aid - b.CourtRoom - b.KwRoom);
                reserve = b.R;
                return true;
            }
            catch (Exception e) { Stumble("ScutageCap", e); return false; }
        }

        /// <summary>W6b (decyzja Jeffa 13.10: rod gracza w tarczowym W6 i doplacie W5, "gdy mam ziemie i sluze krolowi") - POMIAR rodu gracza TYM SAMYM wzorem pulapu
        /// co rod AI (Ceiling: D staly z ksiegi dochodu - ksiega liczy go tez graczowi; pomiar krotszy niz 28 dob mieszany jak u AI, tylko "D z zapisu" to D169 ksiegi
        /// (pierscien 28 dob jest w zapisie), bez niego G/60; G = kiesy rodziny; R z doroslych; wojna jak w budzecie), TYLKO jako miara do W5 / W6. Gracz nie ma
        /// budzetu 166: nic tu nie zmienia stanu gry, limitow partii ani werbunku. capW5 = pulap wlasny druzyn jak PartyCapOwn AI (pulap + udzial dworu
        /// ustepujacy zoldowi, minus zold zalog i karawan - zalogi w calosci, bo gracz sam ustawia ich limity; kredytu wojennego 168 gracz nie ma);
        /// capW6 = jak ScutageCap AI (bez udzialu dworu); reserve = R. Liczony w chwili kroku W5 (AI - z wczorajszego budzetu; gracz budzetu z wczoraj nie ma).
        /// false: 166 wylaczone, budzet jeszcze nie policzony w tej sesji (jak AI - od jutra), rod nie w wojnie, Straz bez zoldu. Tylko odczyt.</summary>
        internal static bool PlayerCaps(Clan c, out double capW5, out double capW6, out double reserve, out double D)
        {
            capW5 = 0; capW6 = 0; reserve = 0; D = 0;
            try
            {
                var s = Settings.Current;
                if (s == null || c == null || c != Clan.PlayerClan || !On || Pending || c.Leader == null || c.Kingdom == null || !AtWar(c)) return false;
                if (CrownGifts.WatchUnpaidOn && s.UnpaidTroopsCapInMen && CrownGifts.IsWatch(c.Kingdom)) return false;   // Straz bez zoldu - jak b.Zero u AI
                long G = ClanIncomeBook.FamilyGoldOf(c);
                int adults = AdultsOf(c);
                // D: ta sama droga co DOf rodu AI; "D z zapisu" (D0) = D169 ksiegi dochodu (zapisana), bez niej G/60 (start 166)
                double d0 = ClanIncomeBook.StableIncome(c);
                if (!(d0 >= 0)) d0 = Math.Max(0, G / 60.0);
                double d; int days; double once;
                if (s.ClanBudgetStableD && ClanIncomeBook.StableDWithDays(c, out d, out days, out once))
                {
                    if (Array.IndexOf(Raiders, c.Kingdom.StringId) >= 0) d += once;   // najezdzcy: lup w D (wyjatek lore W-1, jak AI)
                    days = Math.Max(0, Math.Min(Days28, days));
                    D = (days * d + (Days28 - days) * d0) / Days28;
                }
                else D = d0;
                D = Math.Max(0, D);
                // 168: rod w zajeciu - D bez dochodu wsi (jak AI; W5 i W6 i tak go pomijaja)
                if (DebtLadder.On && DebtLadder.IsSeized(c)) D = Math.Max(0, D - Math.Min(D, ClanIncomeBook.VillageIncomeOf(c)));
                double R, f, chest;
                double cap = Ceiling(D, G, true, adults, out R, out f, out chest);
                // udzial dworu ustepujacy zoldowi (jak AI w wojnie): dwor i wyzywienie minus jedzenie partii, gdy jest siedziba z kasa
                double court = 0;
                if (s.HouseholdMinimal && s.WarCourtYieldsToWages)
                {
                    try
                    {
                        var seat = Seat(c);
                        double hh = s.HouseholdShareWar * D * f;
                        if (DebtLadder.On && DebtLadder.IsDebtor(c)) hh *= 0.5;   // recenzja W6b: dluznik (168) - dwor -50%, jak b.Household u AI (u gracza tylko z PlayerSameLadder)
                        if (seat != null && seat.Town != null) court = Math.Max(0, hh - PartyFood(c, seat));
                    }
                    catch (Exception e) { court = 0; Stumble("PlayerCaps(dwor)", e); }
                }
                // zold zalog (twierdze rodu) i karawan rodu - ta sama miara co przejscie po partiach w Daily
                double gar = 0, car = 0;
                var fiefs = c.Fiefs;
                if (fiefs != null)
                    for (int i = 0; i < fiefs.Count; i++)
                    {
                        var gp = fiefs[i] != null ? fiefs[i].GarrisonParty : null;
                        if (gp != null && gp.IsActive) gar += Math.Max(0, gp.TotalWage);
                    }
                foreach (var mp in MobileParty.All)
                {
                    if (mp == null || !mp.IsActive || !mp.IsCaravan || mp.IsMainParty || OwnerOf(mp) != c) continue;
                    car += Math.Max(0, mp.TotalWage);
                }
                capW6 = Math.Max(0, cap - gar - car);
                capW5 = Math.Max(0, cap + court - gar - car);
                reserve = R;
                return true;
            }
            catch (Exception e) { Stumble("PlayerCaps", e); return false; }
        }

        internal static int AdultsOf(Clan c)
        {
            int n = 0; bool leader = false;
            var lords = c.AliveLords;
            if (lords != null) for (int i = 0; i < lords.Count; i++) { var h = lords[i]; if (h == null || h.IsChild) continue; n++; if (h == c.Leader) leader = true; }
            if (!leader && c.Leader != null) n++;
            return Math.Max(1, n);
        }

        private static double DOf(Clan c, B b, long G, Settings s)
        {
            if (b.D0 < 0)
            {
                double[] sv;
                b.D0 = _saved.TryGetValue(c.StringId, out sv) && sv[0] >= 0 ? sv[0] : Math.Max(0, G / 60.0);   // start: D(0) = G/60 (projekt 166)
                if (sv != null) { b.Streak = (int)sv[1]; b.GearLeft = sv[2]; _saved.Remove(c.StringId); }
            }
            double d; int days; double once;
            if (s.ClanBudgetStableD && ClanIncomeBook.StableDWithDays(c, out d, out days, out once))
            {
                if (c.Kingdom != null && Array.IndexOf(Raiders, c.Kingdom.StringId) >= 0) d += once;   // najezdzcy: lup w D (wyjatek lore W-1)
                days = Math.Max(0, Math.Min(Days28, days));
                return (days * d + (Days28 - days) * b.D0) / Days28;   // pomiar krotszy niz 28 dob - reszta z D z zapisu (albo G/60)
            }
            double d169 = ClanIncomeBook.StableIncome(c);
            return d169 >= 0 ? d169 : b.D0;
        }

        // ------------------------------------------------------------ raz na dobe
        internal static void Daily()
        {
            ClearDay();
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            if (!On) { _b.Clear(); return; }
            try
            {
                EnsureHooks();
                int today = (int)CampaignTime.Now.ToDays;
                _day = today;
                foreach (var kv in _b) kv.Value.Today = false;
                _garTarget.Clear();
                // jedno przejscie po partiach: zold i ludzie partii, zalog i karawan na rod
                var wPar = new Dictionary<Clan, long>(); var wGar = new Dictionary<Clan, long>(); var wCar = new Dictionary<Clan, long>();
                var mPar = new Dictionary<Clan, int>(); var mGar = new Dictionary<Clan, int>();
                var nomSum = new Dictionary<Clan, long>(); var nomCache = new Dictionary<CharacterObject, int>();
                foreach (var mp in MobileParty.All)
                {
                    try
                    {
                        if (mp == null || !mp.IsActive || mp.IsMainParty) continue;
                        Clan c = OwnerOf(mp);
                        if (c == null || c == Clan.PlayerClan) continue;
                        if (mp.IsCaravan) { long v; wCar.TryGetValue(c, out v); wCar[c] = v + Math.Max(0, mp.TotalWage); continue; }
                        if (!mp.IsLordParty && !mp.IsGarrison) continue;
                        int wage = Math.Max(0, mp.TotalWage), men = mp.MemberRoster != null ? mp.MemberRoster.TotalRegulars : 0;
                        if (!mp.IsGarrison) wage += GentryService.KnightWage(mp);   // 179: zold rycerzy w druzynie - w pulapie partii pana
                        var dw = mp.IsGarrison ? wGar : wPar; var dm = mp.IsGarrison ? mGar : mPar;
                        long w0; dw.TryGetValue(c, out w0); dw[c] = w0 + wage;
                        int m0; dm.TryGetValue(c, out m0); dm[c] = m0 + men;
                        if (CrownGifts.WatchUnpaidOn && CrownGifts.IsWatchParty(mp) && mp.MemberRoster != null)
                        {
                            long ns = 0; var r = mp.MemberRoster;
                            for (int i = 0; i < r.Count; i++)
                            {
                                var el = r.GetElementCopyAtIndex(i); var ch = el.Character;
                                if (ch == null || ch.IsHero || el.Number <= 0) continue;
                                int nw; if (!nomCache.TryGetValue(ch, out nw)) { nw = MountedWage.Nominal(ch, mp); nomCache[ch] = nw; }
                                ns += (long)nw * el.Number;
                            }
                            long n0; nomSum.TryGetValue(c, out n0); nomSum[c] = n0 + ns;
                        }
                    }
                    catch (Exception e) { Stumble("Daily(partia)", e); }
                }
                var drop = new List<Clan>();
                var watchToday = new List<Clan>();   // 2: rody Strazy rozliczone dzis (pula)
                double watchNomAvg = -1;             // recenzja 2 (R2a): sredni nominal calej Strazy, liczony raz, gdy potrzebny
                foreach (var c in Clan.All)
                {
                    try
                    {
                        if (!Eligible(c)) continue;
                        B b;
                        if (!_b.TryGetValue(c, out b)) { b = new B(); _b[c] = b; }
                        long G = ClanIncomeBook.FamilyGoldOf(c);
                        b.G = G; b.Adults = AdultsOf(c); b.War = AtWar(c);
                        b.D = Math.Max(0, DOf(c, b, G, s));
                        // 168: rod w zajeciu (D3/D4) - dochod wsi bierze wierzyciel, wiec D budzetu liczy ziemie bez niego (poczet sam maleje - zwolnienia);
                        // dluznik (dlug w Banku, dlug zoldu, zaleglosc albo zajecie) - dwor -50%, budowy 0 (nizej)
                        b.Seized = false; b.SeizedLand = 0; b.Debtor = false;
                        if (DebtLadder.On)
                        {
                            try
                            {
                                if (DebtLadder.IsSeized(c)) { b.Seized = true; b.SeizedLand = Math.Min(b.D, ClanIncomeBook.VillageIncomeOf(c)); b.D = Math.Max(0, b.D - b.SeizedLand); _dSeizedN++; _dSeizedLand += (long)b.SeizedLand; }
                                b.Debtor = DebtLadder.IsDebtor(c);
                            }
                            catch (Exception e) { Stumble("Ladder", e); }
                        }
                        double R, f, chest;
                        b.Cap = Ceiling(b.D, G, b.War, b.Adults, out R, out f, out chest);
                        b.R = R; b.F = f;
                        bool merc = c.IsUnderMercenaryService;
                        double mcap = 0;
                        if (merc && !MercContract.CapOf(c, b.War, out mcap)) { b.Today = false; continue; }   // najemnik bez umowy (185) - bez budzetu
                        double shareF = f;   // te same mnozniki biedy dla wszystkich udzialow
                        b.Household = (b.War ? s.HouseholdShareWar : s.HouseholdSharePeace) * b.D * shareF;
                        b.Gear = (b.War ? s.GearShareWar : s.GearSharePeace) * b.D * shareF + (b.War && G > R ? 0.2 * (G - R) / Math.Max(1f, s.WarChestDays) : 0);
                        // recenzja C1 (W5): budowy takze w wojnie (odstepstwo od tabeli 166 "0") - Jeff 05.10 "w wojnie 0, chyba ze mury - tak"; BuildFunding
                        // w wojnie i tak odcina budowy cywilne, wiec wojenny przydzial idzie tylko na mury, wieze i koszary. Dopelnienie ponad 120 D - tylko w pokoju.
                        b.Build = Math.Max(0f, s.BuildIncomeShare) * b.D * shareF;
                        if (merc) { b.Cap = mcap; b.Household = 0; b.Build = 0; f = 1; }   // 185: pulap najemnika = zold ludzi z umowy (nie udzial D), bez dworu
                        else if (!b.War)
                        {
                            // reszta ponad 120 D + 50 000 po 1/180 na dwor i budowy (polowa na polowe)
                            double cap = Math.Max(0f, s.ReserveCapDays) * b.D + 50000;
                            if (G > cap) { double x = (G - cap) / 180.0; b.Household += x / 2; b.Build += x / 2; }
                        }
                        // 168 (166 "Dluznik"): rod w dlugu - budowy 0, dwor -50%; sprzet i tak tylko braki (pan placi tylko TryBuyCore - braki, lepsze tylko z sakiewki ludzi)
                        if (b.Debtor && !merc) { b.Household *= 0.5; b.Build = 0; _dDebtorN++; }
                        // test 120 dob C1 (wojsko w wojnie -20%): pulap wiazal u ok. 97 rodow w wojnie (zold >= 0.9 pulapu; 73% braku ludzi), a dwor 0.20 D
                        // szedl do kasy siedziby mimo to (ok. 40 tys./dobe u tych rodow) - w wojnie DWOR USTEPUJE ZOLDOWI: udzial dworu bez jedzenia partii
                        // dochodzi do pulapu, a dwor dostaje tylko to, czego zold z niego nie zajal (Court). Rod z luzem placi dwor w calosci jak dotad.
                        // Zloto nie powstaje: zostaje w kiesie glowy i idzie na zold (licznik "dwor ustapil zoldowi"). Jedzenie i siedziba - raz na dobe tutaj.
                        b.CapBase = b.Cap; b.CourtRoom = 0; b.Food = 0; b.Seat = null;
                        if (s.HouseholdMinimal)
                        {
                            try
                            {
                                var seat = Seat(c);
                                if (seat != null && seat.Town != null) { b.Seat = seat; b.Food = PartyFood(c, seat); }
                            }
                            catch (Exception e) { b.Seat = null; b.Food = 0; Stumble("Seat", e); }
                            if (b.War && !merc && s.WarCourtYieldsToWages && b.Seat != null)
                            {
                                b.CourtRoom = Math.Max(0, b.Household - b.Food);
                                b.Cap += b.CourtRoom;
                                if (b.CourtRoom > 0) { _dCourtRoom += (long)b.CourtRoom; _dCourtRoomN++; }
                            }
                        }
                        long v;
                        wPar.TryGetValue(c, out v); b.WPar = v; wGar.TryGetValue(c, out v); b.WGar = v; wCar.TryGetValue(c, out v); b.WCar = v;
                        int m; mPar.TryGetValue(c, out m); b.MenPar = m; mGar.TryGetValue(c, out m); b.MenGar = m;
                        // Straz bez zoldu: pulap w ludziach (pulap zoldu / nominalny zold czlowieka)
                        b.Zero = CrownGifts.WatchUnpaidOn && s.UnpaidTroopsCapInMen && CrownGifts.IsWatch(c.Kingdom);
                        if (b.Zero)
                        {
                            long ns; nomSum.TryGetValue(c, out ns);
                            int men = b.MenPar + b.MenGar;
                            // recenzja 2 (R2a): rod Strazy bez ludzi - sredni nominal calej Strazy (nie srednia gry), bo jego miejsce w puli wypelniaja ludzie innych rodow
                            if (men > 0 && ns > 0) b.Nominal = (double)ns / men;
                            else
                            {
                                if (watchNomAvg < 0)
                                {
                                    long nsAll = 0; int menAll = 0;
                                    foreach (var kv in nomSum)
                                    {
                                        if (kv.Key == null || kv.Value <= 0) continue;
                                        nsAll += kv.Value; int m1; mPar.TryGetValue(kv.Key, out m1); int m2; mGar.TryGetValue(kv.Key, out m2); menAll += m1 + m2;
                                    }
                                    watchNomAvg = menAll > 0 && nsAll > 0 ? (double)nsAll / menAll : 0;
                                }
                                b.Nominal = watchNomAvg > 0 ? watchNomAvg : Math.Max(1f, Campaign.Current.AverageWage);
                            }
                            // recenzja 2 (R2b): bez udzialu dworu w wojnie - Straz nie ma zoldu, wiec "dwor ustepuje zoldowi" nigdy nie zachodzi i rod placi dwor w calosci;
                            // ta sama kwota nie moze byc jeszcze miejscem dla ludzi w puli
                            b.MenCap = (int)(Math.Max(0, b.Cap - b.CourtRoom) / Math.Max(0.5, b.Nominal));
                            _dWatchMen += men; _dWatchCap = Math.Max(0, _dWatchCap) + b.MenCap;
                        }
                        else b.MenCap = -1;
                        // 168: KREDYT WOJENNY wpiety w pulap - w wojnie rod z G < R (bez zaleglosci, zajecia, z wolnym kapitalem Banku) moze isc ponad pulap o najwyzej
                        // 0.40 D dziennie w granicy limitu; Bank wyplaca dzis do kiesy glowy brak do zoldu naliczonego ponad pulap bez kredytu (kapital Banku -> glowa)
                        b.CapNoKw = b.Cap; b.KwRoom = 0; b.KwLent = 0;
                        if (DebtLadder.On && !merc && !b.Zero)
                        {
                            try
                            {
                                double kw = DebtLadder.WarCreditRoom(c, b.D, b.G, b.R, b.War, b.Household);
                                if (kw > 0)
                                {
                                    b.KwRoom = kw; b.Cap += kw; _dKwRoom += (long)kw; _dKwRoomN++;
                                    double gap = (b.WPar + b.WGar + b.WCar) - b.CapNoKw;
                                    if (gap >= 1)
                                    {
                                        int lent = DebtLadder.LendWar(c, (int)Math.Min(gap, kw));
                                        if (lent > 0) { b.KwLent = lent; _dKwLent += lent; _dKwLentN++; }
                                    }
                                }
                            }
                            catch (Exception e) { Stumble("WarCredit", e); }
                        }
                        Garrisons(c, b, s);
                        b.PartyCap = Math.Max(0, b.Cap - (GarFull(b, s) ? b.WGar : Math.Min(b.WGar, b.GarTarget)) - b.WCar);
                        if (!b.Zero && b.WPar > b.PartyCap) _dPartyOverCap++;   // rody ponad pulapem partii (limity proporcjonalnie do zoldu)
                        // P3/W2 (pomiar): rod w wojnie, ktoremu zalogi i karawany zajely caly pulap - partie zwalniane do zera, kolejnosc zwolnien nic tu nie zmienia
                        if (!b.Zero && !merc && b.War && b.PartyCap <= 0) { _dGarEatsCap++; if (b.MenPar <= 0) _dGarEatsCapEmpty++; }
                        // W5 (doplata korony do zoldu wedlug potrzeby, KingdomTreasury.NeedAid.cs): przydzial tej doby jest ZNACZONY na druzyny - podnosi pulap partii
                        // (takze u rodu, ktoremu zalogi zajely caly pulap: druzyny dostaja dokladnie przydzial) i pulap rodu o te sama kwote. Nie wchodzi do D, wiec
                        // dwor, sprzet, budowy, kredyt wojenny i limit Banku licza sie jak bez doplaty. Liczniki wyzej (ponad pulapem partii, zalogi zajely pulap) -
                        // wedlug pulapu wlasnego, jak dotad. Klucz wylaczony albo rod bez przydzialu = 0 i budzet co do zlotowki jak przed paczka.
                        // Recenzja W5: AidCash = gotowka korony w tym przydziale (zold, ktorego rod nie placi sam) - Court, CourtFull.Take i SlackGold odejmuja ja od zoldu rodu.
                        b.Aid = 0; b.AidCash = 0;
                        if (!b.Zero && !merc && b.War)
                        {
                            // W7 (KingdomTreasury.LordLevy.cs): pobor stawki wojennej pana z wlasnych lenn - ten sam przydzial znaczony na druzyny (podniesienie
                            // pulapu ponad W5 i gotowka za ludzi, ktorzy sa); klucz LordWarLevy wylaczony albo rod bez poboru = 0 i budzet jak przed paczka
                            double aid = KingdomTreasury.NeedAidBoostOf(c) + KingdomTreasury.LordLevyBoostOf(c);
                            if (aid > 0) { b.Aid = aid; b.PartyCap += aid; b.Cap += aid; b.AidCash = Math.Max(0, Math.Min(Math.Min(aid, (double)b.WPar), (double)(KingdomTreasury.NeedAidCashOf(c) + KingdomTreasury.LordLevyCashOf(c)))); }
                        }
                        b.Today = true;
                        // przydzial sprzetu: niewydany z dni od ostatnich zakupow, najwyzej AiGearDaysCap dni
                        CourtFull.Lapse(b, s, merc);   // E5/A2 (R8): przydzial przeterminowany dzis -> dwor (b.Lapsed; wlasny try, GearLeft bez zmian)
                        b.GearLeft = Math.Min(b.GearLeft + b.Gear, Math.Max(1f, s.AiGearDaysCap) * b.Gear);
                        _dGear += (long)b.Gear; _dBuild += (long)b.Build;
                        _dClans++; if (b.War) _dWar++; else _dPeace++;
                        _dCapSum += (long)b.Cap; _dWageSum += b.WPar + b.WGar + b.WCar; _dD += (long)b.D;
                        if (!b.War && !merc)   // Z.1 (pomiar): rody w pokoju - pulap z ewentualna skrzynia pokoju (chest z Ceiling; w pokoju bez kredytu wojennego i doplat)
                        {
                            _dPzN++; _dPzCap += (long)b.Cap; _dPzWage += b.WPar + b.WGar + b.WCar; _dPzMenPar += b.MenPar; _dPzMenGar += b.MenGar;
                            if (chest > 0) { _dPzChest += (long)chest; _dPzChestN++; }
                        }
                        // kiesa rodziny, dwor, zwolnienia, limity - kazde we wlasnym try
                        try { FamilyTopUp(c, b, s); } catch (Exception e) { Stumble("FamilyTopUp", e); }
                        try { Court(c, b, s); } catch (Exception e) { Stumble("Court", e); }
                        // recenzja C1 (W3): limity przed zwolnieniami - zwolnienia dziela kwote wedlug nadwyzki partii ponad jej dzisiejszy limit
                        try { ApplyPartyLimits(c, b); ApplyGarrisonLimits(c, b); } catch (Exception e) { Stumble("Limits", e); }
                        if (b.Zero) watchToday.Add(c);   // 2: Straz - zwolnienia po petli, wedlug puli calej Strazy
                        else { try { Releases(c, b, s); } catch (Exception e) { Stumble("Releases", e); } }
                        if (c.Leader.Gold < 5000) _dPoor++;
                    }
                    catch (Exception e) { Stumble("Daily(rod)", e); }
                }
                // 2: pula Strazy (suma pulapow i ludzi rodow Strazy z dzisiejszego rozliczenia), potem zwolnienia rodow Strazy wedlug puli
                try
                {
                    _watchClans.Clear(); _watchClans.AddRange(watchToday);
                    _watchPoolCap = watchToday.Count > 0 ? Math.Max(0, _dWatchCap) : -1; _watchPoolMen = _dWatchMen;
                    _watchRel.Clear(); _watchRelSum = 0;
                    foreach (var wc in watchToday) { int r = ReleasableMen(wc); _watchRel[wc] = r; _watchRelSum += r; }
                    var wd = new System.Text.StringBuilder();
                    foreach (var wc in watchToday)
                    {
                        B wb; if (!_b.TryGetValue(wc, out wb)) continue;
                        if (wd.Length > 0) wd.Append(", ");
                        wd.Append(wc.Name).Append(' ').Append(wb.MenPar + wb.MenGar).Append('/').Append(wb.MenCap);
                        try { Releases(wc, wb, s); } catch (Exception e) { Stumble("Releases", e); }
                    }
                    _watchDetail = wd.ToString();
                }
                catch (Exception e) { Stumble("WatchPool", e); }
                foreach (var kv in _b) if (!kv.Value.Today && (kv.Key == null || kv.Key.IsEliminated)) drop.Add(kv.Key);
                foreach (var c in drop) _b.Remove(c);
                Report(s, today);
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally
            {
                LastCourt = _dCourt; LastCourtTown = _dCourtTown; LastFood = _dFood; LastFamily = _dFamily; LastReleasedPurse = _dRelPurse;
                LastReleasedPurseVet = _dRelPurseVet;
                LastBuildShare = _dBuild; LastGearShare = _dGear; LastReleasedMen = _dRelMen; LastReleasedPar = _dRelPar; LastReleasedGar = _dRelGar;
                LastReleasedWatch = _dRelWatch; LastReleasedSeized = _dRelSeized; _dRelWatch = _dRelSeized = 0;
                LastNoVillage = _dNoVillage; LastFamilyN = _dFamilyN;
                LastKwLent = _dKwLent; LastKwLentN = _dKwLentN; LastKwRoom = _dKwRoom; LastKwRoomN = _dKwRoomN;
            }
        }

        // ------------------------------------------------------------ zalogi: cel pokojowy (50% zalogi wojennej, najwyzej 80% pulapu)
        /// <summary>Zalogi bez celu budzetu: w wojnie (GarrisonWarFull - pulap gry, finansowane pierwsze) i u Strazy bez zoldu (limit w zlocie nic nie znaczy).</summary>
        private static bool GarFull(B b, Settings s) { return b.Zero || (b.War && s.GarrisonWarFull); }

        private static void Garrisons(Clan c, B b, Settings s)
        {
            b.GarTarget = 0;
            bool full = GarFull(b, s);
            var fiefs = c.Fiefs;
            if (fiefs == null) return;
            var tgt = new List<KeyValuePair<Settlement, double>>();
            double sum = 0;
            for (int i = 0; i < fiefs.Count; i++)
            {
                var f = fiefs[i];
                var st = f != null ? f.Settlement : null;
                if (st == null || st.StringId == null) continue;
                var gp = f.GarrisonParty;
                float cur = gp != null && gp.IsActive ? Math.Max(0, gp.TotalWage) : 0;
                float wr;
                if (!_warGar.TryGetValue(st.StringId, out wr)) { wr = cur; _warGar[st.StringId] = wr; }   // przed pierwsza wojna - stan z pierwszej doby
                else if (b.War) { wr += (cur - wr) / Days28; _warGar[st.StringId] = wr; }                 // srednia z dob wojny (28 dob)
                if (full) continue;
                double t = Math.Max(0f, s.GarrisonPeaceShare) * wr;
                tgt.Add(new KeyValuePair<Settlement, double>(st, t)); sum += t;
            }
            if (full || tgt.Count == 0) return;
            double lim = Math.Max(0f, s.GarrisonMaxShareOfBudgetPeace) * b.Cap;
            double k = sum > lim && sum > 0 ? lim / sum : 1.0;
            foreach (var kv in tgt) { int t = (int)(kv.Value * k); _garTarget[kv.Key] = t; b.GarTarget += t; }
        }

        // ------------------------------------------------------------ limity zoldu partii i zalog (stosowane tez w rozliczeniu rodu - postfiksy)
        private static void ApplyPartyLimits(Clan c, B b)
        {
            if (GentryService.HoldParties(c)) return;   // 179: rod rycerza bez wlasnych druzyn - stare partie z limitem 0 (bez rekrutow), BK rozwiaze je w majatku
            var wps = c.WarPartyComponents;
            if (wps == null || wps.Count == 0) return;
            int max = Campaign.Current.Models.PartyWageModel.MaxWagePaymentLimit;
            double wsum = 0, wage = 0;
            for (int i = 0; i < wps.Count; i++)
            {
                var mp = wps[i] != null ? wps[i].MobileParty : null;
                if (mp == null || !mp.IsLordParty) continue;
                wsum += mp.LeaderHero == c.Leader ? 1.5 : 1.0;
                wage += Math.Max(0, mp.TotalWage) + GentryService.KnightWage(mp);   // 179: zold rycerzy w druzynie (gra go nie liczy w TotalWage)
            }
            if (wsum <= 0) return;
            // test 120 dob (wojsko w wojnie): staly podzial 1.5 : 1 zostawial luz rodu niewykorzystany - partia ponad swoja czescia (zwykle druzyna glowy,
            // najwieksza i najdrozsza) nie werbowala i nie awansowala, a gra uznawala ja za "pelna" (PaymentLimit / AverageWage) i zostawiala ludzi w zalodze,
            // choc rod mial miejsce w pulapie. Teraz: rod ponizej pulapu partii - kazda partia ma swoj zold + swoja czesc wolnego miejsca (glowa 1.5, inni 1),
            // suma limitow = pulap partii; rod ponad pulapem - limity proporcjonalnie do zoldu (zwolnienia W3 rozkladaja sie wtedy wedlug zoldu partii).
            var s = Settings.Current;
            bool free = s != null && s.PartyLimitsShareFreeRoom;
            double room = b.PartyCap - wage;
            for (int i = 0; i < wps.Count; i++)
            {
                var mp = wps[i] != null ? wps[i].MobileParty : null;
                if (mp == null || !mp.IsLordParty) continue;
                int lim;
                if (b.Zero) lim = max;   // Straz bez zoldu: limit w zlocie nic nie znaczy - pulap w ludziach (werbunek)
                else
                {
                    double w = mp.LeaderHero == c.Leader ? 1.5 : 1.0;
                    int kw = GentryService.KnightWage(mp);                     // 179: limit gry liczy tylko zold ludzi - zold rycerzy schodzi z czesci partii
                    double pw = Math.Max(0, mp.TotalWage) + kw;
                    double x;
                    if (!free) x = b.PartyCap * w / wsum;                                                     // stary podzial (wylacznik)
                    else if (room >= 0) x = pw + room * w / wsum;                                             // swoj zold + czesc wolnego miejsca rodu
                    else x = wage > 0 ? b.PartyCap * pw / wage : b.PartyCap * w / wsum;                       // rod ponad pulapem partii
                    x -= kw;
                    lim = x >= max ? max : (int)Math.Max(0, x);
                }
                if (mp.PaymentLimit != lim) { mp.SetWagePaymentLimit(lim); _dPartyLimited++; }
            }
        }

        private static void ApplyGarrisonLimits(Clan c, B b)
        {
            if (_garTarget.Count == 0 || GarFull(b, Settings.Current)) return;   // w wojnie pulap zalog jak w grze (GarrisonWarFull); Straz - pulap w ludziach
            var fiefs = c.Fiefs;
            if (fiefs == null) return;
            for (int i = 0; i < fiefs.Count; i++)
            {
                var st = fiefs[i] != null ? fiefs[i].Settlement : null;
                int t;
                if (st == null || !_garTarget.TryGetValue(st, out t)) continue;
                if (st.GarrisonWagePaymentLimit > t) { st.SetGarrisonWagePaymentLimit(t); _dGarLimited++; }
            }
        }

        // ------------------------------------------------------------ kiesa rodziny dopelnia glowe
        private static void FamilyTopUp(Clan c, B b, Settings s)
        {
            if (!s.FamilyTopsUpHead) return;
            var head = c.Leader;
            int floor = Math.Max(0, s.FamilyPurseFloor);
            long dayCost = b.WPar + b.WGar + b.WCar + (long)b.Household;
            long target = Math.Max(floor, dayCost);
            if (head.Gold >= target) return;
            long need = target - head.Gold;
            foreach (var h in c.Heroes)
            {
                if (need <= 0) break;
                if (h == null || h == head || h == Hero.MainHero || !h.IsAlive || h.IsChild || h.Clan != c) continue;
                int spare = h.Gold - floor;
                if (spare <= 0) continue;
                int give = (int)Math.Min(spare, need);
                int before = head.Gold;
                TaleWorlds.CampaignSystem.Actions.GiveGoldAction.ApplyBetweenCharacters(h, head, give, true);
                give = Math.Max(0, head.Gold - before);
                need -= give; _dFamily += give; if (give > 0) _dFamilyN++;
            }
        }

        // ------------------------------------------------------------ 162m dwor: glowa -> kasa siedziby
        private static ItemObject _grain; private static bool _grainTried;

        internal static Settlement Seat(Clan c)   // 3.1/3.3 (31b) E3/8b: internal - kasa siedziby dla oplat dworu BK
        {
            var st = c.HomeSettlement;
            if (st != null && st.IsVillage && st.Village != null) st = st.Village.Bound;
            if (st != null && (st.IsTown || st.IsCastle) && st.Town != null) return st;
            var head = c.Leader;
            var here = head != null ? head.CurrentSettlement : null;
            if (here != null && here.IsTown && here.Town != null) return here;
            // najblizsze miasto krolestwa (od glowy albo od rodu)
            var k = c.Kingdom;
            if (k == null) return null;
            Settlement best = null; float bd = float.MaxValue;
            TaleWorlds.Library.Vec2 pos;
            try { pos = head != null && head.PartyBelongedTo != null ? head.PartyBelongedTo.GetPosition2D : (head != null && head.CurrentSettlement != null ? head.CurrentSettlement.GetPosition2D : TaleWorlds.Library.Vec2.Invalid); }
            catch { pos = TaleWorlds.Library.Vec2.Invalid; }
            foreach (var f in k.Fiefs)
            {
                var t = f != null ? f.Settlement : null;
                if (t == null || !t.IsTown) continue;
                float d = pos.IsValid ? pos.DistanceSquared(t.GetPosition2D) : 0f;
                if (best == null || d < bd) { best = t; bd = d; }
            }
            return best;
        }

        /// <summary>Jedzenie partii rodu (szacunek): zuzycie dnia x cena zboza w siedzibie - partie kupuja je same, wiec odejmujemy je od udzialu dworu.</summary>
        private static double PartyFood(Clan c, Settlement seat)
        {
            if (!_grainTried) { _grainTried = true; try { _grain = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemObject>("grain"); } catch { _grain = null; } }
            double food = 0;
            var wps = c.WarPartyComponents;
            if (wps != null)
            {
                int price = 0;
                try { price = _grain != null ? (seat.IsTown ? seat.Town.GetItemPrice(_grain, null, false) : _grain.Value) : 0; } catch { price = _grain != null ? _grain.Value : 0; }
                for (int i = 0; i < wps.Count; i++)
                {
                    var mp = wps[i] != null ? wps[i].MobileParty : null;
                    if (mp == null || !mp.IsActive) continue;
                    try { food += Math.Max(0f, -mp.FoodChange) * price; } catch { }
                }
            }
            return food;
        }

        private static void Court(Clan c, B b, Settings s)
        {
            if (!s.HouseholdMinimal) return;
            var head = c.Leader;
            var seat = b.Seat;   // siedziba i jedzenie partii policzone raz na dobe w Daily (przed pulapem - dwor ustepujacy zoldowi)
            if (seat == null || seat.Town == null) return;
            double food = b.Food;
            double share = Math.Max(0, b.Household - food);
            // test 120 dob (wojsko w wojnie): w wojnie udzial dworu jest czescia pulapu (CourtRoom) - dwor dostaje tylko to, czego zold rodu ponad pulap bez
            // dworu (CapBase) nie zajal; Straz bez zoldu - zold nominalny (ten sam, ktorym liczony jest pulap w ludziach)
            if (b.CourtRoom > 0 && share > 0)
            {
                // recenzja W5: zold, ktory rod placi SAM - bez czesci oplaconej dzis gotowka korony (b.AidCash; 0 bez doplaty i przy wylaczonym kluczu)
                double wageNow = b.Zero ? b.Nominal * (b.MenPar + b.MenGar) + b.WCar : b.WPar + b.WGar + b.WCar - b.AidCash;
                double used = Math.Min(share, Math.Max(0, wageNow - b.CapBase));
                if (used > 0) { share -= used; _dCourtYield += (long)used; _dCourtYieldN++; }
            }
            double court = share;                                  // E5/A2: sam dwor (po ustapieniu zoldowi) - placony pierwszy
            share += CourtFull.Take(b, s, court, head.Gold);       // E5/A2 (R8): przydzial sprzetu przeterminowany dzis -> dwor (w wojnie ustepuje zoldowi po dworze)
            long x = (long)share;
            x = Math.Min(x, Math.Max(0, head.Gold - Math.Max(0, s.FamilyPurseFloor)));   // dwor nie oproznia kiesy glowy ponizej podlogi rodziny
            long lp = CourtFull.Paid(b, seat, x, court);           // E5/A2: liczniki - sprzet z reszty wplaty po dworze (podloga tnie sprzet)
            _dFood += (long)food;
            if (x <= 0) return;
            int ix = (int)Math.Min(int.MaxValue, x);
            head.ChangeHeroGold(-ix);
            CirculationWindows.NoteHeroGold(head, -ix);            // paczka 169b: glowa poza swiatem - zloto weszlo do swiata (tylko licznik)
            int folk = TownFolk.Earn(seat, (int)(ix * (double)CourtFull.FolkShare), TownFolk.KCourt);   // E5/B1 (R8, KL 1.3): 20% dworu do ludu siedziby (zamek - podzamcze); arytmetyka
            seat.Town.ChangeGold(ix - folk);                       // dwor zyje z kasy siedziby: kuchnia, sluzba, stajnie - pieniadze zostaja w osadzie
            if (seat.IsCastle) TownFolk.CourtCredit(seat, ix - folk);   // E5/B1 (kuchnia pana): czesc kupcow dworu w kasie zamku - kredyt na jedzenie podzamcza (bez zlota); CourtCredit sam sprawdza kiese czynna zamkow
            long lpK = folk > 0 && ix > 0 ? lp - (long)(lp * (double)folk / ix) : lp;   // E5/B1: sprzet w kasie (CourtFull.Paid/Own od czesci w kasie)
            ClanIncomeBook.NoteOwnPaid(seat, c, Math.Max(0, ix - folk - CourtFull.Own(seat, c, lpK)));   // 169c: "wlasne" D stalego - dwor wraca zaworem wlasnej osady (tylko licznik); E5/A2 (recenzja): sprzet do wlasnej siedziby - zapas "wlasnego" az do zwrotu
            try { TownFolk.Note(seat, ix, TownFolk.KCourt); } catch { }   // E5/0 (195): dwor w kasie siedziby (tylko licznik)
            if (seat.IsTown) { SoldierPay.HoldCourt(seat, ix); _dCourtTown += ix; }   // tarcza dworu: regulator gry nie kasuje tej wplaty
            _dCourt += ix;
        }

        // ------------------------------------------------------------ zwolnienia (15% nadwyzki dziennie po 3 dobach ponad 1.10 x pulap)
        private static void Releases(Clan c, B b, Settings s)
        {
            // 2-n (bieg 2-letni 10/11.10): Nocna Straz NIE zwalnia ludzi - zakon strzeze Muru przed Innymi zawsze, takze w pokoju z Wolnymi Ludzmi. Dotad pokoj
            // (pulap pokojowy 0.28 D zamiast wojennego z zapasem) zbil pulap puli z ok. 20 tys. do 1.4 tys. i zwolnienia odeslaly do domu ok. 5 tys. ludzi Strazy
            // w kilka dni (7.2 -> 1.2 tys.). Pulap Strazy tylko zatrzymuje werbunek (RecruitPrefix/GarRecruitPrefix/SpawnPrefix); ubytek - bitwy i zgony.
            if (b.Zero && s.WatchNoReleases) { b.Streak = 0; return; }
            double hyst = Math.Max(1f, s.BudgetHysteresis);
            long wage = b.WPar + b.WGar + b.WCar;
            int men = b.MenPar + b.MenGar;
            // 2: Straz - ponad pulapem jest CALA Straz (pula), nie rod; nadwyzka puli dzielona miedzy rody wedlug ich ludzi
            // W5: rod z przydzialem korony - przydzial jest znaczony na druzyny, wiec nadwyzka zoldu zalog ponad pulap wlasny (w wojnie zalog nie tniemy) nie
            // zwalnia ludzi oplaconych przez korone: pulap do zwolnien = pulap partii z przydzialem + zalogi i karawany liczone jak w pulapie partii. U rodu,
            // ktorego zalogi mieszcza sie w pulapie, to ta sama liczba co b.Cap; bez przydzialu (b.Aid = 0) regula bez zmian.
            double capRel = b.Cap;
            if (b.Aid > 0 && !b.Zero) capRel = Math.Max(capRel, b.PartyCap + (GarFull(b, s) ? b.WGar : Math.Min(b.WGar, b.GarTarget)) + b.WCar);
            bool over = b.Zero ? _watchPoolCap >= 0 && _watchPoolMen > hyst * _watchPoolCap : wage > hyst * capRel;
            b.Streak = over ? b.Streak + 1 : 0;
            if (over) _dOver++;
            if (b.Streak < Math.Max(1, s.BudgetHysteresisDays)) return;
            _dStreak++;
            double share = Math.Max(0f, Math.Min(1f, s.ReleasePerDay));
            // do zwolnienia: w zlocie (zold dzienny) albo - Straz bez zoldu - w ludziach
            // recenzja 2 (R1): Straz - nadwyzka calej puli dzielona wedlug ludzi rodu, ktorych da sie zwolnic (partie), nie wedlug zalog
            int rel = 0; if (b.Zero) _watchRel.TryGetValue(c, out rel);
            double left = b.Zero ? (_watchRelSum > 0 ? Math.Ceiling(share * (_watchPoolMen - _watchPoolCap) * rel / (double)_watchRelSum) : 0) : share * (wage - capRel);
            if (left <= 0) return;
            // 1. w pokoju zalogi ponad cel pokojowy (Straz bez zoldu - zalogi na Murze nie sa ciete; od partii)
            if (!GarFull(b, s) && c.Fiefs != null)
                foreach (var f in c.Fiefs)
                {
                    if (left <= 0) break;
                    var st = f != null ? f.Settlement : null; var gp = f != null ? f.GarrisonParty : null;
                    int t;
                    if (st == null || gp == null || !gp.IsActive || gp.MapEvent != null || st.IsUnderSiege || !_garTarget.TryGetValue(st, out t)) continue;   // przeglad C1 (uwaga 5): nie w bitwie ani oblezeniu
                    double above = gp.TotalWage - t;
                    if (above <= 0) continue;
                    double take = Math.Min(left, above);
                    double done = ReleaseFrom(gp, st, c, take, b.Zero, false, true);
                    left -= done;
                }
            if (left <= 0) return;
            // 2. najemnicy z karczmy, 3. najnizszy tier - partie rodu (zalogi w wojnie pelne - nie ruszane)
            var wps = c.WarPartyComponents;
            if (wps == null) return;
            // recenzja C1 (W4): nie z partii w bitwie, w obozie oblezniczym ani zamknietych w oblezonej osadzie (ludzie nie przechodza przez linie oblezenia,
            // armia nie topnieje pod murami); niezwolniona reszta czeka - licznik dob ponad pulapem trwa, zwolnienia wracaja po oblezeniu
            var parties = new List<MobileParty>();
            for (int i = 0; i < wps.Count; i++)
            {
                var mp = wps[i] != null ? wps[i].MobileParty : null;
                if (mp == null || !mp.IsActive || !mp.IsLordParty || mp.MapEvent != null || mp.SiegeEvent != null || mp.BesiegedSettlement != null
                    || (mp.CurrentSettlement != null && mp.CurrentSettlement.IsUnderSiege)) continue;
                parties.Add(mp);
            }
            if (parties.Count == 0) return;
            // recenzja C1 (W3): kwota dnia dzielona miedzy partie - najpierw wedlug nadwyzki kazdej ponad jej wlasny limit zoldu (limity juz z dzisiejszego pulapu),
            // reszta wedlug zoldu partii (Straz bez zoldu - limit w zlocie nic nie znaczy: wedlug ludzi). Dotad cala kwota szla z pierwszej partii na liscie: ta
            // spadala ponizej swojego limitu i werbowala z powrotem (petla werbunek -> zwolnienie na koszt rodu), a inne zostawaly ponad limitem na stale.
            var weight = new double[parties.Count];
            for (int pass = 0; pass < 2 && left > 0; pass++)
            {
                double sumE = 0;
                for (int i = 0; i < parties.Count; i++)
                {
                    var mp = parties[i];
                    weight[i] = b.Zero ? 0 : Math.Max(0, (double)mp.TotalWage - mp.PaymentLimit);
                    sumE += weight[i];
                }
                if (sumE > 0)
                {
                    double part = Math.Min(left, sumE);
                    for (int i = 0; i < parties.Count && left > 0; i++)
                        if (weight[i] > 0) left -= ReleaseFrom(parties[i], null, c, Math.Min(left, part * weight[i] / sumE), b.Zero, pass == 0, false);
                }
                if (left <= 0) break;
                // partie ponizej wlasnego limitu ruszane dopiero teraz, gdy nadwyzki innych nie starczylo
                double sumW = 0;
                for (int i = 0; i < parties.Count; i++)
                {
                    var mp = parties[i];
                    weight[i] = b.Zero ? (mp.MemberRoster != null ? mp.MemberRoster.TotalRegulars : 0) : Math.Max(0, mp.TotalWage);
                    sumW += weight[i];
                }
                if (sumW <= 0) continue;
                double rest = left;
                for (int i = 0; i < parties.Count && left > 0; i++)
                    if (weight[i] > 0) left -= ReleaseFrom(parties[i], null, c, Math.Min(left, rest * weight[i] / sumW), b.Zero, pass == 0, false);
            }
        }

        /// <summary>P3/W2 (TRIAZ-3 O1-3 "Poprawka ostateczna"): ilu ludzi z kazdego stosu odchodzi przy zwolnieniu za `amount` - LOSOWANIE CZLOWIEKA z calej
        /// partii: kazdy czlowiek ma rowna szanse (stos z waga = liczba ludzi), bez zwracania, az zebrany zold >= amount. Ostatni czlowiek, gdy reszta jest
        /// mniejsza niz jego zold, odchodzi z prawdopodobienstwem reszta / zold - srednio wychodzi dokladnie kwota (bez stalego nadmiaru z zaokraglen w gore
        /// i bez zatoru przy malych kwotach dnia: mediana kwoty dnia rodu to 60 zl). Bez stanu - sam rachunek.</summary>
        private static int[] DrawAcross(List<TroopRosterElement> stacks, TaleWorlds.CampaignSystem.ComponentInterfaces.PartyWageModel wageModel, double amount, bool menUnit)
        {
            int k = stacks.Count;
            var take = new int[k]; var left = new int[k]; var w = new int[k];
            int total = 0;
            for (int i = 0; i < k; i++)
            {
                left[i] = Math.Max(0, stacks[i].Number); total += left[i];
                w[i] = menUnit ? 1 : Math.Max(1, wageModel.GetCharacterWage(stacks[i].Character));
            }
            double done = 0;
            while (done < amount && total > 0)
            {
                int x = MBRandom.RandomInt(total), i = 0;
                while (i < k - 1 && x >= left[i]) { x -= left[i]; i++; }
                if (left[i] <= 0) break;                                   // suma wag = total, wiec nie powinno sie zdarzyc
                double rest = amount - done;
                if (w[i] > rest) { if (MBRandom.RandomFloat < rest / w[i]) take[i]++; break; }   // ostatni czlowiek - z prawdopodobienstwem reszta / zold
                take[i]++; left[i]--; total--; done += w[i];
            }
            return take;
        }

        /// <summary>Zwalnia z partii (albo zalogi) ludzi za `amount` zl zoldu dziennie (albo `amount` ludzi przy zoldzie 0), od najnizszego tieru (P3/W2: przy
        /// ReleaseAcrossTiers - losowo z calej partii, DrawAcross); mercsOnly - tylko najemnicy z karczmy (zawsze od najnizszego tieru).
        /// Ludzie do ludnosci BK wsi; czesc sakiewki partii do kiesy wsi (E5/B6: czesc ludzi w karczmie - do puli weteranow). Zwraca zwolnione (zl albo ludzi).</summary>
        private static double ReleaseFrom(MobileParty mp, Settlement fortress, Clan c, double amount, bool menUnit, bool mercsOnly, bool garrison)
        {
            var r = mp.MemberRoster;
            if (r == null || amount <= 0) return 0;
            var wageModel = Campaign.Current.Models.PartyWageModel;
            var stacks = new List<TroopRosterElement>();
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i); var ch = el.Character;
                if (ch == null || ch.IsHero || el.Number <= 0) continue;
                if (mercsOnly && ch.Occupation != Occupation.Mercenary) continue;
                stacks.Add(el);
            }
            if (stacks.Count == 0) return 0;
            stacks.Sort((x, y) => x.Character.Tier.CompareTo(y.Character.Tier));
            var village = VillageFor(mp, fortress, c);
            if (village == null) { _dNoVillage++; return 0; }   // bez wsi z danymi BK nikt nie odchodzi (nikt nie znika - Z6)
            int menBefore = r.TotalRegulars;
            double done = 0; int gone = 0;
            // P3/W2: poza przebiegiem najemnikow z karczmy - liczby na stos z losowania czlowieka (take), potem to samo cialo petli co dotad (najwyzej jedno
            // ToVillage na stos); klucz wylaczony albo najemnicy - take == null i stara petla "od najnizszego tieru az do kwoty"
            var st0 = Settings.Current;
            int[] take = !mercsOnly && st0 != null && st0.ReleaseAcrossTiers ? DrawAcross(stacks, wageModel, amount, menUnit) : null;
            _relTav.Clear(); _relTavOn = true;      // E5/B6 (201): ToVillage dopisuje miasta karczm tego zwolnienia
            try
            {
                for (int si = 0; si < stacks.Count; si++)
                {
                    var el = stacks[si];
                    if (take == null && done >= amount) break;
                    var ch = el.Character;
                    int w = menUnit ? 1 : Math.Max(1, wageModel.GetCharacterWage(ch));
                    int n = take != null ? Math.Min(el.Number, take[si]) : (int)Math.Min(el.Number, Math.Ceiling((amount - done) / w));
                    if (n <= 0) continue;
                    int wounded = el.WoundedNumber > 0 ? Math.Min(el.WoundedNumber, (int)Math.Round(n * (double)el.WoundedNumber / el.Number)) : 0;
                    // E4/7: udzialy pochodzenia zwalnianych - tuz PRZED ToVillage (zdjecie z rosteru jest po nim - Take nie stoi miedzy zdjeciem a dopisaniem, U24)
                    PeopleBook.Shares org = null;
                    try { if (PeopleBook.OriginLive) org = PeopleBook.Take(r, n, PeopleBook.FRelease166); } catch { org = null; }
                    if (!ToVillage(village, ch, n, PeopleBook.M166, org, mp)) { try { PeopleBook.Untake(r, org, PeopleBook.FRelease166); } catch { } _dNoVillage++; continue; }
                    r.AddToCounts(ch, -n, false, -wounded);
                    done += (double)n * w; gone += n;
                    // P3/W2 (pomiar): tier zwalnianych i ich zold (zl dziennie - takze u Strazy bez zoldu, gdzie kwota jest w ludziach)
                    int tb = ch.Tier <= 2 ? 0 : ch.Tier <= 4 ? 1 : 2;
                    _dRelTier[tb] += n; _dRelTierWage[tb] += (long)n * Math.Max(1, wageModel.GetCharacterWage(ch));
                }
            }
            finally { _relTavOn = false; }
            if (gone <= 0) return 0;
            _dRelMen += gone; if (garrison) _dRelGar += gone; else _dRelPar += gone;
            try { if (CrownGifts.IsWatchParty(mp)) _dRelWatch += gone; if (DebtLadder.IsSeized(c)) _dRelSeized += gone; } catch { }    // E4/9: liczniki (bez zmiany reguly)
            // czesc sakiewki partii z odchodzacymi (proporcjonalnie do ludzi) - do kiesy wsi, do ktorej wracaja; E5/B6 (201, R19): sakiewka idzie z czlowiekiem -
            // czesc tych, ktorzy staneli w karczmie (E4/10), do puli weteranow miasta karczmy (VetPurse.FromRelease - nie rzuca, 0 przy wylaczonym kluczu), reszta do wsi
            try
            {
                int purse = MenPurse.Get(mp);
                if (purse > 0 && menBefore > 0)
                {
                    int share = (int)((long)purse * gone / menBefore);
                    int took = share > 0 ? MenPurse.Take(mp, share) : 0;
                    if (took > 0)
                    {
                        int vet = VetPurse.FromRelease(took, gone, _relTav);
                        if (vet < 0) vet = 0; else if (vet > took) vet = took;
                        int vill = took - vet;
                        _dRelPurseVet += vet;
                        if (vill > 0) { village.SettlementComponent.ChangeGold(vill); _dRelPurse += vill; try { TownFolk.Note(village, vill, TownFolk.KReleased); } catch { } }   // E5/0 (195): sakiewka zwolnionych do kiesy wsi (tylko licznik)
                    }
                }
            }
            catch (Exception e) { Stumble("ReleaseFrom(sakiewka)", e); }
            return done;
        }

        // ------------------------------------------------------------ ludnosc BK (refleksja - wzor LosersFlee/Levy; tylko osady z danymi BK)
        private static bool _bkTried;
        private static object _popMgr;
        private static MethodInfo _getPopData, _fromSoldiers, _populated;

        private static bool BkResolve()
        {
            if (_getPopData != null && _popMgr != null && _fromSoldiers != null) return true;
            if (_bkTried) return false;
            _bkTried = true;
            try
            {
                var cfgT = AccessTools.TypeByName("BannerKings.BannerKingsConfig");
                var inst = cfgT != null ? AccessTools.Property(cfgT, "Instance")?.GetValue(null, null) : null;
                _popMgr = inst != null ? AccessTools.Property(cfgT, "PopulationManager")?.GetValue(inst, null) : null;
                if (_popMgr == null) return false;
                _getPopData = AccessTools.Method(_popMgr.GetType(), "GetPopData", new[] { typeof(Settlement) });
                _populated = AccessTools.Method(_popMgr.GetType(), "IsSettlementPopulated", new[] { typeof(Settlement) });
                var pdT = _getPopData != null ? _getPopData.ReturnType : null;
                _fromSoldiers = pdT != null ? AccessTools.Method(pdT, "UpdatePopFromSoldiers", new[] { typeof(CharacterObject), typeof(int) }) : null;
                return _getPopData != null && _fromSoldiers != null;
            }
            catch { return false; }
        }

        private static bool HasPop(Settlement st)
        {
            try { return st != null && BkResolve() && (_populated == null || (bool)_populated.Invoke(_popMgr, new object[] { st })); }
            catch { return false; }
        }

        /// <summary>179 (GentryService.FinishPrefix) i W4c (OverflowHome.SendHome): ludzie do ludnosci BK wsi (tylko wies z danymi BK); false - nikt nie przeszedl.
        /// cause - przyczyna w ksiedze ludzi (PeopleBook.M179 / MW4c).</summary>
        internal static bool ToVillagePop(Settlement v, CharacterObject ch, int n, int cause, PeopleBook.Shares org = null, MobileParty party = null)
        {
            return v != null && ch != null && n > 0 && BkResolve() && HasPop(v) && ToVillage(v, ch, n, cause, org, party);
        }

        /// <summary>E4/10 (W1): weterani do ludnosci BK miasta karczmy (sklad, nie ludzie - R0) - ta sama refleksja co ToVillage, bez ksiegi ludzi. false - miasto bez danych BK.</summary>
        internal static bool BkPopAdd(Settlement town, CharacterObject ch, int n)
        {
            if (town == null || ch == null || n <= 0 || !BkResolve() || !HasPop(town)) return false;
            try
            {
                var pd = _getPopData.Invoke(_popMgr, new object[] { town });
                if (pd == null) return false;
                A16Signals.Ours++;                            // A16: pomiar ludnosci BK - weterani naszej reki
                try { _fromSoldiers.Invoke(pd, new object[] { ch, n }); }
                finally { A16Signals.Ours--; }
                return true;
            }
            catch (Exception e) { Stumble("BkPopAdd(E4/10)", e); return false; }
        }

        /// <summary>Drugi punkt powrotu do DOM (R0, U2): zwolnieni 166, przepelnienie W4c, rycerze 179 - do ludnosci BK wsi (sklad BK) i do ksiegi ludzi
        /// (PeopleBook: SLUZBA -> DOM tylko liczona do E4/7 - werbunek jeszcze nie zdejmuje rekruta z DOM, wiec hearth bylby ludzmi z niczego).</summary>
        private static bool ToVillage(Settlement v, CharacterObject ch, int n, int cause, PeopleBook.Shares org = null, MobileParty party = null)
        {
            // E4/10 (W1, R16): plan weteranow PRZED ludnoscia BK wsi (bez zapisu stanu): t z n stanie w karczmie najblizszego miasta (1/3 uprawnionych od tieru
            // VeteranMinTier), wies dostaje n - t. BK wsi zawiedzie - false PRZED zapisem weteranow (nikt nie zszedl, ReleaseFrom nie zdejmuje - jak dotad).
            PeopleBook.VetTicket vt = null;
            // E4/9 (DLUG 4): odchodzacy z rodu w zajeciu z braku pieniedzy - ta sama droga co przepelnienie W4c (VetDivert jak W4c - E410 :91)
            int vsrc = cause == PeopleBook.M166 ? PeopleBook.V166 : (cause == PeopleBook.MW4c || cause == PeopleBook.MDebt) ? PeopleBook.VW4c : cause == PeopleBook.M179 ? PeopleBook.V179 : -1;
            try { if (vsrc >= 0 && PeopleBook.VetLive && v != null) vt = PeopleBook.VetPlan(party, ch, n, v.GetPosition2D, vsrc); } catch { vt = null; }
            int t = vt != null && vt.Reason == PeopleBook.VROk ? vt.T : 0;
            int known = cause == PeopleBook.M166 ? PeopleBook.MBack166 : cause == PeopleBook.MW4c ? PeopleBook.MBackW4c : cause == PeopleBook.M179 ? PeopleBook.MBack179
                        : cause == PeopleBook.MDebt ? PeopleBook.MBackDebt : -1;     // E4/9 (DLUG 4): dluznicy do domu - z pochodzeniem z hearth
            // A16 (BkReturnsToOriginVillage, ROZPOZNANIE-A16 poz. 9): czesc znana pochodzeniem idzie do ludnosci BK TEJ SAMEJ osady, do ktorej ksiega oddaje ja do DOM
            // (dotad: BK - wies v rodu, ksiega - wies regionu pochodzenia). Wies v nadal musi miec dane BK (false PRZED weteranami - jak dotad); dopisanie BK po
            // ksiedze (lustro HomeOrNoteMirror); reszta (bez pochodzenia, bez wsi w regionie, osada bez BK, zaokraglenie) - BK wsi v jak dotad. Udzial Strazy - jak dotad.
            bool mirror = org != null && known >= 0 && A16Signals.ReturnsToOrigin && org.Known > 0.0 && !(org.W > 0.0) && PeopleBook.LevyLive;
            object pdv = null;
            if (n - t > 0)
            {
                try
                {
                    pdv = _getPopData.Invoke(_popMgr, new object[] { v });
                    if (pdv == null) return false;
                    if (!mirror)
                    {
                        A16Signals.Ours++;                    // A16: pomiar ludnosci BK - powrot naszej reki
                        try { _fromSoldiers.Invoke(pdv, new object[] { ch, n - t }); }
                        finally { A16Signals.Ours--; }
                    }
                }
                catch (Exception e) { Stumble("ToVillage", e); return false; }
            }
            int put = 0;
            if (vt != null)
            {
                put = PeopleBook.VetCommit(vt, org);          // nie rzuca; dopisanie do puli ostatnim krokiem; org pomniejszony o put
                if (cause == PeopleBook.M166) _dRelTavern += put;
                // E5/B6 (201): miasto karczmy tego zwolnienia (tylko w ReleaseFrom - 179 i W4c nie niosa sakiewki)
                try { if (_relTavOn && put > 0 && vt.Town != null && vt.Town.Settlement != null) _relTav.Add(new KeyValuePair<Settlement, int>(vt.Town.Settlement, put)); } catch { }
            }
            if (put < t && !mirror)
            {
                // pula nie przyjela (wyjatek - potkniecie E4/10) - ci ludzie do ludnosci BK wsi jak dotad (nikt nie znika)
                try
                {
                    var pd = _getPopData.Invoke(_popMgr, new object[] { v });
                    if (pd != null) { A16Signals.Ours++; try { _fromSoldiers.Invoke(pd, new object[] { ch, t - put }); } finally { A16Signals.Ours--; } }
                }
                catch (Exception e) { Stumble("ToVillage(E4/10 zwrot)", e); }
            }
            int home = n - put;
            if (home <= 0) return true;
            if (mirror)
            {
                // A16: ksiega najpierw (DOM pochodzenia z lustrem osad), potem ludnosc BK tych samych osad; BK dostaje razem home ludzi - tyle co dotad (n - t + t - put)
                var places = new List<KeyValuePair<Settlement, double>>(2);
                PeopleBook.HomeOrNoteMirror(org, home, known, cause, places);
                int total;
                int ok = LosersFlee.BkAddPlaces(ch, places, home, 0, false, out total);
                int restBk = home - ok;
                if (restBk > 0)
                {
                    try
                    {
                        if (pdv == null) pdv = _getPopData.Invoke(_popMgr, new object[] { v });
                        if (pdv != null) { A16Signals.Ours++; try { _fromSoldiers.Invoke(pdv, new object[] { ch, restBk }); } finally { A16Signals.Ours--; } }
                    }
                    catch (Exception e) { Stumble("ToVillage(A16 reszta)", e); }
                }
                return true;
            }
            // E4/3: wlasny try w ksiedze (U24) - po dopisaniu, nie miedzy zdjeciem a dopisaniem. E4/7: czesc znana pochodzeniem - DOM regionu pochodzenia z hearth
            // (MBack166/MBackW4c/MBack179), reszta - jak w E4/3 (tylko liczona); ludnosc BK dostaje ich tam, gdzie dotad
            if (org != null)
            {
                if (known >= 0) PeopleBook.HomeOrNote(org, home, known, cause); else PeopleBook.Note(home, cause, 0.0);
            }
            else PeopleBook.Note(home, cause, 0.0);
            return true;
        }

        /// <summary>W4c (OverflowHome): wies domowa ludzi, ktorzy odchodza z przepelnionej partii albo zalogi - ta sama regula co zwolnieni 166
        /// (VillageFor: zaloga - wsie jej twierdzy, partia - najblizsza wies rodu, potem krolestwa; tylko z danymi BK). null - brak takiej wsi.</summary>
        internal static Settlement OverflowVillage(MobileParty mp)
        {
            if (mp == null) return null;
            try
            {
                Settlement fortress = mp.IsGarrison ? (mp.CurrentSettlement ?? mp.HomeSettlement) : null;
                return VillageFor(mp, fortress, OwnerOf(mp));
            }
            catch (Exception e) { Stumble("OverflowVillage", e); return null; }
        }

        /// <summary>drobne2 (K20, klucz DebtorHomeNearestVillage; tylko OverflowHome.SendHome dla dluznikow, gdy OverflowVillage nie dal wsi - rod bez twierdzy,
        /// wsi i krolestwa): najblizsza wies swiata z danymi BK od pozycji partii. null - brak BK albo wyjatek (wtedy jak dotad: do puli).</summary>
        internal static Settlement NearestPopVillage(MobileParty mp)
        {
            if (mp == null || !BkResolve()) return null;
            try
            {
                var pos = mp.GetPosition2D;
                Settlement best = null; float bd = float.MaxValue;
                foreach (var vg in Village.All)
                {
                    var st = vg != null ? vg.Settlement : null;
                    if (st == null || !HasPop(st)) continue;
                    float d = pos.DistanceSquared(st.GetPosition2D);
                    if (d < bd) { bd = d; best = st; }
                }
                return best;
            }
            catch (Exception e) { Stumble("NearestPopVillage", e); return null; }
        }

        /// <summary>Wies, do ktorej wracaja zwolnieni: zaloga - wsie tej twierdzy; partia - najblizsza wies rodu; potem najblizsza wies krolestwa. Tylko z danymi BK.</summary>
        private static Settlement VillageFor(MobileParty mp, Settlement fortress, Clan c)
        {
            if (!BkResolve()) return null;
            TaleWorlds.Library.Vec2 pos;
            try { pos = fortress != null ? fortress.GetPosition2D : mp.GetPosition2D; } catch { return null; }
            Settlement best = null; float bd = float.MaxValue;
            if (fortress != null && fortress.BoundVillages != null)
                foreach (var v in fortress.BoundVillages)
                {
                    var st = v != null ? v.Settlement : null;
                    if (st == null || !HasPop(st)) continue;
                    float d = pos.DistanceSquared(st.GetPosition2D);
                    if (d < bd) { bd = d; best = st; }
                }
            if (best != null) return best;
            if (c != null && c.Settlements != null)   // W4c: OverflowVillage moze podac zaloge bez wlasciciela
                foreach (var st in c.Settlements)
                {
                    if (st == null || !st.IsVillage || !HasPop(st)) continue;
                    float d = pos.DistanceSquared(st.GetPosition2D);
                    if (d < bd) { bd = d; best = st; }
                }
            if (best != null) return best;
            var k = c != null ? c.Kingdom : null;
            if (k != null)
                foreach (var st in k.Settlements)
                {
                    if (st == null || !st.IsVillage || !HasPop(st)) continue;
                    float d = pos.DistanceSquared(st.GetPosition2D);
                    if (d < bd) { bd = d; best = st; }
                }
            return best;
        }

        // ------------------------------------------------------------ odczyty dla innych modulow
        /// <summary>AiGear: najwiecej, ile pan rodu AI moze dzis wydac na sprzet (niewydany przydzial); false - rod bez budzetu (bez ograniczenia).</summary>
        internal static bool GearCap(Clan c, out int cap)
        {
            var b = Of(c); cap = b != null ? (int)Math.Max(0, Math.Min(int.MaxValue, b.GearLeft)) : 0; return b != null;
        }

        /// <summary>AiGear: pan zaplacil za sprzet - schodzi z przydzialu.</summary>
        internal static void GearSpent(Clan c, int amount)
        {
            var b = Of(c);
            if (b == null || amount <= 0) return;
            b.GearLeft = Math.Max(0, b.GearLeft - amount); _dGearSpent += amount;
        }

        /// <summary>168: D rodu z ostatniego budzetu (Bank biegnie przed budzetem - to D wczoraj); -1 - rod bez budzetu.</summary>
        internal static double DLast(Clan c)
        {
            B b; return c != null && On && _b.TryGetValue(c, out b) && b.D0 >= 0 ? b.D : -1;
        }

        /// <summary>168: miejsce kredytu wojennego w dzisiejszym pulapie rodu (0 - bez KW albo bez budzetu) - kolumna kandydat_pozyczki.</summary>
        internal static double KwRoomOf(Clan c) { var b = Of(c); return b != null ? b.KwRoom : 0; }

        /// <summary>168 (B5): zakup karawany i warsztatu BK tylko przy G >= 60 D i bez dlugu - true = zablokowany (rod z budzetem).</summary>
        internal static bool PropertyBlocked(Clan c)
        {
            B b; if (c == null || !On || !_b.TryGetValue(c, out b) || b.D0 < 0) return false;
            return b.G < 60.0 * b.D || DebtLadder.IsDebtor(c) || DebtLadder.RansomDebtOf(c) > 0;   // recenzja D: "bez dlugu" - takze dlug okupu
        }

        /// <summary>BuildFunding: dzienny przydzial budow rodu (0.10 D; w wojnie BuildFunding finansuje nim tylko budowy wojskowe - W5); false - rod bez budzetu (stara podstawa).</summary>
        internal static bool BuildShare(Clan c, out float share)
        {
            var b = Of(c); share = b != null ? (float)Math.Max(0, b.Build) : 0f; return b != null;
        }

        /// <summary>N5-E (BuildFunding przez WarFront.CivilAllowed - budowle cywilne w wojnie): rod ma luz w pulapie zoldu - zold partii, zalog i karawan z ostatniego
        /// budzetu ponizej 95% pulapu (ta sama miara co SlackGold [1]). Budzet 166 wylaczony albo Straz bez zoldu - true (bez pulapu zoldu nie ma czego chronic);
        /// budzet jeszcze nie policzony w tej sesji (pierwsza doba po wczytaniu), pulap 0 albo najemnik - false. Tylko odczyt.</summary>
        internal static bool WageRoom(Clan c)
        {
            if (!On) return true;
            var b = Of(c);
            if (b == null || c == null) return false;
            if (b.Zero) return true;
            return b.Cap > 0 && !c.IsUnderMercenaryService && b.WPar + b.WGar + b.WCar < 0.95 * b.Cap;
        }

        // ------------------------------------------------------------ latki gry (w kampanii, raz na proces - pulapka konstruktorow statycznych modeli)
        private static Harmony _harmony;
        private static bool _hooksTried;
        private static readonly List<string> _wired = new List<string>(), _missing = new List<string>();
        internal static void SetHarmony(Harmony h) { _harmony = h; }

        internal static void EnsureHooks()
        {
            if (_hooksTried || _harmony == null || Campaign.Current == null) return;
            _hooksTried = true;
            Wire("limit partii (MakeClanFinancialEvaluation)", AccessTools.Method(typeof(ClanVariablesCampaignBehavior), "MakeClanFinancialEvaluation", new[] { typeof(Clan) }), null, nameof(EvalPostfix), null);
            Wire("limit zalog (UpdateClanSettlementsPaymentLimit)", AccessTools.Method(typeof(ClanVariablesCampaignBehavior), "UpdateClanSettlementsPaymentLimit", new[] { typeof(Clan) }), null, nameof(GarLimitPostfix), null);
            Wire("dezercja z limitu zoldu (GetTroopsToDesertDueToWageAndPartySize)", AccessTools.Method(typeof(DefaultPartyDesertionModel), "GetTroopsToDesertDueToWageAndPartySize", new[] { typeof(MobileParty), typeof(TroopRoster) }), nameof(DesertPrefix), null, nameof(DesertFinalizer));
            Wire("nowa partia (SpawnLordParty)", AccessTools.Method(typeof(HeroSpawnCampaignBehavior), "SpawnLordParty", new[] { typeof(Hero), typeof(bool) }), nameof(SpawnPrefix), nameof(SpawnPostfix), null);
            Wire("werbunek Strazy (CheckRecruiting)", AccessTools.Method(typeof(RecruitmentCampaignBehavior), "CheckRecruiting", new[] { typeof(MobileParty), typeof(Settlement) }), nameof(RecruitPrefix), null, null);
            Wire("zaloga Strazy: werbunek (TickAutoRecruitmentGarrisonChange)", AccessTools.Method(typeof(GarrisonRecruitmentCampaignBehavior), "TickAutoRecruitmentGarrisonChange", new[] { typeof(Town) }), nameof(GarRecruitPrefix), null, null);
            Wire("zaloga Strazy: przyrost (TickGarrisonChangeForTown)", AccessTools.Method(typeof(GarrisonRecruitmentCampaignBehavior), "TickGarrisonChangeForTown", new[] { typeof(Town) }), nameof(GarRecruitPrefix), null, null);
            Log.Info("Budzet rodow (166): latki gry - " + (_wired.Count > 0 ? string.Join(", ", _wired.ToArray()) : "-") + "; BRAK: " + (_missing.Count > 0 ? string.Join(", ", _missing.ToArray()) : "-") + ".");
        }

        private static void Wire(string label, MethodBase m, string pre, string post, string fin)
        {
            try
            {
                if (m == null) { _missing.Add(label); return; }
                _harmony.Patch(m, prefix: pre != null ? new HarmonyMethod(typeof(ClanBudget), pre) : null,
                                  postfix: post != null ? new HarmonyMethod(typeof(ClanBudget), post) { priority = Priority.Last } : null,
                                  finalizer: fin != null ? new HarmonyMethod(typeof(ClanBudget), fin) : null);
                _wired.Add(label);
            }
            catch (Exception e) { _missing.Add(label + " (blad: " + e.Message + ")"); }
        }

        /// <summary>Po ocenie finansow rodu przez gre/BK (BK zwraca false z prefiksu - postfiks biegnie): limity partii z budzetu.</summary>
        public static void EvalPostfix(Clan __0)
        {
            try { var b = Of(__0); if (b != null) ApplyPartyLimits(__0, b); else GentryService.HoldParties(__0); }   // 179: partie rodu rycerza z limitem 0 takze bez budzetu
            catch (Exception e) { Stumble("EvalPostfix", e); }
        }

        /// <summary>Po pulapie zalog gry: w pokoju najwyzej cel pokojowy budzetu (w wojnie bez zmian).</summary>
        public static void GarLimitPostfix(Clan __0)
        {
            try { var b = Of(__0); if (b != null) ApplyGarrisonLimits(__0, b); }
            catch (Exception e) { Stumble("GarLimitPostfix", e); }
        }

        /// <summary>AiWageLimitDesertionOff: dla partii rodu z budzetem gra nie zdejmuje ludzi z limitu zoldu (zwalnia budzet) - limit zdjety na czas wywolania,
        /// przepelnienie partii i zalegly zold zalogi dzialaja jak dotad.</summary>
        public static void DesertPrefix(MobileParty __0, out int __state)
        {
            __state = int.MinValue;
            try
            {
                var s = Settings.Current;
                if (__0 == null || s == null || !(__0.IsLordParty || __0.IsGarrison) || !__0.HasLimitedWage()) return;
                // 179: stara partia rodu rycerza ma limit 0 (bez rekrutow) - nikt nie odchodzi do lasu z limitu, BK rozwiaze ja w majatku (ludzie do wsi)
                if (!GentryService.IsHeldParty(__0) && (!s.AiWageLimitDesertionOff || Of(OwnerOf(__0)) == null)) return;
                __state = __0.PaymentLimit;
                __0.SetWagePaymentLimit(Campaign.Current.Models.PartyWageModel.MaxWagePaymentLimit);
                _dDesertOff++;
            }
            catch (Exception e) { __state = int.MinValue; Stumble("DesertPrefix", e); }
        }

        public static Exception DesertFinalizer(Exception __exception, MobileParty __0, int __state)
        {
            try { if (__state != int.MinValue && __0 != null) __0.SetWagePaymentLimit(__state); }
            catch (Exception e) { Stumble("DesertFinalizer", e); }
            return __exception;
        }

        // przeglad C1 (uwaga 7): gra w jednym wywolaniu ConsiderSpawningLordParties moze wystawic kilka partii - sprawdzamy kazda (SpawnLordParty),
        // z miejscem zajetym przez wczesniejsze nowe partie tej doby; nowa partia dostaje limit zoldu od razu (nie dopiero w rozliczeniu nastepnego dnia)
        private static readonly Dictionary<Clan, double> _spawnUsed = new Dictionary<Clan, double>();
        private static int _spawnDay = -1;

        /// <summary>Nowa partia rodu tylko, gdy w pulapie partii jest miejsce na MinNewPartyMen ludzi (glowa rodu, rod bez partii, nowa kampania - zawsze).</summary>
        public static bool SpawnPrefix(Hero __0, bool __1, ref MobileParty __result)
        {
            try
            {
                if (GentryService.BlocksSpawn(__0)) { __result = null; return false; }   // 179: rod rycerza bez wlasnej druzyny (rycerz jedzie w druzynie pana)
                var s = Settings.Current;
                var c = __0 != null ? __0.Clan : null;
                var b = Of(c);
                if (__1 || b == null || s == null || __0 == c.Leader || c.WarPartyComponents == null || c.WarPartyComponents.Count == 0) return true;
                int day = (int)CampaignTime.Now.ToDays;
                if (day != _spawnDay) { _spawnUsed.Clear(); _spawnDay = day; }
                double used; _spawnUsed.TryGetValue(c, out used);
                int need = Math.Max(0, s.MinNewPartyMen);
                double cost;
                bool room;
                if (b.Zero) { cost = need; room = _watchPoolCap < 0 || _watchPoolCap - WatchLiveMen() - WatchUsed() >= need; }   // 2: pula calej Strazy
                else
                {
                    double avg = b.MenPar > 0 ? (double)b.WPar / b.MenPar : Math.Max(1f, Campaign.Current.AverageWage);
                    cost = need * avg;
                    room = b.PartyCap - b.Aid - b.WPar - used >= cost;   // W5: przydzial korony nie otwiera nowych druzyn (miejsce liczone od pulapu wlasnego)
                }
                if (!room) { _dSpawnBlock++; __result = null; return false; }   // ConsiderSpawningLordParties sprawdza null - partia nie powstaje
                _spawnUsed[c] = used + cost;
                return true;
            }
            catch (Exception e) { Stumble("SpawnPrefix", e); return true; }
        }

        public static void SpawnPostfix(Hero __0, MobileParty __result)
        {
            try { var c = __0 != null ? __0.Clan : null; var b = Of(c); if (b != null && __result != null) ApplyPartyLimits(c, b); }
            catch (Exception e) { Stumble("SpawnPostfix", e); }
        }

        /// <summary>Przeglad C1 (uwaga 6): ludzie rodu teraz (partie i zalogi) - pulap Strazy w ludziach liczony od biezacego stanu, nie od porannego.</summary>
        private static int LiveMen(Clan c)
        {
            int n = 0;
            var wps = c.WarPartyComponents;
            if (wps != null) for (int i = 0; i < wps.Count; i++) { var mp = wps[i] != null ? wps[i].MobileParty : null; if (mp != null && mp.IsActive && mp.MemberRoster != null) n += mp.MemberRoster.TotalRegulars; }
            var fiefs = c.Fiefs;
            if (fiefs != null) for (int i = 0; i < fiefs.Count; i++) { var gp = fiefs[i] != null ? fiefs[i].GarrisonParty : null; if (gp != null && gp.IsActive && gp.MemberRoster != null) n += gp.MemberRoster.TotalRegulars; }
            return n;
        }

        /// <summary>Recenzja 2 (R1): ludzie rodu w partiach lordow, ktore Releases moze ruszyc (ten sam filtr co lista partii w Releases).</summary>
        private static int ReleasableMen(Clan c)
        {
            int n = 0;
            var wps = c != null ? c.WarPartyComponents : null;
            if (wps == null) return 0;
            for (int i = 0; i < wps.Count; i++)
            {
                var mp = wps[i] != null ? wps[i].MobileParty : null;
                if (mp == null || !mp.IsActive || !mp.IsLordParty || mp.MapEvent != null || mp.SiegeEvent != null || mp.BesiegedSettlement != null
                    || (mp.CurrentSettlement != null && mp.CurrentSettlement.IsUnderSiege) || mp.MemberRoster == null) continue;
                n += mp.MemberRoster.TotalRegulars;
            }
            return n;
        }

        /// <summary>2: ludzie calej Strazy teraz (partie i zalogi rodow Strazy z dzisiejszej puli).</summary>
        private static int WatchLiveMen()
        {
            int n = 0;
            for (int i = 0; i < _watchClans.Count; i++) { var c = _watchClans[i]; if (c != null && !c.IsEliminated) n += LiveMen(c); }
            return n;
        }

        /// <summary>2: miejsce w puli Strazy zajete dzis przez nowe partie tej doby (zabezpieczenie: gra moze wystawic kilka partii w jednym wywolaniu;
        /// sklad z szablonu nowej partii jest juz w WatchLiveMen, wiec liczenie dziala w bezpieczna strone - blokuje kolejne partie tej doby).</summary>
        private static double WatchUsed()
        {
            double u = 0;
            for (int i = 0; i < _watchClans.Count; i++) { double v; if (_watchClans[i] != null && _spawnUsed.TryGetValue(_watchClans[i], out v)) u += v; }
            return u;
        }

        /// <summary>N7 (StragglersRally): wolne miejsce puli Strazy bez zoldu w ludziach (pulap calej Strazy - zywi - nowe partie tej doby; ta sama miara co
        /// werbunek Strazy, RecruitPrefix); -1 - rod nie jest Straza bez zoldu albo pula nieznana (wtedy wiaze limit zoldu druzyny). Tylko odczyt.</summary>
        internal static int WatchRoom(Clan c)
        {
            try
            {
                var b = Of(c);
                if (b == null || !b.Zero || _watchPoolCap < 0) return -1;
                return Math.Max(0, (int)Math.Floor(_watchPoolCap - WatchLiveMen() - WatchUsed()));
            }
            catch (Exception e) { Stumble("WatchRoom", e); return -1; }
        }

        /// <summary>Straz bez zoldu: werbunek partii staje na pulapie w ludziach (limit w zlocie przy zoldzie 0 nie wiaze).</summary>
        public static bool RecruitPrefix(MobileParty __0)
        {
            try
            {
                if (__0 == null || !CrownGifts.IsWatchParty(__0)) return true;
                var c = OwnerOf(__0);
                var b = Of(c);
                if (b == null || !b.Zero || _watchPoolCap < 0) return true;
                if (WatchLiveMen() < _watchPoolCap) return true;   // 2: pula calej Strazy
                _dRecruitBlock++;
                return false;
            }
            catch (Exception e) { Stumble("RecruitPrefix", e); return true; }
        }

        public static bool GarRecruitPrefix(Town __0)
        {
            try
            {
                var st = __0 != null ? __0.Settlement : null;
                if (st == null || !CrownGifts.IsWatch(st.MapFaction)) return true;
                var b = Of(st.OwnerClan);
                if (b == null || !b.Zero || _watchPoolCap < 0) return true;
                if (WatchLiveMen() < _watchPoolCap) return true;   // 2: pula calej Strazy
                _dGarBlock++;
                return false;
            }
            catch (Exception e) { Stumble("GarRecruitPrefix", e); return true; }
        }

        // ------------------------------------------------------------ linia "Budzet rodow (166)"
        private static void Report(Settings s, int today)
        {
            if (!s.LogEnabled) return;
            var sb = new StringBuilder(1600);
            sb.Append("Budzet rodow (166): dzien ").Append(today)
              .Append(" | rody AI z budzetem ").Append(_dClans).Append(" (pokoj ").Append(_dPeace).Append(", wojna ").Append(_dWar).Append("; najemnicy - 185, gracz - bez budzetu)")
              .Append(" | D razem ").Append(_dD).Append(", pulap zoldu razem ").Append(_dCapSum).Append(", zold naliczony (partie + zalogi + karawany) ").Append(_dWageSum)
              .Append(" (").Append(_dCapSum > 0 ? (100.0 * _dWageSum / _dCapSum).ToString("0", Inv) + "%" : "-").Append(")")
              .Append(" | pokoj (Z.1): rody w pokoju ").Append(_dPzN).Append(", pulap pokoju razem ").Append(_dPzCap).Append(" (")
              .Append(s.PeaceWageShare.ToString("0.00", Inv)).Append(" D + ").Append(s.PeaceChestShare.ToString("0.00", Inv)).Append(" x skrzynia wojenna")
              .Append(s.PeaceChestShare > 0f ? "" : " - skrzynia pokoju wylaczona").Append("), w tym skrzynia pokoju ").Append(_dPzChest).Append(" u ").Append(_dPzChestN)
              .Append(" rodow, zold naliczony ").Append(_dPzWage).Append(", ludzie w partiach ").Append(_dPzMenPar).Append(", w zalogach ").Append(_dPzMenGar)
              .Append(" | w wojnie dwor ustepuje zoldowi: udzial dworu w pulapie ").Append(_dCourtRoom).Append(" u ").Append(_dCourtRoomN)
              .Append(" rodow, zold go zajal (dwor nie dostal) ").Append(_dCourtYield).Append(" u ").Append(_dCourtYieldN).Append(" rodow")
              .Append(s.WarCourtYieldsToWages ? "" : " (wylaczone)")
              .Append(" | kredyt wojenny (168): miejsce w pulapie ").Append(_dKwRoom).Append(" u ").Append(_dKwRoomN).Append(" rodow, wyplacone dzis ").Append(_dKwLent).Append(" u ").Append(_dKwLentN)
              .Append(" rodow; dluznicy (dwor -50%, budowy 0) ").Append(_dDebtorN).Append(", w zajeciu (D bez dochodu wsi) ").Append(_dSeizedN).Append(" (dochod wsi ").Append(_dSeizedLand).Append(')')
              .Append(DebtLadder.On ? "" : " (drabina dlugu wylaczona)")
              .Append(" | limity partii ").Append(s.PartyLimitsShareFreeRoom ? "z wolnego miejsca rodu" : "staly podzial 1.5 : 1")
              .Append(", rody ponad pulapem partii ").Append(_dPartyOverCap)
              .Append(" | ponad 1.10 x pulap ").Append(_dOver).Append(" rodow, od 3 dob (zwalniaja) ").Append(_dStreak)
              .Append(" | zwolnieni: do wsi ").Append(_dRelMen).Append(" ludzi (z partii ").Append(_dRelPar).Append(", z zalog ").Append(_dRelGar).Append("), zniklo ").Append(_dVanished)
              .Append(", z tego do karczmy (E4/10) ").Append(_dRelTavern)
              .Append(", nie zwolniono - brak wsi z danymi BK ").Append(_dNoVillage).Append(", sakiewki zwolnionych do kies wsi ").Append(_dRelPurse).Append(" zl")
              .Append(", do puli weteranow w karczmach (E5/B6) ").Append(_dRelPurseVet).Append(" zl")
              .Append(" | P3/W2 zwolnieni wedlug tieru (ludzi / ich zold zl): t1-2 ").Append(_dRelTier[0]).Append(" / ").Append(_dRelTierWage[0])
              .Append(", t3-4 ").Append(_dRelTier[1]).Append(" / ").Append(_dRelTierWage[1]).Append(", t5+ ").Append(_dRelTier[2]).Append(" / ").Append(_dRelTierWage[2])
              .Append(s.ReleaseAcrossTiers ? " (Release Across Tiers: losowo z calej partii)" : " (Release Across Tiers wylaczone: najnizszy tier najpierw)")
              .Append("; rody w wojnie, ktorym zalogi i karawany zajely caly pulap (na partie 0): ").Append(_dGarEatsCap).Append(", z tego bez ludzi w partiach ").Append(_dGarEatsCapEmpty)
              .Append(" | dezercja gry z limitu zoldu wylaczona (rody z budzetem): ").Append(_dDesertOff).Append(" wywolan")
              .Append(" | limity: partie zmienione ").Append(_dPartyLimited).Append(", zalogi przyciete do celu pokojowego ").Append(_dGarLimited)
              .Append(", nowe partie wstrzymane ").Append(_dSpawnBlock)
              .Append(" | dwor (162m): wplacone do kas siedzib ").Append(_dCourt).Append(" (w tym miasta pod tarcza dworu ").Append(_dCourtTown).Append("), jedzenie partii (szacunek, odjete) ").Append(_dFood)
              .Append(", ").Append(SoldierPay.CourtNote())
              .Append(" | kiesa rodziny do glow: ").Append(_dFamily).Append(" zl w ").Append(_dFamilyN).Append(" przelewach; glowy < 5000 (rody z budzetem): ").Append(_dPoor)
              .Append(" | sprzet: przydzial dnia ").Append(_dGear).Append(", wydane przez panow od wczoraj ").Append(_dGearSpent)
              .Append(CourtFull.Note(s))   // E5/A2: sprzet niewydany -> dwor
              .Append(" | budowy: przydzial dnia ").Append(_dBuild)
              .Append(" | Straz w ludziach ").Append(_dWatchMen).Append(" / pulap w ludziach ").Append(_dWatchCap < 0 ? "-" : _dWatchCap.ToString(Inv))
              .Append(" (werbunek wstrzymany ").Append(_dRecruitBlock).Append(", przyrost zalog wstrzymany ").Append(_dGarBlock)
              .Append("; jeden pulap calej Strazy (2), rody ludzie/udzial w pulapie: ").Append(_watchDetail.Length > 0 ? _watchDetail : "-").Append(")")
              .Append(_stumbles > 0 ? " | potkniecia " + _stumbles : "").Append('.');
            if (_importN >= 0) { sb.Append(" Wczytano z zapisu: rodow ").Append(_importN).Append(" (bledne ").Append(_importBad).Append(")."); _importN = -1; }
            Log.Info(sb.ToString());
            _dDesertOff = _dSpawnBlock = _dRecruitBlock = _dGarBlock = 0; _dGearSpent = 0;
        }

        // ------------------------------------------------------------ zapis (SaveText, "arm_clanbudget")
        /// <summary>"v1|id:D:streak:sprzet;...|idTwierdzy:zoldWojenny;..."</summary>
        internal static string Export()
        {
            try
            {
                var sb = new StringBuilder(_b.Count * 40 + _warGar.Count * 24 + 8);
                sb.Append("v1|");
                bool first = true;
                foreach (var kv in _b)
                {
                    var c = kv.Key; var b = kv.Value;
                    if (c == null || c.StringId == null || c.StringId.IndexOfAny(Bad) >= 0) continue;
                    if (!first) sb.Append(';'); first = false;
                    sb.Append(c.StringId).Append(':').Append(Math.Round(b.D).ToString(Inv)).Append(':').Append(b.Streak.ToString(Inv)).Append(':').Append(Math.Round(b.GearLeft).ToString(Inv));
                }
                foreach (var kv in _saved)   // rody z zapisu, ktorych dzis nie bylo (np. wczytanie i zapis bez doby) - nie gubimy
                {
                    if (kv.Key == null || kv.Key.IndexOfAny(Bad) >= 0) continue;
                    if (!first) sb.Append(';'); first = false;
                    sb.Append(kv.Key).Append(':').Append(Math.Round(kv.Value[0]).ToString(Inv)).Append(':').Append(((int)kv.Value[1]).ToString(Inv)).Append(':').Append(Math.Round(kv.Value[2]).ToString(Inv));
                }
                sb.Append('|');
                first = true;
                foreach (var kv in _warGar)
                {
                    if (kv.Key == null || kv.Key.IndexOfAny(Bad) >= 0) continue;
                    if (!first) sb.Append(';'); first = false;
                    sb.Append(kv.Key).Append(':').Append(Math.Round(kv.Value).ToString(Inv));
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }
        private static readonly char[] Bad = { '|', ';', ':' };

        internal static void Import(string data)
        {
            _b.Clear(); _saved.Clear(); _warGar.Clear(); _importN = 0; _importBad = 0;
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                var f = data.Split('|');
                if (f.Length < 3 || f[0] != "v1") { _importBad++; return; }
                if (f[1].Length > 0)
                    foreach (var p in f[1].Split(';'))
                    {
                        var x = p.Split(':'); double d, g; int st;
                        if (x.Length == 4 && x[0].Length > 0 && double.TryParse(x[1], NumberStyles.Float, Inv, out d) && int.TryParse(x[2], NumberStyles.Integer, Inv, out st)
                            && double.TryParse(x[3], NumberStyles.Float, Inv, out g))
                        { _saved[x[0]] = new[] { d, st, g }; _importN++; }
                        else _importBad++;
                    }
                if (f[2].Length > 0)
                    foreach (var p in f[2].Split(';'))
                    {
                        var x = p.Split(':'); float w;
                        if (x.Length == 2 && x[0].Length > 0 && float.TryParse(x[1], NumberStyles.Float, Inv, out w)) _warGar[x[0]] = w;
                    }
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
