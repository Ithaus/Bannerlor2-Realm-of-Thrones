using System;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// Paczka 133f (Jeff 25.10 ok. 12:57: "... w tym logu tak szybko to leci ze ja nie wiem ... przeniesmy logi do zakladki armii"; decyzja (e)): wiersz
    /// "Recent messages (N)" okna "Review the army" - lista komunikatow o wojsku gracza z pierscienia PlayerNews (najnowszy pierwszy, przewijana lista okna
    /// wyboru, tytul do ok. 90 znakow, podpowiedz = data gry, godzina, temat i pelny tekst; "Look" - strona z pelnym tekstem, "Back" - do listy). Tylko odczyt
    /// kopii pierscienia (zero liczenia). Okno nadal tylko z obozu (decyzja (d)).
    /// </summary>
    internal static partial class ArmyReview
    {
        /// <summary>Wiersz glownego okna; pusty pierscien - wyszarzony z powodem.</summary>
        private static InquiryElement NewsRowSafe()
        {
            try
            {
                int n = PlayerNews.Count;
                if (n <= 0)
                    return new InquiryElement("news", "Recent messages: none yet", null, false,
                                              "Messages about your men - kit, horses, pay, drill, camp, the march and the quartermaster - are kept here as they come, the last "
                                              + PlayerNews.Keep + ". None since this campaign was loaded.");
                return new InquiryElement("news", "Recent messages (" + n + ")", null, true,
                                          "The last messages about your men - kit, horses, pay, drill, camp, the march and the quartermaster - newest first, each with its day and hour."
                                          + " The last " + PlayerNews.Keep + " are kept until you load a game.");
            }
            catch (Exception e) { Stumble("NewsRow", e); return new InquiryElement("news", "Recent messages (not available)", null, false, "The messages cannot be read now."); }
        }

        /// <summary>Lista komunikatow (kopia pierscienia w chwili otwarcia); strona komunikatu wraca tutaj, "Back" - do przegladu.</summary>
        private static void OpenNews()
        {
            var list = PlayerNews.Newest();
            if (list.Count == 0) { OpenMain(false); return; }
            var rows = new List<InquiryElement>(list.Count);
            foreach (var it in list) rows.Add(new InquiryElement(it.Id, PlayerNews.Title(it), null, true, PlayerNews.Hint(it)));
            bool photo = WindowPhotos.Active;   // paczka 132: okno proby zdjec - przyciski bez skutku
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("Recent messages",
                "About your men only - kit, horses, pay, drill, camp, the march and the quartermaster - newest first. Point at a line for its day and the whole"
                + " message; Look shows it on a page.", rows, true, 1, 1, "Look", "Back",
                delegate (List<InquiryElement> picked)
                {
                    if (Inert(photo)) return;
                    try
                    {
                        object id = picked != null && picked.Count > 0 ? picked[0].Identifier : null;
                        PlayerNews.Item it = null;
                        if (id is int)
                            foreach (var x in list) if (x.Id == (int)id) { it = x; break; }
                        if (it == null) { OpenNews(); return; }
                        _dReports++;
                        ShowPages("Message - " + it.Topic, Paginate(PlayerNews.PageText(it)), 0, OpenNews);
                    }
                    catch (Exception e) { Stumble("News.Pick", e); }
                },
                delegate (List<InquiryElement> _) { if (Inert(photo)) return; try { OpenMain(false); } catch (Exception e) { Stumble("News.Back", e); } }), true);
        }

        /// <summary>133g, proba zdjec okien (tylko autotest): pusty pierscien - jeden komunikat proby z TEGO pliku (lista wojska PlayerNews: ArmyReview -> "Army"),
        /// ta sama droga co test T1 (Log.Player -> znacznik -> nasluch gry -> pierscien), zeby lista miala co pokazac. Zwraca liczbe wpisow po.</summary>
        internal static int PhotoNewsSeed()
        {
            if (PlayerNews.Count > 0) return PlayerNews.Count;
            Log.Player("Window photos test: no message about your men yet - this line shows how one is listed under Recent messages.");
            return PlayerNews.Count;
        }
    }
}
