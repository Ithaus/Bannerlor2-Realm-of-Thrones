using System;
using System.Collections.Generic;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using BannerKings.UI.Crafting;
using TaleWorlds.Library;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ForgeView
{
    /// <summary>
    /// Panel zbroi w Banner Kings wypisuje kazdy przedmiot w grze jednym ciagiem.
    /// Przy ROT to tysiace pozycji. Dokladamy do niego filtr poziomow - bez ruszania
    /// jego kodu, przez mixin UIExtenderEx: nasze wlasciwosci dokladaja sie do istniejacego
    /// widoku, a lista jest ta sama, tylko przesiana.
    /// </summary>
    [ViewModelMixin("RefreshValues")]
    internal sealed class ArmourFilterMixin : BaseViewModelMixin<ArmorCraftingVM>
    {
        private readonly List<ArmorItemVM> _all = new List<ArmorItemVM>();
        private int _tier;          // 0 = wszystkie
        private int _cat;           // 0 = wszystkie kategorie

        public ArmourFilterMixin(ArmorCraftingVM vm) : base(vm) { }

        // ---- stan przyciskow ----
        [DataSourceProperty] public bool FvVisible { get { return true; } }
        [DataSourceProperty] public string FvLabel { get { return "Tier"; } }
        [DataSourceProperty] public string FvAllText { get { return "All"; } }
        [DataSourceProperty] public int FvTier { get { return _tier; } }

        [DataSourceProperty] public bool FvAllOn  { get { return _tier == 0; } }
        [DataSourceProperty] public bool FvT1On   { get { return _tier == 1; } }
        [DataSourceProperty] public bool FvT2On   { get { return _tier == 2; } }
        [DataSourceProperty] public bool FvT3On   { get { return _tier == 3; } }
        [DataSourceProperty] public bool FvT4On   { get { return _tier == 4; } }
        [DataSourceProperty] public bool FvT5On   { get { return _tier == 5; } }
        [DataSourceProperty] public bool FvT6On   { get { return _tier == 6; } }

        private static readonly string[] CatNames =
        {
            "All Armour", "Helmets", "Body Armour", "Boots and Greaves",
            "Gauntlets", "Shoulders and Cloaks", "Shields", "Barding",
            "Bows", "Crossbows", "Arrows", "Bolts"
        };

        [DataSourceProperty] public string FvCategoryText { get { return CatNames[_cat]; } }

        [DataSourceProperty] public bool FvCAllOn { get { return _cat == 0; } }
        [DataSourceProperty] public bool FvCHeadOn { get { return _cat == 1; } }
        [DataSourceProperty] public bool FvCBodyOn { get { return _cat == 2; } }
        [DataSourceProperty] public bool FvCLegOn  { get { return _cat == 3; } }
        [DataSourceProperty] public bool FvCHandOn { get { return _cat == 4; } }
        [DataSourceProperty] public bool FvCCapeOn { get { return _cat == 5; } }
        [DataSourceProperty] public bool FvCShieldOn { get { return _cat == 6; } }
        [DataSourceProperty] public bool FvCHorseOn  { get { return _cat == 7; } }

        [DataSourceProperty] public string FvCountText
        {
            get
            {
                try { return ViewModel != null && ViewModel.Armors != null ? ViewModel.Armors.Count + " pieces" : ""; }
                catch { return ""; }
            }
        }

        // ---- polecenia z przyciskow ----
        [DataSourceMethod]
        public void ExecuteFvAll() { Apply(0); }
        [DataSourceMethod]
        public void ExecuteFvT1()  { Apply(1); }
        [DataSourceMethod]
        public void ExecuteFvT2()  { Apply(2); }
        [DataSourceMethod]
        public void ExecuteFvT3()  { Apply(3); }
        [DataSourceMethod]
        public void ExecuteFvT4()  { Apply(4); }
        [DataSourceMethod]
        public void ExecuteFvT5()  { Apply(5); }
        [DataSourceMethod]
        public void ExecuteFvT6()  { Apply(6); }

        [DataSourceMethod]
        public void ExecuteFvCAll()    { Cat(0); }
        [DataSourceMethod]
        public void ExecuteFvCHead()   { Cat(1); }
        [DataSourceMethod]
        public void ExecuteFvCBody()   { Cat(2); }
        [DataSourceMethod]
        public void ExecuteFvCLeg()    { Cat(3); }
        [DataSourceMethod]
        public void ExecuteFvCHand()   { Cat(4); }
        [DataSourceMethod]
        public void ExecuteFvCCape()   { Cat(5); }
        [DataSourceMethod]
        public void ExecuteFvCShield() { Cat(6); }
        [DataSourceMethod]
        public void ExecuteFvCHorse()  { Cat(7); }

        private void Cat(int c) { _cat = c; Apply(_tier); }

        /// <summary>Wybor kategorii w natywnym oknie - odpowiednik "CHOOSE WHAT TO CRAFT" przy broni.</summary>
        [DataSourceMethod]
        public void ExecuteFvPickCategory()
        {
            try
            {
                if (_all.Count == 0) Snapshot();
                InjectRanged();
                InjectMasterworks();
                var counts = new int[CatNames.Length];
                foreach (var vm in _all)
                    if (vm != null && vm.Item != null) counts[CatOf(vm.Item)]++;
                counts[0] = _all.Count;

                var elements = new List<InquiryElement>();
                for (int i = 0; i < CatNames.Length; i++)
                    elements.Add(new InquiryElement(i, CatNames[i] + "  (" + counts[i] + ")", null,
                                                    counts[i] > 0, null));

                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    "What Will You Make?",
                    "Pick the kind of piece to lay on the anvil.",
                    elements, true, 1, 1, "Choose", "Back",
                    delegate (List<InquiryElement> sel)
                    {
                        try
                        {
                            if (sel == null || sel.Count == 0) return;
                            Cat((int)sel[0].Identifier);
                        }
                        catch (Exception ex) { Log.Error("PickCategory.Selected", ex); }
                    },
                    delegate (List<InquiryElement> _) { }), true);
            }
            catch (Exception e) { Log.Error("ExecuteFvPickCategory", e); }
        }

        private static int CatOf(ItemObject it)
        {
            switch (it.ItemType)
            {
                case ItemObject.ItemTypeEnum.HeadArmor:    return 1;
                case ItemObject.ItemTypeEnum.BodyArmor:    return 2;
                case ItemObject.ItemTypeEnum.LegArmor:     return 3;
                case ItemObject.ItemTypeEnum.HandArmor:    return 4;
                case ItemObject.ItemTypeEnum.Cape:         return 5;
                case ItemObject.ItemTypeEnum.Shield:       return 6;
                case ItemObject.ItemTypeEnum.HorseHarness: return 7;
                case ItemObject.ItemTypeEnum.Bow:          return 8;
                case ItemObject.ItemTypeEnum.Crossbow:     return 9;
                case ItemObject.ItemTypeEnum.Arrows:       return 10;
                case ItemObject.ItemTypeEnum.Bolts:        return 11;
                default: return 0;
            }
        }

        /// <summary>Machiny, sprzet cwiczebny i smieci testowe nie leza na tej polce.</summary>
        private static bool BannedRanged(ItemObject it)
        {
            try
            {
                if (it.WeaponComponent == null) return true;
                string sId = (((TaleWorlds.ObjectSystem.MBObjectBase)it).StringId ?? "").ToLowerInvariant();
                string nm = it.Name != null ? it.Name.ToString().ToLowerInvariant() : "";
                string[] bans = { "ballista", "catapult", "trebuchet", "boulder", "siege", "practice", "tournament", "dummy", "test_", "_test" };
                foreach (var b in bans)
                    if (sId.Contains(b) || nm.Contains(b)) return true;
                return false;
            }
            catch { return false; }
        }

        /// <summary>Czy gracz odkryl juz ten wzor - pytamy Armoury przez refleksje.</summary>
        private static System.Reflection.MethodInfo _knownOf;
        private static bool _knownLooked;

        // internal: SortKnownFirst przegrupowuje lista po sortowaniu BK ta sama miarka
        internal static bool Known(ItemObject it)
        {
            try
            {
                if (!_knownLooked)
                {
                    _knownLooked = true;
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var t = asm.GetType("Armoury.RangedLore");
                        if (t == null) continue;
                        _knownOf = t.GetMethod("KnownOf",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                            | System.Reflection.BindingFlags.Static);
                        break;
                    }
                }
                if (_knownOf == null) return true;
                return (bool)_knownOf.Invoke(null, new object[] { it });
            }
            catch { return true; }
        }

        private static bool RangedType(ItemObject it)
        {
            if (it == null) return false;
            var t = it.ItemType;
            return t == ItemObject.ItemTypeEnum.Bow || t == ItemObject.ItemTypeEnum.Crossbow
                || t == ItemObject.ItemTypeEnum.Arrows || t == ItemObject.ItemTypeEnum.Bolts;
        }

        private bool _rangedInjected;

        /// <summary>
        /// LUCZARNIA NA POLCE PANCERZY (Jeff): luki, kusze, strzaly i belty
        /// wchodza do tej samej listy co zbroje - z pelnym widokiem 3D wyrobu
        /// i wartosciami. Wstrzykujemy je TUTAJ (nie w Armoury), zeby filtr
        /// kategorii i tierow widzial je w swojej pelnej liscie.
        /// </summary>
        private void InjectRanged()
        {
            try
            {
                if (_rangedInjected || ViewModel == null) return;
                _rangedInjected = true;
                int added = 0;
                foreach (var item in TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (!RangedType(item) || BannedRanged(item)) continue;
                    try
                    {
                        _all.Add(new ArmorItemVM(ViewModel, item, ArmorCraftingVM.ItemType.Ammo));
                        added++;
                    }
                    catch { }
                }
                if (added > 0) Log.Info("Luczarnia na polce: dolozono " + added + " wyrobow strzeleckich.");
            }
            catch (Exception e) { Log.Error("InjectRanged", e); }
        }

        private readonly HashSet<string> _mwInjected = new HashSet<string>();
        private static System.Reflection.FieldInfo _fLore;
        private static bool _loreLooked;

        /// <summary>Nauczone wzory unikatow z Armoury (przetopione egzemplarze) -
        /// przez reflection, bo ForgeView nie referencuje Armoury.</summary>
        private static List<string> LearnedLore()
        {
            try
            {
                if (!_loreLooked)
                {
                    _loreLooked = true;
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var t = asm.GetType("Armoury.ArmouryBehavior");
                        if (t == null) continue;
                        _fLore = t.GetField("UniqueLore",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                            | System.Reflection.BindingFlags.Static);
                        break;
                    }
                }
                return _fLore != null ? _fLore.GetValue(null) as List<string> : null;
            }
            catch { return null; }
        }

        /// <summary>
        /// NAUCZONE UNIKATY NA POLCE CRAFT (Jeff 30.08: "kucie jest w kuzni,
        /// a dokladnie w craft"). BK odrzuca z listy itemy NotMerchandise -
        /// a unikaty imienne wlasnie takie sa (mend CrashScribe). Dokladamy
        /// wiec wylacznie te NAUCZONE (przetopiony egzemplarz w Smelt);
        /// RangedLore.KnownOf otwiera je, wiec laduja w znanych, gotowe do kucia.
        /// </summary>
        private void InjectMasterworks()
        {
            try
            {
                if (ViewModel == null) return;
                var lore = LearnedLore();
                if (lore == null || lore.Count == 0) return;
                int added = 0;
                for (int i = 0; i < lore.Count; i++)
                {
                    var id = lore[i];
                    if (id == null || _mwInjected.Contains(id)) continue;
                    var item = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemObject>(id);
                    if (item == null) continue;
                    try
                    {
                        _all.Add(new ArmorItemVM(ViewModel, item, ViewModel.GetItemType(item)));
                        _mwInjected.Add(id);
                        added++;
                    }
                    catch { }
                }
                if (added > 0) Log.Info("Nauczone unikaty na polce CRAFT: +" + added + ".");
            }
            catch (Exception e) { Log.Error("InjectMasterworks", e); }
        }

        /// <summary>Po kazdym odswiezeniu panelu zapamietujemy pelna liste i nakladamy filtr na nowo.</summary>
        public override void OnRefresh()
        {
            try
            {
                Snapshot();
                InjectRanged();
                InjectMasterworks();
                Apply(_tier);
            }
            catch (Exception e) { Log.Error("OnRefresh", e); }
        }

        private void Snapshot()
        {
            var list = ViewModel != null ? ViewModel.Armors : null;
            if (list == null || list.Count == 0) return;
            // przesiana lista jest zawsze KROTSZA od pelnej - pelna bierzemy
            // tylko, gdy panel dal wiecej, niz znamy (swieze odswiezenie BK)
            if (list.Count <= _all.Count) return;
            _all.Clear();
            foreach (var vm in list) _all.Add(vm);
            _rangedInjected = false;   // nowa pelna lista - luczarnia do dolozenia od nowa
            _mwInjected.Clear();       // ... i nauczone unikaty tez
            Log.Info("Zapamietano pelna liste zbroi: " + _all.Count);
        }

        private void Apply(int tier)
        {
            try
            {
                _tier = tier;
                var list = ViewModel != null ? ViewModel.Armors : null;
                if (list == null) return;
                if (_all.Count == 0) Snapshot();
                if (_all.Count == 0) return;

                list.Clear();
                // najpierw to, co UMIESZ wykuc, potem zamkniete wzory - zeby polka
                // otwierala sie na robocie, a nie na kloktach
                var open = new List<ArmorItemVM>();
                var shut = new List<ArmorItemVM>();
                foreach (var vm in _all)
                {
                    if (vm == null || vm.Item == null) continue;
                    if (tier != 0 && (int)vm.Item.Tier + 1 != tier) continue;
                    if (_cat != 0 && CatOf(vm.Item) != _cat) continue;
                    (Known(vm.Item) ? open : shut).Add(vm);
                }
                foreach (var vm in open) list.Add(vm);
                foreach (var vm in shut) list.Add(vm);
                if (list.Count > 0) ViewModel.CurrentItem = list[0];

                OnPropertyChanged("FvTier");
                OnPropertyChanged("FvAllOn"); OnPropertyChanged("FvT1On"); OnPropertyChanged("FvT2On");
                OnPropertyChanged("FvT3On");  OnPropertyChanged("FvT4On"); OnPropertyChanged("FvT5On");
                OnPropertyChanged("FvT6On");  OnPropertyChanged("FvCountText");
                OnPropertyChanged("FvCAllOn"); OnPropertyChanged("FvCHeadOn"); OnPropertyChanged("FvCBodyOn");
                OnPropertyChanged("FvCLegOn"); OnPropertyChanged("FvCHandOn"); OnPropertyChanged("FvCCapeOn");
                OnPropertyChanged("FvCShieldOn"); OnPropertyChanged("FvCHorseOn");
                OnPropertyChanged("FvCategoryText");
                Log.Info("Filtr: poziom " + tier + ", kategoria " + _cat + " -> pozycji: " + list.Count);
            }
            catch (Exception e) { Log.Error("Apply", e); }
        }
    }
}
