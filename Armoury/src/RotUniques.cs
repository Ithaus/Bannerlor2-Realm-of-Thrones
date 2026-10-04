using System.Collections.Generic;

namespace Armoury
{
    /// <summary>
    /// Unikaty Realm of Thrones (spis: docs/ROT-UNIKATY.md, generowany skryptem z ModuleData ROT 04.10). Jeff: "wyklucz przedmioty
    /// unikatowe" - legendy, materialy z legend, rynsztunek imiennych postaci, korony, przedmioty z cena wpisana przez autora
    /// i poza handlem. Uzywane przez ArmsPricing.IsUnique: warsztaty ich nie robia, AI nie kupuje, wycena x prestiz.
    /// </summary>
    internal static class RotUniques
    {
        internal static readonly HashSet<string> Ids = new HashSet<string>
        {
            "dual_blades", "dual_blades2", "dany_hair", "dany_dress", "dany_boots", "ygritte_armor",
            "wight_armor2", "wight_armor3", "koa_sword_tier_5", "longclaw_sword", "ice_sword", "blackfish_shoulders",
            "blackfish_armor", "jon_snow_gorget", "jon_snow_boots", "stannis_cape", "stannis_armor", "baratheon_hammer",
            "jonsnow_armor", "jonsnow_vambraces", "oathkeeper_sword", "nightking_armor", "nightking_boots", "nightking_bracers",
            "nightking_blade", "hound_armor", "hound_boots", "melisandre_dress", "giant_garb", "giant_boots",
            "giant_handwraps", "giant_club", "cersei_crown", "cersei_dress", "margaery_dress", "dragonglass_axe",
            "needle", "tyrion_clothes", "tyrion_boots", "varys_clothes", "varys_shoes", "baelish_clothes",
            "baelish_boots", "mountain_sword", "val_steel_sword_2", "lady_forlorn", "val_steel_sword_3", "val_steel_sword_4",
            "val_steel_sword_5", "val_steel_sword_6", "val_steel_sword_7", "val_steel_sword_8", "mace_helmet", "mace_armor",
            "mace_bracers", "widows_wail", "dawn", "noble_default", "casterly_helmet2", "lightbringer",
            "ww2_sword", "wight_head4", "darksister", "brienne_armor", "blackfyre", "val_steel_sword_blue",
            "val_steel_sword_red", "weirwood_bow", "lyanna_armor", "lyanna_shoulders", "skull_sword", "dragonbone_bow",
            "celtigar_axe", "hightower_lord_armor", "viper_spear", "ned_clothes", "whyt_sword", "dany_sash",
            "jaime_clothes", "heartsbane", "vigilance_sword", "nightking_spikes", "giant_bow", "giant_arrows",
            "bronn_armor1", "bronn_armor2", "podrick_armor", "podrick_armor2", "red_rain", "white_walker_saddle",
            "plate_armor_reinforcements", "chain_armor_reinforcements", "assist_sword", "whitewalker_armor", "barristan_sword", "valyrian_surcoat_master",
            "valyrian_pauldrons_master", "goldenheart_longbow", "brightroar", "brightroar2", "jaime_leather", "jaime_leather2",
            "bolton_sword", "renly_armor", "renly_crown", "renly_boots", "renly_shoulders_cloak", "renly_gloves",
            "renly_clothes", "renly_sword", "lamentation", "truth", "nightfall", "tyrell_sword",
            "cersei_red_dress", "cersei_armor", "mountain_armor", "mountain_helmet", "mountain_pauldrons", "mountain_boots",
            "mountain_gloves", "mountain_gauntlets", "lady_forlorn2", "euron_axe", "hound_helmet", "hound_shoulders",
            "hound_gloves", "ravens_teeth_longbow", "ravens_teeth_arrows", "dothraki_hair", "blackfyre_plate", "rhaegar_plate",
            "rhaegar_plate2", "blackfyre_gauntlets", "rhaegar_gauntlets", "blackfyre_boots", "rhaegar_boots", "blackfyre_helmet",
            "rhaegar_helmet", "blackfyre_pauldrons", "rhaegar_pauldrons", "nightking_armor2", "brienne_plate", "loras_armor",
            "brienne_pauldrons", "loras_pauldrons", "brienne_helmet", "loras_helmet", "loras_helmet2", "brienne_boots",
            "loras_boots", "brienne_gloves", "loras_gloves", "ice_sword2", "whitewalker_armor2", "whitewalker_bracers2",
            "whitewalker_greaves", "gendry_hammer", "baratheon_crown", "euron_crown", "joffrey_crown", "margaery_crown",
            "robb_crown", "sansa_crown", "andal_civ_boots", "andal_civ_boots2", "bull_helmet", "ramsay_armor",
            "ramsay_helmet", "ramsay_gloves", "ramsay_shoulders", "ramsay_shoulders2", "ramsay_boots", "wildling_axe",
            "wildling_pickaxe", "andal_noble_bracelet", "westerling_axe",
        };
    }
}
