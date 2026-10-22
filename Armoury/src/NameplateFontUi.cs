using System;
using System.Collections.Generic;
using Bannerlord.UIExtenderEx;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace Armoury
{
    /// <summary>
    /// m105-czcionki (Jeff 21.10: czcionki "bezpiecznie"): nazwy osad na mapie czcionka ROT "Game of Thrones" (ROT-Content/GUI/Fonts - 56 znakow: A-Z, a-z,
    /// spacja, #, apostrof; wszystkie 1066 nazw osad ROT sie mieszcza). Noc 21/22.10: kopie prefabow SettlementNameplateItem* w Armoury/GUI/Prefabs NIE dzialaly
    /// (WidgetFactory bierze przy dublu nazwy plik, ktory przyjdzie pozniej z ResourceDepot - u nas wygrywal SandBox; test 103a: nazwy dalej Galahad), wiec
    /// zmiana idzie latka UIExtenderEx (ta sama droga co MapSpeedUi): atrybut Brush.Font na TextWidget Id="SettlementNameTextWidget" w trzech rozmiarach tabliczki.
    /// Rejestracja raz na proces (SubModuleMain.OnSubModuleLoad, metoda NoInlining); klucz MapSettlementNamesRotFont czytany przy starcie gry (plik MCM albo
    /// ustawienia) - zmiana w MCM dziala po ponownym uruchomieniu gry. Brak UIExtenderEx = linia w logu, reszta modulu bez zmian.
    /// </summary>
    internal static class NameplateFontUi
    {
        internal const string FontName = "Game of Thrones";
        private static bool _done;
        internal static string State = "nierejestrowane";

        internal static void Register()
        {
            if (_done) return;
            _done = true;
            string src;
            if (!OnAtStart(out src))
            {
                State = "NIE rejestrowane - Map Settlement Names Rot Font wylaczone przy starcie gry (" + src + "); wlaczenie w MCM - po ponownym uruchomieniu gry";
                Log.Info("Czcionka nazw osad (m105): UIExtenderEx - " + State + ".");
                return;
            }
            var ext = UIExtender.Create("Armoury.NameplateFont");
            ext.Register(new[] { typeof(NameplateFontLarge), typeof(NameplateFontMedium), typeof(NameplateFontSmall) });
            ext.Enable();
            State = "latki SettlementNameplateItemLarge/Medium/Small (TextWidget SettlementNameTextWidget: Brush.Font = " + FontName + ") zarejestrowane (klucz przy starcie: " + src + ")";
            Log.Info("Czcionka nazw osad (m105): UIExtenderEx - " + State + ".");
        }

        /// <summary>Klucz przy starcie gry: plik MCM (Configs/ModSettings/Global/Armoury/Armoury.json, wpis "MapSettlementNamesRotFont"), bez wpisu - Settings.Current.</summary>
        private static bool OnAtStart(out string src)
        {
            bool on = Settings.Current == null || Settings.Current.MapSettlementNamesRotFont;
            src = "ustawienia modulu / domyslne: " + (on ? "TAK" : "NIE");
            try
            {
                string doc = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (string.IsNullOrEmpty(doc)) return on;
                string f = System.IO.Path.Combine(doc, "Mount and Blade II Bannerlord", "Configs", "ModSettings", "Global", "Armoury", "Armoury.json");
                if (!System.IO.File.Exists(f)) return on;
                var m = System.Text.RegularExpressions.Regex.Match(System.IO.File.ReadAllText(f), "\"MapSettlementNamesRotFont\"\\s*:\\s*(true|false)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!m.Success) { src = "plik MCM bez wpisu, " + src; return on; }
                on = string.Equals(m.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase);
                src = "plik MCM: " + (on ? "TAK" : "NIE");
            }
            catch (Exception e) { src = "plik MCM nieczytelny (" + e.GetType().Name + "), " + src; }
            return on;
        }

        internal static List<PrefabExtensionSetAttributePatch.Attribute> FontAttr()
        {
            return new List<PrefabExtensionSetAttributePatch.Attribute> { new PrefabExtensionSetAttributePatch.Attribute("Brush.Font", FontName) };
        }
    }

    [PrefabExtension("SettlementNameplateItemLarge", "descendant::TextWidget[@Id='SettlementNameTextWidget']")]
    internal sealed class NameplateFontLarge : PrefabExtensionSetAttributePatch
    {
        public override List<Attribute> Attributes => NameplateFontUi.FontAttr();
    }

    [PrefabExtension("SettlementNameplateItemMedium", "descendant::TextWidget[@Id='SettlementNameTextWidget']")]
    internal sealed class NameplateFontMedium : PrefabExtensionSetAttributePatch
    {
        public override List<Attribute> Attributes => NameplateFontUi.FontAttr();
    }

    [PrefabExtension("SettlementNameplateItemSmall", "descendant::TextWidget[@Id='SettlementNameTextWidget']")]
    internal sealed class NameplateFontSmall : PrefabExtensionSetAttributePatch
    {
        public override List<Attribute> Attributes => NameplateFontUi.FontAttr();
    }
}
