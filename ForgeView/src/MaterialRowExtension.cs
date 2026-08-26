using System.Collections.Generic;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace ForgeView
{
    /// <summary>
    /// Ikony surowcow, nalozone dokladnie w puste miejsca kafli Banner Kings.
    /// Ich ImageIdentifierWidget wiaze martwe ImageTypeCode, wiec obrazek nigdy sie nie rysuje;
    /// my powtarzamy ta sama geometrie (kotwica Center/Bottom, MarginRight 675, MarginBottom 100,
    /// kafel 95x96, ikona 90x55 nad plakietka 90x24) i rysujemy WYLACZNIE obrazek z poprawnym
    /// wiazaniem TextureProviderName. Liczby i podpowiedzi zostaja ich.
    /// Wstawka typu Insert - sprawdzona; to SetAttribute wieszal ekran, nie ta droga.
    /// </summary>
    [PrefabExtension("Crafting", "descendant::CraftingScreenWidget/Children", "Crafting")]
    internal class MaterialRowExtension : PrefabExtensionInsertPatch
    {
        private readonly List<XmlNode> nodes;
        public override InsertType Type => (InsertType)3;
        public override int Index => 5;

        [PrefabExtensionXmlNodes]
        public IEnumerable<XmlNode> Nodes => nodes;

        public MaterialRowExtension()
        {
            var xml =
              "<Widget DoNotAcceptEvents=\"true\" WidthSizePolicy=\"CoverChildren\" HeightSizePolicy=\"CoverChildren\" " +
              "HorizontalAlignment=\"Center\" VerticalAlignment=\"Bottom\" MarginRight=\"675\" MarginBottom=\"100\"><Children>" +
                "<ListPanel DataSource=\"{CurrentExtraMaterials}\" WidthSizePolicy=\"CoverChildren\" " +
                "HeightSizePolicy=\"CoverChildren\" HorizontalAlignment=\"Center\" VerticalAlignment=\"Bottom\"><ItemTemplate>" +
                  "<Widget DoNotAcceptEvents=\"true\" WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"Fixed\" " +
                  "SuggestedWidth=\"95\" SuggestedHeight=\"96\" VerticalAlignment=\"Bottom\"><Children>" +
                    "<ListPanel WidthSizePolicy=\"CoverChildren\" HeightSizePolicy=\"CoverChildren\" " +
                    "HorizontalAlignment=\"Center\" StackLayout.LayoutMethod=\"VerticalBottomToTop\"><Children>" +
                      "<ImageIdentifierWidget DataSource=\"{Visual}\" WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"Fixed\" " +
                      "SuggestedWidth=\"90\" SuggestedHeight=\"55\" HorizontalAlignment=\"Center\" " +
                      "TextureProviderName=\"@TextureProviderName\" ImageId=\"@Id\" AdditionalArgs=\"@AdditionalArgs\"/>" +
                      "<Widget WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"Fixed\" SuggestedWidth=\"90\" SuggestedHeight=\"24\"/>" +
                    "</Children></ListPanel>" +
                  "</Children></Widget>" +
                "</ItemTemplate></ListPanel>" +
              "</Children></Widget>";
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            nodes = new List<XmlNode> { doc };
            Log.Info("MaterialRowExtension: ikony nalozone w geometrii kafli BK.");
        }
    }
}
