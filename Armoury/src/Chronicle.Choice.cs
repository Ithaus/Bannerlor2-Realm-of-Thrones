using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    internal static partial class Chronicle
    {
        /// <summary>
        /// m105-kronika K0 (krok A2) - WYBOR GRACZA PO OKNIE (KRONIKA-FORMAT 4; plan E "Chronicle.Choice.cs"). Okno ROT ma Done/Cancel, a Esc zawsze wola Cancel
        /// (dekompilacja EventScreen.OnFrameTick) - wybor NIGDY nie lezy na przyciskach okna: po zamknieciu okna, nad mapa, lista MBInformationManager.ShowMultiSelectionInquiry
        /// (radio max 1, bez wyjscia - isExitShown false, Esc nic nie robi; opis z id okna - ten sam tekst dwa razy daje FailedAssert duplikatu).
        /// Kiedy lista: wiersz ma opcje, forma okno, gracz spelnia kolumne gracz, klucz ChroniclePlayerChoices TAK i co najmniej jedna opcja dostepna. Inaczej opcja
        /// BIERNA (ostatnia, wersja z ksiazki) od razu, bez pytania. Lista czeka w stanie 'a' (zapis "ka") - po wczytaniu wraca. Bramka: Prologue.SceneGate (bez innego
        /// zapytania i bez okna ROT na stosie) i ChoiceDelay s po zamknieciu okna (po wczytaniu StableSec). Autotest: plik CrashScribe/armoury-kronika-wybor.flag
        /// (a|b|c|d, tryb LogGate 1/2) wymusza opcje, gdy jest dostepna - lista i tak sie pokazuje (para z linia AUTOTEST "OKNO wybor z listy").
        /// Skutki okna sa juz wykonane (przed lista); skutki opcji - po wyborze (Actions.Run). Linia "wybor NNN.w = x 'etykieta' (z n; dostepne abc; wymuszone flaga
        /// TAK/NIE; bierna TAK/NIE)". Lista zniknieta bez wyboru (cudzy kod schowal zapytanie) - pokazana ponownie (licznik).
        /// </summary>
        internal static class Choice
        {
            internal const string ForceFlagName = "armoury-kronika-wybor.flag";
            internal const string ConfirmText = "{=ARM_KR_UI_CONFIRM}Confirm";

            private sealed class Pend
            {
                public string Id = "", RowKey = "";
                public int Day;
                public Terms.Tokens Tok;
                public int Lost;   // recenzja K0 (R9): ile razy lista zniknela bez wyboru (sesja) - po LostMax opcja bierna
            }

            private const int LostMax = 5;

            private static readonly List<Pend> _pend = new List<Pend>();
            private static Pend _listFor;
            private static long _listAt;
            private static int _lost, _dLists, _dPassive, _dChosen, _dForced;

            internal static int Count { get { return _pend.Count; } }
            internal static bool ListOpen { get { return _listFor != null; } }

            internal static void Reset()
            {
                _pend.Clear();
                _listFor = null; _listAt = 0; _lost = 0;
                DayReset();
            }

            private static void DayReset() { _dLists = _dPassive = _dChosen = _dForced = 0; }

            internal static string DayText()
            {
                string s = "dzis listy " + _dLists + ", wybrane " + _dChosen + " (wymuszone flaga " + _dForced + "), bierne bez pytania " + _dPassive + (_lost > 0 ? ", listy znikniete " + _lost : "");
                DayReset();
                return s;
            }

            internal static bool Waiting(string id)
            {
                foreach (var p in _pend) if (p.Id == id) return true;
                return false;
            }

            /// <summary>Po rozstrzygnieciu okna (skutki okna juz wykonane): lista (stan 'a') albo opcja bierna od razu.</summary>
            internal static void AfterResolve(string id, RowPick p, string form, Terms.Tokens tok)
            {
                var row = p.Row;
                if (row.Options.Count == 0) return;
                string avail;
                int n = Available(row, tok, id, out avail);
                string why = null;
                if (form != "okno") why = "forma " + form;
                else if (!p.Player) why = "gracz poza kolumna gracz";
                else if (!KeyChoices) why = "klucz Chronicle Player Choices NIE";
                else if (n == 0) why = "0 dostepnych opcji";
                if (why != null) { Passive(id, row, tok, p.Vars, avail, why); return; }
                if (Waiting(id)) return;
                _pend.Add(new Pend { Id = id, RowKey = row.Key, Day = Terms.Day(), Tok = tok });
                Log.Info(Tag + "wybor " + id + " czeka na liste po oknie (opcji " + row.Options.Count + ", dostepne " + avail + ").");
            }

            /// <summary>Dostepne opcje (kolumna dostepna): liczba i litery (np. "ac").</summary>
            private static int Available(Data.RowDef row, Terms.Tokens tok, string id, out string letters)
            {
                var sb = new StringBuilder(4);
                foreach (var o in row.Options)
                {
                    bool nv; string w;
                    if (Terms.Eval(o.Avail, tok, id + " dostepna " + o.Letter, out nv, out w)) sb.Append(o.Letter);
                }
                letters = sb.Length > 0 ? sb.ToString() : "-";
                return sb.Length;
            }

            private static Data.OptionDef PassiveOpt(Data.RowDef row)
            {
                foreach (var o in row.Options) if (o.Passive) return o;
                return row.Options.Count > 0 ? row.Options[row.Options.Count - 1] : null;
            }

            private static void Passive(string id, Data.RowDef row, Terms.Tokens tok, Dictionary<string, string> vars, string avail, string why)
            {
                var o = PassiveOpt(row);
                if (o == null) return;
                _dPassive++;
                Chosen(id, row, o, tok, vars, avail, false, true, why);
            }

            private static void Chosen(string id, Data.RowDef row, Data.OptionDef o, Terms.Tokens tok, Dictionary<string, string> vars, string avail, bool forced, bool passive, string why)
            {
                State.Choose(id, o.Letter[0]);
                string label = "";
                try { label = Data.Plain(o.Label); } catch { }
                Log.Info(Tag + "wybor " + id + " = " + o.Letter + " '" + label + "' (z " + row.Options.Count + "; dostepne " + avail + "; wymuszone flaga " + (forced ? "TAK" : "NIE")
                         + "; bierna " + (o.Passive ? "TAK" : "NIE") + (passive ? " - bez pytania: " + why : "") + ").");
                Actions.Run(new Actions.Ctx { Id = id, RowKey = row.Key, Opt = o.Letter[0], Tok = tok, Vars = vars }, o.Effects);
            }

            /// <summary>AppTick (bramki spelnione): lista pierwszego czekajacego wyboru. Wiersz zniknal z danych (paczka wylaczona) - wybor porzucony z linia.</summary>
            internal static void ShowNext(long t)
            {
                if (_pend.Count == 0 || _listFor != null) return;
                var pd = _pend[0];
                if (State.Choice(pd.Id) != '\0') { _pend.RemoveAt(0); return; }
                var row = Data.Row(pd.RowKey);
                if (row == null || row.Options.Count == 0)
                {
                    _pend.RemoveAt(0);
                    Log.Info(Tag + "wybor " + pd.Id + " - wiersza " + pd.RowKey + " nie ma w danych (paczka wylaczona albo zmieniona) - lista porzucona, bez skutkow opcji.");
                    return;
                }
                string avail;
                int n = Available(row, pd.Tok, pd.Id, out avail);
                if (!KeyChoices || n == 0)
                {
                    _pend.RemoveAt(0);
                    Passive(pd.Id, row, pd.Tok, VarsOrNull(row, pd.Tok), avail, !KeyChoices ? "klucz Chronicle Player Choices NIE" : "0 dostepnych opcji");
                    return;
                }
                var vars = VarsOrNull(row, pd.Tok);
                var elems = new List<InquiryElement>(row.Options.Count);
                foreach (var o in row.Options)
                {
                    bool on = avail.IndexOf(o.Letter[0]) >= 0;
                    elems.Add(new InquiryElement(o.Letter, TextOf(o.Label, null), null, on, TextOf(o.Hint, null)));
                }
                string title = TextOf(row.Title, vars);
                string desc = TextOf(row.Question, vars) + " (" + pd.Id + ")";
                var me = pd;
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(title, desc, elems, false, 1, 1, new TextObject(ConfirmText).ToString(), "",
                    sel => OnAffirm(me, sel), null), true);
                _listFor = pd; _listAt = t;
                _dLists++;
                Log.Info(Tag + "lista wyboru " + pd.Id + " pokazana (opcji " + row.Options.Count + ", dostepne " + avail + ", czekala od dnia " + pd.Day + ").");
            }

            private static Dictionary<string, string> VarsOrNull(Data.RowDef row, Terms.Tokens tok)
            {
                string why;
                var v = Vars(row, tok, out why);
                return why == null ? v : null;
            }

            /// <summary>AppTick przy otwartej liscie: lista zniknela bez wyboru (zadnego zapytania przez 1 s) - zostaje w stanie 'a' i pokaze sie znowu.
            /// Recenzja K0 (R9): ta sama lista zniknela LostMax razy (cudzy kod chowa zapytania) - opcja bierna bez pytania, inaczej petla co ok. 3 s z 2 liniami logu
            /// i stoja inne okna Kroniki (Choice.Count > 0).</summary>
            internal static void Watch(long t)
            {
                if (_listFor == null) return;
                if (InformationManager.IsAnyInquiryActive()) return;
                if ((t - _listAt) / (double)Stopwatch.Frequency < 1.0) return;
                _lost++;
                var pd = _listFor;
                _listFor = null;
                pd.Lost++;
                if (pd.Lost < LostMax)
                {
                    Log.Info(Tag + "lista wyboru " + pd.Id + " zniknela bez wyboru (proba " + pd.Lost + "/" + LostMax + ", w sesji " + _lost + ") - pokaze sie znowu.");
                    return;
                }
                Log.Info(Tag + "lista wyboru " + pd.Id + " zniknela bez wyboru (proba " + pd.Lost + "/" + LostMax + ", w sesji " + _lost + ") - opcja bierna bez pytania.");
                _pend.Remove(pd);
                if (State.Choice(pd.Id) != '\0') return;
                var row = Data.Row(pd.RowKey);
                if (row == null || row.Options.Count == 0) return;
                string avail;
                Available(row, pd.Tok, pd.Id, out avail);
                Passive(pd.Id, row, pd.Tok, VarsOrNull(row, pd.Tok), avail, "lista znikala " + LostMax + " razy");
            }

            /// <summary>Potwierdzenie listy: wybrana litera (albo wymuszona plikiem flagi, gdy dostepna), stan, linia, skutki opcji.</summary>
            private static void OnAffirm(Pend pd, List<InquiryElement> sel)
            {
                try
                {
                    if (ReferenceEquals(_listFor, pd)) _listFor = null;
                    _pend.Remove(pd);
                    if (State.Choice(pd.Id) != '\0') return;   // juz wybrane (druga lista) - nic
                    var row = Data.Row(pd.RowKey);
                    if (row == null || row.Options.Count == 0) return;
                    string avail;
                    Available(row, pd.Tok, pd.Id, out avail);
                    string letter = sel != null && sel.Count > 0 && sel[0] != null ? sel[0].Identifier as string : null;
                    bool forced = false;
                    string f = ForcedLetter();
                    if (f != null && avail.IndexOf(f[0]) >= 0 && f != letter) { letter = f; forced = true; }
                    else if (f != null && f == letter) forced = true;
                    Data.OptionDef o = null;
                    foreach (var x in row.Options) if (x.Letter == letter) o = x;
                    if (o == null) { o = PassiveOpt(row); if (o == null) return; }
                    _dChosen++;
                    if (forced) _dForced++;
                    Chosen(pd.Id, row, o, pd.Tok, VarsOrNull(row, pd.Tok), avail, forced, false, null);
                }
                catch (Exception e) { Stumble("Choice.OnAffirm(" + pd.Id + ")", e); }
            }

            /// <summary>Plik flagi autotestu (tylko LogGate 1/2): pierwsza litera a-d albo null.</summary>
            private static string ForcedLetter()
            {
                int m = LogGate.Mode;
                if (m != 1 && m != 2) return null;
                try
                {
                    var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    string p = System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe"), ForceFlagName);
                    if (!System.IO.File.Exists(p)) return null;
                    string s = System.IO.File.ReadAllText(p).Trim().ToLowerInvariant();
                    return s.Length > 0 && s[0] >= 'a' && s[0] <= 'd' ? s.Substring(0, 1) : null;
                }
                catch { return null; }
            }

            /// <summary>Linie zapisu "ka id wiersz dzien tokeny" (stan 'a' - czeka na wybor).</summary>
            internal static string Export()
            {
                var sb = new StringBuilder(64 * _pend.Count);
                foreach (var p in _pend)
                    sb.Append("ka\t").Append(p.Id).Append('\t').Append(p.RowKey).Append('\t').Append(p.Day.ToString(Inv)).Append('\t').Append(TokText(p.Tok)).Append('\n');
                return sb.ToString();
            }

            internal static void Import(string s)
            {
                _pend.Clear(); _listFor = null;
                if (string.IsNullOrEmpty(s)) return;
                foreach (var raw in s.Split('\n'))
                {
                    var p = raw.TrimEnd('\r').Split('\t');
                    int d;
                    if (p[0] != "ka" || p.Length < 4 || p[1].Length == 0 || !int.TryParse(p[3], System.Globalization.NumberStyles.AllowLeadingSign, Inv, out d)) continue;
                    if (State.Choice(p[1]) != '\0') continue;
                    _pend.Add(new Pend { Id = p[1], RowKey = p[2], Day = d, Tok = p.Length >= 5 ? TokParse(p[4]) : null });
                }
            }
        }
    }
}
