## 2026-10-18 (KS runda 4, paczka r4-polnoc: najpierw wyzywic siebie + prawdziwe plony zimnej ziemi)

**Mod:** Armoury + `tools/sprawdz_logi.py` (+ `McmSettings.cs` z `tools/gen_mcm.py`) | **Galaz:** `w-toku/r4-polnoc` (od `efbbf0dc` = noc-ks4 "KS runda 4 cz. 5") | **Tor:** L (bez gry; harness Planu kmoney na ks_dump 16.10, 5 Planow cieplych; test 120 dob w grze robi prowadzacy noc) | **Klucze (nowe):** `KsSelfFeedFirst` (TAK), `KsSelfFeedEverywhere` (NIE), `KsColdLand` (TAK). Wszystkie wylaczone = Plan i tabela swiata bit w bit jak dotad (harness: wynik identyczny z biegiem bazowym, poza jedna nowa linia logu).

**Dla Jeffa (prosto):**
- Na Polnocy, za Murem, u Nocnej Strazy, na Skagos, Zelaznych Wyspach i w Ib wies, ktorej ludzie gloduja, najpierw stawia rece przy jedzeniu, ktore na jej ziemi daje najwiecej kalorii z reki: jeczmien i owies, krowy w oborze, owce i kozy, ryby, polowanie. Dopiero reszta rak idzie za cena (futra, welna, drewno).
- Wies, ktora za swoje futra kupi wiecej jedzenia, niz urodzi jej ziemia (po tym, co wezmie pan, dziesiecina i korona), zostaje przy futrach. Regula nikogo nie robi glodniejszym.
- Zimna ziemia rodzi naprawde: jeczmien i owies na sniegu daja ok. 1/3 plonu niziny (dotad zero), krowy w oborze na sianie, owce i kozy jak na Islandii i Szetlandach, rzeki i jeziora polnocy pelne lososi. Mysliwy bierze ok. 1 kg miesa dziennie (nie 54 g), ale zwierzyny jest tyle, ile dzikiej ziemi - 15 km2 na mysliwego. Dlatego Wolni Ludzie nie wyzywia sie samym polowaniem.
- Wynik w rachunku Planu (harness, krainy przed -> po): Polnoc je 96% potrzeby (dotad 91%), Wolni Ludzie 79% (52%), Nocna Straz 39% (27%), Zelazne Wyspy 97% (93%), Ib 98% (100%), Skagos 85% (91% - mala wyspa nie wyzywi 50 tys. ludzi polowaniem). W calym Westeros koszyk wsi bez pokrycia spada z 10.5% do 5.5% kalorii. Reszta to dowoz kupcow (r4-kupcy), danina zbozowa dla Muru (r4-mur) i prawdziwy glod.
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
  - mysliwy z ColdLand potrzebuje `HuntHa` = 1500 ha dzikiej ziemi (`HaPerWorker` jest teraz wlasciwoscia: tabela bazowa albo zimna);
  - zrodla i zalozenia (Norwegia, Islandia, Szkocja gorska, Szetlandy, lososie i sztokfisz, gestosci zwierzyny tajgi) opisane w komentarzu tabeli;
  - kozy, owies i jeczmien nie sa nowymi zajeciami: kozy = sheep_farm (towar sheep - gra nie ma kozy), owies i jeczmien = wheat_farm (towar grain - gra ma jedno zboze), ryby jezior = fisherman przy wodzie (plik ziemi nie zna jezior osobno);
  - `FoodKcal` (kcal na kg towaru Planu) i `FoodNeedKcal` (potrzeba jedzenia osady w kcal surowca).
- `StartCalibration.Feed.cs` (nowy plik, czysty C#, partial `KsCore` / `KsInput` / `KsResult`) - prog "najpierw wyzywic siebie":
  - `FloorStep` na poczatku kazdej rownowagi. Zdolnosc pary (kcal jedzenia, ziemia, wartosc towarow bez jedzenia) przy pelnej pracy, wzorem `BlockOutput`. Mieszanka jedzenia bloku z zadania liniowego (najwiecej kcal przy udzialach <= 1 i ziemi <= 0.98 x powierzchni; najwyzej dwie pary). Potrzeba okregu w kcal surowca minus daktyle regale. Alfa okregu jak w kroku 8.
  - `ApplyFloors` co runde po logicie. Okreg liczy "kcal dostepu" przy udzialach rynku: jedzenie + alfa x utarg bez jedzenia / cena kcal zboza (cena startu x gradient krainy kroku 5). Gdy brakuje do potrzeby, przesuwa rece do mieszanki jedzenia, tylko z par, ktore daja mniej kcal dostepu niz mieszanka (najpierw futra i len biednej wsi). Kolejnosc od najwiekszego zysku na reke; tylko tyle, ile brakuje, i najwyzej do udzialow mieszanki. Okreg wyzywiony - udzialy bez zmian. Suma udzialow bloku = 1.
  - `FeedStats` / `FeedLine`: linia "KS: wyzywienie krain" przy kazdym Planie:
    - jedzenie wsi = 1 - kcal koszyka bez pokrycia kroku 8 / potrzeba ludzi wsi;
    - kcal wlasne wsi i krainy (z polami mieszczan i praca domowa);
    - prog: okregi z brakiem, rece przesuniete;
    - tozsamosc rak.
  - Zakres: okregi wsi stref zimnych (`SelfFeed` 1), wszystkie okregi (`KsSelfFeedEverywhere`, 2), pola mieszczan nigdy.
- `StartCalibration.Core.cs` (haki): `KsInput` / `KsResult` partial, `FloorStep` w `Equilibrium`, `ApplyFloors` na koncu `Shares` (obie sciezki), `FeedStats` w `Finish`, kcal robione w domu i bez pokrycia do wyniku (krok 8), `SubPhys` - ziemia dla zajecia z ha na pracujacego (mysliwy przy ColdLand; bez klucza jak dotad).
- `StartCalibration.Deliver.cs`: pamiec przydatnosci ziemi bloku (`KsPart.L`, `LGrain`) od nowa po zmianie `KsTab.LandGen` (zmiana klucza Ks Cold Land w trakcie gry).
- `StartCalibration.Polnoc.cs` (nowy, strona gry):
  - `SyncColdLand` przy starcie sesji, co dobe i przy zrzucie Planu;
  - `SelfFeedMode` -> `KsInput.SelfFeed`;
  - `FeedLineLog` (linia Planu);
  - regula A7 (klucz Start Calibration Switch Rule, domyslnie wylaczony): przy progu czesc okregu w zakresie nie przechodzi z jedzenia na zajecie bez jedzenia. Licznik w linii reguly ("najpierw wyzywic siebie (...): zmiany z jedzenia na zajecie tylko na targ wstrzymane N").
- `StartCalibration.Plan.cs`, `StartCalibration.cs`, `StartCalibration.Rule.cs`: wywolania powyzszych (po jednej linii).
- `Settings.cs` + `McmSettings.cs` (gen_mcm): blok "KS round 4: feed yourself first and the yields of cold land (night 17/18.10)" - 3 klucze, opisy po angielsku.
- `tools/sprawdz_logi.py`: grupa `npolnoc` (NKS-30):
  - NKS-30-0 linie i klucze;
  - NKS-30a Polnoc kraina >= 0.85;
  - NKS-30b jedzenie wsi: Polnoc >= 0.90, Wolni >= 0.70, Skagos >= 0.80, Ib >= 0.93, Zelazne Wyspy >= 0.90 (Nocna Straz, Lorath - INFO);
  - NKS-30c prog dziala;
  - NKS-30d tozsamosc rak <= 1e-6;
  - NKS-30e mediana niezbieznych Planow <= 10 (z `--baza-doby`: baza + 2);
  - NKS-30f-i INFO.
  - Na logu 2026-10-17_15-37-31 dziala bez wyjatku (brak linii = INFO); nks na tym logu bez zmian (24/53).

**Wynik w Planie (harness kmoney h4 na ks_dump 16.10, budzety i alfa z gry 01-15-30, swiat r2, 5 Planow cieplych; Plan 5):**

| kraina | jedzenie wsi przed -> po | kcal wlasne krainy przed -> po | sama zimna ziemia | sam prog |
|---|---|---|---|---|
| Polnoc (battania) | 0.91 -> 0.96 | 1.00 -> 1.08 | 0.96 / 1.08 | 0.86 / 0.96 |
| Wolni Ludzie | 0.52 -> 0.79 | 0.35 -> 0.41 | 0.76 / 0.31 | 0.47 / 0.42 |
| Nocna Straz | 0.27 -> 0.39 | 0.33 -> 0.45 | 0.45 / 0.49 | 0.38 / 0.44 |
| Skagos | 0.91 -> 0.85 | 0.82 -> 0.70 | 0.77 / 0.67 | 0.84 / 0.72 |
| Ib | 1.00 -> 0.98 | 0.69 -> 0.75 | 0.99 / 0.75 | 1.00 / 0.69 |
| Zelazne Wyspy | 0.93 -> 0.97 | 0.78 -> 0.82 | 0.97 / 0.81 | 0.97 / 0.81 |

- Westeros: Plan bez pokrycia 10.5% -> 5.5% kcal koszyka wsi; Essos 1.8% -> 1.9%.
- Koszyk bez pokrycia (mln kcal/d): Polnoc 582 -> 261, Wolni 328 -> 145, Straz 33 -> 28, Skagos 10 -> 17, Ib 0 -> 10, Wyspy 77 -> 32.
- Rece przesuniete progiem: 0.5% rak pola wsi swiata (Polnoc 2.4%, Straz 97%, Wyspy 18%).
- Bilans NA TARG Westeros: zboze 1.00, mieso 0.99 (baza 0.98), ryby 0.93 (0.95), welna 1.14 (1.08).
- Niezbiezne towary Planu 5: 11 (baza 7; roznica to owoce Essos i welna/len - w Planie 4 w pasmie, szum Planu).
- Czas 5 Planow 78 s (baza 73 s).
- Proby odrzucone po drodze (harness):
  - prog na samych kcal (bez kcal dostepu) robil Polnoc glodniejsza (koszyk bez pokrycia 582 -> 650 - futra kupowaly wiecej jedzenia, niz rodzi snieg) i mieso NA TARG Westeros 0.92-0.93 (NKS-3);
  - prog "Everywhere": Essos niestabilny (winogrona 0.60, pomarancze 1.62), Westeros 7.6% - dlatego `KsSelfFeedEverywhere` = NIE.

**Ryzyko / co sprawdzic (kontrola calosci):**
- Regresje:
  - klucze wylaczone = harness bit w bit (wszystkie liczby Planu i bilans identyczne jak bieg bazowy); jedyna roznica to nowa linia "KS: wyzywienie krain" (diagnostyka, takze przy kluczach wylaczonych);
  - `HaPerWorker` jako wlasciwosc - tylko odczyty indeksem (Shares bierze tabele raz na runde);
  - zmiana `KsColdLand` w trakcie gry - pamiec przydatnosci blokow od nowa (`LandGen`), lista kandydatow A7 (`b.Cand`) zostaje do przebudowy okregu (A7 domyslnie wylaczone).
- Kolizje: prog dziala tylko w Planie. W stanie zastosowanym zajecia blokow bierze z Planu tylko wlaczenie (`BuildLive`), wiec u Jeffa prog wejdzie przy wlaczeniu KS. Zimna ziemia od razu zmienia produkcje okregow przy KS zastosowanym (`LandEff`). r4-kupcy (dowoz) i r4-mur (danina dla Muru) przykrywaja reszte. Ich linie i liczniki sa niezalezne od tej paczki.
- Zamknieta ekonomia:
  - prog przesuwa tylko rece bloku miedzy jego zajeciami (suma udzialow = 1, w linii odchylenie 4e-16);
  - produkcja dalej = rece x tabela swiata x ziemia;
  - zero nowych platnosci i towarow z niczego;
  - pieniadz wsi liczy dalej krok 8 (alfa x utarg).
- Cudzy kod: nie dotyczy (tylko Plan KS; gra, BK i ROT bez nowych latek).
- Zapis: bez nowych kluczy SyncData; stan liczy sie z kluczy i Planu. Stary zapis, starsza DLL i Ironman Jeffa bez zmian.
- Harnessy (port test, kmoney): dopisac `StartCalibration.Feed.cs` do listy plikow csproj (Core wola `FloorStep`, `ApplyFloors`, `FeedStats`). Przelaczniki harnessu: `KsCore.FeedOnlyLands`, `FeedFullWork`, `FeedAccess`; env w kopii h4: `KS_FEED`, `KS_COLD`, `KS_FEEDONLY`, `KS_FEEDHW`, `KS_FEEDACC`.
- Do sprawdzenia w tescie 120 dob (flaga armoury-ks-wlacz.flag):
  - `--grupa npolnoc` (NKS-30a/b);
  - NKS-3 (bilans NA TARG: mieso, ryby, welna Westeros);
  - NKS-15 (glod) i NKS-8c (kcal bez pokrycia);
  - linia "KS: pieniadze planu" - Plan bez pokrycia wedlug krainy (Polnoc, Wolni, Straz w dol; Skagos moze w gore - polowanie na malej wyspie);
  - koszt Planu NKS-2 <= 30 s.
- Liczby w grze beda inne niz w harnessie (inni ludzie, alfa z praw DS, uchodzcy). Progi NKS-30 sa ok. 5-10% ponizej harnessu, a ich zrodlo opisane jest w komentarzu grupy.
- Nie zrobione: strefa klimatu Nocnej Strazy. Plik ziemi daje okregom Strazy strefe 1-arktyka z kultury (generator `tools/ks/ks_ziemia.py`, zone_of). Daru (wsie Strazy na poludnie od Muru) klimatem odpowiada Polnocy (2-zimno). Gdyby tak bylo, Straz mialaby jeczmien i owies. To zmiana danych ziemi (nowy arm_ks_land.tsv) - decyzja Jeffa.

**Status:** DO SPRAWDZENIA (kod + harness; test 120 dob w grze przed wgraniem - prowadzacy noc).
