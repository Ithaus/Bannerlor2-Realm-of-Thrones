using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 559 (Jeff 15.11 "tak popraw kusze"): PULAP ENERGII BELTU. Wzor RBM (Utilities.calculateMissileSpeed, Utilities.cs:269-277) daje kuszy energie
    /// 0.994 J na funt naciagu: 330 lb = ok. 244 J z beltem bolt_a (0.15 kg), a historyczne kusze reczne (Payne-Gallwey, Williams, Tod) nie szly wiele ponad
    /// ok. 200 J. Pulap (klucz CrossbowBoltEnergyCap, J, 0 = wylaczony): predkosc wylotowa kuszy (uzytek "crossbow" / "crossbow_fast") nie wieksza niz
    /// floor(sqrt(2 x pulap / waga beltu)). Luki, proce i machiny bez zmian. Belty nie przebijaja pancerza mocniej ani slabiej - spada tylko predkosc,
    /// a z nia (wzory RBM 0.5 m v^2) obrazenia, zasieg i opad.
    /// Jedna latka (prefiks + postfiks Priority.Last) na RBMCombat.Utilities.calculateMissileSpeed - z niej RBM bierze predkosc przy rozmieszczeniu
    /// (RangedRework.cs:381, celowanie AI i gracza), przy strzale (RangedRework.cs:630) i w podpowiedzi "RBM Stats" (MagnitudeChanges.cs:809/908).
    /// Prefiks zapamietuje wage beltu: RBM nadpisuje argument w srodku (ammoWeight += lb x 0.00015, Utilities.cs:275) - postfiks dostalby mase zawyzona.
    /// FireRange469.RbmSpeed przycina tym samym wzorem (Clamp) tylko, gdy latka weszla (Live) - inaczej zasieg 469 bylby krotszy niz rzeczywisty.
    /// Latka w kampanii (NightHooks.InCampaign, raz na proces) - jak 413. Nic nie trafia do zapisu gry.
    /// </summary>
    internal static class XbowCap559
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const float BoltFallbackW = 0.15f;   // bolt_a w RBMCombat_arrow_visuals.xml

        private static bool _patched;
        private static string _note = "nie zakladano";

        // liczniki (od poczatku misji 469 - ResetMission w FireRange469.Begin); latka bywa wolana poza glownym watkiem - Interlocked
        private static int _calls, _clipped, _err;
        private static long _cutSum;   // suma (v przed - v po) przycietych wywolan

        /// <summary>Latka weszla i pulap > 0 - wtedy FireRange469 tez przycina.</summary>
        internal static bool Live { get { return _patched && CapJ() > 0f; } }

        private static float CapJ()
        {
            var s = Settings.Current;
            float c = s != null ? s.CrossbowBoltEnergyCap : 190f;
            return c > 0f ? c : 0f;
        }

        private static bool IsXbow(string usage)
        {
            return usage == "crossbow" || usage == "crossbow_fast";
        }

        /// <summary>Najwyzsza predkosc (m/s) beltu o wadze m przy pulapie; int.MaxValue = bez pulapu.</summary>
        private static int Vmax(float m, float cap)
        {
            if (!(cap > 0f) || !(m > 0.001f)) return int.MaxValue;
            return (int)Math.Floor(Math.Sqrt(2.0 * cap / m));
        }

        /// <summary>FireRange469.RbmSpeed: ten sam pulap co latka - tylko gdy latka weszla.</summary>
        internal static int Clamp(float ammoWeight, string usage, int v)
        {
            if (!_patched || !IsXbow(usage)) return v;
            int vc = Vmax(ammoWeight, CapJ());
            return v > vc ? vc : v;
        }

        /// <summary>NightHooks.InCampaign (raz na proces). Zwraca opis do linii "Latki nocy (K3): kampania".</summary>
        internal static string ApplyInCampaign(Harmony h)
        {
            if (h == null) { _note = "brak Harmony"; return _note; }
            if (_patched) return _note;
            Type t = null;
            try { t = AccessTools.TypeByName("RBMCombat.Utilities"); } catch { t = null; }
            if (t == null) { _note = "RBM nieobecny - pulap nieczynny"; Log.Info("Pulap energii beltu (559): " + _note + "."); return _note; }
            MethodInfo mi = null;
            try { mi = AccessTools.DeclaredMethod(t, "calculateMissileSpeed", new[] { typeof(float), typeof(string), typeof(int) }); } catch { mi = null; }
            if (mi == null || !mi.IsStatic || mi.ReturnType != typeof(int))
            {
                _note = "NIE wpiete - brak RBMCombat.Utilities.calculateMissileSpeed(float, string, int)";
                Log.Info("Pulap energii beltu (559): " + _note + " - predkosc kusz jak w RBM, zasieg 469 bez pulapu.");
                return _note;
            }
            try
            {
                h.Patch(mi,
                    prefix: new HarmonyMethod(typeof(XbowCap559), nameof(Pre)),
                    postfix: new HarmonyMethod(typeof(XbowCap559), nameof(Post)) { priority = Priority.Last });
                _patched = Owned(mi, h.Id);
            }
            catch (Exception e) { _patched = false; Log.Error("XbowCap559.Patch", e); }
            var s = Settings.Current;
            _note = (_patched ? "wpiete" : "NIE weszla") + ", pulap " + (s != null ? s.CrossbowBoltEnergyCap.ToString("0", Inv) : "190") + " J";
            Log.Info("Pulap energii beltu (559): latka raz na proces na RBMCombat.Utilities.calculateMissileSpeed - " + _note
                     + ". Kusza: v <= floor(sqrt(2 x pulap / waga beltu)); luki i proce bez zmian; 0 = wylaczony. Tabela kusz w linii startu sesji.");
            return _note;
        }

        private static bool Owned(MethodBase mi, string id)
        {
            try
            {
                var info = Harmony.GetPatchInfo(mi);
                if (info == null) return false;
                bool pre = false, post = false;
                foreach (var p in info.Prefixes) if (p != null && p.owner == id && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(XbowCap559)) pre = true;
                foreach (var p in info.Postfixes) if (p != null && p.owner == id && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(XbowCap559)) post = true;
                return pre && post;
            }
            catch { }
            return false;
        }

        /// <summary>Przed RBM: waga beltu sprzed doliczenia "masy ramion" (RBM zmienia argument w srodku).</summary>
        private static void Pre(float ammoWeight, out float __state)
        {
            __state = ammoWeight;
        }

        /// <summary>Po RBM: dla kusz predkosc przycieta do pulapu energii.</summary>
        private static void Post(string rangedWeaponType, float __state, ref int __result)
        {
            try
            {
                if (!IsXbow(rangedWeaponType)) return;
                Interlocked.Increment(ref _calls);
                int vc = Vmax(__state, CapJ());
                if (__result > vc)
                {
                    Interlocked.Add(ref _cutSum, __result - vc);
                    Interlocked.Increment(ref _clipped);
                    __result = vc;
                }
            }
            catch { Interlocked.Increment(ref _err); }   // potkniecie liczone, wynik RBM zostaje
        }

        /// <summary>FireRange469.Begin: liczniki od zera.</summary>
        internal static void ResetMission()
        {
            Interlocked.Exchange(ref _calls, 0);
            Interlocked.Exchange(ref _clipped, 0);
            Interlocked.Exchange(ref _err, 0);
            Interlocked.Exchange(ref _cutSum, 0L);
        }

        /// <summary>Fragment linii "Ogien w zasiegu (469)".</summary>
        internal static string MissionNote()
        {
            int c = Volatile.Read(ref _calls), k = Volatile.Read(ref _clipped);
            long cut = Interlocked.Read(ref _cutSum);
            return " | pulap beltu 559 (" + _note + "): wywolan predkosci kusz " + c + ", przycietych " + k
                   + (k > 0 ? " (sr. o " + ((double)cut / k).ToString("0.0", Inv) + " m/s)" : "") + ", potkniec " + Volatile.Read(ref _err);
        }

        /// <summary>Waga beltu bolt_a z danych (sztuka), zapas 0.15 kg.</summary>
        internal static float BoltAWeight()
        {
            try
            {
                var it = MBObjectManager.Instance.GetObject<ItemObject>("bolt_a");
                if (it != null && it.Weight > 0.001f && it.Weight < 1f) return it.Weight;
            }
            catch { }
            return BoltFallbackW;
        }

        /// <summary>FireRange469.StartTable (start sesji, przy RBM): kusze w grze pogrupowane wedlug uzytku i naciagu, przed / po pulapie z beltem bolt_a.</summary>
        internal static void SessionTable(float boltW)
        {
            try
            {
                float cap = CapJ();
                var groups = new SortedDictionary<string, int[]>(StringComparer.Ordinal);   // klucz "uzytek lb" -> [lb, ile]
                var first = new Dictionary<string, string>();
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (it == null || it.ItemType != ItemObject.ItemTypeEnum.Crossbow || it.PrimaryWeapon == null) continue;
                    var w = it.PrimaryWeapon;
                    string u = w.ItemUsage ?? "";
                    if (!IsXbow(u)) continue;
                    int lb = w.MissileSpeed;
                    string key = u + " " + lb.ToString("0000", Inv);
                    int[] g;
                    if (!groups.TryGetValue(key, out g)) { g = new[] { lb, 0 }; groups[key] = g; first[key] = it.StringId; }
                    g[1]++;
                }
                var sb = new StringBuilder(1500);
                sb.Append("Pulap energii beltu (559): start sesji | ").Append(_note).Append(", klucz ").Append(cap > 0f ? cap.ToString("0", Inv) + " J" : "0 (wylaczony)")
                  .Append(", belt bolt_a ").Append(boltW.ToString("0.000", Inv)).Append(" kg | kusze w grze (uzytek, naciag lb, ile, przyklad: v m/s przed->po, J przed->po):");
                int clip = 0;
                foreach (var kv in groups)
                {
                    int lb = kv.Value[0];
                    string u = kv.Key.Substring(0, kv.Key.IndexOf(' '));
                    int v0 = FireRange469.RbmSpeedRaw(boltW, u, lb);
                    int vc = Vmax(boltW, cap);
                    int v1 = v0 > vc ? vc : v0;
                    if (v1 < v0) clip += kv.Value[1];
                    sb.Append(' ').Append(u).Append(' ').Append(lb).Append(" x").Append(kv.Value[1]).Append(' ').Append(first[kv.Key]).Append(": ")
                      .Append(v0).Append("->").Append(v1).Append(' ').Append((0.5 * boltW * v0 * v0).ToString("0", Inv)).Append("->").Append((0.5 * boltW * v1 * v1).ToString("0", Inv)).Append(';');
                }
                if (groups.Count == 0) sb.Append(" brak kusz w danych");
                sb.Append(" | przycietych kusz (bolt_a): ").Append(clip).Append(_patched ? "" : " (latka nie wpieta - w bitwie bez zmian)").Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Log.Error("XbowCap559.SessionTable", e); }
        }
    }
}
