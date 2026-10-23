using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// m109-przesluch (Jeff 22.10 21:40, pkt 4c: "przy przesluchaniu liczy sie tez umiejetnosc perswazji (Charm) i perki innych umiejetnosci, ktore daja premie
    /// do perswazji - to samo powinno byc w plotkach liczone w tawernie") - JEDNA MIARA PERSWAZJI GRACZA w punktach Uroku. Wolaja ja przesluchanie jencow w obozie
    /// (CampInterrogation) i kufel w karczmie (InnDrink.Talk -> Rumours.Drink, klucz GossipPerDrinkByPersuasion).
    /// Dekompilacja 1.4.8 (DefaultPersuasionModel, DefaultPerks): wzor gry daje ok. 99% szansy bazowej przy kazdym Uroku, a premia za umiejetnosc jest 0 ponizej
    /// MaxSkillRequiredForEpicPerkBonus (250) - dlatego miara jest nasza, z perkow, ktore gra opisuje jako premie do perswazji:
    ///   P = Urok + Imposing Stature (Atletyka 75, "+30% persuasion chance") 30 + Meaningful Favors (Urok, "podwojny sukces") 10
    ///       + Moral Leader 25 wobec tej samej kultury | Natural Leader 25 wobec innej (Urok 250, "-1 sukces wymagany")
    ///       + mistrz 20 przy Uroku >= MaxSkillRequiredForEpicPerkBonus + trudnosc perswazji gry x 100 (Very Easy 10, Easy 5, Realistic 0).
    /// Forgivable Grievances nie wchodzi do P (gra: "20% na unikniecie krytycznej porazki") - przesluchanie liczy nim tylko smierc pod torturami.
    /// Cechy i blogoslawienstwa ROT nie wchodza (w grze przy tym wzorze < 1%). Rozmowca bez kultury (null) - bez premii przywodcy.
    /// Wyjatek - sam Urok, potkniecie liczone (pierwsze 3 i co 50. do Log.Error), nic nie gasimy.
    /// </summary>
    internal static class Persuasion
    {
        internal const int StatureBonus = 30, FavorsBonus = 10, LeaderBonus = 25, MasterBonus = 20;

        internal struct Parts
        {
            public int Charm, Stature, Favors, Leader, Master, Difficulty, Total;
            public string LeaderPerk;   // "Moral" | "Natural" | "-"
        }

        private static int _stumbles;

        internal static int Stumbles { get { return _stumbles; } }

        /// <summary>Miara perswazji bohatera me wobec rozmowcy kultury other (null - bez premii przywodcy).</summary>
        internal static Parts Measure(Hero me, CultureObject other)
        {
            var p = new Parts { LeaderPerk = "-" };
            if (me == null) return p;
            try { p.Charm = me.GetSkillValue(DefaultSkills.Charm); } catch { p.Charm = 0; }
            try
            {
                if (me.GetPerkValue(DefaultPerks.Athletics.ImposingStature)) p.Stature = StatureBonus;
                if (me.GetPerkValue(DefaultPerks.Charm.MeaningfulFavors)) p.Favors = FavorsBonus;
                if (other != null)
                {
                    if (me.Culture == other) { if (me.GetPerkValue(DefaultPerks.Charm.MoralLeader)) { p.Leader = LeaderBonus; p.LeaderPerk = "Moral"; } }
                    else if (me.GetPerkValue(DefaultPerks.Charm.NaturalLeader)) { p.Leader = LeaderBonus; p.LeaderPerk = "Natural"; }
                }
                var m = Campaign.Current != null ? Campaign.Current.Models : null;
                if (m != null && m.CharacterDevelopmentModel != null && p.Charm >= m.CharacterDevelopmentModel.MaxSkillRequiredForEpicPerkBonus) p.Master = MasterBonus;
                if (m != null && m.DifficultyModel != null) p.Difficulty = (int)Math.Round(Math.Max(0f, m.DifficultyModel.GetPersuasionBonusChance()) * 100f);
            }
            catch (Exception e)
            {
                p.Stature = p.Favors = p.Leader = p.Master = p.Difficulty = 0; p.LeaderPerk = "-";
                _stumbles++;
                if (_stumbles <= 3 || _stumbles % 50 == 0) Log.Error("Persuasion.Measure (potkniecie " + _stumbles + ")", e);
            }
            p.Total = p.Charm + p.Stature + p.Favors + p.Leader + p.Master + p.Difficulty;
            return p;
        }

        /// <summary>Dla gracza (EN): "Charm 15, Imposing Stature +30, Natural Leader +25".</summary>
        internal static string Words(Parts p)
        {
            string s = "Charm " + p.Charm;
            if (p.Stature > 0) s += ", Imposing Stature +" + p.Stature;
            if (p.Favors > 0) s += ", Meaningful Favors +" + p.Favors;
            if (p.Leader > 0) s += ", " + (p.LeaderPerk == "Moral" ? "Moral Leader" : "Natural Leader") + " +" + p.Leader;
            if (p.Master > 0) s += ", mastery +" + p.Master;
            if (p.Difficulty > 0) s += ", easier persuasion +" + p.Difficulty;
            return s;
        }

        /// <summary>Do logu (PL): "Urok 15 + Imposing Stature 0 + Meaningful Favors 0 + przywodca 0 (-) + mistrz 0 + trudnosc 0 = 15".</summary>
        internal static string LogText(Parts p)
        {
            return "Urok " + p.Charm + " + Imposing Stature " + p.Stature + " + Meaningful Favors " + p.Favors + " + przywodca " + p.Leader + " (" + (p.LeaderPerk ?? "-") + ")"
                   + " + mistrz " + p.Master + " + trudnosc " + p.Difficulty + " = " + p.Total;
        }
    }
}
