using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// ZC5 - PRZETWORY MIAST (projekt ZYCIE CODZIENNE rozdz. 13-14 "ZC5", KS rozdz. 11.2 "Przetwory: pary TownCrafts"): czynne tylko przy zastosowanej kalibracji startu
    /// (lambda koszyka > 0) i kluczu Start Calibration Town Crafts. Rzemieslnicy miasta robia z towaru z polki SWOJEGO miasta: chleb i placek z ziarna (1 kg na kg), piwo
    /// z ziarna (0.4 kg na litr), wino z winogron (1.3 kg na litr), oliwe z oliwek (5 kg na kg), miod pitny z miodu (0.35 kg na litr), garnki z gliny (1.2 kg na kg),
    /// narzedzia z zelaza (1.2 kg na kg - recenzja 16.10, projekt KS 11.2), mieso z uboju zywca (krowy, swinie, owce: 2 kg zywca na kg miesa, skory 0.08 / 0.05 / 0.10 kg
    /// na kg zywca - recenzja 16.10: dotad zywiec z KS czekal na rzeznie BK, ktorych moc nie rosnie, i mieso zostawalo w polu jako owce i skory),
    /// a w Lys, Volantis, Myr, Tyrosh i Pentos garum z ryb i soli (2 kg ryb i 0.4 kg soli na kg) - te same proporcje co popyt Planu (wsady), port 1:1 tabel modelu.
    ///  - RECE: rzemieslnicy miasta z ksiegi stanow x 0.55 (praca stanu w miescie) / sigma x 0.5 (polowa rak cechow - reszta sukno, kuznie, warsztaty); praca na sztuke =
    ///    kg sztuki / wyrob na rzemieslnika na dobe (PROC modelu: chleb 60 kg, placek 40, piwo 150 l, wino 80, oliwa 15, miod pitny 50, garnki 15, narzedzia 1.5,
    ///    mieso 30, garum 20). Rece dostaja pary wedlug zysku na roboczodzien przy cenach miasta (recenzja 16.10: dotad stala kolejnosc - chleb pierwszy, wino i oliwa ostatnie).
    ///  - REGULA (jak rzemioslo miasta 148): przerob tylko wtedy, gdy wyrob (z ubocznym - skory) placi wsad + prace (dniowka miasta) + marze mistrza (Smith Profit
    ///    Percent), krokami z cena liczona od nowa; NA ZBYT: wyrob najwyzej do tygodnia tego, co miasto kupuje (popyt koszyka miasta i wsi x lambda + wczorajsza
    ///    sprzedaz x (1 - lambda)); wsad najwyzej ponad 2 dni bezposredniej potrzeby ludu na ten towar (ziarno, ryby, miod, winogrona i oliwki jedzone wprost) i ponad 5 sztuk.
    ///  - Bez zlota w przerobie: towar z polki na polke tego samego miasta (kasa miasta = kupcy i rzemieslnicy razem), jak rzemioslo 148; ramka ksiegi towarow rzemiosla
    ///    miasta. PLACE: wyrob idzie przez zdarzenie gry OnItemProduced -> TownFolk.OnItemProduced (wartosc wyrobu do f.ProdV) -> doplata kiesy ludu do 35% wartosci
    ///    wyrobow z kasy miasta (TownFolkCraftWageShare, W70/R30) - ta sama droga co warsztaty (recenzja 16.10: praca ZC5 JEST placona tym mechanizmem, bez drugiego przelewu).
    /// Pod kalibracja tkanie i garbowanie rzemiosla 148 bierze wsad wedlug tabel KS (len 1.25, welna 1.45, skory 2.4 kg na kg wyrobu) - TextileRatio.
    /// </summary>
    internal static partial class StartCalibration
    {
        private sealed class KPair
        {
            public string Out, Name; public string[] In; public double[] KgIn; public double KgDay; public bool Garum;
            public ItemObject OutIt; public ItemObject[] InIt; public double[] Ratio; public double Labor; public int ZOut; public int[] ZIn;
            public int Live = -1;                    // uboj: 0 krowy, 1 swinie, 2 owce (wsad zywca; popyt ceny zywca - Zc4Derived)
            public ItemObject Side; public double SideRatio;   // wyrob uboczny (skory) na sztuke wyrobu
        }
        private static List<KPair> _kp;
        private static readonly Dictionary<Town, double[]> _kAcc = new Dictionary<Town, double[]>();
        private static int[] _kMade, _kTowns, _kNoProfit, _kNoInput, _kFull, _kNoHands; private static double[] _kTaken; private static long[] _kSide;
        private static int _kDayTowns, _kStum; private static double _kHands, _kUsed; private static long _kTicks;
        internal static bool CraftsOn { get { var s = Settings.Current; return s == null || s.StartCalibrationTownCrafts; } }

        private static void CraftsReset() { _kp = null; _kAcc.Clear(); CraftsZero(); _kTicks = 0; }

        private static void CraftsZero()
        {
            int n = _kp != null ? _kp.Count : 0;
            _kMade = new int[n]; _kTowns = new int[n]; _kNoProfit = new int[n]; _kNoInput = new int[n]; _kFull = new int[n]; _kNoHands = new int[n]; _kTaken = new double[n]; _kSide = new long[n];
            _kDayTowns = 0; _kHands = _kUsed = 0;
        }

        private static double KgOf(ItemObject it) { return it != null && it.Weight > 0.05f ? it.Weight : 10.0; }

        private static List<KPair> KPairs()
        {
            if (_kp != null) return _kp;
            Zc4EnsureMap();
            var l = new List<KPair>();
            Func<string, string[], double[], double, bool, string, KPair> add = (o, ins, kg, day, garum, name) =>
            {
                var p = new KPair { Out = o, In = ins, KgIn = kg, KgDay = day, Garum = garum, Name = name, OutIt = ItemOf(o), InIt = new ItemObject[ins.Length], Ratio = new double[ins.Length], ZIn = new int[ins.Length] };
                if (p.OutIt == null || p.OutIt.Value <= 0) return null;
                p.ZOut = Array.IndexOf(ZIds, o);
                double kgo = KgOf(p.OutIt);
                for (int j = 0; j < ins.Length; j++)
                {
                    var it = ItemOf(ins[j]);
                    if (it == null || it.Value <= 0) return null;
                    p.InIt[j] = it; p.ZIn[j] = Array.IndexOf(ZIds, ins[j]);
                    p.Ratio[j] = kg[j] * kgo / KgOf(it);
                }
                p.Labor = kgo / day;
                l.Add(p);
                return p;
            };
            add("bread", new[] { "grain" }, new[] { 1.0 }, 60.0, false, "ziarno -> chleb");
            add("pie", new[] { "grain" }, new[] { 1.0 }, 40.0, false, "ziarno -> placek");
            add("beer", new[] { "grain" }, new[] { KsTab.BeerGrain }, 150.0, false, "ziarno -> piwo");
            add("wine", new[] { "grape" }, new[] { KsTab.WineGrape }, 80.0, false, "winogrona -> wino");
            add("oil", new[] { "olives" }, new[] { KsTab.OilOlive }, 15.0, false, "oliwki -> oliwa");
            add("mead", new[] { "honey" }, new[] { KsTab.MeadHoney }, 50.0, false, "miod -> miod pitny");
            add("pottery", new[] { "clay" }, new[] { KsTab.PotClay }, 15.0, false, "glina -> garnki");
            add("tools", new[] { "iron" }, new[] { KsTab.ToolsIron }, 1.5, false, "zelazo -> narzedzia");
            // uboj (recenzja 16.10): 1 / 0.5 kg zywca na kg miesa, skory zywca (HideOfLive) na kg miesa jako wyrob uboczny; PROC miesa 30 kg na rzemieslnika
            var hides = ItemOf("hides");
            int[] lg = { KsTab.GCow, KsTab.GHog, KsTab.GSheep };
            string[] ln = { "krowy -> mieso", "swinie -> mieso", "owce -> mieso" };
            for (int k = 0; k < 3; k++)
            {
                var p = add("meat", new[] { ZLiveIds[k] }, new[] { 1.0 / KsTab.MeatOfLive }, 30.0, false, ln[k]);
                if (p == null) continue;
                p.Live = k;
                if (hides != null && hides.Value > 0) { p.Side = hides; p.SideRatio = KsTab.HideOfLive(lg[k]) / KsTab.MeatOfLive * KgOf(p.OutIt) / KgOf(hides); }
            }
            add("garum", new[] { "fish", "salt" }, new[] { 2.0, 0.4 }, 20.0, true, "ryby + sol -> garum");
            _kp = l;
            CraftsZero();
            return l;
        }

        /// <summary>TownCrafts.OnDailyTickTown: przetwory ZC5 w tym miescie dzis (kalibracja zastosowana, lambda > 0, klucz).</summary>
        internal static bool CraftsWanted(Town town)
        {
            ZTown zt;
            return town != null && town.IsTown && CraftsOn && ZTownOf(town, out zt);
        }

        private static float PriceIndex(Town t, ItemObject it, bool selling)
        {
            var cat = it.ItemCategory;
            var d = t.MarketData.GetCategoryData(cat);
            return Campaign.Current.Models.TradeItemPriceFactorModel.GetBasePriceFactor(cat, d.InStoreValue, d.Supply, d.Demand, selling, it.Value);
        }

        private static int SoldYesterday(Town t, ItemCategory c)
        {
            try
            {
                var l = t.SoldItems; if (l == null || c == null) return 0;
                int n = 0; foreach (var x in l) if (x.Category == c && x.Number > 0) n += x.Number;
                return n;
            }
            catch { return 0; }
        }

        /// <summary>Zysk pary na sztuke wyrobu przy cenach miasta (wyrob + uboczny - wsad) i koszt wsadu + pracy.</summary>
        private static void PairMoney(Town town, KPair p, double pay, out double pout, out double cost)
        {
            pout = PriceIndex(town, p.OutIt, true) * p.OutIt.Value;
            if (p.Side != null && p.SideRatio > 0) pout += p.SideRatio * PriceIndex(town, p.Side, true) * p.Side.Value;
            cost = p.Labor * pay;
            for (int j = 0; j < p.InIt.Length; j++) cost += p.Ratio[j] * PriceIndex(town, p.InIt[j], false) * p.InIt[j].Value;
        }

        /// <summary>Doba miasta (w ramce ksiegi towarow rzemiosla miasta, wolane z TownCrafts.OnDailyTickTown): pary ZC5 krokami, cena od nowa, na zbyt; rece wedlug
        /// zysku na roboczodzien.</summary>
        internal static void TownCraftsDay(Town town)
        {
            ZTown zt;
            if (!ZTownOf(town, out zt)) return;
            // koszt (poprawka po autotescie 16.10: 1.96 ms/d): miasto co 2 doby na zmiane, wsad i rece dwoch dob naraz (zbyt i tak do tygodnia zakupow)
            if (((Fnv(town.Settlement != null ? town.Settlement.StringId : null) + (uint)Today()) & 1u) != 0) return;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                var pairs = KPairs();
                int n = pairs.Count;
                if (n == 0) return;
                var s = Settings.Current;
                var shelf = town.Owner.ItemRoster; var sett = town.Settlement;
                if (shelf == null || sett == null) return;
                double[] acc;
                if (!_kAcc.TryGetValue(town, out acc) || acc == null || acc.Length != n * 3) { acc = new double[n * 3]; _kAcc[town] = acc; }   // [pi*2+j] ulamki wsadu, [2n+pi] ulamek ubocznego
                float wage = Math.Max(0.5f, s != null ? s.WorkshopWagePerDay : 3f);
                double pay = wage * TownCrafts.WageIndex(town) * KsWageHands.Factor(town.Settlement);   // r4-koszyk: placa za brak rak (Ks Wage By Hands; inaczej 1)
                double margin = 1.0 + Math.Max(0f, s != null ? s.SmithProfitPercent : 20f) / 100.0;
                double hands = 2.0 * zt.Hands, lam = zt.Lam;
                bool real = SwRealOfftake;
                _kDayTowns += 2; _kHands += hands;                       // miasto-doby: jedna przerobka za dwie doby
                var made = new int[n]; var taken = new int[n * 2]; var side = new int[n];
                // kolejnosc par: zysk na roboczodzien przy cenach miasta (najlepsza pierwsza)
                var order = new int[n]; var score = new double[n];
                for (int pi = 0; pi < n; pi++)
                {
                    order[pi] = pi;
                    double po, co; PairMoney(town, pairs[pi], pay, out po, out co);
                    score[pi] = pairs[pi].Labor > 0 ? (po - co) / pairs[pi].Labor : 0.0;
                }
                Array.Sort(order, (x, y) => score[y].CompareTo(score[x]));
                foreach (int pi in order)
                {
                    var p = pairs[pi];
                    if (p.Garum && !zt.Garum) continue;
                    if (hands < p.Labor * 0.5) { _kNoHands[pi]++; continue; }
                    // na zbyt: wyrob do tygodnia zakupow miasta
                    double dOut = (p.ZOut >= 0 ? lam * (zt.Q[p.ZOut] + zt.QV[p.ZOut]) : 0.0) + (1 - lam) * SoldYesterday(town, p.OutIt.ItemCategory);
                    if (p.Garum) dOut = Math.Max(dOut, SoldYesterday(town, p.OutIt.ItemCategory) + 1.0);
                    // KS-R3 G5 (klucz Ks Real Offtake): wyrob najwyzej do 4 dni zakupow (dotad 7) - swiezy lot ziarna nie staje sie tygodniem chleba przy pustej polce zboza
                    int room = (int)Math.Ceiling((real ? 4.0 : 7.0) * dOut) - shelf.GetItemNumber(p.OutIt);
                    if (room <= 0) { _kFull[pi]++; continue; }
                    int batch = Math.Max(1, (int)Math.Ceiling(room / 6.0));
                    int steps = 0, why = 0;
                    while (room > 0 && steps < 12)
                    {
                        steps++;
                        int k = Math.Min(batch, room);
                        // wsad: ponad 2 dni (KS-R3 G5: 6 dni) bezposredniej potrzeby ludu na ten towar i ponad 5 sztuk
                        int kw = k;
                        for (int j = 0; j < p.InIt.Length && k > 0; j++)
                        {
                            double reserve = 5 + (p.ZIn[j] >= 0 ? (real ? 6.0 : 2.0) * lam * (zt.Q[p.ZIn[j]] + zt.QV[p.ZIn[j]]) : 0.0);
                            double avail = shelf.GetItemNumber(p.InIt[j]) - reserve;
                            int kj = (int)Math.Floor((avail - acc[pi * 2 + j]) / p.Ratio[j]);
                            if (kj < k)
                            {
                                // KS-R3 P4: brak wsadu ZC5 - brak zaplaconego popytu (sterowanie rynku)
                                if (kj <= 0 && p.ZIn[j] >= 0 && SwMarketControl) KsUnmet(zt.Market, p.ZIn[j], kw * p.Ratio[j]);   // raz: partia bez wsadu (petla konczy sie)
                                k = Math.Max(0, kj);
                            }
                        }
                        if (k <= 0) { why = 2; break; }
                        int kh = (int)Math.Floor(hands / p.Labor);
                        if (kh < k) k = kh;
                        if (k <= 0) { why = 3; break; }
                        double pout, cost; PairMoney(town, p, pay, out pout, out cost);
                        if (pout < cost * margin) { why = 1; break; }
                        for (int j = 0; j < p.InIt.Length; j++)
                        {
                            double want = acc[pi * 2 + j] + k * p.Ratio[j];
                            int take = (int)Math.Floor(want + 1e-9);
                            acc[pi * 2 + j] = want - take;
                            if (take > 0) { shelf.AddToCounts(p.InIt[j], -take); taken[pi * 2 + j] += take; MaterialOrders.NoteUse(town, p.InIt[j], take); }
                        }
                        shelf.AddToCounts(p.OutIt, k);
                        if (p.Side != null && p.SideRatio > 0)
                        {
                            double sw = acc[2 * n + pi] + k * p.SideRatio;
                            int sk = (int)Math.Floor(sw + 1e-9);
                            acc[2 * n + pi] = sw - sk;
                            if (sk > 0) { shelf.AddToCounts(p.Side, sk); side[pi] += sk; }
                        }
                        made[pi] += k; room -= k; hands -= k * p.Labor;
                    }
                    if (why == 1) _kNoProfit[pi]++; else if (why == 2) _kNoInput[pi]++; else if (why == 3) _kNoHands[pi]++;
                }
                for (int pi = 0; pi < n; pi++)
                {
                    if (made[pi] <= 0) continue;
                    var p = pairs[pi];
                    _kMade[pi] += made[pi]; _kTowns[pi]++; _kSide[pi] += side[pi];
                    try
                    {
                        for (int j = 0; j < p.InIt.Length; j++) { int t = taken[pi * 2 + j]; _kTaken[pi] += t; if (t > 0) CampaignEventDispatcher.Instance.OnItemConsumed(p.InIt[j], sett, t); }
                        CampaignEventDispatcher.Instance.OnItemProduced(p.OutIt, sett, made[pi]);
                        if (side[pi] > 0) CampaignEventDispatcher.Instance.OnItemProduced(p.Side, sett, side[pi]);
                    }
                    catch (Exception e) { _kStum++; Stumble("TownCraftsDay(zdarzenia)", e); }
                }
                _kUsed += 2.0 * zt.Hands - hands;
                KsWageHands.NoteCraft(town.Settlement, 2.0 * zt.Hands, 2.0 * zt.Hands - hands);   // r4-koszyk: zajecie rak przetworow - sygnal braku rak (decyzja tygodnia)
            }
            catch (Exception e) { _kStum++; Stumble("TownCraftsDay", e); }
            finally { _kTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>TownCrafts.Work (rzemioslo 148): przy zastosowanej kalibracji wsad tkania i garbowania wedlug tabel KS (len 1.25, welna 1.45, skory 2.4 kg na kg wyrobu) -
        /// te same proporcje co popyt Planu; inaczej - proporcja 148 (wartosc / masa).</summary>
        internal static float TextileRatio(ItemObject inIt, ItemObject outIt, float def)
        {
            if (inIt == null || outIt == null || !Applied || Lam(0) <= 0 && Lam(1) <= 0) return def;
            double k;
            string o = outIt.StringId, i = inIt.StringId;
            if (o == "felt" && i == "wool") k = KsTab.FeltWool;
            else if (o == "linen" && i == "flax") k = KsTab.LinenFlax;
            else if (o == "leather" && i == "hides") k = KsTab.LeatherHide;
            else return def;
            double kgo = outIt.Weight > 0.05f ? outIt.Weight : 10.0, kgi = inIt.Weight > 0.05f ? inIt.Weight : 10.0;
            return (float)(k * kgo / kgi);
        }

        /// <summary>Linia "Przetwory miast (ZC5): dzien N" (liczniki od ostatniej linii) albo null (nic nie liczylo).</summary>
        private static string CraftsLine(int day)
        {
            if (_kp == null || _kDayTowns == 0) { CraftsZero(); return null; }
            var sb = new StringBuilder(1800);
            sb.Append("Przetwory miast (ZC5): dzien ").Append(day).Append(" | miast-dob ").Append(_kDayTowns).Append(", rece ").Append(F0(_kHands)).Append(" roboczodni, zajete ").Append(F0(_kUsed))
              .Append(" (rece wedlug zysku na roboczodzien) | wyrobione (miast; koniec: bez zysku / brak wsadu / polka pelna / rece):");
            for (int i = 0; i < _kp.Count; i++)
                sb.Append(i == 0 ? " " : ", ").Append(_kp[i].Name).Append(' ').Append(_kMade[i]).Append(" szt. (wsad ").Append(F0(_kTaken[i])).Append(_kp[i].Side != null ? "; skory " + _kSide[i] : "").Append("; ").Append(_kTowns[i]).Append("; ")
                  .Append(_kNoProfit[i]).Append('/').Append(_kNoInput[i]).Append('/').Append(_kFull[i]).Append('/').Append(_kNoHands[i]).Append(')');
            sb.Append(" | place: przez zdarzenie OnItemProduced -> doplata kiesy ludu do 35% wartosci wyrobow | potkniecia ").Append(_kStum).Append('.');
            CraftsZero(); _kStum = 0;
            return sb.ToString();
        }
    }
}
