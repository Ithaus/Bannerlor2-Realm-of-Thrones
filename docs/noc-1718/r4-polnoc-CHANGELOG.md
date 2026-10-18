## 2026-10-18 (KS runda 4, paczka r4-polnoc: najpierw wyzywic siebie + prawdziwe plony zimnej ziemi; poprawki po recenzji)

**Mod:** Armoury + `tools/sprawdz_logi.py` (+ `McmSettings.cs` z `tools/gen_mcm.py`) | **Galaz:** `w-toku/r4-polnoc` (od `efbbf0dc` = noc-ks4 "KS runda 4 cz. 5") | **Tor:** L (bez gry; harness Planu kmoney na ks_dump 16.10, 5 Planow cieplych; test 120 dob w grze robi prowadzacy noc) | **Klucze (nowe):** `KsSelfFeedFirst` (TAK), `KsSelfFeedEverywhere` (NIE), `KsSelfFeedOwnFoodOnly` (NIE, po recenzji - regula Jeffa doslownie, do jego decyzji), `KsColdLand` (TAK). Wszystkie wylaczone = Plan i tabela swiata bit w bit jak dotad (harness po recenzji: zrzut okregow i wszystkie liczby Planu identyczne z biegiem bazowym, rozni sie tylko linia "KS: wyzywienie krain").

Recenzja: 7 uwag, poprawione 7, odrzucone 0 (z uwagi 5 odrzucony jeden podpunkt - 5.3, uzasadnienie nizej).

**Dla Jeffa (prosto):**
- Na Polnocy, za Murem, u Nocnej Strazy, na Skagos, Zelaznych Wyspach i w Ib wies, ktorej ludzie gloduja, najpierw stawia rece przy jedzeniu, ktore na jej ziemi daje najwiecej kalorii z reki: jeczmien i owies, krowy w oborze, owce i kozy, ryby, polowanie. Dopiero reszta rak idzie za cena (futra, welna, drewno).
- Wies, ktora za swoje futra kupi wiecej jedzenia, niz urodzi jej ziemia (po tym, co wezmie pan, dziesiecina i korona), zostaje przy futrach. Wies przesuwa rece do jedzenia tylko na tyle, na ile ma wolna ziemie - nie dostaje jedzenia z pol, ktorych nie ma.
- Zimna ziemia rodzi naprawde: jeczmien i owies na sniegu daja ok. 1/3 plonu niziny (dotad zero), krowy w oborze na sianie, owce i kozy jak na Islandii i Szetlandach, rzeki i jeziora polnocy pelne lososi. Mysliwy na Polnocy i za Murem bierze ok. 1 kg miesa dziennie (nie 54 g), ale zwierzyny jest tyle, ile dzikiej ziemi - 15 km2 na mysliwego. Dlatego Wolni Ludzie nie wyzywia sie samym polowaniem. Mysliwi cieplych krain - bez zmian.
- Wynik w rachunku Planu (harness, krainy przed -> po): Polnoc je 97% potrzeby (dotad 92%), Wolni Ludzie 79% (56%), Nocna Straz 43% (33%), Skagos 98% (92%), Zelazne Wyspy 97% (93%), Ib 99% (100%). W calym Westeros koszyk wsi bez pokrycia spada z 10.5% do 5.9% kalorii. Reszta to dowoz kupcow (r4-kupcy), danina zbozowa dla Muru (r4-mur) i prawdziwy glod.
- **Jedna sprawa do Twojej decyzji** (opis w "DO DECYZJI JEFFA" nizej): czy wies liczy tez to, co kupi za futra (tak jest teraz - lepiej dla Polnocy, Wysp i Ib), czy tylko to, co sama urodzi (Twoja regula doslownie - Wolni Ludzie jedza 94% zamiast 79%, ale Polnoc 91% zamiast 97% i w calym Westeros glodu wiecej). Oba warianty sa w kodzie; drugi wlacza "Ks Self Feed Own Food Only".
- W grze to dziala, gdy kalibracja startu (KS) zostanie wlaczona w Twoim zapisie - wtedy zajecia wiosek ustawia Plan z ta regula. Dzis w Twojej grze KS nie jest wlaczony, wiec nic sie nie zmienia.

**Problem (ks-plan.csv doba 109179, test fin80 Armoury-2026-10-17_16-26-10.log):**
- Wolni Ludzie 292 tys. ludzi pracuja tylko jako traperzy, mysliwi i przy koniach: 53 kcal jedzenia z wlasnej produkcji na glowe (potrzeba ok. 2600). Nocna Straz 50 kcal. Polnoc 2.75 mln ludzi: wheat_farm 760 tys. rak, trapper 310 tys. (futra placa lepiej).
- Linia "KS: pieniadze planu" fin80: koszyk wsi bez pokrycia - Polnoc 31.8% (877 mln kcal/d), Wolni Ludzie 52.1%, Nocna Straz 80.5%, Skagos 28.0%, Ib 20.3%.

**Przyczyna:**
- Plan (`KsCore.Shares`) wybiera zajecia bloku tylko z dochodu na reke przy cenie rynku (logit beta 10). Futra (20 d/kg) daja wiecej niz zboze (0.3 d/kg), wiec okreg na sniegu idzie w traperow, a jedzenie ma kupic z utargu. Z utargu wsi zostaje jednak tylko alfa (ok. 0.3), reszta to pan, dziesiecina, korona i kupiec.
- Tabela swiata (`KsTab`) na terenie "snieg" (wystepuje tylko w strefach 1-arktyka, 2-wyspy, 2-zimno) miala: zboze 0 (nic nie rosnie), bydlo 0.3, owce 0.6. Rzeka dawala rybakowi 0.4 takze na polnocy. Mysliwy nie potrzebowal ziemi, wiec dowolnie wielu mysliwych bralo tyle samo zwierzyny.

**Zmiana:**
- `StartCalibration.Tables.cs` (`KsTab`):
  - przelacznik `ColdLand` (ustawia go gra z klucza `KsColdLand`; zmiana podbija `LandGen`);
  - zimna ziemia jako jedna tabela swiata (bez mnoznikow okregu): snieg - zboze (jeczmien, owies, zyto; zajecie wheat_farm) 0.35, bydlo 0.5, owce i kozy (zajecie sheep_farm) 0.75; rzeka i jezioro przy rzece w strefach zimnych 0.7 zamiast 0.4;
  - mysliwy z ColdLand w STREFACH ZIMNYCH potrzebuje `HuntHa` = 1500 ha dzikiej ziemi - `KsTab.Ha(c, strefa)` / `Ha(c, blok)`; `HaPerWorker` wrocil do tabeli bazowej (readonly, bit w bit jak przed paczka); poza strefami zimnymi mysliwy bez ziemi jak dotad (recenzja);
  - zrodla i zalozenia (Norwegia, Islandia, Szkocja gorska, Szetlandy, lososie i sztokfisz, gestosci zwierzyny tajgi) opisane w komentarzu tabeli;
  - kozy, owies i jeczmien nie sa nowymi zajeciami: kozy = sheep_farm (towar sheep - gra nie ma kozy), owies i jeczmien = wheat_farm (towar grain - gra ma jedno zboze), ryby jezior = fisherman przy wodzie (plik ziemi nie zna jezior osobno);
  - `FoodKcal` (kcal na kg towaru Planu) i `FoodNeedKcal` (potrzeba jedzenia osady w kcal surowca).
- `StartCalibration.Feed.cs` (nowy plik, czysty C#, partial `KsCore` / `KsInput` / `KsResult`) - prog "najpierw wyzywic siebie":
  - `FloorStep` na poczatku kazdej rownowagi. Zdolnosc pary (kcal jedzenia, ziemia, wartosc towarow bez jedzenia) przy pelnej pracy, wzorem `BlockOutput`. Mieszanka jedzenia bloku z zadania liniowego (najwiecej kcal przy udzialach <= 1 i ziemi <= 0.98 x powierzchni; najwyzej dwie pary). Potrzeba okregu w kcal surowca minus daktyle regale. Alfa okregu jak w kroku 8.
  - `ApplyFloors` co runde po logicie. Okreg liczy "kcal dostepu" przy udzialach rynku: jedzenie + alfa x utarg bez jedzenia / cena kcal zboza (cena startu x gradient krainy kroku 5); z kluczem `KsSelfFeedOwnFoodOnly` - samo jedzenie. Gdy brakuje do potrzeby, przesuwa rece do mieszanki jedzenia, tylko z par, ktore daja mniej kcal dostepu niz mieszanka (najpierw futra i len biednej wsi). Kolejnosc od najwiekszego zysku na reke; tylko tyle, ile brakuje, najwyzej do udzialow mieszanki i (recenzja) najwyzej do wolnej ziemi bloku: ha zajete (suma udzial x `LandP` - miara renty i LandBind Planu) po progu <= 0.98 x powierzchni; blok, ktory logit juz wypchnal ponad granice, prog nie powieksza. Okreg wyzywiony - udzialy bez zmian. Suma udzialow bloku = 1.
  - `FeedSave` / `FeedRestore` (recenzja): liczby progu zapisywane z punktem kontrolnym rownowagi - rownowaga bez zbieznosci oddaje najlepszy stan, linia i A7 maja liczby tego samego stanu co udzialy.
  - `FeedStats` / `FeedLine`: linia "KS: wyzywienie krain" przy kazdym Planie:
    - jedzenie wsi = 1 - kcal koszyka bez pokrycia kroku 8 / pelna potrzeba kcal ludzi wsi w domu w tej samej mierze (`DietKcal`: koszyk przy zerze z wlasnego, KsBasket.Kcal, potrzeby R8 - recenzja; dotad mianownik z innej tabeli kcal i potrzeb startu);
    - kcal wlasne wsi i krainy (z polami mieszczan i praca domowa);
    - prog: okregi z brakiem, brak po progu ("ziemia i rece nie starcza" - recenzja: z niepokrytego braku, nie ze zdolnosci rak), rece przesuniete, okregi z ruchem ucietym ziemia;
    - ziemia blokow w zakresie: ponad 0.98 powierzchni przed / po progu, najwiekszy przyrost ziemi z progu ponad granice (tozsamosc ziemi);
    - tozsamosc rak; regula progu w naglowku ("prog na kcal dostepu" / "prog na samym wlasnym jedzeniu").
  - `KsResult.FeedShort` - okregi z brakiem kcal dostepu przy udzialach rynku (dla A7).
  - Zakres: okregi wsi stref zimnych (`SelfFeed` 1), wszystkie okregi (`KsSelfFeedEverywhere`, 2), pola mieszczan nigdy.
- `StartCalibration.Core.cs` (haki): `KsInput` / `KsResult` partial, `FloorStep` w `Equilibrium`, `ApplyFloors` na koncu `Shares` (obie sciezki), `FeedSave` / `FeedRestore` przy punkcie kontrolnym, `FeedStats` w `Finish`, kcal robione w domu, bez pokrycia i pelna potrzeba do wyniku (krok 8); `Pairs` liczy ha pary z `KsTab.Ha` (`Ctx.PHa`, renta w `Shares` z niej); `SubPhys` - ziemia wedlug bloku (mysliwy na zimnej ziemi; bez klucza jak dotad).
- `StartCalibration.Deliver.cs`: pamiec przydatnosci ziemi bloku (`KsPart.L`, `LGrain`) od nowa po zmianie `KsTab.LandGen`; `KsPart.LandOf` z `KsTab.Ha(c, K)`.
- `StartCalibration.Rule.cs`: dochod A7 z renta x `KsTab.Ha(c, b.K)`; licznik progu w linii reguly.
- `StartCalibration.Polnoc.cs` (nowy, strona gry):
  - `SyncColdLand` przy starcie sesji, co dobe i przy zrzucie Planu;
  - `SelfFeedMode` -> `KsInput.SelfFeed`, `SelfFeedOwnOnly` -> `KsInput.SelfFeedOwn`;
  - `FeedLineLog` (linia Planu);
  - regula A7 (klucz Start Calibration Switch Rule, domyslnie wylaczony): okreg z `FeedShort` ostatniego Planu (brak kcal dostepu przy udzialach rynku - ten sam warunek co prog Planu; recenzja: dotad kazdy okreg strefy zimnej, takze wyzywiony) nie przechodzi z jedzenia na zajecie bez jedzenia. Licznik w linii reguly ("najpierw wyzywic siebie (...): zmiany z jedzenia na zajecie tylko na targ wstrzymane N").
- `StartCalibration.Plan.cs`, `StartCalibration.cs`: wywolania powyzszych (po jednej linii).
- `Settings.cs` + `McmSettings.cs` (gen_mcm): blok "KS round 4: feed yourself first and the yields of cold land (night 17/18.10)" - 4 klucze, opisy po angielsku (Ks Cold Land: "a hunter in the cold lands", mysliwi cieplych krain bez zmian).
- `tools/sprawdz_logi.py`: grupa `npolnoc` (NKS-30):
  - NKS-30-0 linie i klucze;
  - NKS-30a Polnoc kraina >= 0.85;
  - NKS-30b jedzenie wsi: Polnoc >= 0.90, Wolni >= 0.70, Skagos >= 0.80, Ib >= 0.93, Zelazne Wyspy >= 0.90 (Nocna Straz, Lorath - INFO);
  - NKS-30c prog dziala;
  - NKS-30d tozsamosc rak <= 1e-6;
  - NKS-30e mediana niezbieznych Planow <= 10 (z `--baza-doby`: baza + 2; recenzja: opis w uzyciu poprawiony z "<= 8");
  - NKS-30f-i INFO;
  - NKS-30j (recenzja) ziemia: najwiekszy przyrost ziemi z progu ponad 0.98 powierzchni bloku <= 1e-6 (w wierszu bloki ponad granica przed / po progu i okregi z ruchem ucietym ziemia).
  - Na logu 2026-10-17_15-37-31 dziala bez wyjatku (brak linii = INFO, 12 progow, NIE 0); na 2026-10-17_16-26-10 tez; na logu syntetycznym z linii harnessu: 16 progow, NIE 0, NKS-30j TAK (7e-16). nks na 15-37-31 bez zmian (24/53).

**Recenzja (7 uwag) - co zrobione:**
1. (wazne) Prog nie liczyl ziemi par, ktore zostaja, i proporcji logitu - blok mogl wyjsc ponad powierzchnie (takze mysliwy ponad 1500 ha). POPRAWIONE: wolna ziemia bloku w `ApplyFloors` (ruch uciety do wolnej ziemi, blok ponad granica nie rosnie), tozsamosc ziemi w linii i NKS-30j. Harness: bloki w zakresie ponad 0.98 powierzchni przed progiem 208, po progu 196 (to bloki z renta - logit), okregi z ruchem ucietym ziemia 17 (Polnoc 3, Zelazne Wyspy 14), najwiekszy przyrost ziemi z progu 7e-16 powierzchni bloku.
2. (wazne) Regula to "wyzyw sie, gdy dostep przy rynku nie starcza", nie "najpierw uprawiaj swoje". POPRAWIONE opisem i kluczem: odstepstwo i liczby obu wariantow w "DO DECYZJI JEFFA" (STAN-PRAC uzupelnia prowadzacy noc - ta galaz STAN-PRAC nie zmienia); regula doslownie pod nowym kluczem `KsSelfFeedOwnFoodOnly` (NIE). Opcjonalnego dyskonta kcal dostepu czescia zaplacona kroku 8 nie wprowadzalem: zakupy kroku 8 sa ograniczone potrzeba (wies bogata kupuje caly koszyk, wiec "kcal za monete" wychodzi zanizony i prog ruszalby wyzywione wsie) - to zmiana reguly, ktora wymaga osobnego strojenia w harnessie i decyzji Jeffa.
3. (drobne) HuntHa szlo na caly swiat (ok. 1570 blokow ciepla z mysliwym w kandydatach). POPRAWIONE: `KsTab.Ha(c, strefa)` - 1500 ha tylko w strefach zimnych (1-arktyka, 2-wyspy, 2-zimno), poza nimi tabela bazowa; wszystkie miejsca uzycia (Pairs, renta w Shares, PairFood, SubPhys, KsPart.LandOf, dochod A7) wedlug strefy bloku.
4. (drobne) A7 wstrzymywal kazda zmiane jedzenie -> targ w strefie zimnej, takze we wsi wyzywionej. POPRAWIONE: tylko okreg z `FeedShort` ostatniego Planu (ta sama zasada co prog Planu). Po wczytaniu do pierwszego Planu sesji lista pusta - okreg idzie za cena (A7 i tak domyslnie wylaczone).
5. (drobne) Liczby linii niespojne ze stanem Planu:
   - 5.1 liczby progu z ostatniej rundy przy przywroconym punkcie kontrolnym - POPRAWIONE (`FeedSave` / `FeedRestore`);
   - 5.2 "jedzenie" w innej mierze niz licznik i linia "Plan bez pokrycia wedlug krainy" - POPRAWIONE (`DietKcal`: ta sama miara kcal i potrzeby R8; linia "Plan bez pokrycia" dzieli te same kcal przez koszyk kupowany - mianownik inny z definicji, opisany w linii);
   - 5.3 slowniki kroku 8 nie czyszczone miedzy przebiegami - ODRZUCONE: `DistUncKcal` / `DistHomeKcalAbs` / `DistDietKcal` pisze tylko ostatnia runda kroku 8 (`last`) dla kazdego okregu listy `vs`, a lista jest ta sama w kazdym przebiegu MoneyStep (budowana z tych samych warunkow: wies z ludzmi w domu, koszykiem i blokami; w petli okregu nie ma `continue` przed `if (last)`), przerwanie zwraca Plan z bledem; `KsResult` jest nowy na kazdy Plan. Stara wartosc nie moze zostac - czyszczenie byloby kodem "na wszelki wypadek" (CLAUDE.md 8.3).
6. (drobne) W zapisie z KS juz zastosowanym zimna ziemia zmienia produkcje od wczytania, a prog wchodzi dopiero z nowym wlaczeniem. PRZYJETE opisem (Ryzyko nizej): u Jeffa KS nie jest zastosowany (wlaczenie bierze Plan z obiema regulami naraz); zapis z KS zastosowanym dostaje prawdziwe plony na tych samych rekach (zboze wlasne na sniegu to samo "najpierw wyzywic siebie"), bez nowych danych w zapisie; test ze starego zapisu z KS - wlaczyc KS od nowa (Arm + Confirm).
7. (drobne) Opis NKS-30e mowil "<= 8", kod 10. POPRAWIONE: "<= 10 (z --baza-doby: baza + 2)" w uzyciu, w opisie funkcji i w wierszu bez linii.

**DO DECYZJI JEFFA (odstepstwo od decyzji 17.10 "okreg najpierw stawia rece przy jedzeniu ..., nadwyzka rak na targ"):**
- Domyslnie (klucz `KsSelfFeedOwnFoodOnly` = NIE) wies liczy "kcal dostepu": to, co urodzi, plus to, co kupi za futra, welne i len po czesci pana, kosciola i korony (alfa kroku 8), po cenie zboza w swojej krainie. Wies, ktorej futra kupia wiecej jedzenia, niz urodzi snieg, zostaje przy futrach.
- Doslownie (klucz = TAK) wies liczy tylko wlasne jedzenie i najpierw stawia rece przy jedzeniu, takze gdy futra kupilyby wiecej.
- Harness (Plan 5, jedzenie wsi / kcal wlasne krainy): Polnoc 0.97 / 1.09 (domyslnie) vs 0.91 / 1.03 (doslownie); Wolni Ludzie 0.79 / 0.33 vs 0.94 / 0.70; Nocna Straz 0.43 / 0.44 vs 0.48 / 0.48; Skagos 0.98 / 0.87 vs 0.98 / 0.92; Ib 0.99 / 0.75 vs 0.96 / 0.76; Zelazne Wyspy 0.97 / 0.82 vs 0.90 / 0.82. Westeros bez pokrycia 5.9% vs 9.6%; mieso NA TARG 0.99 vs 0.95 (NKS-3), ryby 0.95 vs 0.99, welna 1.10 vs 1.06.
- Krainy, ktore przy domyslnej regule nie zyskuja albo traca wobec biegu bez kluczy: Ib 1.00 -> 0.99 (szum), Polnoc z samym progiem (bez zimnej ziemi) 0.92 -> 0.91. Skagos przy tej wersji juz nie traci (0.92 -> 0.98; Plany 0.92-1.00 - mala kraina, szum Planu).
- Wybor: domyslna regula daje mniej glodu w calym Westeros; doslowna - Wolni Ludzie i Nocna Straz zyja z wlasnych stad, Polnoc i Wyspy glodniejsze, mniej miesa na targu. Mozna tez zostawic domyslna i dac Wolnym Ludziom dowoz / najazdy (r4-mur).

**Wynik w Planie po recenzji (harness kmoney h4 na ks_dump 16.10, budzety i alfa z gry 01-15-30, swiat r2, 5 Planow cieplych; Plan 5; "jedzenie" w nowej mierze):**

| kraina | jedzenie wsi bez kluczy -> z kluczami | kcal wlasne krainy bez -> z | sama zimna ziemia | sam prog | doslownie (Own Food Only + zimna ziemia) |
|---|---|---|---|---|---|
| Polnoc (battania) | 0.92 -> 0.97 | 1.00 -> 1.09 | 0.97 / 1.09 | 0.91 / 1.00 | 0.91 / 1.03 |
| Wolni Ludzie | 0.56 -> 0.79 | 0.35 -> 0.33 | 0.76 / 0.27 | 0.56 / 0.35 | 0.94 / 0.70 |
| Nocna Straz | 0.33 -> 0.43 | 0.33 -> 0.44 | 0.45 / 0.44 | 0.32 / 0.32 | 0.48 / 0.48 |
| Skagos | 0.92 -> 0.98 | 0.82 -> 0.87 | 1.00 / 0.90 | 1.00 / 0.86 | 0.98 / 0.92 |
| Ib | 1.00 -> 0.99 | 0.69 -> 0.75 | 0.99 / 0.75 | 1.00 / 0.69 | 0.96 / 0.76 |
| Zelazne Wyspy | 0.93 -> 0.97 | 0.78 -> 0.82 | 0.97 / 0.80 | 0.95 / 0.80 | 0.90 / 0.82 |

- Westeros: Plan bez pokrycia 10.5% -> 5.9% kcal koszyka wsi (przed recenzja 5.5% - prog nie bierze juz ziemi, ktorej blok nie ma); Essos 1.8% -> 2.0%.
- Koszyk bez pokrycia (mln kcal/d): Polnoc 582 -> 222, Wolni 328 -> 155, Wyspy 77 -> 36, Straz 33 -> 29, Skagos 10 -> 3, Ib 0 -> 9.
- Rece przesuniete progiem: 0.47% rak pola wsi swiata (Polnoc 2.5%, Wolni 7.0%, Straz 95.8%, Skagos 8.7%, Ib 7.5%, Wyspy 14.7%).
- Bilans NA TARG Westeros: zboze 1.00, mieso 0.99 (bez kluczy 0.98), ryby 0.95 (0.95), welna 1.10 (1.08; przed recenzja 1.14).
- Niezbiezne towary Planu 5: 8 (bez kluczy 7; przed recenzja 11).
- Czas 5 Planow 71 s (bez kluczy 62 s; biegi rownolegle, szum).
- Klucze wylaczone: zrzut okregow (csv) bajt w bajt jak bieg bazowy paczki i jak bieg sprzed paczki (base0, harness bez linii r4-polnoc), wszystkie liczby Planu te same.
- Proby odrzucone po drodze (harness): prog "Everywhere": Essos niestabilny (winogrona 0.60, pomarancze 1.62), Westeros 7.6% - dlatego `KsSelfFeedEverywhere` = NIE.

**Ryzyko / co sprawdzic (kontrola calosci):**
- Regresje:
  - klucze wylaczone = harness bit w bit (zrzut okregow i bilans identyczne jak bieg bazowy); jedyna roznica to linia "KS: wyzywienie krain" (diagnostyka, takze przy kluczach wylaczonych);
  - `HaPerWorker` znow tablica readonly jak przed paczka; `KsTab.Ha(c, strefa)` bez klucza = `HaPerWorker[c]`; renta w `Shares` z tablicy pary `PHa` (budowana w `Pairs` razem z `LandP`, takze po kroku 8);
  - `SubPhys`: sufit z wolnej ziemi tylko, gdy wszystkie bloki z plonem sa "na ziemi" (bez klucza: zboze i daktyle zawsze, ryby i dziczyzna nigdy - jak dotad);
  - zmiana `KsColdLand` w trakcie gry - pamiec przydatnosci blokow od nowa (`LandGen`); lista kandydatow A7 (`b.Cand`) zostaje do przebudowy okregu (A7 domyslnie wylaczone); przelaczenie klucza w trakcie Planu (12-14 s, watek roboczy czyta `KsTab.ColdLand`) daje jeden Plan mieszany.
- Kolizje:
  - prog dziala tylko w Planie. W stanie zastosowanym zajecia blokow bierze z Planu tylko wlaczenie (`BuildLive`), wiec u Jeffa prog wejdzie przy wlaczeniu KS (paczka ks-arm) razem z zimna ziemia - jeden stan.
  - Zapis z KS JUZ zastosowanym (nie u Jeffa; zapisy testowe): od wczytania zimna ziemia zmienia produkcje okregow (zboze na sniegu 0 -> 0.35, czyli zboze wlasne domow z rak zajec, rzeki x1.75, obora x1.67, owce x1.25, mysliwi stref zimnych na 1500 ha w `LandUse`), a rece bloku zostaja z dnia wlaczenia - prog wejdzie dopiero z nowym wlaczeniem. Test ze starego zapisu z KS: Arm + Confirm od nowa.
  - r4-kupcy (dowoz) i r4-mur (danina dla Muru) przykrywaja reszte; ich linie i liczniki sa niezalezne od tej paczki. r4-mur liczy danine z braku Strazy - z ta paczka brak Strazy mniejszy (koszyk bez pokrycia 33 -> 29 mln kcal/d).
  - Scalenie: `Core.cs` - kilka linii (Pairs, Shares, Equilibrium x2, SubPhys, krok 8); `Settings.cs` - nowy blok po `StartCalibrationTownCrafts`; `sprawdz_logi.py` - blok w uzyciu, linia wyboru grupy i funkcje przed `__main__`; `Rule.cs`, `Deliver.cs`, `Plan.cs`, `StartCalibration.cs` - po jednej linii.
- Zamknieta ekonomia:
  - prog przesuwa tylko rece bloku miedzy jego zajeciami (suma udzialow = 1, w linii odchylenie <= 7e-16) i tylko na wolna ziemie bloku (przyrost ziemi z progu ponad granice <= 7e-16 powierzchni);
  - produkcja dalej = rece x tabela swiata x ziemia; zero nowych platnosci i towarow z niczego;
  - pieniadz wsi liczy dalej krok 8 (alfa x utarg).
- Cudzy kod: nie dotyczy (tylko Plan KS; gra, BK i ROT bez nowych latek).
- Zapis: bez nowych kluczy SyncData; stan liczy sie z kluczy i Planu (lista A7 `FeedShort` z ostatniego Planu sesji). Stary zapis, starsza DLL i Ironman Jeffa bez zmian.
- Harnessy (port test, kmoney): dopisac `StartCalibration.Feed.cs` do listy plikow csproj (Core wola `FloorStep`, `ApplyFloors`, `FeedSave`, `FeedRestore`, `FeedStats`, `DietKcal`). Przelaczniki harnessu: `KsCore.FeedOnlyLands`, `FeedFullWork`, `FeedAccess` (false = jak klucz Own Food Only); env w kopii h4: `KS_FEED`, `KS_COLD`, `KS_FEEDONLY`, `KS_FEEDHW`, `KS_FEEDACC`.
- Do sprawdzenia w tescie 120 dob (flaga armoury-ks-wlacz.flag):
  - `--grupa npolnoc` (NKS-30a/b, NKS-30j ziemia);
  - NKS-3 (bilans NA TARG: mieso, ryby, welna Westeros);
  - NKS-15 (glod) i NKS-8c (kcal bez pokrycia);
  - linia "KS: pieniadze planu" - Plan bez pokrycia wedlug krainy (Polnoc, Wolni, Straz, Wyspy w dol);
  - koszt Planu NKS-2 <= 30 s.
- Liczby w grze beda inne niz w harnessie (inni ludzie, alfa z praw DS, uchodzcy). Progi NKS-30 sa ok. 5-10% ponizej harnessu, a ich zrodlo opisane jest w komentarzu grupy. "jedzenie" jest po recenzji w nowej mierze (bez kluczy Polnoc 0.92 zamiast 0.91, Wolni 0.56 zamiast 0.52, Straz 0.33 zamiast 0.27) - progi NKS-30b bez zmian.
- Nie zrobione: strefa klimatu Nocnej Strazy. Plik ziemi daje okregom Strazy strefe 1-arktyka z kultury (generator `tools/ks/ks_ziemia.py`, zone_of). Dar (wsie Strazy na poludnie od Muru) klimatem odpowiada Polnocy (2-zimno) - wtedy Straz mialaby jeczmien i owies. To nowy arm_ks_land.tsv: nowe crc pliku ziemi (zapis z KS zastosowanym traci stan, uzbrojony - Arm od nowa), kolizja z regeneracja ukladu wiosek (sw-przysiolki) i z danina r4-mur liczona z braku Strazy - decyzja Jeffa / prowadzacego po scaleniu.

**Status:** DO SPRAWDZENIA (kod + harness; test 120 dob w grze przed wgraniem - prowadzacy noc).
