using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;

namespace Armoury
{
    /// <summary>
    /// m103-sceny (E4): OGOLNE OKNO OPOWIESCI w wygladzie wydarzen ROT - prolog 298 teraz, Kronika Westeros (cala ksiazka, potem serial) pozniej: tytul, dwa akapity,
    /// obrazek (sprite ROT albo z pliku - StoryPictures), jeden albo dwa przyciski, akcja po zamknieciu.
    /// DROGA (rozpoznanie m103 "okno" + krytyk, dekompilacja ROT 8.1.8): ArmStoryEvent dziedziczy po ROT.Events.EventBase (public abstract, wlasciwosci okna public virtual
    /// z setterem protected, protected OpenPopup = PushState(CreateState&lt;EventPopupState&gt;(this))) - okno to zwykly EventPopupState ROT z ekranem EventScreen (prefab
    /// EventXml z ROT-Content), wiec autotest (AutotestUi: stan ROT.CampaignBehaviors.EventPopupState, pole EventPanel, ExecuteDone po 1.5 s, linia "OKNO wydarzenie
    /// ROT") i lektor (latka na EventScreen) obejmuja je bez zmian.
    /// ZASADY: obiekt NIGDY w ROTEventBehavior._events ani w zapisywanym polu (podklasy nie ma w definicjach typow zapisu - zapis by sie wywrocil), NIGDY StartEvent;
    /// tytul nigdy "Stannis Sails for King's Landing" / "Stannis Claims the Iron Throne" (EventScreen przelacza na prefaby Blackwater). Esc w EventScreen.OnFrameTick
    /// ZAWSZE wola ExecuteCancel (bez sprawdzania IsCancelEnabled) - przy jednym przycisku HandleCancelButton == HandleDoneButton; zamkniecie idempotentne.
    /// EventPanel.ExecuteDone/Cancel: najpierw PopState, potem Handle* - akcja po zamknieciu biegnie juz nad mapa (bez pchania okien z wnetrza Handle*).
    /// Typ ROT uzywany TYLKO w metodach [MethodImpl(NoInlining)] (blad ladowania typu po aktualizacji ROT zostaje w try wolajacego). Zapas: brak ROT, brak kategorii
    /// sprite'ow ui_event_category (konstruktor EventScreen rzuca KeyNotFound) albo wyjatek przy otwarciu - InformationManager.ShowInquiry z tym samym tekstem i pauza
    /// (opowiesc idzie dalej). Jedno okno naraz. Bledy: licznik potkniec, bez globalnego wylacznika.
    /// </summary>
    internal static class StoryWindow
    {
        internal const string Tag = "Okno opowiesci (m103): ";
        internal const string RotTitleBlackwater1 = "Stannis Sails for King's Landing";
        internal const string RotTitleBlackwater2 = "Stannis Claims the Iron Throne";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Tresc i zachowanie okna (bez typow ROT).</summary>
        internal sealed class Spec
        {
            public string Key = "";          // do logu (np. prolog_05)
            public string Title = "", Upper = "", Lower = "";
            public string DoneText = "";     // przycisk glowny ("Continue...")
            public string CancelText;        // null/"" = jeden przycisk (Esc = przycisk glowny)
            public string Header = "";       // nazwa sprite'a obrazka (arm_* z pliku albo sprite ROT z ui_event_category)
            public string Background = "event-bg";
            public Action<Spec, string> Closed;   // (okno, jak: "przycisk" / "anuluj" / "bez przycisku")
            public int Tag;                  // dane wolajacego (np. numer sceny)
            public bool Cancelled;           // zamkniete drugim przyciskiem (tylko przy dwoch przyciskach)
            internal bool Done;              // zamkniete - drugie zamkniecie nic nie robi
            internal string Mode = "";       // "ROT" / "zapas"
        }

        private static Spec _open;
        private static object _state;   // EventPopupState okna (typ ROT - tylko jako object)
        private static long _openedAt;
        private static int _stumbles, _shownRot, _shownInquiry;
        private static int _rotKnown;   // 0 - nie sprawdzone, 1 - ROT jest, -1 - brak ROT (stale dla procesu)

        internal static bool IsOpen { get { return _open != null; } }
        internal static Spec Open { get { return _open; } }
        internal static int Stumbles { get { return _stumbles; } }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 5) Log.Error("StoryWindow." + where + " (potkniecie " + _stumbles + ")", e);
        }

        /// <summary>Pokazuje okno. Zwraca "ROT" (okno wydarzenia ROT), "zapas (powod)" (InformationManager.ShowInquiry) albo null (nie pokazane - drugie okno
        /// otwarte, wyjatek takze w zapasie). Watek glowny, poza tickiem kampanii (straznik w OnApplicationTick).</summary>
        internal static string Show(Spec s)
        {
            if (s == null || _open != null) return null;
            if (s.Title == RotTitleBlackwater1 || s.Title == RotTitleBlackwater2) s.Title = s.Title + ".";   // EventScreen przelaczylby na prefab Blackwater
            string why = RotUnavailable();
            if (why == null)
            {
                try
                {
                    _open = s; s.Mode = "ROT"; _openedAt = Stopwatch.GetTimestamp();
                    object st = ShowRot(s);
                    if (st != null) { _state = st; _shownRot++; return "ROT"; }
                    why = "okno ROT nie weszlo na stos stanow";
                    _open = null;
                }
                catch (Exception e)
                {
                    Stumble("Show.Rot " + s.Key, e);
                    why = "wyjatek " + e.GetType().Name;
                    Recover(s);
                }
            }
            try
            {
                _open = s; s.Mode = "zapas"; _state = null; _openedAt = Stopwatch.GetTimestamp();
                ShowInquiry(s);
                _shownInquiry++;
                return "zapas ShowInquiry (" + why + ")";
            }
            catch (Exception e)
            {
                Stumble("Show.Inquiry " + s.Key, e);
                _open = null;
                return null;
            }
        }

        /// <summary>null = okno ROT mozliwe; inaczej powod zapasu.</summary>
        private static string RotUnavailable()
        {
            try
            {
                // AccessTools.TypeByName przeszukuje wszystkie zestawy (dziesiatki modow) - raz na proces
                if (_rotKnown == 0) _rotKnown = AccessTools.TypeByName("ROT.Events.EventBase") != null ? 1 : -1;
                if (_rotKnown < 0) return "brak ROT (ROT.Events.EventBase)";
                var data = UIResourceManager.SpriteData;
                if (data == null || data.SpriteCategories == null || !data.SpriteCategories.ContainsKey("ui_event_category")) return "brak kategorii sprite'ow ui_event_category (ROT-Content)";
                return null;
            }
            catch (Exception e) { Stumble("RotUnavailable", e); return "blad sprawdzenia ROT"; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object ShowRot(Spec s)
        {
            var ev = new ArmStoryEvent(s);
            ev.Show();
            var gsm = GameStateManager.Current;
            if (gsm == null) return null;
            object found = null;
            foreach (var g in gsm.GameStates)
                if (g is ROT.CampaignBehaviors.EventPopupState && ReferenceEquals(EventOf(g), ev)) found = g;
            // EventPopupState.Event nieczytelne (zmiana ROT) - okno ROT na wierzchu jest nasze (straznik pcha tylko, gdy zadnego okna wydarzenia nie bylo na stosie);
            // bez tego zapas pokazalby drugie okno obok naszego
            if (found == null && _pEvent == null && gsm.ActiveState is ROT.CampaignBehaviors.EventPopupState) found = gsm.ActiveState;
            return found;
        }

        private static System.Reflection.PropertyInfo _pEvent;

        /// <summary>EventPopupState.Event (internal w ROT) - odbiciem.</summary>
        private static object EventOf(object st)
        {
            if (st == null) return null;
            if (_pEvent == null || _pEvent.DeclaringType != st.GetType()) _pEvent = AccessTools.Property(st.GetType(), "Event");
            return _pEvent != null ? _pEvent.GetValue(st, null) : null;
        }

        /// <summary>Wyjatek w trakcie pchniecia: okno na wierzchu stosu bez ekranu (EventScreen.OnActivate rzucil) - zdejmujemy je, zeby gra nie stanela.</summary>
        private static void Recover(Spec s)
        {
            try
            {
                var gsm = GameStateManager.Current;
                var st = gsm != null ? gsm.ActiveState : null;
                if (st != null && st.GetType().FullName == "ROT.CampaignBehaviors.EventPopupState" && IsOurs(st))
                {
                    s.Done = true;   // PopState nie wola Handle* - zamkniecie zalatwiamy tu
                    gsm.PopState(0);
                    Log.Info(Tag + "okno " + s.Key + " zdjete po wyjatku przy otwarciu - pokazuje zapas.");
                    s.Done = false;
                }
            }
            catch (Exception e) { Stumble("Recover " + s.Key, e); }
            _open = null; _state = null;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsOurs(object st)
        {
            return EventOf(st) is ArmStoryEvent;
        }

        private static void ShowInquiry(Spec s)
        {
            bool two = !string.IsNullOrEmpty(s.CancelText);
            var sp = s;
            InformationManager.ShowInquiry(new InquiryData(s.Title, s.Upper + "\n\n" + s.Lower, true, two, s.DoneText, two ? s.CancelText : null,
                () => OnClosed(sp, "przycisk"), two ? (Action)(() => OnClosed(sp, "anuluj")) : null), true, false);
        }

        /// <summary>Zamkniecie okna (przycisk, Esc, zapas, straznik). Idempotentne: drugie zamkniecie tego samego okna nic nie robi.</summary>
        internal static void OnClosed(Spec s, string how)
        {
            if (s == null || s.Done) return;
            s.Done = true;
            s.Cancelled = how == "anuluj";
            if (ReferenceEquals(_open, s)) { _open = null; _state = null; }
            try { var f = s.Closed; if (f != null) f(s, how); }
            catch (Exception e) { Stumble("Closed " + s.Key, e); }
        }

        /// <summary>Straznik (OnApplicationTick, 4 razy na sekunde): okno zamkniete bez przycisku (cudzy PopState / CleanStates, zapas schowany) - zamkniecie.</summary>
        internal static void Watch()
        {
            var s = _open;
            if (s == null) return;
            try
            {
                double sec = (Stopwatch.GetTimestamp() - _openedAt) / (double)Stopwatch.Frequency;
                if (s.Mode == "ROT")
                {
                    if (!OnStack(_state)) OnClosed(s, "bez przycisku - okna nie ma na stosie stanow");
                }
                else if (sec > 3.0 && !InformationManager.IsAnyInquiryActive()) OnClosed(s, "bez przycisku - okno zapasu zniknelo");
            }
            catch (Exception e) { Stumble("Watch", e); }
        }

        private static bool OnStack(object st)
        {
            var gsm = GameStateManager.Current;
            if (st == null || gsm == null) return false;
            foreach (var g in gsm.GameStates) if (ReferenceEquals(g, st)) return true;
            return false;
        }

        internal static double OpenSeconds { get { return _open == null ? 0 : (Stopwatch.GetTimestamp() - _openedAt) / (double)Stopwatch.Frequency; } }

        /// <summary>Koniec kampanii (OnGameEnd): bez okna - stan zerowany (bez akcji po zamknieciu).</summary>
        internal static void Reset()
        {
            if (_open != null) _open.Done = true;
            _open = null; _state = null;
        }

        internal static string StatText()
        {
            return "okna ROT " + _shownRot.ToString(Inv) + ", zapas " + _shownInquiry.ToString(Inv) + ", potkniecia okna " + _stumbles.ToString(Inv);
        }
    }

    /// <summary>m103-sceny: okno opowiesci jako wydarzenie ROT (podklasa EventBase). Tworzone i otwierane TYLKO z metod NoInlining w StoryWindow. Nie jest
    /// zapisywane (nigdy w ROTEventBehavior._events), nie startuje (StartEvent - nie wolamy), ConditionsMet/ConditionsHold = false.</summary>
    internal sealed class ArmStoryEvent : ROT.Events.EventBase
    {
        private readonly StoryWindow.Spec _spec;
        private readonly bool _two;

        internal ArmStoryEvent(StoryWindow.Spec s)
        {
            _spec = s;
            _two = !string.IsNullOrEmpty(s.CancelText);
            Title = s.Title ?? "";
            UpperText = s.Upper ?? "";
            LowerText = s.Lower ?? "";
            DoneButtonEnabled = true;
            DoneButtonText = s.DoneText ?? "";
            CancelButtonEnabled = _two;
            CancelButtonText = _two ? s.CancelText : "";
            BackgroundImage = string.IsNullOrEmpty(s.Background) ? "event-bg" : s.Background;
            HeaderImage = s.Header ?? "";
        }

        internal void Show() { OpenPopup(); }

        /// <summary>m105-kronika K0 (A2): klucz okna dla lektora - Spec.Key (Kronika: kr_NNN_w[war] = kolumna 1 arm_lektor; sceny prologu: prolog_NN). Publiczny - lektor
        /// moze go czytac odbiciem po nazwie przy oknie z EventScreen (P9: najpierw klucz, potem tytul).</summary>
        public string Key { get { return _spec.Key ?? ""; } }

        public override void Initialize() { }

        public override bool ConditionsMet() { return false; }

        public override bool ConditionsHold() { return false; }

        public override void HandleDoneButton() { StoryWindow.OnClosed(_spec, "przycisk"); }

        // Esc w EventScreen zawsze tu (takze przy jednym przycisku) - przy jednym przycisku to samo co przycisk glowny
        public override void HandleCancelButton() { StoryWindow.OnClosed(_spec, _two ? "anuluj" : "przycisk (Esc)"); }
    }
}
