# Raport z biegu rocznego - rozdzial WOJNA I SWIAT (klucz: wojna)

Bieg: nowa kampania, 364 doby, sam swiat AI, kod w-toku/zbior-e8 9e2e0e6 (WGRANIE 36 + runda 3 + sluby NPC).
Praca tylko do odczytu. Nic nie uruchamialem, nic nie zmienialem.

## Jak czytac

- Doba = dzien gry minus 108836 (dzien 108837 = doba 1). Rok = 364 doby (kalendarz Armoury).
- [P] = pomiar z logu albo tabeli. [S] = szacunek (moje dzialanie na pomiarach).
- Skroty zrodel:
  - ARM = D:\Backup-Bannerlord\logs\2026-10-13-noc34-35\Armoury-2026-10-13_06-24-10.log (L = numer linii)
  - CS = ...\session-2026-10-13_06-24-09.log (log CrashScribe, L = numer linii)
  - BIT = ...\Logs\2026-10-13_06-24-10\bitwy.log
  - BAL = ...\balans-krolestw.csv, LR = ludzie-regiony.csv, BR = budzet-rodow.csv
  - P2 / P4 / P5 / P7 = ...\scratchpad\test\progi-rok5\etap2.txt / etap4.txt / etap5.txt / etap7.txt (numer linii pliku)
  - POPRZ = poprzedni bieg roczny (WGRANIE 34): Armoury-2026-10-13_02-15-21.log, tabele Logs\2026-10-13_02-15-21\, log CrashScribe session-2026-10-13_02-15-20.log (w Documents\...\CrashScribe)
- Skrypty i ich wyniki: ...\scratchpad\raport-rok\skrypty\wojna\ (k1-k15, pliki .txt obok; podkatalog poprz = te same skrypty na poprzednim biegu).
- Nazwy krolestw w tabelach gry to id ROT. Slownik z bitwy.log [P, k5]: battania = The North, vlandia = House Baratheon of King's Landing (dalej "KL"), empire = The Vale, empire_w = The Reach, empire_s = House Targaryen, aserai = Dorne, sturgia = Iron Islands, khuzait = Dothraki Horde, nord = Lorath, valyrian = House Targaryen, Aegon.

## Najkrocej

1. Bitwy trzymaja logike. Silniejszy wygrywa 1849 z 1869 bitew lordow (98.9%) [P]. Ziemie bierze ten, kto przyprowadzi wieksza armie. Nikt nie rosnie jak kula sniezna i nikt nie znika.
2. Wojna jako calosc NIE trzyma logiki, bo sie nie konczy. 14 wojen fabularnych ROT trwa caly rok. Krolestwa 73 razy uchwalily w nich pokoj i 73 razy ROT wypowiedzial te sama wojne tego samego dnia [P]. 26 z 29 krolestw jest w wojnie przez wieksza czesc roku: 25 przez 342-364 doby z 364, Riverlands przez 277 [P].
3. Z tego jednego faktu biora sie prawie wszystkie inne klopoty tego rozdzialu: wojna nie ma wyniku w zlocie (12.3 mln reparacji nie naliczono), jency wychodza za darmo przy pokoju na zero dni, uchodzcy nie wracaja (187 910 na koniec roku), wyrzutki wracaja do domu 4 razy wolniej, a "podatek na czas wojny" jest w praktyce podatkiem stalym.
4. Wobec poprzedniego biegu rocznego wojsko trzyma sie lepiej (79 409 wobec 64 013 w druzynach lordow krolestw w wojnie), mapa jest duzo spokojniejsza (14 zmian pana twierdzy wobec 41), nie bylo ani jednego buntu (poprzednio 3).

---

## 1. Wojny roku

### 1.1 Ile wojen naraz

| doba | par w wojnie | krolestw w wojnie (z 29) |
|---|---|---|
| 2 | 2 | 4 |
| 30 | 15 | 25 |
| 91 | 16 | 25 |
| 182 | 18 | 26 |
| 273 | 17 | 26 |
| 364 | 15 | 26 |

Zrodlo: linie KRONIKA WOJEN w CS, jedna na dobe (k1, kronika.txt) [P]. Od doby 31 do konca: srednio 16.6 par (15-18), srednio 25.8 krolestw w wojnie (25-26) [P]. Prog P7:55 mowi to samo (najmniej 15, srednio 16.6, najwiecej 18).

Poza wojna przez caly rok sa tylko trzy male krolestwa: Skagos, Summer Isles, Yi Ti Exiles (0 dob z flaga WOJNA w liniach "Skarbce", ARM L603-L129131; Yi Ti mialo jedna wojne na 1 dobe) [P].

### 1.2 Zdarzenia roku

Zrodlo: linie "Wojna i pokoj (E7/0)" w ARM (k3, e70.txt) i "Reparacje (T9)" w CS (k4, reparacje.txt). Wszystko [P].

| Zdarzenie | ile |
|---|---|
| wypowiedzenia wojny przez ROT (EnforceWars) | 86 = 13 pierwszych + 73 ponowne |
| wojny z wezwania sojusznika (CallToWar) | 17 |
| pokoje z decyzji krolestwa | 80 = 73 w wojnach fabularnych + 7 w zwyklych |
| pokoje, bo wzywajacy przestal placic sojusznikowi | 8 |
| krolestwa zniszczone | 0 |
| nowe krolestwa (bunty) | 0 |

Prog P7:10 liczy okno o jedna dobe krotsze i podaje 186 zdarzen. Ja licze caly log: 191.

### 1.3 Czternascie wojen fabularnych ROT - wojny bez konca

ROT co dobe sprawdza liste wojen fabularnych i wypowiada kazda od nowa, jesli strony zawarly pokoj. Nasza Fabula rozciaga okna tych wojen x4.33 (CS L285). Dat konca tych okien w logach nie ma - nie wiem, kiedy ROT ktorakolwiek zwolni. Nastepne zdarzenie fabuly (RenlyDeath) stoi na dzien 519 (CS L9234).

Wynik pomiaru: pokojow fabularnych 73, wznowionych 73, nie wznowionych 0 (CS L9240) [P]. Od ponownego wypowiedzenia do nastepnego pokoju mija od 14 do 127 dob, mediana 47 [P, k3].

| Para (A - B) | wojna od doby | pokoje zerwane tego samego dnia | bitwy lordow (wygrane A : B) | twierdze | kto wedlug Diplomacy przegrywal (placilby reparacje) |
|---|---|---|---|---|---|
| Free Folk - Nights Watch | 0 (od startu) | 3 | 7 (2 : 5) | bez zmian | Free Folk 3 razy, 652 000 |
| Dragonstone - Stormlands | 1 | 10 | 384 (212 : 172) | Stormlands +4 zamki | Dragonstone 7 razy 1 866 700; Stormlands 3 razy 355 600 |
| KL - The North | 9 | 4 | 57 (7 : 50) | North +1 (Harrenhal, oblezenie z fabuly) | KL 3 razy, 866 500 |
| The Vale - Iron Islands | 22 | 0 | 0 | bez zmian | nikt (zadnego pokoju) |
| The Reach - Dorne | 22 | 6 | 165 (108 : 57) | Reach +4 | Dorne 4 razy 965 300; Reach 2 razy 272 300 |
| Braavos - Lorath | 22 | 8 | 210 (77 : 133) | Braavos +1 | Lorath 6 razy 579 000; Braavos 2 razy 109 700 |
| Tyrosh - Lys | 22 | 7 | 151 (101 : 50) | Tyrosh +1 | Lys 6 razy 523 700; Tyrosh 1 raz 59 000 |
| Pentos - Myr | 22 | 4 | 121 (57 : 64) | bez zmian | Myr 3 razy 167 100; Pentos 1 raz 186 600 |
| Norvos - Qohor | 22 | 8 | 279 (97 : 182) | bez zmian | Norvos 8 razy, 727 800 |
| Volantis - Aegon | 22 | 2 | 25 (16 : 9) | Volantis +1 | po 1 razie (80 400 i 156 600) |
| Qarth - House Targaryen | 22 | 6 | 92 (42 : 50) | bez zmian | Qarth 4 razy 508 000; Targaryen 2 razy 295 300 |
| Dothraki - House Targaryen | 22 | 6 | 55 (37 : 18) | bez zmian | Dothraki 6 razy, 1 615 600 |
| Sarnor - Ibben | 22 | 2 | 34 (10 : 24) | bez zmian | Ibben 2 razy, 117 200 |
| KL - Riverlands | 130 | 7 | 220 (118 : 102) | KL +1 | Riverlands 7 razy, 2 209 000 |

Zrodla: wojna od / pokoje - e70.txt; bitwy lordow - bitwy.txt (k5, linie B175 z "lordowie tak"); twierdze - mapa.txt (k6, LR); reparacje - reparacje.txt (CS, linie "reparacje 0 zamiast N"). Wszystko [P].

Co widac:
- Reparacji w tych wojnach nie naliczono: 12 313 400 zl w 72 pokojach (CS L9240) [P]. To nasza latka T9 - slusznie, bo pokoj trwal zero dni. Skutek uboczny: zwyciezca nie dostaje nic.
- 7 z 14 wojen przez caly rok nie zmienilo ani jednej twierdzy. W tym Norvos - Qohor: 279 bitew lordow i zero zmian na mapie.
- The Vale - Iron Islands: 342 doby wojny, 0 bitew lordow, 0 rabunkow, 0 pokojow. Te krolestwa leza po dwoch stronach Westeros.
- Kierunek reparacji nie zawsze zgadza sie z bitwami. Dothrakowie wygrywaja 67% bitew, a 6 razy to oni mieliby placic. Lorath wygrywa 63% bitew, a 6 razy mialby placic i stracil zamek. Nie wiem, z czego Diplomacy to liczy (zmeczenie wojna, straty, rabunki) - tego w logu nie ma.

### 1.4 Wojny zwykle (z wezwania sojusznika) - te sie koncza

| Para | odcinki (doby) | koniec | reparacje |
|---|---|---|---|
| Qarth - Volantis | 22-136 (114 d); 191-345 (154 d) | 1. pokoj z decyzji; 2. zerwane wezwanie | 0 |
| House Targaryen - Aegon | 22-182 (160 d); 198-207 (9 d); 245-... trwa | 1. pokoj; 2. zerwane wezwanie | Aegon zaplacil 76 500 |
| Riverlands - The Vale | 33-76 (43 d) | pokoj z decyzji | 0; Vale zatrzymala Saltpans |
| Myr - Tyrosh | 50-111 (61 d); potem 115-116, 172-184, 227-228, 303-323 | 1. pokoj; 2-5. zerwane wezwanie | Tyrosh zaplacil 137 100 |
| KL - Iron Islands | 130-223 (93 d); 245-336 (91 d); 364-... trwa | pokoj, pokoj | 0 |
| Lorath - The Vale | 163-164 (1 d) | zerwane wezwanie | - |
| Lys - Pentos | 173-264 (91 d) | pokoj z decyzji | 0 |
| Sarnor - Yi Ti Exiles | 338-339 (1 d) | zerwane wezwanie | - |

Zrodla: e70.txt (ARM, m.in. L8085, L11926, L27049, L39416, L41004, L48290, L64569, L79214, L93774, L119313, L120190) i reparacje.txt (CS L4247, L5593) [P].

Razem 17 odcinkow: 7 skonczylo sie prawdziwym pokojem po 43-160 dobach, 8 zerwaniem wezwania, 2 trwaja. Reparacje zaplacone naprawde: 213 600 zl w 2 pokojach (CS L9240) [P]. Trybutu dziennego w logach nie widze - nie wiem, czy jakikolwiek byl.

Wezwania do wojny (linie "Wezwania do wojny (168)", k13): 17 porozumien, 8 zerwanych z braku wplywow dnia, zaplacono sojusznikom 1 790 085 zl w roku, skreslono 5 396 992 zl nieoplaconych reszt [P]. Pentos piec razy wezwal Tyrosh przeciw Myr za ok. 0.9-1.0 mln (ARM L17900, L40827, L61036, L80628, L107600) i cztery razy nie mial na rate ok. 5 tys. zl dziennie (ARM L41005, L65120, L80816, L114507) [P]. Cztery wojny trwaly 1 dobe.

Otwarte: Nights Watch wezwalo Skagos przeciw Free Folk za 611 334 zl, po 3 359 zl dziennie (ARM L18251). Skagos nie pojawia sie w KRONICE ani razu i nie ma ani jednej doby z flaga WOJNA. Nie wiem, czy Skagos weszlo do wojny i ile wzielo.

### 1.5 Pokoj z biedy

Mechanizm "krolestwo bez zlota prosi o pokoj" przez rok nie zlozyl ani jednego wniosku (CS L9239): "WNIOSKOW Z BIEDY 0", powod "tylko wojny fabularne ROT 14" [P]. Gra pytala o pokoj 6469 razy, 5634 razy odpowiedz brzmiala "wojna trwa za krotko" (Diplomacy), do glosowania poszlo 78 wnioskow [P]. Wojna jest "za mloda" stale, bo po kazdym pokoju ROT zaczyna ja od nowa.

---

## 2. Bitwy i oblezenia

### 2.1 Ile

Zrodlo: BIT, linie "Bitwa: B175" (5734 w roku) i "Bitwa: H3" (8266), skrypty k5, k14, k15. Wszystko [P].

| Rodzaj | w roku | na dobe |
|---|---|---|
| bitwy lordow (lord lub armia po obu stronach) | 1869 | 5.13 |
| w tym wyrownane | 263 | 0.72 |
| rabunki wsi (Raid) | 2220 | 6.1 |
| szturmy (Siege) | 23 | 0.06 |
| bitwy pod murami i wypady (SiegeOutside, SallyOut) | 61 | 0.17 |
| bitwy morskie | 131 | 0.36 |
| wszystkie bitwy polowe z ksiegi przegranych (H3) | 8266 | 22.7 |

Bitwy lordow wedlug kwartalow: 3.35 / 6.05 / 5.92 / 5.21 na dobe. BAL potwierdza sume 1869 (ARM L129204).

Straty w bitwach polowych w roku (ARM, linie "Przegrani (H3)", k8): przegrani 337 935 ludzi - zabici 74 744 (22%), jency 21 563 (6%), rozbici 241 628 (72%); zwyciezcy 1 328 933 - zabici 23 866 (1.8%) [P]. Gra bez naszej reguly zabilaby 173 236.
Wszyscy zabici zolnierze i cywile w roku: 260 864, w tym z druzyn lordow 146 594, z taborow wsi 25 105, z karawan 19 267, z band 37 985 (ARM, linie "Ludzie:", k7) [P].

### 2.2 Czy wygrywa silniejszy

| Stosunek sil | silniejszy wygrywa |
|---|---|
| 1.0-1.25 | 94 z 111 (85%) |
| 1.25-1.5 | 146 z 149 (98%) |
| 1.5 i wiecej | 1609 z 1609 (100%) |
| razem bitwy lordow | 1849 z 1869 (98.9%) |

Zrodlo: BIT, pole "moc" i "wygrywa" w liniach B175 (k5) [P].

### 2.3 Wygrane i przegrane wedlug krolestw, stan na koniec

| Krolestwo | twierdze start -> koniec | bitwy lordow wygrane / przegrane | wojsko lordow: doba 1 -> szczyt (doba) -> koniec | zalogi: doba 1 -> koniec | dob w wojnie | dlug w Banku na koniec |
|---|---|---|---|---|---|---|
| The North | 22 -> 23 | 50 / 7 (88%) | 2392 -> 12120 (84) -> 7685 | 4204 -> 9394 | 355 | 24 022 |
| KL | 21 -> 21 (+1 -1) | 126 / 152 (45%) | 1153 -> 10279 (72) -> 6068 | 4336 -> 6989 | 355 | 174 530 |
| The Reach | 16 -> 20 | 108 / 57 (65%) | 2014 -> 7953 (104) -> 5883 | 3104 -> 6093 | 342 | 8 431 |
| Dorne | 16 -> 12 | 57 / 108 (35%) | 763 -> 7419 (71) -> 4340 | 2995 -> 4683 | 342 | 18 974 |
| Riverlands | 14 -> 12 | 113 / 121 (48%) | 2932 -> 7404 (74) -> 2424 | 3118 -> 4767 | 277 | 3 509 |
| The Vale | 13 -> 14 | 3 / 11 (21%) | 1900 -> 7796 (132) -> 5493 | 2710 -> 5663 | 342 | 0 |
| Stormlands | 13 -> 17 | 172 / 212 (45%) | 2152 -> 6762 (213) -> 5198 | 2634 -> 5635 | 363 | 3 717 |
| Iron Islands | 10 -> 10 | 0 / 1 | 1218 -> 5849 (68) -> 1428 | 2325 -> 4860 | 342 | 0 |
| Dragonstone | 10 -> 6 | 212 / 172 (55%) | 1972 -> 4701 (12) -> 3425 | 2075 -> 3166 | 363 | 281 508 |
| Dothraki Horde | 7 -> 7 | 37 / 18 (67%) | 709 -> 3597 (244) -> 3011 | 1357 -> 2232 | 342 | 0 |
| Free Folk | 7 -> 7 | 2 / 5 | 1344 -> 3627 (33) -> 720 | 1210 -> 2797 | 364 | 0 |
| House Targaryen | 6 -> 6 | 74 / 92 (45%) | 1137 -> 3460 (207) -> 2228 | 1691 -> 2236 | 342 | 7 906 |
| Volantis | 6 -> 7 | 20 / 9 (69%) | 1173 -> 4347 (132) -> 3609 | 1739 -> 2879 | 342 | 6 260 |
| Lorath | 5 -> 4 | 133 / 77 (63%) | 774 -> 2592 (226) -> 1593 | 983 -> 1661 | 342 | 44 421 |
| Nights Watch | 5 -> 5 | 5 / 2 | 1191 -> 4511 (306) -> 4077 | 836 -> 1737 | 364 | 0 |
| Braavos | 5 -> 6 | 77 / 133 (37%) | 1033 -> 3117 (123) -> 1522 | 1101 -> 2097 | 342 | 661 983 |
| Pentos | 5 -> 5 | 59 / 64 (48%) | 403 -> 2119 (54) -> 1283 | 1006 -> 1966 | 342 | 76 670 |
| Norvos | 5 -> 5 | 97 / 182 (35%) | 688 -> 2563 (107) -> 1254 | 1029 -> 2014 | 342 | 28 360 |
| Qohor | 5 -> 5 | 182 / 97 (65%) | 282 -> 2788 (191) -> 1356 | 1043 -> 1730 | 342 | 36 030 |
| Myr | 5 -> 5 | 76 / 74 (51%) | 577 -> 2972 (75) -> 1966 | 1153 -> 1812 | 342 | 42 353 |
| Lys | 5 -> 4 | 50 / 103 (33%) | 296 -> 2273 (114) -> 1065 | 1069 -> 1071 | 342 | 76 565 |
| Tyrosh | 5 -> 6 | 118 / 62 (66%) | 283 -> 2544 (81) -> 2327 | 1065 -> 2032 | 342 | 68 327 |
| Sarnor | 5 -> 5 | 10 / 24 (29%) | 321 -> 2662 (89) -> 1399 | 1021 -> 633 | 342 | 0 |
| Aegon | 5 -> 4 | 22 / 22 (50%) | 278 -> 3208 (120) -> 1597 | 1178 -> 1732 | 342 | 194 762 |
| Qarth | 4 -> 4 | 42 / 54 (44%) | 367 -> 3682 (244) -> 1818 | 1207 -> 1849 | 342 | 91 263 |
| Ibben | 4 -> 4 | 24 / 10 (71%) | 366 -> 2590 (55) -> 521 | 797 -> 1660 | 342 | 2 519 |
| Skagos | 1 -> 1 | 0 / 0 | 140 -> 823 (359) -> 241 | 176 -> 89 | 0 | 0 |
| Summer Isles | 1 -> 1 | 0 / 0 | 59 -> 606 (57) -> 538 | 97 -> 48 | 0 | 0 |
| Yi Ti Exiles | 1 -> 1 | 0 / 0 | 54 -> 690 (311) -> 68 | 97 -> 68 | 0 | 0 |

Zrodla: BAL, wiersze dnia 108837 i 109200 (kolumny twierdze, twierdze_start, bitwy_lordow, wygrane, ludzie_partie, ludzie_zalogi); szczyt = najwieksza wartosc ludzie_partie w roku; dob w wojnie i dlug - linie "Skarbce" ARM L603-L631 i L129103-L129131 (k9, krolestwa.txt). Wszystko [P].

Swiat razem: druzyny lordow 29 896 (doba 0) -> 109 917 (szczyt, doba 66) -> 77 837 (doba 363); zalogi 47 356 -> 83 593; milicje 62 991 -> 74 006 (ARM L770, L129266 i linie "Ludzie:" miedzy nimi, k7) [P]. Wojsko lordow spadlo od szczytu o 29% [S].

### 2.4 Szturmy

23 szturmy: 14 udanych, 9 odpartych albo przerwanych (BIT; ARM L129204 potwierdza 14) [P].
- Udane: atakujacy razem 47 013 ludzi, stracili 13 663 (29%); obroncy 7 399, stracili wszystkich. Mediana przewagi 6.8 do 1, najmniejsza 3.6 do 1 [P, k14].
- Nieudane: atakujacy 28 659, stracili 8 760 (31%); obroncy stracili 63%. Mediana przewagi 4.1 do 1 [P, k14].

### 2.5 Ktore twierdze zmienily pana

| doba | twierdza | rodzaj | od | do | atakujacy / zaloga | straty atakujacego |
|---|---|---|---|---|---|---|
| 51 | Cape Wrath | zamek | Dragonstone | Stormlands | 1649 / 109 | 123 |
| 62 | Fellwood | zamek | Dragonstone | Stormlands | 1803 / 110 | 136 |
| 64 | Saltpans | miasto | Riverlands | The Vale | 3500 / 756 | 1224 |
| 67 | Starfall | miasto | Dorne | The Reach | 5035 / 573 | 1059 |
| 83 | Demongate | zamek | Aegon | Volantis | 2324 / 299 | 891 |
| 113 | Blackmont | zamek | Dorne | The Reach | 3205 / 341 | 472 |
| 132 | Harrenhal | miasto | KL | The North | 10321 / 2459 | 4804 |
| 158 | Sandstone | zamek | Dorne | The Reach | 2339 / 303 | 650 |
| 194 | Kingsgrave | zamek | Dorne | The Reach | 3662 / 573 | 1005 |
| 209 | Sombazmion | zamek | Lys | Tyrosh | 1122 / 349 | 453 |
| 223 | Sweetport | zamek | Dragonstone | Stormlands | 3340 / 535 | 1157 |
| 225 | Bisa | zamek | Lorath | Braavos | 1529 / 452 | 620 |
| 236 | Stonedance | zamek | Dragonstone | Stormlands | 2658 / 192 | 489 |
| 298 | Willow Wood | zamek | Riverlands | KL | 4526 / 348 | 580 |

Zrodla: BIT linie B175 "Siege ... zdobyta tak" (numery w bitwy.txt: L2086, L2747, L2818, L3026, L4013, L5971, L7222, L9376, L12325, L13406, L14545, L14737, L15602, L20412); wlasciciel przed i po - LR kolumny krolestwo, rod (mapa.txt). Wszystko [P].

- 14 twierdz, kazda zmienila pana dokladnie raz. Zadna nie przechodzila z rak do rak.
- 11 zamkow i 3 miasta. Harrenhal to oblezenie z fabuly ROT (CS L9248: "HarrenhalSiege").
- Proby nieudane: Demongate (doba 69), Blackmont (82), Sweetport (107, 2139 zabitych atakujacych), Sandstone (126), Yunkai dwa razy (156 i 304, Qarth), Stoney Sept (205, Riverlands), Sharp Point (250, Stormlands), Kingsgrave (347, Dorne probowalo odbic) [P, bitwy.txt].
- Po zdobyciu korona oddawala twierdze wasalowi w 4 z 14 przypadkow w ciagu doby (Saltpans -> Royce, Harrenhal -> Bolton, Bisa -> Antaryon, Willow Wood -> Lannister); reszte trzymaja glownie krolowie (Tyrell 4, Renly 4, Uhoris 1) [P, mapa.txt].

### 2.6 Kula sniezna albo znikanie

- Najwiekszy zysk: The Reach 16 -> 20 i Stormlands 13 -> 17 (po +4, czyli +25% i +31%) [P].
- Najwieksza strata: Dragonstone 10 -> 6 (-40%) i Dorne 16 -> 12 (-25%) [P].
- Dragonstone stracil 4 zamki, ale wszystkie 4 miasta trzyma, a jedyny szturm na jego miasto (Sharp Point) zostal odparty. Tempo strat: 2 zamki w pierwszej polowie roku i 2 w drugiej. Jesli tak zostanie, w roku 2 Dragonstone moze zejsc do samych miast [S].
- Zadne krolestwo nie upadlo. Zadne nie podwoilo stanu posiadania.

---

## 3. Mapa na koniec roku

Tabela twierdz start -> koniec jest w 2.3 (kolumna 2). Suma 227 -> 227 [P, LR].

- Krolestwa upadle: 0. Nowe krolestwa: 0 (w ARM nie ma ani jednej linii "KROLESTWO ZNISZCZONE" ani wojny "CausedByKingdomCreation") [P].
- Rody, ktore zniknely z tabeli budzetow: 0 z 347 [P, BR, mapa.txt].
- Rody z lennem, ktore zmienily krolestwo: 0 [P, BR].
- Nowe rody: 28, wszystkie to "Courtiers of ..." (dworzanie BannerKings), po jednym na krolestwo, pojawiaja sie miedzy doba 7 a 91 [P, BR].
- Najemnicy: 11 kompanii zmienilo pracodawce 764 razy. Mediana jednej sluzby 3 doby, srednia 5.1, najdluzsza 31. 151 sluzb trwalo 1 dobe. Kazda kompania sluzyla w roku u 20-23 roznych krolestw. 11 razy kompania przeszla prosto do wroga poprzedniego pana [P, BR, k11].

Dorosli zywi lordowie AI: 1227 (doba 1) -> 1662 (doba 364), czyli +435 (ARM L683, L129182) [P]. Wiekszosc przyrostu to pierwsze 4 miesiace (dworzanie BK).

---

## 4. Wsie, uchodzcy, wyrzutki i bandy

### 4.1 Rabunki wsi i spustoszenie

Zrodla: ARM linie "Ludzie (spustoszenie)" (k7), P7:81, P7:108, P7:114, P7:120, P4:113-128, BIT. Wszystko [P], chyba ze napisano inaczej.

| Miara | rok |
|---|---|
| rabunki wsi (bitwy Raid) | 2220, napastnik wygrywa 1801, bez wyniku 393, wies sie broni 26 |
| sesje rabunku osada po osadzie | 1485: doprowadzone do konca 345, przerwane 1140 (77%) |
| wsie spladrowane (Looted) | 345 razy; spalone w ksiedze 28 (z 2446 wiosek) |
| ludzie trafieni rabunkiem i zerowaniem armii | 230 527 |
| z nich zabici | 6 422 (2.8%) |
| uciekli do lasu (do puli wyrzutkow) | 8 037 (3.5%) |
| zostali uchodzcami | 216 069 (94%) |
| lup z kies wsi | 105 403 zl, zboze 10 644 szt. |

Kto rabuje najwiecej: The North 293, Stormlands 188, House Targaryen 167, Qohor 130. Kogo: KL 412, Dragonstone 188, House Targaryen 150, Norvos 130 [P, bitwy.txt]. Polnocy nikt nie rabuje (0 uchodzcow w jej wsiach, LR doba 363).

### 4.2 Uchodzcy

| doba | uchodzcow poza domem | wsi z uchodzcami |
|---|---|---|
| 30 | 1 090 | 11 |
| 91 | 16 183 | 67 |
| 182 | 65 633 | 153 |
| 273 | 132 504 | 181 |
| 363 | 187 910 | 191 |

Zrodlo: ARM L11220, L32673, L64918, L97324, L129272 [P].

- W roku przybylo 216 068 uchodzcow, wrocilo 24 232 (11%), zmarlo 1 002 [P, k7].
- W ostatnim kwartale wraca juz co piaty (14 798 z 71 617 nowych) [P].
- Konto rosnie rowno, ok. 675 ludzi na dobe w drugim polroczu [S z L64918 i L129272]. Nie widac, zeby sie zatrzymywalo.
- Powrot rusza dopiero po 30 dobach spokoju we wsi. W ostatniej dobie czeka na to 99 wsi (ARM L129272) [P]. W wojnie bez konca front nie odchodzi, wiec wiekszosc nie wraca.
- Skupienie: ziemie KL 68 475 uchodzcow (36% calosci), Dragonstone 28 480 (5.6% jego ludnosci). Sharp Point: 31.5% ludzi poza domem, plon wsi x0.43. Duskendale 29%, Rathylar 22% (LR doba 363; ARM L129272) [P].
- Prosta kontynuacja: ok. 430 tys. uchodzcow na koniec roku 2 [S]. To dalej ok. 1% ludnosci wsi (41.1 mln), ale w najbardziej najezdzanych okregach plon moze spasc ponizej polowy - dzis jest tak w jednym (Sharp Point, x0.43) [S].

Ludnosc swiata: 51.986 mln -> 51.471 mln w domach, z uchodzcami 51.659 mln (ARM L770, L129266) [P]. Prog P4:121: ludzi zywych ubywa 505 na dobe = -0.35% na rok (w pasie decyzji Jeffa z 12.10).

### 4.3 Pula wyrzutkow - skad sie biora i czy wracaja

Stan: 819 (doba 1) -> 2 258 (91) -> 8 630 (182) -> 10 785 (273) -> 11 869 (364). Prog P4:81 NIE (baza 3 053) [P, ARM L635, L32192, L64433, L96815, L129135].

Przeplywy w dobach 2-364 (ARM linie "Pula wyrzutkow (E4/3)", k7) [P]:

| Naplyw | ludzi | udzial |
|---|---|---|
| rozbici z bitew, ktorzy poszli w las | 78 887 | 61% |
| rozwiazane bandy (wracaja do puli) | 28 751 | 22% |
| spalone wsie | 8 037 | 6% |
| dezercja | 7 071 | 5% |
| bieda i wojna | 6 932 | 5% |
| razem | 129 678 | |

| Odplyw | ludzi | udzial |
|---|---|---|
| narodziny nowych band | 72 006 | 61% |
| werbunek do istniejacych band | 24 127 | 20% |
| powrot do domu | 21 043 | 18% |
| do Nocnej Strazy | 1 439 | 1% |
| razem | 118 619 | |

- Z rozbitych w bitwach (241 726 w roku) dwie trzecie idzie od razu do domu albo do karczmy, jedna trzecia w las [P z "Ludzie:" i "Pula"].
- Kto raz trafil do lasu, w 4 przypadkach na 5 konczy w bandzie, a do domu wraca co piaty [S z tabeli].
- Powrot do domu ma dwa tempa: 1/30 dob w pokoju i 1/120 dob w wojnie. Przez caly rok 212 z 227 regionow liczy sie jako "w wojnie" (ARM L129135) [P]. Czyli prawie caly swiat wraca 4 razy wolniej - znow przez wojny bez konca.
- Kontrola ksiegi: roznica 0.00 w 363 dobach (P4:80). Nikt nie znika i nikt nie powstaje z niczego [P].

Czy rosnie bez konca: nie tak jak w pierwszym polroczu. Tempo puli: +40 na dobe (doby 1-120), +53 (121-244), +4.8 (245-364) [P, k7]. Ostatnie 120 dob to prawie plaski stan ok. 11 tys.

### 4.4 Bandy

Stan: 314 band / 5 694 ludzi (doba 1) -> 713 / 14 464 (doba 182) -> 819 / 17 279 (doba 364). Prog P4:84 NIE (baza 826 / 14 328, tolerancja do 15 044) [P, ARM L634, L64432, L129134].

- Tempo: +62 ludzi na dobe (doby 1-120), +29 (121-244), +20 (245-364) [P, k7]. Zwalnia, ale nie stoi. Prosta kontynuacja: ok. 24-25 tys. ludzi w bandach na koniec roku 2 [S].
- Pula i bandy razem: 17 175 (doba 120) -> 26 435 (doba 240) -> 29 148 (doba 364) [P].
- Bandy w polu (BIT, linie H3, k15): wygraly 2 774 bitwy i przegraly 2 392 [P]. 2 544 z wygranych to male partie do 30 ludzi (tabory wsi, male karawany) [P].
- Wygrane band rosna z kwartalu na kwartal: 4.1 -> 7.7 -> 8.9 -> 9.8 na dobe. Przegrane stoja: 5.0 -> 6.9 -> 7.2 -> 7.1 na dobe [P, k15b]. Czyli lordowie zajeci wojna nie nadazaja z tepieniem band.
- Zabitych bandytow w roku: 37 985 (ARM "Ludzie:"). To ponad dwa razy wiecej niz stan band - bandy sa stale wybijane i stale odnawiane z puli [P].

Czy to grozi swiatu w roku 2: nie zawali go. Bandy to 17 tys. wobec 78 tys. w druzynach lordow i 84 tys. w zalogach. Ale szkoda dla handlu wsi rosnie: w ostatnim kwartale bandy rozbijaja prawie 10 malych partii na dobe, przy ok. 551 taborach wsi i 559 karawanach na mapie (ARM L129266) [P]. Lordowie w tym samym roku rozbili 457 taborow wsi i 512 karawan wroga (BIT, B175) [P].

---

## 5. Jency, okupy, bohaterowie, sluby, turnieje

### 5.1 Lordowie w niewoli i okupy

Zrodla: ARM linie "Niewola lordow i okupy (169c)" i "Okupy (178)", k8. Wszystko [P].

| Miara | rok |
|---|---|
| pojmania lordow | 2 919 (9.2 na rod AI); w tym krolowie 53, glowy rodow 844 |
| w niewoli naraz | srednio 35, najwiecej 84, w ostatniej dobie 76 |
| czas w niewoli | mediana w dobie od 0 do 8 dni (srednio 3.4), w zadnej dobie nikt ponad 60 dni |
| uwolnieni za okup | 1 260 |
| uwolnieni przez pokoj | 528 |
| uciekli | 680 |
| odbici po bitwie | 334 |
| wypuszczeni z wyboru | 41 |
| zmarli w niewoli | 0 |
| okupy AI-AI | 1 259 za 52 328 752 zl: gotowka 45 679 415, dlug okupu 6 649 337 |
| okup, ktorego rod nie mial z czego zaplacic | 20 |

- Okupy to duzy przeplyw: 52.3 mln w roku przy 208 mln zlota na swiecie (ARM L129205) i 228.6 mln rocznego zoldu (k12) [P].
- 440 z 528 uwolnien "przez pokoj" wypada w dobach, w ktorych zawarto pokoj fabularny trwajacy zero dni [P, k10]. Jency wychodza za darmo, a wojna trwa dalej.

### 5.2 Jency szeregowi

Wzieto do niewoli 105 180 ludzi, odbito 21 589, ucieklo ponad limit lochu 8 528, pojmano ze wsi 2 924 (ARM "Jency (E4/0)", k8) [P]. Prawo jenca w roku: do domu 26 394, na Mur 224, sprzedani w niewole w krainach z niewola 20 604 (ARM L128989) [P]. Na koniec roku: 4 771 jencow w partiach, 173 w lochach (ARM L129266) [P]. Rownanie jencow zamyka sie z reszta 1.5% obrotu (P4:57) [P].

### 5.3 Smierc i narodziny bohaterow

Nie wiem. Log nie ma linii zgonow ani narodzin bohaterow. Wiem tylko, ze: w niewoli nie zmarl nikt, a doroslych zywych lordow AI przybylo 435 (patrz rozdz. 3).

### 5.4 Sluby NPC

- 215 slubow w roku (ARM "Sluby AI (169c)" suma; CS L9237 "od wczytania 215") [P]. Poprzedni bieg: 113 [P, POPRZ].
- Wolni lordowie szukajacy pary: 669 (doba 2, CS L2152) -> 576 (doba 364, CS L9237) [P].
- Z tych 576 az 229 nie ma ani jednego kandydata z szansa (CS L9237) [P]. W dobie 2 bylo ich 28.
- 45 slubow to "biedny z bogatym" [P, k8].

### 5.5 Turnieje

524 starty w roku, najdluzsza przerwa 6 dob, zaden tydzien pory bez turnieju (P7:12, P7:52-53) [P]. Nagrody: gra chciala 61 144 zl, miasta-gospodarze zaplacily 57 004 (P7:57) [P].

---

## 6. Czy wojna trzyma logike

### 6.1 Czy wygrywa silniejszy i bogatszy

Silniejszy - tak (2.2). Na mapie tez: we wszystkich 14 zdobyciach atakujacy mial przewage co najmniej 3.6 do 1 [P].

| Para | wojsko lordow doba 1 (A : B) | kiesy rodow doba 1 (A : B) | wynik na mapie |
|---|---|---|---|
| Stormlands : Dragonstone | 2152 : 1972 | 2.64 mln : 2.17 mln | Stormlands +4 |
| The Reach : Dorne | 2014 : 763 | 3.15 mln : 4.19 mln | Reach +4 |
| KL : Riverlands | 1153 : 2932 | 3.76 mln : 2.97 mln | KL +1 |
| Volantis : Aegon | 1173 : 278 | 1.30 mln : 1.14 mln | Volantis +1 |
| Tyrosh : Lys | 283 : 296 | 1.10 mln : 1.05 mln | Tyrosh +1 |
| Braavos : Lorath | 1033 : 774 | 1.08 mln : 1.03 mln | Braavos +1 |

Zrodlo: BAL dzien 108837, linie "Skarbce" ARM L603-L631, LR [P]. Kiesy "razem" licza tez najemnikow w sluzbie, wiec to przyblizenie.

W 5 z 6 par ziemie wziela strona bogatsza w kiesach rodow, w 4 z 6 strona z wiekszym wojskiem na starcie [P]. Wyjatek bogactwa: Dorne (bogatsze, 4 twierdze w plecy). Probka jest mala - to wskazowka, nie dowod.

Zwyciestwo nie daje zlota. Skarbiec Stormlands (+4 twierdze) to 804 381, Reach (+4) 799 121, a przegrywajace Dorne ma 1 001 618 (ARM L129104, L129107, L129116) [P]. Reparacji w tych wojnach nie ma (1.3).

### 6.2 Czy armie maja co jesc i czym placic

Tak.
- Morale partii lordow: srednio 70.2 przez caly rok (66.3-73.2) [P, ARM "Morale AI", k8].
- Partie w glodzie: srednio 2.3 z 624, najwiecej 23 jednej doby [P].
- Warownie glodne: 0 przez 364 doby. Z ujemnym bilansem zywnosci srednio 2.8 z 227. Ponizej 90 dni zapasu najwyzej 1 [P, ARM "Ludzie:"].
- Zalegly zold: 2.49% (P2:11, prog 2% - lekko ponad). Odeszlo za brak zoldu 460 ludzi w roku [P, ARM "WarLedger doba"].
- Dezercja AI razem 13 254 w roku; z glodu w partiach lordow 1 219 [P, k8]. Poprzedni bieg: 21 596 i 4 067.
- Zaraza obozowa wybuchla przy 31 oblezeniach [P, ARM "CampFever"]. Liczby zmarlych nie podaje - nie wiem, czy linie licza dziennie, czy narastajaco.

### 6.3 Czy przegrywajacy traci ziemie i dochod

Ziemie - tak (Dragonstone -4, Dorne -4, Riverlands -2). Zloto - czesciowo: Dragonstone ma na koniec dlug 281 508 w Banku i skarbiec 697 397; Braavos dlug 661 983 i skarbiec 510 303 (ARM L129114, L129118) [P]. Ale Braavos wzial zamek, a Dorne po stracie 4 twierdz nie zbiednialo (kiesy rodow 4.19 mln -> 4.67 mln, linie "Skarbce" doby 1 i 364; liczone razem z najemnikami w sluzbie) [P]. Zwiazek "przegrales, wiec jestes biedniejszy" jest slaby, bo wojna nie konczy sie rozliczeniem.

### 6.4 Czy pokoj przychodzi, gdy strona jest wyczerpana

Decyzja o pokoju - tak: 80 razy krolestwa uchwalily pokoj; w wojnach fabularnych mediana to 47 dob od wypowiedzenia [P, k3]. W wojnach zwyklych pokoj jest prawdziwy (7 razy). W 14 wojnach fabularnych ROT kasuje go tego samego dnia (73 razy). Wiec dla 26 z 29 krolestw odpowiedz brzmi: nie.

### 6.5 Pory roku

- Kalendarz Westeros: lato do doby 128, jesien od doby 129 (ARM L213, L45849, L769, L129265) [P]. Jesien trwa 432 dni. Zima zacznie sie ok. doby 560, czyli w drugim roku [S z L129265: "do konca 197 dni"].
- W roku 1 zimy nie bylo: "ciecie plonu 0, dodatek jedzenia partii 0" we wszystkich 364 liniach [P].
- Zywnosc wsi swiata spadla o 2.96% wobec doby 1 (ARM L129265) [P].
- Wplywu pory roku na wojne nie widac. Bitew jest wiecej jesienia niz latem, ale to rozruch kampanii, nie pogoda.
- Uwaga: gra ma tez swoj krotki kalendarz (13 tygodni na pore) - linie turniejow pisza w dobie 1 "pora Spring" (ARM L722). To dwa rozne zegary, nie blad.

### 6.6 Nocna Straz, Wolni Ludzie, Inni, Dothrakowie, Zelazne Wyspy

Nocna Straz - zgodnie ze swiatem:
- Druzyny 1 191 -> 4 077, zalogi 836 -> 1 737 (BAL) [P].
- Nie rabuje: 0 rabunkow w roku, ok. 1 000 odrzuconych opcji rabunku dziennie (ARM L129165) [P].
- Skad ma ludzi: z Muru (skazancy) 2 145, z pul wyrzutkow 1 439, z ludnosci Westeros 2 658 (ARM L129165) [P].
- Bije Wolnych Ludzi w polu: 205 atakow lordow Strazy, z tego 132 na tabory wsi i 70 na karawany Wolnych Ludzi [P, BIT]. Bitew lord z lordem tylko 7.

Wolni Ludzie - slabna:
- Druzyny lordow 1 344 -> 720 (szczyt 3 627 w dobie 33); zalogi 1 210 -> 2 797 [P, BAL].
- Zginelo ich w roku 4 197 (ARM L129271) [P]. Z rak Innych poleglo 3 382 ludzi (ARM L129159) [P].
- Jeden rabunek w roku i ani jednego szturmu [P, BIT]. Nie probuja przejsc Muru.

Inni - zgodnie z planem:
- Spia do dnia 728 (CS L9242). 195 prob oblezen zablokowanych (CS L9243) [P].
- Rosna tylko z poleglych: 616 -> 3 699 w 4 bandach (ARM L655, L129155); nekromancja +4 452 w roku [P].

Dothrakowie - polowicznie:
- Wojna tylko z House Targaryen. 55 bitew lordow, wygrali 37 [P].
- Nie ruszyli Wolnych Miast ani razu: "wsie i karawany rozbite przez Dothrakow 0" (ARM L129204) [P].
- Horda nie jest konna: jazda 43.9% (prog 60%), 27.8% wojownikow czeka na konia (P5:366, P5:368) [P]. To znana sprawa koni i uprzezy.

Zelazne Wyspy - niezgodnie ze swiatem:
- 342 doby wojny z The Vale i 186 dob z KL. W calym roku 1 bitwa lordow i 0 rabunkow wsi [P, BIT].
- Wojsko 1 218 -> 5 849 (doba 68) -> 1 428. Kiesy rodow 3 137 952 -> 1 274 328 (ARM L606, L129106) [P]. Zaplacili za wojne, ktorej nie stoczyli.

---

## 7. Porownanie z poprzednim biegiem rocznym (WGRANIE 34)

| Miara | poprzedni bieg | ten bieg |
|---|---|---|
| wojsko lordow w krolestwach w wojnie, srednia ostatnich 28 dob | 64 013 | 79 409 |
| druzyny lordow, doba 363 | 67 015 | 77 837 |
| zalogi, doba 363 | 74 147 | 83 593 |
| zalegly zold (28 dob) | 3.17% | 2.49% |
| bitwy lordow w roku | 1 702 (4.68/d) | 1 869 (5.13/d) |
| szturmy / udane | 33 / 24 | 23 / 14 |
| zmiany krolestwa twierdz | 41 (12 twierdz wiecej niz raz) | 14 (zadna wiecej niz raz) |
| bunty - nowe krolestwa | 3 (Pentos, Qohor, Braavos); 1 upadlo | 0 |
| najwieksza strata krolestwa | Qohor 5 -> 1 | Dragonstone 10 -> 6 |
| pokoje fabularne zerwane tego samego dnia | 67 | 73 |
| reparacje nienaliczone (fabularne) | 10 826 600 | 12 313 400 |
| reparacje zaplacone (zwykle wojny) | 683 700 | 213 600 |
| zabici w roku | 246 608 | 260 864 |
| dezercja AI | 21 596 | 13 254 |
| pula wyrzutkow, doba 364 | 9 788 | 11 869 |
| bandy, doba 364 (liczba / ludzi) | 937 / 20 501 | 819 / 17 279 |
| pula + bandy | 30 289 | 29 148 |
| uchodzcy, doba 364 | 176 816 | 187 910 |
| pojmania lordow / okupy | 2 883 / 47.9 mln | 2 919 / 52.3 mln |
| sluby NPC | 113 | 215 |
| turnieje | 552 | 524 |
| zmiany pracodawcy najemnikow | 681 | 764 |

Zrodla: te same skrypty na POPRZ (katalog skrypty\wojna\poprz); wiersze 1 i 4 z progi-rok364\etap2.txt i P2:11-12. Wszystko [P].

Co sie zmienilo:
- Wojsko: +24% w miarze progu. Nadal ponizej podlogi 85 tys. (P2:12), ale spadek od szczytu jest lagodniejszy.
- Mapa: trzy razy mniej zmian pana, zadnych buntow. Nie wiem, czy brak buntow to skutek poprawek rundy 3 (lord nie traci renty), czy przypadek. Jeden bieg tego nie rozstrzyga.
- Wyrzutki i bandy: w sumie tyle samo (29 tys. wobec 30 tys.). Wiecej stoi w puli, mniej chodzi w bandach.
- Bez zmian: wojny fabularne bez konca, najemnicy skaczacy co kilka dob, uchodzcy rosnacy rowno.

Test 120 dob rundy 3 (progi-fin4\etap2.txt): wojsko 101 144 srednio w ostatnich 28 dobach. To byl szczyt z doby 60-120. Bieg roczny pokazuje, ze potem schodzi do ok. 78-79 tys.

---

## 8. Dla pytania Jeffa o podatki "na czas wojny"

Jeff zapytal: skoro 85 tys. wojska nie wyjdzie bez doplat, to gdzie podniesc podatki na czas wojny. Z mojej dzialki sa cztery fakty i trzy propozycje. Liczenie stawek nalezy do rozdzialu o gospodarce - tu tylko rzad wielkosci.

Fakty:
1. "Czas wojny" trwa zawsze. 25 z 29 krolestw ma wojne przez 342-364 doby z 364, Riverlands przez 277 [P, linie "Skarbce", k9]. Podatek wlaczany flaga wojny bylby podatkiem stalym.
2. Podatek wojenny ludu juz jest (paczka 184). W roku zebral 3 882 375 zl, w ostatnim kwartale 14 922 zl dziennie. 20 z 26 krolestw stoi na pulapie 3% (ARM L129100; k12) [P]. To 1.7% rocznego zoldu (228.6 mln) [P].
3. Zloto swiata stoi w miejscu (208.20 mln -> 208.34 mln), ale zmienilo wlasciciela: skarbce krolestw 57.9 mln -> 27.1 mln, kiesy mieszczan 6.3 mln -> 44.1 mln, sakiewki zolnierzy 0.3 mln -> 16.6 mln (ARM L707, L129205) [P]. W ostatnim kwartale skarbcom ubywa 51 tys. zl dziennie, a mieszczanom przybywa 75 tys. zl dziennie [P z L97256 i L129205].
4. Zolnierz druzyny kosztuje ok. 5.7 zl dziennie (443 964 zl zoldu na 77 837 ludzi; ARM L129132, L129266) [S]. Brakuje od 5.6 tys. ludzi (wobec sredniej 79 409) do 11.4 tys. (wobec ostatniej doby 73 578) do podlogi 85 tys. To 32-65 tys. zl dziennie [S].

Czyli: zloto na brakujace wojsko istnieje i kazdego dnia odklada sie u mieszczan w kwocie wiekszej niz potrzeba.

Propozycje (do sprawdzenia w rozdziale o gospodarce):
- A. Podatek wojenny ludu zostaje, ale wyzszy pulap dostaje tylko krolestwo, ktore naprawde walczy: ma armie w polu, oblezenie albo najazd na swoje wsie w ostatnich 30 dobach. Nie flaga wojny. Rzad wielkosci: dzis 1 punkt procentowy daje ok. 6 tys. zl dziennie na swiat [S z 14 922 zl przy sredniej stawce 2.41%], wiec 32-65 tys. zl to pulap rzedu 8-13% zamiast 3% [S, liniowo - podstawy podatku nie sprawdzalem].
- B. Zwyciezca dostaje reparacje jako zwykly przelew ze skarbca przegranego do skarbca wygranego - ale tylko przy prawdziwym pokoju (patrz niespojnosc 1). Dzis 12.3 mln rocznie przepada jako "nienaliczone".
- C. Nie placic za wojny, ktorych nie ma. Kiesy rodow Zelaznych Wysp stopnialy o 1.86 mln w roku wojny prawie bez bitew [P, ARM L606, L129106]. Wezwania sojusznikow kosztowaly skarbce 1.79 mln; 8 z 17 zerwano, w tym 4 po jednej dobie [P, k13, e70.txt].

---

## CO TRZYMA SENS

1. W bitwie wygrywa silniejszy: 1849 z 1869 bitew lordow (98.9%); przy przewadze 1.5 do 1 i wiecej 1609 z 1609 [P, BIT].
2. Twierdze padaja tylko przed duza przewaga: 14 udanych szturmow, najmniejsza przewaga 3.6 do 1, atakujacy traci srednio 29% ludzi; 9 z 23 szturmow obroncy odparli [P, BIT].
3. Nie ma kuli snieznej: najwiekszy zysk +4 twierdze (Reach 16 -> 20, Stormlands 13 -> 17), zadna twierdza nie zmienila pana dwa razy [P, LR].
4. Nikt nie znika: 0 krolestw upadlych, 0 rodow zniklo z 347, 0 rodow z lennem zmienilo strone [P, BR, ARM].
5. Armie maja jedzenie: morale srednio 70.2, w glodzie srednio 2.3 z 624 partii, glodnych warowni 0 przez 364 doby [P, ARM].
6. Armie sa oplacone: zalegly zold 2.49%, za brak zoldu odeszlo 460 ludzi w roku [P, P2:11, ARM].
7. Straty w bitwach sa jak w historii: przegrani traca 22% zabitych i 72% rozbitych, zwyciezcy 1.8% [P, ARM "Przegrani (H3)"].
8. Ksiega ludzi sie zamyka: kontrola puli wyrzutkow 0.00 w 363 dobach, konto uchodzcow 0.0000, rownanie jencow 1.5% obrotu [P, P4:80, P4:107, P4:57].
9. Rabunek wsi nie wybija swiata: 6 422 zabitych z 41 mln ludzi wsi, 28 wiosek spalonych z 2446, ludzi zywych ubywa 0.35% na rok [P, P4:113, P7:114, P4:121].
10. Okupy dzialaja: 1 260 lordow wykupionych za 52.3 mln, nikt nie siedzi ponad 60 dni, tylko 20 okupow bez pokrycia [P, ARM].
11. Wojna zwykla konczy sie pokojem: 7 pokojow po 43-160 dobach, 2 z reparacjami (213 600 zl) [P, ARM, CS].
12. Sojusznik wychodzi z wojny, gdy mu sie nie placi: 8 razy [P, ARM].
13. Nocna Straz nie rabuje i bierze ludzi z Muru, z wyrzutkow i z Westeros (2 145 / 1 439 / 2 658) [P, ARM L129165].
14. Inni spia do dnia 728 i rosna tylko z poleglych (616 -> 3 699); 195 prob oblezen zablokowanych [P, CS L9243, ARM L129155].
15. Sluby NPC ruszyly: 215 w roku wobec 113 [P].
16. Turnieje ida rowno: 524 starty, najdluzsza przerwa 6 dob [P, P7:52].
17. Wojsko trzyma sie lepiej niz w poprzednim biegu: 79 409 wobec 64 013 [P, P2:12].

## NIESPOJNOSCI

1. WOJNY FABULARNE NIE MAJA KONCA. Waga: wysoka.
   Dowod: 73 pokoje uchwalone i 73 zerwane przez ROT tego samego dnia (CS L9240; ARM, e70.txt); 25 z 29 krolestw w wojnie 342-364 doby i Riverlands 277 (linie "Skarbce", k9); "pokoj z biedy" 0 wnioskow (CS L9239); 7 z 14 tych wojen bez zadnej zmiany na mapie.
   Co by naprawilo: po pokoju uchwalonym przez krolestwo ROT wznawia wojne fabularna dopiero po rozejmie gry (87 dni), nie tego samego dnia - wtedy pokoj cos znaczy, a fabula i tak wraca.

2. POKOJ NA ZERO DNI WYPUSZCZA JENCOW ZA DARMO. Waga: srednia.
   Dowod: 440 z 528 lordow uwolnionych "przez pokoj" wyszlo w dobach pokoju fabularnego, po ktorym wojna wrocila tego samego dnia (k10; ARM "Niewola lordow i okupy").
   Co by naprawilo: zniknie razem z punktem 1; bez niego - nie zwalniac jencow, gdy ta sama para jest znow w wojnie przed koncem doby.

3. ZWYCIEZCA NIC NIE DOSTAJE. Waga: srednia.
   Dowod: 12 313 400 zl reparacji nienaliczonych (CS L9240); skarbiec Stormlands (+4 twierdze) 804 381 wobec Dorne (-4) 1 001 618 (ARM L129116, L129107).
   Co by naprawilo: przy prawdziwym pokoju (punkt 1) reparacje jako przelew skarbiec przegranego -> skarbiec wygranego.

4. WOJNA BEZ WALKI KOSZTUJE JAK WOJNA. Waga: srednia.
   Dowod: The Vale - Iron Islands 342 doby, 0 bitew lordow, 0 rabunkow; Zelazne Wyspy 1 bitwa lordow w roku, wojsko 5 849 -> 1 428, kiesy rodow 3.14 mln -> 1.27 mln (BIT; BAL; ARM L606, L129106).
   Co by naprawilo: nie wymuszac wojny fabularnej miedzy krolestwami bez wspolnej granicy albo dac Zelaznym Wyspom rabunek wybrzezy z morza.

5. UCHODZCY NIE WRACAJA. Waga: srednia.
   Dowod: 187 910 na koniec roku, przyrost ok. 675 na dobe, wrocilo 11% (ARM L129272, k7); Sharp Point 31.5% ludzi poza domem, plon x0.43 (LR).
   Co by naprawilo: punkt 1 (30 dob spokoju przychodzi dopiero z prawdziwym pokojem); samo podkrecenie powrotu bez pokoju zrobi ludzi, ktorych zaraz znow wypedza.

6. WYRZUTKI WRACAJA DO DOMU W TEMPIE WOJNY PRAWIE WSZEDZIE. Waga: srednia.
   Dowod: 212 z 227 regionow liczy sie "w wojnie" caly rok (ARM L129135); pula 11 869 wobec progu 3 053 (P4:81); bandy 819 / 17 279 wobec progu 867 / 15 044 (P4:84); wygrane band rosna 4.1 -> 9.8 na dobe (k15b).
   Co by naprawilo: liczyc "wojne regionu" od prawdziwego zagrozenia (najazd albo oblezenie w regionie w ostatnich 30 dobach), tak jak przy powrocie uchodzcow, a nie od flagi wojny krolestwa.

7. NAJEMNICY ZMIENIAJA PANA CO 3 DOBY. Waga: srednia.
   Dowod: 11 kompanii, 764 zmiany pracodawcy, mediana sluzby 3 doby, 151 sluzb jednodniowych, 20-23 pracodawcow na kompanie w roku (BR, k11).
   Co by naprawilo: najkrotszy czas kontraktu liczony w tygodniach, nie w dobach (np. jedna pora roku), i zakaz przejscia prosto do wroga poprzedniego pana.

8. WEZWANIA SOJUSZNIKOW, NA KTORE NIE STAC. Waga: srednia.
   Dowod: 8 z 17 porozumien zerwanych z braku wplywow dnia; Pentos 5 razy wzywa Tyrosh przeciw Myr i 4 razy nie ma na rate; 4 wojny trwaly 1 dobe; skreslono 5 396 992 zl reszt (ARM, k13).
   Co by naprawilo: nie podpisywac porozumienia, gdy rata dzienna jest wieksza niz wplyw dnia skarbca wzywajacego.

9. BANDY BIJA CORAZ WIECEJ MALYCH PARTII. Waga: srednia.
   Dowod: wygrane band 373 -> 700 -> 808 -> 893 na kwartal, przegrane stoja na ok. 650 (BIT linie H3, k15b); ludzi w bandach przybywa 20 na dobe (ARM, k7).
   Co by naprawilo: punkt 6 zmniejszy doplyw; do tego zaloga twierdzy powinna wychodzic na bandy w swoim okregu, gdy lordowie sa na wojnie.

10. KIERUNEK REPARACJI NIE ZGADZA SIE Z BITWAMI. Waga: niska.
    Dowod: Dothrakowie wygrywaja 37 z 55 bitew, a 6 razy to oni mieliby placic (1 615 600); Lorath wygrywa 133 z 210 bitew i 6 razy mialby placic (CS, reparacje.txt; BIT).
    Co by naprawilo: najpierw pomiar - dopisac do linii pokoju wynik wojny, z ktorego Diplomacy liczy kwote; dzis nie wiem, skad sie bierze.

11. DOTHRAKOWIE NIE SA HORDA KONNA I NIE RUSZAJA WOLNYCH MIAST. Waga: niska.
    Dowod: jazda 43.9% (prog 60%), 27.8% czeka na konia (P5:366, P5:368); 0 rozbitych wsi i karawan Wolnych Miast (ARM L129204).
    Co by naprawilo: konie i uprzaz dla hordy (sprawa juz znana z triazu koni); najazdy na Wolne Miasta to osobna decyzja Jeffa.

12. WEZWANIE SKAGOS BEZ WOJNY. Waga: niska.
    Dowod: Nights Watch placi Skagos 3 359 zl dziennie (ARM L18251), a Skagos nie ma ani jednej doby wojny w liniach "Skarbce" i nie ma go w KRONICE.
    Co by naprawilo: sprawdzic w kodzie wezwan, czy wezwany naprawde wchodzi do wojny, zanim poplynie pierwsza rata.

13. BRAK POMIARU SMIERCI I NARODZIN BOHATEROW. Waga: niska.
    Dowod: w ARM i CS nie ma takiej linii; doroslych lordow AI przybylo 435 (ARM L683, L129182), a zgonow nie da sie policzyc.
    Co by naprawilo: jedna linia dobowa z liczba zgonow (bitwa, starosc, egzekucja), narodzin i nowych dworzan BK.

14. DROBNE BLEDY POMIARU W MOJEJ DZIEDZINIE. Waga: niska.
    Dowod: kontrola linii Straz z Westeros 56 ludzi w dobie 175 (P4:264); trafieni spustoszenia poza tolerancja w dobie 229 (P4:243); lordowie ruszajacy noca bez powodu 2.3 na noc wobec progu 2.0 i 4 noce z pobudka przed switem (P7:17-18).
    Co by naprawilo: obejrzec te trzy doby w logu; to pojedyncze zdarzenia, nie trend.
