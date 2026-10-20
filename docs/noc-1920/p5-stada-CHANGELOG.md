# p5-stada (noc 20.10) - R3-06c stada jako lup, R3-06d ugor i zmeczona ziemia

Galaz `w-toku/p5-stada`, baza 5189c0fe (kandydat WGRANIA 92). Jeff 20.10 o liscie 35 pomyslow: "28 - nie, 29 - nie. Pozostale tak".

## Dla Jeffa (prosto)

Wies hodowlana ma teraz prawdziwe stado. Gdy ktos ja lupi, najpierw odpedza owce, swinie i krowy (czesc trafia do jego taboru, reszta ginie), a wies przez lata daje mniej zwierzat, welny, sera i masla - owce i swinie odrastaja o ok. jedna czwarta rocznie, bydlo o ok. jedna osma, wiec po ciezkim najezdzie welna wraca dopiero po kilku latach. Druga zmiana: gdy w okregu ludzi przybywa ponad to, co jego ziemia uniesie, chlopi obsiewaja ugor i ziemia sie meczy - kazdy kolejny chlop dorzuca mniej plonu niz poprzedni, wiec gesto zaludnione okregi maja mniej nadwyzki i drozsze jedzenie, a ludnosc sama przestaje rosnac bez konca. Na starcie nic sie nie zmienia (zadna wies nie traci plonu za to, ile ma ludzi dzis) - hamulec zaczyna dzialac dopiero przy przyroscie ponad ziemie.

## R3-06c - Stada jako lup (klucz MCM `Village Herds As Loot`, domyslnie TAK)

**Problem / zrodlo pomyslu:** POMYSLY-35 poz. 14; audyt `docs/audyt-2026-10-09/02-wies-produkcja-krainy.md` rozdz. 2.2, 2.3 i E pkt 2 (stada lupem nr 1, welna Northumberland 7-8 lat po rozejmie, Bolton 43 woly). Dotad rabunek (DistrictRaid) zabieral tylko zwierzeta z magazynu wsi, a produkcja hodowli wracala razem z ludzmi - stado jako majatek okregu nie istnialo.

**Zmiana (nowy plik `Armoury/src/VillageHerds.cs`):**
- Stan stada wsi H w [0.10, 1] w dwoch czesciach: drobne (owce i swinie - towary sheep, hog, wool) i bydlo (cow, cheese, butter). Wsie z pelnym stadem nie sa przechowywane.
- Produkcja: towary stada x H swojej czesci. Wies pod KS - w `StartCalibration.ComputeDistrict` (gra, z czynnikami, po zimie i rekach E4/5, E4/7; Plan kalibracji bez zmian). Lancuch gry (wies poza KS, rampa) - `MaterialLaw.ProdPostfix` po rekach wojny E4/7 (wzor czynnik x suma) i ten sam czynnik w `ChainFactor`.
- Rabunek osada po osadzie (`DistrictRaid.Loot`, AI i gracz, przed towarem z magazynu): stado traci l = min(90%, 1.5 x s) obecnej wielkosci (s - ten sam udzial co lup z magazynu), nie mniej niz do 10%. Ubytek w sztukach gry = ubytek H x stado wsi (stawka zwierzat przy pelnym stadzie x 364 / przyrost roczny). Napastnik pedzi najwyzej 0.5 sztuki na czlowieka (owce, swinie, potem bydlo) do taboru wodza - w ksiedze towarow jako "produkcja wsi" (prawdziwe zwierzeta okregu); reszta ubytku to zabite i rozproszone (tylko licznik, bez sztuk - zniszczenie majatku, nie towar z niczego).
- Rabunek gry bez DistrictRaid (klucz rabunku osada po osadzie wylaczony): zdarzenie `VillageLooted` bez naszego ubytku w ostatnich 3 dobach - ubytek jak za pelny rabunek gry (s = 0.40, czyli l = 60%), bez zwierzat (gra daje swoj lup).
- Odrost: co dobe wies w stanie Normal: H x (1 + r)^(1/dni roku), r: drobne 25%, bydlo 12% (kalendarz Armoury, rok 364 dni); do 1.
- Zapis: klucz `arm_herds` przez `SaveText.Sync` (`v1|id:drobne:bydlo;...`); stary zapis bez klucza = pelne stada. Napis odlozony do `OnSessionLaunched` (Resolve).

**Linia logu (log_grep):** `Stada (R3-06c): dzien N | wsie z niepelnym stadem X ... | dzis rabunki ze stratami stada A (osada po osadzie) + B (rabunek gry), zwierzat do taborow C (owce i swinie, bydlo), zabite i rozproszone D szt. ... | sesja: ... | potkniecia`. Linia co dobe z rabunkiem, poza tym w dobach pelnych logow (LogGate).

## R3-06d - Ugor i zmeczona ziemia (klucz MCM `Tired Land Yield`, domyslnie TAK)

**Problem / zrodlo pomyslu:** POMYSLY-35 poz. 15; audyt 02 rozdz. 2.1 (trojpolowka, Postan) i E pkt 5; 10-pomysly F6. KS liczy plon okregu liniowo od rak (rece x P0 x przydatnosc ziemi); ziemia zajeta bloku (`KsPart.LandUse`) pilnowala tylko reguly zmiany zajec. Gdy ludzi przybywalo, kazdy nowy chlop dawal tyle samo, choc ziemi juz brakowalo.

**Zmiana (nowy plik `Armoury/src/TiredLand.cs`):**
- W `ComputeDistrict` (gra, z czynnikami): rho = suma ziemi zajetej blokow / suma ziemi blokow okregu. Baza okregu b = max(1, rho przy pierwszym rachunku w grze) - w zapisie (start neutralny, takze dla kampanii Jeffa wczytanej z nowa DLL).
- Gdy rho > b: towary ziemi (zboze, owoce i warzywa, winogrona, oliwki, daktyle, przyprawy, len, bawelna, welna, nabial, zywiec) x (b / rho)^0.6, najmniej x0.5 - plon okregu rosnie dalej, ale jak rece^0.4. Bez zmian: jaja i miod (dom), ryby, sol, drewno, kopaliny, futra, wieloryby, dziczyzna.
- Ugor jest w hektarach na pracujacego tabeli KS (zboze 4 ha - z odlogiem trojpolowki); "obsiany ugor" = ziemia zajeta ponad ziemie okregu. Pola mieszczan pod KS - tak samo.
- Zapis: klucz `arm_tiredland` (`v1|id:baza;...`).

**Linia logu (log_grep):** `Zmeczona ziemia (R3-06d): dzien N | okregi KS z ziemia X (baza z zapisu, nowa baza w sesji) | ziemia zajeta / ziemia okregu: mediana, najwiecej | ponad baze A, plon na glowe nizszy w B okregach (srednio, najnizej) | potkniecia`.

## Ryzyko / co sprawdzic (kontrola calosci)

- **Regresje:** wpiecia tylko addytywne - jedna linia w `ComputeDistrict` (wewnatrz galezi `factors`, czyli tylko gra; Plan, T1 watku roboczego i linie Planu licza `factors = false`), jedna w `ProdPostfix` + jedna w `ChainFactor` (zgodnosc utrzymana), jedna w `DistrictRaid.Loot` (wlasny try, przed towarem - `return` w bloku towaru jej nie omija), wpiecia w `ArmouryBehavior` (SyncData, RegisterEvents, doba). Klucz wylaczony = czynnik 1 i brak ubytku.
- **Kolizje:** stado mnozy sie z zima, rekami E4/5 i E4/7 (jeden wzor - czynnik x suma; w KS - mnozenie kg). Regulator towaru W3 i hodowla K4 dalej po nim w lancuchu gry. Zwierzeta z magazynu wsi DistrictRaid daje jak dotad (udzial s) - stado jest osobnym majatkiem, nie drugim liczeniem magazynu. DragonFeed je z magazynow i taborow - bez zmian. Regula zmiany zajec A7 liczy dochody bez czynnika stada i ziemi (moze wybrac zajecie, ktore potem zarabia mniej) - swiadomie poza zakresem.
- **Zamknieta ekonomia:** zwierzeta do taboru = prawdziwe zwierzeta stada (w ksiedze jako produkcja wsi, sprzedawane potem zwyklym handlem); zabite i rozproszone to zniszczenie majatku (bez sztuk i zlota). Odrost to przychowek (produkcja biologiczna), jak kazda produkcja wsi. Zmeczona ziemia tylko obniza produkcje - nic nie dopisuje.
- **Watki:** slownik stada podmieniany w calosci na watku glownym, czytany bez blokad (ProdPostfix moze isc z watkow AI); pamiec stawek zwierzat zapisywana tylko na watku glownym. TiredLand tylko watek glowny (LiveDaily). Goraca sciezka bez alokacji (switch po id towaru, pusty slownik = wyjscie od razu).
- **Skala:** stado wsi owczej (np. 10 owiec na dobe) to ok. 14,5 tys. sztuk gry - liczba "zabite i rozproszone" bedzie duza; do taborow trafia najwyzej 0.5 sztuki na napastnika (lord ze 100 ludzmi - do 50 zwierzat). Sprawdzic, czy AI z dodatkowymi zwierzetami nie zwalnia na mapie (kara stada gry) i czy sprzedaje je w miastach.
- **Ciagle wojny:** Dorzecze i Krainy Burzy moga miec stada przy 10% latami - sprawdzic w linii "najnizej" po dlugim tescie, czy welna i skory nie znikaja z rynku.
- **Zmeczona ziemia w krotkim tescie:** przyrost ludnosci +0.8% rocznie najwyzej, a baza = max(1, rho startu) - w tescie 160 dob czynnik prawie nigdzie nie zadziala; test pokaze glownie mediane rho i to, ze start jest neutralny (0 okregow z nizszym plonem w pierwszej dobie).
- Wies poza KS (lancuch gry) nie zna ziemi okregu - tam zmeczonej ziemi nie ma (dzis KS obejmuje 569/569 wsi i 92 pola mieszczan wedlug logu WGRANIA 91).

## Status

DO SPRAWDZENIA - zbudowane (Armoury, kod 0, 0 Error(s)), niewgrane, bez autotestu (zakaz w zadaniu).

## Decyzje (projektuje Claude, wylacznik = klucz MCM)

- Dwie czesci stada (drobne / bydlo) zamiast stada na kazdy gatunek - ser i maslo ida z bydlem (w KS bydlo daje ich 6 razy wiecej niz owce).
- Swinie razem z owcami (przyrost 25%) - pomysl mowi o owcach i bydle; swinie mnoza sie co najmniej tak szybko jak owce.
- Ubytek stada 1.5 x udzial trafionych, najwyzej 90% na rabunek, dno 10% stada; 0.5 sztuki pedzonej na napastnika.
- Stado w sztukach = stawka przy pelnym stadzie x 364 / przyrost roczny (przychowek rowny ubojowi w stanie ustalonym).
- Zmeczona ziemia: elastycznosc plonu do rak ponad ziemie 0.4 (czynnik na glowe (b/rho)^0.6), najmniej x0.5; baza okregu = max(1, rho pierwszego rachunku) - start neutralny.

## Test w grze

1. Autotest 120-160 dob na nowej kampanii: `grep "Stada (R3-06c)"` - po rabunkach "wsie z niepelnym stadem" > 0, "zwierzat do taborow" > 0, srednie odrastaja z doby na dobe; potkniecia 0.
2. `grep "Zmeczona ziemia (R3-06d)"` - okregi KS z ziemia ok. 661, mediana rho ponizej 1, w pierwszych dobach "plon na glowe nizszy w 0 okregach"; potkniecia 0.
3. Linia "Rabunek osada po osadzie (E7/R1)" - bilans (v2) dalej 0 (zwierzeta stada ida poza tym bilansem, w ramce produkcji wsi).
4. Linia KS "na targ jutro" - sheep, wool, cow, cheese spadaja w regionach wojny wzgledem pokoju.
