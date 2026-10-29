using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// PACZKA 299b - PAMIEC POLKI SPRZETU (Jeff 29.10 ok. 20:20, pytany, czy wyplaszczenie ceny ma objac tez lup i sprzet: "tak dla wszysatkich - 15% dziennie za duzo, dajmy
    /// 5-8% dziennie"). Prawo podazy i popytu sprzetu (SupplyDemand.Factor: ((popyt + 1) / (polka + 1))^0.5 na koszyk typ x tier) nie mialo pamieci - kazda sprzedana sztuka
    /// liczyla sie w cenie od razu cala (sesja Jeffa 29.10: miecz t3 w Riverrun, polka 17, popyt 22.1 - 1. sztuka x1.10, 20. x0.78). Teraz to samo prawo, ktore 299b daje
    /// towarom handlowym: kazdy koszyk (osada x typ x tier) pamieta, ile sztuk lezalo (M), i co dobe zamyka czesc Pace (RawPrice.Pace, 6%) roznicy do polki; cena widzi
    /// S_eff = Pace x polka teraz + (1 - Pace) x M - sztuka sprzedana albo kupiona wazy od razu tylko Pace, reszta przychodzi w nastepnych dobach.
    /// Przy M = S kupno bez zmian (S_eff = S); sprzedaz pierwszej sztuki liczona przy S + Pace zamiast S + 1 (troche drozej). Kupno i odsprzedaz w jednej wizycie daja ten sam
    /// czynnik (cena zalezy tylko od stanu polki i pamieci). Polka fizyczna rzadzi dalej tym, ILE sie bierze i wiezie (nadwyzka i toShip wywozu, bestCap, ShopReserve,
    /// Substitution, brak w warsztatach), zamowienia podnosza popyt od razu. Start neutralny: koszyk bez pamieci (nowa kampania, stary zapis, klucz wlaczony w trakcie gry)
    /// dostaje M = polka teraz. Zapis "arm_shelfmem": tylko roznice M - polka >= 0.005 (przy wczytaniu M = polka + roznica). Sama cena - zlota ani sztuk nie przybywa.
    /// Klucz PriceFlatterAllItems (wymaga PriceFlatterLotWalk) i prawo podazy wlaczone; wylaczony - cena dokladnie jak w 299 (Eff zwraca polke), pamiec czyszczona.
    /// RawPrice.GameCurve (sonda 299 / 299b) - krzywa bez pamieci. Watki: wyceny moga biec poza watkiem gry - tablica osady powstaje pod zamkiem, odczyt bez zamka (float).
    /// </summary>
    internal static class ShelfMemory
    {
        private const int TierSlots = 8;
        private static readonly int Types = MaxType() + 1;
        private static int MaxType()
        {
            int m = 0;
            foreach (var v in Enum.GetValues(typeof(ItemObject.ItemTypeEnum))) m = Math.Max(m, (int)v);
            return Math.Min(m, 255);
        }
        private static int N { get { return Types * TierSlots; } }

        private sealed class Mem { public Settlement St; public float[] M; }
        private static readonly object _lock = new object();
        private static readonly Dictionary<string, Mem> _byId = new Dictionary<string, Mem>();
        private static ConditionalWeakTable<Settlement, Mem> _cache = new ConditionalWeakTable<Settlement, Mem>();
        private static readonly Dictionary<string, List<KeyValuePair<int, float>>> _pending = new Dictionary<string, List<KeyValuePair<int, float>>>();
        private static int _stumbles, _imported, _importedSlots, _appliedPending;
        private static bool _errLogged;

        // stan ostatniej doby (linia PriceWalk299b)
        internal static int DPlaces, DBuckets, DOff1;
        internal static float DAvg, DMax;
        internal static bool DRan;

        internal static bool On { get { return RawPrice.AllItemsOn && SupplyDemand.Active; } }

        private static int TierOf(ItemObject it)
        {
            try { return Math.Max(1, Math.Min(6, (int)it.Tier + 1)); } catch { return 1; }
        }

        private static void Stumble(string where, Exception e)
        {
            System.Threading.Interlocked.Increment(ref _stumbles);
            if (!_errLogged) { _errLogged = true; Log.Error("ShelfMemory." + where, e); }   // raz na kampanie; cena wtedy bez pamieci (jak 299)
        }

        internal static int TakeStumbles() { return System.Threading.Interlocked.Exchange(ref _stumbles, 0); }

        /// <summary>Nowa gra / wczytanie (konstruktor ArmouryBehavior, obok SupplyDemand.ResetOrders - przed SyncData): pamiec od zera.</summary>
        internal static void Reset()
        {
            lock (_lock)
            {
                _byId.Clear(); _pending.Clear(); _cache = new ConditionalWeakTable<Settlement, Mem>();
                _imported = _importedSlots = _appliedPending = 0; _errLogged = false;
                DPlaces = DBuckets = DOff1 = 0; DAvg = DMax = 0f; DRan = false;
            }
            System.Threading.Interlocked.Exchange(ref _stumbles, 0);
        }

        /// <summary>Sztuki sprzetu na polce wedlug koszyka (typ x 8 + tier) - zywa polka, bez zamrozenia ekranu handlu, bez Hold i bez sztuk sondy.</summary>
        private static void Count(ItemRoster r, int[] into)
        {
            Array.Clear(into, 0, into.Length);
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (el.Amount <= 0 || it == null || !SupplyDemand.Equipmentish(it)) continue;
                int ty = (int)it.ItemType;
                if (ty < 0 || ty >= Types) continue;
                into[ty * TierSlots + TierOf(it)] += el.Amount;
            }
        }

        /// <summary>Sonda 299b: sztuki koszykow polki (indeks typ x 8 + tier, tablica dlugosci Slots).</summary>
        internal static int Slots { get { return N; } }
        internal static void CountInto(ItemRoster r, int[] into) { Count(r, into); }

        private static Mem Get(Settlement st)
        {
            Mem x;
            var c = _cache;
            if (c.TryGetValue(st, out x)) return x;
            lock (_lock)
            {
                c = _cache;
                if (c.TryGetValue(st, out x)) return x;
                string id = st.StringId;
                if (string.IsNullOrEmpty(id) || st.ItemRoster == null) return null;
                if (!_byId.TryGetValue(id, out x) || !ReferenceEquals(x.St, st))
                {
                    var counts = new int[N];
                    Count(st.ItemRoster, counts);
                    var m = new float[N];
                    for (int i = 0; i < m.Length; i++) m[i] = counts[i];   // start neutralny: M = polka teraz
                    List<KeyValuePair<int, float>> pend;
                    if (_pending.TryGetValue(id, out pend))
                    {
                        foreach (var kv in pend) if (kv.Key >= 0 && kv.Key < m.Length) m[kv.Key] = Math.Max(0f, counts[kv.Key] + kv.Value);   // z zapisu: M = polka + roznica
                        _pending.Remove(id); _appliedPending++;
                    }
                    x = new Mem { St = st, M = m };
                    _byId[id] = x;
                }
                try { c.Add(st, x); } catch (ArgumentException) { }
                return x;
            }
        }

        internal static float Blend(float p, float sDir, float m) { return p * sDir + (1f - p) * m; }

        internal static float Eff(Settlement st, ItemObject it, float sDir)
        {
            if (it == null) return sDir;
            return Eff(st, it.ItemType, TierOf(it), sDir);
        }

        /// <summary>Polka, jaka widzi cena: Pace x sDir + (1 - Pace) x M. sDir = polka wedlug SupplyDemand.Stock (przy sprzedazy + 1). Klucz wylaczony, krzywa gry
        /// (RawPrice.GameCurve), osada poza miastem i zamkiem albo potkniecie - sDir (jak w 299).</summary>
        internal static float Eff(Settlement st, ItemObject.ItemTypeEnum type, int tier, float sDir)
        {
            if (st == null || RawPrice.GameCurve || !On) return sDir;
            int ty = (int)type;
            if (ty < 0 || ty >= Types || tier < 1 || tier > 6 || (!st.IsTown && !st.IsCastle)) return sDir;
            try
            {
                var x = Get(st);
                if (x == null) return sDir;
                float m = x.M[ty * TierSlots + tier];
                if (float.IsNaN(m)) return sDir;
                return Blend((float)RawPrice.Pace, sDir, m);
            }
            catch (Exception e) { Stumble("Eff", e); return sDir; }
        }

        /// <summary>Pamiec koszyka (NaN - brak pamieci albo klucz wylaczony) - sonda i linia handlu gracza.</summary>
        internal static float M(Settlement st, ItemObject.ItemTypeEnum type, int tier)
        {
            if (st == null || !On) return float.NaN;
            int ty = (int)type;
            if (ty < 0 || ty >= Types || tier < 1 || tier > 6 || (!st.IsTown && !st.IsCastle)) return float.NaN;
            try { var x = Get(st); return x != null ? x.M[ty * TierSlots + tier] : float.NaN; }
            catch (Exception e) { Stumble("M", e); return float.NaN; }
        }

        /// <summary>Raz na dobe (ArmouryBehavior, zaraz po SupplyDemand.DecayOrders - przed ArmsPricing.Daily i wywozem kupcow, jak wygladzanie podazy gry na poczatku doby
        /// miasta): kazdy koszyk miast i zamkow M += Pace x (polka - M), przy |M - polka| &lt; 0.005 M = polka. Klucz wylaczony - pamiec czyszczona (wlaczenie = start neutralny).</summary>
        internal static void Daily()
        {
            DRan = false;
            if (!On)
            {
                lock (_lock) { if (_byId.Count > 0 || _pending.Count > 0) { _byId.Clear(); _pending.Clear(); _cache = new ConditionalWeakTable<Settlement, Mem>(); } }
                DPlaces = DBuckets = DOff1 = 0; DAvg = DMax = 0f;
                return;
            }
            float p = (float)RawPrice.Pace;
            var counts = new int[N];
            int places = 0, buckets = 0, off1 = 0; double sum = 0; float max = 0f;
            foreach (var st in Settlement.All)
            {
                if (st == null || (!st.IsTown && !st.IsCastle) || st.ItemRoster == null || st.Town == null) continue;
                try
                {
                    var x = Get(st);
                    if (x == null) continue;
                    Count(st.ItemRoster, counts);
                    var m = x.M;
                    for (int i = 0; i < m.Length; i++)
                    {
                        float s = counts[i], v = m[i];
                        if (float.IsNaN(v)) v = s;
                        v += p * (s - v);
                        if (Math.Abs(v - s) < 0.005f) v = s;
                        m[i] = v;
                        if (v == 0f && s == 0f) continue;
                        buckets++;
                        float dv = Math.Abs(v - s);
                        if (dv >= 1f) { off1++; sum += dv; if (dv > max) max = dv; }
                    }
                    places++;
                }
                catch (Exception e) { Stumble("Daily", e); }
            }
            DPlaces = places; DBuckets = buckets; DOff1 = off1; DAvg = off1 > 0 ? (float)(sum / off1) : 0f; DMax = max; DRan = true;
        }

        // ------------------------------------------------------------ zapis (klucz "arm_shelfmem", SaveText.Sync): "osada:slot=roznica,slot=roznica;"
        internal static string Export()
        {
            if (!On) return "";
            var sb = new StringBuilder();
            var inv = CultureInfo.InvariantCulture;
            var counts = new int[N];
            lock (_lock)
            {
                foreach (var kv in _byId)
                {
                    try
                    {
                        var x = kv.Value;
                        if (x.St == null || x.St.ItemRoster == null || BadId(kv.Key)) continue;
                        Count(x.St.ItemRoster, counts);
                        bool any = false;
                        for (int i = 0; i < x.M.Length; i++)
                        {
                            float d = x.M[i] - counts[i];
                            if (float.IsNaN(d) || Math.Abs(d) < 0.005f) continue;
                            sb.Append(any ? "," : kv.Key + ":").Append(i.ToString(inv)).Append('=').Append(d.ToString("0.###", inv));
                            any = true;
                        }
                        if (any) sb.Append(';');
                    }
                    catch (Exception e) { Stumble("Export", e); }
                }
                foreach (var kv in _pending)   // wczytane, a w tej sesji jeszcze nieuzyte (osada bez wyceny i bez doby) - jak przyszly
                {
                    if (_byId.ContainsKey(kv.Key) || BadId(kv.Key) || kv.Value.Count == 0) continue;
                    bool any = false;
                    foreach (var e in kv.Value)
                    {
                        sb.Append(any ? "," : kv.Key + ":").Append(e.Key.ToString(inv)).Append('=').Append(e.Value.ToString("0.###", inv));
                        any = true;
                    }
                    sb.Append(';');
                }
            }
            return sb.ToString();
        }

        private static bool BadId(string id) { return id.IndexOf(':') >= 0 || id.IndexOf(';') >= 0 || id.IndexOf(',') >= 0 || id.IndexOf('=') >= 0; }

        internal static void Import(string s)
        {
            lock (_lock)
            {
                _pending.Clear(); _imported = 0; _importedSlots = 0;
                if (string.IsNullOrEmpty(s)) return;
                var inv = CultureInfo.InvariantCulture;
                int n = N;
                foreach (var rec in s.Split(';'))
                {
                    int c = rec.IndexOf(':');
                    if (c <= 0) continue;
                    var list = new List<KeyValuePair<int, float>>();
                    foreach (var part in rec.Substring(c + 1).Split(','))
                    {
                        int e = part.IndexOf('=');
                        int slot; float d;
                        if (e <= 0 || !int.TryParse(part.Substring(0, e), NumberStyles.Integer, inv, out slot) || slot < 0 || slot >= n) continue;
                        if (!float.TryParse(part.Substring(e + 1), NumberStyles.Float, inv, out d) || float.IsNaN(d) || float.IsInfinity(d)) continue;
                        list.Add(new KeyValuePair<int, float>(slot, d));
                    }
                    if (list.Count == 0) continue;
                    _pending[rec.Substring(0, c)] = list;
                    _imported++; _importedSlots += list.Count;
                }
            }
        }

        /// <summary>Linia doby: wczytane z zapisu (osad / koszykow), z nich uzyte w tej sesji.</summary>
        internal static string LoadReport()
        {
            lock (_lock) { return _imported + " osad / " + _importedSlots + " koszykow, uzyte " + _appliedPending + ", czeka " + _pending.Count; }
        }
    }
}
