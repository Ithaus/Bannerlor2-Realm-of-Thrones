using System;
using System.Collections.Generic;
using System.Xml;
using Bannerlord.UIExtenderEx;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// r4-wioska (20.10, Jeff: "NIe ma poakzanego moje thiefts" - zakladka Fiefs ekranu rodu: "Clan Does Not Own Any Fiefs", Towns 0, Castles 0): WIOSKA POBOCZNA GRACZA
    /// W ZAKLADCE FIEFS. Wioska poboczna nie jest osada gry (wiersz ukladu wiosek), wiec nie moze byc pozycja ClanSettlementItemVM (ta wymaga Settlement - zaznaczenie,
    /// namiestnik, projekty, teleport). Najbezpieczniej: osobny blok tekstu POD lista zamkow (lewa kolumna) - "Side villages (1)", nazwa z okregiem i panem, dochod dnia.
    ///  - wstawka do prefabu ClanFiefs (SandBox\GUI\Prefabs\Clan\ClanFiefs.xml) jako SIOSTRA listy zamkow (NavigatableListPanel Id="CastlesList") w ClanElementsListPanel;
    ///    same TextWidgety z pedzlami tego prefabu, bez zdarzen (DoNotAcceptEvents), bez nawigacji pada i bez wlasnych sprite'ow;
    ///  - mixin ClanFiefsVM z handleDerived (NavalDLC podmienia VM na NavalClanFiefsVM : ClanFiefsVM - CreateFiefsDataSource; zestawy modulow ladowane przed OnSubModuleLoad,
    ///    Module.LoadSubModules) - 4 wlasnosci tylko do odczytu, wypelniane w konstruktorze i po RefreshValues; kazde wypelnienie we wlasnym try (potkniecia liczone
    ///    w SideVillageFief), ekran sie nie wywraca;
    ///  - prawa czesc zakladki (NoFiefsText, gdy rod nie ma miast ani zamkow) - osobny postfiks Harmony bez UIExtenderEx (SideVillageFief.NoFiefsPostfix).
    /// Rejestracja w OnSubModuleLoad (wlasny UIExtender "Armoury.SideVillage" - niezalezny od przyciskow predkosci mapy, ktore u Jeffa sa nierejestrowane przez Better
    /// Time), metoda NoInlining w SubModuleMain - brak UIExtenderEx = linia w logu. Klucz SideVillageFiefClanTab czytany przy starcie gry z pliku MCM (jak MapSpeedButtons).
    /// m112-inwest (24.10, Jeff: "i ma byc to poakzane w thiefts zeby wedizal gdzie mam majtek i inwestycje"): ta sama wstawka niesie jeszcze dwa bloki - "Village works (N)"
    /// i "Estates (N)" (FiefInvestments, klucz InvestmentsInFiefs) - tez same TextWidgety bez zdarzen, kazde wypelnienie we wlasnym try (potkniecia liczone
    /// w FiefInvestments). Rejestracja, gdy przy starcie gry wlaczony jest KTORYKOLWIEK z kluczy SideVillageFiefClanTab / InvestmentsInFiefs; blok wioski pobocznej
    /// dalej tylko przy SideVillageFiefClanTab.
    /// </summary>
    internal static class SideVillageFiefUi
    {
        private static bool _done;
        internal static string State = "nierejestrowane";

        internal static void Register()
        {
            if (_done) return;
            _done = true;
            string src, src2;
            bool side = KeyAtStart("SideVillageFiefClanTab", Settings.Current == null || Settings.Current.SideVillageFiefClanTab, out src);
            bool inv = KeyAtStart("InvestmentsInFiefs", Settings.Current == null || Settings.Current.InvestmentsInFiefs, out src2);   // m112-inwest
            src = "SideVillageFiefClanTab " + src + "; InvestmentsInFiefs " + src2;
            if (!side && !inv)
            {
                State = "NIE rejestrowane - SideVillageFiefClanTab i InvestmentsInFiefs wylaczone przy starcie gry (" + src + ")";
                Log.Info("Wioska gracza (n20): zakladka Fiefs (UIExtenderEx) - " + State + ".");
                return;
            }
            var ext = UIExtender.Create("Armoury.SideVillage");
            ext.Register(new[] { typeof(SideVillageFiefsExtension), typeof(SideVillageFiefsMixin) });
            ext.Enable();
            State = "wstawka ClanFiefs i mixin ClanFiefsVM (z pochodnymi) zarejestrowane (" + src + ")";
            Log.Info("Wioska gracza (n20): zakladka Fiefs (UIExtenderEx) - " + State + ".");
        }

        /// <summary>Klucz przy starcie gry (MCM wchodzi do Settings dopiero w sesji): plik Armoury.json MCM, bez wpisu - Settings.Current / domyslne (TAK). Nie rzuca.</summary>
        private static bool KeyAtStart(string key, bool dflt, out string src)
        {
            bool on = dflt;
            src = "ustawienia modulu / domyslne: " + (on ? "TAK" : "NIE");
            try
            {
                string doc = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (string.IsNullOrEmpty(doc)) return on;
                string f = System.IO.Path.Combine(doc, "Mount and Blade II Bannerlord", "Configs", "ModSettings", "Global", "Armoury", "Armoury.json");
                if (!System.IO.File.Exists(f)) return on;
                var m = System.Text.RegularExpressions.Regex.Match(System.IO.File.ReadAllText(f), "\"" + key + "\"\\s*:\\s*(true|false)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!m.Success) { src = "plik MCM bez wpisu, " + src; return on; }
                on = string.Equals(m.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase);
                src = "plik MCM: " + (on ? "TAK" : "NIE");
            }
            catch (Exception e) { src = "plik MCM nieczytelny (" + e.GetType().Name + "), " + src; }
            return on;
        }
    }

    /// <summary>r4-wioska: blok "Side villages (1)" pod lista zamkow zakladki Fiefs (siostra CastlesList). Same teksty bez zdarzen; niewidoczny bez wioski gracza.</summary>
    [PrefabExtension("ClanFiefs", "descendant::NavigatableListPanel[@Id='CastlesList']")]
    internal sealed class SideVillageFiefsExtension : PrefabExtensionInsertPatch
    {
        private readonly List<XmlNode> _nodes;
        public override InsertType Type => InsertType.Append;

        [PrefabExtensionXmlNodes]
        public IEnumerable<XmlNode> Nodes => _nodes;

        public SideVillageFiefsExtension()
        {
            const string txt = "DoNotAcceptEvents=\"true\" WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"CoverChildren\" ";
            var xml =
                "<ListPanel Id=\"ArmSideVillages\" IsVisible=\"@ArmSideShown\" DoNotAcceptEvents=\"true\" WidthSizePolicy=\"Fixed\" SuggestedWidth=\"!Clan.Management.Collapser.Width\" "
              + "HeightSizePolicy=\"CoverChildren\" HorizontalAlignment=\"Right\" MarginTop=\"14\" MarginBottom=\"10\" StackLayout.LayoutMethod=\"VerticalTopToBottom\"><Children>"
              + "<TextWidget " + txt + "MarginLeft=\"12\" Brush=\"Clan.Management.Collapser.Text\" Brush.TextHorizontalAlignment=\"Left\" Text=\"@ArmSideHeader\" />"
              + "<TextWidget " + txt + "MarginLeft=\"24\" MarginRight=\"10\" MarginTop=\"8\" Brush=\"Clan.Tuple.Name.Text\" Brush.TextHorizontalAlignment=\"Left\" Text=\"@ArmSideLine\" />"
              + "<TextWidget " + txt + "MarginLeft=\"24\" MarginRight=\"10\" MarginTop=\"4\" Brush=\"Clan.Tuple.Name.Text\" Brush.TextHorizontalAlignment=\"Left\" Brush.FontSize=\"18\" Text=\"@ArmSideIncome\" />"
              + "</Children></ListPanel>";
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            _nodes = new List<XmlNode> { doc, Block("ArmWorks"), Block("ArmEstates") };   // m112-inwest: prace i majatki gracza pod blokiem wioski pobocznej
        }

        /// <summary>m112-inwest: blok "Village works (N)" / "Estates (N)" - naglowek i jeden tekst wielowierszowy (linie z \n), ten sam wyglad co blok wioski pobocznej.</summary>
        private static XmlNode Block(string id)
        {
            const string txt = "DoNotAcceptEvents=\"true\" WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"CoverChildren\" ";
            var xml =
                "<ListPanel Id=\"" + id + "\" IsVisible=\"@" + id + "Shown\" DoNotAcceptEvents=\"true\" WidthSizePolicy=\"Fixed\" SuggestedWidth=\"!Clan.Management.Collapser.Width\" "
              + "HeightSizePolicy=\"CoverChildren\" HorizontalAlignment=\"Right\" MarginTop=\"14\" MarginBottom=\"10\" StackLayout.LayoutMethod=\"VerticalTopToBottom\"><Children>"
              + "<TextWidget " + txt + "MarginLeft=\"12\" Brush=\"Clan.Management.Collapser.Text\" Brush.TextHorizontalAlignment=\"Left\" Text=\"@" + id + "Header\" />"
              + "<TextWidget " + txt + "MarginLeft=\"24\" MarginRight=\"10\" MarginTop=\"8\" Brush=\"Clan.Tuple.Name.Text\" Brush.TextHorizontalAlignment=\"Left\" Brush.FontSize=\"18\" Text=\"@" + id + "Body\" />"
              + "</Children></ListPanel>";
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            return doc;
        }
    }

    /// <summary>r4-wioska: mixin ClanFiefsVM (z pochodnymi - NavalClanFiefsVM): wlasnosci bloku wioski pobocznej. Wypelnienie w konstruktorze i po RefreshValues; wyjatek
    /// - potkniecie w SideVillageFief, blok ukryty (ekran bez zmian).</summary>
    [ViewModelMixin("RefreshValues", true)]
    internal sealed class SideVillageFiefsMixin : BaseViewModelMixin<ClanFiefsVM>
    {
        private bool _shown;
        private string _header = "", _line = "", _income = "";
        private bool _wShown, _eShown;                                        // m112-inwest: prace i majatki gracza
        private string _wHeader = "", _wBody = "", _eHeader = "", _eBody = "";

        public SideVillageFiefsMixin(ClanFiefsVM vm) : base(vm)
        {
            FiefInvestments.Opened(vm);                                         // m112-inwest po recenzji: nowy ekran - skan majatkow od nowa (nie rzuca)
            try { Fill(false); } catch (Exception e) { _shown = false; SideVillageFief.UiStumble("Mixin.ctor", e); }
            try { FillWorks(false); } catch (Exception e) { _wShown = false; FiefInvestments.UiStumble("Mixin.ctor(prace)", e); }
            try { FillEstates(false); } catch (Exception e) { _eShown = false; FiefInvestments.UiStumble("Mixin.ctor(majatki)", e); }
        }

        public override void OnRefresh()
        {
            try { Fill(true); } catch (Exception e) { _shown = false; SideVillageFief.UiStumble("Mixin.OnRefresh", e); }
            try { FillWorks(true); } catch (Exception e) { _wShown = false; FiefInvestments.UiStumble("Mixin.OnRefresh(prace)", e); }
            try { FillEstates(true); } catch (Exception e) { _eShown = false; FiefInvestments.UiStumble("Mixin.OnRefresh(majatki)", e); }
        }

        /// <summary>m112-inwest: blok "Village works (N)" (FiefInvestments.WorksTab - klucz InvestmentsInFiefs; brak prac - ukryty).</summary>
        private void FillWorks(bool notify)
        {
            string h, b;
            bool on = FiefInvestments.WorksTab(out h, out b);
            _wShown = on; _wHeader = on ? h ?? "" : ""; _wBody = on ? b ?? "" : "";
            if (!notify) return;
            OnPropertyChangedWithValue(_wShown, nameof(ArmWorksShown));
            OnPropertyChangedWithValue(_wHeader, nameof(ArmWorksHeader));
            OnPropertyChangedWithValue(_wBody, nameof(ArmWorksBody));
        }

        /// <summary>m112-inwest: blok "Estates (N)" (FiefInvestments.EstatesTab - majatki BK rodu gracza; brak - ukryty).</summary>
        private void FillEstates(bool notify)
        {
            string h, b;
            bool on = FiefInvestments.EstatesTab(out h, out b);
            _eShown = on; _eHeader = on ? h ?? "" : ""; _eBody = on ? b ?? "" : "";
            if (!notify) return;
            OnPropertyChangedWithValue(_eShown, nameof(ArmEstatesShown));
            OnPropertyChangedWithValue(_eHeader, nameof(ArmEstatesHeader));
            OnPropertyChangedWithValue(_eBody, nameof(ArmEstatesBody));
        }

        private void Fill(bool notify)
        {
            string h = "", l = "", i = "";
            var s = Settings.Current;
            bool on = s != null && s.SideVillageFiefClanTab && SideVillageFief.ClanTab(out h, out l, out i);
            if (!on) h = l = i = "";
            _shown = on; _header = h ?? ""; _line = l ?? ""; _income = i ?? "";
            if (!notify) return;
            OnPropertyChangedWithValue(_shown, nameof(ArmSideShown));
            OnPropertyChangedWithValue(_header, nameof(ArmSideHeader));
            OnPropertyChangedWithValue(_line, nameof(ArmSideLine));
            OnPropertyChangedWithValue(_income, nameof(ArmSideIncome));
        }

        [DataSourceProperty] public bool ArmSideShown => _shown;
        [DataSourceProperty] public string ArmSideHeader => _header;
        [DataSourceProperty] public string ArmSideLine => _line;
        [DataSourceProperty] public string ArmSideIncome => _income;
        [DataSourceProperty] public bool ArmWorksShown => _wShown;
        [DataSourceProperty] public string ArmWorksHeader => _wHeader;
        [DataSourceProperty] public string ArmWorksBody => _wBody;
        [DataSourceProperty] public bool ArmEstatesShown => _eShown;
        [DataSourceProperty] public string ArmEstatesHeader => _eHeader;
        [DataSourceProperty] public string ArmEstatesBody => _eBody;
    }
}
