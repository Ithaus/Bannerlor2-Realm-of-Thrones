using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// PACZKA 174, PYTANIE 4 JEFFA - STARY NADMIAR NA ZLOM (wylacznik OldStockToScrap, DOMYSLNIE WYLACZONY - czeka na decyzje Jeffa).
    /// Kampania Jeffa: ok. 1 mln sztuk na polkach, w ok. 2/3 z dawnych darmowych kompletow (glownie bron 1H t1-t2 i buty); 174.0 zamyka ujscia, ktore
    /// ten stos powoli zjadaly - bez decyzji zostanie na zawsze. Regula (historia: stare kolczugi w Anglii lat 1330. szly na zlom za 7-14% ceny nowej):
    /// nadmiar koszyka (typ x tier) na polce miasta ponad rok popytu (364 x srednia dziennych zakupow lordow, zalog i notabli w tym miescie, zmierzona
    /// w tej sesji) i ponad popyt polki (SupplyDemand.Demand) kowale miasta skupuja na zlom po OldStockScrapDailyShare dziennie (1%), najgorsze
    /// sztuki najpierw. Zlom to metal: OldStockScrapYield (0.5) rudy, z ktorej sztuke wykuto (WorkshopLaw.Needs), wraca na polke tego miasta jako ruda -
    /// nic z niczego (sztuka znika, metal zostaje w czesci). Bez zlota: kowale i kupcy to ta sama kasa miasta (jak strzelarze 172). Rozgrzewka:
    /// regula rusza po 30 dobach pomiaru zakupow w sesji (srednia od zera nie moze udawac braku popytu). Linia "Zlom z nadmiaru (174, pytanie 4)".
    /// </summary>
    internal static class ArmsScrap
    {
        private static readonly Dictionary<long, float> _ema = new Dictionary<long, float>();     // (miasto, koszyk) -> srednia dziennych zakupow
        private static readonly Dictionary<long, float> _today = new Dictionary<long, float>();
        private static readonly Dictionary<long, float> _acc = new Dictionary<long, float>();     // ulamki skupu
        private static readonly Dictionary<Town, float> _oreAcc = new Dictionary<Town, float>();
        private static int _days, _lastDay = -1, _stumbles;
        private static ItemObject _ore;

        internal static bool On { get { var s = Settings.Current; return s != null && s.OldStockToScrap; } }

        internal static void Reset() { _ema.Clear(); _today.Clear(); _acc.Clear(); _oreAcc.Clear(); _days = 0; _lastDay = -1; _stumbles = 0; _ore = null; }

        private static long Key(Settlement st, int basket) { return ((long)st.Id.InternalValue << 8) ^ (uint)basket; }

        /// <summary>Zakup uzbrojenia z polki (AiGear, notable) - pomiar popytu koszyka w miescie (tylko licznik).</summary>
        internal static void NoteBuy(Settlement market, ItemObject it, int n)
        {
            try
            {
                if (market == null || it == null || n <= 0 || !ArmsLeaks.ArmsPiece(it)) return;
                long k = Key(market, (int)it.ItemType * 10 + Math.Max(1, Math.Min(6, (int)it.Tier + 1)));
                float v; _today.TryGetValue(k, out v); _today[k] = v + n;
            }
            catch { _stumbles++; }
        }

        internal static void Daily()
        {
            int day = (int)CampaignTime.Now.ToDays;
            if (day == _lastDay) return;
            _lastDay = day;
            // srednia zakupow (ok. 30 dob)
            try
            {
                var keys = new HashSet<long>(_ema.Keys); foreach (var k in _today.Keys) keys.Add(k);
                foreach (var k in keys) { float e, t; _ema.TryGetValue(k, out e); _today.TryGetValue(k, out t); _ema[k] = e + (t - e) / 30f; }
                _today.Clear(); _days++;
            }
            catch (Exception e) { _stumbles++; Log.Error("ArmsScrap.Daily(srednia)", e); }
            if (!On) return;
            var s = Settings.Current;
            if (_days < 30) { Log.Info("Zlom z nadmiaru (174, pytanie 4): dzien " + day + " - rozgrzewka " + _days + "/30 dob pomiaru zakupow w tej sesji, skup jeszcze nie rusza."); return; }
            if (_ore == null) { try { _ore = MBObjectManager.Instance.GetObject<ItemObject>("iron"); } catch { } }
            float share = MBMath.ClampFloat(s.OldStockScrapDailyShare, 0f, 1f), yield = MBMath.ClampFloat(s.OldStockScrapYield, 0f, 1f);
            long pieces = 0, worth = 0, excessAll = 0; int towns = 0, oreBack = 0;
            var byType = new Dictionary<int, long>();
            foreach (var t in Town.AllTowns)
            {
                if (t == null || !t.IsTown || t.Owner == null || t.Owner.ItemRoster == null) continue;
                var gf = GoodsLedger.Begin(GoodsLedger.FScrap, t);
                try
                {
                    var roster = t.Owner.ItemRoster;
                    var stock = new Dictionary<int, List<ItemRosterElement>>();
                    for (int i = 0; i < roster.Count; i++)
                    {
                        var el = roster.GetElementCopyAtIndex(i);
                        var it = el.EquipmentElement.Item;
                        if (el.Amount <= 0 || !ArmsLeaks.ArmsPiece(it) || ArmsPricing.IsUnique(it) || it.IsCraftedByPlayer) continue;
                        int b = (int)it.ItemType * 10 + Math.Max(1, Math.Min(6, (int)it.Tier + 1));
                        List<ItemRosterElement> l; if (!stock.TryGetValue(b, out l)) { l = new List<ItemRosterElement>(); stock[b] = l; }
                        l.Add(el);
                    }
                    int made = 0;
                    foreach (var kv in stock)
                    {
                        int have = 0; foreach (var el in kv.Value) have += el.Amount;
                        long k = Key(t.Settlement, kv.Key);
                        float ema; _ema.TryGetValue(k, out ema);
                        float keep = 364f * ema + SupplyDemand.Demand(t.Settlement, (ItemObject.ItemTypeEnum)(kv.Key / 10), kv.Key % 10);
                        float excess = have - keep;
                        if (excess <= 0f) continue;
                        excessAll += (long)excess;
                        float a; _acc.TryGetValue(k, out a);
                        a += excess * share;
                        int n = (int)a; _acc[k] = a - n;
                        if (n <= 0) continue;
                        kv.Value.Sort((x, y) => x.EquipmentElement.ItemValue.CompareTo(y.EquipmentElement.ItemValue));   // najgorsze sztuki najpierw
                        foreach (var el in kv.Value)
                        {
                            if (n <= 0) break;
                            int take = Math.Min(n, el.Amount);
                            roster.AddToCounts(el.EquipmentElement, -take);
                            n -= take; pieces += take; made += take; worth += (long)el.EquipmentElement.Item.Value * take;
                            long bt; byType.TryGetValue(kv.Key, out bt); byType[kv.Key] = bt + take;
                            float d; var need = WorkshopLaw.Needs(el.EquipmentElement.Item, out d);
                            if (need != null && yield > 0f) { float o; _oreAcc.TryGetValue(t, out o); _oreAcc[t] = o + need[0] * take * yield; }
                        }
                    }
                    if (made > 0) towns++;
                    float oa;
                    if (_ore != null && _oreAcc.TryGetValue(t, out oa) && oa >= 1f)
                    {
                        int ore = (int)oa; _oreAcc[t] = oa - ore;
                        roster.AddToCounts(_ore, ore);
                        OreLedger.NoteScrap(_ore, ore);
                        oreBack += ore;
                    }
                }
                catch (Exception e) { _stumbles++; Log.Error("ArmsScrap.Daily", e); }
                finally { GoodsLedger.End(gf); }
            }
            var parts = new List<string>();
            var bl = new List<KeyValuePair<int, long>>(byType); bl.Sort((x, y) => y.Value.CompareTo(x.Value));
            for (int i = 0; i < bl.Count && i < 8; i++) parts.Add((ItemObject.ItemTypeEnum)(bl[i].Key / 10) + " t" + (bl[i].Key % 10) + " " + bl[i].Value);
            Log.Info("Zlom z nadmiaru (174, pytanie 4): dzien " + day + " - skupiono na zlom " + pieces + " szt. (wartosc nowych " + worth + " d) w " + towns + " miastach [" + string.Join(", ", parts.ToArray())
                     + "]; ruda ze zlomu " + oreBack + " ladunkow (wydajnosc " + yield.ToString("0.##", CultureInfo.InvariantCulture) + "); nadmiar swiata ponad rok popytu ok. " + excessAll + " szt.; potkniecia " + _stumbles + ".");
            _stumbles = 0;
        }
    }
}
