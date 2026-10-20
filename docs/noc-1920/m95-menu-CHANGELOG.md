## m95-menu - porzadek menu miasta: karczma, kuznia, praca i sprawy miasta w podmenu (Armoury)

**Dla Jeffa (prosto):** menu miasta jest teraz krotkie - zostaja w nim miejsca (keep, arena, tavern district, port, trade, recruit) i trzy nowe
wejscia: **Go to the smithy**, **Look for paid work**, **Town affairs**. Nocleg ("Take lodgings at an inn") i informatorzy sa w **Go to the tavern
district**, a wszystko o kuzni (kuznia gry, nasza kuznia, naprawa u kowala, zbrojownia, Qohor) w **Go to the smithy**, z krotkim opisem kazdej opcji
na gorze. Kazda opcja dziala jak dotad i pokazuje sie w tych samych warunkach - zmienia sie tylko miejsce. Wylacznik: MCM Armoury -> "The town menu" ->
**Town Menu Tidy** (wylaczony = menu jak dzis, po wczytaniu zapisu).

**Problem:** Jeff 20.10 (zrzut menu Seagard, doslownie): "czy nie mozemy jakos posegregowac te opcje w miescie i ogolnie, jest tak duzo roznych
opcji, np. spanie w tawernie, powinno byc go to inn district i wszystko tam powinno byc. Forge powinno byc w forge i wchodze do zakladki forge czy tam
smith i wszystko mam tam wyjasnione i po kolei. Trzeba to uporzadkowac." Na zrzucie ok. 20 opcji w jednym menu (przewijane), w kolejnosci przypadkowej
(kolejnosc rejestracji modow i ich indeksow), m.in. trzy rozne "kowalskie" opcje z trzech modow porozrzucane po liscie.

**Przyczyna (sprawdzone w dekompilacji, nie zgadywane):** kazdy mod dokleja swoje opcje do menu gry `town` (`CampaignGameStarter.AddGameMenuOption`
-> `GameMenu.AddOption`, lista `GameMenu._menuItems`, wstawianie wedlug indeksu w chwili rejestracji). Zrodla opcji z zrzutu:

| Opcja (napis) | id opcji | Mod / klasa | Warunek (skrot) | Skutek / powrot |
|---|---|---|---|---|
| Go to the port | `port` | NavalDLC NavalTransitionCampaignBehavior | port osady | menu portu |
| Banner Kings | `bannerkings_submenu` | BK BKSettlementActions | osada z ludnoscia BK | menu `bannerkings`, Back -> `town`/`castle` |
| People of this land | `arm_people_town` | Armoury PeopleMenu | klucz People Menu Entry | okno (ShowInquiry), bez zmiany menu |
| Visit the local blacksmith to repair party equipment | `repair_equipment_town` | RealisticBannerlord EquipmentRepairBehavior | RB Equipment wlaczone; nieaktywna przy 0 kosztu / braku zlota | placi, `SwitchToMenu("town")` |
| Contact local informants | `espionage_contact_informants` | RealisticBannerlord EspionageMenuBehavior | RB Espionage; cudze miasto | okna rozmowy, bez zmiany menu |
| Manage Armory | `army_armory_manage` | DynamicTroopEquipmentReupload ArmyArmoryBehavior | zawsze | menu `army_armory_submenu`, Back -> `town` |
| Hire yourself out for day labour | `rc_work_labour_town` | RealisticCaptivity Work | warunki RC | menu oczekiwania, koniec -> `town` |
| Stand the night watch for pay | `rc_work_guard` | RealisticCaptivity Work | warunki RC | menu oczekiwania, koniec -> `town` |
| Buy a house in this place | `rc_home_buy_town` | RealisticCaptivity Homes | brak domu tutaj | okno kupna -> `rc_home` |
| Work the forge | `arm_enter` | Armoury SmithMenu | warunki kuzni | menu `armoury_forge` |
| Take lodgings at an inn | `arm_inn_enter` | Armoury TownInns | klucz Town Inns, miasto | menu karczmy |
| Realm ledger | `arm_realm_ledger_town` | Armoury DailyLife.View | klucz Daily Life Estates | okno, bez zmiany menu |
| Estates and livelihood | `arm_estates_town` | Armoury DailyLife.View | jw. | okno, bez zmiany menu |
| Living Economy | `bee_town_root` | BetterEconomy SettlementMenuBehavior | miasto | menu `bee_town_menu`, Back -> `town` |
| Go to the keep / arena / tavern district | `town_keep` / `town_arena` / `town_backstreet` | gra PlayerTownVisitCampaignBehavior | dostep do miejsca | podmenu gry |
| Recruit troops / Trade / Enter smithy | `recruit_volunteers` / `trade` / `town_smithy` | gra | dostep (SettlementAccessModel) | ekran gry |
| (nizej, przewijane) Wait here, Return to Army, Leave, Take a walk, Manage town, Enter the warehouse, Go to alley | `town_wait`, ... | gra / SandBox | - | - |
| (tylko w niektorych miastach / sytuacjach) Seek out the masters of Qohor, Visit the Iron Bank, Go to your house, Borrow from / Repay the town council, Proclaim a tourney, Hand kit to the garrison | `arm_qohor_enter`, `arm_iron_bank_enter`, `rc_home_enter_town`, `arm_townloan_borrow_town` / `_repay_town`, `gt_proclaim_tourney`, `arm_garrison_kit_town` | Armoury / RC / GT | Qohor, Braavos, wlasny dom, rada miasta, wlasne miasto | - |
| (zadania) Stop the Red Wedding, rozmowa z dowodca zalogi, Hand over the revenue | `rescue_robb_stark`, `talk_to_garrison_commander_town`, `talk_to_steward_for_revenue_town` | ROT / gra (zadania) | stan zadania | - |

**Zmiana:** nowy plik `Armoury/src/TownMenus.cs`, nowy klucz MCM **Town Menu Tidy** (`Settings.TownMenuTidy`, grupa "The town menu", domyslnie
wlaczony; MCM Jeffa tego klucza nie ma - nic do zmiany w `Armoury.json`). Raz na sesje, w `SubModuleMain.OnAfterGameInitializationFinished`
(po `OnSessionLaunched` i `OnAfterSessionLaunched` WSZYSTKICH zachowan - `Campaign.DoLoadingForGameType` -> `MBGameManager` -> moduly; a PRZED
pokazaniem pierwszego menu - `SandBoxGameManager.OnLoadFinished` otwiera menu z zapisu dopiero potem) opcje z listy ponizej sa przenoszone z menu
`town` jako TE SAME obiekty `GameMenuOption` (id, napis, warunek, skutek bez zmian) - nie ma kopii ani nowych warunkow. Nasze trzy "wyjscia" (karczma
"Back to the town", kuznia "Wipe your hands and step out", Qohor "Leave") wracaja do podmenu, z ktorego gracz przyszedl (`TownMenus.BackTo`);
cudze wracaja jak dotad do `town` (sprawdzone: RB, DTE, BEE, BK, RC wracaja `SwitchToMenu("town")` albo do wlasnego menu - nic nie zaklada menu
nadrzednego, zaden warunek nie patrzy na id biezacego menu).

Nowy uklad (opcja -> nowe miejsce):

| Opcja | Skad | Nowe miejsce |
|---|---|---|
| Take lodgings at an inn | Armoury | **Go to the tavern district** (podmenu gry `town_backstreet`, przed "Back to town center") |
| Contact local informants | RB | **Go to the tavern district** |
| Enter smithy (kuznia gry, crafting) | gra | **Go to the smithy** (nowe podmenu `arm_tm_smithy_menu`) |
| Work the forge | Armoury | **Go to the smithy** |
| Visit the local blacksmith to repair party equipment | RB | **Go to the smithy** |
| Manage Armory | DTE | **Go to the smithy** |
| Seek out the masters of Qohor (tylko Qohor) | Armoury | **Go to the smithy** |
| Hire yourself out for day labour | RC | **Look for paid work** (nowe podmenu `arm_tm_work_menu`) |
| Stand the night watch for pay | RC | **Look for paid work** |
| People of this land | Armoury | **Town affairs** (nowe podmenu `arm_tm_affairs_menu`) |
| Estates and livelihood | Armoury | **Town affairs** |
| Realm ledger | Armoury | **Town affairs** |
| Living Economy | BEE | **Town affairs** |
| Banner Kings | BK | **Town affairs** |
| Buy a house in this place | RC | **Town affairs** |
| Borrow from / Repay the town council | Armoury | **Town affairs** |
| Proclaim a tourney | GT | **Town affairs** (nie arena: arena jest zamknieta noca - opcja by znikala) |
| Hand kit to the garrison | Armoury | **Town affairs** (nie keep: wejscie do keepu bywa zablokowane) |
| Go to the port, keep, arena, tavern district, Recruit troops, Trade, Wait, Leave, Take a walk, Manage town, warehouse, alley, Go to your house, Visit the Iron Bank, opcje zadan | gra / NavalDLC / RC / Armoury / ROT | **zostaja w menu miasta** (miejsca i zadania) |

- Trzy nowe wejscia staja w menu miasta w miejscu dawnego "Enter smithy" (za "Trade"); wejscie jest widoczne, gdy choc jedna opcja w srodku jest
  widoczna (warunki opcji wolane jak w grze na swiezym `MenuCallbackArgs`), znaczniki zadan "!" opcji (np. zamowienia w kuzni gry) przechodza na
  wejscie, podpowiedz wejscia wymienia opcje widoczne w srodku. Kazde podmenu ma opis: zdanie na kazda opcje widoczna w tej chwili, i "Back to town center".
- Karczma i informatorzy ida do podmenu gry `town_backstreet`, bo jego wejscie jest zawsze dostepne (`DefaultSettlementAccessModel.CanMainHeroGoToTavern`
  = true; ROT i NavalDLC przekazuja dalej, BK tego nie latal) - nic nie moze przez to zniknac.
- Ochrona: przygotowanie (nowe podmenu, listy) przed ruszeniem menu miasta; przy bledzie w trakcie przenoszenia listy `town` i `town_backstreet` wracaja
  z kopii (menu jak dotad). Drugie wolanie w tej samej sesji - nic. Wyjatek w cudzym warunku przy liczeniu wejscia - licznik per opcja, opcja liczona
  jako widoczna (nic nie znika), nic sie nie gasi.
- Linia logu Armoury przy kazdym starcie sesji (nowa kampania i wczytanie): `Menu miasta (m95-menu): start | WLACZONY ... przeniesione N z 20 w planie:
  do karczmy (town_backstreet) 2 [...]; arm_tm_smithy_menu 4 [...]; ... | wejscia podmenu wpiete 3 (...) | nie znaleziono cudzych K [...], naszych M [...]
  | menu miasta teraz X opcji: <id po kolei>`. Wylaczony klucz: `... WYLACZONY w MCM (Town Menu Tidy) - menu miasta jak dotad`.
- Zamek (`castle`) i wies - bez zmian. Menu zamku ma tych samych gosci (BK, RB "castle smith", BEE, People, Realm ledger, Estates, pozyczka, sprzet zalogi),
  ale jest krotsze (ok. 15-18 opcji); jesli Jeff zechce, ta sama klasa moze dostac drugi plan dla zamku ("Castle affairs") - osobny wpis.

**Ryzyko / co sprawdzic (kontrola calosci):**
- Kolejnosc: przenoszenie przed pierwszym pokazaniem menu - VM menu (`GameMenuVM`) trzyma opcje po indeksie, wiec zmiana listy przy otwartym menu bylaby
  niebezpieczna; tu jej nie ma (sprawdzone: `SandBoxGameManager.OnLoadFinished` -> `SetNextMenu`/`MapState.OnLoadingFinished` po `DoLoadingForGameType`).
- Opcje, ktore cudze mody dodaja PO starcie sesji (zadania gry przez `QuestBase.AddGameMenuOption`, `OnGameLoadFinished`, AIInfluence - zaciemniony,
  nie dalo sie zdekompilowac), zostaja w menu miasta tam, gdzie je mod wstawil - nie gina. Linia startu wypisuje cale menu miasta po zmianie.
- Zapis zrobiony WEWNATRZ nowego podmenu i wczytany z WYLACZONYM kluczem: po poprawkach recenzji trzy podmenu sa rejestrowane zawsze, wiec zapis otwiera
  puste podmenu (opis + "Back to town center" -> menu miasta). Bez Armoury (mod usuniety) - gra sama konczy spotkanie (`PlayerEncounter.Finish`, druzyna
  na mapie przy miescie), jak przy kazdym usunietym modzie z wlasnym menu.
- Cudze powroty (naprawa RB po zaplacie, Back z DTE / BEE / BK, koniec pracy RC) laduja w glownym menu miasta, nie w podmenu - jak dotad.
- Koszt: warunki opcji w podmenu sa liczone przez wejscie tak jak dotad przez menu miasta (te same wywolania, raz na odswiezenie menu) + raz przy
  otwarciu podmenu dla opisu. Bez stanu w zapisie, bez latek Harmony (tylko odczyt prywatnego pola `GameMenu._menuItems` przez AccessTools).
- Zmiana klucza w trakcie gry dziala od nastepnego wczytania; warunki wejsc nie patrza na klucz, wiec wylaczenie w trakcie gry niczego nie chowa.
- Build: Armoury i RealisticCaptivity `dotnet build -c Release` kod 0 (0 bledow). Gry nie uruchamiano (Jeff gra) - zachowanie w grze NIESPRAWDZONE.

**Status:** DO SPRAWDZENIA (galaz w-toku/m95-menu; do WGRANIA 95 po autotescie).

**Test w grze krok po kroku (Jeff):**
1. Wczytaj zapis (np. Seagard). W `Modules/Armoury/Armoury.log` (albo logu tematu) jest linia `Menu miasta (m95-menu): start | WLACZONY` z liczba
   przeniesionych opcji i lista menu miasta.
2. Wejdz do miasta. Menu glowne: port, keep, arena, tavern district, Recruit troops, Trade, **Go to the smithy**, **Look for paid work**, **Town affairs**,
   Wait here, Leave (plus opcje zadan / dom / Iron Bank, gdzie sa). Nie powinno byc juz "Work the forge", "Take lodgings at an inn", "Realm ledger" itd.
3. **Go to the tavern district**: sa "Visit the tavern", okup jencow, najemnicy i nowe: "Take lodgings at an inn" i (w cudzym miescie)
   "Contact local informants", nad "Back to town center". Wejdz do karczmy: wyjscie nazywa sie teraz "Back to the tavern district" i tam wraca. Przespij noc - jak dotad (pobudka w menu miasta).
4. **Go to the smithy**: na gorze opis kazdej opcji; "Enter smithy" otwiera kuznie gry (crafting); "Work the forge" - nasza kuznia, a "Wipe your hands and
   step out" wraca do "Go to the smithy"; "Visit the local blacksmith..." (RB) i "Manage Armory" (DTE) dzialaja jak dotad (ich powrot - menu miasta).
   Jesli kowal ma zamowienie (!) w kuzni gry, "!" widac na "Go to the smithy".
5. **Look for paid work**: dniowka i warta nocna - start, przerwanie, powrot do miasta jak dotad.
6. **Town affairs**: People of this land, Estates and livelihood, Realm ledger, Living Economy, Banner Kings, Buy a house (gdy nie masz tu domu) - kazda
   otwiera to samo okno / menu co dotad. "Back to town center" wraca do menu miasta.
7. MCM Armoury -> "The town menu" -> wylacz **Town Menu Tidy**, zapisz, wczytaj: menu miasta jak dawniej (linia logu: WYLACZONY). Wlacz z powrotem.
8. Na pasku u gory podmenu sa tytuly: "Smithy", "Paid Work", "Town Affairs". W Qohorze (u mistrzow) wyjscie nazywa sie "Back to the smithy" i tam wraca.
9. Dom (poprawka RC): we WSI, w ktorej nie masz domu, jako pierwsza rzecz po wczytaniu zapisu kup dom ("Buy a house in this place"), potem "Step back outside" -
   ma byc zwykle menu wsi (dotad menu miasta we wsi i blad). To samo w miescie: kup dom w "Town affairs", wyjdz - menu miasta.
10. Zapis w srodku "Go to the smithy", wylacz Town Menu Tidy, wczytaj: puste podmenu z "Back to town center" (dotad druzyna ladowala na mapie). Wlacz z powrotem.
11. Napisz, czego brakuje albo co by bylo lepiej gdzie indziej (np. Banner Kings z powrotem na wierzch) - przestawienie to jedna linijka w planie
    (oprocz "Manage town" i "Recruit troops" - te musza zostac w menu miasta, patrz nizej).

### Poprawki po recenzjach (gracz, technika) - 20.10

**Dla Jeffa (prosto):** wyjscie z karczmy nazywa sie teraz "Back to the tavern district", a u mistrzow Qohoru "Back to the smithy" - napis mowi,
dokad prowadzi. Nowe podmenu maja tytul na gorze. Przy okazji naprawiony stary blad domow: kupno domu we wsi i wyjscie z niego moglo otworzyc menu miasta
we wsi i wywrocic menu - teraz wyjscie zawsze prowadzi do menu tej osady, w ktorej stoisz.

| Uwaga recenzji | Ocena | Co zrobione |
|---|---|---|
| (technika, WAZNE, blad sprzed m95) RC `Homes.cs`: po kupnie domu "Step back outside" i sprzedaz wracaja do statycznego `_cameFrom`, ktore ustawia tylko wejscie "Go to your house" | **PRAWDZIWE** - sprawdzone: `BuyConsequence` -> `SwitchToMenu("rc_home")` bez `_cameFrom` (domyslnie "town"); gra `PlayerTownVisitCampaignBehavior.game_menu_town_on_init` czyta `Settlement.CurrentSettlement.Town.GarrisonParty` (we wsi `Town == null` -> NullReference w init menu); odwrotnie (wejscie we wsi, kupno w miescie) - menu wsi w miescie | nowe `Homes.Outside()` (wies -> "village", miasto -> "town", zapas `_cameFrom`) - ten sam wzor co `Work.BackMenu` (audyt 13.10); "Step back outside" i powrot po sprzedazy przez `Outside()`, kupno ustawia `_cameFrom = Outside()`. Bez klucza MCM: to naprawa bledu (wylacznik przywracalby wywrotke), w zwyklej drodze (wejscie z miasta/wsi) wynik identyczny jak dotad. Kupno z "Town affairs" -> wyjscie do glownego menu miasta (RC nie zna podmenu Armoury) |
| (gracz) napisy naszych wyjsc nie mowia, dokad prowadza ("Back to the town" -> dzielnica karczm, "Leave" u Qohoru -> "Go to the smithy") | prawdziwe | `TownMenus.SetBackLabel` z warunku opcji (gra liczy warunek przed odczytem napisu - `GameMenuVM.Refresh`; ten sam wzor co `CrimeCampaignBehavior`: `args.Text.SetTextVariable`): "Back to the tavern district" / "Back to the smithy", gdy wejscie przeniesione; inaczej napis jak dotad ("Back to the town", "Leave"). Pobudka po nocy w karczmie dalej w menu miasta (`NightRest.SettlementMenu`) - zmiana w NightRest, osobny wpis, jesli Jeff zechce |
| (gracz) przy wylaczonym kluczu podmenu nie powstaja - zapis z podmenu wyprowadza druzyne na mape | prawdziwe | trzy podmenu `arm_tm_*` (opis, tytul, "Back to town center") rejestrowane zawsze, takze przy wylaczonym kluczu i pustym podmenu; klucz decyduje tylko o przenoszeniu opcji i wpieciu wejsc. Przy bledzie przenoszenia listy podmenu tez wracaja z kopii (tylko "Back" - opcja nie stoi w dwoch miejscach) |
| (technika) zewnetrzny catch po zatwierdzeniu moze zgasic `_on` i schowac wejscia | prawdziwe (dzis praktycznie nieosiagalne) | flaga `committed`; linia logu startu we wlasnym try (`Stumble("linia startu")`); zewnetrzny catch nie rusza stanu po zatwierdzeniu |
| (technika) statyczne `_doneFor` i `_hubs` trzymaja menu i zachowania poprzedniej kampanii | prawdziwe | `_hubs` juz nie statyczne (lokalne w `Apply`; obiekty Hub zyja w delegatach swoich menu i gina z `GameMenuManager` kampanii), `_doneFor` jako `WeakReference<GameMenuManager>`; `_where` to same napisy. `BackTo` dodatkowo sprawdza, ze podmenu istnieje w biezacej kampanii (inaczej "town") |
| (technika) brak tytulu podmenu (`args.MenuTitle`) | prawdziwe | `Hub.Title`: "Smithy", "Paid Work", "Town Affairs" - `Init` ustawia `args.MenuTitle` (`GameMenu.RunOnInit` przepisuje go do menu) |
| (technika) pulapka: opcje gry z `GameMenuEventHandler("town", ...)` po przeniesieniu bylyby martwe | prawdziwe (dzis nie w planie) | straznik `TownOnly` = `manage_production`, `manage_production_cheat`, `recruit_volunteers` (sprawdzone w dekompilacji: tylko te trzy maja `GameMenuEventHandler("town", ...)` w CampaignSystem; SandBox, SandBox.View, StoryMode, ViewModelCollection - brak); `Apply` je pomija i dopisuje do linii logu "nieprzenaszalne", gdyby ktos dodal je do planu |

**Ryzyko / co sprawdzic:** napisy wyjsc liczone w warunku opcji (raz na odswiezenie menu, napis z `_where` - bez pracy co klatke); zmienna tekstu na
wlasnym `TextObject` opcji (nie globalna). Puste podmenu przy wylaczonym kluczu sa nieosiagalne z menu miasta (wejsc nie ma), tylko z zapisu.
RC: `Outside()` wolane tylko przy kliknieciu. Build Armoury i RC kod 0. **Status:** DO SPRAWDZENIA (jak caly wpis).
