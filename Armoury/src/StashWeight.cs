using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 537 E4 (projekt docs/projekty/KONIE-UPRZEZE.md, rozdz. 3; Jeff 15.11 "przenosze zbroje i bron do stash i ubywa mi udzwigu, a nadal wioze wszystko").
    /// Zbrojownia DTE gracza (ArmyArmory.Armory, osobny statyczny roster) wazyla 0 - gra liczy wage tylko z MobileParty.ItemRoster. Teraz (klucz ArmouryHasWeight):
    ///  - wpisy wagi "Troops' armoury (unused): yours" i "Troops' armoury (unused): men's" (D13 - rzeczy ludzi tez waza, z rozbiciem) = waga zbrojowni
    ///    (+ sztuki schowane przez Escrow na czas okna) MINUS waga tego, co jest "na ludziach";
    ///  - JEDNA lista "na ludziach" (D12 a): podglad DTE KitPreview.ForWeight (ten sam rozdzielacz co bitwa, ranni liczeni jak zdrowi - D4: kit rannych nie
    ///    wazy); konie z Rode, uprzeze z Saddles (po strazach wystawienia), reszta z Given. Ta sama lista (WornKeys) idzie do Escrow (co chowac jako noszone)
    ///    i do wagi - sztuka schowana jako noszona nie wazy, widoczna wazy;
    ///    recenzja 537: na liscie tez konie i uprzeze zbrojowni pod piechota z par MarchPairs (D11, FootArmouryWorn) - jak uprzaz z ladunku pod piechurem (E3);
    ///  - sztuki gracza (czesc gracza z podzialu porzadku, bez podzialu - ksiega per id) nigdy nie sa "na ludziach" (widoczne na liscie gracza, waza);
    ///  - waga przez model (GetItemEffectiveWeight z flaga morza wolajacego), sztuka po sztuce z modyfikatorem (dokladnie, bez "najlzejszej sztuki");
    ///  - bez podgladu (Own Kit Only wylaczone, bez DTE, CrashScribe sprzed 133a) - cala zbrojownia wazy jak ladunek (bezpieczny kierunek);
    ///  - zbrojownie lordow AI dalej waza 0 (D3 - swiadoma asymetria); tylko partia gracza (partie rodu nie maja zbrojowni DTE).
    /// Liczenie: postfiks wagi (wspolny MarchPairs.WeightPostfix, DeclaredMethod) na watku glownym liczy zbrojownie od razu (O(n), takze przy otwartym ekranie -
    /// liczba w oknie zgadza sie po kazdym przeniesieniu); poza watkiem glownym - ostatni wynik. Lista "na ludziach" z pamieci (Refresh z paska mapy, najwyzej
    /// raz na 1 s po zmianie wersji zbrojowni / skladu / dnia; przy otwartej zbrojowni i w misji - bez liczenia). Zmiana wyniku -> MarchPairs.InvalidateWeight
    /// (pamiec wagi gry, bez VersionNo - bez kaskady i bez petli: Refresh porownuje swoj wynik, nie wynik postfiksu).
    /// Jednorazowy komunikat po wgraniu (zapis "arm_537_stashw"). Potkniecia liczone per miejsce, bez wylacznika. Stan czyszczony w Reset (nowa kampania, wczytanie).
    /// </summary>
    internal static class StashWeight
    {
        private const string P = "Waga zbrojowni (537 E4): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On { get { var s = Settings.Current; return s != null && s.ArmouryHasWeight; } }

        // wynik jednego liczenia (publikowany w calosci - watki tiku tylko czytaja)
        internal sealed class W
        {
            public float Total, Worn, Yours, Mens; public int Pieces, WornPieces, YoursPieces, Held; public bool Preview, AtSea;
            public float Unused { get { return Math.Max(0f, Total - Worn); } }
        }

        private static volatile W _pub;                       // ostatni wynik (Refresh albo postfiks na watku glownym)
        private static Dictionary<ItemObject, int> _perItem;  // lista "na ludziach" z podgladu (null - bez podgladu: cala zbrojownia wazy)
        private static string _pvWhy = "nie liczony";
        private static int _mainTid = -1;
        private static int _at = Environment.TickCount - 20000;
        private static bool _told, _toldLoaded, _startLogged;
        private static int _inval, _refresh, _pvMs;
        private static long _calls, _live, _cached;
        private static int _stumbles;
        private static readonly Dictionary<string, int> _stumbleAt = new Dictionary<string, int>(StringComparer.Ordinal);
        private static TextObject _txYours, _txMens;
        private static TextObject TxYours { get { return _txYours ?? (_txYours = new TextObject("{=!}Troops' armoury (unused): yours")); } }
        private static TextObject TxMens { get { return _txMens ?? (_txMens = new TextObject("{=!}Troops' armoury (unused): men's")); } }

        internal static void Reset()
        {
            _pub = null; _perItem = null; _perBase = null; _mergedBase = null; _footSig = "-"; _footPieces = 0; _lastPv = null; _mKey = null; _pvWhy = "nie liczony"; _mainTid = -1; _at = Environment.TickCount - 20000;
            _told = false; _toldLoaded = false; _startLogged = false;
            _inval = 0; _refresh = 0; _pvMs = 0;
            Interlocked.Exchange(ref _calls, 0); Interlocked.Exchange(ref _live, 0); Interlocked.Exchange(ref _cached, 0);
            lock (_stumbleAt) { _stumbles = 0; _stumbleAt.Clear(); }
        }

        private static void Stumble(string where, Exception e)
        {
            int n;
            lock (_stumbleAt) { _stumbles++; _stumbleAt.TryGetValue(where, out n); _stumbleAt[where] = ++n; }
            if (n <= 3 || n % 50 == 0) { try { Log.Error("StashWeight." + where + " (potkniecie " + n + ")", e); } catch { } }
        }

        // ---------------------------------------------------------------- zapis (flaga komunikatu jednorazowego)
        internal static string Export() { return _told ? "1" : "0"; }
        internal static void Import(string v) { _told = v == "1"; _toldLoaded = v != null; }

        // ---------------------------------------------------------------- jedna lista "na ludziach"
        /// <summary>Lista z podgladu wedlug przedmiotu: konie z Rode, uprzeze z Saddles (po strazach), reszta z Given (sloty bez tymczasowych).</summary>
        private static Dictionary<ItemObject, int> PerItem(KitPreview.Result r)
        {
            var d = new Dictionary<ItemObject, int>();
            if (r == null || !r.Ok) return null;
            foreach (var kv in r.Given)
            {
                var it = kv.Key;
                if (it == null || kv.Value <= 0 || it.ItemType == ItemObject.ItemTypeEnum.Horse || it.ItemType == ItemObject.ItemTypeEnum.HorseHarness) continue;
                int v; d.TryGetValue(it, out v); d[it] = v + kv.Value;
            }
            foreach (var kv in r.Rode) { if (kv.Key == null || kv.Value <= 0) continue; int v; d.TryGetValue(kv.Key, out v); d[kv.Key] = v + kv.Value; }
            foreach (var kv in r.Saddles) { if (kv.Key == null || kv.Value <= 0) continue; int v; d.TryGetValue(kv.Key, out v); d[kv.Key] = v + kv.Value; }
            return d;
        }

        /// <summary>Recenzja 537 (3): lista z podgladu (base) + konie i uprzeze zbrojowni pod piechota z par MarchPairs (D11 - "taka para tez jest na ludziach").
        /// Nowy slownik tylko po zmianie podgladu albo podpisu par (klucz pamieci Refresh po RuntimeHelpers.GetHashCode). Watek glowny.</summary>
        private static void MergeFoot()
        {
            if (_perBase == null) { _perItem = null; return; }
            string sig;
            Dictionary<ItemObject, int> foot = null;
            try { foot = MarchPairs.FootArmouryWorn(out sig); } catch (Exception e) { Stumble("FootArmouryWorn", e); sig = "blad"; foot = null; }
            if (_perItem != null && ReferenceEquals(_mergedBase, _perBase) && sig == _footSig) return;
            var d = new Dictionary<ItemObject, int>(_perBase);
            int n = 0;
            if (foot != null) foreach (var kv in foot) { if (kv.Key == null || kv.Value <= 0) continue; int v; d.TryGetValue(kv.Key, out v); d[kv.Key] = v + kv.Value; n += kv.Value; }
            _perItem = d; _mergedBase = _perBase; _footSig = sig; _footPieces = n;
        }
        private static Dictionary<ItemObject, int> _perBase, _mergedBase;
        private static string _footSig = "-";
        private static int _footPieces;

        private sealed class KeyLot { public string Key; public EquipmentElement E; public int N, Own, Worn, Used; public float Pm; }

        /// <summary>Zbrojownia (+ schowane) zebrana po egzemplarzu (id|mod); czesc gracza (podzial porzadku, bez niego ksiega per id - najgorsze najpierw);
        /// "na ludziach" z listy per przedmiot, najpierw egzemplarze noszone wedlug porzadku, potem lepsze; nigdy ponad (sztuki - czesc gracza).</summary>
        private static Dictionary<string, KeyLot> Lots(List<KeyValuePair<EquipmentElement, int>> stock, Dictionary<ItemObject, int> per, Dictionary<string, int[]> split)
        {
            var lots = new Dictionary<string, KeyLot>(StringComparer.Ordinal);
            var byItem = new Dictionary<ItemObject, List<KeyLot>>();
            foreach (var kv in stock)
            {
                var it = kv.Key.Item;
                if (it == null || kv.Value <= 0) continue;
                string key = QuartermasterLaw.KeyOf(kv.Key);
                KeyLot l;
                if (!lots.TryGetValue(key, out l))
                {
                    var m = kv.Key.ItemModifier;
                    l = new KeyLot { Key = key, E = kv.Key, Pm = m != null ? m.PriceMultiplier : 1f };
                    lots[key] = l;
                    List<KeyLot> li; if (!byItem.TryGetValue(it, out li)) { li = new List<KeyLot>(); byItem[it] = li; } li.Add(l);
                }
                l.N += kv.Value;
            }
            foreach (var kv in byItem)
            {
                var li = kv.Value;
                bool inSplit = false;
                if (split != null) foreach (var l in li) { int[] v; if (split.TryGetValue(l.Key, out v)) { inSplit = true; l.Own = Math.Min(l.N, Math.Max(0, v[1])); l.Used = Math.Max(0, v[0]); } }
                if (!inSplit)
                {
                    // bez podzialu - ksiega gracza per id, najgorsze egzemplarze najpierw (ludzie nosza lepsze)
                    int left = Math.Max(0, ArmouryBehavior.StockOf(kv.Key.StringId));
                    li.Sort((a, b) => a.Pm.CompareTo(b.Pm));
                    foreach (var l in li) { int o = Math.Min(l.N, left); l.Own = o; left -= o; if (left <= 0) break; }
                }
                int n; if (per == null || !per.TryGetValue(kv.Key, out n) || n <= 0) continue;
                // noszone wedlug porzadku najpierw, potem lepszy stan
                li.Sort((a, b) => { int c = b.Used.CompareTo(a.Used); return c != 0 ? c : b.Pm.CompareTo(a.Pm); });
                foreach (var l in li) { int w = Math.Min(n, Math.Max(0, l.N - l.Own)); l.Worn = w; n -= w; if (n <= 0) break; }
            }
            return lots;
        }

        /// <summary>D12 (a): ile sztuk KAZDEGO egzemplarza (id|mod) jest na ludziach - dla Escrow przy otwarciu ekranu (ta sama lista co waga). null - klucz
        /// wylaczony albo podglad niedostepny (Escrow zostaje przy papierze kwatermistrza). Watek glowny, przed schowaniem polek.</summary>
        internal static Dictionary<string, int> WornForEscrow(ItemRoster armory, Dictionary<string, int[]> split, out int pieces)
        {
            pieces = 0;
            try
            {
                if (!On || armory == null) return null;
                var pv = KitPreview.ForWeight();
                if (pv == null) { _perItem = null; _perBase = null; _lastPv = null; _pvWhy = "brak (Gate / CrashScribe)"; return null; }
                if (pv.Ok) { if (!ReferenceEquals(pv, _lastPv)) { _perBase = PerItem(pv); _lastPv = pv; _pvMs = (int)pv.Ms; } _pvWhy = "TAK (" + _pvMs + " ms)"; }
                MergeFoot();   // recenzja 537 (3): pary piechoty ze zbrojowni tez na ludziach - Escrow chowa je jak noszone
                if (_perItem == null) return null;
                var stock = new List<KeyValuePair<EquipmentElement, int>>();
                for (int i = 0; i < armory.Count; i++) { var el = armory.GetElementCopyAtIndex(i); if (el.Amount > 0) stock.Add(new KeyValuePair<EquipmentElement, int>(el.EquipmentElement, el.Amount)); }
                var lots = Lots(stock, _perItem, split);
                var d = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var l in lots.Values) { d[l.Key] = l.Worn; pieces += l.Worn; }
                return d;
            }
            catch (Exception e) { Stumble("WornForEscrow", e); return null; }
        }

        // ---------------------------------------------------------------- liczenie wagi
        private static W Measure(MobileParty mp, bool atSea)
        {
            var arm = QuartermasterLaw.DteArmory();
            var c = Campaign.Current;
            var model = c != null && c.Models != null ? c.Models.InventoryCapacityModel : null;
            if (arm == null || model == null) return null;
            var stock = new List<KeyValuePair<EquipmentElement, int>>(arm.Count + 8);
            for (int i = 0; i < arm.Count; i++) { var el = arm.GetElementCopyAtIndex(i); if (el.Amount > 0) stock.Add(new KeyValuePair<EquipmentElement, int>(el.EquipmentElement, el.Amount)); }
            int held = 0;
            if (QuartermasterEscrow.Active)
                foreach (var kv in QuartermasterEscrow.Held) if (kv.Value > 0) { stock.Add(kv); held += kv.Value; }   // te sztuki sa na czas okna poza Armory
            var per = _perItem;
            var lots = Lots(stock, per, QuartermasterLaw.LastSplit);
            var w = new W { Preview = per != null, AtSea = atSea, Held = held };
            foreach (var l in lots.Values)
            {
                TextObject d;
                float kg = model.GetItemEffectiveWeight(l.E, mp, atSea, out d);
                if (kg <= 0f) kg = 0f;
                w.Pieces += l.N; w.Total += kg * l.N;
                w.WornPieces += l.Worn; w.Worn += kg * l.Worn;
                int own = Math.Min(l.Own, l.N - l.Worn);
                w.YoursPieces += own; w.Yours += kg * own;
            }
            w.Mens = Math.Max(0f, w.Unused - w.Yours);
            return w;
        }

        /// <summary>Postfiks wagi (wolany z MarchPairs.WeightPostfix - jeden postfiks na CalculateTotalWeightCarried). Gracz; watek glowny - liczy od razu,
        /// inny watek - ostatni wynik z ta sama flaga morza.</summary>
        internal static void Add(MobileParty mp, bool atSea, ref ExplainedNumber result)
        {
            try
            {
                if (mp == null || !mp.IsMainParty || !On) return;
                Interlocked.Increment(ref _calls);
                W w;
                if (_mainTid >= 0 && Thread.CurrentThread.ManagedThreadId == _mainTid && TaleWorlds.MountAndBlade.Mission.Current == null)
                {
                    w = Measure(mp, atSea);
                    Interlocked.Increment(ref _live);
                    if (w != null && w.AtSea == mp.IsCurrentlyAtSea) _pub = w;
                }
                else { w = _pub; Interlocked.Increment(ref _cached); if (w != null && w.AtSea != atSea) w = null; }
                if (w == null) return;
                if (w.Yours > 0.05f) result.Add(w.Yours, TxYours);
                if (w.Mens > 0.05f) result.Add(w.Mens, TxMens);
            }
            catch (Exception e) { Stumble("Add", e); }
        }

        /// <summary>Pasek mapy (MarchHud, watek glowny, co 0.2 s): najwyzej raz na 1 s - lista "na ludziach" po zmianie wersji, wynik wagi, przy zmianie
        /// InvalidateWeight; komunikat jednorazowy. Przy otwartej zbrojowni i w misji - nic.</summary>
        internal static void Refresh()
        {
            try
            {
                var mp = MobileParty.MainParty;
                if (Campaign.Current == null || mp == null || !mp.IsActive) return;
                _mainTid = Thread.CurrentThread.ManagedThreadId;
                if (!On)
                {
                    if (_pub != null) { _pub = null; _perItem = null; _perBase = null; MarchPairs.InvalidateWeight(mp); _inval++; }
                    return;
                }
                if (QuartermasterEscrow.Active || TaleWorlds.MountAndBlade.Mission.Current != null) return;
                int now = Environment.TickCount;
                if (unchecked(now - _at) < 1000) return;
                _at = now;
                _refresh++;
                var pv = KitPreview.ForWeight();
                if (pv == null) { _perItem = null; _perBase = null; _lastPv = null; _pvWhy = "brak (Gate / CrashScribe - cala zbrojownia wazy)"; }
                else if (pv.Ok) { if (!ReferenceEquals(pv, _lastPv)) { _perBase = PerItem(pv); _lastPv = pv; _pvMs = (int)pv.Ms; } _pvWhy = "TAK (" + _pvMs + " ms)"; }
                else _pvWhy = "potkniecie - poprzednia lista";   // _perBase bez zmiany
                MergeFoot();   // recenzja 537 (3): + konie i uprzeze zbrojowni pod piechota (pary D11) - nowy slownik tylko po zmianie, wiec klucz nizej sie zmienia
                // bez liczenia, gdy nic sie nie zmienilo (wersja zbrojowni, lista na ludziach, podzial porzadku, morze) - najwyzej co 10 s (ksiega gracza bez wersji)
                var arm = QuartermasterLaw.DteArmory();
                var split = QuartermasterLaw.LastSplit;
                string mk = (arm != null ? arm.VersionNo : -1) + "|" + (_perItem != null ? RuntimeHelpers.GetHashCode(_perItem) : 0) + "|"
                            + (split != null ? RuntimeHelpers.GetHashCode(split) : 0) + "|" + (mp.IsCurrentlyAtSea ? 1 : 0);
                if (_pub != null && mk == _mKey && unchecked(now - _mAt) < 10000) return;
                _mKey = mk; _mAt = now;
                var w = Measure(mp, mp.IsCurrentlyAtSea);
                if (w == null) return;
                var old = _pub;
                _pub = w;
                if (old == null || Math.Abs(old.Yours - w.Yours) > 0.05f || Math.Abs(old.Mens - w.Mens) > 0.05f || old.AtSea != w.AtSea)
                { MarchPairs.InvalidateWeight(mp); _inval++; }
                if (!_startLogged) { _startLogged = true; Log.Info(P + "start sesji | " + Brief(w) + " | komunikat jednorazowy " + (_told ? "juz pokazany w tej kampanii" : "jeszcze nie") + (_toldLoaded ? "" : " (zapis bez klucza arm_537_stashw)") + "."); }
                if (!_told)
                {
                    _told = true;
                    if (w.Unused >= 0.5f)
                    {
                        Log.Player("Your troops' armoury now counts toward cargo weight: +" + w.Unused.ToString("0", Inv) + " kg (what your men do not wear or ride: "
                                   + w.Yours.ToString("0", Inv) + " kg yours, " + w.Mens.ToString("0", Inv) + " kg theirs).");
                        Log.Info(P + "KOMUNIKAT JEDNORAZOWY - zbrojownia od teraz wazy: +" + w.Unused.ToString("0.0", Inv) + " kg (" + Brief(w) + ").");
                    }
                    else Log.Info(P + "komunikat jednorazowy pominiety - zbrojownia nic nie wazy ponad to, co na ludziach (" + Brief(w) + ").");
                }
            }
            catch (Exception e) { Stumble("Refresh", e); }
        }
        private static KitPreview.Result _lastPv;
        private static string _mKey;
        private static int _mAt;

        private static string Brief(W w)
        {
            return "zbrojownia " + w.Pieces + " szt. " + w.Total.ToString("0.0", Inv) + " kg" + (w.Held > 0 ? " (w tym schowane przez Escrow " + w.Held + " szt.)" : "")
                   + ", na ludziach " + w.WornPieces + " szt. " + w.Worn.ToString("0.0", Inv) + " kg (podglad DTE " + (w.Preview ? "TAK" : "NIE - cala zbrojownia wazy") + ")"
                   + ", wazy " + w.Unused.ToString("0.0", Inv) + " kg: Twoje " + w.YoursPieces + " szt. " + w.Yours.ToString("0.0", Inv) + " kg, ludzi " + w.Mens.ToString("0.0", Inv) + " kg"
                   + (w.AtSea ? ", na morzu" : "");
        }

        /// <summary>Linia doby (tylko log).</summary>
        internal static void Daily()
        {
            try
            {
                var mp = MobileParty.MainParty;
                if (Campaign.Current == null || mp == null || !mp.IsActive) return;
                var sb = new StringBuilder(600);
                sb.Append(P).Append("dzien ").Append((int)CampaignTime.Now.ToDays - 1).Append(" | klucz ArmouryHasWeight ").Append(On ? "TAK" : "NIE");
                if (On)
                {
                    W w = null;
                    try { if (!QuartermasterEscrow.Active) w = Measure(mp, mp.IsCurrentlyAtSea); } catch (Exception e) { Stumble("Daily.Measure", e); }
                    var pub = _pub;
                    sb.Append(" | ").Append(w != null ? Brief(w) : "brak liczenia (zbrojownia otwarta albo DTE nieobecne)");
                    if (pub != null && w != null && (Math.Abs(pub.Unused - w.Unused) > 0.5f))
                        sb.Append(" | ROZNICA z ostatnim wynikiem paska ").Append(pub.Unused.ToString("0.0", Inv)).Append(" kg");
                    sb.Append(" | lista na ludziach: podglad ").Append(_pvWhy).Append(", liczony ").Append(KitPreview.WeighRuns).Append(" razy od wczytania (ranni jak zdrowi - D4)")
                      .Append(", w tym pary piechoty ze zbrojowni (D11) ").Append(_footPieces).Append(" szt.");
                    try
                    {
                        var c = Campaign.Current; var m = c.Models != null ? c.Models.InventoryCapacityModel : null;
                        if (m != null) sb.Append(" | waga gracza ").Append(m.CalculateTotalWeightCarried(mp, mp.IsCurrentlyAtSea).ResultNumber.ToString("0", Inv))
                                         .Append(" (pamiec gry ").Append(mp.TotalWeightCarried.ToString("0", Inv)).Append(")");
                    }
                    catch (Exception e) { Stumble("Daily.model", e); }
                }
                sb.Append(" | wpis wagi: wywolan ").Append(Interlocked.Read(ref _calls)).Append(" (na zywo ").Append(Interlocked.Read(ref _live)).Append(", z pamieci ")
                  .Append(Interlocked.Read(ref _cached)).Append("), odswiezen ").Append(_refresh).Append(", odswiezen pamieci wagi gry ").Append(_inval)
                  .Append(" | komunikat jednorazowy ").Append(_told ? "pokazany/pominiety" : "nie");
                if (_stumbles > 0) { sb.Append(" | potkniecia ").Append(_stumbles); lock (_stumbleAt) foreach (var kv in _stumbleAt) sb.Append(' ').Append(kv.Key).Append('=').Append(kv.Value); }
                Log.Info(sb.Append('.').ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
        }
    }
}
