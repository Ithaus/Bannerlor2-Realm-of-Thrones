using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;
using TaleWorlds.TwoDimension;

namespace Armoury
{
    /// <summary>
    /// m105-dziennik (Jeff 21.10 "tak, rob dziennik"). Dziennik wiadomosci na mapie kampanii (lewy dolny rog: kruki, pojmania, wiesci) lezal NA panelu menu osady
    /// ("You are waiting in The Eyrie") i go zaslanial; nad nazwami miast staly wiersze handlu karawan i wiesniakow ("Caravan of X Sold Jewelry x1 ...").
    /// Bez XML i bez UIExtenderEx - wszystko z kodu (dekompilacja 1.4.8):
    ///  - dziennik to film "SPChatLog" w warstwie globalnej GauntletChatLogView (TaleWorlds.MountAndBlade.GauntletUI - bez referencji, odbicie raz przy starcie);
    ///    ChatLogWidget ma marginesy tylko jako stale atrybuty prefabu (MarginLeft 27, MarginBottom 32) i sam ich nie rusza - przestawiamy je z kodu;
    ///  - P1 przesuniecie (klucz MapChatLogClearOfMenu): tylko w czystym stanie menu na mapie (MapScreen na wierzchu, IsInMenu, bez okien nad menu, rozmowy i pytan)
    ///    dziennik idzie na prawo od menu: MarginLeft 560 (menu 27 + 493 + ramka 25 + odstep 15), MarginBottom 150 (dol ramki nad napisem PAUSED); poza tym
    ///    wartosci z prefabu, odczytane z widzetu przy znalezieniu. Bez animacji. Okna ROT to osobny ekran - TopScreen przestaje byc MapScreen, dziennik wraca;
    ///  - P2 ramka (klucz MapChatLogFrame): wlasny kontener "ArmChatLogFrame" jako dziecko 0 ChatLogWidget (pod tekstem) - tlo BlankWhiteSquare_9 #000000CC i dwie
    ///    belki General\TooltipHint\tooltip_frame (jak dymek ROT / ArmMapVillageTooltip.xml); wysokosc = widoczne linie (Alpha > 0.01 od konca MessageHistory,
    ///    suma wysokosci ostatnich k dzieci InnerPanel) + 2 x 10, najwyzej pole tekstu; tylko na mapie kampanii, gdy gracz nie przeglada i nie pisze;
    ///  - P3 filtr handlu (klucz MapHideTradeNotices): prefiks na prywatnej SettlementNameplateNotificationsVM.OnCaravanTransactionCompleted (tabliczka osady) - wiersz
    ///    nie powstaje; zdarzenie gry dalej dochodzi do BK i naszej ekonomii (TownFolk.OnCaravanTrade). Wiersze rekrutow, jencow i okretow bez zmian.
    /// Takt 4 razy na sekunde z OnApplicationTick (SubModuleMain.ChatLogTick - NoInlining, licznik bledow ladowania typu); wlasciwosci widzetow tylko przy zmianie,
    /// poza taktem nic (licznik czasu i powrot). Potkniecia liczone (pierwsze 3 i co 200 do Log.Error), nic sie nie wylacza. Nic do zapisu gry.
    /// Autotest: DLL autotestu CrashScribe (LogGate.Mode 1/2) + plik CrashScribe\armoury-dziennik.flag - 3 linie testowe, pomiar prostokatow i zdjecie silnika
    /// (raz na mapie bez menu, do 2 razy w menu); progi sprawdz_logi --grupa m105dziennik.
    /// </summary>
    internal static class MapChatLog
    {
        internal const string P = "Dziennik (m105): ";
        internal const string AutoFlagName = "armoury-dziennik.flag";
        private const float MovedLeft = 560f, MovedBottom = 150f;   // jednostki UI 1920x1080
        private const float FramePad = 10f;                          // odstep ramki nad i pod liniami
        private const float FrameBottom = 55f;                       // ScrollablePanelContainer MarginBottom 65 - FramePad
        private const float TextHeightCut = 95f;                     // pole tekstu = SizeY - 45 (pasek) - 5 - 65; ramka najwyzej pole + 2 x FramePad
        private const float PausedTop = 885f;                        // gorna granica napisu PAUSED (zrzut autotestu 18.10: y ok. 895..925, zapas 10)
        private const float BarTop = 1015f;                          // gorna krawedz lewych przyciskow paska mapy
        private const float MenuFrame = 27f;                         // ramka panelu menu po prawej (Frame1.Border 25 + 2)
        private const string TestTag = "Chat log test (autotest)";

        // ---- odbicie (raz na proces)
        private static bool _wired, _patchOk;
        private static string _patchWhy = "nie probowano";
        private static PropertyInfo _pCurrent, _pHist, _pInsp, _pTyp, _pAlpha, _pLine;
        private static FieldInfo _fData;
        private static bool _viewOk, _vmOk;

        // ---- widzet dziennika (tylko pamiec)
        private static GauntletLayer _layer;
        private static Widget _root, _w, _inner, _text, _frame;
        private static float _origLeft = 27f, _origBottom = 32f;
        private static bool _moved, _frameTried, _frameWasVis;
        private static float _frameH;
        private static int _lastK;
        private static int _findWait;      // takty do nastepnego szukania po nieudanym (co 5 s)
        private static int _finds, _movesLogged, _backsLogged;

        // ---- czas i kampania
        private static float _acc, _clock, _campAt, _movedAt;
        private static bool _campLine;

        // ---- test autotestu
        private static bool _testOn, _mapTestDone;
        private static int _menuTests, _testN, _probeStage;
        private static float _lastMenuTestAt = -1e9f, _probeAt;
        private static string _probeKind, _probeLine, _probePath;
        private static List<string> _probeFails = new List<string>();

        private static void Stumble(string where, Exception e)
        {
            int n = ++MapChatLogStats.St;
            if (n <= 3 || n % 200 == 0) Log.Error("MapChatLog." + where + " (potkniecie " + n + ")", e);
        }

        // ============================================================ start (raz na proces, OnBeforeInitialModuleScreenSetAsRoot)
        internal static void ApplyAll(Harmony h)
        {
            if (_wired) return;
            _wired = true;
            try
            {
                var t = AccessTools.TypeByName("SandBox.ViewModelCollection.Nameplate.NameplateNotifications.SettlementNotificationTypes.SettlementNameplateNotificationsVM");
                var m = t != null ? AccessTools.DeclaredMethod(t, "OnCaravanTransactionCompleted") : null;
                var f = t != null ? AccessTools.Field(t, "_settlement") : null;
                if (t == null) _patchWhy = "brak typu SettlementNameplateNotificationsVM";
                else if (m == null) _patchWhy = "brak metody OnCaravanTransactionCompleted";
                else if (f == null || f.FieldType != typeof(Settlement)) _patchWhy = "brak pola _settlement (Settlement)";
                else
                {
                    var ps = m.GetParameters();
                    if (ps.Length < 2 || ps[0].ParameterType != typeof(MobileParty) || ps[1].ParameterType != typeof(Town)) _patchWhy = "inne parametry metody";
                    else
                    {
                        h.Patch(m, prefix: new HarmonyMethod(typeof(MapChatLog), nameof(TradePrefix)));
                        _patchOk = HasOurs(m, h.Id);   // CLAUDE.md 7: wyjatek Harmony bywa polykany - sprawdzamy, czy prefiks naprawde siedzi
                        _patchWhy = _patchOk ? "prefiks wpiety" : "prefiks nie wszedl (brak po Patch)";
                    }
                }
            }
            catch (Exception e) { _patchWhy = "wyjatek " + e.GetType().Name; Log.Error("MapChatLog.ApplyAll (latka tabliczek)", e); }
            try { Resolve(); }
            catch (Exception e) { Log.Error("MapChatLog.ApplyAll (odbicie dziennika)", e); }
            Log.Info(P + "start - latka tabliczek (OnCaravanTransactionCompleted) " + (_patchOk ? "TAK" : "BRAK") + " (" + _patchWhy + "), widok dziennika (GauntletChatLogView) "
                     + (_viewOk ? "TAK" : "BRAK") + ", pola MPChatVM " + (_vmOk ? "TAK" : "BRAK") + ".");
        }

        private static bool HasOurs(MethodBase m, string id)
        {
            try
            {
                var info = Harmony.GetPatchInfo(m);
                if (info == null) return false;
                foreach (var p in info.Prefixes) if (p.owner == id && p.PatchMethod != null && p.PatchMethod.DeclaringType == typeof(MapChatLog)) return true;
            }
            catch { }
            return false;
        }

        /// <summary>Typy bez referencji: GauntletChatLogView (Current, _dataSource), MPChatVM (MessageHistory, IsInspectingMessages, IsTypingText), MPChatLineVM (Alpha,
        /// ChatLine). Typ VM z typu pola, typ linii z argumentu listy - bez szukania zestawow po nazwie.</summary>
        private static void Resolve()
        {
            if (!_viewOk)
            {
                var t = AccessTools.TypeByName("TaleWorlds.MountAndBlade.GauntletUI.GauntletChatLogView");
                if (t != null && typeof(GlobalLayer).IsAssignableFrom(t))
                {
                    _pCurrent = AccessTools.Property(t, "Current");
                    _fData = AccessTools.Field(t, "_dataSource");
                    _viewOk = _pCurrent != null && _pCurrent.GetGetMethod(true) != null && _pCurrent.GetGetMethod(true).IsStatic;
                }
            }
            if (_viewOk && !_vmOk && _fData != null)
            {
                Type vm = _fData.FieldType;
                _pHist = AccessTools.Property(vm, "MessageHistory");
                _pInsp = AccessTools.Property(vm, "IsInspectingMessages");
                _pTyp = AccessTools.Property(vm, "IsTypingText");
                Type line = _pHist != null && _pHist.PropertyType.IsGenericType ? _pHist.PropertyType.GetGenericArguments()[0] : null;
                _pAlpha = line != null ? AccessTools.Property(line, "Alpha") : null;
                _pLine = line != null ? AccessTools.Property(line, "ChatLine") : null;
                _vmOk = _pHist != null && typeof(IList).IsAssignableFrom(_pHist.PropertyType) && _pInsp != null && _pInsp.PropertyType == typeof(bool)
                        && _pTyp != null && _pTyp.PropertyType == typeof(bool) && _pAlpha != null && _pAlpha.PropertyType == typeof(float);
                if (_pLine != null && _pLine.PropertyType != typeof(string)) _pLine = null;
            }
        }

        // ============================================================ P3: filtr handlu na tabliczkach osad
        /// <summary>Prefiks SettlementNameplateNotificationsVM.OnCaravanTransactionCompleted. Liczy tylko tam, gdzie wiersz naprawde by powstal
        /// (pole _settlement == osada miasta - ten sam warunek co gra). false = wiersz nie powstaje (zdarzenie gry juz rozeslane - inni sluchacze bez zmian).</summary>
        public static bool TradePrefix(MobileParty __0, Town __1, Settlement ____settlement)
        {
            try
            {
                var s = Settings.Current;
                bool on = s != null && s.MapHideTradeNotices;
                bool here = __1 != null && ____settlement != null && __1.Owner != null && ReferenceEquals(____settlement, __1.Owner.Settlement);
                if (!on)
                {
                    if (here) MapChatLogStats.DPassed++;
                    return true;
                }
                if (here)
                {
                    if (__0 != null && __0.IsVillager) MapChatLogStats.DHidVil++;
                    else MapChatLogStats.DHidCar++;
                }
                return false;
            }
            catch (Exception e) { Stumble("TradePrefix", e); return true; }
        }

        // ============================================================ takt (OnApplicationTick)
        /// <summary>Co klatke z SubModuleMain.ChatLogTick: tylko liczniki czasu; praca 4 razy na sekunde.</summary>
        internal static void AppTick(float dt)
        {
            _clock += dt;
            _acc += dt;
            if (_acc < 0.25f) return;
            _acc = 0f;
            try { Step(); }
            catch (Exception e) { Stumble("Tick", e); }
        }

        private static void Step()
        {
            var s = Settings.Current;
            MapScreen ms = Campaign.Current != null ? ScreenManager.TopScreen as MapScreen : null;
            bool onMap = ms != null;
            if (onMap && !_campLine) CampaignLine(s);
            if (!Alive())
            {
                if (_w != null && _moved) { try { _w.MarginLeft = _origLeft; _w.MarginBottom = _origBottom; } catch { } }   // odpadly widzet - marginesy prefabu
                _w = null; _moved = false;
                if (!onMap) return;
                if (_findWait > 0) { _findWait--; return; }
                if (!Find(s)) { _findWait = 20; return; }
            }
            bool want = s != null && s.MapChatLogClearOfMenu && onMap && MenuClean(ms);
            if (want != _moved) Move(want);
            Frame(s, onMap);
            if (onMap && _testOn) TestStep(ms, s);
        }

        /// <summary>Czysty stan menu: menu osady albo spotkania otwarte, nic nad nim (ucieczka, zarzadzanie, werbunek, okienka gry, rozmowa, pytanie).</summary>
        private static bool MenuClean(MapScreen ms)
        {
            if (!ms.IsInMenu || ms.IsEscapeMenuOpened || ms.IsInTownManagement || ms.IsInRecruitment || ms.IsInArmyManagement || ms.IsInHideoutTroopManage
                || ms.IsMarriageOfferPopupActive || ms.IsHeirSelectionPopupActive || ms.IsMapIncidentActive || ms.IsInCampaignOptions || ms.IsInBattleSimulation) return false;
            var cm = Campaign.Current != null ? Campaign.Current.ConversationManager : null;
            if (cm != null && cm.IsConversationInProgress) return false;
            return !InformationManager.IsAnyInquiryActive();
        }

        private static void CampaignLine(Settings s)
        {
            _campLine = true;
            _campAt = _clock;
            try { if (!_viewOk || !_vmOk) Resolve(); } catch (Exception e) { Stumble("Resolve", e); }
            _testOn = TestWanted();
            Log.Info(P + "kampania - klucze MapChatLogClearOfMenu=" + YN(s != null && s.MapChatLogClearOfMenu) + ", MapChatLogFrame=" + YN(s != null && s.MapChatLogFrame)
                     + ", MapHideTradeNotices=" + YN(s != null && s.MapHideTradeNotices) + " (czytane na zywo, co 0,25 s), widok dziennika " + (_viewOk ? "TAK" : "BRAK")
                     + ", pola MPChatVM " + (_vmOk ? "TAK" : "BRAK") + ", latka tabliczek " + (_patchOk ? "TAK" : "BRAK") + ", test autotestu " + YN(_testOn) + ".");
        }

        private static string YN(bool b) { return b ? "TAK" : "NIE"; }

        // ------------------------------------------------------------ widzet
        /// <summary>Zapamietany widzet wisi jeszcze pod korzeniem warstwy dziennika (film przeladowuje sie w menu glownym - wtedy szukamy od nowa).</summary>
        private static bool Alive()
        {
            if (_w == null || _root == null) return false;
            Widget p = _w;
            for (int i = 0; i < 32 && p != null; i++)
            {
                if (ReferenceEquals(p, _root)) return true;
                p = p.ParentWidget;
            }
            return false;
        }

        private static GauntletLayer ChatLayer()
        {
            if (!_viewOk) return null;
            var gl = _pCurrent.GetValue(null, null) as GlobalLayer;
            return gl != null ? gl.Layer as GauntletLayer : null;
        }

        private static bool Find(Settings s)
        {
            GauntletLayer layer = ChatLayer();
            UIContext ctx = layer != null ? layer.UIContext : null;
            Widget root = ctx != null ? ctx.Root : null;
            if (root == null) return false;
            Widget w = root.FindChild("ChatLogWidget", true);
            if (w == null) return false;
            _layer = layer; _root = root; _w = w;
            _inner = w.FindChild("InnerPanel", true);
            _text = w.FindChild("ScrollablePanelContainer", true);
            _origLeft = w.MarginLeft; _origBottom = w.MarginBottom;   // nowy film - wartosci prefabu
            _moved = false;
            _frame = null; _frameTried = false; _frameWasVis = false; _frameH = 0f; _lastK = 0;
            _finds++;
            string fr;
            if (s != null && s.MapChatLogFrame) fr = BuildFrame();
            else fr = "NIE (klucz MapChatLogFrame wylaczony - ramka powstanie przy wlaczeniu)";
            float sc = ctx.CustomScale;
            Log.Info(P + "widget dziennika znaleziony (" + _finds + ". raz; ChatLogWidget, marginesy " + F0(_origLeft) + "/" + F0(_origBottom) + ", rozmiar "
                     + F0(w.SuggestedWidth) + "x" + F0(w.SuggestedHeight) + ", skala " + sc.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                     + ", InnerPanel " + (_inner != null ? "TAK" : "BRAK") + ", pole tekstu " + (_text != null ? "TAK" : "BRAK") + "), ramka dodana " + fr + ".");
            return true;
        }

        private static string F0(float v) { return ((int)Math.Round(v)).ToString(System.Globalization.CultureInfo.InvariantCulture); }

        /// <summary>Ramka w stylu dymka ROT: kontener (StretchToParent w poziomie, wysokosc Fixed, przy dole), tlo i dwie belki; dziecko 0 ChatLogWidget - pod tekstem.
        /// Bez zdarzen myszy. Zwraca "TAK" albo "NIE (powod)".</summary>
        private static string BuildFrame()
        {
            _frameTried = true;
            if (_inner == null) return "NIE (brak InnerPanel - nie ma czego mierzyc)";
            if (!_vmOk) return "NIE (brak pol MPChatVM)";
            UIContext ctx = _w.Context;
            SpriteData sd = ctx != null ? ctx.SpriteData : null;
            Sprite white = sd != null ? sd.GetSprite("BlankWhiteSquare_9") : null;
            Sprite bar = sd != null ? sd.GetSprite("General\\TooltipHint\\tooltip_frame") : null;
            if (white == null) return "NIE (brak sprite'u BlankWhiteSquare_9)";
            var f = new Widget(ctx)
            {
                Id = "ArmChatLogFrame",
                WidthSizePolicy = SizePolicy.StretchToParent,
                HeightSizePolicy = SizePolicy.Fixed,
                SuggestedHeight = 2f * FramePad,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                MarginLeft = 8f,
                MarginRight = 14f,
                MarginBottom = FrameBottom,
                DoNotAcceptEvents = true,
                DoNotPassEventsToChildren = true,
                IsVisible = false
            };
            var bg = new Widget(ctx)
            {
                WidthSizePolicy = SizePolicy.StretchToParent,
                HeightSizePolicy = SizePolicy.StretchToParent,
                MarginTop = 6f,
                MarginBottom = 6f,
                Sprite = white,
                Color = Color.ConvertStringToColor("#000000CC"),
                DoNotAcceptEvents = true
            };
            f.AddChild(bg);
            if (bar != null)
            {
                f.AddChild(Bar(ctx, bar, VerticalAlignment.Top));
                f.AddChild(Bar(ctx, bar, VerticalAlignment.Bottom));
            }
            _w.AddChildAtIndex(f, 0);
            _frame = f;
            return bar != null ? "TAK" : "TAK (bez belek - brak sprite'u General\\TooltipHint\\tooltip_frame)";
        }

        private static Widget Bar(UIContext ctx, Sprite bar, VerticalAlignment va)
        {
            return new Widget(ctx)
            {
                WidthSizePolicy = SizePolicy.StretchToParent,
                HeightSizePolicy = SizePolicy.Fixed,
                SuggestedHeight = 12f,
                VerticalAlignment = va,
                Sprite = bar,
                ExtendLeft = 4f,
                ExtendRight = 4f,
                SaturationFactor = -100f,
                ColorFactor = 0.5f,
                DoNotAcceptEvents = true
            };
        }

        // ------------------------------------------------------------ P1: przesuniecie
        private static void Move(bool want)
        {
            float l0 = _w.MarginLeft, b0 = _w.MarginBottom;
            float l = want ? MovedLeft : _origLeft, b = want ? MovedBottom : _origBottom;
            _w.MarginLeft = l;
            _w.MarginBottom = b;
            _moved = want;
            if (want)
            {
                MapChatLogStats.DMoves++;
                _movedAt = _clock;
                if (++_movesLogged <= 3) Log.Info(P + "przesuniety obok menu (MarginLeft " + F0(l0) + " -> " + F0(l) + ", MarginBottom " + F0(b0) + " -> " + F0(b) + ").");
            }
            else
            {
                MapChatLogStats.DBacks++;
                if (++_backsLogged <= 3) Log.Info(P + "wrocil na miejsce gry (MarginLeft " + F0(l0) + " -> " + F0(l) + ", MarginBottom " + F0(b0) + " -> " + F0(b) + ").");
            }
        }

        // ------------------------------------------------------------ P2: ramka
        private static void Frame(Settings s, bool onMap)
        {
            bool key = s != null && s.MapChatLogFrame;
            if (_frame == null)
            {
                if (!key || !onMap || _frameTried) return;
                string fr = BuildFrame();
                Log.Info(P + "ramka dodana pozniej (klucz MapChatLogFrame wlaczony w sesji) " + fr + ".");
                if (_frame == null) return;
            }
            bool vis = false;
            float h = _frameH;
            int k = 0;
            if (key && onMap)
            {
                IList hist;
                bool busy;
                k = VisibleLines(out hist, out busy);
                if (!busy)
                {
                    int n = hist != null ? hist.Count : 0;
                    if (k > 0)
                    {
                        int cc = _inner.ChildCount;
                        if (cc == n)
                        {
                            float sum = 0f;
                            for (int i = cc - k; i < cc; i++) { Widget c = _inner.GetChild(i); sum += c.Bottom - c.Top; }   // piksele (Left/Top/Bottom - uklad wzgledem rodzica)
                            float sc = _w.Context.CustomScale;
                            if (sc <= 0f) sc = 1f;
                            h = sum / sc + 2f * FramePad;
                            float max = _w.SuggestedHeight - TextHeightCut;
                            if (h > max) h = max;
                            vis = sum > 0.5f;
                        }
                        else { vis = _frame.IsVisible; k = _lastK; }   // lista w trakcie przebudowy - stan z poprzedniego taktu
                    }
                }
            }
            _lastK = k;
            if (vis && Math.Abs(h - _frameH) > 0.5f) { _frame.SuggestedHeight = h; _frameH = h; }
            if (vis != _frame.IsVisible) _frame.IsVisible = vis;
            if (vis && !_frameWasVis) MapChatLogStats.DFrames++;
            _frameWasVis = vis;
        }

        /// <summary>Linie widoczne od konca MessageHistory (Alpha > 0.01; starsze gasna wczesniej). busy = gracz przeglada albo pisze (gra pokazuje wtedy
        /// wszystkie linie na wlasnym tle) albo brak VM.</summary>
        private static int VisibleLines(out IList hist, out bool busy)
        {
            hist = null;
            busy = true;
            object view = _pCurrent.GetValue(null, null);
            object vm = view != null ? _fData.GetValue(view) : null;
            if (vm == null || (bool)_pInsp.GetValue(vm, null) || (bool)_pTyp.GetValue(vm, null)) return 0;
            busy = false;
            hist = _pHist.GetValue(vm, null) as IList;
            int n = hist != null ? hist.Count : 0, k = 0;
            for (int i = n - 1; i >= 0; i--)
            {
                object line = hist[i];
                if (line != null && (float)_pAlpha.GetValue(line, null) > 0.01f) k++;
                else break;
            }
            return k;
        }

        // ============================================================ test autotestu
        internal static bool TestWanted()
        {
            int m = LogGate.Mode;
            if (m != 1 && m != 2) return false;
            try { return File.Exists(Path.Combine(CrashScribeDir(), AutoFlagName)); }
            catch { return false; }
        }

        private static string CrashScribeDir()
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(Path.Combine(docs, "Mount and Blade II Bannerlord"), "CrashScribe");
        }

        /// <summary>(a) raz na mapie bez menu (najwczesniej 10 s po wejsciu na mape), (b) w czystym stanie menu najwczesniej 20 s po przesunieciu, najwyzej 2 razy,
        /// co najmniej co 120 s. Proba: 3 linie, po 1,5 s pomiar i zdjecie silnika, po 3 s sprawdzenie pliku i linia "test - ...".</summary>
        private static void TestStep(MapScreen ms, Settings s)
        {
            if (_probeStage == 1)
            {
                if (_clock < _probeAt) return;
                if (_probeKind == "mapa" && ms.IsInMenu) { Log.Info(P + "test - bez proby (mapa: menu otworzylo sie przed pomiarem)."); _probeStage = 0; return; }
                if (_probeKind == "menu" && !_moved) { Log.Info(P + "test - bez proby (menu: menu zamkniete albo okno nad menu przed pomiarem)."); _probeStage = 0; return; }
                Measure(ms, s);
                _probeStage = 2;
                _probeAt = _clock + 3f;
                return;
            }
            if (_probeStage == 2)
            {
                if (_clock < _probeAt) return;
                bool file = false;
                try { file = _probePath != null && File.Exists(_probePath); } catch { }
                if (!file) _probeFails.Add("brak pliku zdjecia");
                Log.Info(P + _probeLine + " - " + (_probeFails.Count == 0 ? "TAK." : "NIE (" + string.Join("; ", _probeFails.ToArray()) + ")."));
                _probeStage = 0;
                return;
            }
            if (!_mapTestDone && !ms.IsInMenu && _clock - _campAt >= 10f && !ms.IsEscapeMenuOpened && !InformationManager.IsAnyInquiryActive())
            {
                var cm = Campaign.Current != null ? Campaign.Current.ConversationManager : null;
                if (cm == null || !cm.IsConversationInProgress) { _mapTestDone = true; Post("mapa"); return; }
            }
            if (_menuTests < 2 && _moved && _clock - _movedAt >= 20f && _clock - _lastMenuTestAt >= 120f)
            {
                _menuTests++;
                _lastMenuTestAt = _clock;
                Post("menu");
            }
        }

        private static void Post(string kind)
        {
            _testN++;
            InformationManager.DisplayMessage(new InformationMessage(TestTag + " 1/3: A raven from Tyrosh, 3 days on the wing: the Sealord's fleet sails for the Stepstones, and every captain in the Free Cities talks of nothing else."));
            InformationManager.DisplayMessage(new InformationMessage(TestTag + " 2/3: Ser Jorah Mormont has been taken prisoner."));
            InformationManager.DisplayMessage(new InformationMessage(TestTag + " 3/3: The harvest in the Reach is the best in ten years."));
            Log.Info(P + "test - start proby nr " + _testN + " (" + kind + "): 3 linie wyslane, pomiar i zdjecie za 1,5 s.");
            _probeStage = 1;
            _probeKind = kind;
            _probeAt = _clock + 1.5f;
        }

        private struct RectU { public float L, T, R, B; }

        /// <summary>Prostokat widzetu w jednostkach UI: suma Left/Top po rodzicach (to samo co GlobalPosition po ukladzie - bez typu Vector2, ktorego zestawu
        /// Armoury nie ma w referencjach), szerokosc Right - Left, wysokosc Bottom - Top.</summary>
        private static RectU U(Widget w, float sc)
        {
            float x = 0f, y = 0f;
            int n = 0;
            for (Widget p = w; p != null && n < 64; p = p.ParentWidget, n++) { x += p.Left; y += p.Top; }
            float wd = w.Right - w.Left, ht = w.Bottom - w.Top;
            return new RectU { L = x / sc, T = y / sc, R = (x + wd) / sc, B = (y + ht) / sc };
        }

        private static string XY(RectU r) { return "x " + F0(r.L) + ".." + F0(r.R) + " y " + F0(r.T) + ".." + F0(r.B); }

        private static void Measure(MapScreen ms, Settings s)
        {
            _probeFails.Clear();
            _probePath = null;
            float sc = _w.Context.CustomScale;
            if (sc <= 0f) sc = 1f;
            RectU log = U(_w, sc);
            RectU text = _text != null ? U(_text, sc) : log;
            bool frameVis = _frame != null && _frame.IsVisible;
            RectU fr = frameVis ? U(_frame, sc) : text;
            int tests = 0, vis = 0;
            try
            {
                IList hist = null;
                bool busy;
                if (_vmOk) vis = VisibleLines(out hist, out busy);
                if (hist != null && _pLine != null)
                    for (int i = Math.Max(0, hist.Count - vis); i < hist.Count; i++)
                    {
                        var t = _pLine.GetValue(hist[i], null) as string;
                        if (t != null && t.IndexOf(TestTag, StringComparison.Ordinal) >= 0) tests++;
                    }
            }
            catch (Exception e) { Stumble("Measure.Lines", e); }
            bool hasMenu = false;
            RectU menu = default(RectU);
            try
            {
                GauntletLayer ml = ms.FindLayer<GauntletLayer>("MapMenuView");
                Widget gm = ml != null && ml.UIContext != null && ml.UIContext.Root != null ? ml.UIContext.Root.FindChild("GameMenuParent", true) : null;
                if (gm != null)
                {
                    float msc = ml.UIContext.CustomScale;
                    menu = U(gm, msc > 0f ? msc : 1f);
                    menu.R += MenuFrame;
                    hasMenu = true;
                }
            }
            catch (Exception e) { Stumble("Measure.Menu", e); }
            RectU content = frameVis ? fr : text;
            int over = 0;
            if (hasMenu && _probeKind == "menu")
            {
                float ov = Math.Min(content.R, menu.R) - Math.Max(content.L, menu.L);
                float oh = Math.Min(content.B, menu.B) - Math.Max(content.T, menu.T);
                if (ov > 0f && oh > 0f) over = (int)Math.Ceiling(ov);
            }
            int bottom = frameVis ? (int)Math.Round(fr.B) : -1;
            bool key = s != null && s.MapChatLogFrame;
            if (_probeKind == "menu")
            {
                if (!hasMenu) _probeFails.Add("menu nie znalezione (GameMenuParent)");
                if (over > 0) _probeFails.Add("zachodzenie " + over + " px");
                if (frameVis && bottom > PausedTop) _probeFails.Add("dol ramki ponizej granicy PAUSED");
            }
            else if (frameVis && bottom >= BarTop) _probeFails.Add("dol ramki na pasku mapy");
            if (key && !frameVis) _probeFails.Add("ramka niewidoczna");
            if (tests < 3) _probeFails.Add("linii testowych widac " + tests + "/3");
            string shot = "zdjecie ";
            try
            {
                string dir = Path.Combine(CrashScribeDir(), "zdjecia");
                Directory.CreateDirectory(dir);
                string name = "dziennik-m105-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + _testN + ".png";
                _probePath = Path.Combine(dir, name);
                TaleWorlds.Engine.Utilities.TakeScreenshot(_probePath.Replace('\\', '/'));
                shot += "zdjecia\\" + name;
            }
            catch (Exception e) { shot += "BLAD (" + e.GetType().Name + ")"; _probeFails.Add("zdjecie: wyjatek"); Stumble("Measure.Shot", e); }
            _probeLine = "test - " + _probeKind + ": linie testowe " + tests + "/3 (widocznych " + vis + "), dziennik " + XY(log) + ", tekst " + XY(text)
                         + ", menu " + (hasMenu ? XY(menu) + " (z ramka " + F0(MenuFrame) + ")" : "brak") + ", zachodzenie " + over + " px, ramka " + (frameVis ? "TAK (" + XY(fr) + ", linii " + _lastK + ")" : "NIE (klucz " + YN(key) + ")")
                         + ", dol ramki y=" + bottom + " (granica PAUSED " + F0(PausedTop) + ", pasek " + F0(BarTop) + "), " + shot;
        }

        // ============================================================ doba i kampania
        /// <summary>ArmouryBehavior.OnDailyTickBodyInner: linia doby (liczby za dobe, potkniecia od startu gry) i zerowanie licznikow doby.</summary>
        internal static void Daily(int day)
        {
            var s = Settings.Current;
            Log.Info(P + MapChatLogStats.DayText(day, s != null && s.MapHideTradeNotices));
            MapChatLogStats.DayReset();
        }

        /// <summary>Wyjscie z kampanii (SubModuleMain.OnGameEnd): linia kampanii i test od nowa; widzet zostaje (takt poza kampania oddaje miejsce gry i chowa ramke).</summary>
        internal static void Reset()
        {
            _campLine = false;
            _testOn = false; _mapTestDone = false; _menuTests = 0; _probeStage = 0; _lastMenuTestAt = -1e9f;
        }
    }

    /// <summary>Liczniki dziennika bez typow SandBox - linia doby dziala takze, gdy typ MapChatLog sie nie zaladowal.</summary>
    internal static class MapChatLogStats
    {
        internal static int DMoves, DBacks, DFrames, DHidCar, DHidVil, DPassed, St;

        internal static string DayText(int day, bool filter)
        {
            return "dzien " + day + " - przesuniecia " + DMoves + ", powroty " + DBacks + ", ramka " + DFrames + ", handel na tabliczkach: filtr " + (filter ? "TAK" : "NIE")
                   + ", ukryte " + (DHidCar + DHidVil) + " (karawany " + DHidCar + ", wiesniacy " + DHidVil + "), przepuszczone " + DPassed + ", potkniecia " + St + ".";
        }

        internal static void DayReset()
        {
            DMoves = DBacks = DFrames = DHidCar = DHidVil = DPassed = 0;
        }
    }
}
