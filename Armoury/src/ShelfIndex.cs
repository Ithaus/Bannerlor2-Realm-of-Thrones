using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// PACZKA 174b.5 F1 - PAMIEC POLKI (docs/PROJEKT-174B-DOWOZ-2026-10-09.md rozdz. 3.5 i "Krytyka i odpowiedzi" uwagi 4 i 11). Ten sam wynik,
    /// mniej liczenia: kazda cena sprzetu w osadzie (SupplyDemand.Factor) przechodzila CALA polke w Stock i drugi raz w Substitution (ok. 300 stosow
    /// na miasto na zapisie Jeffa). Teraz na kazdy ItemRoster (ConditionalWeakTable - polka znika z pamieci razem z osada) indeks: sztuk wedlug
    /// koszyka typ x tier (wszystkie i bez unikatow - rezerwa kramu 174b.4). Klucz waznosci (VersionNo, Count): w grze 1.4.8 kazda zmiana rosteru idzie
    /// przez AddToCounts / AddNewElement / Clear -> UpdateVersion (ItemRoster.cs:181, :218, :282). Przebudowa do NOWEGO obiektu i podmiana referencji
    /// w uchwycie (net472 nie ma ConditionalWeakTable.AddOrUpdate); VersionNo i Count czytane PRZED budowa - zmiana w trakcie budowy daje przy
    /// nastepnym odczycie rozjazd wersji i przebudowe, nigdy stary indeks z nowa wersja.
    /// Samokontrola (krytyka 4 - probkowana, zeby nie spowalniac doby pomiaru tempa): w pierwszych ShelfIndexSelfCheckDays dobach sesji co 64. odczyt,
    /// potem co 4096., liczy koszyk pelnym przejsciem polki; rozjazd -> wynik z przejscia, przebudowa indeksu, linia w logu (raz na miejsce) i licznik
    /// w linii "Koszt 171-174 (doba)". Zmiana rosteru przez refleksje innego moda bez UpdateVersion zlapie tylko klucz Count - po to samokontrola.
    /// Wylacznik ShelfIndexEnabled = false: liczenie jak dotad (petla po polce).
    /// E8/O1 (265, PLAN 8.17; RD8 K1 / O1): po WLASNYM zdjeciu albo oddaniu sztuki (ShelfBuy.Take / PutBack - AddToCounts naszego kodu) indeks nie jest
    /// przebudowywany przy nastepnej wycenie, tylko dostaje -n/+n w koszyku tej sztuki i nowe (Ver, Count) - OwnBefore / OwnAfter. Warunki: indeks byl
    /// aktualny tuz PRZED zmiana (OwnBefore oddaje ten obiekt), PO zmianie w uchwycie lezy dalej TEN SAM obiekt (nikt - takze odczyt z innego watku w
    /// trakcie AddToCounts - nie podmienil go przebudowa) i wersja polki urosla dokladnie o 1 (jedna zmiana - nasza; skok o wiecej = ktos zmienil polke
    /// w zdarzeniu albo PutBack zalozyl nowy stos przez AddNewElement - wtedy zwykla przebudowa przy odczycie). Kopia przy zapisie: nowy Idx z kopii obu
    /// tablic, poprawka w kopii, podmiana Holder.Cur - opublikowane tablice nigdy sie nie zmieniaja (odczyty biegna tez poza glownym watkiem).
    /// R9: co 64. poprawka liczy tez stara droga (Build) i porownuje wszystkie koszyki; rozjazd -> indeks z przebudowy, licznik i jedna linia w logu.
    /// Samokontrola odczytow (co 64./4096.) bez zmian. Wylacznik ShelfIndexOwnUpdate = false: jak przed paczka (przebudowa przy nastepnym odczycie).
    /// </summary>
    internal static class ShelfIndex
    {
        private sealed class Idx { public int Ver, Count; public int[] All, NoUnique; }
        private sealed class Holder { public volatile Idx Cur; }

        private static ConditionalWeakTable<ItemRoster, Holder> _t = new ConditionalWeakTable<ItemRoster, Holder>();
        private static readonly ConditionalWeakTable<ItemRoster, Holder>.CreateValueCallback Make = r => new Holder();
        private static readonly int Types;
        internal static long Hits, Builds, Checks, Mismatch;
        // E8/0 (242): przebudowy wedlug zrodla zmiany - po wlasnym ShelfBuy.Take/PutBack (wersja polki = zapamietana po naszym AddToCounts) albo reszta (gra, cudze, nasze inne)
        internal static long BuildsOwn, BuildsRest;
        private static ItemRoster _ownR;
        private static int _ownVer;
        // E8/O1 (265): poprawka indeksu po wlasnej zmianie polki (OwnBefore / OwnAfter) - liczniki doby do linii "Polka i delegaty (E8/O1)"
        internal static long OwnDeltas, OwnSkips, OwnNoIdx, OwnProbes;   // poprawione w pamieci / bez poprawki (pamiec nieaktualna albo skok wersji) / polka bez pamieci / probek 1/64
        internal static long OwnMismatch;                                 // rozjazdy probki od startu sesji (R9: prog 0)
        internal static long OwnStumbles;                                 // wyjatki OwnBefore / OwnAfter od startu sesji - Log.Error tylko pierwszy
        private static long _ownProbeDelta, _ownProbeBuild;               // doba: takty zegara probek - nowa droga (kopia + poprawka) i stara (Build)
        private static int _ownN;
        private static bool _ownMismatchLogged, _ownErrLogged;
        internal static long Stumbles;      // poprawka 174b (recenzja): wyjatki odczytu od startu sesji - Log.Error tylko pierwszy (goraca sciezka cen)
        private static bool _errLogged;
        private static double _sessionStart = -1.0;
        private static int _calls;
        private static bool _mismatchLogged;

        static ShelfIndex()
        {
            int max = 0;
            foreach (var v in Enum.GetValues(typeof(ItemObject.ItemTypeEnum))) max = Math.Max(max, (int)v);
            Types = max + 1;
        }

        internal static bool On { get { var s = Settings.Current; return s != null && s.ShelfIndexEnabled; } }

        /// <summary>Nowa gra / wczytanie (konstruktor ArmouryBehavior): nowa tablica - indeksy poprzedniej kampanii nie przezyja.</summary>
        internal static void Reset()
        {
            _t = new ConditionalWeakTable<ItemRoster, Holder>();
            ShopReserve.Reset();   // poprawka 174b: licznik potkniec rezerwy kramu - ta sama chwila co pamiec polki
            Hits = Builds = Checks = Mismatch = 0; BuildsOwn = BuildsRest = 0; _ownR = null; _sessionStart = -1.0; _calls = 0; _mismatchLogged = false; Stumbles = 0; _errLogged = false;
            ClearOwnDay(); FastCall.ClearDay(); OwnMismatch = 0; OwnStumbles = 0; _ownN = 0; _ownMismatchLogged = false; _ownErrLogged = false;   // E8/O1
        }

        private static int TierOf(ItemObject it) { try { return Math.Max(1, Math.Min(6, (int)it.Tier + 1)); } catch { return 1; } }
        private static int Slot(int type, int tier) { return type * 8 + tier; }

        private static Idx Build(ItemRoster r, int ver, int cnt)
        {
            var x = new Idx { Ver = ver, Count = cnt, All = new int[Types * 8], NoUnique = new int[Types * 8] };
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (el.Amount <= 0 || it == null) continue;
                int ty = (int)it.ItemType;
                if (ty < 0 || ty >= Types) continue;
                int s = Slot(ty, TierOf(it));
                x.All[s] += el.Amount;
                if (!ArmsPricing.IsUnique(it)) x.NoUnique[s] += el.Amount;
            }
            return x;
        }

        private static Idx Get(ItemRoster r)
        {
            var h = _t.GetValue(r, Make);
            int ver = r.VersionNo, cnt = r.Count;   // PRZED budowa (krytyka 11a)
            var x = h.Cur;
            if (x != null && x.Ver == ver && x.Count == cnt) { Hits++; return x; }
            x = Build(r, ver, cnt);
            h.Cur = x;
            Builds++;
            if (ReferenceEquals(r, _ownR) && ver == _ownVer) BuildsOwn++; else BuildsRest++;   // E8/0 (tylko licznik)
            return x;
        }

        private static bool CheckNow()
        {
            var s = Settings.Current;
            int days = s != null ? Math.Max(0, s.ShelfIndexSelfCheckDays) : 1;
            double now = CampaignTime.Now.ToDays;
            if (_sessionStart < 0) _sessionStart = now;
            int mask = now - _sessionStart < days ? 63 : 4095;
            return (++_calls & mask) == 0;
        }

        /// <summary>Petla po polce - ten sam warunek co dawne SupplyDemand.Stock (Amount > 0, typ i tier koszyka; unikaty wedlug noUnique).</summary>
        private static int Scan(ItemRoster r, int type, int tier, bool noUnique)
        {
            int n = 0;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (el.Amount <= 0 || it == null || (int)it.ItemType != type || TierOf(it) != tier) continue;
                if (noUnique && ArmsPricing.IsUnique(it)) continue;
                n += el.Amount;
            }
            return n;
        }

        /// <summary>Sztuk koszyka (typ, tier 1-6) na polce; noUnique - bez unikatow. -1 = indeks wylaczony albo wyjatek (wolajacy liczy po staremu).</summary>
        internal static int Count(ItemRoster r, ItemObject.ItemTypeEnum type, int tier, bool noUnique = false)
        {
            if (r == null || tier < 1 || tier > 6 || !On) return -1;
            try
            {
                int ty = (int)type;
                if (ty < 0 || ty >= Types) return -1;
                var x = Get(r);
                int v = noUnique ? x.NoUnique[Slot(ty, tier)] : x.All[Slot(ty, tier)];
                if (CheckNow())
                {
                    Checks++;
                    int real = Scan(r, ty, tier, noUnique);
                    if (real != v)
                    {
                        Mismatch++;
                        var h = _t.GetValue(r, Make); h.Cur = null;   // przebudowa przy nastepnym odczycie
                        if (!_mismatchLogged)
                        {
                            _mismatchLogged = true;
                            Log.Info("Pamiec polki (174b.5): ROZJAZD - koszyk " + type + " t" + tier + (noUnique ? " (bez unikatow)" : "") + ": indeks " + v + ", polka " + real
                                     + " (wersja " + r.VersionNo + ", stosow " + r.Count + ") - wynik z polki, indeks przebudowany; dalsze rozjazdy tylko w liczniku linii \"Koszt 171-174 (doba)\".");
                        }
                        return real;
                    }
                }
                return v;
            }
            catch (Exception e) { Stumbles++; if (!_errLogged) { _errLogged = true; Log.Error("ShelfIndex.Count", e); } return -1; }   // poprawka 174b: raz w logu, reszta w liczniku
        }

        /// <summary>Sztuk pasma zbroi (t1-2 / t3-4 / t5-6) bez unikatow - rezerwa kramu (174b.4). -1 = indeks wylaczony.</summary>
        internal static int Band(ItemRoster r, ItemObject.ItemTypeEnum type, int band)
        {
            int lo = band * 2 + 1;
            int a = Count(r, type, lo, true);
            if (a < 0) return -1;
            int b = Count(r, type, lo + 1, true);
            return b < 0 ? -1 : a + b;
        }

        internal static string Text()
        {
            return "pamiec polki: odczytow z pamieci " + Hits + ", przebudow " + Builds + ", samokontroli " + Checks + ", rozjazdow od startu sesji " + Mismatch + (On ? "" : " (WYLACZONA w MCM)")
                   + "; potkniecia od startu sesji: pamiec polki " + Stumbles + ", rezerwa kramu " + ShopReserve.Stumbles;
        }

        internal static void ClearDay() { Hits = Builds = Checks = 0; BuildsOwn = BuildsRest = 0; }

        /// <summary>E8/0 (242): ShelfBuy.Take/PutBack po wlasnym AddToCounts - zapamietana polka i jej wersja (przebudowa przy tej wersji = "wlasne"; tylko licznik).</summary>
        internal static void NoteOwn(ItemRoster r) { if (r == null) return; _ownR = r; _ownVer = r.VersionNo; }

        // ------------------------------------------------------------ E8/O1 (265): indeks po wlasnej zmianie polki
        internal static bool OwnOn { get { var s = Settings.Current; return s != null && s.ShelfIndexEnabled && s.ShelfIndexOwnUpdate; } }

        /// <summary>
        /// E8/O1 (265): ShelfBuy.Take / PutBack TUZ PRZED wlasnym r.AddToCounts - indeks tej polki, jesli istnieje i jest aktualny (Ver i Count = polka);
        /// inaczej null (i licznik: polka bez pamieci albo pamiec nieaktualna - po zmianie zwykla przebudowa przy odczycie). Nigdy nie rzuca.
        /// </summary>
        internal static object OwnBefore(ItemRoster r)
        {
            try
            {
                if (r == null || !OwnOn) return null;
                Holder h;
                if (!_t.TryGetValue(r, out h)) { OwnNoIdx++; return null; }   // tej polki nikt nie liczyl z pamieci - nie ma czego poprawiac
                var x = h.Cur;
                if (x == null || x.Ver != r.VersionNo || x.Count != r.Count) { OwnSkips++; return null; }
                return x;
            }
            catch (Exception e) { OwnStumbles++; if (!_ownErrLogged) { _ownErrLogged = true; Log.Error("ShelfIndex.OwnBefore", e); } return null; }
        }

        /// <summary>
        /// E8/O1 (265): ShelfBuy.Take / PutBack ZARAZ PO wlasnym r.AddToCounts(el, n) (n = -1 przy zdjeciu, +n przy oddaniu); before = wynik OwnBefore sprzed
        /// tej zmiany. Gdy w uchwycie lezy dalej ten sam obiekt i wersja polki urosla dokladnie o 1 - nowy indeks z kopii tablic z poprawka n w koszyku sztuki
        /// (te same reguly co Build: typ x tier, unikaty poza NoUnique) i z nowymi (Ver, Count). W kazdym innym przypadku nic: indeks zostaje ze stara wersja
        /// i przebuduje sie przy najblizszym odczycie (jak przed paczka). Nigdy nie rzuca (wolana z petli zakupu).
        /// </summary>
        internal static void OwnAfter(ItemRoster r, object before, EquipmentElement el, int n)
        {
            try
            {
                var x = before as Idx;
                if (x == null || r == null || n == 0) return;
                Holder h;
                var it = el.Item;
                int ver1 = r.VersionNo, cnt1 = r.Count;
                if (it == null || !_t.TryGetValue(r, out h) || !ReferenceEquals(h.Cur, x) || ver1 != x.Ver + 1) { OwnSkips++; return; }
                bool probe = (++_ownN & 63) == 0;
                long t0 = probe ? Stopwatch.GetTimestamp() : 0;
                var y = new Idx { Ver = ver1, Count = cnt1, All = new int[x.All.Length], NoUnique = new int[x.NoUnique.Length] };
                Array.Copy(x.All, y.All, x.All.Length);
                Array.Copy(x.NoUnique, y.NoUnique, x.NoUnique.Length);
                int ty = (int)it.ItemType;
                if (ty >= 0 && ty < Types)   // jak Build: typ spoza tablicy nie jest liczony
                {
                    int slot = Slot(ty, TierOf(it));
                    y.All[slot] += n;
                    if (!ArmsPricing.IsUnique(it)) y.NoUnique[slot] += n;
                }
                if (probe)   // R9: stara droga (pelna przebudowa) obok nowej - wynik i czas
                {
                    long t1 = Stopwatch.GetTimestamp();
                    var b = Build(r, ver1, cnt1);
                    long t2 = Stopwatch.GetTimestamp();
                    OwnProbes++; _ownProbeDelta += t1 - t0; _ownProbeBuild += t2 - t1;
                    int bad = -1;
                    for (int i = 0; i < b.All.Length && bad < 0; i++) if (b.All[i] != y.All[i] || b.NoUnique[i] != y.NoUnique[i]) bad = i;
                    if (bad >= 0)
                    {
                        OwnMismatch++;
                        if (!_ownMismatchLogged)
                        {
                            _ownMismatchLogged = true;
                            Log.Info("Polka i delegaty (E8/O1): ROZJAZD poprawki po wlasnej zmianie polki - koszyk " + (ItemObject.ItemTypeEnum)(bad / 8) + " t" + (bad % 8) + ": poprawka "
                                     + y.All[bad] + " (bez unikatow " + y.NoUnique[bad] + "), przebudowa " + b.All[bad] + " (bez unikatow " + b.NoUnique[bad] + "); sztuka "
                                     + it.StringId + " " + n.ToString("+0;-0", CultureInfo.InvariantCulture) + ", wersja " + x.Ver + " -> " + ver1 + ", stosow " + x.Count + " -> " + cnt1
                                     + " - indeks z przebudowy; dalsze rozjazdy tylko w liczniku linii doby.");
                        }
                        y = b;
                    }
                }
                h.Cur = y;
                OwnDeltas++;
            }
            catch (Exception e) { OwnStumbles++; if (!_ownErrLogged) { _ownErrLogged = true; Log.Error("ShelfIndex.OwnAfter", e); } }   // indeks zostaje ze stara wersja - przebudowa przy odczycie
        }

        private static void ClearOwnDay() { OwnDeltas = OwnSkips = OwnNoIdx = OwnProbes = 0; _ownProbeDelta = _ownProbeBuild = 0; }

        /// <summary>E8/O1 (265): linia doby "Polka i delegaty (E8/O1)" (ArmouryBehavior, takt doby, po ShelfBuy.Daily) - sam log.</summary>
        internal static void DailyOwn()
        {
            try
            {
                var s = Settings.Current;
                if (s != null && Campaign.Current != null)
                {
                    var inv = CultureInfo.InvariantCulture;
                    double us = 1000000.0 / Stopwatch.Frequency;
                    Log.Info("Polka i delegaty (E8/O1): dzien " + ((int)CampaignTime.Now.ToDays - 1)
                             + " | ShelfIndexOwnUpdate " + (s.ShelfIndexOwnUpdate ? "[WL]" : "[wyl]") + (s.ShelfIndexEnabled ? "" : " (pamiec polki WYLACZONA w MCM)")
                             + " | wlasne zmiany polki " + (OwnDeltas + OwnSkips + OwnNoIdx) + ": poprawione w pamieci " + OwnDeltas + ", bez poprawki " + OwnSkips
                             + " (pamiec nieaktualna przed zmiana albo skok wersji - przebudowa przy odczycie), polka bez pamieci " + OwnNoIdx
                             + " | probka 1/64 (stara i nowa droga): " + OwnProbes + ", rozjazdow od startu sesji " + OwnMismatch
                             + (OwnProbes > 0 ? ", srednio poprawka " + (_ownProbeDelta * us / OwnProbes).ToString("0.00", inv) + " us / przebudowa "
                                                + (_ownProbeBuild * us / OwnProbes).ToString("0.00", inv) + " us" : "")
                             + " | " + FastCall.Text()
                             + " | potkniecia od startu sesji " + (OwnStumbles + FastCall.Stumbles) + ".");
                }
            }
            catch (Exception e) { Log.Error("ShelfIndex.DailyOwn", e); }
            ClearOwnDay(); FastCall.ClearDay();
        }
    }
}
