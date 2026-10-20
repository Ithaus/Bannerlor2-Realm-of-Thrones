# n22f-najemnicy - P3W3-3 "ludzie najemnikow AI" po fali 1 (galaz w-toku/n22f-najemnicy na 4885ae75, poprawki przed WGRANIEM 93)

**Status:** DO SPRAWDZENIA (zmiana tylko w narzedziu testow; kod gry, DLL i MCM bez zmian; build Armoury kod 0). Po recenzji: prog 2 000 przywrocony
w kazdym oknie, podloga 1 500 usunieta. Otwarte: bieg kontrolny nowej kampanii bez pliku armoury-start-zloto.flag (nizej, "Otwarte").

## Dla Jeffa (prosto)
Narzedzie testow pokazalo "za malo najemnikow" w biegu nowej kampanii z fala 1 (1 679 ludzi w kompaniach, prog 2 000). Fala 1 tego nie
robi: ten sam bieg bez fali 1 (WGRANIE 92, Twoja gra) tez mial za malo (1 869), a najnowszy bieg z cala fala 1 ma 2 092. Kompanie maja
pieniadze (ok. 200 tys. zl kazda) i miejsce w umowach - traca ludzi w wojnach swoich pracodawcow. Biegi nowej kampanii na WGRANIU 92 wychodza
jednak srednio nizej niz te sprzed 92 i nie umiem jeszcze powiedziec, czy to przypadek; trzeba jednego biegu kontrolnego. Kodu gry nie
zmienialem; w narzedziu testow zostawilem prog 2 000 i dodalem wiersz, ktory od razu mowi, co trzyma kompanie.

## Problem
- Grupa `etap8`, wiersz `P3W3-3: ludzie najemnikow AI (sr28)`, prog `>= 2 000`: n22anowa (92 + 15 galezi fali 1, 91bf9c63) **1 679 - NIE**;
  nowa 2 236, nowa88 2 114, r2nowa 2 153, r3nowa 2 142 - TAK.

## Dowod
1. **Skad liczba.** `Armoury/src/MercContract.cs` `Daily()` - linia doby `Kontrakty najemnikow (185): dzien N | ... | ludzie najemnikow AI X / z umowy (w pokoju polowa) Y ...`;
   X = suma `MemberRoster.TotalRegulars` aktywnych partii wojennych rodow AI w sluzbie najemnej (`Company`). Narzedzie (`etap8_p3woj`) - srednia X
   z ostatnich 28 dob okna. Ta sama liczba jest w `budzet-rodow.csv` (wiersze `rodzaj = najemnik`, kolumna `ludzi_partie`).
2. **Biegi nowej kampanii, 120 dob** (linia 185; bloki to doby BIEGU, ostatnia kolumna = sr28 narzedzia):
   | bieg | plik start-zloto | d1-28 | d29-56 | d57-84 | d85-112 | sr28 (ostatnie 28) |
   |---|---|---|---|---|---|---|
   | r2nowa (kandydat 90) | nie | 1 502 | 2 047 | 2 164 | 2 217 | 2 153 |
   | r3nowa (WGRANIE 91) | nie | 1 150 | 1 737 | 2 240 | 2 121 | 2 142 |
   | n21nowa (WGRANIE 92, bez fali 1) | tak | 917 | 2 262 | 2 077 | 1 890 | **1 869** |
   | n22anowa (92 + 15 galezi fali 1) | tak | 1 081 | 2 226 | 1 900 | 1 723 | **1 679** |
   | n22nowa (07-23-31, budowa z p5-kruki - linie Raven News / Siege Not Yet; 107 dob w chwili odczytu) | tak | 987 | 2 517 | 2 261 | 2 152 (d85-107) | 2 092 (d80-107) |
   Starsze: nowa 2 236, nowa88 2 114 (bez pliku start-zloto). Kompanii w sluzbie w kazdym biegu tyle samo (10-11 od d29); spadek to mniej ludzi
   na kompanie (n21nowa 213 -> 165, n22anowa 190 -> 147 w d29-56 -> d93-120; r3nowa 173 -> 196).
3. **Fala 1 nie jest przyczyna.** n21nowa (bez fali 1, ten sam plik startu) tez ponizej progu; n22nowa (cala fala 1, ten sam plik) 2 092.
   Z TEGO SAMEGO zapisu d240 (ten sam stan startu): n22a160 (fala 1) mial w d29-112 o 260-510 ludzi WIECEJ niz n21160 (92): d29-56 1 878 / 1 528,
   d57-84 2 106 / 1 596, d85-112 2 141 / 1 878; sr28 d373-400: r2160 2 421, r3160 2 403, n21160 2 295, n22a160 2 197. Kod fali 1
   (`git diff 3957fa13 91bf9c63 -- '*.cs'`) nie dotyka `MercContract`, `CapOf`, werbunku kompanii ani linii 185; `AiDowry` i `VassalRansomAid`
   pomijaja najemnikow (`IsUnderMercenaryService`), `LoserSkills` zmniejsza smiertelnosc przegranych (oczekiwani zabici -6.4%).
4. **Rozrzut jednego biegu jest duzy** (to jest argument za ostroznoscia, nie za "dopiero rosna"): n21160 i n22a160 - ten sam zapis, kod rozni sie
   tylko fala 1 - roznily sie w oknach 28 dob d29-112 o 260-510 ludzi, a n21160 spadl w d29-84 do ok. 1 530-1 600 i odbil do 2 258 w d133-160.
5. **Co trzyma kompanie** (CSV, sr28 wierszy `najemnik`; nowy wiersz P3W3-3b), r3nowa / n21nowa / n22anowa / n22a160 / n21160:
   ludzie / limit partii 32 / 28 / 25 / 32 / 31%; ludzie / ludzie z umowy (185) 0.79 / 0.67 / 0.64 / 0.68 / 0.72; wiersze przy pulapie SWOJEJ umowy
   (zold partii >= 0.9 pulapu) 30 / 28 / 29 / 22 / 30%; zold / pulap razem 76 / 68 / 62 / 69 / 73%; kiesa na kompanie 231 / 201 / 211 / 286 / 267 tys. zl.
   Kontrakt / zold partii 1.30 (n22anowa) i 1.27 (n21nowa), niedoplata <= 3%, odejsc po niedoplacie 0. Udzial kompanii przy pulapie umowy jest
   ten sam w biegach niskich i wysokich - pulap (przeglad 28 dob tylko w dol, C1-m) nie tlumaczy roznicy miedzy biegami; zloto tez nie.
6. **Gdzie gina ludzie:** te same 11 kompanii w kazdym biegu, ale z innymi pracodawcami. Spadek skupiony w kilku kompaniach i ich wojnach:
   n21nowa Sons of the Harpy -223 (Wyspy Letnie), Brotherhood without Banners -123 (Norvos), The Windblown -128 (Dothrakowie); n22anowa The Windblown -150
   (Lys), Wild Hares -106 (Skagos), Brotherhood -101 (Tyrosh), Bright Banners -99 (Sarnor). W r3nowa tez traca (The Windblown -103), ale rosna kompanie
   Pentos, Smoczej Skaly i Targaryenow, a Stone Crows weszly do sluzby po d56 (+252).
7. **Mechanizmu z WGRANIA 92 ani z pliku startu nie ma w kodzie.** `git diff 1ffbd891 3957fa13 -- '*.cs'`: StartKit (tnie tylko `Hero.MainHero.Gold`),
   wioska i dom gracza, przetop, zlecenia kowalskie (`GiverPay`/`CraftOrderClanPurse` - wyplaty gracza), napisy noclegow - nic nie dotyka kompanii AI.
   Jedyna zmiana swiata AI to `FreeFolkAllAdultsFight` (pula Wolnych Ludzi), a kompanie n21nowa sluzyly w Essos (Qohor, Norvos, Myr, Sarnor,
   Dothrakowie, Pentos, Smocza Skala). Plik `armoury-start-zloto.flag` wybiera opcje BK `start_mariner`: `BannerKings.Managers.CampaignStart.DefaultStartOptions`
   (dekompilacja) - 10 000 zl, statek i ok. 8 marynarzy tylko dla `MobileParty.MainParty`; swiat dostaje inny przebieg losowania, nic wiecej.

## Przyczyna
Nie fala 1 (pkt 3) i nie blad w kodzie gry, ktory umiem wskazac. Kompanie traca ludzi w wojnach pracodawcow i nie dobieraja ich mimo zlota
i miejsca w umowie (pkt 5-6). To, czy nizszy poziom biegow nowej kampanii z plikiem start-zloto (srednio ok. 1 880 przy 2 161 czterech biegow
bez pliku) jest przypadkiem, czy skutkiem WGRANIA 92 albo opcji startu, **nie jest rozstrzygniete**: 3 biegi z plikiem (1 679 - 2 092) to za
malo, a mechanizmu w kodzie nie ma (pkt 7). Pierwsza wersja tego wpisu twierdzila, ze prog 2 000 mial wiazac tylko w biegu rocznym - to
bylo zle: wpis P3/W3-B (CHANGELOG.md, "Sprawdzic w biegu: ... 'ludzie najemnikow AI' >= 2 000" obok liczb d1-120) kazal go sprawdzac takze
w biegu 120 dob, a docstring `etap8_p3woj` wymienia 2 000 bez dopisku "bieg roczny".

## Zmiana (tylko narzedzie, kod gry bez zmian)
`tools/sprawdz_logi.py`, `etap8_p3woj`:
1. `P3W3-3`: prog `>= 2 000` w kazdym oknie, jak przed ta galezia (podloga 1 500 i warunek `year` z pierwszej wersji usuniete). W opisie progu
   dopisane "albo straty w wojnach i werbunek - czytac P3W3-3b i P3W3-4"; w komentarzu - skad prog i jaki jest rozrzut biegow.
2. Wiersz INFO `P3W3-3b` (z `budzet-rodow.csv` i linii 185, sr28): ludzie / limit partii, ludzie / ludzie z umowy, wiersze kompanii przy pulapie
   SWOJEJ umowy (zold >= 90% pulapu; ich ludzie), zold / pulap razem, kiesa na kompanie. Regula odczytu nie wnioskuje juz "to nie pulap" z samej sumy.

Wynik (blok `etap8_p3woj` na logach z katalogu gry): r3nowa TAK 2 142, P3W3-3b `2104 / 6508 (32%), 2142 / 2698 (0.79), przy pulapie 89 z 301
wierszy (30%, ludzi 748), 11372 / 14947 zl (76%), 230792 zl (10.8 kompanii)`; n21nowa **NIE 1 869** (`1825 / 6433 (28%), 1869 / 2809 (0.67),
przy pulapie 87 z 308 (28%), 9911 / 14677 zl (68%), 200899 zl`); n22anowa **NIE 1 679** (`1632 / 6516 (25%), 1679 / 2632 (0.64), przy pulapie
88 z 307 (29%), 9036 / 14611 zl (62%), 210911 zl`); n22a160 (`--do-doby 400`) TAK 2 197; n21160 (`--do-doby 400`) TAK 2 295. Wynik nie zalezy
juz od `--do-doby` (bez warunku `year`).

## Ryzyko / co sprawdzic (zasada 0)
- Nic w grze sie nie zmienia (bez DLL). Narzedzie: P3W3-3 jak przed galezia; nowy jest tylko wiersz INFO P3W3-3b (bez werdyktu).
- P3W3-3 NIE w biegu nowej kampanii nie blokuje fali 1 (pkt 3), ale nie wolno go czytac jako "zmiennosc udowodniona" - patrz Otwarte.

## Otwarte
- **Bieg kontrolny** (do zlecenia przez prowadzacego - tu zakaz uruchamiania gry): nowa kampania 120 dob na tej samej budowie BEZ pliku
  `armoury-start-zloto.flag` (albo r3 z plikiem). Jesli bez pliku wyjdzie ok. 2 100+ - przyczyna w przebiegu startu z opcja BK (los), jesli
  znow < 2 000 - szukac w WGRANIU 92 (jest w grze Jeffa), mimo braku mechanizmu w diffie. Dokonczenie biegu 07-23-31 (n22nowa) dolozy jeden punkt.
- Kiesy kompanii w biegach nowej kampanii spadaja (P3W3-4): przed 92 -1.9 do -2.6 tys. zl/d, na 92 z plikiem startu -5.4 (n21nowa) i -4.5 (n22anowa)
  tys. zl/d na 11 kompanii; z zapisu d240 r3160 +1.5, n21160 +0.3, n22a160 +0.9 tys. zl/d. Zapas kies ok. 200 tys. na kompanie (rok+). Ten sam podzial
  co wyzej (92 z plikiem startu) - ten sam bieg kontrolny to rozstrzygnie.
- Kompanie maja 25-32% limitu partii i 0.64-0.79 ludzi z umowy przy luzie w zlocie - to werbunek AI i straty w wojnach, nie marza; glebiej nie badalem.

## Test w grze
Nie dotyczy (bez DLL). Po nastepnym biegu: `python -I tools/sprawdz_logi.py --grupa etap8 <Armoury-*.log>` (bieg 160 dob z zapisu: `--do-doby 400`) -
P3W3-3 jak dotad (>= 2 000) i wiersz P3W3-3b.

## Recenzja (20.10, dwie soczewki) - co z kazdym punktem
- **diagnoza / wazne - "zmiennosc" nie oddzielona od WGRANIA 92 i pliku startu, podloga 1 500 to zaslania:** PRAWDA. Podloga usunieta (prog 2 000
  wszedzie), wpis nie twierdzi juz, ze zmiennosc jest udowodniona; spadek opisany jako sprawa WGRANIA 92 z plikiem startu (Otwarte). Nowy punkt
  danych: n22nowa (cala fala 1, ten sam plik) 2 092 w d80-107 - wiec plik i 92 nie daja zawsze < 2 000, ale srednia 3 biegow z plikiem (ok. 1 880)
  jest nizsza od 4 biegow bez pliku (2 161); bez biegu kontrolnego nie rozstrzygam.
- **diagnoza / drobne - uzasadnienie "w krotkim oknie kompanie dopiero rosna" przeczy danym:** PRAWDA. Zdanie usuniete z komentarza i z Przyczyny;
  argumentem jest rozrzut biegow (pkt 4), a bieg roczny Armoury-2026-10-18_04-41-44 (wedlug recenzji: doby biegu 93-120 1 814, koniec 2 598; logu nie mam w kopii 20.10) to potwierdza.
- **diagnoza / drobne - P3W3-3b zbiorczo chowa kompanie przy pulapie umowy:** PRAWDA. Dodane: wiersze przy pulapie swojej umowy i ludzie wobec ludzi
  z umowy. Wynik: 28-30% wierszy przy pulapie w biegach nowej kampanii, tyle samo w niskich i wysokich (r3160 33%, n22a160 22%) - pulap nie tlumaczy
  roznicy miedzy biegami. Regula odczytu poprawiona.
- **diagnoza / drobne - `year` zalezy od `--do-doby`, nie od dlugosci okna:** PRAWDA dla pierwszej wersji; po usunieciu warunku `year` z P3W3-3
  werdykt tego samego logu nie zalezy od `--do-doby` (dla biegu 120 dob okno i ostatnie 28 dob sa te same przy 120 i przy 364).
- **technika / wazne - Przyczyna "brakowalo warunku year wedlug docstringu" nie wynika z kodu ani dokumentow; podloga pod jedynym NIE:** PRAWDA
  (CHANGELOG.md P3/W3-B: "Sprawdzic w biegu ... >= 2 000" przy liczbach d1-120). Przyczyna przepisana, poluzowanie cofniete.
- **technika / wazne - zmienna zakloca (plik start-zloto + WGRANIE 92):** PRAWDA. Nazwana w Dowodzie (pkt 2 i 7) i w Otwartych, z biegiem kontrolnym.
- **technika / drobne - kolumna "d113-140" to 6-7 dob:** PRAWDA. Tabela przeliczona na bloki dob biegu (d1-28 ... d85-112) i sr28 z narzedzia.
- **technika / drobne - liczby w komentarzach (55-65%), docstring, brak Status:** PRAWDA. Komentarze maja zmierzone zakresy (zold 62-78%, ludzie
  25-32% limitu); docstring bez zmian, bo prog znow jest 2 000 jak w nim napisano; Status dopisany na gorze.
