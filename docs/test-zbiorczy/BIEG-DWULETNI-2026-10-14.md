# BIEG DWULETNI WERSJI KONCOWEJ (14.10) - SYNTEZA

Bieg: nowa kampania, 728/728 dob, kod `w-toku/zbior-e8` **43af413** (= WGRANIE 49 + lup 5b; Armoury 6dd1e9e5 = test\fin21). W tym biegu NIE ma poprawek po audycie (c3aa637); wgrana wersja koncowa WGRANIE 50 = c3aa637 (Armoury 3cceb8cc).
Log `D:\Backup-Bannerlord\logs\2026-10-14-noc\Armoury-2026-10-13_21-14-13.log` (278 MB), tabele `...\Logs\2026-10-13_21-14-13\`, sesja CS `session-2026-10-13_21-14-11.log`.
Porownania: bieg roczny kodu 43 bez gospodarki od ludzi (`Armoury-2026-10-13_15-02-59.log`, dalej **W43**) i bieg roczny gospodarki od ludzi (`Armoury-2026-10-13_19-21-12.log`, dalej **GL**); rachunek na sucho `docs/gospodarka-od-ludzi/sucho85/WYNIK-RACHUNEK-85-TYS.md` rozdz. 7.
Miara progu 85 tys. = W5-7: wojsko rodow krolestw w WOJNIE z linii `Skarbce:`, srednia 28 dob okna (doba = dzien gry - 108836 + 1).
Znaczniki: **[P]** pomiar z logu / tabel, **[S]** szacunek albo wniosek; czego nie da sie ustalic - "nie wiem".

Raporty zrodlowe (trzy analizy; tu tylko wyciag i odsylacze) - `SCRATCH = C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\c36b73ca-1de0-4b1b-80ce-71bd02afe545\scratchpad\dwuletni`:
- `SCRATCH\wojsko\RAPORT.md` - wojsko i pieniadz (miara, krolestwa, te same rody, zalogi, 10 progow alarmowych, doplaty, pieniadz swiata, zdobycia);
- `SCRATCH\gospodarka\RAPORT.md` - gospodarka od ludzi, towary, lud (8 grup progow, zima, glod, plon, regresja ziemi, sciana ludu, danina, towary, Qarth, budowy, zbytki, konie);
- `SCRATCH\paczki\RAPORT.md` - paczki nocy i stabilnosc (bledy, czas doby, zapis, N7, lup, M6a, W5-W7, lista 35 NIE z ocena).
Uwaga: SCRATCH to katalog tymczasowy na dysku C - wyniki narzedzia i skrypty sa tylko tam.

## 0. Dla Jeffa - prostym jezykiem

1. **Czy wersja jest gotowa do gry? Do zaczecia nowej kampanii - tak, ale Twoje wlasne sciezki (lenno, tarczowe, lup, rozbici gracza) nie przeszly zadnego testu w grze - to 10 punktow z rozdz. 9. Do grania dluzej niz ok. 450 dob - jeszcze nie** (pkt 4 i 8e; licznik z 8e moze wyskoczyc juz w roku 1 - w biegu W43 byl w dobie 287). Dwa lata gry przeszly bez wywrotki: 728/728 dob, 0 bledow naszych modow, doba 12.3 s i nie zwalnia, 13 zapisow OK [P]. Wgrana wersja koncowa (WGRANIE 50) ma jeszcze drobne poprawki po audycie - przeszla test 40 dob i wczytanie zapisu z doby 720 tego biegu, 0 bledow [P wedlug STAN-PRAC].
2. **Wojsko w roku 1: 108.9 tys.** lordow w wojnie na koniec roku przy progu 85 tys. [P] - wiecej niz w obu biegach rocznych (101.3 i 96.4) [P]; do ok. doby 450 swiat trzymal ok. 110 tys. [P].
3. **Wojsko w roku 2: 55.3 tys. - ok. 30 tys. pod progiem** [P]; najnizej 36.1 tys. w dobie 537 [P].
4. Przyczyna to glownie mapa wojen (pieniadz to ok. 1/10 spadku): w dobach 451-513 16 krolestw zawarlo pokoj i w wojnie zostalo 10-17 krolestw zamiast 26 [P]. **Dlaczego naraz:** te same wojny (Lys-Tyrosh, Lorath-Braavos, Norvos-Qohor, Ibben-Sarnor, Myr-Pentos, Dothrakowie-Targaryen, Qarth-Targaryen, Dorne-Reach, Wyspy-Dolina) do dob 371-422 byly wojnami fabularnymi ROT - po kazdym pokoju ROT wypowiadal je od nowa; ich nastepny pokoj (d449-511) log liczy juz jako niefabularny i nikt ich nie wznawia [P] (krytyka, rozdz. 11). ROT przestal je wymuszac miedzy doba 422 a 449 - dlaczego, nie wiem; prawdopodobnie tak samo bedzie w Twojej kampanii [S]. Krolestwo w pokoju rozpuszcza wojsko prawie do zera (132 rody: 38 tys. -> 1.5 tys. ludzi; Dolina 7.8 tys. -> 25 ludzi) - to w duzej czesci NASZA regula budzetu rodow: w pokoju pulap zoldu 0.28 dochodu zamiast 0.60, zalogi placone pierwsze, a danina i doplaty ida tylko w wojnie [P wedlug kodu ClanBudget.cs; ile z tego to ta regula - S]. I juz nie wraca do wojny - Dolina, Dothrakowie, Qohor, Braavos, Lys i Sarnor staly w pokoju 220-279 dob do konca [P]. Te same rody, ktore walczyly w obu latach, stracily 7-10% ludzi [P, zalezy od tego, kogo liczyc]; przy mapie wojen z roku 1 byloby ok. 95-101 tys. [S].
5. Od doby 460 przychodzi dluga zima ROT (1 316 dob): wsie daja 41% mniej, zboza jest o 31% mniej [P]. W Twojej nowej kampanii przyjdzie w tej samej dobie [S].
6. **Gospodarka od ludzi dziala:** wies zboza w Reach daje 3.3 razy tyle co na Polnocy, bo ma 3.2 razy wiecej ludzi [P]; dochod panow z ziemi idzie za ludzmi ("waga ludzi" 0.63-0.81 w roku 1, bez reguly 0.15-0.27) [P]; przez 2 lata nikt nie umarl z glodu, ludzi ubylo 1.0% w roku 1, w roku 2 liczba stoi [P]; pieniadz swiata jest szczelny - 1.0% mniej w 2 lata (rok 1 -0.2%, rok 2 -0.8%), zloto z niczego 0 [P].
7. **Co dzialalo z nowych regul:** 102 wiersze grup paczek nocy (67 progow z ocena + 35 INFO), 1 NIE (stary W5-4) [P]; W6b w autotescie praktycznie niesprawdzalny (gracz bez lenna). Rozbici wracaja: z 359 tys. rozbitych 191 tys. (53%) wrocilo do druzyn rodu; z tych, ktorzy czekali na odbior, po czasie rozeszlo sie tylko 6 tys. (97% odebranych) [P]. Lup miast: 39 zdobyc, 1.9 mln zl lupu, 108 poziomow budowli splonelo, polowa juz odbudowana [P]. Najemnicy nie skacza (0 krotkich sluzb, 0 przejsc do wroga), okup powtorny jest tanszy, pokoj na jeden dzien nie wypuszcza jencow [P]. Stawka wojenna pana (W7) bierze 14.7 tys. zl dziennie u 24 rodow [P]. Lnu jest dwa razy wiecej (860 szt./d) [P]. Skarbiec stolicy zadzialal 3 razy (0.42 mln zl) [P].
8. **Co slabe:** (a) "pulapka pokoju" z pkt 4; (b) pieniadz panow slabnie w roku 2: brak, ktorego nikt nie pokryl, wzrosl z 24 do 54 tys. zl dziennie, zapas skarbcow koron prawie sie skonczyl (64 -> 6 tys. zl dziennie) [P]; najbiedniejsi: Wolni Ludzie, Zelazne Wyspy, Pentos, Smocza Skala [P]; (c) biedota wielkich miast w zimie: na koniec 7 miast nie stac na jedzenie (Highgarden, Oldtown, Braavos i in.), chleb swiata 89.7% przy celu 98% - ludzie jedza (gra daje), ale biednieja [P]; nocna decyzja "to cecha" (03:55) byla na danych roku 1 - w roku 2 chleb spada co kwartal (97.1 -> 95.7 -> 95.3 -> 92.3 -> 90.9%) i dna nie widac, wiec to sprawa do ponownej decyzji, nie cecha [P]/[S]; (d) plotna brak w 75-77 z 97 miast - waskim gardlem sa tkalnie, nie len [P]/[S]; (e) **blad licznika podatku wsi**: od doby 699 licznik jednej wsi skacze o 2.1 mld zl [P] - nikt tego nie dostal [P], ale gdyby gra to wyplacila, pan dostalby miliardy [S]; (f) zabor karawan wroga w zdobytym miescie nie zadzialal ani razu w 39 zdobyciach - nie wiem, czy kod sie odpala.
9. **Co bym zrobil dalej - kolejnosc:**
   1. blad licznika podatku wsi: sprawdzic w kodzie, czy te miliardy moga trafic do pana, i postawic straz (maly tor). Nie czeka do doby 700: w biegu W43 ten sam skok byl juz w dobach 287-292 (inna wies, Tumblestone Farms); w obu biegach przy wsi "bez podatku BK" i zloto poszlo "bez odbiorcy" [P];
   2. Twoj reczny test sciezek gracza - lista w rozdz. 9 (ok. godzina); autotest gra bez lenna i sam nie zdobywa miast, wiec tych sciezek nie sprawdzil [P];
   3. "pulapka pokoju": najpierw z kodu - dekompilacja ROT `ROTStorylineWars` (`IsWarForced` / `EnforceWars`: co konczy wymuszanie wojen Essos ok. doby 422-449) i nasza regula budzetu w pokoju (`ClanBudget`: pulap 0.28 D, zalogi pierwsze; suwak `PeaceWageShare`) oraz czemu krolestwo bez wojska nie wraca do wojny; potem rachunek na sucho "krolestwo w pokoju trzyma czesc wojska i zalog"; dopiero potem kod i nowy bieg dwuletni - bez tego progu 85 tys. w roku 2 nie bedzie [S];
   4. pieniadz biednych rodow w roku 2 (Wolni Ludzie, Wyspy, Pentos, Smocza Skala);
   5. biedota wielkich miast w zimie i tkalnie plotna;
   6. karawany wroga (L5b-2) - czy kod w ogole moze sie odpalic;
   7. na koncu wydajnosc (zbytki 5.6 ms / dobe i in. - Twoja decyzja 08.10: optymalizacja na koniec).

## 1. Stabilnosc, czas, zapis (zrodlo: `paczki\RAPORT.md` rozdz. 2)

| co | bieg dwuletni (728 d) | W43 (364 d) | GL (364 d) |
|---|---:|---:|---:|
| doby / AUTOTEST KONIEC | 728/728 OK, 149.8 min [P] | 364/364 OK | 364/364 OK |
| ERROR w logu Armoury | 0 [P] | 0 | 0 |
| CS: ERROR IN A MOD | 0 [P] | 0 | 0 |
| CS: ERROR CAUGHT FURTHER UP | 25 = 23 przy starcie + 2 w biegu (cudzy kod) [P] | 23 | 23 |
| CS: wyjatek polkniety przez nasz Mends | 1 (BK ClaimantDemand NRE, d626) [P] | 0 | 0 |
| GAME HANG | 1, po QuitGame, poza kampania [P] | 0 | 1 (tez po wyjsciu) |
| s / dobe | rok 1 12.43, rok 2 12.27; trend -0.02 s/d na rok [P] | 12.52 | 12.58 |
| takt doby naszych modow | 398 -> 417 ms (+5%, 0.15% doby) [P] | 372 | 381 |
| zapisy | 13 OK, 4.7 s (d60) -> 5.5 s (d720) [P] | | |
| arm_people (znaki) | 438 523 (d60), max 749 916 (d420), 707 615 (d728) - nie rosnie bez konca [P] | | |
| pole N7 (znaki) | max 34 057 (rok 1) - ponad 32 767, SaveText dzieli na kawalki po 8 000 [P] | | |

- Nowe sygnatury (cudzy kod, gra zyje) [P]: 2 x `AmbiguousMatchException` BKROTPatch `ClanActionsKnighthoodCleanupPatch` (d382, polkniety w BK); 1 x NRE BK `ClaimantDemand.SetTexts` (d626, polkniety przez Mends).
- Potkniecia > 0: tylko GL2 w dobie 1 (2 - zloto startowe kies wsi) [P]; naprawione w c3aa637 - test 40 dob fin22: zloto startowe wsi zgodne (2 wsie dwa razy na liscie gry, teraz pomijane) [P wedlug STAN-PRAC].
- Wczytanie zapisu z pola N7 ponad 32 767 znakow - nikt nie sprawdzal; nie wiem. Wczytanie zapisu z doby 720 tego biegu + 9 dob: 0 ERROR [P wedlug STAN-PRAC].

## 2. Wojsko i prog 85 tys. (zrodlo: `wojsko\RAPORT.md` rozdz. 1-7, 9-10)

### 2.1 Miara W5-7 [P]

| okno | dwuletni | krolestw w wojnie | wojsko wszystkich krolestw (tez w pokoju) | W43 | GL |
|---|---:|---:|---:|---:|---:|
| d93-120 | 99.5 | 25.0 | 100.2 | 112.2 | 101.6 |
| d337-364 (koniec roku 1) | **108.9** | 26.0 | 109.0 | 101.3 | 96.4 |
| Q5 d365-455 | 110.4 | 25.9 | 110.7 | - | - |
| d449-476 | 83.0 | 19.0 | 92.7 | - | - |
| d533-560 (dno) | 43.9 | 10.1 | 51.4 | - | - |
| d589-616 | 62.7 | 16.8 | 64.5 | - | - |
| d701-728 (koniec roku 2) | **55.3** | 17.3 | 57.4 | - | - |

Kwartaly roku 2: 110.4 / 59.9 / 56.7 / 55.1 tys.; minimum dobowe 36.1 tys. (d537) [P]. Narzedzie `--grupa w5 --do-doby 728`: W5-7 55.3 [P]; etap2 (inna srednia) 55 550 [P]. Pelne tabele: `wojsko\RAPORT.md` rozdz. 2.

### 2.2 Skad spadek: te same rody, d337-364 -> d701-728 [P] (`wojsko\a8_te_same_rody.out.txt`)

| grupa rodow | rodow | ludzie w partiach | zmiana | wypelnienie limitu | kiesy rodow (mln) | zalogi |
|---|---:|---|---:|---|---|---|
| w wojnie w obu oknach | 176 | 56 544 -> 50 683 | -5.9 tys. (-10.4%) | 63.2% -> 58.1% | 35.8 -> 37.1 | 48 484 -> 47 822 |
| z wojny do pokoju | 132 | 38 125 -> 1 486 | -36.6 tys. | 69.7% -> 5.3% | 26.4 -> 29.0 | 29 398 -> 6 462 |
| mieszane | 28 | 9 578 -> 2 191 | -7.4 tys. | 62.3% -> 21.0% | 4.3 -> 4.7 | 8 672 -> 3 282 |
| tylko w jednym oknie | 21 | 2 015 -> 343 | -1.7 tys. | | | |
| z pokoju do wojny | 1 | 15 -> 566 | +0.6 tys. | | | |

Skladniki (tabela budzet-rodow.csv) sumuja sie do -51.0 tys. przy -53.6 tys. w mierze z linii `Skarbce` - dwie rozne tabele [P]. Wniosek: ok. 44 tys. spadku to rody, ktore wyszly z wojny (mapa), ok. 4-6 tys. to tez rody w wojnie w obu oknach [P]. Uwaga krytyki: grupa "w wojnie w obu" liczy rod z >= 22 z 28 dob wojny; przy scislym 28 z 28 (`synteza\kryt_rody.py`) jest 175 rodow 55 041 -> 50 961 (-4.1 tys., -7.4%) [P]. Ze ten spadek to "pieniadz", a nie tez straty w bitwach - [S] (kiesy tych rodow rosna 35.8 -> 37.1 mln; spada dochod: danina, W5b, 1/360). "Gdyby mapa zostala": 108.9 x 0.896..0.926 = 97.6..100.8, z utrata Aegona i Krain Burzy ok. 95-101 tys. [S].

### 2.3 Fala pokojow i krolestwa [P]

- Pokoje: Lys i Tyrosh d451, Lorath d453, Dorne / Qarth / Reach d455, Volantis d460, Qohor / Norvos d463, Sarnor d477, Braavos / Ibben d495, Dolina / Dothrakowie / Targaryen (Meereen) d508, Myr d513 - wojny trwaly od doby 24 (`wojsko\RAPORT.md` rozdz. 3). Pokoje z decyzji krolestw; "pokoj z biedy" - 0 wnioskow przez caly bieg.
- Wznowienia wojen fabularnych przez ROT (`EnforceWars`, CS `Reparacje (T9)`): 98 do doby 451, potem 13 [P]. Wojny zakonczone w d451-513 sa w logu "niefabularne" - nikt ich nie wznawia [P]. Wypowiedzen wojen: 112 w roku 1, 65 w roku 2 [P].
- Dlaczego naraz - **poprawione w krytyce (rozdz. 11)**: 10 par z fali (Lys-Tyrosh, Lorath-Braavos, Dorne-Reach, Qarth-Targaryen, Aegon-Volantis, Norvos-Qohor, Ibben-Sarnor, Wyspy-Dolina, Dothrakowie-Targaryen, Myr-Pentos) mialo wczesniej pokoje FABULARNE (ROT wznawial wojne po kazdym; ostatni taki pokoj d248-422, Lorath-Braavos 11 razy), a pokoj w fali (CS d449-511) jest juz NIEFABULARNY [P]. ROT przestal wymuszac te wojny miedzy doba 422 a 449 (jesli to jeden przelacznik dla wszystkich; regula para po parze tez pasuje - nie wiem); po d422 wymusza juz tylko wojny Westeros (Riverlands-KL, Wolni Ludzie-Straz, Polnoc-KL, Smocza Skala-Reach/KL) [P]. Hipoteza STAN-PRAC 06:49 ("koncza sie wymuszone wojny fabularne ROT") jest wiec zgodna z logiem - sprzecznosci zrodel nie ma. Co w ROT konczy wymuszanie - **nie wiem** (kolejka fabuly ROT miedzy d129 a d520 nic nie startuje [P]; potrzebna dekompilacja `ROTStorylineWars`). Wojny "odnowione" przez ROT maja nowa date poczatku, wiec Diplomacy blokuje pokoj ("This war hasn't gone on long enough") - od ostatniego pokoju fabularnego pary do pokoju w fali minelo 31-98 dob [P]; ze to blokada Diplomacy - [S]. Czy tak bedzie w kazdej kampanii - jeden bieg dochodzi do d450, wiec [S] (wymuszanie jest w kodzie ROT, nie w losie).
- Powrot do wojny odbudowuje: Dorne d537 450 -> d616 8 536 ludzi; Volantis 132 -> 4 017; Qarth 318 -> 1 987 [P].
- Krolestwa na koniec praktycznie bez wojska: Dolina 47, Qohor 17, Braavos 34, Lys 36, Qarth 60, Sarnor 79, Dorne 86 ludzi; zalogi Qarth 93, Lys 90, Qohor 241 [P]. Czy straca twierdze przy pierwszym ataku - nie wiem (w roku 2 tylko 12 zdobyc).
- Zniknely w roku 2: Targaryen (Aegon) d459, Krainy Burzy d536 (14 rodow do Smoczej Skaly), 5 malych buntow; powstalo 13 malych krolestw (0.1-0.9 tys. ludzi) [P].

| krolestwo (ludzie) | d337-364 | d701-728 | zmiana [P] |
|---|---:|---:|---:|
| zyskali: Smocza Skala / Skagos / Riverlands / Reach / Pentos | 3 144 / 16 / 7 228 / 8 212 / 2 397 | 5 778 / 1 178 / 8 110 / 8 939 / 2 715 | +2.6 / +1.2 / +0.9 / +0.7 / +0.3 tys. |
| stracili (pokoj): Dolina / Dorne / Dothrakowie / Targaryen / Qarth | 7 453 / 6 709 / 4 183 / 4 883 / 3 148 | 47 / 86 / 220 / 992 / 60 | -7.4 / -6.6 / -4.0 / -3.9 / -3.1 tys. |
| zniszczone: Krainy Burzy / Aegon | 7 158 / 2 263 | 0 / 0 | -7.2 / -2.3 tys. |
| w wojnie caly czas: Polnoc / KL / Straz / Wyspy | 10 810 / 8 816 / 4 225 / 3 800 | 10 324 / 8 623 / 1 615 / 2 777 | -0.5 / -0.2 / -2.6 / -1.0 tys. |
| **razem wszystkie krolestwa** | **109 013** | **57 431** | **-51.6 tys.** |

Krolestwa w wojnie w obu oknach (9): 49.9 -> 49.7 tys. [P]. Pelna tabela 29 krolestw: `wojsko\RAPORT.md` rozdz. 4.

### 2.4 Zalogi [P]

| | d93-120 | d337-364 | d701-728 |
|---|---:|---:|---:|
| zalogi krolestw (balans-krolestw.csv) | 81.2 tys. | 89.2 tys. | **58.2 tys.** |
| W43 / GL | 86.3 / 83.2 | 85.0 / 85.2 | - |
| wojsko bez krolestwa (partie) | 2.1 | 5.2 | 8.9 |

Rody w wojnie w obu oknach trzymaja zalogi (48.5 -> 47.8 tys.), krolestwa w pokoju je traca (Dolina 5.5 -> 0.9, Dorne 5.1 -> 1.5, Qarth 1.8 -> 0.1 tys.) [P]. Etap4 N4 liczy 58 366 [P].

### 2.5 Pieniadz koron i doplaty, srednia zl/d [P] (`wojsko\RAPORT.md` rozdz. 7)

| strumien | W43 rok 1 | GL rok 1 | dwuletni rok 1 | **dwuletni rok 2** | d701-728 |
|---|---:|---:|---:|---:|---:|
| danina P1 | 135.9 tys. | 150.5 | 274.2 | **273.3** | 210.2 |
| W5 wyplacono | 34.7 | 33.3 | 42.7 | **32.9** | 28.3 |
| W5b ze skarbca | 16.6 | 8.3 | 9.1 | **3.5** | 0.0 |
| W6 tarczowe | 1.2 | 0.7 | 1.1 | 2.1 | 0.4 |
| W7 stawka pana (lud / wsie) | - | 10.4 | 12.1 | **14.7 (10.3 / 4.4)** | 14.7 |
| 1/360 zapasu koron | 66.7 | 67.7 | 64.4 | **12.0** | 6.1 |
| potrzeba W5 | 78.4 | 80.4 | 87.5 | 98.2 | 85.7 |
| niepokryte po W5 / W5b / W6 | 11.5 | 27.4 | 23.7 | **53.9** | 53.4 |
| N7 odebrani przez druzyny (ludzi / d) | - | 299 | 333 | **194** | 156 |

W ludziach w roku 2: W5 ok. 5.0, W5b 0.5, W6 0.3, W7 2.2 tys. - razem ok. 8 tys. (rok 1 ok. 10 tys.) [S]; ile z N7 to wojsko ponad zwykly werbunek - nie wiem. Niepokryte na koniec (tys. zl/d): Wolni Ludzie 19.2, Zelazne Wyspy 15.7-17.9, Pentos 8.7-9.0, Smocza Skala 3.1-9.8 [P]. Biedne rody (G < R, w wojnie): 87 na koniec, G/R 0.85 -> 0.54, zold przyciety 3.8 -> 6.0 tys. zl/d (szczyt 16.7 tys. w d505-532) [P]. Saldo rodow bez luzu: -59.9 i -62.4 tys. zl/d w d365-476, potem -15..-28 (jedno okno +12.0) [P].

### 2.6 Dziesiec progow alarmowych z rachunku na sucho (rozdz. 7.6) - TAK = alarm [P]

| # | co | rok 1 (d337-364) | rok 2 - pomiar | ALARM |
|---|---|---|---|---|
| 1 | miara W5-7 | 108.9 | koniec 55.3 (< 90); bez mapy (te same rody) -7.4..-10.4% | **TAK** |
| 2 | kiesy ludu / danina P1 | 23.9 mln / 419 tys. | kiesy min 22.5 mln; danina < 170 tys. tylko w 1 oknie (d533-560: 152.5) | NIE (na granicy) |
| 3 | biedne rody: ludzie / G/R | 28.4 tys. / 0.56 | min 12.0 tys. (d561-588); spadek > 1.5 tys. na 28 dob w 3 oknach | **TAK** (glownie pokoje) |
| 4 | rody pod klifem (G < 0.25 R) | 1.7 tys. | max 2.19 tys. | NIE |
| 5 | saldo rodow bez luzu | -3.2 / -10.4 tys. zl/d | -59.9 i -62.4 (d365-476) | **TAK** (pieniadz, jeszcze przy pelnej mapie) |
| 6 | skarbce koron / dlug koron w Banku | 22.3 / 0.94 mln | dlug max 1.35 mln; zwrot niepelny >= 14 dob max w 1 krolestwie | NIE |
| 7 | luz w puli zoldu (paliwo N7) | 19.6 tys. | d701-728 10.3 tys. (< 15) | **TAK** |
| 8 | N7 - skutek w mierze | - | nie do wydzielenia w jednym biegu | nie wiem (formalnie TAK) |
| 9 | krolestw w wojnie / wojsko bez krolestwa | 26.0 / 5.2 tys. | 17.3 (min 10.1) / 8.9 tys. | **TAK** |
| 10 | Polnoc / Dorne / Zelazne Wyspy | 10.8 / 6.7 / 3.8 tys. | 10.3 / 0.09 / 2.8 tys. (Wyspy min 1.4) | **TAK** (Dorne w pokoju od d628) |

Razem 6 TAK, 3 NIE, 1 nie wiem. Progi 1, 3, 7, 9, 10 wywala glownie mapa (pokoje), prog 5 to pieniadz [P]/[S].

### 2.7 Wobec rachunku na sucho

| | rachunek | pomiar |
|---|---|---|
| koniec roku 1, S3 | 93.3 (87.9-99.1) | **108.9** [P] |
| rok 2-3, S3 z N7 (srodek) | 90.5 | **55.3** [P] |
| najgorszy realny przypadek | 73 | 55.3 - 18 tys. nizej [P] |
| skladnik "mapa" | -1.5 tys. (2 krolestwa wybite) | ok. -44 tys. [P] |
| skladnik "pieniadz" | ok. -6 tys. (-5.9%) | te same rody -7.4..-10.4% [P] = ok. -8..-11 tys. na pelnej mapie [S] |

Rachunek dobrze trafil kierunek pieniadza, ale nie mial w modelu tego, ze krolestwo w pokoju schodzi do zera i nie wraca do wojny [S] - ani tego, ze ROT ok. d422-449 przestaje wymuszac wojny Essos (rachunek liczyl na biegu W43, w ktorym przez caly rok wymuszanie trwalo) [P].

### 2.8 Zdobycia [P]

Twierdze zdobyte oblezeniem: 27 w roku 1, 12 w roku 2 (W43 18, GL 30 w roku 1). Upadki stolic 3 (wszystkie w roku 1, 0.42 mln zl).

## 3. Pieniadz swiata i blad licznika podatku wsi (zrodla: `wojsko` rozdz. 8, `gospodarka` rozdz. 12, `paczki` rozdz. 9)

| | d2 | d364 | d728 |
|---|---:|---:|---:|
| razem (bez skoku licznika) | 209.18 mln | 208.69 mln | **207.04 mln** [P] |
| skarbce krolestw | 57.80 | 23.75 | 15.67 |
| glowy rodow | 54.87 | 66.20 | 69.50 |
| kiesy ludu miast / podzamcza | 6.75 / 0.76 | 20.49 / 3.14 | 29.09 / 5.29 |
| sakiewki ludzi | 0.53 | 24.24 | 11.47 |
| kasy miast | 24.04 | 11.93 | 11.55 |

- 2 lata: -1.0% (209.18 -> 207.04 mln; rok 1 -0.2%, rok 2 -0.8%) [P] - poprawione w krytyce (bylo "-0.8% w 2 lata"); reszta [R] roku 2 -0.55 tys. zl/d [P]. Zrodla bez platnika = 0 we wszystkich 727 dobach (bitwy netto 0, notable 89 177 wobec przelewu 89 313) [P].
- **BLAD licznika** (stary - byl w W43 w d287-292, "Tumblestone Farms"; nie z paczek nocy) [P]:

| doba | suma "Pieniadz swiata" | skok | uwagi |
|---|---:|---:|---|
| 698 | 206 839 330 | - | |
| 699 | 2 354 328 538 | +2 147 489 208 | "liczniki: podatek wsi" 2 147 541 739 |
| 707 | 206 928 685 | -2 147 402 451 | wyzerowany przy powrocie "bez podatku BK: Tanos", "bez odbiorcy 2 147 538 814" |
| 709 | 2 354 412 757 | +2 147 488 490 | |
| 716 | 207 008 595 | -2 147 416 250 | jak d707 |
| 725 | 4 501 975 670 | +4 294 965 384 | do konca biegu |

Glowy rodow i skarbce w tych dobach bez skoku (69.37 -> 69.23 mln) - nikt tego zlota nie dostal [P]. `MoneyLedger.cs` sumuje `v.TradeTaxAccumulated` (pole gry, int) [P]; jedna wies ma na chwile pole ok. int.MaxValue [S]; podejrzana wies Tanos (ROT_castle31_village1) [S]. Krytyka: w W43 (d287-292, rok 1!) to samo - licznik 2 147 601 915, skok zdjety przy wsi "bez podatku BK: Tumblestone Farms", "bez odbiorcy 2 147 564 210", glowy rodow bez skoku [P]. Wspolne w obu biegach: wies bez podatku BK i zbior "bez odbiorcy" - wiec sciezka, nie jedna wies [S]. Przyczyna - nie wiem. Psuje odczyty etap2 ("pieniadz swiata +76.7 mln/d", "112 nieprzypisane") i kiesa1 P1-5c (91%) - te wiersze czytac po odjeciu skokow.

## 4. Gospodarka od ludzi (zrodlo: `gospodarka\RAPORT.md` rozdz. 1-13)

### 4.1 Zima i ludzie [P]

| kwartal | dob zimy | ciecie plonu wsi swiata | ludnosc mln (koniec) | zmarli z glodu | "brak pieniedzy na jedzenie" (osad sr.) |
|---|---:|---:|---:|---:|---:|
| d274-364 | 0 | - | 51.600 | 0 | 0.2 |
| d365-455 | 0 | - | 51.515 | 0 | 1.5 |
| d456-546 | 87 (od d460) | 40.8% | 51.594 | 0 | 1.9 |
| d638-728 | 91 | 40.8% | 51.618 | 0 | 3.5 |

Start 52.134 mln; rok 1 -1.02% (prog -1.0 +- 0.5 - TAK), rok 2 +0.03% [P]. Zima do d1775; ciecie: Polnoc 56.3%, Reach 40.3%, Wolni Ludzie 62.5%, Volantis 15.9%, Qarth 15.4% [P]. Linia T9 podaje nowa pore w dobie 461, linia E4/0 w 460 [P].

### 4.2 Plon wsi i dochod z ziemi wedlug ludzi [P]

| kwartal | Reach : Polnoc - plon | - ludzie | waga ludzi w dochodzie z ziemi | waga osad | p90/p10 ziemi na 1000 ludzi |
|---|---:|---:|---:|---:|---:|
| d1-91 | 3.64 | 3.53 | 0.63 | 0.37 | 2.9 |
| d274-364 | 3.34 | 3.24 | 0.70 | 0.32 | 3.3 |
| d456-546 | 4.41 | 3.24 | 0.52 | 0.38 | 4.3 |
| d547-637 | 4.64 | 3.24 | 0.40 | 0.54 | 4.4 |
| d638-728 | 4.56 | 3.24 | 0.62 | 0.33 | 4.2 |

W43 bez reguly: waga ludzi 0.15-0.27, osad 0.77-0.86 [P]; GL: 0.52-0.60 [P]. Progi konca roku 1: ludzie >= 0.6 TAK, osady <= 0.4 TAK, p90/p10 <= 3 NIE na styk (3.3) [P]. W zimie plon odchodzi od ludzi, bo Polnoc marznie mocniej ((1-0.403)/(1-0.563) = 1.37 x 3.3 = ok. 4.5) [S]; do tego nowe krolestwa buntownikow (29 -> 35) [P].

### 4.3 Sciana ludu i chleb swiata [P]

| kwartal | stac pelny koszyk, swiat | miast z jedzeniem < 95% | zjedzone bez zaplaty | miasta m_t > 1.5: dni oszczednosci (min) | stac na koszyk |
|---|---:|---:|---:|---|---:|
| d274-364 | 97.1% | 1.1 | 0.14% | 34 (27) | 94% |
| d365-455 | 95.7% | 1.2 | 0.29% | 31 (26) | 94% |
| d547-637 | 92.3% | 3.9 | 0.40% | 25 (23) | 89% |
| d638-728 | 90.9% | 4.2 | 0.51% | 19 (14) | 87% |

P1-6 (okna 64 dob): d301-364 97.1% / d665-728 89.7% przy progu 98% - NIE w obu latach; miast < 95%: 1.2 -> 4.4 (prog 10 TAK); bez zaplaty 0.19% -> 0.56% (prog 2.5 TAK) [P]. Na koniec 7 miast < 0.95: Highgarden 0.33, Aquos Dhaen 0.45, Oldtown 0.52 (ponizej od d183), Volon Therys 0.60, Braavos 0.62 (kiesa W 245 -> 5 tys.), Rhyos 0.68, Selhorys 0.74 [P]. Rok 1 wersji koncowej lepszy niz GL (13 dni, 88%) i W43 (8.2 miast < 95%) [P]. DECYZJA CLAUDE 14.10 03:55: "biedota wielkich miast" zostaje jako cecha, suwak TownFolkLabourVillageBuys [P wedlug STAN-PRAC]; w zimie roku 2 bieda rosnie ok. 3-4 razy [P].

### 4.4 Danina i kiesy ludu [P]

| okno 28 dob | danina tys. zl/d | kiesy ludu miast mln | lokata W mln | krolestw z danina | krolestw w pokoju |
|---|---:|---:|---:|---:|---:|
| d337-364 | 419.1 | 20.80 | 16.18 | 25.8 | 3.0 |
| d477-504 | 282.8 | 22.22 | 17.42 | 15.0 | 14.0 |
| d533-560 | **152.5** | 26.42 | 21.89 | 8.4 | 20.0 |
| d701-728 | 210.2 | 28.87 | 25.01 | 15.7 | 17.1 |

Spadek daniny to pokoj, nie puste kiesy (kiesy rosna od d449) [P]; lokata W 25.08 mln w d728, z tego 16.61 mln w krolestwach w pokoju [P].

### 4.5 Towary, szt./d [P]

| miara | GL rok 1 | rok 1 | rok 2 | d701-728 |
|---|---:|---:|---:|---:|
| len | 872 | 859 | 718 | 647 |
| plotno | 277 | 273 | 239 | 204 |
| sol | 1 065 | 1 054 | 948 | 880 |
| drewno | 2 082 | 2 093 | 2 218 | 2 206 |
| ruda zelaza | 267 | 252 | 233 | 230 |
| zboze | 9 673 | 9 466 | 7 177 | 6 532 |
| miast bez plotna (z 97) | 79.6 | 79.9 | 76.4 | 76.7 |
| miast bez rudy | 15.6 | 14.1 | 5.1 | 5.0 |

Len przed paczka 400-520 szt./d [P]. Plotna na polkach calego swiata 70-207 szt., cena przy suficie [P]; waskie gardlo w tkalniach (len lezy w miastach: 3-7 tys. szt.) [S]. Sol prawie cala kupuja wsie, zapas wsi 3.1-3.2 dnia (cel 14) [P]. Drewno: zapas 12 dni (projekt 30-60) [P].

### 4.6 Qarth, budowy, zbytki, konie [P]

- Qarth: lud stac na jedzenie i koszyk caly bieg (1.00); zapas warowni 31-38 tys., bilans ok. 0; jedzenie na polkach targu sr. 3.5-4.9, min 1.1-3.2 dob (prog 7 - NIE caly bieg); od d638 miasto u buntownikow (Emeros Conspiracy); kiesa ludu 530 -> 156 tys. Dowozu do Qarthu log nie mierzy - nie wiem.
- Budowy: 150 poziomow w roku 1, 208 w roku 2 (W43 131, GL 157); lud doplacil rownowartosc 35.8 i 30.8 poziomu (prog 40-90); osad bez materialu 181-210; lupy: spalone 70 / 38, odbudowane 19 / 35; zgliszcza na koniec 13 osad / 54 poziomy.
- Zbytki: kupione / oferta 28.6% (rok 1), 23.8% (rok 2), 18.7% (d701-728) przy progu 50; plotno to ok. 2/3 niezaspokojonej oferty (d301-364: oferta 95.7, kupione 18.8, bez wiersza na polce 62.2 tys. zl/d).
- Konie robocze: 9 737 kupionych (13.4/d przy planie 13.6), stado 74 564 -> 73 863 (x0.991), skokow 0; osad z "za mala kiesa" 0 -> 2.84.

## 5. Paczki nocy (zrodlo: `paczki\RAPORT.md` rozdz. 1, 3-6)

### 5.1 Progi grup, okno 728 dob [P]

| grupa | progow | NIE | najwazniejsze |
|---|---:|---:|---|
| n7 | 9 | 0 | podzial 55.4 / 26.7 / 17.8% (regula 55.4 / 26.3 / 18.3); odebrani 96.9%; potkniecia 0 |
| w6b | 5 | 0 | gracz bez lenna 727/727 dob - sprawdzalny tylko W6b-3 |
| m6a | 12 | 0 | krotka sluzba 0, najem u wroga 0, okup powtorny 71% ceny |
| w7 | 11 | 0 | pobrano 9.79 mln zl (lud 6.89 / wsie 2.90), zamkniecie 0 rozjazdow |
| lup | 27 | 0 | 39 zdobyc, 1.92 mln zl, budowle zgodne co do poziomu |
| lup5b | 10 | 0 | 3 upadki stolic, 420 568 zl; karawan w murach 0 |
| w5 | 18 | 1 | W5-4 (43.4 -> 40.5%) |
| w6 | 10 | 0 | tarczowe 1.17 mln zl w 2 lata |
| **razem paczki nocy** | **102** | **1** | krytyka: 102 to wiersze z INFO - progow z ocena TAK/NIE 67 (66 TAK, 1 NIE), INFO 35 [P] |
| etap2 / etap4 / etap8 | 55 / 288 / 292 | 7 / 9 / 17 (+1 DECYZJA) | szczegoly w rozdz. 7 |
| gospodarka: gl1, gl2, glb, kiesa1, zbytki, p2bud, k13a, m6b | 140 | 26 (+2 DECYZJA) | rok 1 tez 26 (+1); szczegoly w rozdz. 7 |

### 5.2 N7, lup, M6a, W5-W7 [P]

| N7 | rok 1 | rok 2 | 2 lata |
|---|---:|---:|---:|
| rozbici z druzyn lordow | 225 768 | 133 032 | 358 800 |
| odebrani przez druzyny rodu | 120 719 | 70 623 | 191 342 |
| rozeszli sie po czasie czekania | 1 559 | 4 529 | 6 088 |
| odebrani / (odebrani + rozeszli) | 98.7% | 94.0% | 96.9% (prog >= 50%) |

Pula wyrzutkow d364: 7 483 (W43 bez N7, inna kampania: 11 674); rok 2 sr. 6 774, koniec 4 511. Bandy rosna we wszystkich biegach (d364: 19-22 tys. ludzi) - nie od N7.

| lup | rok 1 | rok 2 | 2 lata |
|---|---:|---:|---:|
| zdobycia szturmem | 27 (9 miast) | 12 (3 miasta) | 39 |
| laska / grabiez / spustoszenie | 13 / 10 / 4 | 5 / 4 / 3 | 18 / 14 / 7 |
| lup z monety (zl) | 1 517 938 | 403 296 | 1 921 234 |
| zabici przy nastepstwach | 15 794 | 9 018 | 24 812 |

Podzial lupu: zolnierze 1.28 mln, wodzowie 0.64 mln, korona ok. 0.21 mln; splonelo 108 poziomow w 21 lupach, odbudowano 54; glod w lupionych 0 z 1 193 pomiarow; karawany wroga 0 w 39 zdobyciach.

| M6a | rok 1 | rok 2 |
|---|---:|---:|
| najmy / krotka sluzba / najem u wroga | 150 / 0 / 0 | 195 / 0 / 0 |
| okupy AI-AI / w tym powtorne | 1 623 / 613 | 1 034 / 285 |
| osoby wykupione 5+ razy | 95 (GL z paczka 54, bez 53) | 21 |
| jency-bohaterowie nie wyszli za darmo | 585 | 246 |

| W5 / W6 / W7 | d337-364 | d701-728 |
|---|---:|---:|
| danina / wyplacono W5 (tys. zl/d) | 417.6 / 40.5 (49 rodow) | 210.6 / 28.3 (34 rody) |
| W6 tarczowe | 1.2 | 0.4 |
| W7: pobrano; rodow; osad; pokrycie | 18.1; 23; 78; 39% | 14.7; 24; 77; 28% |

W7 rok 2 najmocniej: Zelazne Wyspy 6.5 tys. zl/d (lojalnosc -26 / tydzien), Wolni Ludzie 2.4 tys. (pokrycie 13%).

## 6. Rok 2 w innych progach (kontekst) [P]

Zalogi d728 58.4 tys. (d364 89.5); Straz 3 640 (baza 8 691); ludzie w partiach rodow AI 57.0 tys. (prog 70); rody z lennem bez ludzi 41 z 186 (prog 12); glowy < 5000: 18 (prog 10); zalegly zold 3.03% (prog 2%); skory 312/d (prog 430); najemnicy AI 1 612 (prog 2 000). Wszystkie kontrole W5 / W6 / W7 / N7 / M6a w roku 2 sa czyste - nie wskazuja na blad paczek [P]/[S].

## 7. Lista NIE z ocena (okno 728 dob; zrodla: `paczki` rozdz. 8, `gospodarka` rozdz. 1, `wojsko` rozdz. 6)

Ocena: **BLAD** / **ROK2** (skutek pokojow i zimy) / **PROG** (prog do zmiany) / **CECHA** / **ZNANE** (bez zmian od biegow rocznych) / **NOWE** (drobne, do obejrzenia) / **CZAS** (optymalizacja na koniec) / **DECYZJA**.

| # | grupa | wiersz | wartosc [P] | prog | ocena | uwagi |
|---|---|---|---|---|---|---|
| 1 | etap2 / kiesa1 | 112 nieprzypisane / P1-5c Pieniadz swiata (DECYZJA) | 4 294 966 847 / 91% | 0 / - | **BLAD** stary | licznik podatku wsi > 2^31 (rozdz. 3); W43 tez; do naprawy - pkt 1 kolejnosci |
| 2 | etap2 | bankruci K39 | 1 (d642) | 0 | BLAD drobny, stary | GL 1 (d226) |
| 3 | etap4 | E4/7 T3, E4/10 V10, E4/9 K19 - rownanie krain | valyrian -0.216% (-550 ludzi od d130) | <= 0.1% / 200 | BLAD ksiegi drobny, stary | GL aserai -0.188% |
| 4 | etap4 | E4/9 K20 DLUG 4 do lasu | 47 / 94 | <= 5% | BLAD reguly, stary | GL 73 / 136 |
| 5 | etap4 | E4/11 G5 trafieni Devastation | 1 doba (d286) | 0 | BLAD drobny | |
| 6 | etap8 | C-4b kasy miast ponizej zapasu przez nasze sciezki | 9 | 0 | BLAD drobny, stary | W43 3, GL 6 |
| 7 | etap8 | H-4b reszta uprzezy | -81% | <= 10% | BLAD ksiegi, stary | Z33 -82% |
| 8 | gl2 | GL2-1 potkniecia (d1) i GL2-S1 zloto startowe wsi | 2 / -4 000 zl | 0 | BLAD drobny - **naprawiony w c3aa637** | test 40 dob fin22: zgodne [P wedlug STAN-PRAC] |
| 9 | w5 / etap2 | W5-7 / wojsko w druzynach lordow | 55.3 / 55.55 tys. | 85 tys. | **ROK2** - glowny alarm | pulapka pokoju, rozdz. 2 |
| 10 | etap2 | glowy < 5000 d728 | 18 | <= 10 | ROK2 | d364: 11 |
| 11 | etap2 | zalegly zold w partiach | 3.03% | <= 2% | ROK2 | GL 2.03% |
| 12 | etap4 | E4/8 N4 zalogi d728 | 58 366 | >= 69 020 | ROK2 + PROG | prog wiaze w d120 |
| 13 | etap4 | E4/9 K7 Straz d728 | 3 640 | >= 7 387 | ROK2 | bunt "Confederation of Snow" zabral rody [P]; reszta - nie wiem |
| 14 | etap8 | P3W-5 ludzie w partiach rodow AI | 56 969 | >= 70 000 | ROK2 | |
| 15 | etap8 | P3W1-6 rody z lennem z 0 ludzi | 41 z 186 | <= 12 | ROK2 | |
| 16 | etap8 | P3W1-1 rody bez warunku renty | 10.8% | <= 10% | ROK2 / PROG | na granicy |
| 17 | etap8 | N5-2 skora | 312/d, 79 miast bez skory | >= 430/d | ROK2 (zima) | przyczyna - nie wiem |
| 18 | etap8 | P3W3-3 ludzie najemnikow AI | 1 612 | >= 2 000 | CECHA M6a + ROK2 | dluzsza sluzba = mniej najmow |
| 19 | gl2 | GL2-2 korelacja wydatkow ludu z ludzmi rynku | 0.78 -> 0.64 | 0.8 | ROK2 [S] | |
| 20 | gl2 | GL2-9 Braavos bez zaworu kas | 220 dob z rzedu (rok 1: 33) | 14 | ROK2 - do obejrzenia | bieda Braavos |
| 21 | glb | GLB-P5-1 len na x1.0 regulatora | 628 (rok 1 709 TAK) | 680 | ROK2 (zima) | |
| 22 | kiesa1 | P1-1 lokata W | 25.08 mln | 6 | ROK2 + PROG | 16.61 mln w krolestwach w pokoju |
| 23 | kiesa1 / gl2 | P1-6 chleb swiata; GL2-8 dni oszczednosci miast m_t > 1.5 | 97.1% -> 89.7%; min 14 dni | 98%; 60 dni | SLABE - do ponownej decyzji (krytyka: "cecha" z DECYZJI CLAUDE 14.10 03:55 byla na roku 1; w roku 2 spada co kwartal, dna nie widac) + ROK2 | pkt 5 kolejnosci |
| 24 | k13a | KR-8 stos koni bojowych | do 3 397 dni zuzycia (d561) | <= 60 dni, nie rosnie | ROK2 [S] | w pokoju konie sie nie zuzywaja [S] |
| 25 | etap2 | glowy < 5000 d364 | 11 | <= 10 | PROG / ZNANE | GL 15 |
| 26 | etap2 | 110 pan samych zamkow z zaworu | 405 zl/d | 200-350 | PROG | W43 415, GL 429 |
| 27 | etap4 | E4/3 pula wyrzutkow d728 | 4 429 | <= 3 053 | PROG | baza sprzed N7 |
| 28 | etap4 | E4/3 bandy d728 | 1 097 / 21 618 | <= 867 / 15 044 | CECHA / PROG - do decyzji | rosna w kazdym biegu |
| 29 | etap8 | G1-5a, G1-5c, O5-2 (wobec Z33) | +12.5%; +99%; x0.91 / x0.75 | wzory d120 / Z33 | PROG | |
| 30 | etap8 | P3W2-4 zold na czlowieka pulap / luz | 1.32x | <= 1.25x | ZNANE / PROG | W43 1.26 |
| 31 | w5 | W5-4 wypelnienie rodow z doplata | 43.4% -> 40.5% | rosnie | PROG / CECHA | w kazdym biegu |
| 32 | gl1 | GL1-1 winogrona | 0.949 | 0.95 | PROG (drobne) | |
| 33 | zbytki | ZB-7 filc w jukach | 7 347 | 5 000 | PROG [S] | |
| 34 | p2bud | P2-2 roboty ludu; P2-3 poziomy ludu | 6.5 tys. zl/d; 35 / rok | 8 tys.; 40-90 | PROG / brak materialu [S] | 181-210 osad bez materialu |
| 35 | gl2 | GL2-6 Qarth jedzenie na polkach | min 1.1-3.2 dob | 7 | CECHA (Jeff: "Qarth zostawic i mierzyc") | dowozu - nie wiem |
| 36 | glb | GLB-P5-2 miast bez plotna; GLB-P5-3 regulator lnu na suficie | 76.9; x1.30 | 60; - | ZNANE - do roboty (tkalnie) | pkt 5 kolejnosci |
| 37 | zbytki | ZB-2 kupione / oferta; ZB-6 miast z kasa ponizej zapasu | 25.2%; 10 | 50%; 4 | ZNANE | plotno 2/3 braku |
| 38 | kiesa1 | P1-8 dlug koron w Banku | 0.93 wobec 0.52 mln | nie rosnie | ZNANE | GL tez NIE |
| 39 | etap8 | H-5b, N5-3, N5-5, P3W1-3 | x1.35; 26.4 szt./d; 96.6 / 124.4; 24 042 zl/d | | ZNANE | bez zmian od biegow rocznych |
| 40 | m6b | M6b-4b juczne; M6b-5a ryby bez wyjasnienia | -32.3/d; +72.2/d (rok 2 ok. +100) | 20 | ZNANE (czesciowo naprawione w m6b) | |
| 41 | glb | GLB-P4-2 p90/p10 rekrutow | 100.7 | | INFO (LevyByPeople wylaczony) | |
| 42 | zbytki / p2bud / kiesa1 | ZB-11, ZB-13, P2-7, P1-19 czas | 3.5; 5.6; 1.94; 0.133 ms/d | 1; 2; 1; 0.1 | CZAS | kiesa ludu 13.5 ms (projekt 11.9) |
| 43 | etap8 | P3-2 zatkane wsie bez wozu | ciag 6 dob | <= 2 | **NOWE** drobne | w biegach rocznych TAK; przyczyna - nie wiem |
| 44 | etap8 | Q-3 partia zawracana > 3 razy / dobe | 7 partio-dob (d448) | 0 | **NOWE** drobne | przyczyna - nie wiem |
| 45 | etap8 | C-1e barda t6 | mediana 2 760 | <= 2 600 | DECYZJA (stara) | |
| 46 | kiesa1 | P1-3 kiesy ludu | 20.4 -> 29.0 mln | | DECYZJA | |

Poza progami narzedzia: L5b-2 karawany wroga 0 w 39 zdobyciach - CECHA albo martwy kod, nie wiem; LUP-11 "stac na jedzenie" 1.00 w 1 193 pomiarach - nie wiem, czy miara jest czula (w swiecie zima dala 721 osado-dob braku pieniedzy na jedzenie); petla wykupu 95 osob 5+ razy w roku 1 - rozrzut kampanii czy tanszy okup, nie wiem; 10 progow alarmowych - 6 TAK (rozdz. 2.6).
Narzedzie: `--grupa kiesa1` przy `--do-doby 728` liczy P1-2/4/6/7/8 na sztywnych oknach roku 1 [P]; GLB-P4-3 daje 180.8/d w obu oknach - nie sprawdzone, czy to blad odczytu.
Podsumowanie: zaden BLAD nie wskazuje na paczki nocy (wszystkie stare albo drobne, jeden naprawiony) [P]/[S]; najwazniejsze sa: wojsko w roku 2 (ROK2) i licznik podatku wsi (BLAD).

## 8. Czego nie wiem

- Co w ROT konczy wymuszanie wojen Essos (oraz Dorne-Reach, Wyspy-Dolina) miedzy d422 a d449 i dlaczego krolestwa w pokoju nie wracaja do wojny (log nie podaje powodu decyzji AI); czy w kazdej kampanii w tej samej dobie - jeden bieg dwuletni, biegi roczne nie dochodza do doby 450 (to, ze pokoje przyszly po koncu wymuszania - [P], rozdz. 2.3).
- Ile z rozpadu wojska krolestw w pokoju robi nasza regula budzetu (pulap w pokoju 0.28 D, zalogi pierwsze), a ile AI gry - nie liczone.
- Skutek N7 w mierze (trzeba biegu z N7 wylaczonym).
- Przyczyna skoku licznika podatku wsi o 2^31 i czy gra moglaby go wyplacic panu.
- Czy krolestwa w pokoju z zalogami 0.1-0.9 tys. straca twierdze przy pierwszym ataku.
- Czy L5b-2 (karawany wroga) moze sie odpalic przy zdobyciu przez AI; czy miara LUP-11 jest czula.
- Wczytanie zapisu z polem N7 ponad 32 767 znakow.
- Dowoz zywnosci do Qarthu i wywoz z Reach (brak miary w logu); ceny zywnosci w zimie (nie wyciagane).
- Czy spadek "wagi ludzi" w d546-637 to tylko zima, czy tez bunty.
- Log RealisticCaptivity / GrandTourney tego biegu - nie bylo go w kopii na D:.

## 9. Do recznego testu Jeffa

Autotest gra postacia bez lenna i bez krolestwa i sam nie zdobywa miast, wiec zadna sciezka gracza z tej nocy nie przeszla testu w grze [P] (audyt integracji, `docs/test-zbiorczy/audyt-integracji-2026-10-14/RAPORT-gracz.md` pkt 2 i 9). Zrodla punktow: wpisy CHANGELOG nocy na galezi `noc/wgranie-50` (c3aa637) - W6b, W7, N7, L3, L5, L5b, P1b, P2, GL-1 / GL-5, poprawki po audycie.

Przygotowanie: **nowa kampania** (zloto startowe wedlug ludzi i 16 nowych wsi lnianych dzialaja tylko w nowej kampanii). Twoj `Armoury.json` MCM (13.10 02:10) nie ma zadnego klucza z tej nocy, wiec gra wezmie wartosci domyslne z kodu (m.in. LevyByPeople = off) [P]. W MCM zobacz nowe grupy "The sack of towns and castles" i "Defeats and payers (D11)".

1. **W6b - tarczowe i doplata dla Twojego rodu** (wasal z zamkiem albo miastem, krolestwo w wojnie, "Scutage Player Clan" wlaczone):
   a. mala druzyna i zloto ponad rezerwe (np. kilkaset tysiecy): w logu Armoury linia "Tarczowe (W6)" ma `gracz: ... warunki tak ... nalezne gracza T > 0` i albo `zaplacil X` (wtedy wyjasnienie "Scutage: in war a vassal..." i po 7 dobach "Scutage: you paid X denars to the crown of ... this week"), albo "korona dzis nie brala" (normalne i czeste); kiesa nigdy ponizej rezerwy;
   b. duza druzyna (ponad to, co udzwigna ziemie): wyjasnienie "War aid: ..." i tygodniowo "War aid: the crown of ... paid you X denars"; w linii W5 `gracz: ... potrzeba W5 N, dostal X` - nigdy tego samego dnia "zaplacil" > 0;
   c. pokoj, utrata ostatniego zamku, korona albo sluzba najemna: od nastepnego dnia "warunki nie spelnione"; potem `python -I tools/sprawdz_logi.py --grupa w6b <Armoury-*.log>` - same TAK albo INFO;
   d. wyjasnienie wraca najwyzej raz na 30 dob, nie po kazdym wczytaniu (poprawka c3aa637).
2. **W7 dla gracza (opcjonalnie):** klucz PlayerWarLevy jest domyslnie WYLACZONY ("u gracza to jego decyzja"). Jesli go wlaczysz (razem z "Scutage Player Clan"): jako wasal w wojnie z brakiem po W5 - komunikat przy pierwszym poborze i suma tygodnia, spadek lojalnosci i dobrobytu Twoich osad, bez poboru przy lojalnosci < 40. Bez klucza linia W7 liczy Cie jako "gracz bez klucza".
3. **Lup - okno gracza (L5):** zdobadz szturmem zamek i miasto. Podpowiedzi Devastate / Pillage / Show Mercy z kwotami ("The sack: a third (two thirds) of all coin in X", "Merchants' coin", przy zamku "Castle treasury" / "Castle folk's coin", "Your party (n of m men): ..."); porownaj z liniami `Lup miast (L5)` i `Lup miast (L2)` w logu (napisy = kwoty); podsumowanie "Your army took T coins from X ..."; "Prosperity Lost" liczone od tej osady (D9), kwoty w "denars" z separatorem; w oknie "People of this land" przez 30 dob "Sacked N days ago ...".
4. **Lup - 3 doby bez wydatkow (L3):** po grabiezy albo spustoszeniu Twoi ludzie przez 3 doby nic nie kupuja w tej osadzie: "Your men are still gorged on the sack of X" (gdy brales udzial) albo "Your realm's army sacked X only days ago ..." (gdy nie); w linii L3 licznik "zakupy ludzi gracza w postoju wstrzymane N dob".
5. **Towar z polek (L7) i karawany wroga (L5b-2):** szturm miasta, w ktorym stoi karawana wroga: predkosc druzyny po wyjsciu normalna (ladunek do udzwigu, nadmiar na polce osady - komunikat mowi, ile zostalo na targu); w logu linie "Karawany w zdobytym miescie (L5b-2)" bez potkniec, kontrola L5B-5 0/0/0, L7 "bilans sztuk = 0". Odwrotnie: wrog zdobywa miasto z Twoja karawana - tracisz ja i dostajesz komunikat.
6. **Skarbiec stolicy (L5b-1):** jako glowa rodu bez krolestwa zdobadz stolice - 1/3 skarbca ponad zapas wojny idzie do Twojej kiesy (ta galaz nie byla w zadnym biegu); w krolestwie - komunikat przy stracie albo zdobyciu stolicy.
7. **Rozbici wracaja (N7):** przegraj bitwe w polu - "Stragglers: N of your men fled toward {TOWN}", potem "have gathered at {TOWN}", a gdy Twoja druzyna jest w zasiegu 5 dob - "rejoined you from {TOWN}" (odbior bez pytania, w limicie druzyny i zoldu); po 30 dobach bez odbioru "gave up waiting - H went home, W took to the woods"; w menu krainy wiersz "Stragglers waiting to rejoin their lords". Znane drobne (audyt pkt 4, niepoprawione): "has fallen to the enemy" moze wyjsc tez przy zmianie strony Twojego rodu.
8. **Twoje lenno - danina i budowy (P1b, P2):** w dymku lojalnosci pozycja "War aid from the people's savings"; przy lojalnosci <= 30 w oknie mieszkancow "The tax gatherers have left: the town is too close to revolt to pay the war aid."; w mocy budowy pozycja "Town works (paid by the townsfolk)".
9. **Dymki:** wies - "People of this land: x.." pod "Primary Production"; wies lniana - "Flax growers and linen weavers.".
10. **Zapis:** po kilkudziesieciu dobach z czekajacymi rozbitymi zapisz i wczytaj - liczba czekajacych w linii N7 ta sama, "zgubieni przy wczytaniu 0".

## 10. Pliki i polecenia

- Raporty: `SCRATCH\wojsko\RAPORT.md`, `SCRATCH\gospodarka\RAPORT.md`, `SCRATCH\paczki\RAPORT.md`.
- Wyniki narzedzia progow: `SCRATCH\wojsko\sl_{w5,w6,w7,n7,lup,lup5b}.out.txt`; `SCRATCH\gospodarka\{gl1,gl2,glb,kiesa1,zbytki,p2bud,k13a,m6b}-{364,728}.txt`; `SCRATCH\paczki\grupa-*.txt` i `por\` (okno roku 1 trzech biegow).
- Skrypty analiz: `SCRATCH\wojsko\a1-a8*.py` i `kryt\` (skrypty krytyki rachunku na biegu dwuletnim); `SCRATCH\gospodarka\skrypty\` (wyciag_log, analiza, regresja_ziemia, plon_kwartaly, qarth); `SCRATCH\paczki\analiza.py`, `analiza2.py`, `cs_sesje.py`.
- Narzedzie progow: `python -I tools/sprawdz_logi.py --grupa <g> --do-doby 728 D:\Backup-Bannerlord\logs\2026-10-14-noc\Armoury-2026-10-13_21-14-13.log`.
- Kontekst: `docs/STAN-PRAC.md` (wpisy od "NOWE KONTO (sesja c36b73ca"), `docs/gospodarka-od-ludzi/sucho85/WYNIK-RACHUNEK-85-TYS.md` rozdz. 7, `docs/test-zbiorczy/audyt-integracji-2026-10-14/`.
- Krytyka: `SCRATCH\synteza\kryt_log.py` (`python -I kryt_log.py <Armoury-2026-10-13_21-14-13.log>` -> `kryt_log.out.txt`), `kryt_rody.py` (`python -I kryt_rody.py <Logs\2026-10-13_21-14-13\budzet-rodow.csv>` -> `kryt_rody.out.txt`), `kryt_t9_pokoje.txt` (pokoje z linii CS `Reparacje (T9): dzien N - pokoj`: dzien | para | fabularna / niefabularna).

## 11. Krytyka (14.10, po syntezie)

Niezalezne skrypty krytyka (rozdz. 10) na logu Armoury, `budzet-rodow.csv` i sesji CS tego biegu, plus grep na W43 i na `Armoury.json` Jeffa. Kod, repo i gra bez zmian; poza tym plikiem nic w repo nie ruszone.

### 11.1 Co sprawdzone wlasnym skryptem [P]

| liczba w syntezie | pomiar krytyka | zgodne? |
|---|---|---|
| W5-7 d701-728 55.3 tys., 17.3 krolestw w wojnie | 55.32 tys., 17.3; wszystkie krolestwa 57.43 | tak |
| W5-7 d337-364 108.9; Q5 110.4; d533-560 43.9; dno 36.1 tys. w d537 | 108.89; 110.35; 43.93; 36 128 w d537 | tak |
| fala: 16 krolestw w pokoju w d451-513, w wojnie 10-17 | 16 (w wojnie caly d421-448, pokoj w d449-520); okna 10.1-17.3, dobowo 10-21 | tak |
| licznik podatku wsi: d699 +2 147 489 208, d725 4.50 mld, glowy rodow bez skoku | d699 razem 2 354 328 538 (licznik 2 147 541 739); d725 4 501 975 670; glowy 69.38 / 69.37 / 69.50 mln; razem bez licznika 206.8-207.0 mln | tak |
| N7: odebrani 191 342, rozeszli po czasie 6 088, 96.9% | 191 342 / 6 088 / 96.9% w d2-728 (z linia d729: 191 691 / 6 092) | tak |
| lup: 39 zdobyc (27 / 12; 12 miast), 1 921 234 zl | 39 (27 / 12), 12 miast, 1 921 234 zl (rok 1 1 517 938) | tak |
| 0 ERROR, 728 dob, 12.43 / 12.27 s/dobe | Armoury 0 ERROR, CS "ERROR IN A MOD" 0, bloki "CAUGHT FURTHER UP" 25; linie Skarbce doby 2-729; 12.43 / 12.26 s | tak |
| z wojny do pokoju 132 rody, 38 125 -> 1 486 | 134 rody, 38 694 -> 1 487 (granica 28 z 28 dob) | tak |
| w wojnie w obu oknach 176 rodow, -10.4% | 175 rodow, 55 041 -> 50 961, -7.4% (granica 28 z 28; a8 liczy >= 22 z 28) | czesciowo - poprawione na 7-10% |
| pieniadz swiata -0.8% w 2 lata | 209.18 -> 207.04 mln = -1.0%; -0.8% to tylko rok 2 | NIE - poprawione |
| Armoury.json Jeffa bez kluczy nocy | brak StragglersRally, ScutagePlayerClan, LordWarLevy, LevyByPeople, SackTakesRealCoin, CapitalTreasurySeized, PeaceWageShare i in.; jest WeeksPerSeason 13 | tak |
| 102 progi paczek nocy, 1 NIE | 102 wiersze = 67 progow z ocena (66 TAK, 1 NIE) + 35 INFO | liczba tak, slowo "progi" przesadzone - poprawione |
| W43: licznik podatku wsi w d287-292 | razem 2 355 300 665 w d287, "bez podatku BK: Tumblestone Farms", "bez odbiorcy 2 147 564 210", glowy rodow bez skoku | tak, i to rok 1 |

### 11.2 Co poprawione w tym pliku

1. **Przyczyna fali pokojow** (pkt 0.4, rozdz. 2.3, 8). Bylo: "dlaczego naraz - nie wiem" i "sprzecznosc zrodel" ze STAN-PRAC 06:49. Pomiar z sesji CS (`kryt_t9_pokoje.txt`): 10 par z fali mialo wczesniej pokoje FABULARNE, ktore ROT wznawial (Lorath-Braavos 11 razy, Norvos-Qohor 10, Dothrakowie-Targaryen 7, Dorne-Reach 6+3, Ibben-Sarnor 6+1, Myr-Pentos 5; ostatni d248-422), a pokoj w fali (CS d449-511) jest juz NIEFABULARNY i nikt go nie wznawia. Po d422 ROT wymusza tylko wojny Westeros. Czyli: ROT przestal wymuszac te wojny miedzy d422 a d449 [P], STAN-PRAC mial racje co do kierunku, sprzecznosci nie ma. Co w ROT to konczy - nie wiem (w kolejce fabuly miedzy d129 a d520 nic nie startuje).
2. **Nasza regula w "pulapce pokoju"** (pkt 0.4, 0.9.3, rozdz. 8). Synteza opisywala rozpad wojska krolestw w pokoju jak zachowanie AI. W kodzie wersji koncowej (`ClanBudget.cs`, c3aa637) pulap zoldu w pokoju to 0.28 D (w wojnie 0.60 D + nadwyzka kiesy), w pokoju zalogi placone pierwsze, partie dostaja reszte, zwolnienia 15% nadwyzki dziennie; danina P1 i doplaty W5 / W5b / W6 / W7 ida tylko w wojnie [P wedlug kodu]. Ile z -96% ludzi rodow w pokoju to ta regula, a ile AI - nie liczone; ludzie spadli bardziej niz pulap, wiec dziala tez cos innego [S]. To dzwignia w NASZYM kodzie (suwak `PeaceWageShare`; w Armoury.json Jeffa go nie ma - domyslne 0.28).
3. **"Mapa, nie pieniadz"** -> "glownie mapa, pieniadz ok. 1/10": alarm 5 (saldo rodow -60 tys. zl/d) to pieniadz jeszcze przy pelnej mapie; spadek tych samych rodow w wojnie to 7.4-10.4% wedlug granicy grupy; ze to pieniadz, a nie straty w bitwach - [S] (ich kiesy rosna, spada dochod). Szacunek "przy mapie z roku 1" 95-98 -> 95-101 tys. [S].
4. **Pieniadz swiata**: "-0.8% w 2 lata" -> -1.0% (rok 1 -0.2%, rok 2 -0.8%). "Szczelny" zostaje: zrodla bez platnika 0, reszta [R] -0.55 tys. zl/d.
5. **Licznik podatku wsi**: "kampania dojdzie tam ok. doby 700" bylo mylace - w W43 ten sam skok byl w d287-292 (rok 1). W obu biegach wies "bez podatku BK" i zbior "bez odbiorcy" - to sciezka, nie jedna wies Tanos [S]. Dopisane do pkt 0.1 i 0.9.1 oraz rozdz. 3.
6. **"Gotowe do gry"** (pkt 0.1): dopisane, ze zadna sciezka gracza z tej nocy nie przeszla testu w grze (autotest gra bez lenna i krolestwa) - "tak" dotyczy swiata AI i stabilnosci, nie Twoich sciezek.
7. **"Biedota wielkich miast" to nie CECHA** (pkt 0.8c, wiersz 23 listy NIE): decyzja z 03:55 byla na danych roku 1 (bieg GL); w roku 2 chleb swiata spada w kazdym kwartale (97.1 -> 95.7 -> 95.3 -> 92.3 -> 90.9%), 7 miast < 0.95 (Highgarden 0.33), dni oszczednosci 34 -> 19 - trend bez dna. Zmienione na "SLABE - do ponownej decyzji".
8. **Progi paczek**: 102 to wiersze razem z INFO; progow z ocena 67, 1 NIE; W6b w autotescie prawie niesprawdzalny (3 wiersze z ocena, gracz bez lenna).
9. **N7 prostym jezykiem**: 97% to odsetek odebranych sposrod czekajacych; z 359 tys. rozbitych do druzyn wrocilo 191 tys. (53%).

### 11.3 Czy wniosek stoi

- **Stoi.** Bieg 728/728, 0 ERROR, czas doby stoi [P]. Rok 1 108.9 tys. ponad progiem, rok 2 55.3 tys. - prog 85 tys. NIE [P]. Wniosek "nowa kampania - tak, gra dluzej niz ok. 450 dob - jeszcze nie" po krytyce jest mocniejszy: pokoje przyszly po koncu wymuszania wojen przez ROT (kod ROT, nie los tej kampanii) [P], wiec w kampanii Jeffa najpewniej w tej samej porze [S]; a rozpad wojska w pokoju to w duzej czesci nasza regula budzetu [P wedlug kodu]/[S] - czyli jest co naprawic u nas.
- "Gotowe do gry" jest uczciwe tylko z zastrzezeniami, ktore teraz sa w pkt 0.1: sciezki gracza nietestowane, licznik podatku wsi moze wyjsc w roku 1 (zlota nikt dotad nie dostal - "bez odbiorcy" w obu biegach), WGRANIE 50 ma za soba 40 dob + wczytanie, nie dwa lata.
- 10 progow alarmowych (6 TAK / 3 NIE / 1 nie wiem) sprawdzone z definicjami z rozdz. 7.6 rachunku - zgodne. Gospodarka od ludzi, paczki nocy, lista NIE - bez zmian poza wierszem 23. Kolejnosc dalej - bez zmian, punkt 3 doprecyzowany.

### 11.4 Czego krytyk nie sprawdzil

- 13 zapisow, czasy zapisu, pole N7 34 057 znakow, plon / regresja ziemi / chleb / towary / Qarth (rozdz. 4) - wziete z raportow, bez przeliczenia.
- Logi RealisticCaptivity / GrandTourney (brak w kopii) - "0 bledow naszych modow" opiera sie na Armoury.log i CS ("ERROR IN A MOD" 0).
- Warunek konca wymuszania w ROT i udzial reguly `PeaceWageShare` w rozpadzie wojska w pokoju - wymaga dekompilacji ROT i osobnego rachunku.
