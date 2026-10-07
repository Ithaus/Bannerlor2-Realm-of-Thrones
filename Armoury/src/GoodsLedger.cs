using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Armoury
{
    /// <summary>
    /// KSIEGA TOWAROW (paczka 146; w planie K13 robocza 127, docs/PLAN-K13-2026-10-07.md). TYLKO LOG - niczego nie zmienia w grze.
    ///
    /// Dla kazdego towaru handlowego (ItemObject.IsTradeGood) i zywego inwentarza (IsAnimal i IsLiveStock: krowy, owce, swinie, drob) raz na dobe:
    /// produkcja wedlug zrodla, zuzycie wedlug ujscia, przewoz (wozy wsi, karawany, inne partie), zapas wedlug posiadacza i reszta
    /// "bez wyjasnienia". Kazda pozycja zamyka sie: zapas wczoraj + produkcja - zuzycie = zapas dzis + bez wyjasnienia.
    ///
    /// JAK LICZYMY (jedna zasada dla wszystkich zrodel): "ramka" = czas wywolania jednej metody gry albo moda, ktorej skutek jest
    /// znany (cykl warsztatu, produkcja wsi, konsumpcja mieszczan, jedzenie partii, zaopatrzenie BK, dobowy tick osady ...).
    /// Prefiks otwiera ramke, finalizer ja zamyka (finalizer biegnie zawsze, takze po wyjatku - ramka nie zostaje otwarta).
    /// W czasie ramki postfiks-podsluch na ItemRoster.AddToCounts(EquipmentElement, int) - jedynej metodzie gry, ktora zmienia liczbe
    /// sztuk w rosterze (Add, Remove, RemoveIf, AddToCounts(ItemObject) ida przez nia) - zapisuje do NAJBARDZIEJ WEWNETRZNEJ otwartej
    /// ramki zmiane kazdego sledzonego towaru. Po zamknieciu: suma zmian ramki na wszystkich rosterach = produkcja (+) albo zuzycie (-)
    /// tego zrodla; przewoz miedzy dwoma rosterami swiata znosi sie do zera (w ramkach handlu liczony osobno wedlug strony).
    /// Dlatego wynik nie zalezy od tego, JAK BK czy gra robia swoje: mnoznik rzemieslnikow BK (count sztuk z jednego wyrobu), garbowanie
    /// 1:1 (TanOrWeave), wsad warsztatow zbrojnych, gnicie BK - wszystko widac w roznicy rosterow, bez zgadywania zdarzen gry
    /// (zdarzenie OnItemProduced gra wola raz na wyrob, a BK drugi raz z liczba sztuk - stad ksiega na zdarzeniach liczylaby zle).
    /// Ramki zewnetrzne (dobowy tick partii, osady, miasta - dyspozytor zdarzen gry) lapia wszystko, czego nie zlapala ramka wewnetrzna:
    /// pozycja "inne ticki dobowe" (np. jaja i ser BK w partiach, BetterEconomy, cudze mody). Poza ramkami podsluch nic nie robi
    /// (jedno porownanie z null) - zmiany spoza ramek (handel gracza, lup, nowe partie z towarem, ticki godzinowe) zostaja w "bez wyjasnienia".
    /// Partia zniszczona albo rozwiazana: towar, ktory w niej zostal, przepada (zdarzenie gry MobilePartyDestroyed) - osobna pozycja.
    ///
    /// Zapas: jeden przebieg raz na dobe po rosterach wszystkich osad (+ schowki osad, Settlement.Stash) i wszystkich partii.
    /// Kontrola: ruda (iron) i drewno (hardwood) maja te same liczby co "Ruda:" i "Drewno:" (OreLedger) - wsie, las wsi, warsztaty
    /// zbrojne, linie towarowe, budowy, zapas - co do sztuki; linia "Towary (bilans):" mowi ZGODNA albo NIEZGODNA (od drugiej linii sesji).
    /// "Warsztaty zbrojne" = to, co zdjal WorkshopLaw (OreLedger.NoteWorkshop -> NoteArms), jak w ksiedze rudy; wsad gry i BK w linii zbrojnej
    /// (warsztat gracza, wylaczony WorkshopLaw) to "linie warsztatow" - jak "linie towarowe" ksiegi rudy (zdarzenie OnItemConsumed).
    /// Druga linia "Towary (utarg wozow):" - co tabory wsi sprzedaly w miastach i zamkach: sztuki, utarg (po zwrocie nadplaty 119),
    /// srednia cena sztuki i jej stosunek do wartosci przedmiotu.
    /// Linie dnia drukuja dzien zakonczony ((int)Now.ToDays - 1, jak "Ruda:"); stan ksiegi nie idzie do zapisu (pierwsza doba po
    /// wczytaniu nie ma "wczoraj" - bilans od drugiej). Wylacznik: GoodsLedgerEnabled (wylaczony = ramki sie nie otwieraja,
    /// podsluch konczy na pierwszym porownaniu).
    /// </summary>
    internal static class GoodsLedger
    {
        // ------------------------------------------------------------ rodzaje ramek
        internal const int FVillage = 0, FVillageFood = 1, FCycle = 2, FCons = 3, FFood = 4, FSupplyUse = 5, FSupplyBuy = 6, FBkSettle = 7,
                           FBkParty = 8, FWagon = 9, FSell = 10, FCaravanLeave = 11, FBuild = 12, FForage = 13, FBee = 14,
                           FTickParty = 15, FTickSettle = 16, FTickTown = 17, Kinds = 18;
        private static readonly string[] KindName = { "produkcja wsi", "zywnosc wsi", "cykle warsztatow", "konsumpcja osad", "jedzenie partii",
            "zaopatrzenie BK (zuzycie)", "zaopatrzenie BK (zakupy)", "tick osady BK", "tick partii BK", "sprzedaz wozow", "handel partii",
            "wyjazd karawany (BK)", "budowy", "furaz", "BetterEconomy", "tick partii", "tick osady", "tick miasta" };

        // ------------------------------------------------------------ posiadacze zapasu
        private const int HTown = 0, HCastle = 1, HVillage = 2, HStash = 3, HOtherSettl = 4, HWagon = 5, HCaravan = 6, HLord = 7, HPlayer = 8, HOtherParty = 9, Holders = 10;
        private static readonly string[] HolderName = { "miasta", "zamki", "wsie", "schowki osad", "inne osady", "wozy", "karawany", "lordowie", "gracz", "inne partie" };

        // ------------------------------------------------------------ przewoz (handel miedzy osada a partia)
        private const int TWagon = 0, TCarBuy = 1, TCarSell = 2, TLordBuy = 3, TLordSell = 4, TOthBuy = 5, TOthSell = 6, TSupplyBuy = 7, Transfers = 8;

        // ------------------------------------------------------------ grupy zrodel i ujsc (kolejnosc w linii)
        private const string GVil = "wsie", GWoodlot = "las wsi (126)", GShop = "warsztaty", GArt = "rzemieslnicy BK", GTanW = "garbowanie i tkanie 1:1 (TanOrWeave)",
                             GShopIn = "linie warsztatow", GArms = "warsztaty zbrojne", GBuild = "budowy", GTown = "mieszczanie", GCastle = "zamki",
                             GFood = "zywnosc partii", GSlaughter = "uboj w partiach", GSupply = "zaopatrzenie BK", GBkSettle = "BK osady", GBkParty = "BK partie",
                             GBee = "BetterEconomy", GForage = "furaz armii (Armoury)", GLost = "przepadlo z rozbitymi partiami", GNoBuyer = "sprzedane bez kupca",
                             GTick = "inne ticki dobowe", GRest = "handel (reszta)";
        private static readonly string[] SrcOrder = { GVil, GWoodlot, GShop, GArt, GTanW, GSlaughter, GBkParty, GBkSettle, GBee, GForage, GTick };
        private static readonly string[] SinkOrder = { GShopIn, GArt, GTanW, GArms, GBuild, GTown, GCastle, GFood, GSupply, GBkSettle, GBkParty, GBee, GLost, GNoBuyer, GTick };

        // ------------------------------------------------------------ ramka
        internal sealed class Frame
        {
            public int Kind, Depth;
            public Frame Parent;
            public object A, B;                  // kontekst: wies / warsztat / osada / partia / strona handlu
            public object Info;                  // linia warsztatu, etykieta BetterEconomy
            public ItemRoster RA, RB;            // ramki handlu: dwa rostery (A = osada albo sprzedajacy, B = partia albo kupujacy)
            public int[] Net, InA, InB, Price;
            public int[] Arms;                   // cykl warsztatu: sztuki zdjete przez WorkshopLaw (OreLedger.NoteWorkshop) - "warsztaty zbrojne"
            public bool[] Mark;
            public readonly List<int> Touched = new List<int>();
            public int Wood;                     // drewno lasu wsi (126) dopisane w tej ramce (OreLedger.NoteWoodlot)
            public long Gold0;                   // sprzedaz wozu: kiesa taboru przed sprzedaza
            public int Thread;

            public void Size(int n)
            {
                if (Net != null && Net.Length == n) return;
                Net = new int[n]; InA = new int[n]; InB = new int[n]; Price = new int[n]; Arms = new int[n]; Mark = new bool[n];
                Touched.Clear();                 // indeksy starej listy towarow nie pasuja do nowych tablic
            }

            public void Clean()
            {
                foreach (var i in Touched) { Net[i] = 0; InA[i] = 0; InB[i] = 0; Price[i] = 0; Arms[i] = 0; Mark[i] = false; }
                Touched.Clear();
                A = B = Info = null; RA = RB = null; Parent = null; Wood = 0; Gold0 = 0;
            }

            public void Touch(int i) { if (!Mark[i]) { Mark[i] = true; Touched.Add(i); } }

            public void Rec(ItemRoster r, int i, int n)
            {
                Touch(i);
                Net[i] += n;
                if (RA != null && ReferenceEquals(r, RA)) InA[i] += n;
                else if (RB != null && ReferenceEquals(r, RB)) InB[i] += n;
            }
        }

        private sealed class RefEq : IEqualityComparer<ItemObject>
        {
            public bool Equals(ItemObject a, ItemObject b) { return ReferenceEquals(a, b); }
            public int GetHashCode(ItemObject o) { return RuntimeHelpers.GetHashCode(o); }
        }

        /// <summary>Linia produkcji warsztatu (WorkshopType.Production) - opis raz na sesje, klucz: lista wyrobow linii (obiekt z XML, staly).</summary>
        private sealed class Line
        {
            public string Key;                   // "zboze>chleb" w id kategorii gry, np. "grain>bread", "sheep>meat+hides+wool"
            public bool TanWeave;
            public readonly Dictionary<ItemCategory, int> Out = new Dictionary<ItemCategory, int>();
        }

        /// <summary>Dzienne liczniki jednego towaru.</summary>
        private sealed class Acc
        {
            public readonly Dictionary<string, Dictionary<string, long>> Src = new Dictionary<string, Dictionary<string, long>>();
            public readonly Dictionary<string, Dictionary<string, long>> Sink = new Dictionary<string, Dictionary<string, long>>();
            public readonly long[] Tr = new long[Transfers];
            public readonly long[] Hold = new long[Holders];
            public long Extra;                   // ponad recepture u ukrytych rzemieslnikow (mnoznik BK count)
            public long WagonUnits, WagonGold;
            public long Last = -1;               // zapas wczoraj (-1 = nieznany: pierwsza doba sesji)

            public void NewDay()
            {
                Src.Clear(); Sink.Clear(); Array.Clear(Tr, 0, Tr.Length); Array.Clear(Hold, 0, Hold.Length);
                Extra = 0; WagonUnits = 0; WagonGold = 0;
            }
        }

        // ------------------------------------------------------------ stan
        private static ItemObject[] _items;
        private static Dictionary<ItemObject, int> _idx;
        private static Acc[] _acc;
        private const int MaxDepth = 24;
        private static readonly Frame[] _pool = new Frame[MaxDepth];
        private static Frame _top;               // najbardziej wewnetrzna otwarta ramka (tylko watek glowny)
        private static int _depth, _main = -1;
        private static readonly Dictionary<object, Line> _lines = new Dictionary<object, Line>();
        private static readonly HashSet<string> _wired = new HashSet<string>(), _missing = new HashSet<string>();
        private static bool _tapWired;
        private static PropertyInfo _supplyParty;

        // liczniki doby (tylko log)
        private static readonly int[] _framesN = new int[Kinds];
        private static long _tapAll, _tapIn, _tapRec, _ticksFrames, _lostParties;
        private static int _orphans, _overflow, _stumbles, _stumblesAll, _offThread;
        private static readonly HashSet<string> _errSites = new HashSet<string>();
        private static int _lastDay = -1;
        private static bool _wasOn;

        internal static bool On
        {
            get { var s = Settings.Current; return s != null && s.GoodsLedgerEnabled && s.LogEnabled; }
        }

        internal static void Reset()
        {
            _items = null; _idx = null; _acc = null; _top = null; _depth = 0; _lines.Clear();
            for (int k = 0; k < Kinds; k++) _framesN[k] = 0;
            _tapAll = _tapIn = _tapRec = _ticksFrames = _lostParties = 0; _orphans = _overflow = _stumbles = _offThread = 0;
            _woodLeft = 0; _armsLeft = 0; _wagonGold = 0; _wagonSales = 0;
            _lastDay = -1; _wasOn = false;
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesAll++;
            if (_errSites.Add(where)) Log.Error("GoodsLedger." + where, e);
        }

        /// <summary>Lista towarow: wszystko, co gra ma jako towar handlowy, i zywy inwentarz (BK dodaje towary w kodzie - stad lista
        /// z menedzera obiektow, nie z plikow XML). Raz na sesje, w kampanii.</summary>
        private static bool EnsureItems()
        {
            if (_items != null) return true;
            try
            {
                var mgr = MBObjectManager.Instance;
                if (mgr == null) return false;
                var list = new List<ItemObject>();
                foreach (var it in mgr.GetObjectTypeList<ItemObject>())
                {
                    if (it == null || it.IsCraftedByPlayer) continue;
                    bool live = it.IsAnimal && it.HorseComponent != null && it.HorseComponent.IsLiveStock;
                    if (it.IsTradeGood || live) list.Add(it);
                }
                if (list.Count == 0) return false;
                list.Sort((a, b) => string.CompareOrdinal(a.StringId, b.StringId));
                var idx = new Dictionary<ItemObject, int>(new RefEq());
                for (int i = 0; i < list.Count; i++) idx[list[i]] = i;
                var acc = new Acc[list.Count];
                for (int i = 0; i < acc.Length; i++) acc[i] = new Acc();
                for (int d = 0; d < MaxDepth; d++) { if (_pool[d] == null) _pool[d] = new Frame(); _pool[d].Size(list.Count); _pool[d].Clean(); }
                _idx = idx; _acc = acc; _items = list.ToArray();
                return true;
            }
            catch (Exception e) { Stumble("EnsureItems", e); return false; }
        }

        // ------------------------------------------------------------ ramki
        /// <summary>Otwiera ramke (null, gdy ksiega wylaczona, poza watkiem glownym albo za gleboko - wtedy nic nie liczymy).</summary>
        internal static Frame Begin(int kind, object a = null, object b = null, ItemRoster ra = null, ItemRoster rb = null)
        {
            long t0 = 0;
            try
            {
                if (!On) return null;
                t0 = Stopwatch.GetTimestamp();   // koszt ramki = otwarcie + zamkniecie (End) - linia bilansu "ramki X ms"
                if (_main < 0) _main = Environment.CurrentManagedThreadId;
                if (Environment.CurrentManagedThreadId != _main) { _offThread++; return null; }
                if (!EnsureItems()) return null;
                if (_depth >= MaxDepth) { _overflow++; return null; }
                var f = _pool[_depth];
                f.Clean();
                f.Kind = kind; f.Depth = _depth; f.A = a; f.B = b; f.RA = ra; f.RB = rb; f.Parent = _top; f.Thread = _main;
                _top = f; _depth++;
                _framesN[kind]++;
                return f;
            }
            catch (Exception e) { Stumble("Begin", e); return null; }
            finally { if (t0 != 0) _ticksFrames += Stopwatch.GetTimestamp() - t0; }
        }

        /// <summary>Zamyka ramke i ksieguje jej zmiany. Ramki wewnatrz, ktore nie zostaly zamkniete (nie powinno sie zdarzyc - finalizer),
        /// zamykamy po kolei z licznikiem.</summary>
        internal static void End(Frame f)
        {
            if (f == null) return;
            long t0 = Stopwatch.GetTimestamp();
            bool mine = false;
            try
            {
                // ramka juz zamknieta albo stos wyczyszczony w srodku (Reset) - nic nie ruszamy, obiekt moze juz sluzyc innej ramce
                if (f.Depth >= _depth || !ReferenceEquals(_pool[f.Depth], f) || f.Thread != Environment.CurrentManagedThreadId) { _orphans++; return; }
                mine = true;
                while (_depth > f.Depth + 1)
                {
                    var inner = _top;
                    _orphans++;
                    _top = inner.Parent; _depth--;
                    try { Settle(inner); } catch (Exception e) { Stumble("Settle(sierota)", e); }
                    inner.Clean();
                }
                _top = f.Parent; _depth = f.Depth;
                Settle(f);
            }
            catch (Exception e) { Stumble("End", e); }
            finally
            {
                if (mine) { try { f.Clean(); } catch { } }
                _ticksFrames += Stopwatch.GetTimestamp() - t0;
            }
        }

        /// <summary>Paczka 126: las wsi (VillageWoodlot) dopisal drewno - ta czesc zmiany drewna w ramce idzie do pozycji "las wsi".</summary>
        internal static void NoteWoodlot(ItemObject wood, int n)
        {
            try
            {
                if (n <= 0 || _top == null || Environment.CurrentManagedThreadId != _main) { if (n > 0 && _top == null && On && EnsureItems()) BookItem(wood, GWoodlot, null, n); return; }
                _top.Wood += n;
            }
            catch (Exception e) { Stumble("NoteWoodlot", e); }
        }

        /// <summary>Recenzja 146: WorkshopLaw (warsztaty zbrojne) zdjal z polki material - OreLedger.NoteWorkshop (ruda, drewno, skora, len
        /// albo welna). Ta czesc zmiany w ramce cyklu idzie do "warsztaty zbrojne"; reszta cyklu (warsztat zbrojny GRACZA, start gry, wylaczony
        /// WorkshopLaw - wsad gry i BK ze zdarzeniem OnItemConsumed) do "linie warsztatow" - ten sam podzial co w ksiedze rudy i drewna
        /// (NoteWorkshop = "warsztaty zbrojne", OnItemConsumed = "linie towarowe"). Dotad ksiega towarow dawala wszystko z linii zbrojnej
        /// do "warsztaty zbrojne" - przy warsztacie zbrojnym gracza kontrola z ksiega rudy pokazalaby NIEZGODNA.</summary>
        internal static void NoteArms(ItemObject item, int n)
        {
            try
            {
                if (n <= 0 || item == null || !On) return;
                int i;
                if (_idx == null || !_idx.TryGetValue(item, out i)) return;
                var f = _top;
                if (f == null || f.Kind != FCycle || Environment.CurrentManagedThreadId != f.Thread) { _armsLeft += n; return; }   // nie powinno sie zdarzyc - licznik w bilansie
                f.Touch(i);
                f.Arms[i] += n;
            }
            catch (Exception e) { Stumble("NoteArms", e); }
        }

        // ------------------------------------------------------------ podsluch (ItemRoster.AddToCounts)
        /// <summary>Postfiks ItemRoster.AddToCounts(EquipmentElement, int): poza ramka konczy sie na pierwszym porownaniu. Wynik &lt; 0 =
        /// gra niczego nie zmienila (zero albo brak elementu do zdjecia).</summary>
        public static void Tap(ItemRoster __instance, EquipmentElement __0, int __1, int __result)
        {
            _tapAll++;
            var f = _top;
            if (f == null) return;
            if (__result < 0 || __1 == 0) return;
            TapIn(f, __instance, __0.Item, __1);
        }

        private static void TapIn(Frame f, ItemRoster r, ItemObject it, int n)
        {
            try
            {
                if (Environment.CurrentManagedThreadId != f.Thread) { _offThread++; return; }
                _tapIn++;
                int i;
                if (it == null || _idx == null || !_idx.TryGetValue(it, out i)) return;
                _tapRec++;
                f.Rec(r, i, n);
            }
            catch (Exception e) { Stumble("Tap", e); }
        }

        // ------------------------------------------------------------ ksiegowanie
        private static void Book(int i, string group, string sub, long n)
        {
            if (n == 0 || _acc == null) return;
            var a = _acc[i];
            var d = n > 0 ? a.Src : a.Sink;
            Dictionary<string, long> subs;
            if (!d.TryGetValue(group, out subs)) { subs = new Dictionary<string, long>(); d[group] = subs; }
            string k = sub ?? "";
            long v; subs.TryGetValue(k, out v); subs[k] = v + Math.Abs(n);
        }

        private static void BookItem(ItemObject it, string group, string sub, long n)
        {
            int i;
            if (it != null && _idx != null && _idx.TryGetValue(it, out i)) Book(i, group, sub, n);
        }

        private static int PartyClass(MobileParty mp)
        {
            if (mp == null) return HOtherParty;
            if (mp == MobileParty.MainParty) return HPlayer;
            if (mp.IsVillager) return HWagon;
            if (mp.IsCaravan) return HCaravan;
            if (mp.IsLordParty) return HLord;
            return HOtherParty;
        }

        private static int SettlClass(Settlement st)
        {
            if (st == null) return HOtherSettl;
            if (st.IsTown) return HTown;
            if (st.IsCastle) return HCastle;
            if (st.IsVillage) return HVillage;
            return HOtherSettl;
        }

        private static bool InType(Village v, ItemObject it)
        {
            try
            {
                var t = v != null ? v.VillageType : null;
                if (t == null || t.Productions == null) return false;
                foreach (var p in t.Productions) if (ReferenceEquals(p.Item1, it)) return true;
            }
            catch { }
            return false;
        }

        /// <summary>Rozlicza zamknieta ramke: zmiany na rosterach swiata -> pozycje produkcji, zuzycia i przewozu.</summary>
        private static void Settle(Frame f)
        {
            if (f.Touched.Count == 0 && f.Wood == 0) return;
            switch (f.Kind)
            {
                case FVillage:
                case FVillageFood:
                    {
                        var v = f.A as Village;
                        bool start = f.B != null;
                        foreach (var i in f.Touched)
                        {
                            long n = f.Net[i];
                            var it = _items[i];
                            if (f.Wood > 0 && n > 0 && it.StringId == "hardwood")
                            {
                                long w = Math.Min(n, f.Wood);
                                Book(i, GWoodlot, null, w); n -= w; f.Wood -= (int)w;
                            }
                            if (n == 0) continue;
                            string sub;
                            if (start) sub = "rozdanie startowe";
                            else if (f.Kind == FVillageFood) sub = "zywnosc wsi";
                            else if (InType(v, it)) sub = v.VillageType.StringId;
                            else if (it.StringId == "leather") sub = "garbarnia BK";
                            else if (it.StringId == "tools") sub = "kuznia BK";
                            else sub = "BK poza typem wsi";
                            Book(i, GVil, sub, n);
                        }
                        if (f.Wood > 0) _woodLeft += f.Wood;
                        return;
                    }
                case FCycle:
                    {
                        var w = f.A as Workshop;
                        var line = f.Info as Line;
                        var type = w != null ? w.WorkshopType : null;
                        string tid = type != null ? type.StringId : "?";
                        bool hidden = type != null && type.IsHidden;
                        bool player = w != null && w.Owner != null && w.Owner == Hero.MainHero;
                        // linia skory/plotna rzemieslnikow bez wsadu: gdy cos zeszlo z polki - to garbowanie/tkanie WorkshopLaw (skory -> skora,
                        // len -> plotno 1:1); gdy nic nie zeszlo, a wyrob przybyl - linia BK z niczego (WorkshopNoFreeRaw wylaczony)
                        bool tanWeave = false;
                        if (line != null && line.TanWeave && hidden) foreach (var i in f.Touched) if (f.Net[i] < 0) { tanWeave = true; break; }
                        foreach (var i in f.Touched)
                        {
                            long n = f.Net[i];
                            long take = f.Arms[i];
                            // warsztaty zbrojne = dokladnie to, co zdjal WorkshopLaw (NoteArms); reszta zmiany idzie wedlug linii jak kazdy cykl
                            if (take > 0) { Book(i, GArms, null, -take); n += take; }
                            if (n == 0) continue;
                            if (tanWeave) { Book(i, GTanW, null, n); continue; }
                            if (hidden)
                            {
                                string key = line != null ? (tid == "artisans" ? line.Key : tid + ":" + line.Key) : tid;
                                Book(i, GArt, key, n);
                                if (n > 0 && line != null)
                                {
                                    int exp;
                                    var cat = _items[i].ItemCategory;
                                    if (cat != null && line.Out.TryGetValue(cat, out exp) && exp > 0 && n > exp) _acc[i].Extra += n - exp;
                                }
                                continue;
                            }
                            string sub = player ? tid + " (gracz)" : tid;
                            Book(i, n > 0 ? GShop : GShopIn, sub, n);
                        }
                        return;
                    }
                case FCons:
                    {
                        var town = f.A as Town;
                        bool isTown = town != null && town.IsTown;
                        foreach (var i in f.Touched) Book(i, isTown ? GTown : GCastle, null, f.Net[i]);
                        return;
                    }
                case FFood:
                    foreach (var i in f.Touched) { long n = f.Net[i]; Book(i, n > 0 ? GSlaughter : GFood, null, n); }
                    return;
                case FSupplyUse:
                    foreach (var i in f.Touched) Book(i, GSupply, null, f.Net[i]);
                    return;
                case FBkSettle:
                    foreach (var i in f.Touched) { long n = f.Net[i]; Book(i, GBkSettle, n > 0 ? "dokup z nadwyzki rak i zywnosci" : "gnicie i nadprodukcja", n); }
                    return;
                case FBkParty:
                    foreach (var i in f.Touched) Book(i, GBkParty, null, f.Net[i]);
                    return;
                case FBuild:
                    foreach (var i in f.Touched) Book(i, GBuild, null, f.Net[i]);
                    return;
                case FForage:
                    foreach (var i in f.Touched) Book(i, GForage, null, f.Net[i]);
                    return;
                case FBee:
                    {
                        string lab = f.Info as string;
                        foreach (var i in f.Touched) Book(i, GBee, lab, f.Net[i]);
                        return;
                    }
                case FTickParty:
                case FTickSettle:
                case FTickTown:
                    {
                        string sub = f.Kind == FTickParty ? "partie" : f.Kind == FTickTown ? "miasta" : "osady";
                        foreach (var i in f.Touched) Book(i, GTick, sub, f.Net[i]);
                        return;
                    }
                case FWagon:
                    {
                        var mp = f.B as MobileParty;
                        long gold = mp != null ? (long)mp.PartyTradeGold - f.Gold0 : 0;
                        if (gold > 0) { _wagonGold += gold; _wagonSales++; }
                        // waga podzialu: sztuki x cena pierwszej sztuki; gdy ceny nie bylo (wyjatek wyceny) - same sztuki
                        double weight = 0, units = 0;
                        foreach (var i in f.Touched) if (f.InA[i] > 0) { units += f.InA[i]; if (f.Price[i] > 0) weight += (double)f.InA[i] * f.Price[i]; }
                        foreach (var i in f.Touched)
                        {
                            var a = _acc[i];
                            int sold = f.InA[i];
                            if (sold > 0)
                            {
                                a.Tr[TWagon] += sold;
                                a.WagonUnits += sold;
                                if (gold > 0 && weight > 0 && f.Price[i] > 0) a.WagonGold += (long)Math.Round(gold * ((double)sold * f.Price[i] / weight));
                                else if (gold > 0 && weight <= 0 && units > 0) a.WagonGold += (long)Math.Round(gold * (sold / units));
                            }
                            Book(i, GRest, "wozy", f.Net[i]);
                        }
                        return;
                    }
                case FSell:
                    {
                        var seller = f.A as PartyBase;
                        var buyer = f.B as PartyBase;
                        int sc = seller != null && seller.IsMobile ? PartyClass(seller.MobileParty) : -1;
                        int bc = buyer != null && buyer.IsMobile ? PartyClass(buyer.MobileParty) : -1;
                        foreach (var i in f.Touched)
                        {
                            var a = _acc[i];
                            if (sc >= 0 && f.InA[i] < 0) a.Tr[sc == HCaravan ? TCarSell : (sc == HLord || sc == HPlayer) ? TLordSell : TOthSell] += -f.InA[i];
                            if (bc >= 0 && f.InB[i] > 0) a.Tr[bc == HCaravan ? TCarBuy : (bc == HLord || bc == HPlayer) ? TLordBuy : TOthBuy] += f.InB[i];
                            long n = f.Net[i];
                            if (n != 0) Book(i, buyer == null && n < 0 ? GNoBuyer : GRest, buyer == null ? null : "handel partii", n);
                        }
                        return;
                    }
                case FCaravanLeave:
                    foreach (var i in f.Touched)
                    {
                        var a = _acc[i];
                        if (f.InB[i] > 0) a.Tr[TCarBuy] += f.InB[i]; else if (f.InB[i] < 0) a.Tr[TCarSell] += -f.InB[i];
                        Book(i, GRest, "wyjazd karawany", f.Net[i]);
                    }
                    return;
                case FSupplyBuy:
                    foreach (var i in f.Touched)
                    {
                        if (f.InB[i] > 0) _acc[i].Tr[TSupplyBuy] += f.InB[i];
                        Book(i, GRest, "zaopatrzenie BK", f.Net[i]);
                    }
                    return;
            }
        }
        private static long _woodLeft;       // drewno lasu wsi zgloszone w ramce, w ktorej nie bylo drewna (nie powinno sie zdarzyc) - licznik
        private static long _armsLeft;       // wsad WorkshopLaw zgloszony poza ramka cyklu warsztatu (nie powinno sie zdarzyc) - licznik
        private static long _wagonGold;      // utarg wozow doby co do denara (po zwrocie nadplaty 119); podzial na towary - wedlug ceny pierwszej sztuki
        private static int _wagonSales;

        /// <summary>Opis linii warsztatu raz na sesje (Production to struct, ale listy wejsc i wyjsc to obiekty wczytane z XML).</summary>
        private static Line LineOf(WorkshopType.Production p)
        {
            var outs = p.Outputs;
            if (outs == null) return null;
            Line l;
            if (_lines.TryGetValue(outs, out l)) return l;
            l = new Line();
            var sb = new StringBuilder();
            bool anyIn = false;
            if (p.Inputs != null)
                foreach (var x in p.Inputs)
                {
                    if (x.Item1 == null) continue;
                    if (anyIn) sb.Append('+');
                    if (x.Item2 > 1) sb.Append(x.Item2);
                    sb.Append(x.Item1.StringId); anyIn = true;
                }
            sb.Append('>');
            bool first = true; string only = null; int outN = 0;
            foreach (var x in outs)
            {
                if (x.Item1 == null) continue;
                if (!first) sb.Append('+');
                if (x.Item2 > 1) sb.Append(x.Item2);
                sb.Append(x.Item1.StringId); first = false; outN++; only = x.Item1.StringId;
                int c; l.Out.TryGetValue(x.Item1, out c); l.Out[x.Item1] = c + x.Item2;
            }
            l.Key = sb.ToString();
            l.TanWeave = !anyIn && outN == 1 && (only == "leather" || only == "linen");   // WorkshopLaw.TanOrWeave: linia rzemieslnikow bez wsadu
            _lines[outs] = l;
            return l;
        }

        // ------------------------------------------------------------ prefiksy (otwarcie ramki) i finalizer (zamkniecie)
        public static void VillagePre(Village __0, bool __1, out Frame __state)
        {
            __state = null;
            try { if (__0 != null) __state = Begin(FVillage, __0, __1 ? (object)"start" : null); } catch (Exception e) { Stumble("VillagePre", e); }
        }

        public static void VillageFoodPre(Village __0, bool __1, out Frame __state)
        {
            __state = null;
            try { if (__0 != null) __state = Begin(FVillageFood, __0, __1 ? (object)"start" : null); } catch (Exception e) { Stumble("VillageFoodPre", e); }
        }

        public static void CyclePre(WorkshopType.Production __0, Workshop __1, out Frame __state)
        {
            __state = null;
            try
            {
                if (__1 == null || !On) return;
                var line = LineOf(__0);
                var f = Begin(FCycle, __1);
                if (f != null) f.Info = line;
                __state = f;
            }
            catch (Exception e) { Stumble("CyclePre", e); }
        }

        public static void ConsPre(Town __0, out Frame __state)
        {
            __state = null;
            try { if (__0 != null) __state = Begin(FCons, __0); } catch (Exception e) { Stumble("ConsPre", e); }
        }

        public static void FoodPre(MobileParty __0, out Frame __state)
        {
            __state = null;
            try { if (__0 != null) __state = Begin(FFood, __0); } catch (Exception e) { Stumble("FoodPre", e); }
        }

        public static void WagonPre(Settlement __0, MobileParty __1, out Frame __state)
        {
            __state = null;
            try
            {
                if (__0 == null || __1 == null || !__1.IsVillager) return;
                var f = Begin(FWagon, __0, __1, __0.ItemRoster, __1.ItemRoster);
                if (f == null) return;
                __state = f;
                f.Gold0 = __1.PartyTradeGold;
                var town = __0.Town;
                var r = __1.ItemRoster;
                if (town == null || r == null) return;
                long tp = Stopwatch.GetTimestamp();   // wycena wsadu wozu liczy sie do kosztu ramek
                try
                {
                    for (int k = 0; k < r.Count; k++)
                    {
                        var el = r.GetElementCopyAtIndex(k);
                        int i;
                        if (el.EquipmentElement.Item == null || !_idx.TryGetValue(el.EquipmentElement.Item, out i)) continue;
                        if (f.Price[i] != 0) continue;
                        f.Touch(i);
                        f.Price[i] = Math.Max(1, town.GetItemPrice(el.EquipmentElement, __1, true));   // cena pierwszej sztuki - tylko waga podzialu utargu
                    }
                }
                finally { _ticksFrames += Stopwatch.GetTimestamp() - tp; }
            }
            catch (Exception e) { Stumble("WagonPre", e); }
        }

        public static void SellPre(PartyBase __0, PartyBase __1, out Frame __state)
        {
            __state = null;
            try { if (__0 != null) __state = Begin(FSell, __0, __1, __0.ItemRoster, __1 != null ? __1.ItemRoster : null); } catch (Exception e) { Stumble("SellPre", e); }
        }

        public static void TickPartyPre(MobileParty __0, out Frame __state)
        {
            __state = null;
            try { if (__0 != null) __state = Begin(FTickParty, __0); } catch (Exception e) { Stumble("TickPartyPre", e); }
        }

        public static void TickSettlePre(Settlement __0, out Frame __state)
        {
            __state = null;
            try { if (__0 != null) __state = Begin(FTickSettle, __0); } catch (Exception e) { Stumble("TickSettlePre", e); }
        }

        public static void TickTownPre(Town __0, out Frame __state)
        {
            __state = null;
            try { if (__0 != null) __state = Begin(FTickTown, __0); } catch (Exception e) { Stumble("TickTownPre", e); }
        }

        public static void SupplyUsePre(object __instance, out Frame __state)
        {
            __state = null;
            try { __state = Begin(FSupplyUse, __instance); } catch (Exception e) { Stumble("SupplyUsePre", e); }
        }

        public static void SupplyBuyPre(object __instance, out Frame __state)
        {
            __state = null;
            try
            {
                if (!On) return;
                var mp = _supplyParty != null ? _supplyParty.GetValue(__instance, null) as MobileParty : null;
                var st = mp != null ? mp.CurrentSettlement : null;
                __state = Begin(FSupplyBuy, mp, st, st != null ? st.ItemRoster : null, mp != null ? mp.ItemRoster : null);
            }
            catch (Exception e) { Stumble("SupplyBuyPre", e); }
        }

        public static void BkSettlePre(Settlement __0, out Frame __state)
        {
            __state = null;
            try { if (__0 != null) __state = Begin(FBkSettle, __0); } catch (Exception e) { Stumble("BkSettlePre", e); }
        }

        public static void BkPartyPre(MobileParty __0, out Frame __state)
        {
            __state = null;
            try { if (__0 != null) __state = Begin(FBkParty, __0); } catch (Exception e) { Stumble("BkPartyPre", e); }
        }

        public static void CaravanLeavePre(MobileParty __0, Settlement __1, out Frame __state)
        {
            __state = null;
            try
            {
                if (__0 == null || __1 == null || !__0.IsCaravan) return;
                __state = Begin(FCaravanLeave, __0, __1, __1.ItemRoster, __0.ItemRoster);
            }
            catch (Exception e) { Stumble("CaravanLeavePre", e); }
        }

        public static void BeeVillagePre(Settlement __0, out Frame __state) { __state = BeeOpen(__0, "wies: druga produkcja"); }
        public static void BeeTownPre(Settlement __0, out Frame __state) { __state = BeeOpen(__0, "miasto: zbrojownia i warsztaty"); }
        public static void BeeLordPre(Settlement __0, out Frame __state) { __state = BeeOpen(__0, "inwestycje panow"); }

        private static Frame BeeOpen(Settlement st, string label)
        {
            try
            {
                if (st == null) return null;
                var f = Begin(FBee, st);
                if (f != null) f.Info = label;
                return f;
            }
            catch (Exception e) { Stumble("BeeOpen", e); return null; }
        }

        /// <summary>Finalizer kazdej ramki - biegnie po wszystkich postfiksach i takze po wyjatku (void: wyjatek idzie dalej bez zmian).</summary>
        public static void Fin(Frame __state)
        {
            if (__state != null) End(__state);
        }

        // ------------------------------------------------------------ zdarzenie gry: partia zniszczona albo rozwiazana
        /// <summary>Towar, ktory zostal w partii w chwili jej zniszczenia, znika razem z nia (lup zwyciezca wzial wczesniej - to przewoz).</summary>
        internal static void OnPartyDestroyed(MobileParty mp, PartyBase destroyer)
        {
            try
            {
                if (mp == null || !On || !EnsureItems()) return;
                var r = mp.ItemRoster;
                if (r == null) return;
                string sub = HolderName[PartyClass(mp)];
                bool any = false;
                for (int k = 0; k < r.Count; k++)
                {
                    var el = r.GetElementCopyAtIndex(k);
                    int i;
                    if (el.Amount <= 0 || el.EquipmentElement.Item == null || !_idx.TryGetValue(el.EquipmentElement.Item, out i)) continue;
                    Book(i, GLost, sub, -el.Amount);
                    any = true;
                }
                if (any) _lostParties++;
            }
            catch (Exception e) { Stumble("OnPartyDestroyed", e); }
        }

        // ------------------------------------------------------------ linia dnia
        private static string Signed(long n) { return (n >= 0 ? "+" : "") + n; }

        private static long Sum(Dictionary<string, Dictionary<string, long>> d)
        {
            long s = 0;
            foreach (var g in d.Values) foreach (var v in g.Values) s += v;
            return s;
        }

        private static long Group(Dictionary<string, Dictionary<string, long>> d, string g)
        {
            Dictionary<string, long> subs;
            if (!d.TryGetValue(g, out subs)) return 0;
            long s = 0;
            foreach (var v in subs.Values) s += v;
            return s;
        }

        private static void Groups(StringBuilder sb, Dictionary<string, Dictionary<string, long>> d, string[] order)
        {
            bool first = true;
            var seen = new HashSet<string>();
            var keys = new List<string>(order);
            foreach (var k in d.Keys) if (Array.IndexOf(order, k) < 0) keys.Add(k);
            foreach (var g in keys)
            {
                if (!seen.Add(g)) continue;
                Dictionary<string, long> subs;
                if (!d.TryGetValue(g, out subs)) continue;
                long tot = 0;
                foreach (var v in subs.Values) tot += v;
                if (tot == 0) continue;
                if (!first) sb.Append("; ");
                first = false;
                sb.Append(g).Append(' ').Append(tot);
                var named = new List<KeyValuePair<string, long>>();
                foreach (var kv in subs) if (kv.Key.Length > 0 && kv.Value != 0) named.Add(kv);
                if (named.Count == 0) continue;
                named.Sort((x, y) => y.Value.CompareTo(x.Value));
                sb.Append(" (");
                long rest = tot;
                int shown = 0;
                foreach (var kv in named)
                {
                    if (shown == 5) break;
                    if (shown > 0) sb.Append(", ");
                    sb.Append(kv.Key).Append(' ').Append(kv.Value);
                    rest -= kv.Value; shown++;
                }
                if (rest != 0) sb.Append(", reszta ").Append(rest);
                sb.Append(')');
            }
            if (first) sb.Append('-');
        }

        /// <summary>Raz na dobe (ArmouryBehavior.OnDailyTick, zaraz po OreLedger.Daily): linie "Towary:", "Towary (utarg wozow):",
        /// "Towary (bilans):". Przebieg zapasu po rosterach, bilans kazdego towaru, kontrola z ksiega rudy i drewna, koszt doby.</summary>
        internal static void Daily()
        {
            bool on = On;
            if (!on)
            {
                if (_wasOn) Log.Info("Towary (bilans): dzien " + ((int)CampaignTime.Now.ToDays - 1) + " - KSIEGA WYLACZONA (Goods Ledger Enabled = off) - brak linii towarow.");
                _wasOn = false; _lastDay = -1;
                if (_acc != null) foreach (var a in _acc) { a.NewDay(); a.Last = -1; }
                return;
            }
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                _main = Environment.CurrentManagedThreadId;
                if (!EnsureItems()) return;
                int day = (int)CampaignTime.Now.ToDays - 1;
                var inv = CultureInfo.InvariantCulture;
                // ---- zapas wedlug posiadacza
                foreach (var a in _acc) Array.Clear(a.Hold, 0, Holders);
                foreach (var st in Settlement.All)
                {
                    if (st == null) continue;
                    int c = SettlClass(st);
                    Count(st.ItemRoster, c);
                    if (st.Stash != null && !ReferenceEquals(st.Stash, st.ItemRoster)) Count(st.Stash, HStash);
                }
                foreach (var mp in MobileParty.All)
                {
                    if (mp == null) continue;
                    Count(mp.ItemRoster, PartyClass(mp));
                }
                long tStock = Stopwatch.GetTimestamp() - t0;

                // ---- linie towarow
                string ts = DateTime.Now.ToString("HH:mm:ss");
                var all = new StringBuilder();
                long sumP = 0, sumC = 0, sumD = 0, sumU = 0; int moving = 0, closed = 0;
                var unexplained = new List<KeyValuePair<string, long>>();
                long wagonUnits = 0, wagonGold = 0;
                var wagonLines = new List<KeyValuePair<long, string>>();
                for (int i = 0; i < _items.Length; i++)
                {
                    var a = _acc[i];
                    var it = _items[i];
                    long now = 0;
                    foreach (var h in a.Hold) now += h;
                    long p = Sum(a.Src), c = Sum(a.Sink);
                    bool trade = false;
                    foreach (var t in a.Tr) if (t != 0) { trade = true; break; }
                    if (now == 0 && p == 0 && c == 0 && !trade && a.Last <= 0) { a.Last = now; a.NewDay(); continue; }
                    moving++;
                    var sb = new StringBuilder();
                    sb.Append("Towary: dzien ").Append(day).Append(" - ").Append(it.StringId).Append(" (").Append(it.Value).Append(" d): zapas ").Append(now);
                    long u = 0; bool known = a.Last >= 0;
                    if (known)
                    {
                        long d = now - a.Last;
                        u = d - (p - c);
                        sb.Append(" (").Append(Signed(d)).Append(')');
                        sumD += d; sumU += u; closed++;
                        if (u != 0) unexplained.Add(new KeyValuePair<string, long>(it.StringId, u));
                    }
                    else sb.Append(" (pierwszy pomiar - bilans od jutra)");
                    sb.Append(" | produkcja ").Append(p).Append(" [");
                    Groups(sb, a.Src, SrcOrder);
                    sb.Append("] | zuzycie ").Append(c).Append(" [");
                    Groups(sb, a.Sink, SinkOrder);
                    sb.Append("] | bez wyjasnienia ").Append(known ? Signed(u) : "-");
                    if (a.Extra != 0) sb.Append(" | mnoznik BK rzemieslnikow +").Append(a.Extra).Append(" (z ").Append(Group(a.Src, GArt)).Append(')');
                    // przewoz: tylko pozycje niezerowe (linia krotsza; brak pozycji = 0)
                    sb.Append(" | przewoz [");
                    int tl = sb.Length;
                    if (a.Tr[TWagon] != 0) sb.Append("wozy do osad ").Append(a.Tr[TWagon]);
                    if (a.Tr[TCarBuy] != 0 || a.Tr[TCarSell] != 0) sb.Append(sb.Length > tl ? "; " : "").Append("karawany kupily ").Append(a.Tr[TCarBuy]).Append(", sprzedaly ").Append(a.Tr[TCarSell]);
                    if (a.Tr[TLordBuy] != 0 || a.Tr[TLordSell] != 0) sb.Append(sb.Length > tl ? "; " : "").Append("lordowie kupili ").Append(a.Tr[TLordBuy]).Append(", sprzedali ").Append(a.Tr[TLordSell]);
                    if (a.Tr[TOthBuy] != 0 || a.Tr[TOthSell] != 0) sb.Append(sb.Length > tl ? "; " : "").Append("inne partie kupily ").Append(a.Tr[TOthBuy]).Append(", sprzedaly ").Append(a.Tr[TOthSell]);
                    if (a.Tr[TSupplyBuy] != 0) sb.Append(sb.Length > tl ? "; " : "").Append("zaopatrzenie BK kupilo ").Append(a.Tr[TSupplyBuy]);
                    if (sb.Length == tl) sb.Append('-');
                    sb.Append(']');
                    sb.Append(" | posiadacze [");
                    bool fh = true;
                    for (int h = 0; h < Holders; h++)
                    {
                        if (a.Hold[h] == 0) continue;
                        if (!fh) sb.Append(", ");
                        sb.Append(HolderName[h]).Append(' ').Append(a.Hold[h]); fh = false;
                    }
                    if (fh) sb.Append('-');
                    sb.Append("].");
                    if (all.Length > 0) all.Append(Environment.NewLine).Append('[').Append(ts).Append("] ");
                    all.Append(sb);
                    sumP += p; sumC += c;
                    if (a.WagonUnits > 0)
                    {
                        wagonUnits += a.WagonUnits; wagonGold += a.WagonGold;
                        double avg = (double)a.WagonGold / a.WagonUnits;
                        wagonLines.Add(new KeyValuePair<long, string>(a.WagonGold, it.StringId + " " + a.WagonUnits + " szt. " + a.WagonGold + " d (" + avg.ToString("0.0", inv)
                                                                                    + " d/szt. = " + (it.Value > 0 ? (avg / it.Value).ToString("0.00", inv) : "-") + " wartosci)"));
                    }
                }
                // ---- utarg wozow wedlug towaru
                wagonLines.Sort((x, y) => y.Key.CompareTo(x.Key));
                var wl = new StringBuilder();
                wl.Append("Towary (utarg wozow): dzien ").Append(day).Append(" - sprzedazy ").Append(_wagonSales).Append(", razem ").Append(_wagonGold).Append(" d za ").Append(wagonUnits)
                  .Append(" szt. (podzial na towary wedlug ceny pierwszej sztuki: ").Append(wagonGold).Append(" d) | ");
                if (wagonLines.Count == 0) wl.Append('-');
                for (int k = 0; k < wagonLines.Count; k++) { if (k > 0) wl.Append("; "); wl.Append(wagonLines[k].Value); }
                wl.Append('.');
                if (all.Length > 0) all.Append(Environment.NewLine).Append('[').Append(ts).Append("] ");
                all.Append(wl);

                // ---- bilans, kontrola, koszt
                unexplained.Sort((x, y) => Math.Abs(y.Value).CompareTo(Math.Abs(x.Value)));
                var bl = new StringBuilder();
                bl.Append("Towary (bilans): dzien ").Append(day).Append(" - towarow w ksiedze ").Append(_items.Length).Append(", z zapasem albo ruchem ").Append(moving)
                  .Append(" (bilans zamkniety dla ").Append(closed).Append(")")
                  .Append("; produkcja ").Append(sumP).Append(" szt., zuzycie ").Append(sumC).Append(", zmiana zapasu ").Append(Signed(sumD))
                  .Append(", bez wyjasnienia ").Append(Signed(sumU)).Append(" (najwieksze: ");
                if (unexplained.Count == 0) bl.Append("brak");
                for (int k = 0; k < unexplained.Count && k < 8; k++) { if (k > 0) bl.Append(", "); bl.Append(unexplained[k].Key).Append(' ').Append(Signed(unexplained[k].Value)); }
                bl.Append(")");
                bl.Append("; ramki:");
                for (int k = 0; k < Kinds; k++) if (_framesN[k] > 0) bl.Append(' ').Append(KindName[k]).Append(' ').Append(_framesN[k]).Append(',');
                if (bl[bl.Length - 1] == ',') bl.Length--;
                bl.Append("; partie zniszczone z towarem ").Append(_lostParties);
                // recenzja 146: pierwsza linia sesji (i pierwsza po wlaczeniu) bez kontroli - w nowej kampanii ramki zbieraja od chwili
                // utworzenia swiata (ticki startowe: produkcja wsi, rozdanie startowe), a ksiega rudy zeruje je przy przeliczeniu zapasu
                // startowego (StartStock -> OreLedger.TakeStartTicks); po wlaczeniu w MCM ksiega rudy liczy cala dobe, a ta - od wlaczenia
                bl.Append("; kontrola z ksiega rudy i drewna: ");
                if (_lastDay < 0) bl.Append("od jutra (pierwszy pomiar - doba ksiegi towarow i ksiegi rudy zaczela sie w innej chwili)");
                else bl.Append(Control("iron", "ruda")).Append(", ").Append(Control("hardwood", "drewno"));
                double f = 1000.0 / Stopwatch.Frequency;
                long tAll = Stopwatch.GetTimestamp() - t0;
                bl.Append("; koszt: zapas ").Append((tStock * f).ToString("0.0", inv)).Append(" ms, ramki ").Append((_ticksFrames * f).ToString("0.0", inv))
                  .Append(" ms, linie ").Append(((tAll - tStock) * f).ToString("0.0", inv)).Append(" ms; AddToCounts: wszystkich ").Append(_tapAll)
                  .Append(", w ramkach ").Append(_tapIn).Append(" (towary ").Append(_tapRec).Append(")");
                if (_orphans + _overflow + _offThread + _woodLeft + _armsLeft > 0)
                    bl.Append("; ramki osierocone ").Append(_orphans).Append(", za glebokie ").Append(_overflow).Append(", poza watkiem glownym ").Append(_offThread).Append(", drewno lasu poza drewnem ramki ").Append(_woodLeft)
                      .Append(", wsad warsztatow zbrojnych poza ramka cyklu ").Append(_armsLeft);
                bl.Append("; potkniecia dzis ").Append(_stumbles).Append(" (od wczytania ").Append(_stumblesAll).Append(").");
                if (_lastDay >= 0 && day != _lastDay + 1) bl.Append(" UWAGA: poprzednia linia ksiegi byla dla dnia ").Append(_lastDay).Append(" - bilans obejmuje ").Append(day - _lastDay).Append(" dob.");

                // jeden zapis do pliku: kazda linia ma wlasny znacznik czasu, jak z osobnych Log.Info (40-60 linii dziennie - jeden dostep do dysku)
                all.Append(Environment.NewLine).Append('[').Append(ts).Append("] ").Append(bl);
                Log.Info(all.ToString());

                // ---- nowa doba
                for (int i = 0; i < _items.Length; i++)
                {
                    var a = _acc[i];
                    long now = 0; foreach (var h in a.Hold) now += h;
                    a.Last = now; a.NewDay();
                }
                for (int k = 0; k < Kinds; k++) _framesN[k] = 0;
                _tapAll = _tapIn = _tapRec = _ticksFrames = _lostParties = 0; _orphans = _overflow = _offThread = _stumbles = 0; _woodLeft = 0; _armsLeft = 0;
                _wagonGold = 0; _wagonSales = 0;
                _lastDay = day; _wasOn = true;
            }
            catch (Exception e) { Stumble("Daily", e); }
        }

        private static void Count(ItemRoster r, int holder)
        {
            if (r == null) return;
            for (int k = 0; k < r.Count; k++)
            {
                var el = r.GetElementCopyAtIndex(k);
                var it = el.EquipmentElement.Item;
                int i;
                if (it == null || el.Amount == 0 || !_idx.TryGetValue(it, out i)) continue;
                _acc[i].Hold[holder] += el.Amount;
            }
        }

        /// <summary>Ten sam towar w ksiedze rudy i drewna (OreLedger, linia wydrukowana chwile wczesniej w tym samym ticku): wsie, las wsi,
        /// warsztaty zbrojne, linie towarowe (wsad z targu), budowy i zapas (miasta + zamki + wsie + wszystkie partie) - co do sztuki.
        /// "miasta i zamki" (wyroby warsztatow) tylko dla informacji: ksiega rudy liczy tam zdarzenia gry, a gra i BK wolaja je dwa razy.</summary>
        private static string Control(string id, string name)
        {
            try
            {
                var o = OreLedger.ShownToday(id);
                int i = -1;
                if (_items != null) for (int k = 0; k < _items.Length; k++) if (_items[k].StringId == id) { i = k; break; }
                if (o == null || o.Length < 7 || i < 0) return name + " - brak linii ksiegi rudy";
                var a = _acc[i];
                long vil = Group(a.Src, GVil), wood = Group(a.Src, GWoodlot), towns = Group(a.Src, GShop) + Group(a.Src, GArt);
                long shops = Group(a.Sink, GArms), lines = Group(a.Sink, GShopIn) + Group(a.Sink, GArt), builds = Group(a.Sink, GBuild);
                long stock = a.Hold[HTown] + a.Hold[HCastle] + a.Hold[HVillage] + a.Hold[HWagon] + a.Hold[HCaravan] + a.Hold[HLord] + a.Hold[HPlayer] + a.Hold[HOtherParty];
                bool ok = vil == o[0] && wood == o[1] && shops == o[3] && lines == o[4] && builds == o[5] && stock == o[6];
                return name + " " + (ok ? "ZGODNA" : "NIEZGODNA") + " (wsie " + vil + "/" + o[0] + ", las " + wood + "/" + o[1] + ", zbrojne " + shops + "/" + o[3]
                       + ", linie " + lines + "/" + o[4] + ", budowy " + builds + "/" + o[5] + ", zapas " + stock + "/" + o[6] + "; wyroby miast " + towns + " wobec zdarzen gry " + o[2] + ")";
            }
            catch (Exception e) { Stumble("Control", e); return name + " - blad kontroli"; }
        }

        // ------------------------------------------------------------ wpiecie
        internal static void ApplyAll(Harmony h)
        {
            _wired.Clear(); _missing.Clear();
            _main = Environment.CurrentManagedThreadId;
            var fin = new HarmonyMethod(typeof(GoodsLedger), nameof(Fin));
            Action<string, MethodBase, string> frame = (name, target, prefix) =>
            {
                try
                {
                    if (target == null) { _missing.Add(name); return; }
                    h.Patch(target, prefix: new HarmonyMethod(typeof(GoodsLedger), prefix) { priority = Priority.First }, finalizer: fin);
                    _wired.Add(name);
                }
                catch (Exception e) { _missing.Add(name + " (" + e.Message + ")"); Log.Error("GoodsLedger.ApplyAll(" + name + ")", e); }
            };
            const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            // ramki wewnetrzne (gra)
            var prod = typeof(VillageGoodProductionCampaignBehavior);
            frame("produkcja wsi", prod.GetMethod("TickGoodProduction", any, null, new[] { typeof(Village), typeof(bool) }, null), nameof(VillagePre));
            frame("zywnosc wsi", prod.GetMethod("TickFoodProduction", any, null, new[] { typeof(Village), typeof(bool) }, null), nameof(VillageFoodPre));
            var ws = typeof(WorkshopsCampaignBehavior);
            frame("cykl warsztatu notabla", AccessTools.Method(ws, "TickOneProductionCycleForNotableWorkshop"), nameof(CyclePre));
            frame("cykl warsztatu gracza", AccessTools.Method(ws, "TickOneProductionCycleForPlayerWorkshop"), nameof(CyclePre));
            frame("konsumpcja osady", AccessTools.Method(typeof(ItemConsumptionBehavior), "MakeConsumptionInTown"), nameof(ConsPre));
            frame("jedzenie partii", AccessTools.Method(typeof(FoodConsumptionBehavior), "PartyConsumeFood"), nameof(FoodPre));
            var sellGoods = AccessTools.Method(typeof(SellGoodsForTradeAction), "ApplyInternal");
            var sgp = sellGoods != null ? sellGoods.GetParameters() : null;
            frame("sprzedaz wozu", sgp != null && sgp.Length >= 2 && sgp[0].ParameterType == typeof(Settlement) && sgp[1].ParameterType == typeof(MobileParty) ? sellGoods : null, nameof(WagonPre));
            var sellItems = AccessTools.Method(typeof(SellItemsAction), "ApplyInternal");
            var sip = sellItems != null ? sellItems.GetParameters() : null;
            frame("handel partii", sip != null && sip.Length >= 2 && sip[0].ParameterType == typeof(PartyBase) && sip[1].ParameterType == typeof(PartyBase) ? sellItems : null, nameof(SellPre));
            // ramki wewnetrzne (BK, BetterEconomy - cudzy kod, szukane po nazwie typu)
            var tSup = QuartermasterLaw.FindType("BannerKings.Behaviours.PartyNeeds.PartySupplies");
            if (tSup != null)
            {
                _supplyParty = tSup.GetProperty("Party", BindingFlags.Public | BindingFlags.Instance);
                frame("zaopatrzenie BK (zuzycie)", tSup.GetMethod("ConsumeItems", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(float), typeof(List<ItemCategory>) }, null), nameof(SupplyUsePre));
                frame("zaopatrzenie BK (zakupy)", _supplyParty != null ? tSup.GetMethod("BuyItems", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(float), typeof(List<ItemCategory>) }, null) : null, nameof(SupplyBuyPre));
            }
            else { _missing.Add("zaopatrzenie BK (brak BK)"); }
            var tBkSet = QuartermasterLaw.FindType("BannerKings.Behaviours.BKSettlementBehavior");
            frame("tick osady BK", tBkSet != null ? tBkSet.GetMethod("DailySettlementTick", any, null, new[] { typeof(Settlement) }, null) : null, nameof(BkSettlePre));
            var tBkParty = QuartermasterLaw.FindType("BannerKings.Behaviours.BKPartyBehavior");
            frame("tick partii BK", tBkParty != null ? tBkParty.GetMethod("OnDailyTick", any, null, new[] { typeof(MobileParty) }, null) : null, nameof(BkPartyPre));
            frame("wyjazd karawany BK", tBkParty != null ? tBkParty.GetMethod("OnSettlementLeft", any, null, new[] { typeof(MobileParty), typeof(Settlement) }, null) : null, nameof(CaravanLeavePre));
            foreach (var bee in new[] { new[] { "VillageDevelopmentCampaignBehavior", nameof(BeeVillagePre) }, new[] { "TownEconomyCampaignBehavior", nameof(BeeTownPre) }, new[] { "LordInvestmentCampaignBehavior", nameof(BeeLordPre) } })
            {
                var t = QuartermasterLaw.FindType("BetterEconomy.Behaviors." + bee[0]);
                if (t == null) { _missing.Add("BetterEconomy " + bee[0] + " (brak modu)"); continue; }
                frame("BetterEconomy " + bee[0], t.GetMethod("OnSettlementDailyTick", any, null, new[] { typeof(Settlement) }, null), bee[1]);
            }
            // ramki zewnetrzne: dyspozytor dobowych zdarzen gry (wszyscy sluchacze naraz; ramki wewnetrzne wyzej zabieraja swoje)
            var disp = typeof(CampaignEventDispatcher);
            frame("tick partii", disp.GetMethod("DailyTickParty", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(MobileParty) }, null), nameof(TickPartyPre));
            frame("tick osady", disp.GetMethod("DailyTickSettlement", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Settlement) }, null), nameof(TickSettlePre));
            frame("tick miasta", disp.GetMethod("DailyTickTown", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Town) }, null), nameof(TickTownPre));
            // podsluch - na koncu, gdy wszystkie ramki sa juz wpiete
            try
            {
                var add = typeof(ItemRoster).GetMethod("AddToCounts", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(EquipmentElement), typeof(int) }, null);
                if (add == null || add.ReturnType != typeof(int)) _missing.Add("podsluch ItemRoster.AddToCounts(EquipmentElement, int)");
                else { h.Patch(add, postfix: new HarmonyMethod(typeof(GoodsLedger), nameof(Tap))); _tapWired = true; }
            }
            catch (Exception e) { _missing.Add("podsluch (" + e.Message + ")"); Log.Error("GoodsLedger.ApplyAll(podsluch)", e); }
            Log.Info("GoodsLedger: ksiega towarow (146) " + (!_tapWired ? "NIECZYNNA - brak podsluchu ItemRoster.AddToCounts" : On ? "CZYNNA" : "wylaczona w MCM (Goods Ledger Enabled)")
                     + " - tylko log, niczego nie zmienia; ramki wpiete (" + _wired.Count + "): " + string.Join(", ", new List<string>(_wired).ToArray())
                     + (_missing.Count > 0 ? "; BRAK: " + string.Join(", ", new List<string>(_missing).ToArray()) : "")
                     + "; linie dnia \"Towary:\" (kazdy towar), \"Towary (utarg wozow):\", \"Towary (bilans):\".");
        }
    }
}
