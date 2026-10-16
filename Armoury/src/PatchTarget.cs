using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;

namespace Armoury
{
    /// <summary>
    /// PULAPKA HARMONY "CZYNNEGO MODELU" (15.10, drewno w kuzni - SmithWood). AccessTools.Method(typPochodny, nazwa) dla metody DZIEDZICZONEJ zwraca
    /// MethodInfo z ReflectedType typu pochodnego (BKSmithingModel), a DeclaringType klasy bazowej (DefaultSmithingModel). Harmony 2.4.2 w
    /// PatchProcessor.Patch rzuca wtedy ArgumentException "You can only patch implemented methods" (IsDeclaredMember = DeclaringType == ReflectedType),
    /// a finalizer BKROTPatch (Byzantium1071HarmonyPatchProcessorPatch) ten wyjatek polyka: Harmony.Patch zwraca null, nasz kod pisal "wpiete",
    /// latka nie dzialala. Slad zostawal tylko w Modules/BKROTPatch/BKROTLog.txt (nadpisywany przy kazdym starcie gry).
    /// (A) Declared / OfActive / OfActiveOr - wspolne szukanie celu latki na metodzie czynnego modelu: zawsze metoda z klasy, ktora ja deklaruje;
    ///     kazde szukanie zapisuje wpis do raportu (etykieta, typ czynny, klasa deklarujaca, lancuch modeli).
    /// (B) Straznik (od OnSubModuleLoad Armoury, przed wszystkimi naszymi latkami): prefiks na PatchProcessor.Patch - latka NASZEGO wlasciciela
    ///     (com.jeff.*) z celem przez typ pochodny jest przestawiana na metode klasy deklarujacej (klucz PatchTargetGuard; Off = tylko raport);
    ///     cudze latki - tylko licznik, bez zmian. Przed przestawieniem sprawdzenie dubla: ta sama metoda latki juz wisi na celu (Harmony 2.4.2 nie
    ///     odrzuca duplikatow - postfiks bieglby 2x) - pole tej latki zerowane (Harmony pomija null), licznik "dubel". Postfiks liczy udane proby
    ///     (biegnie tylko przy sukcesie), finalizer zapisuje blad proby (priorytet First - widzi wyjatek przed finalizerem BKROTPatch; void, niczego
    ///     nie polyka). Prefiks na obu PatchProcessor.Unpatch: nasze zdejmowanie tym samym MethodInfo z typem pochodnym idzie na metode klasy
    ///     deklarujacej (inaczej Harmony zaklada pusta otoczke i na tej metodzie nie dziala zadna latka - recenzja, test A); cudze - tylko licznik.
    /// (C) Report - jedna linia "Latki czynnego modelu (HT): start sesji" po OnSessionLaunched wszystkich modow (OnAfterSessionLaunched), potem
    ///     najwyzej 20 linii "po starcie" na sesje dla prob i zdjec, ktore po raporcie sie nie udaly albo zostaly poprawione.
    /// Wszystko w try/catch z licznikiem potkniec: wyjatek w latkach straznika zepsulby latanie calej gry.
    /// Harmony.GetPatchInfo pytac zawsze o metode deklarujaca - dla MethodInfo z typem pochodnym zwraca null takze po udanej latce.
    /// </summary>
    internal static class PatchTarget
    {
        private const string Ours = "com.jeff.";
        private const string GuardId = "com.jeff.armoury.patchguard";
        private const string Head = "Latki czynnego modelu (HT): ";
        private const int LateMax = 20;
        private const BindingFlags Decl = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private static readonly object Gate = new object();

        // wpisy szukania celu: etykieta -> ostatnie szukanie w tym procesie (latki sa raz na proces, wiec raport kazdej sesji pokazuje wszystkie);
        // From = liczba prob w chwili szukania - wynik wpisu to PIERWSZA proba na tym uchwycie od tej chwili (latka zalozona zaraz po szukaniu)
        private sealed class Entry { internal string Label, Active, Name, Chain, Why; internal MethodInfo Method; internal bool ViaDecl; internal int From; }
        private static readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private static readonly List<string> _order = new List<string>();

        /// <summary>Proba latki naszego wlasciciela przez PatchProcessor.Patch (stan miedzy prefiksem, postfiksem i finalizerem straznika).</summary>
        internal sealed class Attempt { internal string Id, Err, From, Dup; internal MethodBase M; internal bool Ok, Fixed; }
        private static readonly List<Attempt> _tries = new List<Attempt>();                 // od startu procesu
        private static readonly List<string> _fixed = new List<string>(), _unfixed = new List<string>(), _foreign = new List<string>(), _dups = new List<string>();
        private static readonly List<string> _uFixed = new List<string>(), _uUnfixed = new List<string>(), _uForeign = new List<string>();
        private static int _fixedN, _unfixedN, _foreignN, _nullN, _dupN, _uFixedN, _uUnfixedN, _uForeignN, _late, _stumbles;
        // przestawienia straznika: cele (uchwyt metody deklarujacej) i trojki cel|rodzaj|metoda latki - do dubla w odwrotnej kolejnosci i do Unpatch
        private static readonly HashSet<long> _redirTargets = new HashSet<long>();
        private static readonly HashSet<string> _redirKeys = new HashSet<string>(StringComparer.Ordinal);
        private static FieldInfo _fOrig, _fInst;
        // pola latek PatchProcessor (Harmony 2.4.2, PatchProcessor.cs:16-26) w kolejnosci list Patches (ListOf)
        private static readonly string[] Kinds = { "prefix", "postfix", "transpiler", "finalizer", "innerprefix", "innerpostfix" };
        private static FieldInfo[] _fKinds;
        private static bool _installed, _wired, _reported;
        private static string _guard = "nie zakladany";

        private static bool GuardOn { get { var s = Settings.Current; return s == null || s.PatchTargetGuard; } }

        private static void Stumble(string where, Exception e)
        {
            int n;
            lock (Gate) n = ++_stumbles;
            try { if (n <= 5 || n % 100 == 0) Log.Error("PatchTarget." + where + " (potkniecie " + n + ")", e); } catch { }
        }

        // ------------------------------------------------------------ (A) szukanie celu
        /// <summary>Metoda z klasy, ktora ja deklaruje (ReflectedType == DeclaringType); metoda juz deklarowana - ten sam obiekt; null -> null.</summary>
        internal static MethodInfo Declared(MethodInfo m)
        {
            if (m == null) return null;
            try
            {
                if (m.DeclaringType == null || m.DeclaringType == m.ReflectedType) return m;
                return DeclaredOf(m) as MethodInfo ?? m;
            }
            catch (Exception e) { Stumble("Declared", e); return m; }
        }

        /// <summary>Ta sama metoda wzieta z klasy deklarujacej: ten sam MetadataToken i modul wsrod skladowych DeclaredOnly (jak HarmonyLib GetDeclaredMember,
        /// ale bez skladowych dziedziczonych dalej); metoda generyczna - definicja z klasy deklarujacej z tymi samymi argumentami. Brak -> null.</summary>
        private static MethodBase DeclaredOf(MethodBase m)
        {
            var dt = m.DeclaringType;
            if (dt == null) return null;
            if (dt == m.ReflectedType) return m;
            var mi = m as MethodInfo;
            if (mi != null && mi.IsGenericMethod && !mi.IsGenericMethodDefinition)
            {
                var def = DeclaredOf(mi.GetGenericMethodDefinition()) as MethodInfo;
                return def != null ? def.MakeGenericMethod(mi.GetGenericArguments()) : null;
            }
            MethodBase[] all = m is ConstructorInfo ? (MethodBase[])dt.GetConstructors(Decl) : dt.GetMethods(Decl);
            foreach (var c in all)
                if (c != null && c.MetadataToken == m.MetadataToken && c.Module == m.Module) return c;
            return null;
        }

        /// <summary>Cel latki na metodzie czynnego modelu: AccessTools.Method(typ modelu, nazwa, args), znormalizowana do klasy deklarujacej (Declared),
        /// plus wpis do raportu (etykieta, typ czynny, klasa deklarujaca, lancuch modeli). Model null -> null bez wpisu. Wyjatek AccessTools idzie dalej
        /// do wolajacego jak dotad (wpis z "wyjatek"). Latke zakladac zaraz po szukaniu - raport bierze pierwsza proba na tej metodzie od szukania.</summary>
        internal static MethodInfo OfActive(object model, string name, Type[] args, string label)
        {
            return model == null ? null : Find(model.GetType(), model, name, args, label);
        }

        /// <summary>Jak OfActive, ale model null -> szukanie w typie zapasowym (np. DefaultSiegeStrategyActionModel - SiegeWalls2).</summary>
        internal static MethodInfo OfActiveOr(object model, Type fallback, string name, Type[] args, string label)
        {
            var t = model != null ? model.GetType() : fallback;
            return t == null ? null : Find(t, model, name, args, label);
        }

        private static MethodInfo Find(Type t, object model, string name, Type[] args, string label)
        {
            MethodInfo m;
            try { m = AccessTools.Method(t, name, args); }
            catch (Exception e) { Note(label, t, name, null, model, "wyjatek " + e.GetType().Name); throw; }
            var d = Declared(m);
            Note(label, t, name, d, model, d == null ? "brak metody" : null);
            return d;
        }

        private static void Note(string label, Type t, string name, MethodInfo d, object model, string why)
        {
            try
            {
                var e = new Entry
                {
                    Label = string.IsNullOrEmpty(label) ? t.Name + "." + name : label, Active = t.Name, Name = name, Method = d, Why = why,
                    ViaDecl = d != null && d.DeclaringType != null && d.DeclaringType != t,
                    Chain = model != null ? ChainOf(model) : t.Name + " (model nieznany)"
                };
                lock (Gate)
                {
                    Entry old;
                    // ponowne szukanie tej samej metody (kolejna sesja; latka raz na proces) - wynik dalej z proby po pierwszym szukaniu
                    e.From = _entries.TryGetValue(e.Label, out old) && Same(old.Method, d) ? old.From : _tries.Count;
                    if (old == null) _order.Add(e.Label);
                    _entries[e.Label] = e;
                }
            }
            catch (Exception x) { Stumble("Note", x); }
        }

        /// <summary>Lancuch modeli do raportu: model owijajacy (wprost po typie abstrakcyjnym) - nastepny = pierwsze pole instancji typu modelu (jak
        /// BkrotChains.Next: ROT _previousModel, BEE _baseModel, NavalDLC BaseModel); model po klasie konkretnej - koniec. Najwyzej 8 krokow.</summary>
        private static string ChainOf(object model)
        {
            try
            {
                var t = model.GetType();
                var sb = new StringBuilder(t.Name);
                var abs = t.BaseType;
                if (abs == null || !abs.IsAbstract) return sb.ToString();
                var seen = new List<object> { model };
                object c = model;
                for (int n = 0; n < 8; n++)
                {
                    c = Next(c, abs);
                    if (c == null || seen.Exists(o => ReferenceEquals(o, c))) break;
                    seen.Add(c);
                    sb.Append(" -> ").Append(c.GetType().Name);
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("ChainOf", e); return model.GetType().Name + " (lancuch ?)"; }
        }

        // kopia regul BkrotChains.Next (prywatna, P4 - bez ruszania BkrotChains.cs)
        private static object Next(object m, Type abs)
        {
            if (m == null) return null;
            var t = m.GetType();
            if (t.BaseType != abs) return null;
            for (var k = t; k != null && k != typeof(object); k = k.BaseType)
                foreach (var fi in k.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!abs.IsAssignableFrom(fi.FieldType)) continue;
                    var v = fi.GetValue(m);
                    if (v != null && !ReferenceEquals(v, m)) return v;
                }
            return null;
        }

        // ------------------------------------------------------------ (B) straznik na PatchProcessor.Patch i PatchProcessor.Unpatch
        /// <summary>SubModuleMain.OnSubModuleLoad (Armoury laduje sie przed ForgeView, GT i BKROTPatch): raz na proces. Pola PatchProcessor z Harmony 2.4.2
        /// ("original", "instance", pola latek); brak pola = straznik BRAK, bez wywrotki (inna wersja Harmony). Najpierw Unpatch: bez niego przestawianie
        /// w Patch psuloby zdejmowanie latek tym samym MethodInfo - wtedy straznik Patch nie jest zakladany.</summary>
        internal static void InstallGuard()
        {
            if (_installed) return;
            _installed = true;
            try
            {
                _fOrig = AccessTools.Field(typeof(PatchProcessor), "original");
                _fInst = AccessTools.Field(typeof(PatchProcessor), "instance");
                var kinds = new FieldInfo[Kinds.Length];
                bool kindsOk = true;
                for (int k = 0; k < Kinds.Length; k++)
                {
                    kinds[k] = AccessTools.Field(typeof(PatchProcessor), Kinds[k]);
                    if (kinds[k] == null || kinds[k].IsStatic || !typeof(HarmonyMethod).IsAssignableFrom(kinds[k].FieldType)) kindsOk = false;
                }
                var pp = AccessTools.DeclaredMethod(typeof(PatchProcessor), "Patch", Type.EmptyTypes);
                var u1 = AccessTools.DeclaredMethod(typeof(PatchProcessor), "Unpatch", new[] { typeof(HarmonyPatchType), typeof(string) });
                var u2 = AccessTools.DeclaredMethod(typeof(PatchProcessor), "Unpatch", new[] { typeof(MethodInfo) });
                if (_fOrig == null || _fInst == null || !kindsOk || !typeof(MethodBase).IsAssignableFrom(_fOrig.FieldType) || !typeof(Harmony).IsAssignableFrom(_fInst.FieldType))
                    _guard = "BRAK (pola PatchProcessor)";
                else if (pp == null || u1 == null || u2 == null) _guard = "BRAK (PatchProcessor.Patch / Unpatch)";
                else
                {
                    _fKinds = kinds;
                    var g = new Harmony(GuardId);
                    g.Patch(u1, prefix: new HarmonyMethod(typeof(PatchTarget), nameof(GuardUnpatchPre)) { priority = Priority.First });
                    g.Patch(u2, prefix: new HarmonyMethod(typeof(PatchTarget), nameof(GuardUnpatchPre)) { priority = Priority.First });
                    bool uOk = HasOwner(Harmony.GetPatchInfo(u1), GuardId) && HasOwner(Harmony.GetPatchInfo(u2), GuardId);
                    if (uOk)
                    {
                        g.Patch(pp,
                            prefix: new HarmonyMethod(typeof(PatchTarget), nameof(GuardPre)) { priority = Priority.First },
                            postfix: new HarmonyMethod(typeof(PatchTarget), nameof(GuardPost)) { priority = Priority.Last },
                            finalizer: new HarmonyMethod(typeof(PatchTarget), nameof(GuardFin)) { priority = Priority.First });
                        _wired = HasOwner(Harmony.GetPatchInfo(pp), GuardId);
                    }
                    _guard = !uOk ? "BRAK (latka Unpatch nie weszla)" : _wired ? "wpiety" : "BRAK (latka nie weszla)";
                }
            }
            catch (Exception e) { _guard = "BRAK (wyjatek " + e.GetType().Name + ")"; Stumble("InstallGuard", e); }
            Log.Info(Head + "straznik Harmony " + _guard + " (PatchProcessor.Patch: prefiks First, postfiks Last, finalizer First; PatchProcessor.Unpatch x2: prefiks First)"
                     + " | klucz PatchTargetGuard " + (GuardOn ? "TAK" : "NIE") + " (z pliku ustawien Armoury.settings.xml; wartosc z MCM od startu kampanii - OnGameStart).");
        }

        private static bool HasOwner(Patches info, string exact)
        {
            if (info == null || info.Owners == null) return false;
            foreach (var o in info.Owners)
                if (o != null && (exact != null ? o == exact : o.StartsWith(Ours, StringComparison.Ordinal))) return true;
            return false;
        }

        private static string Name(MethodBase m)
        {
            if (m == null) return "-";
            var t = m.ReflectedType ?? m.DeclaringType;
            return (t != null ? t.Name : "?") + "." + m.Name;
        }

        private static long Key(MethodBase m) { return m.MethodHandle.Value.ToInt64(); }

        private static string RKey(long target, int kind, MethodBase patch) { return target + "|" + kind + "|" + Key(patch); }

        private static IList<Patch> ListOf(Patches info, int k)
        {
            switch (k)
            {
                case 0: return info.Prefixes;
                case 1: return info.Postfixes;
                case 2: return info.Transpilers;
                case 3: return info.Finalizers;
                case 4: return info.InnerPrefixes;
                default: return info.InnerPostfixes;
            }
        }

        /// <summary>Dubel (recenzja, test B): ta sama metoda latki tego samego rodzaju juz wisi na celu - Harmony 2.4.2 (PatchInfo.Add) jej nie odrzuca, wiec
        /// biegla by 2x. Przy przestawieniu (redirect) - kazda taka; bez przestawienia - tylko wpieta wczesniej przez przestawienie straznika (bez straznika
        /// tamta proba bylaby polknieta, a ta wpieta - 1x). Pole tej latki zerowane: Harmony pomija null (jak latka bez tego rodzaju), reszta latki idzie
        /// dalej. Zwraca opis albo null.</summary>
        private static string DropDups(PatchProcessor pp, MethodBase target, bool redirect)
        {
            var info = Harmony.GetPatchInfo(target);
            if (info == null) return null;
            long t = Key(target);
            List<string> parts = null;
            for (int k = 0; k < Kinds.Length; k++)
            {
                var hm = _fKinds[k].GetValue(pp) as HarmonyMethod;
                var pm = hm != null ? hm.method : null;
                var list = pm != null ? ListOf(info, k) : null;
                if (list == null) continue;
                foreach (var p in list)
                {
                    if (p == null || !Same(p.PatchMethod, pm)) continue;
                    if (!redirect)
                    {
                        bool mine;
                        lock (Gate) mine = _redirKeys.Contains(RKey(t, k, pm));
                        if (!mine) continue;
                    }
                    _fKinds[k].SetValue(pp, null);
                    if (parts == null) parts = new List<string>();
                    parts.Add(Kinds[k] + " " + Name(pm) + " (" + p.owner + ")");
                    break;
                }
            }
            return parts != null ? string.Join(", ", parts.ToArray()) : null;
        }

        /// <summary>Prefiks PatchProcessor.Patch (Priority.First). Nigdy nie przerywa latania (void) i nigdy nie rzuca.</summary>
        public static void GuardPre(PatchProcessor __instance, out Attempt __state)
        {
            __state = null;
            try
            {
                if (__instance == null || _fOrig == null || _fInst == null || _fKinds == null) return;
                var o = _fOrig.GetValue(__instance) as MethodBase;
                var h = _fInst.GetValue(__instance) as Harmony;
                string id = h != null ? h.Id : null;
                bool ours = id != null && id.StartsWith(Ours, StringComparison.Ordinal);
                if (o == null)
                {
                    if (ours) lock (Gate) _nullN++;   // Harmony rzuci NullReferenceException wolajacemu - jak dotad
                    return;
                }
                bool fix = false;
                string from = null, dup = null;
                if (o.DeclaringType != null && o.DeclaringType != o.ReflectedType)
                {
                    if (!ours)
                    {
                        lock (Gate) { _foreignN++; if (_foreign.Count < 5) _foreign.Add(id + " " + Name(o)); }
                    }
                    else
                    {
                        bool on = GuardOn;
                        var d = on ? DeclaredOf(o) : null;
                        if (d != null && d.DeclaringType == d.ReflectedType)
                        {
                            dup = DropDups(__instance, d, true);
                            _fOrig.SetValue(__instance, d);
                            long t = Key(d);
                            var keys = new List<string>();
                            for (int k = 0; k < Kinds.Length; k++)
                            {
                                var hm = _fKinds[k].GetValue(__instance) as HarmonyMethod;
                                if (hm != null && hm.method != null) keys.Add(RKey(t, k, hm.method));
                            }
                            lock (Gate)
                            {
                                _redirTargets.Add(t);
                                foreach (var key in keys) _redirKeys.Add(key);
                                _fixedN++; if (_fixed.Count < 10) _fixed.Add(id + " " + Name(o) + " -> " + Name(d));
                            }
                            from = Name(o);
                            o = d;
                            fix = true;
                        }
                        else
                        {
                            lock (Gate) { _unfixedN++; if (_unfixed.Count < 10) _unfixed.Add(id + " " + Name(o) + (on ? " (brak metody w klasie deklarujacej)" : " (klucz NIE)")); }
                        }
                    }
                }
                else if (ours)
                {
                    bool was;
                    lock (Gate) was = _redirTargets.Contains(Key(o));
                    if (was) dup = DropDups(__instance, o, false);   // odwrotna kolejnosc: najpierw przestawiona przez typ pochodny, teraz wprost na metodzie
                }
                if (dup != null)
                    lock (Gate) { _dupN++; if (_dups.Count < 5) _dups.Add(id + " " + (from != null ? from + " -> " : "") + Name(o) + ": " + dup); }
                if (!ours) return;
                var a = new Attempt { Id = id, M = o, Fixed = fix, From = from, Dup = dup };
                lock (Gate) _tries.Add(a);
                __state = a;
            }
            catch (Exception e) { Stumble("GuardPre", e); }
        }

        /// <summary>Postfiks PatchProcessor.Patch (Priority.Last): biegnie tylko, gdy Patch skonczyl bez wyjatku - proba udana.</summary>
        public static void GuardPost(MethodInfo __result, Attempt __state)
        {
            try
            {
                if (__state == null || __result == null) return;
                __state.Ok = true;
                if (_reported && (__state.Fixed || __state.Dup != null)) Late((__state.Fixed ? "poprawiona " : "dubel ") + Describe(__state));
            }
            catch (Exception e) { Stumble("GuardPost", e); }
        }

        /// <summary>Finalizer PatchProcessor.Patch (Priority.First - przed finalizerem BKROTPatch, ktory polyka "You can only patch implemented methods"):
        /// zapisuje blad proby; void - wyjatku nie zmienia ani nie polyka.</summary>
        public static void GuardFin(Exception __exception, Attempt __state)
        {
            try
            {
                if (__state == null || __exception == null) return;
                string msg = __exception.Message ?? "";
                if (msg.Length > 140) msg = msg.Substring(0, 140);
                __state.Err = __exception.GetType().Name + ": " + msg.Replace('\r', ' ').Replace('\n', ' ');
                if (_reported) Late("NIE " + Describe(__state));
            }
            catch (Exception e) { Stumble("GuardFin", e); }
        }

        /// <summary>Prefiks obu PatchProcessor.Unpatch (Priority.First; recenzja, test A). Zdejmowanie latki tym samym MethodInfo z typem pochodnym, ktorym
        /// ja zalozono: Harmony szuka latek pod tym MethodInfo (GetPatchInfo = null), zaklada pusta otoczke na metodzie pochodnej i na tej metodzie natywnej
        /// nie dziala ZADNA latka (nasza, BK, ROT), dopoki ktos nie zalozy nastepnej - wtedy wraca tez "zdjeta". Nasze (com.jeff.*): cel na metode klasy
        /// deklarujacej (klucz PatchTargetGuard albo cel przestawiony wczesniej w Patch - Patch i Unpatch zawsze na tej samej metodzie). Cudze: tylko licznik
        /// i lista (do 5), bez zmian (zasada 4). Nigdy nie przerywa (void) i nigdy nie rzuca.</summary>
        public static void GuardUnpatchPre(PatchProcessor __instance)
        {
            try
            {
                if (__instance == null || _fOrig == null || _fInst == null) return;
                var o = _fOrig.GetValue(__instance) as MethodBase;
                if (o == null || o.DeclaringType == null || o.DeclaringType == o.ReflectedType) return;
                var h = _fInst.GetValue(__instance) as Harmony;
                string id = h != null ? h.Id : null;
                string text;
                if (id == null || !id.StartsWith(Ours, StringComparison.Ordinal))
                {
                    text = id + " " + Name(o);
                    lock (Gate) { _uForeignN++; if (_uForeign.Count < 5) _uForeign.Add(text); }
                    if (_reported) Late("cudzy Unpatch z typem pochodnym " + text);
                    return;
                }
                var d = DeclaredOf(o);
                bool ok = d != null && d.DeclaringType == d.ReflectedType, was = false;
                if (ok) lock (Gate) was = _redirTargets.Contains(Key(d));
                if (ok && (GuardOn || was))
                {
                    _fOrig.SetValue(__instance, d);
                    text = id + " " + Name(o) + " -> " + Name(d);
                    lock (Gate) { _uFixedN++; if (_uFixed.Count < 10) _uFixed.Add(text); }
                    if (_reported) Late("Unpatch poprawiony " + text);
                }
                else
                {
                    text = id + " " + Name(o) + (ok ? " (klucz NIE)" : " (brak metody w klasie deklarujacej)");
                    lock (Gate) { _uUnfixedN++; if (_uUnfixed.Count < 10) _uUnfixed.Add(text); }
                }
            }
            catch (Exception e) { Stumble("GuardUnpatchPre", e); }
        }

        private static void Counts(out int tries, out int ok)
        {
            tries = ok = 0;
            lock (Gate)
                foreach (var a in _tries) { tries++; if (a.Ok) ok++; }
        }

        private static string Describe(Attempt a)
        {
            return a.Id + " " + (a.From != null ? a.From + " -> " : "") + Name(a.M) + (a.Dup != null ? " (dubel: " + a.Dup + ")" : "") + (a.Err != null ? " (" + a.Err + ")" : "");
        }

        private static void Late(string what)
        {
            lock (Gate) { if (_late >= LateMax) return; _late++; }
            int p, w;
            Counts(out p, out w);
            Log.Info(Head + "po starcie - " + what + " (proby " + p + ", udane " + w + ").");
        }

        // ------------------------------------------------------------ (C) raport
        private static bool Same(MethodBase a, MethodBase b)
        {
            try { return a != null && b != null && a.MethodHandle == b.MethodHandle; } catch { return false; }
        }

        private static string Join(List<string> l) { return string.Join("; ", l.ToArray()); }

        /// <summary>ArmouryBehavior: OnAfterSessionLaunched (po OnSessionLaunched wszystkich modow). Jedna linia na sesje; liczniki od startu procesu.</summary>
        internal static void Report()
        {
            try
            {
                List<Entry> list;
                List<Attempt> tries;
                List<string> fixedL, unfixedL, foreignL, dupL, uFixedL, uUnfixedL, uForeignL;
                int fixedN, unfixedN, foreignN, nullN, dupN, uFixedN, uUnfixedN, uForeignN;
                lock (Gate)
                {
                    list = new List<Entry>(_order.Count);
                    foreach (var l in _order) list.Add(_entries[l]);
                    tries = new List<Attempt>(_tries);
                    fixedL = new List<string>(_fixed); unfixedL = new List<string>(_unfixed); foreignL = new List<string>(_foreign); dupL = new List<string>(_dups);
                    uFixedL = new List<string>(_uFixed); uUnfixedL = new List<string>(_uUnfixed); uForeignL = new List<string>(_uForeign);
                    fixedN = _fixedN; unfixedN = _unfixedN; foreignN = _foreignN; nullN = _nullN; dupN = _dupN;
                    uFixedN = _uFixedN; uUnfixedN = _uUnfixedN; uForeignN = _uForeignN;
                }
                int wired = 0, direct = 0;
                var via = new List<string>(); var none = new List<string>(); var no = new List<string>(); var chains = new List<string>();
                foreach (var e in list)
                {
                    try
                    {
                        if (e.Chain != null && e.Chain.IndexOf(" -> ", StringComparison.Ordinal) >= 0) chains.Add(e.Label + " " + e.Chain);
                        if (e.Method == null) { no.Add(e.Label + " " + e.Active + "." + e.Name + " (" + (e.Why ?? "brak metody") + ")"); continue; }
                        // wynik wpisu = pierwsza proba na tym uchwycie od szukania celu (latka tej linii); inna nasza latka na tej samej metodzie - przed albo po -
                        // nie zaslania nieudanej (recenzja); proby nie bylo (latka juz byla - dedup, Default wpiety wyzej): wlasciciel com.jeff.* w latkach
                        Attempt a = null;
                        for (int i = Math.Max(0, e.From); i < tries.Count && a == null; i++)
                            if (Same(tries[i].M, e.Method)) a = tries[i];
                        bool w = a != null ? a.Ok : HasOwner(Harmony.GetPatchInfo(e.Method), null);
                        string where = e.ViaDecl ? e.Active + " -> " + e.Method.DeclaringType.Name + "." + e.Method.Name : Name(e.Method);
                        if (w)
                        {
                            wired++;
                            if (e.ViaDecl) via.Add(e.Label + " " + where); else direct++;
                        }
                        else if (a == null) none.Add(e.Label + " " + where);
                        else no.Add(e.Label + " " + where + (a.Err != null ? " (" + a.Err + ")" : ""));
                    }
                    catch (Exception x) { Stumble("Report.Entry", x); no.Add(e.Label + " (potkniecie)"); }
                }
                int p = tries.Count, okN = 0;
                var bad = new List<string>();
                foreach (var a in tries)
                {
                    if (a.Ok) { okN++; continue; }
                    if (bad.Count < 10) bad.Add(a.Id + " " + Name(a.M) + (a.Err != null ? " (" + a.Err + ")" : ""));
                }
                var sb = new StringBuilder(1800);
                sb.Append(Head).Append("start sesji | klucz PatchTargetGuard ").Append(GuardOn ? "TAK" : "NIE");
                sb.Append(" | czynne modele: wpiete ").Append(wired).Append(" z ").Append(list.Count)
                  .Append(" (na metodzie typu czynnego ").Append(direct).Append(", przez klase deklarujaca ").Append(via.Count).Append(via.Count > 0 ? ": " + Join(via) : "").Append(")")
                  .Append(", bez proby ").Append(none.Count).Append(none.Count > 0 ? ": " + Join(none) : "")
                  .Append(", NIE ").Append(no.Count).Append(no.Count > 0 ? ": " + Join(no) : "");
                sb.Append(" | lancuchy ").Append(chains.Count).Append(chains.Count > 0 ? ": " + Join(chains) : "");
                sb.Append(" | straznik Harmony ").Append(_guard).Append(": nasze proby ").Append(p).Append(", udane ").Append(okN).Append(", NIE ").Append(p - okN)
                  .Append(bad.Count > 0 ? ": " + Join(bad) : "")
                  .Append("; poprawione na klase deklarujaca ").Append(fixedN).Append(fixedL.Count > 0 ? ": " + Join(fixedL) : "");
                if (unfixedN > 0) sb.Append("; nasze z typem pochodnym bez poprawki ").Append(unfixedN).Append(": ").Append(Join(unfixedL));
                sb.Append("; dubel ").Append(dupN).Append(dupL.Count > 0 ? ": " + Join(dupL) : "");
                sb.Append("; cudze z typem pochodnym ").Append(foreignN).Append(foreignL.Count > 0 ? ": " + Join(foreignL) : "");
                if (nullN > 0) sb.Append("; nasze z celem null ").Append(nullN).Append(" (Harmony rzuca wolajacemu)");
                sb.Append(" | Unpatch z typem pochodnym: nasze poprawione ").Append(uFixedN).Append(uFixedL.Count > 0 ? ": " + Join(uFixedL) : "")
                  .Append(", nasze bez poprawki ").Append(uUnfixedN).Append(uUnfixedL.Count > 0 ? ": " + Join(uUnfixedL) : "")
                  .Append(", cudze ").Append(uForeignN).Append(uForeignL.Count > 0 ? ": " + Join(uForeignL) : "");
                int s;
                lock (Gate) s = _stumbles;
                sb.Append(" | potkniecia ").Append(s).Append(".");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Report", e); }
            lock (Gate) _late = 0;
            _reported = true;
        }
    }
}
