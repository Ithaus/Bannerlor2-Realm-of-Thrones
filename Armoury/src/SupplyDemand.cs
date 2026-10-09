using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// PRAWO PODAZY I POPYTU dla uzbrojenia (Jeff 04.10: "ceny maja zostac jak sa,
    /// trzeba zmienic prawo podazy i popytu - jesli wykuje 10 mieczy tieru 6, to
    /// nagle beda mniej warte, bo jest ich wiecej, niz jest zapotrzebowanie").
    ///
    /// Cena bazowa = prawdziwa wartosc przedmiotu (Value) - nietknieta. Vanilla ma
    /// podaz/popyt, ale dla wszystkiego, co nie jest towarem handlowym, zaciska mnoznik
    /// do 0.8-1.3 (DefaultTradeItemPriceFactorModel.GetBasePriceFactor - sprawdzone
    /// w 1.4.8), wiec dziesiec mieczy na polce prawie nie rusza ceny.
    ///
    /// Nasza regula, w miescie i zamku, dla kazdego "koszyka" = typ przedmiotu x tier:
    ///   popyt D = PopytBazowy x (zamoznosc / zamoznosc wzorcowa, 0.3-3) x waga tieru
    ///             (t1 1.0 ... t6 0.15 - na rycerski miecz jest mniej chetnych)
    ///   podaz S = sztuk tego koszyka na polce (przy sprzedazy + ta jedna sprzedawana);
    ///   mnoznik = ((D + 1) / (S + 1)) ^ elastycznosc, w granicach Min..Max.
    /// Pusta polka przy duzym popycie = drozej, zawalona polka = taniej - dla KUPNA
    /// i SPRZEDAZY, dla gracza i dla AI. Ekran handlu dziala na zywej polce miasta,
    /// wiec dziesiaty sprzedany miecz juz widzi dziewiec poprzednich.
    ///
    /// Towar jak kazdy: co dzien kupcy wywoza TradePercent nadwyzki z zawalonych polek
    /// do najblizszej osady, ktorej brakuje (patrz DailyTrade) - nic nie znika w prozni,
    /// a popyt wojenny bierze sie z PRAWDZIWYCH zakupow armii (osobna zmiana), nie z mnoznika.
    ///
    /// Nasycony rynek (MarketGlut) milczy, gdy to prawo dziala (kara za nadmiar liczylaby
    /// sie dwa razy). Od 07.10 nie stawia tez swojej podlogi 5% PRZED nami (pokretla Jeffa:
    /// "najnizsza cena tak, ale zalezna od podazy i popytu za rupiecie") - jedyna podloga
    /// to MinSellPercentOfValue, liczona na koncu, po mnozniku polki, i nie wyzsza niz cena, jakiej ta polka
    /// zada za te sama sztuke (recenzja: bez petli "kup rupiec tanio, sprzedaj za podloge"). Wylacznik OneScrapFloor.
    /// </summary>
    internal static class SupplyDemand
    {
        private static readonly float[] TierWeight = { 1.0f, 0.9f, 0.7f, 0.5f, 0.3f, 0.15f };
        private static readonly Dictionary<string, int> _loggedHour = new Dictionary<string, int>();

        internal static bool Active
        {
            get { var c = Settings.Current; return c != null && c.SupplyDemandEnabled; }
        }

        internal static bool Equipmentish(ItemObject it)
        {
            if (it == null) return false;
            switch (it.ItemType)
            {
                case ItemObject.ItemTypeEnum.HeadArmor:
                case ItemObject.ItemTypeEnum.BodyArmor:
                case ItemObject.ItemTypeEnum.LegArmor:
                case ItemObject.ItemTypeEnum.HandArmor:
                case ItemObject.ItemTypeEnum.Cape:
                case ItemObject.ItemTypeEnum.OneHandedWeapon:
                case ItemObject.ItemTypeEnum.TwoHandedWeapon:
                case ItemObject.ItemTypeEnum.Polearm:
                case ItemObject.ItemTypeEnum.Shield:
                case ItemObject.ItemTypeEnum.Bow:
                case ItemObject.ItemTypeEnum.Crossbow:
                case ItemObject.ItemTypeEnum.Arrows:
                case ItemObject.ItemTypeEnum.Bolts:
                case ItemObject.ItemTypeEnum.Thrown:
                case ItemObject.ItemTypeEnum.Horse:
                case ItemObject.ItemTypeEnum.HorseHarness:
                    return true;
                default:
                    return false;   // towary handlowe maja wlasny, zywy rynek vanilla (0.1-10)
            }
        }

        private static int TierOf(ItemObject it)
        {
            try { return Math.Max(1, Math.Min(6, (int)it.Tier + 1)); } catch { return 1; }
        }

        private static bool SameBucket(ItemObject a, ItemObject b)
        {
            return a != null && b != null && a.ItemType == b.ItemType && TierOf(a) == TierOf(b);
        }

        /// <summary>Popyt miasta na jeden koszyk (typ x tier).</summary>
        // ------------------------------------------------------------ zamowienia (wpis 67)
        // Jeff 04.10: "skoro czegos brakuje, to powinno sie najbardziej oplacac - nie dziala logika podazy i popytu". Popyt byl
        // STALY (dobrobyt x 4 sztuki na rodzaj); ochotnik bez zbroi i lord z brakami odchodzili z niczym, a rynek tego nie
        // widzial. Teraz kazda nieudana proba zakupu zapisuje sie w miescie jako zamowienie na rodzaj i tier, podnosi popyt
        // (a wiec cene i oplacalnosc dla warsztatow) i codziennie wygasa (SupplyDemandOrderDecay).
        private static readonly Dictionary<string, float> _unmet = new Dictionary<string, float>();
        private static int _noted;
        private static string Key(Settlement st, ItemObject.ItemTypeEnum type, int tier) { return (st != null ? st.StringId : "-") + "|" + (int)type + "|" + tier; }

        internal static void NoteUnmet(Settlement market, ItemObject.ItemTypeEnum type, int tier, float n)
        {
            if (market == null || n <= 0f) return;
            tier = Math.Max(1, Math.Min(6, tier));
            string k = Key(market, type, tier);
            float v; _unmet.TryGetValue(k, out v);
            _unmet[k] = Math.Min(v + n, Math.Max(1f, Settings.Current.SupplyDemandOrderCap));
            _noted++;
        }

        internal static void DecayOrders()
        {
            float keep = MBMath.ClampFloat(1f - Settings.Current.SupplyDemandOrderDecay, 0f, 1f);
            var keys = new List<string>(_unmet.Keys);
            float total = 0f;
            foreach (var k in keys) { float v = _unmet[k] * keep; if (v < 0.2f) _unmet.Remove(k); else { _unmet[k] = v; total += v; } }
            if (_noted > 0 || total > 0f) Log.Info("PodazPopyt: zamowienia - nowych dzis " + _noted + ", otwartych " + _unmet.Count + " (razem " + (int)total + " szt. czeka na towar).");
            _noted = 0;
        }

        internal static void ResetOrders() { _unmet.Clear(); _noted = 0; _onceSeen.Clear(); _loggedHour.Clear(); }

        // wpis 81 (audyt 05.10, pkt 1 - petla drozenia): ten sam niezaspokojony kupiec (garnizon co dzien, notabl z cofnietym
        // awansem co dzien) wpisywal to samo zamowienie od nowa - przy wygaszaniu 15% stan rosl do ~6.7x dziennego wpisu,
        // do sufitu 60, a cena polki szla na x4. Teraz jeden kupiec zamawia dany rodzaj (miasto x typ x tier) raz na
        // SupplyDemandOrderRepeatDays dni - zamowienie to potrzeba, nie licznik prob.
        private static readonly Dictionary<string, int> _onceSeen = new Dictionary<string, int>();
        internal static void NoteUnmetOnce(object who, Settlement market, ItemObject.ItemTypeEnum type, int tier, float n)
        {
            if (who == null || market == null || n <= 0f) return;
            int day = (int)CampaignTime.Now.ToDays, last;
            string k = who.GetHashCode() + "|" + Key(market, type, Math.Max(1, Math.Min(6, tier)));
            if (_onceSeen.TryGetValue(k, out last) && day - last < Math.Max(1, Settings.Current.SupplyDemandOrderRepeatDays)) return;
            _onceSeen[k] = day;
            if (_onceSeen.Count > 20000) _onceSeen.Clear();
            NoteUnmet(market, type, tier, n);
            if (type == ItemObject.ItemTypeEnum.Arrows || type == ItemObject.ItemTypeEnum.Bolts) TownFletchers.NoteUnmet(type);   // 172: "AI bez towaru" (tylko licznik)
        }

        internal static float Demand(Settlement st, ItemObject it)
        {
            return Demand(st, it.ItemType, TierOf(it));
        }

        /// <summary>Popyt na koszyk: zamoznosc x waga tieru x (1 + premia oczekiwan wojennych frakcji).</summary>
        internal static float Demand(Settlement st, ItemObject.ItemTypeEnum type, int tier)
        {
            var c = Settings.Current;
            float prosp = 1f;
            try
            {
                float p = st.Town != null ? st.Town.Prosperity : 0f;
                prosp = MBMath.ClampFloat(p / Math.Max(1f, c.SupplyDemandRefProsperity), 0.3f, 3f);
                if (st.IsCastle) prosp *= 0.5f;   // zamek to garnizon, nie targ
                // OCZEKIWANIA WOJENNE (ArmsPricing): kupcy doliczaja zakupy, ktorych sie spodziewaja
                if (type != ItemObject.ItemTypeEnum.Horse) prosp *= 1f + ArmsPricing.WarPremium(st.MapFaction);
            }
            catch { }
            tier = Math.Max(1, Math.Min(6, tier));
            float orders = 0f;
            try { var m = st != null && st.IsVillage && st.Village != null && st.Village.Bound != null ? st.Village.Bound : st; _unmet.TryGetValue(Key(m, type, tier), out orders); } catch { }
            return Math.Max(0.1f, Math.Max(0f, c.SupplyDemandBase) * prosp * TierWeight[tier - 1] + orders * Math.Max(0f, c.SupplyDemandOrderWeight));
        }

        /// <summary>SUBSTYTUCJA (Jeff 04.10: "wojsko patrzy, jaki jest najlepszy pancerz do ceny"):
        /// gdy na polce nie ma ani jednej sztuki tieru wyzej, czesc tamtego popytu przechodzi
        /// na ten tier - kupujacy biora gorsze, ale dostepne.</summary>
        private static float Substitution(Settlement st, ItemObject it)
        {
            try
            {
                var c = Settings.Current;
                if (!c.SubstitutionEnabled) return 0f;
                int t = TierOf(it);
                if (t >= 6) return 0f;
                int higher = 0;
                var shelf = st.ItemRoster;
                var view = ShelfView(shelf);
                if (view != null) { foreach (var kv in view) if (kv.Key.ItemType == it.ItemType && TierOf(kv.Key) == t + 1) { higher += kv.Value; break; } }
                else
                for (int i = 0; i < shelf.Count; i++)
                {
                    var el = shelf.GetElementCopyAtIndex(i);
                    var x = el.EquipmentElement.Item;
                    if (el.Amount > 0 && x != null && x.ItemType == it.ItemType && TierOf(x) == t + 1) { higher += el.Amount; break; }
                }
                if (higher > 0) return 0f;
                return MBMath.ClampFloat(c.SubstitutionShare, 0f, 1f) * Demand(st, it.ItemType, t + 1);
            }
            catch { return 0f; }
        }

        // ZAMROZONA POLKA (audyt dziur B1, Jeff 04.10 "napraw"): w jednym ekranie handlu gracz mogl
        // wykupic polke "na kredyt" (zloto liczone dopiero przy Done), sprzedac swoje po cenie pustej
        // polki i odkupic towar za te sama cene - rozrzut x0.25..x2. Gdy ekran handlu jest otwarty,
        // stan polki bierzemy z chwili jego otwarcia; po zamknieciu wraca zywy stan.
        private static readonly Dictionary<ItemRoster, List<KeyValuePair<ItemObject, int>>> _frozen = new Dictionary<ItemRoster, List<KeyValuePair<ItemObject, int>>>();

        private static bool TradeScreenOpen()
        {
            try
            {
                var top = TaleWorlds.ScreenSystem.ScreenManager.TopScreen;
                return top != null && top.GetType().Name.IndexOf("Inventory", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
        }

        private static List<KeyValuePair<ItemObject, int>> ShelfView(ItemRoster shelf)
        {
            if (shelf == null) return null;
            if (!TradeScreenOpen()) { if (_frozen.Count > 0) _frozen.Clear(); return null; }
            List<KeyValuePair<ItemObject, int>> snap;
            if (_frozen.TryGetValue(shelf, out snap)) return snap;
            snap = new List<KeyValuePair<ItemObject, int>>();
            for (int i = 0; i < shelf.Count; i++)
            {
                var el = shelf.GetElementCopyAtIndex(i);
                if (el.Amount > 0 && el.EquipmentElement.Item != null) snap.Add(new KeyValuePair<ItemObject, int>(el.EquipmentElement.Item, el.Amount));
            }
            _frozen[shelf] = snap;
            return snap;
        }

        // paczka 145 (wycena zamowienia kowala bez ruszania polki): na czas jednego wywolania modelu ceny Stock odejmuje sztuki,
        // ktore zamowienie juz "zdjelo" z koszyka tej polki - cena kolejnej sztuki jak po prawdziwym zdjeciu, a polka nietknieta.
        // Poza takim wywolaniem (Hold ... Release) - zero, nic sie nie zmienia.
        [ThreadStatic] private static ItemRoster _heldShelf;
        [ThreadStatic] private static ItemObject _heldBucket;
        [ThreadStatic] private static int _held;

        internal static void Hold(ItemRoster shelf, ItemObject bucketOf, int taken)
        {
            _heldShelf = taken > 0 ? shelf : null; _heldBucket = bucketOf; _held = Math.Max(0, taken);
        }

        internal static void Release() { _heldShelf = null; _heldBucket = null; _held = 0; }

        /// <summary>Ile sztuk tego koszyka lezy na polce.</summary>
        internal static int Stock(ItemRoster shelf, ItemObject it, int dir = 0)
        {
            int n = 0;
            if (shelf == null) return 0;
            var view = ShelfView(shelf);
            int frozen = -1;
            if (view != null) { frozen = 0; foreach (var kv in view) if (SameBucket(kv.Key, it)) frozen += kv.Value; }
            for (int i = 0; i < shelf.Count; i++)
            {
                var el = shelf.GetElementCopyAtIndex(i);
                if (el.Amount > 0 && SameBucket(el.EquipmentElement.Item, it)) n += el.Amount;
            }
            if (_heldShelf != null && ReferenceEquals(shelf, _heldShelf) && SameBucket(_heldBucket, it)) n = Math.Max(0, n - _held);   // paczka 145: wycena
            // Audyt ponowny K1: wlasne transakcje gracza w ekranie handlu tylko mu szkodza:
            // sprzedaz (dir +1) - wiekszy zapas (cena spada z kazda sprzedana sztuka, wlasny wykup jej nie podnosi);
            // kupno (dir -1) - mniejszy zapas (cena rosnie z kazda kupiona sztuka, wlasna sprzedaz jej nie obniza).
            if (frozen < 0 || dir == 0) return n;
            return dir > 0 ? Math.Max(frozen, n) : Math.Min(frozen, n);
        }

        internal static float Factor(Settlement st, ItemObject it, bool isSelling, out float d, out int s)
        {
            var c = Settings.Current;
            d = Demand(st, it) + Substitution(st, it);
            s = Stock(st.ItemRoster, it, isSelling ? 1 : -1) + (isSelling ? 1 : 0);
            float f = (float)Math.Pow((d + 1f) / (s + 1f), MBMath.ClampFloat(c.SupplyDemandElasticity, 0.05f, 2f));
            float lo = MBMath.ClampFloat(c.SupplyDemandMinFactor, 0.01f, 1f);
            float hi = Math.Max(1f, c.SupplyDemandMaxFactor);
            return MBMath.ClampFloat(f, lo, hi);
        }

        // ZAGNIEZDZENIE: BEE_ItemPriceFactorModel.GetPrice w srodku wola
        // DefaultTradeItemPriceFactorModel.GetPrice, a latka siedzi na obu - mnoznik
        // liczymy tylko raz, w najbardziej zewnetrznym wywolaniu
        [ThreadStatic] private static int _depth;

        public static void PricePrefix() { _depth++; }
        // finalizer biegnie ZAWSZE (takze po wyjatku) - licznik nie zostanie zawyzony na stale
        public static Exception PriceFinalizer(Exception __exception) { if (_depth > 0) _depth--; return __exception; }

        /// <summary>
        /// Czy te transakcje wycenia prawo podazy (miasto/zamek, sprzet, nie zbrojownia) - wtedy podloge zlomu
        /// stawiamy MY, PO mnozniku polki, a ScrapFloor nie stawia drugiej przed nami (pokretla Jeffa 07.10).
        /// </summary>
        internal static bool Prices(PartyBase merchant, ItemObject item)
        {
            try
            {
                if (!Active || merchant == null) return false;
                var st = merchant.Settlement;
                if (st == null || (!st.IsTown && !st.IsCastle)) return false;
                if (QuartermasterEscrow.Active) return false;           // zbrojownia, nie targ
                return Equipmentish(item);
            }
            catch { return false; }
        }

        /// <summary>Postfix na GetPrice kazdego modelu cen - po MarketGlut (rejestrowany pozniej).</summary>
        public static void PricePostfix(TradeItemPriceFactorModel __instance, EquipmentElement __0, MobileParty __1, PartyBase __2, bool __3, float __4, float __5, float __6, ref int __result)
        {
            if (_depth > 1) return;                                     // wewnetrzny model - zewnetrzny policzy
            try
            {
                var item = __0.Item;
                if (!Prices(__2, item)) { SellByCondition.Seen(__0, __3, __result, -1); return; }   // ksiega skupu (tylko log): sprzedaz poza prawem podazy
                var st = __2.Settlement;
                float d; int s;
                float shelfF = Factor(st, item, __3, out d, out s);
                // PODSTAWA x SUROWCE (ArmsPricing): cena konkretnej sztuki z kosztu wykucia w granicach
                // bezpiecznika i koszt odtworzenia przy dzisiejszych cenach surowcow w okolicy
                float arms = ArmsPricing.Multiplier(st, item);
                float f = shelfF * arms;
                int before = __result;
                // wpis 70 (Jeff 05.10: "czy ceny w sklepie sa odpowiednie?"): przy kupnie lancuch modeli cen (gra + cudze mody,
                // najpewniej AIInfluence) dawal juz ~2x wartosci PRZED nasza warstwa (log: karstark_boots 145 -> 211 przy wartosci 73).
                // Cena kupna sprzetu = wartosc sztuki (ze stanem) x nasz mnoznik x marza kupca - bez cudzych narzutow.
                float baseP = __result;
                if (!__3 && HistoricalPrices.On && Settings.Current.RetailFromWorth)
                    baseP = Math.Max(1, __0.ItemValue) * (1f + Math.Max(0f, Settings.Current.RetailMarkupPercent) / 100f);
                // CENA SPRZEDAZY OD STANU (B2, 07.10): gdy gra dala swoje minimum 1 zl, bierzemy prawdziwy ulamek (wartosc ze stanem x mnoznik
                // ceny aktywnego modelu) i dopiero go mnozymy przez polke - "1 zl x polka" placilo za wrak tarczy wartej 2 zl 4-6 zl. Wylacznik SellPriceByCondition.
                bool byCond = __3 && SellByCondition.On;
                if (byCond && before <= 1) baseP = SellByCondition.Fraction(__instance, __0, __1, __2, __4, __5, __6);
                int np = (int)Math.Round(baseP * f);
                __result = np < 1 ? 1 : np;
                int floorP = 0, capP = 0, bound = 0;
                // wpis 88 (audyt pkt 10): podloga zlomu (MinSellPercentOfValue) liczyla sie PRZED nami - mnoznik polki 0.25 sciagal ja do 1.25%
                if (__3 && Settings.Current.MinSellPercentOfValue > 0 && item.Value > 0)
                {
                    // B1 (07.10): podloga od WARTOSCI ZE STANEM - wrak (x0.1) ma podloge 0.2% wartosci nowej, legenda 10%; wylacznik: od czystej, jak w 127
                    int floor = Math.Max(1, (int)((byCond ? __0.ItemValue : item.Value) * Settings.Current.MinSellPercentOfValue / 100f));
                    // POKRETLA JEFFA 07.10 (recenzja; "zero darmowej kasy"): podloga nie placi wiecej, niz TA polka zada za TE
                    // sama sztuke (wartosc ze stanem x marza x ten sam mnoznik polki i surowcow, polka juz z ta sztuka) - inaczej
                    // "zmasakrowany" rupiec (cena x0.03) kupiony z zawalonej polki za ~0.8% wartosci szedl z powrotem za 2%
                    // (a przy dawnej podlodze 5% takze rupiecie x0.08 / x0.1). Wylacznik OneScrapFloor = false: jak w 126.
                    if (Settings.Current.OneScrapFloor && HistoricalPrices.On && Settings.Current.RetailFromWorth)
                    {
                        int ask = (int)Math.Round(Math.Max(1, __0.ItemValue) * (1f + Math.Max(0f, Settings.Current.RetailMarkupPercent) / 100f) * f);
                        if (ask < floor) floor = Math.Max(1, ask);
                    }
                    floorP = floor;
                    if (__result < floor) { __result = floor; bound = 1; }
                }
                // B3 (decyzja Jeffa 07.10 "tak"): kupiec nie da za sztuke wiecej niz SellCapPercentOfNewAsk (10%) ceny, jaka TA polka zada za NOWA
                // sztuke tej jakosci - czyli nie wiecej, niz sam bierze za jej wrak; sufit wygrywa z podloga
                if (byCond) { capP = SellByCondition.Cap(__0, f); if (capP > 0 && __result > capP) { __result = capP; bound = 2; } }
                SellByCondition.Seen(__0, __3, __result, bound);   // ksiega skupu (tylko log)

                // log: handel gracza, raz na godzine gry na koszyk i miejsce
                if (__1 == MobileParty.MainParty)
                {
                    string key = st.StringId + "|" + item.ItemType + "|" + TierOf(item) + "|" + (__3 ? "s" : "b");
                    int hour = (int)CampaignTime.Now.ToHours;
                    int last;
                    if (!_loggedHour.TryGetValue(key, out last) || last != hour)
                    {
                        _loggedHour[key] = hour;
                        Log.Info("PodazPopyt: " + st.Name + " " + item.ItemType + " t" + TierOf(item)
                                 + " (" + item.StringId + ") " + (__3 ? "SPRZEDAZ" : "KUPNO")
                                 + ": na polce " + s + ", popyt " + d.ToString("0.0")
                                 + " -> polka x" + shelfF.ToString("0.00") + ", podstawa+surowce x" + arms.ToString("0.00")
                                 + " (podstawa " + (int)ArmsPricing.BaseOf(item) + ", surowce x" + ArmsPricing.MaterialIndex(st, item).ToString("0.00")
                                 + (ArmsPricing.IsUnique(item) ? ", UNIKAT" : "") + ") = x" + f.ToString("0.00")
                                 + " (" + before + " -> " + __result + ", wartosc " + item.Value
                                 + (__3 ? SellByCondition.Bounds(__0, floorP, capP, bound) : "") + ").");
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// WYWOZ NADWYZKI (Jeff 04.10: "jak kupuja, to gdzie potem sprzedaja - nie znika
        /// 15% w prozni"). Nic nie znika: co dzien kupcy zabieraja TradePercent nadwyzki
        /// koszyka ponad popyt z zawalonej polki i wioza ja do NAJBLIZSZEJ osady (w zasiegu
        /// TradeRange), ktorej w tym koszyku brakuje. Osada docelowa PLACI osadzie zrodlowej
        /// (TradePricePercent wartosci x mnoznik zawalonego rynku zrodla - hurt z nadmiaru
        /// jest tani); bez zlota nie kupuje. Gdy nikt w zasiegu nie potrzebuje - towar zostaje
        /// na polce i cena zostaje niska, az ktos kupi.
        /// </summary>
        internal static void DailyTrade()
        {
            try
            {
                if (!Active) return;
                var c = Settings.Current;
                float share = MBMath.ClampFloat(c.SupplyDemandTradePercent, 0f, 100f) / 100f;
                if (share <= 0f) return;
                float range = Math.Max(1f, c.SupplyDemandTradeRange);
                float pricePct = MBMath.ClampFloat(c.SupplyDemandTradePricePercent, 0f, 200f) / 100f;

                // stan: osada -> koszyk -> sztuk; probka przedmiotu koszyka do liczenia popytu
                var places = new List<Settlement>();
                var stock = new Dictionary<Settlement, Dictionary<int, int>>();
                var sample = new Dictionary<int, ItemObject>();
                foreach (var st in Settlement.All)
                {
                    if (st == null || (!st.IsTown && !st.IsCastle) || st.ItemRoster == null || st.Town == null) continue;
                    places.Add(st);
                    var b = new Dictionary<int, int>();
                    var shelf = st.ItemRoster;
                    for (int i = 0; i < shelf.Count; i++)
                    {
                        var el = shelf.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (el.Amount <= 0 || !Equipmentish(it)) continue;
                        int k = (int)it.ItemType * 10 + TierOf(it);
                        int n; b.TryGetValue(k, out n); b[k] = n + el.Amount;
                        if (!sample.ContainsKey(k)) sample[k] = it;
                    }
                    stock[st] = b;
                }

                int moved = 0, deals = 0, stuck = 0; long paid = 0;
                // 171 C8 (Z6): zamek nie jest targiem broni - kupcy nie wioza tam broni natychmiast, bez drogi; zaloga zamku zamawia w miescie
                // (GarrisonCarts). Zamek zostaje ZRODLEM: jego zapas ponad popyt kupcy wywoza do miast
                bool noCastles = GarrisonCarts.On;
                foreach (var kv in sample)
                {
                    int key = kv.Key; var probe = kv.Value;
                    // recenzja 171: tylko bron i zbroje - konie i rzedy kupcy woza do zamkow jak dotad (zaloga ich nie zamawia, gracz kupuje je na polce zamku)
                    bool noCastlesHere = noCastles && !MenPurse.HorseKind(probe);
                    foreach (var src in places)
                    {
                        int have; stock[src].TryGetValue(key, out have);
                        if (have <= 0) continue;
                        float surplus = have - Demand(src, probe);
                        if (surplus <= 0f) continue;
                        int toShip = (int)Math.Ceiling(surplus * share);
                        float srcFactor = (float)Math.Pow((Demand(src, probe) + 1f) / (have + 1f), MBMath.ClampFloat(c.SupplyDemandElasticity, 0.05f, 2f));
                        srcFactor = MBMath.ClampFloat(srcFactor, MBMath.ClampFloat(c.SupplyDemandMinFactor, 0.01f, 1f), 1f);
                        var srcPos = src.GetPosition2D;
                        var poor = new HashSet<Settlement>();   // odbiorcy bez zlota - do konca tego przebiegu
                        while (toShip > 0)
                        {
                            // ARBITRAZ (docs/MODEL-MATERIALOW.md): kupiec wiezie tam, gdzie zarobi najwiecej -
                            // cena w celu (polka po dostawie x podstawa x surowce) minus cena u zrodla minus
                            // koszt drogi (TradeTransportPercentPer100 wartosci na 100 jednostek mapy);
                            // brak w celu to nie warunek, tylko powod, dla ktorego tam drozej
                            Settlement best = null; float bestProfit = 0f; int bestCap = 0;
                            float elast = MBMath.ClampFloat(c.SupplyDemandElasticity, 0.05f, 2f);
                            float srcIdx = srcFactor * ArmsPricing.Multiplier(src, probe);
                            float perDist = Math.Max(0f, c.TradeTransportPercentPer100) / 100f / 100f;
                            foreach (var dst in places)
                            {
                                if (dst == src || poor.Contains(dst)) continue;
                                if (noCastlesHere && dst.IsCastle) continue;
                                float dist = srcPos.Distance(dst.GetPosition2D);
                                if (dist > range) continue;
                                int dh; stock[dst].TryGetValue(key, out dh);
                                float dd = Demand(dst, probe);
                                float dstShelf = MBMath.ClampFloat((float)Math.Pow((dd + 1f) / (dh + 2f), elast),
                                                                   MBMath.ClampFloat(c.SupplyDemandMinFactor, 0.01f, 1f), Math.Max(1f, c.SupplyDemandMaxFactor));
                                float profit = dstShelf * ArmsPricing.Multiplier(dst, probe) - srcIdx - perDist * dist;
                                if (profit <= bestProfit) continue;
                                best = dst; bestProfit = profit; bestCap = Math.Max(1, (int)Math.Ceiling(dd) - dh);
                            }
                            if (best == null) { stuck += toShip; break; }
                            // przenosimy sztuki koszyka ze zrodla (od konca polki), placi odbiorca
                            int want = Math.Min(toShip, bestCap);
                            int got = 0;
                            var shelf = src.ItemRoster;
                            for (int i = shelf.Count - 1; i >= 0 && got < want; i--)
                            {
                                var el = shelf.GetElementCopyAtIndex(i);
                                var it = el.EquipmentElement.Item;
                                if (el.Amount <= 0 || it == null || (int)it.ItemType * 10 + TierOf(it) != key) continue;
                                // B4 (07.10): hurt od wartosci ZE STANEM - odbiorca nie placi za wrak jak za czysta sztuke (wylacznik SellPriceByCondition: jak w 127)
                                int worth = SellByCondition.On ? el.EquipmentElement.ItemValue : it.Value;
                                int unit = Math.Max(1, (int)(worth * pricePct * srcFactor * ArmsPricing.Multiplier(src, it)));
                                int n = Math.Min(want - got, el.Amount);
                                int afford = best.Town.Gold / unit;
                                if (afford <= 0) break;
                                if (n > afford) n = afford;
                                shelf.AddToCounts(el.EquipmentElement, -n);
                                best.ItemRoster.AddToCounts(el.EquipmentElement, n);
                                best.Town.ChangeGold(-unit * n);
                                src.Town.ChangeGold(unit * n);
                                got += n; paid += (long)unit * n;
                            }
                            if (got <= 0) { poor.Add(best); continue; }   // biedny odbiorca - pomijamy go dzis
                            int sh; stock[src].TryGetValue(key, out sh); stock[src][key] = sh - got;
                            int dh2; stock[best].TryGetValue(key, out dh2); stock[best][key] = dh2 + got;
                            toShip -= got; moved += got; deals++;
                        }
                    }
                }
                if (moved > 0 || stuck > 0)
                    Log.Info("PodazPopyt: kupcy wywiezli " + moved + " szt. nadwyzki uzbrojenia w " + deals + " transakcjach miedzy osadami (zaplacone "
                             + paid + " zlota); " + stuck + " szt. bez odbiorcy w zasiegu " + (int)range + " - zostaja na polkach.");
            }
            catch (Exception e) { Log.Error("SupplyDemand.DailyTrade", e); }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                int patched = 0;
                var seen = new HashSet<Type>();
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); } catch { continue; }
                    foreach (var t in types)
                    {
                        try
                        {
                            if (t == null || t.IsAbstract || !typeof(TradeItemPriceFactorModel).IsAssignableFrom(t)) continue;
                            var m = t.GetMethod("GetPrice", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            if (m == null || m.DeclaringType != t || seen.Contains(t)) continue;
                            seen.Add(t);
                            // Priority.Last i rejestracja PO MarketGlut - biegniemy po nim (MarketGlut milczy, gdy dzialamy)
                            h.Patch(m, prefix: new HarmonyMethod(typeof(SupplyDemand), "PricePrefix") { priority = Priority.First },
                                       postfix: new HarmonyMethod(typeof(SupplyDemand), "PricePostfix") { priority = Priority.Last },
                                       finalizer: new HarmonyMethod(typeof(SupplyDemand), "PriceFinalizer"));
                            patched++;
                        }
                        catch { }
                    }
                }
                Log.Info("PodazPopyt: prawo podazy i popytu dla uzbrojenia " + (Active ? "CZYNNE" : "wylaczone w MCM")
                         + " (" + patched + " modeli cen).");
            }
            catch (Exception e) { Log.Error("SupplyDemand.ApplyAll", e); }
        }
    }
}
