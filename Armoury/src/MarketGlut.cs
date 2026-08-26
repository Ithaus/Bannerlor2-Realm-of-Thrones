using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// NASYCONY RYNEK, wedle slow Jeffa: pierwsza sprzedana sztuka idzie po
    /// pelnej cenie rynku (u zuzytego lupu to i tak ~5% wartosci), ale kazda
    /// KOLEJNA sztuka TEGO SAMEGO typu w tym samym miejscu schodzi taniej
    /// o 10% ceny pierwszej (5% -> 4.5% -> 4% -> ...), az do dna 20% ceny
    /// pierwszej - z 5% robi sie rowno 1%. Kupiec nie potrzebuje
    /// czterdziestu piatych wilczych lbow. Odkupienie sztuki oddaje miejsce
    /// na rynku, a targ trawi towar z czasem - licznik topnieje co dzien.
    /// </summary>
    internal static class MarketGlut
    {
        // miejsce (osada / karawana) -> typ przedmiotu -> ile sztuk juz wzial
        private static readonly Dictionary<string, Dictionary<int, float>> Sold =
            new Dictionary<string, Dictionary<int, float>>();

        private static bool Equipmentish(ItemObject it)
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
                    return false;   // towary handlowe (zboze, welna...) maja wlasny zywy rynek
            }
        }

        private static string PlaceKey(PartyBase merchant)
        {
            try
            {
                if (merchant == null) return null;
                if (merchant.Settlement != null) return merchant.Settlement.StringId;
                if (merchant.MobileParty != null) return "p_" + merchant.MobileParty.StringId;
            }
            catch { }
            return null;
        }

        private static float SoldCount(string place, int type)
        {
            Dictionary<int, float> d;
            if (!Sold.TryGetValue(place, out d)) return 0f;
            float n; d.TryGetValue(type, out n);
            return n;
        }

        /// <summary>
        /// REGULA JEFFA (ostateczna): 5% wartosci to PODLOGA pierwszej sztuki -
        /// jesli handel/perki daja wiecej (np. 8%), liczy sie wiecej. Kazda
        /// KOLEJNA sztuka tego typu w tym miejscu schodzi o 0.25 punktu
        /// procentowego OD TWOJEJ stawki, az do dna absolutnego 1% wartosci.
        /// Czyli: stawka_n = max(1%, max(stawka_rynku, 5%) - 0.25 x sprzedane).
        /// </summary>
        public static void PricePostfix(EquipmentElement __0, MobileParty __1, PartyBase __2, bool __3, ref int __result)
        {
            try
            {
                if (!__3 || __1 == null || __1 != MobileParty.MainParty) return;   // __3 = isSelling
                var item = __0.Item;
                var c = Settings.Current;
                if (c == null || !c.MarketGlutEnabled || !Equipmentish(item)) return;
                if (item.Value <= 0) return;
                var key = PlaceKey(__2);
                if (key == null) return;
                float sold = SoldCount(key, (int)item.ItemType);

                float baseRate = __result * 100f / item.Value;                  // co daje rynek (handel, perki, stan)
                float start = MathF.Max(baseRate, c.MarketGlutStartPercent);    // 5% to podloga, lepsza stawka stoi
                float min = MBMath.ClampFloat(c.MarketGlutMinPercent, 0.1f, 100f);
                float rate = start - MathF.Max(0f, c.MarketGlutDropPP) * sold;  // kazda kolejna sztuka -0.25 pp
                if (rate < min) rate = min;                                     // dno absolutne 1%

                int np = (int)(item.Value * rate / 100f);
                __result = np < 1 ? 1 : np;
            }
            catch { }
        }

        /// <summary>
        /// Postfix na InventoryLogic.TransferItem: liczymy sztuki FAKTYCZNIE
        /// sprzedane kupcowi (nie ruchy do zbrojowni DTE ani do skrytki -
        /// tam nie ma handlu). Odkupienie zdejmuje licznik.
        /// </summary>
        public static void TransferPostfix(InventoryLogic __instance, TransferCommand transferCommand)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.MarketGlutEnabled) return;
                if (__instance == null || !__instance.IsTrading) return;
                if (QuartermasterEscrow.Active) return;                    // ekran zbrojowni, nie targ
                var merchant = __instance.OtherParty;
                if (merchant == null) return;

                var item = transferCommand.ElementToTransfer.EquipmentElement.Item;
                if (!Equipmentish(item)) return;
                int amount = Math.Max(1, transferCommand.Amount);

                bool playerSells = transferCommand.FromSide == InventoryLogic.InventorySide.PlayerInventory
                                   && transferCommand.ToSide == InventoryLogic.InventorySide.OtherInventory;
                bool playerBuysBack = transferCommand.FromSide == InventoryLogic.InventorySide.OtherInventory
                                   && transferCommand.ToSide == InventoryLogic.InventorySide.PlayerInventory;
                if (!playerSells && !playerBuysBack) return;

                var key = PlaceKey(merchant);
                if (key == null) return;
                Dictionary<int, float> d;
                if (!Sold.TryGetValue(key, out d)) { d = new Dictionary<int, float>(); Sold[key] = d; }
                int t = (int)item.ItemType;
                float n; d.TryGetValue(t, out n);
                n += playerSells ? amount : -amount;
                if (n <= 0f) d.Remove(t); else d[t] = n;
            }
            catch (Exception e) { Log.Error("MarketGlut.Transfer", e); }
        }

        /// <summary>Rynek trawi: co dzien kazdy licznik topnieje o RecoverPerDay sztuk.</summary>
        internal static void DailyDigest()
        {
            try
            {
                var c = Settings.Current;
                float eat = c != null ? Math.Max(0f, c.MarketGlutRecoverPerDay) : 2f;
                if (eat <= 0f) return;
                var deadPlaces = new List<string>();
                foreach (var kv in Sold)
                {
                    var deadTypes = new List<int>();
                    var keys = new List<int>(kv.Value.Keys);
                    foreach (var t in keys)
                    {
                        float n = kv.Value[t] - eat;
                        if (n <= 0f) deadTypes.Add(t); else kv.Value[t] = n;
                    }
                    foreach (var t in deadTypes) kv.Value.Remove(t);
                    if (kv.Value.Count == 0) deadPlaces.Add(kv.Key);
                }
                foreach (var p in deadPlaces) Sold.Remove(p);
            }
            catch { }
        }

        // ---- zapis/odczyt: "miejsce;typ:ile,typ:ile|miejsce2;..." ----
        internal static string Export()
        {
            try
            {
                var parts = new List<string>();
                foreach (var kv in Sold)
                {
                    var items = new List<string>();
                    foreach (var tv in kv.Value)
                        items.Add(tv.Key + ":" + tv.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
                    if (items.Count > 0) parts.Add(kv.Key + ";" + string.Join(",", items.ToArray()));
                }
                return string.Join("|", parts.ToArray());
            }
            catch { return ""; }
        }

        internal static void Import(string data)
        {
            try
            {
                Sold.Clear();
                if (string.IsNullOrEmpty(data)) return;
                foreach (var part in data.Split('|'))
                {
                    var half = part.Split(';');
                    if (half.Length != 2) continue;
                    var d = new Dictionary<int, float>();
                    foreach (var pair in half[1].Split(','))
                    {
                        var tv = pair.Split(':');
                        if (tv.Length != 2) continue;
                        int t; float n;
                        if (int.TryParse(tv[0], out t)
                            && float.TryParse(tv[1], System.Globalization.NumberStyles.Float,
                                              System.Globalization.CultureInfo.InvariantCulture, out n) && n > 0f)
                            d[t] = n;
                    }
                    if (d.Count > 0) Sold[half[0]] = d;
                }
            }
            catch { }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var c = Settings.Current;
                if (c == null || !c.MarketGlutEnabled) { Log.Info("MarketGlut: wylaczony."); return; }

                // cena: kazdy model cen w grze (vanilla, BK, BetterEconomy...) dostaje nasz dopisek
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
                            var m = t.GetMethod("GetPrice",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            if (m == null || m.DeclaringType != t || seen.Contains(t)) continue;
                            seen.Add(t);
                            h.Patch(m, postfix: new HarmonyMethod(typeof(MarketGlut), "PricePostfix") { priority = Priority.Last });
                            patched++;
                        }
                        catch { }
                    }
                }

                var mt = AccessTools.Method(typeof(InventoryLogic), "TransferItem");
                if (mt != null)
                    h.Patch(mt, postfix: new HarmonyMethod(typeof(MarketGlut), "TransferPostfix"));

                Log.Info("MarketGlut: nasycony rynek czynny (" + patched + " modeli cen, licznik sprzedazy "
                         + (mt != null ? "wpiety" : "BRAK TransferItem!") + ").");
            }
            catch (Exception e) { Log.Error("MarketGlut.ApplyAll", e); }
        }
    }
}
