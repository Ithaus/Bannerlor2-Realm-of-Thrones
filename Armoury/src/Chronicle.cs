using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// m105-kronika K0 (krok A2) - SILNIK KRONIKI WESTEROS: doba, okna, wybory, zapis. Kontrakt danych: docs/KRONIKA-FORMAT.md (wygrywa przy sprzecznosci), plan:
    /// PLANY-E5-WOJNY-KRONIKA "## PLAN kronika" (E: Chronicle.cs, G: linie logu). Dane - Chronicle.Data (A1), warunki - Chronicle.Terms (A1), stan okien - Chronicle.State.
    /// DOBA (ChronicleBehavior.OnDaily, po CanonCompanions - mv: swiezy): Terms.Daily (pierwsze dni ev:/done:), akcje czekajace (Actions.Daily), okna powracajace
    /// (Recurring.Daily), plan okien (Plan): dla kazdego okna nierozstrzygnietego co dobe liczone po/warunki (pamiec pierwszego spelnienia +N - takze w prologu i przed od),
    /// potem (poza prologiem, Prologue.BeforeStory): warunek "nigdy" -> POMINIETE z powodem; kotwica przed juz zaszla albo spoznienie ponad MaxLate po do -> wiesc
    /// (jest tekst _W i swiat sie zgadza; gracz spoza kolumny gracz z inni=nic - rozstrzygniecie "nic") albo POMINIETE; nalezne (dzien opowiesci >= od, po i warunki
    /// prawdziwe) -> wybor wiersza (warianty w kolejnosci pliku z filtrem wersji, potem glowny) i formy (gracz -> forma, inaczej inni): okno - kolejka do pokazania,
    /// wiesc / log / nic - rozstrzygniecie od razu. Linia doby (plan G).
    /// OKNA (Chronicle.AppTick z SubModuleMain.OnApplicationTick, 4 razy na sekunde, poza tickiem kampanii jak sceny prologu): bramki Prologue.SceneGate + brak okna
    /// opowiesci (StoryWindow.IsOpen) + StableSec s spokoju, najwyzej PerDay okien na dobe, GapHours od poprzedniego okna Kroniki i od zamkniecia okna wydarzenia ROT
    /// (EventPopupState znika ze stosu stanow). Przed pokazaniem ponowna kontrola warunkow i wyboru wiersza (swiat mogl sie zmienic od doby). Okno: StoryWindow
    /// (ArmStoryEvent.Key = kr_NNN_w[war] i VoiceMd5 = md5 tekstu okna dla lektora - Chronicle.Voice, zasada md5; A3), obrazek Chronicle.Pictures.Pick (A3: pula tematu,
    /// staly skrot id, bez powtorki poprzedniego; null = Pictures.Fallback). Rozstrzygniecie przy pokazaniu: stan (State),
    /// skutki okna (Actions.Run), wybor (Choice - lista po zamknieciu albo opcja bierna). Okno, ktore nie otworzylo sie 3 razy - POMINIETE.
    /// ZAPIS: jeden klucz "arm_kronika" (SaveText.Sync): State + Terms + paczki znane w zapisie (pk) + tempo + Choice + Actions + Recurring. Zapis bez klucza (sprzed
    /// Kroniki) - stan pusty, paczki nowe w zapisie: okna minione (do juz minal albo od minal o ponad MaxLate) POMINIETE "zapis sprzed paczki kNN" - bez lawiny.
    /// Klucz ChronicleOfWesteros NIE: brak okien i list (stan zostaje, doba pisze tylko linie). Wyjatki per okno - potkniecie (licznik Stumble), nic nie gasnie.
    /// </summary>
    internal static partial class Chronicle
    {
        // --- stale (plan F, bez pytania Jeffa) ---
        internal const int MaxLate = 30;              // dni opowiesci po do (i w kolejce) - potem wiesc albo POMINIETE
        internal const int PerDay = 2;                // okien Kroniki na dobe
        internal const double GapHours = 6.0;         // od poprzedniego okna Kroniki i od zamkniecia okna ROT
        internal const double StableSec = 2.0;        // bramki spelnione tyle sekund z rzedu
        internal const double ChoiceDelay = 0.5;      // lista wyboru po zamknieciu okna
        internal const int WaitActionDays = 30;       // akcja CZEKA najdluzej tyle dni
        internal const int MaxRecurringPerDay = 1;    // instancji okien powracajacych na dobe
        internal const int RecurringKeys = 400;       // rejestr kluczy instancji w zapisie
        private const string SaveKey = "arm_kronika";
        private const int SaveWarnBytes = 24 * 1024;
        private const int OpenFailsMax = 3;
        private const int WaitShown = 6;              // ile okien czekajacych z powodem w linii doby
        private static readonly long CheckTicks = Stopwatch.Frequency / 4;

        /// <summary>Wiersz wybrany do pokazania: wiersz, gracz w kolumnie gracz, forma, zmienne.</summary>
        internal sealed class RowPick
        {
            public Data.RowDef Row;
            public bool Player;
            public string Form = "", PlayerWhy;
            public Dictionary<string, string> Vars;
        }

        /// <summary>Okno w kolejce do pokazania (zwykle - Id = NNN.w, Tok = null; instancja okna powracajacego - Id = NNN.w#klucz, Tok).</summary>
        private sealed class Due
        {
            public string Id = "";
            public Data.WindowDef W;
            public Terms.Tokens Tok;
            public bool Inst;
        }

        private sealed class OpenWin
        {
            public string Id = "";
            public Data.WindowDef W;
            public RowPick P;
            public Terms.Tokens Tok;
            public StoryWindow.Spec Spec;
            public long At;
            public bool FilePic;
        }

        // --- stan kampanii (zapis) ---
        private static readonly Dictionary<int, int> _packsKnown = new Dictionary<int, int>();   // pk: paczka -> dzien kampanii, w ktorym weszla do zapisu
        private static int _tempoDay = -1, _tempoCount;
        private static double _lastWinHour = -1e9;
        // --- stan sesji ---
        private static bool _newCampaign;
        private static readonly List<Due> _ready = new List<Due>();
        private static readonly List<string> _waitInfo = new List<string>();
        private static int _waitCount;
        private static OpenWin _open;
        private static int _openSeq, _openFails;
        private static string _openFailId;
        private static long _lastCheck, _okSince, _closedAt;
        private static bool _rotWasOn;
        private static double _rotClosedHour = -1e9;
        private static int _saveBytes;
        // liczniki doby
        private static int _dShown, _dNews, _dSkipped, _dLog, _dNothing, _dFails;

        // ---------------------------------------------------------------- cykl kampanii

        /// <summary>Nowa kampania i wczytanie (konstruktor ChronicleBehavior w OnGameStart) oraz OnGameEnd: caly stan kampanii od zera (dane plikow zostaja).</summary>
        internal static void ResetCampaign()
        {
            State.Reset();
            Terms.Reset();
            _packsKnown.Clear();
            _tempoDay = -1; _tempoCount = 0; _lastWinHour = -1e9;
            _newCampaign = false;
            _ready.Clear(); _waitInfo.Clear(); _waitCount = 0;
            _open = null; _openFails = 0; _openFailId = null;
            _okSince = 0; _closedAt = 0; _rotWasOn = false; _rotClosedHour = -1e9;
            _saveBytes = 0;
            StumblesDay = 0;
            DayReset();
            Choice.Reset();
            Actions.Reset();
            Recurring.Reset();
            Pictures.Reset();
        }

        private static void DayReset() { _dShown = _dNews = _dSkipped = _dLog = _dNothing = _dFails = 0; }

        /// <summary>OnNewGameCreated: paczki obecne przy starcie kampanii sa "znane od startu" (bez pomijania minionych okien).</summary>
        internal static void OnNewGame() { _newCampaign = true; }

        /// <summary>SubModuleMain.OnGameEnd: wyjscie z kampanii - okno i listy bez akcji po zamknieciu, stan od zera.</summary>
        internal static void GameEnd()
        {
            try { ResetCampaign(); }
            catch (Exception e) { Stumble("GameEnd", e); }
        }

        // ---------------------------------------------------------------- czas
        private static int Day() { return Terms.Day(); }

        private static double HourNow() { try { return CampaignTime.Now.ToHours; } catch { return 0.0; } }

        private static string F1(double v) { return v.ToString("0.0", Inv); }

        // ---------------------------------------------------------------- start sesji

        /// <summary>OnSessionLaunched (nowa kampania i wczytanie): dane (EnsureLoaded), paczki nowe w zapisie (pk), kolejka okien do pokazania, linia startu.</summary>
        internal static void SessionStart()
        {
            try { Data.EnsureLoaded(); } catch (Exception e) { Stumble("SessionStart.Data", e); }
            try { Terms.Invalidate(); } catch (Exception e) { Stumble("SessionStart.Terms", e); }
            try { CheckPacks(); } catch (Exception e) { Stumble("SessionStart.Packs", e); }
            try { Recurring.SessionStart(); } catch (Exception e) { Stumble("SessionStart.Recurring", e); }
            try { if (KeyOn) Plan(Day(), false); } catch (Exception e) { Stumble("SessionStart.Plan", e); }
            try { StartLine(); } catch (Exception e) { Stumble("SessionStart.Line", e); }
        }

        /// <summary>Paczki dobrych plikow, ktorych nie bylo w zapisie: nowa kampania - znane od startu; wczytany zapis - okna minione POMINIETE ("zapis sprzed paczki").</summary>
        private static void CheckPacks()
        {
            int day = Day();
            double sd = Terms.StoryDay();
            foreach (var p in Data.Packs)
            {
                if (!p.Ok || _packsKnown.ContainsKey(p.Num)) continue;
                _packsKnown[p.Num] = day;
                if (_newCampaign) continue;
                int n = 0;
                foreach (var w in Data.Windows)
                {
                    if (w.Pack != p || w.Recur != null || State.IsDone(w.Id)) continue;
                    bool gone = (w.Do >= 0 && sd > Terms.DocToStory(w.Do)) || (w.Od > 0 && sd > Terms.DocToStory(w.Od) + MaxLate);
                    if (!gone) continue;
                    Skip(w.Id, "zapis sprzed paczki " + p.Name);
                    n++;
                }
                Log.Info(Tag + "paczka " + p.Name + " nowa w zapisie (dzien kampanii " + day + ", dzien opowiesci " + F1(sd) + ") - minionych okien pominietych " + n + ".");
            }
        }

        /// <summary>Lektor do linii startu (plan G "lektor okien z nagraniem a/b"): wiersze lektor=1 pokazywane w oknie, dla kazdego zasada md5 (Chronicle.Voice) -
        /// z nagraniem zgodnym / bez nagrania / tekst inny niz nagrany / stary tekst w manifescie / plik odrzucony / zmienne.</summary>
        private static string VoiceStartText()
        {
            int narr = 0, rec = 0, none = 0, changed = 0, stale = 0, bad = 0, vars = 0;
            foreach (var w in Data.Windows)
            {
                foreach (var r in Rows(w))
                {
                    if (!r.Narrated || !r.Shows) continue;
                    narr++;
                    string md5, why = Voice.Why(r, out md5);
                    if (why == null) rec++;
                    else if (why.StartsWith("tekst okna inny", StringComparison.Ordinal)) changed++;
                    else if (why.IndexOf("starym tekstem", StringComparison.Ordinal) >= 0) stale++;
                    else if (why.StartsWith("plik ", StringComparison.Ordinal)) bad++;
                    else if (why.StartsWith("zmienne", StringComparison.Ordinal)) vars++;
                    else none++;
                }
            }
            return "lektor: wierszy lektor=1 z nagraniem " + rec + "/" + narr + " (bez nagrania " + none + ", tekst inny niz nagrany " + changed + ", stary tekst w manifescie " + stale
                   + ", plik odrzucony " + bad + ", zmienne " + vars + "; klucze lektora okien " + YN(Lektor.WindowsEnabled) + ") | " + Lektor.KronikaText();
        }

        private static void StartLine()
        {
            int okno, wiesc, log, nic;
            State.FormCounts(out okno, out wiesc, out log, out nic);
            double sd = Terms.StoryDay();
            Data.WindowDef next = null;
            foreach (var w in Data.Windows)
            {
                if (w.Recur != null || State.IsDone(w.Id) || Terms.DocToStory(w.Od) <= sd) continue;
                if (next == null || w.Od < next.Od) next = w;
            }
            Log.Info(Tag + "start sesji | klucze: Chronicle Of Westeros " + YN(KeyOn) + ", World Changes " + YN(KeyWorld) + ", Full Versions " + YN(KeyFull)
                     + ", Player Choices " + YN(KeyChoices) + " | " + Data.Summary()
                     + " | os x" + Terms.DocK().ToString("0.00", Inv) + ", plan Fabuli " + YN(Terms.PlanAvailable)
                     + " | " + Pictures.StartText()
                     + " | " + VoiceStartText()
                     + " | zapis: pokazane " + okno + ", wiesci " + wiesc + ", log " + log + ", nic " + nic + ", pominiete " + State.Skipped + ", wybory " + State.Choices
                     + ", flagi " + State.Flags + ", czeka na wybor " + Choice.Count + ", akcje czekaja " + Actions.WaitCount + ", ukryci " + Actions.HiddenText()
                     + ", instancje w kolejce " + Recurring.QueueCount + ", paczki znane " + PacksText() + (_newCampaign ? " (nowa kampania)" : "")
                     + " | " + Recurring.StartText()
                     + " | dzien kampanii " + Day() + ", dzien opowiesci " + F1(sd) + (Prologue.BeforeStory() ? " (prolog - okna osi czekaja)" : "")
                     + " | do pokazania " + _ready.Count + " | nastepne okno " + (next == null ? "-" : next.Id + " (od " + next.Od + " = dzien opowiesci " + F1(Terms.DocToStory(next.Od)) + ")")
                     + " | potkniecia " + Stumbles + ".");
        }

        private static string PacksText()
        {
            if (_packsKnown.Count == 0) return "-";
            var l = new List<int>(_packsKnown.Keys);
            l.Sort();
            var sb = new StringBuilder();
            foreach (int n in l) { if (sb.Length > 0) sb.Append(','); sb.Append('k').Append(n.ToString("00", Inv)); }
            return sb.ToString();
        }

        private static string YN(bool b) { return b ? "TAK" : "NIE"; }

        private static IEnumerable<Data.RowDef> Rows(Data.WindowDef w)
        {
            yield return w.Main;
            foreach (var v in w.Variants) yield return v;
        }

        // ---------------------------------------------------------------- doba

        /// <summary>DailyTickEvent (ChronicleBehavior): pierwsze dni termow, akcje czekajace, okna powracajace, plan okien, linia doby.</summary>
        internal static void Daily()
        {
            int day = Day();
            try { Terms.Daily(); } catch (Exception e) { Stumble("Daily.Terms", e); }
            try { Actions.HiddenDaily(day, KeyOn); } catch (Exception e) { Stumble("Daily.Hidden", e); }   // A4: koniec ukrycia (klucz NIE - tylko porzadek rejestru)
            if (!KeyOn)
            {
                Log.Info(Tag + "dzien " + day + " | klucz Chronicle Of Westeros NIE - bez okien (stan zostaje) | potkniecia " + StumblesDay + " (razem " + Stumbles + ").");
                StumblesDay = 0; DayReset(); Actions.DayReset(); Recurring.DayReset();
                return;
            }
            try { Actions.Daily(day); } catch (Exception e) { Stumble("Daily.Actions", e); }
            try { Recurring.Daily(day); } catch (Exception e) { Stumble("Daily.Recurring", e); }
            try { Plan(day, true); } catch (Exception e) { Stumble("Daily.Plan", e); }
            try { DayLine(day); } catch (Exception e) { Stumble("Daily.Line", e); }
            StumblesDay = 0; DayReset(); Actions.DayReset(); Recurring.DayReset();
        }

        /// <summary>Plan okien: act = true (doba) - pomijanie, wiesci i rozstrzygniecia; act = false (start sesji) - tylko kolejka okien do pokazania.</summary>
        private static void Plan(int day, bool act)
        {
            _ready.RemoveAll(d => !d.Inst);
            _waitInfo.Clear(); _waitCount = 0;
            bool prolog = Prologue.BeforeStory();
            double sd = Terms.StoryDay();
            var list = Data.Windows;
            for (int i = 0; i < list.Count; i++)
            {
                var w = list[i];
                if (w.Recur != null || State.IsDone(w.Id)) continue;
                try { PlanOne(w, sd, prolog, act); }
                catch (Exception e) { Stumble("Plan(" + w.Id + ")", e); }
            }
            try { Recurring.Plan(day, act); } catch (Exception e) { Stumble("Plan.Recurring", e); }
        }

        private static void PlanOne(Data.WindowDef w, double sd, bool prolog, bool act)
        {
            // po i warunki CO DOBE (takze w prologu i przed od) - pamiec pierwszego spelnienia +N zapisuje sie w dniu, w ktorym term stal sie prawdziwy
            bool nvPo, nvW; string whyPo, whyW;
            bool po = Terms.Eval(w.Po, null, w.Id + " po", out nvPo, out whyPo);
            bool cond = Terms.Eval(w.Main.Cond, null, w.Id + " warunki", out nvW, out whyW);
            if (nvPo || nvW) { if (act) Skip(w.Id, "warunki swiata: " + (nvPo ? whyPo : whyW)); return; }
            if (prolog) return;   // okna osi nie ida w prologu (FORMAT 3.2 p. 1)
            bool ok = po && cond;
            string waitWhy = !po ? whyPo : whyW;
            if (w.Przed.Length > 0)
            {
                bool nvA; string whyA;
                if (Terms.Eval(w.Przed, null, w.Id + " przed", out nvA, out whyA))
                {
                    if (act) Fallback(w, ok, "kotwica " + w.Przed + " juz zaszla", AnchorName(w.Przed) + " juz ruszylo" + (ok ? "" : " (okno czekalo na: " + waitWhy + ")"));
                    return;
                }
            }
            if (w.Do >= 0)
            {
                double late = sd - Terms.DocToStory(w.Do);
                if (late > MaxLate)
                {
                    string t = "spoznione o " + F1(late) + " dni (do " + w.Do + " = dzien opowiesci " + F1(Terms.DocToStory(w.Do)) + ", limit " + MaxLate + ")";
                    if (act) Fallback(w, ok, t, t + (ok ? "" : " - okno czekalo na: " + waitWhy));
                    return;
                }
            }
            if (sd < Terms.DocToStory(w.Od)) return;   // przed od - jeszcze nie nalezne (KW-3)
            if (!ok) { NoteWait(w.Id, waitWhy); return; }
            string why;
            var p = PickRow(w, null, out why);
            if (p == null) { NoteWait(w.Id, why); return; }
            if (p.Form == "okno") { AddReady(w.Id, w, null, false); return; }
            if (act) Resolve(w.Id, w, p, p.Form, null, p.Player ? "forma " + p.Form : "gracz poza kolumna gracz: " + p.PlayerWhy);
        }

        private static void NoteWait(string id, string why)
        {
            _waitCount++;
            if (_waitInfo.Count < WaitShown) _waitInfo.Add(id + ": " + (why ?? "?"));
        }

        private static string AnchorName(string przed)
        {
            int c = przed.IndexOf(':');
            if (c < 0) return przed;
            string k = przed.Substring(0, c), a = przed.Substring(c + 1);
            return k == "kr" ? "okno " + a : a;
        }

        /// <summary>Okno, ktore juz sie nie pokaze (kotwica przed albo spoznienie): swiat sie zgadza i jest tekst _W - wiesc; gracz spoza kolumny gracz z inni=nic -
        /// rozstrzygniecie "nic" (skutki bez niczego dla gracza); inaczej POMINIETE z powodem.</summary>
        private static void Fallback(Data.WindowDef w, bool ok, string newsWhy, string skipWhy)
        {
            if (ok)
            {
                string why;
                var p = PickRow(w, null, out why);
                if (p != null)
                {
                    if (p.Form == "nic") { Resolve(w.Id, w, p, "nic", null, newsWhy + "; gracz poza kolumna gracz: " + p.PlayerWhy); return; }
                    if (Data.NewsRaw(p.Row) != null) { Resolve(w.Id, w, p, "wiesc", null, newsWhy); return; }
                }
            }
            Skip(w.Id, skipWhy);
        }

        /// <summary>Okno pominiete z powodem (POMINIETE, plan G) - kr: tego okna juz nigdy nie zajdzie.</summary>
        internal static void Skip(string id, string why)
        {
            if (State.IsDone(id)) return;
            State.Skip(id, Day(), why);
            _ready.RemoveAll(d => d.Id == id);
            _dSkipped++;
            Log.Info(Tag + "POMINIETE okno " + id + " - " + why + " (dzien opowiesci " + F1(Terms.StoryDay()) + ").");
        }

        private static void AddReady(string id, Data.WindowDef w, Terms.Tokens tok, bool inst)
        {
            foreach (var d in _ready) if (d.Id == id) return;
            _ready.Add(new Due { Id = id, W = w, Tok = tok, Inst = inst });
        }

        // ---------------------------------------------------------------- wybor wiersza i formy (FORMAT 3.1 wersja, 3.2 p. 2-3)

        /// <summary>Filtr wersji: ChronicleFullVersions NIE - lekka i obie; TAK - pelna i obie, lekka tylko w oknie bez wiersza pelna.</summary>
        private static bool VersionOk(string ver, bool hasFull)
        {
            if (!KeyFull) return ver == "lekka" || ver == "obie";
            return ver == "pelna" || ver == "obie" || (ver == "lekka" && !hasFull);
        }

        /// <summary>Wiersz do rozstrzygniecia: pierwszy wariant (kolejnosc pliku) z wersja i warunkami prawdziwymi i zmiennymi z wartoscia, inaczej glowny (zapas);
        /// forma: gracz spelnia kolumne gracz (pusta = spelnia) - forma, inaczej inni. null - brak wiersza (why).</summary>
        internal static RowPick PickRow(Data.WindowDef w, Terms.Tokens tok, out string why)
        {
            why = null;
            string firstWhy = null;
            foreach (var v in w.Variants)
            {
                if (!VersionOk(v.Version, w.HasFull)) continue;
                bool nv; string vw;
                if (!Terms.Eval(v.Cond, tok, v.Key + " warunki", out nv, out vw)) continue;
                var p = MakePick(v, tok, out vw);
                if (p != null) return p;
                if (firstWhy == null) firstWhy = v.Key + ": " + vw;
            }
            if (!VersionOk(w.Main.Version, w.HasFull)) { why = "brak wiersza dla wersji (" + (KeyFull ? "pelne" : "lekkie") + ")" + (firstWhy != null ? "; " + firstWhy : ""); return null; }
            string mw;
            var m = MakePick(w.Main, tok, out mw);
            if (m == null) why = mw + (firstWhy != null ? "; " + firstWhy : "");
            return m;
        }

        private static RowPick MakePick(Data.RowDef r, Terms.Tokens tok, out string why)
        {
            var vars = Vars(r, tok, out why);
            if (why != null) return null;
            bool nv; string pw;
            bool pl = Terms.Eval(r.Player, tok, r.Key + " gracz", out nv, out pw);
            return new RowPick { Row = r, Player = pl, Form = pl ? r.Form : r.Others, PlayerWhy = pl ? null : pw, Vars = vars };
        }

        /// <summary>Zmienne wiersza (NAZWA=@token): wartosci z gry; brak wartosci - why (wiersz nie bierze udzialu - tekst nie moze twierdzic czegos, czego nie ma).</summary>
        private static Dictionary<string, string> Vars(Data.RowDef r, Terms.Tokens tok, out string why)
        {
            why = null;
            if (r.Vars.Count == 0) return null;
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var v in r.Vars)
            {
                string val = null;
                try { val = TokenText(v.Token, tok); } catch (Exception e) { Stumble("Vars(" + r.Key + ")", e); }
                if (string.IsNullOrEmpty(val)) { why = "zmienna " + v.Name + "=" + v.Token + " bez wartosci"; return null; }
                d[v.Name] = val;
            }
            return d;
        }

        /// <summary>Tekst tokenu zmiennej (FORMAT 3.5): imie bohatera albo nazwa osady/rodu/krolestwa; @liczba i @pora - z instancji. null - brak.</summary>
        internal static string TokenText(string token, Terms.Tokens tok)
        {
            if (string.IsNullOrEmpty(token)) return null;
            switch (token)
            {
                case "@gracz": return Hero.MainHero != null ? Hero.MainHero.Name.ToString() : null;
                case "@liczba": return tok != null && tok.Liczba.Length > 0 ? tok.Liczba : null;
                case "@pora": return tok != null && tok.Pora.Length > 0 ? tok.Pora : null;
                case "@cel": { var s = Terms.FindSettlement("@cel", tok); return s != null ? s.Name.ToString() : null; }
                case "@kto": { var h = Terms.FindHero("@kto", tok); return h != null ? h.Name.ToString() : null; }
                case "@rodkto": { var c = Terms.FindClan("@rodkto", tok); return c != null ? c.Name.ToString() : null; }
                case "@region": { var k = Terms.FindKingdom("@region", tok); return k != null ? k.Name.ToString() : null; }
            }
            int i = token.IndexOf(':');
            if (i < 0) return null;
            string t = token.Substring(0, i), a = token.Substring(i + 1);
            if (t == "@imie") { var h = Terms.FindHero(a, tok); return h != null ? h.Name.ToString() : null; }
            if (t == "@nazwa")
            {
                var s = Terms.FindSettlement(a, tok); if (s != null) return s.Name.ToString();
                var c = Terms.FindClan(a, tok); if (c != null) return c.Name.ToString();
                var k = Terms.FindKingdom(a, tok); return k != null ? k.Name.ToString() : null;
            }
            var hh = Terms.FindHero(token, tok);   // @wladca:k, @glowa:rod, @pan:osada
            return hh != null ? hh.Name.ToString() : null;
        }

        /// <summary>Tekst komorki {=ARM_KR_...} ze zmiennymi wiersza (TextObject swiezy - Data.Tx).</summary>
        internal static string TextOf(string raw, Dictionary<string, string> vars)
        {
            var t = Data.Tx(raw);
            if (t == null) return "";
            if (vars != null) foreach (var kv in vars) t.SetTextVariable(kv.Key, kv.Value);
            return t.ToString();
        }

        /// <summary>Linia wiesci dla gracza (forma wiesc, akcja wiesc:) - wiadomosc w dzienniku gry.</summary>
        internal static void ShowNews(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            InformationManager.DisplayMessage(new InformationMessage(text, Color.FromUint(0xFFE8D9A8)));
        }

        // ---------------------------------------------------------------- rozstrzygniecie

        /// <summary>Rozstrzygniecie okna (raz): stan, linia formy (wiesc/log/nic - okno ma linie "pokazane"), skutki okna, potem wybor (lista po oknie albo opcja bierna).</summary>
        internal static void Resolve(string id, Data.WindowDef w, RowPick p, string form, Terms.Tokens tok, string why)
        {
            if (State.IsDone(id)) return;
            State.Resolve(id, Day(), form, p.Row.Key);
            _ready.RemoveAll(d => d.Id == id);
            string label = id + (p.Row.IsMain ? "" : " wariant " + p.Row.War);
            switch (form)
            {
                case "wiesc":
                    try { ShowNews(TextOf(Data.NewsRaw(p.Row), p.Vars)); } catch (Exception e) { Stumble("Resolve.News(" + id + ")", e); }
                    _dNews++;
                    Log.Info(Tag + "wiesc " + label + " (" + (why ?? "forma wiesc") + ") | dzien opowiesci " + F1(Terms.StoryDay()) + ".");
                    break;
                case "log":
                    _dLog++;
                    Log.Info(Tag + "log " + label + " (" + (why ?? "forma log") + ") - tylko linia logu, skutki ida | dzien opowiesci " + F1(Terms.StoryDay()) + ".");
                    break;
                case "nic":
                    _dNothing++;
                    Log.Info(Tag + "nic " + label + " (" + (why ?? "inni = nic") + ") - nic dla gracza, skutki ida | dzien opowiesci " + F1(Terms.StoryDay()) + ".");
                    break;
            }
            Actions.Run(new Actions.Ctx { Id = id, RowKey = p.Row.Key, Tok = tok, Vars = p.Vars }, p.Row.Effects);
            Choice.AfterResolve(id, p, form, tok);
            if (w != null && w.Recur != null) Recurring.OnResolved(w, id);
        }

        // ---------------------------------------------------------------- okna (SubModuleMain.OnApplicationTick)

        /// <summary>4 razy na sekunde: zamkniecie okna ROT (odstep), lista wyboru, nastepne okno Kroniki. Bez kampanii, bez danych albo przy kluczu NIE - od razu wyjscie.</summary>
        internal static void AppTick()
        {
            long t = Stopwatch.GetTimestamp();
            if (t - _lastCheck < CheckTicks) return;
            _lastCheck = t;
            try { Step(t); }
            catch (Exception e) { Stumble("AppTick", e); }
        }

        private static void Step(long t)
        {
            var c = Campaign.Current;
            if (c == null || !c.GameStarted || c.Models == null || !KeyOn) { _okSince = 0; return; }
            bool rot = StoryPictures.EventWindowOnStack();
            if (_rotWasOn && !rot) _rotClosedHour = HourNow();   // okno wydarzenia (ROT, scena prologu albo nasze) zeszlo ze stosu - odstep GapHours
            _rotWasOn = rot;
            if (_open != null && !StoryWindow.IsOpen) OnClosed(_open.Spec, "bez przycisku (straznik)");
            if (StoryWindow.IsOpen) { _okSince = 0; return; }
            if (Choice.ListOpen) { Choice.Watch(t); _okSince = 0; return; }
            if (_ready.Count == 0 && Choice.Count == 0) { _okSince = 0; return; }
            if (Choice.Count == 0 && TempoWhy() != null) { _okSince = 0; return; }   // limit na dobe / odstep - bez liczenia bramek co tick
            string why = Prologue.SceneGate(c);
            if (why != null) { _okSince = 0; return; }
            if (_okSince == 0) _okSince = t;
            double ok = (t - _okSince) / (double)Stopwatch.Frequency;
            if (Choice.Count > 0)
            {
                // lista wyboru: ChoiceDelay po zamknieciu naszego okna, inaczej (wczytanie zapisu ze stanem 'a') StableSec spokoju
                bool justClosed = _closedAt != 0 && (t - _closedAt) / (double)Stopwatch.Frequency < 5.0;
                if (ok < (justClosed ? ChoiceDelay : StableSec)) return;
                Choice.ShowNext(t);
                _okSince = 0;
                return;
            }
            if (ok < StableSec) return;
            _okSince = 0;
            OpenNext(t);
        }

        /// <summary>null = wolno pokazac okno; inaczej powod (limit na dobe, odstep).</summary>
        private static string TempoWhy()
        {
            int day = Day();
            if (_tempoDay == day && _tempoCount >= PerDay) return "limit " + PerDay + " okien na dobe";
            double h = HourNow();
            if (h - _lastWinHour < GapHours) return "odstep " + GapHours + " h od okna Kroniki";
            if (h - _rotClosedHour < GapHours) return "odstep " + GapHours + " h od zamkniecia okna wydarzenia";
            return null;
        }

        private static void OpenNext(long t)
        {
            _ready.Sort((a, b) => { int x = a.W.Od.CompareTo(b.W.Od); return x != 0 ? x : string.CompareOrdinal(a.Id, b.Id); });
            Terms.Invalidate();
            bool prolog = Prologue.BeforeStory();
            while (_ready.Count > 0)
            {
                var d = _ready[0];
                if (State.IsDone(d.Id)) { _ready.RemoveAt(0); continue; }
                string why;
                if (!StillDue(d, prolog, out why)) { _ready.RemoveAt(0); continue; }   // doba policzy je znowu
                var p = PickRow(d.W, d.Tok, out why);
                if (p == null) { _ready.RemoveAt(0); continue; }
                if (p.Form != "okno")
                {
                    // swiat zmienil sie od doby (np. gracz wszedl do armii) - forma bez okna, rozstrzygniecie od razu
                    _ready.RemoveAt(0);
                    Resolve(d.Id, d.W, p, p.Form, d.Tok, (p.Player ? "forma " + p.Form : "gracz poza kolumna gracz: " + p.PlayerWhy) + " (przy pokazaniu)");
                    return;
                }
                Show(d, p, t);
                return;
            }
        }

        /// <summary>Ponowna kontrola przy pokazaniu: zwykle okno - po i warunki prawdziwe, kotwica przed nie zaszla, nie prolog; instancja - warunki z tokenami, prolog tylko z opcja prolog.</summary>
        private static bool StillDue(Due d, bool prolog, out string why)
        {
            bool nv;
            why = null;
            var w = d.W;
            if (d.Inst)
            {
                if (prolog && !w.Recur.Prolog) { why = "prolog"; return false; }
                return Terms.Eval(w.Main.Cond, d.Tok, d.Id + " warunki", out nv, out why);
            }
            if (prolog) { why = "prolog"; return false; }
            if (!Terms.Eval(w.Po, null, w.Id + " po", out nv, out why)) return false;
            if (!Terms.Eval(w.Main.Cond, null, w.Id + " warunki", out nv, out why)) return false;
            if (w.Przed.Length > 0 && Terms.Eval(w.Przed)) { why = "kotwica przed"; return false; }
            return true;
        }

        /// <summary>Sprite ROT tematu wiersza - gdy Pictures.Pick nie da obrazka (jak Prologue.Scenes przy braku pliku): pierwszy dozwolony sprite tematu (bez poza
        /// rotacja, stannis-slain tylko 081, kolumna tylko), inaczej messenger-img (Pictures.Fallback).</summary>
        private static string DefaultSprite(Data.RowDef r)
        {
            try { return Pictures.Fallback(r); }
            catch (Exception e) { Stumble("DefaultSprite", e); return Pictures.Neutral; }
        }

        private static void Show(Due d, RowPick p, long t)
        {
            long t0 = Stopwatch.GetTimestamp();
            var r = p.Row;
            string how = "-";
            string header = null, prevPic = Pictures.Last;
            try { header = Pictures.Pick(d.W, r, d.Id, out how); } catch (Exception e) { Stumble("Pictures.Pick(" + d.Id + ")", e); header = null; }
            bool file = header != null && header.StartsWith(StoryPictures.SpritePrefix, StringComparison.Ordinal);
            if (header == null) { header = DefaultSprite(r); how = (how ?? "-") + " - sprite domyslny"; }
            // lektor (A3, P9): klucz nagrania i md5 tekstu okna - Lektor gra nagranie Kroniki tylko przy zgodnym md5 (zasada md5, Chronicle.Voice)
            string vmd5, vwhy = Voice.Why(r, out vmd5);
            var spec = new StoryWindow.Spec
            {
                Key = r.NarrationKey, VoiceMd5 = vmd5,
                Title = TextOf(r.Title, p.Vars), Upper = TextOf(r.Upper, p.Vars), Lower = TextOf(r.Lower, p.Vars),
                DoneText = PrologueScenes.Continue(), Header = header, Background = "event-bg", Tag = ++_openSeq, Closed = OnClosed
            };
            string mode = StoryWindow.Show(spec);
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            if (mode == null)
            {
                if (file) StoryPictures.Drop(header);
                Pictures.Restore(prevPic);
                if (_openFailId != d.Id) { _openFailId = d.Id; _openFails = 0; }
                _openFails++; _dFails++;
                Log.Info(Tag + "okno " + d.Id + " '" + spec.Title + "' - nie otworzylo sie (proba " + _openFails + "/" + OpenFailsMax + ").");
                if (_openFails >= OpenFailsMax) { _openFails = 0; _openFailId = null; Skip(d.Id, "okno nie otwiera sie (" + OpenFailsMax + " proby)"); }
                return;
            }
            _openFails = 0; _openFailId = null;
            _open = new OpenWin { Id = d.Id, W = d.W, P = p, Tok = d.Tok, Spec = spec, At = t, FilePic = file };
            int day = Day();
            if (_tempoDay != day) { _tempoDay = day; _tempoCount = 0; }
            _tempoCount++;
            _lastWinHour = HourNow();
            _dShown++;
            var w = d.W;
            Log.Info(Tag + "okno " + d.Id + " '" + spec.Title + "' pokazane | " + (r.IsMain ? "wiersz glowny" : "wariant " + r.War)
                     + " | dzien opowiesci " + F1(Terms.StoryDay()) + " (od " + w.Od + " = " + F1(Terms.DocToStory(w.Od))
                     + (w.Do >= 0 ? ", do " + w.Do + " = " + F1(Terms.DocToStory(w.Do)) : ", do -") + PlanText(w) + ")"
                     + " | obrazek " + (r.Topic.Length > 0 ? r.Topic : "-") + "->" + header + " (" + (file ? "plik" : "ROT") + "; " + how + ")"
                     + " | lektor " + (mode == "ROT" ? Voice.LineText(r, vwhy) : r.NarrationKey + " NIE (okno zapasu - lektor gra tylko w oknie ROT)")
                     + " | okno " + mode + " | gracz: " + Where() + (p.Player ? "" : " (poza kolumna gracz: " + p.PlayerWhy + ")")
                     + " | godzina kampanii " + F1(_lastWinHour) + " | dzis " + _tempoCount + "/" + PerDay + " | " + ms.ToString("0", Inv) + " ms.");   // A4: godzina - prog KW-4 (odstep 6 h)
            Resolve(d.Id, d.W, p, "okno", d.Tok, null);
        }

        private static string PlanText(Data.WindowDef w)
        {
            string c = w.Main.Cond;
            int i = c.IndexOf("plan:", StringComparison.Ordinal);
            if (i < 0) return "";
            int e = i + 5;
            while (e < c.Length && c[e] != ' ' && c[e] != '&' && c[e] != '|') e++;
            string term = c.Substring(i + 5, e - i - 5);
            int m = term.LastIndexOf('-');
            if (m <= 0) return "";
            double pd;
            return ", plan " + term + (Terms.PlanDay(term.Substring(0, m), out pd) ? " = " + F1(pd) : " (brak osi Fabuli)");
        }

        private static string Where()
        {
            try
            {
                var mp = MobileParty.MainParty;
                var ctx = Campaign.Current != null ? Campaign.Current.CurrentMenuContext : null;
                string menu = ctx != null && ctx.GameMenu != null ? ctx.GameMenu.StringId : "-";
                string place = mp != null && mp.CurrentSettlement != null ? mp.CurrentSettlement.StringId : "mapa";
                return place + ", menu " + menu + (Hero.MainHero != null && Hero.MainHero.IsPrisoner ? ", w niewoli" : "") + (mp != null && mp.Army != null ? ", w armii" : "");
            }
            catch { return "-"; }
        }

        /// <summary>Zamkniecie okna (przycisk, Esc, zapas, straznik). Lista wyboru tego okna - ChoiceDelay s pozniej (Step).</summary>
        private static void OnClosed(StoryWindow.Spec s, string how)
        {
            try
            {
                var o = _open;
                if (o == null || s == null || o.Spec.Tag != s.Tag) return;
                _open = null;
                _closedAt = Stopwatch.GetTimestamp();
                _okSince = 0;
                double sec = (_closedAt - o.At) / (double)Stopwatch.Frequency;
                Log.Info(Tag + "okno " + o.Id + " zamkniete po " + sec.ToString("0.0", Inv) + " s (" + how + ")" + (Choice.Waiting(o.Id) ? " - lista wyboru za " + ChoiceDelay + " s" : "") + ".");
                if (o.FilePic) StoryPictures.Drop(s.Header);
            }
            catch (Exception e) { Stumble("OnClosed", e); }
        }

        // ---------------------------------------------------------------- linia doby (plan G)

        private static void DayLine(int day)
        {
            var sb = new StringBuilder(400);
            sb.Append(Tag).Append("dzien ").Append(day).Append(" (opowiesci ").Append(F1(Terms.StoryDay())).Append(Prologue.BeforeStory() ? ", prolog" : "").Append(')');
            sb.Append(" | nalezne ").Append(_waitCount + _ready.Count);
            if (_ready.Count > 0) sb.Append(" (do pokazania ").Append(_ready.Count).Append(')');
            if (_waitInfo.Count > 0) sb.Append(" [").Append(string.Join("; ", _waitInfo.ToArray())).Append(_waitCount > _waitInfo.Count ? "; ..." : "").Append(']');
            sb.Append(" | dzis okna ").Append(_dShown).Append(", wiesci ").Append(_dNews).Append(", log ").Append(_dLog).Append(", nic ").Append(_dNothing)
              .Append(", pominiete ").Append(_dSkipped);
            if (_dFails > 0) sb.Append(", nieotwarte ").Append(_dFails);
            sb.Append(" | ").Append(Actions.DayText());
            sb.Append(" | wybory: czeka ").Append(Choice.Count).Append(", ").Append(Choice.DayText());
            sb.Append(" | ").Append(Recurring.DayText());
            sb.Append(" | bandy ").Append(Actions.Bands).Append("/12 | ukryci ").Append(Actions.HiddenText());
            sb.Append(" | ").Append(Pictures.DayText());
            sb.Append(" | stan: pokazane/wiesci ").Append(State.Resolved).Append(", pominiete ").Append(State.Skipped).Append(", flagi ").Append(State.Flags);
            int bytes = _saveBytes;
            try { bytes = Encoding.UTF8.GetByteCount(Build()); } catch (Exception e) { Stumble("DayLine.Size", e); }
            sb.Append(" | zapis ").Append((bytes / 1024.0).ToString("0.0", Inv)).Append(" KB").Append(bytes > SaveWarnBytes ? " (PONAD " + (SaveWarnBytes / 1024) + " KB)" : "");
            sb.Append(" | nieznane termy dzis ").Append(Terms.UnknownDay);
            sb.Append(" | potkniecia ").Append(StumblesDay).Append(" (razem ").Append(Stumbles).Append(").");
            Log.Info(sb.ToString());
        }

        // ---------------------------------------------------------------- zapis (ChronicleBehavior.SyncData)

        /// <summary>Zapis gry (SyncData): caly stan Kroniki + linia "zapis arm_kronika n KB" (prog KW-12: ponizej 24 KB).</summary>
        internal static string Export()
        {
            string s = Build();
            _saveBytes = Encoding.UTF8.GetByteCount(s);
            Log.Info(Tag + "zapis arm_kronika " + (_saveBytes / 1024.0).ToString("0.0", Inv) + " KB" + (_saveBytes > SaveWarnBytes ? " - PONAD " + (SaveWarnBytes / 1024) + " KB (prog KW-12)" : "")
                     + " | rozstrzygniete " + State.Resolved + ", pominiete " + State.Skipped + ", czeka na wybor " + Choice.Count + ", akcje czekaja " + Actions.WaitCount
                     + ", instancje w kolejce " + Recurring.QueueCount + ", pamiec termow " + Terms.FirstCount + ".");
            return s;
        }

        /// <summary>Caly stan Kroniki jako linie z prefiksami (State kr/ks/kc/kf, Terms tf, silnik kv/kp/kn, Choice ka, Actions kw, Recurring kq/kl/kt/ko/kh).</summary>
        private static string Build()
        {
            var sb = new StringBuilder(4096);
            sb.Append("kv\t1\n");
            sb.Append(State.Export());
            sb.Append(Terms.Export());
            foreach (var kv in _packsKnown) sb.Append("kp\t").Append(kv.Key.ToString(Inv)).Append('\t').Append(kv.Value.ToString(Inv)).Append('\n');
            sb.Append("kn\t").Append(_tempoDay.ToString(Inv)).Append('\t').Append(_tempoCount.ToString(Inv)).Append('\t').Append(_lastWinHour.ToString("0.00", Inv)).Append('\n');
            sb.Append(Choice.Export());
            sb.Append(Actions.Export());
            sb.Append(Recurring.Export());
            return sb.ToString();
        }

        /// <summary>Wczytanie: wszystko od zera, potem linie swoich prefiksow. Pusty napis (zapis sprzed Kroniki) - stan pusty.</summary>
        internal static void Import(string s)
        {
            ResetCampaign();
            if (string.IsNullOrEmpty(s)) return;
            _saveBytes = Encoding.UTF8.GetByteCount(s);
            State.Import(s);
            Terms.Import(s);
            foreach (var raw in s.Split('\n'))
            {
                var p = raw.TrimEnd('\r').Split('\t');
                int a, b; double h;
                if (p[0] == "kp" && p.Length >= 3 && int.TryParse(p[1], NumberStyles.Integer, Inv, out a) && int.TryParse(p[2], NumberStyles.AllowLeadingSign, Inv, out b)) _packsKnown[a] = b;
                else if (p[0] == "kn" && p.Length >= 4 && int.TryParse(p[1], NumberStyles.AllowLeadingSign, Inv, out a) && int.TryParse(p[2], NumberStyles.Integer, Inv, out b)
                         && double.TryParse(p[3], NumberStyles.Float, Inv, out h)) { _tempoDay = a; _tempoCount = b; _lastWinHour = h; }
            }
            Choice.Import(s);
            Actions.Import(s);
            Recurring.Import(s);
        }

        // ---------------------------------------------------------------- tokeny w zapisie
        /// <summary>Tokeny instancji do zapisu: 6 pol rozdzielonych '|' (cel|kto|rodkto|region|liczba|pora); null - "-".</summary>
        internal static string TokText(Terms.Tokens t)
        {
            if (t == null) return "-";
            return Tk(t.Cel) + "|" + Tk(t.Kto) + "|" + Tk(t.Rodkto) + "|" + Tk(t.Region) + "|" + Tk(t.Liczba) + "|" + Tk(t.Pora);
        }

        private static string Tk(string s) { return string.IsNullOrEmpty(s) ? "" : s.Replace('|', '_').Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' '); }

        internal static Terms.Tokens TokParse(string s)
        {
            if (string.IsNullOrEmpty(s) || s == "-") return null;
            var p = s.Split('|');
            if (p.Length < 6) return null;
            return new Terms.Tokens { Cel = p[0], Kto = p[1], Rodkto = p[2], Region = p[3], Liczba = p[4], Pora = p[5] };
        }
    }

    /// <summary>m105-kronika K0: zachowanie kampanii Kroniki Westeros - start sesji, doba, zdarzenia okien powracajacych, zapis (arm_kronika).
    /// Rejestrowane PO CanonCompanionsBehavior (doba Kroniki po dobie postaci kanonu - mv: i kr: w tej samej kolejnosci co dzien).</summary>
    internal sealed class ChronicleBehavior : CampaignBehaviorBase
    {
        public ChronicleBehavior() { Chronicle.ResetCampaign(); }

        public override void RegisterEvents()
        {
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, _ => Chronicle.OnNewGame());
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => Chronicle.SessionStart());
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, Chronicle.Daily);
            CampaignEvents.BeforeHeroKilledEvent.AddNonSerializedListener(this, Chronicle.Recurring.OnBeforeHeroKilled);   // oldage
            CampaignEvents.RaidCompletedEvent.AddNonSerializedListener(this, Chronicle.Recurring.OnRaidCompleted);         // ironraid
        }

        public override void SyncData(IDataStore dataStore)
        {
            string v = null;
            try { if (dataStore.IsSaving) v = Chronicle.Export(); }
            catch (Exception e) { Chronicle.Stumble("Export", e); v = ""; }
            SaveText.Sync(dataStore, "arm_kronika", ref v);
            if (dataStore.IsLoading)
            {
                try { Chronicle.Import(v); }
                catch (Exception e) { Chronicle.Stumble("Import", e); }
            }
        }
    }
}
