using System.Collections.Generic;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace ForgeView
{
    /// <summary>Model 3D wybranej zbroi, obracany kolkiem - z powrotem, bez podpisu z poziomem.</summary>
    [PrefabExtension("Crafting", "descendant::CraftingScreenWidget/Children", "Crafting")]
    internal class TableauExtension : PrefabExtensionInsertPatch
    {
        private readonly List<XmlNode> nodes;
        public override InsertType Type => (InsertType)3;
        public override int Index => 6;

        [PrefabExtensionXmlNodes]
        public IEnumerable<XmlNode> Nodes => nodes;

        public TableauExtension()
        {
            var xml =
              "<Widget WidthSizePolicy=\"CoverChildren\" HeightSizePolicy=\"CoverChildren\" " +
              "HorizontalAlignment=\"Center\" VerticalAlignment=\"Center\" MarginRight=\"760\" MarginTop=\"170\" " +
              "IsVisible=\"@IsInArmorMode\"><Children>" +
                "<Widget WidthSizePolicy=\"CoverChildren\" HeightSizePolicy=\"CoverChildren\" DataSource=\"{ArmorCrafting}\"><Children>" +
                  "<Widget WidthSizePolicy=\"CoverChildren\" HeightSizePolicy=\"CoverChildren\" DataSource=\"{CurrentItem}\"><Children>" +
                    "<ItemTableauWidget WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"Fixed\" " +
                    "SuggestedWidth=\"460\" SuggestedHeight=\"460\" HorizontalAlignment=\"Center\" " +
                    "InitialTiltRotation=\"0.35\" InitialPanRotation=\"0.6\" " +
                    "StringId=\"@FvItemId\" ItemModifierId=\"@FvModifierId\" BannerCode=\"@FvBannerCode\"/>" +
                  "</Children></Widget>" +
                "</Children></Widget>" +
              "</Children></Widget>";
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            nodes = new List<XmlNode> { doc };
            Log.Info("TableauExtension: podglad 3D przywrocony.");
        }
    }
}
