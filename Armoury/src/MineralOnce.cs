using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// MINERAL RAZ (Jeff 05.10: "podwojny mineral BK - usunac").
    /// BannerKings PopulationManager.GetProductions sklada liste produkcji wsi z VillageType.Productions i dokleja do niej
    /// mineraly z MineralData wsi gorniczej (takze przy kopalni BK poziomu 0, z waga 0). Glowny mineral wsi (ruda w
    /// iron_mine, sol w salt_mine, glina w clay_mine, srebro w silver_mine; w silver_mine bywa tez marmur i ruda zlota)
    /// jest wiec na liscie DWA razy, a latka BK na VillageGoodProductionCampaignBehavior.TickGoodProduction wola aktywny
    /// model (vanilla przez NavalDLC - wagi z listy nie czyta) dla KAZDEJ pozycji. Wies dostawala mineral dwa razy na
    /// dobe (pomiar 05.10: 7.4 ladunku rudy na kopiaca wies przy 3.7 z modelu), a magazyn wsi (Village.GetWarehouseCapacity)
    /// i wielkosc taboru (GetIdealVillagerPartySize) licza z VillageType.Productions raz.
    /// Latka: postfiks na GetProductions. Kolejne wystapienia tego samego przedmiotu znikaja, zostaje pierwsze; jego waga
    /// (Item2) przejmuje wagi zdjetych wpisow, zeby suma wag listy zostala ta sama (czytaja ja model BK - dzis tylko do
    /// okna wsi - i BKSettlementBehavior.HandleExcessFood; tick BK wagi nie czyta). Listy z BK nie ruszamy: przy
    /// powtorzeniu budujemy nowa i podmieniamy wynik.
    /// WYDOBYCIE BEZ ZMIAN (Jeff 06.10: o poziomach produkcji decyduje projekt ekonomii, nie skutek uboczny poprawki):
    /// zapamietujemy dla wsi, ile powtorzen kazdego przedmiotu zdjelismy, a MaterialLaw.ProdPostfix mnozy wynik modelu
    /// przez (1 + liczba zdjetych powtorzen) - Times(). Wies dostaje wiec dokladnie tyle rudy, soli, gliny i srebra co
    /// dotad, ale JEDNYM wpisem: mnoznik, magazyn wsi, tabor i ksiega (OreLedger: "wsie dopisaly" = "model") licza to samo.
    /// Mnoznika rudy w ustawieniach (MineOutputMultiplier) nie zmieniamy. Wylacznik wraca do stanu BK w obie strony:
    /// przy wylaczonym postfiks listy nie rusza, a Times() oddaje 1.
    /// KOLEJNOSC (przeglad 06.10): tick BK sklada liste (GetProductions) tuz przed pytaniem modelu, wiec w ticku mnoznik jest
    /// zawsze zgodny z lista. Magazyn wsi i tabor pytaja model BEZ listy i dostaja mnoznik z ostatniego jej zlozenia: po
    /// wczytaniu gry (Reset) licza wiec jeszcze po staremu, dopoki BK pierwszy raz nie zlozy listy tej wsi (zwykle pierwsza doba).
    /// Nowa kampania: dane mineralow BK (MineralData) powstaja dopiero przy pierwszej dobowej aktualizacji ludnosci, wiec ticki
    /// startowe nie maja powtorzen, Times() = 1 i zapas startowy oraz podatek poczatkowy wsi wychodza jak dotad.
    /// Zdjety wpis oddaje MaterialLaw.ProdPostfix - latka wpina sie tylko wtedy, gdy ten postfix siedzi w modelach produkcji.
    /// </summary>
    internal static class MineralOnce
    {
        private static readonly object Gate = new object();
        // przedmiot -> wsie (obiekt danych ludnosci BK = jedna wies), ktorym od ostatniej linii logu zdjeto powtorzenie
        private static readonly Dictionary<ItemObject, HashSet<object>> _seen = new Dictionary<ItemObject, HashSet<object>>();
        // wies -> przedmiot -> ile powtorzen zdjeto przy OSTATNIM zlozeniu jej listy (stan biezacy, nie licznik doby)
        private static readonly Dictionary<Village, Dictionary<ItemObject, int>> _dup = new Dictionary<Village, Dictionary<ItemObject, int>>();
        private static MethodInfo _settlementOf;     // PopulationData -> Settlement (wlasciwosc BK)
        private static int _lists, _removed, _stumbles;
        private static bool _patched;                // czesc mineralow czynna (MaterialLaw w modelach produkcji - oddaje zdjety wpis mnoznikiem)

        // --- T8 (noc 08/09.10): FILTR KLIMATU UPRAW - papirus tylko na goracym poludniu ---
        // BK dopisuje papirus (0.5) do listy typu farmy zboza (BKVillageTypes: WheatFarm), wiec rodzi go kazda farma, takze na Polnocy
        // i za Murem (audyt 02 L8: ok. 62 z 80 sztuk dziennie tam, gdzie nie rosnie). Papirus to roslina goracych bagien (Nil, Sycylia).
        // Wlasna bramka CropClimateFilter, sprawdzana PRZED bramka mineralow - wylaczenie jednej nie gasi drugiej (krytyka S17).
        // Waga zdjetego wpisu przechodzi na pierwszy wpis listy (glowny plon farmy) - suma wag listy bez zmian, jak przy dublu mineralu.
        // Id kultur sprawdzone w ROT-Content/ModuleData/spcultures.xml (aserai = Dorne). Gdy zadnej z nich nie ma w grze - nie filtrujemy.
        private static readonly HashSet<string> HotSouth = new HashSet<string>(StringComparer.Ordinal)
        { "aserai", "ghiscari", "qartheen", "volantine", "lyseni", "myrish", "tyroshi", "valyrian", "summer" };
        private static readonly HashSet<Village> _papyrusOff = new HashSet<Village>();   // wsie, ktorym od ostatniej linii zdjeto papirus
        private static int _cropStumbles, _hotKnown = -1;                               // _hotKnown: ile kultur goracego poludnia zna gra (-1 = nie liczone)
        private static bool _wired;                  // postfiks wpiety (filtr klimatu dziala takze bez czesci mineralow)

        internal static void Reset()
        {
            lock (Gate) { _seen.Clear(); _dup.Clear(); _lists = 0; _removed = 0; _stumbles = 0; _papyrusOff.Clear(); _cropStumbles = 0; _hotKnown = -1; }
        }

        private static bool IsPapyrus(ItemObject it)
        {
            return it != null && string.Equals(it.StringId, "Papyrus", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>T8: lista bez papirusu, gdy kultura wsi nie jest z goracego poludnia; waga zdjetego wpisu na pierwszy pozostaly wpis
        /// (glowny plon). Bez papirusu na liscie, bez kultury albo gdy gra nie zna zadnej kultury poludnia - ta sama lista (bez alokacji).</summary>
        private static List<(ItemObject, float)> CropFilter(Village v, List<(ItemObject, float)> list)
        {
            int n = list.Count, hit = -1;
            for (int i = 0; i < n; i++) if (IsPapyrus(list[i].Item1)) { hit = i; break; }
            if (hit < 0) return list;
            var c = v.Settlement != null ? v.Settlement.Culture : null;
            string cid = c != null ? c.StringId : null;
            if (string.IsNullOrEmpty(cid) || HotSouth.Contains(cid)) return list;
            if (_hotKnown < 0)
            {
                int k = 0;
                try
                {
                    var om = TaleWorlds.ObjectSystem.MBObjectManager.Instance;
                    if (om != null) foreach (var id in HotSouth) if (om.GetObject<CultureObject>(id) != null) k++;
                }
                catch { k = 0; }
                _hotKnown = k;
            }
            if (_hotKnown == 0) return list;            // brak id kultur (inna mapa) - nie filtrujemy
            var res = new List<(ItemObject, float)>(n);
            float moved = 0f;
            for (int i = 0; i < n; i++)
            {
                var e = list[i];
                if (IsPapyrus(e.Item1)) { moved += e.Item2; continue; }
                res.Add(e);
            }
            if (res.Count == 0) return list;            // sam papirus na liscie - nie ruszamy
            res[0] = (res[0].Item1, res[0].Item2 + moved);
            lock (Gate) _papyrusOff.Add(v);
            return res;
        }

        /// <summary>Ile razy liczyc wynik modelu produkcji dla tego przedmiotu w tej wsi: 1 + powtorzenia zdjete z listy BK.
        /// 1, gdy latka nie jest wpieta, jest wylaczona albo wies nie miala powtorzen.</summary>
        internal static int Times(Village v, ItemObject item)
        {
            if (!_patched || v == null || item == null) return 1;
            var s = Settings.Current;
            if (s == null || !s.MineralsCountedOnce) return 1;
            lock (Gate)
            {
                if (_dup.Count == 0) return 1;
                Dictionary<ItemObject, int> d; int c;
                return _dup.TryGetValue(v, out d) && d.TryGetValue(item, out c) ? 1 + c : 1;
            }
        }

        private static Village VillageOf(object data)
        {
            try
            {
                if (data == null || _settlementOf == null) return null;
                var st = _settlementOf.Invoke(data, null) as Settlement;
                return st != null ? st.Village : null;
            }
            catch { return null; }
        }

        public static void Postfix(object __0, ref List<(ItemObject, float)> __result)
        {
            Village v = null;
            try
            {
                var s = Settings.Current;
                if (s == null) return;
                bool crop = s.CropClimateFilter, mineral = _patched && s.MineralsCountedOnce;   // T8: dwie osobne bramki
                if (!crop && !mineral) return;
                var list = __result;
                if (list == null) return;
                v = VillageOf(__0);
                if (v == null) return;                     // bez wsi nie oddamy mnoznika modelem - lista zostaje, jak dal BK (wydobycie bez zmian)
                if (crop)
                {
                    // T8: filtr na wejsciu, przed petla powtorzen; potkniecie filtra - lista jak z BK, czesc mineralow liczy dalej
                    try { list = CropFilter(v, list); }
                    catch (Exception cex)
                    {
                        int ck;
                        lock (Gate) ck = ++_cropStumbles;
                        if (ck <= 3) Log.Error("MineralOnce.CropFilter", cex);
                        list = __result;
                    }
                }
                if (!mineral) { __result = list; return; }
                int n = list.Count;
                List<(ItemObject, float)> res = null;      // nowa lista dopiero przy pierwszym powtorzeniu - zwykla wies kosztuje tylko odczyt osady (ok. 0.1 mikrosekundy)
                Dictionary<ItemObject, int> extra = null;  // przedmiot -> ile powtorzen zdjeto w tym zlozeniu listy
                for (int i = 0; i < n; i++)
                {
                    var e = list[i];
                    int first = -1;
                    if (e.Item1 != null)
                    {
                        // dopoki nie bylo powtorzenia, indeksy nowej listy sa takie same jak starej
                        var src = res ?? list;
                        int upto = res != null ? res.Count : i;
                        for (int j = 0; j < upto; j++)
                            if (ReferenceEquals(src[j].Item1, e.Item1)) { first = j; break; }
                    }
                    if (first < 0) { if (res != null) res.Add(e); continue; }
                    if (res == null)
                    {
                        res = new List<(ItemObject, float)>(n);
                        for (int j = 0; j < i; j++) res.Add(list[j]);
                    }
                    var keep = res[first];
                    res[first] = (keep.Item1, keep.Item2 + e.Item2);     // waga zdjetego wpisu przechodzi na pierwszy
                    if (extra == null) extra = new Dictionary<ItemObject, int>();
                    int c; extra.TryGetValue(e.Item1, out c); extra[e.Item1] = c + 1;
                    Note(__0, e.Item1);
                }
                // stan wsi i podmiana listy ida RAZEM: Times() ma oddawac mnoznik tylko wtedy, gdy lista naprawde jest bez powtorzen
                lock (Gate)
                {
                    if (extra != null) { _dup[v] = extra; _lists++; }
                    else if (_dup.Count > 0) _dup.Remove(v);
                }
                __result = res ?? list;                    // list = lista BK albo lista po filtrze klimatu (T8)
            }
            catch (Exception ex)
            {
                // potkniecie na jednej wsi: jej lista zostaje taka, jaka dal BK (z powtorzeniem), wiec mnoznik tej wsi musi zniknac
                int k;
                lock (Gate) { k = ++_stumbles; if (v != null) _dup.Remove(v); }
                if (k <= 3) Log.Error("MineralOnce.Postfix", ex);
            }
        }

        private static void Note(object data, ItemObject item)
        {
            lock (Gate)
            {
                _removed++;
                HashSet<object> set;
                if (!_seen.TryGetValue(item, out set)) { set = new HashSet<object>(); _seen[item] = set; }
                if (data != null) set.Add(data);
            }
        }

        /// <summary>Raz na dobe: ktorym przedmiotom i w ilu wsiach zdjeto powtorzenie (dowod, ze latka dziala). Tylko log.</summary>
        internal static void Daily()
        {
            var s = Settings.Current;
            if (s == null) return;
            string what;
            lock (Gate)
            {
                if (!_patched) what = "latka NIE wpieta (brak BannerKings, inna postac GetProductions albo MaterialLaw poza modelami produkcji - patrz linia MineralOnce przy starcie gry) - BK dopisuje mineral po swojemu";
                else if (!s.MineralsCountedOnce) what = "LATKA WYLACZONA w ustawieniach - BK dopisuje mineral wsi gorniczej dwa razy na dobe";
                else if (_removed == 0) what = "zadnego powtorzenia na listach produkcji wsi (BK nie pytal o listy albo niczego nie dubluje)";
                else
                {
                    var parts = new List<string>();
                    foreach (var kv in _seen) parts.Add((kv.Key.StringId ?? "?") + " " + kv.Value.Count);
                    parts.Sort(StringComparer.Ordinal);
                    // wsie liczone raz (zbior), a "razem" to wszystkie wywolania doby - tick produkcji, ale tez okna i podpowiedzi BK
                    what = "powtorzenia zdjete z list produkcji BK (przedmiot i liczba wsi): " + string.Join(", ", parts.ToArray())
                           + "; razem " + _removed + " zdjetych wpisow w " + _lists + " wywolaniach listy"
                           + "; wsi z mnoznikiem modelu teraz " + _dup.Count + " (wydobycie bez zmian: jeden wpis liczony tyle razy, ile bylo powtorzen)";
                }
                what += "; potkniecia od wczytania kampanii " + _stumbles + ".";
                _seen.Clear(); _lists = 0; _removed = 0;
            }
            Log.Info("Mineraly (dubel BK): dzien " + ((int)CampaignTime.Now.ToDays - 1) + " - " + what);
            // T8: osobna linia filtra klimatu upraw (nie doklejamy do linii mineralow - parsery czytaja jej poczatek)
            string crop;
            lock (Gate)
            {
                if (!_wired) crop = "NIEAKTYWNY - latka GetProductions nie wpieta (linia MineralOnce przy starcie gry); papirus jak w BK";
                else if (!s.CropClimateFilter) crop = "WYLACZONY w ustawieniach (Crop Climate Filter) - papirus na kazdej farmie zboza jak w BK";
                else if (_hotKnown == 0) crop = "CZYNNY, ale gra nie zna zadnej kultury goracego poludnia - nic nie zdjete";
                else crop = "CZYNNY: papirus zdjety z listy " + _papyrusOff.Count + " wsi poza goracym poludniem (waga na glowny plon farmy)";
                crop += "; potkniecia od wczytania " + _cropStumbles + ".";
                _papyrusOff.Clear();
            }
            Log.Info("Papirus wedlug klimatu (T8): dzien " + ((int)CampaignTime.Now.ToDays - 1) + " - " + crop);
        }

        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var t = QuartermasterLaw.FindType("BannerKings.Managers.PopulationManager");
                var tData = QuartermasterLaw.FindType("BannerKings.Managers.Populations.PopulationData");
                var m = t == null ? null : (tData != null ? AccessTools.Method(t, "GetProductions", new[] { tData }) : AccessTools.Method(t, "GetProductions"));
                if (m == null) { Log.Info("MineralOnce: BRAK BannerKings PopulationManager.GetProductions - listy produkcji wsi bez zmian."); return; }
                if (m.ReturnType != typeof(List<(ItemObject, float)>))
                {
                    Log.Info("MineralOnce: GetProductions zwraca " + m.ReturnType + " zamiast listy par (przedmiot, waga) - latka NIE wpieta, BK dopisuje mineral dwa razy.");
                    return;
                }
                // wies z danych ludnosci BK: bez niej nie oddamy zdjetego wpisu mnoznikiem modelu, wiec wtedy listy nie ruszamy wcale
                MethodInfo get = null;
                if (tData != null)
                {
                    get = AccessTools.PropertyGetter(tData, "Settlement") ?? AccessTools.PropertyGetter(tData, "settlement");
                    if (get != null && (get.IsStatic || get.GetParameters().Length != 0 || !typeof(Settlement).IsAssignableFrom(get.ReturnType))) get = null;
                }
                if (get == null)
                {
                    Log.Info("MineralOnce: BRAK wlasciwosci Settlement w danych ludnosci BannerKings - latka NIE wpieta, BK dopisuje mineral dwa razy (wydobycie bez zmian).");
                    return;
                }
                // zdjety wpis oddaje MaterialLaw.ProdPostfix (Times). Gdyby nie wpial sie w zaden model produkcji, lista bez powtorzen
                // znaczylaby polowe mineralu - wtedy listy nie ruszamy. Dlatego MaterialLaw.ApplyAll musi isc PRZED ta latka (SubModuleMain).
                // T8: wtedy postfiks i tak wchodzi (filtr klimatu upraw ma wlasna bramke), ale czesc mineralow spi (_patched = false)
                bool mineralOk = MaterialLaw.ProdModels > 0;
                if (!mineralOk)
                    Log.Info("MineralOnce: MaterialLaw nie wpial sie w zaden model produkcji wsi (zdjetego wpisu nie byloby czym oddac) - czesc mineralow NIEAKTYWNA, BK dopisuje mineral dwa razy (wydobycie bez zmian); postfiks wpiety tylko dla filtra klimatu upraw (T8).");
                _settlementOf = get;
                h.Patch(m, postfix: new HarmonyMethod(typeof(MineralOnce), nameof(Postfix)) { priority = Priority.Last });
                _wired = true;
                _patched = mineralOk;
                if (!mineralOk) return;
                // stanu wylacznika tu nie podajemy: ustawienia z MCM wchodza dopiero przy starcie kampanii (McmSettings.Apply),
                // a postfiks czyta wylacznik przy kazdym wywolaniu - stan pokazuje dzienna linia "Mineraly (dubel BK)"
                Log.Info("MineralOnce: lista produkcji wsi BK bez powtorzen, wydobycie bez zmian (zdjety wpis oddaje mnoznik modelu) - latka wpieta (wylacznik Minerals Counted Once czytany przy kazdym wywolaniu; stan w dziennej linii \"Mineraly (dubel BK)\").");
            }
            catch (Exception e) { Log.Error("MineralOnce.ApplyAll", e); }
        }
    }
}
