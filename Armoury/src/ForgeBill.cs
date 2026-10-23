using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// RACHUNEK KUZNI - JEDEN DLA EKRANU I DLA ZUZYCIA (m109-kuznia; Jeff 23.10: "totalnie zle zrobiony jest interfejs, czego ile potrzeba, aby wykonac np. luk;
    /// musimy ujednolicic caly system ... dorobic te liczby na ekranie kucia: mamy 3 drewna, czyli 300 kg, i 87 kg w pamieci - jako liczba nad ikonkami metali;
    /// teraz przy lukach nie ma w ogole pokazanych wymagan i kuje i kuje i nie wiem, czy to blad, czy mam surowce, ktore sa w pamieci"). Klucz ForgeBillOnScreen.
    /// DLACZEGO LUKI NIE POKAZYWALY NIC (sprawdzone): ekran (pasek gry i rzad BK) pokazywal tylko SZTUKI DO OTWARCIA z sakw (KgPart.Units / ladunki drewna);
    /// przy otwartych resztkach to 0, a ChangeAmountTextWidget przy 0 sie chowa (IsVisible = Amount != 0) - przepis luku (drewno, rog, cieciwa w kg) byl
    /// niewidoczny, a BK HasMaterials przepuszczal. Teraz rzad kafli (ForgeView) pokazuje zawsze kg przepisu i stan: sztuki w sakwach x kg sztuki + pamiec.
    /// PAMIEC (resztki otwartych sztuk przy kowadle, w zapisie): drewno SmithWood.OpenKg (arm_smith_wood_kg), wegiel SmithCoal.OpenKg (arm_smith_coal_kg),
    /// len i skora SmithStock (arm_smith_stock), metal - skrzynka resztek SmeltScraps (arm_smeltscraps, tysiaczne sztaby). Rachunek tylko je czyta.
    /// CO WIDAC = CO ZABRANE: potrzeba z tych samych funkcji, ktore zuzywaja - Recipes.For dla CRAFT (zuzywa Forge.Smith/TakeFor, kwit BK FletchForge.SpendBill
    /// i Recipes.MassCommitBk), koszt modelu kuzni z naszymi latkami + SmithWood/SmithCoal.WeaponKg dla broni (zuzywa CraftingCampaignBehavior.SpendMaterials gry
    /// z prefiksami SmithWood/SmithCoal). Brak = te same warunki co Recipes.HasMaterials, RareMaterials.Ready i BK HasMaterials (sztuki do otwarcia > sakwy).
    /// Pancerz w CRAFT: kwit BK (GetCraftingInputForArmor, ktory zdejmuje SpendMaterials) porownany z przepisem; inny = wiersze z kwitu BK w sztukach i licznik.
    /// Linie "Kuznia wymagania (m109): ..." - start sesji z probkami, ekran (pierwsze 10, potem co 50.), doba; bledy liczone, bez wylacznika.
    /// </summary>
    internal static class ForgeBill
    {
        internal const string P = "Kuznia wymagania (m109): ";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On { get { var s = Settings.Current; return s != null && s.ForgeBillOnScreen; } }

        internal sealed class Row
        {
            public ItemObject Item;
            public string Kind = "piece";   // wood, coal, metal, linen, leather, velvet, rare, piece - slowo jednostki w napisie
            public bool ByKg;               // true: kg z pamiecia; false: cale sztuki
            public float Need;              // kg (ByKg) albo sztuki na jedno kucie (amunicja: cala seria AmmoBatchStacks)
            public int Bags;                // sztuki w sakwach (Recipes.CountInInventory - len, skora, aksamit cala kategoria)
            public float UnitKg;            // kg jednej sztuki
            public float OpenKg;            // pamiec: reszta otwartej sztuki przy kowadle
            public int Opens;               // sztuki z sakw do otwarcia przy tym kuciu (kwit BK / koszt gry)
            public bool Short;
            public bool Rare;
            public float HaveKg { get { return Bags * UnitKg + OpenKg; } }
        }

        internal sealed class Bill
        {
            public ItemObject Item;
            public WeaponDesign Design;
            public readonly List<Row> Rows = new List<Row>();
            public bool Short;
            public Recipes.Recipe Recipe;
            public int[] Cost;
            public int Batch = 1;
            public bool BkTable;            // pancerz: wiersze z kwitu BK (kwit inny niz przepis)
        }

        // ------------------------------------------------------------ liczniki
        private static int _stumbles, _sRefresh;
        private static int _dCraft, _dWeapon, _dShort, _dBlocked, _dBlockedRare, _dBkOther;
        private static Harmony _h;
        private static readonly HashSet<System.Reflection.MethodBase> _patched = new HashSet<System.Reflection.MethodBase>();
        private static string _hook = "nie wpiety";
        private static AccessTools.FieldRef<CraftingVM, Crafting> _fCrafting;
        private static bool _fLooked;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 3 || _stumbles % 100 == 0) Log.Error("ForgeBill." + where + " (potkniecie " + _stumbles + ")", e);
        }

        private static void ResetDay() { _dCraft = _dWeapon = _dShort = _dBlocked = _dBlockedRare = _dBkOther = 0; }

        internal static int Stumbles { get { return _stumbles; } }

        // ------------------------------------------------------------ wiersze
        private static string KindOf(ItemObject it)
        {
            if (it == null) return "piece";
            if (it == Recipes.WoodItem) return "wood";
            if (it == Recipes.MaterialItem(CraftingMaterials.Charcoal)) return "coal";
            if (it == Recipes.SoftLinen) return "linen";
            if (it == Recipes.SoftLeather) return "leather";
            if (it == Recipes.SoftVelvet) return "velvet";
            for (int g = 1; g <= 6; g++) if (it == Recipes.MaterialItem((CraftingMaterials)g)) return "metal";
            return "piece";
        }

        private static Row KgRow(ItemObject it, string kind, float need, float unitKg, float openKg, int opens)
        {
            return new Row { Item = it, Kind = kind, ByKg = true, Need = Math.Max(0f, need), Bags = Recipes.CountInInventory(it), UnitKg = Math.Max(0.001f, unitKg),
                             OpenKg = Math.Max(0f, openKg), Opens = Math.Max(0, opens) };
        }

        private static Row PieceRow(ItemObject it, string kind, int need)
        {
            float u = 0f;
            try { u = it != null ? it.Weight : 0f; } catch { }
            return new Row { Item = it, Kind = kind, ByKg = false, Need = Math.Max(0, need), Bags = Recipes.CountInInventory(it), UnitKg = u, Opens = Math.Max(0, need) };
        }

        private static bool InKg(Recipes.Recipe r, ItemObject it)
        {
            if (r.Kg == null || it == null) return false;
            foreach (var k in r.Kg) if (k.Item == it && k.Grade >= 0) return true;
            return false;
        }

        private static void Finish(Bill b)
        {
            bool any = false;
            foreach (var w in b.Rows)
            {
                w.Short = w.ByKg ? w.Bags < w.Opens : w.Bags < (int)Math.Round(w.Need);
                if (w.Short) any = true;
            }
            b.Short = any;
        }

        // ------------------------------------------------------------ CRAFT (pancerz, luk, kusza, amunicja, tarcza) i nasza kuznia
        /// <summary>Rachunek przedmiotu z przepisu Recipes.For (CRAFT BK, nasza kuznia, menu kowala).</summary>
        internal static Bill Of(ItemObject item)
        {
            if (item == null) return new Bill();
            return Of(item, Recipes.For(item));
        }

        /// <summary>Rachunek z podanego przepisu - Forge.Smith / Begin i test zuzycia biora ten sam przepis, ktory potem zdejmuja.</summary>
        internal static Bill Of(ItemObject item, Recipes.Recipe r)
        {
            var b = new Bill { Item = item, Recipe = r };
            if (item == null) return b;
            var t = item.ItemType;
            if (t == ItemObject.ItemTypeEnum.Arrows || t == ItemObject.ItemTypeEnum.Bolts) b.Batch = Math.Max(1, Settings.Current.AmmoBatchStacks);
            var wood = Recipes.WoodItem;
            int woodParts = 0;
            if (r.Parts != null) foreach (var p in r.Parts) if (p.Item != null && p.Item == wood) woodParts += p.Count;
            // drewno w kg (SmithWood): potrzeba = kg przepisu, otwarcie = ladunki w kwicie (0, gdy otwarty ladunek starcza)
            if (r.WoodKg > 0f && wood != null) b.Rows.Add(KgRow(wood, "wood", r.WoodKg, SmithWood.LoadKg, SmithWood.OpenKg, woodParts));
            // przepis z masy: metal, len, skora w kg z otwartych ladunkow
            if (r.Kg != null)
                foreach (var k in r.Kg)
                {
                    if (k.Item == null || k.Grade < 0) continue;
                    b.Rows.Add(KgRow(k.Item, k.Grade >= 1 ? "metal" : KindOf(k.Item), k.Kg, Recipes.UnitKgOf(k), Recipes.OpenKgOf(k), k.Units));
                }
            // reszta kwitu w calych sztukach (stary przepis: metal, wegiel w brylach, aksamit; drewno bez SmithWoodKg - cale ladunki)
            if (r.Parts != null)
                foreach (var p in r.Parts)
                {
                    if (p.Item == null || p.Count <= 0) continue;
                    if (p.Item == wood && r.WoodKg > 0f) continue;
                    if (InKg(r, p.Item)) continue;
                    b.Rows.Add(PieceRow(p.Item, KindOf(p.Item), p.Count));
                }
            // surowiec luku (czardrzewo, smocza kosc) - te same liczby co RareMaterials.Ready i Take
            ItemObject rm; int need, have;
            if (RareMaterials.NeedOf(item, out rm, out need, out have) && rm != null)
            {
                float u = 0f;
                try { u = rm.Weight; } catch { }
                b.Rows.Add(new Row { Item = rm, Kind = "rare", ByKg = false, Need = need, Bags = have, UnitKg = u, Opens = need, Rare = true });
            }
            // pancerz i tarcza w CRAFT BK: zdejmuje kwit BK - musi byc kwitem tego przepisu (latki TrueArmourCost / BkArmorBillPostfix); inny = wiersze z kwitu
            if (!FletchForge.RangedType(item) && (item.HasArmorComponent || t == ItemObject.ItemTypeEnum.Shield)) CheckBkTable(b, r);
            Finish(b);
            return b;
        }

        private static int BkIndex(ItemObject it)
        {
            if (it == null) return -1;
            for (int m = 0; m < 9; m++) if (Recipes.MaterialItem((CraftingMaterials)m) == it) return m;
            if (it == Recipes.SoftLeather) return 9;
            if (it == Recipes.SoftLinen) return 10;
            return -1;
        }

        /// <summary>Kwit BK pancerza (BannerKingsConfig.Instance.SmithingModel.GetCraftingInputForArmor - z naszymi latkami); null bez BK.</summary>
        internal static int[] BkTable(ItemObject item)
        {
            var cfg = Traverse.CreateWithType("BannerKings.BannerKingsConfig").Property("Instance").GetValue();
            var model = cfg != null ? Traverse.Create(cfg).Property("SmithingModel").GetValue() : null;
            var bill = model != null ? Traverse.Create(model).Method("GetCraftingInputForArmor", item).GetValue<int[]>() : null;
            return bill != null && bill.Length >= 11 ? bill : null;
        }

        private static void CheckBkTable(Bill b, Recipes.Recipe r)
        {
            int[] bill = null;
            try { bill = BkTable(b.Item); } catch (Exception e) { Stumble("BkTable", e); }
            if (bill == null) return;
            var mine = new int[11];
            if (r.Parts != null)
                foreach (var p in r.Parts)
                {
                    if (p.Item == null || p.Count <= 0) continue;
                    int i = BkIndex(p.Item);
                    if (i >= 0) mine[i] += p.Count;
                }
            bool same = true;
            for (int i = 0; i < 11; i++) if (mine[i] != Math.Max(0, bill[i])) { same = false; break; }
            if (same) return;
            // kwit BK inny niz przepis (inne klucze niz u Jeffa) - zdejmie kwit BK w calych sztukach, kg nie schodza (FletchForge.MassSpend: BillMatches NIE)
            _dBkOther++;
            b.BkTable = true;
            var keep = new List<Row>();
            foreach (var w in b.Rows) if (w.Rare || (!w.ByKg && BkIndex(w.Item) < 0)) keep.Add(w);   // surowiec luku i aksamit tarczy - poza tablica BK
            b.Rows.Clear();
            for (int i = 0; i < 11; i++)
            {
                if (bill[i] <= 0) continue;
                var it = i < 9 ? Recipes.MaterialItem((CraftingMaterials)i) : (i == 9 ? Recipes.SoftLeather : Recipes.SoftLinen);
                if (it != null) b.Rows.Add(PieceRow(it, KindOf(it), bill[i]));
            }
            b.Rows.AddRange(keep);
        }

        // ------------------------------------------------------------ bron z ekranu kuzni gry
        /// <summary>Rachunek broni: koszt modelu kuzni (to samo wywolanie co SpendMaterials gry, z latkami SmeltTruth, SmithWood, SmithCoal); drewno i wegiel
        /// w kg z pamiecia (SmithWood/SmithCoal.WeaponKg - te same liczby, ktore zdejmuja ich prefiksy SpendMaterials).</summary>
        internal static Bill OfWeapon(WeaponDesign d)
        {
            var b = new Bill { Design = d };
            if (d == null || Campaign.Current == null || Campaign.Current.Models == null) return b;
            var cost = Campaign.Current.Models.SmithingModel.GetSmithingCostsForWeaponDesign(d);
            b.Cost = cost;
            if (cost == null) return b;
            int iw = (int)CraftingMaterials.Wood, ic = (int)CraftingMaterials.Charcoal;
            var wood = Recipes.MaterialItem(CraftingMaterials.Wood);
            int wl = cost.Length > iw && cost[iw] < 0 ? -cost[iw] : 0;
            if (wood != null)
            {
                if (SmithWood.On) { float kg = SmithWood.WeaponKg(d); if (kg > 0f || wl > 0) b.Rows.Add(KgRow(wood, "wood", kg, SmithWood.LoadKg, SmithWood.OpenKg, wl)); }
                else if (wl > 0) b.Rows.Add(PieceRow(wood, "wood", wl));
            }
            for (int i = 0; i < cost.Length && i < 9; i++)
            {
                if (i == iw || i == ic || cost[i] >= 0) continue;
                var mat = Recipes.MaterialItem((CraftingMaterials)i);
                if (mat != null) b.Rows.Add(PieceRow(mat, "metal", -cost[i]));
            }
            var coal = Recipes.MaterialItem(CraftingMaterials.Charcoal);
            int cl = cost.Length > ic && cost[ic] < 0 ? -cost[ic] : 0;
            if (coal != null)
            {
                if (SmithCoal.On) { float kg = SmithCoal.WeaponKg(d); if (kg > 0f || cl > 0) b.Rows.Add(KgRow(coal, "coal", kg, SmithCoal.LumpKg, SmithCoal.OpenKg, cl)); }
                else if (cl > 0) b.Rows.Add(PieceRow(coal, "coal", cl));
            }
            Finish(b);
            return b;
        }

        // ------------------------------------------------------------ napisy (EN)
        internal static string Kg(double v)
        {
            if (v < 0) v = 0;
            return v < 0.1 ? v.ToString("0.###", Inv) : v < 10 ? v.ToString("0.##", Inv) : v.ToString("0", Inv);
        }

        private static string NameOf(ItemObject it)
        {
            try { return it != null && it.Name != null ? it.Name.ToString() : "?"; }
            catch { return "?"; }
        }

        private static string Unit(Row w, int n)
        {
            string u;
            switch (w.Kind)
            {
                case "wood": u = "load"; break;
                case "metal": u = "bar"; break;
                case "linen": u = "bolt"; break;
                case "leather": u = "hide"; break;
                case "coal": u = "lump"; break;
                default: u = "piece"; break;
            }
            return n == 1 ? u : u + "s";
        }

        internal static string NeedText(Row w) { return w.ByKg ? "-" + Kg(w.Need) + " kg" : "-" + ((int)Math.Round(w.Need)).ToString(Inv); }
        internal static string HaveText(Row w) { return w.ByKg ? Kg(w.HaveKg) + " kg" : w.Bags.ToString(Inv); }
        internal static string DetailText(Row w)
        {
            if (w.ByKg) return w.Bags.ToString(Inv) + "x" + Kg(w.UnitKg) + " +" + Kg(w.OpenKg);
            return w.Rare ? "rare" : "";
        }

        private static string Takes(Bill b)
        {
            if (b.Batch > 1) return "This batch of " + b.Batch + " sheaves takes ";
            return b.Design != null ? "This weapon takes " : "This piece takes ";
        }

        /// <summary>Podpowiedz kafla: co ten przedmiot bierze, co masz (sakwy x kg sztuki + otwarta), co sie stanie przy kuciu.</summary>
        internal static string Hint(Bill b, Row w)
        {
            var sb = new StringBuilder(NameOf(w.Item)).Append(". ");
            if (w.Rare)
            {
                int n = (int)Math.Round(w.Need);
                sb.Append("This bow is made from it: ").Append(n).Append(n == 1 ? " piece" : " pieces").Append(". You carry ").Append(w.Bags).Append('.');
                if (w.Short) { string where = b.Item != null ? RareMaterials.WhereOf(b.Item) : null; if (where != null) sb.Append(" Not enough - ").Append(where).Append('.'); }
                return sb.ToString();
            }
            if (w.ByKg)
            {
                sb.Append(Takes(b)).Append(Kg(w.Need)).Append(" kg. You have ").Append(w.Bags).Append(" in your bags (").Append(Kg(w.UnitKg)).Append(" kg each) = ")
                  .Append(Kg(w.Bags * w.UnitKg)).Append(" kg, and ").Append(Kg(w.OpenKg)).Append(" kg left of the open one = ").Append(Kg(w.HaveKg)).Append(" kg.");
                if (w.Opens <= 0) sb.Append(" The open one covers it - nothing new is taken from your bags.");
                else if (!w.Short) sb.Append(" The open one is not enough - ").Append(w.Opens).Append(" new ").Append(Unit(w, w.Opens)).Append(" will be opened from your bags; what is left stays open for the next pieces.");
                else sb.Append(" The open one is not enough - it needs ").Append(w.Opens).Append(" new ").Append(Unit(w, w.Opens)).Append(" from your bags and you have ").Append(w.Bags).Append(". Not enough to forge it.");
                return sb.ToString();
            }
            int need = (int)Math.Round(w.Need);
            sb.Append(Takes(b)).Append(need).Append(". You have ").Append(w.Bags).Append(" in your bags.");
            if (w.Short) sb.Append(" You are ").Append(need - w.Bags).Append(" short - not enough to forge it.");
            return sb.ToString();
        }

        private static string ShortLine(Bill b, Row w)
        {
            string name = NameOf(w.Item);
            if (w.Rare)
            {
                string where = b.Item != null ? RareMaterials.WhereOf(b.Item) : null;
                return name + ": needs " + (int)Math.Round(w.Need) + ", you carry " + w.Bags + (where != null ? " - " + where : "");
            }
            if (w.ByKg)
                return name + ": needs " + Kg(w.Need) + " kg - the open one holds " + Kg(w.OpenKg) + " kg, so " + w.Opens + " new " + Unit(w, w.Opens) + " must be opened and you have " + w.Bags;
            return name + ": needs " + (int)Math.Round(w.Need) + ", you have " + w.Bags;
        }

        /// <summary>Napis przycisku (multiline: podpowiedz) albo komunikatu (jedna linia): czego brakuje.</summary>
        internal static string ShortText(Bill b, bool multiline = true)
        {
            var parts = new List<string>();
            foreach (var w in b.Rows) if (w.Short) parts.Add(ShortLine(b, w));
            if (parts.Count == 0) return "You don't have all required materials!";
            return multiline ? "You don't have all required materials:\n- " + string.Join("\n- ", parts.ToArray())
                             : "You lack the materials for it - " + string.Join("; ", parts.ToArray()) + ".";
        }

        /// <summary>Opis rachunku w menu kowala (SmithMenu) - te same liczby co rzad na ekranie kucia.</summary>
        internal static string Describe(Bill b)
        {
            var sb = new StringBuilder(b.Batch > 1 ? "Materials for a batch of " + b.Batch + " sheaves (needs / you have):" : "Materials (needs / you have):");
            bool kg = false;
            foreach (var w in b.Rows)
            {
                sb.Append("\n").Append(NameOf(w.Item)).Append(": ").Append(w.ByKg ? Kg(w.Need) + " kg" : ((int)Math.Round(w.Need)).ToString(Inv)).Append(" / ").Append(HaveText(w));
                if (w.ByKg) { kg = true; sb.Append(" (").Append(w.Bags).Append(" x ").Append(Kg(w.UnitKg)).Append(" kg + ").Append(Kg(w.OpenKg)).Append(" kg open)"); }
                if (w.Short) sb.Append(" - NOT ENOUGH");
            }
            if (kg) sb.Append("\nA part-used load, bar, bolt or hide stays open at your anvil; a new one is taken from your bags only when the open one runs short.");
            return sb.ToString();
        }

        // ------------------------------------------------------------ linie logu
        private static string RowLog(Row w)
        {
            string id = w.Item != null ? w.Item.StringId : "?";
            string s = w.ByKg
                ? id + " potrzeba " + Kg(w.Need) + " kg, jest " + w.Bags + " x " + Kg(w.UnitKg) + " + " + Kg(w.OpenKg) + " = " + Kg(w.HaveKg) + " kg, otwarcie " + w.Opens
                : id + " potrzeba " + ((int)Math.Round(w.Need)).ToString(Inv) + " szt., jest " + w.Bags;
            if (w.Rare) s += " (rzadki)";
            if (w.Short) s += " BRAK";
            return s;
        }

        internal static string RowsLog(Bill b)
        {
            if (b.Rows.Count == 0) return "bez surowcow";
            var l = new List<string>();
            foreach (var w in b.Rows) l.Add(RowLog(w));
            return string.Join("; ", l.ToArray()) + (b.BkTable ? " (kwit BK inny niz przepis)" : "");
        }

        private static string IdOf(Bill b)
        {
            if (b.Item != null) return b.Item.StringId;
            try { return b.Design != null && b.Design.Template != null ? b.Design.Template.StringId : "-"; } catch { return "-"; }
        }

        private static string YN(bool v) { return v ? "TAK" : "NIE"; }

        // ------------------------------------------------------------ wejscia dla ForgeView (refleksja, jak PatternGate.BlockText)
        /// <summary>Wiersze rzedu wymagan na ekranie kucia: null = klucz NIE albo blad (ForgeView zostawia rzad BK jak dotad). Element: object[]{ItemObject surowiec,
        /// string potrzeba, string jest, string sklad (sakwy x kg + pamiec), bool brak, string podpowiedz}. design != null - bron z ekranu gry, inaczej przedmiot CRAFT.</summary>
        public static object[] ScreenRows(ItemObject item, WeaponDesign design)
        {
            if (!On) return null;
            try
            {
                bool craft = design == null;
                if (craft && item == null) return new object[0];
                var b = craft ? Of(item) : OfWeapon(design);
                var res = new object[b.Rows.Count];
                for (int i = 0; i < b.Rows.Count; i++)
                {
                    var w = b.Rows[i];
                    res[i] = new object[] { w.Item, NeedText(w), HaveText(w), DetailText(w), w.Short, Hint(b, w) };
                }
                _sRefresh++;
                if (craft) _dCraft++; else _dWeapon++;
                if (b.Short) _dShort++;
                if (_sRefresh <= 10 || _sRefresh % 50 == 0)
                    Log.Info(P + "ekran " + (craft ? "CRAFT" : "bron") + " " + IdOf(b) + " | " + RowsLog(b) + " | da sie wykuc " + YN(!b.Short) + " | odswiezen w sesji " + _sRefresh + ".");
                return res;
            }
            catch (Exception e) { Stumble("ScreenRows", e); return null; }
        }

        /// <summary>Linia ForgeView do logu Armoury (raz na proces, z ForgeView).</summary>
        public static void ScreenNote(string text)
        {
            try { Log.Info(P + text); } catch { }
        }

        /// <summary>Autotest (LogGate 1/2 i plik CrashScribe\armoury-kuznia.flag) - ForgeView pyta o to przy kroku testu.</summary>
        public static bool TestWanted() { return SmithXp.TestWanted(); }

        // ------------------------------------------------------------ przycisk Craft (CRAFT BK i bron)
        /// <summary>FletchForge.BkHasMaterialsPostfix: napis braku (przycisk szary) albo null. bkOk = BK sam przepuszczal - wtedy liczy sie zablokowanie rachunkiem.</summary>
        internal static string CraftBlock(ItemObject item, bool bkOk)
        {
            try
            {
                if (item == null) return null;
                var b = Of(item);
                if (!b.Short) return null;
                if (bkOk)
                {
                    _dBlocked++;
                    bool onlyRare = true;
                    foreach (var w in b.Rows) if (w.Short && !w.Rare) { onlyRare = false; break; }
                    if (onlyRare) _dBlockedRare++;
                }
                return ShortText(b);
            }
            catch (Exception e) { Stumble("CraftBlock", e); return null; }
        }

        private static Crafting CraftingOf(CraftingVM vm)
        {
            if (!_fLooked)
            {
                _fLooked = true;
                try { _fCrafting = AccessTools.FieldRefAccess<CraftingVM, Crafting>("_crafting"); } catch (Exception e) { _fCrafting = null; Stumble("CraftingOf", e); }
            }
            return _fCrafting != null && vm != null ? _fCrafting(vm) : null;
        }

        /// <summary>Postfiks CraftingVM.RefreshEnableMainAction (metoda klasy deklarujacej): przycisk broni zablokowany brakiem materialow (stamina jest, czesci
        /// odblokowane, zlecenie wykonalne) - podpowiedz mowi, czego brakuje, zamiast "You don't have all required materials!". Stan przycisku bez zmian.</summary>
        public static void MainActionPostfix(CraftingVM __instance)
        {
            try
            {
                if (!On || __instance == null || !__instance.IsInCraftingMode || __instance.IsMainActionEnabled || __instance.MainActionHint == null) return;
                if (Campaign.Current == null || Campaign.Current.GameMode == CampaignGameMode.Tutorial) return;
                var hero = __instance.CurrentCraftingHero != null ? __instance.CurrentCraftingHero.Hero : null;
                if (hero != null)
                {
                    var cb = Campaign.Current.GetCampaignBehavior<ICraftingCampaignBehavior>();
                    if (cb == null || cb.GetHeroCraftingStamina(hero) <= 10) return;   // jak HaveEnergy gry - napis "rest" zostaje
                }
                var wd = __instance.WeaponDesign;
                if (wd == null || !wd.HaveUnlockedAllSelectedPieces() || !wd.CanCompleteOrder()) return;
                var c = CraftingOf(__instance);
                var d = c != null ? c.CurrentWeaponDesign : null;
                if (d == null) return;
                var b = OfWeapon(d);
                if (!b.Short) return;
                string text = ShortText(b);
                __instance.MainActionHint = new BasicTooltipViewModel(() => text);
                _dBlocked++;
            }
            catch (Exception e) { Stumble("MainActionPostfix", e); }
        }

        // ------------------------------------------------------------ probki (linia startu i test)
        /// <summary>Przedmioty probne: luk, kusza, zbroja, bron z ekranu gry (jednoreczna z drewnem w czesciach), strzaly - para (rodzaj, przedmiot).</summary>
        internal static List<KeyValuePair<string, ItemObject>> Samples()
        {
            var l = new List<KeyValuePair<string, ItemObject>>();
            var mgr = MBObjectManager.Instance;
            if (mgr == null) return l;
            ItemObject bow = mgr.GetObject<ItemObject>("composite_bow"), xbow = mgr.GetObject<ItemObject>("crossbow_a"), arm = mgr.GetObject<ItemObject>("royce_armor"),
                       arr = mgr.GetObject<ItemObject>("default_arrows"), wpn = null;
            foreach (var it in mgr.GetObjectTypeList<ItemObject>())
            {
                if (it == null) continue;
                var t = it.ItemType;
                if (bow == null && t == ItemObject.ItemTypeEnum.Bow) bow = it;
                if (xbow == null && t == ItemObject.ItemTypeEnum.Crossbow) xbow = it;
                if (arr == null && t == ItemObject.ItemTypeEnum.Arrows) arr = it;
                if (arm == null && t == ItemObject.ItemTypeEnum.BodyArmor && it.ArmorComponent != null && it.ArmorComponent.MaterialType == ArmorComponent.ArmorMaterialTypes.Plate
                    && Recipes.MassCovers(it)) arm = it;
                if (wpn == null && t == ItemObject.ItemTypeEnum.OneHandedWeapon && WoodInParts(it.WeaponDesign)) wpn = it;
                if (bow != null && xbow != null && arm != null && arr != null && wpn != null) break;
            }
            if (bow != null) l.Add(new KeyValuePair<string, ItemObject>("luk", bow));
            if (xbow != null) l.Add(new KeyValuePair<string, ItemObject>("kusza", xbow));
            if (arm != null) l.Add(new KeyValuePair<string, ItemObject>("zbroja", arm));
            if (wpn != null) l.Add(new KeyValuePair<string, ItemObject>("bron", wpn));
            if (arr != null) l.Add(new KeyValuePair<string, ItemObject>("strzaly", arr));
            return l;
        }

        internal static bool WoodInParts(WeaponDesign d)
        {
            try
            {
                if (d == null || d.UsedPieces == null) return false;
                foreach (var el in d.UsedPieces)
                {
                    if (el == null || !el.IsValid || el.CraftingPiece == null || el.CraftingPiece.MaterialsUsed == null) continue;
                    foreach (var m in el.CraftingPiece.MaterialsUsed) if (m.Item1 == CraftingMaterials.Wood && m.Item2 > 0) return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>Rachunek probki: bron (z projektem) - z ekranu gry, reszta - CRAFT.</summary>
        internal static Bill SampleBill(string kind, ItemObject it)
        {
            return kind == "bron" ? OfWeapon(it.WeaponDesign) : Of(it);
        }

        // ------------------------------------------------------------ sesja i doba
        /// <summary>OnAfterSessionLaunched (po Recipes.MassStartLine): latka przycisku broni (raz na proces), linia startu z probkami.</summary>
        internal static void SessionStart()
        {
            _sRefresh = 0;
            ResetDay();
            try
            {
                if (_h == null) _h = new Harmony("com.jeff.armoury.forgebill");
                var m = AccessTools.DeclaredMethod(typeof(CraftingVM), "RefreshEnableMainAction");
                if (m == null) _hook = "BRAK RefreshEnableMainAction";
                else
                {
                    if (!_patched.Contains(m)) { _h.Patch(m, postfix: new HarmonyMethod(typeof(ForgeBill), nameof(MainActionPostfix))); _patched.Add(m); }
                    _hook = "wpiety";
                }
            }
            catch (Exception e) { _hook = "wyjatek " + e.GetType().Name; Stumble("SessionStart", e); }
            var samples = new List<string>();
            try
            {
                foreach (var kv in Samples())
                {
                    try { samples.Add(kv.Value.StringId + " (" + kv.Key + ") [" + RowsLog(SampleBill(kv.Key, kv.Value)) + "]"); }
                    catch (Exception e) { Stumble("SessionStart(probka " + kv.Value.StringId + ")", e); samples.Add(kv.Value.StringId + " (" + kv.Key + ") [blad]"); }
                }
            }
            catch (Exception e) { Stumble("SessionStart(probki)", e); }
            string craft = FletchForge.ShieldHookState ?? "?";
            string craftHook = craft.IndexOf("czy stac BRAK", StringComparison.Ordinal) >= 0 ? "BRAK" : craft.IndexOf("czy stac", StringComparison.Ordinal) >= 0 ? "wpiete" : craft;
            Log.Info(P + "start sesji | klucz ForgeBillOnScreen " + YN(On) + " | latki: czy stac CRAFT " + craftHook + ", przycisk broni (RefreshEnableMainAction) " + _hook
                     + " | test autotestu " + YN(TestWanted()) + " | probki: " + (samples.Count > 0 ? string.Join("; ", samples.ToArray()) : "brak") + " | potkniecia " + _stumbles + ".");
        }

        /// <summary>ArmouryBehavior.OnDailyTickBodyInner: linia doby (gdy ekran byl odswiezany albo sa potkniecia) i zerowanie.</summary>
        internal static void Daily(int day)
        {
            try
            {
                int n = _dCraft + _dWeapon;
                if (n > 0 || _stumbles > 0)
                    Log.Info(P + "dzien " + day + " | odswiezen ekranu " + n + " (CRAFT " + _dCraft + ", bron " + _dWeapon + "), braki " + _dShort + ", przycisk zablokowany rachunkiem "
                             + _dBlocked + " (rzadki surowiec " + _dBlockedRare + "), kwit BK inny niz przepis " + _dBkOther + ", potkniecia " + _stumbles + ".");
            }
            catch (Exception e) { Stumble("Daily", e); }
            ResetDay();
        }
    }
}
