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
    ///     RECENZJA (noc 18.10, uwaga 1): pod KS (miasto w mapie koszyka ZC4, lambda > 0) popyt domowy RawPrice to juz (1 - lambda) x gra + lambda x (lud Q + wsie QV
    ///     + wsad ZC5 + 1:1 swiata z dworami) x wartosc (StartCalibration.Zc4Demand przez HistoricalPrices.DemandPostfix) - zakupy wsi z koszyka i dwory bylyby
    ///     policzone drugi raz. Dlatego dwie pamieci na miasto: wojsko + naprawy (tego popyt KS nie liczy - zawsze w cenie) i wsie + dwory (w cenie x (1 - lambda)
    ///     miasta; poza KS x 1). Zapis "f7c2" (4 liczby na miasto); stary napis "f7c1" (testy tej nocy) - pomiar od zera.
    ///     RECENZJA (uwaga 4): pomiar idzie z ramek ksiegi towarow - bez logu albo bez ksiegi pomiar staje i cena skory i plotna wraca do starej reguly
    ///     (wygladzenie gry 15%/d, ok. tydzien); napisane w podpowiedzi MCM, linii startu i linii dnia (tak samo jak 1:1 Planu KS, ktore tez czyta ksiege);
    ///  K3 BuildClayForPotters - budowy biora gline z polki targu tylko ponad zuzycie warsztatow garncarskich miasta (linie towarowe z wsadem gliny: szybkosc x sztuk
    ///     wsadu, aktualna receptura) x CaravanBulkStockDays - ta sama regula co drewno K1 (BuildFunding.WoodHold); ponizej budowa kupuje inny material albo czeka.
    ///     RECENZJA (uwaga 2): ten sam prog dla przetworow KS (ZC5, para "glina -> garnki" w StartCalibration.TownCraftsDay - idzie PRZED warsztatami gry): wsad gliny
    ///     tylko ponad potrzebe garncarzy (zapas wsadu ZC5 + prog K3) - inaczej ukryte rece zjadaja gline do 5 sztuk przed cyklem garncarni notabla.
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
            _today.Clear(); _avg = new Dictionary<Town, float[]>(); _days = 0; _firstDay = true; _pending = null; _loadedDays = -1; _loadedTowns = 0; _saveCut = 0; _loadedOld = false;
            Array.Clear(_dSrc, 0, _dSrc.Length);
            _leather = null; _linen = null; _clay = null; _itemsTried = false;
            _clayDay = int.MinValue; _clayNeed.Clear(); _clayHeldLord.Clear(); _clayHeldFolk.Clear(); _clayHeldLordPcs = 0; _clayHeldFolkPcs = 0;
            _clayHeldKs.Clear(); _clayHeldKsPcs = 0;
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
        private const string OffTag = "f7c2";            // recenzja (uwaga 1): 4 liczby na miasto; "f7c1" (2 liczby, testy tej nocy) - pomiar od zera
        private const int OffSaveMax = 20000;            // straznik rozmiaru zapisu (97 miast x 4 liczby - ok. 5 000 znakow)
        private const int OffN = 4;                      // [0] skora wojsko+naprawy, [1] plotno wojsko+naprawy, [2] skora wsie+dwory, [3] plotno wsie+dwory
        private static ItemObject _leather, _linen, _clay; private static bool _itemsTried;
        private static readonly Dictionary<Town, float[]> _today = new Dictionary<Town, float[]>();   // miasto -> OffN od ostatniej linii (sztuki)
        // miasto -> OffN, srednia ok. 14 dob (sztuki na dobe). Recenzja: slownik podmieniany w calosci (Fold, Resolve) - UseOf moze isc z watkow AI (szacunek popytu,
        // jak Zc4Demand) i czyta tylko jedna, juz gotowa kopie; tablic w podmienionym slowniku nikt nie zmienia
        private static Dictionary<Town, float[]> _avg = new Dictionary<Town, float[]>();
        private static int _days; private static bool _firstDay = true;
        private static string _pending; private static int _loadedDays = -1, _loadedTowns, _saveCut; private static bool _loadedOld;
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
                    if (arr == null && !_today.TryGetValue(town, out arr)) { arr = new float[OffN]; _today[town] = arr; }
                    arr[src <= 1 ? g : 2 + g] += -net[i];   // recenzja (uwaga 1): wojsko i naprawy osobno od wsi i dworow (te pod KS sa juz w popycie koszyka)
                    _dSrc[g, src] += -net[i];
                }
            }
            catch (Exception e) { Stumble("FromFrame", e); }
        }

        /// <summary>RawPrice.UseDemand (popyt ceny surowcow): odbior skory albo plotna z polki miasta (srednia ok. 14 dob, sztuki rynku na dobe); 0 = klucz wylaczony,
        /// inny towar, pomiar jeszcze pusty albo blad (popyt jak dotad). Recenzja (uwaga 1): wojsko + naprawy zawsze; wsie + dwory x (1 - lambda) miasta z koszykiem
        /// ZC4 (VcWeight) - tam popyt domowy ma juz koszyk wsi (QV) i 1:1 dworow.</summary>
        internal static float UseOf(Town town, ItemObject item)
        {
            if (town == null || item == null || !OffOn) return 0f;
            try
            {
                if (!Items()) return 0f;
                int g = item == _leather ? 0 : item == _linen ? 1 : -1;
                if (g < 0) return 0f;
                float[] a;
                var avg = _avg;
                if (!avg.TryGetValue(town, out a) || a == null || a.Length != OffN) return 0f;
                float vc = a[2 + g];
                return vc > 0f ? a[g] + VcWeight(town) * vc : a[g];
            }
            catch (Exception e) { Stumble("UseOf", e); return 0f; }
        }

        /// <summary>Recenzja (uwaga 1): waga zakupow wsi i dworow w popycie ceny miasta - 1 poza KS; w miescie z koszykiem ZC4 (to samo pytanie co StartCalibration.ZTownOf,
        /// ktorym Zc4Demand podmienia popyt domowy: stan zastosowany, klucz, miasto w mapie, lambda > 0) 1 - lambda. Moze isc z watkow AI (sam odczyt mapy podmienianej
        /// w calosci - jak Zc4Demand). Blad - 1 (jak dotad w tej paczce).</summary>
        private static float VcWeight(Town town)
        {
            try
            {
                var zt = StartCalibration.ZTownFor(town);
                if (zt == null) return 1f;
                double lam = zt.Lam;
                if (!(lam > 0)) return 1f;
                return lam >= 1 ? 0f : (float)(1.0 - lam);
            }
            catch { return 1f; }
        }

        private static void Fold(Dictionary<Town, float[]> cur)
        {
            _days++;
            float k = Math.Min(_days, OffSpan);   // pierwsza pelna doba = srednia (k = 1), potem srednia wykladnicza z ok. 14 dob
            var old = _avg;
            var next = new Dictionary<Town, float[]>(old.Count + 8);
            var towns = new List<Town>(old.Keys);
            foreach (var t in cur.Keys) if (!old.ContainsKey(t)) towns.Add(t);
            foreach (var t in towns)
            {
                float[] o, c;
                var a = new float[OffN];                       // nowa tablica - stara zostaje nietknieta dla czytajacych stara kopie
                if (old.TryGetValue(t, out o) && o != null && o.Length == OffN) Array.Copy(o, a, OffN);
                cur.TryGetValue(t, out c);
                bool any = false;
                for (int g = 0; g < OffN; g++)
                {
                    float now = c != null && g < c.Length ? c[g] : 0f;
                    a[g] += (now - a[g]) / k;
                    if (a[g] < 0.001f) a[g] = 0f;
                    if (a[g] > 0f) any = true;
                }
                if (any) next[t] = a;
            }
            _avg = next;
        }

        /// <summary>SyncData (zapis): "f7c2|doby|miasto=skoraWN,plotnoWN,skoraWD,plotnoWD|..." (WN - wojsko i naprawy, WD - wsie i dwory) - null przy wylaczonym kluczu
        /// (klucz zapisu nie powstaje, zapis jak dotad).</summary>
        internal static string Export()
        {
            if (!OffKey) return null;
            try
            {
                Resolve();
                var sb = new StringBuilder(4096);
                sb.Append(OffTag).Append('|').Append(_days.ToString(Inv));
                foreach (var kv in _avg)   // watek glowny (zapis); slownik podmieniany w calosci, wiec iteracja po jednej kopii
                {
                    var t = kv.Key; var a = kv.Value;
                    string id = t != null && t.Settlement != null ? t.Settlement.StringId : null;
                    if (a == null || a.Length != OffN || string.IsNullOrEmpty(id) || id.IndexOf('|') >= 0 || id.IndexOf('=') >= 0 || id.IndexOf(',') >= 0) continue;
                    string part = id + "=" + a[0].ToString("0.###", Inv) + "," + a[1].ToString("0.###", Inv) + "," + a[2].ToString("0.###", Inv) + "," + a[3].ToString("0.###", Inv);
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
                if (parts.Length < 2 || parts[0] != OffTag || !int.TryParse(parts[1], NumberStyles.Integer, Inv, out d) || d < 0) { _loadedDays = 0; _loadedOld = parts.Length > 0 && parts[0] != OffTag; return; }   // recenzja: "f7c1" (2 liczby) - od zera
                var byId = new Dictionary<string, Town>(StringComparer.Ordinal);
                foreach (var t in Town.AllTowns) if (t != null && t.IsTown && t.Settlement != null) byId[t.Settlement.StringId] = t;
                int towns = 0;
                var next = new Dictionary<Town, float[]>(_avg);   // podmiana w calosci (UseOf z watkow AI)
                for (int i = 2; i < parts.Length; i++)
                {
                    int eq = parts[i].IndexOf('=');
                    if (eq <= 0) continue;
                    Town town;
                    if (!byId.TryGetValue(parts[i].Substring(0, eq), out town)) continue;
                    var v = parts[i].Substring(eq + 1).Split(',');
                    if (v.Length < OffN) continue;
                    var arr = new float[OffN]; bool good = true;
                    for (int g = 0; g < OffN && good; g++)
                    {
                        float x;
                        good = float.TryParse(v[g], NumberStyles.Float, Inv, out x) && x >= 0f && !float.IsInfinity(x);
                        if (good) arr[g] = x;
                    }
                    if (!good) continue;
                    next[town] = arr; towns++;
                }
                _avg = next;
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
        private static readonly HashSet<Settlement> _clayHeldKs = new HashSet<Settlement>();   // recenzja (uwaga 2): przetwory KS (ZC5) zostawily gline garncarzom
        private static long _clayHeldKsPcs;

        /// <summary>K3: ile gliny budowa zostawia na polce targu dla warsztatow garncarskich miasta - zuzycie dobowe linii towarowych z wsadem gliny (szybkosc x sztuk
        /// wsadu aktualnej receptury) x CaravanBulkStockDays; inny material, klucz wylaczony albo blad - 0 (jak dotad). folk - licznik robot ludu i gminy, inaczej pana.
        /// Licznik: raz na targ i dobe, sztuki zostawione = min(polka, prog).</summary>
        internal static int ClayHold(Settlement market, ItemObject it, bool folk) { return ClayHoldCore(market, it, folk ? 1 : 0); }

        /// <summary>Recenzja (uwaga 2): StartCalibration.TownCraftsDay (przetwory KS, para "glina -> garnki" - PRZED warsztatami gry): ten sam prog K3 dopisany do zapasu
        /// wsadu ZC5 (ukryte rece biora gline tylko ponad potrzebe garncarzy miasta). Klucz wylaczony, inny towar albo blad - 0 (zapas ZC5 jak dotad).</summary>
        internal static int ClayHoldKs(Settlement st, ItemObject it) { return ClayHoldCore(st, it, 2); }

        private static int ClayHoldCore(Settlement market, ItemObject it, int kind)
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
                    if (kind == 1) { if (_clayHeldFolk.Add(market)) _clayHeldFolkPcs += Math.Min(shelf, keep); }
                    else if (kind == 2) { if (_clayHeldKs.Add(market)) _clayHeldKsPcs += Math.Min(shelf, keep); }
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
            if (GoodsPaths.BulkClay) use /= Math.Max(1f, HistoricalPrices.CountScale(clay));   // ZC1 (kampania z glina w ladunkach): cykl bierze liczbe z receptury / czynnik ladunku
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
                         + (_loadedDays >= 0 ? "dob " + _loadedDays + ", miast " + _loadedTowns + (_loadedOld ? " - stary format, od zera" : "") : "brak (pierwsza pelna doba = srednia)") + ")"
                         + "; pod KS wsie i dwory x (1 - lambda) - juz w popycie koszyka ZC4; bez logu albo ksiegi towarow pomiar staje i cena skory i plotna wraca do starej reguly"
                         + " | K3 glina budow ponad potrzebe garncarzy (Build Clay For Potters) " + YN(ClayKey) + " (takze przetwory KS ZC5)"
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
            long heldKsPcs = _clayHeldKsPcs; int heldKs = _clayHeldKs.Count; _clayHeldKs.Clear(); _clayHeldKsPcs = 0;   // recenzja (uwaga 2): przetwory KS
            try
            {
                Resolve();
                bool on = OffOn;
                if (!on) _firstDay = true;
                else if (_firstDay) _firstDay = false;   // pierwsza doba sesji (od wczytania) niepelna - odrzucona
                else Fold(cur);
                if (AfterKey || OffKey || ClayKey) Line(day, cur, src, after, before, heldLord, heldLordPcs, heldFolk, heldFolkPcs, heldKs, heldKsPcs, t0);   // wszystkie klucze wylaczone - bez linii (log jak dotad)
            }
            catch (Exception e) { Stumble("Daily", e); }
        }

        private static void Line(int day, Dictionary<Town, float[]> cur, long[,] src, int after, int before, int heldLord, long heldLordPcs, int heldFolk, long heldFolkPcs,
                                 int heldKs, long heldKsPcs, long t0)
        {
            var sb = new StringBuilder(1800);
            // K2: dzis i srednia (recenzja, uwaga 1: wojsko + naprawy osobno od wsi + dworow; w popycie ceny wsie i dwory x (1 - lambda) w miastach z koszykiem ZC4)
            double tl = 0, tn = 0, al = 0, an = 0, al2 = 0, an2 = 0, pl = 0, pn = 0, kl = 0, kn = 0, sl = 0, sn = 0; int curTowns = 0, avgTowns = 0, ksTowns = 0;
            bool offOn = OffOn;
            foreach (var kv in cur)
            {
                var c = kv.Value; if (c == null || c.Length != OffN) continue;
                tl += c[0] + c[2]; tn += c[1] + c[3];
                if (c[0] + c[2] > 0f || c[1] + c[3] > 0f) curTowns++;
            }
            foreach (var kv in _avg)
            {
                var a = kv.Value; if (a == null || a.Length != OffN) continue;
                al += a[0]; an += a[1]; al2 += a[2]; an2 += a[3]; avgTowns++;
                float w = VcWeight(kv.Key);
                if (w < 1f) { ksTowns++; kl += a[2]; kn += a[3]; }   // miasta z koszykiem ZC4: ich wsie+dwory (sztuki/d)
                if (offOn)
                {
                    pl += a[0] + w * a[2]; pn += a[1] + w * a[3];        // to samo, co UseOf daje RawPrice (0 przy wylaczonym kluczu albo ksiedze)
                    sl += (1f - w) * a[2]; sn += (1f - w) * a[3];        // pominiete (juz w popycie koszyka ZC4); tozsamosc: wojsko+naprawy + wsie+dwory - pominiete = w popycie ceny
                }
            }
            // K3 i typy swiata: jeden przebieg po miastach (97 x kilka warsztatow)
            int potTowns = 0, potShops = 0; double potNeed = 0; long potKeep = 0, potShelf = 0;
            var types = new int[Daily4.Length];
            Items();
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
                            potTowns++; potShops += shops; potNeed += use; potKeep += ClayKeep(t, _clay);   // ten sam prog co ClayHold (z czynnikiem ladunku ZC1)
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
              .Append((al + al2).ToString("0.0", Inv)).Append(", plotno ").Append((an + an2).ToString("0.0", Inv)).Append(" szt./d w ").Append(avgTowns).Append(" miastach (dob pomiaru ").Append(_days)
              .Append(_firstDay && offOn ? ", doba sesji niepelna - odrzucona" : "").Append("); w tym wojsko+naprawy skora ").Append(al.ToString("0.0", Inv)).Append(", plotno ")
              .Append(an.ToString("0.0", Inv)).Append(", wsie+dwory skora ").Append(al2.ToString("0.0", Inv)).Append(", plotno ").Append(an2.ToString("0.0", Inv))
              .Append("; w popycie ceny skora ").Append(pl.ToString("0.0", Inv)).Append(", plotno ").Append(pn.ToString("0.0", Inv)).Append(" szt./d (wsie i dwory x (1 - lambda) w ")
              .Append(ksTowns).Append(" miastach z koszykiem ZC4 - tam juz w popycie koszyka: wsie QV, dwory 1:1; w tych miastach wsie+dwory skora ").Append(kl.ToString("0.0", Inv))
              .Append(", plotno ").Append(kn.ToString("0.0", Inv)).Append(", pominiete skora ").Append(sl.ToString("0.0", Inv)).Append(", plotno ").Append(sn.ToString("0.0", Inv))
              .Append(") - dopisane do popytu ceny surowcow (115); zapas karawan i progi polki bez zmian")
              .Append(offOn ? "" : "; bez logu albo ksiegi towarow pomiar stoi - cena skory i plotna wraca do starej reguly (ok. tydzien)")
              .Append(" | K3 glina budow ponad potrzebe garncarzy (Build Clay For Potters) ").Append(YN(ClayKey)).Append(": miast z garncarzami ").Append(potTowns)
              .Append(" (warsztatow ").Append(potShops).Append(", potrzeba ").Append(potNeed.ToString("0.0", Inv)).Append(" szt./d, prog ").Append(potKeep).Append(" szt.), glina na ich polkach ")
              .Append(potShelf).Append(" szt.; budowy zostawily gline - pan na ").Append(heldLord).Append(" targach (").Append(heldLordPcs).Append(" szt.), lud na ").Append(heldFolk)
              .Append(" targach (").Append(heldFolkPcs).Append(" szt.), przetwory KS (ZC5) na ").Append(heldKs).Append(" targach (").Append(heldKsPcs).Append(" szt.)")
              .Append(" | warsztaty codzienne swiata: ");
            for (int k = 0; k < Daily4.Length; k++) sb.Append(k > 0 ? ", " : "").Append(Daily4[k]).Append(' ').Append(types[k]);
            sb.Append(" (razem ").Append(sumTypes).Append(")")
              .Append(" | obieg: zloto i towary bez nowego przeplywu (K1 kolejnosc - warsztat placi miastu za wsad jak dotad, rzemioslo bez zlota; K2 tylko cena;"
                      + " K3 budowa czeka albo kupuje inny material, przetwory KS robia mniej garnkow, glina zostaje na polce)")
              .Append(" | zapis: obcietych miast ").Append(_saveCut)
              .Append(" | czas ").Append(((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency).ToString("0.00", Inv)).Append(" ms")
              .Append(" | potkniecia ").Append(_stumbles).Append(" (sesja ").Append(_stumblesAll).Append(").");
            _stumbles = 0;
            Log.Info(sb.ToString());
        }
    }
}
