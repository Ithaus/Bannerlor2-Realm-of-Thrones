# Projekt budynkow (13.11): nowe efekty, Farmlands na liczbach, budynek wiary

Tylko odczyt: w repo i worktree n473 nic nie zmienilem. Zrodla: kod worktree n473 (`Armoury/src`), dekompilacje (`scratchpad/n472dec/bk_all.cs`, `bkrot_all.cs`, `decout/v/all_cs`), log 60 dob `Armoury-2026-11-12_22-47-01.log` (doba 109793) i CSV z `Logs/2026-11-12_22-47-01/`.

Rok = 364 doby. Punkt cywilny kosztuje ok. 31 zl, wojskowy ok. 63 zl. Tempo: miasto ok. 7 pkt/d, zamek ok. 3 pkt/d. Zwroty liczone bez inflacji.

**Najwazniejsze odkrycie przy okazji:** napiecie religijne BK jest zawsze rowne 0. Wplyw na lojalnosc (-6 x napiecie) nigdy nie dziala. Szczegoly w czesci C1.

## Kotwice liczb (log 109793)

| Wielkosc | Wartosc | Zrodlo |
|---|---|---|
| 1 jednostka jedzenia (Food) | ok. 1 szt. zboza = **9.9 zl** na targu, czyli ok. 30-32 kg zboza (dwie niezalezne miary: 9.9 / 0.31 d/kg oraz spozycie BK 0.02/chlopa/d przy ok. 0.64 kg/d) | log "Budynki (338) produkcja wsi: ceny miast: zboze 9.9"; HistoricalPrices.cs:44; bk_all.cs:39293 |
| Wydajnosc KS na pracownika na dobe | zboze 3 kg (ok. **0.094 jedzenia**), ryby 4 kg, zelazo 2 kg, kon 0.01 | StartCalibration.Tables.cs:97-122 |
| Dniowka | rzemieslnik 3 d (Settings.cs:1315), parobek ok. 1 d (szacunek historyczny) | |
| Ruda | wartosc 38 d/szt.; kopalnie zamkow: 9 szt./d za 78 zl plac, czyli ok. 8.7 zl placy na sztuke | log "Ceny surowcow", "Budynki (338) zamki" |
| Zold zalog AI | 164 598 zl/d na 67 741 ludzi, ok. **2.4 zl/czlowieka/d**; koszary i biuro oszczedzaja 46 022 zl/d | log "Budynki (338) zold" |
| Kon bojowy / zwykly | 1 821-3 940 d / 160-480 d | HistoricalPrices.cs:424-428 |
| Utarg wozow wsi | 263 694 zl/d na 571 wsi, ok. **462 zl/d na wies** | log "Daniny wsi (DS)" |
| Urzad podatkowy miasta | +12 962 zl/d w 89 miastach, ok. 146 zl/d na miasto | log "Budynki (338) urzad podatkowy" |
| Popyt zboza KS | 10 453 szt./d w 43 miastach KS, ok. 103 tys. zl/d | ks-miasta-towary.csv |

## A. Nowe efekty dla budynkow MARTWYCH, BEZ SENSU i SLABYCH

Zasady wspolne dla wszystkich ponizszych propozycji:
- Kazdy efekt ma platnika i odbiorce. Ludzie pochodza z ludnosci osady albo z wolnych rak regionu (FreeHands). Material jest prawdziwy, z polki targu (jak DsWorksRealMaterial).
- AI wybiera budowe losowo (DefaultBuildingScoreCalculationModel.cs:16-18). Wybor wedlug zwrotu to paczka 5.
- Mason i Barracks pomijam: wdrozenie idzie osobno (pkt 2 i 5).

### Miasto

| Budynek (ocena) | Nowy efekt (historycznie) | Kto placi / kto dostaje / skad ludzie | Koszt: pkt i zl, czas | Efekt na dobe | Zwrot | Kolizje |
|---|---|---|---|---|---|---|
| Training Fields (SLABY) | Plac cwiczen: XP tylko dla ludzi z bronia w zbrojowni zalogi (jak w zamku). Instruktorami sa weterani zalogi, juz oplacani zoldem | Brak nowego zlota; czas zalogi | Skala zamku: ok. 410/560/700 pkt wojskowych = 26/35/44 tys. zl; 59/80/100 dni | +XP wedlug tablicy gry, bez zmian | Niepieniezny (XP) | GarrisonArmory (bron w zbrojowni), FairXp |
| Marketplace (SLABY) | Oplata targowa (stallage, cla): 0.5/1/1.5% prawdziwego obrotu targu | Kiesa kupcow (P) i karawany oddaja do kasy miasta; dalej rata DsTowns do pana | Koszt x0.4: 1 200/1 450/1 900 pkt = 30/45/60 tys. zl; 170/210/270 dni | Ok. 26/52/78 zl/d na miasto KS (obrot jedzenia ok. 3.5 tys. zl/d; z reszta towarow x1.5) | L1 ok. 3.2 roku; calosc ok. 4.8 roku | Buildings338Caravans (x1.02-1.06 karawan) zostaje; nie dublowac z podatkiem BK (PopulationLaw.TownTaxPostfix) |
| Courthouse gry + Court House BK (SLABY / BEZ SENSU) | Jeden "sad pana" (vanilla Courthouse). Grzywny 3/6/9% raty pana DsTowns, bezpieczenstwo +0.5/1/1.5 na dobe, wplyw jak dzis. Slot BK Court House przechodzi na budynek wiary (C) | Kiesy W/R/P placa panu (grzywny jak dziesiecina, od zarobku okna, najwyzej stan kiesy) | Koszt jak urzad podatkowy: 1 800/3 000/4 200 pkt = 56/93/130 tys. zl; 0.7/1.2/1.6 roku | Ok. 44/87/131 zl/d (podstawa ok. 1 456 zl/d raty na miasto) | L1 ok. 3.5 roku; calosc ok. 6 lat | KsHunger i bunt (nizsze kiesy W); OutlawLaw: czy bezpieczenstwo tnie bandy, trzeba sprawdzic czytnik; BK Court House daje dzis stabilnosc +0.02/poziom (bk_all.cs:48582) |
| Harbor BK (SLABY) | Rybacy z dzierzawcow miasta: 40/80/120 lodzi i ludzi | Kasa miasta placi place do kiesy ludu (A17 PortsFin); ryby na polke, Plan KS je liczy (PortFishExo) | Koszt x0.6: 1 500/2 700/3 600 pkt = 46/84/112 tys. zl | 160/320/480 kg ryb/d, czyli ok. 160/320/480 zl brutto. Odejmij sprzet (KCost ryb 0.85 d na rybaka na dobe), zostaje ok. 126/252/378 zl wartosci dodanej. 480 kg wyzywi ok. 240 osob | Dla kasy miasta (marza ok. 10%): ponad 5 lat. Glowny zysk to jedzenie w KS | Buildings338Ports (338c porty pod KS); Plan KS; ryby BK RunPorts nie moga liczyc sie drugi raz |
| Mines BK (BEZ SENSU) | Kopalnia tylko tam, gdzie region ma zloze (wies okregu iron/silver/salt/clay_mine). Bez zloza budynek znika z listy, tak jak Harbor bez portu (bk_all.cs:64962). Gornicy z wolnych rak regionu | Kasa miasta placi place (ok. 8.7 zl/szt.) do kiesy ludu; ruda na polke; 1/8 regale dla pana (MineRegale) | x0.6: 900/1 500/2 400 pkt = 28/47/74 tys. zl | 3/6/9 szt. rudy/d, marza ok. 29 zl/szt., czyli 87/174/261 zl/d | L1 ok. 0.9 roku; calosc ok. 1.6 roku | Zdjac `ConstructionPerDay` z dymka (bk_all.cs:77369); rece FreeHands dziela sie z KS; dymarka i drewno (F4b) |
| Theater BK (SLABY) | Zostaje teatrem kultury, ale z prawdziwym kosztem: igrzyska oplacane z kasy miasta. Lojalnosc +0.2/0.5/1 i waga kultury pana +5/poziom (bk_all.cs:45007-45010) | Kasa miasta placi 20/40/60 zl/d kuglarzom (kiesa W) | Bez zmiany kosztu; AI buduje tylko w osadzie obcej kultury | -20/40/60 zl/d za lojalnosc | Niepieniezny | Nie mylic z budynkiem wiary (C). Theater wymaga innowacji Theater, inaczej znika z listy (bk_all.cs:64959-64961) |
| Armory BK (BEZ SENSU) | Zbrojmistrz zalogi: konserwacja i naprawa sprzetu ze zbrojowni DTE. Naprawy -10/20/30%. Zostaje BK: milicja +4%/poziom jakosci (bk_all.cs:41328-41331; audyt tego nie wymienil) | Kasa osady placi 1/2/3 platnerzom 3-10 d/d (kiesa R); naprawy mniej kosztuja pana | 1 500/2 000/2 500 pkt wojskowych = 47/62/78 tys. zl | Oszczednosc = 10-30% kosztu napraw zalogi | **Bez danych:** koszt napraw zalog trzeba zmierzyc w TroopWearLedger | GarrisonArmory (zapis zbrojowni), WearKeep, TroopSelfMend: jedna regula zuzycia |

### Zamek

| Budynek | Nowy efekt | Platnik / odbiorca / ludzie | Koszt, czas | Efekt na dobe | Zwrot | Kolizje |
|---|---|---|---|---|---|---|
| Guard House (SLABY) | Zostaje (tani): milicja i loch | - | 22/31/39 tys. zl | jak dzis | - | - |
| Craftmans Quarters (MARTWY) | Rzemieslnicy podzamcza przerabiaja surowiec wsi zamku na towar: drewno na deski (brakuje ich budowom!), skory na skore, welne na sukno (regula TownCrafts: praca = 20% wartosci) | 20/40/60 rzemieslnikow z BK (zamek ma srednio 720 rzemieslnikow); place 3 d/d (WorkshopWagePerDay) z kasy zamku do kiesy podzamcza; towar idzie na targ miasta handlowego, ktore placi kasie zamku | 11/15/20 tys. zl (jak dzis) | Place 60/120/180 zl, towar wart ok. 300/600/900 zl/d. Kasa zamku zyskuje ok. 10% wartosci: 30/60/90 zl/d | L1 ok. 1 rok; calosc ok. 1.4 roku | Lancuch podazy 435 (deski); DsWorksRealMaterial. Zamek nie ma targu: sprzedaz jak ruda zamkow (TradeBound, Buildings338Ports.cs naglowek 338d). Gra: dochod od ognisk tylko w CalculateTownIncomeFromProjects (martwe, DefaultClanFinanceModel.cs:459-467) |
| Castle Mines (BEZ SENSU) | Jak kopalnie miasta: tylko przy zlozu. Gornicy z wolnych rak **regionu**, nie zamku. Dzis zamek ma nasycenie 0.81, wiec wolnych rak prawie nie ma: 4 zl/d na zamek | Kasa miasta handlowego placi za rude; place do kiesy podzamcza; regale 1/8 | Paczka 3, x0.3: 450/750/1 200 pkt = 14/23/37 tys. zl | 3/6/9 szt./d, czyli 87/174/261 zl/d marzy | L1 ok. 0.5 roku; calosc ok. 0.8 roku (przy limicie rak i zloza) | Buildings338Ports (338c kopalnie zamkow: zmienic tylko zrodlo rak); FreeHands |
| Mines "miejskie" w zamku (MARTWY) | Scalic: ich poziom liczy sie jako Castle Mines (efekt jak wyzej) | - | - | - | - | Urobek dzis tylko w ticku miast (bkbb.cs:242-246) |
| Warhorse Studs (BEZ SENSU) | Stadnina z klaczami: 20/40/60 klaczy (zwykle konie kupione raz z targu), 0.5 zrebiecia na klacz na rok, polowa nadaje sie na konie bojowe. Pasza z pastwisk zamku (BK 4 049 akrow pastwisk) | Stajenni 4/8/12 po 3 d z kasy zamku do kiesy podzamcza. Kon bojowy idzie na targ miasta handlowego (kasa miasta placi kasie zamku) albo do druzyny pana (placi pan) | Klasa wojskowa (paczka 2): 400/700/1 000 pkt = 25/44/63 tys. zl; klacze 20 x ok. 300 d = 6 tys. zl na poziom | 5/10/15 koni bojowych na rok, ok. 34/68/103 zl/d brutto (2 500 d/szt.); netto 22/44/67 zl/d | L1 ok. 3 lata; calosc ok. 5.4 roku | Buildings338Studs (dzis 1 zwykly kon na 1 bojowy, 3 konie w 59 dobach); MountLaw; AiGear (kto kupuje) |
| Port (MARTWY) i Harbor (BEZ SENSU) w zamku | Jeden budynek "Port" tylko dla zamku nad morzem (BK juz filtruje HasPort, bk_all.cs:64962-64966). Rybacy z dzierzawcow i chlopow; ryby do Food zamku (338d), nadwyzka do miasta | Kasa zamku placi rybakom | x0.3: 450/900/1 350 pkt = 14/28/42 tys. zl | 20/40/60 rybakow, 0.13 jedzenia/rybaka, czyli 2.7/5.3/8 jedzenia/d (ok. 26/53/79 zl) przy placach 20/40/60 zl | Ok. 0 pieniedzy: lowienie za place ledwo sie oplaca, jak orka (B). Zysk to zapas jedzenia | Harbor scalic z Portem w zamku |
| Theater (SLABY) | Jak w miescie | | | | | |
| Armory BK (BEZ SENSU) | Jak w miescie (zbrojmistrz zalogi) | | | | | Dzis zjada 11% punktow zamkow |
| Court House BK (BEZ SENSU) | Slot staje sie budynkiem wiary (C) | | | | | Dzis zjada 15% punktow zamkow |
| Farmlands | Zobacz B | | | | | |

### Wies

Dzis wsie buduja praca wsi za 0 zl i bez materialu. Tempo ok. 3 pkt/d. Propozycja wspolna: material prawdziwy z polki (DsWorks), praca wsi kosztuje czesc rak.

| Budynek | Nowy efekt | Platnik / ludzie | Koszt, czas | Efekt na dobe | Zwrot | Kolizje |
|---|---|---|---|---|---|---|
| Mines (BEZ SENSU) | Dostepne tylko we wsi gorniczej albo z zlozem. "Szyb glebinowy": sufit rak kopalni +25/50/75% (glebsze wyrobisko, deski na stemple) | Material: 200/400/600 szt. drewna i narzedzia z targu, kiesa wsi | 2 000/3 000/4 000 pkt pracy wsi (1.8/2.7/3.7 roku) | +25-75% urobku tam, gdzie brakuje miejsca, a nie rak. Dzis 53 wsie KS daja +20 szt./d, wartosc +160 zl i inne plony -160 zl (zero netto) | Zalezy od zloza; poza zlozem nic | Sufit dymarki R1, FreeHands, MineRegale |
| Mill (SLABY) | Mlyn wodny zwalnia rece od zaren recznych: +2/3.5/5% produkcji wsi (tylko wies zbozowa) i chleb jak dzis | Kiesa wsi: kamienie, deski | 1 800/2 600/3 500 pkt | Ok. +9/16/23 zl/d przy utargu 462 zl/d | Praca ok. 2 lat | Buildings338VillageGoods (chleb z ziarna) |
| Bakery (SLABY) | Wspolny piec: drewno na wypiek -20/40/60%; zaoszczedzone drewno na targ | Kiesa wsi | jak dzis | Kilka zl/d | Slaby, ale tani | VillageWoodlot |
| Fur Tannery (BEZ SENSU) | Tylko wies ze skorami (bydlo, owce, mysliwi). Garbarze przerabiaja 12 skor na 5 skor wyprawionych (regula TownCrafts) | Rece wsi; garbniki (kora) z lasu wsi | 1 500/2 400/3 200 pkt | Wartosc dodana ok. 20% przerobionych skor | **Bez danych:** podaz skor na wies trzeba odczytac (KsHides) | 338d: tylko "hides", bez futer |
| Bee Skeps (SLABY) | Do miodu (+1/poziom) dochodzi wosk: popyt "swiatlo" w koszyku NonFood (StartCalibration.Tables.cs:60-67) | Rece wsi | jak dzis | +0.3 szt. wosku/poziom/d | Tani | Towar wosku: sprawdzic, czy istnieje w ROT |
| Marketplace wsi (SLABY) | Targ tygodniowy: oplata 1% utargu wozow do pana wsi, wzrost ognisk jak dzis | Kiesa wsi placi panu (licznik podatku wsi) | jak dzis | Ok. 4.6 zl/d na poziom | Slaby | VillageTaxToLand354, podatek wsi |
| Tax Office wsi (MARTWY) | Stodola dziesiecinna: zboze z daniny i zapas gminy psuje sie o 10/20/30% wolniej (zima, spichlerz okregu Famine) | Material: deski i wapien, kiesa wsi | 1 500/2 500/3 500 pkt | Dzis zima dodaje ubytku -481 jedzenia/d w 227 warowniach (log "Zima wedlug krain") | **Bez danych:** ubytek zapasu wsi trzeba zmierzyc | Famine, WinterBite (jedna regula psucia) |
| Courier Post (MARTWY) | Goniec targowy: woz wsi zna ceny 2/3/4 najblizszych targow i jedzie tam, gdzie lepiej. Dla gracza zostaja tez meldunki BK | Rece wsi (1-3 gonców) | 1 000/1 800/2 500 pkt | +2/4/6% utargu wozu, ok. 9/18/28 zl/d na wies | Praca 1-2 lat | MarketRoad / MarketCarts: wybor targu nie moze sie dublowac |
| Manor (SLABY) | Dwor rzadcy: orszak 15/30/45 jak dzis, ale **zywiony z ziarna wsi** (zamknieta ekonomia); wplyw jak dzis | Wies karmi orszak | Koszt x0.4: 1 600/2 400/3 200 pkt | Orszak -0.02 jedzenia na czlowieka na dobe | Niepieniezny | HouseLevies, Levy |
| Palisade (SLABY) | Wolniejszy rabunek jak dzis oraz ochrona spichlerza wsi przed furazem 471 (-20/40/60% zabranego) | Drewno 300/500/700 z targu | x0.5: 1 500/2 500/3 500 pkt | Zalezy od wojny | Wartosc w wojnie | 471 (furaz), SideVillageVisit.Raid |
| Training Grounds (SLABY) | Milicja +0.2/0.5/1 jak dzis, ale cwiczenia kosztuja czas: -0.5/1/1.5% produkcji wsi | Rece wsi | x0.5 | -2/5/7 zl/d | Bezpieczenstwo | Levy (pobor z pol) |
| Projekty akrow (SZKODLIWE) | "Melioracja": przez 180 dni 10% rak wsi karczuje i osusza, po tym czasie +5% produkcji na stale (najwyzej +15%, 3 projekty) | Rece wsi (produkcja -10% w czasie projektu) | 0 zl, 180 dni | Po projekcie ok. +23 zl/d na wies | Ok. 2 lata (koszt 10% x 0.5 roku = 5% roku) | Dzis projekt akrow wylacza +15% Production (Buildings338Village): nie mozna laczyc. Akrow BK nikt nie czyta, wiec efekt musi isc przez czynnik KS (tablica A) |

## B. Farmlands na liczbach

### B1. Jak liczy sie jedzenie zamku (kod)

**Bilans jedzenia zamku** (wanilia, `DefaultSettlementFoodModel.cs`):
- Okolica zamku: +10 (miasto +15), linia 65.
- Kazda wies w stanie Normal: (poziom ognisk + 1) x 6, linia 72.
- Budynki z efektem FoodProduction (linia 76). Farmlands daje **+6/12/18** (`DefaultBuildingTypes.cs:283-285`).
- Odejmuje sie spozycie: dobrobyt / 40 (linia 47) i zaloga / 20.

**Dodatek BK** (`bk_all.cs`):
- Do bilansu wchodzi tylko **15% produkcji ludnosci** (BKFoodModel, bk_all.cs:39327 "Local farmland").
- Produkcja ludnosci (PFP) to akry x wydajnosc (bk_all.cs:39396). Pole uprawne daje 0.025 na akr (74578). Akrow jest ludnosc x 2.5-3.0, z tego 70% pola na rowninie (74557).
- Nasycenie rak daje czynnik od -100% do 0 (39413). Rece = 0.5 x chlopi + niewolni (74354).
- **Farmlands dodaje czynnik +0.5 x poziom** (39425). W miescie ten sam czynnik daje **Warehouse**, x0.3.
- Spozycie samej ludnosci BK (chlop 0.02/d, bk_all.cs:39293) do bilansu Food **nie wchodzi** (opis KsHunger.cs:28-31).

### B2. Typowy zamek (srednie ze 130 zamkow, doba 109793)

Dane: ludnosc BK 6 692 (5 278 chlopow, 360 niewolnych, 16 dzierzawcow, 319 szlachty, 720 rzemieslnikow), dobrobyt 3 165, 2 wsie. Zapas srednio 13 642. Bilans srednio **+45** (mediana 39, q10 3, q90 94). 12 zamkow ma bilans ponizej 0.

Wyliczenie PFP (szacunek: akry z poczatku kampanii, rownina, bez innowacji):
- Pole 12 882 akrow daje 322, pastwiska 4 049 akrow daja 25, las daje 2. Baza = **349**.
- Rece: 0.5 x 5 294 + 360 = **3 007**. Potrzeba 12 882/4 + 4 049/8 = 3 726. Nasycenie **0.81**, czynnik -19%.

| Farmlands | PFP | 15% do zamku | +gra | **Razem z pol** | W tym z budynku | Wartosc budynku (9.9 zl/szt.) |
|---|---|---|---|---|---|---|
| brak | 282 | 42.3 | 0 | 42.3 | 0 | 0 |
| L1 | 456 | 68.4 | +6 | 74.4 | **+32** | 320 zl/d |
| L2 | 631 | 94.6 | +12 | 106.6 | **+64** | 640 zl/d |
| L3 | 805 | 120.8 | +18 | 138.8 | **+97** | 960 zl/d |

Koszt Farmlands to 13/20/26 tys. zl, razem 59 tys. Budynek zwraca sie w **ok. 41 dni (L1) i ok. 62 dni (calosc)**. Inne budynki zwracaja sie w 1-5 lat.

**Kontrola niezalezna:** rece zamku x KS zboze 3 kg = 3 007 x 3 = 9 021 kg/d, czyli ok. **282** jednostek. To dokladnie PFP bez budynku. Baza BK = uczciwa praca chlopow zamku.

### B3. Skad jedzenie "z niczego"

| Skladnik | Czy jest praca? | Ile w typowym zamku |
|---|---|---|
| Premia BK +50%/poziom | **Nie.** Te same 3 007 rak i te same akry daja +50/100/150%. To odpowiednik ok. **1 500 / 3 000 / 4 500 nieistniejacych parobkow** (przy L3 1.5 raza wiecej niz cala sila robocza zamku, ktorej i tak brakuje: nasycenie 0.81) | +26 / +52 / +79 |
| Gra +6/12/18 | Nie, stala | +6 / +12 / +18 |
| "Okolica zamku" +10 | Nie, stala (wanilia) | +10 |
| Spozycie ludnosci BK | Ludnosc BK zjada **199/d** (ludnosc-bk.csv `zuzycie_bk`), a bilans Food liczy tylko dobrobyt / 40 = 79 i zaloge | Tego "jedzenia" nikt nie odejmuje |
| Nadmiar ponad sufit | **110 ze 130 zamkow** ma zapas wyzszy niz 2 x ludnosc + 250 (srednio 1.00 sufitu BK). Jedzenie ponad sufit **znika** | Wiekszosc premii przepada |

Wniosek: dzis w zamku premia Farmlands glownie **utrzymuje bilans dodatni** i zapas pod sufitem (ok. 300 dob jedzenia). Reszta przepada nad sufitem.

### B4. Co bez premii (skala swiata)

- **Pomiar 338b** (CHANGELOG 338c, test at338, 30 dob): zdjecie premii BK (Farmlands zamkow i Warehouse miast) to **-10 100 jedzenia/d** na swiecie, czyli ok. 100 tys. zl/d wartosci. Osad z bilansem ponizej 0 bylo 82, po zdjeciu **172 z 219**.
- **Szacunek dla zamkow dzis:** 129 zamkow z Farmlands x ok. 52 (L2) = ok. **6 700 jedzenia/d**.

Ile zamkow zeszloby ponizej zera, gdyby kazdy mial dzis L2 (rozklad bilansu z CSV):

| Zdjete | Zamkow ponizej 0 (dzis 12) | Ile dni do pustego zapasu (13 600) |
|---|---|---|
| Premia BK (-52) | ok. 83 ze 130 | przy -7/d: ok. 1 900 dni (5 lat) |
| Premia BK i gra (-64) | ok. 91 | przy -19/d: ok. 700 dni (2 lata) |
| Cale "Local farmland" BK (-95) | ok. 115 | ok. 270 dni |

Poziomow Farmlands w zamkach log nie podaje. Te liczby zakladaja L2.

### B5. Propozycja zgodna z KS

Zatrudnienie najemnikow do pola sie nie spina. Parobek daje 0.094 jedzenia, czyli 0.93 zl/d, a kosztuje ok. 1 d/d. Najemna praca na polu pana wychodzi na zero. Tak bylo historycznie: dwor zyl z panszczyzny, nie z najemnikow. Dlatego proponuje:

**Farmlands = "pola dworskie" (panszczyzna).**
- Budynek nie tworzy jedzenia. Przesuwa czesc pracy chlopow z ich zagonow na pole pana.
- Udzial zamku w plonie rosnie z 15% do **20/25/30%** (L1/L2/L3).
- Znika +6/12/18 gry i premia BK +50%/poziom (klucz Buildings338FoodDouble juz istnieje).
- Koszt panszczyzny ponosza chlopi: ich czesc spada z 85% do 80/75/70%. W grze daje to lojalnosc -0.1/-0.2/-0.3 na dobe (ciezsza robocizna). Tak samo dziala BK "SerfsAgricultureDuties" (+10%).

| Poziom | Zamek dostaje | Dzis | Roznica | Bilans typowego zamku |
|---|---|---|---|---|
| L0 | 42.3 | 42.3 | 0 | +45 |
| L1 | 56.4 | 74.4 | -18 | +27 |
| L2 | 70.5 | 106.6 | -36 | **+9** |
| L3 | 84.6 | 138.8 | -54 | -9 (zapas na ok. 1 500 dni) |

- Swiat: ok. -4 650 jedzenia/d w zamkach (L2). To glownie nadmiar, ktory dzis i tak przepada nad sufitem.
- Ponizej 0 zeszloby ok. 59 zamkow (ten, ktorego bilans jest ponizej 36), ale z zapasem na lata.

**Warunki przed wdrozeniem:**
1. Linia logu z poziomem Farmlands kazdego zamku i z bilansem przed i po (dzis jej brak).
2. Spozycie zamku (dobrobyt / 40) to nie prawdziwi ludzie. Uczciwie: zaloga i dwor jedza z Food, a chlopi zyja ze swoich 85%. Tej zmiany nie robic razem, tylko osobno.
3. Miasta: Warehouse daje ten sam czynnik BK (x0.3 na poziom). Decyzja dla miast osobno (KS ma tam rynek).

## C. Budynek wiary

### C1. Jak dziala dzis religia BK (kod)

| Element | Dzialanie | Kod |
|---|---|---|
| Udzial wiar w osadzie | Codziennie przesuwa sie o 1% luki do celu (polowa drogi w ok. 69 dni) | bk_all.cs:61402 (BalanceReligions) |
| Cel (waga wiary) | Kazdy notable danej wiary: 25 x liczba notabli x jego udzial wladzy; **pan: +5**. Calosc x (1 + zapal + sila wiary - 1) | bk_all.cs:48316-48373 (pan 48344, notable 48373) |
| Wniosek | Pan prawie nie rusza wiary. Przy 6 notablach to 150 wobec 5, czyli 3% | |
| **Napiecie religijne** | `CalculateTensionTarget` startuje od 0 i dodaje tylko AddFactor. ExplainedNumber = baza + baza x czynniki, wiec **zawsze 0**. Kara lojalnosci -6 x napiecie (bk_all.cs:40380) nigdy nie dziala. Koszt mianowania kaplana od napiecia tez nie dziala | bk_all.cs:48185-48208; ExplainedNumber.cs:149, 238-247 |
| Zapal (fervor) | 0.4 + siedziba wiary + swiete miejsca + rody | bk_all.cs:48215-48290 |
| Kler | Duchowny BK jest notablem; jest w 798 osadach z zywym kaplanem | log "Dziesiecina (E5/C3)" |
| Wiary ROT | **Siedmiu:** nightswatch, vale, river, dragonstone, stormlands, reach, crownlands, aserai, vlandia. **Starzy Bogowie:** freefolk, battania (=Polnoc), skagosi, nightswatch, ibbenese. **Utopiony Bog:** sturgia (Zelazne Wyspy). **R'hllor:** dragonstone, volantine. Do tego 18 wiar Essos | bkrot_all.cs:5144-5168 |
| Budynek wiary w grze | **Nie ma** w zadnym module | audyt 12.11 |

### C2. Nasza dziesiecina dzis (TownFolk.Tithe.cs:11-25, log 109793)

- **Miasta:** 7 666 zl/d (W 2 920, R 1 972, P 2 774; stawki 1/2/3% zarobku). Jalmuzna 3 835. "Budowa septow" 3 831/d: zlecenia za prawdziwy material.
- **Wsie:** 13 187 zl/d. Jalmuzna 6 595. 6 592 zostaje w kiesach.
- "Septy" kupuja material i placa ludzi, ale **nie ma budynku ani efektu**. To sam obieg zlota.
- Material robot gminy i septow brakuje: 265 900 zl zlecen bez materialu zostalo w kiesach (log "Akumulator kies").
- Fundusz septow na osade: **miasto ok. 40 zl/d**, wies ok. 11.5 zl/d, zamek (2 wsie) ok. 23 zl/d.

### C3. Projekt

**Typ budynku:** slot BK Court House (`bk_building_courthouse`). Jest juz w zapisach (zero ryzyka dla save), nie ma efektu i scala sie z sadem gry (A). Nazwa wedlug wiary w podpowiedzi, po angielsku:

| Wiara | Nazwa | Material | Koszt pkt (cywilny 31 zl) | Zloto | Czas z samej dziesieciny |
|---|---|---|---|---|---|
| Siedmiu | Sept | wapien, deski | 1 000/1 500/2 000 (miasto), 600/900/1 200 (zamek) | 31/46/62 tys. (miasto), 19/28/37 tys. (zamek) | miasto L1 ok. 2.1 roku; zamek L1 ok. 2.2 roku |
| Starzy Bogowie | Godswood | tylko drewno (polana, wyrzezbiona twarz) | x0.3 | 9/14/19 tys. | miasto L1 ok. 0.6 roku |
| Utopiony Bog | Drowned God's Hall | drewno | x0.5 | 16/23/31 tys. | ok. 1.1 roku |
| R'hllor | Temple of R'hllor | wapien | jak Sept | | |
| Wiary Essos | Temple | wapien | jak Sept | | |

**Kto placi:**
- Czesc "budowa septow" dziesieciny osady staje sie skarbonka budynku (dzis bez efektu).
- Pan moze doplacic ze swojego budzetu budowy tylko wtedy, gdy jego wiara jest w osadzie mniejszoscia (nawracanie).
- Material prawdziwy (DsWorks). Wola sie go **po** budowach pana i ludu, tak jak dzis.
- Platnosc idzie droga oplaconej pracy BuildFunding, nigdy jako darmowe punkty.

**Efekty:**

| Nr | Efekt | Liczby | Czytnik |
|---|---|---|---|
| E1 | Waga wiary budynku w osadzie | +10/20/30 do CalculateReligionWeight (postfiks). Przyklad: miasto z 6 notablami obcej wiary. Wiara pana idzie z 3% (5/155) do 19% celu przy L3 (35/185); po 69 dniach ok. +8 pp | bk_all.cs:48316 |
| E2 | Lojalnosc, gdy wiara budynku = wiara panujaca osady | +0.25/0.5/0.75 na dobe (Theater daje +0.2/0.5/1 za kulture). W osadzie wiary obcej 0 | model lojalnosci (postfiks jak KsHunger) |
| E3 | Napiecie | **Nie ruszac teraz.** Naprawa wzoru BK wlaczylaby karte -6 x napiecie w kazdej mieszanej osadzie: martwy kod staje sie zywy (CLAUDE.md, zasada 4). Najpierw linia logu "napiecie wedlug poprawnego wzoru" (ile osad i jaka kara), potem decyzja Jeffa. Do sprawdzenia stanowiska wiar ROT (GetStance) | bk_all.cs:48185 |
| - | Opieka nad chorymi, morale zalogi | **Odrzucone: brak czytnika.** Choroby to tylko bohaterowie AI Influence (PlagueWatch.cs). Zalogi nie maja morale | |

**Jak AI wybiera:**
- Buduje tylko z zywym kaplanem w osadzie.
- Buduje, gdy udzial wiary pana jest ponizej 50%, albo wiara panujaca jest wiara pana i lojalnosc ponizej 60 (E2).
- Nie buduje, gdy wiara budynku ma juz ponad 90% i lojalnosc ponad 70.

**Kolizje:**
- BuildFunding (oplacona moc) i brak materialu (38-42 budowy stoja).
- BK Court House daje dzis stabilnosc +0.02/poziom (bk_all.cs:48582): zdjac albo zaakceptowac.
- Theater (kultura) to osobny budynek.
- Rytualy BKROT (bkrot_all.cs:2033+) sa niezalezne.
- Zloto sie nie tworzy: dziesiecina idzie na material (kasa miasta) i place (kiesy ludu).

## Pytania do Jeffa (wynikaja z projektu)

1. **B5:** czy Farmlands ma byc "panszczyzna" (udzial zamku 20/25/30% bez premii z niczego)? Najpierw dopisalbym linie logu z poziomami Farmlands.
2. **C:** czy budynek wiary wchodzi w slot BK Court House? Czy E2 (lojalnosc) tak, a E3 (napiecie) dopiero po pomiarze?
3. **Kolejnosc wdrozen z A:** proponuje najpierw kopalnie przy zlozu (miasto i zamek) i Craftmans Quarters (deski), bo zwracaja sie w mniej niz 1.5 roku i lagodza brak materialu.

**Pliki:**
- C:/Users/GAME/AppData/Local/Temp/claude/bfd708ab6e1d6456/264cfac3-8338-4c40-91b8-6c1304da3584/scratchpad/n473/Armoury/src/Buildings338Food.cs
- C:/Users/GAME/AppData/Local/Temp/claude/bfd708ab6e1d6456/264cfac3-8338-4c40-91b8-6c1304da3584/scratchpad/n473/Armoury/src/TownFolk.Tithe.cs
- C:/Users/GAME/AppData/Local/Temp/claude/bfd708ab6e1d6456/264cfac3-8338-4c40-91b8-6c1304da3584/scratchpad/n473/Armoury/src/Buildings338Ports.cs
- C:/Users/GAME/AppData/Local/Temp/claude/bfd708ab6e1d6456/264cfac3-8338-4c40-91b8-6c1304da3584/scratchpad/n473/Armoury/src/StartCalibration.Tables.cs
- C:/Users/GAME/AppData/Local/Temp/claude/bfd708ab6e1d6456/264cfac3-8338-4c40-91b8-6c1304da3584/scratchpad/n472dec/bk_all.cs
- C:/Users/GAME/AppData/Local/Temp/claude/bfd708ab6e1d6456/264cfac3-8338-4c40-91b8-6c1304da3584/scratchpad/n472dec/bkrot_all.cs
- C:/Users/GAME/AppData/Local/Temp/claude/bfd708ab6e1d6456/264cfac3-8338-4c40-91b8-6c1304da3584/scratchpad/decout/v/all_cs/TaleWorlds.CampaignSystem.GameComponents/DefaultSettlementFoodModel.cs
- C:/Users/GAME/AppData/Local/Temp/claude/bfd708ab6e1d6456/264cfac3-8338-4c40-91b8-6c1304da3584/scratchpad/decout/v/all_cs/TaleWorlds.CampaignSystem/ExplainedNumber.cs
- C:/Program Files (x86)/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/Armoury/Armoury-2026-11-12_22-47-01.log
- C:/Program Files (x86)/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/Armoury/Logs/2026-11-12_22-47-01/ (spichlerze-regiony.csv, ludnosc-bk.csv, budowy.log)