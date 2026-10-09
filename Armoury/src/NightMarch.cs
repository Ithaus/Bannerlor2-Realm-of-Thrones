using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
    /// </summary>
    internal static partial class NightRest
    {
#if T10_DRY
        private const bool DryBuild = true;
#else
        private const bool DryBuild = false;
#endif
        private const int DebtCampHour = 20;          // oboz splaty dlugu 1: ticki 21..6 = 10 h pelnej stawki (1 h zapasu nad 9 h)
        private const float RestMoveLimit = 0.35f;    // jak gracz (OnHourly): ponizej - partia stoi
        private const double AlarmGraceHours = 0.5;   // AI sprawdza inicjatywe co ok. 0.15-0.18 h (AiCheckInterval 0.25 x 0.6-0.7)
        private const int AiSaveCap = 1500;           // wpisow ksiegi w zapisie (lordow ROT ok. 500-700)

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
        private static double _ledgerMsSum, _ledgerMsMax; private static int _ledgerCalls;

        private sealed class DayCounters
        {
            public int Paid, NoFullDay, Marched, MarchedNoDebt, Collapses, EveningCamps, ContSleeps, Released, DebtReason, DebtAlarm;
            public readonly int[] NewDebt = new int[4];
            public int HFlee, HChase, HRelief, HAlarm, HAlarmFled, HAlarmSlept, HAlarmOther, HTownHeld, HTownOut;
            public int MoveHours, MoveLone, MoveNoReason, MoveExit, MoveWoken, MoveAlarmStay, MoveDebtWoken, MoveOther, MoveLead, MoveLeadNoReason;
            public int DebtWoken;
        }
        private static DayCounters _day = new DayCounters();

        private sealed class NightTally
        {
            public int LoneField, LoneSleep, LoneFlee, LoneChase, LoneRelief, LoneAlarm;
            public int LeadField, LeadSleep, LeadFlee, LeadChase, LeadRelief, LeadAlarm;
            public int TownHeld, TownOut, TownForeign, Sea, DebtSleepers, AlarmPending, CaravansLot;
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
        private static long HourStamp() { return (long)Math.Round(CampaignTime.Now.ToHours); }
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

        /// <summary>Dlug snu partii AI z ksiegi R2, ktory naprawde dziala (0 przy wylaczonym AiSleepDebt i na sucho) - hak dla musztry
        /// (paczka MUSZTRA: Drill.SleepDebtOf = NightRest.AiDebtOf przy scaleniu). Czyta podmieniany w calosci slownik kar - bezpieczne z kazdego watku.</summary>
        internal static int AiDebtOf(MobileParty mp)
        {
            if (mp == null) return 0;
            var pen = _aiPenalty;
            int d;
            return pen != null && pen.TryGetValue(mp, out d) ? d : 0;
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

        // ------------------------------------------------------------ R1: powod
        /// <summary>
        /// Powod nocnego marszu. slept != null = partia spi (AI wylaczone, rozkaz Hold) - liczy sie rozkaz zapamietany przed snem.
        /// Ucieczka zawsze; poscig i odsiecz tylko przy dlugu < AiNightsAwakeInChase (lord z dlugiem traci wiecej, niz zyska).
        /// </summary>
        private static NReason Classify(MobileParty mp, Settings s, int debt, bool leader, NightOrder slept, out string detail)
        {
            detail = null;
            if (slept == null && mp.IsFleeing())
            {
                var from = mp.ShortTermTargetParty;
                detail = "ucieczka" + (from != null ? " przed " + from.Name + " (sila " + F0(Strength(from)) + " vs " + F0(Strength(mp)) + ")" : "");
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
                        return NReason.Relief;
                    }
                }
            }
            // POSCIG / PRZECHWYCENIE: wrogi cel, ktory dogoni jeszcze tej nocy (linia prosta x 1.1 jak trasa w A07 1.4)
            MobileParty tgt = stB == AiBehavior.EngageParty ? stP : (defB == AiBehavior.EngageParty ? defP : null);
            if (tgt != null && tgt.IsActive && Hostile(mp, tgt))
            {
                float d = mp.GetPosition2D.Distance(tgt.GetPosition2D);
                float hr = d * 1.1f / UnitsPerHour(mp);
                if (hr <= len)
                {
                    detail = "poscig za " + tgt.Name + " (" + F1(hr) + " h marszu, prosto " + F1(d) + " jedn., trasa ok. " + F1(d * 1.1f) + ")";
                    return NReason.Chase;
                }
            }
            return NReason.None;
        }

        /// <summary>
        /// ALARM: wrogi lord, banda albo gracz w promieniu AiCampDangerRadius, ktory NIE SPI (poza obozem, poza snem dluznikow,
        /// z wlaczonym AI), jest SILNIEJSZY (miara gry przy ucieczce: sila armii albo partii) i IDZIE NA LORDA (cel = lord albo
        /// odleglosc zmalala od poprzedniej godziny o > 0.2 jedn.). Wyszukiwanie przez lokator mapy (jak gra), nie po wszystkich.
        /// </summary>
        private static MobileParty AlarmThreat(MobileParty mp, Settings s, HashSet<MobileParty> campSet, out float ts, out float ms, out string how)
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
                    // silniejszy
                    float st = Strength(t);
                    if (st <= ms) continue;
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
                    ts = st; how = t.Name + " (sila " + F0(st) + " > " + F0(ms) + ", " + F1(dist) + " jedn., " + why + ")";
                    return t;
                }
                catch { }
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
            try { AiSleepLedger(s, h); } catch (Exception e) { Log.Error("NightRest.AiSleepLedger", e); }
            try { AiDebtCamp(s, h); } catch (Exception e) { Log.Error("NightRest.AiDebtCamp", e); }
            AiNightCamp(s, h);
            AiBanditRest(s, h);
            try { SnapshotPositions(s); } catch (Exception e) { Log.Error("NightRest.SnapshotPositions", e); }
        }

        /// <summary>Nowa gra albo wczytanie: ksiega AI od zera (zapis wczytuje ImportAi po tym).</summary>
        private static void ResetAi()
        {
            _ai.Clear(); _aiPenalty = new Dictionary<MobileParty, int>(); _debtSleep.Clear(); _alarmed.Clear(); _townHold.Clear();
            _prevPos = new Dictionary<MobileParty, Vec2>();
            _aiPending = null; _aiImportFresh = false; _debtWasOn = null; _ledgerStamp = -1;
            _lastDebtSweep = CampaignTime.Zero; _lastAlarmSweep = CampaignTime.Zero; _debtDhMax = 0; _alarmDhMax = 0;
            _alarmFled = 0; _alarmSlept = 0; _alarmOther = 0; _alarmResets = 0; _alarmBattle = 0; _debtWokenHour = 0;
            _ledgerMsSum = 0; _ledgerMsMax = 0; _ledgerCalls = 0;
            _day = new DayCounters();
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
        /// </summary>
        private static void TownNight(MobileParty mp, Settings s, AiSleep e, bool debtOn, NightTally t)
        {
            var st = mp.CurrentSettlement;
            bool held = _townHold.Contains(mp);
            if (mp.AttachedTo != null || st.SiegeEvent != null || Undead.Party(mp))
            {
                if (held) { _townHold.Remove(mp); EnableAi(mp); }
                Mark(e, StSkip);
                return;
            }
            if (_debtSleep.ContainsKey(mp)) { t.DebtSleepers++; Mark(e, StDebt); return; }
            if (!held && mp.Ai != null && mp.Ai.IsDisabled) { t.TownForeign++; Mark(e, StSkip); return; }   // AI trzyma kto inny (np. BK)
            bool leader = IsLeader(mp);
            int debt = debtOn && e != null ? e.Debt : 0;
            string det;
            var r = Classify(mp, s, debt, leader, null, out det);   // rozkaz lorda nietkniety - liczy sie biezacy
            if (r != NReason.None)
            {
                if (held) { _townHold.Remove(mp); EnableAi(mp); }
                t.TownOut++; t.Count(leader, r);
                Mark(e, StReason, r);
                Example(mp, det + " - wyjezdza z " + st.Name, debt, leader);
                return;
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
            int debt = debtOn && e != null ? e.Debt : 0;
            string det;
            var r = Classify(mp, s, debt, leader, asleep ? (mine ?? EmptyOrder) : null, out det);
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
            float ts, ms; string how;
            var th = AlarmThreat(mp, s, campSet, out ts, out ms, out how);
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
            string det;
            r = Classify(mp, s, e != null ? e.Debt : 0, leader, asleep ? (mine ?? EmptyOrder) : null, out det);
            if (r == NReason.None)
            {
                float a, b; string how;
                alarm = AlarmThreat(mp, s, campSet, out a, out b, out how) != null;
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
                  .Append(", AI trzyma inny mod (np. uczta BK) ").Append(t.TownForeign)
                  .Append("; na morzu ").Append(t.Sea).Append("; dluznicy w snie dlugu ").Append(t.DebtSleepers)
                  .Append(" (razem w snie dlugu ").Append(_debtSleep.Count).Append(", alarm dluznika w tym ticku ").Append(t.AlarmPending).Append(")")
                  .Append("; karawany z losowania ").Append(t.CaravansLot)
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
                  .Append("; dluznicy w snie dlugu ").Append(t.DebtSleepers).Append("; na morzu ").Append(t.Sea)
                  .Append("; karawany z losowania ").Append(t.CaravansLot).Append('.');
            }
            sb.Append(" Alarmy od poprzedniej godziny: ucieczka ").Append(_alarmFled).Append(", spi dalej ").Append(_alarmSlept)
              .Append(", inny marsz ").Append(_alarmOther).Append(" (cel AI cofniety ").Append(_alarmResets).Append(", bitwa/osada ").Append(_alarmBattle)
              .Append("); dluznicy obudzeni cudza reka ").Append(_debtWokenHour).Append('.');
            Log.Info(sb.ToString());
            _day.HAlarmFled += _alarmFled; _day.HAlarmSlept += _alarmSlept; _day.HAlarmOther += _alarmOther;
            _day.HTownHeld += t.TownHeld; _day.HTownOut += t.TownOut; _day.DebtWoken += _debtWokenHour;
            _alarmFled = 0; _alarmSlept = 0; _alarmOther = 0; _alarmResets = 0; _alarmBattle = 0; _debtWokenHour = 0;
        }

        // ------------------------------------------------------------ R2: ksiega snu AI
        private static void AiSleepLedger(Settings s, int h)
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
            bool isDawn = h == dawn;
            double nowH = NowH();
            var mt = campPast ? new MoveTally() : null;
            bool changed = imported;   // wczytane dlugi - kary od pierwszego ticku
            var lords = MobileParty.AllLordParties;
            var main = MobileParty.MainParty;
            if (lords != null)
            {
                for (int i = 0; i < lords.Count; i++)
                {
                    var mp = lords[i];
                    try
                    {
                        if (mp == null || mp == main || !mp.IsActive) continue;
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
                        bool moved = known && pos.Distance(e.Pos) > RestMoveLimit;
                        if (known)
                        {
                            bool resting = mp.CurrentSettlement != null || !moved || (s.SleepAtSeaFree && mp.IsCurrentlyAtSea)
                                           || _bedPos.ContainsKey(mp) || _debtSleep.ContainsKey(mp);
                            if (resting)
                            {
                                float inc = night ? 1f : dayF;
                                e.Rest += inc;
                                if (e.Acc >= 0f) e.Acc += inc;
                            }
                            if (mt != null && moved && mp.AttachedTo == null && mp.CurrentSettlement == null && mp.MapEvent == null)
                                mt.Add(e, IsLeader(mp), stamp - 1);
                        }
                        e.Pos = pos; e.Stamp = stamp;
                        // splata od reki (jak CreditRest gracza): cala suma -> dlug 0
                        float eff = e.Acc >= 0f ? Math.Max(e.Rest, e.Acc) : e.Rest;
                        if (!e.Credited && eff >= NeededAi(baza, e.Debt))
                        {
                            e.Credited = true;
                            if (e.Debt > 0) { e.Debt = 0; _day.Paid++; changed = true; }
                        }
                        if (isDawn && SettleAi(e, baza, nowH)) changed = true;
                    }
                    catch (Exception ex) { AiStumble("AiSleepLedger", mp, ex); }
                }
            }
            if (isDawn)
            {
                var dead = new List<MobileParty>();
                foreach (var kv in _ai)
                    if (kv.Key == null || !kv.Key.IsActive || kv.Value.Stamp != stamp) dead.Add(kv.Key);
                foreach (var m in dead)
                {
                    if (m == null) continue;
                    _ai.Remove(m); _debtSleep.Remove(m); _alarmed.Remove(m); _townHold.Remove(m);
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

        /// <summary>Swit ksiegi AI - jak SettleNight gracza: kto nie przespal bazy (i nie spi snem ciaglym), temu dlug +1 (maks. 3).</summary>
        private static bool SettleAi(AiSleep e, float baza, double nowH)
        {
            bool full = nowH - e.SinceH >= 18.0;
            bool sleptBase = e.Acc >= 0f || e.Rest >= baza;
            bool ch = false;
            if (e.NightFlags != 0) { _day.Marched++; if (sleptBase) _day.MarchedNoDebt++; }
            if (!full) _day.NoFullDay++;
            else if (!sleptBase)
            {
                e.Debt = Math.Min(3, e.Debt + 1);
                _day.NewDebt[e.Debt]++;
                if (e.Debt == 3) _day.Collapses++;   // zapasc: sen ciagly od reki (AiDebtCamp w tym samym ticku)
                ch = true;
            }
            e.Rest = 0f; e.Credited = false; e.NightFlags = 0;
            return ch;
        }

        /// <summary>Kary AI: nowy slownik (tylko dlug > 0) podmieniany w calosci; pamiec predkosci uniewazniona tylko zmienionym.</summary>
        private static void RebuildPenalties(bool on)
        {
            var old = _aiPenalty;
            var neu = new Dictionary<MobileParty, int>();
            if (on)
                foreach (var kv in _ai)
                    if (kv.Value.Debt > 0 && kv.Key != null && kv.Key.IsActive) neu[kv.Key] = kv.Value.Debt;
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
            foreach (var e in _ai.Values) { e.Debt = 0; e.Acc = -1f; }
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
                     + ", los (stare reguly) " + m.OldLot + ", stare wyjatki T1 " + m.OldOther + ", morze/Inni/doczepieni " + m.Other
                     + ", bez wpisu " + m.NoEntry + "; wodzowie armii w ruchu " + m.Lead + " (z powodem " + m.LeadReason + ").");
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
              .Append("; splacone od reki ").Append(d.Paid)
              .Append("; z dlugiem teraz 1/2/3: ").Append(now[1]).Append('/').Append(now[2]).Append('/').Append(now[3])
              .Append("; szly noca z powodem partii ").Append(d.Marched).Append(" (z nich odpoczely mimo to >= baza ").Append(d.MarchedNoDebt).Append(")")
              .Append("; partio-godziny nocnego marszu: ucieczka ").Append(d.HFlee).Append(", poscig ").Append(d.HChase).Append(", odsiecz ").Append(d.HRelief)
              .Append("; alarmy ").Append(d.HAlarm).Append(" (ucieczka ").Append(d.HAlarmFled).Append(", spi dalej ").Append(d.HAlarmSlept)
              .Append(", inny marsz ").Append(d.HAlarmOther).Append(")")
              .Append("; w osadach zostalo na noc (partio-godziny) ").Append(d.HTownHeld).Append(", wyjechalo z powodem ").Append(d.HTownOut)
              .Append("; oboz splaty od ").Append(DebtCampHour).Append(":00 partii ").Append(d.EveningCamps).Append(", sen ciagly ").Append(d.ContSleeps)
              .Append(", zwolnione ze snu dlugu ").Append(d.Released).Append(", dluznik szedl z powodem ").Append(d.DebtReason)
              .Append(", alarm dluznika ").Append(d.DebtAlarm).Append(", dluznicy obudzeni cudza reka ").Append(d.DebtWoken)
              .Append("; zapasci (nowy dlug 3) ").Append(d.Collapses)
              .Append(" | ruch w oknie obozu (pomiar z pozycji, ").Append(d.MoveHours).Append(" h): samotni lordowie ").Append(d.MoveLone)
              .Append(" partio-godzin, BEZ WPISU POWODU ").Append(d.MoveNoReason)
              .Append(" (sr. ").Append((d.MoveHours > 0 ? d.MoveNoReason / (float)d.MoveHours : 0f).ToString("0.0", ci)).Append(" na godzine; wyjazd z osady / po bitwie ")
              .Append(d.MoveExit).Append(", obudzeni cudza reka ").Append(d.MoveWoken).Append(", alarm bez ucieczki ").Append(d.MoveAlarmStay)
              .Append(", dluznicy obudzeni ").Append(d.MoveDebtWoken).Append(", inne ").Append(d.MoveOther).Append("); wodzowie armii ")
              .Append(d.MoveLead).Append(" (bez powodu ").Append(d.MoveLeadNoReason).Append(")")
              .Append(" | stoper ksiegi: sr. ").Append((_ledgerCalls > 0 ? _ledgerMsSum / _ledgerCalls : 0).ToString("0.000", ci))
              .Append(" ms, maks. ").Append(_ledgerMsMax.ToString("0.000", ci)).Append(" ms (").Append(_ledgerCalls).Append(" tikow)")
              .Append("; straznik snu dlugu - maks. odstep ").Append(_debtDhMax.ToString("0.000", ci)).Append(" h, alarmow ")
              .Append(_alarmDhMax.ToString("0.000", ci)).Append(" h gry; potkniecia T10 w sesji ").Append(_aiStumbles).Append('.');
            Log.Info(sb.ToString());
            _debtDhMax = 0; _alarmDhMax = 0;
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
                float D = me.ResultNumber, Fm = me.SumOfFactors, fl = 0f; bool hasM = false;
                foreach (var l in me.GetLines()) if (l.Item1 == name) { fl += l.Item2 / 100f; hasM = true; }
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
                         + " - wzor (dlug +1, gdy < baza i nie spi; maks. 3): " + (expect == Debt ? "zgodny" : "NIEZGODNY, oczekiwany " + expect) + ".");
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
            if (gone != null) foreach (var m in gone) if (m != null) _debtSleep.Remove(m);
            bool evening = InDebtEvening(h);
            HashSet<MobileParty> campSet = null;
            foreach (var kv in _ai)
            {
                var mp = kv.Key; var e = kv.Value;
                try
                {
                    if (mp == null || !mp.IsActive) continue;
                    DebtSleeper ds;
                    bool inDebt = _debtSleep.TryGetValue(mp, out ds);
                    if (e.Debt < 1 && !inDebt) continue;
                    bool want = e.Debt >= 2 || (e.Debt == 1 && evening);
                    var st = mp.CurrentSettlement;
                    bool can = mp.AttachedTo == null && mp.MapEvent == null && mp.BesiegerCamp == null && !mp.IsCurrentlyAtSea
                               && (st == null || st.SiegeEvent == null);
                    if (!want || !can) { if (inDebt) ReleaseDebt(mp, e, ds); continue; }
                    bool leader = IsLeader(mp);
                    string det;
                    // spiacy w polu: rozkaz sprzed snu; spiacy w osadzie: rozkaz nietkniety (tylko AI wstrzymane) - liczy sie biezacy
                    NightOrder slept = !inDebt ? null : (ds.Order != null ? ds.Order : (st != null ? null : EmptyOrder));
                    var r = Classify(mp, s, e.Debt, leader, slept, out det);
                    if (r != NReason.None)
                    {
                        if (inDebt) ReleaseDebt(mp, e, ds);
                        _day.DebtReason++;
                        Mark(e, StReason, r);
                        Example(mp, "dluznik idzie: " + det, e.Debt, leader);
                        continue;
                    }
                    if (st == null)
                    {
                        if (campSet == null) campSet = new HashSet<MobileParty>(_camping);
                        float ts, ms; string how;
                        var th = AlarmThreat(mp, s, campSet, out ts, out ms, out how);
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
                            Example(mp, "ALARM dluznika - " + how + " - AI ocenia ucieczke", e.Debt, leader);
                            continue;
                        }
                    }
                    if (!inDebt) EnterDebt(mp, e, null);
                    else
                    {
                        if (e.Debt >= 2 && ds.Kind != 2) { ds.Kind = 2; if (e.Acc < 0f) e.Acc = e.Rest; _day.ContSleeps++; }
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

        private static void EnterDebt(MobileParty mp, AiSleep e, NightOrder given)
        {
            // w osadzie wystarczy wstrzymac AI (gra nie wypusci partii z wylaczonym AI) - rozkaz zostaje nietkniety
            bool inTown = mp.CurrentSettlement != null;
            var ds = new DebtSleeper { Bed = mp.GetPosition2D, Kind = e.Debt >= 2 ? 2 : 1, Order = given ?? (inTown ? null : TakeOrder(mp)) };
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
                        if (e != null) { e.AlarmEnd = 1; if (InCamp(hNow)) e.NightFlags |= 8; }
                        continue;
                    }
                    if (mp.DefaultBehavior != AiBehavior.Hold) { mp.SetMoveModeHold(); _alarmResets++; }   // nowy cel strategiczny - bez powodu nie idzie
                    if (now - a.AtH < AlarmGraceHours) continue;
                    _alarmed.Remove(mp);
                    float drift = mp.GetPosition2D.Distance(a.Pos);
                    if (drift > RestMoveLimit) { _alarmOther++; if (e != null) e.AlarmEnd = 3; }
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
                if (s != null && DebtOn(s) && e != null && (e.Debt >= 2 || (e.Debt == 1 && InDebtEvening(h)))) { EnterDebt(mp, e, a.Order); return; }
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
                    if (kv.Key == null || !kv.Key.IsActive) continue;
                    if (e.Debt > 0 || e.Rest > 0.05f || e.Acc >= 0f || e.Credited) list.Add(kv);
                }
                list.Sort((a, b) => a.Value.Debt != b.Value.Debt ? b.Value.Debt.CompareTo(a.Value.Debt) : b.Value.Rest.CompareTo(a.Value.Rest));
                var ci = CultureInfo.InvariantCulture;
                var sb = new StringBuilder("v1|");
                int n = 0, debts = 0, sleeps = 0, skipped = 0;
                foreach (var kv in list)
                {
                    if (n >= AiSaveCap) break;
                    string id = kv.Key.StringId;
                    if (string.IsNullOrEmpty(id) || id.IndexOf(':') >= 0 || id.IndexOf(';') >= 0 || id.IndexOf('|') >= 0) { skipped++; continue; }
                    var e = kv.Value;
                    sb.Append(id).Append(':').Append(e.Debt).Append(':').Append(e.Rest.ToString("0.##", ci)).Append(':')
                      .Append(e.Credited ? '1' : '0').Append(':').Append(e.Acc.ToString("0.##", ci)).Append(';');
                    n++;
                    if (e.Debt > 0) debts++;
                    if (e.Acc >= 0f) sleeps++;
                }
                string str = sb.ToString();
                if (saving)
                    Log.Info("NocnyMarsz: zapis ksiegi snu AI - wpisow " + n + " (z dlugiem " + debts + ", w snie ciaglym " + sleeps + "), napis "
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

        /// <summary>Napis z zapisu -> wpisy ksiegi (przy pierwszym ticku po wczytaniu, gdy partie juz istnieja).</summary>
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
                int n = 0, missing = 0, bad = 0, sleeps = 0; int[] debts = new int[4];
                foreach (var part in data.Substring(bar + 1).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var f = part.Split(':');
                    if (f.Length < 5) { bad++; continue; }
                    MobileParty mp;
                    if (!byId.TryGetValue(f[0], out mp) || !mp.IsActive) { missing++; continue; }
                    int d; float r, a;
                    if (!int.TryParse(f[1], NumberStyles.Integer, ci, out d) || !float.TryParse(f[2], NumberStyles.Float, ci, out r)
                        || !float.TryParse(f[4], NumberStyles.Float, ci, out a)) { bad++; continue; }
                    var e = new AiSleep { Debt = Math.Max(0, Math.Min(3, d)), Rest = Math.Max(0f, r), Credited = f[3] == "1", Acc = a >= 0f ? a : -1f, SinceH = nowH - 48.0 };
                    _ai[mp] = e;
                    n++; debts[e.Debt]++; if (e.Acc >= 0f) sleeps++;
                }
                _aiImportFresh = true;
                Log.Info("NocnyMarsz: wczytano ksiege snu AI - wpisow " + n + " (z dlugiem 1/2/3: " + debts[1] + "/" + debts[2] + "/" + debts[3]
                         + ", w snie ciaglym " + sleeps + "; partii juz nie ma " + missing + ", zlych wpisow " + bad
                         + "); partie spoza zapisu bez dlugu, z pelna doba.");
                return n > 0;
            }
            catch (Exception e) { Log.Error("NightRest.ResolveImport", e); return false; }
        }
    }
}
