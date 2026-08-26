using System.Collections.Generic;
using System.Text;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace ForgeView
{
    /// <summary>
    /// Rzedy filtrow dla zbroi, zbudowane doslownie tak jak natywny rzad poziomow przy broni.
    /// Znaczniki przepisane z SandBox\GUI\Prefabs\Crafting\CraftingCategory.xml:
    /// ButtonWidget z Brush="Crafting.Checkbox.Empty.Button", w srodku ImageWidget
    /// z Brush="Crafting.Checkbox.Full.Button" widoczny przy zaznaczeniu, obok TextWidget
    /// z Brush="Crafting.Tier.Text". Zadnych wlasnych sprite'ow i kolorow - stad poprzednio
    /// nie bylo ani wygladu, ani reakcji na klikniecie.
    ///
    /// Wstawiamy sie w RightPanel, czyli tam gdzie natywny rzad i gdzie Banner Kings
    /// wklada swoja liste zbroi.
    /// </summary>
    [PrefabExtension("Crafting", "descendant::Widget[@Id='RightPanel']/Children", "Crafting")]
    internal class TierBarExtension : PrefabExtensionInsertPatch
    {
        private readonly List<XmlNode> nodes;
        public override InsertType Type => (InsertType)3;
        public override int Index => 4;

        [PrefabExtensionXmlNodes]
        public IEnumerable<XmlNode> Nodes => nodes;

        /// <summary>Jeden kwadrat z podpisem - dokladnie jak natywny filtr poziomu.</summary>
        private static string Box(string cmd, string on, string label, int labelWidth)
        {
            return "<Widget WidthSizePolicy=\"CoverChildren\" HeightSizePolicy=\"Fixed\" SuggestedHeight=\"40\"><Children>" +
                     "<ButtonWidget OverrideDefaultStateSwitchingEnabled=\"false\" DoNotPassEventsToChildren=\"true\" " +
                     "WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"StretchToParent\" SuggestedWidth=\"40\" " +
                     "Brush=\"Crafting.Checkbox.Empty.Button\" Command.Click=\"" + cmd + "\" IsSelected=\"@" + on + "\" " +
                     "UpdateChildrenStates=\"true\"><Children>" +
                       "<ImageWidget WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"StretchToParent\" " +
                       "Brush=\"Crafting.Checkbox.Full.Button\" IsVisible=\"@" + on + "\"/>" +
                     "</Children></ButtonWidget>" +
                     "<TextWidget WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"StretchToParent\" SuggestedWidth=\"" + labelWidth + "\" " +
                     "MarginLeft=\"45\" Brush=\"Crafting.Tier.Text\" Text=\"" + label + "\" DoNotAcceptEvents=\"true\"/>" +
                   "</Children></Widget>";
        }

        private static string Row(string inner)
        {
            // Stala szerokosc i rowny rozstaw, dokladnie jak natywny NavigatableListPanel z TierFilters.
            // Poprzednio wiersz rosl swobodnie i osiem pozycji wychodzilo poza ekran.
            return "<ListPanel WidthSizePolicy=\"Fixed\" SuggestedWidth=\"470\" HeightSizePolicy=\"Fixed\" " +
                   "SuggestedHeight=\"42\" HorizontalAlignment=\"Center\" MarginTop=\"2\" " +
                   "StackLayout.LayoutMethod=\"HorizontalSpaced\"><Children>" + inner + "</Children></ListPanel>";
        }

        public TierBarExtension()
        {
            var tiers = new StringBuilder();
            tiers.Append(Box("ExecuteFvAll", "FvAllOn", "All", 45));
            tiers.Append(Box("ExecuteFvT1", "FvT1On", "I", 30));
            tiers.Append(Box("ExecuteFvT2", "FvT2On", "II", 34));
            tiers.Append(Box("ExecuteFvT3", "FvT3On", "III", 38));
            tiers.Append(Box("ExecuteFvT4", "FvT4On", "IV", 38));
            tiers.Append(Box("ExecuteFvT5", "FvT5On", "V", 30));
            tiers.Append(Box("ExecuteFvT6", "FvT6On", "VI", 34));

            // Przycisk kategorii - znaczniki skopiowane z natywnego FreeModeClassSelectionButton
            // (CraftingCategory.xml, linia 63): ten sam brush, rozmiar i czcionka co "One Handed Sword".
            var catButton =
                "<ButtonWidget WidthSizePolicy=\"CoverChildren\" HeightSizePolicy=\"Fixed\" MinWidth=\"250\" " +
                "SuggestedHeight=\"41\" VerticalAlignment=\"Center\" HorizontalAlignment=\"Center\" MarginTop=\"5\" " +
                "Command.Click=\"ExecuteFvPickCategory\" Brush=\"Crafting.Weapon.Type.Selection.Button\" " +
                "DominantSelectedState=\"true\" UpdateChildrenStates=\"true\" DoNotPassEventsToChildren=\"true\"><Children>" +
                "<TextWidget WidthSizePolicy=\"CoverChildren\" HeightSizePolicy=\"CoverChildren\" VerticalAlignment=\"Center\" " +
                "HorizontalAlignment=\"Center\" MarginLeft=\"10\" Text=\"@FvCategoryText\" Brush=\"Crafting.WeaponType.Text\" " +
                "Brush.FontSize=\"24\" Brush.TextVerticalAlignment=\"Center\"/>" +
                "</Children></ButtonWidget>";

            var xml =
              "<Widget WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"CoverChildren\" " +
              "VerticalAlignment=\"Top\" MarginTop=\"8\" IsVisible=\"@IsInArmorMode\"><Children>" +
                "<ListPanel DataSource=\"{ArmorCrafting}\" WidthSizePolicy=\"StretchToParent\" " +
                "HeightSizePolicy=\"CoverChildren\" HorizontalAlignment=\"Center\" " +
                "StackLayout.LayoutMethod=\"VerticalBottomToTop\"><Children>" +
                  catButton +
                  Row(tiers.ToString()) +
                "</Children></ListPanel>" +
              "</Children></Widget>";

            var doc = new XmlDocument();
            doc.LoadXml(xml);
            nodes = new List<XmlNode> { doc };
            Log.Info("TierBarExtension: wezel zbudowany wg natywnego CraftingCategory.xml.");
        }
    }
}
