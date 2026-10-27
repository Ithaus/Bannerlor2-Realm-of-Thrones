using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// PACZKA 284 (D, klucz TownMaterialOrderWorkshopInputs): WSAD WARSZTATOW TOWAROWYCH. Dotad brak wsadu liczyly tylko warsztaty zbrojne (linia "Warsztaty":
    /// brak surowca ruda/drewno/skora/len-welna), strzelarze i rzemioslo miasta; warsztaty towarowe gry (garbarnia, tkalnie, kuznia narzedzi, warsztat drzewny,
    /// zlotnik, winnica...) stawaly bez wsadu bez zadnego sladu (log Jeffa 27.10: zlotnicy pracuja srednio 1.2 z 8 na dobe, 2.1 cyklu wobec 12 mozliwych).
    /// Pomiar (zawsze): prefiks cyklu warsztatu notabla i gracza (TickOneProductionCycleFor{Notable,Player}Workshop, Priority.First) ustawia warsztat cyklu; postfiks
    /// DetermineItemRosterHasSufficientInputs (tylko odczyt - GoodsPaths ma tam wlasny postfiks kosztu) przy wyniku false na polce miasta liczy brak kazdej kategorii
    /// wsadu, ktorej na polce jest mniej niz trzeba; udany cykl (postfiks cyklu z wynikiem true po sprawdzeniu wsadu) - zuzycie wsadu wedlug receptury gry.
    /// Zamowienia (klucz): kategoria wsadu, ktora jest surowcem tabeli zamowien 174 (ruda, drewno, skora, plotno, len, skory surowe, welna), daje sygnal
    /// MaterialOrders.NoteMiss i zuzycie MaterialOrders.NoteUse - miasto zamawia surowiec jak dla warsztatow zbrojnych. Srebro, bawelna, glina i jedzenie - tylko pomiar
    /// (rozszerzenie tabeli 174 o nowe surowce - osobny krok po pomiarze; jedzenie zabieraloby mieszczanom).
    /// Bez zlota i bez towaru - tylko liczniki i sygnal. Linia doby "Wsad warsztatow (284): dzien N". Ukryte warsztaty (rzemieslnicy BK) - pominiete.
    /// </summary>
    internal static class CommodityInputs284
    {
        private static Workshop _ctx;
        private static bool _ctxChecked;
        private static readonly Dictionary<ItemCategory, int> _miss = new Dictionary<ItemCategory, int>(), _use = new Dictionary<ItemCategory, int>();
        private static int _missCycles, _okCycles, _toOrders, _useOrders, _stumbles, _stumbleSess;
        private static bool _wCycle, _wSuf;
        private static readonly HashSet<string> _err = new HashSet<string>();

        internal static void Reset() { _ctx = null; _ctxChecked = false; _stumbleSess = 0; _err.Clear(); ZeroDay(); }

        private static void ZeroDay() { _miss.Clear(); _use.Clear(); _missCycles = _okCycles = _toOrders = _useOrders = _stumbles = 0; }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumbleSess++;
            try { if (_err.Add(where)) Log.Error("CommodityInputs284." + where, e); } catch { }
        }

        public static void CyclePre(Workshop __1)
        {
            _ctxChecked = false;
            try { _ctx = __1 != null && __1.WorkshopType != null && !__1.WorkshopType.IsHidden && __1.Settlement != null && __1.Settlement.Town != null ? __1 : null; }
            catch { _ctx = null; }
        }

        public static void CyclePost(bool __result, WorkshopType.Production __0, Workshop __1)
        {
            try
            {
                if (_ctx == null || __1 != _ctx || !__result || !_ctxChecked || __0.Inputs == null) return;
                _okCycles++;
                var town = __1.Settlement.Town;
                var s = Settings.Current;
                foreach (var inp in __0.Inputs)
                {
                    if (inp.Item1 == null || inp.Item2 <= 0) continue;
                    int x; _use.TryGetValue(inp.Item1, out x); _use[inp.Item1] = x + inp.Item2;
                    if (s != null && s.TownMaterialOrderWorkshopInputs && MaterialOrders.NoteUseCategory(town, inp.Item1, inp.Item2)) _useOrders += inp.Item2;
                }
            }
            catch (Exception e) { Stumble("CyclePost", e); }
            finally { _ctx = null; _ctxChecked = false; }
        }

        public static Exception CycleFin(Exception __exception) { _ctx = null; _ctxChecked = false; return __exception; }

        public static void SufPost(WorkshopType.Production __0, ItemRoster __1, Town __2, bool __result)
        {
            if (_ctx == null || __1 == null || __2 == null || __0.Inputs == null) return;
            try
            {
                if (__result) { _ctxChecked = true; return; }
                if (__2.Owner == null || !ReferenceEquals(__1, __2.Owner.ItemRoster) || _ctx.Settlement == null || _ctx.Settlement.Town != __2) return;   // magazyn gracza - potem polka miasta
                _missCycles++;
                var s = Settings.Current;
                foreach (var inp in __0.Inputs)
                {
                    var c = inp.Item1;
                    if (c == null || inp.Item2 <= 0) continue;
                    int have = 0;
                    for (int i = 0; i < __1.Count && have < inp.Item2; i++)
                    {
                        var it = __1.GetItemAtIndex(i);
                        if (it != null && it.ItemCategory == c) have += __1.GetElementNumber(i);
                    }
                    if (have >= inp.Item2) continue;
                    int x; _miss.TryGetValue(c, out x); _miss[c] = x + 1;
                    if (s != null && s.TownMaterialOrderWorkshopInputs && MaterialOrders.NoteMissCategory(__2, c)) _toOrders++;
                }
            }
            catch (Exception e) { Stumble("SufPost", e); }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var b = typeof(WorkshopsCampaignBehavior);
                var cn = AccessTools.DeclaredMethod(b, "TickOneProductionCycleForNotableWorkshop");
                var cp = AccessTools.DeclaredMethod(b, "TickOneProductionCycleForPlayerWorkshop");
                var suf = AccessTools.DeclaredMethod(b, "DetermineItemRosterHasSufficientInputs");
                if (cn != null && cp != null && suf != null && cn.ReturnType == typeof(bool) && cp.ReturnType == typeof(bool) && suf.ReturnType == typeof(bool))
                {
                    foreach (var m in new[] { cn, cp })
                        h.Patch(m, prefix: new HarmonyMethod(typeof(CommodityInputs284), nameof(CyclePre)) { priority = Priority.First },
                                   postfix: new HarmonyMethod(typeof(CommodityInputs284), nameof(CyclePost)),
                                   finalizer: new HarmonyMethod(typeof(CommodityInputs284), nameof(CycleFin)));
                    _wCycle = true;
                    h.Patch(suf, postfix: new HarmonyMethod(typeof(CommodityInputs284), nameof(SufPost)));
                    _wSuf = true;
                }
            }
            catch (Exception e) { Log.Error("CommodityInputs284.ApplyAll", e); }
            Log.Info("CommodityInputs284 (284): wsad warsztatow towarowych - cykl " + (_wCycle ? "wpiety" : "NIE") + ", sprawdzenie wsadu " + (_wSuf ? "wpiete" : "NIE") + ".");
        }

        private static string Top(Dictionary<ItemCategory, int> d, Func<ItemCategory, bool> keep, int k, out long sum)
        {
            var l = new List<KeyValuePair<ItemCategory, int>>();
            sum = 0;
            foreach (var kv in d) if (kv.Key != null && keep(kv.Key)) { l.Add(kv); sum += kv.Value; }
            l.Sort((a, b) => b.Value.CompareTo(a.Value));
            var sb = new StringBuilder();
            for (int i = 0; i < l.Count && i < k; i++) sb.Append(i > 0 ? ", " : "").Append(l[i].Key.StringId).Append(' ').Append(l[i].Value);
            return sb.Length > 0 ? sb.ToString() : "-";
        }

        private static bool Food(ItemCategory c) { return c.IsAnimal || CaravanTrade284.IsFoodCat(c); }

        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null || Campaign.Current == null) return;
            try
            {
                int day = (int)CampaignTime.Now.ToDays - 1;
                long a, b, c2, d2;
                string miss = Top(_miss, x => !Food(x), 20, out a), food = Top(_miss, Food, 10, out b), use = Top(_use, x => !Food(x), 20, out c2);
                Top(_use, Food, 0, out d2);
                Log.Info(new StringBuilder(1200).Append("Wsad warsztatow (284): dzien ").Append(day).Append(" | klucz Town Material Order Workshop Inputs ").Append(s.TownMaterialOrderWorkshopInputs ? "TAK" : "NIE")
                    .Append(", rowny zapas dni wszystkich surowcow ").Append(s.TownMaterialOrderEqualCoverAll && s.TownMaterialOrderOreEqualCover ? "TAK" : "NIE")
                    .Append(" | cykle warsztatow towarowych bez wsadu ").Append(_missCycles).Append(", udane ").Append(_okCycles)
                    .Append(" | brak wsadu wedlug kategorii (cykle; poza jedzeniem): ").Append(miss)
                    .Append(" | jedzenie i zwierzeta (tylko pomiar): ").Append(food).Append(" (razem ").Append(b).Append(')')
                    .Append(" | zuzycie wsadu (udane cykle; poza jedzeniem): ").Append(use).Append(" (jedzenie ").Append(d2).Append(')')
                    .Append(" | do zamowien surowca (tabela 174): braki ").Append(_toOrders).Append(", zuzycie ").Append(_useOrders).Append(" szt.")
                    .Append(" | przyciete rownym zapasem dni (poza ruda) ").Append(MaterialOrders.TakeEqCappedOther())
                    .Append(" | latki ").Append(_wCycle && _wSuf ? "TAK" : "NIE").Append(" | potkniecia ").Append(_stumbles).Append(" (sesja ").Append(_stumbleSess).Append(").").ToString());
            }
            catch (Exception e) { Stumble("Daily", e); }
            finally { ZeroDay(); }
        }
    }
}
