using System.Collections.Generic;

namespace Armoury
{
    /// <summary>
    /// Unikaty Realm of Thrones (spis: docs/ROT-UNIKATY.md, generowany tools/rot_uniques.py). O unikacie decyduje NAZWA
    /// widoczna w grze (Jeff 05.10): imie postaci, legenda/material z legend, korona albo legendarna cena autora.
    /// Uzywane przez ArmsPricing.IsUnique i UniqueSpoils: warsztaty ich nie robia, AI nie kupuje, wycena x prestiz.
    /// </summary>
    internal static class RotUniques
    {
        internal static readonly HashSet<string> Ids = new HashSet<string>
        {
            "dany_hair", "dany_dress", "dany_boots", "wildling_medium_armor", "ygritte_armor", "koa_sword_tier_5",
            "longclaw_sword", "ice_sword", "blackfish_shoulders", "blackfish_armor", "stannis_cape", "stannis_armor",
            "baratheon_hammer", "oathkeeper_sword", "nightking_armor", "nightking_boots", "nightking_bracers", "nightking_blade",
            "hound_armor", "hound_boots", "melisandre_dress", "giant_garb", "giant_boots", "giant_handwraps",
            "giant_club", "cersei_crown", "cersei_dress", "margaery_dress", "dragonglass_axe", "needle",
            "tyrion_clothes", "tyrion_boots", "varys_clothes", "varys_shoes", "baelish_clothes", "baelish_boots",
            "mountain_sword", "val_steel_sword_2", "lady_forlorn", "val_steel_sword_3", "val_steel_sword_4", "val_steel_sword_5",
            "val_steel_sword_6", "val_steel_sword_7", "val_steel_sword_8", "mace_helmet", "mace_armor", "mace_bracers",
            "widows_wail", "dawn", "lightbringer", "ww2_sword", "gold_cloak_armor", "gold_cloak_helmet",
            "gold_cloak_pauldrons", "gold_cloak_boots", "gold_cloak_bracers", "darksister", "brienne_armor", "blackfyre",
            "val_steel_sword_blue", "val_steel_sword_red", "weirwood_bow", "lyanna_armor", "lyanna_shoulders", "skull_sword",
            "dragonbone_bow", "celtigar_axe", "viper_spear", "ned_clothes", "whyt_sword", "heartsbane",
            "vigilance_sword", "nightking_spikes", "giant_bow", "giant_arrows", "bronn_armor1", "bronn_armor2",
            "podrick_armor", "red_rain", "white_walker_saddle", "assist_sword", "barristan_sword", "goldenheart_longbow",
            "brightroar", "brightroar2", "jaime_leather", "jaime_leather2", "bolton_sword", "renly_armor",
            "renly_crown", "renly_boots", "renly_shoulders_cloak", "renly_gloves", "renly_clothes", "renly_sword",
            "lamentation", "truth", "nightfall", "tyrell_sword", "cersei_red_dress", "cersei_armor",
            "mountain_armor", "mountain_helmet", "mountain_pauldrons", "mountain_boots", "mountain_gloves", "mountain_gauntlets",
            "lady_forlorn2", "euron_axe", "rot_horse_armor19", "hound_helmet", "hound_shoulders", "hound_gloves",
            "blackfyre_plate", "rhaegar_plate", "rhaegar_plate2", "blackfyre_gauntlets", "rhaegar_gauntlets", "blackfyre_boots",
            "rhaegar_boots", "blackfyre_helmet", "rhaegar_helmet", "blackfyre_pauldrons", "rhaegar_pauldrons", "nightking_armor2",
            "brienne_plate", "loras_armor", "brienne_pauldrons", "loras_pauldrons", "brienne_helmet", "loras_helmet",
            "loras_helmet2", "brienne_boots", "loras_boots", "brienne_gloves", "loras_gloves", "ice_spear",
            "ice_sword2", "whitewalker_armor2", "whitewalker_bracers2", "whitewalker_greaves", "gendry_hammer", "baratheon_crown",
            "euron_crown", "joffrey_crown", "margaery_crown", "robb_crown", "sansa_crown", "bull_helmet",
            "ramsay_armor", "ramsay_helmet", "ramsay_gloves", "ramsay_shoulders", "ramsay_shoulders2", "ramsay_boots",
            "fm_noble_dress1",
        };
    }
}
