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
    /// Nasycony rynek (MarketGlut) zostaje tylko jako podloga 5% za zuzyty lup -
    /// jego licznik "kazda kolejna sztuka -0.25 pp" jest wylaczony, gdy to prawo dziala
    /// (inaczej kara za nadmiar liczylaby sie dwa razy).
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
            return Math.Max(0.1f, Math.Max(0f, c.SupplyDemandBase) * prosp * TierWeight[tier - 1]);
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

        /// <summary>Ile sztuk tego koszyka lezy na polce.</summary>
        internal static int Stock(ItemRoster shelf, ItemObject it)
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
            // sprzedaz gracza podnosi zapas (cena spada z kazda sztuka), zakup w tym samym ekranie go NIE obniza
            return frozen >= 0 ? Math.Max(frozen, n) : n;
        }

        internal static float Factor(Settlement st, ItemObject it, bool isSelling, out float d, out int s)
        {
            var c = Settings.Current;
            d = Demand(st, it) + Substitution(st, it);
            s = Stock(st.ItemRoster, it) + (isSelling ? 1 : 0);
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

        /// <summary>Postfix na GetPrice kazdego modelu cen - po MarketGlut (rejestrowany pozniej).</summary>
        public static void PricePostfix(EquipmentElement __0, MobileParty __1, PartyBase __2, bool __3, ref int __result)
        {
            if (_depth > 1) return;                                     // wewnetrzny model - zewnetrzny policzy
            try
            {
                if (!Active || __2 == null) return;
                var st = __2.Settlement;
                if (st == null || (!st.IsTown && !st.IsCastle)) return;
                if (QuartermasterEscrow.Active) return;                 // zbrojownia, nie targ
                var item = __0.Item;
                if (!Equipmentish(item)) return;
                float d; int s;
                float shelfF = Factor(st, item, __3, out d, out s);
                // PODSTAWA x SUROWCE (ArmsPricing): cena konkretnej sztuki z kosztu wykucia w granicach
                // bezpiecznika i koszt odtworzenia przy dzisiejszych cenach surowcow w okolicy
                float arms = ArmsPricing.Multiplier(st, item);
                float f = shelfF * arms;
                int before = __result;
                int np = (int)Math.Round(__result * f);
                __result = np < 1 ? 1 : np;

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
                                 + " (" + before + " -> " + __result + ", wartosc " + item.Value + ").");
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
                foreach (var kv in sample)
                {
                    int key = kv.Key; var probe = kv.Value;
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
                                int unit = Math.Max(1, (int)(it.Value * pricePct * srcFactor * ArmsPricing.Multiplier(src, it)));
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
                            // Priority.Last i rejestracja PO MarketGlut - biegniemy po nim (podloga 5% najpierw)
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
