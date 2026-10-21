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
| E2a | Robert i Ned zyja, glowy rodow i wladcy | ZROBIONE (build Armoury 0) |
| E2b | koniec prologu (dzien 186): smierc Roberta, Joffrey krolem, Ned w lochu; NedEvent zabija Neda, Robb wladca Polnocy | ZROBIONE (build Armoury 0) |
| LOG | grupa tools/sprawdz_logi.py --grupa m101prolog (PR-0..PR-8) | ZROBIONE (sprawdzone na logu syntetycznym) |
| REC | poprawki po recenzji R1-R5 (1 wazne, 4 drobne - wszystkie prawdziwe, wszystkie poprawione) | ZROBIONE (build Armoury 0, CrashScribe 0; commit po kazdej) |

## Rozpoznanie (dekompilacja gry 1.4.8, ROT 8.1.8, Diplomacy 1.4.7, BK)

- **Kiedy powstaje zegar kampanii**: `Campaign.OnInitialize` - najpierw `GameManager.OnGameStart` (nasz `SubModuleMain.OnGameStart`, tam `LongYearTimeModel.Install`),
  potem `MapTimeTracker = new MapTimeTracker(Models.CampaignTimeModel.CampaignStartTime)` (tylko nowa kampania; wczytanie - zegar z zapisu), potem XML swiata
  (bohaterowie: `Hero.Deserialize` - `alive="false"` daje od razu stan Dead i losowe daty urodzin/smierci, a `Clan = clan` wpisuje bohatera do listy martwych
  rodu), `CampaignObjectManager.InitializeOnNewGame` (listy zywych/martwych wedlug stanu), na koncu `OnNewGameCreated` -> 100 x `OnNewGameCreatedPartialFollowUp`
  (HeroSpawnCampaignBehavior rozstawia lordow i druzyny przywodcow rodow) -> `...PartialFollowUpEnd`. Kreator postaci rusza dopiero po tym wszystkim
  (SandBoxGameManager.OnLoadFinished).
  **Wniosek**: wybor prologu musi byc znany PRZED `Campaign.OnInitialize` - w kreatorze postaci albo w OnNewGameCreated jest za pozno (zegar i stan Roberta/Neda
  juz ustalone; cofniecie zegara po stworzeniu swiata przesunelo by w przyszlosc wszystkie znaczniki czasu nadane przy tworzeniu - wszystkie mody). Dlatego okno
  wyboru jest w MENU GLOWNYM, przy nowej kampanii (ROT zastepuje "Campaign" wlasnym "Into the Realm", ktory tez wola `MBGameManager.StartNewGame(new
  SandBoxGameManager(...))`; prefiks `MBGameManager.StartNewGame(MBGameManager)` - jedyna droga SandBox do nowej gry i do wczytania;
  `SandBoxGameManager.LoadingSavedGame` odroznia wczytanie).
- **Odczyty CampaignStartTime gry** (TaleWorlds.CampaignSystem): `Campaign.DailyTick` (tydzien co 7 dni od startu), `Campaign.CreateCampaignEvents` (faza
  ticku dobowego), `MapTimeTracker` nowej gry, `SaveHandler` (DayLong w opisie zapisu), `FoodConsumptionBehavior` (pierwsza doba), `KingdomDecisionProposalBehavior`
  (5 dni bez decyzji). Wszystkie chca startu KALENDARZA - zostaja na modelu. Przesuniecie o pelne 188 dni nie zmienia fazy ticku dobowego.
- **Odczyty ROT** (ROT.dll; recenzja 21.10 - licznik IL: 35 wywolan `CampaignTimeModel.get_CampaignStartTime` w 31 metodach; 38 "miejsc" z
  rozpoznania (pytania-tmp/rot-grep-start.txt) to linie dectool grep razem z definicjami `ROTCampaignData.CampaignStartTime` i `ROTCampaignTimeModel.CampaignStartTime`) - wydarzenia (`CampaignStartTime + Days(N)`), `ROTStorylineWar.Enforced`
  (StartDay/EndDay), `ROTStorylineWars.EnforceWars` (nic przed doba 1), `ROTOthersCampaignBehavior` (Inni 300/400/500 dni, rytm co 4 dni), `ROTEnlistmentBehavior`,
  `AIThinkPatch` (Nocna Straz po 1.5 roku). Wszystkie to os OPOWIESCI.
- **Inne mody**: BK (`BKInterestGroupsModel` lata radykalow od startu, `BKClanBehavior` 30 dni) - wiek kampanii (kalendarz, bez zmian); ROTBattleAtmosphere
  `LoreGates.CampaignDay` (bramki atmosfery bitew "d25", "d120"... z lore_gates.json, ujemny dzien = bramka zamknieta) - os opowiesci, ta sama latka; AIInfluence -
  zaciemniony, bez zmian; SandBox statystyki - kalendarz. ROT `ROTEnlistmentBehavior` ("You must wait at least 5 days before enlisting") - wiek kampanii, bez latki.
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
  `KingSelectionKingdomDecision` (tylko gdy ginie glowa rodu panujacego; kandydaci - 3 najsilniejsze rody). ROT `KingdomDecisionPatches.patch1` mnozy x999 ocene
  wystawiana PRZEZ rod panujacy (`CalculateMeritOfOutcomeForClan`, clan = glosujacy) - to nie daje tronu rodowi panujacemu (recenzja R5). BK przy tytule dziedzicznym
  uniewaznia glosowanie (`IsKingSelectionDecisionAllowed`), CrashScribe StoryThrone pilnuje tronu Joffreya. ROT `JoffreyEvent` omija glosowanie: najpierw
  `ChangeClanLeaderAction.ApplyWithSelectedNewLeader` (Cersei), potem smierc - tak robimy przy Robercie i Nedzie.

## Zasada

Jeden wybor przy nowej kampanii (menu glowne, "Into the Realm"), przypiety do jednej kampanii i zapisany w niej. Kampania z prologiem: kalendarz zaczyna sie 188 dni przed startem
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
- `Armoury/src/Prologue.Story.cs` (nowy): JEDNA latka - transpiler na wszystkich metodach ROT.dll (i ROTBattleAtmosphere - `LoreGates.CampaignDay`) czytajacych
  `CampaignTimeModel.CampaignStartTime`; bez latki zostaje zaciag ROT (`ROTEnlistmentBehavior`, wiek kampanii) i RYTM: odczyt, po ktorym w 6 instrukcjach jest
  reszta z dzielenia (`ROTOthersCampaignBehavior.OnAiHourlyTick`: `ElapsedDaysUntilNow % 4f > 0f` trzyma cel patrolu Innych - z ujemnym dniem prologu reszta nigdy
  nie jest > 0 i Inni zmienialiby cel co godzine; kalendarz daje liczbe dodatnia, 188 dzieli sie przez 4, rytm jak w klasycznym starcie)
  (`callvirt get_CampaignStartTime` -> `call Prologue.StoryStartOf(model)`: StoryStartTime modelu Armoury; inny model - jego CampaignStartTime). Szukanie po
  wszystkich typach ROT.dll (z zagniezdzonymi), filtr po bajtach IL + potwierdzenie `PatchProcessor.ReadMethodBody`. Wpinana leniwie, raz na proces, przy pierwszej
  kampanii z prologiem (nowa: OnGameStart, przed zegarem; wczytana: SyncData, przed pierwszym tickiem). Klasyczna gra bez prologu w procesie - zero latek.
  Skutek: wszystkie wydarzenia ROT (`CampaignStartTime + Days(N)`), wojny fabularne (`Enforced` StartDay/EndDay, `EnforceWars` nic przed doba 1 opowiesci -
  takze wojny z StartDay 0), Inni (300/400/500 dni, wynik najazdow) i AI Nocnej Strazy licza od startu opowiesci; zaciag i rytm patrolu Innych - od kalendarza.
- `CrashScribe/src/StoryClock.cs` (nowy): start opowiesci odbiciem (`StoryStartTime` czynnego modelu; brak - `CampaignStartTime`). `Fabula.DayNow` i `NightKingCall.Day`
  licza od niego (bramki Fabuli i kalendarz Innych w prologu ujemne = zamkniete; os x4.33 bez zmian - dni prologu to zwykle dni gry, os liczy od startu opowiesci).
- Log: `Prolog (m101): latki opowiesci ROT (nowa kampania|wczytany zapis): wpiete N/M metod (miejsc odczytu X, podmienionych Y, rytm na kalendarzu R, wiek kampanii
  (zaciag) bez latki A) | ROTBattleAtmosphere: wpiete 1/1 metod (...) w Z ms.` (oczekiwane dla ROT 8.1.8: N/M = 30/30, X = 34, Y = 33, R = 1, A = 1 - razem z zaciagiem 35 wywolan w 31 metodach); w linii startu sesji:
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
    miedzy krolem i sojusznikami z prologu zablokowane w prologu (prefiks `AllianceCampaignBehavior.StartCallToWarAgreement`); tym samym prefiksem kazde wezwanie
    krolestwa Westeros do wojny z krolestwem Westeros (bez krolestwa gracza) - przed rejestracja porozumienia i platnoscia (recenzja R4). `EndPacts` (wola E2b przy smierci
    Roberta): nasze sojusze `EndAlliance`, nasze pakty `Expire()`; zapis pamieta, ktore sa nasze (pa=/pn=).
  - Linia doby w prologu: `Prolog (m101): dzien N prologu (opowiesc za X dni) | pokoj krola TAK | wojny Westeros-Westeros (bez Wolnych Ludzi) 0 | zablokowane
    wypowiedzenia dzis A (razem B) [...] | pakty: sojusze 3/3, pakty o nieagresji 6/6 | wydarzenia ROT ruszone 0 | potkniecia 0 (razem 0).` (po starcie opowiesci
    jeszcze 20 dni linii).
- `CrashScribe/src/CanonAlliances.cs`: `Allowed` przepuszcza sojusz z prologu (`StoryClock.PrologueAlliance` -> `Armoury.Prologue.IsPrologueAlliance` odbiciem) -
  zrywanie przy wczytaniu i blokada propozycji AI nie ruszaja sojuszy krola w prologu.

### E2a - Robert i Ned zyja (ZROBIONE)
- `Armoury/src/Prologue.Kings.cs` (nowy), klucz `PrologueKings` (MCM, nowy, domyslnie TAK, czytany przy tworzeniu kampanii):
  - prefiks + finalizer `Hero.Deserialize`: w nowej kampanii z prologiem wezly XML `ROTlord_506` (Robert) i `ROTlord_512` (Ned) czytane z `alive="true"` na czas
    tego jednego wywolania (finalizer przywraca "false"). Gra wczytuje ich jak zywych lordow z XML (NotSpawned, urodziny z wieku 50 / 42, zony z XML, listy zywych,
    rozstawienie HeroSpawnCampaignBehavior). Bez przepisywania list rodu i menedzera obiektow (stan ustawiony ZANIM `Clan = ...` wpisuje bohatera do rodu).
  - `OnNewGameCreated` (przed rozstawieniem lordow): `clan_nord_1.SetLeader(Robert)` + nazwa rodu "Baratheon, Robert" (oryginal "Baratheon, Joffrey" w zapisie `cn=`,
    wraca przy smierci krola) -> Robert wladca vlandia; `clan_battania_1.SetLeader(Ned)` -> Ned wladca battania. Dziedzice: Joffrey, Robb (najstarsi synowie z XML).
  - `CanHeroDie`: Robert (do smierci krola, najdalej 30 dni po dniu 186) i Ned (do egzekucji w NedEvent, najdalej 30 dni opowiesci) nie umieraja zwykla smiercia
    (starosc, bitwa, nie wymuszona egzekucja) - smierci E2b sa wymuszone (isForced). Po smierci krola / po NedEvent ochrony nie ma (recenzja R3).
- Leniwe wpinanie (zmiana w `Prologue.cs`): w menu glownym tylko okno wyboru; latki pokoju/paktow i XML - `InstallRest` przy pierwszej kampanii z prologiem
  (nowa: OnGameStart - przed zegarem i XML bohaterow; wczytana: SyncData). Klasyczna gra: zadnej latki poza oknem.
- Log: `Prolog (m101): latki prologu (...): pokoj i pakty: wojny TAK, wezwania TAK, koniec sojuszu TAK | Robert i Ned z XML TAK.`,
  `Prolog (m101): Robert i Ned (XML ozywionych 2/2, ...): Robert ... glowa rodu clan_nord_1 (byl Joffrey ...), krolestwo vlandia wladca Robert ... TAK | Ned ... TAK.`;
  linia doby dostala `krol Robert ZYJE | korona: wladca ... | Ned ZYJE (wolny) | Polnoc: wladca ...`.

### E2b - koniec prologu i egzekucja Neda (ZROBIONE)
- `Armoury/src/Prologue.End.cs` (nowy):
  - doba prologu >= 186 (`DailyEnd`, raz): `EndPacts` (sojusze zerwane, pakty wygaszone), potem smierc krola: `AddDeathMark(WoundedInBattle)` +
    `KillCharacterAction.ApplyByDeathMarkForced` (wymuszona - "od ran z polowania"); krol w bitwie/oblezeniu (albo strona, ktora trzyma go w niewoli, w bitwie) - nastepna doba.
    Sukcesja (recenzja R5): PRZED smiercia krola Joffrey glowa rodu `clan_nord_1` (`ChangeClanLeaderAction.ApplyWithSelectedNewLeader`, jak ROT JoffreyEvent) - krol
    ginie jako zwykly czlonek rodu, bez losowania dziedzica i bez glosowania o tron; Joffrey martwy albo poza rodem - sukcesje robi gra (log `gra: ...`).
    Krol w walce od dnia 186 - proba co dobe bez limitu dnia (recenzja R2); pakty gasna w dniu 186 zawsze. Nazwa rodu wraca do "Baratheon, Joffrey" (z zapisu, zakodowana
    `Uri.EscapeDataString`). Komunikat dla gracza (EN) o smierci krola, Joffreyu na tronie i uwiezieniu Neda.
  - Ned do lochu Krolewskiej Przystani (`town_EW4`): jego armia rozwiazana (`DisbandArmyAction.ApplyByUnknownReason`) albo druzyna odpieta od cudzej armii,
    `TakePrisonerAction.Apply(KL.Party, Ned)`, pusta druzyna `DisbandPartyAction.StartDisband`; Ned w bitwie - nastepna doba; jeniec kogos innego - najpierw
    `EndCaptivityAction.ApplyByEscape`. Pilnowane co dobe do egzekucji (gra wypuszcza jencow przy pokoju - Ned wraca do lochu).
  - prefiks `ROT.Events.NedEvent.StartEvent` (leniwie z reszta latek prologu), PRZED `ROTEvents.OnNedDeath` ROT: najpierw Robb glowa Starkow
    (`ChangeClanLeaderAction.ApplyWithSelectedNewLeader` - recenzja R5, bez glosowania o tron Polnocy) = wladca Polnocy, potem zywy Ned ->
    `KillCharacterAction.ApplyByExecution(Ned, Joffrey, pokaz, isForced)`. Ned w walce - tylko znak smierci; gra zabija go po walce, a gdy dalej zyje -
    smierc wymuszona ze znaku w nastepnej dobie bez walki (`NedMarkFollowUp`, zapis `nm=`), wtedy druga linia egzekucji.
  - zapis: `kf=` (krol upadl), `nd=` (Ned stracony), `nm=` (Ned ze znakiem smierci, czeka na koniec walki).
- Log: `Prolog (m101): KONIEC PROLOGU (doba 186, dzien 186.x) | Robert martwy TAK (WoundedInBattle) | korona: wladca Joffrey Baratheon (lord_7_1) - Joffrey krolem TAK
  (...) | Ned: jeniec w town_EW4 TAK (...) | pakty: sojusze zerwane 3, pakty wygaszone 6 | potkniecia 0.`;
  `Prolog (m101): egzekucja Neda (NedEvent, dzien opowiesci 8.x) | Ned martwy TAK | rod Stark: glowa Robb Stark (lord_5_1) (...) | Polnoc: wladca Robb Stark (lord_5_1) - Robb TAK.`

## Ryzyko / co sprawdzic

- E0: okno pokazuje sie w menu glownym (InformationManager.ShowInquiry na ekranie poczatkowym - tak jak okna gry o wyjsciu); wywolanie StartNewGame z przycisku okna
  idzie ta sama droga co przycisk "New Campaign" (GameStateManager.CleanAndPushState). Inna droga do nowej gry niz MBGameManager.StartNewGame (np. mod z wlasnym
  menedzerem gry) = brak okna i klasyczny start (bezpieczny kierunek).
- Regresja klasycznego startu: w E0 jedyna zmiana w kampanii to nowe zachowanie z pustym kluczem zapisu i linia logu.
- E1a: oczekiwane 35 wywolan gettera w 31 metodach (licznik IL na ROT.dll 8.1.8; 38 z rozpoznania liczylo tez linie definicji), z tego 1 (ROTEnlistmentBehavior - "You must wait at least 5 days before enlisting") to wiek kampanii i
  zostaje BEZ latki (inaczej w prologu gracz nie moglby sie zaciagnac) - linia logu: "wiek kampanii (zaciag) bez latki 1"; linia: miejsc odczytu 34 w 30 metodach (1 rytm); mniej = ktores miejsce ROT liczy od kalendarza
  (wydarzenie albo wojna ruszy za wczesnie). Metoda ROT wklejona przez JIT w wolajacego przed latka nie zobaczy podmiany - latka wchodzi przed pierwszym tickiem
  kampanii (ROT czyta start w tickach), a czytelnicy to duze metody i wywolania wirtualne/delegaty. ROTBattleAtmosphere `LoreGates.CampaignDay` (atmosfera bitew wedlug
  dnia) - ta sama latka (inaczej bramki atmosfery bylyby przesuniete o 188 dni na cala kampanie). AIInfluence (zaciemniony) - od kalendarza.
- E1b: zablokowana decyzja wojny AI zuzywa wplyw rodu (decyzja przechodzi, akcja nie) - w prologu AI moze tracic wplyw na probach (licznik w linii doby).
  Wojna zablokowana w samej akcji: postfiksow na ApplyInternal nie ma (grep BK/Diplomacy/BKROTPatch/nasze), zdarzenie OnWarDeclared nie leci. Prefiks ROT na tej samej
  metodzie dalej dziala. Sojusz gry daje wspolne bitwy/armie AI jak kazdy sojusz; wezwania do wojny zablokowane tylko dla par z prologu. Krolestwo korony w wojnie z
  ktoryms krolestwem - para pominieta (linia "pominiete"). Brak Diplomacy - same sojusze (3/3), linia "brak moda Diplomacy".
- E2a: BK nadaje tytuly przy nowej grze - jesli robi to przed naszym SetLeader, tytul krola trzyma Joffrey (a nie Robert), tytul Polnocy - Robb; gra i ROT
  i tak licza wladce z przywodcy rodu panujacego. Sprawdzic w encyklopedii BK (tytuly) po starcie prologu. ROT nigdzie nie czyta Roberta ani Neda (grep ROT.dll:
  tylko definicje w ROTLords). AIInfluence moze mowic o "zmarlym krolu" (opisy) - poza zakresem.
- E2b: glosowania o tron nie ma (recenzja R5: Joffrey i Robb sa glowami rodow przed smiercia ojcow; dawniej opis twierdzil, ze premia ROT x999 zostawia tron rodowi
  Joffreya - z kodu to nie wynika). `ChangeClanLeaderAction` przed smiercia robi to samo co przy smierci (zloto ojca do syna, druzyna syna, relacje); nikt w BK,
  ROT, Diplomacy ani u nas nie slucha `OnClanLeaderChanged` (dectool grep). BK `BKTitleBehavior.OnHeroKilled` - tytuly Roberta/Neda (jesli maja) dziedziczy BK jak dotad. Ned w lochu moze uciec (4% dziennie w grze, RealisticCaptivity) albo wyjsc przy pokoju
  korony z kims trzecim - wraca do lochu nastepnej doby; egzekucja w NedEvent zabija go wszedzie. NedEvent nie odpali (wylaczona Fabula, ROT bez wydarzenia) - Ned
  zostaje w lochu i zyje (do decyzji: wtedy wlasna egzekucja w dniu ~9 opowiesci).
- E1a regresja klasycznego startu: model czasu zwraca te sama wartosc co dotad (StoryStartTime = stary getter), latka ROT nie wpina sie; StoryClock w CrashScribe
  zwraca StoryStartTime = CampaignStartTime co do tika.

## Poprawki po recenzji (21.10)

Recenzja pelna HEAD 8e09b71e: nic krytycznego, 1 wazne, 4 drobne. Kazde sprawdzone w kodzie i dekompilacji, prawdziwe poprawione (build Armoury 0, commit po kazdej).

| Nr | Waga | Co | Sprawdzenie | Stan |
|---|---|---|---|---|
| R1 | wazne | PR-2 wymagal miejsc + zaciag >= 38, a w ROT.dll jest 35 | PRAWDA: wlasny licznik IL (cel wywolania = `TaleWorlds...CampaignTimeModel`): 35 wywolan w 31 metodach (30 z latka: 34 miejsca, w tym 1 rytm `% 4f`; 1 zaciag). 38 z rozpoznania to linie dectool grep z definicjami | POPRAWIONE: PR-2 `N = M >= 30`, `miejsca + zaciag >= 35`; CHANGELOG (Odczyty ROT, log E1a, Ryzyko E1a, Test krok 4) - oczekiwane 30/30, 34, 33, 1, 1; komentarz w Prologue.Story.cs |
| R2 | drobne | `Daily()` konczyl sie po dniu 208 przed `DailyEnd` - krol w bitwie/oblezeniu od dnia 186 do 208 (i przy NedEvent) nie umarlby nigdy; pakty gasly dopiero po sprawdzeniu bitwy | PRAWDA w kodzie (Prologue.cs: return przed DailyEnd; Prologue.End.cs: Busy przed EndPacts). Sojusze i tak maja koniec w dniu 187 (`UpdateAllianceEndTime` tylko przedluza - gdy domyslny koniec sojuszu gry jest pozniej, zostaje pozniej) | POPRAWIONE: `DailyEnd` co dobe bez limitu dnia (limit 208 tylko dla linii logu); `EndPacts` przed sprawdzeniem bitwy (pakty gasna w dniu 186 zawsze); NedEvent przy krolu w bitwie - Ned stracony jak dotad (ROT zaklada martwego Neda), krol umiera po walce w nastepnej udanej probie, linia egzekucji mowi `krol Robert jeszcze zyje ...`; krol po NedEvent - bez drugiego aresztu Neda (`Ned: juz stracony (NedEvent)`, PR-7 to przyjmuje), komunikat gracza bez zdania o uwiezieniu |
| R3 | drobne | `CanHeroDie` chronil Neda do dnia 218 takze po NedEvent - egzekucja Neda w bitwie stawia tylko znak smierci, smierc po bitwie bez wymuszenia, nasze weto = Ned zyje | PRAWDA: gra `KillCharacterAction.ApplyInternal` - druzyna w MapEvent/SiegeEvent i `DeathMark == None` -> tylko `AddDeathMark` i powrot; `ApplyByDeathMark` wola `ApplyInternal` bez `isForced` -> `CanDie` -> nasze CanHeroDie | POPRAWIONE: ochrona Neda tylko `!_nedDone`, Roberta tylko `!_kingFallen` |
| R4 | drobne | blokada wojny dopiero w `DeclareWarAction` na koncu `StartCallToWarAgreement` - wczesniej gra rejestruje porozumienie, przelewa `CallToWarWallet` (gracz placi zlotem) i daje premie relacji | PRAWDA: gra `AllianceCampaignBehavior.StartCallToWarAgreement(callingKingdom, calledKingdom, kingdomToCallToWarAgainst, callToWarCost, isPlayerPaying)`: `AddCallToWarAgreement`, `ChangeHeroGold(-cost)` / portfele, `OnCallToWarAgreementStarted`, `ApplyAcceptingCallToWarOfferBonus`, na koncu `DeclareWarAction.ApplyByCallToWarAgreement` | POPRAWIONE: `CallToWarPrefix` dostal `kingdomToCallToWarAgainst` (nazwa parametru z dekompilacji) i zwraca false takze, gdy wezwany i wrog to krolestwa Westeros i zadnym nie rzadzi gracz (regula `WarPrefix`) |
| R5 | drobne | Robert i Ned gina jako glowy rodow panujacych - gra dodaje glosowanie o tron (`KingSelectionKingdomDecision`); opis "x999 zostawia tron rodowi Joffreya" nie wynika z kodu | PRAWDA: gra `KillCharacterAction.ApplyInternal` - glowa rodu panujacego: `ChangeClanLeaderAction.ApplyWithoutSelectedNewLeader`, potem `AddDecision(new KingSelectionKingdomDecision(...))`; ROT `KingdomDecisionPatches.patch1` mnozy x999 `CalculateMeritOfOutcomeForClan` dla GLOSUJACEGO rodu panujacego. ROT `JoffreyEvent` zmienia glowe rodu przed smiercia | POPRAWIONE: Joffrey glowa `clan_nord_1` przed smiercia Roberta, Robb glowa `clan_battania_1` przed egzekucja Neda (`ChangeClanLeaderAction.ApplyWithSelectedNewLeader`); zapasowo jak dotad po smierci. Opis w Rozpoznaniu i Ryzyku poprawiony. Przy okazji R3: Ned ze znakiem smierci - dobicie wymuszone po walce (`NedMarkFollowUp`) i druga linia egzekucji |

## Status

DO SPRAWDZENIA - E0, E1a, E1b, E2a, E2b i grupa logow zrobione, kazdy krok z buildem kod 0 (Armoury, CrashScribe) i commitem na `w-toku/m101-prolog`.
Recenzja pelna (21.10): R1-R5 poprawione (tabela "Poprawki po recenzji"); grupa logow sprawdzona na logach syntetycznych z prawdziwymi liczbami ROT 8.1.8
(30/30 metod, 34 miejsca, 1 rytm, 1 zaciag) i na przypadku krola w walce przy NedEvent (krol umiera pozniej, Ned juz stracony, druga linia egzekucji).
Nic nie wgrane do gry, gra Jeffa i test 100 nietkniete; w grze NIE sprawdzone (zakaz uruchamiania gry w tej pracy). Zostaje: autotest nowej kampanii z flaga
prologu (co najmniej 220 dob - przejscie przez dzien 186 i NedEvent), potem sceny E4/E5 i postacie E3 (osobno).

## Test w grze krok po kroku

Autotest (DLL autotestu CrashScribe): plik `Documents\Mount and Blade II Bannerlord\CrashScribe\armoury-prolog.flag` (tresc dowolna) = nowa kampania od razu z
prologiem, bez okna; bez pliku = klasyczny start bez okna. Po biegu: `python -I tools/sprawdz_logi.py --grupa m101prolog <Armoury-*.log> --crashscribe <session-*.log>`.

Klasyczny start (regresja):
1. Menu glowne -> "Into the Realm" (przycisk nowej kampanii ROT - ROT.SubModule: MBGameManager.StartNewGame(new SandBoxGameManager(...))): okno "Choose your start"
   z przyciskami "Prologue 298" i "Classic 299".
2. "Classic 299" -> kampania jak dotad: data "Year 299, Day 1"; w Armoury.log `Prolog (m101): wybor startu nowej kampanii: KLASYCZNY 299 (okno wyboru)` i
   `start sesji | prolog NIE | ... | latki opowiesci ROT: nie wpiete (brak kampanii z prologiem w tym procesie) | nie wpiete`; Fabula jak dotad (`FABULA: dzien 0`).
3. Klucz Prologue Enabled NIE (MCM, Armoury, grupa "Prologue 298 (new campaign)") -> nowa kampania bez okna, klasyczny start.

Prolog:
4. "Prologue 298" -> data na pasku "Year 298, Day 177"; w Armoury.log: `wybor ... PROLOG 298 (okno wyboru)`, `nowa kampania z PROLOGIEM 298`,
   `latki opowiesci ROT (nowa kampania): wpiete 30/30 metod (miejsc odczytu 34, podmienionych 33, rytm na kalendarzu 1, wiek kampanii (zaciag) bez latki 1)`, `latki prologu (nowa kampania): ... TAK`,
   `Robert i Ned (XML ozywionych 2/2 ...): ... TAK | ... TAK`, `swiat rozstawiony | pakty: sojusze 3/3 ..., pakty o nieagresji 6/6 ...`.
5. Encyklopedia: Robert Baratheon zyje, wladca krolestwa korony, glowa rodu "Baratheon, Robert", zona Cersei; Eddard Stark zyje, wladca Polnocy, zona Catelyn;
   Joffrey i Robb w rodach. Krolestwo korony: sojusze z Polnoca, Krainami Burzy i Dorzeczem; pakty o nieagresji (ekran Diplomacy) z Dolina, Smocza Skala, Reach,
   Dorne, Zelaznymi Wyspami i Skagos. Nocna Straz dalej w wojnie z Wolnymi Ludzmi.
6. Linie doby `Prolog (m101): dzien N prologu (opowiesc za X dni) | ... | wojny Westeros-Westeros (bez Wolnych Ludzi) 0 | ... | wydarzenia ROT ruszone 0`;
   w logu CrashScribe `FABULA: dzien -187 ...` (ujemne w prologu) i zadnego `FABULA: wydarzenie ... WYSTARTOWALO` z dniem ujemnym.
7. Zapisz i wczytaj: `start sesji | prolog TAK (okno wyboru) | start kalendarza Year 298, Day 177 | start opowiesci Year 299, Day 1 (dzien prologu D) | latki opowiesci
   ROT (wczytany zapis): ...`, pakty dalej (nasze sojusze 3, pakty 6).
8. Dzien 186 prologu: komunikat "King Robert Baratheon is dead ..."; encyklopedia - Robert martwy (od ran), Joffrey wladca korony, rod znow "Baratheon, Joffrey";
   Ned jencem w Krolewskiej Przystani; sojusze i pakty z korona znikaja. Log: `KONIEC PROLOGU ... Robert martwy TAK ... Joffrey krolem TAK ... Ned: jeniec w town_EW4 TAK`.
9. Dzien 188 = start opowiesci (data "Year 299, Day 1"); ok. 9 dni pozniej okno ROT "Execution of Ned Stark": Ned martwy (stracony), Robb wladca Polnocy, potem wojny
   ROT jak w klasycznym starcie. Log: `egzekucja Neda (NedEvent ...) | Ned martwy TAK | ... - Robb TAK`.
10. Zaciag u lorda (rozmowa ROT "enlist") dziala w prologu po 5 dniach jak w klasycznym starcie.

## Dla Jeffa

Zrobilem pierwszy kawalek wczesniejszego startu: przy nowej kampanii gra pyta, czy zaczac od prologu 298 (krol Robert wyrusza na polnoc), czy jak dotad w 299.
W prologu kalendarz zaczyna sie 188 dni wczesniej, Robert i Ned zyja i rzadza (Robert korona, Ned Polnoca), krolestwa Westeros nie wypowiadaja sobie wojen i trzymaja z
Robertem: Polnoc, Krainy Burzy i Dorzecze jako sojusznicy, reszta paktami o nieagresji z moda Diplomacy. W dniu 186 Robert umiera od ran z polowania, Joffrey zostaje
krolem, Ned trafia do lochu w Krolewskiej Przystani, a w dniu 188 rusza zwykla opowiesc ROT - egzekucja Neda (teraz naprawde go zabija) i Robb na tronie Polnocy.
Fabula ROT (wydarzenia, wojny fabularne, Inni) liczy sie od tego dnia, wiec nic z ROT nie ruszy za wczesnie. Sceny z ksiazki (okna wydarzen, orszak krola, turniej)
i postacie to nastepne etapy; tego nie bylo jeszcze w grze - najpierw autotest.
