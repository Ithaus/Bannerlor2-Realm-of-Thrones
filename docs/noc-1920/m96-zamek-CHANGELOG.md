## m96-zamek - porzadek menu zamku: koszary i sprawy zamku w podmenu, jak w miescie (Armoury)

**Dla Jeffa (prosto):** menu zamku jest teraz krotkie, tak jak menu miasta: zostaja w nim miejsca (loch, sala lorda, spacer, rozmowa z kasztelanem w
zamkach ROT), Banner Kings (tam jest werbunek w zamku - zostaje pod reka, jak "Recruit troops" w miescie), "Manage castle", schowek, czekanie i wyjazd,
a reszta jest w dwoch nowych wejsciach. **Go to the barracks** - zaloga (Manage garrison, Donate troops to garrison), sprzet dla zalogi i naprawa u
kowala zamku. **Castle affairs** - People of this land, Estates and livelihood, Realm ledger, Living Economy i pozyczka u ludzi zamku. Kazda opcja
dziala jak dotad i pokazuje sie w tych samych warunkach, kazde podmenu ma na gorze krotki opis i na dole "Back to the castle". Wylacznik: MCM Armoury -> "The castle menu" -> **Castle Menu Tidy**
(wylaczony = menu zamku jak dzis, po wczytaniu zapisu).

**Problem:** Jeff 20.10, po porzadku menu miasta (m95-menu, WGRANIE 95): "menu zamku tez uporzadkuj". Menu zamku ma ok. 20 opcji z szesciu zrodel (gra,
ROT, BK, RB, BEE i szesc opcji Armoury) w kolejnosci przypadkowej (kolejnosc rejestracji modow i ich indeksow 1, 4, 4, 5, 6, 7, 7, 7, 8 i "na koniec").

**Dowod (dekompilacja, nie zgadywane):** wszystkie DLL w `bin` i `Modules\*\bin\Win64_Shipping_Client` przeszukane na napis "castle" (sterta #US) przy
`AddGameMenuOption`, potem zdekompilowane i przejrzane rejestracje (`dectool grep`). Opcje menu `castle`:

| Opcja (napis) | id | Mod / klasa | Warunek (skrot) | Skutek / powrot |
|---|---|---|---|---|
| Go to the dungeon (+ Cheat) | `castle_prison`, `castle_prison_cheat` | gra PlayerTownVisitCampaignBehavior | jency / cheat | podmenu `castle_dungeon`, Back -> `castle` |
| Manage garrison | `manage_garrison` | gra | pan zamku = rod gracza, ta sama strona | ekran oddzialow (PartyScreenHelper), bez zmiany menu |
| Manage castle | `manage_production` | gra | pan zamku = rod gracza | skutek `null` - prawdziwy w `[GameMenuEventHandler("castle", "manage_production")]` (PlayerTownVisit) -> **nieprzenaszalne** |
| Open stash | `open_stash` | gra | pan zamku = rod gracza | ekran schowka, bez zmiany menu |
| Donate troops to garrison | `leave_troops_to_garrison` | gra | cudzy zamek tej samej strony, jest miejsce | ekran oddzialow, bez zmiany menu |
| Take a walk around the castle | `take_a_walk_around_the_castle` | gra | zawsze | misja, powrot -> `castle` (MenuHelper SetNextMenu) |
| Go to the lord's hall (+ Cheat) | `castle_lords_hall`, `castle_lords_hall_cheat` | gra | dostep | misja |
| Wait here for some time | `town_wait` | gra | - | `town_wait_menus` |
| Return to Army / Leave | `castle_return_to_army`, `leave` | gra | - | - |
| Speak to {CASTELLAN} | `speak_to_castellan` | ROT ROTHouseTroopsBehavior (index 1) | kasztelan w tej osadzie | misja sali lorda |
| Banner Kings | `bannerkings_castle_submenu` | BK BKSettlementActions (index 4) | osada z ludnoscia BK | menu `bannerkings` (w zamku z "Recruit troops"), Leave -> `castle` |
| Visit the castle smith to repair party equipment | `repair_equipment_castle` | RB EquipmentRepairBehavior (index 4) | RB Equipment; nieaktywna przy 0 kosztu / braku zlota | placi, `SwitchToMenu("town")` (stary blad RB - patrz Ryzyko) |
| Hand kit to the garrison | `arm_garrison_kit_castle` | Armoury GarrisonKit (index 5) | wlasny zamek, zaloga, DTE | ekran ekwipunku, bez zmiany menu |
| People of this land | `arm_people_castle` | Armoury PeopleMenu (index 6) | klucz People Menu Entry | okno (ShowInquiry) |
| Realm ledger / Estates and livelihood | `arm_realm_ledger_castle`, `arm_estates_castle` | Armoury DailyLife.View (index 7) | klucz Daily Life Estates | okna |
| Borrow from / Repay the castle folk | `arm_townloan_borrow_castle` / `_repay_castle` | Armoury TownFolk.TownLoan (7, 8) | klucz Town Loan | okna |
| Living Economy | `bee_castle_root` | BEE SettlementMenuBehavior (index -1 = na koniec) | zamek | menu `bee_castle_menu`, Back -> `castle` |

- Nie dodaja nic do menu `castle`: NavalDLC (port tylko w `town`), DTE (zadnego napisu "castle"), RC (domy i praca tylko `town`/`village`),
  GrandTourney, ForgeView, CrashScribe, SandBox, StoryMode, Spoils of War. Zadania gry (garnizon, podatki) wpinaja sie w `castle_guard`, nie `castle`.
  AIInfluence jest zaciemniony (dekompilator nie czyta metadanych) - w napisach brak id opcji menu; jesli cos dodaje, zostaje w zamku (linia logu wypisuje cale menu przed i po).
- `GameMenuEventHandler("castle", ...)`: atrybuty przeszukane w sterte blob wszystkich DLL - jedyny to `manage_production` w CampaignSystem.
- Zaden warunek przenoszonej opcji nie patrzy na id biezacego menu; skutki to ekrany gry, okna albo wlasne menu (BK, BEE wracaja `SwitchToMenu("castle")`).
  Zadna nasza opcja zamku nie otwiera naszego menu (w miescie karczma / kuznia / Qohor - tu nic), wiec `BackTo` z miasta nie jest potrzebne.

**Przyczyna:** kazdy mod dokleja swoje opcje do menu gry `castle` wedlug wlasnego indeksu (`GameMenu.AddOption`: wstawienie pod index albo na koniec).

**Zmiana:** nowy plik `Armoury/src/CastleMenus.cs` (wzor `TownMenus`, osobna klasa - bez ruszania TownMenus), nowy klucz MCM **Castle Menu Tidy**
(`Settings.CastleMenuTidy`, grupa "The castle menu", domyslnie wlaczony; MCM Jeffa tego klucza nie ma - nic do zmiany w `Armoury.json`). Osobny klucz, nie
wspolny z Town Menu Tidy: menu zamku da sie wylaczyc bez ruszania miasta. Wpiecie: `SubModuleMain.OnAfterGameInitializationFinished` zaraz po
`TownMenus.Apply` (osobny try) - po `OnSessionLaunched` i `OnAfterSessionLaunched` wszystkich zachowan, przed pierwszym pokazaniem menu. Opcje przechodza
jako TE SAME obiekty `GameMenuOption` (id, napis, warunek, skutek bez zmian).

| Opcja | Skad | Nowe miejsce |
|---|---|---|
| Manage garrison | gra | **Go to the barracks** (nowe podmenu `arm_cm_barracks_menu`; w miescie gra trzyma ja w "Go to the keep") |
| Donate troops to garrison | gra | **Go to the barracks** |
| Hand kit to the garrison | Armoury | **Go to the barracks** |
| Visit the castle smith to repair party equipment | RB | **Go to the barracks** |
| People of this land / Estates and livelihood / Realm ledger | Armoury | **Castle affairs** (nowe podmenu `arm_cm_affairs_menu`) |
| Living Economy | BEE | **Castle affairs** |
| Borrow from / Repay the castle folk | Armoury | **Castle affairs** |
| Banner Kings | BK | **zostaje w menu zamku** (poprawka po recenzji: w zamku jedyna droga werbunku to BK "Recruit troops" - jak "Recruit troops" gry w miescie, ktory zostal na wierzchu) |
| Go to the dungeon, lord's hall, Speak to castellan, Take a walk, Manage castle, Open stash, Wait, Return to Army, Leave, opcje cheat i zadan | gra / ROT | **zostaja w menu zamku** |

- Wejscia "Go to the barracks" i "Castle affairs" staja w menu zamku przed "Wait here for some time" (bez niego - przed pierwszym Leave, na koncu);
  wejscie widoczne, gdy choc jedna opcja w srodku jest widoczna (warunki opcji wolane jak w grze, na swiezym `MenuCallbackArgs` - stan opcji nieruszony),
  znaczniki zadan "!" przechodza na wejscie, podpowiedz wejscia wymienia widoczne opcje. Podmenu: tytul ("Barracks", "Castle Affairs"), opis - zdanie na
  kazda opcje widoczna w tej chwili, tlo osady, na dole "Back to the castle" (-> `castle`).
- Pulapki z m95-menu przeniesione: (1) moment - przed pierwszym pokazaniem menu (`GameMenuVM` trzyma opcje po indeksie); (2) stan statyczny bez mocnych
  odwolan - `_doneFor` jako `WeakReference<GameMenuManager>`, obiekty Hub tylko w delegatach swoich menu; (3) straznik `CastleOnly` = `manage_production`;
  (4) dwa podmenu rejestrowane ZAWSZE, takze przy wylaczonym kluczu (zapis z podmenu wczyta sie: puste podmenu z "Back to the castle"); (5) napis powrotu
  mowi, dokad prowadzi. Przy bledzie w polowie przenoszenia menu zamku i podmenu wracaja z kopii (nic nie znika); blad linii logu po zatwierdzeniu nie
  gasi wejsc (flaga `committed`, osobny try). Potkniecia liczone per opcja / miejsce (`CastleMenus.<miejsce> (potkniecie N)`), nic sie nie gasi.
- Narzedzie progow: `python -I tools/sprawdz_logi.py --grupa m96zamek <Armoury-*.log>` (ZM-0..ZM-4; sprawdzone na sztucznej linii i na logu WGRANIA 95 - INFO).

**Linia logu (Armoury.log, kazdy start sesji - nowa kampania i wczytanie):** `Menu zamku (m96-zamek): start | WLACZONY (Castle Menu Tidy) | menu zamku przed N
opcji: <id po kolei> | przeniesione X z 10 w planie: arm_cm_barracks_menu 4 [...]; arm_cm_affairs_menu 6 [...] | wejscia podmenu wpiete 2 (przed 'town_wait'
(index K)) | nie znaleziono cudzych F [...], naszych O [...] | menu zamku teraz M opcji: <id po kolei>`. Wylaczony klucz: `... WYLACZONY w MCM (Castle Menu Tidy) -
menu zamku jak dotad ...`. **Oczekiwane w autotescie** (pelny zestaw modow Jeffa): przeniesione **10 z 10** (koszary 4, sprawy zamku 6), nie znaleziono
cudzych **0**, naszych **0**, wejscia **2**, menu zamku przed **22** (12 gry + ROT + BK + RB + 6 Armoury + BEE; wiecej tylko, gdy AIInfluence cos dokleja),
teraz **14** (przed - 10 + 2; na liscie "teraz" `bannerkings_castle_submenu`, `manage_production`, `leave`, `arm_cm_barracks`, `arm_cm_affairs`),
`ERROR in CastleMenus` i `ERROR in OnAfterGameInitializationFinished (CastleMenus)` **0**; `--grupa m96zamek` - 0 x NIE. Linia miasta `Menu miasta (m95-menu): start` bez zmian (19 z 19).

**Ryzyko / co sprawdzic (kontrola calosci):**
- Regresje: menu miasta i wsi nietkniete (osobna klasa, osobny klucz, te same id opcji miasta). Zadna inna nasza klasa nie szuka opcji w menu `castle`
  po id po starcie sesji (BeeGifts, BeeSeal, ShortLabels, DistrictWorks czytaja menu w `OnSessionLaunched`/`OnAfterSessionLaunched` - przed przeniesieniem,
  i tylko menu BEE / wsi). Gra nie odwoluje sie do `manage_garrison`, `leave_troops_to_garrison` itd. poza rejestracja (napis tylko raz w CampaignSystem,
  w zadnym innym DLL). `IncidentsCampaignBehaviour` losuje zdarzenie "EnteringCastle" przy kazdym otwarciu menu `castle` i patrzy na "leave" (zostaje);
  "Back to the castle" otwiera `castle` ponownie, ale losowanie ma ziarno z `CampaignTime.Now.NumTicks`, a w menu czas stoi - ponowne otwarcie w tej samej
  chwili daje ten sam wynik, wiec dodatkowej szansy na zdarzenie nie ma (tak samo jak powrot z lochu i sali lorda w samej grze; poprawka po recenzji).
- Cudzy powrot (Back z BEE) laduje w glownym menu zamku, nie w "Castle affairs" - jak w miescie. Leave z BK wraca tam, skad gracz wszedl (BK zostaje na wierzchu). Ekrany (zaloga, ekwipunek) wracaja do
  podmenu, z ktorego je otwarto.
- **Stary blad RB (nie ruszany, sprzed tej zmiany):** "Visit the castle smith..." po zaplacie robi `SwitchToMenu("town")` takze w zamku (dekompilacja
  `EquipmentRepairBehavior.repair_equipment_consequence`) - gracz widzi w zamku menu miasta (Town Center). Bylo tak przed m96 i jest tak samo z podmenu
  koszar. Naprawa (powrot do menu osady) - osobny wpis, jesli Jeff zechce.
- Banner Kings zostaje na wierzchu menu zamku (poprawka po recenzji), wiec werbunek w zamku (BK "Recruit troops") jest dalej dwa klikniecia od menu zamku,
  jak dotad. W miescie BK jest w "Town affairs" (tam werbunek to "Recruit troops" gry na wierzchu) - roznica celowa: w obu osadach werbunek pod reka.
- Zapis zrobiony w podmenu i wczytany na DLL sprzed m96 (cofniecie): gra nie zna `arm_cm_*` i konczy spotkanie (druzyna na mapie przy zamku) - jak przy
  kazdym usunietym menu moda. Bez stanu w zapisie, bez latek Harmony (tylko odczyt prywatnego `GameMenu._menuItems` przez AccessTools).
- Koszt: warunki opcji w podmenu liczone przez wejscie tak jak dotad przez menu zamku (raz na odswiezenie menu) + raz przy otwarciu podmenu dla opisu;
  nic co klatke, nic z watkow AI.
- Zmiana klucza w trakcie gry dziala od nastepnego wczytania; warunki wejsc nie patrza na klucz, wiec wylaczenie w trakcie gry niczego nie chowa.
- Build: Armoury `dotnet build -c Release` kod 0 (0 bledow, 5 starych ostrzezen; takze po poprawkach recenzji), `tools/gen_mcm.py` - Armoury 2130 ustawien. Gry nie uruchamiano
  (Jeff gra) - zachowanie w grze NIESPRAWDZONE.

**Status:** DO SPRAWDZENIA (galaz w-toku/m96-zamek; do WGRANIA 96 po nocnym autotescie).

**Test w grze krok po kroku (Jeff):**
1. Wczytaj zapis. W `Modules/Armoury/Armoury.log` jest linia `Menu zamku (m96-zamek): start | WLACZONY` (przeniesione 10 z 10, menu zamku przed i teraz; Banner Kings na liscie "teraz").
2. Wjedz do zamku (np. przyjaznego). Menu: loch (gdy sa jency), sala lorda, spacer, Banner Kings, **Go to the barracks**, **Castle affairs**, Wait here,
   Leave (we wlasnym zamku takze Manage castle i Open stash; w zamkach ROT z kasztelanem - Speak to ...). Nie powinno byc juz "Realm ledger",
   "Living Economy", "Manage garrison" itd. na wierzchu. Banner Kings -> Recruit troops dziala jak dotad, Leave z BK wraca do menu zamku.
3. **Go to the barracks**: opis na gorze; we wlasnym zamku Manage garrison i Hand kit to the garrison, w cudzym zamku swojej strony Donate troops,
   wszedzie (przy RB Equipment) kowal zamku. Ekran zalogi po zamknieciu wraca do koszar. "Back to the castle" - menu zamku.
4. **Castle affairs**: People of this land, Estates and livelihood, Realm ledger, Living Economy, Borrow / Repay the castle folk (gdy sa) - kazda otwiera
   to samo okno / menu co dotad. Back z Living Economy wraca do glownego menu zamku.
5. MCM Armoury -> "The castle menu" -> wylacz **Castle Menu Tidy**, zapisz, wczytaj: menu zamku jak dawniej (linia logu: WYLACZONY). Wlacz z powrotem.
6. Zapis w srodku "Go to the barracks", wylacz Castle Menu Tidy, wczytaj: puste podmenu z "Back to the castle". Wlacz z powrotem.
7. Napisz, co by bylo lepiej gdzie indziej (np. Manage garrison z powrotem na wierzch albo Banner Kings do "Castle affairs") - przestawienie to jedna linijka w planie
   (oprocz "Manage castle" - musi zostac w menu zamku).

### Poprawki po recenzjach (20.10, recenzje "gra" i "technika")

Obie recenzje: nic krytycznego ani waznego, zrodla opcji, straznik CastleOnly, id opcji i kolejnosc wpiecia potwierdzone osobno w dekompilacji. Drobne:

| Uwaga | Prawdziwa? | Co zrobione |
|---|---|---|
| (gra) werbunek w zamku dwa klikniecia dalej (Castle affairs -> Banner Kings -> Recruit troops), a w miescie Recruit troops zostal na wierzchu | prawdziwa: BK `BKSettlementActions` dodaje `castle_recruit_volunteers` ("Recruit troops") tylko do menu `bannerkings` (index 3) - w zamku to jedyna droga werbunku; Leave z BK robi `SwitchToMenu("castle")` | **Banner Kings zostaje na wierzchu menu zamku** (usuniety z `Plan`, komentarz w miejscu wpisu; opis i podpowiedz "Castle affairs" licza sie z Kids same). Decyzja projektu: werbunek pod reka w kazdej osadzie (miasto - "Recruit troops" gry, zamek - BK); przy okazji Leave z BK wraca tam, skad gracz wszedl. Oczekiwane: przeniesione 10 z 10, teraz 14. `sprawdz_logi --grupa m96zamek`: plan bez BK, ZM-3 sprawdza, ze BK zostal w zamku (`M96ZAMEK_KEEP`). Napis MCM i komentarz Settings.cs poprawione, `gen_mcm.py` ponownie |
| (gra) CHANGELOG: Incidents patrzy w zamku nie tylko na "leave" - losuje "EnteringCastle" przy kazdym otwarciu `castle` | prawdziwa (IncidentsCampaignBehaviour: `RandomFloatWithSeed((uint)CampaignTime.Now.NumTicks)`, `StringId == "castle"` -> `TryInvokeIncident(EnteringCastle)`); skutek zaden - w menu czas stoi, to samo ziarno | zdanie w "Ryzyko" poprawione; kod bez zmian |
| (gra, technika) brak wpisu `--grupa m96zamek` w pomocy narzedzia | prawdziwa | dopisany za `p6szubienice` (ZM-0..ZM-4) |
| (gra, technika) ZM-4 nie widzi `ERROR in OnAfterGameInitializationFinished (CastleMenus)` (blad przy wywolaniu Apply, np. JIT/TypeLoad - linii startu wtedy nie ma i ZM-0 dawal INFO jak dla starego logu) | prawdziwa | ZM-4 liczy oba napisy (`M96ZAMEK_ERR`); bez linii startu, a z takim bledem ZM-0 = NIE (nie INFO), ZM-1 INFO "bez linii startu - blad paczki". Sprawdzone na sztucznych logach: pelny nowy (6 progow, 0 x NIE), stary uklad 11 z 11 (ZM-1 i ZM-3 NIE - stary DLL nie przejdzie), sam blad z wywolania (ZM-0 i ZM-4 NIE), klucz wylaczony (0 x NIE), log bez paczki i log WGRANIA 95 12-25-19 (INFO, 1,2 s) |

Build Armoury po poprawkach: kod 0, 0 bledow, 5 starych ostrzezen; w DLL plan bez `bannerkings_castle_submenu` (dectool grep). Gry nie uruchamiano - zachowanie w grze
dalej NIESPRAWDZONE (status DO SPRAWDZENIA).
