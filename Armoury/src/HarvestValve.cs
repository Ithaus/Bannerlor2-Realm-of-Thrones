using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// E6/W3 (paczka 221, PLAN 6.4 + krok 6.3; projekt etapu 6 v2 rozdz. 3 "E6/W3", R5/R6; krytyka K1-03, KB W1-ksiega) - JEDEN REGULATOR WSI.
    /// Jedna zasada: wies robi tyle, ile swiat zuzywa. Dotad towar bez zbytu rosl na polkach i znikal przez gnicie BK (RawNoRot 174.3: 135 szt./d w nicosc,
    /// glownie sol, drewno, glina), a towaru chcianego nie przybywalo (krok x1.3 K13 132 - v2 tutaj, nie w W1).
    ///
    /// Towary: kazdy towar handlowy wsi poza zywnoscia i zwierzetami (IsTradeGood, !IsFood, !IsAnimal - ten sam zbior co gnicie BK; konie to K4).
    /// Pomiar (wlasne liczniki, niezalezne od LogEnabled i od ksiegi 146):
    ///  - produkcja wsi = przyrost magazynu wsi w ticku produkcji (prefiks Priority.First przed BK i postfiks Priority.Last po lesie wsi 126 na
    ///    VillageGoodProductionCampaignBehavior.TickGoodProduction) - dokladnie to, co gra albo BK dopisaly (takze las wsi); drewno lasu, ktore
    ///    poszlo prosto do dymarki kopalni (R1), dopisuje VillageWoodlot (MadeOffRoster) - to produkcja i od razu zuzycie;
    ///  - skasowane = to, co zdjal RawNoRot (gnicie 174.3) przed pierwsza decyzja - nie jest zuzyciem;
    ///  - stos swiata = polki osad, magazyny wsi, schowki, wszystkie partie (tabory, karawany, lordowie, gracz) i wozy kupcow w drodze (TradeCarts),
    ///    liczony raz na 28 dob (na granicy kubelka), nie co dobe;
    ///  - zuzycie okna = produkcja wsi - skasowane - (stos teraz - stos 56 dob temu), najmniej 0 - to, co swiat zuzyl ponad inne zrodla (warsztaty).
    /// Decyzja co 28 dob na oknie 56 dob (dwa kubelki), pierwsza 56 dob po starcie pomiaru (nowa kampania - d56); wczesniej mnozniki 1 i gnicie 174.3:
    ///  - w dol: zuzycie < 90% produkcji i stos > ValveStockDaysHigh (60) dni zuzycia -> mnoznik = 0.95 x zuzycie/produkcja bazowa okna (przy mnozniku 1; cel ponizej zuzycia -
    ///    stos maleje), najwyzej x0.5 na decyzje, podloga 0.1; towar bez odbiorcy - prog w dniach produkcji zamiast zuzycia (schodzi do podlogi);
    ///  - w gore: zuzycie >= 98% produkcji i stos < ValveStockDaysLow (30) dni zuzycia -> x1.3, najpierw najwyzej do 1 (po cieciu), potem najwyzej
    ///    VillageOutputStep (1.3) ponad baze - tylko produkcja, bez mnoznika popytu (D6-4);
    ///  - pomiedzy - bez zmian (martwa strefa: stos zostaje miedzy 30 a 60 dniami zuzycia, bez zapadki i kolysania).
    /// Mnoznik dziala w MaterialLaw.ProdPostfix (czynnik x suma, jak inne czynniki; przed W2 i sufitem dymarki R1) i w lesie wsi (VillageWoodlot.PiecesPerDay -
    /// tick i magazyn). Od pierwszej decyzji RawNoRot nie kasuje towarow (stawka 0; wyroby gracza i sztandary kasuje dalej jak BK) - nic w nicosc.
    /// Stan w zapisie (arm_harvestvalve): mnozniki, kubelki produkcji i skasowanych, stosy na granicach kubelkow, doby; brak klucza (stary zapis) - pomiar
    /// od pierwszej doby po wczytaniu. Wylaczony klucz - stan czyszczony (mnozniki 1, gnicie 174.3), wlaczenie zaczyna pomiar od nowa.
    /// Koszt: dwa przebiegi malego magazynu wsi w jej ticku produkcji, odczyt slownika w modelu wsi, pelny spis stosu raz na 28 dob.
    ///
    /// E6/K4 (paczka 220, PLAN 6.12, R9; projekt etapu 6 v2 rozdz. 3 "E6/K4", krytyka K1-02) - HODOWLA WEDLUG POPYTU w tym samym regulatorze i tym samym stanie zapisu.
    /// Konie wedlug kategorii (HorseBook.Cat: zwykly, bojowy, szlachetny, juczny; egzotyczne - nie) to "towary" o id "#kon:N" (bez przedmiotu):
    ///  - produkcja kategorii = przyrost koni w magazynie wsi w tym samym ticku produkcji (stadniny wsi, z bojowymi z przesuniecia) + przyrost koni w skrytce
    ///    zamku w BK BKBuildingsBehavior.RunStuds (hodowla zamkow - zrodlo z przyczyna; prefiks i postfiks tylko licza);
    ///  - stos kategorii = konie na targu: polki i magazyny osad, schowki osad, wozy kupcow w drodze (bez taborow - kon w partii jest w uzyciu); ten sam spis co 28 dob;
    ///  - zuzycie okna = produkcja - (stos teraz - stos 56 dob temu), najmniej 0 - zakupy wojska i karawan, mieszczanie, odkup po zgonach, netto po zwrotach na targ;
    ///  - (b) w gore CZYNNE: regula towarow (zuzycie >= 98% produkcji i stos < ValveStockDaysLow dni) -> x1.3, najwyzej VillageOutputStep;
    ///  - (a) w dol: ponizej mnoznika 1 (baza gry) CZYNNE przy kluczu HorseBreedingCutLive (K1-3a krok 2 (c), decyzja Jeffa 12.10 15:15 "hodowla - TAK": ta sama
    ///    regula co towary - cel 0.95 x zuzycie / produkcja bazowa, najwyzej x0.5 na decyzje, podloga 0.1; powrot w gore x1.3 na decyzje najwyzej do x1);
    ///    bez klucza NA SUCHO - linia podaje "mnoznik by byl" i ile sztuk na dobe by ubylo, czynne tylko cofniecie wlasnego kroku (z x1.3 najwyzej do x1),
    ///    a mnozniki ponizej x1 z czasu wlaczonego klucza wracaja do x1 (HorseKeyState);
    ///  - (c) przesuniecie CZYNNE: bojowe chciane (regula w gore) i zwykle w nadmiarze (stos > ValveStockDaysHigh dni) -> udzial +12.5 pp (najwyzej 25%) produkcji
    ///    zwyklych kazdej stadniny z bojowymi w liscie typu wsi idzie na bojowe tej listy (wedlug wag listy); zwykle ponizej ValveStockDaysLow dni albo bojowe w nadmiarze
    ///    -> -12.5 pp; pomiedzy bez zmian. Te same sztuki: w ticku stadniny wynik zwyklych x (1 - udzial) po wszystkich czynnikach (MaterialLaw.ProdPostfix ->
    ///    HorseStep), zdjete sztuki dopisane bojowym w postfiksie ticku (z OnItemProduced, jak produkcja gry); poza tickiem (podpowiedz, magazyn wsi) model
    ///    pokazuje sume bez przesuniecia.
    /// Mnoznik kategorii w MaterialLaw.ProdPostfix (czynnik x suma, po W3, przed W2). Klucz HorseBreedingByDemand (dziala tylko z HarvestValve); wylaczony - konie
    /// wyczyszczone (mnozniki 1, przesuniecie 0); wlaczony w biegu regulatora - pomiar koni od pelnego okna. Linie "Hodowla (220)" (doba, decyzja) i raz na sesje
    /// "Przeglad stadnin (220)" (wsie z konmi wedlug krainy i typu - dane do decyzji o typach wsi; paczka typow wsi nie zmienia).
    ///
    /// N5 (poprawka 5 po biegu rocznym 364 doby, decyzja Jeffa 13.10) - dwie reguly decyzji towarow (konie bez zmian), kazda z wlasnym kluczem:
    ///  - N5-A ValveRaiseWhileShort: surowiec z tabeli karawan, ktorego nie ma w wiecej niz ValveShortTowns miastach, ma sufit kroku w gore ValveShortCeiling (4)
    ///    zamiast VillageOutputStep (1.3) - warunki kroku bez zmian (zuzycie >= 98% produkcji, stos < ValveStockDaysLow dni). Bieg roczny: len x1.30 od doby 85 do
    ///    konca, zuzycie/produkcja 0.99-1.01, stos 12-13 dni, 58-70 miast bez lnu. Wylaczony: sufit zwykly, a mnoznik ponad max(sufit, 1.3) wraca do niego od razu.
    ///  - N5-D ValveStepFollowsUse: krok ponad baze wraca do zuzycia bez czekania na stos > ValveStockDaysHigh dni. Bieg roczny: drewno x1.30 od doby 113,
    ///    zuzycie/produkcja 0.79-0.90 przez 8 decyzji, stos 17 488 -> 152 862. Przyciecie (m > 1, poprzednia decyzja nie byla krokiem w gore - pelne okno przy
    ///    nowym mnozniku): stos < dolny prog i zuzycie < 90% produkcji -> 1.05 x zuzycie / produkcja bazowa okna; stos >= dolny prog i zuzycie < 98% -> zuzycie /
    ///    produkcja bazowa; nie ponizej 1, tylko gdy zmiana >= 2%. Krok w gore od bazy wzwyz: x (1 + brak stosu do dolnego progu), w granicach 1.05-1.3, i nie
    ///    w decyzji zaraz po przycieciu. Cel przyciecia zalezy tylko od zuzycia i produkcji bazowej (powtorzony daje ten sam mnoznik), wiec bez kolysania.
    /// </summary>
    internal static class HarvestValve
    {
        private const int Bucket = 28;             // doby kubelka; decyzja co kubelek, okno = dwa kubelki
        private const float CutBelow = 0.90f, StepAbove = 0.98f, CutTarget = 0.95f, CutMaxStep = 0.5f, Floor = 0.1f, UpStep = 1.3f;
        // N5: najwyzszy mnoznik w stanie i w zapisie (sufit braku N5-A przycinany do niego); przyciecie kroku N5-D: zapas na odbudowe stosu ponizej dolnego progu,
        // najmniejsza zmiana warta decyzji; najmniejszy krok w gore od bazy wzwyz
        private const float MaxM = 8f, TrimSpare = 1.05f, TrimMin = 0.02f, StepMin = 1.05f;

        private sealed class Good
        {
            public string Id;
            public ItemObject Item;
            public float M = 1f;                   // mnoznik produkcji wsi (1 = jak model)
            public float MA = 1f;                  // mnoznik, przy ktorym powstala produkcja kubelka starszego (P0); biezacy P1 powstal przy M
            public double P0, P1;                  // produkcja wsi: kubelek starszy / biezacy (szt.)
            public double D0, D1;                  // skasowane przez RawNoRot: kubelek starszy / biezacy (szt.)
            public long SA = -1, SB = -1;          // stos swiata na poczatku kubelka starszego / biezacego (-1 = nieznany)
            public int Dir;                        // ostatnia decyzja: -1 w dol, 0 bez zmian, 1 w gore
            public int Flips;                      // zmiany kierunku w dwoch kolejnych decyzjach (od startu pomiaru)
        }

        private sealed class RefEq : IEqualityComparer<ItemObject>
        {
            public bool Equals(ItemObject a, ItemObject b) { return ReferenceEquals(a, b); }
            public int GetHashCode(ItemObject o) { return RuntimeHelpers.GetHashCode(o); }
        }

        internal static readonly TextObject Text = new TextObject("{=!}Armoury: village output follows what the world uses up");
        private static readonly Dictionary<string, Good> _goods = new Dictionary<string, Good>(StringComparer.Ordinal);
        private static readonly Dictionary<ItemObject, Good> _byItem = new Dictionary<ItemObject, Good>(new RefEq());
        private static volatile Dictionary<ItemObject, float> _factors;   // mnozniki != 1 (podmieniane w calosci - model wsi czyta bez zamka)
        private static int _start = -1, _bucketStart = -1, _buckets, _decisions, _firstDecisionDay = -1;
        private static bool _wired, _dirty, _sessionLine;
        private static int _stumbles;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        // tick produkcji wsi (tylko watek glowny)
        private static Village _tv;
        private static readonly Dictionary<ItemObject, int> _before = new Dictionary<ItemObject, int>(new RefEq());
        private static readonly Dictionary<ItemObject, int> _after = new Dictionary<ItemObject, int>(new RefEq());

        // E6/K4 (220): konie wedlug kategorii w tym samym regulatorze
        private const int HCats = 4;                                 // zwykly, bojowy, szlachetny, juczny (HorseBook.Cat 0..3; egzotyczne - nie)
        private const string HPrefix = "#kon:";
        private static readonly string[] HNames = { "zwykly", "bojowy", "szlachetny", "juczny" };
        private const float ShiftMax = 0.25f, ShiftStep = 0.125f;   // przesuniecie zwykle -> bojowe: najwyzej 25% produkcji zwyklych stadniny, krok na decyzje
        internal static readonly TextObject HorseText = new TextObject("{=!}Armoury: horse breeding follows what the world uses up");
        private static readonly TextObject ShiftText = new TextObject("{=!}Armoury: mares put to war horses");
        private sealed class Stud { public ItemObject[] War; public float[] W; public float Sum; }
        private static volatile float[] _hm;                         // mnozniki czynne kategorii (null = wszystkie 1) - podmieniane w calosci, model wsi czyta bez zamka
        private static volatile float _shift;                        // udzial produkcji zwyklych stadniny przesuwany na bojowe (0..ShiftMax)
        private static bool _hHas;                                   // stan koni istnieje (towary "#kon:N" w _goods)
        private static long[] _hNow = new long[HCats];                // stos koni na targu z ostatniego spisu
        private static readonly Dictionary<VillageType, Stud> _studs = new Dictionary<VillageType, Stud>();   // typ wsi -> bojowe listy (null: bez bojowych albo bez zwyklych)
        private static readonly long[] _hb = new long[HCats], _ha = new long[HCats], _cb = new long[HCats], _ca = new long[HCats];
        private static bool _hCounted, _studsWired, _hLine, _review;
        private static int _tvThread;
        private static float _shiftAcc;                              // sztuki zdjete ze zwyklych w biezacym ticku stadniny (HorseStep -> ShiftOut)
        private static Town _ct;                                     // BK RunStuds w toku (zamek)
        private static readonly long[] _hDay = new long[HCats];      // produkcja doby wedlug kategorii (wsie + zamki)
        private static long _castleDay, _shiftAddDay;
        private static double _shiftTakeDay;

        /// <summary>Regulator liczy i decyduje: klucz wlaczony i tick produkcji wsi wpiety (bez niego nie ma pomiaru produkcji).</summary>
        internal static bool On { get { var s = Settings.Current; return s != null && s.HarvestValve && _wired; } }

        /// <summary>Po pierwszej decyzji: RawNoRot nie kasuje towarow (stawka 0).</summary>
        internal static bool Active { get { return On && _decisions > 0; } }

        /// <summary>E6/K4 (220): klucz hodowli wedlug popytu.</summary>
        private static bool HorseKey { get { var s = Settings.Current; return s != null && s.HorseBreedingByDemand; } }

        /// <summary>K1-3a krok 2 (c): ciecie hodowli ponizej bazy gry czynne (klucz HorseBreedingCutLive; dziala tylko z hodowla wedlug popytu).</summary>
        private static bool HorseCutLive { get { var s = Settings.Current; return s != null && s.HorseBreedingCutLive; } }

        /// <summary>E6/K4 (220): hodowla wedlug popytu dziala - klucz i regulator wsi.</summary>
        internal static bool HorseOn { get { return On && HorseKey; } }

        internal static void Reset()
        {
            Clear();
            _stumbles = 0; _errSites.Clear();
            _tv = null; _before.Clear(); _after.Clear();
            _dirty = false; _sessionLine = false;
            _studs.Clear(); _ct = null; _hCounted = false; _shiftAcc = 0f; _hLine = false; _review = false;   // E6/K4 (220)
            ClearDay();
        }

        private static void Clear()
        {
            _goods.Clear(); _byItem.Clear(); _factors = null;
            _start = -1; _bucketStart = -1; _buckets = 0; _decisions = 0; _firstDecisionDay = -1;
            _hm = null; _shift = 0f; _hHas = false; _hNow = new long[HCats];   // E6/K4 (220): konie razem z regulatorem
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_errSites.Add(where)) Log.Error("HarvestValve." + where, e);
        }

        private static bool Eligible(ItemObject it) { return it != null && it.IsTradeGood && !it.IsFood && !it.IsAnimal; }

        /// <summary>Mnoznik produkcji wsi dla towaru (1 = bez zmian). Model wsi i las wsi; czytane z kazdego watku.</summary>
        internal static float Factor(ItemObject item)
        {
            var d = _factors;
            float f;
            return d != null && item != null && On && d.TryGetValue(item, out f) ? f : 1f;
        }

        private static Good Get(ItemObject item)
        {
            Good g;
            if (_byItem.TryGetValue(item, out g)) return g;
            string id = item.StringId;
            if (string.IsNullOrEmpty(id)) return null;
            if (!_goods.TryGetValue(id, out g))
            {
                // spis stosu obejmuje kazdy towar z zakresu obecny w swiecie - towar bez stanu nie byl w zadnym spisie, wiec jego stos tam byl 0
                g = new Good { Id = id, SB = _bucketStart >= 0 ? 0 : -1, SA = _buckets >= 1 ? 0 : -1 };
                _goods[id] = g;
            }
            g.Item = item;
            _byItem[item] = g;
            return g;
        }

        /// <summary>Przedmioty ze stanu (zapis) i slownik mnoznikow - po wczytaniu, gdy menedzer obiektow jest gotowy.</summary>
        private static void Rebuild()
        {
            RebuildHorse();                                 // E6/K4 (220): konie bez przedmiotow - przed menedzerem obiektow (HorseKeyState w Daily idzie przed Rebuild)
            var mgr = MBObjectManager.Instance;
            if (mgr == null) { _dirty = true; return; }
            _byItem.Clear();
            var f = new Dictionary<ItemObject, float>(new RefEq());
            foreach (var g in _goods.Values)
            {
                if (IsH(g.Id)) continue;                                  // E6/K4 (220): konie - kategoria, bez przedmiotu (RebuildHorse)
                if (g.Item == null) { try { g.Item = mgr.GetObject<ItemObject>(g.Id); } catch { g.Item = null; } }
                if (g.Item == null) continue;
                _byItem[g.Item] = g;
                if (g.M != 1f) f[g.Item] = g.M;
            }
            _factors = f.Count > 0 ? f : null;
            _dirty = false;
        }

        // ------------------------------------------------------------ pomiar produkcji
        /// <summary>Prefiks TickGoodProduction(Village, bool), Priority.First (przed BK): stan magazynu wsi (towary z zakresu, suma po przedmiocie).
        /// Rozdanie startowe nowej kampanii (initialProductionForTowns) i doby przed startem pomiaru - bez liczenia.</summary>
        public static void TickPrefix(Village __0, bool __1)
        {
            _tv = null; _shiftAcc = 0f; _hCounted = false;
            if (__1 || __0 == null || _start < 0 || !On) return;
            try
            {
                var r = __0.Settlement != null ? __0.Settlement.ItemRoster : null;
                if (r == null) return;
                Count(r, _before);
                if (_hHas && HorseKey) { CountH(r, _hb); _hCounted = true; }   // E6/K4 (220): konie wsi wedlug kategorii przed tickiem
                _tv = __0;
                _tvThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            }
            catch (Exception e) { _tv = null; _hCounted = false; Stumble("TickPrefix", e); }
        }

        /// <summary>Postfiks tego samego ticku (Priority.Last - po lesie wsi 126 i dymarce R1): przyrost magazynu wsi = produkcja wsi.</summary>
        public static void TickPostfix(Village __0)
        {
            var v = _tv;
            _tv = null;
            float acc = _shiftAcc;
            bool hc = _hCounted;
            _shiftAcc = 0f; _hCounted = false;
            if (v == null || !ReferenceEquals(v, __0)) return;
            // E6/K4 (220): sztuki zdjete ze zwyklych w tym ticku (HorseStep) -> bojowe listy stadniny; PRZED pomiarem przyrostu (to produkcja bojowych)
            if (acc > 0f) { try { ShiftOut(v, acc); } catch (Exception e) { Stumble("ShiftOut", e); } }
            try
            {
                Count(v.Settlement.ItemRoster, _after);
                foreach (var kv in _after)
                {
                    int b;
                    _before.TryGetValue(kv.Key, out b);
                    if (kv.Value > b) { var g = Get(kv.Key); if (g != null) g.P1 += kv.Value - b; }
                }
            }
            catch (Exception e) { Stumble("TickPostfix", e); }
            if (!hc) return;
            try
            {
                // E6/K4 (220): przyrost koni wsi wedlug kategorii = produkcja stadniny (z bojowymi z przesuniecia)
                CountH(v.Settlement.ItemRoster, _ha);
                for (int c = 0; c < HCats; c++)
                {
                    long d = _ha[c] - _hb[c];
                    if (d <= 0) continue;
                    var g = HFind(c);
                    if (g != null) g.P1 += d;
                    _hDay[c] += d;
                }
            }
            catch (Exception e) { Stumble("TickPostfix.konie", e); }
        }

        public static Exception TickFinalizer(Exception __exception) { _tv = null; _shiftAcc = 0f; _hCounted = false; return __exception; }

        private static void Count(ItemRoster r, Dictionary<ItemObject, int> into)
        {
            into.Clear();
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                var it = el.EquipmentElement.Item;
                if (el.Amount == 0 || !Eligible(it)) continue;
                int n;
                into.TryGetValue(it, out n);
                into[it] = n + el.Amount;
            }
        }

        /// <summary>Produkcja wsi poza magazynem wsi (drewno lasu prosto do dymarki kopalni, R1) - w kubelku produkcji; stos jej nie widzi, wiec to od razu zuzycie.</summary>
        internal static void MadeOffRoster(ItemObject item, int n)
        {
            if (n <= 0 || _start < 0 || !On || !Eligible(item)) return;
            try { var g = Get(item); if (g != null) g.P1 += n; }
            catch (Exception e) { Stumble("MadeOffRoster", e); }
        }

        /// <summary>RawNoRot.Prefix: sztuki skasowane przez gnicie (przed pierwsza decyzja) - nie sa zuzyciem.</summary>
        internal static void NoteDecay(ItemObject item, int n)
        {
            if (n <= 0 || _start < 0 || !On || !Eligible(item)) return;
            try { var g = Get(item); if (g != null) g.D1 += n; }
            catch (Exception e) { Stumble("NoteDecay", e); }
        }

        // ------------------------------------------------------------ stos swiata
        private static Dictionary<ItemObject, long> Census()
        {
            var c = new Dictionary<ItemObject, long>(new RefEq());
            Action<ItemRoster> add = r =>
            {
                if (r == null) return;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    var it = el.EquipmentElement.Item;
                    if (el.Amount <= 0 || !Eligible(it)) continue;
                    long n;
                    c.TryGetValue(it, out n);
                    c[it] = n + el.Amount;
                }
            };
            // E6/K4 (220): konie na targu wedlug kategorii - polki i magazyny osad, schowki osad, wozy w drodze (taborow nie liczymy: kon w partii jest w uzyciu)
            var h = new long[HCats];
            Action<ItemRoster> addH = r =>
            {
                if (r == null) return;
                for (int i = 0; i < r.Count; i++)
                {
                    var el = r.GetElementCopyAtIndex(i);
                    if (el.Amount <= 0) continue;
                    int hc = HCat(el.EquipmentElement.Item);
                    if (hc >= 0) h[hc] += el.Amount;
                }
            };
            foreach (var st in Settlement.All)
            {
                if (st == null) continue;
                try
                {
                    add(st.ItemRoster); addH(st.ItemRoster);
                    if (st.Stash != null && !ReferenceEquals(st.Stash, st.ItemRoster)) { add(st.Stash); addH(st.Stash); }
                }
                catch (Exception e) { Stumble("Census.osada", e); }
            }
            foreach (var mp in MobileParty.All)
            {
                if (mp == null) continue;
                try { add(mp.ItemRoster); }
                catch (Exception e) { Stumble("Census.partia", e); }
            }
            try
            {
                TradeCarts.ForEachInTransit((it, n) =>
                {
                    if (n <= 0) return;
                    if (Eligible(it)) { long k; c.TryGetValue(it, out k); c[it] = k + n; }
                    else { int hc = HCat(it); if (hc >= 0) h[hc] += n; }
                });
            }
            catch (Exception e) { Stumble("Census.wozy", e); }
            // P3/Z1 (TRIAZ-3 O2-1 P1): surowiec w wozach kupcow w drodze (MaterialCarts) - czesc stosu swiata jak wozy kupcow R3
            try { MaterialCarts.ForEachInTransit((it, n) => { if (n > 0 && Eligible(it)) { long k; c.TryGetValue(it, out k); c[it] = k + n; } }); }
            catch (Exception e) { Stumble("Census.wozy surowcow", e); }
            _hNow = h;
            return c;
        }

        // ------------------------------------------------------------ doba
        /// <summary>Raz na dobe (ArmouryBehavior.OnDailyTick): start pomiaru, granica kubelka (spis stosu, decyzja od drugiego kubelka), linia.</summary>
        internal static void Daily()
        {
            int today = (int)CampaignTime.Now.ToDays;
            int day = today - 1;
            try
            {
                HorseKeyState(day);                         // E6/K4 (220): wlaczenie / wylaczenie hodowli wedlug popytu
                if (!On)
                {
                    if (_start >= 0 || _goods.Count > 0)
                    {
                        Clear();
                        Log.Info("Regulator wsi (221): dzien " + day + " - WYLACZONY" + (_wired ? " (Harvest Valve = off)" : " (BRAK latki ticku produkcji wsi)")
                                 + " - stan wyczyszczony, mnozniki 1, gnicie BK jak 174.3; wlaczenie zacznie pomiar od nowa.");
                    }
                    else if (!_sessionLine)
                        Log.Info("Regulator wsi (221): dzien " + day + " - " + (_wired ? "WYLACZONY (Harvest Valve = off)" : "NIEAKTYWNY - BRAK latki ticku produkcji wsi") + " - produkcja wsi jak model, gnicie BK jak 174.3.");
                    _sessionLine = true;
                    return;
                }
                if (_dirty) Rebuild();
                if (_start < 0)
                {
                    Begin(today);
                    _sessionLine = true;
                }
                else if (today - _bucketStart >= Bucket) { Close(today); _sessionLine = true; }
                else if (!_sessionLine) { Status(day, today); _sessionLine = true; }
                HorseDaily(day, today);                     // E6/K4 (220): linia doby "Hodowla (220)" i raz na sesje przeglad stadnin
            }
            catch (Exception e) { Stumble("Daily", e); }
        }

        /// <summary>
        /// GL-1 (P1, projekt "gospodarka od ludzi" 3.5 i ryzyko 5): plon wsi od ludzi wlaczony, wylaczony albo z g typow policzonym od nowa - okna 56 dob
        /// porownywalyby nowa produkcje ze starym zuzyciem. Stan czyszczony tak samo jak przy wylaczeniu klucza (mnozniki 1, gnicie BK jak 174.3, konie
        /// razem z regulatorem), start pomiaru w nastepnej dobie (Daily -> Begin). false = regulator bez stanu albo z pomiarem zaczetym w tej dobie (nic do czyszczenia). Watek glowny.
        /// </summary>
        internal static bool RestartMeasurement(string why)
        {
            try
            {
                if (_start < 0 && _goods.Count == 0) return false;
                int today = (int)CampaignTime.Now.ToDays, day = today - 1;
                if (_start >= today && _decisions == 0) return false;      // pomiar zaczety w tej dobie (nowa kampania: Begin przed kalibracja reguly) - nic do czyszczenia
                int dec = _decisions, from = _start;
                Clear();
                Log.Info("Regulator wsi (221): dzien " + day + " - POMIAR OD NOWA (" + why + "): stan wyczyszczony (pomiar od dnia " + from + ", decyzji " + dec
                         + "), mnozniki 1, gnicie BK jak 174.3; start pomiaru w nastepnej dobie, pierwsza decyzja po " + (2 * Bucket) + " dobach.");
                return true;
            }
            catch (Exception e) { Stumble("RestartMeasurement", e); return false; }
        }

        private static void Begin(int today)
        {
            _start = today; _bucketStart = today; _buckets = 0; _decisions = 0; _firstDecisionDay = -1;
            var c = Census();
            foreach (var kv in c) { var g = Get(kv.Key); if (g != null) g.SB = kv.Value; }
            Log.Info("Regulator wsi (221): dzien " + (today - 1) + " - CZYNNY, start pomiaru: spis stosu swiata " + c.Count + " towarow (bez zywnosci i zwierzat); okno "
                     + (2 * Bucket) + " dob, decyzja co " + Bucket + " dob, pierwsza za " + (2 * Bucket) + " dob; do niej mnozniki 1 i gnicie BK jak 174.3; potkniecia " + _stumbles + ".");
            if (HorseKey)
            {
                // E6/K4 (220): konie startuja z regulatorem - stos na targu z tego samego spisu
                for (int k = 0; k < HCats; k++) HAdd(k, _hNow[k]);
                _hHas = true;
                Log.Info("Hodowla (220): dzien " + (today - 1) + " - start pomiaru koni z regulatorem wsi: stos na targu (polki, magazyny wsi, schowki, wozy) zwykly/bojowy/szlachetny/juczny "
                         + Row(_hNow) + "; pierwsza decyzja za " + (2 * Bucket) + " dob, do niej mnozniki 1 i bez przesuniecia.");
            }
        }

        private static void Status(int day, int today)
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("Regulator wsi (221): dzien ").Append(day).Append(" - CZYNNY (stan z zapisu), doba pomiaru ").Append(today - _start)
              .Append(", decyzji ").Append(_decisions).Append(", nastepna za ").Append(Math.Max(0, Bucket - (today - _bucketStart))).Append(" dob")
              .Append(_buckets < 1 ? " (pierwsza - po " + (2 * Bucket) + " dobach pomiaru)" : "")
              .Append("; gnicie BK: ").Append(_decisions > 0 ? "stawka 0" : "jak 174.3").Append("; mnozniki inne niz 1:");
            int n = 0;
            var ids = new List<string>(_goods.Keys); ids.Sort(StringComparer.Ordinal);
            foreach (var id in ids) { var g = _goods[id]; if (g.M != 1f && !IsH(id)) { sb.Append(' ').Append(id).Append(" x").Append(g.M.ToString("0.00", inv)).Append(';'); n++; } }
            if (n == 0) sb.Append(" brak;");
            sb.Append(" potkniecia ").Append(_stumbles).Append('.');
            Log.Info(sb.ToString());
        }

        private static void Close(int today)
        {
            var inv = CultureInfo.InvariantCulture;
            var c = Census();
            foreach (var kv in c) Get(kv.Key);              // towar pierwszy raz w spisie: stos na poprzednich granicach 0 (Get)
            bool decide = _buckets >= 1;
            var s = Settings.Current;
            float ceil = s != null ? Math.Max(1f, Math.Min(2f, s.VillageOutputStep)) : UpStep;
            int low = s != null ? Math.Max(1, s.ValveStockDaysLow) : 30;
            int high = s != null ? Math.Max(low + 1, s.ValveStockDaysHigh) : 60;
            double win = 2.0 * Bucket;
            int down = 0, up = 0, keep = 0, flips = 0;
            // P3/Z2 (TRIAZ-3 bieg roczny, O2-2 "Poprawka ostateczna" pkt 2) - BEZPIECZNIK: towar, ktorego nie ma w wiecej niz ValveShortTowns miastach, nie jest
            // nadprodukowany - zly jest dowoz. Stos swiata liczy tez zapas, ktory do zuzycia nie dochodzi (bieg roczny d364: zamki 3 654 + juki karawan 2 582 +
            // cztery miasta 5 200 = 79% z 14 517 lup), wiec regulator cial wszystkie kopalnie (d196 x0.73, d308 x0.61) przy 34-50 miastach bez zelaza.
            // Liczone tutaj (raz na 28 dob), nie z pola CaravanBulk (przeliczane po regulatorze, po wczytaniu 0): towary z tabeli surowcow karawan, puste polki miast.
            Dictionary<ItemObject, int> emptyOf = null;
            int shortAt = s != null ? Math.Max(0, s.ValveShortTowns) : 15, holdCut = 0, holdBack = 0;
            bool holdKey = s != null && s.ValveHoldWhileShort, holdReady = false;
            // N5-A / N5-D (poprawka 5 po biegu rocznym): sufit kroku przy braku w miastach i krok ponad baze wedlug zuzycia. Tabela pustych polek (holdReady) liczona
            // takze dla samego N5-A; bezpiecznik P3/Z2 dziala dalej tylko przy swoim kluczu (holdKey)
            bool raiseKey = s != null && s.ValveRaiseWhileShort, followKey = s != null && s.ValveStepFollowsUse;
            float shortCeil = s != null ? Math.Max(1f, Math.Min(MaxM, s.ValveShortCeiling)) : ceil;
            int n5Raised = 0, n5Trim = 0, n5TrimWait = 0, n5UpWait = 0, n5Back = 0;
            var n5Above = new List<string>();
            if (decide && (holdKey || raiseKey))
            {
                try
                {
                    var bulk = CaravanBulk.Items();
                    if (bulk.Count > 0)
                    {
                        emptyOf = new Dictionary<ItemObject, int>(new RefEq());
                        foreach (var it in bulk) emptyOf[it] = 0;
                        foreach (var t in Town.AllTowns)
                        {
                            if (t == null || !t.IsTown || t.Owner == null || t.Owner.ItemRoster == null) continue;
                            foreach (var it in bulk) if (t.Owner.ItemRoster.GetItemNumber(it) <= 0) emptyOf[it]++;
                        }
                        holdReady = true;
                    }
                }
                catch (Exception e) { Stumble("Close.bezpiecznik", e); holdReady = false; }
            }
            var parts = new List<string>();
            var ids = new List<string>(_goods.Keys); ids.Sort(StringComparer.Ordinal);
            var f = new Dictionary<ItemObject, float>(new RefEq());
            foreach (var id in ids)
            {
                if (IsH(id)) continue;                      // E6/K4 (220): konie - osobno (HorseClose), stos z ich spisu
                var g = _goods[id];
                float mBucket = g.M;                        // mnoznik kubelka biezacego (P1) - po przesunieciu kubelek starszy
                long now = 0;
                if (g.Item != null) c.TryGetValue(g.Item, out now);
                if (decide && g.SA >= 0 && g.Item != null)
                {
                    try
                    {
                        double P = g.P0 + g.P1, D = g.D0 + g.D1;
                        if (P > 0.0 || g.M < 1f)
                        {
                            double C = Math.Max(0.0, P - D - (now - g.SA));
                            double cDay = C / win, pDay = P / win;
                            float m0 = g.M, m1 = m0;
                            int dir = 0;
                            int emptyTowns = -1;
                            bool shortNow = holdReady && emptyOf.TryGetValue(g.Item, out emptyTowns) && emptyTowns > shortAt;   // P3/Z2: brak w miastach
                            string holdNote = null;
                            if (holdKey && shortNow && m0 < 1f)
                            {
                                // P3/Z2 (c): powrot do bazy - cofniecie wlasnego ciecia krokiem x1.3, nigdy ponad baze, bez warunku stosu < ValveStockDaysLow
                                // (ten warunek jest nieosiagalny, dopoki zapas lezy tam, skad nie wyjezdza)
                                m1 = Math.Min(1f, m0 * UpStep);
                                if (m1 > m0 + 1e-4f) dir = 1; else m1 = m0;
                                holdBack++;
                                holdNote = ", powrot do bazy: bez towaru " + emptyTowns + " miast";
                            }
                            else if (!raiseKey && m0 > Math.Max(ceil, UpStep) + 1e-4f)
                            {
                                // N5-A wylaczone: mnoznik ponad zwykly sufit (osiagalny tylko krokiem N5-A) wraca do niego od razu - stan sprzed paczki
                                m1 = Math.Max(ceil, UpStep); dir = -1; n5Back++;
                                holdNote = ", powrot do zwyklego sufitu (N5-A wylaczone)";
                            }
                            else if (P > 0.0 && C < CutBelow * P && (cDay > 0.0 ? now > high * cDay : now > high * pDay))
                            {
                                // recenzja 221: cel = 0.95 x zuzycie / produkcja BAZOWA okna (przy mnozniku 1) - kubelek starszy powstal przy MA, biezacy przy m0;
                                // m0 x C / P przy oknie z dwoma mnoznikami (po cieciu) cial drugi raz towar juz ustawiony na 0.95 x zuzycia (np. 0.57 -> 0.41)
                                double baseP = (g.MA > 0f ? g.P0 / g.MA : g.P0) + (m0 > 0f ? g.P1 / m0 : g.P1);
                                float target = (float)(CutTarget * C / (baseP > 0.0 ? baseP : P));
                                m1 = Math.Max(Floor, Math.Max(target, m0 * CutMaxStep));
                                // P3/Z2 (b): przy braku w miastach ciecie nie schodzi ponizej bazy (cofniecie wlasnego kroku x1.3 do x1.00 dziala dalej)
                                if (holdKey && shortNow && m1 < 1f) { m1 = 1f; holdCut++; holdNote = ", ciecie wstrzymane: bez towaru " + emptyTowns + " miast"; }
                                if (m1 < m0 - 1e-4f) dir = -1; else m1 = m0;
                            }
                            else if (followKey && m0 > 1f + 1e-4f && cDay > 0.0 && (now < low * cDay ? C < CutBelow * P : C < StepAbove * P))
                            {
                                // N5-D: krok ponad baze wraca do zuzycia - bez czekania na stos > ValveStockDaysHigh dni (warunek ciecia 221 wyzej). Dopiero po pelnym
                                // oknie przy nowym mnozniku (poprzednia decyzja nie byla krokiem w gore): okno z dwoma mnoznikami po kroku ma tez wozy i magazyny
                                // dopelniane nowa produkcja - to nie jest jeszcze nadprodukcja
                                if (g.Dir > 0) { n5TrimWait++; holdNote = ", przyciecie kroku czeka na pelne okno (N5-D)"; }
                                else
                                {
                                    double bp = (g.MA > 0f ? g.P0 / g.MA : g.P0) + (m0 > 0f ? g.P1 / m0 : g.P1);
                                    if (!(bp > 0.0)) bp = P;
                                    bool spare = now < low * cDay;   // stos ponizej dolnego progu - zostaje 5% ponad zuzycie na jego odbudowe
                                    float target = (float)((spare ? TrimSpare : 1.0) * C / bp);
                                    m1 = Math.Max(1f, Math.Min(m0, target));
                                    if (m1 < m0 * (1f - TrimMin)) { dir = -1; n5Trim++; holdNote = ", przyciecie kroku do zuzycia (N5-D" + (spare ? ", z 5% na odbudowe stosu" : "") + ")"; }
                                    else m1 = m0;
                                }
                            }
                            else if (cDay > 0.0 && C >= StepAbove * P && now < low * cDay)
                            {
                                if (followKey && m0 >= 1f && g.Dir < 0) { n5UpWait++; holdNote = ", krok w gore czeka po przycieciu (N5-D)"; }   // bez zmiany kierunku w kolejnych decyzjach
                                else
                                {
                                    // N5-A: sufit kroku przy braku w miastach; N5-D: od bazy wzwyz krok proporcjonalny do braku stosu (x1.05 przy dolnym progu, x1.3 przy pustym)
                                    bool raise = raiseKey && shortNow && shortCeil > ceil;
                                    float step = UpStep;
                                    if (followKey && m0 >= 1f) step = Math.Max(StepMin, Math.Min(UpStep, 1f + (float)((low * cDay - now) / (low * cDay))));
                                    m1 = Math.Min(m0 < 1f ? 1f : (raise ? shortCeil : ceil), m0 * step);
                                    if (m1 > m0 + 1e-4f)
                                    {
                                        dir = 1;
                                        if (raise && m1 > ceil + 1e-4f) { n5Raised++; holdNote = ", ponad zwykly sufit (N5-A): bez towaru " + emptyTowns + " miast"; }
                                    }
                                    else m1 = m0;
                                }
                            }
                            if (dir != 0 && g.Dir == -dir) { g.Flips++; flips++; }
                            g.Dir = dir;
                            g.M = m1;
                            if (dir < 0) down++; else if (dir > 0) up++; else keep++;
                            parts.Add(id + ": x" + m0.ToString("0.00", inv) + (dir != 0 ? " -> x" + m1.ToString("0.00", inv) : "")
                                      + ", zuzycie/produkcja " + (P > 0.0 ? (C / P).ToString("0.00", inv) : "-") + " (" + C.ToString("0", inv) + "/" + P.ToString("0", inv)
                                      + (D > 0.0 ? ", skasowane " + D.ToString("0", inv) : "") + "), stos " + now + " = "
                                      + (cDay > 0.0 ? (now / cDay).ToString("0", inv) + " dni zuzycia" : "bez odbiorcy") + ", "
                                      + (dir < 0 ? "w dol" : dir > 0 ? "w gore" : "bez zmian") + (holdNote ?? ""));
                        }
                    }
                    catch (Exception e) { Stumble("Decide", e); }
                }
                g.P0 = g.P1; g.P1 = 0.0; g.D0 = g.D1; g.D1 = 0.0; g.SA = g.SB; g.SB = now; g.MA = mBucket;
                if (g.Item != null && g.M != 1f) f[g.Item] = g.M;
                if (g.M > ceil + 1e-4f) n5Above.Add(id + " x" + g.M.ToString("0.00", inv));   // N5-A: towary ponad zwyklym sufitem (do linii decyzji)
            }
            _factors = f.Count > 0 ? f : null;
            string hl = null;
            try { hl = HorseClose(decide, low, high, ceil, win, today - 1, _decisions + 1); }   // E6/K4 (220): decyzja kategorii koni i przesuniecia
            catch (Exception e) { Stumble("HorseClose", e); }
            _buckets++; _bucketStart = today;
            int day = today - 1;
            if (!decide)
            {
                Log.Info("Regulator wsi (221): dzien " + day + " - CZYNNY, koniec pierwszego kubelka (doba pomiaru " + (today - _start) + "), spis stosu " + c.Count
                         + " towarow; pierwsza decyzja za " + Bucket + " dob; gnicie BK jak 174.3; potkniecia " + _stumbles + ".");
                if (hl != null) Log.Info(hl);
                return;
            }
            _decisions++;
            if (_firstDecisionDay < 0) _firstDecisionDay = day;
            var sb = new StringBuilder();
            sb.Append("Regulator wsi (221): dzien ").Append(day).Append(" - CZYNNY, decyzja ").Append(_decisions).Append(" (doba pomiaru ").Append(today - _start)
              .Append(", okno ").Append((int)win).Append(" dob, stos ").Append(low).Append('-').Append(high).Append(" dni zuzycia, sufit x").Append(ceil.ToString("0.00", inv)).Append("): [")
              .Append(string.Join("; ", parts.ToArray())).Append("]; w dol ").Append(down).Append(", w gore ").Append(up).Append(", bez zmian ").Append(keep)
              .Append("; zmiany kierunku w kolejnych decyzjach ").Append(flips).Append("; skasowano 0 (od doby ").Append(_firstDecisionDay)
              .Append(" gnicie BK stawka 0)");
            // P3/Z2: stan bezpiecznika i puste miasta wedlug towaru z tabeli surowcow karawan (tylko towary regulatora)
            sb.Append("; bezpiecznik braku w miastach (P3/Z2): ");
            if (!holdKey) sb.Append("WYLACZONY (Valve Hold While Short)");
            else if (!holdReady) sb.Append("NIECZYNNY (tabela surowcow karawan niegotowa)");
            else
            {
                sb.Append("CZYNNY prog ").Append(shortAt).Append(" miast, ciecia wstrzymane ").Append(holdCut).Append(", powroty do bazy ").Append(holdBack).Append(", miast bez towaru [");
                bool fe = true;
                foreach (var kv in emptyOf)
                {
                    if (kv.Key == null || !_byItem.ContainsKey(kv.Key)) continue;
                    sb.Append(fe ? "" : ", ").Append(kv.Key.StringId).Append(' ').Append(kv.Value); fe = false;
                }
                sb.Append(fe ? "-]" : "]");
            }
            // N5: stan dwoch kluczy i liczniki tej decyzji (noty przy towarach - w nawiasie kwadratowym wyzej)
            sb.Append("; N5-A sufit przy braku w miastach: ");
            if (!raiseKey) sb.Append("WYLACZONY (Valve Raise While Short), powroty do zwyklego sufitu ").Append(n5Back);
            else if (!holdReady) sb.Append("NIECZYNNY (tabela surowcow karawan niegotowa)");
            else sb.Append("CZYNNY x").Append(shortCeil.ToString("0.00", inv)).Append(" przy braku w ponad ").Append(shortAt).Append(" miastach, kroki ponad zwykly sufit ").Append(n5Raised)
                   .Append(", towary ponad zwyklym sufitem [").Append(n5Above.Count > 0 ? string.Join(", ", n5Above.ToArray()) : "-").Append(']');
            sb.Append("; N5-D krok wedlug zuzycia: ");
            if (!followKey) sb.Append("WYLACZONY (Valve Step Follows Use)");
            else sb.Append("CZYNNY, przyciecia ").Append(n5Trim).Append(", przyciecia czekajace na pelne okno ").Append(n5TrimWait).Append(", kroki w gore czekajace po przycieciu ").Append(n5UpWait);
            sb.Append("; potkniecia ").Append(_stumbles).Append('.');
            Log.Info(sb.ToString());
            if (hl != null) Log.Info(hl);
        }

        // ------------------------------------------------------------ E6/K4 (220): hodowla wedlug popytu
        private static bool IsH(string id) { return id != null && id.StartsWith(HPrefix, StringComparison.Ordinal); }

        /// <summary>Kategoria konia w regulatorze: 0..3 (zwykly, bojowy, szlachetny, juczny); -1 = nie kon albo egzotyczny.</summary>
        private static int HCat(ItemObject it) { int c = HorseBook.Cat(it); return c >= 0 && c < HCats ? c : -1; }

        private static Good HFind(int c) { Good g; return _goods.TryGetValue(HPrefix + c.ToString(CultureInfo.InvariantCulture), out g) ? g : null; }

        private static void HAdd(int c, long sb)
        {
            var g = new Good { Id = HPrefix + c.ToString(CultureInfo.InvariantCulture), SA = -1, SB = sb };
            _goods[g.Id] = g;
        }

        private static void RebuildHorse()
        {
            float[] m = null;
            bool has = false;
            for (int c = 0; c < HCats; c++)
            {
                var g = HFind(c);
                if (g == null) continue;
                has = true;
                if (g.M == 1f) continue;
                if (m == null) m = new[] { 1f, 1f, 1f, 1f };
                m[c] = g.M;
            }
            _hHas = has;
            _hm = m;
            if (!has) _shift = 0f;
        }

        private static void ClearHorses()
        {
            for (int c = 0; c < HCats; c++) _goods.Remove(HPrefix + c.ToString(CultureInfo.InvariantCulture));
            _hm = null; _shift = 0f; _hHas = false;
        }

        private static void ClearDay() { Array.Clear(_hDay, 0, HCats); _castleDay = 0; _shiftAddDay = 0; _shiftTakeDay = 0.0; }

        private static void CountH(ItemRoster r, long[] into)
        {
            Array.Clear(into, 0, HCats);
            if (r == null) return;
            for (int i = 0; i < r.Count; i++)
            {
                var el = r.GetElementCopyAtIndex(i);
                if (el.Amount <= 0) continue;
                int c = HCat(el.EquipmentElement.Item);
                if (c >= 0) into[c] += el.Amount;
            }
        }

        private static string Row(long[] a)
        {
            var p = new string[a.Length];
            for (int i = 0; i < a.Length; i++) p[i] = a[i].ToString(CultureInfo.InvariantCulture);
            return string.Join("/", p);
        }

        private static string Pct(float s) { return (100f * s).ToString("0.0", CultureInfo.InvariantCulture) + "%"; }

        /// <summary>Stadnina: bojowe z listy typu wsi (wagi listy), gdy typ ma tez zwykle; null - przesuniecie tej wsi nie dotyczy. Tylko watek glowny (tick, doba).</summary>
        private static Stud StudOf(Village v)
        {
            var t = v != null ? v.VillageType : null;
            if (t == null) return null;
            Stud st;
            if (_studs.TryGetValue(t, out st)) return st;
            st = null;
            var war = new List<ItemObject>();
            var w = new List<float>();
            bool ord = false;
            float sum = 0f;
            foreach (var p in t.Productions)
            {
                if (p.Item1 == null || !(p.Item2 > 0f)) continue;
                int c = HCat(p.Item1);
                if (c == 0) ord = true;
                else if (c == 1 && !war.Contains(p.Item1)) { war.Add(p.Item1); w.Add(p.Item2); sum += p.Item2; }
            }
            if (ord && war.Count > 0 && sum > 0f) st = new Stud { War = war.ToArray(), W = w.ToArray(), Sum = sum };
            _studs[t] = st;
            return st;
        }

        /// <summary>
        /// MaterialLaw.ProdPostfix (najbardziej zewnetrzny model - _depth 1), po W3 i przed W2: mnoznik kategorii konia (czynnik x suma, jak inne czynniki);
        /// w ticku produkcji TEJ wsi (watek ticku) zwykly kon stadniny z bojowymi w liscie x (1 - udzial przesuniecia) od wyniku koncowego, zdjete sztuki do _shiftAcc
        /// (ShiftOut dopisze je bojowym w postfiksie ticku). Poza tickiem - tylko mnoznik (suma sztuk wsi bez zmian).
        /// </summary>
        internal static void HorseStep(Village v, ItemObject item, ref ExplainedNumber r)
        {
            if (item == null || item.ItemType != ItemObject.ItemTypeEnum.Horse) return;
            try
            {
                if (!HorseOn) return;
                int c = HCat(item);
                if (c < 0) return;
                var hm = _hm;
                float m = hm != null ? hm[c] : 1f;
                if (m != 1f)
                {
                    float sum = 1f + r.SumOfFactors;
                    if (sum > 0f) r.AddFactor((m - 1f) * sum, HorseText);
                }
                float s = _shift;
                if (c != 0 || !(s > 0f) || v == null || !ReferenceEquals(_tv, v) || System.Threading.Thread.CurrentThread.ManagedThreadId != _tvThread) return;
                if (StudOf(v) == null) return;
                float before = r.ResultNumber, sum2 = 1f + r.SumOfFactors;
                if (before > 0f && sum2 > 0f)
                {
                    r.AddFactor(-s * sum2, ShiftText);                 // (1 + suma) x (1 - s): wynik koncowy x (1 - s)
                    _shiftAcc += Math.Max(0f, before - r.ResultNumber);
                }
            }
            catch (Exception e) { Stumble("HorseStep", e); }
        }

        /// <summary>Postfiks ticku: sztuki zdjete ze zwyklych tej stadniny -> bojowe jej listy wedlug wag (zaokraglenie losowe - ta sama liczba sztuk w oczekiwaniu),
        /// do magazynu wsi z OnItemProduced (jak produkcja gry - ksiega koni E6/0 i pomiar regulatora widza je jako produkcje bojowych).</summary>
        private static void ShiftOut(Village v, float acc)
        {
            var st = StudOf(v);
            var sett = v.Settlement;
            if (st == null || sett == null || sett.ItemRoster == null) return;
            _shiftTakeDay += acc;
            for (int i = 0; i < st.War.Length; i++)
            {
                int n = MBRandom.RoundRandomized(acc * st.W[i] / st.Sum);
                if (n <= 0) continue;
                sett.ItemRoster.AddToCounts(st.War[i], n);
                CampaignEventDispatcher.Instance.OnItemProduced(st.War[i], sett, n);
                _shiftAddDay += n;
            }
        }

        /// <summary>Prefiks BK BKBuildingsBehavior.RunStuds(Town): konie w skrytce zamku przed hodowla BK (tylko licznik; BK dziala jak dotad).</summary>
        public static void StudsPrefix(Town __0)
        {
            _ct = null;
            if (__0 == null || !__0.IsCastle || _start < 0 || !_hHas || !HorseOn) return;
            try
            {
                var r = __0.Settlement != null ? __0.Settlement.Stash : null;
                if (r == null) return;
                CountH(r, _cb);
                _ct = __0;
            }
            catch (Exception e) { _ct = null; Stumble("StudsPrefix", e); }
        }

        /// <summary>Postfiks RunStuds: przyrost koni w skrytce zamku = hodowla zamku (produkcja kategorii - skrytka jest w stosie na targu).</summary>
        public static void StudsPostfix(Town __0)
        {
            var t = _ct;
            _ct = null;
            if (t == null || !ReferenceEquals(t, __0)) return;
            try
            {
                CountH(t.Settlement.Stash, _ca);
                for (int c = 0; c < HCats; c++)
                {
                    long d = _ca[c] - _cb[c];
                    if (d <= 0) continue;
                    var g = HFind(c);
                    if (g != null) g.P1 += d;
                    _hDay[c] += d;
                    _castleDay += d;
                }
            }
            catch (Exception e) { Stumble("StudsPostfix", e); }
        }

        /// <summary>Doba: klucz hodowli - wylaczony czysci konie; wlaczony w biegu regulatora zaklada konie bez stosu (pomiar od pelnego okna).</summary>
        private static void HorseKeyState(int day)
        {
            if (!HorseKey)
            {
                if (_hHas || _shift > 0f || _hm != null)
                {
                    ClearHorses();
                    Log.Info("Hodowla (220): dzien " + day + " - WYLACZONA (Horse Breeding By Demand = off) - konie wyczyszczone: mnozniki 1, przesuniecie 0; wlaczenie zacznie pomiar koni od nowa.");
                }
                else if (!_hLine) Log.Info("Hodowla (220): dzien " + day + " - WYLACZONA (Horse Breeding By Demand = off) - stadniny jak model, bez przesuniecia.");
                _hLine = true;
                return;
            }
            if (!On)
            {
                if (!_hLine) Log.Info("Hodowla (220): dzien " + day + " - NIEAKTYWNA - regulator wsi " + (_wired ? "wylaczony (Harvest Valve = off)" : "bez latki ticku produkcji wsi")
                                      + " - stadniny jak model, bez przesuniecia (hodowla dziala w regulatorze wsi).");
                _hLine = true;
                return;
            }
            if (_hHas && !HorseCutLive)
            {
                // K1-3a krok 2 (c): klucz ciecia wylaczony - mnozniki ponizej x1 (z czasu czynnego ciecia) wracaja do x1 (stan sprzed: ciecie tylko w logu)
                var back = new List<string>();
                for (int c = 0; c < HCats; c++)
                {
                    var g = HFind(c);
                    if (g == null || !(g.M < 1f - 1e-4f)) continue;
                    back.Add(HNames[c] + " x" + g.M.ToString("0.00", CultureInfo.InvariantCulture) + " -> x1.00");
                    g.M = 1f; g.Dir = 0;
                }
                if (back.Count > 0)
                {
                    RebuildHorse();
                    Log.Info("Hodowla (220): dzien " + day + " - ciecie hodowli WYLACZONE (Horse Breeding Cut Live = off) - mnozniki ponizej x1 wracaja do bazy gry: " + string.Join("; ", back.ToArray()) + ".");
                }
            }
            if (_start >= 0 && !_hHas)
            {
                for (int c = 0; c < HCats; c++) HAdd(c, -1);
                _hHas = true;
                Log.Info("Hodowla (220): dzien " + day + " - CZYNNA od tej doby (wlaczona w biegu regulatora, doba pomiaru " + ((int)CampaignTime.Now.ToDays - _start)
                         + ") - pomiar koni od nastepnej granicy kubelka, pierwsza decyzja koni po pelnym oknie " + (2 * Bucket) + " dob.");
            }
        }

        /// <summary>
        /// Granica kubelka (Close): decyzja kategorii koni (ta sama regula co towary; ponizej x1 na sucho) i udzial przesuniecia; przesuniecie kubelkow koni
        /// i stosu z ich spisu. Zwraca linie "Hodowla (220): dzien N - decyzja ..." (null - brak stanu koni).
        /// </summary>
        private static string HorseClose(bool decide, int low, int high, float ceil, double win, int day, int dec)
        {
            if (!_hHas) return null;
            var inv = CultureInfo.InvariantCulture;
            var now = _hNow;
            var parts = new List<string>();
            var dry = new List<string>();
            var cuts = new List<string>();              // K1-3a krok 2 (c): ciecia czynne tej decyzji (ponizej x1)
            bool cutLive = HorseCutLive;
            var want = new bool[HCats]; var over = new bool[HCats]; var lowS = new bool[HCats]; var done = new bool[HCats];
            int flips = 0;
            for (int c = 0; c < HCats; c++)
            {
                var g = HFind(c);
                if (g == null) continue;
                float mBucket = g.M;
                long n = now[c];
                if (decide && g.SA >= 0)
                {
                    try
                    {
                        double P = g.P0 + g.P1;
                        double C = Math.Max(0.0, P - (n - g.SA));
                        double cDay = C / win, pDay = P / win;
                        float m0 = g.M, m1 = m0;
                        int dir = 0;
                        string dr = "";
                        over[c] = P > 0.0 && (cDay > 0.0 ? n > high * cDay : n > high * pDay);
                        want[c] = cDay > 0.0 && C >= StepAbove * P && n < low * cDay;
                        lowS[c] = cDay > 0.0 && n < low * cDay;
                        done[c] = true;
                        if (P > 0.0 && C < CutBelow * P && over[c])
                        {
                            // ta sama regula co towary (cel 0.95 x zuzycie / produkcja bazowa okna, najwyzej x0.5, podloga 0.1); ponizej x1 - czynne tylko z kluczem ciecia
                            double baseP = (g.MA > 0f ? g.P0 / g.MA : g.P0) + (m0 > 0f ? g.P1 / m0 : g.P1);
                            if (!(baseP > 0.0)) baseP = P;
                            float cut = Math.Max(Floor, Math.Max((float)(CutTarget * C / baseP), m0 * CutMaxStep));
                            if (cut < m0 - 1e-4f)
                            {
                                // K1-3a krok 2 (c): przy HorseBreedingCutLive ciecie ponizej x1 CZYNNE (jak towary wsi); bez klucza czynne tylko cofniecie
                                // wlasnego kroku (najwyzej do x1), reszta na sucho
                                float live = cutLive ? cut : Math.Max(1f, cut);
                                if (live < m0 - 1e-4f) { m1 = live; dir = -1; }
                                if (cut < 1f - 1e-4f)
                                {
                                    if (cutLive)
                                    {
                                        double drop = (m0 - cut) * baseP / win;
                                        cuts.Add(HNames[c] + " x" + m0.ToString("0.00", inv) + " -> x" + cut.ToString("0.00", inv) + " (-" + drop.ToString("0.0", inv) + " szt./d)");
                                    }
                                    else
                                    {
                                        double less = (Math.Min(m1, 1f) - cut) * baseP / win;
                                        dr = "x" + cut.ToString("0.00", inv) + " (-" + less.ToString("0.0", inv) + " szt./d)";
                                        dry.Add(HNames[c] + " " + dr);
                                    }
                                }
                            }
                        }
                        else if (want[c])
                        {
                            m1 = Math.Min(m0 < 1f ? 1f : ceil, m0 * UpStep);
                            if (m1 > m0 + 1e-4f) dir = 1; else m1 = m0;
                        }
                        if (dir != 0 && g.Dir == -dir) { g.Flips++; flips++; }
                        g.Dir = dir;
                        g.M = m1;
                        parts.Add(HNames[c] + ": x" + m0.ToString("0.00", inv) + (dir != 0 ? " -> x" + m1.ToString("0.00", inv) : "")
                                  + ", zuzycie/produkcja " + (P > 0.0 ? (C / P).ToString("0.00", inv) : "-") + " (" + C.ToString("0", inv) + "/" + P.ToString("0", inv) + ")"
                                  + ", stos " + n + " = " + (cDay > 0.0 ? (n / cDay).ToString("0", inv) + " dni zuzycia" : "bez odbiorcy") + ", "
                                  + (dir < 0 ? (m1 < 1f - 1e-4f ? "w dol (ciecie czynne)" : "w dol (cofniecie kroku)") : dir > 0 ? "w gore" : "bez zmian") + (want[c] ? ", chciane" : "")
                                  + (dr.Length > 0 ? ", ciecie na sucho: mnoznik by byl " + dr : ""));
                    }
                    catch (Exception e) { Stumble("HorseDecide", e); }
                }
                else if (decide) parts.Add(HNames[c] + ": pomiar w toku (pelne okno po dwoch granicach kubelka od wlaczenia)");
                g.P0 = g.P1; g.P1 = 0.0; g.D0 = 0.0; g.D1 = 0.0; g.SA = g.SB; g.SB = n; g.MA = mBucket;
            }
            float s0 = _shift, s1 = s0;
            if (done[0] && done[1])
            {
                if (want[1] && over[0]) s1 = Math.Min(ShiftMax, s0 + ShiftStep);
                else if (s0 > 0f && (lowS[0] || over[1])) s1 = Math.Max(0f, s0 - ShiftStep);
            }
            _shift = s1;
            RebuildHorse();
            if (!decide)
                return "Hodowla (220): dzien " + day + " - koniec pierwszego kubelka koni, stos na targu zwykly/bojowy/szlachetny/juczny " + Row(now)
                       + "; pierwsza decyzja za " + Bucket + " dob.";
            Func<bool, bool, string> tn = (d, x) => d ? (x ? "TAK" : "NIE") : "-";
            var sb = new StringBuilder();
            sb.Append("Hodowla (220): dzien ").Append(day).Append(" - decyzja ").Append(dec).Append(" (okno ").Append((int)win).Append(" dob, stos ").Append(low).Append('-').Append(high)
              .Append(" dni zuzycia, sufit x").Append(ceil.ToString("0.00", inv)).Append("): [").Append(string.Join("; ", parts.ToArray()))
              .Append("] | przesuniecie zwykle -> bojowe ").Append(Pct(s0)).Append(" -> ").Append(Pct(s1))
              .Append(" (bojowe chciane ").Append(tn(done[1], want[1])).Append(", zwykle w nadmiarze ").Append(tn(done[0], over[0]))
              .Append(", zwykle ponizej ").Append(low).Append(" dni ").Append(tn(done[0], lowS[0])).Append(", bojowe w nadmiarze ").Append(tn(done[1], over[1])).Append(')')
              .Append(" | ciecie na sucho: ").Append(dry.Count > 0 ? string.Join("; ", dry.ToArray()) : "brak")
              .Append(cutLive ? " (czynne - klucz Horse Breeding Cut Live)" : " (czynne pod kluczem Horse Breeding Cut Live - wylaczony)")
              .Append(" | ciecie czynne (K1-3a): ").Append(!cutLive ? "wylaczone" : cuts.Count > 0 ? string.Join("; ", cuts.ToArray()) : "brak")
              .Append(" | zmiany kierunku w kolejnych decyzjach ").Append(flips).Append("; potkniecia ").Append(_stumbles).Append('.');
            return sb.ToString();
        }

        /// <summary>Doba: linia "Hodowla (220): dzien N - CZYNNA | ..." (produkcja doby z licznikow ticku i RunStuds, przesuniecie, mnozniki, stos ze spisu); raz na sesje przeglad stadnin.</summary>
        private static void HorseDaily(int day, int today)
        {
            if (!On || !_hHas || !HorseKey) { ClearDay(); return; }
            try
            {
                if (!_review) { _review = true; Review(day); }
                var inv = CultureInfo.InvariantCulture;
                var hm = _hm;
                var sb = new StringBuilder();
                sb.Append("Hodowla (220): dzien ").Append(day).Append(" - CZYNNA | produkcja doby zwykly/bojowy/szlachetny/juczny ").Append(Row(_hDay))
                  .Append(" (stadniny wsi i zamkow; zamki BK ").Append(_castleDay).Append(_studsWired ? "" : ", BRAK latki RunStuds BK").Append(") | przesuniecie zwykle -> bojowe ")
                  .Append(Pct(_shift)).Append(": zdjete ze zwyklych ").Append(_shiftTakeDay.ToString("0.0", inv)).Append(", dopisane bojowym ").Append(_shiftAddDay)
                  .Append(" | mnozniki ");
                for (int c = 0; c < HCats; c++) sb.Append(c > 0 ? "/" : "").Append('x').Append((hm != null ? hm[c] : 1f).ToString("0.00", inv));
                sb.Append(HorseCutLive ? " (czynne: krok w gore, cofniecie kroku i ciecie ponizej x1 - Horse Breeding Cut Live) | stos na targu (spis co "
                                       : " (czynne: krok w gore i cofniecie kroku; ciecie ponizej x1 na sucho) | stos na targu (spis co ").Append(Bucket).Append(" dob) ").Append(Row(_hNow))
                  .Append(" | doba pomiaru ").Append(today - _start).Append(", nastepna decyzja za ").Append(Math.Max(0, Bucket - (today - _bucketStart)))
                  .Append(" dob; potkniecia ").Append(_stumbles).Append('.');
                Log.Info(sb.ToString());
            }
            catch (Exception e) { Stumble("HorseDaily", e); }
            ClearDay();
        }

        /// <summary>Raz na sesje: wsie z konmi w typie wsi wedlug krainy (kultura z dnia kalibracji) i typu - przeglad stadnin wobec lore (tylko log, typy bez zmian).</summary>
        private static void Review(int day)
        {
            var inv = CultureInfo.InvariantCulture;
            var realm = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var kind = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var w = new double[HCats];
            int n = 0, studs = 0;
            foreach (var v in Village.All)
            {
                if (v == null || v.VillageType == null || v.Settlement == null) continue;
                bool any = false;
                foreach (var p in v.VillageType.Productions)
                {
                    int c = HCat(p.Item1);
                    if (c < 0 || !(p.Item2 > 0f)) continue;
                    any = true;
                    w[c] += p.Item2;
                }
                if (!any) continue;
                n++;
                if (StudOf(v) != null) studs++;
                string r = PeopleBook.CultureAtCalibration(v.Settlement) ?? "?", k = v.VillageType.StringId ?? "?";
                int x;
                realm.TryGetValue(r, out x); realm[r] = x + 1;
                kind.TryGetValue(k, out x); kind[k] = x + 1;
            }
            var rp = new List<string>(); foreach (var kv in realm) rp.Add(kv.Key + " " + kv.Value);
            var kp = new List<string>(); foreach (var kv in kind) kp.Add(kv.Key + " " + kv.Value);
            var wp = new string[HCats]; for (int c = 0; c < HCats; c++) wp[c] = w[c].ToString("0.0", inv);
            Log.Info("Przeglad stadnin (220): dzien " + day + " - wsi z konmi w typie wsi " + n + " (stadnin ze zwyklymi i bojowymi - przesuniecie mozliwe: " + studs
                     + ") | suma wag typow zwykly/bojowy/szlachetny/juczny " + string.Join("/", wp) + " | wedlug krainy: " + string.Join(", ", rp.ToArray())
                     + " | wedlug typu: " + string.Join(", ", kp.ToArray()) + " | typy wsi bez zmian (zmiana typow stadnin - decyzja Jeffa).");
        }

        // ------------------------------------------------------------ zapis
        internal static string Export()
        {
            if (_start < 0) return "";
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("v1;").Append(_start).Append(';').Append(_bucketStart).Append(';').Append(_buckets).Append(';').Append(_decisions).Append(';').Append(_firstDecisionDay)
              .Append(';').Append(_shift.ToString("R", inv));   // E6/K4 (220): 7. pole naglowka - udzial przesuniecia zwykle -> bojowe (brak - 0)
            foreach (var g in _goods.Values)
            {
                sb.Append('|').Append(g.Id).Append(',').Append(g.M.ToString("R", inv)).Append(',').Append(g.P0.ToString("R", inv)).Append(',').Append(g.P1.ToString("R", inv))
                  .Append(',').Append(g.D0.ToString("R", inv)).Append(',').Append(g.D1.ToString("R", inv)).Append(',').Append(g.SA).Append(',').Append(g.SB)
                  .Append(',').Append(g.Dir).Append(',').Append(g.Flips).Append(',').Append(g.MA.ToString("R", inv));
            }
            return sb.ToString();
        }

        internal static void Import(string s)
        {
            Clear();
            if (string.IsNullOrEmpty(s)) return;
            try
            {
                var inv = CultureInfo.InvariantCulture;
                var parts = s.Split('|');
                var h = parts[0].Split(';');
                if (h.Length < 6 || h[0] != "v1") { Log.Info("Regulator wsi (221): stan z zapisu nieczytelny - pomiar od nowa."); return; }
                _start = int.Parse(h[1], inv); _bucketStart = int.Parse(h[2], inv); _buckets = int.Parse(h[3], inv);
                _decisions = int.Parse(h[4], inv); _firstDecisionDay = int.Parse(h[5], inv);
                float sh;
                if (h.Length >= 7 && float.TryParse(h[6], NumberStyles.Float, inv, out sh) && sh > 0f && sh <= ShiftMax) _shift = sh;   // E6/K4 (220)
                for (int i = 1; i < parts.Length; i++)
                {
                    var p = parts[i].Split(',');
                    if (p.Length < 10 || p[0].Length == 0) continue;
                    var g = new Good
                    {
                        Id = p[0], M = float.Parse(p[1], inv), P0 = double.Parse(p[2], inv), P1 = double.Parse(p[3], inv),
                        D0 = double.Parse(p[4], inv), D1 = double.Parse(p[5], inv), SA = long.Parse(p[6], inv), SB = long.Parse(p[7], inv),
                        Dir = int.Parse(p[8], inv), Flips = int.Parse(p[9], inv)
                    };
                    if (!(g.M > 0f) || g.M > MaxM) g.M = 1f;   // N5-A: sufit braku do MaxM (dotad 2 - zwykly sufit); wylaczony klucz cofa nadwyzke przy najblizszej decyzji
                    g.MA = p.Length >= 11 ? float.Parse(p[10], inv) : g.M;   // recenzja 221: pole 11 (mnoznik kubelka starszego); brak - jak M
                    if (!(g.MA > 0f) || g.MA > MaxM) g.MA = g.M;
                    _goods[g.Id] = g;
                }
                Rebuild();
                // recenzja 220: stos koni na targu z ostatniego spisu = SB wpisow "#kon:N" (stos na ostatniej granicy kubelka) - bez tego po wczytaniu linia doby
                // "Hodowla (220)" (i prog K4-9 sprawdz_logi) widzi stos 0/0/0/0 az do nastepnego spisu (do 28 dob)
                for (int c = 0; c < HCats; c++) { var hg = HFind(c); if (hg != null && hg.SB >= 0) _hNow[c] = hg.SB; }
            }
            catch (Exception e) { Clear(); Log.Error("HarvestValve.Import", e); }
        }

        // ------------------------------------------------------------ wpiecie
        /// <summary>Po VillageWoodlot.ApplyAll i Bloomery.ApplyAll (postfiks Priority.Last widzi ich dopiski w magazynie wsi).</summary>
        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var tb = AccessTools.TypeByName("TaleWorlds.CampaignSystem.CampaignBehaviors.VillageGoodProductionCampaignBehavior");
                var tick = tb != null ? AccessTools.Method(tb, "TickGoodProduction", new[] { typeof(Village), typeof(bool) }) : null;
                if (tick == null) { Log.Info("HarvestValve (E6/W3, 221): BRAK VillageGoodProductionCampaignBehavior.TickGoodProduction(Village, bool) - regulator wsi NIEAKTYWNY."); return; }
                h.Patch(tick, prefix: new HarmonyMethod(typeof(HarvestValve), nameof(TickPrefix)) { priority = Priority.First },
                              postfix: new HarmonyMethod(typeof(HarvestValve), nameof(TickPostfix)) { priority = Priority.Last },
                              finalizer: new HarmonyMethod(typeof(HarvestValve), nameof(TickFinalizer)));
                _wired = true;
                Log.Info("HarvestValve (E6/W3, 221): tick produkcji wsi wpiety (prefiks Priority.First przed BK, postfiks Priority.Last po lesie wsi, finalizer) - regulator przy kluczu Harvest Valve.");
            }
            catch (Exception e) { Log.Error("HarvestValve.ApplyAll", e); }
            try
            {
                // E6/K4 (220): hodowla zamkow BK (kon bojowy do skrytki zamku) - tylko licznik produkcji kategorii, BK dziala jak dotad
                var bt = AccessTools.TypeByName("BannerKings.Behaviours.BKBuildingsBehavior");
                var rs = bt != null ? AccessTools.Method(bt, "RunStuds", new[] { typeof(Town) }) : null;
                if (rs == null) Log.Info("HarvestValve (E6/K4, 220): BRAK BK BKBuildingsBehavior.RunStuds(Town) - hodowla zamkow BK poza pomiarem koni.");
                else
                {
                    h.Patch(rs, prefix: new HarmonyMethod(typeof(HarvestValve), nameof(StudsPrefix)), postfix: new HarmonyMethod(typeof(HarvestValve), nameof(StudsPostfix)));
                    _studsWired = true;
                    Log.Info("HarvestValve (E6/K4, 220): BK RunStuds wpiete (prefiks i postfiks licza konie dopisane skrytce zamku - produkcja kategorii; bez zmiany dzialania BK).");
                }
            }
            catch (Exception e) { Log.Error("HarvestValve.ApplyAll.RunStuds", e); }
        }
    }
}
