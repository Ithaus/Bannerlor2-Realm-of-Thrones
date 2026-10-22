using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    internal static partial class Chronicle
    {
        internal static partial class Actions
        {
            // ================================================================ m105-kronika K0 krok B2 - ZADANIA KRONIKI (KRONIKA-FORMAT 7, plan J K2 "Chronicle.Tasks.cs")
            // task:tNNN - zadanie z sekcji #zadanie we WLASNYM rejestrze (bez QuestBase gry - zadnych typow Kroniki w zapisie gry; zapis "kz" w arm_kronika).
            // Rodzaje: deliver (dowiez ilosc sztuk towaru co do osady cel - towar schodzi z ekwipunku druzyny gracza na rynek osady, nic nie znika), defeat (rozbij bande
            // Kroniki co = bNNN - zywa na mapie w chwili zadania; rozbicie przez gracza - Chronicle.Bands.OnMapEventEnded), prisoners (dostarcz ilosc jencow - nie
            // bohaterow, nie umarlych - z lochu druzyny gracza do lochu osady cel; ksiega ludzi PeopleBook.Transfer jak kazde przeniesienie). Wykonanie: gracz w osadzie
            // cel (SettlementEntered druzyny gracza albo doba, gdy w niej stoi) - nagroda = skutki z kolumny nagroda zwykla droga akcji (kontekst tNNN; zloto zawsze
            // z platnikiem). Termin: dni dni od przyjecia (dni kampanii) - po nim zadanie NIEUDANE z linia i wiadomoscia. Banda zadania defeat zniknela bez gracza
            // (rozbita przez innych, koniec terminu) - NIEUDANE. Tablica ogloszen (NoticeBoard): wpisy tablicy to dzis zadania gry (QuestBase) i listy goncze; nowy
            // rodzaj wpisu wymagalby zmian w karcie, warunkach i przyciskach tablicy - ryzyko bez potrzeby, wiec zadanie Kroniki idzie WIADOMOSCIA dla gracza
            // (tytul, opis, gdzie, termin) i linia logu; banda zadania defeat i tak ma list gonczy tablicy, gdy kolumna list = 1.
            // Najwyzej MaxTasks zadan naraz; to samo zadanie drugi raz w trakcie - bez zmian (OK "juz trwa").

            private const int MaxTasks = 8;

            private sealed class TaskRow
            {
                public string Id = "", Src = "", Target = "";
                public int Day, Due;
                public bool Told;                 // sesja: przypomnienie o brakach przy wejsciu do osady (raz)
            }

            private static readonly List<TaskRow> _tasks = new List<TaskRow>();

            internal static int TaskCount { get { return _tasks.Count; } }

            private static void TasksReset() { _tasks.Clear(); }

            private static string TaskTitle(Data.TaskDef def)
            {
                string t = def != null ? TextOf(def.Title, null) : "";
                return string.IsNullOrEmpty(t) && def != null ? Data.Plain(def.Title) : t;
            }

            private static string PlaceName(string id)
            {
                var st = Terms.FindSettlement(id, null);
                return st != null && st.Name != null ? st.Name.ToString() : id;
            }

            /// <summary>task:&lt;tNNN&gt; - zadanie z #zadanie (opis bloku wyzej). Linia "zadanie tNNN 'tytul' przyjete | rodzaj | cel | co | termin".</summary>
            private static Res Task(Ctx c, List<string> a, out string detail)
            {
                detail = null;
                if (a.Count != 1 || a[0].Length == 0) { detail = "task:<tNNN> - zle argumenty"; return Res.Skip; }
                var def = Data.Task(a[0]);
                if (def == null) { detail = "brak zadania " + a[0] + " w #zadanie (paczka wylaczona albo zly wiersz)"; return Res.Skip; }
                foreach (var r in _tasks) if (r.Id == def.Id) { detail = "zadanie " + def.Id + " juz trwa (od dnia " + r.Day + ", do dnia " + r.Due + ") - bez zmian"; return Res.Done; }
                if (_tasks.Count >= MaxTasks) { detail = "najwyzej " + MaxTasks + " zadan Kroniki naraz"; return Res.Skip; }
                if (Hero.MainHero == null || MobileParty.MainParty == null) { detail = "brak gracza"; return Res.Skip; }
                var st = Terms.FindSettlement(def.Target, c.Tok);
                if (st == null) { detail = "osada " + def.Target + " nie istnieje w grze"; return Res.Skip; }
                string what;
                switch (def.Kind)
                {
                    case "deliver":
                        {
                            var item = ItemById(def.What);
                            if (item == null) { detail = "nieznany towar " + def.What; return Res.Skip; }
                            if (st.ItemRoster == null) { detail = "osada " + st.StringId + " bez rynku"; return Res.Skip; }
                            what = def.Amount + " x " + item.StringId;
                            break;
                        }
                    case "defeat":
                        {
                            var mp = BandLive(def.What);
                            if (mp == null) { detail = "banda " + def.What + " nie jest na mapie (band: przed task: w skutkach albo banda nie powstala)"; return Res.Skip; }
                            what = "banda " + def.What + " (" + mp.StringId + ")";
                            break;
                        }
                    case "prisoners":
                        if (st.Party == null || st.Party.PrisonRoster == null) { detail = "osada " + st.StringId + " bez lochu"; return Res.Skip; }
                        what = def.Amount + " jencow";
                        break;
                    default:
                        detail = "nieznany rodzaj zadania " + def.Kind;
                        return Res.Skip;
                }
                int day = Terms.Day();
                var row = new TaskRow { Id = def.Id, Src = c.Label, Target = st.StringId, Day = day, Due = day + def.Days };
                _tasks.Add(row);
                string title = TaskTitle(def), desc = TextOf(def.Desc, null);
                try
                {
                    Log.Player("New task: " + title + ". " + desc + " (" + (st.Name != null ? st.Name.ToString() : st.StringId) + ", " + def.Days + " days)");
                }
                catch (Exception e) { Stumble("Tasks.Tell", e); }
                Log.Info(Tag + "zadanie " + def.Id + " '" + title + "' przyjete | rodzaj " + def.Kind + " | cel " + st.StringId + " | " + what + " | termin dzien " + row.Due + " (" + def.Days
                         + " dni) | nagroda " + def.Reward.Length + " akcji | wiadomosc dla gracza (bez wpisu tablicy ogloszen) | " + c.Label + ".");
                detail = def.Kind + " " + what + " -> " + st.StringId + ", do dnia " + row.Due;
                return Res.Done;
            }

            /// <summary>Doba (WorldDaily): termin minal - NIEUDANE; gracz stoi w osadzie celu - proba wykonania (deliver, prisoners).</summary>
            private static void TasksDaily(int day)
            {
                if (_tasks.Count == 0) return;
                for (int i = 0; i < _tasks.Count; i++)
                {
                    var r = _tasks[i];
                    try
                    {
                        var def = Data.Task(r.Id);
                        if (def == null) { _tasks.RemoveAt(i); i--; Log.Info(Tag + "zadanie " + r.Id + " - brak definicji w danych (paczka wylaczona) - wpis usuniety."); continue; }
                        if (day > r.Due) { _tasks.RemoveAt(i); i--; TaskFail(r, def, "termin minal (dzien " + r.Due + ")"); continue; }
                        var main = MobileParty.MainParty;
                        if (main != null && main.CurrentSettlement != null && main.CurrentSettlement.StringId == r.Target && TryComplete(r, def, false)) { i--; }
                    }
                    catch (Exception e) { Stumble("Tasks.Daily(" + r.Id + ")", e); }
                }
            }

            /// <summary>CampaignEvents.SettlementEntered: druzyna gracza w osadzie celu zadania - proba wykonania (deliver, prisoners). Bez zadan - od razu wyjscie.</summary>
            internal static void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
            {
                if (_tasks.Count == 0 || party == null || settlement == null || party != MobileParty.MainParty) return;
                try
                {
                    for (int i = 0; i < _tasks.Count; i++)
                    {
                        var r = _tasks[i];
                        if (r.Target != settlement.StringId) continue;
                        var def = Data.Task(r.Id);
                        if (def != null && TryComplete(r, def, true)) i--;
                    }
                }
                catch (Exception e) { Stumble("Tasks.OnSettlementEntered", e); }
            }

            /// <summary>Wykonanie deliver/prisoners (gracz w osadzie celu): true - zadanie zdjete z rejestru (wykonane). Brak towaru/jencow - przypomnienie przy wejsciu (raz).</summary>
            private static bool TryComplete(TaskRow r, Data.TaskDef def, bool entered)
            {
                var main = MobileParty.MainParty;
                var st = Terms.FindSettlement(r.Target, null);
                if (main == null || st == null) return false;
                string done = null;
                if (def.Kind == "deliver")
                {
                    var item = ItemById(def.What);
                    if (item == null || main.ItemRoster == null || st.ItemRoster == null) return false;
                    int have = CountIn(main.ItemRoster, item);
                    if (have < def.Amount)
                    {
                        if (entered && !r.Told) { r.Told = true; Log.Player(TaskTitle(def) + ": you carry " + have + " of " + def.Amount + " " + (item.Name != null ? item.Name.ToString() : item.StringId) + "."); }
                        return false;
                    }
                    int left = def.Amount, s0 = CountIn(st.ItemRoster, item);
                    var parts = new List<KeyValuePair<EquipmentElement, int>>();
                    for (int i = 0; i < main.ItemRoster.Count && left > 0; i++)
                    {
                        var el = main.ItemRoster.GetElementCopyAtIndex(i);
                        if (el.EquipmentElement.Item != item || el.Amount <= 0) continue;
                        int t = Math.Min(el.Amount, left);
                        parts.Add(new KeyValuePair<EquipmentElement, int>(el.EquipmentElement, t));
                        left -= t;
                    }
                    foreach (var p in parts) { main.ItemRoster.AddToCounts(p.Key, -p.Value); st.ItemRoster.AddToCounts(p.Key, p.Value); }
                    done = "towar " + item.StringId + ": druzyna gracza " + have + " -> " + CountIn(main.ItemRoster, item) + ", rynek " + st.StringId + " " + s0 + " -> " + CountIn(st.ItemRoster, item);
                }
                else if (def.Kind == "prisoners")
                {
                    var pr = main.PrisonRoster;
                    var to = st.Party != null ? st.Party.PrisonRoster : null;
                    if (pr == null || to == null) return false;
                    int have = 0;
                    for (int i = 0; i < pr.Count; i++)
                    {
                        var el = pr.GetElementCopyAtIndex(i);
                        if (el.Character == null || el.Character.IsHero || PeopleBook.IsWight(el.Character)) continue;
                        have += el.Number;
                    }
                    if (have < def.Amount)
                    {
                        if (entered && !r.Told) { r.Told = true; Log.Player(TaskTitle(def) + ": you bring " + have + " of " + def.Amount + " prisoners."); }
                        return false;
                    }
                    int p0 = PeopleBook.Men(pr), d0 = PeopleBook.Men(to), left = def.Amount;
                    var take = new List<KeyValuePair<CharacterObject, int>>();
                    for (int i = 0; i < pr.Count && left > 0; i++)
                    {
                        var el = pr.GetElementCopyAtIndex(i);
                        if (el.Character == null || el.Character.IsHero || PeopleBook.IsWight(el.Character) || el.Number <= 0) continue;
                        int t = Math.Min(el.Number, left);
                        take.Add(new KeyValuePair<CharacterObject, int>(el.Character, t));
                        left -= t;
                    }
                    foreach (var t in take) { pr.AddToCounts(t.Key, -t.Value); to.AddToCounts(t.Key, t.Value); }
                    double moved = 0.0;
                    try { moved = PeopleBook.Transfer(pr, p0, to, d0, PeopleBook.FTransfer); } catch (Exception e) { Stumble("Tasks.Prisoners(ksiega)", e); }
                    done = "jency: loch gracza " + p0 + " -> " + PeopleBook.Men(pr) + ", loch " + st.StringId + " " + d0 + " -> " + PeopleBook.Men(to) + " (ksiega ludzi " + moved.ToString("0", Inv) + ")";
                }
                else return false;
                _tasks.Remove(r);
                TaskDone(r, def, done);
                return true;
            }

            private static void TaskDone(TaskRow r, Data.TaskDef def, string how)
            {
                string title = TaskTitle(def);
                Log.Info(Tag + "zadanie " + r.Id + " '" + title + "' WYKONANE (dzien " + Terms.Day() + ", przyjete " + r.Day + ", termin " + r.Due + ") | " + how + " | nagroda " + def.Reward.Length + " akcji | " + r.Src + ".");
                try { Log.Player("Task complete: " + title + "."); } catch { }
                if (def.Reward.Length > 0) Run(new Ctx { Id = def.Id, RowKey = "" }, def.Reward);
            }

            private static void TaskFail(TaskRow r, Data.TaskDef def, string why)
            {
                string title = TaskTitle(def);
                Log.Info(Tag + "zadanie " + r.Id + " '" + title + "' NIEUDANE - " + why + " (przyjete " + r.Day + ", termin " + r.Due + ", " + r.Src + ").");
                try { Log.Player("Task failed: " + title + ".", true); } catch { }
            }

            /// <summary>Banda bNNN rozbita przez gracza (Bands.OnMapEventEnded) - zadania defeat tej bandy wykonane.</summary>
            private static void TasksBandDefeated(string bandId)
            {
                for (int i = 0; i < _tasks.Count; i++)
                {
                    var r = _tasks[i];
                    var def = Data.Task(r.Id);
                    if (def == null || def.Kind != "defeat" || def.What != bandId) continue;
                    _tasks.RemoveAt(i); i--;
                    try { TaskDone(r, def, "banda " + bandId + " rozbita przez gracza"); } catch (Exception e) { Stumble("Tasks.Defeated(" + r.Id + ")", e); }
                }
            }

            /// <summary>Banda bNNN zniknela bez gracza (rozbita przez innych, koniec terminu) - zadania defeat tej bandy nieudane.</summary>
            private static void TasksBandGone(string bandId, string why)
            {
                for (int i = 0; i < _tasks.Count; i++)
                {
                    var r = _tasks[i];
                    var def = Data.Task(r.Id);
                    if (def == null || def.Kind != "defeat" || def.What != bandId) continue;
                    _tasks.RemoveAt(i); i--;
                    try { TaskFail(r, def, why); } catch (Exception e) { Stumble("Tasks.Gone(" + r.Id + ")", e); }
                }
            }

            /// <summary>Zapis "kz id zrodlo cel dzien termin".</summary>
            private static void TasksExport(StringBuilder sb)
            {
                foreach (var r in _tasks)
                    sb.Append("kz\t").Append(r.Id).Append('\t').Append(r.Src.Replace('\t', ' ')).Append('\t').Append(r.Target).Append('\t')
                      .Append(r.Day.ToString(Inv)).Append('\t').Append(r.Due.ToString(Inv)).Append('\n');
            }

            private static void TasksImport(string s)
            {
                foreach (var raw in s.Split('\n'))
                {
                    var p = raw.TrimEnd('\r').Split('\t');
                    if (p[0] != "kz" || p.Length < 6 || p[1].Length == 0 || p[3].Length == 0) continue;
                    int d, u;
                    if (!int.TryParse(p[4], NumberStyles.AllowLeadingSign, Inv, out d) || !int.TryParse(p[5], NumberStyles.AllowLeadingSign, Inv, out u)) continue;
                    if (_tasks.Count >= MaxTasks * 2) break;
                    _tasks.Add(new TaskRow { Id = p[1], Src = p[2], Target = p[3], Day = d, Due = u });
                }
            }
        }
    }
}
