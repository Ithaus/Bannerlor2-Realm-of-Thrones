# Bieg dwuletni (728 dob) wersji koncowej - NOWE PACZKI NOCY I STABILNOSC

Bieg: `w-toku/zbior-e8` 43af413 = WGRANIE 49 + lup 5b, Armoury 6dd1e9e5 = test\fin21 (wedlug STAN-PRAC; poprawek po audycie c3aa637 / fin22 w tym biegu NIE ma).
Log `D:\Backup-Bannerlord\logs\2026-10-14-noc\Armoury-2026-10-13_21-14-13.log` (279 656 linii, 1 sesja, doby kampanii 1-728), tabele `Logs\2026-10-13_21-14-13\`, sesja CS `session-2026-10-13_21-14-11.log`.
Oznaczenia: [P] pomiar z logu / tabel, [S] szacunek lub wniosek. Wszystkie liczby w tabelach sa [P], chyba ze przy liczbie stoi [S].

## 0. Dla Jeffa - prosto (10 linii)

1. Dwa lata gry przeszly do konca bez wywrotki: 728/728 dob, 0 bledow naszych modow, gra nie zwalnia (rok 1: 12.4 s na dobe, rok 2: 12.3 s), zapis gry trwa 5 s [P].
2. Nowe paczki nocy robia to, co maja: w ich 102 progach jest 1 NIE (stary W5-4, byl w kazdym biegu rocznym), pieniadze i ludzie zamykaja sie co do sztuki [P].
3. Rozbici wracaja: w 2 lata 199 tys. rozbitych poszlo pod choragiew, druzyny odebraly 96% z nich (191 tys.); tlum wloczegow na koniec roku 1 to 7.5 tys., w biegu rocznym bez tej paczki (inna kampania) 11.7 tys. [P].
4. Lup miast: 39 zdobyc (27 w roku 1, 12 w roku 2), 21 z lupem, razem 1.9 mln zl; splonelo 108 poziomow budowli, polowa juz odbudowana; pomiar glodu w zlupionych miastach nie pokazal ani jednego przypadku (ale ta miara ani razu nie drgnela) [P].
5. Najemnicy nie skacza juz miedzy panami (0 krotkich sluzb, 0 przejsc do wroga), okup powtorny jest tanszy, pokoj "na jeden dzien" nie wypuszcza juz jencow za darmo [P].
6. ALE w roku 2 przyszla zima (od doby 461) i wojsko lordow w wojnie spadlo ze 110 tys. do 55 tys.; wojen jest mniej (21 par -> 7-15), zalogi 90 -> 58 tys. Prog 85 tys. na koniec roku 2: NIE [P]. Rok 1 byl dobry: 109 tys. [P].
7. To nie jest blad nowych paczek (wszystkie ich kontrole sa czyste), ale dokladnej przyczyny spadku nie znam - do rozebrania osobno (zima, pokoje, budzety) [S].
8. Znalazlem stary blad licznika: podatek jednej wsi "przeskakuje" o 2.1 mld zl (przepelnienie liczby). Nikt tych pieniedzy nie dostal, ale psuje pomiar "pieniadz swiata"; byl juz w biegu rocznym sprzed nocy [P].
9. Skarbiec stolicy zadzialal 3 razy (0.42 mln zl, wszystko w roku 1); zabor karawan wroga w zdobytym miescie nie zadzialal ani razu w 39 zdobyciach - nie wiem, czy karawan tam po prostu nie ma, czy kod sie nie odpala [P]/[S].
10. Sciezek gracza (tarczowe gracza z lennem, okno lupu gracza) autotest nie sprawdza - zostaja do Twojego recznego testu [P].

## 1. Progi grup (narzedzie `sprawdz_logi.py --do-doby 728`; wyniki w `grupa-*.txt`) [P]

| grupa | progow | NIE | DECYZJA | najwazniejsze liczby (okno 728 dob) |
|---|---:|---:|---:|---|
| n7 | 9 | 0 | 0 | podzial 55.4 / 26.7 / 17.8% (regula 55.4 / 26.3 / 18.3); odebrani 96.9%; potkniecia 0 |
| w6b | 5 | 0 | 0 | gracz bez lenna i krolestwa 727/727 dob - tylko W6b-3 sprawdzalny (TAK) |
| m6a | 12 | 0 | 0 | krotka sluzba 0, najem u wroga 0; okup powtorny 71% ceny; pokoje odlozone 159 = 111 odwolanych + 42 uwolnione + 6 upadlo |
| w7 | 11 | 0 | 0 | pobrano 9.79 mln zl (lud 6.89 / wsie 2.90); zamkniecie 0 rozjazdow w 727 dobach |
| lup | 27 | 0 | 0 | 39 zdobyc, lup 1.92 mln zl, budowle zgodne co do poziomu |
| lup5b | 10 | 0 | 0 | 3 upadki stolic, 420 568 zl; karawan w murach 0 |
| w5 | 18 | 1 | 0 | W5-4 NIE (43.4 -> 40.5% limitu); W5-7 55.3 tys. (INFO) |
| w6 | 10 | 0 | 0 | tarczowe pobrane 1.17 mln zl w 2 lata; d701-728 0.4 tys. zl/d |
| etap2 | 55 | 7 | 0 | wojsko 55.6 tys., glowy < 5000: 11 (d364) / 18 (d728) |
| etap4 | 288 | 9 | 0 | T1 bledy 0; NIE glownie stare (pula / bandy / rownanie krain) i rok 2 (zalogi, Straz) |
| etap8 | 292 | 17 | 1 | T0 ERROR 0; NIE: 10 starych, 5 roku 2, 2 nowe drobne |
| **razem paczki nocy (n7, w6b, m6a, w7, lup, lup5b, w5, w6)** | **102** | **1** | **0** | |

Te same grupy w oknie roku 1 (`--do-doby 364`, `por\`): etap4 NIE 7 (bieg roczny kodu 43: 3, bieg roczny GL: 6), etap8 NIE 13 (10 / 10), w5 NIE 1 (1 / 1), grupy paczek 0 NIE poza W5-4 [P].

## 2. Stabilnosc

### 2.1 Bledy [P]

| co | ten bieg (728 d) | bieg roczny kodu 43 (15-02) | bieg roczny GL (19-21) |
|---|---:|---:|---:|
| ERROR w logu Armoury | 0 | 0 | 0 |
| potkniecia > 0 w liniach Armoury (linio-doby) | 1 (GL2 "Rynek od ludzi", doba 1: 2 potkniecia; wedlug testu paczki A to zloto startowe kies wsi [S] - znane, straz dodana w c3aa637, ktorej w tym biegu nie ma) | - | - |
| CS: bloki "ERROR IN A MOD" | 0 | 0 | 0 |
| CS: bloki "ERROR CAUGHT FURTHER UP" | 25 (23 przy starcie gry + 2 w biegu) | 23 (start) | 23 (start) |
| CS: wyjatek polkniety przez Mends | 1 (BK ClaimantDemand.SetTexts NRE, d626) | 0 | 0 |
| CS: GAME HANG | 1 (23:46:46, po QuitGame, poza kampania) | 0 | 1 (tez po wyjsciu) |
| AUTOTEST KONIEC | OK 728/728, 149.8 min, 12.3 s/dobe, "bledy: 26" | OK 364/364, 12.5 s/d, 23 | OK 364/364, 12.6 s/d, 23 |

Nowe sygnatury wobec biegow 13.10 (wszystkie w cudzym kodzie, gra zyje):
- 2 x `AmbiguousMatchException` w BKROTPatch `ClanActionsKnighthoodCleanupPatch.ClearCouncilPositions` (BK tworzy nowy rod rycerza, d382) - FirstChance, polkniety w BK `TryCatch`.
- 1 x `NullReferenceException` BK `ClaimantDemand.SetTexts` (d626) - polkniety przez nasz Mends ("zadanie bez skutku tego dnia").
- "Wzrost Innych" / "NocnyMarsz" z wyrazem "wyjatek" to nazwy regul (ucieczka-wyjatek), nie bledy kodu.

### 2.2 Czas doby [P]

| okno | s / dobe | takt doby naszych modow (ms, srednio) |
|---|---:|---:|
| rok 1 (d2-364) | 12.43 | 398 |
| rok 2 (d365-728) | 12.27 | 417 |
| d337-364 / d701-728 | 12.25 / 12.56 | 415 / 390 |
| bieg roczny kodu 43 / GL (d2-364) | 12.52 / 12.58 | 372 / 381 |

- Trend (regresja s/dobe po dobie kampanii): -0.02 s/dobe na rok - czas NIE rosnie [P]. Bloki 28 dob od 10.6 (d1-28) do 13.6 s/d (d281-308); najdluzsza doba 18 s (d301, d366, d421) [P].
- Takt naszych modow +19 ms w roku 2 (+5%), przy 12.3 s doby to 0.15% [P].

### 2.3 Zapis [P]

13 zapisow (12 kontrolnych co 60 dob + koncowy), kazdy OK; czas zapisu 4.7 s (d60) -> 5.5 s (d720) -> 5.3 s (d728).

| pole (linia przy zapisie) | d60 | d300 | d420 | d728 |
|---|---:|---:|---:|---:|
| arm_people caly napis (znaki) | 438 523 | 712 666 | 749 916 | 707 615 |
| w tym weterani E4/10 (znaki) | 10 924 | 72 496 | 176 635 (d540) | 120 443 |
| zbrojownie zalog (szt.) | 395 469 | 480 929 | 510 971 | 387 696 |
| warsztaty (znaki) | 222 790 | 211 332 | 214 790 | 207 766 |
| zlom z nadmiaru (znaki) | 149 098 | 165 089 | 169 338 | 187 185 |
| NocnyMarsz / Musztra (znaki) | 24 270 / 33 922 | 23 999 / 36 778 | 23 843 / 37 910 | 17 863 / 26 991 |
| N7 arm_n7_rally (znaki, z linii doby N7) | - | - | - (max rok 1: 34 057, rok 2: 30 256) | 13 615 |

- arm_people po d300 stoi na 700-750 tys. znakow (nie rosnie bez konca) [P]. Pole N7 przekracza 32 767 znakow (max 34 057), ale SaveText.Sync dzieli napis na kawalki po 8 000 [P z linii N7-6]. Wczytania zapisu z tak duza pula N7 ten bieg nie sprawdza - nie wiem, czy wczyta sie bez straty (test zapisu po biegu: osobno).

## 3. N7 - rozbici wracaja pod choragiew [P]

| | rok 1 (d1-364) | rok 2 (d365-728) | 2 lata |
|---|---:|---:|---:|
| rozbici z druzyn lordow | 225 768 | 133 032 | 358 800 |
| pod choragiew / do domu / do lasu | 55.4 / 26.4 / 18.2% | 55.4 / 27.4 / 17.2% | 55.4 / 26.7 / 17.8% |
| odebrani przez druzyny rodu | 120 719 | 70 623 | 191 342 |
| rozeszli sie po czasie czekania | 1 559 | 4 529 | 6 088 |
| odebrani / (odebrani + rozeszli) | 98.7% | 94.0% | 96.9% (prog >= 50%) |
| czekajacy srednio (max) | 2 136 (5 568) | 1 635 (4 666) | |
| potkniecia / czas linii | 0 / 0.65 ms | 0 / 0.57 ms | 0 |

Pula wyrzutkow i bandy:

| doba | pula (ten bieg) | bieg roczny kodu 43 bez N7 | bieg roczny GL z N7 | bandy (liczba / ludzie, ten bieg) |
|---|---:|---:|---:|---|
| d120 | 1 955 | 7 771 | 2 422 | 576 / 10 706 |
| d182 | 5 189 | 12 948 | 2 188 | 710 / 14 049 |
| d364 | 7 483 | 11 674 | 5 711 | 949 / 19 240 |
| d728 | 4 479 (d722-728 srednio 4 511) | - | - | 1 098 / 21 627 |

- Rok 2: pula srednio 6 774 (min 4 313, max 9 037), rok 1 srednio 4 923 [P]. Cel 3-4 tys. byl liczony na d120 - w roku 2 pula ponad celem, ale spada od d420 (8.3 tys.) do 4.5 tys. [P].
- Bandy rosna we wszystkich trzech biegach (d364: 21.8 / 18.7 / 19.2 tys. ludzi) - nie od N7 [P].
- W roku 2 trzy razy wiecej rozejsc po czasie (1.6 -> 4.5 tys.) przy mniejszym wojsku i mniejszej liczbie wojen - spojne z luzem zoldu [S].

## 4. Lup zdobytego miasta (L0-L7) i lup 5b [P]

| | rok 1 | rok 2 | 2 lata |
|---|---:|---:|---:|
| zdobycia szturmem (linie L0) | 27 (9 miast, 18 zamkow) | 12 (3 miasta, 9 zamkow) | 39 |
| laska / grabiez / spustoszenie | 13 / 10 / 4 | 5 / 4 / 3 | 18 / 14 / 7 |
| lup z monety (L2), zl | 1 517 938 | 403 296 | 1 921 234 (kupcy 806 575, lud 1 114 659) |
| do zolnierzy / wodzow / korona (z czesci wodzow) | | | 1 280 948 / 640 286 / ok. 213 428 |
| zabici przy nastepstwach (BK, L6) | 15 794 (7.8% ludnosci zdobytych osad) | 9 018 (10.0%) | 24 812 |
| towar z polek do taborow (L7) | | | 7 118 szt. za 1 214 110; spalone 0 szt. |

- Wybor AI zgodny z regula 3.4 w 39/39; "armia w potrzebie" 1 z 39 [P].
- Budowle: splonelo 108 poziomow w 21 lupach (grabiez 3-4, wyjatkowo 9; spustoszenie 7-10); mury bez zmian, laska bez pozaru [P]. Zgliszcza: d120 6 osad / 26 poziomow, d364 13 / 51, d480 17 / 66 (szczyt), d728 13 / 54; odbudowane ze zgliszcz 54 poziomy (50%) [P]. Cywilne czekajace przez front albo brak luzu: 2-8 [P].
- Glod w lupionych: 1 193 pomiary "stac na jedzenie" d+0..d+30 po 39 nastepstwach - wszystkie 1.00, 0 ponizej 0.95 (LUP-11) [P]. Ta miara nie drgnela ani razu, wiec nie wiem, czy jest czula (w calym swiecie zima dala 721 osado-dob "brak pieniedzy na jedzenie") [S].
- Kasa kupcow zlupionej osady ponizej zapasu: 4 osady, najdluzej 13 dob (Tolos) [P]. L3: wstrzymane wydatki zycia w oknie lupu 6.06 mln zl zostalo w sakiewkach [P].
- Te same twierdze zdobyte dwa razy: Qamayl, Haystack Hall, Pinkmaiden, Wayfarer's Rest, Tolos (Tolos lupiony 2 razy: d163, d639) [P].
- L5b-1 skarbiec stolicy: 3 upadki (d106 Dragonstone 0 zl - bez nadwyzki; d166 Mantarys 256 950; d182 Oros 163 618) = 420 568 zl; obieg co do zlotowki, przegrany nie ponizej zapasu wojny [P]. Rachunek CHANGELOG: 2-3 upadki / rok, 0.30-0.39 mln [S].
- L5b-2 karawany wroga: w 39 zdobyciach 0 karawan w murach (wrogich 0, niewrogich 0, ze statkami 0), latka wpieta [P]. Szacunek byl 1-3 / rok [S]. Zero nawet niewrogich karawan w 12 miastach to podejrzenie, ze w chwili wywolania gra juz wyprowadzila partie z osady - nie wiem.

## 5. M6a - najemnicy, okupy, pokoj na zero dni [P]

| | rok 1 | rok 2 |
|---|---:|---:|
| najmy / konce sluzby kompanii AI | 150 / 139 | 195 / 195 |
| sluzba krotsza niz 28 dob bez wyjatku / najem u wroga | 0 / 0 | 0 / 0 |
| kompanii w sluzbie srednio | 10.5 | 10.0 |
| okupy AI-AI / w tym powtorne w 56 dobach | 1 623 / 613 | 1 034 / 285 |
| cena pelna / zaplacona powtornych (zl) | 27.5 mln / 19.4 mln | 13.7 mln / 9.7 mln |
| zostalo u rodzin jencow (zl) | 8.10 mln | 3.99 mln |
| osoby wykupione 5+ razy w roku (linie Okupy 178) | 95 | 21 |
| pokoje z odlozonym uwolnieniem / odwolane (wojna wrocila) / uwolnieni po dobie / krolestwo upadlo | 95 / 77 / 17 / 1 | 64 / 34 / 24 / 5 |
| jency-bohaterowie, ktorzy nie wyszli za darmo | 585 | 246 |

- Srednia sluzba 22.0 doby (2 lata; krotsza niz 28 tylko z wyjatkami - pokoj u pracodawcy, niedoplata, upadek) [P]. Ludzie najemnikow AI d701-728: 1 612 (prog etapu 8 >= 2 000 - NIE, patrz rozdz. 8) [P].
- Uwolnieni przez pokoj w doby pokoju fabularnego: 0.32 / dobe (prog <= 1; bez paczki 6.8) [P].
- Petla wykupu dalej jest: 95 osob 5+ razy w roku 1 (bieg roczny GL z ta sama paczka: 54, bieg bez paczki: 53) - rozrzut kampanii duzy, nie wiem, czy paczka petle zwieksza [P]/[S].

## 6. W5 / W6 / W6b / W7 - krotko [P]

| | koniec roku 1 (d337-364) | koniec roku 2 (d701-728) |
|---|---:|---:|
| W5 danina / garnek / wyplacono (tys. zl/d) | 417.6 / 379.8 / 40.5 (49 rodow) | 210.6 / 195.1 / 28.3 (34 rody) |
| renty 180 (tys. zl/d) | 477.6 | 332.4 |
| W6 tarczowe pobrane (tys. zl/d) | 1.2 | 0.4 |
| W7 pobrano (tys. zl/d; rodow; osad; pokrycie) | 18.1; 23; 78; 39% | 14.7; 24; 77; 28% |
| W7 koszt (suma osad / dobe) | lojalnosc -9.5, dobrobyt -19.1 | lojalnosc -10.1, dobrobyt -20.3 |

- W5b ze skarbcow w 2 lata 4.58 mln zl; skarbce ponad zapas wojny 3.42 mln (4 krolestwa) -> 0.13 mln (6) [P].
- W7 rok 2 najmocniej: Zelazne Wyspy 6.5 tys. zl/d (lojalnosc -26 / tydzien), Wolni Ludzie 2.4 tys. (pokrycie 13%) [P].
- W6b: gracz w autotescie bez lenna i krolestwa przez 727 dob - regula ani razu nie miala warunkow; wynik tylko z recznego testu [P].

## 7. Wojsko w roku 2 (miara W5-7) - glowne NIE biegu [P]

| okno | wojsko rodow krolestw w wojnie (tys.) | krolestw w wojnie | wojsko wszystkich krolestw (tys.) | par wojen (CS) | pora |
|---|---:|---:|---:|---:|---|
| d337-364 | 108.9 | 26.0 | 109.0 | 16.5 | jesien |
| d421-448 | 109.7 | 26.0 | 109.8 | 21.3 | jesien |
| d449-476 | 83.0 | 19.0 | 92.7 | 16.9 | zima od d461 |
| d505-532 | 47.7 | 11.6 | 59.5 | 9.7 | zima |
| d533-560 | 43.9 | 10.1 | 51.4 | 7.1 | zima |
| d589-616 | 62.7 | 16.8 | 64.5 | 13.7 | zima |
| d701-728 | 55.3 | 17.3 | 57.4 | 14.8 | zima |

- Porownania koniec roku 1: kod 43 101.3, gospodarka od ludzi 96.4, ten bieg 108.9 tys. [P]. Rachunek na sucho (rozdz. 7) przewidywal w roku 2-3 ok. -6 tys., najgorszy realny przypadek 73 tys. [S z dokumentu]; zmierzone 55.3 tys. - ponizej najgorszego przypadku [P].
- Alarmy z rozdz. 7.6: nr 1 (koniec roku 2 < 90) - ALARM; nr 9 (krolestw w wojnie < 23) - ALARM (10-17) [P]. Zalogi d364 89.5 tys. -> d728 58.4 tys.; ludzie w partiach rodow AI (CSV) 57.0 tys.; Straz 3.6 tys. (baza 8.7) [P].
- Spadek zaczyna sie razem z zima (T9: "nowa pora Winter" doba 461, plon wsi ciety srednio o 40.8% przez 269 dob) i maleje tez wojsko krolestw w pokoju - to nie samo "mniej wojen w mierze" [P]. Przyczyny (zima / pokoje / budzety 166 / BK) nie ustalilem - nie wiem. Wszystkie kontrole W5 / W6 / W7 / N7 / M6a w roku 2 sa czyste, wiec nie wskazuja na blad tych paczek [P]/[S].

## 8. Wszystkie NIE (okno 728 dob) z ocena

Kolumna "byl?": czy ten sam wiersz byl NIE w oknie roku 1 biegow porownawczych (kod 43 = r43, gospodarka od ludzi = GL) albo tego biegu (d364). Ocena: BLAD / CECHA / PROG (prog do zmiany) / ROK2 (skutek zimy i spadku wojska - przyczyna nieustalona).

| # | grupa | wiersz | wartosc [P] | prog | byl? | ocena |
|---|---|---|---|---|---|---|
| 1 | etap2 | wojsko w druzynach lordow (28 dob) | 55 550 | 85 tys. podloga | nie (rok 1: 108.9) | ROK2 - glowny alarm, rozdz. 7 |
| 2 | etap2 | glowy < 5000 d728 | 18 | <= 10 | d364 tego biegu 11 | ROK2 (rody biednieja w zimie) |
| 3 | etap2 | glowy < 5000 d364 | 11 | <= 10 | GL 15 | PROG / znane (rozrzut kampanii) |
| 4 | etap2 | bankruci K39 | 1 (d642) | 0 | GL 1 (d226) | BLAD drobny, stary (nie z nocy) |
| 5 | etap2 | zalegly zold w partiach | 3.03% | <= 2% | GL 2.03% | ROK2 |
| 6 | etap2 | 110 pan samych zamkow z zaworu | 405 zl/d | 200-350 | r43 415, GL 429 | PROG (cel sprzed paczek) |
| 7 | etap2 | 112 nieprzypisane utarg | 4 294 966 847 (2 doby) | 0 | r43 6 442 449 530 (3 doby) | BLAD stary - przepelnienie licznika podatku wsi (rozdz. 9) |
| 8 | etap4 | E4/3 pula wyrzutkow d728 | 4 429 | <= 3 053 i <= 1.25 x d90 | r43, GL, d364 | PROG (baza sprzed N7, okno 120) |
| 9 | etap4 | E4/3 bandy d728 | 1 097 / 21 618 | <= 867 / 15 044 | r43, GL, d364 | CECHA / PROG (bandy rosna w kazdym biegu - do decyzji) |
| 10 | etap4 | E4/7 T3 rownanie krain | valyrian -0.216% (-550 ludzi, od d130) | <= max(0.1%, 200) | GL aserai -0.188%, d364 | BLAD ksiegi drobny, stary typ |
| 11 | etap4 | E4/10 V10 (to samo rownanie z weteranami) | jak 10 | jak 10 | GL, d364 | jak 10 (ten sam brak) |
| 12 | etap4 | E4/9 K19 (to samo rownanie z T i W) | jak 10 | jak 10 | GL, d364 | jak 10 |
| 13 | etap4 | E4/8 N4 zalogi d728 | 58 366 | >= 69 020 (wiaze przy d120) | nie | PROG (okno) + ROK2 (89.5 -> 58.4 tys.) |
| 14 | etap4 | E4/9 K7 Straz d728 | 3 640 | >= 7 387 | nie (d364 TAK) | ROK2 - nie wiem |
| 15 | etap4 | E4/9 K20 DLUG 4 do lasu | 47 / 94 | <= 5% | GL 73 / 136 | BLAD reguly, stary |
| 16 | etap4 | E4/11 G5 trafieni Devastation | 1 doba poza (d286) | 0 dob | d364 (ta sama doba) | BLAD drobny, 1 doba |
| 17 | etap8 | C-4b kasy miast ponizej zapasu przez nasze sciezki | 9 (5 niezerowych) | 0 | r43 3, GL 6 | BLAD drobny, stary |
| 18 | etap8 | P3-2 zatkane wsie bez wozu > 7 dob | ciag 6 dob, max 2 wsie | ciag <= 2 | d364 (ciag 3); r43 i GL nie | nowe wobec biegow rocznych (tam ciag <= 2; w testach 120 dob TAK), drobne - przyczyna nieznana, do obejrzenia |
| 19 | etap8 | Q-3 partia zawracana > 3 razy / dobe | 7 partio-dob (max 4, d448) | 0 | nie | drobne, rok 2 - do obejrzenia |
| 20 | etap8 | G1-5a suma kas miast wobec Z33 | +12.5% | 80-110% w d120 | r43 +25.6, GL +26.7 | PROG (wiaze tylko d120) |
| 21 | etap8 | G1-5c kiesa na karawane wobec Z33 | +99% (536 karawan zamiast 962) | +-20% w d120 | GL +33% | PROG (wzor Z33) |
| 22 | etap8 | H-4b reszta uprzezy w ksiedze | -18 948 przy 23 273 (-81%) | <= 10% | r43, GL (Z33: -82%) | BLAD ksiegi uprzezy, stary (ten sam odsetek co Z33) |
| 23 | etap8 | H-5b wstrzymane z braku uprzezy | x1.35 | <= 1.1x + 5 | r43 x1.44, GL x1.22 | znane (kolejka siodel) |
| 24 | etap8 | O5-2 sprzedaz zalog wobec Z33 | x0.91 szt. / x0.75 zl | +-15% | r43, GL | PROG (Z33 sprzed etapow) |
| 25 | etap8 | P3W1-1 rody bez warunku renty | 10.8% | <= 10% | nie | PROG / ROK2 (na granicy) |
| 26 | etap8 | P3W1-3 udzialy bez warunku | 24 042 zl/d | <= 20 000 | GL 42 353, d364 34 252 | znane (w roku 2 lepiej) |
| 27 | etap8 | P3W3-3 ludzie najemnikow AI | 1 612 | >= 2 000 | od testu WGRANIA 45 (1 830) | CECHA M6a (dluzsza sluzba = mniej najmow) + ROK2 |
| 28 | etap8 | P3W-5 ludzie w partiach rodow AI | 56 969 | >= 70 000 | nie | ROK2 |
| 29 | etap8 | P3W1-6 rody z lennem z 0 ludzi | 41 z 186 | <= 12 | nie | ROK2 |
| 30 | etap8 | P3W2-4 zold na czlowieka pulap / luz | 1.32x | <= 1.25x | r43 1.26, d364 1.30 | znane / PROG |
| 31 | etap8 | N5-2 skora | 312 skor/d, 79 miast bez skory wyprawionej | >= 430/d | nie | ROK2 (zima) - nie wiem |
| 32 | etap8 | N5-3 zbroja korpusu t3-4 | 26.4 szt./d, brak 3 479 | >= 1.15x bez paczki, brak <= 3 500 | r43, GL, d364 | znane |
| 33 | etap8 | N5-5 budowy cywilne | idzie 96.6, stoi 124.4 | >= 80 i <= 30% materialow | r43, GL, d364 | znane |
| 34 | w5 | W5-4 wypelnienie rodow z doplata | 43.4 -> 40.5% | rosnie | r43, GL, d364 | PROG / CECHA (awanse zjadaja miejsce - jak w kazdym biegu) |
| 35 | etap8 | C-1e barda t6 (DECYZJA) | mediana 2 760 | <= 2 600 | r43, GL | DECYZJA czeka (stara) |

Poza progami narzedzia (moje odczyty):
- L5b-2: 0 karawan w 39 zdobyciach - nie wiem: CECHA (karawany nie stoja w obleganych miastach) albo martwy kod (wywolanie po wyprowadzeniu partii przez gre).
- Licznik podatku wsi > 2^31 - BLAD stary (rozdz. 9).
- LUP-11 "stac na jedzenie" = 1.00 we wszystkich 1 193 pomiarach - nie wiem, czy miara jest czula.

Podsumowanie ocen (z tabeli; wiersz moze miec dwie oceny): BLAD 9 wierszy = 7 roznych spraw, wszystkie stare albo drobne (byly w biegach sprzed paczek albo 1 doba), zaden nie wskazuje na paczki nocy; ROK2 10; PROG 11; CECHA 3; znane bez zmian 5; nowe drobne do obejrzenia 2 (P3-2, Q-3); DECYZJA 1.

## 9. Blad licznika podatku wsi (przepelnienie) [P]

- `MoneyLedger.cs` sumuje `v.TradeTaxAccumulated` (pole gry, int) do licznika "podatek wsi". W dobach d699, d709 licznik skacze o +2 147 474 953 / +2 147 492 075, w d725 o +4 294 942 579 (dwa przeskoki); po 7-8 dobach (d707, d716) wraca - przy powrocie taboru linia 112 pokazuje "pan (licznik podatku) 0 (-1 071 443% po dopisaniu) ... bez odbiorcy 2 147 538 814", "nieprzypisane 2 147 483 430".
- "Pieniadz swiata razem" skacze 206.8 mln -> 2 354 mln -> 4 502 mln (d726), a kiesy glow rodow tych dob NIE rosna (69.37 -> 69.23 mln) - nikt tych pieniedzy nie dostal; jedna wies ma na chwile pole ok. int.MaxValue [P]/[S].
- W obu powrotach linia 112 wymienia "1 powrotow bez podatku BK: Tanos" (w biegu kodu 43, d287-292: "Tumblestone Farms") - prawdopodobnie wies bez podatku BK [S]. Przyczyny nie znam.
- Byl juz w biegu rocznym kodu 43 (6 dob, d287-292), nie bylo go w biegu rocznym GL - stary, rzadki, nie z paczek nocy [P]. Skutek: psuje pomiary oparte na "Pieniadz swiata" (etap2 "pieniadz swiata +76.7 mln/d" w oknie d701-728, "112 nieprzypisane") - te wiersze w tym biegu czytac z odjeciem przeskokow.

## 10. Czego nie wiem

- Dlaczego wojsko spadlo w zimie roku 2 o polowe (rozdz. 7) - tu tylko zbieznosc w czasie z zima i z mniejsza liczba wojen.
- Czy zapis z pula N7 34 tys. znakow i arm_people 750 tys. znakow wczytuje sie bez strat (ten bieg nie wczytuje).
- Czy L5b-2 (karawany wroga) w ogole moze sie odpalic przy zdobyciu przez AI.
- Czy miara LUP-11 (stac na jedzenie) jest czula.
- Ktora wies i ktora sciezka kodu daje przepelnienie TradeTaxAccumulated.
- Sciezki gracza (W6b, okno lupu L5, karawany gracza) - autotest ich nie dotyka.
- Log RealisticCaptivity / GrandTourney tego biegu - nie bylo go w kopii na D:.

## 11. Pliki (w tym katalogu)

`grupa-<g>.txt` - progi 728 dob; `por\<r43|rgl|d364>-<g>.txt` - progi okna roku 1 (bieg kodu 43, bieg GL, ten bieg); `analiza.py` / `analiza2.py` + `analiza-dwuletni.txt`, `analiza-15-02-59.txt`, `analiza-19-21-12.txt` - czas doby, wojsko, N7, pula, M6a, lup, zapis, bledy (jeden przebieg strumieniowy); `cs_sesje.py` + `cs-sesje.txt` - bloki bledow trzech sesji CS i nowe sygnatury; `run_groups.sh` - wywolania narzedzia.
