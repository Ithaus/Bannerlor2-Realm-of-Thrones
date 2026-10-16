using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// ZAMIENNIK JEDZENIA, GDY NA STRAGANIE GO BRAK (decyzja Jeffa 15.10 TAK - "placek i marchew"; DECYZJA CLAUDE noc 15/16.10: polowa niezaspokojonego budzetu).
    /// Dowod (log 2026-10-15_10-56-30, 12:10:56 "Ceny surowcow"): placek - indeks 0.23/2.15/10.00, pusto w 41 miastach, budzet mieszczan 7146 d (przy polkach do 3871 d);
    /// owoce (apple) - 0 szt., pusto w 97 miastach, 9.95 x (budzet 9840 d, wydane 0); jajka 10.00 x, budzet 28 812 d (polki do 2481 d).
    /// Przyczyna [K]: gra przesuwa popyt tylko na kategorie obfitsza (ItemConsumptionBehavior.UpdateDemandShift: gdy S/D wlasnej > S/D zamiennika), wiec pusta polka
    /// placka nigdy nie oddaje popytu; BK MakeConsumption (EconomyPatches.ItemConsumptionPatch) zjada polke wedlug slownika budzetu dnia, a niewydany budzet zostaje
    /// w kiesie ludu - pusty towar ma pelny popyt i cena stoi pod sufitem x10.
    /// Regula (postfiks UpdateDemandShift, Priority.HigherThanNormal - PRZED TownFolk.DemandPostfix, ktory otwiera nawias konsumpcji: nasze pytania o budzet BK nie
    /// wchodza do jego wierszy, a jego kotwica i pomiar widza juz slownik po zamianie; suma slownika bez zmian): dla kategorii jedzenia z tabeli (b = budzet dnia,
    /// B = budzet BK CalculateBudget(b) - ten sam, ktory policzy wiersz polki, S = wartosc polki po cenie targu) przy S < B przesuwamy (1 - S/B) x b x FoodSubstituteShare
    /// na zamienniki z tabeli PO KOLEI (najblizszy najpierw, zboze i ryby na koncu - DECYZJA: kolejnosc zamiast podzialu wedlug zapasu, inaczej zboze o
    /// najwiekszej polce bralo prawie wszystko); zamiennik bierze najwyzej polowe swojej nadwyzki polki ponad dzisiejszy budzet (S_t - B_t), przeliczona na budzet
    /// dnia nachyleniem BK (dB/db = indeks^a) - jego polka nie pustoszeje w jeden dzien (bez kaskady brakow). Brak zamiennika z nadwyzka - nic (jak dotad).
    /// Popyt targu (ItemData.Demand, cena): ta sama zamiana z waga wygladzania gry (GetSupplyDemandForCategory: nowy = 0.85 stary + 0.15 dzisiejszy - waga mierzona
    /// raz na sesje), wiec w stanie ustalonym popyt pustego towaru spada o przesuniety budzet, a zamiennika rosnie o tyle samo - nie 6.7 razy tyle (POPRAWKA PLANU:
    /// odjecie calej kwoty co dobe kumulowaloby sie przez wygladzanie). Podloga 0.1 x popyt.
    /// Zloto: tylko przesuniecie budzetu w tym samym miescie - placi dalej lud (TownFolk / TownPurse), kotwica budzetu ludu ogranicza wydatek jak dotad.
    /// Petla startowa nowej gry (OnNewGameCreatedPartialFollowUpEnd - jeden slownik dla wszystkich miast) - pomijana. Wyjatek przy osadzie - potkniecie, nastepna dalej.
    /// </summary>
    internal static class FoodSubstitute
    {
        private static readonly string[][] Table =
        {
            new[] { "apple", "date_fruit", "grape", "olives", "grain", "fish" },
            new[] { "pie", "bread", "grain", "fish" },
            new[] { "honey", "butter", "cheese", "grain", "fish" },
            new[] { "Eggs", "meat", "cheese", "grain", "fish" },
            new[] { "garum", "fish", "grain" },
            new[] { "bread", "grain", "fish" },
            new[] { "butter", "cheese", "grain", "fish" },
            new[] { "cheese", "butter", "meat", "grain", "fish" },
            new[] { "meat", "fish", "grain" },
            new[] { "date_fruit", "apple", "grape", "grain", "fish" },
            new[] { "grape", "apple", "date_fruit", "grain", "fish" },
            new[] { "olives", "apple", "grain", "fish" },
        };
        private static readonly string[] IndexIds = { "apple", "pie", "honey", "Eggs", "garum", "bread", "meat", "cheese", "butter", "grain", "fish" };
        private const float SpareUse = 0.5f;   // DECYZJA: zamiennik oddaje najwyzej polowe nadwyzki polki ponad dzisiejszy budzet

        private sealed class Src { public ItemCategory Cat; public ItemCategory[] To; }
        private static List<Src> _map;
        private static HashSet<ItemCategory> _cats;
        private static Campaign _builtFor;
        private static object _lastDict;
        private static Func<Town, float, ItemCategory, float> _bk;
        private static bool _bkLooked;
        private static float _alpha = -1f;
        private static readonly Dictionary<ItemCategory, float> _shelf = new Dictionary<ItemCategory, float>();
        private static readonly List<KeyValuePair<ItemCategory, float>> _adds = new List<KeyValuePair<ItemCategory, float>>();
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        // liczniki od poprzedniej linii doby
        private static int _towns, _castles, _shortT, _shortC, _noSub, _stumbles, _stDay;
        private static double _moved;
        private static readonly Dictionary<string, double> _pairs = new Dictionary<string, double>(StringComparer.Ordinal);
        private static bool _errLogged;
        private static string _hook = "nie wpiety";

        private static void EnsureMap()
        {
            var c = Campaign.Current;
            if (_map != null && ReferenceEquals(c, _builtFor)) return;
            var byId = new Dictionary<string, ItemCategory>(StringComparer.Ordinal);
            foreach (var cat in ItemCategories.All)
                if (cat != null && cat.StringId != null && cat.IsTradeGood && cat.Properties == ItemCategory.Property.BonusToFoodStores && !byId.ContainsKey(cat.StringId)) byId[cat.StringId] = cat;
            var map = new List<Src>(); var cats = new HashSet<ItemCategory>();
            foreach (var row in Table)
            {
                ItemCategory src;
                if (!byId.TryGetValue(row[0], out src)) continue;
                var to = new List<ItemCategory>();
                for (int i = 1; i < row.Length; i++) { ItemCategory t; if (byId.TryGetValue(row[i], out t) && t != src && !to.Contains(t)) to.Add(t); }
                if (to.Count == 0) continue;
                map.Add(new Src { Cat = src, To = to.ToArray() });
                cats.Add(src); foreach (var t in to) cats.Add(t);
            }
            _map = map; _cats = cats; _builtFor = c; _alpha = -1f; _lastDict = null;
        }

        /// <summary>Budzet wiersza polki BK (EconomyPatches.CalculateBudget - z naszymi postfiksami), bez BK - model gry.</summary>
        private static float Budget(Town t, float demand, ItemCategory c)
        {
            if (!_bkLooked)
            {
                _bkLooked = true;
                try
                {
                    var m = AccessTools.Method("BannerKings.Patches.EconomyPatches:CalculateBudget");
                    if (m != null && m.IsStatic) _bk = (Func<Town, float, ItemCategory, float>)Delegate.CreateDelegate(typeof(Func<Town, float, ItemCategory, float>), m);
                }
                catch { _bk = null; }
            }
            float v = _bk != null ? _bk(t, demand, c) : Campaign.Current.Models.SettlementEconomyModel.CalculateDailySettlementBudgetForItemCategory(t, demand, c);
            return float.IsNaN(v) || float.IsInfinity(v) || v < 0f ? 0f : v;
        }

        /// <summary>Waga dzisiejszego popytu w wygladzaniu gry (GetSupplyDemandForCategory(..., dzis 1, stary 0) = 0.15) - raz na sesje.</summary>
        private static float Alpha(Town t, ItemCategory c)
        {
            if (_alpha > 0f) return _alpha;
            float a = 0.15f;
            try
            {
                var r = Campaign.Current.Models.SettlementEconomyModel.GetSupplyDemandForCategory(t, c, 0f, 1f, 0f, 0f);
                if (r.Item2 > 0.001f && r.Item2 <= 1f) a = r.Item2;
            }
            catch { a = 0.15f; }
            _alpha = a;
            return a;
        }

        public static void Postfix(Town __0, Dictionary<ItemCategory, float> __1)
        {
            try
            {
                var s = Settings.Current;
                if (s == null || !s.FoodSubstituteWhenShort || __0 == null || __1 == null || Campaign.Current == null) return;
                if (ReferenceEquals(__1, _lastDict)) return;   // petla startowa nowej gry: jeden slownik dla wszystkich miast - nie ruszamy
                _lastDict = __1;
                EnsureMap();
                Shift(__0, __1, s);
            }
            catch (Exception e)
            {
                _stumbles++; _stDay++;
                if (!_errLogged) { _errLogged = true; Log.Error("FoodSubstitute", e); }
            }
        }

        private static void Shift(Town town, Dictionary<ItemCategory, float> dict, Settings s)
        {
            var shelf = town.Owner != null ? town.Owner.ItemRoster : null;
            var md = town.MarketData;
            if (shelf == null || md == null || _map.Count == 0) return;
            if (town.IsTown) _towns++; else _castles++;
            _shelf.Clear();
            for (int i = 0; i < shelf.Count; i++)
            {
                var el = shelf.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (it == null || el.Amount <= 0) continue;
                var c = it.GetItemCategory();
                if (c == null || !_cats.Contains(c)) continue;
                int p = md.GetPrice(it);
                if (p <= 0) continue;
                float v; _shelf.TryGetValue(c, out v); _shelf[c] = v + (float)el.Amount * p;
            }
            float share = Math.Max(0f, Math.Min(1f, s.FoodSubstituteShare));
            if (!(share > 0f)) return;
            bool shortHere = false;
            foreach (var src in _map)
            {
                float b;
                if (!dict.TryGetValue(src.Cat, out b) || !(b > 0.01f)) continue;
                float B = Budget(town, b, src.Cat);
                if (!(B > 0.01f)) continue;
                float S; _shelf.TryGetValue(src.Cat, out S);
                if (S >= B) continue;
                shortHere = true;
                float move = (1f - S / B) * b * share, left = move;
                // najpierw rachunek (pytania o budzet BK moga rzucic), potem zmiany - slownik albo caly przesuniety, albo nietkniety (suma bez zmian)
                _adds.Clear();
                foreach (var t in src.To)
                {
                    float bt;
                    if (!dict.TryGetValue(t, out bt) || bt < 0f) continue;
                    float St; _shelf.TryGetValue(t, out St);
                    float Bt = Budget(town, bt, t);
                    float spare = St - Bt;
                    if (!(spare > 0f)) continue;
                    float step = Math.Max(1f, 0.1f * bt);
                    float slope = (Budget(town, bt + step, t) - Bt) / step;
                    if (!(slope > 0.001f)) continue;
                    float add = Math.Min(left, SpareUse * spare / slope);
                    if (!(add > 0.01f)) continue;
                    _adds.Add(new KeyValuePair<ItemCategory, float>(t, add));
                    left -= add;
                    if (left <= 0.01f) break;
                }
                float moved = move - left;
                if (_adds.Count == 0 || !(moved > 0.01f)) { _noSub++; continue; }
                float alpha = Alpha(town, src.Cat);
                foreach (var kv in _adds)
                {
                    dict[kv.Key] = dict[kv.Key] + kv.Value;
                    var dt = md.GetCategoryData(kv.Key);
                    md.SetDemand(kv.Key, dt.Demand + alpha * kv.Value);
                    string key = src.Cat.StringId + " -> " + kv.Key.StringId;
                    double pv; _pairs.TryGetValue(key, out pv); _pairs[key] = pv + kv.Value;
                }
                dict[src.Cat] = b - moved;
                var dc = md.GetCategoryData(src.Cat);
                md.SetDemand(src.Cat, Math.Max(0.1f * dc.Demand, dc.Demand - alpha * moved));
                _moved += moved;
            }
            if (shortHere) { if (town.IsTown) _shortT++; else _shortC++; }
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var m = AccessTools.Method(typeof(ItemConsumptionBehavior), "UpdateDemandShift", new[] { typeof(Town), typeof(Dictionary<ItemCategory, float>) });
                if (m == null) { _hook = "BRAK ItemConsumptionBehavior.UpdateDemandShift"; Log.Info("Zamienniki jedzenia (B2): " + _hook + " - nieczynne."); return; }
                h.Patch(m, postfix: new HarmonyMethod(typeof(FoodSubstitute), nameof(Postfix)) { priority = Priority.HigherThanNormal });
                _hook = "wpiety (UpdateDemandShift, przed nawiasem kiesy ludu)";
                Log.Info("Zamienniki jedzenia (B2): postfiks " + _hook + "; klucz FoodSubstituteWhenShort czytany przy kazdym wywolaniu (stan - linia doby).");
            }
            catch (Exception e) { _hook = "wyjatek " + e.GetType().Name; Log.Error("FoodSubstitute.ApplyAll", e); }
        }

        private static string Median(ItemCategory c)
        {
            var l = new List<float>();
            foreach (var t in Town.AllTowns)
            {
                try { if (t != null && t.IsTown) l.Add(t.GetItemCategoryPriceIndex(c)); } catch { }
            }
            if (l.Count == 0) return "-";
            l.Sort();
            return l[l.Count / 2].ToString("0.00", Inv);
        }

        /// <summary>Z ArmouryBehavior.OnDailyTick: linia "Zamienniki jedzenia (B2)" (tylko log).</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null) return;
            int towns = _towns, castles = _castles, st = _shortT, sc = _shortC, ns = _noSub, sd = _stDay;
            double moved = _moved;
            var pairs = new List<KeyValuePair<string, double>>(_pairs);
            _towns = _castles = _shortT = _shortC = _noSub = _stDay = 0; _moved = 0; _pairs.Clear();
            pairs.Sort((a, b) => b.Value.CompareTo(a.Value));
            var pp = new List<string>();
            for (int i = 0; i < pairs.Count && i < 14; i++) pp.Add(pairs[i].Key + " " + pairs[i].Value.ToString("0", Inv));
            var ix = new List<string>();
            try
            {
                if (Campaign.Current != null)
                {
                    EnsureMap();
                    var byId = new Dictionary<string, ItemCategory>(StringComparer.Ordinal);
                    foreach (var c in _cats) byId[c.StringId] = c;
                    foreach (var id in IndexIds) { ItemCategory c; if (byId.TryGetValue(id, out c)) ix.Add(id + " " + Median(c)); }
                }
            }
            catch (Exception e) { _stumbles++; sd++; if (!_errLogged) { _errLogged = true; Log.Error("FoodSubstitute.Daily", e); } }
            Log.Info("Zamienniki jedzenia (B2): dzien " + (int)CampaignTime.Now.ToDays + " | " + (s.FoodSubstituteWhenShort ? "CZYNNE" : "WYLACZONE (MCM)") + " (udzial " + s.FoodSubstituteShare.ToString("0.00", Inv)
                     + ", zamiennik oddaje najwyzej " + SpareUse.ToString("0.0", Inv) + " nadwyzki polki, waga wygladzania popytu " + (_alpha > 0f ? _alpha.ToString("0.00", Inv) : "-") + "; latka " + _hook + ")"
                     + " | osad przeliczonych " + towns + " (zamkow " + castles + ") | miast z brakiem jedzenia " + st + " (zamkow " + sc + ")"
                     + " | przesunieto budzetu " + moved.ToString("0", Inv) + (pp.Count > 0 ? " (" + string.Join(", ", pp.ToArray()) + ")" : "")
                     + " | bez zamiennika na polce " + ns
                     + " | indeks cen jedzenia (mediana miast): " + (ix.Count > 0 ? string.Join(", ", ix.ToArray()) : "-")
                     + " | potkniecia " + sd + " (sesja " + _stumbles + ").");
        }
    }
}
