using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// R3-05b (p5-handel, noc 20/21.10; Jeff 20.10: "pozostale tak") - BARKI RZECZNE (klucz RiverBarges). Zrodlo: docs/POMYSLY-35-2026-10-20.md poz. 9, audyt 10-pomysly
    /// Z9 (Masschaele 1993: w Anglii XIV w. przewoz ladem ok. 2 x drozszy niz rzeka; miasta nad splawnymi rzekami - 1/5 grodow - mialy w 1334 r. ponad polowe majatku miast).
    /// Regula: droga miedzy dwiema osadami nad TA SAMA rzeka liczy sie jak Factor (polowa) drogi ladowej - tylko w rachunku, barek na mapie nie ma (wozy i karawany jada
    /// trasa gry). Osada nad rzeka = wpis w ModuleData\arm_rivers.tsv (recznie z mapy ROT 8.1.8: rzeka, id osady, nazwa). Wies nalezy do rzeki swojej osady (Village.Bound -
    /// miasto albo zamek), chyba ze sama jest w pliku. Uklad rzeki (dorzecze z doplywami: Trident z Widlami, Rhoyne z Noyne i Qhoyne) to jedna rzeka.
    ///  - WOZ WSI (MarketCarts.Choose, miernik utargu na dobe): gdy wies i miasto sa nad ta sama rzeka, droga do ZASIEGU (MarketMaxDistance) liczy sie x Factor -
    ///    barka poszerza rynek wsi wzdluz rzeki (do 2 x dalej). Czas kursu i przestoj magazynu wsi (doby magazynu / doby kursu) dalej z prawdziwej drogi, bo woz jedzie
    ///    caly kurs ladem (recenzja p5-handel: polowa czasu w mierniku tylko ukrywala przestoj, ktory naprawde nastepuje) - dalekie miasto rzeczne wygrywa tylko wtedy,
    ///    gdy mimo dluzszej jazdy daje wiecej na dobe (np. bliskie miasta zawalone tym towarem). W mierniku wozu nie ma innego kosztu drogi niz czas, wiec nic wiecej
    ///    barka uczciwie nie obniza.
    ///  - KARAWANA (BK BKCaravansBehavior.GetTradeScoreForTown, postfiks): ocena miasta BK = zysk x 1 / (droga + kara dalekiej drogi + 8). Gdy osada, w ktorej karawana stoi
    ///    (albo z ktorej wyjechala), i miasto celu sa nad ta sama rzeka, wynik x (d + kara(d) + dom + 8) / (d/2 + kara(d/2) + dom + 8) - ta sama formula BK z polowa drogi
    ///    (progi kary z sredniej odleglosci miast, jak BK i BKROT; czlon powrotu do domu bez zmian). Wynik -1 (odciety) zostaje.
    /// Nic nie przenosi zlota ani towaru - zmienia tylko wybor celu, wiec zamknieta ekonomia bez zmian. Bez pliku - nieczynne (linia startu mowi dlaczego).
    /// Liczniki (Interlocked - ocena BK moze biec poza watkiem glownym), slownik osad tylko do odczytu po starcie sesji. Linie "Barki rzeczne (R3-05b): start sesji" i "dzien N".
    /// </summary>
    internal static class RiverBarges
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal const string FileName = "arm_rivers.tsv";
        internal const float Factor = 0.5f;

        private static volatile Dictionary<Settlement, int> _river;                  // osada -> numer rzeki (od 1); tylko odczyt po zbudowaniu
        private static readonly List<string> _names = new List<string>();
        private static string _loadNote = "nie wczytano";
        private static int _listed, _missing;
        private static Harmony _h; private static bool _hookTried, _hooked;          // raz na proces
        private static volatile bool _limits; private static float _lVeryFar, _lFar, _lMedium, _lClose;

        // liczniki od ostatniej linii
        private static int _cartEval, _cartExtra, _cartPick, _cartPickFar, _cartPickAll, _carScore, _stumbles, _stumbleSess;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static readonly object Gate = new object();

        internal static bool On { get { var s = Settings.Current; return s != null && s.RiverBarges; } }

        internal static void Reset()
        {
            _river = null; _limits = false; _loadNote = "nie wczytano"; _listed = _missing = 0;
            lock (Gate) { _names.Clear(); _err.Clear(); }
            _stumbleSess = 0;
            ZeroDay();
        }

        private static void ZeroDay() { _cartEval = _cartExtra = _cartPick = _cartPickFar = _cartPickAll = _carScore = _stumbles = 0; }

        private static void Stumble(string where, Exception e)
        {
            Interlocked.Increment(ref _stumbles); Interlocked.Increment(ref _stumbleSess);
            try { bool first; lock (Gate) first = _err.Add(where); if (first) Log.Error("RiverBarges." + where, e); } catch { }
        }

        // ------------------------------------------------------------ dane
        private static void Load()
        {
            var map = new Dictionary<Settlement, int>();
            var names = new List<string>();
            _listed = _missing = 0;
            string path = null;
            try { path = MapVillageData.DataPathFor(FileName); } catch { path = null; }
            if (path == null || !System.IO.File.Exists(path)) { _loadNote = "BRAK pliku ModuleData\\" + FileName; _river = map; return; }
            try
            {
                var idx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var miss = new List<string>();
                foreach (var raw in System.IO.File.ReadAllLines(path))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    var c = line.Split('\t');
                    if (c.Length < 2) continue;
                    string r = c[0].Trim(), id = c[1].Trim();
                    if (r.Length == 0 || id.Length == 0) continue;
                    _listed++;
                    int n;
                    if (!idx.TryGetValue(r, out n)) { n = names.Count + 1; idx[r] = n; names.Add(r); }
                    Settlement st = null;
                    try { st = Settlement.Find(id); } catch { st = null; }
                    if (st == null) { _missing++; if (miss.Count < 8) miss.Add(id); continue; }
                    map[st] = n;
                }
                _loadNote = "plik " + FileName + ": wpisow " + _listed + ", osad na mapie " + map.Count + (_missing > 0 ? ", BRAK na mapie " + _missing + " [" + string.Join(", ", miss.ToArray()) + "]" : "");
            }
            catch (Exception e) { _loadNote = "blad czytania pliku"; Stumble("Load", e); }
            lock (Gate) { _names.Clear(); _names.AddRange(names); }
            _river = map;
        }

        /// <summary>Rzeka osady (0 - zadna): wpis w pliku, a dla wsi bez wpisu - rzeka jej osady (Bound).</summary>
        private static int RiverOf(Dictionary<Settlement, int> map, Settlement st)
        {
            if (st == null) return 0;
            int r;
            if (map.TryGetValue(st, out r)) return r;
            var v = st.Village;
            if (v != null && v.Bound != null && map.TryGetValue(v.Bound, out r)) return r;
            return 0;
        }

        // ------------------------------------------------------------ sesja
        internal static void SessionStart()
        {
            Load();
            EnsureHooks();
            try
            {
                var map = _river;
                var sb = new StringBuilder(900);
                sb.Append("Barki rzeczne (R3-05b): start sesji | klucz River Barges ").Append(On ? "tak" : "nie").Append(" | ").Append(_loadNote)
                  .Append(" | droga nad ta sama rzeka x").Append(Factor.ToString("0.##", Inv)).Append(" w zasiegu wozu wsi (czas kursu i przestoj magazynu z prawdziwej drogi) i w ocenie miast karawan BK | latka ocena miast BK ")
                  .Append(_hooked ? "wpieta" : "NIE").Append(" | rzeki: ");
                if (map != null)
                {
                    var cnt = new Dictionary<int, int>();
                    int vil = 0;
                    foreach (var kv in map) { int c0; cnt.TryGetValue(kv.Value, out c0); cnt[kv.Value] = c0 + 1; }
                    foreach (var st in Settlement.All) if (st != null && st.IsVillage && RiverOf(map, st) > 0) vil++;
                    List<string> names; lock (Gate) names = new List<string>(_names);
                    for (int i = 0; i < names.Count; i++) { int c1; cnt.TryGetValue(i + 1, out c1); if (i > 0) sb.Append(", "); sb.Append(names[i]).Append(' ').Append(c1); }
                    sb.Append(" (osad), wsi nad rzekami ").Append(vil);
                }
                sb.Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        private static void EnsureHooks()
        {
            if (_hookTried) return;
            _hookTried = true;
            try
            {
                var bk = AccessTools.TypeByName("BannerKings.Behaviours.BKCaravansBehavior");
                var m = bk != null ? AccessTools.DeclaredMethod(bk, "GetTradeScoreForTown") : null;
                var ps = m != null ? m.GetParameters() : null;
                if (m == null || m.ReturnType != typeof(float) || ps.Length < 3 || ps[0].ParameterType != typeof(MobileParty) || ps[1].ParameterType != typeof(Town) || ps[2].ParameterType != typeof(CampaignTime))
                {
                    Log.Info("Barki rzeczne (R3-05b): BRAK BKCaravansBehavior.GetTradeScoreForTown(MobileParty, Town, CampaignTime, ..) - karawany bez barek (wozy wsi dzialaja).");
                    return;
                }
                _h = _h ?? new Harmony("armoury.p5handel.barges");
                _h.Patch(m, postfix: new HarmonyMethod(typeof(RiverBarges), nameof(ScorePostfix)));
                _hooked = true;
            }
            catch (Exception e) { Log.Error("RiverBarges.EnsureHooks", e); }
        }

        // ------------------------------------------------------------ woz wsi (MarketCarts.Choose)
        /// <summary>Mnoznik drogi do testu zasiegu wozu: Factor, gdy wies (home) i miasto (to) sa nad ta sama rzeka; inaczej 1. Nie rzuca.</summary>
        internal static double CartFactor(Settlement home, Settlement to)
        {
            try
            {
                var map = _river;
                if (map == null || map.Count == 0 || !On) return 1.0;
                int a = RiverOf(map, home);
                if (a <= 0 || a != RiverOf(map, to)) return 1.0;
                Interlocked.Increment(ref _cartEval);
                return Factor;
            }
            catch (Exception e) { Stumble("CartFactor", e); return 1.0; }
        }

        /// <summary>Miasto weszlo do wyboru wozu tylko dzieki barce (droga ladowa ponad zasieg, polowa w zasiegu). Nie rzuca.</summary>
        internal static void NoteCartExtra() { Interlocked.Increment(ref _cartExtra); }

        /// <summary>Woz wybral miasto (licznik linii: ile wyborow, ile z nich nad ta sama rzeka, ile z nich poza zasiegiem ladowym - far). Nie rzuca.</summary>
        internal static void NoteCartPick(Settlement home, Settlement to, bool far)
        {
            try
            {
                var map = _river;
                if (map == null || map.Count == 0 || !On) return;
                Interlocked.Increment(ref _cartPickAll);
                int a = RiverOf(map, home);
                if (a > 0 && a == RiverOf(map, to)) { Interlocked.Increment(ref _cartPick); if (far) Interlocked.Increment(ref _cartPickFar); }
            }
            catch (Exception e) { Stumble("NoteCartPick", e); }
        }

        // ------------------------------------------------------------ karawana (BK GetTradeScoreForTown)
        public static void ScorePostfix(MobileParty __0, Town __1, CampaignTime __2, ref float __result)
        {
            if (!(__result > 0f) || __0 == null || __1 == null) return;
            if (CaravanSea308.ScoresBySea(__0)) return;   // 308a: ocena BK karawany ze statkami liczy juz droge morska - mnoznik barek (droga ladowa) nie dotyczy
            try
            {
                var map = _river;
                if (map == null || map.Count == 0 || !On) return;
                var to = __1.Settlement;
                int rt = RiverOf(map, to);
                if (rt <= 0) return;
                var from = __0.CurrentSettlement ?? __0.LastVisitedSettlement;
                if (from == null || from == to || RiverOf(map, from) != rt) return;
                var c = Campaign.Current;
                if (c == null) return;
                var dm = c.Models.MapDistanceModel;
                float lr;
                float d = dm.GetDistance(__0, to, false, MobileParty.NavigationType.Default, out lr);
                if (!(d > 0f) || float.IsInfinity(d)) return;
                if (!_limits) Limits(c);
                float home = 0f;
                float e = __2.ElapsedDaysUntilNow;
                if (e > 2f && __0.HomeSettlement != null)
                {
                    float d2 = dm.GetDistance(to, __0.HomeSettlement, false, false, MobileParty.NavigationType.Default);
                    home = Far(d2, ((e - 1f) * (float)Math.Sqrt(e - 1f) - 1f) * 0.008f);
                }
                float full = d + Far(d, 0.15f) + home + 8f, half = Factor * d + Far(Factor * d, 0.15f) + home + 8f;
                if (!(half > 0f) || float.IsInfinity(full)) return;
                __result *= full / half;
                Interlocked.Increment(ref _carScore);
            }
            catch (Exception ex) { Stumble("ScorePostfix", ex); }
        }

        /// <summary>Progi kary dalekiej drogi BK (BKCaravansBehavior.DistanceLimit*; te same liczy BKROT) - raz na sesje.</summary>
        private static void Limits(Campaign c)
        {
            float a = 0f;
            try { a = c.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.Default); } catch { a = 0f; }
            _lVeryFar = (508f + 9f * a) / 2f; _lFar = (381f + 6.75f * a) / 2f; _lMedium = (254f + 4.5f * a) / 2f; _lClose = (127f + 2.25f * a) / 2f;
            _limits = true;
        }

        /// <summary>Kara dalekiej drogi jak BK AdjustVeryFarAddition.</summary>
        private static float Far(float d, float min)
        {
            float add = 0f;
            if (d > _lVeryFar) add += (d - _lVeryFar) * min * 4f;
            if (d > _lFar) add += (d - _lFar) * min * 3f;
            if (d > _lMedium) add += (d - _lMedium) * min * 2f;
            if (d > _lClose) add += (d - _lClose) * min;
            return add;
        }

        // ------------------------------------------------------------ linia doby
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            try
            {
                if (_river == null) SessionStart();
                int day = (int)CampaignTime.Now.ToDays - 1;
                var map = _river;
                Log.Info("Barki rzeczne (R3-05b): dzien " + day + " | " + (s.RiverBarges ? "CZYNNE" : "WYLACZONE (MCM)") + " | osad nad rzekami " + (map != null ? map.Count : 0)
                         + " | wozy wsi: par wies-miasto nad ta sama rzeka " + _cartEval + ", miast w wyborze tylko dzieki barce " + _cartExtra + ", wyborow " + _cartPickAll
                         + ", w tym miasto nad rzeka wsi " + _cartPick + " (z tego poza zasiegiem ladowym " + _cartPickFar + ")"
                         + " | karawany BK: ocen miast z barka " + _carScore + " | potkniecia " + _stumbles + " (sesja " + _stumbleSess + ").");
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ZeroDay(); }
        }
    }
}
