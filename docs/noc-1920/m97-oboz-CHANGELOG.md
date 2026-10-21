## m97-oboz - oboz 21-3, spacer po obozie, woz taborowy i siodla transportowe (Armoury)

**Dla Jeffa (prosto):** swiat rozbija oboz o 21:00 i rusza o 3:00, wiec cala noc gry (22:00-2:00) kolumny stoja i nikt nie jedzie z nocna kara.
Cala doba snu przesuwa sie razem z obozem o 3 godziny: sen trwa jak dotad 6 godzin, a "noc snu" (pelna stawka odpoczynku) to teraz 18:00-3:00 zamiast
21:00-6:00, wiec dlug snu splaca sie tak samo szybko jak dotad; nocny wzrok liczy sie z ta sama noca co gra. W Twoim obozie (klawisz O) jest nowa opcja
**Walk through the camp**: scena w miejscu postoju z namiotami wedlug liczby ludzi (w armii - calej armii), ogniskami z siedzacymi zolnierzami i
uwiazanymi konmi; wychodzisz TYLKO przytrzymujac Tab (Esc nie wychodzi - tam jest tylko "Exit to Main Menu", nie klikaj). W miescie, w "Go to the smithy":
**Visit the cartwright and saddler** - woz (zwierze juczne w zaprzegu wiezie 500 kg zamiast 100, kolumna 10% wolniej, takze piechota) i siodlo
transportowe (+50 kg na wolnym jucznym). **Przy wgraniu trzeba w Twoich ustawieniach zmienic CampStartHour 0 -> 21 i CampEndHour 6 -> 3** (inaczej oboz zostanie 0-6).

**Problem:** Jeff 20.10 (`docs/ZADANIA-OBOZ-NOC.md`, lista 1, 2, 4, 5, 6 i DECYZJE JEFFA PO RAPORCIE 2, 3, 6): "oboz zaklada chyba za pozno, powinnismy
przeniesc na 22:00 [...] aby nie jechac z modyfikatorem noc"; "wejscie do obozu i chodzenie po nim, im wieksza armia, tym wiekszy oboz"; "sam gracz = poslanie
i kon"; "woz do kupienia + kon juczny do powozenia, daje tyle ile w wozie kg mozna przewiezc, juki nie, juki to siodlo [...] siodla bojowe i siodla
transportowe". Rozpoznanie `docs/ROZPOZNANIE-OBOZ-2026-10-20.md` rozdz. 1, 2, 4, 5, 6.

**Dowod (stan przed zmiana, z kodu i danych):**
- Noc gry 22:00-1:59 (`CampaignTime.IsDayTime`: `floor(godzina) >= SunRise(2) && < SunSet(22)`; ROT te same 2 i 22). Oboz swiata 0-6
  (`Settings.CampStartHour = 0`, `CampEndHour = 6`; u Jeffa w `Armoury.json` tez 0 i 6 - odczytane 20.10 17:5x). Tick godzinowy w minucie losowanej przy
  wczytaniu (NightClock), wiec "start o 21" = kolumny staja miedzy 21:00 a 22:00, "koniec o 3" = ruszaja miedzy 3:00 a 4:00.
- `NightMarch.cs:61` `DebtCampHour = 20` na sztywno: oboz splaty dlugu 1 = 10 h do konca obozu przy 0-6, przy 21-3 dalby 7 h (potrzeba 9).
- `SightRange.cs:35` noc wzroku 21:00-4:59 (inna niz noc gry). Sen w menu i w karczmie: noc 21-5 na sztywno (`NightRest.cs` SleepInit/SleepTick,
  `TownInns.PredictWake`), przy swicie 3 liczylby 3:00-5:59 jako noc po swicie.
- Wozu jako przedmiotu nie ma w zadnym module (rozpoznanie 6, grep ModuleData); model wozu tylko w prefabach scen (Native `bd_cart_a/b/c`).
  Siodla bojowe sa: ROT 16 siodel (`ROT-Content/ModuleData/ROTassets.xml`, np. rot_horse_saddle1..4, braavosi_saddle), RBM i SandBoxCore ok. 10 siodel
  i pancerze konskie; SaddleLaw (klucz RiderNeedsHarness) wymaga uprzezy od jezdzca ("any horse harness will do"). Siodla transportowe tez sa jako przedmioty:
  `mule_load_a/b/c` ("Mule Harness with Pack / Baskets / Bags", waga 120, is_merchandise=false, ArmorComponent family_type 1) i `camel_saddle_a/b`
  - nikt ich nie robi i nic nie daja; SaddleLaw.ItemKind uznalby mule_load za siodlo jezdzca (rodzina 1).
- Gra: zwierze juczne 100 kg (`DefaultInventoryCapacityModel`: PackAnimals x 10 x 10), luzak 20 kg (u nas 60 - PackMarket).
- Wzor sceny: Practice Drills 1.3 (public domain, LICENSE w `scratchpad/zb/practicedrills-1.3`) - `Drill.Open` (misja "CustomBattle" na scenie
  `GetBattleSceneForMapPatch`, rekord jak nizej), `CampVisitLogic.Lie/HoldPose` (zestaw ruchow `as_human_hideout_bandit`, pozy `act_dungeon_prisoner_*`,
  Controller None, pozy odnawiane). SandBox ma nieuzywany typ misji "Camp" z widokami (`SandBoxMissionViews [ViewMethod("Camp")]`, w tym MissionCampaignView,
  menu Esc, pasek zdrowia; MissionMainAgentController to [DefaultView] - dochodzi sam).

**Zmiana (Armoury):**
1. **Oboz 21-3** (commit d86e2d54):
   - `Settings.cs`: `CampStartHour` 0 -> **21**, `CampEndHour` 6 -> **3**, opisy MCM ("21 and 3 = camp over the whole game night (22:00-2:00)"); opis
     `NightSightFactor` "(the game's night, 22:00-2:00)". `McmSettings.cs` z `gen_mcm.py`.
   - `NightRest.cs`: zapasowe wartosci `CampStart/CampEnd` (bez ustawien) 21 i 3. **Noc snu zakotwiczona o swicie (poprawka recenzji, commit c8ff9370)**:
     `LedgerNight(h)` = tick h w [swit-9, swit] (10 tikow ksiegi, przez polnoc) - ksiega gracza (OnHourly) i ksiega AI (`NightMarch.AiSleepLedger`);
     `MenuNight(h)` = godzina w [swit-9, swit) (9 h zegara) - sen w menu (SleepInit, SleepTick) i przewidywanie pobudki w karczmie (`TownInns.PredictWake`).
     Swit 6 (oboz 0-6 albo rowne godziny) -> ksiega 21..6 i menu 21:00-5:59 - **dokladnie jak dotad**; swit 3 -> ksiega 18..3 i menu 18:00-2:59. Pierwsza wersja
     (commit d86e2d54: "od 21:00 do switu") skracala pelna stawke przy swicie 3 z 10 tikow do 7 - patrz "Poprawki po recenzjach" nizej.
   - `NightMarch.cs`: `DebtCampHour` liczony **od konca obozu**: `CampEnd - max(10, dlugosc obozu)` (0-6 -> 20:00 jak dotad; 21-3 -> 17:00). Lord polozony o 17:00
     zbiera ticki 18..3 = 10 h pelnej stawki (z noca zakotwiczona o swicie; NeededAi(6,1) = 9 - 1 h zapasu jak dotad).
   - `SightRange.cs`: noc wzroku = `CampaignTime.Now.IsNightTime` (noc gry 22-2, pola statyczne SunRise/SunSet - tanie).
2. **Spacer po obozie** (commity 0ff640ba, 01b47ebf, 6ec93447, eaee474d; poprawki recenzji 8c8223e5) - NOWY `CampWalk.cs`:
   - Opcja **Walk through the camp** tylko w naszym `arm_camp_wait` (zwykle menu, czas stoi; NIE w "camp" gry - to menu spotkania
     `DefaultEncounterGameMenuModel`, i nie w menu oczekiwania BK). Warunki: brak bitwy/oblezenia/spotkania, poza osada, na ladzie, brak innej misji.
   - Misja `MissionState.OpenNew("Camp", ...)` na scenie bitwy z miejsca postoju (rekord jak Practice Drills: teren, pogoda i pora z mapy, latka mapy).
     Zachowania: MissionOptionsComponent, CampaignMissionComponent, BasicLeaveMissionLogic, granice (Hard/Boundary/Crossing 10 s), EquipmentControllerLeaveLogic,
     nasza `CampWalkLogic`, `ViewCreator.CreateMissionLeaveView()` (Tab). **Wyjscie tylko Tab** - Esc NIE wychodzi (menu Esc misji: Return/Options/Photo Mode/
     Exit to Main Menu - to ostatnie porzuca kampanie bez zapisu); komunikat wejscia: "Hold Tab to go back (Esc does not leave the camp)". Bez MapEvent, bez logiki bitwy, jedna druzyna. `DoesMissionRequireCivilianEquipment = true`
     w delegacie (przed OnMissionBehaviorInitialize modulow) - **DTE nie dopina swojej logiki zbrojowni** (sprawdzone: DTE SubModule.OnMissionBehaviorInitialize
     pomija misje cywilne), ludzie w stroju cywilnym.
   - `CampWalkLogic.Build` (raz, AfterStart): **kolumna** = druzyna gracza, a w armii (gracz wodzem albo doczepiony do wodza) wodz + wszystkie doczepione partie
     (jeden oboz armii; gracz dopiero idacy do armii - sama druzyna). Srodek = srodek granic sceny (walk area) albo srodek sceny; gracz 6 m przed srodkiem; ognisko gracza,
     jego poslanie i jego kon (BattleEquipment); do CampWalkMenPerTent-1 ludzi - poslania wokol ogniska (noca ludzie spia NA nich, jeden czuwa przy ognisku; za dnia po 4
     przy ognisku, reszta lezy na poslaniach); od CampWalkMenPerTent (6) ludzi: namiot wodza `Tent_Leader` 12 m za srodkiem, od 60 ludzi `Tent_Officer` 16 m w bok, namioty zolnierskie
     `Tent_Small` po jednym na 6 ludzi (najwyzej 40), rzedami po 6 symetrycznie po obu stronach osi, co 11 m, **zaczynajac za obrysem namiotu wodza** (najdalszy zajety punkt + 8 m),
     ognisko co trzy namioty (najwyzej 14); w armii chorazy z herbem rodu **wodza armii** (`campaign_banner_small`) przed namiotem wodza; wozy gracza (`bd_cart_a..c`, najwyzej 6)
     z przodu obozu po lewej; konie na postoju z przodu po prawej: konie i juczne z taboru gracza (najwyzej 12, bez bydla), za nimi konie jezdzcow kolumny (1 na 8 jezdzcow,
     co najmniej 1, najwyzej 10; kon i siodlo z FirstBattleEquipment jezdzca - wierzchowce nie leza w taborze). **Konie uwiazane** jak w Practice Drills (Tether/Hold/HoldAnimals):
     zdjete flagi CanWander i Mountable, Controller None, SetMaximumSpeedLimit(0), niesmiertelne, co 1.5 s cofane na miejsce, gdy odejda dalej niz 1.5 m.
   - **Obrysy prefabow** (`Footprints`, z pozycji dzieci prefabow `ROT-Map/Prefabs/Swan_Siege.xml` i `Fantasy.xml`): Tent_Leader x -3.8..11.8, y -7.5..4.2 (ognisko
     `burning_campfire_big` 10.4 m z boku, stragan, beczki i drewno do 6.3 m z tylu), Tent_Small 5.2 x 5.2 m, Tent_Officer 6.6 x 7 m, Tent_Officer_Hangout do 9.5 m; obrot jak
     `Mat3.RotateAboutUp` (sprawdzone w dekompilacji). Kazdy namiot, ognisko, woz, kon, czlowiek i gracz zajmuje swoj obrys; nastepny element wchodzacy w zajety obrys jest
     przesuwany (te same przesuniecia co przy drzewach) albo pominiety; poslania sa plaskie (nie zajmuja). Ludzie lezacy "przy namiotach" - w przerwach miedzy namiotami
     (4 m w bok od namiotu, po 2), nie na stalej siatce (dawna siatka 14 m za srodkiem wchodzila w rekwizyty Tent_Leader). Uklad sprawdzony na sucho (python, bez sceny):
     6 / 60 / 300 ludzi - 0 nakladan, 0 przesuniec, rzedy od 27.5 m. Brak prefabu ROT - nastepny z listy (namioty Native), sprawdzane raz na proces `GameEntity.PrefabExists`.
     Miejsce kazdego elementu, gracza i siedzacych sprawdzane jak w PD (same odczyty sceny): drzewo/skala (najwyzsze cialo > 0.8 m nad terenem,
     maska cial z PD) albo woda w punkcie - przesuniecie do 8 m (9 prob), inaczej element pominiety; probki 7 x 7 co 10 m przed budowa - odczyt wody albo
     cial "zalewajacy" ponad 90% sceny jest dla tej sceny pomijany (PD robi to samo dla wody). Linia `Spacer po obozie (m97): probki terenu N | woda | drzewa/skaly`.
   - Ludzie: zolnierze druzyny gracza (bez bohaterow), najwyzej **CampWalkMaxMen (40)**, `SimpleAgentOrigin` (bez powiazania z rosterem - zadne zdarzenie misji
     nie zabierze nikogo z druzyny), Controller None, bez broni; po 4 przy ognisku (siedza, w godzinach obozu albo noca polowa spi), reszta limitu lezy przy
     namiotach. Pozy co 1.5 s (tylko postawieni ludzie). Scenografia **bez fizyki** (jak CampScene - sprawdzony sposob).
   - Linie logu: `Spacer po obozie (m97): opcja ...` (start sesji), `... otwieranie | scena ...`, `... wejscie | ... [armia (kolumna N partii)] | ludzi zdrowych N |
     namioty N (zolnierskich wedlug ludzi N, postawionych N), poslania, ogniska, wozy a/b | agenci ludzi N z limitu 40 (spiacych) | konie N (jezdzcow N z M jezdzcow,
     uwiazane N) | elementy N (przesuniete ..., pominiete ..., nakladanie obrysow N) | gracz tak`, `... wyjscie po N s | potkniecia razem`. Brak prefabu: `... brak prefabu X`.
3. **Woz taborowy i siodla transportowe** (commit c81c3ea3; poprawki recenzji 93f08f66) - NOWY `CampCart.cs`:
   - Woz = **stan druzyny gracza** (liczba wozow, zapis `arm_campcart` "1|wozy" przez `SaveText.Sync`, osobny try; brak klucza = 0), NIE nowy przedmiot XML
     (bez nowej siatki i typu w zapisie - nie da sie sprawdzic w grze tej nocy).
   - Miasto: opcja **Visit the cartwright and saddler** (menu `arm_cartwright`; przy TownMenuTidy w podmenu **Go to the smithy** - wpis planu `TownMenus.Plan`, poprawka recenzji): woz robi stolarz (warsztat `wood_WorkshopType`) z **6 drewna + 1 narzedzi** z polki
     miasta (ShelfBuy - kazda sztuka po swojej cenie, zloto do kasy miasta) + robocizna **120** wlascicielowi warsztatu (wlasny warsztat - bez robocizny, bez wlasciciela -
     kasa miasta). Siodlo transportowe robi garbarz (`tannery`): **2 skory + 1 drewno** + robocizna **40** -> prawdziwy przedmiot gry `mule_load_a` do taboru.
     "Break up a cart for its wood" - woz mniej, 3 drewna do taboru. Niewola gracza: wozy biora zwyciezcy (3 drewna na woz do taboru ich partii). AI wozow nie kupuje.
   - Udzwig: postfiks `DefaultInventoryCapacityModel.CalculateInventoryCapacity` (`AccessTools.DeclaredMethod`, jak PackMarket; Naval wola BaseModel): na kazdy woz
     jedno zwierze juczne gracza w zaprzegu = **CartLoadKg (500) - 100** kg wiecej (wpis "Supply carts"); wolne zwierze juczne z siodlem transportowym w taborze partii
     rodu gracza = **PackSaddleExtraKg (50)** + waga siodla (siodlo lezy na zwierzeciu; wpis "Pack saddles"). Armia (includeFollowers) - takze doczepione partie.
   - Predkosc (**poprawka recenzji**): bez wlasnej latki - `CampCart.Speed` wolane w latce kolumny `MarchPace.SpeedPostfix` PO suficie kolumny, drogach i blocie (jak
     `MapMud`: procent wyniku przez Add, wynik schodzi ponizej sufitu; gdy inny model trzyma wynik samym LimitMax - sufit obnizony do celu; lancuch modeli raz - `SpeedDepth`).
     Druzyna gracza z wozem w zaprzegu i wodz armii, do ktorej gracz jest doczepiony: **-CartSpeedPenaltyPercent (10)%** (wpis "Supply cart"), takze kolumna piechoty
     na suficie 4.0 (3.6); na morzu i w przejsciu lad-morze bez kary; woz bez zwierzecia - bez udzwigu i bez kary. `MarchPace.ApplyAll` wpina latke takze przy samym
     kluczu CampCartEnabled. Linia startu: `... latka kolumny w N modelach predkosci` (0 = `KARA WOZU NIECZYNNA`).
   - Nieudany zakup (drugi material za drogi): zwrot na polke `ShelfBuy.PutBack(..., tally: false)` - bez ruszania licznikow linii "Ceny hurtu" (nowy parametr
     z domyslna wartoscia true - pozostale 4 miejsca wywolan bez zmian).
   - `SaddleLaw.ItemKind`: siodlo transportowe = "nie pasuje" (nie robi jezdzca) - siodla bojowe i transportowe rozdzielone.
   - Linie logu: `Woz obozowy (m97): wpiecia: udzwig, predkosc N modeli`, `... start sesji | woz TAK ... | siodla transportowe TAK ... | wozy gracza N | materialy ...
     | menu miasta tak`, zakup/rozbicie/niewola, doba `Woz obozowy (m97): dzien N | wozy gracza ... (w zaprzegu ... z ... jucznych, +... kg, wolniej o ...%) | siodla ...`
     (tylko gdy jest woz albo cos sie stalo).
- Klucze NOWE (domyslnie TAK, grupa MCM "Camp and baggage (night 20/21.10)"): **CampWalkEnabled**, **CampWalkMaxMen** (40, suwak 0..100), **CampWalkMenPerTent** (6, 2..20),
  **CampCartEnabled**, **CartLoadKg** (500, 100..2000), **CartSpeedPenaltyPercent** (10, 0..50), **PackSaddleEnabled**, **PackSaddleExtraKg** (50, 0..300).
- Wpiecia: `ArmouryBehavior.cs` dwie linie RegisterEvents (CampWalk, CampCart) + blok SyncData `arm_campcart`; `SubModuleMain.cs` jedna linia `CampCart.ApplyAll`;
  `SaddleLaw.cs` jedna linia; `MarchPace.cs` jedna linia wywolania + warunek wpiecia; `ShelfBuy.cs` parametr `tally` w PutBack; `TownMenus.cs` jeden wpis planu. Zasoby Practice Drills (dzwieki .wav, tarp_canvas.jpg) NIE dolaczone - dzwiek wymaga wlasnego odtwarzacza (PD ma AmbientMixer),
  a tekstura nie jest potrzebna przy prefabach ROT; mozna w etapie 2.

**Uzasadnienie liczb (decyduje Claude, zasada Jeffa):** woz jednokonny w sredniowieczu wiozl ok. 400-600 kg (kon juczny ok. 100 kg) - 500 kg; woz
jedzie stepa wolniej niz maszerujaca piechota (postoje, blotniste koleiny) - 10% wolniej od tempa kolumny po jej suficie. **Jeden woz czy wiele - ta sama kara**
(decyzja projektu, recenzja gry "drobne"): kolumna idzie tempem wozow, a kazdy woz potrzebuje wlasnego zwierzecia jucznego, wiec duzy tabor juz zwalnia kolumne
konna regula taboru MarchPace (juczne ponad 1 na 4 ludzi = sufit MarchTrainPace 4.2); druga kara od liczby wozow liczylaby tabor dwa razy. Siodlo transportowe (lepszy rozklad ladunku) +50%
udzwigu jucznego - 50 kg. Materialy: rama i kola z drewna, okucia z narzedzi; siodlo z dwoch skor na drewnianym terlicy; robocizna ok. 2-3 dni rzemieslnika.

**Ryzyko / co sprawdzic (kontrola calosci):**
- **Oboz 21-3 dotyczy calego swiata** - wszystkie moduly pytaja o ten sam przelacznik (InCamp/CampEnd/PlayerDawn): sen gracza i "Night falls - the column makes camp"
  o 21, ksiega snu AI, marsz nocny, dlug snu, dzien odpoczynku armii (`ArmyRestDay` - swit = PlayerDawn = 3), karczmy (pobudka nie przed switem = 3:00), Wayfinder
  (18 h marszu na dobe jak dotad; ciemnosc 22-2 cala w obozie), WorldMeasure. Noc przez polnoc obsluzona (InCamp, CampHolds z `InCamp(h-1)` przez Mod24).
  Swit 3:00 zamiast 6:00 przesuwa pobudke, karczme i dobe armii o 3 h wczesniej. Stawki snu (po poprawce recenzji): noc snu = 10 tikow konczacych sie o swicie
  (ksiega 18..3, menu 18:00-2:59) - przesuniecie calej doby snu o 3 h, wzgledny koszt dlugu jak dotad (spiacy w obozie 21-3 ma pelne 6 h z tikow 22..3; dlug 1
  splaca oboz od 17:00 - 10 h; gracz z dlugiem 1 kladacy sie o 18:00 budzi sie jak dawniej o 21:00 -> 3:00 zamiast 6:00). Odpoczynek za dnia 18:00-21:00
  w osadzie liczy sie teraz pelna stawka (wczesniej 0.6) - to te same 3 h przesuniecia. Przy obozie 0-6 (Armoury.json Jeffa przed zmiana) wszystko dokladnie jak dotad.
  Zmieniac godziny w dzien (4:00-17:00), nie w srodku nocy obozu.
- **Noc wzroku (SightRange) = noc gry 22-2 - zmiana stala, bez osobnego klucza** (recenzja gry "drobne": decyzja). Jedna noc dla kary predkosci gry, wzroku
  i obozu (rozpoznanie 1). Cofniecie godzin obozu do 0/6 cofa oboz, splate dlugu i noc snu, ale nie noc wzroku (21-4:59 -> 22-1:59: wzrok nocny o 1 h krocej
  wieczorem i 3 h krocej nad ranem). Wylacznik calosci jak dotad: SightCycleEnabled.
- Warsztat, kuznia i samonaprawa (`WorkshopNightRest` 23-5) - inna regula (noc warsztatu), bez zmian. `HideoutAlarm` (noc 21-5, slyszalnosc w kryjowce) - bez zmian.
- **Spacer = pierwsza wlasna misja w naszych modach, NIEPRZETESTOWANA w grze; autotest jej NIE obejmuje** (recenzja techniki "wazne": prawda - nie da sie
  naprawic w tym drzewie, sterownik autotestu jest w DLL autotestu CrashScribe; plan etapu ponizej w "Co zostalo") (zakaz uruchamiania gry tej nocy). Nie sprawdzone: czy widoki "Camp" dzialaja
  bez spotkania (PD otwiera "CustomBattle"; my "Camp" jak SandBox OpenCampMission bez MapEvent), powrot do `arm_camp_wait` po Tab (PD wraca do swojego menu tak samo),
  zachowanie BK/ROT na `OnMissionStarted` z `CampaignMissionComponent` (PD ma to samo w trybie odwiedzin), polozenie namiotow ROT (skala, obrot; drzewa, skaly i woda sprawdzane odczytem
  sceny jak w PD, bez gwarancji przy gestym lesie; gracz przechodzi przez namioty - bez fizyki), czy zestaw `as_human_hideout_bandit` i pozy PD sa w ROT. Kazdy element w try z licznikiem
  potkniec - wywrotka jednego elementu = bez niego. Wydajnosc: tylko na zadanie gracza; najwyzej 40 ludzi + 12 koni + ok. 60 elementow.
- Nasze zachowania misji dopinane do kazdej misji (FieldCraft, HitScribe, GuardMaster, BrokenMen, RoutKeeper, BattleSense, HorseFateMission, CampScene): dzialaja
  tez w misjach wsi/miast; HorseFateMission liczy tylko jezdzcow z PartyBase (nasi maja SimpleAgentOrigin, bez koni) - zero wpisow. CampScene: `IsFieldBattle` false - nic.
- **Woz**: udzwig - jedno miejsce z PackMarket (ten sam postfiks, inny skladnik), luzak pod jukami bez zmian; zwierze w zaprzegu traci swoje 100 kg na rzecz 500 (bez
  podwojnego liczenia). Predkosc: po poprawce kara po suficie kolumny - dziala tez przy kolumnie piechoty (pierwsza wersja ginela pod LimitMax MarchPace).
  Zamknieta ekonomia: material z polki (cena z popytu, zloto do kasy miasta), robocizna do wlasciciela warsztatu, woz rozbity -> drewno, niewola -> drewno do zwyciezcy;
  nic z niczego. Linia "Ceny hurtu" nie liczy tych zakupow (tally false). Siodlo `mule_load_a` w taborze: sprzedawalne jak kazdy przedmiot; DTE moze je widziec
  w taborze jako uprzaz (nie sprawdzone, czy DTE bierze uprzaz z taboru gracza) - SaddleLaw juz go nie uzyje do jezdzca. Siodlo bez wolnego jucznego = 120 kg
  bagazu (tekst menu mowi to wprost).
- Menu miasta: przy TownMenuTidy opcja w podmenu "Go to the smithy" (ten sam obiekt opcji, warunki bez zmian); "Leave" z menu stolarza wraca do menu miasta
  (`SwitchToMenu("town")`). Przy wylaczonym TownMenuTidy - w menu miasta jak w pierwszej wersji.
- Zapis: nowy klucz `arm_campcart`, stary zapis bez klucza = 0 wozow; stara DLL ignoruje klucz.

**Poprawki po recenzjach (gra, technika) - 20/21.10:**
- KRYTYCZNE (gra) "oboz splaty dlugu 1 od 17:00 daje 8.8 h z 9" - **PRAWDA, poprawione** (c8ff9370): noc snu zakotwiczona o swicie (LedgerNight/MenuNight),
  wpis w dzienniku "17:00, 10 h pelnej stawki" teraz prawdziwy. Sprawdzone na sucho: ticki 18..3 przy swicie 3 = 10.0 h; przy swicie 6 zbiory godzin identyczne
  z dawnymi (21..6 i 21-5).
- WAZNE (gra) "gracz splaca dlug 2-5 h dluzej" - **PRAWDA, poprawione** tym samym (ksiega gracza, sen w menu, karczma z jednej funkcji).
- WAZNE (technika) "konie nie uwiazane" - **PRAWDA, poprawione** (8c8223e5): Tether jak PD + zdjete CanWander i Mountable + pilnowanie miejsca.
- WAZNE (technika) "uklad wchodzi w rekwizyty Tent_Leader" - **PRAWDA, poprawione**: obrysy prefabow z XML ROT, rzedy za obrysem wodza, lezacy w przerwach miedzy namiotami.
- WAZNE (technika) "w armii oboz liczy tylko druzyne gracza" - **PRAWDA, poprawione**: kolumna armii (wodz + doczepione), herb wodza armii.
- WAZNE (technika) "kara wozu ginie pod sufitem MarchPace" - **PRAWDA, poprawione** (93f08f66): kara po suficie w latce kolumny.
- WAZNE (technika) "brak testu wejscia w scene" - **PRAWDA, NIE do naprawienia w tym drzewie** (sterownik autotestu poza drzewem) - status czesciowo, plan nizej.
- DROBNE: konie jezdzcow (gra) - zrobione (1 na 8 jezdzcow, najwyzej 10); Esc (gra) - komentarz, wpis i komunikat poprawione; PutBack (gra) - poprawione
  (tally); kara stala od liczby wozow (gra) - decyzja, uzasadnienie wyzej, opis MCM i menu mowi to wprost; siodlo jako martwy ciezar (gra) - tekst menu i opis MCM;
  noc wzroku bez klucza (gra) - decyzja, opisane w Ryzyku; opcja w glownym menu miasta (technika) - przeniesiona do "Go to the smithy"; drugi licznik glebokosci
  (technika) - usuniety (kara w latce kolumny, SpeedDepth); polska litera w komentarzu (technika) - usunieta; uklad malego oddzialu (technika) - ludzie na poslaniach.

**Status:** DO SPRAWDZENIA (build Armoury kod 0 po kazdym kroku; gra nieuruchomiona; nic nie wgrane; Armoury.json Jeffa nie ruszany).

**Przy wgraniu (Armoury.json Jeffa, gra zamknieta, kopia .bak):** `"CampStartHour": 0` -> `21`, `"CampEndHour": 6` -> `3`. Nowe klucze (CampWalk*, CampCart*,
CartLoadKg, CartSpeedPenaltyPercent, PackSaddle*) w pliku nie istnieja - MCM wezmie domyslne z kodu (TAK). Zmieniac w dzien gry (4:00-20:00).

**Test w grze krok po kroku:**
1. Start sesji - w `Armoury.log`: `NightRest: oboz swiata 21:00-3:00 ... swit gracza 3:00 ... oboz splaty dlugu 1 od 17:00, noc snu (pelna stawka) 18:00-3:00`;
   `Woz obozowy (m97): wpiecia: udzwig, predkosc w latce kolumny MarchPace (po suficie)`; `Woz obozowy (m97): start sesji | woz TAK ... materialy drewno tak, narzedzia tak,
   skora tak, siodlo do robienia tak | menu miasta tak | wpiecia ..., latka kolumny w N modelach predkosci` (N >= 1, bez "KARA WOZU NIECZYNNA");
   `Spacer po obozie (m97): opcja "Walk through the camp" w menu obozu (arm_camp_wait) | klucz Camp Walk Enabled TAK`.
2. Mapa ok. 20:30-21:59: pierwszy tick po 21:00 - pytanie "Night falls - the column makes camp." i kolumny AI staja; o 22:00-2:00 w podpowiedzi predkosci
   nikt nie jedzie (kara "night" tylko u marszu forsownego). Wzrok: podpowiedz zasiegu o 21:30 - "Daylight", o 22:30 - "Darkness". Swit: kolumny ruszaja 3:00-4:00.
   Po 2-3 dobach w linii `NocnyMarsz: swit` licznik "dlug 1 z poprzedniego switu ... dalej 1" nie rosnie z doby na dobe (lordowie splacaja dlug 1 w obozie od 17:00).
3. Klawisz O na mapie -> menu obozu -> **Walk through the camp** -> ekran ladowania -> scena: gracz przy ognisku, poslanie, jego kon; przy 20 ludziach 4 namioty,
   namiot wodza, ogniska z siedzacymi; w nocy czesc spi; konie stoja w rzedzie i po 30 s sa dalej na miejscu (nie da sie na nie wsiasc); zaden namiot ani czlowiek
   nie stoi w rekwizytach namiotu wodza (beczki, stragan, ognisko z boku). W logu linia `Spacer po obozie (m97): wejscie | ...` z "nakladanie obrysow 0" albo malym.
   Przytrzymac Tab -> wraca menu obozu, godzina na mapie ta sama co przed wejsciem; w logu `... wyjscie po N s`. NIE uzywac Esc -> Exit to Main Menu.
   Sprawdzic wejscie w dzien i w nocy, sam (bez ludzi), z 3 ludzmi (noca na poslaniach) i w armii (namioty wedlug calej armii, "armia (kolumna N partii)", chorazy z herbem wodza).
4. Miasto ze stolarzem (Wood Workshop) -> **Go to the smithy** -> **Visit the cartwright and saddler** -> "Have a supply cart built" -> komunikat o cenie; w ekwipunku/druzynie:
   podpowiedz udzwigu "Supply carts +400" (przy co najmniej jednym zwierzeciu jucznym), podpowiedz predkosci "Supply cart" ok. -10% wyniku TAKZE przy piechocie
   (wynik 4.0 -> ok. 3.6 pod "Marching column: N afoot"). Bez jucznego - woz bez udzwigu i bez kary.
5. Miasto z garbarnia -> "Have a pack saddle made" -> w taborze "Mule Harness with Pack"; podpowiedz udzwigu "Pack saddles +170" (50 + 120 waga siodla) na wolnym jucznym.
   Awans piechura na jezdzca nie bierze tego siodla (SaddleLaw).
6. Zapis i wczytanie - liczba wozow zostaje (`Woz obozowy (m97): start sesji | ... wozy gracza N`).
Linie do grep: `NightRest: oboz swiata`, `Woz obozowy (m97)`, `Spacer po obozie (m97)`, `CampWalk.`, `CampCart.` (bledy w Log.Error).

**Co zostalo (etap 1 - czesciowo):** spacer nieprzetestowany w grze (pierwsza wlasna misja - autotest z wejsciem do sceny przed wgraniem, `bieg-bitwa.ps1`
nie obejmuje tej misji; potrzebny etap w DLL autotestu CrashScribe, poza tym drzewem: na mapie, poza osada, otworzyc `arm_camp_wait`, wybrac opcje
`arm_camp_walk_arm_camp_wait`, odczekac ok. 20 s w misji, zakonczyc ja (`Mission.Current.EndMission()`), sprawdzic linie `Spacer po obozie (m97): wejscie` i `wyjscie`,
brak `CampWalk.` w Log.Error i powrot do `arm_camp_wait`; powtorzyc w nocy i w armii); srodek obozu staly (srodek granic sceny - bez szukania najrowniejszego miejsca jak PD); bez fizyki namiotow; ludzie tylko siedza/spia (chodzenie, jedzenie, rozmowy,
cwiczenia - etap 2); bez dzwiekow obozu (zasoby PD - etap 2); woz nie jest przedmiotem ekwipunku (nie widac go w ekranie ekwipunku, tylko w menu i w scenie);
markietani i miasteczko obozowe - etap 2 (decyzja zadania).
