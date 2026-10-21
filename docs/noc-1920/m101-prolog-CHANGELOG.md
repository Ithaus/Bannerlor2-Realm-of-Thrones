# m101-prolog - prolog 298 (start od wyjazdu krola Roberta do Winterfell), etapy E0-E2

Galaz `w-toku/m101-prolog` (baza n29-calosc 1d2318e3). Projekt: PROJEKT-PROLOG-298-2026-10-21.md (pkt 1, 2 - os dni 0-188, pkt 4 - etapy).
Jeff 21.10 (doslownie): "Robimy wczesniejszy start"; "Ok wersje lekka robimy" + wariant "Krolestwa zostaja osobno, ale na czas prologu lacze je z
krolestwem Roberta paktami i sojuszami (mod Diplomacy to umie)" - "ok"; "I zrob wydarzenia wedlug ksiazki"; "i rob te rzeczy, najwyzej wyczerpiesz
limit, to zapisuj przy kazdej zmianie". Zakres tej galezi: E0 + E1 + E2 (sceny E4/E5 i postacie E3 - pozniej, osobno).

## Stan pracy (aktualizowany po kazdym kroku)

| Krok | Co | Stan |
|---|---|---|
| E0 | wybor startu przy nowej kampanii, zapis wyboru, klucz MCM, flaga autotestu | ZROBIONE (build 0) |
| E1a | kalendarz -188 dni, start opowiesci, latka odczytow CampaignStartTime w ROT.dll, Fabula/Inni CrashScribe | ZROBIONE (build Armoury 0, CrashScribe 0) |
| E1b | pokoj w prologu (wojny Westeros-Westeros zablokowane), pakty i sojusze z krolestwem Roberta | ZROBIONE (build Armoury 0, CrashScribe 0) |
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

### E1a - kalendarz i start opowiesci (ZROBIONE)
- `Calendar.cs`: `LongYearTimeModel.CampaignStartTime` = `StoryStartTime - Prologue.Span` w kampanii z prologiem (`Prologue.CalendarShifted`), inaczej
  `StoryStartTime` (jak dotad). `Prologue.Span` = 188 dni liczone z minut (`CampaignTime.Minutes`, bez bledu float). Kalendarz startuje w roku 298, dzien 177
  (rok 364 dni), o 06:00 jak ROT; start opowiesci = data ROT 299 bez zmian. Prolog wymaga klucza Long Year Enabled (bez modelu Armoury - klasyczny start + linia logu).
- `Armoury/src/Prologue.Story.cs` (nowy): JEDNA latka - transpiler na wszystkich metodach ROT.dll czytajacych `CampaignTimeModel.CampaignStartTime`
  (`callvirt get_CampaignStartTime` -> `call Prologue.StoryStartOf(model)`: StoryStartTime modelu Armoury; inny model - jego CampaignStartTime). Szukanie po
  wszystkich typach ROT.dll (z zagniezdzonymi), filtr po bajtach IL + potwierdzenie `PatchProcessor.ReadMethodBody`. Wpinana leniwie, raz na proces, przy pierwszej
  kampanii z prologiem (nowa: OnGameStart, przed zegarem; wczytana: SyncData, przed pierwszym tickiem). Klasyczna gra bez prologu w procesie - zero latek.
  Skutek: wszystkie wydarzenia ROT (`CampaignStartTime + Days(N)`), wojny fabularne (`Enforced` StartDay/EndDay, `EnforceWars` nic przed doba 1 opowiesci -
  takze wojny z StartDay 0), Inni (300/400/500 dni, rytm co 4 dni - 188 dzieli sie przez 4, rytm ten sam), zaciag i AI Nocnej Strazy licza od startu opowiesci.
- `CrashScribe/src/StoryClock.cs` (nowy): start opowiesci odbiciem (`StoryStartTime` czynnego modelu; brak - `CampaignStartTime`). `Fabula.DayNow` i `NightKingCall.Day`
  licza od niego (bramki Fabuli i kalendarz Innych w prologu ujemne = zamkniete; os x4.33 bez zmian - dni prologu to zwykle dni gry, os liczy od startu opowiesci).
- Log: `Prolog (m101): latki opowiesci ROT (nowa kampania|wczytany zapis): wpiete N/M metod (miejsc odczytu X, podmienionych Y) w Z ms.`; w linii startu sesji:
  `start kalendarza Year 298, Day 177 | start opowiesci Year 299, Day 1 (dzien prologu D) | latki opowiesci ROT: ...`.

### E1b - pokoj krola i pakty z krolestwem Roberta (ZROBIONE)
- `Armoury/src/Prologue.Peace.cs` (nowy), klucze `ProloguePeace` i `ProloguePacts` (MCM, nowe, domyslnie TAK):
  - POKOJ (do startu opowiesci): prefiks `DeclareWarAction.ApplyInternal` - wojna miedzy dwoma krolestwami Westeros (battania, vlandia, riverlands, dragonstone,
    stormlands, empire_w, empire, sturgia, aserai, nightswatch, skagosi) nie wchodzi. Wolni Ludzie (freefolk) poza regula (Nocna Straz - Wolni Ludzie i najazdy jak
    dotad). Poza regula tez krolestwo gracza i wojny `CausedByPlayerHostility / CausedByCrimeRatingChange / CausedByRebellion / CausedByKingdomCreation`.
    Lapie wszystkie drogi (AI, decyzje krolestwa, BK, Diplomacy, wojny fabularne ROT, wezwania sojusznikow) - jedna brama gry.
  - PAKTY: sojusz gry (`IAllianceCampaignBehavior.StartAlliance`, koniec przesuniety na dzien 187 prologu prywatnym `UpdateAllianceEndTime`) korona (vlandia) +
    Polnoc, Krainy Burzy, Dorzecze; pakt o nieagresji Diplomacy (odbiciem, bez kosztow, bez pytania gracza) korona + Dolina, Smocza Skala, Reach, Dorne, Zelazne
    Wyspy, Skagos. Zakladane po rozstawieniu swiata (`OnNewGameCreatedPartialFollowUpEnd`), pilnowane co dobe (wygasly / zerwany - od nowa). Wezwania do wojny
    miedzy krolem i sojusznikami z prologu zablokowane w prologu (prefiks `AllianceCampaignBehavior.StartCallToWarAgreement`). `EndPacts` (wola E2b przy smierci
    Roberta): nasze sojusze `EndAlliance`, nasze pakty `Expire()`; zapis pamieta, ktore sa nasze (pa=/pn=).
  - Linia doby w prologu: `Prolog (m101): dzien N prologu (opowiesc za X dni) | pokoj krola TAK | wojny Westeros-Westeros (bez Wolnych Ludzi) 0 | zablokowane
    wypowiedzenia dzis A (razem B) [...] | pakty: sojusze 3/3, pakty o nieagresji 6/6 | wydarzenia ROT ruszone 0 | potkniecia 0 (razem 0).` (po starcie opowiesci
    jeszcze 20 dni linii).
- `CrashScribe/src/CanonAlliances.cs`: `Allowed` przepuszcza sojusz z prologu (`StoryClock.PrologueAlliance` -> `Armoury.Prologue.IsPrologueAlliance` odbiciem) -
  zrywanie przy wczytaniu i blokada propozycji AI nie ruszaja sojuszy krola w prologu.

## Ryzyko / co sprawdzic

- E0: okno pokazuje sie w menu glownym (InformationManager.ShowInquiry na ekranie poczatkowym - tak jak okna gry o wyjsciu); wywolanie StartNewGame z przycisku okna
  idzie ta sama droga co przycisk "New Campaign" (GameStateManager.CleanAndPushState). Inna droga do nowej gry niz MBGameManager.StartNewGame (np. mod z wlasnym
  menedzerem gry) = brak okna i klasyczny start (bezpieczny kierunek).
- Regresja klasycznego startu: w E0 jedyna zmiana w kampanii to nowe zachowanie z pustym kluczem zapisu i linia logu.
- E1a: oczekiwane M = 28 typow / 38 miejsc z rozpoznania (liczba metod moze byc wieksza o metody zagniezdzone); mniej = ktores miejsce ROT liczy od kalendarza
  (wydarzenie albo wojna ruszy za wczesnie). Metoda ROT wklejona przez JIT w wolajacego przed latka nie zobaczy podmiany - latka wchodzi przed pierwszym tickiem
  kampanii (ROT czyta start w tickach), a czytelnicy to duze metody i wywolania wirtualne/delegaty. ROTBattleAtmosphere `LoreGates.CampaignDay` (atmosfera bitew wedlug
  dnia) liczy dalej od kalendarza - w prologu zachowa sie jak po 188 dniach opowiesci (poza zakresem, do decyzji). AIInfluence (zaciemniony) - od kalendarza.
- E1b: zablokowana decyzja wojny AI zuzywa wplyw rodu (decyzja przechodzi, akcja nie) - w prologu AI moze tracic wplyw na probach (licznik w linii doby).
  Wojna zablokowana w samej akcji: postfiksow na ApplyInternal nie ma (grep BK/Diplomacy/BKROTPatch/nasze), zdarzenie OnWarDeclared nie leci. Prefiks ROT na tej samej
  metodzie dalej dziala. Sojusz gry daje wspolne bitwy/armie AI jak kazdy sojusz; wezwania do wojny zablokowane tylko dla par z prologu. Krolestwo korony w wojnie z
  ktoryms krolestwem - para pominieta (linia "pominiete"). Brak Diplomacy - same sojusze (3/3), linia "brak moda Diplomacy".
- E1a regresja klasycznego startu: model czasu zwraca te sama wartosc co dotad (StoryStartTime = stary getter), latka ROT nie wpina sie; StoryClock w CrashScribe
  zwraca StoryStartTime = CampaignStartTime co do tika.

## Status

DO SPRAWDZENIA (nic nie wgrane; gra Jeffa i test 100 nietkniete).

## Test w grze krok po kroku

(uzupelniane po kazdym kroku)
1. Menu glowne -> Campaign / New Campaign: ma wyskoczyc okno "Choose your start" z dwoma przyciskami.
2. "Classic 299" -> kampania jak dotad; w Armoury.log `Prolog (m101): wybor startu nowej kampanii: KLASYCZNY 299 (okno wyboru)` i `start sesji | prolog NIE`.
3. "Prologue 298" -> w logu `wybor ... PROLOG 298 (okno wyboru)`, `nowa kampania z PROLOGIEM 298`, `start sesji | prolog TAK`.
4. Zapisz, wczytaj: `start sesji | prolog TAK (okno wyboru)` (wybor z zapisu).
5. (E1a) Prolog: na pasku mapy data "Year 298, Day 177"; w Armoury.log `latki opowiesci ROT (nowa kampania): wpiete N/N metod (miejsc odczytu 38 ...)`;
   w logu CrashScribe `FABULA: dzien -187 | ...` (ujemny = prolog) i zadnego `FABULA: wydarzenie ... WYSTARTOWALO` przed dniem 0.
7. (E1b) Prolog: linia `swiat rozstawiony | pakty: sojusze 3/3, pakty o nieagresji 6/6`; na mapie (encyklopedia krolestwa korony / ekran Diplomacy) sojusze z
   Polnoca, Krainami Burzy i Dorzeczem, pakty z reszta; w liniach doby `wojny Westeros-Westeros (bez Wolnych Ludzi) 0`; Nocna Straz w wojnie z Wolnymi Ludzmi.
6. (E1a) Klasyczny: data "Year 299, Day 1", `latki opowiesci ROT: nie wpiete (brak kampanii z prologiem w tym procesie)`, Fabula jak dotad (`FABULA: dzien 0`).

## Dla Jeffa

(uzupelniane na koniec)
