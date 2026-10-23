using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// m109-list (Jeff 22.10 23:05: "jak wchodze na zadanie od kapitana i je biore, nie dostaje tego zadania w Quests") - ZADANIE GRY DLA PRZYJETEGO LISTU GONCZEGO.
    /// Zwykle zadanie (nie "special"), bez zleceniodawcy (QuestGiver null - bez relacji BK, blokady spraw i smierci notabla), bez typu w zapisie gry (bez definera):
    /// NoticeBoard.BountyBeforeSave zdejmuje je z QuestManager przed kazdym zapisem, po zapisie wraca, po wczytaniu odtwarzane z arm_board. Termin konczy nasza doba
    /// (WatchDaily), nie gra - OnBeforeTimedOut bez rozstrzygniecia (zadnego Timeout, wiec bez kary BK). Zadanie samo nie rusza zlota (wyplata tylko NoticeBoard.Pay).
    /// </summary>
    internal sealed class BountyQuest : QuestBase
    {
        internal readonly object Tag;                   // NoticeBoard.Notice - list, ktory zadanie odzwierciedla
        private readonly TextObject _title;
        internal string How = "";                       // jak zamkniete (nadpisania gry ponizej) - do linii "koniec" i "zamkniete przez gre"
        internal List<ITrackableCampaignObject> Saved;  // obiekty sledzone zdjete przed zapisem (wracaja po zapisie)
        internal bool Detached;                         // poza QuestManager (okno zapisu albo zatrzask po nieudanym zapisie)
        internal int QuietDay = -1;                     // doba ostatniego cichego wpisu o cudzej bitwie (najwyzej 1 na dobe)

        internal BountyQuest(string id, object tag, string band, int dueDay, int sum) : base(id, null, CampaignTime.Days(dueDay + 1), sum)
        {
            Tag = tag;
            _title = new TextObject("{=!}Bounty: " + (string.IsNullOrEmpty(band) ? "a band" : band));
        }

        public override TextObject Title { get { return _title; } }
        public override bool IsRemainingTimeHidden { get { return false; } }
        protected override void SetDialogs() { }
        protected override void InitializeQuestOnGameLoad() { }
        protected override void OnBeforeTimedOut(ref bool completeWithSuccess, ref bool doNotResolveTheQuest) { doNotResolveTheQuest = true; }
        protected override void OnCompleteWithSuccess() { How = "Sukces"; }
        public override void OnCanceled() { How = "Anulowane"; }
        public override void OnFailed() { How = "Nieudane"; }
        protected override void OnTimedOut() { How = "Termin gry"; }
        protected override void OnBetrayal() { How = "Zdrada"; }
        protected override void OnFinalize() { NoticeBoard.OnBountyFinalized(this); }
    }

    /// <summary>
    /// m109-list - LIST GONCZY W OKNIE QUESTS (projekt m109-list, wariant b). Jedno zrodlo prawdy: stan arm_board (Notice.Taken); zadanie gry (BountyQuest) tylko go
    /// odzwierciedla: przyjecie (TakeNotice - rozmowa z kapitanem i z soltysem ta sama droga) zaklada zadanie z opisem i trackerem bandy zamiast flagi "p" na mapie;
    /// banda uszla z bitwy - wpis; kazde zdjecie listu (Settle, OnPartyDestroyed, WatchDaily) zamyka zadanie: gracz rozbil bande - Sukces po wyplacie z Pay, termin -
    /// Nieudane, reszta - Anulowane, zawsze z wpisem. Zapis gry bez naszego typu: BountyBeforeSave (przed SyncData zachowan - MbEvent wola ostatnio dopisanych
    /// najpierw) zdejmuje zadania z QuestManager (_quests i sledzone obiekty z korekta licznika VisualTrackerManager) i z zaznaczenia okna Quests, BountySaveOver
    /// przywraca; w oknie zapisu kampania nie tyka (MapState: IsSaving - tylko SaveTick). Nieudany zapis - zatrzask do konca sesji. Wczytanie: BountySessionStart
    /// (OnAfterSessionLaunched - po QuestManager.OnGameLoaded, ktory anulowalby zadanie bez sprawy) odtwarza zadania przyjetych listow; wpisy z dziennika gry po id
    /// zadania (JournalLogEntry - trzyma tylko id), listy sprzed wgrania dostaja nowy opis. Klucz WatchBountyQuests NIE - zadnych zadan, flaga "p" jak dotad.
    /// Linie "Listy goncze - zadania (m109)"; hak autotestu tylko pod plikiem CrashScribe\armoury-list-test.flag (LogGate.Mode 1/2).
    /// </summary>
    internal static partial class NoticeBoard
    {
        internal const string PQ = "Listy goncze - zadania (m109): ";
        internal const string ListFlagName = "armoury-list-test.flag";
        private const int BqDetail = 20;      // linie "wpis" i "odtworzone" szczegolowo - pierwsze 20 w sesji, dalej tylko liczniki doby
        private const int BqDayEvery = 10;    // linia doby bez zdarzen co 10 dob
        private const int BqMaxIds = 9;       // id zadania listu: podstawowy i dopiski _2.._9 (ponowne przyjecie po zamknieciu przez gre)
        // powody zamkniecia (linia "koniec" i wpis)
        private const int EndPaid = 0, EndLord = 1, EndOthers = 2, EndEnemy = 3, EndGone = 4, EndKey = 5, EndDue = 6, EndOwner = 7, EndGrid = 8;

        private static readonly List<BountyQuest> _bq = new List<BountyQuest>();
        private static BountyQuest _closing;          // zamykane przez nasza logike (BountyClose) - OnFinalize nie liczy go jako "przez gre"
        private static BountyQuest _selSaved;         // zaznaczenie okna Quests zdjete przed zapisem
        private static bool _bqLatch;                 // nieudany zapis - do konca sesji bez zadan (flaga "p" jak dotad)
        private static bool _bqPending, _bqSaveLine;  // zapis w toku (po OnBeforeSave), linia "zapis" wypisana
        private static bool _bqLoaded;                // sesja z wczytanego zapisu (Import arm_board) - do haka autotestu
        private static int _bqWired;                  // nasluchy zapisu wpiete (OnBeforeSave, OnSaveOver)
        private static int _bqWpisLines, _bqOdtwLines;
        private static int _bdStart, _bdWpis, _bdOk, _bdCancel, _bdFail, _bdGame, _bdGrid, _bdSaves, _bdDet, _bdRe, _bdStumb;
        private static int _bqStumbles;

        internal static bool QuestsOn { get { var s = Settings.Current; return s != null && s.WatchBountyQuests; } }

        /// <summary>Zadanie tego modulu (QuestPay8 i "Your tasks" je pomijaja).</summary>
        internal static bool IsOurQuest(QuestBase q) { return q is BountyQuest; }

        /// <summary>ArmouryBehavior.RegisterEvents: ile nasluchow zapisu wpietych (do linii "wpiete").</summary>
        internal static void NoteBountyWired(int n) { _bqWired = n; }

        /// <summary>ResetWatch (nowa kampania i kazde wczytanie - przed SyncData).</summary>
        private static void BountyReset()
        {
            _bq.Clear(); _closing = null; _selSaved = null;
            _bqLatch = _bqPending = _bqSaveLine = _bqLoaded = false;
            _bqWpisLines = _bqOdtwLines = 0; _bqStumbles = 0;
            ZeroBountyDay();
            _listTest = false; _tSaveWait = false; _tDay0 = -1; _tStage = 0; _tSel = null; _tBattle = null; _tMe = null; _tRestored = 0;
        }

        private static void ZeroBountyDay() { _bdStart = _bdWpis = _bdOk = _bdCancel = _bdFail = _bdGame = _bdGrid = _bdSaves = _bdDet = _bdRe = _bdStumb = 0; }

        private static void BStumble(string where, Exception e) { _bdStumb++; _bqStumbles++; WStumble("Bounty." + where, e); }

        private static string BandTitle(Notice n) { return string.IsNullOrEmpty(n.Name) ? "a band" : n.Name; }

        // ------------------------------------------------------------ id zadania (bez zmian formatu zapisu: z listu, nie z zapisu)

        private static string BaseId(Notice n) { return "arm_bounty_" + n.Kind.ToString(Inv) + "_" + n.Town + "_" + n.Band + "_" + n.Due.ToString(Inv); }

        private static string IdAt(string b, int k) { return k <= 1 ? b : b + "_" + k.ToString(Inv); }

        /// <summary>Wpis dziennika gry (historia) zadania o tym id - MBObjectBase(id) jako sonda (JournalLogEntry.IsRelatedTo porownuje StringId).</summary>
        private static JournalLogEntry EntryOf(string id)
        {
            var h = Campaign.Current != null ? Campaign.Current.LogEntryHistory : null;
            if (h == null) return null;
            var probe = new MBObjectBase(id);
            return h.FindLastGameActionLog<JournalLogEntry>(x => { try { return x != null && x.IsRelatedTo(probe); } catch { return false; } });
        }

        private static bool IdInUse(string id) { foreach (var q in _bq) if (q.StringId == id) return true; return false; }

        /// <summary>Przyjecie: pierwszy id bez wpisu w dzienniku gry (null - wszystkie zajete).</summary>
        private static string FreeId(Notice n)
        {
            string b = BaseId(n);
            for (int k = 1; k <= BqMaxIds; k++) { string id = IdAt(b, k); if (!IdInUse(id) && EntryOf(id) == null) return id; }
            return null;
        }

        /// <summary>Wczytanie: ostatni id z otwartym wpisem dziennika gry; bez otwartego - pierwszy wolny (closed: byly tylko zamkniete wpisy).</summary>
        private static string RestoreId(Notice n, out JournalLogEntry je, out bool closed)
        {
            je = null; closed = false;
            string b = BaseId(n), free = null, open = null;
            for (int k = 1; k <= BqMaxIds; k++)
            {
                string id = IdAt(b, k);
                var e = EntryOf(id);
                if (e == null) { if (free == null && !IdInUse(id)) free = id; continue; }
                if (e.IsEnded()) closed = true;
                else { open = id; je = e; }
            }
            if (open != null) return open;
            return free;
        }

        // ------------------------------------------------------------ teksty zadania (po angielsku)

        /// <summary>Opis listu (pierwszy wpis zadania): kto wywiesil, na kogo, gdzie widziani, szkody, kwota i kto placi, ile dni, czy lord AI moze przejac.</summary>
        private static string Brief(Notice n)
        {
            var st = n.St ?? Settlement.Find(n.Town);
            bool live = n.Mp != null && n.Mp.IsActive;
            string near = live ? NearName(n.Mp.GetPosition2D) : null;
            string seen = near != null ? ", last seen near " + near + Away(st, n.Mp.GetPosition2D) : "";
            int days = Math.Max(0, n.Due - Today);
            var sb = new StringBuilder();
            if (n.Kind == 1)
            {
                var vs = Settlement.Find(n.Village);
                var head = Hero.Find(n.Giver);
                sb.Append("You told ").Append(NameOf(head)).Append(", headman of ").Append(vs != null ? vs.Name.ToString() : "a village").Append(", that you would hunt down ")
                  .Append(BandTitle(n)).Append(" - ").Append(n.Men.ToString(Inv)).Append(" men who plundered the village").Append(seen).Append('.');
                sb.Append("\n\nPay: ").Append(n.Sum.ToString("N0", Inv)).Append(" denars from the village purse, paid at once to ").Append(PaidTo()).Append(" - on top of whatever the band carries.");
            }
            else
            {
                sb.Append("You told ").Append(CaptainTitle(st)).Append(" of ").Append(st != null ? st.Name.ToString() : "the town").Append(" that you would hunt down ")
                  .Append(BandTitle(n)).Append(" - ").Append(n.Men.ToString(Inv)).Append(" men").Append(seen).Append('.').Append(HarmText(n));
                sb.Append("\n\nPay: ").Append(n.Sum.ToString("N0", Inv)).Append(" denars, paid at once to ").Append(PaidTo()).Append(" - on top of whatever the band carries. ").Append(PayerText(n, st));
            }
            sb.Append("\nTime: ").Append(days.ToString(Inv)).Append(days == 1 ? " day left." : " days left.")
              .Append(AiLordsPaid ? " The notice is public: if a lord breaks them first, he is paid and the notice comes down." : " If a lord breaks them first, the notice comes down unpaid.");
            sb.Append("\n\nThe band is marked on your map.");
            return sb.ToString();
        }

        private static string PayerName(Notice n, Settlement st)
        {
            if (n.Kind == 1) { var vs = Settlement.Find(n.Village); return "the village of " + (vs != null ? vs.Name.ToString() : "the headman"); }
            return "the council of " + (st != null ? st.Name.ToString() : "the town");
        }

        private static string BrokenBy(MobileParty lm, Settlement st)
        {
            if (lm == null) return "others";
            if (lm.IsGarrison) return "a garrison";
            if (lm.IsMilitia) return "the militia";
            if (lm.IsCaravan) return "a caravan";
            if (lm.IsVillager) return "villagers";
            if (lm.IsBandit) return "other outlaws";
            if (lm.IsLordParty && lm.LeaderHero != null) return NameOf(lm.LeaderHero) + "'s men";
            return "others";
        }

        /// <summary>Wpis zamkniecia (tabela projektu pkt 2.2).</summary>
        private static string EndText(Notice n, int why, int paid, Hero payee, MobileParty by, MapEvent me)
        {
            var st = n.St ?? Settlement.Find(n.Town);
            string b = BandTitle(n), town = st != null ? st.Name.ToString() : "the town";
            switch (why)
            {
                case EndPaid:
                {
                    string payer = PayerName(n, st);
                    if (paid <= 0) return "You broke " + b + ", but " + payer + " could pay nothing of the promised " + n.Sum.ToString("N0", Inv) + " denars.";
                    string t = "You broke " + b + ". " + Cap(payer) + " paid you " + paid.ToString("N0", Inv) + " denars.";
                    if (paid < n.Sum) t += " That is all they could pay of the promised " + n.Sum.ToString("N0", Inv) + ".";
                    return t;
                }
                case EndLord:
                    if (me != null && me.IsPlayerMapEvent) return b + " were broken by the host led by " + NameOf(payee) + ", so the bounty went to " + Him(payee) + ". The notice has come down.";
                    return NameOf(payee) + " broke " + b + " first and was paid the bounty. The notice has come down.";
                case EndOthers: return b + " were broken by " + BrokenBy(by, st) + "; the notice has come down unpaid.";
                case EndEnemy:
                    if (payee == Hero.MainHero)
                        return "You broke " + b + ", but " + (n.Kind == 1 ? PayerName(n, st) : GuardName(st) + " of " + town) + " will pay no bounty to an enemy of their town.";
                    return b + " were broken by " + NameOf(payee) + ", an enemy of " + town + "; the notice has come down unpaid.";
                case EndGone: return b + " have scattered; the notice has come down.";
                case EndKey: return "The notice has been taken down.";
                case EndDue: return "Time ran out: " + b + " are still at large, and the notice has come down.";
                case EndOwner: return town + " has a new lord, and the old council's notices have come down.";
                default: return "The notice is no longer on the board.";
            }
        }

        private static string PartyId(PartyBase p)
        {
            if (p == null) return "?";
            if (p.MobileParty != null) return p.MobileParty.StringId;
            return p.Settlement != null ? p.Settlement.StringId : "?";
        }

        private static string EndName(int why, Hero payee)
        {
            switch (why)
            {
                case EndPaid: return "wyplata gracz";
                case EndLord: return "lord " + (payee != null ? payee.StringId : "?");
                case EndOthers: return "straz/inni";
                case EndEnemy: return "wrog miasta";
                case EndGone: return "banda znikla";
                case EndKey: return "klucz";
                case EndDue: return "termin";
                case EndOwner: return "zmiana pana";
                default: return "siatka";
            }
        }

        // ------------------------------------------------------------ przyjecie (rozmowa z kapitanem, z soltysem, hak autotestu)

        /// <summary>Przyjecie listu przez gracza (dotychczasowe cialo CaptainTakeCons). m109: przy kluczu WatchBountyQuests, bez zatrzasku i bez zadania na tym liscie -
        /// zadanie w Quests z trackerem bandy; start nieudany albo klucz NIE (albo quest false - hak autotestu "stara droga") - flaga "p" na mapie jak dotad.</summary>
        private static void TakeNotice(Notice n, string how, bool quest)
        {
            if (!Live(n)) return;
            if (!n.Taken) { n.Taken = true; _wTakenToday++; }
            bool q = n.Quest != null;
            if (!q && quest && QuestsOn && !_bqLatch) q = BountyStart(n, how);
            if (!q) FlagTaken(n);
            Log.Info(PW + "przyjety przez gracza - " + n.Town + " " + n.Band + " (" + n.Men + " ludzi) = " + n.Sum);
        }

        /// <summary>Flaga "p" na bandzie przyjetego listu (kod 44c03a05; nasza czesc licznika VisualTrackerManager, zdejmowana po terminie listu).</summary>
        private static bool FlagTaken(Notice n)
        {
            var vt = Campaign.Current != null ? Campaign.Current.VisualTrackerManager : null;
            if (vt == null || n.Mp == null || FlagOf(n.Mp) != null) return false;
            vt.RegisterObject(n.Mp);
            _flags.Add(new Flag { Kind = "p", Id = n.Band, Until = Math.Max(Today + 1, n.Due), Obj = n.Mp });
            _dFlag++;
            return true;
        }

        private static bool BountyStart(Notice n, string how)
        {
            BountyQuest q = null;
            try
            {
                string id = FreeId(n);
                if (id == null)
                {
                    Log.Info(PQ + "start BLAD - " + n.Town + " " + n.Band + " (brak wolnego id - " + BqMaxIds + " zadan tego listu w dzienniku gry) - flaga na mapie jak dotad");
                    return false;
                }
                q = new BountyQuest(id, n, BandTitle(n), n.Due, n.Sum);
                _bq.Add(q); n.Quest = q;                   // od razu na liscie - zadanie, ktore weszlo do QuestManager, zawsze zdejmowane przed zapisem
                q.StartQuest();                            // gra: "Quest started", wpis w dzienniku gry (historia - tylko id zadania)
                q.AddLog(new TextObject("{=!}" + Brief(n)), true);
                bool tr = false;
                if (n.Mp != null && n.Mp.IsActive) { q.AddTrackedObject(n.Mp); tr = true; }
                _bdStart++;
                Log.Info(PQ + "start - " + id + " (" + n.Town + " " + n.Band + ", rodzaj " + n.Kind + ", kwota " + n.Sum + ", termin doba " + n.Due + ", tracker " + YN(tr)
                         + ", special " + YN(q.IsSpecialQuest) + ", droga " + how + ") - " + q.Title);
                return true;
            }
            catch (Exception e)
            {
                BStumble("Start", e);
                Log.Info(PQ + "start BLAD - " + n.Town + " " + n.Band + " (" + e.Message + ") - flaga na mapie jak dotad");
                try
                {
                    var qm = Campaign.Current != null ? Campaign.Current.QuestManager : null;
                    if (q != null && (qm == null || !qm.Quests.Contains(q))) { _bq.Remove(q); if (ReferenceEquals(n.Quest, q)) n.Quest = null; }
                }
                catch { }
                return false;
            }
        }

        // ------------------------------------------------------------ postep i zakonczenie

        /// <summary>Settle: banda z listem przegrala bitwe, ale uszla z sila (list stoi) - wpis; bitwa gracza z powiadomieniem, cudza cicho (najwyzej 1 na dobe).</summary>
        private static void BountyProgress(Notice n, MapEvent me, MapEventSide winSide, int left)
        {
            var q = n.Quest;
            if (q == null || !q.IsOngoing) return;
            try
            {
                bool mine = me != null && me.IsPlayerMapEvent;
                int day = Today;
                if (!mine && q.QuietDay == day) return;
                string near = null;
                try { near = me != null ? NearName(me.Position.ToVec2()) : null; } catch { near = null; }
                var lp = winSide != null ? winSide.LeaderParty : null;
                string winner = lp != null && lp.Name != null ? lp.Name.ToString() : null;
                string t = mine ? "You beat " + BandTitle(n) + (near != null ? " near " + near : "") + ", but " + left + " of them got away. The bounty still stands."
                                : BandTitle(n) + " were beaten" + (near != null ? " near " + near : "") + (winner != null ? " by " + winner : "") + " but got away with " + left + " men. The bounty still stands.";
                q.AddLog(new TextObject("{=!}" + t), !mine);
                if (!mine) q.QuietDay = day;
                _bdWpis++;
                if (_bqWpisLines < BqDetail)
                {
                    _bqWpisLines++;
                    Log.Info(PQ + "wpis - " + q.StringId + " banda uszla z bitwy (zostalo " + left + " z " + n.Men + " ludzi), zwyciezca " + PartyId(lp) + ", bitwa gracza " + YN(mine));
                }
            }
            catch (Exception e) { BStumble("Progress", e); }
        }

        /// <summary>Kazde zdjecie listu (Settle, OnPartyDestroyed, WatchDaily): zadanie listu zamkniete z wpisem. Bez zadania (klucz NIE) - nic.</summary>
        private static void BountyEnd(Notice n, int why, int paid = 0, Hero payee = null, MobileParty by = null, MapEvent me = null)
        {
            var q = n != null ? n.Quest : null;
            if (q == null) return;
            n.Quest = null;
            BountyClose(q, n, why, paid, payee, by, me);
        }

        private static void BountyClose(BountyQuest q, Notice n, int why, int paid, Hero payee, MobileParty by, MapEvent me)
        {
            _bq.Remove(q);
            if (!q.IsOngoing) return;   // juz zamkniete (przez gre) - bez drugiego zamkniecia
            try
            {
                string t = n != null ? EndText(n, why, paid, payee, by, me) : "The notice is no longer on the board.";
                _closing = q;
                q.AddLog(new TextObject("{=!}" + t), true);   // powiadomienie daje samo zamkniecie ("Quest completed / failed / canceled")
                if (why == EndPaid) { q.CompleteQuestWithSuccess(); _bdOk++; }
                else if (why == EndDue) { q.CompleteQuestWithFail(null); _bdFail++; }
                else { q.CompleteQuestWithCancel(null); _bdCancel++; if (why == EndGrid) _bdGrid++; }
            }
            catch (Exception e) { BStumble("End", e); }
            finally { _closing = null; }
            Log.Info(PQ + "koniec - " + q.StringId + " [" + (n != null ? n.Town + " " + n.Band : "? ?") + "] " + (q.How.Length > 0 ? q.How : "BLAD") + " (" + EndName(why, payee) + "), wyplacone " + paid
                     + " z " + (n != null ? n.Sum : 0) + ", wpisy " + q.JournalEntries.Count + (q.Detached ? ", odpiete" : ""));
            if (_listTest) TestAfterEnd(q);
        }

        /// <summary>BountyQuest.OnFinalize (kazde zamkniecie, przed QuestManager.OnQuestFinalized): obiekty sledzone zdjete z korekta licznika flag; zamkniecie spoza
        /// naszej logiki (gra: OnPlayerCharacterChanged, inny mod) - list znow wolny (przyjety NIE), ponowne przyjecie dostaje nowy id.</summary>
        internal static void OnBountyFinalized(BountyQuest q)
        {
            if (q == null) return;
            try { int fix = 0; Untrack(q, ref fix); } catch (Exception e) { BStumble("Finalize(tracker)", e); }
            if (ReferenceEquals(_closing, q)) return;
            try
            {
                _bq.Remove(q);
                var n = q.Tag as Notice;
                bool live = n != null && _notices.Contains(n);
                if (n != null && ReferenceEquals(n.Quest, q)) { n.Quest = null; n.Taken = false; }
                _bdGame++;
                Log.Info(PQ + "zamkniete przez gre - " + q.StringId + " (" + (q.How.Length > 0 ? q.How : "?") + ", list " + (live ? "czynny" : "zdjety") + ") - list znow wolny (przyjety NIE)");
            }
            catch (Exception e) { BStumble("Finalize", e); }
        }

        /// <summary>Obiekty sledzone zadania zdjete z QuestManager z dokladnym licznikiem flag VisualTrackerManager: gra przy wspolnym obiekcie (inne zadanie) nie odejmuje
        /// naszej flagi, a przy wylaczonym sledzeniu odejmuje cudza - obie roznice wyrownane (fix). Zwraca zdjete obiekty.</summary>
        private static List<ITrackableCampaignObject> Untrack(BountyQuest q, ref int fix)
        {
            var c = Campaign.Current;
            var qm = c != null ? c.QuestManager : null;
            var vt = c != null ? c.VisualTrackerManager : null;
            var objs = qm != null ? qm.GetAllTrackedObjectsOfAQuest(q) : new List<ITrackableCampaignObject>();
            bool en = q.IsTrackEnabled;
            foreach (var o in objs)
            {
                if (o == null) continue;
                bool alone = qm.TrackedObjects.ContainsKey(o) && qm.TrackedObjects[o].Count == 1;
                bool had = vt != null && vt.CheckTracked(o);
                qm.RemoveTrackedObjectForQuest(o, q);
                if (vt == null) continue;
                if (en && !alone) { if (vt.CheckTracked(o)) vt.RemoveTrackedObject(o); fix++; }   // nasza flaga (gra jej nie zdjela - obiekt ma inne zadanie)
                else if (!en && alone && had) { vt.RegisterObject(o); fix++; }                      // gra zdjela cudza flage (nasze sledzenie wylaczone) - oddana
            }
            return objs;
        }

        private static int TrackedCount(BountyQuest q)
        {
            try { var qm = Campaign.Current != null ? Campaign.Current.QuestManager : null; return qm != null ? qm.GetAllTrackedObjectsOfAQuest(q).Count : 0; }
            catch { return 0; }
        }

        // ------------------------------------------------------------ zapis gry (bez naszego typu)

        /// <summary>CampaignEvents.OnBeforeSaveEvent (przed SyncData zachowan): kazde BountyQuest zdjete z QuestManager (takze nieznane naszej liscie - siatka),
        /// zaznaczenie okna Quests na naszym zadaniu wyzerowane. Zawsze, niezaleznie od klucza.</summary>
        internal static void BountyBeforeSave()
        {
            var c = Campaign.Current;
            if (c == null) return;
            var qm = c.QuestManager;
            int n = 0, tr = 0, fix = 0, orphan = 0;
            bool sel = false;
            _bdSaves++;
            try
            {
                if (qm != null)
                {
                    foreach (var x in qm.Quests) { var b = x as BountyQuest; if (b != null && !_bq.Contains(b)) { _bq.Add(b); orphan++; } }
                    foreach (var kv in qm.TrackedObjects)
                        if (kv.Value != null) foreach (var x in kv.Value) { var b = x as BountyQuest; if (b != null && !_bq.Contains(b)) { _bq.Add(b); orphan++; } }
                }
                foreach (var q in _bq)
                {
                    if (q.Detached) continue;
                    try
                    {
                        q.Saved = Untrack(q, ref fix);
                        tr += q.Saved.Count;
                        if (qm != null) qm.OnQuestFinalized(q);   // tylko _quests.Remove - bez zdarzen
                        q.Detached = true;
                        n++;
                    }
                    catch (Exception e) { BStumble("BeforeSave(zadanie)", e); }
                }
            }
            catch (Exception e) { BStumble("BeforeSave", e); }
            try
            {
                var vdt = c.GetCampaignBehavior<IViewDataTracker>();
                var s = vdt != null ? vdt.GetQuestSelection() as BountyQuest : null;
                if (s != null) { _selSaved = s; vdt.SetQuestSelection(null); sel = true; }
            }
            catch (Exception e) { BStumble("BeforeSave(wybor)", e); }
            _bdGrid += orphan; _bdDet += n;
            _bqPending = true;
            _bqSaveLine = n > 0 || sel || orphan > 0;
            if (_bqSaveLine) Log.Info(PQ + "zapis - odpiete " + n + " (tracker " + tr + ", korekta flag " + fix + (orphan > 0 ? ", spoza listy " + orphan : "") + "), wybor okna Quests wyzerowany " + YN(sel));
        }

        /// <summary>CampaignEvents.OnSaveOverEvent: zapis udany i klucz TAK - zadania czynnych przyjetych listow z powrotem w QuestManager (z trackerem), zaznaczenie wraca;
        /// zapis nieudany - zatrzask: zadania zostaja odpiete do wczytania, ich bandy dostaja flage, nowe listy - flaga zamiast zadania.</summary>
        internal static void BountySaveOver(bool ok, string name)
        {
            if (!_bqPending) return;
            _bqPending = false;
            var c = Campaign.Current;
            var qm = c != null ? c.QuestManager : null;
            int m = 0, r = 0, tr = 0;
            bool sel = false;
            foreach (var q in _bq) if (q.Detached) m++;
            if (!ok)
            {
                _bqLatch = true;
                int fl = 0;
                ResolveFlags();   // flagi z zapisu po id - bez drugiej flagi na tej samej bandzie
                foreach (var q in _bq)
                {
                    var nt = q.Tag as Notice;
                    try { if (q.Detached && nt != null && ReferenceEquals(nt.Quest, q) && nt.Taken && Live(nt) && FlagTaken(nt)) fl++; } catch (Exception e) { BStumble("SaveOver(flaga)", e); }
                }
                _selSaved = null;
                Log.Info(PQ + "po zapisie NIEUDANY - zadania zostaja odpiete do wczytania (" + m + "); nowe listy bez zadan (flaga na mapie), flagi dla odpietych " + fl + " (zapis " + (name ?? "?") + ")");
                if (_listTest && _tSaveWait) { _tSaveWait = false; Log.Info(PQ + "TEST po zapisie - zapis NIEUDANY - NIE"); }
                return;
            }
            if (QuestsOn && !_bqLatch && qm != null)
            {
                foreach (var q in _bq)
                {
                    if (!q.Detached) continue;
                    var nt = q.Tag as Notice;
                    if (!q.IsOngoing || nt == null || !ReferenceEquals(nt.Quest, q) || !nt.Taken || !_notices.Contains(nt)) continue;   // bez listu - siatka doby
                    try
                    {
                        qm.OnQuestStarted(q);   // tylko _quests.Add - bez zdarzen
                        q.Detached = false;
                        if (q.Saved != null) foreach (var o in q.Saved) if (o != null) { q.AddTrackedObject(o); tr++; }
                        q.Saved = null;
                        r++;
                    }
                    catch (Exception e) { BStumble("SaveOver", e); }
                }
                try
                {
                    if (_selSaved != null && !_selSaved.Detached && _selSaved.IsOngoing)
                    {
                        var vdt = c.GetCampaignBehavior<IViewDataTracker>();
                        if (vdt != null && vdt.GetQuestSelection() == null) { vdt.SetQuestSelection(_selSaved); sel = true; }
                    }
                }
                catch (Exception e) { BStumble("SaveOver(wybor)", e); }
            }
            _selSaved = null;
            _bdRe += r;
            if (_bqSaveLine) Log.Info(PQ + "po zapisie OK - przywrocone " + r + " z " + m + " (tracker " + tr + "), wybor przywrocony " + YN(sel) + (QuestsOn ? "" : " (klucz NIE)"));
            _bqSaveLine = false;
            if (_listTest && _tSaveWait) { _tSaveWait = false; TestAfterSave(m, r, sel); }
        }

        // ------------------------------------------------------------ start sesji (po QuestManager.OnGameLoaded) i doba

        /// <summary>ArmouryBehavior: OnAfterSessionLaunchedEvent. Linia "wpiete"; przy kluczu TAK zadania przyjetych listow z arm_board (wpisy z dziennika gry po id;
        /// listy sprzed wgrania - nowy opis); klucz NIE - flagi "p" przyjetych listow jak przed m109.</summary>
        internal static void BountySessionStart()
        {
            _listTest = ListTestWanted();
            _tDay0 = Today;
            Log.Info(PQ + "wpiete - klucz Watch Bounty Quests " + YN(QuestsOn) + ", nasluchy zapisu " + _bqWired + "/2 (OnBeforeSave, OnSaveOver), BountyQuest bez definicji zapisu"
                     + " (zdejmowane przed zapisem, odtwarzane z arm_board), test autotestu " + YN(_listTest));
            int t = 0, q = 0, w = 0, e = 0, x = 0, b = 0, z = 0, f = 0, fl = 0;
            foreach (var n in _notices) if (n.Taken) t++;
            if (t > 0) ResolveFlags();   // flagi "p" z zapisu (44c03a05) po id - FlagTaken nie stawia drugiej
            if (!QuestsOn)
            {
                foreach (var n in _notices) { try { if (n.Taken && Live(n) && FlagTaken(n)) fl++; } catch (Exception ex) { BStumble("SessionStart(flaga)", ex); } }
                Log.Info(PQ + "wczytanie - klucz NIE, listy przyjete " + t + ", flagi na mapie dodane " + fl + " (bez zadan, jak przed m109)");
                return;
            }
            var qm = Campaign.Current != null ? Campaign.Current.QuestManager : null;
            foreach (var n in _notices)
            {
                if (!n.Taken || n.Quest != null) continue;
                try
                {
                    bool live = Live(n);   // banda po id (nazwa liczona po wczytaniu)
                    JournalLogEntry je;
                    bool closed;
                    string id = RestoreId(n, out je, out closed);
                    if (id == null || qm == null)
                    {
                        f++;
                        if (live) FlagTaken(n);
                        Log.Info(PQ + "odtworzone BLAD - " + n.Town + " " + n.Band + " (brak wolnego id) - flaga na mapie jak dotad");
                        continue;
                    }
                    if (closed && je == null) z++;
                    var bq = new BountyQuest(id, n, BandTitle(n), n.Due, n.Sum);
                    _bq.Add(bq); n.Quest = bq;
                    if (je != null)
                    {
                        var list = (MBList<JournalLog>)bq.JournalEntries;   // MBList : MBReadOnlyList - wpisy zadania wprost (bez refleksji)
                        foreach (var j in je.GetEntries()) if (j != null) { list.Add(j); e++; }
                        w++;
                    }
                    qm.OnQuestStarted(bq);   // tylko _quests.Add - bez zdarzen (bez "Quest started", kar i komunikatow)
                    if (je == null) { bq.AddLog(new TextObject("{=!}" + Brief(n)), true); x++; }   // gra sama zaklada wpis w dzienniku (OnQuestLogAdded)
                    if (live) { bq.AddTrackedObject(n.Mp); b++; }
                    q++;
                    if (_bqOdtwLines < BqDetail)
                    {
                        _bqOdtwLines++;
                        Log.Info(PQ + "odtworzone - " + id + " (" + n.Town + " " + n.Band + "), wpisy " + bq.JournalEntries.Count + ", z dziennika gry " + YN(je != null) + ", banda na mapie " + YN(live));
                    }
                }
                catch (Exception ex) { f++; BStumble("SessionStart", ex); }
            }
            Log.Info(PQ + "wczytanie - listy przyjete " + t + ", odtworzone " + q + " (z dziennika gry " + w + ", wpisow " + e + "; nowe opisy " + x + " - listy sprzed wgrania), z banda na mapie " + b
                     + ", pominiete: dziennik zamkniety (nowy id) " + z + ", blad " + f);
            _tRestored = q;
            if (_listTest && _bqLoaded && t > 0)
                Log.Info(PQ + "TEST wczytanie - odtworzone " + q + " z " + t + ", z dziennika gry " + w + ", nowe opisy " + x + ", blad " + f + " - " + YN(q == t && f == 0));
        }

        /// <summary>NoticeBoard.Daily (po WatchDaily): siatka (zadanie bez listu - Anulowane), klucz NIE - zadania zdjete i zapomniane (flaga "p" jak dotad), hak autotestu,
        /// linia doby (przy kluczu TAK, gdy cos sie dzialo albo co 10 dob).</summary>
        internal static void BountyDaily(int day)
        {
            if (!QuestsOn)
            {
                if (_bq.Count > 0) BountyKeyOff();
                ZeroBountyDay();
                return;
            }
            for (int i = _bq.Count - 1; i >= 0; i--)
            {
                if (i >= _bq.Count) continue;
                var q = _bq[i];
                var n = q.Tag as Notice;
                if (n != null && ReferenceEquals(n.Quest, q) && _notices.Contains(n)) continue;
                try { if (n != null && ReferenceEquals(n.Quest, q)) n.Quest = null; BountyClose(q, n, EndGrid, 0, null, null, null); }
                catch (Exception e) { BStumble("Daily(siatka)", e); }
            }
            if (_listTest) { try { TestDaily(day); } catch (Exception e) { BStumble("TestDaily", e); } }
            try
            {
                int act = 0, tr = 0, taken = 0, noq = 0;
                foreach (var q in _bq) if (!q.Detached && q.IsOngoing) { act++; if (TrackedCount(q) > 0) tr++; }
                foreach (var n in _notices) if (n.Taken) { taken++; if (n.Quest == null) noq++; }
                bool any = _bdStart + _bdWpis + _bdOk + _bdCancel + _bdFail + _bdGame + _bdGrid + _bdSaves + _bdStumb > 0;
                if (any || day % BqDayEvery == 0)
                    Log.Info(PQ + "dzien " + day + ", klucz TAK, czynne " + act + " (tracker " + tr + "), przyjete " + taken + " (bez zadania " + noq + "); dzis: start " + _bdStart + ", wpisy " + _bdWpis
                             + ", sukces " + _bdOk + ", anulowane " + _bdCancel + ", nieudane " + _bdFail + ", przez gre " + _bdGame + ", siatka " + _bdGrid + "; zapisy " + _bdSaves
                             + " (odpiete " + _bdDet + ", przywrocone " + _bdRe + "); zatrzask " + YN(_bqLatch) + "; potkniecia " + _bdStumb + " (sesja " + _bqStumbles + ")");
            }
            catch (Exception e) { BStumble("Daily(linia)", e); }
            ZeroBountyDay();
        }

        /// <summary>Klucz przelaczony na NIE w trakcie gry: zadania zdjete z QuestManager bez zdarzen i zapomniane (wpis dziennika gry zostaje otwarty - po wczytaniu
        /// przy kluczu TAK zadanie wraca z tym samym id), zywe przyjete listy - flaga "p" jak przed m109.</summary>
        private static void BountyKeyOff()
        {
            var c = Campaign.Current;
            var qm = c != null ? c.QuestManager : null;
            int n = 0, fl = 0, fix = 0;
            ResolveFlags();
            foreach (var q in _bq)
            {
                try
                {
                    if (!q.Detached) { Untrack(q, ref fix); if (qm != null) qm.OnQuestFinalized(q); n++; }
                    var nt = q.Tag as Notice;
                    if (nt != null && ReferenceEquals(nt.Quest, q)) { nt.Quest = null; if (nt.Taken && _notices.Contains(nt) && Live(nt) && FlagTaken(nt)) fl++; }
                }
                catch (Exception e) { BStumble("KeyOff", e); }
            }
            _bq.Clear();
            try { var vdt = c != null ? c.GetCampaignBehavior<IViewDataTracker>() : null; if (vdt != null && vdt.GetQuestSelection() is BountyQuest) vdt.SetQuestSelection(null); }
            catch (Exception e) { BStumble("KeyOff(wybor)", e); }
            _selSaved = null;
            Log.Info(PQ + "klucz NIE - odpiete " + n + " zadan, flagi na mapie " + fl + " - wroca po wczytaniu przy kluczu TAK");
        }

        // ------------------------------------------------------------ autotest (plik CrashScribe\armoury-list-test.flag, tylko LogGate.Mode 1/2 - w zwyklej grze nic)
        // Nowa kampania (bez wczytania): t=2 przyjecie dwoch listow (A - najwiecej ludzi, B - list soltysa albo najmniejsza banda) i zaznaczenie A w oknie Quests,
        // t=4 zapis "autotest-list-mid" (po nim TEST po zapisie), t=16 przyjecie C, t=18 list D stara droga (flaga, bez zadania - jak przed wgraniem).
        // Wczytany zapis z przyjetymi listami: start - TEST wczytanie, t=2 termin A' na wczoraj (nastepna doba - Nieudane), t=3 banda B' usunieta (Anulowane).
        // Bitwa gracza z banda: list (istniejacy albo testowy w najblizszym miescie, ten sam wzor kwoty) przyjety przy MapEventStarted; po bitwie TEST po koncu.

        private static bool _listTest, _tSaveWait;
        private static int _tDay0 = -1, _tStage, _tRestored;
        private static BountyQuest _tSel;
        private static Notice _tBattle;
        private static MapEvent _tMe;

        private static bool ListTestWanted()
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;
            try
            {
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return File.Exists(Path.Combine(Path.Combine(Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), ListFlagName));
            }
            catch { return false; }
        }

        private static bool FreeForTest(Notice n) { return n != null && !n.Taken && n.Quest == null && KindOn(n) && Live(n); }

        /// <summary>Stan zadania po przyjeciu (linia TEST przyjecie): w QuestManager, w toku, tracker gry, flaga na mapie, wpisy, wpis w dzienniku gry, bez flagi "p".</summary>
        private static void TestTakenLine(Notice n, string what)
        {
            var c = Campaign.Current;
            var q = n.Quest;
            bool inQm = q != null && c.QuestManager.Quests.Contains(q), on = q != null && q.IsOngoing;
            bool trk = q != null && n.Mp != null && c.QuestManager.TrackedObjects.ContainsKey(n.Mp) && c.QuestManager.TrackedObjects[n.Mp].Contains(q);
            bool vis = n.Mp != null && c.VisualTrackerManager.CheckTracked(n.Mp);
            int ent = q != null ? q.JournalEntries.Count : 0;
            bool hist = q != null && EntryOf(q.StringId) != null;
            bool flagP = n.Mp != null && FlagOf(n.Mp) != null;
            bool ok = inQm && on && trk && vis && ent >= 1 && hist && !flagP;
            Log.Info(PQ + "TEST przyjecie " + what + " - " + (q != null ? q.StringId : "brak zadania") + " (" + n.Town + " " + n.Band + ", rodzaj " + n.Kind + "): w QuestManager " + YN(inQm)
                     + ", IsOngoing " + YN(on) + ", tracker gry " + YN(trk) + ", flaga na mapie " + YN(vis) + ", wpisy " + ent + ", wpis w historii " + YN(hist) + ", flaga p " + YN(flagP) + " - " + YN(ok));
        }

        private static void TestDaily(int day)
        {
            int t = day - _tDay0;
            var c = Campaign.Current;
            if (c == null) return;
            if (!_bqLoaded)
            {
                if (_tStage == 0 && t >= 2)
                {
                    Notice a = null, b = null;
                    foreach (var n in _notices) if (n.Kind == 0 && FreeForTest(n) && (a == null || n.Men > a.Men)) a = n;
                    foreach (var n in _notices) if (n.Kind == 1 && FreeForTest(n)) { b = n; break; }
                    if (b == null) foreach (var n in _notices) if (n != a && n.Kind == 0 && FreeForTest(n) && (b == null || n.Men < b.Men)) b = n;
                    if (a == null)
                    {
                        if (t >= 8) { _tStage = 1; Log.Info(PQ + "TEST przyjecie - brak wolnego listu do doby t=" + t + " - NIE"); }
                        return;
                    }
                    TakeNotice(a, "TEST", true);
                    if (b != null) TakeNotice(b, "TEST", true);
                    _tSel = a.Quest;
                    try { var vdt = c.GetCampaignBehavior<IViewDataTracker>(); if (vdt != null && _tSel != null) vdt.SetQuestSelection(_tSel); } catch (Exception e) { BStumble("Test(wybor)", e); }
                    TestTakenLine(a, "A");
                    if (b != null) TestTakenLine(b, "B (" + (b.Kind == 1 ? "soltys" : "najmniejsza banda") + ")");
                    else Log.Info(PQ + "TEST przyjecie B - pominiete (brak drugiego wolnego listu)");
                    _tStage = 1;
                }
                else if (_tStage == 1 && t >= 4)
                {
                    _tStage = 2;
                    int act = 0;
                    foreach (var q in _bq) if (!q.Detached && q.IsOngoing) act++;
                    if (act == 0) { Log.Info(PQ + "TEST zapis - pominiete (brak czynnych zadan)"); return; }
                    _tSaveWait = true;
                    Log.Info(PQ + "TEST zapis - SaveAs autotest-list-mid (czynne zadania " + act + ", zaznaczenie " + YN(_tSel != null && _tSel.IsOngoing) + ")");
                    c.SaveHandler.SaveAs("autotest-list-mid");
                }
                else if (_tStage == 2 && t >= 16)
                {
                    _tStage = 3;
                    Notice n3 = null;
                    foreach (var n in _notices) if (FreeForTest(n) && (n3 == null || n.Men > n3.Men)) n3 = n;
                    if (n3 == null) { Log.Info(PQ + "TEST przyjecie C - pominiete (brak wolnego listu)"); return; }
                    TakeNotice(n3, "TEST", true);
                    TestTakenLine(n3, "C");
                }
                else if (_tStage == 3 && t >= 18)
                {
                    _tStage = 4;
                    Notice n4 = null;
                    foreach (var n in _notices) if (FreeForTest(n) && (n4 == null || n.Men > n4.Men)) n4 = n;
                    if (n4 == null) { Log.Info(PQ + "TEST stara droga - pominiete (brak wolnego listu)"); return; }
                    TakeNotice(n4, "TEST stara droga", false);
                    bool flag = n4.Mp != null && FlagOf(n4.Mp) != null;
                    Log.Info(PQ + "TEST stara droga - " + n4.Town + " " + n4.Band + ": przyjety " + YN(n4.Taken) + ", zadanie " + YN(n4.Quest != null) + ", flaga na mapie " + YN(flag)
                             + " - " + YN(n4.Taken && n4.Quest == null && flag));
                }
                return;
            }
            if (_tRestored <= 0) return;
            if (_tStage == 0 && t >= 2)
            {
                _tStage = 1;
                Notice a = null;
                foreach (var q in _bq)
                {
                    var n = q.Tag as Notice;
                    if (q.Detached || !q.IsOngoing || n == null || !ReferenceEquals(n.Quest, q)) continue;
                    if (a == null || q.JournalEntries.Count > a.Quest.JournalEntries.Count) a = n;   // najpierw zadanie z wpisami z dziennika gry
                }
                if (a == null) { Log.Info(PQ + "TEST termin - pominiete (brak czynnego zadania)"); return; }
                a.Due = day - 1;   // nastepna doba WatchDaily: day > Due - list zdjety (termin), zadanie Nieudane
                Log.Info(PQ + "TEST termin - " + a.Quest.StringId + " (" + a.Town + " " + a.Band + ") termin listu na dobe " + a.Due);
            }
            else if (_tStage == 1 && t >= 3)
            {
                _tStage = 2;
                Notice d = null;
                foreach (var q in _bq)
                {
                    var n = q.Tag as Notice;
                    if (q.Detached || !q.IsOngoing || n == null || !ReferenceEquals(n.Quest, q) || n.Mp == null || !n.Mp.IsActive || n.Mp.MapEvent != null) continue;
                    d = n; break;
                }
                if (d == null) { Log.Info(PQ + "TEST rozbicie - pominiete (brak drugiego zadania z banda na mapie)"); return; }
                Log.Info(PQ + "TEST rozbicie - " + d.Quest.StringId + " (" + d.Town + " " + d.Band + ") banda usunieta z mapy (DestroyPartyAction)");
                DestroyPartyAction.Apply(null, d.Mp);   // OnPartyDestroyed - list zdjety (banda znikla), zadanie Anulowane
            }
        }

        /// <summary>Po zapisie autotestu: zadania wrocily do QuestManager z trackerem, zaznaczenie okna Quests wrocilo.</summary>
        private static void TestAfterSave(int m, int r, bool sel)
        {
            var c = Campaign.Current;
            int inQm = 0, trk = 0, act = 0;
            foreach (var q in _bq)
            {
                if (!q.IsOngoing) continue;
                act++;
                if (!q.Detached && c.QuestManager.Quests.Contains(q)) inQm++;
                var n = q.Tag as Notice;
                if (n == null || n.Mp == null || !n.Mp.IsActive || TrackedCount(q) > 0) trk++;
            }
            bool selOk = _tSel == null || !_tSel.IsOngoing || sel;
            bool ok = m >= 1 && r == m && inQm == act && trk == act && selOk;
            Log.Info(PQ + "TEST po zapisie - zapis OK TAK, odpiete " + m + ", wrocily " + r + ", w QuestManager " + inQm + "/" + act + ", tracker " + trk + "/" + act + ", zaznaczenie " + YN(selOk) + " - " + YN(ok));
        }

        /// <summary>Po zamknieciu zadania w autotescie: poza QuestManager, wpis w dzienniku gry zamkniety, tracker zdjety.</summary>
        private static void TestAfterEnd(BountyQuest q)
        {
            try
            {
                var c = Campaign.Current;
                bool outQm = !c.QuestManager.Quests.Contains(q);
                var je = EntryOf(q.StringId);
                bool closed = je != null && je.IsEnded();
                bool noTrk = TrackedCount(q) == 0;
                Log.Info(PQ + "TEST po koncu - " + q.StringId + " " + (q.How.Length > 0 ? q.How : "BLAD") + ": poza QuestManager " + YN(outQm) + ", wpis w historii zamkniety " + YN(closed)
                         + ", tracker zdjety " + YN(noTrk) + " - " + YN(outQm && closed && noTrk && q.How.Length > 0));
            }
            catch (Exception e) { BStumble("TestAfterEnd", e); }
        }

        /// <summary>ArmouryBehavior: CampaignEvents.MapEventStarted - tylko autotest. Gracz przeciw bandzie: jej list (albo list testowy w najblizszym miescie, ten sam wzor
        /// kwoty co WatchDaily, placi kasa miasta) przyjety wspolna droga TakeNotice; wynik w OnMapEventEnded (Settle) i TestBattleEnd.</summary>
        internal static void OnMapEventStartedTest(MapEvent me, PartyBase a, PartyBase d)
        {
            if (!_listTest || me == null || _tBattle != null || !QuestsOn) return;
            try
            {
                var main = PartyBase.MainParty;
                PartyBase other = a == main ? d : d == main ? a : null;
                var band = other != null ? other.MobileParty : null;
                if (band == null || !band.IsBandit || !band.IsActive) return;
                Notice n = null;
                foreach (var x in _notices) if (x.Band == band.StringId) { n = x; break; }
                if (n == null)
                {
                    var st = NearestTown(band.GetPosition2D);
                    if (st == null || st.Town == null || st.OwnerClan == null || !WatchOn) { Log.Info(PQ + "TEST list testowy - pominiete (" + band.StringId + ": brak miasta albo klucz listow NIE)"); return; }
                    var cand = new Cand { Mp = band, Men = band.Party != null ? band.Party.NumberOfHealthyMembers : 0 };
                    Score(cand, st);
                    float rate = GarrisonRate(st);
                    int N = Math.Max(10, Math.Min(60, cand.Men)), D = cand.Mounted ? 4 : 3;
                    int sum = Math.Max(1, (int)Math.Round(N * D * rate * Premium * (1 + cand.Bonus / 100.0)));
                    int day = Today;
                    n = new Notice
                    {
                        Town = st.StringId, Band = band.StringId, Name = BandNameOf(band), Owner = st.OwnerClan.StringId, Men = cand.Men, Days = D, BonusPct = cand.Bonus, S = rate, Sum = sum,
                        Posted = day, Due = day + DaysUp, St = st, Mp = band
                    };
                    _notices.Add(n);
                    Log.Info(PQ + "TEST list testowy - " + st.StringId + " " + band.StringId + " (" + cand.Men + " ludzi, " + D + " d, S " + rate.ToString("0.00", Inv) + ", szkody +" + cand.Bonus + "%) = " + sum
                             + ", platnik kasa (bitwa gracza)");
                }
                else Log.Info(PQ + "TEST list testowy - istniejacy list " + n.Town + " " + n.Band + " = " + n.Sum + ", przyjety " + YN(n.Taken) + ", zadanie " + YN(n.Quest != null) + " (bitwa gracza)");
                _tBattle = n; _tMe = me;
                if (n.Quest == null) TakeNotice(n, "TEST bitwa", true);
            }
            catch (Exception e) { BStumble("TestBattle", e); }
        }

        /// <summary>OnMapEventEnded (po Settle): bitwa testowa bez rozbicia bandy - zadanie ma trwac.</summary>
        private static void TestBattleEnd(MapEvent me)
        {
            var n = _tBattle;
            _tBattle = null; _tMe = null;
            if (n == null || n.Quest == null) return;   // zamkniete w Settle - linia TEST po koncu juz jest
            var q = n.Quest;
            bool inQm = Campaign.Current.QuestManager.Quests.Contains(q);
            Log.Info(PQ + "TEST po koncu - " + q.StringId + " bitwa bez rozbicia bandy (zwyciezca " + me.WinningSide + "): zadanie trwa " + YN(q.IsOngoing) + ", w QuestManager " + YN(inQm) + " - " + YN(q.IsOngoing && inQm));
        }
    }
}
