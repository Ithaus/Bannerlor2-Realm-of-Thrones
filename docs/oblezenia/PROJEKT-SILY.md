# PROJEKT-SILY - zalogi, milicja, posilki, bunty (14.10.2026)

Odpowiedz na slowa Jeffa po biegu dwuletnim fin30: "sily nie sa rowne, oblezenia sa za szybkie i za latwe".
Ta czesc to SILY: wojsko i zalogi, milicja, posilki do oblezonych, wyrownanie krolestw, bunty i rozpad na hrabstwa.
Samych oblezen (decyzja o szturmie, tempo machin, mury w symulacji, zaraza obozu, okup) NIE rusza - to projekt CZAS / MODY.
Projekt powstal bez zmian kodu i bez commitow. Gry nie uruchamialem, w folderze gry niczego nie zapisalem.

Znaczniki: [P] pomiar (plik / linia), [K] kod (plik:linia), [U] ustawienie (plik), [S] szacunek (model albo rachunek).
Rok kalendarza = 364 doby; "doba to doba" (zasada Jeffa "1 dzien = 1 dzien").
Drzewo naszego kodu: `zb\int` (w-toku/zbior-e8, HEAD 7bc5dd0b; fin30 = 6e6313c). Gra: `dec32\cs_full`. BK: `e4tools\bkfull`
(wszystkie w scratchpadzie ae222cb7). Model i wyniki: `docs\oblezenia\robocze\projekt-sily\` (rozdz. 8).

## 0. Dla Jeffa (prosto)

1. **Same wieksze zalogi nie dadza odpartych szturmow.** AI zaczyna oblezenie dopiero wtedy, gdy ma okolo 3 razy wiecej sily niz obroncy.
   W szturmie oblegajacy traci 1.5-2 ludzi na kazdego obronce. Dlatego wynik jest znany, zanim bitwa ruszy [K][P].
   Wieksze zalogi daja co innego: AI rzadziej rusza na miasta i stolice, zdobycie kosztuje armie o ok. 43% wiecej ludzi, a szturm trwa ok. 3 doby dluzej [S].
2. **Szturmy odpiera dopiero obrona, ktora rosnie juz w czasie oblezenia.** Wtedy armia przyszla na mniejsza zaloge, niz zastaje.
   Pomagaja tu dwie reguly: odsiecz walczy z murow, a nie w polu (S6), oraz lordowie z okolicy wchodza do murow (S7).
   Model sprawdzilem na 4 zdobyciach z fin30: Krolewska Przystan d530, Yronwood, Skyreach, Wyl. Z tymi regulami we wszystkich czterech szturm zostaje odparty [S].
3. **Na 113 decyzjach o szturmie z fin30:** model bez zmian daje 54 zdobycia, z regulami S1-S6 - 42. Razem z projektem CZAS (AI nie szturmuje, gdy mury trzymaja) - 26 [S].
   W calym biegu zamiast 57 zdobyc bedzie ich ok. 45-52 przy samych silach. Razem z CZAS bedzie ich 26-35 [S, rozdz. 4].
4. **Norma zalogi dla kazdej twierdzy.** Zamek ma 400 ludzi, miasto 800. Ludnosc ziemi przesuwa norme w granicach x0.9-1.4, stolica krolestwa ma x2.
   Typowy zamek dostaje 360, typowe miasto 720. W fin30 na poczatku oblezenia bylo 254 i 517 [P]. Krolewska Przystan dostaje 1594.
   Przy pelnej normie w zalogach swiata jest 151 tys. ludzi, a w fin30 najwiecej bylo 92 tys. Zold rosnie o ok. 145 tys. zl na dobe.
   Swiat wydaje wtedy na zold 69% pulapu, dzis 60% [P+S].
5. **W pokoju zaloga nie spada ponizej 60% normy.** W roku 2 budzet rodow zwolnil z zalog 49 tys. ludzi [P].
   Ponizej tej granicy zaloga moze brac do 5 rekrutow na dobe, dzis bierze 2. Patrole BK wychodza z twierdzy tylko w pokoju.
6. **Bunty.** BK przestaje robic z rodu, ktory odchodzi z lennem, krolestwo "County / Barony of X". W fin30 tak powstalo do 16 z 19 nowych panstw (11 potwierdzonych liniami wojen) [P].
   Dla Diplomacy proponuje szanse wojen domowych x84/364. To ustawienie obcego moda, wiec tylko z kopia i za Twoja zgoda.
7. **Dorne.** Same zalogi Dorne nie uratuja: w modelu traci 7 twierdz zamiast 8 [S]. Dorne przegrywa w polu, bo ma 3.2 razy mniejszy pulap zoldu niz Reach [P].
   Pomagaja dwie rzeczy. Male oddzialy Dorne wchodza do murow, zamiast bic bandy (S7). Milicja twierdz w Dorne rosnie z x0.45 do x0.75 (S8).
8. **Ryzyko.** Biedne rody beda mialy mniej ludzi w polu, bo zold zalog placi sie pierwszy.
   Ty tez zastaniesz wieksze zalogi: stolice 1400-2200 ludzi, a Twoje autobitwy pod murami beda dluzsze.
   Bez projektu CZAS ok. 11 dzisiejszych okupow zamieni sie w szturm, bo obronca uzna, ze mury wytrzymaja, a AI i tak szturmuje (rozdz. 5).

## 1. Na czym stoi projekt (dowody z rozpoznan)

| Fakt | Liczba | Zrodlo |
|---|---|---|
| szturm: atak / obrona | mediana 2995 ludzi (16 druzyn) na 431 obroncow = 7.4 : 1; ponizej 3 : 1 tylko 7 razy | [P] ROZPOZNANIE-SILY 3.1 (`bitwy.log`) |
| sila przy decyzji o szturmie | oblegajacy / obroncy: min 2.82, mediana 5.74 (113 decyzji O0) | [P] ROZPOZNANIE-CZAS 5 |
| szturmy odparte | 0 z 73 (fin30) i 0 z 67 (fin21); 18 / 29 "bez rozstrzygniecia" | [P] `bitwy.log` B175 Siege |
| zaloga na poczatku oblezenia | zamki 254 (101-360), miasta 517 (281-625); limit gry 200 / 400 | [P] `ludzie-regiony.csv`; [K] `DefaultPartySizeLimitModel.cs:172-180` |
| zalogi swiata | 47 235 (d1) -> 92 002 (d361) -> 65 611 (d728) | [P] linie "Zalogi: przyrost (169c)" |
| zwolnieni z zalog przez budzet rodow | rok 1: 5 357, rok 2: 49 186 (w pokoju - cel B4) | [P] linie "Budzet rodow (166)"; [K] `ClanBudget.cs:688-721, 918-931` |
| zaloga ginie w polu przed szturmem | 17 bitew SiegeOutside wygranych przez oblegajacych (11 tuz przed zdobyciem): obrona 20 866 ludzi, zabici 8 936, ranni 12 012 | [P] `robocze\sily\bitwy-pod-murami.tsv` |
| skad SiegeOutside | do trwajacego szturmu dolacza obronca spoza murow -> gra zmienia szturm w bitwe w polu | [K] `MapEvent.cs:651-654` |
| odsiecz byla, ale daleko | lordowie KP d516-d530: 42 z 59 bitew w Dorzeczu, najwiekszy oddzial pod KP 1323 (d519); Dorne bije bandy oddzialami 85-462 ludzi | [P] `robocze\sily\gdzie-armie.txt` |
| pulap zoldu Reach / Dorne | d364: 183 697 / 57 483 zl/d (3.2 x); wypelnienie druzyn Dorne 0.36-0.42 | [P] `budzet-rodow.csv` |
| milicja wedlug ludzi (GL4) | czynnik Reach x2.12, Polnoc x0.58, Dorne x0.45, Zelazne Wyspy x0.37; milicja* na twierdze Reach 495-553, Dorne 136-170 | [P] linie GL4; [K] `PeopleLevy.cs:13-36` |
| nowe panstwa | 19 (18 w roku 2, wszystkie w Essos): 3 wojny domowe Diplomacy, 16 to rody, ktore odeszly z lennem (nazwy z tytulow BK "County / Barony / Kingdom of X"); rozpoznanie MODY z linii wojen: 11 z 14 droga BK | [P] ROZPOZNANIE-SILY 8.1, ROZPOZNANIE-MODY 5; [K] `BKClanBehavior.cs:454-478` |
| patrole BK | 5% na dobe (zaloga >= 100, nie w oblezeniu) wyprowadza 30-50% zalogi na >= 48 h | [K] `BKSettlementBehavior.cs:446-458`, `GarrisonPartyComponent.cs:44-66` |

## 2. Dlaczego same sily nie robia odpartych szturmow (rachunek)

**Prog wyboru celu** [K `DefaultTargetScoreCalculatingModel.cs:192-243`]: cel oblezenia odpada, gdy sila armii < 2 x (obrona w murach x (1 + 0.33 x poziom murow - agresja wodza)
+ rezerwa krolestwa). Przy murach 2 to ok. 3.3 x obrony. Zmierzone minimum przy decyzji: 2.82 [P].

**Szturm w symulacji** [K `DefaultCombatSimulationModel.cs:241-262`, `SimulateHit :5-14`, `:282-290`; `MapEventSide.cs:970-1000`]:
- w godzinie oblegajacy ma 2 x round(1.5 + O^0.3) ciosow, a obronca 2 x round(0.5 + 1 + O^0.3 x 0.7 x przewaga murow), gdzie O to liczba obroncow.
  Liczba ciosow oblegajacego zalezy od liczby OBRONCOW, a nie od wlasnej;
- cios wylacza czlowieka z szansa obrazenie / HP. Obrazenie to 40 x (sila/sila)^0.7 x (0.5-1), a oblegajacy ma w szturmie x0.9;
- skutek [S, model]: oblegajacy traci ok. 1.8 czlowieka na kazdego obronce (zmierzone 1.5 [P B175]). Szturm wygrywa wiec kazda armia,
  ktora ma ponad ok. 2 x tylu ludzi co obroncy. Premia murow znika ponizej 100 obroncow w miescie i 30 w zamku.

**Wniosek:** AI przychodzi z >= 2.8 x sily, a do wygranej wystarczy ok. 2 x. Wynik jest wiec przesadzony, zanim bitwa ruszy, i to niezaleznie od wielkosci zalogi.
Wieksza zaloga moze zrobic tylko trzy rzeczy:
- (a) podniesc prog startu: AI wybierze slabszy cel albo zbierze wieksza armie;
- (b) podniesc koszt i czas szturmu: strata oblegajacego ~1.8 x obrona, a czas rosnie z liczba obroncow;
- (c) zadzialac razem z czlonem murow w decyzji o szturmie (projekt CZAS).
Odparcie wymaga, zeby obrona urosla PO rozpoczeciu oblezenia (posilki), albo zeby armia stopniala.

**Kontrola modelu** (`model_sily.py`: wzory gry, wartosci oczekiwane, dane 113 linii O0) na fin30 bez zmian:

| Miara | Model | Pomiar fin30 |
|---|---|---|
| zdobyte w 10 dob po decyzji / okup / inne | 54 / 26 / 33 | 54 / 26 / 33 [P `fin30-decyzje-szturmu-O0-wynik.csv`] |
| odparte szturmy | 0 | 0 [P] |
| doby od startu oblezenia do zdobycia | mediana 12.9, p90 16.0 | mediana 12 (zamki 12, miasta 14), p90 14 [P] |
| strata oblegajacego na zdobycie | mediana 807 | srednio 714 (B175: 39 261 / 55) [P] |

## 3. Reguly

Typ: [N] nowy kod / latka u nas (Armoury albo CrashScribe), [M] nasz klucz MCM, [O] ustawienie obcego moda (tylko z kopia `.bak` i za zgoda Jeffa).
Kazda regula ma wlasny wylacznik (nazwy MCM po angielsku); klucze nie sa w `Armoury.json` Jeffa, wiec dziala wartosc domyslna z `Settings.cs`.

| # | Regula | Liczby | Gdzie w kodzie | Typ |
|---|---|---|---|---|
| S1 | **Norma zalogi twierdzy** = miejsca w zalodze i cel zalogi w wojnie | N = baza x P x W; baza: zamek 400, miasto 800; P = pierwiastek(ludzie ziemi / srednia rodzaju) w granicach 0.9-1.4; W = 2 dla stolicy krolestwa (`CapitalTreasury.IsCapital`), inaczej 1. Typowo: zamek 360, miasto 720, KP 1594 | postfiks `DefaultPartySizeLimitModel.CalculateGarrisonPartySizeLimit` (`:172-180`): limit = max(gra, N); w wojnie limit zoldu zalogi AI co najmniej N x zold czlowieka (`ClanBudget.GarLimitPostfix` `:1352`, `GarFull :686`); P z czynnika GL4 osady (`PeopleLevy`), nie z regionu | [N] klucze `GarrisonNormEnabled`, `GarrisonNormCastle` 400, `GarrisonNormTown` 800, `GarrisonNormCapitalFactor` 2.0, `GarrisonNormPeopleMin` 0.9, `GarrisonNormPeopleMax` 1.4 |
| S2 | **Odbudowa zalogi** | ponizej 0.6 N - do 5 rekrutow / dobe; 0.6-1.0 N - 2 / dobe (jak dzis K8); tylko AI, tylko z ludzi tej ziemi i w limicie zoldu (jak K8) | `ArmsFromPeople.cs:56` (K8Limit), `:336` (K8) | [N] `GarrisonRecruitPerDayBelowFloor` 5 |
| S3 | **Podloga zalogi 0.6 N** | cel pokojowy B4 = max(GarrisonPeaceShare x srednia wojenna, 0.6 N), dalej najwyzej 0.8 pulapu rodu; zwolnienia budzetu (krok 1) nie ponizej podlogi; nowa druzyna pana (K3) zostawia w siedzibie 0.6 N zamiast 25 / 50 | `ClanBudget.cs:688-721` (Garrisons), `:918-931` (zwolnienia), `ArmsFromPeople.cs:636` (K3) | [N] `GarrisonFloorShare` 0.6 |
| S4 | **Patrole BK tylko w pokoju** | patrol nie wychodzi, gdy krolestwo twierdzy jest w wojnie (dzis 5% / dobe, 30-50% zalogi) | prefiks `BKSettlementBehavior.HandleGarrison` (`:446-458`, warunek `:453`) | [N] `BkPatrolsOnlyInPeace` true (zamiast [O] PatrolParties = false - patrole w pokoju lapia bandy) |
| S5 | **Milicja wsi do murow** | na poczatku oblezenia cala milicja wsi twierdzy przechodzi do milicji twierdzy; po oblezeniu wraca do wsi w proporcji, jaka przezyla. Ksiega ludzi bez zmian (milicja* = twierdza + wsie) | `CampaignEvents.OnSiegeEventStartedEvent` (`CampaignEvents.cs:927`) i koniec oblezenia; `Settlement.Militia` (`Village.cs:136`); `PeopleLedger.cs:408-419` | [N] `VillageMilitiaToWalls` true |
| S6 | **Odsiecz walczy z murow** | gdy do trwajacego szturmu AI dolacza obronca spoza murow, bitwa zostaje szturmem (mury licza sie dla wszystkich obroncow), nie zmienia sie w SiegeOutside; bitwy gracza bez zmian | postfiks `MapEvent.AddInvolvedPartyInternal` (`MapEvent.cs:651-654`): typ sprzed wywolania Siege -> przywroc Siege (`_mapEventType`), gdy nie IsPlayerMapEvent | [N] `ReliefFightsFromWalls` true |
| S7 | **Przebicie do murow (posilki)** - etap 2 | lord (druzyna poza armia) krolestwa twierdzy w zasiegu 1 doby marszu od jego wlasnej obleganej twierdzy, slabszy niz 0.8 x oblegajacy: (a) wynik celu "obrona" tej twierdzy x3, (b) u bramy wchodzi do twierdzy zamiast bic sie w polu, traci tylu ludzi, ilu gra kaze graczowi przy przebiciu | postfiks `DefaultTargetScoreCalculatingModel.GetTargetScoreForFaction` (Defender, `:124-150`, `:298-301`; SCA mnozy wynik gry); strata `TroopSacrificeModel.GetLostTroopCountForBreakingInBesiegedSettlement` (`BreakInOutBesiegedSettlementAction.cs:28` - akcja gry jest tylko dla gracza, wiec wejscie przez `EnterSettlementAction` u nas) | [N] `ReliefBreaksIn` true, `ReliefBreakInDays` 1.0, `ReliefBreakInMaxRatio` 0.8 |
| S8 | **Milicja twierdz lagodniej wedlug ludzi** (wyrownanie) | czynnik GL4 milicji TWIERDZY (limit i przyrost) = pierwiastek(c) w granicach 0.75-1.5 zamiast c w granicach 0.1-3; wsie i pobor bez zmian; suma milicji swiata trzymana normalizacja GL4. Dorne x0.45 -> x0.75, Polnoc x0.58 -> x0.76, Zelazne Wyspy x0.37 -> x0.75, Reach x2.12 -> x1.46 | `PeopleLevy.cs` (MilitiaByPeople: postfiksy `CalculateMilitiaChangeInternal`, `BKMilitiaModel.GetMilitiaLimit`) | [N] `FortressMilitiaPeopleRoot` true |
| S9 | **Bez krolestw z odchodzacych rodow (BK)** | rod z lennem bez krolestwa zostaje rodem niezaleznym (jak w grze), BK nie tworzy "County / Barony / Kingdom of X" | CrashScribe przy starcie kampanii ustawia `BKClanBehavior._disableRebelKingdom = true` (pole publiczne statyczne `:44`, bramka `:456`; dziala tez na bunt miasta `:71-82`) | [N] CrashScribe `BkRebelKingdoms` false |
| S10 | **Wojny domowe Diplomacy x84/364** | DailyChanceToStartRebelFaction 0.05 -> 0.0115, DailyChanceToJoinRebelFaction 0.1 -> 0.023, DailyChanceToStartCivilWar 0.1 -> 0.023 | [U] `Configs\ModSettings\Global\Diplomacy\DiplomacySettings_v1.2.json` (wartosci sprawdzone 14.10); [K] `CivilWarBehavior.cs:136-158` | [O] kopia `.bak` + zgoda Jeffa (jak zmeczenie wojna 14.10) |
| S11 | **Pomiar: dlaczego rod odchodzi z lennem** | linia logu przy kazdym `OnClanChangedKingdomEvent` rodu z lennami: rod, stare / nowe krolestwo, `ChangeKingdomActionDetail`, relacja z wladca, kiesa, lenna. Regula na przyczyne - dopiero po pomiarze (np. gra: `DiplomaticBartersBehavior.cs:145-157` sprawdza odejscie w ok. 19% dob, jesli wartosc odejscia > 0 - to warunek stanu, nie szansa "na dobe") | nasluch w Armoury (KingdomBalance) | [N] tylko log |

Szczegoly i kontrola calosci (CLAUDE.md 8.0):
- **S1** dziala na limit miejsc i na limit zoldu zalogi AI w wojnie. Zaloga rosnie dalej tylko z placonych rekrutow (K1, K8) i z ludzi zostawionych przez druzyny.
  Nic nie bierze sie z niczego: ludzie przychodza z wolnych rak ziemi, a hamulec wolnych rak zostaje. Zold placi rod, w wojnie przed druzynami (GarrisonWarFull).
  Najpierw trzeba sprawdzic w kodzie, czy ROT i NavalDLC wolaja `Default.CalculateGarrisonPartySizeLimit`. Rozpoznanie MODY rozdz. 4 mowi, ze przepuszczaja, ale tej latki nie sprawdzalem.
  BK `BKGarrisonModel` (lord zostawia malo ludzi) zostaje bez zmian. CrownRents (zaloga >= 0.5 zwyklej zalogi krolestwa) jest wzgledne, wiec samo sie dopasuje.
  Bunt miasta wymaga milicji >= 1.4 x zaloga; przy wiekszej zalodze ten warunek spelnia sie rzadziej [K `RebellionsCampaignBehavior.cs:187-204`].
- **S3**: w pokoju pulap jest mniejszy (0.28 D). Prog "zalogi najwyzej 0.8 pulapu" zostaje, wiec bardzo biedny rod moze miec mniej niz podloga - liczymy to w linii L1.
- **S5**: zanim powstanie latka, trzeba sprawdzic w kodzie, jak gra przenosi `Settlement.Militia` na partie milicji. Milicja musi wrocic przed pierwszym dziennym sprawdzeniem buntu po oblezeniu
  (gra nie sprawdza buntu w oblezeniu [K `RebellionsCampaignBehavior.cs:104-112`]). W czasie oblezenia wsie sa bez milicji, wiec najazdy na nie latwiej sie udaja.
- **S6**: bitwa zostaje szturmem, wiec przegrani oblegajacy gina albo ida do niewoli jak w szturmie. Regula N3 (`LosersFleeBesiegers`, `Settings.cs:823`) obejmuje tylko SiegeOutside i wypad, w szturmie nie dziala.
  Dzis 12 takich bitew wygrala obrona i oblegajacy w 70% sie rozbiegali. Po zmianie zwyciestwo odsieczy bedzie rozstrzygajace.
  B175 zapisze te bitwy jako "Siege", a nie "SiegeOutside" - to trzeba uwzglednic przy porownaniu z fin30.
- **S7**: lord w zdobytej twierdzy idzie do niewoli, wiec bedzie wiecej okupow (RealisticCaptivity) i wiecej wypadow (dzis 39 z 41 wygrali wychodzacy [P]).
  SCA zdejmuje oblezenie, gdy wrog w promieniu 95 ma > 1.25 x sily [K `StrategicCampaignAI145Behavior.cs:302-326`] - S7 przyspieszy takie odstapienia.
- **S9**: niezalezny rod z lennem gra obsluguje sama (pokoj, dolaczenie do krolestwa: `DiplomaticBartersBehavior.cs:47-76, 99-143`). Rod dalej odchodzi z krolestwa, ale mapa nie rozpada sie na nowe panstwa.

## 4. Skutek w liczbach (fin30, model `model_sily.py`)

Wszystko [S]: wzory gry na 113 decyzjach o szturmie (linie O0). Do tego zaloga i milicja* z `ludzie-regiony.csv` w dobie decyzji i atak z B175.
"nie zaczete" znaczy, ze armia nie miala >= 2.8 x nowej obrony (prog skalibrowany na zmierzonym minimum). Milicje wsi przyjmuje jako 40% milicji* (BK: wies 1% ludnosci + 20 [K `BKMilitiaModel.cs:149-161`]).

| Wariant | zdobyte | odparte | okup | inne | czeka | nie zaczete | doby do zdobycia (med / p90) | strata ataku na zdobycie (med) |
|---|---|---|---|---|---|---|---|---|
| bez zmian (model) | 54 | 0 | 26 | 33 | 0 | 0 | 12.9 / 16.0 | 807 |
| tylko S6 | 51 | 3 | 26 | 33 | 0 | 0 | 14.3 / 18.9 | 892 |
| S1 300/600 | 55 | 0 | 19 | 26 | 0 | 13 | 14.1 / 16.8 | 950 |
| S1 400/800 | 53 | 1 | 12 | 21 | 0 | 26 | 14.8 / 16.6 | 1018 |
| S1 500/1000 | 40 | 0 | 12 | 17 | 0 | 44 | 15.6 / 17.5 | 1250 |
| **S1 400/800 + S5 + S6 (projekt)** | **42** | **2** | **13** | **16** | 0 | **40** | **16.1 / 17.6** | **1156** |
| to samo, zalogi tylko do 80% normy | 52 | 2 | 13 | 23 | 0 | 23 | 15.5 / 19.1 | 1168 |
| projekt, AI zaczyna mimo progu wszystkie 113 | 52 | 15 | 13 | 33 | 0 | 0 | 16.2 / 20.6 | 1302 |
| bez zmian + CZAS (szturm tylko przy "mury" > 1) | 47 | 0 | 26 | 33 | 7 | 0 | 12.6 / 16.0 | 771 |
| **projekt + CZAS** | **26** | 2 | 13 | 16 | 16 | 40 | 15.8 / 20.5 | 1135 |

Jak to czytac:
- **Zdobycia**. Na tych samych decyzjach jest 54 -> 42 (-22%). W pelnym biegu wyjdzie mniej, bo armie, ktore nie zaczna oblezenia, poszukaja innego celu.
  34 z tych 40 armii ma >= 2.8 x sily typowego zamku po normie (631). W biegu dwuletnim oczekuje wiec 45-52 zdobyc zamiast 57 (-10..-20%).
  Cele przesuwaja sie z miast i stolic na zamki: model nie zaczyna 6 oblezen stolic ze zdobyciem (Lorath, Totava, Iksa Calo, Iksa Keria, Vaes Dothrak, Krolewska Przystan).
- **Odparcia**. Bez S7 sa 2, obie dzieki S6: Deep Den d214-217, gdzie odsiecz 2533 ludzi walczy z murow, i Silverhill d610-614, gdzie walczy z murow odsiecz 38 druzyn, 5062 ludzi.
  Gdyby AI zaczelo mimo progu wszystkie 113 oblezen, 15 skonczyloby sie odparciem. Wieksze zalogi same w sobie trzymaja, ale AI ich unika.
- **Czas**. Od startu oblezenia do zdobycia mija 12.9 -> 16.1 doby (+3.2). Sama decyzja o szturmie dalej zapada ok. 8.4 doby, bo to sprawa projektu CZAS; dluzszy jest tylko szturm.
- **Koszt dla atakujacego**. Mediana straty na jedno zdobycie rosnie z 807 do 1156 (+43%). Robb, ktory w fin30 wzial Krolewska Przystan, Rosby i Duskendale w 30 dob,
  przy probie szturmu KP mimo progu traci 4236 z 4484 ludzi (zdobycie po 26.4 dobach) i nie ma juz czym isc dalej.
- **S7 na przykladach**. Do murow wchodzi najwiekszy oddzial obroncow z regionu w oknie oblezenia [P `gdzie-armie.txt`]. We wszystkich czterech przypadkach szturm zostaje odparty:

| Twierdza (fin30) | atak | dzis (model) | S1 + S5 + oddzial w murach |
|---|---|---|---|
| Krolewska Przystan d530 | 4484 ludzi, sila 7700 | zdobyta po 10.9 dobach szturmu, strata 2173 | + 1323 (Boros Blount, d519): sila obrony 4598, odparty po 14.3 dobach |
| Yronwood d324 | 6722, 8923 | zdobyta po 11.3, strata 2234 | + 2085: 4288, odparty po 23.5 |
| Skyreach d273 | 3275, 4552 | zdobyta po 6.0, strata 1006 | + 2007: 3269, odparty po 10.1 |
| Wyl d429 | 2343, 3767 | zdobyta po 6.2, strata 724 | + 1050: 2116, odparty po 14.5 |

  Jak czesto taki oddzial jest w zasiegu doby, nie zmierzylem - dowie sie tego linia L5. W Dorne najwiekszy oddzial w oknie oblezenia mial zwykle 85-462 ludzi, a 1050-2085 tylko w 3 z 8 oblezen [P].
- **Dorne** (13 decyzji): bez zmian 8 zdobyc, S1-S6 - 7, z CZAS - 6. S7 odpiera 3 z 8 obecnych zdobyc, wiec Dorne traci 4-5 twierdz zamiast 8 [S].
- **Razem z CZAS**. Przy murach liczonych w decyzji (wariant "mury" <= 1 = czekaj) wieksze zalogi daja 57 takich decyzji zamiast 22 [S].
  Wtedy dopiero zaczynaja grac glod (zapas 225-274 dob [P]), odsiecz i rozpad armii.
- **Bunty** (S9-S10): z 19 nowych panstw nie powstaje 11-16 (S9; 11 potwierdzonych liniami wojen BK, 16 z nazwy). Wojny domowe Diplomacy: 3 -> ok. 0.7 na 2 lata (S10, x84/364) [P + S]. Rody dalej moga odchodzic z lennem - S11 zmierzy, dlaczego.

**Koszt normy** (S1 400/800) [P `budzet-rodow.csv`, rachunek S]:

| Miara | fin30 d364 (wojna) | z norma | fin30 d546 (wiecej pokoju) | z podloga 0.6 N |
|---|---|---|---|---|
| ludzie w zalogach swiata | 92 382 | 151 316 (pelna norma) | 70 842 | ok. 90 800 |
| zold zalog (2.46-2.69 zl/czl./d [P]) | 227 429 zl/d | ok. 372 000 | 190 812 | ok. 244 000 |
| zold razem / pulap swiata | 888 099 / 1 490 170 = 60% | ok. 69% | 640 477 / 959 723 = 67% | ok. 72% |
| Dorne: zold / pulap | 35 103 / 57 483 = 61% | norma 7 560 ludzi -> ok. 76% | - | - |

Norma wedlug krolestw (S1, suma N) [S `p_krol.py`]: Reach 12 110 (P 1.40), Baratheon KP 12 197 (1.14), Polnoc 11 160 (0.90), Dorzecze 8 273, Dorne 7 560 (0.90), Dolina 7 402,
Zelazne Wyspy 5 760; na starcie (d1) zalogi tych krolestw 2 300-4 400.

## 5. Ryzyko

| Obszar | Co moze sie stac | Liczba / jak pilnowac |
|---|---|---|
| gospodarka | zalogi placone pierwsze, wiec biedne rody (na pulapie) maja mniej ludzi w polu; Dorne przy normie ma 76% pulapu zajete | swiat: zold 60% -> 69% pulapu [S]; prog testu: ludzie w druzynach >= 80% fin30 |
| ludzie | +59 tys. ludzi z wolnych rak (0.11% z 52.1 mln [P d1]); ziemie z hamulcem wolnych rak nie dobiora | linia L1: twierdze ponizej podlogi i powod (brak rak / brak zoldu) |
| jedzenie | zaloga je zaloga/20 na dobe: miasto +200 ludzi = -10/d; zapas w oblezeniu 274 -> ok. 267 dob | bez znaczenia dla glodu [S] |
| wydajnosc | szturmy o ~3 doby dluzsze (symulacja co godzine; srednio 2.4 oblezenia naraz [P]); S7 - postfiks na wyniku celu wolany bardzo czesto | S7: najpierw tanie wyjscie (missionType != Defender albo brak oblezenia), potem reszta; prog czasu doby +5% |
| gracz | wroga stolica 1440-2240, miasto ok. 720, zamek ok. 360 obroncow (misje oblezen wieksze, autobitwy dluzsze); S7 - lordowie wroga wchodza do miast, ktore oblegasz; Twoje zalogi maja wiecej miejsca, ale limit zoldu ustawiasz sam (S2, S3 tylko AI) | S6 tylko bitwy AI z AI; Jeff 17.09 chcial szybszego wlasnego oblezenia (RBL 1.0) - tego projekt nie rusza |
| interakcja z okupem | wieksza zaloga obniza "mury" ponizej 1, wiec obronca odmawia okupu (`SiegeRansom`); bez CZAS AI i tak szturmuje, wiec 11 okupow fin30 zamienia sie w zdobycie | wdrozyc razem z CZAS albo w tym samym kroku poprawic regule okupu (projekt CZAS/MODY) |
| cudze mody | S4 i S9 to latki na BK (prefiks i pole); S6 dotyka wnetrza MapEvent; S7 - SCA mnozy wynik gry, BK ArmySiegePersistence | S6/S7 tylko AI z AI; kazda regula z wylacznikiem; przed kodem dekompilacja jak w CLAUDE.md 6 |
| lore | Dorne broni sie murami, Reach dalej silniejszy w polu (8 mln ludzi); KP 1594 + City Watch ROT (+10/d) | zgodne z "Reach najbogatszy, Dorne nie do zdobycia" [S] |

## 6. Kolejnosc wdrozenia i test

Tory pracy (zasada Jeffa S/M/L):
- **paczka A**: S1, S2, S3, S4 - tor L (gospodarka zalog, pelny lancuch, recenzja);
- **paczka B**: S5, S6 - tor M;
- **paczka C**: S9 i linia S11 - tor S;
- **paczka D**: S7 i S8 - tor L, dopiero po teście A-C;
- **S10**: tylko po zgodzie Jeffa, z kopia pliku.
Kazda paczka ma wylaczniki i wpis w CHANGELOG.

**Nowe linie logu** (bez nich progow nie da sie sprawdzic):
- L1 "Zalogi - norma (S1-S3)": raz na dobe dla calego swiata: suma N, zaloga, wypelnienie (mediana, p10), twierdze ponizej podlogi, koszt zoldu;
- L2 "Patrole BK (S4)": ile patroli zablokowanych / puszczonych;
- L3 "Milicja wsi do murow (S5)": ilu ludzi przeszlo i ilu wrocilo;
- L4 "Odsiecz z murow (S6)": bitwa, kto dolaczyl, sila;
- L5 "Przebicie do murow (S7)": druzyna, strata, sila obrony przed / po;
- L6 "Odejscie rodu (S11)";
- L7 rozszerzenie O0: osobno zaloga, milicja i lordowie.

**Bieg roczny (364 doby), paczki A + B + C.** Gdy nie przejdzie P1, P2, P3 albo P8, cofamy paczke wylacznikiem:

| Prog | Wartosc | fin30 (rok 1) |
|---|---|---|
| P1 zalogi swiata | d182 >= 110 tys., d364 >= 120 tys.; wypelnienie normy w wojnie mediana >= 80% | 90 356 (d182), 92 382 (d364) [P] |
| P2 ludzie w druzynach swiata | d182 >= 90 tys. (80% fin30) | 111 958 (d182) [P] |
| P3 zold / pulap swiata | <= 80% | 60% (d364) [P] |
| P4 zdobycia w roku | <= 27 (-13%); szturmy odparte (B175 "wygrywa obronca" w Siege) >= 1 | 31; 0 [P] |
| P5 czas i koszt | mediana dob od startu do zdobycia >= 14.5; strata oblegajacego na zdobycie >= 1.3 x fin30 | 12; ok. 714 [P] |
| P6 bunty | nowe krolestwa z BK = 0; L6 podaje przyczyne kazdego odejscia | 1 w roku 1 [P] |
| P7 podloga | zwolnieni z zalog ponizej 0.6 N = 0 | - |
| P8 technika | czas doby (DayProfile) <= +5%; zero nowych bledow CrashScribe | - |

**Bieg dwuletni (728 dob)**: + paczka D, + projekt CZAS (+ S10, jesli Jeff zgodzi sie).

| Prog | Wartosc | fin30 |
|---|---|---|
| zdobycia razem | <= 35 | 57 [P] |
| szturmy odparte | >= 5 | 0 [P] |
| mediana dob do zdobycia | >= 20 (z CZAS) | 12 [P] |
| krolestwa znikniete przez oblezenia | 0 (Stormlands przez fabule ROT - poza regula) | Braavos d310 po szturmie, Norvos d635 bez szturmu [P] |
| Dorne - stracone twierdze | <= 4 | 8 [P] |
| nowe panstwa | <= 3 | 19 [P] |
| seria jednej armii | zadna armia nie bierze 3 twierdz w 30 dob | Robb: KP d530, Rosby d541, Duskendale d559 [P] |
| wydajnosc i gospodarka | jak P2, P3, P8 | - |

## 7. Czego nie wiem

- Ile armii, ktore nie zaczna oblezenia (40 w modelu), znajdzie inny cel. Stad widelki 45-52 zdobyc. Odpowie bieg roczny (linia O0 + L1).
- Jak czesto lord krolestwa jest w zasiegu doby od obleganej twierdzy (S7). Przyklady sa, czestosci nie mam (L5).
- Udzial milicji wsi w milicji* (przyjete 40%, z limitow BK) i to, jak `Settlement.Militia` przechodzi na partie milicji (S5). Trzeba sprawdzic przed kodem.
- Czy ROT i NavalDLC wolaja `Default.CalculateGarrisonPartySizeLimit` (S1). Rozpoznanie mowi "przepuszczaja", ale postfiksu na tej metodzie nie testowalem.
- Co zabralo zaloge KP d524-d529 (541 -> 271 bez glodu i bez bitwy). Patrol BK w oblezeniu nie wychodzi [K `BKSettlementBehavior.cs:453`], ale mogl wyjsc przed oblezeniem. L2 i L7 to pokaza.
- Dlaczego rody odchodza z lennem w roku 2 (S11 - pomiar, nie zakladam).
- Model szturmu liczy wartosci oczekiwane i nie ma w nim morale, perkow, smokow ROT, Stirrups BK (+20% Essos) ani tego, ze w czasie szturmu dolaczaja nowe druzyny (poza odsiecza z B175).
  P (ludzie ziemi) liczylem z regionu `ludzie-regiony.csv`, nie z czynnika GL4.
- Skutek S8 w biegu: zalozone przesuniecie milicji Reach -> Dorne przy tej samej sumie swiata [K normalizacja GL4], nie liczone na fin30.

## 8. Pliki robocze (`docs\oblezenia\robocze\projekt-sily\`)

| Plik | Co |
|---|---|
| `model_sily.py` | model (python -I): prog startu, okup, szturm wedlug wzorow gry, warianty S1/S5/S6/CZAS, przyklady S7, koszt normy |
| `model-wynik-warianty.txt` | wynik modelu (tabela rozdz. 4) |
| `model-wyniki.tsv` | 113 decyzji: zaloga, milicja*, norma, sila przed / po, wynik bez zmian i z projektem |
| `p_krol.py` | czynnik P i suma normy wedlug krolestw (d1), ludzie swiata |

Dane wejsciowe (tylko odczyt): `robocze\sily\okup-O0-fin30.tsv`, `robocze\sily\zdobycia-szczegoly.tsv`, `robocze\sily\bitwy-pod-murami.tsv`, `robocze\mody\fin30-decyzje-szturmu-O0-wynik.csv`,
`D:\Backup-Bannerlord\logs\2026-10-14-noc\tabele-06-01-15\ludzie-regiony.csv`, `budzet-rodow.csv`, `balans-krolestw.csv`.
