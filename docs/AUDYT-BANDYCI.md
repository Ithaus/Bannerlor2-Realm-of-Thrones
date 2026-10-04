# AUDYT BANDYTOW - skad ich tylu i skad plyta (2026-10-04)

Pytanie Jeffa: "bandyci - rycerze-rabusie, piraci - co drugi biega w zbroi plytowej,
i jest ich strasznie duzo; jak oni rosna?"

Audyt tylko do odczytu: XML ROT/SandBox/NavalDLC/BK, dekompilacja (vanilla CampaignSystem,
ROT.dll, BannerKings, NavalDLC, DTE, BetterEconomy, Spoils of War), nasze zrodla
(Armoury, CrashScribe), logi CrashScribe i Armoury, `items-dump.csv`. Zadnych zmian w kodzie.

Skroty sciezek:
- `MOD` = `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules`
- `DEC` = katalog zdekompilowanych zrodel w scratchpadzie (`tw`, `bk`, `rot`, `nv`, `dte`, `sow`, `be`)
- `DUMP` = `Documents\Mount and Blade II Bannerlord\CrashScribe\items-dump.csv`
  (tier w dumpie = `(int)Tier + 1`, czyli tier wyswietlany; pancerz i cena PO przeliczeniu RBM)

---

## 0. Odpowiedz w trzech zdaniach

1. **Plyta jest wpisana na sztywno w szablony XML** jednostek ROT: najnizszy pirat (`sea_raiders_bandit`,
   poziom 11) nosi `crude_armor` - material **Plate**, 20 kg, w grze tier 5, warta ~33 tys. denarow;
   rycerz-rabus od drugiego szczebla (`robber_horseman`) nosi `common_armor` - tez **Plate**, 20 kg, T5.
   Nie ma zadnej ekonomii: bandyta dostaje komplet przy narodzinach za darmo, DTE nie daje bandom
   zbrojowni, a nasz AiGear kaze kupowac sprzet tylko lordom.
2. **Liczba band to nie zapas, tylko poziom docelowy.** Vanilla co nocna godzine dosypuje 10% brakujacej
   roznicy do limitu `zarazone kryjowki x 12`. ROT podniosl limity: do **20 kryjowek na klan** (vanilla 9)
   i **6+6 band na kryjowke** (vanilla 3+3) - czyli do **240 band na klan** zamiast 54. Zabijanie band w polu
   nic nie zmienia: nastepnej nocy odrastaja z niczego. Co dzien zarazana jest (prawie na pewno) jedna
   nowa kryjowka, wiec swiat "rosnie" przez ~3 miesiace gry az do nasycenia.
3. **Bandy rosna tez od srodka**: rozmiar nowej bandy skaluje sie z postepem gracza (u Jeffa ~1.0 = prawie
   maksymalne bandy), bandy awansuja za XP **bez kosztu w zlocie**, BannerKings dosypuje 2-6 ludzi dziennie
   kazdej bandzie w kryjowce z bohaterem-zbojem, a nasz HideoutPurge scala bandy w horde, co zwalnia
   "sloty" limitu i pozwala vanilli dosypac nowe.

---

## 1. Frakcje bandyckie i ich jednostki

### 1.1 Klany i kultury (ROT-Content `spclans.xml`, `spcultures.xml`)

| Klan (id) | Kultura | Szablon domyslny | Kryjowki na mapie ROT | Zrodlo |
|---|---|---|---|---|
| `looters` | looters | `looters_template` | 0 (spawn przy wsiach/miastach) | spclans.xml:7 |
| `sea_raiders` ("Pirates" w grze) | sea_raiders | `sea_raiders_template` | **62** (`hideout_seaside_*`) | spclans.xml:11 |
| `mountain_bandits` | mountain_bandits | `mountain_bandits_template` | 49 | spclans.xml:12 |
| `forest_bandits` | forest_bandits | `forest_bandits_template` | **67** | spclans.xml:13 |
| `desert_bandits` | desert_bandits | `desert_bandits_template` | 31 | spclans.xml:14 |
| `steppe_bandits` (Dothrakowie-renegaci) | steppe_bandits | `steppe_bandits_template` | 24 | spclans.xml:15 |
| `robber_knights` | robber_knights | `robber_knight_template` | 11 | spclans.xml:505, spcultures.xml:14455 |
| `kingswood_outlaw` | kingswood_outlaw | `kingswood_outlaw_template` | 3 | spclans.xml:508, spcultures.xml:14247 |
| `wildlings` (bandy dzikich) | wildlings | `wildling_outlaw_template` | 9 (`wight_hideout_*`) | spclans.xml:509, spcultures.xml:13941 |
| `hilltribesmen` | hilltribesmen | `hilltribesmen_outlaw_template` | 4 | spclans.xml:510, spcultures.xml:14094 |
| `yiti_bandits` (Lengii Slavers) | yiti_bandits | `yiti_bandit_template` | 6 | spclans.xml:512, spcultures.xml:14352 |
| `northern_pirates` (morscy, z NavalDLC) | **looters** (!) | `northern_pirates_template` (statki) | - (punkty spawnu morskiego) | spclans.xml:~540 |
| `southern_pirates` "Stepstone Pirates" | southern_pirates | `southern_pirates_template` (statki) | - | spclans.xml:~553 |
| `deserters` (vanilla 1.4) | deserters | brak (z rozbitkow bitew) | - | SandBox spclans.xml:31 |

Liczba kryjowek: 266 w `MOD\ROT-Map\ModuleData\settlements.xml` (policzone po atrybucie `culture`
osad z komponentem `<Hideout>`). `ROT_Hideouts` zmienia tylko sceny, nie liczbe.

Drobne znalezisko: kultura `yiti_bandits` ma `basic_troop="NPCCharacter.yiti_clansman"`
(spcultures.xml:14352), a takiej postaci nie ma w zadnym module.

### 1.2 Szablony band (ile ludzi w bandzie)

| Szablon | Sklad (min-max) | Razem | Plik:linia |
|---|---|---|---|
| `sea_raiders_template` (ROT) | bandit 15-30, raider 5-10, chief 3-7, boss 1-2 | **24-49** | ROT-Content partyTemplates.xslt:9-16 |
| `sea_raiders_boss_party_template` | raider 2-4, chief 1, boss 1 | 4-6 | partyTemplates.xslt:21-27 |
| `northern_pirates_template` (NavalDLC) | sea_raiders_bandit 20-35, raider 15-25, chief 5-10 + 3-4 statki | **40-70** | NavalDLC naval_partyTemplates.xml:3-29 |
| `southern_pirates_template` (NavalDLC) | bandit 20-35, raider 15-25, chief 5-10 + statki | 40-70 | naval_partyTemplates.xml:30-56 |
| `robber_knight_template` | scout 5-18, horseman 3-6, knight 1-3 | **9-27** | partyTemplatesROT.xml:336-342 |
| `robber_knight_boss_party_template` | horseman 2-4, knight 1, boss 1 | 4-6 | partyTemplatesROT.xml:344-350 |
| `kingswood_outlaw_template` | thief 5-18, bandit 3-6, chief 1-3 | 9-27 | partyTemplatesROT.xml:631-637 |
| `yiti_bandit_template` | criminal 10-18, raider 3-6, chief 1-3 | 14-27 | partyTemplatesROT.xml:649-655 |
| `wildling_outlaw_template` | thug 5-18, ambusher 3-6, pillager 1-3 | 9-27 | partyTemplatesROT.xml:1379-1385 |
| `hilltribesmen_outlaw_template` | grunt 5-18, poacher 3-6, savage 1-3 | 9-27 | partyTemplatesROT.xml:1395-1401 |
| `mountain/forest/desert/steppe_bandits_template` (vanilla) | bandit 2-27, raider 0-12, chief 0-6 | 2-45 | SandBox partyTemplates.xml:102-237 |
| `looters_template` (ROT) | looter 4-18, looter2 1-4, looter3 2-10 | 7-32 | partyTemplates.xslt:136-143 |
| `bandits_hero_sea_raiders` (BK, bohater-zboj) | raider 120-160, chief 60-90, bandit 90-120, boss 5-10 | **275-380** | BannerKings.Redux partyTemplates.xml:44-51 |

### 1.3 Ekwipunek jednostek (szablon bojowy) - czy niski tier ma plyte?

Kolumny: poziom jednostki z XML; "pancerz" = suma head+body+arm+leg wszystkich czesci zbroi
(wartosci W GRZE, po RBM `ArmorMultiplier=2`, `MOD\RBM\DefaultConfigDONOTEDIT.xml:22`);
"kit" = suma `value` z DUMP. Tier przedmiotu = tier wyswietlany.

**Rycerze-rabusie** (`MOD\ROT-Content\ModuleData\ROT-Troops.xml`)

| Jednostka | Poz. | Awans do | Korpus | Helm | Inne | Pancerz | Kit (den.) | Linia |
|---|---|---|---|---|---|---|---|---|
| `robber_scout` "Robber Squire" | 11 | horseman (bez wymogu) | stormlands_gambeson T3 (przeszywanica) | **common_barbute T4** (metal, 127) | miecz T3, buty T2 | 263 | 20 246 | 13329 |
| `robber_horseman` | 16 | knight (wymaga `war_horse` w taborze) | **common_armor T5, Plate, 20 kg** | common_kettle T4 | miecz T5, rumak T4 | 333 | 68 003 | 13362 |
| `robber_knight` | 21 | - | **common_armor T5 Plate** | common_spangen T5 | **common_pauldrons T6**, kopia T5, nagolenniki/naramienniki, konska kolczuga | 457 | 91 405 | 13399 |
| `robber_boss` | 26 | - | jw. | jw. | jw. + tarcza T4 | 457 | 92 085 | 13438 |

`common_armor` w XML: `body_armor="42" ... modifier_group="plate" material_type="Plate"`, waga 20
(`MOD\ROT-Content\ModuleData\ROTassets.xml:3337-3342`). W grze po RBM: body 100, T5, 34 736 den.

Udzial plyty w bandzie: srednio scout 11.5 / horseman 4.5 / knight 2 -> **~36% w plycie od narodzin**,
a **100% w metalowym helmie T4+** (giermek tez ma barbute). Po paru bitwach giermkowie awansuja
na horsemana za darmo (rozdz. 2.5) - stad "co drugi w plycie" Jeffa zgadza sie z mechanika.

**Piraci ladowi = `sea_raiders`** (`MOD\ROT-Content\ModuleData\bandits.xslt`)

| Jednostka | Nazwa | Poz. | Korpus | Helm | Pancerz | Kit | Linia |
|---|---|---|---|---|---|---|---|
| `sea_raiders_bandit` | Pirate | 11 | **crude_armor T5, Plate, 20 kg** | crude_helmet T4 | 296 | **53 916** | 10-46 |
| `sea_raiders_raider` | Ravager | 16 | crude_armor T5 Plate | crude_barbute T4 | 303 | 56 490 | 50-89 |
| `sea_raiders_chief` | Reaver | 21 | crude_armor T5 Plate + **crude_pauldrons T6** | crude_nasal T4 | 387 | 66 942 | 91-130 |
| `sea_raiders_boss` | Marauder | 26 | jw. | crude_spangen T5 | 399 | 73 030 | 132-170 |

`crude_armor` w XML: `body_armor="40" ... material_type="Plate"`, 20 kg, `culture="Culture.sea_raiders"`
(ROTassets.xml:3393-3398). **Kazdy** pirat ROT od najnizszego szczebla jest w plycie - nie "co drugi", a wszyscy.
Te same jednostki obsadzaja morskich `northern_pirates` (40-70 ludzi na bande).

**Piraci Stepstones = `southern_pirates`** (`MOD\ROT-Content\ModuleData\naval_characters.xslt:6890+`)

| Jednostka | Poz. | Korpus | Pancerz | Kit |
|---|---|---|---|---|
| `southern_pirates_bandit` | 11 | pirate_armor1 T4 (skora, 12 kg) | 157 | 14 435 |
| `southern_pirates_raider` | 16 | pirate_armor2 T4 (skora), helm T1 | 243 | 14-16 tys. |
| `southern_pirates_chief` | 21 | pirate_armor3 T4 (skora) | 273 | 15-19 tys. |

Skora, nie plyta - to jest wzor, jak pirat powinien wygladac.

**Pozostale bandy** (skrot; pelne wydruki w notatkach audytu)

| Frakcja | Najnizsza jednostka (poz. 11) | Szef (poz. 21) | Plyta na dole? |
|---|---|---|---|
| looters (`looter`, `looter2`, `looter3` "Broken Man/Guard/Archer", poz. 6) | skorzana tunika T1, pancerz 57-116, kit 2-4 tys. | - | nie |
| mountain_bandits | lachy T0-T1, pancerz 55-66 | kolczuga/luska T3-T4, pancerz ~210-237 | nie |
| forest_bandits | odziez lesna T0-T1, pancerz 41-107 | kolczuga T3-T4 | nie |
| desert_bandits (vanilla SandBox bandits.xml) | szaty T0, pancerz 58-74 | Harami: kolczuga T4 lub **desert_scale_armor T6** (1 z 6 wariantow) | nie |
| steppe_bandits (Dothrakowie) | dothraki_armor8 T3, pancerz 106 | dothraki_armor T4 | nie |
| kingswood_outlaw | bandit_garb T3, 114 | bandit_garb T3 + czapka, 159 | nie |
| wildlings | wilding_garb T1, 87 | **thenn_plate T4** + helm T4, 302 | nie (ale szef tak) |
| hilltribesmen | highland_cloth T0, ~100 | rough_fur_over_chain T4 | nie |
| yiti_bandits | reinforced_suede T1, 124 | **eastern_lamellar T5** + helm T4, 280 | nie (szef lamelka) |

Dla porownania regularne wojsko tego samego poziomu: `vlandian_footman` (Westerlands, poz. 11) -
pancerz 237, kit 19 735; `vlandian_recruit` (poz. 6) - pancerz 73-87, kit 2-3 tys.
**Pirat poziomu 11 jest opancerzony lepiej niz zolnierz Westerlands tego samego poziomu i ma komplet
2.7x drozszy.**

### 1.4 Nasze prawa podbijaja tych bandytow dalej

- CrashScribe `Mends.SkillSinew` (`CrashScribe\src\Mends.cs:2403-2470`) podnosi jednostce umiejetnosci
  do wymagan JEJ WLASNEGO sprzetu ("raise, never lower"; sufit tieru usuniety 01.09, bo bandyci biegali
  "z golymi klatami"). Skutek w logu `session-2026-10-04_05-45-33.log`:
  - `[05:47:25] Mends: sea_raiders_bandit (tier 2) - Athletics 50 -> 140 (wlasny sprzet difficulty 140).`
  - `[05:47:25] Mends: sea_raiders_chief (tier 4) - Athletics 110 -> 175 ...`
  - `[05:47:25] Mends: robber_knight (tier 4) - Athletics 110 -> 175 ...`
  - `[05:47:25] Mends: robber_scout (tier 2) - Athletics 40 -> 105 ...`
  - `[05:47:25] Mends: robber_horseman (tier 3) - One Handed 90 -> 140 ...`
  Wymogi pochodza z Prawa Wagi i Prawa Tieru Pancerza (`Mends.cs:2018`, `2297`; log:
  `prawo tieru pancerza - 35 Atletyki na tier; wymog podniesiony 1303 sztukom`).
  Czyli: plyta z XML -> wysoki wymog -> **bandyta T2 dostaje Atletyke 140**. Opis ustawienia
  `ArmorAthleticsPerTier` (`Armoury\src\Settings.cs:417`) zakladal odwrotnie ("a low-Athletics bandit never
  'qualifies'") - dla jednostek z wlasnym sprzetem SkillSinew to odwraca.
- Skutek w walce (Armoury `HitScribe`, `Armoury-2026-09-20_06-53-05.log`): 150 trafien strzala w
  `sea_raiders_bandit` - srednio surowe 88, **wchloniete 67 (77%)**, zadane 20. Przyklad:
  `HIT Arrow -> sea_raiders_bandit [ShoulderRight] dmg=4 wchloniete=63 surowe=67 ... od river_ranger [weirwood_bow] skill=140`.

---

## 2. Jak bandy sie rodza i rosna

### 2.1 Vanilla `BanditSpawnCampaignBehavior` (DEC `tw\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSpawnCampaignBehavior.cs`)

| Mechanizm | Co robi | Linie |
|---|---|---|
| `HourlyTickClan` | **tylko w nocy**, co godzine: bandy kryjowkowe `SpawnBanditsAroundHideout(clan, 0.1)`, looterzy `SpawnLooters(clan, 0.07)` | 240-253 |
| `SpawnBanditsAroundHideout` | tworzy `round((zarazone_kryjowki x (around+inEach) - obecne_bandy) x 0.1)` nowych band | 255-263 |
| `SpawnLooters` | `round((limit - obecne) x 0.07)`, limit = `min(7 x wszystkie zarazone kryjowki, GetMaxSupportedNumberOfLootersForClan)` | 265-273, 503-506 |
| `DailyTick -> AddNewHideouts` | raz dziennie losuje JEDEN klan (waga `1 - zarazone/max`) i z p-stwem `0.2 + (max - n) x 0.1` (dla n < max/2; czyli ~1.0) zaraza nowa kryjowke | 208-213, 275-298 |
| `FillANewHideoutWithBandits` | nowa kryjowka dostaje od razu `NumberOfMinimumBanditPartiesInAHideoutToInfestIt` band | 300-310 |
| start gry | `NumberOfInitialHideoutsAtEachBanditFaction` kryjowek x minimum band + 50-75% luki limitu | 85-102, 130-147, 436-445 |
| `InitializeBanditParty` | zloto `10 x ludzie x (0.5-1.5)`, darmowe jedzenie, agresja 0.8-1.0 | 569-582 |
| `MobilePartyDestroyed` | tylko zmniejsza licznik - **nic nie blokuje odrostu** | 55-63 |

Wniosek: populacja band to **poziom docelowy**. Kazda zniszczona banda jest odtwarzana z powietrza
nastepnej nocy, dopoki jej kryjowka jest zarazona. Jedyne co obniza limit to oczyszczenie kryjowki -
a nastepnego dnia `AddNewHideouts` zaraza inna.

### 2.2 Modele gestosci - kto wygrywa

Lancuch modeli: **ROT** (`ROT\SubModule.cs:320` `AddModel(new ROTBanditDensityModel(GetGameModel<...>))`)
-> NavalDLC -> Default.

| Parametr | Vanilla Default (`tw\...\DefaultBanditDensityModel.cs:14-22`) | NavalDLC (`nv\...\NavalDLCBanditDensityModel.cs:42-74`) | **ROT (obowiazuje)** (`rot\ROT.Models\ROTBanditDensityModel.cs:12-20`) |
|---|---|---|---|
| min band do zarazenia kryjowki | 2 | (bazowy) | **4** |
| max band w kryjowce | 3 | (bazowy) | **6** |
| max band wokol kryjowki | 3 (+2 latka BK, nie dziala na ROT) | (bazowy) | **6** |
| max zarazonych kryjowek na klan | 9 | 9 | **20** |
| poczatkowe kryjowki na klan | 7 | 8 | **5** |
| limit looterow | 270 - dezerterzy | 300 - dezerterzy | -> NavalDLC: **300** |
| limit dezerterow | 50 | -> Default | 50 |
| piraci morscy | - | liczba punktow spawnu klanu | -> NavalDLC |

**Sufit teoretyczny pod ROT** = min(20, kryjowki kultury) x 12 band:

| Klan | Kryjowek | Max zarazonych | Max band | Ludzi na bande (srednio, szablon) | Max ludzi |
|---|---|---|---|---|---|
| sea_raiders | 62 | 20 | **240** | ~36 (24-49) | **~8 700, wszyscy w plycie** |
| forest_bandits | 67 | 20 | 240 | ~23 | ~5 500 |
| mountain_bandits | 49 | 20 | 240 | ~23 | ~5 500 |
| desert_bandits | 31 | 20 | 240 | ~23 | ~5 500 |
| steppe_bandits | 24 | 20 | 240 | ~23 | ~5 500 |
| robber_knights | 11 | 11 | **132** | ~18 (9-27) | ~2 400 (~36% w plycie) |
| wildlings | 9 | 9 | 108 | ~18 | ~1 900 |
| yiti_bandits | 6 | 6 | 72 | ~20 | ~1 400 |
| hilltribesmen | 4 | 4 | 48 | ~18 | ~860 |
| kingswood_outlaw | 3 | 3 | 36 | ~18 | ~650 |
| looters | - | - | 300 | ~19 | ~5 700 |
| deserters | - | - | 50 | z rozbitkow | - |
| **Razem (ladowe)** | 266 | 153 | **~1 950 band** | | **~43 000 ludzi** |

Vanilla na tej samej mapie: 9 x 6 = 54 bandy na klan -> ~460 band + 270 looterow.
**ROT pozwala na ~4x wiecej band niz vanilla, a sea_raiders maja najwiecej kryjowek i najwieksze bandy.**

Tempo: start = 5 kryjowek x 4 bandy + 50-75% luki = ~40-50 band na klan (~450 w swiecie). Potem
~1 nowa zarazona kryjowka dziennie dla calego swiata (tylko jeden klan na dobe), do nasycenia brakuje
~86 kryjowek (5 duzych klanow x 15 + robber 6 + wildlings 4 + yiti 1) -> **~90-100 dni gry**
(polowa dlugiego roku Armoury 168 dni). Kazda nowa kryjowka to +4 bandy od razu i +12 do limitu;
nocne dosypywanie (10% luki na godzine nocy) wypelnia luke w 1-2 noce. To jest "rosna" Jeffa.

### 2.3 Bandy rosna razem z graczem

`DefaultPartySizeLimitModel.GetInitialPartySizeRatioForMobileParty` (DEC `tw\...\DefaultPartySizeLimitModel.cs:390-405`):
dla bandy ladowej `ratio = (0.4 + 0.8 x PlayerProgress) x U(0.2, 0.8)` - czyli przy postepie 1.0 banda
rodzi sie w 24-96% rozpietosci szablonu zamiast 8-32%. `PlayerProgress` (`DefaultPlayerProgressionModel.cs:9-12`)
liczy m.in. sile klanu x 0.0008, ludzi w partii x 0.002, lenna x 0.1 i jest obcinany do 1.0.
Wg `economy-2026-10-04_05-47-36.csv` klan Jeffa ma 613 ludzi wojska, a partia gracza 241 -
**postep jest praktycznie 1.0, wiec nowe bandy rodza sie prawie maksymalne** (pirat: ~30-48 ludzi).
Tak samo rosna potyczki w kryjowce (`NumberOfMaximumTroopCountForFirstFightInHideout = 11 x (2 + progress)`).

### 2.4 BannerKings - bohaterowie-zbojcy

DEC `bk\BannerKings.Behaviours\BKBanditBehavior.cs`, `bk\BannerKings.Components\BanditHeroComponent.cs`:

| Mechanizm | Efekt | Linie |
|---|---|---|
| `OnClanTickImpl` -> `RunWeekly` | raz w tygodniu, jesli klan nie ma bohatera: 2.5% szansy na `CreateBanditHero` | BKBanditBehavior.cs:107-113 |
| `CreateBanditHero` | bohater + banda z `bandits_hero_<klan>` (dla sea_raiders 275-380 ludzi) + 4x `UpgradeParty` (+2-6 ludzi kazdy) + **10 000 zlota** | 205-264; BanditHeroComponent.cs:247-258 |
| `InfestHieout` | **dodatkowe 2 x 4 = 8 band** w kryjowce bohatera, z pominieciem limitu | 256, 273-280 |
| `ConsiderLeaveHideout` (codziennie) | **kazda** banda w kryjowce bohatera ponizej limitu rozmiaru dostaje +2-6 ludzi z szablonu - za darmo, codziennie | BanditHeroComponent.cs:133-157 |
| `TickBandits` | bandy w promieniu 10 eskortuja bohatera -> tworza sie "rojowiska" | BKBanditBehavior.cs:173-197 |
| `ConsiderLeaveHideout` | bohater sprzedaje caly lup po cenie rynku najblizszego miasta (zloto z niczego dla niego) | BanditHeroComponent.cs:~170-182 |

Szablony `bandits_hero_*` istnieja tylko dla 6 vanillowych klanow (looters, sea_raiders, mountain,
forest, desert, steppe) - rycerze-rabusie, dzicy, Kingswood, Yi Ti, gorale bohatera nie dostana.
Postacie-bohaterowie maja kultury sturgia/battania/vlandia/aserai/khuzait/empire (BK
`ModuleData\Characters\bandits.xml`) - ROT uzywa tych id, wiec mechanizm moze sie odpalic.
W logach nie ma wpisu o powstaniu bohatera (BK pisze to tylko na ekran) - **brak dowodu, ze sie zdarzylo**.

Ustawienie BK `BanditPartiesLimit` - u Jeffa **150** (`Configs\ModSettings\Global\BannerKings\BannerKings.json:71`,
domyslne 60). Opis w MCM obiecuje "twardy sufit band dowolnego klanu", ale kod
(`bk\BannerKings.Patches\VanillaModelTweakPatches.cs:200-212`) to tylko postfix na
`DefaultBanditDensityModel.GetMaxSupportedNumberOfLootersForClan` - pod ROT->NavalDLC dociera tam
jedynie zapytanie o dezerterow (50 < 150). **To ustawienie w tym zestawie modow nic nie ogranicza.**
Latka "+2 band wokol kryjowki" (linie 214-219) tez trafia w Default, ktory ROT nadpisuje - bez efektu.

### 2.5 Awanse, XP, jency, scalanie

| Pytanie | Odpowiedz | Dowod |
|---|---|---|
| Czy bandy werbuja? | Nie (vanilla) - poza BK `UpgradeParty` i nocnym dosypywaniem z szablonu | BanditSpawnCampaignBehavior; BK jw. |
| Czy dostaja XP? | Tak - zwykle XP bitewne jak kazda partia | vanilla MapEvent |
| Czy awansuja? | **Tak, codziennie i po kazdej bitwie, bez zlota**: koszt w zlocie sprawdzany tylko gdy `party.LeaderHero != null`; banda bez bohatera awansuje darmo. Blokada tylko na awans do niebandyckiej kultury (perk Veterans Respect) i na `upgrade_requires` (robber_knight wymaga `war_horse` w taborze) | DEC `tw\...\PartyUpgraderCampaignBehavior.cs:46-60, 121, 129`; `DefaultPartyTroopUpgradeModel.cs:110-135` |
| Ciag awansu w plyte | `robber_scout` -> `robber_horseman` (plyta) bez wymogow; `sea_raiders_bandit` -> raider -> chief (pauldrony T6) bez wymogow | ROT-Troops.xml:13341-13344; bandits.xslt:24-27, 64-67 |
| Czy biora jencow do szeregow? | Nie - `RecruitPrisonersCampaignBehavior.DailyTickAIMobileParty` wymaga `IsLordParty`; a jednostek kultury bandyckiej nikt nie rekrutuje z niewoli | DEC `tw\...\RecruitPrisonersCampaignBehavior.cs:49-51`; `DefaultPrisonerRecruitmentCalculationModel.cs:79` |
| Czy sie scalaja? | Vanilla: tylko dezerterzy (`DesertersCampaignBehavior.HourlyTickParty`). BK: eskorta bohatera. **Nasze Armoury: HideoutPurge scala bandy w horde** (rozdz. 2.6) | `DesertersCampaignBehavior.cs:52-97` |
| Czy jest sufit? | Tak: liczba band = limit gestosci ROT (2.2); rozmiar bandy = PartySizeLimit. Ludzi w swiecie - **brak sufitu poza iloczynem tych dwoch** | jw. |

### 2.6 Nasze mody

| Element | Wplyw na liczebnosc/sprzet band | Plik |
|---|---|---|
| **HideoutPurge.Reprisal** | po zwyciestwie gracza w kryjowce bandy w promieniu `HideoutReprisalRadius` **scalaja sie w jedna horde**: ludzie, jency i tabor przechodza, puste bandy `DestroyPartyAction`. Kazda zniszczona banda zwalnia slot limitu -> vanilla dosypuje nowe w nocy. **Netto przyrost ludzi w swiecie.** Log: `HideoutPurge: odwet rusza JEDNA HORDA (7 band scalono, 172 ludzi, sila 204 vs 576)` (`Armoury-2026-09-20_09-03-01.log:1064`) | `Armoury\src\HideoutPurge.cs:330-412` |
| HideoutPurge.OneShotVendetta | to samo, jednorazowo, z pliku `vendetta.now` | `HideoutPurge.cs:197-258` |
| HideoutPurge.BuildLoot / DoSearch | lup T1-T3 i zloto `baza + stawka x bandy` **z niczego** (nie z taboru band) | `HideoutPurge.cs:102-130, 477+` |
| BanditCheer | tylko relacje wiosek po wygranej | `Armoury\src\BanditCheer.cs` |
| HideoutSpotter, HideoutAlarm, HideoutSpawnShim | tylko widocznosc kryjowek / alarm w misji / brama DTE w misji kryjowki | odpowiednie pliki |
| BrokenMen | ranni uciekaja z pola - nie dotyczy populacji | `BrokenMen.cs` |
| DesertionLaw | dotyczy tylko partii gracza i jego klanu (`DesertionLawForAi = false`); dezerterzy NIE ida do band | `DesertionLaw.cs:12-41` |
| Undead / "Zew Nocnego Krola" | osobna horda umarlych, nie klany bandyckie | `Undead.cs` |
| NightRest (AiNightCamp) | logi: `band 0` - bandy nie spia (`AiBanditsCampToo` wylaczone) | `NightRest.cs:440` |
| AiGear | **lordowie AI musza KUPOWAC sprzet** od 04.10; bandytow nie dotyczy (`!mp.IsLordParty -> return`) | `Armoury\src\AiGear.cs:1-20, 122` |
| CrashScribe SkillSinew | podnosi umiejetnosci bandytom pod ich plyte (1.4) | `Mends.cs:2403` |
| CrashScribe Mends | zadnego spisu band; jedynie "kultura X bez imion" dla kultur bandyckich | log 05:47:25 |

### 2.7 Inne mody

- **ROT.dll**: `ROTBanditLeaveHideouts` (`rot\ROT.CampaignBehaviors\ROTBanditLeaveHideouts.cs:27-57`) - gdy w
  kryjowce jest >= min+1 = 5 band, nadmiarowe wychodza patrolowac po 0-72 h (wiecej band na mapie, nie w srodku).
  `ROTEnlistmentBehavior.cs:318-333` - gdy gracz jest zaciagniety, co dobe moze wyjsc banda z najblizszej kryjowki.
  `SubModule.cs:461-466` - jeden wspolny lider wszystkich klanow bandyckich (kosmetyka).
- **NavalDLC** `PiratesCampaignBehavior.TrySpawnPirateParties` (`nv\...\PiratesCampaignBehavior.cs:393-417`):
  codziennie `floor((limit - obecni)^0.66)` band morskich; limit = liczba punktow spawnu klanu; slabe bandy
  (< 70% dolnego limitu szablonu) sa usuwane (`:235-255`).
- **BetterEconomy**: bandy tylko jako "niebezpieczenstwo szlaku" dla karawan (`RouteDangerCampaignBehavior.cs:126`) - bez spawnu.
- **Spoils of War**: lup z bandy przy pokonaniu (wspolczynnik 0.4, `BaggageTrainModel.cs:106, 165-167`) - tylko lup dla zwyciezcy.
- **Vanilla 1.4 `DesertersCampaignBehavior`** (`tw\...\DesertersCampaignBehavior.cs:20-22, 99-180`): po bitwie
  polowej 90% szansy, ze **rozbici (routed) zolnierze** tworza do 3 (armia: 5) band dezerterow przy wsiach, max 50.
  To jedyny mechanizm, gdzie bandyta powstaje z prawdziwego czlowieka w prawdziwym sprzecie - wzor do rozbudowy.

---

## 3. Skad bandyci maja sprzet

| Zrodlo | Dziala dla band? | Koszt | Dowod |
|---|---|---|---|
| Szablon XML przy narodzinach | **Tak - jedyne zrodlo zbroi** | **zero** | rozdz. 1.3 |
| DTE zbrojownia (`PartyArmories`) | **Nie** dla zwyklych band: `MobilePartyExtension.IsValid` zwraca true dla nowej partii tylko, gdy ma bohatera-lidera i wlasciciela (nie gracza); bandy bez bohatera nie dostaja zbrojowni i walcza w sprzecie z szablonu. Bandy BK z bohaterem moga ja dostac | DEC `dte\DynamicTroopEquipmentReupload.Extensions\MobilePartyExtension.cs:39-80`; `EveryoneCampaignBehavior.cs:338-360` |
| Lup z wygranych bitew | trafia do `ItemRoster` bandy jako towar; **nie zmienia tego, w czym walcza** | vanilla |
| Rabunek wiosek/karawan | towar do taboru; przy wejsciu do kryjowki 25% wartosci nie-jedzenia zamienia sie w zloto kryjowki | `BanditSpawnCampaignBehavior.cs:162-179` |
| Awans | darmowy przeskok w lepszy komplet (gambeson -> plyta) | 2.5 |
| Zloto | start `10 x ludzie`, potem dryf do `50 x ludzie` dziennie (`DailyTick`, linia 220) - nie wydawane na sprzet | `BanditSpawnCampaignBehavior.cs:17-19, 220, 578-582` |
| BK bohater | 10 000 zlota na start, sprzedaz lupu - zloto nie kupuje sprzetu bandzie | BanditHeroComponent.cs:255, ~170 |

**Wniosek:** zbroja bandyty nie ma zadnego pochodzenia w ekonomii. Po zmianach z 04.10 (AiGear, SupplyDemand,
rynek uzbrojenia) lord AI placi za kazda kolczuge, a pirat dostaje plyte T5 za darmo przy kazdym
nocnym odrodzeniu. To najwieksza dziura w "realnym obiegu uzbrojenia".

---

## 4. Liczby z logow

**Spisu band w logach nie ma.** CrashScribe nie liczy band ani zarazonych kryjowek; Armoury tez nie.
Nie da sie z logow pokazac krzywej wzrostu - ponizej tylko poszlaki:

| Co | Wartosc | Zrodlo |
|---|---|---|
| Kryjowki wypatrzone przez gracza | 31 (20.09) -> 32 (20.09 09:03) -> 35 (03.10-04.10) | `Armoury-*.log`: `HideoutSpotter: na starcie wypatrzonych kryjowek N` |
| Bandy w jednej kryjowce przy szturmie | 6 (= max ROT w kryjowce) | `Armoury-2026-09-20_09-03-01.log:1063` `HideoutPurge: zwyciestwo w Hideout, band 6` |
| Bandy wokol jednej kryjowki (promien odwetu) | 7 band = 172 ludzi (~25/bande) | tamze :1064 |
| Bandy spiace w nocy | 0 (zawsze) | `AiNightCamp: ... band 0` |
| Trafienia w pirata | 150 strzal, 77% obrazen wchlonietych | `Armoury-2026-09-20_06-53-05.log` (HIT) |
| Bandyci w zasiegu Kwatermistrza gracza | "8x Forest Bandit 110" w partii gracza (z niewoli/werbunku) | `Armoury-2026-10-02_14-55-44.log` |
| Postep gracza (szacunek) | ~1.0 (613 ludzi klanu, 241 w partii) | `economy-2026-10-04_05-47-36.csv` |

Zeby odpowiedziec liczbami "ile ich jest i jak rosna", trzeba najpierw dopisac spis (propozycja P0).

---

## 5. Propozycje (NIE wdrozone)

Kolejnosc wg zasady "najpierw log, jedna zmiana naraz".

### P0. Spis band (CrashScribe, tylko log)
Raz dziennie linia `BANDY: dzien N | klan: band X / ludzi Y / zarazonych kryjowek Z / w plycie W% | razem ...`
(`MobileParty.AllBanditParties`, `Hideout.All.IsInfested`, material korpusu z `BattleEquipments`).
Dopiero na tym mierzyc kazda kolejna zmiane. Zero wplywu na rozgrywke.

### P1. Sufit (najtaniej, natychmiastowy efekt)
Wlasny `BanditDensityModel` dodany PO ROT (opakowuje ROT jak ROT opakowuje NavalDLC) albo postfix
na getterach `ROTBanditDensityModel`: suwaki MCM, domyslnie np. max 8 kryjowek/klan, 3 w kryjowce,
3 wokol (= vanilla+). Uwaga: ROT `ValidateGameModel` wypisze czerwony komunikat, jesli nasz model
bedzie ostatni (`ROT\SubModule.cs:536-547`) - postfix na getterach ROT tego unika.
Ryzyko: mniejszy limit = przez pare dni nic nie dosypuje (to dobrze); istniejace bandy nie znikna.

### P2. Sprzet z XML (latka xslt w naszym module, jedna zmiana)
Nadpisac szablony bojowe najnizszych szczebli:
- `sea_raiders_bandit` / `sea_raiders_raider`: skora/przeszywanica jak `southern_pirates` (pirate_armor1-3,
  T4 skora, 12 kg), helmy T1-T2; `crude_armor` zostawic dopiero `sea_raiders_chief/boss` (1-9 na bande).
- `robber_scout`: kaptur/czapka zamiast `common_barbute` T4; `robber_horseman`: kolczuga zamiast plyty;
  plyta tylko `robber_knight/boss` (upadli rycerze - lorowo uzasadnione, 1-3 na bande).
- Skutek uboczny pozytywny: SkillSinew przestanie dawac piratom T2 Atletyki 140.

### P3. Liczebnosc z czegos realnego - "pula ludzi wyjetych spod prawa"
Zamiast poziomu docelowego - **zapas** na region (np. na zamek/miasto i jego wsie):
- WPLYWY (ludzie przybywaja):
  - **wojna**: rozbici z bitew (vanilla `DesertersCampaignBehavior` juz to liczy - podpiac sie pod
    `MapEventEnded`, routed troops); spalone/zrabowane wsie (`RaidCompletedEvent`) -> czesc
    mieszkancow (hearth) idzie w las;
  - **bieda/glod**: wies z hearth spadajacym, miasto z `FoodStocks` 0 lub glodem, wysoki podatek BK;
  - **niezaplacony zold**: lordowie AI z dlugiem / zoldem ponad limit - nasz DesertionLaw (dla AI
    dzis wylaczony) i vanilla dezercja z braku zoldu oddaja ludzi do puli zamiast w nicosc;
  - **demobilizacja**: rozwiazane armie i pokoj (`MakePeaceAction`) - czesc zolnierzy z rozwiazanych partii;
  - **uciekinierzy z niewoli** (RealisticCaptivity) - jency bandyccy po ucieczce.
- WYPLYWY: zabici, wzieci w niewole i sprzedani/straceni, amnestia/powrot do wsi przy pokoju i dobrych
  zbiorach (powolny odplyw do hearth).
- SPAWN: nocny spawn vanilli przepuszczony przez pule (`Harmony Prefix` na `SpawnBanditParty` /
  `SpawnLooterParty` / `AddBanditToHideout`: brak ludzi w puli regionu = brak bandy). Zabita banda nie
  odrasta, dopoki region nie wyprodukuje nowych wyrzutkow.
- Klan bandy wedle regionu: dezerterzy z wojsk Westerlands -> rycerze-rabusie/mountain, wybrzeza ->
  sea_raiders itd.

### P4. Sprzet z tej samej ekonomii - "paser i tabor"
- Kazda kryjowka ma **magazyn** (ItemRoster): to, co bandy zrabowaly (karawany, wsie - towar
  z `ItemRoster` band juz dzis tam trafia i jest zamieniany w 25% zlota, `BanditSpawnCampaignBehavior.cs:162-179`),
  lup z pol bitew w okolicy (przedmioty pokonanych), zakupy u **pasera** w miescie z wysoka przestepczoscia
  (BK Criminality) - po cenie rynku z SupplyDemand, z ich zlota (dzis zloto band nie jest wydawane).
- Przy narodzinach bandy ludzie dostaja to, co jest w magazynie kryjowki; reszta idzie w `civilian`
  roster szablonu (lachy, palki) - **brak plyty w magazynie = brak plyty na grzbiecie**.
- Technicznie: zamiast przerabiac XML na sztywno - nadpisanie sprzetu agenta przy spawnie misji
  (mamy juz haki: `HideoutSpawnShim`, `DressCode`, `CaptiveRags`) albo wpisanie bandom zbrojowni DTE
  (`PartyArmories[party.Id]`) z magazynu kryjowki - DTE juz umie ubrac oddzial ze zbrojowni.
- Awans bandyty wymaga sztuki z magazynu (wzorem `upgrade_requires`): giermek zostaje rycerzem dopiero,
  gdy banda zdobyla plyte i konia.
- Pokonanie bandy oddaje graczowi DOKLADNIE to, co nosili i wiezli (a nie lup z niczego z HideoutPurge).

### P5. Zamknac darmowe zrodla, ktore juz znamy
- **HideoutPurge.Reprisal**: scalanie zostawia puste sloty -> nocny odrost. Albo nie niszczyc band
  (tylko wspolny rozkaz pogoni - EngageParty dziala na kazda z osobna), albo przy P3 zabrac
  scalonych z puli. Najprostsze: scalac, ale zaznaczyc kryjowke jako "wyczerpana" na N dni.
- **BK BanditHero**: `ConsiderLeaveHideout` dosypuje +2-6 ludzi dziennie kazdej bandzie w kryjowce,
  `InfestHieout` +8 band ponad limit. Do wylaczenia latka (prefix `UpgradeParty` -> return false,
  albo limit raz na tydzien), po P0 sprawdzic, czy bohaterowie w ogole sie pojawiaja.
- **BK BanditPartiesLimit = 150** - nie dziala na nic; nie ruszac, ale nie liczyc na niego.
- **Darmowe awanse band** (koszt zlota tylko z bohaterem): postfix na `GetGoldCostForUpgrade` /
  `CanPartyUpgradeTroopToTarget` dla band - awans tylko gdy jest sprzet w taborze (laczy sie z P4).
- **PlayerProgress -> rozmiar band**: to celowa skala vanilli; przy P3 rozmiar powinna dyktowac pula
  regionu, nie postep gracza.

### Rekomendowana kolejnosc
1. P0 (spis) - zmierzyc, ile band i ludzi jest teraz i jak rosna przez kilka dni gry.
2. P2 (XML piratow i giermkow) - Jeff widzi efekt od razu, ryzyko minimalne.
3. P1 (sufit gestosci) - jedna liczba w MCM.
4. P5 (HideoutPurge, BK UpgradeParty) - po jednym.
5. P3, potem P4 - duze systemy, osobne zadania z wlasnym planem jak `MODEL-MATERIALOW.md`.
