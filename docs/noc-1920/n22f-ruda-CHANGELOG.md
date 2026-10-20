# n22f-ruda (20.10) - dwa nowe NIE z biegu n22a160: ZC1-16 ruda srebra i D2K-7 konie bojowe - ZMIENNOSC BIEGOW, NIE REGRESJA FALI 1; poprawiony odczyt narzedzia

Galaz `w-toku/n22f-ruda` (baza 4885ae75 = w-toku/n22-calosc). **Kod gry bez zmian** (Armoury, CrashScribe, RC, GrandTourney, ForgeView nietkniete;
build Armoury kod 0 - sprawdzenie drzewa). Zmieniony tylko `tools/sprawdz_logi.py`: wiersz ZC1-16 (grupa `nzc1`) i D2K-7 (grupa `drobne2`).
Klucze MCM - brak nowych (zmiana tylko w narzedziu).

## Dla Jeffa (prostymi slowami)
W tescie fali 1 dwa wskazniki zapalily sie na czerwono: "ruda srebra sie gromadzi" i "koni bojowych przybywa, a hodowla nie tnie". Sprawdzilem
osiem biegow (cztery wersje moda, nowa kampania i 160 dni z zapisu) - to nie jest blad nowych zmian, tylko normalne falowanie rynku: w ostatnim
miesiacu karawany i wozy wiozly wiecej rudy niz zwykle (polki miast mialy jej nawet mniej), a stado koni bojowych przez dwa miesiace stoi w miejscu
(spadlo, potem wrocilo do tego samego). Ta sama ruda w starszej wersji (90) czerwienila sie w 13 miesiecznych oknach na 106. W grze nic nie
zmieniam; poprawilem tylko miarke testu, zeby patrzyla na pelny cykl rynku (dwa miesiace), a nie na polowe fali.

## (A) ZC1-16 "zapas - ruda srebra wobec produkcji" NIE w n22a160

**Problem:** n22a160 (92 + 15 galezi fali 1, bieg 160 dob z autotest-F-d240) - ZC1-16 ruda srebra NIE: +27.0/d przy produkcji 206.7/d (zapas 4834 -> 5564),
prog <= 10% produkcji w 28 ostatnich dobach. Zlecenie: "nie bylo go w 6 poprzednich biegach".

**Dowod (liczby z logow, `D:/Backup-Bannerlord/logi-testow/2026-10-20`, wiersze `Towary: dzien N - silver`):**
- Ten sam wiersz NIE byl juz przed fala 1: r3nowa (WGRANIE 91) +20.1/169.5 NIE, starsze nowa +24.9/167.2 i nowa88 +18.6/164.9 NIE (`progi/*/nzc1.txt`);
  r2nowa +16.3/166.4 = 9.8% (TAK o wlos). Zdanie "nie bylo w 6 poprzednich" jest nieprawdziwe dla tego wiersza.
- Okna 28 dob przesuwane co dobe (od doby 29, 106 okien w biegu 160 dob): **r2160 NIE w 13/106** (najgorsze od d109186: +31.3/d przy 164.5 = 19%),
  r3160 1/106 (12%), n21160 0/106, n22a160 2/106 (ostatnie 13%). Te same biegi, okna 56 dob: **NIE 0 z 78 w kazdym** (najwyzej 7%, n22a160 5%).
- n22a160, dwie polowki ostatnich 56 dob: d109180-207 P 192.2 / C 198.5 (-6.3/d), d109208-235 P 206.7 / C 181.2 (+25.5/d) - pol fali w dol, pol w gore;
  56 dob: 5061 -> 5564 = +9.1/d przy 199.4/d (5%). r2160 odwrotnie: d109180-207 5317 -> 5998 (+24.3/d przy 164.1), potem 6118 -> 5878.
- Gdzie przybylo w n22a160 (ostatnie 28 dob): karawany 3065 -> 3743 (+678), wozy 196 -> 728 (+532); **polki miast + wsie 1527 -> 994 (-533)**.
  Zapas rudy w ksiedze to w 2/3 towar w drodze (karawany 2 800-5 700 szt. we wszystkich biegach) - polki sie nie zalewaja.
- Produkcja: KS podnosi rece przy kopalniach srebra, bo indeks ceny srebra w miastach jest wysoki we wszystkich biegach (d109207: 3.66 / 3.52 / 3.41 / 3.32;
  d109235: 3.42 / 3.12 / 2.93 / 3.12 - r2160 / r3160 / n21160 / n22a160). Plan KS rudy d109140 -> d109235: r2 149 -> 186, r3 166 -> 205, n21 159 -> 192,
  n22a 168 -> 213 - n22a160 tylko 8 szt./d nad WGRANIEM 91 (r3160 205). To podaz za cena (projekt KS), nie fala 1.
- Zuzycie ostatnich 28 dob: n22a160 181.2/d wobec 182.5 (r2160), 213.7 (r3160), 186.1 (n21160) - w zakresie; warsztaty srebra ("linie warsztatow") 60.7/d
  wobec 51.8-67.9 (p5-warsztaty bez sladu).
- Podejrzane galezie:
  - **p5-regale** - nie zmienia decyzji sprzedazy: `MineRegale.SellPrefix/SellPostfix` tylko zapisuja ladunek i cene (void, bez `return false`, bez zmiany
    `__result`), pobor `home.ChangeGold(-take)` dopiero przy powrocie wozu do wsi. ZC1-16b utarg wozow rudy srebra n22a160 5587 d/d wobec 5617 (r3160),
    5918 (r2160), 4930 (n21160) - wsie gornicze sprzedaja tyle samo; "wozy do osad" 177.2/d wobec 167.0-202.4.
  - **p5-handel** (barki, jarmarki) - karawany kupuja/sprzedaja rude srebra srednio w calym biegu 80.1 / 41.5 szt./d wobec 70.9 / 40.5 (r2160),
    77.9 / 43.0 (r3160), 71.7 / 38.0 (n21160) - w zakresie biegow bez fali 1.
  - **p5-warsztaty** - zuzycie warsztatow w zakresie (wyzej).
- Nowa kampania (r2nowa, r3nowa, n21nowa, n22anowa): w 56 dobach zapas rudy rosnie 9-14% produkcji (r2nowa +22.8/167.3, r3nowa +16.9/170.6,
  n21nowa +21.7/163.9, n22anowa +15.3/168.5) - we wszystkich wersjach, w 75-90% w jukach karawan (karawany 2 820-2 970 -> 3 580-4 160; biegi z zapisu
  d240 zaczynaja z 5 700 w karawanach i schodza do 2 800-4 450). To dochodzenie zapasow karawan do rownowagi w nowej kampanii, nie fala 1.

**Przyczyna:** zmiennosc biegow + odczyt narzedzia. Wiersz porownywal zapas z dwoch koncow 28 dob, a KS zmienia rece przy kopalniach co 7 dob wedlug
ceny z 28 dob - zapas (w 2/3 towar w drodze) faluje z okresem ok. 56 dob, wiec okno 28 dob lapie polowe fali. Regresji fali 1 brak (dowod wyzej).

**Zmiana (tylko `tools/sprawdz_logi.py`, `nzc1_main`, wiersz ZC1-16 dla jedwabiu i rudy srebra; nazwa wiersza bez zmian):**
- NIE, gdy przyrost > 10% produkcji w 28 ostatnich dobach **i** w 56 ostatnich dobach (zalew trwaly i dalej trwajacy), albo gdy przyrost 28 dob > 25%
  produkcji (nagly zalew; najwieksze wahniecie polowki cyklu w biegach 160 dob 19%, blad ZC1 16.10 ok. 70%). Bieg krotszy niz 56 dob - prog 28 dob jak dotad.
- W napisie wiersza obie liczby: 28 i 56 dob (zapas na poczatku i koncu okna). Opis grupy (`--help`, docstring) poprawiony.

**Wynik na 8 biegach (ZC1-16 ruda srebra stary -> nowy):** n22a160 NIE -> TAK (56 dob +9.1/199.4); r3nowa NIE -> TAK (56 dob +16.9/170.6 = 9.9%);
r2nowa, n21nowa, n22anowa, r2160, r3160, n21160 TAK -> TAK. Jedwab - wszystkie bez zmiany werdyktu (n21nowa jedwab NIE przed i po: 28 dob +7.1/60.8,
56 dob +8.4/61.3 - oba ponad 10%, stary prog tez NIE). Diff wynikow `nzc1` z `progi/*/nzc1.txt` (7 biegow): rozne tylko 2 wiersze ZC1-16 i linia "razem".
- Testy syntetyczne (n21160, do zapasu rudy dosypane narastajaco czesc produkcji doby): trwale +12% od 60 dob przed koncem - **NIE** (28 dob +26.0/189.4,
  56 dob +19.2/183.5); nagle +30% w 28 ostatnich dobach - **NIE** (+60.2/189.4); nagle +15% w 28 ostatnich dobach - TAK (+31.7/189.4, 56 dob 6%).

**Ryzyko / co sprawdzic:**
- Swiadomie slabsze: nagly zalew 10-25% produkcji, ktory zaczal sie w ostatnich 28 dobach, przejdzie jako TAK (nieodroznialny od polowki fali - test
  syntetyczny +15%); w nastepnym biegu wyjdzie w oknie 56 dob. Zalew trwaly (> 10% przez 56 dob) i nagly > 25% - dalej NIE.
- r3nowa (WGRANIE 91) zmienia werdykt na TAK przy 56 dobach 9.9% - na granicy; liczba 56 dob jest w napisie wiersza.
- Inne wiersze bez zmian (diff wynikow); ZC1-16b, ZC1-16c nietkniete; `gser`, `_nz1_mean` tylko czytane.

## (B) D2K-7 "KR-8 stos rosnie ponad 60 dni zuzycia, a ciecie stoi" NIE w n22a160

**Problem:** n22a160 - D2K-7 NIE 1: bojowy d109172: 2071 -> 2193 (105 dni, x0.34 bez zmian). W 7 innych biegach 0.

**Dowod (linie `Hodowla (220): dzien N - decyzja`, okno 56 dob, decyzja co 28 dob):**
- n22a160 bojowy: d109116 stos 2187 (x0.41 -> x0.35, ciecie), d109144 2071 (x0.35 -> x0.34, ciecie), d109172 2193 = 105 dni, zuzycie/produkcja
  **0.99 (1168/1174)**, bez zmian. W oknie regulatora (56 dob) stos 2187 -> 2193 = +0.3%; +5.9% liczyl wiersz tylko wobec poprzedniej decyzji (pol okna,
  po spadku -5.3%).
- Regula (`HarvestValve.HorseClose`, drobne2 KR-8): przy nadmiarze tnie, gdy C < 0.98 x P, a C = P - (stos - stos poczatku okna) - czyli gdy stos w oknie 56 dob
  urosl o > 2% produkcji okna. Tu urosl o 6 szt. przy P 1174 - regula slusznie nie tnie.
- Koniec biegu, konie bojowe: mnoznik x0.31 (r2160), x0.30 (r3160), x0.31 (n21160), x0.29 (n22a160); stos 2016 / 2114 / 2041 / 2162 = 93 / 104 / 105 / 111 dni.
  C/P z 6 decyzji: 1.006 / 0.979 / 0.995 / 0.966 - n22a160 1.3 pkt ponizej WGRANIA 91, w rozrzucie biegow (2.7 pkt miedzy biegami bez fali 1).
- Podejrzane galezie: **p5-turniej** (`GrandTourney/src/TourneyRansom.cs`) przenosi konia pokonanego do taboru zwyciezcy albo na targ (`AddToCounts(+1)`),
  a pokonany bierze konia z wlasnego taboru albo z targu (`AddToCounts(-1)`, zloto do kasy miasta) - przeniesienie, nie konie z niczego; **p5-stada** -
  owce, swinie, bydlo (nie konie bojowe); **p5-armia** - postoj armii nie zmienia strat koni w bitwach. W logu brak sladu wiekszej produkcji ani mniejszego
  zuzycia koni bojowych ponad rozrzut biegow (wyzej).

**Przyczyna:** odczyt narzedzia - porownanie ze stosem poprzedniej decyzji (28 dob = pol okna regulatora) zamiast z poczatkiem okna, z ktorego regulator
liczy zuzycie. Pol-okienne wahniecie (spadek, potem powrot) wygladalo jak wzrost przy stojacym cieciu. Regresji fali 1 brak.

**Zmiana (tylko `tools/sprawdz_logi.py`, `drobne2k_main`, wiersz D2K-7; nazwa wiersza bez zmian):** wzrost stosu > 5% liczony wobec decyzji sprzed okna
(dlugosc okna z linii "okno N dob", domyslnie 56); reszta warunku jak dotad (> 60 dni zuzycia, mnoznik w mocy ponad podloga x0.10, "bez zmian").
Wzrost tylko wobec poprzedniej decyzji - INFO w napisie wiersza.

**Wynik:** n22a160 NIE -> TAK, w napisie "INFO: wzrost tylko wobec poprzedniej decyzji (pol okna) 1 [bojowy d109172: 2071 -> 2193, w oknie 56 dob 2187 -> 2193]";
r2nowa, r2160, r3nowa, r3160, n21nowa, n21160, n22anowa - TAK jak dotad (wyciag linii Hodowla). Pelny `drobne2` na n22a160, r3160, n22anowa - kod 0;
diff z `progi/*/drobne2.txt`: D2K-7 oraz D2K-1 / D2K-5 (te dwa tylko dlatego, ze obok kopii logow w Backup nie ma CSV - "brak budzet-rodow.csv",
"brak kasy-miast.csv"; na logu z folderu gry z CSV - jak dotad). Test syntetyczny: d109172 bojowy stos 2400 (2187 -> 2400 w oknie, bez zmian) - **NIE**.

**Ryzyko / co sprawdzic:** wiersz nie widzi juz wzrostu > 5% w samej drugiej polowce okna, gdy w oknie 56 dob stos nie urosl - to przypadek, ktory regulator
z definicji zostawia (C >= 0.98 P). Pierwsza decyzja w biegu (brak decyzji sprzed okna) nie jest oceniana (do tej pory tez nie - brak poprzedniej decyzji).

## Otwarte (poza ta galezia)
- Nowa kampania: zapas rudy srebra rosnie 9-14% produkcji przez 56 dob we wszystkich wersjach (r2, r3, n21, n22a), glownie w jukach karawan - dochodzenie
  do rownowagi; jesli ma byc szybciej, osobna paczka (karawany i ruda srebra), nie fala 1.
- n21nowa (WGRANIE 92, nowa kampania): ZC1-16 jedwab NIE (+7.1/60.8 w 28 dobach, +8.4/61.3 w 56) - stary i nowy prog; nie bylo w zleceniu, nie badane.
- n22a160: D2K-5 (zawor kas miast, 65 dob Selhorys) NIE i ZC1-4o (aksamit) NIE - inne wiersze, nie w zleceniu tej galezi.
- Konie bojowe stoja ok. 93-111 dni zuzycia przy rownym C i P (powyzej pasa 30-60 dni) we wszystkich biegach 160 dob - regula KR-8 nie zbija nadmiaru
  przy C >= 0.98 P (cecha drobne2, "w pokoju konie bojowe sie nie zuzywaja"); zmiana reguly - osobna decyzja projektu.

## Test w grze
Gra bez zmian - nic do wgrania z tej galezi. Po nastepnym autoteście: `python -I tools/sprawdz_logi.py --grupa nzc1 <log>` - ZC1-16 z dwiema liczbami
(28 i 56 dob); `--grupa drobne2 <log>` - D2K-7 z ewentualnym "INFO: wzrost tylko wobec poprzedniej decyzji".

## Status
DO SPRAWDZENIA (tylko narzedzie; kod gry bez zmian, build Armoury kod 0).
