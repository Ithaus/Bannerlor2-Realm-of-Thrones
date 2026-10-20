# p5-stada (noc 20.10) - R3-06c stada jako lup, R3-06d ugor i zmeczona ziemia

Galaz `w-toku/p5-stada`, baza 5189c0fe (kandydat WGRANIA 92). Jeff 20.10 o liscie 35 pomyslow: "28 - nie, 29 - nie. Pozostale tak".
Etap 1: 28eea691 (obie reguly). Etap 2: poprawki po recenzji (sekcja "Recenzja" na dole).

## Dla Jeffa (prosto)

Wies hodowlana ma teraz prawdziwe stado. Gdy ktos ja lupi, najpierw odpedza owce, swinie i krowy: czesc trafia do jego taboru (najwyzej jedno zwierze na dwoch ludzi, tak zeby armia nie zwalniala), reszta ginie. Potem wies przez lata daje mniej zwierzat, welny, sera i masla: owce i swinie odrastaja o ok. jedna czwarta rocznie, bydlo o ok. jedna osma. Druga zmiana to hamulec ludnosci: gdy okreg urosnie ponad ziemie, ktora uprawial na starcie gry, nowi chlopi obsiewaja ugor i kazdy dorzuca mniej plonu niz poprzedni. Na starcie nic sie nie zmienia, wiec w Twojej kampanii zadziala to dopiero po latach wzrostu. Bezpiecznik KS nie bierze glodu po rabunku stada ani glodu z przeludnienia za blad KS, wiec nie wylacza przez to KS w calej krainie.

## R3-06c - Stada jako lup (klucz MCM `Village Herds As Loot`, domyslnie TAK)

**Problem / zrodlo pomyslu:** POMYSLY-35 poz. 14; audyt `docs/audyt-2026-10-09/02-wies-produkcja-krainy.md` rozdz. 2.2, 2.3 i E pkt 2 (stada lupem nr 1, welna Northumberland 7-8 lat po rozejmie, Bolton 43 woly). Dotad rabunek (DistrictRaid) zabieral tylko zwierzeta z magazynu wsi, a produkcja hodowli wracala razem z ludzmi - stado jako majatek okregu nie istnialo.

**Zmiana (nowe pliki `Armoury/src/VillageHerds.cs`, `Armoury/src/StartCalibration.Stada.cs`):**
- Stan stada wsi H w [0.10, 1] w dwoch czesciach: drobne (owce i swinie - towary sheep, hog, wool) i bydlo (cow, cheese, butter). Wsie z pelnym stadem nie sa przechowywane.
- Produkcja: towary stada x H swojej czesci. Wies pod KS - w `StartCalibration.ComputeDistrict` (gra, z czynnikami) ZARAZ PO `Produce`, przed dodatkiem prac okregu (DistrictWorks) i przed zima / rekami E4/5, E4/7 (czynniki sa mnozne - kolejnosc nie zmienia wyniku); Plan kalibracji bez zmian. Lancuch gry (wies poza KS, rampa) - `MaterialLaw.ProdPostfix` po rekach wojny E4/7 (wzor czynnik x suma) i ten sam czynnik w `ChainFactor`.
- Rabunek osada po osadzie (`DistrictRaid.Loot`, AI i gracz, na poczatku lupu kroku - przed zlotem i towarem z magazynu): wies zawsze dostaje znacznik "rabunek DistrictRaid" (takze przy udziale 0); przy udziale s > 0 stado traci l = min(90%, 1.5 x s) obecnej wielkosci, nie mniej niz do 10%. Ubytek w sztukach gry = ubytek H x stado wsi (stawka zwierzat przy pelnym stadzie x 364 / przyrost roczny). Napastnik pedzi do taboru wodza najwyzej 0.5 sztuki na czlowieka partii wodza i partii doczepionych, ale tak, zeby stado taboru (juczne, zywiec, wolne wierzchowce ponad pieszych - jak model predkosci gry) nie przekroczylo liczby ludzi; owce, swinie, potem bydlo. W ksiedze towarow jako "produkcja wsi" (prawdziwe zwierzeta okregu); reszta ubytku to zabite i rozproszone (tylko licznik, bez sztuk). Liczba pedzonych zwierzat dochodzi do "TAKEN N" w okienku kroku gracza.
- Rabunek gry bez DistrictRaid (klucz rabunku osada po osadzie wylaczony, odpuszczona sesja): zdarzenie `VillageLooted` bez znacznika z ostatnich 3 dob - ubytek jak za pelny rabunek gry (s = 0.40, czyli l = 60%), bez zwierzat (gra daje swoj lup).
- Odrost: co dobe wies w stanie Normal: H x (1 + r)^(1/dni roku), r: drobne 25%, bydlo 12% (kalendarz Armoury, rok 364 dni); do 1.
- Zapis: klucz `arm_herds` przez `SaveText.Sync` (`v1|id:drobne:bydlo;...`); stary zapis bez klucza = pelne stada. Napis odlozony do `OnSessionLaunched` (Resolve).

**Linia logu (log_grep):** `Stada (R3-06c): dzien N | wsie z niepelnym stadem X ... | dzis rabunki ze stratami stada A (osada po osadzie) + B (rabunek gry), rabunki osada po osadzie bez lupu (udzial 0 - bez straty stada) Z, zwierzat do taborow C ..., zabite i rozproszone D szt. ... | bramka krainy KS: wsi z kcal wlasnego stolu zabranymi przez stado i ziemie K, wzor gry obnizony o ten sam ubytek we wsiach G (racje ...), wsi nie liczonych jako racje KS ponizej wzoru gry (stado i ziemia, nie KS) F, potkniecia | potkniecia`. Linia co dobe z rabunkiem, poza tym w dobach pelnych logow (LogGate).

## R3-06d - Ugor i zmeczona ziemia (klucz MCM `Tired Land Yield`, domyslnie TAK)

**Problem / zrodlo pomyslu:** POMYSLY-35 poz. 15; audyt 02 rozdz. 2.1 (trojpolowka, Postan) i E pkt 5; 10-pomysly F6. KS liczy plon okregu liniowo od rak (rece x P0 x przydatnosc ziemi); ziemia zajeta bloku (`KsPart.LandUse`) pilnowala tylko reguly zmiany zajec. Gdy ludzi przybywalo, kazdy nowy chlop dawal tyle samo, choc ziemi juz brakowalo.

**Co to jest (uczciwie, po recenzji):** HAMULEC PRZYROSTU ponad ziemie startu - bez osobnego modelu ugoru. Propozycji z opisu ("ugor jako czesc ziemi, ktora w danym roku nie rodzi", odlog 1/3 na polnocy, 1/2 na poludniu) nie robie osobno: tabela KS ma ha na pracujacego (`StartCalibration.Tables.cs`, zboze 4 ha) bez zrodla, czy zawieraja odlog - wczesniejsze zdanie, ze "4 ha juz zawiera odlog trojpolowki", nie mialo podstawy i je wycofuje. Liczenie odlogu osobno zmieniloby kalibracje startu (95-105% rak i sprzetu) we wszystkich okregach - to poza tym zadaniem.

**Zmiana (nowy plik `Armoury/src/TiredLand.cs`, wpiecie w `StartCalibration.Stada.cs`):**
- W `ComputeDistrict` (gra, z czynnikami): rho = suma ziemi zajetej blokow / suma ziemi blokow okregu. Baza okregu b = max(1, rho przy pierwszym rachunku w grze) - w zapisie (start neutralny, takze dla kampanii Jeffa wczytanej z nowa DLL; okreg przeludniony juz na starcie zachowuje plon).
- Gdy rho > b: towary ziemi (zboze, owoce i warzywa, winogrona, oliwki, daktyle, przyprawy, len, bawelna, welna, nabial, zywiec) x (b / rho)^0.6, najmniej x0.5 - plon okregu rosnie dalej, ale jak rece^0.4. Bez zmian: jaja i miod (dom), ryby, sol, drewno, kopaliny, futra, wieloryby, dziczyzna.
- Tylko w stanie zastosowanym KS (wsie i pola mieszczan pod KS). Podpowiedz MCM mowi to wprost.
- Zapis: klucz `arm_tiredland` (`v1|id:baza;...`).

**Linia logu (log_grep):** `Zmeczona ziemia (R3-06d): dzien N | okregi KS z ziemia X (baza z zapisu, nowa baza w sesji) | ziemia zajeta / ziemia okregu: mediana, najwiecej | ponad baze A, plon na glowe nizszy w B okregach (srednio, najnizej) | potkniecia`.

## Ryzyko / co sprawdzic (kontrola calosci)

- **Regresje:** wpiecia addytywne. `ComputeDistrict`: `HerdKs` po `Produce` i `LandKs` po zimie i rekach (oba w galezi `factors`, czyli tylko gra; Plan, T1 watku roboczego i linie Planu licza `factors = false`), `HerdLandOwn` po `KsDeliver.Own` (tylko przy `factors`). `NoteGranary` - jedna linia (wzor gry). `ProdPostfix` + `ChainFactor` - po jednej linii (zgodnosc utrzymana). `DistrictRaid.Loot` - jedna linia przed wyjsciem przy udziale 0 (wlasny try). `ArmouryBehavior` - SyncData, RegisterEvents, doba (z `DayProfile.Lap`). Klucz wylaczony = czynnik 1 i brak ubytku; bramka bez zmian.
- **Kolizja z bramka krainy KS (ks-arm, f26) - naprawione:** stado i ziemia obnizaja racje KS (ser, maslo, zywiec, zboze), a wzor gry, z ktorym bramka porownuje racje, nie zna ani stad, ani ziemi. Teraz `HerdLandOwn` liczy kcal wlasnego stolu bez stada i ziemi (kopia plonu, ten sam `KsDeliver.Own`) i zapisuje roznice na wies; `NoteGranary` obniza wzor gry o te same racje (ludzie w domu x min(1.5, kcal / potrzeba) - wzor racji LiveDaily i FloorDaily). Glod po rabunku stada i glod z przeludnienia nie sa wiec "glodem z winy KS"; to samo dla luki strat A/C i sumy wzoru krainy. Przyblizenie: koszyk ZC4 (jedzenie robione w domu) liczony z wlasnym stolem po stadzie i ziemi.
- **Kolizja z pracami okregu (DistrictWorks, KHerd) - naprawione:** stado wsi mnozy plon PRZED dodatkiem prac; kupione stado prac nie obrywa drugi raz (DistrictWorks sam pedzi je do napastnika i tnie jednostki prac). Pamiec stawek zwierzat (wielkosc stada) liczy sie teraz bez zimy i rak, a w lancuchu gry - jak dotad (wynik / czynnik).
- **Kolizje pozostale:** stado mnozy sie z zima, rekami E4/5 i E4/7 (jeden wzor - czynnik x suma; w KS - mnozenie kg). Regulator towaru W3 i hodowla K4 dalej po nim w lancuchu gry. Zwierzeta z magazynu wsi DistrictRaid daje jak dotad (udzial s). DragonFeed - bez zmian. Regula zmiany zajec A7 liczy dochody bez czynnika stada i ziemi - swiadomie poza zakresem.
- **Scalanie z `w-toku/p5-urodzaj` (d45c73fe, R3-05e/R3-05f) - do zrobienia przy scal.sh:** (1) `HarvestYear.KsApply` w `ComputeDistrict` postawic PRZED `LandKs(ld, prod, f)` - kopia plonu bez stada i ziemi musi miec rok urodzaju, inaczej bramka bralaby nieurodzaj za skutek stada i ziemi. (2) `HarvestYear.ProdStep` / `HarvestYear.Factor` i `VillageHerds.ProdStep` / `VillageHerds.Factor` w `MaterialLaw` - obie linie, kolejnosc dowolna (czynniki mnozne). (3) HerdCull (R3-05f) trzyma osobny "dlug stada" (uboj jesienny). Uboj liczy sie od dziennego przyrostu, ktory pod KS jest juz x H, wiec maleje razem ze stadem; sam dlug przy rabunku nie maleje - decyzja przy scalaniu: zostawic (dlug splaca sie polowa przyrostu, ktory i tak jest mniejszy) albo zmniejszac dlug proporcjonalnie do ubytku H. Dwa stany jednego stada - do ujednolicenia w kolejnej rundzie.
- **Zamknieta ekonomia:** zwierzeta do taboru = prawdziwe zwierzeta stada (w ksiedze jako produkcja wsi, sprzedawane potem zwyklym handlem); zabite i rozproszone to zniszczenie majatku (bez sztuk i zlota). Odrost to przychowek. Zmeczona ziemia tylko obniza produkcje. Poprawka bramki niczego nie dopisuje - zmienia tylko miare.
- **Watki:** slownik stada podmieniany w calosci na watku glownym, czytany bez blokad (ProdPostfix moze isc z watkow AI); pamiec stawek, znaczniki rabunku i kopie plonu bramki - tylko watek glowny (`ComputeDistrict` z czynnikami wolany tylko z LiveDaily, `NoteGranary` z Famine.Daily). Goraca sciezka bez alokacji (switch po id towaru, pusty slownik = wyjscie od razu); kopie plonu - bufory wielokrotnego uzytku.
- **Skala:** stado wsi owczej (np. 10 owiec na dobe) to ok. 14,5 tys. sztuk gry - liczba "zabite i rozproszone" bedzie duza; do taborow trafia najwyzej 0.5 sztuki na czlowieka i nie wiecej, niz pozwala stado taboru. AI sprzedaje zywiec tylko w miescie, z ktorym nie jest w wojnie (PartiesSellLootCampaignBehavior) - sprawdzic, czy zwierzeta nie zalegaja w taborach.
- **Ciagle wojny:** Dorzecze i Krainy Burzy moga miec stada przy 10% latami - sprawdzic w linii "najnizej" po dlugim tescie, czy welna i skory nie znikaja z rynku.
- **Zmeczona ziemia w krotkim tescie:** przyrost ludnosci najwyzej +0.8% rocznie, a baza = max(1, rho startu) - w tescie 160 dob czynnik prawie nigdzie nie zadziala; "co zobaczysz" z opisu pomyslu (mniej nadwyzki w gestych okregach) pojawi sie dopiero po latach kampanii.
- **Pod KS nie widac stanu stada w grze:** dopisek "Armoury: herd of the district (raids)" pokazuje tylko lancuch gry; wsie KS (dzis wszystkie) go nie maja. Dopisek w dymku albo menu okregu (E7/W2, W3) - do zrobienia osobno (wspolne pliki dymka i menu).
- **Zapis w trakcie okienka kroku gracza:** znacznik rabunku nie jest w zapisie. Wczytanie zapisu z otwartym okienkiem i "End Raiding" bez nowego kroku moze raz potraktowac koniec jak rabunek gry (stado -60%). Rzadkie; do obserwacji.
- Wies poza KS (lancuch gry) nie zna ziemi okregu - tam zmeczonej ziemi nie ma (dzis KS obejmuje 569/569 wsi i 92 pola mieszczan wedlug logu WGRANIA 91).

## Status

DO SPRAWDZENIA - zbudowane (Armoury, kod 0, 0 Error(s)), McmSettings z gen_mcm, niewgrane, bez autotestu (zakaz w zadaniu).

## Decyzje (projektuje Claude, wylacznik = klucz MCM)

- Dwie czesci stada (drobne / bydlo) zamiast stada na kazdy gatunek - ser i maslo ida z bydlem (w KS bydlo daje ich 6 razy wiecej niz owce).
- Swinie razem z owcami (przyrost 25%) - pomysl mowi o owcach i bydle; swinie mnoza sie co najmniej tak szybko jak owce.
- Ubytek stada 1.5 x udzial trafionych, najwyzej 90% na rabunek, dno 10% stada; 0.5 sztuki pedzonej na czlowieka, stado taboru nie ponad liczbe ludzi.
- Stado w sztukach = stawka przy pelnym stadzie x 364 / przyrost roczny (przychowek rowny ubojowi w stanie ustalonym).
- Zmeczona ziemia: elastycznosc plonu do rak ponad ziemie 0.4 (czynnik na glowe (b/rho)^0.6), najmniej x0.5; baza okregu = max(1, rho pierwszego rachunku) - start neutralny; bez osobnego modelu odlogu.
- Bramka krainy KS: kcal zabrane przez stado i ziemie odejmowane od wzoru gry (nie wyjatek "wies z niepelnym stadem = glod z wojny") - dokladna miara zamiast drugiego wylacznika.

## Test w grze

1. Autotest 120-160 dob na nowej kampanii: `grep "Stada (R3-06c)"` - po rabunkach "wsie z niepelnym stadem" > 0, "zwierzat do taborow" > 0, srednie odrastaja z doby na dobe; "rabunki osada po osadzie bez lupu" > 0 nie zmienia stada; "(rabunek gry)" przy wlaczonym E7/R1 blisko 0; potkniecia 0.
2. W tej samej linii "bramka krainy KS": po rabunkach "wsi z kcal ... zabranymi" > 0, "wzor gry obnizony" > 0; potkniecia 0. Linia bramki krainy KS: "glod z winy KS: wsi" i powroty krain nie rosna po rabunkach wzgledem WGRANIA 91.
3. `grep "Zmeczona ziemia (R3-06d)"` - okregi KS z ziemia ok. 661, mediana rho ponizej 1, w pierwszych dobach "plon na glowe nizszy w 0 okregach"; potkniecia 0.
4. Linia "Rabunek osada po osadzie (E7/R1)" - bilans (v2) dalej 0 (zwierzeta stada ida przed pomiarem taboru, w ramce produkcji wsi).
5. Linia KS "na targ jutro" - sheep, wool, cow, cheese spadaja w regionach wojny wzgledem pokoju.
6. Po zapisie i wczytaniu: "Stada (R3-06c): z zapisu N wsi z niepelnym stadem".
7. Profil doby: okrazenia "VillageHerds.Daily" i "TiredLand.Daily" osobno.

## Recenzja (PELNA, 20.10) - co zrobione

- **[wazne] Droga zapasowa OnLooted brala 60% stada po rabunku DistrictRaid z udzialem 0 - PRAWDA, poprawione.** `VillageHerds.OnRaid` wolany przed `if (!(s > 0.0)) return;` w `DistrictRaid.Loot`; znacznik wsi ustawiany zawsze (takze przy s = 0 i stadzie na dnie). Nowy licznik "rabunki osada po osadzie bez lupu".
- **[wazne] Kolizja z bramka krainy KS - PRAWDA, poprawione** (patrz Ryzyko: `StartCalibration.Stada.cs`, `HerdLandFormula` w `NoteGranary`; dopisek bramki w linii stad).
- **[drobne] Stado prac okregu obrywa dwa razy - PRAWDA, poprawione** (stado wsi przed `WorksKs`).
- **[drobne] Pedzone zwierzeta od calej strony, kara predkosci - PRAWDA, poprawione** (`DriveCap`: ludzie partii wodza i doczepionych, stado taboru nie ponad ludzi - wzor jak `DefaultPartySpeedCalculatingModel`, sprawdzony w dekompilacji).
- **[drobne] Okienko gracza bez zwierzat - poprawione czesciowo:** liczba pedzonych zwierzat dochodzi do "TAKEN N" (`ss.STake`, tylko wyswietlanie i linia sesji - bilans v2 z niego nie korzysta). Stan stada w dymku / menu pod KS - nie (Ryzyko).
- **[drobne] R3-06d tylko czesciowo - PRAWDA, opisane uczciwie:** sekcja "Co to jest", podpowiedz MCM uzupelniona (only with the start calibration applied; a district already crowded at the start keeps its yield), wycofane zdanie o odlogu w 4 ha.
- **[drobne] Kolizja z p5-urodzaj (HerdCull, HarvestYear) - opisane** w "Scalanie" (kolejnosc wpiec, dlug stada).
- **[drobne] Brak DayProfile.Lap - poprawione.**
