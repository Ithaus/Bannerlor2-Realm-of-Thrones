using System;
using System.Collections.Generic;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace RealisticCaptivity
{
    /// <summary>
    /// Zakladka Klan -> Other: czwarta grupa "Houses" pod Supporters.
    /// Kazdy dom pokazuje osade i zloto lezace w skrzyni. Ten sam mechanizm,
    /// ktorym Banner Kings dodaje tam Estates: mixin na ClanIncomeVM + dwa
    /// InsertPatche na prefab ClanIncome (przycisk-naglowek i lista).
    /// Append wzgledem elementow Supporters - odporne na indeksy innych modow.
    /// </summary>
    public class RcHouseVM : ViewModel
    {
        private string _name;
        private string _location;
        private string _income;

        public RcHouseVM(string name, string location, string income)
        { _name = name; _location = location; _income = income; }

        [DataSourceProperty]
        public string Name
        {
            get { return _name; }
            set { if (value != _name) { _name = value; OnPropertyChangedWithValue(value, "Name"); } }
        }

        [DataSourceProperty]
        public string Location
        {
            get { return _location; }
            set { if (value != _location) { _location = value; OnPropertyChangedWithValue(value, "Location"); } }
        }

        [DataSourceProperty]
        public string IncomeText
        {
            get { return _income; }
            set { if (value != _income) { _income = value; OnPropertyChangedWithValue(value, "IncomeText"); } }
        }
    }

    [ViewModelMixin("RefreshList")]
    internal sealed class ClanHousesMixin : BaseViewModelMixin<ClanIncomeVM>
    {
        private MBBindingList<RcHouseVM> _houses = new MBBindingList<RcHouseVM>();
        private string _housesText = "Houses (0)";

        public ClanHousesMixin(ClanIncomeVM vm) : base(vm) { }

        [DataSourceProperty]
        public MBBindingList<RcHouseVM> RcHouses
        {
            get { return _houses; }
            set { if (value != _houses) { _houses = value; if (ViewModel != null) ViewModel.OnPropertyChangedWithValue(value, "RcHouses"); } }
        }

        [DataSourceProperty]
        public string RcHousesText
        {
            get { return _housesText; }
            set { if (value != _housesText) { _housesText = value; if (ViewModel != null) ViewModel.OnPropertyChangedWithValue(value, "RcHousesText"); } }
        }

        public override void OnRefresh()
        {
            try
            {
                _houses.Clear();
                int n = 0;
                if (Homes.Vault != null)
                {
                    foreach (var pair in Homes.Vault)
                    {
                        var s = Settlement.Find(pair.Key);
                        if (s == null) continue;
                        int items = 0;
                        try
                        {
                            if (Homes.Stash != null && Homes.Stash.ContainsKey(pair.Key) && Homes.Stash[pair.Key] != null)
                                items = CountItems(pair.Key);
                        }
                        catch { }
                        string kind = s.IsVillage ? "Cottage" : "Townhouse";
                        _houses.Add(new RcHouseVM(kind, s.Name.ToString(),
                            pair.Value + (items > 0 ? " (+" + items + " items)" : "")));
                        n++;
                    }
                }
                RcHousesText = "Houses (" + n + ")";
                if (ViewModel != null)
                {
                    ViewModel.OnPropertyChangedWithValue(_houses, "RcHouses");
                    ViewModel.OnPropertyChangedWithValue(_housesText, "RcHousesText");
                }
            }
            catch (Exception e) { Log.Error("ClanHousesMixin.OnRefresh", e); }
        }

        private static int CountItems(string id)
        {
            try
            {
                int total = 0;
                var roster = Homes.Stash[id];
                for (int i = 0; i < roster.Count; i++) total += roster.GetElementNumber(i);
                return total;
            }
            catch { return 0; }
        }
    }

    /// <summary>Przycisk-naglowek "Houses (N)" - doklejony za przyciskiem Supporters.</summary>
    [PrefabExtension("ClanIncome", "descendant::PartyHeaderToggleWidget[@Id='SupportersToggleButton']", "ClanIncome")]
    internal sealed class HousesButtonExtension : PrefabExtensionInsertPatch
    {
        private readonly List<XmlNode> _nodes;
        public override InsertType Type { get { return InsertType.Append; } }

        [PrefabExtensionXmlNodes]
        public IEnumerable<XmlNode> Nodes { get { return _nodes; } }

        public HousesButtonExtension()
        {
            var doc = new XmlDocument();
            doc.LoadXml(
                "<PartyHeaderToggleWidget Id=\"RcHousesToggleButton\" DoNotPassEventsToChildren=\"true\" WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"Fixed\" SuggestedWidth=\"!Clan.Management.Collapser.Width\" SuggestedHeight=\"!Clan.Management.Collapser.Height\" CollapseIndicator=\"RcHousesCollapser\\RcHousesCollapseIndicator\" ListPanel=\"..\\ClanElementsRect\\ClanElementsListPanel\\RcHousesList\" HorizontalAlignment=\"Left\" VerticalAlignment=\"Top\" Brush=\"Clan.Management.Collapser\" RenderLate=\"true\" WidgetToClose=\"..\\ClanElementsRect\\ClanElementsListPanel\\RcHousesList\">" +
                "<Children>" +
                "<ListPanel Id=\"RcHousesCollapser\" WidthSizePolicy=\"CoverChildren\" HeightSizePolicy=\"CoverChildren\" HorizontalAlignment=\"Center\" VerticalAlignment=\"Center\">" +
                "<Children>" +
                "<BrushWidget Id=\"RcHousesCollapseIndicator\" WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"Fixed\" SuggestedWidth=\"!Party.Toggle.ExpandIndicator.Width\" SuggestedHeight=\"!Party.Toggle.ExpandIndicator.Height\" VerticalAlignment=\"Center\" PositionYOffset=\"-2\" MarginRight=\"5\" Brush=\"Party.Toggle.ExpandIndicator\" />" +
                "<TextWidget WidthSizePolicy=\"CoverChildren\" HeightSizePolicy=\"CoverChildren\" Brush=\"Clan.Management.Collapser.Text\" Text=\"@RcHousesText\" />" +
                "</Children>" +
                "</ListPanel>" +
                "</Children>" +
                "</PartyHeaderToggleWidget>");
            _nodes = new List<XmlNode> { doc };
        }
    }

    /// <summary>Naglowek i lista domow - doklejone za lista Supporters.</summary>
    [PrefabExtension("ClanIncome", "descendant::NavigatableListPanel[@Id='SupportersList']", "ClanIncome")]
    internal sealed class HousesListExtension : PrefabExtensionInsertPatch
    {
        private readonly List<XmlNode> _nodes;
        public override InsertType Type { get { return InsertType.Append; } }

        [PrefabExtensionXmlNodes]
        public IEnumerable<XmlNode> Nodes { get { return _nodes; } }

        public HousesListExtension()
        {
            var a = new XmlDocument();
            a.LoadXml("<NavigationAutoScrollWidget TrackedWidget=\"..\\RcHousesHeader\" />");
            var b = new XmlDocument();
            b.LoadXml("<ScrollablePanelFixedHeaderWidget Id=\"RcHousesHeader\" FixedHeader=\"..\\..\\..\\RcHousesToggleButton\" WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"Fixed\" SuggestedWidth=\"!Clan.Management.Collapser.Width\" HeaderHeight=\"!Clan.Management.Collapser.Height\" AdditionalBottomOffset=\"!Clan.Management.Collapser.Height\" />");
            var c = new XmlDocument();
            c.LoadXml(
                "<NavigatableListPanel Id=\"RcHousesList\" DataSource=\"{RcHouses}\" WidthSizePolicy=\"CoverChildren\" HeightSizePolicy=\"CoverChildren\" HorizontalAlignment=\"Right\" StackLayout.LayoutMethod=\"VerticalTopToBottom\" UseSelfIndexForMinimum=\"true\">" +
                "<ItemTemplate>" +
                "<ListPanel WidthSizePolicy=\"CoverChildren\" HeightSizePolicy=\"Fixed\" SuggestedHeight=\"40\" MarginTop=\"2\" MarginBottom=\"2\">" +
                "<Children>" +
                "<TextWidget WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"CoverChildren\" SuggestedWidth=\"170\" VerticalAlignment=\"Center\" Brush=\"Clan.Management.Collapser.Text\" Brush.FontSize=\"22\" Text=\"@Name\" />" +
                "<TextWidget WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"CoverChildren\" SuggestedWidth=\"180\" VerticalAlignment=\"Center\" Brush=\"Clan.Management.Collapser.Text\" Brush.FontSize=\"22\" Text=\"@Location\" />" +
                "<TextWidget WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"CoverChildren\" SuggestedWidth=\"150\" VerticalAlignment=\"Center\" Brush=\"Clan.Management.Collapser.Text\" Brush.FontSize=\"22\" Text=\"@IncomeText\" />" +
                "</Children>" +
                "</ListPanel>" +
                "</ItemTemplate>" +
                "</NavigatableListPanel>");
            _nodes = new List<XmlNode> { a, b, c };
        }
    }
}
