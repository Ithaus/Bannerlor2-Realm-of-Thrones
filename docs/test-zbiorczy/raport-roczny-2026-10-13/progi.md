# Raport z biegu rocznego - rozdzial PROGI, BLEDY, CZAS (klucz: progi)

Bieg: nowa kampania, 364 doby, sam swiat AI. Kod: w-toku/zbior-e8 9e2e0e6 (WGRANIE 36 + runda 3 + sluby NPC).
Praca tylko do odczytu. Nic nie uruchamialem w grze, nic nie zmienialem.

Uwaga do nazwy pliku: zadanie podalo nazwe "${l.key}.md" (niepodstawiony szablon). Zapisalem jako `progi.md` (moj klucz).

Uwaga do pytania Jeffa z czatu ("podatki trzeba gdzies zwiekszyc na czas wojny - masz propozycje?"): liczby do tej odpowiedzi sa w rozdziale 7.

## Skroty zrodel

| Skrot | Plik |
|---|---|
| R5 e5:63 | `scratchpad\test\progi-rok5\etap5.txt`, linia 63 (ten bieg) |
| R364 | `scratchpad\test\progi-rok364\` (poprzedni bieg roczny, WGRANIE 34) |
| F4 | `scratchpad\test\progi-fin4\` (test 120 dob rundy 3) |
| POPRZ e3/e7 | `raport-rok\skrypty\progi\poprz\etap3.txt`, `etap7.txt` - doliczone przeze mnie tym samym parserem z logu poprzedniego biegu (w progi-rok364 ich nie bylo) |
| X e3/e4/e7 | `raport-rok\skrypty\progi\fin4x\` - to samo dla testu 120 dob |
| LOG:n | `D:\Backup-Bannerlord\logs\2026-10-13-noc34-35\Armoury-2026-10-13_06-24-10.log`, linia n |
| LOGP:n | to samo, `Armoury-2026-10-13_02-15-21.log` (poprzedni bieg) |
| CS:n | `...\session-2026-10-13_06-24-09.log`, linia n |
| S:nazwa | wynik mojego skryptu `raport-rok\skrypty\progi\nazwa.txt` (skrypt `nazwa.py` obok) |

Kontrola narzedzia: parser uruchomiony przeze mnie na logu tego biegu dal etap3 i etap7 identyczne z progi-rok5 (diff pusty) [P].
Doba = numer z linii logu minus 108835 (tak liczy parser progow; doba 1 = dzien 108836).
[P] = pomiar z pliku. [S] = moj szacunek (wzor podany obok).

## 0. W skrocie

- Progow razem 1161. NIE: 45. DECYZJA (czeka na Jeffa): 4. Poprzedni bieg roczny: NIE 41 przy 1124 progach. [P: ostatnie linie R5 e2..e8, R364, POPRZ]
- Runda 3 naprawila 9 pozycji (NIE -> TAK). Zepsula sie formalnie 11 (TAK -> NIE), z tego prawdziwe klopoty to 2 sprawy: miasto Thenn bez pieniedzy i dluzsza kolejka odziezy wojska.
- Nowe progi rundy 3: 37 wierszy. TAK 22, NIE 2, INFO 13. P3W1-2 i P3W2-1, ktore padly w tescie 120 dob, teraz przeszly.
- Bledy: 0 linii ERROR, 0 WARN, 0 wyjatkow, 0 potkniec w logu Armoury. 23 bledy CrashScribe przy starcie sa te same co zawsze (te same 23 sygnatury w 4 sesjach dzisiejszej nocy).
- Czas: 11.98 s na dobe (poprzednio 12.98). Rosnie tylko przez pierwsze 4 miesiace (10.6 -> 12.1 s), potem stoi.
- Ksiegi: pieniadz zamkniety (+0.063% w roku), ludzie zamknieci. Nie domykaja sie: konie juczne (+56 tys. z niczego), uprzaz (-49% produkcji znika z ksiegi), zboze i ryby (+125 tys. sztuk bez wyjasnienia).

## 1. Wszystkie pozycje NIE (45) i DECYZJA (4)

Wszystkie liczby w tabelach to [P] z plikow progow (zrodlo w ostatniej kolumnie), chyba ze stoi [S].
Werdykty: REGRES (gorzej niz w poprzednim roku), ZNANE (bylo i jest), LEPIEJ (poprawa, ale prog dalej niespelniony), PROG DO ZMIANY.
"Wpis" = czy sprawa jest opisana w docs (STAN-PRAC, TRIAZ-zb58 = T1, TRIAZ-2 = T2, TRIAZ-3 = T3). [P: S:docs_id]

### 1a. Waga wysoka dla gracza (wojsko, pieniadze, miasta)

| Nr | Co mierzy | Teraz | Poprzedni rok | 120 dob | Prog | Werdykt | Zrodlo |
|---|---|---|---|---|---|---|---|
| 1 | Wojsko w druzynach lordow (krolestwa w wojnie, srednia 28 dob) | 79 409 (ostatnia doba 73 578) | 64 013 | 101 144 | podloga 85 tys. | LEPIEJ o 15.4 tys.; brakuje 5.6 tys. PROG DO ZMIANY albo nowy dochod koron - patrz rozdz. 7. Wpis: T3 O1-1, STAN-PRAC 1605 | R5 e2:12, R364 e2:12, F4 e2:11 |
| 2 | Rody z lennem bez ani jednego zolnierza | 14 z 205 | 29 (z tekstu progu; T3 linia 29) | 7 z 214 | <= 12 | LEPIEJ, brakuje 2 rodow. Nowy prog rundy 3 | R5 e8:263, F4 e8:264 |
| 3 | Zold na czlowieka: rody na pulapie wobec rodow z luzem | 6.88 wobec 5.00 zl = 1.38x | 1.51x (z tekstu progu) | 1.19x | <= 1.25x | LEPIEJ, dalej NIE. Biedne rody dalej maja drozsze wojsko | R5 e8:265, F4 e8:266 |
| 4 | Miasta z kasa ponizej 1000 zl | 38 z 363 dob; Thenn 30 dob, Frostfang's Camp 9; minimum 7 zl (Thenn, d321) | 1 doba (Saath 963 zl) | 0 | 0 kazdej doby | REGRES. Przyczyna: nie wiem. Opis nizej (1d). Wpis o Thenn: brak | R5 e5:160, R364 e5:160, F4 e5:160, S:kasy_miast |
| 5 | Towar wsi niesprzedany w miastach | 10 406 szt. = 0.181% | 4 372 = 0.077% | 1 045 = 0.054% | < 0.1% | REGRES, ta sama przyczyna co nr 4. Najgorsza doba d290: 2 739 z 5 038 szt. w 1 miescie. Nazwy miasta w linii nie ma; tego dnia tylko Thenn mial kase ponizej 2 000 zl (39 zl dobe wczesniej), wiec to Thenn [S] | R5 e5:43, e5:159, LOG:102871, kasy-miast.csv dzien 109124-109125 |
| 6 | Kasy miast przy 0; najdluzsza seria ponizej zapasu kupcow | 3 doby przy 0; seria 323 dob (Thenn) | 0 dob; 144 | 0 dob; 31 | 0 kazdej doby | REGRES, ta sama przyczyna co nr 4 | R5 e5:44 |
| 7 | Dlug koron w Banku (186) nie rosnie w II polroczu | wzrosl u 16 z 26 krolestw | u 19 z 26 | - | 0 | LEPIEJ o 3, dalej NIE. Wpis: brak. Wiaze sie z rozdz. 7 | R5 e5:175, R364 e5:175 |
| 8 | Zloto swiata - zmiana w ostatnich 28 dobach | +0.111% (+230 296 zl) | -0.201% | -0.029% | < 0.1% | LEPIEJ: w calym roku +0.063% wobec -1.14% (rozdz. 6). Przekroczenie o 0.011 punktu | R5 e5:161, S:pieniadz |
| 9 | Lud zjadl bez zaplaty | 2.16% wydatku | 3.62% | 0.53% | < 1% | LEPIEJ wobec roku. Wpis: brak | R5 e5:101 |
| 10 | DECYZJA: miasta, gdzie lud nie stac na jedzenie | srednio 9.6, najwiecej 14 | 15.8, 19 | 4.0, 8 | 0 (projekt z gory zaklada NIE) | LEPIEJ. Czeka na Jeffa | R5 e5:102 |
| 11 | DECYZJA: pelny koszyk ludu | swiat 98.2%; 14.1% miast ponizej 95% | 96.9%; 21.4% | 99.1%; 6.0% | 98% i 5% | LEPIEJ. Czeka na Jeffa | R5 e5:103 |
| 12 | Partie z zaleglym zoldem | 2.49% | 3.17% | 0.49% | <= 2% | LEPIEJ. Wpis: T1, T2, T3 | R5 e2:11 |
| 13 | Udzial panow samych zamkow w stalym dochodzie | 36.6% | 32.7% | 38.4% (INFO) | 30-35% | PROG DO ZMIANY. Runda 3 oddala rente biednym panom zamkow, wiec udzial wyszedl ponad pasmo pisane przed nia [S]. Prog glowny (>= 30%) spelniony: 34.6% | R5 e5:192, e5:191 |
| 14 | Sakiewki zolnierzy w dniach zoldu | 26.1 dnia | 24.7 | 24.2 | <= 15 | ZNANE (T2: bylo wczesniej), o 1.4 dnia gorzej. Lezy tam 16.59 mln zl | R5 e5:63, S:pieniadz |
| 15 | Kasy miast ponizej zapasu przez nasze sciezki | 2 zl w roku (1 zl d205, 1 zl d269) | 0 | 0 | 0 | REGRES tylko formalny: 2 zlote przy obrocie 394 514 i 537 861 zl tych dob. Zaokraglenie | R5 e8:98, LOG:72756, LOG:95510 |

### 1b. Waga srednia (jazda, konie, towary, bandy)

| Nr | Co mierzy | Teraz | Poprzedni rok | 120 dob | Prog | Werdykt | Zrodlo |
|---|---|---|---|---|---|---|---|
| 16 | Jazda t3+ wobec celu | 63% (17.2% przy celu 27.4%) | 64% | 64% (d91-120) | >= 90% | ZNANE (T2: czeka na siodla) | R5 e5:362 |
| 17 | Jazda wsrod wszystkich ludzi partii AI | 9.9% | 10.3% | - | >= 12% | ZNANE | R5 e5:363 |
| 18 | Wykonanie celu jazdy nie spada | 69% -> 63% | 70% -> 64% | 70% -> 64% | spadek najwyzej 2 punkty | ZNANE | R5 e5:364 |
| 19 | Dothrakowie czekajacy na konia | 27.8% (1 369 z 4 919) | 34.1% | 25.1% | <= 10% | LEPIEJ wobec roku; ZNANE (T1, T2) | R5 e5:366 |
| 20 | Jazda u Dothrakow | 43.9% | 43.9% | - | >= 60% | ZNANE, bez zmian | R5 e5:368 |
| 21 | Kolejka czekajacych na konia rosnie | +40/d (4 476 -> 5 878) | +32/d | +54/d | <= +30/d | ZNANE (T2 K3-8); gorzej, bo wojska jest wiecej [S] | R5 e5:371 |
| 22 | Zakupy koni lordow | 56.7/d | 51.5/d | - | >= 58.4/d | LEPIEJ, brakuje 1.7/d | R5 e5:377 |
| 23 | Kon "za drogi" dla lorda | 9.7% wizyt | 9.7% | 12.3% | <= 5% | ZNANE (T2) | R5 e5:378 |
| 24 | Ksiega koni - reszta bez wyjasnienia | 47 z 47 tygodni ponad prog | 47 z 47 | 12 z 12 | 0 | ZNANE (STAN-PRAC, T1, T2). W roku +54 358 koni, patrz rozdz. 6 | R5 e5:257, S:ksiegi |
| 25 | Konie na targu d180 -> d364 | 13 255 -> 16 683 (+26%) | 13 336 -> 18 668 (+40%) | - | +-10% | LEPIEJ, dalej rosnie. Wpis: brak | R5 e5:359 |
| 26 | Ksiega uprzezy - reszta | -7 395 szt. przy 14 995 wyprodukowanych | -8 361 przy 13 849 | -1 746 przy 5 781 | do 10% | ZNANE (T2 H-4b: ksiega nie widzi zapisu notabla; traktowac jak INFO) | R5 e8:181 |
| 27 | Dymarki bez wegla | 5.7% lup | 5.2% | 7.5% | <= 5% | ZNANE (T1, T3 O2-6) | R5 e5:295 |
| 28 | Stos drewna w miastach | 6 389 -> 135 398 (+426/d) | +373/d | +15/d | <= 290/d | ZNANE (T1), gorzej. Drewno rosnie bez konca | R5 e5:299 |
| 29 | Zatkane magazyny wsi zamkowych | 7.95% | 10.71% | 3.97% | <= 5.6% | LEPIEJ wobec roku | R5 e5:216 |
| 30 | Port najdluzej bez nadwyzki | 62 doby (Saath) | 191 dob | 20 dob | <= 28 | LEPIEJ; ZNANE (T2) | R5 e5:203 |
| 31 | Kolejka odziezy wojska d90 -> d364 | 9 910 -> 10 912 | 10 379 -> 9 299 | 9 872 -> 11 380 | bez wzrostu | REGRES wobec roku. Podejrzana zmiana: wiecej wojska po rundzie 3 (79 wobec 64 tys.) = wiekszy popyt na odziez [S]. ZNANE jako zjawisko (T1, T2) | R5 e5:340 |
| 32 | Skora w kolejce odziezy | 2 077 -> 2 852 | 2 084 -> 2 092 | 2 081 -> 2 380 | bez wzrostu | REGRES, ta sama przyczyna co nr 31 | R5 e5:387 |
| 33 | Kuznie czekaja na material | 2 343 szt./d | 3 143 | 1 288 | <= 1000 | LEPIEJ wobec roku; ZNANE | R5 e5:383 |
| 34 | Kuznie czekaja na skore | 2 231 szt./d | 2 548 | 1 076 | <= 500 | LEPIEJ wobec roku; ZNANE | R5 e5:384 |
| 35 | DECYZJA: polki uprzezy rosna | 1 496 -> 2 558 (+71%) | +51% | +18.6% | bez wzrostu | ZNANE, czeka na decyzje | R5 e5:323 |
| 36 | Pula wyrzutkow | 11 869 w d364 | 9 788 | 5 314 w d120 | <= 3 053 i <= 1.25 x d90 | PROG DO ZMIANY. Pula staje na ok. 11.9 tys. (d335: 11 936, d364: 11 869). Prog zakladal plateau juz po 90 dobach. ZNANE (T1) | R5 e4:81, LOG "Ludzie (ksiega)" dni 109170 i 109199 |
| 37 | Bandy w d364 | 819 band, 17 279 ludzi | 937, 20 501 | - | <= 867, <= 15 044 | LEPIEJ. Liczba band w progu, ludzi 15% za duzo | R5 e4:84 |
| 38 | Kiesa na karawane wobec wzoru | 5 363 zl (+26.6%), karawan 556 | +23.1%, 439 | +43.8%, 624 | +-20% w dobie 120 | PROG DO ZMIANY: wzor Z33 mial 962 karawany, dzis limit 3 na kupca | R5 e8:139 |
| 39 | Sprzedaz nadwyzek sprzetu zalog (doby 1-120) | sztuki x0.78, utarg x0.63 wzoru | x1.02, x1.01 | x0.66, x0.48 | +-15% wzoru Z33 | PROG DO ZMIANY (sam prog pisze, ze wzor jest sprzed etapow 5-8). Podejrzana zmiana rundy 3: mniej zwolnien i wieksze zalogi (83 593 wobec 74 122 ludzi na koniec roku [P: S:wojsko_trend]) = mniej zbednego sprzetu [S] | R5 e8:239 |
| 40 | Zatkana wies bez wozu ponad 7 dob | ciag 3 doby (d298) | ciag 2 | ciag 2 | ciag <= 2 | PROG DO ZMIANY (T2: prog do poprawy). Dob z problemem mniej: 34 wobec 42 | R5 e8:104 |
| 41 | Przebudowy pamieci polki | 9 093/d | 8 987/d | 8 028/d | <= 9 000/d | PROG DO ZMIANY (szum: +1% ponad prog). Rosnie z liczba wojska [S] | R5 e8:222 |
| 42 | Partia zawracana ponad 3 razy na dobe | 1 raz (d8) | 4 razy | 1 raz (ta sama partia, d8) | 0 | LEPIEJ wobec roku. Drobne. Wpis: brak | R5 e8:124 |
| 43 | DECYZJA: cena bardy t6 | 2 760 zl | 2 760 | 2 760 | <= 2 600 | Bez zmian, czeka na Jeffa | R5 e8:80 |

### 1c. Waga niska (pojedyncze doby, pomiary)

| Nr | Co mierzy | Teraz | Poprzedni rok | 120 dob | Prog | Werdykt | Zrodlo |
|---|---|---|---|---|---|---|---|
| 44 | Ksiega Nocnej Strazy - kontrola | 56 ludzi w 1 dobie (d175) na stan 5 942 | 0 | 0 | <= 1 kazdej doby | REGRES formalny. Jedyna doba roku, w ktorej partie zmienialy rodzaj w obie strony naraz (+101 / -279). W poprzednim biegu taka doba (d363, +127 / -127) dala 0. Zwiazek z runda 3: nie wiem | R5 e4:264, LOG:61995, S:straz_k8 |
| 45 | Ksiega wiosek - trafieni przez spustoszenie | 6.09 czlowieka w 1 dobie (d229) przy 102.54 trafionych; w roku trafionych 230 521 | 0 | 0 | 0.1% doby | REGRES formalny, 6 ludzi w roku | R5 e4:243, LOG:81057 |
| 46 | Probki pieniadza wokol okien | 2 doby po +45 zl (nowe karawany) | 3 doby | 2 doby | 0 | ZNANE (T2). 90 zl w roku | R5 e3:6, POPRZ e3:6, X e3:6, LOG:41124, LOG:47509 |
| 47 | Unikat znika z targu | 1 (suknia Cersei, d3) | 4 | 1 (ten sam) | 0 | LEPIEJ; ZNANE (T2) | R5 e5:329 |
| 48 | Lordowie ruszaja noca bez powodu | 2.3 na noc | 2.3 | 2.4 | <= 2.0 | ZNANE (T1), bez zmian | R5 e7:17, POPRZ e7:17, X e7:17 |
| 49 | Spiacy lordowie AI wstaja przed switem | 4 noce z 333 (5 lordow) | 7 nocy (7 lordow) | 0 z 89 | 0 | LEPIEJ wobec roku | R5 e7:18, POPRZ e7:18, X e7:18 |

Podsumowanie werdyktow (49 wierszy): REGRES 8 (5 przyczyn), PROG DO ZMIANY 7, LEPIEJ 19, ZNANE bez poprawy 15. [P: zliczenie tabel]

### 1d. Regres nr 4-6: miasto Thenn

- Thenn (Wolni Ludzie, za Murem). Kasa miasta: 178 228 zl (d2) -> 484 (d139) -> 7 (d321) -> 5 278 (d364). [P: kasy-miast.csv, kolumna kasa; S:thenn2]
- Ponizej zapasu kupcow przez 331 dob, seria 323 dob. W poprzednim biegu 105 dob, seria 7. [P: kolumna dni_ponizej_zapasu; S:kasy_miast]
- Lud Thenn: stac go na 40% jedzenia w d182 i 69% w d364. W poprzednim biegu 99-100%. [P: kiesa-ludu.csv, kolumna stac_jedzenie]
- Karawana kupca stoi w Thenn bez statkow od 289 dob. [P: LOG:129081, linia "Wyspy i drogi"]
- Sasiad, Frostfang's Camp: kasa 2 962 zl, a lud ma w kiesach 268 292 + 27 143 + 9 067 zl. [P: kiesa-ludu.csv d364]
- Co to znaczy: w jednym miescie pieniadz przestal krazyc. Lud biedny, kasa pusta, wies nie sprzedaje, karawana stoi.
- Przyczyna: nie wiem. W danych nie widze dowodu, ze to runda 3. Rozjazd z poprzednim biegiem zaczyna sie przed d57 (kasa 39 395 wobec 69 205 srednio w d57-84). [P: S:thenn]

## 2. Co runda 3 naprawila (NIE w poprzednim roku, TAK teraz)

| Co | Poprzedni rok | Teraz | Prog | Zrodlo |
|---|---|---|---|---|
| Glowy rodow z kiesa ponizej 5 000 (doba 364) | 21 | 5 | <= 10 | R364 e2:3, R5 e2:3 |
| Glowy rodow ponizej 5 000 (najwiecej w roku) | 29 | 8 | <= 10 | R364 e5:51, R5 e5:51 |
| Wojsko lordow srednio d90-120 | 93 392 | 98 796 | 95-115 tys. | R364 e4:157, R5 e4:157 |
| Podzamcze stac na jedzenie | 90.3% | 95.5% | >= 95% | R364 e5:104, R5 e5:104 |
| Miasta bez zelaza (srednio d91-364) | 41.4 | 11.7 | <= 30 | R364 e5:297, R5 e5:297 |
| Miasta bez rudy w d91-120 | 39.8 | 8.9 | <= 32.2 | R364 e8:141, R5 e8:141 |
| Karawany stojace 7+ dob w tym samym miescie | 13 | 7 | <= 10 | R364 e8:131, R5 e8:131 |
| Petla "sprzedal uprzaz, kupil siodlo" | 1.15% siodel | 0.34% | <= 1% | R364 e8:185, R5 e8:185 |
| Przejscia rzeki poza mostem | 1 | 0 | 0 | POPRZ e7:68, R5 e7:68 |

Wobec testu 120 dob: "wplyw pana samych zamkow z zaworu" przeszedl z DECYZJA (180 zl/d) na TAK (252 zl/d). [P: F4 e2:34, R5 e2:35]

Odwrotny kierunek (TAK w poprzednim roku, NIE teraz) - 11 wierszy: nr 5, 6, 13, 15, 31, 32, 39, 40, 41, 44, 45 z rozdzialu 1. [P: S:por_progi]

## 3. Nowe progi rundy 3

### 3a. Wojsko (blok etap8_p3woj) - TAK 12, NIE 2, INFO 6

| ID | Co | Rok (teraz) | 120 dob | Prog | Wynik | Zrodlo R5 |
|---|---|---|---|---|---|---|
| P3W1-0 | Oba klucze renty wlaczone | 364 z 364 dob | 120 z 120 | kazda doba | TAK | e8:247 |
| P3W1-1 | Rody z lennem bez renty korony | 5.0% (najwiecej 6.8%) | 7.2% | <= 10% (bylo 27%) | TAK | e8:248 |
| P3W1-2 | Bez renty wedlug przyczyny: sluzba / zaloga / oba | 6 / 6 / 0 | 17 / 5 / 0 | sluzba <= 8, oba <= 2 (bylo 47 / 3 / 7) | TAK (w 120 dobach NIE) | e8:249 |
| P3W1-3 | Udzialy rodow bez renty oddawane innym | 5 523 zl/d | 13 723 | <= 20 000 (bylo 62 558) | TAK | e8:250 |
| P3W1-4 | Sluzba "na miare srodkow": rodow w wojnie / sluzylo i tak / doba zaliczona / wymog zdjety | 160 / 56 / 27 / 0 | 106 / 35 / 11 / 3 | - | INFO | e8:251 |
| P3W1-4b | Rody na miare srodkow: rodow / z renta / renty zl na dobe / ludzi | 75 / 66 / 53 704 / 11 319 | 38 / 35 / 26 706 / 10 903 | - | INFO | e8:252 |
| P3W1-5 | Krolestwa w wojnie / armia w polu / oblezenie / napad na wlasna osade / tylko rajdy | 26.0 / 8.7 / 5.7 / 6.1 / 5.1 | 25.8 / 7.0 / 4.9 / 5.2 / 3.7 | - | INFO | e8:253 |
| P3W2-1 | Zwolnieni wedlug tieru t1-2 / t3-4 / t5+; zold zwalnianego | 4 539 / 3 880 / 954; 5.52 zl | 8 821 / 4 447 / 814; 4.47 zl | >= 5.0 zl (bylo 3.65) | TAK (w 120 dobach NIE) | e8:254 |
| P3W2-2 | Zwalniani przez budzet na dobe | 332 | 380 | (bylo 520) | INFO | e8:255 |
| P3W2-3 | Rody, ktorym zalogi zajely caly pulap / z nich bez ludzi | 47 / 42 | 26 / 25 | - | INFO | e8:256 |
| P3W3-1 | Zadatek BK dla kompanii najemnych wstrzymany | 767 najmow, 28 773 126 zl zostalo u krolow | 250, 4 331 795 | latka wpieta | TAK | e8:257 |
| P3W3-2 | Kontrakty najemnikow: nalezne za ludzi wobec umowy | 5 399 289 wobec 8 576 599 (63%) | 63% | - | INFO | e8:258 |
| P3W3-3 | Ludzie najemnikow AI | 2 425 | 2 329 | >= 2 000 | TAK | e8:259 |
| P3W4-1 | Dlug wezwan: nowy / splacony w nicosc | 0 / 0 | 0 / 0 | 0 / 0 (bylo 3.56 mln / 2.01 mln) | TAK | e8:260 |
| P3W4-2 | Reszta cen zerwanych wezwan oddana | 5 396 992 wobec 5 396 992 | rowne | rowne | TAK | e8:261 |
| P3W-5 | Ludzie w partiach rodow AI (srednia 28 dob) | 79 001 | 102 044 (INFO) | >= 70 000 (bylo 63 129) | TAK | e8:262 |
| P3W1-6 | Rody z lennem z 0 ludzi | 14 z 205 | 7 z 214 | <= 12 (bylo 29) | NIE | e8:263 |
| P3W1-7 | Polnoc - ludzie w partiach | 8 272 | 11 301 (INFO) | >= 7 500 (bylo 5 118) | TAK | e8:264 |
| P3W2-4 | Zold na czlowieka: rody na pulapie wobec rodow z luzem | 1.38x | 1.19x (INFO) | <= 1.25x (bylo 1.51x) | NIE | e8:265 |
| P3W3-4 | Kiesy 11 kompanii najemnych | 2 852 237 -> 5 649 484 (+7 706 zl/d) | +7 714 zl/d | <= +10 000 zl/d (bylo +48.7 tys.) | TAK | e8:266 |

### 3b. Zelazo (blok etap8_p3_zelazo) - TAK 10, NIE 0, INFO 7

| ID | Co | Rok (teraz) | 120 dob | Prog | Wynik | Zrodlo R5 |
|---|---|---|---|---|---|---|
| P3/Z1-1 | Zamowienie wozem czynne | 363 z 363 dob | 119 | kazda doba | TAK | e8:267 |
| P3/Z1-2 | Wozy kupcow | 7 228 kontraktow (19.9/d), 42 014 lup rudy, 1 938 342 zl; dojechalo 7 086 | 2 213 | - | INFO | e8:268 |
| P3/Z1-3 | Kontrola wozow | 0 dob z reszta | 0 | 0 | TAK | e8:269 |
| P3/Z1-4 | Potkniecia i czas wozow | 0; 1.7 ms/d, najwiecej 5.9 ms | 0; 1.4 ms/d | 0; <= 30 ms/d | TAK | e8:270 |
| P3/Z1-5 | Powody braku kontraktu | brak zrodla 3 786, bez drogi 628, kasa miasta przy zapasie 604, bez zysku 452 | 3 653 / 507 / 80 / 141 | - | INFO | e8:271 |
| P3/Z1-6 | Miasta bez zelaza | od d91 srednio 9.1; ostatnie 30 dob 4.2 | 21.4 | <= 30 (bylo 40.1 i 51-61) | TAK | e8:272 |
| P3/P2a | Zelazo wedlug ladow | Westeros: 180 lup/d, zuzycie 145/d; Essos: 77/d, zuzycie 99/d | - | - | INFO | e8:273 |
| P3/P2b,d | Zamki z ruda; porty bez rudy | 7 zamkow, 22 lupy; 5 portow | 2 zamki; 7 portow | - | INFO | e8:274 |
| P3/Z2-1 | Regulator nie tnie zelaza, gdy go brakuje | 11 decyzji, 0 zlych | 3, 0 | 0 zlych | TAK | e8:275 |
| P3/Z2-2 | Ciecia wstrzymane / powroty | 0 / 0 | 0 / 0 | - | INFO | e8:276 |
| P3/Z2-3 | Zelazo poza zasiegiem zuzycia | 31% z 8 857 lup | 27% | (bylo 79% z 14 517) | INFO | e8:277 |
| P3/Z3-1 | Przyrost zelaza w zamkach | 298 -> 22 lup (-0.8/d) | -2.5/d | <= +2/d (bylo +9.2/d) | TAK | e8:278 |
| P3/Z3-A1 | Woz nie zostawia zelaza w zamku | wwiezione 7 561 lup, sprzedane zamkom 0 | 2 640, 0 | <= 10% | TAK | e8:279 |
| P3/Z3-A2 | Straz wozu wygasla z ladunkiem | 30 | 24 | - | INFO | e8:280 |
| P3/Z3-B1 | Dobytek rozbitych: zelazo do miast | zamki 0, miasta 2 311 | 0, 419 | zamki <= 10% | TAK | e8:281 |
| P3/Z4-1 | Towar kasowany lordom przez gre trafia na polke | 255 396 szt. na polki miast, bez polki 0 | - | bez polki 0 | TAK | e8:282 |
| P3/Z4-2 | Zelazo bez wyjasnienia w ksiedze | -38 lup w roku | +0 | >= -150 (bylo -1 474) | TAK | e8:283 |

## 4. Bledy

| Co | Ten bieg | Poprzedni rok | Nowe? | Zrodlo |
|---|---|---|---|---|
| Linie ERROR w logu Armoury | 0 z 129 290 linii | 0 z 129 051 | nie | [P] grep, S:potkniecia |
| Linie WARN, Exception | 0, 0 | 0, 0 | nie | [P] grep |
| Potkniecia (35 582 linie ze slowem "potkni"; czytana pierwsza liczba po slowie) | 0 niezerowych | 0 (35 163 linie) | nie | [P] S:potkniecia |
| ERROR / Exception w 11 logach tematycznych (bitwy, finanse, budowy...) | 0 | 0 | nie | [P] grep w `Logs\2026-10-13_06-24-10\*.log` |
| Bloki bledow CrashScribe | 23 | 23 | nie - te same 23 sygnatury | [P] S:bledy_cs |
| Z tego przed wejsciem na mape / po | 22 / 1 | 22 / 1 | nie | [P] S:bledy_cs |
| Bloki "ERROR IN A MOD" (nasze mody) | 0 | 0 | nie | [P] R5 e5:9, S:bledy_cs |
| Wyjatki pierwszej szansy w czasie kampanii | 2 432 (6.7 na dobe), zgloszonych 0 | 4 271 (11.8 na dobe), zgloszonych 0 | mniej | [P] S:cs_profil |
| Okna zamkniete przez autotest | 9 | 13 | - | [P] CS:9255 |

Sklad 23 blokow startowych [P: S:bledy_cs]:
- 14 blokow: BannerKings przy zakladaniu swoich latek (Main.OnGameStart).
- 6 blokow: BKROTPatch (OnSubModuleLoad).
- 1 blok: TournamentsXPanded (ladowanie modulu).
- 1 blok: UIExtenderEx, "Ambiguous match found", jedyny po wejsciu na mape (CS:1529).
- 1 blok: "Cannot bind to the target method", stos przechodzi przez Armoury.WesterosClimate.ApplyAll (CS:322). To wyjatek zlapany w srodku Harmony. Log Armoury pisze po nim "WesterosClimate: data wpieta, niebo metod 6, gospodarka metod 19" (LOG:66). Jest we wszystkich 4 sesjach.
- Porownanie z 3 innymi sesjami tej nocy (poprzedni rok, 120 dob, 40 dob cienia swatow): 0 roznic w sygnaturach.

Zawieszenie i wywrotka przy wyjsciu:
- `hang-2026-10-13_07-39-37.log`: watek glowny milczal 61 s, stan "outside a campaign - main menu or loading". [P]
- Kolejnosc: 07:38:26 koniec OK, 07:38:31 zapis OK, 07:38:35 QuitGame (CS:9255-9259), 07:38:37 wywrotka silnika 0xC0000005 pod 0x7FF92B84E20A (`C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_errors_34624.txt`), 07:39:37 plik hang. [P]
- Stos silnika jest identyczny (te same 11 pozycji) jak w wywrotce przy wyjsciu z biegu 05:11 (`rgl_log_errors_1396.txt`). [P]
- Poprzedni bieg roczny tez wywrocil sie przy wyjsciu (adres 0x7FF92B53E0C3, plik hang 03:36:48). Pliku silnika z tamtego biegu juz nie ma, stosu nie porownam. [P: S:at-w34-rok364.utf8, linia 160]
- Test 120 dob wyszedl sam, bez wywrotki. Czyli blad jest losowy. [P: S:at-fin4-120.utf8, linia 136]
- Werdykt: to znany blad zamykania (STAN-PRAC linie 24, 383, 1385). Po zapisie, nie wplywa na wynik. Zapis `autotest-2026-10-13-0738.sav` powstal przed nim.

## 5. Czas

### 5a. Sekundy na dobe

| Okno dob | Ten bieg, s/dobe | Poprzedni rok, s/dobe | Nasze mody, ms/dobe (ten bieg) | Pamiec, MB (srednio / najwiecej) |
|---|---|---|---|---|
| 1-28 | 10.63 | 10.64 | 1 045 | 1 218 / 1 319 |
| 85-112 | 12.01 | 13.03 | 1 378 | 1 357 / 1 491 |
| 197-224 | 12.39 | 14.80 | 2 310 (jedna doba z bledem stopera, nizej) | 1 448 / 1 582 |
| 337-364 | 12.40 | 12.63 | 1 373 | 1 517 / 1 642 |
| caly rok | 11.98 (mediana 11.76) | 12.98 (mediana 12.85) | 1 468 (mediana 1 358) | - |

[P: S:czas - pole "doba N ms (zegar)" linii "Koszt 171-174 (doba)" i linia "Profil doby (E8/0)", 363 doby; LOG:853 do LOG:128960]

- Czy rosnie z wiekiem kampanii: tylko na poczatku. Doby 2-120: +1.49 s na 100 dob. Doby 120-364: +0.01 s na 100 dob. [P: S:czas2, trend liniowy]
- Powod wzrostu na poczatku: swiat sie zapelnia (wojsko 55 -> 107 tys. ludzi w partiach w 84 doby). [P: S:wojsko_trend] Zwiazek przyczynowy: [S].
- Pamiec rosnie caly rok: 1 218 -> 1 517 MB (+25%). W poprzednim biegu tak samo (1 213 -> 1 521). Nie wiem, czy stanie w drugim roku.
- Szesc najwolniejszych dob (16-20 s) to doby 61, 121, 181, 241, 301, 361. W kazdej autotest robil zapis kontrolny (co 60 dob biegu, 4.9-5.1 s). To nie gra zwalnia. [P: S:czas linia 72; CS:3285, 4443, 5572, 6711, 7918, 9160-9161]

### 5b. Co kosztuje najwiecej (nasze mody, srednio na dobe)

| Skladnik | Ten bieg, ms | Poprzedni rok, ms | Roznica |
|---|---|---|---|
| Takt doby (162 wywolania) | 358.6 | 348.6 | +10.0 |
| Godzina (24 takty) | 272.7 | 223.1 | +49.5 |
| Wejscia do osad | 255.4 (mediana 114) | 202.6 (mediana 99) | +52.8 |
| Doba partii (glownie AiGear) | 199.8 | 196.6 | +3.3 |
| Gra wola nasze (warsztaty, ochotnicy, musztra, ceny) | 320.3 | 293.2 | +27.1 |
| Razem | 1 467.5 = 12.2% doby | 1 325.1 = 10.2% doby | +142.4 |

[P: S:czas]

Najdrozsze pozycje taktu doby [P: S:czas]: ClanIncomeBook 49.5 ms, SupplyDemand.DailyTrade 44.2, KingdomTreasury 31.4, Sutlers 25.6, WarLedger 19.0, ScorchedEarth 18.1.
Najdrozsza pozycja godziny: "Kwatermistrz + projekty + MaterialOrders" 219.8 ms (poprzednio 171.1).
Najdrozsza pozycja w ogole: musztra w treningu gry 126.9 ms (poprzednio 114.3).

Uwaga o pomiarze: w dobie 215 stoper wejsc do osad pokazal 25 671 ms, a cala doba trwala 14.6 s. To niemozliwe, wiec stoper policzyl cos kilka razy. Bez tej doby "nasze razem" to 1 397 ms. Ale jedno zdarzenie tej doby trwalo naprawde 1 594 ms (w poprzednim biegu najdluzsze 342 ms). [P: LOG:76078, LOG:76079, S:wejscia, S:czas2]

### 5c. Koszt nowych rzeczy rundy 3 i slubow

| Rzecz | Koszt | Zrodlo |
|---|---|---|
| Wozy surowcow (zamowienie zelaza) | 1.7 ms/d, najwiecej 5.9 ms | [P] R5 e8:270 |
| Budzet rodow (ClanBudget.Daily) | 12.2 ms wobec 11.5 (+0.7) | [P] S:czas |
| Caly takt doby (renta, zwolnienia, najemnicy, regulator) | +10.0 ms razem | [P] S:czas |
| Godzina (pozycja zbiorcza z zamowieniami surowca) | +49.5 ms. Sam MaterialOrders.Hourly to 0.3 ms w probce (LOG:53283), wiec reszta to kwatermistrz. Dlaczego wzrosl: nie wiem; wojska jest o 24% wiecej [S] | [P] S:czas |
| Swaty i sluby NPC (latka CrashScribe na kod ROT) | srednio 609 ms/d; rosnie 464 -> 697 ms miedzy pierwszym a ostatnim kwartalem (lordow szukajacych 1 192 -> 1 371) | [P] S:czas, CS:1576, CS:9237 |
| Sluby w roku | 215 | [P] CS:9237 |

- Swaty przed latka: STAN-PRAC podaje ok. 1.2 s/dobe (linia 1539). W logu poprzedniego biegu nie ma tej linii, wiec sam tego nie zmierzylem.
- Caly bieg jest o 1.00 s na dobe szybszy niz poprzedni (11.98 wobec 12.98), mimo ze nasze mody kosztuja o 0.14 s wiecej. Ile z tego to swaty, a ile inny los swiata: nie wiem.
- Koszt swatow rosnie z liczba lordow. To jedyna pozycja, ktora w tym biegu wyraznie rosnie do konca roku (+50% w rok).

## 6. Spojnosc ksiag

| Ksiega | Kontrola | Wynik | Kiedy i ile | Zrodlo |
|---|---|---|---|---|
| Pieniadz - suma swiata | d1 -> d364 | 208 204 213 -> 208 335 095 zl = +130 882 (+0.063%) | poprzedni rok -2 373 117 (-1.14%) | [P] LOG:707, LOG:129205, S:pieniadz |
| Pieniadz - reszta bez nazwy (R) | srednia 28 dob | 914 zl/d (prog 10 000) | suma roku -135 106; najwieksza doba 39 593 zl (d309, LOG:109657); 10 dob ponad 10 000 | [P] R5 e3:3, S:pieniadz |
| Pieniadz - bez platnika, bez odbiorcy | suma roku | 0 / 0 / 0 / 0 | - | [P] R5 e3:16-20 |
| Kiesy ludu | zmiana = wplywy - wydatki | 0 dob poza progiem 1 zl z 363 | - | [P] R5 e5:98 |
| "Zakupy" z niczego (kasy miast i zamkow) | kazda doba | 0 z 363 dob | - | [P] R5 e5:100 |
| Bitwy z niczego | kazda doba | najgorsza doba -16 zl, suma roku -67 zl | - | [P] R5 e5:13 |
| Ludzie - ognie domowe (hearth) | reszta | 0.000 w 100% dob | - | [P] R5 e4:14-15 |
| Ludzie - rownanie krain | najgorsza kraina | 115 ludzi = 0.115% (Sarnor), 0 dob poza progiem | - | [P] R5 e4:147 |
| Ludzie pod bronia z jencami | srednia reszta | +38.2 ludzi/d (prog 100) | - | [P] R5 e4:30 |
| Ludzie - w nicosc bez nazwy | srednia 28 dob | 35/d przy ruchu 9 699/d (0.36%) | - | [P] R5 e4:148 |
| Ludzie - z niczego | srednia 28 dob | 45/d, z tego karawany 25 | caly rok | [P] R5 e4:149 |
| Nocna Straz | kontrola | 56 ludzi | 1 doba: d175 | [P] R5 e4:264 |
| Ksiega wiosek | trafieni | 6.09 czlowieka | 1 doba: d229 | [P] R5 e4:243 |
| Wozy surowcow, wozy dymarek, rachunek za drewno | reszta | 0 dob z reszta | - | [P] R5 e8:269, e5:307, e5:311 |
| KONIE | reszta bez wyjasnienia | +54 358 koni w roku (+150/d) = 19.4% obrotu | caly rok, kazdy tydzien. Prawie wszystko to juczne: +56 478. Poprzedni rok +52 991 | [P] S:ksiegi, R5 e5:257 |
| Konie - stan swiata | d2 -> d365 | 21 839 -> 66 616 koni (juczne 7 342 -> 35 108) | caly rok | [P] S:ksiegi, linie "Konie (E6/0)" |
| UPRZAZ | reszta | -7 420 szt. = 49% z 15 026 wyprodukowanych | caly rok; poprzedni rok -8 383 (60%) | [P] S:ksiegi, R5 e8:181 |
| TOWARY razem (48 towarow) | bez wyjasnienia | +19 397 szt. przy produkcji 9 042 624 (0.215%) | poprzedni rok -296 183 (-3.3%) | [P] S:ksiegi |
| Towary - zboze | bez wyjasnienia | +65 002 szt. (1.9% produkcji) | rowno caly rok: 17 471 / 14 980 / 17 688 / 14 863 na kwartal | [P] S:towary_zboze |
| Towary - ryby | bez wyjasnienia | +59 623 szt. (6.1% produkcji) | rowno caly rok: 13 981 / 13 551 / 15 812 / 16 279 | [P] S:towary_zboze |
| Towary - 12 rodzajow jedzenia | bez wyjasnienia | po ok. -6 700 do -7 200 szt. kazdy | rowno caly rok, ok. 245 dob z 363 | [P] S:ksiegi |
| Zelazo | bez wyjasnienia | -38 lup | - | [P] R5 e8:283 |

Uwagi:
- Zboze i ryby "z niczego" nie koreluja z towarem porzucanym przez lordow (korelacja dzienna 0.01 i 0.02). W poprzednim biegu ten doplyw byl zakryty przez kasowanie towaru lordom, ktore runda 3 nazwala i zamknela. Zrodla doplywu nie znam. [P: S:towary_zboze; wniosek o zakryciu to S]
- Konie juczne: ksiega sama wymienia niepodpiete zrodla (LegendaryLaw, stadniny BK, zbrojownie rozwiazanych, zakupy gry - LOG:93703). STAN-PRAC linia 1566 pisze o naplywie jucznych z taborow wsi 105-156/d. Sprawa znana, paczki nie ma.
- Uprzaz: wedlug TRIAZ-2 (H-4b) to nie przeciek, tylko zapis notabla, ktorego ksiega nie widzi. Pewnosci nie ma - brak licznika.

## 7. Liczby do pytania Jeffa: skad wziac pieniadze na wojsko w czasie wojny

To nie moja dziedzina w raporcie, ale pytanie padlo w czacie, a liczby wyszly przy okazji.

### 7a. Gdzie pieniadz jest i dokad plynie (ten bieg, caly rok)

| Kto trzyma zloto | d1, mln | d364, mln | Zmiana w roku, mln |
|---|---|---|---|
| Skarbce krolestw | 57.93 | 27.11 | -30.82 |
| Kasy miast | 27.33 | 10.98 | -16.36 |
| Notable | 28.82 | 21.85 | -6.98 |
| Glowy rodow | 52.25 | 47.23 | -5.02 |
| Kiesy ludu miast | 6.28 | 44.12 | +37.84 |
| Sakiewki zolnierzy | 0.34 | 16.59 | +16.25 |
| Kiesy wsi | 1.11 | 9.55 | +8.44 |
| Kiesy ludu podzamczy | 0.67 | 3.33 | +2.65 |
| Razem swiat | 208.20 | 208.34 | +0.13 |

[P: LOG:707 i LOG:129205, linia "Pieniadz swiata"; S:pieniadz]

- Zloto nie znika. Przeszlo ze skarbcow koron i kas miast do kies mieszczan i sakiewek zolnierzy.
- Sakiewki zolnierzy i kiesy wsi stoja od doby 120 (14.92 i 9.81 mln w d120). Rosna tylko kiesy mieszczan: 22.38 -> 29.66 -> 37.31 -> 44.12 mln w d120 / d182 / d273 / d364. [P: S:pieniadz]

### 7b. Rachunek korony i ludu (zl na dobe)

| Pozycja | Wartosc | Zrodlo |
|---|---|---|
| Wplywy koron, srednia d337-364 | 448 915 | [P] S:lud_podatki, linia "Korona: wplywy dnia (165)" |
| w tym danina wojenna z kas osad | 217 636 | [P] jw., LOG:128736 |
| w tym podatek wojenny ludu (184) | 15 025 | [P] R5 e5:181, LOG:128746 |
| Stawka podatku ludu: srednia / pulap | 2.37% / 3.0% (6.0% z polityka War Tax); 19 z 26 krolestw na pulapie | [P] LOG:128746 |
| Zwrot zoldu rodom (70% zoldu partii) | 277 712 | [P] S:lud_podatki, LOG:128742 |
| Renty korony dla rodow | 172 233 | [P] S:lud_podatki, LOG:128744 |
| Ubytek skarbcow: srednio w roku / w ostatnim kwartale | 84 900 / 51 400 | [P] (57.93 - 27.11) mln / 363 doby; (31.79 - 27.11) mln / 91 dob |
| Zold partii rodow AI | 456 511 na 79 001 ludzi = 5.78 zl na czlowieka | [P] S:wojsko_trend, budzet-rodow.csv kolumny zold_partii, ludzi_partie, d337-364 |
| Lud miast: dochod / wydatki / odklada (srednia 53 probek roku) | 537 132 / 436 616 / 100 516 | [P] kiesa-ludu.csv kolumny dochod, wydatki, rodzaj = miasto |
| Lud miast odklada - ostatnie 8 probek | 61 612 | [P] jw. |
| Krolestwa w wojnie | srednio 26.0 z 29 przez caly rok; 16 wojen w ostatniej dobie | [P] R5 e2:12, CS:9247 |

### 7c. Co z tego wynika

1. Dzisiejsze 79 tys. wojska tez nie jest oplacone z samych biezacych dochodow. Skarbce koron topnieja o 51-85 tys. zl/d [P]. Wedlug opisu w linii zwrotu korona doklada do wplywow dnia 1/360 zapasu (LOG:128742); najemnicy to ok. 14.8 tys. zl/d (5 399 289 zl w roku, R5 e8:258). Pelnego rozbicia ubytku nie robilem. Gdy zapas spadnie, spadnie i doplata z zapasu, a wojsko pojdzie za nia w dol [S].
2. Brak do 85 tys.: 85 000 - 79 409 = 5 591 ludzi x 5.78 zl = ok. 32 tys. zl/d [S].
3. Razem potrzeba ok. 85-115 tys. zl/d nowego, stalego dochodu koron [S: 51-85 tys. ubytku + 32 tys.].
4. Tyle samo odklada lud miast: 62-100 tys. zl/d [P]. To jedyna kiesa, ktora rosnie. Zamkniety obieg podpowiada wiec jedno miejsce: mieszczanie.
5. "Na czas wojny" w tym swiecie znaczy "zawsze": 26 z 29 krolestw wojuje caly rok [P]. Podatek wojenny bedzie w praktyce podatkiem stalym.

### 7d. Propozycje (wszystko [S], do decyzji projektu, nie liczby z logu)

| Nr | Propozycja | Rachunek | Ryzyko |
|---|---|---|---|
| A | Podniesc pulap podatku wojennego ludu (184) z 3% do ok. 10-12%. Mechanizm juz jest i sam rosnie co tydzien, gdy korona ma potrzebe | 15 025 zl/d x (10 / 2.37) = ok. 63 tys. zl/d; przy 12% ok. 76 tys. [S, liniowo] | W 8-10 miastach lud juz dzis nie stac na jedzenie (nr 10 w rozdz. 1). Te miasta trzeba zwolnic albo brac tylko od zamoznych (wagi W 0.5 / R 1.0 / P 1.5 sa w linii 184) |
| B | Brac nie od dochodu, tylko od oszczednosci ponad zapas (to P1 z gotowego projektu `docs\kiesa-ludu\PROJEKT-OBIEG-KIESY-LUDU-2026-10-13.md`: danina w wojnie z oszczednosci ponad zapas) | 44.12 mln lezy; 0.2% dziennie = ok. 88 tys. zl/d na poczatku, potem maleje do tego, co lud odklada [S] | Nie rusza biednych miast. Wymaga "tak" Jeffa na projekt kiesy ludu (STAN-PRAC 1631) |
| C | Sakiewki zolnierzy: 16.59 mln lezy, 26 dni zoldu przy celu 15 | Jednorazowo ok. 7 mln (16.59 x (1 - 15/26.1)) [S]. To nie jest staly dochod | Zolnierz wydaje u markietanow, a 85% tego trafia do miast - wiec to wraca przez A albo B, nie wprost do korony |
| D | Nie brac wiecej z: kas miast (10.98 mln, 17 miast ponizej zapasu kupcow w d364), wsi (kiesy stoja od d120), notabli (-6.98 mln w roku) | - | Tam nie ma nadwyzki [P: S:kasy_miast, S:pieniadz] |

Moja kolejnosc: B (bo nie krzywdzi biednych miast i zamyka obieg u zrodla), a jesli projekt kiesy czeka - A z wyjatkiem dla miast, gdzie lud nie stac na jedzenie.
Sprawdzenie rachunku innym pomiarem: w biegu koni roboczych mieszczanie przestali wydawac na konie, panowie i korony dostali o 100-170 tys. zl/d mniej, a wojska ubylo 14.6 tys. (STAN-PRAC linie 1566 i 1597). To daje 7-12 zl na zolnierza dziennie [S]. Na 5 591 ludzi wychodzi 39-65 tys. zl/d - ten sam rzad co 32 tys. z punktu 2.
Czego nie wiem: jak mieszczanie zareaguja na podatek. Jesli kupia mniej, spadna kasy miast i zawor panow, czyli czesc zysku wroci jako strata. Trzeba to zmierzyc biegiem 120 dob przed wgraniem.

## 8. Skrypty

Wszystkie w `raport-rok\skrypty\progi\`. Kazdy uruchamiany `python -I`, tylko odczyt.

| Skrypt | Co liczy |
|---|---|
| por_progi.py | porownanie trzech zestawow progow wiersz po wierszu |
| docs_id.py | czy pozycja ma wpis w STAN-PRAC i TRIAZ |
| bledy_cs.py, cs_profil.py | bloki bledow CrashScribe i wyjatki na dobe |
| potkniecia.py | potkniecia i slowa ERROR / WARN / Exception w czterech logach |
| czas.py, czas2.py, wejscia.py | czas doby, profil, trend, skoki wejsc do osad |
| pieniadz.py, lud_podatki.py | posiadacze zlota, rachunek korony i ludu |
| ksiegi.py, towary_zboze.py | reszty ksiag koni, towarow i uprzezy |
| kasy_miast.py, thenn.py, thenn2.py | kasy miast, Thenn |
| straz_k8.py | ksiega Nocnej Strazy |
| wojsko_trend.py | wojsko, zold, dochod rodow w blokach 28 dob |

## 9. CO TRZYMA SENS

1. Pieniadz swiata jest zamkniety: +130 882 zl na 208 mln w rok (+0.063%). Rok wczesniej -2.37 mln. [P: LOG:707, LOG:129205]
2. Nikt nie placi z niczego i nic nie idzie w nicosc w zmierzonych sciezkach: bez platnika 0, bez odbiorcy 0, "zakupy" mieszczan z niczego 0 w 363 dobach, bitwy -67 zl w roku. [P: R5 e3:16-20, e5:100, e5:13]
3. Ludzie sie zgadzaja: reszta ogni domowych 0.000, najgorsza kraina 115 ludzi. Ludnosc spada o 0.35% w roku ciaglej wojny (-505 ludzi dziennie na 52.18 mln). [P: R5 e4:14, e4:147, e4:121]
4. Wojsko odbudowane o jedna czwarta: 64 013 -> 79 409. Polnoc 5 118 -> 8 272. Rody bez renty 27% -> 5.0%. [P: R5 e2:12, e8:264, e8:248]
5. Zelazo dociera do miast: miasta bez zelaza 41.4 -> 11.7 srednio, 4.2 w ostatnich 30 dobach. Wozy kupcow: 7 228 kontraktow, kontrola 0 dob z reszta. [P: R5 e5:297, e8:272, e8:268-269]
6. Biedne rody przestaly bankrutowac: glowy z kiesa ponizej 5 000 zl: 21 -> 5. [P: R5 e2:3]
7. Kompanie najemne przestaly zbierac zloto bez wydatku: +48.7 tys. -> +7.7 tys. zl/d. [P: R5 e8:266]
8. Dlug wezwan do wojny przestal isc w nicosc: 2.01 mln -> 0. [P: R5 e8:260]
9. Zero bledow naszych modow przez 364 doby: 0 ERROR, 0 potkniec, 0 blokow naszych modow w CrashScribe. [P: rozdz. 4]
10. Gra nie zwalnia z wiekiem kampanii: od doby 120 do 364 +0.01 s na 100 dob. Cala doba 11.98 s, o 1 s szybciej niz przed runda 3. [P: S:czas2]
11. Korona oddaje to, co obiecala: zwrot zoldu 99.8% naleznego (95.68 z 95.84 mln). [P: R5 e5:178]

## 10. NIESPOJNOSCI

| Nr | Co nie trzyma sensu | Dowod | Waga | Co by to naprawilo (jedno zdanie) |
|---|---|---|---|---|
| 1 | Wojsko jest oplacane z topniejacych skarbcow, nie z biezacego dochodu | skarbce 57.93 -> 27.11 mln (-51 do -85 tys. zl/d), a kiesy mieszczan +37.84 mln [P: S:pieniadz] | wysoka | Staly podatek wojenny od oszczednosci mieszczan, ktory zawraca do koron tyle, ile lud odklada (rozdz. 7) |
| 2 | Konie juczne biora sie z niczego | +56 478 jucznych w roku bez zrodla; stan swiata koni 21 839 -> 66 616 [P: S:ksiegi] | srednia | Paczka "juczne z niczego": tabor wsi dostaje juczne ze stada wsi albo z targu, nie od gry |
| 3 | Miasto Thenn bez pieniedzy przez pol roku | kasa 7 zl (d321), lud stac na 40-69% jedzenia, karawana stoi 289 dob, 10 406 szt. plonu niesprzedane [P: rozdz. 1d] | srednia | Najpierw rozpoznanie przyczyny (nie znam jej); potem droga dla karawany Thenn albo platnik zastepczy dla miasta odcietego |
| 4 | Zboze i ryby przybywaja bez wyjasnienia | +65 002 i +59 623 szt. w roku, rowno w kazdym kwartale [P: S:towary_zboze] | srednia | Dopisac do ksiegi towarow brakujace zrodlo (najpierw znalezc, ktory tick dosypuje) |
| 5 | Drewno w miastach rosnie bez konca | 6 389 -> 135 398 ladunkow (+426/d) [P: R5 e5:299] | srednia | Regulator wsi tnie wyrab, gdy stos miasta przekracza zapas na N dni (jak dla innych towarow) |
| 6 | Bogactwo mieszczan jest nierowne: razem 44 mln, a w ok. 10 miastach lud nie stac na jedzenie | srednio 9.6 miast, najwiecej 14 [P: R5 e5:102]; 2.16% wydatku na jedzenie zjedzone bez zaplaty [P: R5 e5:101] | srednia | Decyzja Jeffa z projektu (rozklad dochodu miast, plan rozdz. 7.3) |
| 7 | Biedny rod dalej ma drozszego zolnierza niz bogaty | 6.88 wobec 5.00 zl (1.38x) [P: R5 e8:265]; 14 rodow z lennem bez wojska [P: R5 e8:263] | srednia | Rod na pulapie nie awansuje ludzi ponad to, co udzwignie jego budzet (druga polowa zapadki opisana w T3 O1-3 pkt C) |
| 8 | Polowa wyprodukowanej uprzezy znika z ksiegi | -7 420 z 15 026 szt. [P: S:ksiegi] | niska (prawdopodobnie brak licznika, nie przeciek) | Dopisac do ksiegi stan "zapisy notabli" (T2 H-4b) |
| 9 | Jazdy jest za malo wobec celu | 63% celu t3+; Dothrakowie 43.9% jazdy, 27.8% czeka na konia [P: R5 e5:362, e5:368, e5:366] | srednia (znana) | Poprawka uprzezy i limit polki koni z TRIAZ-2 (K3-3, K3-8) |
| 10 | Sakiewki zolnierzy puchna | 26.1 dnia zoldu przy celu 15; 16.59 mln lezy [P: R5 e5:63] | niska | Markietani z szersza oferta albo wyzszy udzial wydatku z sakiewki |
| 11 | Pojedyncze potkniecia ksiag ludzi | Straz 56 ludzi w d175, wioski 6 ludzi w d229 [P: R5 e4:264, e4:243] | niska | Kontrola ksiegi Strazy dla doby, w ktorej partia zmienia rodzaj w obie strony |
| 12 | Jedno zdarzenie wejscia do osady trwalo 1.6 s, a stoper tej doby pokazal wiecej niz cala doba | d215: 1 594 ms; 25 671 ms przy dobie 14.6 s [P: LOG:76078-76079] | niska | Stoper wejsc liczony tylko na zewnetrznym wywolaniu; osobny odczyt, co trwalo 1.6 s |
| 13 | Koszt swatow rosnie z liczba lordow | 464 -> 697 ms/d w ciagu roku [P: S:czas] | niska | Zostawic do etapu optymalizacji (decyzja Jeffa 08.10: wydajnosc na koniec) |
| 14 | Pamiec gry rosnie caly rok | 1 218 -> 1 517 MB, tak samo w obu biegach [P: S:czas] | niska, do obserwacji w biegu 2-letnim | Nic teraz; sprawdzic w biegu dwuletnim, czy staje |

Czego nie szukalem na sile: 7 progow uznalem za do zmiany, nie za blad swiata (nr 1, 13, 36, 38, 39, 40, 41 w rozdz. 1). 15 pozycji to znane sprawy z wpisem w docs, bez pogorszenia.
