using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace CrashScribe
{
    /// <summary>
    /// Autotest: "rece gracza" na oknach i ekranach. Wszystko przez te same metody, ktore wola
    /// klawisz Enter / Esc albo przycisk w oknie (ExecuteAffirmativeAction, OnNextStage, ExecuteDone...).
    /// Typy widokow (GauntletUI, SandBox.View, ROT) szukamy po nazwie - CrashScribe nie ma do nich
    /// referencji, a brak typu to nie blad, tylko linia "BRAK" w samosprawdzeniu na starcie.
    /// </summary>
    internal static class AutotestUi
    {
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;

        // ------------------------------------------------------------------ typy po nazwie

        internal static readonly string[][] Types =
        {
            new[] { "query",      "TaleWorlds.MountAndBlade.GauntletUI",        "TaleWorlds.MountAndBlade.GauntletUI.GauntletQueryManager" },
            new[] { "popup",      "TaleWorlds.MountAndBlade.ViewModelCollection", "TaleWorlds.MountAndBlade.ViewModelCollection.Inquiries.PopUpBaseVM" },
            new[] { "multi",      "TaleWorlds.MountAndBlade.ViewModelCollection", "TaleWorlds.MountAndBlade.ViewModelCollection.Inquiries.MultiSelectionQueryPopUpVM" },
            new[] { "text",       "TaleWorlds.MountAndBlade.ViewModelCollection", "TaleWorlds.MountAndBlade.ViewModelCollection.Inquiries.TextQueryPopUpVM" },
            new[] { "element",    "TaleWorlds.Core.ViewModelCollection",        "TaleWorlds.Core.ViewModelCollection.Information.InquiryElementVM" },
            new[] { "scene",      "TaleWorlds.MountAndBlade.GauntletUI",        "TaleWorlds.MountAndBlade.GauntletUI.SceneNotification.GauntletSceneNotification" },
            new[] { "sceneVm",    "TaleWorlds.Core.ViewModelCollection",        "TaleWorlds.Core.ViewModelCollection.Information.SceneNotificationVM" },
            new[] { "ccScreen",   "SandBox.View",                               "SandBox.View.CharacterCreation.CharacterCreationScreen" },
            new[] { "ccView",     "SandBox.View",                               "SandBox.View.CharacterCreation.CharacterCreationStageViewBase" },
            new[] { "ccVm",       "TaleWorlds.CampaignSystem.ViewModelCollection", "TaleWorlds.CampaignSystem.ViewModelCollection.CharacterCreation.CharacterCreationStageBaseVM" },
            new[] { "ccCulture",  "TaleWorlds.CampaignSystem.ViewModelCollection", "TaleWorlds.CampaignSystem.ViewModelCollection.CharacterCreation.CharacterCreationCultureVM" },
            new[] { "ccOption",   "TaleWorlds.CampaignSystem.ViewModelCollection", "TaleWorlds.CampaignSystem.ViewModelCollection.CharacterCreation.CharacterCreationOptionVM" },
            new[] { "mapScreen",  "SandBox.View",                               "SandBox.View.Map.MapScreen" },
            new[] { "incView",    "SandBox.View",                               "SandBox.View.Map.MapIncidentView" },
            new[] { "incVm",      "SandBox.ViewModelCollection",                "SandBox.ViewModelCollection.Map.Incidents.MapIncidentVM" },
            new[] { "rotEvent",   "ROT",                                        "ROT.CampaignBehaviors.EventPopupState" },
            new[] { "rotPanel",   "ROT",                                        "ROT.ViewModels.EventPanel" },
            new[] { "bkUi",       "BannerKings",                                "BannerKings.UI.UIManager" },
        };

        private static readonly Dictionary<string, Type> Found = new Dictionary<string, Type>();
        private static readonly Dictionary<string, double> Missed = new Dictionary<string, double>();
        private static double _now;

        internal static Type T(string key)
        {
            Type t;
            if (Found.TryGetValue(key, out t)) return t;
            double last;
            if (Missed.TryGetValue(key, out last) && _now - last < 5.0) return null;
            var spec = Types.FirstOrDefault(s => s[0] == key);
            if (spec == null) return null;
            t = FindType(spec[1], spec[2]);
            if (t != null) { Found[key] = t; Missed.Remove(key); }
            else Missed[key] = _now;
            return t;
        }

        internal static Type FindType(string asm, string full)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (!string.Equals(a.GetName().Name, asm, StringComparison.OrdinalIgnoreCase)) continue;
                    var t = a.GetType(full, false);
                    if (t != null) return t;
                }
                catch { }
            }
            return null;
        }

        // ------------------------------------------------------------------ odbicia (z pamiecia)

        private static readonly Dictionary<string, MemberInfo> Members = new Dictionary<string, MemberInfo>();

        private static MemberInfo Member(Type type, string name)
        {
            if (type == null) return null;
            string key = type.FullName + "|" + name;
            MemberInfo mi;
            if (Members.TryGetValue(key, out mi)) return mi;
            for (var t = type; t != null && mi == null; t = t.BaseType)
            {
                try { mi = (MemberInfo)t.GetProperty(name, All | BindingFlags.DeclaredOnly) ?? t.GetField(name, All | BindingFlags.DeclaredOnly); }
                catch (AmbiguousMatchException) { mi = t.GetProperties(All | BindingFlags.DeclaredOnly).FirstOrDefault(x => x.Name == name && x.GetIndexParameters().Length == 0); }
            }
            Members[key] = mi;
            return mi;
        }

        internal static object Get(object o, string name)
        {
            if (o == null) return null;
            var mi = Member(o.GetType(), name);
            var p = mi as PropertyInfo;
            if (p != null) return p.GetValue(o, null);
            var f = mi as FieldInfo;
            return f != null ? f.GetValue(o) : null;
        }

        internal static object GetStatic(Type type, string name)
        {
            var mi = Member(type, name);
            var p = mi as PropertyInfo;
            if (p != null) return p.GetValue(null, null);
            var f = mi as FieldInfo;
            return f != null ? f.GetValue(null) : null;
        }

        internal static bool Set(object o, string name, object v)
        {
            if (o == null) return false;
            var mi = Member(o.GetType(), name);
            var p = mi as PropertyInfo;
            if (p != null && p.CanWrite) { p.SetValue(o, v, null); return true; }
            var f = mi as FieldInfo;
            if (f != null) { f.SetValue(o, v); return true; }
            return false;
        }

        internal static object Call(object o, string name)
        {
            if (o == null) return null;
            for (var t = o.GetType(); t != null; t = t.BaseType)
            {
                var m = t.GetMethod(name, All | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                if (m != null) return m.Invoke(o, null);
            }
            throw new MissingMethodException(o.GetType().FullName, name);
        }

        internal static bool Has(object o, string name)
        {
            if (o == null) return false;
            for (var t = o.GetType(); t != null; t = t.BaseType)
                if (t.GetMethod(name, All | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null) != null) return true;
            return false;
        }

        private static bool B(object v) { return v is bool && (bool)v; }
        private static int I(object v) { return v is int ? (int)v : 0; }
        private static string S(object v) { return v == null ? "" : v.ToString(); }

        /// <summary>Pierwsze pole obiektu, ktorego typ (albo typ bazowy) nazywa sie typeName.</summary>
        internal static object FieldOfType(object o, string typeName)
        {
            if (o == null) return null;
            for (var t = o.GetType(); t != null; t = t.BaseType)
            {
                foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    for (var ft = f.FieldType; ft != null; ft = ft.BaseType)
                        if (ft.Name == typeName) return f.GetValue(o);
                }
            }
            return null;
        }

        // ------------------------------------------------------------------ okna: wspolny stan

        private static object _seen;
        private static double _seenSince, _lastTry;
        private static int _tries, _phase;
        // (tytul|tresc, czas rzeczywisty, czas kampanii w ms albo -1 poza kampania)
        private static readonly List<Tuple<string, double, double>> Recent = new List<Tuple<string, double, double>>();

        private static bool Fresh(object key, double t)
        {
            if (ReferenceEquals(key, _seen)) return false;
            _seen = key;
            _seenSince = t;
            _lastTry = 0;
            _tries = 0;
            _phase = 0;
            return true;
        }

        /// <summary>
        /// Petla = TO SAMO okno (tytul + tresc) 6 razy w 3 minuty, a czas kampanii przez ten czas
        /// prawie stoi (ponizej 1 h gry) - odmowa przywraca to samo okno i nic innego sie nie dzieje.
        /// Tylko wtedy odwracamy odpowiedz. Rozne okna z tym samym tytulem (kilka krukow, kilka ofert
        /// malzenstwa czy sluzby) przy plynacym czasie to NIE petla - kazde dostaje zwykla odmowe.
        /// </summary>
        private static bool Looping(string title, string body, double t)
        {
            string key = (title ?? "") + "|" + (body ?? "");
            double cm = CampaignMs();
            Recent.RemoveAll(x => t - x.Item2 > 180);
            Recent.Add(Tuple.Create(key, t, cm));
            var same = Recent.Where(x => x.Item1 == key).ToList();
            if (same.Count < 6) return false;
            if (same.All(x => x.Item3 < 0)) return true;          // menu glowne / kreator: liczy sie czas rzeczywisty
            if (same.Any(x => x.Item3 < 0)) return false;         // przejscie menu -> kampania: to nie jedno okno w kolko
            return same.Max(x => x.Item3) - same.Min(x => x.Item3) < 3600000.0;
        }

        private static double CampaignMs()
        {
            try { return Campaign.Current != null ? CampaignTime.Now.ToMilliseconds : -1.0; }
            catch { return -1.0; }
        }

        /// <summary>Kazde okno: widoczne co najmniej 1 s, kolejne proby co 2.5 s, najwyzej 6.</summary>
        private static bool Due(double t, double minVisible)
        {
            if (t - _seenSince < minVisible || t - _lastTry < 2.5) return false;
            _lastTry = t;
            _tries++;
            if (_tries == 7) Autotest.Note("OKNO nie daje sie zamknac po 6 probach: " + OpenWindowName() + " | " + Autotest.Where(), true);
            return _tries <= 6;
        }

        // ------------------------------------------------------------------ okna: wejscie

        /// <summary>Zwraca true, gdy jakies okno jest otwarte (wtedy etap autotestu czeka).</summary>
        internal static bool Handle(double t)
        {
            _now = t;
            object o;
            if ((o = ActiveScene()) != null) { HandleScene(o, t); return true; }
            if ((o = ActivePopup()) != null) { HandlePopup(o, t); return true; }
            if ((o = ActiveRotEvent()) != null) { HandleRotEvent(o, t); return true; }
            if ((o = ActiveEscMenu()) != null) { HandleEscMenu(o, t); return true; }
            if ((o = ActiveIncident()) != null) { HandleIncident(o, t); return true; }
            _seen = null;
            return false;
        }

        internal static bool AnyWindowOpen()
        {
            try { return ActiveScene() != null || ActivePopup() != null || ActiveRotEvent() != null || ActiveEscMenu() != null || ActiveIncident() != null; }
            catch { return false; }
        }

        internal static string OpenWindowName()
        {
            object o;
            if ((o = ActiveScene()) != null) return "scena \"" + SceneTitle(o) + "\"";
            if ((o = ActivePopup()) != null) return "zapytanie \"" + S(Get(o, "TitleText")) + "\"";
            if ((o = ActiveRotEvent()) != null) return "wydarzenie ROT \"" + S(Get(o, "EventTitle")) + "\"";
            if ((o = ActiveEscMenu()) != null) return "menu Esc gry";
            if ((o = ActiveIncident()) != null) return "zdarzenie mapy \"" + S(Get(o, "Title")) + "\"";
            return null;
        }

        // ------------------------------------------------------------------ menu Esc mapy (gra otwiera je sama przy utracie fokusu)

        /// <summary>
        /// [AT1b] Bieg 07.10 05:09:39: okno gry stracilo fokus (rgl_log "OnGameWindowFocusChange: False"), a Jeff ma
        /// w BannerlordConfig.txt StopGameOnFocusLost=True - MapScreen.OnFocusChangeOnGameWindow otworzyl menu Esc
        /// (OnEscapeMenuToggled(true) -> GameStateManager.RegisterActiveStateDisableRequest). Wstrzymany MapState
        /// dostaje tylko OnIdleTick: czas kampanii i SaveTick stoja, dopoki menu jest otwarte (powrot fokusu go
        /// nie zamyka). Zamkniecie jak "Return to the Game": MapScreen.CloseEscapeMenu(). NavalMapScreen (NavalDLC)
        /// dziedziczy to bez zmian.
        /// </summary>
        private static object ActiveEscMenu()
        {
            // tylko przy mapie na wierzchu: przy wyjsciu do menu glownego ekran mapy jest juz sprzatany
            if (!(Autotest.ActiveState() is global::TaleWorlds.CampaignSystem.GameState.MapState)) return null;
            var tm = T("mapScreen");
            if (tm == null) return null;
            var inst = GetStatic(tm, "Instance");
            return inst != null && B(Get(inst, "IsEscapeMenuOpened")) ? inst : null;
        }

        private static void HandleEscMenu(object screen, double t)
        {
            if (Fresh(Get(screen, "_escapeMenuView") ?? screen, t) || !Due(t, 1.0)) return;
            bool? f = WindowFocused();
            Autotest.Note("OKNO menu Esc gry (gra otwiera je sama po utracie fokusu okna - opcja Stop Game On Focus Lost; dopoki jest otwarte, czas kampanii i zapis stoja; okno gry "
                          + (f == false ? "BEZ fokusu" : f == true ? "z fokusem" : "?") + ") -> zamykam [Return to the Game]", true);
            Autotest.CountWindow();
            Call(screen, "CloseEscapeMenu");
        }

        private static FieldInfo _focusField;

        /// <summary>Czy okno gry ma fokus (ScreenManager._isWindowFocused); null = nie wiadomo.</summary>
        internal static bool? WindowFocused()
        {
            try
            {
                if (_focusField == null) _focusField = typeof(ScreenManager).GetField("_isWindowFocused", BindingFlags.NonPublic | BindingFlags.Static);
                var v = _focusField != null ? _focusField.GetValue(null) : null;
                return v is bool ? (bool?)(bool)v : null;
            }
            catch { return null; }
        }

        // ------------------------------------------------------------------ zapytania (InformationManager / MBInformationManager)

        private static object ActivePopup()
        {
            var tq = T("query");
            return tq != null ? GetStatic(tq, "_activeDataSource") : null;
        }

        private static void HandlePopup(object vm, double t)
        {
            object data = GetStatic(T("query"), "_activeQueryData") ?? vm;
            if (Fresh(data, t) || !Due(t, 1.0)) return;
            string title = S(Get(vm, "TitleText"));
            string body = Autotest.OneLine(S(Get(vm, "PopUpLabel")), 140);
            bool okShown = B(Get(vm, "IsButtonOkShown")), okOn = B(Get(vm, "IsButtonOkEnabled"));
            bool noShown = B(Get(vm, "IsButtonCancelShown"));
            object noOnRaw = Get(vm, "IsButtonCancelEnabled");
            bool noOn = noOnRaw == null || B(noOnRaw);
            bool decline = Autotest.PreferDecline;
            bool loop = _tries == 1 && Looping(title, body, t);
            if (loop) decline = !decline;
            string loopMark = loop ? " [PETLA - odwrotna odpowiedz]" : "";

            if (data is MultiSelectionInquiryData)
            {
                // lista nie ustawia IsButtonCancelEnabled (zawsze false) - liczy sie samo IsExitShown
                HandleMulti(vm, title, body, noShown, decline, loopMark);
                return;
            }
            if (data is TextInquiryData)
            {
                if (!okOn && Set(vm, "InputText", "Autotest")) okOn = B(Get(vm, "IsButtonOkEnabled"));
                if (okOn) Press(vm, true, "zapytanie (tekst)", title, body, S(Get(vm, "InputText")) + loopMark);
                else if (noShown) Press(vm, false, "zapytanie (tekst)", title, body, loopMark);
                return;
            }
            bool yes;
            if (decline && noShown && noOn) yes = false;
            else if (okShown && okOn) yes = true;
            else if (noShown && noOn) yes = false;
            else { if (_tries == 1) Autotest.Note("OKNO zapytanie \"" + title + "\" bez czynnego przycisku - czekam", true); return; }
            Press(vm, yes, "zapytanie", title, body, loopMark);
        }

        /// <summary>
        /// Lista wyboru. W kampanii (decline) z czynnym wyjsciem - wyjscie bez wyboru (odmowa, nie
        /// akceptacja nieznanej rzeczy). Wyjatek: pozycja "Finish" (powitanie BK) - to jawne "nic
        /// nie wybieram, zaczynamy", wiec ja wybieramy. Bez wyjscia: "Finish", dalej od konca listy.
        /// </summary>
        private static void HandleMulti(object vm, string title, string body, bool canExit, bool decline, string loopMark)
        {
            var elems = (Get(vm, "InquiryElements") as IEnumerable ?? new object[0]).Cast<object>().ToList();
            int min = I(Get(vm, "MinSelectableOptionCount"));
            var enabled = elems.Where(e => B(Get(e, "IsEnabled"))).ToList();
            var fin = enabled.FirstOrDefault(e => IsFinish(S(Get(e, "Text"))));
            if (fin == null && canExit && decline)
            {
                // pusty wybor + OK bywa "odznacz wszystko", a wybor czegokolwiek - akceptacja; wyjscie jest bierne
                Press(vm, false, "wybor z listy", title, body, "zamkniete bez wyboru (z " + elems.Count + ", min " + min + ")" + loopMark);
                return;
            }
            var chosen = new List<object>();
            if (fin != null) chosen.Add(fin);
            for (int i = enabled.Count - 1; i >= 0 && chosen.Count < min; i--)
                if (!chosen.Contains(enabled[i])) chosen.Add(enabled[i]);
            foreach (var e in elems) if (!chosen.Contains(e) && B(Get(e, "IsSelected"))) Set(e, "IsSelected", false);
            foreach (var e in chosen) if (!B(Get(e, "IsSelected"))) Set(e, "IsSelected", true);
            bool okOn = B(Get(vm, "IsButtonOkEnabled"));
            string picked = chosen.Count == 0 ? "nic" : string.Join(" | ", chosen.Select(e => "\"" + S(Get(e, "Text")) + "\"").ToArray());
            if (okOn) Press(vm, true, "wybor z listy", title, body, "wybrane: " + picked + " (z " + elems.Count + ")" + loopMark);
            else if (canExit) Press(vm, false, "wybor z listy", title, body, "OK nieczynne przy wyborze " + picked + loopMark);
            else if (_tries == 1) Autotest.Note("OKNO wybor z listy \"" + title + "\" - OK nieczynne, brak wyjscia (wybrane " + picked + ")", true);
        }

        private static bool IsFinish(string s)
        {
            s = (s ?? "").ToLowerInvariant();
            return s.Contains("finish") || s.Contains("zakoncz") || s.Contains("zako\u0144cz") || s == "done";
        }

        private static void Press(object vm, bool yes, string kind, string title, string body, string extra)
        {
            string label = S(Get(vm, yes ? "ButtonOkLabel" : "ButtonCancelLabel"));
            Autotest.Note("OKNO " + kind + " \"" + title + "\"" + (body.Length > 0 ? " (" + body + ")" : "") + " -> "
                          + (yes ? "TAK" : "NIE") + " [" + label + "]" + (string.IsNullOrEmpty(extra) ? "" : " " + extra.Trim()), true);
            Autotest.CountWindow();
            Call(vm, yes ? "ExecuteAffirmativeAction" : "ExecuteNegativeAction");
        }

        // ------------------------------------------------------------------ sceny (MBInformationManager.ShowSceneNotification)

        private static object ActiveScene()
        {
            var ts = T("scene");
            if (ts == null) return null;
            var cur = GetStatic(ts, "Current");
            if (cur == null || !B(Get(cur, "IsActive"))) return null;
            var vm = Get(cur, "_dataSource");
            return vm != null && Get(vm, "ActiveData") != null ? vm : null;
        }

        private static string SceneTitle(object vm)
        {
            var data = Get(vm, "ActiveData");
            return data == null ? "" : S(Get(data, "TitleText")) + " [" + S(Get(data, "SceneID")) + "]";
        }

        private static void HandleScene(object vm, double t)
        {
            var data = Get(vm, "ActiveData");
            if (Fresh(data, t)) return;
            bool ready = B(Get(vm, "IsReady"));
            if (_phase == 0)
            {
                // scena musi sie wczytac (OnPositiveAction rusza postacie sceny) - jak gracz, ktory czeka na przycisk
                if (t - _seenSince < 2.0 || (!ready && t - _seenSince < 10.0)) return;
                bool aff = B(Get(data, "IsAffirmativeOptionShown"));
                Autotest.Note("OKNO scena \"" + SceneTitle(vm) + "\" -> " + (aff ? "TAK [" + S(Get(data, "AffirmativeText")) + "], potem zamkniecie" : "zamkniecie"), true);
                Autotest.CountWindow();
                _phase = 1;
                _lastTry = t;
                if (aff) Call(vm, "ExecuteAffirmativeProcess");
                return;
            }
            if (_phase == 1 && t - _lastTry > 2.5) { _phase = 2; _lastTry = t; Call(vm, "ExecuteClose"); return; }
            if (_phase == 2 && t - _lastTry > 5.0)
            {
                _phase = 3;
                Autotest.Note("OKNO scena \"" + SceneTitle(vm) + "\" nadal otwarta - MBInformationManager.HideSceneNotification", true);
                MBInformationManager.HideSceneNotification();
            }
        }

        // ------------------------------------------------------------------ wydarzenia fabuly ROT (EventPopupState / EventScreen)

        private static object ActiveRotEvent()
        {
            var te = T("rotEvent");
            if (te == null) return null;
            var st = Autotest.ActiveState();
            if (st == null || !te.IsInstanceOfType(st)) return null;
            return FieldOfType(ScreenManager.TopScreen, "EventPanel");
        }

        private static void HandleRotEvent(object panel, double t)
        {
            if (Fresh(panel, t) || !Due(t, 1.5)) return;
            string title = S(Get(panel, "EventTitle"));
            bool done = B(Get(panel, "IsDoneEnabled")), cancel = B(Get(panel, "IsCancelEnabled"));
            bool yes = !(Autotest.PreferDecline && cancel) && done;
            if (!yes && !cancel) { if (_tries == 1) Autotest.Note("OKNO wydarzenie ROT \"" + title + "\" bez czynnego przycisku - czekam", true); return; }
            Autotest.Note("OKNO wydarzenie ROT \"" + title + "\" (" + Autotest.OneLine(S(Get(panel, "EventUpperText")), 120) + ") -> "
                          + (yes ? "[" + S(Get(panel, "DoneButtonText")) + "]" : "[" + S(Get(panel, "CancelButtonText")) + "]"), true);
            Autotest.CountWindow();
            Call(panel, yes ? "ExecuteDone" : "ExecuteCancel");
        }

        // ------------------------------------------------------------------ zdarzenia mapy (incydenty w czasie czekania)

        private static MethodInfo _getMapView;

        private static object ActiveIncident()
        {
            var tm = T("mapScreen");
            var tv = T("incView");
            if (tm == null || tv == null) return null;
            var inst = GetStatic(tm, "Instance");
            if (inst == null) return null;
            if (_getMapView == null)
            {
                var g = tm.GetMethods(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(m => m.Name == "GetMapView" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
                if (g == null) return null;
                _getMapView = g.MakeGenericMethod(tv);
            }
            var view = _getMapView.Invoke(inst, null);
            return view != null ? FieldOfType(view, "MapIncidentVM") : null;
        }

        private static void HandleIncident(object vm, double t)
        {
            if (Fresh(vm, t) || !Due(t, 1.5)) return;
            var opts = (Get(vm, "Options") as IEnumerable ?? new object[0]).Cast<object>().ToList();
            if (opts.Count == 0) { if (_tries == 1) Autotest.Note("OKNO zdarzenie mapy \"" + S(Get(vm, "Title")) + "\" bez opcji - czekam", true); return; }
            var first = opts[0];
            Call(first, "ExecuteSelect");
            if (!B(Get(vm, "CanConfirm"))) return;
            Autotest.Note("OKNO zdarzenie mapy \"" + S(Get(vm, "Title")) + "\" -> pierwsza opcja \"" + Autotest.OneLine(S(Get(first, "Description")), 120) + "\" (z " + opts.Count + ")", true);
            Autotest.CountWindow();
            Call(vm, "ExecuteConfirm");
        }

        // ------------------------------------------------------------------ okno BK (UIManager) - tylko przy postoju

        internal static bool TryCloseBkWindow()
        {
            try
            {
                var tb = T("bkUi");
                if (tb == null) return false;
                var inst = GetStatic(tb, "instance");
                if (inst == null || Get(inst, "mapView") == null) return false;
                Autotest.Note("ODBLOKOWANIE: zamykam okno Banner Kings (UIManager.CloseUI)", true);
                Call(inst, "CloseUI");
                return true;
            }
            catch (Exception e) { Autotest.Fail("TryCloseBkWindow", e); return false; }
        }

        // ------------------------------------------------------------------ kreator postaci

        private static string _ccKey;
        private static double _ccSince, _ccLast;
        private static int _ccTries;

        /// <summary>
        /// Jeden krok kreatora na 1.5 s: etap ma model widoku z CanAdvance/OnNextStage (kultura,
        /// opowiesc, nazwa rodu, przeglad, opcje) - jak Enter; gdy nie moze isc dalej, pierwszy wybor
        /// z listy (jak klikniecie). Etap bez takiego modelu (twarz, herb) - przycisk "dalej" widoku.
        /// </summary>
        internal static void DriveCreation(CharacterCreationState st, double t)
        {
            _now = t;
            var mgr = st.CharacterCreationManager;
            if (mgr == null) return;
            int idx = mgr.GetIndexOfCurrentStage(), total = mgr.GetTotalStagesCount();
            var stage = mgr.CurrentStage;
            string stageName = stage != null ? stage.GetType().Name : "(brak)";
            string menu = null;
            try { menu = mgr.CurrentMenu != null ? mgr.CurrentMenu.StringId : null; } catch { }
            string key = idx + ":" + stageName + ":" + menu;
            if (key != _ccKey)
            {
                _ccKey = key;
                _ccSince = t;
                _ccTries = 0;
                Autotest.Note("ETAP kreator: etap " + (idx + 1) + "/" + total + " " + stageName + (menu != null ? " (menu " + menu + ")" : ""), true);
                return;
            }
            if (t - _ccSince > 180) { Autotest.CreationStuck(stageName + (menu != null ? "/" + menu : "") + " od 3 min", t); return; }
            if (t - _ccSince < 1.5 || t - _ccLast < 1.5) return;
            _ccLast = t;
            _ccTries++;

            var screen = ScreenManager.TopScreen;
            object view = Get(screen, "_currentStageView");
            if (view == null)
            {
                if (_ccTries % 10 == 1) Autotest.Note("ETAP kreator: brak widoku etapu (ekran " + (screen != null ? screen.GetType().Name : "-") + ")");
                return;
            }
            object vm = FieldOfType(view, "CharacterCreationStageBaseVM");
            if (vm != null)
            {
                bool can = B(Get(vm, "CanAdvance"));
                string what = "";
                if (!can) { what = Choose(vm); can = B(Get(vm, "CanAdvance")); }
                if (can)
                {
                    Autotest.Note("  kreator: " + vm.GetType().Name + (what.Length > 0 ? " - " + what : "") + " -> dalej");
                    Call(vm, "OnNextStage");
                    return;
                }
                if (_ccTries < 6) return;
                Autotest.Note("  kreator: " + vm.GetType().Name + " nie pozwala isc dalej (" + what + ") - przycisk widoku");
            }
            Autotest.Note("  kreator: " + view.GetType().Name + " -> NextStage (przycisk \"dalej\" widoku)");
            Call(view, "NextStage");
        }

        /// <summary>Wybor domyslny: pierwsza kultura / pierwsza opcja opowiesci / nazwa, gdy pusta.</summary>
        private static string Choose(object vm)
        {
            var cultures = Get(vm, "Cultures") as IEnumerable;
            if (cultures != null)
            {
                var first = cultures.Cast<object>().FirstOrDefault();
                if (first == null) return "brak kultur";
                if (Has(first, "ExecuteSelectCulture")) Call(first, "ExecuteSelectCulture");
                return "kultura " + S(Get(first, "CultureID")) + " (" + S(Get(first, "NameText")) + ")";
            }
            var list = Get(vm, "SelectionList") as IEnumerable;
            if (list != null)
            {
                var first = list.Cast<object>().FirstOrDefault();
                if (first == null) return "pusta lista";
                Call(first, "ExecuteSelect");
                return "opcja \"" + Autotest.OneLine(S(Get(first, "ActionText")), 80) + "\"";
            }
            foreach (var name in new[] { "ClanName", "Name" })
            {
                var v = Get(vm, name) as string;
                if (v != null && string.IsNullOrWhiteSpace(v) && Set(vm, name, "Autotest")) return name + " = Autotest";
            }
            return "brak wyboru";
        }

        // ------------------------------------------------------------------ samosprawdzenie (na starcie i w probie poza gra)

        private static readonly string[][] Need =
        {
            new[] { "query", "_activeDataSource" }, new[] { "query", "_activeQueryData" },
            new[] { "popup", "TitleText" }, new[] { "popup", "PopUpLabel" }, new[] { "popup", "ButtonOkLabel" }, new[] { "popup", "ButtonCancelLabel" },
            new[] { "popup", "IsButtonOkShown" }, new[] { "popup", "IsButtonOkEnabled" }, new[] { "popup", "IsButtonCancelShown" }, new[] { "popup", "IsButtonCancelEnabled" },
            new[] { "popup", "ExecuteAffirmativeAction()" }, new[] { "popup", "ExecuteNegativeAction()" },
            new[] { "multi", "InquiryElements" }, new[] { "multi", "MinSelectableOptionCount" },
            new[] { "text", "InputText" },
            new[] { "element", "IsSelected" }, new[] { "element", "IsEnabled" }, new[] { "element", "Text" },
            new[] { "scene", "Current" }, new[] { "scene", "IsActive" }, new[] { "scene", "_dataSource" },
            new[] { "sceneVm", "ActiveData" }, new[] { "sceneVm", "IsReady" }, new[] { "sceneVm", "ExecuteAffirmativeProcess()" }, new[] { "sceneVm", "ExecuteClose()" },
            new[] { "ccScreen", "_currentStageView" }, new[] { "ccView", "NextStage()" },
            new[] { "ccVm", "CanAdvance" }, new[] { "ccVm", "OnNextStage()" },
            new[] { "ccCulture", "ExecuteSelectCulture()" }, new[] { "ccCulture", "CultureID" },
            new[] { "ccOption", "ExecuteSelect()" }, new[] { "ccOption", "ActionText" },
            new[] { "mapScreen", "Instance" }, new[] { "mapScreen", "GetMapView<>" },
            new[] { "mapScreen", "IsEscapeMenuOpened" }, new[] { "mapScreen", "_escapeMenuView" }, new[] { "mapScreen", "CloseEscapeMenu()" },
            new[] { "incVm", "Options" }, new[] { "incVm", "CanConfirm" }, new[] { "incVm", "ExecuteConfirm()" }, new[] { "incVm", "Title" },
            new[] { "rotPanel", "EventTitle" }, new[] { "rotPanel", "IsDoneEnabled" }, new[] { "rotPanel", "IsCancelEnabled" }, new[] { "rotPanel", "ExecuteDone()" }, new[] { "rotPanel", "ExecuteCancel()" },
            new[] { "bkUi", "instance" }, new[] { "bkUi", "mapView" }, new[] { "bkUi", "CloseUI()" },
        };

        internal static List<string> SelfCheck(bool onlyMissing = false)
        {
            var res = new List<string>();
            var miss = new List<string>();
            int ok = 0;
            foreach (var spec in Types)
            {
                Missed.Remove(spec[0]);
                if (T(spec[0]) == null) miss.Add("typ " + spec[2] + " (" + spec[1] + ")");
            }
            foreach (var n in Need)
            {
                var t = T(n[0]);
                if (t == null) continue;
                bool has;
                string m = n[1];
                if (m.EndsWith("<>"))
                    has = t.GetMethods(BindingFlags.Public | BindingFlags.Instance).Any(x => x.Name == m.Substring(0, m.Length - 2) && x.IsGenericMethodDefinition);
                else if (m.EndsWith("()"))
                {
                    has = false;
                    for (var x = t; x != null && !has; x = x.BaseType)
                        has = x.GetMethod(m.Substring(0, m.Length - 2), All | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null) != null;
                }
                else has = Member(t, m) != null;
                if (has) ok++; else miss.Add(t.Name + "." + m);
            }
            // metody gry, ktore lata autotest
            var ta = typeof(SaveHandler).GetMethod("TryAutoSave", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(bool) }, null);
            var fa = typeof(SaveHandler).GetMethod("ForceAutoSave", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            var ow = typeof(MBSaveLoad).GetMethod("OverwriteSaveAux", BindingFlags.Static | BindingFlags.NonPublic);
            if (ta != null) ok++; else miss.Add("SaveHandler.TryAutoSave(bool)");
            if (fa != null) ok++; else miss.Add("SaveHandler.ForceAutoSave()");
            if (ow != null && ow.GetParameters().Any(p => p.Name == "saveName" && p.ParameterType == typeof(string))) ok++; else miss.Add("MBSaveLoad.OverwriteSaveAux(.., string saveName, ..)");
            // [AT1b] pola czytane tylko do opisu postoju (brak = brak tej czesci opisu, nie blad)
            var dr = typeof(GameStateManager).GetField("_activeStateDisableRequests", BindingFlags.Instance | BindingFlags.NonPublic);
            var wf = typeof(ScreenManager).GetField("_isWindowFocused", BindingFlags.Static | BindingFlags.NonPublic);
            if (dr != null) ok++; else miss.Add("GameStateManager._activeStateDisableRequests");
            if (wf != null) ok++; else miss.Add("ScreenManager._isWindowFocused");

            if (!onlyMissing) res.Add("typow " + (Types.Length - miss.Count(x => x.StartsWith("typ "))) + "/" + Types.Length + ", skladowych zgodnych " + ok + (miss.Count == 0 ? " - komplet" : ""));
            foreach (var m in miss) res.Add("BRAK " + m);
            return res;
        }
    }
}
