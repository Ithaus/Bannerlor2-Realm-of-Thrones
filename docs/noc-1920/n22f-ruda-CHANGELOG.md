# n22f-ruda (20.10) - dwa nowe NIE z biegu n22a160: ZC1-16 ruda srebra i D2K-7 konie bojowe - ZMIENNOSC BIEGOW, NIE REGRESJA FALI 1; poprawiony odczyt narzedzia

Galaz `w-toku/n22f-ruda` (baza 4885ae75 = w-toku/n22-calosc). **Kod gry bez zmian** (Armoury, CrashScribe, RC, GrandTourney, ForgeView nietkniete;
build Armoury kod 0 - sprawdzenie drzewa). Zmieniony tylko `tools/sprawdz_logi.py`: wiersz ZC1-16 (grupa `nzc1`) i D2K-7 (grupa `drobne2`) oraz ich opisy
(`--help`, docstringi). Klucze MCM - brak nowych (zmiana tylko w narzedziu). Druga wersja po dwoch recenzjach (sekcja "Recenzja" na dole).

## Dla Jeffa (prostymi slowami)
W tescie fali 1 zapalily sie dwa czerwone wskazniki: "ruda srebra sie gromadzi" i "koni bojowych przybywa, a hodowla nie tnie". Sprawdzilem osiem biegow
z czterech wersji moda. To nie jest blad nowych zmian. W ostatnim miesiacu przybylo rudy w jukach karawan i na wozach, a na polkach miast bylo jej mniej
niz miesiac wczesniej. Hodowla koni bojowych robi dokladnie to, co ma robic: zuzycie rowna sie produkcji, wiec nie ma czego ciac. W grze nic nie
zmieniam; poprawilem tylko miarke testu, zeby patrzyla na polki i na regule hodowli wprost, a nie na sam stos.

## (A) ZC1-16 "zapas - ruda srebra wobec produkcji" NIE w n22a160

**Problem:** n22a160 (92 + 15 galezi fali 1, bieg 160 dob z autotest-F-d240) - ZC1-16 ruda srebra NIE: +27.0/d przy produkcji 206.7/d (zapas 4834 -> 5564),
prog <= 10% produkcji w 28 ostatnich dobach. Zlecenie: "nie bylo go w 6 poprzednich biegach".

**Dowod (logi `D:/Backup-Bannerlord/logi-testow/2026-10-20`, wiersze `Towary: dzien N - silver`, `KS: produkcja (zastosowane)`):**
- Ten sam wiersz byl NIE juz przed fala 1: r3nowa (WGRANIE 91) +20.1/169.5, starsze nowa +24.9/167.2 i nowa88 +18.6/164.9 (`progi/*/nzc1.txt`);
  r2nowa +16.3/166.4 = 9.8% (TAK o wlos). Zdanie "nie bylo w 6 poprzednich" jest dla tego wiersza nieprawdziwe.
- Okna 28 dob przesuwane co dobe (od doby 56, 106 okien w biegu 160 dob), ruda srebra, stary prog: **r2160 NIE w 13/106**, r3160 1, n21160 0, n22a160 2
  (oba na samym koncu). Wiersz z jednego okna 28 dob jest szumny: zuzycie tygodniowe skacze (r2160 108-321/d, r3160 130-255, n21160 134-233,
  n22a160 145-246), a zapas rudy w ksiedze to w 2/3 towar w drodze (karawany 2 800-5 700 szt.).
- Produkcja rosnie schodkami: regale KS co 28 dob (`StartCalibration.Rule.cs`, `RegaleCycle`: `ix > 1.2` -> rece x1.1, sufit pol rak bloku poza domem).
  Indeks srebra jest 3.1-3.9 przez caly bieg we wszystkich wersjach, wiec rece przy kopalniach tylko rosna. Plan zastosowany od d109111: r2 149 -> 186,
  r3 166 -> 205, n21 159 -> 194, n22a 169 -> 212. W ostatnich 56 dobach P rosnie w drugiej polowce we wszystkich 4 biegach 160 dob.
- n22a160, dwie polowki ostatnich 56 dob: P 192.2 -> 206.7, C 198.5 -> 181.2; zapas 5061 -> 4834 -> 5564 (najpierw spadek -8.1/d, potem +27.0/d);
  56 dob +9.1/d przy 199.4 (5%). Gdzie przybylo w ostatnich 28 dobach: karawany +678, wozy +532, **polki miast + wsie 1527 -> 994 (-533)**.
- Fala 1 wobec n21 (WGRANIE 92, bezposrednia baza): plan srebra +6% na starcie (169 / 159) i +9% na koncu (212 / 194). Miedzy wersjami bez fali 1
  (r2 / r3 / n21, ten sam zapis) rozrzut startu 11% (149-166) i konca 10% (186-205); n22a jest 2% / 3% nad r3. Na gorze rozrzutu, ale w nim.
- Podejrzane galezie:
  - **p5-regale**: nie zmienia decyzji sprzedazy. `MineRegale.SellPrefix/SellPostfix` tylko zapisuja ladunek i cene (void, bez `return false`, bez zmiany
    `__result`); pobor `home.ChangeGold(-take)` jest dopiero przy powrocie wozu do wsi. ZC1-16b utarg wozow rudy srebra n22a160 5587 d/d wobec 5617 (r3160),
    5918 (r2160), 4930 (n21160) - wsie gornicze sprzedaja tyle samo.
  - **p5-handel** (barki, jarmarki): karawany kupuja / sprzedaja rude srebra srednio w calym biegu 80.1 / 41.5 szt./d wobec 70.9 / 40.5 (r2160),
    77.9 / 43.0 (r3160), 71.7 / 38.0 (n21160) - w zakresie biegow bez fali 1.
  - **p5-warsztaty**: zuzycie warsztatow srebra 60.7/d wobec 51.8-67.9 - bez sladu.

**Przyczyna:** zmiennosc biegow i odczyt narzedzia. Wiersz ocenial jedno okno 28 dob na calym zapasie ksiegi, a zapas faluje nieregularnie (zuzycie
tygodniowe, towar w drodze), przy produkcji podnoszonej schodkami regale KS co 28 dob. W n22a160 ostatnie 28 dob to odbicie po 28 dobach spadku, z nowym
schodkiem KS i nizszym zuzyciem; caly przyrost poszedl w droge (karawany, wozy), polki spadly. Regresji fali 1 brak (dowod wyzej). Zapadka regale srebra
(indeks z polek, gdy ruda lezy w jukach) jest we wszystkich wersjach - Otwarte.

**Zmiana (tylko `tools/sprawdz_logi.py`, `nzc1_main`, wiersz ZC1-16 dla jedwabiu i rudy srebra; nazwa wiersza bez zmian):**
- Prog jak dotad: przyrost zapasu w 28 dobach <= 10% produkcji. Nowy waski wyjatek "odbicie po spadku" - przyrost 10-25% nie jest NIE, gdy jednoczesnie:
  (1) poprzednie 28 dob zapas nie rosl (<= +0.5/d), (2) w 56 dobach przyrost <= 10% produkcji, (3) polki miast i wsie (posiadacze "miasta" + "wsie"
  z linii ksiegi) w 28 dobach urosly <= 10% produkcji. Przyrost > 25% w 28 dobach - zawsze NIE.
- Napis wiersza: polki miast i wsie (poczatek -> koniec 28 dob), 56 dob z zapasem w trzech punktach (poczatek, polowa, koniec) i znacznik wyjatku.

**Wynik na 8 biegach (filtr linii `Towary` z kopii logow; stara wersja 4885ae75 wobec nowej, wszystkie wiersze nzc1):** jedyna roznica to n22a160 ruda
srebra NIE -> TAK (wyjatek: 5061 -> 4834 -> 5564, polki -19.7/d). r3nowa ruda zostaje NIE jak w bazie (zapas rosl w obu polowkach: +13.8 i +20.1/d).
n21nowa jedwab zostaje NIE (+9.6 i +7.1/d). Pozostale bez zmian. Pelny `nzc1` na pelnym logu n22a160 - kod 0, wobec `progi/n22a160/nzc1.txt` rozni sie
tylko ZC1-16 ruda srebra (zostaje NIE ZC1-4o aksamit - poza zleceniem). Okna przesuwane od doby 56, ruda srebra: NIE stary / nowy - r2160 13 / 1,
r3160 1 / 1, n21160 0 / 0, n22a160 2 / 0.
- Testy syntetyczne (do zapasu i do jednego posiadacza dosypane narastajaco: czesc produkcji doby przez L ostatnich dob; baza n21160 i n22a160):
  - +12% przez 60 dob na polki: NIE / NIE; to samo w karawany: NIE / NIE;
  - +30% przez 28 dob w karawany: NIE / NIE (prog 25%);
  - +15% przez 28 dob na polki: NIE / NIE; +12% przez 35 dob na polki: NIE / NIE;
  - +15% przez 28 dob w karawany: **TAK** / NIE; +12% przez 35 dob w karawany: **TAK** / NIE; +18% przez 35 dob w karawany: **TAK** / NIE;
    +12% przez 42 doby w karawany: NIE / NIE. TAK tylko na n21160, gdzie poprzednie 28 dob zapas spadal (-8.8/d).

**Ryzyko / co sprawdzic:**
- Przepuszcza zalew w samej drodze (karawany, wozy) 10-25% produkcji, krotszy niz ok. 5-6 tygodni, gdy przed nim zapas spadal (testy wyzej). Zalew polek
  ponad 10% produkcji, przyrost trwajacy w obu polowkach 56 dob i przyrost > 25% - dalej NIE.
- Wyjatek opiera sie na liscie posiadaczy w linii ksiegi (`posiadacze [miasta N, wsie N, ...]`). Gdy linii brak - wyjatek nie dziala (stary prog).
- Inne wiersze bez zmian (diff wynikow); ZC1-16b, ZC1-16c nietkniete; `gser`, `_nz1_mean` tylko czytane.

## (B) D2K-7 "KR-8 ... ciecie stoi" NIE w n22a160

**Problem:** n22a160 - D2K-7 NIE 1: bojowy d109172: 2071 -> 2193 (105 dni, x0.34 bez zmian). W 7 innych biegach 0.

**Dowod (linie `Hodowla (220): dzien N - decyzja`, okno 56 dob, decyzja co 28 dob; kod `HarvestValve.HorseClose`):**
- Regulator: C = P - (stos - stos poczatku okna). Przy nadmiarze (stos > 60 dni zuzycia) tnie, gdy C < 0.98 P (klucz Horse Breeding Cut Over Stock,
  `HarvestValve.cs:991`), do cel = max(x0.10, 0.95 C / baseP, polowa mnoznika), baseP = P0 / MA + P1 / m0 (`:994-996`); zmiana, gdy cel < m0.
- n22a160 bojowy: d109116 stos 2187 (x0.41 -> x0.35), d109144 2071 (x0.35 -> x0.34), d109172 2193 = 105 dni, **C/P 0.99 (1168/1174)**, bez zmian.
  1174 - 1168 = 6 = 2193 - 2187 (wzor regulatora sie zgadza). C >= 0.98 P - regula slusznie nie tnie. +5.9% liczyl wiersz wobec poprzedniej decyzji,
  po spadku -5.3%.
- Koniec biegu, konie bojowe: x0.31 (r2160), x0.30 (r3160), x0.31 (n21160), x0.29 (n22a160); stos 93 / 104 / 105 / 111 dni. C/P z 6 decyzji:
  1.006 / 0.979 / 0.995 / 0.966 - w rozrzucie.
- Podejrzane galezie: **p5-turniej** (`GrandTourney/src/TourneyRansom.cs`) - przeniesienie konia, nie kon z niczego; w n22a160 linie
  "Konie i ryby - autorzy zmian (A17 D1b)" przez 161 dob: TourneyRansom.Yield bojowe +23, Buy -1 (ok. +8 na okno 56 dob przy P 1174 - bez znaczenia).
  **p5-stada** - owce, swinie, bydlo (nie konie bojowe). **p5-armia** - postoj armii nie zmienia strat koni w bitwach.

**Przyczyna:** odczyt narzedzia. Wiersz porownywal stos z poprzednia decyzja (pol okna regulatora), a nie sprawdzal reguly regulatora. Pol-okienne wahniecie
(spadek, potem powrot) wygladalo jak wzrost przy stojacym cieciu. Regresji fali 1 brak.

**Zmiana (tylko `tools/sprawdz_logi.py`, `drobne2k_main`, wiersz D2K-7):** wiersz sprawdza wprost regule `HorseClose` z liczb linii decyzji.
- NIE: decyzja "bez zmian" (bez "ciecie na sucho") przy stosie ponad gornym progiem dni zuzycia (z naglowka "stos 30-60 dni"), mnozniku ponad podloga x0.10,
  C < 0.98 P i celu regulatora < 0.97 x m0 (zapas 3% na zaokraglenie mnoznikow do 2 miejsc).
- Cel liczony jak w regulatorze: P0 / P1 z sum "produkcja doby" z linii dziennych (doby d-55..d-28 i d-27..d; suma = P z linii decyzji co do sztuki:
  n22a160 d109144 / d109172 / d109200 / d109228, r2nowa d108892 / d108920 / d108948). MA - m0 decyzji sprzed 28 dob, a bez niej mnoznik z linii doby ze
  srodka starszego kubelka. Bez linii dziennych: NIE, gdy MA <= m0 (wtedy baseP >= P / m0, czyli cel < m0 przy kazdym C/P < 0.98).
- INFO w napisie: regulator czeka z projektu (cel >= mnoznik - pierwsze okno po cieciu), brak mnoznika starszego kubelka, wzrost stosu > 5% wobec poprzedniej
  decyzji. Nazwa wiersza zmieniona na "D2K-7: KR-8 stos ponad gornym progiem dni zuzycia, a ciecie stoi (C/P < 0.98, mnoznik ponad podloga x0.10, 'bez zmian')"
  (zaden inny skrypt w drzewie jej nie czyta).

**Wynik:** 8 biegow (wyciagi linii Hodowla) - NIE 0. INFO: r2nowa d108920 bojowy "regulator czeka" (C/P 1762/2474 po x1.00 -> x0.50, cel x0.51 >= x0.50);
n22a160 d109172 bojowy "wzrost stosu > 5% wobec poprzedniej decyzji" (C/P 1168/1174). Pelny `drobne2` na pelnym logu n22a160 - kod 0; wobec
`progi/n22a160/drobne2.txt` rozni sie D2K-7 (NIE -> TAK) oraz D2K-1 / D2K-5 (INFO, bo obok kopii logow nie ma CSV - "brak budzet-rodow.csv",
"brak kasy-miast.csv"; na logu z folderu gry z CSV jak dotad).
- Log dwuletni sprzed KR-8 (`D:/Backup-Bannerlord/logs/2026-10-14-noc/Armoury-2026-10-13_21-14-13.log`, wyciag `n22f-ruda-tmp/rec/bd-hod.log`) - **NIE 7**:
  bojowy d109004 (C/P 0.91), zwykly d109032 (0.93), bojowy d109032 (0.92), zwykly d109060 (0.94), zwykly d109088 (0.92), bojowy d109116 (0.95),
  bojowy d109228 (0.95) - martwy pas 0.90-0.98 (CHANGELOG 14.10, pkt 5), dla ktorego wiersz powstal. Stary kod widzial z tego 1 przypadek
  (zwykly d109088), wersja 70d4f601 - zadnego.
- Testy syntetyczne (wyciag n22a160 / n22anowa): d109172 bojowy C/P 0.95 bez zmian - NIE (cel x0.31, okno po cieciu x0.35 -> x0.34 liczone z P0 / P1);
  d109172 stos 2400 przy C 961 (zgodnie ze wzorem) - NIE; druga decyzja biegu d109116 zmieniona na "bez zmian" przy C/P 0.90 - NIE (wczesniej nieoceniana);
  pierwsza decyzja nowej kampanii n22anowa d108892 bojowy "bez zmian" przy C/P 0.42 - NIE (MA x1.00 z linii dziennych, cel x0.50).

**Ryzyko / co sprawdzic:** wiersz zaklada klucz Horse Breeding Cut Over Stock wlaczony (domyslnie i u Jeffa). Z kluczem wylaczonym regulator tnie dopiero
przy C < 0.90 P, a wiersz da NIE w pasie 0.90-0.98 - to swiadomie (to jest blad KR-8). Pierwsza decyzja biegu z zapisu (d109088 w biegach 160 dob)
nie ma mnoznika starszego kubelka w logu - INFO. Sama regula w grze bez zmian.

## Recenzja (dwie recenzje 20.10 - co prawda, co poprawione)
1. **D2K-7 nie widzi pasa KR-8 (diagnoza, wazne) - PRAWDA.** Prog "wzrost > 5% stosu w oknie" odpowiadal C/P ok. 0.90-0.935 zaleznie od dni zuzycia.
   Na logu dwuletnim zwykly d109088 spadal do INFO. Poprawione: wiersz sprawdza regule regulatora wprost (wyzej). Na logu dwuletnim NIE 7, na 8 biegach 0.
2. **ZC1-16: przyczyna "cykl KS 56 dob" nieprawdziwa, poluzowanie szersze niz opisane (diagnoza i technika, wazne) - PRAWDA.** Regale KS to zapadka co
   28 dob (`RegCycle = 28`, `ix > 1.2` -> x1.1), a nie zmiana co 7 dob. Faluje zuzycie i towar w drodze, nie produkcja. Przepustka 56 dob przepuszczala
   staly wzrost (r3nowa) i zalewy 12-18% przez 5-7 tygodni. Poprawione: przyczyna opisana zgodnie z kodem i logiem. Przepustke 56 dob zastapil waski wyjatek
   "odbicie po spadku" z warunkiem polek (cel wiersza - "bez zalewu polek"). r3nowa wraca do NIE jak w bazie. Ryzyka wypisane z testow syntetycznych.
   Porownanie fali 1 z n21 (WGRANIE 92) dopisane w Dowodzie.
3. **D2K-7 nie ocenia drugiej decyzji biegu (diagnoza i technika, drobne) - PRAWDA.** Poprawione: mnoznik starszego kubelka z poprzedniej decyzji albo z linii
   dziennych, wiec oceniana jest druga decyzja biegu z zapisu i pierwsza decyzja nowej kampanii (testy syntetyczne wyzej).
4. **Ryzyko w CHANGELOG nieprawdziwe (technika, drobne) - PRAWDA** (zalew > 10% przez 56 dob nie zawsze dawal NIE). Regula zmieniona, ryzyka opisane od nowa.
5. **"Okno 56 dob bedzie przepuszczac rosnaca nadwyzke w dlugiej kampanii" (technika) - PRAWDA dla wersji 70d4f601.** Teraz staly wzrost w obu polowkach
   daje NIE (pierwsza polowka musi nie rosnac), a polki nie moga rosnac > 10%.

## Otwarte (poza ta galezia)
- Zapadka regale KS dla srebra: indeks liczony z polek miast, gdy ruda lezy w jukach karawan, daje +10% rak co 28 dob az do sufitu bloku (pol rak poza
  domem) - we wszystkich wersjach (r2, r3, n21, n22a). Jesli ma sie zatrzymac wczesniej (np. indeks z calego zapasu), to osobna paczka KS, nie fala 1.
- n22a160: pierwszy Plan KS (d109111) - zboze 26 769 wobec 24 328-24 637 w r2 / r3 / n21 (+9%) i ryby 3 378 wobec 3 122-3 212 (recenzja technika);
  srebro na gorze rozrzutu (wyzej). Mozliwe przesuniecie wejsc KS przez fale 1 (p5-urodzaj albo p5-stada) - nie badane w tej galezi.
- Nowa kampania: zapas rudy srebra rosnie 9-14% produkcji przez 56 dob we wszystkich wersjach, w 75-90% w jukach karawan; r3nowa NIE w ZC1-16 jak w bazie.
- n21nowa (WGRANIE 92): ZC1-16 jedwab NIE (+9.6 i +7.1/d w polowkach 56 dob) - stary i nowy prog; nie bylo w zleceniu, nie badane.
- n22a160: D2K-5 (zawor kas miast, 65 dob Selhorys) NIE i ZC1-4o (aksamit) NIE - inne wiersze, nie w zleceniu tej galezi.
- Konie bojowe stoja ok. 93-111 dni zuzycia przy rownym C i P we wszystkich biegach 160 dob - regula KR-8 nie zbija nadmiaru przy C >= 0.98 P
  (cecha, "w pokoju konie bojowe sie nie zuzywaja"); zmiana reguly to osobna decyzja projektu.

## Test w grze
Gra bez zmian - nic do wgrania z tej galezi. Po nastepnym autotescie: `python -I tools/sprawdz_logi.py --grupa nzc1 <log>` - ZC1-16 z polkami, 56 dobami
w trzech punktach i ewentualnym "wyjatek: odbicie po spadku"; `--grupa drobne2 <log>` - D2K-7 z liczba ocenionych decyzji "bez zmian" w nadmiarze i INFO.

## Status
DO SPRAWDZENIA (tylko narzedzie; kod gry bez zmian, build Armoury kod 0).
