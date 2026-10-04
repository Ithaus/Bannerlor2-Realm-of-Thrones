using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// WYCENA UZBROJENIA (Jeff 04.10, "zbuduj pelny system" - docs/MODEL-MATERIALOW.md, kroki 2-4).
    /// Cena w miescie = Podstawa x Wskaznik surowcow x Wskaznik oczekiwan(w popycie) x Wskaznik polki.
    /// Ten plik liczy Podstawe, Wskaznik surowcow i premie wojenne; polka zyje w SupplyDemand.
    ///
    /// KOSZT WYKUCIA KONKRETNEJ SZTUKI (Jeff: "nie tylko cena za tier - dwie zbroje t6 roznia sie
    /// waga i pancerzem"): metal = waga x udzial metalu (wedle typu i MaterialType) x 1.4 strat kucia,
    /// gatunek wedle tieru (t1-2 zelazo kute ... t5-6 stal szlachetna), wegiel 1 szt. na kg metalu,
    /// skora/len/drewno wedle typu, praca w dniach (tabela wedle typu i tieru, kolczuga 5 dni/kg,
    /// plyta 1.5 dnia/kg) x jakosc (Effectiveness sztuki / mediana jej typu i tieru, 0.6-1.8),
    /// stawka warsztatu SmithDayWage, zysk SmithProfitPercent. Ceny surowcow = Value (po MaterialLaw).
    ///
    /// PODSTAWA (suwak ArmsPriceBand): cena z gry przycieta do [koszt / B, koszt x B]; B=1 = cena
    /// z kosztu (opcja 2), B=2 = opcja 3. UNIKATY (NotMerchandise albo straz bitewna CrashScribe:
    /// unikaty/lore) - bez przyciecia: ich cena to prestiz, nie metal (Jeff: "Ramsay plyta moze byc
    /// tylko jedna, nikt jej nie bedzie kul").
    ///
    /// WSKAZNIK SUROWCOW (koszt odtworzenia, Jeff: "rynek dziala z wyprzedzeniem"): raz dziennie dla
    /// kazdego miasta lokalne ceny rudy, drewna, skory i lnu / ich Value (vanilla rynek 0.1-10),
    /// metal = 0.3 ruda + 0.7 drewno (wegiel robi koszt zelaza); sztuka dostaje srednia wazona swoich
    /// skladnikow; dochodzi plynnie (MaterialIndexInertia dziennie). Zamek bierze najblizsze miasto.
    ///
    /// OCZEKIWANIA WOJENNE (Jeff: "jak wybucha wojna, ceny ida w gore, bo wiedza, ze zaraz beda
    /// kupowac bron"): przy wypowiedzeniu wojny obie strony dostaja premie popytu WarExpectationBase
    /// x (sila wroga / sila wlasna, 0.5-2), wygasa wykladniczo w WarExpectationDays; prawdziwe zakupy
    /// armii (AiGear) ja potwierdzaja albo nie. Premia zyje w pamieci sesji (po wczytaniu od zera).
    /// </summary>
    internal static class ArmsPricing
    {
        internal sealed class Cost
        {
            public float Metal, Charcoal, Leather, Linen, Wood, Special, Labor, Total;
            // ilosci fizyczne - dla warsztatow (WorkshopLaw): kg metalu danego gatunku, skory, lnu, drewna, dni pracy (z jakoscia)
            public float MetalKg, LeatherKg, LinenKg, WoodKg, Days; public CraftingMaterials Grade;
        }

        private static readonly Dictionary<ItemObject, Cost> _cost = new Dictionary<ItemObject, Cost>();
        private static readonly Dictionary<int, float> _effMedian = new Dictionary<int, float>();
        private static ItemObject _ore, _wood, _leather, _linen;
        private static bool _built;

        // indeksy surowcow per miasto: [ruda, drewno, skora, len]
        private static readonly Dictionary<Settlement, float[]> _idx = new Dictionary<Settlement, float[]>();
        private static readonly Dictionary<Settlement, Settlement> _nearestTown = new Dictionary<Settlement, Settlement>();
        // premia oczekiwan wojennych per frakcja
        private static readonly Dictionary<IFaction, float> _war = new Dictionary<IFaction, float>();

        private static readonly float[] DaysWeapon = { 1f, 2f, 3f, 5f, 8f, 12f };
        private static readonly float[] DaysPole = { 0.5f, 1f, 1.5f, 2.5f, 4f, 6f };
        private static readonly float[] DaysThrown = { 0.5f, 0.8f, 1f, 1.5f, 2f, 3f };
        private static readonly float[] DaysShield = { 1f, 1.5f, 2f, 3f, 4f, 5f };
        private static readonly float[] DaysBow = { 2f, 3f, 4f, 6f, 8f, 12f };
        private static readonly float[] DaysXbow = { 3f, 4f, 6f, 8f, 10f, 14f };
        private static readonly float[] DaysAmmo = { 0.3f, 0.4f, 0.5f, 0.7f, 1f, 1.5f };
        private static readonly float[] DaysCape = { 0.5f, 1f, 1.5f, 2f, 4f, 6f };

        internal static bool PricingOn { get { var s = Settings.Current; return s != null && s.ArmsCostPricingEnabled; } }

        private static int TierOf(ItemObject it)
        {
            try { return Math.Max(1, Math.Min(6, (int)it.Tier + 1)); } catch { return 1; }
        }

        private static readonly Dictionary<ItemObject, bool> _unique = new Dictionary<ItemObject, bool>();
        internal static bool IsUnique(ItemObject it)
        {
            if (it == null) return false;
            bool u;
            if (_unique.TryGetValue(it, out u)) return u;     // straz CrashScribe przez refleksje - raz na przedmiot
            try { u = it.NotMerchandise || QuartermasterLaw.BarredInBattle(it); } catch { u = false; }
            _unique[it] = u;
            return u;
        }

        private static float KgPrice(ItemObject it)
        {
            if (it == null) return 0f;
            float w = it.Weight > 0.01f ? it.Weight : 1f;
            return it.Value / w;
        }

        private static CraftingMaterials GradeFor(int tier)
        {
            switch (tier)
            {
                case 1: case 2: return CraftingMaterials.Iron2;
                case 3: return CraftingMaterials.Iron3;
                case 4: return CraftingMaterials.Iron4;
                default: return CraftingMaterials.Iron5;     // t6 w ROT to nie stal valyrianska
            }
        }

        private static int Stack(ItemObject it)
        {
            try { var w = it.PrimaryWeapon; return w != null && w.MaxDataValue > 0 ? w.MaxDataValue : 1; } catch { return 1; }
        }

        // ------------------------------------------------------------ koszt wykucia
        internal static void Build()
        {
            try
            {
                _cost.Clear(); _effMedian.Clear(); _unique.Clear();
                _ore = MBObjectManager.Instance.GetObject<ItemObject>("iron");
                _wood = MBObjectManager.Instance.GetObject<ItemObject>("hardwood");
                _leather = MBObjectManager.Instance.GetObject<ItemObject>("leather");
                _linen = MBObjectManager.Instance.GetObject<ItemObject>("linen");
                var lists = new Dictionary<int, List<float>>();
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (it == null || !SupplyDemand.Equipmentish(it) || it.ItemType == ItemObject.ItemTypeEnum.Horse) continue;
                    int k = (int)it.ItemType * 10 + TierOf(it);
                    List<float> l;
                    if (!lists.TryGetValue(k, out l)) lists[k] = l = new List<float>();
                    if (it.Effectiveness > 0f) l.Add(it.Effectiveness);
                }
                foreach (var kv in lists)
                {
                    if (kv.Value.Count == 0) continue;
                    kv.Value.Sort();
                    _effMedian[kv.Key] = kv.Value[kv.Value.Count / 2];
                }
                _built = true;
                // przyklad do logu: kilka sztuk z ich kosztem i podstawa
                var s = Settings.Current;
                var sample = new List<string>();
                foreach (var id in new[] { "ramsay_armor", "sturgian_fortified_armor", "casterly_heavy_helm", "battania_sword_5_t5", "stark_boots_1" })
                {
                    var it = MBObjectManager.Instance.GetObject<ItemObject>(id);
                    if (it == null) continue;
                    var c = CostOf(it);
                    sample.Add(id + " (" + it.ItemType + " t" + TierOf(it) + ", " + it.Weight.ToString("0.0") + " kg): wartosc " + it.Value
                               + ", koszt wykucia " + (c != null ? (int)c.Total : 0) + ", podstawa " + (int)BaseOf(it) + (IsUnique(it) ? " (UNIKAT - prestiz)" : ""));
                }
                Log.Info("ArmsPricing: wycena uzbrojenia " + (PricingOn ? "CZYNNA" : "wylaczona") + " - bezpiecznik x" + s.ArmsPriceBand.ToString("0.0")
                         + ", praca " + s.SmithDayWage + "/dzien, zysk " + s.SmithProfitPercent + "%. Przyklady: " + string.Join("; ", sample.ToArray()));
            }
            catch (Exception e) { Log.Error("ArmsPricing.Build", e); }
        }

        internal static Cost CostOf(ItemObject it)
        {
            if (it == null) return null;
            Cost c;
            if (_cost.TryGetValue(it, out c)) return c;
            c = Compute(it);
            _cost[it] = c;
            return c;
        }

        private static Cost Compute(ItemObject it)
        {
            var s = Settings.Current;
            var c = new Cost();
            try
            {
                int t = TierOf(it);
                float w = Math.Max(0.01f, it.Weight);
                var metalItem = Recipes.MaterialItem(GradeFor(t));
                var charItem = Recipes.MaterialItem(CraftingMaterials.Charcoal);
                float metalKg = 0f, leatherKg = 0f, linenKg = 0f, woodKg = 0f, days = 0f, special = 0f;
                var mat = it.ArmorComponent != null ? it.ArmorComponent.MaterialType : ArmorComponent.ArmorMaterialTypes.None;
                switch (it.ItemType)
                {
                    case ItemObject.ItemTypeEnum.HeadArmor:
                    case ItemObject.ItemTypeEnum.BodyArmor:
                    case ItemObject.ItemTypeEnum.LegArmor:
                    case ItemObject.ItemTypeEnum.HandArmor:
                    case ItemObject.ItemTypeEnum.HorseHarness:
                        if (mat == ArmorComponent.ArmorMaterialTypes.Plate) { metalKg = w * 0.9f * 1.4f; linenKg = w * 0.1f; days = w * 1.5f + t; }
                        else if (mat == ArmorComponent.ArmorMaterialTypes.Chainmail) { metalKg = w * 0.9f * 1.4f; linenKg = w * 0.1f; days = w * 5f; }
                        else if (mat == ArmorComponent.ArmorMaterialTypes.Leather) { leatherKg = w * 0.85f * 1.2f; metalKg = w * 0.15f * 1.4f; days = w * 1f + t; }
                        else { linenKg = w * 1.1f; days = w * 0.7f + t * 0.5f; }
                        break;
                    case ItemObject.ItemTypeEnum.Cape:
                        linenKg = w * 1.1f; days = DaysCape[t - 1];
                        if (t >= 5) { leatherKg = w * 0.5f; }                 // futro na lepszych plaszczach
                        break;
                    case ItemObject.ItemTypeEnum.OneHandedWeapon:
                        metalKg = w * 0.85f * 1.4f; woodKg = w * 0.15f; days = DaysWeapon[t - 1]; break;
                    case ItemObject.ItemTypeEnum.TwoHandedWeapon:
                        metalKg = w * 0.85f * 1.4f; woodKg = w * 0.15f; days = DaysWeapon[t - 1] * 1.3f; break;
                    case ItemObject.ItemTypeEnum.Polearm:
                        metalKg = w * 0.35f * 1.4f; woodKg = w * 0.65f * 1.5f; days = DaysPole[t - 1]; break;
                    case ItemObject.ItemTypeEnum.Thrown:
                        { int n = Stack(it); metalKg = w * n * 0.3f * 1.4f; woodKg = w * n * 0.7f * 1.5f; days = DaysThrown[t - 1]; }
                        break;
                    case ItemObject.ItemTypeEnum.Shield:
                        metalKg = w * 0.1f * 1.4f; woodKg = w * 0.8f * 1.5f; leatherKg = w * 0.1f; days = DaysShield[t - 1]; break;
                    case ItemObject.ItemTypeEnum.Bow:
                        woodKg = w * 2f; special = 20f * t; days = DaysBow[t - 1]; break;
                    case ItemObject.ItemTypeEnum.Crossbow:
                        metalKg = w * 0.3f * 1.4f; woodKg = w * 0.7f * 1.5f; days = DaysXbow[t - 1]; break;
                    case ItemObject.ItemTypeEnum.Arrows:
                    case ItemObject.ItemTypeEnum.Bolts:
                        { int n = Stack(it); metalKg = w * n * 0.15f * 1.4f; woodKg = w * n * 0.85f * 1.5f; days = DaysAmmo[t - 1]; }
                        break;
                    default:
                        return null;
                }
                // jakosc konkretnej sztuki wzgledem jej typu i tieru (Jeff: pancerz, jaki daje)
                float q = 1f;
                float med;
                if (it.Effectiveness > 0f && _effMedian.TryGetValue((int)it.ItemType * 10 + t, out med) && med > 0f)
                    q = MBMath.ClampFloat(it.Effectiveness / med, 0.6f, 1.8f);
                c.Metal = metalKg * KgPrice(metalItem);
                c.Charcoal = metalKg * (charItem != null ? charItem.Value * 2f * 0.5f : 9f);   // ~1 szt. (0.5 kg) wegla na kg metalu
                c.Leather = leatherKg * KgPrice(_leather);
                c.Linen = linenKg * KgPrice(_linen);
                c.Wood = woodKg * KgPrice(_wood);
                c.Special = special;
                c.MetalKg = metalKg; c.LeatherKg = leatherKg; c.LinenKg = linenKg; c.WoodKg = woodKg; c.Days = days * q; c.Grade = GradeFor(t);
                c.Labor = days * q * Math.Max(0f, s.SmithDayWage);
                float raw = c.Metal + c.Charcoal + c.Leather + c.Linen + c.Wood + c.Special + c.Labor;
                c.Total = raw * (1f + Math.Max(0f, s.SmithProfitPercent) / 100f);
            }
            catch { return null; }
            return c;
        }

        /// <summary>Podstawa ceny: wartosc z gry w granicach bezpiecznika, unikaty bez granic.</summary>
        internal static float BaseOf(ItemObject it)
        {
            if (it == null) return 0f;
            float v = it.Value;
            if (!PricingOn || IsUnique(it) || it.ItemType == ItemObject.ItemTypeEnum.Horse) return v;
            var c = CostOf(it);
            if (c == null || c.Total <= 0f) return v;
            float b = Settings.Current.ArmsPriceBand;
            if (b <= 1.001f) return c.Total;
            return MBMath.ClampFloat(v, c.Total / b, c.Total * b);
        }

        // ------------------------------------------------------------ wskaznik surowcow
        private static float LocalRatio(Settlement town, ItemObject it)
        {
            try
            {
                if (town == null || town.Town == null || it == null || it.Value <= 0) return 1f;
                int p = town.Town.MarketData.GetPrice(it, null, false, null);
                return MBMath.ClampFloat(p / (float)it.Value, 0.1f, 10f);
            }
            catch { return 1f; }
        }

        private static Settlement MarketOf(Settlement st)
        {
            if (st == null) return null;
            if (st.IsTown) return st;
            Settlement t;
            if (_nearestTown.TryGetValue(st, out t)) return t;
            float best = float.MaxValue;
            var pos = st.GetPosition2D;
            foreach (var x in Settlement.All)
            {
                if (x == null || !x.IsTown) continue;
                float d = pos.Distance(x.GetPosition2D);
                if (d < best) { best = d; t = x; }
            }
            _nearestTown[st] = t;
            return t;
        }

        /// <summary>Codzienny przelicznik: indeksy surowcow per miasto (z bezwladnoscia), wygaszanie premii wojennych, spis.</summary>
        internal static void Daily()
        {
            try
            {
                var s = Settings.Current;
                if (!_built) Build();
                float inertia = MBMath.ClampFloat(s.MaterialIndexInertia, 0.01f, 1f);
                if (s.MaterialIndexEnabled)
                {
                    foreach (var st in Settlement.All)
                    {
                        if (st == null || !st.IsTown) continue;
                        var target = new[] { LocalRatio(st, _ore), LocalRatio(st, _wood), LocalRatio(st, _leather), LocalRatio(st, _linen) };
                        float[] cur;
                        if (!_idx.TryGetValue(st, out cur)) { _idx[st] = target; continue; }
                        for (int i = 0; i < 4; i++) cur[i] += (target[i] - cur[i]) * inertia;
                    }
                }
                // premie wojenne gasna
                float days = Math.Max(1f, s.WarExpectationDays);
                var keys = new List<IFaction>(_war.Keys);
                foreach (var f in keys)
                {
                    float v = _war[f] * (1f - 1f / days);
                    if (v < 0.01f) _war.Remove(f); else _war[f] = v;
                }
                Census();
            }
            catch (Exception e) { Log.Error("ArmsPricing.Daily", e); }
        }

        /// <summary>Wskaznik surowcow konkretnej sztuki w osadzie (1 = ceny normalne).</summary>
        internal static float MaterialIndex(Settlement st, ItemObject it)
        {
            try
            {
                var s = Settings.Current;
                if (!s.MaterialIndexEnabled) return 1f;
                var m = MarketOf(st);
                float[] ix;
                if (m == null || !_idx.TryGetValue(m, out ix)) return 1f;
                var c = CostOf(it);
                if (c == null) return 1f;
                float raw = c.Metal + c.Charcoal + c.Leather + c.Linen + c.Wood + c.Special + c.Labor;
                if (raw <= 0f) return 1f;
                float metalIdx = 0.3f * ix[0] + 0.7f * ix[1];
                float now = c.Metal * metalIdx + c.Charcoal * ix[1] + c.Leather * ix[2] + c.Linen * ix[3] + c.Wood * ix[1] + c.Special + c.Labor;
                return MBMath.ClampFloat(now / raw, 0.3f, 5f);
            }
            catch { return 1f; }
        }

        /// <summary>Laczny mnoznik ceny w osadzie wzgledem Value: podstawa x surowce (bez polki).</summary>
        internal static float Multiplier(Settlement st, ItemObject it)
        {
            try
            {
                if (it == null || it.Value <= 0) return 1f;
                if (it.ItemType == ItemObject.ItemTypeEnum.Horse) return 1f;
                float m = 1f;
                if (PricingOn) m *= BaseOf(it) / it.Value;
                if (!IsUnique(it)) m *= MaterialIndex(st, it);   // prestiz nie zalezy od ceny rudy
                return MBMath.ClampFloat(m, 0.02f, 50f);
            }
            catch { return 1f; }
        }

        // ------------------------------------------------------------ oczekiwania wojenne
        internal static void OnWarDeclared(IFaction a, IFaction b, DeclareWarAction.DeclareWarDetail detail)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.WarExpectationEnabled) return;
                if (a == null || b == null || a.IsBanditFaction || b.IsBanditFaction) return;
                Bump(a, b, s); Bump(b, a, s);
            }
            catch (Exception e) { Log.Error("ArmsPricing.OnWarDeclared", e); }
        }

        private static void Bump(IFaction own, IFaction enemy, Settings s)
        {
            float ratio = MBMath.ClampFloat(enemy.CurrentTotalStrength / Math.Max(1f, own.CurrentTotalStrength), 0.5f, 2f);
            float add = Math.Max(0f, s.WarExpectationBase) * ratio;
            float cur; _war.TryGetValue(own, out cur);
            _war[own] = Math.Min(3f, cur + add);
            Log.Info("ArmsPricing: wojna " + own.Name + " z " + enemy.Name + " - kupcy oczekuja zakupow broni: premia popytu +"
                     + (add * 100f).ToString("0") + "% (sila wroga/wlasna " + ratio.ToString("0.00") + "), razem " + (_war[own] * 100f).ToString("0") + "%.");
        }

        internal static float WarPremium(IFaction f)
        {
            float v;
            return f != null && _war.TryGetValue(f, out v) ? v : 0f;
        }

        // ------------------------------------------------------------ spis
        private static void Census()
        {
            try
            {
                var perType = new Dictionary<ItemObject.ItemTypeEnum, int>();
                int total = 0;
                foreach (var st in Settlement.All)
                {
                    if (st == null || (!st.IsTown && !st.IsCastle) || st.ItemRoster == null) continue;
                    var r = st.ItemRoster;
                    for (int i = 0; i < r.Count; i++)
                    {
                        var el = r.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (el.Amount <= 0 || !SupplyDemand.Equipmentish(it)) continue;
                        int n; perType.TryGetValue(it.ItemType, out n); perType[it.ItemType] = n + el.Amount;
                        total += el.Amount;
                    }
                }
                var oreL = new List<float>(); var woodL = new List<float>();
                foreach (var kv in _idx) { oreL.Add(kv.Value[0]); woodL.Add(kv.Value[1]); }
                oreL.Sort(); woodL.Sort();
                var parts = new List<string>();
                foreach (var kv in perType) parts.Add(kv.Key + " " + kv.Value);
                var wars = new List<string>();
                foreach (var kv in _war) wars.Add(kv.Key.Name + " +" + (kv.Value * 100f).ToString("0") + "%");
                Log.Info("Rynek broni: na polkach " + total + " szt. [" + string.Join(", ", parts.ToArray()) + "]; indeks rudy min/med/max "
                         + Q(oreL, 0f) + "/" + Q(oreL, 0.5f) + "/" + Q(oreL, 1f) + ", drewna " + Q(woodL, 0f) + "/" + Q(woodL, 0.5f) + "/" + Q(woodL, 1f)
                         + "; premie wojenne: " + (wars.Count > 0 ? string.Join(", ", wars.ToArray()) : "brak") + ".");
            }
            catch { }
        }

        private static string Q(List<float> l, float q)
        {
            if (l.Count == 0) return "-";
            int i = Math.Min(l.Count - 1, (int)(q * (l.Count - 1)));
            return l[i].ToString("0.00");
        }
    }
}
