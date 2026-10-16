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
    ///     cudze latki - tylko licznik, bez zmian. Postfiks liczy udane proby (biegnie tylko przy sukcesie), finalizer zapisuje blad proby
    ///     (priorytet First - widzi wyjatek przed finalizerem BKROTPatch; void, niczego nie polyka).
    /// (C) Report - jedna linia "Latki czynnego modelu (HT): start sesji" po OnSessionLaunched wszystkich modow (OnAfterSessionLaunched), potem
    ///     najwyzej 20 linii "po starcie" na sesje dla prob, ktore po raporcie sie nie udaly albo zostaly poprawione.
    /// Wszystko w try/catch z licznikiem potkniec: wyjatek w prefiksie / postfiksie / finalizerze PatchProcessor.Patch zepsulby latanie calej gry.
    /// Harmony.GetPatchInfo pytac zawsze o metode deklarujaca - dla MethodInfo z typem pochodnym zwraca null takze po udanej latce.
    /// </summary>
    internal static class PatchTarget
    {
        private const string Ours = "com.jeff.";
        private const string Head = "Latki czynnego modelu (HT): ";
        private const int LateMax = 20;
        private const BindingFlags Decl = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private static readonly object Gate = new object();

        // wpisy szukania celu: etykieta -> ostatnie szukanie w tym procesie (latki sa raz na proces, wiec raport kazdej sesji pokazuje wszystkie)
        private sealed class Entry { internal string Label, Active, Name, Chain, Why; internal MethodInfo Method; internal bool ViaDecl; }
        private static readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private static readonly List<string> _order = new List<string>();

        /// <summary>Proba latki naszego wlasciciela przez PatchProcessor.Patch (stan miedzy prefiksem, postfiksem i finalizerem straznika).</summary>
        internal sealed class Attempt { internal string Id, Err, From; internal MethodBase M; internal bool Ok, Fixed; }
        private static readonly List<Attempt> _tries = new List<Attempt>();                 // od startu procesu
        private static readonly List<string> _fixed = new List<string>(), _unfixed = new List<string>(), _foreign = new List<string>();
        private static int _fixedN, _unfixedN, _foreignN, _nullN, _late, _stumbles;
        private static FieldInfo _fOrig, _fInst;
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
        /// do wolajacego jak dotad (wpis z "wyjatek").</summary>
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
                    if (!_entries.ContainsKey(e.Label)) _order.Add(e.Label);
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

        // ------------------------------------------------------------ (B) straznik na PatchProcessor.Patch
        /// <summary>SubModuleMain.OnSubModuleLoad (Armoury laduje sie przed ForgeView, GT i BKROTPatch): raz na proces. Pola PatchProcessor z Harmony 2.4.2
        /// ("original", "instance"); brak pola = straznik BRAK, bez wywrotki (inna wersja Harmony).</summary>
        internal static void InstallGuard()
        {
            if (_installed) return;
            _installed = true;
            try
            {
                _fOrig = AccessTools.Field(typeof(PatchProcessor), "original");
                _fInst = AccessTools.Field(typeof(PatchProcessor), "instance");
                var pp = AccessTools.DeclaredMethod(typeof(PatchProcessor), "Patch", Type.EmptyTypes);
                if (_fOrig == null || _fInst == null || !typeof(MethodBase).IsAssignableFrom(_fOrig.FieldType) || !typeof(Harmony).IsAssignableFrom(_fInst.FieldType))
                    _guard = "BRAK (pola PatchProcessor)";
                else if (pp == null) _guard = "BRAK (PatchProcessor.Patch)";
                else
                {
                    new Harmony("com.jeff.armoury.patchguard").Patch(pp,
                        prefix: new HarmonyMethod(typeof(PatchTarget), nameof(GuardPre)) { priority = Priority.First },
                        postfix: new HarmonyMethod(typeof(PatchTarget), nameof(GuardPost)) { priority = Priority.Last },
                        finalizer: new HarmonyMethod(typeof(PatchTarget), nameof(GuardFin)) { priority = Priority.First });
                    _wired = HasOwner(Harmony.GetPatchInfo(pp), "com.jeff.armoury.patchguard");
                    _guard = _wired ? "wpiety" : "BRAK (latka nie weszla)";
                }
            }
            catch (Exception e) { _guard = "BRAK (wyjatek " + e.GetType().Name + ")"; Stumble("InstallGuard", e); }
            Log.Info(Head + "straznik Harmony " + _guard + " (PatchProcessor.Patch: prefiks First, postfiks Last, finalizer First) | klucz PatchTargetGuard "
                     + (GuardOn ? "TAK" : "NIE") + " (z pliku ustawien Armoury.settings.xml; wartosc z MCM od startu kampanii - OnGameStart).");
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

        /// <summary>Prefiks PatchProcessor.Patch (Priority.First). Nigdy nie przerywa latania (void) i nigdy nie rzuca.</summary>
        public static void GuardPre(PatchProcessor __instance, out Attempt __state)
        {
            __state = null;
            try
            {
                if (__instance == null || _fOrig == null || _fInst == null) return;
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
                string from = null;
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
                            _fOrig.SetValue(__instance, d);
                            lock (Gate) { _fixedN++; if (_fixed.Count < 10) _fixed.Add(id + " " + Name(o) + " -> " + Name(d)); }
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
                if (!ours) return;
                var a = new Attempt { Id = id, M = o, Fixed = fix, From = from };
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
                if (__state.Fixed && _reported) Late("poprawiona", __state);
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
                if (_reported) Late("NIE", __state);
            }
            catch (Exception e) { Stumble("GuardFin", e); }
        }

        private static void Counts(out int tries, out int ok)
        {
            tries = ok = 0;
            lock (Gate)
                foreach (var a in _tries) { tries++; if (a.Ok) ok++; }
        }

        private static void Late(string kind, Attempt a)
        {
            lock (Gate) { if (_late >= LateMax) return; _late++; }
            int p, w;
            Counts(out p, out w);
            Log.Info(Head + "po starcie - " + kind + " " + a.Id + " " + (a.From != null ? a.From + " -> " : "") + Name(a.M) + (a.Err != null ? " (" + a.Err + ")" : "") + " (proby " + p + ", udane " + w + ").");
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
                List<string> fixedL, unfixedL, foreignL;
                int fixedN, unfixedN, foreignN, nullN;
                lock (Gate)
                {
                    list = new List<Entry>(_order.Count);
                    foreach (var l in _order) list.Add(_entries[l]);
                    tries = new List<Attempt>(_tries);
                    fixedL = new List<string>(_fixed); unfixedL = new List<string>(_unfixed); foreignL = new List<string>(_foreign);
                    fixedN = _fixedN; unfixedN = _unfixedN; foreignN = _foreignN; nullN = _nullN;
                }
                int wired = 0, direct = 0;
                var via = new List<string>(); var none = new List<string>(); var no = new List<string>(); var chains = new List<string>();
                foreach (var e in list)
                {
                    try
                    {
                        if (e.Chain != null && e.Chain.IndexOf(" -> ", StringComparison.Ordinal) >= 0) chains.Add(e.Label + " " + e.Chain);
                        if (e.Method == null) { no.Add(e.Label + " " + e.Active + "." + e.Name + " (" + (e.Why ?? "brak metody") + ")"); continue; }
                        bool tried = false, ok = false;
                        string err = null;
                        foreach (var a in tries)
                            if (Same(a.M, e.Method)) { tried = true; if (a.Ok) ok = true; else if (err == null) err = a.Err; }
                        // proba byla: wynik proby (inna nasza latka na tej metodzie nie zaslania nieudanej); proby nie bylo: wlasciciel com.jeff.* w latkach
                        bool w = tried ? ok : HasOwner(Harmony.GetPatchInfo(e.Method), null);
                        string where = e.ViaDecl ? e.Active + " -> " + e.Method.DeclaringType.Name + "." + e.Method.Name : Name(e.Method);
                        if (w)
                        {
                            wired++;
                            if (e.ViaDecl) via.Add(e.Label + " " + where); else direct++;
                        }
                        else if (!tried) none.Add(e.Label + " " + where);
                        else no.Add(e.Label + " " + where + (err != null ? " (" + err + ")" : ""));
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
                var sb = new StringBuilder(1600);
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
                sb.Append("; cudze z typem pochodnym ").Append(foreignN).Append(foreignL.Count > 0 ? ": " + Join(foreignL) : "");
                if (nullN > 0) sb.Append("; nasze z celem null ").Append(nullN).Append(" (Harmony rzuca wolajacemu)");
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
