using System.Collections.Generic;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace ForgeView
{
    /// <summary>
    /// Wiersze listy zbroi w zakladce CRAFT: wzor NIEODKRYTY dostaje szara plachte
    /// przyciemniajaca CALY kafelek (portret + napisy) - jak nieposiadane czesci
    /// broni w kuzni; znany wzor zostaje w naturalnych barwach. Wczesniejsza wersja
    /// barwila tylko napis nazwy na czerwono - Jeff chcial caly kafelek.
    /// Append za ostatnim widgetem kafelka (rodzenstwo, nie SetAttribute - ten
    /// wieszal ekran, patrz komentarz w MaterialRowExtension), wiec plachta
    /// renderuje sie NA WIERZCHU wszystkich dzieci ButtonWidgeta.
    /// XPath celuje w wiazanie @ItemTypeText (ostatnie dziecko kafelka w prefabu
    /// BK ArmorCraftingCategory.xml na dysku Jeffa - sprawdzone w pliku modulu).
    /// </summary>
    [PrefabExtension("ArmorCraftingCategory", "descendant::*[@Text='@ItemTypeText']")]
    internal sealed class ArmourListColour : PrefabExtensionInsertPatch
    {
        /// <summary>
        /// True dopiero, gdy XPath trafil i plachta NAPRAWDE weszla do prefabu
        /// (getter Nodes biegnie po udanym SelectSingleNode - sprawdzone w zrodlach
        /// UIExtenderEx, PrefabComponent.RegisterPatch). Armoury.BkArmourList czyta
        /// to pole przez reflection i tylko wtedy zdejmuje dopisek "- LOCKED";
        /// jak latka nie wejdzie (inny prefab), dopisek zostaje jako zapas.
        /// </summary>
        public static bool Applied;

        private readonly List<XmlNode> _nodes;

        // 4 = Append w Prefabs2 (Prepend 0, ReplaceKeepChildren 1, Replace 2, Child 3, Append 4);
        // wezel wchodzi ZA celem jako rodzenstwo - zostaje ostatnim dzieckiem kafelka
        public override InsertType Type => (InsertType)4;

        [PrefabExtensionXmlNodes]
        public IEnumerable<XmlNode> Nodes
        {
            get
            {
                if (!Applied)
                {
                    Applied = true;
                    Log.Info("ArmourListColour: kafelki listy zbroi dostaly szara plachte dla nieznanych wzorow.");
                }
                return _nodes;
            }
        }

        public ArmourListColour()
        {
            var xml =
              "<Widget DoNotAcceptEvents=\"true\" IsDisabled=\"true\" WidthSizePolicy=\"StretchToParent\" " +
              "HeightSizePolicy=\"StretchToParent\" Sprite=\"BlankWhiteSquare_9\" Color=\"#141414FF\" " +
              "AlphaFactor=\"0.62\" IsVisible=\"@FvLocked\"/>";
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            _nodes = new List<XmlNode> { doc.DocumentElement };
        }
    }
}
