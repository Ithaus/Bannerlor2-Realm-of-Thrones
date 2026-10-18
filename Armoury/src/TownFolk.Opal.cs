using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// f4-opal (noc 17/18.10, fala 4; koncowy przeglad ekonomii 18.10 - paczka P2 "Opal", uwagi "Opal mieszczan pod KS rosnie 29 razy" i "Opal pod KS gubi letnie
    /// zmniejszenie"). Dowod (fin86, Armoury-2026-10-17_23-22-37.log): przy tych samych 1.48 mln glow potrzeba opalu 148 ladunkow/d przed KS, 4270/d po rampie ZC4;
    /// kupione ok. 2100/d, "bez opalu" ok. 2100/d (51%), drewno indeks 0.71 -> 1.42-1.6, ruda mediana indeksu 3.8-4.8 i 53-55 miast bez rudy (bez KS 0.6 i 5-10),
    /// "kopalnia bez wegla" 72.6 lup/d (bez KS 15.1). Przyczyna [K]: TownFolk.Firewood.cs:118 przekazywal do Zc4Firewood czynnik pory tylko zima (latem x1 zamiast
    /// FwSummer 0.5 - dwie reguly na jedno zjawisko), a czesc KS lambda x 1.5 kg na osobe nie zalezala od drewna, ktore naprawde dochodzi na targ; ta sama pelna
    /// potrzeba szla do popytu ceny drewna (Zc4Demand), a zapas rzemiosla (CaravanBulk.TargetFor) liczyl tylko drewno, ktore dymarki zdolaly kupic.
    /// Regula (jedna dla kazdego miasta z koszykiem ZC4; poza ZC4 nic sie nie zmienia):
    ///  (1) FirewoodKsSeason - do czesci KS idzie czynnik pory zawsze: lato FwSummer, zima 1 + kraina x polnoc (jak dotad), wiosna/jesien i "zima bez skutkow" 1.
    ///  (2) FirewoodKsSupplyCap - czesc KS potrzeby = min(lambda x opal x pora, limit), limit = max(0, dowoz drewna na targ miasta - dobowe zuzycie rzemiosla miasta)
    ///      + 1/7 drewna, ktore juz lezy na polce ponad zapas rzemiosla (ten sam prog co zakup: TargetFor, ShelfKeep - zapas z lata ma odbiorce w zimie; przeglad:
    ///      "albo polka ponad zapas rzemiosla").
    ///      Dowoz: drewno sprzedane w tym miescie przez wozy wsi (MarketCarts, latka sprzedazy wozu - roznica jukow przed i po) + drewno z pol mieszczan KS
    ///      (DeliverTownFields) - srednia 7 pelnych dob; dopoki pierscien nie ma 7 dob (po wczytaniu, po wlaczeniu klucza) - Plan: stawki KS wsi z tym miastem
    ///      targowym (TradeBound, inaczej Bound) i pola mieszczan x beta rampy (StartCalibration.OpalPlanWood). Zuzycie rzemiosla: CaravanBulk.CraftWoodUse (to samo,
    ///      z czego miasto liczy zapas rzemiosla - kowale, dymarki, warsztaty, deski, domy; z krokiem 3). Popyt rosnie tylko razem z prawdziwa podaza drewna, tak jak
    ///      wsad przetworow ZC5 tylko z tego, co przetwory naprawde robia (R4cMade7) - rzemioslo ma pierwszenstwo jak przy zakupie (zapas rzemiosla pierwszy).
    ///  (3) FirewoodKsPriceDemand - popyt ceny drewna (StartCalibration.Zc4Demand) liczy opal po regule (1)-(2) zamiast pelnej potrzeby (_fwEff - przy pelnej lambdzie).
    ///  (4) FirewoodMineWoodKeep (CaravanBulk.TargetFor) - zapas rzemiosla miasta liczy drewno dymarek wsi handlujacych w tym miescie: potrzeba ponad las okregu
    ///      (Bloomery.MineShelfNeed) zamiast tego, co zdolaly kupic (ShelfUse), gdy wieksza.
    /// Pieniadze i towar: zadnego nowego przelewu ani ruchu towaru - paczka zmienia tylko ILE lud chce (zakup opalu, zaplata i spalenie - linia N2-E4 bez zmian formatu).
    /// Nic z niczego: dowoz to licznik (sztuki juz na polce), Plan to odczyt stawek. Stan tylko w pamieci (bez zapisu): pierscien od nowa w kazdej sesji.
    /// Koszt: raz na dobe po miastach (97), pomiar dowozu - dwa odczyty jukow na sprzedaz wozu wsi. Linia "Opal pod KS (f4-opal): dzien N". Potkniecia liczone per miasto.
    /// </summary>
    internal static partial class TownFolk
    {
        private const int FwWin = 7;                       // doby sredniej zmierzonego dowozu drewna
        private sealed class FwRing
        {
            public readonly float[] D = new float[FwWin]; public int N, At;
            public void Push(float v) { D[At] = v; At = (At + 1) % FwWin; if (N < FwWin) N++; }
            public double Mean() { if (N <= 0) return 0.0; double a = 0; for (int i = 0; i < N; i++) a += D[i]; return a / N; }
        }
        private static readonly Dictionary<Settlement, FwRing> _fwRing = new Dictionary<Settlement, FwRing>();
        private static readonly Dictionary<Settlement, float> _fwDay = new Dictionary<Settlement, float>();   // dowoz od ostatniej doby opalu (wozy wsi + pola mieszczan)
        private static bool _fwPartial = true;             // pierwsza doba opalu w sesji (albo po przerwie) - niepelna, bez probki
        private static volatile Dictionary<Settlement, float> _fwEff = new Dictionary<Settlement, float>();   // popyt ceny: opal KS po regule (ladunki/d przy pelnej lambdzie)
        [ThreadStatic] private static MobileParty _fwCart;
        [ThreadStatic] private static int _fwCart0;
        private static long _fwCartPcs, _fwFieldPcs;       // dowoz zmierzony od ostatniej linii (tylko licznik)
        private static int _opStumbles;
        private static ItemObject _fwWood;

        /// <summary>Akumulatory jednej doby opalu (FirewoodDaily); null - zaden klucz paczki nie czynny (stara droga co do bitu).</summary>
        private sealed class OpalDay
        {
            public bool Season, Cap, Price, Keep, Full, Wired;
            public Dictionary<Settlement, double> Plan;    // dowoz wedlug Planu (zapas na pierwsze doby pierscienia)
            public Dictionary<Settlement, float> Eff = new Dictionary<Settlement, float>();
            public int Towns, Capped, SrcMeas, SrcPlan, SrcNone, KeepTowns;
            public double KFull, KCut, CapSum, DSum, USum, StockSum, LamSum, KSum, EffSum, FireSum, OldPart, Over, KeepExtra, MineNeed;
            public long T0;
        }

        private static ItemObject FwWood() { if (_fwWood == null) { try { _fwWood = DefaultItems.HardWood; } catch { _fwWood = null; } } return _fwWood; }

        private static bool FwMeasureOn { get { var s = Settings.Current; return s != null && s.TownFolkFirewood && s.FirewoodKsSupplyCap; } }

        /// <summary>FirewoodSessionStart: pamiec paczki od nowa (pierscien, popyt ceny, liczniki) i linia startu; proba zimy teraz (autotest - WinterTrial).</summary>
        private static void OpalSessionStart()
        {
            _fwRing.Clear(); _fwDay.Clear(); _fwPartial = true; _fwEff = new Dictionary<Settlement, float>();
            _fwCart = null; _fwCart0 = 0; _fwCartPcs = 0; _fwFieldPcs = 0; _opStumbles = 0; _fwWood = null;
            string winterNow = "?";
            try { winterNow = WinterTrial.LoadedSessionStart(); } catch (Exception e) { winterNow = "BLAD"; Stumble("OpalSessionStart(zima)", e); }
            var s = Settings.Current;
            Log.Info("Opal pod KS (f4-opal): start sesji | klucze: pora roku (Firewood Ks Season) " + Yn(s != null && s.FirewoodKsSeason)
                     + ", limit dowozu (Firewood Ks Supply Cap) " + Yn(s != null && s.FirewoodKsSupplyCap) + ", popyt ceny (Firewood Ks Price Demand) " + Yn(s != null && s.FirewoodKsPriceDemand)
                     + ", zapas dymarek (Firewood Mine Wood Keep) " + Yn(s != null && s.FirewoodMineWoodKeep) + " | opal " + (s != null && s.TownFolkFirewood ? "TAK" : "NIE (Town Folk Firewood - paczka bez skutku)")
                     + " | tylko miasta z koszykiem ZC4 (KS zastosowany, lambda > 0); dowoz = wozy wsi + pola mieszczan, srednia " + FwWin + " pelnych dob (do tego czasu Plan)"
                     + " | proba zimy teraz (plik " + WinterTrial.NowFlagName + "): " + winterNow + " | potkniecia 0.");
        }

        private static string Yn(bool b) { return b ? "TAK" : "NIE"; }

        // ------------------------------------------------------------ pomiar dowozu (wozy wsi, pola mieszczan)
        /// <summary>MarketCarts.SellPrefix (sprzedaz wozu wsi w osadzie, przed gra i BK): drewno w jukach przed sprzedaza - tylko miasta, tylko przy kluczu limitu.</summary>
        internal static void FwCartPre(Settlement st, MobileParty mp)
        {
            _fwCart = null;
            try
            {
                if (st == null || mp == null || st.Town == null || !st.Town.IsTown || !FwMeasureOn) return;
                var w = FwWood(); var r = mp.ItemRoster;
                if (w == null || r == null) return;
                _fwCart = mp; _fwCart0 = r.GetItemNumber(w);
            }
            catch (Exception e) { _fwCart = null; _opStumbles++; Stumble("FwCartPre", e); }
        }

        /// <summary>MarketCarts.SellPostfix (po sprzedazy gry/BK i zwrocie nadplaty 119, PRZED zakupami wsi przez woz - CartBuys): sprzedane drewno = juki przed - po.</summary>
        internal static void FwCartPost(Settlement st, MobileParty mp)
        {
            var c = _fwCart; _fwCart = null;
            if (c == null || !ReferenceEquals(c, mp) || st == null) return;
            try
            {
                var w = FwWood(); var r = mp.ItemRoster;
                if (w == null || r == null) return;
                int sold = _fwCart0 - r.GetItemNumber(w);
                if (sold <= 0) return;
                float v; _fwDay.TryGetValue(st, out v); _fwDay[st] = v + sold;
                _fwCartPcs += sold;
            }
            catch (Exception e) { _opStumbles++; Stumble("FwCartPost", e); }
        }

        /// <summary>StartCalibration.DeliverTownFields: drewno z pol mieszczan KS polozone na polke miasta (zaplacone) - dowoz tego miasta.</summary>
        internal static void FwFieldArrive(Settlement st, int n)
        {
            if (st == null || n <= 0 || !FwMeasureOn) return;
            try { float v; _fwDay.TryGetValue(st, out v); _fwDay[st] = v + n; _fwFieldPcs += n; }
            catch (Exception e) { _opStumbles++; Stumble("FwFieldArrive", e); }
        }

        // ------------------------------------------------------------ doba opalu (z FirewoodDaily)
        /// <summary>Poczatek doby opalu: null, gdy zaden klucz paczki nie czynny (stara droga co do bitu); przy limicie - Plan na zapas pierscienia.</summary>
        private static OpalDay OpalBegin()
        {
            var s = Settings.Current;
            if (s == null || !(s.FirewoodKsSeason || s.FirewoodKsSupplyCap || s.FirewoodKsPriceDemand || s.FirewoodMineWoodKeep)) { OpalIdle(); return null; }
            var op = new OpalDay { Season = s.FirewoodKsSeason, Cap = s.FirewoodKsSupplyCap, Price = s.FirewoodKsPriceDemand, Keep = s.FirewoodMineWoodKeep, T0 = Stopwatch.GetTimestamp() };
            op.Wired = MarketCarts.SellWired;   // bez latki sprzedazy wozu pierscien mialby same zera z wozow - limit wtedy z Planu
            if (op.Cap)
            {
                op.Full = !_fwPartial; _fwPartial = false;
                try { op.Plan = StartCalibration.OpalPlanWood(); } catch (Exception e) { op.Plan = null; _opStumbles++; Stumble("OpalBegin(Plan)", e); }
            }
            else { _fwRing.Clear(); _fwDay.Clear(); _fwPartial = true; }
            return op;
        }

        /// <summary>Opal wylaczony albo kiesa nieczynna (FirewoodDaily wraca wczesniej): pierscien od nowa (po przerwie doba niepelna), popyt ceny bez reguly paczki.</summary>
        private static void OpalIdle()
        {
            if (_fwRing.Count > 0) _fwRing.Clear();
            if (_fwDay.Count > 0) _fwDay.Clear();
            _fwPartial = true;
            if (_fwEff.Count > 0) _fwEff = new Dictionary<Settlement, float>();
        }

        /// <summary>Kazde miasto doby opalu (przed regula zakupu): probka pierscienia dowozu (doba pelna) - takze miasta bez potrzeby.</summary>
        private static void OpalRoll(OpalDay op, Settlement st)
        {
            if (op == null || !op.Cap || st == null) return;
            FwRing r;
            if (!_fwRing.TryGetValue(st, out r)) { r = new FwRing(); _fwRing[st] = r; }
            if (!op.Full) return;
            float v; _fwDay.TryGetValue(st, out v);
            r.Push(v);
        }

        /// <summary>Potrzeba opalu miasta po regule paczki (ladunki/d): (1 - lambda) x dzis + min(lambda x opal KS x pora, limit dowozu); poza ZC4 - dzis (jak Zc4Firewood).
        /// k - czynnik pory po kroku 1 (Firewood Ks Season); bez limitu - ten sam wzor co StartCalibration.Zc4Firewood.</summary>
        private static double OpalNeed(OpalDay op, Town town, double old, double k)
        {
            double lam, fire;
            if (!StartCalibration.Zc4FireOf(town, out lam, out fire)) return old;
            if (!(k > 0) || double.IsNaN(k) || double.IsInfinity(k)) k = 1.0;
            double full = lam * fire * k, cut = full;
            op.Towns++; op.LamSum += lam; op.KSum += k; op.FireSum += lam * fire;
            if (op.Cap)
            {
                var st = town.Settlement;
                double d = double.NaN;
                FwRing r;
                if (op.Wired && _fwRing.TryGetValue(st, out r) && r.N >= FwWin) { d = r.Mean(); op.SrcMeas++; }
                else if (op.Plan != null && op.Plan.Count > 0) { double pd; op.Plan.TryGetValue(st, out pd); d = Math.Max(0.0, pd); op.SrcPlan++; }
                else op.SrcNone++;
                if (!double.IsNaN(d))
                {
                    double u = 0;
                    try { u = Math.Max(0f, CaravanBulk.CraftWoodUse(town)); } catch (Exception e) { _opStumbles++; Stumble("OpalNeed(rzemioslo)", e); }
                    // drewno, ktore juz lezy na polce ponad zapas rzemiosla (ten sam prog co zakup - TargetFor, ShelfKeep) - 1/7 na dobe: zapas z lata i jesieni ma odbiorce
                    // w zimie, a nadwyzka nie lezy bez konca (w stanie ustalonym polka = zapas rzemiosla i ten czlon to 0)
                    double stock = 0;
                    try
                    {
                        var shelf = st.ItemRoster; var w = FwWood();
                        if (shelf != null && w != null)
                        {
                            int have = shelf.GetItemNumber(w), keep = Math.Max(ShelfKeep, CaravanBulk.TargetFor(town, w));
                            if (have > keep) stock = (have - keep) / (double)FwWin;
                        }
                    }
                    catch (Exception e) { _opStumbles++; Stumble("OpalNeed(polka)", e); }
                    double cap = Math.Max(0.0, d - u) + stock;
                    op.DSum += d; op.USum += u; op.StockSum += stock; op.CapSum += cap;
                    if (cut > cap) { cut = cap; op.Capped++; }
                    if (cut > cap + 1e-6) op.Over += cut - cap;   // kontrola (nie powinno sie zdarzyc)
                }
            }
            op.KFull += full; op.KCut += cut; op.OldPart += (1 - lam) * old;
            if (op.Price)
            {
                double eff = lam > 0 ? cut / lam : fire * k;
                if (!double.IsNaN(eff) && !double.IsInfinity(eff) && eff >= 0) { op.Eff[town.Settlement] = (float)eff; op.EffSum += eff; }
            }
            return (1 - lam) * old + cut;
        }

        /// <summary>Krok 3 do linii: o ile zapas rzemiosla (TargetFor) wzrosl o drewno dymarek wsi (raz na miasto na dobe).</summary>
        private static void OpalKeepNote(OpalDay op, Town town, ItemObject wood)
        {
            if (op == null || !op.Keep) return;
            try
            {
                float need; int extra = CaravanBulk.MineKeepExtra(town, wood, out need);
                if (need > 0) op.MineNeed += need;
                if (extra > 0) { op.KeepExtra += extra; op.KeepTowns++; }
            }
            catch (Exception e) { _opStumbles++; Stumble("OpalKeepNote", e); }
        }

        /// <summary>Koniec doby opalu: popyt ceny (podmiana slownika w calosci), linia "Opal pod KS (f4-opal)", pierscien - nowa doba.</summary>
        private static void OpalEnd(OpalDay op, int day, bool winter, bool summer, bool winterOff, double needAll, long want)
        {
            if (op == null) return;
            _fwEff = op.Price ? op.Eff : new Dictionary<Settlement, float>();
            if (op.Cap) _fwDay.Clear();
            try
            {
                double ms = (Stopwatch.GetTimestamp() - op.T0) * 1000.0 / Stopwatch.Frequency;
                int ringFull = 0; foreach (var r in _fwRing.Values) if (r.N >= FwWin) ringFull++;
                var sb = new StringBuilder(900);
                sb.Append("Opal pod KS (f4-opal): dzien ").Append(day)
                  .Append(" | klucze: pora ").Append(Yn(op.Season)).Append(", limit dowozu ").Append(Yn(op.Cap)).Append(", popyt ceny ").Append(Yn(op.Price)).Append(", zapas dymarek ").Append(Yn(op.Keep))
                  .Append(" | pora: ").Append(winter ? "ZIMA" : (summer ? "lato" : (winterOff ? "zima bez skutkow" : "wiosna albo jesien")))
                  .Append(", czynnik czesci KS srednio ").Append(op.Towns > 0 ? (op.KSum / op.Towns).ToString("0.00", Inv) : "-")
                  .Append(" | miasta ZC4 ").Append(op.Towns).Append(" (lambda srednio ").Append(op.Towns > 0 ? (op.LamSum / op.Towns).ToString("0.00", Inv) : "-").Append(")")
                  .Append(" | czesc KS: opal x lambda ").Append(F2(op.FireSum)).Append(", z pora ").Append(F2(op.KFull)).Append(", po limicie ").Append(F2(op.KCut))
                  .Append(" (obcieta ").Append(F2(op.KFull - op.KCut)).Append(" w ").Append(op.Capped).Append(" miastach)")
                  .Append(" | limit = dowoz - rzemioslo + polka ponad zapas rzemiosla / ").Append(FwWin).Append(": dowoz ").Append(F2(op.DSum)).Append(" ladunkow/d (zmierzony ").Append(FwWin).Append(" dob w ").Append(op.SrcMeas)
                  .Append(" miastach, Plan w ").Append(op.SrcPlan).Append(", bez danych ").Append(op.SrcNone).Append("), rzemioslo ").Append(F2(op.USum)).Append(", polka ponad zapas / ").Append(FwWin).Append(' ').Append(F2(op.StockSum))
                  .Append(", limit ").Append(F2(op.CapSum))
                  .Append(" | potrzeba opalu razem ").Append(F2(needAll)).Append(" (po zaokragleniu ").Append(want).Append("; czesc dzisiejsza (1 - lambda) ").Append(F2(op.OldPart)).Append(")")
                  .Append(" | popyt ceny drewna (opal przy pelnej lambdzie): ").Append(op.Price ? "po regule " + F2(op.EffSum) + " w " + op.Eff.Count + " miastach" : "pelny (klucz NIE)")
                  .Append(" | zapas rzemiosla + drewno dymarek wsi: ").Append(op.Keep ? "+" + N0(op.KeepExtra) + " ladunkow w " + op.KeepTowns + " miastach (potrzeba dymarek ponad las " + F2(op.MineNeed) + " ladunkow/d)" : "NIE")
                  .Append(" | dowoz zmierzony od ostatniej linii: wozy wsi ").Append(_fwCartPcs).Append(", pola mieszczan ").Append(_fwFieldPcs).Append(" (pierscienie pelne ").Append(ringFull).Append('/').Append(_fwRing.Count)
                  .Append(op.Cap && !op.Full ? ", doba niepelna - bez probki" : "").Append(op.Cap && !op.Wired ? ", BRAK LATKI sprzedazy wozu - limit z Planu" : "").Append(")")
                  .Append(" | kontrola: po limicie ponad limit ").Append(F2(op.Over)).Append(" (0), z pora - po limicie ").Append(F2(op.KFull - op.KCut)).Append(" >= 0")
                  .Append(" | pieniadze i towar: bez nowych przelewow i ruchu (zakup i spalenie - linia N2-E4)")
                  .Append(" | czas ").Append(ms.ToString("0.0", Inv)).Append(" ms | potkniecia ").Append(_opStumbles).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { _opStumbles++; Stumble("OpalEnd", e); }
            _fwCartPcs = 0; _fwFieldPcs = 0;
        }

        /// <summary>StartCalibration.Zc4Demand (popyt ceny, moze isc z watkow AI - sam odczyt slownika podmienianego w calosci): opal KS miasta po regule paczki
        /// (ladunki/d przy pelnej lambdzie); false - brak (klucz wylaczony, miasto spoza doby opalu) - popyt ceny jak dotad.</summary>
        internal static bool FwEffOf(Settlement st, out double eff)
        {
            eff = 0;
            if (st == null) return false;
            var d = _fwEff; float v;
            if (d == null || d.Count == 0 || !d.TryGetValue(st, out v)) return false;
            eff = v; return true;
        }
    }
}
