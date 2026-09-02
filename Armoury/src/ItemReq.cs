using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace Armoury
{
    /// <summary>
    /// ZASADA NADRZEDNA (Jeff 29.08): "umiejetnosci sa swiete - jesli ich nie
    /// masz, nie mozesz uzywac. Konie i pancerze tez, CALY ekwipunek!".
    /// Jeden egzekutor wymagan dla wszystkich naszych mechanik:
    ///  - bron i kon: RelevantSkill + Difficulty (silnik),
    ///  - PANCERZ: silnik nie ma wymogu (RelevantSkill null), ale ROT wpisuje
    ///    difficulty w XML (helm 140, czapka Dany 200...) - u nas to wymog
    ///    ATLETYKI: Athletics >= Difficulty.
    /// </summary>
    internal static class ItemReq
    {
        internal static bool Meets(CharacterObject ch, ItemObject it, out string why)
        {
            why = null;
            try
            {
                if (ch == null || it == null) return true;
                if (it.Difficulty <= 0) return true;
                SkillObject skill = SkillFor(it);
                if (skill == null) return true;
                int have = ch.GetSkillValue(skill);
                if (have >= it.Difficulty) return true;
                why = "Requires " + skill.Name + " " + it.Difficulty + " - this troop has " + have + ".";
                return false;
            }
            catch { return true; }
        }

        internal static bool Meets(CharacterObject ch, ItemObject it)
        {
            string why; return Meets(ch, it, out why);
        }

        /// <summary>
        /// JEDNO ZRODLO PRAWDY o tym, ktory skill pilnuje przedmiotu: bron
        /// i kon maja RelevantSkill z danych; pancerz - Atletyka (Prawo Wagi
        /// i Prawo Tieru wpisuja mu Difficulty); AMUNICJA nie ma skilla
        /// w danych, a Prawo Tieru daje jej wymog - wiec strzaly pilnuje
        /// Bow, belty Crossbow (Jeff 02.09: "strzal t6 bandyci nie moga
        /// miec"). Wszystkie miejsca doboru (ItemReq, TroopFit, kwatermistrz,
        /// DragonUnmount, warta DTE w CrashScribe) maja uzywac tego.
        /// </summary>
        internal static SkillObject SkillFor(ItemObject it)
        {
            if (it == null) return null;
            var skill = it.RelevantSkill;
            if (skill != null) return skill;
            if (it.HasArmorComponent) return DefaultSkills.Athletics;
            if (it.ItemType == ItemObject.ItemTypeEnum.Arrows) return DefaultSkills.Bow;
            if (it.ItemType == ItemObject.ItemTypeEnum.Bolts) return DefaultSkills.Crossbow;
            return null;
        }
    }
}
