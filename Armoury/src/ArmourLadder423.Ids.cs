// PLIK GENEROWANY: python tools/rot_armour_ladder.py --write (kontrola: --check). Nie edytowac recznie.
// Korpusy ROT-Content (ROTassets.xml, items.xml) typu BodyArmor o materiale Leather / Chainmail / Plate: 337 sztuk
// (Leather 171, Chainmail 51, Plate 115). Material i wartosci czytane sa w grze z ArmorComponent - tu tylko id.
namespace Armoury
{
    internal sealed partial class ArmourLadder423
    {
        internal const int IdsExpected = 337;

        private static readonly string[] Ids =
        {
            "lannister_armor", "stark_armor", "unsullied_armor", "unsullied_soldier_armor", "reach_armor", "nightswatch_armor2", "bolton_armor",
            "karstark_armor", "karstark_lord_armor", "dany_dress", "wildling_medium_armor", "ygritte_armor", "wight_armor2", "wight_armor3",
            "stark_armor_female", "reach_armor3", "dayne_armor", "kg_armor", "kg_armor2", "riverlands_armor", "riverlands_noble_armor",
            "blackfish_armor", "westerlands_armor", "stark_armor_1", "stark_armor_2", "stark_armor_3", "stark_armor_4", "dragonstone_armor",
            "stannis_armor", "jonsnow_armor", "hughes_armor", "ironborn_armor", "greyjoy_armor", "nightking_armor", "sarnor_armor", "hound_armor",
            "melisandre_dress", "giant_garb", "vale_knight_armor", "reach_armor2", "cersei_dress", "margaery_dress", "stormlands_armor",
            "tyrion_clothes", "varys_clothes", "baelish_clothes", "baratheon_gambeson", "stormlands_gambeson", "stark_gambeson", "north_gambeson",
            "lannister_gambeson", "west_gambeson", "vale_gambeson", "reach_gambeson", "tarly_gambeson", "dragonstone_gambeson", "riverlands_gambeson",
            "norvos_armor", "norvoshi_priest_armor", "mace_armor", "targ_armor", "manderly_armor", "casterly_armor", "casterly_armor2", "dorne_plate",
            "martell_plate", "yronwood_plate", "west_archer_armor", "lannister_noble_armor", "lannister_soldier_armor", "lannister_soldier_armor2",
            "lannister_scout_armor", "lannister_light_armor", "lannister_officer_armor", "targ_armor2", "bandit_garb", "west_militia_armor",
            "gold_cloak_armor", "valyrian_plate", "braavosi_plate", "valyrian_surcoat", "targ_plate", "braavosi_curiass", "volantis_armor",
            "volantis_heavy_tunic", "volantis_light_armor", "volantis_tunic", "mormont_armor", "volantis_armor2", "ironborn_heavy_armor",
            "reach_heavy_armor", "ironborn_leather_armor", "manderly_light_armor", "ghiscari_armor", "brienne_armor", "sarnor_tunic",
            "bolton_knight_armor", "tully_scale_armor", "arryn_armor", "arryn_armor2", "royce_armor", "lyanna_armor", "hightower_armor",
            "hightower_lord_armor", "knight_armor2", "arryn_archer_armor", "common_armor", "crude_armor", "ornate_armor", "ranger_armor",
            "ornate_armor2", "vale_knight_armor2", "royce_armor_female", "casterly_armor_light", "tarly_armor", "ned_clothes",
            "baratheon_king_clothes", "lan_knight_armor", "samurai_armor", "thenn_plate", "bone_tunic", "jaime_clothes", "stark_knight_armor",
            "stark_knight_armor_2", "northern_knight_armor", "northern_knight_armor_2", "dreadguard_armor", "fancy_clothes", "cerwin_armor",
            "bronn_armor1", "bronn_armor2", "podrick_armor", "podrick_armor2", "velaryon_armor", "vale_armor", "vale_armor2", "vale_armor3",
            "vale_armor4", "golden_company_armor", "velaryon_soldier_armor", "tarly_soldier_armor", "golden_company_armor2", "kraken_armor",
            "whitewalker_armor", "summer_armor", "valyrian_surcoat_master", "frey_armor", "jaime_leather", "jaime_leather2", "renly_armor",
            "renly_clothes", "chain_armor_short_tabbard", "scale_armor_short_tabbard", "chain_armor_long_tabbard", "scale_armor_long_tabbard",
            "chainmail", "scale_armor", "tarth_armor", "tarth_armor2", "nightswatch_brigandine", "brigandine1", "brigandine2", "tyroshi_armor",
            "tyroshi_armor2", "tyroshi_armor3", "tyroshi_armor4", "goldcloak_armor", "goldcloak_armor2", "lyseni_armor", "lyseni_lord_armor",
            "umber_armor", "umber_armor2", "cersei_red_dress", "cersei_armor", "dondarion_armor", "dondarion_armor2", "mountain_armor",
            "dondarion_leather", "north_leather", "dragonstone_plate", "stormlands_plate", "myrish_armor", "dayne_plate", "dayne_armor2",
            "dayne_armor3", "bolton_plate", "dorne_armor1", "dorne_armor2", "dorne_armor3", "dorne_armor4", "celtigar_armor", "celtigar_knight_armor",
            "celtigar_lord_armor", "glover_armor", "harlaw_plate", "harlaw_gambeson", "arryn_gambeson", "greyjoy_gambeson", "hightower_surcoat",
            "blackwood_armor", "blackwood_armor2", "blackwood_armor3", "blackwood_lord_armor", "bracken_lord_armor", "bracken_armor",
            "arryn_winged_armor", "thenn_scale_armor", "thenn_plate_armor", "raven_teeth_armor", "bracken_heavy_armor", "vargo_armor", "brave_armor",
            "harras_armor", "golden_company_armor3", "tarly_light_armor", "tarly_light_armor2", "blackfyre_plate", "rhaegar_plate", "rhaegar_plate2",
            "dothraki_armor", "dothraki_armor2", "dothraki_armor3", "dothraki_armor4", "dothraki_armor5", "dothraki_armor6", "dothraki_armor8",
            "nightking_armor2", "brienne_plate", "loras_armor", "whitewalker_armor2", "north_plate", "north_plate2", "nightswatch_gambeson",
            "vardis_armor", "qohorik_armor", "qohorik_armor2", "qohorik_armor3", "qohorik_robe", "qohorik_armor4", "pentoshi_armor",
            "pentoshi_armor2", "pentoshi_tunic", "manderly_leather_armor", "manderly_chainmail", "manderly_plate", "manderly_lord_armor",
            "manderly_lord_armor2", "lorathi_armor", "lorathi_armor2", "lorathi_armor3", "lorathi_armor4", "andal_clothes1", "andal_clothes2",
            "andal_clothes3", "andal_clothes4", "andal_clothes5", "andal_clothes6", "andal_clothes7", "andal_clothes8", "andal_robe1", "andal_robe2",
            "andal_robe3", "andal_robe4", "andal_robe5", "andal_robe6", "skagosi_armor", "skagosi_armor2", "dragonstone_garb", "riverlands_garb",
            "westerlands_garb", "northern_garb", "northern_garb2", "ironborn_garb", "lorathi_garb", "vale_garb", "reach_garb", "stormlands_garb",
            "nightswatch_garb", "dorne_garb", "ghiscari_garb", "volantine_garb", "braavosi_garb", "qohorik_garb", "norvoshi_garb", "umber_garb",
            "manderly_garb", "dayne_garb", "hightower_garb", "tarly_garb", "frey_garb", "crownlands_garb", "valyrian_garb", "tyroshi_garb",
            "myrish_garb", "lyseni_garb", "summer_garb", "sarnori_garb", "bracken_garb", "iron_gambeson", "brav_gambeson", "step_gambeson",
            "step_gambeson2", "step_gambeson3", "step_gambeson4", "dorne_gambeson", "yiti_gambeson", "yiti_garb", "ramsay_armor", "stark_lord_armor",
            "stark_lord_armor2", "wilding_garb", "wildling_armor1", "wildling_armor2", "wildling_armor3", "yiti_armor", "yiti_armor2", "yiti_armor3",
            "aemon_armor", "lys_light_armor", "tyr_light_armor", "myr_light_armor", "qarth_armor", "qarth_armor2", "ibb_armor", "ibb_armor2",
            "ibb_armor3", "mallister_armor", "mallister_armor2", "mallister_plate", "westerling_armor", "westerling_armor2", "grafton_armor",
            "grafton_plate", "grafton_plate2", "pirate_armor1", "pirate_armor2", "pirate_armor3",
        };
    }
}
