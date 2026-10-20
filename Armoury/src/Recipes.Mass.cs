using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// PRZEPISY Z MASY (m96-receptury, klucz RecipesByMass; Jeff 20.10: "strzaly wymagaja 14 najlepszej stali albo luki 8 lnu - bez sensu" i "len jest drogi,
    /// zastosujmy ogolna zasade wagi surowca, tak jak w drewnie ... popatrz, ile 1 jednostka surowca wazy i na ile starcza w danym przedmiocie").
    /// DOWOD (stary przepis, Recipes.BuildRecipe + Legendize): luk = 75% wagi w drewnie + 25% wagi w sztabach swojego tieru (min. 1) + 1 len (10 kg, 100 d) na
    /// cieciwe, x2 przy t5-6 (RangedHighTierCostFactor), legenda x4 (LegendaryMaterialFactor) + Iron6 x tier: dragonbone_bow / goldenheart_longbow (0.1 kg) =
    /// 14 x Iron6 + 8 x len; zwykly luk 0.3 kg = 1 sztaba + 1 len (10.9 kg surowca na 0.3 kg luku); pancerz skorzany/tkany z punktow ochrony w CALYCH sztukach
    /// skory i lnu (po 10 kg). Jednostki w grze (items-dump): sztaba kazdego gatunku 0.5 kg, len i skora 10 kg, drewno - ladunek 100 kg, wegiel 5 kg.
    /// REGULA: przepis = kilogramy surowca, ktore naprawde wchodza w przedmiot, plus odpad kucia - te same kg, z ktorych gra liczy wartosc sztuki i ktore zuzywa
    /// warsztat AI (ArmsPricing.CostOf, regula R7 "jedna wycena"): metal x1.4 (zgorzelina, obcinki), groty x1.2, drewno x1.5 (klepka, drzewce), skora x1.2,
    /// len x1.1. Do tego budowa: luk bez metalu (klepka; luk refleksyjny - rdzen z drewna i rog/sciegno jako skora, 40% masy), cieciwa = gramy lnu (luk 20 g,
    /// kusza 50 g, x1.5 odpad), kusza = lozysko z drewna, mechanizm z zelaza (10% masy), lek stalowy (20% masy, gatunek wedlug sily kuszy) albo drewniany
    /// u slabszych kusz; strzaly i belty - groty i drzewca serii (AmmoBatchStacks kolczanow), lotki pominiete; pancerz metalowy 80% metalu swojego tieru +
    /// 20% tier nizej (regula Jeffa 01.09) + len podszycia, skorzany skora + okucia, tkany len. Tier zmienia GATUNEK metalu, nie ilosc. Legenda: caly metal ze
    /// stali zamkowej (Iron6) i x2 metalu (mistrz odrzuca kazdy gorszy kawalek), stamina x3 i prog legendy jak dotad; luki t5-6 - x RangedHighTierCostFactor
    /// drewna (dobra klepka: z klody wychodzi mniej sztuk), nie metalu.
    /// OTWARTE LADUNKI (jak drewno SmithWood): niepelna jednostka zostaje otwarta i starcza na kolejne sztuki; nowa sztuka z sakw dopiero, gdy otwarta sie
    /// skonczy. Metal - skrzynka resztek kuzni (SmeltScraps, tysiaczne sztaby, zapis arm_smeltscraps): napoczeta sztaba i resztki przetopu to ten sam metal;
    /// len i skora - SmithStock (kg, zapis arm_smith_stock); drewno - SmithWood (kg, bez zmian). Ekran CRAFT i nasze menu pokazuja sztuki do otwarcia (0, gdy
    /// otwarty ladunek starcza) - tak jak drewno.
    /// PRZETOP: masa metalu w SZTUCE (bez odpadu kucia) minus straty przetopu (zgar i zuzel): plyta 25%, kolczuga 35% (tysiace kolek, nity), okucia skory
    /// i tkaniny 40%, metal luku/kuszy 30%, groty 40%; stal (Iron4+) +5% (odweglenie - liczymy jako strata, gatunek bez zmian, jak przetop broni z czesci).
    /// Nigdy wiecej niz udzial przetopu (SmeltingReturnShare + Smithing x bonus, najwyzej polowa - Jeff 30.08) z kg PRZEPISU tej sztuki: kucie + przetop
    /// zawsze traci okolo polowy metalu, przetopem nie da sie zarabiac. Drewno, skora, len i rog przepadaja.
    /// Wylaczony klucz = przepisy jak dotad (Recipes.BuildRecipe). Tarcze - bez zmian (stary przepis; poza zakresem).
    /// </summary>
    internal static partial class Recipes
    {
        internal struct KgPart
        {
            public ItemObject Item;
            public float Kg;        // surowiec przepisu (z odpadem kucia)
            public float PieceKg;   // ile z tego zostaje w gotowej sztuce (podstawa przetopu)
            public float Loss;      // strata przetopu (metal)
            public int Units;       // sztuk z sakw do otwarcia teraz (0 = otwarty ladunek starcza)
            public int Grade;       // 1..6 metal (CraftingMaterials Iron1..Iron6), 0 len/skora, -1 drewno
            public string Role;     // do logu: plates, rivets, heads, shafts, stave, string, prod, lock, horn
        }

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        [ThreadStatic] private static bool _massOff;   // linia startu: stary przepis do porownania
        private static int _massStumbles, _massUses, _massOpened, _massLines;

        internal static bool MassOn { get { var s = Settings.Current; return s != null && s.RecipesByMass; } }

        private static void MassStumble(string where, Exception e)
        {
            _massStumbles++;
            if (_massStumbles <= 5 || _massStumbles % 100 == 0) Log.Error("Recipes.Mass." + where + " (potkniecie " + _massStumbles + ")", e);
        }

        /// <summary>Ktore przepisy ida z masy: luki, kusze, strzaly, belty i pancerze (z peleryna i rzedem konskim). Tarcze - stary przepis.</summary>
        internal static bool MassCovers(ItemObject it)
        {
            if (it == null) return false;
            var t = it.ItemType;
            if (t == ItemObject.ItemTypeEnum.Bow || t == ItemObject.ItemTypeEnum.Crossbow || t == ItemObject.ItemTypeEnum.Arrows || t == ItemObject.ItemTypeEnum.Bolts) return true;
            if (t == ItemObject.ItemTypeEnum.Shield) return false;
            return it.HasArmorComponent;
        }

        // ------------------------------------------------------------ budowa przepisu
        private static bool MassFor(ItemObject item, out Recipe r)
        {
            r = default(Recipe);
            if (_massOff || !MassOn || !MassCovers(item)) return false;
            try
            {
                var kg = MassKg(item);
                if (kg == null || kg.Count == 0) return false;   // bez receptury wartosci (CostOf) - stary przepis
                r = BuildRecipe(item);                           // tier, stamina, prog i trudnosc jak dotad; czesci zastapione nizej
                _woodKgBuild = 0f;
                bool legend = false;
                try { legend = IsLegendary(item); } catch { }
                if (legend) r = LegendMarks(r);
                r.Parts = new List<Part>();
                r.WoodKg = 0f;
                for (int i = 0; i < kg.Count; i++)
                {
                    var k = kg[i];
                    if (k.Grade < 0)
                    {
                        if (SmithWood.On) r.WoodKg += k.Kg;
                        else Add(r, _wood, Math.Max(1, (int)Math.Ceiling(k.Kg / WoodKg() - 1e-4)));   // bez SmithWoodKg - cale ladunki, jak dotad
                        continue;
                    }
                    k.Units = UnitsNow(k, k.Kg);
                    kg[i] = k;
                    Add(r, k.Item, k.Units);
                }
                if (r.WoodKg > 0f) Add(r, _wood, SmithWood.LoadsFor(r.WoodKg));
                r.Kg = kg;
                r.MassItem = item;
                return true;
            }
            catch (Exception e) { MassStumble("MassFor", e); r = default(Recipe); return false; }
        }

        /// <summary>Legenda: stamina x3 i prog legendy jak w Legendize - bez mnozenia ilosci (metal legendy liczy MassKg).</summary>
        private static Recipe LegendMarks(Recipe r)
        {
            try
            {
                var s = Settings.Current;
                r.Stamina = Math.Min(100, r.Stamina * 3);
                r.SkillNeeded = Math.Max(r.SkillNeeded, s.LegendarySkillNeeded);
                r.Difficulty = Math.Max(r.Difficulty, s.LegendarySkillNeeded);
            }
            catch (Exception e) { MassStumble("LegendMarks", e); }
            return r;
        }

        private static readonly System.Text.RegularExpressions.Regex Composite = new System.Text.RegularExpressions.Regex(
            "composite|recurve|reflex|horn|steppe|nomad|khuzait|aserai|desert|dothraki|lhazar|sarnor", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        /// <summary>Luk refleksyjny (rog i sciegno na drewnianym rdzeniu): id albo nazwa (RBM: "Composite Bow").</summary>
        private static bool IsComposite(ItemObject it)
        {
            try { return Composite.IsMatch(it.StringId ?? "") || (it.Name != null && Composite.IsMatch(it.Name.ToString())); }
            catch { return false; }
        }

        private static void AddKg(List<KgPart> l, ItemObject mat, int grade, float kg, float piece, float loss, string role)
        {
            if (mat == null || kg <= 0.00001f) return;
            for (int i = 0; i < l.Count; i++)
                if (l[i].Item == mat)
                {
                    var p = l[i]; p.Kg += kg; p.PieceKg += piece; p.Loss = Math.Max(p.Loss, loss); p.Role = p.Role + "+" + role; l[i] = p; return;
                }
            l.Add(new KgPart { Item = mat, Kg = kg, PieceKg = piece, Loss = loss, Grade = grade, Role = role });
        }

        private static void AddMetal(List<KgPart> l, CraftingMaterials g, float raw, float piece, float loss, string role, bool legend)
        {
            if (raw <= 0f) return;
            if (legend) { g = CraftingMaterials.Iron6; raw *= LegendMetalFactor; }
            if (g >= CraftingMaterials.Iron4) loss += SteelExtraLoss;
            AddKg(l, MaterialItem(g), (int)g, raw, piece, Math.Min(0.9f, loss), role);
        }

        // stale reguly (Jeff: "nie pytac o parametry" - opisane w CHANGELOG)
        private const float StringKgBow = 0.02f, StringKgXbow = 0.05f, StringWaste = 1.5f;   // cieciwa: gramy lnu (konopi), odpad x1.5
        private const float LegendMetalFactor = 2f, SteelExtraLoss = 0.05f;
        private const float LossPlate = 0.25f, LossMail = 0.35f, LossFittings = 0.40f, LossRanged = 0.30f, LossHeads = 0.40f;
        private const float MetalWaste = 1.4f, WoodWaste = 1.5f, HornWaste = 1.25f;

        /// <summary>Kilogramy surowcow przedmiotu (bez sztuk do otwarcia). null = brak receptury wartosci (CostOf) - stary przepis.</summary>
        private static List<KgPart> MassKg(ItemObject it)
        {
            ResolveGoods();
            var c = ArmsPricing.CostOf(it);
            if (c == null) return null;
            var s = Settings.Current;
            int tier = Grade(it);
            bool legend = false;
            try { legend = IsLegendary(it); } catch { }
            float w = Math.Max(0.01f, ArmsPricing.CostWeight(it));
            var l = new List<KgPart>();
            var tp = it.ItemType;
            if (tp == ItemObject.ItemTypeEnum.Bow)
            {
                float f = tier >= 5 ? Math.Max(1f, s.RangedHighTierCostFactor) : 1f;
                bool comp = IsComposite(it);
                float wood = (c.WoodKg > 0f ? c.WoodKg : w * 2f) * f;
                if (comp)
                {
                    AddKg(l, _wood, -1, wood * 0.6f, w * 0.6f, 0f, "core");
                    AddKg(l, _leather, 0, w * 0.4f * HornWaste, w * 0.4f, 0f, "horn");
                }
                else AddKg(l, _wood, -1, wood, w, 0f, "stave");
                AddKg(l, StringMat(StringKgBow * StringWaste), 0, StringKgBow * StringWaste, StringKgBow, 0f, "string");
            }
            else if (tp == ItemObject.ItemTypeEnum.Crossbow)
            {
                int ft = Math.Max(tier, XbowPrice.ForgeTier(it));
                AddKg(l, _wood, -1, w * 0.6f * WoodWaste, w * 0.6f, 0f, "stock");
                AddMetal(l, CraftingMaterials.Iron2, w * 0.1f * MetalWaste, w * 0.1f, LossRanged, "lock", legend);
                if (ft >= 4) AddMetal(l, IronForTier(ft), w * 0.2f * MetalWaste, w * 0.2f, LossRanged, "prod", legend);
                else AddKg(l, _wood, -1, w * 0.2f * WoodWaste, w * 0.2f, 0f, "prod");
                AddKg(l, StringMat(StringKgXbow * StringWaste), 0, StringKgXbow * StringWaste, StringKgXbow, 0f, "string");
            }
            else if (tp == ItemObject.ItemTypeEnum.Arrows || tp == ItemObject.ItemTypeEnum.Bolts)
            {
                int batch = Math.Max(1, s.AmmoBatchStacks);
                // groty: kg z receptury wartosci (15% masy strzaly x strata 1.2, gatunek grotu S2), drzewca: 85% x 1.5 - cala seria
                float heads = c.MetalKg * batch;
                AddMetal(l, c.Grade, heads, heads / 1.2f, LossHeads, "heads", legend);
                AddKg(l, _wood, -1, c.WoodKg * batch, c.WoodKg * batch / WoodWaste, 0f, "shafts");
            }
            else if (it.HasArmorComponent)
            {
                var mat = it.ArmorComponent != null ? it.ArmorComponent.MaterialType : ArmorComponent.ArmorMaterialTypes.None;
                bool mail = mat == ArmorComponent.ArmorMaterialTypes.Chainmail;
                bool plate = mat == ArmorComponent.ArmorMaterialTypes.Plate;
                // wspolczynniki jak ArmsPricing (plyta/kolczuga: metal 90% x1.4 + len 10%; skora: skora 85% x1.2 + okucia 15% x1.4; tkanina: len x1.1),
                // ale WEDLUG MATERIALU takze dla pelerynek - CostOf liczy kazda peleryne jak tkanine (193 naramienniki plytowe ROT "z lnu")
                bool leatherA = mat == ArmorComponent.ArmorMaterialTypes.Leather;
                float metal = plate || mail ? w * 0.9f * MetalWaste : leatherA ? w * 0.15f * MetalWaste : 0f, piece = metal / MetalWaste;
                float hide = leatherA ? w * 0.85f * 1.2f : 0f;
                float cloth = plate || mail ? w * 0.1f : leatherA ? 0f : w * 1.1f;
                if (tp == ItemObject.ItemTypeEnum.Cape && !plate && !mail && !leatherA && tier >= 5) hide = w * 0.5f;   // futro na lepszych plaszczach (jak CostOf)
                if (plate || mail)
                {
                    float loss = mail ? LossMail : LossPlate;
                    string role = mail ? "rings" : "plates";
                    if (tier >= 2 && !legend)
                    {
                        AddMetal(l, IronForTier(tier), metal * 0.8f, piece * 0.8f, loss, role, false);
                        AddMetal(l, IronForTier(tier - 1), metal * 0.2f, piece * 0.2f, loss, role, false);
                    }
                    else AddMetal(l, IronForTier(tier), metal, piece, loss, role, legend);
                }
                else AddMetal(l, IronForTier(tier), metal, piece, LossFittings, "rivets", legend);
                AddKg(l, _leather, 0, hide, hide / 1.2f, 0f, "leather");
                AddKg(l, _linen, 0, cloth, cloth / 1.1f, 0f, "cloth");
            }
            return l;
        }

        /// <summary>Cieciwa: len (konopie) - a gdy lnu nie ma ani w sakwach, ani w otwartym ladunku, starczy rzemien ze skory (jak dotad).</summary>
        private static ItemObject StringMat(float kg)
        {
            if (_linen == null) return _leather;
            if (SmithStock.OpenKg(_linen) >= kg || CountInInventory(_linen) > 0 || _leather == null) return _linen;
            if (SmithStock.OpenKg(_leather) >= kg || CountInInventory(_leather) > 0) return _leather;
            return _linen;
        }

        private static float UnitKg(KgPart k)
        {
            if (k.Grade >= 1) return MatKg((CraftingMaterials)k.Grade);
            return SmithStock.UnitKg(k.Item);
        }

        private static int Milli(KgPart k, float kg) { return (int)Math.Ceiling(Math.Max(0f, kg) / Math.Max(0.01f, UnitKg(k)) * SmeltScraps.Unit - 1e-3); }

        /// <summary>Sztuki z sakw do otwarcia na kg (0, gdy otwarty ladunek starcza).</summary>
        private static int UnitsNow(KgPart k, float kg)
        {
            if (kg <= 0f) return 0;
            if (k.Grade >= 1) return SmeltScraps.BarsFor(k.Grade, Milli(k, kg));
            return SmithStock.UnitsFor(k.Item, kg);
        }

        private static void Commit(KgPart k, float kg, int taken)
        {
            if (k.Grade >= 1) SmeltScraps.ForgeUse(k.Grade, Milli(k, kg), taken);
            else if (k.Grade == 0) SmithStock.Use(k.Item, kg, taken);
        }

        // ------------------------------------------------------------ pobranie (nasza kuznia), czesc (pekniecie, naprawa), CRAFT BK
        /// <summary>Kucie w naszej kuzni (Recipes.TakeMaterials): sztuki z sakw (liczone naprawde zdjete), kg z otwartych ladunkow, drewno jak SmithWood.</summary>
        private static bool MassTake(Recipe r, string what)
        {
            try
            {
                var roster = MobileParty.MainParty.ItemRoster;
                var opened = new List<string>(); var used = new List<string>();
                foreach (var p in r.Parts) if (p.Item != null && p.Item == _wood) Take(roster, p.Item, p.Count);   // drewno: ladunki do otwarcia (SmithWood)
                if (r.WoodKg > 0f) SmithWood.Use(r.WoodKg, WoodLoads(r), what);
                for (int i = 0; i < r.Kg.Count; i++)
                {
                    var k = r.Kg[i];
                    if (k.Grade < 0 || k.Item == null) continue;
                    int taken = 0;
                    if (k.Units > 0)
                    {
                        int before = CountInInventory(k.Item);
                        Take(roster, k.Item, k.Units);
                        taken = Math.Max(0, before - CountInInventory(k.Item));
                        if (taken < k.Units) _massStumbles++;   // brak w sakwach mimo HasMaterials - licz potkniecie (otwarty ladunek nie dostaje metalu z niczego: Commit przycina do 0)
                    }
                    Commit(k, k.Kg, taken);
                    used.Add(Kg(k.Kg) + " kg " + k.Item.StringId);
                    if (taken > 0) opened.Add(taken + " " + k.Item.Name);
                }
                Note(r, what, used, opened, 1f);
                return true;
            }
            catch (Exception e) { MassStumble("MassTake", e); return false; }
        }

        /// <summary>Kucie pancerza w CRAFT BK: sztuki z sakw zdjal juz kwit BK (te same Units - GetCraftingInputForArmor = Recipes.For) - tu tylko kg otwartych ladunkow.</summary>
        internal static void MassCommitBk(ItemObject item, string what)
        {
            try
            {
                if (!MassOn || item == null) return;
                var r = For(item);
                if (r.Kg == null) return;
                var used = new List<string>(); var opened = new List<string>();
                foreach (var k in r.Kg)
                {
                    if (k.Grade < 0 || k.Item == null) continue;
                    Commit(k, k.Kg, k.Units);
                    used.Add(Kg(k.Kg) + " kg " + k.Item.StringId);
                    if (k.Units > 0) opened.Add(k.Units + " " + k.Item.Name);
                }
                Note(r, what ?? item.StringId, used, opened, 1f);
            }
            catch (Exception e) { MassStumble("MassCommitBk", e); }
        }

        /// <summary>Czy tablica kwitu BK (9 surowcow kuzni, skora, len) to dokladnie sztuki do otwarcia tego przepisu z masy (FletchForge.BkMassSpendPostfix).</summary>
        internal static bool BillMatches(Recipe r, int[] bill)
        {
            if (r.Kg == null || r.Parts == null || bill == null || bill.Length < 11) return false;
            var mine = new int[11];
            foreach (var p in r.Parts)
            {
                if (p.Item == null || p.Count <= 0) continue;
                bool hit = false;
                for (int m = 0; m < 9; m++) if (MaterialItem((CraftingMaterials)m) == p.Item) { mine[m] += p.Count; hit = true; break; }
                if (hit) continue;
                if (p.Item == SoftLeather) mine[9] += p.Count;
                else if (p.Item == SoftLinen) mine[10] += p.Count;
                else return false;
            }
            for (int i = 0; i < 11; i++) if (mine[i] != Math.Max(0, bill[i])) return false;
            return true;
        }

        /// <summary>Czesc przepisu (pekniecie MaterialLossOnFailure, naprawa SelfRepairMaterialFactor x brak) - kg x udzial z otwartych ladunkow.</summary>
        private static void MassTakePartial(Recipe r, float share)
        {
            try
            {
                share = Math.Max(0f, share);
                var roster = MobileParty.MainParty.ItemRoster;
                var used = new List<string>(); var opened = new List<string>();
                if (r.WoodKg > 0f)
                {
                    float wkg = r.WoodKg * share;
                    int loads = SmithWood.LoadsFor(wkg);
                    if (loads > 0) Take(roster, _wood, loads);
                    SmithWood.Use(wkg, loads, null, true);
                }
                else { int wl = WoodLoads(r); if (wl > 0) Take(roster, _wood, Math.Max(1, (int)(wl * share))); }
                foreach (var k in r.Kg)
                {
                    if (k.Grade < 0 || k.Item == null) continue;
                    float kg = k.Kg * share;
                    int need = UnitsNow(k, kg), taken = 0;
                    if (need > 0)
                    {
                        int before = CountInInventory(k.Item);
                        Take(roster, k.Item, need);
                        taken = Math.Max(0, before - CountInInventory(k.Item));
                    }
                    Commit(k, kg, taken);
                    used.Add(Kg(kg) + " kg " + k.Item.StringId);
                    if (taken > 0) opened.Add(taken + " " + k.Item.Name);
                }
                Note(r, (r.MassItem != null ? r.MassItem.StringId : "-") + " czesc " + Kg(share), used, opened, share);
            }
            catch (Exception e) { MassStumble("MassTakePartial", e); }
        }

        private static List<string> MassMissing(Recipe r, float share)
        {
            var missing = new List<string>();
            try
            {
                share = Math.Max(0f, share);
                if (r.WoodKg > 0f)
                {
                    int loads = SmithWood.LoadsFor(r.WoodKg * share);
                    int have = _wood != null ? CountInInventory(_wood) : 0;
                    if (loads > have && _wood != null) missing.Add((loads - have) + "x " + _wood.Name);
                }
                foreach (var k in r.Kg)
                {
                    if (k.Grade < 0 || k.Item == null) continue;
                    int need = UnitsNow(k, k.Kg * share);
                    if (need <= 0) continue;
                    int have = CountInInventory(k.Item);
                    if (have < need) missing.Add((need - have) + "x " + k.Item.Name);
                }
            }
            catch (Exception e) { MassStumble("MassMissing", e); }
            return missing;
        }

        private static void Note(Recipe r, string what, List<string> used, List<string> opened, float share)
        {
            _massUses++;
            int op = 0; foreach (var o in opened) { int n; int sp = o.IndexOf(' '); if (sp > 0 && int.TryParse(o.Substring(0, sp), out n)) op += n; }
            _massOpened += op;
            if (used.Count == 0) return;
            if (_massLines < 40 || _massUses % 25 == 0)
            {
                _massLines++;
                Log.Info("Surowce w kuzni (kg) (m96-receptury): " + (what ?? "-") + " | zuzyte: " + string.Join(", ", used.ToArray()) + " | z sakw: "
                         + (opened.Count > 0 ? string.Join(", ", opened.ToArray()) : "nic (otwarte ladunki starczyly)") + " | otwarte: " + OpenText(false)
                         + " | od startu sesji: kuc " + _massUses + ", sztuk z sakw " + _massOpened + ", potkniecia " + _massStumbles + ".");
            }
            Log.Player("Materials at the anvil: " + string.Join(", ", PlayerUsed(r, share).ToArray()) + (opened.Count > 0 ? " - opened from your bags: " + string.Join(", ", opened.ToArray()) : " - all from what was already open")
                       + ". Left open: " + OpenText(true) + ".");
        }

        private static List<string> PlayerUsed(Recipe r, float share)
        {
            var l = new List<string>();
            if (r.Kg == null) return l;
            foreach (var k in r.Kg)
            {
                if (k.Item == null || k.Grade < 0) continue;
                l.Add(Kg(k.Kg * share) + " kg " + k.Item.Name);
            }
            return l;
        }

        private static string Kg(double v) { return v < 0.1 ? v.ToString("0.###", Inv) : v.ToString("0.##", Inv); }

        /// <summary>Otwarte ladunki: metal (skrzynka, kg), len i skora (kg).</summary>
        internal static string OpenText(bool player)
        {
            var sb = new StringBuilder();
            for (int g = 1; g <= 6; g++)
            {
                int m = SmeltScraps.BoxMilli(g);
                if (m <= 0) continue;
                var it = MaterialItem((CraftingMaterials)g);
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(player && it != null && it.Name != null ? it.Name.ToString() : (it != null ? it.StringId : "metal" + g)).Append(' ').Append(Kg(m / (double)SmeltScraps.Unit * MatKg((CraftingMaterials)g))).Append(" kg");
            }
            string soft = SmithStock.Text(player);
            if (soft.Length > 0) { if (sb.Length > 0) sb.Append(", "); sb.Append(soft); }
            return sb.Length > 0 ? sb.ToString() : (player ? "nothing" : "nic");
        }

        private static string MassDescribe(Recipe r)
        {
            var sb = new StringBuilder("\nBy weight: ");
            bool first = true;
            foreach (var k in r.Kg)
            {
                if (k.Item == null) continue;
                if (!first) sb.Append(", ");
                first = false;
                sb.Append(Kg(k.Kg)).Append(" kg ").Append(k.Item.Name);
            }
            sb.Append(" (a new piece is opened only when the open one runs short; open: ").Append(OpenText(true)).Append(")");
            return sb.ToString();
        }

        // ------------------------------------------------------------ przetop
        /// <summary>Dokladny odzysk metalu w sztabach (indeksy 1..6): masa metalu w sztuce x (1 - strata), nie wiecej niz min(udzial, 0.5) x kg przepisu.</summary>
        internal static double[] MassSmeltBars(Recipe r, float share)
        {
            var bars = new double[7];
            if (r.Kg == null) return bars;
            double cap = Math.Max(0.0, Math.Min(0.5, Math.Round((double)share, 6)));
            foreach (var k in r.Kg)
            {
                if (k.Grade < 1 || k.Grade > 6) continue;
                double y = Math.Min(k.PieceKg * (1.0 - k.Loss), cap * k.Kg);
                if (y <= 0) continue;
                bars[k.Grade] += y / Math.Max(0.01, MatKg((CraftingMaterials)k.Grade));
            }
            return bars;
        }

        /// <summary>Nasza kuznia i menu kowala (Forge.Smelt, SmithMenu): cale sztaby z dokladnego odzysku (w dol, bez minimum 1).</summary>
        private static List<Part> MassSmeltYield(Recipe r, float share)
        {
            var list = new List<Part>();
            try
            {
                var bars = MassSmeltBars(r, share);
                for (int g = 1; g <= 6; g++)
                {
                    int n = (int)Math.Floor(bars[g] + 1e-6);
                    if (n > 0) list.Add(new Part(MaterialItem((CraftingMaterials)g), n));
                }
            }
            catch (Exception e) { MassStumble("MassSmeltYield", e); }
            return list;
        }

        /// <summary>Zakladka Smelt (SmeltScraps.ArmourMilli): tysiaczne sztaby; null = przepis nie z masy.</summary>
        internal static int[] MassSmeltMilli(ItemObject it, float share)
        {
            if (!MassOn || !MassCovers(it)) return null;
            var r = For(it);
            if (r.Kg == null) return null;
            var bars = MassSmeltBars(r, share);
            var m = new int[7];
            for (int g = 1; g <= 6; g++) m[g] = (int)Math.Floor(bars[g] * SmeltScraps.Unit + 1e-6);
            return m;
        }

        /// <summary>Dokladne (ulamkowe) sztuki przepisu - naprawa wlasnymi rekami (MendMaterial.SelfExact): przy przepisie z masy kg / waga sztuki, inaczej sztuki.</summary>
        internal static List<KeyValuePair<ItemObject, float>> ExactUnits(Recipe r)
        {
            var l = new List<KeyValuePair<ItemObject, float>>();
            if (r.Parts == null) return l;
            if (r.Kg == null)
            {
                foreach (var p in r.Parts) l.Add(new KeyValuePair<ItemObject, float>(p.Item, p.Count));
                return l;
            }
            foreach (var p in r.Parts) if (p.Item != null && p.Item == _wood) l.Add(new KeyValuePair<ItemObject, float>(p.Item, p.Count));   // drewno: ladunki jak dotad (N12 liczy kg osobno)
            foreach (var k in r.Kg)
            {
                if (k.Item == null || k.Grade < 0) continue;
                l.Add(new KeyValuePair<ItemObject, float>(k.Item, k.Kg / Math.Max(0.01f, UnitKg(k))));
            }
            return l;
        }

        // ------------------------------------------------------------ linia startu sesji
        private static string Old(ItemObject it, out float kg)
        {
            kg = 0f;
            bool keep = _massOff;
            _massOff = true;
            try
            {
                var r = For(it);
                var sb = new StringBuilder();
                if (r.Parts != null)
                    foreach (var p in r.Parts)
                    {
                        if (p.Item == null || p.Count <= 0 || p.Item == _wood) continue;
                        if (sb.Length > 0) sb.Append(" + ");
                        sb.Append(p.Count).Append("x ").Append(p.Item.StringId);
                        kg += p.Count * Math.Max(0.01f, p.Item.Weight);
                    }
                if (r.WoodKg > 0f) { if (sb.Length > 0) sb.Append(" + "); sb.Append(Kg(r.WoodKg)).Append(" kg wood"); kg += r.WoodKg; }
                else foreach (var p in r.Parts) if (p.Item == _wood && p.Count > 0) { if (sb.Length > 0) sb.Append(" + "); sb.Append(p.Count).Append("x hardwood"); kg += p.Count * WoodKg(); }
                return sb.Length > 0 ? sb.ToString() : "-";
            }
            finally { _massOff = keep; }
        }

        private static string New(Recipe r, out float kg)
        {
            kg = 0f;
            var sb = new StringBuilder();
            foreach (var k in r.Kg)
            {
                if (k.Item == null) continue;
                if (sb.Length > 0) sb.Append(" + ");
                sb.Append(Kg(k.Kg)).Append(" kg ").Append(k.Item.StringId).Append(" (").Append(k.Role).Append(')');
                kg += k.Kg;
            }
            return sb.ToString();
        }

        private static readonly string[] Samples =
        {
            "dragonbone_bow", "goldenheart_longbow", "stealth_bow", "hunting_bow", "composite_bow", "noble_long_bow", "crossbow_a", "crossbow_g",
            "default_arrows", "hardened_125_gr_arrows", "bolt_a", "royce_armor", "steppe_leather_boots", "knight_cap"
        };

        /// <summary>OnAfterSessionLaunched: ile przepisow liczy sie z masy, przyklady przed/po, najwieksze odchylenia masy surowca od masy wyrobu, otwarte ladunki.</summary>
        internal static void MassStartLine()
        {
            try
            {
                _massUses = _massOpened = _massLines = 0;
                var mgr = MBObjectManager.Instance;
                if (mgr == null || Campaign.Current == null) return;
                if (!MassOn) { Log.Info("Przepisy z masy (m96-receptury): start sesji | klucz RecipesByMass NIE - przepisy w sztukach jak dotad | potkniecia " + _massStumbles + "."); return; }
                int armour = 0, bows = 0, xbows = 0, ammo = 0, legends = 0, old = 0, metalInBows = 0;
                var ratios = new List<KeyValuePair<float, string>>();
                var samples = new List<string>();
                var want = new HashSet<string>(Samples);
                foreach (var it in mgr.GetObjectTypeList<ItemObject>())
                {
                    if (it == null || !MassCovers(it)) continue;
                    var r = For(it);
                    if (r.Kg == null) { old++; continue; }
                    var t = it.ItemType;
                    if (t == ItemObject.ItemTypeEnum.Bow) { bows++; foreach (var k in r.Kg) if (k.Grade >= 1) metalInBows++; }
                    else if (t == ItemObject.ItemTypeEnum.Crossbow) xbows++;
                    else if (t == ItemObject.ItemTypeEnum.Arrows || t == ItemObject.ItemTypeEnum.Bolts) ammo++;
                    else armour++;
                    if (IsLegendary(it)) legends++;
                    float nk; string nd = New(r, out nk);
                    float piece = Math.Max(0.01f, ArmsPricing.CostWeight(it));
                    if (t == ItemObject.ItemTypeEnum.Arrows || t == ItemObject.ItemTypeEnum.Bolts)
                        piece *= Math.Max(1, it.PrimaryWeapon != null ? (int)it.PrimaryWeapon.MaxDataValue : 30) * Math.Max(1, Settings.Current.AmmoBatchStacks);
                    if (it.Weight > 0.05f) ratios.Add(new KeyValuePair<float, string>(nk / piece, it.StringId + " " + Kg(nk) + "/" + Kg(piece) + " kg"));
                    if (want.Contains(it.StringId) && samples.Count < 16)
                    {
                        float ok; string od = Old(it, out ok);
                        samples.Add(it.StringId + " t" + Grade(it) + " " + Kg(piece) + " kg: BYLO " + od + " (" + Kg(ok) + " kg) -> JEST " + nd);
                    }
                }
                ratios.Sort((a, b) => a.Key.CompareTo(b.Key));
                string med = ratios.Count > 0 ? ratios[ratios.Count / 2].Key.ToString("0.00", Inv) : "-";
                var lo = new List<string>(); var hi = new List<string>();
                for (int i = 0; i < ratios.Count && i < 3; i++) lo.Add(ratios[i].Value + " x" + ratios[i].Key.ToString("0.00", Inv));
                for (int i = ratios.Count - 1; i >= 0 && i >= ratios.Count - 5; i--) hi.Add(ratios[i].Value + " x" + ratios[i].Key.ToString("0.00", Inv));
                Log.Info("Przepisy z masy (m96-receptury): start sesji | klucz RecipesByMass TAK | policzone " + (armour + bows + xbows + ammo) + " (pancerze " + armour + ", luki " + bows
                         + ", kusze " + xbows + ", strzaly i belty " + ammo + "; legendy " + legends + "), stary przepis bez receptury wartosci " + old + ", metal w lukach " + metalInBows
                         + " (ma byc 0) | jednostki: sztaba " + Kg(MatKg(CraftingMaterials.Iron4)) + " kg, len " + Kg(SmithStock.UnitKg(_linen)) + " kg, skora " + Kg(SmithStock.UnitKg(_leather))
                         + " kg, drewno ladunek " + Kg(SmithWood.LoadKg) + " kg | masa surowca / masa wyrobu: mediana x" + med + ", najmniej: " + string.Join("; ", lo.ToArray())
                         + ", najwiecej: " + string.Join("; ", hi.ToArray()) + " | otwarte ladunki: " + OpenText(false) + " | przyklady: " + string.Join("; ", samples.ToArray())
                         + " | potkniecia " + _massStumbles + ".");
            }
            catch (Exception e) { MassStumble("MassStartLine", e); }
        }
    }
}
