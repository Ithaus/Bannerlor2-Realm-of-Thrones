# MAPA: dane przedmiotów (RBM 4.3.5 + ROT 8.1.8) — pancerze, broń, „ile ciosów do zabicia"

Audyt tylko do odczytu, 11.11. Skróty ścieżek:
- `RC` = `scratchpad/bad3/dec/rbmcombat.cs` (dekompilacja RBMCombat; ten sam kod jest w `scratchpad/bad4/rbm.cs`, numery linii podaję z `RC`)
- `RB` = `scratchpad/bad3/dec/rbm.cs` (RBM.dll), `ROT` = `scratchpad/rot_all.cs` (ROT.dll)
- `MOD` = `...\Mount & Blade II Bannerlord\Modules`
- `DUMP` = `Documents\Mount and Blade II Bannerlord\CrashScribe\items-dump.csv` (11.11 13:27, 4038 pozycji, wartości W GRZE)
- skrypty i wyniki pośrednie: `scratchpad/bad4/dp/` (`load.py`, `dump.py`, `b1.py`, `b2.py`, `t1.py`, `t2.py`, `w1.py`, `w3.py`, `sim.py`, `run.py`, `pairs.py`, `sim_out.txt`, `troops.json`, `crafting.cs`)

## 0. Werdykt w pięciu zdaniach

1. Zbroje ROT **przechodzą przez przeliczenie** — ale nie RBM, tylko własne ROT („RBM Integration", u Jeffa włączone), które mnoży wartości z XML stałym mnożnikiem wg typu i materiału. Potwierdzone w `DUMP`: 1156 z 1175 sprawdzonych pozycji zgadza się co do punktu.
2. Mnożniki ROT są wyższe niż własna skala RBM, a wartości bazowe ROT są wyższe niż vanilla, więc **cała drabina ROT stoi o klasę wyżej**: skóra ROT (mediana 61) > kolczuga RBM (47), kolczuga ROT (88) > płyta RBM (79), płyta ROT (120) to górny koniec RBM (maks. 105).
3. Skutek we wzorach RBM: w wojskach ROT **żadna broń ręczna nie przebija tułowia przeciwnika z tego samego tieru** (próg przebicia miecza T1–T6: pancerz 12–32, włóczni 31–57; tułów ROT T1–T6: 33–115). Całe obrażenia to „tępy uraz" liczony jednym wzorem, więc skóra T2 (61) i płyta T6 (115) różnią się tylko ok. 1,5x w liczbie ciosów (10 vs 15), a niskie tiery ROT giną 2–4x wolniej niż w skali, pod którą RBM stroił wzory (włócznia T1 na T1: 6 ciosów zamiast 2; T2: 8 zamiast 3). Od T5 różnicy już nie ma.
4. Za to kończyny i szyja ROT są dziurawe: 98% T1, 85% T2 i 69% T3 nie ma rękawic (ręce 0), hełmy ROT nie mają wartości „szyi" (1 z 245), nogi <30 u 58–100% wojsk. Ten sam miecz T2 robi 7 obrażeń w tułów (15 ciosów) i 46 w rękę (3 ciosy); włócznia T4 w szyję rycerza ROT zabija jednym pchnięciem (120), rycerza w sprzęcie RBM rani za 36.
5. Największe anomalie: suknie i szaty lordów z pancerzem płyty (Daenerys 132, Cersei 110, Varys 99 przy 3–4 kg), skóry Dothraków 72–81 przy 4 kg, Unsullied 92–99 przy 6 kg, „włosy Daenerys" jako hełm 150, konie ROT z pancerzem 7x RBM, strzały „barbed" niskich tierów praktycznie nieszkodliwe od T2 w górę, a znacznik `Plate` daje niemal pełną odporność na strzały.

## 1. Jak dane trafiają do gry (trzy warstwy)

### 1.1 RBM podmienia przedmioty vanilla po `id`
- `RBM.XmlLoadingPatches.MergeTwoXmlsPatch.Prefix` (`RB`:1316-1456): gdy doklejany XML ma komentarz `RBM_XML_TAG`, z wcześniejszego dokumentu usuwane są `Item`/`CraftedItem`/`ItemModifier`/`NPCCharacter` o tym samym `id`, potem doklejane są wersje RBM. To nie XSLT, tylko podmiana po `id`.
- Pliki: `MOD\RBM\ModuleData\RBMCombat_body_armors.xml` (246), `_head_armors` (255+1), `_shoulder_armors` (91), `_arm_armors` (32), `_leg_armors` (39), `_shields` (128), `_horse_armors` (34), `_horses` (50), `_ranged` + `_arrow_visuals` (łuki 21, kusze 13, strzały 39, bełty 24, proce), części broni (`_sword_blades`, `_axe_pieces`, `_mace_pieces`, `_couched_lances`: 340 z 805 części vanilla, w tym **wszystkie 289 głowni**).
- 105 pozycji RBM to nowe `id`; reszta to nadpisane vanilla. Przedmioty ROT mają własne `id` (1511 pozycji; pokrywa się tylko `scale_armor` i 24 konie) — RBM ich nie rusza.

### 1.2 ROT przelicza własne przedmioty mnożnikami („RBM Integration")
- Przełącznik: `ROT.ROTSettings.RBMIntegration` (`ROT`:6865), u Jeffa `Configs\ModSettings\Global\RealmOfThrones\RealmOfThrones_v1.json:50` = `true`.
- `ROT.HarmonyPatches.ModCompatibility.RBM.XmlFoo` (`ROT`:17567-17591): każdy węzeł `Weapon`/`Armor`/`NPCCharacter`/`BladeData` z plików w `ROT-Content` dostaje atrybut `isROT`.
- `DeserializeArmorComponent` (`ROT`:17484-17565) + `BuildDictionary` (`ROT`:17658-17706): wszystkie cztery pola pancerza × mnożnik (obcięcie do int):

| typ | Cloth | Leather | Chainmail | Plate |
|---|---|---|---|---|
| BodyArmor | 0,7 | 2,2 | 2,1 | 2,4 |
| HeadArmor | 1,0 | 1,25 | 1,5 | 2,5 |
| Cape (naramienniki) | 1,1 | 1,6 | 1,7 | 4,1 |
| HandArmor | 1,0 | 1,3 | 1,5 | 2,35 |
| LegArmor | 1,0 | 1,5 | 2,1 | 2,35 |
| HorseHarness | 1,0 (potem osobny rozkład na głowę/nogi, linie 17517-17563) | | | |

- Hełm bez `body_armor` dostaje `body_armor = head/2` (`ROT`:17511-17516) — w RBM to pancerz **twarzy**.
- Głownie ROT: `damage_factor × 0,4` (`DeserializeBladeData`, `ROT`:17593-17610). Tarcze ROT: HP × 1,75 (drewno) / × 4 (metal) (`ROT`:17612-17635).
- Łuki „lore" nadpisywane refleksją w `ROTRBMCompatibility.ModifyBowsAndArrows` (`ROT`:32152+): dragonbone 180 lb, weirwood 170, goldenheart 200, ravens_teeth 210, giant_bow 500 lb.
- **Masy nie są przeliczane** — zostają z XML ROT.

### 1.3 RBM czyta pola pancerza inaczej niż vanilla
`RBMCombat.ArmorRework.GetBaseArmorEffectivenessForBodyPartRBM` (`RC`:461-511) zastępuje `Agent.GetBaseArmorEffectivenessForBodyPart` (`RC`:14-21):

| strefa trafienia | skąd wartość | linie `RC` |
|---|---|---|
| klatka, brzuch | `body_armor` zbroi (tylko slot Body) | 178-218 |
| barki | naramiennik (`body`+`arm`) + `arm_armor` zbroi | 137-154 |
| ręce | **tylko** `arm_armor` rękawic | 234-246 |
| nogi | 0,5 × (`leg` butów + `leg` zbroi) | 276-292 |
| głowa | `head_armor` hełmu | 23-32 |
| szyja | `arm_armor` hełmu + `arm_armor` zbroi | 45-61 |
| twarz (trafienie z przodu) | `body_armor` hełmu | 3585-3636 |
| pod pachą | `arm_armor` zbroi (+ naramiennika) | 3606-3656 |

RBM-owe XML są pod ten podział ręcznie strojone (hełmy z kolczym czepcem mają `arm_armor`, zamknięte przyłbice mają wysokie `body_armor`). ROT stosuje jeden mnożnik na wszystko.

### 1.4 Ustawienia RBM u Jeffa
`Documents\...\Configs\RBM\config.xml` = wartości domyślne (`MOD\RBM\DefaultConfigDONOTEDIT.xml`) poza drobiazgami: `ArmorMultiplier 2`, `ArmorThresholdModifier 1`, `MaceBluntModifier 1`, `BluntTraumaBonus 0`, `ThrustMagnitudeModifier 0,05`, `TroopOverhaulActive 1`, postura i stamina włączone. Progi wg klasy broni: cięcie ×5 (buława ×4, strzała ×2,6), pchnięcie ×3,5 miecz / ×3 drzewcowa / ×2,5 topór / ×2 buława, strzała, bełt.

## 2. Skala pancerza — wartości w grze (`DUMP`)

Format: min / p25 / **mediana** / p75 / maks.

### 2.1 Zbroja (slot Body), pole `body` = klatka i brzuch

| materiał | vanilla (XML) | RBM (w grze) | ROT (XML) | ROT (w grze) | ROT/RBM mediana |
|---|---|---|---|---|---|
| Cloth | 0/4/**5**/9/36 | 0/7/**8**/12/28 (n=108) | 10/10/**20**/20/50 | 7/7/**14**/14/35 (n=80) | 1,75 |
| Leather | 1/8/**12**/16/42 | 0/12/**18**/24/48 (n=55) | 10/15/**28**/38/80 | 19/33/**61**/83/176 (n=171) | **3,4** |
| Chainmail | 0/25/**30**/34/53 | 0/44/**47**/55/93 (n=45) | 20/37/**42**/45/99 | 42/77/**88**/94/207 (n=51) | **1,9** |
| Plate | 22/33/**39**/48/57 | 32/55/**79**/90/105 (n=37) | 30/47/**50**/53/57 | 72/112/**120**/127/136 (n=115) | **1,5** |

Dla porównania mnożnik RBM względem vanilla (te same `id`, mediana): Cloth ×1,47, Leather ×1,44, Chainmail ×1,64, Plate ×1,88. ROT bierze 2,2 / 2,1 / 2,4 i startuje z wyższej bazy (skóra ROT w XML 28 vs vanilla 12).

### 2.2 Pozostałe części (mediany w grze)

| część | pole | RBM Cloth/Leather/Chain/Plate | ROT Cloth/Leather/Chain/Plate |
|---|---|---|---|
| zbroja: barki (`arm`) | arm | 6 / 12 / 38 / 35 | 4 / 19 / 23 / 24 |
| zbroja: nogi (`leg`) | leg | 8 / 19 / 33 / 38 | 4 / 17 / 23 / 26 |
| hełm | head | 10 / 23 / 39 / **85** (maks. 144) | 5 / 35 / 46 / **95** (maks. 200) |
| hełm: twarz | body | 0 / 0 / 0 / 12 (maks. 116) | 2 / 17 / 23 / **47** (zawsze head/2) |
| hełm: szyja | arm | 122 z 183 płytowych ma >0 (mediana 15, maks. 46) | **1 z 245** ma >0 |
| naramiennik | body+arm | 7 / 18 / 30 / **30** (maks. 88) | 13 / 31 / 42 / **97** (maks. 139) |
| rękawice | arm | 26 / 26 / 48 / 44 | – / 27 / 33 / 56 |
| buty | leg | 26 / 29 / 43 / 46 | 5 / 30 / 44 / 58 |
| tarcza HP | – | 345 / **520** / 2000 | 525 / **735** / 2000 |
| zbroja konia `body` | – | 0 / **10** / 50 | 15 / **71** / 132 |

### 2.3 Masy

| | RBM | ROT |
|---|---|---|
| zbroja Leather | mediana 2,3 kg | 9 kg |
| zbroja Chainmail | 10,6 kg | 18 kg |
| zbroja Plate | 19,4 kg | **34 kg** (15–40) |
| zbroja konia | 11 kg (maks. 130) | **140 kg** (50–145) |
| komplet T6 (zbroja+hełm+rękawice+buty+naramiennik) | 39,7 kg | 42 kg |
| komplet T4 | 19,7 kg | 21 kg |

Masa kompletu wojska wychodzi podobnie, bo ROT ma ciężkie zbroje, ale rzadko daje rękawice i ciężkie buty.

## 3. Wojska: tier → pancerz (z XML wojsk + wartości z `DUMP`)

ROT: `MOD\ROT-Content\ModuleData\ROT-Troops.xml`, 866 jednostek bojowych, tier = `ceil((poziom-5)/5)`. RBM: `MOD\RBM\ModuleData\RBMCombat_unit_overhaul.xml`, 215 jednostek Calradii (skala, pod którą RBM stroił wzory). Kolumny = mediany stref wg tabeli z 1.3.

| tier | ROT klatka p10/med/p90 | RBM klatka p10/med/p90 | ROT barki | RBM barki | ROT ręce | RBM ręce | ROT nogi | RBM nogi | ROT głowa | RBM głowa | ROT skill wręcz (med.) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| T1 | 33/**33**/33 | 10/**10**/10 | 11 | 9 | **0** | 22 | 14 | 15 | **0** | 18 | 20 |
| T2 | 10/**61**/63 | 18/**22**/30 | 26 | 24 | **0** | 24 | 17 | 24 | 14 | 31 | 40 |
| T3 | 42/**61**/83 | 18/**32**/47 | 25 | 33 | **0** | 30 | 22 | 27 | 75 | 57 | 70 |
| T4 | 61/**83**/115 | 38/**50**/79 | 24 | 51 | 26 | 42 | 26 | 36 | 90 | 80 | 100 |
| T5 | 77/**92**/122 | 50/**79**/96 | 95 | 69 | 27 | 48 | 27 | 43 | 92 | 102 | 140 |
| T6 | 83/**115**/132 | 81/**99**/105 | 115 | 85 | 51 | 56 | 29 | **67** | 112 | 129 | 240 |

Materiał zbroi wojsk ROT: T1–T3 prawie sama „Leather" (107/112, 147/186, 163/214), T4 97 skóra / 71 kolczuga / 57 płyta, T5 71/70/91, T6 15/13/42. Bez rękawic: T1 98%, T2 85%, T3 69%, T4 40%, T5 11%, T6 4%. Bez hełmu: T1 85%, T2 41%. Nogi < 30: T1 100%, T2 96%, T3 95%, T4 84%, T5 77%, T6 58%.

## 4. Broń — dane

### 4.1 Co RBM robi z bronią
- Obrażenia cięcia/obucha **nie biorą się z „obrażeń" broni**. `OverrideDamageCalc.Prefix` (`RC`:3511-3774) bierze surową magnitudę fizyczną (`CalculateBaseMeleeBlowMagnitudePatch`, `RC`:676-843 → `CombatStatCalculator.CalculateStrikeMagnitudeForSwing`, `scratchpad/bad4/combatstat.cs`), a `Utilities.GetSkillBasedDamage` (`RC`:5831-6109) zamienia ją na `clamp(surowa + skill_DR × k, min×(1+s), maks×(1+2s)) × 4,6`, gdzie `s = skill/250`, `skill_DR = 600×skill/(600+skill)`.
- `damage_factor` głowni działa tylko jako `√f` obniżające próg przebicia (`RC`:3744-3756 i 6207): próg = pancerz × mnożnik klasy / √f.
- Pchnięcie = energia w dżulach z masy broni, masy ręki i prędkości pchnięcia (`Utilities.CalculateThrustMagnitudeForOneHandedWeapon/TwoHandedWeapon`, `RC`:6312-6376), sufit 180 J (1H) / 250 J (2H).

### 4.2 Współczynniki głowni i masy (broń noszona przez wojska ROT; min/mediana/maks)

| klasa | RBM `f` cięcie | ROT `f` cięcie (po ×0,4) | RBM masa kg | ROT masa kg | RBM dł. cm | ROT dł. cm | użyć RBM / ROT |
|---|---|---|---|---|---|---|---|
| miecz 1H | 0,40/**0,80**/1,20 | 1,44/**1,72**/2,00 | 0,85/**1,11**/1,59 | 1,20/**1,40**/1,73 | 104 | 91 | 542 / 142 |
| topór 1H | 0,30/**0,70**/1,20 | 1,16/**1,60**/1,76 | 0,45/**0,75**/1,02 | 1,20/**1,30**/1,40 | 65 | 60 | 171 / 25 |
| buława 1H | 0,30/**0,50**/1,30 | 1,32/**1,56**/1,60 | 0,40/**0,48**/1,16 | **2,10** | 54 | 62 | 78 / 17 |
| drzewcowa 1H (pchnięcie) | 0,40/**0,90**/1,15 | 1,20/**1,20**/1,60 | 0,60/**1,20**/3,74 | **1,70** | 167 | 170 | 368 / 71 |
| drzewcowa 2H | 1,10 cięcie, 0,80 pchn. | 1,44 cięcie, 0,80 pchn. | 2,72 | 2,00 | 238 | 143 | 44 / 28 |
| topór 2H | 1,00/**1,10**/1,30 | 1,64/**1,68**/4,12 | 1,17 | 1,80 | 115 | 104 | 21 / 17 |
| miecz 2H | 0,80 | 1,52 | 1,59 | 1,90 | 109 | 114 | 13 / 8 |

Całość części: RBM cięcie 0,20/**0,90**/1,70 (n=217), pchnięcie 0,20/**0,80**/1,40; ROT po ×0,4: cięcie 0,96/**1,80**/2,80 (n=162), obuch 1,32/**1,60**/4,12, pchnięcie 0,56/**1,24**/4,00. RBM względem vanilla: mediana ×0,27 (cięcie), ×0,34 (pchnięcie); ROT wziął ×0,4 od bazy wyższej niż vanilla (ROT cięcie w XML mediana 4,5 vs vanilla 3,6).

Skutek: głownia ROT ma `f` ok. 2x RBM, czyli próg przebicia niższy o ok. 30% (√1,72 = 1,31 vs √0,80 = 0,89). Broń ROT jest też cięższa (miecz +26%, topór +73%, buława ×4,4).

Skrajne: `giant_club` f=4,12, `ice_spear` pchnięcie f=4,00 (przebija do pancerza ok. 120 — tematycznie na miejscu), `nightking_blade` 2,80, `baratheon_hammer` 2,60, miecze valyriańskie 2,20–2,24.

### 4.3 Łuki i strzały
- RBM: `missile_speed` łuku/kuszy = naciąg w funtach, `thrust_damage` = 100; strzała `thrust_damage` 100 (bodkin, Pierce) albo 115 (barbed, Cut). Magnituda = energia kinetyczna × (suma−100)/100 (`MagnitudeChanges.CalculateMissileMagnitude`, `RC`:1426-1528), prędkość z naciągu i masy strzały (`Utilities.calculateMissileSpeed`, `RC`:5466-5543).
- Łuki wojsk ROT (wg tieru, mediana naciągu): T2 90 lb, T3 90, T4 100, T5 125, T6 200; kusze T4 230, T5 300.
- Strzały: T1–T3 głównie `default_arrows` (barbed 50 g, **Cut**), T4+ bodkiny (Pierce). `ROT.…RBM.BuildMap` (`ROT`:17637-17656) podmienia strzały wg łuku dla 14 łuków vanilla; najczęstszy łuk T2–T3 `highland_ranger_bow` (40 użyć) **nie jest na liście** i zostaje z barbed.
- Łuki lore ROT mają po nadpisaniu `ThrustDamage` 84–97 zamiast 100: dragonbone 180 lb daje współczynnik 0,84 — słabszy niż zwykły łuk o tym samym naciągu.

## 5. Ile ciosów do zabicia (wzory RBM, HP 100, trafienie w klatkę ×0,9)

Wzór końcowy (`Utilities.RBMComputeDamage` + `WeaponTypeDamage`, `RC`:6111-6310):
- przebicie = max(0, magnituda − pancerz × próg_klasy / √f)
- tępy uraz = magnituda × wsp_klasy × (część zatrzymana) × 100/(100 + 2×pancerz); wsp: miecz cięcie 0,25, topór 0,30, pchnięcie 0,35, **obuch 0,70**, strzała 0,15
- × mnożnik strefy (`GetBodyPartDamageMultiplier`, `RC`:4185-4236): klatka 0,9, brzuch 1,0, głowa/szyja 1,5, barki 0,6–0,7, ręce i nogi 0,5–0,7.

Skąd liczby w symulacji (`dp/sim.py`, `dp/run.py`, `dp/pairs.py`, wynik `dp/sim_out.txt`):
- **zamach**: magnituda nominalna broni = `swing` z `DUMP` / `f` (gra liczy `SwingDamage = najlepsza magnituda × damage_factor`, `TaleWorlds.Core.Crafting.CalculateSwingBaseDamage`, `dp/crafting.cs`:363-375). Mediany dla broni noszonej przez wojska: miecz 1H RBM 16,0 / ROT 17,4; topór 1H 14,3 / 19,3; buława 8,0 / 21,8; topór 2H 32,7 / 36,3; drzewcowa 2H cięcie 31,8 / 42,4; miecz 2H 26,2 / 30,9. W bitwie RBM mnoży prędkość ×0,83 (topór 2H ×0,75) × (1 + skill_DR/800 lub /1000), a magnituda rośnie z kwadratem prędkości. To jest **cios pełnym zamachem w najlepszy punkt głowni** — górna granica; cios słabszą częścią albo na początku zamachu daje mniej.
- **pchnięcie**: wprost ze wzorów RBM; prędkość pchnięcia z modelu (bezwładność przyjęta szacunkowo), bo `thrust` w `DUMP` jest spłaszczony łatką RBM (`CalculateStrikeMagnitudeForThrustPatch`, `RC`:881-898) i nie nadaje się do kalibracji.
- skill wg tieru: mediana skilla wręcz wojsk ROT (20/40/70/100/140/240).

### 5.1 Magnitudy wg tieru atakującego

| broń | T1 | T2 | T3 | T4 | T5 | T6 |
|---|---|---|---|---|---|---|
| miecz 1H RBM, cięcie | 65 | 79 | 97 | 115 | 136 | 180 |
| miecz 1H ROT, cięcie | 70 | 83 | 102 | 120 | 141 | 186 |
| topór 1H RBM / ROT | 56 / 72 | 66 / 83 | 80 / 98 | 93 / 112 | 108 / 128 | 141 / 163 |
| topór 2H RBM / ROT | 102 / 112 | 119 / 129 | 142 / 152 | 163 / 174 | 189 / 201 | 243 / 257 |
| buława RBM 0,48 kg / ROT 2,1 kg | 50 / 78 | 53 / 87 | 59 / 100 | 64 / 111 | 72 / 125 | 94 / 154 |
| glewia 2H ROT, cięcie | 130 | 148 | 169 | 189 | 213 | 263 |
| włócznia 1H pchnięcie (J) RBM / ROT | 97 / 104 | 108 / 114 | 123 / 129 | 138 / 143 | 157 / 161 | 180 / 180 |
| miecz 1H pchnięcie (J) RBM / ROT | 87 / 92 | 98 / 102 | 113 / 118 | 129 / 133 | 149 / 152 | 180 / 180 |
| pika 2H RBM pchnięcie (J) | 83 | 94 | 110 | 127 | 148 | 200 |

Udział fizyki broni w cięciu mieczem: 11,6–16,3 punktu z 14–39 przed mnożnikiem 4,6 (T1: 82%, T4: 54%, T6: 42%); reszta to skill.

**Najwyższy pancerz, który broń jeszcze przebija** (magnituda × √f / próg klasy), T1→T6:

| broń | T1 | T2 | T3 | T4 | T5 | T6 |
|---|---|---|---|---|---|---|
| miecz 1H RBM (f 0,80), cięcie | 12 | 14 | 17 | 20 | 24 | 32 |
| miecz 1H ROT (f 1,72), cięcie | 18 | 22 | 27 | 31 | 37 | 49 |
| topór 2H ROT (f 1,68) | 29 | 33 | 40 | 45 | 52 | 67 |
| buława ROT (obuch, próg ×5) | 20 | 22 | 25 | 28 | 31 | 38 |
| włócznia 1H RBM (f 0,90), pchnięcie | 31 | 34 | 39 | 44 | 50 | 57 |
| włócznia 1H ROT (f 1,20), pchnięcie | 38 | 42 | 47 | 52 | 59 | 66 |
| **tułów wojsk ROT (mediana)** | **33** | **61** | **61** | **83** | **92** | **115** |
| **tułów wojsk RBM/Calradia (mediana)** | **10** | **22** | **32** | **50** | **79** | **99** |

W skali RBM włócznia przebija rówieśnika do T3 włącznie, miecz tylko T1. W skali ROT nie przebija nic (wyjątek: włócznia ROT na T1). Strzały: bodkin 125 lb przebija płytę do 61 i nie-płytę do 122; longbow 180 lb: płyta 81, nie-płyta 163; kusza 300 lb: płyta 113.

### 5.2 Obrażenia / ciosy do zabicia — atakujący T4 (skill 100)

| broń | goło 0 | płótno 14 | RBM skóra 18 | **ROT T1 33** | RBM kolczuga 47 | **ROT T2–T3 61** | RBM płyta 79 | **ROT T4 83** | **ROT T5 92** | **ROT T6 115** | ROT maks. 136 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| miecz 1H RBM cięcie | 102/1 | 45/3 | 28/4 | 15/7 | 12/9 | 10/10 | 9/12 | 9/12 | 9/12 | 7/15 | 6/17 |
| miecz 1H ROT cięcie | 108/1 | 69/2 | 57/2 | 16/7 | 13/8 | 11/10 | 9/12 | 9/12 | 9/12 | 8/13 | 7/15 |
| topór 1H RBM | 82/2 | 25/4 | 18/6 | 14/8 | 12/9 | 10/10 | 9/12 | 9/12 | 8/13 | 7/15 | 6/17 |
| topór 1H ROT | 99/2 | 62/2 | 50/2 | 18/6 | 15/7 | 13/8 | 10/10 | 10/10 | 9/12 | 9/12 | 7/15 |
| topór 2H RBM | 145/1 | 99/2 | 85/2 | 29/4 | 22/5 | 19/6 | 16/7 | 16/7 | 15/7 | 12/9 | 11/10 |
| topór 2H ROT | 156/1 | 118/1 | 108/1 | 62/2 | 23/5 | 20/5 | 18/6 | 17/6 | 16/7 | 13/8 | 12/9 |
| buława RBM | 57/2 | 31/4 | 29/4 | 24/5 | 20/5 | 18/6 | 15/7 | 14/8 | 13/8 | 11/10 | 10/10 |
| buława ROT | 99/2 | 76/2 | 68/2 | 41/3 | 36/3 | 31/4 | 27/4 | 26/4 | 24/5 | 20/5 | 18/6 |
| włócznia 1H RBM pchn. | 124/1 | 94/2 | 85/2 | 49/3 | 21/5 | 18/6 | 16/7 | 16/7 | 15/7 | 12/9 | 10/10 |
| włócznia 1H ROT pchn. | 128/1 | 103/1 | 95/2 | 63/2 | 33/4 | 19/6 | 17/6 | 16/7 | 15/7 | 13/8 | 11/10 |
| pika 2H RBM pchn. | 113/1 | 82/2 | 73/2 | 35/3 | 19/6 | 17/6 | 15/7 | 14/8 | 13/8 | 11/10 | 9/12 |
| miecz 1H RBM pchn. | 115/1 | 79/2 | 68/2 | 24/5 | 20/5 | 18/6 | 15/7 | 14/8 | 13/8 | 11/10 | 10/10 |
| glewia 2H ROT cięcie | 169/1 | 129/1 | 117/1 | 68/2 | 26/4 | 22/5 | 18/6 | 18/6 | 17/6 | 15/7 | 13/8 |

### 5.3 Atakujący T1 (skill 20) i T6 (skill 240) — skrót

| broń | T1: goło | T1: ROT T1 33 | T1: ROT T2 61 | T1: ROT T4 83 | T6: ROT T1 33 | T6: RBM kolczuga 47 | T6: ROT T2 61 | T6: ROT T4 83 | T6: ROT T6 115 | T6: maks. 136 |
|---|---|---|---|---|---|---|---|---|---|---|
| miecz 1H RBM cięcie | 58/2 | 8/13 | 6/17 | 5/20 | 24/5 | 20/5 | 18/6 | 14/8 | 11/10 | 10/10 |
| miecz 1H ROT cięcie | 62/2 | 9/12 | 6/17 | 5/20 | 71/2 | 27/4 | 18/6 | 15/7 | 12/9 | 10/10 |
| topór 2H ROT | 99/2 | 18/6 | 13/8 | 10/10 | 136/1 | 92/2 | 47/3 | 25/4 | 20/5 | 18/6 |
| buława ROT | 70/2 | 29/4 | 21/5 | 18/6 | 69/2 | 49/3 | 43/3 | 36/3 | 28/4 | 25/4 |
| włócznia 1H RBM pchn. | 87/2 | 18/6 | 13/8 | 10/10 | 87/2 | 52/2 | 25/4 | 20/5 | 17/6 | 14/8 |

(pełne tabele T1–T6 dla 14 broni: `dp/sim_out.txt`)

### 5.4 Tier na tier — obrażenia / ciosy w tułów (pełny zamach)

| broń, układ | T1 | T2 | T3 | T4 | T5 | T6 |
|---|---|---|---|---|---|---|
| miecz RBM, wojska **ROT** (33/61/61/83/92/115) | 8/13 | 7/15 | 9/12 | 9/12 | 9/12 | 11/10 |
| miecz RBM, wojska **RBM** (10/22/32/50/79/99) | 18/6 | 11/10 | 12/9 | 12/9 | 11/10 | 13/8 |
| włócznia RBM, wojska **ROT** | 18/6 | 14/8 | 17/6 | 16/7 | 17/6 | 17/6 |
| włócznia RBM, wojska **RBM** | 66/2 | 49/3 | 38/3 | 21/5 | 18/6 | 18/6 |
| topór 2H ROT, wojska **ROT** | 18/6 | 15/7 | 18/6 | 17/6 | 18/6 | 20/5 |
| topór 2H ROT, wojska **RBM** | 73/2 | 54/2 | 45/3 | 23/5 | 20/5 | 22/5 |
| buława ROT, wojska **ROT** | 29/4 | 24/5 | 27/4 | 26/4 | 27/4 | 28/4 |
| buława ROT, wojska **RBM** | 54/2 | 37/3 | 37/3 | 34/3 | 29/4 | 32/4 |

Odczyt: w skali ROT liczba ciosów jest **płaska przez wszystkie tiery** (miecz 10–15, włócznia 6–8, buława 4–5), bo pancerz i skill rosną razem, a nic nie przebija. W skali RBM niskie tiery giną od włóczni w 2–3 ciosach, wysokie w 5–6.

Strefy poza tułowiem (pełny zamach / pchnięcie):
- miecz T2 w rękę: pancerz 0 (ROT T1–T3) → **46** obr. (3 ciosy); pancerz 24 (RBM T2) → 7. W tułów ROT T2 (61) → 7.
- miecz T4 w rękę: pancerz 0 → **68** (2 ciosy); 26–30 → 10.
- włócznia T4 w szyję: 24 (rycerz ROT) → **120** (jedno pchnięcie); 50 (rycerz RBM) → 36; w głowę 112 → 21.
- włócznia T4 w nogę: 29 (ROT T6) → 33; 67 (RBM T6) → 10.
- premia staminy ofiary ×1,3 (rozdz. 7 pkt 6): miecz T4 na ROT T4 83 → 108: 9 → 8 obr.

### 5.5 Strzały (pełna energia przy wylocie; na dystansie mniej)

| pocisk | mag. | goło | ROT T1 33 (skóra) | RBM kolczuga 47 | ROT T2–T3 61 (skóra) | RBM płyta 79 | ROT T4 83 (skóra) | ROT T5 92 (kolczuga) | ROT T6 115 (płyta) |
|---|---|---|---|---|---|---|---|---|---|
| łuk 60 lb, barbed 50 g (Cut) | 72 | 63/2 | 5/20 | 4/25 | 3/34 | 3/34 | 3/34 | 2/50 | 2/50 |
| łuk 90 lb, barbed 50 g (Cut) | 90 | 81/2 | 10/10 | 5/20 | 5/20 | 4/25 | 4/25 | 3/34 | 3/34 |
| łuk 100 lb, bodkin 60 g | 101 | 90/2 | 63/2 | 52/2 | 40/3 | 4/25 | 21/5 | 14/8 | 3/34 |
| łuk 125 lb, bodkin 70 g | 122 | 108/1 | 82/2 | 71/2 | 59/2 | 6/17 | 40/3 | 33/4 | 4/25 |
| longbow 180 lb, 125 g | 163 | 145/1 | 119/1 | 108/1 | 96/2 | 11/10 | 77/2 | 69/2 | 6/17 |
| kusza 120 lb, 150 g | 103 | 91/2 | 65/2 | 54/2 | 42/3 | 4/25 | 23/5 | 15/7 | 3/34 |
| kusza 300 lb, 150 g | 227 | 203/1 | 177/1 | 165/1 | 153/1 | 70/2 | 135/1 | 127/1 | 9/12 |
| giant bow 500 lb, 300 g | 875 | 786/1 | 760/1 | 748/1 | 737/1 | 652/1 | 718/1 | 710/1 | 589/1 |

Reguła materiału (`RC`:6113-6116): dla strzał i bełtów typu Pierce pancerz inny niż `Plate` liczy się w połowie. Stąd skok: „skóra" 83 i kolczuga 92 dają 3–4 strzały, płyta 79 (niższa!) 17, płyta 115 — 25.

## 6. Anomalie

### 6.1 Nie do przebicia / zawyżone
| przedmiot | materiał | w grze body/arm/leg | masa | XML | uwaga |
|---|---|---|---|---|---|
| `valyrian_surcoat_master` | Chainmail | 207/207/207 | 2 kg | `ROTassets.xml:4769` (99/99/99) | wszystko 207, odporny na wszystko prócz giant bow |
| `giant_garb` | Leather | 176/44/44 | 50 kg | :1060 (80) | olbrzym — zapewne celowe |
| `nightking_armor`, `nightking_armor2` | Leather | 154/66/66 | 2 kg | :961 (70) | Inni — celowe, ale jako „skóra" strzały liczą połowę |
| `whitewalker_armor` | Leather | 121/44/44 | 2 kg | | jw. |
| **`dany_dress`** | Leather | **132/55/33** | 3 kg | :188 (60/25/15) | suknia = pełna płyta (płyta ROT mediana 120) |
| **`cersei_dress`, `margaery_dress`, `melisandre_dress`, `cersei_armor`** | Leather | 110/55/33 | 3–3,5 kg | :1139 (50) | jw. |
| **`varys_clothes`, `tyrion_clothes`, `baelish_clothes`** | Leather | 99/33/33 | 4 kg | :1248 (45) | szaty dworzan = płyta RBM p75 |
| **`unsullied_armor`, `unsullied_soldier_armor`** | Leather | 99 i 92 /22/22 | **6 kg** | :56 (45) | pancerz płyty przy masie przeszywanicy |
| **`dothraki_armor`…`dothraki_armor6`, Bloodrider** | Leather | 72–81 / 4 / 22–26 | **4 kg** | :7565 (37) | uprząż skórzana = płyta RBM (79) |
| ok. 45 „skór" T4 (`pirate_armor1-3`, `stark_armor_1-4`, `ranger_armor`, `nightswatch_armor2`, `lorathi_armor`, `qarth_armor`…) | Leather | 81–85 /17–26/17–26 | 12–18 kg | np. :14032 (38) | masowy sprzęt T4 na poziomie płyty RBM |
| `dany_hair` | Plate (hełm) | head 150, twarz 75 | 0,2 kg | :181 (60) | włosy jako hełm lepszy od każdego hełmu RBM (maks. 144) |
| `nightking_spikes` | Plate | head 200 | 1,6 kg | | |
| naramienniki płytowe ROT (148 szt.) | Plate | body+arm 53/**97**/139 | 1–8 kg | | ×4,1 na OBA pola; RBM ma `arm`=0 i medianę 30 → barki ROT ok. 2x RBM |
| zbroje koni ROT (79 szt.) | – | body 15/**71**/132 | 140 kg | | RBM 0/**10**/50; koń ROT ok. 7x lepiej chroniony, 13x cięższa zbroja |
| `armored_dragon_saddle` | Plate | 1000/300/1000/1000 | 80 kg | `ROT`:17708 | celowe (smok) |

### 6.2 Papierowe / dziury
| co | liczby | skutek |
|---|---|---|
| ręce wojsk ROT T1–T3 | pancerz 0 u 98% / 85% / 69% (brak rękawic; `arm_armor` zbroi RBM liczy do barków, nie do rąk) | cięcie w rękę 2–3 ciosy przy 12–15 w tułów |
| szyja w hełmach ROT | `arm_armor` > 0 w 1 z 245 hełmów płytowych (RBM: 122 z 183) | szyja = tylko `arm` zbroi (ok. 24) przy mnożniku 1,5; rycerz RBM ma ok. 50 |
| nogi T6 | mediana 29 (RBM 67) | noga rycerza ROT ma pancerz rekruta |
| twarz | zawsze head/2: otwarty kapalin daje twarzy 45–47, zamknięty hełm 73–100; RBM: otwarte 0–12, zamknięte do 116 | otwarte hełmy za mocne, zamknięte za słabe |
| głowa T1–T2 | bez hełmu 85% / 41% | głowa ×1,5, pancerz 0 |
| strzały barbed (Cut) | 3–10 obr. przy pancerzu ≥33 | łucznicy T1–T3 z `default_arrows` (i `highland_ranger_bow` poza mapą podmian) prawie nieszkodliwi dla wojsk ROT od T1 w górę (10–34 strzały) |
| buława RBM | 0,40–0,48 kg (mediana broni noszonej), magnituda nominalna 8 (buława ROT 21,8) | do T5 obrażenia w całości z dolnego ograniczenia klasy; buława ROT bije 1,7x mocniej |
| łuki lore ROT | `ThrustDamage` 84–97 | dragonbone 180 lb słabszy od zwykłego 180 lb o 16% |
| płótno ROT | ×0,7 → mediana 14 | jedyna klasa ROT zgodna ze skalą RBM (8) |

### 6.3 Niespójności danych między źródłami
- **Skala**: mnożniki zbroi ROT (2,2/2,1/2,4) vs faktyczny stosunek RBM/vanilla (1,44/1,64/1,88) — zawyżenie 53% / 28% / 28%, a baza skóry ROT jest 2,3x vanilla.
- **Materiał jako przełącznik**: `material_type` decyduje o (a) mnożniku ROT, (b) regule strzał ×0,5, (c) cenie RBM (5/15/80/120 za punkt, `ItemValuesTiers.OverrideCalculateValue`, `RC`:3077-3160). ROT znakuje „Leather" brygantyny (`brigandine1`, `nightswatch_brigandine`), zbroje łuskowe, suknie i szaty — dostają pancerz płyty, wrażliwość skóry na strzały i cenę skóry (wzór RBM: skóra 83/22/22 = ok. 3,9 tys., płyta `common_armor` 100/21/21 = 34 736).
- **Tier** (`OverrideCalculateArmorTier`, `RC`:3327-3362: body×0,05 + leg×0,035 + arm×0,025 − 1): skóry ROT 81–85 wychodzą T4–T5, tak jak kolczugi i lekkie płyty.
- **Reguły RBM po nazwie przedmiotu** nie łapią ROT: lista zbroi „płyta pod pachą" (`RC`:3648: „Brigandine over Hauberk", „Khan's Coat of Plates"…) i „Closed" w nazwie hełmu (`RC`:3626) dotyczą nazw vanilla. Reguła „mail w nazwie → barki jak kolczuga" (`RC`:156-176) łapie np. „Stark Knight Armor with Mail".
- **Masa broni**: ROT 1,3–2,1 kg przy RBM 0,5–1,1 kg; w jednej armii te same klasy broni mają dwie skale mas i dwie skale `f`.
- 19 pozycji `DUMP` nie zgadza się z mnożnikiem: korony (`joffrey_crown`, `robb_crown`… XML 75, w grze 10) i `andal_clothes1-4` (22 → 19) — coś jeszcze je zmienia po wczytaniu (nie ustaliłem co; w repo pola pancerza pisze `Armoury/src/Uniques.cs:56-59` i `CrashScribe/src/Mends.cs:2666-2669`).

## 7. Czy to ma sens, czy jest uproszczone (w zakresie danych)

**Ma sens:** podział na strefy z osobnymi polami, próg przebicia zależny od klasy broni i jakości głowni, energia strzały z naciągu i masy, masa zbroi jako osobna dana, pancerz modyfikowany procentowo przez stan (`RBMCombat_item_modifiers.xml`: −50%…+15%, `ModifyArmorPatch`, `RC`:598-620).

**Uproszczone / rozjechane:**
1. Jeden mnożnik ROT na typ×materiał zamiast strojenia sztuka po sztuce — stąd suknie-płyty i dziury w szyi.
2. Powyżej progu przebicia pancerz działa już tylko przez `100/(100+2×pancerz)`: 83 → 0,376, 136 → 0,269. Między „skórą" T4 a najlepszą płytą jest 28% różnicy, a prawie całe wojsko ROT od T2 jest powyżej progu.
3. Cięcie: fizyka broni (masa, długość, wyważenie) daje 12–16 punktów, skill dokłada 3–23 (`skill_DR × 0,133`), całość × 4,6 w widełkach klasy. U rekruta decyduje broń (82%), u rycerza T6 skill (58%). Buława RBM 0,48 kg ma tak małą magnitudę (6–8), że do T5 jej obrażenia w całości wyznacza dolne ograniczenie klasy (`10 × (1+s) × 4,6`).
4. Obuch ma próg ×5 jak cięcie (`RC`:6223) — buława nie „przebija" nigdy, wygrywa stałym 0,70 zamiast 0,25.
5. „Częściowe przebicie" w pchnięciu (`RC`:6266-6283) algebraicznie się znosi (`num7` odjęte i dodane) — parametr `partialPenetrationThreshold` nic nie zmienia.
6. Przy włączonej staminie pancerz ofiary × (0,3 + max(0,7; stamina)) = ×1,0–1,3 (`RC`:3711-3723): wypoczęty ma +30% pancerza, czyli ROT T4 83 → 108. Tabele w rozdz. 5 liczą ×1,0.

## 8. Czego nie udało się ustalić

- **Zamach** liczony z `swing`/`f` z `DUMP` — to cios pełnym zamachem w najlepszy punkt; rozkładu rzeczywistych trafień (punkt na głowni, faza zamachu, ruch) nie znam, więc liczby ciosów w rozdz. 5 to dolna granica. `swing` w `DUMP` jest liczbą całkowitą (dla broni RBM 2–22), więc magnituda nominalna lekkich broni RBM ma błąd zaokrąglenia do ok. 10%.
- **Pchnięcie**: bezwładności i środka masy broni nie ma w `DUMP`; prędkość pchnięcia z modelu przybliżonego (energia ±15%).
- **Faktyczny ekwipunek w bitwie**: tabela tierów jest z XML wojsk; DynamicTroopEquipment i nasze zbrojownie podmieniają sprzęt w locie, a `TroopOverhaulActive=1` podmienia wojska vanilla. Nie sprawdzałem w logu bitwy, co żołnierze mają na sobie.
- **HP**: przyjąłem 100; nie sprawdzałem perków, ROT ani naszych zmian HP.
- Nie uruchamiałem gry — „w grze" znaczy „w `items-dump.csv` z 11.11 13:27". Wzory przeliczone ręcznie, nie potwierdzone logiem obrażeń.
- Klasyfikacja łuk / long_bow w RBM (inna sprawność) i spadek energii strzały z dystansem — poza zakresem; strzały liczone przy wylocie.
- Co zmienia korony i `andal_clothes` po wczytaniu (19 rozbieżności).
- 386 pozycji `dp*` w `DUMP` z zerowym pancerzem — nie ustaliłem, z którego moda pochodzą.
- Tarcze: porównane tylko HP; pancerz tarczy i wzór `RBMComputeBlowDamageOnShield` (`RC`:3776+) nie analizowane. Zbroje koni: tylko rozkład wartości.
- Nie sprawdzałem, czy mnożnik ROT obejmuje przedmioty ROT ładowane spoza katalogu `ROT-Content` (ROT_Hideouts, BKROTPatch) — znacznik `isROT` zależy od ścieżki pliku.
