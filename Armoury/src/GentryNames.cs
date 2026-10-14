using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// Trop gentry z audytu modow 14.10 (RAPORT-AUDYT-MODOW.md rozdz. 7) - POTWIERDZONY w kodzie i w tabeli biegu dwuletniego. Klucz GentryNamesFallback.
    ///
    /// BK BKGentryBehavior.InitializeGentry (:431-476) - nowy rod gentry dostaje nazwe z ClanActions.CanCreateNewClan -> GetRandomAvailableName (:143-170):
    /// PIERWSZA nazwa z listy kultury (CultureObject.ClanNameList), ktorej nie nosi rod TEJ SAMEJ kultury (IsAvailable patrzy tylko na rody tej kultury);
    /// potem gra (FactionHelper.IsClanNameApplicable :107-131) odrzuca nazwe noszona przez JAKIKOLWIEK rod. ROT 8.1.8 (ROT-Content spcultures.xml) daje
    /// jedna liste wielu kulturom: empire (Braavos), pentoshi, lyseni, myrish, tyroshi, norvos, qohorik, volantine, valyrian, nord, summer - 44 nazwy;
    /// vale, river, reach, stormlands, dragonstone, crownlands, nightswatch - 8; sturgia / skagosi / ibbenese; aserai / sarnor; khuzait / yiti;
    /// ghiscari / qartheen. Kultura, ktorej wies pierwsza dostala rod (kolejnosc settlements.xml przy tworzeniu swiata), zabiera pierwsza nazwe listy;
    /// pozostale kultury tej listy dostaja od BK zawsze te sama zajeta nazwe i gentry nie maja nigdy. Male listy wyczerpuja sie (North 5 nazw na 57 wsi,
    /// Free Folk 3, Ghiscari 2, Vale 8, Dorne 26), a Westerlands staje na 11. nazwie ('Darke' - nosi ja rod gentry Vale).
    /// Dowod (TAB budzet-rodow.csv, bieg 06:01, 728 dob): 88 rodow gentry w 9 krolestwach na starcie, 89 po dwoch latach; w kazdej wspolnej liscie gentry
    /// ma tylko kultura pierwszej wsi; Pentoshi 7 = 7 pierwszych nazw listy, Westerlands 10 = nazwy przed 'Darke'; rody rycerzy BK (sciezka bez
    /// IsClanNameApplicable) dostaly 'Bywater' (Dragonstone) i 'Acapanos' (Lorath) - nazwy rodow gentry Vale i Pentos.
    ///
    /// Teraz (postfiks GetRandomAvailableName): pierwsza nazwa z listy kultury wolna w CALYM swiecie (zaden rod jej nie nosi - regula gry; zaden rod tej
    /// kultury nie ma nazwy, ktora ja zawiera - regula BK; zasady nazwy gry: 1-50 znakow, bez znakow specjalnych i podwojnych spacji). Gdy BK dal nazwe
    /// wolna - wynik bez zmian (to ta sama nazwa). Zapas, gdy lista wyczerpana: nazwa wsi, 'nazwa z listy of wies', 'wies kultura' (bez nowych plikow).
    /// Zapas i nazwa zamiast braku - tylko w oknie InitializeGentry (gentry). Inne wywolania (rycerz BK zakladajacy rod, nazwa po rozwiazaniu buntu) -
    /// tylko tam, gdzie BK dal nazwe (zajeta): dostaja wolna; gdzie BK nie dal nazwy (null), zostaje null - liczba rodow rycerzy jak dotad.
    /// Kultury bez wzorca gentry BK (CharacterObject 'bannerkings_gentry_' tej kultury: nord - BKROT ma 'lorathi', ibbenese, qartheen, nightswatch, summer)
    /// dalej bez gentry - tego latka nie zmienia (linia startu je wymienia; raport AM-28: gentry_nord NIE dodawac).
    /// Wylaczone = b47fecc3 (postfiks wraca od razu; okno i licznik nowych rodow to tylko log). Wyjatek per wywolanie - licznik potkniec, wynik BK zostaje.
    /// Linie "Nazwy gentry BK: start" (raz na sesje) i "Nazwy gentry BK: dzien N" (nowe rody gentry wedlug krolestwa, nazwy, krolestwa bez gentry).
    /// </summary>
    internal static class GentryNames
    {
        private const string GentryPrefix = "gentryClan_";
        private const string KnightSuffix = "_knight_clan";
        private const int NameMax = 50;   // gra: FactionHelper.IsFactionNameApplicable :87

        internal static bool On { get { var s = Settings.Current; return s != null && s.GentryNamesFallback; } }

        // okno BK InitializeGentry - prosba o nazwe dla gentry
        private static int _ctxDepth;

        // liczniki nazw: [okres 0 doba / 1 sesja, rodzaj 0 gentry / 1 inne, pozycja]
        // pozycje: 0 prosby, 1 BK dal wolna (bez zmian), 2 z listy zamiast zajetej, 3 z listy zamiast braku, 4 zapas z nazwy wsi, 5 bez nazwy (wynik BK zostaje)
        private const int NReq = 0, NSame = 1, NPoolTaken = 2, NPoolNone = 3, NVillage = 4, NNone = 5, NSlots = 6;
        private static readonly int[,,] _n = new int[2, 2, NSlots];

        // nowe rody gentry (BK ClanActions.CreateNewClan, id "gentryClan_") wedlug krolestwa osady
        private static readonly Dictionary<string, int> _newDay = new Dictionary<string, int>(), _newSess = new Dictionary<string, int>();
        private static int _newDayN, _newSessN;

        private static readonly Dictionary<CultureObject, bool> _tpl = new Dictionary<CultureObject, bool>();
        private static readonly List<string> _wired = new List<string>(), _missing = new List<string>();
        private static string _wiredText = "-", _missingText = "-";
        private static int _wiredN;
        private const int NeedN = 3;
        private static bool _startLogged;
        private static int _stumbles;
        private static readonly HashSet<string> _err = new HashSet<string>();

        /// <summary>Konstruktor ArmouryBehavior (nowa kampania - PRZED tworzeniem gentry w OnCharacterCreationIsOver - i kazde wczytanie).</summary>
        internal static void Reset()
        {
            _ctxDepth = 0;
            Array.Clear(_n, 0, _n.Length);
            _newDay.Clear(); _newSess.Clear(); _newDayN = _newSessN = 0;
            _tpl.Clear();
            _startLogged = false;
            _stumbles = 0; _err.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("GentryNames." + where, e); } catch { }
        }

        private static void Bump(bool gentry, int slot)
        {
            int k = gentry ? 0 : 1;
            _n[0, k, slot]++;
            _n[1, k, slot]++;
        }

        private static void Add(Dictionary<string, int> d, string k)
        {
            int v;
            d.TryGetValue(k, out v);
            d[k] = v + 1;
        }

        private static string NameOf(Clan c)
        {
            var n = c != null ? c.Name : null;
            return n != null ? n.ToString() : null;
        }

        // ------------------------------------------------------------ wpiecia (SubModuleMain - przy starcie gry, PRZED tworzeniem swiata)
        private static void Patch(Harmony h, string label, MethodBase m, string pre, string post, string fin)
        {
            try
            {
                if (h == null) { _missing.Add(label + " (brak Harmony)"); return; }
                if (m == null) { _missing.Add(label); return; }
                h.Patch(m, prefix: pre != null ? new HarmonyMethod(typeof(GentryNames), pre) : null,
                           postfix: post != null ? new HarmonyMethod(typeof(GentryNames), post) { priority = Priority.Last } : null,
                           finalizer: fin != null ? new HarmonyMethod(typeof(GentryNames), fin) { priority = Priority.High } : null);
                _wired.Add(label);
            }
            catch (Exception e) { _missing.Add(label + " (blad: " + e.Message + ")"); }
        }

        internal static void ApplyAll(Harmony h)
        {
            _wired.Clear(); _missing.Clear();
            var ca = AccessTools.TypeByName("BannerKings.Actions.ClanActions");
            var gb = AccessTools.TypeByName("BannerKings.Behaviours.BKGentryBehavior");
            var nameM = ca != null ? AccessTools.Method(ca, "GetRandomAvailableName", new[] { typeof(CultureObject), typeof(Settlement) }) : null;
            if (nameM != null && nameM.ReturnType != typeof(TextObject)) nameM = null;
            var initM = gb != null ? AccessTools.Method(gb, "InitializeGentry") : null;
            var clanM = ca != null ? AccessTools.Method(ca, "CreateNewClan") : null;
            if (clanM != null)
            {
                var p = clanM.GetParameters();
                if (clanM.ReturnType != typeof(Clan) || p.Length < 3 || p[1].ParameterType != typeof(Settlement) || p[2].ParameterType != typeof(string)) clanM = null;
            }
            Patch(h, "nazwa rodu (BK ClanActions.GetRandomAvailableName)", nameM, null, nameof(NamePost), null);
            Patch(h, "okno gentry (BK BKGentryBehavior.InitializeGentry)", initM, nameof(CtxPre), null, nameof(CtxFin));
            Patch(h, "nowy rod gentry (BK ClanActions.CreateNewClan)", clanM, null, nameof(ClanPost), null);
            _wiredN = _wired.Count;
            _wiredText = _wired.Count > 0 ? string.Join(", ", _wired.ToArray()) : "-";
            _missingText = _missing.Count > 0 ? string.Join(", ", _missing.ToArray()) : "-";
            Log.Info("Nazwy gentry BK: latki przy starcie gry - wpiete " + _wiredN + "/" + NeedN + ": " + _wiredText + "; BRAK: " + _missingText + ".");
        }

        // ------------------------------------------------------------ okno gentry (BK BKGentryBehavior.InitializeGentry(Settlement, bool))
        public static void CtxPre(out bool __state)
        {
            __state = true;
            _ctxDepth++;
        }

        public static void CtxFin(bool __state)
        {
            if (__state && _ctxDepth > 0) _ctxDepth--;
        }

        // ------------------------------------------------------------ nazwa (BK ClanActions.GetRandomAvailableName(CultureObject, Settlement))
        public static void NamePost(CultureObject __0, Settlement __1, ref TextObject __result)
        {
            if (!On || __0 == null) return;
            bool gentry = _ctxDepth > 0;
            try
            {
                Bump(gentry, NReq);
                string bk = __result != null ? __result.ToString() : null;
                if (!gentry && bk == null) { Bump(false, NNone); return; }   // rycerz / bunt: BK nie dal nazwy - jak dotad (null)
                var taken = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                var same = new List<string>();
                foreach (var cl in Clan.All)
                {
                    string n = NameOf(cl);
                    if (n == null) continue;
                    taken.Add(n);
                    if (cl.Culture == __0) same.Add(n);
                }
                if (bk != null && Free(bk, taken, same)) { Bump(gentry, NSame); return; }   // BK dal nazwe wolna w calym swiecie - ta sama, ktora dalaby latka
                var list = __0.ClanNameList;
                if (list != null && list.Count > 1)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        var t = list[i];
                        string n = t != null ? t.ToString() : null;
                        if (n != null && Free(n, taken, same))
                        {
                            __result = t;   // ten sam obiekt z listy kultury, jak w BK
                            Bump(gentry, bk != null ? NPoolTaken : NPoolNone);
                            return;
                        }
                    }
                }
                string fb = Fallback(__0, __1, taken, same);
                if (fb != null)
                {
                    __result = new TextObject("{=!}" + fb);
                    Bump(gentry, NVillage);
                    return;
                }
                Bump(gentry, NNone);   // nic wolnego - wynik BK (zajeta albo null) zostaje
            }
            catch (Exception e) { Stumble("NamePost", e); }
        }

        /// <summary>Nazwa przechodzi BK CanCreateNewClan (rod tej kultury z nazwa ja zawierajaca) i gre IsClanNameApplicable (zaden rod jej nie nosi, zasady nazwy).</summary>
        private static bool Free(string n, HashSet<string> taken, List<string> same)
        {
            if (string.IsNullOrEmpty(n) || n.Length > NameMax || n.StartsWith(" ") || n.EndsWith(" ") || n.Contains("  ")) return false;
            if (Common.TextContainsSpecialCharacters(n)) return false;
            if (taken.Contains(n)) return false;
            for (int i = 0; i < same.Count; i++) if (same[i].Contains(n)) return false;
            return true;
        }

        /// <summary>Zapas, gdy lista kultury wyczerpana w calym swiecie: nazwa wsi, potem 'nazwa z listy of wies', potem 'wies kultura'. null - nic wolnego.</summary>
        private static string Fallback(CultureObject c, Settlement st, HashSet<string> taken, List<string> same)
        {
            string s = st != null && st.Name != null ? st.Name.ToString() : null;
            if (string.IsNullOrEmpty(s)) return null;
            if (Free(s, taken, same)) return s;
            var list = c.ClanNameList;
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    string n = list[i] != null ? list[i].ToString() : null;
                    if (string.IsNullOrEmpty(n)) continue;
                    string x = n + " of " + s;
                    if (Free(x, taken, same)) return x;
                }
            }
            string cn = c.Name != null ? c.Name.ToString() : null;
            if (!string.IsNullOrEmpty(cn))
            {
                string x = s + " " + cn;
                if (Free(x, taken, same)) return x;
            }
            return null;
        }

        // ------------------------------------------------------------ nowy rod (BK ClanActions.CreateNewClan(Hero, Settlement, string id, ...)) - tylko licznik
        public static void ClanPost(Settlement __1, string __2, Clan __result)
        {
            try
            {
                if (__result == null || __2 == null || !__2.StartsWith(GentryPrefix)) return;
                var f = __1 != null ? __1.MapFaction : null;
                string k = f != null && f.Name != null ? f.Name.ToString() : "-";
                Add(_newDay, k); Add(_newSess, k);
                _newDayN++; _newSessN++;
            }
            catch (Exception e) { Stumble("ClanPost", e); }
        }

        // ------------------------------------------------------------ linia doby (ArmouryBehavior - po BkDipGold.Daily)
        private static bool HasTemplate(CultureObject c)
        {
            if (c == null) return false;
            bool v;
            if (_tpl.TryGetValue(c, out v)) return v;
            v = false;
            foreach (var ch in CharacterObject.All)   // BK BKGentryBehavior.GetTemplate :479-483 (glowa rodu - mezczyzna)
            {
                if (ch != null && !ch.IsFemale && ch.Occupation == Occupation.Lord && ch.Culture == c && ch.StringId != null && ch.StringId.Contains("bannerkings_gentry_")) { v = true; break; }
            }
            _tpl[c] = v;
            return v;
        }

        private static string Counts(int period, int kind)
        {
            return "prosby " + _n[period, kind, NReq] + ", BK dal wolna " + _n[period, kind, NSame] + ", z listy zamiast zajetej " + _n[period, kind, NPoolTaken]
                 + ", z listy zamiast braku " + _n[period, kind, NPoolNone] + ", z nazwy wsi " + _n[period, kind, NVillage] + ", bez nazwy " + _n[period, kind, NNone];
        }

        private static void AppendMap(StringBuilder sb, Dictionary<string, int> d)
        {
            if (d.Count == 0) { sb.Append('-'); return; }
            var keys = new List<string>(d.Keys);
            keys.Sort(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(keys[i]).Append(' ').Append(d[keys[i]]);
            }
        }

        internal static void Daily()
        {
            try
            {
                var s = Settings.Current;
                if (Campaign.Current == null || s == null || !s.LogEnabled) return;
                int day = (int)CampaignTime.Now.ToDays - 1;

                // rody: gentry zywe wedlug krolestwa, nazwy powtorzone (gentry / rycerze BK)
                var alive = new Dictionary<string, int>();
                int aliveN = 0;
                var names = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
                var bkNames = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                foreach (var cl in Clan.All)
                {
                    if (cl == null || cl.IsEliminated) continue;
                    string n = NameOf(cl);
                    if (n != null) Add(names, n);
                    string id = cl.StringId;
                    bool g = id != null && id.StartsWith(GentryPrefix);
                    if (n != null && id != null && (g || id.EndsWith(KnightSuffix))) bkNames.Add(n);
                    if (!g) continue;
                    aliveN++;
                    Add(alive, cl.Kingdom != null && cl.Kingdom.Name != null ? cl.Kingdom.Name.ToString() : "bez krolestwa");
                }
                int dup = 0;
                foreach (var n in bkNames) { int v; if (names.TryGetValue(n, out v) && v > 1) dup++; }

                // wsie krolestw: kultury, wsie kultur z wzorcem gentry BK
                var kVill = new Dictionary<Kingdom, int>();
                var kTplVill = new Dictionary<Kingdom, int>();
                var kCult = new Dictionary<Kingdom, List<string>>();
                var vCult = new Dictionary<CultureObject, int>();
                foreach (var st in Settlement.All)
                {
                    if (st == null || !st.IsVillage) continue;
                    var c = st.Culture;
                    if (c != null) { int v; vCult.TryGetValue(c, out v); vCult[c] = v + 1; }
                    var kd = st.MapFaction as Kingdom;
                    if (kd == null) continue;
                    int a; kVill.TryGetValue(kd, out a); kVill[kd] = a + 1;
                    if (c != null && HasTemplate(c)) { int b; kTplVill.TryGetValue(kd, out b); kTplVill[kd] = b + 1; }
                    List<string> lc;
                    if (!kCult.TryGetValue(kd, out lc)) kCult[kd] = lc = new List<string>();
                    string cid = c != null ? c.StringId : "-";
                    if (!lc.Contains(cid)) lc.Add(cid);
                }
                var noTpl = new List<string>(); var noOnly = new List<string>();
                int withG = 0;
                foreach (var kv in kVill)
                {
                    string kn = kv.Key.Name != null ? kv.Key.Name.ToString() : kv.Key.StringId;
                    int g; alive.TryGetValue(kn, out g);
                    if (g > 0) { withG++; continue; }
                    int tv; kTplVill.TryGetValue(kv.Key, out tv);
                    string item = kn + " (wsi " + kv.Value + ", z wzorcem " + tv + ": " + string.Join("/", kCult[kv.Key].ToArray()) + ")";
                    if (tv > 0) noTpl.Add(item); else noOnly.Add(item);
                }
                noTpl.Sort(StringComparer.Ordinal); noOnly.Sort(StringComparer.Ordinal);

                if (!_startLogged)
                {
                    _startLogged = true;
                    var sb0 = new StringBuilder(1500);
                    sb0.Append("Nazwy gentry BK: start | Gentry Names Fallback ").Append(On ? "TAK" : "NIE (nazwy jak dotad)")
                       .Append(" | latki: wpiete ").Append(_wiredN).Append('/').Append(NeedN).Append(" (").Append(_wiredText).Append("); BRAK: ").Append(_missingText)
                       .Append(" | rody gentry przed pierwsza doba sesji (tworzenie swiata albo od wczytania) ").Append(_newSessN)
                       .Append(" | kultury wsi bez wzorca gentry BK (latka nie pomoze): ");
                    var cl0 = new List<string>();
                    foreach (var kv in vCult) if (!HasTemplate(kv.Key)) cl0.Add(kv.Key.StringId + " (wsi " + kv.Value + ")");
                    cl0.Sort(StringComparer.Ordinal);
                    sb0.Append(cl0.Count > 0 ? string.Join(", ", cl0.ToArray()) : "-");
                    // wspolne listy nazw kultur wsi (przyczyna blokady)
                    var grp = new Dictionary<string, List<string>>();
                    var grpN = new Dictionary<string, int>();
                    foreach (var kv in vCult)
                    {
                        var l = kv.Key.ClanNameList;
                        if (l == null || l.Count == 0) continue;
                        var parts = new string[l.Count];
                        for (int i = 0; i < l.Count; i++) parts[i] = l[i] != null ? l[i].ToString() : "";
                        string key = string.Join("|", parts);
                        List<string> gl;
                        if (!grp.TryGetValue(key, out gl)) { grp[key] = gl = new List<string>(); grpN[key] = l.Count; }
                        gl.Add(kv.Key.StringId);
                    }
                    var gtxt = new List<string>();
                    foreach (var kv in grp) if (kv.Value.Count > 1) { kv.Value.Sort(StringComparer.Ordinal); gtxt.Add(string.Join("+", kv.Value.ToArray()) + " (" + grpN[kv.Key] + ")"); }
                    gtxt.Sort(StringComparer.Ordinal);
                    sb0.Append(" | wspolne listy nazw rodow: ").Append(gtxt.Count > 0 ? string.Join("; ", gtxt.ToArray()) : "-").Append('.');
                    Log.Info(sb0.ToString());
                }

                var sb = new StringBuilder(2000);
                sb.Append("Nazwy gentry BK: dzien ").Append(day).Append(" | Gentry Names Fallback ").Append(On ? "TAK" : "NIE")
                  .Append(" | nowe rody gentry dzis ").Append(_newDayN).Append(" (");
                AppendMap(sb, _newDay);
                sb.Append(") | od startu sesji ").Append(_newSessN).Append(" (");
                AppendMap(sb, _newSess);
                sb.Append(") | nazwy dla gentry dzis: ").Append(Counts(0, 0)).Append("; od startu sesji: ").Append(Counts(1, 0))
                  .Append(" | inne (rycerz BK, bunt) od startu sesji: ").Append(Counts(1, 1))
                  .Append(" | rody gentry zywe ").Append(aliveN).Append(" w ").Append(withG).Append(" z ").Append(kVill.Count).Append(" krolestw z wsiami: ");
                AppendMap(sb, alive);
                sb.Append(" | krolestwa z wsiami bez gentry: z wzorcem ").Append(noTpl.Count).Append(" (").Append(noTpl.Count > 0 ? string.Join(", ", noTpl.ToArray()) : "-")
                  .Append("), tylko bez wzorca ").Append(noOnly.Count).Append(" (").Append(noOnly.Count > 0 ? string.Join(", ", noOnly.ToArray()) : "-").Append(')')
                  .Append(" | powtorzone nazwy rodow gentry / rycerzy BK ").Append(dup)
                  .Append(" | potkniecia ").Append(_stumbles).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally
            {
                _newDay.Clear(); _newDayN = 0;
                for (int k = 0; k < 2; k++) for (int i = 0; i < NSlots; i++) _n[0, k, i] = 0;
            }
        }
    }
}
