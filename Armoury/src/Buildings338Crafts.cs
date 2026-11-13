using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// 476-b (Jeff 13.11: "Craftmans - daj rzemieslnicy"; audyt 12.11: Craftmans Quarters zamku MARTWY - efekt DenarByBoundVillageHeartPerDay czyta tylko podatek gry,
    /// ktory zastepuje PopulationLaw.TownTaxPostfix) - RZEMIESLNICY ZAMKU.
    /// Co dobe zamek z Craftmans Quarters (poziom L &gt; 0): rzemieslnicy podzamcza przerabiaja surowiec okolicy na towar - welna -&gt; sukno (cloth), len -&gt; plotno
    /// (linen), skory -&gt; skora (leather), drewno -&gt; deski (planks), zelazo -&gt; narzedzia (tools); 1 sztuka surowca = 1 sztuka towaru, najwyzej 2 x L sztuk
    /// na dobe (poziom = wiecej rzemieslnikow). Surowiec tylko taki, jaki daja wsie okolicy zamku (Buildings338Works - mapa zasobow).
    /// Obieg (nic z niczego): surowiec z polki miasta handlowego okregu (TradeBound wsi zamku - jak wozy wsi i ruda kopalni zamku) - kasa zamku placi miastu cene
    /// kupna; towar na te sama polke - miasto placi zamkowi cene sprzedazy. Marza (sprzedaz - kupno, tylko gdy dodatnia): 60% place rzemieslnikow (TownFolk.Earn
    /// KCraftPaid - kiesa podzamcza; czego kiesa nie przyjmie - zostaje w kasie zamku), reszta - podatek pana (kasa zamku). Ramka ksiegi towarow FTownCraft.
    /// Brak surowca na polce, kasy zamku na zakup albo kasy miasta na towar - nic sie nie dzieje (licznik). Klucz Buildings338 Castle Craftsmen (TAK); NIE - jak przed.
    /// </summary>
    internal static class Buildings338Crafts
    {
        internal const string CqId = "building_castle_craftmans_quarters";
        internal static bool On { get { var s = Settings.Current; return s != null && s.Buildings338 && s.Buildings338CastleCraftsmen; } }
        private static readonly string[][] Pairs = { new[] { "wool", "cloth" }, new[] { "flax", "linen" }, new[] { "hides", "leather" }, new[] { "hardwood", "planks" }, new[] { "iron", "tools" } };
        private static ItemObject[][] _items;
        private static int _castles, _made, _noRaw, _noGold, _noMarket, _noMargin, _stumbles;
        private static long _wage, _tax;
        private static long _sumMade, _sumWage, _sumTax; private static int _sumDays;

        internal static void SessionStart() { _items = null; _sumMade = _sumWage = _sumTax = 0; _sumDays = 0; _stumbles = 0; }

        private static ItemObject[][] Items()
        {
            if (_items != null) return _items;
            var l = new List<ItemObject[]>();
            foreach (var p in Pairs)
            {
                var a = MBObjectManager.Instance.GetObject<ItemObject>(p[0]);
                var b = MBObjectManager.Instance.GetObject<ItemObject>(p[1]);
                if (a != null && b != null) l.Add(new[] { a, b });
            }
            _items = l.ToArray();
            return _items;
        }

        internal static void CastleTick()
        {
            _castles = _made = _noRaw = _noGold = _noMarket = _noMargin = 0; _wage = _tax = 0;
            if (!On || Campaign.Current == null) return;
            var items = Items();
            foreach (var t in Town.AllCastles)
            {
                try
                {
                    int lvl = t != null ? Buildings338.Level(t, CqId) : 0;
                    if (lvl <= 0 || t.Settlement == null) continue;
                    _castles++;
                    var mk = Buildings338Ports.MarketTown(t);
                    if (mk == null || mk.Settlement == null || mk.Settlement.ItemRoster == null) { _noMarket++; continue; }
                    int left = 2 * lvl;
                    bool any = false, noRaw = true;
                    foreach (var pr in items)
                    {
                        if (left <= 0) break;
                        if (!Buildings338Works.InDeposit(t, pr[0])) continue;   // surowiec okolicy zamku
                        var shelf = mk.Settlement.ItemRoster;
                        int have = shelf.GetItemNumber(pr[0]);
                        if (have <= 0) continue;
                        noRaw = false;
                        int n = Math.Min(left, have);
                        int buy = Math.Max(1, mk.GetItemPrice(pr[0], null, false)), sell = Math.Max(1, mk.GetItemPrice(pr[1], null, true));
                        if (sell <= buy) { _noMargin++; continue; }
                        if (t.Gold < buy * n) n = t.Gold / buy;
                        if (n <= 0) { _noGold++; continue; }
                        if (mk.Gold < sell * n) n = Math.Min(n, mk.Gold / sell);
                        if (n <= 0) { _noGold++; continue; }
                        var fr = GoodsLedger.Begin(GoodsLedger.FTownCraft, mk.Settlement);
                        OreLedger.CraftEnter();   // 476-p4 (recenzja B5): wsad zelaza i drewna w pozycji "przetwory miast" ksiegi rudy - ta sama co ujscie ramki FTownCraft (kontrola GoodsLedger:1820)
                        try
                        {
                            shelf.AddToCounts(pr[0], -n);
                            OreLedger.OnConsumed(pr[0], mk.Settlement, n);
                            t.ChangeGold(-buy * n); mk.ChangeGold(buy * n);       // zamek kupuje surowiec od miasta
                            shelf.AddToCounts(pr[1], n);
                            mk.ChangeGold(-sell * n); t.ChangeGold(sell * n);     // miasto kupuje towar od zamku
                        }
                        finally { OreLedger.CraftExit(); if (fr != null) { try { GoodsLedger.End(fr); } catch { } } }
                        int margin = (sell - buy) * n;
                        int wage = (int)(margin * 0.6);
                        int folk = wage > 0 ? Math.Max(0, Math.Min(wage, TownFolk.Earn(t.Settlement, wage, TownFolk.KCraftPaid))) : 0;
                        if (folk > 0) t.ChangeGold(-folk);                       // place rzemieslnikow z kasy zamku do kiesy podzamcza
                        _wage += folk; _tax += margin - folk;
                        _made += n; left -= n; any = true;
                    }
                    if (!any && noRaw) _noRaw++;
                }
                catch (Exception e) { _stumbles++; if (_stumbles <= 3) { try { Log.Error("Buildings338Crafts.CastleTick", e); } catch { } } }
            }
        }

        internal static void Daily(int day)
        {
            if (!On) return;
            _sumDays++; _sumMade += _made; _sumWage += _wage; _sumTax += _tax;
            Log.Info("Budynki (338) rzemieslnicy zamkow (476): dzien " + day + " | zamkow z Craftmans Quarters " + _castles + " | towaru " + _made + " szt. z surowca okolicy, place rzemieslnikow "
                     + _wage + " zl, podatek pana " + _tax + " zl | bez surowca na polce " + _noRaw + ", bez kasy " + _noGold + ", bez miasta handlowego " + _noMarket + ", marza <= 0 " + _noMargin
                     + " | w biegu: towaru " + _sumMade + " szt., place " + _sumWage + " zl, podatek " + _sumTax + " zl w " + _sumDays + " dobach | bledy " + _stumbles);
        }
    }
}
