# m101-prolog - prolog 298 (start od wyjazdu krola Roberta do Winterfell), etapy E0-E2

Galaz `w-toku/m101-prolog` (baza n29-calosc 1d2318e3). Projekt: PROJEKT-PROLOG-298-2026-10-21.md (pkt 1, 2 - os dni 0-188, pkt 4 - etapy).
Jeff 21.10 (doslownie): "Robimy wczesniejszy start"; "Ok wersje lekka robimy" + wariant "Krolestwa zostaja osobno, ale na czas prologu lacze je z
krolestwem Roberta paktami i sojuszami (mod Diplomacy to umie)" - "ok"; "I zrob wydarzenia wedlug ksiazki"; "i rob te rzeczy, najwyzej wyczerpiesz
limit, to zapisuj przy kazdej zmianie". Zakres tej galezi: E0 + E1 + E2 (sceny E4/E5 i postacie E3 - pozniej, osobno).

## Stan pracy (aktualizowany po kazdym kroku)

| Krok | Co | Stan |
|---|---|---|
| E0 | wybor startu przy nowej kampanii, zapis wyboru, klucz MCM, flaga autotestu | ZROBIONE (build 0) |
| E1a | kalendarz -188 dni, start opowiesci, latka odczytow CampaignStartTime w ROT.dll, Fabula/Inni CrashScribe | do zrobienia |
| E1b | pokoj w prologu (wojny Westeros-Westeros zablokowane), pakty i sojusze z krolestwem Roberta | do zrobienia |
| E2a | Robert i Ned zyja, glowy rodow i wladcy | do zrobienia |
| E2b | koniec prologu (dzien 186): smierc Roberta, Joffrey krolem, Ned w lochu; NedEvent zabija Neda, Robb wladca Polnocy | do zrobienia |
| LOG | grupa tools/sprawdz_logi.py --grupa m101prolog | do zrobienia |

## Rozpoznanie (dekompilacja gry 1.4.8, ROT 8.1.8, Diplomacy 1.4.7, BK)

- **Kiedy powstaje zegar kampanii**: `Campaign.OnInitialize` - najpierw `GameManager.OnGameStart` (nasz `SubModuleMain.OnGameStart`, tam `LongYearTimeModel.Install`),
  potem `MapTimeTracker = new MapTimeTracker(Models.CampaignTimeModel.CampaignStartTime)` (tylko nowa kampania; wczytanie - zegar z zapisu), potem XML swiata
  (bohaterowie: `Hero.Deserialize` - `alive="false"` daje od razu stan Dead i losowe daty urodzin/smierci, a `Clan = clan` wpisuje bohatera do listy martwych
  rodu), `CampaignObjectManager.InitializeOnNewGame` (listy zywych/martwych wedlug stanu), na koncu `OnNewGameCreated` -> 100 x `OnNewGameCreatedPartialFollowUp`
  (HeroSpawnCampaignBehavior rozstawia lordow i druzyny przywodcow rodow) -> `...PartialFollowUpEnd`. Kreator postaci rusza dopiero po tym wszystkim
  (SandBoxGameManager.OnLoadFinished).
  **Wniosek**: wybor prologu musi byc znany PRZED `Campaign.OnInitialize` - w kreatorze postaci albo w OnNewGameCreated jest za pozno (zegar i stan Roberta/Neda
  juz ustalone; cofniecie zegara po stworzeniu swiata przesunelo by w przyszlosc wszystkie znaczniki czasu nadane przy tworzeniu - wszystkie mody). Dlatego okno
  wyboru jest w MENU GLOWNYM, przy "New Campaign" (prefiks `MBGameManager.StartNewGame(MBGameManager)` - jedyna droga SandBox do nowej gry i do wczytania;
  `SandBoxGameManager.LoadingSavedGame` odroznia wczytanie).
- **Odczyty CampaignStartTime gry** (TaleWorlds.CampaignSystem): `Campaign.DailyTick` (tydzien co 7 dni od startu), `Campaign.CreateCampaignEvents` (faza
  ticku dobowego), `MapTimeTracker` nowej gry, `SaveHandler` (DayLong w opisie zapisu), `FoodConsumptionBehavior` (pierwsza doba), `KingdomDecisionProposalBehavior`
  (5 dni bez decyzji). Wszystkie chca startu KALENDARZA - zostaja na modelu. Przesuniecie o pelne 188 dni nie zmienia fazy ticku dobowego.
- **Odczyty ROT** (ROT.dll, lista pytania-tmp/rot-grep-start.txt): 38 miejsc w 28 typach - wydarzenia (`CampaignStartTime + Days(N)`), `ROTStorylineWar.Enforced`
  (StartDay/EndDay), `ROTStorylineWars.EnforceWars` (nic przed doba 1), `ROTOthersCampaignBehavior` (Inni 300/400/500 dni, rytm co 4 dni), `ROTEnlistmentBehavior`,
  `AIThinkPatch` (Nocna Straz po 1.5 roku). Wszystkie to os OPOWIESCI.
- **Inne mody**: BK (`BKInterestGroupsModel` lata radykalow od startu, `BKClanBehavior` 30 dni) - wiek kampanii (kalendarz, bez zmian); ROTBattleAtmosphere
  `LoreGates.CampaignDay` - os opowiesci, ale osobny zestaw (poza zakresem tej latki - ryzyko); AIInfluence - zaciemniony, bez zmian; SandBox statystyki - kalendarz.
  Nasze: CrashScribe Fabula.DayNow i NightKingCall.Day - os opowiesci; reszta (logi, wiek kampanii w Armoury) - kalendarz.
- **Wojny fabularne ROT**: `ROTStorylineWars.OnNewGameCreated` - 27 wojen; StartDay 0 bez wyzwalacza: Smocza Skala - Krainy Burzy oraz Nocna Straz - Wolni Ludzie;
  `EnforceWars` (doba) wypowiada przez `DeclareWarAction.ApplyByDefault`; na starcie gry w XML tylko Nocna Straz - Wolni Ludzie `isAtWar="true"`
  (ROT_spkingdoms.xml). ROT sam latka `DeclareWarAction.ApplyInternal` (prefiks, ModCompatibility.Diplomacy).
- **Diplomacy 1.4.7**: tylko pakty o nieagresji (`AbstractDiplomaticAction<FormNonAggressionPactAction>.Apply(k1, k2, forcePlayerCharacterCosts, bypassCosts,
  customDurationInDays, queryPlayer)`, `DiplomaticAgreementManager.HasNonAggressionPact` + `Expire()`), sojusze sa w grze od 1.3 (`IAllianceCampaignBehavior.StartAlliance /
  EndAlliance`, sojusz 84 dni, `UpdateAllianceEndTime` prywatne - tylko przedluza). Poczatek sojuszu gasi pakt tej pary (Diplomacy: OnAllianceStarted).
- **Bohaterowie** (ROT-Content/ModuleData): Robert `ROTlord_506` (wiek 50, zona Cersei `lord_4_2`, rod `clan_nord_1` "Baratheon, Joffrey", `alive="false"`),
  Ned `ROTlord_512` (42, zona Catelyn `ROTlord_513`, rod `clan_battania_1` Stark, `alive="false"`). Krolestwo korony `vlandia` (owner `lord_7_1` Joffrey), Polnoc
  `battania` (owner `lord_5_1` Robb). Krolewska Przystan `town_EW4`.
- **Smierc wladcy** (`KillCharacterAction.ApplyInternal`): nowy przywodca rodu z `ChangeClanLeaderAction.ApplyWithoutSelectedNewLeader`, potem glosowanie
  `KingSelectionKingdomDecision`; ROT mnozy zaslugi rodu panujacego x999 (KingdomDecisionPatches.patch1), BK przy tytule dziedzicznym daje tytul dziedzicowi.

## Zasada

Jeden wybor przy "New Campaign" (menu glowne), przypiety do jednej kampanii i zapisany w niej. Kampania z prologiem: kalendarz zaczyna sie 188 dni przed startem
opowiesci ROT; cala opowiesc ROT (wydarzenia, wojny fabularne, Inni) liczy sie od startu opowiesci, czyli tak jak w klasycznym starcie. Klasyczny start: nic sie nie
zmienia (latki opowiesci nawet sie nie wpinaja).

## Zmiana na krok

### E0 - wybor startu (ZROBIONE)
- `Armoury/src/Prologue.cs` (nowy): prefiks `MBGameManager.StartNewGame` - nowa kampania SandBox: okno "Choose your start" (tekst EN: "Prologue 298 - the King rides
  north" / "Classic start 299", przyciski "Prologue 298" / "Classic 299"), po wyborze ta sama nowa gra startuje (ponowne wywolanie z bramka `_reentry`).
  Wczytanie zapisu - bez okna. Klucz `PrologueEnabled` (MCM, nowy, domyslnie TAK; czytany przy kliknieciu przez `McmSettings.Apply`): NIE = klasyczny bez okna.
  Autotest (`LogGate.Mode` 1/2): bez okna; plik `Documents\Mount and Blade II Bannerlord\CrashScribe\armoury-prolog.flag` = prolog, bez pliku = klasyczny.
- `Prologue.OnGameStart` (z `SubModuleMain.OnGameStart`, przed `LongYearTimeModel.Install`): wybor przypiety do kampanii tylko, gdy `game.GameManager` to ta sama
  instancja SandBoxGameManager, dla ktorej wybrano (`Active`); kazda inna kampania (wczytanie) - `Active = false`, potem wartosc z zapisu.
- `PrologueBehavior` (nowe zachowanie): `SyncData` przez `SaveText.Sync("arm_prologue")` (format `v=1;on=1;by=...;`; pusty = klasyczny), linia startu sesji.
- `Calendar.cs`: wlasciwosc `StoryStartTime` (data startu modelu sprzed nas - ROT 299); `CampaignStartTime` zwraca ja (w E0 bez zmian wartosci).
- Log: `Prolog (m101): latki menu glownego: okno wyboru TAK|BRAK ...`, `Prolog (m101): wybor startu nowej kampanii: PROLOG 298|KLASYCZNY 299 (...)`,
  `Prolog (m101): start sesji | prolog TAK|NIE | start kalendarza ... | start opowiesci ... | potkniecia N.`

## Ryzyko / co sprawdzic

- E0: okno pokazuje sie w menu glownym (InformationManager.ShowInquiry na ekranie poczatkowym - tak jak okna gry o wyjsciu); wywolanie StartNewGame z przycisku okna
  idzie ta sama droga co przycisk "New Campaign" (GameStateManager.CleanAndPushState). Inna droga do nowej gry niz MBGameManager.StartNewGame (np. mod z wlasnym
  menedzerem gry) = brak okna i klasyczny start (bezpieczny kierunek).
- Regresja klasycznego startu: w E0 jedyna zmiana w kampanii to nowe zachowanie z pustym kluczem zapisu i linia logu.

## Status

DO SPRAWDZENIA (nic nie wgrane; gra Jeffa i test 100 nietkniete).

## Test w grze krok po kroku

(uzupelniane po kazdym kroku)
1. Menu glowne -> Campaign / New Campaign: ma wyskoczyc okno "Choose your start" z dwoma przyciskami.
2. "Classic 299" -> kampania jak dotad; w Armoury.log `Prolog (m101): wybor startu nowej kampanii: KLASYCZNY 299 (okno wyboru)` i `start sesji | prolog NIE`.
3. "Prologue 298" -> w logu `wybor ... PROLOG 298 (okno wyboru)`, `nowa kampania z PROLOGIEM 298`, `start sesji | prolog TAK`.
4. Zapisz, wczytaj: `start sesji | prolog TAK (okno wyboru)` (wybor z zapisu).

## Dla Jeffa

(uzupelniane na koniec)
