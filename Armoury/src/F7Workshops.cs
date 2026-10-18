using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// F7-BROWARY (noc 18.10, paczka f7-browary; fin91 Armoury-2026-10-18_06-29-09.log, NWAR-4b "browary + garbarnie + garncarze + tkalnie lnu 47 -> 23 (0.49 x)").
    ///
    /// Pomiar fin91 (linie "Warsztaty towarowe", "Towary", "Rzemioslo miasta (148)", "Ceny surowcow", "Warsztaty za cena (E3-D3)"):
    ///  - caly spadek 47 -> 22 przyszedl w pierwszych 33 dobach, PRZED wlaczeniem KS (109111); pod KS 22 -> 23. Przezbrajane byly warsztaty tracace od >= 60 dob
    ///    w obu pamieciach (srednia -3..-6 d/d = samo utrzymanie: 0 cykli). Regula przezbrojenia F4 wybiera dobrze (cele pracuja 44 z 44) - przyczyna lezy w rynku;
    ///  - tkalnie lnu (17): 0 cykli przez cale przed-KS. Len na polkach jest (8140, indeks 1.30), plotno 0.67: 6 lnu -> 2 plotna (receptura 216) kosztuje 156 + 9 d plac,
    ///    utarg 134. Rzemioslo miasta (148) tka te sama proporcja tylko dzieki cenie wywozu (274 z 417 szt./d "tylko dzieki cenie wywozu");
    ///  - garbarnie (12): 0-2 cykli. Skory surowe indeks 1.6-2.1, skora 0.5-0.8 (20-41 miast z nadwyzka po 18-22 d): 5 skor -> 2 skory kosztuje 82-100 + 9 d, utarg 40-64;
    ///  - garncarze (6): 0 cykli od pierwszej doby - glina na polkach WSZYSTKICH miast 96 szt. (wsie 2298, karawany 1112), budowy biora 224 -> 1088 szt./d
    ///    (pod KS 2 x produkcja gliny 438-550);
    ///  - browary (12 -> 8 przed KS): receptura gry 1 zboze -> 2 piwa przy piwie i zbozu po 3 d nie placi 18 d plac na dobe + 4 d utrzymania; pod KS (2 -> 5) pracuja.
    /// Przyczyny w kodzie:
    ///  (1) KOLEJNOSC: sluchacz doby miasta rzemiosla (148) idzie PRZED warsztatami gry (MbEvent wola od ostatnio dopisanego) - przypadek rejestracji, nie regula.
    ///      Ukryte rece miasta (zajete 11%, bezczynne nic nie traca) zabieraja len, skory i welne z polki przed warsztatem notabla, ktory za wsad placi, placi
    ///      place i utrzymanie (len: przerob konczy sie "brak surowca" w 46-70 miastach);
    ///  (2) CENA WYROBU NIE WIDZI ODBIORU: popyt ceny surowcow (RawPrice / CaravanBulk.Use) liczy dla skory i plotna tylko mieszczan i rece platnerzy, a nie odziez
    ///      wojska (150: 71-108 szt./d), naprawy kowali (135), zakupy wsi w miescie (E5/B1: 4-238) i dwory (E3-P1) - polowa (plotno) do dwoch trzecich (skora)
    ///      prawdziwego zuzycia. Wsad (skory, len) ma w popycie cale zuzycie rzemiosla - stad klin cen: wsad 1.6-2.0, wyrob 0.5-0.8, a stan skory w swiecie spada;
    ///  (3) GLINA: budowy (pan, lud, roboty gminy) kupuja gline z polki targu do ostatniej sztuki; garncarze nie maja z czego robic.
    /// Regula (trzy klucze, kazdy osobno; wylaczone = kod jak dotad):
    ///  K1 TownCraftsAfterWorkshops - rzemioslo miasta (148) w postfiksie WorkshopsCampaignBehavior.DailyTickTown (cel przez AccessTools.DeclaredMethod, priorytet High -
    ///     przed strzelarzami 172, ktorzy dalej widza swieza skore i plotno); przetwory KS (ZC5) zostaja przed warsztatami. Bez latki albo bez zachowania warsztatow
    ///     gry w kampanii - po staremu (sluchacz przed warsztatami). Raz na miasto i dobe (TownCrafts._workDay);
    ///  K2 ClothDemandFromOfftake - odbior skory i plotna z polki miasta w ramkach ksiegi towarow (GoodsLedger.SettleMk: odziez wojska, naprawy kowali, zakupy wsi,
    ///     dwory - tylko ubytek netto ramki, tylko miasta) -> srednia ok. 14 dob na miasto -> popyt ceny surowcow (RawPrice.UseDemand, czesc "off"). SAMA CENA:
    ///     CaravanBulk.Use (zapas karawan, prog nadwyzki, prog polki MaterialOrders.ShelfReserve - zapas wojska, z ktorego wsie i zbytki ludu kupuja tylko ponad
    ///     nim) bez zmian - zakupy wsi w progu dalyby petle (wsie podnosilyby prog, ktory je wyklucza). Pierwsza doba sesji (niepelna) odrzucona, pierwsza pelna
    ///     doba = srednia; zapis "arm_f7_cloth" (straznik 20 000 znakow);
    ///  K3 BuildClayForPotters - budowy biora gline z polki targu tylko ponad zuzycie warsztatow garncarskich miasta (linie towarowe z wsadem gliny: szybkosc x sztuk
    ///     wsadu, aktualna receptura) x CaravanBulkStockDays - ta sama regula co drewno K1 (BuildFunding.WoodHold); ponizej budowa kupuje inny material albo czeka.
    /// Obieg: zadna regula nie tworzy przeplywu - K1 zmienia kolejnosc (warsztat placi miastu za wsad jak dotad, rzemioslo bez zlota), K2 tylko cene (zloto
    /// plynie tymi samymi drogami: kupujacy -> kasa miasta), K3 budowa czeka albo kupuje inny material (towar zostaje na polce).
    /// Browary przed KS - przyczyna w recepturze gry i placach (KsRecipes tylko pod KS) - bez zmiany w tej paczce (not_done).
    /// </summary>
    internal static class F7Workshops
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static int _stumbles, _stumblesAll;
        private static readonly HashSet<string> _errOnce = new HashSet<string>();

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesAll++;
            if (_errOnce.Add(where)) Log.Error("F7Workshops." + where, e);
        }

        /// <summary>Nowa gra / wczytanie (z TownCrafts.Reset, czyli z konstruktora ArmouryBehavior - przed SyncData). Latki (AfterHooked) zostaja na proces.</summary>
        internal static void Reset()
        {
            _behOk = true; _dAfter = 0; _dBefore = 0;
            _today.Clear(); _avg.Clear(); _days = 0; _firstDay = true; _pending = null; _loadedDays = -1; _loadedTowns = 0; _saveCut = 0;
            Array.Clear(_dSrc, 0, _dSrc.Length);
            _leather = null; _linen = null; _clay = null; _itemsTried = false;
            _clayDay = int.MinValue; _clayNeed.Clear(); _clayHeldLord.Clear(); _clayHeldFolk.Clear(); _clayHeldLordPcs = 0; _clayHeldFolkPcs = 0;
            _stumbles = 0; _stumblesAll = 0; _errOnce.Clear();
        }

        private static bool Items()
        {
            if (_leather != null && _linen != null) return true;
            if (_itemsTried && (_leather == null || _linen == null)) return false;
            var om = MBObjectManager.Instance;
            if (om == null) return false;
            _itemsTried = true;
            _leather = om.GetObject<ItemObject>("leather");
            _linen = om.GetObject<ItemObject>("linen");
            _clay = om.GetObject<ItemObject>("clay");
            return _leather != null && _linen != null;
        }

        // ============================================================ K1: rzemioslo miasta po warsztatach
        internal static bool AfterHooked;                // postfiks WorkshopsCampaignBehavior.DailyTickTown wpiety (raz na proces - Reset go nie rusza)
        private static bool _behOk = true;               // zachowanie warsztatow gry obecne w kampanii (sprawdzane przy starcie sesji)
        private static int _dAfter, _dBefore;            // miasto-doby rzemiosla 148: po warsztatach (postfiks) / przed nimi (sluchacz - stara kolejnosc albo zapasowo)
        internal static bool AfterKey { get { var s = Settings.Current; return s != null && s.TownCraftsAfterWorkshops; } }
        /// <summary>K1 czynne: klucz, latka wpieta i zachowanie warsztatow gry w kampanii - inaczej rzemioslo idzie po staremu w sluchaczu doby miasta.</summary>
        internal static bool CraftAfterOn { get { return AfterHooked && _behOk && AfterKey; } }

        /// <summary>SubModuleMain (menu, raz na proces) - po TownFletchers.ApplyAll: postfiks doby warsztatow miasta. Wlasny try; brak celu = K1 nieczynne (stara kolejnosc).</summary>
        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(WorkshopsCampaignBehavior), "DailyTickTown", new[] { typeof(Town) });
                if (m != null)
                {
                    h.Patch(m, postfix: new HarmonyMethod(typeof(F7Workshops), nameof(TickPostfix)) { priority = Priority.High });
                    AfterHooked = true;
                }
                Log.Info("Warsztaty codzienne (f7-browary): latki - rzemioslo miasta po warsztatach (postfiks WorkshopsCampaignBehavior.DailyTickTown, przed strzelarzami 172) "
                         + (AfterHooked ? "wpiete" : "BRAK celu - rzemioslo przed warsztatami jak dotad") + ".");
            }
            catch (Exception e) { Log.Error("F7Workshops.ApplyAll", e); }
        }

        /// <summary>Postfiks doby warsztatow miasta (priorytet High): rzemioslo miasta 148 na tym, co zostalo po warsztatach (TownCrafts.RunAfterWorkshops - wlasny try).</summary>
        public static void TickPostfix(Town __0) { TownCrafts.RunAfterWorkshops(__0); }

        internal static void NoteAfter() { _dAfter++; }
        internal static void NoteBefore() { _dBefore++; }

        // ============================================================ K2: popyt skory i plotna z odbioru
        internal static bool OffKey { get { var s = Settings.Current; return s != null && s.ClothDemandFromOfftake; } }
        /// <summary>K2 czynne: klucz i ksiega towarow (ramki tylko przy niej). Bez ksiegi - srednia nie rosnie, zuzycie miasta jak dotad.</summary>
        internal static bool OffOn { get { return OffKey && GoodsLedger.On; } }
        private const float OffSpan = 14f;               // srednia z ok. 14 dob (jak zuzycie rzemiosla 148 i zamowien 174)
        private const string OffTag = "f7c1";
        private const int OffSaveMax = 20000;            // straznik rozmiaru zapisu (97 miast x 2 liczby - ok. 3 000 znakow)
        private static ItemObject _leather, _linen, _clay; private static bool _itemsTried;
        private static readonly Dictionary<Town, float[]> _today = new Dictionary<Town, float[]>();   // miasto -> [skora, plotno] od ostatniej linii (sztuki)
        private static readonly Dictionary<Town, float[]> _avg = new Dictionary<Town, float[]>();     // miasto -> srednia ok. 14 dob (sztuki na dobe)
        private static int _days; private static bool _firstDay = true;
        private static string _pending; private static int _loadedDays = -1, _loadedTowns, _saveCut;
        private static readonly long[,] _dSrc = new long[2, 4];   // [skora/plotno, wojsko/naprawy/wsie/dwory] od ostatniej linii

        /// <summary>GoodsLedger.SettleMk (po rozliczeniu ramki): ubytek netto skory i plotna w ramkach odziezy wojska (150), napraw kowali (135), zakupow wsi (E5/B1)
        /// i dworow (E3-P1) z osada-miastem - sam licznik. Ramki zamkow i bez osady pomijane.</summary>
        internal static void FromFrame(int kind, object a, List<int> touched, int[] net, ItemObject[] items)
        {
            try
            {
                int src = kind == GoodsLedger.FArmyCloth ? 0 : kind == GoodsLedger.FMend ? 1 : kind == GoodsLedger.FFolkVill ? 2 : kind == GoodsLedger.FCourt ? 3 : -1;
                if (src < 0 || touched == null || touched.Count == 0 || net == null || items == null || !OffKey) return;
                var st = a as Settlement;
                if (st == null) { var t0 = a as Town; if (t0 != null) st = t0.Settlement; }
                var town = st != null ? st.Town : null;
                if (town == null || !st.IsTown || !Items()) return;
                float[] arr = null;
                foreach (var i in touched)
                {
                    if (i < 0 || i >= net.Length || i >= items.Length || net[i] >= 0) continue;
                    var it = items[i];
                    int g = it == _leather ? 0 : it == _linen ? 1 : -1;
                    if (g < 0) continue;
                    if (arr == null && !_today.TryGetValue(town, out arr)) { arr = new float[2]; _today[town] = arr; }
                    arr[g] += -net[i];
                    _dSrc[g, src] += -net[i];
                }
            }
            catch (Exception e) { Stumble("FromFrame", e); }
        }

        /// <summary>RawPrice.UseDemand (popyt ceny surowcow): odbior skory albo plotna z polki miasta (srednia ok. 14 dob, sztuki rynku na dobe); 0 = klucz wylaczony,
        /// inny towar, pomiar jeszcze pusty albo blad (popyt jak dotad).</summary>
        internal static float UseOf(Town town, ItemObject item)
        {
            if (town == null || item == null || !OffOn) return 0f;
            try
            {
                if (!Items()) return 0f;
                int g = item == _leather ? 0 : item == _linen ? 1 : -1;
                if (g < 0) return 0f;
                float[] a;
                return _avg.TryGetValue(town, out a) ? a[g] : 0f;
            }
            catch (Exception e) { Stumble("UseOf", e); return 0f; }
        }

        private static void Fold(Dictionary<Town, float[]> cur)
        {
            _days++;
            float k = Math.Min(_days, OffSpan);   // pierwsza pelna doba = srednia (k = 1), potem srednia wykladnicza z ok. 14 dob
            var towns = new List<Town>(_avg.Keys);
            foreach (var t in cur.Keys) if (!_avg.ContainsKey(t)) towns.Add(t);
            foreach (var t in towns)
            {
                float[] a, c;
                if (!_avg.TryGetValue(t, out a)) { a = new float[2]; _avg[t] = a; }
                cur.TryGetValue(t, out c);
                for (int g = 0; g < 2; g++)
                {
                    float now = c != null ? c[g] : 0f;
                    a[g] += (now - a[g]) / k;
                    if (a[g] < 0.001f) a[g] = 0f;
                }
                if (a[0] <= 0f && a[1] <= 0f) _avg.Remove(t);
            }
        }

        /// <summary>SyncData (zapis): "f7c1|doby|miasto=skora,plotno|..." - null przy wylaczonym kluczu (klucz zapisu nie powstaje, zapis jak dotad).</summary>
        internal static string Export()
        {
            if (!OffKey) return null;
            try
            {
                Resolve();
                var sb = new StringBuilder(4096);
                sb.Append(OffTag).Append('|').Append(_days.ToString(Inv));
                foreach (var kv in _avg)
                {
                    var t = kv.Key; var a = kv.Value;
                    string id = t != null && t.Settlement != null ? t.Settlement.StringId : null;
                    if (a == null || string.IsNullOrEmpty(id) || id.IndexOf('|') >= 0 || id.IndexOf('=') >= 0 || id.IndexOf(',') >= 0) continue;
                    string part = id + "=" + a[0].ToString("0.###", Inv) + "," + a[1].ToString("0.###", Inv);
                    if (sb.Length + part.Length + 1 > OffSaveMax) { _saveCut++; break; }   // straznik rozmiaru: reszta miast od zera po wczytaniu
                    sb.Append('|').Append(part);
                }
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return null; }
        }

        /// <summary>SyncData (wczytanie): napis odlozony do pierwszego uzycia na watku glownym; brak klucza (stary zapis) = pomiar od zera.</summary>
        internal static void Import(string data) { _pending = string.IsNullOrEmpty(data) ? null : data; }

        private static void Resolve()
        {
            if (_pending == null || Campaign.Current == null) return;
            string data = _pending; _pending = null;
            try
            {
                var parts = data.Split('|');
                int d;
                if (parts.Length < 2 || parts[0] != OffTag || !int.TryParse(parts[1], NumberStyles.Integer, Inv, out d) || d < 0) { _loadedDays = 0; return; }
                var byId = new Dictionary<string, Town>(StringComparer.Ordinal);
                foreach (var t in Town.AllTowns) if (t != null && t.IsTown && t.Settlement != null) byId[t.Settlement.StringId] = t;
                int towns = 0;
                for (int i = 2; i < parts.Length; i++)
                {
                    int eq = parts[i].IndexOf('=');
                    if (eq <= 0) continue;
                    Town town;
                    if (!byId.TryGetValue(parts[i].Substring(0, eq), out town)) continue;
                    var v = parts[i].Substring(eq + 1).Split(',');
                    float l, n;
                    if (v.Length < 2 || !float.TryParse(v[0], NumberStyles.Float, Inv, out l) || !float.TryParse(v[1], NumberStyles.Float, Inv, out n)) continue;
                    if (!(l >= 0f) || !(n >= 0f) || float.IsInfinity(l) || float.IsInfinity(n)) continue;
                    _avg[town] = new[] { l, n }; towns++;
                }
                _days = Math.Max(_days, d); _loadedDays = d; _loadedTowns = towns;
            }
            catch (Exception e) { Stumble("Resolve", e); }
        }

        // ============================================================ K3: glina budow ponad potrzebe garncarzy
        internal static bool ClayKey { get { var s = Settings.Current; return s != null && s.BuildClayForPotters; } }
        private static int _clayDay = int.MinValue;
        private static readonly Dictionary<Town, int> _clayNeed = new Dictionary<Town, int>();   // prog gliny miasta na dzis (sztuki) - pamiec doby
        private static readonly HashSet<Settlement> _clayHeldLord = new HashSet<Settlement>(), _clayHeldFolk = new HashSet<Settlement>();
        private static long _clayHeldLordPcs, _clayHeldFolkPcs;

        /// <summary>K3: ile gliny budowa zostawia na polce targu dla warsztatow garncarskich miasta - zuzycie dobowe linii towarowych z wsadem gliny (szybkosc x sztuk
        /// wsadu aktualnej receptury) x CaravanBulkStockDays; inny material, klucz wylaczony albo blad - 0 (jak dotad). folk - licznik robot ludu i gminy, inaczej pana.
        /// Licznik: raz na targ i dobe, sztuki zostawione = min(polka, prog).</summary>
        internal static int ClayHold(Settlement market, ItemObject it, bool folk)
        {
            try
            {
                if (it == null || market == null || market.Town == null || !ClayKey || it.StringId != "clay") return 0;
                int day = (int)CampaignTime.Now.ToDays;
                if (day != _clayDay) { _clayDay = day; _clayNeed.Clear(); }
                int keep;
                if (!_clayNeed.TryGetValue(market.Town, out keep)) { keep = ClayKeep(market.Town, it); _clayNeed[market.Town] = keep; }
                if (keep <= 0) return 0;
                int shelf = market.ItemRoster != null ? market.ItemRoster.GetItemNumber(it) : 0;
                if (shelf > 0)
                {
                    if (folk) { if (_clayHeldFolk.Add(market)) _clayHeldFolkPcs += Math.Min(shelf, keep); }
                    else if (_clayHeldLord.Add(market)) _clayHeldLordPcs += Math.Min(shelf, keep);
                }
                return keep;
            }
            catch (Exception e) { Stumble("ClayHold", e); return 0; }
        }

        /// <summary>Zuzycie gliny warsztatow miasta (nieukrytych, linie z wyrobem-towarem; ta sama miara co CaravanBulk.ShopUse) w sztukach na dobe.</summary>
        private static float ClayUse(Town town, ItemCategory cat, out int shops)
        {
            shops = 0;
            var list = town.Workshops;
            if (cat == null || list == null) return 0f;
            float use = 0f;
            foreach (var w in list)
            {
                var type = w != null ? w.WorkshopType : null;
                if (type == null || type.IsHidden || type.Productions == null) continue;
                float one = 0f;
                foreach (var p in type.Productions)
                {
                    if (p.Inputs == null || p.Outputs == null) continue;      // Production to struct
                    bool goods = false;
                    foreach (var o in p.Outputs) if (o.Item1 != null && o.Item1.IsTradeGood) { goods = true; break; }
                    if (!goods) continue;
                    foreach (var i in p.Inputs) if (i.Item1 == cat) one += p.ConversionSpeed * i.Item2;
                }
                if (one > 0f) { use += one; shops++; }
            }
            return use;
        }

        private static int ClayKeep(Town town, ItemObject clay)
        {
            int shops;
            float use = ClayUse(town, clay.ItemCategory, out shops);
            if (use <= 0f) return 0;
            var s = Settings.Current;
            return (int)Math.Ceiling(use * Math.Max(1, s != null ? s.CaravanBulkStockDays : 10));
        }

        // ============================================================ start sesji i linia dnia
        /// <summary>OnSessionLaunched (TownCraftsBehavior): zachowanie warsztatow gry w kampanii, odczyt pomiaru z zapisu, linia startu.</summary>
        internal static void SessionStart()
        {
            try
            {
                try { _behOk = Campaign.Current != null && Campaign.Current.GetCampaignBehavior<WorkshopsCampaignBehavior>() != null; }
                catch (Exception e) { _behOk = false; Stumble("SessionStart(zachowanie)", e); }
                Resolve();
                Log.Info("Warsztaty codzienne (f7-browary): start sesji | K1 rzemioslo miasta po warsztatach (Town Crafts After Workshops) " + YN(AfterKey)
                         + " (latka " + YN(AfterHooked) + ", warsztaty gry w kampanii " + YN(_behOk) + ", czynne " + YN(CraftAfterOn) + ")"
                         + " | K2 popyt skory i plotna z odbioru (Cloth Demand From Offtake) " + YN(OffKey) + " (ksiega towarow " + YN(GoodsLedger.On) + "; pomiar z zapisu "
                         + (_loadedDays >= 0 ? "dob " + _loadedDays + ", miast " + _loadedTowns : "brak (pierwsza pelna doba = srednia)") + ")"
                         + " | K3 glina budow ponad potrzebe garncarzy (Build Clay For Potters) " + YN(ClayKey)
                         + " | obieg bez nowego przeplywu (kolejnosc, cena, czekajaca budowa) | potkniecia 0.");
            }
            catch (Exception e) { Stumble("SessionStart", e); }
        }

        private static string YN(bool b) { return b ? "tak" : "nie"; }
        private static readonly string[] Daily4 = { "brewery", "tannery", "pottery_shop", "linen_weavery" };

        /// <summary>Raz na dobe (TownCraftsBehavior, DailyTickEvent): odbior od ostatniej linii do sredniej (pierwsza doba sesji niepelna - odrzucona), linia
        /// "Warsztaty codzienne (f7-browary)". Liczniki doby zerowane tu.</summary>
        internal static void Daily()
        {
            long t0 = Stopwatch.GetTimestamp();
            int day = (int)CampaignTime.Now.ToDays;
            var cur = new Dictionary<Town, float[]>(_today); _today.Clear();
            var src = (long[,])_dSrc.Clone(); Array.Clear(_dSrc, 0, _dSrc.Length);
            int after = _dAfter, before = _dBefore; _dAfter = 0; _dBefore = 0;
            long heldLordPcs = _clayHeldLordPcs, heldFolkPcs = _clayHeldFolkPcs; int heldLord = _clayHeldLord.Count, heldFolk = _clayHeldFolk.Count;
            _clayHeldLord.Clear(); _clayHeldFolk.Clear(); _clayHeldLordPcs = 0; _clayHeldFolkPcs = 0;
            try
            {
                Resolve();
                bool on = OffOn;
                if (!on) _firstDay = true;
                else if (_firstDay) _firstDay = false;   // pierwsza doba sesji (od wczytania) niepelna - odrzucona
                else Fold(cur);
                if (AfterKey || OffKey || ClayKey) Line(day, cur, src, after, before, heldLord, heldLordPcs, heldFolk, heldFolkPcs, t0);   // wszystkie klucze wylaczone - bez linii (log jak dotad)
            }
            catch (Exception e) { Stumble("Daily", e); }
        }

        private static void Line(int day, Dictionary<Town, float[]> cur, long[,] src, int after, int before, int heldLord, long heldLordPcs, int heldFolk, long heldFolkPcs, long t0)
        {
            var sb = new StringBuilder(1400);
            // K2: dzis i srednia
            double tl = 0, tn = 0, al = 0, an = 0; int curTowns = 0, avgTowns = 0;
            foreach (var kv in cur) { if (kv.Value == null) continue; tl += kv.Value[0]; tn += kv.Value[1]; if (kv.Value[0] > 0f || kv.Value[1] > 0f) curTowns++; }
            foreach (var kv in _avg) { if (kv.Value == null) continue; al += kv.Value[0]; an += kv.Value[1]; avgTowns++; }
            // K3 i typy swiata: jeden przebieg po miastach (97 x kilka warsztatow)
            int potTowns = 0, potShops = 0; double potNeed = 0; long potKeep = 0, potShelf = 0;
            var types = new int[Daily4.Length];
            Items();
            var s = Settings.Current;
            int stockDays = Math.Max(1, s != null ? s.CaravanBulkStockDays : 10);
            foreach (var t in Town.AllTowns)
            {
                if (t == null || !t.IsTown) continue;
                try
                {
                    var ws = t.Workshops;
                    if (ws != null)
                        foreach (var w in ws)
                        {
                            var id = w != null && w.WorkshopType != null ? w.WorkshopType.StringId : null;
                            if (id == null) continue;
                            for (int k = 0; k < Daily4.Length; k++) if (id == Daily4[k]) { types[k]++; break; }
                        }
                    if (_clay != null)
                    {
                        int shops;
                        float use = ClayUse(t, _clay.ItemCategory, out shops);
                        if (use > 0f)
                        {
                            potTowns++; potShops += shops; potNeed += use; potKeep += (long)Math.Ceiling(use * stockDays);
                            if (t.Owner != null && t.Owner.ItemRoster != null) potShelf += t.Owner.ItemRoster.GetItemNumber(_clay);
                        }
                    }
                }
                catch (Exception e) { Stumble("Line(miasto)", e); }
            }
            int sumTypes = 0; foreach (var n in types) sumTypes += n;
            sb.Append("Warsztaty codzienne (f7-browary): dzien ").Append(day)
              .Append(" | K1 rzemioslo miasta po warsztatach (Town Crafts After Workshops) ").Append(YN(AfterKey)).Append(" (latka ").Append(YN(AfterHooked))
              .Append(", warsztaty gry ").Append(YN(_behOk)).Append("): miast-dob po warsztatach ").Append(after).Append(", przed warsztatami ").Append(before)
              .Append(" | K2 popyt skory i plotna z odbioru (Cloth Demand From Offtake) ").Append(YN(OffKey)).Append(" (ksiega towarow ").Append(YN(GoodsLedger.On))
              .Append("): dzis skora ").Append(tl.ToString("0", Inv)).Append(" [wojsko ").Append(src[0, 0]).Append(", naprawy ").Append(src[0, 1]).Append(", wsie ").Append(src[0, 2])
              .Append(", dwory ").Append(src[0, 3]).Append("], plotno ").Append(tn.ToString("0", Inv)).Append(" [wojsko ").Append(src[1, 0]).Append(", naprawy ").Append(src[1, 1])
              .Append(", wsie ").Append(src[1, 2]).Append(", dwory ").Append(src[1, 3]).Append("] szt. w ").Append(curTowns).Append(" miastach; srednia ok. 14 dob swiat: skora ")
              .Append(al.ToString("0.0", Inv)).Append(", plotno ").Append(an.ToString("0.0", Inv)).Append(" szt./d w ").Append(avgTowns).Append(" miastach (dob pomiaru ").Append(_days)
              .Append(_firstDay && OffOn ? ", doba sesji niepelna - odrzucona" : "").Append(") - dopisane do popytu ceny surowcow (115); zapas karawan i progi polki bez zmian")
              .Append(" | K3 glina budow ponad potrzebe garncarzy (Build Clay For Potters) ").Append(YN(ClayKey)).Append(": miast z garncarzami ").Append(potTowns)
              .Append(" (warsztatow ").Append(potShops).Append(", potrzeba ").Append(potNeed.ToString("0.0", Inv)).Append(" szt./d, prog ").Append(potKeep).Append(" szt.), glina na ich polkach ")
              .Append(potShelf).Append(" szt.; budowy zostawily gline - pan na ").Append(heldLord).Append(" targach (").Append(heldLordPcs).Append(" szt.), lud na ").Append(heldFolk)
              .Append(" targach (").Append(heldFolkPcs).Append(" szt.)")
              .Append(" | warsztaty codzienne swiata: ");
            for (int k = 0; k < Daily4.Length; k++) sb.Append(k > 0 ? ", " : "").Append(Daily4[k]).Append(' ').Append(types[k]);
            sb.Append(" (razem ").Append(sumTypes).Append(")")
              .Append(" | obieg: zloto i towary bez nowego przeplywu (K1 kolejnosc - warsztat placi miastu za wsad jak dotad, rzemioslo bez zlota; K2 tylko cena;"
                      + " K3 budowa czeka albo kupuje inny material, glina zostaje na polce)")
              .Append(" | zapis: obcietych miast ").Append(_saveCut)
              .Append(" | czas ").Append(((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency).ToString("0.00", Inv)).Append(" ms")
              .Append(" | potkniecia ").Append(_stumbles).Append(" (sesja ").Append(_stumblesAll).Append(").");
            _stumbles = 0;
            Log.Info(sb.ToString());
        }
    }
}
