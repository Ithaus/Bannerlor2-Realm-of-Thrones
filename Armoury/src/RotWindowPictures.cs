using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// m103-sceny (E4, punkt E zadania): OBRAZKI JEFFA W OKNACH WYDARZEN ROT. Prefiks na ROT.Screens.EventScreen.IGameStateListener.OnActivate (PRZED new EventPanel
    /// i LoadMovie - wiazanie @HeaderImage czyta wlasciwosc wydarzenia przy ladowaniu filmu): jesli w Modules\Armoury\Images\ROT jest plik rot_&lt;KlasaROT&gt;_&lt;n&gt;.png|jpg
    /// dla tego okna, HeaderImage wydarzenia (EventBase, setter protected - odbiciem) dostaje sprite z pliku (StoryPictures - walidacja bajtow, sprawdzanie pliku przy
    /// kazdym pokazaniu). Postfiks na OnDeactivate (warstwa juz zdjeta): oryginalny HeaderImage wraca, obrazek do kolejki zwolnienia. Bez pliku - nic sie nie zmienia
    /// (raz na sesje linia z nazwa pliku, ktorego okno szuka).
    /// ROZPOZNANIE OKNA: klasa wydarzenia (GetType().Name) i tytul - tabela Windows (35 okien ROT 8.1.8 z tools/lektor/teksty.json: klasa, n, id tekstu, tytul EN);
    /// klasa z jednym oknem - po klasie, klasa z kilkoma oknami - po tytule (EN albo przetlumaczony TextObject z tym samym id). Nasze okna (ArmStoryEvent, zestaw
    /// Armoury) pomijane - obrazek wybiera StoryWindow przy pokazaniu. Lektor (galaz m103-lektor) ma osobna latke na te same metody - niezalezne.
    /// HeaderImage nie jest zapisywany (bez [SaveableProperty]), a i tak wraca przy zamknieciu. Klucz StoryImagesForRotWindows (MCM, czytany na zywo).
    /// BEZ OBRAZKA Z PLIKU (recenzja m103): okna o tytule "Stannis Sails for King's Landing" (BlackwaterSiegeNotificationEvent_0) i "Stannis Claims the Iron Throne"
    /// (BlackwaterEvent_1) - EventScreen.OnActivate laduje dla nich prefaby BlackwaterEventWarning / BlackwaterEventStannisWin z obrazkiem wpisanym na sztywno
    /// (Sprite="StannisSetsSail" / "StannisIronThrone", bez @HeaderImage). To samo porownanie tytulu co w ROT (EventPanel.EventTitle == Event.Title): przy takim tytule
    /// prefiks nic nie podmienia (raz na proces linia "prefab Blackwater"); przy tytule przetlumaczonym ROT bierze EventXml i obrazek z pliku dziala.
    /// Bledy: licznik potkniec, bez globalnego wylacznika; okno ROT otwiera sie zawsze (wyjatek prefiksu nie wychodzi).
    /// </summary>
    internal static class RotWindowPictures
    {
        private const string Tag = StoryPictures.Tag;

        // klasa wydarzenia ROT, numer okna w klasie, id tekstu tytulu, tytul EN (ROT 8.1.8; tools/lektor/teksty.json)
        private static readonly string[][] Windows =
        {
            new[] { "BlackwaterEvent", "0", "ROTd0hD1DmLa", "Stannis Retreats to Dragonstone" },
            new[] { "BlackwaterEvent", "1", "ROTd0hD1DmLaa", "Stannis Claims the Iron Throne" },          // prefab BlackwaterEventStannisWin - bez obrazka z pliku
            new[] { "AegonInvasionEvent", "0", "ROTEjHgHyLa", "Aegon Makes His Move" },
            new[] { "BlackwaterSiegeEvent", "0", "ROTd0hD1DmLa", "Battle of the Blackwater" },
            new[] { "BlackwaterSiegeNotificationEvent", "0", "ROTd0hD1DmL", "Stannis Sails for King's Landing" },   // prefab BlackwaterEventWarning - bez obrazka z pliku
            new[] { "BastardsSiegeNotificationEvent", "0", "bastard1", "To Winterfell" },
            new[] { "BastardsEvent", "0", "bastard4", "Ramsay Holds Winterfell" },
            new[] { "BastardsEvent", "1", "bastards7", "Stark Homecoming" },
            new[] { "BastardsSiegeEvent", "0", "bastards10", "Battle of the Bastards" },
            new[] { "HarrenhalEvent", "0", "harren7", "Roose Bolton Denied!" },
            new[] { "HarrenhalEvent", "1", "harren10", "A Northern Stronghold in the South" },
            new[] { "HarrenhalSiegeEvent", "0", "harren4", "Roose Bolton Besieges Harrenhal" },
            new[] { "HarrenhalSiegeNotificationEvent", "0", "harren1", "Roose Bolton Strikes a Deal" },
            new[] { "JonEvent", "0", "jonevent1", "King in the North" },
            new[] { "RamsayEvent", "0", "ramsayevent1", "A Bastard to Lord" },
            new[] { "WallEvent", "0", "wall333", "Wildlings Routed" },
            new[] { "WallEvent", "1", "wall338", "Freefolk Overwhelm" },
            new[] { "WallSiegeEvent", "0", "wall222", "Freefolk Attack The Wall" },
            new[] { "WallSiegeNotificationEvent", "0", "wall111", "Freefolk March South" },
            new[] { "KingsLandingEvent", "0", "ROTkuFgsFRE", "The Iron Throne is Yours" },
            new[] { "FistEvent", "0", "ROTRcQV5TQA", "The Undead Swarm the Fist of the First Men" },
            new[] { "NightKingDeathEvent", "0", "ROTq9VmC243", "The Army Of The Dead Is No More" },
            new[] { "JeorMutinyEvent", "0", "ROTnSOdZKwe", "Mutiny at the Crasters Keep" },
            new[] { "RiverlandsDeclareWarEvent", "0", "ROTRhKTB9hN", "Riverlands Join the Fight" },
            new[] { "DanyInvasionEvent", "0", "ROTOJFCuxVf", "Danaerys Targaryen Invades" },
            new[] { "JoffreyEvent", "0", "ROTHe3TFq5U", "The Purple Wedding" },
            new[] { "EastwatchEvent", "0", "ROTVFZM1Yqz", "Stannis to Eastwatch" },
            new[] { "NedEvent", "0", "ROT3I4POW6w", "Execution of Ned Stark" },
            new[] { "RickardKarstarkExecutionEvent", "0", "ROTRk8CXPzh", "Rickard Karstark Execution" },
            new[] { "RooseDeathEvent", "0", "ROTevKPxRyQ", "The Demise of Roose Bolton." },
            new[] { "RedWeddingEvent", "0", "ROTtVlfw5co", "A Joyous Occasion" },
            new[] { "RedWeddingEvent", "1", "redwedding1", "The Red Wedding" },
            new[] { "RenlyDeathEvent", "0", "ROTgnVsyL6t", "Influence of Melisandre" },
            new[] { "RenlyDeathEvent", "1", "renlydeath1", "A Shadow's Deed" },
            new[] { "StannisDeathEvent", "0", "ROToLSmMbN3", "The End of King Stannis Baratheon." },
        };

        private sealed class Swap
        {
            public object Event;
            public string Orig;
            public string Sprite;
        }

        private static FieldInfo _fState;                 // EventScreen._eventPopupState
        private static PropertyInfo _pEvent;              // EventPopupState.Event (internal)
        private static readonly Dictionary<object, Swap> _swapped = new Dictionary<object, Swap>();   // klucz: ekran EventScreen
        private static readonly HashSet<string> _noted = new HashSet<string>(StringComparer.Ordinal);
        private static Dictionary<string, List<int>> _byClass;
        private static string[] _localized;
        private static string _patch = "nie wpiete";
        private static int _stumbles, _swaps;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            if (_stumbles <= 5) Log.Error("RotWindowPictures." + where + " (potkniecie " + _stumbles + ")", e);
        }

        private static bool Key { get { var s = Settings.Current; return s != null && s.StoryImagesForRotWindows; } }

        /// <summary>Raz na proces (OnBeforeInitialModuleScreenSetAsRoot): prefiks OnActivate i postfiks OnDeactivate ekranu wydarzen ROT (jawne implementacje
        /// IGameStateListener - przez mape interfejsu). Bez ROT - linia BRAK, gra bez zmian.</summary>
        internal static void ApplyAll(Harmony h)
        {
            string a = "BRAK", d = "BRAK";
            try
            {
                var t = AccessTools.TypeByName("ROT.Screens.EventScreen");
                if (t == null) { _patch = "BRAK (ROT.Screens.EventScreen)"; Log.Info(Tag + "latka okien ROT: " + _patch + " - obrazki okien ROT nieczynne."); return; }
                _fState = AccessTools.Field(t, "_eventPopupState");
                MethodInfo act = null, deact = null;
                var map = t.GetInterfaceMap(typeof(IGameStateListener));
                for (int i = 0; i < map.InterfaceMethods.Length; i++)
                {
                    if (map.InterfaceMethods[i].Name == "OnActivate") act = map.TargetMethods[i];
                    else if (map.InterfaceMethods[i].Name == "OnDeactivate") deact = map.TargetMethods[i];
                }
                if (_fState != null && act != null && h.Patch(act, prefix: new HarmonyMethod(typeof(RotWindowPictures), nameof(ActivatePrefix))) != null) a = "TAK";
                if (_fState != null && deact != null && h.Patch(deact, postfix: new HarmonyMethod(typeof(RotWindowPictures), nameof(DeactivatePostfix))) != null) d = "TAK";
            }
            catch (Exception e) { Stumble("ApplyAll", e); }
            _patch = "OnActivate " + a + ", OnDeactivate " + d;
            Log.Info(Tag + "latka okien ROT: " + _patch + " | okien w tabeli " + Windows.Length + " (z obrazkiem z pliku " + (Windows.Length - 2) + ", prefab Blackwater bez obrazka 2)"
                     + " | klucz Story Images For Rot Windows " + (Key ? "TAK" : "NIE")
                     + " | pliki: Modules\\Armoury\\Images\\ROT\\rot_<Klasa>_<n>.png|jpg.");
        }

        /// <summary>Prefiks EventScreen.OnActivate: obrazek z pliku zamiast HeaderImage wydarzenia (gdy plik jest).</summary>
        public static void ActivatePrefix(object __instance)
        {
            try
            {
                if (__instance == null || _fState == null || !Key) return;
                var st = _fState.GetValue(__instance);
                if (st == null) return;
                if (_pEvent == null || _pEvent.DeclaringType != st.GetType()) _pEvent = AccessTools.Property(st.GetType(), "Event");
                var ev = _pEvent != null ? _pEvent.GetValue(st, null) : null;
                if (ev == null) return;
                var et = ev.GetType();
                if (et.Assembly == typeof(RotWindowPictures).Assembly) return;   // nasze okno opowiesci - obrazek wybrany przy pokazaniu
                string title = Get(ev, "Title");
                string key = KeyOf(et.Name, title);
                if (key == null)
                {
                    if (_noted.Add("?" + et.Name + "|" + title)) Log.Info(Tag + "okno ROT " + et.Name + " '" + title + "' poza tabela okien - bez obrazka z pliku.");
                    return;
                }
                string stem = "rot_" + key;
                string orig = Get(ev, "HeaderImage");
                if (title == StoryWindow.RotTitleBlackwater1 || title == StoryWindow.RotTitleBlackwater2)
                {
                    // EventScreen.OnActivate przelacza na prefab Blackwater z obrazkiem na sztywno - podmiana HeaderImage nic by nie dala (dekodowanie na marne)
                    if (_noted.Add("bw|" + key)) Log.Info(Tag + "okno ROT " + key + " '" + title + "' - prefab Blackwater ROT (obrazek wpisany w prefab, bez @HeaderImage)"
                                                          + " - obrazek z pliku niemozliwy, zostaje obrazek ROT.");
                    return;
                }
                if (StoryPictures.FindFile(StoryPictures.DirRot, stem) == null)
                {
                    if (_noted.Add(key)) Log.Info(Tag + "okno ROT " + key + " '" + title + "' - brak pliku Images\\ROT\\" + stem + ".png|jpg, obrazek ROT " + orig + ".");
                    return;
                }
                string how;
                string sprite = StoryPictures.Acquire(StoryPictures.DirRot, stem, out how);
                if (sprite == null)
                {
                    Log.Info(Tag + "okno ROT " + key + " '" + title + "' - obrazek z pliku nie wszedl (" + how + "), obrazek ROT " + orig + ".");
                    return;
                }
                if (!Set(ev, "HeaderImage", sprite)) { StoryPictures.Drop(sprite); return; }
                _swapped[__instance] = new Swap { Event = ev, Orig = orig, Sprite = sprite };
                _swaps++;
                Log.Info(Tag + "okno ROT " + key + " '" + title + "' - obrazek " + sprite + " (plik " + how + ") zamiast " + orig + ".");
            }
            catch (Exception e) { Stumble("ActivatePrefix", e); }
        }

        /// <summary>Postfiks EventScreen.OnDeactivate (warstwa zdjeta): oryginalny HeaderImage wraca, obrazek do kolejki zwolnienia (StoryPictures.AppTick).</summary>
        public static void DeactivatePostfix(object __instance)
        {
            try
            {
                Swap sw;
                if (__instance == null || !_swapped.TryGetValue(__instance, out sw)) return;
                _swapped.Remove(__instance);
                Set(sw.Event, "HeaderImage", sw.Orig);
                StoryPictures.Drop(sw.Sprite);
            }
            catch (Exception e) { Stumble("DeactivatePostfix", e); }
        }

        /// <summary>Klucz okna "Klasa_n" albo null (okno spoza tabeli).</summary>
        private static string KeyOf(string cls, string title)
        {
            if (_byClass == null)
            {
                var m = new Dictionary<string, List<int>>(StringComparer.Ordinal);
                for (int i = 0; i < Windows.Length; i++)
                {
                    List<int> l;
                    if (!m.TryGetValue(Windows[i][0], out l)) m[Windows[i][0]] = l = new List<int>();
                    l.Add(i);
                }
                _byClass = m;
            }
            List<int> rows;
            if (_byClass.TryGetValue(cls, out rows))
            {
                if (rows.Count == 1) return Windows[rows[0]][0] + "_" + Windows[rows[0]][1];
                foreach (int i in rows) if (TitleIs(i, title)) return Windows[i][0] + "_" + Windows[i][1];
                return null;
            }
            for (int i = 0; i < Windows.Length; i++) if (TitleIs(i, title)) return Windows[i][0] + "_" + Windows[i][1];
            return null;
        }

        private static bool TitleIs(int i, string title)
        {
            if (string.IsNullOrEmpty(title)) return false;
            if (title == Windows[i][3]) return true;
            try
            {
                if (_localized == null) _localized = new string[Windows.Length];
                if (_localized[i] == null) _localized[i] = new TextObject("{=" + Windows[i][2] + "}" + Windows[i][3]).ToString();
                return title == _localized[i];
            }
            catch { return false; }
        }

        private static string Get(object o, string prop)
        {
            var p = AccessTools.Property(o.GetType(), prop);
            return p != null ? p.GetValue(o, null) as string : null;
        }

        private static bool Set(object o, string prop, string value)
        {
            var p = AccessTools.Property(o.GetType(), prop);
            var set = p != null ? p.GetSetMethod(true) : null;
            if (set == null) return false;
            set.Invoke(o, new object[] { value });
            return true;
        }

        /// <summary>OnSessionLaunched: linia startu obrazkow okien (kazda kampania).</summary>
        internal static void StartLine()
        {
            Log.Info(Tag + "start sesji | latka okien ROT: " + _patch + " | klucz Story Images For Rot Windows " + (Key ? "TAK" : "NIE") + " | " + StoryPictures.StartText()
                     + " | podmienionych okien ROT w procesie " + _swaps + " | wczytanych obrazkow " + StoryPictures.Loads + ", odrzuconych " + StoryPictures.Rejects
                     + " | potkniecia " + _stumbles + " (obrazki " + StoryPictures.Stumbles + ").");
        }

        /// <summary>OnGameEnd: podmienione naglowki wracaja (okna juz nie ma).</summary>
        internal static void Reset()
        {
            try { foreach (var sw in _swapped.Values) Set(sw.Event, "HeaderImage", sw.Orig); }
            catch (Exception e) { Stumble("Reset", e); }
            _swapped.Clear();
        }
    }
}
