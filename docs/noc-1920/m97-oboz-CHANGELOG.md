## m97-oboz - oboz 21-3, spacer po obozie, woz taborowy i siodla transportowe (Armoury)

**Dla Jeffa (prosto):** swiat rozbija oboz o 21:00 i rusza o 3:00, wiec cala noc gry (22:00-2:00) kolumny stoja i nikt nie jedzie z nocna kara;
sen trwa jak dotad 6 godzin, a nocny wzrok liczy sie teraz z ta sama noca co gra. W Twoim obozie (klawisz O) jest nowa opcja **Walk through the camp**:
wchodzisz na scene w miejscu postoju, gdzie stoja namioty wedlug liczby Twoich ludzi (sam = poslanie i kon, potem namiot na 6 ludzi, namiot wodza,
od 60 ludzi namiot oficerski), przy ogniskach siedza Twoi zolnierze, obok stoja konie i wozy; Tab - powrot do menu obozu, czas stoi. W miescie
**Visit the cartwright and saddler**: stolarz zrobi Ci woz (zwierze juczne w zaprzegu wiezie 500 kg zamiast 100, kolumna jedzie 10% wolniej), garbarz -
siodlo transportowe (+50 kg na jucznym, nie da sie na nim jezdzic w boju). **Przy wgraniu trzeba w Twoich ustawieniach zmienic CampStartHour 0 -> 21
i CampEndHour 6 -> 3** (inaczej oboz zostanie 0-6).

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
   - `NightRest.cs`: zapasowe wartosci `CampStart/CampEnd` (bez ustawien) 21 i 3; nowe `MenuNight(h)` = `h >= 21 || h < min(swit gracza, 12)` - sen w menu
     (SleepInit, SleepTick) i przewidywanie pobudki w karczmie (`TownInns.PredictWake`) licza pelna stawke od 21:00 do switu (przy swicie 6 dokladnie jak dotad 21-5).
   - `NightMarch.cs`: `DebtCampHour` liczony **od konca obozu**: `CampEnd - max(10, dlugosc obozu)` (0-6 -> 20:00 jak dotad; 21-3 -> 17:00, 10 h pelnej stawki).
   - `SightRange.cs`: noc wzroku = `CampaignTime.Now.IsNightTime` (noc gry 22-2, pola statyczne SunRise/SunSet - tanie).
2. **Spacer po obozie** (commity 0ff640ba, 01b47ebf) - NOWY `CampWalk.cs`:
   - Opcja **Walk through the camp** tylko w naszym `arm_camp_wait` (zwykle menu, czas stoi; NIE w "camp" gry - to menu spotkania
     `DefaultEncounterGameMenuModel`, i nie w menu oczekiwania BK). Warunki: brak bitwy/oblezenia/spotkania, poza osada, na ladzie, brak innej misji.
   - Misja `MissionState.OpenNew("Camp", ...)` na scenie bitwy z miejsca postoju (rekord jak Practice Drills: teren, pogoda i pora z mapy, latka mapy).
     Zachowania: MissionOptionsComponent, CampaignMissionComponent, BasicLeaveMissionLogic, granice (Hard/Boundary/Crossing 10 s), EquipmentControllerLeaveLogic,
     nasza `CampWalkLogic`, `ViewCreator.CreateMissionLeaveView()` (Tab). Bez MapEvent, bez logiki bitwy, jedna druzyna. `DoesMissionRequireCivilianEquipment = true`
     w delegacie (przed OnMissionBehaviorInitialize modulow) - **DTE nie dopina swojej logiki zbrojowni** (sprawdzone: DTE SubModule.OnMissionBehaviorInitialize
     pomija misje cywilne), ludzie w stroju cywilnym.
   - `CampWalkLogic.Build` (raz, AfterStart): srodek = srodek granic sceny (walk area) albo srodek sceny; gracz 6 m przed srodkiem; ognisko gracza, jego poslanie
     i jego kon (BattleEquipment); 1-5 ludzi - poslania wokol ogniska; od CampWalkMenPerTent (6) ludzi: namiot wodza `Tent_Leader`, namioty zolnierskie
     `Tent_Small` po jednym na 6 ludzi (najwyzej 40), rzedami po obu stronach, ognisko co trzy namioty (najwyzej 14), od 60 ludzi `Tent_Officer`; przy armii
     chorazy z herbem rodu gracza (`campaign_banner_small`) przed namiotem wodza; wozy gracza (`bd_cart_a..c`, najwyzej 6) z boku; konie i juczne z taboru gracza
     (SpawnMonster, najwyzej 12, bez bydla). Brak prefabu ROT - nastepny z listy (namioty Native), sprawdzane raz na proces `GameEntity.PrefabExists`.
   - Ludzie: zolnierze druzyny gracza (bez bohaterow), najwyzej **CampWalkMaxMen (40)**, `SimpleAgentOrigin` (bez powiazania z rosterem - zadne zdarzenie misji
     nie zabierze nikogo z druzyny), Controller None, bez broni; po 4 przy ognisku (siedza, w godzinach obozu albo noca polowa spi), reszta limitu lezy przy
     namiotach. Pozy co 1.5 s (tylko postawieni ludzie). Scenografia **bez fizyki** (jak CampScene - sprawdzony sposob).
   - Linie logu: `Spacer po obozie (m97): opcja ...` (start sesji), `... otwieranie | scena ...`, `... wejscie | ... namioty N ..., poslania, ogniska, wozy a/b |
     agenci ludzi N z limitu 40 (spiacych) | konie | elementy | gracz tak`, `... wyjscie po N s | potkniecia razem`. Brak prefabu: `... brak prefabu X`.
3. **Woz taborowy i siodla transportowe** (commit c81c3ea3) - NOWY `CampCart.cs`:
   - Woz = **stan druzyny gracza** (liczba wozow, zapis `arm_campcart` "1|wozy" przez `SaveText.Sync`, osobny try; brak klucza = 0), NIE nowy przedmiot XML
     (bez nowej siatki i typu w zapisie - nie da sie sprawdzic w grze tej nocy).
   - Miasto: opcja **Visit the cartwright and saddler** (menu `arm_cartwright`): woz robi stolarz (warsztat `wood_WorkshopType`) z **6 drewna + 1 narzedzi** z polki
     miasta (ShelfBuy - kazda sztuka po swojej cenie, zloto do kasy miasta) + robocizna **120** wlascicielowi warsztatu (wlasny warsztat - bez robocizny, bez wlasciciela -
     kasa miasta). Siodlo transportowe robi garbarz (`tannery`): **2 skory + 1 drewno** + robocizna **40** -> prawdziwy przedmiot gry `mule_load_a` do taboru.
     "Break up a cart for its wood" - woz mniej, 3 drewna do taboru. Niewola gracza: wozy biora zwyciezcy (3 drewna na woz do taboru ich partii). AI wozow nie kupuje.
   - Udzwig: postfiks `DefaultInventoryCapacityModel.CalculateInventoryCapacity` (`AccessTools.DeclaredMethod`, jak PackMarket; Naval wola BaseModel): na kazdy woz
     jedno zwierze juczne gracza w zaprzegu = **CartLoadKg (500) - 100** kg wiecej (wpis "Supply carts"); wolne zwierze juczne z siodlem transportowym w taborze partii
     rodu gracza = **PackSaddleExtraKg (50)** + waga siodla (siodlo lezy na zwierzeciu; wpis "Pack saddles"). Armia (includeFollowers) - takze doczepione partie.
   - Predkosc: postfiks `CalculateFinalSpeed` kazdego modelu predkosci (licznik glebokosci jak SightRange - raz na lancuch): druzyna gracza z wozem w zaprzegu
     i wodz armii, do ktorej gracz jest doczepiony: **-CartSpeedPenaltyPercent (10)%** (wpis "Supply cart"); na morzu bez kary; woz bez zwierzecia - bez udzwigu i bez kary.
   - `SaddleLaw.ItemKind`: siodlo transportowe = "nie pasuje" (nie robi jezdzca) - siodla bojowe i transportowe rozdzielone.
   - Linie logu: `Woz obozowy (m97): wpiecia: udzwig, predkosc N modeli`, `... start sesji | woz TAK ... | siodla transportowe TAK ... | wozy gracza N | materialy ...
     | menu miasta tak`, zakup/rozbicie/niewola, doba `Woz obozowy (m97): dzien N | wozy gracza ... (w zaprzegu ... z ... jucznych, +... kg, wolniej o ...%) | siodla ...`
     (tylko gdy jest woz albo cos sie stalo).
- Klucze NOWE (domyslnie TAK, grupa MCM "Camp and baggage (night 20/21.10)"): **CampWalkEnabled**, **CampWalkMaxMen** (40, suwak 0..100), **CampWalkMenPerTent** (6, 2..20),
  **CampCartEnabled**, **CartLoadKg** (500, 100..2000), **CartSpeedPenaltyPercent** (10, 0..50), **PackSaddleEnabled**, **PackSaddleExtraKg** (50, 0..300).
- Wpiecia: `ArmouryBehavior.cs` dwie linie RegisterEvents (CampWalk, CampCart) + blok SyncData `arm_campcart`; `SubModuleMain.cs` jedna linia `CampCart.ApplyAll`;
  `SaddleLaw.cs` jedna linia. Zasoby Practice Drills (dzwieki .wav, tarp_canvas.jpg) NIE dolaczone - dzwiek wymaga wlasnego odtwarzacza (PD ma AmbientMixer),
  a tekstura nie jest potrzebna przy prefabach ROT; mozna w etapie 2.

**Uzasadnienie liczb (decyduje Claude, zasada Jeffa):** woz jednokonny w sredniowieczu wiozl ok. 400-600 kg (kon juczny ok. 100 kg) - 500 kg; woz
jedzie stepa, ale piechota tez - 10% wolniej (sufit kolumny MarchPace i tak trzyma tempo piechoty). Siodlo transportowe (lepszy rozklad ladunku) +50%
udzwigu jucznego - 50 kg. Materialy: rama i kola z drewna, okucia z narzedzi; siodlo z dwoch skor na drewnianym terlicy; robocizna ok. 2-3 dni rzemieslnika.

**Ryzyko / co sprawdzic (kontrola calosci):**
- **Oboz 21-3 dotyczy calego swiata** - wszystkie moduly pytaja o ten sam przelacznik (InCamp/CampEnd/PlayerDawn): sen gracza i "Night falls - the column makes camp"
  o 21, ksiega snu AI, marsz nocny, dlug snu, dzien odpoczynku armii (`ArmyRestDay` - swit = PlayerDawn = 3), karczmy (pobudka nie przed switem = 3:00), Wayfinder
  (18 h marszu na dobe jak dotad; ciemnosc 22-2 cala w obozie), WorldMeasure. Noc przez polnoc obsluzona (InCamp, CampHolds z `InCamp(h-1)` przez Mod24).
  Swit 3:00 zamiast 6:00 przesuwa pobudke, karczme i dobe armii o 3 h wczesniej. Stawki snu: noc snu gracza (OnHourly) ticki 21..3 = 7 h pelnej stawki
  (bylo 21..6 = 10 h) - spiacy w obozie 21-3 ma pelne 6 h; kto kladzie sie po polnocy, spi czesciowo "za dnia" (DayRestFactor). Zmieniac godziny w dzien
  (4:00-20:00), nie w srodku nocy obozu.
- Warsztat, kuznia i samonaprawa (`WorkshopNightRest` 23-5) - inna regula (noc warsztatu), bez zmian. `HideoutAlarm` (noc 21-5, slyszalnosc w kryjowce) - bez zmian.
- **Spacer = pierwsza wlasna misja w naszych modach, NIEPRZETESTOWANA w grze** (zakaz uruchamiania gry tej nocy). Nie sprawdzone: czy widoki "Camp" dzialaja
  bez spotkania (PD otwiera "CustomBattle"; my "Camp" jak SandBox OpenCampMission bez MapEvent), powrot do `arm_camp_wait` po Tab (PD wraca do swojego menu tak samo),
  zachowanie BK/ROT na `OnMissionStarted` z `CampaignMissionComponent` (PD ma to samo w trybie odwiedzin), polozenie namiotow ROT (skala, obrot, drzewa i skaly -
  bez sprawdzania kolizji; gracz przechodzi przez namioty - bez fizyki), czy zestaw `as_human_hideout_bandit` i pozy PD sa w ROT. Kazdy element w try z licznikiem
  potkniec - wywrotka jednego elementu = bez niego. Wydajnosc: tylko na zadanie gracza; najwyzej 40 ludzi + 12 koni + ok. 60 elementow.
- Nasze zachowania misji dopinane do kazdej misji (FieldCraft, HitScribe, GuardMaster, BrokenMen, RoutKeeper, BattleSense, HorseFateMission, CampScene): dzialaja
  tez w misjach wsi/miast; HorseFateMission liczy tylko jezdzcow z PartyBase (nasi maja SimpleAgentOrigin, bez koni) - zero wpisow. CampScene: `IsFieldBattle` false - nic.
- **Woz**: udzwig - jedno miejsce z PackMarket (ten sam postfiks, inny skladnik), luzak pod jukami bez zmian; zwierze w zaprzegu traci swoje 100 kg na rzecz 500 (bez
  podwojnego liczenia). Predkosc: sufit kolumny MarchPace (Last) moze "zjesc" kare wozu, gdy piechota i tak trzyma nizsze tempo - zamierzone (woz jedzie stepa).
  Zamknieta ekonomia: material z polki (cena z popytu, zloto do kasy miasta), robocizna do wlasciciela warsztatu, woz rozbity -> drewno, niewola -> drewno do zwyciezcy;
  nic z niczego. Linia "Ceny hurtu" nie liczy tych zakupow (tally false). Siodlo `mule_load_a` w taborze: sprzedawalne jak kazdy przedmiot; DTE moze je widziec
  w taborze jako uprzaz (nie sprawdzone, czy DTE bierze uprzaz z taboru gracza) - SaddleLaw juz go nie uzyje do jezdzca.
- Menu miasta: `TownMenuTidy` przenosi tylko opcje z listy - nasza zostaje w menu miasta (wyjscie `SwitchToMenu("town")`).
- Zapis: nowy klucz `arm_campcart`, stary zapis bez klucza = 0 wozow; stara DLL ignoruje klucz.

**Status:** DO SPRAWDZENIA (build Armoury kod 0 po kazdym kroku; gra nieuruchomiona; nic nie wgrane; Armoury.json Jeffa nie ruszany).

**Przy wgraniu (Armoury.json Jeffa, gra zamknieta, kopia .bak):** `"CampStartHour": 0` -> `21`, `"CampEndHour": 6` -> `3`. Nowe klucze (CampWalk*, CampCart*,
CartLoadKg, CartSpeedPenaltyPercent, PackSaddle*) w pliku nie istnieja - MCM wezmie domyslne z kodu (TAK). Zmieniac w dzien gry (4:00-20:00).

**Test w grze krok po kroku:**
1. Start sesji - w `Armoury.log`: `NightRest: oboz swiata 21:00-3:00 ... swit gracza 3:00 ... oboz splaty dlugu 1 od 17:00`; `Woz obozowy (m97): wpiecia: udzwig,
   predkosc N modeli` (N >= 1); `Woz obozowy (m97): start sesji | woz TAK ... materialy drewno tak, narzedzia tak, skora tak, siodlo do robienia tak | menu miasta tak`;
   `Spacer po obozie (m97): opcja "Walk through the camp" w menu obozu (arm_camp_wait) | klucz Camp Walk Enabled TAK`.
2. Mapa ok. 20:30-21:59: pierwszy tick po 21:00 - pytanie "Night falls - the column makes camp." i kolumny AI staja; o 22:00-2:00 w podpowiedzi predkosci
   nikt nie jedzie (kara "night" tylko u marszu forsownego). Wzrok: podpowiedz zasiegu o 21:30 - "Daylight", o 22:30 - "Darkness". Swit: kolumny ruszaja 3:00-4:00.
3. Klawisz O na mapie -> menu obozu -> **Walk through the camp** -> ekran ladowania -> scena: gracz przy ognisku, poslanie, jego kon; przy 20 ludziach 4 namioty,
   namiot wodza, ogniska z siedzacymi; w nocy czesc spi. W logu linia `Spacer po obozie (m97): wejscie | ...`. Przytrzymac Tab -> wraca menu obozu,
   godzina na mapie ta sama co przed wejsciem; w logu `... wyjscie po N s`. Sprawdzic wejscie w dzien i w nocy, sam (bez ludzi) i w armii (chorazy z herbem).
4. Miasto ze stolarzem (Wood Workshop) -> **Visit the cartwright and saddler** -> "Have a supply cart built" -> komunikat o cenie; w ekwipunku/druzynie: podpowiedz
   udzwigu "Supply carts +400" (przy co najmniej jednym zwierzeciu jucznym), podpowiedz predkosci "Supply cart -10%". Bez jucznego - woz bez udzwigu i bez kary.
5. Miasto z garbarnia -> "Have a pack saddle made" -> w taborze "Mule Harness with Pack"; podpowiedz udzwigu "Pack saddles +170" (50 + 120 waga siodla) na wolnym jucznym.
   Awans piechura na jezdzca nie bierze tego siodla (SaddleLaw).
6. Zapis i wczytanie - liczba wozow zostaje (`Woz obozowy (m97): start sesji | ... wozy gracza N`).
Linie do grep: `NightRest: oboz swiata`, `Woz obozowy (m97)`, `Spacer po obozie (m97)`, `CampWalk.`, `CampCart.` (bledy w Log.Error).

**Co zostalo (etap 1 - czesciowo):** spacer nieprzetestowany w grze (pierwsza wlasna misja - autotest z wejsciem do sceny przed wgraniem, `bieg-bitwa.ps1`
nie obejmuje tej misji); bez kolizji z drzewami i skalami przy stawianiu namiotow; bez fizyki namiotow; ludzie tylko siedza/spia (chodzenie, jedzenie, rozmowy,
cwiczenia - etap 2); bez dzwiekow obozu (zasoby PD - etap 2); woz nie jest przedmiotem ekwipunku (nie widac go w ekranie ekwipunku, tylko w menu i w scenie);
markietani i miasteczko obozowe - etap 2 (decyzja zadania).
