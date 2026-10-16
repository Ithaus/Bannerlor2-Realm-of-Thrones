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
    /// najwiekszej polce bralo prawie wszystko). LIMIT ZAMIENNIKA NA OSADE (poprawka po recenzji 16.10): wszystkie zrodla tej osady RAZEM dopisuja do zamiennika t
    /// najwyzej SpareUse (0.5) jego nadwyzki polki N_t = S_t - B_t(slownik sprzed naszych zmian) - oferta nadwyzki ludu na t (TownFolk.OfferCapFor: AddOffer dopisuje
    /// ja po nas do "lepszego stolu" - mieso, ser, maslo; gorna granica = cala waga grupy), przeliczonej na budzet dnia nachyleniem BK (budzet BK jest liniowy w
    /// popycie: popyt x indeks^a + dobrobyt / indeks). Limit liczony RAZ przy pierwszym uzyciu t w osadzie i zmniejszany kazdym dopisem (nie od nowa ze zmienionego
    /// slownika - inaczej N zrodel bralo 1 - 0.5^N nadwyzki); zrodlo z brakiem nie ma nadwyzki, wiec nie bywa zamiennikiem (bez przelewania tam i z powrotem).
    /// Kontrola po osadzie: budzet BK dopisany do kazdego zamiennika <= 0.5 N_t (licznik przekroczen w linii doby). Brak zamiennika z nadwyzka - nic (jak dotad).
    /// POPRAWKA PO RECENZJI noc-ekonomia2 16.10 (ShiftLimit; autotesty 15.10: co dobe 51-91 przekroczen, najwiecej 53.6% nadwyzki): dopis podnosi popyt targu
    /// zamiennika, a z nim indeks ceny i budzet BK ponad nachylenie liczone przy starym indeksie - teraz najpierw plan calej osady (ze slownika sprzed zmian),
    /// potem dopis kazdego zamiennika x f (przyrost budzetu BK przy NOWYM popycie targu <= 0.5 N_t), dopiero potem zmiany; czesc odcieta zostaje w zrodle.
    /// Popyt targu (ItemData.Demand, cena): ta sama zamiana z waga wygladzania gry (GetSupplyDemandForCategory: nowy = 0.85 stary + 0.15 dzisiejszy - waga mierzona
    /// raz na sesje), wiec w stanie ustalonym popyt pustego towaru spada o przesuniety budzet, a zamiennika rosnie o tyle samo - nie 6.7 razy tyle (POPRAWKA PLANU:
    /// odjecie calej kwoty co dobe kumulowaloby sie przez wygladzanie). Podloga 0.1 x popyt.
    /// Zloto: tylko przesuniecie budzetu w tym samym miescie - placi dalej lud (TownFolk / TownPurse), kotwica budzetu ludu ogranicza wydatek jak dotad.
    /// Tworzenie nowej gry (przed Campaign.GameStarted: konsumpcja OnNewGameCreatedPartialFollowUp i petla OnNewGameCreatedPartialFollowUpEnd - jeden slownik dla
    /// wszystkich miast) - pomijane w calosci, jak nawias kiesy ludu (TownFolk.Run). Wyjatek przy osadzie - potkniecie, nastepna dalej.
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
        private const float SpareUse = 0.5f;   // DECYZJA: wszystkie zrodla osady razem biora najwyzej polowe nadwyzki polki zamiennika (po ofercie ludu)

        private sealed class Src { public ItemCategory Cat; public ItemCategory[] To; }
        /// <summary>Zamiennik w tej osadzie: reszta limitu (jednostki budzetu dnia), limit w zlocie (0.5 nadwyzki), budzet BK sprzed dopisow, suma dopisow.</summary>
        private sealed class Room { public float Left, Gold, B0, Got, F = 1f; }
        private sealed class Plan { public int Src; public ItemCategory To; public float Add; }
        private static List<Src> _map;
        private static HashSet<ItemCategory> _cats;
        private static Campaign _builtFor;
        private static Func<Town, float, ItemCategory, float> _bk;
        private static bool _bkLooked;
        private static float _alpha = -1f;
        private static readonly Dictionary<ItemCategory, float> _shelf = new Dictionary<ItemCategory, float>();
        private static readonly Dictionary<ItemCategory, float> _orig = new Dictionary<ItemCategory, float>();   // slownik osady sprzed naszych zmian (kategorie z tabeli)
        private static readonly Dictionary<ItemCategory, Room> _room = new Dictionary<ItemCategory, Room>();     // limit zamiennikow tej osady (null = bez nadwyzki)
        private static readonly List<Plan> _plan = new List<Plan>();
        private static float[] _srcB, _srcMoved;   // budzet dnia zrodla z planem i suma jego dopisow po limicie (indeks w _map)
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        // liczniki od poprzedniej linii doby
        private static int _towns, _castles, _shortT, _shortC, _noSub, _stumbles, _stDay;
        private static int _chk, _over, _offCut, _offAll, _trim;   // kontrola limitu: zamiennikow z dopisem, przekroczen; oferta ludu zmniejszyla / zjadla nadwyzke; przyciete (ShiftLimit)
        private static double _maxUse;                       // najwiekszy udzial nadwyzki zamiennika wziety w jednej osadzie (kontrola, cel <= 0.5)
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
            _map = map; _cats = cats; _builtFor = c; _alpha = -1f;
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
                // tworzenie nowej gry (konsumpcja startowa i petla OnNewGameCreatedPartialFollowUpEnd ze wspolnym slownikiem) - nie ruszamy; zapis wczytany ma GameStarted od razu
                if (!Campaign.Current.GameStarted) return;
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
            // slownik sprzed naszych zmian - z niego limit kazdego zamiennika (raz na osade, nie od nowa po dopisach) i budzet kazdego zrodla
            _orig.Clear(); _room.Clear(); _plan.Clear();
            foreach (var c in _cats) { float v; if (dict.TryGetValue(c, out v)) _orig[c] = v; }
            if (_srcB == null || _srcB.Length < _map.Count) { _srcB = new float[_map.Count]; _srcMoved = new float[_map.Count]; }
            bool shortHere = false;
            int noSub = 0;
            ItemCategory anySrc = null;
            // 1. PLAN (bez zmian slownika i popytu targu): zamienniki po kolei, limit zamiennika wspolny dla wszystkich zrodel osady
            for (int si = 0; si < _map.Count; si++)
            {
                _srcB[si] = 0f; _srcMoved[si] = 0f;
                var src = _map[si];
                float b;
                if (!_orig.TryGetValue(src.Cat, out b) || !(b > 0.01f)) continue;
                float B = Budget(town, b, src.Cat);
                if (!(B > 0.01f)) continue;
                float S; _shelf.TryGetValue(src.Cat, out S);
                if (S >= B) continue;
                shortHere = true;
                float move = (1f - S / B) * b * share, left = move;
                int n0 = _plan.Count;
                foreach (var t in src.To)
                {
                    var rm = RoomOf(town, t);
                    if (rm == null || !(rm.Left > 0.01f)) continue;
                    float add = Math.Min(left, rm.Left);
                    if (!(add > 0.01f)) continue;
                    _plan.Add(new Plan { Src = si, To = t, Add = add });
                    rm.Left -= add; rm.Got += add;
                    left -= add;
                    if (left <= 0.01f) break;
                }
                if (_plan.Count == n0) { noSub++; continue; }
                _srcB[si] = b;
                if (anySrc == null) anySrc = src.Cat;
            }
            // 2. LIMIT PRZY NOWYM POPYCIE TARGU (poprawka po recenzji noc-ekonomia2 16.10, ShiftLimit - dowod: 51-91 przekroczen na dobe, najwiecej 53.6% nadwyzki):
            // dopis zamiennika x f - przyrost budzetu BK zamiennika z indeksem ceny po dopisie <= 0.5 nadwyzki polki; popyt targu wraca do stanu sprzed rachunku
            float alpha = anySrc != null ? Alpha(town, anySrc) : 0.15f;
            foreach (var kv in _room)
            {
                var rm = kv.Value;
                if (rm == null || !(rm.Got > 0f)) continue;
                float bt0; _orig.TryGetValue(kv.Key, out bt0);
                float used; bool cut;
                rm.F = ShiftLimit.Fit(town, md, kv.Key, bt0, rm.B0, rm.Got, rm.Gold, alpha, Budget, out used, out cut);
                _chk++;
                if (cut) _trim++;
                if (rm.Gold > 0f) { double u = SpareUse * used / rm.Gold; if (u > _maxUse) _maxUse = u; }
                if (used > rm.Gold * 1.001f + 0.5f) _over++;
            }
            // 3. ZMIANY (po calym rachunku osady - slownik albo caly przesuniety, albo nietkniety; suma bez zmian)
            foreach (var pl in _plan)
            {
                float fin = pl.Add * _room[pl.To].F;
                _srcMoved[pl.Src] += fin;
                if (!(fin > 0f)) continue;
                string key = _map[pl.Src].Cat.StringId + " -> " + pl.To.StringId;
                double pv; _pairs.TryGetValue(key, out pv); _pairs[key] = pv + fin;
            }
            foreach (var kv in _room)
            {
                var rm = kv.Value;
                if (rm == null || !(rm.Got > 0f)) continue;
                float add = rm.Got * rm.F;
                if (!(add > 0f)) continue;
                float before; dict.TryGetValue(kv.Key, out before);
                dict[kv.Key] = before + add;
                var dt = md.GetCategoryData(kv.Key);
                md.SetDemand(kv.Key, dt.Demand + alpha * add);
            }
            for (int si = 0; si < _map.Count; si++)
            {
                if (!(_srcB[si] > 0f)) continue;
                float moved = _srcMoved[si];
                if (!(moved > 0.01f)) { noSub++; continue; }
                var sc = _map[si].Cat;
                dict[sc] = _srcB[si] - moved;
                var dc = md.GetCategoryData(sc);
                md.SetDemand(sc, Math.Max(0.1f * dc.Demand, dc.Demand - alpha * moved));
                _moved += moved;
            }
            _noSub += noSub;
            if (shortHere) { if (town.IsTown) _shortT++; else _shortC++; }
        }

        /// <summary>Limit zamiennika t w tej osadzie - liczony raz (pierwsze uzycie), ze slownika sprzed naszych zmian: 0.5 x (polka - budzet BK - oferta ludu)
        /// w zlocie i w jednostkach budzetu dnia (nachylenie BK). null = bez nadwyzki (zrodlo z brakiem, pusta polka, nadwyzke zjada oferta ludu).</summary>
        private static Room RoomOf(Town town, ItemCategory t)
        {
            Room rm;
            if (_room.TryGetValue(t, out rm)) return rm;
            rm = null;
            float bt0;
            if (_orig.TryGetValue(t, out bt0) && bt0 >= 0f)
            {
                float St; _shelf.TryGetValue(t, out St);
                float Bt = Budget(town, bt0, t);
                if (St - Bt > 0f)
                {
                    float step = Math.Max(1f, 0.1f * bt0);
                    float slope = (Budget(town, bt0 + step, t) - Bt) / step;
                    if (slope > 0.001f)
                    {
                        // oferta nadwyzki ludu (TownFolk.AddOffer dopisze ja po nas do "lepszego stolu") - gorna granica w budzecie BK
                        double offU = TownFolk.OfferCapFor(town, t);
                        float offG = double.IsNaN(offU) || double.IsInfinity(offU) ? float.MaxValue : (float)(Math.Max(0.0, offU) * slope);
                        float spare = St - Bt - offG;
                        if (offU > 0) { if (spare > 0f) _offCut++; else _offAll++; }
                        if (spare > 0f) rm = new Room { Left = SpareUse * spare / slope, Gold = SpareUse * spare, B0 = Bt };
                    }
                }
            }
            _room[t] = rm;
            return rm;
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

        /// <summary>Z ArmouryBehavior.OnSessionLaunched: linia startu sesji (klucz, latka, tabela kategorii tej kampanii).</summary>
        internal static void SessionStart()
        {
            var s = Settings.Current;
            int src = 0, dst = 0;
            try { if (Campaign.Current != null) { EnsureMap(); src = _map.Count; dst = _cats.Count - _map.Count; } }
            catch (Exception e) { _stumbles++; if (!_errLogged) { _errLogged = true; Log.Error("FoodSubstitute.SessionStart", e); } }
            Log.Info("Zamienniki jedzenia (B2): start sesji | klucz FoodSubstituteWhenShort " + (s != null && s.FoodSubstituteWhenShort ? "TAK" : "NIE")
                     + " (udzial " + (s != null ? s.FoodSubstituteShare.ToString("0.00", Inv) : "-") + ", zamiennik oddaje wszystkim zrodlom osady razem najwyzej " + SpareUse.ToString("0.0", Inv) + " nadwyzki polki po ofercie ludu) | latka " + _hook
                     + " | budzet BK " + (Campaign.Current != null ? "pytany przy pierwszej osadzie" : "-") + " | kategorii jedzenia z tabeli: zrodel " + src + ", innych kategorii w tabeli " + Math.Max(0, dst)
                     + " | potkniecia " + _stumbles + ".");
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
            int chk = _chk, over = _over, offCut = _offCut, offAll = _offAll, trim = _trim;
            double moved = _moved, maxUse = _maxUse;
            var pairs = new List<KeyValuePair<string, double>>(_pairs);
            _towns = _castles = _shortT = _shortC = _noSub = _stDay = 0; _moved = 0; _pairs.Clear();
            _chk = _over = _offCut = _offAll = _trim = 0; _maxUse = 0;
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
                     + ", zamiennik oddaje wszystkim zrodlom osady razem najwyzej " + SpareUse.ToString("0.0", Inv) + " nadwyzki polki po ofercie ludu, waga wygladzania popytu " + (_alpha > 0f ? _alpha.ToString("0.00", Inv) : "-") + "; latka " + _hook + ")"
                     + " | osad przeliczonych " + towns + " (zamkow " + castles + ") | miast z brakiem jedzenia " + st + " (zamkow " + sc + ")"
                     + " | przesunieto budzetu " + moved.ToString("0", Inv) + (pp.Count > 0 ? " (" + string.Join(", ", pp.ToArray()) + ")" : "")
                     + " | bez zamiennika na polce " + ns
                     + " | kontrola limitu zamiennikow: z dopisem " + chk + ", przekroczen " + over + " (najwiecej " + (maxUse * 100.0).ToString("0.0", Inv) + "% nadwyzki), oferta ludu zmniejszyla nadwyzke w " + offCut + ", zjadla w " + offAll + ", przyciete (indeks ceny rosnie z popytem) " + trim
                     + " | indeks cen jedzenia (mediana miast): " + (ix.Count > 0 ? string.Join(", ", ix.ToArray()) : "-")
                     + " | potkniecia " + sd + " (sesja " + _stumbles + ").");
        }
    }
}
