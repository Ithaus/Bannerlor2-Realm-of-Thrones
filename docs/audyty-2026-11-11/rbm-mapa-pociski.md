# MAPA: pociski, tarcze, postura w RBM (audyt, tylko czytanie)

Zrodla (numery linii "src:N" = moja dekompilacja `bad4/rbm-pociski-src.cs`: RBMCombat + RBMConfig + RBMAI.Stance/PostureDamage/StanceLogic; plik `bad4/rbm.cs` innego agenta ma inne numery):
- RBM DLL z 21.08: `Modules/RBM/bin/Win64_Shipping_Client/RBMCombat.dll, RBMAI.dll, RBMConfig.dll`
- konfiguracja Jeffa: `Documents/Mount and Blade II Bannerlord/Configs/RBM/config.xml` (wersja 2)
- XML: `Modules/RBM/ModuleData/RBMCombat_ranged.xml, RBMCombat_arrow_visuals.xml, RBMCombat_shields.xml`, `Modules/ROT-Content/ModuleData/ROTassets.xml, ROT-Troops.xml`
- silnik: `rally_dec/mb_full.cs` (Mission.MissileHitCallback 57760-57990, RegisterBlow 57411), `bad4/wcd.cs` (WeaponComponentData), `bad4/itemobject.cs`
- kolejnosc ladowania (LauncherData.xml): ... RBM, RBM_WS, ROT-Core, ROT-Content ... => przy tym samym id wygrywa ROT. ROT ma JEDEN plik przedmiotow (ROTassets.xml, 1273 pancerze, 6 lukow, 2 strzaly, ~55 tarcz); reszta sprzetu wojsk ROT to id vanilli nadpisane przez RBM.

Ustawienia Jeffa majace znaczenie: RBMCombat=1, Posture=1, Stamina=1, PlayerPostureMultiplier=0 (=x1.0), ArmorMultiplier=2, ArmorThresholdModifier=1, BluntTraumaBonus=0, ThrustMagnitudeModifier=0.05 (=> OneHanded/TwoHandedThrustDamageBonus=20), RealisticRangedReload=1, RealisticArrowArc=0, PassiveShoulderShields=0. Arrow/Bolt: prog Pierce 2, prog Cut 2.6, obuch 0.15. Javelin: prog 3/3, obuch Pierce 0.2, Cut 0.05. ThrowingAxe: 2.5/4, 0.2/0.3. ThrowingKnife: 3/5, 0.15.

## 1. Energia pocisku

### 1.1 Luk / kusza: predkosc wylotowa - `RBMCombat.Utilities.calculateMissileSpeed` (src:4454)
W RBM atrybut `missile_speed` luku/kuszy to NACIAG W FUNTACH (dw), nie predkosc. `thrust_damage` luku = 100 (stala).
- E = 0.5 * (dw * 4.448 N) * skok * sprawnosc; v0 = floor( sqrt( 2E / (m_strzaly + dw * c) ) )
- "bow": skok 0.635 m, sprawnosc 0.90, c = 0.00015 kg/lb
- "long_bow": 0.635 m, 0.835, c = 0.00018
- "crossbow"/"crossbow_fast": 0.508 m, 0.88, c = 0.00015
- modyfikator przedmiotu: dw += ModifyHitPoints(50)-50 (src:3559-3563)
- wpiecie: `RangedRework.OverrideOnAgentShootMissile.Prefix` (src:3496): wektor predkosci jest ZASTEPOWANY: kierunek * (v0 + rzut predkosci strzelca na kierunek) (src:3588-3595). Skutki:
  - stopien naciagniecia cieciwy nie ma znaczenia (kazdy strzal = pelny naciag);
  - umiejetnosc lucznika nie wplywa na energie; jedyny prog: skill < dw + 9 => flaga UnloadWhenSheathed (nie utrzyma strzaly na cieciwie; src:3338, 3701) - zadnej kary do celnosci ani energii za zbyt mocny luk;
  - konny strzelajacy w galopie do przodu dostaje +predkosc konia (12 m/s przy 58 m/s = +46% energii).
- MissileSpeed luku RBM podmienia na v0 tylko miedzy prefiksem a postfiksem strzalu, potem wraca naciag (src:3596, 3629).

Naciagi RBM (RBMCombat_ranged.xml): luki 60 (hunting_bow) ... 200 (noble_long_bow); kusze 80-330 (crossbow_a 120 "fast", crossbow_f 330). Strzaly (RBMCombat_arrow_visuals.xml): masa 0.03-0.125 kg, bolty 0.05-0.15 kg.

### 1.2 Groty - sa, przez typ obrazen i `thrust_damage` strzaly
- "Bodkin" = Pierce, thrust_damage 100: bodkin_arrows_a/b/c (90/70/50 g), piercing_arrows 110 g, vlandic 80 g, steppe 40 g, heavy_steppe 60 g, bolt_a..e.
- "Broadhead" = Cut, thrust_damage 115: default_arrows 50 g, barbed_arrows 60 g, range_arrows 40 g, tournament.
- Mnoznik energii: `MagnitudeChanges.CalculateMissileMagnitude` (src:1424): missileTotalDamage (luk 100 + strzala) - 100, * 0.01 => bodkin x1.00, broadhead x1.15. (Suma luk+strzala liczona natywnie - WNIOSEK z ukladu liczb, nie widzialem kodu.)
- W wojskach ROT (ROT-Content, zliczenie id): default_arrows 336, range_arrows 77, bodkin_b 58, vlandic 53, bodkin_a 45, bodkin_c 36, steppe 32, barbed 30, piercing 22; bolt_a 267. Luki: glen_ranger_bow 105 (100 lb), hunting_bow 84 (60), steppe_bow 71 (90), highland_ranger 48 (90), steppe_heavy 46 (125), mountain_hunting 42 (70), composite 39 (100), woodland_longbow 16 (180), noble_long_bow 2 (200); kusze: crossbow_a 254 (120 lb), reszta < 15.

### 1.3 Sila trafienia (magnitude) - `ComputeBlowMagnitudeMissilePatch` (src:845) -> `CalculateMissileMagnitude` (src:1424)
- predkosc = |v_ofiary(2D) - v_pocisku| w chwili trafienia (ruch ofiary liczony fizycznie)
- KE = 0.5 * m * v^2, m = waga sztuki amunicji (ItemObject.Weight)
- limity KE: strzala m*2250 (v <= 67 m/s), belt m*2500 (70.7 m/s), proca m*3000, oszczep m*300 (24.5 m/s)
- strzala/belt/proca: magnitude = KE * mnoznik_grotu * momentumRemaining [J]
- oszczep: od v odejmuje sie 3 m/s (throwableCorrectionSpeed, min 5); Pierce: KE*0.05, Cut: KE, inne KE*0.5 (Pierce x20 wraca w RBMComputeDamage => efektywnie KE w J)
- topor do rzucania: KE; noz/sztylet rzucony: KE*0.05*0.6 (x20 potem => 0.6 KE)
- kamien/glaz: KE * thrust_damage*0.01

### 1.4 Opor powietrza i spadek z dystansem
- `RangedRework.MissionLoadChangeParameters` (src:3284): AirFrictionArrow 0.0015 (vanilla 0.003), AirFrictionJavelin 0.00215 (0.002), AirFrictionAxe/Knife 0.01 (0.007), MissileMinimumDamageToStick 12.5, MakesRearAttackDamageThreshold 13.
- Sam lot jest NATYWNY (kodu nie ma w DLL). Przyjalem dv/dx = -k*v (hamowanie ~ v^2): po 20 m v x0.970 (KE x0.94), po 100 m v x0.861 (KE x0.74). To zalozenie - patrz "nie ustalono".
- Proca: AirFrictionBullet - RBM nie rusza.
- Opcja RealisticArrowArc (u Jeffa 0): graczowi podnosi lufe o 0.083 rad (src:3598).

### 1.5 Bron miotana - `Utilities.calculateThrowableSpeed` (src:4532), `assignThrowableMissileSpeed` (src:4543)
- v = ceil( sqrt( (clamp(m*70, 60, 250) + skillDR*0.75) * 2 / m ) ), skillDR = 600*s/(600+s)
- x tarcza: duza 0.87, mala 0.96; x sqrt(clamp(1 - (pancerz barkow-20 + pancerz rak-20)*0.005 + min(0.2, skillDR*0.001), 0.7, 1)) - ciezki pancerz rak/barkow zmniejsza zasieg rzutu
- +3 m/s korekty (odejmowane z powrotem przy obrazeniach)
- kamien reczny: stale 25 m/s. Proca: `assignSlingMissileSpeed` (src:4561), energia 730*(1+skillDR/100)*m*naciag*0.01, clamp 60-350 J.
- AI w bitwie polowej dostaje poprawke kata rzutu oszczepem/toporem (src:3500-3538).

## 2. Przebijanie pancerza - `DamageRework.OverrideDamageCalc.Prefix` (src:2512) -> `Utilities.RBMComputeDamage` (src:5099) -> `WeaponTypeDamage` (src:5192)

Dla pocisku (IsMissile) NIE ma dodatku od umiejetnosci (GetSkillBasedDamage tylko dla walki wrecz, src:2687).
1. Pancerz A = pancerz trafionej czesci ciala (`ArmorRework.GetBaseArmorEffectivenessForBodyPartRBM`, src:459), po perkach (CalculateAdjustedArmorForBlow).
2. Ofiara lezy / odrzucona: A x0.75 (src:2671).
3. Stamina (wlaczona u Jeffa, src:2706-2726): sila strzelca x Lerp(0.85, 1, stamina%) ; pancerz ofiary x (0.3 + max(0.7, stamina%)) => WYPOCZETA ofiara ma pancerz x1.3, zmeczona (<=70% staminy) x1.0.
4. Strzala/belt Pierce w pancerz inny niz Plate (Cloth, Leather, Chainmail): A x0.5 (src:5101). Broadhead (Cut) - bez tej ulgi.
5. Przebicie: pen = max(0, mag - A * prog), prog Pierce 2.0, Cut 2.6 (oszczep 3/3). Dzielnik progu = sqrt(ThrustDamageFactor) = 1 dla strzal/boltow/oszczepow z XML (wcd.cs:471-478); oszczep z flaga BonusAgainstShield: x3 (prog /3).
6. Obuch przez pancerz: mag * 0.15 * (czesc zatrzymana) * 100/(100 + A*ArmorMultiplier(2)).
7. x czesc ciala (`GetBodyPartDamageMultiplier`, src:3186): glowa/szyja 1.5, brzuch 1.0, piers 0.9, barki 0.6, rece/nogi 0.5 (Pierce) / 0.6 (Cut); x trudnosc; floor.
8. Trafienie w twarz (src:2586-2637; kierunek ciosu przeciwny do spojrzenia < -0.75 i normalna.z < 0): pancerz = `body_armor` HELMU (tak RBM koduje oslone twarzy), a bez helmu A x0.15. Trafienie pod pache (src:2607): pancerz = arm_armor zbroi + plaszcza.
9. Odbicie strzaly (silnik 57909): obrazenia < 12.5 i pochloniete > 25 => strzala odbija sie i lamie.

MARTWY KOD: "czesciowe przebicie do 15 pkt" (partialPenetrationThreshold = 0 dla Arrow/Bolt/SlingStone/Mace, src:5256-5268) - num7 jest odejmowane i dodawane z powrotem, wynik identyczny dla 0 i 2. Reklamowana mechanika nie dziala.

Skutek ksztaltu wzoru: obrazenia to "klif". Ponizej progu A*prog zostaje 3-7 pkt obuchu, powyzej - prawie cala nadwyzka energii. Brak losowosci, brak kata padania (rykoszet), brak roznicy miedzy miejscami na tej samej czesci ciala poza twarza/pacha.

### 2.1 Jak liczy sie pancerz czesci ciala (wazne dla ROT)
- piers/brzuch = body_armor zbroi (plaszcz sie NIE liczy) (src:176-217)
- barki = body+arm plaszcza + arm_armor zbroi (src:135)
- rece = arm_armor TYLKO rekawic (src:232); nogi = 0.5*leg butow + 0.5*leg zbroi (src:274)
- szyja = arm_armor helmu + arm_armor zbroi (src:43); glowa = head_armor helmu
- kon: bez rzedu 0; z rzedem wartosc +10 (src:83-134)

Mediany wartosci: RBM (RBMCombat_body_armors.xml): Plate body 77 (32-135), Chainmail 47, Leather 18, Cloth 8; helm Plate head 85 (do 150), helm body_armor (twarz) mediana 12, do 116.
ROT (ROTassets.xml): Plate body 50 (30-57), Chainmail 42 (20-99), Leather 28 (10-80), Cloth 20; helm Plate head 39 (25-80); body_armor ma 13 z 245 helmow plytowych (5-10); arm_armor helmu: 1 sztuka; rekawice 21-24; buty Plate 25.
=> zbroje ROT sa w skali ROT/vanilli, nie RBM: plyta ROT (50) jest niewiele lepsza od kolczugi ROT (42) i o 1/3 slabsza od plyty RBM; twarz w helmie ROT = pancerz 0-10 (helm zamkniety nie chroni twarzy); szyja ROT ~10 przy mnozniku x1.5.

### 2.2 Przyklady (piers x0.9; ofiara wypoczeta = pancerz x1.3 / w nawiasie zmeczona x1.0; HP czlowieka 100)

| strzelec | dyst. | v, mag | bez panc. | skora ROT 28 | kolczuga ROT 42 | plyta ROT 50 | plyta RBM 77 | plyta RBM 110 | helm ROT 39 (glowa) | twarz (0) |
|---|---|---|---|---|---|---|---|---|---|---|
| glen_ranger 100 lb + default (broadhead 50 g) | 20 m | 56 m/s, 91 | 81 | 6 (22) | 5 (6) | 4 (5) | 3 | 2 | 9 | 136 |
| to samo | 100 m | 50, 72 | 63 | 5 | 4 | 3 | 2 | 1 | 7 | 107 |
| glen_ranger 100 lb + bodkin_b 70 g | 20 m | 49.5, 86 | 76 | 47 (54) | 32 (43) | 4 (5) | 3 | 2 | 9 (21) | 128 |
| to samo | 100 m | 44, 67 | 60 | 31 (37) | 16 (26) | 3 | 2 | 1 | 7 | 101 |
| steppe_heavy 125 lb + bodkin_a 90 g | 20 m | 52, 124 | 110 | 81 | 66 (77) | 7 (27) | 5 | 3 | 43 (78) | 185 |
| woodland_longbow 180 lb + bodkin_a | 20 m | 56, 143 | 127 | 99 | 83 (94) | 18 (45) | 6 | 4 | 72 (106) | 213 |
| to samo | 100 m | 50, 112 | 100 | 71 | 55 (66) | 6 (17) | 4 | 3 | 27 (60) | 168 |
| crossbow_a 120 lb + bolt_a 150 g | 20 m | 36, 97 | 86 | 57 | 42 (53) | 5 (6) | 3 | 2 | 10 (37) | 145 |
| to samo | 100 m | 32, 76 | 68 | 38 | 23 (34) | 3 | 2 | 1 | 7 | 114 |
| crossbow_e 230 lb + bolt_a | 20 m | 48, 170 | 152 | 123 | 108 | 43 (69) | 7 (21) | 5 | 112 | 254 |
| crossbow_f 330 lb + bolt_a | 20 m | 55, 229 | 206 | 177 | 162 | 96 (122) | 35 (75) | 7 (17) | 202 | 344 |
| to samo | 100 m | 49, 181 | 162 | 133 | 117 | 52 (79) | 8 (31) | 6 | 129 | 270 |

Odczyt:
- Najczestszy strzelec ROT (luk 100 lb, broadhead default_arrows) zabija tylko nieopancerzonych; skora 28 juz go zatrzymuje (6 pkt). Z bodkinem ten sam luk rani kolczuge za 16-43.
- Kolczuga jest wobec bodkinow prawie papierem (polowa pancerza): luk 125-180 lb = 66-94 pkt z 20 m, czyli smierc lub prawie smierc jedna strzala.
- Plyta RBM (77+) jest na luki odporna calkowicie na kazdym dystansie; rusza ja dopiero kusza 230-330 lb z bliska.
- Plyta ROT (50) lezy dokladnie na progu: wypoczetego rycerza luk 180 lb rani za 18 z 20 m, zmeczonego za 45; z 100 m 6-17.
- Kazde trafienie w twarz od przodu w helmie ROT = smierc (101-344 pkt), niezaleznie od helmu.
- Dystans 20 -> 100 m zabiera ok. 26% energii, ale przez "klif" czesto zmienia wynik z 40+ na 5.

### 2.3 Oszczep (prog 3, obuch 0.2, brak ulgi na kolczuge)
v = 15-26 m/s, KE 100-217 J (0.6-1.2 kg, skill 60-250). Piers, ofiara x1.0: bez pancerza 91-194; skora ROT 26-128; kolczuga ROT 9 (skill 60) / 41-73 (skill 150) / 60-93 (skill 250); plyta ROT 9 / 20-53 / 40-72; plyta RBM 77: 7-15.
=> oszczep o energii 130-160 J robi kolczudze mniej niz strzala 86 J z bodkinem (prog 3 x pelny pancerz kontra 2 x polowa). Oszczepy sa z czesci (crafting) - ich ThrustDamageFactor moze zmieniac prog (nie liczone).

### 2.4 Konie
- Ten sam wzor i te same mnozniki czesci ciala; kon bez rzedu ma pancerz 0 => strzala 100 lb zabiera 60-80 pkt.
- Deba: tylko pocisk od przodu (dot < -0.35), kon wolniejszy niz 5 m/s, obrazenia >= 26 (`DecideMountRearedByBlowPatch`, src:~2000; prog 13 x 2).
- Zrzucenie z konia pociskiem: RBM nie zmienia (prefiks pomija IsMissile).

## 3. Tarcze

### 3.1 Blok i przebicie
- Strzala/belt NIE przechodzi przez tarcze (silnik 57765-57800: przejscie tylko z flaga CanPenetrateShield albo po rozbiciu tarczy + perk). W danych flage maja: balista (RBM_WS) i giant_arrows (ROT). Strzala wbita w tarcze zostaje w niej.
- Tarcza na plecach: RBM zamienia trafienie na blok (`RangedRework.MissileHitCallbackPatch`, src:4026; wyjatki: pociski przebijajace, perk Impale) i zdejmuje HP tarczy (src:4051-4092).
- `StanceLogic.OnShieldDamagedPatch` (src:8596): HP tarczy = 0 => tarcza znika.
- `GetEntityDamageMultiplierPatch` (src:2432): strzala/belt/oszczep w obiekty x0.1.

### 3.2 Obrazenia tarczy - `OverrideDamageCalc.RBMComputeBlowDamageOnShield` (src:2777)
- dmg = RBMComputeDamage(typ, rodzaj, mag, pancerz_tarczy * 10) (src:2914), potem dla pociskow: strzala/belt x1.5, oszczep x25, topor rzucany x10 (+60 do mag), wlocznia rzucona x5, reszta x0.1 (src:2915-2927); flaga BonusAgainstShield x2; x ShieldCorrectSideBlockDamageMultiplier 0.7 przy poprawnej stronie; potem perki (CalculateShieldDamage).
- Tarcze RBM: drewno HP 345-800, pancerz 1-7 (typowo 4-6); stal HP 900-2000, pancerz 9-15.
- Tarcze ROT (ROTassets.xml): HP 300-510, pancerz ALBO 1 (targaryen 2/3, greyjoy, tyrell, bolton, mormont, baratheon, arryn, dragonstone, lannister_kite, unsullied, golden_company, braavosi, sarnori, tyroshi, lyseni, myrish, dayne, bronze, spiked) ALBO 7-9 (stark, lannister, lion, wildling2, martell, thenn, qohorik, pentoshi, manderly, lorathi, skagosi, yiti, qarth, ibb, mallister, westerling, grafton).

Ile strzal rozbija tarcze (bez mnoznika 0.7 i perkow):

| pocisk | ROT panc. 1 / HP 370 | RBM panc. 4 / HP 450 | RBM panc. 6 / HP 700 | ROT panc. 9 / HP 380 | stal 15 / HP 2000 |
|---|---|---|---|---|---|
| luk 100 lb, broadhead (mag 97) | 3.3 | 37 | 71 | 49 | 367 |
| luk 100 lb, bodkin (94) | 3.2 | 14.5 | 73 | 50 | 378 |
| luk 180 lb, bodkin (151) | 1.8 | 3.9 | 12 | 31 | 236 |
| kusza 330 lb (244) | 1.1 | 1.8 | 3.5 | 3.4 | 146 |
| oszczep 0.8 kg skill 150 (160 J, x25) | 1 (3375 pkt) | 1 (1333) | 1 | 1-2 (286) | ok. 7 |

=> tarcze ROT z pancerzem 1 padaja po 2-4 strzalach zwyklego luku; tarcze ROT z pancerzem 9 wytrzymuja ok. 50. To roznica danych ROT, nie zamysl RBM (15x miedzy rodami). Oszczep niszczy kazda drewniana tarcze jednym rzutem (zamysl "pilum").

## 4. Postura (RBMAI) - `Stance` (src:6869), `PostureDamage` (src:7034), `StanceLogic` (src:7516)

### 4.1 Pula i odnowa
- maxPosture = 30 + 20*(1 + atletyka/500) + 80*(1 + skill_broni/500) + max(0, waga pancerza - 5); konno jezdziectwo zamiast atletyki (`Stance.InitializePosture`, src:6917). Skill 100/100, zbroja 25 kg: 170.
- odnowa co 0.5 s: 30 * (0.016*(1+atl/500) + 0.064*(1+skill/500)) => przy 100/100 ok. 5.8/s; x3 po 10 s bez walki (src:8817, 8912).
- stamina: max 1000*(1 + 3*atl/500); odnowa 30*0.03*(1+2*atl/500) na 0.5 s, x (1+brak%)^2.5, x3 poza walka ponizej 70%; helm z body_armor >= 30: odnowa x0.5 (src:6893-6909; w ROT prawie zaden helm nie ma body_armor => kara nie dziala). Stamina > 85%: +0.9 HP co 10 s (src:8937).
- gracz: x PlayerPostureMultiplier (u Jeffa 1.0).

### 4.2 Co zuzywa posture (walka wrecz) - `CreateMeleeBlowPatch.Postfix` (src:7859), `calculateDefenderPostureDamage` (src:8108)
- obronca: (20 + koszt_obrony[klasa broni/tarczy, typ bloku] + 20 + drenaz_ataku[klasa broni, kierunek] + 4 * predkosc wzgledna) * (1 + atl_atak/500 + skill_atak/500) / (1 + atl_obr/500 + (skill_obr + 20 gdy tarcza)/500) * comHitModifier * Lerp(1.25, 1, stamina%).
  Stale (src:7080-7310): drenaz zamachu: miecz 1H 10, topor 1H 13, maczuga 15, miecz 2H 27, drzewcowa 2H 29, topor 2H 32, mlot 2H 36; pchniecie mniej (5-25). Koszt obrony: blok mieczem -20, parowanie -30; mala tarcza -30/-40 (zla strona -25), duza -35/-45 (-30).
  Przyklad: topor 2H w duza tarcze, dobry blok: (20-35)+(20+32) = 37; miecz 1H w miecz: 30.
- napastnik traci: (20 + koszt_ataku) + (20 + odbicie) - np. miecz 1H w duza tarcze 15.
- trafienie w cialo: postura ofiary -= zadane obrazenia (src:7690).
- stamina: obrona 40-46 * (1 - skill*0.001), trafiony 2*obrazenia + 20, atak 35-49; wszystko x (1 + (waga pancerza - atl/20)/50) (src:7701-7708, 8005-8106).
- MARTWE: parametry actionModifier (0.5-1.0) sa nadpisywane na 1 (src:8115, 8167); parametr damageShield nieuzywany; koszt dla klas bez stalej (np. JAVELIN_BLOCK_COST) leci przez wyjatek refleksji i wraca 20 (src:7335-7364).

### 4.3 Zlamanie postury (postura <= 0) - `handleDefender` (src:7683)
- blok bronia / parowanie: obrazenia "crush through" = pelne obrazenia broni napastnika przeciw SREDNIEMU pancerzowi 9 czesci ciala, gdy nadmiar > 20 (inaczej proporcja nadmiar/20) (`calculateHealthDamage`, src:8431); zachwianie; gdy nadmiar >= 50% maxPosture - wypuszczenie broni (tylko jesli ma 2+ bronie biale).
- blok tarcza: bez obrazen (crushThrough: false), zachwianie; nadmiar >= 50% - UPUSZCZENIE TARCZY.
- po zlamaniu postura wraca do 75% max; napastnik po trafieniu w cialo przy zerze: "tired" i 33%.

### 4.4 Pociski a postura
- Strzelec placi wlasna postura (`OverrideOnAgentShootMissile`, src:7609; `calculateRangedPostureLoss`, src:8540): luk 35 + 25*f, kusza 5 + 5*f, oszczep/topor/noz 25 + 25*f, f = 1 - 0.5*max(0,1-skill/200) - 0.5*max(0,1-atl/200) (wyzszy skill = WIEKSZY koszt: skill 0 -> 35, skill 200 -> 60 dla luku). Zejscie do zera: postura 0 + 50% max. Stamina: luk/kusza max(20, 70 - 0.5*(skill - difficulty)), miotana max(50, 100 - 0.5*skill).
- Trafiony pociskiem w cialo (`StanceLogic.OnAgentHit`, src:9020): strzala/belt/noz -25 postury (-50 w glowe), stamina -50 (-100); oszczep/topor -100 (-200 w glowe), stamina -300 (-600). Postura 0 => zachwianie, powrot do 75%.
- Galaz "pocisk w tarcze" (-20 / -70, src:9069-9092) jest MARTWA: silnik nie wola Agent.RegisterBlow przy bloku tarcza (mb_full.cs:57414), a `RegisterBlowPatch` RBM (src:3074) dla bloku wola tylko OnRegisterBlow - OnAgentHit nie przychodzi. Ostrzal tarcz nie meczy i nie lamie postury.

## 5. Niezgodnosci danych ROT z RBM (najwazniejsze dla pytania "czy to ma sens")
1. `giant_bow` + `giant_arrows` (giant_archer, giant_rider, elder_giant): strzala 0.3 kg przy naciagu 90 lb leci 25 m/s, mnoznik (120+10-100)/100 = 0.3 => mag 28 J: 25 pkt w nagiego, 0-2 w kazdy pancerz. Luk olbrzyma jest pod RBM prawie nieszkodliwy (ma za to CanPenetrateShield, AffectsArea, CanKnockDown).
2. `ravens_teeth_arrows` (thrust_damage 5; ROT-Troops.xml:24336): mnoznik 0.01-0.05 => 0-3 pkt obrazen. Oddzial z tymi strzalami nie zabija nikogo.
3. Luki unikalne ROT (weirwood 93, dragonbone 95, goldenheart/ravens_teeth/qarth 87 lb; thrust_damage 84-97 zamiast 100): mnoznik 0.84-0.97 z bodkinem - slabsze od zwyklego luku RBM 100 lb, mimo ceny/legendy.
4. Zbroje i helmy ROT w skali vanilli (sekcja 2.1): plyta ~ kolczuga, twarz i szyja bez ochrony.
5. Tarcze ROT pancerz 1 kontra 9 (sekcja 3.2).

## 6. Nasz kod a pociski RBM
- `Armoury/src/CleanShot.cs:250` (worktree n367c): wyprzedzenie liczone z `CurrentUsageItem.MissileSpeed` luku - pod RBM to naciag w funtach (60-330), nie m/s (prawdziwe 37-63). Wyprzedzenie zanizone 1.7-6x; wplyw maly, bo pas jest ograniczony do 4.5 m i liczy sie tylko ponizej 30 m.
- `Armoury/src/BattleWind.cs` latka RBMAI.Stance.InitializePosture, `Armoury/src/Charge.cs:46` latka ComputeBlowMagnitudeFromHorseCharge (RBM ma tam prefiks zwracajacy false, src:~2060) - poza zakresem tej mapy, do sprawdzenia przez mape walki wrecz.
- Zuzycie pancerza w bitwie przez RBM: `Utilities.lowerArmorQualityCheck` (src:4632): szansa 5% + pochloniete/(pancerz*20) * wsp. materialu na obnizenie modyfikatora trafionej czesci (SpawnEquipment) - dziala tez dla strzal; styk z naszym zuzyciem sprzetu (AiWear) do sprawdzenia przez mape pancerzy.

## 7. Ocena: sens czy uproszczenie
Ma sens (fizyka): energia z naciagu, masy strzaly i sprawnosci luku; ciezka strzala wolniejsza, ale niesie wiecej; predkosc ofiary i strzelca; opor powietrza; bodkin kontra broadhead; kolczuga slaba na szpic, plyta odporna; oszczep niszczy tarcze; trafiony traci rownowage.
Uproszczone / nie dziala:
- obrazenia progowe bez kata padania i losowosci ("klif" 5 pkt albo 40+), martwe "czesciowe przebicie";
- naciag zawsze pelny, umiejetnosc nie zmienia energii ani nie karze za zbyt mocny luk;
- ulga x0.5 dla szpicow jednakowa dla sukna, skory i kolczugi; plyta zero-jedynkowa;
- wypoczeta ofiara +30% pancerza (stamina) - arbitralne;
- ostrzal tarcz nie kosztuje postury (martwa galaz), strzaly nie przebijaja tarcz nigdy;
- wyzszy skill strzelca = wiekszy koszt postury strzalu (odwrocony znak);
- dane ROT (zbroje, helmy, tarcze, luki olbrzymow, Ravens' Teeth) nie sa przeliczone na skale RBM - to psuje wynik bardziej niz same wzory.

## 8. Czego nie udalo sie ustalic
- Natywny model lotu: czy hamowanie to dokladnie -k*v^2 i czy wartosc ManagedParameters ustawiona w FinishMissionLoading dociera do silnika (komentarz w managed_core_parameters.xml mowi o "blizniaku" w native_core_parameters) - liczby dla 20/100 m sa przy tym zalozeniu.
- Czy MissileTotalDamage = thrust_damage luku + strzaly (liczone natywnie) - wniosek z ukladu danych RBM (100 + 100/115, potem -100).
- Wagi i ThrustDamageFactor oszczepow / toporow do rzucania (bron z czesci craftingu) oraz ktore maja BonusAgainstShield (w XML RBM flaga widoczna tylko na ostrzach toporow) - przyklady oszczepu sa dla masy 0.6-1.2 kg i wspolczynnika 1.
- HP koni (bazowe z Monster + extra_health) - nie liczylem smierci konia w strzalach.
- Mnoznik trudnosci (combatDifficultyMultiplier) i perki (CalculateAdjustedArmorForBlow, CalculateShieldDamage) pominiete w przykladach.
- Jakie zbroje faktycznie nosza wojska ROT (ROT czy vanilla/RBM) i co podmienia DynamicTroopEquipment - nie zliczalem; tabela pokazuje obie skale.
- Nic nie bylo testowane w grze - wszystko z kodu i XML.
