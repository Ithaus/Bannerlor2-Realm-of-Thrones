# RAPORT ROCZNY - rozdzial: GOSPODARKA TOWAROWA I LUDZIE MIAST (klucz: gospodarka)

Bieg: nowa kampania, 364 doby, sam swiat AI. Kod: w-toku/zbior-e8 9e2e0e6 (WGRANIE 36 + runda 3).
Praca tylko do odczytu. Nic nie uruchamialem, nic nie zmienialem.

## 0. Skad sa liczby i jak je czytac

| Skrot | Co to jest |
|---|---|
| LOG | D:\Backup-Bannerlord\logs\2026-10-13-noc34-35\Armoury-2026-10-13_06-24-10.log; "L129238" = numer linii w tym pliku |
| LOG-S | poprzedni bieg roczny (WGRANIE 34): Armoury-2026-10-13_02-15-21.log w tym samym katalogu |
| CSV | tabele z ...\Logs\2026-10-13_06-24-10\ (nazwa pliku + kolumna) |
| progi | ...\scratchpad\test\progi-rok5\etapN.txt (nazwa wiersza), stare: progi-rok364, test 120 dob: progi-fin4 |
| [P] | pomiar - liczba wprost z logu albo tabeli |
| [S] | szacunek - moj rachunek z kilku pomiarow |

- Doba 1 = pierwsza doba kampanii (kolumna dzien 108836 w CSV). Doba 364 = dzien 109199.
- Kwartaly: Q1 = doby 1-91, Q2 = 92-182, Q3 = 183-273, Q4 = 274-364. "Q1-4: a / b / c / d" = srednie z dob kwartalu.
- Srednie kwartalne licza moje skrypty z linii dobowych (364 linie kazdego rodzaju). Skrypty i wyniki: ...\raport-rok\skrypty\gospodarka\ (pliki s1..s6 i wynik_*.txt).
- UWAGA: kiesa-ludu.csv ma probke co 7 dob (53 probki), nie kazda dobe. Liczby "dob" z tego pliku to liczby probek.
- Zloto swiata jest stale: 207.93 mln (doba 30, L10804) -> 208.34 mln (doba 364, L129205) [P]. To wazne: pieniadz nie znika, tylko zmienia wlasciciela.

## 1. Jedzenie

### 1.1 Prawdziwy glod (brak jedzenia) - NIE MA GO

| Co | Wynik w roku | Zrodlo |
|---|---|---|
| Wsie z glodem | 0 w kazdej dobie | LOG linia "Spichlerze (E4/6)", pole "glod: wsi", 364 linie; d364 L128976 [P] |
| Okregi glodne | 0 w kazdej dobie | ta sama linia, "okregi glodne" [P] |
| Zmarli z glodu (wies, miasto, uchodzcy) | 0 | L128976 "zmarli z glodu ... od poczatku kampanii: ze wsi 0, z miast 0" [P] |
| Spichlerze wsi swiata | 728 -> 726 dni potrzeby | L480 -> L128976 [P] |
| Plon dnia wobec potrzeby | 119.5 / 118.8 / 118.5 / 118.5 % | linia Spichlerze, Q1-4 [P] |
| Warownie z zapasem 0 (miasta i zamki) | 0 w kazdej dobie | linia "Glod ludu (E5/C2)", "Food 0 warowni"; CSV spichlerze-regiony.csv kolumna zywnosc: 0 regiono-dni <= 0 [P] |
| Zapas jedzenia warowni razem | 3.68 mln -> 4.19 mln | linia "Jedzenie z targu", L743 -> L129247 [P] |
| Warownie z zapasem < 30 dni | 0 w kazdej dobie | CSV spichlerze-regiony.csv, dni_zapasu_U27 [P] |
| Racje spalone przy najazdach | 132 tys. / 436 tys. / 626 tys. / 563 tys. na dobe | linia Spichlerze, Q1-4 [P] - to ok. 1.3% plonu dnia (48.8 mln racji) [S] |

Wniosek: jedzenia w swiecie jest duzo. Wojna go nie niszczy w skali, ktora boli.

### 1.2 "Nie stac" (brak pieniedzy na jedzenie) - ROSNIE CALY ROK

| Co | Q1 | Q2 | Q3 | Q4 | doba 364 | Zrodlo |
|---|---|---|---|---|---|---|
| Miast, gdzie lud ma < 95% na jedzenie | 0.9 | 4.6 | 6.8 | 9.1 | 9 z 97 | LOG "Kiesa ludu", L129238 [P] |
| Miast z pelnym koszykiem < 95% | 1.6 | 7.4 | 10.5 | 13.8 | 14 z 97 | ta sama linia [P] |
| Zjedzone bez zaplaty, zl na dobe | 2 856 | 3 872 | 6 686 | 12 201 | 13 709 | ta sama linia [P] |
| - w tym miasta | 2 189 | 3 142 | 4 570 | 6 520 | 5 569 | [P] |
| - w tym zamki (podzamcza) | 667 | 730 | 2 116 | 5 681 | 8 140 | [P] |
| Zjedzone bez zaplaty, % wydatku gry | 1.16 | 0.81 | 1.29 | 2.25 | 2.54 | [P]; prog < 1% - progi etap5 B1-T6: NIE (2.16%) |
| Podzamcza: stac (%) | 99.0 | 99.1 | 98.0 | 96.4 | 97.5 | [P] |
| Zamkow bez kredytu dworu | 9.4 | 8.2 | 10.7 | 21.3 | 26 z 130 | [P] |
| Osad z brakiem pieniedzy 7 dob z rzedu | 0.0 | 0.2 | 0.3 | 0.3 | 1 | LOG "Glod ludu", L129242 [P] |

- Q1 jest zawyzony przez pierwsza dobe (144 080 zl, rozruch, L732). Bez niej Q1 to ok. 1.3 tys. zl na dobe [S].
- Wedlug osad (CSV kiesa-ludu.csv, kolumna stac_jedzenie, 53 probki): choc raz ponizej 95% bylo 30 miast z 97 i 35 zamkow z 130. Nikt nie byl ponizej przez 28 probek. Najczesciej: Red Keep 27 probek, Astapor 25, Mhysa Faer 24, Aquos Dhaen 23, Evenfall Hall 19, Karhold 18 [P].
- Ostatnia probka, najbiedniejsi: Ten Towers 0.20, New Ghys 0.36, Sisterton 0.39, Seagard 0.40, Celtigar Keep 0.63, Thenn 0.69 (stac_jedzenie) [P]. To glownie wyspy i porty z dala od armii.
- Zamki: polka jedzenia na targu < 10 dni zuzycia ma 124 -> 98 zamkow z 130 (L743 -> L129247), mediana 5.0 -> 5.5 dnia [P]. Zalogi nie gloduja (maja wlasny zapas), ale targ podzamcza jest cienki.

Dlaczego rosnie [S]: koszt jedzenia ludu rosnie razem z dobrobytem, a dochod wolniej.
- Wydatek ludu miast na jedzenie: 102 901 (doba 30, L10837) -> 137 155 zl (doba 364, L129238) = +33% [P].
- Dobrobyt miast: 475 883 (doba 30) -> 624 706 (doba 364) = +31% (CSV kasy-miast.csv, kolumna dobrobyt) [P].
- Dochod ludu z zewnatrz i z kas: 188 364 -> 221 781 zl = +18% (linia Kiesa ludu) [P].

## 2. Towary i ceny

### 2.1 Surowce - indeks ceny i miasta bez towaru

Indeks: 1.0 = cena bazowa, 10 = sufit. Zrodlo: LOG linia "Ceny surowcow" (mediana po miastach), d364 = L129060 [P].

| Surowiec | Indeks d1 / d91 / d182 / d273 / d364 | Miast BEZ towaru Q1 / Q2 / Q3 / Q4 (d364) | Miast z NADWYZKA Q1..Q4 (d364) |
|---|---|---|---|
| Ruda (zelazo) | 10.00 / 0.88 / 0.61 / 0.55 / 0.52 | 44 / 19 / 9 / 7 (8) | 5 / 20 / 32 / 42 (42) |
| Drewno | 0.34 / 0.58 / 0.26 / 0.23 / 0.16 | 40 / 16 / 9 / 8 (8) | 7 / 37 / 63 / 70 (73) |
| Skory surowe | 0.37 / 0.98 / 1.24 / 1.53 / 1.51 | 18 / 10 / 12 / 13 (10) | 7 / 8 / 7 / 5 (3) |
| Skora wyprawiona | 10.00 / 1.29 / 1.59 / 1.89 / 1.85 | 33 / 34 / 39 / 43 (42) | 0 (0) |
| Len | 1.07 / 1.24 / 1.30 / 1.40 / 1.44 | 62 / 63 / 64 / 66 (70) | 0 (0) |
| Plotno | 1.01 / 10.00 / 10.00 / 10.00 / 10.00 | 86 / 88 / 90 / 89 (89) | 0 (0) |
| Welna | 1.74 / 1.38 / 1.06 / 0.89 / 0.85 | 34 / 16 / 8 / 7 (3) | 2 / 3 / 5 / 5 (13) |

- Zelazo: naprawione. Progi etap5 R1-8: srednio 11.7 miasta bez zelaza w dobach 91-364 (prog <= 30: TAK) [P].
- Drewno: zalew. Tanieje do 0.16 ceny bazowej.
- Plotno: brak caly rok. 89 z 97 miast bez plotna od doby 30 do konca. To najgorszy niedobor swiata.
- Skora wyprawiona: 42 miasta bez, i jest gorzej niz na poczatku roku (33).
- Narzedzia: zapas swiata 132 -> 2 112 szt. (LOG "Towary: tools", L129041), ale na polkach miast lezy 251, a 1 857 wioza karawany [P]. Ile miast nie ma narzedzi - nie wiem, log tego nie liczy.
- Konie: ile miast nie ma koni - nie wiem, log tego nie liczy. Polki rosna (pkt 2.4).

### 2.2 Zywnosc i towary "lepsze" - ceny

Zrodlo: druga i trzecia czesc linii "Ceny surowcow", d364 = L129060 [P].

| Towar | Indeks Q1 / Q2 / Q3 / Q4 | Pusto w ilu miastach (d364) |
|---|---|---|
| Zboze | 1.01 / 1.10 / 1.32 / 1.47 | - |
| Piwo | 2.94 / 2.86 / 2.97 / 3.10 | - |
| Sol | 9.53 / 10.00 / 9.99 / 10.00 | - (mediana na suficie) |
| Chleb | 4.18 / 4.02 / 4.27 / 4.47 | 16 |
| Pasztet (pie) | 1.95 / 1.81 / 2.04 / 2.13 | 33 |
| Miod pitny | 2.02 / 1.44 / 1.63 / 1.88 | 35 |
| Jajka | 8.66 / 10 / 10 / 10 | 94 |
| Miod, owoce (apple) | 10 / 10 / 10 / 10 | 97 i 97 |
| Garum | 8.35 / 8.78 / 9.21 / 9.60 | 97 |
| Atrament | 6.81 / 7.29 / 7.78 / 8.21 | 97 |
| Barwniki | 6.81 / 7.30 / 7.78 / 8.22 | 97 |
| Klejnoty | 6.84 / 7.11 / 7.39 / 7.64 | 97 |
| Futra | 6.83 / 5.35 / 5.61 / 6.58 | 69 |
| Papirus | 7.62 / 6.80 / 7.70 / 8.52 | 81 |

- Garum, atrament, barwniki i klejnoty: w ksiedze towarow nie maja ani zapasu, ani ruchu (ksiega zna 92 towary, zapas albo ruch ma 48 - L129048; tych czterech wsrod 48 nie ma) [P]. Nikt ich nie produkuje. Lud ma na nie budzet, ale towaru nie ma.
- Miod i owoce sa w swiecie (miod 562 szt., jablka 3 560, L129022 i L129002), ale nie na polkach miast [P].
- Budzet mieszczan na 15 towarow z tej listy: 160 276 d na dobe. Przy dzisiejszych polkach da sie wydac 38 289 d (24%). Reszta, ok. 122 tys. d na dobe, nie ma na co pojsc (L129060; suma z pol "budzet mieszczan" i "przy dzisiejszych polkach") [P, suma S].
- Ceny wozow wsi w miastach (LOG "Towary (utarg wozow)", podzial utargu na towary wedlug ceny pierwszej sztuki - to zawyza duze partie, wiec [S]): zywnosc 2.08 / 2.26 / 2.52 / 2.50 x wartosci, zwierzeta rzezne 0.98 -> 1.09, surowce 1.16 -> 1.13. Ser 9.4 -> 12.2 x, maslo 4.5 -> 4.7 x.
- Indeksu cen dla narzedzi, ceramiki, wina, aksamitu w logu nie ma - nie wiem.

### 2.3 Bron i zbroje

| Co | d1 | d91 | d182 | d273 | d364 | Zrodlo |
|---|---|---|---|---|---|---|
| Bron i zbroje na polkach miast (szt.) | 66 190 | 75 172 | 212 224 | 304 580 | 321 682 | LOG "Rynek broni", L496 -> L128991 [P] |
| - zbroja korpusu | 6 581 | 7 534 | 41 862 | 72 393 | 79 920 | [P] |
| - nogi | 3 349 | 11 347 | 51 936 | 75 669 | 81 588 | [P] |
| - bron jednoreczna | 8 680 | 19 976 | 71 010 | 104 935 | 107 628 | [P] |
| Korpus tier 1-2 na polkach | 6 338 | 6 858 | 40 223 | 65 860 | 70 723 | LOG "Zbroja na polkach", L678 -> L129174 [P] |
| Korpus tier 3-4 na polkach | 194 | 142 | 105 | 201 | 129 | [P] |
| Korpus tier 5-6 na polkach | 45 | 63 | 79 | 93 | 98 | [P] |

- Polki pelne zlomu, a dobrej zbroi brak. Doba 364: zbroja korpusu tier 3 - popyt 3 004, polka 71, zrobiono tego dnia 36. Tier 4 - popyt 2 182, polka 41, zrobiono 9. Helm tier 3 - popyt 1 756, polka 212 (warsztaty.log linia 1092) [P].
- Skad zlom: zolnierze sprzedaja lup. Doba 364: 3 485 szt. za 5 279 zl = 7.4% wartosci (LOG "Skup sprzetu (doba)", L129193) [P].
- Ujscie: skup na zlom 1 276 szt. na dobe; nadmiar swiata ponad rok popytu ok. 127 701 szt. (L129192) [P]. Przyrost polek zwalnia: +92 tys. w Q3, +17 tys. w Q4.
- Caly sprzet swiata: 1 348 711 szt., z tego 1 034 658 w zbrojowniach wojska (LOG "Uzbrojenie wedlug zrodla", L129220) [P].
- Zamowienia wojska czekajace na towar: 43 994 szt. (doba 30) -> 14 740 (doba 364) (handel.log linie 31 i 365) [P]. Kolejka maleje.

### 2.4 Konie

Zrodlo: LOG "Konie (E6/0)", L709 -> L129221 [P]. Ceny = srednia zaplacona przez mieszczan miast.

| Kategoria | Cena Q1 / Q2 / Q3 / Q4 (zl) | Na polkach d1 -> d364 | W swiecie d1 -> d364 |
|---|---|---|---|
| Zwykly | 394 / 347 / 348 / 340 | 1 616 -> 7 223 | 9 352 -> 19 522 |
| Bojowy | 3 226 / 3 002 / 2 903 / 2 708 | 116 -> 1 863 | 3 210 -> 9 205 |
| Szlachetny | 8 898 / 8 867 / 9 190 / 9 243 | 5 -> 337 | - |
| Juczny | 86 / 70 / 73 / 71 | 308 -> 4 288 | 7 342 -> 35 108 |

- Koni przybywa, ceny lekko spadaja. To sie zgadza.
- Ale: konie leza na polkach, a 5 540 zolnierzy czeka na konia (progi etap5 K3-10) i jazda ma 63% celu (K3-3: NIE) [P]. Hamulec to siodla i cena, nie brak koni (opis w tych samych wierszach progow).

### 2.5 Zapasy swiata - najwieksze nadwyzki i niedobory na koniec roku

Zrodlo: LOG linie "Towary: <towar>", doba 364 (L128999-L129046) [P]. "Dni" = zapas / srednie zuzycie z ostatnich 28 dob [S].

| NADWYZKI | Zapas d1 -> d364 | Dni zuzycia | NIEDOBORY | Zapas d1 -> d364 | Dni zuzycia |
|---|---|---|---|---|---|
| Drewno | 11 792 -> 152 862 | 102 | Plotno | 359 -> 34 | 0 |
| Bawelna | 1 604 -> 5 565 | 105 | Skora | 96 -> 263 | 2 |
| Wapien | 1 435 -> 7 154 | 82 | Ceramika | 27 -> 157 | 2 |
| Glina | 2 239 -> 15 905 | 49 | Wino | 217 -> 258 | 4 |
| Srebro | 1 147 -> 5 074 | 67 | Piwo | 2 389 -> 3 085 | 4 |
| Ruda | 1 151 -> 8 857 | 34 | Sol | 3 151 -> 2 859 | 5 |
| Zboze | 105 162 -> 150 637 | 17 | Chleb | 2 630 -> 5 259 | 5 |
| Ryby | 49 423 -> 103 712 | 37 | Mieso | 1 763 -> 3 025 | 6 |

Zapasy, ktore topnieja caly rok [P]: maslo 30 117 -> 5 728, ser 27 960 -> 4 567, jajka 12 868 -> 1 834, winogrona 20 647 -> 11 927, oliwki 18 677 -> 11 670. W Q1 zuzycie masla bylo 499 na dobe przy produkcji 344; w Q4 juz 311 przy 318. Czyli spadek sie zatrzymuje, bo zuzycie spadlo, a nie bo przybylo towaru [P]. Moje wyjasnienie: lud kupuje mniej, bo drogo (ser 9-12 x wartosci) [S].

### 2.6 Bilans towarow - co powstaje i znika poza produkcja i zuzyciem

Zrodlo: LOG "Towary (bilans)", 364 linie, d364 = L129048 [P].

| Kwartal | Produkcja szt./d | Zuzycie szt./d | Zmiana zapasu /d | Bez wyjasnienia /d |
|---|---|---|---|---|
| Q2 | 24 075 | 23 928 | +206 | +60 |
| Q3 | 23 053 | 22 767 | +331 | +46 |
| Q4 | 22 867 | 22 377 | +544 | +54 |

- Rok: produkcja 9.04 mln szt., zuzycie 8.36 mln, bez wyjasnienia +19 397 szt. = 0.2% produkcji [P]. Ksiega sie zamyka.
- Q1 pomijam w tabeli: pierwsze doby maja wpisy rozruchu i rownanie tam sie nie sklada z samych trzech liczb. Nie rozbieralem tego.
- Kontrola z osobna ksiega rudy i drewna: zgodna w 363 z 364 dob [P].
- Co jest w "bez wyjasnienia" wedlug towaru (rok, linie "Towary:") [P]: zboze +65 002 (ok. +179/d), ryby +59 623 (ok. +164/d) - powstaja bez wpisu. Za to 16 lepszych rodzajow jedzenia (mieso, ser, maslo, jajka, miod, owoce, oliwki, winogrona, chleb, pasztet, piwo...) traci po ok. 6-7 tys. szt. kazdy, czyli po ok. 17-20 szt. na dobe kazdy. Razem to wyglada na jedna nieopisana regule, ktora zamienia lepsze jedzenie na zboze i ryby. Ktora - nie wiem.
- Partie zniszczone z towarem: 4 994 w roku [P] - towar przechodzi do zwyciezcy albo przepada wedlug osobnych linii (to nie moj rozdzial).

## 3. Produkcja

### 3.1 Wsie - plony i wydobycie

Zrodlo: LOG linie "Towary:", pole "produkcja [wsie N ...]", srednia na dobe kwartalami [P].

| Towar (produkcja wsi na dobe) | Q1 | Q2 | Q3 | Q4 |
|---|---|---|---|---|
| Zboze | 9 116 | 8 896 | 8 613 | 8 641 |
| Ryby | 2 413 | 2 349 | 2 331 | 2 332 |
| Sol | 475 | 527 | 533 | 509 |
| Ruda (lupy z kopaln) | 227 | 259 | 258 | 254 |
| Len | 420 | 517 | 497 | 501 |
| Welna | 272 | 266 | 254 | 308 |
| Maslo | 301 | 293 | 286 | 289 |
| Ser | 389 | 382 | 367 | 372 |

- Zboze: produkcja -5% w roku, a zuzycie w Q4 (8 991/d) jest wyzsze niz produkcja (8 768/d razem z innymi zrodlami) [P]. Zapas zboza spada od doby 91 (168 877 -> 150 637).
- Wioski zniszczone wojna (LOG "Ksiega wiosek", L482 -> L128977) [P]: puste osady 0 -> 824, wiosek dotknietych 249, spalonych 27. Uchodzcy: 187 910 (CSV ludzie-regiony.csv, suma kolumny uchodzcy, doba 364) [P].
- Zatkane magazyny wsi (pelny magazyn, woz nie wywozi): 13 / 28 / 41 / 36 z 571, doba 364: 37, z tego 22 spladrowane (LOG "Zatkane wsie (diagnoza)", L129065) [P].
- Kopalnie: 5.7% lup nie wytopiono z braku wegla (progi etap5 R1-7: NIE, prog 5%). Dwie kopalnie stale bez zlota na drewno: Harridan Hall i Kenning (R1-13, R1-18) [P].

### 3.2 Warsztaty zbrojne (rzemieslnicy miast)

Zrodlo: LOG "Warsztaty", d364 = L128929 [P].

| Co | Q1 | Q2 | Q3 | Q4 |
|---|---|---|---|---|
| Wykonano sztuk na dobe | 1 453 | 1 409 | 1 285 | 1 212 |
| Cykle stracone: brak surowca | 944 | 439 | 208 | 176 |
| - brak rudy | 551 | 235 | 74 | 45 |
| - brak drewna | 393 | 152 | 67 | 45 |
| - brak skory | 100 | 98 | 85 | 96 |
| Cykle stracone: bez zysku | 223 | 378 | 476 | 549 |
| Cykle stracone: brak zlota albo kupca | 129 | 212 | 251 | 308 |
| Rece bezczynne | 5.6% | 3.0% | 2.2% | 2.3% |

- Surowiec przestal byc hamulcem (ruda 551 -> 45). Skora zostala (ok. 96 na dobe, bez poprawy).
- Rosna dwa inne hamulce: "bez zysku" (x2.5) i "brak zlota albo kupca" (x2.4).
- Rece sa zajete w 98%, a mimo to dobrej zbroi robi sie kilkadziesiat sztuk dziennie przy brakach rzedu tysiecy (pkt 2.3).

### 3.3 Warsztaty towarowe (291 sztuk)

Zrodlo: LOG "Warsztaty towarowe", d364 = L128955 [P].

- Pracuje: 215 / 218 / 214 / 211 z 291 (72-75%). Stoi ok. 80.
- Wynik wszystkich razem: ok. +10 tys. zl na dobe, bez zmian w roku. Bankructw 6 (wszystkie w Q4).
- Miasta zatrzymuja w kasach nadwyzke ceny wyrobow ponad koszt: 109 tys. / 102 tys. / 108 tys. / 104 tys. zl na dobe. Warsztaty dostaja ok. 41-44 tys.

| Typ | Ile | Pracuje Q1 -> Q4 | Zysk na warsztat na dobe Q1 -> Q4 | Uwaga |
|---|---|---|---|---|
| Kopalnie BK w miastach | 12 | 2% -> 0% | -6.6 -> -4.6 | stoja caly rok |
| Wytworca strzal (fletcher) | 7 | 78% -> 35% | +5.5 -> -3.7 | gasnie |
| Zbroje konskie | 5 | 19% -> 13% | +0.5 -> +6.2 | prawie stoi |
| Platnerz broni (weaponsmithy) | 9 | 42% -> 52% | +33.3 -> +3.2 | zysk znika |
| Tartak (wood) | 29 | 75% -> 60% | +22.9 -> +7.6 | zalew drewna |
| Browar | 12 | 75% -> 62% | +2.5 -> +0.5 | |
| Tlocznia wina | 14 | 60% -> 58% | +1.6 -> +1.7 | |
| Garbarnia | 11 | 48% -> 60% | +8.7 -> +11.5 | za malo skor |
| Tkalnia lnu | 15 | 75% -> 76% | ok. +69 | za malo lnu |
| Garncarz | 2 | 27% -> 60% | -2.4 -> -0.7 | tylko 2 w swiecie |
| Piekarnia | 72 | 94% -> 92% | +11.2 -> +12.9 | dziala |
| Rzeznik | 17 | 100% | +19.1 -> +16.6 | dziala |
| Kowal (smithy) | 23 | 89% -> 97% | +80.4 -> +72.9 | dziala |
| Tkalnia welny | 27 | 84% -> 85% | +107 -> +84 | dziala |
| Tkalnia aksamitu | 8 | 58% -> 62% | +285 -> +371 | dziala |

- Kolumna "Ile" to srednia z Q4 (liczba warsztatow danego typu zmienia sie w roku). Kolumna "Uwaga" to moja ocena [S].
- Dlaczego stoi przerob lnu: doba 364, w 72 miastach "brak surowca" dla plotna (LOG "Rzemioslo miasta (148)", L128928) [P]. Len jest w 27 miastach, plotno robi 23.
- W tym samym przerobie rece miast sa zajete w 17% (2 514 z 15 166 roboczodni) [P]. Rak jest dosc, brakuje lnu i skor.

### 3.4 Budowy

Zrodlo: LOG "Budowy" (L128995), "Budowy oplacone" (L128994), budowy.log (82 073 linie) [P].

| Co | Q1 | Q2 | Q3 | Q4 | doba 364 |
|---|---|---|---|---|---|
| Osad, gdzie budowa idzie | 58 | 49 | 36 | 29 | 29 z 227 |
| Osad, gdzie budowa stoi | 168 | 176 | 189 | 197 | 198 z 227 |
| Wstrzymane: wojna (budowle cywilne) | 130 | 162 | 177 | 186 | 189 |
| Wstrzymane: brak materialow | 37 | 6 | 3 | 2 | 0 |
| Wydane na budowy, zl na dobe | 17 408 | 13 614 | 9 944 | 8 520 | 9 180 |

- Ukonczonych poziomow w roku: 63 (kwartalami 6 / 22 / 22 / 13). W 227 osadach.
- Doba 364: 144 osady stoja co najmniej 300 dni, mediana stania 342 dni (budowy.log, ostatni dzien) [P].
- Co stoi: Armory 19, Court House 19, Mason 18, Retinue Barracks 15, Warhorse Studs 14, Mines 13. Co idzie: Fortifications 12, Barracks 7, Training Fields 4.
- Powod: budowle cywilne stoja w wojnie, a w wojnie jest srednio 26 krolestw (progi etap2). Material nie jest juz przeszkoda.
- Rok kosztowal 4.50 mln zl [P]. Wydatek spada o polowe.

## 4. Handel

### 4.1 Karawany

| Co | d1 | d91 | d182 | d273 | d364 | Zrodlo |
|---|---|---|---|---|---|---|
| Karawany w swiecie | 416 | 627 | 604 | 577 | 556 | LOG "Karawany (stan)", L584 -> L129083 [P] |
| Karawany, ktorym kasa nie starcza na zold | 7 | 62 | 147 | 184 | 177 | LOG "Obieg: notable i karawany", L1092 -> L129216 [P] |
| Doplata notabli do zoldu, zl na dobe | 0 | 12 113 | 34 666 | 46 544 | 49 479 | ta sama linia [P] |
| Kiesy notabli razem | 30.5 mln | 27.0 mln | 24.7 mln | 23.1 mln | 21.8 mln | ta sama linia [P] |

- Nowych karawan w roku: 1 733, kapital 12.81 mln zl (ta sama linia, suma) [P].
- Pokonanych karawan w bitwach: 554, z tego 512 przez lordow (wojna) i 38 przez bandy (bitwy.log, strona "rodzaj=karawana" przegrywa) [P].
- Zold karawan: 157 tys. zl na dobe. Jedna trzecia karawan (177 z 535) nie zarabia na wlasny zold [P].
- Kiesy karawan: 4.62 mln (doba 30) -> 2.98 mln (doba 364) (LOG "Pieniadz swiata") [P].
- Zysk karawan z handlu wprost - nie wiem, log nie ma takiej liczby. Wiem tylko, ze oczekiwany zysk z samych surowcow to 852 tys. d w roku (LOG "Karawany", suma) [P] - to ok. 2.3 tys. na dobe przy zoldzie 157 tys.
- Karawany dowiozly miastom z brakiem: rude 10 144, drewno 6 333, skory surowe 6 064, welne 4 538 szt. w roku [P].

### 4.2 Wozy surowcow rundy 3 - CZY DOWOZA ZELAZO: TAK

Zrodlo: LOG "Kontrakty surowca (174)", czesc "wozem kupcow (P3/Z1)", 364 linie, d364 = L129190 [P].

| Co | Rok | Na dobe |
|---|---|---|
| Wozow wyslanych | 7 252 | 19.9 |
| Ruda dowieziona wozem | 42 101 szt. | 115.7 |
| Welna | 25 591 | 70.3 |
| Skory surowe | 15 041 | 41.3 |
| Drewno | 10 822 | 29.7 |
| Len | 7 976 | 21.9 |
| Skora wyprawiona, plotno | 0 i 0 | brak zrodla z nadwyzka |
| Zaplacone (kasa miasta-celu -> kasa zrodla) | 1.95 mln zl | 5.3 tys. |
| Dojechalo / zawrocone | 7 102 wozy, 99 512 szt. / 0 | - |

- Udzial wozow w dowozie rudy do miast: 42 101 z 54 416 = 77% [S] (reszta: karawany 10 144, kontrakty karawan 2 171).
- Kontrakty karawan (stara droga): 302 w roku, czyli 0.8 na dobe, dojechalo 270 [P]. To droga prawie martwa.
- Woz nie pomaga na skore i plotno, bo nigdzie nie ma nadwyzki do kupienia: doba 364 - skora 82 miasta, plotno 97 miast "brak zrodla z nadwyzka w zasiegu" (L129190) [P].
- Wozy dymarek (drewno do kopaln): 186 689 ladunkow w roku (progi etap5 R1-7e) [P].

### 4.3 Wozy wsi

- Sprzedazy wozow wsi: 121 / 117 / 111 / 112 na dobe. Utarg: 352 tys. / 303 tys. / 301 tys. / 311 tys. zl na dobe (LOG "Dowoz (wozy)", L129078) [P].
- Rozbitych wozow wsi w roku: 3 972, z tego 3 014 przez bandy. Rosnie: 7.3 / 10.5 / 12.1 / 13.8 na dobe (LOG "Dowoz", L129063) [P].
- Bandy: 5 694 -> 17 279 ludzi (CSV ludzie-regiony.csv, bandy_ludzie) [P]. Trzy razy wiecej bandytow, dwa razy wiecej rozbitych wozow.
- Towar wsi niesprzedany w miastach: 0.181% (progi etap5 A1-T19: NIE, prog 0.1%) [P]. Male.

### 4.4 Kasy kupcow miast

Zrodlo: CSV kasy-miast.csv (97 miast, 364 doby), kolumny kasa i zapas_kupcow [P]. "Zapas kupcow" = minimum, ktore kupcy trzymaja (10 000 + 12 x dobrobyt).

| Doba | Suma kas | Mediana | Najbiedniejsza | Miast ponizej zapasu | Kasy < 1 000 |
|---|---|---|---|---|---|
| 1 | 27.67 mln | 262 590 | 106 651 | 0 | 0 |
| 30 | 10.82 mln | 107 809 | 49 017 | 4 | 0 |
| 91 | 9.38 mln | 89 873 | 35 175 | 9 | 0 |
| 182 | 9.88 mln | 91 546 | 17 919 | 11 | 0 |
| 273 | 10.93 mln | 97 743 | 2 816 | 15 | 0 |
| 364 | 10.93 mln | 98 963 | 3 173 | 17 | 0 |

- Spadek z doby 1 na 30 to zaplanowany dar startowy (progi etap5 A1-T12: 13.99 mln do podzialu). Potem kasy stoja na 9.4-11 mln.
- Miast ponizej zapasu, srednio: 7.2 / 10.5 / 13.0 / 16.1. Rosnie. W roku 53 miasta byly ponizej co najmniej 28 dob, 12 miast co najmniej 91 dob.
- Kasy ponizej 1 000 zl: 38 dob w roku, najwyzej 2 miasta naraz (progi etap5 C1-3: NIE). To Thenn (30 dob, najmniej 7 zl w dobie 321) i Frostfang's Camp (9 dob, najmniej 131 zl w dobie 360) - oba Wolni Ludzie [P].
- Thenn: 331 dob ponizej zapasu, kasa 66 958 (doba 30) -> 5 278 (doba 364). Frostfang's Camp: 84 205 (doba 273) -> 3 173 (doba 364). Dlaczego akurat one - nie wiem.
- Zawor kas (nadwyzka ponad zapas do pana i korony) w roku: panom 52.9 mln + koronie 44.1 mln = 97.1 mln zl [P]. W Q4: panom 138 tys., koronie 113 tys. na dobe.

### 4.5 handel.log i zakupy.log

- handel.log (365 linii) to zamowienia uzbrojenia - opisane w pkt 2.3.
- zakupy.log (13 644 linie) to zakupy sprzetu lordow. To rozdzial o wojsku - nie analizowalem.
- Wozy kupcow z uzbrojeniem miedzy miastami: 842 576 szt. w roku za 106.2 mln zl (LOG "Przewoz kupcow wozem (215)", suma) [P]. To przelewy miedzy kasami miast, nie nowy pieniadz.

## 5. Ludzie

### 5.1 Ludnosc

Zrodlo: CSV ludzie-regiony.csv (227 regionow), doba 1 i 364 [P].

| Co | Doba 1 | Doba 364 | Zmiana |
|---|---|---|---|
| Ludzie razem | 51.99 mln | 51.47 mln | -0.99% |
| Ludzie miast | 10.47 mln | 10.36 mln | -1.07% |
| Ludzie wsi | 41.52 mln | 41.11 mln | -0.97% |
| Uchodzcy | 0 | 187 910 | - |
| Zalogi | 47 356 | 83 593 | +77% |
| Milicja | 62 322 | 73 621 | +18% |
| Bandyci | 5 694 | 17 279 | x3.0 |
| Pula wyrzutkow | 820 | 11 869 | x14 |

- Regionow ze spadkiem ludzi ponad 5%: 15. Najgorzej: Sharp Point -32.8%, Rathylar -25.6%, Rosby -22.1%, Sweetport -18.7%.
- Krolestwa (wedlug stanu z doby 1): Dragonstone -7.6%, Sarnor -6.6%, Wolni Ludzie -4.2%.
- Wojna kosztuje 1% ludzi rocznie. To jest liczba z sensem.

### 5.2 Dobrobyt i zadowolenie

- Dobrobyt miast: 464 917 -> 624 706 = +34.4% w roku. Rosnie w 95 z 97 miast (CSV kasy-miast.csv) [P]. Najwiecej: Winterfell +4 009, King's Landing +3 072. Spada tylko Harrenhal -705 i Starfall -421.
- Dobrobyt zamkow: 135 559 -> 280 200 = +107% (CSV ludnosc-bk.csv, kolumna dobrobyt_lub_hearth) [P].
- Ludnosc w rachunku moda BannerKings (CSV ludnosc-bk.csv, razem_bk) [P]: miasta -3.2%, wsie -18.4% (spada w 476 z 571 wsi), zamki +45.1%.
- Zadowolenie BK w miastach (0 do 1), Q1 -> Q4: jedzenie 0.83 -> 1.00, wyroby 0.63 -> 0.96, luksus 0.54 -> 0.85 (LOG "Zadowolenie BK", L129248) [P]. Rosnie.
- Zadowolenie BK w zamkach: wyroby 0.50 i luksus 0.50 przez caly rok, bez ruchu [P]. Zamki nie dostaja wyrobow.
- Lojalnosc miast: w logu jej nie ma. Nie wiem.

### 5.3 Kiesy ludu (pieniadze mieszczan)

Zrodlo: LOG "Kiesa ludu", L732 -> L129238 [P].

| Co | d1 | d91 | d182 | d273 | d364 |
|---|---|---|---|---|---|
| Kiesy ludu miast razem | 6.20 mln | 19.04 mln | 29.57 mln | 37.23 mln | 44.03 mln |
| - w tym ponad zapas (lokata) | 0 | 7.64 mln | 16.24 mln | 23.27 mln | 29.49 mln |
| Przyrost na dobe (srednia kwartalu) | - | 169 tys. (Q1) | 126 tys. (Q2) | 84 tys. (Q3) | 68 tys. (Q4) |
| Kiesa podzamczy | 0.64 mln | 3.06 mln | 4.01 mln | 3.95 mln | 3.31 mln |

Rozklad miedzy miastami (CSV kiesa-ludu.csv, ostatnia probka) [P]:
- 10 najbogatszych miast ma 36% pieniedzy ludu. 31 najbiedniejszych ma 1.5%.
- Najbogatsze: Riverrun 2.50 mln, Vaes Dothrak 2.17 mln, Winterfell 1.73 mln. Najbiedniejsze: Thenn 4 363 zl.
- Kiesa liczona w dniach wydatkow: mediana 41 dni. Ponizej 7 dni: 13 miast. Ponizej 14 dni: 33 miasta. Ponad 120 dni: 33 miasta.
- Od doby ok. 92 do konca: kiesa rosnie w 56 miastach, spada w 41.
- W ostatniej probce dochod ludu jest nizszy niz wydatki w 42 miastach z 97.
- Miast biednych (pelny koszyk < 95%): 14. W 9 z nich lud zarabia mniej, niz wydaje.

Skad lud ma dochod (doba 364, L129238) [P]: z zewnatrz i z kas 221 781 zl, z tego wojsko 140 803 (63%), place warsztatow 45 809, dwor 20 419, budowy 4 170. Czyli lud zyje z zolnierzy. Gdzie armia nie zaglada (wyspy), tam lud biednieje.

### 5.4 Wsie

- Kiesy wsi: 1.07 mln (doba 1) -> 9.24 mln (doba 91) -> 9.55 mln (doba 363). Od Q2 stoja (LOG "Przeplywy osad (kiesy wsi)", L129213) [P].
- Wsi z kiesa ponizej 1 000 zl: 47 -> 73 z 571 [P].
- Z gotowki, ktora wplywa do wsi, korona bierze: 34.5% / 58.7% / 61.4% / 58.5% (doba 363: 192 001 zl = 65.1%); pan 32.3%; majatki 7%; wsi zostaje -4.4% (LOG "Gotowka wsi (E5/0)", L129218) [P].
- Zakupy wsi w miastach: 12-14% potrzeby. 478 wsi nie ma czego kupic w swoim miescie (L129238) [P].

## 6. Czy gospodarka trzyma sens

**Czy ceny odpowiadaja niedoborom - TAK.** Dla wszystkich 7 surowcow kierunek sie zgadza (pkt 2.1): ruda taniala, gdy znikaly miasta bez rudy; drewno tanieje przy zalewie; plotno stoi na suficie, bo go nie ma; skora drozeje, bo miast bez skory przybywa. Zboze drozeje od doby 91, bo zapas spada. Konie tanieja, bo polki rosna.

**Czy bogate miasta sa bogate z powodu - CZESCIOWO.**
- Kasa kupcow idzie w parze z dobrobytem slabo: korelacja rang 0.45 (kasa d364 wobec dobrobytu d364; CSV kasy-miast.csv) [S].
- Kasa wobec wlasnego zapasu nie zalezy od dobrobytu wcale: korelacja 0.02 [S]. Powod: zawor zdejmuje wszystko ponad zapas, wiec kazda kasa stoi tuz nad swoim minimum (mediana kasa/zapas 1.09, L129149).
- Pieniadze ludu sa tam, gdzie chodzi wojsko (Riverrun, Winterfell, Storm's End). To ma powod.

**Czy biedne maja szanse sie podniesc - SLABO.**
- 31 miast z najnizsza kasa/zapas na poczatku (doby 30-91): w Q4 dalej w dolnej 31 jest 15. Srednia tej grupy 1.09 -> 1.11. Poprawilo sie 6, pogorszylo 8, bez zmian 17 (CSV kasy-miast.csv) [S]. Ruch jest, ale grupa jako calosc stoi.
- Lud: 41 miast ma kiese malejaca od doby 92. 14 miast biednych nie ma skad wziac dochodu.

**Co stoi w miejscu caly rok.**
- Plotno: 86-90 miast bez, 364 doby.
- Zadowolenie zamkow z wyrobow i luksusu: 0.50 przez 364 doby.
- Kopalnie BK w miastach: 0% pracuje.
- Budowy cywilne: 144 osady stoja ponad 300 dni.
- Zbroja tier 3-4 na polkach: 105-201 sztuk przy brakach 2-3 tys.

**Glowna rzecz, ktora sie nie spina: pieniadz wojny osiada u ludu i nie wraca.**

Kto ma zloto (LOG "Pieniadz swiata", L10804 i L129205) [P]:

| Kto | Doba 30 | Doba 364 | Zmiana | Na dobe [S] |
|---|---|---|---|---|
| Skarbce krolestw | 54.57 mln | 27.11 mln | -27.46 mln | -82 tys. |
| Glowy rodow | 60.71 mln | 47.23 mln | -13.48 mln | -40 tys. |
| Notable | 28.86 mln | 21.85 mln | -7.01 mln | -21 tys. |
| Karawany | 4.62 mln | 2.98 mln | -1.64 mln | -5 tys. |
| Lud miast (kiesy) | 11.03 mln | 44.12 mln | +33.09 mln | +99 tys. |
| Sakiewki zolnierzy | 4.66 mln | 16.59 mln | +11.93 mln | +36 tys. |
| Kiesy wsi | 6.34 mln | 9.55 mln | +3.20 mln | +10 tys. |
| Kasy zamkow | 3.68 mln | 5.41 mln | +1.72 mln | +5 tys. |
| Kasy miast | 10.85 mln | 10.98 mln | +0.13 mln | 0 |
| RAZEM SWIAT | 207.93 mln | 208.34 mln | +0.41 mln | +1 tys. |

Prosto: korony i panowie placa zold. Zolnierz wydaje go w miescie. Lud dostaje pieniadz, ale polki z tym, co chce kupic, sa puste (pkt 2.2: 122 tys. d dziennie bez towaru). Wiec lud odklada. Do korony wraca z tego ok. 15 tys. dziennie podatku wojennego. Reszta stoi.

## 7. Porownanie z poprzednim biegiem rocznym (WGRANIE 34, bez rundy 3)

Te same skrypty na LOG-S i na tabelach z ...\Logs\2026-10-13_02-15-21\ [P].

| Co | Poprzedni bieg | Ten bieg | Ocena |
|---|---|---|---|
| Miast bez zelaza, srednio doby 91-364 (progi R1-8) | 41.4 | 11.7 | duzo lepiej |
| Miast bez rudy w dobie 364 (linia Ceny surowcow) | 55 | 8 | duzo lepiej |
| Indeks ceny rudy, doba 364 | 2.34 | 0.52 | duzo lepiej |
| Zuzycie rudy w roku | 64 597 | 86 439 | +34% |
| Produkcja rudy w roku | 79 553 | 94 417 | +19% |
| Zapas rudy swiata, doba 364 | 14 517 | 8 857 | mniej lezy, wiecej pracuje |
| Cykle warsztatow stracone z braku rudy, Q4 | 486 | 45 | duzo lepiej |
| Sztuk z warsztatow zbrojnych na dobe, Q4 | 1 043 | 1 212 | +16% |
| Miast bez welny, doba 364 | 35 | 3 | duzo lepiej |
| Miast bez skor surowych, doba 364 | 21 | 10 | lepiej |
| Narzedzia - produkcja w roku | 36 194 | 53 122 | +47% |
| Zapas drewna, doba 364 | 151 306 | 152 862 | bez zmian (zalew) |
| Suma kas miast, doba 364 | 10.83 mln | 10.93 mln | bez zmian |
| Miast ponizej zapasu kupcow, doba 364 | 19 | 17 | bez zmian |
| Dob z kasa miasta < 1 000 zl | 1 | 38 | gorzej (Thenn, Frostfang) |
| Miast, gdzie ludu nie stac na jedzenie, Q4 | 13.3 | 9.1 | lepiej |
| Zjedzone bez zaplaty, Q4 | 2.90% | 2.25% | lepiej, dalej ponad prog 1% |
| Zamkow bez kredytu dworu, doba 364 | 45 | 26 | lepiej |
| Kiesy ludu miast, doba 364 | 36.5 mln | 44.0 mln | wiecej stoi u ludu |
| Glowy rodow, doba 364 | 56.7 mln | 47.2 mln | panowie biedniejsi o 9.4 mln |
| Karawany, doba 364 | 439 | 556 | wiecej |
| Karawany bez pieniedzy na zold, doba 363 | 123 | 177 | gorzej |
| Bron i zbroje na polkach, doba 364 | 316 545 | 321 682 | bez zmian (zalew) |
| Zamowienia wojska czekajace, doba 364 | 19 539 | 14 740 | lepiej |
| Bankructwa warsztatow | 11 | 6 | lepiej |
| Towary bez wyjasnienia w roku | -296 183 | +19 397 | ksiega szczelna |
| Zloto swiata, doba 30 -> 364 | 208.18 -> 206.03 mln | 207.93 -> 208.34 mln | przestalo ubywac |

- Woz z surowcem (runda 3) w poprzednim biegu nie istnial (0 linii z "wozem kupcow (P3/Z1)" w LOG-S). To on zrobil roznice w zelazie.
- Test 120 dob (progi-fin4) pokazal 25.9 miasta bez zelaza w dobach 91-120. Rok pokazuje, ze potem jest jeszcze lepiej (Q3: 9, Q4: 7).
- Test 120 dob NIE widzial zalewu drewna (+15 ladunkow na dobe). W roku jest +426 na dobe (progi etap5 R1-9: NIE). Zalew zaczyna sie po dobie 91: zapas 17 186 (doba 91) -> 48 249 (doba 182).

## 8. Dane do pytania Jeffa: gdzie podniesc podatki na czas wojny

Jeff zapytal, gdzie zwiekszyc podatki, skoro 85 tys. wojska nie wychodzi bez doplat. Ponizej to, co widac z mojej dziedziny. Decyzja i reszta rachunku (wojsko, skarbce) naleza do innych rozdzialow.

Ile kosztuje zolnierz: zold partii rodow 443 964 zl na dobe na ok. 74 tys. ludzi = ok. 6.0 zl na zolnierza na dobe (LOG "Zold", L129132; "Odziez wojska", L129133) [S]. Czyli 10 tys. wojska wiecej = ok. 60 tys. zl na dobe [S].

### 8.1 Gdzie pieniadz jest i rosnie

| Zrodlo | Ile lezy | Ile przybywa | Ile dzis placi koronie | Zrodlo danych |
|---|---|---|---|---|
| Lud miast - nadwyzka ponad zapas (lokata) | 29.49 mln | 68 tys./d w Q4 (99 tys./d srednio w roku) | podatek wojenny 14 908 zl/d przy pulapie 3% | L129238, L129100 [P] |
| Sakiewki zolnierzy | 16.59 mln razem; w partiach rodow 11.04 mln = 24.9 dnia zoldu (cel 15 dni) | 36 tys./d | 0 | L129205, L129140 [P] |

- Podatek wojenny ludu dzis: pulap 3% (6% tylko z polityka War Tax). Na pulapie stoi 20 z 26 krolestw w wojnie. Krolestwa byly na pulapie przez 302 doby z 364 (L129100; progi etap5 B2-9, B2-11) [P]. Pulap po prostu trzyma.
- Wszystkie daniny ludu miast razem: prawa panskie 10 992 + dziesiecina 10 972 + podatek wojenny 13 677 + akcyza 9 371 = ok. 45 tys. zl na dobe (L129240, L129243, L129100, L129238) przy dochodzie 519 tys. (CSV kiesa-ludu.csv, suma kolumny dochod, ostatnia probka) = ok. 8.7% [S].

### 8.2 Propozycja z liczbami [S]

1. **Podatek wojenny ludu liczyc od nadwyzki (lokaty), nie od dochodu, i podniesc pulap.** Bierze tylko z kiesy ponad zapas dni. Miasta biedne (33 miasta z kiesa ponizej 14 dni) nie placa nic. Gdyby korona brala w wojnie tyle, ile ludowi i tak przybywa (68 tys. zl na dobe w Q4), kiesy ludu przestana rosnac, ale nie spadna. To daje ok. 53 tys. zl na dobe ponad dzisiejsze 15 tys., czyli zold ok. 9 tys. zolnierzy. Lokata 29.5 mln to do tego zapas na ok. 300 dob dodatkowych 100 tys. dziennie.
2. **Zold w wojnie krotszy o to, czego zolnierz i tak nie wydaje.** Sakiewki rosna o 36 tys. zl na dobe = ok. 6% zoldu wplacanego do sakiewek (601 572 zl na dobe, LOG "Sakiewka ludzi", L128938). Ten sam budzet panow utrzyma wtedy ok. 6 tys. ludzi wiecej. Ryzyko: morale i dezercja - to nie moja dziedzina, trzeba sprawdzic w rozdziale o wojsku.
3. Oba razem: ok. 89 tys. zl na dobe = ok. 15 tys. zolnierzy. To wiecej niz brakuje do 85 tys. (ostatnia doba 73.6 tys., progi etap2).

Dlaczego to jest logiczne w zamknietym obiegu: to sa dokladnie te dwa miejsca, do ktorych splywa zold (tabela w pkt 6). Podatek stamtad zamyka kolo: korona placi -> zolnierz wydaje -> lud odklada -> korona bierze z odlozonego.

### 8.3 Gdzie NIE podnosic - i dlaczego

| Miejsce | Dlaczego nie | Dowod |
|---|---|---|
| Wsie | Korona bierze juz 59-65% gotowki wsi, pan 32%. Wsi nie zostaje nic (-4.4%). 73 wsie maja kiese < 1 000. | L129218, L129213 [P] |
| Kasy kupcow miast | 17 miast juz ponizej zapasu i przybywa. Zawor i tak oddaje panom i koronie wszystko ponad zapas (251 tys./d w Q4). Wyzsza stawka da jednorazowo 2.75 mln, potem nic. | CSV kasy-miast.csv; L129149 [P] |
| Notable i karawany | Notable traca 21 tys. zl na dobe, 177 karawan nie zarabia na zold. | L129216 [P] |
| Podzamcza | Zjedzone bez zaplaty rosnie 12 razy w roku (667 -> 8 140 zl/d), 26 zamkow bez kredytu. | L129238 [P] |
| Budowy | Juz stoja w wojnie (189 osad). Wydatek spadl do 8.5 tys./d. Nie ma czego ciac. | L128994 [P] |

Uwaga do pkt 1: prawdziwa przyczyna lokaty to puste polki (122 tys. d dziennie bez towaru). Podatek leczy skutek. Gdyby kiedys doszla produkcja plotna, skory, soli, jajek i towarow luksusowych, lud zaczalby wydawac i baza podatku by zmalala.

## 9. Czego nie wiem

- Lojalnosc miast - nie ma jej w logu.
- Ile miast nie ma narzedzi, odziezy i koni - log liczy to tylko dla 7 surowcow.
- Indeks cen narzedzi, ceramiki, wina i aksamitu - linia cen ich nie obejmuje.
- Ile wozy wsi naprawde dostaja za drewno. Linia "Towary (utarg wozow)" dzieli utarg wedlug ceny pierwszej sztuki i pokazuje drewno po 12.9 d za ladunek przy cenie miasta 1-3.5 d. To prawie na pewno blad podzialu, nie prawdziwa cena.
- Ile karawany zarabiaja na handlu (jest tylko zold i doplaty notabli).
- Dlaczego kasy Thenn i Frostfang's Camp spadly do zera.
- Co zamienia ok. 300 szt. lepszego jedzenia dziennie na ok. 340 szt. zboza i ryb poza ksiega (pkt 2.6).
- zakupy.log nie byl analizowany.

---

## CO TRZYMA SENS

1. **Jedzenia nie brakuje i nikt nie umiera z glodu.** Wsi z glodem 0, zmarlych 0, warowni z zapasem 0: zero dob. Spichlerze 726 dni (L128976).
2. **Zelazo dociera do miast.** Miast bez rudy 44 (Q1) -> 7 (Q4); cykle warsztatow stracone z braku rudy 551 -> 45 na dobe (L129060, L128929).
3. **Wozy surowcow rundy 3 dzialaja.** 42 101 szt. rudy w roku, 77% calego dowozu rudy, 0 wozow zawroconych (L129190).
4. **Ceny ida za niedoborem.** Wszystkie 7 surowcow: indeks spada, gdy przybywa miast z nadwyzka, rosnie, gdy przybywa miast bez towaru (pkt 2.1).
5. **Ksiega towarow sie zamyka.** Bez wyjasnienia +19 397 szt. na 9.04 mln wyprodukowanych = 0.2% (poprzedni bieg: -296 183).
6. **Zloto swiata jest stale.** 207.93 -> 208.34 mln w 334 doby (L10804, L129205).
7. **Wojna kosztuje ludzi w rozsadnej skali.** -0.99% ludnosci w roku, 187 910 uchodzcow, 824 puste wioski (CSV ludzie-regiony.csv, L128977).
8. **Bandy rosna, gdy rosnie liczba wyrzutkow, i bija wozy.** Bandyci x3, rozbite wozy wsi 7.3 -> 13.8 na dobe (L129063).
9. **Budowy wojenne ida, cywilne czekaja.** Idzie: Fortifications 12, Barracks 7 (budowy.log, ostatni dzien).
10. **Konie tanieja, gdy polki rosna.** Bojowy 3 226 -> 2 708 zl przy polkach 116 -> 1 863 (L129221).
11. **Kolejka zamowien wojska maleje.** 43 994 -> 14 740 szt. (handel.log).
12. **Wozy wsi sprzedaja prawie wszystko.** Niesprzedane 0.181% (progi A1-T19).
13. **Lud jest bogaty tam, gdzie wydaje wojsko.** 63% dochodu ludu z zewnatrz to zolnierze (L129238).

## NIESPOJNOSCI

| Nr | Co nie trzyma sensu | Dowod | Waga | Co by naprawilo (jedno zdanie) |
|---|---|---|---|---|
| 1 | Pieniadz wojny osiada u ludu miast i u zolnierzy, a korony i panowie biednieja. Lud +33.1 mln, zolnierze +11.9 mln; skarbce -27.5 mln, glowy rodow -13.5 mln. | LOG "Pieniadz swiata" L10804 i L129205 | wysoka | Podatek wojenny ludu od nadwyzki kiesy z wyzszym pulapem (dzis 3% i 15 tys./d przy 20 z 26 krolestw na pulapie). |
| 2 | Lud ma pieniadze, a nie ma czego kupic. Z budzetu 160 tys. d na dobe na 15 towarow da sie wydac 24%. Garum, atramentu, barwnikow i klejnotow nikt nie produkuje; jajka, miod, owoce puste w 94-97 miastach. | LOG "Ceny surowcow" L129060; "Towary (bilans)" L129048 | wysoka | Dac tym towarom zrodlo (wsie albo warsztaty) albo zdjac je z koszyka ludu. |
| 3 | Plotna nie ma caly rok: 86-90 miast bez, cena na suficie od doby 30. Lnu jest 501 na dobe, a w 72 miastach przerob stoi z braku lnu. Woz surowcow nie pomaga, bo nigdzie nie ma nadwyzki. | L129060; L128928; L129190 | wysoka | Wiecej lnu u zrodla (wsie lniane) - regulator plonow powinien podniesc len, gdy stos ma 12 dni zamiast 30-60. |
| 4 | Polki pelne zbroi tier 1-2 (korpus 70 723 szt.), a tier 3-4 prawie nie ma (129 szt. przy brakach 2 851 i 2 061). Rece rzemieslnikow zajete w 98%. | L129174; warsztaty.log linia 1092; L128929 | wysoka | Przesunac rece platnerzy z tier 1-2 (ktorych jest zalew) na tier 3-4, gdzie jest brak. |
| 5 | Dobrobyt miast +34% w roku ciaglej wojny, przy ludnosci -1%. Dobrobyt zamkow +107%. Razem z dobrobytem rosnie koszt koszyka ludu (+33%) i lud przestaje nadazac. | CSV kasy-miast.csv, ludnosc-bk.csv, ludzie-regiony.csv; L10837 i L129238 | srednia | Powiazac przyrost dobrobytu z ludzmi i wojna, tak by w wojnie nie rosl sam z siebie. |
| 6 | Ludu coraz czesciej nie stac na jedzenie, choc jedzenia jest w brod: 0.9 -> 9.1 miast; zjedzone bez zaplaty 2.9 tys. -> 12.2 tys. zl na dobe (2.54% w dobie 364, prog 1%). W podzamczach 12 razy wiecej niz na poczatku. | L129238; progi B1-T6, B1-T7 | srednia | Dac biednym miastom (wyspy, porty bez armii) dochod niezalezny od zolnierzy, np. czesc zaworu kasy do ludu tam, gdzie kiesa ma ponizej 14 dni. |
| 7 | Karawany nie zarabiaja na siebie: 177 z 535 nie ma na zold, notable doplacaja 49 tys. zl na dobe i stracili 28% pieniedzy w roku. 554 karawany rozbite, 92% przez lordow. | L129216; bitwy.log | srednia | Ograniczyc liczbe karawan do tylu, ile handel utrzyma (limit na kupca jest 3) albo chronic karawany neutralnych kupcow przed lordami. |
| 8 | Drewna jest zalew i dalej rosnie: 11 792 -> 152 862 ladunkow, +426 na dobe. Tartaki traca zysk (22.9 -> 7.6). | L129019; progi R1-9 | srednia | Regulator lasu wsi powinien ciac wycinke, gdy stos przekracza 60 dni zuzycia (dzis 102 dni). |
| 9 | Budowy cywilne stoja caly rok: 198 z 227 osad, 144 ponad 300 dni, 63 poziomy w roku. Przy wojnie bez przerwy miasta nigdy sie nie rozwina. | L128995; budowy.log | srednia | Pozwolic budowac cywilne w osadach daleko od frontu, nawet gdy krolestwo jest w wojnie. |
| 10 | Wsie nie maja z czego zyc po podatkach: korona bierze 59-65% gotowki, pan 32%, wsi zostaje ok. zera; kiesy stoja od Q2; 73 wsie ponizej 1 000 zl. | L129218; L129213 | srednia | Nie podnosic tu nic; pulap lacznej daniny wsi (pan + korona) ponizej 100% wplywow. |
| 11 | Skora wyprawiona: 42 miasta bez i gorzej niz na poczatku (33). 96 cykli warsztatow dziennie traci sie z braku skory, naprawy czekaja na 2 231 szt. | L129060; L128929; progi N1-3 | srednia | Wiecej skor surowych u zrodla (hodowla, uboj) - garbarnie pracuja w 60% z braku wsadu. |
| 12 | Zamki nie dostaja wyrobow ani luksusu: zadowolenie 0.50 przez 364 doby. | L129248 | niska | Wozy kupcow powinny dowozic wyroby takze na targi zamkow. |
| 13 | Kopalnie BK w miastach (12 warsztatow) stoja caly rok i przynosza strate. | L128955 | niska | Zamknac je albo dac im rude z dymarek. |
| 14 | Ludnosc wsi w rachunku BannerKings spada 18% rocznie, a zamkow rosnie 45%. Nasza ksiega ludzi pokazuje -1%. Dwa rachunki tych samych ludzi sie rozjezdzaja. | CSV ludnosc-bk.csv | niska | Sprawdzic, co w BannerKings przenosi ludzi ze wsi do zamkow, i czy cos od tego zalezy (ochotnicy, plon). |
| 15 | Kasy Thenn i Frostfang's Camp przy zerze (38 dob ponizej 1 000 zl; w poprzednim biegu 1 doba). | CSV kasy-miast.csv; progi C1-3 | niska | Najpierw ustalic przyczyne - nie wiem, co je oproznia. |
| 16 | Poza ksiega: ok. +340 szt. zboza i ryb dziennie powstaje, ok. -300 szt. lepszego jedzenia znika. | LOG "Towary:" suma roczna | niska | Nazwac te regule w ksiedze towarow. |
| 17 | Konie leza na polkach (9 423 wierzchowce), a 5 540 zolnierzy czeka na konia. | L129221; progi K3-10 | niska (moja dziedzina) | Opisane w progach jako brak siodel i cena - sprawa rozdzialu o wojsku. |
