using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// m96-jakosc - RZADKOSC JAKOSCI (Jeff 20.10, WGRANIE 95: "za duzo jest legendary przedmiotow, legendarne przedmioty to kilka sztuk na swiecie,
    /// masterwork juz wiecej itp., a w sklepach za latwo mozna znalezc legendary i masterwork").
    ///
    /// Przyczyna (dekompilacja + item_modifiers.xml): kazda sztuka uzbrojenia z warsztatu AI (WorkshopLaw - ok. 1000-2000 szt. na dobe), z drogi gry
    /// (WorkshopsCampaignBehavior.GetRandomItemAux - warsztaty gracza, linie gry, zamiennik CraftRealm.FilterRandom) i zamiennik broni jenca ROT
    /// dostaje rzut ItemModifierGroup.GetRandomItemModifierProductionScoreBased. Wagi RBM (RBMCombat_item_modifiers.xml) przy no_modifier_production_score 75
    /// (Native item_modifiers_groups.xml): legendary 5, masterwork 10, fine 15, inferior 5, poor 5 - legendarna co 23. sztuka (4.3%), mistrzowska co 12.
    /// (8.7%), bez wzgledu na tier i na to, kto kuje. Lup gracza z poleglych (DefaultBattleRewardModel.GetRandomItem, wagi lupu) - legendarna 2.2%,
    /// mistrzowska 7.6%. Ludzie lordow i zalogi nie kupuja sztuk z dodatnim stanem (AiSkipFineGoods - zbrojownia DTE to "przedmiot: ile"), wiec takie
    /// sztuki zostaja na polkach na zawsze: linia "Zakupy i nadwyzki AI (E8/Z)" u Jeffa 20.10 - 3505 odrzuconych kandydatow z dodatnim stanem w 166
    /// wizytach jednej doby, kupionych 0.
    ///
    /// Regula (klucz QualityScarcity):
    ///  1. POWSTANIE sztuki (warsztat AI, droga gry, lup gracza, jeniec ROT, nagroda zaciagu ROT, nagroda turnieju zamowiona u rzemieslnikow): rzut gry
    ///     Legendary albo Masterwork przepada (sztuka zwykla - miasto i tak placi warsztatowi cene zwyklej sztuki, WorkshopLaw.Revenue; jakosc byla
    ///     wartoscia z niczego). Fine, slabe i zepsute - jak dzis. WLASNY rzut tylko tam, gdzie sztuke ktos naprawde robi i na pewno trafia do kogos
    ///     (warsztaty AI, zamiennik broni jenca ROT, nagroda turnieju zamowiona u rzemieslnikow miasta): legendarna tylko z tieru 5-6 (QualityLegendaryChance
    ///     na sztuke), mistrzowska tylko z tieru 4-6 (QualityMasterworkChance) - i tylko, gdy na swiecie jest mniej niz limit (QualityLegendaryWorldCap /
    ///     QualityMasterworkWorldCap). Droga gry, lup i nagroda zaciagu - tylko odbior rzutu gry (recenzja 20.10: BK ProduceOutputPrefix i tak kladzie
    ///     wyrob drogi gry bez modyfikatora, Spoils of War bierze lup z trupa, MapEvent odrzuca czesc lupu - wlasny rzut zajmowal miejsce w limicie
    ///     sztukami, ktorych nikt nie dostal). Fuszerka (Inferior, Poor) nie staje sie arcydzielem.
    ///  2. SPIS raz na dobe: wszystkie polki osad (miasta, zamki, wsie), juki partii, rynsztunek bohaterow (bojowy, cywilny, skradania), wozy kupcow
    ///     w drodze (TradeCarts), gracz (sakwy, schowek, zbrojownia DTE wojska gracza, bohaterowie rodu; zuzyta legendarna/mistrzowska gracza - wedlug
    ///     stanu z ksiegi zuzycia). Zbrojownie DTE lordow i zalog nie maja jakosci (Dictionary ItemObject -> ile). Limit przy powstaniu = spis + nowe od
    ///     spisu (bez stanu w zapisie - zapis zgodny wstecz i w przod).
    ///  3. NADMIAR (stary swiat Jeffa): tylko na POLKACH OSAD, przy rozliczeniu doby - sztuki ponad limit swiata (limit minus to, co trzymaja juki,
    ///     bohaterowie i gracz) schodza o stopien: legendarna -> mistrzowska (gdy jest miejsce w limicie mistrzowskich), inaczej -> dobra (Fine);
    ///     mistrzowska -> dobra. Zostaja najdrozsze. Nic nie znika (ta sama liczba sztuk), zloto nie rusza - spada tylko wartosc towaru na polce.
    ///     Pociski i bron miotana (modyfikator zmienia liczbe w stosie, np. RBM large_bag_* +15 strzal): cel o liczbie w stosie nie wiekszej niz zrodlo
    ///     (inaczej zwykla) - strzal i oszczepow nie przybywa z niczego.
    ///     Sakwy, schowek i bohaterowie gracza oraz bron wykuta przez gracza (IsCraftedByPlayer) - nigdy. Kuznia gracza bez zmian.
    /// Wylacznik QualityScarcity = false: wszystko jak przed paczka; spis i linia doby dalej (pomiar).
    /// Linia doby "Jakosc przedmiotow (m96jakosc): dzien N" (grupa sprawdz_logi m96jakosc).
    /// </summary>
    internal static class QualityScarcity
    {
        internal const int SrcWorkshop = 0, SrcGame = 1, SrcLoot = 2, SrcGank = 3, SrcEnlist = 4, SrcTourney = 5;
        private const int NSrc = 6;
        private static readonly string[] SrcName = { "warsztaty AI", "droga gry", "lup gracza", "jeniec ROT", "nagroda zaciagu ROT", "nagroda turnieju zamowiona" };

        // miejsca spisu: polki osad, juki partii AI, bohaterowie AI, gracz (sakwy, schowek, zbrojownia DTE gracza, bohaterowie rodu gracza), wozy kupcow w drodze
        private const int PShelf = 0, PBag = 1, PHero = 2, PPlayer = 3, PCart = 4, NPlace = 5;
        private static readonly string[] PlaceName = { "polki", "juki", "bohaterowie", "gracz", "wozy" };

        // jakosci (indeks = ItemQuality w grze 1.4.8: Poor 0, Inferior 1, Common 2, Fine 3, Masterwork 4, Legendary 5)
        private const int NQ = 6;

        internal static bool On { get { var s = Settings.Current; return s != null && s.QualityScarcity; } }

        private static int CapL { get { var s = Settings.Current; return s == null ? 6 : Math.Max(0, Math.Min(200, s.QualityLegendaryWorldCap)); } }
        private static int CapM { get { var s = Settings.Current; return s == null ? 60 : Math.Max(0, Math.Min(2000, s.QualityMasterworkWorldCap)); } }
        private static float ChanceL { get { var s = Settings.Current; return s == null ? 0f : Math.Max(0f, Math.Min(0.05f, s.QualityLegendaryChance)); } }
        private static float ChanceM { get { var s = Settings.Current; return s == null ? 0f : Math.Max(0f, Math.Min(0.05f, s.QualityMasterworkChance)); } }

        // ------------------------------------------------------------ stan (czyszczony miedzy kampaniami - konstruktor zachowania)
        private static int _censusDay = -1;          // doba ostatniego spisu (-1 = jeszcze nie bylo: przy powstaniu bez legendarnych i mistrzowskich)
        private static int _worldL, _worldM;          // ze spisu (po wyrownaniu)
        private static int _newL, _newM;              // nowe od spisu (rezerwa limitu)
        private static readonly int[] _pieces = new int[NSrc], _gameL = new int[NSrc], _gameM = new int[NSrc], _madeL = new int[NSrc], _madeM = new int[NSrc];
        private static int _capL, _capM, _noCensus, _noMod;
        private static int _stumbles;
        private static bool _errLogged;
        private static readonly Dictionary<ItemModifierGroup, ItemModifier[]> _mods = new Dictionary<ItemModifierGroup, ItemModifier[]>();
        private static readonly object _modsLock = new object();

        // spis doby
        private static readonly int[,] _cnt = new int[NPlace, NQ];
        private static long _plainShelf;
        private struct Entry { public ItemRoster R; public EquipmentElement El; public int N; public int V; }
        private static readonly List<Entry> _shelfL = new List<Entry>(), _shelfM = new List<Entry>();
        private static readonly HashSet<Equipment> _seenEq = new HashSet<Equipment>();
        private static readonly Comparison<Entry> ByValueDesc = (a, b) => b.V.CompareTo(a.V);
        private static int _crafted, _craftedL, _craftedM;   // wyroby gracza (IsCraftedByPlayer) legendarne / mistrzowskie na polkach - nietykalne
        private static int _dLM, _dLF, _dMF, _dPlain, _dFromL, _dFromM, _dStacks; private static long _dValue;   // obnizki doby (do zwyklej - grupa bez Fine albo pociski)
        private static int _dSkip, _dFail;             // obnizki doby: pominiete (pociski bez celu o nie wiekszym stosie), nieudane zdjecie z polki
        private static int _wornOrig;                  // zuzyte sztuki gracza liczone wedlug stanu z ksiegi zuzycia (legendarne / mistrzowskie)
        private static int _tourneyFixed;              // nagrody turnieju zamowione u rzemieslnikow, ktorym regula zmienila jakosc (od wczoraj)
        private static bool _wiredAux, _wiredLoot, _wiredEnlist;
        private static bool _dteLooked; private static System.Reflection.FieldInfo _dteArmory;   // DynamicTroopEquipmentReupload.ArmyArmory.Armory (ItemRoster gracza)
        private static readonly Action<Settlement, Settlement, EquipmentElement, int> CartLine = (a, b, el, n) => Add(PCart, null, el, n);
        // ostatnia sztuka, ktorej regula dala legendarna/mistrzowska - drugi przebieg tej samej sztuki (ROTGankBehavior.GetRandomItem wola sam siebie,
        // postfiks biegnie na obu poziomach) nie odbiera jej jak rzutu gry
        [ThreadStatic] private static ItemObject _lastItem;
        [ThreadStatic] private static ItemModifier _lastMod;
        private static int _lastLineDay = -1;
        private static double _startMs;

        internal static void Reset()
        {
            _censusDay = -1; _worldL = _worldM = 0; _newL = _newM = 0;
            Array.Clear(_pieces, 0, NSrc); Array.Clear(_gameL, 0, NSrc); Array.Clear(_gameM, 0, NSrc); Array.Clear(_madeL, 0, NSrc); Array.Clear(_madeM, 0, NSrc);
            _capL = _capM = _noCensus = _noMod = 0; _stumbles = 0; _errLogged = false;
            lock (_modsLock) _mods.Clear();
            Array.Clear(_cnt, 0, _cnt.Length); _plainShelf = 0; _shelfL.Clear(); _shelfM.Clear(); _seenEq.Clear(); _crafted = _craftedL = _craftedM = 0;
            _dLM = _dLF = _dMF = _dPlain = _dFromL = _dFromM = _dStacks = 0; _dValue = 0; _dSkip = _dFail = 0; _wornOrig = 0; _tourneyFixed = 0;
            _lastLineDay = -1; _lastItem = null; _lastMod = null;
        }

        private static void Stumble(string where, Exception e)
        {
            Interlocked.Increment(ref _stumbles);
            if (!_errLogged) { _errLogged = true; Log.Error("QualityScarcity." + where, e); }   // raz w logu, reszta w liczniku potkniec
        }

        /// <summary>Najdrozszy modyfikator grupy z dana etykieta jakosci (grupa chain ma etykiety pomieszane tylko w stanach zuzycia i slabych -
        /// Legendary, Masterwork i Fine sa jednoznaczne w kazdej grupie broni i zbroi Native/RBM). null = grupa nie ma takiej jakosci (konie, BK).</summary>
        private static ItemModifier ModOf(ItemModifierGroup g, ItemQuality q)
        {
            if (g == null) return null;
            int i = (int)q;
            if (i < 0 || i >= NQ) return null;
            ItemModifier[] a;
            lock (_modsLock)
            {
                if (!_mods.TryGetValue(g, out a))
                {
                    a = new ItemModifier[NQ];
                    var all = g.ItemModifiers;
                    for (int k = 0; k < all.Count; k++)
                    {
                        var m = all[k];
                        if (m == null || m.PriceMultiplier <= 0f) continue;
                        int j = (int)m.ItemQuality;
                        if (j < 0 || j >= NQ) continue;
                        if (a[j] == null || m.PriceMultiplier > a[j].PriceMultiplier) a[j] = m;
                    }
                    _mods[g] = a;
                }
            }
            return a[i];
        }

        private static ItemModifierGroup GroupOf(ItemObject it) { return it != null && it.ItemComponent != null ? it.ItemComponent.ItemModifierGroup : null; }

        private static int QIdx(ItemModifier m) { if (m == null) return -1; int j = (int)m.ItemQuality; return j >= 0 && j < NQ ? j : -1; }

        // ------------------------------------------------------------ 1. powstanie sztuki
        /// <summary>
        /// Jakosc nowej sztuki: rzut gry (rolled) przechodzi przez regule. Legendary i Masterwork z rzutu gry -> zwykla; wlasny rzut: legendarna
        /// (tier 5-6) i mistrzowska (tier 4-6) w granicy limitu swiata. Reszta rzutu gry bez zmian. Wylaczony klucz albo sztuka bez grupy - rolled.
        /// own = false: tylko odbior rzutu gry, bez wlasnego rzutu (droga gry, lup, nagroda zaciagu, pierwszy przebieg jenca ROT - sztuka moze nigdzie
        /// nie trafic, a wlasny rzut zajmowalby miejsce w limicie).
        /// </summary>
        internal static ItemModifier Roll(ItemObject item, ItemModifier rolled, int src) { return Roll(item, rolled, src, true); }

        private static ItemModifier Roll(ItemObject item, ItemModifier rolled, int src, bool own)
        {
            if (item == null || src < 0 || src >= NSrc || !On) return rolled;
            try
            {
                var grp = item.ItemComponent != null ? item.ItemComponent.ItemModifierGroup : null;
                if (grp == null) return rolled;
                Interlocked.Increment(ref _pieces[src]);
                ItemModifier baseMod = rolled;
                if (src == SrcGank)
                {
                    // tylko jeniec ROT i tylko nastepne wywolanie: ta sama sztuka z tym samym modyfikatorem prosto z poziomu nizej jest juz po regule
                    var li = _lastItem; var lm = _lastMod; _lastItem = null; _lastMod = null;
                    if (rolled != null && ReferenceEquals(item, li) && ReferenceEquals(rolled, lm)) return rolled;
                }
                if (rolled != null)
                {
                    var q = rolled.ItemQuality;
                    if (q == ItemQuality.Legendary) { Interlocked.Increment(ref _gameL[src]); baseMod = null; }
                    else if (q == ItemQuality.Masterwork) { Interlocked.Increment(ref _gameM[src]); baseMod = null; }
                    else if (q != ItemQuality.Fine) return rolled;   // fuszerka zostaje fuszerka; stany bez etykiety (konie, zuzycie, lup Spoils) - bez zmian
                }
                if (!own) return baseMod;
                int grade = Recipes.Grade(item);
                float pl = ChanceL, pm = ChanceM;
                if (grade >= 5 && pl > 0f && MBRandom.RandomFloat < pl)
                {
                    var m = ModOf(grp, ItemQuality.Legendary);
                    if (m == null) Interlocked.Increment(ref _noMod);
                    else if (_censusDay < 0) Interlocked.Increment(ref _noCensus);
                    else if (_worldL + _newL >= CapL) Interlocked.Increment(ref _capL);
                    else { Interlocked.Increment(ref _newL); Interlocked.Increment(ref _madeL[src]); if (src == SrcGank) { _lastItem = item; _lastMod = m; } return m; }
                }
                if (grade >= 4 && pm > 0f && MBRandom.RandomFloat < pm)
                {
                    var m = ModOf(grp, ItemQuality.Masterwork);
                    if (m == null) Interlocked.Increment(ref _noMod);
                    else if (_censusDay < 0) Interlocked.Increment(ref _noCensus);
                    else if (_worldM + _newM >= CapM) Interlocked.Increment(ref _capM);
                    else { Interlocked.Increment(ref _newM); Interlocked.Increment(ref _madeM[src]); if (src == SrcGank) { _lastItem = item; _lastMod = m; } return m; }
                }
                return baseMod;
            }
            catch (Exception e) { Stumble("Roll", e); return rolled; }
        }

        /// <summary>Gotowy element z postfiksu (droga gry, lup, nagroda zaciagu, jeniec ROT): tylko odbior rzutu gry Legendary/Masterwork, bez
        /// wlasnego rzutu - ten element moze nigdzie nie trafic (BK kladzie wyrob drogi gry bez modyfikatora, MapEvent odrzuca czesc lupu, HeroGear
        /// podmienia bron jenca na zamiennik z wlasnym rzutem).</summary>
        internal static void Filter(ref EquipmentElement el, int src)
        {
            try
            {
                var it = el.Item;
                if (it == null || !On) return;
                var m0 = el.ItemModifier;
                var m1 = Roll(it, m0, src, false);
                if (!ReferenceEquals(m0, m1)) el = new EquipmentElement(it, m1);
            }
            catch (Exception e) { Stumble("Filter", e); }
        }

        /// <summary>PlayerFees8.SettlePrize, sciezka TourneyPrizeCommissioned: nagrody nie bylo na polce, miasto zamowilo ja u rzemieslnikow i placi
        /// cene ZWYKLEJ sztuki - jakosc z rzutu RBMTournament (ok. 5% legendarna, 10% mistrzowska) idzie przez regule warsztatow (wlasny rzut w limicie
        /// swiata). Podmienia sztuke w sakwach gracza (ta sama liczba sztuk); el - sztuka po regule.</summary>
        internal static void Commissioned(ItemRoster mine, ref EquipmentElement el, Settlement st)
        {
            try
            {
                var it = el.Item;
                if (it == null || mine == null || !On || mine.FindIndexOfElement(el) < 0) return;
                var m0 = el.ItemModifier;
                int q0 = QIdx(m0);
                var m1 = Roll(it, m0, SrcTourney, true);
                if (ReferenceEquals(m0, m1)) return;
                var el1 = new EquipmentElement(it, m1);
                if (mine.AddToCounts(el, -1) < 0) return;
                mine.AddToCounts(el1, 1);
                el = el1;
                _tourneyFixed++;
                Log.Info("Jakosc przedmiotow (m96jakosc): nagroda turnieju zamowiona u rzemieslnikow " + (st != null && st.Name != null ? st.Name.ToString() : "?") + " - "
                         + it.StringId + " " + (m0 != null ? m0.StringId : "zwykla") + " -> " + (m1 != null ? m1.StringId : "zwykla") + ".");
                if ((q0 == (int)ItemQuality.Legendary || q0 == (int)ItemQuality.Masterwork) && QIdx(m1) < q0)   // RBM oglosil rzut - gracz wie, czemu sztuka inna
                    Log.Player("The craftsmen of " + (st != null && st.Name != null ? st.Name.ToString() : "the town") + " made your prize, " + (it.Name != null ? it.Name.ToString() : "the piece")
                               + ", as " + (m1 != null && m1.Name != null ? "a " + m1.ItemQuality.ToString().ToLowerInvariant() + " piece" : "a plain piece")
                               + " - legendary and masterwork pieces are rare in the world.");
            }
            catch (Exception e) { Stumble("Commissioned", e); }
        }

        /// <summary>Postfiks WorkshopsCampaignBehavior.GetRandomItemAux (Priority.Last - po WorkshopLaw.RandomItemPostfix i CraftRealm.FilterRandom).</summary>
        public static void AuxPostfix(ref EquipmentElement __result) { Filter(ref __result, SrcGame); }

        /// <summary>Postfiks DefaultBattleRewardModel.GetLootedItemFromTroop (Priority.Last - po BK LootScale): lup gracza z poleglych.</summary>
        public static void LootPostfix(ref EquipmentElement __result) { Filter(ref __result, SrcLoot); }

        /// <summary>Postfiks ROTEnlistmentBehavior.GetRandomItem(int, Settlement): sztuka z wagami lupu dla gracza w zaciagu (EnlistmentPatches).</summary>
        public static void EnlistPostfix(ref EquipmentElement __result) { Filter(ref __result, SrcEnlist); }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var aux = AccessTools.DeclaredMethod(typeof(WorkshopsCampaignBehavior), "GetRandomItemAux");
                if (aux != null) { h.Patch(aux, postfix: new HarmonyMethod(typeof(QualityScarcity), nameof(AuxPostfix)) { priority = Priority.Last }); _wiredAux = true; }
                var loot = AccessTools.DeclaredMethod(typeof(DefaultBattleRewardModel), "GetLootedItemFromTroop");
                if (loot != null && loot.ReturnType == typeof(EquipmentElement))
                { h.Patch(loot, postfix: new HarmonyMethod(typeof(QualityScarcity), nameof(LootPostfix)) { priority = Priority.Last }); _wiredLoot = true; }
                // ROT 8.1.8: nagroda zaciagu (internal static GetRandomItem(int, Settlement) -> EquipmentElement); innej sygnatury nie ruszamy
                var tEn = QuartermasterLaw.FindType("ROT.CampaignBehaviors.ROTEnlistmentBehavior");
                var en = tEn != null ? AccessTools.DeclaredMethod(tEn, "GetRandomItem", new[] { typeof(int), typeof(Settlement) }) : null;
                if (en != null && en.ReturnType == typeof(EquipmentElement))
                { h.Patch(en, postfix: new HarmonyMethod(typeof(QualityScarcity), nameof(EnlistPostfix)) { priority = Priority.Last }); _wiredEnlist = true; }
                Log.Info("Jakosc przedmiotow (m96jakosc): wpiete - warsztaty AI (WorkshopLaw, w kodzie), jeniec ROT (HeroGear, w kodzie), nagroda turnieju zamowiona"
                         + " (PlayerFees8, w kodzie), tylko odbior rzutu gry: droga gry "
                         + (_wiredAux ? "GetRandomItemAux" : "BRAK GetRandomItemAux") + ", lup gracza " + (_wiredLoot ? "GetLootedItemFromTroop" : "BRAK GetLootedItemFromTroop")
                         + ", nagroda zaciagu ROT " + (_wiredEnlist ? "GetRandomItem" : tEn == null ? "(ROT nieobecny)" : "inna sygnatura - bez latki") + ".");
            }
            catch (Exception e) { Log.Error("QualityScarcity.ApplyAll", e); }
        }

        internal static void SessionStart()
        {
            try
            {
                // spis bez wyrownania: limit przy powstaniu sztuk zna swiat od pierwszej godziny (wyrownanie i linia - w dobie)
                if (Campaign.Current != null)
                {
                    long t0 = Stopwatch.GetTimestamp();
                    Census();
                    _worldL = Sum((int)ItemQuality.Legendary); _worldM = Sum((int)ItemQuality.Masterwork);
                    _newL = 0; _newM = 0; _censusDay = (int)CampaignTime.Now.ToDays;
                    _shelfL.Clear(); _shelfM.Clear();
                    _startMs = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                }
                Log.Info("Jakosc przedmiotow (m96jakosc): start sesji | klucz Quality Scarcity " + (On ? "TAK" : "NIE (tylko spis)")
                         + " | limit swiata: legendarne " + CapL + ", mistrzowskie " + CapM
                         + " | szansa na sztuke: legendarna " + (ChanceL * 100f).ToString("0.000") + "% (tier 5-6), mistrzowska " + (ChanceM * 100f).ToString("0.000") + "% (tier 4-6)"
                         + " | latki: droga gry " + (_wiredAux ? "tak" : "nie") + ", lup gracza " + (_wiredLoot ? "tak" : "nie") + ", nagroda zaciagu ROT " + (_wiredEnlist ? "tak" : "nie")
                         + " | spis przy wczytaniu: legendarne " + _worldL + " (" + Places((int)ItemQuality.Legendary) + "), mistrzowskie " + _worldM + " (" + Places((int)ItemQuality.Masterwork)
                         + "), dobre " + Sum((int)ItemQuality.Fine) + ", zwykle uzbrojenie na polkach " + _plainShelf + ", koszt " + _startMs.ToString("0.0") + " ms.");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        // ------------------------------------------------------------ 2. spis raz na dobe
        private static void Add(int place, ItemRoster r, EquipmentElement el, int n)
        {
            var it = el.Item;
            if (it == null || n <= 0) return;
            var m = el.ItemModifier;
            if (m == null)
            {
                if (place == PShelf && it.ItemComponent != null && it.ItemComponent.ItemModifierGroup != null && (it.WeaponComponent != null || it.ArmorComponent != null)) _plainShelf += n;
                return;
            }
            int q = QIdx(m);
            if (q < 0 || q == (int)ItemQuality.Common) return;
            _cnt[place, q] += n;
            if (place != PShelf || r == null) return;
            if (q != (int)ItemQuality.Legendary && q != (int)ItemQuality.Masterwork) return;
            if (it.IsCraftedByPlayer)   // bron wykuta przez gracza - nietykalna (liczona w spisie)
            {
                _crafted += n;
                if (q == (int)ItemQuality.Legendary) _craftedL += n; else _craftedM += n;
                return;
            }
            var e = new Entry { R = r, El = el, N = n, V = it.Value };
            if (q == (int)ItemQuality.Legendary) _shelfL.Add(e); else _shelfM.Add(e);
        }

        private static void Roster(int place, ItemRoster r)
        {
            if (r == null) return;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                if (el.Amount > 0) Add(place, r, el.EquipmentElement, el.Amount);
            }
        }

        private static void Gear(int place, Equipment eq)
        {
            if (eq == null || !_seenEq.Add(eq)) return;   // wspolne obiekty (np. rynsztunek zmarlych gry) - raz
            for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
            {
                var el = eq[i];
                if (el.Item != null) Add(place, null, el, 1);
            }
        }

        /// <summary>Rynsztunek bojowy gracza: zuzycie Armoury podmienia modyfikator na stan zuzycia, a oryginal trzyma w ksiedze (GoodOriginal) - zuzyta
        /// legendarna/mistrzowska liczy sie wedlug oryginalu (po naprawie wraca), inaczej zwalnialaby miejsce w limicie.</summary>
        private static void PlayerBattleGear(Equipment eq)
        {
            if (eq == null || !_seenEq.Add(eq)) return;
            var ab = ArmouryBehavior.Instance;
            for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
            {
                var el = eq[i];
                if (el.Item == null) continue;
                ItemModifier orig = null;
                try { orig = ab != null ? ab.GoodOriginal(i) : null; } catch { orig = null; }
                int qo = QIdx(orig);
                if ((qo == (int)ItemQuality.Legendary || qo == (int)ItemQuality.Masterwork) && !ReferenceEquals(orig, el.ItemModifier))
                {
                    _wornOrig++;
                    Add(PPlayer, null, new EquipmentElement(el.Item, orig), 1);
                }
                else Add(PPlayer, null, el, 1);
            }
        }

        /// <summary>Zbrojownia DTE wojska gracza (DynamicTroopEquipmentReupload.ArmyArmory.Armory - statyczny ItemRoster z modyfikatorami); null bez DTE.</summary>
        private static ItemRoster DteArmory()
        {
            if (!_dteLooked)
            {
                _dteLooked = true;
                try { var t = QuartermasterLaw.FindType("DynamicTroopEquipmentReupload.ArmyArmory"); _dteArmory = t != null ? AccessTools.Field(t, "Armory") : null; }
                catch { _dteArmory = null; }
                if (_dteArmory != null && !_dteArmory.IsStatic) _dteArmory = null;
            }
            return _dteArmory != null ? _dteArmory.GetValue(null) as ItemRoster : null;
        }

        private static void Census()
        {
            Array.Clear(_cnt, 0, _cnt.Length); _plainShelf = 0; _shelfL.Clear(); _shelfM.Clear(); _seenEq.Clear(); _crafted = _craftedL = _craftedM = 0; _wornOrig = 0;
            var setts = Settlement.All;
            for (int i = 0; i < setts.Count; i++)
            {
                var s = setts[i];
                if (s == null || s.Party == null) continue;
                try
                {
                    Roster(PShelf, s.ItemRoster);
                    if (s.Stash != null && s.Stash.Count > 0) Roster(PPlayer, s.Stash);
                }
                catch (Exception e) { Stumble("Census(osada)", e); }
            }
            var main = MobileParty.MainParty;
            var pc = Clan.PlayerClan;
            var parties = MobileParty.All;
            for (int i = 0; i < parties.Count; i++)
            {
                var p = parties[i];
                if (p == null) continue;
                try { Roster(ReferenceEquals(p, main) || (pc != null && p.ActualClan == pc) ? PPlayer : PBag, p.ItemRoster); }
                catch (Exception e) { Stumble("Census(partia)", e); }
            }
            // zbrojownia DTE wojska gracza i wozy kupcow w drodze (tylko liczone - wyrownanie tylko na polkach)
            try { Roster(PPlayer, DteArmory()); } catch (Exception e) { Stumble("Census(DTE gracza)", e); }
            try { TradeCarts.ForEachCartLine(CartLine); } catch (Exception e) { Stumble("Census(wozy)", e); }
            var heroes = Hero.AllAliveHeroes;
            var me = Hero.MainHero;
            for (int i = 0; i < heroes.Count; i++)
            {
                var h = heroes[i];
                if (h == null) continue;
                try
                {
                    int place = h == me || (pc != null && h.Clan == pc) ? PPlayer : PHero;
                    if (h == me) PlayerBattleGear(h.BattleEquipment); else Gear(place, h.BattleEquipment);
                    Gear(place, h.CivilianEquipment);
                    Gear(place, h.StealthEquipment);
                }
                catch (Exception e) { Stumble("Census(bohater)", e); }
            }
            _seenEq.Clear();
        }

        private static int Sum(int q) { int n = 0; for (int p = 0; p < NPlace; p++) n += _cnt[p, q]; return n; }

        // ------------------------------------------------------------ 3. nadmiar na polkach osad
        /// <summary>Cel obnizki w grupie: najdrozszy modyfikator jakosci q o liczbie w stosie NIE WIEKSZEJ niz zrodlo (pociski i bron miotana - RBM
        /// large_bag_* daje +3..+15 w stosie; bez tego obnizka legendarnych strzal do "duzego kolczana" dawalaby strzaly z niczego), przy rownej cenie -
        /// stos blizszy zrodlu. ok = false: zadnego celu (wtedy zwykla, o ile zrodlo nie ma ujemnego stosu). Raz na dobe, bez alokacji.</summary>
        private static ItemModifier Target(ItemModifierGroup g, ItemQuality q, ItemModifier from, out bool plainOk)
        {
            int maxStack = from != null ? from.StackCount : 0;
            plainOk = maxStack >= 0;   // zwykla = stos 0
            if (g == null) return null;
            ItemModifier best = null;
            var all = g.ItemModifiers;
            for (int k = 0; k < all.Count; k++)
            {
                var m = all[k];
                if (m == null || m.PriceMultiplier <= 0f || m.ItemQuality != q || m.StackCount > maxStack) continue;
                if (best == null || m.PriceMultiplier > best.PriceMultiplier || (m.PriceMultiplier == best.PriceMultiplier && m.StackCount > best.StackCount)) best = m;
            }
            return best;
        }

        private static void Demote(ref Entry e, int n, ItemModifier to, int fromQ)
        {
            if (n <= 0) return;
            var r = e.R; var it = e.El.Item; var from = e.El.ItemModifier;
            if (r.AddToCounts(e.El, -n) < 0) { _dFail += n; return; }   // sztuki juz nie ma na polce - nic nie dopisujemy (nic z niczego)
            r.AddToCounts(new EquipmentElement(it, to), n);   // ta sama liczba sztuk na tej samej polce - nic nie znika
            float f0 = from != null ? from.PriceMultiplier : 1f, f1 = to != null ? to.PriceMultiplier : 1f;
            _dValue += (long)Math.Round((f0 - f1) * it.Value * n);
            _dStacks++;
            int tq = QIdx(to);
            if (fromQ == (int)ItemQuality.Legendary) _dFromL += n; else _dFromM += n;
            if (tq == (int)ItemQuality.Masterwork) _dLM += n;
            else if (tq == (int)ItemQuality.Fine) { if (fromQ == (int)ItemQuality.Legendary) _dLF += n; else _dMF += n; }
            else _dPlain += n;
            e.N -= n;
        }

        private static void Level()
        {
            // mistrzowskie: limit minus to, czego regula nie rusza (juki, bohaterowie, gracz, wyroby gracza na polkach)
            int shelfM = 0; for (int i = 0; i < _shelfM.Count; i++) shelfM += _shelfM[i].N;
            int fixedM = Sum((int)ItemQuality.Masterwork) - shelfM;
            int allowM = Math.Max(0, CapM - fixedM);
            int keptM = 0;
            if (shelfM > allowM)
            {
                _shelfM.Sort(ByValueDesc);   // zostaja najdrozsze
                int left = allowM;
                for (int i = 0; i < _shelfM.Count; i++)
                {
                    var e = _shelfM[i];
                    int keep = Math.Min(e.N, left); left -= keep; keptM += keep;
                    int d = e.N - keep;
                    if (d <= 0) continue;
                    bool plainOk;
                    var fine = Target(GroupOf(e.El.Item), ItemQuality.Fine, e.El.ItemModifier, out plainOk);
                    if (fine == null && !plainOk) { _dSkip += d; keptM += d; continue; }   // pocisk z ujemnym stosem bez celu - zostaje (w danych gry nie ma)
                    Demote(ref e, d, fine, (int)ItemQuality.Masterwork);
                }
            }
            else keptM = shelfM;
            int roomM = Math.Max(0, allowM - keptM);   // miejsce na legendarne zepchniete o stopien

            int shelfL = 0; for (int i = 0; i < _shelfL.Count; i++) shelfL += _shelfL[i].N;
            int fixedL = Sum((int)ItemQuality.Legendary) - shelfL;
            int allowL = Math.Max(0, CapL - fixedL);
            if (shelfL > allowL)
            {
                _shelfL.Sort(ByValueDesc);
                int left = allowL;
                for (int i = 0; i < _shelfL.Count; i++)
                {
                    var e = _shelfL[i];
                    int keep = Math.Min(e.N, left); left -= keep;
                    int d = e.N - keep;
                    if (d <= 0) continue;
                    var g = GroupOf(e.El.Item);
                    bool plainOkM, plainOkF;
                    var mw = Target(g, ItemQuality.Masterwork, e.El.ItemModifier, out plainOkM);
                    int toM = mw != null ? Math.Min(d, roomM) : 0;
                    if (toM > 0) { Demote(ref e, toM, mw, (int)ItemQuality.Legendary); roomM -= toM; }
                    if (d - toM <= 0) continue;
                    var fine = Target(g, ItemQuality.Fine, e.El.ItemModifier, out plainOkF);
                    if (fine == null && !plainOkF) { _dSkip += d - toM; continue; }
                    Demote(ref e, d - toM, fine, (int)ItemQuality.Legendary);
                }
            }
        }

        /// <summary>Doba (DailyTickEvent zachowania): spis, wyrownanie nadmiaru na polkach (przy kluczu), linia logu.</summary>
        internal static void Daily()
        {
            try
            {
                if (Settings.Current == null || Campaign.Current == null) return;
                int day = (int)CampaignTime.Now.ToDays;
                if (day == _lastLineDay) return;
                _lastLineDay = day;
                long t0 = Stopwatch.GetTimestamp();
                _dLM = _dLF = _dMF = _dPlain = _dFromL = _dFromM = _dStacks = 0; _dValue = 0; _dSkip = _dFail = 0;
                Census();
                int before0 = Sum((int)ItemQuality.Legendary), before1 = Sum((int)ItemQuality.Masterwork);
                int shelfL0 = _cnt[PShelf, (int)ItemQuality.Legendary], shelfM0 = _cnt[PShelf, (int)ItemQuality.Masterwork];
                bool on = On;
                if (on)
                {
                    try { Level(); } catch (Exception e) { Stumble("Level", e); }
                }
                // po wyrownaniu: polki przesuniete o obnizki (sztuk tyle samo)
                _cnt[PShelf, (int)ItemQuality.Legendary] -= _dFromL;
                _cnt[PShelf, (int)ItemQuality.Masterwork] += _dLM - _dFromM;
                _cnt[PShelf, (int)ItemQuality.Fine] += _dLF + _dMF;
                _worldL = Sum((int)ItemQuality.Legendary); _worldM = Sum((int)ItemQuality.Masterwork);
                int newL = _newL, newM = _newM;
                _newL = 0; _newM = 0; _censusDay = day;
                double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                Line(day, on, before0, before1, shelfL0, shelfM0, newL, newM, ms);
                _shelfL.Clear(); _shelfM.Clear();
            }
            catch (Exception e) { Stumble("Daily", e); }
        }

        private static string Places(int q)
        {
            var sb = new StringBuilder();
            for (int p = 0; p < NPlace; p++) { if (p > 0) sb.Append(", "); sb.Append(PlaceName[p]).Append(' ').Append(_cnt[p, q]); }
            return sb.ToString();
        }

        private static string BySrc(int[] a)
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < NSrc; i++) { if (i > 0) sb.Append(", "); sb.Append(SrcName[i]).Append(' ').Append(a[i]); }
            return sb.Append(']').ToString();
        }

        private static int Tot(int[] a) { int n = 0; for (int i = 0; i < a.Length; i++) n += a[i]; return n; }

        private static void Line(int day, bool on, int worldL0, int worldM0, int shelfL0, int shelfM0, int newL, int newM, double ms)
        {
            var sb = new StringBuilder(900);
            sb.Append("Jakosc przedmiotow (m96jakosc): dzien ").Append(day)
              .Append(" | regula ").Append(on ? "TAK" : "NIE (tylko spis)")
              .Append(", limit swiata: legendarne ").Append(CapL).Append(", mistrzowskie ").Append(CapM)
              .Append(" | spis przed wyrownaniem: legendarne ").Append(worldL0).Append(" (polki ").Append(shelfL0).Append(")")
              .Append(", mistrzowskie ").Append(worldM0).Append(" (polki ").Append(shelfM0).Append(")")
              .Append(" | swiat po: legendarne ").Append(_worldL).Append(" (").Append(Places((int)ItemQuality.Legendary)).Append(")")
              .Append(", mistrzowskie ").Append(_worldM).Append(" (").Append(Places((int)ItemQuality.Masterwork)).Append(")")
              .Append(", dobre ").Append(Sum((int)ItemQuality.Fine)).Append(" (").Append(Places((int)ItemQuality.Fine)).Append(")")
              .Append(", slabe ").Append(Sum((int)ItemQuality.Inferior)).Append(", zepsute ").Append(Sum((int)ItemQuality.Poor))
              .Append(" | zwykle uzbrojenie na polkach ").Append(_plainShelf)
              .Append(" | nowe od wczoraj: legendarne ").Append(newL).Append(' ').Append(BySrc(_madeL))
              .Append(", mistrzowskie ").Append(newM).Append(' ').Append(BySrc(_madeM))
              .Append(" z sztuk z jakoscia ").Append(Tot(_pieces)).Append(' ').Append(BySrc(_pieces))
              .Append(" | rzut gry odebrany: legendarne ").Append(Tot(_gameL)).Append(' ').Append(BySrc(_gameL))
              .Append(", mistrzowskie ").Append(Tot(_gameM)).Append(' ').Append(BySrc(_gameM))
              .Append(" | zatrzymane limitem swiata: legendarne ").Append(_capL).Append(", mistrzowskie ").Append(_capM)
              .Append("; przed pierwszym spisem ").Append(_noCensus).Append("; grupa bez tej jakosci ").Append(_noMod)
              .Append(" | obnizki na polkach: legendarne->mistrzowskie ").Append(_dLM).Append(", legendarne->dobre ").Append(_dLF)
              .Append(", mistrzowskie->dobre ").Append(_dMF).Append(", do zwyklej (grupa bez Fine albo pociski) ").Append(_dPlain).Append(" (stosow ").Append(_dStacks).Append("), wartosc towaru na polkach -").Append(_dValue).Append(" zl (zloto nie rusza)")
              .Append("; pominiete (pociski bez celu) ").Append(_dSkip).Append(", nieudane zdjecie z polki ").Append(_dFail)
              .Append(" | wyroby gracza na polkach nietykalne ").Append(_crafted).Append(" (legendarne ").Append(_craftedL).Append(", mistrzowskie ").Append(_craftedM).Append(")")
              .Append(" | zuzyte rzeczy gracza wedlug ksiegi ").Append(_wornOrig).Append(", nagrody turnieju zamowione z jakoscia po regule ").Append(_tourneyFixed)
              .Append(" | koszt ").Append(ms.ToString("0.0")).Append(" ms | potkniecia ").Append(_stumbles).Append('.');
            Log.Info(sb.ToString());
            Array.Clear(_pieces, 0, NSrc); Array.Clear(_gameL, 0, NSrc); Array.Clear(_gameM, 0, NSrc); Array.Clear(_madeL, 0, NSrc); Array.Clear(_madeM, 0, NSrc);
            _capL = _capM = _noCensus = _noMod = 0; _tourneyFixed = 0;
        }
    }

    /// <summary>m96-jakosc: doba (spis, wyrownanie, linia) i start sesji; bez stanu w zapisie (limit liczony ze spisu).</summary>
    internal sealed class QualityScarcityBehavior : CampaignBehaviorBase
    {
        public QualityScarcityBehavior() { QualityScarcity.Reset(); }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, DayProfile.Dx("QualityScarcity.Daily", QualityScarcity.Daily));
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, s => QualityScarcity.SessionStart());
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}
