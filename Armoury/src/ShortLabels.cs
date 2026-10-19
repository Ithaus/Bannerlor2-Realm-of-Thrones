using System;
using System.Collections;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Localization;

namespace Armoury
{
    /// <summary>
    /// n20-napisy (Jeff 19.10: "przy inwestowaniu i wybieraniu opcji ... za duzo tekstu na buttonie i zle to wyglada"): krotkie napisy opcji menu gry
    /// i wierszy okien wyboru; kwoty, sumy i wyjasnienia ida do podpowiedzi (tooltip opcji menu / podpowiedz wiersza okna). Klucz Short Button Labels
    /// (wylaczony = dawne dlugie napisy). Logika i identyfikatory opcji bez zmian.
    /// Szerokosci z prefabow i metryk czcionek gry (GUI/GauntletUI/Fonts/*.fnt, xadvance przy rozmiarze 32):
    /// - opcja menu gry: GameMenu.xml NormalMode.Menu.Width 493 - marginesy napisu 55 + 35 (GameMenuItem.xml) = 403 px, czcionka Galahad 24;
    ///   dluzszy napis lamie sie na 2 linie i przycisk rosnie;
    /// - wiersz okna wyboru (MultiSelectionQueryPopup.xml ROT-Content): 447 - 20 - 8 = 419 px (z obrazkiem 86 px mniej = 333), FiraSansExtraCondensed 25,
    ///   wysokosc stala 65 px = najwyzej 2 linie (trzecia wychodzi poza ramke);
    /// - przycisk zwyklego okna (SingleQueryPopup.xml): 251 - 47 - 40 = 164 px, Galahad 24.
    /// </summary>
    internal static class ShortLabels
    {
        internal const int MenuPx = 403, RowPx = 419, ButtonPx = 164;

        // xadvance znakow ASCII 32..126 przy rozmiarze 32 (Galahad.fnt) - tylko do linii kontrolnej szerokosci napisow menu
        private static readonly byte[] Galahad =
        {
            8,6,7,18,14,17,23,5,9,9,12,13,5,10,5,10,16,9,14,14,16,14,15,13,16,15,5,5,13,13,13,12,21,21,16,22,22,15,15,23,21,7,8,17,15,27,22,24,15,24,
            16,15,18,21,19,27,18,16,18,9,10,9,13,16,5,13,15,14,16,14,10,14,14,6,7,13,6,22,15,15,16,16,9,10,13,15,13,22,13,13,14,9,5,9,13
        };

        private static int _menuShort;      // opcje menu wpiete z krotkim napisem (ta sesja)

        internal static bool On { get { var s = Settings.Current; return s == null || s.ShortButtonLabels; } }

        /// <summary>Napis opcji menu przy wpinaniu: krotki przy kluczu, dawny bez (menu zmienia sie po wczytaniu zapisu).</summary>
        internal static string Menu(string shortText, string longText)
        {
            if (!On) return longText;
            _menuShort++;
            return shortText;
        }

        /// <summary>Napis wiersza okna albo opcji z tekstem liczonym na zywo: krotki przy kluczu, dawny bez.</summary>
        internal static string Pick(string shortText, string longText) { return On ? shortText : longText; }

        /// <summary>Podpowiedz wiersza okna: przy kluczu szczegoly z napisu + dawna podpowiedz; bez klucza dawna.</summary>
        internal static string Hint(string details, string old)
        {
            if (!On || string.IsNullOrEmpty(details)) return old;
            return string.IsNullOrEmpty(old) ? details : details + "\n" + old;
        }

        /// <summary>
        /// Warunek opcji menu z dopiskiem do podpowiedzi: wola dawny warunek bez zmian, a przy kluczu dokleja szczegoly, ktore wypadly z napisu
        /// (przy opcji nieaktywnej powod zostaje na gorze). Bez klucza - dawny warunek 1:1.
        /// </summary>
        internal static GameMenuOption.OnConditionDelegate Tip(GameMenuOption.OnConditionDelegate cond, string details)
        {
            if (!On || string.IsNullOrEmpty(details)) return cond;
            return delegate (MenuCallbackArgs a)
            {
                bool r = cond == null || cond(a);
                try
                {
                    if (r)
                    {
                        string old = a.Tooltip != null ? a.Tooltip.ToString() : null;
                        string t = string.IsNullOrEmpty(old) ? details : a.IsEnabled ? details + "\n" + old : old + "\n" + details;
                        a.Tooltip = new TextObject("{=!}" + t);
                    }
                }
                catch { }
                return r;
            };
        }

        /// <summary>Szerokosc napisu w px w menu gry (Galahad 24); znaki spoza ASCII liczone jak 'n'.</summary>
        internal static int MenuWidth(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int sum = 0;
            foreach (char c in s) sum += c >= 32 && c <= 126 ? Galahad[c - 32] : 15;
            return (int)Math.Round(sum * 24 / 32.0);
        }

        /// <summary>
        /// OnAfterSessionLaunched (po wpieciu wszystkich menu): linia kontrolna - ile opcji Armoury (id arm_*) jest w menu gry, ile wpieto z krotkim napisem,
        /// najszerszy staly napis wedlug metryk czcionki i ile przekracza szerokosc przycisku (403 px). Napisy liczone na zywo ({ZMIENNA}) pominiete.
        /// </summary>
        internal static void Audit(CampaignGameStarter starter)
        {
            try
            {
                var mgr = Campaign.Current != null ? Campaign.Current.GameMenuManager : null;
                var f = AccessTools.Field(typeof(GameMenuManager), "_gameMenus");
                var menus = mgr != null && f != null ? f.GetValue(mgr) as IDictionary : null;
                int n = 0, live = 0, over = 0, maxW = 0; string maxT = "-";
                if (menus != null)
                    foreach (DictionaryEntry kv in menus)
                    {
                        var m = kv.Value as GameMenu;
                        if (m == null) continue;
                        foreach (var o in m.MenuOptions)
                        {
                            if (o == null || o.IdString == null || !o.IdString.StartsWith("arm_", StringComparison.Ordinal)) continue;
                            n++;
                            string raw = o.Text != null ? o.Text.ToString() : "";
                            if (raw.Length == 0 || raw.IndexOf('{') >= 0) { live++; continue; }
                            int w = MenuWidth(raw);
                            if (w > MenuPx) over++;
                            if (w > maxW) { maxW = w; maxT = raw; }
                        }
                    }
                Log.Info("Napisy przyciskow (n20-napisy): krotkie " + (On ? "WLACZONE" : "WYLACZONE (klucz Short Button Labels)") + " | opcji menu Armoury " + n
                         + ", wpietych z krotkim napisem " + _menuShort + ", liczonych na zywo " + live + " | najszerszy staly napis " + maxW + " px z " + MenuPx
                         + " ('" + maxT + "') | za szerokich: " + over);
            }
            catch (Exception e) { Log.Error("ShortLabels.Audit", e); }
            _menuShort = 0;
        }
    }
}
