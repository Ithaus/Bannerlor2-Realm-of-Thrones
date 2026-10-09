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
            public int SkillNeeded;  // PROG: od ilu Smithing wolno sie zabrac (SmithingSkillPerTier) - kucie, naprawa wlasnoreczna
            public int Difficulty;   // TRUDNOSC dla kosci (SmithingDifficultyPerTier): jakosc, pekniecie, XP, odczyt wzoru (pokretla Jeffa 07.10)
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
                return it != null && it.NotMerchandise && HistoricalPrices.Orig(it) >= MathF.Max(1000f, s.LegendaryValueFloor);   // od wartosci sprzed cen historycznych
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
                r.Difficulty = Math.Max(r.Difficulty, s.LegendarySkillNeeded);
            }
            catch (Exception e) { Log.Error("Legendize", e); }
            return r;
        }

        /// <summary>Waga sztuki metalu danego tieru z zywych danych gry
        /// (Jeff 01.09: "jeden iron wazy 0.5") - fallback 0.5, gdyby item
        /// nie mial wagi.</summary>
        private static float IngotKg(int tier)
        {
            try
            {
                var it = MaterialItem(IronForTier(tier));
                return it != null && it.Weight > 0.01f ? it.Weight : 0.5f;
            }
            catch { return 0.5f; }
        }

        /// <summary>sklad8-s S1: waga sztuki metalu danego gatunku (fallback 0.5, jak IngotKg).</summary>
        private static float MatKg(CraftingMaterials g)
        {
            try
            {
                var it = MaterialItem(g);
                return it != null && it.Weight > 0.01f ? it.Weight : 0.5f;
            }
            catch { return 0.5f; }
        }

        /// <summary>
        /// sklad8-s S1: linia startowa "Kuznia: amunicja (S1)" (tylko log, raz na sesje - po cenach historycznych): kwity serii amunicji
        /// z Recipes.For - ile sztuk z receptury wartosci, ile starym przepisem z wagi, ile kwitow ze stali Iron4-Iron6 (poza legendami ma byc 0),
        /// przyklady (default_arrows, hardened_125_gr_arrows) i 5 najdrozszych kwitow wedlug wartosci materialow.
        /// </summary>
        internal static void AmmoStartLine()
        {
            try
            {
                var mgr = MBObjectManager.Instance;
                if (mgr == null || Campaign.Current == null) return;
                var s = Settings.Current;
                var steel = new HashSet<ItemObject>();
                foreach (var g in new[] { CraftingMaterials.Iron4, CraftingMaterials.Iron5, CraftingMaterials.Iron6 }) { var m = MaterialItem(g); if (m != null) steel.Add(m); }
                int n = 0, legends = 0, oldRule = 0, steelN = 0;
                var all = new List<KeyValuePair<float, string>>();
                var samples = new List<string>(); var steelIds = new List<string>();
                foreach (var it in mgr.GetObjectTypeList<ItemObject>())
                {
                    if (it == null || (it.ItemType != ItemObject.ItemTypeEnum.Arrows && it.ItemType != ItemObject.ItemTypeEnum.Bolts)) continue;
                    if (IsLegendary(it)) { legends++; continue; }
                    n++;
                    var c = ArmsPricing.CostOf(it);
                    if (c == null || c.MetalKg <= 0f) oldRule++;
                    var r = For(it);
                    float cost = 0f; bool hasSteel = false;
                    var parts = new List<string>();
                    foreach (var p in r.Parts)
                    {
                        if (p.Item == null || p.Count <= 0) continue;
                        cost += p.Count * Math.Max(0, p.Item.Value);
                        if (steel.Contains(p.Item)) hasSteel = true;
                        parts.Add(p.Count + "x " + p.Item.StringId);
                    }
                    string desc = it.StringId + " t" + Grade(it) + ": " + string.Join(" + ", parts.ToArray()) + " = "
                                  + cost.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " d (wartosc kolczana " + it.Value + ")";
                    if (hasSteel) { steelN++; if (steelIds.Count < 10) steelIds.Add(it.StringId); }
                    if (it.StringId == "default_arrows" || it.StringId == "hardened_125_gr_arrows") samples.Add(desc);
                    all.Add(new KeyValuePair<float, string>(cost, desc));
                }
                all.Sort((a, b) => b.Key.CompareTo(a.Key));
                var top = new List<string>();
                for (int i = 0; i < all.Count && i < 5; i++) top.Add(all[i].Value);
                Log.Info("Kuznia: amunicja (S1) - kwit serii " + Math.Max(1, s.AmmoBatchStacks) + " kolczanow: zelazo na groty z receptury wartosci (metal grotu x seria / waga sztabki, gatunek grotu) + 1 drewno; "
                         + n + " strzal i beltow (legendy osobno: " + legends + "), starym przepisem z wagi (bez receptury) " + oldRule + ", ze stali Iron4-Iron6 " + steelN
                         + (steelIds.Count > 0 ? " [" + string.Join(", ", steelIds.ToArray()) + "]" : "") + " (ma byc 0); przyklady: " + (samples.Count > 0 ? string.Join("; ", samples.ToArray()) : "-")
                         + "; najdrozsze: " + (top.Count > 0 ? string.Join("; ", top.ToArray()) : "-") + ".");
            }
            catch (Exception e) { Log.Error("Recipes.AmmoStartLine", e); }
        }

        /// <summary>Waga sztuki drewna (Jeff: "drewno wazy akurat 10").</summary>
        private static float WoodKg()
        {
            try { return _wood != null && _wood.Weight > 0.01f ? _wood.Weight : 10f; }
            catch { return 10f; }
        }

        /// <summary>
        /// PROG I TRUDNOSC (Jeff 07.10: "kowalstwo - okay, moze byc zmiana, ale pamietaj, ze jak kujemy zbroje,
        /// to jest ta sama zasada jak przy broni - pancerz mozna zepsuc lub zrobic legendary"). Do 07.10 jedna liczba
        /// SkillNeeded byla i progiem (od ilu Smithing wolno kuc), i trudnoscia dla kosci (jakosc, pekniecie, XP) -
        /// obnizenie SmithingSkillPerTier 45 -> 35 przy TEJ SAMEJ umiejetnosci dawalo wiecej legend i mniej
        /// spartaczonych (tier 6 przy 250: legenda 3.3% -> 4.9%, pekniecie 20% -> 0%). Teraz prog idzie z
        /// SmithingSkillPerTier, a trudnosc ze stalej skali SmithingDifficultyPerTier (45) - ta sama ulga
        /// (cloth -20, skora/luki -10) w obu. Pokretlo skraca droge do wyzszych tierow, nie zmienia kosci.
        /// </summary>
        private static void Need(ref Recipe r, int tier, int less)
        {
            var s = Settings.Current;
            r.SkillNeeded = MathF.Max(0, (tier - 1) * s.SmithingSkillPerTier - less);
            r.Difficulty = MathF.Max(0, (tier - 1) * s.SmithingDifficultyPerTier - less);
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
                    // velvet wypadl (Jeff 01.09: "pancerze nie potrzebuja velvet, to nie suknia")
                    r.Stamina = MathF.Max(3, (int)(tier * s.StaminaPerTier * 0.4f));
                    Need(ref r, tier, 20);
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
                    Need(ref r, tier, 10);
                    return r;
                }

                // --- PRAWO WAGI KUZNI (Jeff 01.09): pancerz metalowy liczy sie
                // Z WAGI, nie z punktow ochrony. 80% wagi w metalu SWOJEGO
                // tieru + 20% wagi w metalu o tier nizej (T1: wszystko w T1),
                // sztuki wedle realnej wagi ingota (0.5 kg); do tego zawsze
                // 1 skora albo 1 len na podpinke. Bez velvet i bez wegla -
                // kwit plytnerza, nie huty. Przyklad Jeffa: 20 kg T5 =
                // 32x iron5 + 8x iron4 + 1 skora. ---
                if (item.HasArmorComponent)
                {
                    int hi = MathF.Max(1, MathF.Ceiling(weight * 0.8f / IngotKg(tier)));
                    Add(r, MaterialItem(IronForTier(tier)), hi);
                    if (tier >= 2)
                    {
                        int lo = MathF.Max(1, MathF.Ceiling(weight * 0.2f / IngotKg(tier - 1)));
                        Add(r, MaterialItem(IronForTier(tier - 1)), lo);
                    }
                    // podpinka: 1 skora TYLKO przy korpusie i ladrach konskich;
                    // rekawice, helmy, buty I PELERYNA bez miekkich dodatkow
                    // (Jeff 01.09: "nie dajemy lnu, skory, velvet" + "przy
                    // pelerynie tez nie") - sam metal
                    var tp = item.ItemType;
                    if (tp == ItemObject.ItemTypeEnum.BodyArmor
                        || tp == ItemObject.ItemTypeEnum.HorseHarness)
                        Add(r, _leather, 1);
                    float fidA = IsFiddly(item.ItemType) ? (1f + s.FiddlyStaminaBonus) : 1f;
                    r.Stamina = MathF.Max(5, (int)(tier * s.StaminaPerTier * fidA));
                    Need(ref r, tier, 0);
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
                    if (tt == ItemObject.ItemTypeEnum.Bow || tt == ItemObject.ItemTypeEnum.Crossbow)
                    {
                        // PRAWO WAGI (Jeff 01.09): 75% wagi w drewnie, 25% wagi
                        // w metalu SWOJEGO tieru ("t6 = iron6"), kazdy skladnik
                        // minimum 1 sztuka. Kusza liczy sie tak samo.
                        Add(r, _wood, MathF.Max(1, MathF.Ceiling(weight * 0.75f / WoodKg())));
                        Add(r, MaterialItem(IronForTier(tier)),
                            MathF.Max(1, MathF.Ceiling(weight * 0.25f / IngotKg(tier))));
                        Add(r, bowstring, 1);
                    }
                    else
                    {
                        int batch = Math.Max(1, s.AmmoBatchStacks);
                        // sklad8-s S1 (Jeff 09.10 10:35: "wykucie strzal to powinno byc grosze"): zelazo to GROTY, nie caly kolczan -
                        // metal z receptury wartosci (ArmsPricing.CostOf: grot 15% masy x strata kucia, gatunek grotu), ta sama, z ktorej
                        // licza strzelarze miasta (WorkshopLaw.Needs) i wartosc sztuki (HistCost) - jedna regula gracz/AI. Sztabek = metal
                        // serii / waga sztabki gatunku (W GORE, min 1); drewno 1 ladunek na serie jak dotad (drzewca i wegiel kuzni serii
                        // to 16-40 kg). Dotad cala masa kolczana szla w sztabki tieru strzaly: 3 kolczany t6 = 21 x Iron6 (2 100 d) za towar
                        // wart ok. 75 d. Bez receptury (CostOf null) - stary przepis z wagi ponizej; legendy - Legendize jak dotad.
                        var cost = ArmsPricing.CostOf(item);
                        if (cost != null && cost.MetalKg > 0f)
                        {
                            int heads = Math.Max(1, (int)Math.Ceiling(cost.MetalKg * batch / MatKg(cost.Grade) - 1e-4));
                            Add(r, MaterialItem(cost.Grade), heads);
                            Add(r, _wood, 1);
                        }
                        else
                        {
                            // PRAWO WAGI dla amunicji (Jeff 01.09, rachunek na
                            // Ravens' Teeth: 40 g x 30 strzal = 1.2 kg -> 2 zelaza,
                            // seria x3 -> 6): masa kolczana = waga strzaly x stack,
                            // zelazo tieru strzaly = masa / waga ingota (W DOL,
                            // min 1) x kolczany w serii; drewno 1 na cala serie
                            // (sztuka drewna wazy 10 kg - starcza z zapasem).
                            float perShaft = MathF.Max(0.01f, item.Weight);
                            int stack = item.PrimaryWeapon != null
                                ? Math.Max(1, (int)item.PrimaryWeapon.MaxDataValue) : 30;
                            float quiverKg = perShaft * stack;
                            int heads = Math.Max(1, (int)MathF.Floor(quiverKg / IngotKg(tier))) * batch;
                            Add(r, MaterialItem(IronForTier(tier)), heads);
                            Add(r, _wood, 1);
                        }
                    }
                    bool ammo = tt == ItemObject.ItemTypeEnum.Arrows || tt == ItemObject.ItemTypeEnum.Bolts;
                    // szczyt rzemiosla kosztuje: luki i kusze tieru 5-6 zra
                    // wielokrotnosc materialow (Jeff 26.08: "za latwo sie tworzy,
                    // daj x2 zasobow"). Amunicji nie dotyczy.
                    if (!ammo && tier >= 5)
                    {
                        float f = MathF.Max(1f, s.RangedHighTierCostFactor);
                        for (int i = 0; i < r.Parts.Count; i++)
                        {
                            var p = r.Parts[i];
                            p.Count = MathF.Max(1, MathF.Ceiling(p.Count * f));
                            r.Parts[i] = p;
                        }
                    }
                    // wiazka strzal to nie kirys: 10x mniej staminy niz stara stawka (Jeff);
                    // luki i kusze: stary mnoznik 0.8 pozwalal wykuc 2 luki na sesje -
                    // luczarnia ma byc lekka jak kuznia broni (Jeff), stad wlasny mnoznik
                    r.Ranged = true;
                    r.Stamina = ammo
                        ? MathF.Max(2, (int)(tier * s.StaminaPerTier * 0.05f))
                        : MathF.Max(4, (int)(tier * s.StaminaPerTier * MathF.Max(0.05f, s.RangedStaminaFactor)));
                    Need(ref r, tier, 10);
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
                    Add(r, _wood, MathF.Max(1, MathF.Ceiling(weight / WoodKg())));   // wpis 50: sztuka drewna to ladunek, nie kilogram

                float fiddly = IsFiddly(item.ItemType) ? (1f + s.FiddlyStaminaBonus) : 1f;
                r.Stamina = MathF.Max(5, (int)(tier * s.StaminaPerTier * fiddly));
                Need(ref r, tier, 0);   // tier 1 od zera - zaczynasz od podkowek, nie od plach
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
                // Iron1 (crude) to SUROWKA - z niej dopiero rafinuje sie metal,
                // do wyrobu nie wchodzi (Jeff 01.09: "wrought iron to tier 1");
                // wyroby T1 i T2 dziela wrought iron
                case 1: return CraftingMaterials.Iron2;
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

        /// <summary>Co wraca z przetopu - tylko metal, i to nie caly.
        /// Jeff 30.08: przetop zwraca NAJWYZEJ POLOWE receptury (zadnych 90%
        /// przy wysokim skillu). 177-1: Iron6 to stal zamkowa - wraca jak kazda
        /// inna stal (dawne dodatkowe "/2" dla "stali valyrianskiej" zdjete).</summary>
        internal static List<Part> SmeltYield(Recipe r, float share)
        {
            var list = new List<Part>();
            try
            {
                share = MathF.Min(share, 0.5f);
                var castle = MaterialItem(CraftingMaterials.Iron6);
                foreach (var p in r.Parts)
                {
                    if (p.Item == null) continue;
                    if (p.Item != MaterialItem(CraftingMaterials.Iron1) &&
                        p.Item != MaterialItem(CraftingMaterials.Iron2) &&
                        p.Item != MaterialItem(CraftingMaterials.Iron3) &&
                        p.Item != MaterialItem(CraftingMaterials.Iron4) &&
                        p.Item != MaterialItem(CraftingMaterials.Iron5) &&
                        p.Item != castle) continue;
                    int amount = MathF.Max(1, (int)(p.Count * share));
                    list.Add(new Part(p.Item, amount));
                }
            }
            catch (Exception e) { Log.Error("SmeltYield", e); }
            return list;
        }
    }
}
