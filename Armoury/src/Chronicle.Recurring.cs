using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    internal static partial class Chronicle
    {
        /// <summary>
        /// m105-kronika K0 (krok A2) - OKNA POWRACAJACE (KRONIKA-FORMAT 13, kolumna powtorz). Instancja = NNN.w#klucz w stanie (State), tokeny @cel/@kto/@rodkto/@region/
        /// @liczba/@pora w zmiennych, warunkach i akcjach. Wyzwalacz daje kandydata; kandydat wchodzi do KOLEJKI (zapis "kq"), gdy: klucz Kronika TAK, nie prolog (albo
        /// opcja prolog), dzien opowiesci >= od, instancja nierozstrzygnieta i nie w kolejce, max:N (licznik instancji okna w zapisie "kl") i odstep:N (dni od ostatniej
        /// instancji okna) pozwalaja, warunki wiersza glownego z tokenami prawdziwe. WYDANIE (doba): najwyzej MaxRecurringPerDay instancji widocznych (forma okno albo
        /// wiesc) na dobe, kolejnosc kolejki; log/nic - od razu. Okno - kolejka okien silnika (te same bramki i tempo); wiesc/log/nic - rozstrzygniecie. Instancja nie
        /// wydana albo nie pokazana przez MaxLate dni - POMINIETE (wydana: wiesc, gdy jest _W). Rejestr kluczy w zapisie najwyzej RecurringKeys (State.PruneInstances).
        /// Wyzwalacze (detektory doby, poza oldage/ironraid - zdarzenia kampanii):
        ///  season - zmiana pory roku WesterosClimate.TextSeason (pierwsza obserwacja w kampanii to nie zmiana), kandydat do MaxLate dni po zmianie; klucz pora-dzien
        ///           zmiany; @pora.
        ///  siegehunger:N - oblezona twierdza N dob z rzedu z Town.FoodStocks &lt;= 0 (licznik w zapisie "kh"); klucz osada-dzien startu oblezenia; @cel, @region.
        ///  ironraid - wies zlupiona przez stronnictwo sturgia (Zelazne Wyspy), gdy jej warownia (Village.Bound) ma port; klucz warownia-okres 30 dni; @cel, @region.
        ///  oldage - BeforeHeroKilled ze staroscia: glowa rodu szlacheckiego (bez mniejszych i band) z tierem >= 4 albo rod wladcy krolestwa; klucz bohater; @kto, @rodkto,
        ///           @region (krolestwo rodu), @cel (Clan.HomeSettlement, zapas - najwieksza osada rodu).
        ///  others:N[,N] - liczba osad (miasta, zamki, wsie) rodu Innych ROTclan_126 &gt;= N; klucz nN; @liczba = liczba osad.
        ///  captured:osada[:k] - wlasciciel (krolestwo) osady zmienil sie od poprzedniej doby (zapis "ko"), opcjonalnie na k; klucz osada-krolestwo-dzien; @cel, @region.
        ///  besieged:osada - osada oblezona; klucz osada-dzien startu oblezenia; @cel, @region.
        ///  wizja:N - co N dni (od ostatniej instancji okna): @cel = oblezona twierdza Polnocy (battania) albo Muru (nightswatch), inaczej osada Innych najblizsza
        ///           Winterfell (town_B2), inaczej brak instancji; @region = krolestwo @cel; klucz dzien; najwyzej jedna instancja okna w kolejce.
        ///  brokenmen - (B2) okreg OutlawLaw (warownia + wsie) z rabunkiem w ostatnich BrokenRaidDays dniach (PeopleBook.LastStrike - zegar rabunku/zerowania z trafionymi)
        ///           i ponad BrokenShare ludzi okregu poza domem: (uchodzcy wsi okregu PeopleBook.RegionRefugees + pula wyrzutkow OutlawLaw.PoolIn) wobec (to + ludzie
        ///           w domu we wsiach okregu PopulationLaw.PeopleOf); klucz okreg-okres 90 dni; @cel = warownia okregu, @region = jej krolestwo. Raz na dobe, tylko gdy
        ///           jakies okno ma ten wyzwalacz (warunki okna z tokenami odsiewaja kandydatow, np. p:region:@region).
        /// Wyjatek detektora - potkniecie; reszta dziala.
        /// </summary>
        internal static class Recurring
        {
            internal static readonly string[] Triggers = { "season", "siegehunger", "ironraid", "oldage", "brokenmen", "others", "captured", "besieged", "wizja" };
            private static readonly HashSet<string> Unsupported = new HashSet<string>(StringComparer.Ordinal);   // B2: brokenmen wspierany - pusty zbior zostaje na przyszle wyzwalacze
            private const int BrokenRaidDays = 30, BrokenPeriod = 90;
            private const double BrokenShare = 0.30;
            private static readonly string[] SeasonNames = { "spring", "summer", "autumn", "winter" };   // kolejnosc CampaignTime.Seasons / WesterosClimate
            private const string OthersClan = "ROTclan_126";
            private const string IronKingdom = "sturgia";
            private const int QueueMax = 40;

            private sealed class Inst
            {
                public string Id = "", Win = "", Trig = "";
                public Terms.Tokens Tok;
                public int Day, Rel = -1;   // dzien wyzwalacza; dzien wydania (-1 - w kolejce)
            }

            private static readonly List<Inst> _queue = new List<Inst>();
            private static readonly Dictionary<string, int[]> _stat = new Dictionary<string, int[]>(StringComparer.Ordinal);    // okno -> [ostatnia instancja (dzien), liczba instancji]
            private static int _season = -2, _seasonDay = -1;
            private static readonly Dictionary<string, string> _owner = new Dictionary<string, string>(StringComparer.Ordinal); // captured: osada -> krolestwo wlasciciela
            private static readonly Dictionary<string, int[]> _hunger = new Dictionary<string, int[]>(StringComparer.Ordinal);  // siegehunger: osada -> [doby bez jedzenia, dzien startu oblezenia]
            private static int _relDay = -1, _relCount;
            private static int _gen = -1;
            private static readonly List<Data.WindowDef> _wins = new List<Data.WindowDef>();
            private static int _dQueued, _dReleased, _dExpired, _dPruned, _dFull;

            internal static int QueueCount { get { return _queue.Count; } }

            internal static void Reset()
            {
                _queue.Clear(); _stat.Clear(); _owner.Clear(); _hunger.Clear();
                _season = -2; _seasonDay = -1; _relDay = -1; _relCount = 0;
                DayReset();
            }

            internal static void DayReset() { _dQueued = _dReleased = _dExpired = _dPruned = _dFull = 0; }

            private static void EnsureList()
            {
                if (_gen == Data.Generation) return;
                _gen = Data.Generation;
                _wins.Clear();
                foreach (var w in Data.Windows) if (w.Recur != null) _wins.Add(w);
            }

            internal static void SessionStart() { EnsureList(); }

            internal static string StartText()
            {
                EnsureList();
                var by = new SortedDictionary<string, int>(StringComparer.Ordinal);
                var un = new StringBuilder();
                var bad = new StringBuilder();
                foreach (var w in _wins)
                {
                    string t = w.Recur.Trigger;
                    int n; by.TryGetValue(t, out n); by[t] = n + 1;
                    if (Unsupported.Contains(t)) un.Append(un.Length > 0 ? "," : "").Append(w.Id);
                    else if (Array.IndexOf(Triggers, t) < 0) bad.Append(bad.Length > 0 ? "," : "").Append(w.Id + "(" + t + ")");
                }
                var sb = new StringBuilder("okna powracajace ").Append(_wins.Count);
                if (by.Count > 0)
                {
                    sb.Append(" (");
                    bool first = true;
                    foreach (var kv in by) { if (!first) sb.Append(", "); first = false; sb.Append(kv.Key).Append(' ').Append(kv.Value); }
                    sb.Append(')');
                }
                if (un.Length > 0) sb.Append(", wyzwalacz niewspierany w K0 - okna czekaja: ").Append(un);
                if (bad.Length > 0) sb.Append(", NIEZNANY wyzwalacz: ").Append(bad);
                sb.Append(", pora roku ").Append(_season >= 0 && _season < 4 ? SeasonNames[_season] : "-").Append(_seasonDay >= 0 ? " (od dnia " + _seasonDay + ")" : "");
                return sb.ToString();
            }

            internal static string DayText()
            {
                return "powracajace: kolejka " + _queue.Count + " (nowe " + _dQueued + ", wydane " + _dReleased + ", przepadle " + _dExpired + (_dFull > 0 ? ", kolejka pelna " + _dFull : "")
                       + (_dPruned > 0 ? ", klucze usuniete " + _dPruned : "") + ")";
            }

            private static bool InQueue(string id)
            {
                foreach (var q in _queue) if (q.Id == id) return true;
                return false;
            }

            private static bool WindowQueued(string win)
            {
                foreach (var q in _queue) if (q.Win == win) return true;
                return false;
            }

            private static string CleanKey(string k)
            {
                var sb = new StringBuilder(k.Length);
                foreach (char ch in k) sb.Append(ch == '#' || ch == '|' || ch == '\t' || ch == '\n' || ch == '\r' || ch == ' ' ? '_' : ch);
                return sb.ToString();
            }

            private static int ArgInt(string s, int def)
            {
                int v;
                return int.TryParse(s, NumberStyles.Integer, Inv, out v) ? v : def;
            }

            /// <summary>Kandydat instancji - do kolejki, gdy przechodzi warunki (opis klasy).</summary>
            private static void Candidate(Data.WindowDef w, string key, Terms.Tokens tok, int day, string trig)
            {
                if (!KeyOn) return;
                string id = w.Id + "#" + CleanKey(key);
                if (Prologue.BeforeStory() && !w.Recur.Prolog) return;
                if (w.Od > 0 && Terms.StoryDay() < Terms.DocToStory(w.Od)) return;
                if (State.IsDone(id) || InQueue(id)) return;
                int[] st;
                _stat.TryGetValue(w.Id, out st);
                if (w.Recur.Max > 0 && st != null && st[1] >= w.Recur.Max) return;
                if (w.Recur.Gap > 0 && st != null && st[0] >= 0 && day - st[0] < w.Recur.Gap) return;
                bool nv; string why;
                if (!Terms.Eval(w.Main.Cond, tok, id + " warunki", out nv, out why)) return;
                if (_queue.Count >= QueueMax) { _dFull++; return; }
                _queue.Add(new Inst { Id = id, Win = w.Id, Trig = trig, Tok = tok, Day = day });
                _dQueued++;
                Log.Info(Tag + "instancja " + id + " w kolejce (wyzwalacz " + trig + ", tokeny " + TokText(tok) + ", w kolejce " + _queue.Count + ").");
            }

            // ---------------------------------------------------------------- doba

            internal static void Daily(int day)
            {
                EnsureList();
                if (_wins.Count == 0) return;
                try { Season(day); } catch (Exception e) { Stumble("Recurring.Season", e); }
                try { Others(day); } catch (Exception e) { Stumble("Recurring.Others", e); }
                try { Captured(day); } catch (Exception e) { Stumble("Recurring.Captured", e); }
                try { Sieges(day); } catch (Exception e) { Stumble("Recurring.Sieges", e); }
                try { Vision(day); } catch (Exception e) { Stumble("Recurring.Vision", e); }
                try { BrokenMen(day); } catch (Exception e) { Stumble("Recurring.BrokenMen", e); }
                try { Release(day); } catch (Exception e) { Stumble("Recurring.Release", e); }
                try { _dPruned += State.PruneInstances(RecurringKeys); } catch (Exception e) { Stumble("Recurring.Prune", e); }
            }

            private static IEnumerable<Data.WindowDef> With(string trig)
            {
                foreach (var w in _wins) if (w.Recur.Trigger == trig) yield return w;
            }

            private static bool Any(string trig)
            {
                foreach (var w in _wins) if (w.Recur.Trigger == trig) return true;
                return false;
            }

            private static void Season(int day)
            {
                if (!Any("season")) return;
                var now = CampaignTime.Now;
                int s = WesterosClimate.TextSeason(ref now);
                if (s < 0 || s > 3) return;
                if (_season == -2) { _season = s; _seasonDay = -1; return; }   // pierwsza obserwacja (nowa kampania albo zapis sprzed Kroniki) - to nie zmiana pory
                if (s != _season)
                {
                    Log.Info(Tag + "pora roku: " + (_season >= 0 && _season < 4 ? SeasonNames[_season] : "-") + " -> " + SeasonNames[s] + " (dzien " + day + ").");
                    _season = s; _seasonDay = day;
                }
                if (_seasonDay < 0 || day - _seasonDay > MaxLate) return;   // zmiana pory dawniej niz MaxLate dni (np. w prologu) - juz nie "nowa pora"
                foreach (var w in With("season"))
                    Candidate(w, SeasonNames[s] + "-" + _seasonDay, new Terms.Tokens { Pora = SeasonNames[s] }, day, "season");
            }

            private static void Others(int day)
            {
                if (!Any("others")) return;
                int n = -1;
                foreach (var w in With("others"))
                {
                    int[] st;
                    if (w.Recur.Max > 0 && _stat.TryGetValue(w.Id, out st) && st[1] >= w.Recur.Max) continue;
                    if (n < 0)
                    {
                        n = 0;
                        foreach (var s in Settlement.All)
                            if (s != null && (s.IsTown || s.IsCastle || s.IsVillage) && s.OwnerClan != null && s.OwnerClan.StringId == OthersClan) n++;
                    }
                    foreach (var part in w.Recur.Arg.Split(','))
                    {
                        int th = ArgInt(part.Trim(), -1);
                        if (th < 1 || n < th) continue;
                        Candidate(w, "n" + th, new Terms.Tokens { Liczba = n.ToString(Inv) }, day, "others:" + th);
                    }
                }
            }

            private static void Captured(int day)
            {
                if (!Any("captured")) return;
                var changed = new Dictionary<string, string>(StringComparer.Ordinal);   // osada -> nowe krolestwo (zmiana dzis)
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var w in With("captured"))
                {
                    string sid = w.Recur.Arg; int c = sid.IndexOf(':');
                    if (c >= 0) sid = sid.Substring(0, c);
                    if (sid.Length == 0 || !seen.Add(sid)) continue;
                    var st = Terms.FindSettlement(sid, null);
                    if (st == null) continue;
                    string own = st.MapFaction != null ? st.MapFaction.StringId ?? "" : "";
                    string old;
                    if (!_owner.TryGetValue(sid, out old)) { _owner[sid] = own; continue; }   // pierwsza obserwacja - zapis wlasciciela
                    if (old == own) continue;
                    _owner[sid] = own;
                    changed[sid] = own;
                    Log.Info(Tag + "osada " + sid + " zmienila wlasciciela: " + old + " -> " + own + " (dzien " + day + ").");
                }
                if (changed.Count == 0) return;
                foreach (var w in With("captured"))
                {
                    string sid = w.Recur.Arg, k = "";
                    int c = sid.IndexOf(':');
                    if (c >= 0) { k = sid.Substring(c + 1); sid = sid.Substring(0, c); }
                    string own;
                    if (!changed.TryGetValue(sid, out own) || (k.Length > 0 && own != k)) continue;
                    Candidate(w, sid + "-" + own + "-" + day, new Terms.Tokens { Cel = sid, Region = own }, day, "captured");
                }
            }

            /// <summary>besieged i siegehunger - jeden przebieg po twierdzach.</summary>
            private static void Sieges(int day)
            {
                bool bes = Any("besieged"), hun = Any("siegehunger");
                if (!bes && !hun) return;
                if (bes)
                    foreach (var w in With("besieged"))
                    {
                        var st = Terms.FindSettlement(w.Recur.Arg, null);
                        if (st == null || st.SiegeEvent == null) continue;
                        int start = (int)st.SiegeEvent.SiegeStartTime.ToDays;
                        Candidate(w, st.StringId + "-" + start, new Terms.Tokens { Cel = st.StringId, Region = st.MapFaction != null ? st.MapFaction.StringId : "" }, day, "besieged");
                    }
                if (!hun) return;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var st in Settlement.All)
                {
                    if (st == null || !st.IsFortification || st.Town == null || st.SiegeEvent == null || st.StringId == null) continue;
                    int start = (int)st.SiegeEvent.SiegeStartTime.ToDays;
                    seen.Add(st.StringId);
                    int[] h;
                    if (!_hunger.TryGetValue(st.StringId, out h) || h[1] != start) { h = new[] { 0, start }; _hunger[st.StringId] = h; }
                    if (st.Town.FoodStocks <= 0f) h[0]++; else h[0] = 0;
                    if (h[0] <= 0) continue;
                    foreach (var w in With("siegehunger"))
                    {
                        int n = ArgInt(w.Recur.Arg, 1);
                        if (h[0] < n) continue;
                        Candidate(w, st.StringId + "-" + start, new Terms.Tokens { Cel = st.StringId, Region = st.MapFaction != null ? st.MapFaction.StringId : "" }, day, "siegehunger:" + n);
                    }
                }
                var gone = new List<string>();
                foreach (var k in _hunger.Keys) if (!seen.Contains(k)) gone.Add(k);
                foreach (var k in gone) _hunger.Remove(k);
            }

            private static void Vision(int day)
            {
                if (!Any("wizja")) return;
                Settlement target = null;
                bool looked = false;
                foreach (var w in With("wizja"))
                {
                    if (WindowQueued(w.Id)) continue;
                    int n = Math.Max(1, ArgInt(w.Recur.Arg, 60));
                    int[] st;
                    if (_stat.TryGetValue(w.Id, out st) && st[0] >= 0 && day - st[0] < n) continue;
                    if (!looked) { looked = true; target = VisionTarget(); }
                    if (target == null) return;
                    Candidate(w, "d" + day, new Terms.Tokens { Cel = target.StringId, Region = target.MapFaction != null ? target.MapFaction.StringId : "" }, day, "wizja:" + n);
                }
            }

            /// <summary>B2 brokenmen (opis klasy): okregi z rabunkiem w BrokenRaidDays dniach i ponad BrokenShare ludzi poza domem - kandydat (klucz okreg-okres).</summary>
            private static void BrokenMen(int day)
            {
                if (!Any("brokenmen")) return;
                List<Data.WindowDef> wins = null;
                foreach (var w in With("brokenmen"))
                {
                    int[] st;
                    if (w.Recur.Max > 0 && _stat.TryGetValue(w.Id, out st) && st[1] >= w.Recur.Max) continue;
                    (wins ?? (wins = new List<Data.WindowDef>())).Add(w);
                }
                if (wins == null) return;
                foreach (var region in OutlawLaw.RegionNodes())
                {
                    if (region == null || region.StringId == null || region.BoundVillages == null || region.BoundVillages.Count == 0) continue;
                    int last;
                    if (!PeopleBook.LastStrike(region, out last) || day - last > BrokenRaidDays) continue;
                    double away, dead;
                    PeopleBook.RegionRefugees(region, out away, out dead);
                    double outside = Math.Max(0.0, away) + Math.Max(0f, OutlawLaw.PoolIn(region)), home = 0.0;
                    foreach (var v in region.BoundVillages) if (v != null && v.Settlement != null) home += Math.Max(0f, PopulationLaw.PeopleOf(v.Settlement));
                    if (!(outside + home > 0.0) || outside / (outside + home) <= BrokenShare) continue;
                    var tok = new Terms.Tokens { Cel = region.StringId, Region = region.MapFaction != null ? region.MapFaction.StringId : "" };
                    foreach (var w in wins)
                        Candidate(w, region.StringId + "-" + (day / BrokenPeriod), tok, day, "brokenmen (" + (100.0 * outside / (outside + home)).ToString("0", Inv) + "% poza domem, rabunek d" + last + ")");
                }
            }

            /// <summary>@cel wizji: oblezona twierdza Polnocy albo Muru, inaczej osada Innych najblizsza Winterfell, inaczej null.</summary>
            private static Settlement VisionTarget()
            {
                foreach (var s in Settlement.All)
                    if (s != null && s.IsFortification && s.SiegeEvent != null && s.MapFaction != null && (s.MapFaction.StringId == "battania" || s.MapFaction.StringId == "nightswatch"))
                        return s;
                var wf = Terms.FindSettlement("town_B2", null);
                Settlement best = null;
                float bd = float.MaxValue;
                foreach (var s in Settlement.All)
                {
                    if (s == null || !(s.IsTown || s.IsCastle || s.IsVillage) || s.OwnerClan == null || s.OwnerClan.StringId != OthersClan) continue;
                    float d = wf != null ? s.GetPosition2D.DistanceSquared(wf.GetPosition2D) : 0f;
                    if (d < bd) { bd = d; best = s; }
                }
                return best;
            }

            // ---------------------------------------------------------------- zdarzenia kampanii

            /// <summary>BeforeHeroKilledEvent (przed zmiana glowy rodu): smierc ze starosci glowy waznego rodu - kandydat oldage.</summary>
            internal static void OnBeforeHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool notify)
            {
                if (detail != KillCharacterAction.KillCharacterActionDetail.DiedOfOldAge || victim == null || !KeyOn) return;
                try
                {
                    EnsureList();
                    if (!Any("oldage")) return;
                    var cl = victim.Clan;
                    if (cl == null || cl.Leader != victim || victim == Hero.MainHero || cl.IsBanditFaction || cl.IsMinorFaction || !cl.IsNoble) return;
                    bool important = cl.Tier >= 4 || (cl.Kingdom != null && cl.Kingdom.RulingClan == cl);
                    if (!important) return;
                    Settlement seat = cl.HomeSettlement;
                    if (seat == null)
                        foreach (var s in cl.Settlements)
                            if (s != null && (seat == null || (s.IsTown && !seat.IsTown) || (s.IsCastle && seat.IsVillage))) seat = s;
                    var tok = new Terms.Tokens { Kto = victim.StringId, Rodkto = cl.StringId, Region = cl.Kingdom != null ? cl.Kingdom.StringId : "", Cel = seat != null ? seat.StringId : "" };
                    int day = Terms.Day();
                    foreach (var w in With("oldage")) Candidate(w, victim.StringId, tok, day, "oldage");
                }
                catch (Exception e) { Stumble("Recurring.OldAge", e); }
            }

            /// <summary>RaidCompletedEvent: wies na wybrzezu (warownia z portem) zlupiona przez Zelaznych - kandydat ironraid.</summary>
            internal static void OnRaidCompleted(BattleSideEnum winner, RaidEventComponent raid)
            {
                if (!KeyOn || raid == null || winner != BattleSideEnum.Attacker) return;
                try
                {
                    EnsureList();
                    if (!Any("ironraid")) return;
                    var st = raid.MapEventSettlement;
                    if (st == null || !st.IsVillage || st.Village == null) return;
                    var lp = raid.AttackerSide != null ? raid.AttackerSide.LeaderParty : null;
                    var f = lp != null ? lp.MapFaction : null;
                    if (f == null || f.StringId != IronKingdom) return;
                    var bound = st.Village.Bound;
                    if (bound == null || !bound.HasPort) return;
                    int day = Terms.Day();
                    var tok = new Terms.Tokens { Cel = st.StringId, Region = st.MapFaction != null ? st.MapFaction.StringId : "" };
                    foreach (var w in With("ironraid")) Candidate(w, bound.StringId + "-" + (day / 30), tok, day, "ironraid");
                }
                catch (Exception e) { Stumble("Recurring.IronRaid", e); }
            }

            // ---------------------------------------------------------------- wydanie, plan, rozstrzygniecie

            private static void Stat(string win, int day)
            {
                int[] st;
                if (!_stat.TryGetValue(win, out st)) { st = new[] { -1, 0 }; _stat[win] = st; }
                st[0] = day; st[1]++;
            }

            /// <summary>Wydanie z kolejki: najwyzej MaxRecurringPerDay instancji widocznych (okno/wiesc) na dobe; log/nic od razu.</summary>
            private static void Release(int day)
            {
                if (_relDay != day) { _relDay = day; _relCount = 0; }
                bool prolog = Prologue.BeforeStory();
                foreach (var q in _queue.ToArray())
                {
                    if (q.Rel >= 0) continue;
                    if (State.IsDone(q.Id)) { _queue.Remove(q); continue; }
                    var w = Data.Window(q.Win);
                    if (w == null || w.Recur == null) continue;   // paczka wylaczona - instancja przepadnie po MaxLate
                    if (prolog && !w.Recur.Prolog) continue;
                    bool nv; string why;
                    if (!Terms.Eval(w.Main.Cond, q.Tok, q.Id + " warunki", out nv, out why)) continue;
                    var p = PickRow(w, q.Tok, out why);
                    if (p == null) continue;
                    bool visible = p.Form == "okno" || p.Form == "wiesc";
                    if (visible && _relCount >= MaxRecurringPerDay) continue;
                    q.Rel = day;
                    Stat(w.Id, day);
                    if (visible) _relCount++;
                    _dReleased++;
                    if (p.Form == "okno") AddReady(q.Id, w, q.Tok, true);
                    else Resolve(q.Id, w, p, p.Form, q.Tok, "instancja " + q.Trig + (p.Player ? "" : "; gracz poza kolumna gracz: " + p.PlayerWhy));
                }
            }

            /// <summary>Plan silnika: wydane instancje z forma okno - do kolejki okien; przeterminowane (MaxLate od wydania albo od wyzwalacza) - wiesc albo POMINIETE.</summary>
            internal static void Plan(int day, bool act)
            {
                foreach (var q in _queue.ToArray())
                {
                    if (State.IsDone(q.Id)) { _queue.Remove(q); continue; }
                    var w = Data.Window(q.Win);
                    int age = q.Rel >= 0 ? day - q.Rel : day - q.Day;
                    if (age > MaxLate)
                    {
                        if (!act) continue;
                        _queue.Remove(q);
                        _dExpired++;
                        if (w == null || w.Recur == null) { Skip(q.Id, "okna " + q.Win + " nie ma w danych (paczka wylaczona albo zmieniona)"); continue; }
                        if (q.Rel < 0) { Skip(q.Id, "instancja czekala w kolejce ponad " + MaxLate + " dni (limit " + MaxRecurringPerDay + " na dobe, warunki albo prolog)"); continue; }
                        string why;
                        var p = PickRow(w, q.Tok, out why);
                        string t = "instancja nie pokazana przez " + age + " dni (limit " + MaxLate + ")";
                        if (p != null && p.Form == "nic") Resolve(q.Id, w, p, "nic", q.Tok, t + "; gracz poza kolumna gracz: " + p.PlayerWhy);
                        else if (p != null && Data.NewsRaw(p.Row) != null) Resolve(q.Id, w, p, "wiesc", q.Tok, t);
                        else Skip(q.Id, t);
                        continue;
                    }
                    if (q.Rel >= 0 && w != null && w.Recur != null) AddReady(q.Id, w, q.Tok, true);
                }
            }

            /// <summary>Chronicle.Resolve: instancja rozstrzygnieta - z kolejki.</summary>
            internal static void OnResolved(Data.WindowDef w, string id)
            {
                for (int i = _queue.Count - 1; i >= 0; i--) if (_queue[i].Id == id) _queue.RemoveAt(i);
            }

            // ---------------------------------------------------------------- zapis

            internal static string Export()
            {
                var sb = new StringBuilder(64 * (_queue.Count + _stat.Count + _owner.Count + _hunger.Count) + 32);
                foreach (var q in _queue)
                    sb.Append("kq\t").Append(q.Id).Append('\t').Append(q.Win).Append('\t').Append(q.Trig).Append('\t').Append(q.Day.ToString(Inv)).Append('\t')
                      .Append(q.Rel.ToString(Inv)).Append('\t').Append(TokText(q.Tok)).Append('\n');
                foreach (var kv in _stat) sb.Append("kl\t").Append(kv.Key).Append('\t').Append(kv.Value[0].ToString(Inv)).Append('\t').Append(kv.Value[1].ToString(Inv)).Append('\n');
                if (_season != -2) sb.Append("kt\tseason\t").Append(_season.ToString(Inv)).Append('\t').Append(_seasonDay.ToString(Inv)).Append('\n');
                foreach (var kv in _owner) sb.Append("ko\t").Append(kv.Key).Append('\t').Append(kv.Value).Append('\n');
                foreach (var kv in _hunger) sb.Append("kh\t").Append(kv.Key).Append('\t').Append(kv.Value[0].ToString(Inv)).Append('\t').Append(kv.Value[1].ToString(Inv)).Append('\n');
                return sb.ToString();
            }

            internal static void Import(string s)
            {
                Reset();
                if (string.IsNullOrEmpty(s)) return;
                foreach (var raw in s.Split('\n'))
                {
                    var p = raw.TrimEnd('\r').Split('\t');
                    if (p.Length < 2 || p[1].Length == 0) continue;
                    int a, b;
                    switch (p[0])
                    {
                        case "kq":
                            if (p.Length >= 7 && int.TryParse(p[4], NumberStyles.AllowLeadingSign, Inv, out a) && int.TryParse(p[5], NumberStyles.AllowLeadingSign, Inv, out b)
                                && !State.IsDone(p[1]) && _queue.Count < QueueMax)
                                _queue.Add(new Inst { Id = p[1], Win = p[2], Trig = p[3], Day = a, Rel = b, Tok = TokParse(p[6]) });
                            break;
                        case "kl":
                            if (p.Length >= 4 && int.TryParse(p[2], NumberStyles.AllowLeadingSign, Inv, out a) && int.TryParse(p[3], NumberStyles.Integer, Inv, out b)) _stat[p[1]] = new[] { a, b };
                            break;
                        case "kt":
                            if (p[1] == "season" && p.Length >= 4 && int.TryParse(p[2], NumberStyles.AllowLeadingSign, Inv, out a) && int.TryParse(p[3], NumberStyles.AllowLeadingSign, Inv, out b))
                            { _season = a; _seasonDay = b; }
                            break;
                        case "ko":
                            if (p.Length >= 3) _owner[p[1]] = p[2];
                            break;
                        case "kh":
                            if (p.Length >= 4 && int.TryParse(p[2], NumberStyles.Integer, Inv, out a) && int.TryParse(p[3], NumberStyles.AllowLeadingSign, Inv, out b)) _hunger[p[1]] = new[] { a, b };
                            break;
                    }
                }
            }
        }
    }
}
