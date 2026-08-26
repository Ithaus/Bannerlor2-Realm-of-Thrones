using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>Receptury. Gra nie ma ich dla zbroi - skladamy je z kilku gatunkow metalu i surowcow miekkich.</summary>
    internal static class Recipes
    {
        internal struct Part
        {
            public ItemObject Item;
            public int Count;
            public Part(ItemObject i, int c) { Item = i; Count = c; }
        }

        internal struct Recipe
        {
            public List<Part> Parts;
            public int Stamina;
            public int SkillNeeded;
            public int Tier;
            public bool Ranged;      // luk/kusza/amunicja - lzejsze ryzyko i stamina niz platnerka
        }

        // --- surowce miekkie: szukamy po ID, bo ROT moze miec swoje ---
        private static ItemObject _leather, _linen, _velvet, _wood;
        private static bool _resolved;

        private static ItemObject Resolve(params string[] ids)
        {
            foreach (var id in ids)
            {
                var it = MBObjectManager.Instance.GetObject<ItemObject>(id);
                if (it != null) return it;
            }
            return null;
        }

        internal static void ResolveGoods()
        {
            if (_resolved) return;
            _resolved = true;
            try
            {
                _leather = Resolve("leather", "hides", "fur");
                _linen   = Resolve("linen", "flax", "wool", "cotton");
                _velvet  = Resolve("velvet", "silk", "cotton");
                _wood    = MaterialItem(CraftingMaterials.Wood);
                Log.Info("Surowce: leather=" + Name(_leather) + " linen=" + Name(_linen) +
                         " velvet=" + Name(_velvet) + " wood=" + Name(_wood));
            }
            catch (Exception e) { Log.Error("ResolveGoods", e); }
        }

        private static string Name(ItemObject i) { return i != null ? i.StringId : "(brak)"; }

        // dostep dla FletchForge (zdjecie skory/lnu kategoria przy kuciu pancerzy BK)
        internal static ItemObject SoftLeather { get { ResolveGoods(); return _leather; } }
        internal static ItemObject SoftLinen { get { ResolveGoods(); return _linen; } }

        /// <summary>
        /// Ile jednostek materialu wart jest ten pancerz - regula Jeffa:
        /// "ile daje pancerza, tyle materialu, plus ktory tier". Suma punktow
        /// ochrony / ArmorPointsPerMaterial, razy bonus za tier.
        /// </summary>
        internal static int ArmourUnits(ItemObject item)
        {
            try
            {
                var s = Settings.Current;
                if (item == null || !item.HasArmorComponent) return 0;
                var a = item.ArmorComponent;
                int total = a.HeadArmor + a.BodyArmor + a.LegArmor + a.ArmArmor;
                if (total <= 0) return 1;
                float units = total / MathF.Max(1f, s.ArmorPointsPerMaterial);
                units *= 1f + MathF.Max(0f, item.Tierf - 1f) * MathF.Max(0f, s.ArmorTierBonusPercent) / 100f;
                // POLOWA rachunku (Jeff) liczy sie TUTAJ, raz dla wszystkich:
                // zakladka CRAFT BannerKings, nasze menu, naprawa i przetop
                // musza widziec te sama liczbe - inaczej mozna bylo kuc taniej
                // u BK i odzyskiwac wiecej metalu w naszym tyglu.
                float scale = MBMath.ClampFloat(s.ArmorMaterialScale, 0.1f, 2f);
                return MathF.Max(1, MathF.Round(units * scale));
            }
            catch { return 1; }
        }

        /// <summary>
        /// PRAWDZIWY POZIOM WYROBU. W grze ItemTiers.Tier1 == 0, wiec
        /// "(int)item.Tier" zanizal KAZDY wyrob o jeden stopien: luk tieru 6
        /// dostawal stal tieru 5, pancerz tieru 3 material tieru 2, a proba
        /// umiejetnosci byla o caly stopien za niska. Tu raz na zawsze: 1..6.
        /// </summary>
        internal static int Grade(ItemObject item)
        {
            try
            {
                if (item == null) return 1;
                int t = (int)item.Tier + 1;
                if (t < 1) t = 1;
                if (t > 6) t = 6;
                return t;
            }
            catch { return 1; }
        }

        internal static Recipe For(ItemObject item)
        {
            var r = BuildRecipe(item);
            try { if (IsLegendary(item)) r = Legendize(r); } catch { }
            return r;
        }

        /// <summary>
        /// LEGENDA = rzecz spoza kramow (NotMerchandise) warta krocie. Zeby
        /// nie bylo jej "x100" (Jeff), kwit jest legendarny: materialy
        /// wielokrotnie, najszlachetniejsza stal obowiazkowo, mistrzowski
        /// prog umiejetnosci - a Forge pilnuje, ze legenda moze byc TYLKO JEDNA.
        /// </summary>
        internal static bool IsLegendary(ItemObject it)
        {
            try
            {
                var s = Settings.Current;
                return it != null && it.NotMerchandise && it.Value >= MathF.Max(1000f, s.LegendaryValueFloor);
            }
            catch { return false; }
        }

        private static Recipe Legendize(Recipe r)
        {
            try
            {
                var s = Settings.Current;
                float f = MathF.Max(1f, s.LegendaryMaterialFactor);
                for (int i = 0; i < r.Parts.Count; i++)
                    r.Parts[i] = new Part(r.Parts[i].Item, MathF.Max(1, MathF.Ceiling(r.Parts[i].Count * f)));
                // legenda wymaga najszlachetniejszej stali, czymkolwiek by nie byla
                Add(r, MaterialItem(CraftingMaterials.Iron6), MathF.Max(1, r.Tier));
                r.Stamina = Math.Min(100, r.Stamina * 3);
                r.SkillNeeded = Math.Max(r.SkillNeeded, s.LegendarySkillNeeded);
            }
            catch (Exception e) { Log.Error("Legendize", e); }
            return r;
        }

        private static Recipe BuildRecipe(ItemObject item)
        {
            ResolveGoods();
            var s = Settings.Current;
            var r = new Recipe();
            r.Parts = new List<Part>();
            try
            {
                int tier = Grade(item);
                r.Tier = tier;
                float weight = MathF.Max(0.5f, item.Weight);
                int units = ArmourUnits(item);
                // pancerz liczy sie z punktow ochrony; bron/tarcze/strzaly dalej z wagi
                float baseIron = item.HasArmorComponent && units > 0
                    ? units
                    : weight * s.IronPerWeightUnit * ClassFactor(item.ItemType);

                // --- tkanina i skora: zadnego metalu, zadnego wegla - krawiec i rymarz, nie hutnik ---
                var armourStuff = ArmourMaterial(item);
                int softCap = MathF.Max(1, tier * MathF.Max(1, s.SoftMaterialPerTier));
                if (armourStuff == ArmorComponent.ArmorMaterialTypes.Cloth)
                {
                    int u = units > 0 ? units : (int)MathF.Max(1, MathF.Ceiling(weight * 1.2f));
                    int soft = Math.Min(u, softCap);
                    Add(r, _linen, soft);
                    if (u - soft > 0) Add(r, MaterialItem(IronForTier(tier)), u - soft);   // okucia, nity, sprzaczki
                    if (tier >= 5) Add(r, _velvet, MathF.Max(1, MathF.Ceiling(soft * 0.3f)));
                    r.Stamina = MathF.Max(3, (int)(tier * s.StaminaPerTier * 0.4f));
                    r.SkillNeeded = MathF.Max(0, (tier - 1) * s.SmithingSkillPerTier - 20);
                    return r;
                }
                if (armourStuff == ArmorComponent.ArmorMaterialTypes.Leather)
                {
                    int u = units > 0 ? units : (int)MathF.Max(1, MathF.Ceiling(weight * 0.8f));
                    int soft = Math.Min(u, softCap);
                    int hide = MathF.Max(1, MathF.Ceiling(soft * 0.67f));
                    if (hide > soft) hide = soft;
                    Add(r, _leather, hide);
                    if (soft - hide > 0) Add(r, _linen, soft - hide);
                    if (u - soft > 0) Add(r, MaterialItem(IronForTier(tier)), u - soft);
                    r.Stamina = MathF.Max(4, (int)(tier * s.StaminaPerTier * 0.6f));
                    r.SkillNeeded = MathF.Max(0, (tier - 1) * s.SmithingSkillPerTier - 10);
                    return r;
                }

                // --- LUCZARNIA (Jeff: "dodaj do forge tworzenie kuszy lukow strzal boltow"):
                // luk to DREWNO, rog i sciegno - zelazo tylko na okucia, zamki i groty.
                // Stary przepis liczyl luki jak plachy metalu, stad bzdurne kwity. ---
                var tt = item.ItemType;
                if (tt == ItemObject.ItemTypeEnum.Bow || tt == ItemObject.ItemTypeEnum.Crossbow
                    || tt == ItemObject.ItemTypeEnum.Arrows || tt == ItemObject.ItemTypeEnum.Bolts)
                {
                    // cieciwa: len - a gdy lnu w sakwach brak (w Westeros bywa
                    // nie do kupienia), starczy rzemien ze skory (sciegno)
                    var bowstring = ((_linen == null || CountInInventory(_linen) <= 0)
                                     && _leather != null && CountInInventory(_leather) > 0) ? _leather : _linen;
                    if (tt == ItemObject.ItemTypeEnum.Bow)
                    {
                        Add(r, _wood, MathF.Max(2, MathF.Ceiling(weight * 2f)));
                        Add(r, bowstring, 1);
                        // okucia ROSNA Z TIEREM 1:1 (Jeff: "luk tier 6 = stal tier 6")
                        if (tier >= 3) Add(r, MaterialItem(IronForTier(tier)), 1);
                        if (tier >= 4) Add(r, _leather, 1);                             // owijka chwytu
                    }
                    else if (tt == ItemObject.ItemTypeEnum.Crossbow)
                    {
                        Add(r, _wood, MathF.Max(2, MathF.Ceiling(weight * 1.2f)));
                        Add(r, MaterialItem(IronForTier(tier)),
                            MathF.Max(1, MathF.Ceiling(baseIron * 0.5f)));              // zamek, orzech, strzemie - stal wg tieru
                        Add(r, MaterialItem(CraftingMaterials.Charcoal), 1);
                        Add(r, bowstring, 1);
                    }
                    else
                    {
                        // strzaly i belty: DREWNO i GROTY, nic wiecej - lotki
                        // strugasz z piora znalezionego po drodze, nie z beli lnu.
                        // Groty rosna z tierem: strzaly t6 = stal thamaskenska.
                        Add(r, _wood, 2);                                               // promienie i brzechwy
                        Add(r, MaterialItem(IronForTier(tier)), 1);                     // groty wg tieru
                    }
                    bool ammo = tt == ItemObject.ItemTypeEnum.Arrows || tt == ItemObject.ItemTypeEnum.Bolts;
                    // wiazka strzal to nie kirys: 10x mniej staminy niz stara stawka (Jeff);
                    // luki i kusze: stary mnoznik 0.8 pozwalal wykuc 2 luki na sesje -
                    // luczarnia ma byc lekka jak kuznia broni (Jeff), stad wlasny mnoznik
                    r.Ranged = true;
                    r.Stamina = ammo
                        ? MathF.Max(2, (int)(tier * s.StaminaPerTier * 0.05f))
                        : MathF.Max(4, (int)(tier * s.StaminaPerTier * MathF.Max(0.05f, s.RangedStaminaFactor)));
                    r.SkillNeeded = MathF.Max(0, (tier - 1) * s.SmithingSkillPerTier - 10);
                    return r;
                }

                // --- metal: im wyzszy wyrob, tym wiecej gatunkow. Szlachetny stop na wierzch, tanszy na rdzen ---
                if (tier >= 3)
                {
                    Add(r, MaterialItem(IronForTier(tier)),     MathF.Max(1, MathF.Ceiling(baseIron * 0.35f)));
                    Add(r, MaterialItem(IronForTier(tier - 1)), MathF.Max(1, MathF.Ceiling(baseIron * 0.35f)));
                    Add(r, MaterialItem(IronForTier(tier - 2)), MathF.Max(1, MathF.Ceiling(baseIron * 0.30f)));
                }
                else if (tier == 2)
                {
                    Add(r, MaterialItem(IronForTier(2)), MathF.Max(1, MathF.Ceiling(baseIron * 0.5f)));
                    Add(r, MaterialItem(IronForTier(1)), MathF.Max(1, MathF.Ceiling(baseIron * 0.5f)));
                }
                else
                {
                    Add(r, MaterialItem(IronForTier(1)), MathF.Max(1, MathF.Ceiling(baseIron)));
                }

                Add(r, MaterialItem(CraftingMaterials.Charcoal),
                    MathF.Max(1, MathF.Ceiling(baseIron * s.CharcoalPerIron)));

                // --- surowce miekkie: podszycie, pasy, wyscielka ---
                // wyscielka pod metal - najwyzej tyle, ile pozwala limit na tier
                if (tier <= 2)
                    Add(r, _linen, Math.Min(softCap, MathF.Max(1, MathF.Ceiling(weight * 0.5f))));
                else if (tier <= 4)
                    Add(r, _leather, Math.Min(softCap, MathF.Max(1, MathF.Ceiling(weight * 0.6f))));
                else
                {
                    Add(r, _leather, Math.Min(softCap, MathF.Max(1, MathF.Ceiling(weight * 0.5f))));
                    Add(r, _velvet, Math.Min(softCap, MathF.Max(1, MathF.Ceiling(weight * 0.3f))));
                }

                if (item.ItemType == ItemObject.ItemTypeEnum.Shield ||
                    item.ItemType == ItemObject.ItemTypeEnum.Bow ||
                    item.ItemType == ItemObject.ItemTypeEnum.Crossbow ||
                    item.ItemType == ItemObject.ItemTypeEnum.Arrows ||
                    item.ItemType == ItemObject.ItemTypeEnum.Bolts)
                    Add(r, _wood, MathF.Max(1, MathF.Ceiling(weight)));

                float fiddly = IsFiddly(item.ItemType) ? (1f + s.FiddlyStaminaBonus) : 1f;
                r.Stamina = MathF.Max(5, (int)(tier * s.StaminaPerTier * fiddly));
                r.SkillNeeded = (tier - 1) * s.SmithingSkillPerTier;   // tier 1 od zera - zaczynasz od podkówek, nie od plach
            }
            catch (Exception e) { Log.Error("Recipes.For", e); }
            return r;
        }

        /// <summary>Z czego naprawde jest ta czesc pancerza. Tarcze i bron traktujemy jak metal.</summary>
        internal static ArmorComponent.ArmorMaterialTypes ArmourMaterial(ItemObject item)
        {
            try
            {
                if (item == null || !item.HasArmorComponent) return ArmorComponent.ArmorMaterialTypes.Plate;
                if (item.ItemType == ItemObject.ItemTypeEnum.Shield) return ArmorComponent.ArmorMaterialTypes.Plate;
                return item.ArmorComponent.MaterialType;
            }
            catch { return ArmorComponent.ArmorMaterialTypes.Plate; }
        }

        /// <summary>Czy to robota dla tygla - metal. Tkanina i skora nie topnieja w rude.</summary>
        internal static bool IsMetalwork(ItemObject item)
        {
            var m = ArmourMaterial(item);
            return m == ArmorComponent.ArmorMaterialTypes.Plate || m == ArmorComponent.ArmorMaterialTypes.Chainmail;
        }

        /// <summary>Ile stopu zjada dana czesc. Kirys marnuje najwiecej, rekawice najmniej.</summary>
        internal static float ClassFactor(ItemObject.ItemTypeEnum t)
        {
            var s = Settings.Current;
            switch (t)
            {
                case ItemObject.ItemTypeEnum.BodyArmor:    return s.ClassCostBody;
                case ItemObject.ItemTypeEnum.LegArmor:     return s.ClassCostLeg;
                case ItemObject.ItemTypeEnum.HeadArmor:    return s.ClassCostHead;
                case ItemObject.ItemTypeEnum.HandArmor:    return s.ClassCostHand;
                case ItemObject.ItemTypeEnum.Cape:         return s.ClassCostCape;
                case ItemObject.ItemTypeEnum.HorseHarness: return s.ClassCostHorse;
                case ItemObject.ItemTypeEnum.Shield:       return s.ClassCostShield;
                case ItemObject.ItemTypeEnum.Bow:
                case ItemObject.ItemTypeEnum.Crossbow:
                case ItemObject.ItemTypeEnum.Arrows:
                case ItemObject.ItemTypeEnum.Bolts:        return s.ClassCostRanged;
                default: return 1f;
            }
        }

        /// <summary>Drobna, precyzyjna robota - malo materialu, za to dluga.</summary>
        internal static bool IsFiddly(ItemObject.ItemTypeEnum t)
        {
            return t == ItemObject.ItemTypeEnum.HandArmor || t == ItemObject.ItemTypeEnum.HeadArmor;
        }

        private static void Add(Recipe r, ItemObject item, int count)
        {
            if (item == null || count <= 0) return;
            for (int i = 0; i < r.Parts.Count; i++)
                if (r.Parts[i].Item == item) { r.Parts[i] = new Part(item, r.Parts[i].Count + count); return; }
            r.Parts.Add(new Part(item, count));
        }

        private static CraftingMaterials IronForTier(int tier)
        {
            if (tier < 1) tier = 1;
            switch (tier)
            {
                case 1: return CraftingMaterials.Iron1;
                case 2: return CraftingMaterials.Iron2;
                case 3: return CraftingMaterials.Iron3;
                case 4: return CraftingMaterials.Iron4;
                case 5: return CraftingMaterials.Iron5;
                default: return CraftingMaterials.Iron6;
            }
        }

        // otwarcie polki strzeleckiej buduje tysiace receptur, a kazda pyta
        // model o kilka surowcow - trzymamy je pod reka zamiast pytac za kazdym razem
        private static readonly ItemObject[] _matCache = new ItemObject[16];
        private static readonly bool[] _matCached = new bool[16];

        internal static ItemObject MaterialItem(CraftingMaterials mat)
        {
            try
            {
                int i = (int)mat;
                if (i >= 0 && i < _matCache.Length && _matCached[i]) return _matCache[i];
                var it = Campaign.Current.Models.SmithingModel.GetCraftingMaterialItem(mat);
                if (i >= 0 && i < _matCache.Length) { _matCache[i] = it; _matCached[i] = true; }
                return it;
            }
            catch (Exception e) { Log.Error("MaterialItem", e); return null; }
        }

        /// <summary>
        /// ROT ma WLASNE odmiany lnu i skory pod innymi ID - gracz "ma len",
        /// a licznik po sztywnym przedmiocie widzial 0 (Jeff). Surowce miekkie
        /// (len/skora/aksamit) licza sie i schodza CALA KATEGORIA handlowa.
        /// </summary>
        private static bool SoftGood(ItemObject want)
        {
            return want != null && (want == _linen || want == _leather || want == _velvet);
        }

        private static bool CountsAs(ItemObject want, ItemObject have)
        {
            if (want == null || have == null) return false;
            if (have == want) return true;
            if (!SoftGood(want)) return false;
            try { return want.ItemCategory != null && want.ItemCategory == have.ItemCategory; }
            catch { return false; }
        }

        internal static int CountInInventory(ItemObject mat)
        {
            try
            {
                if (mat == null) return 0;
                var roster = MobileParty.MainParty.ItemRoster;
                if (!SoftGood(mat)) return roster.GetItemNumber(mat);
                int n = 0;
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster[i];
                    if (CountsAs(mat, el.EquipmentElement.Item)) n += el.Amount;
                }
                return n;
            }
            catch { return 0; }
        }

        internal static bool HasMaterials(Recipe r)
        {
            try
            {
                foreach (var p in r.Parts)
                    if (CountInInventory(p.Item) < p.Count) return false;
                return true;
            }
            catch (Exception e) { Log.Error("HasMaterials", e); return false; }
        }

        internal static string Describe(Recipe r)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var p in r.Parts)
            {
                int have = CountInInventory(p.Item);
                sb.Append(p.Count + "x " + p.Item.Name + " (" + have + ")");
                sb.Append("\n");
            }
            return sb.ToString().TrimEnd();
        }

        internal static bool TakeMaterials(Recipe r)
        {
            try
            {
                var roster = MobileParty.MainParty.ItemRoster;
                foreach (var p in r.Parts) Take(roster, p.Item, p.Count);
                return true;
            }
            catch (Exception e) { Log.Error("TakeMaterials", e); return false; }
        }

        /// <summary>Czego brakuje do wziecia share receptury - lista "2x Crude Iron".</summary>
        internal static List<string> MissingParts(Recipe r, float share)
        {
            var missing = new List<string>();
            try
            {
                var roster = MobileParty.MainParty.ItemRoster;
                foreach (var p in r.Parts)
                {
                    if (p.Item == null) continue;
                    int need = MathF.Max(1, (int)(p.Count * share));
                    int have = CountInInventory(p.Item);
                    if (have < need) missing.Add((need - have) + "x " + p.Item.Name);
                }
            }
            catch (Exception e) { Log.Error("MissingParts", e); }
            return missing;
        }

        internal static void TakePartial(Recipe r, float share)
        {
            try
            {
                var roster = MobileParty.MainParty.ItemRoster;
                foreach (var p in r.Parts) Take(roster, p.Item, MathF.Max(1, (int)(p.Count * share)));
            }
            catch (Exception e) { Log.Error("TakePartial", e); }
        }

        // internal: FletchForge zdejmuje tedy skore/len takze dla kucia pancerzy
        // BK-owa droga (SpendMaterials liczyl tylko sztywne ID i schodzil na minus)
        internal static void Take(ItemRoster roster, ItemObject mat, int need)
        {
            if (mat == null || need <= 0) return;
            if (!SoftGood(mat))
            {
                int have = roster.GetItemNumber(mat);
                int take = Math.Min(have, need);
                if (take > 0) roster.AddToCounts(mat, -take);
                return;
            }
            // miekki surowiec: zdejmujemy z KAZDEGO stosu tej kategorii
            while (need > 0)
            {
                int idx = -1;
                for (int i = 0; i < roster.Count; i++)
                {
                    var el = roster[i];
                    if (el.Amount > 0 && CountsAs(mat, el.EquipmentElement.Item)) { idx = i; break; }
                }
                if (idx < 0) return;
                var elx = roster[idx];
                int take = Math.Min(need, elx.Amount);
                roster.AddToCounts(elx.EquipmentElement, -take);
                need -= take;
            }
        }

        /// <summary>Co wraca z przetopu - tylko metal, i to nie cały.</summary>
        internal static List<Part> SmeltYield(Recipe r, float share)
        {
            var list = new List<Part>();
            try
            {
                foreach (var p in r.Parts)
                {
                    if (p.Item == null) continue;
                    if (p.Item != MaterialItem(CraftingMaterials.Iron1) &&
                        p.Item != MaterialItem(CraftingMaterials.Iron2) &&
                        p.Item != MaterialItem(CraftingMaterials.Iron3) &&
                        p.Item != MaterialItem(CraftingMaterials.Iron4) &&
                        p.Item != MaterialItem(CraftingMaterials.Iron5) &&
                        p.Item != MaterialItem(CraftingMaterials.Iron6)) continue;
                    int amount = MathF.Max(1, (int)(p.Count * share));
                    list.Add(new Part(p.Item, amount));
                }
            }
            catch (Exception e) { Log.Error("SmeltYield", e); }
            return list;
        }
    }
}
