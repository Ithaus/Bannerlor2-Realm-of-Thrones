using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// GL-2 / GL-3 (projekt docs/gospodarka-od-ludzi/PROJEKT-GOSPODARKA-OD-LUDZI-2026-10-13.md rozdz. 3.3, 5, 6, 8, 9; paczki P2 i P3) - RYNEK MIASTA OD LUDZI,
    /// GLOWY WSI I PODZAMCZA OD LUDZI oraz ZLOTO STARTOWE WEDLUG LUDZI (decyzja Jeffa 13.10 pyt. 7). Skale bierze z uslugi GL-0 (PeopleScale - pamiec doby,
    /// odczyt z dowolnego watku); Try* = false (usluga nie gotowa, wies bez przelicznika, potkniecie, wyjatek Strazy) = dzisiejszy wzor gry.
    ///
    /// P2 (klucz MarketByPeople) - jedna skala m_t na jedno zjawisko "ile ludzi kupuje w tym miescie", w pieciu miejscach:
    ///  (a) glowy kiesy ludu = srednia swiata glow BK miast x m_t / mianownik (PeopleScale.HeadNorm), w proporcjach klas BK z kalibracji (TownFolk.Scale ->
    ///      PeopleHeads); suma swiata glow w dniu kalibracji = glowy BK miast pod regula (mianownik = srednie m_t pod regula z tego dnia);
    ///  (b) popyt u ZRODLA: wynik GetDailyDemandForCategory x m_t (HistoricalPrices.DemandPostfix; zamek x skala podzamcza) - karmi slownik budzetu dnia
    ///      (UpdateDemandShift), popyt wzoru ceny (GetEstimatedDemandForCategory, wygladzany co dobe) i czesc domowa RawPrice.UseDemand; czlon dobrobytu
    ///      wiersza BK (dobrobyt / 1000 / indeks) x m_t w TownFolk.AnchorBudgetPostfix - takze poza nawiasem konsumpcji (prognoza targu zamku, RawPrice.HouseBudget).
    ///      Kotwica min(wydatek gry, koszyk) bez zmian - wydatek gry przychodzi juz przeskalowany. NOWA GALAZ "W GORE" (zasada 4): czlon dobrobytu wiersza
    ///      rosnie tylko tam, gdzie m x czynnik kotwicy > 1 - licznik "kotwica w gore" i czas latki w linii doby;
    ///  (c) zapas kupcow x m_t - TownPurse.Reserve (zawor, podatki, nasze zakupy) i Seal31.TownReserve (pomiary, dwie dawne kopie wzoru w kodzie logu:
    ///      Measure169c, MoneyLedger.Obieg) razem; dar startowy przycina sie wobec zapasu PLASKIEGO (TownPurse.ReserveFlat) - przestawienie wedlug ludzi robi zloto startowe;
    ///  (d) rece warsztatow notabli 6 roboczodni x m_t (WorkshopLaw.Hands - kazdy warsztat liczony tym planem, takze gracza);
    ///  (e) NEUTRALIZACJA licznika Food warowni: skladnik "zywnosc sprzedana mieszczanom" (Town.SoldItems z wlasciwoscia BonusToFoodStores, jak gra
    ///      DefaultSettlementFoodModel :82-91; model BK wola model gry - BKFoodModel :46-59) x (1 / m - 1) dopisany postfiksem - licznik zostaje w swoich
    ///      jednostkach. Bez korekty (sztuki 1:1 jak w grze): oblezona warownia (gra dopisuje wtedy do SoldItems jedzenie z polki do zapasu) i warownia z zapasem
    ///      <= 10% (BK dokupuje jedzenie na brak wedlug FoodChange, nie popytu) - korekta x 1/m robilaby z tych sztuk jedzenie z niczego.
    /// P3 (klucz VillageHeadsByPeople): glowy wsi (oplata stala panu, zakupy wsi w miescie targowym, prog daniny wsi) = srednia swiata glow BK wsi x skala DOMU
    ///  wsi (bez uchodzcow - recenzja kodu D2) / mianownik (TownFolk.VillHeads); glowy podzamcza = srednia glow podzamczy x skala podzamcza / mianownik (TownFolk.CastleHeads).
    /// Zloto startowe (klucz StartGoldByPeople, TYLKO nowa kampania - flaga w zapisie arm_peoplemarket): po darach startowych (TownPurse.TrimStartGift
    ///  z kiesami ludu i wsi, ArmouryBehavior: zaraz po TownPurse.BeforeRents, przed zaworem) pule przelane PRAWDZIWYM zlotem miedzy posiadaczami
    ///  tego samego rodzaju, suma kazdej puli co do zlotowki ta sama: kasy kupcow miast wedlug zapasu plaskiego x m_t i kiesy ludu W/R/P wedlug koszyka
    ///  klasy x m_t (TYLKO przy MarketByPeople - zapas kupcow i glowy miast ida wtedy x m_t), kiesy wsi wedlug skali domu wsi (TYLKO przy VillageHeadsByPeople -
    ///  glowy wsi), skarbce koron wedlug ludzi krolestwa z podloga CrownFloor (zawsze przy StartGoldByPeople); kiesy podzamczy wedlug skali podzamcza (TYLKO
    ///  przy VillageHeadsByPeople) - osobny krok tej samej doby PO darze zamkow (StartGoldCastles, po CastlePurse.Daily). Recenzja obiegu GL W1: bez kluczy
    ///  P2/P3 przelew wedlug ludzi rozjechalby zloto z plaskim zapasem i glowami (Oldtown ok. 14 x zapas, zawor oddalby to panom). Ksiega pieniadza nie widzi przelewow (te same klasy
    ///  posiadaczy); migawka skarbcow korony (CrownIncome) i okno udzialu kupcow (TownPurse) dostaja przesuniecie, zeby przelew nie byl "wplywem dnia".
    /// Wyjatki per osada (licznik potkniec), zadnych globalnych wylacznikow; odczyt skal bez liczenia ksiegi.
    /// </summary>
    internal static class PeopleMarket
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>DECYZJA (pelne uprawnienia, rachunek garnka sucho85/garnek: podloga 1.0 mln trzyma w pustych krolestwach 0.5 mln skrzyni wojennej ponad zapas korony).</summary>
        internal const long CrownFloor = 1000000;
        private const double StartMaxAge = 3.5;            // zloto startowe najpozniej w tej dobie kampanii (pamiec skal albo dary startowe jeszcze nie gotowe - czekamy)
        private const double BigMarket = 1.5;               // prog projektu rozdz. 8 [R]: "lud miast o skali rynku ponad 1.5"
        private static readonly TextObject _txtFood = new TextObject("{=!}Townsfolk purchases: counted for the people of the market");

        // ------------------------------------------------------------ klucze
        internal static bool MarketOn { get { var s = Settings.Current; return s != null && s.MarketByPeople; } }
        internal static bool HeadsOn { get { var s = Settings.Current; return s != null && s.VillageHeadsByPeople; } }
        private static bool StartOn { get { var s = Settings.Current; return s != null && s.StartGoldByPeople; } }

        // ------------------------------------------------------------ stan
        private static int _start = -1;                     // zloto startowe: -1 nieznane, 0 do zrobienia (nowa kampania), 1 zrobione, 2 stary zapis, 3 wylaczone przy starcie, 4 nie zdazylo
        private static int _startDay = -1;
        private static bool _startWaitLogged, _foodPatched, _castlePending;
        private static string _startLine;
        // liczniki okna doby (watek glowny - nawias konsumpcji TownFolk)
        private static double _bT0, _bT1, _bC0, _bC1; private static int _bN, _upT, _upC;
        // potkniecia (watki AI - Interlocked) i praca czlonu dobrobytu wiersza
        private static int _stumbles, _readStumbles;
        private static long _anchorTicks; private static int _anchorN;
        private static readonly HashSet<string> _err = new HashSet<string>();
        private static bool _weekDone;

        internal static void Reset()
        {
            _start = -1; _startDay = -1; _startWaitLogged = false; _startLine = null; _castlePending = false;
            _bT0 = _bT1 = _bC0 = _bC1 = 0; _bN = _upT = _upC = 0;
            _stumbles = 0; _readStumbles = 0; _anchorTicks = 0; _anchorN = 0; _err.Clear(); _weekDone = false;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) { if (e != null) Log.Error("PeopleMarket." + where, e); else Log.Info("Rynek od ludzi (GL2): potkniecie - " + where + "."); } } catch { }
        }

        private static void ReadStumble(string where, Exception e)
        {
            if (Interlocked.Increment(ref _readStumbles) == 1) { try { Log.Error("PeopleMarket." + where, e); } catch { } }
        }

        // ============================================================ skale dla latek (odczyt z dowolnego watku, nie rzuca)

        /// <summary>P2 (b): skala popytu osady - miasto m_t, zamek skala podzamcza; 1 = klucz wylaczony, usluga nie gotowa, wyjatek Strazy albo inna osada.</summary>
        internal static float DemandScale(Town t)
        {
            try
            {
                if (t == null || !MarketOn) return 1f;
                var st = t.Settlement;
                if (st == null) return 1f;
                float k;
                if (st.IsTown) return PeopleScale.TryMarket(t, out k) && k > 0f && !float.IsInfinity(k) ? k : 1f;
                if (st.IsCastle) return PeopleScale.TryCastleWard(st, out k) && k > 0f && !float.IsInfinity(k) ? k : 1f;
                return 1f;
            }
            catch (Exception e) { ReadStumble("DemandScale", e); return 1f; }
        }

        /// <summary>P2 (c): skala zapasu kupcow MIASTA (m_t); false = zapas plaski (klucz wylaczony, nie miasto, brak skali).</summary>
        internal static bool ReserveScale(Town t, out float m)
        {
            m = 1f;
            try
            {
                if (t == null || !MarketOn || t.Settlement == null || !t.Settlement.IsTown) return false;
                return PeopleScale.TryMarket(t, out m) && m > 0f && !float.IsInfinity(m);
            }
            catch (Exception e) { ReadStumble("ReserveScale", e); m = 1f; return false; }
        }

        /// <summary>P2 (d): mnoznik rak warsztatu notabla w miescie (m_t); 1 = klucz wylaczony albo brak skali.</summary>
        internal static float WorkersScale(Town t)
        {
            float m;
            return ReserveScale(t, out m) ? m : 1f;
        }

        // ============================================================ (e) licznik Food warowni
        internal static void ApplyAll(Harmony h)
        {
            string wired = "-", missing = "-";
            try
            {
                var m = AccessTools.Method(typeof(DefaultSettlementFoodModel), "CalculateTownFoodStocksChange");
                if (m != null && m.ReturnType == typeof(ExplainedNumber))
                {
                    h.Patch(m, postfix: new HarmonyMethod(typeof(PeopleMarket), nameof(FoodPostfix)));
                    _foodPatched = true; wired = "licznik Food warowni - skladnik sprzedane mieszczanom x 1/m (DefaultSettlementFoodModel.CalculateTownFoodStocksChange)";
                }
                else missing = "DefaultSettlementFoodModel.CalculateTownFoodStocksChange";
            }
            catch (Exception e) { missing = "CalculateTownFoodStocksChange (" + e.GetType().Name + ")"; Log.Error("PeopleMarket.ApplyAll", e); }
            Log.Info("Rynek od ludzi (GL2): latki przy starcie gry - wpiete: " + wired + "; BRAK: " + missing + " (popyt u zrodla - HistoricalPrices.DemandPostfix, czlon dobrobytu wiersza - TownFolk.AnchorBudgetPostfix; liczy przy kluczu MarketByPeople).");
        }

        /// <summary>
        /// Postfiks na DefaultSettlementFoodModel.CalculateTownFoodStocksChange (addytywny, obok VillageBook, WinterBite, Famine): skladnik "sprzedane mieszczanom"
        /// x (1 / m - 1), m = skala popytu warowni (DemandScale). Tylko z includeMarketStocks (jak gra), bez oblezenia. Moze isc z watkow AI - sam odczyt. Nie rzuca.
        /// </summary>
        public static void FoodPostfix(Town town, bool includeMarketStocks, ref ExplainedNumber __result)
        {
            if (!_foodPatched || town == null || !includeMarketStocks) return;
            try
            {
                if (!MarketOn || town.IsUnderSiege || LowStock(town)) return;
                float k = DemandScale(town);
                if (k == 1f || !(k > 0f)) return;
                int sold = SoldFood(town);
                if (sold <= 0) return;
                float adj = sold * (1f / k - 1f);
                if (Math.Abs(adj) > 0.001f && !float.IsNaN(adj) && !float.IsInfinity(adj)) __result.Add(adj, _txtFood);
            }
            catch (Exception e) { ReadStumble("FoodPostfix", e); }
        }

        /// <summary>Zapas warowni <= 10% gornej granicy: BK dokupuje wtedy jedzenie z polki na brak (EconomyPatches :532, ilosc z FoodChange, nie z popytu) - te sztuki
        /// ida do licznika 1:1 jak w grze (korekta x 1/m robilaby z nich jedzenie z niczego albo je kasowala).</summary>
        private static bool LowStock(Town t)
        {
            try { return t.FoodStocks <= t.FoodStocksUpperLimit() * 0.1f; } catch { return false; }
        }

        /// <summary>Jedzenie sprzedane mieszczanom w ostatnim ticku osady - ten sam skladnik, co w liczniku gry (BonusToFoodStores).</summary>
        private static int SoldFood(Town t)
        {
            var l = t.SoldItems;
            if (l == null) return 0;
            int n = 0;
            foreach (var x in l) if (x.Category != null && x.Category.Properties == ItemCategory.Property.BonusToFoodStores && x.Number > 0) n += x.Number;
            return n;
        }

        // ============================================================ liczniki z nawiasu konsumpcji (watek glowny)

        /// <summary>TownFolk.DemandPostfix (nawias konsumpcji osady, watek glowny): budzet dnia po skali (suma slownika) i czynniki kotwicy tej doby.</summary>
        internal static void NoteBracket(Town t, double sumAfter, double ff, double fr)
        {
            try
            {
                if (t == null || t.Settlement == null) return;
                float k = DemandScale(t);
                double before = k > 0f ? sumAfter / k : sumAfter;
                bool up = k * Math.Max(ff, fr) > 1.0 + 1e-9;
                if (t.Settlement.IsTown) { _bT0 += before; _bT1 += sumAfter; if (up) _upT++; }
                else { _bC0 += before; _bC1 += sumAfter; if (up) _upC++; }
                _bN++;
            }
            catch (Exception e) { Stumble("NoteBracket", e); }
        }

        /// <summary>TownFolk.AnchorBudgetPostfix: czas galezi skali czlonu dobrobytu wiersza (z dowolnego watku).</summary>
        internal static void NoteAnchorWork(long ticks)
        {
            Interlocked.Add(ref _anchorTicks, ticks);
            Interlocked.Increment(ref _anchorN);
        }

        // ============================================================ doba (watek glowny, po PeopleScale.Daily)
        private static string F(double x, string fmt) { return double.IsNaN(x) || double.IsInfinity(x) ? "-" : x.ToString(fmt, Inv); }
        private static string N0(double x) { return F(x, "0"); }

        private static string Spread(List<float> l, string fmt)
        {
            if (l.Count == 0) return "-";
            l.Sort();
            return F(l[0], fmt) + "/" + F(l[l.Count / 2], fmt) + "/" + F(l[l.Count - 1], fmt);
        }

        private static string Name(Settlement s) { try { return s != null && s.Name != null ? s.Name.ToString() : "?"; } catch { return "?"; } }
        private static string KName(IFaction f) { try { return f != null && f.Name != null ? f.Name.ToString().Replace(';', ',').Replace('|', '/') : "bez krolestwa"; } catch { return "?"; } }

        internal static void Daily()
        {
            try { DayLine(); } catch (Exception e) { Stumble("Daily", e); }
            _bT0 = _bT1 = _bC0 = _bC1 = 0; _bN = _upT = _upC = 0;
            Interlocked.Exchange(ref _anchorTicks, 0); Interlocked.Exchange(ref _anchorN, 0);
        }

        private static string StartState()
        {
            switch (_start)
            {
                case 0: return "czeka (nowa kampania)";
                case 1: return "zrobione (dzien " + _startDay + ")";
                case 2: return "nie dotyczy (zapis kampanii w toku)";
                case 3: return "wylaczone przy starcie kampanii";
                case 4: return "nie zdazylo (skale albo dary startowe niegotowe do doby " + StartMaxAge.ToString("0.0", Inv) + ")";
                default: return "nieznane";
            }
        }

        private static void DayLine()
        {
            int day = (int)CampaignTime.Now.ToDays;
            bool on = MarketOn, heads = HeadsOn;
            var sb = new StringBuilder(1400);
            sb.Append("Rynek od ludzi (GL2): dzien ").Append(day).Append(" - MarketByPeople ").Append(on ? "tak" : "nie").Append(", VillageHeadsByPeople ").Append(heads ? "tak" : "nie")
              .Append(", StartGoldByPeople ").Append(StartOn ? "tak" : "nie").Append(", pamiec skal dnia ").Append(PeopleScale.MemoryDay);
            // m_t, zapas kupcow, rece, licznik Food
            var mt = new List<float>(); var wd = new List<float>();
            double r0 = 0, r1 = 0, hands = 0; int below = 0, nT = 0, siege = 0, low = 0, nAdj = 0;
            double sold = 0, plus = 0, minus = 0;
            float wk = 0f;
            try { var s = Settings.Current; wk = s != null ? Math.Max(0.1f, s.WorkshopWorkers) : 6f; } catch { wk = 6f; }
            foreach (var t in Town.AllFiefs)
            {
                if (t == null || t.Settlement == null) continue;
                try
                {
                    float k = DemandScale(t), m;
                    bool town = t.Settlement.IsTown;
                    if (town)
                    {
                        nT++;
                        if (on && PeopleScale.TryMarket(t, out m)) mt.Add(m);
                        long flat = TownPurse.ReserveFlat(t), now = TownPurse.Reserve(t);
                        r0 += flat; r1 += now;
                        if (t.Gold < now) below++;
                        hands += wk * WorkersScale(t);
                    }
                    else if (on && PeopleScale.TryCastleWard(t.Settlement, out m)) wd.Add(m);
                    int sf = SoldFood(t);
                    sold += sf;
                    if (on && k != 1f && sf > 0)
                    {
                        if (t.IsUnderSiege) siege++;
                        else if (LowStock(t)) low++;
                        else { double a = sf * (1.0 / k - 1.0); if (a > 0) plus += a; else minus -= a; nAdj++; }
                    }
                }
                catch (Exception e) { Stumble("DayLine(warownia)", e); }
            }
            sb.Append(" | m_t miast pod regula ").Append(mt.Count).Append(" min/med/max ").Append(Spread(mt, "0.00"))
              .Append(", podzamcza pod regula ").Append(wd.Count).Append(" min/med/max ").Append(Spread(wd, "0.00"));
            sb.Append(" | popyt x m_t (budzet dnia z nawiasu konsumpcji): swiat przed ").Append(N0(_bT0 + _bC0)).Append(" / po ").Append(N0(_bT1 + _bC1))
              .Append(" (miasta ").Append(N0(_bT0)).Append(" / ").Append(N0(_bT1)).Append(", zamki ").Append(N0(_bC0)).Append(" / ").Append(N0(_bC1)).Append(", nawiasow ").Append(_bN)
              .Append("), kotwica w gore ").Append(_upT).Append(" miast / ").Append(_upC).Append(" zamkow");
            sb.Append(" | zapas kupcow x m_t: swiat przed ").Append(N0(r0)).Append(" / po ").Append(N0(r1)).Append(", miast z kasa ponizej zapasu ").Append(below).Append(" z ").Append(nT)
              .Append(" | rece warsztatow notabli ").Append(F(wk, "0.#")).Append(" x m_t: srednio ").Append(nT > 0 ? F(hands / nT, "0.00") : "-");
            sb.Append(" | licznik Food zneutralizowany +").Append(F(plus, "0.0")).Append(" / -").Append(F(minus, "0.0")).Append(" szt. w ").Append(nAdj)
              .Append(" warowniach (sprzedane mieszczanom ").Append(N0(sold)).Append(" szt.; bez korekty: oblezone ").Append(siege).Append(", zapas <= 10% ").Append(low).Append(')');
            // glowy (TownFolk)
            try
            {
                double tO, tN, vO, vN, cO, cN;
                TownFolk.HeadsWorld(out tO, out tN, out vO, out vN, out cO, out cN);
                sb.Append(" | glowy przed / po: miasta ").Append(N0(tO)).Append(" / ").Append(N0(tN)).Append(", wsie ").Append(N0(vO)).Append(" / ").Append(N0(vN))
                  .Append(", podzamcza ").Append(N0(cO)).Append(" / ").Append(N0(cN));
            }
            catch (Exception e) { Stumble("DayLine(glowy)", e); }
            int an = Interlocked.CompareExchange(ref _anchorN, 0, 0);
            long at = Interlocked.Read(ref _anchorTicks);
            sb.Append(" | czlon dobrobytu wiersza x m: wywolan ").Append(an).Append(", czas ").Append(F(at * 1000.0 / Stopwatch.Frequency, "0.0")).Append(" ms")
              .Append(" | zloto startowe: ").Append(StartState())
              .Append(" | potkniecia ").Append(_stumbles + Interlocked.Exchange(ref _readStumbles, 0)).Append('.');
            _stumbles = 0;
            Log.Info(sb.ToString());
            if (_startLine != null) { Log.Info(_startLine); _startLine = null; }
            bool week = day % 7 == 0 || !_weekDone;
            if (!week) return;
            _weekDone = true;
            try { WeekLine(day); } catch (Exception e) { Stumble("WeekLine", e); }
        }

        private sealed class KAcc { public string Name; public int Towns; public double M, Spend, Need, Aff, Purse, Gold, Reserve, Shelf, Sold; }

        // recenzja obiegu GL W3 (pomiar, bez zmiany gry): wyroby linii towarowych cyklu gry (piekarnie, browary, winiarnie, tlocznie, tkalnie, garbarnie, kuznie
        // narzedzi) - rece x m_t dzialaja tylko w planie linii uzbrojenia (WorkshopLaw.Hands), te cykle zostaja plaskie, a popyt na ich wyroby idzie x m_t
        private static readonly string[] Wares = { "bread", "pie", "beer", "wine", "oil", "felt", "linen", "leather", "tools" };
        private sealed class WAcc { public int Towns, Empty; public double Units; public readonly int[] EmptyBy = new int[Wares.Length]; }

        /// <summary>Jedzenie na polce miasta (sztuki z BonusToFoodStores); `ware` - sztuki wyrobow Wares (ten sam przebieg po polce).</summary>
        private static int ShelfFood(Town t, int[] ware)
        {
            var r = t.Owner != null ? t.Owner.ItemRoster : null;
            if (r == null) return 0;
            int n = 0;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                var c = it != null ? it.ItemCategory : null;
                if (c != null && c.Properties == ItemCategory.Property.BonusToFoodStores && el.Amount > 0) n += el.Amount;
                if (ware != null && it != null && el.Amount > 0) { int j = Array.IndexOf(Wares, it.StringId); if (j >= 0) ware[j] += el.Amount; }
            }
            return n;
        }

        private static string WareText(WAcc a)
        {
            var sb = new StringBuilder(200);
            sb.Append(a.Empty).Append(" z ").Append(a.Towns * Wares.Length).Append(" [");
            for (int j = 0; j < Wares.Length; j++) { if (j > 0) sb.Append(", "); sb.Append(Wares[j]).Append(' ').Append(a.EmptyBy[j]); }
            sb.Append("], sztuk tych wyrobow na polce na miasto ").Append(a.Towns > 0 ? F(a.Units / a.Towns, "0") : "-");
            return sb.ToString();
        }

        private static void WeekLine(int day)
        {
            var rows = TownFolk.MarketTowns();
            var acc = new Dictionary<string, KAcc>();
            var big = new KAcc { Name = "m_t > 1.5" };
            var wBig = new WAcc(); var wSmall = new WAcc();
            var xs = new List<double>(); var ys = new List<double>();
            foreach (var r in rows)
            {
                try
                {
                    var t = r.St != null ? r.St.Town : null;
                    if (t == null) continue;
                    var f = r.St.MapFaction;
                    string id = f != null && f.StringId != null ? f.StringId : "-";
                    KAcc k;
                    if (!acc.TryGetValue(id, out k)) { k = new KAcc { Name = KName(f) }; acc[id] = k; }
                    // m_t uslugi skal NIEZALEZNIE od klucza (recenzja obiegu GL: ten sam podzial miast w biegu bazowym z kluczami wylaczonymi - progi GL2-8/8b/11
                    // wobec bazy); Straz i miasto bez skali - 1. Tylko log.
                    float m = 1f;
                    bool svc = PeopleScale.TryMarket(t, out m);
                    if (!svc) m = 1f;
                    var ware = new int[Wares.Length];
                    k.Towns++; k.M += m; k.Spend += r.Spend; k.Need += r.Need; k.Aff += r.Aff * r.Need; k.Purse += r.Purse;
                    k.Gold += Math.Max(0, t.Gold); k.Reserve += TownPurse.Reserve(t); k.Shelf += ShelfFood(t, ware); k.Sold += SoldFood(t);
                    if (svc && m > BigMarket) { big.Towns++; big.Need += r.Need; big.Aff += r.Aff * r.Need; big.Purse += r.Purse; }
                    var wa = svc && m > BigMarket ? wBig : (svc && m < 0.5f ? wSmall : null);
                    if (wa != null)
                    {
                        wa.Towns++;
                        for (int j = 0; j < Wares.Length; j++) { wa.Units += ware[j]; if (ware[j] <= 0) { wa.Empty++; wa.EmptyBy[j]++; } }
                    }
                    float p;
                    if (svc && PeopleScale.TryPeople(r.St, out p)) { xs.Add(r.Spend); ys.Add(p); }
                }
                catch (Exception e) { Stumble("WeekLine(miasto)", e); }
            }
            var list = new List<KAcc>(acc.Values);
            list.Sort((a, b) => b.Spend.CompareTo(a.Spend));
            var sb = new StringBuilder(4000);
            sb.Append("Rynek od ludzi wedlug krolestw (GL2): dzien ").Append(day).Append(" - ");
            for (int i = 0; i < list.Count; i++)
            {
                var k = list[i];
                if (i > 0) sb.Append(" | ");
                sb.Append(k.Name).Append(": miast ").Append(k.Towns).Append(", m_t sr. ").Append(F(k.M / Math.Max(1, k.Towns), "0.00"))
                  .Append(", wydatki ludu ").Append(N0(k.Spend)).Append(" zl/d, stac na koszyk ").Append(k.Need > 0 ? F(100.0 * k.Aff / k.Need, "0") : "-").Append('%')
                  .Append(", dni oszczednosci ").Append(k.Need > 0 ? F(k.Purse / k.Need, "0") : "-")
                  .Append(", zapas kupcow / kasa ").Append(N0(k.Reserve)).Append(" / ").Append(N0(k.Gold)).Append(" (").Append(k.Reserve > 0 ? F(k.Gold / k.Reserve, "0.00") : "-").Append(')')
                  .Append(", jedzenie na polkach ").Append(k.Sold > 0 ? F(k.Shelf / k.Sold, "0.0") : "-").Append(" dob");
            }
            sb.Append(" || miasta o m_t > ").Append(BigMarket.ToString("0.0", Inv)).Append(": ").Append(big.Towns).Append(", stac na koszyk ").Append(big.Need > 0 ? F(100.0 * big.Aff / big.Need, "0") : "-").Append('%')
              .Append(", dni oszczednosci ").Append(big.Need > 0 ? F(big.Purse / big.Need, "0") : "-")
              .Append(" || korelacja wydatkow ludu z ludzmi rynku r = ").Append(xs.Count >= 3 ? F(Pearson(xs, ys), "0.00") : "-").Append(" (miast ").Append(xs.Count).Append(")")
              .Append(" || wyroby linii towarowych cyklu gry na polkach - miasta o m_t > ").Append(BigMarket.ToString("0.0", Inv)).Append(" (").Append(wBig.Towns).Append("): bez towaru (par miasto x wyrob) ").Append(WareText(wBig))
              .Append("; miasta o m_t < 0.5 (").Append(wSmall.Towns).Append("): bez towaru ").Append(WareText(wSmall))
              .Append(" (m_t = skala uslugi takze przy wylaczonym kluczu; wydatki ludu = wydatek gry w konsumpcji doby, stac = kiesa wobec pelnej potrzeby, dni = kiesy W/R/P / potrzeba dzienna, jedzenie = sztuki na polkach / sprzedane mieszczanom w dobie).");
            Log.Info(sb.ToString());
        }

        private static double Pearson(List<double> x, List<double> y)
        {
            int n = Math.Min(x.Count, y.Count);
            if (n < 2) return double.NaN;
            double mx = 0, my = 0;
            for (int i = 0; i < n; i++) { mx += x[i]; my += y[i]; }
            mx /= n; my /= n;
            double sxy = 0, sxx = 0, syy = 0;
            for (int i = 0; i < n; i++) { double a = x[i] - mx, b = y[i] - my; sxy += a * b; sxx += a * a; syy += b * b; }
            return sxx > 0 && syy > 0 ? sxy / Math.Sqrt(sxx * syy) : double.NaN;
        }

        // ============================================================ zloto startowe wedlug ludzi (tylko nowa kampania)

        /// <summary>CampaignEvents.OnNewGameCreatedEvent: ta sesja zaklada kampanie - zloto startowe do zrobienia (flaga pojdzie do zapisu).</summary>
        internal static void OnNewGame() { _start = 0; }

        /// <summary>Zapis arm_peoplemarket: "1|stan|dzien" (stan jak _start).</summary>
        internal static string Export() { return "1|" + _start.ToString(Inv) + "|" + _startDay.ToString(Inv); }

        /// <summary>Wczytanie: brak pola (zapis sprzed paczki) = kampania w toku - zloto startowe nigdy; inny format - tak samo.</summary>
        internal static void Import(string v)
        {
            _start = 2; _startDay = -1;
            if (string.IsNullOrEmpty(v)) return;
            try
            {
                var p = v.Split('|');
                if (p.Length < 3 || p[0] != "1") return;
                int st = int.Parse(p[1], Inv);
                _startDay = int.Parse(p[2], Inv);
                // zapis nowej kampanii sprzed pierwszej doby (st 0) - zloto startowe dalej do zrobienia; -1 - jak stary zapis
                _start = st >= 0 && st <= 4 ? st : 2;
            }
            catch (Exception e) { _start = 2; Stumble("Import", e); }
        }

        private static double CampaignAge()
        {
            try { return (CampaignTime.Now - Campaign.Current.Models.CampaignTimeModel.CampaignStartTime).ToDays; }
            catch { return double.NaN; }
        }

        /// <summary>
        /// Z ArmouryBehavior.OnDailyTickBody zaraz po TownPurse.BeforeRents (dar startowy przyciety, kiesy ludu i wsi z daru), przed PopulationLaw.Daily (zawor).
        /// Raz na kampanie: flaga PRZED robota - przerwanego przelewu nie powtarzamy.
        /// </summary>
        internal static void StartGold()
        {
            if (_start != 0) { if (_start == -1) _start = 2; return; }   // -1: ani nowa gra, ani zapis z polem - traktujemy jak kampanie w toku (nigdy)
            double age = CampaignAge();
            if (!StartOn) { _start = 3; _startLine = "Zloto startowe wedlug ludzi (GL2): WYLACZONE przy starcie kampanii (StartGoldByPeople) - zloto kas, kies i skarbcow zostaje jak w grze; pozniejsze wlaczenie nic nie zmieni."; return; }
            if (double.IsNaN(age) || age > StartMaxAge)
            {
                _start = 4;
                _startLine = "Zloto startowe wedlug ludzi (GL2): NIE ZROBIONE - doba kampanii " + F(age, "0.00") + " ponad " + StartMaxAge.ToString("0.0", Inv)
                             + " (pamiec skal ludzi albo dary startowe nie byly gotowe); zloto zostaje jak w grze.";
                return;
            }
            bool ready = PeopleScale.Ready, trim = TownPurse.StartTrimSettled, seeds = !TownFolk.SeedTownsDue;
            if (!ready || !trim || !seeds)
            {
                if (!_startWaitLogged)
                {
                    _startWaitLogged = true;
                    Log.Info("Zloto startowe wedlug ludzi (GL2): czeka - doba kampanii " + F(age, "0.00") + ", pamiec skal ludzi " + (ready ? "tak" : "nie") + ", dar startowy kas miast rozliczony "
                             + (trim ? "tak" : "nie") + ", kiesy ludu z daru " + (seeds ? "tak" : "nie") + " (proba co dobe do doby " + StartMaxAge.ToString("0.0", Inv) + ").");
                }
                return;
            }
            _start = 1; _startDay = (int)CampaignTime.Now.ToDays;
            _castlePending = HeadsOn;                       // pula podzamczy: ta sama doba, po darze zamkow (StartGoldCastles)
            try { DoStart(age); }
            catch (Exception e) { Stumble("StartGold", e); }
        }

        /// <summary>
        /// Recenzja obiegu GL D4: pula podzamczy zlota startowego - kiesy podzamczy (dar zamkow SeedCastle w CastlePurse.Daily) przelane miedzy zamkami wedlug
        /// skali podzamcza, suma co do zlotowki ta sama. Z OnDailyTickBody zaraz po CastlePurse.Daily, tylko w dobie, w ktorej StartGold zrobil pozostale pule
        /// (pamiec, bez zapisu - obie metody w jednym ticku doby), tylko przy VillageHeadsByPeople (glowy podzamczy x skala podzamcza).
        /// </summary>
        internal static void StartGoldCastles()
        {
            if (!_castlePending) return;
            _castlePending = false;
            try
            {
                long c0, c1, moved; int n;
                TownFolk.StartCastleShift(out c0, out c1, out moved, out n);
                Log.Info("Zloto startowe wedlug ludzi (GL2): kiesy podzamczy - dzien " + (int)CampaignTime.Now.ToDays + ": przed " + c0 + " = po " + c1 + (c0 == c1 ? " (zgodne)" : " (ROZJAZD)")
                         + " (przelano " + moved + ", zamkow " + n + "; waga skala podzamcza; dar zamkow " + (TownFolk.SeedCastlesDue ? "jeszcze NIE rozliczony - kiesy bez daru" : "rozliczony") + ").");
            }
            catch (Exception e) { Stumble("StartGoldCastles", e); }
        }

        /// <summary>Podzial `cur` wedlug wag `w` przy tej samej sumie (co do zlotowki): czesci calkowite, reszta zaokraglen do najwiekszej wagi. Wagi 0 - cel 0.</summary>
        internal static long[] Spread(long[] cur, double[] w)
        {
            int n = cur.Length;
            var t = new long[n];
            long total = 0; double W = 0;
            for (int i = 0; i < n; i++) { total += cur[i]; if (w[i] > 0 && !double.IsInfinity(w[i])) W += w[i]; }
            if (!(W > 0) || total <= 0) { Array.Copy(cur, t, n); return t; }
            long given = 0; int best = -1; double bw = -1;
            for (int i = 0; i < n; i++)
            {
                double wi = w[i] > 0 && !double.IsInfinity(w[i]) ? w[i] : 0;
                t[i] = wi > 0 ? (long)Math.Floor(total * (wi / W)) : 0;
                given += t[i];
                if (wi > bw) { bw = wi; best = i; }
            }
            if (best >= 0) t[best] += total - given;
            return t;
        }

        /// <summary>Podzial skarbcow: cel = max(podloga, lambda x ludzie), suma = suma przed (podlogi wodne); gdy podloga x liczba >= suma - po rowno.</summary>
        private static long[] SpreadFloor(long[] cur, double[] people, long floor, out int onFloor)
        {
            int n = cur.Length;
            onFloor = 0;
            long total = 0; for (int i = 0; i < n; i++) total += cur[i];
            var fl = new bool[n];
            if (n == 0) return new long[0];
            if (floor * (long)n >= total)
            {
                var eq = new double[n]; for (int i = 0; i < n; i++) eq[i] = 1;
                onFloor = n;
                return Spread(cur, eq);
            }
            for (int pass = 0; pass < n + 1; pass++)
            {
                double rest = total - floor * (double)Count(fl), ps = 0;
                for (int i = 0; i < n; i++) if (!fl[i]) ps += Math.Max(0, people[i]);
                bool changed = false;
                for (int i = 0; i < n; i++)
                {
                    if (fl[i]) continue;
                    double x = ps > 0 ? rest * Math.Max(0, people[i]) / ps : 0;
                    if (x < floor) { fl[i] = true; changed = true; }
                }
                if (!changed) break;
            }
            onFloor = Count(fl);
            // czesc ponad podlogami wedlug ludzi, potem podlogi
            var w = new double[n]; var curRest = new long[n];
            long restAll = total - floor * (long)onFloor;
            for (int i = 0; i < n; i++) w[i] = fl[i] ? 0 : Math.Max(0, people[i]);
            curRest[0] = restAll;
            bool any = false; for (int i = 0; i < n; i++) if (w[i] > 0) any = true;
            if (!any) { for (int i = 0; i < n; i++) w[i] = 1; }       // nikt ponad podloga z ludzmi (krolestwa bez osad) - reszta po rowno
            var t = Spread(curRest, w);
            for (int i = 0; i < n; i++) if (fl[i]) t[i] += floor;
            return t;
        }

        private static int Count(bool[] b) { int n = 0; foreach (var x in b) if (x) n++; return n; }

        private sealed class KStart { public string Name; public long T0, T1, F0, F1, V0, V1, C0, C1; }

        private static KStart KOf(Dictionary<string, KStart> d, IFaction f)
        {
            string id = f != null && f.StringId != null ? f.StringId : "-";
            KStart k;
            if (!d.TryGetValue(id, out k)) { k = new KStart { Name = KName(f) }; d[id] = k; }
            return k;
        }

        private static void DoStart(double age)
        {
            var kd = new Dictionary<string, KStart>();
            var sb = new StringBuilder(4000);
            int day = (int)CampaignTime.Now.ToDays;
            long all0 = 0, all1 = 0;
            sb.Append("Zloto startowe wedlug ludzi (GL2): dzien ").Append(day).Append(" (doba kampanii ").Append(F(age, "0.00")).Append(")");
            bool mk = MarketOn, hd = HeadsOn;
            // ---- (1) kasy kupcow miast: waga zapas plaski x m_t - tylko przy MarketByPeople (zapas kupcow x m_t; bez klucza zapas plaski - kasy jak w grze)
            if (!mk) sb.Append(" | kasy kupcow miast: POMINIETE (MarketByPeople wylaczone - zapas kupcow plaski, kasy jak w grze)");
            else
            {
                var towns = new List<Town>(); var cur = new List<long>(); var w = new List<double>();
                foreach (var t in Town.AllTowns)
                {
                    try
                    {
                        float m;
                        if (t == null || t.Settlement == null || !PeopleScale.TryMarket(t, out m)) continue;   // Straz (Castle Black) i miasto bez skali - kasa zostaje
                        towns.Add(t); cur.Add(Math.Max(0, t.Gold)); w.Add(TownPurse.ReserveFlat(t) * (double)m);
                    }
                    catch (Exception e) { Stumble("StartGold(kasa " + (t != null && t.Settlement != null ? t.Settlement.StringId : "?") + ")", e); }
                }
                var tg = Spread(cur.ToArray(), w.ToArray());
                long b0 = 0, b1 = 0, moved = 0;
                // najpierw zdjecia, potem dopisania - zadna kasa nie schodzi ponizej 0 (cel >= 0)
                for (int pass = 0; pass < 2; pass++)
                    for (int i = 0; i < towns.Count; i++)
                    {
                        long d = tg[i] - cur[i];
                        if ((pass == 0) != (d < 0) || d == 0) continue;
                        try
                        {
                            var t = towns[i];
                            int had = t.Gold;
                            t.ChangeGold((int)d);
                            long real = (long)t.Gold - had;
                            TownPurse.NoteStartShift(t, real);
                            if (real != d) Stumble("StartGold(kasa niepelna)", null);
                            if (d > 0) moved += real;
                        }
                        catch (Exception e) { Stumble("StartGold(kasa przelew)", e); }
                    }
                for (int i = 0; i < towns.Count; i++)
                {
                    long g = Math.Max(0, towns[i].Gold);
                    b0 += cur[i]; b1 += g;
                    var k = KOf(kd, towns[i].Settlement.MapFaction); k.T0 += cur[i]; k.T1 += g;
                }
                double sw = 0; foreach (var x in w) sw += x;
                all0 += b0; all1 += b1;
                sb.Append(" | kasy kupcow miast: przed ").Append(b0).Append(" = po ").Append(b1).Append(" (przelano ").Append(moved).Append(", miast ").Append(towns.Count)
                  .Append("; waga zapas plaski x m_t - kasa / zapas kupcow x ").Append(sw > 0 ? F(b1 / sw, "0.00") : "-").Append(" w kazdym miescie)");
            }
            // ---- (2) kiesy ludu W/R/P: waga koszyk klasy x m_t (TownFolk) - tylko przy MarketByPeople (glowy miast x m_t)
            if (!mk) sb.Append(" | kiesy ludu: POMINIETE (MarketByPeople wylaczone - glowy miast jak dotad)");
            else try
            {
                long[] f0, f1; long fm; int fn;
                var per = TownFolk.StartFolkShift(out f0, out f1, out fm, out fn);
                foreach (var kv in per) { var k = KOf(kd, kv.Key.MapFaction); k.F0 += kv.Value[0]; k.F1 += kv.Value[1]; }
                all0 += f0[0] + f0[1] + f0[2]; all1 += f1[0] + f1[1] + f1[2];
                sb.Append(" | kiesy ludu: W ").Append(f0[0]).Append(" = ").Append(f1[0]).Append(", R ").Append(f0[1]).Append(" = ").Append(f1[1]).Append(", P ").Append(f0[2]).Append(" = ").Append(f1[2])
                  .Append(" (przelano ").Append(fm).Append(", miast ").Append(fn).Append("; waga koszyk klasy x m_t)");
            }
            catch (Exception e) { Stumble("StartGold(kiesy ludu)", e); sb.Append(" | kiesy ludu: BLAD (bez przelewu)"); }
            // ---- (3) kiesy wsi: waga skala domu wsi (glowy wsi P3) - tylko przy VillageHeadsByPeople
            if (!hd) sb.Append(" | kiesy wsi: POMINIETE (VillageHeadsByPeople wylaczone - glowy wsi jak dotad)");
            else
            {
                var vs = new List<Village>(); var cur = new List<long>(); var w = new List<double>();
                foreach (var v in Village.All)
                {
                    try
                    {
                        float u;
                        if (v == null || v.Settlement == null || !PeopleScale.TryVillageHome(v, out u)) continue;   // bez przelicznika i Straz - kiesa zostaje
                        vs.Add(v); cur.Add(Math.Max(0, v.Gold)); w.Add(u);
                    }
                    catch (Exception e) { Stumble("StartGold(wies)", e); }
                }
                var tg = Spread(cur.ToArray(), w.ToArray());
                long b0 = 0, b1 = 0, moved = 0;
                for (int pass = 0; pass < 2; pass++)
                    for (int i = 0; i < vs.Count; i++)
                    {
                        long d = tg[i] - cur[i];
                        if ((pass == 0) != (d < 0) || d == 0) continue;
                        try
                        {
                            int had = vs[i].Gold;
                            vs[i].ChangeGold((int)d);
                            long real = (long)vs[i].Gold - had;
                            if (real != d) Stumble("StartGold(wies niepelna)", null);
                            if (d > 0) moved += real;
                        }
                        catch (Exception e) { Stumble("StartGold(wies przelew)", e); }
                    }
                for (int i = 0; i < vs.Count; i++)
                {
                    long g = Math.Max(0, vs[i].Gold);
                    b0 += cur[i]; b1 += g;
                    var k = KOf(kd, vs[i].Settlement.MapFaction); k.V0 += cur[i]; k.V1 += g;
                }
                all0 += b0; all1 += b1;
                sb.Append(" | kiesy wsi: przed ").Append(b0).Append(" = po ").Append(b1).Append(" (przelano ").Append(moved).Append(", wsi ").Append(vs.Count).Append("; waga skala domu wsi)");
            }
            // ---- (4) skarbce koron: ludzie krolestwa (ksiega: wsie + miasta), podloga CrownFloor
            {
                var ks = new List<Kingdom>(); var cur = new List<long>(); var people = new Dictionary<Kingdom, double>();
                foreach (var s in Settlement.All)
                {
                    try
                    {
                        if (s == null || !(s.IsVillage || s.IsTown)) continue;
                        var k = s.MapFaction as Kingdom;
                        if (k == null || k.IsEliminated) continue;
                        double p = PopulationLaw.PeopleOf(s);
                        if (!(p > 0) || double.IsInfinity(p)) continue;
                        double x; people.TryGetValue(k, out x); people[k] = x + p;
                    }
                    catch (Exception e) { Stumble("StartGold(ludzie krolestwa)", e); }
                }
                foreach (var k in Kingdom.All) if (k != null && !k.IsEliminated) { ks.Add(k); cur.Add(Math.Max(0, k.KingdomBudgetWallet)); }
                var pw = new double[ks.Count];
                for (int i = 0; i < ks.Count; i++) { double x; pw[i] = people.TryGetValue(ks[i], out x) ? x : 0; }
                int onFloor;
                var tg = SpreadFloor(cur.ToArray(), pw, CrownFloor, out onFloor);
                long b0 = 0, b1 = 0, moved = 0;
                for (int pass = 0; pass < 2; pass++)
                    for (int i = 0; i < ks.Count; i++)
                    {
                        long d = tg[i] - cur[i];
                        if ((pass == 0) != (d < 0) || d == 0) continue;
                        try
                        {
                            var k = ks[i];
                            k.KingdomBudgetWallet += (int)d;
                            CrownIncome.ShiftSnapshot(k, d);       // migawka korony - przelew nie jest wplywem dnia
                            if (d > 0) moved += d;
                        }
                        catch (Exception e) { Stumble("StartGold(skarbiec)", e); }
                    }
                for (int i = 0; i < ks.Count; i++)
                {
                    long g = ks[i].KingdomBudgetWallet;
                    b0 += cur[i]; b1 += g;
                    var k = KOf(kd, ks[i]); k.C0 += cur[i]; k.C1 += g;
                }
                all0 += b0; all1 += b1;
                sb.Append(" | skarbce koron: przed ").Append(b0).Append(" = po ").Append(b1).Append(" (przelano ").Append(moved).Append(", krolestw ").Append(ks.Count)
                  .Append(", na podlodze ").Append(CrownFloor).Append(": ").Append(onFloor).Append("; waga ludzie krolestwa)");
            }
            sb.Append(" | suma swiata przed ").Append(all0).Append(" = po ").Append(all1).Append(all0 == all1 ? " (zgodne)" : " (ROZJAZD)");
            var list = new List<KStart>(kd.Values);
            list.Sort((a, b) => (b.T1 + b.F1 + b.V1 + b.C1).CompareTo(a.T1 + a.F1 + a.V1 + a.C1));
            sb.Append(" | wedlug krolestw (kasy miast, kiesy ludu, kiesy wsi, skarbiec: przed -> po): ");
            for (int i = 0; i < list.Count; i++)
            {
                var k = list[i];
                if (i > 0) sb.Append("; ");
                sb.Append(k.Name).Append(' ').Append(k.T0).Append("->").Append(k.T1).Append(", ").Append(k.F0).Append("->").Append(k.F1).Append(", ")
                  .Append(k.V0).Append("->").Append(k.V1).Append(", ").Append(k.C0).Append("->").Append(k.C1);
            }
            sb.Append('.');
            _startLine = sb.ToString();
            Log.Info(_startLine);
            _startLine = null;
        }
    }
}
