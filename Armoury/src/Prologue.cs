using System;
using System.Globalization;
using System.IO;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// m101-prolog (Jeff 21.10: "Robimy wczesniejszy start", "Ok wersje lekka robimy", krolestwa osobno + pakty i sojusze z krolestwem Roberta na czas prologu,
    /// "I zrob wydarzenia wedlug ksiazki"; projekt PROJEKT-PROLOG-298-2026-10-21.md, etapy E0-E2). PROLOG 298: nowa kampania zaczyna sie 188 dni gry przed startem
    /// opowiesci ROT (dzien wyjazdu krola Roberta z Krolewskiej Przystani do Winterfell), dzien 186 prologu - smierc Roberta, dzien 188 = dzien 0 ROT (egzekucja Neda).
    ///
    /// E0 - WYBOR STARTU. Okno przy "New Campaign" w MENU GLOWNYM (prefiks MBGameManager.StartNewGame dla SandBoxGameManager bez wczytywania zapisu), nie w kreatorze
    /// postaci: zegar kampanii (Campaign.OnInitialize: MapTimeTracker = new MapTimeTracker(CampaignTimeModel.CampaignStartTime)) i stan bohaterow z XML (Hero.Deserialize:
    /// alive="false" -> Dead) powstaja ZANIM ruszy kreator postaci i OnNewGameCreated - przesuniecie zegara potem przesunelo by wstecz wszystkie znaczniki czasu z tworzenia
    /// swiata (rozpoznanie m101, docs/noc-1920/m101-prolog-CHANGELOG.md). Wybor trzymamy w pamieci dla tej jednej instancji SandBoxGameManager; OnGameStart Armoury (pierwszy
    /// krok Campaign.OnInitialize) przypina go do kampanii (Active), PrologueBehavior zapisuje go w zapisie (SaveText.Sync "arm_prologue"). Wczytanie zapisu: Active = false
    /// w OnGameStart, potem wartosc z zapisu (LoadBehaviorData - przed CreateCampaignEvents i pierwszym tickiem). Zapis bez klucza (sprzed paczki, klasyczny) = klasyczny.
    /// Klucz PrologueEnabled (MCM, czytany przy kliknieciu - McmSettings.Apply): NIE = zawsze klasyczny start, bez okna. Autotest (LogGate.Mode 1/2): bez okna -
    /// plik Documents\Mount and Blade II Bannerlord\CrashScribe\armoury-prolog.flag = prolog, bez pliku = klasyczny (stare autotesty bez zmian).
    /// KLASYCZNY START: nic sie nie zmienia - zadnej latki prologu poza tym prefiksem (latki opowiesci wpinamy dopiero przy pierwszej kampanii z prologiem w procesie).
    /// Watek glowny (menu glowne, zdarzenia kampanii). Bledy: per wywolanie, liczone (Stumble), bez globalnego wylacznika.
    /// </summary>
    internal static partial class Prologue
    {
        internal const string Tag = "Prolog (m101): ";
        internal const int PrologueDays = 188;     // dzien 188 prologu = dzien 0 opowiesci ROT (projekt pkt 2)
        internal const int KingFallsDay = 186;     // smierc Roberta, Joffrey krolem, Ned w lochu (projekt pkt 2)
        internal const string TestFlagName = "armoury-prolog.flag";
        private const string SaveKey = "arm_prologue";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ---------------------------------------------------------------- stan
        // wybor z menu glownego dla jednej nowej kampanii (instancja SandBoxGameManager), zuzywany w OnGameStart
        private static object _creatingFor;
        private static bool _creatingPrologue;
        private static string _creatingWhy = "";
        private static bool _reentry;
        // biezaca kampania (statyczne - jedna kampania naraz; zerowane w OnGameStart kazdej kampanii)
        internal static bool Active;            // ta kampania zaczela sie prologiem 298
        internal static string ChosenBy = "";   // skad wybor (okno, autotest, zapis)
        private static int _stumbles, _stumblesDay;

        /// <summary>188 dni co do minuty (CampaignTime.Days(float) gubi do ulamka sekundy - liczymy z minut).</summary>
        internal static CampaignTime Span { get { return CampaignTime.Minutes(PrologueDays * 24L * 60L); } }

        /// <summary>Przesuniecie kalendarza dla modelu czasu (LongYearTimeModel.CampaignStartTime): kampania z prologiem.</summary>
        internal static bool CalendarShifted { get { return Active; } }

        internal static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            if (_stumbles <= 5) Log.Error("Prologue." + where + " (potkniecie " + _stumbles + ")", e);
        }

        internal static int Stumbles { get { return _stumbles; } }

        private static Settings S { get { return Settings.Current; } }

        // ---------------------------------------------------------------- E0: okno wyboru w menu glownym

        /// <summary>Raz na proces (SubModuleMain.OnBeforeInitialModuleScreenSetAsRoot): tylko prefiks MBGameManager.StartNewGame(MBGameManager). Wszystkie inne latki
        /// prologu (opowiesc ROT, pokoj i pakty, Robert i Ned z XML, egzekucja Neda) - leniwie przy pierwszej kampanii z prologiem (OnPrologueCampaign), dla nowej
        /// kampanii w OnGameStart, czyli przed zegarem i przed XML bohaterow.</summary>
        internal static void ApplyAll(Harmony h)
        {
            string a = "BRAK";
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(MBGameManager), "StartNewGame", new[] { typeof(MBGameManager) });
                if (m != null && h.Patch(m, prefix: new HarmonyMethod(typeof(Prologue), nameof(StartNewGamePrefix))) != null) a = "TAK";
            }
            catch (Exception e) { Stumble("ApplyAll.StartNewGame", e); }
            Log.Info(Tag + "latki menu glownego: okno wyboru " + a + " (reszta latek prologu - przy pierwszej kampanii z prologiem)"
                     + " | klucz Prologue Enabled " + (S != null && S.PrologueEnabled ? "TAK" : "NIE") + ".");
        }

        /// <summary>Prefiks MBGameManager.StartNewGame: nowa kampania SandBox - okno "Prologue 298" / "Classic 299" (false = start po wyborze), wczytanie zapisu - bez zmian.</summary>
        public static bool StartNewGamePrefix(MBGameManager gameLoader)
        {
            if (_reentry) return true;
            try
            {
                if (gameLoader == null || gameLoader.GetType().FullName != "SandBox.SandBoxGameManager") return true;
                object loading = null;
                try { loading = Traverse.Create(gameLoader).Property("LoadingSavedGame").GetValue(); } catch { }
                if (!(loading is bool) || (bool)loading) { Choose(null, false, "wczytanie zapisu"); return true; }
                McmSettings.Apply();   // klucz z MCM juz w menu glownym (MCM trzyma wartosci globalnie)
                var s = S;
                if (s == null || !s.PrologueEnabled) { Choose(gameLoader, false, "klucz Prologue Enabled NIE"); return true; }
                int mode = LogGate.Mode;
                if (mode == 1 || mode == 2)
                {
                    bool flag = File.Exists(FlagPath());
                    Choose(gameLoader, flag, flag ? "autotest, plik " + TestFlagName : "autotest bez pliku " + TestFlagName);
                    return true;
                }
                var loader = gameLoader;
                InformationManager.ShowInquiry(new InquiryData(
                    "Choose your start",
                    "Prologue 298 - the King rides north\nThe campaign begins half a year before the War of the Five Kings, on the day King Robert Baratheon leaves "
                    + "King's Landing for Winterfell. Robert and Eddard Stark are alive, the realms of Westeros keep the King's peace and stand with the crown by pacts and "
                    + "alliances. The story of Realm of Thrones begins when the King is dead and Lord Eddard has been executed.\n\n"
                    + "Classic start 299\nThe campaign begins as Realm of Thrones intends: Ned Stark has just been executed and the War of the Five Kings is at hand.",
                    true, true, "Prologue 298", "Classic 299",
                    () => Go(loader, true), () => Go(loader, false)), true, true);
                Log.Info(Tag + "nowa kampania - okno wyboru startu pokazane (Prologue 298 / Classic 299).");
                return false;
            }
            catch (Exception e)
            {
                Stumble("StartNewGamePrefix", e);
                Choose(null, false, "blad okna - klasyczny");
                return true;
            }
        }

        private static void Go(MBGameManager loader, bool prologue)
        {
            Choose(loader, prologue, "okno wyboru");
            _reentry = true;
            try { MBGameManager.StartNewGame(loader); }
            catch (Exception e) { Stumble("Go.StartNewGame", e); }
            finally { _reentry = false; }
        }

        private static void Choose(object loader, bool prologue, string why)
        {
            _creatingFor = loader;
            _creatingPrologue = loader != null && prologue;
            _creatingWhy = why ?? "";
            if (loader != null) Log.Info(Tag + "wybor startu nowej kampanii: " + (_creatingPrologue ? "PROLOG 298" : "KLASYCZNY 299") + " (" + _creatingWhy + ").");
        }

        internal static string FlagPath()
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(Path.Combine(Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), TestFlagName);
        }

        /// <summary>SubModuleMain.OnGameStart (przed LongYearTimeModel.Install i przed zegarem kampanii): nowa kampania z wyborem prologu = Active; kazda inna = false
        /// (wczytanie - wartosc przyjdzie z zapisu). Zeruje stan poprzedniej kampanii w procesie.</summary>
        internal static void OnGameStart(Game game)
        {
            try
            {
                ResetCampaign();
                bool mine = game != null && _creatingFor != null && ReferenceEquals(game.GameManager, _creatingFor);
                Active = mine && _creatingPrologue;
                ChosenBy = mine ? _creatingWhy : "";
                _creatingFor = null; _creatingPrologue = false; _creatingWhy = "";
                if (Active && (S == null || !S.LongYearEnabled))
                {
                    // bez modelu czasu Armoury (klucz Long Year Enabled) nie ma przesuniecia kalendarza ani startu opowiesci - klasyczny start
                    Active = false;
                    Log.Info(Tag + "wybrano prolog, ale klucz Long Year Enabled NIE (brak modelu czasu Armoury) - kampania KLASYCZNA.");
                }
                if (Active)
                {
                    Log.Info(Tag + "nowa kampania z PROLOGIEM 298 (" + ChosenBy + ") - kalendarz zacznie sie " + PrologueDays + " dni przed startem opowiesci ROT.");
                    OnPrologueCampaign("nowa kampania");
                }
            }
            catch (Exception e) { Stumble("OnGameStart", e); }
        }

        /// <summary>Kampania z prologiem w tym procesie (nowa albo wczytana): latki opowiesci ROT raz na proces (Prologue.Story).</summary>
        private static void OnPrologueCampaign(string why)
        {
            try { InstallStory(why); } catch (Exception e) { Stumble("InstallStory", e); }
            try { InstallRest(why); } catch (Exception e) { Stumble("InstallRest", e); }
        }

        private static bool _restTried;
        private static string _restState = "nie wpiete";

        /// <summary>Raz na proces, przy pierwszej kampanii z prologiem: pokoj i pakty (Peace), Robert i Ned z XML (Kings).</summary>
        private static void InstallRest(string why)
        {
            if (_restTried) return;
            _restTried = true;
            var h = new Harmony("com.jeff.armoury.prologue");
            string p = "-", k = "-", n = "-";
            try { p = ApplyPeace(h); } catch (Exception e) { Stumble("ApplyPeace", e); p = "BLAD"; }
            try { k = ApplyKings(h); } catch (Exception e) { Stumble("ApplyKings", e); k = "BLAD"; }
            try { n = ApplyEnd(h); } catch (Exception e) { Stumble("ApplyEnd", e); n = "BLAD"; }
            _restState = "pokoj i pakty: " + p + " | Robert i Ned z XML " + k + " | egzekucja Neda (NedEvent) " + n;
            Log.Info(Tag + "latki prologu (" + why + "): " + _restState + ".");
        }

        private static void ResetCampaign()
        {
            Active = false; ChosenBy = "";
            _stumblesDay = 0;
            ResetPeace();
            ResetKings();
            ResetEnd();
        }

        // ---------------------------------------------------------------- zapis

        internal static string Pack()
        {
            if (!Active) return "";
            return "v=1;on=1;by=" + Clean(ChosenBy) + ";" + PackPeace() + PackKings() + PackEnd();
        }

        internal static void Unpack(string v)
        {
            bool was = Active;
            Active = false;
            if (string.IsNullOrEmpty(v)) { if (was) Log.Info(Tag + "zapis bez prologu - kampania klasyczna."); return; }
            var map = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var part in v.Split(';'))
            {
                int i = part.IndexOf('=');
                if (i <= 0) continue;
                map[part.Substring(0, i)] = part.Substring(i + 1);
            }
            string on;
            Active = map.TryGetValue("on", out on) && on == "1";
            string by; ChosenBy = map.TryGetValue("by", out by) ? Unesc(by) : "zapis";
            UnpackPeace(map);
            UnpackKings(map);
            UnpackEnd(map);
            if (Active) OnPrologueCampaign("wczytany zapis");
        }

        /// <summary>Wartosc do zapisu "klucz=wartosc;" - znaki ';' i '=' (np. w tekscie "{=ROT01670}...") zakodowane (Uri.EscapeDataString), odczyt - Unesc.</summary>
        private static string Clean(string s) { return Uri.EscapeDataString(s ?? ""); }
        internal static string Unesc(string s) { try { return Uri.UnescapeDataString(s ?? ""); } catch { return s ?? ""; } }

        internal static int ParseInt(System.Collections.Generic.Dictionary<string, string> map, string key, int def)
        {
            string v; int r;
            return map.TryGetValue(key, out v) && int.TryParse(v, NumberStyles.Integer, Inv, out r) ? r : def;
        }

        // ---------------------------------------------------------------- zegar prologu

        /// <summary>Start opowiesci ROT (data ROT 299 - jak w klasycznym starcie): model Armoury (StoryStartTime), bez niego - start kampanii.</summary>
        internal static CampaignTime StoryStart()
        {
            var m = Campaign.Current.Models.CampaignTimeModel;
            var l = m as LongYearTimeModel;
            return l != null ? l.StoryStartTime : m.CampaignStartTime;
        }

        /// <summary>Dzien prologu (od startu kalendarza kampanii; 0..188 w prologu, potem dalej).</summary>
        internal static double PrologueDay()
        {
            return (CampaignTime.Now - Campaign.Current.Models.CampaignTimeModel.CampaignStartTime).ToDays;
        }

        /// <summary>Opowiesc ROT jeszcze nie ruszyla (CampaignTime.Now przed startem opowiesci).</summary>
        internal static bool BeforeStory()
        {
            try { return Active && CampaignTime.Now < StoryStart(); } catch { return false; }
        }

        /// <summary>OnSessionLaunched: linia startu sesji (nowa kampania i wczytanie).</summary>
        internal static void StartLine()
        {
            string cal = "-", story = "-";
            try
            {
                cal = CalendarText.Date(Campaign.Current.Models.CampaignTimeModel.CampaignStartTime, false);
                story = CalendarText.Date(StoryStart(), false);
            }
            catch { }
            Log.Info(Tag + "start sesji | prolog " + (Active ? "TAK" : "NIE") + (Active ? " (" + ChosenBy + ")" : "")
                     + " | start kalendarza " + cal + " | start opowiesci " + story + (Active ? " (dzien prologu " + PrologueDay().ToString("0.0", Inv) + ")" : "")
                     + " | latki opowiesci ROT: " + StoryState + " | " + _restState
                     + (Active ? " | " + DailyKings() + " | pakty: " + (_pactsEnded ? "zakonczone" : "nasze sojusze " + _ourAllies.Count + ", nasze pakty o nieagresji " + _ourNaps.Count)
                        + " | koniec prologu " + (_kingFallen ? "TAK" : "NIE") : "")
                     + " | klucz Prologue Enabled " + (S != null && S.PrologueEnabled ? "TAK" : "NIE") + " | potkniecia " + _stumbles + ".");
        }

        /// <summary>Po rozstawieniu swiata nowej kampanii (OnNewGameCreatedPartialFollowUpEnd): pakty z krolestwem Roberta.</summary>
        internal static void OnWorldReady()
        {
            if (!Active) return;
            string p = EnsurePacts("start kampanii");
            Log.Info(Tag + "swiat rozstawiony | pakty: " + p + ".");
        }

        private const int LogAfterStoryDays = 20;   // linia doby jeszcze tyle dni opowiesci (egzekucja Neda - dzien ~9 osi x4.33)

        /// <summary>Doba kampanii z prologiem: koniec prologu (co dobe, bez limitu dnia), pokoj i pakty (Peace), linia doby. Po starcie opowiesci - jeszcze
        /// LogAfterStoryDays dni linii, potem cisza w logu.</summary>
        internal static void Daily()
        {
            if (!Active) return;
            double day = PrologueDay();
            // recenzja R2: smierc krola (ponawiana, gdy krol w bitwie) i Ned w lochu - takze po dniu 208, inaczej krol w dlugim oblezeniu zostalby krolem na zawsze
            string end = DailyEnd();
            if (day > PrologueDays + LogAfterStoryDays) return;
            bool before = BeforeStory();
            string peace = DailyPeace();
            Log.Info(Tag + "dzien " + ((int)day).ToString(Inv) + " prologu (" + (before ? "opowiesc za " + (PrologueDays - day).ToString("0.0", Inv) + " dni"
                     : "opowiesc trwa, dzien opowiesci " + (day - PrologueDays).ToString("0.0", Inv)) + ") | " + end + " | " + peace + " | " + DailyKings()
                     + " | wydarzenia ROT ruszone " + RotEventsStarted() + " | potkniecia " + _stumblesDay + " (razem " + _stumbles + ").");
            _stumblesDay = 0;
        }

        // wydarzenia ROT (odbiciem, jak CrashScribe Fabula): ROTEventBehavior._events, EventBase.TotalEvents / IsActive
        private static System.Reflection.FieldInfo _fEvents;
        private static System.Reflection.PropertyInfo _pTotal, _pIsActive;
        private static Type _tEvBeh;

        /// <summary>Ile wydarzen ROT juz ruszylo (TotalEvents > 0 albo trwa) i ktore - w prologu ma byc 0.</summary>
        private static string RotEventsStarted()
        {
            try
            {
                if (_tEvBeh == null)
                {
                    _tEvBeh = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTEventBehavior");
                    var tBase = AccessTools.TypeByName("ROT.Events.EventBase");
                    if (_tEvBeh == null || tBase == null) return "- (brak ROT)";
                    _fEvents = AccessTools.Field(_tEvBeh, "_events");
                    _pTotal = AccessTools.Property(tBase, "TotalEvents");
                    _pIsActive = AccessTools.Property(tBase, "IsActive");
                }
                if (_fEvents == null || _pTotal == null || _pIsActive == null) return "- (brak pol ROT)";
                var mi = typeof(Campaign).GetMethod("GetCampaignBehavior");
                var beh = mi != null ? mi.MakeGenericMethod(_tEvBeh).Invoke(Campaign.Current, null) : null;
                var list = beh != null ? _fEvents.GetValue(beh) as System.Collections.IEnumerable : null;
                if (list == null) return "- (brak listy ROT)";
                int n = 0; var names = new System.Collections.Generic.List<string>();
                foreach (var ev in list)
                {
                    if (ev == null) continue;
                    int total = Convert.ToInt32(_pTotal.GetValue(ev, null));
                    bool act = Convert.ToBoolean(_pIsActive.GetValue(ev, null));
                    if (total <= 0 && !act) continue;
                    n++;
                    if (names.Count < 6) names.Add(ev.GetType().Name);
                }
                return n == 0 ? "0" : n + " (" + string.Join(", ", names.ToArray()) + ")";
            }
            catch (Exception e) { Stumble("RotEventsStarted", e); return "- (blad)"; }
        }

        internal static void ResetDay() { _stumblesDay = 0; }
        internal static int StumblesDay { get { return _stumblesDay; } }
    }

    /// <summary>m101-prolog: zachowanie kampanii - zapis wyboru (arm_prologue), zdarzenia prologu (Robert i Ned, pakty, koniec prologu - Prologue.Kings/Peace).</summary>
    internal sealed class PrologueBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
            CampaignEvents.OnNewGameCreatedPartialFollowUpEndEvent.AddNonSerializedListener(this, OnNewGameCreatedEnd);
            CampaignEvents.CanHeroDieEvent.AddNonSerializedListener(this, Prologue.CanHeroDie);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDaily);
        }

        public override void SyncData(IDataStore dataStore)
        {
            string v = "";
            try { if (dataStore.IsSaving) v = Prologue.Pack(); } catch (Exception e) { Prologue.Stumble("Pack", e); v = ""; }
            SaveText.Sync(dataStore, "arm_prologue", ref v);
            if (dataStore.IsLoading) { try { Prologue.Unpack(v); } catch (Exception e) { Prologue.Stumble("Unpack", e); } }
        }

        private void OnNewGameCreated(CampaignGameStarter starter)
        {
            try { Prologue.OnNewGameCreated(); } catch (Exception e) { Prologue.Stumble("OnNewGameCreated", e); }
        }

        private void OnNewGameCreatedEnd(CampaignGameStarter starter)
        {
            try { Prologue.OnWorldReady(); } catch (Exception e) { Prologue.Stumble("OnWorldReady", e); }
        }

        private void OnDaily()
        {
            try { Prologue.Daily(); } catch (Exception e) { Prologue.Stumble("Daily", e); }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            try { Prologue.StartLine(); } catch (Exception e) { Prologue.Stumble("StartLine", e); }
        }
    }
}
