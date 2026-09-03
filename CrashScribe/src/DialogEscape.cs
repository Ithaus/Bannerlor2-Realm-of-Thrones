using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;

namespace CrashScribe
{
    /// <summary>
    /// WYJSCIE AWARYJNE Z ROZMOWY (Jeff 03.09: "religia jest zepsuta - wchodzisz
    /// w rozmowe z preacherem, klikasz continue i nic sie nie dzieje, bo nie ma
    /// sciezek dialogowych; musialem recznie wylaczyc gre"). Powitanie BK
    /// prowadzi lord_introduction -> lord_start; gdy zaden wiersz NPC ze stanu
    /// lord_start nie przejdzie warunku (zepsuta religia, brak duchownego
    /// w rejestrze), gra stoi z przyciskiem "continue" i pusta lista.
    /// Wiersze GRACZA pokazuja sie tylko wtedy, gdy zaden wiersz NPC nie
    /// pasuje - wiec dokladamy dwa: "Porozmawiajmy" (do zwyklych opcji)
    /// i "Zegnaj" (zamkniecie). Dla kazdego rozmowcy, nie tylko kaplana -
    /// dziura bez wyjscia nigdy nie powinna byc mozliwa.
    /// </summary>
    internal sealed class DialogEscape : CampaignBehaviorBase
    {
        private static int _used;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            try
            {
                foreach (var state in new[] { "lord_start", "lord_introduction", "lord_pretalk", "bk_preacher_asked_preaching", "bk_preacher_asked_faith", "bk_preacher_asked_induction" })
                {
                    starter.AddPlayerLine("cs_escape_talk_" + state, state, "hero_main_options",
                        "{=!}Let us talk.", () => Stuck(), () => { _used++; Scribe.Line("DialogEscape: rozmowa uratowana (" + _used + ") - opcje z hero_main_options."); }, 1);
                    starter.AddPlayerLine("cs_escape_bye_" + state, state, "close_window",
                        "{=!}Farewell.", () => Stuck(), () => { _used++; Scribe.Line("DialogEscape: rozmowa zamknieta awaryjnie (" + _used + ")."); }, 0);
                }
                Scribe.Line("DialogEscape: awaryjne 'Let us talk' / 'Farewell' w martwych stanach rozmowy (lord_start i stany kaplana BK).");
            }
            catch (Exception e) { try { Scribe.Report("CrashScribe", e, "DialogEscape.OnSessionLaunched", null); } catch { } }
        }

        private static bool Stuck()
        {
            try
            {
                var h = Hero.OneToOneConversationHero;
                if (h == null) return false;
                if (h.IsPreacher) Scribe.Line("DialogEscape: martwy stan rozmowy z kaplanem " + h.Name
                    + " (" + (h.CurrentSettlement != null ? h.CurrentSettlement.Name.ToString() : "?") + ") - pokazuje wyjscie.");
                return true;
            }
            catch { return true; }
        }
    }
}
