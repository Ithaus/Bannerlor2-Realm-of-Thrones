## p6-drogi (20.10) - AI wybiera drogi: lordowie, karawany i wozy wsi skrecaja na trakt (Armoury, R3-07d)

**Dla Jeffa (prosto).** Dotad lordowie, karawany i wozy wsi szly prosto przez pola i lasy, a na droge trafialy tylko przypadkiem.
Teraz, gdy taka partia rusza w drozsza droge (od ok. 140 km), sprawdza, czy oplaca sie wjechac na trakt: jedzie do najblizszego dobrego kawalka drogi,
jedzie nim i zjezdza blisko celu. Robi to tylko wtedy, gdy droga jest najwyzej o 15% dluzsza, a dzieki premii drogi dojedzie tak samo szybko
albo szybciej (liczone z ta sama premia, ktora daje gra, takze na kawalkach drogi, ktore lezaly juz na starej trasie). Dotyczy tez lordow jadacych
na uczte albo do majatku (Banner Kings). W pogoni, ucieczce, przy oblezeniu, najezdzie, odsieczy i w bitwie AI dalej idzie jak w grze. Twoja partia
i bandy bez zmian. W logu jest tez prawdziwy pomiar: ile godzin partie z objazdem jechaly do celu w porownaniu z partiami bez objazdu.
Uwaga: ROT namalowal drogi tylko na czesci mapy (glownie Westeros) - tam, gdzie drogi nie ma, nic sie nie zmienia.

**Problem / zrodlo pomyslu.** POMYSLY-35 pkt 20 (R3-07d), Jeff 20.10: "28 - nie, 29 - nie. Pozostale tak" i "Rob od razu fale 2".
Audyt drog 09.10 (`docs/audyt-2026-10-09/08-drogi-wioski-widok.md` 1.4, 3.4, tabela 4.3 M-4): trasa partii = najkrotsza po scianach siatki, drog nie zna.
Dowod z logu 160 dob (WGRANIE 91, `Armoury-2026-10-20_00-44-29.log`, linie `Drogi (DR): dzien`): partio-godziny w ruchu na drodze - rody 7.3-9.0%,
karawany 7.9-9.8%, wozy wsi 7.6-8.4% (bandy 1.7-3.0%) - przy szerszym polu drogi (1.0 jedn.) AI lapie premie +15% tylko przypadkiem.

**Zmiana.** Nowy plik `Armoury/src/AiRoadRoutes.cs` + minimalne wpiecia.
- DEKOMPILACJA 1.4.8 (sprawdzone, nie zgadywane): `MobileParty.DoAIMove` -> `IsComputePathCacheDirty` (cel zmienil sciane / pozycje, inny rodzaj nawigacji,
  koniec trasy) -> prywatna `MobileParty.ComputePath(CampaignVec2, NavigationType, bool)` -> `IMapScene.GetPathBetweenAIFaces` (silnik, A* po scianach,
  zakazane typy scian z `PartyNavigationModel`) -> `NavigationPath` (stale 128 punktow). Partia idzie po prostych miedzy punktami (`DoAiPathMode`), ruch
  sprawdza tylko wyspe sciany (`TryToMoveThePartyWithCurrentTickMoveData`). ComputePath wolaja watki robocze gry (ruch partii, `CampaignTickCacheDataStore.RealTick`
  -> `TWParallel.For`), glowny (`ComputePathAfterLoad`, `FinishNavigationTransitionInternal`) i liczenie ucieczki (`RecalculateLongTermPath` z `MobilePartyAi`).
  Raz na nowy cel, nie co klatke.
- KOSZT SCIAN DROGI - porownany i odrzucony: drogi ROT to farba terenu, nie sciany siatki (sciany sa duze, droga ma ok. 0.5 jedn.); koszt scian liczy silnik,
  a jedyne pokretla to lista scian zakazanych i mnoznik calej trasy. Tanszej drogi po siatce nie da sie dac bez przebudowy siatki mapy ROT. Wybrane:
  PUNKTY POSREDNIE w trasie po jej wyznaczeniu przez gre (postfiks ComputePath).
- KTO: lord (takze wodz armii; czlonkowie armii nie licza tras), karawana, woz wsi. Nie gracz, nie bandy/patrole/milicja. Marsz ladem (`NavigationType.Default`,
  partia na ladzie, bez przejscia lad-morze i tratwy), ruch do punktu (`MoveModeType.Point`), bez `ForceAiNoPathMode`; AI czynne albo wylaczone przez
  inny mod przy rozkazie do osady / punktu (uczta BK `DisableAi` + `SetMoveGoToSettlement`, majatek gentry BK `DisableForHours(72)` - po recenzji).
  Zachowanie dlugie `GoToSettlement` / `GoToPoint` / `PatrolAroundPoint`, krotkie to samo albo `None`. Bez pogoni (`EngageParty`, `GoAroundParty`,
  `EscortParty`), ucieczki (`IsFleeing`; liczenie ucieczki `RecalculateLongTermPath` - prefiks/finalizer z licznikiem [ThreadStatic]), oblezenia
  (`BesiegeSettlement`, `AssaultSettlement`, `SiegeEvent`, `BesiegedSettlement`), najazdu (`RaidSettlement`), odsieczy (`DefendSettlement`), bitwy (`MapEvent`).
  Trasa gry co najmniej `MapRoadAiMinDistance` (30 jedn.).
- SIEC (raz na sesje, watek glowny, po `MapRoads.SessionStart`; wymaga czynnej premii drog `MapRoads.Ready`): ten sam plik `ModuleData/arm_map_roads_geo.tsv`.
  Lamane uproszczone Douglas-Peucker 0.5 jedn. (cieciwa najwyzej 0.5 od osi drogi - w polu premii: pol-szerokosc >= 0.15 + margines 1.0), konce lamanych
  laczone z najblizszym wierzcholkiem innej lamanej do 0.6 jedn. (rozstaje; pomiar pliku: 402 z 622 koncow ma sasiada do 0.5, nic wiecej do 1.5).
  Symulacja pliku (p6-drogi-tmp/py/sym.py, poza repo): 1819 wezlow, 1910 krawedzi, 402 rozstaje. Kazdy wezel: sciana siatki gry i jej waznosc dla marszu ladem
  (`CampaignVec2.IsValid`). Kazda krawedz raz sprawdzona tym samym wywolaniem silnika co ComputePath: ta sama sciana albo sciezka silnika = sama cieciwa
  (kazdy punkt do 0.02 jedn. od niej) -> PROSTA (w planowaniu bez silnika); objazd do 3 x cieciwa + 2 -> dlugosc objazdu, silnik w planowaniu; bez sciezki
  albo dalej (np. rzeka bez mostu pod farba) -> krawedz WYCIETA.
- WYBOR (watek roboczy, bez alokacji - pamiec [ThreadStatic] raz na watek): Dijkstra z wielu zrodel - wezly w promieniu R (1/4 trasy gry, 3..40 jedn.) od
  startu, koszt = odcinek poza droga x 1 + droga x 1/(1 + premia traktu `MapRoadSpeedPercent`); zejscie w promieniu R od celu. Najtanszy wariant
  "start -> wejscie -> trakt -> zejscie -> cel" przyjety (po policzeniu trasy przez silnik), gdy: koszt przejazdu objazdu <= koszt przejazdu trasy gry
  (oba z probek co 1 jedn. ta sama miara co premia - `MapRoads.KindAt`: trakt / (1 + premia), droga boczna / (1 + premia boczna), reszta x 1), trakt
  z probek >= 50% trasy, dlugosc <= trasa gry x (1 + `MapRoadAiDetourPercent`/100) - nadklad to tylko sufit dlugosci (po recenzji).
  Wejscie / zejscie to najlepszy wezel w zasiegu, nie slepo najblizszy (najblizszy bywa na drodze w bok).
- TRASA: start -> wejscie i zejscie -> cel (oraz krawedzie z objazdem) liczy silnik tym samym wywolaniem co gra (promien 0.3, te same sciany zakazane,
  mnoznik 2, koszty przejscia lad-morze z `GetRegionSwitchCost...` w tej samej kolejnosci). Ponad 128 punktow / odcinek bez sciezki / dlugosc albo udzial drogi
  po policzeniu poza progiem = trasa gry bez zmian. Przyjeta trasa zastepuje punkty `NavigationPath` (Size, PathPoints; `PathBegin = 0` ustawia ComputePath),
  `NextLongTermPathPoint` jak w grze. Cel bez zmian - `DoAIMove` porownuje cel, nie punkty trasy.
- PRZEPRAWY: lord w wojnie z panem przeprawy CrossingLaw (Twins, Moat Cailin, Bloody Gate - te same id) nie dostaje objazdu przez strefe (CrossingRadius + 1
  jedn.) - inaczej droga wiodlaby go pod most, straz odbija, po 3 h przerwy znow objazd.
- POWTORKA CELU: pamiec partii bez blokad (1 << 17 slotow long: Party.Index | skrot celu | stan; godzina i liczba planow osobno) - ten sam cel w tej samej
  godzinie po ODRZUCONYM objezdzie = bez planowania; po PRZYJETYM objezdzie planowanie od nowa (gra nadpisala trase, a bez zmiany celu nie przeliczy
  jej do konca marszu); najwyzej 3 plany partii na godzine, dalej trasa gry do konca marszu albo nastepnego planu - wtedy stan slotu "odrzucony" (licznik
  `objazd utracony`), tak samo gdy gra liczy trase do celu objazdu bez planu (np. reszta trasy krotsza od progu).
- POMIAR (tylko log): `AI drogi (AD): latka MobileParty.ComputePath ...` (start gry), `AI drogi (AD): start sesji ...` (siec, krawedzie, przeprawy, czas budowy,
  CZYNNE/powod), `AI drogi (AD): dzien N | ...` (liczone, krotsze, powtorki, planowane, objazdy wg rodzaju, odrzucone z powodem, ominiete strefy przepraw,
  sredni nadklad / udzial drogi / czas szac., spis partio-godzin w marszu na drodze - TA SAMA MIARA co `Drogi (DR)`: `MapRoads.KindAt == 2` (trakt) w punkcie
  partii, ten sam filtr partii -
  osobno partie na objezdzie i ile z nich na drodze, koszt ms/dobe, odcinki silnika, potkniecia). Linia w dobach pomiaru (`LogGate`, jak MapRoads).
- KLUCZE NOWE (grupa MCM "The marching column", po kluczach blota): `MapRoadAiRoutes` (TAK), `MapRoadAiMinDistance` (30, 10..200), `MapRoadAiDetourPercent`
  (15, 0..50; zakresy w `tools/gen_mcm.py`). Latki wpinane zawsze przy starcie gry (klucz dziala na zywo), postfiks wychodzi od razu przy kluczu NIE albo
  `MapRoadSpeedBonus` NIE. Wlaczenie w trakcie sesji (MapRoadAiRoutes albo MapRoadSpeedBonus) - siec budowana w nastepnej dobie.
- Wpiecia: `ArmouryBehavior.cs` (linia `_p6DrogiReset`, godzina po MapMud.Hourly, start sesji po MapMud.SessionStart, doba po MapMud.Daily),
  `SubModuleMain.cs` (ApplyAll po MapRoads.ApplyAll), `Settings.cs` (3 pola po MapMudWetSpeedPercent), `McmSettings.cs` (gen_mcm), `tools/gen_mcm.py` (2 zakresy).
  `tools/sprawdz_logi.py`: nowa grupa `--grupa p6drogi` (funkcja `p6drogi_main`, progi AD-0..AD-10; dopis pomocy przed `--grupa nks`, wywolanie po `ndrogi`).
- WZNOWIENIE (20.10, po "zacznij fale 2"): kod poprzedniego agenta (3 commity) przejrzany wobec dekompilacji 1.4.8 i n22-calosc (MapRoads / MapMud /
  CrossingLaw / MarchPace bez zmian od bazy; nikt inny nie latkuje ComputePath / RecalculateLongTermPath / NavigationPath; BK, NavalDLC, SCAI, Diplomacy,
  RealisticBannerlord nie czytaja Path / NextLongTermPathPoint). Poprawki: spis "na drodze" liczy tylko trakt (jak `Drogi (DR)`, wczesniej takze droga boczna),
  limit 3 planow partii na godzine, ponowna budowa sieci po wlaczeniu `MapRoadSpeedBonus` w trakcie sesji, grupa `p6drogi` w sprawdz_logi.py.

**Ryzyko / co sprawdzic (kontrola calosci).**
- ZASIEG: drogi sa tylko tam, gdzie ROT je namalowal. Symulacja (L = prosta x 1.15): polowa miast/zamkow nie ma wezla drogi w promieniu 20 jedn.
  (mediana 21.3, p75 87.5 - glownie Essos); objazd w 13% skokow karawan do 5 najblizszych miast i 5% kursow wozow wsi (z tego "bez drogi w zasiegu"
  79% / 51%). Tam, gdzie objazd jest: droga ok. 70% trasy, sr. 8-11 wezlow (maks. 29 - daleko od 128 punktow). Drogi boczne do wiosek (MapSideRoads,
  plan liczony w widoku) NIE sa w sieci - tylko trakty z pliku.
- WOJNA: marsze wojenne (oblezenie, najazd, odsiecz) bez objazdu - swiadomie (zadanie: "bez oblezenia"); armie w pokoju i lordowie do osad tak.
  Czasy wojen i oblezen nie zmieniaja sie przez objazd.
- CZASY DOSTAW: objazd przyjety tylko przy koszcie przejazdu <= trasa gry (czynna premia drogi na obu trasach). Szacunek pomija teren (las -30%, snieg)
  i bloto - dlatego pomiar `dojazd zmierzony` (AD-11): sredni czas dojazdu objazdow wobec proby porownawczej (trasa gry, choc droga w zasiegu), obie
  grupy wzgledem czasu trasy gry przy predkosci bazowej z planu. `czas szac.` (AD-5) to tylko konstrukcja (INFO).
- UTKNIECIE: trasa sklada sie z odcinkow silnika (jak trasa gry) albo z cieciw, ktore silnik sam poprowadzil prosto (sprawdzone przy budowie) - partia nie idzie
  po prostej przez sciane innej wyspy. Ryzyko szczatkowe: brzeg sciany na tolerancji 0.02 jedn. Sprawdzic w autotescie: partie stojace w miejscu z celem
  (linie BLAD / FailedAssert "Path finding target is not valid", kronika ruchu), "na objezdzie" partio-godziny rosna z dniem.
- POWTORKA CELU: po przyjetym objezdzie ponowne ComputePath do tego samego celu planuje od nowa (licznik `ponowione po objezdzie`); po limicie 3 planow
  na godzine albo bez planu zostaje trasa gry do konca marszu (licznik `objazd utracony`) - spis nie liczy wtedy partii jako objazdu.
- WATKI: graf niezmienny (volatile), pamiec robocza na watek, liczniki Interlocked, sloty long atomowe; `IsAtWarWith` czytany w ruchu rownoleglym
  (dyplomacja zmienia sie tylko na watku glownym poza `TWParallel.For`). Bez alokacji w planowaniu (poza pierwsza pamiecia watku).
- KOSZT: budowa sieci - ok. 1900 wywolan silnika raz na sesje (oczekiwane dziesiatki ms, w linii startu); planowanie: Dijkstra po <= 1819 wezlach + silnik
  tylko na koncach i krawedziach z objazdem. Sprawdzic `koszt: planowanie X ms/dobe` (cel < 50 ms/dobe; przy wiecej - podniesc `MapRoadAiMinDistance`).
- WCZYTANIE: `ComputePathAfterLoad` biegnie przed OnSessionLaunched (sieci jeszcze nie ma) - po wczytaniu trasy gry do pierwszej zmiany celu. Zapis bez zmian
  (stary zapis i nowa kampania save040 = stan poczatkowy), pamiec partii czyszczona przy nowej kampanii / przed wczytaniem (`_p6DrogiReset`) i na starcie sesji.
- KOLIZJE: StrategicCampaignAI145 (`SetMove...IfNeeded` co godzine, tylko przy zmianie celu) i Banner Kings (`SetMoveGoToSettlement` w zachowaniach,
  `AiDecisionTracePatches` tylko log) nie latkuja ComputePath / NavigationPath i nie przesuwaja celu co klatke - ten sam cel nie liczy trasy od nowa
  (dekompilacja DoAIMove: ta sama sciana i pozycja celu). NavalDLC bez ComputePath/GetPathBetweenAIFaces. Nasze: CrossingLaw (strefy - wyzej), NightRest
  (sen zatrzymuje rozkaz - inna latka, SetMove*), MapRoads/MapMud (predkosc - objazd daje im wiecej partii na drodze; pamiec predkosci przelicza sie przy
  wejsciu na droge jak dotad). Latka ComputePath w linii startu podaje liczbe cudzych latek na tej metodzie.
- Zamknieta ekonomia: bez zlota i towarow (tylko trasa). Gracz bez zmian.

**Status.** DO SPRAWDZENIA (build Armoury Release: kod 0, 0 Error(s); autotestu nie uruchamialem - zakaz w zadaniu).

**Decyzje (moje, Jeff: "ekonomie projektuje Claude").**
- Punkty posrednie zamiast kosztu scian: koszt scian drogi niewykonalny (drogi to farba, nie sciany siatki; koszt liczy silnik).
- Marsz od 30 jedn. (ok. 140 km), nie 60: pozycje osad ROT - kurs wozu wsi do swojej osady mediana 29 jedn. (przy 60 objalby 6% kursow, przy 30 - 47%),
  skok karawany miedzy miastami mediana 69 (przy 30 - 96%). Tak regula obejmuje wszystkie trzy rodzaje partii, nie ulamek. Klucz `MapRoadAiMinDistance`.
- Prog "15% dluzej" liczony od trasy gry (z gorami i rzekami), nie od prostej - to jest prawdziwa alternatywa partii. Do tego koszt z waga drogi 1/1.15
  i droga co najmniej polowa trasy: objazd tylko, gdy partia na nim nie traci, a "dotkniecie drogi" na chwile nie jest objazdem.
- Najlepsze wejscie/zejscie w zasiegu (Dijkstra z wielu zrodel) zamiast slepo najblizszego punktu drogi.
- Bez marszow wojennych (oblezenie, najazd, odsiecz) i bez drog bocznych do wiosek w sieci - mniejsze ryzyko na pierwszy krok; do rozszerzenia po tescie.
- Wrogi lord omija strefy przepraw CrossingLaw.
- Bez zapisu w save (trasa liczy sie od nowa po wczytaniu).

**Test w grze / log_grep.**
- `AI drogi (AD): latka MobileParty.ComputePath wpieta` (start gry; `cudzych latek na ComputePath 0` oczekiwane).
- `AI drogi (AD): start sesji - klucz MapRoadAiRoutes tak` ... `| CZYNNE.` - siec: `wezlow` ok. 1800-1900, `poza siatka gry` maly ulamek, `krawedzie po siatce gry:
  proste` wiekszosc, `wyciete` mala liczba, `przeprawy CrossingLaw 3/3`, `budowa` < ok. 300 ms.
- `AI drogi (AD): dzien` - `objazdy droga N` > 0 z rodami, karawanami i wozami wsi > 0; `odrzucone` z powodami (najwiecej `bez drogi w zasiegu` - Essos);
  `objazd sr.: nadklad drogi` <= +15% (bywa ujemny - znak z liczby), `droga` >= 50%, `czas szac.` <= 100% (konstrukcja); `dojazd zmierzony ... objazdy N sr. X%,
  proba ... M sr. Y%` - X <= Y + 5 pp; `na objezdzie K, z tego na drodze J` z udzialem wyraznie ponad 9% (oczekiwane > 50%);
  `koszt: planowanie` < ok. 50 ms/dobe; `potkniecia dzis 0`.
- Porownanie z WGRANIEM 91: w `Drogi (DR): dzien` udzial `rody`, `karawany`, `wozy wsi` powinien wzrosnac ponad 7-10% (bandy bez zmian ok. 2-3%);
  NDR-3 (`--grupa ndrogi`, udzial calego AI 1-25%) moze wzrosnac z ok. 6.5% - nadal w progu.
- `python -I tools/sprawdz_logi.py --grupa p6drogi <Armoury-*.log>` - AD-0..AD-4, AD-6, AD-8, AD-9, AD-11 TAK (AD-5, AD-7, AD-10 INFO; AD-11 INFO, gdy
  mniej niz 30 dojazdow w grupie).
- Brak nowych bledow w CrashScribe (FailedAssert sciezki, NullReference w ComputePath / DoAiPathMode) i brak partii stojacych z celem (np. karawany bez
  handlu przez wiele dni) - porownac liczbe kursow wozow/karawan z linii ekonomii.

**Recenzja (dwie soczewki, gra i technika; wszystkie uwagi drobne, wszystkie prawdziwe - poprawione).**
1. Waga drogi z klucza nadkladu, nie z premii (obie soczewki). PRAWDA: przy `MapRoadAiDetourPercent` 30 i premii 15% objazd 1.3 L w calosci droga
   przechodzil (1.3 L / 1.3 = L), a jechal 1.13 trasy gry. POPRAWKA: waga = 1 / (1 + `MapRoadSpeedPercent`) (przyciete 0..50, jak `MapRoads.PctOf`);
   warunek czasu po policzeniu trasy z probek obu tras co 1 jedn. (`MapRoads.KindAt`: trakt, droga boczna z premia boczna, reszta) - trasa gry tez bywa
   czesciowo na drodze; nadklad tylko sufitem dlugosci; premia 0 = bez objazdow (`NIECZYNNE (MapRoadSpeedPercent 0 ...)`). Teren (las, snieg) i bloto
   dalej poza szacunkiem - stad pomiar z pkt 2. Podpowiedzi MCM `MapRoadAiRoutes` / `MapRoadAiDetourPercent` poprawione (gen_mcm).
2. AD-5 tautologia (obie soczewki). PRAWDA: trzy warunki AD-5 kod wymusza przy przyjeciu. POPRAWKA: AD-5 = INFO (konstrukcja); nowy pomiar
   `dojazd zmierzony` w linii doby i prog AD-11: przy nowym celu godzina planu, cel i czas trasy gry przy predkosci bazowej (`LastCalculatedBaseSpeed`);
   dojazd = partia do 2 jedn. od celu albo w osadzie celu (spis co godzine albo nastepny plan, blad do 1 h w obu grupach); kazda trasa do innego celu
   przed dojazdem (takze pogon) = `przerwane`; ponad 10 dob albo 5 x szacunek = `poza pomiarem`. Grupy: objazdy i proba porownawcza (plan odrzucony
   mimo drogi w zasiegu: nie oplaca sie / malo drogi / za dlugie) - te same noce, postoje, teren. AD-11 TAK, gdy srednia objazdow <= srednia proby
   + 5 pp (po >= 30 dojazdow w obu grupach); mniej - INFO.
3. Powtorka celu po przyjetym objezdzie zostawiala trase gry do konca marszu i spis liczyl partie jako objazd (obie soczewki). PRAWDA (DoAIMove:
   `_pathMode` + ta sama sciana celu = bez ponownego liczenia). POPRAWKA: powtorka pomijana tylko po odrzuconym; po przyjetym planowanie od nowa
   (`ponowione po objezdzie`); po limicie 3 planow albo gdy trasa do celu objazdu liczy sie bez planu (krotka reszta trasy, inny rodzaj nawigacji,
   zmiana zachowania) stan slotu "odrzucony" (`objazd utracony`). Komentarz w kodzie i ten wpis poprawione ("do konca godziny" bylo nieprawda).
4. Lordowie z AI wylaczonym przez BK (uczta, majatek gentry) bez objazdu (soczewka gra). PRAWDA (BKFeastBehavior: `DisableAi` + `SetMoveGoToSettlement`;
   BKGentryBehavior: `DisableForHours(72)`). POPRAWKA: przy rozkazie `GoToSettlement` / `GoToPoint` dopuszczone (patrol wokol punktu - nie, to wlasne
   myslenie AI). Nasze uspienia (NightRest / NightMarch / SleepGate / ArmyRestDay) daja rozkaz `Hold` - dalej poza regula (sprawdzone grepem w n22-calosc).
   Licznik w linii: `z AI wylaczonym przez inny mod - uczta / majatek BK`.
5. Ujemny nadklad psul AD-5 ("+-1.2%") (soczewka technika). PRAWDA. POPRAWKA: znak z liczby (format `+0.0;-0.0;0.0`), regex przyjmuje oba zapisy
   (sprawdzone na `p6-drogi-tmp/fake.log` z "+-1.2": AD-5 czyta -1.2).

Sprawdzenie poprawek: build Armoury Release kod 0, 0 Error(s); `sprawdz_logi --grupa p6drogi` na sztucznym logu nowej postaci (`p6-drogi-tmp/rec2/ok.log`:
12 progow, NIE 0, ujemny nadklad czytany; `bad.log`: AD-11 NIE przy objazdach 150% wobec proby 130%) i na logu WGRANIA 91 (INFO, bez wyjatku).
Koszt: probki ok. 2 x dlugosc trasy wywolan `KindAt` (dziesiatki ns) tylko dla wariantu po silniku; sprawdzanie dojazdu co godzine - ta sama petla
po partiach co spis, takze poza dobami pomiaru (bez alokacji). Pamiec: 5 tablic po 1 << 17 slotow (ok. 2.2 MB, raz na proces).
