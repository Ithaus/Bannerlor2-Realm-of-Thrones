using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// PACZKA 299 - SONDA KROKU CENY NA SZTUKE (Jeff 29.10: "za bardzo spada cena jak sprzedaje towar, trzeba bardziej wyplaszczyc zmiane kursu
    /// towaru gdy jest sprzedaz i zakup"). Regula siedzi w RawPrice.FactorPrefix (klucz PriceFlatterLotWalk); tu tylko pomiar, raz na dobe pomiaru
    /// (LogGate.Lines, zaraz po linii "Ceny surowcow"): kazde miasto x kazdy towar handlowy z popytem > 0.01 liczony dwa razy przy TYM SAMYM stanie rynku -
    /// krzywa gry (RawPrice.GameCurve = true) i krzywa 299. Czysty rachunek: zadnej zmiany polki, podazy, popytu ani zlota, zadnego stanu w zapisie.
    /// Wsie handluja na danych rynku miasta (Bound), zamki maja popyt 0 (indeks na podlodze 0.1) - liczone same miasta.
    /// Linia "Ceny na sztuke (299): dzien" czyta grupa ceny299 w tools/sprawdz_logi.py.
    /// Paczka 299b: "299" w linii to nasza krzywa (przy kluczu PriceSlowSupplyMemory wagi 0.14 x tempo); po 7 dobach gra idzie swoim tempem 0.15, nasza krzywa - RawPrice.Pace.
    /// </summary>
    internal static class PriceWalk299
    {
        private static Dictionary<ItemCategory, ItemObject> _sample;
        private static Campaign _sampleFor;
        private static bool _errCell, _errLine;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int LotCap = 20000;          // najwiekszy lot jednej doby popytu w sondzie (sztuk)
        private const int FlipCap = 100000;        // najdalsza sztuka szukana dla "spadek o 1 zl"

        /// <summary>Przedmiot probny kategorii: ten o StringId kategorii, inaczej najtanszy (raz na kampanie).</summary>
        internal static Dictionary<ItemCategory, ItemObject> Samples()
        {
            if (_sample != null && ReferenceEquals(_sampleFor, Campaign.Current)) return _sample;
            var d = new Dictionary<ItemCategory, ItemObject>();
            foreach (var it in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
            {
                var c = it != null ? it.ItemCategory : null;
                if (c == null || !c.IsTradeGood || it.Value <= 0) continue;
                ItemObject have;
                if (!d.TryGetValue(c, out have)) { d[c] = it; continue; }
                if (have.StringId == c.StringId) continue;
                if (it.StringId == c.StringId || it.Value < have.Value) d[c] = it;
            }
            _sample = d; _sampleFor = Campaign.Current; _errCell = false; _errLine = false;
            return d;
        }

        internal static float Bf(TradeItemPriceFactorModel m, bool game, ItemCategory c, float ins, float sup, float dem, bool sell, int val)
        {
            bool was = RawPrice.GameCurve;
            RawPrice.GameCurve = game;
            try { return m.GetBasePriceFactor(c, ins, sup, dem, sell, val); }
            finally { RawPrice.GameCurve = was; }
        }

        private static int Pr(TradeItemPriceFactorModel m, bool game, EquipmentElement el, PartyBase merchant, bool sell, float ins, float sup, float dem)
        {
            bool was = RawPrice.GameCurve;
            RawPrice.GameCurve = game;
            try { return m.GetPrice(el, MobileParty.MainParty, merchant, sell, ins, sup, dem); }
            finally { RawPrice.GameCurve = was; }
        }

        internal static float Q(List<float> l, double p)
        {
            if (l.Count == 0) return float.NaN;
            l.Sort();
            double x = p * (l.Count - 1); int i = (int)Math.Floor(x); int j = Math.Min(l.Count - 1, i + 1);
            return (float)(l[i] + (l[j] - l[i]) * (x - i));
        }

        private static string P(float v) { return float.IsNaN(v) ? "-" : (100f * v).ToString("0.0", Inv); }
        private static string R(float v, string f) { return float.IsNaN(v) ? "-" : v.ToString(f, Inv); }

        private sealed class Cls
        {
            internal readonly List<float> G10 = new List<float>(), G30 = new List<float>(), N10 = new List<float>(), N30 = new List<float>(), Q10 = new List<float>(), Q30 = new List<float>();
            internal int Cells;
            internal void Add(float g10, float g30, float n10, float n30)
            {
                Cells++; G10.Add(g10); G30.Add(g30); N10.Add(n10); N30.Add(n30);
                if (g10 >= 0.002f) Q10.Add(n10 / g10);
                if (g30 >= 0.005f) Q30.Add(n30 / g30);
            }
            internal string Text(string name)
            {
                return name + " (" + Cells + "): spadek sztuki 10 / 30 wobec 1 mediana gra " + P(Q(G10, 0.5)) + "% / " + P(Q(G30, 0.5)) + "% -> 299 " + P(Q(N10, 0.5)) + "% / "
                       + P(Q(N30, 0.5)) + "%, stosunek 10 szt. " + R(Q(Q10, 0.5), "0.000") + ", 30 szt. " + R(Q(Q30, 0.5), "0.000");
            }
        }

        /// <summary>Raz na dobe pomiaru, z ArmouryBehavior zaraz po RawPrice.Daily. Tylko odczyt.</summary>
        internal static void Daily()
        {
            int stumbles = 0;
            try
            {
                var s = Settings.Current;
                if (s == null || Campaign.Current == null || Campaign.Current.Models == null) return;
                var model = Campaign.Current.Models.TradeItemPriceFactorModel;
                if (model == null) return;
                var samples = Samples();
                int day = (int)CampaignTime.Now.ToDays - 1;
                var stocked = new Cls(); var thin = new Cls(); var empty = new Cls();
                var lotG = new List<float>(); var lotN = new List<float>();
                var lvEq = new List<float>(); var lvAll = new List<float>();
                var w7G = new List<float>(); var w7N = new List<float>();
                var loopG = new List<float>(); var loopN = new List<float>();
                var loopBG = new List<float>(); var loopBN = new List<float>();
                int cells = 0, towns = 0, inv = 0, inv0 = 0, eq = 0, eq0 = 0, ident = 0;
                // 299b: podaz po 7 dobach - gra w swoim tempie (0.15), nasza krzywa w tempie RawPrice.Pace (klucz PriceSlowSupplyMemory NIE - 0.15, jak w 299)
                double d7 = 1.0 - Math.Pow(0.85, 7), d7n = 1.0 - Math.Pow(1.0 - RawPrice.Pace, 7);
                var sellG = new float[31]; var sellN = new float[31];
                foreach (var t in Town.AllTowns)
                {
                    if (t == null || !t.IsTown || t.MarketData == null || t.Owner == null) continue;
                    towns++;
                    foreach (var kv in samples)
                    {
                        try
                        {
                            var cat = kv.Key; var it = kv.Value;
                            var d = t.MarketData.GetCategoryData(cat);
                            float D = d.Demand, S = d.Supply, I = Math.Max(0, d.InStoreValue);
                            if (!(D > 0.01f)) continue;
                            int w = Math.Max(1, HistoricalPrices.ShelfWorth(it)), val = it.Value;
                            cells++;
                            float f0g = Bf(model, true, cat, I, S, D, false, 0), f0n = Bf(model, false, cat, I, S, D, false, 0);
                            for (int n = 1; n <= 30; n++)
                            {
                                sellG[n] = Bf(model, true, cat, I + (n - 1) * (float)w, S, D, true, val);
                                sellN[n] = Bf(model, false, cat, I + (n - 1) * (float)w, S, D, true, val);
                            }
                            if (f0g == f0n && sellG[1] == sellN[1] && sellG[10] == sellN[10] && sellG[30] == sellN[30]) ident++;
                            float g10 = 1f - sellG[10] / sellG[1], g30 = 1f - sellG[30] / sellG[1];
                            float n10 = 1f - sellN[10] / sellN[1], n30 = 1f - sellN[30] / sellN[1];
                            double days = I / D;
                            if (I <= 0f) empty.Add(g10, g30, n10, n30);
                            else if (days >= 3.0) stocked.Add(g10, g30, n10, n30);
                            else thin.Add(g10, g30, n10, n30);
                            // lot jednej doby popytu
                            int l1 = (int)Math.Min(LotCap, Math.Max(1.0, Math.Round(D / w)));
                            if (l1 >= 2)
                            {
                                lotG.Add(1f - Bf(model, true, cat, I + (l1 - 1) * (float)w, S, D, true, val) / sellG[1]);
                                lotN.Add(1f - Bf(model, false, cat, I + (l1 - 1) * (float)w, S, D, true, val) / sellN[1]);
                            }
                            // poziom przy tym samym stanie
                            if (f0g > 0f)
                            {
                                lvAll.Add(f0n / f0g);
                                if (I > 0f && S > 0f && Math.Abs(I / S - 1f) <= 0.1f) lvEq.Add(f0n / f0g);
                            }
                            // po 7 dobach: podaz dochodzi do polki z 30 sprzedanymi sztukami (wygladzanie gry 15% dziennie)
                            float I30 = I + 30f * w;
                            float S7 = (float)(S + d7 * (I30 - S)), S7n = (float)(S + d7n * (I30 - S));
                            float f7g = Bf(model, true, cat, I30, S7, D, false, 0), f7n = Bf(model, false, cat, I30, S7n, D, false, 0);
                            w7G.Add(f7g / sellG[1]); w7N.Add(f7n / sellN[1]);
                            // petla opozniona: sprzedaj 30 teraz, odkup 30 po 7 dobach (brutto, bez kar i zuzycia) - polki >= 3 dob
                            if (I > 0f && days >= 3.0)
                            {
                                double sg = 0, sn = 0, bg = 0, bn = 0;
                                for (int n = 1; n <= 30; n++)
                                {
                                    sg += sellG[n]; sn += sellN[n];
                                    bg += Bf(model, true, cat, I30 - (n - 1) * (float)w, S7, D, false, 0);
                                    bn += Bf(model, false, cat, I30 - (n - 1) * (float)w, S7n, D, false, 0);
                                }
                                if (bg > 0) loopG.Add((float)(sg / bg - 1.0));
                                if (bn > 0) loopN.Add((float)(sn / bn - 1.0));
                            }
                            // recenzja 299: petla lustrzana - kup 30 teraz (polka I -> I - 30 w przy podazy S), sprzedaj 30 po 7 dobach, gdy podaz zeszla do polki
                            // (S7b = S + d7 x (I - 30 w - S)); zuzycie miasta jej pomaga (tu brutto bez zuzycia) - polki >= 3 dob z co najmniej 30 sztukami
                            if (days >= 3.0 && I >= 30f * w)
                            {
                                float Ib = I - 30f * w;
                                float S7b = (float)(S + d7 * (Ib - S)), S7bn = (float)(S + d7n * (Ib - S));
                                double kg = 0, kn = 0, og = 0, on2 = 0;
                                for (int n = 1; n <= 30; n++)
                                {
                                    kg += Bf(model, true, cat, I - (n - 1) * (float)w, S, D, false, 0);
                                    kn += Bf(model, false, cat, I - (n - 1) * (float)w, S, D, false, 0);
                                    og += Bf(model, true, cat, Ib + (n - 1) * (float)w, S7b, D, true, val);
                                    on2 += Bf(model, false, cat, Ib + (n - 1) * (float)w, S7bn, D, true, val);
                                }
                                if (kg > 0) loopBG.Add((float)(og / kg - 1.0));
                                if (kn > 0) loopBN.Add((float)(on2 / kn - 1.0));
                            }
                            // w miejscu: kupno sztuki przy polce I i odsprzedaz tej samej sztuki zaraz potem (polka I - w + w) - ceny gracza
                            if (I >= w && MobileParty.MainParty != null)
                            {
                                var el = new EquipmentElement(it);
                                int bN = Pr(model, false, el, t.Owner, false, I, S, D), rN = Pr(model, false, el, t.Owner, true, I - w, S, D);
                                int bG = Pr(model, true, el, t.Owner, false, I, S, D), rG = Pr(model, true, el, t.Owner, true, I - w, S, D);
                                if (bN < rN) inv++; else if (bN == rN) eq++;
                                if (bG < rG) inv0++; else if (bG == rG) eq0++;
                            }
                        }
                        catch (Exception e)
                        {
                            stumbles++;
                            if (!_errCell) { _errCell = true; Log.Error("PriceWalk299.Daily (komorka)", e); }
                        }
                    }
                }
                var ex = new StringBuilder();
                Example(ex, model, samples, "grain", FindTown("town_V2"), new[] { 1, 10, 233 }, ref stumbles);
                Example(ex, model, samples, "fish", PickTown(samples, "fish", false), new[] { 1, 30, 100, 200 }, ref stumbles);
                Example(ex, model, samples, "tools", PickTown(samples, "tools", false), new[] { 1, 10, 20 }, ref stumbles);
                Example(ex, model, samples, "cotton", PickTown(samples, "cotton", false), new[] { 1, 5, 10 }, ref stumbles);
                Example(ex, model, samples, "velvet", PickTown(samples, "velvet", false), new[] { 1, 2, 3 }, ref stumbles);
                Example(ex, model, samples, "wine", PickTown(samples, "wine", true), new[] { 1, 10, 30 }, ref stumbles);
                bool on = s.PriceFlatterLotWalk;
                // 299b: wagi czynne (RawPrice.ShelfW / SupplyW - przy kluczu PriceSlowSupplyMemory 0.14 x tempo); "299" w tej linii = nasza krzywa (299 albo 299b)
                Log.Info("Ceny na sztuke (299): dzien " + day + " | klucz " + (on ? "TAK" : "NIE") + ", wagi od razu " + RawPrice.ShelfW.ToString("0.0000", Inv)
                         + " / podaz " + RawPrice.SupplyW.ToString("0.0000", Inv)
                         + " | ocen " + cells + " (miasta " + towns + "; wsie wedlug miasta, zamki popyt 0)"
                         + " | " + stocked.Text("polka >= 3 dob") + " | " + thin.Text("polka chuda") + " | " + empty.Text("polka pusta")
                         + " | lot 1 doby popytu (" + lotG.Count + "): spadek mediana gra " + P(Q(lotG, 0.5)) + "% -> 299 " + P(Q(lotN, 0.5)) + "% (p90 gra " + P(Q(lotG, 0.9)) + "% -> 299 " + P(Q(lotN, 0.9)) + "%)"
                         + " | poziom 299/gra ten sam stan: rownowaga (" + lvEq.Count + ") mediana " + R(Q(lvEq, 0.5), "0.000") + ", wszystkie mediana " + R(Q(lvAll, 0.5), "0.000")
                         + " p10 " + R(Q(lvAll, 0.1), "0.000") + " p90 " + R(Q(lvAll, 0.9), "0.000")
                         + " | po 7 dobach (30 szt.) indeks/indeks 1. sztuki mediana gra " + R(Q(w7G, 0.5), "0.000") + " -> 299 " + R(Q(w7N, 0.5), "0.000")
                         + " | w miejscu: kupno < odsprzedaz " + inv + " (gra " + inv0 + "), rowne " + eq + " (gra " + eq0 + ") z " + cells
                         + " | petla opozniona (30 szt., 7 dob, brutto) polka >= 3 dob (" + loopG.Count + ") mediana gra " + P(Q(loopG, 0.5)) + "% -> 299 " + P(Q(loopN, 0.5))
                         + "%, p90 gra " + P(Q(loopG, 0.9)) + "% -> 299 " + P(Q(loopN, 0.9)) + "%"
                         + " | petla kup teraz, sprzedaj po 7 dobach (30 szt., brutto) polka >= 3 dob (" + loopBG.Count + ") mediana gra " + P(Q(loopBG, 0.5)) + "% -> 299 "
                         + P(Q(loopBN, 0.5)) + "%, p90 gra " + P(Q(loopBG, 0.9)) + "% -> 299 " + P(Q(loopBN, 0.9)) + "%"
                         + " | przyklady (gracz, zl za sztuke, gra -> 299): " + ex
                         + " | identyczne pary gra = 299 " + ident + " z " + cells
                         + " | potkniecia " + stumbles + ".");
            }
            catch (Exception e)
            {
                if (!_errLine) { _errLine = true; Log.Error("PriceWalk299.Daily", e); }
            }
        }

        private static Town FindTown(string id)
        {
            try { var st = Settlement.Find(id); return st != null ? st.Town : null; } catch { return null; }
        }

        private static ItemCategory CatOf(Dictionary<ItemCategory, ItemObject> samples, string id)
        {
            foreach (var kv in samples) if (kv.Key.StringId == id) return kv.Key;
            return null;
        }

        /// <summary>Miasto przykladu: pusta polka z najwiekszym popytem, inaczej najmniej dob polki; wine - polka najblizej 7 dob.</summary>
        private static Town PickTown(Dictionary<ItemCategory, ItemObject> samples, string id, bool week)
        {
            var cat = CatOf(samples, id);
            if (cat == null) return null;
            Town best = null; double key = double.MaxValue; bool bestEmpty = false;
            foreach (var t in Town.AllTowns)
            {
                if (t == null || !t.IsTown || t.MarketData == null) continue;
                var d = t.MarketData.GetCategoryData(cat);
                if (!(d.Demand > 0.01f)) continue;
                double days = Math.Max(0, d.InStoreValue) / (double)d.Demand;
                if (week)
                {
                    if (d.InStoreValue <= 0) continue;
                    double k = Math.Abs(days - 7.0);
                    if (k < key) { key = k; best = t; }
                    continue;
                }
                bool isEmpty = d.InStoreValue <= 0;
                if (isEmpty)
                {
                    if (!bestEmpty || -d.Demand < key) { bestEmpty = true; key = -d.Demand; best = t; }
                }
                else if (!bestEmpty && days < key) { key = days; best = t; }
            }
            return best;
        }

        /// <summary>Przyklad dla gracza: cena sprzedazy sztuk z listy (gra -> 299), spadek indeksu do ostatniej, pierwsza sztuka tansza o 1 zl od pierwszej
        /// i liczba sztuk, ktore 299 placi taniej niz gra, choc polka po sprzedazy ma co najmniej tyle co podaz (tam 299 nie moze byc tansze).</summary>
        private static void Example(StringBuilder sb, TradeItemPriceFactorModel model, Dictionary<ItemCategory, ItemObject> samples, string id, Town t, int[] units, ref int stumbles)
        {
            if (sb.Length > 0) sb.Append(" ; ");
            try
            {
                var cat = CatOf(samples, id);
                if (cat == null || t == null || t.MarketData == null || t.Owner == null || MobileParty.MainParty == null) { sb.Append(id).Append("@- brak"); return; }
                var it = samples[cat];
                var d = t.MarketData.GetCategoryData(cat);
                float D = d.Demand, S = d.Supply, I = Math.Max(0, d.InStoreValue);
                int w = Math.Max(1, HistoricalPrices.ShelfWorth(it)), val = it.Value;
                var el = new EquipmentElement(it);
                var merchant = t.Owner;
                Func<bool, int, int> sell = (g, n) => Pr(model, g, el, merchant, true, I + (n - 1) * (float)w, S, D);
                var pg = new List<string>(); var pn = new List<string>();
                foreach (int n in units) { pg.Add(sell(true, n).ToString(Inv)); pn.Add(sell(false, n).ToString(Inv)); }
                int last = units[units.Length - 1];
                float bg1 = Bf(model, true, cat, I, S, D, true, val), bn1 = Bf(model, false, cat, I, S, D, true, val);
                float bgL = Bf(model, true, cat, I + (last - 1) * (float)w, S, D, true, val), bnL = Bf(model, false, cat, I + (last - 1) * (float)w, S, D, true, val);
                int under = 0;
                for (int n = 1; n <= last; n++)
                    if (I + n * (float)w >= S && sell(false, n) < sell(true, n)) under++;
                string name = t.Name != null ? t.Name.ToString() : t.StringId;
                sb.Append(id).Append('@').Append(name).Append(" (").Append(t.Settlement != null ? t.Settlement.StringId : "?").Append(", polka ")
                  .Append((D > 0f ? I / D : 0f).ToString("0.0", Inv)).Append(" dob, polka/podaz ").Append(S > 0f ? (I / S).ToString("0.00", Inv) : "-").Append(") sztuki ").Append(string.Join("/", Array.ConvertAll(units, x => x.ToString(Inv))))
                  .Append(": ").Append(string.Join("/", pg.ToArray())).Append(" -> ").Append(string.Join("/", pn.ToArray()))
                  .Append(", indeks do ").Append(last).Append(". sztuki gra ").Append(bg1 > 0f ? (100f * (bgL / bg1 - 1f)).ToString("0.0", Inv) : "0.0")
                  .Append("% -> 299 ").Append(bn1 > 0f ? (100f * (bnL / bn1 - 1f)).ToString("0.0", Inv) : "0.0")
                  .Append("%, spadek o 1 zl od sztuki ").Append(Flip(sell, true)).Append(" -> ").Append(Flip(sell, false))
                  .Append(", pod gra przy polce >= podazy ").Append(under);
            }
            catch (Exception e)
            {
                stumbles++;
                if (!_errCell) { _errCell = true; Log.Error("PriceWalk299.Daily (przyklad " + id + ")", e); }
                sb.Append(id).Append("@- brak");
            }
        }

        /// <summary>Pierwsza sztuka, za ktora gracz dostaje mniej niz za pierwsza (cena sprzedazy nie rosnie z polka - polowienie); "-" gdy do FlipCap nie spada.</summary>
        private static string Flip(Func<bool, int, int> sell, bool game)
        {
            int p1 = sell(game, 1);
            if (sell(game, FlipCap) >= p1) return "-";
            int lo = 1, hi = FlipCap;          // sell(lo) == p1, sell(hi) < p1
            while (hi - lo > 1) { int mid = lo + (hi - lo) / 2; if (sell(game, mid) >= p1) lo = mid; else hi = mid; }
            return hi.ToString(Inv);
        }
    }
}
