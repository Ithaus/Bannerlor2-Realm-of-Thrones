using System.Collections.Generic;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace ForgeView
{
    /// <summary>
    /// Wiersze listy zbroi w zakladce CRAFT: wzor nieodkryty na czerwono, znany normalnie -
    /// jak zablokowane czesci broni, zamiast dopisku "- LOCKED" (docs/PLAN-kuznia-1do1.md).
    /// Podmiana (Replace) wezla nazwy na DWA warianty przelaczane IsVisible - zadnego
    /// SetAttribute, bo ten wieszal ekran (patrz komentarz w MaterialRowExtension).
    /// XPath celuje w wiazanie @ItemName, nie w strukture drzewa - przezyje drobne
    /// roznice ukladu miedzy BK z githuba a BK.Redux u Jeffa. Wzor wezla przepisany
    /// ze zrodel BK: GUI/Prefabs/Crafting/ArmorCraftingCategory.xml (RichTextWidget
    /// w ItemTemplate listy SmeltableItemList, DataSource {Armors} = ArmorItemVM).
    /// </summary>
    [PrefabExtension("ArmorCraftingCategory", "descendant::*[@Text='@ItemName']")]
    internal sealed class ArmourListColour : PrefabExtensionInsertPatch
    {
        /// <summary>
        /// True dopiero, gdy XPath trafil i warianty NAPRAWDE weszly do prefabu
        /// (getter Nodes biegnie po udanym SelectSingleNode - sprawdzone w zrodlach
        /// UIExtenderEx, PrefabComponent.RegisterPatch). Armoury.BkArmourList czyta
        /// to pole przez reflection i tylko wtedy zdejmuje dopisek "- LOCKED";
        /// jak latka nie wejdzie (inny prefab w Redux), dopisek zostaje.
        /// </summary>
        public static bool Applied;

        private readonly List<XmlNode> _nodes;

        // 2 = Replace w Prefabs2 (Prepend 0, ReplaceKeepChildren 1, Replace 2, Child 3, Append 4);
        // pierwszy wezel podmienia cel, drugi wchodzi ZA nim jako rodzenstwo
        public override InsertType Type => (InsertType)2;

        [PrefabExtensionXmlNodes]
        public IEnumerable<XmlNode> Nodes
        {
            get
            {
                if (!Applied)
                {
                    Applied = true;
                    Log.Info("ArmourListColour: wiersze listy zbroi dostaly warianty known/locked.");
                }
                return _nodes;
            }
        }

        public ArmourListColour()
        {
            _nodes = new List<XmlNode>
            {
                Row("@FvKnown", null),
                Row("@FvLocked", "#D65252FF")     // czerwien jak PATTERN NOT LEARNED w TableauExtension
            };
        }

        /// <summary>Kopia oryginalnego wezla nazwy z prefabu BK + IsVisible i ew. kolor.</summary>
        private static XmlNode Row(string visible, string colour)
        {
            var xml =
              "<RichTextWidget IsDisabled=\"true\" DoNotAcceptEvents=\"true\" WidthSizePolicy=\"Fixed\" " +
              "HeightSizePolicy=\"StretchToParent\" SuggestedWidth=\"150\" HorizontalAlignment=\"Left\" " +
              "VerticalAlignment=\"Center\" MarginLeft=\"180\" Brush=\"Smelting.Tuple.Text\" " +
              (colour != null ? "Brush.FontColor=\"" + colour + "\" " : "") +
              "Text=\"@ItemName\" Brush.TextHorizontalAlignment=\"Left\" IsVisible=\"" + visible + "\"/>";
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            return doc.DocumentElement;
        }
    }
}
