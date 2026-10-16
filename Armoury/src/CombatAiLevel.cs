using System;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace Armoury
{
    /// <summary>
    /// TRUDNOSC SI W WALCE (Jeff 16.10: "Combat AI ... Daj Realistic albo Hard" - w jego opcjach tego nie widac).
    /// Opcja kampanii gry CampaignOptions.CombatAIDifficulty (pole zapisu, Very Easy / Easy / Realistic - Realistic to najwyzszy stopien gry).
    /// Gra mnozy nia poziom SI w walce: DefaultDifficultyModel.GetCombatAIDifficultyMultiplier 0 / 0.5 / 1, SandboxAgentStatCalculateModel.GetDifficultyModifier,
    /// RBM w postfiksie SetAiRelatedProperties 0.7 / 0.85 / 1.0 - nizej niz Realistic nawet tier 6 rzadko blokuje i paruje.
    /// Dowod, ze kampania Jeffa nie stoi na Realistic: log 16.10 "Audyt predkosci ... Game Difficulty +0.40" przy Base +3.99 = premia 10% predkosci na mapie,
    /// czyli PlayerMapMovementSpeed Very Easy (preset Freebooter albo wlasny) - ta sama grupa opcji co SI walki.
    /// Klucz CombatAiRealistic: przy kazdym starcie sesji kampanii ustawia SI walki na Realistic, gdy jest nizej (wylaczony = zostaje to, co w opcjach kampanii).
    /// Inne opcje trudnosci tylko czyta do linii startu (predkosc na mapie, obrazenia twoich ludzi, werbunek itd. bez zmian).
    /// </summary>
    internal static class CombatAiLevel
    {
        internal static void Apply()
        {
            var s = Settings.Current;
            if (Campaign.Current == null || Campaign.Current.Options == null) { Log.Info("Trudnosc SI walki (CombatAiRealistic): brak opcji kampanii - nic."); return; }
            CampaignOptions.Difficulty was = CampaignOptions.CombatAIDifficulty;
            string act;
            if (s == null || !s.CombatAiRealistic) act = "klucz wylaczony - zostaje " + Name(was);
            else if (was == CampaignOptions.Difficulty.Realistic) act = "juz Realistic";
            else
            {
                CampaignOptions.CombatAIDifficulty = CampaignOptions.Difficulty.Realistic;
                act = Name(was) + " -> " + Name(CampaignOptions.CombatAIDifficulty) + " (zapis zachowa przy nastepnym zapisie)";
            }
            var sb = new StringBuilder("Trudnosc SI walki (CombatAiRealistic): ");
            sb.Append(act)
              .Append(" | inne opcje kampanii (bez zmian): obrazenia twoich ludzi ").Append(Name(CampaignOptions.PlayerTroopsReceivedDamage))
              .Append(", werbunek ").Append(Name(CampaignOptions.RecruitmentDifficulty))
              .Append(", predkosc na mapie ").Append(Name(CampaignOptions.PlayerMapMovementSpeed))
              .Append(", perswazja ").Append(Name(CampaignOptions.PersuasionSuccessChance))
              .Append(", smierc rodu w bitwie ").Append(Name(CampaignOptions.ClanMemberDeathChance))
              .Append(", smierc w bitwie ").Append(Name(CampaignOptions.BattleDeath))
              .Append(", skradanie ").Append(Name(CampaignOptions.StealthAndDisguiseDifficulty))
              .Append(", Ironman ").Append(CampaignOptions.IsIronmanMode ? "tak" : "nie").Append('.');
            Log.Info(sb.ToString());
        }

        private static string Name(CampaignOptions.Difficulty d)
        {
            switch (d)
            {
                case CampaignOptions.Difficulty.VeryEasy: return "Very Easy";
                case CampaignOptions.Difficulty.Easy: return "Easy";
                case CampaignOptions.Difficulty.Realistic: return "Realistic";
                default: return "?" + ((int)d).ToString();
            }
        }
    }
}
