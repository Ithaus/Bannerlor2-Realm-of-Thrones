using System;
using System.Reflection;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using BannerKings.UI.Crafting;
using TaleWorlds.Library;

namespace ForgeView
{
    /// <summary>Dane, ktorych ItemTableauWidget potrzebuje do narysowania zbroi w 3D
    /// + stan wzoru (znany/nieznany) do kolorowego oznaczenia jak przy broniach.</summary>
    [ViewModelMixin]
    internal sealed class ArmourItemMixin : BaseViewModelMixin<ArmorItemVM>
    {
        public ArmourItemMixin(ArmorItemVM vm) : base(vm) { }

        [DataSourceProperty]
        public string FvItemId
        {
            get { try { return ViewModel != null && ViewModel.Item != null ? ViewModel.Item.StringId : ""; } catch { return ""; } }
        }

        [DataSourceProperty] public string FvModifierId { get { return ""; } }
        [DataSourceProperty] public string FvBannerCode { get { return ""; } }

        // ---- wzor znany czy nie (Armoury.RangedLore przez reflection, bez twardej zaleznosci) ----
        private static MethodInfo _knownOf;
        private static bool _knownLooked;

        private static bool KnownOf(TaleWorlds.Core.ItemObject item)
        {
            try
            {
                if (!_knownLooked)
                {
                    _knownLooked = true;
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var t = asm.GetType("Armoury.RangedLore", false);
                        if (t != null) { _knownOf = t.GetMethod("KnownOf", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static); break; }
                    }
                }
                if (_knownOf == null || item == null) return true;
                return _knownOf.Invoke(null, new object[] { item }) is bool b && b;
            }
            catch { return true; }
        }

        [DataSourceProperty]
        public bool FvLocked
        {
            get { try { return ViewModel != null && ViewModel.Item != null && !KnownOf(ViewModel.Item); } catch { return false; } }
        }

        [DataSourceProperty]
        public bool FvKnown
        {
            get { return !FvLocked; }
        }
    }
}
