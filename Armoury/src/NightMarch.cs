using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// PACZKA T10 - NOCNY MARSZ AI (Jeff 09.10 ok. 04:00: samotne kolumny lordow "maszeruja noca, gdy trzeba - spiesza sie,
    /// by przerwac rabunek, uciekaja przed armia - ale maja miec takie same kary jak gracz"). Projekt:
    /// docs/PROJEKT-T10-NOCNY-MARSZ-2026-10-09.md (z rozdzialem "Krytyka i odpowiedzi").
    ///
    /// R1 - POWOD NOCNEGO MARSZU (samotna partia lorda AI i wodz armii, w godzinach obozu swiata): idzie dalej tylko gdy
    ///   UCIECZKA (gra sama uznala, ze trzeba uciekac - MobileParty.IsFleeing), POSCIG (cel EngageParty wrogi i w zasiegu
    ///   dlugosci obozu w godzinach marszu), ODSIECZ (cel DefendSettlement: wlasna wies lupiona W TEJ CHWILI, do 2 x dlugosc
    ///   obozu w h marszu; oblezenie i odsiecz armia tylko przy AiNightReliefWider - pytanie do Jeffa). ALARM (silniejszy wrog,
    ///   ktory nie spi i idzie na lorda, w promieniu AiCampDangerRadius) nie jest powodem marszu: budzi lorda, AI ma 0.5 h na
    ///   ucieczke, inaczej lord kladzie sie z powrotem (jego rozkaz sprzed snu zostaje zapamietany). Reszta spi. Lord w osadzie
    ///   bez powodu nie wyjezdza w godzinach obozu (spi pod dachem). Poscig i odsiecz tylko przy dlugu < AiNightsAwakeInChase.
    /// R2 - TA SAMA KSIEGA SNU CO GRACZ dla kazdej partii lorda AI (bez Nieumarlych): co godzine +1 h (21:00-swit) albo
    ///   +DayRestFactor (dzien), gdy partia stoi (< 0.35 jedn./h), jest w osadzie, na morzu (SleepAtSeaFree) albo lezy w obozie;
    ///   o swicie doba bez bazy = dlug +1 (maks. 3); splata calej sumy od reki = dlug 0. Kary te same tablice co gracz
    ///   (SpeedPostfix / MoralePostfix w NightRest.cs).
    /// R3 - SPLATA JAK U GRACZA: dlug 1 = oboz od 20:00 do konca obozu swiata (10 h pelnej stawki, NeededHours(1) = 9), dlug 2-3
    ///   = sen ciagly tam, gdzie stoi (licznik jak pasek snu gracza, swit nie dolicza dlugu spiacemu). Osobny slownik _debtSleep -
    ///   galaz switu obozu swiata go nie rusza. Ucieczka i alarm przerywaja sen.
    /// Pomiar: linie "AiNightCamp: marsz" (co godzine obozu), "AiNightCamp: ruch" (niezalezny pomiar z pozycji: kto przesunal sie
    /// > 0.35 jedn. w godzinie obozu i co T10 o nim wiedzial), "NocnyMarsz: swit" i "NocnyMarsz: kara - probka" (glowny log).
    /// Na sucho: wylaczniki MCM albo DLL budowany z -p:T10Dry=true (stala T10_DRY) - ksiega i klasyfikacja tylko w logu,
    /// zachowanie i kary jak w T1.
    ///
    /// T10-R - AI MUSI ODPOCZYWAC (decyzja Jeffa 10.10, wiazaca: "musza odpoczywac; najwyzej 2 dni forsownego marszu, tylko w sytuacjach
    ///   wyjatkowych"). Test 120 dob: 11-15 partii na dlugu 3 (morale -95%) przez ok. 2 tygodnie (Dorne: czlonkowie armii Dorana Martella) -
    ///   czlonek armii nie mogl wejsc w sen dlugu (doczepiony), a wodz z dlugiem 0 gonil noca dalej; oboz oblezenia, oblezona osada i morze
    ///   tez nie daja snu ciaglego, a dlug 3 wymaga 21 h naraz. Do tego ksiega gubila 1-5 tikow na dobe (numer godziny z zaokraglenia czasu,
    ///   a tick godzinowy gry ma dowolna faze) - zgubiona godzina obozu 0-6 to 5 h zamiast 6 i dlug 1 dla polowy swiata (doby 24, 27, 68...).
    ///   R4 - OBOWIAZKOWY ODPOCZYNEK: seria nocy bez snu (doby bez bazy z rzedu) albo dlug >= MaxForcedNights -> partia (a w armii wodz za cala
    ///   armie) nie idzie noca ani za poscigiem, ani na odsiecz, ani w ucieczce, spi snem dlugu az do dlugu 0; jedyny wyjatek to ucieczka przed
    ///   wrogiem co najmniej CrushRatio razy silniejszym (zniszczylby ja) - licznik wyjatkow w linii switu. Alarm przy obowiazkowym odpoczynku
    ///   tez tylko od takiego wroga.
    ///   R5 - ARMIA ODPOCZYWA RAZEM: wodz decyduje wedlug najbardziej zmeczonej partii (dlug i odpoczynek obowiazkowy z wodza i doczepionych):
    ///   oboz splaty od 20:00 albo sen ciagly calej armii; doczepieni spia z wodzem (SleepsWithLeader).
    ///   R6 - SEN CIAGLY NA MIEJSCU: partia z dlugiem >= 2, ktorej nie wolno polozyc snem dlugu (oboz oblezenia, oblezona osada, morze, doczepiona
    ///   do armii, AI trzyma inny mod), a ktora w tej godzinie odpoczywa - licznik snu ciaglego (Acc) biegnie jak w snie dlugu; ruch go konczy.
    ///   R7 - KAZDY TICK GODZINOWY TO JEDNA GODZINA (jak ksiega gracza): numer godziny ksiegi z licznika wywolan, nie z czasu; tick nadrabiany
    ///   w tej samej klatce (dwie godziny naraz) powtarza stan odpoczynku poprzedniej godziny, a swit liczy sie raz.
    /// </summary>
    internal static partial class NightRest
    {
#if T10_DRY
        private const bool DryBuild = true;
#else
        private const bool DryBuild = false;
#endif
        private const int DebtCampHour = 20;          // oboz splaty dlugu 1: ticki 21..6 = 10 h pelnej stawki (1 h zapasu nad 9 h)
        private const float RestMoveLimit = Drill.RestStep;   // jak gracz (OnHourly) i musztra (grupa11: jedna stala 0.35): ponizej - partia stoi
        private const double AlarmGraceHours = 0.5;   // AI sprawdza inicjatywe co ok. 0.15-0.18 h (AiCheckInterval 0.25 x 0.6-0.7)
        private const int AiSaveCap = 1500;           // wpisow ksiegi w zapisie (lordow ROT ok. 500-700)
        // T10-R (R4): wrog, ktory "by ja zniszczyl" - co najmniej dwa razy silniejszy (sila armii albo partii, jak przy ucieczce w grze);
        // tylko przed takim partia w obowiazkowym odpoczynku ucieka dalej
        private const float CrushRatio = 2f;

        // co T10 zrobil z partia w ostatnim ticku - do niezaleznego pomiaru ruchu w nastepnej godzinie
        private const byte StNone = 0, StSkip = 1, StSlept = 2, StReason = 3, StAlarm = 4, StDebt = 5, StOldLot = 6, StOldOther = 7, StOther = 8;

        private enum NReason : byte { None = 0, Flee = 1, Chase = 2, Relief = 3 }

        private sealed class AiSleep
        {
            public int Debt;                 // 0..3 jak Debt gracza
            public float Rest;               // odpoczynek biezacej doby (swit-swit) - jak _restTonight gracza
            public bool Credited;            // splata od reki w tej dobie juz byla - jak _credited gracza
            public float Acc = -1f;          // sen ciagly (dlug 2-3): licznik od polozenia sie, swit go nie zeruje (jak pasek snu gracza); -1 = nie spi
            public Vec2 Pos; public long Stamp = -1;   // pozycja i numer godziny ostatniego odczytu
            public double SinceH;            // od ktorej godziny kampanii w ksiedze (pierwszy swit liczy sie dopiero po 18 h)
            public byte State; public long StateStamp = -1;
            public byte AlarmEnd;            // wynik alarmu: 1 ucieczka, 2 spi dalej, 3 inny marsz
            public int NightFlags;           // bity NReason marszu w godzinach obozu tej doby (8 = alarm -> ucieczka)
            public int DawnDebt;             // poprawka recenzji (P5): dlug zaraz po poprzednim swicie - losy dlugu 1 do nastepnego switu; MUSZTRA-j: kara
                                             // musztry "noc bez snu = dzien bez cwiczen" (NightRest.DawnDebtOf), w zapisie szoste pole wpisu
            public bool PaidSinceDawn;       // splata od reki od poprzedniego switu
            // T10-R: seria dob bez bazy snu z rzedu (forsowny marsz noca) - zeruje ja przespana baza albo splata; w zapisie siodme pole
            public int Streak;
            public int D3;                   // T10-R: ile switow z rzedu na dlugu 3 (cel testu: nikt > 2 poza ucieczka); w zapisie osme pole
            public bool Passive;             // T10-R (R6): w ostatniej godzinie sen ciagly na miejscu (bez snu dlugu) - tylko w pamieci
            public bool LastRest;            // T10-R (R7): czy ostatnia godzina byla odpoczynkiem - powtarzana w ticku nadrabianym
            public int CrushDay = -1;        // T10-R: dzien ostatniej ucieczki-wyjatku (wrog >= CrushRatio) - tylko w pamieci, do linii switu
        }

        private sealed class DebtSleeper { public NightOrder Order; public Vec2 Bed; public int Kind; }
        private sealed class AlarmInfo { public double AtH; public Vec2 Pos; public bool DebtPath; public NightOrder Order; }

        private static readonly Dictionary<MobileParty, AiSleep> _ai = new Dictionary<MobileParty, AiSleep>();
        // kary AI czytane rownolegle (predkosc) - tylko podmiana calego obiektu, nigdy zmiana w miejscu
        private static volatile Dictionary<MobileParty, int> _aiPenalty = new Dictionary<MobileParty, int>();
        private static readonly Dictionary<MobileParty, DebtSleeper> _debtSleep = new Dictionary<MobileParty, DebtSleeper>();
        private static readonly Dictionary<MobileParty, AlarmInfo> _alarmed = new Dictionary<MobileParty, AlarmInfo>();
        private static readonly HashSet<MobileParty> _townHold = new HashSet<MobileParty>();
        private static Dictionary<MobileParty, Vec2> _prevPos = new Dictionary<MobileParty, Vec2>();
        private static readonly NightOrder EmptyOrder = new NightOrder { Behavior = AiBehavior.Hold };

        private static string _aiPending;            // wczytany napis ksiegi - rozwiazywany przy pierwszym ticku (partie juz istnieja)
        private static bool _aiImportFresh;          // pierwszy tick po wczytaniu: partie spoza zapisu mialy stan zerowy - pelna doba
        private static bool? _debtWasOn;
        private static long _ledgerStamp = -1;
        private static CampaignTime _lastDebtSweep = CampaignTime.Zero, _lastAlarmSweep = CampaignTime.Zero;
        private static double _debtDhMax, _alarmDhMax;
        private static int _exampleN, _aiStumbles;
        private static int _alarmFled, _alarmSlept, _alarmOther, _alarmResets, _alarmBattle, _debtWokenHour;
        // poprawka recenzji: liczniki alarmow i straznika snu dlugu zamykane CO GODZINE (AiHourly) - linia nocna pokazuje wartosci tej godziny,
        // a zdarzenia dzienne nie trafiaja do linii 0:00
        private static int _hFled, _hSlept, _hOther, _hResets, _hBattle, _hDebtWoken;
        private static double _ledgerMsSum, _ledgerMsMax; private static int _ledgerCalls;
        private static bool _nrWasOn;                // przelacznik glowny NightRestEnabled byl wlaczony (zmiana na wylaczony = jednorazowe sprzatanie)
        private static PropertyInfo _enableAtProp; private static bool _enableAtTried;
        // T10-R (R7): numer godziny ksiegi = licznik wywolan AiHourly (tick godzinowy gry ma dowolna faze - zaokraglony czas dawal ten sam
        // numer dwom tikom i ksiega gubila godzine); czas gry poprzedniego wywolania (nadrabianie w jednej klatce) i ostatniego switu
        private static long _hourTick;
        private static double _lastTickH = -1, _lastDawnH = -1;
        private static int _restExN;

        private sealed class DayCounters
        {
            public int Paid, NoFullDay, Marched, MarchedNoDebt, Collapses, EveningCamps, ContSleeps, Released, DebtReason, DebtAlarm;
            public readonly int[] NewDebt = new int[4];
            public readonly int[] PaidBy = new int[4];        // splacone od reki wedlug poziomu dlugu
            public int StillCollapsed;                         // dlug 3 bez snu kolejna doba - zapasc trwa (nie nowa)
            public int C1Paid, C1Up, C1Stay;                   // dlug 1 z poprzedniego switu: splacony / wzrosl do 2 / dalej 1
            public int DebtForeign, VillageAlarm, Gone, GoneDebt, SeaMove;
            public int HFlee, HChase, HRelief, HAlarm, HAlarmFled, HAlarmSlept, HAlarmOther, HTownHeld, HTownOut;
            public int MoveHours, MoveLone, MoveNoReason, MoveExit, MoveWoken, MoveAlarmStay, MoveDebtWoken, MoveOther, MoveLead, MoveLeadNoReason;
            public int DebtWoken;
            // T10-R: obowiazkowy odpoczynek (nowe o tym switcie), marsz zablokowany regula (partio-godziny wedlug powodu i partie),
            // wyjatki (ucieczka przed wrogiem >= CrushRatio), wodzowie trzymani dlugiem czlonkow, sen ciagly na miejscu (R6), ticki nadrabiane
            public int MustNew, BlockFlee, BlockChase, BlockRelief, BlockAlarm, CrushHours, ArmyHeld, PassiveHours, PassivePaid, CatchUps;
            public readonly HashSet<MobileParty> BlockedP = new HashSet<MobileParty>();
            public readonly HashSet<MobileParty> CrushP = new HashSet<MobileParty>();
            // stopery (P7): petla obozu swiata, oboz splaty, straznicy T10 - ms na wywolanie
            public double CampMsSum, CampMsMax, DebtMsSum, DebtMsMax, GuardMsSum, GuardMsMax;
            public int CampCalls, DebtCalls, GuardCalls;
        }
        private static DayCounters _day = new DayCounters();

        private sealed class NightTally
        {
            public int LoneField, LoneSleep, LoneFlee, LoneChase, LoneRelief, LoneAlarm;
            public int LeadField, LeadSleep, LeadFlee, LeadChase, LeadRelief, LeadAlarm;
            public int TownHeld, TownOut, TownForeign, TownAlarm, Sea, DebtField, DebtTown, AlarmPending, CaravansLot;
            // na sucho (stare reguly T1 dzialaja, T10 tylko liczy)
            public int DLoneField, DLoneMarch, DLeadField, DLeadMarch, DFlee, DChase, DRelief, DAlarm;
            public int DNoReason, DNoLot, DNoGather, DNoChase, DNoEnemy, DLeadNoReason;

            public void Count(bool leader, NReason r)
            {
                if (leader)
                {
                    if (r == NReason.Flee) LeadFlee++; else if (r == NReason.Chase) LeadChase++; else if (r == NReason.Relief) LeadRelief++;
                }
                else
                {
                    if (r == NReason.Flee) LoneFlee++; else if (r == NReason.Chase) LoneChase++; else if (r == NReason.Relief) LoneRelief++;
                }
            }
        }

        private sealed class MoveTally
        {
            public int Lone, Reason, AlarmFled, AlarmStay, Woken, DebtWoken, Exit, OldLot, OldOther, Other, NoEntry, Lead, LeadReason;
            public int Sea;   // poprawka recenzji: zeglujacy (wyjatek W2, legalny) osobno - nie wchodza do "BEZ WPISU POWODU" (P2)
            public int NoReason { get { return Lone - Reason - AlarmFled; } }

            public void Add(AiSleep e, bool leader, long prevStamp)
            {
                byte st = e.StateStamp == prevStamp ? e.State : StNone;
                bool reason = st == StReason || (st == StAlarm && e.AlarmEnd == 1);
                if (leader) { Lead++; if (reason) LeadReason++; return; }
                Lone++;
                switch (st)
                {
                    case StReason: Reason++; break;
                    case StAlarm: if (e.AlarmEnd == 1) AlarmFled++; else AlarmStay++; break;
                    case StSlept: Woken++; break;
                    case StDebt: DebtWoken++; break;
                    case StSkip: Exit++; break;
                    case StOldLot: OldLot++; break;
                    case StOldOther: OldOther++; break;
                    case StOther: Other++; break;
                    default: NoEntry++; break;
                }
            }
        }

        // ------------------------------------------------------------ wspolne
        private static bool LedgerOn(Settings s) { return s != null && s.AiCampsAtNight && CampStart != CampEnd; }
        private static bool DebtOn(Settings s) { return LedgerOn(s) && s.AiSleepDebt && !DryBuild; }
        private static bool ByReason(Settings s) { return s != null && s.AiNightMarchByReason && !DryBuild; }
        /// <summary>Dlugosc okna obozu swiata w godzinach (0-6 -> 6) = zasieg poscigu w h marszu; odsiecz 2x.</summary>
        private static int CampLen() { int st = CampStart, e = CampEnd; return st == e ? 0 : Mod24(e - st); }
        // T10-R (R7): numer godziny ksiegi z licznika tikow (bylo: Math.Round(czas w h) - przy fazie ticku ok. 0.5 h dwa ticki z rzedu dostawaly
        // ten sam numer, drugi wypadal z ksiegi, a pomiar ruchu laczyl dwie godziny w jedna)
        private static long HourStamp() { return _hourTick; }
        private static double NowH() { return CampaignTime.Now.ToHours; }
        private static bool IsLeader(MobileParty mp) { return mp.Army != null && mp.Army.LeaderParty == mp; }
        private static string F1(float v) { return v.ToString("0.0", CultureInfo.InvariantCulture); }
        private static string F0(float v) { return v.ToString("0", CultureInfo.InvariantCulture); }
        private static float NeededAi(float baza, int debt) { return baza + (debt > 0 ? 3f * (2 * debt - 1) : 0f); }

        /// <summary>Okno obozu splaty dlugu 1: od DebtCampHour do konca obozu swiata (przez polnoc).</summary>
        private static bool InDebtEvening(int h)
        {
            int st = CampStart, e = CampEnd, x = Mod24(h);
            if (st == e) return false;
            return DebtCampHour < e ? (x >= DebtCampHour && x < e) : (x >= DebtCampHour || x < e);
        }

        private static void EnableAi(MobileParty mp) { try { mp.Ai.EnableAi(); } catch { } }

        // grupa11: hak dla musztry (dawne AiDebtOf) zastapiony jedna funkcja NightRest.DebtOf (NightRest.cs, sekcja kar) - gracz i AI z tej samej
        // ksiegi, ktora zabiera predkosc i morale; musztra wola ja wprost (Drill.SleepDebt), bez haka wpinanego przy starcie.
        // MUSZTRA-j: musztra czyta juz dlug O SWICIE (NightRest.DawnDebtOf - AiSleep.DawnDebt z SettleAi); DebtOf tylko do linii (kontrola).

        /// <summary>grupa11: ksiega daje AI prawdziwy dlug (przelacznik glowny, oboz swiata, AiSleepDebt, nie na sucho) - linia startowa musztry i (MUSZTRA-j)
        /// warunek NightRest.DawnDebtOf.</summary>
        internal static bool AiDebtLive(Settings s) { return s != null && s.NightRestEnabled && DebtOn(s); }

        /// <summary>
        /// AI partii trzyma INNY mod dluzej niz nasza godzina: DisableAi (uczta / statek BK - "nigdy") albo DisableForHours z dluzszym
        /// terminem (gentry BK 72 h). Nasze DisableForHours(1) daje termin <= 1 h, wiec > 1.5 h = cudza blokada, ktorej nie wolno skracac
        /// (MobilePartyAi.DisableForHours nadpisuje _enableAgainAtHour, EnableAi ja zdejmuje). Odczyt prywatnej wlasciwosci gry przez odbicie.
        /// </summary>
        private static bool ForeignHold(MobileParty mp)
        {
            try
            {
                if (mp == null || mp.Ai == null || !mp.Ai.IsDisabled) return false;
                if (!_enableAtTried)
                {
                    _enableAtTried = true;
                    _enableAtProp = typeof(MobilePartyAi).GetProperty("_enableAgainAtHour", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (_enableAtProp == null) Log.Info("NocnyMarsz: brak MobilePartyAi._enableAgainAtHour - cudza blokade AI rozpoznaje tylko przy wejsciu w sen dlugu.");
                }
                if (_enableAtProp == null) return false;
                var at = (CampaignTime)_enableAtProp.GetValue(mp.Ai, null);
                return (at - CampaignTime.Now).ToHours > 1.5;
            }
            catch (Exception ex) { AiStumble("ForeignHold", mp, ex); return false; }
        }

        /// <summary>Doczepiony do wodza, ktory spi snem ciaglym (sen dlugu, dlug 2-3) - spi razem z nim (poprawka recenzji: ten sam licznik).
        /// grupa11-p: wodzem moze byc gracz spiacy w menu ("Bed down", _sleeping) - jego lordowie spia z nim przez swit tak jak on (SettleNight: kto spi,
        /// dlugu nie dostaje; LeaveSleep: caly sen do doby), inaczej o swicie gracz bez dlugu, a jego lordowie z dlugiem (morale i dzien musztry).</summary>
        private static bool SleepsWithLeader(MobileParty mp)
        {
            var lead = mp != null ? mp.AttachedTo : null;
            if (lead == null) return false;
            if (lead == MobileParty.MainParty) return _sleeping;
            DebtSleeper ds;
            return _debtSleep.TryGetValue(lead, out ds) && ds.Kind == 2;
        }

        /// <summary>
        /// Cel poscigu, dla ktorego warto zarwac noc (poprawka recenzji, slowa Jeffa "gdy trzeba"): partia lorda albo armia (takze gracz)
        /// i banda, ktora wlasnie walczy (napada kogos). Karawana, wiesniacy i banda w marszu - to lup, nie potrzeba: noca nie.
        /// </summary>
        private static bool ChaseWorthy(MobileParty tgt)
        {
            if (tgt == MobileParty.MainParty) return true;
            if (tgt.IsCaravan || tgt.IsVillager) return false;
            if (tgt.IsLordParty) return true;
            return tgt.IsBandit && tgt.MapEvent != null;
        }

        private static NightOrder Capture(MobileParty mp)
        {
            return new NightOrder { Behavior = mp.DefaultBehavior, Settlement = mp.TargetSettlement, Party = mp.TargetParty };
        }

        private static void Mark(AiSleep e, byte state, NReason r = NReason.None)
        {
            if (e == null) return;
            e.State = state; e.StateStamp = HourStamp();
            if (state == StAlarm) e.AlarmEnd = 0;
            if (state == StReason && r != NReason.None && InCamp(CampaignTime.Now.GetHourOfDay)) e.NightFlags |= 1 << (int)r;
        }

        private static void AiStumble(string where, MobileParty mp, Exception e)
        {
            _aiStumbles++;
            if (_aiStumbles <= 3 || _aiStumbles % 100 == 0)
            {
                string who = "?";
                try { who = mp != null ? mp.StringId : "null"; } catch { }
                Log.Error(where + " (partia " + who + ", potkniecie " + _aiStumbles + " w sesji)", e);
            }
        }

        private static float Strength(MobileParty p)
        {
            try
            {
                if (p.Army != null && p.Army.LeaderParty == p) return p.Army.EstimatedStrength;
                return p.Party != null ? p.Party.EstimatedStrength : 0f;
            }
            catch { return 0f; }
        }

        private static bool Hostile(MobileParty a, MobileParty b)
        {
            if (b.IsBandit) return !a.IsBandit;
            var fa = a.MapFaction; var fb = b.MapFaction;
            return fa != null && fb != null && fa != fb && fa.IsAtWarWith(fb);
        }

        /// <summary>Jednostek mapy na godzine: partia robi Speed / 1.2 jedn./h (A07 1.1: Campaign.cs - 4320 s gry na jednostke dt).</summary>
        private static float UnitsPerHour(MobileParty mp)
        {
            float sp = 0f;
            try { sp = mp.Speed; } catch { }
            if (sp <= 0f) sp = 2.4f;
            return Math.Max(0.05f, sp / 1.2f);
        }

        /// <summary>Godziny marszu do osady: odleglosc z modelu gry (siatka osad - woda, zatoki, przesmyki), zapasowo 1.1 x linia prosta (A07 1.4).</summary>
        private static float HoursToSettlement(MobileParty mp, Settlement st, out float straight, out float route)
        {
            straight = mp.GetPosition2D.Distance(st.GatePosition.ToVec2());
            route = -1f;
            try
            {
                float lr;
                float d = Campaign.Current.Models.MapDistanceModel.GetDistance(mp, st, false, MobileParty.NavigationType.Default, out lr);
                if (d > 0f && d < 1000000f) route = d;
            }
            catch { }
            if (route < 0f) route = straight * 1.1f;
            return route / UnitsPerHour(mp);
        }

        // ------------------------------------------------------------ R4-R5: obowiazkowy odpoczynek, armia odpoczywa razem
        /// <summary>T10-R: MaxForcedNights z ustawien (0 = bez limitu - stare reguly).</summary>
        private static int MaxForced(Settings s) { return s != null ? Math.Max(0, s.MaxForcedNights) : 2; }

        /// <summary>T10-R (R4): obowiazkowy odpoczynek - seria nocy bez snu albo dlug >= MaxForcedNights; trwa, az dlug zejdzie do 0
        /// (splata zeruje serie, a dlug nie maleje czesciowo, wiec warunek trzyma do pelnej splaty).</summary>
        private static bool Must(AiSleep e, int max) { return e != null && max > 0 && e.Debt > 0 && (e.Debt >= max || e.Streak >= max); }

        /// <summary>
        /// T10-R (R5, Jeff 10.10: "jesli partia jest w armii, decyduje wodz"): dlug, wedlug ktorego decyduje partia - samotny lord swoj, wodz armii
        /// najgorszy z siebie i doczepionych (czlonek nie moze sam polozyc sie spac, wiec odpoczywa razem z armia); must - obowiazkowy odpoczynek
        /// kogokolwiek z nich. Czlonek armii zwraca swoj (o nim i tak decyduje wodz).
        /// </summary>
        private static int EffDebt(MobileParty mp, AiSleep e, int max, out bool must)
        {
            int d = e != null ? e.Debt : 0;
            must = Must(e, max);
            if (mp == null || !IsLeader(mp)) return d;
            try
            {
                var att = mp.AttachedParties;
                if (att != null)
                    for (int i = 0; i < att.Count; i++)
                    {
                        var m = att[i]; AiSleep me;
                        if (m == null || m == MobileParty.MainParty || !_ai.TryGetValue(m, out me)) continue;
                        if (me.Debt > d) d = me.Debt;
                        if (Must(me, max)) must = true;
                    }
            }
            catch (Exception ex) { AiStumble("EffDebt", mp, ex); }
            return d;
        }

        /// <summary>T10-R: sila wroga tak, jak liczy ja gra przy ucieczce - wodz i doczepieni armii sila calej armii.</summary>
        private static float ThreatStrength(MobileParty p)
        {
            try
            {
                if (p.Army != null && (p.Army.LeaderParty == p || p.AttachedTo == p.Army.LeaderParty)) return p.Army.EstimatedStrength;
            }
            catch { }
            return Strength(p);
        }

        /// <summary>T10-R (R4): czy ucieczka jest wyjatkiem - wrog, przed ktorym partia ucieka (cel ucieczki gry), albo czuwajacy wrog idacy na nia
        /// w promieniu alarmu jest co najmniej CrushRatio razy silniejszy (zniszczylby ja).</summary>
        private static bool Crushing(MobileParty mp, MobileParty from, Settings s, out string how)
        {
            how = null;
            float own = Strength(mp);
            try
            {
                if (from != null && from.IsActive && Hostile(mp, from))
                {
                    float fs = ThreatStrength(from);
                    if (fs >= CrushRatio * own)
                    {
                        how = from.Name + " (sila " + F0(fs) + " >= " + F1(CrushRatio) + " x " + F0(own) + ")";
                        return true;
                    }
                }
            }
            catch (Exception ex) { AiStumble("Crushing", mp, ex); }
            float ts, ms; string h;
            if (AlarmThreat(mp, s, null, CrushRatio, out ts, out ms, out h) != null) { how = h; return true; }
            return false;
        }

        /// <summary>T10-R (R4): alarm - przy obowiazkowym odpoczynku budzi tylko wrog, ktory by partie zniszczyl; slabszy (silniejszy od partii,
        /// ale ponizej CrushRatio) liczony jako marsz zablokowany regula (blockedBefore - ta godzina juz policzona jako zablokowana ucieczka).</summary>
        private static MobileParty AlarmFor(MobileParty mp, Settings s, HashSet<MobileParty> campSet, bool must, NReason blockedBefore, out string how)
        {
            float ts, ms;
            var th = AlarmThreat(mp, s, campSet, must ? CrushRatio : 1f, out ts, out ms, out how);
            if (th != null || !must || blockedBefore != NReason.None) return th;
            string h2;
            if (AlarmThreat(mp, s, campSet, 1f, out ts, out ms, out h2) != null)
            {
                _day.BlockAlarm++; _day.BlockedP.Add(mp);
                RestExample(mp, "alarm zablokowany (wrog silniejszy, ale nie zniszczylby jej): " + h2 + " - spi dalej", null);
            }
            return null;
        }

        /// <summary>T10-R: liczniki reguly R4 z wyniku Classify - marsz zablokowany (partia w obowiazkowym odpoczynku spi) albo wyjatek (ucieczka
        /// przed wrogiem >= CrushRatio).</summary>
        private static void CountRule(MobileParty mp, AiSleep e, NReason blocked, bool crush, string det)
        {
            if (blocked != NReason.None)
            {
                if (blocked == NReason.Flee) _day.BlockFlee++; else if (blocked == NReason.Chase) _day.BlockChase++; else if (blocked == NReason.Relief) _day.BlockRelief++;
                _day.BlockedP.Add(mp);
                RestExample(mp, "marsz zablokowany - " + det + " - odpoczywa", e);
            }
            if (crush)
            {
                _day.CrushHours++; _day.CrushP.Add(mp);
                if (e != null) e.CrushDay = (int)CampaignTime.Now.ToDays;
                RestExample(mp, "WYJATEK - " + det, e);
            }
        }

        private static void RestExample(MobileParty mp, string what, AiSleep e)
        {
            _restExN++;
            if (_restExN > 5 && _restExN % 25 != 0) return;
            try
            {
                if (e == null) _ai.TryGetValue(mp, out e);
                Log.Info("NocnyMarsz: odpoczynek - " + mp.Name + (IsLeader(mp) ? " (wodz armii)" : "") + ": " + what + " (dlug " + (e != null ? e.Debt : 0)
                         + ", seria nocy bez snu " + (e != null ? e.Streak : 0) + ") [przyklad " + _restExN + " w sesji]");
            }
            catch { }
        }

        /// <summary>T10-R: gdzie jest partia - do linii switu (dlug 3 dluzej niz 2 doby).</summary>
        private static string Where(MobileParty mp)
        {
            try
            {
                if (!mp.IsActive) return "nieaktywna (rejs BK)";
                if (mp.AttachedTo != null) return "w armii " + (mp.AttachedTo.LeaderHero != null ? mp.AttachedTo.LeaderHero.Name.ToString() : mp.AttachedTo.Name.ToString());
                string w = IsLeader(mp) ? "wodz armii, " : "";
                if (_debtSleep.ContainsKey(mp)) return w + "sen dlugu";
                if (mp.BesiegerCamp != null) return w + "oboz oblezenia";
                if (mp.MapEvent != null) return w + "bitwa";
                if (mp.IsCurrentlyAtSea) return w + "na morzu";
                var st = mp.CurrentSettlement;
                if (st != null) return w + (st.SiegeEvent != null ? "oblezona osada " : "w osadzie ") + st.Name + (mp.Ai != null && mp.Ai.IsDisabled && !_townHold.Contains(mp) ? " (AI trzyma inny mod)" : "");
                return w + "w polu" + (mp.IsFleeing() ? " (ucieka)" : "");
            }
            catch { return "?"; }
        }

        // ------------------------------------------------------------ R1: powod
        /// <summary>
        /// Powod nocnego marszu. slept != null = partia spi (AI wylaczone, rozkaz Hold) - liczy sie rozkaz zapamietany przed snem.
        /// Ucieczka zawsze; poscig i odsiecz tylko przy dlugu < AiNightsAwakeInChase (lord z dlugiem traci wiecej, niz zyska).
        /// T10-R (R4): must = obowiazkowy odpoczynek - zaden powod nie wazy (blocked = powod zablokowany regula), poza ucieczka przed wrogiem
        /// >= CrushRatio (crush = wyjatek). debt i must u wodza armii - najgorsze z armii (EffDebt).
        /// </summary>
        private static NReason Classify(MobileParty mp, Settings s, int debt, bool must, bool leader, NightOrder slept, out string detail,
                                        out NReason blocked, out bool crush)
        {
            detail = null; blocked = NReason.None; crush = false;
            if (slept == null && mp.IsFleeing())
            {
                var from = mp.ShortTermTargetParty;
                detail = "ucieczka" + (from != null ? " przed " + from.Name + " (sila " + F0(ThreatStrength(from)) + " vs " + F0(Strength(mp)) + ")" : "");
                if (must)
                {
                    string how;
                    if (!Crushing(mp, from, s, out how)) { blocked = NReason.Flee; return NReason.None; }
                    crush = true;
                    detail += " - obowiazkowy odpoczynek, ale " + how + " zniszczylby ja: ucieka dalej";
                }
                return NReason.Flee;
            }
            if (debt >= s.AiNightsAwakeInChase) return NReason.None;
            var defB = slept != null ? slept.Behavior : mp.DefaultBehavior;
            var defS = slept != null ? slept.Settlement : mp.TargetSettlement;
            var defP = slept != null ? slept.Party : mp.TargetParty;
            var stB = slept != null ? AiBehavior.None : mp.ShortTermBehavior;
            var stP = slept != null ? null : mp.ShortTermTargetParty;
            int len = Math.Max(1, CampLen());
            bool wider = s.AiNightReliefWider;
            // ODSIECZ: rabunek (albo oblezenie - tylko przy AiNightReliefWider) trwa W TEJ CHWILI; napastnik "zyje" nie wystarcza
            // (gra trzyma LastAttackerParty jeszcze dobe po zagrozeniu - SettlementVariablesBehavior)
            if (defB == AiBehavior.DefendSettlement && defS != null && (!leader || wider) && defS.MapFaction == mp.MapFaction)
            {
                bool raid = defS.IsUnderRaid;
                bool siege = wider && defS.IsUnderSiege;
                if (raid || siege)
                {
                    float straight, route;
                    float hr = HoursToSettlement(mp, defS, out straight, out route);
                    if (hr <= 2f * len)
                    {
                        detail = "odsiecz " + defS.Name + " (" + (raid ? "rabunek" : "oblezenie") + ", " + F1(hr) + " h marszu, trasa " + F1(route)
                                 + " jedn., prosto " + F1(straight) + ")";
                        if (must) { blocked = NReason.Relief; return NReason.None; }
                        return NReason.Relief;
                    }
                }
            }
            // POSCIG / PRZECHWYCENIE: wrogi lord / armia albo banda w walce, ktorych dogoni jeszcze tej nocy (linia prosta x 1.1 jak trasa
            // w A07 1.4); karawana i wiesniacy to lup, nie potrzeba - noca nie (poprawka recenzji)
            MobileParty tgt = stB == AiBehavior.EngageParty ? stP : (defB == AiBehavior.EngageParty ? defP : null);
            if (tgt != null && tgt.IsActive && Hostile(mp, tgt) && ChaseWorthy(tgt))
            {
                float d = mp.GetPosition2D.Distance(tgt.GetPosition2D);
                float hr = d * 1.1f / UnitsPerHour(mp);
                if (hr <= len)
                {
                    detail = "poscig za " + tgt.Name + " (" + F1(hr) + " h marszu, prosto " + F1(d) + " jedn., trasa ok. " + F1(d * 1.1f) + ")";
                    if (must) { blocked = NReason.Chase; return NReason.None; }
                    return NReason.Chase;
                }
            }
            return NReason.None;
        }

        /// <summary>
        /// ALARM: wrogi lord, banda albo gracz w promieniu AiCampDangerRadius, ktory NIE SPI (poza obozem, poza snem dluznikow,
        /// z wlaczonym AI), jest SILNIEJSZY (miara gry przy ucieczce: sila armii albo partii) i IDZIE NA LORDA (cel = lord albo
        /// odleglosc zmalala od poprzedniej godziny o > 0.2 jedn.). Wyszukiwanie przez lokator mapy (jak gra), nie po wszystkich.
        /// T10-R: ratio - ile razy silniejszy ma byc wrog (1 = silniejszy; CrushRatio = zniszczylby partie w obowiazkowym odpoczynku).
        /// </summary>
        private static MobileParty AlarmThreat(MobileParty mp, Settings s, HashSet<MobileParty> campSet, float ratio, out float ts, out float ms, out string how)
        {
            ts = 0f; ms = 0f; how = null;
            float r = s.AiCampDangerRadius;
            if (r <= 0f) return null;
            ms = Strength(mp);
            var pos = mp.GetPosition2D;
            var main = MobileParty.MainParty;
            Vec2 myPrev;
            bool havePrev = _prevPos.TryGetValue(mp, out myPrev);
            var data = MobileParty.StartFindingLocatablesAroundPosition(pos, r);
            for (var t = MobileParty.FindNextLocatable(ref data); t != null; t = MobileParty.FindNextLocatable(ref data))
            {
                try
                {
                    if (t == mp || !t.IsActive) continue;
                    if (!t.IsLordParty && !t.IsBandit && t != main) continue;
                    if (t.CurrentSettlement != null || t.MapEvent != null || t.BesiegerCamp != null || t.AttachedTo != null) continue;
                    if (t.IsCurrentlyAtSea != mp.IsCurrentlyAtSea) continue;
                    if (mp.Army != null && t.Army == mp.Army) continue;
                    if (!Hostile(mp, t)) continue;
                    float dist = pos.Distance(t.GetPosition2D);
                    if (dist > r) continue;
                    // nie spi
                    if (t == main) { if (PlayerCamped || _sleeping) continue; }
                    else if ((campSet != null && campSet.Contains(t)) || _debtSleep.ContainsKey(t) || (t.Ai != null && t.Ai.IsDisabled)) continue;
                    // silniejszy (T10-R: przy ratio > 1 - tyle razy silniejszy)
                    float st = Strength(t);
                    if (st <= ms * ratio) continue;
                    // idzie na lorda
                    string why = null;
                    if (t.ShortTermTargetParty == mp || t.TargetParty == mp) why = "idzie na niego";
                    else
                    {
                        Vec2 tPrev;
                        if (havePrev && _prevPos.TryGetValue(t, out tPrev) && dist < myPrev.Distance(tPrev) - 0.2f)
                            why = "zbliza sie (" + F1(myPrev.Distance(tPrev)) + " -> " + F1(dist) + " jedn.)";
                    }
                    if (why == null) continue;
                    ts = st; how = t.Name + " (sila " + F0(st) + " > " + (ratio > 1.001f ? F1(ratio) + " x " : "") + F0(ms) + ", " + F1(dist) + " jedn., " + why + ")";
                    return t;
                }
                catch (Exception ex) { AiStumble("AlarmThreat", t, ex); }   // poprawka recenzji: liczone i w logu, nie polykane
            }
            return null;
        }

        private static void Example(MobileParty mp, string what, int debt, bool leader)
        {
            _exampleN++;
            if (_exampleN <= 3 || _exampleN % 50 == 0)
            {
                try
                {
                    Log.Info("AiNightCamp: marsz - " + mp.Name + (leader ? " (wodz armii)" : "") + ": " + what + ", dlug " + debt
                             + " [przyklad " + _exampleN + " w sesji]");
                }
                catch { }
            }
        }

        // ------------------------------------------------------------ godzina AI (z OnHourly, przed wyjsciami gracza)
        private static void AiHourly(Settings s, int h)
        {
            FlushHourCounters();
            // T10-R (R7): kazde wywolanie to jedna godzina gry (gra wola tick godzinowy raz na kazda minieta godzine - CampaignPeriodicEvent.CheckUpdate);
            // dwa wywolania w tej samej chwili gry to nadrabianie w jednej klatce - ksiega powtarza wtedy stan odpoczynku poprzedniej godziny
            _hourTick++;
            double nowH = NowH();
            bool catchUp = _lastTickH >= 0 && nowH >= _lastTickH && nowH - _lastTickH < 0.5;
            _lastTickH = nowH;
            if (catchUp) _day.CatchUps++;
            try { AiSleepLedger(s, h, catchUp); } catch (Exception e) { Log.Error("NightRest.AiSleepLedger", e); }
            long t0 = Stopwatch.GetTimestamp();
            try { AiDebtCamp(s, h); } catch (Exception e) { Log.Error("NightRest.AiDebtCamp", e); }
            long t1 = Stopwatch.GetTimestamp();
            AiNightCamp(s, h);
            long t2 = Stopwatch.GetTimestamp();
            // stopery P7 (poprawka recenzji): oboz splaty i petla obozu swiata - do linii switu
            double dms = (t1 - t0) * 1000.0 / Stopwatch.Frequency, cms = (t2 - t1) * 1000.0 / Stopwatch.Frequency;
            _day.DebtCalls++; _day.DebtMsSum += dms; if (dms > _day.DebtMsMax) _day.DebtMsMax = dms;
            _day.CampCalls++; _day.CampMsSum += cms; if (cms > _day.CampMsMax) _day.CampMsMax = cms;
            AiBanditRest(s, h);
            try { SnapshotPositions(s); } catch (Exception e) { Log.Error("NightRest.SnapshotPositions", e); }
        }

        /// <summary>Liczniki alarmow i straznika snu dlugu z minionej godziny: do doby i do linii nocnej tej godziny, potem od zera.</summary>
        private static void FlushHourCounters()
        {
            _hFled = _alarmFled; _hSlept = _alarmSlept; _hOther = _alarmOther; _hResets = _alarmResets; _hBattle = _alarmBattle; _hDebtWoken = _debtWokenHour;
            _day.HAlarmFled += _alarmFled; _day.HAlarmSlept += _alarmSlept; _day.HAlarmOther += _alarmOther; _day.DebtWoken += _debtWokenHour;
            _alarmFled = 0; _alarmSlept = 0; _alarmOther = 0; _alarmResets = 0; _alarmBattle = 0; _debtWokenHour = 0;
        }

        /// <summary>Stoper straznikow T10 (OnTick) - do linii switu.</summary>
        private static void GuardTime(long t0)
        {
            double ms = MsSince(t0);
            _day.GuardCalls++; _day.GuardMsSum += ms; if (ms > _day.GuardMsMax) _day.GuardMsMax = ms;
        }

        /// <summary>Nowa gra albo wczytanie: ksiega AI od zera (zapis wczytuje ImportAi po tym).</summary>
        private static void ResetAi()
        {
            _ai.Clear(); _aiPenalty = new Dictionary<MobileParty, int>(); _debtSleep.Clear(); _alarmed.Clear(); _townHold.Clear();
            _prevPos = new Dictionary<MobileParty, Vec2>();
            _aiPending = null; _aiImportFresh = false; _debtWasOn = null; _ledgerStamp = -1;
            _lastDebtSweep = CampaignTime.Zero; _lastAlarmSweep = CampaignTime.Zero; _debtDhMax = 0; _alarmDhMax = 0;
            _alarmFled = 0; _alarmSlept = 0; _alarmOther = 0; _alarmResets = 0; _alarmBattle = 0; _debtWokenHour = 0;
            _hFled = 0; _hSlept = 0; _hOther = 0; _hResets = 0; _hBattle = 0; _hDebtWoken = 0;
            _ledgerMsSum = 0; _ledgerMsMax = 0; _ledgerCalls = 0;
            _nrWasOn = false;
            _hourTick = 0; _lastTickH = -1; _lastDawnH = -1; _restExN = 0;
            _day = new DayCounters();
        }

        /// <summary>
        /// Przelacznik glowny NightRestEnabled wylaczony w trakcie gry (poprawka recenzji): OnHourly konczy sie wtedy przed AiHourly,
        /// wiec nikt by nie zwolnil spiacych. Jednorazowo wszystko wraca do starego zachowania: spiacy obozu swiata i alarmowani wstaja
        /// z rozkazem sprzed snu, sen dlugu i wstrzymani w osadach zwolnieni, namioty zdjete, ksiega AI wyczyszczona, kary AI zdjete.
        /// Wolane z OnTick i OnHourly; ponowne wlaczenie = ksiega od zera.
        /// </summary>
        internal static void MasterSwitch(Settings s)
        {
            if (s == null) return;   // brak ustawien (chwila ladowania) - nic nie zmieniamy
            bool on = s.NightRestEnabled;
            if (on) { _nrWasOn = true; return; }
            if (!_nrWasOn) return;
            _nrWasOn = false;
            try
            {
                ReleaseAllDebt();
                foreach (var kv in new List<KeyValuePair<MobileParty, AlarmInfo>>(_alarmed))
                {
                    try { if (kv.Key != null && kv.Value.DebtPath) ApplyOrder(kv.Key, kv.Value.Order, false); }
                    catch (Exception ex) { AiStumble("MasterSwitch", kv.Key, ex); }
                }
                _alarmed.Clear();
                foreach (var mp in _townHold) EnableAi(mp);
                _townHold.Clear();
                foreach (var mp in _camping) if (mp != null && mp.IsActive) EnableAi(mp);
                foreach (var mp in _tented) Tent(mp, false);
                _tented.Clear(); _tentPos.Clear();
                foreach (var mp in new List<MobileParty>(_orders.Keys)) GiveOrderBack(mp);
                _orders.Clear();
                int sleeping = _camping.Count;
                _camping.Clear(); _bedPos.Clear();
                _ai.Clear();
                RebuildPenalties(false);
                _debtWasOn = null; _ledgerStamp = -1;
                _prevPos = new Dictionary<MobileParty, Vec2>();
                Log.Info("NocnyMarsz: NightRestEnabled wylaczony - spiacy obozu swiata (" + sleeping + ") i alarmowani wstali z rozkazem sprzed snu, "
                         + "sen dlugu zwolniony, ksiega snu AI wyczyszczona, kary AI zdjete (stare zachowanie).");
            }
            catch (Exception e) { Log.Error("NightRest.MasterSwitch", e); }
        }

        /// <summary>Pozycje lordow, band i gracza z konca ticku - ALARM w nastepnej godzinie sprawdza, czy wrog sie zbliza.</summary>
        private static void SnapshotPositions(Settings s)
        {
            if (!LedgerOn(s)) { if (_prevPos.Count > 0) _prevPos = new Dictionary<MobileParty, Vec2>(); return; }
            var d = new Dictionary<MobileParty, Vec2>(Math.Max(64, _prevPos.Count));
            var main = MobileParty.MainParty;
            foreach (var mp in MobileParty.All)
            {
                if (mp == null || !mp.IsActive) continue;
                if (!mp.IsLordParty && !mp.IsBandit && mp != main) continue;
                d[mp] = mp.GetPosition2D;
            }
            _prevPos = d;
        }

        // ------------------------------------------------------------ R1 w obozie swiata
        /// <summary>
        /// Lord w osadzie w godzinie obozu: z powodem wyjezdza, bez powodu zostaje na noc. Wystarczy wstrzymac AI na godzine:
        /// gra nie wypuszcza z osady partii z wylaczonym AI (MobileParty.CheckExitingSettlementParallel: Ai.IsDisabled -> zostaje),
        /// a rozkaz lorda zostaje nietkniety (bez Hold i bez zapamietywania - o swicie po prostu jedzie dalej). Lorda, ktoremu AI
        /// wylaczyl kto inny (uczta / gentry BK - DisableAi), nie ruszamy: i tak nie wyjedzie, a krotszy zamek zepsulby rozkaz BK.
        /// Poprawka recenzji: wies nie jest obronna - lord we wsi, na ktorego idzie silniejszy, czuwajacy wrog (ALARM), dostaje wolne AI
        /// (gra ocenia ucieczke), zamiast czekac do rana; w miescie i zamku mury chronia.
        /// </summary>
        private static void TownNight(MobileParty mp, Settings s, AiSleep e, bool debtOn, HashSet<MobileParty> campSet, NightTally t)
        {
            var st = mp.CurrentSettlement;
            bool held = _townHold.Contains(mp);
            if (mp.AttachedTo != null || st.SiegeEvent != null || Undead.Party(mp))
            {
                if (held) { _townHold.Remove(mp); EnableAi(mp); }
                Mark(e, StSkip);
                return;
            }
            if (_debtSleep.ContainsKey(mp)) { t.DebtTown++; Mark(e, StDebt); return; }
            if (!held && mp.Ai != null && mp.Ai.IsDisabled) { t.TownForeign++; Mark(e, StSkip); return; }   // AI trzyma kto inny (np. BK)
            // inny mod przejal AI w nocy (dluzsza blokada niz nasza godzina) - jego blokada zostaje, nie skracamy jej
            if (held && ForeignHold(mp)) { _townHold.Remove(mp); t.TownForeign++; Mark(e, StSkip); return; }
            bool leader = IsLeader(mp);
            // T10-R (R4, R5): wodz armii decyduje wedlug najgorszego dlugu armii; obowiazkowy odpoczynek blokuje wyjazd (poza ucieczka-wyjatkiem)
            bool must = false;
            int debt = debtOn && e != null ? EffDebt(mp, e, MaxForced(s), out must) : 0;
            string det; NReason blk; bool crush;
            var r = Classify(mp, s, debt, must, leader, null, out det, out blk, out crush);   // rozkaz lorda nietkniety - liczy sie biezacy
            CountRule(mp, e, blk, crush, det);
            if (r != NReason.None)
            {
                if (held) { _townHold.Remove(mp); EnableAi(mp); }
                t.TownOut++; t.Count(leader, r);
                Mark(e, StReason, r);
                Example(mp, det + " - wyjezdza z " + st.Name, debt, leader);
                return;
            }
            if (!st.IsFortification)
            {
                string how;
                if (AlarmFor(mp, s, campSet, must, blk, out how) != null)
                {
                    if (held) { _townHold.Remove(mp); EnableAi(mp); }
                    t.TownAlarm++; _day.VillageAlarm++;
                    Mark(e, StSkip);
                    Example(mp, "ALARM we wsi " + st.Name + " - " + how + " - AI wolne (ocena ucieczki)", debt, leader);
                    return;
                }
            }
            mp.Ai.DisableForHours(1);
            _townHold.Add(mp);
            t.TownHeld++;
            Mark(e, StSkip);
        }

        /// <summary>Samotny lord albo wodz armii w polu w godzinie obozu (T10 R1).</summary>
        private static void LordNight(MobileParty mp, Settings s, AiSleep e, HashSet<MobileParty> campSet, bool debtOn, NightTally t)
        {
            AlarmInfo al;
            if (_alarmed.TryGetValue(mp, out al))
            {
                if (NowH() - al.AtH < 0.01) { t.AlarmPending++; return; }   // alarm z tego samego ticku (sciezka dlugu)
                _alarmed.Remove(mp);                                          // stary alarm - rozstrzyga nowa godzina
            }
            if (mp.IsCurrentlyAtSea) { t.Sea++; Mark(e, StOther); return; }   // W2: straze na pokladzie (gracz tez)
            if (mp.AttachedTo != null) { Mark(e, StOther); return; }            // doczepieni ida z wodzem
            if (Undead.Party(mp)) { Mark(e, StOther); return; }                 // Inni nie spia
            bool leader = IsLeader(mp);
            if (leader) t.LeadField++; else t.LoneField++;
            bool asleep = campSet.Contains(mp);
            NightOrder mine = null;
            if (asleep) _orders.TryGetValue(mp, out mine);
            // T10-R (R4, R5): wodz armii decyduje wedlug najgorszego dlugu armii; obowiazkowy odpoczynek - tylko ucieczka-wyjatek
            bool must = false;
            int debt = debtOn && e != null ? EffDebt(mp, e, MaxForced(s), out must) : 0;
            string det; NReason blk; bool crush;
            var r = Classify(mp, s, debt, must, leader, asleep ? (mine ?? EmptyOrder) : null, out det, out blk, out crush);
            CountRule(mp, e, blk, crush, det);
            if (r != NReason.None)
            {
                if (asleep)
                {
                    // spiacy z rozkazem, ktory wlasnie stal sie powodem (np. jego wies wlasnie lupia) - wstaje z tym rozkazem
                    if (_tented.Contains(mp)) { Tent(mp, false); _tented.Remove(mp); }
                    _camping.Remove(mp); _bedPos.Remove(mp);
                    GiveOrderBack(mp);
                    EnableAi(mp);
                }
                t.Count(leader, r);
                CountDayReason(r);
                Mark(e, StReason, r);
                Example(mp, "idzie noca: " + det, debt, leader);
                return;
            }
            string how;
            var th = AlarmFor(mp, s, campSet, must, blk, out how);
            if (th != null)
            {
                // ALARM: wstaje, stary cel wstrzymany (bez powodu nie idzie), AI ocenia ucieczke; rozkaz sprzed snu zostaje w _orders
                if (asleep)
                {
                    if (_tented.Contains(mp)) { Tent(mp, false); _tented.Remove(mp); }
                    _camping.Remove(mp); _bedPos.Remove(mp);
                }
                else RememberOrder(mp);
                mp.SetMoveModeHold();
                EnableAi(mp);
                _alarmed[mp] = new AlarmInfo { AtH = NowH(), Pos = mp.GetPosition2D, DebtPath = false };
                if (leader) t.LeadAlarm++; else t.LoneAlarm++;
                _day.HAlarm++;
                Mark(e, StAlarm);
                Example(mp, "ALARM - " + how + " - AI ocenia ucieczke", debt, leader);
                return;
            }
            RememberOrder(mp);              // po co wyszedl - zapisane przed snem
            mp.Ai.DisableForHours(1);       // spia godzine; nocny tick odnowi
            mp.SetMoveModeHold();
            if (!asleep) { _camping.Add(mp); _bedPos[mp] = mp.GetPosition2D; }
            if (leader) t.LeadSleep++; else t.LoneSleep++;
            Mark(e, StSlept);
        }

        private static void CountDayReason(NReason r)
        {
            if (r == NReason.Flee) _day.HFlee++; else if (r == NReason.Chase) _day.HChase++; else if (r == NReason.Relief) _day.HRelief++;
        }

        /// <summary>Na sucho (stare reguly T1): co powiedzialaby regula T10. false = nie dotyczy (doczepiony, morze, Nieumarli).</summary>
        private static bool DryClassify(MobileParty mp, Settings s, AiSleep e, HashSet<MobileParty> campSet, out NReason r, out bool alarm)
        {
            r = NReason.None; alarm = false;
            if (mp.AttachedTo != null || mp.IsCurrentlyAtSea || Undead.Party(mp)) return false;
            bool leader = IsLeader(mp);
            bool asleep = campSet.Contains(mp);
            NightOrder mine = null;
            if (asleep) _orders.TryGetValue(mp, out mine);
            string det; NReason blk; bool crush;
            // poprawka recenzji: na sucho dlug nie ma splaty (bez obozu od 20:00 i snu ciaglego), wiec rosnie sztucznie do 3 - klasyfikacja
            // z dlugiem 0, inaczej poscig i odsiecz bylyby odciete i P0 zanizalby "szloby z powodem"; T10-R: na sucho bez obowiazkowego odpoczynku
            r = Classify(mp, s, 0, false, leader, asleep ? (mine ?? EmptyOrder) : null, out det, out blk, out crush);
            if (r == NReason.None)
            {
                float a, b; string how;
                alarm = AlarmThreat(mp, s, campSet, 1f, out a, out b, out how) != null;
            }
            return true;
        }

        private static void DryCount(MobileParty mp, string cause, NReason r, bool alarm, NightTally t)
        {
            bool leader = IsLeader(mp);
            bool march = cause != null;
            if (leader) { t.DLeadField++; if (march) t.DLeadMarch++; }
            else { t.DLoneField++; if (march) t.DLoneMarch++; }
            if (r == NReason.Flee) t.DFlee++; else if (r == NReason.Chase) t.DChase++; else if (r == NReason.Relief) t.DRelief++;
            else if (alarm) t.DAlarm++;
            if (march && r == NReason.None)
            {
                if (leader) { t.DLeadNoReason++; return; }
                t.DNoReason++;
                if (cause == "los") t.DNoLot++;
                else if (cause == "armia") t.DNoGather++;
                else if (cause == "poscig" || cause == "ucieczka") t.DNoChase++;
                else if (cause == "wrog") t.DNoEnemy++;
            }
        }

        private static void LogNightTally(int h, bool byReason, NightTally t)
        {
            var sb = new StringBuilder();
            if (byReason)
            {
                int loneGo = t.LoneFlee + t.LoneChase + t.LoneRelief, leadGo = t.LeadFlee + t.LeadChase + t.LeadRelief;
                sb.Append("AiNightCamp: marsz ").Append(h).Append(":00 - samotnych lordow w polu ").Append(t.LoneField)
                  .Append(": spi ").Append(t.LoneSleep).Append(", ida noca ").Append(loneGo)
                  .Append(" (ucieczka ").Append(t.LoneFlee).Append(", poscig ").Append(t.LoneChase).Append(", odsiecz ").Append(t.LoneRelief)
                  .Append("), alarm - wstali ocenic zagrozenie ").Append(t.LoneAlarm)
                  .Append("; wodzowie armii w polu ").Append(t.LeadField).Append(": spi ").Append(t.LeadSleep).Append(", ida ").Append(leadGo)
                  .Append(" (ucieczka ").Append(t.LeadFlee).Append(", poscig ").Append(t.LeadChase).Append(", odsiecz ").Append(t.LeadRelief)
                  .Append("), alarm ").Append(t.LeadAlarm)
                  .Append("; w osadach: zostaja na noc ").Append(t.TownHeld).Append(", wyjechali z powodem ").Append(t.TownOut)
                  .Append(", AI trzyma inny mod (np. uczta BK) ").Append(t.TownForeign).Append(", alarm we wsi - AI wolne ").Append(t.TownAlarm)
                  .Append("; na morzu ").Append(t.Sea).Append("; dluznicy w snie dlugu: w polu ").Append(t.DebtField).Append(", w osadach ").Append(t.DebtTown)
                  .Append(" (razem w snie dlugu ").Append(_debtSleep.Count).Append(", alarm dluznika w tym ticku ").Append(t.AlarmPending).Append(")")
                  .Append("; karawany z losowania ").Append(t.CaravansLot);
                // P4 (poprawka recenzji): ta sama populacja co w T1 - lordowie spiacy W POLU (oboz swiata + sen dlugu w polu); w osadach osobno
                int campLords = 0;
                foreach (var mp in _camping) if (mp != null && mp.IsLordParty) campLords++;
                sb.Append(". P4: lordow spiacych w polu ").Append(campLords + t.DebtField).Append(" (oboz swiata ").Append(campLords)
                  .Append(" + sen dlugu w polu ").Append(t.DebtField).Append("), w osadach na noc ").Append(t.TownHeld + t.DebtTown)
                  .Append(", udzial spiacych samotnych w polu ").Append(t.LoneField > 0 ? (100f * t.LoneSleep / t.LoneField).ToString("0", CultureInfo.InvariantCulture) : "-").Append('%')
                  .Append(". BEZ POWODU 0 z konstrukcji - kto naprawde szedl, mowi linia 'AiNightCamp: ruch'.");
            }
            else
            {
                int would = t.DFlee + t.DChase + t.DRelief;
                sb.Append("AiNightCamp: marsz ").Append(h).Append(":00 [NA SUCHO - dzialaja stare reguly T1] - samotnych lordow w polu ").Append(t.DLoneField)
                  .Append(": idzie po staremu ").Append(t.DLoneMarch).Append(", spi ").Append(t.DLoneField - t.DLoneMarch)
                  .Append("; wodzowie armii w polu ").Append(t.DLeadField).Append(": idzie ").Append(t.DLeadMarch)
                  .Append(" | wedlug T10 (samotni i wodzowie) szloby z powodem ").Append(would).Append(" (ucieczka ").Append(t.DFlee)
                  .Append(", poscig ").Append(t.DChase).Append(", odsiecz ").Append(t.DRelief).Append("), alarm ").Append(t.DAlarm)
                  .Append("; BEZ POWODU wedlug T10 samotnych ").Append(t.DNoReason).Append(" (los ").Append(t.DNoLot).Append(", w drodze na zbiorke ")
                  .Append(t.DNoGather).Append(", poscig daleko / cel nie wrog / dlug ").Append(t.DNoChase).Append(", wrog blisko slabszy albo spiacy ")
                  .Append(t.DNoEnemy).Append("), wodzow ").Append(t.DLeadNoReason)
                  .Append("; dluznicy w snie dlugu ").Append(t.DebtField + t.DebtTown).Append("; na morzu ").Append(t.Sea)
                  .Append("; karawany z losowania ").Append(t.CaravansLot).Append('.');
            }
            // wartosci minionej godziny (FlushHourCounters na poczatku AiHourly) - dzienne zdarzenia sa w swoich godzinach, nie tutaj
            sb.Append(" Alarmy od poprzedniej godziny: ucieczka ").Append(_hFled).Append(", spi dalej ").Append(_hSlept)
              .Append(", inny marsz ").Append(_hOther).Append(" (cel AI cofniety ").Append(_hResets).Append(", bitwa/osada ").Append(_hBattle)
              .Append("); dluznicy obudzeni cudza reka ").Append(_hDebtWoken).Append('.');
            Log.Info(sb.ToString());
            _day.HTownHeld += t.TownHeld; _day.HTownOut += t.TownOut;
        }

        // ------------------------------------------------------------ R2: ksiega snu AI
        private static void AiSleepLedger(Settings s, int h, bool catchUp)
        {
            if (!LedgerOn(s))
            {
                if (_ai.Count > 0 || _debtSleep.Count > 0 || _aiPenalty.Count > 0)
                    ClearAiLedger("oboz swiata wylaczony (AiCampsAtNight albo rowne godziny obozu) - ksiega snu AI wyczyszczona, kary zdjete");
                return;
            }
            long stamp = HourStamp();
            if (stamp == _ledgerStamp) return;          // drugi tick w tej samej godzinie - nie liczyc dwa razy
            _ledgerStamp = stamp;
            long t0 = Stopwatch.GetTimestamp();
            bool imported = ResolveImport();
            bool on = DebtOn(s);
            if (_debtWasOn != on)
            {
                if (_debtWasOn.HasValue || !on)
                    ResetDebts(on ? "ksiega dlugu AI wlaczona - dlugi AI od zera"
                                  : "ksiega dlugu AI tylko w logu (na sucho) - kary i oboz splaty zdjete, dlugi liczone od zera");
                _debtWasOn = on;
            }
            int dawn = PlayerDawn;
            bool night = h >= 21 || h <= Math.Min(dawn, 12);      // jak gracz (OnHourly)
            float dayF = MBMath.ClampFloat(s.DayRestFactor, 0.1f, 1f);
            float baza = Math.Max(1f, s.SleepHoursNeeded);
            bool campPast = InCamp(h - 1);                         // godzina, ktora wlasnie minela, byla godzina obozu
            double nowH = NowH();
            // T10-R (R7): swit rozlicza sie raz - tick nadrabiany w tej samej klatce ma te sama godzine doby
            bool isDawn = h == dawn && (_lastDawnH < 0 || nowH - _lastDawnH > 12.0);
            if (isDawn) _lastDawnH = nowH;
            int maxF = MaxForced(s);
            var mt = campPast ? new MoveTally() : null;
            bool changed = imported;   // wczytane dlugi - kary od pierwszego ticku
            var lords = MobileParty.AllLordParties;
            var main = MobileParty.MainParty;
            var seen = isDawn ? new HashSet<MobileParty>() : null;   // partie, ktore jeszcze istnieja (lista lordow gry)
            if (lords != null)
            {
                for (int i = 0; i < lords.Count; i++)
                {
                    var mp = lords[i];
                    try
                    {
                        // poprawka recenzji: partia chwilowo nieaktywna (rejs statkiem BK: IsActive=false na czas podrozy) zostaje w ksiedze
                        // i liczy sie jak gracz w tym samym rejsie (stoi w miejscu = odpoczynek, swit jak kazdemu) - rejs nie kasuje dlugu
                        if (mp == null || mp == main) continue;
                        if (seen != null) seen.Add(mp);
                        if (Undead.Party(mp)) { if (_ai.Remove(mp)) changed = true; continue; }   // umarli nie znaja dlugu snu
                        AiSleep e;
                        if (!_ai.TryGetValue(mp, out e))
                        {
                            // po wczytaniu partia spoza zapisu miala stan zerowy (zapis trzyma kazdy niezerowy wpis) - pelna doba
                            e = new AiSleep { SinceH = _aiImportFresh ? nowH - 48.0 : nowH };
                            _ai[mp] = e;
                        }
                        var pos = mp.GetPosition2D;
                        bool known = e.Stamp == stamp - 1;
                        // poprawka recenzji (krytyczne): jak gracz (OnHourly: moved = _hadPos && ...) - bez poprzedniego odczytu (nowa partia,
                        // pierwszy tick po wczytaniu) godzina liczy sie jako postoj; known zostaje tylko dla pomiaru ruchu
                        float step = known ? pos.Distance(e.Pos) : 0f;
                        bool moved = step > RestMoveLimit;
                        // doczepiony do wodza we snie ciaglym spi razem z nim - ten sam licznik (swit go nie zeruje)
                        bool withLead = SleepsWithLeader(mp);
                        if (e.Acc < 0f) { if (withLead) e.Acc = e.Rest; }
                        // grupa11-p: gracz wstal (LeaveSleep) - jego doczepieni koncza sen ciagly od reki, przed switem tej godziny, jak gracz (LeaveSleep
                        // dopisuje sen do doby od razu, wiec swit liczy juz tylko odpoczynek doby); AiDebtCamp robilby to dopiero po swicie i tylko przy
                        // czynnym dlugu AI. Bez czynnego dlugu (na sucho, AiSleepDebt wylaczony) sen ciagly bierze sie tylko od gracza - konczy sie tu kazdy.
                        else if (!withLead && (mp.AttachedTo == main || !on) && !_debtSleep.ContainsKey(mp)) LeaveAcc(e);
                        // grupa11: czesc wspolna z gracza i musztra - Drill.RestHour (osada, oboz obleznikow, krok <= 0.35 jedn.);
                        // morze, oboz swiata (_bedPos) i sen dlugu to zasady snu - dochodza tylko tutaj
                        bool resting = Drill.RestHour(mp, step) || (s.SleepAtSeaFree && mp.IsCurrentlyAtSea)
                                       || _bedPos.ContainsKey(mp) || _debtSleep.ContainsKey(mp);
                        // T10-R (R7): tick nadrabiany w tej samej klatce - pozycja sie nie zmienila, wiec krok 0 dalby darmowy odpoczynek;
                        // ta godzina wyglada jak poprzednia
                        if (catchUp && known) resting = e.LastRest;
                        e.LastRest = resting;
                        // T10-R (R6): sen ciagly na miejscu - dlug >= 2, odpoczywa, a snem dlugu polozyc jej nie wolno albo jeszcze nie lezy
                        // (oboz oblezenia, oblezona osada, morze, czlonek armii, AI trzyma inny mod); licznik biegnie jak w snie dlugu, ruch go konczy.
                        // Doczepieni do gracza - jak dotad (o snie decyduje gracz).
                        bool inDebtSleep = _debtSleep.ContainsKey(mp);
                        bool passive = on && resting && e.Debt >= 2 && !withLead && !inDebtSleep && mp.AttachedTo != main;
                        if (passive) { if (e.Acc < 0f) e.Acc = e.Rest; _day.PassiveHours++; }
                        else if (e.Passive && !withLead && !inDebtSleep) LeaveAcc(e);
                        e.Passive = passive;
                        if (resting)
                        {
                            float inc = night ? 1f : dayF;
                            e.Rest += inc;
                            if (e.Acc >= 0f) e.Acc += inc;
                        }
                        if (mt != null && known && moved && mp.IsActive && mp.AttachedTo == null && mp.CurrentSettlement == null && mp.MapEvent == null)
                        {
                            if (mp.IsCurrentlyAtSea) mt.Sea++;   // zeglujacy (W2) - osobno, poza "bez wpisu powodu"
                            else mt.Add(e, IsLeader(mp), stamp - 1);
                        }
                        e.Pos = pos; e.Stamp = stamp;
                        // splata od reki (jak CreditRest gracza): cala suma -> dlug 0
                        float eff = e.Acc >= 0f ? Math.Max(e.Rest, e.Acc) : e.Rest;
                        if (!e.Credited && eff >= NeededAi(baza, e.Debt))
                        {
                            e.Credited = true;
                            if (e.Debt > 0)
                            {
                                _day.PaidBy[Math.Min(3, e.Debt)]++; e.Debt = 0; e.PaidSinceDawn = true; _day.Paid++; changed = true;
                                if (e.Passive) _day.PassivePaid++;
                                e.Streak = 0; e.D3 = 0;   // T10-R: splata konczy obowiazkowy odpoczynek i serie
                            }
                        }
                        if (isDawn && SettleAi(e, baza, nowH, maxF)) changed = true;
                    }
                    catch (Exception ex) { AiStumble("AiSleepLedger", mp, ex); }
                }
            }
            if (isDawn && seen != null)
            {
                // z ksiegi wypada tylko partia zniszczona (zniknela z listy lordow gry), nie chwilowo nieaktywna
                var dead = new List<MobileParty>();
                foreach (var kv in _ai)
                    if (kv.Key == null || !seen.Contains(kv.Key)) dead.Add(kv.Key);
                foreach (var m in dead)
                {
                    AiSleep g;
                    if (_ai.TryGetValue(m, out g) && g.Debt > 0) _day.GoneDebt++;
                    _day.Gone++;
                    _ai.Remove(m);
                    if (m == null) continue;
                    _debtSleep.Remove(m); _alarmed.Remove(m); _townHold.Remove(m);
                    changed = true;
                }
            }
            if (changed) RebuildPenalties(on);
            _aiImportFresh = false;
            if (mt != null) LogMove(h, mt);
            double ms = MsSince(t0);
            _ledgerCalls++; _ledgerMsSum += ms; if (ms > _ledgerMsMax) _ledgerMsMax = ms;
            if (isDawn)
            {
                try { LogDawn(on); } catch (Exception ex) { Log.Error("NocnyMarsz.LogDawn", ex); }
                if (on) { try { LogPenaltySample(); } catch (Exception ex) { Log.Error("NocnyMarsz.LogPenaltySample", ex); } }
                _day = new DayCounters();
                _ledgerCalls = 0; _ledgerMsSum = 0; _ledgerMsMax = 0;
            }
        }

        /// <summary>Swit ksiegi AI - jak SettleNight gracza: kto nie przespal bazy (i nie spi snem ciaglym), temu dlug +1 (maks. 3).
        /// T10-R: seria nocy bez snu (doba bez bazy = noc forsownego marszu), doby na dlugu 3 i nowe obowiazkowe odpoczynki.</summary>
        private static bool SettleAi(AiSleep e, float baza, double nowH, int maxF)
        {
            bool full = nowH - e.SinceH >= 18.0;
            bool sleptBase = e.Acc >= 0f || e.Rest >= baza;
            bool ch = false;
            bool wasMust = Must(e, maxF);
            if (e.NightFlags != 0) { _day.Marched++; if (sleptBase) _day.MarchedNoDebt++; }
            if (!full) _day.NoFullDay++;
            else if (!sleptBase)
            {
                int before = e.Debt;
                e.Debt = Math.Min(3, e.Debt + 1);
                // poprawka recenzji (P5): nowy dlug i zapasc tylko przy faktycznym wzroscie - dlug 3 bez snu kolejna doba to zapasc trwajaca
                if (e.Debt > before)
                {
                    _day.NewDebt[e.Debt]++;
                    if (e.Debt == 3) _day.Collapses++;   // zapasc: sen ciagly od reki (AiDebtCamp w tym samym ticku)
                    ch = true;
                }
                else _day.StillCollapsed++;
            }
            // losy dlugu 1 z poprzedniego switu (P5: ile splaconych do nastepnego switu)
            if (e.DawnDebt == 1)
            {
                if (e.PaidSinceDawn) _day.C1Paid++;
                else if (e.Debt >= 2) _day.C1Up++;
                else _day.C1Stay++;
            }
            // T10-R: seria rosnie o kazda pelna dobe bez bazy, przespana baza ja zeruje; doby na dlugu 3 z rzedu (cel testu: nikt > 2 poza ucieczka)
            if (full) e.Streak = sleptBase ? 0 : e.Streak + 1;
            if (e.Debt == 0) e.Streak = 0;
            e.D3 = e.Debt >= 3 ? e.D3 + 1 : 0;
            if (!wasMust && Must(e, maxF)) _day.MustNew++;
            e.DawnDebt = e.Debt; e.PaidSinceDawn = false;
            e.Rest = 0f; e.Credited = false; e.NightFlags = 0;
            return ch;
        }

        /// <summary>Kary AI: nowy slownik (tylko dlug > 0) podmieniany w calosci; pamiec predkosci uniewazniona tylko zmienionym.</summary>
        private static void RebuildPenalties(bool on)
        {
            var old = _aiPenalty;
            var neu = new Dictionary<MobileParty, int>();
            if (on)
                foreach (var kv in _ai)   // takze partia chwilowo nieaktywna (rejs BK) - kara czeka na nia po powrocie
                    if (kv.Value.Debt > 0 && kv.Key != null) neu[kv.Key] = kv.Value.Debt;
            _aiPenalty = neu;
            foreach (var kv in neu)
            {
                int o;
                if (!old.TryGetValue(kv.Key, out o) || o != kv.Value) Touch(kv.Key);
            }
            foreach (var kv in old)
                if (!neu.ContainsKey(kv.Key)) Touch(kv.Key);
        }

        private static void Touch(MobileParty mp) { try { if (mp != null && mp.IsActive) mp.UpdateVersionNo(); } catch { } }

        private static void ResetDebts(string why)
        {
            ReleaseAllDebt();
            foreach (var e in _ai.Values) { e.Debt = 0; e.Acc = -1f; e.DawnDebt = 0; e.PaidSinceDawn = false; }
            RebuildPenalties(false);
            Log.Info("NocnyMarsz: " + why + ".");
        }

        private static void ClearAiLedger(string why)
        {
            ReleaseAllDebt();
            _ai.Clear();
            RebuildPenalties(false);
            _debtWasOn = null;
            Log.Info("NocnyMarsz: " + why + ".");
        }

        private static void LogMove(int h, MoveTally m)
        {
            int ph = Mod24(h - 1);
            Log.Info("AiNightCamp: ruch " + ph + ":00-" + h + ":00 (pomiar z pozycji, > " + RestMoveLimit.ToString("0.00", CultureInfo.InvariantCulture)
                     + " jedn. w godzinie obozu, poza osada i bitwa) - samotni lordowie w ruchu " + m.Lone + ": z powodem " + m.Reason
                     + ", alarm -> ucieczka " + m.AlarmFled + " | BEZ WPISU POWODU " + m.NoReason + ": alarm bez ucieczki " + m.AlarmStay
                     + ", obudzeni cudza reka " + m.Woken + ", dluznicy obudzeni " + m.DebtWoken + ", wyjazd z osady / po bitwie " + m.Exit
                     + ", los (stare reguly) " + m.OldLot + ", stare wyjatki T1 " + m.OldOther + ", Inni/doczepieni " + m.Other
                     + ", bez wpisu " + m.NoEntry + "; wodzowie armii w ruchu " + m.Lead + " (z powodem " + m.LeadReason + ")"
                     + "; na morzu (W2, poza progiem P2) " + m.Sea + ".");
            _day.SeaMove += m.Sea;
            _day.MoveHours++; _day.MoveLone += m.Lone; _day.MoveNoReason += m.NoReason; _day.MoveExit += m.Exit;
            _day.MoveWoken += m.Woken; _day.MoveAlarmStay += m.AlarmStay; _day.MoveDebtWoken += m.DebtWoken;
            _day.MoveOther += m.OldLot + m.OldOther + m.Other + m.NoEntry; _day.MoveLead += m.Lead; _day.MoveLeadNoReason += m.Lead - m.LeadReason;
        }

        private static void LogDawn(bool on)
        {
            int total = 0; int[] now = new int[4];
            foreach (var e in _ai.Values) { total++; now[Math.Max(0, Math.Min(3, e.Debt))]++; }
            var d = _day;
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("NocnyMarsz: swit dnia ").Append((int)CampaignTime.Now.ToDays).Append(on ? "" : " [NA SUCHO - bez kar i bez obozu splaty]")
              .Append(" - partii lordow AI w ksiedze ").Append(total).Append(" (bez pelnej doby ").Append(d.NoFullDay).Append(")")
              .Append("; nowy dlug 1/2/3: ").Append(d.NewDebt[1]).Append('/').Append(d.NewDebt[2]).Append('/').Append(d.NewDebt[3])
              .Append(" (zapasc trwa - dlug 3 bez snu kolejna doba ").Append(d.StillCollapsed).Append(")")
              .Append("; splacone od reki ").Append(d.Paid).Append(" (wedlug poziomu 1/2/3: ").Append(d.PaidBy[1]).Append('/').Append(d.PaidBy[2])
              .Append('/').Append(d.PaidBy[3]).Append(")")
              .Append("; dlug 1 z poprzedniego switu: splacony ").Append(d.C1Paid).Append(", wzrosl do 2 ").Append(d.C1Up).Append(", dalej 1 ").Append(d.C1Stay)
              .Append(" (splaconych ").Append(d.C1Paid + d.C1Up + d.C1Stay > 0 ? (100f * d.C1Paid / (d.C1Paid + d.C1Up + d.C1Stay)).ToString("0", ci) : "-").Append("%)")
              .Append("; z dlugiem teraz 1/2/3: ").Append(now[1]).Append('/').Append(now[2]).Append('/').Append(now[3])
              .Append("; wypadly z ksiegi (partia zniszczona) ").Append(d.Gone).Append(" (z dlugiem ").Append(d.GoneDebt).Append(")")
              .Append("; sen dlugu pominiety / przerwany, bo AI trzyma inny mod (partio-godziny) ").Append(d.DebtForeign)
              .Append("; alarm we wsi - AI wolne ").Append(d.VillageAlarm)
              .Append("; szly noca z powodem partii ").Append(d.Marched).Append(" (z nich odpoczely mimo to >= baza ").Append(d.MarchedNoDebt).Append(")")
              .Append("; partio-godziny nocnego marszu: ucieczka ").Append(d.HFlee).Append(", poscig ").Append(d.HChase).Append(", odsiecz ").Append(d.HRelief)
              .Append("; alarmy ").Append(d.HAlarm).Append(" (ucieczka ").Append(d.HAlarmFled).Append(", spi dalej ").Append(d.HAlarmSlept)
              .Append(", inny marsz ").Append(d.HAlarmOther).Append(")")
              .Append("; w osadach zostalo na noc (partio-godziny) ").Append(d.HTownHeld).Append(", wyjechalo z powodem ").Append(d.HTownOut)
              .Append("; oboz splaty od ").Append(DebtCampHour).Append(":00 partii ").Append(d.EveningCamps).Append(", sen ciagly ").Append(d.ContSleeps)
              .Append(", zwolnione ze snu dlugu ").Append(d.Released).Append(", dluznik szedl z powodem ").Append(d.DebtReason)
              .Append(", alarm dluznika ").Append(d.DebtAlarm).Append(", dluznicy obudzeni cudza reka ").Append(d.DebtWoken)
              .Append("; zapasci (nowy dlug 3) ").Append(d.Collapses);
            AppendRestRule(sb, now, d);
            sb.Append(" | ruch w oknie obozu (pomiar z pozycji, ").Append(d.MoveHours).Append(" h): samotni lordowie ").Append(d.MoveLone)
              .Append(" partio-godzin, BEZ WPISU POWODU ").Append(d.MoveNoReason)
              .Append(" (sr. ").Append((d.MoveHours > 0 ? d.MoveNoReason / (float)d.MoveHours : 0f).ToString("0.0", ci)).Append(" na godzine; wyjazd z osady / po bitwie ")
              .Append(d.MoveExit).Append(", obudzeni cudza reka ").Append(d.MoveWoken).Append(", alarm bez ucieczki ").Append(d.MoveAlarmStay)
              .Append(", dluznicy obudzeni ").Append(d.MoveDebtWoken).Append(", inne ").Append(d.MoveOther).Append("); wodzowie armii ")
              .Append(d.MoveLead).Append(" (bez powodu ").Append(d.MoveLeadNoReason).Append("); na morzu (W2, poza P2) ").Append(d.SeaMove)
              .Append(" | stoper ksiegi: sr. ").Append((_ledgerCalls > 0 ? _ledgerMsSum / _ledgerCalls : 0).ToString("0.000", ci))
              .Append(" ms, maks. ").Append(_ledgerMsMax.ToString("0.000", ci)).Append(" ms (").Append(_ledgerCalls).Append(" tikow)")
              .Append("; oboz swiata (petla AiNightCamp): sr. ").Append((d.CampCalls > 0 ? d.CampMsSum / d.CampCalls : 0).ToString("0.000", ci))
              .Append(" ms, maks. ").Append(d.CampMsMax.ToString("0.000", ci)).Append(" ms; oboz splaty (AiDebtCamp): sr. ")
              .Append((d.DebtCalls > 0 ? d.DebtMsSum / d.DebtCalls : 0).ToString("0.000", ci)).Append(" ms, maks. ").Append(d.DebtMsMax.ToString("0.000", ci))
              .Append(" ms; straznicy T10: ").Append(d.GuardCalls).Append(" x, sr. ").Append((d.GuardCalls > 0 ? d.GuardMsSum / d.GuardCalls : 0).ToString("0.000", ci))
              .Append(" ms, maks. ").Append(d.GuardMsMax.ToString("0.000", ci)).Append(" ms")
              .Append("; straznik snu dlugu - maks. odstep ").Append(_debtDhMax.ToString("0.000", ci)).Append(" h, alarmow ")
              .Append(_alarmDhMax.ToString("0.000", ci)).Append(" h gry; potkniecia T10 w sesji ").Append(_aiStumbles).Append('.');
            Log.Info(sb.ToString());
            _debtDhMax = 0; _alarmDhMax = 0;
        }

        /// <summary>
        /// T10-R: czesc linii switu o regule odpoczynku - partie na dlugu 1/2/3, obowiazkowy odpoczynek (teraz i nowe), najdluzsza seria nocy bez snu,
        /// dlug 3 dluzej niz 2 doby z rzedu (cel testu: 0 poza ucieczka; do 3 nazw z miejscem), wymuszone odpoczynki i wyjatki (ucieczka) dzis,
        /// wodzowie trzymani dlugiem czlonkow, sen ciagly na miejscu (R6) i ticki nadrabiane (R7).
        /// </summary>
        private static void AppendRestRule(StringBuilder sb, int[] now, DayCounters d)
        {
            int maxF = MaxForced(Settings.Current);
            int today = (int)CampaignTime.Now.ToDays;
            int mustNow = 0, bestStreak = 0, overStreak = 0, d3Long = 0, d3LongFlee = 0;
            string bestName = "-";
            var names = new List<string>();
            foreach (var kv in _ai)
            {
                var e = kv.Value; var mp = kv.Key;
                if (e == null || mp == null) continue;
                if (Must(e, maxF)) mustNow++;
                if (e.Streak > bestStreak) { bestStreak = e.Streak; try { bestName = mp.Name.ToString(); } catch { bestName = "?"; } }
                if (maxF > 0 && e.Streak > maxF) overStreak++;
                if (e.D3 > 2)
                {
                    d3Long++;
                    bool fled = e.CrushDay >= 0 && e.CrushDay >= today - e.D3;
                    if (fled) d3LongFlee++;
                    if (names.Count < 3)
                    {
                        string nm; try { nm = mp.Name.ToString(); } catch { nm = "?"; }
                        names.Add(nm + " " + e.D3 + " dob, " + Where(mp) + (fled ? ", ucieczka-wyjatek" : ""));
                    }
                }
            }
            sb.Append(" | ODPOCZYNEK T10-R (najwyzej ").Append(maxF).Append(" noce marszu z rzedu, potem oboz do dlugu 0): na dlugu 1/2/3: ")
              .Append(now[1]).Append('/').Append(now[2]).Append('/').Append(now[3])
              .Append("; obowiazkowy odpoczynek teraz ").Append(mustNow).Append(" (nowe dzis ").Append(d.MustNew).Append(")")
              .Append("; najdluzsza seria nocy bez snu ").Append(bestStreak).Append(bestStreak > 0 ? " (" + bestName + ")" : "")
              .Append(", serii dluzszych niz ").Append(maxF).Append(": ").Append(overStreak)
              .Append("; dlug 3 dluzej niz 2 doby z rzedu ").Append(d3Long).Append(" (w tym z ucieczka-wyjatkiem ").Append(d3LongFlee).Append(")");
            if (names.Count > 0) sb.Append(" [").Append(string.Join("; ", names)).Append("]");
            sb.Append("; wymuszone odpoczynki dzis: partii ").Append(d.BlockedP.Count).Append(" (zablokowany marsz, partio-godziny: ucieczka ").Append(d.BlockFlee)
              .Append(", poscig ").Append(d.BlockChase).Append(", odsiecz ").Append(d.BlockRelief).Append(", alarm ").Append(d.BlockAlarm).Append(")")
              .Append("; wyjatki dzis - ucieczka przed wrogiem >= ").Append(F1(CrushRatio)).Append(" x silniejszym: partii ").Append(d.CrushP.Count)
              .Append(" (partio-godzin ").Append(d.CrushHours).Append(")")
              .Append("; wodzowie armii spia za zmeczonych czlonkow (partio-godziny) ").Append(d.ArmyHeld)
              .Append("; sen ciagly na miejscu bez snu dlugu (oblezenie, oblezona osada, morze, armia, inny mod - partio-godziny) ").Append(d.PassiveHours)
              .Append(", splacone tak ").Append(d.PassivePaid)
              .Append("; ticki nadrabiane w jednej klatce ").Append(d.CatchUps);
        }

        /// <summary>
        /// Dowod kary AI (uwaga krytyki 5): do 3 dluznikow (w tym wodz armii, jesli jest) - predkosc i morale z rozpiska gry
        /// (SpeedExplained, GetEffectivePartyMorale z opisami): bez kary, z kara, procent i tablica; zgodnosc +-1 pp.
        /// </summary>
        private static void LogPenaltySample()
        {
            var pen = _aiPenalty;
            if (pen.Count == 0) { Log.Info("NocnyMarsz: kara - brak partii AI z dlugiem snu (nic do sprawdzenia)."); return; }
            var pick = new List<MobileParty>();
            foreach (var kv in pen) if (kv.Key.IsActive && IsLeader(kv.Key)) { pick.Add(kv.Key); break; }
            foreach (var kv in pen)
            {
                if (pick.Count >= 3) break;
                if (kv.Key.IsActive && !pick.Contains(kv.Key)) pick.Add(kv.Key);
            }
            string name = _txtSleepless.ToString();
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder("NocnyMarsz: kara - probka ").Append(pick.Count).Append(" z ").Append(pen.Count).Append(" dluznikow AI: ");
            int ok = 0, leaders = 0;
            for (int i = 0; i < pick.Count; i++)
            {
                var mp = pick[i];
                int d; pen.TryGetValue(mp, out d);
                bool leader = IsLeader(mp); if (leader) leaders++;
                var en = mp.SpeedExplained;
                float B = en.ResultNumber, F = en.SumOfFactors, line = 0f; bool hasS = false;
                foreach (var l in en.GetLines()) if (l.Item1 == name) { line += l.Item2; hasS = true; }
                float A = B - line * (1f + F);
                bool floor = en.LimitMinValue > -1e30f && B <= en.LimitMinValue + 0.001f;
                float sp = A > 0.01f ? (A - B) / A * 100f : 0f;
                var me = Campaign.Current.Models.PartyMoraleModel.GetEffectivePartyMorale(mp, true);
                float D = me.ResultNumber, Fm = me.SumOfFactors, fl = 0f, mb = me.BaseNumber; bool hasM = false;
                // poprawka recenzji: linia typu Multiply z GetLines() to PUNKTY (BaseNumber x wspolczynnik - ExplainedNumber.GetLines),
                // nie procent - wspolczynnik = punkty / BaseNumber (dawne "/ 100" bylo dobre tylko przy bazie 100)
                foreach (var l in me.GetLines()) if (l.Item1 == name) { fl += Math.Abs(mb) > 0.001f ? l.Item2 / mb : 0f; hasM = true; }
                float C = (1f + Fm) > 0.001f ? D / (1f + Fm) * (1f + Fm - fl) : D;
                bool mfloor = me.LimitMinValue > -1e30f && D <= me.LimitMinValue + 0.001f;
                float mp2 = C > 0.01f ? (C - D) / C * 100f : 0f;
                int dd = Math.Min(3, Math.Max(1, d));
                bool okS = hasS && (floor || Math.Abs(sp - SpdPenalty[dd]) <= 1f);
                bool okM = hasM && (mfloor || Math.Abs(mp2 - MorPenalty[dd]) <= 1f);
                if (okS && okM) ok++;
                if (i > 0) sb.Append("; ");
                sb.Append(mp.Name).Append(leader ? " (wodz armii)" : "").Append(" dlug ").Append(d)
                  .Append(": predkosc ").Append(A.ToString("0.00", ci)).Append(" -> ").Append(B.ToString("0.00", ci))
                  .Append(" (-").Append(sp.ToString("0.0", ci)).Append("%, tablica -").Append(SpdPenalty[dd]).Append('%').Append(floor ? ", na podlodze" : "").Append(')')
                  .Append(", morale ").Append(C.ToString("0.0", ci)).Append(" -> ").Append(D.ToString("0.0", ci))
                  .Append(" (-").Append(mp2.ToString("0.0", ci)).Append("%, tablica -").Append(MorPenalty[dd]).Append('%').Append(mfloor ? ", na podlodze" : "").Append(')')
                  .Append(", wpis '").Append(name).Append("' ").Append(hasS ? "tak" : "NIE").Append('/').Append(hasM ? "tak" : "NIE");
            }
            sb.Append(" | wodzow armii w probce ").Append(leaders).Append("; zgodnosc z tablica +-1 pp: ").Append(ok).Append('/').Append(pick.Count).Append('.');
            Log.Info(sb.ToString());
        }

        /// <summary>Linia ksiegi gracza o swicie (P10): sam log, SettleNight bez zmian; sprawdza wzor dlugu.</summary>
        private static void LogPlayerDawn(Settings s, float rest, int before, bool sleeping, bool credited)
        {
            try
            {
                float baza = Math.Max(1f, s.SleepHoursNeeded);
                bool enlisted = RotEnlisted();
                int expect = enlisted ? 0 : ((sleeping || rest >= baza) ? before : Math.Min(3, before + 1));
                Log.Info("NocnyMarsz: gracz o swicie - odpoczynek doby " + F1(rest) + " h (baza " + F1(baza) + ", sen w menu "
                         + (sleeping ? "tak" : "nie") + ", splata od reki w tej dobie " + (credited ? "tak" : "nie")
                         + (enlisted ? ", w sluzbie ROT" : "") + "), dlug przed " + before + ", po " + Debt
                         + " - wzor (dlug +1, gdy < baza i nie spi; maks. 3): " + (expect == Debt ? "zgodny" : "NIEZGODNY, oczekiwany " + expect)
                         + "; dlug o swicie (musztra) " + DawnDebt + (DawnDebt == Debt ? "" : " - NIEZGODNY z dlugiem po") + ".");
            }
            catch { }
        }

        // ------------------------------------------------------------ R3: splata (oboz od 20:00, sen ciagly)
        private static void AiDebtCamp(Settings s, int h)
        {
            if (!DebtOn(s))
            {
                if (_debtSleep.Count > 0) { ReleaseAllDebt(); Log.Info("NocnyMarsz: ksiega dlugu AI nieczynna - spiacy dluznicy obudzeni."); }
                return;
            }
            if (_ai.Count == 0) return;
            List<MobileParty> gone = null;
            foreach (var kv in _debtSleep)
                if (kv.Key == null || !kv.Key.IsActive || !_ai.ContainsKey(kv.Key)) (gone ?? (gone = new List<MobileParty>())).Add(kv.Key);
            if (gone != null)
                foreach (var m in gone)
                {
                    if (m == null) continue;
                    // nieaktywna (rejs BK - AI trzyma BK) albo poza ksiega: bez EnableAi; koniec licznika snu jak LeaveSleep gracza
                    _debtSleep.Remove(m);
                    AiSleep ge;
                    if (_ai.TryGetValue(m, out ge)) LeaveAcc(ge);
                }
            bool evening = InDebtEvening(h);
            HashSet<MobileParty> campSet = null;
            int maxF = MaxForced(s);
            foreach (var kv in _ai)
            {
                var mp = kv.Key; var e = kv.Value;
                try
                {
                    if (mp == null || !mp.IsActive) continue;
                    DebtSleeper ds;
                    bool inDebt = _debtSleep.TryGetValue(mp, out ds);
                    // sen ciagly bez snu dlugu (wczytany zapis, splata w trakcie, doczepiony po zwolnieniu wodza) - koniec licznika
                    // jak LeaveSleep gracza; inaczej Acc >= 0 zwalnialby partie z dlugu o kazdym swicie (SettleAi)
                    // T10-R (R6): sen ciagly na miejscu (e.Passive) trwa - konczy go ruch (AiSleepLedger)
                    if (!inDebt && e.Acc >= 0f && !e.Passive && !SleepsWithLeader(mp)) LeaveAcc(e);
                    // T10-R (R5): wodz armii decyduje wedlug najgorszego dlugu armii (czlonek nie moze sam sie polozyc - spi z wodzem)
                    bool must;
                    int ed = EffDebt(mp, e, maxF, out must);
                    if (ed < 1 && !inDebt) continue;
                    // poprawka recenzji (R1b): AI trzyma INNY mod (uczta / gentry / statek BK) - nie kladziemy spac i nie skracamy cudzej
                    // blokady (DisableForHours(1) nadpisalby DisableAi, a ReleaseDebt -> EnableAi wypuscilby goscia z uczty)
                    if (inDebt && ForeignHold(mp))
                    {
                        _debtSleep.Remove(mp); LeaveAcc(e);
                        _day.DebtForeign++;
                        Mark(e, StSkip);
                        continue;
                    }
                    if (!inDebt && mp.Ai != null && mp.Ai.IsDisabled && !_townHold.Contains(mp) && !_camping.Contains(mp))
                    {
                        _day.DebtForeign++;
                        continue;
                    }
                    bool want = ed >= 2 || (ed == 1 && evening);
                    var st = mp.CurrentSettlement;
                    bool can = mp.AttachedTo == null && mp.MapEvent == null && mp.BesiegerCamp == null && !mp.IsCurrentlyAtSea
                               && (st == null || st.SiegeEvent == null);
                    if (!want || !can) { if (inDebt) ReleaseDebt(mp, e, ds); continue; }
                    bool leader = IsLeader(mp);
                    string det;
                    // spiacy w polu: rozkaz sprzed snu; spiacy w osadzie: rozkaz nietkniety (tylko AI wstrzymane) - liczy sie biezacy
                    NightOrder slept = !inDebt ? null : (ds.Order != null ? ds.Order : (st != null ? null : EmptyOrder));
                    NReason blk; bool crush;
                    var r = Classify(mp, s, ed, must, leader, slept, out det, out blk, out crush);
                    CountRule(mp, e, blk, crush, det);
                    if (r != NReason.None)
                    {
                        if (inDebt) ReleaseDebt(mp, e, ds);
                        _day.DebtReason++;
                        Mark(e, StReason, r);
                        Example(mp, "dluznik idzie: " + det, ed, leader);
                        continue;
                    }
                    if (st == null)
                    {
                        if (campSet == null) campSet = new HashSet<MobileParty>(_camping);
                        string how;
                        var th = AlarmFor(mp, s, campSet, must, blk, out how);
                        if (th != null)
                        {
                            NightOrder keep = inDebt ? ds.Order : TakeOrder(mp);
                            if (inDebt) { _debtSleep.Remove(mp); LeaveAcc(e); }
                            DropFromCamp(mp);
                            mp.SetMoveModeHold();
                            EnableAi(mp);
                            _alarmed[mp] = new AlarmInfo { AtH = NowH(), Pos = mp.GetPosition2D, DebtPath = true, Order = keep };
                            _day.DebtAlarm++;
                            Mark(e, StAlarm);
                            Example(mp, "ALARM dluznika - " + how + " - AI ocenia ucieczke", ed, leader);
                            continue;
                        }
                    }
                    if (leader && ed > e.Debt) _day.ArmyHeld++;   // T10-R (R5): wodz spi za zmeczonych czlonkow armii
                    if (!inDebt) EnterDebt(mp, e, null, ed);
                    else
                    {
                        if (ed >= 2 && ds.Kind != 2) { ds.Kind = 2; if (e.Acc < 0f) e.Acc = e.Rest; _day.ContSleeps++; }
                        mp.Ai.DisableForHours(1);
                        if (st == null && mp.DefaultBehavior != AiBehavior.Hold)
                        {
                            if (ds.Order == null) ds.Order = Capture(mp);   // zasnal w osadzie, a wyszedl cudzym rozkazem - zapamietac go
                            mp.SetMoveModeHold();
                        }
                    }
                    Mark(e, StDebt);
                }
                catch (Exception ex) { AiStumble("AiDebtCamp", mp, ex); }
            }
        }

        /// <summary>Rozkaz partii zapamietany przez oboz swiata (przejety - swit go nie odda) albo biezacy.</summary>
        private static NightOrder TakeOrder(MobileParty mp)
        {
            NightOrder o;
            if (_orders.TryGetValue(mp, out o)) { _orders.Remove(mp); return o; }
            return Capture(mp);
        }

        /// <summary>Partia wychodzi z obozu swiata (namiot, legowisko) - przechodzi do snu dlugu albo do alarmu.</summary>
        private static void DropFromCamp(MobileParty mp)
        {
            if (_camping.Remove(mp))
            {
                _bedPos.Remove(mp);
                if (_tented.Contains(mp)) { Tent(mp, false); _tented.Remove(mp); }
            }
            _townHold.Remove(mp);
        }

        /// <summary>ed - dlug, wedlug ktorego partia spi (T10-R R5: u wodza armii najgorszy z armii): 2-3 sen ciagly, 1 oboz splaty.</summary>
        private static void EnterDebt(MobileParty mp, AiSleep e, NightOrder given, int ed)
        {
            // w osadzie wystarczy wstrzymac AI (gra nie wypusci partii z wylaczonym AI) - rozkaz zostaje nietkniety
            bool inTown = mp.CurrentSettlement != null;
            var ds = new DebtSleeper { Bed = mp.GetPosition2D, Kind = ed >= 2 ? 2 : 1, Order = given ?? (inTown ? null : TakeOrder(mp)) };
            DropFromCamp(mp);
            _debtSleep[mp] = ds;
            if (ds.Kind == 2) { if (e.Acc < 0f) e.Acc = e.Rest; _day.ContSleeps++; } else _day.EveningCamps++;
            mp.Ai.DisableForHours(1);
            if (!inTown) mp.SetMoveModeHold();
        }

        /// <summary>Koniec snu ciaglego - jak LeaveSleep gracza: odpoczynek doby = max(doba, licznik snu).</summary>
        private static void LeaveAcc(AiSleep e)
        {
            if (e == null || e.Acc < 0f) return;
            e.Rest = Math.Max(e.Rest, e.Acc);
            e.Acc = -1f;
        }

        private static void ReleaseDebt(MobileParty mp, AiSleep e, DebtSleeper ds)
        {
            _debtSleep.Remove(mp);
            LeaveAcc(e);
            EnableAi(mp);
            if (ds != null) ApplyOrder(mp, ds.Order, true);
            _day.Released++;
        }

        private static void ReleaseAllDebt()
        {
            if (_debtSleep.Count == 0) return;
            var list = new List<KeyValuePair<MobileParty, DebtSleeper>>(_debtSleep);
            foreach (var kv in list)
            {
                try
                {
                    AiSleep e = null;
                    if (kv.Key != null) _ai.TryGetValue(kv.Key, out e);
                    if (kv.Key != null && kv.Key.IsActive) ReleaseDebt(kv.Key, e, kv.Value);
                }
                catch { }
            }
            _debtSleep.Clear();
        }

        /// <summary>Straznik snu dluznikow (co 0.1 h gry, o kazdej godzinie): cudzy rozkaz albo zjazd z legowiska -> z powrotem Hold.</summary>
        private static void HoldDebtSleepers()
        {
            var list = new List<KeyValuePair<MobileParty, DebtSleeper>>(_debtSleep);
            foreach (var kv in list)
            {
                var mp = kv.Key; var ds = kv.Value;
                try
                {
                    if (mp == null || !mp.IsActive) { if (mp != null) _debtSleep.Remove(mp); continue; }
                    if (mp.MapEvent != null || mp.CurrentSettlement != null) continue;   // bitwa/osada - nie nasza sprawa
                    float drift = mp.GetPosition2D.Distance(ds.Bed);
                    bool ordered = mp.DefaultBehavior != AiBehavior.Hold || mp.TargetSettlement != null || mp.TargetParty != null;
                    if (!ordered && drift <= 0.3f) continue;
                    // poprawka recenzji: rozkaz z dluzsza cudza blokada AI (uczta / gentry BK) - nie nasza sprawa, sen dlugu konczy sie bez EnableAi
                    if (ForeignHold(mp))
                    {
                        _debtSleep.Remove(mp);
                        AiSleep fe;
                        if (_ai.TryGetValue(mp, out fe)) LeaveAcc(fe);
                        _day.DebtForeign++;
                        continue;
                    }
                    mp.Ai.DisableForHours(1);
                    mp.SetMoveModeHold();
                    ds.Bed = mp.GetPosition2D;
                    _debtWokenHour++;
                }
                catch (Exception ex)
                {
                    GuardStumble("HoldDebtSleepers", mp, ex);
                    try { if (mp != null) _debtSleep.Remove(mp); } catch { }
                }
            }
        }

        /// <summary>Alarm rozstrzygniety (co 0.1 h gry): ucieczka = powod; nowy cel AI bez powodu cofniety; po 0.5 h z powrotem spac.</summary>
        private static void GuardAlarmed()
        {
            var list = new List<KeyValuePair<MobileParty, AlarmInfo>>(_alarmed);
            double now = NowH();
            int hNow = CampaignTime.Now.GetHourOfDay;
            foreach (var kv in list)
            {
                var mp = kv.Key; var a = kv.Value;
                try
                {
                    if (mp == null) continue;
                    AiSleep e;
                    _ai.TryGetValue(mp, out e);
                    if (!mp.IsActive) { _alarmed.Remove(mp); continue; }
                    if (mp.MapEvent != null || mp.CurrentSettlement != null) { _alarmed.Remove(mp); _alarmBattle++; continue; }
                    if (mp.IsFleeing())
                    {
                        _alarmed.Remove(mp); _alarmFled++;
                        // poprawka recenzji: lord, ktory uciekal, jest na nogach i mysli sam - rozkaz sprzed polnocy nie wraca o swicie
                        if (!a.DebtPath) _orders.Remove(mp);
                        if (e != null) { e.AlarmEnd = 1; if (InCamp(hNow)) e.NightFlags |= 8; }
                        continue;
                    }
                    // poprawka recenzji: alarm obozu swiata, a oboz juz sie skonczyl (alarm 5:30-6:00) - galaz switu oddala rozkaz sprzed snu,
                    // nie cofamy go i rozstrzygamy od reki (inaczej lord zostawal w Hold bez rozkazu do ok. 6:30)
                    bool campOver = !a.DebtPath && !InCamp(hNow);
                    if (!campOver)
                    {
                        if (mp.DefaultBehavior != AiBehavior.Hold) { mp.SetMoveModeHold(); _alarmResets++; }   // nowy cel strategiczny - bez powodu nie idzie
                        if (now - a.AtH < AlarmGraceHours) continue;
                    }
                    _alarmed.Remove(mp);
                    float drift = mp.GetPosition2D.Distance(a.Pos);
                    // po switie ruch to juz rozkaz oddany przez galaz switu, nie "inny marsz" po alarmie
                    if (drift > RestMoveLimit && !campOver) { _alarmOther++; if (e != null) e.AlarmEnd = 3; }
                    else { _alarmSlept++; if (e != null) e.AlarmEnd = 2; }
                    SleepAgain(mp, a, e);
                }
                catch (Exception ex)
                {
                    GuardStumble("GuardAlarmed", mp, ex);
                    try { if (mp != null) _alarmed.Remove(mp); } catch { }
                }
            }
        }

        private static void SleepAgain(MobileParty mp, AlarmInfo a, AiSleep e)
        {
            var s = Settings.Current;
            int h = CampaignTime.Now.GetHourOfDay;
            if (a.DebtPath)
            {
                // T10-R (R5): wodz armii wedlug najgorszego dlugu armii
                bool must;
                int ed = e != null ? EffDebt(mp, e, MaxForced(s), out must) : 0;
                if (s != null && DebtOn(s) && e != null && (ed >= 2 || (ed == 1 && InDebtEvening(h)))) { EnterDebt(mp, e, a.Order, ed); return; }
                ApplyOrder(mp, a.Order, false);   // dlug splacony albo koniec okna - rozkaz sprzed snu
                return;
            }
            if (s != null && s.AiCampsAtNight && InCamp(h))
            {
                RememberOrder(mp);   // rozkaz sprzed snu zwykle juz jest (zostal przy alarmie)
                mp.Ai.DisableForHours(1);
                mp.SetMoveModeHold();
                if (!_camping.Contains(mp)) { _camping.Add(mp); _bedPos[mp] = mp.GetPosition2D; }
                return;
            }
            GiveOrderBack(mp);   // swit juz byl - rozkaz sprzed snu (jesli galaz switu go jeszcze nie oddala)
        }

        // ------------------------------------------------------------ zapis
        /// <summary>
        /// Ksiega AI do zapisu: kazdy wpis niezerowy (dlug, odpoczynek doby, sen ciagly, splata) - takze partia bez dlugu, ktora
        /// tej nocy szla (uwaga krytyki 10); partie spoza zapisu po wczytaniu = stan zerowy z pelna doba. Limit AiSaveCap:
        /// odpada najmniejszy dlug i odpoczynek (w logu ile).
        /// </summary>
        internal static string ExportAi(bool saving)
        {
            try
            {
                if (_aiPending != null) return _aiPending;   // wczytane, jeszcze nie rozwiazane - oddajemy bez zmian
                if (_ai.Count == 0) return "";
                var list = new List<KeyValuePair<MobileParty, AiSleep>>();
                foreach (var kv in _ai)
                {
                    var e = kv.Value;
                    if (kv.Key == null) continue;   // poprawka recenzji: partia chwilowo nieaktywna (rejs BK) tez idzie do zapisu
                    // MUSZTRA-j: takze wpis z samym dlugiem o swicie (dlug splacony w ciagu dnia - dzien cwiczen dalej stracony do nastepnego switu)
                    // T10-R: takze sama seria nocy bez snu albo doby na dlugu 3
                    if (e.Debt > 0 || e.DawnDebt > 0 || e.Rest > 0.05f || e.Acc >= 0f || e.Credited || e.Streak > 0 || e.D3 > 0) list.Add(kv);
                }
                list.Sort((a, b) => a.Value.Debt != b.Value.Debt ? b.Value.Debt.CompareTo(a.Value.Debt) : b.Value.Rest.CompareTo(a.Value.Rest));
                var ci = CultureInfo.InvariantCulture;
                var sb = new StringBuilder("v1|");
                int n = 0, debts = 0, sleeps = 0, skipped = 0, dawn = 0;
                foreach (var kv in list)
                {
                    if (n >= AiSaveCap) break;
                    string id = kv.Key.StringId;
                    if (string.IsNullOrEmpty(id) || id.IndexOf(':') >= 0 || id.IndexOf(';') >= 0 || id.IndexOf('|') >= 0) { skipped++; continue; }
                    var e = kv.Value;
                    // MUSZTRA-j: szoste pole - dlug o swicie; format "v1" zostaje (stary DLL czyta pola 0-4 i szoste pomija, nowy czyta je, jesli jest)
                    // T10-R: siodme pole - seria nocy bez snu, osme - doby na dlugu 3 (stary DLL je pomija; napis idzie przez SaveText.Sync - "arm_nightrest_ai")
                    sb.Append(id).Append(':').Append(e.Debt).Append(':').Append(e.Rest.ToString("0.##", ci)).Append(':')
                      .Append(e.Credited ? '1' : '0').Append(':').Append(e.Acc.ToString("0.##", ci)).Append(':').Append(e.DawnDebt)
                      .Append(':').Append(e.Streak).Append(':').Append(e.D3).Append(';');
                    n++;
                    if (e.Debt > 0) debts++;
                    if (e.Acc >= 0f) sleeps++;
                    if (e.DawnDebt > 0) dawn++;
                }
                string str = sb.ToString();
                if (saving)
                    Log.Info("NocnyMarsz: zapis ksiegi snu AI - wpisow " + n + " (z dlugiem " + debts + ", z dlugiem o swicie " + dawn + ", w snie ciaglym " + sleeps + "), napis "
                             + str.Length + " zn." + (list.Count - n - skipped > 0 ? ", POMINIETO " + (list.Count - n - skipped) + " (limit " + AiSaveCap
                             + ": najmniejszy dlug i odpoczynek)" : "") + (skipped > 0 ? ", zle id " + skipped : "") + ".");
                return str;
            }
            catch (Exception e) { Log.Error("NightRest.ExportAi", e); return ""; }
        }

        internal static void ImportAi(string data)
        {
            try
            {
                _aiPending = string.IsNullOrEmpty(data) ? null : data;
                if (_aiPending == null) Log.Info("NocnyMarsz: wczytano zapis bez ksiegi snu AI (stary zapis albo pusta ksiega) - partie AI zaczynaja bez dlugu.");
            }
            catch { }
        }

        /// <summary>
        /// grupa11-p: ksiega snu AI z zapisu od reki - z pierwszej klatki po wczytaniu (NightRest.OnTick, watek glowny, partie juz istnieja), nie dopiero
        /// w pierwszym ticku godzinowym: do tej chwili slownik kar byl pusty, wiec partie AI z dlugiem nie tracily predkosci, morale ani dnia musztry
        /// (tick treningu wypada o roznych godzinach), a dlug gracza dzialal od razu (Import). Tylko przy czynnej ksiedze (jak dotad: przelacznik glowny
        /// i oboz swiata) - inaczej napis czeka i ExportAi oddaje go bez zmian. _aiImportFresh zostaje do pierwszego AiSleepLedger, a jego ResolveImport
        /// jest wtedy pusty (_aiPending == null).
        /// </summary>
        private static void AiImportNow(Settings s)
        {
            if (_aiPending == null || s == null || !s.NightRestEnabled || !LedgerOn(s)) return;
            try { if (ResolveImport()) RebuildPenalties(DebtOn(s)); }
            catch (Exception e) { Log.Error("NightRest.AiImportNow", e); }
        }

        /// <summary>Napis z zapisu -> wpisy ksiegi (przy pierwszej klatce po wczytaniu - AiImportNow - albo w pierwszym ticku, gdy partie juz istnieja).</summary>
        private static bool ResolveImport()
        {
            if (_aiPending == null) return false;
            string data = _aiPending;
            _aiPending = null;
            try
            {
                var byId = new Dictionary<string, MobileParty>();
                var lords = MobileParty.AllLordParties;
                if (lords != null)
                    for (int i = 0; i < lords.Count; i++)
                    {
                        var mp = lords[i];
                        if (mp != null && mp.StringId != null) byId[mp.StringId] = mp;
                    }
                int bar = data.IndexOf('|');
                if (bar < 0 || data.Substring(0, bar) != "v1") { Log.Info("NocnyMarsz: ksiega snu AI w zapisie w nieznanej wersji - pominieta."); return false; }
                var ci = CultureInfo.InvariantCulture;
                double nowH = NowH();
                int n = 0, missing = 0, bad = 0, sleeps = 0, dawn = 0, noDawnField = 0; int[] debts = new int[4];
                foreach (var part in data.Substring(bar + 1).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var f = part.Split(':');
                    if (f.Length < 5) { bad++; continue; }
                    MobileParty mp;
                    if (!byId.TryGetValue(f[0], out mp)) { missing++; continue; }   // nieaktywna (rejs BK) zostaje - jest na liscie lordow
                    int d; float r, a;
                    if (!int.TryParse(f[1], NumberStyles.Integer, ci, out d) || !float.TryParse(f[2], NumberStyles.Float, ci, out r)
                        || !float.TryParse(f[4], NumberStyles.Float, ci, out a)) { bad++; continue; }
                    // Stamp = -1: pierwsza godzina po wczytaniu liczy sie jako postoj (jak gracz po wczytaniu - _hadPos zerowane w ResetWorld)
                    var e = new AiSleep { Debt = Math.Max(0, Math.Min(3, d)), Rest = Math.Max(0f, r), Credited = f[3] == "1", Acc = a >= 0f ? a : -1f, SinceH = nowH - 48.0 };
                    // MUSZTRA-j: dlug o swicie z szostego pola; stary zapis (bez pola) - dlug o swicie = dlug (jak dotad)
                    int dd;
                    if (f.Length > 5 && int.TryParse(f[5], NumberStyles.Integer, ci, out dd)) e.DawnDebt = Math.Max(0, Math.Min(3, dd));
                    else { e.DawnDebt = e.Debt; noDawnField++; }
                    // T10-R: seria nocy bez snu i doby na dlugu 3 (zapis sprzed T10-R - od zera)
                    int sk, d3;
                    if (f.Length > 6 && int.TryParse(f[6], NumberStyles.Integer, ci, out sk)) e.Streak = Math.Max(0, Math.Min(999, sk));
                    if (f.Length > 7 && int.TryParse(f[7], NumberStyles.Integer, ci, out d3)) e.D3 = Math.Max(0, Math.Min(999, d3));
                    if (e.Debt == 0) e.Streak = 0;
                    _ai[mp] = e;
                    n++; debts[e.Debt]++; if (e.Acc >= 0f) sleeps++; if (e.DawnDebt > 0) dawn++;
                }
                _aiImportFresh = true;
                Log.Info("NocnyMarsz: wczytano ksiege snu AI - wpisow " + n + " (z dlugiem 1/2/3: " + debts[1] + "/" + debts[2] + "/" + debts[3]
                         + ", z dlugiem o swicie " + dawn + (noDawnField > 0 ? " - stary zapis bez pola u " + noDawnField + ": dlug o swicie = dlug" : "")
                         + ", w snie ciaglym " + sleeps + "; partii juz nie ma " + missing + ", zlych wpisow " + bad
                         + "); partie spoza zapisu bez dlugu, z pelna doba.");
                return n > 0;
            }
            catch (Exception e) { Log.Error("NightRest.ResolveImport", e); return false; }
        }
    }
}
