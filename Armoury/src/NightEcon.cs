using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// NOC 16.10, galaz noc-ekonomia - LINIE LOGU paczki "towary i ceny" (raport audytu ekonomii 15.10, rozdz. 3.1 N2, N3, N9 helmy, N10, N11, N12 i rozdz. 3.2 P8a).
    /// Same zmiany siedza w: ArmouryBehavior.FixCharcoalWeight + Recipes.CoalLumpScale + ArmsPricing (N2 wegiel), GoodsRates (N3 sol x3.5, P8a futra x3),
    /// ArmsPricing.CostWeight / Compute (N9 helmy), NoTownMines (N10), BulkUnit (N11), SmithWood.ScreenPostfix + Recipes.MissingParts/TakePartial +
    /// MendMaterial.SelfParts (N12). Tu tylko rachunek do progow `sprawdz_logi --grupa nekon` (NE-0..):
    ///  - start sesji (PO HistoricalPrices.Apply i SmithWood.SessionStart): klucze TAK/NIE, wegiel (waga, wegle z ladunku drewna z czynnego modelu kuzni),
    ///    helmy plytowe (mediany wartosci przed / po wedlug tieru), jednostka ladunku, kolejnosc latek DecideBestWorkshopType, kuznia (ekran, samosprawdzenie naprawy);
    ///  - doba: futra (wywolania czynnika), kopalnie w miastach (zamiany), kuznia (podpowiedzi, czesci z drewnem w kg), potkniecia.
    /// Bez stanu kampanii (nic do zapisu); wyjatek liczony w potknieciach, reszta liczy sie dalej.
    /// </summary>
    internal static class NightEcon
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static int _stumbles;

        internal static void Reset() { _stumbles = 0; NoTownMines.Reset(); }

        private static string TN(bool b) { return b ? "TAK" : "NIE"; }
        private static string F(double v) { return v.ToString("0.##", Inv); }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 3) Log.Error("NightEcon." + where, e);
        }

        internal static void SessionStart()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            try { NoTownMines.SessionStart(); } catch (Exception e) { Stumble("SessionStart(N10)", e); }
            Log.Info("Noc-ekonomia (NE): start sesji | klucze: Charcoal Weight Kg " + F(s.CharcoalWeightKg) + (s.CharcoalWeightKg > 0f ? "" : " (waga gry)")
                     + ", Salt Rate Raised " + TN(s.SaltRateRaised) + ", Fur Rate Raised " + TN(s.FurRateRaised) + ", Helm One Rule " + TN(s.HelmOneRule)
                     + ", No Town Mines " + TN(s.NoTownMines) + ", Bulk Unit Per Campaign " + TN(s.BulkUnitPerCampaign) + ", Smith Wood On Screen " + TN(s.SmithWoodOnScreen)
                     + ", Self Mend Wood Kg " + TN(s.SelfMendWoodKg) + " | obok: Goods Rates By Pattern " + TN(s.GoodsRatesByPattern) + ", Smith Wood Kg " + TN(s.SmithWoodKg)
                     + ", ceny historyczne " + TN(HistoricalPrices.On && HistoricalPrices.Applied) + ".");
            CoalLine(s);
            HelmLine(s);
            try { Log.Info("Noc-ekonomia (NE): jednostka ladunku (N11) | " + BulkUnit.Line() + "."); } catch (Exception e) { Stumble("SessionStart(N11)", e); }
            MinesLine(s);
            ForgeLine(s);
        }

        // ------------------------------------------------------------ N2 wegiel
        private static void CoalLine(Settings s)
        {
            int st0 = _stumbles;
            try
            {
                var coal = Recipes.MaterialItem(CraftingMaterials.Charcoal);
                var wood = Recipes.MaterialItem(CraftingMaterials.Wood);
                float cw = coal != null ? coal.Weight : 0f, ww = wood != null ? wood.Weight : 0f;
                int fromWood = -1;
                var steps = new List<string>();
                try
                {
                    var model = Campaign.Current.Models != null ? Campaign.Current.Models.SmithingModel : null;
                    if (model != null && Hero.MainHero != null)
                        foreach (var f in model.GetRefiningFormulas(Hero.MainHero))
                        {
                            if (f.Input1 == CraftingMaterials.Wood && f.Output == CraftingMaterials.Charcoal) fromWood = f.OutputCount;
                            else if (f.Input2 == CraftingMaterials.Charcoal && f.Input2Count > 0 && steps.Count < 7)
                                steps.Add(f.Input1 + " " + f.Input1Count + " + wegiel " + f.Input2Count + " -> " + f.Output + " " + f.OutputCount);
                        }
                }
                catch (Exception e) { Stumble("CoalLine(przetop)", e); }
                int expect = cw > 0.01f ? Math.Max(1, (int)Math.Round(ww * 0.2f / cw)) : -1;
                Log.Info("Noc-ekonomia (NE): wegiel (N2) | bryla " + F(cw) + " kg (waga gry " + F(ArmouryBehavior.CoalGameKg) + " kg), cena bryly " + (coal != null ? coal.Value.ToString(Inv) : "-")
                         + " d (" + (cw > 0.01f && coal != null ? F(coal.Value / cw) : "-") + " d/kg)"
                         + " | ladunek drewna " + F(ww) + " kg -> wegli " + fromWood + " (przetop gracza z czynnego modelu kuzni; z wag oczekiwane " + expect + ")"
                         + " | bryly wegla w kwicie kuzni x" + F(Recipes.CoalLumpScale()) + " (kg wegla na kg zelaza jak przy bryle 0.5 kg)"
                         + " | stopnie z weglem: " + (steps.Count > 0 ? string.Join("; ", steps.ToArray()) : "-")
                         + " | potkniecia " + (_stumbles - st0) + ".");
            }
            catch (Exception e) { Stumble("CoalLine", e); }
        }

        // ------------------------------------------------------------ N9 helmy
        private static string Med(List<int> l)
        {
            if (l == null || l.Count == 0) return "-";
            l.Sort();
            return F((l[(l.Count - 1) / 2] + l[l.Count / 2]) / 2.0);
        }

        private static double MedV(List<int> l)
        {
            if (l == null || l.Count == 0) return double.NaN;
            l.Sort();
            return (l[(l.Count - 1) / 2] + l[l.Count / 2]) / 2.0;
        }

        private static void HelmLine(Settings s)
        {
            int st0 = _stumbles;
            try
            {
                bool hist = HistoricalPrices.On && HistoricalPrices.Applied;
                var before = new List<int>[7]; var after = new List<int>[7];
                for (int i = 0; i < 7; i++) { before[i] = new List<int>(); after[i] = new List<int>(); }
                int capped = 0, all = 0;
                bool first = true;
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    try
                    {
                        if (it == null || it.ItemType != ItemObject.ItemTypeEnum.HeadArmor || it.ArmorComponent == null
                            || it.ArmorComponent.MaterialType != ArmorComponent.ArmorMaterialTypes.Plate || it.Value <= 0) continue;
                        all++;
                        if (ArmsPricing.CostWeight(it) < it.Weight - 0.001f) capped++;
                        if (it.NotMerchandise || ArmsPricing.IsUnique(it)) continue;
                        int t = Math.Max(1, Math.Min(6, (int)it.Tier + 1));
                        var co = ArmsPricing.ComputeOldHelm(it);
                        var cn = ArmsPricing.CostOf(it);
                        if (co == null || cn == null) continue;
                        float vb = hist ? HistoricalPrices.HistCostOf(it, co) : co.Total;
                        float va = hist ? it.Value : cn.Total;
                        before[t].Add(Math.Max(1, (int)Math.Round(vb)));
                        after[t].Add(Math.Max(1, (int)Math.Round(va)));
                    }
                    catch (Exception e) { if (first) { first = false; Stumble("HelmLine(sztuka)", e); } else _stumbles++; }
                }
                var parts = new List<string>();
                for (int t = 1; t <= 6; t++) parts.Add("t" + t + " " + Med(before[t]) + "/" + Med(after[t]) + " (" + after[t].Count + " szt.)");
                double b3 = MedV(before[3]), b4 = MedV(before[4]), a3 = MedV(after[3]), a4 = MedV(after[4]);
                string ord = double.IsNaN(a3) || double.IsNaN(a4) ? "brak t3 albo t4" : TN(a3 <= a4);
                string ordB = double.IsNaN(b3) || double.IsNaN(b4) ? "-" : TN(b3 <= b4);
                Log.Info("Noc-ekonomia (NE): helmy plytowe (N9) | Helm One Rule " + TN(s.HelmOneRule) + ", dni = waga x " + F(s.HelmDaysPerKg) + " + 0.5 x max(0, t - 3) dla t1-6"
                         + ", waga do kosztu = min(waga gry, max(0.5, k x pancerz glowy)), k " + ArmsPricing.HelmKgPerArmor().ToString("0.0000", Inv) + " kg na punkt (mediana "
                         + ArmsPricing.HelmKCount + " helmow plytowych), przycieta waga " + capped + " z " + all
                         + " | mediany wartosci na handel bez unikatow przed/po (" + (hist ? "ceny historyczne" : "koszt wykucia ArmsPricing") + "): " + string.Join(", ", parts.ToArray())
                         + " | po: t3 <= t4 " + ord + " (przed: " + ordB + ") | potkniecia " + (_stumbles - st0) + ".");
            }
            catch (Exception e) { Stumble("HelmLine", e); }
        }

        // ------------------------------------------------------------ N10 kopalnie
        private static void MinesLine(Settings s)
        {
            try
            {
                int mines = 0, towns = 0;
                foreach (var t in Town.AllTowns)
                {
                    if (t == null || t.Workshops == null || t.Settlement == null || !t.Settlement.IsTown) continue;
                    towns++;
                    foreach (var w in t.Workshops) if (w != null && w.WorkshopType != null && w.WorkshopType.StringId == "mines") mines++;
                }
                Log.Info("Noc-ekonomia (NE): kopalnie w miastach (N10) | No Town Mines " + TN(s.NoTownMines) + ", latka " + NoTownMines.Hook
                         + ", kolejnosc postfiksow DecideBestWorkshopType: " + NoTownMines.Order() + " | kopalni w miastach teraz " + mines + " (miast " + towns
                         + "; stoja do zmiany wlasciciela - przestawiamy tylko przyszle wybory typu).");
            }
            catch (Exception e) { Stumble("MinesLine", e); }
        }

        // ------------------------------------------------------------ N12 kuznia
        private static void ForgeLine(Settings s)
        {
            float keep = SmithWood.OpenKg;
            string probe = "brak sztuki z drewnem w przepisie";
            try
            {
                // samosprawdzenie: otwarty ladunek 1 kg, naprawa 10% (MendMaterialMaxShare x polowa stanu) sztuki z drewnem 2-10 kg - nowa regula 0 ladunkow
                SmithWood.OpenKg = 1f;
                float share = Math.Max(0f, s.MendMaterialMaxShare) * 0.5f;
                foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (it == null || it.ItemType != ItemObject.ItemTypeEnum.Shield || it.NotMerchandise) continue;
                    var r = Recipes.For(it);
                    if (r.WoodKg < 2f || r.WoodKg > 10f) continue;
                    int full = SmithWood.LoadsFor(r.WoodKg);
                    int now = SmithWood.LoadsFor(r.WoodKg * share);
                    int old = full > 0 ? Math.Max(1, (int)(full * share)) : 0;
                    probe = it.StringId + " drewno przepisu " + F(r.WoodKg) + " kg, naprawa " + F(share * 100f) + "% = " + F(r.WoodKg * share) + " kg przy otwartym 1 kg: ladunkow "
                            + (SmithWood.MendKgOn ? now : old) + " (regula kg " + now + ", stara regula czesci " + old + "; oczekiwane przy kluczu 0)";
                    break;
                }
            }
            catch (Exception e) { Stumble("ForgeLine", e); }
            finally { SmithWood.OpenKg = keep; }
            Log.Info("Noc-ekonomia (NE): kuznia (N12) | Smith Wood Kg " + TN(s.SmithWoodKg) + ", ekran kucia " + SmithWood.ScreenHook + " (Smith Wood On Screen " + TN(s.SmithWoodOnScreen)
                     + "), Self Mend Wood Kg " + TN(s.SelfMendWoodKg) + ", Self Repair Same Rule " + TN(s.SelfRepairSameRule) + " | zapas otwartego ladunku " + F(SmithWood.OpenKg) + " kg"
                     + " | samosprawdzenie naprawy: " + probe + ".");
        }

        // ------------------------------------------------------------ doba
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null) return;
            try
            {
                int trappers = 0;
                foreach (var v in Village.All) if (v != null && v.VillageType != null && v.VillageType.StringId == "trapper") trappers++;
                long fur = GoodsRates.TakeFurCalls();
                int swaps, mst;
                string mines = NoTownMines.DayText(out swaps, out mst);
                var coal = Recipes.MaterialItem(CraftingMaterials.Charcoal);
                int hints = SmithWood.ScreenHints, sst = SmithWood.ScreenStumbles;
                int mu = SmithWood.MendUses, ml = SmithWood.MendLoads; double mk = SmithWood.MendKg;
                SmithWood.ScreenHints = SmithWood.ScreenStumbles = 0; SmithWood.MendUses = SmithWood.MendLoads = 0; SmithWood.MendKg = 0;
                int st = _stumbles + mst + sst; _stumbles = 0;
                Log.Info("Noc-ekonomia (NE): dzien " + (int)CampaignTime.Now.ToDays + " | wegiel " + (coal != null ? F(coal.Weight) : "-") + " kg"
                         + " | futra: Fur Rate Raised " + TN(s.FurRateRaised) + ", wsi traperskich " + trappers + ", wywolan modelu z czynnikiem x" + F(GoodsRates.FurRate) + " " + fur
                         + " | kopalnie: No Town Mines " + TN(s.NoTownMines) + ", " + mines
                         + " | kuznia: podpowiedzi drewna na ekranie " + hints + ", czesci przepisu z drewnem w kg (naprawy wlasnymi rekami, pekniecia) " + mu + " (kg " + mk.ToString("0.##", Inv) + ", ladunkow " + ml + ")"
                         + " | potkniecia " + st + ".");
            }
            catch (Exception e) { Stumble("Daily", e); }
        }
    }
}
