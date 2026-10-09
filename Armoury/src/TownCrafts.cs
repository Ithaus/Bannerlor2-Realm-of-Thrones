using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// RZEMIOSLO MIASTA WEDLE WARTOSCI (paczka 148; w planie K13 robocza 129, docs/PLAN-K13-2026-10-07.md): folowanie i tkanie welny, tkanie lnu,
    /// garbowanie - zamiast garbowania i tkania 1:1 (WorkshopLaw.TanOrWeave).
    ///
    /// Stan przed paczka (autotest 07.10, 40 dob): welne przerabia tylko ok. 20 tkalni notabli (34 welny dziennie z ok. 205), len i skory
    /// rzemieslnicy miasta tkaja i garbuja 1:1 (10 kg lnu -> 10 kg plotna, najwyzej 5 sztuk na cykl linii BK bez wsadu, bez wzgledu na
    /// ceny), wiec polki welny, lnu i skor rosna o ok. 130 / 150 / 110 dziennie, a ceny leza przy 0.1-0.4 wartosci.
    ///
    /// Regula (jedna dla kazdego miasta): rzemieslnicy miasta przerabiaja surowiec z polki SWOJEGO miasta na wyrob, gdy przy dzisiejszych
    /// cenach wyrob placi wsad + prace + marze mistrza (SmithProfitPercent - ta sama marza co cena broni i cena sprawiedliwa 123).
    /// Krokami po jednej sztuce wyrobu: przed kazdym krokiem cena liczona od nowa (polka surowca maleje, polka wyrobu rosnie - wzor ceny
    /// gry z danymi rynku miasta), z trzech przerobow wybrany ten, ktory daje najwiecej zysku na roboczodzien (jak ranking kuzni).
    ///  - Pary z receptur gry (WorkshopType.All, linie z jednym wsadem i jednym wyrobem - towarem): wsad = surowiec lancucha tekstyliow
    ///    i skory (welna, len, skory surowe - zakres paczki 148; dalszy krok planu K13 (roboczy 136) obejmie kazda pare gry), wyrob = to, co z niego robi receptura
    ///    gry (filc = sukno, plotno, skora).
    ///  - Wsad wedle wartosci w granicach fizyki: material = 60% wartosci wyrobu (historia 1300-1400: welna 40-65% kosztu sukna, skora
    ///    surowa 50-60% skory), ale nigdy mniej masy niz masa wyrobu. Przy cenach historycznych: welna 1.45 na sukno (14.5 kg -> 10 kg,
    ///    wydajnosc 0.69 - przedza i folowanie traca tluszcz i odpad 30-40%), len 3.0 na plotno (30 kg -> 10 kg, 0.33 - paz i pakuly),
    ///    skory 2.4 na skore (24 kg -> 10 kg, 0.42 - mizdra, wlos, woda). Ulamki jako dlug miasta (jak w kuzni): sukno bierze na
    ///    przemian 1 i 2 welny.
    ///  - Praca = 20% wartosci wyrobu przy dniowce WorkshopWagePerDay (3 d): sukno 13.3, plotno 6.7, skora 2.7 roboczodnia na sztuke.
    ///    Paczka 149 (Jeff 07.10: "wszelkie koszty w danym miescie zalezne od dobrobytu i stawek historycznych"): te roboczodni w bramce
    ///    zysku placi sie dniowka TEGO miasta - WorkshopWagePerDay x poziom plac miasta (TownWage.Index: dobrobyt / TownWageRefProsperity,
    ///    0.5-1.5), ta sama regula co place linii towarowych warsztatow (WorkshopTrade.CycleLabour) i utrzymanie (paczka 136); przy
    ///    wylaczonym WorkshopWageByTier - wszedzie 3 d. Roboczodni na sztuke (rece) bez zmian - wskaznik dziala tylko na stawke.
    ///  - Rece = TownCraftHandsPerArmsHand (2) x rece rzemieslnikow miasta (WorkshopLaw.TownHands, rosna z dobrobytem): sukiennictwo
    ///    i skornictwo zatrudnialy ok. 2 razy wiecej ludzi niz cechy zbrojne (Paryz 1292, Gandawa 1356 - szacunek). Sztuka zaczeta,
    ///    gdy zostala choc czesc dnia pracy; brakujaca praca to dlug rak na nastepna dobe (male miasto robi sukno co kilka dni).
    ///    Rece bez roboty nie odkladaja pracy na pozniej.
    ///  - Bez zlota: towar z polki na polke tego samego miasta (kasa miasta = kupcy i rzemieslnicy razem).
    ///  - Zuzycie surowca rzemiosla (srednia wykladnicza z ok. 14 dob, od zera) wchodzi do zuzycia miasta, z ktorego karawany licza zapas docelowy,
    ///    a cena surowcow (115) popyt (CaravanBulk.Use) - welna jest droga tam, gdzie ja ktos przerabia. Garbowanie i tkanie 1:1
    ///    (TanOrWeave) i jego stala w tym zuzyciu (PerCycle) przy czynnym rzemiosle nie dzialaja.
    /// Wymaga cen historycznych (HistoricalPrices.Applied) - wartosci 60% / 20% sa w nowej monecie; bez nich zostaje TanOrWeave.
    /// Stan (dlug wsadu, dlug rak, srednia zuzycia) w zapisie ("arm_towncrafts"), czyszczony przy kazdej nowej grze / wczytaniu (Reset).
    /// Wylaczone (TownCraftsEnabled albo suwak rak TownCraftHandsPerArmsHand = 0) - TanOrWeave i zuzycie karawan jak dotad (zadna latka,
    /// sam sluchacz doby miasta, ktory nic nie robi).
    /// Kolejnosc dnia: sluchacz doby miasta idzie PRZED warsztatami gry tego miasta (MbEvent wola od ostatnio dopisanego, TownCraftsBehavior
    /// jest dopisany po WorkshopsCampaignBehavior) - przerob widzi polke sprzed warsztatow; warsztaty zbrojne i notabli biora z tego, co zostalo
    /// (wsad do progu ceny, za to swieza skora i plotno juz na polce).
    /// </summary>
    internal static class TownCrafts
    {
        internal const float MaterialShare = 0.6f;     // material ok. 60% ceny wyrobu (proporcje historyczne, raport B rozdz. 2)
        internal const float LaborShare = 0.2f;        // praca ok. 20% ceny wyrobu
        private const float UseDays = 14f;             // srednia wykladnicza zuzycia surowca z ok. 14 dob
        private const int MaxSteps = 400;              // bezpiecznik petli jednego miasta na dobe (dzis najwyzej ok. 60 krokow)
        private const float MinHands = 0.01f;          // sztuke zaczyna sie, gdy zostala choc setna dnia pracy (bez tego blad zaokraglenia dlugu rak, np. 1e-6, zaczynalby sztuke na kredyt)
        private const string Scope = "wool,flax,hides";   // paczka 148: surowce lancucha tekstyliow i skory (dalszy krok planu K13, roboczy 136 - kazda para gry)

        internal sealed class Pair
        {
            public int Ix;
            public string Key, Name, Shop;             // "wool>felt", "welna -> filc", typ warsztatu z receptura
            public ItemObject In, Out;
            public float Ratio, ValueRatio, MassRatio; // sztuk wsadu na sztuke wyrobu (wiaze wieksze z dwoch), 60% wartosci, masa
            public float Labor;                        // roboczodni na sztuke
        }

        /// <summary>Stan miasta: dlug rak (roboczodni) i na kazda pare [dlug wsadu, srednia zuzycia (sztuk wsadu na dobe), czy srednia jest].</summary>
        private sealed class St
        {
            public float Labor;
            public readonly Dictionary<string, float[]> P = new Dictionary<string, float[]>();
            public float[] Of(string key) { float[] a; if (!P.TryGetValue(key, out a)) { a = new float[3]; P[key] = a; } return a; }
        }

        private static List<Pair> _pairs;              // null = jeszcze nie szukane w tej sesji
        private static readonly Dictionary<string, St> _st = new Dictionary<string, St>();
        private static bool _errResolve;
        private static readonly HashSet<string> _errOnce = new HashSet<string>();

        // linia dnia
        private static int _dayStamp = -1, _dTowns, _dStumbles, _stumblesAll, _dGuard;
        private static float _dHands, _dUsedHands, _dIdle, _dDebt;
        private static int[] _dMade, _dTaken, _dActive, _dNoProfit, _dNoInput, _dNoHands;
        private static double[] _dWanted;              // suma wsadu wedle proporcji (sztuki x Ratio) - kontrola dlugu

        // recenzja: suwak rak 0 = rzemioslo wylaczone jak wlacznikiem (wraca TanOrWeave 1:1) - wczesniej 0 gasil rzemioslo, a TanOrWeave
        // zostawal wylaczony: zadne miasto nie garbowalo ani nie tkalo
        internal static bool Enabled { get { var s = Settings.Current; return s != null && s.TownCraftsEnabled && s.TownCraftHandsPerArmsHand > 0f; } }

        /// <summary>Rzemioslo czynne: wlaczone (wlacznik i rece > 0), ceny w nowej monecie i znalezione pary. Wtedy TanOrWeave i jego stala zuzycia nie dzialaja.</summary>
        internal static bool Active { get { return Enabled && HistoricalPrices.Applied && Pairs().Count > 0; } }

        /// <summary>Nowa gra / wczytanie (z WorkshopLaw.Reset, czyli z konstruktora ArmouryBehavior - przed SyncData).</summary>
        internal static void Reset()
        {
            _pairs = null; _st.Clear(); _errResolve = false; _errOnce.Clear();
            _dayStamp = -1; _stumblesAll = 0;
            ClearDay();
        }

        private static void ClearDay()
        {
            int n = _pairs != null ? _pairs.Count : 0;
            _dTowns = 0; _dStumbles = 0; _dGuard = 0; _dHands = _dUsedHands = _dIdle = _dDebt = 0f;
            _dMade = new int[n]; _dTaken = new int[n]; _dActive = new int[n]; _dNoProfit = new int[n]; _dNoInput = new int[n]; _dNoHands = new int[n];
            _dWanted = new double[n];
        }

        private static void Stumble(string where, Exception e)
        {
            _dStumbles++; _stumblesAll++;
            if (_errOnce.Add(where)) Log.Error("TownCrafts." + where, e);
        }

        // ------------------------------------------------------------ pary z receptur gry
        internal static List<Pair> Pairs()
        {
            if (_pairs != null) return _pairs;
            var list = new List<Pair>();
            try
            {
                if (!HistoricalPrices.Applied || Campaign.Current == null || WorkshopType.All == null) return list;   // za wczesnie - sprobujemy przy nastepnym wywolaniu
                var scope = new HashSet<string>(Scope.Split(','));
                var seen = new HashSet<string>();
                var s = Settings.Current;
                float wage = Math.Max(0.5f, s.WorkshopWagePerDay);
                foreach (var wt in WorkshopType.All)
                {
                    if (wt == null || wt.Productions == null) continue;
                    foreach (var p in wt.Productions)
                    {
                        try   // recenzja: wyjatek jednej linii receptury - licznik i ta linia pominieta, reszta par sie liczy
                        {
                            if (p.Inputs == null || p.Outputs == null || p.Inputs.Count != 1 || p.Outputs.Count != 1) continue;   // Production to struct
                            var inCat = p.Inputs[0].Item1; var outCat = p.Outputs[0].Item1;
                            if (inCat == null || outCat == null || !outCat.IsTradeGood || !scope.Contains(inCat.StringId)) continue;
                            var inIt = ItemOf(inCat); var outIt = ItemOf(outCat);
                            if (inIt == null || outIt == null || inIt == outIt || inIt.Value <= 0 || outIt.Value <= 0) continue;
                            string key = inIt.StringId + ">" + outIt.StringId;
                            if (!seen.Add(key)) continue;
                            var pr = new Pair { Ix = list.Count, Key = key, In = inIt, Out = outIt, Shop = wt.StringId };
                            pr.ValueRatio = MaterialShare * outIt.Value / inIt.Value;
                            pr.MassRatio = Kg(outIt) / Kg(inIt);
                            pr.Ratio = Math.Max(pr.ValueRatio, pr.MassRatio);
                            pr.Labor = LaborShare * outIt.Value / wage;
                            pr.Name = NameOf(inIt) + " -> " + NameOf(outIt);
                            list.Add(pr);
                        }
                        catch (Exception e) { Stumble("Pairs", e); }
                    }
                }
            }
            catch (Exception e)
            {
                // recenzja: wyjatek calego przegladu receptur (nie jednej linii) - pusta lista zostaje na sesje (rzemioslo nieczynne, TanOrWeave
                // 1:1 jak dotad, linia startowa mowi NIECZYNNE); bez tego kazde pytanie o Active (CaravanBulk.Use) liczyloby przeglad od nowa z wyjatkiem
                _stumblesAll++;
                if (!_errResolve) { _errResolve = true; Log.Error("TownCrafts.Pairs", e); }
                list.Clear();
            }
            _pairs = list;
            ClearDay();
            return _pairs;
        }

        private static float Kg(ItemObject it) { return it.Weight > 0.05f ? it.Weight : 10f; }

        /// <summary>Przedmiot kategorii (gra robi wyrob kategorii; dla tych szesciu kategorii jest jeden towar o tym samym id).</summary>
        private static ItemObject ItemOf(ItemCategory cat)
        {
            var mgr = MBObjectManager.Instance;
            if (mgr == null) return null;
            var it = mgr.GetObject<ItemObject>(cat.StringId);
            if (it != null && it.ItemCategory == cat && it.IsTradeGood) return it;
            ItemObject best = null;
            foreach (var x in mgr.GetObjectTypeList<ItemObject>())
                if (x != null && x.ItemCategory == cat && x.IsTradeGood && !x.IsCraftedByPlayer && (best == null || string.CompareOrdinal(x.StringId, best.StringId) < 0)) best = x;
            return best;
        }

        /// <summary>Dopelniacz do linii logu ("1.45 welny na sztuke", "zuzyto 300 lnu").</summary>
        private static string NameGen(ItemObject it)
        {
            switch (it.StringId)
            {
                case "wool": return "welny";
                case "flax": return "lnu";
                case "hides": return "skor";
                default: return NameOf(it);
            }
        }

        private static string NameOf(ItemObject it)
        {
            switch (it.StringId)
            {
                case "wool": return "welna";
                case "felt": return "filc";
                case "flax": return "len";
                case "linen": return "plotno";
                case "hides": return "skory";
                case "leather": return "skora";
                default: return it.StringId;
            }
        }

        // ------------------------------------------------------------ zuzycie dla karawan i ceny surowca
        /// <summary>Zuzycie surowca przez rzemioslo miasta: srednia wykladnicza z ok. 14 dob, w sztukach rynku na dobe (0 = towar nie jest
        /// wsadem rzemiosla albo rzemioslo nieczynne). Czyta CaravanBulk.Use - zapas docelowy karawan i popyt ceny surowcow (115).</summary>
        internal static float UseOf(Town town, ItemObject item)
        {
            try
            {
                if (town == null || item == null || !Active) return 0f;
                St st;
                if (!_st.TryGetValue(town.Settlement.StringId, out st)) return 0f;
                float u = 0f;
                foreach (var p in _pairs)
                {
                    if (p.In != item) continue;
                    float[] a;
                    if (st.P.TryGetValue(p.Key, out a) && a[2] > 0f) u += a[1];
                }
                return u;
            }
            catch (Exception e) { Stumble("UseOf", e); return 0f; }   // recenzja: potkniecie liczone (bylo ciche), zuzycie tego miasta = 0 jak bez rzemiosla
        }

        // ------------------------------------------------------------ doba miasta
        /// <summary>Sluchacz CampaignEvents.DailyTickTownEvent (TownCraftsBehavior).</summary>
        internal static void OnDailyTickTown(Town town)
        {
            try
            {
                if (town == null || !town.IsTown || Campaign.Current == null || !Campaign.Current.GameStarted) return;
                if (!Active) return;
                int day = (int)CampaignTime.Now.ToDays;
                if (_dayStamp != day) { Flush(); _dayStamp = day; }
                var gf = GoodsLedger.Begin(GoodsLedger.FTownCraft, town);   // ksiega towarow (146): przerob tego miasta jako osobna pozycja (tylko licznik)
                try { Work(town); }
                finally { GoodsLedger.End(gf); }
            }
            catch (Exception e) { Stumble("OnDailyTickTown", e); }
        }

        private static float Index(Town t, ItemObject it, bool selling)
        {
            var cat = it.ItemCategory;
            var d = t.MarketData.GetCategoryData(cat);
            return Campaign.Current.Models.TradeItemPriceFactorModel.GetBasePriceFactor(cat, d.InStoreValue, d.Supply, d.Demand, selling, it.Value);
        }

        /// <summary>Paczka 149: poziom plac miasta dla dniowki rzemiosla - jak WorkshopTrade (place linii towarowych i utrzymanie): TownWage.Index
        /// przy wlaczonym WorkshopWageByTier, inaczej 1 (wszedzie tyle samo).</summary>
        internal static float WageIndex(Town town)
        {
            var s = Settings.Current;
            return s != null && s.WorkshopWageByTier ? TownWage.Index(town) : 1f;
        }

        /// <summary>Przerob jednego miasta na dzis. Wyjatek - licznik i koniec tego miasta (inne miasta dzialaja dalej).</summary>
        private static void Work(Town town)
        {
            var pairs = _pairs;
            int n = pairs.Count;
            try
            {
                var s = Settings.Current;
                var shelf = town.Owner.ItemRoster;
                var sett = town.Settlement;
                St st;
                if (!_st.TryGetValue(sett.StringId, out st)) { st = new St(); _st[sett.StringId] = st; }
                float wage = Math.Max(0.5f, s.WorkshopWagePerDay);
                float pay = wage * WageIndex(town);      // paczka 149: dniowka tego miasta (poziom plac) - tylko w koszcie pracy, roboczodni bez zmian
                float margin = 1f + Math.Max(0f, s.SmithProfitPercent) / 100f;
                float today = Math.Max(0f, s.TownCraftHandsPerArmsHand) * WorkshopLaw.TownHands(town);
                float hands = today - st.Labor;          // dlug rak z wczoraj schodzi z dzisiejszej pracy
                _dTowns++; _dHands += today;
                var made = new int[n]; var taken = new int[n]; var reason = new int[n];   // powod konca: 1 bez zysku, 2 brak surowca, 3 rece
                var acc = new float[n][];
                var labor = new float[n];                // praca na sztuke przy dzisiejszej dniowce (suwak MCM dziala od razu)
                for (int i = 0; i < n; i++) { acc[i] = st.Of(pairs[i].Key); labor[i] = LaborShare * pairs[i].Out.Value / wage; }
                int steps = 0;
                while (hands > MinHands && steps < MaxSteps)
                {
                    Pair best = null; float bestScore = float.MinValue; int bestTake = 0;
                    for (int i = 0; i < n; i++)
                    {
                        var p = pairs[i];
                        reason[i] = 0;                    // powod liczy sie z ostatniej oceny (ceny po poprzednim kroku)
                        int take = (int)Math.Floor(acc[i][0] + p.Ratio);
                        if (take < 1) take = 1;
                        if (shelf.GetItemNumber(p.In) < take) { reason[i] = 2; continue; }
                        float pin = Index(town, p.In, false) * p.In.Value;
                        float pout = Index(town, p.Out, true) * p.Out.Value;
                        float cost = p.Ratio * pin + labor[i] * pay;
                        if (pout < cost * margin) { reason[i] = 1; continue; }
                        float score = (pout - cost) / labor[i];
                        if (score > bestScore) { bestScore = score; best = p; bestTake = take; }
                    }
                    if (best == null) break;
                    var a = acc[best.Ix];
                    shelf.AddToCounts(best.In, -bestTake);
                    MaterialOrders.NoteUse(town, best.In, bestTake);   // 174.2: zmierzone zuzycie miasta
                    shelf.AddToCounts(best.Out, 1);
                    a[0] = a[0] + best.Ratio - bestTake;
                    hands -= labor[best.Ix];
                    made[best.Ix]++; taken[best.Ix] += bestTake;
                    steps++;
                }
                if (steps >= MaxSteps) _dGuard++;
                // przerob, ktory do konca mial surowiec i zysk, skonczyl sie na rekach (albo na bezpieczniku petli)
                for (int i = 0; i < n; i++) if (reason[i] == 0) reason[i] = 3;
                // dzisiejsze rece: splacily wczorajszy dlug, poszly w robote albo staly (bez roboty sie nie odkladaja)
                float idle = Math.Max(0f, hands);
                st.Labor = Math.Max(0f, -hands);
                _dIdle += idle; _dUsedHands += today - idle; _dDebt += st.Labor;
                for (int i = 0; i < n; i++)
                {
                    var p = pairs[i]; var a = acc[i];
                    float used = made[i] * p.Ratio;
                    a[1] += (used - a[1]) / UseDays; a[2] = 1f;   // srednia od zera (nowa kampania / stary zapis): pierwszy dzien z zapasem lezacym od dawna nie udaje stalego zuzycia
                    _dMade[i] += made[i]; _dTaken[i] += taken[i]; _dWanted[i] += used;
                    if (made[i] > 0)
                    {
                        _dActive[i]++;
                        try
                        {
                            CampaignEventDispatcher.Instance.OnItemConsumed(p.In, sett, taken[i]);
                            CampaignEventDispatcher.Instance.OnItemProduced(p.Out, sett, made[i]);
                        }
                        catch (Exception e) { Stumble("Zdarzenia", e); }
                    }
                    if (reason[i] == 1) _dNoProfit[i]++; else if (reason[i] == 2) { _dNoInput[i]++; MaterialOrders.NoteMiss(town, p.In); } else if (reason[i] == 3) _dNoHands[i]++;   // 174.2: sygnal zamowienia (len, skory, welna)
                }
            }
            catch (Exception e) { Stumble("Work", e); }
        }

        // ------------------------------------------------------------ linia startowa i linia dnia
        /// <summary>Z ArmouryBehavior.OnSessionLaunched zaraz po przeliczeniu cen (HistoricalPrices.Apply): pary i proporcje wedle wartosci.</summary>
        internal static void SessionStart()
        {
            try
            {
                if (!Enabled)
                {
                    var s0 = Settings.Current;
                    Log.Info("Rzemioslo miasta (148): wylaczone w MCM" + (s0 != null && s0.TownCraftsEnabled ? " (Town Craft Hands Per Arms Hand = 0)" : "") + " - garbowanie i tkanie rzemieslnikow 1:1 (TanOrWeave) jak dotad.");
                    return;
                }
                if (!HistoricalPrices.Applied) { Log.Info("Rzemioslo miasta (148): NIECZYNNE - brak cen historycznych (proporcje 60% / 20% sa w nowej monecie); TanOrWeave 1:1 jak dotad."); return; }
                var pairs = Pairs();
                if (pairs.Count == 0) { Log.Info("Rzemioslo miasta (148): NIECZYNNE - w recepturach gry brak par welna / len / skory -> towar; TanOrWeave 1:1 jak dotad."); return; }
                var s = Settings.Current;
                float world = 0f; int towns = 0;
                try { foreach (var t in Town.AllTowns) { if (t == null || !t.IsTown) continue; world += Math.Max(0f, s.TownCraftHandsPerArmsHand) * WorkshopLaw.TownHands(t); towns++; } } catch { }
                var parts = new List<string>();
                foreach (var p in pairs)
                    parts.Add(p.Name + " (" + p.Shop + "): " + F2(p.Ratio) + " " + NameGen(p.In) + " na sztuke (" + F1(p.Ratio * Kg(p.In)) + " kg -> " + F1(Kg(p.Out)) + " kg; 60% wartosci "
                              + p.Out.Value + " d = " + F2(p.ValueRatio) + " x " + p.In.Value + " d, masa " + F2(p.MassRatio) + " - wiaze " + (p.ValueRatio >= p.MassRatio ? "wartosc" : "masa")
                              + "), praca " + F1(p.Labor) + " roboczodnia");
                Log.Info("Rzemioslo miasta (148): WLACZONE w " + towns + " miastach - rzemieslnicy przerabiaja surowiec z polki swojego miasta, gdy wyrob placi wsad + prace (dniowka "
                         + F1(s.WorkshopWagePerDay) + " d" + (s.WorkshopWageByTier && s.TownWageRefProsperity > 0f ? " x poziom plac miasta (dobrobyt / " + F1(s.TownWageRefProsperity) + ", " + F1(TownWage.Min) + "-" + F1(TownWage.Max) + ")" : " w kazdym miescie") + ") + " + F1(s.SmithProfitPercent) + "% (Smith Profit Percent), krokami po sztuce z cena od nowa, najpierw przerob z najwiekszym zyskiem na roboczodzien; pary z receptur gry ["
                         + string.Join("; ", parts.ToArray()) + "]; rece " + F1(s.TownCraftHandsPerArmsHand) + " x rece rzemieslnikow miasta = " + F1(world)
                         + " roboczodni dziennie na swiat; bez zlota (polka -> polka); garbowanie i tkanie 1:1 (TanOrWeave) zastapione rzemioslem, "
                         // recenzja: przy wylaczonym Workshop No Free Raw linie BK skory i plotna bez wsadu ida torem gry (z niczego) - linia ma to mowic
                         + (s.WorkshopNoFreeRaw ? "jego linie bez wsadu zablokowane" : "UWAGA: Workshop No Free Raw wylaczone - linie BK skory i plotna bez wsadu robia z niczego obok rzemiosla")
                         + "; zuzycie surowca rzemiosla (srednia z ok. "
                         + UseDays.ToString("0", CultureInfo.InvariantCulture) + " dob) w zuzyciu miasta dla karawan i ceny surowcow.");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        private static string F1(float v) { return v.ToString("0.0", CultureInfo.InvariantCulture); }
        private static string F2(float v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }

        /// <summary>Linia dnia (drukowana przy pierwszym miescie nastepnej doby - ta sama pora co "Warsztaty: dzien N").</summary>
        private static void Flush()
        {
            try
            {
                if (_dayStamp < 0 || _pairs == null || _dTowns == 0) return;
                var sb = new StringBuilder();
                sb.Append("Rzemioslo miasta (148): dzien ").Append(_dayStamp).Append(" - ").Append(_dTowns).Append(" miast");
                for (int i = 0; i < _pairs.Count; i++)
                {
                    var p = _pairs[i];
                    sb.Append("; ").Append(p.Name).Append(": zrobiono ").Append(_dMade[i]).Append(", zuzyto ").Append(_dTaken[i]).Append(" ").Append(NameGen(p.In))
                      .Append(" (wedle proporcji ").Append(_dWanted[i].ToString("0.0", CultureInfo.InvariantCulture)).Append(") w ").Append(_dActive[i]).Append(" miastach");
                }
                float pct = _dHands > 0f ? 100f * _dUsedHands / _dHands : 0f;
                sb.Append("; rece: zajete ").Append(F1(_dUsedHands)).Append(" z ").Append(F1(_dHands)).Append(" roboczodni (").Append(pct.ToString("0", CultureInfo.InvariantCulture))
                  .Append("%), bez roboty ").Append(F1(_dIdle)).Append(", dlug rak na jutro ").Append(F1(_dDebt));
                sb.Append("; koniec przerobu w miastach (bez zysku / brak surowca / rece) [");
                for (int i = 0; i < _pairs.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(_pairs[i].Name).Append(" ").Append(_dNoProfit[i]).Append("/").Append(_dNoInput[i]).Append("/").Append(_dNoHands[i]);
                }
                sb.Append("]");
                if (_dGuard > 0) sb.Append(" (bezpiecznik petli w ").Append(_dGuard).Append(" miastach)");
                Stocks(sb);
                sb.Append("; potkniecia ").Append(_dStumbles).Append(" (od startu ").Append(_stumblesAll).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Flush", e); }
            finally { ClearDay(); }
        }

        /// <summary>Zapas na polkach miast, mediana indeksu ceny i srednia zuzycia rzemiosla (sztuk wsadu na dobe, suma miast) - tylko odczyt.</summary>
        private static void Stocks(StringBuilder sb)
        {
            var goods = new List<ItemObject>();
            foreach (var p in _pairs) { if (!goods.Contains(p.In)) goods.Add(p.In); if (!goods.Contains(p.Out)) goods.Add(p.Out); }
            var stock = new long[goods.Count];
            var idx = new List<float>[goods.Count];
            for (int g = 0; g < goods.Count; g++) idx[g] = new List<float>();
            var use = new double[_pairs.Count];
            foreach (var t in Town.AllTowns)
            {
                if (t == null || !t.IsTown) continue;
                try
                {
                    var r = t.Owner.ItemRoster;
                    for (int g = 0; g < goods.Count; g++) { stock[g] += r.GetItemNumber(goods[g]); idx[g].Add(t.MarketData.GetPriceFactor(goods[g].ItemCategory)); }
                    St st;
                    if (_st.TryGetValue(t.Settlement.StringId, out st))
                        for (int i = 0; i < _pairs.Count; i++) { float[] a; if (st.P.TryGetValue(_pairs[i].Key, out a) && a[2] > 0f) use[i] += a[1]; }
                }
                catch (Exception e) { Stumble("Stocks", e); }
            }
            sb.Append("; na polkach miast [");
            for (int g = 0; g < goods.Count; g++)
            {
                if (g > 0) sb.Append(", ");
                var l = idx[g]; l.Sort();
                float med = l.Count > 0 ? l[l.Count / 2] : 0f;
                sb.Append(NameOf(goods[g])).Append(" ").Append(stock[g]).Append(" (indeks ").Append(F2(med)).Append(")");
            }
            sb.Append("]; srednia zuzycia rzemiosla z ok. 14 dob (do zapasu karawan i ceny) [");
            for (int i = 0; i < _pairs.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(NameOf(_pairs[i].In)).Append(" ").Append(use[i].ToString("0.0", CultureInfo.InvariantCulture));
            }
            sb.Append("]");
        }

        // ------------------------------------------------------------ zapis
        /// <summary>"1|miasto~dlug rak~para:dlug:srednia:jest~...|..." (liczby w kulturze niezmiennej).</summary>
        internal static string Export()
        {
            try
            {
                var sb = new StringBuilder("1");
                foreach (var kv in _st)
                {
                    sb.Append('|').Append(kv.Key).Append('~').Append(kv.Value.Labor.ToString("R", CultureInfo.InvariantCulture));
                    foreach (var p in kv.Value.P)
                        sb.Append('~').Append(p.Key).Append(':').Append(p.Value[0].ToString("R", CultureInfo.InvariantCulture)).Append(':')
                          .Append(p.Value[1].ToString("R", CultureInfo.InvariantCulture)).Append(':').Append(p.Value[2] > 0f ? "1" : "0");
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return null; }
        }

        internal static void Import(string data)
        {
            _st.Clear();
            if (string.IsNullOrEmpty(data)) return;
            try
            {
                var towns = data.Split('|');
                if (towns.Length == 0 || towns[0] != "1") return;
                for (int i = 1; i < towns.Length; i++)
                {
                    var f = towns[i].Split('~');
                    if (f.Length < 2 || f[0].Length == 0) continue;
                    var st = new St();
                    float.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out st.Labor);
                    for (int k = 2; k < f.Length; k++)
                    {
                        var q = f[k].Split(':');
                        if (q.Length < 4) continue;
                        var a = st.Of(q[0]);
                        float.TryParse(q[1], NumberStyles.Float, CultureInfo.InvariantCulture, out a[0]);
                        float.TryParse(q[2], NumberStyles.Float, CultureInfo.InvariantCulture, out a[1]);
                        a[2] = q[3] == "1" ? 1f : 0f;
                    }
                    _st[f[0]] = st;
                }
            }
            catch (Exception e) { Stumble("Import", e); _st.Clear(); }
        }
    }

    /// <summary>Sluchacz doby miasta i zapis stanu rzemiosla (paczka 148).</summary>
    internal sealed class TownCraftsBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickTownEvent.AddNonSerializedListener(this, TownCrafts.OnDailyTickTown);
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                string data = dataStore.IsSaving ? TownCrafts.Export() : null;
                SaveText.Sync(dataStore, "arm_towncrafts", ref data);   // 161: dlugi napis w kawalkach
                if (dataStore.IsLoading) TownCrafts.Import(data);
            }
            catch (Exception e) { Log.Error("TownCrafts.SyncData", e); }
        }
    }
}
