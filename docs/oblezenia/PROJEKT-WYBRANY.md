# PROJEKT-WYBRANY - oblezenia i sily (14.10.2026, redakcja po glosowaniu)

Odpowiedz na slowa Jeffa 14.10 po biegu dwuletnim fin30: "przeciez to tak nie moze wygladac, cos jest zle, sily nie sa rowne, oblezenia sa za szybkie
i za latwe, musimy zmienic oblezenia albo sily albo to i to". Podstawa: `PROJEKT-OBA.md` (zwyciezca) z przeszczepami z `PROJEKT-OBLEZENIA.md` i
`PROJEKT-SILY.md`, rozpoznania `ROZPOZNANIE-CZAS/SILY/MODY.md`, glosy trzech sedziow, szacunek redaktora (`robocze\wybrany\`).
To projekt: kod nie jest zmieniony, nic nie wgrane, bez commitow. Znaczniki: [P] pomiar, [K] kod (plik:linia), [U] ustawienie, [S] szacunek.
Rok = 364 doby; gra liczona pod 84 (x4.33). Skroty: G = gra `dec32\cs_full`, INT = nasze drzewo `zb\int` (w-toku/zbior-e8), BK = `e4tools\bkfull`.

## 0. Dla Jeffa (prosto)

1. **Co jest zle [P]:** w 2 lata padlo 57 twierdz, w tym 7 stolic; ze 140 szturmow (dwa biegi) nie odparto ani jednego. Zamek pada po ok. 12 dobach, miasto po 14.
2. **Dlaczego (sprawdzone w kodzie gry):** (a) gra nie pozwala szturmujacym sie cofnac - szturm trwa, az obroncow zabraknie, wiec armia 3:1 wygrywa zawsze;
   (b) cierpliwosc oblegajacych gra liczy pod rok 84 dni - juz od ok. 6. doby mury licza sie na KORZYSC atakujacego (110 ze 113 decyzji o szturmie);
   (c) gdy w czasie szturmu nadciaga odsiecz, zaloga wychodzi w pole i traci mury - tak padla Krolewska Przystan (1156 obroncow w polu na 4409 ludzi Robba).
3. **"Sily nie sa rowne":** mury dzis niczego nie wyrownuja (pkt 2), Reach ma 3.2 raza wiecej pieniedzy na zold niz Dorne, a 11-16 nowych panstw w Essos
   to hrabstwa, ktore BannerKings robi z rodow odchodzacych z lennem.
4. **Oblezenia - zmieniamy trzy rzeczy.** OB1: cierpliwosc wedlug naszego roku (1 dzien = 1 dzien: 4 doby -> 17 dob), mury nigdy nie pomagaja atakujacemu,
   w stolicy i ostatniej twierdzy krolestwa licza sie zawsze w pelni. OB2: szturm moze byc odparty - gdy armia straci 17-27% ludzi (kazda armia ma inna
   wytrzymalosc), a mury sa obsadzone, cofa sie i 60 dob nie wraca. OB4: armie AI przygotowuja oblezenie 2 razy dluzej, miasto 1.5 raza dluzej niz zamek, stolica 2.5 raza.
5. **Sily:** OB3 - gdy do szturmu dochodzi odsiecz, a wiekszosc obroncow to ludzie twierdzy, bija sie z murow, nie w polu. S9 - BK nie robi juz krolestw
   z rodow odchodzacych z lennem (rod zostaje niezalezny, jak w grze). Lordowie wchodzacy do murow (S7) i patrole BK tylko w pokoju (S4) - po tescie.
6. **Czego sie spodziewac [S, na biegu fin30]:** zamiast 57 zdobyc ok. 25-28; ok. polowa szturmow AI odparta; zamek pada po ok. 16 dobach (13-26), miasto po ok. 22
   (18-32); stolice prawie nigdy; zadne krolestwo nie zniknie od szturmu; Krolewska Przystan zostaje; nowych panstw najwyzej 3. Reach dalej bierze zamki Dorne przy 10:1 (ok. 5 zamiast 8).
7. **Twoje oblezenia sie nie zmieniaja** (sam decydujesz, tempo machin z 17.09 zostaje, Twoj szturm bez OB2/OB3). Twoje twierdze bez Ciebie bronia sie lepiej.
8. **Ryzyko:** swiat zwalnia (Essos prawie stoi), a za duzo odparc wyglada na glupie AI - stroje to jednym kluczem po biegu rocznym. Pieniadze Reach-Dorne: osobny tor potem.
9. **Twoja zgoda tylko na jedno (S10):** szanse buntow i wojen domowych w Diplomacy x84/364 (liczone "na dobe", wiec w roku 4.33 raza za czeste) - plik obcego moda,
   tylko z kopia .bak; bez zgody zostaje jak jest. S9 robie naszym wylacznikiem (pliki BK nietkniete) - jesli nie chcesz, powiedz.

## 1. Glosy i wybor

| Projekt | Sedzia 1 | Sedzia 2 | Sedzia 3 | Suma | Wybrany przez |
|---|---|---|---|---|---|
| **PROJEKT-OBA** | 6 | 7 | 7.5 | **20.5** | 2 sedziow |
| PROJEKT-OBLEZENIA | 7 | 6 | 6 | 19 | 1 sedzia |
| PROJEKT-SILY | 4 | 4 | 4 | 12 | - |

**Zwyciezca: PROJEKT-OBA** (OB1 cierpliwosc x dni roku/84 + stolica, OB2 odparcie, OB3 zaloga z murow, rozejm 60 dob). Glowna wada wskazana przez wszystkich
trzech: nie odpowiada na "za szybkie" (mediana zdobycia ok. 10 dob, krocej niz dzis). Redakcja rozstrzyga spory tak:

| Spor | Glosy | Decyzja redakcji i powod |
|---|---|---|
| dlugosc oblezen teraz czy po tescie | sedzia 2: T1 od razu + klasa twierdzy; sedzia 1: dlugie oblezenia (OBLEZENIA); sedzia 3: T1 dopiero, gdy Jeff dalej powie "za szybko" | **teraz (OB4)** - "za szybkie" to polowa slow Jeffa; OB4 i OB2 da sie rozdzielic w logu (OB4 przesuwa dobe decyzji w linii O0/OB0, OB2 zmienia wynik szturmu w linii OB2) |
| krzywa O1 z OBLEZENIA, podloga f >= 1, O5, O6 | sedziowie 2 i 3: nie przenosic (robia armie stojace ok. 100 dob); sedzia 1: za | **nie** - 22 decyzje z murami < 1 nie mialyby szturmu nigdy [P sedzia-dowod], a O6 trzyma takie armie ok. 100 dob. O5 zostaje zapasem (rozdz. 5) |
| S9 przed / z / po OBA | sedzia 2: z albo przed; sedzia 1: osobny tor S; sedzia 3: po tescie | **w tym samym biegu, osobna paczka (CrashScribe), wlasny wylacznik** - bez S9 regula "ostatnia twierdza" chroni hrabstwa BK (wszyscy trzej); skutek S9 czytany z osobnych linii (nowe panstwa, S11); w roku 1 fin30 powstalo tylko 1 krolestwo BK, wiec bieg roczny mierzy oblezenia prawie czysto |
| szacunek OB2 zawyzony? | sedziowie 1 i 2: przy progu 0.20 tylko 27 (nie 33) z 55 zdobyc przekracza prog w fazie murow -> 28-30 zdobyc; sedzia 3: przeliczyc modelem fazy murow | **sprawdzone:** wzorem ciosow gry [K G `DefaultCombatSimulationModel.cs:249-262`] obronca na murach ma 2.2-2.6 x wiecej ciosow niz szturmujacy, a po murach 5-10 x mniej - mediana 99% strat szturmujacego pada w fazie murow (model c). Rowna wymiana sedziow (model b, 26/55) to dolna granica. Podaje zakres b-c |
| twardy prog 20% | sedzia 2: narastajaca szansa 15-25%; sedzia 3: prog w gestwinie 0.197-0.23 | **prog losowany na armie U[L-0.05, L+0.05]**, L = 0.22 - ten sam cel (mniejsza wrazliwosc, nie mechanicznie), jeden klucz, prosto do wytlumaczenia ("kazda armia ma inna wytrzymalosc") |
| udzial odparc 55-60% | sedzia 1: 18-28% wiarygodne; sedziowie 2 i 3: grozi "nieudolnym AI" | L 0.22 zamiast 0.20: z OB4 ok. 49-55% szturmow AI odparte; strojenie jednym kluczem |

**Przeszczepy przyjete:** OB4 (T1 z OBA + klasa twierdzy z O1 OBLEZENIA; sedzia 2), prog losowany (sedzia 2, zmienione), S9 + S11 (wszyscy), progi realizmu i blokady (wszyscy),
pola logu: HP murow w %, linia startowa z czynnymi modelami i wlascicielami latek Harmony, L7 (sedziowie 1 i 3), sprawdzenie fabul ROT przed kodem (sedzia 3), zapas okupu (sedzia 1).
**Na pozniej:** S4 (tor M), S7 (tor L), S10 (zgoda Jeffa), tor pieniedzy Reach-Dorne. **Odrzucone:** O5/O6, podloga f >= 1, S1-S3/S5 (koszt zoldu bez zmierzonego zysku),
S6 (OB3 robi to samo wezej: wielka odsiecz dalej bije sie w polu), S8 (sprzeczne z decyzja Jeffa "milicja bez wyjatku").

## 2. Reguly z liczbami

Kazda regula ma wylacznik w MCM (nazwy i opisy po angielsku). Nowe klucze nie istnieja w `Armoury.json` Jeffa, wiec dzialaja domyslne z kodu (CLAUDE.md 7).
Wszedzie poza regula: gracz (jego wlasne oblezenie, jego szturm, bitwa z jego udzialem - `IsPlayerMapEvent`), Inni (Undead - oblezeniami rzadzi CrashScribe T2c),
bitwy morskie (`IsNavalMapEvent`), szturmy fabuly ROT (ROT startuje je sam, bez decyzji AI - `HarrenhalSiegeEvent` `ApplyStartAssaultAgainstWalls`).

### OB1 - cierpliwosc wedlug roku + stolica (tylko decyzja AI)

Gra [K G `BesiegerCamp.cs:406-428`, sprawdzone]: `w = 0.8 - 0.02 x (godziny - 96)` (x1.25 bez taranu, x1.25 bez wiezy); szturm, gdy `s = A / (O x przewaga^w) > 1`,
potem szansa na mysl `0.1 x s x kara za machiny`.

| Liczba | Gra | OB1 | Skad |
|---|---|---|---|
| poczatek niecierpliwosci | 96 h | 96 x DaysPerYear/84 = **416 h (17.3 doby)** | zasada 1 dzien = 1 dzien; `Calendar.DaysPerYear` [K INT `Calendar.cs:62`] - przy zmianie kalendarza przeliczy sie sam |
| spadek wykladnika | 0.02/h | 0.02 x 84/DaysPerYear = **0.00462/h** (w0 -> 0 w 7.2 doby) | jw. |
| najnizszy wykladnik | bez dna (mediana -1.66 przy decyzji [P]) | **0** | mury nigdy nie pomagaja oblegajacemu |
| stolica (`CapitalTreasury.IsCapital` [K INT `CapitalTreasury.cs:195`]) i ostatnia twierdza krolestwa (`Kingdom.Fiefs.Count <= 1`) | jak wszedzie | **w = w0 zawsze** | szturm tylko, gdy armia bije pelne mury; krolestwo nie znika szturmem |

Wykonanie: gdy gra zwraca "true", liczymy `s_OB1` (nasze w) i `s_gra`; `s_OB1 <= 1` -> "false"; inaczej "true" z prawdopodobienstwem
`min(1, 0.1 x s_OB1 x kara) / min(1, 0.1 x s_gra x kara)` - dokladnie wzor gry z naszym w (latka tylko odrzuca). Kolejnosc: OB1 PRZED okupem.

### OB2 - szturm odparty (tylko szturm z decyzji AI)

| Warunek / liczba | Wartosc | Skad |
|---|---|---|
| znacznik | bitwa Siege, ktorej oboz dostal w tej dobie "true" od OB1/gry; przy wczytaniu zapisu w trakcie szturmu znacznik ginie - ten szturm jak w grze | OBA rozdz. 3; bez nowego pola zapisu |
| prog armii | losowany przy starcie szturmu: **U[L - 0.05, L + 0.05], L = 0.22** (17-27%) | rozdz. 3; klucz `SiegeAssaultRepelLoss` 0.22, `SiegeAssaultRepelSpread` 0.05 (0 = twardy prog jak OBA) |
| straty | (ranni + zabici + rozbici) / (straty + zostali w bitwie), strona oblegajaca | [K G `MapEventSide.cs:55, :126`] |
| mury pracuja | obroncow > 100 (miasto) / > 30 (zamek), nie sala lorda | ten sam warunek co gra [K G `DefaultCombatSimulationModel.cs:251`] |
| skutek | szansa odwrotu 1.0 -> gra: 4 rundy poscigu, `EndByRunAway`, DefenderVictory, `RemoveAllSiegeParties`, bez jencow z cofajacych sie | [K G `DefaultEncounterModel.cs:381-389` (dzis 0 dla Siege/SallyOut/SiegeOutside), `MapEvent.cs:1326-1338, :1955-1960, :2084-2098`] |
| rozejm | odparta frakcja i jej rody nie oblegaja tej twierdzy **60 dob** (`SiegeRansomTruceDays`) - ten sam rozejm co po okupie | [K INT `SiegeRansom.Act.cs:364` AddTruce, `:53` TruceDays]; zapis `arm_siege_ransom` jest |

Mala zaloga za murami zatrzymuje armie do ok. 7 razy wieksza w ludziach (0.22 x A = 1.58 x (O - 100/30), wymiana 1.58 [P]); wieksza przewaga wygrywa jak dzis.

### OB3 - zaloga bije sie z murow, gdy odsiecz dolacza do szturmu

| Warunek / liczba | Wartosc | Skad |
|---|---|---|
| kiedy | bitwa oznaczona OB2, ktora gra zmienila w SiegeOutside (obronca spoza murow dolaczyl) | [K G `MapEvent.cs:651-654`, sprawdzone]; 31 takich bitew w fin30 [P] |
| warunek | ludzie twierdzy (zaloga, milicja, druzyny stojace w osadzie) >= **50%** strony obrony, obroncow > 100/30 | wielka odsiecz z pola bije sie w polu jak dzis (Sandstone d75: 43 druzyny, 5744 ludzi [P]) |
| co | ciosy na runde wzorem szturmu gry: oblegajacy `2 x round(1.5 + O^0.3)`, obronca `2 x round(0.5 + max(1 + O^0.3 x 0.7 x przewaga, (O+1)/(A+1)))` | [K G `DefaultCombatSimulationModel.cs:253-256`] - zadna nowa liczba |
| odwrot | OB2 dziala tez w tej bitwie | jedna regula odwrotu |

Klucze: `SiegeReliefFromWalls` true, `SiegeReliefWallsShare` 0.5. KP d530: z murow Robb placi ok. 1.58 x (1156 - 100) = 1670 ludzi = 38% [S] -> odwrot.

### OB4 - przygotowanie oblezenia AI (nowe; T1 z OBA + klasa twierdzy z OBLEZENIA)

| Liczba | Dzis | OB4 | Skad |
|---|---|---|---|
| tempo budowy obozu i machin strony oblegajacej AI | x0.25 gry (BK 0.5 x RBL 1.0 x nasze 0.5) [K][U] | **x0.5 tego** (x0.125 gry) | [K INT `WorldPace.cs:128-145` SiegePostfix]; klucz `SiegeAiPrepPercent` 50 (100 = dzis) |
| klasa twierdzy (czas budowy) | zamek = miasto = stolica | **zamek x1, miasto x1.5, stolica x2.5** | proporcja H z O1 OBLEZENIA (14/21/35); klucze `SiegeTownPrepFactor` 1.5, `SiegeCapitalPrepFactor` 2.5 |
| gotowosc obozu przy 2352 ludziach (mediana [P]) | 4.3 doby | zamek 8.6, miasto 12.9, stolica 21.5 doby | [S z ROZPOZNANIE-CZAS 4.1] |

Kogo nie dotyczy: oboz, ktorego wodzem jest gracz albo w ktorego armii jest gracz (Jeff 17.09: RBL 1.0, wlasne oblezenie "za wolne"); strona broniaca (jej machiny
bez zmian); Inni. Gdy gracz BRONI twierdzy, OB4 dziala (oblegajace AI buduje wolniej). Uzasadnienie: budowa jest juz ok. x84/364 (x0.25 vs x0.23), wiec OB4 to
NIE zasada 1 dzien = 1 dzien, tylko wybor projektu: przygotowanie oblezenia trwa tygodnie, a miasto i stolica dluzej niz zamek. CampFever (od 9. doby) i spojnosc
armii (23-29 dob [S MODY 3]) zostaja - dluzsze oblezenie kosztuje; pilnuja tego progi testu (rozdz. 5).

### S9 + S11 - bunty BK (paczka CrashScribe + linia Armoury)

- **S9:** CrashScribe przy starcie/wczytaniu kampanii ustawia `BKClanBehavior._disableRebelKingdom = true` [K BK `BKClanBehavior.cs:44` pole public static, bramka `:456`
  w `MakeRebelKingdom`; BK sam przelacza je w `BannerKingsCheats.cs:2466` - sprawdzone]. Rod z lennem bez krolestwa zostaje rodem niezaleznym (obsluga gry).
  Klucz CrashScribe `BkRebelKingdoms` false (true = jak dzis). Linia `Mends: BK rebel kingdoms off`. Pliki ustawien BK nietkniete.
- **S11 (pomiar, bez reguly):** linia przy kazdym `OnClanChangedKingdom` rodu z lennami: rod, stare/nowe krolestwo, `ChangeKingdomActionDetail`, relacja z wladca, kiesa, lenna.

### Linie logu (warunek wgrania)

- `Mury oblezen (OB0)` raz na dobe: decyzje AI gra true X, OB1 wstrzymane Y (mury <= 1 Z, stolica/ostatnia S), przepuszczone; OB4: obozy AI spowolnione, gracz pominiety;
  szturmy AI A, odparte B (srednie straty %, srednie progi), zdobyte C; bitwy ze szturmu Q, z murow R (wygrala twierdza T); rozejmy po odparciu N;
  armie rozwiazane pod murami wedlug przyczyny (`ArmyDispersed`: CohesionDepleted, FoodProblem, inne [K G `Army.cs:32-50`]); potkniecia.
- `Szturm odparty (OB2)`: doba, osada (stolica?), frakcja, wodz, ludzi na starcie, prog armii, straty %, godzin szturmu, obroncow zostalo/bylo, rozejm do dnia.
- `Bitwa ze szturmu z murow (OB3)`: doba, osada, ludzie twierdzy / strona obrony (udzial), wynik.
- Linia O0 (decyzja o szturmie) rozszerzona: klasa twierdzy, w gry / w OB1, **HP murow w %** (czy ostrzal zbija mury do zera - przewaga x0.25 [K G `:210-213`]),
  **L7: obrona rozbita na zaloge, milicje i lordow w osadzie** (pokaze tez, co zabralo zaloge KP d524-529: 541 -> 271 [P]).
- Linia startowa: czynne typy `CombatSimulationModel`, `EncounterModel`, `ArmyManagementCalculationModel`, `SiegeEventModel`, stan wpiecia latek OB i
  **wlasciciele latek Harmony** na `GetMapEventSideRunAwayChance` i `GetSimulationTicksForBattleRound` (`Harmony.GetPatchInfo().Owners`).

## 3. Skutek na biegu fin30 (szacunek redakcji)

Metoda [S]: `robocze\wybrany\szacunek_wybrany.py` (python -I) na `czas\fin30-epizody.csv` (206 oblezen [P]) i `projekt-oba\oba-zdobycia.tsv` (flagi OBA).
Straty w fazie murow: (a) cale straty szturmu B175 (OBA), (b) rowna wymiana (sedziowie, dolna granica), (c) wymiana wazona ciosami gry w fazie murow i po murach.
OB2 z progiem losowanym; OB1 jak OBA; OB4: dodatkowe doby = T0 x (1 / 2 / 4) dla zamku / miasta / stolicy, T0 = 4.3 x pierwiastek(2352/ludzie), przezycie oblezenia
exp(-0.061 x doby) [P OBA]; powrot 29% po 60 dobach [P OBA]. Wynik: `robocze\wybrany\szacunek-wynik.txt`.

| Miara (2 lata) | fin30 [P] | OBA sam (L 0.20 twardy) [S] | **WYBRANY (L 0.22 losowy + OB4)** [S, model c / b] |
|---|---|---|---|
| zdobycia oblezeniem (z powrotem) | 57 | 23.4 / 29.8 | **24.9 / 27.8** |
| szturmy odparte (z 18 "bez rozstrzygniecia") | 0 | 40.7 / 34.7 | **25.5 / 22.8** |
| udzial odpartych w szturmach AI | 0% | 67% / 57% | **55% / 49%** |
| zdobycie od startu: mediana / p90 | 12 / 14 dob | 10 / 14 | **15.4 / 19.0** (b: 15.7 / 20.3) |
| zamek / miasto, gdy zdobyte (mediana, p10-p90) | 12 / 14 | ok. 10 | **16.3 (12.8-25.7) / 22.3 (17.6-32.2)** |
| stolice zdobyte | 7 | 0 | **0** (dodatkowe 15-22 doby budowy; mury <= 1 albo straty 27-88%) |
| krolestwa zniszczone szturmem | 1 (Braavos) | 0 | **0** |
| nowe panstwa | 13-19 [P SILY 8.1 / zadanie] | bez zmian | **<= 3** (S9 usuwa 11-16 [P/S SILY]) |

Wrazliwosc (model c / b, prog losowany + OB4): L 0.20 -> 21.5 / 24.5 zdobyc; **0.22 -> 24.9 / 27.8**; 0.25 -> 29.8 / 32.5. Prog losowany zmniejsza skok:
0.22 -> 0.25 daje +4.9 zdobyc zamiast +8.0 przy progu twardym [S].
Kto kogo [S OBA rozdz. 4, bez OB4]: Reach z Dorne 8 -> ok. 5, Krolewska Przystan, Duskendale, Lonely Light odparte, Essos 18 -> ok. 1.
Uczciwie: szacunek jest na tych samych oblezeniach i silach; armie odparte szukaja innych celow (model liczy tylko powrot 29%), wiec realnie zdobyc moze byc wiecej.

## 4. Plan kodu

**Przed kodem (30 min, tylko odczyt):** (1) w dekompilacji ROT sprawdzic, czy Czarny Nurt, Mur i Bekarty startuja szturm same (jak Harrenhal), czy czekaja na decyzje
AI - jesli czekaja, wylaczyc OB1/OB4 dla oblezen ze skryptu (lista StoryIds `SiegeRansom.cs:69-70`); (2) przeszukac dekompilacje wszystkich modow z listy ladowania
pod `GetMapEventSideRunAwayChance`, `GetSimulationTicksForBattleRound`, `GetConstructionProgressPerHour` (OBA przeszukala rot, bkrot, rbl, rbm, sca, dipl, dte, dr, beco, naval, BK).

| Regula | Plik / cel | Co | Klucze (domyslnie; wylacznik) |
|---|---|---|---|
| OB1 | INT `SiegeRansom.cs:266` `AssaultPost`: wywolanie `SiegeWalls.PatienceVeto(__instance, ref __result)` PRZED bramka okupu `if (!Live \|\| !AnyKey) return;` (:271). Latka wpieta zawsze (`Seal31.EnsureHooks` -> `SiegeRansom.EnsureHooks`, `Seal31.cs:204`, `SiegeRansom.cs:425-442`) - bramki sa w czasie gry | wzor OB1, liczenie przez istniejace `GameRatio`; stolica `CapitalTreasury.IsCapital` (:195) + `Fiefs.Count <= 1` | `SiegePatienceByYear` true; `SiegeCapitalWallsHold` true |
| OB2 | nowy `Armoury/src/SiegeWalls.cs`: postfiks `DefaultEncounterModel.GetMapEventSideRunAwayChance`; znacznik - wlasny nasluch `CampaignEvents.MapEventStarted` w SiegeWalls (NIE w `ArmouryBehavior.OnMapEventStarted`, ktory wychodzi przy bitwach bez gracza, :2018); prog armii w slowniku pamieci | wyjscie po 1 porownaniu typu bitwy; odwrot 1.0 po progu | `SiegeAssaultRepelLoss` 0.22 (0 = gra); `SiegeAssaultRepelSpread` 0.05 |
| rozejm | INT `SiegeRansom.Act.cs:364` `AddTruce` z private na internal (albo wrapper `TruceAfterRepel`); bramka `ScorePost` (`SiegeRansom.Act.cs:401`, dzis `!ActOn` = okup wylaczony) ma przepuszczac takze rozejm po odparciu (`ActOn \|\| SiegeRepelTruce`) | ta sama linia "rozejm zlamany" (:429-444) | `SiegeRepelTruce` true (dni = `SiegeRansomTruceDays` 60) |
| OB3 | `SiegeWalls.cs`: postfiks `DefaultCombatSimulationModel.GetSimulationTicksForBattleRound` (ROT `ROTCombatSimulationModel.cs:81-84` i NavalDLC na ladzie wolaja gre) | wyjscie po `!IsSiegeOutside`; petla po druzynach tylko w oznaczonej bitwie | `SiegeReliefFromWalls` true; `SiegeReliefWallsShare` 0.5 |
| OB4 | INT `WorldPace.cs:128-145` `SiegePostfix` (raz na lancuch - `SpeedDepth.OutermostSiege`): po `SiegePacePercent` mnoznik AI i klasy, tylko strona oblegajaca, nie oboz gracza / armii z graczem, nie Inni | log "WorldPace" dla gracza bez zmian; licznik do OB0 | `SiegeAiPrepPercent` 50 (100 = dzis); `SiegeTownPrepFactor` 1.5; `SiegeCapitalPrepFactor` 2.5 |
| logi | `SiegeWalls.cs` (OB0-OB3, ArmyDispersed), `SiegeRansom` (pola O0: klasa, w, HP murow, L7), linia startowa | rozdz. 2 | `SiegeWallsLog` true |
| S11 | Armoury `KingdomBalance` - nasluch `OnClanChangedKingdom` | tylko linia | `ClanLeaveLog` true |
| S9 | CrashScribe (Mends, start i wczytanie kampanii): `AccessTools.Field("BannerKings.Behaviours.BKClanBehavior:_disableRebelKingdom")` -> true; brak pola = linia ostrzezenia, nic wiecej | linia Mends | CrashScribe `BkRebelKingdoms` false |

Ogolne: latki w kampanii (`SiegeRansom.EnsureHooks` / `Seal31.Patch`), wyjatek per obiekt = licznik potkniec, bez globalnego wylacznika (CLAUDE.md 7); po kluczach
`python tools/gen_mcm.py`; wpis CHANGELOG (Problem / Przyczyna / Zmiana / Ryzyko / Status) dla paczki A (Armoury) i C (CrashScribe).

**Paczki i tory:** A = OB1 + OB2 + rozejm + OB3 + OB4 + logi + S11 (Armoury, tor L: pelny lancuch, 2 recenzje); C = S9 (CrashScribe, tor S). Obie w tym samym biegu,
kazda z wlasnym wylacznikiem; skutki czytane z osobnych linii.

**Czas [S]:** sprawdzenia 0.5 h; SiegeWalls.cs 2 h; OB1 + rozejm 0.75 h; OB4 0.5 h; pola logu i linia startowa 0.75 h; S11 + S9 0.75 h; Settings + gen_mcm + CHANGELOG +
build 0.5 h; 2 recenzje 1 h - razem ok. 6.75 h. Autotest 60 dob ok. 0.7 h, bieg roczny ok. 1.5 h, bieg dwuletni ok. 3 h.

**Kontrola calosci (CLAUDE.md 8.0):**
- OB1 siedzi w tym samym postfiksie co okup, przed nim - liczniki okupu licza po naszym "tak"; przy murach > 1 okup zostaje (obronca placi tylko wtedy).
- OB2: `LosersFlee` N3/H3 nie dotyka szturmu [K INT `Settings.cs:823`]; odwrot nie bierze jencow; `SiegeRansom.OnMapEventEnded` liczy to jako szturm bez zdobycia;
  B175 wypisze "wygrywa O". DefenderVictory w szturmie gra obsluguje (gdy oblegajacy zgina), ale przez odwrot nigdy - BK `OnSiegeEventEnded`, SCA, armia po odwrocie: test.
- OB3: BK `SimulateHit`, nasze NorthHomeEdge / ValyrianWardSim siedza w innej metodzie - nie koliduja.
- OB4: jeden mnoznik w istniejacym postfiksie po `SiegePacePercent`; nie liczy drugi raz (OutermostSiege). Dluzsze oboz = wiecej CampFever i spadku spojnosci - zamierzone, z progami.
- S9: dziala tez na bunt miasta [K BK `BKClanBehavior.cs:71-82`] - miasto zbuntowane zostaje przy rodzie buntownikow bez krolestwa (obsluga gry).
- Nic z niczego: zadna regula nie tworzy zlota ani ludzi. Zapis: bez nowych pol (znacznik i progi w pamieci, rozejm w `arm_siege_ransom`).

## 5. Plan testu i progi

Kolejnosc: build kod 0 -> autotest 60 dob z zapisu roku 1 (dym: linie OB0-OB4, S9, S11, 0 ERROR, wczytanie zapisu w trakcie szturmu) -> wgranie po udanym autotescie
(stala zgoda Jeffa z 09.10, z kopia) -> bieg roczny (nowa kampania) -> bieg dwuletni. Skrypt progow: grupa `--grupa oba` w `sprawdz_logi`.

| Prog | Bieg roczny 364 | Bieg dwuletni 728 |
|---|---|---|
| W-1 zdobycia oblezeniem (L5b-2) | 11-17 (fin30 31); alarm < 8 albo > 21 | **20-32** (fin30 57); alarm < 15 albo > 38 |
| W-2 szturmy odparte (OB2 / B175 Siege wygrywa O) | >= 8; udzial 35-70% szturmow AI | >= 18; udzial 35-70% |
| W-3 zdobycie od startu - mediana | zamek >= 14, miasto >= 18 dob (12 / 14) | jw. |
| W-4 decyzje AI przy murach <= 1 przed 17.3 doby / szturm na stolice i ostatnia twierdze przy murach <= 1 | 0 / 0 | 0 / 0 |
| W-5 stolice zdobyte / krolestwa zniszczone szturmem | <= 1 / 0 | <= 2 / 0 (Stormlands przez fabule ROT dozwolone) |
| W-6 OB3: bitwy ze szturmu z murow, wygrywa twierdza | >= 1, >= 50% | >= 3, >= 50% |
| W-7 rozejm po odparciu zlamany / gracz w liniach OB2-OB3 / odparcie z udzialem Innych | 0 / 0 / 0 | 0 / 0 / 0 |
| W-8 blokada AI: oblezenia > 100 dob / poza stolicami > 45 dob | 0 / <= 2 | 0 / <= 3 |
| W-9 armie rozwiazane pod murami (CohesionDepleted + FoodProblem) | <= 10% oblezen | <= 10% |
| W-10 CampFever: zmarli oblegajacy | <= 5% ludzi obozu | jw. |
| W-11 okupy AI | >= 9 (fin30 18) | >= 13 (fin30 26) |
| W-12 seria: zadna armia nie bierze 3 twierdz w 30 dob (fin30: Robb KP d530, Rosby d541, Duskendale d559) | 0 | 0 |
| W-13 nowe panstwa (S9) / linia S11 przy kazdym odejsciu z lennem | BK 0, razem <= 1 / tak | BK 0, razem <= 3 / tak |
| W-14 fabula: Harrenhal Polnocy | ok. d128 jak fin30 | jw. |
| W-15 gospodarka | grupy etap8 / gl1 / gl2 / kiesa / okup / lup5b bez nowych NIE; ludzie w druzynach d182 >= 89 600 (80% z 111 958 [P]); zold/pulap <= 80% | jw. + mediana dlugosci wojen <= 1.5 x fin30 |
| W-16 wydajnosc / bledy | <= 13.7 s/dobe (fin30 13.0), Armoury i CrashScribe 0 ERROR, potkniecia OB 0 | jw. |
| W-17 informacyjnie | Reach w Dorne (<= 6), Essos, krolestwa na koniec, Krolewska Przystan (czy i po ilu dobach), HP murow przy decyzji, L7 | jw. |

**Strojenie (jeden klucz na bieg, bez nowego kodu):**
- W-1 ponad gorna granice -> `SiegeAssaultRepelLoss` 0.20; ponizej dolnej -> 0.25.
- W-3 ponizej (oblezenia dalej za krotkie) -> `SiegeAiPrepPercent` 50 -> 40; W-9 ponad 10% -> 50 -> 65.
- W-10 ponad 5% -> `SiegeSicknessIncubationDays` 9 -> 14 (klucz jest w `Armoury.json` Jeffa - gra zamknieta, kopia .bak; jedna proba, nie obchodzic zabezpieczen).
- W-11 ponizej -> mala zmiana kodu (tor S): cykl okupu otwiera sie na "true" gry przy murach > 1, przed rozrzedzeniem OB1 (okup nie oddaje twierdzy, nie przyspiesza zdobyc).
- W-8/W-9 i W-1 ponizej dolnej razem -> najpierw `SiegeCapitalWallsHold` zostaje, `SiegeCapitalPrepFactor` 2.5 -> 1.5.

## 6. Etap 2 (po biegu dwuletnim) i czego nie robimy

| Co | Kiedy | Tor |
|---|---|---|
| S4 - patrole BK tylko w pokoju (prefiks `BKSettlementBehavior.HandleGarrison`, warunek `:453`) | gdy L7 pokaze, ze zaloga wychodzi przed oblezeniem (KP d524-529) | M |
| S7 - lordowie z okolicy (1 doba marszu, < 0.8 x oblegajacy) wchodza do murow | po tescie; tanie wyjscie na poczatku postfiksu `GetTargetScoreForFaction` (missionType != Defender albo brak oblezenia); czas doby <= +5% | L |
| S10 - Diplomacy x84/364 (0.05/0.1/0.1 -> 0.0115/0.023/0.023 w `DiplomacySettings_v1.2.json`) | tylko na slowo Jeffa, z kopia .bak | S |
| tor pieniedzy Reach-Dorne (pulap zoldu 3.2 x [P]) | gdy W-17 pokaze Reach w Dorne > 6 | L |
| O5 (zaraza pozniej), krzywa O1 z OBLEZENIA | tylko gdy W-10 / W-3 nie przejda po strojeniu | M |
| Nie robimy: S1-S3, S5 (zold +145 tys. zl/d bez zmierzonego zysku), S6 (zastapione OB3), S8 (decyzja Jeffa "milicja bez wyjatku"), O6, BK PartySizes, D10 okup czestszy | - | - |

## 7. Czego nie wiem

- Jak AI zachowa sie po odparciu (rozejm, inny cel, rozwiazanie armii) - model liczy tylko powrot 29% [P]; dlatego W-1 ma szeroki zakres.
- Odwrot w szturmie to droga gry, ktorej gra przy oblezeniu nigdy nie uzywa - reakcje BK (`OnSiegeEventEnded`, ArmySiegePersistence) i SCA sprawdzi autotest.
- B175 przy szturmach wielodniowych liczy tylko druzyny obecne przy koncu (KP: 92 obroncow wobec 541 + 340 [P CZAS 1]) - wymiana 1.58 jest orientacyjna.
- Czy AI w dluzszym oblezeniu buduje machiny miotajace i zbija mury do zera (przewaga x0.25 skraca OB1) - odpowie pole HP murow.
- Dlaczego 47 oblezen fin30 odstapiono bez bitwy i 18 szturmow skonczylo sie "bez rozstrzygniecia" - nadal nie wiem; hazard 0.061/dobe wziety z pomiaru.
- Dlaczego rody odchodza z lennem w roku 2 - S11 zmierzy; przyczyny nie zakladam.
- Czy fabuly ROT poza Harrenhalem czekaja na decyzje AI - sprawdzenie przed kodem (rozdz. 4).
- OB4: czas gotowosci obozu to szacunek z kodu (gra nie loguje budowy AI) - T0 przyblizony z liczby ludzi w szturmie.

## 8. Pliki robocze (`docs\oblezenia\robocze\wybrany\`)

| Plik | Co |
|---|---|
| `szacunek_wybrany.py` | szacunek redakcji (python -I; wejscie `..\czas\fin30-epizody.csv`, `..\projekt-oba\oba-zdobycia.tsv`): modele strat a/b/c, prog losowany, OB4 |
| `szacunek-wynik.txt` | wynik: udzial strat w fazie murow, warianty L 0.18-0.25 x prog twardy/losowy x OB4, czasy zdobycia wedlug klasy |
