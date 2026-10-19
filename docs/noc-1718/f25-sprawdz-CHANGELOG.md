## 2026-10-19 (paczka f25-sprawdz) - DECYZJE JEFFA 2a I 3c: JEDWAB (ZC1-16b) I AKSAMIT (ZC1-4) - ZMIANA SAMEGO POMIARU, GRA BEZ ZMIAN

**Polecenie Jeffa (doslownie, 19.10):** "1.a 2.a 3.c 4. b 5. c 6. tak 7. tak". Ta paczka robi odpowiedzi 2 i 3 (tylko checker):
- 2a - "Jedwab: ok. 23% jedwabiu rosnie na polach mieszczan KS, wsie jedwabne -28% produkcji, -40% wozow. a) w porzadku - zmieniam tylko pomiar".
- 3c - "Aksamit tani tylko w 7-32% miast przy celu 50% ... c) zmienic cel: aksamit ma byc tani w miastach z tkalnia i w ich okolicy, dalej drogi jak luksus - w grze nic sie nie zmienia, tylko pomiar".

**Mod:** tylko narzedzie `tools/sprawdz_logi.py`, grupa `nzc1`. Kod gry BEZ ZMIAN (`git diff 6d577536 -- Armoury/ RealisticCaptivity/ GrandTourney/ ForgeView/ CrashScribe/` pusty) | **Galaz:** `w-toku/f25-sprawdz` (od `6d577536` = WGRANIE 85 + CHANGELOG) | **Tor:** bez gry (build Armoury kod 0, te same 4 stare ostrzezenia; checker na logach fin95/fin97/fin98 i na logach syntetycznych) | **Klucze:** zadnych nowych, zadna domyslna nie zmieniona (gen_mcm uruchomiony - 2006 ustawien, bez zmian w plikach) | **Zapis:** bez zmian.

**Problem:** po WGRANIU 85 dwa wiersze `nzc1` mierzyly cos innego, niz Jeff chce:
- ZC1-16b (utarg wozow wsi - jedwab) porownywal sam utarg z baza: fin97 0.78 (NIE), fin95 0.83. Spadek to jedwab, ktory przy KS rosnie na polach mieszczan (ok. 23% produkcji, ZC1-16c: fin97 wsie 37.7 -> 27.3/d, pola 0 -> 8.3/d) i idzie prosto na polke miasta, bez wozu wsi. Jeff (2a): to w porzadku.
- ZC1-4 (aksamit) liczyl miasta z indeksem < 10 w CALYM swiecie (cel 50%): fin95 29%, fin97 7%, fin98 32%. Jeff (3c): aksamit ma byc tani w miastach z tkalnia i w ich okolicy, a dalej drogi.

**Przyczyna:** ZC1-16b - prog na samym stosunku utargu (f24-rynek po recenzji zostawil przeliczenie przez czesc produkcji wsi tylko jako INFO "do decyzji"). ZC1-4 - cel projektu ZC1 (50% wszystkich miast) zmieniony decyzja 3c.

**Zmiana (tylko `nzc1` w `tools/sprawdz_logi.py`):**
- **ZC1-16b:** prog `utarg >= 0.80 x baza x czesc produkcji wsi` (czesc = (produkcja - "pola mieszczan (KS)") / produkcja z ksiegi towarow, ta sama liczba, ktora f24-rynek pisal jako INFO). Sam stosunek utargu do bazy (dawny prog) - INFO w napisie wiersza; ZC1-16c (produkcja samych wsi) zostaje INFO. Gdy czesc sie nie zmienia (ruda srebra, bieg bez KS, baza z KS) - baza bez przeliczenia, jak dotad.
- **ZC1-4:** prog na miastach z tkalnia aksamitu, >= 50%. Log nie ma indeksu aksamitu wedlug miasta - ma tylko srednia indeksu w miastach z tkalnia (linia "Zbytki ludu (P3-P5)", pole "cena u wytworcy ... velvet M w N", liczona `Town.GetItemCategoryPriceIndex` jak licznik linii ZC1) i liste tkalni (tygodniowa linia "Warsztaty zbytkow (P1)"). Indeks jest >= 0, a miasto niezaliczone do tanich ma indeks >= 9.99 (GoodsPaths: tanie = ix < 9.99), wiec tanich miast z tkalnia jest co najmniej `N x (1 - M / 9.99)` (zaokraglone w gore; M + 0.005 na zaokraglenie w logu). Wiersz bierze mediane tej DOLNEJ granicy z ostatnich do 28 dob; gorna granica (min(N, tanich w swiecie tej doby)) w napisie.
- **ZC1-4a (nowy, INFO):** dawna miara - czesc wszystkich miast z indeksem < 10.
- **ZC1-4o (nowy, INFO):** okolica tkalni - NIE DO ZMIERZENIA z logu: brak indeksu aksamitu wedlug miasta i brak odleglosci miedzy miastami (ks-miasta.csv ma tylko "kraina" = region kultury, np. 9 miast "river" - to nie okolica). Wiersz podaje, ile tanich i drogich miast jest POZA tkalniami (z dwoch granic), i mowi wprost, ze log nie mowi, czy te tanie leza przy tkalniach.
- Linia pomocy (`--grupa nzc1`) i docstring `nzc1_main` opisuja nowe progi.

**Wyniki (nowy checker, `--grupa nzc1`, bez `--baza-doby`):**

| bieg | ZC1-4 (miasta z tkalnia, dolna granica, mediana 28 dob) | ZC1-4a wszystkie miasta (INFO) | ZC1-4o poza tkalniami (ostatnia doba) | ZC1-16b jedwab: utarg / baza x czesc wsi | sam stosunek (INFO) | NIE razem |
|---|---|---|---|---|---|---|
| fin98 (WGRANIE 85) | **TAK** co najmniej 61% (ostatnia doba srednia 5.29 w 10 -> tanich 5-10) | 32% | 87 miast: tanich 20-25, drogich 62-67 | **TAK** 29608 / 23393 = 1.27 | 0.96 | 0 z 42 |
| fin97 (WGRANIE 84) | **NIE** co najmniej 33% (6.81 w 10 -> tanich 4-10) | 7% | 87 miast: tanich 2-8, drogich 79-85 | **TAK** 29924 / 29431 = 1.02 | 0.78 | 1 z 42 (ZC1-4) |
| fin95 | **TAK** co najmniej 55% (5.10 w 11 -> tanich 6-11) | 29% | 86 miast: tanich 21-26, drogich 60-65 | **TAK** 29951 / 27634 = 1.08 | 0.83 | 0 z 42 |

Ruda srebra ZC1-16b bez zmian (fin98 5198 / 3990 TAK; czesc wsi 1.00 - bez przeliczenia). Wierszy 40 -> 42 (ZC1-4a i ZC1-4o INFO).

**Czy wiersze nie sa slepe (logi syntetyczne z fin98 - tylko linie czytane przez `nzc1`, 9579 linii; skrypt `synth.py` w scratchpadzie sesji):**
- S0 bez zmian - wynik identyczny z pelnym logiem fin98 (diff pusty poza linia "plik:").
- S1 wszystkie tkalnie drogie (srednia 10.00), a 62% WSZYSTKICH miast tanie (stary prog dalby TAK): **ZC1-4 NIE** (co najmniej 0%), ZC1-4a INFO 62%.
- S2 6 z 10 tkalni drogie (10), 4 tanie (1) - srednia 6.40: **NIE** (co najmniej 40% = prawda 40%).
- S3 5 z 10 drogie (10), 5 bardzo tanie (0.5) - srednia 5.25: **TAK** (co najmniej 50% = prawda 50%).
- S4 5 z 10 drogie (10), 5 lekko tanie (3.0) - srednia 6.50: **NIE** (co najmniej 40%, a prawda 50%) - ostroznosc granicy, patrz Ryzyko.
- S5 utarg wozow jedwabiu x 0.6 w 28 ostatnich dobach (wsie traca rynek przy tej samej produkcji): **ZC1-16b NIE** 17764 / 23393 = 0.76 (sam stosunek 0.58); ten sam log z `--baza-doby S0` (ta sama czesc wsi - bez przeliczenia): NIE 17764 / 29608.
- S6 log bez linii "Zbytki ludu" i "Warsztaty zbytkow": ZC1-4 INFO (brak pola), bez wyjatku.

**Ryzyko / co sprawdzic (kontrola calosci, zasada 0):**
- Regresje: zmieniona tylko funkcja `nzc1_main` (ZC1-4, ZC1-16b) i `_nz1_scan` (dwa nowe typy linii w galezi `full`, zbierane tylko dla logu biegu - baza `--baza-doby` czyta jak dotad). Nazwy wierszy ZC1-4 / ZC1-16b zmienione - inne narzedzia ich nie czytaja (grep `tools/`: tylko `sprawdz_logi.py`). Inne grupy bez zmian; ZB-14 (`--grupa zbytki`, P3-P5) czyta to samo pole "cena u wytworcy" swoim kodem - nietkniety.
- **ZC1-4 to DOLNA granica:** TAK zawsze prawdziwe (nigdy nie zaliczy biegu, w ktorym tanich tkalni jest mniej niz polowa). Ale NIE moze byc falszywe, gdy tanie miasta z tkalnia sa tylko lekko tanie (S4). fin97 jest w tej strefie: prawda gdzies miedzy 40% a 100% (ostatnia doba). Granica pracuje na sredniej, bo log nie ma indeksu wedlug miasta.
- **Okolica tkalni (czesc decyzji 3c) - niezmierzona.** Do pomiaru potrzebne pole w logu gry (indeks aksamitu w miastach z tkalnia i w ich sasiadach, np. najblizsze miasta wedlug odleglosci na mapie) - to zmiana kodu gry, osobna paczka, decyzja Jeffa.
- md5: build tej galezi Armoury.dll `d5cbd8332c9f9e242ea96301c05d8708` (Rebuild ten sam). Ten sam build z kodem `6d577536` BEZ tej paczki (git stash) daje te same bajty `d5cbd833...` - paczka nie rusza DLL. Rozni sie od wgranego aefc0ee7 juz dla niezmienionego zrodla w tym katalogu (srodowisko budowania: inny katalog / biblioteki; gen_mcm nie zmienia plikow, DLL bez sciezek PDB) - nic do wgrania, wiec bez znaczenia dla gry; przy nastepnym WGRANIU budowac jak dotad.
- Ekonomia zamknieta / kolejnosc zdarzen / stan miedzy kampaniami: nie dotyczy (sam pomiar).

**Status:** DO SPRAWDZENIA (tylko checker; kod gry bez zmian - nic do wgrania). Do decyzji Jeffa: czy chce pomiaru okolicy tkalni (wymaga nowej linii w logu gry).

**Dla Jeffa (prosto):**
- **Jedwab (Twoja decyzja 2a) - zrobione.** Sprawdzam teraz utarg wozow wsi wobec tego, ile jedwabiu wsie naprawde rodza (bez tego z pol mieszczan). Wynik: fin98 1.27, fin97 1.02, fin95 1.08 - wszystko TAK. Gdyby wsie tracily rynek przy tej samej produkcji, wiersz da NIE (sprawdzone na sztucznym logu: 0.76 -> NIE).
- **Aksamit (Twoja decyzja 3c) - zrobione dla miast z tkalnia, okolicy nie da sie zmierzyc z logu.** Tkalnie aksamitu ma 10-11 miast (np. Meereen, Vaes Dothrak, Saath, Kyth, Lhazosh, Planky Town, Starfall, Vaith, Sharp Point, Volon Therys). Log podaje tylko srednia cene aksamitu w tych miastach, wiec licze, ile z nich NA PEWNO ma aksamit tani: fin98 co najmniej 61% (TAK), fin95 co najmniej 55% (TAK), fin97 co najmniej 33% (NIE - ale prawdziwy wynik moze byc wyzszy, srednia tego nie rozstrzyga). Jesli tkalnie maja drogi aksamit, wiersz da NIE (sprawdzone na sztucznym logu).
- **Okolica tkalni:** log nie mowi, ktore miasta sa obok tkalni ani ile tam kosztuje aksamit. Widac tylko, ze poza tkalniami tanich jest 20-25 miast (fin98), a reszta (62-67) drogich - czyli "dalej drogi" sie zgadza, ale czy te tanie sa przy tkalniach - nie wiadomo. Zeby to zmierzyc, trzeba dopisac jedna linie do logu gry - to osobna paczka, jesli chcesz.
