## m96-wies - menu wsi uporzadkowane jak miasto i zamek + komunikaty platnosci z powodem (Armoury, RealisticCaptivity, GrandTourney)

**Dla Jeffa (prosto):** (1) Te "You paid 2" w Seagard to nie byl nocleg w karczmie, tylko **wynajem kuzni na dobe** - przy przetopie Banner Kings kaze czekac
ok. 23 h, wiec wyglada to jak noc (log: 5 x "kuznia w Seagard oplacona na 24h za 2" miedzy 16:07 a 16:23, noclegow w karczmie 0). Teraz zamiast golego "You paid 2"
jest jeden komunikat: "You paid 2 to hire the forge in Seagard for a full day, paid up front." Tak samo wszedzie, gdzie placisz albo dostajesz zloto przez nasze mody,
np. w karczmie: "You paid 2 for your night at a common room in Seagard (bed 1, horse 1). Your men paid 14 from their purses." - nocleg w karczmie i zajezdzie, kuznia na dobe,
ksiazki, okup, zlecenia, sprzedaz warsztatu, okup wsi, wplaty karawan, lapowki, golibroda, kolejka w karczmie, dom, dlug okupu, turniej. Kwoty sie nie zmieniaja.
(2) Menu wsi jest teraz krotkie jak menu miasta i zamku: zostaja werbunek, kupno towarow, spacer, "Take a hostile action" (rabunek, zapasy, rekruci, okup - jak
dotad w jednym podmenu gry), Twoj dom, Twoja wioska, czekanie i wyjazd, a ksiegi, okreg i prace okregu, Living Economy, Banner Kings i kupno domu sa w **Village affairs**,
dniowka w **Look for paid work** (jak w miescie). Wylaczniki: MCM Armoury -> "The village menu" -> **Village Menu Tidy** i "Payment messages" -> **Player Payment
Reasons** (ten sam klucz jest tez w MCM RealisticCaptivity i GrandTourney).

**Problem:** Jeff 20.10 (gra na 95a): (A) "You paid w miescie 2 - powinno byc, za co: za nocleg w karczmie." (B) "Wioski menu tez ustaw, aby byly poszeregowane jak w
miescie i zamkach."

**Dowod (dekompilacja i logi, nie zgadywane):**
- (A) "You paid N" / "You received N" to `SandBox DefaultNotificationsCampaignBehavior.OnHeroOrPartyTradedGold` (teksty `str_gold_removed_with_icon` /
  `str_you_received_gold_with_icon` z Native module_strings, dzwiek monet) - pokazywany po kazdym `GiveGoldAction.*` z `disableNotification = false`, gdy gracz jest
  po jednej stronie (`GiveGoldAction.ApplyInternal` -> zdarzenie `HeroOrPartyTradedGold(..., showQuickInformation)`). `ChangeHeroGold` komunikatu nie daje.
- (A) **Zrodlo "You paid 2" potwierdzone w logu (poprawka po recenzji - pierwsza wersja wpisu go nie znalazla):** to dobowa oplata za kuznie (`DayPass.EnsureBought`,
  Patches.cs) w Seagard. `D:\Backup-Bannerlord\logi-jeffa\2026-10-20\Armoury-2026-10-20_14-53-56.log`: 5 x `DayPass: kuznia w Seagard oplacona na 24h za 2.`
  (16:07:47, 16:08:50, 16:11:34, 16:22:22, 16:23:38), tuz obok `Jeden zegar kuzni: ekran BK 23.3 h ... odsiadka BK za przetop / rafinacje 23.3 h` i `Przetop broni
  (n20-kuznia)`; w `Armoury-2026-10-20_10-24-16.log` 3 x to samo w Seagard (10:51:38, 10:59:28, 11:09:07), w innych miastach po 3-4 (The Twins, Harrenhal, The Eyrie,
  Riverrun ...). BK przy przetopie kaze odsiedziec ok. 23 h - stad skojarzenie z noclegiem. Kod przed zmiana: `ApplyForCharacterToSettlement(Hero.MainHero, s, rate)`
  bez flagi = komunikat gry "You paid 2" i osobny zolty "The forge is yours for a full day - 2 gold, paid up front.". W tych samych logach noclegow w karczmie 0
  ("Karczmy (KA): dzien ... noclegi 0" kazdej doby; linie "Nocleg: gracz ..." to oboz w marszu) i oplat E8/D2 0 ("Wydatki gracza (E8/D2)": oplaty 0). Biezacy
  `Modules\Armoury\Armoury-2026-10-20_14-53-56.log` w folderze gry (ostatni zapis 16:27:36) - to samo. Karczma i zajazd i tak dostaly powod (to tez placenie gracza
  w miescie), a **zwiad** (nizej) pokaze kazde inne miejsce.
- (A) Przejrzane wszystkie `GiveGoldAction.*` w Armoury, RC i GT (376 wywolan): te z komunikatem gry dla gracza i bez `true` - lista w tabeli "Zmiana (A)". Reszta
  juz przelewa z `disableNotification = true` albo nie dotyczy gracza (AI).
- (B) Wszystkie DLL w `bin` i `Modules\*\bin\Win64_Shipping_Client` przeszukane na napis "village" (sterta #US) przy `AddGameMenuOption`, potem zdekompilowane
  (`dectool grep '"village"'`): opcje menu `village` (21 - zgadza sie z linia Jeffa 14:56:35 "Look over the district wpieta: village index 2 z 21"):

| Opcja (napis) | id | Mod / klasa | Warunek (skrot) | Skutek / powrot |
|---|---|---|---|---|
| Recruit troops | `recruit_volunteers` | gra PlayerTownVisitCampaignBehavior | warunek gry | skutek takze w `[GameMenuEventHandler("village", "recruit_volunteers")]` -> **nieprzenaszalne** |
| Buy products | `trade` | gra | wies | skutek `null`, prawdziwy w `[GameMenuEventHandler("village", "trade")]` -> **nieprzenaszalne** |
| Take a walk through the lands | `village_center` | gra | wies | misja, powrot do `village` |
| Take a hostile action | `hostile_action` | gra VillageHostileActionCampaignBehavior | gracz moze napasc | podmenu gry `village_hostile_action` (rabunek, zapasy, rekruci, nasz "Demand tribute" p6-okup) |
| Wait here / Return to Army / Disembark / Set Sail / Leave (2x) | `village_wait`, `village_return_to_army`, `disembark`, `leave_set_sail`, `leave_at_sea`, `leave` | gra (opcje morskie z gry, NavalDLC tylko warunki) | - | - |
| Collect revenue | `revenue_farming_quest_collect_tax_menu_button` | gra RevenueFarmingIssueBehavior (index 4) | zadanie | zadanie |
| Banner Kings | `bannerkings_village_submenu` | BK BKSettlementActions (index 2) | osada z ludnoscia BK | menu `bannerkings` (demesne, tytuly, Village Projects, ziemia); Leave -> `village` |
| Living Economy | `bee_village_root` | BEE SettlementMenuBehavior (index -1) | wies | menu `bee_village_menu`, Back -> `village` |
| People of this land / Estates and livelihood | `arm_people_village`, `arm_estates_village` | Armoury PeopleMenu (6), DailyLife.View (7) | klucze | okna (ShowInquiry / ShowMultiSelectionInquiry). "Realm ledger" NIE ma wsi (tylko miasto i zamek) |
| Look over the district / Invest in the district's works | `arm_district_village`, `arm_works_village` | Armoury DistrictMenu (7), DistrictWorks.Menu (8) | klucze, ksiega wiosek | okna |
| Visit {wioska poboczna} | `arm_side_fief_visit` | Armoury SideVillageFief (9) | wioska poboczna gracza w tym okregu | menu `arm_side_fief` (Rest the night here, Invest in its works, Look over your village, Trade, Your house, Take a walk, Leave - wlasne menu, uporzadkowane indeksami 0-5 i 9, bez zmian); Leave -> `village` |
| Buy a house / Go to your house | `rc_home_buy_village`, `rc_home_enter_village` | RC Homes (index 4) | brak domu / dom | okno kupna -> `rc_home`; "Step back outside" -> `Outside()` = `village` |
| Hire yourself out for day labour | `rc_work_labour_village` | RC Work (index 5) | warunki RC | menu oczekiwania `rc_work_labour`, koniec -> `BackMenu()` = `village` |

- Nie dodaja nic do menu `village`: ROT (tylko `ActivateGameMenu("village")` przy zaciagu), RB, DTE, NavalDLC (osobny DLL bez napisu), GT, ForgeView, CrashScribe, SandBox
  (FamilyFeud tylko patrzy na id menu), StoryMode (samouczek). AIInfluence zaciemniony - w jego napisach brak id opcji menu (sprawdzone); jesli cos dodaje, zostaje we wsi
  (linia logu wypisuje cale menu przed i po).
- `GameMenuEventHandler("village", ...)`: `trade`, `recruit_volunteers` (PlayerTownVisitCampaignBehavior) oraz `buy_goods`, `manage_production`, `recruit_volunteers`
  (PlayerTownVisit; dwa pierwsze bez opcji we wsi) - wszystkie w straznik `VillageOnly`.
- Zaden warunek przenoszonej opcji nie patrzy na id biezacego menu (grep `GameMenu.StringId` w Armoury i RC; BK, BEE w dekompilacji); skutki to okna albo wlasne menu
  z powrotem do `village`.

**Przyczyna:** (A) nasze przelewy gracza wolaly `GiveGoldAction` z komunikatem gry (domyslne `disableNotification = false`), a powod (jesli byl) szedl osobnym
komunikatem albo wcale. (B) kazdy mod dokleja opcje do menu gry `village` wedlug wlasnego indeksu.

**Zmiana (B) - menu wsi:** nowy plik `Armoury/src/VillageMenus.cs` (wzor `CastleMenus`, osobna klasa), nowy klucz **Village Menu Tidy** (`Settings.VillageMenuTidy`,
grupa "The village menu", domyslnie wlaczony; MCM Jeffa tego klucza nie ma). Wpiecie: `SubModuleMain.OnAfterGameInitializationFinished` zaraz po `CastleMenus.Apply`
(osobny try) - ten sam moment co miasto i zamek. Opcje przechodza jako TE SAME obiekty `GameMenuOption`.

| Opcja | Skad | Nowe miejsce |
|---|---|---|
| People of this land, Estates and livelihood | Armoury | **Village affairs** (nowe podmenu `arm_vm_affairs_menu`) |
| Look over the district, Invest in the district's works | Armoury | **Village affairs** |
| Living Economy | BEE | **Village affairs** |
| Banner Kings | BK | **Village affairs** (jak w miescie - werbunek wsi to "Recruit troops" gry, zostaje na wierzchu; w zamku BK zostal, bo tam jest werbunek) |
| Buy a house in this place | RC | **Village affairs** (jak w miescie) |
| Hire yourself out for day labour | RC | **Look for paid work** (nowe podmenu `arm_vm_work_menu`, jak w miescie; jedna opcja, ta sama nazwa co w miescie) |
| Recruit troops, Buy products, Take a walk, Take a hostile action, Visit {wioska}, Go to your house, Collect revenue, Wait, Return to Army, Disembark, Set Sail, Leave | gra / Armoury / RC | **zostaja w menu wsi** |

- **Wrogie czyny zostaja na wierzchu** (bezpieczniej): gra juz trzyma je w jednym podmenu "Take a hostile action" (`village_hostile_action` - rabunek, wymuszenie zapasow
  i rekrutow, nasz "Demand tribute" p6-okup), wiec drugie podmenu "Hostile actions" byloby podmenu w podmenu i ruszaloby powroty gry (`SwitchToMenu("village")` z
  `village_hostile_action`, zadania gry). Nic tam nie zmieniam.
- Wejscia "Village affairs" i "Look for paid work" staja w menu wsi przed "Wait here for some time" (bez niego - przed pierwszym Leave, na koncu); widoczne, gdy choc
  jedna opcja w srodku jest widoczna (warunki opcji wolane jak w grze, na swiezym `MenuCallbackArgs`), znaczniki zadan "!" przechodza na wejscie, podpowiedz wymienia
  widoczne opcje. Podmenu: tytul ("Village Affairs", "Paid Work"), opis - zdanie na kazda widoczna opcje, tlo wsi, na dole "Back to the village" (-> `village`).
- Wspolny wzorzec z miastem i zamkiem: ten sam moment, `WeakReference<GameMenuManager>`, puste podmenu rejestrowane takze przy wylaczonym kluczu, przywrocenie z
  kopii przy bledzie w polowie, flaga `committed`, potkniecia per miejsce (`VillageMenus.<miejsce> (potkniecie N)`), napisy "Back to the ...".
- Linie logu okregu: `DistrictMenu.Where` i `DistrictWorks.MenuText` znajduja opcje w podmenu (`VillageMenus.MovedTo`) i pisza `village -> arm_vm_affairs_menu index K z M
  (przed: .., po: ..)` zamiast "BRAK pozycji" / "index -1"; `tools/sprawdz_logi.py` E7/W3 T1 i NPO-0 przyjmuja oba zapisy (sprawdzone na obu napisach).

**Zmiana (A) - komunikaty platnosci:** nowy plik `Armoury/src/PayNotes.cs` (+ kopie `RealisticCaptivity/src/PayNotes.cs`, `GrandTourney/src/PayNotes.cs`), nowy klucz
**Player Payment Reasons** w kazdym z trzech modow (grupa "Payment messages", domyslnie wlaczony; nowy - MCM Jeffa go nie ma). Przy wlaczonym kluczu przelew gracza idzie
z `disableNotification = true` i jest JEDEN komunikat (bialy, ikona monety, ten sam dzwiek co gra): nowy z powodem (`Paid` / `Got`) albo dotychczasowy nasz, ktory juz
mowil powod (`Silent` - tylko licznik). Wylaczony = komunikat gry jak dotad (Armoury - od nastepnej godziny gry, McmSettings.Apply co godzine; RC i GT - po
wczytaniu zapisu, jak wszystkie ich klucze MCM). Kwoty, platnicy i odbiorcy bez zmian (ta sama metoda GiveGoldAction,
ten sam MoneyLedger/RealPayer - zdarzenie gry idzie dalej, zmienia sie tylko flaga komunikatu).

| Miejsce | Plik | Komunikat (klucz wlaczony) |
|---|---|---|
| Nocleg w karczmie miasta | Armoury TownInns.cs (+ TownInns.PayNote.cs) | "You paid 2 for your night at a common room in Seagard (bed 1, horse 1). Your men paid 14 from their purses. Your companions paid 3." (jeden na cala druzyne; towarzysze placili juz cicho) |
| Nocleg w zajezdzie | Armoury TownInns.Road.cs | jak wyzej ("... in The Crossroads Inn ...") |
| Kuznia na dobe (DayPass) - **to bylo "You paid 2" Jeffa** | Armoury Patches.cs | "You paid 2 to hire the forge in Seagard for a full day, paid up front." (zamiast "You paid 2" gry + zoltego "The forge is yours ...") |
| Ksiazki u ksiegarza / z celu BK | Armoury CraftOrders8.cs, Wyceny20.cs | "You paid N for {ksiazka} ({ksiegarz})." |
| Okup gracza (menu niewoli) | Armoury RansomFlows.cs | "You paid N as your ransom, to {odbiorca}." |
| Zlecenie lorda (tablica) | Armoury Orders.cs | "You received N from {lord} for the {rzecz}. Word of your craft spreads." |
| Sprzedaz warsztatu | Armoury WorkshopTrade.cs | "You received N from {kupiec} for your workshop." |
| Zmiana produkcji warsztatu gracza (nowy sprzet) | Armoury WorkshopTrade.cs (ConvertPostfix) | "You paid N to the craftsmen of X for new tools to change your workshop's trade." (dopisane po recenzji; warsztat AI bez zmian) |
| Okup wsi (Demand tribute) | Armoury RaidTribute.cs | "You received N: tribute from the elders of X. Your realm will spare the village for D days." |
| Zysk karawany | Armoury Seal31B.cs | "You received N: the {karawana} deposited its profit with you in X." |
| Sakiewka pobitych wiesniakow | Armoury VillageTakings.cs | "You received N: the purse of the beaten villagers of X." |
| Lapowka strazy, odbicie wieznia, golibroda, wiesci karczmarza, kolejka, wiesci o ludziach do najecia (E8/D2) | Armoury PlayerFees8.cs | "You paid N to the barber." / "You paid 2 to the innkeeper for word of people looking for work." itd. (tylko gdy gra pokazalaby komunikat) |
| Rata dlugu okupu | RC CaptivityBehavior.cs | "You paid N as an instalment on your ransom debt - D still owed." / "... - the debt is paid in full." |
| Zloto zabrane przy pojmaniu, lapowka za ucieczke, okup przez poslanca, skarbiec domu | RC CaptivityBehavior, Drobne39, Rescue, Homes | komunikat gry wylaczony, zostaje nasz dotychczasowy ("They emptied your purse ...", "X bribed a guard for you", "... negotiated your release ...", "N gold rests under your own roof now.") |
| Wykup rynsztunku, kupno i sprzedaz domu | RC CaptivityBehavior, Homes | "You paid N to buy back your war gear." / "You paid N for the house in X. It is yours." / "You received N for your house in X (H for the house, S from its strongbox). It has a new owner." |
| Zwrot oplaty odwolanego turnieju | GT TourneyBehavior.cs | "You received N: your hosting fee back - war has broken out and the tourney at X is called off before it began." (zamiast dwoch komunikatow) |
| Pula mistrza, utarg, oplata gospodarza, zwrot puli | GT TourneyBehavior.cs | komunikat gry wylaczony, zostaje nasz dotychczasowy ("The champion's purse is yours", "... gold in takings", "It cost you N gold", "... comes back to you") |

- AI bez komunikatow: kazdy nowy komunikat tylko przy graczu (gracz placi albo dostaje); przelewy AI bez zmian.
- **Zwiad (tylko odczyt, Armoury):** nasluch zdarzenia gry `HeroOrPartyTradedGold` (raz na kampanie, `WeakReference<Campaign>`): gdy gra MIMO TO pokazuje graczowi
  komunikat (flaga zdarzenia `showNotification` - pierwszy warunek, dla AI koszt zero), linia `Komunikaty platnosci (m96-wies): komunikat gry bez powodu (You paid N) |
  gracz -> ... | wolajacy: <do 5 ramek stosu poza GiveGoldAction / CampaignEvents / MbEvent>` (pierwsze 3 z kazdego miejsca, potem co 50.). Tak znajdziemy zrodlo
  "You paid 2", jesli to nie byla karczma (np. cudzy mod), i nastepne miejsca (np. RealPayer.PayHeroes, nagrody zadan QuestPay8, turniej TourneyPay - tam komunikat
  gry zostal, bo ekran zadania / turnieju mowi powod).

**Linie logu:**
- Armoury.log, kazde wczytanie: `Menu wsi (m96-wies): start | WLACZONY (Village Menu Tidy) | menu wsi przed 21 opcji: <id po kolei> | przeniesione 8 z 8 w planie:
  arm_vm_affairs_menu 7 [...]; arm_vm_work_menu 1 [rc_work_labour_village] | wejscia podmenu wpiete 2 (przed 'village_wait' (index K)) | nie znaleziono cudzych 0,
  naszych 0 | menu wsi teraz 15 opcji: <id po kolei>`. Wylaczony klucz: `... WYLACZONY w MCM (Village Menu Tidy) - menu wsi jak dotad ...`.
- Armoury.log, kazde wczytanie: `Komunikaty platnosci (m96-wies): start | WLACZONY (Player Payment Reasons) ... | nasluch zwiadu: wpiety ...`.
- RealisticCaptivity.log / GrandTourney.log, kazde wczytanie (OnSessionLaunched): `Komunikaty platnosci (m96-wies): nowa sesja | liczniki od zera | klucz
  PlayerPaymentReasons teraz: TAK|NIE` (nie "start" - PN-0 czyta linie startu Armoury).
- Armoury.log / RealisticCaptivity.log / GrandTourney.log, przy kazdej podmianie: `Komunikaty platnosci (m96-wies): zaplata inn 2 | podmienione w sesji N (zaplaty ..,
  wplywy ..) | wg powodu: inn 1, ...` (RC/GT: `zaplata|wplyw|komunikat gry wylaczony (nasz mowi powod) <powod> <kwota> | podmienione w sesji N | wg powodu: ...`).
- **Oczekiwane w autotescie** (pelny zestaw modow Jeffa): przeniesione **8 z 8** (sprawy wsi 7, praca 1), nie znaleziono cudzych **0**, naszych **0**, wejscia **2**,
  menu wsi przed **21**, teraz **15** (na liscie "teraz" `recruit_volunteers`, `trade`, `village_center`, `hostile_action`, `arm_side_fief_visit`, `rc_home_enter_village`,
  `arm_vm_affairs`, `arm_vm_work`, `village_wait`, `leave`); linia startu komunikatow z "nasluch zwiadu: wpiety"; `ERROR in VillageMenus`, `ERROR in PayNotes`,
  `ERROR in OnAfterGameInitializationFinished (VillageMenus|PayNotes)` **0**. Linie miasta i zamku bez zmian (19 z 19, 10 z 10). Linia "Menu okregu (E7/W3): start":
  `village -> arm_vm_affairs_menu index 2 z 8 (przed: arm_estates_village, po: arm_works_village)`; "Prace okregu (PO): start": `village -> arm_vm_affairs_menu index 3 z 8`.
- Narzedzie: `python -I tools/sprawdz_logi.py --grupa m96wies <Armoury-*.log>` (WS-0..WS-4 menu wsi, PN-0..PN-3 komunikaty). Sprawdzone na sztucznych logach (pelny
  nowy: 10 progow 0 x NIE; opcja planu zostala + leave zniknelo + ERROR: 3 x NIE; klucz wylaczony: WS INFO; log bez paczki i log Jeffa 14-53-56: INFO, 0,8 s).

**Ryzyko / co sprawdzic (kontrola calosci):**
- Regresje menu: menu miasta i zamku nietkniete (osobna klasa, osobny klucz). Kto czyta menu `village` po id po starcie sesji: `DistrictMenu.Where` i
  `DistrictWorks.MenuText` (tylko linie logu - poprawione, szukaja w podmenu), `ShortLabels.Audit`, `BeeGifts`, `BeeSeal` (w `OnAfterSessionLaunched` - przed
  przeniesieniem, i tylko menu BEE). Gra: `IncidentsCampaignBehaviour` losuje zdarzenie wejscia do wsi przy otwarciu `village` i patrzy na "leave" (zostaje);
  "Back to the village" otwiera `village` ponownie w tej samej chwili - to samo ziarno losowania co powrot z "Take a hostile action" w samej grze (jak w zamku).
  Zadania gry patrzace na menu `village` (LandLordNeedsManualLaborers, MerchantArmyOfPoachers, FamilyFeud) - jak przy kazdym powrocie do menu wsi.
- Cudze powroty (BK Leave, BEE Back, dom RC, koniec dniowki) laduja w glownym menu wsi, nie w podmenu - jak w miescie i zamku.
- Zapis zrobiony w podmenu i wczytany na DLL sprzed m96 (cofniecie): gra nie zna `arm_vm_*` i konczy spotkanie (druzyna na mapie przy wsi) - jak przy kazdym usunietym
  menu moda. Bez stanu w zapisie, bez latek Harmony.
- Komunikaty: zmienia sie tylko flaga `disableNotification` i napis. `RealPayer`/`NotableGold`/`SmallGold`/`TownPurse` (prefiksy na `ApplyInternal`) i ksiega pieniadza
  (`MoneyLedger`, nasluch `HeroOrPartyTradedGold` w AlleyProbe/VillageTakings) widza ten sam przelew - flaga komunikatu ich nie obchodzi (sprawdzone: zaden nie czyta
  `showNotification`). StatisticsCampaignBehavior (SandBox) tez nie. Jedna roznica: nasz komunikat nie ma osobnego "You paid" od gry, wiec przy kluczu wlaczonym
  na ekranie jest jedna linia zamiast dwoch.
- Zwiad: `new StackTrace(2, false)` tylko przy komunikacie gry graczowi (rzadko; AI - wyjscie na pierwszym warunku). Wlasny watek, bez `Thread.Suspend` (pulapka sampler)
  i bez czytania cudzych stosow.
- RC i GT maja wlasny klucz (osobne MCM) - wylaczenie w Armoury nie wylacza RC/GT i odwrotnie.
- Nie ruszane (komunikat gry zostaje, powod widac na ekranie gry): nagrody zadan (QuestPay8, GiverPay), nagroda/zaklady turnieju (TourneyPay, BetStake/BetWon - ekran
  turnieju), dniowka RC przy HistoricalTownRates wylaczonym (przy wlaczonym - u Jeffa - placi kasa osady bez komunikatu gry), RealPayer.PayHeroes, statki (ShipsToPort181),
  naprawy RB (cudzy mod - komunikat RB), myto i dlugi (juz `true` albo ChangeHeroGold - bez komunikatu gry). Zwiad pokaze, czy ktores z nich Jeff widzi.
- Build: Armoury, RealisticCaptivity, GrandTourney `dotnet build -c Release` kod 0 (Armoury 5 starych ostrzezen, RC 2 stare, GT 0), `tools/gen_mcm.py` - Armoury 2132
  ustawien, RC 115, GT 44. Gry nie uruchamiano (Jeff gra) - zachowanie w grze NIESPRAWDZONE.

**Status:** DO SPRAWDZENIA (galaz w-toku/m96-wies; do WGRANIA 96 po nocnym autotescie).

**Test w grze krok po kroku (Jeff):**
1. Wczytaj zapis. W `Modules/Armoury/Armoury.log` sa linie `Menu wsi (m96-wies): start | WLACZONY` (przeniesione 8 z 8, menu przed 21, teraz 15) i
   `Komunikaty platnosci (m96-wies): start | WLACZONY ... nasluch zwiadu: wpiety`.
2. Wjedz do wsi. Menu: Recruit troops, Buy products, Take a walk through the lands, Take a hostile action (we wrogiej), Visit {Twoja wioska} (jesli masz), Go to your house
   (jesli masz), **Village affairs**, **Look for paid work**, Wait here, Leave. Nie ma juz na wierzchu People of this land, Living Economy, Banner Kings itd.
3. **Village affairs**: opis na gorze, People of this land, Estates and livelihood, Look over the district, Invest in the district's works, Living Economy, Banner Kings,
   Buy a house (gdy sa) - kazda otwiera to samo okno / menu co dotad. Back z BK / Living Economy wraca do glownego menu wsi. "Back to the village" - menu wsi.
4. **Look for paid work**: Hire yourself out for day labour - dziala jak dotad, po pracy wraca do menu wsi.
5. Take a hostile action - bez zmian (raid, zapasy, rekruci, Demand tribute).
6. **To bylo Twoje "You paid 2":** w miescie, gdzie kuznia nie jest jeszcze oplacona na dzis (np. Seagard), przetop albo kucie w kuzni Banner Kings. JEDEN komunikat
   "You paid 2 to hire the forge in Seagard for a full day, paid up front." - bez osobnego "You paid 2" i bez zoltego "The forge is yours ...". W logu
   `Komunikaty platnosci (m96-wies): zaplata forgeday 2` obok `DayPass: kuznia w Seagard oplacona na 24h za 2.`
6b. Przespij sie w karczmie miasta (wspolna izba): JEDEN komunikat "You paid 2 for your night at a common room in X (bed 1, horse 1). ..." - bez osobnego "You paid 2".
   To samo w zajezdzie. Kup kolejke albo u golibrody: "You paid N for a round of drinks for your men." / "... to the barber."; zapytaj karczmarza o ludzi do
   najecia (zwiadowca, chirurg ...): "You paid 2 to the innkeeper for word of people looking for work."
6c. Turniej ogloszony przez Ciebie z pula, wygrany przez lorda AI: pula idzie do zwyciezcy, kasa miasta NIE rosnie o nia drugi raz (linia `Pula turnieju w X: N -> <imie>`;
   kasa miasta w ekranie miasta przed i po turnieju bez skoku o pule).
7. Jesli gdzies dalej widzisz gole "You paid N" / "You received N" - napisz gdzie; w Armoury.log bedzie linia `komunikat gry bez powodu (...) | wolajacy: ...`, ktora
   pokaze, skad przyszlo (np. `grep -a "komunikat gry bez powodu" Armoury-*.log`).
8. MCM Armoury -> "The village menu" -> wylacz **Village Menu Tidy**, zapisz, wczytaj: menu wsi jak dawniej (linia WYLACZONY). "Payment messages" -> wylacz **Player
   Payment Reasons**: po godzinie gry (Armoury) / po wczytaniu (RC, GT) znowu "You paid N" gry i stare komunikaty. Wlacz z powrotem.
9. Napisz, co by bylo lepiej gdzie indziej (np. Banner Kings albo dniowka z powrotem na wierzch) - przestawienie to jedna linijka w planie (oprocz "Recruit troops"
   i "Buy products" - musza zostac w menu wsi).

Potkniecia: VillageMenus i PayNotes licza per miejsce (`VillageMenus.<miejsce> (potkniecie N)`, `PayNotes.Show/OnTraded (potkniecie N)`) - nic sie nie gasi.

**Poprawki po recenzjach (20.10, trzeci commit; dwie recenzje - "gracz" i "technika"):**
- **KRYTYCZNE, prawdziwe - poprawione: GrandTourney `PayPurse`.** Wstawiona linia `if (... PayNotes.On) PayNotes.Silent("championpurse", ...)` stala miedzy
  `if (h != null && h.IsAlive) GiveGoldAction...` a `else if (town != null) town.ChangeGold(purse)` i przejela `else`. Skutek: pule gracza wygrana przez zywego
  bohatera AI (albo przez gracza przy wylaczonym kluczu) dostawal zwyciezca, a kasa miasta dostawala ja drugi raz z niczego - przy kazdym turnieju gracza z pula
  (`HistoricalTownRates` = tak u Jeffa). Teraz `if (bohater) { GiveGold...; if (gracz && klucz) Silent(...); } else if (town != null) town.ChangeGold(purse);` -
  jak przed paczka (4358d771). Przejrzany caly diff paczki pod ten sam wzor (kazde `else` za wstawiona linia): Orders, Patches (DayPass), RaidTribute, RansomFlows,
  Seal31B, TownInns.PayNote, GT (zwroty, oplata gospodarza), RC (dlug, wykup, dom, Drobne39) - wszystkie `else` naleza do zamierzonego `if` (para "nasz komunikat
  albo stary komunikat" albo zmieniona tylko linia `else`). W grze dopisany krok 6c.
- **WAZNE, prawdziwe - poprawione: zly powod dla oplaty 2 u karczmarza** (`PlayerFees8` `FDrink`). To nie napitek, tylko zaplata karczmarzowi za wiesc o ludziach
  do najecia (SandBox `TavernEmployeesCampaignBehavior.FindCompanionWithType`: `GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, 2, false)`, dialog
  `tavernkeeper_companion_info_start` "I am looking for some people to hire with specific skills..." - sprawdzone w dekompilacji). Powod teraz
  " to the innkeeper for word of people looking for work.", klucz w logu `hireinfo`, nazwa w linii doby E8/D2 "wiesci o ludziach do najecia" (stala `FDrink` i
  metoda `FolkDrink` bez zmian; `sprawdz_logi.py` nie czyta tej nazwy - grep).
- **WAZNE, prawdziwe - poprawione: diagnoza (A).** Zrodlo "You paid 2" jest w logu: kuznia na dobe w Seagard (punkt "Dowod" wyzej, tekst "Dla Jeffa", krok testu 6).
  Kod juz to pokrywal (`forgeday`); powod brzmi teraz wprost "to hire the forge in X for a full day, paid up front." (wczesniej "for a full day at the forge in X, up front.").
- Drobne - poprawione: zmiana produkcji warsztatu gracza (`WorkshopTrade.ConvertPostfix`) placila z `disableNotification = false` - teraz przy graczu i kluczu
  `true` + "You paid N to the craftsmen of X for new tools to change your workshop's trade."; warsztat AI i kwota (`_dGear`) bez zmian.
- Drobne - poprawione: RC i GT licza "podmienione w sesji" od zera w kazdej sesji (`PayNotes.Reset()` w `OnSessionLaunched`, linia "nowa sesja"); w RC rata dlugu i
  wykup rynsztunku u pojmujacego bez zywego bohatera liczyly sie w logu dwa razy (`Silent("captorhome")` w `GoldToCaptorHome` + `Paid` u wolajacego) - `Silent` usuniety,
  licznik robi wolajacy. Gracz i tak widzial jeden komunikat.
- Build po poprawkach: Armoury, RealisticCaptivity, GrandTourney `dotnet build -c Release` kod 0 (0 bledow). Gry nie uruchamiano - NIESPRAWDZONE w grze.

Poprawka po wlasnym przegladzie (20.10, drugi commit): w GrandTourney klucz stal przed `LogEnabled`, wiec `gen_mcm.py` przenosil "Log Enabled" do grupy
"Payment messages" - blok przeniesiony za `LogEnabled` (Log Enabled zostaje w "The lists themselves"); opisy, kiedy dziala wylacznik (Armoury - co godzine gry,
RC/GT - po wczytaniu), poprawione w Settings.cs, PayNotes.cs i tutaj. Build trzech modow ponownie kod 0.
