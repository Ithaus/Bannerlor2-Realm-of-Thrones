## m109-oboz - bitwa w obozie stawia ten sam oboz co spacer po obozie (23.10)

**Problem:** Jeff 22.10 21:20 (zrzut, wersja 103.5): "inny jest oboz, jak wchodze do obozu - jedno polanie, a inny, jak jestem atakowany
w obozie; moze przez jencow? jency dodaja namioty?". Log `Armoury-2026-10-22_20-54-10.log` 21:20:01: `CampScene: postawiono 10 elementow obozu
na polu bitwy` (staly zestaw), spacer 20:18:13 i 22:31:47 (`Armoury-2026-10-22_19-54-56.log`, `..._22-28-22.log`): `ludzi zdrowych 0 ... namioty 0,
poslania 1, ogniska 1 ... konie 1` / `konie 2`.

**Przyczyna:** dwa niezalezne kody obozu. Spacer (`CampWalk.cs`, m97) buduje oboz wedlug kolumny: namioty ROT po jednym na Camp Walk Men Per Tent (6)
zdrowych ludzi, wodza od 6, oficerski od 60, ogniska, poslania, wozy CampCart, konie taboru; sam gracz = poslanie, ognisko i kon. Bitwa w obozie
(`CampScene.cs`, E7/O2) stawia STALY zestaw Native: 3 namioty tent_vlandia_c, ognisko, 4 pochodnie, 2 ploty - bez wzgledu na liczbe ludzi.
Jency NIE dodaja namiotow ani tu, ani tu: `CampScene` nie czyta zadnego rostera, spacer czyta tylko `MemberRoster` bez bohaterow (jedyne slowo
o jencach to `Hero.MainHero.IsPrisoner` w `CanWalk`).

**Zmiana (klucz `CampBattleLikeWalk`, domyslnie TAK; NIE = stary zestaw Native 1:1 jak 44c03a05):**
- `Armoury/src/CampWalk.cs`: nowa klasa `CampLayout` - caly uklad scenografii spaceru przeniesiony bez zmian wartosci i kolejnosci FreeSpot/Occupy
  (stale, listy prefabow, obrysy Footprints, Probe, Blocked, FreeSpot, Put, konie taboru i jezdzcow, uwiazanie, pilnowanie). Uklad lokalny obozu:
  srodek C, os Up, Right = (Up.y, -Up.x); obrysy w tym ukladzie (spacer: Up = (0, 1) - obrysy dawnego spaceru przesuniete o srodek, test nakladania
  ten sam). Plan wedlug regul (`CampLayout.Count` / `Plan.Recount`: namioty, zolnierskie, poslania, ogniska, wozy, konie taboru) - te same liczby,
  ktore uklad probuje postawic. Ludzie spaceru (gracz, siedzacy, chorazy) zostaja w `CampWalkLogic` (haki OnPlayerSpot, OnHeroHorseSpot,
  OnLeaderTent); linia "Spacer po obozie (m97): wejscie" bez zmian.
- `Armoury/src/CampScene.cs`: `BuildLikeWalk` po rozstawieniu wojska (ten sam wyzwalacz co E7/O2): strona gracza = aktywni ludzie wszystkich druzyn
  tej strony, kierunek do wroga (`FrontYaw`), os obozu od wroga, srodek obozu 21 m za ostatnim zolnierzem (6 m odstepu + 15 m - rzad wozow 12 m, obrys
  woza 2.4 m, zapas), namioty wejsciem do wroga. Bez fizyki (jak dotad), nic blizej niz 6 m od kazdego agenta (migawka) i od linii wojska (zaden obrys
  przed y = -15 w ukladzie obozu); bez ludzi przy ogniskach, bez konia gracza i koni jezdzcow (miejsca gracza i jego konia zarezerwowane, puste).
  Malo miejsca za linia: scenografia (namioty, ogniska, poslania, wozy - bez koni) moze stac do 100 m za granica pola bitwy, w granicach sceny silnika
  z zapasem 10 m (poprawka recenzji R1), a gdy i tego brakuje - rzedy namiotow szersze (6..12 obok siebie). Limity jak w spacerze: 40 namiotow
  zolnierskich, 14 ognisk, 6 wozow, 12 koni taboru. Konie taboru uwiazane (bez wedrowania i wsiadania, niesmiertelne, na miejsce co 1.5 s), zawsze w polu
  bitwy i poza pula koni bez jezdzca (poprawka W1). Linie `CampScene (m109): ` - start sesji, "bitwa w obozie nr k", "oboz jak spacer nr k" (plan
  wedlug regul, plan spaceru tej samej druzyny, postawione, pominiete z przyczyna, przesuniete, za granica pola, rzad, glebokosc z pasem i samego pola,
  wodz, rzad 1, najblizej gracz / wojsko, front obozu za linia, konie w puli koni bez jezdzca, srodek posilkow, kierunek, encje, czas, potkniecia),
  "bez obozu (powod)"; bledy `ERROR in CampScene.<miejsce>` (pierwsze 3 i co 50 na miejsce).
- `Armoury/src/SubModuleMain.cs`: CampScene dopinana takze pod plikiem testu; `CampWalk.TestTick()` w OnApplicationTick (test spaceru).
- `Armoury/src/Settings.cs` + `McmSettings.cs` (gen_mcm): klucz "Camp Battle Like Walk" obok "Camp Battle Props Enabled" (grupa "A night's rest").
- Haki autotestu (tylko LogGate.Mode 1/2 + plik; w grze nic nie robia): `CrashScribe/armoury-oboz-bitwa.flag` - kazda bitwa w polu jak w obozie
  (NightRest bez zmian - ODSTEPSTWO od opisu zadania "ustawia obozowanie gracza": test omija `NightRest.PlayerCamped` przez `|| TestWanted()`, bo
  wyzwalacz gry jest bez zmian od 44c03a05 i dziala u Jeffa - `Armoury-2026-10-22_20-54-10.log` 21:20:01 "CampScene: postawiono 10"; w linii "nr"
  widac "gracz obozowal: TAK/NIE"), linie "bitwaK: ludzi=N konie=N wozy=N glebokosc=N" albo "bitwaK: stary" zmieniaja tylko plan i scenografie (roster
  i tabor bez zmian; brakujace konie - juczny sumpter_horse; glebokosc - nic dalej niz N m za srodkiem obozu, sciezka malej glebokosci), zdjecie obozu
  kamera nad linia (`CrashScribe/zdjecia/oboz-m109-k-*.png`; wywrotka zdjecia - kamera gry wraca, linia "NIE (wyjatek)");
  `CrashScribe/armoury-oboz-spacer.flag` - jeden spacer na proces przy pierwszej wolnej chwili na mapie po wyjsciu z osady, linia "Spacer po obozie
  (m109): test - uklad ...", wyjscie po 8 s od poczatku budowy (takze po wywrotce budowy).
- `tools/sprawdz_logi.py`: grupa `m109oboz` (OBOZ-S, OBOZ-S2, OBOZ-0..OBOZ-15; opis w --help).

Slowami gracza: oboz staje, gdy klikniesz "Rozpocznij bitwe", za twoim wojskiem, wejsciami namiotow do wroga - tyle namiotow, ognisk, poslan, wozow
i koni taboru, ile widzisz w spacerze po obozie. Gdy pole bitwy konczy sie tuz za wojskiem, tylne rzedy namiotow stoja za jego krawedzia (tam sie nie
walczy, ale je widac). Nikt w namiotach nie grzeznie, strzaly przez nie przelatuja; konie stoja uwiazane - sa twarde jak kazdy kon, ale nie da sie ich
zranic ani dosiasc (twoi spieszeni zolnierze tez po nie nie siegaja). Jeff walczy sam z towarzyszami (0 zdrowych zolnierzy) - zobaczy ognisko, poslanie
i konia taboru, bez namiotow (tak samo jak dzis w spacerze).

**Ryzyko / co sprawdzic (kontrola calosci):**
1. Spacer po refaktorze: przenos bez zmiany stalych i kolejnosci FreeSpot/Occupy/Put/Tether; obrysy w ukladzie lokalnym = dawne przesuniete o srodek
   (w spacerze krawedzie obrysow sie nie stykaja). Sprawdzic linie "wejscie" (te same liczby co przed m109 na tej samej druzynie) i OBOZ-10/OBOZ-11.
2. Stan gry tylko czytany: `NightRest.PlayerCamped` (pisza go dalej TentCore, zwiniecie, straznik, reset, ForcedMarch - bez zmian), `CampCart.Carts`,
   `MemberRoster`, `ItemRoster`, `CampWalkMenPerTent`. Scenografia i konie obozu nie wychodza z rostera - ekonomia zamknieta, bez nowego stanu i typow
   zapisu (stare zapisy sie wczytaja).
3. Agenci-konie w bitwie (najwyzej 12, `Mission.SpawnMonster` - `OnAgentBuild` u wszystkich zachowan misji; konie Native tez nie maja druzyny):
   nasze miejsca z IsMount/IsHuman sa bezpieczne (DothrakiArms, HorseFate - tylko ludzie; BloodPools, FieldCraft, BattlefieldLaw, GrandTourney - wierzchowiec
   z jezdzcem). Zabieraja 12 miejsc z limitu agentow silnika (posilki). Posilki / uciekajacy moga otrzec sie o konia (zderzenie agentow) - tylko wyglad.
   Przy scalaniu z w-toku/m109-lup: jesli zbiera zywe konie z misji - pomijac konie obozu (Character null, Team null, bez flagi Mountable).
   Poprawka W1: `Agent.Build` wpisuje konia bez jezdzca do `Mission.MountsWithoutRiders`, `Tie` (Controller None) zdejmuje mu CommonAIComponent
   (AgentCommonAILogic.OnAgentControllerChanged), a `HumanAIComponent.FindClosestMountAvailable` / `ReserveMount` (spieszony zolnierz SI z rozkazem
   "Mount") czyta `CommonAIComponent.ReservedRiderAgentIndex` konia z puli bez sprawdzenia null - wywrotka w ticku agenta. W bitwie `Tether` zdejmuje
   konia z puli (`Mission.RemoveMountWithoutRider`, klucz `Unpool`; spacer 1:1 - tam nie ma ludzi SI), linia ukladu podaje "konie obozu w puli koni bez
   jezdzca" (OBOZ-13: 0). Pule czyta w grze tylko HumanAIComponent (2 petle) i SpawningBehaviorBase (MP).
4. Koszt: do ok. 510 encji scenografii w jednej klatce przy "Rozpocznij bitwe" (OBOZ-5: <= 250 ms, <= 520 encji); przekroczenie - budowa na klatki.
   Poprawki dokladaja na bitwe: drugi `CampLayout.Count` (plan spaceru tej druzyny - przejscie rostera), do 200 krokow glebokosci i do ok. 540 odczytow
   `GetClosestBoundaryPosition` (tylko punkty za granica pola) - w tym samym pomiarze "czas".
5. Malo miejsca za linia (recenzja R1, pomiar plikow scen: od srodka wojska do granicy pola 51-89 m, gdy wojsko stoi na koncu sciezki rozstawienia; 40
   namiotow po 12 w rzedzie siega 81.5 m za ostatniego zolnierza). Poprawka: scenografia bez koni do 100 m za granica pola bitwy (`CampLayout.Inside`:
   granica miekka albo do OutsideMax od najblizszego punktu granicy, w `Scene.GetSceneLimits` pomniejszonych o 10 m; teren scen bitew siega 100-350 m za
   granice pola - `scene.xscene` battle_terrain_a/b/f/s/z/011/021/026/033/J/L, terrain node_size x node_dimension), dopiero potem rzedy 6..12. Konie
   tylko w polu (agenci). Spacer bez zmian (OutsideMax 0). Elementy za granica pola widac w linii ("poza polem bitwy"); gracz, ktory tam wejdzie, dostaje
   zwykle ostrzezenie gry o opuszczaniu pola.
6. Test spaceru w biegu bitwa: autotest czeka na mape najwyzej 60 s (StepSpawn) - spacer 8 s + wczytanie sceny; gdy nie zdazy - bitwy "nie ruszyly"
   (wtedy zdjac plik armoury-oboz-spacer.flag i puscic test spaceru osobno).
7. E7/O2: linia startu bez zmian, T0 liczy takze bledy ukladu w bitwie; przy kluczu TAK wiersz "postawiono" - INFO tylko z bitew "stary".
8. Punkty scalania z innymi m109: SubModuleMain (warunek CampScene i linia w OnApplicationTick), Settings obok CampBattlePropsEnabled, McmSettings
   (wygenerowac ponownie po scaleniu), dispatch i --help w sprawdz_logi.py (po m107gosp).

**Status:** DO SPRAWDZENIA (autotest bieg `oboz`; nie wgrane).

**Plan autotestu:** build Armoury (kod wyjscia 0), gen_mcm, sprawdz_narzedzie, `sprawdz_logi --grupa m109oboz` na dowolnym logu (OBOZ-S i OBOZ-S2 OK).
Bieg `oboz` = bieg `bitwa` (zapis autotest-F-d240, `-Days 5 -Battles 5 -BattleSec 90 -Speed 20`, flaga armoury-wioska-gracza.flag) plus pliki:
`CrashScribe/armoury-oboz-spacer.flag` (pusty) i `CrashScribe/armoury-oboz-bitwa.flag`:
```
bitwa2: ludzi=400 konie=12 wozy=3
bitwa3: ludzi=120 glebokosc=45
bitwa4: ludzi=0
bitwa5: stary
```
(bitwa 1 - BEZ nadpisan: prawdziwa druzyna, plan = spacer tej samej druzyny, OBOZ-14; 2 - limity, koszt, 12 koni uwiazanych poza pula (OBOZ-13) i wozy;
3 - mala glebokosc: rzedy po 10 (OBOZ-12); 4 - przypadek Jeffa: ognisko i poslanie; 5 - zachowanie przy NIE, 10 elementow).
Ocena: `--grupa m109oboz` na logu Armoury biegu; E7/O2 bez regresji (T1, T0, INFO "postawiono" z 1 bitwa i 10 elementami); zwykle progi biegu bitwa
(AUTOTEST BITWA BLAD 0, HorseFate, BloodPools, BattlefieldLaw bez nowych ERROR); obejrzec zdjecia `zdjecia/oboz-m109-*.png` (oboz za linia, wejscia do wroga,
nic na zolnierzach). Pliki flag USUNAC po biegu.
NIEPEWNE z recenzji - sprawdzic w biegu:
- R1: czy `Scene.GetSceneLimits` daje granice terenu (linia ukladu: "glebokosc za srodkiem obozu D m (pole bitwy F m)" - D > F, gdy pole konczy sie za
  obozem; "poza polem bitwy N") i jak wygladaja namioty za granica pola (zdjecie: teren, stoki, las). Gdy D = F przy malym F - granice sceny nie
  przepuszczaja, wtedy wrocic do planu B recenzji (rzedy obok namiotu wodza albo kierunek z wachlarza).
- R2: posilki pojawiaja sie za wojskiem (OBOZ-15: y > 0 = za srodkiem obozu) i ida przez oboz; namioty bez fizyki, przeszkoda to tylko rzad koni 12 m przed
  srodkiem (x 6..30 m, co 2.2 m - przerwy ok. 1.4 m). Zobaczyc na zdjeciu / w bitwie 2 (12 koni), czy jest przepychanie; jesli tak - osobna zmiana (konie
  w bitwie za wozami albo z boku), bo dzis rzad koni jest jak w spacerze.
- Kamera zdjecia nad linia (test) - czy ShotRestore po wyjatku oddaje kamere (linia "NIE (wyjatek)" i dalsza bitwa z kamera gracza).

**Recenzja (23.10) - werdykty i poprawki:**
- W1 PRAWDA (wywrotka warunkowa: rozkaz "Mount" i kon obozu w puli) - poprawione: `Tether` w bitwie `RemoveMountWithoutRider`, OBOZ-13.
- R1 PRAWDA (mechanizm w kodzie: srodek 21 m za ostatnim, rzedy do 81.5 m, `FreeSpot` tylko granica miekka) - poprawione: scenografia do 100 m za granica
  pola (`Inside`), test `glebokosc=N`, OBOZ-12, linia z glebokoscia pola i "poza polem bitwy"; wyglad za granica - NIEPEWNE (wyzej).
- R2 PRAWDA (posilki rodza sie na poczatku sciezki - za wojskiem; `DefaultDeploymentPlan` przy IsReinforcement) - tylko wyglad (konie stoja z przerwami,
  namioty bez fizyki): pomiar srodka posilkow w linii (OBOZ-15 INFO), ocena w biegu - NIEPEWNE (wyzej); uklad koni bez zmian (= spacer).
- R3 / D4 PRAWDA (Shifts[7] namiotu oficerskiego: +5 m wzdluz osi, obrys do 20.5, rzad 1 = 28.5) - poprawione: "przesuniete" w linii bitwy, OBOZ-11 przy
  przesunieciach 27.5..28.5.
- R4 / D3 PRAWDA (Announce dopiero po IsFieldBattle - "nie bitwa w polu" nie pada nigdy) - poprawione: OnEndMission rozroznia "koniec misji przed
  rozstawieniem" i "koniec misji bez gracza na polu"; OBOZ-2 przyjmuje obie (wypisuje je), martwy warunek usuniety.
- R5 / D6 PRAWDA (spacer testu idzie przed dolozeniem zolnierzy przez StepSpawn, kazda bitwa miala nadpisania) - poprawione: linia ukladu podaje plan
  spaceru tej samej druzyny (`CampLayout.Count` bez nadpisan, ta sama funkcja co spacer), OBOZ-14, bitwa 1 w planie bez nadpisan.
- R6 PRAWDA (test omija `NightRest.PlayerCamped`) - odstepstwo opisane wyzej (Haki autotestu), w linii "nr" "gracz obozowal: TAK/NIE"; obozowania nie
  ustawiam w tescie (stan NightRest czytaja ForcedMarch, NightMarch, namiot na mapie - test zmienialby gre poza obozem w bitwie).
- D1 PRAWDA (zegar wyjscia startowal na koncu Build) - poprawione: zegar zaraz po odczycie `_test`.
- D2 PRAWDA (catch bez ShotRestore) - poprawione: osobny try zdjecia, ShotRestore i linia "NIE (wyjatek)".
- D5 PRAWDA (konie to agenci z kapsula) - poprawiony opis MCM ("the tied horses are solid but cannot be hurt or mounted", namioty moga stac za krawedzia
  pola), gen_mcm.
- NIEPRAWDA: brak.

**Do decyzji Jeffa (nie robione):** czy towarzysze maja sie liczyc jako ludzie obozu (dzis i w spacerze, i w bitwie nie - stad jedno poslanie);
zagroda jencow (pominieta - nie bylo decyzji).
