# m110-stan - cztery nazwane stany sprzetu (sprawny / uszkodzony / zmasakrowany / wrak) i stan lupu z prawdziwych obrazen

Galaz `w-toku/m110-stan` (baza `502833b6` = WGRANIE 104). Mod: Armoury (+ `tools/sprawdz_logi.py`). Tor L (gospodarka, lup, zapis). Parametry ustalal Claude.

Jeff 23.10 ok. 14:45 (o lupie i "peknietej zbroi" DTE): "ma wracac, ale przeciez kazdy pancerz ma durability i powinien byc w takim stanie, jak zadam mu obrazenia,
np. walne goscia w body i zabijam, zeszlo mu z pancerza np. 480 punktow z 2000 ... to zniszczony w sumie". Ok. 15:00 (DECYZJA, zastepuje stopnie co 10%): "zrobmy 4:
rozbity/wrak ponizej 10% hp durability; zmasakrowany powyzej 10%, ale ponizej 40%; uszkodzony powyzej 40%, ale do 80%; sprawny powyzej 80 do 100. I okreslajmy go
nazwami, nie procentami."

## Stan pracy

| Krok | Co | Stan |
|---|---|---|
| A1 | klucze `GearFourStates`, `LootStateFromWounds` (gen_mcm), `Armoury/ModuleData/arm_item_modifiers.xml` (48 modyfikatorow bez grupy), wezel `ItemModifiers` w `SubModule.xml`, opisy MCM | ZROBIONE (`db91048f`) |
| A2 | `GearState.cs` + odbiorcy stanu (ArmouryBehavior, Patches, LootPrices, TooltipCondition, SmithMenu, QuartermasterLaw, AiWear, ArmourQuality, QualityScarcity, FieldCraft) | ZROBIONE (`e4d05e0b`) |
| B1 | `LootWear.cs` (ksiega obrazen misji, stan czesci, linie, hak testu) + SubModuleMain | ZROBIONE (`6f2e78e0`) |
| B2 | BattlefieldLaw (pule, podzial wrakow, dzialka ze stanem, lucky, sakwy, zwrot) + LootTally.FelledPost | ZROBIONE (`86ca8521`) |
| A3 | ksiega gracza: stary wpis (4 pola) ta sama sztuka po STANIE; wytrzymalosc w podpowiedzi tylko z ksiegi | ZROBIONE (`5446fd93`) |
| T1 | grupa progow `m110stan` (STAN-S, STAN-S2, STAN-0..6, STAN-LC) | ZROBIONE (`60c2a80a`; `sprawdz_narzedzie.py` bez uwag; STAN-S i STAN-S2 TAK; log 109f-bitwa5 (DLL 104) - INFO, STAN-1 TAK) |
| R | weryfikacja dwoch recenzji (zgodnosc R1-R8, kod R1-R5) i poprawki - tabela "Weryfikacja recenzji" nizej | ZROBIONE (`89a52303` napisy, `5850a327` GearState i ksiega, `1e9bd829` lup, `af824804` progi) |
| AT | autotest (plan nizej) - gry NIE uruchamialem (zakaz etapu); nic nie wgrane | DO ZROBIENIA (glowna sesja) |

**Build:** Armoury 0 bledow po kazdym commicie (kod wyjscia 0; B1 sprawdzony osobno bez plikow B2); po poprawkach recenzji - kod wyjscia 0, 0 bledow. gen_mcm: Armoury 2335 ustawien.

## Problem

1. Stan sprzetu byl mieszanka: procent w nazwie z ceny modyfikatora ("(30%)"), progi ThresholdWorn/Damaged/Ruined 70/45/20 z Armoury.json, w grupach zbroi dwa stany
   Native (rusty/dented, loose, worn/battered, ripped) i do 8 stanow RBM `*_damage_*` po cenie 0.1 (kolczuga po pierwszym trafieniu w bitwie RBM od razu x0.271 -
   pierwszy zly na liscie to `chain_damage_95`), lup Spoils 4 stany (Plundered 55 / Damaged 40 / Battered 25 / Mangled 10 - w cenie). Bron stawala na 0.3.
2. Lup z ciala nie znal obrazen: stan dawala loteria Spoils (`RollDamageTier` - losowo 10/25/40/55/100), czesc trafiona smiertelnym ciosem byla z automatu wrakiem
   "Mangled" (0.10), od 104 takze czesc "peknieta" DTE (m109-lup Z1) - nawet gdy nikt jej nie trafil.

## Zmiana

### A - cztery stany (klucz `GearFourStates`, domyslnie TAK; NIE = 1:1 jak 502833b6)

| Stan (w grze) | d = ile zostalo z puli | Cena | Kara pancerza i obrazen (krzywa ConditionPenaltyMax 90, wykladnik 2) |
|---|---|---|---|
| Sound (sama nazwa) | d > 80 | bez modyfikatora; dobre jakosci (fine/lordly/legendary) tylko tu | 0 |
| Damaged | 40 < d <= 80 | 0.60 (= dzisiejszy "inferior" - najczestszy zly stan bez zmiany ceny i kary) | -14% (x0.856) |
| Mangled | 10 <= d <= 40 | 0.25 (srodek 10-40) | -51% (x0.494) |
| Wrecked | d < 10 | 0.05 (srodek 0-10; powyzej progu zniszczenia 3%, ponizej 0.10 - "wraki na zlom" jak dotad) | -81% (x0.188) |

- **48 modyfikatorow `arm_<rodzina>_damaged/_mangled/_wrecked`** w `Armoury/ModuleData/arm_item_modifiers.xml` (16 rodzin: plate, chain, leather, cloth,
  cloth_unarmoured, sword, polearm, mace, axe, cheap_weapon, axe_throwing, knife_throwing, spear_dart_throwing, shield, bow, crossbow), BEZ `modifier_group` (wzor
  Spoils `rl_*`; `ItemModifier.Deserialize` laczy z grupa tylko podana - dekompilacja). Kazdy wpis ma `id` i `name` (lata RBM `MergeTwoXmls` czyta oba).
  Pancerz i obrazenia -14/-51/-81 (RBM czyta je jako procent, wiec przy wylaczonym ConditionScalesStats kara ta sama); pola dodawane wprost: Uszkodzony = dzisiejszy
  "inferior" po RBM i QualityRich, Zmasakrowany = "poor", Wrak = poor x 1.5 (miecz/drzewce/buzdygan/topor/tani: szybkosc -2/-4/-6; tarcza: wytrzymalosc -70/-100/-150,
  szybkosc -2/-2/-3; luk: -10/-15/-22, -5/-10/-15, pocisk -3/-6/-9; kusza: -15/-22/-33, -10/-15/-22, -3/-6/-9; bron miotana bez szybkosci). Wagi lupu 13/15/0,
  produkcji 5/5/0 (te same co dzisiejsze 0.6/0.3 - rozklad loterii gry bez zmian). Etykiety inferior/poor/poor.
- **`GearState.Apply()`** (start sesji, PO LootPrices.Apply, PRZED McmSettings/ArmsPricing/StartStock), wszystko w pamieci:
  (a) alias - kazdy stary zly modyfikator 16 grup (cena oryginalna: >= 0.5 uszkodzony, > 0.15 zmasakrowany, reszta wrak), `rl_*` wedlug id (rl_looted - uszkodzony,
  rl_looted_medium i rl_looted_heavy - zmasakrowany, rl_looted_heavy_max - wrak) i companion_weapon/companion_armor (0.4 - zmasakrowany) dostaje nazwe i cene stanu,
  a stary zly modyfikator 16 rodzin takze statystyki arm_* swojej rodziny i stanu (Damage, Speed, MissileSpeed, Armor, HitPoints - recenzja R2: jeden stan w nazwie =
  jedne statystyki; oryginal w kopii, Restore; rl_* i companion sa wspolne dla wszystkich rodzin - statystyki bez zmian, ryzyko 14);
  (b) w 16 grupach zle modyfikatory zastapione przez Uszkodzony i Zmasakrowany (wraku nie ma w zadnej grupie), listy wag lupu i produkcji przebudowane, wpis "bez
  modyfikatora" zostaje; (c) drabinki jakosci (ArmourQuality) i modyfikatory jakosci (QualityScarcity) od nowa. Kopie oryginalow per MBObjectManager (`Prepare`
  PRZED LootPrices: ten sam zestaw obiektow wraca do XML; rl_* lapane przed LootPrices). Blad grupy liczony - grupa zostaje stara; brak ktoregos z 48 modyfikatorow
  albo niepasujace pola - cale A wylaczone, linia "stany NIE".
- **Ksiega gracza** (`arm_condition`) bez zmian formatu: dokladna pula od 100 wzgledem stanu, w jakim sztuka weszla do ksiegi. Stan liczony z
  d = ksiega - (100 - srodek stanu oryginalu) i progow 80/40/10 (`ApplyModifierForCondition`, `WornModifierFor`, `SamePiece`). Zuzycie nigdy nie poprawia - po stanie
  i zawsze (dotad tylko przy ArmourQuality). Komunikat "<Item> is now Damaged."; linia `Zuzycie (m110)`. Naprawy jak dotad (ksiega, start 100).
  Po wczytaniu (recenzja R6, `ResyncWornStates` po WearGroups.Fix): sztuki na grzbiecie z wpisem ksiegi TEJ sztuki dostaja stan z ksiegi (zapis 104 trzymal sztuki z
  ksiega 70-80 bez modyfikatora - stary prog ThresholdWorn 70 - a podpowiedz pokazywala "Damaged"); tylko pogorszenie, bez komunikatu, linie `Zuzycie (m110)` i linia
  "wyrownanie po wczytaniu".
- **Zuzycie o jeden stan do wraku** (wojsko `WearTheTroops`, AI `AiWear.NextWorse`, pierwszy stopien `MildWornModifier` = uszkodzony) - takze bron, tarcze i luki.
  Loteria bez wiedzy o ciele (`PickWornModifier`, `WearTheLoot`) w czterech stanach z dzisiejszymi udzialami (> 55 uszkodzony, > 30 zmasakrowany, reszta wrak w
  zbrojach RBM, zmasakrowany w broni - jak dotad 30/50/20 i 30/70/0).
- **Napisy:** nazwa "Damaged X" / "Mangled X" / "Wrecked X" zamiast procentu (`ConditionTagPatch`), sprawny - sama nazwa; podpowiedz "Condition: Damaged",
  panel "Armoury Stats" - stan slowem (gorszy z modyfikatora i ksiegi), "Durability cur / pula" tylko gdy znana dokladnie (ksiega) albo sztuka sprawna; kowal i
  kwatermistrz - slowo stanu. `IsWreck` = stan Wrecked (inne modyfikatory jak dotad); `HarnessWreck` - ponizej 10% (dokladnie 10% to Mangled, do naprawy);
  `ConditionScaling.ConditionOf` = wartosc stanu. Wrak w napisach kowala ("Smith: not for coin", "Pick a piece", "Mend for coin"), naprawy wojska, kwatermistrza i w
  opisach MCM (Spoils Quartermaster Repair, Smith Mend From Market, Mend Metal By Kind) - "Wrecked - less than a tenth of its strength left" (recenzja R1;
  `LootPrices.WreckWordEn` / `WreckDefEn`); tekst kwatermistrza: "mend the Damaged and Mangled spoils of battle ... back to sound" zamiast "remove the 'Plundered' mark".
- **Prog zniszczenia bez dokladnej wytrzymalosci** (recenzja R7, `GearState.BelowDestroy`): sztuka ze stanem ginie, gdy caly przedzial stanu lezy na progu
  LootMinConditionPercent albo nizej - wrak (0-10%) dopiero od progu 10, jak w 104 (wrak 0.10); cena wraku 0.05 nie niszczy go juz przy progu 5-9 (`CleanseTrash`,
  `AppendWrecks`). Czesc z ciala z ksiega obrazen - wedlug d (LootWear). U Jeffa prog 3 - bez skutku.

### B - stan lupu z obrazen (klucz `LootStateFromWounds`, domyslnie TAK; wymaga A i BattlefieldLawEnabled; NIE = 1:1)

- **`LootWear.Logic`** (MissionLogic, bez latki): `OnAgentBuild` - wrogowie strony gracza (ludzie, nie bohaterowie), stan "przed" ze srodka przedzialu tylko,
  gdy ktoras czesc 5-11 nie jest sprawna (RBM obniza SpawnEquipment w walce); `OnAgentHit` - surowy cios = InflictedDamage + AbsorbedByArmor, pocisk x
  MissileArmorWearPercent, czesc ciala -> slot (`FieldCraft.ArmorSlotFor`), trafienie konia -> uprzaz jezdzca x HarnessWearFactor, blok tarcza pomijany - jak u gracza.
  Ksiega czyszczona na koncu misji. Cios smiertelny jest policzony przed rozliczeniem ciala (gra wola OnAgentHit przed Die).
- **Czesc z ciala:** d = przed - obrazenia x 100 / ArmorPool (ta sama pula co gracz), przyciete 0-100; stan wedlug progow; d <= LootMinConditionPercent - zniszczona.
- **Droga cialo po ciele (BattlefieldLaw):** `LootKillPrefix` pamieta ofiare; `LootTag` - czesc do puli "Ty" albo "zolnierz" (niezaleznie od OwnKillsLootWhole);
  `QueueWreck` (cios) i `LootTally.FelledPost` -> `AddWreck(it, czesc)` (pekniete) - dane rownolegle do wrakow.
  `AfterDtePartyItems`: (i) podzial wrakow - zniszczona przepada, wrak (< 10) zostaje wrakiem (caly dla gracza), lepsza czesc: powalony przez Ciebie/towarzysza - do
  kolejki w calosci, przez zolnierza - trzecia kapitana na przedmiot (WholeShare) jednym zaokragleniem ze zwyklym lupem tego przedmiotu (recenzja R4: dzialka z
  "zwykly lup + czesci" minus dzialka samego zwyklego lupu - pojedyncza czesc obok zwyklego lupu nie ginie w zaokragleniu do zera; przy OwnKillsLootWhole NIE -
  osobno jak dotad), reszta do zbrojowni wojska ZE SWOIM STANEM (arm_* - recenzje zgodnosc R4 / kod R1: dotad sprawna, wartosc z niczego); bez zbrojowni - jak dotad;
  (ii) dzialka przedmiotu z danymi - najpierw sprawne sztuki ze zbrojowni, kazda dostaje czesc (najpierw pula "Ty", potem losowo "zolnierz"); zniszczona zdjeta ze
  zbrojowni (nie do kolejki); inna - do kolejki z modyfikatorem Spoils stanu (rl_looted / rl_looted_heavy / rl_looted_heavy_max - Spoils rozpoznaje lup tylko po nich),
  gorszym z (stan z obrazen, stan zdjetej sztuki); sprawna zachowuje swoj modyfikator; reszta pul - zostaje u wojska.
  `AfterGenerateLoot`: `RegisterLuckyElement` dla kazdej sztuki B (takze "id|" dla sprawnej) - `ApplyDamageToLoot` Spoils zostawia ja 1:1. Sakwy (`FlushShareToBaggage`,
  `FlushMountsToBaggage`) - sprawna sztuka B bez loterii; `WearTheLoot` (kryjowka) odejmuje je od przybylych. Zwrot (`DropShare`) - sztuki B wracaja jako oryginaly
  ze zbrojowni, B z podzialu wrakow przepadaja jak wraki. Pule czesci puszczane ("zostaje u wojska") takze przy braku rekordu DTE i po wyjatku (recenzja kod R5 -
  dotad zostawaly do nastepnej bitwy). Prog zniszczenia czytany raz na bitwe (Tally.Min) i wypisany w linii bitwy (recenzja R8).
- Lup bez wiedzy o ciele (symulacja, Send troops, bron, tarcze, sztuki bez danych) - jak dotad, ale w czterech stanach (alias rl_*).

### C - zapis i cofniecie

- Bez nowych typow i kluczy zapisu. Gra zapisuje modyfikator jako StringId; alias i zamiana grup nie wchodza do zapisu. Nowe id `arm_*` trafiaja do istniejacych
  napisow (`arm_condition`, ksiega AiWear, ArmouryZb).
- Stary zapis: stare modyfikatory pokazane jako stan (alias); ksiega bez przeliczenia (stan nigdy nie poprawia sie sam); stary wpis 4 pola - ta sama sztuka po stanie.
- **Cofniecie = podmiana samej `Armoury.dll` na 104; `arm_item_modifiers.xml` i `SubModule.xml` z m110 ZOSTAJA w grze.** 104 widzi arm_* jako zle modyfikatory po
  cenie (60% / 25% / 5%), wrak <= 0.10, bez grup. Bez XML (przywrocony caly folder 104) sztuki arm_* traca stan (darmowa naprawa); HIPOTEZA (niesprawdzona): NRE w
  StealthEquipment - wpisac do procedury wgrywania i cofania.

## Klucze MCM

- `GearFourStates` (Wear and tear, obok Threshold*) i `LootStateFromWounds` (The law of the battlefield, obok Wreck Salvage Enabled / Extra Broken Armor Wrecks) -
  domyslnie TAK, w Armoury.json Jeffa ich nie ma (wejda domyslne z kodu). Zmiana GearFourStates - od nastepnego wczytania.
- Poprawione opisy (prawda przy obu stanach klucza): Loot Price Follows Condition, Show Condition Percent, Threshold Worn/Damaged/Ruined, Wreck Salvage Enabled,
  Loot Min Condition Percent, Wrecks To Scrap.

## Linie logu (Log.Info)

- `Stany sprzetu (m110): start | klucz GearFourStates TAK | XML arm_* 48/48 | grupy zamienione 16/16, pominiete (brak grupy) brak, bledy brak | stare zle -> stany:
  uszkodzony a, zmasakrowany b, wrak c (rl_* 4/4, companion 2) | progi 80/40/10, wartosci 60/25/5 | kontrola: 480/2000 -> 76% uszkodzony, 80 uszkodzony, 80.1 sprawny,
  40 zmasakrowany, 10 zmasakrowany, 9.9 wrak - OK | lup z obrazen TAK (latki DTE 2/2, Spoils lucky TAK) | potkniecia 0 | statystyki starych zlych jak arm_* a/b.`
  (klucz NIE: `... | stany NIE - jak 104 ...`; a/b - recenzja R2, b = uszkodzony + zmasakrowany + wrak)
- `Stany sprzetu (m110): rynsztunek i sakwy gracza | sprawny n, uszkodzony n, zmasakrowany n, wrak n | stare (przemapowane) n, arm_* n, rl_* n | ksiega: wpisow n,
  z przesunieciem n.` (raz na sesje, PO WearGroups.Fix - recenzja R3; stan z zapisu, przed wyrownaniem)
- `Stany sprzetu (m110): wyrownanie po wczytaniu | sztuk z ksiega n, nowy stan n, bledy n.` (raz na sesje, klucz TAK - recenzja R6)
- `Stany sprzetu (m110): zapis | gracz arm_* n (rynsztunek a, sakwy b), rl_* n, stare n | ksiega wpisow z arm_* n.` (przy zapisie, tylko pod flaga)
- `Zuzycie (m110): slot 6 <id> -> <mod> | ksiega 71.3%, poczatek sprawny, liczony 71.3% -> uszkodzony (byl sprawny).`
- `Stan lupu (m110): czesc tulow <id> (lup, Ty): przed sprawny (100%), obrazenia 480/2000, po 76.0% -> uszkodzony | do Ciebie.` (12 na bitwe, pod flaga wszystkie;
  drogi: do Ciebie / dzialka / zbrojownia wojska / wrak do Ciebie / przepadla / zostaje u wojska / zniszczona)
- `Stan lupu (m110): bitwa N | ksiega: wrogow a (trafionych e), trafien h, przed nie-sprawnych p, czesci z ksiega c | lup z cial [sprawny/uszkodzony/zmasakrowany/
  wrak/zniszczony]: do gracza a/b/c/d/e, zostaje u wojska f | cios [...]: a/b/c/d/e -> do Ciebie x, dzialka y, zbrojownia z, wraki w, przepadly p | pekniete [...]:
  ... | bez ksiegi: lup n, cios/pekniete n | koniec: <lista trofeow Spoils / sakwy - ... / zwrot ... / ekran gry> (lucky k/k) | prog zniszczenia N% |
  pomiar X ms (trafien h) | potkniecia S.` (przy oddaniu kolejki; nastepna bitwa przed oddaniem - "bez rozliczenia"; "prog zniszczenia" - recenzja R8)
- `Stan lupu (m110): test - bitwa N: sztuczny stan przed bitwa dla n wrogow (sprawny a, uszkodzony b, zmasakrowany c, wrak d).` (pod flaga)

## Progi - grupa `m110stan`

`python -I tools/sprawdz_logi.py --grupa m110stan [<Armoury-*.log>] [--baza-doby <log poprzedniego biegu z linia zapisu>]` - opis w `--help`.
STAN-S self-test wyrazen, STAN-S2 test syntetyczny (granice 80/80.01/40/40.01/10/9.99, 480/2000 -> 76 uszkodzony, pula 2439, zniszczona <= 3, mapowanie cen, logi
dobry/zly/bez startu, STAN-LC zgodny/niezgodny), STAN-0 start, STAN-1 bledy i potkniecia, STAN-2 arytmetyka czesci, STAN-3 tozsamosci bitwy, STAN-4 pokrycie pod
flaga, STAN-5 zuzycie gracza, STAN-6 pomiar, STAN-LC cofniecie.
Po recenzjach (`af824804`): STAN-0 - linie m110 bez linii startu NIE (log bez linii m110 - INFO), przy "lup z obrazen TAK" latki DTE 2/2 i Spoils lucky TAK,
statystyki starych zlych a/a; STAN-2 - zniszczona przy d <= prog z linii swojej bitwy (+-0.06), nie zniszczona przy d > prog (czesc bez linii bitwy - d <= 10);
STAN-5 - linia "wyrownanie po wczytaniu" z bledy 0; STAN-1 - takze ERROR ResyncWornStates; STAN-S2 - nowe przypadki zly2 (latki 1/2, zniszczona 8.0 przy progu 3,
nie zniszczona 2.0, bledy wyrownania), zly3 (statystyki 50/59), bez linii m110 (INFO) i bez startu z linia bitwy (NIE).

## Hak autotestu

Plik `Documents/Mount and Blade II Bannerlord/CrashScribe/armoury-stan.flag`, tylko przy LogGate.Mode 1/2: wrogowie dostaja w OnAgentBuild sztuczny stan "przed"
po kolei (sprawny, uszkodzony, zmasakrowany, wrak) - tylko liczony stan lupu (nie rynsztunek, nie walka); linie czesci bez limitu, linia "zapis". Razem z
`armoury-lup.flag` (6 jezdzcow T5) - pokrycie uprzezy. W grze Jeffa nic nie robi.

## Plan autotestu (dla glownej sesji)

Wczesniej: kopia logow Jeffa z dnia. DLL "A" = commit `e4d05e0b` (albo koncowa z kluczem LootStateFromWounds NIE, jesli narzedzie ustawia MCM), DLL "A+B" = koniec galezi.
Do folderu gry ida takze `Modules/Armoury/ModuleData/arm_item_modifiers.xml` i `Modules/Armoury/SubModule.xml` (skrypty wgrywania kopiuja arm_*.xml i SubModule.xml).

| Bieg | Parametry | Co sprawdzic |
|---|---|---|
| bitwa-A | DLL A; zapis autotest-F-d240, 4 bitwy, flaga lup | STAN-0/1/5; m109lup LUP-J0..J10 TAK; linia "Zbroja z kuzni: drabinka jakosci grupy ..." z arm_* (slaba arm_*_damaged, zepsuta arm_*_mangled) |
| bitwa | DLL A+B; flagi lup i stan | STAN-0..6 TAK; m109lup TAK; inne grupy bitewne bez nowych NIE (wartosc lupu - INFO) |
| bitwa-real | DLL A+B; tylko flaga lup | rozklad stanow i wartosc lupu zbroi na bitwe wobec 109f-bitwa5 - liczby dla Jeffa |
| p20 | prolog 20 dob | STAN-0/1; E8/W i P2 (zuzycie AI) wobec 109f/109g-p20 - przesuniecia wyjasnione (wrak broni, progi 80/40/10) |
| wczyt | zapis autotest-dlugi + 20 dob | linia rynsztunku z przemapowanymi starymi modyfikatorami; STAN-0/1; brak nowych kluczy zapisu |
| lc-a | nowa DLL: bitwa + zapis (flaga stan) -> DLL 104 (fd209d68) z NOWYM SubModule.xml i arm_item_modifiers.xml: wczytanie + 20 dob -> nowa DLL: wczytanie | `--grupa m110stan <log ostatniego wczytania> --baza-doby <log z linia zapisu>` STAN-LC TAK; bieg na 104: STAN-LC (bez m110) ERROR 0; 0 bledow CrashScribe |
| lc-b (opcja) | jak lc-a, ale SubModule.xml z 104 (bez XML) | wczytanie bez wywrotki; sztuki arm_* staja sie sprawne (HIPOTEZA NRE StealthEquipment) |

Dodatkowo po recenzjach (sprawdzic w tych samych biegach):
- **wszystkie biegi:** linia startu "statystyki starych zlych jak arm_* a/b" z a = b (STAN-0); w bitwie z flaga lup linie czesci "zbrojownia wojska" przy stanie
  uszkodzony/zmasakrowany (sztuka do zbrojowni ze stanem - w logu tylko droga; stan sztuki w zbrojowni widac po "Zbroja ... drabinka" / WearTheTroops, jesli jest linia);
  linia bitwy "prog zniszczenia 3%" i STAN-2 TAK.
- **wczyt (zapis autotest-dlugi = zapis 104):** linia "wyrownanie po wczytaniu | sztuk z ksiega n, nowy stan k, bledy 0" - k > 0 tylko, gdy w zapisie jest sztuka z
  ksiega 70-80 bez modyfikatora (albo 40-45 z lepszym modyfikatorem niz stan); linie "Zuzycie (m110)" z wyrownania - STAN-5 TAK; brak komunikatu "is now" na ekranie.
- **bitwa / bitwa-real:** w linii E8/A "dzialka gracza" i w liniach czesci "dzialka" przy pojedynczych czesciach zolnierza obok zwyklego lupu tego samego przedmiotu
  (jedno zaokraglenie - recenzja R4) - INFO dla Jeffa (wartosc lupu na bitwe wobec 109f-bitwa5).
- **NIEPEWNE (z recenzji, nie do sprawdzenia w kodzie):** brak - kazda uwaga rozstrzygnieta w kodzie albo w dekompilacji (tabela "Weryfikacja recenzji"); dalej
  otwarta tylko HIPOTEZA NRE StealthEquipment (lc-b).

## Ryzyko / co sprawdzic (kontrola calosci, CLAUDE.md 8.0)

1. **Walka (RBM w trakcie bitwy):** RBM obniza zbroje trafionego wedlug listy grupy - teraz przez Uszkodzony (x0.856), potem Zmasakrowany (x0.494) i na tym staje
   (dekompilacja RBMCombat Utilities :788). Dotad kolczuga po pierwszym obnizeniu x0.271 (`chain_damage_95` 0.1 pierwszy zly), plyta do x0.271. Kazda bitwa, tylko
   przy kluczu TAK.
2. **Ceny starych modyfikatorow przesuwaja sie** (alias): "poor" 0.3 -> 0.25, rl_looted 0.55 -> 0.6, rl_looted_medium 0.40 -> 0.25, wraki 0.10 -> 0.05,
   companion 0.4 -> 0.25; kara statystyk i koszt napraw (1 - cena) razem z cena. Przesuniecie wartosci, nie zloto z niczego. Po recenzji R2 stare zle 16 rodzin
   maja tez statystyki arm_* (przy ustawieniach Jeffa - RBM + QualityRich + ConditionScalesStats TAK - zmiana tylko: cracked_shield szybkosc -1 -> -2, wraki RBM
   *_damage_* pancerz z krzywej jak dotad; przy ConditionScalesStats NIE takze pancerz i obrazenia, np. dented_plate -10 -> -14, rusty_plate -20 -> -51).
3. **Bron, tarcze i luki moga zostac wrakiem przez zuzycie** (gracz, wojsko, AI) - kowale miasta ich nie odnowia (WrecksToScrap), `AiWear.Mendable` pomija wraki.
   Sprawdzic w p20, czy wraki broni AI nie zalegaja (decyzja Claude - do odwrocenia przez Jeffa).
4. **Gracz:** pierwszy stan juz przy 80% (dotad 70), dobra jakosc tracona przy pierwszym zlym stanie (zuzycie nigdy nie poprawia - teraz zawsze, nie tylko przy
   ArmourQuality); sztuka dokladnie na 10% to Mangled, nie wrak.
5. **Ekonomia B:** lup z cial glownie sprawny/uszkodzony (wiecej wart niz loteria Spoils - szacunek 2-4x wartosci zbroi na sztuke - zmierzy bitwa-real); lepsze czesci
   z ciosu i pekniete: zabici przez gracza w calosci, przez zolnierzy 1/3 (WholeShare na przedmiot, jednym zaokragleniem ze zwyklym lupem przedmiotu - recenzja R4),
   reszta do zbrojowni wojska ZE SWOIM STANEM (recenzja R1/R4; dotad wszystkie jako wraki 0.10 dla gracza - decyzja Claude, do odwrocenia). Liczba sztuk ta sama;
   zmienia sie odbiorca i stan. Zamknieta ekonomia: sztuka czesci z ciosu/peknieta to fizyczna sztuka z ciala (DTE jej nie liczy) - ta sama co dotad szla jako wrak;
   w zbrojowni ma swoj stan (wojsko nie dostaje wartosci z niczego). Gracz traci dzisiejsze wraki tych czesci (zlom 0.10), gdy 2/3 idzie do wojska.
6. **Cudzy kod (dekompilacja):** DTE `OnAgentRemoved(__0 ofiara, __1 zabojca)`, `AddLootedItem` w biegu OnAgentRemoved, `HandlePartyItems` -> `ReturnItemsToDestination`
   -> `ArmyArmory.AddItemToArmory` (sztuki bez stanu); Spoils `GenerateLootFromUnitList` czysci lucky na poczatku (`ClearLuckyItems` :518), `ApplyDamageToLoot`
   zachowuje zarejestrowane 1:1 (klucz "id|mod"); pola `ItemModifierGroup._itemModifiers / _lootDropItemModifierScores / _productionDropItemModifierScores`
   (MBList : List) - gra 1.4.8; linia startu liczy, co weszlo.
7. **Martwe wpisy lucky Spoils** z jego loterii moga zachowac 1:1 sztuke spoza B o tym samym kluczu - tak bylo, nie ruszane.
8. **Zbrojownia wojska:** zwykly lup, ktory zostaje u wojska (DTE `AddItemToArmory(item)`), jest bez stanu - jak dotad; czesci z ciosu i pekniete zolnierza
   (nowa droga m110) - ze stanem arm_* (recenzja R1/R4). DTE trzyma modyfikatory w zbrojowni (`ArmyArmory.TryNormalizeArmoryElement` - dekompilacja).
9. **Grupy sprawdz_logi:** E8/W i P2 (zuzycie AI) oraz grupy ekonomii moga pokazac przesuniecia; m109lup zostaje zielona z zalozenia (czesci przeniesione z wrakow do
   kolejki licza sie jako "dzialka" w bloku B, w bloku C nie sa wrakami; A i J7 bez zmian - LootTally liczy klasyfikacje DTE).
10. **Wydajnosc:** OnAgentHit - jeden lock + TryGetValue na trafienie; OnAgentBuild - slownik tylko dla agentow z nie-sprawna czescia (zbior wrogow - wszyscy wrogowie
    misji); STAN-6.
11. **Zasady:** stan misji czyszczony (koniec misji, LReset); kopie oryginalow per MBObjectManager; bledy liczone per grupa i per sztuka (bez globalnego wylacznika);
    brak XML wylacza A jawnie (linia NIE). Hooki autotestu tylko pod flaga i LogGate 1/2.
12. **Pokrycie B:** tylko ciala, ktore DTE rozlicza (rozpoznanie: 60-73% cial DTE nie rozlicza - uciekajacy) i tylko zbroja (bron i tarcze z cial - loteria Spoils jak
    dotad). Bitwy AI z AI bez zmian.
13. **Regula Spoils "tier przedmiotu ponad tier klanu = gorszy stan" nie dziala dla lupu z cial** (recenzja zgodnosc R5; Spoils `EquipmentDamageModel.RollDamageTier`:
    roznica >= 3 - zawsze heavy_max, >= 1 - bez sprawnego). Stan z obrazen zastepuje losowanie Spoils (zadanie Jeffa), wiec zbroja T5-T6 z ciala trafia do gracza z
    klanem T1 zwykle sprawna albo uszkodzona (pule rzedu 2000+ pkt wobec obrazen jednej bitwy). DO DECYZJI JEFFA: zostawic (stan = obrazenia) albo dolozyc gorna
    granice stanu wedlug roznicy tierow (np. roznica >= 3 - najwyzej zmasakrowany). Bez zmiany w kodzie.
14. **rl_* i companion_* maja wspolne statystyki dla wszystkich rodzin** (recenzja zgodnosc R2, czesc nienaprawiona): "Damaged Shield" z lupu Spoils (rl_looted)
    ma wytrzymalosc -40, a z zuzycia (arm_shield_damaged) -70; luk z rl_looted: szybkosc -2 / pocisk -2 / -40 HP wobec arm_bow_damaged -5 / -3 / -10; wrak z
    rl_looted_heavy_max: -120 HP, szybkosc -7 wobec np. arm_bow_wrecked -22 / -15. Pancerz i obrazenia przy ConditionScalesStats TAK (Jeff) sa rowne (krzywa stanu).
    Tak bylo i w 104 (te same rl_*), teraz widac to, bo nazwy sa te same. Naprawa wymaga nowej latki na getterach EquipmentElement (GetModified*ForUsage - podmiana
    rl_* na arm_* rodziny przedmiotu) albo innego znacznika lupu niz rl_* (Spoils rozpoznaje lup tylko po rl_*) - DO DECYZJI JEFFA, poza tym etapem.
15. **Wyrownanie po wczytaniu (recenzja R6):** pierwsze wczytanie kampanii Jeffa (zapis 104) moze obnizyc nazwe i cene sztuk na grzbiecie z ksiega 70-80% do
    "Damaged" (60% wartosci, kara -14%) - to stan, ktory ksiega juz zna i ktory podpowiedz juz pokazywala; bez komunikatu na ekranie (linie w logu).
16. **Prog zniszczenia (recenzja R7):** przy LootMinConditionPercent 10-12 wraki bez dokladnej wytrzymalosci gina jak w 104; przy 5-9 juz nie gina (cena 0.05 nie
    jest progiem); czesci z ksiega - wedlug d. U Jeffa 3 - bez skutku.

## Odstepstwa od projektu (etap PROJEKT)

1. `GearState.Prepare()` przed LootPrices.Apply (dodatkowa linia w OnSessionLaunched): nowa sesja w tym samym MBObjectManager wraca do oryginalow, a ceny rl_* z XML
   Spoils sa lapane przed LootPrices (inaczej kopia rl_* trzymalaby cene LootPrices z pierwszej sesji).
2. `SamePiece` (stary wpis ksiegi, 4 pola) przy stanach porownuje STAN, nie konkretny modyfikator - stary rusty i arm_*_mangled to ta sama sztuka (inaczej ksiega
   resetowala sie przy pierwszym odczycie starego zapisu).
3. "Armoury Stats": wiersz Durability tylko, gdy wytrzymalosc znana dokladnie (ksiega bila sztuke) albo sztuka sprawna - lup "Damaged" nie ma dokladnej liczby.
4. Linia czesci: "po 76.0%" (1 miejsce po przecinku zamiast "po 76%") - przy granicy 80/40/10 calkowita liczba dawala falszywy stan w progu STAN-2.
5. Linia bitwy: dodane "wrogow a (trafionych e)", "czesci z ksiega c" i drogi ciosu/peknietych "wraki w, przepadly p" (czesci wraku bez podzialu - przegrana, limit
   400 wrakow) - zeby STAN-3 domykal kazda czesc. Linia testu: "test - bitwa N: sztuczny stan ...".
6. Pule czesci w LootTag niezaleznie od OwnKillsLootWhole (przy wylaczonym - dzialka ze sredniej, czesci dalej najpierw z puli "Ty").
7. Tarcza szybkosc -2/-2/-3 jak w liczbach projektu (dzisiejszy "poor" RBM ma -1 - Zmasakrowany bylby lepszy od Uszkodzonego).
8. STAN-LC: porownanie linii "zapis" z bazy (`--baza-doby`) z pierwsza linia rynsztunku logu; log bez linii m110 (bieg na DLL 104) - ERROR 0 INFO, inaczej NIE.
   STAN-2 "zniszczona" sprawdzane jako d <= 10 (LootMinConditionPercent nie ma w logu) - po recenzji R8: wedlug "prog zniszczenia N%" z linii bitwy.

## Weryfikacja recenzji (23.10, etap R)

Kazda uwage najpierw probowalem obalic (kod galezi, dekompilacja gry 1.4.8, RBM, DTE, Spoils). Werdykt: PRAWDA / NIEPRAWDA / NIEPEWNE.

| Uwaga | Werdykt | Dowod | Co zrobione |
|---|---|---|---|
| zgodnosc R1 = kod R2 (napisy "Mangled" dla wraku) | PRAWDA | grep: SmithMenu :614 :1047 :1058 :1131, MendMaterial :617, SpoilsSeal :944 :1098, opisy MCM | `89a52303` - przy kluczu "Wrecked ..."; kwatermistrz "Damaged and Mangled spoils"; NIE - napisy 1:1 |
| zgodnosc R2 (rozne statystyki w jednym stanie) | PRAWDA (przyklad dull_sword NIEPRAWDA) | RBM `MergeTwoXmls` (RBM_XML_TAG) usuwa wezel Native o tym samym id i name - dull_sword ma speed 0 z RBM, QualityRich daje -2 = arm_sword_damaged; prawdziwe roznice: cracked_shield -1/-2, pancerz i obrazenia przy ConditionScalesStats NIE, rl_* / companion | `5850a327` - stare zle 16 rodzin dostaja statystyki arm_* (kopia, Restore, linia startu a/b); rl_* / companion - ryzyko 14, do decyzji Jeffa |
| zgodnosc R3 (linia rynsztunku przed WearGroups.Fix) | PRAWDA | `GearState.Apply` (OnSessionLaunched :2223) przed `WearGroups.Fix` (:2347); `Add` pomija !Handles | `5850a327` - `GearState.SessionLine()` po WearGroups.Fix |
| zgodnosc R4 = kod R1 (zbrojownia bez stanu, osobne zaokraglenie) | PRAWDA | `SplitWrecks` `new EquipmentElement(kv.Key)`; `WholeShare(1, 0, 1, 33)` = 0; DTE trzyma modyfikatory (`TryNormalizeArmoryElement`) | `1e9bd829` - do zbrojowni z arm_* stanu; jedno zaokraglenie ze zwyklym lupem (roznica WholeShare) |
| zgodnosc R5 (regula tieru Spoils) | PRAWDA (fakt), zgodne z zadaniem | Spoils `RollDamageTier` (tierGap >= 3 - heavy_max) dotyczy losowania, ktore zadanie zastepuje | bez kodu - ryzyko 13, do decyzji Jeffa |
| zgodnosc R6 (podpowiedz "Damaged" przy czystej nazwie) | PRAWDA | TooltipCondition: stan = max(modyfikator, ksiega); ApplyModifierForCondition tylko przy zuzyciu | `5850a327` - `ResyncWornStates` po wczytaniu (tylko ta sama sztuka, tylko pogorszenie, bez komunikatu) |
| zgodnosc R7 = kod R4 (prog zniszczenia po cenie 0.05) | PRAWDA (uspione - prog Jeffa 3) | `CleanseTrash` :822, `AppendWrecks` :1594 - cena x 100 <= prog | `5850a327` + `1e9bd829` - `GearState.BelowDestroy` (gorna granica stanu, wrak od 10 jak 104) |
| zgodnosc R8 + kod R3 (progi testu) | PRAWDA | STAN-2 d <= 10; STAN-0 INFO bez startu; `badk` bez latki/lucky | `1e9bd829` (prog w linii bitwy) + `af824804` (STAN-0, STAN-2, STAN-5, testy syntetyczne) |
| kod R5 (`records == null` przed DropPools) | PRAWDA | `AfterDtePartyItems` :897 `return` bez DropPools; tak samo po wyjatku | `1e9bd829` - DropPools przy braku rekordu i w catch |

NIEPEWNE: brak. Sprawdzone przy okazji weryfikacji: `LoadBehaviorData` (SyncData) biegnie przed `OnSessionStart` (Campaign.cs :1429 wobec :1671) - ksiega jest
wczytana, gdy biegnie wyrownanie; `Hero.CheckInvalidEquipmentsAndReplaceIfNeeded` biegnie po OnSessionStart (:1672).

## Status

DO SPRAWDZENIA - kod, klucze, linie, progi i build gotowe na galezi; autotest (plan wyzej) i decyzja o wgraniu - glowna sesja. Nic nie wgrane.
