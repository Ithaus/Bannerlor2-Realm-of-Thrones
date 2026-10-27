using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;

namespace Armoury
{
    /// <summary>
    /// 264 - BEZ POKOJU W WOJNIE KANONU DO KRWAWYCH GODOW (Jeff 27.10: "czemu dorzecze i polnoc jest slabsza od lannisterow i krolewskiej przystani to nie ma
    /// sensu"; audyt 27.10 rozdz. 3, punkt 4 "tak zacznij od tych pieciu"). Klucz CanonWarNoPeaceUntilRedWedding.
    /// Dowod: dzien 108982 (log Jeffa 26.10 21:42:38) pokoj Dorzecze - korona "ByKingdomDecision": CrashScribe StorylineReparations widzial jedyne wywolanie
    /// DetermineReparationsForMakingPeace = Diplomacy KingdomPeaceAction.ApplyPeace (zmeczenie wojna, WarExhaustionManager.ConsiderPeaceActions - Loss/Tie,
    /// bez pytania modelu uprawnien), konczy sie MakePeaceAction.ApplyByKingdomDecision. Nastepnej doby ROT EnforceWars wypowiada wojne znowu (IsWarForced) -
    /// a M6a-3 zdazyl uwolnic 6 jencow. Model uprawnien ROT (IsWarForced) jest pytany tylko z KingdomDecision.ShouldBeCancelled (co godzine w krolestwie gracza);
    /// AI (gra ConsiderPeace, BK TryQueuePeaceFor, CrashScribe PovertyPeace) idzie prosto przez Kingdom.AddDecision -> wybory od razu.
    /// REGULA (tabela Rules nizej - przelacznik danych): para Polnoc (battania) - korona (vlandia, House Baratheon of King's Landing z Lannisterami) i Dorzecze
    /// (riverlands) - korona, kolejnosc stron dowolna. Blokada CZYNNA, gdy: klucz TAK, kampania kanonu (Prologue.CanonHere - zakres m104 decyzji nr 5), zadna strona
    /// nie jest krolestwem gracza (gracz WLADCA jednej strony = pokoj to jego wybor - jak model ROT "PlayerKingdom == kingdom1" i regula m116 "Never if you rule
    /// either realm"; gracz wasal nie zawrze ani nie zaproponuje tego pokoju), oba krolestwa istnieja, wydarzenie ROT RedWeddingEvent nie ruszylo (stan < 1 - ten
    /// sam sygnal, ktory konczy sojusz A3 m116; -1 = nie widac - blokada trwa do smierci Robba, linia doby mowi "Krwawe Gody nie widac") i Robb (lord_5_1) zyje.
    /// Bez nowych liczb.
    /// LATKI (jedna instancja Harmony SubModuleMain, raz na proces, EnsureHooks w kampanii obok PeaceHold):
    ///  H1 prefiks First Diplomacy KingdomPeaceAction.ApplyPeace(Kingdom, Kingdom, int?, bool, bool, bool) - false dla pary z blokada PRZED kosztem Diplomacy,
    ///     zwrotem lenn, oknem gracza i MakePeaceAction (zloto nie rusza - nic z niczego). Obejmuje zmeczenie wojna (co dobe - blokada co dobe), wynik glosowania
    ///     (MakePeaceKingdomDecisionPatch Diplomacy idzie przez ApplyPeace), CivilWar i przycisk ekranu krolestwa (otwarty tylko wladcy). BkDipGold (AcceptPeace)
    ///     i StorylineReparations CrashScribe po prostu nie zachodza.
    ///  H2 prefiks First Kingdom.AddDecision(KingdomDecision, bool): MakePeaceKingdomDecision pary z blokada - false przed kosztem wplywow i wyborami (gra
    ///     ConsiderPeace AI, BK TryQueuePeaceFor, CrashScribe PovertyPeace, wniosek gracza-wasala - jemu komunikat raz na sesje). CrashScribe
    ///     StoryThrone.AddDecisionPrefix tez First - oba biegna, bez sprzecznosci.
    ///  H3 postfiks MakePeaceKingdomDecision.IsAllowed -> false: decyzja pokoju czekajaca w krolestwie gracza (stary zapis) - gra anuluje ja w ciagu godziny
    ///     (ShouldBeCancelled). Cialo metody duzo ponad 12 bajtow IL - ryzyko zawieszenia GC malej metody nie dotyczy.
    ///  Bez blokady (celowo): bezposrednie MakePeaceAction.Apply (Kronika "truce:" - i tak pomija wojny wymuszone ROT; Prologue.Crown; kody). CrownCallToWar 168
    ///  (zerwane wezwanie) pyta Stops() obok swojego warunku RotWarForced.
    /// Linie "Pokoj kanonu (264)": start (latki, klucz), kazda zablokowana proba (najwyzej raz na pare, droge i dobe; liczniki wszystkich), doba.
    /// Zapis: brak (stan z gry). Bledy: potkniecia liczone (Stumble), bez globalnego wylacznika.
    /// </summary>
    internal static class CanonPeaceLock
    {
        internal const string Tag = "Pokoj kanonu (264): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Wiersz tabeli: para krolestw (id ROT ROTKingdoms), etykieta linii, wydarzenie ROT konczace blokade (stan >= 1) i bohater, ktorego smierc ja konczy.</summary>
        private sealed class Rule
        {
            public string A, B, Label, EndEvent, EndHero, EndHeroName;
            public Rule(string a, string b, string label, string endEvent, string endHero, string endHeroName) { A = a; B = b; Label = label; EndEvent = endEvent; EndHero = endHero; EndHeroName = endHeroName; }
        }

        // TABELA (przelacznik danych 264): Wojna Pieciu Krolow - Polnoc i Dorzecze przeciw koronie (vlandia = House Baratheon of King's Landing, Lannisterowie)
        // do Krwawych Godow. Reach i Dorne (Prologue.Crown) poza tabela - kolejny wiersz, gdy Jeff zechce.
        private static readonly Rule[] Rules =
        {
            new Rule(Prologue.NorthId, Prologue.Crown, "Polnoc-korona", "RedWeddingEvent", Prologue.RobbId, "Robb"),
            new Rule(Prologue.RiverId, Prologue.Crown, "Dorzecze-korona", "RedWeddingEvent", Prologue.RobbId, "Robb"),
        };

        internal static bool KeyOn { get { var s = Settings.Current; return s != null && s.CanonWarNoPeaceUntilRedWedding; } }

        // ---- stan doby / sesji (bez zapisu)
        private static int _dBlocked, _sBlocked, _stumbles, _dStumbles;
        private static readonly Dictionary<string, int> _dPaths = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly HashSet<string> _dSaid = new HashSet<string>(StringComparer.Ordinal);   // para|droga - linia raz na dobe
        private static bool _playerTold;
        // pamiec konca blokady na godzine gry (IsAllowed pyta czesto - RotEventState i Hero.FindFirst raz na godzine)
        private static long _cacheHour = long.MinValue;
        private static string[] _cacheEnd = new string[Rules.Length];
        private static int _cacheRw = -1;
        private static bool _cacheRobbDead, _cacheRobbMissing;

        internal static void Reset()
        {
            _dBlocked = _sBlocked = _stumbles = _dStumbles = 0;
            _dPaths.Clear(); _dSaid.Clear(); _playerTold = false;
            _cacheHour = long.MinValue; _cacheEnd = new string[Rules.Length]; _cacheRw = -1; _cacheRobbDead = _cacheRobbMissing = false;
            CanonWarWatch.Reset();
            CanonPeaceLockTest.Reset();
        }

        internal static void Stumble(string where, Exception e)
        {
            _stumbles++; _dStumbles++;
            if (_stumbles <= 5) { try { Log.Error("CanonPeaceLock." + where + " (potkniecie " + _stumbles + ")", e); } catch { } }
        }

        private static string Name(IFaction f) { try { return f != null && f.Name != null ? f.Name.ToString() : "-"; } catch { return "?"; } }

        // ------------------------------------------------------------ regula (czysta funkcja - autotest T4 sprawdza tabele prawdy)

        /// <summary>Blokada pary wedlug warunkow (opis klasy). true = pokoj zablokowany; why - powod stanu do linii.</summary>
        internal static bool Locked(bool key, bool canon, bool playerRules, bool eliminated, int redWedding, bool heroDead, out string why)
        {
            if (!key) { why = "klucz NIE"; return false; }
            if (!canon) { why = "kampania klasyczna - nie dotyczy"; return false; }
            if (playerRules) { why = "gracz rzadzi strona - pokoj to jego wybor"; return false; }
            if (eliminated) { why = "krolestwo zniszczone"; return false; }
            if (redWedding >= 1) { why = "Krwawe Gody (RedWeddingEvent " + redWedding + ")"; return false; }
            if (heroDead) { why = "Robb nie zyje"; return false; }
            why = redWedding < 0 ? "Robb zyje, Krwawe Gody nie widac" : "Robb zyje, RedWeddingEvent 0";
            return true;
        }

        private static void RefreshCache()
        {
            long h = (long)Math.Floor(CampaignTime.Now.ToHours);
            if (h == _cacheHour) return;
            _cacheHour = h;
            _cacheRw = Prologue.RotEventState("RedWeddingEvent");
            var robb = Prologue.HeroById(Prologue.RobbId);
            _cacheRobbMissing = robb == null;
            _cacheRobbDead = robb != null && !robb.IsAlive;
        }

        private static int RuleIndex(IFaction a, IFaction b)
        {
            string x = a != null ? a.StringId : null, y = b != null ? b.StringId : null;
            if (x == null || y == null) return -1;
            for (int i = 0; i < Rules.Length; i++)
            {
                var r = Rules[i];
                if ((x == r.A && y == r.B) || (x == r.B && y == r.A)) return i;
            }
            return -1;
        }

        /// <summary>Stan reguly i pary teraz (bez logu); idx - wiersz tabeli albo -1.</summary>
        private static bool State(IFaction a, IFaction b, out int idx, out string why)
        {
            why = "-";
            idx = -1;
            if (a == null || b == null || !a.IsKingdomFaction || !b.IsKingdomFaction) return false;
            idx = RuleIndex(a, b);
            if (idx < 0) return false;
            if (!KeyOn) { why = "klucz NIE"; return false; }   // bez odczytu ROT, gdy wylaczone
            if (!Prologue.CanonHere) { why = "kampania klasyczna - nie dotyczy"; return false; }
            RefreshCache();
            bool heroDead = _cacheRobbDead;   // tabela: jeden bohater konca (Robb) dla obu wierszy
            return Locked(true, true, Prologue.RulesRealm(a) || Prologue.RulesRealm(b), a.IsEliminated || b.IsEliminated, _cacheRw, heroDead, out why);
        }

        /// <summary>Pokoj pary jest teraz zablokowany (tylko odczyt, bez logu). WorldClock7 (linia POKOJ) i autotest.</summary>
        internal static bool Blocks(IFaction a, IFaction b)
        {
            try { int i; string w; return State(a, b, out i, out w); }
            catch (Exception e) { Stumble("Blocks", e); return false; }
        }

        /// <summary>Zablokowany - licznik i linia (raz na pare, droge i dobe). CrownCallToWar 168 i latki.</summary>
        internal static bool Stops(IFaction a, IFaction b, string path)
        {
            try
            {
                int i; string why;
                if (!State(a, b, out i, out why)) return false;
                Note(a, b, i, why, path);
                return true;
            }
            catch (Exception e) { Stumble("Stops", e); return false; }
        }

        private static void Note(IFaction a, IFaction b, int idx, string why, string path)
        {
            _dBlocked++; _sBlocked++;
            string pk = PathKey(path);
            int n; _dPaths.TryGetValue(pk, out n); _dPaths[pk] = n + 1;
            if (TestArmed) TestHits++;
            var s = Settings.Current;
            if (s == null || !s.LogEnabled) return;
            string key = (a.StringId ?? "") + "|" + (b.StringId ?? "") + "|" + pk;
            if (!_dSaid.Add(key)) return;
            Log.Info(Tag + "dzien " + (int)CampaignTime.Now.ToDays + " | " + Name(a) + " -> " + Name(b) + ": pokoj ZABLOKOWANY (droga: " + path + ") - wojna kanonu trwa do Krwawych Godow ("
                     + why + ", " + Rules[idx].Label + ").");
        }

        /// <summary>Klucz drogi do licznikow: tekst do pierwszego nawiasu (wnioskodawca i wolajacy tylko w linii).</summary>
        private static string PathKey(string path)
        {
            if (string.IsNullOrEmpty(path)) return "?";
            int p = path.IndexOf(" (", StringComparison.Ordinal);
            return p > 0 ? path.Substring(0, p) : path;
        }

        // ------------------------------------------------------------ latki

        private static bool _hooksTried;
        private static Harmony _harmony;
        private static string _wH1 = "BRAK", _wH2 = "BRAK", _wH3 = "BRAK", _wAi = "BRAK";
        internal static MethodInfo DipApplyPeace;   // autotest T1
        internal static void SetHarmony(Harmony h) { _harmony = h; }

        /// <summary>Raz na proces, w kampanii (ArmouryBehavior obok PeaceHold.EnsureHooks): H1-H3 i mysl AI CanonWarWatch; linia startu przy kazdej sesji.</summary>
        internal static void EnsureHooks()
        {
            if (Campaign.Current == null) return;
            if (!_hooksTried && _harmony != null)
            {
                _hooksTried = true;
                try
                {
                    var t = AccessTools.TypeByName("Diplomacy.DiplomaticAction.WarPeace.KingdomPeaceAction");
                    MethodInfo m = null;
                    if (t != null)
                        foreach (var x in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                        {
                            if (x.Name != "ApplyPeace") continue;
                            var ps = x.GetParameters();
                            if (ps.Length == 6 && ps[0].ParameterType == typeof(Kingdom) && ps[1].ParameterType == typeof(Kingdom) && ps[2].ParameterType == typeof(int?)
                                && ps[3].ParameterType == typeof(bool) && ps[4].ParameterType == typeof(bool) && ps[5].ParameterType == typeof(bool)) { m = x; break; }
                        }
                    if (m == null) _wH1 = t == null ? "BRAK (brak Diplomacy KingdomPeaceAction)" : "BRAK (sygnatura ApplyPeace)";
                    else
                    {
                        _harmony.Patch(m, prefix: new HarmonyMethod(typeof(CanonPeaceLock), nameof(DipApplyPeacePrefix)) { priority = Priority.First });
                        DipApplyPeace = m; _wH1 = "TAK";
                    }
                }
                catch (Exception e) { _wH1 = "BLAD (" + e.GetType().Name + ")"; Stumble("EnsureHooks.H1", e); }
                try
                {
                    var m = AccessTools.DeclaredMethod(typeof(Kingdom), "AddDecision", new[] { typeof(KingdomDecision), typeof(bool) });
                    if (m == null) _wH2 = "BRAK";
                    else { _harmony.Patch(m, prefix: new HarmonyMethod(typeof(CanonPeaceLock), nameof(AddDecisionPrefix)) { priority = Priority.First }); _wH2 = "TAK"; }
                }
                catch (Exception e) { _wH2 = "BLAD (" + e.GetType().Name + ")"; Stumble("EnsureHooks.H2", e); }
                try
                {
                    var m = AccessTools.DeclaredMethod(typeof(MakePeaceKingdomDecision), "IsAllowed", Type.EmptyTypes);
                    if (m == null) _wH3 = "BRAK";
                    else { _harmony.Patch(m, postfix: new HarmonyMethod(typeof(CanonPeaceLock), nameof(IsAllowedPostfix))); _wH3 = "TAK"; }
                }
                catch (Exception e) { _wH3 = "BLAD (" + e.GetType().Name + ")"; Stumble("EnsureHooks.H3", e); }
                _wAi = CanonWarWatch.Install(_harmony);
            }
            try
            {
                Log.Info(Tag + "latki - Diplomacy ApplyPeace " + _wH1 + ", AddDecision " + _wH2 + ", IsAllowed " + _wH3 + ", mysl AI " + _wAi
                         + "; klucz Canon War No Peace Until Red Wedding " + (KeyOn ? "tak" : "nie") + " | zakres: " + (Prologue.CanonHere ? "kampania kanonu" : "kampania klasyczna - nie dotyczy")
                         + " | pary: " + RulesText() + "." + TestStartText());
            }
            catch (Exception e) { Stumble("StartLine", e); }
        }

        private static string RulesText()
        {
            var l = new List<string>();
            foreach (var r in Rules) l.Add(r.Label + " (" + r.A + "-" + r.B + ", do " + r.EndEvent + " albo smierci " + r.EndHero + ")");
            return string.Join(", ", l.ToArray());
        }

        /// <summary>Droga H1 ze stosu (tylko przy blokadzie - rzadko): zmeczenie wojna, wynik glosowania, wojna domowa, ekran; inaczej pierwsze ramki.</summary>
        private static string DipPath()
        {
            try
            {
                var st = new StackTrace(2, false);
                string first = null;
                for (int i = 0; i < st.FrameCount && i < 24; i++)
                {
                    var m = st.GetFrame(i)?.GetMethod();
                    if (m == null) continue;
                    string n = m.Name ?? "", t = m.DeclaringType != null ? m.DeclaringType.FullName ?? "" : "";
                    if (n.IndexOf("ConsiderPeaceActions", StringComparison.Ordinal) >= 0 || t.IndexOf("WarExhaustion", StringComparison.Ordinal) >= 0) return "Diplomacy zmeczenie wojna";
                    if (t.IndexOf("MakePeaceKingdomDecision", StringComparison.Ordinal) >= 0 || n.IndexOf("ApplyChosenOutcome", StringComparison.Ordinal) >= 0) return "decyzja krolestwa (wynik glosowania, Diplomacy)";
                    if (t.IndexOf("CivilWar", StringComparison.Ordinal) >= 0) return "Diplomacy wojna domowa";
                    if (t.IndexOf("ViewModel", StringComparison.Ordinal) >= 0 || t.EndsWith("VM", StringComparison.Ordinal)) return "ekran krolestwa (Diplomacy)";
                    if (first == null && m.DeclaringType != null && m.DeclaringType != typeof(CanonPeaceLock)) first = m.DeclaringType.Name + "." + n;
                }
                return "Diplomacy ApplyPeace (wola: " + (first ?? "?") + ")";
            }
            catch { return "Diplomacy ApplyPeace"; }
        }

        /// <summary>H1: prefiks First Diplomacy KingdomPeaceAction.ApplyPeace - para z blokada: false (nic dalej - koszt, lenna, okno, pokoj). Nie rzuca.</summary>
        public static bool DipApplyPeacePrefix(Kingdom __0, Kingdom __1)
        {
            try
            {
                int i; string why;
                if (!State(__0, __1, out i, out why)) return true;
                Note(__0, __1, i, why, DipPath());
                return false;
            }
            catch (Exception e) { Stumble("DipApplyPeacePrefix", e); return true; }
        }

        /// <summary>H2: prefiks First Kingdom.AddDecision - MakePeaceKingdomDecision pary z blokada: false przed kosztem wplywow i wyborami. Nie rzuca.</summary>
        public static bool AddDecisionPrefix(Kingdom __instance, KingdomDecision __0)
        {
            try
            {
                var d = __0 as MakePeaceKingdomDecision;
                if (d == null) return true;
                var k = d.Kingdom ?? __instance;
                var other = d.FactionToMakePeaceWith;
                int i; string why;
                if (!State(k, other, out i, out why)) return true;
                var who = d.ProposerClan;
                Note(k, other, i, why, "decyzja krolestwa (AddDecision, wnioskodawca " + (who != null && who.Name != null ? who.Name.ToString() : "-") + ")");
                if (who != null && who == Clan.PlayerClan && !_playerTold)
                {
                    _playerTold = true;
                    bool crownSide = k.StringId == Prologue.Crown;
                    Log.Player(crownSide ? "The crown will hear no talk of peace with the rebels while Robb Stark lives." : "King Robb will hear no talk of peace with the crown while the war goes on.", true);
                }
                return false;
            }
            catch (Exception e) { Stumble("AddDecisionPrefix", e); return true; }
        }

        /// <summary>H3: postfiks MakePeaceKingdomDecision.IsAllowed - para z blokada: false (gra anuluje czekajaca decyzje). Nie rzuca.</summary>
        public static void IsAllowedPostfix(MakePeaceKingdomDecision __instance, ref bool __result)
        {
            if (!__result || __instance == null) return;
            try
            {
                int i; string why;
                if (!State(__instance.Kingdom, __instance.FactionToMakePeaceWith, out i, out why)) return;
                __result = false;
                Note(__instance.Kingdom, __instance.FactionToMakePeaceWith, i, why, "decyzja w toku (IsAllowed)");
            }
            catch (Exception e) { Stumble("IsAllowedPostfix", e); }
        }

        // ------------------------------------------------------------ doba

        /// <summary>Linia doby (ArmouryBehavior, po PeaceHold.Daily) i linie CanonWarWatch; zeruje liczniki doby.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            try
            {
                if (s.LogEnabled) Log.Info(DayLine());
            }
            catch (Exception e) { Stumble("Daily", e); }
            try { CanonWarWatch.Daily(); } catch (Exception e) { Stumble("CanonWarWatch.Daily", e); }
            _dBlocked = 0; _dStumbles = 0; _dPaths.Clear(); _dSaid.Clear();
        }

        private static string DayLine()
        {
            var sb = new StringBuilder(400);
            sb.Append(Tag).Append("dzien ").Append((int)CampaignTime.Now.ToDays).Append(" | ");
            var parts = new List<string>();
            bool any = false, player = false;
            string global = null;
            for (int i = 0; i < Rules.Length; i++)
            {
                var r = Rules[i];
                var a = Prologue.KingdomById(r.A); var b = Prologue.KingdomById(r.B);
                string why;
                bool on;
                if (a == null || b == null) { on = false; why = "brak krolestwa (" + (a == null ? r.A : r.B) + ")"; }
                else
                {
                    int idx;
                    on = State(a, b, out idx, out why);
                    if (Prologue.RulesRealm(a) || Prologue.RulesRealm(b)) player = true;
                }
                if (on) any = true;
                if (global == null || on) global = why;
                bool war = a != null && b != null && !a.IsEliminated && !b.IsEliminated && a.IsAtWarWith(b);
                string since = war ? Prologue.WarSinceText(a, b, r.A == Prologue.RiverId || r.B == Prologue.RiverId ? Prologue.RiverFirstDay : Prologue.NoDayValue) : "";
                parts.Add(r.Label + ": " + (on ? "CZYNNA" : "KONIEC") + ", wojna " + (war ? "TAK " + since : "NIE"));
            }
            if (!KeyOn) sb.Append("blokada WYLACZONA (klucz NIE)");
            else if (!Prologue.CanonHere) sb.Append("blokada NIE DOTYCZY (kampania klasyczna)");
            else sb.Append("blokada ").Append(any ? "CZYNNA" : "KONIEC").Append(" (").Append(global ?? "-").Append(_cacheRobbMissing ? "; brak bohatera Robba " + Prologue.RobbId : "").Append(')');
            foreach (var p in parts) sb.Append(" | ").Append(p);
            sb.Append(" | zablokowane dzis ").Append(_dBlocked);
            if (_dPaths.Count > 0)
            {
                var l = new List<string>();
                foreach (var kv in _dPaths) l.Add(kv.Key + " " + kv.Value);
                sb.Append(" [").Append(string.Join(", ", l.ToArray())).Append(']');
            }
            sb.Append(", sesja ").Append(_sBlocked).Append(" | gracz rzadzi: ").Append(player ? "tak" : "nie").Append(" | potkniecia ").Append(_dStumbles).Append(" (sesja ").Append(_stumbles).Append(").");
            return sb.ToString();
        }

        // ------------------------------------------------------------ autotest (CanonPeaceLock.Test.cs)
        internal static bool TestArmed;      // autotest: liczy blokady w czasie proby
        internal static int TestHits;
        [ThreadStatic] internal static bool TestDirect;   // autotest T6: bezposredni pokoj Kronika-podobny (WorldClock7 - dopisek "test 264")
        private static string TestStartText() { return CanonPeaceLockTest.StartText(); }
    }
}
