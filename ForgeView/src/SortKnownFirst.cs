using System;
using System.Collections.Generic;
using System.Reflection;
using BannerKings.UI.Crafting;
using HarmonyLib;
using TaleWorlds.Library;

namespace ForgeView
{
    /// <summary>
    /// ZNANE WZORY NA GORZE EKRANU - PO KAZDEJ PRZEBUDOWIE LISTY (Jeff 26.08: "najpierw
    /// dostepne, zeby nie zjezdzac na dol"; Jeff 16.10: "zeby rzeczy dostepne nie byly na
    /// samym dole listy tylko na gorze").
    ///
    /// Dlaczego do 16.10 znane byly na DOLE (sprawdzone w kodzie):
    /// (1) prefab BK ArmorCraftingCategory.xml - lista wzorow (ListPanel Id="SmeltableItemList",
    ///     DataSource="{Armors}") ma StackLayout.LayoutMethod="VerticalBottomToTop": GauntletUI
    ///     StackLayout.LayoutLinearVertical kladzie dziecko 0 NA DOLE, kazde nastepne nad nim.
    ///     Pozycja 0 listy = najnizszy kafelek. Filtr (ArmourFilterMixin.Apply) ustawial znane na
    ///     POCZATKU listy - wiec na ekranie ladowaly na samym dole.
    /// (2) stary postfiks wisial na SortByCurrentState, ktorej BK nie wola nigdzie (dekompilacja
    ///     BannerKings.dll 1:1 z gra): przyciski Type/Name/Yield wolaja ExecuteSortByType/Name/Yield,
    ///     a te sortuja _listToControl wprost - przegrupowanie po sortowaniu nie biegalo nigdy.
    ///
    /// Teraz: zamkniete na poczatku listy, znane na KONCU (= na gorze ekranu); w obu grupach
    /// porzadek sortowania BK zachowany (przegrupowanie stabilne). Sciezki przebudowy listy:
    /// - otwarcie zakladki zbroi (CraftingMixin -> ArmorCraftingVM.RefreshValues -> nasz
    ///   ArmourFilterMixin.OnRefresh -> Apply), przycisk tieru, kategoria - Apply uklada liste
    ///   w pamieci przez Arrange (sort BK wybranego przycisku + grupy) i wstawia ja raz;
    /// - przyciski sortowania BK (ExecuteSortByName/Yield/Type) i SortByCurrentState - postfiks
    ///   przegrupowuje na miejscu (Regroup, MBBindingList.Sort - kafelki tylko przestawione).
    /// Kucie (CraftingMixin.ExecuteMainActionBK) listy nie przebudowuje - nic do roboty.
    /// Wszystko tylko po zdarzeniu, nigdy co klatke.
    /// </summary>
    internal static class SortKnownFirst
    {
        private static FieldInfo _fList;                    // ArmorCraftingSortController._listToControl
        private static FieldInfo _fName, _fYield, _fType;   // komparatory BK (kierunek z ostatniego klikniecia)
        private static bool _looked;
        private static int _stumbles;                       // potkniecia - licznik, bez wylacznika

        private static readonly string[] Targets = { "ExecuteSortByName", "ExecuteSortByYield", "ExecuteSortByType", "SortByCurrentState" };
        private static readonly string[] Posts = { "AfterName", "AfterYield", "AfterType", "AfterCurrent" };

        /// <summary>Postfiksy na metodach DEKLAROWANYCH w ArmorCraftingSortController - kazda osobno.</summary>
        internal static void Apply(Harmony h)
        {
            var ok = new List<string>();
            var miss = new List<string>();
            for (int i = 0; i < Targets.Length; i++)
            {
                try
                {
                    var target = AccessTools.DeclaredMethod(typeof(ArmorCraftingSortController), Targets[i], Type.EmptyTypes);
                    var post = AccessTools.DeclaredMethod(typeof(SortKnownFirst), Posts[i]);
                    if (target == null || post == null) { miss.Add(Targets[i]); continue; }
                    h.Patch(target, postfix: new HarmonyMethod(post));
                    ok.Add(Targets[i]);
                }
                catch (Exception e) { miss.Add(Targets[i]); Log.Error("SortKnownFirst.Apply " + Targets[i], e); }
            }
            Log.Info("SortKnownFirst: znane wzory na gorze ekranu (lista BK rysuje sie od dolu) - postfiksy wpiete "
                     + ok.Count + "/" + Targets.Length + " (" + string.Join(", ", ok.ToArray()) + ")"
                     + (miss.Count > 0 ? "; BRAK: " + string.Join(", ", miss.ToArray()) : "") + ".");
        }

        private static void AfterName(ArmorCraftingSortController __instance)    { AfterSort(__instance, "przycisk Name"); }
        private static void AfterYield(ArmorCraftingSortController __instance)   { AfterSort(__instance, "przycisk Yield"); }
        private static void AfterType(ArmorCraftingSortController __instance)    { AfterSort(__instance, "przycisk Type"); }
        private static void AfterCurrent(ArmorCraftingSortController __instance) { AfterSort(__instance, "SortByCurrentState"); }

        private static void AfterSort(ArmorCraftingSortController c, string path)
        {
            try
            {
                Look();
                var list = c != null && _fList != null ? _fList.GetValue(c) as MBBindingList<ArmorItemVM> : null;
                if (list == null) return;
                Regroup(list, path, SortName(c));
            }
            catch (Exception e) { _stumbles++; Log.Error("SortKnownFirst " + path, e); }
        }

        private static void Look()
        {
            if (_looked) return;
            _looked = true;
            var t = typeof(ArmorCraftingSortController);
            _fList = AccessTools.Field(t, "_listToControl");
            _fName = AccessTools.Field(t, "_nameComparer");
            _fYield = AccessTools.Field(t, "_yieldComparer");
            _fType = AccessTools.Field(t, "_typeComparer");
            Log.Info("SortKnownFirst: pola sortownika BK - lista " + (_fList != null ? "TAK" : "BRAK")
                     + ", komparatory Name/Yield/Type " + (_fName != null ? "TAK" : "BRAK") + "/"
                     + (_fYield != null ? "TAK" : "BRAK") + "/" + (_fType != null ? "TAK" : "BRAK") + ".");
        }

        // ta sama miarka co kafelek (FvLocked) i filtr: Armoury.RangedLore.KnownOf
        private static bool IsOpen(ArmorItemVM vm)
        {
            return vm.Item == null || ArmourFilterMixin.Known(vm.Item);
        }

        private static string SortName(ArmorCraftingSortController c)
        {
            if (c == null) return "brak sortownika";
            if (c.IsNameSelected) return "Name, stan " + c.NameState;
            if (c.IsYieldSelected) return "Yield, stan " + c.YieldState;
            if (c.IsTypeSelected) return "Type, stan " + c.TypeState;
            return "brak (kolejnosc BK z odswiezenia)";
        }

        /// <summary>
        /// Przebudowa listy filtrem (Apply): uklad w pamieci - najpierw sort BK wedlug przycisku
        /// wybranego na ekranie (ten sam komparator BK i kierunek), potem stabilnie: zamkniete,
        /// na koncu znane. Lista wejsciowa jest sortowana w miejscu.
        /// </summary>
        internal static List<ArmorItemVM> Arrange(ArmorCraftingSortController c, List<ArmorItemVM> items,
                                                  out int open, out int shut, out string sort)
        {
            sort = SortLikeBk(c, items);
            var shutL = new List<ArmorItemVM>(items.Count);
            var openL = new List<ArmorItemVM>();
            foreach (var vm in items)
            {
                if (vm == null) continue;
                (IsOpen(vm) ? openL : shutL).Add(vm);
            }
            open = openL.Count;
            shut = shutL.Count;
            shutL.AddRange(openL);
            return shutL;
        }

        private static string SortLikeBk(ArmorCraftingSortController c, List<ArmorItemVM> items)
        {
            try
            {
                if (c == null) return "brak sortownika";
                Look();
                string name = SortName(c);
                FieldInfo f = c.IsNameSelected ? _fName : c.IsYieldSelected ? _fYield : c.IsTypeSelected ? _fType : null;
                if (f == null) return name;   // zaden przycisk - kolejnosc BK z odswiezenia
                var cmp = f.GetValue(c) as IComparer<ArmorItemVM>;
                if (cmp == null) return name + " - BRAK komparatora, kolejnosc BK z odswiezenia";
                if (items.Count > 1) items.Sort(cmp);
                return name;
            }
            catch (Exception e) { _stumbles++; Log.Error("SortKnownFirst.SortLikeBk", e); return "blad sortu BK (kolejnosc bez zmian)"; }
        }

        /// <summary>
        /// Przegrupowanie listy juz pokazanej (po sortowaniu BK): MBBindingList.Sort komparatorem
        /// rangi - GauntletView.OnListSorted tylko przestawia istniejace kafelki (bez budowania od
        /// nowa). Ranga = pozycja w obecnej kolejnosci BK, znane przesuniete o n - wiec stabilnie.
        /// </summary>
        internal static void Regroup(MBBindingList<ArmorItemVM> list, string path, string sort)
        {
            int n = list.Count;
            var rank = new Dictionary<ArmorItemVM, int>(n);
            int open = 0, shut = 0;
            for (int i = 0; i < n; i++)
            {
                var vm = list[i];
                if (vm == null || rank.ContainsKey(vm)) continue;
                bool known = IsOpen(vm);
                if (known) open++; else shut++;
                rank[vm] = (known ? n : 0) + i;
            }
            bool moved = false;
            if (open > 0 && shut > 0)
            {
                var cmp = new RankComparer(rank);
                moved = !list.IsOrdered(cmp);
                if (moved) list.Sort(cmp);
            }
            Line(path, open, shut, sort, moved);
        }

        /// <summary>Jedna linia na przegrupowanie: sciezka, ile znanych i zamknietych, sort BK.</summary>
        internal static void Line(string path, int open, int shut, string sort, bool moved)
        {
            Log.Info("Znane na gorze [" + path + "]: znane " + open + " na gorze ekranu (koniec listy), zamkniete "
                     + shut + " pod nimi | sort BK: " + sort + " | przestawiono: " + (moved ? "tak" : "nie")
                     + (_stumbles > 0 ? " | potkniecia " + _stumbles : "") + ".");
        }

        private sealed class RankComparer : IComparer<ArmorItemVM>
        {
            private readonly Dictionary<ArmorItemVM, int> _rank;
            internal RankComparer(Dictionary<ArmorItemVM, int> rank) { _rank = rank; }

            private int R(ArmorItemVM vm)
            {
                int r;
                return vm != null && _rank.TryGetValue(vm, out r) ? r : -1;
            }

            // -1/0/1 - MBBindingList.IsOrdered sprawdza "== 1"
            public int Compare(ArmorItemVM x, ArmorItemVM y) { return R(x).CompareTo(R(y)); }
        }
    }
}
