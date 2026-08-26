using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using BannerKings.UI.Crafting;
using TaleWorlds.Library;

namespace ForgeView
{
    /// <summary>Dane, ktorych ItemTableauWidget potrzebuje do narysowania zbroi w 3D.</summary>
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
    }
}
