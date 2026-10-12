using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// E6/W3 (paczka 221, PLAN 6.4 + krok 6.3; projekt etapu 6 v2 rozdz. 3 "E6/W3", R5/R6; krytyka K1-03, KB W1-ksiega) - JEDEN REGULATOR WSI.
    /// Jedna zasada: wies robi tyle, ile swiat zuzywa. Dotad towar bez zbytu rosl na polkach i znikal przez gnicie BK (RawNoRot 174.3: 135 szt./d w nicosc,
    /// glownie sol, drewno, glina), a towaru chcianego nie przybywalo (krok x1.3 K13 132 - v2 tutaj, nie w W1).
    ///
    /// Towary: kazdy towar handlowy wsi poza zywnoscia i zwierzetami (IsTradeGood, !IsFood, !IsAnimal - ten sam zbior co gnicie BK; konie to K4).
    /// Pomiar (wlasne liczniki, niezalezne od LogEnabled i od ksiegi 146):
    ///  - produkcja wsi = przyrost magazynu wsi w ticku produkcji (prefiks Priority.First przed BK i postfiks Priority.Last po lesie wsi 126 na
    ///    VillageGoodProductionCampaignBehavior.TickGoodProduction) - dokladnie to, co gra albo BK dopisaly (takze las wsi); drewno lasu, ktore
    ///    poszlo prosto do dymarki kopalni (R1), dopisuje VillageWoodlot (MadeOffRoster) - to produkcja i od razu zuzycie;
    ///  - skasowane = to, co zdjal RawNoRot (gnicie 174.3) przed pierwsza decyzja - nie jest zuzyciem;
    ///  - stos swiata = polki osad, magazyny wsi, schowki, wszystkie partie (tabory, karawany, lordowie, gracz) i wozy kupcow w drodze (TradeCarts),
    ///    liczony raz na 28 dob (na granicy kubelka), nie co dobe;
    ///  - zuzycie okna = produkcja wsi - skasowane - (stos teraz - stos 56 dob temu), najmniej 0 - to, co swiat zuzyl ponad inne zrodla (warsztaty).
    /// Decyzja co 28 dob na oknie 56 dob (dwa kubelki), pierwsza 56 dob po starcie pomiaru (nowa kampania - d56); wczesniej mnozniki 1 i gnicie 174.3:
    ///  - w dol: zuzycie < 90% produkcji i stos > ValveStockDaysHigh (60) dni zuzycia -> mnoznik = 0.95 x zuzycie/produkcja bazowa okna (przy mnozniku 1; cel ponizej zuzycia -
    ///    stos maleje), najwyzej x0.5 na decyzje, podloga 0.1; towar bez odbiorcy - prog w dniach produkcji zamiast zuzycia (schodzi do podlogi);
    ///  - w gore: zuzycie >= 98% produkcji i stos < ValveStockDaysLow (30) dni zuzycia -> x1.3, najpierw najwyzej do 1 (po cieciu), potem najwyzej
    ///    VillageOutputStep (1.3) ponad baze - tylko produkcja, bez mnoznika popytu (D6-4);
    ///  - pomiedzy - bez zmian (martwa strefa: stos zostaje miedzy 30 a 60 dniami zuzycia, bez zapadki i kolysania).
    /// Mnoznik dziala w MaterialLaw.ProdPostfix (czynnik x suma, jak inne czynniki; przed W2 i sufitem dymarki R1) i w lesie wsi (VillageWoodlot.PiecesPerDay -
    /// tick i magazyn). Od pierwszej decyzji RawNoRot nie kasuje towarow (stawka 0; wyroby gracza i sztandary kasuje dalej jak BK) - nic w nicosc.
    /// Stan w zapisie (arm_harvestvalve): mnozniki, kubelki produkcji i skasowanych, stosy na granicach kubelkow, doby; brak klucza (stary zapis) - pomiar
    /// od pierwszej doby po wczytaniu. Wylaczony klucz - stan czyszczony (mnozniki 1, gnicie 174.3), wlaczenie zaczyna pomiar od nowa.
    /// Koszt: dwa przebiegi malego magazynu wsi w jej ticku produkcji, odczyt slownika w modelu wsi, pelny spis stosu raz na 28 dob.
    /// </summary>
    internal static class HarvestValve
    {
        private const int Bucket = 28;             // doby kubelka; decyzja co kubelek, okno = dwa kubelki
        private const float CutBelow = 0.90f, StepAbove = 0.98f, CutTarget = 0.95f, CutMaxStep = 0.5f, Floor = 0.1f, UpStep = 1.3f;

        private sealed class Good
        {
            public string Id;
            public ItemObject Item;
            public float M = 1f;                   // mnoznik produkcji wsi (1 = jak model)
            public float MA = 1f;                  // mnoznik, przy ktorym powstala produkcja kubelka starszego (P0); biezacy P1 powstal przy M
            public double P0, P1;                  // produkcja wsi: kubelek starszy / biezacy (szt.)
            public double D0, D1;                  // skasowane przez RawNoRot: kubelek starszy / biezacy (szt.)
            public long SA = -1, SB = -1;          // stos swiata na poczatku kubelka starszego / biezacego (-1 = nieznany)
            public int Dir;                        // ostatnia decyzja: -1 w dol, 0 bez zmian, 1 w gore
            public int Flips;                      // zmiany kierunku w dwoch kolejnych decyzjach (od startu pomiaru)
        }

        private sealed class RefEq : IEqualityComparer<ItemObject>
        {
            public bool Equals(ItemObject a, ItemObject b) { return ReferenceEquals(a, b); }
            public int GetHashCode(ItemObject o) { return RuntimeHelpers.GetHashCode(o); }
        }

        internal static readonly TextObject Text = new TextObject("{=!}Armoury: village output follows what the world uses up");
        private static readonly Dictionary<string, Good> _goods = new Dictionary<string, Good>(StringComparer.Ordinal);
        private static readonly Dictionary<ItemObject, Good> _byItem = new Dictionary<ItemObject, Good>(new RefEq());
        private static volatile Dictionary<ItemObject, float> _factors;   // mnozniki != 1 (podmieniane w calosci - model wsi czyta bez zamka)
        private static int _start = -1, _bucketStart = -1, _buckets, _decisions, _firstDecisionDay = -1;
        private static bool _wired, _dirty, _sessionLine;
        private static int _stumbles;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        // tick produkcji wsi (tylko watek glowny)
        private static Village _tv;
        private static readonly Dictionary<ItemObject, int> _before = new Dictionary<ItemObject, int>(new RefEq());
        private static readonly Dictionary<ItemObject, int> _after = new Dictionary<ItemObject, int>(new RefEq());

        /// <summary>Regulator liczy i decyduje: klucz wlaczony i tick produkcji wsi wpiety (bez niego nie ma pomiaru produkcji).</summary>
        internal static bool On { get { var s = Settings.Current; return s != null && s.HarvestValve && _wired; } }

        /// <summary>Po pierwszej decyzji: RawNoRot nie kasuje towarow (stawka 0).</summary>
        internal static bool Active { get { return On && _decisions > 0; } }

        internal static void Reset()
        {
            Clear();
            _stumbles = 0; _errSites.Clear();
            _tv = null; _before.Clear(); _after.Clear();
            _dirty = false; _sessionLine = false;
        }

        private static void Clear()
        {
            _goods.Clear(); _byItem.Clear(); _factors = null;
            _start = -1; _bucketStart = -1; _buckets = 0; _decisions = 0; _firstDecisionDay = -1;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errSites.Add(where)) Log.Error("HarvestValve." + where, e);
        }

        private static bool Eligible(ItemObject it) { return it != null && it.IsTradeGood && !it.IsFood && !it.IsAnimal; }

        /// <summary>Mnoznik produkcji wsi dla towaru (1 = bez zmian). Model wsi i las wsi; czytane z kazdego watku.</summary>
        internal static float Factor(ItemObject item)
        {
            var d = _factors;
            float f;
            return d != null && item != null && On && d.TryGetValue(item, out f) ? f : 1f;
        }

        private static Good Get(ItemObject item)
        {
            Good g;
            if (_byItem.TryGetValue(item, out g)) return g;
            string id = item.StringId;
            if (string.IsNullOrEmpty(id)) return null;
            if (!_goods.TryGetValue(id, out g))
            {
                // spis stosu obejmuje kazdy towar z zakresu obecny w swiecie - towar bez stanu nie byl w zadnym spisie, wiec jego stos tam byl 0
                g = new Good { Id = id, SB = _bucketStart >= 0 ? 0 : -1, SA = _buckets >= 1 ? 0 : -1 };
                _goods[id] = g;
            }
            g.Item = item;
            _byItem[item] = g;
            return g;
        }

        /// <summary>Przedmioty ze stanu (zapis) i slownik mnoznikow - po wczytaniu, gdy menedzer obiektow jest gotowy.</summary>
        private static void Rebuild()
        {
            var mgr = MBObjectManager.Instance;
            if (mgr == null) { _dirty = true; return; }
            _byItem.Clear();
            var f = new Dictionary<ItemObject, float>(new RefEq());
            foreach (var g in _goods.Values)
            {
                if (g.Item == null) { try { g.Item = mgr.GetObject<ItemObject>(g.Id); } catch { g.Item = null; } }
                if (g.Item == null) continue;
                _byItem[g.Item] = g;
                if (g.M != 1f) f[g.Item] = g.M;
            }
            _factors = f.Count > 0 ? f : null;
            _dirty = false;
        }

        // ------------------------------------------------------------ pomiar produkcji
        /// <summary>Prefiks TickGoodProduction(Village, bool), Priority.First (przed BK): stan magazynu wsi (towary z zakresu, suma po przedmiocie).
        /// Rozdanie startowe nowej kampanii (initialProductionForTowns) i doby przed startem pomiaru - bez liczenia.</summary>
        public static void TickPrefix(Village __0, bool __1)
        {
            _tv = null;
            if (__1 || __0 == null || _start < 0 || !On) return;
            try
            {
                var r = __0.Settlement != null ? __0.Settlement.ItemRoster : null;
                if (r == null) return;
                Count(r, _before);
                _tv = __0;
            }
            catch (Exception e) { _tv = null; Stumble("TickPrefix", e); }
        }

        /// <summary>Postfiks tego samego ticku (Priority.Last - po lesie wsi 126 i dymarce R1): przyrost magazynu wsi = produkcja wsi.</summary>
        public static void TickPostfix(Village __0)
        {
            var v = _tv;
            _tv = null;
            if (v == null || !ReferenceEquals(v, __0)) return;
            try
            {
                Count(v.Settlement.ItemRoster, _after);
                foreach (var kv in _after)
                {
                    int b;
                    _before.TryGetValue(kv.Key, out b);
                    if (kv.Value > b) { var g = Get(kv.Key); if (g != null) g.P1 += kv.Value - b; }
                }
            }
            catch (Exception e) { Stumble("TickPostfix", e); }
        }

        public static Exception TickFinalizer(Exception __exception) { _tv = null; return __exception; }

        private static void Count(ItemRoster r, Dictionary<ItemObject, int> into)
        {
            into.Clear();
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (el.Amount == 0 || !Eligible(it)) continue;
                int n;
                into.TryGetValue(it, out n);
                into[it] = n + el.Amount;
            }
        }

        /// <summary>Produkcja wsi poza magazynem wsi (drewno lasu prosto do dymarki kopalni, R1) - w kubelku produkcji; stos jej nie widzi, wiec to od razu zuzycie.</summary>
        internal static void MadeOffRoster(ItemObject item, int n)
        {
            if (n <= 0 || _start < 0 || !On || !Eligible(item)) return;
            try { var g = Get(item); if (g != null) g.P1 += n; }
            catch (Exception e) { Stumble("MadeOffRoster", e); }
        }

        /// <summary>RawNoRot.Prefix: sztuki skasowane przez gnicie (przed pierwsza decyzja) - nie sa zuzyciem.</summary>
        internal static void NoteDecay(ItemObject item, int n)
        {
            if (n <= 0 || _start < 0 || !On || !Eligible(item)) return;
            try { var g = Get(item); if (g != null) g.D1 += n; }
            catch (Exception e) { Stumble("NoteDecay", e); }
        }

        // ------------------------------------------------------------ stos swiata
        private static Dictionary<ItemObject, long> Census()
        {
            var c = new Dictionary<ItemObject, long>(new RefEq());
            Action<ItemRoster> add = r =>
            {
                if (r == null) return;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (el.Amount <= 0 || !Eligible(it)) continue;
                    long n;
                    c.TryGetValue(it, out n);
                    c[it] = n + el.Amount;
                }
            };
            foreach (var st in Settlement.All)
            {
                if (st == null) continue;
                try { add(st.ItemRoster); if (st.Stash != null && !ReferenceEquals(st.Stash, st.ItemRoster)) add(st.Stash); }
                catch (Exception e) { Stumble("Census.osada", e); }
            }
            foreach (var mp in MobileParty.All)
            {
                if (mp == null) continue;
                try { add(mp.ItemRoster); }
                catch (Exception e) { Stumble("Census.partia", e); }
            }
            try { TradeCarts.ForEachInTransit((it, n) => { if (n > 0 && Eligible(it)) { long k; c.TryGetValue(it, out k); c[it] = k + n; } }); }
            catch (Exception e) { Stumble("Census.wozy", e); }
            return c;
        }

        // ------------------------------------------------------------ doba
        /// <summary>Raz na dobe (ArmouryBehavior.OnDailyTick): start pomiaru, granica kubelka (spis stosu, decyzja od drugiego kubelka), linia.</summary>
        internal static void Daily()
        {
            int today = (int)CampaignTime.Now.ToDays;
            int day = today - 1;
            try
            {
                if (!On)
                {
                    if (_start >= 0 || _goods.Count > 0)
                    {
                        Clear();
                        Log.Info("Regulator wsi (221): dzien " + day + " - WYLACZONY" + (_wired ? " (Harvest Valve = off)" : " (BRAK latki ticku produkcji wsi)")
                                 + " - stan wyczyszczony, mnozniki 1, gnicie BK jak 174.3; wlaczenie zacznie pomiar od nowa.");
                    }
                    else if (!_sessionLine)
                        Log.Info("Regulator wsi (221): dzien " + day + " - " + (_wired ? "WYLACZONY (Harvest Valve = off)" : "NIEAKTYWNY - BRAK latki ticku produkcji wsi") + " - produkcja wsi jak model, gnicie BK jak 174.3.");
                    _sessionLine = true;
                    return;
                }
                if (_dirty) Rebuild();
                if (_start < 0)
                {
                    Begin(today);
                    _sessionLine = true;
                    return;
                }
                if (today - _bucketStart >= Bucket) { Close(today); _sessionLine = true; return; }
                if (!_sessionLine) { Status(day, today); _sessionLine = true; }
            }
            catch (Exception e) { Stumble("Daily", e); }
        }

        private static void Begin(int today)
        {
            _start = today; _bucketStart = today; _buckets = 0; _decisions = 0; _firstDecisionDay = -1;
            var c = Census();
            foreach (var kv in c) { var g = Get(kv.Key); if (g != null) g.SB = kv.Value; }
            Log.Info("Regulator wsi (221): dzien " + (today - 1) + " - CZYNNY, start pomiaru: spis stosu swiata " + c.Count + " towarow (bez zywnosci i zwierzat); okno "
                     + (2 * Bucket) + " dob, decyzja co " + Bucket + " dob, pierwsza za " + (2 * Bucket) + " dob; do niej mnozniki 1 i gnicie BK jak 174.3; potkniecia " + _stumbles + ".");
        }

        private static void Status(int day, int today)
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("Regulator wsi (221): dzien ").Append(day).Append(" - CZYNNY (stan z zapisu), doba pomiaru ").Append(today - _start)
              .Append(", decyzji ").Append(_decisions).Append(", nastepna za ").Append(Math.Max(0, Bucket - (today - _bucketStart))).Append(" dob")
              .Append(_buckets < 1 ? " (pierwsza - po " + (2 * Bucket) + " dobach pomiaru)" : "")
              .Append("; gnicie BK: ").Append(_decisions > 0 ? "stawka 0" : "jak 174.3").Append("; mnozniki inne niz 1:");
            int n = 0;
            var ids = new List<string>(_goods.Keys); ids.Sort(StringComparer.Ordinal);
            foreach (var id in ids) { var g = _goods[id]; if (g.M != 1f) { sb.Append(' ').Append(id).Append(" x").Append(g.M.ToString("0.00", inv)).Append(';'); n++; } }
            if (n == 0) sb.Append(" brak;");
            sb.Append(" potkniecia ").Append(_stumbles).Append('.');
            Log.Info(sb.ToString());
        }

        private static void Close(int today)
        {
            var inv = CultureInfo.InvariantCulture;
            var c = Census();
            foreach (var kv in c) Get(kv.Key);              // towar pierwszy raz w spisie: stos na poprzednich granicach 0 (Get)
            bool decide = _buckets >= 1;
            var s = Settings.Current;
            float ceil = s != null ? Math.Max(1f, Math.Min(2f, s.VillageOutputStep)) : UpStep;
            int low = s != null ? Math.Max(1, s.ValveStockDaysLow) : 30;
            int high = s != null ? Math.Max(low + 1, s.ValveStockDaysHigh) : 60;
            double win = 2.0 * Bucket;
            int down = 0, up = 0, keep = 0, flips = 0;
            var parts = new List<string>();
            var ids = new List<string>(_goods.Keys); ids.Sort(StringComparer.Ordinal);
            var f = new Dictionary<ItemObject, float>(new RefEq());
            foreach (var id in ids)
            {
                var g = _goods[id];
                float mBucket = g.M;                        // mnoznik kubelka biezacego (P1) - po przesunieciu kubelek starszy
                long now = 0;
                if (g.Item != null) c.TryGetValue(g.Item, out now);
                if (decide && g.SA >= 0 && g.Item != null)
                {
                    try
                    {
                        double P = g.P0 + g.P1, D = g.D0 + g.D1;
                        if (P > 0.0 || g.M < 1f)
                        {
                            double C = Math.Max(0.0, P - D - (now - g.SA));
                            double cDay = C / win, pDay = P / win;
                            float m0 = g.M, m1 = m0;
                            int dir = 0;
                            if (P > 0.0 && C < CutBelow * P && (cDay > 0.0 ? now > high * cDay : now > high * pDay))
                            {
                                // recenzja 221: cel = 0.95 x zuzycie / produkcja BAZOWA okna (przy mnozniku 1) - kubelek starszy powstal przy MA, biezacy przy m0;
                                // m0 x C / P przy oknie z dwoma mnoznikami (po cieciu) cial drugi raz towar juz ustawiony na 0.95 x zuzycia (np. 0.57 -> 0.41)
                                double baseP = (g.MA > 0f ? g.P0 / g.MA : g.P0) + (m0 > 0f ? g.P1 / m0 : g.P1);
                                float target = (float)(CutTarget * C / (baseP > 0.0 ? baseP : P));
                                m1 = Math.Max(Floor, Math.Max(target, m0 * CutMaxStep));
                                if (m1 < m0 - 1e-4f) dir = -1; else m1 = m0;
                            }
                            else if (cDay > 0.0 && C >= StepAbove * P && now < low * cDay)
                            {
                                m1 = Math.Min(m0 < 1f ? 1f : ceil, m0 * UpStep);
                                if (m1 > m0 + 1e-4f) dir = 1; else m1 = m0;
                            }
                            if (dir != 0 && g.Dir == -dir) { g.Flips++; flips++; }
                            g.Dir = dir;
                            g.M = m1;
                            if (dir < 0) down++; else if (dir > 0) up++; else keep++;
                            parts.Add(id + ": x" + m0.ToString("0.00", inv) + (dir != 0 ? " -> x" + m1.ToString("0.00", inv) : "")
                                      + ", zuzycie/produkcja " + (P > 0.0 ? (C / P).ToString("0.00", inv) : "-") + " (" + C.ToString("0", inv) + "/" + P.ToString("0", inv)
                                      + (D > 0.0 ? ", skasowane " + D.ToString("0", inv) : "") + "), stos " + now + " = "
                                      + (cDay > 0.0 ? (now / cDay).ToString("0", inv) + " dni zuzycia" : "bez odbiorcy") + ", "
                                      + (dir < 0 ? "w dol" : dir > 0 ? "w gore" : "bez zmian"));
                        }
                    }
                    catch (Exception e) { Stumble("Decide", e); }
                }
                g.P0 = g.P1; g.P1 = 0.0; g.D0 = g.D1; g.D1 = 0.0; g.SA = g.SB; g.SB = now; g.MA = mBucket;
                if (g.Item != null && g.M != 1f) f[g.Item] = g.M;
            }
            _factors = f.Count > 0 ? f : null;
            _buckets++; _bucketStart = today;
            int day = today - 1;
            if (!decide)
            {
                Log.Info("Regulator wsi (221): dzien " + day + " - CZYNNY, koniec pierwszego kubelka (doba pomiaru " + (today - _start) + "), spis stosu " + c.Count
                         + " towarow; pierwsza decyzja za " + Bucket + " dob; gnicie BK jak 174.3; potkniecia " + _stumbles + ".");
                return;
            }
            _decisions++;
            if (_firstDecisionDay < 0) _firstDecisionDay = day;
            var sb = new StringBuilder();
            sb.Append("Regulator wsi (221): dzien ").Append(day).Append(" - CZYNNY, decyzja ").Append(_decisions).Append(" (doba pomiaru ").Append(today - _start)
              .Append(", okno ").Append((int)win).Append(" dob, stos ").Append(low).Append('-').Append(high).Append(" dni zuzycia, sufit x").Append(ceil.ToString("0.00", inv)).Append("): [")
              .Append(string.Join("; ", parts.ToArray())).Append("]; w dol ").Append(down).Append(", w gore ").Append(up).Append(", bez zmian ").Append(keep)
              .Append("; zmiany kierunku w kolejnych decyzjach ").Append(flips).Append("; skasowano 0 (od doby ").Append(_firstDecisionDay)
              .Append(" gnicie BK stawka 0); potkniecia ").Append(_stumbles).Append('.');
            Log.Info(sb.ToString());
        }

        // ------------------------------------------------------------ zapis
        internal static string Export()
        {
            if (_start < 0) return "";
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("v1;").Append(_start).Append(';').Append(_bucketStart).Append(';').Append(_buckets).Append(';').Append(_decisions).Append(';').Append(_firstDecisionDay);
            foreach (var g in _goods.Values)
            {
                sb.Append('|').Append(g.Id).Append(',').Append(g.M.ToString("R", inv)).Append(',').Append(g.P0.ToString("R", inv)).Append(',').Append(g.P1.ToString("R", inv))
                  .Append(',').Append(g.D0.ToString("R", inv)).Append(',').Append(g.D1.ToString("R", inv)).Append(',').Append(g.SA).Append(',').Append(g.SB)
                  .Append(',').Append(g.Dir).Append(',').Append(g.Flips).Append(',').Append(g.MA.ToString("R", inv));
            }
            return sb.ToString();
        }

        internal static void Import(string s)
        {
            Clear();
            if (string.IsNullOrEmpty(s)) return;
            try
            {
                var inv = CultureInfo.InvariantCulture;
                var parts = s.Split('|');
                var h = parts[0].Split(';');
                if (h.Length < 6 || h[0] != "v1") { Log.Info("Regulator wsi (221): stan z zapisu nieczytelny - pomiar od nowa."); return; }
                _start = int.Parse(h[1], inv); _bucketStart = int.Parse(h[2], inv); _buckets = int.Parse(h[3], inv);
                _decisions = int.Parse(h[4], inv); _firstDecisionDay = int.Parse(h[5], inv);
                for (int i = 1; i < parts.Length; i++)
                {
                    var p = parts[i].Split(',');
                    if (p.Length < 10 || p[0].Length == 0) continue;
                    var g = new Good
                    {
                        Id = p[0], M = float.Parse(p[1], inv), P0 = double.Parse(p[2], inv), P1 = double.Parse(p[3], inv),
                        D0 = double.Parse(p[4], inv), D1 = double.Parse(p[5], inv), SA = long.Parse(p[6], inv), SB = long.Parse(p[7], inv),
                        Dir = int.Parse(p[8], inv), Flips = int.Parse(p[9], inv)
                    };
                    if (!(g.M > 0f) || g.M > 2f) g.M = 1f;
                    g.MA = p.Length >= 11 ? float.Parse(p[10], inv) : g.M;   // recenzja 221: pole 11 (mnoznik kubelka starszego); brak - jak M
                    if (!(g.MA > 0f) || g.MA > 2f) g.MA = g.M;
                    _goods[g.Id] = g;
                }
                Rebuild();
            }
            catch (Exception e) { Clear(); Log.Error("HarvestValve.Import", e); }
        }

        // ------------------------------------------------------------ wpiecie
        /// <summary>Po VillageWoodlot.ApplyAll i Bloomery.ApplyAll (postfiks Priority.Last widzi ich dopiski w magazynie wsi).</summary>
        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var tb = AccessTools.TypeByName("TaleWorlds.CampaignSystem.CampaignBehaviors.VillageGoodProductionCampaignBehavior");
                var tick = tb != null ? AccessTools.Method(tb, "TickGoodProduction", new[] { typeof(Village), typeof(bool) }) : null;
                if (tick == null) { Log.Info("HarvestValve (E6/W3, 221): BRAK VillageGoodProductionCampaignBehavior.TickGoodProduction(Village, bool) - regulator wsi NIEAKTYWNY."); return; }
                h.Patch(tick, prefix: new HarmonyMethod(typeof(HarvestValve), nameof(TickPrefix)) { priority = Priority.First },
                              postfix: new HarmonyMethod(typeof(HarvestValve), nameof(TickPostfix)) { priority = Priority.Last },
                              finalizer: new HarmonyMethod(typeof(HarvestValve), nameof(TickFinalizer)));
                _wired = true;
                Log.Info("HarvestValve (E6/W3, 221): tick produkcji wsi wpiety (prefiks Priority.First przed BK, postfiks Priority.Last po lesie wsi, finalizer) - regulator przy kluczu Harvest Valve.");
            }
            catch (Exception e) { Log.Error("HarvestValve.ApplyAll", e); }
        }
    }
}
