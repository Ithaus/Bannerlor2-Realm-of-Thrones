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

        internal static void Reset() { _stumbles = 0; NoTownMines.Reset(); for (int i = 0; i < _wEma.Length; i++) _wEma[i] = double.NaN; _wEmaDay = int.MinValue; }

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
                     + ", Self Mend Wood Kg " + TN(s.SelfMendWoodKg) + ", Smith Coal Kg " + TN(s.SmithCoalKg)
                     + ", Folk Craft Wage On Sale " + TN(s.FolkCraftWageOnSale) + ", Sutlers By Purse " + TN(s.SutlersByPurse) + ", Folk Guard No Memory " + TN(s.FolkGuardNoMemory)
                     + ", Folk Loan Before Guard " + TN(s.FolkLoanBeforeGuard)
                     + " | obok: Goods Rates By Pattern " + TN(s.GoodsRatesByPattern) + ", Smith Wood Kg " + TN(s.SmithWoodKg)
                     + ", ceny historyczne " + TN(HistoricalPrices.On && HistoricalPrices.Applied)
                     + ", Folk Hoard Tax " + TN(s.FolkHoardTax) + ", Folk Circulation Guard " + TN(s.FolkCirculationGuard) + ", Folk Loans " + TN(s.FolkLoans)
                     + ", Sutlers Enabled " + TN(s.SutlersEnabled) + ", Men Purse Enabled " + TN(s.MenPurseEnabled) + ", Goods Ledger Enabled " + TN(s.GoodsLedgerEnabled) + ".");
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
                // recenzja (drobna): MaterialLaw.RefinePostfix daje x1.5 z perkiem CharcoalMaker (Jeff kuje - perk prawdopodobny) - oczekiwane tym samym wzorem
                bool perk = false;
                try { perk = Hero.MainHero != null && TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks.Crafting.CharcoalMaker != null && Hero.MainHero.GetPerkValue(TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks.Crafting.CharcoalMaker); } catch { }
                int expect = cw > 0.01f ? Math.Max(1, (int)Math.Round(ww * 0.2f / cw * (perk ? 1.5f : 1f))) : -1;
                Log.Info("Noc-ekonomia (NE): wegiel (N2) | bryla " + F(cw) + " kg (waga gry " + F(ArmouryBehavior.CoalGameKg) + " kg), cena bryly " + (coal != null ? coal.Value.ToString(Inv) : "-")
                         + " d (" + (cw > 0.01f && coal != null ? F(coal.Value / cw) : "-") + " d/kg)"
                         + " | ladunek drewna " + F(ww) + " kg -> wegli " + fromWood + " (przetop gracza z czynnego modelu kuzni; perk CharcoalMaker " + TN(perk) + "; z wag oczekiwane " + expect + ")"
                         + " | bryly wegla w kwicie kuzni x" + F(Recipes.CoalLumpScale()) + " (kg wegla na kg zelaza jak przy bryle 0.5 kg)"
                         + " | stopnie z weglem: " + (steps.Count > 0 ? string.Join("; ", steps.ToArray()) : "-")
                         + " | bron i przetop na ekranie kuzni: Smith Coal Kg " + TN(s.SmithCoalKg) + ", " + F(SmithCoal.PieceKg) + " kg wegla na sztuke z otwartej bryly (zapas "
                         + F(SmithCoal.OpenKg) + " kg), latki " + SmithCoal.Hook + ", samosprawdzenie wegla broni: " + SmithCoal.CheckText + " (przy kluczu: 0 i -1)"
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
                int cu = SmithCoal.DayUses, cl = SmithCoal.DayLumps, ch = SmithCoal.ScreenHints, cst = SmithCoal.DayStumbles; double ck = SmithCoal.DayKg;   // N2 po recenzji
                SmithCoal.DayUses = SmithCoal.DayLumps = SmithCoal.ScreenHints = SmithCoal.DayStumbles = 0; SmithCoal.DayKg = 0;
                int st = _stumbles + mst + sst + cst; _stumbles = 0;
                Log.Info("Noc-ekonomia (NE): dzien " + (int)CampaignTime.Now.ToDays + " | wegiel " + (coal != null ? F(coal.Weight) : "-") + " kg"
                         + " | futra: Fur Rate Raised " + TN(s.FurRateRaised) + ", wsi traperskich " + trappers + ", wywolan modelu z czynnikiem x" + F(GoodsRates.FurRate) + " " + fur
                         + " | kopalnie: No Town Mines " + TN(s.NoTownMines) + ", " + mines
                         + " | kuznia: podpowiedzi drewna na ekranie " + hints + ", czesci przepisu z drewnem w kg (naprawy wlasnymi rekami, pekniecia) " + mu + " (kg " + mk.ToString("0.##", Inv) + ", ladunkow " + ml + ")"
                         + ", wegiel z otwartej bryly (bron i przetop na ekranie kuzni) " + cu + " (kg " + ck.ToString("0.##", Inv) + ", bryl " + cl + ", podpowiedzi " + ch + ")"
                         + " | potkniecia " + st + ".");
            }
            catch (Exception e) { Stumble("Daily", e); }
            CoinLine(s);
            CaravanLine();
        }

        // ------------------------------------------------------------ cz. 2: obieg zlota (N4, N6, N7, N8) - doba
        private static string Pc(double a, double b) { return b > 0 ? (100.0 * a / b).ToString("0.0", Inv) + "%" : "-"; }
        private static string Sg(double v) { return (v >= 0 ? "+" : "") + Math.Round(v).ToString("0", Inv); }

        /// <summary>
        /// Linia "Noc-ekonomia (NE): obieg zlota (N4 N6 N7 N8): dzien D" - po TownFolk.Daily (N4 doplata, N7 straznik, N8 zaliczenie), Sutlers.Daily (N6) i ClanBudget.Daily
        /// (N8 pozyczki tej doby). Kazda czesc: ile przelano i ile mniej / wiecej niz stara regula (te same granice, ta sama doba).
        /// </summary>
        private static void CoinLine(Settings s)
        {
            int st0 = _stumbles;
            try
            {
                var sb = new System.Text.StringBuilder(1400);
                sb.Append("Noc-ekonomia (NE): obieg zlota (N4 N6 N7 N8): dzien ").Append((int)CampaignTime.Now.ToDays);
                // N4
                int n4st = TownFolk.N4Stumbles; TownFolk.N4Stumbles = 0;
                sb.Append(" | N4 doplata do 35%: Folk Craft Wage On Sale ").Append(TN(s.FolkCraftWageOnSale))
                  .Append(", wartosc wyrobow miast wykonanych ").Append(Math.Round(TownFolk.N4MadeV).ToString("0", Inv))
                  .Append(", zeszlych z polki z puli ").Append(Math.Round(TownFolk.N4SoldV).ToString("0", Inv)).Append(" (").Append(TownFolk.N4SoldN).Append(" szt. w ").Append(TownFolk.N4Towns)
                  .Append(" miastach; na zlom bez doplaty ").Append(TownFolk.N4GoneN).Append(" szt.)")
                  .Append(", pula niesprzedanych ").Append(Math.Round(TownFolk.N4PoolV).ToString("0", Inv)).Append(" (").Append(TownFolk.N4PoolN).Append(" szt.)")
                  .Append(", doplata z kas miast zaplacona ").Append(TownFolk.N4TopUp).Append(" (stara regula ").Append(TownFolk.N4TopUpOld).Append(", roznica ").Append(Sg(TownFolk.N4TopUp - TownFolk.N4TopUpOld)).Append(')')
                  // poprawka po recenzji: sztuki-widma - dubli zdarzen gry pominietych, sztuki gry bez BK, zdjete przez rzemieslnikow BK, kontrola polki (prog NE-17b)
                  .Append(", dubli zdarzen gry pominietych ").Append(TownFolk.N4DupN).Append(" szt., sztuk gry bez BK ").Append(TownFolk.N4GameN)
                  .Append(", zdjetych przez rzemieslnikow BK ").Append(TownFolk.N4DropN).Append(" szt., kontrola polki (latki ").Append(TownFolk.N4Wired ? "TAK" : "NIE")
                  .Append("): do puli w wyrobie do miasta ").Append(TownFolk.N4ChkIn).Append(" szt. wobec przyrostu polek ").Append(TownFolk.N4ChkShelf)
                  .Append(" szt. (ponad przyrost ").Append(TownFolk.N4ChkOver).Append(", oczekiwan bez zdarzenia ").Append(TownFolk.N4StaleN).Append(')');
                // N6
                long sut = Sutlers.LastTown >= 0 ? Sutlers.LastTown + Math.Max(0, Sutlers.LastVill) : -1;
                sb.Append(" | N6 markietani: Sutlers By Purse ").Append(TN(s.SutlersByPurse)).Append(Sutlers.N6On ? "" : " (nie biegnie dzis)")
                  .Append(", udzial nowej reguly ").Append(Sutlers.N6Ramp.ToString("0.00", Inv)).Append(" (").Append(Sutlers.N6RampText).Append(')')
                  .Append(", kwota wedlug starej reguly ").Append(Sutlers.N6PlanOld).Append(", wedlug nowej ").Append(Sutlers.N6Plan).Append(" (roznica ").Append(Sg(Sutlers.N6Plan - Sutlers.N6PlanOld))
                  .Append("), zdjete z sakiewek ").Append(sut >= 0 ? sut.ToString(Inv) : "-")
                  .Append(" | partii ").Append(Sutlers.N6Parties).Append(" (mnoznik > 1: ").Append(Sutlers.N6Up).Append(", < 1: ").Append(Sutlers.N6Down).Append(", sredni ")
                  .Append(Sutlers.N6Parties > 0 ? (Sutlers.N6FacSum / Sutlers.N6Parties).ToString("0.00", Inv) : "-").Append(')')
                  .Append(", nizszy limit K1 (bez zakupu sprzetu od 14 dob) ").Append(Sutlers.N6Idle).Append(" partii - uwolnione oszczednosci ").Append(Sutlers.N6Released)
                  .Append(", partii z doba zakupu sprzetu ").Append(Sutlers.N6Tracked);
                // N7
                double q = TownFolk.N7Ratio;
                sb.Append(" | N7 straznik: Folk Guard No Memory ").Append(TN(s.FolkGuardNoMemory)).Append(TownFolk.N7Today ? "" : " (regula bez pamieci nie liczyla dzis)")
                  .Append(", lud / poziom normalny ").Append(double.IsNaN(q) ? "-" : q.ToString("0.000", Inv))
                  .Append(", mnoznik bez pamieci ").Append(double.IsNaN(TownFolk.N7Mult) ? "-" : TownFolk.N7Mult.ToString("0.000", Inv)).Append(" w granicach ").Append(TownFolk.N7Min.ToString("0.00", Inv)).Append("-").Append(TownFolk.N7Max.ToString("0.00", Inv))
                  .Append(" (stara regula z pamiecia ").Append(TownFolk.N7MultOld.ToString("0.000", Inv)).Append(')')
                  .Append(", stawka doby ").Append((100 * TownFolk.N7Rate).ToString("0.000", Inv)).Append("%/d (stara ").Append((100 * TownFolk.N7RateOld).ToString("0.000", Inv)).Append("%/d)")
                  .Append(", podatek od nadwyzki zaplanowany ").Append(TownFolk.N7Plan).Append(" (przy starej stawce ").Append(TownFolk.N7PlanOld).Append(", roznica ").Append(Sg(TownFolk.N7Plan - TownFolk.N7PlanOld)).Append(')');
                // N8
                sb.Append(" | N8 pozyczka przed darem: Folk Loan Before Guard ").Append(TN(s.FolkLoanBeforeGuard))
                  .Append(", migawka kies ").Append(TownFolk.N8SnapN).Append(", kandydaci z nadwyzka tylko dzieki migawce ").Append(TownFolk.N8Unlocked)
                  .Append(", miejsce w pulapie ").Append(Math.Round(TownFolk.N8RoomNew).ToString("0", Inv)).Append(" (stara regula ").Append(Math.Round(TownFolk.N8RoomOld).ToString("0", Inv))
                  .Append("), pozyczone dzis ").Append(TownFolk.LastLoanLent).Append(" (ponad stara regule ").Append(TownFolk.N8Extra).Append(" u ").Append(TownFolk.N8ExtraN).Append(" rodow)")
                  .Append(", zaliczone na podatek dzis ").Append(TownFolk.N8CreditUsed).Append(" w ").Append(TownFolk.N8CreditN).Append(" kiesach - podatek mniejszy o ")
                  .Append(Math.Max(0, TownFolk.N8PlanNoCredit - TownFolk.N7Plan))
                  .Append(" | potkniecia ").Append(n4st + (_stumbles - st0)).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("CoinLine", e); }
        }

        // ------------------------------------------------------------ N1(b): karawany - zapas wedlug towaru (ksiega towarow, ta sama doba)
        private static readonly double[] _wEma = { double.NaN, double.NaN, double.NaN, double.NaN, double.NaN };
        private static int _wEmaDay = int.MinValue;
        private static readonly string[] WatchName = { "sol", "narzedzia", "wino", "maslo", "srebro" };

        private static void CaravanLine()
        {
            try
            {
                int day = GoodsLedger.WatchDay;
                if (day < 0 || day != (int)CampaignTime.Now.ToDays - 1) { Log.Info("Karawany - zapas wedlug towaru (nekon): dzien " + ((int)CampaignTime.Now.ToDays - 1) + " | ksiega towarow nie liczyla dzis (Goods Ledger Enabled / Log Enabled) - brak liczb."); return; }
                var sb = new System.Text.StringBuilder(900);
                sb.Append("Karawany - zapas wedlug towaru (nekon): dzien ").Append(day).Append(" | dni = zapas / zuzycie swiata na dobe (srednia 28 dob ksiegi towarow w tej sesji)");
                bool newDay = day != _wEmaDay;
                for (int w = 0; w < GoodsLedger.WatchIds.Length && w < WatchName.Length; w++)
                {
                    if (!GoodsLedger.WSeen[w]) { sb.Append(" | ").Append(WatchName[w]).Append(" (").Append(GoodsLedger.WatchIds[w]).Append("): brak towaru w ksiedze"); continue; }
                    double c = GoodsLedger.WCons[w];
                    if (newDay) _wEma[w] = double.IsNaN(_wEma[w]) ? c : _wEma[w] + (c - _wEma[w]) / 28.0;
                    double e = _wEma[w];
                    Func<long, string> D = x => e >= 0.5 ? (x / e).ToString("0.0", Inv) : "-";
                    sb.Append(" | ").Append(WatchName[w]).Append(" (").Append(GoodsLedger.WatchIds[w]).Append("): karawany ").Append(GoodsLedger.WCar[w]).Append(" = ").Append(D(GoodsLedger.WCar[w])).Append(" dni")
                      .Append(", miasta ").Append(GoodsLedger.WTown[w]).Append(" = ").Append(D(GoodsLedger.WTown[w])).Append(" dni, swiat ").Append(GoodsLedger.WAll[w])
                      .Append(" (karawany ").Append(Pc(GoodsLedger.WCar[w], GoodsLedger.WAll[w])).Append(')')
                      .Append(", zuzycie dnia ").Append(GoodsLedger.WCons[w]).Append(" (srednia ").Append(e.ToString("0.0", Inv)).Append("), produkcja ").Append(GoodsLedger.WProd[w])
                      .Append(", karawany kupily ").Append(GoodsLedger.WCarBuy[w]).Append(" / sprzedaly ").Append(GoodsLedger.WCarSell[w]);
                }
                _wEmaDay = day;
                sb.Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("CaravanLine", e); }
        }
    }
}
