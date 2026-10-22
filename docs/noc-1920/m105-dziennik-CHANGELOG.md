# m105-dziennik - dziennik wiadomosci na mapie obok menu osady, ramka w stylu ROT, bez wierszy handlu nad miastami (Armoury)

Galaz `w-toku/m105-dziennik` (baza 77c17f3c = n32 + m105-kolko). Jeff 21.10: "tak, rob dziennik". Tor M (jeden workflow z jedna recenzja).

**Dla Jeffa (prosto):** gdy na mapie masz otwarte menu osady albo spotkania ("You are waiting in The Eyrie"), wiadomosci (kruki, pojmania, wiesci) nie leza juz
na tym menu - przeskakuja na prawo od niego, ponizej srodka ekranu, nad napisem PAUSED. Na samej mapie (bez menu) dziennik stoi kawalek dalej od lewej krawedzi,
zeby nie zaslaniac sylwetki "Army Armory readiness" w rogu. Gdy otworzy sie jakies okno (rozmowa, pytanie, werbunek, zarzadzanie, encyklopedia, okno wykutego
przedmiotu, tabela turnieju, okno wydarzenia ROT), dziennik wraca w lewy dolny rog, jak w grze, i nie ma za nim ciemnego panelu - nie wchodzi na przyciski okna.
Za liniami jest ciemny panel z cienkimi srebrnymi belkami, jak w dymkach Realm of Thrones; panel ma wysokosc linii, ktore akurat widac, i znika z ostatnia linia.
Nad nazwami miast nie ma juz wierszy z portretem "Caravan of X Sold ..." / "Villagers of Y Sold ..." - handel dziala jak dotad, znikaja tylko te wiersze. Trzy
przelaczniki w MCM, grupa "Map news" (wszystkie domyslnie wlaczone, dzialaja od razu po zmianie, takze w zatrzymanym menu).

## Stan pracy

| Krok | Co | Stan |
|---|---|---|
| K1 | `Armoury/src/MapChatLog.cs` (nowy): przesuniecie, ramka, filtr handlu, linie logu, test autotestu; wpiecie w `SubModuleMain` (latka, takt, OnGameEnd) i `ArmouryBehavior` (doba); 3 klucze w `Settings.cs` + `McmSettings.cs` z gen_mcm | ZROBIONE (e591dc3f, build Armoury kod 0, 0 ostrzezen, gen_mcm Armoury 2278) |
| K2 | grupa `tools/sprawdz_logi.py --grupa m105dziennik` (DZ-0..DZ-5) + 8 logow syntetycznych | ZROBIONE (77f88bc4; sprawdz_narzedzie: dispatch poza main 0, zle wstawki 0, duble def 0, duble grup 0, kompilacja ok) |
| K3 | ten CHANGELOG (rozpoznanie, decyzje, linie, progi, ryzyko, plan autotestu) | ZROBIONE (8db0583b, 80d8583d) |
| K4 | poprawki po recenzji w kodzie (tabela nizej): okna bez flagi MapScreen, trzy pozycje, ramka tylko bez okien, klucze z MCM wprost, licznik prob, granice od wysokosci ekranu, Reset; `CraftPopup.IsOpenOn`; podpowiedzi MCM | ZROBIONE (01884554, build Armoury kod 0, 0 bledow, 0 nowych ostrzezen; gen_mcm Armoury 2278 - bez nowych ustawien) |
| K5 | poprawki po recenzji w `sprawdz_logi` (granica z linii, DZ-4 NIE bez pomiaru, DZ-4b wiazace) + 13 logow syntetycznych | ZROBIONE (6189cb05; sprawdz_narzedzie czysto) |
| K6 | ten CHANGELOG po recenzji | ZROBIONE (ten commit) |
| AT | autotest (plan nizej) - gry NIE uruchamialem, nic nie wgrane | DO ZROBIENIA |

## Recenzja i poprawki (K4, K5)

| Uwaga | Werdykt weryfikacji | Zrobione |
|---|---|---|
| gra R1: okno wyniku kucia (CraftPopup, warstwa 4500 na MapScreen bez flagi) - przesuniety dziennik 15300 z ciemnym panelem lezy na ok. 78% przycisku Done przez 10,5 s; to samo encyklopedia otwarta z menu i tabela turnieju (przycisk Done) | PRAWDA (ZLY STAN) | `NoWindows`: `EncyclopediaScreenManager.IsEncyclopediaOpen`, `CraftPopup.IsOpenOn(ms)` (warstwa naprawde na ekranie - `HasLayer`, nie samo `_layer != null`), `FindLayer<ScreenLayer>("MapTournamentLeaderboard")`; dziennik wraca na 27/32, po Done znowu obok menu (dodatkowe przesuniecia / powroty w liczniku doby - zgodnie z zamiarem). Ogolny warunek "jakakolwiek warstwa z IsFocusLayer" odrzucony (powiadomienia mapy ustawiaja go przy najechaniu mysza) |
| kontrakt R1: na mapie bez menu ramka (x 35..508) zaslania sylwetke "Army Armory readiness" moda DynamicTroopEquipment (x 8..92, y 868..1008) | PRAWDA | trzecia pozycja: mapa bez menu i bez okien - `MarginLeft 100` (ramka od x 108, tekst od x 115), dol z prefabu; pod kluczem `MapChatLogClearOfMenu` (sam tekst tez lezal na sylwetce); `int _pos` (gra / menu / mapa) zamiast `bool _moved`; proba "mapa": lewa krawedz (ramki albo tekstu) >= 100 przy kluczu |
| kontrakt R2: ramka widoczna przy oknie na mapie (a: rozmowa na mapie - panel wypowiedzi x 230..950; b: klucz przesuniecia NIE - ciemny panel na menu; c: okna nad menu) | PRAWDA (a, b); c NIEPEWNE co do skali | ramka tylko przy `NoWindows(ms) && (!ms.IsInMenu || _pos == menu)`; (c) zalatwia ten sam warunek (ucieczka, werbunek, zarzadzanie itd. sa w NoWindows) - zdjecie w planie autotestu; proba "mapa" i jej przerwanie uzywaja tego samego `NoWindows` |
| kontrakt R3: klucze MCM "na zywo" dzialaly dopiero po godzinie gry (McmSettings.Apply co godzine) | PRAWDA (DROBNE) | zmiana zachowania: `ReadKeys` 4 razy na sekunde w kampanii czyta `McmSettings.Instance` przez `McmKeys` (NoInlining; brak MCM / 3 bledy = `Settings.Current`); filtr tabliczek i linia doby z tej samej wartosci; teksty (linia kampanii "z MCM na zywo, co 0,25 s", Settings.cs) zgodne |
| kontrakt R4: autotest mogl przejsc bez pomiaru w menu (zero prob = INFO; licznik prob rosl przed pomiarem, przerwana proba zjadala miejsce) | PRAWDA | `_menuTests++` przy pomiarze (etap 1, tuz przed `Measure`); sprawdz_logi: brak prob w menu przy pliku testu i przesunieciach = NIE (log `09-bez-prob-menu`, takze log recenzenta - NIE) |
| kontrakt R5: granice PAUSED 885 i paska 1015 to stale dla 16:9 i UIScale 1 (1920x1200 = 1176 jednostek - falszywe NIE) | PRAWDA (DROBNE, u Jeffa bez skutku) | `hU = (_root.Bottom - _root.Top) / CustomScale` (zapas 1080 przy blednym odczycie), granice `hU - 195` i `hU - 65`, w linii testu `wysokosc hU` i `lewa krawedz x=`; sprawdz_logi bierze granice z linii (`granica PAUSED N`), stary format - 885 (log `10-ekran-16x10`: dol 971 <= 981 TAK) |
| kontrakt R6: `Reset()` nie zerowal licznikow doby (wczytanie zapisu w srodku doby - liczby w pierwszej linii nowej sesji, mozliwe falszywe DZ-2) ani `_movesLogged` / `_backsLogged` | PRAWDA (DROBNE) | `Reset()`: `MapChatLogStats.DayReset()`, zera linii przesuniec (menu, powroty, mapa), marginesy prefabu i ramka ukryta od razu bez licznika powrotow (bez tego pierwszy takt poza kampania dopisywal powrot do nowej sesji); potkniecia `St` zostaja (od startu gry) |
| dodatkowo | - | proba "mapa" przerwana (menu albo okno w 1,5 s) - do 3 podejsc co >= 30 s (dotad jedna, potem nigdy) |

## Problem

- Zrzut Jeffa 21.10: dziennik wiadomosci na mapie kampanii (lewy dolny rog: "A raven from Tyrosh, 3 days on the wing: ...", "X has been taken prisoner") lezy NA
  panelu menu osady ("You are waiting in The Eyrie") i zaslania jego tresc i przyciski.
- Nad nazwa osady (lista z portretami nad "Monton") stoja wiersze handlu karawan i wiesniakow ("Caravan of X Sold Jewelry x1 Cheese x1 ...", "Villagers of Driftwood
  Sold ...", "... Purchased ...") - Jeff: smietnik.

## Rozpoznanie (dekompilacja 1.4.8; pliki w scratchpadzie sesji `dziennik/`)

- **Dziennik** to film `SPChatLog` (Native/GUI/Prefabs/SPChatLog.xml) w warstwie globalnej `GauntletChatLogView` ("ChatLog", kolejnosc 15300; zestaw
  TaleWorlds.MountAndBlade.GauntletUI - Armoury nie ma do niego referencji). `ChatLogWidget`: `HorizontalAlignment Left`, `VerticalAlignment Bottom`, `MarginLeft 27`,
  `MarginBottom 32`, rozmiar `SizeX/SizeY` z VM (`BannerlordConfig.ChatBoxSize*`, u Jeffa 495x340; SizeY ustawia `SuggestedHeight`). Marginesy sa tylko stalymi
  atrybutami prefabu - `ChatLogWidget.OnUpdate/UpdateResize` ich nie rusza (zmienia tylko rozmiar przy przeciaganiu w trybie przegladania).
- Uklad w pionie (od dolu ChatLogWidget): pole wpisywania 40, `ScrollablePanelContainer` z `MarginBottom 65`, `MarginTop 5`, nad nim pasek 45 -> pole tekstu =
  SizeY - 115. `InnerPanel` (lista linii, `DataSource {MessageHistory}`) przy dole; dziecko = jeden wpis `MessageHistory` (ta sama kolejnosc; dodanie na koncu,
  `RemoveAt(0)` przy 250).
- **Widocznosc linii:** `MPChatLineVM.Alpha` = 1 przez 10 s, potem w 0,5 s do 0 (`HandleFading` z `MPChatVM.Tick` - takze przy pauzie); przy przegladaniu / pisaniu
  `ToggleForceVisible` daje 1 wszystkim liniom i gra pokazuje wlasne tlo `ChatLogBackground` (`IsVisible="@IsInspectingMessages"`).
- **Menu osady:** `GauntletMenuBaseView` laduje film `GameMenu` do warstwy "MapMenuView" (tej samej, ktora tworzy `GauntletMapBasicView`); `GameMenuParent`
  `MarginLeft 27`, szerokosc 493 (tryb zwykly i spotkania), ramka panelu ok. 25. `MapScreen.IsInMenu` = `_menuViewContext != null`; okna nad menu:
  `IsEscapeMenuOpened`, `IsInTownManagement`, `IsInRecruitment`, `IsInArmyManagement`, `IsInHideoutTroopManage`, `IsMarriageOfferPopupActive`,
  `IsHeirSelectionPopupActive`, `IsMapIncidentActive`, `IsInCampaignOptions`, `IsInBattleSimulation` (wszystkie publiczne), rozmowa
  (`ConversationManager.IsConversationInProgress`), pytania (`InformationManager.IsAnyInquiryActive`). Okna ROT (`EventScreen`) to osobny ekran - `TopScreen`
  przestaje byc `MapScreen`.
- **Warstwy na MapScreen bez flagi ekranu** (recenzja): encyklopedia (`GauntletMapEncyclopediaView`, warstwa "EncyclopediaBar" 310 na TopScreen; publiczne
  `MapScreen.EncyclopediaScreenManager.IsEncyclopediaOpen`), okno wyniku kucia Armoury (`CraftPopup`, `GauntletLayer` 4500 na `ScreenManager.TopScreen`; ROT
  nadpisuje prefab `NewCraftedWeaponPopup` - przycisk Done x 817..1103, y 799..881), tabela turnieju (`GauntletMenuTournamentLeaderboardView`, warstwa
  "MapTournamentLeaderboard" 206 przez `MenuViewContext.AddLayer` -> `MapScreen.AddLayer`; Done ok. y 791..874). Kazda lezy pod dziennikiem (15300 - sortowanie
  warstw po `InputRestrictions.Order`). Inne warstwy Armoury na MapScreen: tylko etykiety spalonych wsi (`MapVillageLook`) - nie okno.
- **Lewy dolny rog mapy** (recenzja): przycisk moda DynamicTroopEquipment "Army Armory readiness" (`MapArmoryReadinessPrefab`, UIExtenderEx obok `ListPanel MapBar`,
  84x140, Left/Bottom, offset 8/-72 -> x 8..92, y 868..1008; klikalny, z dymkiem). Na kadrze 4K z autotestu poza nim nic tam nie ma.
- **Wiersze handlu nad miastami** to NIE dziennik, tylko powiadomienia tabliczki osady: `SettlementNameplateNotificationsVM` (SandBox.ViewModelCollection),
  prywatna `OnCaravanTransactionCompleted(MobileParty, Town, List<(EquipmentElement,int)>)` - wiersz `CaravanTransactionNotificationItemVM`
  ("<nazwa partii> Sold/Purchased ..."), warunek `_settlement == town.Owner.Settlement`. Zdarzenie `OnCaravanTransactionCompletedEvent` wysylaja: gra
  (`SellGoodsForTradeAction` - tylko karawany), BK (`SellGoodsPatch` - **wiesniacy**, stad "Villagers of ... Sold") i nasze CaravanBulk / CaravanAmmo / CaravanGoods.
  Te same zdarzenie sluchaja: `TownFolk.OnCaravanTrade` (ArmouryBehavior:1601) i BK - zostaja bez zmian (latka jest na metodzie VM, nie na zdarzeniu).
  `OnItemSold` (inny typ wiersza) - nikt go nie wywoluje, bez zmian.
- Sprite'y ramki: `BlankWhiteSquare_9` i `General\TooltipHint\tooltip_frame` (kategoria `ui_group1`, `AlwaysLoad` w NativeSpriteData.xml).
- `Widget.Size/GlobalPosition` sa typu `System.Numerics.Vector2` (zestaw System.Numerics.Vectors - brak w referencjach Armoury): pomiary przez `Left/Top/Right/Bottom`
  (float, uklad wzgledem rodzica; suma po rodzicach = GlobalPosition po ukladzie). Dzieki temu `Armoury.csproj` bez zmian.
- Skala UI (`UIContext.UpdateScale`): wysokosc / 1080, przy proporcjach wezszych niz 16:9 x 0,98 dodatkowy mnoznik, `CustomScale` = skala x UIScale - wysokosc
  ekranu w jednostkach UI to 1080 tylko przy 16:9 i UIScale 1 (1920x1200: 1176).

## Decyzje (z projektu; sprawdzone w kodzie)

1. **Wszystko w C#, bez XML i bez UIExtenderEx** - jeden nowy plik `MapChatLog.cs` (+ jedna metoda `CraftPopup.IsOpenOn`). Odrzucone: wstawka UIExtenderEx (bledna
   zabiera caly dziennik, zdjecie = restart), kopia SPChatLog.xml (nie idzie za aktualizacjami gry), chowanie dziennika w menu (Jeff traci kruki), wlasna warstwa
   (najwiekszy zakres, ryzyko podwojnych linii), latka `GauntletChatLogView.OnTick` (co klatke), latka `InformationManager.DisplayMessage` (mala metoda - zawieszenie
   GC 19.10). **Zero nowych plikow do wgrania** (sam Armoury.dll); listy plikow GUI w `wgraj-v5.ps1` i `bieg-bitwa.ps1` bez zmian.
2. **Odbicie raz na proces:** `GauntletChatLogView.Current` (statyczna), `_dataSource`; typ VM z typu pola, typ linii z argumentu `MBBindingList<>` (bez szukania
   zestawow po nazwie). Brak czlonu = BRAK w linii start/kampania; przesuniecie potrzebuje tylko warstwy, ramka - pol VM.
3. **Trzy pozycje** (klucz `MapChatLogClearOfMenu`), tylko na MapScreen i bez okien (`NoWindows`: flagi MapScreen wyzej + encyklopedia + okno wyniku kucia +
   tabela turnieju + rozmowa + pytanie):
   - **menu** (IsInMenu): `MarginLeft 560` (27 + 493 + 25 + 15), `MarginBottom 150` (dol tekstu y 865, dol ramki y 875 nad PAUSED y ok. 895..925 - zrzut autotestu
     18.10). Menu zwiniete przyciskiem - dziennik zostaje po prawej (tam tez jest wolne miejsce);
   - **mapa** (bez menu): `MarginLeft 100` (sylwetka DTE do x 92 + odstep), dol z prefabu (dol ramki y 993 nad paskiem 1015; PAUSED od x ok. 900);
   - **gra** (kazdy inny stan: inny ekran, bitwa, okno, klucz NIE, wyjscie z kampanii): wartosci z prefabu odczytane z widzetu przy znalezieniu (27/32).
   Bez animacji.
4. **Ramka:** kontener `ArmChatLogFrame` jako dziecko 0 `ChatLogWidget` (pod tekstem): StretchToParent w poziomie, Fixed w pionie, Bottom, marginesy L8 / R14 / B55,
   bez zdarzen, ukryty na start; tlo `BlankWhiteSquare_9` `#000000CC` (Top/Bottom 6), belki 12 px `tooltip_frame` (Extend 4, Saturation -100, ColorFactor 0.5).
   Wysokosc = suma wysokosci ostatnich k dzieci `InnerPanel` (k = linie z Alpha > 0.01 od konca historii) / CustomScale + 2 x 10, najwyzej SizeY - 95. Gdy liczba
   dzieci != liczba wpisow (lista w przebudowie) - stan z poprzedniego taktu. Widoczna: klucz, `TopScreen is MapScreen`, `NoWindows`, w menu tylko przy pozycji
   "menu" (klucz przesuniecia NIE - menu bez ciemnego panelu), k > 0, gracz nie przeglada i nie pisze. Bitwy, inne ekrany i okna - wyglad gry. Ramka powstaje przy
   znalezieniu widzetu (albo pozniej, gdy klucz wlaczono w sesji).
5. **Takt 4 razy na sekunde** z `SubModuleMain.OnApplicationTick` (przed returnem wiosek, `ChatLogTick` NoInlining, licznik `_chatLogFails` - 3 bledy ladowania typu
   = dziennik jak w grze do konca sesji). Wlasciwosci widzetow tylko przy zmianie. Widzet szukany od nowa, gdy odpadl od korzenia warstwy (film przeladowuje sie
   w menu glownym); po nieudanym szukaniu przerwa 5 s. Poza taktem tylko dwa liczniki czasu.
6. **Filtr handlu:** prefiks Harmony na `SettlementNameplateNotificationsVM.OnCaravanTransactionCompleted` (pole `_settlement` wstrzykiwane przez Harmony
   `____settlement`, przed latka sprawdzone `AccessTools.Field`; po Patch sprawdzone `GetPatchInfo`). false = wiersz nie powstaje. Liczymy tylko tam, gdzie wiersz
   naprawde by powstal (`_settlement == town.Owner.Settlement`), wiesniacy (`IsVillager`) osobno. Ukrywa takze karawany gracza (jedna zasada). Latka zakladana
   w `OnBeforeInitialModuleScreenSetAsRoot` (po DistrictIcon - ten sam blok, te same typy SandBox.ViewModelCollection dzialaja).
7. **Klucze z MCM wprost** (recenzja R3): `ReadKeys` w takcie (4 razy na sekunde, tylko w kampanii) czyta `McmSettings.Instance` przez osobna metode NoInlining;
   brak MCM albo 3 bledy odczytu = `Settings.Current` (potkniecia liczone). Zmiana w MCM dziala od razu, takze w zatrzymanym menu (dotad czekala na godzine gry -
   `McmSettings.Apply`). Filtr tabliczek i linia doby biora te sama wartosc.

## Klucze MCM (grupa "Map news", wszystkie `RequireRestart = false`, z MCM wprost co 0,25 s w kampanii)

- `MapChatLogClearOfMenu` = TAK - "Chat Log Clear Of Menus" (pozycje menu i mapy). NIE = dziennik w rogu i na menu, jak dotad.
- `MapChatLogFrame` = TAK - "Chat Log Frame On The Map" (ramka). NIE = goly tekst, jak dotad.
- `MapHideTradeNotices` = TAK - "Hide Trade Notices Over Towns" (wiersze handlu nad miastami). NIE = widoczne, jak dotad.
- Armoury.json Jeffa nie ma tych kluczy (nowe) - domyslne TAK zadzialaja. gen_mcm: Armoury 2278 ustawien (+3 w K1, K4 tylko podpowiedzi), RC 115, GT 44 bez zmian.

## Linie logu Armoury (prefiks `Dziennik (m105): `)

- `start - latka tabliczek (OnCaravanTransactionCompleted) TAK|BRAK (<powod>), widok dziennika (GauntletChatLogView) TAK|BRAK, pola MPChatVM TAK|BRAK.` (start gry)
- `kampania - klucze MapChatLogClearOfMenu=TAK, MapChatLogFrame=TAK, MapHideTradeNotices=TAK (z MCM na zywo, co 0,25 s), widok dziennika TAK, pola MPChatVM TAK,
  latka tabliczek TAK, test autotestu TAK|NIE.` (pierwszy takt na mapie w kampanii; przy BRAK ponowne odbicie)
- `widget dziennika znaleziony (N. raz; ChatLogWidget, marginesy 27/32, rozmiar 495x340, skala 2.00, InnerPanel TAK, pole tekstu TAK), ramka dodana TAK|NIE (<powod>).`
  oraz `ramka dodana pozniej (klucz MapChatLogFrame wlaczony w sesji) TAK|NIE (...).`
- Pozycje (po 3 pierwsze w sesji kazdego rodzaju; od nowa po wyjsciu z kampanii): `przesuniety obok menu (MarginLeft 100 -> 560, MarginBottom 32 -> 150).`,
  `wrocil spod menu na miejsce mapy (obok sylwetki armii) (MarginLeft 560 -> 100, MarginBottom 150 -> 32).` / `wrocil na miejsce gry (MarginLeft 560 -> 27, ...).`,
  `przesuniety obok sylwetki armii (mapa bez menu) (MarginLeft 27 -> 100, MarginBottom 32 -> 32).`
- Doba (ArmouryBehavior, po RoadsideInns.Daily): `dzien N - przesuniecia X, powroty Y, ramka Z, handel na tabliczkach: filtr TAK|NIE, ukryte A (karawany a1,
  wiesniacy a2), przepuszczone B, potkniecia S.` (liczby za dobe - przesuniecia = wejscia obok menu, powroty = wyjscia spod menu; potkniecia od startu gry; "ramka" =
  ile razy sie pokazala; wyjscie z kampanii zeruje liczby doby).
- Bledy: `ERROR in MapChatLog.<miejsce> (potkniecie n)` (3 pierwsze i co 200, nic sie nie wylacza; m.in. `ReadKeys (MCM, proba n/3)`), `ERROR in MapChatLog: nie
  laduje sie (proba n/3)` (typ).
- Test: `test - start proby nr N (menu|mapa): 3 linie wyslane, pomiar i zdjecie za 1,5 s.` (chwila na zapasowy zrzut PowerShellem), po 4,5 s
  `test - menu|mapa: linie testowe 3/3 (widocznych k), dziennik x .. y .., tekst x .. y .., menu x .. y .. (z ramka 27)|brak, zachodzenie 0 px, ramka TAK (x .. y ..,
  linii k)|NIE (klucz ..), dol ramki y=875 (granica PAUSED 885, pasek 1015, wysokosc 1080), lewa krawedz x=568, zdjecie zdjecia\dziennik-m105-<czas>-<n>.png - TAK.|
  - NIE (<powody>).` albo `test - bez proby (<powod>).` Powody NIE: `menu nie znalezione`, `zachodzenie N px`, `dol ramki ponizej granicy PAUSED`, `dol ramki na
  pasku mapy`, `lewa krawedz x=N < 100 (sylwetka armii)` (mapa przy kluczu przesuniecia), `ramka niewidoczna`, `linii testowych widac n/3`, `brak pliku zdjecia`.

## Progi `tools/sprawdz_logi.py --grupa m105dziennik <Armoury-*.log>`

- **DZ-0** linie start i kampania (widok, pola MPChatVM, latka tabliczek TAK); **DZ-0b** widget znaleziony >= 1 i ramka dodana TAK (przy kluczu ramki).
- **DZ-1** `ERROR in MapChatLog` = 0. **DZ-3** potkniecia (maksimum z linii doby) = 0.
- **DZ-2** filtr: latka TAK; doby z filtrem TAK - przepuszczone 0 (przy zerze ukrytych) i suma ukrytych > 0; doby NIE - ukryte 0 i suma przepuszczonych > 0; doba
  z obiema liczbami > 0 = zmiana klucza w dobie (DZ-2b INFO); zero zdarzen = INFO (zadna tabliczka z handlem w zasiegu).
- **DZ-4** proby w menu >= 1, kazda: zachodzenie 0, ramka TAK (klucz ramki TAK), dol ramki <= granica PAUSED z linii testu (stary format bez granicy - 885), wynik
  TAK; brak prob = INFO, **ale przy pliku testu i przesunieciach obok menu = NIE** (autotest nie zmierzyl glownej zmiany).
- **DZ-4b** proby na mapie: wynik TAK (lewa krawedz >= 100 przy kluczu przesuniecia, ramka TAK przy kluczu, dol ramki nad paskiem, linie 3/3) - wiazace (dotad INFO).
- **DZ-5** przy MapChatLogClearOfMenu TAK przesuniecia >= 1 (test autotestu TAK i zero = NIE; bez pliku testu = INFO).
- Logi syntetyczne (scratchpad `dziennik/syntetyczne`, generator `gen_logi_dz.py` v2 z tymi samymi sklejeniami napisow co MapChatLog.cs): 01 dobry (mapa + menu)
  8 progow 0 NIE; 02 zly - NIE w DZ-1, DZ-2, DZ-3, DZ-4; 03 stary log - INFO; 04 klucze NIE - 0 NIE (DZ-4, DZ-5 INFO); 05 latka BRAK + bez przesuniec - NIE w DZ-0,
  DZ-0b, DZ-2, DZ-5; 06 zero handlu - DZ-2 INFO, 0 NIE; 07 zmiana klucza w dobie - DZ-2b INFO, 0 NIE; 08 test bez ramki - DZ-4 NIE; 09 plik testu i przesuniecia bez
  pomiaru w menu - DZ-4 NIE; 10 ekran 16:10 (granica 981, dol 971) - 0 NIE; 11 proba mapy na sylwetce (lewa krawedz 35) - DZ-4b NIE; 12 stary format linii testu
  (bez granicy) - 0 NIE; 13 log recenzenta bez prob w menu - DZ-4 NIE. Log Jeffa sprzed galezi (10-06-55) - INFO, 0 NIE. `sprawdz_narzedzie`: dispatch poza main 0,
  zle wstawki 0, duble def 0, duble grup 0, kompilacja ok.

## Ryzyko / co sprawdzic (kontrola calosci)

- **Regresje:** marginesy dziennika zmieniamy tylko na MapScreen bez okien przy kluczu; w kazdym innym stanie (inny ekran, bitwa, okno, klucz NIE, wyjscie z kampanii)
  wracaja wartosci prefabu. Odpadly widzet dostaje marginesy prefabu od razu. Ramka to dodatkowe dziecko bez zdarzen (DoNotAcceptEvents
  + DoNotPassEventsToChildren) - nie zabiera klikniec ani przewijania; sciezki ChatLogWidget (ScrollablePanel, InnerPanel, `..\ChatLogWidget` w szablonie linii) ida po
  Id, nie po indeksie, wiec wstawka na indeks 0 ich nie przestawia. W bitwach, na innych ekranach i przy oknach ramka ukryta.
- **Pozycja mapy (nowa po recenzji):** dziennik na mapie zawsze 73 jednostki dalej od lewej (takze bez moda DTE - nieszkodliwe). Kazde okno na mapie bez menu
  (pytanie, rozmowa, zarzadzanie armia) przestawia go na 27 i z powrotem na 100 - bez licznika, w logu 3 pierwsze. Przy przegladaniu historii (klawisz dziennika)
  tlo gry `ChatLogBackground` tez stoi od x 100.
- **Kolizje:** nikt w repo nie rusza SPChatLog / ChatLogWidget / MPChatVM (grep); latka na VM tabliczek, nie na zdarzeniu - TownFolk.OnCaravanTrade, ksiegi i BK dostaja
  handel jak dotad; nasze CaravanBulk/CaravanAmmo/CaravanGoods nadal wysylaja zdarzenie "jak BK" - ich wiersze nad miastem tez znikna (zgodnie z zasada). DistrictIcon
  (ten sam VM-owy zestaw, inna klasa i metoda) bez zwiazku. AIInfluence (wylaczony) latuje inne metody VM tabliczek. Linie logu nie wpadaja w tematy Log.TopicOf ani
  w ciche logi (prefiks nieuzywany). Nic do zapisu gry, zadnych liczb gospodarki. `CraftPopup` - tylko nowy odczyt (`IsOpenOn`), zachowanie okna bez zmian.
- **Wydajnosc:** co klatke 2 dodawania; 4 razy na sekunde - odczyt 3 kluczy z MCM (slownik ustawien MCM), kilka odczytow odbiciem (bool, lista, Alpha ostatnich linii),
  flagi MapScreen, przejscie po liscie warstw MapScreen (`FindLayer`, `HasLayer` - kilkanascie warstw) i porownania; szukanie widzetu (rekurencyjne) tylko przy
  znalezieniu / przeladowaniu filmu (po porazce co 5 s); przejscie po rodzicach do 32 krokow.
- **Rozdzielczosci:** wszystko w jednostkach UI (ta sama skala dla dziennika i menu) - 1080p/1440p/4K dadza ten sam uklad. Szersze niz 16:9: menu i dziennik przy lewej
  krawedzi, PAUSED dalej w prawo. Wezsze niz 16:9 i UIScale != 1: wiecej / mniej jednostek wysokosci - pomiar autotestu bierze granice od wysokosci (R5); UIScale > 1
  zmniejsza wysokosc - pelna ramka moze siegnac osady w srodku kadru (u Jeffa 4K 16:9, UIScale 1).
- **Rozmiar dziennika:** max szerokosc 650 -> prawa krawedz x 1210 (w menu), 750 (na mapie); wysoki dziennik (do 470) przy wielu liniach siega wyzej. Rozmiaru nie
  zmieniamy (SizeY z VM i zapisu).
- **Okienka nad menu**, ktorych nie lapie `NoWindows` (np. powiadomienie ze scena, panel kodow, menu kontekstowe partii), moga nachodzic na przesuniety dziennik -
  warstwa dziennika (15300) i tak lezala nad nimi w starym miejscu. Ogolny warunek "jakakolwiek warstwa z IsFocusLayer" odrzucony (najechanie mysza na powiadomienie
  mapy tez go ustawia - dziennik skakalby). Do zobaczenia w grze Jeffa.
- **Drobne:** opoznienie do 0,25 s (przeskok, zmiana ramki, klucz MCM); gorna belka nachodzi ok. 2 px na pierwsza linie; przy pisaniu w przesunietym dzienniku pole
  wpisywania (y 890..930) moze nachodzic na PAUSED (w grze jednoosobowej praktycznie nie wystepuje).
- **Aktualizacja gry:** inna nazwa metody / pola / Id widzetu = BRAK w linii start/kampania albo "widget ... nie znaleziony" (brak linii), dziennik jak w grze, bez wywrotki.
  Inna nazwa warstwy turnieju = dziennik obok menu nad tabela (jak przed recenzja), bez wywrotki.
- **Gdyby Jeff dalej widzial "Sold/Purchased" w samym dzienniku** - to inne zrodlo niz tabliczki; nastepny krok: postfiks `MPChatVM.IsMessageIncluded` (bez latania
  `DisplayMessage`).
- **Wgranie:** sam `Armoury.dll` (build na 6189cb05: md5 3a1e4cd8; DLL zawiera hash commitu w wersji - md5 liczyc z buildu przy wgraniu). Nowych plikow
  GUI/ModuleData BRAK - w `wgraj-v5.ps1` i `bieg-bitwa.ps1` nic do dopisania.

## Plan autotestu (gry nie uruchamialem)

1. Plik `Documents\Mount and Blade II Bannerlord\CrashScribe\armoury-dziennik.flag` (pusty); DLL autotestu CrashScribe (LogGate.Mode 1/2).
2. `bieg-bitwa.ps1 -DllDir <worktree>\Armoury\bin\Release -CsAt <CrashScribe AT> -Tree <worktree> -Out <plik> -Days 4` (bez `-Battles` - zmiana tylko na mapie).
   Gracz musi postac w menu miasta >= 20 s po przesunieciu (pomiar w menu; bez niego DZ-4 = NIE).
3. Usunac plik flag; `python -I tools/sprawdz_logi.py --grupa m105dziennik <Armoury-*.log>` - oczekiwane 0 NIE (DZ-2 moze byc INFO, gdy w zasiegu tabliczek nie bylo handlu).
4. Linie w logu: `start` (latka TAK, widok TAK, pola TAK), `kampania ... (z MCM na zywo, co 0,25 s) ... test autotestu TAK`, `widget dziennika znaleziony ... ramka
   dodana TAK`, `przesuniety obok menu (MarginLeft 27|100 -> 560, MarginBottom 32 -> 150)`, `test - start proby nr 1 (menu)` i po ok. 4,5 s `test - menu: linie
   testowe 3/3 ... zachodzenie 0 px, ramka TAK ... dol ramki y=875 (granica PAUSED 885, pasek 1015, wysokosc 1080), lewa krawedz x=568 ... - TAK.` (do 2 pomiarow
   co >= 120 s); jesli bieg byl na mapie bez menu - `przesuniety obok sylwetki armii (mapa bez menu) (MarginLeft 27 -> 100 ...)` i `test - mapa: ... dol ramki
   y=993 ... lewa krawedz x=108 ... - TAK.`; doby `dzien N - ... filtr TAK, ukryte A ... przepuszczone 0, potkniecia 0.`; brak `ERROR in MapChatLog`.
   **Do potwierdzenia z linii** (gra R1): `rozmiar 495x...` w linii widgetu - rzeczywisty `SizeX` dziennika (`@ChatBoxSizeX` moze byc zapisany przez Jeffa;
   przy 425 prawa krawedz ramki w menu x 971 - i tak bylaby nad przyciskiem Done, stad powrot przy oknie kucia).
5. Zdjecia `CrashScribe\zdjecia\dziennik-m105-<czas>-<n>.png` (zmniejszyc PowerShellem do 1920x1080 albo 960x540):
   - **z menu:** panel "You are waiting in X" w calosci czysty; 3 linie testowe na prawo od niego (przy 4K x ok. 1120..2110 px), ponizej srodka ekranu; za liniami ciemny
     panel z cienkimi szarymi belkami u gory i u dolu, przylegajacy do linii (nie pusty duzy prostokat); panel konczy sie nad PAUSED i nad srodkiem paska z data;
     portrety u gory i tabliczka miasta w srodku niezasloniete; nad nazwa miasta brak wierszy z portretem "Sold/Purchased" (slaby dowod - lista bywa pusta;
     prawdziwy dowod to licznik ukrytych w DZ-2);
   - **z mapy** (jesli powstalo): dziennik w lewym dolnym rogu, ale z odstepem - ramka zaczyna sie na prawo od sylwetki "Army Armory readiness" (przy 4K od x ok.
     216 px; sylwetka do x ok. 184 px) i jej nie zaslania; dol ramki nad lewymi przyciskami paska (y < 1015).
6. **Niepewne - do zobaczenia w grze (reczny zrzut glownej sesji albo zdjecie z gry Jeffa):**
   - **okno nad menu** (recenzja R2c; ucieczka, werbunek, zarzadzanie miastem / armia): dziennik w lewym dolnym rogu, bez ciemnego panelu, nie wchodzi na przyciski
     okna (oczekiwane po NoWindows; skala problemu przed poprawka niepewna);
   - **koniec roboty w kuzni w menu** (gra R1; menu czekania przy robocie -> `Forge.Finish` -> okno wyniku kucia): dopoki okno jest otwarte, dziennik na 27/32
     (linia "wrocil na miejsce gry" w logu, jesli w pierwszych 3), przycisk Done czysty; po Done dziennik znowu obok menu. Przy okazji: czy wczytany jest prefab ROT
     `NewCraftedWeaponPopup` (lista 330/330/220/180), a nie SandBox;
   - **encyklopedia otwarta z menu** i **tabela turnieju** w menu miasta z turniejem: dziennik w rogu, przycisk Done tabeli czysty;
   - **rozmowa na mapie** (recenzja R2a): bez ciemnego panelu na poczatku tekstu postaci;
   - **zmiana klucza w MCM w zatrzymanym menu** (R3): przeskok dziennika / znikniecie ramki w ciagu 0,25 s po powrocie z MCM, bez czekania na godzine gry.
7. Zapas: gdy zdjecie silnika nie powstalo ("brak pliku zdjecia"), glowna sesja robi zrzut `[System.Drawing.Graphics]::CopyFromScreen` w chwili linii
   `test - start proby` (+1,5 s).

**Status:** DO SPRAWDZENIA - kod gotowy (build kod 0, 0 bledow), poprawki po recenzji zrobione, autotest nie wykonany, nic nie wgrane.
