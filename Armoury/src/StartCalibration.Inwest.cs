using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;

namespace Armoury
{
    /// <summary>
    /// inwest (noc 17/18.10, projekt docs/PROJEKT-INWESTYCJE-OKREGU-2026-10-16.md; zajecia wiosek pobocznych + prace okregu): wejscie KS dla paczki - wlasny plik
    /// czesciowy, zeby nie ruszac reszty KS (inne paczki nocy dopisuja tam swoje wiersze).
    ///  - ZAJECIA POD KS: wioska poboczna to blok KS (wlasna ziemia z arm_ks_land.tsv + nakladka przysiolkow v6, kandydaci zajec z jednej tabeli swiata - KsCore.Candidates,
    ///    snieg bez upraw). Rece bloku i ich udzial w zajeciach (C/X) daje Plan przy wlaczeniu, potem zmienia je TYLKO regula A7 (StartCalibration.Rule.cs) z hamulcem Jeffa
    ///    (+25% przez 60 dob, koszt przebudowy do kasy miasta, okres przejsciowy 60 / 364 dob, jedna czesc okregu naraz, 5% rak regionu na sezon) - Plan co 7 dob
    ///    po wlaczeniu nie przestawia rak blokow (tylko praca domowa, z krokiem 0.02 po D+49). Paczka tylko czyta zajecie bloku (najwiekszy udzial rak) do opisu,
    ///    ikony i prac okregu; blok z pracami okregu nie zmienia zajecia (KsWorksLock w RuleDaily).
    ///  - PRACE OKREGU POD KS (jedna regula z droga bez KS): dodatek b czesci mnozy produkcje JEJ zajecia - kg zajecia bloku (P0 x L x rece zajecia, ten sam wzor co
    ///    KsDeliver.Produce) x b dopisane do produkcji okregu PRZED zima, E4/5 i E4/7 (ComputeDistrict) - dalej to, co okreg zje sam, i stawki na targ jak reszta.
    ///    Plan (watek roboczy) dodatku nie widzi - planuje rece, a prace to kapital czesci; nadwyzka idzie na targ i ceny ja wchlaniaja (podaz i popyt).
    /// Wyjatki per okreg liczone (Stumble) - nic sie nie gasi.
    /// </summary>
    internal static partial class StartCalibration
    {
        /// <summary>inwest: czesc okregu pod KS - wioska poboczna (Uid) albo wies gry (Main: 20 blokow razem). Craft - zajecie z najwiekszym udzialem rak,
        /// Mix - udzial rak wedlug zajecia (KsTab.NC), Kg - kg/d zajec czesci (bez zimy), Busy - przebudowa A7 w toku, Hands - rece czesci.</summary>
        internal sealed class KsPartView { public string Uid; public bool Main; public int Craft = -1; public double[] Mix = new double[KsTab.NC]; public double Hands; public bool Busy; public double[] Kg = new double[ItemCount]; public double[] CraftVal = new double[KsTab.NC];
            // f4b-inwest (District Icon Part Crafts): wlasne zajecie czesci do ikony (wies gry - typ okregu, wioska poboczna - zajecie jej ziemi) i kg/d jego towarow NA TARG
            // (kg zajecia bloku x czesc sprzedana okregu - bez tego, co okreg zje sam); null - klucz wylaczony
            public int IconCraft = -1; public double[] IconKg; }

        /// <summary>f4b-inwest: kg jednej sztuki gry towaru dostawy KS - ten sam przelicznik kg -> sztuka co wozy KS (Market = kg / (sigma x kg sztuki)); 10 bez tabeli.</summary>
        internal static double ItemKgOf(int i)
        {
            try { EnsureItems(); } catch { }
            return _itemKg != null && i >= 0 && i < _itemKg.Length && _itemKg[i] > 0 ? _itemKg[i] : 10.0;
        }

        /// <summary>f4b-inwest: wlasne zajecie czesci okregu pod KS do ikony - wies gry: typ okregu (projekt A4); wioska poboczna: zajecie drogi bez KS (ziemia wioski, ta sama
        /// tabela swiata), a bez niej zajecie startowe bloku z pliku ziemi KS; zajecie spoza tabeli - -1.</summary>
        private static int IconCraftOf(LiveDist ld, LiveBlock lb, string type)
        {
            int c = -1;
            if (lb.K.Main) { if (type != null) KsTab.CraftIndex.TryGetValue(type, out c); return c; }
            string uid = lb.Uid ?? lb.K.Uid;
            c = VillageCrafts.SideCraft(uid);
            if (c < 0 && lb.K.Start != null) KsTab.CraftIndex.TryGetValue(lb.K.Start, out c);
            return c;
        }

        /// <summary>inwest: wies z zastosowanym KS (stan, klucz, okreg w stanie, kraina nie wrocona bramka) - opis i prace okregu licza z blokow KS.</summary>
        internal static bool KsDistrictLive(Village v)
        {
            if (v == null || _state != StateApplied || !KeyOn) return false;
            try
            {
                var st = v.Settlement; LiveDist ld;
                if (st == null || st.StringId == null || !_live.TryGetValue(st.StringId, out ld) || ld == null || ld.B == null || ld.K == null) return false;
                return !LandBackOf(st);
            }
            catch { return false; }
        }

        /// <summary>m114-prace: wartosc dnia towaru okregu NA TARG (to, co wozy wioza) i dodatku prac, ktory idzie na targ - z ostatniego rachunku okregu (ld.Res, po zimie
        /// i czynnikach rak), w cenie miasta targowego (VillageCrafts.Price - jak ValDay). false - okreg poza KS, bez rachunku albo bez towaru na targ. Watek glowny, nie rzuca.</summary>
        internal static bool WorksMarketOf(Village v, out double market, out double extra) { bool inc; return WorksMarketOf(v, out market, out extra, out inc, null, null); }

        /// <summary>m114-prace (recenzja): jak wyzej, a takze inc - prace byly w ostatnim rachunku okregu (WorksKs; false - rachunek sprzed zakupu albo okreg bez prac)
        /// i (gdy podane) wartosc dnia dodatku na targ (partMkt) i calego dodatku po czynnikach (partAll) wedlug czesci (klucz DistrictWorks.PartKey) - towar kazdej
        /// pozycji dzielony miedzy czesci wedlug ich kg dodatku przed zima (zima i czynniki rak jednakowe dla towaru).</summary>
        internal static bool WorksMarketOf(Village v, out double market, out double extra, out bool inc, Dictionary<string, double> partMkt, Dictionary<string, double> partAll)
        {
            market = 0; extra = 0; inc = false;
            if (partMkt != null) partMkt.Clear();
            if (partAll != null) partAll.Clear();
            if (v == null || !KsDistrictLive(v)) return false;
            try
            {
                LiveDist ld;
                if (!_live.TryGetValue(v.Settlement.StringId, out ld) || ld == null || ld.Res == null || _items == null) return false;
                Town town = v.TradeBound != null ? v.TradeBound.Town : (v.Bound != null ? v.Bound.Town : null);
                var r = ld.Res;
                for (int i = 0; i < ItemCount; i++)
                {
                    var it = _items[i];
                    if (it == null) continue;
                    double p = VillageCrafts.Price(town, it);
                    if (r.Market[i] > 0) market += r.Market[i] * p;
                    if (r.WorksMkt != null && r.WorksMkt[i] > 0) extra += r.WorksMkt[i] * p;
                    if (r.WorksMkt != null && r.WorksAll != null && r.WorksPart != null && (partMkt != null || partAll != null) && r.WorksAll[i] > 0)
                    {
                        double tot = 0; foreach (var kv in r.WorksPart) if (kv.Value != null && kv.Value[i] > 0) tot += kv.Value[i];
                        if (!(tot > 0)) continue;
                        foreach (var kv in r.WorksPart)
                        {
                            if (kv.Value == null || !(kv.Value[i] > 0)) continue;
                            double fr = kv.Value[i] / tot, a;
                            if (partMkt != null) { partMkt.TryGetValue(kv.Key, out a); partMkt[kv.Key] = a + fr * r.WorksMkt[i] * p; }
                            if (partAll != null) { partAll.TryGetValue(kv.Key, out a); partAll[kv.Key] = a + fr * r.WorksAll[i] * p; }
                        }
                    }
                }
                if (double.IsNaN(market) || double.IsInfinity(market) || double.IsNaN(extra) || double.IsInfinity(extra)) { market = extra = 0; return false; }
                inc = r.WorksMkt != null;
                return market > 0;
            }
            catch { market = extra = 0; inc = false; if (partMkt != null) partMkt.Clear(); if (partAll != null) partAll.Clear(); return false; }
        }

        /// <summary>m114-prace (recenzja): czesc dodatku NOWYCH prac czesci uid (null - wies gry) przy zajeciu craft, ktora poszlaby na targ - wedlug towarow tego zajecia
        /// w bloku (kg x cena): towar, ktorego okreg ma nadmiar na targ (MarketKg > 0 - produkcja ponad stol), idzie na targ caly; towar, ktorego okreg ma mniej niz zjada -
        /// dodatek zjedzony w domu (0, ostroznie: brak liczby niedoboru). false - okreg poza KS, bez rachunku albo czesc bez towaru tego zajecia. Watek glowny, nie rzuca.</summary>
        internal static bool WorksPartFrac(Village v, string uid, int craft, out double frac)
        {
            frac = 1.0;
            if (v == null || craft < 0 || craft >= KsTab.NC || !KsDistrictLive(v)) return false;
            try
            {
                LiveDist ld;
                if (!_live.TryGetValue(v.Settlement.StringId, out ld) || ld == null || ld.Res == null || ld.B == null || ld.K == null || _items == null) return false;
                Town town = v.TradeBound != null ? v.TradeBound.Town : (v.Bound != null ? v.Bound.Town : null);
                string type = ld.K.Type;
                var tmp = new double[ItemCount]; var kg = new double[ItemCount];
                bool cattle = KsTab.Crafts[craft] == "cattle_farm";
                foreach (var lb in ld.B)
                {
                    if (lb == null || lb.K == null || lb.C == null) continue;
                    if (uid == null ? !lb.K.Main : (lb.K.Main || (lb.Uid ?? lb.K.Uid) != uid)) continue;
                    for (int j = 0; j < lb.C.Length; j++)
                    {
                        if (lb.C[j] != craft && !(cattle && KsTab.Crafts[lb.C[j]] == "cattle_farm:maslo")) continue;   // jak WorksKs
                        Array.Clear(tmp, 0, tmp.Length);
                        BlockCraftKg(ld.K, lb, j, type, tmp);
                        for (int i = 0; i < ItemCount; i++) if (tmp[i] > 0) kg[i] += tmp[i];
                    }
                }
                double all = 0, mkt = 0;
                for (int i = 0; i < ItemCount; i++)
                {
                    if (!(kg[i] > 0) || _items[i] == null) continue;
                    double w = kg[i] * VillageCrafts.Price(town, _items[i]) / Math.Max(0.1, _itemKg != null ? _itemKg[i] : 10.0);
                    all += w; if (ld.Res.MarketKg[i] > 1e-9) mkt += w;
                }
                if (!(all > 0) || double.IsNaN(all) || double.IsInfinity(all)) return false;
                frac = Math.Max(0.0, Math.Min(1.0, mkt / all));
                return true;
            }
            catch { frac = 1.0; return false; }
        }

        /// <summary>inwest: wiersze ziemi wiosek pobocznych okregu (plik ziemi KS po nakladce przysiolkow v6) - zajecia startowe i kandydaci drogi bez KS.
        /// false - brak pliku ziemi albo okregu.</summary>
        internal static bool SideRows(string districtId, List<LandRow> into)
        {
            if (into != null) into.Clear();
            if (districtId == null || into == null) return false;
            try
            {
                EnsureLand();
                LandDist d;
                if (_landVil == null || !_landVil.TryGetValue(districtId, out d) || d == null) return false;
                into.AddRange(d.Side);
                return true;
            }
            catch (Exception e) { Stumble("SideRows", e); return false; }
        }

        /// <summary>inwest: pierwszy wiersz bloku wsi gry (punkt wsi) - ziemia wsi gry do zajec wsi gry drogi bez KS (zawsze typ okregu, projekt A4).</summary>
        internal static bool MainRow(string districtId, out LandRow row)
        {
            row = null;
            try
            {
                EnsureLand();
                LandDist d;
                if (_landVil == null || districtId == null || !_landVil.TryGetValue(districtId, out d) || d == null) return false;
                row = d.Main[0];
                return row != null;
            }
            catch { return false; }
        }

        /// <summary>inwest: kandydaci zajec wioski (jedna tabela swiata: przydatnosc ziemi >= 0.3, zajecie startowe zawsze, regale nie) - ta sama regula co blok KS.</summary>
        internal static List<int> CandidatesOf(LandRow row, string dtype)
        {
            if (row == null) return new List<int>();
            var kb = new KsBlock { Uid = row.Uid, Start = row.Start, O = row.O, Main = false };
            return KsCore.Candidates(kb, dtype);
        }

        /// <summary>inwest: przydatnosc ziemi wioski dla zajecia (KsTab.Land2 - strefa x teren x woda; snieg zeruje uprawy).</summary>
        internal static double Suit(LandRow row, int craft, string dtype)
        {
            if (row == null || row.O == null || craft < 0 || craft >= KsTab.NC) return 0.0;
            try { return KsTab.Land2(craft, row.O, dtype); } catch { return 0.0; }
        }

        /// <summary>inwest: czesci okregu pod KS (wies gry jako jedna czesc - 20 blokow; wioski poboczne po uid). false - okreg poza KS (droga bez KS).</summary>
        internal static bool KsPartsOf(Village v, List<KsPartView> into)
        {
            if (into != null) into.Clear();
            if (into == null || !KsDistrictLive(v)) return false;
            try
            {
                LiveDist ld;
                if (!_live.TryGetValue(v.Settlement.StringId, out ld)) return false;
                EnsureValues();
                string type = ld.K.Type;
                var main = new KsPartView { Main = true };
                var tmp = new double[ItemCount];
                // f4b-inwest (District Icon Part Crafts): czesc sprzedana okregu na towar (targ / produkcja z ostatniego rachunku okregu; bez rachunku - calosc)
                var sIc = Settings.Current;
                bool icon = sIc != null && sIc.DistrictIconPartCrafts;
                bool f17 = sIc != null && sIc.DistrictIconCraftValue;     // f5-zajecia: samo zajecie ziemi czesci (IconCraft) do ikony F17 - bez kg sprzedazy
                double[] sold = null;
                if (icon)
                {
                    sold = new double[ItemCount];
                    var res = ld.Res;
                    for (int i = 0; i < ItemCount; i++) sold[i] = res != null && res.Prod[i] > 1e-9 ? Math.Max(0.0, Math.Min(1.0, res.MarketKg[i] / res.Prod[i])) : 1.0;
                }
                foreach (var lb in ld.B)
                {
                    if (lb == null || lb.K == null || lb.C == null || lb.X == null) continue;
                    KsPartView pv;
                    if (lb.K.Main) pv = main;
                    else { pv = new KsPartView { Uid = lb.Uid ?? lb.K.Uid, Main = false }; into.Add(pv); }
                    pv.Hands += lb.Hands;
                    if (lb.Target >= 0) pv.Busy = true;
                    if (icon && pv.IconKg == null) { pv.IconKg = new double[ItemCount]; pv.IconCraft = IconCraftOf(ld, lb, type); }
                    else if (!icon && f17 && pv.IconCraft < 0) pv.IconCraft = IconCraftOf(ld, lb, type);   // f5-zajecia (District Icon Craft Value)
                    for (int j = 0; j < lb.C.Length; j++)
                    {
                        int c = lb.C[j];
                        if (c < 0 || c >= KsTab.NC) continue;
                        pv.Mix[c] += lb.X[j] * lb.Hands;
                        Array.Clear(tmp, 0, tmp.Length);
                        BlockCraftKg(ld.K, lb, j, type, tmp);
                        double val = 0;
                        for (int i = 0; i < ItemCount; i++) { if (tmp[i] <= 0) continue; pv.Kg[i] += tmp[i]; val += tmp[i] * ValKgItem(i); }
                        pv.CraftVal[c] += val;
                        // f4b-inwest: towary wlasnego zajecia czesci (maslo bydla jak bydlo) - tylko czesc sprzedana
                        if (icon && pv.IconCraft >= 0 && (c == pv.IconCraft || (c == KsTab.CCattleB && pv.IconCraft == KsTab.CCattle) || (c == KsTab.CCattle && pv.IconCraft == KsTab.CCattleB)))
                            for (int i = 0; i < ItemCount; i++) if (tmp[i] > 0) pv.IconKg[i] += tmp[i] * sold[i];
                    }
                }
                into.Add(main);
                foreach (var pv in into)
                {
                    double s = 0; int best = -1; double bx = -1;
                    for (int c = 0; c < KsTab.NC; c++) { s += pv.Mix[c]; if (pv.Mix[c] > bx) { bx = pv.Mix[c]; best = c; } }
                    if (s > 0) for (int c = 0; c < KsTab.NC; c++) pv.Mix[c] /= s;
                    pv.Craft = bx > 0 ? best : -1;
                }
                return true;
            }
            catch (Exception e) { Stumble("KsPartsOf", e); into.Clear(); return false; }
        }

        /// <summary>Wartosc kg towaru dostawy przy cenie bazowej (do udzialow zajec w wartosci okregu): zywiec - polowa miesa i skory, reszta Value / kg sztuki.</summary>
        private static double ValKgItem(int i)
        {
            if (_items == null || i < 0 || i >= ItemCount) return 0.0;
            var it = _items[i];
            if (it == null) return 0.0;
            return Math.Max(0, it.Value) / Math.Max(0.1, _itemKg != null ? _itemKg[i] : 10.0);
        }

        /// <summary>kg/d zajecia C[j] bloku lb (ten sam wzor co KsDeliver.Produce: rece zajecia poza domem i zbozem wlasnym x (1 - praca domowa) x P0 x L), bez zimy
        /// i czynnikow rak - z liczb ostatniego rachunku bloku (Produce ustawia Hands, WH, SubNW, Lg, HW). out_ dopisywane (nie zerowane).</summary>
        private static void BlockCraftKg(KsDistrict d, KsPart lb, int j, string type, double[] out_)
        {
            if (d == null || lb == null || lb.C == null || j < 0 || j >= lb.C.Length) return;
            int c = lb.C[j];
            double rsum = 0; if (lb.RF != null) for (int k = 0; k < lb.RF.Length; k++) rsum += lb.RF[k];
            double scale = Math.Max(0.0, 1.0 - rsum);
            double x = lb.X[j] * scale;
            if (!(x > 0)) return;
            double rem = Math.Max(0.0, lb.Hands - lb.WH);
            double lg = lb.Lg;
            double sub = c != KsTab.CWheat && lg > 0 ? Math.Min(rem, lb.SubNW) : 0.0;
            double hw = lb.HW > 0 ? Math.Min(0.95, lb.HW) : 0.0;
            double cl = (rem - sub) * (1.0 - hw);
            if (!(cl > 0)) return;
            double L = lb.L(c, type);
            foreach (var kv in KsTab.P0[c])
            {
                int it = kv.Key == KsTab.GWhaleMeat && c == KsTab.CHunter ? IMeat : GoodToItem[kv.Key];
                if (it >= 0) out_[it] += x * cl * kv.Value * L;
            }
        }

        // ------------------------------------------------------------ prace okregu pod KS (dodatek produkcji czesci)
        private static long _wkCalls, _wkParts; private static double _wkKg;

        /// <summary>ComputeDistrict (zaraz po KsDeliver.Produce, przed zima i czynnikami rak): dodatek prac okregu - dla czesci z pracami b x kg zajecia prac tej czesci
        /// (wies gry - 20 blokow, wioska poboczna - jej blok). Klucz District Works wylaczony albo okreg bez prac - nic. Dodatek zapamietany w pracach okregu
        /// (sztuki gry na dobe przed zima - udzial dodatku w utargu przy wozie).</summary>
        private static double[] WorksKs(LiveDist ld, double[] prod, out Dictionary<string, double[]> parts)
        {
            parts = null;
            if (ld == null || ld.TF || ld.V == null || ld.B == null || ld.K == null || prod == null) return null;
            if (!DistrictWorks.HasWorks(ld.V)) return null;
            try
            {
                _wkCalls++;
                string type = ld.K.Type;
                var extra = new double[ItemCount];
                var tmp = new double[ItemCount];
                foreach (var lb in ld.B)
                {
                    if (lb == null || lb.K == null || lb.C == null) continue;
                    int craft; double b;
                    string puid = lb.K.Main ? null : (lb.Uid ?? lb.K.Uid);
                    if (!DistrictWorks.BonusOf(ld.V, puid, out craft, out b) || !(b > 0) || craft < 0) continue;
                    string pk = DistrictWorks.PartKey(puid);                 // m114-prace (recenzja): dodatek czesci - podzial puli wedlug dodatku na targ
                    double[] pe; if (parts == null) parts = new Dictionary<string, double[]>(StringComparer.Ordinal);
                    if (!parts.TryGetValue(pk, out pe)) { pe = new double[ItemCount]; parts[pk] = pe; }
                    for (int j = 0; j < lb.C.Length; j++)
                    {
                        if (lb.C[j] != craft && !(KsTab.Crafts[lb.C[j]] == "cattle_farm:maslo" && KsTab.Crafts[craft] == "cattle_farm")) continue;
                        Array.Clear(tmp, 0, tmp.Length);
                        BlockCraftKg(ld.K, lb, j, type, tmp);
                        for (int i = 0; i < ItemCount; i++) if (tmp[i] > 0) { extra[i] += b * tmp[i]; pe[i] += b * tmp[i]; }
                    }
                    _wkParts++;
                }
                double sigma = Math.Max(1, DailyLife.KsSigma), kg = 0;
                var pcs = new double[ItemCount];
                for (int i = 0; i < ItemCount; i++)
                {
                    if (!(extra[i] > 0)) continue;
                    prod[i] += extra[i]; kg += extra[i];
                    pcs[i] = extra[i] / (sigma * (_itemKg != null ? _itemKg[i] : 10.0));
                }
                _wkKg += kg;
                DistrictWorks.NoteKsExtra(ld.V, _items, pcs);
                // m114-prace: kg dodatku (przed zima) - ComputeDistrict liczy z niego dodatek na targ. Recenzja: takze przy 0 kg (prace w rachunku, dodatek 0 -
                // wyplata 0, nie dawne E/V z utargu innych towarow); null tylko okreg bez prac albo blad
                if (parts == null) parts = new Dictionary<string, double[]>(StringComparer.Ordinal);
                return extra;
            }
            catch (Exception e) { Stumble("WorksKs", e); parts = null; return null; }
        }

        /// <summary>inwest: blok z pracami okregu nie zmienia zajecia (regula A7) - wioska z pracami (Uid) albo wies gry (Main), gdy prace sa w wsi gry.</summary>
        private static bool KsWorksLock(LiveDist ld, LiveBlock b)
        {
            if (ld == null || ld.V == null || b == null || b.K == null) return false;
            try { return DistrictWorks.Locks(ld.V, b.K.Main ? null : (b.Uid ?? b.K.Uid)); } catch { return false; }
        }

        /// <summary>inwest: okreg do przeliczenia nastepnej doby (nowe prace, ogien) - dodatek wchodzi do stawek KS bez czekania na kolejke 7 dob. Nie rzuca.</summary>
        internal static void KsDirty(Village v)
        {
            // scalenie inwest + ks-koszt (K4a): nowe prace albo ogien moga zmienic blokade A7 (KsWorksLock zeruje Held takze w dobie bez kroku) - nastepna doba
            // przechodzi pelna petla RuleDaily, jak bez K4a (bit w bit; bez prac okregu KsDirty nie jest wolane)
            _k4Clean = false;
            try
            {
                if (v == null || v.Settlement == null || v.Settlement.StringId == null || _state != StateApplied) return;
                LiveDist ld; if (_live.TryGetValue(v.Settlement.StringId, out ld) && ld != null) ld.Res = null;
            }
            catch { }
        }

        /// <summary>inwest: liczniki dodatku prac pod KS do linii "Prace okregu (PO)" (od ostatniego odczytu).</summary>
        internal static string WorksKsText()
        {
            string t = "pod KS: rachunkow okregow z pracami " + _wkCalls + ", czesci z dodatkiem " + _wkParts + ", dodatek " + _wkKg.ToString("0.0", Inv) + " kg/d przed zima";
            _wkCalls = 0; _wkParts = 0; _wkKg = 0;
            return t;
        }
    }
}
