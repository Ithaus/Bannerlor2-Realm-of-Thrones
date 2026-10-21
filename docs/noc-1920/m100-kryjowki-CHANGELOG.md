## m100-kryjowki - kryjowki: wypatrzenie ze Zwiadu, plotka odslania, nowe z dala od osad i nie tam, gdzie rozbita (Armoury)

**Dla Jeffa (prosto):** kryjowki bandytow nie wyskakuja juz same, gdy przejedziesz obok. Przy kazdym podejsciu Twoj zwiadowca ma jedna probe: przy Zwiadzie 10 i nizej
szansy nie ma wcale, przy 30 jest ok. 10%, przy 100 prawie polowa, przy 200 zawsze. Kryjowke, o ktorej uslyszales w karczmie albo zajezdzie ("an outlaw lair in the wilds
near X"), widzisz normalnie, jak dotad w grze - plotka mowi tylko mniej wiecej gdzie (najblizsza osada albo kierunek i dni drogi). Nowe kryjowki powstaja tylko na
pustkowiu: co najmniej 10 jednostek mapy (ok. 47 km) od miasta, zamku i wsi i 6 jednostek (ok. 28 km) od wioski pobocznej. Po rozbiciu kryjowki przez 60 dni zadna
nowa nie powstanie w promieniu 25 jednostek (ok. 120 km) od niej - za kazdym razem trzeba szukac od nowa. Tych samych zasad pilnuja teraz takze bandy, ktore
same zajmuja puste kryjowki (dotad mogly wrocic do rozbitej albo stanac przy wsi). Gdy w malej krainie (np. Kingswood) jedynym wolnym miejscem jest wlasnie rozbita
kryjowka, nowa po prostu sie nie pojawia, dopoki nie zwolni sie inne miejsce albo nie minie 60 dni.

**Problem:** Jeff 21.10 ok. 03:00 (doslownie): "im wiecej masz scouting, tym lepiej widzisz kryjowki bandytow, ale ponizej 10 scoutingu masz szanse 0%, od 11 do 200
rosna do 100% ... inaczej mozesz ja zdobyc w karczmie, pijac i zdobywajac plotki - jesli plotki powiedza, ze tu i tu jest obozowisko bandytow, to wtedy jak na nie
trafisz, wyswietli sie normalnie" oraz "Kryjowki powinny nie pokazywac sie na wioskach pobocznych, tylko tam, gdzie jest wolne miejsce, z dala od miast i wsi, oraz jak
jedna kryjowka zostanie rozbita, to kolejna powinna pojawic sie w innym miejscu".

### Rozpoznanie (dekompilacja 1.4.8, ROT 8.1.8, BK)

- **Gdzie gra wypatruje (tylko dla gracza):**
  - `HideoutCampaignBehavior.HourlyTickSettlement` - co godzine, kryjowka zajeta (`IsInfested`), niewypatrzona, ktos w srodku, gracz blizej niz
    `MapVisibilityModel.GetHideoutSpottingDistance()` (= `MainParty.SeeingRange x 1.2`, z perkiem Rumour Network wiecej); rzut gry `1 - d2/r2`; potem
    `IsSpotted = true`, `IsVisible = true`, zdarzenie `OnHideoutSpotted` (XP Zwiadu).
  - `BanditSpawnCampaignBehavior.OnSettlementEntered` - banda wchodzi do zajetej niewypatrzonej kryjowki, a gracz widzi te bande (`mobileParty.IsVisible`) - kryjowka
    wypatrzona od razu. W tej samej metodzie wczesniej `CheckForSpawningBanditBoss` (herszt TYLKO w wypatrzonej) - dlatego nie wolno ustawic falszywego `IsSpotted`
    na poczatku metody.
  - Pozostale `IsSpotted = true`: zadania (NearbyBanditBaseIssue, CapturedByBountyHunters, GangLeaderNeedsToOffload), wydarzenie (IncidentEffect), kody - to
    wiedza z zadania, zostaja bez zmian.
  - AI nie wypatruje (nie ma takiego stanu) - zmiana dotyczy tylko gracza. BK i ROT nie patchuja tych metod (sprawdzone grep w BannerKings.dll i ROT.dll).
  - `Hideout.IsSpotted` to zwykle pole (getter/setter bez logiki). Pustoszenie kryjowki (`Hideout.OnHideoutIsEmpty`) zeruje `IsSpotted` i `IsVisible`
    i wola `OnHideoutDeactivated`.
- **Gdzie gra aktywuje kryjowke:** `BanditSpawnCampaignBehavior.FillANewHideoutWithBandits` (start kampanii i dzienne `AddNewHideouts`) bierze punkt z
  `SelectANonInfestedHideoutOfSameCultureByWeight(Clan)` - niezajete punkty tej samej kultury, waga rosnie z odlegloscia od zajetych kryjowek, punkt walczony
  w ostatnich 1.5 doby ma wage 1. Pula ROT: **266 stalych punktow** (forest 67, sea_raiders 62, mountain 49, desert 31, steppe 24, robber_knights 11, wildlings 9,
  yiti 6, hilltribesmen 4, kingswood_outlaw 3). BK ma wlasna droge (`BKBanditBehavior.InfestHieout` przy narodzinach herszta BK) - poza ta zmiana.
- **Czy przesuwanie punktu jest bezpieczne: NIE.** Obrazek kryjowki to obiekt sceny mapy (nie idzie za `Settlement.Position`), pozycja osady jest w zapisie
  (zmiana = rozjazd obrazka i klikniecia), a sprawdzenie ladu / przejezdnosci siatki nawigacji z poziomu moda nie daje pewnosci (woda, gory). Ryzyko CTD i
  zepsutego zapisu - wybrana **wersja bezpieczna: wybor innego punktu z puli**.
- **Odleglosci (policzone z `ROT-Map/ModuleData/settlements.xml` i `arm_map_villages.tsv`, 1957 wiosek pobocznych):** najblizsza osada gry (miasto / zamek / wies)
  od kryjowki: 10% ponizej 12 jedn., najmniej 5.7; najblizsza wioska poboczna: mediana 8.3, najmniej 4.9 (wioski poboczne stoja gesto). Progi 10 (osada) i 6 (wioska
  poboczna): oba warunki spelnia **183 z 266** punktow (sam warunek osady 251, sam warunek wioski 194). W kazdej kulturze zostaja co najmniej 3 punkty:
  forest 42/67, sea_raiders 42/62, mountain 27/49, desert 22/31, steppe 23/24, robber_knights 7/11, wildlings 8/9, yiti 6/6, hilltribesmen 3/4, kingswood 3/3.
  Wioska poboczna >= 8 zostawilaby tylko 134 punkty (robber_knights 5, hilltribesmen 2) - za ciasno; linia startu w grze poda pule wedlug progow.
- **Plotki:** `Rumours.Hideouts` (plotka "There is an outlaw lair in the wilds near X / N days to the dir") juz mowi mniej wiecej gdzie (`Where`: najblizsza
  osada do 25 jedn. przy dobrej dokladnosci, inaczej dni drogi i strona swiata); tozsamosc wiesci `h|<id kryjowki>` (m98, plotki za kufel). Dotad mogla mowic
  o kryjowce juz wypatrzonej.

### Zasada z liczbami

Szansa wypatrzenia przez gracza (jeden rzut na podejscie, zwiadowca partii `EffectiveScout`, bez zwiadowcy - wodz):

| Zwiad | 5 | 10 | 11 | 30 | 100 | 150 | 200+ |
|---|---|---|---|---|---|---|---|
| szansa | 0% | 0% | 0.5% | 10.5% | 47.4% | 73.7% | 100% |

Krzywa: `Zwiad <= Min -> 0`, `Min..Full -> (Zwiad - Min) / (Full - Min)`, `>= Full -> 1` (Min 10, Full 200 - klucze). Rzut udany = gra wypatruje jak dotad
(swoj rzut odleglosci co godzine). Wynik zostaje na caly pobyt w zasiegu; odejscie dalej niz 1.5 x zasieg wzroku kryjowki zapomina wynik - kolejne podejscie to
nowy rzut. Kryjowka znana z plotki - bez naszego rzutu. Juz wypatrzone (z zapisu) zostaja.

Miejsce nowej kryjowki: wybor gry zostaje, gdy punkt ma >= 10 jedn. do miasta / zamku / wsi i >= 6 jedn. do wioski pobocznej oraz >= 25 jedn. do kryjowki rozbitej
w ostatnich 60 dniach. Inaczej: (1) losowanie z punktow tej kultury spelniajacych wszystko (waga 1 + odleglosc do najblizszej zajetej kryjowki - rozrzut jak w grze);
(2) brak - punkt z dala od osad, najdalszy od rozbitych; (3) brak - punkt najdalszy od osad. Po recenzji: w (2) i (3) nigdy blizej niz pol promienia (12.5 jedn.)
od rozbitej, w tym nigdy ten sam punkt; gdy nic innego nie zostaje - bez nowej kryjowki (linia `aktywacja brak`, gra probuje znowu kolejnej doby).
Bandy AI (klucz **HideoutGuideBanditAi**): wolny punkt, ktory nie spelnia odleglosci od osad albo lezy w promieniu 25 od rozbitej w ostatnich 60 dniach, nie jest
dla band celem "idz do kryjowki".

### Zmiana

- `Armoury/src/HideoutSight.cs` (nowe zachowanie kampanii + 4 latki, `AccessTools.DeclaredMethod`):
  - prefiks `HideoutCampaignBehavior.HourlyTickSettlement` - te same warunki co gra, potem bramka szansy;
  - prefiks + finalizer `BanditSpawnCampaignBehavior.OnSettlementEntered` i postfiks `CheckForSpawningBanditBoss` - przy nieudanym rzucie `IsSpotted = true`
    tylko na czas testu gry (po teście herszta), finalizer zdejmuje; herszt nie powstaje w niewypatrzonej kryjowce;
  - postfiks `SelectANonInfestedHideoutOfSameCultureByWeight` - miejsce nowej kryjowki;
  - zdarzenia: `OnHideoutSpotted` (linia "wypatrzona ... droga rzut / plotka / inne (zadanie, gra)"), `OnHideoutBattleCompleted` (wygrana napastnika = rozbita),
    `OnHideoutDeactivated` (pusta: plotka i wynik rzutu zapomniane; po walce w ostatnich 2 dobach = rozbita);
  - stan w zapisie `arm_hideout_sight` przez `SaveText.Sync`: znane z plotki, rozbite (id, x, y, doba; starsze niz pamiec usuwane), wyniki rzutow na pobyt.
    Stan w instancji zachowania (nowa kampania / wczytanie = czysto). Potkniecia liczone (linia doby), bez gaszenia funkcji.
- `Armoury/src/Rumours.cs` (2 linie): plotka o kryjowce tylko o zajetej i niewypatrzonej (przy kluczu); okno plotek pokazane graczowi -> `HideoutSight.Heard`
  dla kazdej wiesci `h|<id>` (zwykle plotki i plotki za kufel m98).
- `Settings.cs` + `gen_mcm.py` (zakresy) + `McmSettings.cs` (wygenerowane): **HideoutScoutingChance** (TAK), **HideoutScoutingMin** 10, **HideoutScoutingFull** 200,
  **HideoutRumourReveals** (TAK), **HideoutAwayFromSettlements** (TAK), **HideoutMinDistSettlement** 10, **HideoutMinDistSideVillage** 6,
  **HideoutRelocateAfterClear** (TAK), **HideoutClearedMemoryDays** 60, **HideoutClearedRadius** 25.
- `SubModuleMain.cs`: `HideoutSight.ApplyAll` (przed Leaks310) i `AddBehavior(new HideoutSight())` (po HideoutSpotter).
- `tools/sprawdz_logi.py --grupa m100kryjowki` (KR-0..KR-6).
- Linie logu `Kryjowki (m100): `: `latki wpiete N/4`, `start sesji` (klucze, krzywa z przykladami 5/30/100/200, Zwiad teraz, pula, wykluczone przez odleglosc,
  zajete z zapisu blizej niz prog, pamiec), `rzut <id>` (Zwiad, szansa, wynik, znana z plotki, droga, odleglosc / zasieg), `plotka o kryjowce <id>`,
  `wypatrzona <id>`, `aktywacja <id>` (jak wybrana, stopien, odleglosc do osady / wioski / rozbitej), `rozbita <id>`, `dzien N` (liczniki, potkniecia).

### Ryzyko / co sprawdzic (kontrola calosci)

- **Regresje:** HideoutSpotter (flaga po "Hideout spotted") czyta tylko `IsSpotted` co godzine - falszywe `IsSpotted` zyje tylko wewnatrz `OnSettlementEntered`
  (synchronicznie), wiec go nie widzi. Leaks310 (prefiks/postfiks `CheckForSpawningBanditBoss`) i OutlawLaw (prefiks/postfiks `OnSettlementEntered`, zloto) nie czytaja
  `IsSpotted` po tescie herszta - sprawdzone w kodzie. HideoutPurge / HideoutAlarm / nskarbiec - bez zmian (nie wybieraja ani nie wypatruja kryjowek).
- **Kolizje:** OutlawLaw `GateFill` (prefiks `FillANewHideoutWithBandits`) decyduje CZY, my - GDZIE (postfiks wyboru) - inne pytania. Rumours: przy kluczu plotka
  nie mowi juz o wypatrzonych kryjowkach (dotad mogla) - mniej plotek o kryjowkach przy wielu wypatrzonych.
- **Kryjowki zajete z zapisu** (sprzed zmiany) stoja tam, gdzie staly - nie ruszamy ich; linia startu podaje, ile z nich jest blizej niz prog. Zasada dotyczy kazdej
  NOWEJ kryjowki.
- **Poza zmiana:** BK `InfestHieout` (herszt BK zaklada kryjowke w swoim punkcie); zadania gry odslaniaja kryjowke same (linia "wypatrzona ... droga inne").
- **Wczytanie gry** nie losuje od nowa (wyniki rzutow w zapisie). Odejscie i powrot = nowy rzut (to jest szukanie).
- **Progi** wedlug analizy pliku mapy - linia startu poda pule po progach w grze; stopien 3 w KR-3b = brak wolnego punktu w kulturze (do decyzji, czy luzowac progi).

### Poprawki po recenzji (21.10, ok. 03:30-04:00)

Kazda uwaga sprawdzona w dekompilacji / kodzie; wszystkie prawdziwe.

1. **(wazne) Bandy AI same zasiedlaja wolne punkty z pominieciem wyboru - PRAWDA, poprawione.** `AiVisitSettlementBehavior.CalculateVisitHideoutScoresForBanditParty`
   (1.4.8, l. 494-563) daje wolnemu punktowi `0.25 + 0.75 x (20 - zajete) / 20`, a `Hideout.IsInfested` = bandy w srodku >= `NumberOfMinimumBanditPartiesInAHideoutToInfestIt`
   (ROT 4) - punkt zajmuje sie bez `FillANewHideoutWithBandits`. BK ma te sama metode w `BKAIVisitSettlementBehavior` (obok gry). Zmiana: postfiks na obu metodach
   (typy po nazwie; `PartyThinkParams` jako 3. parametr sprawdzany) zeruje wynik `GoToSettlement` dla punktow z migawki `_aiBlock`. Migawke liczy co godzine watek
   glowny (`HourScan`: wolne punkty, ktore nie spelniaja odleglosci od osad / wiosek pobocznych albo leza w promieniu rozbitych), podmieniajac cala referencje
   (`volatile`); watki AI tylko czytaja ja i zmieniaja wynik WLASNEJ partii (`SetBehaviorScore`), licznik przez `Interlocked`, bez Log i bez slownikow. Zajete kryjowki
   (>= 4 band) nie sa blokowane - bandy dalej wracaja do istniejacych. Pomiar: co godzine zbior zajetych porownany z poprzednim; nowa zajeta, ktora nie przeszla
   przez wybor (`_viaPick`), daje linie `aktywacja poza wyborem <id> ... zgodna z zasada TAK/NIE` + KR-7. Klucz **HideoutGuideBanditAi** (TAK) - wylaczony = bandy
   jak w grze. Opoznienie do 1 godziny po rozbiciu (migawka co godzine) - banda potrzebuje wielu godzin drogi i 4 band, zanim punkt jest zajety.
2. **(wazne) W pelnej kulturze po rozbiciu wraca ten sam punkt - PRAWDA, poprawione.** Kandydaci blizej niz pol promienia od rozbitej (w tym ona sama, odl. 0) sa
   wykluczeni ze wszystkich stopni; gdy nic nie zostaje, postfiks zwraca `null` - `FillANewHideoutWithBandits` ma `if (hideout != null)` (sprawdzone, l. 302-303),
   start kampanii idzie ta sama metoda. Linia `aktywacja brak (<kultura>) | wybor gry <id> przy rozbitej <id> D ...`. KR-5 liczy teraz z pola `od rozbitej X D
   (promien R)`: D < R/2 zawsze NIE, stopien 0/1 z D < R NIE, stopien 2/3 w pasie R/2..R INFO.
3. **(wazne) Nowa kampania: startowe kryjowki przed wczytaniem ukladu wiosek pobocznych - PRAWDA, poprawione.** `Campaign.DoLoadingForGameType`
   (PostInitializeFourthState) wola `OnNewGameCreated` przed `OnSessionStart`, a `VillageBook.Resolve` (jedyne wczytanie `_file`) idzie w OnSessionLaunched.
   Nowe `VillageBook.EnsureLayoutRecords()` wola `LoadFile()`, gdy ukladu jeszcze nie ma. Bezpieczne: scena mapy jest juz wczytana (LoadVisualsThirdState,
   `MapSceneWrapper.GetSceneXmlCrc()` wolane tuz przed `OnNewGameCreated`), wybor pliku czyta MCM wprost (`MapVillageData.EnsureChosen` -> `McmLayoutV5/6/7`),
   `LoadFile` jest raz na plik - `Resolve` potem nie wczytuje drugi raz i dostaje ten sam `_file`. `HideoutSight.EnsureDist` uzywa tej funkcji. Gdyby uklad mimo
   to nie wszedl, linia aktywacji pisze `wioska poboczna - brak warstwy`, a KR-3c pokazuje to jako INFO (nie TAK).
4. (drobne) Herszt BK zawsze w pierwszym punkcie kultury - PRAWDA, tylko pomiar: postfiks `BKBanditBehavior.InfestHieout` pisze `aktywacja BK <id> ... zgodna z
   zasada TAK/NIE` (KR-7 INFO). Podmiany punktu NIE robimy: `CreateBanditHero` tworzy partie herszta w tym punkcie i po `InfestHieout` wprowadza ja do niego -
   inna kryjowka rozdzielilaby herszta i bandy. Rzadkie (raz w tygodniu, 2.5%, gdy klan nie ma herszta; OutlawLaw `GateBkHero` / `GateBkInfest` dalej decyduja).
5. (drobne) Wynik rzutu nie zapominany przy chwilowo pustej kryjowce - PRAWDA, poprawione: `HourScan` co godzine usuwa wyniki kryjowek dalej niz 1.5 x zasieg,
   niezaleznie od zajetosci.
6. (drobne) Plotka o kryjowce "znana" przed przeczytaniem strony - PRAWDA, poprawione: `Rumours.Show` przypisuje kazda wiesc `h|<id>` do strony, a
   `HideoutSight.Heard` idzie w `ShowPage` dla strony, ktora gracz wlasnie widzi ("Hear more" -> nastepna strona).
7. (drobne) Chwilowe opustoszenie kasowalo wiedze z plotki - PRAWDA, poprawione: `OnDeactivated` zapomina tylko wynik rzutu; wiedza z plotki znika dopiero przy
   rozbiciu (`MarkCleared`) - punkt stoi w tym samym miejscu.
8. (drobne) KR-5 nie zapominal rozbitych - PRAWDA, poprawione (pkt 2: pole linii aktywacji juz uwzglednia pamiec 60 dni).
9. (drobne) `HideoutSpotter._seen` nie zdejmowal kryjowki - PRAWDA, poprawione: kryjowka z `IsSpotted = false` schodzi z `_seen`, ponowne wypatrzenie daje znow
   "Hideout spotted" i flage.

Latki teraz: 4 rdzenia + bandy AI (gra i BK, 2/2) + herszt BK (1/1); linia `latki wpiete 4/4 | bandy AI 2/2, BK herszt 1/1`. Linia doby: `brak miejsca`,
`poza wyborem N (niezgodne M), BK K, bandy AI odciete C (bledy E, zakazanych punktow Z), zapomniane rzuty F`.

### Status: DO SPRAWDZENIA

Build Armoury Release kod 0 (takze po poprawkach recenzji). Gra nie uruchamiana (trwa test 99). Nic nie wgrane. Grupa KR sprawdzona na sztucznym logu
(wszystkie wiersze TAK/INFO; drugi log z bledami: KR-5 i KR-7 daja NIE).

### Test w grze krok po kroku

1. Wgrac DLL, wczytac kampanie. W `Armoury.log`: `Kryjowki (m100): latki wpiete 4/4 | bandy AI 2/2, BK herszt 1/1` i `start sesji` (klucze TAK, w tym
   Hideout Guide Bandit Ai, krzywa 5: 0.0%, 30: 10.5%, 100: 47.4%,
   200: 100.0%, pula 266, wykluczonych przez odleglosc ...).
2. Bohater ze Zwiadem <= 10 (bez zwiadowcy w druzynie): podjechac pod znana z mapy okolice kryjowki (np. z `rozbita`/`aktywacja` w logu). Oczekiwane: linia
   `rzut <id> | Zwiad N, szansa 0.0%, wynik NIE`, brak "Hideout spotted", brak ikony. Odjechac i wrocic - nowy `rzut`, dalej NIE.
3. Wejsc do karczmy / zajazdu, posluchac plotek (albo kupic kufel). Przy plotce "outlaw lair in the wilds near X": linia `plotka o kryjowce <id>`. Pojechac tam -
   `rzut <id> ... znana z plotki TAK`, potem w ciagu kilku godzin w zasiegu "Hideout spotted" i linia `wypatrzona <id> ... droga plotka`.
4. Wyznaczyc zwiadowce z wysokim Zwiadem (albo cheat skill) - rzut z szansa wedlug tabeli; przy 200 zawsze TAK.
5. Rozbic kryjowke. Linia `rozbita <id> (bitwa)`. W kolejnych dniach `aktywacja <inne id> ... od rozbitej <id> D (promien 25)` z D >= 25 i osada >= 10, wioska >= 6.
6. Zapisac, wczytac - `start sesji ... pamiec: znanych z plotki N, rozbitych M` te same liczby.
7. `python -I tools/sprawdz_logi.py --grupa m100kryjowki <Armoury-*.log>` - KR-0..KR-5 i KR-7 TAK (albo INFO przy braku zdarzen), ERROR 0.
8. (recenzja) Po kilku dobach: linie `dzien N` maja `bandy AI odciete C` > 0 i `zakazanych punktow Z` > 0; linie `aktywacja poza wyborem` (jesli sa) z
   `zgodna z zasada TAK` po pierwszej dobie sesji. Nowa kampania: pierwsze `aktywacja` (start, ok. 50) z liczba przy `wioska poboczna`, nie `- brak warstwy`.
9. (recenzja) Kraina z malo punktami (kingswood_outlaw 3): po rozbiciu, gdy pozostale zajete - `aktywacja brak (kingswood_outlaw)`, a nie ta sama kryjowka.
