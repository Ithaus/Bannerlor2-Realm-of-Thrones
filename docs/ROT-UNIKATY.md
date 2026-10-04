# Unikaty Realm of Thrones - spis (2026-10-04)

Jeff 04.10: "zrob audyt - tabele przedmiotow, ktore dodaje Gra o Tron, i wyklucz przedmioty unikatowe".
Zrodlo: wszystkie `<Item>`/`<CraftedItem>` uzbrojenia w `Modules/ROT*/ModuleData` (1460 sztuk). Kryteria unikatu:
(1) ROT oznacza je `is_merchandise="false"`; (2) autor wpisal recznie cene (>= 10 000 - zwykle przedmioty ROT nie maja ceny, liczy ja gra);
(3) id zaczyna sie od imienia postaci (Brienne, Ogar, Gora, Ramsay, Loras, Renly, Rhaegar...); (4) legenda albo material z legend
(stal valyrianska, weirwood, smocze szklo, smocza kosc, olbrzymy, Kruczy Zab); (5) korony.
NIE sa unikatami: stroje oddzialow rodow (stark_, mormont_, dayne_, royce_, lannister_...), zbroje kultury valyrian (valyrian_plate - stroj kultury),
zwykla bron z nazwa krainy (crownlands_halberd, spiked_mace).

Skutek w grze (Armoury `RotUniques.cs` -> `ArmsPricing.IsUnique`): warsztaty ich nie robia, AI ich nie kupuje na targu, wycena historyczna x prestiz (`HistUniquePrestige`).
Razem unikatow: 177.

## 1. Legendy i materialy z legend (48)

| id | nazwa | typ | kultura | powod |
|---|---|---|---|---|
| `assist_sword` | Despair | TwoHandedSword | - | poza handlem (ROT); cena wpisana przez autora: 300000; legenda/material: assist_sword |
| `blackfyre` | Blackfyre | TwoHandedSword | - | poza handlem (ROT); cena wpisana przez autora: 300000; legenda/material: blackfyre |
| `blackfyre_boots` | Blackfyre Sabatons | LegArmor | valyrian | legenda/material: blackfyre |
| `blackfyre_gauntlets` | Blackfyre Gauntlets | HandArmor | valyrian | legenda/material: blackfyre |
| `blackfyre_helmet` | Blackfyre Helmet | HeadArmor | valyrian | legenda/material: blackfyre |
| `blackfyre_pauldrons` | Blackfyre Pauldrons | Cape | valyrian | legenda/material: blackfyre |
| `blackfyre_plate` | Blackfyre Plate | BodyArmor | valyrian | legenda/material: blackfyre |
| `brightroar` | Brightroar | TwoHandedSword | - | poza handlem (ROT); cena wpisana przez autora: 300000; legenda/material: brightroar |
| `brightroar2` | Brightroar Silver | TwoHandedSword | - | poza handlem (ROT); cena wpisana przez autora: 300000; legenda/material: brightroar |
| `darksister` | Dark Sister | OneHandedSword | - | poza handlem (ROT); cena wpisana przez autora: 300000; legenda/material: darksister |
| `dragonbone_bow` | Dragonbone Bow | Bow | valyrian | cena wpisana przez autora: 150000; legenda/material: dragonbone |
| `dragonglass_axe` | Dragonglass Axe | TwoHandedAxe | freefolk | legenda/material: dragonglass |
| `giant_arrows` | Giant Arrows | Arrows | freefolk | legenda/material: giant |
| `giant_boots` | Giant Boots | LegArmor | freefolk | legenda/material: giant |
| `giant_bow` | Giant Bow | Bow | freefolk | cena wpisana przez autora: 500000; legenda/material: giant |
| `giant_club` | Giant Club | TwoHandedAxe | freefolk | legenda/material: giant |
| `giant_garb` | Giant King Armor | BodyArmor | freefolk | legenda/material: giant |
| `giant_handwraps` | Giant Handwraps | HandArmor | freefolk | legenda/material: giant |
| `goldenheart_longbow` | Goldenheart Longbow | Bow | summer | cena wpisana przez autora: 200000; legenda/material: goldenheart |
| `heartsbane` | Heartsbane | TwoHandedSword | - | poza handlem (ROT); cena wpisana przez autora: 300000; legenda/material: heartsbane |
| `ice_sword` | Ice | TwoHandedSword | battania | poza handlem (ROT); cena wpisana przez autora: 350000; legenda/material: ice_sword |
| `ice_sword2` | Ice Sword | OneHandedSword | whitewalker | legenda/material: ice_sword |
| `koa_sword_tier_5` | Valyrian Steel Sword type 1 | OneHandedSword | - | cena wpisana przez autora: 200000; legenda/material: koa_sword |
| `lady_forlorn` | Tempest | OneHandedSword | - | poza handlem (ROT); cena wpisana przez autora: 300000; legenda/material: lady_forlorn |
| `lady_forlorn2` | Lady Forlorn | OneHandedSword | - | poza handlem (ROT); cena wpisana przez autora: 300000; legenda/material: lady_forlorn |
| `lamentation` | Lamentation | OneHandedSword | - | poza handlem (ROT); cena wpisana przez autora: 300000; legenda/material: lamentation |
| `lightbringer` | Lightbringer | OneHandedSword | - | poza handlem (ROT); cena wpisana przez autora: 250000; legenda/material: lightbringer |
| `longclaw_sword` | Longclaw | TwoHandedSword | - | poza handlem (ROT); cena wpisana przez autora: 300000; legenda/material: longclaw |
| `nightfall` | Nightfall | TwoHandedSword | - | poza handlem (ROT); cena wpisana przez autora: 300000; legenda/material: nightfall |
| `oathkeeper_sword` | Oathkeeper | OneHandedSword | - | poza handlem (ROT); cena wpisana przez autora: 300000; legenda/material: oathkeeper |
| `ravens_teeth_arrows` | Ravens' Teeth Arrows | Arrows | river | legenda/material: ravens_teeth |
| `ravens_teeth_longbow` | Ravens' Teeth Longbow | Bow | river | legenda/material: ravens_teeth |
| `red_rain` | Red Rain | OneHandedSword | - | poza handlem (ROT); cena wpisana przez autora: 300000; legenda/material: red_rain |
| `truth` | Truth | OneHandedSword | - | poza handlem (ROT); cena wpisana przez autora: 280000; legenda/material: truth |
| `val_steel_sword_2` | Valyrian Steel Sword type 2 | OneHandedSword | - | cena wpisana przez autora: 200000; legenda/material: val_steel |
| `val_steel_sword_3` | Valyrian Steel Sword type 3 | OneHandedSword | - | cena wpisana przez autora: 200000; legenda/material: val_steel |
| `val_steel_sword_4` | Valyrian Steel Sword type 4 | OneHandedSword | - | cena wpisana przez autora: 200000; legenda/material: val_steel |
| `val_steel_sword_5` | Valyrian Steel Sword type 5 | OneHandedSword | - | cena wpisana przez autora: 200000; legenda/material: val_steel |
| `val_steel_sword_6` | Valyrian Steel Sword type 6 | OneHandedSword | - | cena wpisana przez autora: 200000; legenda/material: val_steel |
| `val_steel_sword_7` | Valyrian Steel Sword type 7 | OneHandedSword | - | cena wpisana przez autora: 200000; legenda/material: val_steel |
| `val_steel_sword_8` | Valyrian Steel Sword type 8 | TwoHandedSword | - | cena wpisana przez autora: 200000; legenda/material: val_steel |
| `val_steel_sword_blue` | Valyrian Steel Sword Blue | OneHandedSword | - | cena wpisana przez autora: 200000; legenda/material: val_steel |
| `val_steel_sword_red` | Valyrian Steel Sword Red | OneHandedSword | - | cena wpisana przez autora: 200000; legenda/material: val_steel |
| `vigilance_sword` | Vigilance | OneHandedSword | - | poza handlem (ROT); cena wpisana przez autora: 300000; legenda/material: vigilance_sword |
| `weirwood_bow` | Weirwood Bow | Bow | battania | cena wpisana przez autora: 150000; legenda/material: weirwood |
| `whyt_sword` | Orphanmaker | TwoHandedSword | - | cena wpisana przez autora: 250000; legenda/material: whyt |
| `widows_wail` | Liontooth | OneHandedSword | - | poza handlem (ROT); cena wpisana przez autora: 300000; legenda/material: widows_wail |
| `ww2_sword` | Widow's Wail | OneHandedSword | - | poza handlem (ROT); cena wpisana przez autora: 300000; legenda/material: ww2 |

## 2. Korony (8)

| id | nazwa | typ | kultura | powod |
|---|---|---|---|---|
| `baratheon_crown` | Robert's Crown | HeadArmor | stormlands | korona |
| `cersei_crown` | Cersei's Crown | HeadArmor | crownlands | poza handlem (ROT); postac/imienny: cersei; korona |
| `euron_crown` | Driftwood Crown | HeadArmor | sturgia | postac/imienny: euron; korona |
| `joffrey_crown` | Joffrey's Crown | HeadArmor | crownlands | postac/imienny: joffrey; korona |
| `margaery_crown` | Margaery's Crown | HeadArmor | reach | postac/imienny: margaery; korona |
| `renly_crown` | Renly Crown | HeadArmor | stormlands | postac/imienny: renly; korona |
| `robb_crown` | King in the North Crown | HeadArmor | battania | postac/imienny: robb; korona |
| `sansa_crown` | Sansa's Crown | HeadArmor | battania | postac/imienny: sansa; korona |

## 3. Rynsztunek imiennych postaci (102)

| id | nazwa | typ | kultura | powod |
|---|---|---|---|---|
| `baelish_boots` | Baelish Boots | LegArmor | baelish | poza handlem (ROT); postac/imienny: baelish |
| `baelish_clothes` | Baelish Clothes | BodyArmor | crownlands | poza handlem (ROT); postac/imienny: baelish |
| `baratheon_hammer` | Robert Baratheon's Hammer | TwoHandedAxe | stormlands | poza handlem (ROT); postac/imienny: baratheon_hammer |
| `barristan_sword` | Barristan Selmy Sword | OneHandedSword | - | poza handlem (ROT); postac/imienny: barristan |
| `blackfish_armor` | Blackfish Armor | BodyArmor | river | postac/imienny: blackfish |
| `blackfish_shoulders` | Blackfish Shoulders | Cape | river | postac/imienny: blackfish |
| `bolton_sword` | Bolton Sword | OneHandedSword | - | poza handlem (ROT); cena wpisana przez autora: 200000; postac/imienny: bolton_sword |
| `brienne_armor` | Brienne Armor | BodyArmor | crownlands | postac/imienny: brienne |
| `brienne_boots` | Brienne Plated Boots | LegArmor | stormlands | postac/imienny: brienne |
| `brienne_gloves` | Brienne Plated Gloves | HandArmor | stormlands | postac/imienny: brienne |
| `brienne_helmet` | Brienne Helmet | HeadArmor | stormlands | postac/imienny: brienne |
| `brienne_pauldrons` | Brienne Pauldrons | Cape | stormlands | postac/imienny: brienne |
| `brienne_plate` | Brienne Plate | BodyArmor | stormlands | postac/imienny: brienne |
| `bronn_armor1` | Bronn Leather Armor with Mail | BodyArmor | river | postac/imienny: bronn |
| `bronn_armor2` | Bronn Leather Armor | BodyArmor | crownlands | postac/imienny: bronn |
| `bull_helmet` | Gendry's Bull Helmet | HeadArmor | stormlands | poza handlem (ROT); postac/imienny: bull_helmet |
| `celtigar_axe` | Crab's Pincer | OneHandedAxe | dragonstone | poza handlem (ROT); postac/imienny: celtigar_axe |
| `cersei_armor` | Cersei's Armored Dress | BodyArmor | crownlands | poza handlem (ROT); postac/imienny: cersei |
| `cersei_dress` | Cersei's Dress | BodyArmor | crownlands | poza handlem (ROT); postac/imienny: cersei |
| `cersei_red_dress` | Cersei's Red Dress | BodyArmor | crownlands | poza handlem (ROT); postac/imienny: cersei |
| `dany_boots` | Danaerys Boots | LegArmor | ghiscari | poza handlem (ROT); postac/imienny: dany |
| `dany_dress` | Danaerys Dress | BodyArmor | ghiscari | poza handlem (ROT); postac/imienny: dany |
| `dany_hair` | Danaerys Hair | HeadArmor | ghiscari | poza handlem (ROT); postac/imienny: dany |
| `dany_sash` | Targaryen Sash | Cape | valyrian | postac/imienny: dany |
| `euron_axe` | Euron's Axe | OneHandedAxe | sturgia | poza handlem (ROT); cena wpisana przez autora: 300000; postac/imienny: euron |
| `gendry_hammer` | Gendry's Hammer | TwoHandedAxe | stormlands | poza handlem (ROT); postac/imienny: gendry |
| `hound_armor` | Hound Armor | BodyArmor | vlandia | postac/imienny: hound |
| `hound_boots` | Hound Boots | LegArmor | vlandia | postac/imienny: hound |
| `hound_gloves` | Hound Gauntlets | HandArmor | vlandia | postac/imienny: hound |
| `hound_helmet` | Hound Helmet | HeadArmor | vlandia | postac/imienny: hound |
| `hound_shoulders` | Hound Shoulders | Cape | vlandia | postac/imienny: hound |
| `jaime_clothes` | Noble Clothes | BodyArmor | crownlands | postac/imienny: jaime |
| `jaime_leather` | Jaime Northern Armor | BodyArmor | battania | postac/imienny: jaime |
| `jaime_leather2` | Jaime Northern Armor 2 | BodyArmor | battania | postac/imienny: jaime |
| `jon_snow_boots` | Northern Leather Boots | LegArmor | battania | postac/imienny: jon_snow |
| `jon_snow_gorget` | Stark Gorget | Cape | battania | postac/imienny: jon_snow |
| `jonsnow_armor` | Northern Elite Leathers | BodyArmor | battania | postac/imienny: jonsnow |
| `jonsnow_vambraces` | Northern Vambraces | HandArmor | battania | postac/imienny: jonsnow |
| `loras_armor` | Loras Tyrell Armor | BodyArmor | reach | postac/imienny: loras |
| `loras_boots` | Loras Tyrell Plated Boots | LegArmor | reach | postac/imienny: loras |
| `loras_gloves` | Loras Tyrell Plated Gloves | HandArmor | reach | postac/imienny: loras |
| `loras_helmet` | Loras Tyrell Helmet | HeadArmor | reach | postac/imienny: loras |
| `loras_helmet2` | Loras Tyrell Tournament Helmet | HeadArmor | reach | postac/imienny: loras |
| `loras_pauldrons` | Loras Tyrell Pauldrons | Cape | reach | postac/imienny: loras |
| `lyanna_armor` | Lyanna Mormont Armor | BodyArmor | battania | postac/imienny: lyanna |
| `lyanna_shoulders` | Lyanna Mormont Shoulders | Cape | battania | postac/imienny: lyanna |
| `mace_armor` | Mace Tyrell Armor | BodyArmor | reach | postac/imienny: mace |
| `mace_bracers` | Mace Tyrell Bracers | HandArmor | reach | postac/imienny: mace |
| `mace_helmet` | Mace Tyrell Helmet | HeadArmor | reach | postac/imienny: mace |
| `margaery_dress` | Margaery's Dress | BodyArmor | reach | poza handlem (ROT); postac/imienny: margaery |
| `melisandre_dress` | Melisandre Dress | BodyArmor | dragonstone | poza handlem (ROT); postac/imienny: melisandre |
| `mountain_armor` | Mountain Armor | BodyArmor | vlandia | postac/imienny: mountain |
| `mountain_boots` | Mountain Boots | LegArmor | vlandia | postac/imienny: mountain |
| `mountain_gauntlets` | Mountain Gauntlets | HandArmor | vlandia | postac/imienny: mountain |
| `mountain_gloves` | Mountain Gloves | HandArmor | vlandia | postac/imienny: mountain |
| `mountain_helmet` | Mountain Helmet | HeadArmor | vlandia | postac/imienny: mountain |
| `mountain_pauldrons` | Mountain Pauldrons | Cape | vlandia | postac/imienny: mountain |
| `mountain_sword` | Gregor Clegane's Sword | TwoHandedSword | - | poza handlem (ROT); cena wpisana przez autora: 175000; postac/imienny: mountain |
| `ned_clothes` | Ned Stark Clothes | BodyArmor | battania | postac/imienny: ned |
| `needle` | Needle | OneHandedSword | battania | poza handlem (ROT); postac/imienny: needle |
| `nightking_armor` | Night King Armor | BodyArmor | looters | poza handlem (ROT); postac/imienny: nightking |
| `nightking_armor2` | Night King Armor with Spikes | BodyArmor | looters | poza handlem (ROT); postac/imienny: nightking |
| `nightking_blade` | White Walker Blade | TwoHandedPolearm | looters | poza handlem (ROT); postac/imienny: nightking |
| `nightking_boots` | White Walker Boots | LegArmor | looters | poza handlem (ROT); postac/imienny: nightking |
| `nightking_bracers` | White Walker Bracers | HandArmor | looters | poza handlem (ROT); postac/imienny: nightking |
| `nightking_spikes` | Night King Spikes | HeadArmor | looters | poza handlem (ROT); postac/imienny: nightking |
| `podrick_armor` | Podrick Leather Armor | BodyArmor | westerlands | postac/imienny: podrick_armor |
| `podrick_armor2` | Dragonstone Leather Armor | BodyArmor | dragonstone | postac/imienny: podrick_armor |
| `ramsay_armor` | Ramsay Armor | BodyArmor | battania | postac/imienny: ramsay |
| `ramsay_boots` | Ramsay Sabatons | LegArmor | battania | postac/imienny: ramsay |
| `ramsay_gloves` | Ramsay Plated Gloves | HandArmor | battania | postac/imienny: ramsay |
| `ramsay_helmet` | Ramsay Helmet | HeadArmor | battania | postac/imienny: ramsay |
| `ramsay_shoulders` | Ramsay Pauldrons | Cape | battania | postac/imienny: ramsay |
| `ramsay_shoulders2` | Ramsay Pauldrons with Skin | Cape | battania | postac/imienny: ramsay |
| `renly_armor` | Renly Armor | BodyArmor | stormlands | postac/imienny: renly |
| `renly_boots` | Renly Boots | LegArmor | stormlands | postac/imienny: renly |
| `renly_clothes` | Renly Clothes | BodyArmor | stormlands | postac/imienny: renly |
| `renly_gloves` | Renly Gloves | HandArmor | stormlands | postac/imienny: renly |
| `renly_shoulders_cloak` | Renly Shoulders and Cloak | Cape | stormlands | postac/imienny: renly |
| `renly_sword` | Renly Baratheon's Sword | OneHandedSword | - | poza handlem (ROT); cena wpisana przez autora: 180000; postac/imienny: renly |
| `rhaegar_boots` | Rhaegar Sabatons | LegArmor | valyrian | postac/imienny: rhaegar |
| `rhaegar_gauntlets` | Rhaegar Gauntlets | HandArmor | valyrian | postac/imienny: rhaegar |
| `rhaegar_helmet` | Rhaegar Helmet | HeadArmor | valyrian | postac/imienny: rhaegar |
| `rhaegar_pauldrons` | Rhaegar Pauldrons | Cape | valyrian | postac/imienny: rhaegar |
| `rhaegar_plate` | Rhaegar Plate | BodyArmor | valyrian | postac/imienny: rhaegar |
| `rhaegar_plate2` | Rhaegar Plate 2 | BodyArmor | valyrian | postac/imienny: rhaegar |
| `skull_sword` | Skull Sword | OneHandedSword | - | cena wpisana przez autora: 150000; postac/imienny: skull_sword |
| `stannis_armor` | Stannis Armor | BodyArmor | dragonstone | postac/imienny: stannis |
| `stannis_cape` | Stannis Cape | Cape | dragonstone | postac/imienny: stannis |
| `tyrell_sword` | Tyrell Sword | OneHandedSword | - | poza handlem (ROT); cena wpisana przez autora: 200000; postac/imienny: tyrell_sword |
| `tyrion_boots` | Tyrion's Boots | LegArmor | ghiscari | poza handlem (ROT); postac/imienny: tyrion |
| `tyrion_clothes` | Tyrion's Clothes | BodyArmor | ghiscari | poza handlem (ROT); postac/imienny: tyrion |
| `varys_clothes` | Varys Clothes | BodyArmor | crownlands | poza handlem (ROT); postac/imienny: varys |
| `varys_shoes` | Varys Shoes | LegArmor | crownlands | poza handlem (ROT); postac/imienny: varys |
| `viper_spear` | Red Viper Spear | TwoHandedPolearm | aserai | poza handlem (ROT); postac/imienny: viper_spear |
| `westerling_axe` | Westerling Axe | OneHandedAxe | vlandia | poza handlem (ROT); postac/imienny: westerling_axe |
| `white_walker_saddle` | White Walker Saddle | HorseHarness | wights | postac/imienny: white_walker |
| `whitewalker_armor` | Whitewalker Armor | BodyArmor | whitewalker | postac/imienny: whitewalker |
| `whitewalker_armor2` | White Walker Pteruges | BodyArmor | whitewalker | postac/imienny: whitewalker |
| `whitewalker_bracers2` | White Walker Leather Bracers | HandArmor | whitewalker | postac/imienny: whitewalker |
| `whitewalker_greaves` | White Walker Leather Greaves | LegArmor | whitewalker | postac/imienny: whitewalker |
| `ygritte_armor` | Ygritte's Fur Armor | BodyArmor | freefolk | postac/imienny: ygritte |

## 4. Rzadkie (cena wpisana przez autora ROT) (4)

| id | nazwa | typ | kultura | powod |
|---|---|---|---|---|
| `andal_noble_bracelet` | Andal Noble Bracelet | HandArmor | crownlands | cena wpisana przez autora: 10000 |
| `casterly_helmet2` | Casterly Rock Helmet 2 | HeadArmor | vlandia | cena wpisana przez autora: 10000 |
| `dawn` | Dawn | TwoHandedSword | - | poza handlem (ROT); cena wpisana przez autora: 300000 |
| `hightower_lord_armor` | Hightower Lord Armor | BodyArmor | reach | cena wpisana przez autora: 14000 |

## 5. Poza handlem w ROT (stroje, fryzury, umarli) (15)

| id | nazwa | typ | kultura | powod |
|---|---|---|---|---|
| `andal_civ_boots` | Andal Shoes | LegArmor | crownlands | poza handlem (ROT) |
| `andal_civ_boots2` | Andal Shoes 2 | LegArmor | river | poza handlem (ROT) |
| `chain_armor_reinforcements` | Chain Armor Reinforcements | Cape | bolton | poza handlem (ROT) |
| `dothraki_hair` | Dothraki Default | HeadArmor | khuzait | poza handlem (ROT) |
| `dual_blades` | Dual Blades | OneHandedWeapon | - | poza handlem (ROT) |
| `dual_blades2` | Dual Wield Blade Left | OneHandedWeapon | - | poza handlem (ROT) |
| `noble_default` | Noble Default | HeadArmor | battania | poza handlem (ROT) |
| `plate_armor_reinforcements` | Plate Armor Reinforcements | Cape | dragonstone | poza handlem (ROT) |
| `valyrian_pauldrons_master` | Valyrian Master Pauldrons | Cape | valyrian | poza handlem (ROT) |
| `valyrian_surcoat_master` | Valyrian Master Surcoat | BodyArmor | valyrian | poza handlem (ROT) |
| `wight_armor2` | Wight Armor 2 | BodyArmor | looters | poza handlem (ROT) |
| `wight_armor3` | Wight Armor 3 | BodyArmor | looters | poza handlem (ROT) |
| `wight_head4` | Rusted Northern Helmet | HeadArmor | looters | poza handlem (ROT) |
| `wildling_axe` | Wildling Axe | OneHandedAxe | freefolk | poza handlem (ROT) |
| `wildling_pickaxe` | Wildling Pickaxe | OneHandedAxe | freefolk | poza handlem (ROT) |
