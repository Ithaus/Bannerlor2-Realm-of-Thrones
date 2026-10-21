using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// E8/C (253, PLAN 8.5d-g, 8.4c; projekt etapu 8 v2 rozdz. 3 "E8/C") - CENNIK: LINIE STARTU (tylko log, raz na sesje, po HistoricalPrices.Apply).
    /// Sama zmiana cen siedzi w ArmsPricing (CostWeight, helm plytowy t4-6, DaysBowWar), HistoricalPrices (bron miotana bez mnoznika snopa, konie wedlug
    /// kategorii) i Recipes.BuildRecipe (kwit uprzezy z tej samej wagi); tu tylko rachunek do progow `sprawdz_logi --grupa etap8` (blok etap8_c):
    ///  - "HistoricalPrices: uprzaz z wagi prawdziwej N szt., helmy t4-6 N, luki N, oszczepy N, konie N" - ile sztuk obejmuje kazda regula i stan kluczy;
    ///  - "Cennik (E8/C): mediany ..." - mediany wartosci sztuk na handel bez unikatow: siodla (uprzaz z pancerzem konia do 10), kropierze (11-30), bardy
    ///    (ponad 30; "siodla" ROT z naczolkiem maja 38-44 i licza sie do bard t1-3), helmy t3-t6, luki t1-t6, bron miotana; kwit kuzni gracza wobec wartosci.
    /// Bez stanu kampanii (nic do zapisu i do Reset); wyjatek na jednej sztuce = licznik potkniec w linii, reszta liczona dalej.
    /// </summary>
    internal static class PriceList8
    {
        internal const int SaddleMaxArmor = 10, CaparisonMaxArmor = 30;

        private static string F(double v) { return v.ToString("0.#", CultureInfo.InvariantCulture); }

        /// <summary>"mediana (n szt., min-maks)"; pusta lista - "- (0 szt.)".</summary>
        private static string Med(List<int> l)
        {
            if (l == null || l.Count == 0) return "- (0 szt.)";
            l.Sort();
            double m = (l[(l.Count - 1) / 2] + l[l.Count / 2]) / 2.0;
            return F(m) + " (" + l.Count + " szt., " + l[0] + "-" + l[l.Count - 1] + ")";
        }

        private static string OnOff(bool on) { return on ? "[wl]" : "[wyl]"; }

        internal static void StartLine()
        {
            var s = Settings.Current;
            var mgr = MBObjectManager.Instance;
            if (s == null || mgr == null || Campaign.Current == null) return;
            int stumbles = 0;
            bool firstErr = true;
            int nHarness = 0, nHelm = 0, nBow = 0, nThrown = 0;
            var saddle = new List<int>(); var caparison = new List<int>(); var bardLow = new List<int>(); var bardMid = new List<int>(); var bardTop = new List<int>();
            var helm = new List<int>[7]; var bow = new List<int>[7];
            for (int i = 0; i < 7; i++) { helm[i] = new List<int>(); bow[i] = new List<int>(); }
            var thrown = new List<int>();
            int metalKit = 0, metalOver = 0, softKit = 0, softOver = 0;
            var metalEx = new List<string>(); var softEx = new List<string>();
            foreach (var it in mgr.GetObjectTypeList<ItemObject>())
            {
                try
                {
                    if (it == null || it.Value <= 0) continue;
                    var type = it.ItemType;
                    if (type != ItemObject.ItemTypeEnum.HorseHarness && type != ItemObject.ItemTypeEnum.HeadArmor
                        && type != ItemObject.ItemTypeEnum.Bow && type != ItemObject.ItemTypeEnum.Thrown) continue;
                    int t = Math.Max(1, Math.Min(6, (int)it.Tier + 1));
                    var mat = it.ArmorComponent != null ? it.ArmorComponent.MaterialType : ArmorComponent.ArmorMaterialTypes.None;
                    // ile sztuk obejmuje regula (takze unikaty i sztuki nie na handel - ich wartosc tez liczy sie z tej samej recepty)
                    if (type == ItemObject.ItemTypeEnum.HorseHarness) { if (ArmsPricing.CostWeight(it) < it.Weight - 0.001f) nHarness++; }
                    else if (type == ItemObject.ItemTypeEnum.HeadArmor) { if (s.HelmDaysPerKg > 0f && t >= 4 && mat == ArmorComponent.ArmorMaterialTypes.Plate) nHelm++; }
                    else if (type == ItemObject.ItemTypeEnum.Bow) { if (s.HistBowDaysNew) nBow++; }
                    else if (s.HistThrownOwnDays) nThrown++;
                    // mediany: sztuki na handel, bez unikatow (prestiz x4 to nie koszt wykonania)
                    if (it.NotMerchandise || ArmsPricing.IsUnique(it)) continue;
                    if (type == ItemObject.ItemTypeEnum.HeadArmor) { helm[t].Add(it.Value); continue; }
                    if (type == ItemObject.ItemTypeEnum.Bow) { bow[t].Add(it.Value); continue; }
                    if (type == ItemObject.ItemTypeEnum.Thrown) { thrown.Add(it.Value); continue; }
                    int armor = it.ArmorComponent != null ? it.ArmorComponent.BodyArmor : 0;
                    if (armor <= SaddleMaxArmor) saddle.Add(it.Value);
                    else if (armor <= CaparisonMaxArmor) caparison.Add(it.Value);
                    else if (t <= 3) bardLow.Add(it.Value);
                    else if (t <= 5) bardMid.Add(it.Value);
                    else bardTop.Add(it.Value);
                    // kwit kuzni gracza wobec wartosci (R7): uprzaz z plyty i kolczugi kuje sie z wagi (te sama CostWeight co wartosc); skora i tkanina - z punktow
                    // pancerza, w calych sztukach skory i plotna (poza zakresem E8/C - tylko licznik)
                    var r = Recipes.For(it);
                    float bill = 0f;
                    if (r.Parts != null)
                        foreach (var p in Recipes.ExactUnits(r))   // m96-receptury: przy przepisie z masy dokladne sztuki (kg / waga sztuki), nie sztuki do otwarcia
                            if (p.Key != null && p.Value > 0f) bill += p.Value * Math.Max(0, p.Key.Value);
                    bool metal = mat == ArmorComponent.ArmorMaterialTypes.Plate || mat == ArmorComponent.ArmorMaterialTypes.Chainmail;
                    if (metal)
                    {
                        metalKit++;
                        if (bill >= it.Value) { metalOver++; if (metalEx.Count < 5) metalEx.Add(it.StringId + " " + F(bill) + "/" + it.Value); }
                    }
                    else
                    {
                        softKit++;
                        if (bill >= it.Value) { softOver++; if (softEx.Count < 5) softEx.Add(it.StringId + " " + F(bill) + "/" + it.Value); }
                    }
                }
                catch (Exception e)
                {
                    stumbles++;
                    if (firstErr) { firstErr = false; Log.Error("PriceList8.StartLine (sztuka)", e); }
                }
            }
            bool hist = HistoricalPrices.On && HistoricalPrices.Applied;
            Log.Info("HistoricalPrices: uprzaz z wagi prawdziwej " + nHarness + " szt., helmy t4-6 " + nHelm + ", luki " + nBow + ", oszczepy " + nThrown + ", konie " + (hist ? HistoricalPrices.HorseCount : 0)
                     + " (E8/C) | ceny historyczne " + (hist ? "CZYNNE" : "WYLACZONE - reguly licza koszt wykucia (ArmsPricing), wartosci z gry bez zmian")
                     + " | uprzaz: " + (s.HarnessKgPerArmor > 0f ? "[wl] najwyzej " + s.HarnessKgPerArmor.ToString("0.00", CultureInfo.InvariantCulture) + " kg na punkt pancerza konia, nie mniej niz 3 kg" : "[wyl] waga gry")
                     + " | helm plytowy t4-6: " + (s.HelmDaysPerKg > 0f ? "[wl] " + s.HelmDaysPerKg.ToString("0.00", CultureInfo.InvariantCulture) + " dnia na kg + 0.5 dnia za tier ponad trzeci" : "[wyl] wzor napiersnika")
                     + " | luki " + OnOff(s.HistBowDaysNew) + " dni 1.5 / 1.5 / 1.5 / 1.7 / 2 / 2.5"
                     + " | bron miotana bez mnoznika snopa " + OnOff(s.HistThrownOwnDays)
                     + " | bron miotana jak pocisk - bez obijania (R3) " + OnOff(s.ThrownAsMissile)
                     + " | " + (hist ? HistoricalPrices.HorseLine() : "konie wedlug kategorii - bez cen historycznych nieczynne")
                     + " | potkniecia " + (stumbles + (hist ? HistoricalPrices.HorseStumbles : 0)) + ".");
            Log.Info("Cennik (E8/C): mediany wartosci sztuk na handel bez unikatow (mediana, liczba, zakres) - siodla " + Med(saddle) + ", kropierze " + Med(caparison)
                     + ", bardy t1-3 " + Med(bardLow) + ", bardy t4-5 " + Med(bardMid) + ", bardy t6 " + Med(bardTop)
                     + "; helm t3 " + Med(helm[3]) + ", helm t4 " + Med(helm[4]) + ", helm t5 " + Med(helm[5]) + ", helm t6 " + Med(helm[6])
                     + "; luk t1 " + Med(bow[1]) + ", luk t2 " + Med(bow[2]) + ", luk t3 " + Med(bow[3]) + ", luk t4 " + Med(bow[4]) + ", luk t5 " + Med(bow[5]) + ", luk t6 " + Med(bow[6])
                     + "; bron miotana " + Med(thrown)
                     + " | kwit kuzni gracza wobec wartosci (material po wartosci / wartosc sztuki): uprzaz z plyty i kolczugi - material >= wartosc " + metalOver + " z " + metalKit
                     + (metalEx.Count > 0 ? " [" + string.Join(", ", metalEx.ToArray()) + "]" : "")
                     + ", uprzaz ze skory i tkaniny (kwit z punktow pancerza w calych sztukach skory i plotna - poza E8/C) " + softOver + " z " + softKit
                     + (softEx.Count > 0 ? " [" + string.Join(", ", softEx.ToArray()) + "]" : "")
                     + " | siodlo = pancerz konia do " + SaddleMaxArmor + ", kropierz do " + CaparisonMaxArmor + ", barda powyzej | potkniecia " + stumbles + ".");
        }
    }
}
