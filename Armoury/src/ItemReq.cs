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
        /// Z16 (Jeff 09.10): SITO BOHATEROW - gracz, towarzysze, lordowie AI. To samo co Meets (ladry konskie - Jazda, jak
        /// u zolnierzy: SkillFor od Z16b). r2-pancerz (PLAN 1.7): dotad ladry bohaterom przepuszczalismy ("Horse barding is not
        /// checked" - pozostalosc projektu Z16, gdy SkillFor dawal ladrom Atletyke), a ekran gry ich NIE przepuszczal: ROT 8.1.8
        /// (XMLParserAddons.RelevantSkill, postfiks na ItemObject.RelevantSkill, przy ROTCoreBehavior.ExtraGearRequirements -
        /// domyslnie TAK, zapis kampanii) daje ladrom Jazde, a pancerzowi Atletyke, wiec CharacterHelper.CanUseItem odmawia
        /// ladr ponad Jazde. Samotest Z16 (test 160 dob na WGRANIU 89): 489/500, niezgodne - ladry (ekran NIE, regula TAK). Teraz ladry
        /// wedlug Jazdy takze u bohaterow (wylacznik HeroBardingNeedsRiding - wylaczony = stare przepuszczanie).
        /// Jedna metoda dla wszystkich drog zakladania u bohaterow (sklad9-p: takze sprzet startowy gracza - StartKit): ekran
        /// (HeroGear), Spoils auto-equip, unikaty (UniqueSpoils, UniqueLaw.StandInFor), zaciag ROT (DragonUnmount),
        /// zamiennik broni zabranej jencowi (ROT gank). Lustro w CrashScribe: Mends.CanUseHero.
        /// </summary>
        internal static bool MeetsHero(CharacterObject ch, ItemObject it, out string why)
        {
            why = null;
            try { if (it != null && it.ItemType == ItemObject.ItemTypeEnum.HorseHarness && !BardingByRiding()) return true; }
            catch { return true; }
            return Meets(ch, it, out why);
        }

        /// <summary>r2-pancerz: ladry u bohaterow wedlug Jazdy (HeroBardingNeedsRiding; bez ustawien - TAK). Odczyt pola, bez alokacji.</summary>
        internal static bool BardingByRiding()
        {
            var s = Settings.Current;
            return s == null || s.HeroBardingNeedsRiding;
        }

        internal static bool MeetsHero(CharacterObject ch, ItemObject it)
        {
            string why; return MeetsHero(ch, it, out why);
        }

        /// <summary>
        /// JEDNO ZRODLO PRAWDY o tym, ktory skill pilnuje przedmiotu: bron
        /// i kon maja RelevantSkill z danych; pancerz - Atletyka (Prawo Wagi
        /// i Prawo Tieru wpisuja mu Difficulty); AMUNICJA: Prawo Tieru daje jej
        /// wymog, a strzaly pilnuje Bow, belty Crossbow (Jeff 02.09: "strzal t6
        /// bandyci nie moga miec"). Uwaga (Z16-5, sprawdzone na DLL 1.4.8):
        /// strzaly i belty MAJA w grze RelevantSkill - z klasy broni
        /// (WeaponComponentData.GetRelevantSkillFromWeaponClass: Arrow -> Bow,
        /// Bolt -> Crossbow), wiec zwraca je juz pierwszy warunek; dwa ostatnie
        /// wiersze to tylko zapas. Wszystkie miejsca doboru (ItemReq, TroopFit,
        /// kwatermistrz, DragonUnmount, warta DTE w CrashScribe) maja uzywac tego.
        /// </summary>
        internal static SkillObject SkillFor(ItemObject it)
        {
            if (it == null) return null;
            var skill = it.RelevantSkill;
            if (skill != null) return skill;
            if (it.ItemType == ItemObject.ItemTypeEnum.HorseHarness) return DefaultSkills.Riding;   // ladry konskie - Jazda (Jeff 09.10), nie Atletyka
            if (it.HasArmorComponent) return DefaultSkills.Athletics;
            if (it.ItemType == ItemObject.ItemTypeEnum.Arrows) return DefaultSkills.Bow;
            if (it.ItemType == ItemObject.ItemTypeEnum.Bolts) return DefaultSkills.Crossbow;
            return null;
        }
    }
}
