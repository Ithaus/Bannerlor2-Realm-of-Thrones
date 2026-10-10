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
    ///  - pulap zoldu (partie + zalogi + karawany): pokoj 0.28 D, wojna 0.60 D + 0.8 (G - R)/45 przy G > R; bieda: pokoj G < 60 D -> x (1 - 0.1 (1 - G/60D)),
    ///    wojna G < R -> x (0.8 + 0.2 G/R), G < 0.25 R -> x 0.5. Dwor i wyzywienie 0.35/0.20 D, sprzet 0.17 D (+0.2 (G-R)/45 w wojnie), budowy 0.10 D
    ///    (w wojnie tylko na mury, wieze i koszary - recenzja C1, W5).
    ///  - zalogi: w wojnie pulap gry bez zmian i finansowane pierwsze; w pokoju cel 50% zalogi wojennej (srednia z dob wojny), najwyzej 80% pulapu.
    ///    Partie dostaja reszte - SetWagePaymentLimit (postfiks MakeClanFinancialEvaluation; dzialaja hamulce gry: werbunek i awanse ponad limit).
    ///  - ZWOLNIENIA zamiast dezercji gry z limitu zoldu (AiWageLimitDesertionOff): zold > 1.10 x pulap przez 3 doby -> codziennie 15% nadwyzki ludzi:
    ///    najpierw zalogi ponad cel pokojowy (pokoj), potem najemnicy z karczmy, potem najnizszy tier. Ludzie do ludnosci BK najblizszej wsi rodu (zaloga -
    ///    wsi tej twierdzy), czesc sakiewki partii do kiesy tej wsi; bez wsi z danymi BK nikt nie jest zwalniany (nikt nie znika - Z6).
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
        }

        private static readonly Dictionary<Clan, B> _b = new Dictionary<Clan, B>();
        private static readonly Dictionary<string, double[]> _saved = new Dictionary<string, double[]>();   // id rodu -> [D, streak, przydzial sprzetu] z zapisu
        private static readonly Dictionary<string, float> _warGar = new Dictionary<string, float>();        // id twierdzy -> zold zalogi w wojnie (srednia)
        private static readonly Dictionary<Settlement, int> _garTarget = new Dictionary<Settlement, int>(); // dzis (pokoj): cel zoldu zalogi
        private static int _day = -1;

        // liczniki doby (linia "Budzet rodow (166)" i "Obieg")
        internal static long LastCourt, LastCourtTown, LastFood, LastFamily, LastReleasedPurse, LastBuildShare, LastGearShare;
        internal static int LastReleasedMen, LastReleasedPar, LastReleasedGar, LastNoVillage, LastFamilyN;
        private static long _dCourt, _dCourtTown, _dFood, _dFamily, _dRelPurse, _dBuild, _dGear, _dGearSpent, _dCapSum, _dWageSum, _dD;
        private static int _dRelMen, _dRelPar, _dRelGar, _dNoVillage, _dFamilyN, _dOver, _dStreak, _dPeace, _dWar, _dClans, _dPoor, _dVanished;
        private static int _dDesertOff, _dSpawnBlock, _dRecruitBlock, _dGarBlock, _dGarLimited, _dPartyLimited, _dWatchMen, _dWatchCap = -1;
        // test 120 dob (wojsko w wojnie): dwor ustepuje zoldowi (udzial dostepny, zold go zajal, u ilu rodow); limity partii z wolnego miejsca rodu
        private static long _dCourtRoom, _dCourtYield; private static int _dCourtRoomN, _dCourtYieldN, _dPartyOverCap;
        private static int _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static int _importN = -1, _importBad;

        internal static void Reset()
        {
            _b.Clear(); _saved.Clear(); _warGar.Clear(); _garTarget.Clear(); _day = -1; _stumbles = 0; _err.Clear(); _importN = -1; _importBad = 0;
            ZeroLast(); ClearDay();
            // recenzja C1 (C1-G4): uchwyty BK i obiekty poprzedniej sesji (jak LosersFlee.Reset, PopulationLaw.Reset "wpis 87") - BK tworzy przy kazdym
            // wczytaniu nowy PopulationManager kluczowany obiektami Settlement; stary menedzer nie zna nowych osad, wiec po wczytaniu innego zapisu w tym
            // samym procesie nikt nie bylby zwalniany (a dezercja gry z limitu jest wylaczona). BkResolve jest leniwe - podepnie sie do nowego menedzera.
            // Latki Harmony (_hooksTried, _harmony, _wired, _missing) zyja przez caly proces - zostaja.
            _bkTried = false; _popMgr = null; _getPopData = null; _fromSoldiers = null; _populated = null;
            _grain = null; _grainTried = false; _spawnUsed.Clear(); _spawnDay = -1;
        }

        internal static void ZeroLast()
        {
            LastCourt = LastCourtTown = LastFood = LastFamily = LastReleasedPurse = LastBuildShare = LastGearShare = 0;
            LastReleasedMen = LastReleasedPar = LastReleasedGar = LastNoVillage = LastFamilyN = 0;
        }

        private static void ClearDay()
        {
            _dCourt = _dCourtTown = _dFood = _dFamily = _dRelPurse = _dBuild = _dGear = _dCapSum = _dWageSum = _dD = 0;
            _dRelMen = _dRelPar = _dRelGar = _dNoVillage = _dFamilyN = _dOver = _dStreak = _dPeace = _dWar = _dClans = _dPoor = _dVanished = 0;
            _dGarLimited = _dPartyLimited = 0; _dWatchMen = 0; _dWatchCap = -1;
            _dCourtRoom = _dCourtYield = 0; _dCourtRoomN = _dCourtYieldN = _dPartyOverCap = 0;
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
            return Math.Max(0f, s.PeaceWageShare) * D * f;
        }

        /// <summary>Pulap zoldu policzony dzis w grze (dla linii na sucho - jedna liczba); false - rod bez budzetu.</summary>
        internal static bool TryCap(Clan c, out double cap)
        {
            var b = Of(c); cap = b != null ? b.Cap : 0; return b != null;
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
                            b.Nominal = men > 0 && ns > 0 ? (double)ns / men : Math.Max(1f, Campaign.Current.AverageWage);
                            b.MenCap = (int)(b.Cap / Math.Max(0.5, b.Nominal));
                            _dWatchMen += men; _dWatchCap = Math.Max(0, _dWatchCap) + b.MenCap;
                        }
                        else b.MenCap = -1;
                        Garrisons(c, b, s);
                        b.PartyCap = Math.Max(0, b.Cap - (GarFull(b, s) ? b.WGar : Math.Min(b.WGar, b.GarTarget)) - b.WCar);
                        if (!b.Zero && b.WPar > b.PartyCap) _dPartyOverCap++;   // rody ponad pulapem partii (limity proporcjonalnie do zoldu)
                        b.Today = true;
                        // przydzial sprzetu: niewydany z dni od ostatnich zakupow, najwyzej AiGearDaysCap dni
                        b.GearLeft = Math.Min(b.GearLeft + b.Gear, Math.Max(1f, s.AiGearDaysCap) * b.Gear);
                        _dGear += (long)b.Gear; _dBuild += (long)b.Build;
                        _dClans++; if (b.War) _dWar++; else _dPeace++;
                        _dCapSum += (long)b.Cap; _dWageSum += b.WPar + b.WGar + b.WCar; _dD += (long)b.D;
                        // kiesa rodziny, dwor, zwolnienia, limity - kazde we wlasnym try
                        try { FamilyTopUp(c, b, s); } catch (Exception e) { Stumble("FamilyTopUp", e); }
                        try { Court(c, b, s); } catch (Exception e) { Stumble("Court", e); }
                        // recenzja C1 (W3): limity przed zwolnieniami - zwolnienia dziela kwote wedlug nadwyzki partii ponad jej dzisiejszy limit
                        try { ApplyPartyLimits(c, b); ApplyGarrisonLimits(c, b); } catch (Exception e) { Stumble("Limits", e); }
                        try { Releases(c, b, s); } catch (Exception e) { Stumble("Releases", e); }
                        if (c.Leader.Gold < 5000) _dPoor++;
                    }
                    catch (Exception e) { Stumble("Daily(rod)", e); }
                }
                foreach (var kv in _b) if (!kv.Value.Today && (kv.Key == null || kv.Key.IsEliminated)) drop.Add(kv.Key);
                foreach (var c in drop) _b.Remove(c);
                Report(s, today);
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally
            {
                LastCourt = _dCourt; LastCourtTown = _dCourtTown; LastFood = _dFood; LastFamily = _dFamily; LastReleasedPurse = _dRelPurse;
                LastBuildShare = _dBuild; LastGearShare = _dGear; LastReleasedMen = _dRelMen; LastReleasedPar = _dRelPar; LastReleasedGar = _dRelGar;
                LastNoVillage = _dNoVillage; LastFamilyN = _dFamilyN;
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
            var wps = c.WarPartyComponents;
            if (wps == null || wps.Count == 0) return;
            int max = Campaign.Current.Models.PartyWageModel.MaxWagePaymentLimit;
            double wsum = 0, wage = 0;
            for (int i = 0; i < wps.Count; i++)
            {
                var mp = wps[i] != null ? wps[i].MobileParty : null;
                if (mp == null || !mp.IsLordParty) continue;
                wsum += mp.LeaderHero == c.Leader ? 1.5 : 1.0;
                wage += Math.Max(0, mp.TotalWage);
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
                    double x;
                    if (!free) x = b.PartyCap * w / wsum;                                                     // stary podzial (wylacznik)
                    else if (room >= 0) x = Math.Max(0, mp.TotalWage) + room * w / wsum;                     // swoj zold + czesc wolnego miejsca rodu
                    else x = wage > 0 ? b.PartyCap * Math.Max(0, mp.TotalWage) / wage : b.PartyCap * w / wsum; // rod ponad pulapem partii
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

        private static Settlement Seat(Clan c)
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
                double wageNow = b.Zero ? b.Nominal * (b.MenPar + b.MenGar) + b.WCar : b.WPar + b.WGar + b.WCar;
                double used = Math.Min(share, Math.Max(0, wageNow - b.CapBase));
                if (used > 0) { share -= used; _dCourtYield += (long)used; _dCourtYieldN++; }
            }
            long x = (long)share;
            x = Math.Min(x, Math.Max(0, head.Gold - Math.Max(0, s.FamilyPurseFloor)));   // dwor nie oproznia kiesy glowy ponizej podlogi rodziny
            _dFood += (long)food;
            if (x <= 0) return;
            int ix = (int)Math.Min(int.MaxValue, x);
            head.ChangeHeroGold(-ix);
            CirculationWindows.NoteHeroGold(head, -ix);            // paczka 169b: glowa poza swiatem - zloto weszlo do swiata (tylko licznik)
            seat.Town.ChangeGold(ix);                              // dwor zyje z kasy siedziby: kuchnia, sluzba, stajnie - pieniadze zostaja w osadzie
            ClanIncomeBook.NoteOwnPaid(seat, c, ix);               // 169c: "wlasne" D stalego - dwor wraca zaworem wlasnej osady (tylko licznik)
            if (seat.IsTown) { SoldierPay.HoldCourt(seat, ix); _dCourtTown += ix; }   // tarcza dworu: regulator gry nie kasuje tej wplaty
            _dCourt += ix;
        }

        // ------------------------------------------------------------ zwolnienia (15% nadwyzki dziennie po 3 dobach ponad 1.10 x pulap)
        private static void Releases(Clan c, B b, Settings s)
        {
            double hyst = Math.Max(1f, s.BudgetHysteresis);
            long wage = b.WPar + b.WGar + b.WCar;
            int men = b.MenPar + b.MenGar;
            bool over = b.Zero ? b.MenCap >= 0 && men > hyst * b.MenCap : wage > hyst * b.Cap;
            b.Streak = over ? b.Streak + 1 : 0;
            if (over) _dOver++;
            if (b.Streak < Math.Max(1, s.BudgetHysteresisDays)) return;
            _dStreak++;
            double share = Math.Max(0f, Math.Min(1f, s.ReleasePerDay));
            // do zwolnienia: w zlocie (zold dzienny) albo - Straz bez zoldu - w ludziach
            double left = b.Zero ? Math.Ceiling(share * (men - b.MenCap)) : share * (wage - b.Cap);
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

        /// <summary>Zwalnia z partii (albo zalogi) ludzi za `amount` zl zoldu dziennie (albo `amount` ludzi przy zoldzie 0), od najnizszego tieru; mercsOnly -
        /// tylko najemnicy z karczmy. Ludzie do ludnosci BK wsi; czesc sakiewki partii do kiesy wsi. Zwraca zwolnione (zl albo ludzi).</summary>
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
            foreach (var el in stacks)
            {
                if (done >= amount) break;
                var ch = el.Character;
                int w = menUnit ? 1 : Math.Max(1, wageModel.GetCharacterWage(ch));
                int n = (int)Math.Min(el.Number, Math.Ceiling((amount - done) / w));
                if (n <= 0) continue;
                int wounded = el.WoundedNumber > 0 ? Math.Min(el.WoundedNumber, (int)Math.Round(n * (double)el.WoundedNumber / el.Number)) : 0;
                if (!ToVillage(village, ch, n)) { _dNoVillage++; continue; }
                r.AddToCounts(ch, -n, false, -wounded);
                done += (double)n * w; gone += n;
            }
            if (gone <= 0) return 0;
            _dRelMen += gone; if (garrison) _dRelGar += gone; else _dRelPar += gone;
            // czesc sakiewki partii z odchodzacymi (proporcjonalnie do ludzi) - do kiesy wsi, do ktorej wracaja
            try
            {
                int purse = MenPurse.Get(mp);
                if (purse > 0 && menBefore > 0)
                {
                    int share = (int)((long)purse * gone / menBefore);
                    int took = share > 0 ? MenPurse.Take(mp, share) : 0;
                    if (took > 0) { village.SettlementComponent.ChangeGold(took); _dRelPurse += took; }
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

        private static bool ToVillage(Settlement v, CharacterObject ch, int n)
        {
            try
            {
                var pd = _getPopData.Invoke(_popMgr, new object[] { v });
                if (pd == null) return false;
                _fromSoldiers.Invoke(pd, new object[] { ch, n });
                return true;
            }
            catch (Exception e) { Stumble("ToVillage", e); return false; }
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
            if (c.Settlements != null)
                foreach (var st in c.Settlements)
                {
                    if (st == null || !st.IsVillage || !HasPop(st)) continue;
                    float d = pos.DistanceSquared(st.GetPosition2D);
                    if (d < bd) { bd = d; best = st; }
                }
            if (best != null) return best;
            var k = c.Kingdom;
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

        /// <summary>BuildFunding: dzienny przydzial budow rodu (0.10 D; w wojnie BuildFunding finansuje nim tylko budowy wojskowe - W5); false - rod bez budzetu (stara podstawa).</summary>
        internal static bool BuildShare(Clan c, out float share)
        {
            var b = Of(c); share = b != null ? (float)Math.Max(0, b.Build) : 0f; return b != null;
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
            try { var b = Of(__0); if (b != null) ApplyPartyLimits(__0, b); }
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
                if (__0 == null || s == null || !s.AiWageLimitDesertionOff || !(__0.IsLordParty || __0.IsGarrison) || Of(OwnerOf(__0)) == null || !__0.HasLimitedWage()) return;
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
                if (b.Zero) { cost = need; room = b.MenCap < 0 || b.MenCap - LiveMen(c) - used >= need; }
                else
                {
                    double avg = b.MenPar > 0 ? (double)b.WPar / b.MenPar : Math.Max(1f, Campaign.Current.AverageWage);
                    cost = need * avg;
                    room = b.PartyCap - b.WPar - used >= cost;
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

        /// <summary>Straz bez zoldu: werbunek partii staje na pulapie w ludziach (limit w zlocie przy zoldzie 0 nie wiaze).</summary>
        public static bool RecruitPrefix(MobileParty __0)
        {
            try
            {
                if (__0 == null || !CrownGifts.IsWatchParty(__0)) return true;
                var c = OwnerOf(__0);
                var b = Of(c);
                if (b == null || !b.Zero || b.MenCap < 0) return true;
                if (LiveMen(c) < b.MenCap) return true;
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
                if (b == null || !b.Zero || b.MenCap < 0) return true;
                if (LiveMen(st.OwnerClan) < b.MenCap) return true;
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
              .Append(" | w wojnie dwor ustepuje zoldowi: udzial dworu w pulapie ").Append(_dCourtRoom).Append(" u ").Append(_dCourtRoomN)
              .Append(" rodow, zold go zajal (dwor nie dostal) ").Append(_dCourtYield).Append(" u ").Append(_dCourtYieldN).Append(" rodow")
              .Append(s.WarCourtYieldsToWages ? "" : " (wylaczone)")
              .Append(" | limity partii ").Append(s.PartyLimitsShareFreeRoom ? "z wolnego miejsca rodu" : "staly podzial 1.5 : 1")
              .Append(", rody ponad pulapem partii ").Append(_dPartyOverCap)
              .Append(" | ponad 1.10 x pulap ").Append(_dOver).Append(" rodow, od 3 dob (zwalniaja) ").Append(_dStreak)
              .Append(" | zwolnieni: do wsi ").Append(_dRelMen).Append(" ludzi (z partii ").Append(_dRelPar).Append(", z zalog ").Append(_dRelGar).Append("), zniklo ").Append(_dVanished)
              .Append(", nie zwolniono - brak wsi z danymi BK ").Append(_dNoVillage).Append(", sakiewki zwolnionych do kies wsi ").Append(_dRelPurse).Append(" zl")
              .Append(" | dezercja gry z limitu zoldu wylaczona (rody z budzetem): ").Append(_dDesertOff).Append(" wywolan")
              .Append(" | limity: partie zmienione ").Append(_dPartyLimited).Append(", zalogi przyciete do celu pokojowego ").Append(_dGarLimited)
              .Append(", nowe partie wstrzymane ").Append(_dSpawnBlock)
              .Append(" | dwor (162m): wplacone do kas siedzib ").Append(_dCourt).Append(" (w tym miasta pod tarcza dworu ").Append(_dCourtTown).Append("), jedzenie partii (szacunek, odjete) ").Append(_dFood)
              .Append(", ").Append(SoldierPay.CourtNote())
              .Append(" | kiesa rodziny do glow: ").Append(_dFamily).Append(" zl w ").Append(_dFamilyN).Append(" przelewach; glowy < 5000 (rody z budzetem): ").Append(_dPoor)
              .Append(" | sprzet: przydzial dnia ").Append(_dGear).Append(", wydane przez panow od wczoraj ").Append(_dGearSpent)
              .Append(" | budowy: przydzial dnia ").Append(_dBuild)
              .Append(" | Straz w ludziach ").Append(_dWatchMen).Append(" / pulap w ludziach ").Append(_dWatchCap < 0 ? "-" : _dWatchCap.ToString(Inv))
              .Append(" (werbunek wstrzymany ").Append(_dRecruitBlock).Append(", przyrost zalog wstrzymany ").Append(_dGarBlock).Append(")")
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
