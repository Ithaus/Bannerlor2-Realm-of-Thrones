using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// Paczka 133f (Jeff 25.10 ok. 12:57: "... w tym logu tak szybko to leci ze ja nie wiem ... de facto przeniesmy logi do zakladki armii, aby tam wszystko bylo
    /// czytelne"; decyzja (e), krytyka K13): pierscien ostatnich komunikatow o WOJSKU GRACZA - wiersz "Recent messages (N)" okna "Review the army"
    /// (ArmyReview.News.cs). Kazdy taki komunikat idzie tez do Armoury.log (linia "Wojsko (133f) [Temat]: tekst") - dotad Log.Player nie trafial do zadnego pliku.
    ///
    /// JEDNA ZASADA: komunikat jest "o wojsku", gdy na ekran wysyla go plik zrodlowy z listy Sources (sprzet, konie, zold, musztra, oboz, marsz, kwatermistrz,
    /// armia; pliki partial - nazwa do pierwszej kropki, np. ArmyReview.Pin.cs = ArmyReview).
    ///  - Log.Player (ok. 525 wywolan w 158 plikach) ma [CallerFilePath] - kompilator wpisuje plik wolajacego, zadne wywolanie sie nie zmienia;
    ///  - bezposrednie InformationManager.DisplayMessage / MBInformationManager.AddQuickInformation w plikach z listy ida przez Show / Quick (ten sam
    ///    [CallerFilePath]): QuartermasterLaw (8 - w tym ShoutShortages, K13), NightRest, ForcedMarch, ArmyRestDay (pomocnicze Msg) i SaddleLaw (szybka informacja);
    ///  - plik z listy ustawia znacznik [ThreadStatic] na czas JEDNEGO wyslania (K13); JEDEN nasluch na publicznych zdarzeniach gry
    ///    (InformationManager.DisplayMessageInternal i MBInformationManager.FiringQuickInformation - bez latek Harmony) zapisuje to, co naprawde poszlo na ekran
    ///    przy ustawionym znaczniku. Zdarzenia czysci tylko InformationManager.Clear / MBInformationManager.Clear przy wyjsciu z gry (Module.FinalizeModule) -
    ///    Hook (-= i +=, bez dubla) wolany na starcie kazdej sesji kampanii (ArmyReview.AddMenus).
    /// Ta sama linia w ciagu godziny gry (np. kwatermistrz przy kazdym otwarciu ekranu) - jeden wpis z licznikiem "(xN)"; do logu kazda.
    /// Pierscien Keep wpisow, tylko w pamieci (nie w zapisie gry); czyszczony przy nowej kampanii i kazdym wczytaniu - StateHygiene.ResetAll (konstruktor
    /// ArmouryBehavior, zawsze - takze przy wylaczonym StateHygieneEnabled, jak pamiec E8/H). Bez kampanii (menu glowne) nic nie zapisuje.
    /// Poza zakresem (K13): zolte CrashScribe "QM: N EMPTY in battle" (inny DLL; po 133a tylko w bitwie - pokazuje to wiersz "Last battle") i komunikaty samej gry
    /// (gra nie pisze nic o niezaplaconym zoldzie - DefaultClanFinanceModel ustawia tylko HasUnpaidWages).
    /// Potkniecia liczone (pierwsze 3 i co 50 do Log.Error), nic sie nie wylacza. Napisy w grze po angielsku.
    /// </summary>
    internal static class PlayerNews
    {
        internal const int Keep = 60;            // pierscien: ostatnie 60 wpisow (decyzja (e))
        internal const int TitleChars = 90;      // tytul wiersza listy (wiersz okna ma 2 linie)
        private const string LogHead = "Wojsko (133f) ";

        /// <summary>Wpis pierscienia (kopia dla okna - Newest).</summary>
        internal sealed class Item
        {
            public int Id;                       // numer w kampanii (identyfikator wiersza listy)
            public string Text, Topic;
            public CampaignTime First, Last;     // pierwszy i ostatni raz (ta sama linia w ciagu godziny gry - scalona)
            public int Count;
            public bool Bad;                     // czerwony komunikat (Log.Player bad, Colors.Red)
        }

        // plik zrodlowy (nazwa do pierwszej kropki) -> temat w oknie; tylko komunikaty o wojsku gracza (decyzja (e))
        private static readonly Dictionary<string, string> Sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "QuartermasterLaw", "Quartermaster" }, { "MusterBook", "Quartermaster" },
            { "GarrisonKit", "Kit" }, { "GarrisonArmory", "Kit" }, { "GearFit", "Kit" }, { "MenUpgrade", "Kit" }, { "TroopSelfMend", "Kit" },
            { "ArmyClothing", "Kit" }, { "CompanionGear", "Kit" }, { "AiGear", "Kit" }, { "CaptiveArmory", "Kit" },
            { "Stables", "Horses" }, { "SaddleLaw", "Horses" },
            { "MenPurse", "Pay" }, { "GentryService", "Pay" }, { "UpgradePurse", "Pay" }, { "VetPurse", "Pay" }, { "CasualtyLoot", "Pay" }, { "Leaks310", "Pay" },
            { "Drill", "Drill" }, { "CampDrill", "Drill" },
            { "NightRest", "Camp" }, { "ArmyRestDay", "Camp" }, { "CampFever", "Camp" }, { "CampCart", "Camp" },
            { "ForcedMarch", "March" },
            { "DesertionLaw", "Army" }, { "StragglersRally", "Army" }, { "BattleMuster", "Army" }, { "ArmyReview", "Army" },
        };

        private static readonly object Gate = new object();
        private static readonly List<Item> _ring = new List<Item>(Keep);                                   // najstarszy pierwszy
        private static readonly Dictionary<string, string> _bySrc = new Dictionary<string, string>(StringComparer.Ordinal);   // [CallerFilePath] -> temat (null - nie o wojsku)
        private static int _seq;
        private static long _seen, _merged, _dropped;   // od nowej kampanii / wczytania
        private static int _stumbles;
        private static bool _hookShow, _hookQuick;
        private static string _hookWhy = "nie probowano";

        [ThreadStatic] private static string _topic;    // temat komunikatu wysylanego TERAZ z pliku z listy (null - nie o wojsku)

        private static void Stumble(string where, Exception e)
        {
            int n;
            lock (Gate) n = ++_stumbles;
            if (n <= 3 || n % 50 == 0) { try { Log.Error("PlayerNews." + where + " (potkniecie " + n + ")", e); } catch { } }
        }

        // ------------------------------------------------------------ znacznik
        /// <summary>Temat pliku zrodlowego ([CallerFilePath]); null - plik spoza listy. Pamiec po pelnej sciezce.</summary>
        internal static string TopicOf(string src)
        {
            if (string.IsNullOrEmpty(src)) return null;
            lock (Gate)
            {
                string t;
                if (_bySrc.TryGetValue(src, out t)) return t;
                string name;
                try { name = Path.GetFileName(src); } catch { name = null; }
                if (!string.IsNullOrEmpty(name))
                {
                    int dot = name.IndexOf('.');
                    if (dot > 0) name = name.Substring(0, dot);
                }
                if (string.IsNullOrEmpty(name) || !Sources.TryGetValue(name, out t)) t = null;
                _bySrc[src] = t;
                return t;
            }
        }

        /// <summary>Znacznik na czas wyslania JEDNEGO komunikatu: temat pliku wolajacego (null - nie o wojsku). Zwraca poprzedni - Leave go przywraca. Nie rzuca.</summary>
        internal static string Enter(string src)
        {
            string was = _topic;
            try { _topic = TopicOf(src); }
            catch (Exception e) { _topic = null; Stumble("Enter", e); }
            return was;
        }

        internal static void Leave(string was) { _topic = was; }

        /// <summary>InformationManager.DisplayMessage z plikow z listy (zamiast wywolania wprost) - ta sama wiadomosc, znacznik na czas wyslania.
        /// Wyjatek wychodzi do wolajacego jak dotad.</summary>
        internal static void Show(InformationMessage m, [CallerFilePath] string src = "")
        {
            string was = Enter(src);
            try { InformationManager.DisplayMessage(m); }
            finally { Leave(was); }
        }

        /// <summary>MBInformationManager.AddQuickInformation (tylko tekst - jak wolajacy) z plikow z listy, znacznik na czas wyslania. Wyjatek wychodzi do wolajacego.</summary>
        internal static void Quick(TextObject t, [CallerFilePath] string src = "")
        {
            string was = Enter(src);
            try { MBInformationManager.AddQuickInformation(t); }
            finally { Leave(was); }
        }

        // ------------------------------------------------------------ nasluch
        /// <summary>Start sesji kampanii (ArmyReview.AddMenus): nasluch na zdarzeniach gry - najpierw zdjecie (bez dubla po kolejnej sesji), potem dopiecie.</summary>
        internal static void Hook()
        {
            var why = new List<string>();
            try
            {
                InformationManager.DisplayMessageInternal -= OnShow;
                InformationManager.DisplayMessageInternal += OnShow;
                _hookShow = true;
            }
            catch (Exception e) { _hookShow = false; why.Add("DisplayMessageInternal " + e.GetType().Name); Stumble("Hook.Show", e); }
            try
            {
                MBInformationManager.FiringQuickInformation -= OnQuick;
                MBInformationManager.FiringQuickInformation += OnQuick;
                _hookQuick = true;
            }
            catch (Exception e) { _hookQuick = false; why.Add("FiringQuickInformation " + e.GetType().Name); Stumble("Hook.Quick", e); }
            _hookWhy = why.Count == 0 ? "ok" : string.Join(", ", why.ToArray());
        }

        private static void OnShow(InformationMessage m)
        {
            string t = _topic;
            if (t == null || m == null) return;
            try { Add(t, m.Information, IsRed(m.Color)); }
            catch (Exception e) { Stumble("OnShow", e); }
        }

        private static void OnQuick(string text, int extraMs, BasicCharacterObject who, Equipment eq, string sound)
        {
            string t = _topic;
            if (t == null) return;
            try { Add(t, text, false); }
            catch (Exception e) { Stumble("OnQuick", e); }
        }

        private static bool IsRed(Color c) { return c.Red >= 0.9f && c.Green <= 0.3f && c.Blue <= 0.3f; }

        private static void Add(string topic, string text, bool bad)
        {
            if (string.IsNullOrEmpty(text) || Campaign.Current == null) return;
            string s = text.Replace("\r", "").Trim();
            if (s.Length == 0) return;
            var now = CampaignTime.Now;
            bool merged = false;
            lock (Gate)
            {
                _seen++;
                var last = _ring.Count > 0 ? _ring[_ring.Count - 1] : null;
                if (last != null && last.Topic == topic && string.Equals(last.Text, s, StringComparison.Ordinal) && now.ToHours - last.Last.ToHours < 1.0)
                {
                    last.Count++; last.Last = now; last.Bad |= bad; _merged++; merged = true;
                }
                else
                {
                    if (_ring.Count >= Keep) { _ring.RemoveAt(0); _dropped++; }
                    _ring.Add(new Item { Id = ++_seq, Text = s, Topic = topic, First = now, Last = now, Count = 1, Bad = bad });
                }
            }
            Log.Info(LogHead + "[" + topic + (bad ? ", red" : "") + "]" + (merged ? " (powtorzenie)" : "") + ": " + s.Replace('\n', ' '));
        }

        // ------------------------------------------------------------ stan
        /// <summary>Nowa kampania i kazde wczytanie (StateHygiene.ResetAll - zawsze). Zwraca, ile wpisow bylo w pierscieniu.</summary>
        internal static int Reset()
        {
            _topic = null;
            lock (Gate)
            {
                int had = _ring.Count;
                _ring.Clear();
                _seq = 0; _seen = 0; _merged = 0; _dropped = 0; _stumbles = 0;
                return had;
            }
        }

        internal static int Count { get { lock (Gate) return _ring.Count; } }

        /// <summary>Zapisane komunikaty od nowej kampanii / wczytania (z powtorzeniami) - test T1.</summary>
        internal static long Seen { get { lock (Gate) return _seen; } }

        /// <summary>Kopia pierscienia dla okna: najnowszy pierwszy.</summary>
        internal static List<Item> Newest()
        {
            lock (Gate)
            {
                var l = new List<Item>(_ring.Count);
                for (int i = _ring.Count - 1; i >= 0; i--)
                {
                    var x = _ring[i];
                    l.Add(new Item { Id = x.Id, Text = x.Text, Topic = x.Topic, First = x.First, Last = x.Last, Count = x.Count, Bad = x.Bad });
                }
                return l;
            }
        }

        /// <summary>Linia startu sesji: stan nasluchu.</summary>
        internal static string Wiring
        {
            get
            {
                return "nasluch DisplayMessage " + (_hookShow ? "TAK" : "NIE") + ", szybkich informacji " + (_hookQuick ? "TAK" : "NIE") + " (" + _hookWhy + "), plikow zrodel "
                       + Sources.Count + ", pierscien " + Keep;
            }
        }

        /// <summary>Segment linii raportu okna i testu T1.</summary>
        internal static string Brief()
        {
            lock (Gate)
            {
                return "w pierscieniu " + _ring.Count + "/" + Keep + ", zapisanych " + _seen + " (powtorzen scalonych " + _merged + ", wypadlo " + _dropped + "), nasluch "
                       + (_hookShow && _hookQuick ? "TAK" : "NIE") + ", potkniecia " + _stumbles;
            }
        }

        // ------------------------------------------------------------ teksty okna (po angielsku)
        /// <summary>Tytul wiersza listy: tekst do ok. TitleChars znakow (ciecie na spacji) i "(xN)" przy powtorzeniach.</summary>
        internal static string Title(Item it)
        {
            string s = Short(it.Text, TitleChars);
            return it.Count > 1 ? s + " (x" + it.Count + ")" : s;
        }

        private static string Short(string s, int max)
        {
            s = (s ?? "").Replace('\n', ' ');
            if (s.Length <= max) return s;
            int cut = s.LastIndexOf(' ', max - 3);
            if (cut < max / 2) cut = max - 3;
            return s.Substring(0, cut).TrimEnd(' ', ',', ';', ':', '-') + "...";
        }

        /// <summary>Data gry, godzina i ile dni temu (K16 - bez numeru doby).</summary>
        private static string When(CampaignTime t)
        {
            string d;
            try { d = CalendarText.Date(t, false); } catch { d = "?"; }
            int h = 0;
            try { h = t.GetHourOfDay; } catch { h = 0; }
            int ago = 0;
            try { ago = Math.Max(0, (int)Math.Floor(t.ElapsedDaysUntilNow)); } catch { ago = 0; }
            return d + ", " + h.ToString("00") + ":00" + (ago == 0 ? " (today)" : ago == 1 ? " (yesterday)" : " (" + ago + " days ago)");
        }

        private static string Head(Item it)
        {
            var sb = new StringBuilder();
            sb.Append(When(it.Last)).Append(" - ").Append(it.Topic);
            if (it.Bad) sb.Append(", a warning");
            if (it.Count > 1) sb.Append(" - ").Append(it.Count).Append(" times, the first at ").Append(When(it.First));
            return sb.ToString();
        }

        /// <summary>Podpowiedz wiersza: kiedy, temat i pelny tekst.</summary>
        internal static string Hint(Item it) { return Head(it) + "\n" + it.Text; }

        /// <summary>Strona komunikatu ("Look").</summary>
        internal static string PageText(Item it) { return Head(it) + "\n\n" + it.Text; }
    }
}
