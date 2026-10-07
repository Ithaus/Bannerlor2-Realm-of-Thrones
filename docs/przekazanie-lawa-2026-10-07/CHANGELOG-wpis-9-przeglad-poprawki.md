<!-- GOTOWE TEKSTY WPISOW CHANGELOG - poprawki po niezaleznym przegladzie stosu n131b..n131j (07.10, workflow wf_bd58f0e5-4ca: 5 wymiarow,
     34 zgloszenia, kazde sprawdzane przez 3 sceptykow; potwierdzone 10 = 6 roznych usterek). NIC nie wgrane na stale, nic nie wypchniete, repo Jeffa nietkniete.
     Commity (klon LANCUCH, TU = scratchpad f16095a4\lawa):
       galaz n131h-puste-kasy-kuznia-za-dzien (worktree TU\repo), na c471705:
         1fe5c35 NN: DOBA KUZNI W JEDNYM REJESTRZE Z KARNETEM BK (W ZAPISIE GRY); KAZDA ROBOTA PLACI, TAKZE POD NIEOBECNOSC GRACZA
         100112a NN: NAPRAWA ZALOZONEJ SZTUKI ZOSTAJE W KSIEDZE STANU; APEL KWATERMISTRZA WYCENIA NAPRAWE JAK "SEND THE MEN'S WORN GEAR"
         ad6f6ce NN: TURNIEJE - PULA GRACZA WRACA, GDY GRA ODWOLA TURNIEJ (OBLEZENIE)
       galaz n131i-konie-i-zwierzeta (worktree TU\repo-konie), na 22336e0:
         7d919ee NN: KON - JEDNA CENA NA TARGU (REKRUT, HODOWCA I LORD Z POLKI JAK NOTABL I GRACZ)
     Bez nowych ustawien (gen_mcm bez zmian). Build kod 0 (Armoury: stare ostrzezenie CS0169).
     Proba poza gra: TU\proba 118/118 (bylo 111; +7 sprawdzen), TU\proba-konie 136/136 (bylo 135), TU\proba-zamowienia 130/130; baza 7/7.
     Stary DLL (c471705) na nowej probie: 107/108 - oblewa sekcje 16 (ksiega po naprawie trzymala rl_looted).
     Proba zlozenia: galaz n131k-sklad-proba (worktree TU\repo-sklad) = ad6f6ce + merge n131i (z 7d919ee) + merge n131j - bez konfliktow, gen_mcm bez zmian
     (Armoury 647, GrandTourney 40, RealisticCaptivity 104), build 3 x kod 0, proby 118/118, 136/136, 130/130; DLL TU\dll\sklad. -->

## 2026-10-07 (NN) - KUZNIA: DOBA W JEDNYM REJESTRZE Z KARNETEM BK, W ZAPISIE GRY; PLACI KAZDA ROBOTA, TAKZE GDY KOWAL PRACUJE BEZ CIEBIE
**Mod:** Armoury | **Pliki:** `Forge.cs` (`PayDayRent` - bez slownika `_rentDay`; `ForgeFee`), `Patches.cs` (`DayPass`: NOWE `ActiveAt`, `CoversHere`, `Buy`, `Clear`; `EnsureBought` bez pieniedzy nie wydaje karnetu; `HourlyPostfix`), `ArmouryBehavior.cs` (petla projektow: oplata dla kazdej roboty, ktora idzie naprzod; `OnNewGameCreatedEvent` -> `DayPass.Clear`).

**Problem:** przeglad stosu (3 potwierdzenia na 3): (1) dzien kuzni wlasnego projektu (`Forge.PayDayRent`, dzien kalendarza) i karnet BK (`DayPass`, doba od zaplaty) mialy osobne rejestry - gracz, ktory zaczal projekt, a potem kul w ekranie BK, placil ten sam dzien drugi raz; (2) rejestr dni projektu byl tylko w pamieci - wczytanie gry z tego samego dnia dawalo darmowy dzien, a restart gry kazal placic drugi raz; (3) oplata szla tylko przy kowadle i tylko przy wlaczonym jednym zegarze kuzni (`ForgeOneClock`) - kowal pracujacy pod nieobecnosc gracza (`ForgeWorksWithoutYou`, `ForgeOnlyWhileThere` wylaczone) i projekt przy wylaczonym zegarze szly za darmo (10-dniowy projekt t6: 3 d zamiast 30). Do tego karnet BK przy pustej kiesie byl wydawany za to, co gracz mial (takze 0).

**Przyczyna:** `_rentDay` (statyczny slownik dni) obok `DayPass._paid` (zapis `arm_daypass`); warunek `atForge && ForgeClock.On` w petli projektow; `EnsureBought` bez sprawdzenia zlota.

**Zmiana:** `PayDayRent(osada)` placi DOBE kuzni (`DayRentFee`, 24 h od zaplaty) przez `DayPass.Buy` do kasy osady, w ktorej lezy robota - jeden rejestr z karnetem (zapisywany w grze). Placi kazda robota, ktora idzie naprzod: wlasny projekt, sztuka z ekranu BK, bron kuta po vanillowemu - przy kowadle i zdalnie; bez pieniedzy robota czeka (komunikat raz na dzien jak dotad). Karnet BK bez pieniedzy nie jest wydawany. Oplacona doba zwalnia z oplat godzinowych i oplaty za projekt takze przy wylaczonym karnecie (`CoversHere`). Nowa gra w tej samej sesji czysci doby z poprzedniej. Przy wylaczonym `ForgeHireHistorical` - jak dotad (bk / van: karnet przy kowadle).

**Przyklady (proba, sekcja 12, srednie miasto, dzien kuzni 3 d):** projekt doba 1: -3; ekran BK w tej samej dobie: 0 (dotad -3); ekran BK w dobie 2: -3; projekt w dobie 2: 0; kowal w town_b pracuje bez ciebie: -3 do kasy town_b (dotad 0); po zapisie i wczytaniu gry ta sama doba: 0; przy 2 d w kiesie: karnetu nie ma, robota czeka.

**Ryzyko / co sprawdzic:** REGRESJE - doba liczy sie od zaplaty (24 h), nie od polnocy - ta sama zasada co karnet BK od 31.08 ("DOBA = 24 h OD ZAPLATY"); przy wylaczonym `ForgeHireHistorical` bez zmian. KOLIZJE - zapora `SwallowForgeFee` przepuszcza nasze przelewy (`DayPass.Charging` w `Buy`). SPOJNOSC - jeden rejestr, jedna cena dnia (`DayRentFee`), do kasy osady kuzni. OTWARTE (niskie, nie poprawiane): przy wylaczonym jednym zegarze kuzni (`ForgeOneClock`) i braku karnetu stawka godzinowa BK 3/8 d obcina sie w BK do 0. **Co Jeff zobaczy:** kucie w ekranie BK w dniu, w ktorym projekt juz oplacil kuznie, nic nie kosztuje; kowal pracujacy pod nieobecnosc bierze dzien kuzni; bez pieniedzy robota czeka. **Status:** NIEWGRANE - DO SPRAWDZENIA.

## 2026-10-07 (NN) - NAPRAWY: OPLACONA NAPRAWA ZALOZONEJ SZTUKI ZOSTAJE W KSIEDZE STANU; APEL KWATERMISTRZA WYCENIA NAPRAWE TAK, JAK OPCJA "SEND THE MEN'S WORN GEAR"
**Mod:** Armoury | **Pliki:** `ArmouryBehavior.cs` (`ResetSlotCondition`), `SmithMenu.cs` (`KitReportConsequence`).

**Problem:** przeglad stosu (3/3): (1) "Pick a piece - the smith" przy zalozonej sztuce (np. helm Plundered) zdejmowal modyfikator, ale ksiega stanu uprzezy trzymala Plundered jako "oryginal" - po nastepnej bitwie "Mend everything you wear" przywracal helmowi Plundered (gracz placil dwa razy, a dostawal sztuke gorsza od oplaconej). Blad starszy niz paczki (wspolna petla przywracania). (2) Apel kwatermistrza ("Muster the men's kit") przy regule kowali miasta wycenial naprawe polek starym rachunkiem: wraki w cenie, bez materialu, rabat liczony takze od wrakow - inna liczba niz opcja "Send the men's worn gear", ktora potem naprawde placi.

**Przyczyna:** `ResetSlotCondition` ustawial tylko stan 100, zostawiajac modyfikator z pierwszego wpisu; `KitReportConsequence` wolal `ScanTroopWorn` niezaleznie od `MarketRule`.

**Zmiana:** `ResetSlotCondition` zapisuje w ksiedze nowy stan sztuki (modyfikator po naprawie) jako oryginal. Apel przy `MarketRule` liczy `PlanTroops` - ta sama wycena i ten sam opis co opcja naprawy polek: ile sztuk kowale wezma za monete, robota (z rabatem) + material z targu, co czeka i dlaczego (`LeftEn`).

**Przyklady (proba, sekcja 16):** helm Plundered naprawiony u kowala za 26 zl; ksiega po naprawie "5|100||casterly_heavy_helm" (dotad "5|100|rl_looted|..."); po bitwie (stan 40) naprawa uprzezy oddaje helm bez modyfikatora.

**Ryzyko / co sprawdzic:** REGRESJE - naprawa uprzezy dalej przywraca dobre oryginaly (np. Masterwork), bo "Pick a piece" naprawia tylko sztuki zuzyte. SPOJNOSC - apel i opcja naprawy polek pokazuja te sama kwote. OTWARTE (niskie): ta sama zalozona sztuka ma dwie ceny kowala - "Pick" od modyfikatora, naprawa uprzezy od stanu w ksiedze. **Co Jeff zobaczy:** po naprawie pojedynczej sztuki nic jej nie cofa; apel kwatermistrza podaje kwote, ktora potem zaplaci. **Status:** NIEWGRANE - DO SPRAWDZENIA.

## 2026-10-07 (NN) - TURNIEJE: PULA GRACZA WRACA, GDY GRA ODWOLA TURNIEJ (OBLEZENIE MIASTA)
**Mod:** GrandTourney | **Pliki:** `TourneyBehavior.cs` (NOWE `OnTournamentCancelled`, `RefundPurse`; dzienny tick - wpis bez turnieju zwraca pule).

**Problem:** przeglad stosu (3/3, waga wysoka): pula gracza od paczki turniejow (e010a25) czeka na zwyciezce poza kiesa. Gdy miasto zostalo oblezone, gra sama odwolywala turniej (`ResolveTournament` -> zdarzenie `TournamentCancelled`, nie `TournamentFinished`); GrandTourney tego nie sluchal, a dzienny tick zdejmowal wpis bez zwrotu - pula (np. 15 000) przepadala.

**Przyczyna:** brak sluchacza `CampaignEvents.TournamentCancelled`; galaz `game == null` w ticku usuwala wpis bez rozliczenia.

**Zmiana:** `OnTournamentCancelled` zwraca pule (przy stawkach historycznych cala, inaczej `CancelledFeeRefund` - jak przy innych odwolaniach) i wycisza przejmowanie turnieju; wpis bez turnieju w ticku tez zwraca pule przed zdjeciem. Nasze wlasne odwolania zdejmuja wpis PRZED `ResolveTournament`, wiec zwrotu nie ma dwa razy. Komunikat: "The tourney at X is called off before a champion was crowned. N gold of your purse comes back to you."

**Przyklady (proba, sekcja 15):** pula 5000, oblezenie: gracz +5000 (dotad 0); drugie zdarzenie: +0; turniej AI bez puli: +0; przy wylaczonym `HistoricalTownRates`: zwrot wedle `CancelledFeeRefund`.

**Ryzyko / co sprawdzic:** REGRESJE - zwykle zakonczenie (`TournamentFinished`) bez zmian. SPOJNOSC - kazde odwolanie oddaje pule ta sama regula. **Co Jeff zobaczy:** po oblezeniu miasta z ogloszonym turniejem pula wraca z komunikatem. **Status:** NIEWGRANE - DO SPRAWDZENIA.

## 2026-10-07 (NN) - KONIE: JEDNA CENA KONIA NA TARGU - REKRUT KONNY, HODOWCA I LORD Z POLKI PLACA TO, CO NOTABL I GRACZ
**Mod:** Armoury | **Pliki:** `Stables.cs` (NOWE `ShelfPrice`; `MarketPrice`, `PriceOf`).

**Problem:** przeglad stosu (potwierdzone 3/3 i 2/3): paczka 7a (e10b449) liczyla konia rekruta i hodowcy przez `Town.GetItemPrice` bez kupca - wtedy prawo podazy i popytu Armoury (`SupplyDemand`, `RetailFromWorth`) nie dziala, a liczy lancuch cudzych modeli cen (ok. 2 x wartosci) i wspolczynnik gry dla koni. Notabl kupujacy konia ochotnikowi (`VolunteerKit`) i gracz na straganie placa cene z kupcem-miastem. Ten sam kon mial na jednym targu trzy ceny; notabl placil za konia inaczej, niz lord mu potem oddawal przy werbunku.

**Przyczyna:** `Town.GetItemPrice(el, null, false)` -> `MarketData.GetPrice(el, null, false)` z `merchantParty = null` (sprawdzone w kodzie gry 1.4.8), a `SupplyDemand.PricePostfix` dziala tylko przy znanym kupcu.

**Zmiana:** `Stables.ShelfPrice` = `MarketData.GetPrice(kon, null, false, partia miasta)` - ten sam wzor co `VolunteerKit`, stragan gracza i `AiGear`. Uzywaja go rekrut konny (`MarketPrice` -> `RecruitCost`), hodowca (`BreederPrice`) i lord kupujacy konia z polki (`PriceOf`, przy `HorsesAtMarketPrice`; wylaczone - jak dotad).

**Przyklady (proba koni, z atrapa podazy i popytu przy kupcu x1.5):** kon wierzchowy w town_a: notabl / gracz 1080, rekrut 1080, hodowca 1080, lord z polki 1080 (dotad bez kupca 720).

**Ryzyko / co sprawdzic:** EKONOMIA - przy pustej polce podaz i popyt podnosza cene (do `SupplyDemandMaxFactor`), przy nadmiarze koni (stan swiata wg DIAGNOZA-NADMIARU-KONI) obnizaja - konie lordow i rekrutow pojda za rynkiem; ceny pokaze linia "PodazPopyt" i "RecruitCost" w logu. KOLIZJE - `SupplyDemand.PricePostfix` liczy raz (licznik zagniezdzenia). **Co Jeff zobaczy:** kon kosztuje tyle samo, kto by go nie kupowal na tym samym targu. **Status:** NIEWGRANE - DO SPRAWDZENIA.
