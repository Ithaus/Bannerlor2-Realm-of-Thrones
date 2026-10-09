using System;
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
    /// </summary>
    internal static class ShelfIndex
    {
        private sealed class Idx { public int Ver, Count; public int[] All, NoUnique; }
        private sealed class Holder { public volatile Idx Cur; }

        private static ConditionalWeakTable<ItemRoster, Holder> _t = new ConditionalWeakTable<ItemRoster, Holder>();
        private static readonly ConditionalWeakTable<ItemRoster, Holder>.CreateValueCallback Make = r => new Holder();
        private static readonly int Types;
        internal static long Hits, Builds, Checks, Mismatch;
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
            Hits = Builds = Checks = Mismatch = 0; _sessionStart = -1.0; _calls = 0; _mismatchLogged = false;
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
            catch (Exception e) { Log.Error("ShelfIndex.Count", e); return -1; }
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
            return "pamiec polki: odczytow z pamieci " + Hits + ", przebudow " + Builds + ", samokontroli " + Checks + ", rozjazdow od startu sesji " + Mismatch + (On ? "" : " (WYLACZONA w MCM)");
        }

        internal static void ClearDay() { Hits = Builds = Checks = 0; }
    }
}
