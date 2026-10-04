using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// CENY SREDNIOWIECZNE (Jeff 04.10, "1. tak" po audycie cen - docs/AUDYT-CEN.md).
    /// Zold, dochody, konie i towary w grze trzymaja skale "1 denar = ok. 1 pens"
    /// (Anglia ok. 1300-1350), a bron i zbroje byly 10-200x za drogie: komplet
    /// zolnierza t1 ~3 800 przy zoldzie 2/dzien. Przeskalowujemy WYLACZNIE uzbrojenie
    /// (zbroje, plaszcze, bron, tarcze, luki, kusze, amunicja, rzad koni), typ po
    /// typie i tier po tierze: mnoznik = cel / mediana obecnych cen handlowych w tej
    /// komorce, nigdy powyzej 1 (niczego nie drozejemy). Rozrzut wewnatrz tieru
    /// zostaje (dobry miecz dalej drozszy od kiepskiego). Konie, zwierzeta, towary
    /// i zold - bez zmian.
    ///
    /// KIEDY: jeden przebieg w OnSessionLaunched (wszystkie przedmioty juz wczytane,
    /// takze kute z save'a), plus postfix na ItemObject.DetermineValue dla broni
    /// wykutej POZNIEJ (Crafting.GenerateItem i InitializePreCraftedWeaponOnLoad
    /// wolaja DetermineValue - sprawdzone w TaleWorlds.Core 1.4.8). DetermineValue
    /// liczy od zera, wiec postfix nie skaluje dwa razy; przebieg sesji pomija sztuki
    /// juz przeliczone (zbior po referencji - przy nastepnym wczytaniu gra tworzy
    /// nowe obiekty z XML).
    ///
    /// PIERWOTNA CENA (Orig) zostaje dla progow, ktore nie sa cena: legendy kuzni
    /// (LegendaryValueFloor 25 000), XP za kucie, filtr lupu kryjowek, sufit zamowien,
    /// nagrody GrandTourney - inaczej zmiana cen po cichu przestawilaby te systemy.
    /// </summary>
    internal static class PriceLaw
    {
        private static readonly Dictionary<ItemObject, int> _orig = new Dictionary<ItemObject, int>();
        private static readonly HashSet<ItemObject> _done = new HashSet<ItemObject>();
        private static Dictionary<int, float> _factor;   // klucz: typ*10 + tier (1-6)
        private static MethodInfo _setValue;

        // cele (mediana komorki, w denarach ~ pensach), tier 1..6 - docs/AUDYT-CEN.md
        private static readonly Dictionary<ItemObject.ItemTypeEnum, int[]> Target = new Dictionary<ItemObject.ItemTypeEnum, int[]>
        {
            { ItemObject.ItemTypeEnum.HeadArmor,       new[] { 15, 40, 90, 180, 360, 720 } },
            { ItemObject.ItemTypeEnum.BodyArmor,       new[] { 60, 150, 400, 900, 1900, 3800 } },
            { ItemObject.ItemTypeEnum.LegArmor,        new[] { 8, 20, 60, 150, 300, 600 } },
            { ItemObject.ItemTypeEnum.HandArmor,       new[] { 6, 15, 45, 110, 220, 440 } },
            { ItemObject.ItemTypeEnum.Cape,            new[] { 6, 15, 30, 60, 150, 300 } },
            { ItemObject.ItemTypeEnum.OneHandedWeapon, new[] { 12, 30, 80, 200, 500, 1200 } },
            { ItemObject.ItemTypeEnum.TwoHandedWeapon, new[] { 20, 50, 130, 300, 700, 1600 } },
            { ItemObject.ItemTypeEnum.Polearm,         new[] { 8, 15, 40, 100, 250, 600 } },
            { ItemObject.ItemTypeEnum.Shield,          new[] { 6, 12, 30, 70, 150, 300 } },
            { ItemObject.ItemTypeEnum.Bow,             new[] { 12, 18, 30, 60, 120, 240 } },
            { ItemObject.ItemTypeEnum.Crossbow,        new[] { 30, 60, 120, 240, 480, 960 } },
            { ItemObject.ItemTypeEnum.Arrows,          new[] { 14, 18, 24, 36, 60, 100 } },
            { ItemObject.ItemTypeEnum.Bolts,           new[] { 14, 18, 24, 36, 60, 100 } },
            { ItemObject.ItemTypeEnum.Thrown,          new[] { 8, 15, 30, 60, 120, 240 } },
            { ItemObject.ItemTypeEnum.HorseHarness,    new[] { 30, 60, 150, 400, 900, 2000 } },
            { ItemObject.ItemTypeEnum.Sling,           new[] { 2, 3, 4, 6, 8, 10 } },
            { ItemObject.ItemTypeEnum.SlingStones,     new[] { 2, 3, 4, 6, 8, 10 } },
        };

        /// <summary>Cena sprzed przeskalowania (progi, ktore nie sa cena). Gdy prawo cen
        /// wylaczone albo sztuka nieruszona - zwykla Value.</summary>
        internal static int Orig(ItemObject it)
        {
            if (it == null) return 0;
            int v;
            return _orig.TryGetValue(it, out v) ? v : it.Value;
        }

        private static int Tier(ItemObject it)
        {
            try { return Math.Max(1, Math.Min(6, (int)it.Tier + 1)); } catch { return 1; }
        }

        private static bool SetValue(ItemObject it, int v)
        {
            if (_setValue == null) _setValue = AccessTools.PropertySetter(typeof(ItemObject), "Value");
            if (_setValue == null) return false;
            _setValue.Invoke(it, new object[] { v });
            return true;
        }

        /// <summary>Jeden przebieg po wszystkich przedmiotach (OnSessionLaunched).</summary>
        internal static void Apply()
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.MedievalArmsPrices) { Log.Info("PriceLaw: ceny sredniowieczne WYLACZONE - ceny uzbrojenia z gry."); return; }
                float scale = Math.Max(0.01f, s.MedievalArmsPriceScale);
                var all = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();

                // mediany obecnych cen handlowych w kazdej komorce typ x tier
                var cells = new Dictionary<int, List<int>>();
                foreach (var it in all)
                {
                    if (it == null || it.NotMerchandise || !Target.ContainsKey(it.ItemType)) continue;
                    int v = Orig(it);
                    if (v <= 0) continue;
                    int key = (int)it.ItemType * 10 + Tier(it);
                    List<int> l;
                    if (!cells.TryGetValue(key, out l)) cells[key] = l = new List<int>();
                    l.Add(v);
                }
                _factor = new Dictionary<int, float>();
                var report = new List<string>();
                foreach (var kv in Target)
                {
                    var parts = new List<string>();
                    for (int t = 1; t <= 6; t++)
                    {
                        int key = (int)kv.Key * 10 + t;
                        // pusta komorka - mediana najblizszego tieru (najpierw nizej, potem wyzej)
                        List<int> l = null; int src = t;
                        for (int d = 0; d < 6 && l == null; d++)
                        {
                            if (cells.TryGetValue((int)kv.Key * 10 + t - d, out l) && l.Count > 0) { src = t - d; break; }
                            l = null;
                            if (cells.TryGetValue((int)kv.Key * 10 + t + d, out l) && l.Count > 0) { src = t + d; break; }
                            l = null;
                        }
                        if (l == null) continue;
                        l.Sort();
                        float med = l[l.Count / 2];
                        float target = kv.Value[src - 1] * scale;
                        float f = Math.Min(1f, target / Math.Max(1f, med));
                        _factor[key] = f;
                        parts.Add("t" + t + " " + (int)med + "->" + (int)(med * f));
                    }
                    report.Add(kv.Key + ": " + string.Join(", ", parts.ToArray()));
                }

                int n = 0, skipped = 0;
                foreach (var it in all)
                {
                    if (it == null) continue;
                    if (_done.Contains(it)) { skipped++; continue; }
                    if (Scale(it)) n++;
                }
                Log.Info("PriceLaw: ceny uzbrojenia przeskalowane - " + n + " sztuk (" + skipped + " juz przeliczonych), skala " + scale.ToString("0.00") + ". Mediany komorek (stara -> nowa):");
                foreach (var r in report) Log.Info("PriceLaw:   " + r);
            }
            catch (Exception e) { Log.Error("PriceLaw.Apply", e); }
        }

        /// <summary>Przeskaluj jedna sztuke wedle tabeli (gdy tabela gotowa).</summary>
        private static bool Scale(ItemObject it)
        {
            try
            {
                if (_factor == null || it == null || !Target.ContainsKey(it.ItemType)) return false;
                float f;
                if (!_factor.TryGetValue((int)it.ItemType * 10 + Tier(it), out f)) return false;
                int old = it.Value;
                if (old <= 0) return false;
                if (!_orig.ContainsKey(it)) _orig[it] = old;
                int nv = Math.Max(1, (int)Math.Round(_orig[it] * f));
                if (!SetValue(it, nv)) return false;
                _done.Add(it);
                return true;
            }
            catch { return false; }
        }

        /// <summary>Bron wykuta po starcie sesji: DetermineValue liczy cene od zera -
        /// zapominamy stary oryginal i skalujemy swiezy.</summary>
        private static void DetermineValuePostfix(ItemObject __instance)
        {
            try
            {
                if (_factor == null || __instance == null) return;
                var s = Settings.Current;
                if (s == null || !s.MedievalArmsPrices) return;
                _orig.Remove(__instance);
                _done.Remove(__instance);
                Scale(__instance);
            }
            catch { }
        }

        internal static void ApplyAll(Harmony harmony)
        {
            try
            {
                var m = AccessTools.Method(typeof(ItemObject), "DetermineValue");
                if (m == null) { Log.Info("PriceLaw: brak ItemObject.DetermineValue - bron kuta w trakcie gry zostanie po starej cenie."); return; }
                harmony.Patch(m, postfix: new HarmonyMethod(typeof(PriceLaw), nameof(DetermineValuePostfix)));
            }
            catch (Exception e) { Log.Error("PriceLaw.ApplyAll", e); }
        }
    }
}
