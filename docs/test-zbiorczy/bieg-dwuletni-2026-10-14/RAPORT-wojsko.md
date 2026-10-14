# BIEG DWULETNI (728 dob, wersja koncowa) - WOJSKO I PIENIADZ

Bieg: `D:\Backup-Bannerlord\logs\2026-10-14-noc\Armoury-2026-10-13_21-14-13.log` + tabele `Logs\2026-10-13_21-14-13` + sesja CS `session-2026-10-13_21-14-11.log` (728/728 dob).
Porownania: bieg roczny kodu 43 (`Armoury-2026-10-13_15-02-59.log`, dalej **W43**) i bieg roczny gospodarki od ludzi (`Armoury-2026-10-13_19-21-12.log`, dalej **GL**).
Miara progu = W5-7: wojsko rodow krolestw w WOJNIE z linii `Skarbce:`, srednia 28 dob okna (doba = dzien gry - 108836 + 1, jak `sprawdz_logi.py`). [P] = pomiar, [S] = szacunek.
Nic poza tym katalogiem nie zapisane, gra nie uruchamiana, kod i repo bez zmian.

## 0. Dla Jeffa - prosto

1. **Nie. Swiat NIE utrzymal 85 tys. w roku 2.** Na koniec roku 2 (doby 701-728) wojsko lordow w wojnie to **55.3 tys.** [P] - 30 tys. pod progiem.
2. Rok 1 byl bardzo dobry: koniec roku 1 **108.9 tys.** [P] (kod 43 mial 101.3, sama gospodarka od ludzi 96.4), a pierwsze 3 miesiace roku 2 nawet **110 tys.** [P].
3. Zalamanie przyszlo nagle, w dobach 451-513: **16 krolestw zawarlo pokoj** w ciagu 2 miesiecy (wojny trwaly od doby 24, ponad rok), w dobie 460 przyszla zima i do konca biegu bylo w wojnie tylko **10-17 krolestw zamiast 26** [P].
4. Krolestwo w pokoju rozpuszcza wojsko prawie do zera: Dolina (Vale) miala 7.8 tys., po 2 miesiacach pokoju 0.5 tys., na koniec 0.05 tys. [P]. Tak samo Dorne, Dothrakowie, Qarth, Qohor, Braavos, Lys, Sarnor - po 0.0-0.2 tys. Zalogi tez topnieja [P].
5. Te krolestwa juz nie wracaja do wojny same: Dolina, Dothrakowie, Qohor, Braavos, Lys, Sarnor staly w pokoju 220-277 dob do konca biegu [P]. Wypowiedzen wojen w roku 2: 65, w roku 1: 112 - ale w roku 1 wiekszosc to automatyczne wznowienia wojen fabularnych ROT (98 do doby 451, potem 13) [P].
6. Pieniadz nie jest glowna przyczyna: **te same 176 rodow, ktore walczyly w obu latach, trzyma 50.7 tys. ludzi wobec 56.5 tys.** rok wczesniej (-10%) [P]. Gdyby mapa wojen zostala jak w roku 1, wyszloby ok. **95-98 tys.** [S].
7. Ale pieniadz tez slabnie: biedne rody tna zold (4-17 tys. zl dziennie w roku 2, w roku 1 2-8 tys.) [P], niepokryta potrzeba panow wzrosla z 24 do 54 tys. zl dziennie [P], zapas skarbcow koron prawie sie skonczyl (64 -> 6 tys. zl dziennie) [P]. Najbiedniejsi: Wolni Ludzie, Zelazne Wyspy, Pentos, Smocza Skala.
8. Zniknely w roku 2 dwa duze krolestwa: **Krainy Burzy (Stormlands, d536)** - jego rody przeszly do Smoczej Skaly - i **Targaryen (Aegon, d459)**; do tego 5 malych buntow [P]. Zdobyc twierdz: 27 w roku 1, **12 w roku 2** [P].
9. Pieniadz swiata jest szczelny: przez 2 lata ubylo 0.8% (209.2 -> 207.0 mln) [P], zloto "z niczego" bez platnika = 0 [P]. Jedna usterka pomiaru: licznik podatku jednej wsi skacze o 2.1 mld (5 razy w ostatnim miesiacu) - nikt tego zlota nie dostal, ale linia "Pieniadz swiata" pokazuje 4.5 mld [P]; przyczyna - nie wiem.
10. Z 10 progow alarmowych rachunku: **6 TAK** (miara, biedne rody, saldo rodow, luz dla N7, mapa wojen, Dorne), **3 NIE** (kiesy ludu i danina, klif, skarbce i dlug koron), 1 nie do zmierzenia (N7). Rachunek na sucho liczyl najgorszy realny przypadek 73 tys. - nie przewidzial, ze polowa krolestw wyjdzie z wojny.

## 1. Odpowiedz wprost

**Czy swiat utrzymal 85 tys. w roku 2? NIE.** d701-728: **55.3 tys.** [P] (narzedzie progow `--grupa w5 --do-doby 728`, W5-7: 55.3 - to samo). Kwartaly roku 2: 110.4 / 59.9 / 56.7 / 55.1 tys. [P]. Ponad 85 tys. swiat byl tylko do ok. doby 455 (dobowo: d448 108.5, d455 84.4) [P]; minimum roku 2: **36.1 tys. w d537** [P].
Przyczyna w liczbach (zmiana d337-364 -> d701-728, -53.6 tys.): rody z wojny do pokoju **-36.6 tys.**, rody mieszane (wojna/pokoj na zmiane) -7.4, **te same rody w wojnie -5.9** (pieniadz), rody tylko w jednym oknie -1.7 [P] (`a8_te_same_rody.out.txt`).

## 2. Miara W5-7 - okna [P]

| okno | bieg dwuletni | krolestw w wojnie | wojsko wszystkich krolestw (tez w pokoju) | W43 (kod 43) | GL (gosp. od ludzi) |
|---|---:|---:|---:|---:|---:|
| d93-120 | **99.5** | 25.0 | 100.2 | 112.2 | 101.6 |
| d337-364 | **108.9** | 26.0 | 109.0 | 101.3 | 96.4 |
| d701-728 | **55.3** | 17.3 | 57.4 | - | - |

Kwartaly (91 dob) [P]:

| kwartal | dwuletni | krolestw w wojnie | wojsko wszystkich | W43 | GL |
|---|---:|---:|---:|---:|---:|
| Q1 d2-91 | 75.4 | 20.6 | 82.5 | 82.3 | 75.5 |
| Q2 d92-182 | 102.1 | 25.6 | 102.5 | 109.8 | 101.7 |
| Q3 d183-273 | 104.7 | 26.7 | 104.9 | 105.3 | 101.0 |
| Q4 d274-364 | 107.8 | 26.2 | 107.9 | 104.1 | 97.9 |
| Q5 d365-455 | **110.4** | 25.9 | 110.7 | | |
| Q6 d456-546 | **59.9** | 14.0 | 70.0 | | |
| Q7 d547-637 | **56.7** | 14.7 | 60.0 | | |
| Q8 d638-728 | **55.1** | 15.1 | 58.2 | | |

Okna 28 dob roku 2 (miara / krolestw w wojnie) [P]: d365-392 112.4/26.0, d393-420 111.0/26.0, d421-448 109.7/26.0, **d449-476 83.0/19.0, d477-504 68.2/15.7, d505-532 47.7/11.6, d533-560 43.9/10.1**, d561-588 50.8/13.9, d589-616 62.7/16.8, d617-644 61.4/16.0, d645-672 57.1/14.5, d673-700 53.4/13.6, d701-728 55.3/17.3. Pelna tabela: `a2_tabele.out.txt` (A).

## 3. Co sie stalo w dobach 451-513 [P]

- **Fala pokojow:** Lys i Tyrosh d451, Lorath d453, Dorne / Qarth / Reach d455, Volantis d460, Qohor / Norvos d463, Sarnor d477, Braavos / Ibben d495, Dolina / Dothrakowie / Targaryen (Meereen) d508, Myr d513 - 16 krolestw bez zadnej wojny (`a3_krol_przebieg.out.txt`). Wojny te trwaly od doby 24. Pokoje z decyzji krolestw (`ByKingdomDecision`, reparacje Diplomacy "wojna niefabularna"); **"pokoj z biedy" - 0 wnioskow przez caly bieg** (CS `Pokoj z biedy (E1b)`).
- **Zima od doby 460** (Year 300 Day 97, linia `Zima (E4/0)`: pora Winter) do konca biegu; ciecie plonu wsi 48.6% (zimne krainy) / 15.7% (cieple), jedzenie partii +43%.
- **Malo nowych wojen miedzy duzymi krolestwami:** wypowiedzen 112 w roku 1, 65 w roku 2 (wiele z malymi buntami); pokojow 96 / 63; krolestw zniszczonych 1 / 6 (linie `Wojna i pokoj (E7/0)`). Wojny fabularne ROT (np. Wolni Ludzie - Straz) wraca sama gra ROT (`ROTStorylineWars.EnforceWars`): wznowionych po pokoju 98 do doby 451 i 111 do konca (CS `Reparacje (T9)`); wojny, ktore skonczyly sie w d451-513, to wojny "niefabularne" - nikt ich nie wznawia.
- **Wojsko w pokoju znika:** 132 rody, ktore przeszly z wojny do pokoju: ludzie w partiach 38 125 -> 1 486, limit partii 54.7 -> 28.2 tys., zold 250 -> 12 tys. zl/d, zalogi 29.4 -> 6.5 tys., a kiesy rodow ROSNA 26.4 -> 29.0 mln [P]. Przyklad Dolina: d504 7 761, d518 2 744, d560 482, d728 25 ludzi; pulap rodu Corbray 14 376 -> 3 827 zl/d dzien po pokoju, zwolnienia codziennie [P] (budzet-rodow.csv).
- **Powrot do wojny odbudowuje wojsko:** Dorne d537 450 -> d616 8 536; Volantis d565 132 -> d630 4 017; Qarth d561 318 -> 1 987 [P]. Ale krolestwa bez wojska nie wypowiadaja wojen: Dolina, Dothrakowie, Qohor, Braavos, Lys, Sarnor w pokoju 220-277 dob do konca biegu [P]. Mechanizm "slabe krolestwo nie chce wojny" - [S], w logu nie ma powodu decyzji AI.
- Dlaczego wszystkie te wojny skonczyly sie naraz - **nie wiem** (hipotezy [S]: zmeczenie wojna Diplomacy po ok. 430 dobach; zima; skarbce koron na zapasie 500 tys.).

## 4. Wojsko na krolestwo (wszystkie krolestwa, srednia okna, ludzie) [P]

| krolestwo | d93-120 | d337-364 (rodow, dob wojny) | d701-728 (rodow, dob wojny) | rok 2 - rok 1 | W43 d337-364 | d728 - W43 |
|---|---:|---|---|---:|---:|---:|
| The North | 10 409 | 10 810 (26, 28) | 10 324 (26, 28) | -486 | 12 626 | -2 302 |
| King's Landing | 8 622 | 8 816 (28, 28) | 8 623 (29, 28) | -193 | 8 017 | +606 |
| The Reach | 7 187 | 8 212 (17, 28) | 8 939 (17, 28) | **+727** | 7 436 | +1 503 |
| The Vale | 7 336 | 7 453 (21, 28) | **47** (21, 0) | **-7 405** | 8 321 | -8 273 |
| Riverlands | 431 | 7 228 (15, 28) | 8 110 (16, 28) | **+883** | 6 871 | +1 239 |
| Stormlands | 4 417 | 7 158 (14, 28) | **zniklo d536** | **-7 158** | 7 498 | -7 498 |
| Dorne | 6 035 | 6 709 (40, 28) | **86** (40, 0) | **-6 624** | 6 673 | -6 587 |
| Targaryen (Meereen) | 3 056 | 4 883 (10, 28) | 992 (9, 0) | -3 890 | 3 202 | -2 209 |
| Nights Watch | 3 897 | 4 225 (6, 28) | 1 615 (4, 28) | -2 610 | 4 348 | -2 733 |
| Dothraki Horde | 2 714 | 4 183 (20, 28) | 220 (20, 0) | -3 963 | 3 953 | -3 733 |
| Iron Islands | 4 944 | 3 800 (34, 28) | 2 777 (33, 28) | -1 023 | 4 991 | -2 214 |
| Volantis | 3 738 | 3 716 (7, 28) | 792 (7, 16) | -2 924 | 1 329 | -537 |
| Qarth | 2 324 | 3 148 (6, 28) | 60 (6, 0) | -3 087 | 2 797 | -2 737 |
| Dragonstone | 4 416 | 3 144 (12, 28) | 5 778 (26, 28) | **+2 634** | 3 500 | +2 278 |
| Myr | 3 533 | 3 051 (6, 28) | 315 (4, 11) | -2 736 | 2 367 | -2 052 |
| Ibben | 2 438 | 2 633 (6, 28) | 365 (3, 23) | -2 268 | 2 166 | -1 800 |
| Qohor | 2 945 | 2 559 (8, 28) | 17 (6, 0) | -2 542 | 3 945 | -3 928 |
| Braavos | 2 703 | 2 482 (8, 28) | 34 (6, 0) | -2 449 | 2 145 | -2 111 |
| Pentos | 2 747 | 2 397 (11, 28) | 2 715 (12, 28) | +319 | 515 | +2 200 |
| Sarnor | 2 537 | 2 339 (6, 28) | 79 (7, 0) | -2 260 | 1 464 | -1 386 |
| Targaryen (Aegon) | 2 525 | 2 263 (6, 28) | **zniklo d459** | -2 263 | 0 | 0 |
| Norvos | 2 335 | 2 035 (6, 28) | 235 (7, 19) | -1 800 | 2 126 | -1 891 |
| Tyrosh | 1 972 | 1 535 (7, 28) | 245 (6, 11) | -1 290 | 1 206 | -961 |
| Lys | 1 714 | 1 448 (7, 28) | 36 (6, 0) | -1 412 | 1 160 | -1 124 |
| Lorath | 1 878 | 1 351 (6, 28) | 92 (9, 0) | -1 259 | 1 518 | -1 425 |
| Free Folk | 3 083 | 1 306 (11, 28) | 795 (11, 28) | -510 | 1 656 | -861 |
| Skagos | 110 | 16 (1, 0) | 1 178 (2, 28) | +1 162 | 798 | +380 |
| nowe w roku 2 (Mallarawan, Emeros Conspiracy, Vhassar League, Joth, Ny Sar, Chroyane i in.) | 0 | 0 | ok. 2 850 | +2 850 | 0 | +2 850 |
| **RAZEM** | **100 198** | **109 013** | **57 431** | **-51 582** | **103 302** | **-45 871** |

Zyskali wobec roku 1: Smocza Skala (+2.6 tys. - wchlonela rody Krain Burzy, 12 -> 26 rodow), Skagos (+1.2), Riverlands (+0.9), Reach (+0.7), Pentos (+0.3). Stracili: wszystkie krolestwa w pokoju (Dolina -7.4, Dorne -6.6, Dothrakowie -4.0, Targaryen -3.9, Qarth -3.1, Volantis -2.9, Myr -2.7, Qohor -2.5, Braavos -2.4, Ibben / Sarnor -2.3, Norvos -1.8, Lys -1.4, Tyrosh / Lorath -1.3) i zniszczone (Krainy Burzy -7.2, Aegon -2.3); w wojnie przez caly czas: Straz -2.6 (bunt "Confederation of Snow" zabral rody), Zelazne Wyspy -1.0, Wolni Ludzie -0.5, Polnoc -0.5 [P].
Krolestwa w wojnie w obu oknach (Polnoc, KL, Reach, Riverlands, Smocza Skala, Wyspy, Pentos, Wolni Ludzie, Straz): 49.9 -> 49.7 tys. [P].

**Te same rody** (`a8_te_same_rody.out.txt`) [P]:

| grupa rodow (d337-364 -> d701-728) | rodow | ludzie w partiach | wypelnienie limitu | kiesy rodow (mln) | zalogi |
|---|---:|---|---|---|---|
| w wojnie w obu oknach | 176 | 56 544 -> 50 683 (**-10.4%**) | 63.2% -> 58.1% | 35.8 -> 37.1 | 48 484 -> 47 822 |
| z wojny do pokoju | 132 | 38 125 -> 1 486 | 69.7% -> 5.3% | 26.4 -> 29.0 | 29 398 -> 6 462 |
| mieszane | 28 | 9 578 -> 2 191 | 62.3% -> 21.0% | 4.3 -> 4.7 | 8 672 -> 3 282 |

Szacunek "gdyby mapa zostala": 108.9 x 0.896 = 97.6 tys.; z utrata Aegona i Krain Burzy ok. **95-98 tys.** [S].

## 5. Zalogi [P]

| | d93-120 | d337-364 | d701-728 |
|---|---:|---:|---:|
| zalogi krolestw (balans-krolestw.csv) - dwuletni | 81.2 tys. | 89.2 tys. | **58.2 tys.** |
| W43 / GL | 86.3 / 83.2 | 85.0 / 85.2 | - |
| partie lordow krolestw (ta sama tabela) | 100.1 | 108.9 | 57.3 |
| bez krolestwa: partie | 2.1 | 5.2 | 8.9 |
| zalogi rodow w wojnie (budzet-rodow.csv) | 78.9 (d85-112) | 89.1 | 50.3 |

Zalogi wedlug krolestw (d337-364 -> d701-728): Polnoc 9 388 -> 9 322, KL 7 775 -> 7 683, Reach 7 656 -> 9 021, Wyspy 5 104 -> 5 120, Pentos 2 115 -> 2 193 (w wojnie - stoja); **Dolina 5 473 -> 877, Dorne 5 093 -> 1 510, Targaryen 4 004 -> 1 308, Volantis 3 964 -> 1 145, Qohor 2 458 -> 241, Braavos 2 330 -> 376, Qarth 1 841 -> 93, Lys 1 603 -> 90** (pokoj); Krainy Burzy 5 770 -> 0; Smocza Skala 1 004 -> 4 961 [P] (`a4_csv.out.txt`). Twierdz w krolestwach caly czas 227 [P].

## 6. Dziesiec progow alarmowych (rozdz. 7.6 rachunku) - TAK = alarm

| # | co | rok 1 (d337-364) | rok 2 - pomiar | ALARM? |
|---|---|---|---|---|
| 1 | miara W5-7 | 108.9 tys. (>= 95 - OK) | koniec roku 2 **55.3** (< 90); spadek d421-448 -> d701-728 **-54.4 tys.**; bez mapy (te same rody) -5.9 tys. = -10.4% [P] (na pelnej mapie ok. -11 tys. [S]) | **TAK** |
| 2 | kiesy ludu swiata / danina P1 | 23.9 mln / 419 tys. zl/d | kiesy min 22.5 mln (d421-448), koniec 34.1 mln; danina ponizej 170 tys. tylko w 1 oknie (d533-560: 152.5), koniec 210 tys. [P] | **NIE** (na granicy: 1 okno) |
| 3 | biedne rody (G < R, w wojnie): ludzie / G/R | 28.4 tys. / 0.56 | min **12.0 tys.** (d561-588), koniec 17.5 tys. / 0.55; spadek > 1.5 tys. na 28 dob w 3 oknach (d449-476 -6.0, d505-532 -3.5, d533-560 -5.3); G/R min 0.52 [P]. Udzial biednych w wojsku w wojnie 27% -> 33% | **TAK** (glownie przez pokoje) |
| 4 | rody pod klifem (G < 0.25 R): ludzie | 1.7 tys. | max **2.19 tys.** (d673-700); ponad oczekiwane 2 tys. w 3 oknach [P] | **NIE** |
| 5 | saldo rodow bez luzu (trend 56 dob) / dlug Banku rodow | -3.2 (d253-364), -10.4 (d309-364) tys. zl/d | **-59.9 (d365-420), -62.4 (d421-476)**, potem -20.5, +12.0, -28.5, -20.6, -14.9 (d673-728) tys. zl/d; caly rok 2 -10.8; dlug Banku rodow max 0.73 mln [P] | **TAK** (2 okna ponizej -45 na poczatku roku 2) |
| 6 | skarbce koron w wojnie / dlug koron w Banku / zwrot niepelny | 22.3 / 0.94 mln | skarbce w wojnie 7.2 mln (wszystkie 15.7 mln - w pasmie 15-17); dlug koron max **1.35 mln** (d505-532), koniec 0.42; krolestw ze zwrotem niepelnym >= 14 dob okna: max 1 [P] | **NIE** |
| 7 | luz w puli zoldu do limitu (paliwo N7) | 19.6 tys. (d93-120: 23.5) | d589-616 19.6; **d701-728 10.3 tys.** (rodow z budzetem w wojnie 132 zamiast 210) [P] | **TAK** |
| 8 | N7 - skutek w mierze | - | nie do wydzielenia w jednym biegu; surowo d93-120: 99.5 wobec W43 bez N7 112.2 (-12.7) i GL z N7 101.6 [P]. Odebrani: 120.7 tys. w roku 1, 70.6 tys. w roku 2 (194/d) [P] | **nie wiem** (formalnie TAK) |
| 9 | mapa: krolestw w wojnie / wojsko bez krolestwa | 26.0 / 5.2 tys. | **17.3 (min 10.1 w d533-560)** / 8.9 tys. [P] | **TAK** (krolestwa w wojnie) |
| 10 | Polnoc / Dorne / Zelazne Wyspy | 10.8 / 6.7 / 3.8 tys. | koniec 10.3 / **0.09** / 2.8 tys.; Wyspy min 1.4 tys. (d533-560); Dorne w wojnie d589-616 7.5 tys. [P] | **TAK** (Dorne - w pokoju od d628) |

Razem: **6 TAK** (1, 3, 5, 7, 9, 10), **3 NIE** (2, 4, 6), **1 nie wiem** (8). Progi 1, 3, 7, 9, 10 wywala glownie mapa (pokoje); prog 5 to pieniadz (poczatek roku 2, jeszcze przy pelnej mapie).
Uwaga do 3: w rachunku "dzis" 21.5 tys. dotyczylo W43; w tym biegu rok 1 mial 28.4 tys. biednych w wojnie. 87 rodow biednych na koniec roku 2 (grupa wedlug konca): G/R 0.85 -> 0.54, **zold przyciety 3.8 -> 6.0 tys. zl/d (szczyt 16.7 tys. w d505-532)**, ludzie 23.4 -> 16.0 tys. [P] (`kryt/k6_biedni_dw728.out.txt`).

## 7. Pieniadz koron i doplaty: srednia zl/d (suma) [P]

| strumien | W43 rok 1 | GL rok 1 | dwuletni rok 1 | **dwuletni rok 2** | d701-728 |
|---|---:|---:|---:|---:|---:|
| danina P1 | 135.9 tys. | 150.5 | 274.2 (99.5 mln) | **273.3 (99.5 mln)** | 210.2 |
| W5 wyplacono (gotowka) | 34.7 | 33.3 | 42.7 (15.5 mln) | **32.9 (12.0 mln)** | 28.3 |
| W5 reszta do rent | 93.0 | 103.8 | 197.3 | 200.0 | 166.4 |
| W5b ze skarbca | 16.6 | 8.3 | 9.1 (3.3 mln) | **3.5 (1.3 mln)** | 0.0 |
| W6 tarczowe | 1.2 | 0.7 | 1.1 (0.4 mln) | **2.1 (0.8 mln)** | 0.4 |
| W7 stawka pana (lud / wsie) | - | 10.4 | 12.1 (4.4 mln) | **14.7 (5.4 mln; 10.3 / 4.4)** | 14.7 |
| wplywy dnia koron (165) | 599.7 | 604.1 | 686.7 | 698.1 | 569.4 |
| zwrot zoldu 70% | 287.8 | 269.4 | 271.6 | 226.0 | 167.3 |
| renty 180 | 302.3 | 331.6 | 391.0 | 403.8 | 333.1 |
| 1/360 zapasu koron | 66.7 | 67.7 | 64.4 | **12.0** | 6.1 |
| potrzeba W5 | 78.4 | 80.4 | 87.5 | 98.2 | 85.7 |
| niepokryte po W5 / W5b / W6 | 11.5 | 27.4 | 23.7 | **53.9** | 53.4 |
| N7 odebrani przez druzyny (ludzi/d) | - | 299 | 333 (120.7 tys.) | **194 (70.6 tys.)** | 156 |
| N7 czekaja (stan) | - | 2 101 | 2 136 | 1 635 | 1 765 |

**Ile daly w roku 2 w ludziach** [S] (przy zoldzie 6.60 zl/d na czlowieka rodow z doplata - W5-9): W5 ok. 5.0 tys., W5b ok. 0.5, W6 ok. 0.3, W7 ok. 2.2 tys. (linia W7: rody z W7 trzymaja 5.7-5.9 tys. ludzi [P]) - razem ok. 8 tys. (w roku 1 ok. 10 tys.). N7: 194 ludzi/d wraca pod choragiew [P]; ile z nich to wojsko ponad to, co pan i tak by zwerbowal - **nie wiem**.
Niepokryte na koniec (tys. zl/d, linia W5b-4/W6-7) [P]: Wolni Ludzie 19.2, Zelazne Wyspy 15.7-17.9, Pentos 8.7-9.0, Smocza Skala 3.1-9.8, Tyrosh 1.0. W5-4: wypelnienie rodow z doplata 43.4% -> 40.5% - **NIE** [P].

## 8. Pieniadz swiata [P]

| | d2 | d364 | d728 |
|---|---:|---:|---:|
| razem (bez skoku licznika, patrz nizej) | 209.18 mln | 208.69 mln | **207.04 mln** |
| skarbce krolestw | 57.80 | 23.75 | 15.67 |
| Bank Zelazny | 5.00 | 3.90 | 5.08 |
| glowy rodow | 54.87 | 66.20 | 69.50 |
| kiesy ludu miast / podzamcza | 6.75 / 0.76 | 20.49 / 3.14 | 29.09 / 5.29 |
| sakiewki ludzi (zolnierzy) | 0.53 | 24.24 | 11.47 |
| kasy miast | 24.04 | 11.93 | 11.55 |

- Rok 2: -1.65 mln (-4.5 tys. zl/d, -0.8%); rok 1: -0.49 mln. W43 rok 1: 208.06 -> 206.67, GL: 208.35 -> 206.82.
- Bilans przyczyn rok 2 (d365-698, bez dob skoku): zrodla z niczego 664 tys. zl/d, ujscia 662 tys., netto -6.9 tys., **reszta [R] -0.55 tys. zl/d** (rok 1 -0.35).
- **Zrodla bez platnika = 0:** bitwy netto = ZRODLA 21 566 - UJSCIA 11 939 - zloto partii bez wodza 9 627 = **0 zl/d** (rok 1: 32 381 - 15 715 - 16 666 = 0; W43 tak samo); wyplata dochodu notablom 89 177 wobec przelewu z kapitalu 89 313 (= 0); regulator kas, "zakupy" mieszkancow, lup z oblezen, turnieje, statki, odszkodowania, pasmo i nowi notable, jency do kas - 0 we wszystkich 727 dobach; jency sprzedani 1 zl/d.
- **USTERKA:** licznik podatku wsi (zloto w drodze do panow) skacze o ok. 2^31 = 2 147 483 648: d699 +, d707 -, d709 +, d716 -, d725 + (podwojnie, 4 294 965 384). Linia `Pieniadz swiata` pokazuje wtedy 2.35-4.50 mld, `bilans - przyczyny` ma ujscia ujemne (-2.147 mld); w dobie skoku linia `Utarg wsi (112)` ma "z nadwyzka rozliczona przez BK 1" i licznik pana 2 147 561 580; przy zbiorze "bez odbiorcy 2 147 538 814". Kiesy glow rodow i wsi bez skoku - **nikt tego zlota nie dostal**. Przyczyna (BK? przepelnienie int?) - **nie wiem**.

## 9. Zdobycia i krolestwa [P]

- Twierdze zdobyte oblezeniem: **27 w roku 1, 12 w roku 2** (39 razem; szturmy zdobyte 26 / 12); W43 rok 1: 18, GL: 30. Lup miast: 39 nastepstw (laska 18, grabiez 14, spustoszenie 7; rok 2: 12). Upadki stolic (L5b-1): 3, wszystkie w roku 1 (Dragonstone d106, Mantarys d166, Oros d182; 0.42 mln zl). Karawany w zdobytych miastach: 0.
- **Zniknely:** rok 1 - Qar Deeth League (bunt Qarthu, d290); rok 2 - **Targaryen (Aegon) d459, Krainy Burzy d536** (14 rodow -> Smocza Skala), Quarro Conspiracy (bunt Norvos) d562, County of Valysar d616, Emeros d636, County of Tolero Aeksio d657, Barony of Ny Sar d719 (linie `Skarbce`; licznik E7 "krolestw zniszczonych": 1 / 6).
- Nowe w roku 2 (13, male - 0.1-0.9 tys. ludzi): Duchy of Valyria, Quarro Conspiracy, Confederation of Snow (bunt Strazy), Emeros, Quarro Empire, Tolero Aeksio, Valysar, Ny Sar, Mallarawan, Emeros Conspiracy, Joth, Vhassar League (bunt Volantis), Chroyane.

## 10. Wobec rachunku na sucho (WYNIK-RACHUNEK-85-TYS.md rozdz. 7)

| | rachunek | pomiar |
|---|---|---|
| koniec roku 1, S3 (nowa kampania wedlug ludzi) | 93.3 (87.9-99.1) | **108.9** [P] (GL bez W7 / lupu: 96.4) |
| rok 2-3, S3 3/4, srodek z N7 | 90.5 | **55.3** [P] |
| najgorszy realny przypadek | 73 | 55.3 - 18 nizej [P] |
| skladnik "mapa" | -1.5 tys. (2 krolestwa wybite) | ok. -44 tys. (rody z wojny do pokoju -36.6, mieszane -7.4) [P] |
| skladnik "pieniadz" rok 2 | ok. -6 tys. (-5.9% S0) | te same rody -10.4% [P] = ok. -11 tys. na pelnej mapie [S] |

Rachunek dobrze trafil kierunek pieniadza (kiesy biednych rodow, wygasanie 1/360, saldo), ale nie mial w modelu tego, ze krolestwo w pokoju schodzi do zera i nie wraca do wojny.

## 11. Czego nie wiem

- Dlaczego 16 krolestw zawarlo pokoj w dobach 451-513 i dlaczego nie wypowiadaja nowych wojen (log nie podaje powodu decyzji AI; zima od d460, wojny od d24 - [S]).
- Czy to los tej kampanii, czy regula: jeden bieg dwuletni; biegi roczne W43 i GL nie dochodza do doby 450.
- Skutek N7 w mierze (nie da sie wydzielic bez biegu z N7 wylaczonym).
- Przyczyna skoku licznika podatku wsi o 2^31.
- Czy krolestwa w pokoju z zalogami 0.1-0.9 tys. (Dolina, Qarth, Lys, Qohor, Braavos) straca twierdze przy pierwszym ataku - w roku 2 zdobyc bylo malo (12).

## 12. Pliki (ten katalog) i polecenia

- `RAPORT.md` (ten plik).
- `sl_w5.out.txt`, `sl_w6.out.txt`, `sl_w7.out.txt`, `sl_n7.out.txt`, `sl_lup.out.txt`, `sl_lup5b.out.txt` - narzedzie progow, `--do-doby 728` (w5: 18 progow / 1 NIE W5-4; w6 10 / 0; w7 11 / 0; n7 9 / 0; lup 27 / 0; lup5b 10 / 0).
- `a1_log.py` -> `a1_{dw,w43,gl}.json` (serie dobowe z logow); `a2_tabele.py` -> `a2_tabele.out.txt` (miara, krolestwa, wojny, doplaty, pieniadz); `a3_krol_przebieg.py` (krolestwa co 14 dob, wojna/pokoj); `a4_csv.py` (zalogi, rody biedne, klif, saldo per okno); `a5_saldo56.py` (saldo w oknach 56 dob - na W43 odtwarza -26.5 / -44.8 z rachunku); `a6_okna.py` (doplaty i Polnoc / Dorne / Wyspy po oknach); `a7_bitwy.py` (bitwy netto); `a8_te_same_rody.py` (te same rody).
- `kryt\` - kopie skryptow krytyki z `sucho85\synteza\krytyka` uruchomione na biegu dwuletnim: `k1_przebieg_dw728.out.txt` (uwaga: kolumny opisane "d337-364" i "d309-364" to tam ostatnie 28 / 56 dob biegu), `k2_lud_dw728.out.txt`, `k4_bilans_od{253,365,617}.out.txt` (naglowek "d617-364" to blad napisu - liczy do konca), `k6_biedni_dw728.out.txt`, `k7_klif_dw728.out.txt`, `rody\k5_luz_{120,364,616,728}.out.txt`.

```
L=D:\Backup-Bannerlord\logs\2026-10-14-noc
python -I sprawdz_logi.py --grupa w5 --do-doby 728 L\Armoury-2026-10-13_21-14-13.log   (tak samo w6, w7, n7, lup, lup5b)
python -I a1_log.py --log L\Armoury-2026-10-13_21-14-13.log --wy dw   (oraz 15-02-59 -> w43, 19-21-12 -> gl)
python -I a2_tabele.py ; python -I a3_krol_przebieg.py ; python -I a4_csv.py ; python -I a6_okna.py ; python -I a8_te_same_rody.py
python -I a5_saldo56.py L\Logs\2026-10-13_21-14-13 365-420 421-476 477-532 533-588 589-644 645-700 673-728
python -I a7_bitwy.py L\Armoury-2026-10-13_21-14-13.log
```
