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
    ///             (t1 1.0 ... t6 0.15 - na rycerski miecz jest mniej chetnych);
    ///   podaz S = sztuk tego koszyka na polce (przy sprzedazy + ta jedna sprzedawana);
    ///   mnoznik = ((D + 1) / (S + 1)) ^ elastycznosc, w granicach Min..Max.
    /// Pusta polka przy duzym popycie = drozej, zawalona polka = taniej - dla KUPNA
    /// i SPRZEDAZY, dla gracza i dla AI. Ekran handlu dziala na zywej polce miasta,
    /// wiec dziesiaty sprzedany miecz juz widzi dziewiec poprzednich.
    ///
    /// Rynek trawi: co dzien w kazdym miescie/zamku miejscowi i kupcy wykupuja
    /// DrainPercent nadwyzki ponad popyt (sztuki znikaja z polki - wywiezione,
    /// rozkupione) - inaczej raz zawalony rynek bylby zawalony na zawsze.
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
            var c = Settings.Current;
            float prosp = 1f;
            try
            {
                float p = st.Town != null ? st.Town.Prosperity : 0f;
                prosp = MBMath.ClampFloat(p / Math.Max(1f, c.SupplyDemandRefProsperity), 0.3f, 3f);
                if (st.IsCastle) prosp *= 0.5f;   // zamek to garnizon, nie targ
            }
            catch { }
            return Math.Max(0.1f, Math.Max(0f, c.SupplyDemandBase) * prosp * TierWeight[TierOf(it) - 1]);
        }

        /// <summary>Ile sztuk tego koszyka lezy na polce.</summary>
        internal static int Stock(ItemRoster shelf, ItemObject it)
        {
            int n = 0;
            if (shelf == null) return 0;
            for (int i = 0; i < shelf.Count; i++)
            {
                var el = shelf.GetElementCopyAtIndex(i);
                if (el.Amount > 0 && SameBucket(el.EquipmentElement.Item, it)) n += el.Amount;
            }
            return n;
        }

        internal static float Factor(Settlement st, ItemObject it, bool isSelling, out float d, out int s)
        {
            var c = Settings.Current;
            d = Demand(st, it);
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
                float f = Factor(st, item, __3, out d, out s);
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
                                 + " -> x" + f.ToString("0.00") + " (" + before + " -> " + __result + ", wartosc " + item.Value + ").");
                    }
                }
            }
            catch { }
        }

        /// <summary>Rynek trawi nadwyzke: co dzien DrainPercent nadwyzki ponad popyt znika z polek.</summary>
        internal static void DailyDrain()
        {
            try
            {
                if (!Active) return;
                var c = Settings.Current;
                float share = MBMath.ClampFloat(c.SupplyDemandDrainPercent, 0f, 100f) / 100f;
                if (share <= 0f) return;
                int removed = 0, places = 0;
                foreach (var st in Settlement.All)
                {
                    if (st == null || (!st.IsTown && !st.IsCastle) || st.ItemRoster == null) continue;
                    var shelf = st.ItemRoster;
                    // zlicz koszyki
                    var bucket = new Dictionary<int, int>();
                    var sample = new Dictionary<int, ItemObject>();
                    for (int i = 0; i < shelf.Count; i++)
                    {
                        var el = shelf.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (el.Amount <= 0 || !Equipmentish(it)) continue;
                        int k = (int)it.ItemType * 10 + TierOf(it);
                        int n; bucket.TryGetValue(k, out n); bucket[k] = n + el.Amount;
                        if (!sample.ContainsKey(k)) sample[k] = it;
                    }
                    bool any = false;
                    foreach (var kv in bucket)
                    {
                        float d = Demand(st, sample[kv.Key]);
                        float surplus = kv.Value - d;
                        if (surplus <= 0f) continue;
                        int take = (int)Math.Ceiling(surplus * share);
                        // zdejmujemy od konca polki (najswiezsze wpisy), sztuka po sztuce
                        for (int i = shelf.Count - 1; i >= 0 && take > 0; i--)
                        {
                            var el = shelf.GetElementCopyAtIndex(i);
                            var it = el.EquipmentElement.Item;
                            if (el.Amount <= 0 || it == null || (int)it.ItemType * 10 + TierOf(it) != kv.Key) continue;
                            int t = Math.Min(take, el.Amount);
                            shelf.AddToCounts(el.EquipmentElement, -t);
                            take -= t; removed += t; any = true;
                        }
                    }
                    if (any) places++;
                }
                if (removed > 0) Log.Info("PodazPopyt: rynek strawil " + removed + " szt. nadwyzki uzbrojenia w " + places + " osadach.");
            }
            catch (Exception e) { Log.Error("SupplyDemand.DailyDrain", e); }
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
