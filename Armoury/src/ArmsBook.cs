using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// E6/0 (206) - POMIAR ETAPU 6, TYLKO LOG (projekt etapu 6 v2, rozdz. 3 "E6/0"; klucz Stage6MeasureLog). Niczego nie zmienia w grze: zadna latka,
    /// zadna zmiana rosteru ani zlota. Raz na dobe (po ksiedze pieniadza): ksiega uzbrojenia (ArmsBook), ksiega koni (HorseBook) i "co kuje kazda kraina"
    /// (CraftRealm). Stan czyszczony w konstruktorze ArmouryBehavior (Reset).
    /// </summary>
    internal static class Stage6Measure
    {
        internal static void Reset() { ArmsBook.Reset(); HorseBook.Reset(); CraftRealm.Reset(); _tSeen = 0; _tCheckNext = false; _tChecks = _tBad = _tFails = 0; _tRows = _tRosters = _tArmRows = 0; _tMs = 0; _tMode = 0; }

        /// <summary>Nasluch OnItemProducedEvent: wyroby miast wedlug krainy (CraftRealm) i konie ze stadnin wsi (HorseBook) - tylko liczniki.</summary>
        internal static void OnItemProduced(ItemObject it, Settlement st, int n)
        {
            CraftRealm.OnItemProduced(it, st, n);
            HorseBook.OnItemProduced(it, st, n);
        }

        internal static void Daily()
        {
            var sw = Stopwatch.StartNew();
            // f13-doba (DayTickSmooth): jeden przeglad rosterow swiata i zbrojowni DTE dla obu ksiag (dotad ArmsBook i HorseBook przegladaly te same rostery po
            // kolei - miedzy nimi same odczyty i linie logu, wiec stan ten sam). Doba kontrolna (druga w sesji - pierwsza po wczytaniu i tak najciezsza, co 28 dob i nastepna po rozjezdzie): obie ksiegi
            // licza stara droga (linie z niej), a przeglad wspolny liczony obok i porownany pole po polu. Wyjatek przegladu wspolnego = potkniecie, stara droga.
            Tally use = null, check = null;
            _tMode = 0;
            var s = Settings.Current;
            if (s != null && s.DayTickSmooth && s.Stage6MeasureLog)
            {
                int day = 0; try { day = (int)CampaignTime.Now.ToDays; } catch { }
                _tSeen++;
                bool chk = _tSeen == 2 || _tCheckNext || day % 28 == 0;
                long t0 = Stopwatch.GetTimestamp();
                Tally t = null;
                try { t = Take(); }
                catch (Exception e) { _tFails++; t = null; if (_tFails == 1) Log.Error("Stage6Measure.Take (f13)", e); }
                _tMs = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                if (t != null)
                {
                    _tRows = t.Rows; _tRosters = t.Rosters; _tArmRows = t.ArmRows;
                    if (chk) { check = t; _tCheckNext = false; _tChecks++; _tMode = 2; }
                    else { use = t; _tMode = 1; }
                }
            }
            try { ArmsBook.Daily(use, check); } catch (Exception e) { Log.Error("ArmsBook.Daily", e); }
            try { HorseBook.Daily(use, check); } catch (Exception e) { Log.Error("HorseBook.Daily", e); }
            try { CraftRealm.Daily(); } catch (Exception e) { Log.Error("CraftRealm.Daily", e); }
            ArmsBook.LastCostMs = sw.Elapsed.TotalMilliseconds;
        }

        // ------------------------------------------------------------ f13-doba: przeglad wspolny (tylko odczyt, tylko log)
        /// <summary>Sumy ksiegi uzbrojenia i ksiegi koni z jednego przegladu rosterow (ForEachRoster) i zbrojowni DTE - te same pola, ktore obie ksiegi
        /// licza w swoich przegladach (ArmsBook: now, hz, polki, tabory, zbrojownie; HorseBook: polki / magazyny wsi / schowki / tabory, zbrojownie).</summary>
        internal sealed class Tally
        {
            internal readonly long[] ArmsNow = new long[ArmsBook.SlotCount], ArmsHz = new long[ArmsBook.HPlaceCount];
            internal long ArmsShelves, ArmsParties, ArmsArmories;
            internal readonly long[][] Horse = { new long[HorseBook.CatCount], new long[HorseBook.CatCount], new long[HorseBook.CatCount], new long[HorseBook.CatCount] };
            internal readonly long[] HorseArm = new long[HorseBook.CatCount];
            internal long Rosters, Rows, ArmRows;
        }

        private static bool _tCheckNext;
        private static int _tSeen;   // doby z przegladem wspolnym w sesji (kontrola w drugiej)
        private static int _tChecks, _tBad, _tFails, _tMode;   // _tMode: 0 stara droga, 1 przeglad wspolny, 2 doba kontrolna
        private static long _tRows, _tRosters, _tArmRows;
        private static double _tMs;

        private static Tally Take()
        {
            var t = new Tally();
            ForEachRoster((r, kind) =>
            {
                t.Rosters++;
                var dst = t.Horse[kind];
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    if (el.Amount <= 0) continue;
                    t.Rows++;
                    var it = el.EquipmentElement.Item;
                    ArmsBook.TallyRow(t, it, el.Amount, kind);
                    int c = HorseBook.Cat(it);
                    if (c >= 0) dst[c] += el.Amount;
                }
            });
            var all = AiGear.Armories();
            if (all != null)
                foreach (var arm in all.Values)
                {
                    if (arm == null) continue;
                    foreach (var kv in arm)
                    {
                        if (kv.Value <= 0) continue;
                        t.ArmRows++;
                        ArmsBook.TallyArmory(t, kv.Key, kv.Value);
                        int c = HorseBook.Cat(kv.Key);
                        if (c >= 0) t.HorseArm[c] += kv.Value;
                    }
                }
            return t;
        }

        internal static bool Same(long[] a, long[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>Doba kontrolna: ksiega policzyla stara droga i porownala z przegladem wspolnym; rozjazd - licznik, nastepna doba tez kontrolna (stara droga).</summary>
        internal static void CheckResult(string book, bool same)
        {
            if (same) return;
            _tBad++; _tCheckNext = true;
            Log.Info("Stage6Measure (f13): ROZJAZD przegladu wspolnego w ksiedze " + book + " - linie z dzisiejszej starej drogi, jutro znow kontrola (rozjazdow w sesji " + _tBad + ").");
        }

        /// <summary>Odcinek linii "Doba gladko (f13)".</summary>
        internal static string F13Text()
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string mode = _tMode == 1 ? "przeglad wspolny" : _tMode == 2 ? "doba kontrolna (linie ze starej drogi, wspolny obok)" : "stara droga (dwa przeglady)";
            return " | Stage6Measure: " + mode + ", przeglad wspolny " + _tMs.ToString("0.0", inv) + " ms (rosterow " + _tRosters + ", wierszy " + _tRows + ", wierszy zbrojowni DTE " + _tArmRows
                   + "); kontroli w sesji " + _tChecks + ", rozjazdow " + _tBad + ", potkniec przegladu " + _tFails;
        }

        /// <summary>Przeglad rosterow swiata raz na dobe: polki osad (miasta, zamki, wsie), schowki osad, tabory partii.</summary>
        internal static void ForEachRoster(Action<ItemRoster, int> f)
        {
            foreach (var st in Settlement.All)
            {
                if (st == null) continue;
                if (st.ItemRoster != null) f(st.ItemRoster, st.IsVillage ? 1 : 0);
                ItemRoster stash = null;
                try { stash = st.Stash; } catch { }
                if (stash != null && !ReferenceEquals(stash, st.ItemRoster)) f(stash, 2);
            }
            foreach (var mp in MobileParty.All)
                if (mp != null && mp.ItemRoster != null) f(mp.ItemRoster, 3);
        }
    }

    /// <summary>
    /// KSIEGA UZBROJENIA (E6/0, PLAN 6.6): rodzaj (korpus, helm, nogi, rece, plaszcz, tarcza, bron biala, strzelecka, amunicja, uprzaz) x pasmo (t1-2, t3-4, t5-6).
    /// Stan = polki i schowki osad + tabory partii + zbrojownie DTE. ZRODLA pierwotne podpiete: warsztaty (WorkshopLaw - Measure174b.ArrWorkshop), strzelarze
    /// (TownFletchers); dorobek startowy (ColdStart) biegnie przed pierwszym spisem, wiec jest w stanie wyjsciowym; UJSCIA podpiete: zlom (ArmsScrap). PRZENIESIENIA
    /// (wiersz informacyjny - tylko przyjazdy na polki miast, druga strona w stanie): przerzut kupcow, sakiewki ludzi, zalogi. Reszta = zmiana stanu - (zrodla -
    /// ujscia) - to, czego ksiega jeszcze nie widzi (zuzycie do wraku, przecieki 174, netto lupu DTE, kuznia gracza, komplety z niczego, handel gry);
    /// mianownik = zrodla + ujscia. Kolejne okna (DteWindow, zuzycie) - nastepna sesja etapu 6.
    /// </summary>
    internal static class ArmsBook
    {
        internal const int SWorkshop = 0, SFletcher = 1, Sources = 2;
        // KBkVillage: A12 (H-4b) - rynek wsi BK (BkSeal34.RunVillage): wies kupuje sztuke z polki miasta Bound, sztuka znika (ksiegowane tylko z kluczem ArmsBookBkVillageSink)
        internal const int KScrap = 0, KRaid = 1, KSack = 2, KBkVillage = 3, Sinks = 4;   // KSack: L7 lupu miast (CitySack.Goods.cs) - uzbrojenie z polki zdobytej osady spalone przy spustoszeniu (nie unioslo sie);   // KRaid: E7/R1 (235) - uzbrojenie spalone w rabunku osada po osadzie (z magazynu wsi, nie unioslo sie)
        internal const int TTrade = 0, TPurse = 1, TGarrison = 2, Transfers = 3;
        private const int Kinds = 10, Bands = 3, Slots = Kinds * Bands;
        internal const int SlotCount = Slots, HPlaceCount = HPlaces;   // f13-doba: rozmiary pol przegladu wspolnego (Stage6Measure.Tally)
        private static readonly string[] KindNames = { "korpus", "helm", "nogi", "rece", "plaszcz", "tarcza", "bron biala", "strzelecka", "amunicja", "uprzaz" };
        private static readonly long[,] _src = new long[Sources, Slots], _sink = new long[Sinks, Slots], _xfer = new long[Transfers, Slots];
        private static long[] _prev;
        internal static double LastCostMs;
        // drobne2 (H-4b, diagnoza): uprzaz wedlug miejsca - polki osad, schowki, tabory, zbrojownie DTE, w drodze wozem, pule weteranow (stan i zmiana doby);
        // bieg dwuletni: reszta uprzezy -18 979 przy zrodlach 23 295 (-81%), t1-2 -18 228 - nie wiadomo, w ktorym miejscu znika (tylko log)
        private const int HKind = 9, HPlaces = 6;
        private static readonly string[] HPlaceNames = { "polki osad", "schowki", "tabory", "zbrojownie DTE", "w drodze wozem", "pule weteranow" };
        private static long[] _hPrev;
        private static int _stumbles;
        private static readonly HashSet<string> _errSites = new HashSet<string>();

        internal static void Reset()
        {
            Array.Clear(_src, 0, _src.Length); Array.Clear(_sink, 0, _sink.Length); Array.Clear(_xfer, 0, _xfer.Length);
            _prev = null; _hPrev = null; LastCostMs = 0; _stumbles = 0; _errSites.Clear();
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errSites.Add(where)) Log.Error("ArmsBook." + where, e);
        }

        /// <summary>Rodzaj ksiegi: 0..9; -1 = nie uzbrojenie.</summary>
        internal static int Kind(ItemObject it)
        {
            if (it == null) return -1;
            switch (it.ItemType)
            {
                case ItemObject.ItemTypeEnum.BodyArmor: case ItemObject.ItemTypeEnum.ChestArmor: return 0;
                case ItemObject.ItemTypeEnum.HeadArmor: return 1;
                case ItemObject.ItemTypeEnum.LegArmor: return 2;
                case ItemObject.ItemTypeEnum.HandArmor: return 3;
                case ItemObject.ItemTypeEnum.Cape: return 4;
                case ItemObject.ItemTypeEnum.Shield: return 5;
                case ItemObject.ItemTypeEnum.OneHandedWeapon: case ItemObject.ItemTypeEnum.TwoHandedWeapon: case ItemObject.ItemTypeEnum.Polearm: return 6;
                case ItemObject.ItemTypeEnum.Bow: case ItemObject.ItemTypeEnum.Crossbow: case ItemObject.ItemTypeEnum.Thrown:
                case ItemObject.ItemTypeEnum.Pistol: case ItemObject.ItemTypeEnum.Musket: return 7;
                case ItemObject.ItemTypeEnum.Arrows: case ItemObject.ItemTypeEnum.Bolts: case ItemObject.ItemTypeEnum.Bullets: return 8;
                case ItemObject.ItemTypeEnum.HorseHarness: return 9;
                default: return -1;
            }
        }

        private static int Slot(ItemObject it)
        {
            int k = Kind(it);
            return k < 0 ? -1 : k * Bands + Measure174b.Band(it);
        }

        private static bool On { get { var s = Settings.Current; return s != null && s.Stage6MeasureLog; } }

        internal static void Source(int src, ItemObject it, int n)
        {
            if (n <= 0 || src < 0 || src >= Sources || !On) return;
            try { int sl = Slot(it); if (sl >= 0) _src[src, sl] += n; } catch (Exception e) { Stumble("Source", e); }
        }

        internal static void Sink(int k, ItemObject it, int n)
        {
            if (n <= 0 || k < 0 || k >= Sinks || !On) return;
            try { int sl = Slot(it); if (sl >= 0) _sink[k, sl] += n; } catch (Exception e) { Stumble("Sink", e); }
        }

        /// <summary>Z Measure174b.NoteArrival (kazda nowa sztuka na polce miasta): warsztat = zrodlo, kupcy / sakiewki / zalogi = przeniesienie.</summary>
        internal static void Arrival(ItemObject it, int n, int arrKind)
        {
            if (n <= 0 || !On) return;
            if (arrKind == Measure174b.ArrWorkshop) { Source(SWorkshop, it, n); return; }
            int t = arrKind == Measure174b.ArrTrade ? TTrade : arrKind == Measure174b.ArrPurse ? TPurse : arrKind == Measure174b.ArrGarrison ? TGarrison : -1;
            if (t < 0) return;
            try { int sl = Slot(it); if (sl >= 0) _xfer[t, sl] += n; } catch (Exception e) { Stumble("Arrival", e); }
        }

        private static long Sum(long[,] a, int row) { long s = 0; for (int i = 0; i < Slots; i++) s += a[row, i]; return s; }
        private static long SumAll(long[,] a, int slot) { long s = 0; for (int r = 0; r < a.GetLength(0); r++) s += a[r, slot]; return s; }

        /// <summary>drobne2 (H-4b, diagnoza): odcinek "uprzaz wedlug miejsca" - stan i zmiana doby w kazdym miejscu stanu ksiegi (bez zmiany rachunku reszty).</summary>
        private static string HarnessPlaces(long[] hz, long[] prev)
        {
            var sb = new System.Text.StringBuilder(260);
            sb.Append(" | uprzaz wedlug miejsca (H-4b, diagnoza drobne2): ");
            for (int i = 0; i < HPlaces; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(HPlaceNames[i]).Append(' ').Append(hz[i]);
                if (prev != null) { long d = hz[i] - prev[i]; sb.Append(" (").Append(d >= 0 ? "+" : "").Append(d).Append(')'); }
            }
            // A12 (H-4b): uprzaz zjedzona dzis na rynku wsi BK (ujscie ksiegi z kluczem ArmsBookBkVillageSink) - przy kluczu wylaczonym odcinek jak dotad
            if (VillageSinkOn)
            {
                long hv = 0;
                for (int b = 0; b < Bands; b++) hv += _sink[KBkVillage, HKind * Bands + b];
                sb.Append(", zjedzone na rynku wsi BK (A12) ").Append(hv);
            }
            return sb.ToString();
        }

        private static bool VillageSinkOn { get { var s = Settings.Current; return s != null && s.ArmsBookBkVillageSink; } }

        /// <summary>A12 (H-4b): odcinek ujscia "zjedzone na rynku wsi BK" w linii E6/0 - tylko z kluczem ArmsBookBkVillageSink (wylaczony: linia jak dotad).</summary>
        private static string VillageSinkText()
        {
            return VillageSinkOn ? ", zjedzone na rynku wsi BK (A12) " + Sum(_sink, KBkVillage) : "";
        }

        /// <summary>f13-doba: wiersz rosteru w przegladzie wspolnym - ta sama regula co petla przegladu w Daily (stan, polki/tabory, uprzaz wedlug miejsca).</summary>
        internal static void TallyRow(Stage6Measure.Tally t, ItemObject it, int amount, int kind)
        {
            int sl = Slot(it);
            if (sl < 0) return;
            t.ArmsNow[sl] += amount;
            if (kind == 3) t.ArmsParties += amount; else t.ArmsShelves += amount;
            if (sl / Bands == HKind) t.ArmsHz[kind == 3 ? 2 : kind == 2 ? 1 : 0] += amount;
        }

        /// <summary>f13-doba: wiersz zbrojowni DTE w przegladzie wspolnym - ta sama regula co petla zbrojowni w Daily.</summary>
        internal static void TallyArmory(Stage6Measure.Tally t, ItemObject it, int n)
        {
            int sl = Slot(it);
            if (sl < 0) return;
            t.ArmsNow[sl] += n; t.ArmsArmories += n;
            if (sl / Bands == HKind) t.ArmsHz[3] += n;
        }

        /// <summary>use != null (f13-doba) - stan z przegladu wspolnego zamiast wlasnego przegladu rosterow i zbrojowni; check != null - doba kontrolna: liczy
        /// stara droga i porownuje z przegladem wspolnym (linia z wlasnej liczby). Oba null - jak przed paczka.</summary>
        internal static void Daily(Stage6Measure.Tally use, Stage6Measure.Tally check)
        {
            if (!On) { _prev = null; _hPrev = null; Array.Clear(_src, 0, _src.Length); Array.Clear(_sink, 0, _sink.Length); Array.Clear(_xfer, 0, _xfer.Length); return; }
            int day = (int)CampaignTime.Now.ToDays;
            var now = use != null ? use.ArmsNow : new long[Slots];
            var hz = use != null ? use.ArmsHz : new long[HPlaces];      // drobne2 (H-4b): uprzaz wedlug miejsca
            long shelves = 0, parties = 0, armories = 0;
            if (use != null) { shelves = use.ArmsShelves; parties = use.ArmsParties; armories = use.ArmsArmories; }
            else
            {
            Stage6Measure.ForEachRoster((r, kind) =>
            {
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    if (el.Amount <= 0) continue;
                    int sl = Slot(el.EquipmentElement.Item);
                    if (sl < 0) continue;
                    now[sl] += el.Amount;
                    if (kind == 3) parties += el.Amount; else shelves += el.Amount;
                    if (sl / Bands == HKind) hz[kind == 3 ? 2 : kind == 2 ? 1 : 0] += el.Amount;
                }
            });
            try
            {
                var all = AiGear.Armories();
                if (all != null)
                    foreach (var arm in all.Values)
                    {
                        if (arm == null) continue;
                        foreach (var kv in arm)
                        {
                            if (kv.Value <= 0) continue;
                            int sl = Slot(kv.Key);
                            if (sl < 0) continue;
                            now[sl] += kv.Value; armories += kv.Value;
                            if (sl / Bands == HKind) hz[3] += kv.Value;
                        }
                    }
            }
            catch (Exception e) { Stumble("Armories", e); }
            if (check != null)
                Stage6Measure.CheckResult("uzbrojenia", Stage6Measure.Same(now, check.ArmsNow) && Stage6Measure.Same(hz, check.ArmsHz)
                                          && shelves == check.ArmsShelves && parties == check.ArmsParties && armories == check.ArmsArmories);
            }
            // E6/R3 (215): hurt kupcow wozem - sztuki w drodze sa czescia stanu (zeszly z polki zrodla, na polke celu jeszcze nie weszly)
            long road = 0;
            try { TradeCarts.ForEachInTransit((it, n) => { int sl = Slot(it); if (sl >= 0) { now[sl] += n; road += n; if (sl / Bands == HKind) hz[4] += n; } }); }
            catch (Exception e) { Stumble("TradeCarts", e); }
            // E8/H (271): uprzaz weteranow w pulach karczm - czesc stanu (zbrojownia partii -> pula miasta -> zbrojownia najemcy albo polka: przeniesienie)
            long vetPool = 0;
            try { HorseFate.VetPoolHarness((it, n) => { int sl = Slot(it); if (sl >= 0) { now[sl] += n; vetPool += n; if (sl / Bands == HKind) hz[5] += n; } }); }
            catch (Exception e) { Stumble("VetPool", e); }
            // C3 (Jeff 14.10): komplety weteranow w pulach karczm - czesc stanu (zbrojownia partii -> pula miasta -> zbrojownia najemcy albo polka: przeniesienie)
            long vetKit = 0;
            try { VetKit.ForEach((it, n) => { int sl = Slot(it); if (sl >= 0) { now[sl] += n; vetKit += n; } }); }
            catch (Exception e) { Stumble("VetKit", e); }
            long total = shelves + parties + armories + road + vetPool + vetKit;
            var hPrev = _hPrev; _hPrev = hz;
            if (_prev == null)
            {
                _prev = now;
                Array.Clear(_src, 0, _src.Length); Array.Clear(_sink, 0, _sink.Length); Array.Clear(_xfer, 0, _xfer.Length);
                Log.Info("Uzbrojenie wedlug zrodla (E6/0): dzien " + day + " | stan wyjsciowy " + total + " szt. (polki i schowki osad " + shelves + ", tabory " + parties + ", zbrojownie DTE " + armories
                         + ") - reszta od nastepnej doby.");
                return;
            }
            long src = 0, sink = 0, prevTotal = 0;
            for (int i = 0; i < Sources; i++) src += Sum(_src, i);
            for (int i = 0; i < Sinks; i++) sink += Sum(_sink, i);
            foreach (var v in _prev) prevTotal += v;
            long rest = (total - prevTotal) - (src - sink);
            double den = src + sink;
            var kinds = new List<string>();
            for (int k = 0; k < Kinds; k++)
            {
                var st = new string[Bands]; var rs = new string[Bands]; var sr = new string[Bands];
                for (int b = 0; b < Bands; b++)
                {
                    int sl = k * Bands + b;
                    long f = SumAll(_src, sl) - SumAll(_sink, sl);
                    st[b] = now[sl].ToString(CultureInfo.InvariantCulture);
                    sr[b] = SumAll(_src, sl).ToString(CultureInfo.InvariantCulture);
                    rs[b] = ((now[sl] - _prev[sl]) - f).ToString(CultureInfo.InvariantCulture);
                }
                kinds.Add(KindNames[k] + " t1-2/t3-4/t5-6: stan " + string.Join("/", st) + ", zrodla " + string.Join("/", sr) + ", reszta " + string.Join("/", rs));
            }
            Log.Info("Uzbrojenie wedlug zrodla (E6/0): dzien " + day + " | zrodla: warsztaty " + Sum(_src, SWorkshop) + ", strzelarze " + Sum(_src, SFletcher) + " (dorobek startowy ColdStart - w stanie wyjsciowym)"
                     + " | ujscia: zlom " + Sum(_sink, KScrap) + ", spalone w rabunku (E7/R1) " + Sum(_sink, KRaid) + ", spalone przy lupie miast (L7) " + Sum(_sink, KSack) + VillageSinkText() + " | przeniesienia (przyjazdy na polki miast, info): przerzut kupcow " + Sum(_xfer, TTrade) + ", sakiewki " + Sum(_xfer, TPurse)
                     + ", zalogi " + Sum(_xfer, TGarrison) + " | stan " + total + " (polki i schowki osad " + shelves + ", tabory " + parties + ", zbrojownie DTE " + armories + ", w drodze wozem (215) " + road + "), zmiana " + (total - prevTotal)
                     + " | reszta " + rest + (den > 0 ? " (" + (100.0 * rest / den).ToString("0.0", CultureInfo.InvariantCulture) + "% zrodel + ujsc)" : "")
                     + " | w stanie takze pule weteranow (E8/H) " + vetPool + " szt. uprzezy, komplety weteranow (C3) " + vetKit + " szt. | nie podpiete (w reszcie): zuzycie do wraku, przecieki 174, netto lupu DTE, kuznia gracza, komplety z niczego, handel gry | koszt pomiaru E6/0 " + LastCostMs.ToString("0.0", CultureInfo.InvariantCulture) + " ms (poprzednia doba)"
                     + (_stumbles > 0 ? " | potkniecia " + _stumbles : "") + HarnessPlaces(hz, hPrev) + " | [" + string.Join("; ", kinds.ToArray()) + "].");
            _prev = now;
            Array.Clear(_src, 0, _src.Length); Array.Clear(_sink, 0, _sink.Length); Array.Clear(_xfer, 0, _xfer.Length);
        }
    }

    /// <summary>
    /// KSIEGA KONI (E6/0, PLAN 6.11) wedlug kategorii: zwykly (horse), bojowy (war_horse), szlachetny (noble_horse), juczny (sumpter_horse / pack animal),
    /// egzotyczny (wielblad, rydwan, slon, mamut, smok - MountLaw). Stan: polki miast i zamkow, magazyny wsi, schowki osad, tabory, zbrojownie DTE, konie
    /// bohaterow (slot konia rynsztunku bojowego i cywilnego). ZRODLO podpiete: produkcja wsi (OnItemProduced z osada-wsia - to jest podaz stadnin wedlug
    /// kategorii, rachunek dla K3/K4). Reszta = zmiana stanu - produkcja (polegle, mieszczanie, LegendaryLaw, rozbite partie, BK, dary startowe, kon z
    /// wzorca, zamiany ROT - kolejne okna w nastepnej sesji). Dopisek dla E6/M2: wielblady wyprodukowane w Qarthu i poza nim (kraina wsi z dnia kalibracji).
    /// </summary>
    internal static class HorseBook
    {
        private const int Cats = 5;
        internal const int CatCount = Cats;   // f13-doba: rozmiar pol przegladu wspolnego (Stage6Measure.Tally)
        private static readonly string[] CatNames = { "zwykly", "bojowy", "szlachetny", "juczny", "egzotyczny" };
        private static readonly long[] _prod = new long[Cats], _prod7 = new long[Cats];
        private static long _camelQarth, _camelElse, _camelQarth7, _camelElse7;
        private static long[] _prev;
        private static int _days7, _stumbles;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        // F3 (test zbiorczy 5-8, K1-3a krok 1 - TYLKO POMIAR): dwa ujscia dotad schowane w "reszcie niewyjasnionej".
        //  - mieszczanie: sztuki i zloto koni zdjetych z polki w konsumpcji osady (BK ItemConsumptionPatch / gra MakeConsumption), wedlug KATEGORII TARGU
        //    (horse, war_horse, noble_horse, sumpter_horse - w tych jednostkach BK liczy budzet; wielblad albo rydwan liczy sie w kategorii swojej polki),
        //    osobno miasta [0] i zamki [1]; liczy TownFolk.ConsumePostfix (nawias konsumpcji E5/0);
        //  - porzucone przez gre u lordow (DiscardItemsCampaignBehavior - sztuki znikaja bez odbiorcy): wedlug kategorii ksiegi; liczy HorseState (prefiksy K0).
        internal const int MCats = 4;
        private static readonly long[,] _folkN = new long[2, MCats], _folkGold = new long[2, MCats];
        private static int _folkBr, _folkPart;
        private static readonly long[] _disc = new long[Cats];
        private static long _discHerd, _discWeight;
        // K1-3a krok 2: konie robocze kupione przez ludnosc miasta [0] / zamku [1] w miejsce padlych ze starosci (TownFolk.WorkHorsesDaily) - kon schodzi
        // z polki i znika ze swiata z nazwanym ujsciem "starosc koni roboczych"; wedlug kategorii ksiegi (juczny 3, zwykly 0, juczny wielblad 4)
        private static readonly long[,] _workN = new long[2, Cats];
        private static readonly long[] _workGold = new long[2];

        internal static void Reset()
        {
            Array.Clear(_prod, 0, Cats); Array.Clear(_prod7, 0, Cats);
            _camelQarth = _camelElse = _camelQarth7 = _camelElse7 = 0; _prev = null; _days7 = 0; _stumbles = 0; _errSites.Clear();
            ClearSinks();
        }

        private static void ClearSinks()
        {
            Array.Clear(_folkN, 0, _folkN.Length); Array.Clear(_folkGold, 0, _folkGold.Length); _folkBr = _folkPart = 0;
            Array.Clear(_disc, 0, Cats); _discHerd = _discWeight = 0;
            Array.Clear(_workN, 0, _workN.Length); _workGold[0] = _workGold[1] = 0;
            ArmsLeaks.TakeFolkHorses();   // licznik kontrolny tej samej doby - zeby stara liczba nie wrocila po przerwie w pomiarze
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errSites.Add(where)) Log.Error("HorseBook." + where, e);
        }

        private static bool On { get { var s = Settings.Current; return s != null && s.Stage6MeasureLog; } }

        /// <summary>Kategoria konia: 0..4; -1 = nie wierzchowiec ani juczne.</summary>
        internal static int Cat(ItemObject it)
        {
            if (it == null || it.ItemType != ItemObject.ItemTypeEnum.Horse) return -1;
            var f = MountLaw.FamilyOf(it);
            if (f != MountLaw.Fam.Horse && f != MountLaw.Fam.Other) return 4;
            var c = it.ItemCategory;
            if (c == DefaultItemCategories.WarHorse) return 1;
            if (c == DefaultItemCategories.NobleHorse) return 2;
            if (c == DefaultItemCategories.PackAnimal) return 3;
            return 0;
        }

        internal static void OnItemProduced(ItemObject it, Settlement st, int n)
        {
            if (n <= 0 || st == null || !st.IsVillage || !On) return;
            try
            {
                int c = Cat(it);
                if (c < 0) return;
                _prod[c] += n; _prod7[c] += n;
                if (MountLaw.FamilyOf(it) == MountLaw.Fam.Camel)
                {
                    if (PeopleBook.CultureAtCalibration(st) == "qartheen") { _camelQarth += n; _camelQarth7 += n; }
                    else { _camelElse += n; _camelElse7 += n; }
                }
            }
            catch (Exception e) { Stumble("OnItemProduced", e); }
        }

        private static string Row(long[] a) { var p = new string[Cats]; for (int i = 0; i < Cats; i++) p[i] = a[i].ToString(CultureInfo.InvariantCulture); return string.Join("/", p); }

        // ------------------------------------------------------------ F3: ujscia "mieszczanie" i "porzucone przez gre" (tylko liczniki)
        /// <summary>Czy liczyc konie w nawiasie konsumpcji osady (TownFolk pyta raz na nawias).</summary>
        internal static bool FolkOn { get { return On; } }

        /// <summary>Kategoria TARGU konia: 0 horse, 1 war_horse, 2 noble_horse, 3 sumpter_horse; -1 = inna kategoria (bydlo, towar). Kazdy przedmiot typu Horse
        /// w grze, ROT i RBM stoi w jednej z tych czterech (przeglad XML modulow 12.10) - indeksy 0..3 zgodne z Cat(), poza egzotycznymi (Cat = 4).</summary>
        internal static int MarketCat(ItemCategory c)
        {
            if (c == null) return -1;
            if (c == DefaultItemCategories.Horse) return 0;
            if (c == DefaultItemCategories.WarHorse) return 1;
            if (c == DefaultItemCategories.NobleHorse) return 2;
            if (c == DefaultItemCategories.PackAnimal) return 3;
            return -1;
        }

        /// <summary>TownFolk.ConsumePostfix: konie zdjete z polki przez ludnosc osady w jednej konsumpcji - sztuki z dziennika sprzedazy gry, zloto = cena polki
        /// kazdego wiersza w chwili zdjecia (tyle BK dopisuje kasie osady; ile z tego naprawde placi kiesa ludu, mowi linia "Kiesa ludu"). part = wycena niepelna.</summary>
        internal static void NoteFolk(bool town, long[] pieces, long[] gold, bool part)
        {
            int k = town ? 0 : 1;
            _folkBr++; if (part) _folkPart++;
            for (int i = 0; i < MCats; i++) { _folkN[k, i] += pieces[i]; _folkGold[k, i] += gold[i]; }
        }

        /// <summary>HorseState (prefiksy DiscardItemsCampaignBehavior): kon lorda AI skasowany przez gre - nadmiar stada (herd) albo ponad udzwig.</summary>
        internal static void NoteDiscard(int cat, int n, bool herd)
        {
            if (cat < 0 || cat >= Cats || n <= 0 || !On) return;
            _disc[cat] += n;
            if (herd) _discHerd += n; else _discWeight += n;
        }

        /// <summary>TownFolk.WorkHorsesDaily (K1-3a krok 2): kon roboczy kupiony z polki osady w miejsce padlego ze starosci - UJSCIE ksiegi koni.</summary>
        internal static void NoteWork(bool town, int cat, int n, long gold)
        {
            if (cat < 0 || cat >= Cats || n <= 0 || !On) return;
            int k = town ? 0 : 1;
            _workN[k, cat] += n; _workGold[k] += gold;
        }

        private static string WorkRow(int k)
        {
            var p = new string[Cats];
            for (int i = 0; i < Cats; i++) p[i] = _workN[k, i].ToString(CultureInfo.InvariantCulture);
            return string.Join("/", p);
        }

        private static string FolkRow(long[,] a, int k)
        {
            var p = new string[MCats];
            for (int i = 0; i < MCats; i++) p[i] = a[k, i].ToString(CultureInfo.InvariantCulture);
            return string.Join("/", p);
        }

        /// <summary>use != null (f13-doba) - polki, magazyny wsi, schowki, tabory i zbrojownie DTE z przegladu wspolnego; check != null - doba kontrolna (stara droga
        /// i porownanie, linia z wlasnej liczby). Oba null - jak przed paczka.</summary>
        internal static void Daily(Stage6Measure.Tally use, Stage6Measure.Tally check)
        {
            if (!On) { _prev = null; Array.Clear(_prod, 0, Cats); Array.Clear(_prod7, 0, Cats); _camelQarth = _camelElse = _camelQarth7 = _camelElse7 = 0; _days7 = 0; ClearSinks(); try { PackSources.TakeBook(null, null, null, null); } catch { } return; }
            int day = (int)CampaignTime.Now.ToDays;
            var shelf = new long[Cats]; var vil = new long[Cats]; var stash = new long[Cats]; var party = new long[Cats]; var arm = new long[Cats]; var hero = new long[Cats];
            if (use != null) { shelf = use.Horse[0]; vil = use.Horse[1]; stash = use.Horse[2]; party = use.Horse[3]; arm = use.HorseArm; }
            else
            {
            Stage6Measure.ForEachRoster((r, kind) =>
            {
                var dst = kind == 0 ? shelf : kind == 1 ? vil : kind == 2 ? stash : party;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    if (el.Amount <= 0) continue;
                    int c = Cat(el.EquipmentElement.Item);
                    if (c >= 0) dst[c] += el.Amount;
                }
            });
            try
            {
                var all = AiGear.Armories();
                if (all != null)
                    foreach (var a in all.Values)
                    {
                        if (a == null) continue;
                        foreach (var kv in a) { if (kv.Value <= 0) continue; int c = Cat(kv.Key); if (c >= 0) arm[c] += kv.Value; }
                    }
            }
            catch (Exception e) { Stumble("Armories", e); }
            if (check != null)
                Stage6Measure.CheckResult("koni", Stage6Measure.Same(shelf, check.Horse[0]) && Stage6Measure.Same(vil, check.Horse[1]) && Stage6Measure.Same(stash, check.Horse[2])
                                          && Stage6Measure.Same(party, check.Horse[3]) && Stage6Measure.Same(arm, check.HorseArm));
            }
            try
            {
                foreach (var h in Hero.AllAliveHeroes)
                {
                    if (h == null) continue;
                    foreach (var eq in new[] { h.BattleEquipment, h.CivilianEquipment })
                    {
                        if (eq == null) continue;
                        int c = Cat(eq[EquipmentIndex.Horse].Item);
                        if (c >= 0) hero[c]++;
                    }
                }
            }
            catch (Exception e) { Stumble("Heroes", e); }
            // E6/K1 (211): pula koni weteranow w karczmach - czesc stanu (kon weterana przechodzi zbrojownia -> pula -> zbrojownia/polka: przeniesienie)
            // oraz zbrojownia gracza (DTE ArmyArmory - zgon konia gracza w misji jest ujsciem, lup DTE dla gracza przeniesieniem)
            var vet = new long[Cats]; var parm = new long[Cats];
            try { HorseFate.VetPoolCats(vet); } catch (Exception e) { Stumble("VetPool", e); }
            try { HorseFate.PlayerArmoryCats(parm); } catch (Exception e) { Stumble("PlayerArmory", e); }
            // E6/R3 (215): konie hurtu kupcow w drodze wozem - czesc stanu (przeniesienie miedzy polkami)
            var road = new long[Cats];
            try { TradeCarts.ForEachInTransit((it, n) => { int c = Cat(it); if (c >= 0) road[c] += n; }); } catch (Exception e) { Stumble("TradeCarts", e); }
            var now = new long[Cats];
            for (int i = 0; i < Cats; i++) now[i] = shelf[i] + vil[i] + stash[i] + party[i] + arm[i] + hero[i] + vet[i] + parm[i] + road[i];
            try { GarrisonHorses.NoteShelf(shelf); } catch { }   // r3-jazda: polki koni do linii "Konie zalog dla lordow" (targi nie pustoszeja; tylko odczyt, swieza tablica tej doby)
            string state = "stan " + Row(now) + " (polki miast i zamkow " + Row(shelf) + ", magazyny wsi " + Row(vil) + ", schowki " + Row(stash) + ", tabory " + Row(party)
                           + ", zbrojownie DTE " + Row(arm) + ", zbrojownia gracza " + Row(parm) + ", bohaterowie " + Row(hero) + ", pula weteranow " + Row(vet) + ", w drodze wozem (215) " + Row(road) + ")";
            if (_prev == null)
            {
                Log.Info("Konie (E6/0): dzien " + day + " | kategorie " + string.Join("/", CatNames) + " | " + state + " - stan wyjsciowy, reszta od nastepnej doby.");
                _prev = now; Array.Clear(_prod, 0, Cats); _camelQarth = _camelElse = 0; ClearSinks();
                try { PackSources.TakeBook(null, null, null, null); } catch { }   // m6b: pozycje nazwane od stanu wyjsciowego
                return;
            }
            // E6/K1 (211): zgony koni (zdjete ze zbrojowni w misji i w autobitwie) - UJSCIE; lup, dobytek rozbitych, pula weteranow, dary z polki - przeniesienia w stanie
            var dead = new long[Cats];
            try { HorseFate.DeathsCats(dead); } catch (Exception e) { Stumble("Deaths", e); }
            string k1 = "";
            try { k1 = HorseFate.BookPart(); } catch (Exception e) { Stumble("BookPart", e); }
            // F3 (K1-3a krok 1): mieszczanie (kategorie targu 0..3 = kategorie ksiegi 0..3) i porzucone przez gre - UJSCIA, zdjete z reszty i doliczone do mianownika
            var folk = new long[Cats]; long folkAll = 0;
            for (int i = 0; i < MCats; i++) { folk[i] = _folkN[0, i] + _folkN[1, i]; folkAll += folk[i]; }
            long tap = ArmsLeaks.NoFolkHorses;   // kontrola drugim licznikiem: konie zdjete w ramce "konsumpcja osady" ksiegi towarow (podsluch AddToCounts); brak = ksiega towarow bez linii
            try { tap = ArmsLeaks.TakeFolkHorses(); } catch (Exception e) { Stumble("FolkTap", e); }
            // m6b (4): pozycje nazwane - zapas drogi z niczego (konwoje DTE: DTE + gra; partie BK: GiveMounts / GivePackAnimals) i konie, ktore odchodza z partia
            // przy MobileParty.RemoveParty (po zdarzeniu zniszczenia albo bez niego - konwoje DTE); dotad wszystko to bylo w "reszcie niewyjasnionej"
            var nDte = new long[Cats]; var nBk = new long[Cats]; var gone = new long[Cats]; var removed = new long[Cats];
            try { PackSources.TakeBook(nDte, nBk, gone, removed); } catch (Exception e) { Stumble("PackSources", e); }
            var rest = new long[Cats]; long rs = 0, src = 0, sink = 0;
            // K1-3a krok 2: starosc koni roboczych (wymiana padlych - sztuka zdjeta z polki przez TownFolk.WorkHorsesDaily) - UJSCIE jak mieszczanie F3
            for (int i = 0; i < Cats; i++)
            {
                long work = _workN[0, i] + _workN[1, i];
                rest[i] = (now[i] - _prev[i]) - _prod[i] - nDte[i] - nBk[i] + dead[i] + folk[i] + _disc[i] + gone[i] + removed[i] + work;
                rs += rest[i]; src += _prod[i] + nDte[i] + nBk[i]; sink += dead[i] + folk[i] + _disc[i] + gone[i] + removed[i] + work;
            }
            Log.Info("Konie (E6/0): dzien " + day + " | kategorie " + string.Join("/", CatNames) + " | " + state + " | zrodla: stadniny wsi " + Row(_prod)
                     + " | ujscia: " + k1
                     + " | ujscia poza K1 (F3, pomiar): mieszczanie miast " + FolkRow(_folkN, 0) + " szt. za " + FolkRow(_folkGold, 0) + " zl, mieszczanie zamkow "
                     + FolkRow(_folkN, 1) + " szt. za " + FolkRow(_folkGold, 1) + " zl (kategorie targu zwykly/bojowy/szlachetny/juczny - wielblad albo rydwan w kategorii swojej polki;"
                     + " zloto = cena polki przy zdjeciu; nawiasy konsumpcji " + _folkBr + ", bez pelnej wyceny " + _folkPart + "; kontrola ksiegi towarow: "
                     + (tap != ArmsLeaks.NoFolkHorses ? "konie w ramce konsumpcji " + tap + ", roznica " + (folkAll - tap) : "brak (ksiega towarow bez linii)")
                     + "), porzucone przez gre u lordow " + Row(_disc) + " (nadmiar stada " + _discHerd + ", ponad udzwig " + _discWeight + ")"
                     + " | zrodla nazwane z niczego (m6b): konwoje DTE " + Row(nDte) + ", partie BK " + Row(nBk)
                     + " | ujscia (m6b): konie przepadle przy RemoveParty - po zniszczeniu " + Row(gone) + ", bez zniszczenia (konwoje DTE) " + Row(removed)
                     + " | starosc koni roboczych (K1-3a): miasta " + WorkRow(0) + " szt. za " + _workGold[0] + " zl, zamki " + WorkRow(1) + " szt. za " + _workGold[1] + " zl"
                     + " | wielblady wyprodukowane: w Qarthu " + _camelQarth + ", poza Qarthem " + _camelElse
                     + " | reszta niewyjasniona " + Row(rest) + " (razem " + rs + ", mianownik zrodla + ujscia " + (src + sink)
                     + (src + sink > 0 ? ", " + (100.0 * rs / (src + sink)).ToString("0", CultureInfo.InvariantCulture) + "%" : "")
                     + ") | nie podpiete (w reszcie): " + (_folkBr > 0 ? "" : "mieszczanie (nawias konsumpcji E5/0 nieczynny), ")
                     + "LegendaryLaw, BK stadniny, zbrojownie rozwiazanych zdjete przez DTE, zakupy gry"
                     + (_stumbles > 0 ? " | potkniecia " + _stumbles : "") + ".");
            _prev = now; Array.Clear(_prod, 0, Cats); _camelQarth = _camelElse = 0; ClearSinks();
            if (++_days7 >= 7)
            {
                var parts = new List<string>();
                for (int i = 0; i < Cats; i++)
                {
                    double perDay = _prod7[i] / 7.0;
                    long sh = shelf[i] + vil[i];
                    parts.Add(CatNames[i] + ": podaz stadnin " + perDay.ToString("0.0", CultureInfo.InvariantCulture) + "/d, polki i magazyny wsi " + sh
                              + (perDay > 0 ? " (" + (sh / perDay).ToString("0", CultureInfo.InvariantCulture) + " dni podazy)" : ""));
                }
                Log.Info("Rachunek koni (E6/0, co 7 dob): dzien " + day + " | " + string.Join("; ", parts.ToArray()) + " | wielblady 7 dob: w Qarthu " + _camelQarth7 + ", poza Qarthem " + _camelElse7 + ".");
                Array.Clear(_prod7, 0, Cats); _camelQarth7 = _camelElse7 = 0; _days7 = 0;
            }
        }
    }
}
