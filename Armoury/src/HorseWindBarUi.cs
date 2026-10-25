using System;
using System.Collections.Generic;
using System.Threading;
using System.Xml;
using Bannerlord.UIExtenderEx;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.ViewModelCollection;

namespace Armoury
{
    /// <summary>
    /// 155 (25.10, Jeff ok. 23:10: "Acha i nie widac zmeczenia konia powienin byc pasek pozniejzdoia konia ze stmaina konia" - pasek pod zdrowiem konia
    /// ze stamina konia): PASEK STAMINY KONIA GRACZA w HUD bitwy.
    /// Jedyne typy Armoury dla paska zalezne od Bannerlord.UIExtenderEx (rejestracja przez metode NoInlining w SubModuleMain - brak zestawu UIExtenderEx = linia
    /// w logu, reszta modulu bez zmian). HorseWind zna tylko interfejs IHorseWindBar i HorseWindBarLink (HorseWind.cs) - bez typow UIExtenderEx w takcie.
    ///  - wstawka do prefabu AgentStatus (Native\GUI\Prefabs\Mission\AgentStatus.xml, prefab zalezny filmu MainAgentHUD) jako PIERWSZE dziecko
    ///    &lt;AgentHealthWidget Id="HorseHealthWidget"&gt;: cienki pasek 150 x 6 tuz pod ramka zdrowia konia (191 x 26), prawy brzeg rowno z wypelnieniem zdrowia
    ///    (MarginRight 17), lewy brzeg x = 24 - za ikona konia (x -35..23). Chowa sie razem z paskiem zdrowia konia (AgentHealthWidget.CheckVisibility: ShowHealthBar
    ///    i zdrowie / spadki) - zsiadanie, smierc konia, brak konia. Zwykly Widget + sprite BlankWhiteSquare_9 (ten sam prefab uzywa go przy pasku amunicji oddzialu)
    ///    i bindowane SuggestedWidth - BEZ FillBarWidget i bez pedzla (FillBar.OnRender bierze warstwy pedzla bez sprawdzenia null - NRE co klatke);
    ///  - pasywny mixin MissionAgentStatusVM BEZ metody odswiezania (NIGDY [ViewModelMixin("Tick")]: UIExtenderEx wolalby wtedy Refresh co klatke -
    ///    GetAllRuntimes().ToList() i GetCustomAttribute co klatke); konstruktor tylko zapisuje statyczne odwolanie (w try - biegnie wewnatrz konstruktora VM HUD,
    ///    wyjatek wywrocilby HUD misji). Wartosci wpisuje HorseWind co 0.25 s (PublishBar), powiadomienia tylko przy zmianie.
    /// Rejestracja ZAWSZE przy starcie gry (decyzja: bez restartu) - klucz MCM HorseStaminaBar tylko chowa / pokazuje pasek (HorseWind.PublishBar).
    /// Osobny UIExtender "Armoury.HorseBar" - niezalezny od przyciskow mapy (u Jeffa nierejestrowane przez Better Time) i zakladki Fiefs.
    /// NavalDLC podmienia widok statusu (MissionGauntletNavalAgentStatus, OverrideView), ale dziedziczy EarlyStart - ten sam MissionAgentStatusVM i film MainAgentHUD.
    /// </summary>
    internal static class HorseWindBarUi
    {
        private static bool _done;
        internal static string State = "nierejestrowane";

        internal static void Register()
        {
            if (_done) return;
            _done = true;
            var ext = UIExtender.Create("Armoury.HorseBar");
            ext.Register(new[] { typeof(HorseWindBarExtension), typeof(HorseWindBarMixin) });
            ext.Enable();
            State = "wstawka AgentStatus (dziecko HorseHealthWidget) i mixin MissionAgentStatusVM zarejestrowane (zawsze; klucz HorseStaminaBar tylko chowa pasek)";
            Log.Info("Pasek konia (155): UIExtenderEx - " + State + ".");
        }
    }

    /// <summary>155: pasek pod ramka zdrowia konia - tlo i dwa wypelnienia (pomaranczowe / czerwone ponizej progu zmeczenia), szerokosc z mixinu. Bez zdarzen.
    /// Getter Nodes UIExtenderEx wola dopiero po trafieniu xpath (PrefabComponent.RegisterPatch), wiec licznik HorseWindBarLink.Applied dowodzi nalozenia.</summary>
    [PrefabExtension("AgentStatus", "descendant::AgentHealthWidget[@Id='HorseHealthWidget']/Children")]
    internal sealed class HorseWindBarExtension : PrefabExtensionInsertPatch
    {
        private readonly List<XmlNode> _nodes;
        public override InsertType Type => InsertType.Child;
        public override int Index => 0;   // pierwsze dziecko - rysowane pod ramka; z ikona konia sie nie nakladaja

        [PrefabExtensionXmlNodes]
        public IEnumerable<XmlNode> Nodes
        {
            get
            {
                Interlocked.Increment(ref HorseWindBarLink.Applied);
                return _nodes;
            }
        }

        private static string Fill(string color, string vis)
        {
            return "<Widget DoNotAcceptEvents=\"true\" WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"StretchToParent\" SuggestedWidth=\"@ArmHorseWindFill\" "
                 + "HorizontalAlignment=\"Left\" MarginLeft=\"1\" MarginTop=\"1\" MarginBottom=\"1\" Sprite=\"BlankWhiteSquare_9\" Color=\"" + color + "\" " + vis + " />";
        }

        public HorseWindBarExtension()
        {
            var xml =
                "<Widget Id=\"ArmHorseWind\" DoNotAcceptEvents=\"true\" IsVisible=\"@ArmHorseWindShown\" WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"Fixed\" "
              + "SuggestedWidth=\"150\" SuggestedHeight=\"6\" HorizontalAlignment=\"Right\" VerticalAlignment=\"Bottom\" MarginRight=\"17\" PositionYOffset=\"8\" "
              + "Sprite=\"BlankWhiteSquare_9\" Color=\"#000000B4\"><Children>"
              + Fill("#EB8334FF", "IsHidden=\"@ArmHorseWindLow\"")      // pomaranczowy HUD wanilii (pasek amunicji oddzialu, AgentStatus.xml)
              + Fill("#C8402AFF", "IsVisible=\"@ArmHorseWindLow\"")     // ponizej progu zmeczenia (HorseWind.TiredBelow - kon zwalnia)
              + "</Children></Widget>";
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            _nodes = new List<XmlNode> { doc };
        }
    }

    /// <summary>155: pasywny mixin MissionAgentStatusVM (bez metody odswiezania) - trzy wlasnosci paska; wartosci wpisuje HorseWind przez IHorseWindBar.</summary>
    [ViewModelMixin]
    internal sealed class HorseWindBarMixin : BaseViewModelMixin<MissionAgentStatusVM>, IHorseWindBar
    {
        private const float Track = 148f;   // 150 - 2 x margines 1 px
        private bool _shown, _low;
        private float _fill;

        public HorseWindBarMixin(MissionAgentStatusVM vm) : base(vm)
        {
            // biegnie wewnatrz konstruktora VM HUD (InitializeMixinsForVMInstance bez try) - tylko przypisania, nic nie moze rzucic na zewnatrz
            try
            {
                HorseWindBarLink.Sink = this;
                Interlocked.Increment(ref HorseWindBarLink.MixinMade);
            }
            catch { }
        }

        /// <summary>HorseWind (glowny watek, co 0.25 s): shown - pasek widoczny, frac 0..1 - stamina, low - ponizej progu zmeczenia. Powiadomienia tylko przy zmianie
        /// (szerokosc od 0.5 px); szerokosc przed pokazaniem.</summary>
        public void Set(bool shown, float frac, bool low)
        {
            float f = shown ? Math.Max(0f, Math.Min(1f, frac)) * Track : 0f;
            if (Math.Abs(f - _fill) >= 0.5f || (f <= 0f) != (_fill <= 0f)) { _fill = f; OnPropertyChangedWithValue(_fill, nameof(ArmHorseWindFill)); }
            if (low != _low) { _low = low; OnPropertyChangedWithValue(_low, nameof(ArmHorseWindLow)); }
            if (shown != _shown) { _shown = shown; OnPropertyChangedWithValue(_shown, nameof(ArmHorseWindShown)); }
        }

        public override void OnFinalize()
        {
            base.OnFinalize();
            try { if (ReferenceEquals(HorseWindBarLink.Sink, this)) HorseWindBarLink.Sink = null; } catch { }
        }

        [DataSourceProperty] public bool ArmHorseWindShown => _shown;
        [DataSourceProperty] public float ArmHorseWindFill => _fill;
        [DataSourceProperty] public bool ArmHorseWindLow => _low;
    }
}
