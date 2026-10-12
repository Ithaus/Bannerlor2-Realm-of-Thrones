using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// E6/R1 (214, PLAN 6.1, R1/R2; projekt etapu 6 v2 rozdz. 3 "E6/R1"): WYTOP PRZY KOPALNI. Tylko nowa kampania (flaga w zapisie
    /// arm_e6_bloom, wzor CamelLaw) i klucz BloomeryAtMine; stary zapis - ruda 100 kg jak dotad (stara ruda stalaby sie lupami).
    ///  - Sztuka "iron" to lupa "Iron Bloom": metal z ladunku rudy (100 kg x WorkshopCrudeKgPerOre / 10 = 15 kg surowki), cena = surowka
    ///    + wegiel dymarki za kg (HistoricalPrices.Apply krok 0 i 1). Liczba sztuk z kopalni bez zmian: wszystko, co LICZY sztuki
    ///    (wydobycie MaterialLaw.ProdPostfix, kopalnie BK WorkshopLaw.BulkLineSkip, place cyklu WorkshopTrade.BulkFactor, zapas startowy
    ///    StartStock, przelicznik popytu), bierze HistoricalPrices.CountScale - lupa liczy sie jak ladunek rudy, nie wedlug wagi 15 kg
    ///    (pulapka K13:227-229: BulkScale dalby 6.7 x zelaza). Receptury (WorkshopLaw.Needs, MendMaterial) licza metal przez CountKg.
    ///  - Dymarka pali PRAWDZIWE drewno (v2, KRYTYKA K1-01): WoodPerBloom ladunkow (WorkshopWoodPerOre x 100 kg rudy / ladunek drewna = 5)
    ///    na lupe. Skad: (1) las wsi okregu kopalni (VillageWoodlot wsi z tym samym TradeBound) - zdjety z wozu na targ, zanim pojedzie
    ///    (FromWoodlot; dymarka trzyma najwyzej 2 doby potrzeby); (2) reszta - woz wsi gorniczej kupuje drewno z polki miasta handlowego
    ///    za zloto wsi po cenie targu, raz na dobe (Daily), na NASTEPNA dobe (R15 - zadnego przerzutu w tej samej chwili); (3) brak drewna -
    ///    w ticku produkcji wsi (TickGoodProduction) model daje najwyzej tyle lup, na ile starczy drewna w dymarce (Cap - sufit LimitMax
    ///    w ProdPostfix), reszta rudy niewydobyta ("kopalnia bez wegla", nic z niczego). Po ticku: drewno spalone = WoodPerBloom x lupy
    ///    dopisane wsi (roznica magazynu przed i po ticku) - tozsamosc z linii.
    ///  - Kuznia miasta pali juz tylko wegiel kowalski (WorkshopLaw.Needs bez czlonu dymarki, MendMaterial bez drewna dymarki); kopalnia BK
    ///    w miescie/zamku (linia bez wsadu) wytapia z drewnem z polki swojego miasta (TownMineBlocked). Kuznia gracza: 1 lupa -> sztaby
    ///    surowki wedlug masy (MaterialLaw.RefinePostfix). Zlom (ArmsScrap, Drill) wraca w lupach sam - WorkshopLaw.Needs liczy w lupach.
    ///  - Stan w zapisie: flaga, drewno w dymarce i potrzeba doby kazdej kopalni (ladunki). Drewno dymarki jest poza rosterami: wchodzi
    ///    z lasu okregu (nie wchodzi do zapasu) albo z polki miasta (ujscie zapasu - ksiega drewna "dymarki przy kopalniach", ksiega towarow
    ///    ramka FBloom) i wychodzi tylko w ogniu dymarki przy lupie.
    /// Koszt: ticki O(1) na wies, zakup raz na dobe po kopalniach (ok. 26), okregi z cache przebudowywanym raz na dobe.
    /// Wyjatki liczone per miejsce (pierwsze 3 do logu) - bez globalnych wylacznikow (CLAUDE.md 7).
    /// </summary>
    internal static class Bloomery
    {
        private static readonly TextObject _txtNoCoal = new TextObject("{=!}Armoury: bloomery short of wood");

        internal static bool NewCampaign;
        private static ItemObject _iron, _wood;
        private static bool _applied, _wired;
        private static float _scale = 1f;
        private static int _wpb = -1;

        private sealed class Mine { public int Bank, Want; }
        private static readonly Dictionary<Village, Mine> _mines = new Dictionary<Village, Mine>();
        private static Dictionary<Settlement, List<Village>> _byMarket;
        private static string _pending;

        // liczniki doby (tylko log)
        private static int _dBlooms, _dBurnt, _dWoods, _dBought, _dGold, _dShortShelf, _dShortGold, _dNoMarket, _dTicked, _dOver;
        private static int _dTownMine, _dTownMineWood, _dTownMineSkip;
        private static float _dUnwon;
        private static int _stumbles, _stumblesDay;

        [ThreadStatic] private static Village _tv;
        // recenzja R1: kopalnia BK w miescie - drewno z polki dopiero po cyklu, ktory naprawde dal lupy (postfiks cyklu; gra moze odrzucic cykl PO prefiksie)
        [ThreadStatic] private static Workshop _pw;
        [ThreadStatic] private static int _pNeed, _pPrice, _pCount;
        private static bool _cycleWired;
        private static int _dTownMineRej;
        [ThreadStatic] private static int _tvBefore, _tvReserved;
        [ThreadStatic] private static float _tvModel;

        /// <summary>Nowa gra albo wczytanie (konstruktor ArmouryBehavior): flaga, kopalnie, liczniki od zera.</summary>
        internal static void Reset()
        {
            NewCampaign = false; _iron = null; _wood = null; _applied = false; _scale = 1f; _wpb = -1; _pw = null;
            _mines.Clear(); _byMarket = null; _pending = null;
            NewDay(); _stumbles = 0;
        }

        internal static void OnNewGame() { NewCampaign = true; }

        /// <summary>Lupa obowiazuje w tej sesji (waga i cena ustawione przez HistoricalPrices.Apply).</summary>
        internal static bool On { get { return _applied; } }

        internal static bool IsBloom(ItemObject it) { return _applied && it != null && ReferenceEquals(it, _iron); }

        /// <summary>Ile starych sztuk gry (10 kg rudy) liczy jedna lupa = HistBulkUnitFactor z chwili Apply (ladunek, z ktorego ja wytopiono).</summary>
        internal static float Scale { get { return _scale; } }

        /// <summary>HistoricalPrices.Apply (krok 0): klucz, nowa kampania, latka ticku produkcji wsi i postfiks modelu wsi (bez niego nie ma sufitu wegla).</summary>
        internal static bool CanApply()
        {
            var s = Settings.Current;
            return s != null && s.BloomeryAtMine && NewCampaign && _wired && MaterialLaw.ProdModels > 0;
        }

        internal static void MarkApplied(ItemObject iron, float scale)
        {
            _iron = iron; _scale = Math.Max(1f, scale); _applied = true; _wpb = -1;
            try
            {
                var setter = AccessTools.PropertySetter(typeof(ItemObject), "Name");
                if (setter != null) setter.Invoke(iron, new object[] { new TextObject("{=!}Iron Bloom{@Plural}iron blooms{\\@}", null) });
            }
            catch (Exception e) { Stumble("Name", e); }
        }

        /// <summary>Linia startowa (HistoricalPrices.Apply po kroku 0).</summary>
        internal static void StartLine(bool bulk)
        {
            try
            {
                var s = Settings.Current;
                string st;
                if (_applied) st = "CZYNNE - \"iron\" to lupa " + (_iron != null ? _iron.Weight.ToString("0.#", CultureInfo.InvariantCulture) : "?") + " kg (metal z ladunku rudy, liczona jak ladunek: x" + _scale.ToString("0.#", CultureInfo.InvariantCulture)
                                  + "), drewna na lupe " + WoodPerBloom() + " ladunkow; kopalni z zapisu " + (_pending != null ? "do rozwiazania" : _mines.Count.ToString());
                else if (s == null || !s.BloomeryAtMine) st = "WYLACZONE (klucz Bloomery At Mine) - ruda 100 kg jak dotad";
                else if (!NewCampaign) st = "NIEAKTYWNE - stary zapis (kampania sprzed paczki): ruda 100 kg jak dotad";
                else if (!bulk) st = "NIEAKTYWNE - przelicznik ladunku 1 (ceny historyczne wylaczone albo HistBulkUnitFactor = 1)";
                else st = "NIEAKTYWNE - brak latki " + (!_wired ? "ticku produkcji wsi (TickGoodProduction)" : "modelu produkcji wsi (MaterialLaw.ProdPostfix)");
                Log.Info("Bloomery (E6/R1, 214): " + st + ".");
            }
            catch (Exception e) { Stumble("StartLine", e); }
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            if (_stumbles <= 3) Log.Error("Bloomery." + where, e);
        }

        private static ItemObject Wood()
        {
            if (_wood == null) { try { _wood = MBObjectManager.Instance.GetObject<ItemObject>("hardwood"); } catch { } }
            return _wood;
        }

        /// <summary>Ladunki drewna na jedna lupe: WorkshopWoodPerOre (kg drewna na kg rudy) x kg rudy w lupie / waga ladunku drewna (5 przy 100 kg).</summary>
        internal static int WoodPerBloom()
        {
            if (_wpb >= 0) return _wpb;
            var s = Settings.Current; var w = Wood();
            if (s == null || w == null || _iron == null) return 0;
            float kg = Math.Max(0f, s.WorkshopWoodPerOre) * HistoricalPrices.CountKg(_iron);
            _wpb = kg > 0f ? Math.Max(1, (int)Math.Ceiling(kg / Math.Max(0.1f, w.Weight) - 1e-3f)) : 0;
            return _wpb;
        }

        // ------------------------------------------------------------ zapis
        internal static string Export()
        {
            if (!NewCampaign) return "";
            var sb = new StringBuilder("nk");
            if (_pending != null) { sb.Append('|').Append(_pending); return sb.ToString(); }   // wczytane, jeszcze nie rozwiazane - oddajemy jak bylo
            foreach (var kv in _mines)
            {
                var st = kv.Key != null ? kv.Key.Settlement : null;
                if (st == null || st.StringId == null) continue;
                sb.Append('|').Append(st.StringId).Append(':').Append(kv.Value.Bank).Append(':').Append(kv.Value.Want);
            }
            return sb.ToString();
        }

        internal static void Import(string s)
        {
            NewCampaign = s != null && s.StartsWith("nk", StringComparison.Ordinal);
            _mines.Clear(); _byMarket = null;
            _pending = NewCampaign && s.Length > 3 ? s.Substring(3) : null;
        }

        private static void ResolvePending()
        {
            var p = _pending;
            if (p == null) return;
            _pending = null;
            int ok = 0, bad = 0;
            foreach (var rec in p.Split('|'))
            {
                try
                {
                    var f = rec.Split(':');
                    if (f.Length != 3) { bad++; continue; }
                    var st = Settlement.Find(f[0]);
                    if (st == null || st.Village == null) { bad++; continue; }
                    _mines[st.Village] = new Mine { Bank = Math.Max(0, int.Parse(f[1], CultureInfo.InvariantCulture)), Want = Math.Max(0, int.Parse(f[2], CultureInfo.InvariantCulture)) };
                    ok++;
                }
                catch { bad++; }
            }
            _byMarket = null;
            Log.Info("Bloomery (E6/R1, 214): z zapisu " + ok + " kopalni (drewno w dymarkach, potrzeba doby)" + (bad > 0 ? ", nieczytelnych " + bad : "") + ".");
        }

        // ------------------------------------------------------------ tick produkcji wsi
        /// <summary>Prefiks VillageGoodProductionCampaignBehavior.TickGoodProduction(Village, bool), Priority.First (przed prefiksem BK): wies w ticku
        /// i stan lup w jej magazynie. Rozdanie startowe (initialProductionForTowns) - bez dymarki (zapas startowy gry, StartStock).</summary>
        public static void TickPrefix(Village __0, bool __1)
        {
            _tv = null;
            if (!_applied || __1 || __0 == null) return;
            try
            {
                if (_pending != null) ResolvePending();
                var r = __0.Settlement != null ? __0.Settlement.ItemRoster : null;
                if (r == null) return;
                _tvBefore = r.GetItemNumber(_iron); _tvReserved = 0; _tvModel = 0f;
                _tv = __0;
            }
            catch (Exception e) { _tv = null; Stumble("TickPrefix", e); }
        }

        /// <summary>Postfiks tego samego ticku: lupy dopisane wsi (roznica magazynu) spalaja WoodPerBloom ladunkow drewna dymarki; potrzeba doby
        /// = WoodPerBloom x wynik modelu bez sufitu (zaokraglony w gore).</summary>
        public static void TickPostfix(Village __0, bool __1)
        {
            var v = _tv;
            if (v == null || !ReferenceEquals(v, __0)) return;
            try
            {
                int made = Math.Max(0, v.Settlement.ItemRoster.GetItemNumber(_iron) - _tvBefore);
                Mine m;
                if (!_mines.TryGetValue(v, out m))
                {
                    if (made <= 0 && _tvModel <= 0f) return;
                    m = new Mine(); _mines[v] = m; _byMarket = null;
                }
                int wpb = WoodPerBloom();
                int burn = made * wpb;
                if (burn > m.Bank) { _dOver += wpb > 0 ? (burn - m.Bank + wpb - 1) / wpb : 0; burn = m.Bank; }   // nie powinno sie zdarzyc: sufit w modelu rezerwuje drewno
                m.Bank -= burn;
                m.Want = wpb * (int)Math.Ceiling(_tvModel - 1e-4f);
                _dBlooms += made; _dBurnt += burn; _dTicked++;
            }
            catch (Exception e) { Stumble("TickPostfix", e); }
        }

        public static Exception TickFinalizer(Exception __exception) { _tv = null; return __exception; }

        /// <summary>MaterialLaw.ProdPostfix (ostatni krok, przed ksiega): w ticku produkcji tej wsi lupy najwyzej tyle, na ile starczy drewna w dymarce
        /// (LimitMax, liczba calkowita - gra i BK nie dorzuca ulamka ponad nia). Drewno zarezerwowane na wczesniejsze wywolania w tym samym ticku
        /// (np. mineral dwa razy na liscie BK) odejmuje sie - lup nigdy wiecej niz drewna.</summary>
        internal static void Cap(Village village, ItemObject item, ref ExplainedNumber r)
        {
            var v = _tv;
            if (v == null || !ReferenceEquals(village, v) || !ReferenceEquals(item, _iron)) return;
            try
            {
                float x = r.ResultNumber;
                if (!(x > 0f)) return;
                _tvModel += x;
                int wpb = WoodPerBloom();
                if (wpb <= 0) return;
                Mine m;
                int bank = _mines.TryGetValue(village, out m) ? m.Bank : 0;
                int cap = Math.Max(0, bank / wpb - _tvReserved);
                if (x > cap)
                {
                    _dUnwon += x - cap;
                    r.LimitMax(Math.Min((float)cap, r.LimitMaxValue), _txtNoCoal);
                    _tvReserved += cap;
                }
                else _tvReserved += (int)Math.Ceiling(x - 1e-4f);
            }
            catch (Exception e) { Stumble("Cap", e); }
        }

        // ------------------------------------------------------------ las okregu
        private static List<Village> MinesOf(Settlement market)
        {
            if (_byMarket == null)
            {
                var d = new Dictionary<Settlement, List<Village>>();
                foreach (var v in _mines.Keys)
                {
                    var mk = v != null ? v.TradeBound : null;
                    if (mk == null) continue;
                    List<Village> l;
                    if (!d.TryGetValue(mk, out l)) d[mk] = l = new List<Village>();
                    l.Add(v);
                }
                _byMarket = d;
            }
            List<Village> res;
            return _byMarket.TryGetValue(market, out res) ? res : null;
        }

        /// <summary>VillageWoodlot.TickPostfix: z n ladunkow lasu wsi v ile idzie do dymarek kopalni jej okregu (TradeBound) - najpierw tam, gdzie
        /// najwiekszy brak do 2 x potrzeby doby. Reszta jedzie na targ jak dotad.</summary>
        internal static int FromWoodlot(Village v, int n)
        {
            if (!_applied || n <= 0 || v == null) return 0;
            try
            {
                if (_pending != null) ResolvePending();
                var mk = v.TradeBound;
                if (mk == null) return 0;
                var list = MinesOf(mk);
                if (list == null) return 0;
                int left = n;
                while (left > 0)
                {
                    Mine best = null; int room = 0;
                    foreach (var mv in list)
                    {
                        Mine m;
                        if (!_mines.TryGetValue(mv, out m)) continue;
                        int rm = 2 * m.Want - m.Bank;
                        if (rm > room) { room = rm; best = m; }
                    }
                    if (best == null) break;
                    int take = Math.Min(left, room);
                    best.Bank += take; left -= take;
                }
                int d = n - left;
                _dWoods += d;
                return d;
            }
            catch (Exception e) { Stumble("FromWoodlot", e); return 0; }
        }

        // ------------------------------------------------------------ zakup z polki miasta handlowego (raz na dobe)
        /// <summary>ArmouryBehavior.OnDailyTick (przed OreLedger.Daily): woz kazdej kopalni kupuje brakujace drewno do potrzeby doby z polki miasta
        /// handlowego za zloto wsi po cenie targu (zloto wies -> kasa miasta, towar z polki do dymarki) - drewno na nastepna dobe.</summary>
        internal static void Daily()
        {
            if (!_applied) return;
            try
            {
                if (_pending != null) ResolvePending();
                _byMarket = null;   // TradeBound wsi zamkowych gra przestawia (wojny, zmiana wlasciciela) - okregi od nowa raz na dobe
                var wood = Wood();
                if (wood == null) return;
                var el = new EquipmentElement(wood);
                foreach (var kv in _mines)
                {
                    try
                    {
                        var v = kv.Key; var m = kv.Value;
                        int want = m.Want - m.Bank;
                        if (want <= 0 || v == null) continue;
                        var mk = v.TradeBound;
                        var town = mk != null ? mk.Town : null;
                        var shelf = mk != null ? mk.ItemRoster : null;
                        if (town == null || shelf == null) { _dNoMarket += want; continue; }
                        int n = Math.Min(want, shelf.GetItemNumber(wood));
                        if (n < want) _dShortShelf += want - Math.Max(0, n);
                        if (n <= 0) continue;
                        int price = Math.Max(0, town.MarketData.GetPrice(el));
                        if (price > 0)
                        {
                            int afford = Math.Max(0, v.Gold) / price;
                            if (afford < n) { _dShortGold += n - afford; n = afford; }
                        }
                        if (n <= 0) continue;
                        int paid = 0;
                        var gf = GoodsLedger.Begin(GoodsLedger.FBloom, town);
                        try
                        {
                            if (price > 0)
                            {
                                int had = v.Gold;
                                v.ChangeGold(-n * price);
                                paid = had - v.Gold;
                                town.ChangeGold(paid);
                            }
                            shelf.AddToCounts(wood, -n);
                        }
                        finally { GoodsLedger.End(gf); }
                        m.Bank += n;
                        _dBought += n; _dGold += paid;
                        OreLedger.NoteBloomeryWood(wood, n);
                        if (paid > 0) { try { TownFolk.Sale(mk, paid, TownFolk.MixGoods); } catch { } }   // drewno kupione od kupcow miasta (tylko licznik)
                    }
                    catch (Exception e) { Stumble("Daily(kopalnia)", e); }
                }
            }
            catch (Exception e) { Stumble("Daily", e); }
        }

        // ------------------------------------------------------------ kopalnia BK w miescie / zamku
        /// <summary>WorkshopLaw.CyclePrefix po BulkLineSkip: linia bez wsadu dajaca lupy (kopalnia BK w miescie albo zamku) wytapia z drewnem z polki
        /// tej osady - warsztat kupuje WoodPerBloom x sztuki ladunkow po cenie targu (kasa warsztatu -> kasa miasta). Brak drewna albo kapitalu -
        /// cykl nie idzie (true). Linia drewna i reszta warsztatow bez zmian (false).
        /// Recenzja R1: gra po prefiksie moze cykl jeszcze odrzucic (CanNotableWorkshopProduceThisCycle: utarg <= place cyklu, kasa miasta < utarg) -
        /// zakup idzie wiec w postfiksie cyklu (CyclePostfix), tylko gdy cykl dal lupy; tu tylko sprawdzenie i rezerwacja. Bez postfiksu - zakup od razu (jak dotad).</summary>
        internal static bool TownMineBlocked(WorkshopType.Production p, Workshop w)
        {
            _pw = null;
            if (!_applied || w == null) return false;
            try
            {
                if (p.Inputs != null && p.Inputs.Count > 0) return false;
                if (p.Outputs == null || p.Outputs.Count == 0) return false;
                int count = 0;
                foreach (var o in p.Outputs)
                {
                    if (o.Item1 == null || o.Item1.StringId != "iron") return false;   // kategoria "iron" (wyrob losowany z kategorii - WorkshopLaw.RandomItemPostfix daje lupe zamiast sztabki)
                    count += Math.Max(1, o.Item2);
                }
                int wpb = WoodPerBloom();
                if (wpb <= 0) return false;
                var st = w.Settlement; var town = st != null ? st.Town : null; var shelf = st != null ? st.ItemRoster : null; var wood = Wood();
                if (town == null || shelf == null || wood == null) { _dTownMineSkip++; return true; }
                int need = wpb * count;
                int price = Math.Max(0, town.MarketData.GetPrice(new EquipmentElement(wood)));
                if (shelf.GetItemNumber(wood) < need || w.Capital < need * price) { _dTownMineSkip++; return true; }
                if (_cycleWired) { _pw = w; _pNeed = need; _pPrice = price; _pCount = count; return false; }
                BuyTownMineWood(w, st, town, shelf, wood, need, price, count);
                return false;
            }
            catch (Exception e) { Stumble("TownMineBlocked", e); return false; }
        }

        /// <summary>Postfiks WorkshopsCampaignBehavior.TickOneProductionCycleForNotableWorkshop: cykl kopalni BK zarezerwowany w TownMineBlocked dal lupy
        /// (__result) - dopiero teraz drewno z polki i zaplata; cykl odrzucony przez gre - nic (licznik).</summary>
        public static void CyclePostfix(Workshop __1, bool __result)
        {
            var w = _pw; _pw = null;
            if (w == null || !ReferenceEquals(w, __1)) return;
            if (!__result) { _dTownMineRej++; return; }
            try
            {
                var st = w.Settlement; var town = st != null ? st.Town : null; var shelf = st != null ? st.ItemRoster : null; var wood = Wood();
                if (town == null || shelf == null || wood == null) return;
                int need = Math.Min(_pNeed, Math.Max(0, shelf.GetItemNumber(wood)));
                int wpb = WoodPerBloom();
                if (need < _pNeed && wpb > 0) _dOver += (_pNeed - need + wpb - 1) / wpb;   // nie powinno sie zdarzyc: w cyklu nikt nie zdejmuje drewna
                if (need > 0) BuyTownMineWood(w, st, town, shelf, wood, need, _pPrice, _pCount);
            }
            catch (Exception e) { Stumble("CyclePostfix", e); }
        }

        private static void BuyTownMineWood(Workshop w, Settlement st, Town town, ItemRoster shelf, ItemObject wood, int need, int price, int count)
        {
            var gf = GoodsLedger.Begin(GoodsLedger.FBloom, town);
            try
            {
                shelf.AddToCounts(wood, -need);
                if (price > 0)
                {
                    w.ChangeGold(-need * price);
                    town.ChangeGold(need * price);
                }
            }
            finally { GoodsLedger.End(gf); }
            if (price > 0)
            {
                try { MoneyLedger.Note(MoneyLedger.NShop, st, need * price); } catch { }
                try { TownFolk.Sale(st, need * price, TownFolk.MixGoods); } catch { }
            }
            OreLedger.NoteBloomeryWood(wood, need);
            _dTownMine += count; _dTownMineWood += need;
        }

        // ------------------------------------------------------------ linia (OreLedger)
        /// <summary>Wstawka linii "Zelazo (lupy) (214)" po "wsie dopisaly a lup (...)": lupy z tickow kopalni (kontrola z OnItemProduced), drewno dymarek,
        /// kopalnia bez wegla, kopalnie BK w miastach.</summary>
        internal static string IronSegment(int villagesProduced)
        {
            var inv = CultureInfo.InvariantCulture;
            int bank = 0, want = 0, mines = 0, empty = 0;
            foreach (var kv in _mines) { mines++; bank += kv.Value.Bank; want += kv.Value.Want; if (kv.Value.Want > 0 && kv.Value.Bank < kv.Value.Want) empty++; }
            var sb = new StringBuilder();
            sb.Append(" | dymarki (E6/R1): lupy z tickow kopalni ").Append(_dBlooms).Append(" (kontrola wsie - ticki = ").Append(villagesProduced - _dBlooms)
              .Append("; kopalni z tickiem ").Append(_dTicked).Append(", znanych ").Append(mines)
              .Append("); wegiel dymarek: spalone ").Append(_dBurnt).Append(" ladunkow drewna = ").Append(WoodPerBloom()).Append(" x lupy (roznica ").Append(_dBurnt - WoodPerBloom() * _dBlooms)
              .Append("), las okregu ").Append(_dWoods).Append(", targ ").Append(_dBought).Append(" za ").Append(_dGold).Append(" zl (brak na polce ").Append(_dShortShelf)
              .Append(", brak zlota wsi ").Append(_dShortGold).Append(", bez miasta handlowego ").Append(_dNoMarket).Append(")")
              .Append("; kopalnia bez wegla ").Append(_dUnwon.ToString("0.#", inv)).Append(" lup (model ponad drewno w dymarce)")
              .Append(", lupy bez drewna ").Append(_dOver)
              .Append("; w dymarkach ").Append(bank).Append(" ladunkow przy potrzebie doby ").Append(want).Append(" (kopalni ponizej potrzeby ").Append(empty).Append(")")
              .Append("; kopalnie BK w miastach: lupy ").Append(_dTownMine).Append(", drewno ").Append(_dTownMineWood).Append(", bez drewna/kapitalu ").Append(_dTownMineSkip).Append(", cykl odrzucony przez gre ").Append(_dTownMineRej)
              .Append("; potkniecia dzis ").Append(_stumblesDay).Append(" (od wczytania ").Append(_stumbles).Append(") |");
            return sb.ToString();
        }

        /// <summary>Wstawka linii "Drewno:" w zuzyciu: drewno dymarek przy kopalniach (targ = ujscie zapasu; las okregu nie wchodzi do zapasu).</summary>
        internal static string WoodSegment(int fromShelves)
        {
            return ", dymarki przy kopalniach (214) " + fromShelves + " (las okregu poza zapasem " + _dWoods + ")";
        }

        /// <summary>Wstawka linii "Zelazo (lupy) (214)": przetop - zlom wraca w lupach (ArmsScrap, Drill przez WorkshopLaw.Needs).</summary>
        internal static string SmeltSegment(int scrap, int drill)
        {
            return "; przetop: zlom (174) " + scrap + ", zlom z cwiczen " + drill + ", wraki - metal napraw kowali (135), kuznia gracza - w \"bez wyjasnienia\"";
        }

        internal static void NewDay()
        {
            _dBlooms = 0; _dBurnt = 0; _dWoods = 0; _dBought = 0; _dGold = 0; _dShortShelf = 0; _dShortGold = 0; _dNoMarket = 0; _dTicked = 0; _dOver = 0;
            _dTownMine = 0; _dTownMineWood = 0; _dTownMineSkip = 0; _dTownMineRej = 0; _dUnwon = 0f; _stumblesDay = 0;
        }

        // ------------------------------------------------------------ wpiecie
        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var tb = AccessTools.TypeByName("TaleWorlds.CampaignSystem.CampaignBehaviors.VillageGoodProductionCampaignBehavior");
                var tick = tb != null ? AccessTools.Method(tb, "TickGoodProduction", new[] { typeof(Village), typeof(bool) }) : null;
                if (tick == null) { Log.Info("Bloomery (E6/R1, 214): BRAK VillageGoodProductionCampaignBehavior.TickGoodProduction(Village, bool) - wytop przy kopalni NIEAKTYWNY (ruda jak dotad)."); return; }
                h.Patch(tick, prefix: new HarmonyMethod(typeof(Bloomery), nameof(TickPrefix)) { priority = Priority.First },
                              postfix: new HarmonyMethod(typeof(Bloomery), nameof(TickPostfix)),
                              finalizer: new HarmonyMethod(typeof(Bloomery), nameof(TickFinalizer)));
                _wired = true;
                // recenzja R1: zakup drewna kopalni BK w miescie po cyklu, ktory dal lupy (bez tego wpiecia - zakup w prefiksie jak dotad)
                try
                {
                    var wsb = AccessTools.TypeByName("TaleWorlds.CampaignSystem.CampaignBehaviors.WorkshopsCampaignBehavior");
                    var cyc = wsb != null ? AccessTools.Method(wsb, "TickOneProductionCycleForNotableWorkshop") : null;
                    if (cyc != null) { h.Patch(cyc, postfix: new HarmonyMethod(typeof(Bloomery), nameof(CyclePostfix))); _cycleWired = true; }
                }
                catch (Exception e) { Log.Error("Bloomery.ApplyAll(cykl)", e); }
                Log.Info("Bloomery (E6/R1, 214): tick produkcji wsi wpiety (prefiks Priority.First przed BK, postfiks, finalizer) - lupa i dymarka tylko w nowej kampanii przy kluczu Bloomery At Mine.");
            }
            catch (Exception e) { Log.Error("Bloomery.ApplyAll", e); }
        }
    }
}
