# Rozpoznanie towarow: ile dzis na czlowieka, ile "powinno" byc (2026-10-13)

Praca do odczytu i projektu. Nic w grze, kodzie, ustawieniach ani zapisach nie zostalo zmienione. Zadnych commitow.
Znaczniki: **[P]** = pomiar (log, tabela CSV, plik gry, kod), **[S]** = szacunek (wiedza ogolna o sredniowieczu albo przeliczenie z zalozeniem).
Kod naszego moda: `...\ae222cb7-...\scratchpad\zb\int\Armoury\src` (galaz w-toku/zbior-e8), gra: `...\scratchpad\dec32\cs_full`, BK: `...\scratchpad\e4tools\bkfull`.

## 0. Najkrocej (dla Jeffa)

1. **Dzis ilosc towaru nie zalezy od liczby ludzi.** Kazda wies z symbolem zboza daje ok. 53 sztuki zboza na dobe - na Polnocy (51.6 tys. ludzi na taka wies), w Reach (166.9 tys.) i na Zelaznych Wyspach (16.1 tys.) tyle samo [P]. Przy Twojej zasadzie i tej samej sumie swiata: wies zbozowa Polnocy 39 sztuk, Reach 128, Wysp 12, Volantis 148 [S z pomiaru].
2. **Gra ma trzy rozne "skale" naraz** [P]: zolnierz zjada 1 sztuke jedzenia na 33.7 osobodnia (prawie jak w zyciu), mieszczanin 1 sztuke na 1041 osobodni (31 razy mniej), a chlop je "na papierze" (racje w spichlerzu okregu, nie towar). Dlatego wojsko (0.3% ludzi) potrafi wykupic polowe plotna, skory i sukna swiata.
3. **Skala targu wyznaczona z jedzenia: 1 sztuka na targu stoi za ok. 37 prawdziwymi sztukami** (widelki 28-73). W tej skali wiekszosc towarow jest w dobrych proporcjach: zboze, mieso, nabial, welna, sukno, len, plotno, skory, skora, narzedzia, drewno uzytkowe [S].
4. **O rzad wielkosci za malo (w calym swiecie, nie w jednym regionie):** piwo (14x), wino (50x), ceramika (13x), opal dla miast (ponad 100x - mieszczanie prawie nie pala drewna). **Kilka razy za malo:** sol (4.5x), glina (4x), oliwa (7x). **Za duzo:** ryby (6.5x wobec zboza), zelazo na granicy (3x) [S].
5. **Plotna brakuje w 88 z 97 miast, choc jest "w skali"** [P]. Powod: produkcja jest symbolem (1:37-45), a czesc odbiorcow bierze w skali 1:1 - wojsko potrzebuje 80 sztuk plotna na dobe przy produkcji 157, do tego warsztaty zbrojne. Sama zasada "od ludzi" tego nie naprawi.
6. **Zasada "od ludzi" zmieni podzial, nie mieszanke.** Ludne krainy dostana wiecej tego, co ich wsie maja w symbolu - ale Volantis i Qarth (17% ludzi swiata) nie maja ani jednej wsi lnianej, Qarth nie ma wsi zbozowej, rybackiej, solnej ani gorniczej; Reach (16% ludzi) ma 1 wies lniana z 20 [P]. Braki lnu i soli sa swiatowe - nie znikna same.
7. **Dwie dziury w obiegu znalezione przy okazji** [P]: co dobe przepada ok. 1 114 sztuk jedzenia razem z rozwiazywanymi druzynami (prawie cale jablka, marchew, pomarancze, miod), a ksiega spichlerzy wyrzuca ok. 8 mln racji nadwyzki dziennie (ok. 400 tys. sztuk zboza w skali 1:1), ktorej targ nigdy nie widzi.

## 1. Zrodla i metoda

- Bieg roczny: doby 108836-109199 (364 doby), log `D:\Backup-Bannerlord\logs\2026-10-13-noc34-35\Armoury-2026-10-13_06-24-10.log`, tabele `...\Logs\2026-10-13_06-24-10\`. W biegu byly tylko lato (128 dob) i jesien (235 dob), **zimy nie bylo** - ciecie plonu zima 0 [P linie "Zima (E4/0)"].
- Ludzie swiata: 51 985 925 (doba 1) -> 51 471 311 (doba 364); wsie 41.52 -> 41.11 mln, miasta 10.47 -> 10.36 mln [P ludzie-regiony.csv]. "Na 1000 ludzi" = przez srednia roku 51.73 mln.
- Towary: linie dzienne "Towary: dzien N - <towar>" ksiegi towarow (17 472 linie, 48 towarow x 364 doby). Ksiega mierzy kazda zmiane liczby sztuk w rosterach gry w "ramkach" znanych zdarzen (`GoodsLedger.cs:22-52`), bilans sie zamyka: zapas wczoraj + produkcja - zuzycie = zapas dzis + "bez wyjasnienia". W ksiedze jest 92 towarow, ruch albo zapas ma 48 [P linia "Towary (bilans)", log l.555].
- Okna: **start** = doby 2-29 (bez doby 1, bo zawiera "rozdanie startowe" gry), **koniec** = doby 337-364, **rok** = doby 2-364.
- Miasta bez towaru: log podaje je tylko dla 7 surowcow (linia "Ceny surowcow": ruda, drewno, skory, skora, len, plotno, welna) i dla strzal; dla reszty towarow **pomiaru nie ma**.
- Typy wsi: `Modules\ROT-Map\ModuleData\settlements.xml` (572 wpisy) + 28 podmian "Klimat wsi (T8)" z logu l.269. Moja liczba wsi wedlug typu rozni sie od linii "Produkcja wsi - model" (log l.464) o 1-4 wsie w czterech typach (zboze 123 wobec 124, rybacy 66 wobec 67, bydlo 24 wobec 25, winnice 27 wobec 23) - **nie wiem**, skad roznica; nie zmienia wnioskow.
- Ludzie wsi: `ludzie_wsie` regionu z ksiegi ludzi [P], rozdzielone miedzy wsie regionu wedlug hearth z XML [S - podzial wewnatrz regionu]. Krolestwo = wlasciciel regionu w dobie 364.
- Skrypty i wyniki posrednie: `gosp-ludzie\skrypty-towary\` (lista w rozdz. 10).

## 2. Co znaczy "sztuka" - fakty z kodu i logu

| Fakt | Zrodlo |
|---|---|
| Kazdy towar handlowy gry wazy 10 kg na sztuke (zboze, mieso, skory, narzedzia; welna, len, plotno, sol, ryby, piwo, wino itd.) | `DefaultItems.cs:119-126` (10f), `Modules\SandBoxCore\ModuleData\items\horses_and_others.xml` (weight="10") [P] |
| Zywiec: krowa 200 kg, swinia 120 kg, owca 40 kg | ten sam XML [P] |
| Nasz mod: drewno = ladunek 100 kg, "iron" = lupa 15 kg (metal z ladunku 100 kg rudy) | log l.249 "HistoricalPrices: ruda i drewno w ladunkach - iron 10 -> 15 kg, hardwood 10 -> 100 kg"; `Bloomery.cs:22`, `VillageWoodlot.cs:45` [P] |
| Ceny sa historyczne w pensach za te 10 kg (zboze 3 d, welna 83 d, plotno 100 d, sol 2 d ...) | log l.250 [P] |
| 1 sztuka jedzenia karmi 20 ludzi przez dobe (0.5 kg na czlowieka); Rations -40% -> 33 | `DefaultMobilePartyFoodConsumptionModel.cs:16`; log l.477 "ludzi na 1 jedzenie: aktywny 20", l.120 [P] |
| Produkcja wsi = lista typu wsi x (poziom hearth + 1) x 0.5; poziomy przy 200 i 600 hearth; +5/10/15% z budynku miasta | `DefaultVillageProductionCalculatorModel.cs:31, 63-66`, `Village.cs:320-331`, `DefaultBuildingTypes.cs:241, 294` [P] |
| Lista bazowa: zboze 50 (kazdy inny typ wsi: zboze 3), ryby 28, len 18, sol 15, glina 10, ruda 10, drewno 18, winogrona 11, oliwki 12, daktyle 8, jedwab 8, srebro 3, futra 1.4 | `DefaultVillageTypes.cs:141-162, 259-270` [P] |
| Czynny model produkcji wsi: NavalDLC -> Default (gry). **Model BK, ktory liczy plon z rak roboczych x klasa ziemi, w tym ukladzie NIE biegnie** | log l.464; `BKVillageProductionModel.cs:39-68, 171-223` [P] |
| W dobie 2: 12 wsi ponizej 200 hearth, 520 w pasie 200-600, 37 powyzej 600 - czyli 91% wsi daje dokladnie baze | log, linia "Hearth (E4/0)" doby 108837 [P] |
| Nasze mnozniki: ruda x3 x1.5 i drewno drwali x3 (liczone w ladunkach po 100 kg, czyli sztuki gry / 10), las wsi 2.5 x 1.6 = 4 ladunki na wies na dobe x klasa klimatu (0.32-1.29), regulator wsi x0.38-x1.30 co 28 dob | `Settings.cs:504, 507, 1524, 1525, 1755`; `MaterialLaw.cs:183`; `VillageWoodlot.cs:54`; log l.243, l.272, l.305; `HarvestValve.cs:443` [P] |
| Chlopi nie jedza towaru: spichlerz okregu w racjach, plon = 1.2 x ludzie okregu x q | `Famine.cs:21, 446`; `Settings.cs:838`; log l.133 [P] |
| Rece rzemiosla zbrojnego juz ida za ludzmi: 0.10 roboczodnia na 1000 ludzi rynku x 1.3; rzemioslo tkackie i garbarskie = 2 x rece zbrojne | `WorkshopLaw.cs:792-804`, `Settings.cs:1520, 1863, 589`, `TownCrafts.cs:282`; log l.305 [P] |
| Odziez wojska liczona w prawdziwych kg na zolnierza na rok: w polu skora 2, sukno 3, plotno 2.5; w zalodze 1.2 / 2 / 1 | `Settings.cs:1194-1199`, `ArmyClothing.cs:106-114` [P] |

### 2.1 Trzy skale zmierzone w biegu (srednia roku)

| Kto je | Ilu ludzi | Zjada sztuk jedzenia na dobe | 1 sztuka = ile osobodni | Skala wobec reguly gry (20 osobodni na sztuke) |
|---|---|---|---|---|
| Druzyny (wojsko w polu) | 85 679 [P "Odziez wojska (150)"] | 2 540 [P ujscie "zywnosc partii"] | 33.7 | 1:1.7 (to jest Rations -40%) |
| Mieszczanie 97 miast | 10.41 mln [P] | 10 001 [P ujscie "mieszczanie"] | 1 041 | 1:52 |
| Zamki (130) | podzamcze 328 794 [P log l.732] | 2 038 [P ujscie "zamki"] | - | - |
| Chlopi (wsie) | 41.30 mln [P] | 0 sztuk; 41.30 mln racji papierowych z plonu 49.16 mln racji | - | poza targiem |
| Razem jedzacy z targu (miasta + podzamcze + wojsko i zalogi 163.8 tys.) | 10.91 mln | 14 580 | 748 | **1:37** |

- **Przyjeta skala symbolu N0 = 37:** jedna sztuka na targu stoi za ok. 37 prawdziwymi sztukami po 10 kg. Widelki: 28 (nadwyzka papierowa spichlerzy 7.99 mln racji = 399 tys. sztuk wobec 14 319 sztuk zywnosci pierwotnej na targu), 37 (jedzacy z targu przy racji gry 0.5 kg), ok. 73 (ci sami jedzacy przy 330 kg zywnosci na glowe rocznie z wzorca) [S z pomiarow].
- Na caly swiat: 0.28 sztuki jedzenia z targu na 1000 ludzi na dobe [P].
- Wobec CALEJ historycznej produkcji zboza (290 kg na glowe) gra daje 1:459 - ale to porownanie myli, bo wyzywienie chlopow gra liczy osobno, na papierze (zgodne z docs/EKONOMIA-FUNDAMENT-2026-10-05.md F2: 1:490-590 przed naszymi zmianami).

## 3. Lista towarow i liczby z biegu rocznego

Kolumna "zuzycie": **lud** = mieszczanie + zamki + zakupy wsi w miescie (E5/B1); **wojsko** = zywnosc druzyn + odziez wojska + zaopatrzenie BK; **warsztaty** = linie warsztatow + rzemieslnicy BK + rzemioslo miasta (148) + warsztaty zbrojne + strzelarze + naprawy + dymarki; **przepadlo** = glownie towar rozwiazanych druzyn. Wszystko [P] z ksiegi towarow; "kg na glowe rocznie" = produkcja roku x kg sztuki x 364 / 51.73 mln.

| towar (id) | wart. d | prod. start | prod. koniec | prod. rok | zuzycie rok: lud / wojsko / warsztaty / budowy / przepadlo | zapas doba 1 -> 364 | miast bez towaru start -> koniec (z 97) | prod. na 1000 ludzi swiata na dobe (rok) | kg na glowe rocznie |
|---|---|---|---|---|---|---|---|---|---|
| zboze (grain) | 3 | 9432 | 8761 | 8985 | 6465 / 956 / 1509 / 0 / 111 | 105762 -> 150637 | brak pomiaru | 0.1737 | 0.6323 |
| chleb (bread) | 6 | 1064 | 986 | 1024 | 753 / 186 / 0 / 0 / 57.0 | 1777 -> 5259 | brak pomiaru | 0.0198 | 0.0720 |
| pasztet/placek (pie) | 10 | 648 | 519 | 568 | 314 / 181 / 0 / 0 / 33.2 | 1607 -> 6610 | brak pomiaru | 0.0110 | 0.0400 |
| ryby (fish) | 10 | 3085 | 2559 | 2600 | 1946 / 615 / 0 / 0 / 35.4 | 46954 -> 103712 | brak pomiaru | 0.0503 | 0.1829 |
| mieso (meat) | 9 | 596 | 552 | 619 | 507 / 34.6 / 13.1 / 0 / 37.3 | 1421 -> 3025 | brak pomiaru | 0.0120 | 0.0435 |
| mieso wieloryba (WhaleMeat) | 7 | 500 | 475 | 517 | 408 / 24.6 / 12.4 / 0 / 50.2 | 1495 -> 1922 | brak pomiaru | 0.0100 | 0.0364 |
| ser (cheese) | 13 | 457 | 416 | 442 | 358 / 95.9 / 0 / 0 / 25.7 | 27908 -> 4567 | brak pomiaru | 0.0085 | 0.0311 |
| maslo (butter) | 24 | 343 | 312 | 328 | 239 / 99.6 / 15.7 / 0 / 14.2 | 30614 -> 5728 | brak pomiaru | 0.0063 | 0.0231 |
| jaja (Egg) | 8 | 121 | 83.2 | 110 | 61.6 / 16.3 / 0 / 0 / 43.7 | 12883 -> 1834 | brak pomiaru | 0.0021 | 0.0077 |
| piwo (beer) | 3 | 955 | 761 | 843 | 668 / 32.5 / 0 / 0 / 114 | 957 -> 3085 | brak pomiaru | 0.0163 | 0.0593 |
| miod pitny (mead) | 10 | 94.4 | 46.2 | 53.6 | 42.1 / 0.46 / 0 / 0 / 0.01 | 844 -> 2488 | brak pomiaru | 0.0010 | 0.0038 |
| wino (wine) | 14 | 96.6 | 58.6 | 58.8 | 57.5 / 0.01 / 0 / 0 / 0 | 144 -> 258 | brak pomiaru | 0.0011 | 0.0041 |
| winogrona (grape) | 3 | 458 | 348 | 416 | 132 / 134 / 37.5 / 0 / 113 | 20389 -> 11927 | brak pomiaru | 0.0080 | 0.0293 |
| oliwki (olives) | 5 | 387 | 306 | 354 | 130 / 128 / 21.0 / 0 / 68.6 | 18379 -> 11670 | brak pomiaru | 0.0068 | 0.0249 |
| oliwa (oil) | 40 | 50.5 | 33.2 | 34.9 | 34.3 / 0 / 0 / 0 / 0 | 99 -> 277 | brak pomiaru | 0.0007 | 0.0025 |
| daktyle (date_fruit) | 60 | 156 | 142 | 148 | 85.6 / 31.5 / 0 / 0 / 5.7 | 7511 -> 8640 | brak pomiaru | 0.0029 | 0.0104 |
| jablka (apple) | 2 | 245 | 110 | 204 | 0.48 / 2.4 / 0.01 / 0 / 172 | 123 -> 3560 | brak pomiaru | 0.0039 | 0.0144 |
| marchew (carrot) | 2 | 229 | 122 | 207 | 2.3 / 2.8 / 0.17 / 0 / 175 | 142 -> 3861 | brak pomiaru | 0.0040 | 0.0146 |
| pomarancze (orange) | 10 | 63.8 | 35.5 | 57.2 | 0.09 / 0.68 / 0 / 0 / 34.4 | 15 -> 780 | brak pomiaru | 0.0011 | 0.0040 |
| miod (honey) | 14 | 50.8 | 31.6 | 46.5 | 0.84 / 0.80 / 0 / 0 / 24.5 | 21 -> 562 | brak pomiaru | 0.0009 | 0.0033 |
| sol (salt) | 2 | 434 | 546 | 511 | 511 / 0 / 0 / 0 / 0 | 2784 -> 2859 | brak pomiaru | 0.0099 | 0.0360 |
| przyprawy (spice) | 270 | 8.1 | 7.0 | 5.2 | 4.9 / 0 / 0 / 0 / 0 | 102 -> 211 | brak pomiaru | 0.0001 | 0.0004 |
| krowy (cow) | 113 | 88.1 | 84.9 | 85.5 | 8.3 / 1.3 / 72.8 / 0 / 0 | 619 -> 1480 | brak pomiaru | 0.0017 | 0.1203 |
| owce (sheep) | 17 | 167 | 158 | 161 | 5.3 / 0.01 / 153 / 0 / 0 | 1113 -> 1481 | brak pomiaru | 0.0031 | 0.0452 |
| swinie (hog) | 30 | 207 | 194 | 198 | 14.3 / 0 / 180 / 0 / 0.01 | 1338 -> 1933 | brak pomiaru | 0.0038 | 0.1671 |
| len (wlokno) (flax) | 20 | 416 | 508 | 484 | 3.9 / 0 / 471 / 0 / 0 | 3722 -> 6272 | 58 -> 69 | 0.0094 | 0.0340 |
| plotno (linen) | 100 | 126 | 162 | 157 | 126 / 9.4 / 22.7 / 0 / 0 | 495 -> 34 | 82 -> 88 | 0.0030 | 0.0110 |
| welna (wool) | 83 | 308 | 375 | 317 | 5.0 / 0 / 289 / 0 / 0 | 2206 -> 9409 | 53 -> 4 | 0.0061 | 0.0223 |
| sukno (filc) (felt) | 200 | 148 | 208 | 190 | 98.0 / 83.4 / 0 / 0 / 0.02 | 197 -> 2730 | brak pomiaru | 0.0037 | 0.0134 |
| jedwab surowy (cotton) | 1000 | 126 | 59.9 | 68.9 | 52.6 / 0 / 4.9 / 0 / 0 | 1523 -> 5565 | brak pomiaru | 0.0013 | 0.0049 |
| aksamit (velvet) | 4000 | 6.1 | 9.0 | 9.8 | 9.6 / 0 / 0 / 0 / 0 | 19 -> 112 | brak pomiaru | 0.0002 | 0.0007 |
| skory surowe (hides) | 10 | 317 | 348 | 371 | 2.4 / 0 / 351 / 0 / 0 | 384 -> 3331 | 27 -> 12 | 0.0072 | 0.0261 |
| skora wyprawiona (leather) | 40 | 83.5 | 144 | 145 | 14.9 / 41.1 / 85.7 / 0 / 0 | 31 -> 263 | 37 -> 43 | 0.0028 | 0.0102 |
| futra (fur) | 200 | 39.2 | 14.1 | 18.3 | 15.3 / 0 / 0 / 0 / 0 | 295 -> 1397 | brak pomiaru | 0.0004 | 0.0013 |
| drewno (ladunek 100 kg) (hardwood) | 4 | 1683 | 2186 | 2050 | 92.3 / 0.88 / 810 / 738 / 0.02 | 10759 -> 152862 | 70 -> 7 | 0.0396 | 1.4428 |
| deski (planks) | 2 | 9.2 | 15.9 | 14.8 | 6.0 / 0 / 0 / 8.7 / 0 | 240 -> 60 | brak pomiaru | 0.0003 | 0.0010 |
| zelazo - lupa 15 kg (iron) | 38 | 200 | 273 | 260 | 0 / 0 / 233 / 0 / 0 | 917 -> 8857 | 62 -> 6 | 0.0050 | 0.0275 |
| narzedzia (tools) | 40 | 98.1 | 164 | 146 | 131 / 0.19 / 0 / 7.4 / 0.02 | 89 -> 2112 | brak pomiaru | 0.0028 | 0.0103 |
| glina (clay) | 1 | 395 | 344 | 368 | 87.3 / 0 / 56.7 / 181 / 0 | 1885 -> 15905 | brak pomiaru | 0.0071 | 0.0259 |
| ceramika (pottery) | 8 | 14.2 | 75.6 | 58.7 | 56.7 / 0 / 0 / 0 / 0 | 32 -> 157 | brak pomiaru | 0.0011 | 0.0041 |
| wapien (limestone) | 1 | 149 | 94.4 | 130 | 26.6 / 0 / 0 / 84.3 / 0 | 1287 -> 7154 | brak pomiaru | 0.0025 | 0.0091 |
| marmur (marble) | 2 | 36.1 | 42.6 | 37.9 | 12.6 / 0 / 0 / 19.2 / 0 | 216 -> 1423 | brak pomiaru | 0.0007 | 0.0027 |
| ruda srebra (silver) | 20 | 215 | 86.5 | 108 | 83.4 / 0 / 8.3 / 0 / 0.01 | 947 -> 5074 | brak pomiaru | 0.0021 | 0.0076 |
| ruda zlota (gold_ore) | 50 | 6.8 | 8.1 | 7.4 | 6.6 / 0 / 0 / 0 / 0 | 44 -> 222 | brak pomiaru | 0.0001 | 0.0005 |
| klejnoty (jewelry) | 2400 | 10.8 | 15.5 | 16.7 | 13.8 / 0 / 0 / 0 / 0 | 25 -> 1035 | brak pomiaru | 0.0003 | 0.0012 |
| papirus (Papyrus) | 100 | 13.7 | 6.4 | 7.6 | 6.9 / 0 / 0 / 0 / 0 | 98 -> 265 | brak pomiaru | 0.0001 | 0.0005 |
| kly morsa (walrus_tusk) | 300 | 1.6 | 0.82 | 0.93 | 0.90 / 0 / 0 / 0 / 0 | 26 -> 32 | brak pomiaru | 0.0000 | 0.0001 |
| tran (whale_oil) | 24 | 9.3 | 5.3 | 6.0 | 5.4 / 0 / 0 / 0 / 0 | 162 -> 205 | brak pomiaru | 0.0001 | 0.0004 |

Uwagi do tabeli [P]:
- **Plotno**: zapas swiata spadl z 495 do 34 sztuk; brak w 82 -> 88 miastach; regulator wsi trzyma len na suficie x1.30 od doby 85 (sol od doby 57, drewno od 113, welne od 309), a lnu dalej brakuje w 58 -> 69 miastach; plotna nie bylo w 88-91 miastach przy kazdej z 11 decyzji regulatora.
- **Skora**: zapas 263 sztuki = 1.8 doby zuzycia; brak w 37 -> 43 miastach.
- **Ruda, drewno, welna**: braki z poczatku roku znikly (ruda 62 -> 6 miast, drewno 70 -> 7, welna 53 -> 4), a zapasy rosna: drewno 10.8 tys. -> 152.9 tys. ladunkow (produkcja 2 050, zuzycie 1 652 na dobe), ruda 0.9 -> 8.9 tys., welna 2.2 -> 9.4 tys.
- **Ryby**: zapas 47 tys. -> 103.7 tys. (37 dob zuzycia) - rosnie.
- **Sol**: 95% soli kupuja wsie w miastach (486 z 511 na dobe); mieszczanie 23. Zapas swiata to 5-8 dob zuzycia przez caly rok, regulator na suficie x1.30 - soli tez brakuje, tylko log nie liczy miast bez soli.
- **Jablka, marchew, pomarancze, miod**: 84-85% produkcji przepada z rozwiazanymi druzynami ("inne partie"), mieszczanie prawie ich nie jedza.
- **Narzedzia**: 60% kupuja wsie (87 z 146), mieszczanie 44, budowy 7.

### 3.1 Skad sie bierze kazdy towar

| towar | zrodla |
|---|---|
| grain | wsie 8816 (wheat_farm 6562, reszta 985, zywnosc wsi 814, fisherman 218, lumberjack 141); inne ticki dobowe 138 (osady 118, miasta 20.5); rabunek osada po osadzie (E7/R1) 29.3 (zboze ze spichlerza okregu dla napastnikow 29.3); BK osady 2.4 (dokup z nadwyzki rak i zywnosci 2.4) |
| bread | rzemieslnicy BK 494 (grain>bread 494); warsztaty 448 (bakery 448); inne ticki dobowe 79.8 (osady 59.3, miasta 20.5); BK osady 1.2 (dokup z nadwyzki rak i zywnosci 1.2) |
| pie | warsztaty 512 (bakery 512); inne ticki dobowe 55.2 (osady 34.7, miasta 20.5); BK osady 0.80 (dokup z nadwyzki rak i zywnosci 0.80) |
| fish | wsie 2356 (fisherman 2079, BK poza typem wsi 244, whaler 27.2, walrus_hunter 5.7); inne ticki dobowe 243 (osady 212, miasta 30.7); BK osady 0.64 (dokup z nadwyzki rak i zywnosci 0.64) |
| meat | rzemieslnicy BK 474 (cow>6meat+2hides 145, hog>2meat 102, hog>2meat+hides 97.4, cow>4meat+2hides 42.5, sheep>meat+hides+wool 39.2); warsztaty 82.9 (butcher 82.9); inne ticki dobowe 59.4 (osady 38.9, miasta 20.5); uboj w partiach 1.2; BK osady 0.90 (dokup z nadwyzki rak i zywnosci 0.90) |
| WhaleMeat | rzemieslnicy BK 362 (cow>6meat+2hides 124, hog>2meat 66.2, hog>2meat+hides 61.0, sheep>meat+hides+wool 50.6, cow>4meat+2hides 34.4); warsztaty 81.4 (butcher 81.4); inne ticki dobowe 72.8 (osady 52.3, miasta 20.5); BK osady 1.1 (dokup z nadwyzki rak i zywnosci 1.1) |
| cheese | wsie 377 (zywnosc wsi 187, cattle_farm 115, sheep_farm 52.6, swine_farm 23.1); inne ticki dobowe 47.4 (osady 26.9, miasta 20.5); BK partie 16.2; BK osady 0.60 (dokup z nadwyzki rak i zywnosci 0.60) |
| butter | wsie 292 (cattle_farm 115, zywnosc wsi 101, sheep_farm 52.6, swine_farm 23.1); inne ticki dobowe 35.4 (miasta 20.5, osady 14.9); BK osady 0.39 (dokup z nadwyzki rak i zywnosci 0.39) |
| Egg | inne ticki dobowe 66.1 (osady 45.6, miasta 20.5); wsie 43.2 (cattle_farm 43.2); BK osady 0.87 (dokup z nadwyzki rak i zywnosci 0.87) |
| beer | rzemieslnicy BK 651 (grain>beer 651); inne ticki dobowe 140 (osady 119, miasta 20.5); warsztaty 49.6 (brewery 49.6); BK osady 2.5 (dokup z nadwyzki rak i zywnosci 2.5) |
| mead | wsie 53.6 (lumberjack 53.6) |
| wine | warsztaty 42.6 (wine_press 42.6); rzemieslnicy BK 16.2 (grape>wine 16.2) |
| grape | wsie 274 (vineyard 274); inne ticki dobowe 139 (osady 119, miasta 20.5); BK osady 2.4 (dokup z nadwyzki rak i zywnosci 2.4) |
| olives | wsie 260 (olive_trees 260); inne ticki dobowe 92.2 (osady 71.7, miasta 20.5); BK osady 1.5 (dokup z nadwyzki rak i zywnosci 1.5) |
| oil | warsztaty 27.9 (olive_press 27.9); rzemieslnicy BK 7.0 (olives>oil 7.0) |
| date_fruit | wsie 121 (date_farm 121); inne ticki dobowe 26.5 (miasta 20.5, osady 6.0) |
| apple | inne ticki dobowe 200 (osady 180, miasta 20.5); BK osady 3.6 (dokup z nadwyzki rak i zywnosci 3.6) |
| carrot | inne ticki dobowe 204 (osady 183, miasta 20.5); BK osady 3.4 (dokup z nadwyzki rak i zywnosci 3.4) |
| orange | inne ticki dobowe 56.5 (osady 35.9, miasta 20.5); BK osady 0.77 (dokup z nadwyzki rak i zywnosci 0.77) |
| honey | inne ticki dobowe 46.0 (osady 25.4, miasta 20.5); BK osady 0.50 (dokup z nadwyzki rak i zywnosci 0.50) |
| salt | wsie 511 (salt_mine 511) |
| spice | wsie 5.2 (date_farm 5.2) |
| cow | wsie 85.2 (cattle_farm 58.3, wheat_farm 26.9); inne ticki dobowe 0.32 (partie 0.32) |
| sheep | wsie 161 (sheep_farm 107, wheat_farm 53.7) |
| hog | wsie 198 (wheat_farm 106, swine_farm 92.4) |
| flax | wsie 484 (flax_plant 484) |
| linen | rzemioslo miasta (148) 106; warsztaty 50.8 (linen_weavery 50.8) |
| wool | wsie 275 (sheep_farm 275); rzemieslnicy BK 41.5 (sheep>meat+hides+wool 41.5) |
| felt | rzemioslo miasta (148) 110; warsztaty 80.0 (wool_weavery 80.0) |
| cotton | wsie 68.9 (silk_plant 68.9) |
| velvet | warsztaty 9.8 (velvet_weavery 9.8) |
| hides | rzemieslnicy BK 305 (sheep>meat+hides+wool 93.8, cow>6meat+2hides 89.3, hog>2meat+hides 78.2, cow>4meat+2hides 44.0); warsztaty 65.4 (butcher 65.4) |
| leather | rzemioslo miasta (148) 128; warsztaty 17.7 (tannery 17.7) |
| fur | wsie 18.3 (trapper 18.3) |
| hardwood | las wsi (126) 1748; wsie 302 (lumberjack 302); dymarki przy kopalniach (214) 0.47 |
| planks | warsztaty 14.8 (wood_WorkshopType 14.8) |
| iron | wsie 250 (iron_mine 250); zlom z nadmiaru (174) 10.5 |
| tools | warsztaty 128 (smithy 128); rzemieslnicy BK 18.6 (iron>2tools 18.6) |
| clay | wsie 368 (clay_mine 368) |
| pottery | rzemieslnicy BK 54.7 (clay>pottery 54.7); warsztaty 4.0 (pottery_shop 4.0) |
| limestone | wsie 130 (clay_mine 130) |
| marble | wsie 37.9 (silver_mine 37.9) |
| silver | wsie 108 (silver_mine 108) |
| gold_ore | wsie 7.4 (silver_mine 7.4) |
| jewelry | warsztaty 16.7 (silversmithy 16.7) |
| Papyrus | wsie 7.6 (wheat_farm 7.6) |
| walrus_tusk | wsie 0.93 (walrus_hunter 0.93) |
| whale_oil | wsie 6.0 (whaler 6.0) |

### 3.2 Uzbrojenie i konie (poza ksiega towarow; srednie z linii dziennych) [P]

| Co | Start | Koniec | Rok | Na 1000 ludzi swiata |
|---|---|---|---|---|
| Bron i zbroje z warsztatow zbrojnych, sztuk na dobe | 1 237 | 1 187 | 1 341 | 0.026 na dobe |
| - wedlug typu (rok): helmy 231, bron jednoreczna 204, luki 173, tarcze 138, drzewcowa 136, rekawice 115, korpusy 79, miotana 78, nogawice 46, plaszcze 43, uprzaz 41, kusze 37, dwureczna 19 | | | | |
| Snopy strzal / beltow (strzelarze 172), na dobe | 109 / 108 | 133 / 93 | 179 / 98 | 0.005 |
| Rece zbrojne, roboczodni na dobe | 6 601 | 8 201 | 7 435 | 0.144 |
| Cykle odpuszczone z braku surowca (ruda / drewno / skora / len-welna) | 662 / 559 / 80 / 99 | 40 / 37 / 101 / 4 | 227 / 164 / 95 / 20 | - |
| Ujscia uzbrojenia (zuzycie, zlom, straty - linia 174), sztuk na dobe | - | - | 633 | 0.012 |
| Uzbrojenie na polkach miast i zamkow, sztuk | 48 283 | 304 248 | - | - |
| Uzbrojenie w zbrojowniach wojska (DTE), sztuk | 538 300 | 1 034 019 | - | - |
| Konie z hodowli: zwykle / bojowe / szlachetne / juczne, na dobe | 174 / 61 / 6 / 137 | 164 / 74 / 6 / 130 | 166 / 68 / 5 / 131 (razem 370) | 0.0072 na dobe = 2.6 na 1000 ludzi rocznie |
| Stan koni swiata (zwykle / bojowe / szlachetne / juczne) | 10 920 / 3 874 / 1 662 / 18 062 | 19 513 / 9 156 / 2 082 / 34 711 | - | - |
| Konie kupione przez mieszczan (ujscie), na dobe | 160 | 307 | - | - |
| Wojsko na zoldzie (druzyny + zalogi) | 100 801 | 162 737 | 163 829 | 3.17 (0.32% ludzi) |

- Warsztaty towarowe (liczba, koniec roku) [P linia "Warsztaty towarowe"]: piekarnie 72, tartaki 30, tkalnie welny 27, kuznie narzedzi 23, rzeznie 17, tkalnie lnu 15, tlocznie wina 14, zlotnicy 13, browary 12, garbarnie 11, tlocznie oliwy 11, kopalnie BK 10, miecznicy 9, tkalnie aksamitu 8, strzelarze 7, platnerze 5, rymarze konscy 5, garncarnie 2. **W ktorych krolestwach stoja - nie wiem** (log nie podaje warsztatow wedlug miast; gra losuje je na starcie kampanii).
- Uzbrojenia przybywa szybciej, niz ubywa (1 341 + 277 wobec 633 na dobe): polki miast urosly 6x, zbrojownie wojska 2x. Koni tez (stan x1.9 w rok). To osobna sprawa od tego rozpoznania - zapisuje, bo produkcja "od ludzi" jeszcze ja powiekszy w ludnych krainach.

## 4. Wzorzec: spoleczenstwo rolnicze ok. 1300 r. (Europa Zachodnia)

To jest **wiedza ogolna, nie pomiar** [S]. Rzad wielkosci jest pewny, druga cyfra nie. Tam, gdzie projekt mial juz liczbe ze zrodlem (docs/EKONOMIA-FUNDAMENT-2026-10-05.md rozdz. 3.2 i F2, docs/BILANS-ZUZYCIA.md rozdz. 2.2: Broadberry i in., Campbell, Galloway-Keene-Murphy), biore ja, zeby dokumenty sie zgadzaly.

| Towar | Na glowe rocznie | Skad liczba i co pominieto |
|---|---|---|
| Zboze | 290 kg (200-320) | 1.65 kwartera na glowe netto, z ziarnem na piwo; Anglia ok. 1300 (EKONOMIA-FUNDAMENT F2; weryfikator: 300-316) |
| Mieso | 13 kg (8-25) | Broadberry: 137 mln funtow na 4.72 mln ludzi; po Czarnej Smierci 2x wiecej |
| Ryby | 5 kg (2-10) | sledz i sztokfisz na dni postne; wies w glebi ladu prawie nic - slaba liczba |
| Ser i maslo | 10 kg (5-20) | slaba liczba |
| Piwo | 150 l (80-300) | ok. 0.4 l dziennie srednio z dziecmi; ziarno juz policzone w zbozu |
| Wino | 40 l (15-100) | srednia dla swiata, w ktorym tylko czesc krain ma winnice; w krainach wina 100-200 l |
| Oliwa | 3 l (1-8) | tylko poludnie; slaba liczba |
| Sol | 6 kg (4-8) | z soleniem miesa i ryb; EKONOMIA-FUNDAMENT: 7 kg "bez zrodla" |
| Welna surowa | 1.0 kg (0.7-2.0) | Anglia produkowala 2 kg, polowe wywozila; swiat zamkniety ok. 0.9-1 kg |
| Sukno | 0.6 kg (0.4-1.2) | ok. 1.2 jarda kw. na glowe (Broadberry) |
| Len - wlokno | 1.0 kg (0.5-1.5) | szacunek, twardych danych brak (BILANS-ZUZYCIA 2.2) |
| Plotno | 0.5 kg (0.3-1.0) | koszule, posciel, worki, zagle; szacunek |
| Skory surowe | 0.6 kg (0.4-1.0) | Broadberry: 6.3 mln funtow na 4.72 mln |
| Skora wyprawiona | 0.5 kg (0.3-1.0) | buty 1-2 pary rocznie, pasy, uprzaz |
| Zelazo | 0.35 kg (0.25-1.5) | Anglia 1300: ok. 1 000 t + przywoz; 1.5 kg dopiero ok. 1500 |
| Wyroby zelazne (narzedzia, gwozdzie, podkowy) | 0.25 kg (0.15-1.0) | wiekszosc zelaza poza bronia |
| Drewno opalowe | 1 500 kg (1 000-2 500) | Londyn 1300: 1 760 kg na glowe (Galloway, Keene, Murphy) |
| Drewno uzytkowe + wegiel hut | 90 kg (50-150) | budulec 50-66 kg (EKONOMIA-FUNDAMENT) + ok. 25-35 kg drewna na wegiel do 0.35 kg zelaza |
| Ceramika | 2 kg (1-5) | kilka naczyn na dom rocznie - slaba liczba |
| Glina (garnki, cegla, dachowka) | 4 kg (2-20) | zalezy od budownictwa z cegly - slaba liczba |
| Konie | 0.10-0.15 konia na glowe, 8-15 nowych na 1000 ludzi rocznie | konie robocze i wierzchowe razem, wymiana co 10-12 lat - slaba liczba |
| Rzemieslnicy zbrojni | 0.3-0.6 na 1000 ludzi | EKONOMIA-FUNDAMENT 3.2 |
| Wojsko | pokoj 0.03-0.1%, rok wojny 0.15-0.35% ludzi | EKONOMIA-FUNDAMENT 3.2 |

**Bez wzorca (nie wiem):** srebro i zloto (w grze to "ladunki rudy", nie metal), klejnoty, aksamit i jedwab (zbytek - promile), futra, wapien, marmur, papirus, kly morsa, tran, przyprawy, owoce, miod, jaja.

### 4.1 Przeliczenie na jednostki gry

- **Swiat 1:1**: sztuk na 1000 ludzi na dobe = kg na glowe x 1000 / 364 / kg sztuki. Zboze: 290 x 1000 / 364 / 10 = 79.7 sztuki na 1000 ludzi na dobe (4.12 mln sztuk na swiat) [S].
- **W skali gry**: to samo x udzial kupujacych z targu / N0 (37). Udzial kupujacych: zywnosc i napoje 0.211 (miasta + podzamcze + wojsko; chlopi jedza z papierowego spichlerza i warza u siebie), opal 0.201 (tylko miasta - chlop zbiera sam), reszta 1.0 (welne, len, skory, zelazo, sol, garnki gra sprzedaje i przerabia tylko przez targ; wsie kupuja w miescie sol, narzedzia, plotno, garnki i piwo - `Settings.cs:1652`, log l.733) [S].

## 5. Porownanie: dzis a wzorzec

Ocena: "ZA MALO" gdy dzis jest ponizej 1/3 wzorca w skali gry, "ZA DUZO" gdy powyzej 3x. Przy niepewnosci wzorca (2-3x w kazda strone) tylko takie odchylenia cos znacza. Kolumna "gra wobec calej historii" = ile razy mniej niz caly swiat 1:1.

| towar | wzorzec kg na glowe rocznie [S] (widelki) | kg na sztuke gry | wzorzec 1:1: sztuk na 1000 ludzi na dobe | kto kupuje z targu | wzorzec w skali gry (1:37): sztuk na 1000 ludzi swiata na dobe / sztuk swiata na dobe | DZIS: sztuk na 1000 ludzi na dobe / swiat na dobe (rok; koniec roku) | dzis : wzorzec w skali gry (widelki) | gra wobec calej historii 1:N | ocena |
|---|---|---|---|---|---|---|---|---|---|
| zboze (chleb, kasza, slod) | 290 (200-320) | 10 | 79.670 | zywnosc | 0.4491 / 23232 | 0.1737 / 8985 (8761) | 0.39 (0.35-0.56) | 1:459 | w skali |
| mieso | 13 (8-25) | 10 | 3.571 | zywnosc | 0.0201 / 1041 | 0.0220 / 1136 (1028) | 1.09 (0.57-1.77) | 1:163 | w skali |
| ryby | 5 (2-10) | 10 | 1.374 | zywnosc | 0.0077 / 401 | 0.0503 / 2600 (2559) | 6.49 (3.25-16.23) | 1:27 | ZA DUZO |
| nabial (ser, maslo) | 10 (5-20) | 10 | 2.747 | zywnosc | 0.0155 / 801 | 0.0149 / 770 (728) | 0.96 (0.48-1.92) | 1:185 | w skali |
| piwo (litry = kg) | 150 (80-300) | 10 | 41.209 | zywnosc | 0.2323 / 12016 | 0.0173 / 896 (807) | 0.07 (0.04-0.14) | 1:2379 | ZA MALO |
| wino | 40 (15-100) | 10 | 10.989 | zywnosc | 0.0619 / 3204 | 0.0011 / 59 (59) | 0.02 (0.01-0.05) | 1:9666 | ZA MALO |
| oliwa | 3 (1-8) | 10 | 0.824 | zywnosc | 0.0046 / 240 | 0.0007 / 35 (33) | 0.15 (0.05-0.44) | 1:1221 | ZA MALO |
| sol | 6 (4-8) | 10 | 1.648 | wszyscy | 0.0441 / 2280 | 0.0099 / 511 (546) | 0.22 (0.17-0.34) | 1:167 | ZA MALO |
| welna surowa | 1 (0.7-2) | 10 | 0.275 | wszyscy | 0.0073 / 380 | 0.0061 / 317 (375) | 0.83 (0.42-1.19) | 1:45 | w skali |
| sukno (w grze felt) | 0.6 (0.4-1.2) | 10 | 0.165 | wszyscy | 0.0044 / 228 | 0.0037 / 190 (208) | 0.84 (0.42-1.25) | 1:45 | w skali |
| len - wlokno | 1 (0.5-1.5) | 10 | 0.275 | wszyscy | 0.0073 / 380 | 0.0094 / 484 (508) | 1.27 (0.85-2.55) | 1:29 | w skali |
| plotno (linen) | 0.5 (0.3-1) | 10 | 0.137 | wszyscy | 0.0037 / 190 | 0.0030 / 157 (162) | 0.83 (0.41-1.38) | 1:45 | w skali |
| skory surowe | 0.6 (0.4-1) | 10 | 0.165 | wszyscy | 0.0044 / 228 | 0.0072 / 371 (348) | 1.63 (0.98-2.44) | 1:23 | w skali |
| skora wyprawiona | 0.5 (0.3-1) | 10 | 0.137 | wszyscy | 0.0037 / 190 | 0.0028 / 145 (144) | 0.77 (0.38-1.28) | 1:49 | w skali |
| zelazo (lupa 15 kg) | 0.35 (0.25-1.5) | 15 | 0.064 | wszyscy | 0.0017 / 89 | 0.0050 / 260 (273) | 2.93 (0.68-4.11) | 1:13 | w skali |
| narzedzia i wyroby zelazne | 0.25 (0.15-1) | 10 | 0.069 | wszyscy | 0.0018 / 95 | 0.0028 / 146 (164) | 1.54 (0.39-2.57) | 1:24 | w skali |
| drewno opalowe | 1500 (1000-2500) | 100 | 41.209 | miasta | 0.2218 / 11474 | 0.0018 / 92 (145) | 0.01 (0.00-0.01) | 1:23100 | ZA MALO |
| drewno uzytkowe i wegiel hut | 90 (50-150) | 100 | 2.473 | wszyscy | 0.0661 / 3420 | 0.0379 / 1958 (2041) | 0.57 (0.34-1.03) | 1:65 | w skali |
| ceramika | 2 (1-5) | 10 | 0.549 | wszyscy | 0.0147 / 760 | 0.0011 / 59 (76) | 0.08 (0.03-0.15) | 1:484 | ZA MALO |
| glina (garnki, cegla) | 4 (2-20) | 10 | 1.099 | wszyscy | 0.0294 / 1520 | 0.0071 / 368 (344) | 0.24 (0.05-0.48) | 1:154 | ZA MALO |

Jak to czytac:
- **Cala zywnosc razem jest z definicji 1.0** (z niej wyznaczona skala). Samo zboze wychodzi 0.39, bo w grze 1/3 jedzenia to ryby, mieso i nabial, a w historii ok. 10%. Zboza jest wiec o polowe za malo wobec reszty jedzenia, a **ryb 6.5 razy za duzo** (i ich zapas rosnie).
- **Piwo i wino**: produkcja 896 i 59 sztuk na dobe wobec ok. 12 tys. i 3.2 tys. w skali gry. Zastrzezenie: w grze sztuka piwa to tez "jedzenie" (20 osobodni), wiec przeliczenie litr = kg jest umowne; rzad wielkosci (10-50x za malo jako napoj) zostaje.
- **Sol**: 511 wobec ok. 2 280 (4.5x za malo, widelki 3-6x); cala sol idzie do wsi, mieszczanie prawie jej nie kupuja.
- **Opal**: mieszczanie i zamki pala 92 ladunki na dobe wobec ok. 11 500 w skali gry. To nie brak drewna (zapas 153 tys. ladunkow lezy), tylko **brak ujscia**: lud nie kupuje opalu.
- **Ceramika 13x, glina 4x za malo** - wzorzec slaby, ale kierunek pewny (2 garncarnie na 97 miast).
- **Zelazo 3x powyzej skali** - spojne z tym, ze warsztaty zbrojne pracuja w skali 1:2-1:4 (rece 0.144 na 1000 ludzi wobec wzorca 0.3-0.6), a nie 1:37.
- **Konie**: 2.6 nowego konia na 1000 ludzi rocznie wobec wzorca 8-15, czyli 1:3-1:6; wojsko 0.32% ludzi = gorna granica roku wojny, skala 1:1 [P + S].

### 5.1 Dlaczego brakuje towarow, ktore sa "w skali"

Produkcja plotna, skory i sukna jest symbolem (1:45), ale trzech odbiorcow bierze w prawdziwych kilogramach [P, srednia roku, sztuk na dobe]:

| Wyrob | Produkcja | Potrzeba wojska narasta | Wojsko kupilo | Wojsko czeka (ostatnia doba) | Warsztaty zbrojne | Mieszczanie | Wsie |
|---|---|---|---|---|---|---|---|
| Skora | 145.5 | 72.9 (50% produkcji) | 41.1 | 2 852 sztuk = 20 dob produkcji | 77.2 | 14.9 | 0 |
| Sukno | 190.4 | 113.7 (60%) | 83.4 | 2 133 = 11 dob | 0 | 97.1 | 0 |
| Plotno | 156.9 | 80.4 (51%) | 9.4 | 5 927 = 38 dob | 22.4 | 108.0 | 18.0 |

164 tys. zolnierzy (0.32% ludzi) potrzebuje polowy swiatowej produkcji tkanin i skory. Historycznie 0.32% ludzi zuzywaloby ok. 1-2% tkanin. To jest ta sama niezgodnosc skal, co przy jedzeniu (wiersz "druzyny" w 2.1).

### 5.2 Czy produkcja proporcjonalna do ludzi sama usunie braki?

**Nie - braki sa swiatowe** [P + S]:
- Len: swiat wytwarza 484 sztuki na dobe i zuzywa 477; plotno 157 i 158; zapas plotna 34 sztuki. Nie ma regionu z nadwyzka, ktora nie dojezdza - w 74 z 97 miast rzemioslo konczy przerob "z braku surowca" (linia "Rzemioslo miasta (148)", koniec roku).
- Wsie lniane leza w przecietnie ludnych okolicach (72.7 tys. ludzi na wies wobec sredniej swiata 72.3 tys.). Gdyby dzisiejsza wies o sredniej ludnosci dawala tyle co dzis, a reszta szla za ludzmi, suma lnu swiata zmienilaby sie x1.01. Dla innych: welna x1.23, ruda x1.08, glina x1.01, skory x0.98, zboze x0.97, ryby x0.87, drewno drwali x0.84, **sol x0.77**, futra x0.67; jedwab x1.76, oliwki x1.47 [S z pomiaru].
- Zeby braki zniknely, trzeba albo wiecej wsi lnianych / solnych / hodowlanych, albo wyzszej stawki na czlowieka dla tych towarow, albo zmniejszenia odbiorcow 1:1 (5.1). Sama regula "od ludzi" przesuwa towar miedzy krainami.

## 6. Geografia: kto co wytwarza i co da zasada "wies to symbol"

Ludzie i liczba wsi wedlug krolestw (doba 364) [P]: Reach 8 345 tys. / 49 wsi (153.6 tys. na wies); Volantis 4 993 / 18 (208.1); King's Landing 4 781 / 49 (81.5); Qarth 3 983 / 11 (228.1); Polnoc 3 227 / 60 (49.3); Dolina 3 222 / 34 (88.7); Stormlands 2 757 / 39 (67.6); Dorzecze 2 741 / 32 (79.9); Braavos 2 593 / 15 (119.8); Meereen (Targaryen) 1 972 / 17 (68.2); Dorne 1 116 / 29 (35.3); Zelazne Wyspy 469 / 28 (16.0); swiat 51 471 tys. / 569 wsi (72.3 tys. na wies).

| typ wsi | wsi | ludzie tys. | srednio na wies tys. | krolestwa: liczba wsi (ludzie tys.) |
|---|---|---|---|---|
| wheat_farm | 123 | 8579 | 69.8 | Reach 14 (2336), Polnoc 16 (825), KL 11 (703), Dolina 9 (678), Volantis 3 (581), Dorzecze 9 (566), Braavos 3 (406), Pentos 5 (393), Meereen 5 (340), Stormlands 4 (337), Norvos 3 (206), Dorne 6 (183), Lorath 4 (177), Myr 3 (143), Qohor 1 (113), Dragonstone 4 (112), Tyrosh 2 (109), Wyspy 6 (96), Lys 2 (88), Dothrakowie 2 (80), Ibben 2 (35), Yi Ti 1 (27), Aegon 1 (24), Sarnor 3 (16), Straz 4 (6) |
| fisherman | 66 | 4152 | 62.9 | Reach 5 (673), Volantis 2 (489), Dolina 6 (487), KL 4 (368), Stormlands 5 (320), Braavos 2 (242), Dorne 6 (237), Dragonstone 5 (166), Polnoc 3 (156), Pentos 1 (129), Dorzecze 1 (113), Wyspy 6 (98), Myr 2 (93), Lorath 2 (85), Qohor 1 (68), Aegon 1 (67), Meereen 1 (67), Wolni 4 (64), Lys 1 (54), Tyrosh 1 (41), Dothrakowie 1 (41), Ibben 2 (38), Wyspy Letnie 1 (32), Skagos 1 (13), Sarnor 2 (12) |
| lumberjack | 43 | 2625 | 61.1 | Reach 3 (459), Polnoc 7 (346), Qohor 4 (271), Stormlands 4 (219), Qarth 1 (217), KL 2 (188), Dorzecze 2 (151), Pentos 2 (140), Braavos 1 (129), Dothrakowie 2 (94), Aegon 1 (68), Myr 1 (62), Lorath 1 (42), Wolni 3 (41), Ibben 2 (40), Dragonstone 1 (35), Wyspy Letnie 1 (32), Wyspy 2 (31), Dorne 1 (30), Yi Ti 1 (27), Straz 1 (3) |
| silver_mine | 30 | 2460 | 82.0 | Qarth 3 (651), KL 7 (651), Braavos 2 (257), Dolina 1 (157), Polnoc 3 (156), Dorzecze 1 (134), Aegon 1 (68), Qohor 1 (68), Reach 2 (62), Lys 1 (54), Dothrakowie 1 (41), Wyspy 2 (33), Wyspy Letnie 1 (32), Dorne 1 (32), Stormlands 1 (25), Dragonstone 1 (21), Wolni 1 (18) |
| vineyard | 27 | 1977 | 73.2 | Reach 2 (458), Stormlands 6 (437), Dorzecze 4 (289), Qohor 2 (151), Pentos 2 (128), Polnoc 2 (99), KL 2 (86), Norvos 1 (79), Dolina 1 (78), Tyrosh 1 (70), Aegon 1 (68), Dorne 1 (30), Straz 2 (3) |
| iron_mine | 26 | 2022 | 77.8 | Reach 3 (466), KL 2 (228), Stormlands 3 (195), Volantis 1 (192), Dorzecze 2 (176), Braavos 1 (140), Meereen 2 (131), Qohor 1 (87), Polnoc 1 (80), Dolina 1 (72), Norvos 1 (69), Aegon 1 (68), Wyspy 4 (63), Dothrakowie 1 (39), Wolni 1 (13), Straz 1 (3) |
| trapper | 25 | 1207 | 48.3 | Polnoc 7 (319), KL 3 (299), Reach 1 (156), Lorath 2 (84), Wolni 5 (81), Dolina 1 (69), Qohor 1 (68), Norvos 1 (64), Wyspy 2 (33), Yi Ti 1 (27), Sarnor 1 (8) |
| sheep_farm | 24 | 2133 | 88.9 | Dolina 3 (423), Reach 2 (395), Qarth 1 (277), Volantis 1 (247), Meereen 3 (211), Braavos 1 (128), Stormlands 2 (109), Polnoc 2 (93), Aegon 1 (67), Dothrakowie 1 (52), Tyrosh 1 (47), Dragonstone 1 (47), Wyspy 1 (15), Skagos 1 (13), Sarnor 1 (6), Straz 2 (4) |
| cattle_farm | 24 | 1625 | 67.7 | Dorzecze 5 (508), Dolina 3 (296), Reach 1 (161), Polnoc 3 (136), KL 1 (135), Dothrakowie 2 (79), Norvos 1 (77), Pentos 1 (69), Aegon 1 (68), Dragonstone 1 (31), Stormlands 1 (22), Wolni 1 (20), Wyspy 1 (15), Sarnor 2 (10) |
| flax_plant | 20 | 1455 | 72.7 | KL 3 (295), Stormlands 2 (196), Braavos 2 (196), Norvos 2 (184), Reach 1 (151), Polnoc 3 (139), Dolina 1 (78), Pentos 1 (69), Dorzecze 1 (68), Lorath 1 (42), Wolni 1 (17), Wyspy 1 (15), Sarnor 1 (6) |
| europe_horse_ranch | 20 | 1415 | 70.7 | Dolina 5 (445), Stormlands 6 (368), Dorzecze 3 (241), Reach 1 (133), Braavos 1 (129), Norvos 1 (69), KL 3 (28) |
| olive_trees | 19 | 2016 | 106.1 | Reach 6 (1037), Volantis 2 (388), Dorne 5 (208), Stormlands 2 (172), Tyrosh 1 (56), Lys 1 (54), Dothrakowie 1 (52), Myr 1 (49) |
| clay_mine | 16 | 1164 | 72.8 | Dorzecze 4 (311), Volantis 1 (193), Braavos 2 (170), Norvos 2 (138), KL 1 (136), Meereen 1 (65), Dothrakowie 1 (54), Polnoc 1 (46), Tyrosh 1 (44), Sarnor 1 (6), Straz 1 (2) |
| desert_horse_ranch | 15 | 1096 | 73.1 | Volantis 3 (579), Meereen 2 (129), Dorne 4 (129), Tyrosh 3 (127), Myr 2 (91), Lys 1 (42) |
| date_farm | 14 | 1028 | 73.4 | Qarth 1 (277), Volantis 1 (192), Lys 3 (142), Tyrosh 3 (138), Dorne 3 (104), Meereen 1 (67), Stormlands 1 (63), Reach 1 (45) |
| silk_plant | 14 | 1779 | 127.1 | Volantis 3 (689), Qarth 2 (434), Reach 3 (343), Myr 3 (144), Aegon 2 (92), Meereen 1 (77) |
| steppe_horse_ranch | 13 | 1136 | 87.4 | Qarth 3 (651), Qohor 2 (170), Dothrakowie 4 (161), Meereen 1 (73), Aegon 1 (68), Sarnor 2 (12) |
| salt_mine | 12 | 664 | 55.3 | Volantis 1 (196), Stormlands 2 (172), Dorne 2 (70), Lys 1 (60), Reach 1 (45), Tyrosh 1 (41), Dothrakowie 1 (39), KL 2 (27), Wyspy 1 (15) |
| swine_farm | 10 | 523 | 52.3 | Reach 1 (156), Polnoc 3 (144), Pentos 1 (69), KL 1 (67), Ibben 2 (43), Dragonstone 1 (29), Wolni 1 (14) |
| vlandian_horse_ranch | 10 | 1312 | 131.2 | KL 7 (782), Reach 2 (452), Dolina 1 (78) |
| sturgian_horse_ranch | 7 | 284 | 40.6 | Dolina 2 (155), Polnoc 1 (46), Wyspy 2 (33), Dragonstone 1 (30), Wolni 1 (19) |
| battanian_horse_ranch | 6 | 285 | 47.5 | Polnoc 6 (285) |
| whaler | 6 | 165 | 27.5 | Polnoc 2 (87), Ibben 3 (65), Skagos 1 (13) |
| walrus_hunter | 1 | 13 | 12.6 | Skagos 1 (13) |

### 6.1 Udzial krainy w ludziach swiata a jej udzial w surowcu

Pierwsza liczba = dzis (kazda wies typu daje podobnie; waga = poziom hearth z XML), druga = zasada "od ludzi" przy tej samej sumie swiata. Wszystko w % swiata [S z pomiaru].

| Krolestwo | ludzie swiata | len | welna | sol | ruda | drwale | zywiec i skory | zboze | ryby |
|---|---|---|---|---|---|---|---|---|---|
| Reach | 16.2% | 4.9 / 10.4 | 7.8 / 18.5 | 7.4 / 6.7 | 11.1 / 23.1 | 7.1 / 17.5 | 6.5 / 16.6 | 11.8 / 27.2 | 8.1 / 16.2 |
| Volantis | 9.7% | 0 / 0 | 3.9 / 11.6 | 7.4 / 29.5 | 3.7 / 9.5 | 0 / 0 | 1.6 / 5.8 | 2.4 / 6.8 | 2.9 / 11.8 |
| King's Landing | 9.3% | 17.1 / 20.3 | 0 / 0 | 18.5 / 4.1 | 7.4 / 11.3 | 4.8 / 7.1 | 3.3 / 4.7 | 9.4 / 8.2 | 6.6 / 8.9 |
| Qarth | 7.7% | 0 / 0 | 3.9 / 13.0 | 0 / 0 | 0 / 0 | 2.4 / 8.3 | 1.6 / 6.5 | 0 / 0 | 0 / 0 |
| Polnoc | 6.3% | 14.6 / 9.5 | 7.8 / 4.4 | 0 / 0 | 5.6 / 3.9 | 16.7 / 13.2 | 13.0 / 8.7 | 13.1 / 9.6 | 4.4 / 3.7 |
| Dolina | 6.3% | 4.9 / 5.3 | 15.7 / 19.8 | 0 / 0 | 3.7 / 3.5 | 0 / 0 | 12.2 / 16.8 | 7.3 / 7.9 | 8.8 / 11.7 |
| Stormlands | 5.4% | 9.8 / 13.5 | 7.8 / 5.1 | 22.2 / 25.9 | 11.1 / 9.6 | 9.5 / 8.4 | 4.9 / 3.0 | 3.3 / 3.9 | 7.4 / 7.7 |
| Dorzecze | 5.3% | 4.9 / 4.7 | 0 / 0 | 0 / 0 | 7.4 / 8.7 | 4.8 / 5.7 | 10.6 / 11.9 | 6.9 / 6.6 | 1.5 / 2.7 |
| Braavos | 5.0% | 9.8 / 13.4 | 3.9 / 6.0 | 0 / 0 | 3.7 / 6.9 | 2.4 / 4.9 | 1.6 / 3.0 | 2.4 / 4.7 | 2.9 / 5.8 |
| Dorne | 2.2% | 0 / 0 | 0 / 0 | 14.8 / 10.6 | 0 / 0 | 2.4 / 1.2 | 0 / 0 | 4.9 / 2.1 | 10.3 / 5.7 |

Wniosek: zasada "od ludzi" daje ludnym krainom wiecej tylko tych towarow, ktorych symbol maja. Reach dostaje 27% zboza swiata (dzis 12%), ale lnu 10% przy 16% ludzi; Volantis i Qarth zostaja bez lnu, Qarth bez zboza, ryb, soli i rudy; King's Landing traci sol (18.5% -> 4.1%), bo jej dwie wsie solne leza w malo ludnych Ziemiach Korony (13.5 tys. ludzi na wies).

### 6.2 Liczby dla lnu, skor, soli, rudy i drewna (sztuk na dobe; suma swiata bez zmian)

Stawka = dzisiejsza produkcja swiata / ludzie wsi z tym symbolem. To jest liczba, ktora wstawiona do reguly "wies daje stawke x swoich ludzi" zostawia sume swiata bez zmian [S z pomiaru].

| Surowiec (typ wsi) | Wsi | Ludzie tych wsi | Swiat dzis | Stawka na 1000 ludzi tych wsi na dobe | Najwieksze zmiany dzis -> "od ludzi" |
|---|---|---|---|---|---|
| Zboze (wheat_farm) | 123 | 8 579 tys. | 6 562 (+ 2 253 z pozostalych wsi: "zboze 3" kazdego typu i zywnosc wsi) | 0.765 | Reach 777 -> 1 787; Volantis 161 -> 445; Polnoc 857 -> 631; Dorne 321 -> 140; Wyspy 321 -> 73 |
| Ryby (fisherman) | 66 | 4 152 tys. | 2 079 | 0.501 | Reach 168 -> 337; Volantis 61 -> 245; Wyspy 184 -> 49; Wolni Ludzie 122 -> 32; Dorne 214 -> 119 |
| Len (flax_plant) | 20 | 1 455 tys. | 484 | 0.333 | KL 83 -> 98; Stormlands 47 -> 65; Braavos 47 -> 65; Norvos 47 -> 61; Reach 24 -> 50; Polnoc 71 -> 46; Wyspy 24 -> 5; Wolni Ludzie 24 -> 6 |
| Welna (sheep_farm) | 24 | 2 133 tys. | 275 | 0.129 | Dolina 43 -> 55; Reach 22 -> 51; Qarth 11 -> 36; Volantis 11 -> 32; Polnoc 22 -> 12; Nocna Straz 27 -> 0.5 |
| Sol (salt_mine) | 12 | 664 tys. | 511 | 0.770 | Volantis 38 -> 151; Stormlands 114 -> 133; Dorne 76 -> 54; KL 95 -> 21; Wyspy 38 -> 11 |
| Ruda / lupa (iron_mine) | 26 | 2 022 tys. | 250 | 0.123 | Reach 28 -> 58; KL 19 -> 28; Volantis 9 -> 24; Wyspy 37 -> 8; Nocna Straz 14 -> 0.3 |
| Drewno drwali (lumberjack) | 43 | 2 625 tys. | 302 | 0.115 | Reach 22 -> 53; Qarth 7 -> 25; Polnoc 50 -> 40; Wolni Ludzie 22 -> 5 |
| Drewno z lasu wsi (kazda wies bez drwali) | 528 | 38 488 tys. | 1 748 | 0.045 | Reach 141 -> 321; Volantis 51 -> 170; Qarth 11 -> 104 (dzis klimat pustyni x0.32); Polnoc 225 -> 119 |
| Skory (uboj zywca: wsie bydla, owiec, swin i zbozowe) | 181 | 12 860 tys. | 371 | 0.029 | Reach 33 -> 88; Volantis 7 -> 24; Polnoc 54 -> 35; Dragonstone 17 -> 6 |
| Glina (clay_mine) | 16 | 1 164 tys. | 368 | 0.316 | - |
| Winogrona (vineyard) | 27 | 1 977 tys. | 274 | 0.139 | - |
| Oliwki (olive_trees) | 19 | 2 016 tys. | 260 | 0.129 | - |
| Daktyle (date_farm) | 14 | 1 028 tys. | 122 | 0.118 | - |
| Jedwab surowy (silk_plant) | 14 | 1 779 tys. | 69 | 0.039 | - |
| Srebro (silver_mine) | 30 | 2 460 tys. | 108 | 0.044 | - |
| Futra (trapper) | 25 | 1 207 tys. | 18 | 0.015 | - |

Uwaga do lasu wsi: jesli drewno pojdzie za ludzmi BEZ klimatu, Qarth (pustynia) dostanie 10x wiecej drewna niz dzis - klimat musi zostac jako poprawka (rozdz. 7).

### 6.3 Przyklad Jeffa na liczbach biegu

Wies z symbolem zboza [P ludzie; S przeliczenie]: dzis kazda daje ok. 53.4 sztuki na dobe. Przy stawce 0.765 sztuki na 1000 ludzi (suma swiata bez zmian): Polnoc 51.6 tys. ludzi -> **39 sztuk**, Reach 166.9 tys. -> **128**, King's Landing 63.9 tys. -> 49, Dorne 30.5 tys. -> 23, Zelazne Wyspy 16.1 tys. -> 12, Volantis 193.7 tys. -> 148. Stosunek Reach do Polnocy 3.2 : 1 (w przykladzie Jeffa 3.7 : 1).

## 7. Poprawki wydajnosci na czlowieka: co gra juz mierzy, a czego nie

| Poprawka | Miara w swiecie gry | Dzis w kodzie | Historyczny rozrzut [S] | Uwaga |
|---|---|---|---|---|
| Klimat | klasa krainy (kultura regionu z dnia kalibracji) | las wsi: pustynia 0.32, step 0.54, srodziemnomorskie 0.86, reszta 1.07, lesne 1.29 (log l.272); typ wsi dopasowany do klimatu - 28 podmian (`VillageClimate.cs`, log l.269) | plon zboza z ziarna 3-4:1 na zimnej polnocy, 4-5:1 w Anglii, 6-8:1 na najlepszych ziemiach - rozrzut ok. 0.7-1.5 wokol sredniej | miara jest; dla zboza, lnu i hodowli klas jeszcze nie ma - do policzenia w projekcie |
| Pora roku | kalendarz Westeros | zima tnie plon o ok. 38%, cieple poludnie o ok. 15% (`Settings.cs:444-451`); w tym biegu zimy nie bylo | zima = brak plonu z pola, zapas ze zniw | miara jest; nie sprawdzona na roku z zima |
| Rozwoj osady | budynki miasta, poziom hearth | budynek +5/10/15% produkcji wsi (`DefaultBuildingTypes.cs:241, 294`); progi hearth 0.5 / 1.0 / 1.5 (`DefaultVillageProductionCalculatorModel.cs:31`) | - | prog hearth stanie sie zbedny, gdy plon pojdzie wprost za ludzmi (inaczej liczy to samo drugi raz) |
| Narzedzia zelazne | polka narzedzi w miescie targowym wsi | brak | plug z zelaznym lemieszem i kosa: ok. +10-30% wydajnosci pracy - slaba liczba | miara jest (wsie juz kupuja 87 narzedzi na dobe); swiat ma ich 146 na dobe - poprawka musi byc mala i z granica |
| "Technologia" kultury | - | brak miary w grze; BK ma klasy ziemi i rece robocze (`BKVillageProductionModel.cs:171-223`), ale ten model nie biegnie (log l.464) | - | **nie ma dobrej miary - zostawic na pozniej** |
| Bezpieczenstwo | spustoszenie okregu, wolne rece | spustoszenie tnie plon (log l.130, "plon ^1"); brak rak: 10% rak mniej = 5% plonu mniej (`Settings.cs:863-865`) | - | jest, dziala dla spichlerza; dla towaru do sprawdzenia w rozpoznaniu kodu |

Granice rozsadku z historii [S]: laczna poprawka wydajnosci na czlowieka miesci sie w ok. 0.6-1.5; wszystko ponad to jest juz roznica liczby ludzi, nie wydajnosci.

## 8. Rzeczy zauwazone przy okazji (do decyzji projektu, nie naprawiane)

1. **Jedzenie w nicosc** [P]: "przepadlo z rozbitymi partiami" 1 114 sztuk na dobe (jablka 172 z 204 wytworzonych, marchew 175 z 207, piwo 114, winogrona 113, zboze 111, oliwki 69, chleb 57). Zrodlo tych jablek, marchwi i miodu to "inne ticki dobowe: osady" - towar powstaje w osadach bez wsi-producenta i znika z druzynami. Sprzeczne z zasada "nic z niczego, nic w nicosc".
2. **Nadwyzka spichlerzy w nicosc** [P]: plon 49.16 mln racji na dobe, zjedzone 41.30 mln, "nadwyzka ponad sufit" 7.99 mln racji na dobe przepada (spichlerze pelne, 727 dni zapasu). To jest dokladnie ta czesc plonu, ktora w historii szla na targ (ok. 16% plonu; historia 30-40% wartosci zboza).
3. **"felt" (sukno) na liscie kategorii jedzenia** w linii "Kiesa ludu (E5/0)" (log l.731: "kategorie jedzenia: ... wine, felt, Eggs ...") - nie sprawdzalem w kodzie, czy to blad listy, czy tylko napisu.
4. **Zapasy rosna bez konca** [P]: uzbrojenie na polkach x6, konie x1.9, drewno x14, ryby x2.2, ruda x9.7, glina x8.4, klejnoty x41 w rok.
5. **Model BK "plon z rak roboczych"** istnieje, ale w tym zestawie modow nie biegnie (czynny lancuch NavalDLC -> gra) [P log l.464].

## 9. Czego nie wiem

- Liczby miast bez towaru dla zywnosci, soli, narzedzi, ceramiki, sukna, piwa, wina - log tego nie mierzy.
- W ktorych krolestwach stoja warsztaty poszczegolnych typow.
- Dlaczego liczba wsi wedlug typu z XML + T8 rozni sie o 1-4 od linii "Produkcja wsi - model".
- Jak wyglada rok z zima (w biegu jej nie bylo) - produkcja zywnosci bedzie nizsza.
- Ile lud kupilby plotna, skory i soli, gdyby byly na polkach (popyt ukryty) - ksiega widzi tylko to, co kupiono.
- Wzorce dla srebra, zlota, klejnotow, aksamitu, futer, kamienia - brak rozsadnej liczby na glowe.
- Jak dokladnie liczone sa zakupy wsi w miescie (0.07 zl na glowe dziennie w ustawieniu, a zmierzone zakupy to ok. 6.5 tys. zl na dobe na 41 mln ludzi) - to pytanie do rozpoznania kodu.

## 10. Pliki

Katalog `gosp-ludzie\skrypty-towary\`:
- `towary_rok.py` -> `towary_rok.json`, `towary_rok.txt` (ksiega towarow: zrodla i ujscia kazdego towaru, start / koniec / rok)
- `tabela_towary.py` -> `tabela_towary.tsv`
- `serie.py`, `serie_podsum.py` -> `serie.json`, `serie_podsum.txt` (miasta bez towaru, konie, uzbrojenie, spichlerze, odziez wojska, warsztaty)
- `geografia.py` -> `geografia.json` (wsie: typ, krolestwo, ludzie); `geografia_towary.py` -> `geografia_towary.txt` (tabele wedlug krolestw dla 16 surowcow); `udzialy.py`
- `skala_i_wzorzec.py` -> `skala_i_wzorzec.txt`, `skala_i_wzorzec.json` (skala zywnosci N0 i porownanie z wzorcem)
- `md_tabele.py` -> `md_tabele.md`, `md_wzorzec.md`; `przedmioty.py` (wagi towarow z XML); `prefiksy.py` (spis linii logu)
