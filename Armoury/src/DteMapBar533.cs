using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// 533 (Jeff 15.11 "tak zrob" - po wyjasnieniu, ze "missing 6" przy ludziku w lewym dolnym rogu mapy to podpowiedz paska DTE, inna regula niz meldunek QM):
    /// podpowiedz paska gotowosci zbrojowni DTE na mapie pokazuje NASZA liczbe - te sama co meldunek QM i "Review the army" (KitPreview, jedna funkcja).
    /// Cel (dekompilacja DTE 1.4.7): DynamicTroopEquipmentReupload.GUIExtensions.MapArmoryReadinessMixin (mixin UIExtenderEx MapBarVM). Jego OnRefresh wola
    /// MapBarVM.OnRefresh z Tick co 0.2 s (0.1 s przy szybkim czasie); DTE buduje NOWY HintViewModel tylko przy zmianie wersji zbrojowni, skladu oddzialow,
    /// przelacznika PreferDefaultEquipmentThenClosest albo morza ("Army Armory readiness: X% (F/E required equipment slots filled)\nMost needed equipment: TYPE
    /// (N missing)"). ArmoryReadinessHint to [DataSourceProperty] z prywatnym setterem (wola OnPropertyChangedWithValue) - piszemy przez ten setter.
    /// Postfiks na OnRefresh (DeclaredMethod klasy mixinu): co wywolanie tylko porownanie referencji; podglad (KitPreview.ForArmoury -> Get(false), z pamieci
    /// po kluczu wersji/dnia, liczy sie od nowa tylko po zmianie klucza, ok. 10 ms) najwyzej raz na sekunde - gdy DTE przebudowal podpowiedz albo co 5 s
    /// (dzien gry, sakwy - DTE ich nie sledzi); miedzy tym - wynik z pamieci (KitPreview.Last), gdy przeliczyl go kto inny (meldunek QM, okno armii).
    /// Bez podgladu (wylacznik, CrashScribe sprzed 133a, zbrojownia otwarta, Own Kit Only wylaczone, brak zdrowych) - podpowiedz DTE bez zmian (przywracana).
    /// Wysokosc paska (ArmoryReadinessFillHeight) zostaje miara DTE - nie ruszamy. Bledy liczone, bez globalnego wylacznika.
    /// </summary>
    internal static class DteMapBar533
    {
        private const string Tag = "Pasek DTE na mapie (533): ";

        private static Harmony _h;
        private static MethodInfo _setHint;
        private static string _wired = "nie sprawdzane";
        private static bool _ok;

        // stan podpowiedzi (watek glowny - UI)
        private static HintViewModel _mine;      // nasza podpowiedz (ostatnio wstawiona)
        private static HintViewModel _dteSeen;   // ostatnia podpowiedz DTE, ktora widzielismy (przywracana bez podgladu)
        private static KitPreview.Result _used;  // wynik podgladu, z ktorego zbudowano _mine
        private static string _lastText;
        private static int _lastTry = int.MinValue / 2;
        private static int _stumbles, _shown;
        private static bool _blocked;            // ostatnia proba podgladu bez wyniku - pamieci KitPreview.Last nie bierzemy do nastepnej udanej proby

        internal static bool On { get { var s = Settings.Current; return s != null && s.DteMapBarShowsKitPreview; } }
        /// <summary>Wpiete i wlaczone - napisy KitPreview mowia wtedy, ze podpowiedz paska ma ta sama liczbe.</summary>
        internal static bool Shows { get { return _ok && On; } }

        private static void Stumble(string where, Exception e)
        {
            int n = ++_stumbles;
            try { if (n <= 3 || n % 50 == 0) Log.Error("DteMapBar533." + where + " (potkniecie " + n + ")", e); } catch { }
        }

        /// <summary>OnSessionLaunched (latka raz na proces); linia startu. Nie rzuca.</summary>
        internal static void SessionStart()
        {
            if (_h == null)
            {
                _h = new Harmony("com.jeff.armoury.dtemapbar533");
                try
                {
                    var t = AccessTools.TypeByName("DynamicTroopEquipmentReupload.GUIExtensions.MapArmoryReadinessMixin");
                    var m = t != null ? AccessTools.DeclaredMethod(t, "OnRefresh") : null;
                    var hint = t != null ? AccessTools.DeclaredField(t, "_armoryReadinessHint") : null;
                    var vis = t != null ? AccessTools.DeclaredField(t, "_isArmoryReadinessVisible") : null;
                    _setHint = t != null ? AccessTools.DeclaredPropertySetter(t, "ArmoryReadinessHint") : null;
                    if (t == null) _wired = "DTE bez mixinu paska (MapArmoryReadinessMixin) - nic do podmiany";
                    else if (m == null || hint == null || vis == null || _setHint == null
                             || hint.FieldType != typeof(HintViewModel) || vis.FieldType != typeof(bool))
                        _wired = "BRAK (inna budowa MapArmoryReadinessMixin: OnRefresh " + (m != null) + ", _armoryReadinessHint " + (hint != null)
                                 + ", _isArmoryReadinessVisible " + (vis != null) + ", setter ArmoryReadinessHint " + (_setHint != null) + ") - podpowiedz DTE bez zmian";
                    else
                    {
                        _h.Patch(m, postfix: new HarmonyMethod(typeof(DteMapBar533), nameof(RefreshPostfix)));
                        _ok = true;
                        _wired = "TAK (postfiks MapArmoryReadinessMixin.OnRefresh)";
                    }
                }
                catch (Exception e) { _wired = "wyjatek " + e.GetType().Name + " - podpowiedz DTE bez zmian"; Stumble("SessionStart", e); }
            }
            Reset();
            try
            {
                Log.Info(Tag + "latka " + _wired + " | wylacznik DTE Map Bar Shows Kit Preview " + (On ? "TAK" : "NIE")
                         + " (podpowiedz paska = liczba z meldunku QM i Review the army; wysokosc paska - miara DTE; podglad najwyzej raz na 1 s, z pamieci po kluczu)"
                         + (_stumbles > 0 ? " | potkniecia " + _stumbles : "") + ".");
            }
            catch { }
        }

        /// <summary>Nowa kampania / wczytanie - bez starych obiektow UI i wyniku podgladu.</summary>
        internal static void Reset() { _mine = null; _dteSeen = null; _used = null; _lastText = null; _lastTry = Environment.TickCount - 10000; _blocked = false; }

        /// <summary>Postfiks DTE MapArmoryReadinessMixin.OnRefresh (co 0.2 s na mapie). Tanio: porownanie referencji; podglad najwyzej raz na 1 s.</summary>
        public static void RefreshPostfix(object __instance, HintViewModel ____armoryReadinessHint, bool ____isArmoryReadinessVisible)
        {
            try
            {
                if (!____isArmoryReadinessVisible || __instance == null) return;
                var cur = ____armoryReadinessHint;
                bool oursShown = _mine != null && ReferenceEquals(cur, _mine);
                if (!On)
                {
                    if (oursShown && _dteSeen != null) Put(__instance, _dteSeen, null, null);   // wylaczone w trakcie - wraca tekst DTE
                    return;
                }
                bool dteFresh = false;
                if (!oursShown && !ReferenceEquals(cur, _dteSeen)) { _dteSeen = cur; dteFresh = true; }   // DTE przebudowal podpowiedz (albo nowy mixin)
                int now = Environment.TickCount;
                int since = unchecked(now - _lastTry);
                KitPreview.Result r;
                bool tried = false;
                if (since >= 1000 && (dteFresh || since >= 5000))
                {
                    _lastTry = now; tried = true;
                    r = KitPreview.ForArmoury();   // Get(false): z pamieci po kluczu; od nowa tylko po zmianie wersji zbrojowni / skladu / sakw / dnia
                    _blocked = r == null;          // np. zbrojownia otwarta: Get zwraca wynik bez Ok, a KitPreview.Last zostaje stary - bez migania tekstow
                }
                else r = _blocked ? null : Usable(KitPreview.Last);   // wynik przeliczony przez kogos innego (meldunek QM, okno armii)
                if (r == null)
                {
                    if (tried && oursShown && _dteSeen != null) Put(__instance, _dteSeen, null, null);   // bez podgladu - tekst DTE
                    return;
                }
                if (oursShown && ReferenceEquals(r, _used)) return;
                string text = Text(r);
                if (oursShown && text == _lastText) { _used = r; return; }
                Put(__instance, new HintViewModel(new TextObject(text), null), r, text);
            }
            catch (Exception e) { Stumble("RefreshPostfix", e); }
        }

        private static KitPreview.Result Usable(KitPreview.Result r)
        {
            return r != null && r.Ok && r.Men > 0 && KitPreview.CsQuiet ? r : null;
        }

        private static void Put(object mixin, HintViewModel h, KitPreview.Result r, string text)
        {
            _setHint.Invoke(mixin, new object[] { h });
            if (r == null) { _mine = null; _used = null; _lastText = null; return; }
            _mine = h; _used = r;
            if (text != _lastText)
            {
                _shown++;
                if (_shown <= 20 || _shown % 50 == 0)
                    try { Log.Info(Tag + "podpowiedz paska (" + _shown + ") -> \"" + text.Replace("\n", " / ") + "\" (podglad liczony " + r.Ms + " ms)."); } catch { }
            }
            _lastText = text;
        }

        /// <summary>Tekst podpowiedzi: ten sam zestaw co meldunek QM / Review the army (KitPreview.MainTitle, EmptyDetail, KindsSum).
        /// TextObject bez zmiennych - w tekscie nie ma nawiasow klamrowych.</summary>
        internal static string Text(KitPreview.Result r)
        {
            string head;
            if (r.Missing > 0)
                head = "Kit for the next battle: " + r.Missing + (r.Missing == 1 ? " kit slot empty" : " kit slots empty") + " - " + KitPreview.EmptyDetail(r, 2);
            else head = KitPreview.MainTitle(r);
            return head.Replace("{", "(").Replace("}", ")") + "\nReview the army in camp for details";
        }
    }
}
