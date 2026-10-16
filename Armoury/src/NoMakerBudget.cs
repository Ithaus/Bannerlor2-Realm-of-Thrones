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
    /// N2-E2 (noc 16.10, galaz noc-ekonomia2; raport audytu ekonomii 15.10 rozdz. 1.8 tabela "nie do wydania" i rozdz. 3.2 P8 (b)) - BUDZET KATEGORII BEZ PRODUCENTA
    /// NA ZBYTKI, KTORE LEZA NA POLCE (klucz NoMakerBudgetToLuxury).
    /// Dowod (log Jeffa 2026-10-15_10-56-30, 12:10:56 "Ceny surowcow", kategorie z budzetem mieszczan): garum 5 029 d, Ink 3 422 d, Dyes 4 106 d, gems 170 d - "na polkach
    /// 0 szt., pusto w 97 miastach ... przy dzisiejszych polkach do 0 d"; ruda zlota (gold) 5 244 d, do wydania 399 d (14 sztuk w 5 miastach). W tescie 4-letnim te same
    /// kategorie puste przez 4 lata - zadna wies ani warsztat ich nie robi. Razem ok. 17 600 d na dobe budzetu ludu, ktorego nie da sie wydac; zloto zostaje w kiesie.
    /// Przyczyna [K]: BK EconomyPatches.ItemConsumptionPatch liczy budzet (CalculateBudget) tylko przy wierszu polki - kategoria bez towaru nigdy nie dochodzi do
    /// zakupu, a jej wpis w slowniku budzetu dnia (ItemConsumptionBehavior.UpdateDemandShift: popyt gry kazdej kategorii) przepada. Gra przesuwa popyt tylko miedzy
    /// para CanSubstitute (zadna z tych kategorii jej nie ma). ODSTEPSTWO OD PLANU AUDYTU (tam: HistoricalPrices.BudgetPostfix, wzor budzetu broni 0): BudgetPostfix
    /// biegnie tylko dla wiersza polki, wiec kategorii bez towaru nie widzi, a dopisek do wiersza celu ominalby kotwice kiesy ludu (TownFolk.AnchorDict skaluje
    /// SLOWNIK). Przesuwamy wiec w slowniku budzetu dnia - tam, gdzie zamiennik jedzenia (FoodSubstitute, B2) - i suma slownika zostaje ta sama.
    /// Regula (postfiks UpdateDemandShift, priorytet 450 - PO FoodSubstitute (HigherThanNormal 500), PRZED TownFolk.DemandPostfix (Normal 400), ktory otwiera nawias
    /// kiesy ludu: kotwica i pomiar widza slownik po przesunieciu, a nasze pytania o budzet BK nie wchodza do wierszy nawiasu): dla kategorii zrodla z tabeli
    /// (b = budzet dnia, B = budzet BK CalculateBudget(b), S = wartosc polki po cenie targu) przy S &lt; B przesuwamy CALY niezaspokojony udzial (1 - S/B) x b
    /// (DECYZJA CLAUDE: udzial 1 - to budzet, ktorego w tej osadzie nie da sie dzis wydac; ruda zlota z kilkoma sztukami w kilku miastach traci tylko czesc) na
    /// grupe zbytku z listy dostatku ludu (TownFolk.Luxury LxIds), w ktorej JEST towar:
    ///  - garum (sos rybny) -> lepszy stol (mieso, ser, maslo, piwo, wino, miod pitny, oliwa, miod) - po zamienniku jedzenia B2 (on bierze polowe na ryby i zboze);
    ///  - barwniki (Dyes) -> sukno i odzienie (bawelna, aksamit, futra, odziez; bez lnu, skory i filcu - wsad zbrojowni i odziezy wojska);
    ///  - kamienie szlachetne (gems) i ruda zlota (gold) -> srebro, klejnoty, korzenie (klejnoty, srebro, korzenie, kiel morsa);
    ///  - atrament (Ink) -> dom (garnki, tran, papirus).
    /// Cel bierze najwyzej SpareUse (0.5) swojej nadwyzki polki po ofercie ludu (S_t - B_t - oferta nadwyzki TownFolk.OfferCapFor, jak limit zamiennika B2),
    /// wspolnie dla wszystkich zrodel osady (limit liczony raz na osade i zmniejszany kazdym dopisem); podzial w grupie wedlug tego limitu. Czego cele nie
    /// przyjma - zostaje w kategorii zrodla (jak dotad). Popyt targu (ItemData.Demand, cena): ta sama zamiana z waga wygladzania gry, co w B2 (podloga 0.1 x popyt).
    /// POPRAWKA PO RECENZJI 16.10 (ShiftLimit): dopis podnosi popyt targu celu, a z nim indeks ceny i budzet BK celu ponad nachylenie liczone przy starym indeksie
    /// (w B2 to samo dawalo co dobe 51-91 przekroczen, najwiecej 53.6% nadwyzki) - najpierw plan calej osady, potem dopis kazdego celu x f (przyrost budzetu BK celu
    /// przy NOWYM popycie targu <= limit w zlocie), dopiero potem zmiany slownika i popytu; czesc odcieta zostaje w kategorii zrodla.
    /// Zloto: tylko przesuniecie budzetu w tej samej osadzie - placi dalej lud (TownFolk.PayForGoods: kiesa -> kasa miasta), kotwica ogranicza wydatek jak dotad.
    /// Tworzenie nowej gry (przed GameStarted) - pomijane w calosci (jak B2). Wyjatek przy osadzie - potkniecie (licznik), slownik tej osady bez zmian.
    /// Nic w zapisie. Linie: "Budzet bez producenta (N2-E2): start sesji" i "...: dzien N" (ile przeniesiono na co).
    /// </summary>
    internal static class NoMakerBudget
    {
        private static readonly string[] SrcIds = { "garum", "Dyes", "gems", "gold", "Ink" };
        private static readonly int[] SrcGroup = { 2, 0, 1, 1, 3 };                       // grupa celu kazdego zrodla (indeks GroupIds)
        private static readonly string[][] GroupIds =
        {
            new[] { "cotton", "velvet", "fur", "garment" },
            new[] { "jewelry", "silver", "spice", "walrus_tusk" },
            new[] { "meat", "cheese", "butter", "beer", "wine", "mead", "oil", "honey" },
            new[] { "pottery", "whale_oil", "Papyrus" }
        };
        private static readonly string[] GroupName = { "sukno i odzienie", "srebro, klejnoty, korzenie", "lepszy stol", "dom" };
        private const float SpareUse = 0.5f;   // DECYZJA (jak B2): wszystkie zrodla osady razem biora najwyzej polowe nadwyzki polki celu (po ofercie ludu)
        private const int Prio = 450;           // miedzy FoodSubstitute (500) a TownFolk.DemandPostfix (400)

        private sealed class Room { public float Left, Gold, B0, Bt0, Got, F = 1f, Used; }
        private sealed class Plan { public int Src; public ItemCategory To; public float Add; }
        private static ItemCategory[] _src;
        private static ItemCategory[][] _grp;
        private static HashSet<ItemCategory> _cats;
        private static Campaign _builtFor;
        private static string _mapText = "-";
        private static Func<Town, float, ItemCategory, float> _bk;
        private static bool _bkLooked;
        private static float _alpha = -1f;
        private static readonly Dictionary<ItemCategory, float> _shelf = new Dictionary<ItemCategory, float>();
        private static readonly Dictionary<ItemCategory, Room> _room = new Dictionary<ItemCategory, Room>();
        private static readonly List<Plan> _plan = new List<Plan>();
        private static readonly float[] _srcB = new float[SrcIds.Length], _srcMove = new float[SrcIds.Length], _srcMoved = new float[SrcIds.Length];
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        // liczniki od poprzedniej linii doby
        private static int _towns, _castles, _srcRows, _noRoom, _stumbles, _stDay, _over, _chk, _trim;
        private static double _need, _moved, _gold, _left, _maxUse;
        private static readonly double[] _byGroup = new double[4], _goldByGroup = new double[4];
        private static readonly Dictionary<string, double> _bySrc = new Dictionary<string, double>(StringComparer.Ordinal);
        private static readonly Dictionary<string, double> _byDst = new Dictionary<string, double>(StringComparer.Ordinal);
        private static bool _errLogged;
        private static string _hook = "nie wpiety";

        private static void EnsureMap()
        {
            var c = Campaign.Current;
            if (_src != null && ReferenceEquals(c, _builtFor)) return;
            var byId = new Dictionary<string, ItemCategory>(StringComparer.Ordinal);
            foreach (var cat in ItemCategories.All)
                if (cat != null && cat.StringId != null && !byId.ContainsKey(cat.StringId)) byId[cat.StringId] = cat;
            var src = new ItemCategory[SrcIds.Length];
            var grp = new ItemCategory[GroupIds.Length][];
            var cats = new HashSet<ItemCategory>();
            var found = new List<string>();
            for (int g = 0; g < GroupIds.Length; g++)
            {
                var l = new List<ItemCategory>();
                foreach (var id in GroupIds[g]) { ItemCategory t; if (byId.TryGetValue(id, out t) && !l.Contains(t)) { l.Add(t); cats.Add(t); } }
                grp[g] = l.ToArray();
            }
            for (int i = 0; i < SrcIds.Length; i++)
            {
                ItemCategory sc;
                if (!byId.TryGetValue(SrcIds[i], out sc)) continue;
                src[i] = sc; cats.Add(sc);
                found.Add(SrcIds[i] + " -> " + GroupName[SrcGroup[i]] + " (" + grp[SrcGroup[i]].Length + " kat.)");
            }
            _src = src; _grp = grp; _cats = cats; _builtFor = c; _alpha = -1f;
            _mapText = found.Count > 0 ? string.Join(", ", found.ToArray()) : "BRAK kategorii zrodel";
        }

        /// <summary>Budzet wiersza polki BK (EconomyPatches.CalculateBudget - z naszymi postfiksami), bez BK - model gry. Jak FoodSubstitute.Budget.</summary>
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

        /// <summary>Waga dzisiejszego popytu w wygladzaniu gry (GetSupplyDemandForCategory(..., dzis 1, stary 0) = 0.15) - raz na sesje (jak B2).</summary>
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
                if (s == null || !s.NoMakerBudgetToLuxury || __0 == null || __1 == null || Campaign.Current == null) return;
                if (!Campaign.Current.GameStarted) return;   // tworzenie nowej gry - jeden slownik dla wszystkich miast (jak B2)
                EnsureMap();
                Shift(__0, __1);
            }
            catch (Exception e)
            {
                _stumbles++; _stDay++;
                if (!_errLogged) { _errLogged = true; Log.Error("NoMakerBudget", e); }
            }
        }

        private static void Shift(Town town, Dictionary<ItemCategory, float> dict)
        {
            var shelf = town.Owner != null ? town.Owner.ItemRoster : null;
            var md = town.MarketData;
            if (shelf == null || md == null) return;
            _shelf.Clear(); _room.Clear(); _plan.Clear();
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
            // 1. PLAN (bez zmian slownika i popytu targu): ile kazde zrodlo chce oddac i ile kazdy cel przyjmie w swoim limicie (limit wspolny dla zrodel osady)
            int rows = 0, noRoom = 0;
            double need = 0, left = 0;
            ItemCategory anySrc = null;
            for (int i = 0; i < _src.Length; i++)
            {
                _srcB[i] = 0f; _srcMove[i] = 0f; _srcMoved[i] = 0f;
                var sc = _src[i];
                if (sc == null) continue;
                float b;
                if (!dict.TryGetValue(sc, out b) || !(b > 0.01f)) continue;
                float B = Budget(town, b, sc);
                if (!(B > 0.01f)) continue;
                float S; _shelf.TryGetValue(sc, out S);
                if (S >= B) continue;
                rows++;
                float move = (1f - S / B) * b;
                need += move;
                var targets = _grp[SrcGroup[i]];
                float roomSum = 0f;
                foreach (var t in targets)
                {
                    if (GoodsPaths.NoFolkLuxury(t)) continue;   // ZC1 (SilkToWeavers / SilverOreToSmiths): jedwab i ruda srebra nie sa celem - mieszczanie ich nie kupuja
                    var rm = RoomOf(town, dict, t);
                    if (rm != null && rm.Left > 0.01f) roomSum += rm.Left;
                }
                if (!(roomSum > 0.01f)) { noRoom++; left += move; continue; }
                float take = Math.Min(move, roomSum);
                int n0 = _plan.Count;
                foreach (var t in targets)
                {
                    Room rm;
                    if (GoodsPaths.NoFolkLuxury(t)) continue;   // ZC1: jak wyzej
                    if (!_room.TryGetValue(t, out rm) || rm == null || !(rm.Left > 0.01f)) continue;
                    float add = Math.Min(rm.Left, take * rm.Left / roomSum);
                    if (!(add > 0.01f)) continue;
                    _plan.Add(new Plan { Src = i, To = t, Add = add });
                }
                if (_plan.Count == n0) { noRoom++; left += move; continue; }
                for (int k = n0; k < _plan.Count; k++) { var rt = _room[_plan[k].To]; rt.Left -= _plan[k].Add; rt.Got += _plan[k].Add; }
                _srcB[i] = b; _srcMove[i] = move;
                if (anySrc == null) anySrc = sc;
            }
            // 2. LIMIT PRZY NOWYM POPYCIE TARGU (poprawka po recenzji 16.10, ShiftLimit): dopis celu x f - przyrost budzetu BK celu z indeksem ceny po dopisie
            // nie wiekszy niz 0.5 nadwyzki polki; popyt targu kazdego celu wraca do stanu sprzed rachunku (zmiany - dopiero w kroku 3)
            float alpha = anySrc != null ? Alpha(town, anySrc) : 0.15f;
            int chk = 0, trim = 0, over = 0;
            double maxUse = 0, gold = 0;
            foreach (var kv in _room)
            {
                var rm = kv.Value;
                if (rm == null || !(rm.Got > 0f)) continue;
                float used; bool cut;
                rm.F = ShiftLimit.Fit(town, md, kv.Key, rm.Bt0, rm.B0, rm.Got, rm.Gold, alpha, Budget, out used, out cut);
                rm.Used = used;
                chk++;
                if (cut) trim++;
                if (used > rm.Gold * 1.001f + 0.5f) over++;
                if (rm.Gold > 0f) { double u = SpareUse * used / rm.Gold; if (u > maxUse) maxUse = u; }
            }
            // 3. ZMIANY (po calym rachunku osady - slownik albo przesuniety, albo nietkniety; suma slownika bez zmian)
            foreach (var pl in _plan) _srcMoved[pl.Src] += pl.Add * _room[pl.To].F;
            foreach (var kv in _room)
            {
                var rm = kv.Value;
                if (rm == null || !(rm.Got > 0f)) continue;
                float add = rm.Got * rm.F;
                gold += rm.Used;
                for (int g = 0; g < _grp.Length; g++) if (Array.IndexOf(_grp[g], kv.Key) >= 0) { _goldByGroup[g] += rm.Used; break; }
                if (!(add > 0f)) continue;
                float before; dict.TryGetValue(kv.Key, out before);
                dict[kv.Key] = before + add;
                var dt = md.GetCategoryData(kv.Key);
                md.SetDemand(kv.Key, dt.Demand + alpha * add);
                string id = kv.Key.StringId ?? "?";
                double pv; _byDst.TryGetValue(id, out pv); _byDst[id] = pv + add;
            }
            double moved = 0;
            for (int i = 0; i < _src.Length; i++)
            {
                if (!(_srcMove[i] > 0f)) continue;
                var sc = _src[i];
                float mv = Math.Min(_srcMoved[i], _srcMove[i]);
                left += _srcMove[i] - mv;
                if (!(mv > 0f)) continue;
                dict[sc] = _srcB[i] - mv;
                var dc = md.GetCategoryData(sc);
                md.SetDemand(sc, Math.Max(0.1f * dc.Demand, dc.Demand - alpha * mv));
                moved += mv;
                _byGroup[SrcGroup[i]] += mv;
                string sid = sc.StringId ?? "?";
                double sv; _bySrc.TryGetValue(sid, out sv); _bySrc[sid] = sv + mv;
            }
            if (town.IsTown) _towns++; else _castles++;
            _srcRows += rows; _noRoom += noRoom; _need += need; _left += left; _moved += moved; _gold += gold;
            _chk += chk; _trim += trim; _over += over;
            if (maxUse > _maxUse) _maxUse = maxUse;
        }

        /// <summary>Limit celu t w tej osadzie - liczony raz (pierwsze uzycie), ze slownika po zamienniku jedzenia B2: 0.5 x (polka - budzet BK - oferta ludu) w zlocie
        /// i w jednostkach budzetu dnia (nachylenie BK). null = bez nadwyzki (pusta polka, polka zjedzona budzetem albo oferta ludu). Jak FoodSubstitute.RoomOf.</summary>
        private static Room RoomOf(Town town, Dictionary<ItemCategory, float> dict, ItemCategory t)
        {
            Room rm;
            if (_room.TryGetValue(t, out rm)) return rm;
            rm = null;
            float bt0;
            if (!dict.TryGetValue(t, out bt0) || bt0 < 0f) bt0 = 0f;
            float St; _shelf.TryGetValue(t, out St);
            if (St > 0f)
            {
                float Bt = Budget(town, bt0, t);
                if (St - Bt > 0f)
                {
                    float step = Math.Max(1f, 0.1f * bt0);
                    float slope = (Budget(town, bt0 + step, t) - Bt) / step;
                    if (slope > 0.001f)
                    {
                        double offU = TownFolk.OfferCapFor(town, t);
                        float offG = double.IsNaN(offU) || double.IsInfinity(offU) ? float.MaxValue : (float)(Math.Max(0.0, offU) * slope);
                        float spare = St - Bt - offG;
                        if (spare > 0f) rm = new Room { Left = SpareUse * spare / slope, Gold = SpareUse * spare, B0 = Bt, Bt0 = bt0 };
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
                if (m == null) { _hook = "BRAK ItemConsumptionBehavior.UpdateDemandShift"; Log.Info("Budzet bez producenta (N2-E2): " + _hook + " - nieczynne."); return; }
                h.Patch(m, postfix: new HarmonyMethod(typeof(NoMakerBudget), nameof(Postfix)) { priority = Prio });
                _hook = "wpiety (UpdateDemandShift, po zamienniku jedzenia B2, przed nawiasem kiesy ludu)";
                Log.Info("Budzet bez producenta (N2-E2): postfiks " + _hook + "; klucz NoMakerBudgetToLuxury czytany przy kazdym wywolaniu (stan - linia doby).");
            }
            catch (Exception e) { _hook = "wyjatek " + e.GetType().Name; Log.Error("NoMakerBudget.ApplyAll", e); }
        }

        /// <summary>Z ArmouryBehavior.OnSessionLaunched: linia startu sesji (klucz, latka, mapa kategorii tej kampanii).</summary>
        internal static void SessionStart()
        {
            _stumbles = 0; _errLogged = false; ClearDay();
            var s = Settings.Current;
            try { if (Campaign.Current != null) EnsureMap(); }
            catch (Exception e) { _stumbles++; if (!_errLogged) { _errLogged = true; Log.Error("NoMakerBudget.SessionStart", e); } }
            Log.Info("Budzet bez producenta (N2-E2): start sesji | klucz NoMakerBudgetToLuxury " + (s != null && s.NoMakerBudgetToLuxury ? "TAK | CZYNNE" : "NIE | WYLACZONE (MCM) - budzet kategorii bez towaru zostaje w kiesie ludu, jak dotad")
                     + " | latka " + _hook + " | zrodla -> grupy zbytku: " + _mapText + " | udzial niezaspokojony 1.00, cel bierze najwyzej " + SpareUse.ToString("0.0", Inv)
                     + " nadwyzki polki po ofercie ludu | potkniecia " + _stumbles + ".");
        }

        private static void ClearDay()
        {
            _towns = _castles = _srcRows = _noRoom = _stDay = _over = _chk = _trim = 0;
            _need = _moved = _gold = _left = _maxUse = 0;
            Array.Clear(_byGroup, 0, _byGroup.Length); Array.Clear(_goldByGroup, 0, _goldByGroup.Length);
            _bySrc.Clear(); _byDst.Clear();
        }

        private static string Top(Dictionary<string, double> d, int n)
        {
            var l = new List<KeyValuePair<string, double>>(d);
            l.Sort((a, b) => b.Value.CompareTo(a.Value));
            var p = new List<string>();
            for (int i = 0; i < l.Count && i < n; i++) p.Add(l[i].Key + " " + l[i].Value.ToString("0", Inv));
            return p.Count > 0 ? string.Join(", ", p.ToArray()) : "-";
        }

        /// <summary>Z ArmouryBehavior.OnDailyTick: linia "Budzet bez producenta (N2-E2): dzien" (tylko log).</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null) return;
            var gp = new List<string>();
            for (int g = 0; g < GroupName.Length; g++)
                gp.Add(GroupName[g] + " " + _byGroup[g].ToString("0", Inv) + " (" + _goldByGroup[g].ToString("0", Inv) + " zl)");
            Log.Info("Budzet bez producenta (N2-E2): dzien " + (int)CampaignTime.Now.ToDays + " | " + (s.NoMakerBudgetToLuxury ? "CZYNNE" : "WYLACZONE (MCM)")
                     + " | osad przeliczonych " + _towns + " (zamkow " + _castles + ") | wierszy zrodel z niezaspokojonym budzetem " + _srcRows
                     + " | niezaspokojone (budzet dnia) " + _need.ToString("0", Inv) + ", przeniesiono " + _moved.ToString("0", Inv) + " (zrodla: " + Top(_bySrc, 5) + ")"
                     + ", zostalo w kategorii zrodla " + _left.ToString("0", Inv) + " (bez miejsca na polce celu " + _noRoom + " wierszy)"
                     + " | na grupy (budzet dnia, w nawiasie przyrost budzetu BK celu w zlocie): " + string.Join(", ", gp.ToArray())
                     + " | najwiecej do: " + Top(_byDst, 8)
                     + " | suma slownika bez zmian (przesuniecie), budzet BK celow +" + _gold.ToString("0", Inv) + " zl"
                     + " | kontrola limitu celow: z dopisem " + _chk + ", przekroczen " + _over + " (najwiecej " + (_maxUse * 100.0).ToString("0.0", Inv) + "% nadwyzki)"
                     + ", przyciete (indeks ceny rosnie z popytem) " + _trim
                     + " | potkniecia " + _stDay + " (sesja " + _stumbles + ").");
            ClearDay();
        }
    }
}
