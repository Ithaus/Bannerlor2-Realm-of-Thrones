using System.Collections.Generic;
using System.Threading;
using Bannerlord.UIExtenderEx;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace Armoury
{
    /// <summary>
    /// 541 (Jeff 15.11: "mamy 132/ na 132 ma byc ile mamy zapelnionych"): pole tekstu DOLNEGO rzedu paska mapy (SandBox\GUI\Prefabs\Map\MapBar.xml,
    /// ListPanel Id="BottomInfoBar", szablon pozycji) dostaje MaxWidth 70 zamiast 52 - miesci pelny ulamek "132/150" (ok. 57 px przy FontSize 20, Galahad).
    /// Gorny rzad nietkniety (to on wyznacza szerokosc paska sprzed 537). Teksty gry w dolnym rzedzie sa wezsze niz 52 px (CoverChildren) - bez zmian.
    /// Jedyne typy MarchHud zalezne od Bannerlord.UIExtenderEx (rejestracja przez metode NoInlining w SubModuleMain; brak UIExtenderEx = linia w logu,
    /// pasek liczy wtedy z MaxWidth 52 - ulamek, ktory sie nie miesci, pokazuje sam licznik). Osobny UIExtender "Armoury.MarchHud" - niezalezny od
    /// przyciskow mapy (MapSpeedUi, u Jeffa nierejestrowane przez Better Time). Getter Attributes UIExtenderEx wola dopiero po trafieniu xpath
    /// (PrefabComponent.RegisterPatch) - licznik MarchHud.BottomApplied dowodzi nalozenia.
    /// </summary>
    internal static class MarchHudUi
    {
        private static bool _done;
        internal static string State = "nierejestrowane";

        internal static void Register()
        {
            if (_done) return;
            _done = true;
            var ext = UIExtender.Create("Armoury.MarchHud");
            ext.Register(new[] { typeof(MarchHudBottomTextWidth) });
            ext.Enable();
            State = "MaxWidth " + MarchHud.BottomMaxWidth + " na tekscie dolnego rzedu MapBar zarejestrowane";
            Log.Info("Pasek mapy (541): UIExtenderEx - " + State + ".");
        }
    }

    /// <summary>541: szablon pozycji dolnego rzedu paska mapy - szersze pole tekstu (pelny ulamek). XPath przez SelectSingleNode - tylko dolny szablon.</summary>
    [PrefabExtension("MapBar", "descendant::ListPanel[@Id='BottomInfoBar']/ItemTemplate/HintWidget/Children/ListPanel/Children/MapBarCustomValueTextWidget")]
    internal sealed class MarchHudBottomTextWidth : PrefabExtensionSetAttributePatch
    {
        public override List<Attribute> Attributes
        {
            get
            {
                Interlocked.Increment(ref MarchHud.BottomApplied);
                return new List<Attribute> { new Attribute("MaxWidth", MarchHud.BottomMaxWidth.ToString()) };
            }
        }
    }
}
