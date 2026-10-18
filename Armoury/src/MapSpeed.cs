using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace Armoury
{
    /// <summary>
    /// f9-predkosc (Jeff 18.10: "przy max x8 gra zwalnia i sie przycina tam, gdzie jest duzo jednostek ... jest mod, ktory daje trzy buttony:
    /// predkosc normalna x1, x4 i x8") - TRZY PRZYCISKI BIEGU nad prawa czescia srodkowego panelu paska mapy (nad pauza / play / >> gry).
    /// Sprawdzone w dekompilacji 1.4.8:
    ///  - Campaign.TickMapTime: dt = 0.25 x realDt w StoppablePlay / UnstoppablePlay, dt x SpeedUpMultiplier w StoppableFastForward /
    ///    UnstoppableFastForward / UnstoppableFastForwardForPartyWaitTime; mnoznik dziala WYLACZNIE w biegu szybkim (x1 = zwykle play).
    ///  - SpeedUpMultiplier: gra wpisuje 4 przy kazdym wczytaniu (Campaign.OnLoad) i w kodach (CampaignCheats); poza gra i Armoury nikt go nie
    ///    ustawia (przeszukane wszystkie DLL modulow Jeffa). "Max x8" Jeffa to nasze MapClock.ApplySpeed (FastForwardMultiplier = 8, co godzine gry).
    ///  - Modul FastMode (TaleWorlds): tylko nowa kampania z GameAccelerationMode.Fast (rok 24 dni, krzywa XP) - predkosci mapy nie rusza;
    ///    w LauncherData Jeffa (18.10) FastMode jest odznaczony.
    ///  - Przyciski gry: MapTimeControlVM.ExecuteTimeControlChange(0/1/2) (bramka: brak menu albo menu oczekiwania bez blokady), klawisze gry
    ///    MapHotKeyCategory 60/61/62 = 1/2/3, 63 = spacja (MapScreen.HandleInput, SceneLayer.Input).
    /// Dzialanie: x1 = ExecuteTimeControlChange(1) (to samo co play gry). x4 / x8 = wybor biegu szybkiego (MapSpeedFast / MapSpeedVeryFast) +
    /// ExecuteTimeControlChange(2) (to samo co >> gry). Wybor zostaje do wyjscia z gry (start = bardzo szybko, czyli dotychczasowe x8) i obowiazuje
    /// takze dla >> gry, klawisza 3 i trzymanej spacji. Klawisze 4 / 5 / 6 (MapSpeedKeys; gra ich na mapie nie uzywa) - przez SceneLayer.Input
    /// jak 1/2/3 gry (pole tekstowe, encyklopedia, okno z fokusem - klawisz nie dochodzi), tylko przy czynnym srodkowym panelu.
    /// Widok: UIExtenderEx - wstawka do prefabu MapBar (siostra MapCurrentTimeVisualWidget, MapSpeedUi.cs) i mixin MapTimeControlVM z jedna
    /// wlasciwoscia ArmSpeed (nasz ViewModel). Widzety powstaja RAZ przy wczytaniu ruchu paska; co klatke (postfiks MapTimeControlVM.Tick) tylko
    /// porownanie stanu i ustawienie wlasciwosci przy zmianie (zadnej przebudowy widzetow - CLAUDE.md 7). Bez menu gry (zadnego SwitchToMenu).
    /// Wylaczony MapSpeedButtons: przyciski schowane, klawisze nieczynne, mnoznik jak dotad z FastForwardMultiplier (co godzine gry).
    /// </summary>
    internal static class MapSpeed
    {
        internal const int PickNormal = 0, PickFast = 1, PickVeryFast = 2;
        private static int _pick = PickVeryFast;   // wybor biegu szybkiego - na proces (start = dawne x8)
        private static readonly ConditionalWeakTable<MapTimeControlVM, ArmMapSpeedVM> _vms = new ConditionalWeakTable<MapTimeControlVM, ArmMapSpeedVM>();
        private static int _stumbles;
        internal static bool Hooked;

        internal static int FastOf(Settings s) { int v = s.MapSpeedFast; return v < 2 ? 2 : v > 16 ? 16 : v; }
        internal static int VeryFastOf(Settings s) { int v = s.MapSpeedVeryFast; return v < 2 ? 2 : v > 32 ? 32 : v; }

        /// <summary>Mnoznik biegu szybkiego: przy przyciskach - wybrany bieg (x4 / x8 z MCM); bez nich - FastForwardMultiplier jak dotad (1..64).</summary>
        internal static float FfMultiplier(Settings s)
        {
            if (s.MapSpeedButtons) return _pick == PickFast ? FastOf(s) : VeryFastOf(s);
            return Math.Max(1f, Math.Min(64f, s.FastForwardMultiplier));
        }

        internal static void Attach(MapTimeControlVM owner, ArmMapSpeedVM vm)
        {
            if (owner == null || vm == null) return;
            try { _vms.Remove(owner); _vms.Add(owner, vm); } catch (Exception e) { Stumble("Attach", e); }
        }

        internal static void Detach(MapTimeControlVM owner, ArmMapSpeedVM vm)
        {
            if (owner == null) return;
            try { ArmMapSpeedVM cur; if (_vms.TryGetValue(owner, out cur) && ReferenceEquals(cur, vm)) _vms.Remove(owner); } catch (Exception e) { Stumble("Detach", e); }
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 3 || _stumbles % 500 == 0) Log.Error("MapSpeed." + where + " (potkniecie " + _stumbles + ")", e);
        }

        internal static int Stumbles { get { return _stumbles; } }

        /// <summary>Kubelek klatki do profilu: 0 pauza, 1 x1, 2 szybko (MapSpeedFast), 3 bardzo szybko (MapSpeedVeryFast), 4 inny mnoznik.
        /// state = MapTimeControlVM.TimeFlowState (uproszczony tryb gry: 0 stop, 1/3 play, 2/4 szybko, 6 stop po szybkim).</summary>
        private static int BinOf(int state, float mult, int fast, int very)
        {
            if (state == 1 || state == 3) return 1;
            if (state == 2 || state == 4)
            {
                if (Math.Abs(mult - very) < 0.01f) return 3;
                if (Math.Abs(mult - fast) < 0.01f) return 2;
                return 4;
            }
            return 0;
        }

        /// <summary>Postfiks MapTimeControlVM.Tick (co klatke paska mapy, po stanie gry w VM): pomiar klatki (profil f9), mnoznik wybranego biegu,
        /// stan przyciskow (tylko przy zmianie) i klawisze 4/5/6. Bez alokacji w zwyklej klatce.</summary>
        public static void TickPostfix(MapTimeControlVM __instance)
        {
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var s = Settings.Current;
                var c = Campaign.Current;
                if (s == null || c == null || __instance == null) return;
                int fast = FastOf(s), very = VeryFastOf(s);
                int state = __instance.TimeFlowState;
                if (MapSpeedProfile.On) MapSpeedProfile.Frame(BinOf(state, c.SpeedUpMultiplier, fast, very), t0);
                ArmMapSpeedVM vm;
                if (!_vms.TryGetValue(__instance, out vm)) vm = null;
                if (!s.MapSpeedButtons) { if (vm != null) vm.IsShown = false; return; }
                float m = _pick == PickFast ? fast : very;
                if (Math.Abs(c.SpeedUpMultiplier - m) > 0.01f) c.SpeedUpMultiplier = m;   // suwak MCM i wybor od reki (co godzine gry robi to tez MapClock.ApplySpeed)
                bool panel = __instance.IsCenterPanelEnabled;
                if (vm != null) vm.Update(panel, state, _pick, fast, very, s.MapSpeedKeys);
                if (panel && s.MapSpeedKeys) Keys(__instance);
            }
            catch (Exception e) { Stumble("Tick", e); }
            finally { MapSpeedProfile.Hook(MapSpeedProfile.HButtons, Stopwatch.GetTimestamp() - t0); }
        }

        /// <summary>Klawisze 4 / 5 / 6 na mapie - wejscie warstwy sceny mapy (jak 1/2/3 gry: klawisz nie dochodzi, gdy fokus ma inna warstwa).</summary>
        private static void Keys(MapTimeControlVM vm)
        {
            var ms = MapScreen.Instance;
            if (ms == null || !ReferenceEquals(ScreenManager.TopScreen, ms)) return;
            var layer = ms.SceneLayer;
            var inp = layer != null ? layer.Input : null;
            if (inp == null) return;
            if (inp.IsKeyPressed(InputKey.D4)) Select(vm, PickNormal, true);
            else if (inp.IsKeyPressed(InputKey.D5)) Select(vm, PickFast, true);
            else if (inp.IsKeyPressed(InputKey.D6)) Select(vm, PickVeryFast, true);
        }

        /// <summary>Przycisk albo klawisz: x1 = play gry; x4 / x8 = wybor biegu szybkiego, mnoznik od reki (takze gdy czas juz biegnie szybko) i >> gry.
        /// Bramka menu jak w grze (ExecuteTimeControlChange): w menu bez oczekiwania tryb czasu sie nie zmienia, wybor biegu zostaje na nastepne >>.</summary>
        internal static void Select(MapTimeControlVM vm, int which, bool key)
        {
            try
            {
                var c = Campaign.Current;
                var s = Settings.Current;
                if (c == null || s == null || vm == null || !s.MapSpeedButtons) return;
                if (which == PickNormal) vm.ExecuteTimeControlChange(1);
                else
                {
                    _pick = which == PickFast ? PickFast : PickVeryFast;
                    c.SpeedUpMultiplier = _pick == PickFast ? FastOf(s) : VeryFastOf(s);
                    vm.ExecuteTimeControlChange(2);
                }
                MapSpeedProfile.Use(which, key);
            }
            catch (Exception e) { Stumble("Select", e); }
        }

        /// <summary>Menu glowne (SubModuleMain, po MapClock.ApplyAll): postfiks MapTimeControlVM.Tick - metoda zadeklarowana w tej klasie (DeclaredMethod).</summary>
        internal static void ApplyAll(Harmony h)
        {
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(MapTimeControlVM), "Tick");
                if (m != null)
                {
                    h.Patch(m, postfix: new HarmonyMethod(typeof(MapSpeed), nameof(TickPostfix)));
                    Hooked = true;
                }
                Log.Info("Predkosc mapy (f9): postfiks MapTimeControlVM.Tick " + (Hooked ? "wpiety" : "BRAK metody") + "; przyciski x1 / x4 / x8 przez UIExtenderEx: "
                         + MapSpeedUi.State + ".");
            }
            catch (Exception e) { Log.Error("MapSpeed.ApplyAll", e); }
        }
    }

    /// <summary>f9: ViewModel trzech przyciskow (DataSource "{ArmSpeed}" z mixinu MapTimeControlVM). Wlasciwosci powiadamiaja tylko przy zmianie.</summary>
    public sealed class ArmMapSpeedVM : ViewModel
    {
        private readonly MapTimeControlVM _owner;
        private bool _isShown, _is1On, _isFastOn, _isVeryFastOn;
        private string _text1 = "x1", _textFast = "x4", _textVeryFast = "x8";
        private BasicTooltipViewModel _hint1, _hintFast, _hintVeryFast;
        private int _sig = int.MinValue;
        private string _h1 = "", _hf = "", _hv = "";

        public ArmMapSpeedVM(MapTimeControlVM owner)
        {
            _owner = owner;
            _hint1 = new BasicTooltipViewModel(() => _h1);
            _hintFast = new BasicTooltipViewModel(() => _hf);
            _hintVeryFast = new BasicTooltipViewModel(() => _hv);
        }

        internal MapTimeControlVM Owner { get { return _owner; } }

        /// <summary>Z postfiksu Tick: widocznosc (srodkowy panel czynny), podswietlenie (play / szybko z wybranym biegiem) i napisy (tylko przy zmianie suwakow).</summary>
        internal void Update(bool panel, int state, int pick, int fast, int very, bool keys)
        {
            IsShown = panel;
            bool play = state == 1 || state == 3, ff = state == 2 || state == 4;
            Is1On = play;
            IsFastOn = ff && pick == MapSpeed.PickFast;
            IsVeryFastOn = ff && pick == MapSpeed.PickVeryFast;
            int sig = (fast * 64 + very) * 2 + (keys ? 1 : 0);
            if (sig == _sig) return;
            _sig = sig;
            TextFast = "x" + fast;
            TextVeryFast = "x" + very;
            _h1 = "Normal speed (x1)" + (keys ? " - key 4" : "");
            _hf = "Fast (x" + fast + ")" + (keys ? " - key 5" : "");
            _hv = "Very fast (x" + very + ")" + (keys ? " - key 6" : "");
        }

        public void ExecuteSpeed1() { MapSpeed.Select(_owner, MapSpeed.PickNormal, false); }
        public void ExecuteSpeedFast() { MapSpeed.Select(_owner, MapSpeed.PickFast, false); }
        public void ExecuteSpeedVeryFast() { MapSpeed.Select(_owner, MapSpeed.PickVeryFast, false); }

        [DataSourceProperty]
        public bool IsShown { get { return _isShown; } set { if (value != _isShown) { _isShown = value; OnPropertyChangedWithValue(value, "IsShown"); } } }

        [DataSourceProperty]
        public bool Is1On { get { return _is1On; } set { if (value != _is1On) { _is1On = value; OnPropertyChangedWithValue(value, "Is1On"); } } }

        [DataSourceProperty]
        public bool IsFastOn { get { return _isFastOn; } set { if (value != _isFastOn) { _isFastOn = value; OnPropertyChangedWithValue(value, "IsFastOn"); } } }

        [DataSourceProperty]
        public bool IsVeryFastOn { get { return _isVeryFastOn; } set { if (value != _isVeryFastOn) { _isVeryFastOn = value; OnPropertyChangedWithValue(value, "IsVeryFastOn"); } } }

        [DataSourceProperty]
        public string Text1 { get { return _text1; } set { if (value != _text1) { _text1 = value; OnPropertyChangedWithValue(value, "Text1"); } } }

        [DataSourceProperty]
        public string TextFast { get { return _textFast; } set { if (value != _textFast) { _textFast = value; OnPropertyChangedWithValue(value, "TextFast"); } } }

        [DataSourceProperty]
        public string TextVeryFast { get { return _textVeryFast; } set { if (value != _textVeryFast) { _textVeryFast = value; OnPropertyChangedWithValue(value, "TextVeryFast"); } } }

        [DataSourceProperty]
        public BasicTooltipViewModel Hint1 { get { return _hint1; } set { if (value != _hint1) { _hint1 = value; OnPropertyChangedWithValue(value, "Hint1"); } } }

        [DataSourceProperty]
        public BasicTooltipViewModel HintFast { get { return _hintFast; } set { if (value != _hintFast) { _hintFast = value; OnPropertyChangedWithValue(value, "HintFast"); } } }

        [DataSourceProperty]
        public BasicTooltipViewModel HintVeryFast { get { return _hintVeryFast; } set { if (value != _hintVeryFast) { _hintVeryFast = value; OnPropertyChangedWithValue(value, "HintVeryFast"); } } }
    }
}
