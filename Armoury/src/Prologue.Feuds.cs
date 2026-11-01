using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace Armoury
{
    /// <summary>
    /// 312b - KONCE ZATARGOW ROT (Jeff 01.11, decyzja 1: "tak, ale pamietaj, ze kazdy chce byc i zostac krolem"; projekt rozp312/wojny-projekt.md rozdz. A,
    /// poprawki krytyka wojny-krytyk.md). Klucze CanonFeudWarsEnd i CanonFeudWarMinDays. Linie "Zatargi ROT (312b): ...", progi tools/sprawdz_logi.py --grupa zatargi312b.
    ///
    /// CO: ROT ROTStorylineWars wymusza 10 starych zatargow (StartDay 5, EndDay 100, bez wyzwalacza; po rozciagnieciu osi Fabuly CrashScribe x4.33 dni 22-433):
    ///     Vale-Iron Islands, Reach-Dorne, 6 par Wolnych Miast / Essos, Qarth-Daenerys, Dothrakowie-Daenerys. Do EndDay ROT blokuje pokoj (model zgod ROT
    ///     "Your sovereign will not entertain peace", IsWarForced: PovertyPeace, StorylineReparations, PeaceHold) i po pokoju ze zmeczenia wypowiada wojne od nowa
    ///     nastepnej doby (EnforceWars; E7/0 dzien 108879 "NOWA WOJNA &lt; 21 DNI PO POKOJU").
    ///     Teraz: gdy wojna zatargu trwa >= CanonFeudWarMinDays (91) od StanceLink.WarStartDate, para dostaje ZATRZASK (_feudFree, zapis "fz=" w arm_m104) i od tej
    ///     chwili ROTStorylineWar.Enforced zwraca dla niej false - pokoj z glosu, zmeczenia albo biedy jest mozliwy, a ROT go nie cofa.
    /// KTORE: regula ROT !HasTrigger &amp;&amp; EndDay &gt; 0 &amp;&amp; !IsCustom ORAZ para z listy FeudPairs (w dowolnej kolejnosci stron); sturgia-battania (Iron Islands - North,
    ///     kanon 100-235) jawnie wykluczona. Liczb StartDay/EndDay nie czytamy (WarPrefix Fabuli rozciaga je na czas wywolania) - tylko EndDay &gt; 0.
    /// WARUNEK DATY (krytyk LUKI 6): WarStartDate != CampaignTime.Zero (stance bez daty nigdy nie zwalnia - "Now - Zero" zwolniloby od razu); linia startu liczy
    ///     pary "wojna bez daty" i "wojna sprzed startu opowiesci" (pomiar zamiast zalozenia).
    /// ZATARG ZAKONCZONY ZMECZENIEM DIPLOMACY PRZED 91. DNIEM (krytyk LUKI 7): bez zatrzasku ROT wznowi go nastepnej doby - jak dotad (stan obecny, nie blad 312b).
    /// JAK: postfiks Priority.Last na tym samym MethodInfo co Prologue._mWEnforced (AccessTools.DeclaredMethod - Prologue.Wars RotWarsReady), wpinany z
    ///     CanonWarsBehavior.OnSessionLaunched raz na proces, w kazdej kampanii (InstallEssos tylko w kampanii z prologiem). Rozlaczny z postfiksem Dorzecza m116
    ///     (tylko korona-Dorzecze). Doba (FeudDaily z WarsDaily): Enforced wolane dla zatargow bez zatrzasku (jak IsWarForced) - zatrzask tego samego dnia, linia doby.
    /// Bledy: per wywolanie, liczone (FzStumble, "ERROR in CanonFeuds.*"), bez globalnego wylacznika.
    /// </summary>
    internal static partial class Prologue
    {
        internal const string FzTag = "Zatargi ROT (312b): ";
        // tekst w oknie ROT "Edit Storyline Wars" (powod, czemu wojna nie jest juz wymuszona) - po angielsku
        internal const string FeudText = "This old feud has run its course: the war goes on only while both realms want it.";

        // 10 zatargow ROT (ROTStorylineWars.OnNewGameCreated: StartDay 5, EndDay 100, bez wyzwalacza) - id ROT.Misc.ROTKingdoms, kolejnosc stron jak w ROT
        internal static readonly string[] FeudPairs =
        {
            "empire-sturgia", "empire_w-aserai", "bravos-nord", "tyrosh-lys", "pentos-myr", "norvos-qohor", "volantis-valyrian", "qarth-empire_s", "khuzait-empire_s", "sarnor-ibb"
        };
        internal const string FeudExcluded = "sturgia-battania";   // Iron Islands - North (100, 235): kanon, ma ruszyc i skonczyc sie o czasie ROT

        private static bool FeudKey { get { var s = S; return s != null && s.CanonFeudWarsEnd; } }
        private static int FeudMinDays { get { var s = S; int d = s != null ? s.CanonFeudWarMinDays : 91; return d < 1 ? 1 : d > 364 ? 364 : d; } }

        // ---------------------------------------------------------------- stan
        private static readonly Dictionary<string, int> _feudFree = new Dictionary<string, int>(StringComparer.Ordinal);   // para -> dzien kampanii zwolnienia (zapis)
        private static readonly List<string> _fzFreedToday = new List<string>();
        private static readonly List<string> _fzPeaceToday = new List<string>();
        private static readonly List<string> _fzPeaceAll = new List<string>();     // od wczytania (bez zapisu)
        private static readonly List<string> _fzWarToday = new List<string>();
        private static int _fzPeaceS, _fzRotS, _fzRotDay, _fzNewWarS, _fzNewWarDay;
        private static int _fzStumbles, _fzStumblesDay;

        internal static void FzStumble(string where, Exception e)
        {
            _fzStumbles++; _fzStumblesDay++;
            if (_fzStumbles <= 5) Log.Error("CanonFeuds." + where + " (potkniecie " + _fzStumbles + ")", e);
        }

        /// <summary>ResetWars (OnGameStart kazdej kampanii i przed UnpackWars): stan 312b poprzedniej kampanii w procesie zerowany.</summary>
        private static void ResetFeuds()
        {
            _feudFree.Clear();
            _fzFreedToday.Clear(); _fzPeaceToday.Clear(); _fzPeaceAll.Clear(); _fzWarToday.Clear();
            _fzPeaceS = _fzRotS = _fzRotDay = _fzNewWarS = _fzNewWarDay = 0;
            _fzStumblesDay = 0;
        }

        /// <summary>Segment zapisu "fz=para@dzien,para@dzien;" (puste = "fz=;"). Starsza DLL pomija nieznany klucz (UnpackWars) - gubi tylko zatrzask.</summary>
        private static string PackFeuds()
        {
            var l = new List<string>();
            foreach (var kv in _feudFree) l.Add(kv.Key + "@" + kv.Value.ToString(Inv));
            return "fz=" + string.Join(",", l.ToArray()) + ";";
        }

        /// <summary>Odporne na brak segmentu (zapis sprzed 312b - brak zatrzaskow) i na zle wpisy (pomijane): tylko pary z listy FeudPairs.</summary>
        private static void UnpackFeuds(Dictionary<string, string> map)
        {
            _feudFree.Clear();
            string v;
            if (map == null || !map.TryGetValue("fz", out v) || string.IsNullOrEmpty(v)) return;
            foreach (var item in v.Split(','))
            {
                int at = item.IndexOf('@');
                if (at <= 0) continue;
                string key = item.Substring(0, at);
                int day;
                if (Array.IndexOf(FeudPairs, key) < 0) continue;
                if (!int.TryParse(item.Substring(at + 1), System.Globalization.NumberStyles.Integer, Inv, out day)) continue;
                _feudFree[key] = day;
            }
        }

        // ---------------------------------------------------------------- rozpoznanie zatargu
        private static bool _fzPropsTried;
        private static PropertyInfo _pFzHasTrig, _pFzEndDay, _pFzCustom;

        private static bool FeudPropsReady()
        {
            if (!_fzPropsTried)
            {
                _fzPropsTried = true;
                try
                {
                    var tWar = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTStorylineWar");
                    if (tWar != null)
                    {
                        _pFzHasTrig = AccessTools.Property(tWar, "HasTrigger");
                        _pFzEndDay = AccessTools.Property(tWar, "EndDay");
                        _pFzCustom = AccessTools.Property(tWar, "IsCustom");
                    }
                }
                catch (Exception e) { FzStumble("FeudPropsReady", e); }
            }
            return _pFzHasTrig != null && _pFzEndDay != null && _pFzCustom != null;
        }

        /// <summary>Klucz pary z listy FeudPairs (kolejnosc z listy) dla dwoch id; null - para spoza listy (w tym sturgia-battania).</summary>
        internal static string FeudKeyOf(string a, string b)
        {
            if (a == null || b == null) return null;
            string k1 = a + "-" + b, k2 = b + "-" + a;
            if (k1 == FeudExcluded || k2 == FeudExcluded) return null;
            if (Array.IndexOf(FeudPairs, k1) >= 0) return k1;
            if (Array.IndexOf(FeudPairs, k2) >= 0) return k2;
            return null;
        }

        // strony wpisu ROT sie nie zmieniaja (prywatny setter) - klucz pary w pamieci na obiekt ("" = spoza listy)
        private static readonly ConditionalWeakTable<object, string> _fzPairOf = new ConditionalWeakTable<object, string>();
        private static readonly ConditionalWeakTable<object, string>.CreateValueCallback _fzPairCalc = FeudPairCalc;

        private static string FeudPairCalc(object war)
        {
            try
            {
                var a = WF1(war) as Kingdom; var b = WF2(war) as Kingdom;
                if (a == null || b == null) return "";
                return FeudKeyOf(a.StringId, b.StringId) ?? "";
            }
            catch { return ""; }
        }

        /// <summary>Klucz zatargu dla wpisu ROT albo null: para z listy i regula ROT (bez wyzwalacza, EndDay &gt; 0, nie wlasna gracza) - EndDay czytany na zywo.</summary>
        private static string FeudPairOf(object war)
        {
            if (war == null || !RotWarsReady() || !FeudPropsReady()) return null;
            string key = _fzPairOf.GetValue(war, _fzPairCalc);
            if (string.IsNullOrEmpty(key)) return null;
            if ((bool)_pFzHasTrig.GetValue(war, null) || (bool)_pFzCustom.GetValue(war, null)) return null;
            if ((int)_pFzEndDay.GetValue(war, null) <= 0) return null;
            return key;
        }

        private static string FName(IFaction f) { try { return f != null && f.Name != null ? f.Name.ToString() : "?"; } catch { return "?"; } }

        private static string PairNames(object war) { return FName(WF1(war)) + " - " + FName(WF2(war)); }

        // ---------------------------------------------------------------- latka
        private static bool _fzTried;
        private static string _fzWire = "nie wpieta";

        /// <summary>CanonWarsBehavior.OnSessionLaunched: raz na proces (w kazdej kampanii) postfiks Priority.Last na ROTStorylineWar.Enforced.</summary>
        internal static void InstallFeuds()
        {
            if (_fzTried) return;
            _fzTried = true;
            try
            {
                if (!RotWarsReady()) { _fzWire = "BRAK (ROT ROTStorylineWar.Enforced / StorylineWars)"; return; }
                if (!FeudPropsReady()) { _fzWire = "BRAK (ROTStorylineWar HasTrigger/EndDay/IsCustom)"; return; }
                var h = new Harmony("com.jeff.armoury.feuds312b");
                var r = h.Patch(_mWEnforced, postfix: new HarmonyMethod(typeof(Prologue), nameof(FeudEnforcedPostfix)) { priority = Priority.Last });
                _fzWire = r != null ? "TAK" : "BRAK";
            }
            catch (Exception e) { FzStumble("InstallFeuds", e); _fzWire = "BLAD (" + e.GetType().Name + ")"; }
        }

        /// <summary>Postfiks ROTStorylineWar.Enforced (Priority.Last): zatarg z zatrzaskiem - false z naszym powodem; zatarg wymuszony przez ROT, ktorego wojna trwa
        /// >= CanonFeudWarMinDays od WarStartDate (!= Zero) - zatrzask i false. Reszta wojen ROT bez zmian. Nie rzuca.</summary>
        public static void FeudEnforcedPostfix(object __instance, ref bool __result, ref string disabledReason)
        {
            try
            {
                if (__instance == null || !FeudKey) return;
                string key = FeudPairOf(__instance);
                if (key == null) return;
                if (_feudFree.ContainsKey(key)) { __result = false; disabledReason = FeudText; return; }
                if (!__result) return;   // przed StartDay / po EndDay / wylaczona w oknie ROT / strona zniszczona - jak ROT
                var f1 = WF1(__instance); var f2 = WF2(__instance);
                if (f1 == null || f2 == null || f1.IsEliminated || f2.IsEliminated || !FactionManager.IsAtWarAgainstFaction(f1, f2)) return;
                var link = f1.GetStanceWith(f2);
                if (link == null) return;
                var ws = link.WarStartDate;
                if (ws == CampaignTime.Zero) return;   // stance bez daty - nigdy nie zwalnia (krytyk LUKI 6)
                double dur = (CampaignTime.Now - ws).ToDays;
                int min = FeudMinDays;
                if (dur < min) return;
                int day = (int)CampaignTime.Now.ToDays;
                _feudFree[key] = day;
                _fzFreedToday.Add(key);
                __result = false;
                disabledReason = FeudText;
                Log.Info(FzTag + "dzien " + day + " | ZWOLNIONY zatarg " + key + " (" + PairNames(__instance) + "): wojna od dnia " + ((int)ws.ToDays).ToString(Inv)
                         + " trwa " + dur.ToString("0.0", Inv) + " dni >= " + min + " - pokoj mozliwy (glos rady, zmeczenie Diplomacy, bieda), ROT nie wypowie tej wojny od nowa.");
            }
            catch (Exception e) { FzStumble("FeudEnforcedPostfix", e); }
        }

        // ---------------------------------------------------------------- zdarzenia (CanonWarsBehavior.RegisterEvents)

        private static string FreedKey(IFaction a, IFaction b)
        {
            if (a == null || b == null || !a.IsKingdomFaction || !b.IsKingdomFaction) return null;
            string key = FeudKeyOf(a.StringId, b.StringId);
            return key != null && _feudFree.ContainsKey(key) ? key : null;
        }

        /// <summary>CampaignEvents.MakePeace: pokoj w zwolnionym zatargu - droga (zmeczenie Diplomacy / bieda / Kronika ze stosu, glos rady z detail).</summary>
        internal static void FeudOnPeace(IFaction a, IFaction b, MakePeaceAction.MakePeaceDetail detail)
        {
            try
            {
                string key = FreedKey(a, b);
                if (key == null) return;
                int day = (int)CampaignTime.Now.ToDays;
                string path = PeacePath(detail);
                string s = key + " dzien " + day + " " + path;
                _fzPeaceToday.Add(s);
                if (_fzPeaceAll.Count < 40) _fzPeaceAll.Add(s);
                _fzPeaceS++;
                Log.Info(FzTag + "dzien " + day + " | POKOJ w zwolnionym zatargu " + key + " (" + FName(a) + " - " + FName(b) + "), droga: " + path + ".");
            }
            catch (Exception e) { FzStumble("FeudOnPeace", e); }
        }

        /// <summary>CampaignEvents.WarDeclared: wojna w zwolnionym zatargu - ROT (CallingDeclareWar; sprawdzian, ma byc 0) albo inna droga (AI, wezwanie, gracz).</summary>
        internal static void FeudOnWar(IFaction a, IFaction b, DeclareWarAction.DeclareWarDetail detail)
        {
            try
            {
                string key = FreedKey(a, b);
                if (key == null) return;
                int day = (int)CampaignTime.Now.ToDays;
                bool rot = false;
                try { var beh = RotWarsBehavior(); rot = _fCalling != null && beh != null && (bool)_fCalling.GetValue(beh); } catch { rot = false; }
                if (rot)
                {
                    _fzRotDay++; _fzRotS++;
                    Log.Info(FzTag + "dzien " + day + " | WOJNA w zwolnionym zatargu " + key + " (" + FName(a) + " - " + FName(b) + "): ROT WZNOWIL (CallingDeclareWar) - BLAD 312b.");
                }
                else
                {
                    _fzNewWarDay++; _fzNewWarS++;
                    if (_fzWarToday.Count < 10) _fzWarToday.Add(key + " " + detail);
                    Log.Info(FzTag + "dzien " + day + " | WOJNA w zwolnionym zatargu " + key + " (" + FName(a) + " - " + FName(b) + "): nowa wojna, nie ROT (" + detail + ").");
                }
            }
            catch (Exception e) { FzStumble("FeudOnWar", e); }
        }

        /// <summary>Droga pokoju ze stosu (zdarzenie rzadkie - tylko zwolnione zatargi).</summary>
        private static string PeacePath(MakePeaceAction.MakePeaceDetail detail)
        {
            try
            {
                var st = new StackTrace(2, false);
                for (int i = 0; i < st.FrameCount && i < 40; i++)
                {
                    var m = st.GetFrame(i)?.GetMethod();
                    if (m == null || m.DeclaringType == null) continue;
                    string n = m.Name ?? "", t = m.DeclaringType.FullName ?? "";
                    if (n.IndexOf("ConsiderPeaceActions", StringComparison.Ordinal) >= 0 || t.IndexOf("WarExhaustion", StringComparison.Ordinal) >= 0) return "zmeczenie Diplomacy";
                    if (t.IndexOf("PovertyPeace", StringComparison.Ordinal) >= 0) return "bieda (CrashScribe)";
                    if (t.IndexOf("Chronicle", StringComparison.Ordinal) >= 0) return "Kronika";
                }
            }
            catch { }
            return detail == MakePeaceAction.MakePeaceDetail.ByKingdomDecision ? "glos rady" : "inna (" + detail + ")";
        }

        // ---------------------------------------------------------------- doba i linie

        /// <summary>WarsDaily: Enforced dla zatargow bez zatrzasku (zatrzask tego samego dnia, jak przy IsWarForced), potem linia doby. Zeruje liczniki doby.</summary>
        internal static void FeudDaily()
        {
            if (_fzWire != "TAK") { ResetFeudDay(); return; }
            if (FeudKey)
            {
                foreach (var war in RotWars())
                {
                    try
                    {
                        string key = FeudPairOf(war);
                        if (key == null || _feudFree.ContainsKey(key)) continue;
                        var f1 = WF1(war); var f2 = WF2(war);
                        if (f1 == null || f2 == null || f1.IsEliminated || f2.IsEliminated) continue;
                        _mWEnforced.Invoke(war, new object[] { null });
                    }
                    catch (Exception e) { FzStumble("FeudDaily", e); }
                }
            }
            Log.Info(FeudDayLine());
            ResetFeudDay();
        }

        private static void ResetFeudDay()
        {
            _fzFreedToday.Clear(); _fzPeaceToday.Clear(); _fzWarToday.Clear();
            _fzRotDay = 0; _fzNewWarDay = 0; _fzStumblesDay = 0;
        }

        private static string FreedAllText()
        {
            var l = new List<string>();
            foreach (var kv in _feudFree) l.Add(kv.Key + "@" + kv.Value.ToString(Inv));
            return _feudFree.Count + (l.Count > 0 ? " [" + string.Join(", ", l.ToArray()) + "]" : "");
        }

        private static string FeudDayLine()
        {
            var sb = new StringBuilder(400);
            sb.Append(FzTag).Append("doba | dzien ").Append((int)CampaignTime.Now.ToDays)
              .Append(" | klucz ").Append(FeudKey ? "TAK" : "NIE").Append(", min ").Append(FeudMinDays).Append(" dni")
              .Append(" | zwolnione dzis ").Append(_fzFreedToday.Count);
            if (_fzFreedToday.Count > 0) sb.Append(" [").Append(string.Join(", ", _fzFreedToday.ToArray())).Append(']');
            sb.Append(", razem ").Append(FreedAllText())
              .Append(" | pokoje po zwolnieniu dzis ").Append(_fzPeaceToday.Count);
            if (_fzPeaceToday.Count > 0) sb.Append(" [").Append(string.Join("; ", _fzPeaceToday.ToArray())).Append(']');
            sb.Append(", od wczytania ").Append(_fzPeaceS);
            if (_fzPeaceAll.Count > 0) sb.Append(" [").Append(string.Join("; ", _fzPeaceAll.ToArray())).Append(']');
            sb.Append(" | ROT wznowil zwolniony dzis ").Append(_fzRotDay).Append(", od wczytania ").Append(_fzRotS)
              .Append(" | nowe wojny w zwolnionych parach (nie ROT) dzis ").Append(_fzNewWarDay);
            if (_fzWarToday.Count > 0) sb.Append(" [").Append(string.Join(", ", _fzWarToday.ToArray())).Append(']');
            sb.Append(", od wczytania ").Append(_fzNewWarS)
              .Append(" | potkniecia dzis ").Append(_fzStumblesDay).Append(" (razem ").Append(_fzStumbles).Append(").");
            return sb.ToString();
        }

        /// <summary>CanonWarsBehavior.OnSessionLaunched (po InstallFeuds): latka, klucze, 10 zatargow ze stanem, pary bez daty / sprzed startu opowiesci.</summary>
        internal static void FeudStartLine()
        {
            var sb = new StringBuilder(1200);
            sb.Append(FzTag).Append("start sesji | latka Enforced (zatargi) ").Append(_fzWire)
              .Append(" | klucze: Old Feuds Can End ").Append(FeudKey ? "TAK" : "NIE").Append(", Feud War Min Days ").Append(FeudMinDays);
            int noDate = 0, beforeStory = 0;
            var items = new List<string>();
            var outside = new List<string>();
            if (RotWarsReady() && FeudPropsReady())
            {
                var byKey = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (var war in RotWars())
                {
                    try
                    {
                        string key = FeudPairOf(war);
                        if (key != null) { if (!byKey.ContainsKey(key)) byKey[key] = war; continue; }
                        // regula ROT bez listy (diagnoza: inny zatarg w zapisie) - poza sturgia-battania i wojnami gracza
                        if ((bool)_pFzHasTrig.GetValue(war, null) || (bool)_pFzCustom.GetValue(war, null) || (int)_pFzEndDay.GetValue(war, null) <= 0) continue;
                        var a = WF1(war); var b = WF2(war);
                        string p = (a != null ? a.StringId : "?") + "-" + (b != null ? b.StringId : "?");
                        if (p != FeudExcluded) outside.Add(p);
                    }
                    catch (Exception e) { FzStumble("FeudStartLine.scan", e); }
                }
                CampaignTime story;
                try { story = StoryStart(); } catch { story = CampaignTime.Zero; }
                foreach (var key in FeudPairs)
                {
                    try
                    {
                        object war;
                        if (!byKey.TryGetValue(key, out war)) { items.Add(key + ": brak w ROT" + (_feudFree.ContainsKey(key) ? " (zatrzask od dnia " + _feudFree[key] + ")" : "")); continue; }
                        var f1 = WF1(war); var f2 = WF2(war);
                        string st;
                        if (f1 == null || f2 == null || f1.IsEliminated || f2.IsEliminated) st = "strona zniszczona";
                        else
                        {
                            bool at = FactionManager.IsAtWarAgainstFaction(f1, f2);
                            string since = "";
                            if (at)
                            {
                                var link = f1.GetStanceWith(f2);
                                var ws = link != null ? link.WarStartDate : CampaignTime.Zero;
                                if (ws == CampaignTime.Zero) { noDate++; since = " bez daty"; }
                                else
                                {
                                    if (ws < story) beforeStory++;
                                    since = " od dnia " + ((int)ws.ToDays).ToString(Inv) + " (" + (CampaignTime.Now - ws).ToDays.ToString("0.0", Inv) + " dni)";
                                }
                            }
                            var args = new object[] { null };
                            bool forced = (bool)_mWEnforced.Invoke(war, args);   // te same latki co ROT; moze ustawic zatrzask (linia ZWOLNIONY wyzej)
                            string free = _feudFree.ContainsKey(key) ? "ZWOLNIONY od dnia " + _feudFree[key] : forced ? "ROT wymusza" : "ROT nie wymusza (" + (args[0] as string ?? "") + ")";
                            st = "wojna " + (at ? "TAK" + since : "NIE") + ", " + free;
                        }
                        items.Add(key + " (" + PairNames(war) + "): " + st);
                    }
                    catch (Exception e) { FzStumble("FeudStartLine.pair", e); items.Add(key + ": ?"); }
                }
            }
            else items.Add("ROT nieobecny");
            sb.Append(" | zatargi ").Append(FeudPairs.Length).Append(": ").Append(string.Join("; ", items.ToArray()))
              .Append(" | wojna bez daty (WarStartDate Zero) ").Append(noDate).Append(", wojna sprzed startu opowiesci ").Append(beforeStory)
              .Append(" | wykluczona: ").Append(FeudExcluded).Append(" (kanon)")
              .Append(" | poza lista (regula ROT bez wyzwalacza, EndDay > 0): ").Append(outside.Count > 0 ? string.Join(", ", outside.ToArray()) : "-")
              .Append(" | zwolnione w zapisie ").Append(FreedAllText())
              .Append(" | potkniecia ").Append(_fzStumbles).Append('.');
            Log.Info(sb.ToString());
        }
    }
}
