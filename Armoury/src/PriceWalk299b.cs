using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// PACZKA 299b - SONDA PAMIECI PODAZY I CENY SPRZETU NA SZTUKE (Jeff 29.10 ok. 20:20: "tak dla wszysatkich - 15% dziennie za duzo, dajmy 5-8% dziennie").
    /// Regula: RawPrice (tempo pamieci podazy rynku, wagi 0.14 x tempo) i ShelfMemory (pamiec polki sprzetu). Tu tylko pomiar, raz na dobe pomiaru (LogGate.Lines, zaraz po
    /// linii "Ceny na sztuke (299)"): samotest czynnego modelu ekonomii, liczniki wygladzania, stan pamieci polek, krok ceny sprzetu 299 (bez pamieci - krzywa gry
    /// RawPrice.GameCurve) wobec 299b przy TYM SAMYM stanie, poziom przy M = S, kupno i odsprzedaz w jednej wizycie, petla opozniona gracza na sprzecie i towarach, przyklady
    /// cen gracza w Riverrun. Czysty rachunek: zadnej zmiany polki, pamieci, podazy, popytu ani zlota. Linie czyta grupa ceny299 w tools/sprawdz_logi.py (C299-12..18).
    /// </summary>
    internal static class PriceWalk299b
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static bool _errCell, _errLine;
        private static readonly int[] Units = { 1, 5, 20 };
        private static readonly ItemObject.ItemTypeEnum[] Kinds =
        {
            ItemObject.ItemTypeEnum.HeadArmor, ItemObject.ItemTypeEnum.BodyArmor, ItemObject.ItemTypeEnum.LegArmor, ItemObject.ItemTypeEnum.HandArmor, ItemObject.ItemTypeEnum.Cape,
            ItemObject.ItemTypeEnum.OneHandedWeapon, ItemObject.ItemTypeEnum.TwoHandedWeapon, ItemObject.ItemTypeEnum.Polearm, ItemObject.ItemTypeEnum.Shield, ItemObject.ItemTypeEnum.Bow,
            ItemObject.ItemTypeEnum.Crossbow, ItemObject.ItemTypeEnum.Arrows, ItemObject.ItemTypeEnum.Bolts, ItemObject.ItemTypeEnum.Thrown, ItemObject.ItemTypeEnum.Horse,
            ItemObject.ItemTypeEnum.HorseHarness
        };

        private static string P(float v) { return float.IsNaN(v) ? "-" : (100f * v).ToString("0.0", Inv); }
        private static string R(float v, string f) { return float.IsNaN(v) ? "-" : v.ToString(f, Inv); }
        private static float Q(List<float> l, double p) { return PriceWalk299.Q(l, p); }

        private static void Cell(ref int stumbles, string where, Exception e)
        {
            stumbles++;
            if (!_errCell) { _errCell = true; Log.Error("PriceWalk299b.Daily (" + where + ")", e); }
        }

        /// <summary>Raz na dobe pomiaru, z ArmouryBehavior zaraz po PriceWalk299.Daily. Tylko odczyt.</summary>
        internal static void Daily()
        {
            int stumbles = 0;
            try
            {
                var s = Settings.Current;
                if (s == null || Campaign.Current == null || Campaign.Current.Models == null) return;
                int day = (int)CampaignTime.Now.ToDays - 1;
                bool slow = s.PriceFlatterLotWalk && s.PriceSlowSupplyMemory, all = s.PriceFlatterLotWalk && s.PriceFlatterAllItems;
                double pace = RawPrice.Pace;
                float p = (float)pace;
                float e = MBMath.ClampFloat(s.SupplyDemandElasticity, 0.05f, 2f), lo = MBMath.ClampFloat(s.SupplyDemandMinFactor, 0.01f, 1f), hi = Math.Max(1f, s.SupplyDemandMaxFactor);

                // 1. samotest czynnego modelu ekonomii: (miasto, kategoria, dzis 100, popyt 100, stara podaz 0, stary popyt 0)
                var econ = Campaign.Current.Models.SettlementEconomyModel;
                float tSup = float.NaN, tDem = float.NaN;
                Town t0 = null;
                foreach (var t in Town.AllTowns) if (t != null && t.IsTown && t.MarketData != null) { t0 = t; break; }
                ItemCategory c0 = null;
                foreach (var kv in PriceWalk299.Samples()) { c0 = kv.Key; if (kv.Key.StringId == "grain") break; }
                try
                {
                    if (econ != null && t0 != null && c0 != null)
                    {
                        var r = econ.GetSupplyDemandForCategory(t0, c0, 100f, 100f, 0f, 0f);
                        tSup = r.Item1; tDem = r.Item2;
                    }
                }
                catch (Exception ex) { Cell(ref stumbles, "samotest", ex); }
                double wantSup = RawPrice.MemoryLive && pace < RawPrice.GameDayW - 1e-6 ? Math.Max(0.1, 100.0 * pace) : 15.0;
                int applied, gameN, before, pst;
                RawPrice.PaceCounts(out applied, out gameN, out before, out pst);
                stumbles += pst;
                int towns = 0; foreach (var t in Town.AllTowns) if (t != null && t.IsTown) towns++;
                int cats = 0; try { cats = ItemCategories.All.Count; } catch { }

                // 2. pamiec polek (stan po dzisiejszym ShelfMemory.Daily)
                int saveLen = 0;
                try { saveLen = ShelfMemory.On ? ShelfMemory.Export().Length : 0; } catch (Exception ex) { Cell(ref stumbles, "zapis", ex); }

                // 3. koszyki sprzetu: krok ceny 299 (bez pamieci) wobec 299b przy tym samym stanie
                var d5a = new List<float>(); var d20a = new List<float>(); var d5b = new List<float>(); var d20b = new List<float>(); var q20 = new List<float>(); var first = new List<float>();
                var loopA = new List<float>(); var loopB = new List<float>();
                int cellsEq = 0, levelK = 0, levelAll = 0, levelBad = 0, inPlace = 0, inPlaceBad = 0, profA = 0, profB = 0;
                int n = ShelfMemory.Slots; var counts = new int[n]; var sample = new ItemObject[n];
                double keep21 = Math.Pow(1.0 - pace, 21);
                foreach (var st in Settlement.All)
                {
                    if (st == null || (!st.IsTown && !st.IsCastle) || st.ItemRoster == null || st.Town == null) continue;
                    try
                    {
                        ShelfMemory.CountInto(st.ItemRoster, counts);
                        Array.Clear(sample, 0, sample.Length);
                        var shelf = st.ItemRoster;
                        for (int i = 0; i < shelf.Count; i++)
                        {
                            var el = shelf.GetElementCopyAtIndex(i);
                            var it = el.EquipmentElement.Item;
                            if (el.Amount <= 0 || it == null || !SupplyDemand.Equipmentish(it)) continue;
                            int k = (int)it.ItemType * 8 + Math.Max(1, Math.Min(6, (int)it.Tier + 1));
                            if (k >= 0 && k < n && sample[k] == null) sample[k] = it;
                        }
                    }
                    catch (Exception ex) { Cell(ref stumbles, "polka", ex); continue; }
                    foreach (var ty in Kinds)
                    {
                        for (int tier = 1; tier <= 6; tier++)
                        {
                            try
                            {
                                int slot = (int)ty * 8 + tier;
                                if (slot < 0 || slot >= n) continue;
                                float D = SupplyDemand.Demand(st, ty, tier);
                                if (!(D >= 1f)) continue;
                                float S = counts[slot];
                                float M = ShelfMemory.M(st, ty, tier);
                                if (float.IsNaN(M)) M = S;
                                cellsEq++;
                                float a1 = (float)Math.Pow((D + 1f) / (S + 2f), e), a5 = (float)Math.Pow((D + 1f) / (S + 6f), e), a20 = (float)Math.Pow((D + 1f) / (S + 21f), e);
                                float b1 = (float)Math.Pow((D + 1f) / (ShelfMemory.Eff(st, ty, tier, S + 1f) + 1f), e);
                                float b5 = (float)Math.Pow((D + 1f) / (ShelfMemory.Eff(st, ty, tier, S + 5f) + 1f), e);
                                float b20 = (float)Math.Pow((D + 1f) / (ShelfMemory.Eff(st, ty, tier, S + 20f) + 1f), e);
                                float da5 = 1f - a5 / a1, da20 = 1f - a20 / a1, db5 = 1f - b5 / b1, db20 = 1f - b20 / b1;
                                d5a.Add(da5); d20a.Add(da20); d5b.Add(db5); d20b.Add(db20);
                                if (da20 >= 0.005f) q20.Add(db20 / da20);
                                first.Add(b1 / a1);
                                // poziom przy M = S: kupno (polka bez + 1) przez pamiec = bez pamieci
                                if (Math.Abs(M - S) < 0.005f)
                                {
                                    levelAll++;
                                    if (Math.Abs(ShelfMemory.Eff(st, ty, tier, S) - S) <= 1e-3f) levelK++; else levelBad++;
                                }
                                var it = sample[slot];
                                if (it == null || S < 1f) continue;
                                // w miejscu: kupno sztuki (polka S), potem odsprzedaz tej samej (polka S - 1 + 1) - prawdziwy Factor z sztuka sondy
                                float dd; int sh;
                                float fb = SupplyDemand.FactorProbe(st, it, false, 0, false, out dd, out sh);
                                float fs = SupplyDemand.FactorProbe(st, it, true, -1, false, out dd, out sh);
                                inPlace++;
                                if (fs > fb + 1e-5f) inPlaceBad++;
                                // petla opozniona gracza (miasta, koszyki >= 3 szt.): sprzedaj 20 teraz, odkup 20 po 21 dobach, polka bez odplywu (S + 20), z marza i kara (ceny gracza)
                                if (!st.IsTown || S < 3f) continue;
                                var el0 = new EquipmentElement(it);
                                int pb, ps;
                                if (!SupplyDemand.ProbeBuySell(st, el0, true, out pb, out ps) || pb <= 0) continue;
                                float dA; int sA;
                                float fbA = SupplyDemand.FactorProbe(st, it, false, 0, true, out dA, out sA), fsA = SupplyDemand.FactorProbe(st, it, true, 0, true, out dA, out sA);
                                if (!(fbA > 0f) || !(fsA > 0f)) continue;
                                double rho = (ps / (double)fsA) / (pb / (double)fbA);
                                double sa = 0, ba = 0, sb2 = 0, bb = 0;
                                float M21 = (float)((S + 20f) + (M - (S + 20f)) * keep21);
                                for (int k = 1; k <= 20; k++)
                                {
                                    float sDir = S + k, bDir = S + 21 - k;
                                    sa += MBMath.ClampFloat((float)Math.Pow((dA + 1f) / (sDir + 1f), e), lo, hi);
                                    ba += MBMath.ClampFloat((float)Math.Pow((dA + 1f) / (bDir + 1f), e), lo, hi);
                                    float sE = all ? ShelfMemory.Blend(p, sDir, M) : sDir, bE = all ? ShelfMemory.Blend(p, bDir, M21) : bDir;
                                    sb2 += MBMath.ClampFloat((float)Math.Pow((dA + 1f) / (sE + 1f), e), lo, hi);
                                    bb += MBMath.ClampFloat((float)Math.Pow((dA + 1f) / (bE + 1f), e), lo, hi);
                                }
                                if (ba > 0 && bb > 0)
                                {
                                    float ga = (float)(rho * sa / ba - 1.0), gb = (float)(rho * sb2 / bb - 1.0);
                                    loopA.Add(ga); loopB.Add(gb);
                                    if (ga > 0f) profA++;
                                    if (gb > 0f) profB++;
                                }
                            }
                            catch (Exception ex) { Cell(ref stumbles, "koszyk", ex); }
                        }
                    }
                }

                // 4. towary handlowe: petla 30 szt. (sprzedaj teraz, odkup po 21 dobach, brutto) - gra w tempie 0.15, nasza krzywa w tempie Pace; polki >= 3 dob
                var goodsG = new List<float>(); var goodsN = new List<float>();
                var model = Campaign.Current.Models.TradeItemPriceFactorModel;
                if (model != null)
                {
                    double d21g = 1.0 - Math.Pow(0.85, 21), d21n = 1.0 - keep21;
                    foreach (var t in Town.AllTowns)
                    {
                        if (t == null || !t.IsTown || t.MarketData == null) continue;
                        foreach (var kv in PriceWalk299.Samples())
                        {
                            try
                            {
                                var cat = kv.Key; var it = kv.Value;
                                var d = t.MarketData.GetCategoryData(cat);
                                float D = d.Demand, S = d.Supply, I = Math.Max(0, d.InStoreValue);
                                if (!(D > 0.01f) || I <= 0f || I / D < 3.0) continue;
                                int w = Math.Max(1, HistoricalPrices.ShelfWorth(it)), val = it.Value;
                                float I30 = I + 30f * w;
                                float Sg = (float)(S + d21g * (I30 - S)), Sn = (float)(S + d21n * (I30 - S));
                                double sg = 0, bg = 0, sn = 0, bn = 0;
                                for (int k = 1; k <= 30; k++)
                                {
                                    sg += PriceWalk299.Bf(model, true, cat, I + (k - 1) * (float)w, S, D, true, val);
                                    sn += PriceWalk299.Bf(model, false, cat, I + (k - 1) * (float)w, S, D, true, val);
                                    bg += PriceWalk299.Bf(model, true, cat, I30 - (k - 1) * (float)w, Sg, D, false, 0);
                                    bn += PriceWalk299.Bf(model, false, cat, I30 - (k - 1) * (float)w, Sn, D, false, 0);
                                }
                                if (bg > 0 && bn > 0) { goodsG.Add((float)(sg / bg - 1.0)); goodsN.Add((float)(sn / bn - 1.0)); }
                            }
                            catch (Exception ex) { Cell(ref stumbles, "towar", ex); }
                        }
                    }
                }

                // 5. przyklady gracza w Riverrun (town_V2): miecz t3, zbroja t3, kon t5 - zl za 1. / 5. / 20. sprzedana sztuke, 299 -> 299b
                var ex2 = new StringBuilder();
                Settlement rr = null; try { rr = Settlement.Find("town_V2"); } catch { }
                Example(ex2, rr, "miecz", ItemObject.ItemTypeEnum.OneHandedWeapon, 3, ref stumbles);
                Example(ex2, rr, "zbroja", ItemObject.ItemTypeEnum.BodyArmor, 3, ref stumbles);
                Example(ex2, rr, "kon", ItemObject.ItemTypeEnum.Horse, 5, ref stumbles);

                stumbles += ShelfMemory.TakeStumbles();
                Log.Info("Ceny sprzetu i pamiec podazy (299b): dzien " + day
                         + " | klucze " + (slow ? "TAK" : "NIE") + "/" + (all ? "TAK" : "NIE") + ", tempo " + pace.ToString("0.000", Inv) + "/d (gra 0.150)"
                         + " | towary: wagi od razu " + RawPrice.ShelfW.ToString("0.0000", Inv) + " / podaz " + RawPrice.SupplyW.ToString("0.0000", Inv)
                         + ", model ekonomii " + (econ != null ? econ.GetType().Name : "?") + ": samotest podaz 100 -> " + R(tSup, "0.00") + " (oczekiwane " + wantSup.ToString("0.00", Inv)
                         + "), popyt 100 -> " + R(tDem, "0.00") + " (oczekiwane 15.00), wygladzen w ostatniej dobie z tempem 299b " + applied + ", z tempem gry " + gameN
                         + " (miasta " + towns + " x kategorie " + cats + "), przed startem sesji " + before
                         + " | pamiec polek sprzetu: osad " + ShelfMemory.DPlaces + ", koszykow " + ShelfMemory.DBuckets + ", |M-S| >= 1: " + ShelfMemory.DOff1
                         + " (srednio " + ShelfMemory.DAvg.ToString("0.00", Inv) + ", max " + ShelfMemory.DMax.ToString("0.00", Inv) + "), zapis " + saveLen + " znakow, z zapisu " + ShelfMemory.LoadReport()
                         + " | sprzedaz sztuki 5 / 20 wobec 1 (mnoznik polki, koszyki z popytem >= 1 bez granic, " + cellsEq + "): mediana 299 " + P(Q(d5a, 0.5)) + "% / " + P(Q(d20a, 0.5))
                         + "% -> 299b " + P(Q(d5b, 0.5)) + "% / " + P(Q(d20b, 0.5)) + "%, stosunek 20 szt. " + R(Q(q20, 0.5), "0.000")
                         + " | pierwsza sprzedana sztuka 299b/299 mediana " + R(Q(first, 0.5), "0.000")
                         + " | poziom przy M = S: kupno 299b = 299 w " + levelK + " z " + levelAll + " (rozjazdow " + levelBad + ")"
                         + " | w miejscu: sprzedaz po kupnie drozej niz kupno " + inPlaceBad + " z " + inPlace
                         + " | petla opozniona sprzet (gracz, sprzedaj 20, odkup 20 po 21 dobach, bez odplywu, z marza i kara) koszyki >= 3 szt. (" + loopA.Count + "): mediana 299 "
                         + P(Q(loopA, 0.5)) + "% -> 299b " + P(Q(loopB, 0.5)) + "%, p90 299 " + P(Q(loopA, 0.9)) + "% -> 299b " + P(Q(loopB, 0.9)) + "%, zyskownych " + profA + " -> " + profB
                         + " | towary: petla 30 szt. po 21 dobach (" + goodsG.Count + ") mediana gra " + P(Q(goodsG, 0.5)) + "% -> 299b " + P(Q(goodsN, 0.5)) + "%"
                         + " | przyklady (gracz, zl za sztuke 1/5/20, 299 -> 299b): " + ex2
                         + " | potkniecia " + stumbles + ".");
            }
            catch (Exception ex)
            {
                if (!_errLine) { _errLine = true; Log.Error("PriceWalk299b.Daily", ex); }
            }
        }

        /// <summary>Przedmiot przykladu: pierwszy z polki osady w koszyku, inaczej pierwszy w grze tego typu i tieru.</summary>
        private static ItemObject Pick(Settlement st, ItemObject.ItemTypeEnum ty, int tier)
        {
            var shelf = st.ItemRoster;
            for (int i = 0; i < shelf.Count; i++)
            {
                var el = shelf.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (el.Amount > 0 && it != null && it.ItemType == ty && Math.Max(1, Math.Min(6, (int)it.Tier + 1)) == tier) return it;
            }
            foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                if (it != null && it.ItemType == ty && it.Value > 0 && Math.Max(1, Math.Min(6, (int)it.Tier + 1)) == tier) return it;
            return null;
        }

        private static void Example(StringBuilder sb, Settlement st, string name, ItemObject.ItemTypeEnum ty, int tier, ref int stumbles)
        {
            if (sb.Length > 0) sb.Append(" ; ");
            try
            {
                if (st == null || st.ItemRoster == null || st.Town == null) { sb.Append(name).Append(" t").Append(tier).Append("@- brak"); return; }
                var it = Pick(st, ty, tier);
                if (it == null) { sb.Append(name).Append(" t").Append(tier).Append("@- brak"); return; }
                var el = new EquipmentElement(it);
                var pa = new int[Units.Length]; var pb = new int[Units.Length];
                if (!SupplyDemand.ProbeEquipWalk(st, el, Units, true, pa) || !SupplyDemand.ProbeEquipWalk(st, el, Units, false, pb)) { sb.Append(name).Append(" t").Append(tier).Append("@- brak"); return; }
                var counts = new int[ShelfMemory.Slots];
                ShelfMemory.CountInto(st.ItemRoster, counts);
                int slot = (int)ty * 8 + tier;
                float S = slot < counts.Length ? counts[slot] : 0f, M = ShelfMemory.M(st, ty, tier);
                float D = SupplyDemand.Demand(st, ty, tier);
                sb.Append(name).Append(" t").Append(tier).Append('@').Append(st.Name != null ? st.Name.ToString() : st.StringId).Append(" (").Append(it.StringId).Append(", wartosc ").Append(it.Value)
                  .Append(", polka ").Append(S.ToString("0", Inv)).Append(", popyt ").Append(D.ToString("0.0", Inv)).Append(", pamiec ").Append(float.IsNaN(M) ? "-" : M.ToString("0.0", Inv)).Append("): ")
                  .Append(string.Join("/", Array.ConvertAll(pa, x => x.ToString(Inv)))).Append(" -> ").Append(string.Join("/", Array.ConvertAll(pb, x => x.ToString(Inv))));
            }
            catch (Exception e)
            {
                Cell(ref stumbles, "przyklad " + name, e);
                sb.Append(name).Append(" t").Append(tier).Append("@- brak");
            }
        }
    }
}
