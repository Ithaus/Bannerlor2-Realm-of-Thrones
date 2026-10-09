using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// STRZELARZE I GROTNICY MIASTA (paczka 172, docs/paczki/172-strzaly.md). Strzaly i belty z rudy i drewna polki miasta, reka wlasnych
    /// rzemieslnikow miasta, na polke, z ktorej kupuja armie AI (AiGear). Zasada Jeffa: "jak znika to zamykamy ... wszystko z czegos wynika".
    ///
    /// Stan przed paczka (sprawdzone w dekompilacji): BK robi z kategorii "arrows" towar handlowy (BKItemCategories :122), wiec linia arrows
    /// warsztatow (ukryty "artisans" x4 przy 0.8 i "fletcher" x1 przy 1.5, obie BEZ wsadu) szla droga gry - snopy z niczego, mnozone przez BK
    /// (1 + rzemieslnicy/45/wartosc); WorkshopLaw jej nie widzial (AllOutputsArms = false). Ujscia w nicosc: mieszczanie zjadali amunicje z polek
    /// (budzet BK kategorii 10/10), zaopatrzenie BK kupowalo ja partiom AI za zloto lorda (zloto w nicosc) i niszczylo.
    ///
    /// Regula (czynne = wlacznik, rece > 0, ceny historyczne, kandydaci):
    ///  - linie arrows warsztatow notabli zamkniete (WorkshopLaw.CyclePrefix -> ClosesLine/NoteClosed); warsztat gracza - jak w grze;
    ///  - mieszczanie: budzet kategorii strzal 0 (HistoricalPrices.BudgetPostfix -> HouseUse); BK: potrzeba strzal partii AI 0 (BkSupplyTemper);
    ///  - strzelarze: rece = TownFletcherHandsPerArmsHand x WorkshopLaw.TownHands; po warsztatach miasta (postfiks DailyTickTown), krokami po snopie,
    ///    receptura i ceny wspolne z warsztatami zbrojnymi (WorkshopLaw.Needs / RevenueOf / MatPrice / DayWage), bramka zysku WorkshopMinProfitPercent;
    ///    ulamki surowca jako dlug miasta (< 1 jednostki), dlug rak na nastepna dobe (jak 148); bez zlota (polka -> polka tego samego miasta).
    ///  - kandydaci na koszyk typ x tier: kultura miasta albo neutralne, a gdy w koszyku brak - obce tego koszyka; najwyzej 4 o roznych nazwach.
    /// Sonda (krok 0, dziala zawsze): zmiana liczby snopow na polce miasta w cyklu linii arrows warsztatu - ile gra robi z niczego.
    /// Stan (dlug rak, 4 dlugi surowca) w zapisie "arm_fletchers"; Reset z WorkshopLaw.Reset (konstruktor ArmouryBehavior).
    /// </summary>
    internal static class TownFletchers
    {
        private const int MaxSteps = 200;              // bezpiecznik petli jednego miasta na dobe (60 rak / 2.4 dnia = 25 krokow)
        private const float MinHands = 0.01f;          // jak 148: sztuka zaczyna sie, gdy zostala choc setna dnia pracy
        private const int PerBasket = 4;               // najwyzej tyle wyrobow na koszyk typ x tier w miescie (rozne nazwy)

        private sealed class St { public float Labor; public readonly float[] Owed = new float[4]; }

        private sealed class TownCands
        {
            public readonly List<ItemObject> Items = new List<ItemObject>();
            public readonly Dictionary<int, ItemObject> Rep = new Dictionary<int, ItemObject>();   // koszyk -> przedmiot do wywolania Factor
            public int Foreign;                                                                       // koszyki z obcym wyrobem
        }

        private static List<ItemObject> _all;          // null = jeszcze nie szukane w tej sesji
        private static readonly Dictionary<string, St> _st = new Dictionary<string, St>();
        private static readonly Dictionary<string, TownCands> _town = new Dictionary<string, TownCands>();
        private static readonly Dictionary<ItemObject, float[]> _need = new Dictionary<ItemObject, float[]>();
        private static readonly Dictionary<ItemObject, float> _days = new Dictionary<ItemObject, float>();
        private static int _needDay = -1;
        private static ItemObject _ore, _wood, _leather, _linen;
        private static readonly HashSet<string> _errOnce = new HashSet<string>();
        internal static bool TickHooked, ProbeHooked;

        // linia dnia
        private static int _dayStamp = -1, _stumblesAll;
        private static int _dTowns, _dRebel, _dStumbles, _dGuard, _dNoProfit, _dNoInput, _dNoHands, _dForeignTowns, _dForeignBaskets;
        private static float _dHands, _dUsedHands, _dIdle, _dDebt;
        private static readonly int[] _dMissBy = new int[4];
        private static readonly int[] _dTaken = new int[4];
        private static readonly double[] _dWanted = new double[4];
        private static readonly int[,] _dMade = new int[2, 7];       // [0 strzaly, 1 belty][tier]
        private static readonly int[] _dBought = new int[2], _dUnmet = new int[2];
        private static int _dClosedCycles, _dClosedUnits, _dProbeCycles, _dProbeUnits, _dHouse, _dBkCalls, _dBkReset;

        internal static bool Enabled { get { var s = Settings.Current; return s != null && s.TownFletchersEnabled && s.TownFletcherHandsPerArmsHand > 0f; } }

        /// <summary>Strzelarze czynni: wlaczeni, ceny w nowej monecie, sa kandydaci. Wtedy linie arrows warsztatow notabli, budzet mieszczan
        /// na strzaly i potrzeba strzal BK partii AI sa zamkniete. Kandydaci szukani przy pierwszym pytaniu po przeliczeniu cen (potem z pamieci) -
        /// przed HistoricalPrices.Apply (start nowej gry: gra wypelnia rynki) zawsze nieczynne; wlaczenie w MCM w trakcie sesji dziala od razu.</summary>
        internal static bool Active { get { return Enabled && HistoricalPrices.Applied && All().Count > 0; } }

        /// <summary>Nowa gra / wczytanie (z WorkshopLaw.Reset, czyli z konstruktora ArmouryBehavior - przed SyncData).</summary>
        internal static void Reset()
        {
            _all = null; _st.Clear(); _town.Clear(); _need.Clear(); _days.Clear(); _needDay = -1; _errOnce.Clear();
            _ore = _wood = _leather = _linen = null;
            _dayStamp = -1; _stumblesAll = 0;
            ClearDay();
        }

        private static void ClearDay()
        {
            _dTowns = _dRebel = _dStumbles = _dGuard = _dNoProfit = _dNoInput = _dNoHands = _dForeignTowns = _dForeignBaskets = 0;
            _dHands = _dUsedHands = _dIdle = _dDebt = 0f;
            Array.Clear(_dMissBy, 0, 4); Array.Clear(_dTaken, 0, 4); Array.Clear(_dWanted, 0, 4); Array.Clear(_dMade, 0, _dMade.Length);
            Array.Clear(_dBought, 0, 2); Array.Clear(_dUnmet, 0, 2);
            _dClosedCycles = _dClosedUnits = _dProbeCycles = _dProbeUnits = _dHouse = _dBkCalls = _dBkReset = 0;
        }

        private static void Stumble(string where, Exception e)
        {
            _dStumbles++; _stumblesAll++;
            if (_errOnce.Add(where)) Log.Error("TownFletchers." + where, e);
        }

        private static bool IsAmmo(ItemObject it) { return it != null && (it.ItemType == ItemObject.ItemTypeEnum.Arrows || it.ItemType == ItemObject.ItemTypeEnum.Bolts); }
        private static int TierOf(ItemObject it) { try { return Math.Max(1, Math.Min(6, (int)it.Tier + 1)); } catch { return 1; } }
        private static int Basket(ItemObject it) { return (int)it.ItemType * 10 + TierOf(it); }
        private static int Kind(ItemObject.ItemTypeEnum t) { return t == ItemObject.ItemTypeEnum.Bolts ? 1 : 0; }

        private static void Resolve()
        {
            if (_ore != null) return;
            var m = MBObjectManager.Instance;
            if (m == null) return;
            _ore = m.GetObject<ItemObject>("iron"); _wood = m.GetObject<ItemObject>("hardwood");
            _leather = m.GetObject<ItemObject>("leather"); _linen = m.GetObject<ItemObject>("linen");
        }

        // ------------------------------------------------------------ zamkniecia (WorkshopLaw, WorkshopTrade, HistoricalPrices, BkSupplyTemper)
        /// <summary>Linia warsztatu, ktorej wszystkie wyroby to kategoria "arrows" (strzaly i belty).</summary>
        internal static bool IsArrowsLine(WorkshopType.Production p)
        {
            try
            {
                if (p.Outputs == null || p.Outputs.Count == 0) return false;   // Production to struct
                foreach (var o in p.Outputs) if (o.Item1 == null || o.Item1.StringId != "arrows") return false;
                return true;
            }
            catch { return false; }
        }

        /// <summary>Czy ta linia tego warsztatu jest zamknieta przez 172: linia arrows warsztatu notabla (ukryty artisans, fletcher) przy czynnych
        /// strzelarzach. Warsztat gracza - jak w grze (decyzja Jeffa: funkcje gracza bez zmian).</summary>
        internal static bool ClosesLine(WorkshopType.Production p, Workshop w)
        {
            return w != null && IsArrowsLine(p) && w.Owner != Hero.MainHero && Active;
        }

        /// <summary>Licznik zamknietego cyklu (WorkshopLaw.CyclePrefix): cykle i snopy z receptury (przed mnoznikiem BK).</summary>
        internal static void NoteClosed(WorkshopType.Production p)
        {
            try { _dClosedCycles++; foreach (var o in p.Outputs) _dClosedUnits += Math.Max(0, o.Item2); } catch { }
        }

        /// <summary>Mnoznik budzetu mieszczan dla kategorii strzal (HistoricalPrices.BudgetPostfix): 0 przy czynnych strzelarzach.</summary>
        internal static float HouseUse()
        {
            if (!Active) return 1f;
            _dHouse++;
            return 0f;
        }

        /// <summary>Potrzeba strzal BK partii AI ma byc 0 (BkSupplyTemper): czynni strzelarze i zakupy AI (AiGear liczy amunicje wedlug wzorcow).</summary>
        internal static bool BkArrowsClosed { get { return Active && AiGear.On; } }
        internal static void NoteBk(bool reset) { _dBkCalls++; if (reset) _dBkReset++; }

        internal static void NoteBought(ItemObject.ItemTypeEnum t, int n) { if (n > 0) _dBought[Kind(t)] += n; }
        internal static void NoteUnmet(ItemObject.ItemTypeEnum t) { _dUnmet[Kind(t)]++; }

        // ------------------------------------------------------------ sonda (krok 0): ile linia arrows warsztatu robi z niczego
        private static int AmmoOn(ItemRoster r)
        {
            int n = 0;
            if (r == null) return 0;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                if (el.Amount > 0 && IsAmmo(el.EquipmentElement.Item)) n += el.Amount;
            }
            return n;
        }

        /// <summary>Prefiks (Priority.First) TickOneProductionCycleForNotableWorkshop: stan amunicji polki przed cyklem linii arrows
        /// (__state = stan + 1; 0 = nie mierzone - takze gdy prefiks nie zadzialal).</summary>
        public static void ProbePrefix(WorkshopType.Production __0, Workshop __1, out int __state)
        {
            __state = 0;
            try
            {
                if (__1 == null || __1.Settlement == null || __1.Settlement.Town == null || !IsArrowsLine(__0)) return;
                __state = AmmoOn(__1.Settlement.Town.Owner.ItemRoster) + 1;
            }
            catch (Exception e) { Stumble("ProbePrefix", e); }
        }

        public static void ProbePostfix(WorkshopType.Production __0, Workshop __1, int __state)
        {
            try
            {
                if (__state <= 0 || __1 == null || __1.Settlement == null || __1.Settlement.Town == null) return;
                int d = AmmoOn(__1.Settlement.Town.Owner.ItemRoster) - (__state - 1);
                if (d > 0) { _dProbeCycles++; _dProbeUnits += d; }
            }
            catch (Exception e) { Stumble("ProbePostfix", e); }
        }

        // ------------------------------------------------------------ latki
        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var beh = typeof(WorkshopsCampaignBehavior);
                var tick = AccessTools.Method(beh, "DailyTickTown", new[] { typeof(Town) });
                if (tick != null) { h.Patch(tick, postfix: new HarmonyMethod(typeof(TownFletchers), nameof(TickPostfix))); TickHooked = true; }
                var cyc = AccessTools.Method(beh, "TickOneProductionCycleForNotableWorkshop");
                if (cyc != null)
                {
                    h.Patch(cyc, prefix: new HarmonyMethod(typeof(TownFletchers), nameof(ProbePrefix)) { priority = Priority.First },
                                 postfix: new HarmonyMethod(typeof(TownFletchers), nameof(ProbePostfix)));
                    ProbeHooked = true;
                }
                Log.Info("TownFletchers (172): doba strzelarzy " + (TickHooked ? "po warsztatach miasta (postfiks WorkshopsCampaignBehavior.DailyTickTown)" : "BRAK DailyTickTown - zapasowo sluchacz doby miasta (przed warsztatami)")
                         + ", sonda linii arrows " + (ProbeHooked ? "wpieta" : "BRAK") + ".");
            }
            catch (Exception e) { Log.Error("TownFletchers.ApplyAll", e); }
        }

        public static void TickPostfix(Town __0) { OnDailyTickTown(__0); }

        // ------------------------------------------------------------ kandydaci
        private static string[] _skip; private static string _skipSrc;
        private static bool Skipped(ItemObject it)
        {
            var src = Settings.Current.TownFletcherSkipIds ?? "";
            if (_skip == null || _skipSrc != src)
            {
                _skipSrc = src;
                var l = new List<string>();
                foreach (var p in src.Split(',')) { var t = p.Trim().ToLowerInvariant(); if (t.Length > 0) l.Add(t); }
                _skip = l.ToArray();
            }
            string id = (it.StringId ?? "").ToLowerInvariant();
            foreach (var f in _skip) if (id.Contains(f)) return true;
            return false;
        }

        private static List<ItemObject> All()
        {
            if (_all != null) return _all;
            var list = new List<ItemObject>();
            try
            {
                var mgr = MBObjectManager.Instance;
                if (!HistoricalPrices.Applied || mgr == null || Campaign.Current == null) return list;   // za wczesnie - bez zapamietywania
                foreach (var it in mgr.GetObjectTypeList<ItemObject>())
                {
                    try
                    {
                        if (!IsAmmo(it) || it.NotMerchandise || it.IsCraftedByPlayer || it.Value <= 0) continue;
                        if (ArmsPricing.IsUnique(it) || LegendaryLaw.IsLegend(it) || WorkshopLaw.Forbidden(it) || Skipped(it)) continue;
                        if (ArmsPricing.CostOf(it) == null) continue;   // bez receptury nie ma z czego
                        list.Add(it);
                    }
                    catch (Exception e) { Stumble("All(przedmiot)", e); }
                }
                list.Sort((a, b) => string.CompareOrdinal(a.StringId, b.StringId));
            }
            catch (Exception e) { Stumble("All", e); list.Clear(); }
            _all = list;
            return _all;
        }

        /// <summary>Kandydaci miasta (raz na sesje): na koszyk typ x tier wyroby kultury miasta albo neutralne, a gdy brak - obce tego koszyka;
        /// najwyzej PerBasket o roznych nazwach (warianty RBM "GRE_*" nie zajmuja miejsc).</summary>
        private static TownCands CandidatesOf(Town town)
        {
            TownCands tc;
            string key = town.Settlement.StringId;
            if (_town.TryGetValue(key, out tc)) return tc;
            tc = new TownCands();
            var local = new Dictionary<int, List<ItemObject>>(); var foreign = new Dictionary<int, List<ItemObject>>();
            foreach (var it in All())
            {
                bool own = it.Culture == null || it.Culture.StringId == "neutral_culture" || it.Culture == town.Culture;
                var d = own ? local : foreign;
                int k = Basket(it);
                List<ItemObject> l;
                if (!d.TryGetValue(k, out l)) { l = new List<ItemObject>(); d[k] = l; }
                l.Add(it);
            }
            var keys = new HashSet<int>(local.Keys); keys.UnionWith(foreign.Keys);
            foreach (var k in keys)
            {
                List<ItemObject> l;
                if (!local.TryGetValue(k, out l) || l.Count == 0) { l = foreign[k]; tc.Foreign++; }
                var pool = new List<ItemObject>(l);
                var names = new HashSet<string>();
                int took = 0;
                while (pool.Count > 0 && took < PerBasket)
                {
                    int i = MBRandom.RandomInt(pool.Count);
                    var it = pool[i]; pool.RemoveAt(i);
                    string nm = it.Name != null ? it.Name.ToString() : it.StringId;
                    if (!names.Add(nm)) continue;
                    tc.Items.Add(it); took++;
                    if (!tc.Rep.ContainsKey(k)) tc.Rep[k] = it;
                }
            }
            _town[key] = tc;
            return tc;
        }

        /// <summary>Receptura i dni na snop (WorkshopLaw.Needs - ta sama, z ktorej liczona jest wartosc), raz na dobe (suwaki MCM dzialaja od nastepnej doby).</summary>
        private static void EnsureNeeds(int day)
        {
            if (_needDay == day) return;
            _needDay = day;
            _need.Clear(); _days.Clear();
            foreach (var it in All())
            {
                try
                {
                    float days;
                    var n = WorkshopLaw.Needs(it, out days);
                    if (n == null) continue;
                    _need[it] = n; _days[it] = Math.Max(0.05f, days);
                }
                catch (Exception e) { Stumble("EnsureNeeds", e); }
            }
        }

        // ------------------------------------------------------------ doba miasta
        internal static void OnDailyTickTown(Town town)
        {
            try
            {
                if (town == null || !town.IsTown || Campaign.Current == null || !Campaign.Current.GameStarted) return;
                int day = (int)CampaignTime.Now.ToDays;
                if (_dayStamp != day) { Flush(); _dayStamp = day; }
                if (!Active) return;
                if (town.InRebelliousState) { _dRebel++; return; }   // jak warsztaty gry: miasto w buncie nie pracuje
                var gf = GoodsLedger.Begin(GoodsLedger.FFletch, town);   // ksiega towarow: ruda i drewno strzelarzy jako osobne ujscie (tylko licznik)
                try { Work(town); }
                finally { GoodsLedger.End(gf); }
            }
            catch (Exception e) { Stumble("OnDailyTickTown", e); }
        }

        /// <summary>Praca strzelarzy jednego miasta na dzis (rozdz. 5 specyfikacji). Wyjatek - licznik i koniec tego miasta.</summary>
        private static void Work(Town town)
        {
            try
            {
                var s = Settings.Current;
                Resolve();
                EnsureNeeds((int)CampaignTime.Now.ToDays);
                var tc = CandidatesOf(town);
                var items = tc.Items;
                int n = items.Count;
                if (n == 0) return;
                var sett = town.Settlement;
                var shelf = town.Owner.ItemRoster;
                St st;
                if (!_st.TryGetValue(sett.StringId, out st)) { st = new St(); _st[sett.StringId] = st; }
                float today = Math.Max(0f, s.TownFletcherHandsPerArmsHand) * WorkshopLaw.TownHands(town);
                float hands = today - st.Labor;          // dlug rak z wczoraj schodzi z dzisiejszej pracy
                _dTowns++; _dHands += today;
                if (tc.Foreign > 0) { _dForeignTowns++; _dForeignBaskets += tc.Foreign; }
                float minProfit = 1f + Math.Max(0f, s.WorkshopMinProfitPercent) / 100f;
                var mats = new[] { _ore, _wood, _leather, _linen };
                // raz na miasto: mnoznik wyceny (ruda/skora w cenie broni) i dniowka; Factor koszyka - raz, potem tylko koszyk ruszony snopem
                var mult = new float[n]; var wage = new float[n];
                for (int i = 0; i < n; i++)
                {
                    try { mult[i] = ArmsPricing.Multiplier(sett, items[i]); } catch { mult[i] = 1f; }
                    wage[i] = WorkshopLaw.DayWage(items[i], town);
                }
                var fac = new Dictionary<int, float>();
                var made = new Dictionary<ItemObject, int>();
                var price = new float[4];
                int steps = 0, reason = 0, miss = 0;
                var owed = st.Owed;
                while (hands > MinHands && steps < MaxSteps)
                {
                    for (int m = 0; m < 4; m++) price[m] = -1f;   // ceny surowcow liczone na krok, tylko te potrzebne
                    int best = -1; float bestScore = float.MinValue; bool blocked = false; int stepMiss = 0;
                    for (int i = 0; i < n; i++)
                    {
                        var it = items[i];
                        float[] need; float days;
                        if (!_need.TryGetValue(it, out need) || !_days.TryGetValue(it, out days)) continue;
                        float cost = days * wage[i];
                        for (int m = 0; m < 4; m++)
                        {
                            if (need[m] <= 0f) continue;
                            if (price[m] < 0f) price[m] = mats[m] != null ? WorkshopLaw.MatPrice(town, mats[m], m) : 0f;
                            cost += need[m] * price[m];
                        }
                        float rev = WorkshopLaw.RevenueOf(it, Fac(sett, tc, fac, Basket(it)), mult[i]);
                        if (rev < cost * minProfit) continue;                     // bez zysku przy dzisiejszych cenach
                        int mm = 0;
                        for (int m = 0; m < 4; m++)
                        {
                            int take = (int)Math.Floor(owed[m] + need[m]);
                            if (take <= 0) continue;
                            if (mats[m] == null || shelf.GetItemNumber(mats[m]) < take) mm |= 1 << m;
                        }
                        if (mm != 0) { blocked = true; stepMiss |= mm; continue; }  // oplacalny, ale surowca brak na polce
                        float score = (rev - cost) / Math.Max(0.1f, days);
                        if (score > bestScore) { bestScore = score; best = i; }
                    }
                    if (best < 0) { reason = blocked ? 2 : 1; miss = stepMiss; break; }
                    var pick = items[best];
                    var nd = _need[pick];
                    for (int m = 0; m < 4; m++)
                    {
                        int take = (int)Math.Floor(owed[m] + nd[m]);
                        if (take > 0 && mats[m] != null)
                        {
                            shelf.AddToCounts(mats[m], -take);
                            OreLedger.NoteFletch(mats[m], take);    // ksiega rudy i drewna: pozycja "strzelarze (172)"
                            _dTaken[m] += take;
                        }
                        owed[m] = owed[m] + nd[m] - Math.Max(0, take);
                        _dWanted[m] += nd[m];
                    }
                    shelf.AddToCounts(pick, 1);
                    int c; made.TryGetValue(pick, out c); made[pick] = c + 1;
                    _dMade[Kind(pick.ItemType), TierOf(pick)]++;
                    hands -= _days[pick];
                    steps++;
                    int bk = Basket(pick);
                    fac.Remove(bk); fac.Remove(bk - 1);                    // polka koszyka +1; substytucja koszyka t-1 patrzy na t
                }
                if (steps >= MaxSteps) _dGuard++;
                if (reason == 0) reason = 3;                                // skonczyly sie rece (albo bezpiecznik)
                if (reason == 1) _dNoProfit++;
                else if (reason == 2) { _dNoInput++; for (int m = 0; m < 4; m++) if ((miss & (1 << m)) != 0) _dMissBy[m]++; }
                else _dNoHands++;
                float idle = Math.Max(0f, hands);
                st.Labor = Math.Max(0f, -hands);                          // rece bez roboty nie odkladaja sie; zaczety snop ponad dzisiejsze rece - dlug na jutro
                _dIdle += idle; _dUsedHands += today - idle; _dDebt += st.Labor;
                foreach (var kv in made)
                {
                    try { CampaignEventDispatcher.Instance.OnItemProduced(kv.Key, sett, kv.Value); }   // jak warsztaty zbrojne (wyrob dopisany osadzie)
                    catch (Exception e) { Stumble("Zdarzenia", e); }
                }
            }
            catch (Exception e) { Stumble("Work", e); }
        }

        /// <summary>SupplyDemand.Factor koszyka (popyt / polka) - ten sam, co w cenie targu i w przychodzie warsztatow; liczony raz na koszyk.</summary>
        private static float Fac(Settlement st, TownCands tc, Dictionary<int, float> cache, int k)
        {
            float f;
            if (cache.TryGetValue(k, out f)) return f;
            f = 1f;
            try
            {
                ItemObject rep;
                if (SupplyDemand.Active && tc.Rep.TryGetValue(k, out rep)) { float d; int sh; f = SupplyDemand.Factor(st, rep, false, out d, out sh); }
            }
            catch (Exception e) { Stumble("Factor", e); f = 1f; }
            cache[k] = f;
            return f;
        }

        // ------------------------------------------------------------ linia startowa i linia dnia
        /// <summary>Z ArmouryBehavior.OnSessionLaunched po HistoricalPrices.Apply i ColdStart: kandydaci, koszyki wzorcow, linia startowa.</summary>
        internal static void SessionStart()
        {
            try
            {
                var s = Settings.Current;
                if (!Enabled)
                {
                    Log.Info("Strzelarze (172): NIECZYNNE - wylaczone w MCM" + (s != null && s.TownFletchersEnabled ? " (Town Fletcher Hands Per Arms Hand = 0)" : "")
                             + "; linie arrows warsztatow, mieszczanie i zaopatrzenie BK jak w grze (strzaly z niczego i w nicosc); sonda linii arrows mierzy doplyw.");
                    return;
                }
                if (!HistoricalPrices.Applied) { Log.Info("Strzelarze (172): NIECZYNNE - brak cen historycznych (receptura i bramka zysku sa w nowej monecie); strzaly jak w grze."); return; }
                var all = All();
                if (all.Count == 0) { Log.Info("Strzelarze (172): NIECZYNNE - brak kandydatow (Arrows/Bolts na sprzedaz z receptura); strzaly jak w grze."); return; }
                var byType = new SortedDictionary<int, int>();
                foreach (var it in all) { int k = Basket(it); int c; byType.TryGetValue(k, out c); byType[k] = c + 1; }
                // koszyki wzorcow oddzialow (nie-bohaterowie, sloty broni 0-3)
                var want = new SortedDictionary<int, int>();
                try
                {
                    foreach (var ch in CharacterObject.All)
                    {
                        if (ch == null || ch.IsHero) continue;
                        var seen = new HashSet<int>();
                        foreach (var eq in ch.BattleEquipments)
                        {
                            if (eq == null) continue;
                            for (int sl = 0; sl < 4; sl++)
                            {
                                var it = eq[(EquipmentIndex)sl].Item;
                                if (IsAmmo(it) && seen.Add(Basket(it))) { int c; want.TryGetValue(Basket(it), out c); want[Basket(it)] = c + 1; }
                            }
                        }
                    }
                }
                catch (Exception e) { Stumble("SessionStart(wzorce)", e); }
                var missing = new List<string>();
                foreach (var kv in want)
                {
                    int k = kv.Key, t = k % 10, ty = k / 10;
                    if (!byType.ContainsKey(k) && !(t > 1 && byType.ContainsKey(ty * 10 + t - 1))) missing.Add(BasketName(k) + " (" + kv.Value + " oddzialow)");
                }
                float world = 0f; int towns = 0;
                try { foreach (var t in Town.AllTowns) { if (t == null || !t.IsTown) continue; world += Math.Max(0f, s.TownFletcherHandsPerArmsHand) * WorkshopLaw.TownHands(t); towns++; } } catch { }
                var parts = new List<string>();
                foreach (var kv in byType) parts.Add(BasketName(kv.Key) + " " + kv.Value);
                Log.Info("Strzelarze (172): WLACZONE w " + towns + " miastach - strzaly i belty z rudy i drewna polki miasta (receptura warsztatow zbrojnych: metal 15% masy snopa, drewno 85% + wegiel dymarki i kuzni), "
                         + "rece " + F2(s.TownFletcherHandsPerArmsHand) + " x rece rzemieslnikow miasta = " + F1(world) + " roboczodni dziennie na swiat; bramka zysku " + F1(s.WorkshopMinProfitPercent)
                         + "% (te ceny, place i marza co warsztaty zbrojne); " + (TickHooked ? "po warsztatach miasta" : "ZAPASOWO przed warsztatami (brak latki DailyTickTown)")
                         + "; kandydaci " + all.Count + " [" + string.Join(", ", parts.ToArray()) + "], na koszyk najwyzej " + PerBasket + " (swoje albo neutralne, inaczej obce)"
                         + "; koszyki wzorcow oddzialow " + want.Count + (missing.Count > 0 ? ", BEZ WYROBU (t ani t-1): " + string.Join(", ", missing.ToArray()) : ", wszystkie maja wyrob")
                         + "; zamkniete z niczego: linie arrows warsztatow notabli" + (ProbeHooked ? " (sonda wpieta)" : "") + ", budzet mieszczan na strzaly 0"
                         + (s.TownHouseholdUse ? "" : " (TownHouseholdUse wylaczone - i tak 0)") + ", potrzeba strzal BK partii AI " + (AiGear.On ? "0" : "jak w BK (zakupy AI wylaczone)")
                         + "; warsztat gracza jak w grze.");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        private static string BasketName(int k) { return ((ItemObject.ItemTypeEnum)(k / 10) == ItemObject.ItemTypeEnum.Bolts ? "Bolts" : "Arrows") + " t" + (k % 10); }
        private static string F1(float v) { return v.ToString("0.0", CultureInfo.InvariantCulture); }
        private static string F2(float v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }

        /// <summary>Linia dnia (przy pierwszym miescie nastepnej doby - ta sama pora co "Warsztaty: dzien N" i 148).</summary>
        private static void Flush()
        {
            try
            {
                if (_dayStamp < 0) return;
                int[] stock = new int[2], empty = new int[2]; int towns = 0;
                var idx = new[] { new List<float>(), new List<float>() };
                foreach (var t in Town.AllTowns)
                {
                    if (t == null || !t.IsTown) continue;
                    try
                    {
                        towns++;
                        var r = t.Owner.ItemRoster;
                        int[] here = new int[2];
                        for (int i = 0; i < r.Count; i++)
                        {
                            var el = r.GetElementCopyAtIndex(i);
                            var it = el.EquipmentElement.Item;
                            if (el.Amount > 0 && IsAmmo(it)) here[Kind(it.ItemType)] += el.Amount;
                        }
                        for (int k = 0; k < 2; k++) { stock[k] += here[k]; if (here[k] <= 0) empty[k]++; }
                        TownCands tc;
                        if (Active && SupplyDemand.Active && _town.TryGetValue(t.Settlement.StringId, out tc))
                        {
                            float[] sum = new float[2]; int[] cnt = new int[2];
                            foreach (var kv in tc.Rep) { float d; int sh; int kk = Kind(kv.Value.ItemType); sum[kk] += SupplyDemand.Factor(t.Settlement, kv.Value, false, out d, out sh); cnt[kk]++; }
                            for (int k = 0; k < 2; k++) if (cnt[k] > 0) idx[k].Add(sum[k] / cnt[k]);
                        }
                    }
                    catch (Exception e) { Stumble("Flush(miasto)", e); }
                }
                var sb = new StringBuilder();
                if (!Active)
                {
                    sb.Append("Strzelarze (172): NIECZYNNE (").Append(!Enabled ? "wylaczone w MCM" : !HistoricalPrices.Applied ? "brak cen historycznych" : "brak kandydatow")
                      .Append(") - dzien ").Append(_dayStamp).Append(": warsztaty gry zrobily z niczego ").Append(_dProbeUnits).Append(" snopow w ").Append(_dProbeCycles)
                      .Append(" cyklach linii arrows (sonda, po mnozniku BK); na polkach: strzaly ").Append(stock[0]).Append(", belty ").Append(stock[1])
                      .Append("; kupione przez AI: strzaly ").Append(_dBought[0]).Append(", belty ").Append(_dBought[1]).Append("; potkniecia ").Append(_dStumbles).Append(".");
                    Log.Info(sb.ToString());
                    return;
                }
                sb.Append("Strzelarze (172): dzien ").Append(_dayStamp).Append(" - ").Append(_dTowns).Append(" miast");
                if (_dRebel > 0) sb.Append(" (w buncie ").Append(_dRebel).Append(")");
                for (int k = 0; k < 2; k++)
                {
                    int sum = 0; var tiers = new List<string>();
                    for (int t = 1; t <= 6; t++) if (_dMade[k, t] > 0) { sum += _dMade[k, t]; tiers.Add("t" + t + " " + _dMade[k, t]); }
                    sb.Append("; ").Append(k == 0 ? "strzaly" : "belty").Append(": zrobiono ").Append(sum).Append(" snopow [").Append(string.Join(", ", tiers.ToArray()))
                      .Append("], kupione przez AI ").Append(_dBought[k]).Append(", AI bez towaru ").Append(_dUnmet[k]).Append(" razy");
                }
                sb.Append("; koniec pracy w miastach (bez zysku / brak surowca / rece) ").Append(_dNoProfit).Append("/").Append(_dNoInput).Append("/").Append(_dNoHands)
                  .Append(" [brak: ruda ").Append(_dMissBy[0]).Append(", drewno ").Append(_dMissBy[1]).Append("]");
                if (_dGuard > 0) sb.Append(" (bezpiecznik petli w ").Append(_dGuard).Append(" miastach)");
                float pct = _dHands > 0f ? 100f * _dUsedHands / _dHands : 0f;
                sb.Append("; rece: zajete ").Append(F1(_dUsedHands)).Append(" z ").Append(F1(_dHands)).Append(" roboczodni (").Append(pct.ToString("0", CultureInfo.InvariantCulture))
                  .Append("%), bez roboty ").Append(F1(_dIdle)).Append(", dlug rak ").Append(F1(_dDebt));
                sb.Append("; zuzyto: ruda ").Append(_dTaken[0]).Append(" (wedle proporcji ").Append(_dWanted[0].ToString("0.0", CultureInfo.InvariantCulture))
                  .Append("), drewno ").Append(_dTaken[1]).Append(" (").Append(_dWanted[1].ToString("0.0", CultureInfo.InvariantCulture)).Append(")");
                if (_dTaken[2] + _dTaken[3] > 0) sb.Append(", skora ").Append(_dTaken[2]).Append(", len ").Append(_dTaken[3]);
                sb.Append("; obcy wyrob w ").Append(_dForeignTowns).Append(" miastach (koszyki ").Append(_dForeignBaskets).Append(")");
                sb.Append("; na polkach: strzaly ").Append(stock[0]).Append(" (miast bez strzal ").Append(empty[0]).Append(" z ").Append(towns).Append("), belty ").Append(stock[1])
                  .Append(" (miast bez beltow ").Append(empty[1]).Append(")");
                sb.Append("; mediana indeksu ceny strzal ").Append(F2(Median(idx[0]))).Append(", beltow ").Append(F2(Median(idx[1])));
                sb.Append("; zamkniete z niczego: linie strzal warsztatow ").Append(_dClosedCycles).Append(" cykli (").Append(_dClosedUnits).Append(" snopow z receptury, przed mnoznikiem BK), sonda: warsztaty gry zrobily ")
                  .Append(_dProbeUnits).Append(" snopow w ").Append(_dProbeCycles).Append(" cyklach; mieszczanie: budzet strzal 0 (").Append(_dHouse).Append(" wywolan); BK: zuzycie strzal partii AI ")
                  .Append(BkArrowsClosed ? "0" : "jak w BK").Append(" (").Append(_dBkCalls).Append(" przeliczen, zerowan zapisu ").Append(_dBkReset).Append(")");
                sb.Append("; potkniecia ").Append(_dStumbles).Append(" (od startu ").Append(_stumblesAll).Append(").");
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("Flush", e); }
            finally { ClearDay(); }
        }

        private static float Median(List<float> l) { if (l.Count == 0) return 0f; l.Sort(); return l[l.Count / 2]; }

        // ------------------------------------------------------------ zapis
        /// <summary>"1|miasto~dlug rak~ruda:drewno:skora:len|..." (liczby w kulturze niezmiennej).</summary>
        internal static string Export()
        {
            try
            {
                var sb = new StringBuilder("1");
                foreach (var kv in _st)
                {
                    var o = kv.Value.Owed;
                    sb.Append('|').Append(kv.Key).Append('~').Append(kv.Value.Labor.ToString("R", CultureInfo.InvariantCulture)).Append('~')
                      .Append(o[0].ToString("R", CultureInfo.InvariantCulture)).Append(':').Append(o[1].ToString("R", CultureInfo.InvariantCulture)).Append(':')
                      .Append(o[2].ToString("R", CultureInfo.InvariantCulture)).Append(':').Append(o[3].ToString("R", CultureInfo.InvariantCulture));
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
                    if (f.Length < 3 || f[0].Length == 0) continue;
                    var st = new St();
                    float.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out st.Labor);
                    var q = f[2].Split(':');
                    for (int m = 0; m < 4 && m < q.Length; m++)
                    {
                        float v; if (float.TryParse(q[m], NumberStyles.Float, CultureInfo.InvariantCulture, out v)) st.Owed[m] = Math.Max(0f, Math.Min(0.999f, v));
                    }
                    st.Labor = Math.Max(0f, st.Labor);
                    _st[f[0]] = st;
                }
            }
            catch (Exception e) { Stumble("Import", e); _st.Clear(); }
        }
    }

    /// <summary>Zapis stanu strzelarzy (paczka 172); sluchacz doby miasta tylko zapasowo, gdy latka DailyTickTown sie nie zalozyla.</summary>
    internal sealed class TownFletchersBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            if (!TownFletchers.TickHooked) CampaignEvents.DailyTickTownEvent.AddNonSerializedListener(this, TownFletchers.OnDailyTickTown);
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                string data = dataStore.IsSaving ? TownFletchers.Export() : null;
                SaveText.Sync(dataStore, "arm_fletchers", ref data);   // 161: dlugi napis w kawalkach
                if (dataStore.IsLoading) TownFletchers.Import(data);
            }
            catch (Exception e) { Log.Error("TownFletchers.SyncData", e); }
        }
    }
}
