## r4-dom (20.10) - wioska gracza jak prawdziwa wies: handel, wlasny dom (lozko + skrzynia), spacer (Armoury)

**Dla Jeffa (prosto).** W menu twojej wioski (Seven Elms) sa trzy nowe opcje. "Trade" otwiera handel z targiem wsi okregu (Mudgrave): kupujesz to, co wioska
i okreg wytwarzaja, po ich cenach, a zloto idzie do kiesy tej wsi - jak "Buy products" w zwyklej wsi. "Your house" to twoj dom w wiosce: "Sleep in your bed" daje
najlepszy wypoczynek w grze, za darmo (jak najlepszy pokoj karczmy: szybszy marsz i lepsze morale do zmierzchu, ranni szybciej sie goja w czasie snu), a "Open the chest"
otwiera skrzynie domu - odkladasz tam rzeczy z taboru i zabierasz je, kiedy wrocisz (skrzynia jest w zapisie). "Take a walk" to spacer pieszo po scenie wsi okregu
(twoja wioska nie ma wlasnej sceny) - dziala, gdy wjedziesz przez wies okregu ("Visit Seven Elms (your village)" w jej menu); przy wjezdzie z mapy opcja jest szara
i mowi, jak wejsc. Uwaga na skrzynie: zanim kiedys cofniesz Armoury do starszej wersji albo go wylaczysz, wyjmij wszystko ze skrzyni - starsza wersja jej nie zna
i rzeczy przepadlyby przy pierwszym zapisie (dymek skrzyni o tym przypomina).

**Problem.** Jeff 20.10 (doslownie): "i do wioski ktora nalezy do mnie ja moge do niej wjechac, tak jak do normlanej wioski i tam dokonac sen, inwestycje, zakup rzeczy
z tej wioski, przepsacerowanie sie po wiosce i w tej wiosce jest moj dom wtedy." Menu wioski (n20-wioska, `arm_side_fief`) mialo tylko nocleg, prace okregu, przeglad
i wyjazd - bez handlu, domu i spaceru.

**Przyczyna.** Wioska poboczna to nasz obiekt mapy, nie osada gry - nie ma wlasnego targu, sceny ani domu; menu wsi gry ("village") tych opcji jej nie daje.

**Zmiana.** Nowy plik `Armoury/src/SideVillageHome.cs` (+ minimalne wpiecia):
- (1) WJAZD: bez zmian (sprawdzony kod n20: klik -> ProcessTravel -> menu po dojechaniu w Tick, postoj -> pytanie, "Visit X (your village)" w menu wsi gry). Nowe opcje
  wchodza do tego samego menu: nocleg 0, prace 1, przeglad 2, handel 3, dom 4, spacer 5, wyjazd na koncu (GameMenu.AddOption: Insert przed dodanym wczesniej "Leave").
- (2)/(3) SEN i INWESTYCJE: zostaja jak byly.
- (4) HANDEL "Trade" (klucz `SideVillageFiefTrade`): towar = `Settlement.ItemRoster` wsi gry okregu, ceny = `Village.MarketData` tej wsi, zloto = kiesa wsi okregu
  (`SettlementComponent.Gold`).
  - Wejscie przez wies okregu ("Visit X"): partia JEST w osadzie (PlayerEncounter) - dokladnie wywolanie gry `InventoryScreenHelper.OpenScreenAsTrade(st.ItemRoster,
    st.Village)` (jak "Buy products", PlayerTownVisitCampaignBehavior), zgoda z `SettlementAccessModel.CanMainHeroDoSettlementAction(Trade)` jak w grze.
  - Wejscie z mapy (bez osady i spotkania): dekompilacja pokazala dwie pulapki wywolania gry: `GetCurrentMarketDataForPlayer` bez osady bierze ceny NAJBLIZSZEGO MIASTA,
    a `InventoryLogic.DoneLogic` placi kupcowi tylko przy `Settlement.CurrentSettlement != null` (bez osady zloto gracza szloby w nicosc, a sprzedaz bralaby je z niczego).
    Dlatego wlasny ekran: ten sam `InventoryLogic(wies)` + `Initialize` co gra, ale ceny targu wsi okregu, sluchacz kiesy wsi (kopia prywatnego MerchantInventoryListener)
    i po zamknieciu (`DoneLogicExtrasDelegate`, wolany tylko po udanym DoneLogic) kiesa wsi zmienia sie DOKLADNIE o to, co gra przelala graczowi: `min(-TotalAmount,
    zloto wsi)` w druga strone. Anulowanie = rachunek 0 = bez przelewu. Zamknieta ekonomia.
  - Szara z powodem: wojna z krolestwem okregu, wies okregu rabowana/zlupiona, partia nie przy wiosce.
- (5) DOM "Your house" (klucz `SideVillageFiefHouse`) - menu `arm_side_home`:
  - "Sleep in your bed": stan noclegu karczmy (`TownInns._rest`) z uid wioski i jakoscia "dobry dom" (Q=2, najlepsza: predkosc +12%, morale +8, sen x1.25, gojenie
    +50% w czasie snu), bez oplaty i bez premii cen (`TownInns.HouseSleep`). Dalej ta sama droga co "Rest the night here" (`NightRest.BeginInnSleep`, czekanie do switu,
    pobudka, premia do zmierzchu). Rozpoznanie domu bez nowego pola w zapisie: `HostUid != null && Q == 2` (oba juz sa w arm_inns). Teksty: "Rested in your own house",
    "Slept in your own house" w rozpisce predkosci i morale, opis snu "You sleep in your own bed ...". Szara z powodem: bez karczm (Town Inns / Night Rest), wojna, rabunek,
    spalona wioska ("your house burned with the village").
  - "Open the chest": ekran schowka gry (jak `OpenScreenAsStash`, InventoryMode.Stash) z wlasnym `DoneLogicExtrasDelegate`. Skrzynia zapisywana jako napis
    `arm_side_home` przez `SaveText.Sync`: "sh1|uid|item~modyfikator~sztuk;...". Przedmiot, ktorego nie ma w grze po wczytaniu (mod wylaczony) - wpis zostaje w zapisie
    bez zmian. Rzeczy kute w kampanii (`IsCraftedWeapon` / `IsCraftedByPlayer` - obiekty bez stalego id, zapis tekstowy ich nie odtworzy) wracaja do taboru przy zamknieciu
    z komunikatem.
  - (recenzja) Przy zapisie gry (`SideVillageHome.ExportSave`) linia logu ze skladem skrzyni - pierwszy zapis sesji i kazda zmiana:
    `Wioska gracza - dom (r4-dom): skrzynia w zapisie (N sztuk, K rodzajow, R wpisow spoza gry; przedmiot~modyfikator~sztuk): <sklad>`; przy wczytaniu
    `... skrzynia z zapisu: K rodzajow (N sztuk) ...`. Dymek "Open the chest" i opis klucza MCM `Side Village Fief House` ostrzegaja: oproznic skrzynie przed
    wylaczeniem Armoury albo cofnieciem do starszej wersji.
  - Domy klanowe RealisticCaptivity (`Homes`) NIE uzyte: klucz domu to `Settlement.StringId`, menu `rc_home` czyta `Settlement.CurrentSettlement` (z mapy partia nie
    stoi w osadzie, a wioska poboczna nie jest osada gry), zakladka "Houses" pomija klucze bez `Settlement.Find`, do tego inna DLL. Stad prosty wlasny dom.
- (6) SPACER "Take a walk" (klucz `SideVillageFiefWalk`): ta sama droga co "Take a walk through the lands" gry:
  `(PlayerEncounter.LocationEncounter as VillageEncounter).CreateAndOpenMissionController(LocationComplex.Current.GetLocationWithId("village_center"))` - TYLKO gdy stan
  spotkania jest dokladnie taki jak w menu wsi gry (partia w wsi okregu, `Settlement.CurrentSettlement` = ta wies, VillageEncounter tej osady, lokacja i scena
  "village_center", wies nie rabowana). Po wyjsciu ze sceny `MapState.OnActivate -> MenuContext.Refresh` - wraca menu, z ktorego weszlismy (arm_side_fief).
  Z mapy opcja szara z podpowiedzia ("ride into X and choose 'Visit ... (your village)' there").
- Wpiecia: `SideVillageFief.cs` - odczyt `Uid` / `DistId` / `Name` i 4 wywolania (Reset, koniec AddMenus, Hourly, Daily - to ostatnie w `finally`); `TownInns.cs` - 5 tekstow przez `HostWhere` /
  `IsHouse` (dach wiesniakow albo wlasny dom); `TownInns.Host.cs` - tekst i log snu w domu; `Settings.cs` - 3 klucze za `SideVillageFiefLodging`; `McmSettings.cs` (gen_mcm,
  2059 ustawien); `ArmouryBehavior.cs` - SyncData `arm_side_home` (zapis przez `ExportSave`).

**Ryzyko / co sprawdzic.**
- Regresje: stary zapis bez `arm_side_home` = pusta skrzynia; bez wioski gracza opcje ukryte (warunki `HasFief`). Nocleg "Rest the night here" bez zmian (Q=0); teksty
  karczm miast i zajazdow bez zmian (galaz `HostUid != null` tylko dla wioski). Starsza DLL czytajaca arm_inns z Q=2 i HostUid - nocleg w wiosce jakosci dobrego domu
  (premie przyciete jak dotad do Q=2) - bez wywrotki.
- Kolizje: latki Armoury na InventoryLogic (MarketGlut, SellByCondition, QuartermasterLaw, TownInns.GetItemPrice) czytaja `OtherParty` (= wies okregu) - dzialaja jak
  przy handlu we wsi. `PlayerInventoryExchangeEvent` (TownInns, SellByCondition) - bez osady oba wychodza na pierwszym warunku / sam log. BK MarketPatch dotyczy tylko
  targu miasta; BetterEconomy nie patchuje InventoryLogic (grep dekompilacji).
- Handel z mapy - nie-waniliowy stan (bez osady i spotkania, jak zwykly ekran ekwipunku z mapy, ale isTrading): okno pokazuje nazwe "<wioska> - market of <wies>",
  kiesa kupca widoczna dopiero po pierwszym ruchu (`OnTotalAmountChange`; `UpdateLeftCharacter` bez osady jej nie ustawia) - kosmetyka. Gracz moze "sprzedac" wiecej niz
  kiesa wsi - gra i tak da mu najwyzej kiese wsi (min w DoneLogic), a my zabieramy wsi to samo.
- Spacer z mapy NIE jest zrobiony (otwarte, plan nizej) - misja wsi bez PlayerEncounter i LocationComplex = wywrotka (zachowania misji SandBox czytaja oba).
- Skrzynia: jedna skrzynia na kampanie (wioska gracza jest jedna); przy wielkiej skrzyni napis rosnie (~40 znakow na rodzaj) - SaveText dzieli na kawalki.
- **COFANIE DLL / WYLACZENIE ARMOURY (recenzja, wazne): rzeczy w skrzyni przepadaja.** Rzeczy wlozone do skrzyni schodza z taboru i zyja tylko w kluczu
  `arm_side_home`. Gra przy zapisie pisze tylko klucze, ktore behavior podaje w SyncData (dekompilacja: `CampaignBehaviorManager.OnBeforeSave` ->
  `ClearBehaviorData` + `SaveBehaviorData` z nowym `BehaviorSaveData`; po wczytaniu `LoadBehaviorData` czysci slownik) - starsza DLL Armoury (sprzed r4-dom)
  albo wylaczony Armoury nie zapisze klucza i przy pierwszym zapisie (w Ironman Jeffa - autozapis) skrzynia znika na zawsze.
  **Procedura cofania (dopisac do kazdego cofniecia Armoury ponizej r4-dom):** przed podmiana DLL albo wylaczeniem modulu Jeff wchodzi do wioski -> "Your house"
  -> "Open the chest" -> zabiera wszystko do taboru -> zapis gry. Gdyby skrzynia przepadla: sklad z ostatniej linii `skrzynia w zapisie (...)` w Armoury.log
  (przedmiot~modyfikator~sztuk) - do odtworzenia recznie. Ostrzezenie w grze: dymek "Open the chest" i opis klucza MCM `Side Village Fief House`.
  Rozwazone i odrzucone: trzymanie rzeczy w `Settlement.Stash` wsi okregu (pole zapisywane przez gre - przetrwaloby cofniecie), bo gra przenosi schowek na targ
  osady, gdy klan gracza traci jej wlasnosc (`SettlementClaimantCampaignBehavior`), inne mody moga czytac `Stash`, a zmiana miejsca skrzyni to nowa migracja
  zapisu i nowy test - nie na poprawke po recenzji.
- Zadania gry przypiete do menu "village" (recenzja, drobne): `FamilyFeudIssueBehavior` (konfrontacja w village_center), `LandLordNeedsManualLaborers`,
  `MerchantArmyOfPoachers`, zdarzenia `IncidentsCampaignBehaviour` sprawdzaja `GameMenu.StringId == "village"` przy otwarciu menu. Po "Visit X" menu to
  `arm_side_fief`, wiec spacer z naszego menu nie rozstawia np. konfrontacji Family Feud w wsi okregu. Bez wywrotki; "Leave" wraca do menu "village" (flagi zadan
  ustawiaja sie znowu), a zwykly spacer z menu wsi okregu dziala jak dotad. Dymek "Take a walk" mowi: sprawy zadan w X - spacer z menu X.
- Cudzy kod sprawdzony w dekompilacji: `Helpers.InventoryScreenHelper` (OpenScreenAsTrade, OpenScreenAsStash, GetCurrentMarketDataForPlayer, CloseInventoryPresentation,
  MerchantInventoryListener), `InventoryLogic.DoneLogic` / `CurrentSettlementComponent` / `CurrentMobileParty`, `SPInventoryVM` (CloseScreen tylko przez pomocnika,
  UpdateLeftCharacter, OnTotalAmountChange), `PlayerTownVisitCampaignBehavior` (village: trade, village_center), `VillageEncounter`, `Settlement.CurrentSettlement`,
  `Enter/LeaveSettlementAction` (bez zmiany polozenia partii), `MapState.OnActivate`, `GameMenu.AddOption` (Insert).

**Status: DO SPRAWDZENIA** (zbudowane: Armoury 0 bledow, kod wyjscia 0; autotest nie uruchamiany - zakaz w tym zadaniu).

**Decyzje (podjete samodzielnie; wylaczniki).**
1. Handel z targiem WSI OKREGU (jej zapas i kiesa), nie osobny zapas wioski pobocznej - wioska poboczna nie ma wlasnego targu w grze; to ten sam towar, ktory jej ludzie
   wywoza na targ okregu. Wylacznik `Side Village Fief Trade`.
2. Wlasny prosty dom zamiast domow RC (powody wyzej). Lozko = najlepsza jakosc noclegu karczmy ("dobry dom"), za darmo, bez premii cen (to nie miasto). Wylacznik
   `Side Village Fief House` (off = brak opcji domu; skrzynia zostaje w zapisie).
3. Spacer tylko przy wejsciu przez wies okregu - jedyny stan, w ktorym gra sama otwiera spacer po wsi; z mapy - szara opcja z podpowiedzia. Wylacznik `Side Village Fief Walk`.
4. Skrzynia dostepna takze w spalonej wiosce (lozka nie ma, skrzynia z rzeczami gracza zostaje); w wojnie i przy rabunku dom zamkniety.

**Otwarte - spacer z mapy (plan).** Wejscie z mapy nie ma spotkania z osada. Bezpieczna droga na pozniej: opcja "Take a walk" z mapy prowadzi partie do wsi okregu
(`MapState.ProcessTravel` na osade albo `EncounterManager.StartSettlementEncounter`), gra sama zaklada spotkanie i menu "village"; flaga jednorazowa "spacer po wjezdzie"
przelacza w init menu wsi na `arm_side_fief` i dopiero tam (stan jak dzis przy "Visit") otwiera spacer. Do sprawdzenia w dekompilacji przed robota: kolejnosc
`PlayerEncounter.Init` / `EnterSettlement` przy wsi i czy przelaczenie menu w init "village" nie gubi `UpdateMenuLocations`. Bez autotestu z bitwa i scena nie wgrywac.

**Recenzja (pelna, 20.10) - co sprawdzone i co zrobione.** Recenzent: bez bledow krytycznych; 1 wazne, 3 drobne. Wszystkie cztery sprawdzone - prawdziwe.
1. WAZNE - skrzynia przepada przy cofnieciu DLL albo wylaczeniu Armoury. PRAWDZIWE (dekompilacja `CampaignBehaviorDataStore` / `CampaignBehaviorManager`:
   przy zapisie nowy `BehaviorSaveData` tylko z kluczami z SyncData, po wczytaniu slownik czyszczony). Poprawione tak, jak radzi recenzja: procedura cofania
   i ryzyko wyzej, linia logu ze skladem skrzyni przy zapisie (`ExportSave`) i przy wczytaniu, ostrzezenie w dymku "Open the chest" i w opisie klucza MCM.
   Kodu zapisu nie zmienialem (format `sh1` ten sam).
2. Drobne - dwa wzorce logu z raportu nie pasowaly do kodu. PRAWDZIWE. Linia otwarcia skrzyni zmieniona na `skrzynia domu otwarta w <wioska> (...)` (pasuje
   do wzorca `... skrzynia domu otwarta`); potkniecia w logu to `ERROR in SideVillageHome.<miejsce> (potkniecie N): ...` - wzorzec `ERROR in SideVillageHome.`
   (poprawiony w raporcie i w "Test w grze").
3. Drobne - spacer z `arm_side_fief` nie rozstawia zadan przypietych do menu "village" (Family Feud). PRAWDZIWE (grep dekompilacji: 4 miejsca, lista w Ryzyku).
   Opisane w Ryzyku, zdanie w dymku "Take a walk"; zachowania nie zmienialem (spacer z menu wsi okregu dziala jak w grze).
4. Drobne - `SideVillageHome.Daily()` za `try/finally` pomijane przy `return` w try. PRAWDZIWE. Przeniesione do `finally` w `SideVillageFief.Daily` (po
   `DayZero`); `SideVillageHome.Daily` ma wlasny try i wychodzi bez ruchu, wiec bez wioski nic nie pisze.
Zbudowane po poprawkach: Armoury kod wyjscia 0, 0 bledow (4 stare ostrzezenia); `gen_mcm.py` - zmieniony tylko opis klucza `SideVillageFiefHouse`.

**Test w grze.**
- Autotest (samotest bez klikania) z plikiem `Documents\Mount and Blade II Bannerlord\CrashScribe\armoury-wioska-gracza.flag` (DLL autotestu CrashScribe, Mode 1/2):
  w tej samej godzinie co test n20 (po nadaniu wioski) linia
  `Wioska gracza - dom (r4-dom): test (autotest, plik armoury-wioska-gracza.flag) | wynik TAK 7, NIE 0 | D1 menu domu i opcje wioski ... | D2 wies okregu ... |
  D3 handel: towar, ceny targu wsi, kiesa ... | D4 skrzynia: zapis tam i z powrotem ... | D5 nocleg w domu (stan i zapis) ... | D6 spacer: scena wsi okregu <scena> | D7 tekst domu ...`.
  Stan gracza (skrzynia, nocleg) przywracany w tescie.
- Recznie (Jeff): (a) z mapy klik na Seven Elms -> "Trade": okno "Seven Elms - market of Mudgrave", kup cos -> w logu `handel z mapy zamkniety: gracz -X ..., wies okregu
  Mudgrave +X (kiesa A -> A+X)`; sprzedaj cos -> odwrotnie. (b) "Your house" -> "Open the chest": wloz rzecz, wyjdz, zapisz, wczytaj, sprawdz, ze lezy. (c) "Your house" ->
  "Sleep in your bed": sen do switu, po pobudce "Rested in your own house: until ...", w rozpisce predkosci "Slept in your own house". (d) Wjedz do Mudgrave -> "Visit Seven
  Elms (your village)" -> "Take a walk": scena wsi, wyjscie -> z powrotem menu wioski; "Trade" tam = zwykly handel wsi. Linie logu: `Wioska gracza - dom (r4-dom): handel
  w ...`, `... dom w ... - wejscie`, `... nocleg we wlasnym domu ...`, `... skrzynia domu otwarta w ...`, `... skrzynia domu zamknieta: ...`,
  `... skrzynia w zapisie (...)` (po zapisie z rzeczami w skrzyni), `... skrzynia z zapisu: ...` (po wczytaniu), `... spacer w ... - scena wsi okregu ...`, linia doby
  `... dzien N | klucze ... | handel ... | dom ... | spacery ... | potkniecia ...`; potkniecia: `ERROR in SideVillageHome.`.
  (e) Sprawdzenie skrzyni po wczytaniu: wloz rzecz, zapisz - w logu `skrzynia w zapisie (...): sh1|<uid>|<przedmiot>~~<sztuk>`; wczytaj - `skrzynia z zapisu: 1 rodzajow`.
