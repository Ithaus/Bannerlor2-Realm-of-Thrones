# ROZPOZNANIE: SILY KROLESTW (bieg dwuletni fin30, 14.10)

Rozpoznanie do sprawy Jeffa z 14.10: "sily nie sa rowne, oblezenia sa za szybkie i za latwe".
Tylko pomiar i kod - bez zmian, bez propozycji rozwiazania (to robia projekty).
Znaczniki: [P] pomiar (plik / linia), [K] kod (plik:linia), [U] ustawienie (plik), [S] szacunek.

## 0. W skrocie (prostym jezykiem)

1. Krolestwa jako calosc nie sa bardzo nierowne: zdobywca mial zwykle 1.1-2.3 razy wiecej ludzi w druzynach niz obronca. Nierownosc robi sie pod murami: przychodzi armia 13-16 lordow (ok. 2400-3000 ludzi) na ok. 430 obroncow - mediana 7.4 : 1, ani jednego odpartego szturmu.
2. Zalogi nie sa mniejsze niz w grze bez modow (zamek 254, miasto 517 ludzi na poczatku oblezenia; gra daje 200 / 400 miejsc). Ale w 11 z 57 zdobyc obroncy przegrali tuz przed szturmem bitwe pod murami (gra robi ja ze szturmu, gdy dolacza oddzial spoza murow) i szturm szedl juz tylko na milicje.
3. Odsieczy prawie nie ma: Krolewska Przystan padla, gdy lordowie Baratheonow bili sie w Dorzeczu (42 z 59 ich bitew); Dorne broni sie po kawalku, malymi oddzialami przeciw bandom.
4. Przegrany w polu prawie nie ginie (nasze reguly: 152 tys. zabitych zamiast 399 tys. w grze, 166 tys. rozbitych wraca pod choragiew), a zaloga pobita pod murami ginie jak w grze - armia oblegajaca sie odradza, obrona nie.
5. Wojsko idzie za pieniedzmi, a pieniadze za ludzmi: Reach (8 mln ludzi) ma pulap zoldu 3.2 razy wiekszy niz Dorne (1.5 mln), wiec od polowy wojny Dorne wypelnia druzyny w 36-42 %, Reach w 75 %; kto traci zamek, traci dochod i wojsko.
6. Krolestwo w pokoju prawie nie ma wojska (Dorne 5 %, Dolina 14 %, Targaryen 19 % limitu na koniec), a w roku 2 budzet rodow zwolnil z zalog 49 tys. ludzi (w roku 1 - 5 tys.).
7. Stormlands zniknely przez fabule ROT (smierc Renly'ego d536: 16 twierdz do Smoczej Skaly), nie przez oblezenia. Krolewska Przystan i cztery inne zdobycze Polnocy dostal rod Bolton. Harroway w tabelach caly bieg nalezy do Dorzecza.
8. Nowe krolestwa (19, z tego 18 w roku 2, wszystkie w Essos) to rody, ktore odchodza z krolestwa razem ze swoimi lennami - bez oblezen.

## 1. Zrodla i umowy

| Co | Gdzie |
|---|---|
| log Armoury fin30 (A) | `D:\Backup-Bannerlord\logs\2026-10-14-noc\Armoury-2026-10-14_06-01-15.log` (287 MB) |
| sesja CrashScribe fin30 (S) | `D:\Backup-Bannerlord\logs\2026-10-14-noc\session-2026-10-14_06-01-14.log` |
| tabele fin30 (T) | `D:\Backup-Bannerlord\logs\2026-10-14-noc\tabele-06-01-15\` - `balans-krolestw.csv`, `budzet-rodow.csv`, `ludzie-regiony.csv`, `spichlerze-regiony.csv`, `bitwy.log` |
| tabele fin21 (porownanie) | `D:\Backup-Bannerlord\logs\2026-10-14-noc\Logs\2026-10-13_21-14-13\` |
| kod gry 1.4.8 | `...\ae222cb7...\scratchpad\dec32\cs_full` |
| kod BK | `...\ae222cb7...\scratchpad\e4tools\bkfull` |
| kod ROT | `ROT-Core\bin\Win64_Shipping_Client\ROT.dll` (dekompilacja RenlyDeathEvent: `robocze\sily\dekompilacja\RenlyDeathEvent.cs`, zgodna 1:1 z `...\scratchpad\a6dec\rot`) |
| nasz kod | drzewo `...\scratchpad\zb\int` (galaz w-toku/zbior-e8, 24bd64b3 - 34 commity po fin30 = 6e6313c; wartosci K8 i NorthHomeEdge sprawdzone w logu fin30) |

- **Doba wzgledna**: d = dzien gry - 108835 (Harrenhal d128, Krolewska Przystan d530 - jak w raporcie). Log obejmuje d1-d729 [P] (`balans-krolestw.csv`, pierwszy wiersz 108837, ostatni 109564).
- **Kalendarz**: rok = 364 doby [P] (A, linia "Kalendarz: rok ma 364 dni"); d364 = koniec roku 1.
- **Krolestwa wedlug id** [K] `a6dec\rot\ROT.Misc\ROTKingdoms.cs:12-36`: vlandia = Baratheon KP (w kodzie ROT "Westerlands"), battania = Polnoc, empire_w = Reach, aserai = Dorne, empire = Dolina, sturgia = Zelazne Wyspy, empire_s = Targaryen (Dany), khuzait = Dothrakowie, nord = Lorath, valyrian = Targaryen Aegon.
- **Wiersz dnia w `ludzie-regiony.csv` / `spichlerze-regiony.csv`** czasem pokazuje juz stan po zdarzeniach z pierwszych godzin nastepnej doby (Cape Wrath: wiersz 108857 ma juz nowego wlasciciela, szturm w `bitwy.log` ma dzien 108858) [P]. Dlatego "przed zdobyciem" = ostatni wiersz, w ktorym twierdza nalezy jeszcze do obroncy.
- **Milicja\*** w tabelach regionow = milicja twierdzy + milicja jej wsi [K] `zb\int\Armoury\src\PeopleLedger.cs:408-419`. W szturmie bierze udzial tylko milicja twierdzy.
- Pliki posrednie (TSV, skrypty liczace w scratchpadzie): `docs\oblezenia\robocze\sily\` - lista w rozdz. 10.

## 2. Wojsko krolestw w czasie

### 2.1 Ziemia, ludzie, druzyny, zalogi, milicja

Zrodla: twierdze, druzyny, zalogi - [P] T `balans-krolestw.csv` (kolumny twierdze, ludzie_partie, ludzie_zalogi); ludzie i milicja\* - [P] T `ludzie-regiony.csv` (suma regionow krolestwa). Wolne Miasta = Braavos, Pentos, Norvos, Qohor, Myr, Lys, Tyrosh, Lorath, Volantis (bez buntownikow i hrabstw). Wyliczenie: `robocze\sily\sily-krolestw.tsv`.

| d | krolestwo | twierdze | ludzie (tys.) | w druzynach | w zalogach | milicja* | druzyny na 100 tys. ludzi | druzyny na twierdze | zaloga na twierdze | milicja* na twierdze |
|---|---|---|---|---|---|---|---|---|---|---|
| 2 | Baratheon KP | 21 | 5077 | 1107 | 4393 | 5160 | 22 | 53 | 209 | 246 |
| 2 | Polnoc | 22 | 2999 | 2289 | 4236 | 4486 | 76 | 104 | 193 | 204 |
| 2 | Reach | 16 | 7998 | 1792 | 3415 | 4476 | 22 | 112 | 213 | 280 |
| 2 | Dorne | 16 | 1499 | 849 | 2926 | 2883 | 57 | 53 | 183 | 180 |
| 2 | Dorzecze | 14 | 3021 | 3022 | 3210 | 3543 | 100 | 216 | 229 | 253 |
| 2 | Stormlands | 13 | 2498 | 2152 | 2457 | 3151 | 86 | 166 | 189 | 242 |
| 2 | Dolina | 13 | 2999 | 1765 | 2680 | 3110 | 59 | 136 | 206 | 239 |
| 2 | Zelazne Wyspy | 10 | 473 | 1300 | 2331 | 1522 | 275 | 130 | 233 | 152 |
| 2 | Targaryen (Dany) | 6 | 2008 | 1146 | 1667 | 1590 | 57 | 191 | 278 | 265 |
| 2 | Smocza Skala | 10 | 849 | 1939 | 2038 | 1565 | 228 | 194 | 204 | 156 |
| 2 | Wolne Miasta (9) | 46 | 15411 | 5866 | 9877 | 11918 | 38 | 128 | 215 | 259 |
| 91 | Baratheon KP | 21 | 5065 | 9623 | 8122 | 6691 | 190 | 458 | 387 | 319 |
| 91 | Polnoc | 22 | 2988 | 10514 | 8833 | 4033 | 352 | 478 | 402 | 183 |
| 91 | Reach | 17 | 8102 | 8608 | 6296 | 8836 | 106 | 506 | 370 | 520 |
| 91 | Dorne | 15 | 1372 | 6692 | 5516 | 2034 | 488 | 446 | 368 | 136 |
| 91 | Dorzecze | 14 | 3010 | 7427 | 4907 | 3747 | 247 | 530 | 350 | 268 |
| 91 | Stormlands | 15 | 2701 | 6473 | 5061 | 3729 | 240 | 432 | 337 | 249 |
| 91 | Dolina | 13 | 2990 | 5609 | 4012 | 4027 | 188 | 431 | 309 | 310 |
| 91 | Zelazne Wyspy | 10 | 467 | 4577 | 4858 | 1214 | 979 | 458 | 486 | 121 |
| 91 | Targaryen (Dany) | 6 | 2002 | 2500 | 2351 | 1611 | 125 | 417 | 392 | 268 |
| 91 | Smocza Skala | 8 | 618 | 4402 | 3430 | 882 | 712 | 550 | 429 | 110 |
| 91 | Wolne Miasta (9) | 46 | 15361 | 22744 | 16946 | 14589 | 148 | 494 | 368 | 317 |
| 182 | Baratheon KP | 21 | 4854 | 8531 | 7793 | 6796 | 176 | 406 | 371 | 324 |
| 182 | Polnoc | 23 | 3219 | 10245 | 9449 | 4406 | 318 | 445 | 411 | 192 |
| 182 | Reach | 19 | 8244 | 10194 | 7560 | 10260 | 124 | 537 | 398 | 540 |
| 182 | Dorne | 13 | 1216 | 8010 | 5096 | 1948 | 659 | 616 | 392 | 150 |
| 182 | Dorzecze | 14 | 3169 | 7113 | 5242 | 4245 | 224 | 508 | 374 | 303 |
| 182 | Stormlands | 17 | 2770 | 6261 | 5699 | 4181 | 226 | 368 | 335 | 246 |
| 182 | Dolina | 12 | 2786 | 7497 | 4964 | 4285 | 269 | 625 | 414 | 357 |
| 182 | Zelazne Wyspy | 10 | 467 | 4949 | 5000 | 1210 | 1061 | 495 | 500 | 121 |
| 182 | Targaryen (Dany) | 6 | 1998 | 4068 | 2538 | 1751 | 204 | 678 | 423 | 292 |
| 182 | Smocza Skala | 6 | 535 | 3271 | 2995 | 809 | 612 | 545 | 499 | 135 |
| 182 | Wolne Miasta (9) | 47 | 15692 | 23371 | 19127 | 16306 | 149 | 497 | 407 | 347 |
| 364 | Baratheon KP | 22 | 5021 | 9459 | 7995 | 7873 | 188 | 430 | 363 | 358 |
| 364 | Polnoc | 23 | 3223 | 10832 | 9742 | 4558 | 336 | 471 | 424 | 198 |
| 364 | Reach | 23 | 8653 | 9914 | 10031 | 12183 | 115 | 431 | 436 | 530 |
| 364 | Dorne | 10 | 920 | 4578 | 4264 | 1561 | 498 | 458 | 426 | 156 |
| 364 | Dorzecze | 13 | 2964 | 7519 | 4782 | 3928 | 254 | 578 | 368 | 302 |
| 364 | Stormlands | 17 | 2741 | 5384 | 6508 | 4650 | 196 | 317 | 383 | 274 |
| 364 | Dolina | 12 | 2791 | 7683 | 5343 | 4536 | 275 | 640 | 445 | 378 |
| 364 | Zelazne Wyspy | 10 | 467 | 3636 | 5000 | 1278 | 778 | 364 | 500 | 128 |
| 364 | Targaryen (Dany) | 8 | 2619 | 3163 | 2880 | 2270 | 121 | 395 | 360 | 284 |
| 364 | Smocza Skala | 5 | 433 | 3916 | 2572 | 583 | 905 | 783 | 514 | 117 |
| 364 | Wolne Miasta (9) | 42 | 13159 | 19284 | 17401 | 14430 | 147 | 459 | 414 | 344 |
| 546 | Baratheon KP | 19 | 4596 | 6742 | 6800 | 7692 | 147 | 355 | 358 | 405 |
| 546 | Polnoc | 26 | 3564 | 9783 | 10652 | 5076 | 274 | 376 | 410 | 195 |
| 546 | Reach | 25 | 8817 | 6963 | 6048 | 12363 | 79 | 279 | 242 | 495 |
| 546 | Dorne | 8 | 786 | 1884 | 2110 | 1362 | 240 | 236 | 264 | 170 |
| 546 | Dorzecze | 14 | 3141 | 8098 | 6179 | 4325 | 258 | 578 | 441 | 309 |
| 546 | Stormlands | 0 | 0 | 0 | 0 | 0 | - | - | - | - |
| 546 | Dolina | 12 | 2805 | 1865 | 2126 | 4386 | 66 | 155 | 177 | 366 |
| 546 | Zelazne Wyspy | 10 | 465 | 3129 | 4997 | 1317 | 673 | 313 | 500 | 132 |
| 546 | Targaryen (Dany) | 10 | 3166 | 1865 | 3680 | 3578 | 59 | 186 | 368 | 358 |
| 546 | Smocza Skala | 21 | 3028 | 5518 | 7054 | 4895 | 182 | 263 | 336 | 233 |
| 546 | Wolne Miasta (9) | 31 | 9884 | 5763 | 6307 | 10635 | 58 | 186 | 203 | 343 |
| 728 | Baratheon KP | 15 | 3724 | 6824 | 5125 | 7008 | 183 | 455 | 342 | 467 |
| 728 | Polnoc | 28 | 3836 | 9221 | 10461 | 5263 | 240 | 329 | 374 | 188 |
| 728 | Reach | 26 | 9039 | 10605 | 9741 | 14380 | 117 | 408 | 375 | 553 |
| 728 | Dorne | 8 | 793 | 409 | 1168 | 1219 | 52 | 51 | 146 | 152 |
| 728 | Dorzecze | 17 | 3797 | 7633 | 6433 | 5603 | 201 | 449 | 378 | 330 |
| 728 | Stormlands | 0 | 0 | 0 | 0 | 0 | - | - | - | - |
| 728 | Dolina | 12 | 2815 | 785 | 1657 | 4532 | 28 | 65 | 138 | 378 |
| 728 | Zelazne Wyspy | 9 | 397 | 6770 | 4424 | 1301 | 1707 | 752 | 492 | 145 |
| 728 | Targaryen (Dany) | 10 | 3176 | 572 | 1941 | 3873 | 18 | 57 | 194 | 387 |
| 728 | Smocza Skala | 20 | 2815 | 9259 | 7694 | 5033 | 329 | 463 | 385 | 252 |
| 728 | Wolne Miasta (9) | 28 | 9476 | 3162 | 4214 | 10414 | 33 | 113 | 150 | 372 |

Co z tego widac [P, ta sama tabela]:
- **Start (d2)** druzyny sa male (zimny start: 849-3022 ludzi na krolestwo), do d91 rosna 3-8 razy. Zalogi rosna z 47 235 (d1) do 92 002 (d361), potem spadaja do 65 611 (d728) [P] A, linie "Zalogi: przyrost bez werbunku (169c)".
- **Polnoc** ma najwiecej twierdz (22 -> 28) i najwiecej ludzi w druzynach prawie przez caly bieg (wyjatki w tabeli: d2 Dorzecze 3022, d728 Reach 10 605); jej udzial w wojsku swiata: 7.7 % (d2) -> 9.5 % (d91) -> 9.9 % (d364) -> 13.2 % (d546) -> 15.6 % (d637) -> 12.2 % (d729) [P] T `balans-krolestw.csv`, kolumna ludzie_partie_pct_swiata. Czlowiekodni w druzynach za caly bieg: Polnoc 7.33 mln, Reach 6.09, Baratheon KP 5.74, Dorzecze 5.49, Stormlands 3.48 (do d536), Dolina 3.44, Smocza Skala 3.34, Zelazne Wyspy 3.12, Dorne 3.06, Targaryen 2.12 [P] T `budzet-rodow.csv` (suma ludzi_partie), wyliczenie `doplaty-wobec-zoldu.tsv`.
- **Na glowe mieszkanca** wojska jest najwiecej tam, gdzie ludzi malo: Zelazne Wyspy 673-1707 / 100 tys., Smocza Skala 182-905, Polnoc 240-352, Dorne 488-659 (d91-d364); najmniej w Reach (79-124) i u Baratheonow KP (147-190) (d91-d728) - liczba druzyn idzie za liczba rodow i twierdz z ROT, nie za ludnoscia.
- **Milicja\* na twierdze** (d91-d728): Reach 495-553, Baratheon KP 319-467, Dorzecze 268-330, Polnoc 183-198, Dorne 136-170, Zelazne Wyspy 121-145 (rozdz. 5.3 - regula GL4).
- **Zapasc w pokoju**: Dorne d182 8010 -> d728 409 ludzi w druzynach, Dolina d364 7683 -> d728 785, Targaryen d529 4343 -> d728 572, Wolne Miasta d182 23 371 -> d728 3162 (rozdz. 2.3).
- **fin21 (drugi bieg dwuletni)** daje ten sam obraz: Reach 16 -> 22 twierdz, Dorne 16 -> 13, Stormlands 13 -> 0, Polnoc 14.8 % wojska swiata, Dorne 70 i Dolina 25 ludzi w druzynach na koniec, 0 odpartych szturmow [P] `Logs\2026-10-13_21-14-13\balans-krolestw.csv`, wiersze 109564.

### 2.2 Pulap zoldu, wypelnienie druzyn, kiesy (budzet rodow 166)

[P] T `budzet-rodow.csv`, suma rodow krolestwa danego dnia (kolumny pulap, ludzi_partie, limit_partii, kiesa_glowy, wojna); wyliczenie `robocze\sily\budzet-krolestwa.tsv`.

| krolestwo | d91: druzyny / limit (wyp.) / pulap zl/d | d364 | d529 | d728 |
|---|---|---|---|---|
| Polnoc | 10 440 / 14 237 (0.73) / 69 899 | 10 785 / 13 624 (0.79) / 105 176 | 10 567 / 14 280 (0.74) / 118 593 | 9 170 / 13 921 (0.66) / 79 766 |
| Reach | 8 602 / 12 382 (0.69) / 98 145 | 9 900 / 13 203 (0.75) / 183 697 | pokoj: 3 350 / 6 179 (0.54) / 31 203 | 10 544 / 13 181 (0.80) / 232 460 |
| Dorne | 6 710 / 12 147 (0.55) / 60 429 | 4 545 / 10 942 (0.42) / 57 483 | 4 341 / 11 961 (0.36) / 32 972 | pokoj: 390 / 7 214 (0.05) / 7 925 |
| Baratheon KP | 9 509 / 12 883 (0.74) / 69 865 | 9 373 / 11 571 (0.81) / 174 133 | 6 302 / 10 976 (0.57) / 79 857 | 7 060 / 13 194 (0.54) / 112 260 |
| Dorzecze | 7 427 / 10 895 (0.68) / 71 097 | 7 570 / 12 049 (0.63) / 118 204 | 8 320 / 12 330 (0.67) / 220 061 | 7 425 / 11 365 (0.65) / 84 237 |
| Stormlands | 6 225 / 9 712 (0.64) / 53 144 | 5 349 / 8 711 (0.61) / 44 023 | 8 650 / 10 184 (0.85) / 114 184 | - (zniklo d536) |
| Dolina | 5 699 / 9 125 (0.62) / 77 205 | 7 674 / 9 967 (0.77) / 74 792 | pokoj: 2 008 / 5 199 (0.39) / 25 082 | pokoj: 747 / 5 199 (0.14) / 12 914 |
| Smocza Skala | 4 308 / 7 346 (0.59) / 41 520 | 3 870 / 8 633 (0.45) / 30 280 | 4 204 / 7 513 (0.56) / 53 593 | 9 193 / 14 195 (0.65) / 196 739 |
| Zelazne Wyspy | 4 545 / 8 828 (0.51) / 36 348 | 3 608 / 7 785 (0.46) / 28 377 | 3 199 / 7 409 (0.43) / 28 381 | 6 628 / 8 391 (0.79) / 156 559 |
| Targaryen (Dany) | 2 434 / 4 538 (0.54) / 56 950 | 3 261 / 6 319 (0.52) / 84 870 | 4 351 / 6 308 (0.69) / 95 314 | pokoj: 565 / 2 937 (0.19) / 14 883 |

- Dorne ma najwiecej rodow (41) i limit druzyn podobny do Reach, ale pulap zoldu 3.2 razy mniejszy (d364: 57 483 wobec 183 697) i wypelnienie 0.36-0.42 w wojnie od d364 (d91 0.55) [P].
- Rody Dorne, ktore stracily twierdze, maja pulap przy zerze: Yronwood (stracil Yronwood d324) pulap 186 zl/d, kiesa 110, 14 ludzi w druzynie przy limicie 602; Dayne (Starfall d48) pulap 525, 28 ludzi przy limicie 1097 [P] T `budzet-rodow.csv`, dzien 109199 (d364).
- Pulap w wojnie liczy sie z dochodu rodu D (0.28 x D) i skrzyni wojennej - Z.1 [P] A, linia "Budzet rodow (166): dzien 109564" ("pulap pokoju razem ... (0.28 D + 0.40 x skrzynia wojenna)"). Wiecej ludzi i wiecej zlota startowego (rozdz. 5.2) = wyzszy D = wyzszy pulap.

### 2.3 Wojsko w pokoju i zwolnienia z budzetu

- Krolestwa w pokoju na koniec biegu: 126 rodow, 5718 ludzi w druzynach, 11 062 w zalogach [P] A, linia "Budzet rodow (166): dzien 109564". Z.1 (wojsko w pokoju) jest czynne w fin30 (ta sama linia: "pokoj (Z.1)").
- Zwolnieni przez budzet (rod ponad 1.10 x pulap) do wsi: rok 1 - 47 171 (z druzyn 41 814, z zalog 5 357); rok 2 - 125 059 (z druzyn 75 873, **z zalog 49 186**) [P] A, linie "Budzet rodow (166)", pole "zwolnieni: do wsi". Zalogi swiata w tym czasie: 92 091 (d391) -> 64 016 (d601) [P] A, "Zalogi: przyrost".
- Bieg fin30 to kod "bez pieniedzy roku 2" (zadanie; STAN-PRAC 14.10 13:01: fin30 = zbior-e8 6e6313c, prace "pieniadze-rok-2" osobno) - B4 (GarrisonPeaceShare) nie wystepuje w logu fin30 (0 trafien "GarrisonPeaceShare") [P].


## 3. Zdobyte twierdze: zaloga, milicja, armia oblegajaca

### 3.1 Liczby zbiorcze (57 zdobyc oblezeniem)

Zrodla: lista zdobyc [P] A, 57 linii "Karawany w zdobytym miescie (L5b-2)" (numery linii w `zdobycia-fin30.tsv`); zaloga i milicja\* [P] T `ludzie-regiony.csv`; poczatek oblezenia i liczba druzyn [P] A, linie "Okup oblezenia (O0)" (113 decyzji gry o szturmie, `okup-O0-fin30.tsv`); bitwy [P] T `bitwy.log` (linie bez "H3", `bitwy-pod-murami.tsv`). Wyliczenie: `zdobycia-szczegoly.tsv`.

| Co | Wartosc |
|---|---|
| zdobycia rok 1 / rok 2 | 31 / 26 (zamki 44, miasta 13) |
| zdobywcy | Reach 11 (8 z Dorne, 2 ze Smoczej Skaly, 1 ze Stormlands), Dorzecze 10, Baratheon KP 6, Polnoc 5, Stormlands 5, Targaryen 4, Volantis 3, Myr 2, Sarnor 2, Antaryon 2, po 1: Tyrosh, Lorath, Ibben, Nocna Straz, Drahar, Smocza Skala, Braavos |
| stracili | Baratheon KP 13, Dorne 8, Smocza Skala 7, Dorzecze 6, Braavos 3, po 2: Qohor, Pentos, Ibben, Targaryen Aegon, Dothrakowie; reszta po 1 |
| pogranicze KP - Dorzecze | 16 zdobyc, z tego 12 to 6 zamkow przechodzacych z rak do rak (Willow Wood 3x, Stone Hedge, Acorn Hall, Wayfarer's Rest, Pinkmaiden, Deep Den po 2x) |
| doba oblezenia przy decyzji o szturmie | mediana 8.4 doby (4.2-12.7) we wszystkich 113 decyzjach; 8.0 w 53 zdobyciach |
| armia oblegajaca przy decyzji (113) | mediana 13 druzyn (3-32), 2352 ludzi, 208 ludzi na druzyne |
| szturm (54 bitwy "Siege" wygrane przez atak) | atak mediana 2995 ludzi, 16 druzyn (1192-9708); obrona mediana 431 ludzi |
| stosunek atak / obrona w szturmie | mediana 7.4 : 1 (1.5-45.1); ponizej 3 : 1 tylko 7 razy, ponizej 5 : 1 16 razy, 10 : 1 i wiecej 18 razy |
| sila gry przy decyzji (oblegajacy / obroncy) | mediana 5.7 : 1 (113 decyzji); w zdobyciach 8.0 : 1 |
| atak wobec (zaloga + milicja\*) na poczatku oblezenia | mediana 6.1 : 1 (min 1.2) |
| zaloga na poczatku oblezenia | zamki mediana 254 (101-360), miasta 517 (281-625) |
| zaloga na starcie kampanii (d1, te same twierdze) | zamki 125, miasta 328 |
| milicja\* na poczatku oblezenia | zamki 178, miasta 228 |
| zaloga w ostatnim wierszu przed zdobyciem | zamki mediana 92, miasta 253; zaloga 0 w 8 zamkach |
| glod w twierdzy w czasie oblezenia | 0 przypadkow (kolumna glodna_warownia = 0 we wszystkich) [P] T `spichlerze-regiony.csv` |
| szturmy odparte | 0 (`balans-krolestw.csv`, szturmy_odparte = 0 u wszystkich); 18 bitew "Siege" skonczylo sie "bez rozstrzygniecia" |
| oblezenia zakonczone okupem (O) | 26 z 113 decyzji ("wynik okup AI z AI") |

### 3.2 Zaloga ginie w polu przed szturmem (bitwa pod murami)

- W 11 z 57 zdobyc w ciagu 12 dob przed zdobyciem oblegajacy wygrali bitwe typu **SiegeOutside** pod ta sama twierdza; obroncy tracili w niej 256-1843 zabitych, a nastepny szturm szedl na sama milicje (obrona 92-110 ludzi; Qamayl d193 padl bez osobnego szturmu): Fellwood d39, Bloody Gate d115, Demongate d131, Blackmont d148, Qamayl d193, Deep Den d217 i d576-577, Skyreach d272, Vaes Dothrak d427-428, **Krolewska Przystan d530**, Silverhill d611 i d614 [P] `bitwy-pod-murami.tsv`.
- Przyklad Bloody Gate: zaloga 276 do d113, w wierszu d114 zaloga 0 (milicja\* 332 -> 214); bitwa d115 "SiegeOutside pod Bloody Gate: Tristan Ryger (Riverlands, partii 28) 4607 vs Bloody Gate (The Vale, partii 10) 1371 - wygrywa atakujacy", obrona: zabici 691, ranni 682; szturm d116 "4239 vs 94" [P] T `bitwy.log` linie 6704, 6778; T `ludzie-regiony.csv` castle_EN2.
- Skad bitwa w polu przy szturmie - kod gry: gdy do szturmu dolacza po stronie obrony oddzial, ktory nie stoi w twierdzy, gra zmienia typ bitwy ze szturmu na SiegeOutside [K] `dec32\cs_full\TaleWorlds.CampaignSystem.MapEvents\MapEvent.cs:651-654`; atak na oddzial pod oblegana twierdza to tez SiegeOutside [K] `...\Actions\StartBattleAction.cs:65-77`. Czy w SiegeOutside zaloga walczy z premia murow - nie sprawdzone (rozdz. 9).
- Gdy w polu przegrywa **oblegajacy**, nasze reguly N3/H3 robia z 70 % przegranych rozbitych (wracaja): 111 563 ludzi pobitych oblegajacych, zabici + jency 33 540, rozbici 78 023; gdy przegrywa **strona twierdzy** - "jak w grze" (17 bitew pod murami, 2 wypady) [P] A, suma linii "Przegrani pod murami (N3)". W bitwach SiegeOutside z tabeli obrona ma 0 rozbitych (np. Bloody Gate d115: 691 / 682 / 0) [P] `bitwy-pod-murami.tsv`.

### 3.3 Tabela 57 zdobyc

Kolumny: d - doba zdobycia (linia L5b-2); dni obl. - "oblezenie X dob" z ostatniej linii O0 (decyzja gry o szturmie); zaloga d1 - stan na starcie kampanii; start obl. - wiersz z doby przed poczatkiem oblezenia (doba O0 minus dni oblezenia); przed - ostatni wiersz, gdy twierdza nalezala jeszcze do obroncy; bitwa pod murami - SiegeOutside wygrana przez oblegajacych w 12 dobach przed; szturm - ostatnia bitwa "Siege" wygrana przez atak. "-" = brak linii. Harrenhal d128 to szturm fabularny ROT (rozdz. 7.4).

| d | osada | rodzaj | stracil | zdobywca (wodz) | dni obl. przy decyzji | zaloga d1 | zaloga start obl. | milicja* start | zaloga / milicja* przed | bitwa pod murami (SiegeOutside) wygrana przez oblegajacych | szturm: atak ludzi (druzyn) | obrona ludzi | stosunek |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 23 | Cape Wrath | zamek | Smocza Skala | Stormlands (Renly Baratheon) | 10.4 | 124 | 138 | 98 | 102 / 64 | - | 1242 (3) | 206 | 6.0 |
| 40 | Fellwood | zamek | Smocza Skala | Stormlands (Renly Baratheon) | 8.5 | 124 | 138 | 74 | 0 / 115 | d39: obrona 534 (zabici 256) | 2092 (11) | 108 | 19.4 |
| 48 | Starfall | miasto | Dorne | Reach (Arwyn Oakheart) | 6.1 | 328 | 281 | 179 | 161 / 109 | - | 3950 (13) | 432 | 9.1 |
| 88 | Gargos | zamek | Qohor | Volantis (Alios Qhaedar) | 8.3 | 125 | 215 | 216 | 92 / 144 | - | 2620 (14) | 359 | 7.3 |
| 91 | Sweetport | zamek | Smocza Skala | Stormlands (Bryce Caron) | 10.1 | 125 | 300 | 87 | 126 / 48 | - | 4353 (22) | 496 | 8.8 |
| 98 | Sandstone | zamek | Dorne | Reach (Randyll Tarly) | 6.7 | 124 | 260 | 134 | 140 / 74 | - | 4456 (23) | 385 | 11.6 |
| 106 | Ar Noy | zamek | Qohor | Volantis (Alios Qhaedar) | 9.1 | 125 | 251 | 189 | 140 / 138 | - | 2129 (14) | 397 | 5.4 |
| 116 | Bloody Gate | zamek | Dolina | Dorzecze (Tristan Ryger) | 7.6 | 125 | 274 | 327 | 0 / 162 | d115: obrona 1371 (zabici 691) | 4239 (28) | 94 | 45.1 |
| 128 | Harrenhal | miasto | Baratheon KP | Polnoc (Roose Bolton) | - | 335 | - | - | 247 / 330 | - | 9708 (52) | 6545 | 1.5 |
| 128 | Stonedance | zamek | Smocza Skala | Stormlands (Renly Baratheon) | 8.2 | 124 | 330 | 106 | 196 / 77 | - | 2065 (10) | 429 | 4.8 |
| 132 | Demongate | zamek | Targaryen Aegon | Volantis (Toromo Melane) | 8.5 | 125 | 218 | 416 | 0 / 245 | d131: obrona 587 (zabici 301) | 1911 (12) | 92 | 20.8 |
| 145 | Lorath | miasto | Lorath | Braavos (Tormo Fregar) | 10.1 | 423 | 439 | 244 | 179 / 150 | - | 3286 (15) | 639 | 5.1 |
| 149 | Blackmont | zamek | Dorne | Reach (Baelor Hightower) | 8.6 | 124 | 292 | 101 | 0 / 67 | d148: obrona 705 (zabici 303) | 1834 (10) | 106 | 17.3 |
| 175 | Willow Wood | zamek | Dorzecze | Baratheon KP (Roland Crakehall) | 6.4 | 87 | 110 | 66 | 37 / 56 | - | 4469 (24) | 169 | 26.4 |
| 193 | Qamayl | zamek | Ibben | Sarnor (Tomar Sumai) | 11.8 | 86 | 120 | 22 | 120 / 22 | d193: obrona 643 (zabici 354) | - (-) | - | - |
| 218 | Deep Den | zamek | Baratheon KP | Dorzecze (Raymun Darry) | 7.3 | 125 | 267 | 278 | 0 / 142 | d217: obrona 2533 (zabici 926) | 2270 (20) | 102 | 22.3 |
| 219 | Willow Wood | zamek | Baratheon KP | Dorzecze (Jason Mallister) | 5.6 | 87 | 101 | 65 | 63 / 47 | - | 1605 (8) | 172 | 9.3 |
| 222 | Vaes Efe | zamek | Dothrakowie | Targaryen (Dany) (Daenerys Targaryen) | 5.9 | 124 | 244 | 248 | 143 / 158 | - | 3221 (18) | 656 | 4.9 |
| 233 | Sharp Point | miasto | Smocza Skala | Stormlands (Renly Baratheon) | 4.2 | 334 | 533 | 181 | 325 / 113 | - | 3576 (15) | 732 | 4.9 |
| 244 | Kingsgrave | zamek | Dorne | Reach (Lorent Caswell) | 9.5 | 124 | 302 | 144 | 156 / 76 | - | 4263 (20) | 419 | 10.2 |
| 247 | Totava | zamek | Braavos | Antaryon (bunt Braavos) (Ferrego Antaryon) | 9.8 | 130 | 185 | 329 | 49 / 129 | - | 1400 (6) | 506 | 2.8 |
| 273 | Skyreach | zamek | Dorne | Reach (Victaria Bulwer) | 7.5 | 130 | 344 | 143 | 0 / 125 | d272: obrona 1222 (zabici 510) | 2732 (17) | 110 | 24.8 |
| 283 | Iksa Calo | zamek | Braavos | Lorath (Belynno Eranos) | 9.1 | 130 | 276 | 409 | 121 / 243 | - | 1680 (8) | 611 | 2.7 |
| 291 | Sunstone | zamek | Lys | Tyrosh (Racallo Ryndoon) | 9.4 | 130 | 170 | 171 | 46 / 94 | - | 1353 (7) | 264 | 5.1 |
| 299 | Willow Wood | zamek | Dorzecze | Baratheon KP (Tywin Lannister) | 7.2 | 87 | 160 | 64 | 70 / 49 | - | 2249 (9) | 239 | 9.4 |
| 310 | Iksa Keria | zamek | Braavos | Antaryon (bunt Braavos) (Ferrego Antaryon) | 8.4 | 125 | 207 | 274 | 68 / 130 | - | 1881 (7) | 583 | 3.2 |
| 313 | Bolozo | miasto | Pentos | Myr (Morosh Saldorys) | 7.4 | 262 | 596 | 392 | 280 / 190 | - | 2995 (14) | 924 | 3.2 |
| 315 | Pinkmaiden | zamek | Dorzecze | Baratheon KP (Tywin Lannister) | - | 125 | - | - | 71 / 316 | - | - (-) | - | - |
| 324 | Yronwood | miasto | Dorne | Reach (Mace Tyrell) | 7.7 | 323 | 613 | 234 | 372 / 158 | - | 6722 (33) | 1195 | 5.6 |
| 357 | City of Bones | zamek | Qarth | Targaryen (Dany) (Daenerys Targaryen) | 7.4 | 125 | 360 | 644 | 203 / 502 | - | 1982 (9) | 832 | 2.4 |
| 357 | Nightsong | zamek | Stormlands | Reach (Lorent Caswell) | 6.9 | 125 | 320 | 185 | 143 / 86 | - | 4073 (17) | 594 | 6.9 |
| 368 | Wayfarer's Rest | zamek | Dorzecze | Baratheon KP (Roland Crakehall) | 6.4 | 86 | 174 | 75 | 61 / 48 | - | 4770 (22) | 423 | 11.3 |
| 377 | Hellholt | zamek | Dorne | Reach (Mace Tyrell) | 5.7 | 124 | 315 | 93 | 164 / 63 | - | 4915 (23) | 418 | 11.8 |
| 403 | Omber | miasto | Ibben | Sarnor (Saskir Tymai) | 10.0 | 292 | 498 | 108 | 190 / 59 | - | 1806 (8) | 609 | 3.0 |
| 404 | Qamayl | zamek | Sarnor | Ibben (Gorn Joth) | 9.2 | 86 | 147 | 14 | 83 / 9 | - | 1349 (5) | 195 | 6.9 |
| 410 | Acorn Hall | zamek | Dorzecze | Baratheon KP (Terrence Kenning) | 7.1 | 145 | 276 | 417 | 98 / 176 | - | 4384 (22) | 588 | 7.5 |
| 413 | Gelona | zamek | Pentos | Myr (Trego Drahar) | 9.7 | 125 | 336 | 225 | 125 / 90 | - | 2394 (11) | 569 | 4.2 |
| 427 | Stone Hedge | zamek | Dorzecze | Baratheon KP (Harys Swyft) | 8.0 | 124 | 352 | 248 | 91 / 114 | - | 3857 (17) | 544 | 7.1 |
| 428 | Vaes Dothrak | miasto | Dothrakowie | Targaryen (Dany) (Daenerys Targaryen) | 9.1 | 366 | 625 | 688 | 482 / 625 | d427: obrona 1529 (zabici 594); d428: obrona 140 (zabici 63) | 1545 (13) | 94 | 16.4 |
| 429 | Wyl | zamek | Dorne | Reach (Randyll Tarly) | 9.2 | 129 | 335 | 153 | 203 / 81 | - | 2343 (10) | 586 | 4.0 |
| 442 | Stone Hedge | zamek | Baratheon KP | Dorzecze (Raymun Darry) | 7.8 | 124 | 188 | 148 | 61 / 118 | - | 6312 (32) | 288 | 21.9 |
| 468 | Wayfarer's Rest | zamek | Baratheon KP | Dorzecze (Tytos Blackwood) | 4.9 | 86 | 220 | 93 | 84 / 52 | - | 5913 (31) | 585 | 10.1 |
| 488 | Acorn Hall | zamek | Baratheon KP | Dorzecze (Tytos Blackwood) | 7.5 | 145 | 350 | 293 | 116 / 165 | - | 5142 (32) | 599 | 8.6 |
| 530 | King's Landing | miasto | Baratheon KP | Polnoc (Robb Stark) | 8.0 | 436 | 538 | 343 | 271 / 234 | d530: obrona 1117 (zabici 651); d530: obrona 227 (zabici 105) | 2994 (18) | 92 | 32.5 |
| 532 | Pinkmaiden | zamek | Baratheon KP | Dorzecze (William Mooton) | 6.8 | 125 | 213 | 212 | 92 / 142 | - | 2949 (12) | 394 | 7.5 |
| 535 | Tolos | miasto | Targaryen Aegon | Targaryen (Dany) (Daenerys Targaryen) | 9.3 | 319 | 547 | 224 | 350 / 134 | - | 2488 (12) | 873 | 2.8 |
| 541 | Rosby | zamek | Baratheon KP | Polnoc (Robb Stark) | 5.7 | 127 | 258 | 45 | 144 / 34 | - | 3532 (18) | 298 | 11.9 |
| 549 | Stoney Sept | miasto | Baratheon KP | Dorzecze (Quincy Cox) | 6.9 | 319 | 469 | 231 | 170 / 143 | - | 4894 (23) | 899 | 5.4 |
| 559 | Duskendale | miasto | Baratheon KP | Polnoc (Robb Stark) | 8.3 | 334 | 501 | 100 | 230 / 71 | - | 6410 (32) | 1292 | 5.0 |
| 570 | Craster's Keep | zamek | Wolni Ludzie | Nocna Straz (Jeor Mormont) | 9.5 | 125 | 334 | 281 | 100 / 155 | - | 1650 (7) | 585 | 2.8 |
| 578 | Deep Den | zamek | Baratheon KP | Dorzecze (Quincy Cox) | 8.2 | 125 | 337 | 311 | 0 / 232 | d576: obrona 566 (zabici 371); d577: obrona 782 (zabici 315) | 3476 (20) | 96 | 36.2 |
| 612 | Lonely Light | miasto | Zel. Wyspy | Polnoc (Medger Cerwyn) | 9.0 | 181 | 445 | 114 | 253 / 98 | - | 3161 (17) | 500 | 6.3 |
| 615 | Silverhill | zamek | Baratheon KP | Dorzecze (Edmure Tully) | 7.1 | 124 | 304 | 474 | 0 / 194 | d611: obrona 937 (zabici 526); d614: obrona 5062 (zabici 1843) | 2921 (26) | 102 | 28.6 |
| 617 | Harvest Hall | zamek | Smocza Skala | Reach (Branston Cuy) | 6.6 | 181 | 195 | 301 | 97 / 147 | - | 4542 (24) | 457 | 9.9 |
| 618 | Blackhaven | zamek | Smocza Skala | Reach (Phillip Footly) | - | 125 | - | - | 144 / 270 | - | - (-) | - | - |
| 628 | Odivo | zamek | Myr | Drahar (bunt Myr) (Trego Drahar) | 9.7 | 125 | 213 | 151 | 91 / 94 | - | 1192 (5) | 348 | 3.4 |
| 646 | Blackhaven | zamek | Reach | Smocza Skala (Mary Mertyns) | 7.7 | 125 | 240 | 316 | 112 / 166 | - | 3836 (25) | 480 | 8.0 |

## 4. Zalogi wobec gry bez modow

Biegu "bez modow" (ani ROT bez naszych modow) nie ma w logach - porownanie tylko z kodem gry i ze stanem startowym ROT (d1).

| Co | Gra 1.4.8 (kod) | fin30 (pomiar) |
|---|---|---|
| miejsca w zalodze | 200 + 200 w miescie + Leadership + perki + budynki [K] `dec32\...\GameComponents\DefaultPartySizeLimitModel.cs:172-180` | zaloga na poczatku oblezenia: zamki 254, miasta 517 (mediany) - powyzej podstawy gry |
| werbunek zalogi z notabli | najwyzej 1 / dobe [K] `...\DefaultSettlementGarrisonModel.cs:38-43` | K8: 2 / dobe [P] A, linie "Wojsko z ludzi (E4/8)" ("K8: limit 2/d"); [K] `zb\int\Armoury\src\Settings.cs:919`; dobrano ponad 1/d razem 60 152 ludzi (suma linii E4/8); werbunek gry od notabli: rok 1 75 257, rok 2 47 784 [P] A, "Zalogi: przyrost" |
| zaloga na starcie | z ROT / gry | d1: 47 235 (miasta 30 759, zamki 16 476); zdobyte zamki 125, miasta 328 (mediany) |
| szczyt / koniec | - | 92 002 (d361) / 65 611 (d728) |
| ubytek z budzetu rodow | brak takiej reguly w grze (gra: dezercja przy niezaplaconym zoldzie) | zwolnieni z zalog: rok 1 5 357, rok 2 49 186 (rozdz. 2.3) |
| decyzja o szturmie | sila oblegajacych / (sila obroncow x przewaga murow^wykladnik), potem losowanie 10 % wyniku [K] `dec32\...\Siege\BesiegerCamp.cs:375-430` | przy decyzji sila 5.7 : 1 (mediana 113 decyzji); szturm po 8.4 doby (mediana) |
| bitwa pod murami | szturm zmienia sie w SiegeOutside, gdy dolacza obronca spoza murow [K] `MapEvent.cs:651-654` | 11 z 57 zdobyc poprzedzila wygrana SiegeOutside (rozdz. 3.2) |
| przegrani pod murami | gra: zabici / ranni / jency | oblegajacy pobity: 70 % rozbitych (N3); strona twierdzy: jak w grze [P] A "Przegrani pod murami (N3)" |
| glod w oblezeniu | gra: brak zywnosci zabija zaloge | 0 dob glodu w 57 zdobytych twierdzach [P] |
| ostrzal | kazde trafienie machiny w ludzi: 1 (+1 z perkiem Terror) zabity [K] `...\DefaultSiegeEventModel.cs:255-263` | nie mierzone; Krolewska Przystan: zaloga 541 (d523) -> 271 (d529) w czasie oblezenia bez glodu i bez bitwy pod murami tych dob - przyczyna nie zmierzona (rozdz. 9) |

Wniosek z liczb: zalogi zdobytych twierdz byly na poczatku oblezenia **wieksze** niz podstawa miejsc w grze (200 / 400) i dwa razy wieksze niz na starcie ROT [P + K]. Przewaga oblegajacych bierze sie z wielkosci armii (13-16 druzyn), nie z malych zalog. K8 dodal zalogom 60 tys. ludzi ponad tempo gry - bez niego zalogi bylyby mniejsze [S].

## 5. Nasze reguly a sila krolestw

### 5.1 Doplaty z korony i pobor: W5, W5b, W6, W7

[P] T `budzet-rodow.csv`, suma dzienna kolumn doplata_w5, doplata_ze_skarbca_w5b, doplata_z_tarczowego_w6, pobor_wojenny_w7, zold_partii za caly bieg; wyliczenie `doplaty-wobec-zoldu.tsv`.

| krolestwo | zold druzyn (zl) | W5 | W5b | W6 | W7 | razem jako % zoldu druzyn | dni rodow w wojnie |
|---|---|---|---|---|---|---|---|
| Polnoc | 48.1 mln | 5.51 mln | 0.03 | 0.06 | 0.21 | 12.1 % | 99 % |
| Baratheon KP | 34.5 mln | 1.88 | 0.02 | 0.87 | 0.42 | 9.3 % | 99 % |
| Reach | 31.2 mln | 0.83 | 0.09 | 0.02 | 0.01 | 3.0 % | 84 % |
| Dorzecze | 30.2 mln | 0.57 | 0.07 | 0.00 | 0 | 2.1 % | 96 % |
| Dolina | 22.4 mln | 3.43 | 0.42 | 0.01 | 0.01 | 17.3 % | 57 % |
| Smocza Skala | 22.2 mln | 1.94 | 0.11 | 0.07 | 1.37 | 15.7 % | 89 % |
| Targaryen (Dany) | 20.4 mln | 2.71 | 0.76 | 0.02 | 0.06 | 17.4 % | 74 % |
| Stormlands | 18.9 mln | 2.28 | 0.03 | 0.00 | 0 | 12.2 % | 100 % |
| Zelazne Wyspy | 18.3 mln | 5.47 | 0.37 | 0.01 | 3.62 | 51.9 % | 97 % |
| Dorne | 15.8 mln | 2.58 | 0.06 | 0.07 | 0.31 | 19.1 % | 70 % |
| Wolni Ludzie | 8.1 mln | 3.09 | 0.35 | 0.02 | 2.47 | 73.4 % | 100 % |

- Polnoc dostala najwiecej W5 w zlocie (5.51 mln, razem z Zelaznymi Wyspami), ale wzgledem wlasnego zoldu (12 %) mniej niz Dorne (19 %), Dolina, Targaryen czy Wyspy. Doplaty ida do biednych krolestw - to nie one robia Polnoc najsilniejsza [P].

### 5.2 Zloto startowe wedlug ludzi (GL2)

Na starcie skarbce koron, kasy kupcow i kiesy ludu przelano wedlug liczby ludzi krolestwa [P] A, linia "Zloto startowe wedlug ludzi (GL2): dzien 108837" (przed -> po):

| krolestwo | skarbiec korony | kasy kupcow miast |
|---|---|---|
| Reach | 2.00 -> 7.80 mln | 2.03 -> 4.61 mln |
| Baratheon KP | 2.01 -> 4.95 | 2.49 -> 2.81 |
| Polnoc | 2.00 -> 2.93 | 2.72 -> 1.61 |
| Dorzecze | 2.00 -> 2.95 | 2.04 -> 1.60 |
| Dolina | 2.00 -> 2.93 | 1.58 -> 1.65 |
| Stormlands | 2.00 -> 2.44 | 1.47 -> 1.33 |
| Targaryen (Dany) | 2.00 -> 1.96 | 1.06 -> 1.15 |
| Dorne | 2.00 -> 1.46 | 1.77 -> 0.80 |

Renty korony za caly bieg (suma kolumny renty): Reach 25.3 mln, Polnoc 17.6, Dorzecze 16.7, Baratheon KP 16.1, Dolina 14.3, Smocza Skala 9.9, Targaryen 9.5, Stormlands 7.5, Zelazne Wyspy 7.5, Dorne 6.4 [P] T `budzet-rodow.csv`. Reach ma od startu 5.3 razy wiekszy skarbiec niz Dorne, z ktorym walczy.

### 5.3 Pobor i milicja wedlug ludzi (GL4)

Przyrost milicji i jej limit mnozone przez "czynnik osady" z liczby ludzi (0.1-3, srednia swiata 1.0) [K] `zb\int\Armoury\src\PeopleLevy.cs:13-36`. Sredni czynnik milicji krolestwa [P] A, linie "Pobor i milicja od ludzi wedlug krolestw (GL4)" d1 / d729: Reach x2.12 / x1.56, Baratheon KP x1.08 / x1.31, Dolina x1.10 / x1.10, Dorzecze x0.89 / x0.96, Stormlands x0.94 / -, Smocza Skala x0.42 / x0.67, Polnoc x0.58 / x0.52, Dorne x0.45 / x0.44, Zelazne Wyspy x0.37 / x0.37. Skutek w twierdzach - rozdz. 2.1 (milicja\* na twierdze: Reach ok. 3 razy Dorne).

### 5.4 Przewaga Polnocy (175.4b, 10 %)

- Dziala tylko w autobitwie w polu, na sniegu i w lasach Polnocy, dla piechoty Polnocy tier 3-6, "never at sieges" [K] `zb\int\Armoury\src\Settings.cs:1509`, `NorthHomeEdge.cs:16`; w fin30 wlaczona 10 % [P] A, linia "Armie 175 (Armoury) - wylaczniki".
- Za caly bieg: 42 bitwy z przewaga, straty wlasne 361, wroga 703 [P] T `balans-krolestw.csv`, battania d729. Polnoc wygrala 21 z 59 bitew lordow (36 %) [P] tamze. Ta regula nie robi z Polnocy potegi; jej zdobycze to szturmy armii Robba (rozdz. 6.1).

### 5.5 Przegrani w polu (H3) i rozbici (N7)

- Bitwy w polu za caly bieg: przegranych 744 956 ludzi; zabici 151 805 (gra dalaby 399 140), jency 50 932 (gra ok. 343 484), rozbici 542 219 [P] A, suma linii "Przegrani (H3)".
- Rozbici z druzyn lordow: 296 739, z tego pod choragiew 165 651, do domu 79 786, do lasu 51 302 [P] A, suma linii "Rozbici wracaja (N7)".
- Pod murami - rozdz. 3.2: pobity oblegajacy w 70 % rozbity, strona twierdzy jak w grze.
- Wplyw na sile: armie po przegranej w polu wracaja (ok. 56 % rozbitych pod choragiew), a zaloga po przegranej pod murami nie - asymetria jest zapisana w samej linii N3: "poza regula - pobita strona twierdzy (jak w grze)" [P] A, linie "Przegrani pod murami (N3)". Czy to rozstrzyga wojny - nie mierzone [S].


## 6. Gdzie byly armie obroncow

### 6.1 Krolewska Przystan (d530)

| Co | Pomiar |
|---|---|
| wojny Baratheonow KP | z Polnoca (wymuszona przez ROT od d10, pokoj d487 i ta sama doba nowa wojna), z Dorzeczem (ROT, od d131), ze Smocza Skala (od d404) - 3 wrogow [P] A, linie "Wojna i pokoj (E7/0)"; S, "KRONIKA WOJEN" |
| oblezenie | od d516 (spichlerz: oblezona = 1 od wiersza d516), decyzja o szturmie d524: Robb Stark, 18 druzyn, 4484 ludzi; sila 7700 / 1556 [P] A linia 204696 |
| zaloga / milicja\* | d523: 541 / 348; d529: 271 / 234; zywnosc 24 125, bez glodu [P] T `ludzie-regiony.csv`, `spichlerze-regiony.csv`, town_EW4 |
| bitwy d530 | SiegeOutside 4484 vs 1117 (obrona 5 druzyn; zabici obrony 651) -> SiegeOutside 3006 vs 227 -> szturm 2994 vs 92 [P] T `bitwy.log` linie 36830, 36845, 36855 |
| druzyny KP w tym czasie | 6426 ludzi (d529) wobec 10 711 Polnocy; armia Robba = 42 % wojska Polnocy [P] `balans-krolestw.csv` |
| gdzie walczyli lordowie KP d516-d530 | 59 bitew druzyn lordow: region Riverrun 42 (39 wygranych - wsie, karawany i male druzyny Dorzecza), Krolewska Przystan 7 (najwiekszy oddzial Boros Blount, 6 druzyn, 1323 ludzi d519; Joffrey 260 ludzi d525 - przeciw karawanom i bandom), Wayfarer's Rest 3, Acorn Hall 3, Willow Wood 2, Stoney Sept 1, Rosby 1 [P] T `bitwy.log` linie "Bitwa: H3", wyliczenie `gdzie-armie.txt` |
| ROT | wojna KP - Polnoc wymuszana przez ROT (ROTStorylineWars.EnforceWars) [P] A d10, d487 |

Wniosek z liczb: glowne sily KP staly w Dorzeczu i bily male cele; pod Krolewska Przystania nie bylo odsieczy, ktora moglaby przeciwstawic sie 18 druzynom (najwiekszy oddzial KP w regionie: 1323 ludzi d519).

### 6.2 Dorne, gdy Reach je bral (d48-d429)

- Wojna Reach - Dorne wymuszona przez ROT od d23 (pokoj d155 i nowa wojna tej samej doby), pokoj dopiero d437 po 282 dobach; Dorne mialo w tym czasie 1 wroga (od d404 takze Dorzecze, wezwanie do wojny) [P] A, "Wojna i pokoj (E7/0)" d23, d155, d437, d404.
- Wojsko w polu przy kazdym zdobyciu (Reach / Dorne, ludzi w druzynach): Starfall d48 7999 / 6626, Sandstone d98 8558 / 6836, Blackmont d149 7958 / 6490, Kingsgrave d244 10 081 / 6965, Skyreach d273 8914 / 4963, Yronwood d324 10 086 / 4508, Hellholt d377 10 512 / 4591, Wyl d429 7360 / 4907 - przewaga Reach 1.2-2.3 razy [P] `zdobycia-krolestwa-wojny.tsv` (z `balans-krolestw.csv`).
- Odsiecze Dorne byly i wygrywaly: Sandstone d75 (Dorne 43 druzyny, 5744 ludzi, wygrana), Blackmont d106 (31 druzyn, 4497, wygrana), Skyreach d257 (wypad, 31 druzyn, 4399, wygrana); Reach wracal i bral te same twierdze (Sandstone d98, Blackmont d149, Skyreach d273) [P] `bitwy-pod-murami.tsv`.
- W oknach oblezen druzyny lordow Dorne bily glownie bandy w swoich ziemiach malymi oddzialami (najwiekszy oddzial zwykle 85-462 ludzi; wyjatki: Skyreach d262-273 2007, Yronwood d307-324 2085, Wyl d414-429 1050) [P] `gdzie-armie.txt`.
- Pulap i kiesy: rozdz. 2.2 i 5.2 - Dorne ma 41 rodow (najwiecej), ale pulap zoldu 3.2 razy mniejszy niz Reach (d364) i od d364 wypelnia druzyny w 36-42 % (d91: 55 %, d182: 66 %).

## 7. Pytania Jeffa

### 7.1 Jak zniknely Stormlands (d536) bez utraty zamkow w oblezeniach

- To wydarzenie fabularne ROT **RenlyDeathEvent**: popup "Influence of Melisandre" (start d520), po 14 dobach "A Shadow's Deed" [P] S linie 12326, 12629; S "FABULA: ... ostatni start: 520".
- Co robi ROT [K] `robocze\sily\dekompilacja\RenlyDeathEvent.cs`: warunek - co najmniej 120 dob od startu kampanii (:50); po 14 dobach od startu (:89) `ExecuteRenly` (:106): Storm's End i Smocza Skala, jesli Renly'ego, do Stannisa (:112-118), rod Renly'ego zniszczony (:120), Renly zabity, rod Buckler do Westerlands = Baratheon KP (:130), **wszystkie pozostale rody Stormlands przechodza do Smoczej Skaly** (:136), krolestwo Stormlands zniszczone (:145).
- Data wedlug naszego CrashScribe: os fabuly ROT 120 dob x FabulaTimeScale 4.33 = doba 519-520 [K] `zb\int\CrashScribe\src\Fabula.cs:42`, `Config.cs:31`; [P] S "Fabula: wojny fabularne x4.33".
- Skutek [P]: d536 16 twierdz Stormlands do Smoczej Skaly (Storm's End, Cape Wrath, Fellwood, Stonedance, Sweetport - Stannis; Evenfall Hall, Sharp Point - Tarth; Griffin's Roost, Weeping Town, Haystack Hall, Greenstone, Rain House, Mistwood, Blackhaven, Stonehelm, Harvest Hall), Bronzegate (Buckler) do Baratheon KP [P] `zmiany-krolestwa-osad.tsv`. Smocza Skala: 5 -> 21 twierdz, druzyny 4410 -> 5518, zalogi 2468 -> 7054 (d529 -> d546) [P] `balans-krolestw.csv`. Upadek krolestwa: "upadek bez szturmu i bez pokoju (smierc wladcy, bunt, inne)" [P] A linia 209469, "KROLESTWO ZNISZCZONE" linia 209470.
- Te same Stormlands przed d536 same zdobyly 5 twierdz Smoczej Skaly (Cape Wrath, Fellwood, Sweetport, Stonedance, Sharp Point) - wracaja do Stannisa razem z rodami.
- fin21: Stormlands tez 13 -> 0 [P] `Logs\2026-10-13_21-14-13\balans-krolestw.csv`.

### 7.2 Jak Harroway przeszedl do Polnocy

- W tabelach **nie przeszedl**: Harroway (ROT_town4) przez caly bieg fin30 nalezy do Dorzecza, rod Tully (Blackfish) - jedna para (krolestwo, rod) od d1 do d728; tak samo w fin21 [P] T `ludzie-regiony.csv` i `kasy-miast.csv`, ROT_town4; `Logs\2026-10-13_21-14-13\ludzie-regiony.csv`. Brak go w zdobyciach i w zmianach wlasciciela bez oblezenia.
- Do Polnocy w tej okolicy przeszly: Harrenhal (d128, fabula ROT), potem Krolewska Przystan, Rosby, Duskendale (d530-d559) [P].
- Kolory map w plikach: Dorzecze (ROT) 1f527e / c22628, Polnoc (gra) 2D3F1D [P] `ROT-Content\ModuleData\ROT_spkingdoms.xml`, `SandBox\ModuleData\spkingdoms.xml`. Co Jeff zobaczyl na mapie - nie wiem (rozdz. 9).

### 7.3 Kto trzyma Krolewska Przystan na koniec

- **Rod Bolton (Polnoc)** od zdobycia do konca biegu (wiersz d530 ma juz Bolton; d728: zaloga 531, milicja\* 283) [P] T `ludzie-regiony.csv` town_EW4. Rod zdobywcy (Stark) nie zdazyl pojawic sie w wierszu dnia.
- Tak samo wszystkie zdobycze Polnocy (najpierw doba u Starkow, potem Bolton): Harrenhal (Stark d128 -> Bolton d129), Rosby (d540 -> d541), Duskendale (d559 -> d560), Lonely Light (d611 -> d612) [P]. Bolton: 1 twierdza na starcie -> 6 na koniec; jego zalogi 385 -> 2914 ludzi, zold zalog 1210 -> 10 761 zl/d, kiesa 1.13 mln (d565) -> 171 tys. (d728) [P] T `budzet-rodow.csv`, rod Bolton.
- Kto przydziela: decyzja krolestwa o lennie (SettlementClaimantDecision) z latka BK - kandydaci = rody z prawem do lenna, ocena = BK `CalculateHeroFiefScore`, poparcie zmniejszane, gdy rod ma ponad limit domeny [K] `bkfull\BannerKings.Patches.Government\FiefOwnerPatches.cs:18-59` (kandydaci, ocena) i `:70` (poparcie). Dlaczego za kazdym razem Bolton - nie liczone (rozdz. 9).
- Widok Jeffa (herb Boltonow nad Krolewska Przystania) zgadza sie z danymi [P].

### 7.4 Harrenhal d128

Szturm fabularny ROT: popupy "Roose Bolton Strikes a Deal", "Roose Bolton Besieges Harrenhal", "A Northern Stronghold in the South" [P] S linie 3591, 4326, 4541. Bitwa: Roose Bolton 52 druzyny, 9708 ludzi vs Harrenhal 5 druzyn, 6545 ludzi - jedyny szturm ponizej 2 : 1 (1.5 : 1) [P] T `bitwy.log` linia 7453.


## 8. Bunty i hrabstwa (nowe krolestwa)

### 8.1 Lista

Pierwsza i ostatnia doba nazwy krolestwa [P] T `budzet-rodow.csv`; przejscia twierdz bez oblezenia [P] `zmiany-krolestwa-osad.tsv` (z `ludzie-regiony.csv`); wojna "CausedByKingdomCreation" [P] A, "Wojna i pokoj (E7/0)"; upadki [P] A, "Skarbiec upadlego krolestwa (G14)".

| d | nowe krolestwo | skad | jak (rody i twierdze) | koniec |
|---|---|---|---|---|
| 205 | Antaryon League (Braavos Rebels) | Braavos | wojna domowa Diplomacy (S linia 5947); Antaryon i Celeris z Braavos, Lorath, Rhyos bez oblezenia; potem szturmy Totava d247, Iksa Keria d310 | trwa; Braavos zniszczone d310 (A linia 120720) |
| 428 | Confederation of Phassar (Lorath Rebels) | Lorath | wojna domowa Diplomacy (S 10450); Phassar z Tolero Aeksio | trwa |
| 441 | Kingdom of Volantis | Volantis | Maegyr, Paenymion, Vhassar z 6 twierdzami (Volantis, Selhorys, Valysar, Ar Noy, Demongate, Gargos) | d534 "upadek przy stole pokoju", twierdze wracaja (A 208687) |
| 451 | County of Rhyos | Antaryon League | Celeris z Rhyos | trwa |
| 452 | Barony of Bisa | Lorath | Eranos z Bisa | d549 pokoj, wraca (A 214619) |
| 454 | Kingdom of Norvos | Norvos | Quarro, Hortyr z Norvos i Menetragos | trwa |
| 493 | County of Pentos | Pentos | Mopatis z Pentos | d652 pokoj, wraca (A 255518) |
| 518 | County of Myrth | Myr | Saldorys, Alayris z Myrth, Bolozo, Chroyane, Gelona | trwa |
| 540 | Barony of Ulentor | Norvos | Kasgos, Tavellen z Ulentor, Sambandos | d635 pokoj (A 248745) |
| 543 | County of Norvos | Kingdom of Norvos | Quarro z Norvos | d678 wraca (A 265901) |
| 596 | Barony of Ghoyan Drohe | Pentos | Moesia | d687 pokoj, wraca (A 269475) |
| 609 | Drahar Conspiracy (Myr Rebels) | Myr | wojna domowa Diplomacy (S 14065); Drahar z miastem Myr; szturm Odivo d628 | trwa |
| 621 | Barony of Sambandos | Barony of Ulentor | Tavellen z Sambandos | trwa |
| 628 | Barony of Ny Sar | Norvos | Golathis z Ny Sar | trwa |
| 641 | County of Draconys | Qohor | Adory z Draconys | trwa |
| 673 | Qar Deeth | Qarth | Qar Deeth z Qarkash | trwa |
| 691 | Barony of Tolerrol | Myr | Terys z Tolerrol | trwa |
| 698 | Rakh Jhattar of Vaes Diaf | Dothrakowie | Jhago z Vaes Diaf | trwa |
| 702 | County of Bolozo | County of Myrth | Alayris z Bolozo, Chroyane, Gelona | trwa |

- Razem 19 nowych krolestw: 1 w roku 1, 18 w roku 2; 13 trwa na koniec biegu, 6 wrocilo pokojem. Wszystkie w Essos (Wolne Miasta, Qarth, Dothrakowie) - w Westeros zadne [P].
- Norvos zniknelo d635 bez szturmu i bez pokoju - jego rody odeszly z lennami (d454, d540, d628) [P] A linia 248748.
- Dodatkowo 2 bunty Diplomacy skonczone abdykacja wladcy ("Albersan League of Myr", "Lohar Conspiracy of Lys") [P] S linie 9163-9164.
- Przy kazdym odejsciu rod zostaje wlascicielem tej samej twierdzy (kolumna rod bez zmian), zmienia sie tylko krolestwo - to nie bunt chlopski ani zdobycie [P] `zmiany-krolestwa-osad.tsv`.

### 8.2 Mechanizm w kodzie i w ustawieniach

- BK: rod z lennami bez krolestwa codziennie zamieniany w nowe krolestwo o nazwie z tytulu ("County of X", "Barony of X") [K] `bkfull\BannerKings.Behaviours\BKClanBehavior.cs:366-368` (dzienny przebieg rodu) -> `:454-465` (`MakeRebelKingdom`: `clan.Kingdom == null`, `clan.Settlements.Count > 0` -> `KingdomManager.CreateKingdom`); takze po rozwiazaniu buntu w osadzie `:70-81`.
- Diplomacy [U] `Configs\ModSettings\Global\Diplomacy\DiplomacySettings_v1.2.json`: DailyChanceToStartRebelFaction 0.05, DailyChanceToJoinRebelFaction 0.1, DailyChanceToStartCivilWar 0.1, MinimumTimeSinceLastCivilWarInDays 1000, MaximumFactionDurationInDays 520. To szanse **na dobe** - w roku 364-dniowym losowan jest 4.33 razy wiecej na rok niz przy tych samych liczbach w roku 84-dniowym [S]; zmeczenie wojna Diplomacy juz poprawiono /4.33 (STAN-PRAC 14.10 13:01), tych szans nie [U].
- Kiesy odchodzacych rodow dzien przed odejsciem: 15 z 21 rodow ma kiese ponad rezerwe R (np. Terys 1.45 mln, Qar Deeth 1.48 mln, Jhago 1.32 mln); 6 ponizej R, z tego 4 z dlugiem w Banku (Vhassar, Quarro, Kasgos, Golathis) [P] T `budzet-rodow.csv`. Bieda nie jest glowna przyczyna odejsc.
- Dlaczego rody odchodza (vanilla "opuszczenie krolestwa", Diplomacy secesja czy cos innego) i czemu dopiero w roku 2 - log tego nie zapisuje (rozdz. 9).

## 9. Czego nie wiem

1. **Przyczyna odejsc rodow z lennami w roku 2** (rozdz. 8) - zadna linia logu nie podaje powodu; stanu frakcji buntu Diplomacy nie logujemy.
2. **Dlaczego lenna Polnocy zawsze dostaje Bolton** - wynik BK `CalculateHeroFiefScore` i glosy nie sa w logu.
3. **Co zabralo zaloge Krolewskiej Przystani d524-d529** (541 -> 271 bez glodu i bez bitwy pod murami tych dob): ostrzal (`DefaultSiegeEventModel.cs:255-263`), patrole BK, lordowie - nie zmierzone. To samo dla innych twierdz, gdzie zaloga spadala przed bitwa (Skyreach 340 -> 289 -> 184 -> 0 w d268-d272).
4. **Czy w bitwie SiegeOutside zaloga ma premie murow** - nie sprawdzilem modelu symulacji bitwy dla tego typu.
5. **Bieg bez naszych modow** - nie ma; porownania "wobec gry" sa tylko z kodu gry i ze stanu startowego ROT (d1).
6. **Co Jeff zobaczyl przy Harroway** - dane mowia: Dorzecze caly bieg.
7. **18 szturmow "bez rozstrzygniecia"** i **26 oblezen zakonczonych okupem** - to sprawa rozpoznania czasu oblezen, tu tylko policzone.
8. **Czy asymetria przegranych (oblegajacy wraca, zaloga ginie) rozstrzyga wojny** - liczby sa, wplyw na wynik wojny nie zmierzony.
9. **Dlaczego Dorne nie zbiera armii** (druzyny bija bandy osobno) - tworzenie armii AI nie jest logowane.
10. **Liczba rodzin i druzyn z ROT** (ile rodow, jaki limit druzyn na rod) jako zrodlo przewagi Polnocy - limity zmierzone (rozdz. 2.2), skad ROT bierze ich wielkosc (BK / ROT model wielkosci druzyny) nie sprawdzilem.

## 10. Pliki posrednie (`docs\oblezenia\robocze\sily\`)

| Plik | Co |
|---|---|
| `zdobycia-fin30.tsv` | 57 zdobyc z linii L5b-2 (numer linii A, doba - 1, osada, zdobywca) |
| `okup-O0-fin30.tsv` | 113 decyzji o szturmie (linie O0): druzyny, ludzie, doby oblezenia, sily, wynik |
| `zdobycia-szczegoly.tsv` | 57 zdobyc: zaloga i milicja\* d1 / start / przed, szturm, O0 |
| `zdobycia-krolestwa-wojny.tsv` | przy kazdym zdobyciu: ludzie w druzynach obu krolestw, liczba wrogow |
| `bitwy-pod-murami.tsv` | 142 bitwy Siege / SiegeOutside / SallyOut z `bitwy.log` |
| `zmiany-krolestwa-osad.tsv` | 120 zmian krolestwa twierdz, z oznaczeniem "oblezenie / BEZ oblezenia" |
| `sily-krolestw.tsv`, `regiony-krolestwa.tsv`, `balans-wybrane-dni.tsv` | rozdz. 2.1 |
| `budzet-krolestwa.tsv`, `doplaty-sumy.tsv`, `doplaty-wobec-zoldu.tsv` | rozdz. 2.2, 5.1 |
| `zalogi-dzienne.txt` | zalogi i milicja swiata co 30 dob |
| `gdzie-armie.txt` | bitwy druzyn KP i Dorne wedlug regionow w oknach oblezen |
| `tagi-fin30.txt` | spis rodzajow linii logu A |
| `dekompilacja\RenlyDeathEvent.cs` | ROT.dll, zdarzenie smierci Renly'ego |
| `tabela-zdobycia.md`, `tabela-krolestwa.md`, `czesc*.md` | kawalki tego dokumentu |

Skrypty liczace (python -I): scratchpad sesji `...\c36b73ca-...\scratchpad\py\` (zdobycia.py, okup.py, krol.py, balans.py, budzet.py, zdob_tabela2.py, wojny.py, gdzie.py, zmiany.py).
