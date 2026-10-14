# PROJEKT-OBA - oblezenia i sily razem (14.10)

Kat: OBA - najmniejszy zestaw zmian w oblezeniach I silach razem. Cel (zadanie): zdobyc w 2 lata ok. 1/3-1/2 dzisiejszych (57 -> 19-28),
czesc szturmow odparta (dzis 0), stolice padaja rzadko, krolestwa nie znikaja w 2 lata bez wielkiej przewagi; najmniejsze ryzyko dla gospodarki i wydajnosci.
Podstawa: `ROZPOZNANIE-CZAS.md`, `ROZPOZNANIE-SILY.md`, `ROZPOZNANIE-MODY.md` (bieg dwuletni fin30) + sprawdzenie kodu w tej sesji.
Znaczniki: **[P]** pomiar (plik / linia), **[K]** kod (plik:linia), **[U]** ustawienie, **[S]** szacunek, **[H]** historia z wiedzy ogolnej.
Kalendarz: rok 364 doby; gra strojona pod 84 (x4.33). Bez zmian kodu - to projekt.

## 0. Dla Jeffa (prosto)

1. Dzis szturm NIE MOZE byc odparty: gra nie pozwala oblegajacym sie cofnac - bitwa o mury trwa, az jedna strona nie ma ludzi [K]. Przy przewadze 3:1 i wiecej
   zawsze traci obronca. Dlatego 0 odpartych w 140 szturmach. Zadne "mocniejsze mury" tego nie zmienia - zmienia to dopiero odwrot.
2. **OB2 - szturm odparty:** gdy oblegajacy w szturmie straca 1/5 ludzi, a mury jeszcze stoja (obroncow ponad 100 w miescie / 30 w zamku), cofaja sie.
   Oblezenie upada, ta sama frakcja nie wraca pod te mury 60 dob (jak po okupie). Mala zaloga za murami zatrzymuje armie mniej wiecej szesc razy wieksza.
3. **OB3 - zaloga bije sie z murow:** dzis, gdy w czasie szturmu nadciaga odsiecz, gra robi z szturmu bitwe w polu i zaloga traci mury
   (tak padla Krolewska Przystan: 1156 obroncow w polu zamiast za murami). Jesli w tej bitwie wiekszosc obroncow to ludzie twierdzy - bija sie z murow.
4. **OB1 - cierpliwosc oblegajacych wedlug roku (1 dzien = 1 dzien):** gra po 4 dobach "traci cierpliwosc" i od ok. 6. doby mury licza sie NA KORZYSC
   oblegajacego (110 ze 113 decyzji o szturmie). U nas cierpliwosc x4.33 (17 dob), mury nigdy nie pomagaja atakujacemu. Stolica i ostatnia twierdza
   krolestwa - mury licza sie zawsze w pelni.
5. Skutek na biegu fin30 [S]: zdobyc 57 -> ok. 23 (20-29), odpartych szturmow 0 -> ok. 40, stolic zdobytych 7 -> 0-1, krolestw zniszczonych oblezeniem 1 -> 0.
   Krolewska Przystan zostaje przy Baratheonach. Reach bierze z Dorne ok. 5 zamkow zamiast 8 (armie 10:1 dalej wygrywaja - to sprawa pieniedzy, poza zestawem).
6. Twoje oblezenia sie NIE zmieniaja: OB1 to mysl AI, OB2 i OB3 dzialaja tylko w szturmie, ktory zaczelo AI, i nigdy w bitwie z Twoim udzialem.
   Twoje twierdze pod Twoja nieobecnosc bronia sie lepiej.
7. Bez zmian obcych modow (BK, RBL, Diplomacy, ROT) i bez zmian ustawien Jeffa. Trzy latki w Armoury, jeden nowy plik, klucze w MCM.
8. Norvos (rody odeszly z lennami) i 13-19 nowych krolestw w Essos to bunty, nie oblezenia - osobny tor (rozdz. 6). Stormlands znikaja przez fabule ROT
   (smierc Renly'ego) - zostaje, to lore.

## 1. Dlaczego te trzy reguly (dowody)

| Ustalenie | Dowod | Wniosek dla projektu |
|---|---|---|
| Gra daje oblegajacym szanse odwrotu 0 w szturmie, wypadzie, bitwie pod murami i napadzie; tylko w polu jest odwrot przy morale <= 20 | [K] `dec32\...\GameComponents\DefaultEncounterModel.cs:381-389`; NavalDLC woła gre (`NavalDLCEncounterModel.cs:99-101`) | szturm konczy sie dopiero, gdy jedna strona nie ma ludzi [K `MapEvent.cs:1275-1290`]; przy >= 2.8:1 [P] to zawsze obronca - stad 0 odpartych w 140 [P] |
| Wymiana w szturmie: oblegajacy traca mediana 1.58 czlowieka na obronce | [P] B175 "Siege", 44 zdobycia z >= 150 stratami obrony (`robocze\projekt-oba\szacunek-wynik.txt`) | do zdobycia 500 obroncow armia placi ok. 600-800 ludzi |
| Straty oblegajacych w zdobyciach (straty / ludzie A): kwartyle 7% / 21% / 30% | [P] B175, `oba-zdobycia.tsv` | prog 20% dzieli dzisiejsze zdobycia mniej wiecej na pol |
| Mury dzialaja w symulacji tylko w bitwie typu Siege i przy obroncach > 100 (miasto) / > 30 (zamek) | [K] `DefaultCombatSimulationModel.cs:251-256` | OB2 sprawdza ten sam warunek - odwrot tylko, dopoki mury pracuja |
| Gdy do szturmu dolacza obronca spoza murow, gra zmienia typ bitwy na SiegeOutside - zaloga traci mury | [K] `MapEvent.cs:651-654`; [P] 31 takich bitew w fin30 (17 wygral oblegajacy, 14 strona twierdzy) | KP d530: 4409 vs 1156 w polu - ciosy na runde 154 vs 69 dla oblegajacego; z murow byloby 20 vs 54 dla obroncy [S z kodu :251-268] |
| Czlon czasu decyzji o szturmie: po 96 h -0.02/h, od ok. 136 h wykladnik murow ujemny | [K] `BesiegerCamp.cs:408-409`; [P] w < 0 w 110 ze 113 decyzji (mediana -1.66) | stale pod rok 84; w 22 decyzjach (19%) tylko czlon czasu dopuscil szturm przy murach <= 1 [P] |
| Zdobyte stolice: Lorath, Totava, Iksa Calo, Iksa Keria (3 kolejne stolice Braavos), Vaes Dothrak, Krolewska Przystan, Silverhill | [P] 7 linii "Skarbiec stolicy (L5b-1)" w `Armoury-2026-10-14_06-01-15.log` | stolice padaly jak zwykle zamki; Braavos zniknelo po utracie ostatniej (Iksa Keria d310) |
| Po odwrocie gra nie bierze jencow z pokonanych i zdejmuje oboz | [K] `MapEvent.cs:1955-1960` (CaptureDefeatedPartyMembers wychodzi przy RetreatingSide), `:2084-2098` (DefenderVictory w szturmie -> RemoveAllSiegeParties) | OB2 korzysta z gotowej sciezki gry (odwrot + poscig 4 rundy, `:1326-1338`, `:1134-1139`), bez wlasnego konczenia bitwy |

## 2. Zestaw - trzy reguly z liczbami

### OB1 - Cierpliwosc oblegajacych wedlug roku + stolica (CZAS; tylko AI)

Gra [K `BesiegerCamp.cs:406-428`]: `w = 0.8 - 0.02 x (godziny - 96)` (x1.25 bez taranu, x1.25 bez wiezy); szturm, gdy `s = oblegajacy / (obroncy x przewaga^w) > 1`,
potem szansa na mysl `0.1 x s x kara za maszyny`.

| Liczba | Gra | OB1 | Skad |
|---|---|---|---|
| poczatek niecierpliwosci | 96 h (4 doby) | 96 x 4.33 = **416 h (17.3 doby)** | zasada Jeffa 1 dzien = 1 dzien; mnoznik = dni roku / 84 z `Calendar.DaysPerYear` (zmiana kalendarza - sam sie przeliczy) |
| spadek wykladnika | 0.02 / h | 0.02 / 4.33 = **0.00462 / h** (od w0 do 0 w 7.2 doby) | jw. |
| najnizszy wykladnik | bez dna (mediana -1.66 przy decyzji) | **0** - mury nigdy nie pomagaja oblegajacemu | absurd w grze: po 6 dobach obroncy "licza sie za mniej, niz ich jest" |
| stolica (CapitalTreasury.IsCapital: BK albo poczatkowa siedziba) i ostatnia twierdza krolestwa | jak wszedzie | **w = w0 zawsze** - szturm tylko przy stosunku z pelnymi murami > 1 | cel: stolice padaja rzadko, krolestwo nie znika |
| pusta warownia | szturm od razu | bez zmian | - |

Wykonanie bez zmiany wzoru gry: postfiks juz istnieje (`SiegeRansom.AssaultPost`, `SiegeRansom.cs:266`, liczy te same wielkosci w `Evaluate` `:129-256`).
Gdy gra zwraca "true", liczymy `s_OB1` (nasze w) i `s_gra` (w gry): `s_OB1 <= 1` -> "false"; inaczej "true" z prawdopodobienstwem
`min(1, 0.1 x s_OB1 x kara) / min(1, 0.1 x s_gra x kara)` - to daje dokladnie szanse wzoru gry z naszym w (nasze w >= w gry, wiec latka tylko odrzuca).
Kolejnosc: OB1 PRZED okupem - przy murach > 1 okup O1 zostaje jak dzis (obronca placi tylko przy murach > 1 [K `SiegeRansom.cs` naglowek]).
"False" znaczy dla AI "czekaj pod murami" - jak dzis w pierwszych 4 dobach. Gdy przewaga murow < 1 (mury zburzone do zera i wiele machin - wzor
`DefaultCombatSimulationModel.cs:172-227`) OB1 nic nie zmienia (wynik gry zostaje).

Skutek [S z wzoru; `oba-zdobycia.tsv`]: decyzje przy murach <= 1 (22 ze 113) zapadaja w dobie 17.4-18.4 zamiast 4.2-11.8; stolica przy murach <= 1
(3 decyzje: Vaes Dothrak 0.96 i 0.99, Tolerrol 0.85 [P O0]) - nigdy. Decyzje przy murach > 1: szansa na mysl = 0.1 x mury x kara za maszyny zamiast
0.1 x stosunek gry x kara (dzis mediana 34.6% [P]) - np. mury 1.5 i kara 0.5 = 7.5% na mysl (mysl co ok. 0.3 h) - szturm kilka godzin do 1-2 dob pozniej,
w "oknie" po skonczeniu machiny [S].

### OB2 - Szturm odparty (TRUDNOSC; tylko szturm z decyzji AI)

| Liczba / warunek | Wartosc | Skad |
|---|---|---|
| prog odwrotu: straty strony oblegajacej w tej bitwie (ranni + zabici + rozbici) / (straty + zostali w bitwie) | **>= 0.20** (pewny odwrot, bez losowania) | kwartyle strat w zdobyciach 7/21/30% [P]; 0.20 daje 19-28 zdobyc (rozdz. 4); z wymiana 1.58 [P] mury trzymaja do ok. 6-7:1 w ludziach [S] |
| tylko gdy mury pracuja | obroncow > 100 (miasto) / > 30 (zamek), poza Lord's Hall | ten sam warunek co gra [K `:251`] - gdy mury padly, szturm idzie do konca jak dzis |
| tylko szturm z decyzji AI | znacznik przy starcie bitwy (`ArmouryBehavior.OnMapEventStarted` `:2016`): Siege, a w tej dobie OB1/gra przepuscily "true" dla tego obozu | wylacza: szturmy gracza, szturmy fabuly ROT (ROT startuje je sam - `HarrenhalSiegeEvent.cs` `ApplyStartAssaultAgainstWalls`, milicja 0.6 x armii), gracza w armii AI i w oblezonej twierdzy (`IsPlayerMapEvent`) |
| poza regula | Inni (Undead) - nie cofaja sie (CrashScribe T2c rzadzi ich oblezeniami); bitwy morskie (`IsNavalMapEvent`) | jak okup |
| co sie dzieje | gra: RetreatingSide = oblegajacy, 4 rundy poscigu (obronca bije, oblegajacy nie), DefenderVictory, oboz zdjety, bez jencow z cofajacych sie | [K] `MapEvent.cs:1326-1338`, `:1955-1960`, `:2084-2098`; nasze N3/H3 omijaja odwrot (`LosersFlee.cs:1122`) |
| rozejm | odparta frakcja i jej rody nie oblegaja tej twierdzy **60 dob** - ten sam rozejm co po okupie (`SiegeRansom.AddTruce`, `SiegeRansom.Act.cs:364`; wynik celu 0 `:395-402`; zapis `arm_siege_ransom` juz jest) | jedna zasada: "odparty albo oplacony - nie wraca 60 dob"; bez rozejmu ta sama armia wraca po kilku dobach na przerzedzona zaloge |

Obronca tez placi: przy odwrocie stracil ok. 0.2 x A / 1.58 = 1/8 liczby oblegajacych [S] (np. 300 z 600 przy armii 2500) - kolejne oblezenie po rozejmie
trafi na slabsza zaloge (zaloga rosnie 2/d - K8).

### OB3 - Zaloga bije sie z murow, gdy odsiecz dolacza do szturmu (SILY)

| Liczba / warunek | Wartosc | Skad |
|---|---|---|
| kiedy | bitwa SiegeOutside powstala ze szturmu z decyzji AI (ten sam znacznik co OB2 - obiekt MapEvent ten sam po zmianie typu [K `MapEvent.cs:651-654`]) | 31 takich bitew w fin30 [P] |
| warunek | ludzie twierdzy (zaloga, milicja, druzyny stojace w osadzie) >= **50%** ludzi strony obrony, obroncow > 100 / 30, mury nie zburzone (przewaga x0.25 przy zerze murow liczy gra) | gdy w bitwie przewaza wielka odsiecz z pola - bitwa w polu jak dzis (np. Sandstone d75: 43 druzyny Dorne, 5744 ludzi [P]) |
| co | ciosy na runde wzorem gry dla szturmu: oblegajacy `2 x round(1.5 + O^0.3)`, obronca `2 x round(0.5 + max(1 + O^0.3 x 0.7 x przewaga, (O+1)/(A+1)))` | [K `DefaultCombatSimulationModel.cs:253-256`] - zadna nowa liczba, tylko warunek typu bitwy |
| odwrot | OB2 dziala tez w tej bitwie | jedna regula odwrotu |

Skutek [S]: 5 z 57 zdobyc szlo przez taka bitwe z przewaga ludzi twierdzy (Demongate, Vaes Dothrak, Krolewska Przystan, Deep Den d566, Silverhill) - we wszystkich
oblegajacy straciliby z murow 27-88% ludzi, czyli cofneliby sie (OB2). Krolewska Przystan: 4409 Robba vs 1156 (zaloga + milicja 882 = 76% obrony [P epizod town_EW4])
- z murow Robb placi ok. 1.58 x (1156 - 100) = 1670 ludzi = 38% [S] - odwrot przy 20%.

### Klucze (MCM, Armoury; nazwy i opisy po angielsku przy kodzie)

| Klucz | Domyslnie | Wylaczenie = dzisiejsza gra |
|---|---|---|
| `SiegePatienceByYear` (OB1, cierpliwosc x dni roku / 84, wykladnik >= 0) | true | false |
| `SiegeCapitalWallsHold` (OB1, stolica i ostatnia twierdza: w = w0) | true | false |
| `SiegeAssaultRepelLoss` (OB2, prog strat) | 0.20 | 0 |
| `SiegeRepelTruce` (OB2, rozejm po odparciu = `SiegeRansomTruceDays` 60) | true | false |
| `SiegeReliefFromWalls` (OB3) + `SiegeReliefWallsShare` | true / 0.5 | false |
| `SiegeWallsLog` (linie OB) | true | false |

Nowe klucze nie istnieja w `Armoury.json` Jeffa, wiec dzialaja domyslne z kodu (CLAUDE.md 7). Nowy plik `Armoury/src/SiegeWalls.cs`; latki w kampanii
(`SiegeRansom.EnsureHooks` / `Seal31.Patch`), wyjatek = licznik potkniec bez globalnego wylacznika (CLAUDE.md 7). Rozejm OB2 musi dzialac takze przy wylaczonym
okupie AI (postfiks celu zakladany zawsze, gdy ktorys klucz OB2/okupu jest wlaczony).

## 3. Gdzie w kodzie

| Regula | Typ | Miejsce | Uwagi |
|---|---|---|---|
| OB1 | nasz kod (latka juz jest) | postfiks `BesiegerCamp.StartingAssaultOnBesiegedSettlementIsLogical` = `SiegeRansom.AssaultPost` (`SiegeRansom.cs:266`); nowy fragment na poczatku, niezalezny od kluczy okupu (dzis `if (!AnyKey) return`) | metoda wolana tylko z mysli AI wodza obozu [K `MobilePartyAi.cs:710-718`]; gracz nie ma tej decyzji |
| OB1 stolica | nasz kod | `CapitalTreasury.IsCapital` (`CapitalTreasury.cs:159-206`) + `Kingdom.Fiefs.Count <= 1` | linia O0 juz pisze "stolica tak/nie" |
| OB2 | nowa latka | postfiks `DefaultEncounterModel.GetMapEventSideRunAwayChance` (gra `:381-389`; NavalDLC woła gre `NavalDLCEncounterModel.cs:99-101`; zaden inny mod nie zmienia szansy odwrotu - przeszukane dekompilacje rot, bkrot, rbl, rbm, sca, dipl, dte, dr, beco, naval i BK) | wolane co sesje symulacji (godzina w szturmie) dla obu stron [K `MapEvent.cs:1016`, `:1197-1201`]; wyjscie po 1 porownaniu typu bitwy |
| OB2 znacznik | nasz kod | `ArmouryBehavior.OnMapEventStarted` (`:2016`) + pamiec "true w tej dobie" z AssaultPost | po wczytaniu w trakcie szturmu znacznik ginie - ten szturm jak w grze (bez nowego pola zapisu) |
| OB2 rozejm | nasz kod (jest) | `SiegeRansom.AddTruce` / `IsTruce` / postfiks `GetTargetScoreForFaction` (`SiegeRansom.Act.cs:364-402`), linia "rozejm zlamany" `:429-444` | pole zapisu `arm_siege_ransom` istnieje |
| OB3 | nowa latka | postfiks `DefaultCombatSimulationModel.GetSimulationTicksForBattleRound` (`:241-281`); ROT (`ROTCombatSimulationModel.cs:81-84`) i NavalDLC na ladzie (`NavalDLCCombatSimulationModel.cs:117-188`) wolaja gre | wyjscie po `!IsSiegeOutside`; petla po druzynach strony obrony tylko w oznaczonej bitwie |
| Obce mody | **nic** | - | zadne ustawienie BK / RBL / Diplomacy / ROT nie zmienione; bez kopii i zgody nie trzeba |

Linie logu (do testu): `Mury oblezen (OB0): dzien | decyzje AI: gra true X, OB1 wstrzymane Y (mury<=1 Z, stolica S), przepuszczone | szturmy AI A, odparte B
(srednie straty %), zdobyte C | bitwy ze szturmu Q, z murow R (wygrala twierdza T) | rozejmy po odparciu N | potkniecia` (raz na dobe, gdy cos bylo);
`Szturm odparty (OB2): dzien | osada (stolica?) | oblegajacy frakcja, wodz, ludzi na starcie, straty %, godzin szturmu | obroncow zostalo / bylo | rozejm do dnia`;
`Bitwa ze szturmu z murow (OB3): dzien | osada | ludzie twierdzy / strona obrony (udzial) | wynik`.

## 4. Skutek na biegu fin30 (szacunek)

Metoda [S]: `robocze\projekt-oba\szacunek_oba.py` (python -I) na `robocze\czas\fin30-epizody.csv` (206 oblezen [P]). Dla kazdego z 57 zdobyc: OB2 - zdobycie
zostaje, gdy straty oblegajacych w szturmie (B175) < prog; OB3 - przy bitwie ze szturmu z przewaga ludzi twierdzy straty z murow = 1.58 x (obroncy - 100/30) /
oblegajacy; OB1 - doba dopuszczenia szturmu z wzoru i przezycie oblezenia do niej `exp(-0.061 x dni)` (0.061/dobe = konce bez szturmu: wypad, odsiecz,
odstapienie [P]); stolica przy murach <= 1 - nigdy. Powrot: 29% twierdz po nieudanym oblezeniu albo okupie jest zdobywanych 60-180 dob pozniej [P 26/91] -
liczony z ta sama szansa sukcesu co w nowych regulach. Harrenhal (szturm fabuly ROT) i 2 zdobycia po wypadzie - bez zmian.

| Miara | fin30 [P] | OBA, prog 0.20 [S] | prog 0.18 / 0.22 [S] |
|---|---|---|---|
| zdobycia oblezeniem w 2 lata | 57 | **ok. 23** (19.7 + powrot 3.7) | 20 / 29 |
| zdobycia w roku 1 | 31 | ok. 13 | 11 / 16 |
| szturmy odparte (B175 Siege, wygrywa obronca) | 0 | **ok. 40** (37 z dzisiejszych zdobyc + 7 z 18 "bez rozstrzygniecia", minus ok. 4 wstrzymane przez OB1) | 45 / 35 |
| udzial odpartych w szturmach AI | 0% | ok. 55-60% | |
| stolice zdobyte | 7 | **0** (wszystkie 7: straty 27-88% albo mury <= 1); prog testu <= 2 | 0 / 0 |
| krolestwa zniszczone oblezeniem | 1 (Braavos d310) | **0** (Totava 50%, Iksa Calo 65%, Iksa Keria 54% strat) | |
| decyzja o szturmie przy murach <= 1 (22 decyzje) | doba 4.2-11.8 | doba 17.4-18.4; stolica - nigdy | |
| zdobycie od startu oblezenia - mediana / p90 | 12 / 14 dob | ok. 10 / 14 (zostaja szturmy przytlaczajace) | 10 / 11 |
| oblezenie, gdy mury trzymaja | konczy sie szturmem ok. 8. doby | 17-25 dob albo odstapienie (spojnosc, zaraza od 9. doby) | |
| odparty szturm od decyzji | - | ok. 2 doby (A = 2500: ok. 10 strat / h -> 20% po ok. 50 h) | |

Kto kogo (zdobycia bez powrotu, prog 0.20) [S `oba-zdobycia.tsv` + epizody]: Reach 11 -> 6 (z Dorne 8 -> 5: Starfall, Yronwood, Wyl odparte), Dorzecze 10 -> 4,
Baratheon KP 6 -> 5, Stormlands 5 -> 2, Polnoc 5 -> 2 (Harrenhal z fabuly, Rosby; **Krolewska Przystan, Duskendale, Lonely Light odparte**), Nocna Straz 1 -> 0,
Smocza Skala 1 -> 0, Essos razem 18 -> 1 (Sarnor). Razem 57 -> 20 przed powrotem. Kolejnosc zdarzen po zmianie bylaby inna (np. Rosby i Duskendale szly po upadku KP) - to szacunek na tych samych oblezeniach.
Tracacy: Baratheon KP 13 -> 5, Dorne 8 -> 5, Smocza Skala 7 -> 3, Braavos 3 -> 0.

Uczciwie: Reach dalej bierze zamki Dorne, gdy przychodzi 10-25:1 (Sandstone 11.6, Blackmont 17.3, Kingsgrave 10.2, Skyreach 24.8, Hellholt 11.8 [P SILY 3.3]) - mury
nie zatrzymaja dziesieciokrotnej przewagi i nie powinny. Ta przewaga to pieniadze (pulap zoldu Reach 3.2 x Dorne, GL2 7.8 vs 1.46 mln [P SILY 2.2, 5.2]) - poza zestawem.

## 5. Ryzyko

| Obszar | Ryzyko | Liczby / ocena |
|---|---|---|
| Gospodarka - lupy | mniej zdobyc = mniej przelewow lupu do zwyciezcow (to przelewy, nie zloto z niczego) | lup L2 fin30 1.90 mln zl / 2 lata (114 linii) [P] -> ok. 0.8 mln [S]; skarbiec stolic L5b-1 0.50 mln [P] -> ok. 0; wobec rent korony 6-25 mln na krolestwo [P SILY 5.2] - male |
| Gospodarka - ludzie | obroncy nie gina w calosci (dzis 25 553 zabitych/rannych obroncow w zdobyciach [P CZAS 6c]); w odparciu obronca traci ok. 1/8 liczby oblegajacych | ok. 10-15 tys. ludzi wiecej w zalogach za 2 lata [S] - zold zalog pilnuje budzet rodow 166 (zwalnia ponad pulap); bez nowego zrodla zlota |
| Gospodarka - okup O1 | OB1 nie blokuje decyzji przy murach > 1, a tylko tam okup (26 w fin30, 18 w roku 1 [P]) | okupy zostaja; moga zapasc kilka godzin pozniej [S] |
| Wojny | mniej zdobyc = wolniejsze zmeczenie wojna Diplomacy (WarExhaustionPerSiege) -> wojny moga trwac dluzej, a odparcia dokladaja straty | prog testu: mediana dlugosci wojen <= 1.5 x fin30 |
| Zaraza i spojnosc | dluzej czekaja tylko oblezenia z murami trzymajacymi (19% decyzji) - do ok. 25 dob | CampFever od 9. doby: do 25. doby ok. 22% x tlok zachoruje, 10% chorych umrze [S z `CampFever.cs:64,145-154`]; armia przy granicy ma spojnosc do ok. 29 dob, gleboko do ok. 23 [S MODY 3] - czesc odstapi: zamierzone (blokada kosztuje). D8 / D9 niepotrzebne |
| Wydajnosc | OB2 i OB3 to postfiksy wolane w kazdej bitwie | wyjscie po jednym porownaniu typu bitwy; petla po druzynach tylko w oznaczonym szturmie (2.4 oblezenia naraz [P]); OB1 w istniejacym postfiksie - pomijalne [S]; prog testu <= 13.7 s/dobe (fin30 13.0 [P STAN-PRAC 15:41]) |
| Kod gry | odwrot w szturmie to sciezka gry nigdy nieuzywana przy oblezeniu (gra daje 0) | DefenderVictory w szturmie gra obsluguje (gdy oblegajacy zgina) [K `:2084-2098`], odwrot w polu tez - ryzyko w polaczeniu; BK OnSiegeEventEnded (A11), ROT - sprawdzi test; tor L |
| Fabula ROT | szturmy fabuly (Harrenhal, Czarny Nurt, Mur, Bekarty) startuje ROT bez decyzji AI | OB2/OB3 ich nie dotycza (brak znacznika); OB1 ich nie dotyczy (ROT nie pyta o decyzje); prog testu: Harrenhal Polnocy ok. d128 jak w fin30 |
| Gracz | jego oblezenia NIE dluzsze: sam decyduje (OB1 nie dotyczy), jego szturm (misja i autobitwa) bez OB2/OB3; w armii AI i w oblezonej twierdzy - bitwa z jego udzialem jak w grze | asymetria: gracz zdobywa twierdze AI tak latwo jak dzis, AI - trudniej. Jesli Jeff chce rownosci - pozniej klucz dla autobitwy gracza (nie w zestawie; Jeff 17.09 chcial szybszych oblezen dla siebie [U CHANGELOG]) |
| Swiat | Essos prawie stoi (18 -> ok. 1 zdobycie), rebelianckie hrabstwa z 1 twierdza sa "ostatnia twierdza" - trudniej je odbic | bunty i tak trzeba ruszyc osobno (rozdz. 6); prog informacyjny: liczba krolestw na koniec |

## 6. Czego zestaw nie robi i dlaczego

| Dzwignia z rozpoznan | Dlaczego nie w zestawie | Kiedy wrocic |
|---|---|---|
| T1 / D1 wolniejsza budowa obozu (SiegePacePercent 50 -> 25) | nie jest potrzebna do celow; dotyka gracza (Jeff 17.09: RBL 0.4 -> 1.0, "za wolne"); wydluza wszystkie obozy (zaraza, spojnosc, jedzenie) | gdy po tescie "dalej za szybko": nowy klucz tylko dla AI (`WorldPace.cs:128-145`, oboz AI x0.5 = +4.3 doby [S]) i prog OB2 0.22 -> zdobyc ok. 23, mediana zdobycia ok. 15 dob [S `szacunek-wynik.txt`] |
| D4 mury x1.5-2 (GetSettlementAdvantage) | bez odwrotu nie daje odpartych - tylko krwawsze szturmy (gra nie pozwala sie cofnac [K]); dziala na autobitwe gracza i wybor celu | nie |
| D5 / D6 zalogi (K8 2 -> 4, sufit x1.5) | zalogi przy oblezeniu juz powyzej gry: zamki 254, miasta 517 [P SILY 3.1]; koszt zoldu, ochotnikow; budzet 166 i tak zwalnia ponad pulap | gdy po tescie zalogi po odparciu nie odrastaja (powtorne zdobycia po rozejmie > 40%) |
| Podloga milicji GL4 dla twierdz (Dorne x0.45) | sprzeczne z decyzja Jeffa "milicja bez wyjatku" (`PeopleLevy.cs` naglowek) | tylko na slowo Jeffa |
| D7 BK PartySizes 2.0 -> 1.5 | obce ustawienie, dotyka wszystkich bitew, hordy Innych, zoldu | nie |
| D8 / D9 spojnosc i zaraza | OB1 wydluza tylko 19% oblezen do 17-25 dob - miesci sie w dzisiejszej spojnosci przy granicy (ok. 29 dob) | razem z T1 |
| D10 okup czestszy (BloodyRatio 2.0) | zamienia zdobycia w pieniadze miast; zbedne przy OB2 | nie |
| N3 symetryczne (pobita strona twierdzy ucieka za mury) | wieksza zmiana `LosersFlee`; OB3 zdejmuje glowny przypadek (zaloga w polu) | gdy test pokaze wiele bitew ze szturmu przegranych przez strone twierdzy z odsiecza |
| D13 / D14 bunty (BK krolestwa z buntu, Diplomacy szanse x84/364) | to nie oblezenia ani sily: Norvos zniknelo przez odejscia rodow, 18 nowych krolestw w roku 2 [P SILY 8] | osobny tor zaraz po OBA; D14 = zasada Jeffa 1 dzien = 1 dzien (obce ustawienie Diplomacy - kopia i zgoda Jeffa) |
| Stormlands (RenlyDeathEvent ROT) | fabula ROT, lore [K SILY 7.1] | nie |
| Spirala pieniedzy (pulap, GL2, wojsko w pokoju) | ryzyko gospodarki; tor pieniedzy roku 2 juz scalony (B4 0.7, M1-M4) | po biegu z OBA, jesli Reach dalej zjada Dorne |

## 7. Test (tor L: nowa mechanika bitwy oblezniczej - kod, 2 recenzje, autotest; ok. 3 h kodu + bieg roczny ok. 1.5 h + dwuletni ok. 3 h)

Kolejnosc: build kod 0 -> autotest 60 dob z zapisu roku 1 (dym: linie OB0-OB3, 0 ERROR, wczytanie zapisu) -> bieg roczny (nowa kampania) -> bieg dwuletni.
Skrypt progow: grupa `--grupa oba` w `sprawdz_logi`.

| Prog | Bieg roczny 364 | Bieg dwuletni 728 |
|---|---|---|
| OBA-1 zdobycia oblezeniem (linie L5b-2) | 9-17 (fin30 31) | **19-28**; alarm < 15 albo > 35 |
| OBA-2 szturmy odparte (OB2 / B175 Siege wygrywa O) | >= 8, udzial 30-75% szturmow AI | >= 20 |
| OBA-3 stolice zdobyte (L5b-1) | <= 1 | <= 2 |
| OBA-4 krolestwa zniszczone przez szturm (G14 / "KROLESTWO ZNISZCZONE" po zdobyciu) | 0 | 0 (Stormlands przez fabule ROT dozwolone) |
| OBA-5 decyzje AI przy murach <= 1 przed 17.3 doby oblezenia (O0) | 0 | 0 |
| OBA-6 szturm na stolice / ostatnia twierdze przy murach <= 1 | 0 | 0 |
| OBA-7 OB3: bitwy ze szturmu z murow, strona twierdzy wygrywa | >= 1, >= 50% | >= 3, >= 50% |
| OBA-8 rozejm po odparciu zlamany | 0 | 0 |
| OBA-9 gracz w linii OB2 / OB3 | 0 | 0 |
| OBA-10 fabula: Harrenhal Polnocy | ok. d128 jak fin30 | jw. |
| OBA-11 okupy AI | >= 9 (fin30 rok 1: 18) | >= 13 (fin30: 26) |
| OBA-12 wydajnosc / bledy | <= 13.7 s/dobe, Armoury 0 ERROR, potkniecia OB 0 | jw. |
| OBA-13 gospodarka | grupy etap8 / gl1 / gl2 / kiesa / okup / lup5b - bez nowych NIE wobec fin30 | jw. + mediana dlugosci wojen (E7/0) <= 1.5 x fin30 |
| OBA-14 informacyjnie | czas zdobycia (mediana), Reach z Dorne, Essos, liczba krolestw na koniec | jw. |

Jesli OBA-1 wyjdzie ponad 28 - prog OB2 0.20 -> 0.18; ponizej 19 - 0.22 (jeden klucz, bez nowego kodu). Wrazliwosc jest duza (0.18 / 0.22 = 20 / 29 zdobyc [S]).

## 8. Czego nie wiem

- **Prog 0.20 opiera sie na stratach z B175**, ktore przy szturmach wielodniowych licza tylko druzyny obecne przy koncu (np. KP: 92 obroncow w B175 wobec 541 + 340 w CSV [P CZAS 1]); wymiana 1.58 jest orientacyjna. Stad zakres 20-29.
- **Odwrot w szturmie to droga gry, ktorej gra przy oblezeniu nigdy nie uzywa** - zachowanie BK (`OnSiegeEventEnded`, ArmySiegePersistence), SCA i armii po odwrocie (czy armia sie rozwiaze, dokad pojdzie) sprawdzi dopiero test.
- **Jak AI zareaguje na odparcia** - czy po rozejmie wraca na te same mury (powrot 29% z fin30 to zachowanie bez OB2), czy armie po 20% stratach sie rozpadaja.
- **Udzial ludzi twierdzy w bitwie ze szturmu** liczony w szacunku z zalogi i milicji ze startu oblezenia (`zaloga0 + milicja0`) - przyblizenie; w kodzie liczony na biezaco.
- **18 szturmow "bez rozstrzygniecia"** - przyczyny nadal nie znam (CZAS rozdz. 10); w szacunku 7 z nich przy stratach >= 20% liczone jako odparte.
- **Sklasyfikowanie bitew pod murami** w dwoch rozpoznaniach rozni sie (np. Blackmont d148: SILY - obrona 705, CZAS - odsiecz atakuje oboz); OB3 liczylem tylko z "strona twierdzy broni" (CZAS).
- **Czas zdobycia nie rosnie** - zostaja szturmy przytlaczajace (mediana ok. 10 dob); jesli Jeffowi chodzi takze o dlugosc kazdego oblezenia, potrzebny jest T1 (rozdz. 6).
- Historia [H] (szturm na mury byl krwawy i czesto odparty, przytlaczajacy szturm na mala zaloge bywal szybki) - z wiedzy ogolnej, bez zrodel w tej sesji.

## 9. Pliki robocze (`docs\oblezenia\robocze\projekt-oba\`)

| Plik | Co |
|---|---|
| `szacunek_oba.py` | szacunek OB1-OB3 na epizodach fin30 (python -I; wejscie `..\czas\fin30-epizody.csv`) |
| `szacunek-wynik.txt` | wynik: wymiana, hazard, powrot, progi 0.15-0.25, wariant T1, stolice |
| `oba-zdobycia.tsv` | 57 zdobyc: stolica, fabula, mury przy O0, doba szturmu wedlug OB1, straty oblegajacych w szturmie, szacunek strat z murow (OB3) |
