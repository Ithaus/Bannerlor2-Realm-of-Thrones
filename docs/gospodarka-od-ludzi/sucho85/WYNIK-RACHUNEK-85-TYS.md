# SUCHO 85 - SYNTEZA na zmierzonym biegu rocznym kodu 43 (13.10)

Rachunek na papierze: 4 modele (rody, krolestwa, pobor, garnek) przeliczone na biegu rocznym kodu, ktory jest w grze
(`D:\Backup-Bannerlord\logs\2026-10-14-noc\Armoury-2026-10-13_15-02-59.log`, 364/364 dob, 0 ERROR). Nic w grze, kodzie moda, repo ani w folderze gry nie zmienione; bez commita.
Miara wszedzie ta sama co prog Jeffa: wojsko rodow z linii `Skarbce:` krolestw w WOJNIE, srednia dob 337-364 (`sprawdz_logi.py` W5-7). [P] = pomiar, [S] = szacunek. Liczby w tys. ludzi.

## 0. Dla Jeffa - prosto

1. Kod, ktory masz w grze, skonczyl rok z **101 tys.** wojska lordow w wojnie - 16 tys. ponad prog. Wczoraj liczylismy ok. 92.
2. Pomylka wziela sie glownie z daniny: lud odkladal przez caly rok, a korony braly z tych oszczednosci coraz wiecej - na koniec roku **230 tys. zl dziennie**, 2 razy tyle co w dobie 120. Wiekszosc tej daniny idzie do panow jako renty i placi zold.
3. Pierwszy krok reguly "gospodarka od ludzi" (S1): prawie bez zmian - **ok. 101 tys.**
4. Pelna regula (S2, granica): **ok. 90 tys.** (od 86 do 98). Prog trzyma, ale zapas topnieje z 16 do 5 tys.
5. Dlaczego tyle traci: po regule danina tez idzie za ludzmi - ucieka z pustych krain (Polnoc, Dorne, Zelazne Wyspy, Meereen, Ibben), gdzie dzis placi zold, do ludnych (Reach, Qarth, Braavos, Pentos), gdzie panowie maja juz pieniadze, a ludzi i tak wiecej nie zwerbuja.
6. Nowa kampania (S3: zloto na start wedlug ludzi, skarbce koron od 1 mln): **ok. 93 tys.** (88-99).
7. Pobor wedlug ludzi (P4) sumy prawie nie rusza (od -1 do 0 tys.), tylko przestawia: Dorne, Polnoc, Smocza Skala, Sarnor, Ibben traca po 0.3-0.9 tys., Reach zyskuje 1.1 tys.
8. "Rozbici wracaja pod choragiew" (N7) wedlug recenzji doda 4-10 tys.; w pelnej regule raczej 3-8, bo wiecej panow nie ma juz na zold. Z N7 pelna regula daje ok. **95 tys.**
9. Traca w pelnej regule: Polnoc -3.6 tys., Zelazne Wyspy -3.6, Dorne -1.9, Wolni Ludzie -1.3, Ibben -1.2, Dothrakowie -0.8, Smocza Skala -0.7, Dolina -0.7.
10. Zyskuja: Pentos +1.3, Reach +1.2, Braavos +0.8, Norvos +0.4 - malo, bo tam brakuje chetnych, nie zlota.
11. Zelazne Wyspy i Wolni Ludzie w pelnej regule zostaja prawie bez wojska w polu (1.4 i 0.3 tys.) - caly ich pieniadz zjadaja zalogi.
12. Czego nie wiem: (a) to jeden bieg - inna kampania moze dac 5 tys. wiecej albo mniej (ta byla w dobach 93-120 o ok. 4.5 tys. ponad srednia podobnych biegow); (b) ile daniny zbierze swiat w pelnej regule (100 czy 230 tys. dziennie) - to robi wiekszosc widelek; (c) jak blisko pelnej reguly zacznie nowa kampania; (d) N7 jeszcze nie zmierzony.
13. Wniosek: srodek kazdego scenariusza jest ponad 85 tys. Ryzyko jest tylko w pelnej regule bez N7: dolny koniec (86, z pechowym poborem 83) albo pechowa kampania moga zejsc pod prog. Z N7 zapas ok. 10 tys.
14. **KRYTYKA (rozdz. 7) - poprawia punkty 8 i 13:** (a) N7 policzony za wysoko - recenzja liczyla go przy luzie w zoldzie 33 tys. ludzi (doby 93-120), na koniec roku luzu jest 23 tys., wiec raczej **+3 do +7 tys.** (srodek 5), nie +4 do +10. (b) Ten rok nie jest stanem stalym: lud od doby 250 oddaje w daninie wiecej, niz odklada (kiesy ludu spadaja), 120 biednych rodow (21.5 tys. ludzi) wydaje ponad dochod, a zapas skarbcow koron sie konczy - w roku 2-3 **wszystko schodzi o ok. 6 tys.** (pesymistycznie 8-10). (c) Pelna regula (S2) w dlugiej kampanii: srodek **ok. 87 tys. z N7** (84 bez N7) - na samym progu; **najgorszy realny przypadek ok. 73 tys.** Kod dzisiejszy (S0) i pierwszy krok (S1) trzymaja prog takze w roku 2-3 (srodek 98-100 z N7). Wniosek "swiat utrzyma 85 tys." stoi dla S0, S1 i roku 1; dla pelnej reguly w dluzszej kampanii - NIE stoi pewnie.

## 1. Tabela - swiat, koniec roku (miara progu, tys.)

| scenariusz | nisko | srodek | wysoko | + P4 (srodek; zmiana P4 nisko..wysoko) | + P4 + N7 (srodek; widelki) | wobec 85 (srodek: bez N7 / z P4+N7) |
|---|---:|---:|---:|---|---|---|
| S0 kod 43 (pomiar) | - | **101.3** [P] | - | 100.2 (-4.6..+0.2) | **107.2** (100.7-111.5) | +16.3 / +22.2 |
| S1 pierwszy krok | 99.5 | **100.9** | 101.6 | 99.8 (-4.3..+0.6) | **106.8** (99.2-112.2) | +15.9 / +21.8 |
| S2 granica | 85.7 | **90.4** | 97.5 | 90.5 (-2.4..+1.5) | **95.5** (86.3-107.0) | +5.4 / +10.5 |
| S3 nowa kampania, 3/4 drogi (zloto startowe wedlug ludzi, skarbce z podloga 1.0 mln) | 87.9 | **93.3** | 99.1 | 93.2 (-2.4..+1.4) | **98.2** (88.5-108.5) | +8.3 / +13.2 |
| S3 przy 1/2 drogi / calej drodze | | ok. 96 / ok. 90.5 | | | | |
| wiersz N7 (rozbici wracaja) | +4 | +7 | +10 | w S0/S1; w S2/S3 +3 / +5 / +8 [S] | | |

Do kazdej liczby los kampanii +-5 tys. (nie w widelkach). Srodek = srednia 4 modeli (rozdz. 4); nisko / wysoko = najnizszy / najwyzszy wariant z modeli.
**KRYTYKA (rozdz. 7):** wiersz N7 za wysoki - poprawiony +3 / +5 / +7 w kazdym scenariuszu roku 1 (kolumna "+ P4 + N7": S0 105.2 (99.7-108.5), S1 104.8 (98.2-109.2); S2 i S3 srodek bez zmian, gorny koniec o 0.5-1 nizej); tabela dotyczy KONCA ROKU 1 - w roku 2-3 kazdy wiersz schodzi o ok. 6 tys. (tabela 7.4).
Wczoraj (baza W37 + szacowany pieniadz kodu 43): S0 92, S1 91.7-92.5, S2 85.9-87.7 (garnek 84-94), S3 87.7-90.8. Dzis wszystko wyzej, ale S2 tylko o 3-4 tys. (S0 o 9): dodatkowy pieniadz to danina, a ta po regule tez przechodzi do ludnych krain.

## 2. Tabela - 29 krolestw (miara progu w ludziach; srodek [nisko-wysoko])

| krolestwo | S0 [P] | S1 | S2 | S3 3/4 | S2 - S0 | P4 w S1 / S2 |
|---|---:|---:|---|---|---:|---|
| The North | 12 626 | 12 356 | 8 979 [7 793-10 112] | 9 788 [7 793-10 759] | -3 647 | -683 / -361 |
| The Vale | 8 321 | 8 186 | 7 611 [7 173-8 155] | 7 773 [7 173-8 162] | -710 | +147 / +117 |
| King's Landing | 8 017 | 8 007 | 7 881 [7 442-8 122] | 7 978 [7 806-8 106] | -136 | +133 / +147 |
| Stormlands | 7 498 | 7 410 | 7 502 [6 853-8 134] | 7 452 [6 853-7 969] | +4 | -38 / -35 |
| The Reach | 7 436 | 7 554 | 8 617 [7 436-10 612] | 8 361 [7 436-9 911] | +1 181 | +1 095 / +1 096 |
| Riverlands | 6 871 | 6 845 | 7 008 [6 393-7 732] | 6 981 [6 525-7 516] | +137 | +65 / +63 |
| Dorne | 6 673 | 6 570 | 4 818 [3 461-5 741] | 5 369 [4 754-5 974] | -1 855 | -852 / -512 |
| Iron Islands | 4 991 | 4 776 | 1 407 [338-1 862] | 1 906 [1 047-2 490] | -3 584 | -177 / -14 |
| Nights Watch (wyjatek) | 4 348 | 4 348 | 4 348 | 4 348 | 0 | 0 / 0 |
| Dothraki Horde | 3 953 | 3 874 | 3 165 [2 847-3 427] | 3 364 [3 188-3 535] | -788 | +385 / +286 |
| Qohor | 3 945 | 3 976 | 3 967 [3 944-4 031] | 3 972 [3 945-4 019] | +22 | +47 / +49 |
| Dragonstone | 3 500 | 3 545 | 2 788 [2 565-2 984] | 2 907 [2 831-2 999] | -712 | -430 / -379 |
| Targaryen (Meereen) | 3 202 | 3 004 | 2 790 [2 249-3 172] | 2 898 [2 482-3 208] | -412 | -25 / -25 |
| Qarth | 2 797 | 2 910 | 2 984 [2 932-3 011] | 2 984 [2 932-3 011] | +187 | +160 / +128 |
| Myr | 2 367 | 2 366 | 2 339 [2 296-2 361] | 2 347 [2 315-2 367] | -28 | -131 / -132 |
| Ibben | 2 166 | 2 163 | 940 [570-1 216] | 1 249 [995-1 458] | -1 226 | -302 / -281 |
| Braavos | 2 145 | 2 368 | 2 957 [2 530-3 983] | 2 913 [2 476-3 983] | +812 | +129 / +239 |
| Norvos | 2 126 | 2 095 | 2 523 [2 371-2 628] | 2 460 [2 307-2 561] | +397 | +13 / +26 |
| Free Folk | 1 656 | 1 601 | 346 [0-655] | 589 [128-850] | -1 310 | 0 / 0 |
| Lorath | 1 518 | 1 602 | 1 472 [1 037-1 910] | 1 572 [1 153-1 944] | -46 | -51 / -36 |
| Sarnor (0.25 mln ludzi) | 1 464 | 1 424 | 1 174 [773-1 464] | 1 249 [938-1 464] | -290 | -338 / -123 |
| Tyrosh | 1 206 | 1 227 | 1 350 [1 206-1 524] | 1 338 [1 206-1 501] | +144 | -61 / -78 |
| Lys | 1 160 | 1 168 | 1 204 [1 141-1 242] | 1 200 [1 156-1 226] | +44 | -88 / -83 |
| Skagos | 798 | 745 | 475 [309-589] | 530 [424-589] | -323 | -102 / -60 |
| Pentos | 515 | 765 | 1 778 [575-2 263] | 1 768 [562-2 463] | +1 263 | +4 / +17 |
| Volantis | pokoj (druzyny 1 285, poza miara) | | gdyby w wojnie: zyskuje (wczoraj +1.4-1.9 tys. w S2) | | | |
| Yi Ti Exiles | pokoj (321) | | | | | |
| Summer Isles | pokoj (180) | | | | | |
| Ormollen Empire | pokoj (152) | | | | | |
| **SWIAT (25 w wojnie)** | **101 297** | **100 886** | **90 424** | **93 298** | **-10 873** | **-1 102 / +50** |

(Targaryen (Aegon) w tym biegu zniknal - 0 rodow, 0 wojska; w W37 mial 1.8 tys. w mierze.) Tabela krolestw zaklada mape wojen jak w biegu (25 w wojnie).
Pelne liczby i rozbicie na modele: `s2_synteza.out.txt`, `s2_synteza.json`.

## 3. Dlaczego S0 zmierzone (101.3) jest wyzsze od przewidywan (ok. 92)

Porownanie konca roku: W37 (stary kod, 79.6) vs W43 rok (kod w grze, 101.3) [P] - rody w wojnie, zl/d (`s1_porownaj_rody.out.txt`, `garnek\g01_W43rok.out.txt`, `garnek\g02_W43rok.out.txt`, `rody\r2_w43rok.out.txt`, linie dnia 364):

| | W37 d337-364 | W43 d93-120 | W43 rok d337-364 |
|---|---:|---:|---:|
| danina P1 (z oszczednosci ludu) | 0 | 113 tys. | **230 tys.** (dzien 364: 240 tys.) |
| wplywy dnia koron | 450 tys. | 561 tys. | **742 tys.** |
| renty 180 (linie koron; CSV rodow) | 172 tys. (CSV 168) | 287 tys. | **340 tys.** (CSV 326, x1.93) |
| zwrot zoldu 70% | 277 tys. | 297 tys. | 325 tys. |
| W5 / W5b / W6 wyplacone | 0 / 0 / 0 | 24 / 14 / 0.1 tys. | **57 / 8 / 3 tys.** |
| D staly rodow | 630 tys. | | 869 tys. (+239) |
| kiesy rodzin G / nadwyzka ponad rezerwe G-R | 49.9 / 20.3 mln | | 60.1 / **25.6 mln** (+26%) |
| pulap rodow: 0.6 D f / skrzynia / dwor+kredyt / W5 | 366 / 448 / 124 / 0 tys. | | 509 / 563 / 180 / 75 tys. |
| pulap razem / zold druzyn | 938 / 437 tys. | | 1 327 / 594 tys. |
| zold na czlowieka druzyn | 5.76 zl | - | 6.04 zl (+5%) |
| zwolnieni z pulapu na dobe | 760 | | 371 |
| skarbce koron / dlug Banku | 27.1 / 1.82 mln | 44.4 / 0.05 mln | 23.5 / 0.76 mln |
| krolestw w wojnie | 26 | | 25 |
| miara progu | 79.4 | 109.0 (ten bieg 111.8) | **101.3** |

Danina po dobach (g02): d113-140 110, d169-196 127, d225-252 176, d253-280 227, d281-308 239, d309-336 242, d337-364 230 tys. zl/d - rosla do ok. doby 300. Wojsko: d85-112 114.6, potem 105-109 az do doby 336, na koniec 101.4. W W37 spadalo caly czas (95.4 -> 79.4, -17%); tu -9% od doby 93-120.

Co modele niedoszacowaly:
1. **Danina rosla dalej - glowna przyczyna (+6 do +9 tys. z ok. 9).** Modele braly danine z dob 93-120 (rody: 0.5-1.0 x 112 tys.; krolestwa: pieniadz koron X 110-180 tys. ponad W37; garnek: 0.72 nadwyzki ludu = 108 tys.). Naprawde: 230 tys. danina, X = 284 tys. (renty + zwrot + W5/W5b/W6 ponad W37). Te same modele z prawdziwym pieniadzem: rody S0 99.2-100.8 (`rody\r4_w37_theta206.out.txt`, theta 2.06), krolestwa 98.7 (92.2-101.5) (`krolestwa\k2_w37_Xzmierzone.out.txt`). Lud mial z czego: kiesy ludu miast 17.6 mln (d85-112) -> 25.8 mln (d225-252); stawka s doszla do pulapu 3% w 17 z 25 krolestw w wojnie (linia 184), a regula "brak wojska u panow" podnosila stawke w 2 krolestwach.
2. **Danina poszla glownie w renty, nie w W5**: dzien 364 - garnek 217 tys., potrzeba rodow 126 tys., wyplacono W5 58 tys. (43 rody), **reszta do rent 159 tys.** (linia W5). Renty podniosly D rodow (+239 tys.) i kiesy rodzin.
3. **Skrzynie rosly**: G-R +5.3 mln (+26%) -> skrzynia w pulapie 448 -> 563 tys. zl/d. Modele trzymaly G stale albo G-R stale - tu ok. +1-2 tys. ludzi niedoszacowania [S].
4. **W5b - nie**: spadl (14 -> 8 tys. zl/d), bo skarbce stopnialy (44.4 -> 23.5 mln; 14 krolestw z nadwyzka ponad zapas wojny, pula 9 tys. zl/d). Rody liczyly W5b 10-14 tys. - przeszacowane, ale W5 (57 tys.) wyszlo wyzej, niz zakladaly; celowanej doplaty razem 68 tys. wobec ok. 40-50 tys. w modelach.
5. **Wypelnienie rodow z luzem wyzsze**: 75.6% limitu (W37 70%), krzywa y = min(0.98 x, 0.72) (W37 0.94 / 0.71); Polnoc trzyma 86% limitu (`rody\r3_w43rok.out.txt`, `krolestwa\k1_w43rok.out.txt`). "Naturalne wypelnienie" nie jest stale - z pieniadzem i W5 (miejsce na werbunek) rosnie, mniej zwolnien (760 -> 371/d).
6. **Wojny - przeciw**: Volantis w pokoju (w W37 2.5 tys. w mierze) i Targaryen (Aegon) zniszczony (1.8 tys.) wypadly, Skagos weszlo (+0.8). Modele z prawdziwym pieniadzem na mapie W37 licza ok. 4 tys. z krolestw, ktorych tu nie ma w mierze - czyli przy tej mapie dawalyby ok. 95-97.
7. **Reszta ok. +4-6 tys.** to los kampanii (ten bieg mial w dobach 93-120 111.8 tys. wobec 107.2 sredniej 4 biegow 120 dob z W5) i punkty 3 i 5. Wygrani wojen urosli: Polnoc (D 117 -> 171 tys. zl/d), Qohor (20 -> 54), Zelazne Wyspy (32 -> 44); przegrali male Essos: Tyrosh, Pentos, Lorath, Lys.
8. Zolnierz drozszy tylko o 5% wobec W37 (5.76 -> 6.04 zl) - to nie jest przyczyna.

## 4. Metoda

**Kazdy model przeliczony na nowym biegu (S0 = pomiar)** - kopie skryptow w `synteza\<model>\`, polecenia z ich RAPORT.md, log i tabele nowego biegu, okno 28, do doby 364:
- rody: r1 (miara 101 297, zgodna z CSV 0.2%), r2 (wzor pulapu zgodny w 97.9% dob-rodow), r3 (krzywa min(0.98 x, 0.72); model -1.2..+2.4% swiata), r4 -> S1 101.3, S2 96.5 (95.4-100.3), S3 w=0.5 100.2, w=0.75 98.7 - **z danina stala** (r4 przy bazie z P1 trzyma danine krolestw jak w biegu).
- krolestwa: k1 (krzywa phi 0.67 n 4.0; 13 krolestw z luzem trzyma 68.6 z 98.7 tys.), k2 (wypisuje S0 zmierzone 101.4 i baze bez dodatku), k3 -> S1 101.1, S2 = S2b 98.2 (97.9-100.1), S3 3/4 99.1 - przy X = 0 S2 nie przesuwa daniny.
- garnek: g01 (101.4 [P]), g02 (przebieg), g03/g05 z podloga 1.0 mln -> model S0 101.3-104.2, S1 99.5-103.6, S2 88.2-90.7, S3 87.9-93.3 (danina S2 98 tys. zl/d - wedlug ludzi), g04.
- pobor: p1 (hamulec wolnych rak: odmowy 454/d - Sarnor 336, nowe: Smocza Skala 65, Ibben 50), p2 (sarnor 1.0 i 2.5) -> P4: S0 -1.5..-4.6, S1 -1.2..-4.3, S2/S3 -0.3..-2.4.
- Zaden skrypt sie nie wywrocil. Dopisane w kopiach (opis): `rody\r4_scenariusze.py` - opcja `--danina-ludzie` (w S2/S3 danina krolestw w wojnie wedlug ludzi, suma swiata podana albo -1 = jak w biegu; w S3 czesc drogi w) i zapis `r4_krolestwa_*.json`; `krolestwa\k3_scenariusze.py` - zapis `k3_krolestwa_*.json` (krolestwa dla S0-S3, S2b, P4). Oryginal r4 przed opcja: `rody\r4_scenariusze_przed_dl.py`.

**Dodatkowe przebiegi** (do rozdz. 3 i do poprawki daniny): rody r4 na W37 z theta 1.0/2.06/2.5; krolestwa k2+k3 na W37 z `--x 284 284 284 --tau 0.24 0.24` (pieniadz koron zmierzony) -> S0 99.1, S1 99.0, S2 90.8 (danina wedlug ludzi), S2b 95.8, S3 3/4 93.2; rody r4 na nowej bazie z `--danina-ludzie -1` (225 tys.) -> S2 90.8, S3 3/4 95.3 i `--danina-ludzie 100000` (jak garnek) -> S2 85.7, S3 3/4 91.2.

**Synteza** (`s2_synteza.py`): na krolestwo pomiar + srednia zmian 4 modeli: A rody (danina wedlug ludzi, srednia 225 i 100 tys.), B krolestwa nowa baza + poprawka "danina wedlug ludzi" (srednia: poprawka rodow i 0.81 x (S2 - S2b) krolestw z X zmierzonym; 0.81 = danina / X), C krolestwa W37 + X zmierzone, D garnek (C i D przeliczone proporcja do pomiaru, nie roznica; nie ponizej 0). Zmiany swiata wedlug modeli: S1 A 0.0 / B -0.3 / C -0.6 / D -0.7; S2 -13.0 / -9.4 / -9.3 / -11.4; S3 3/4 -8.0 / -6.8 / -6.8 / -10.2 tys. Widelki swiata: S1 od garnka (nisko) do rodow (wysoko); S2 od rodow z danina 100 tys. i G z rezerwa (85.7) do rodow z danina 225 tys. i G stalym (97.5); S3 od garnka (87.9) do rodow z danina 225 tys. (99.1).
**P4**: srednia na krolestwo z rodow (beta 0.45), krolestw (eta 0.3) i poboru (srodek nisko-wysoko); widelki swiata z czterech modeli (garnek: S0 -1.3, S1 -1.8, S2/S3 -0.7).
**N7**: liczba recenzji +4..+10 tys. (bez wlasnego rachunku). W S2/S3 przyciete do ok. 3/4 (+3..+8): N7 oddaje ludzi rodom, a rod na pulapie zwolni nadwyzke (15%/d po 3 dobach); w S2 na pulap schodza Polnoc, Dorne, Wyspy, Ibben, Wolni Ludzie [S].

## 5. Czego nie wiem

- Los kampanii: jeden bieg roczny kodu 43; biegi 120 dob tego kodu roznia sie o +-5 tys., ten byl w dobach 93-120 "szczesliwy" (+4.5).
- Ile daniny zbierze swiat w granicy: 225 tys. (lud placi jak dzis, tylko gdzie indziej) czy ok. 100 tys. (garnek: bogate korony bez potrzeby nie podnosza stawki) - roznica S2 ok. 5 tys.
- Jak blisko granicy zaczyna nowa kampania (S3 1/2 -> ok. 96, 3/4 -> 93, cala -> 90.5) i czy kiesy rodow (bez zmian na start) dotrwaja do konca roku.
- Wojny po regule: model trzyma mape z biegu; tracace krolestwa (Wyspy, Wolni Ludzie, Ibben) moga wypasc z wojny (pokoj) albo stracic lenna - liczone tylko slownie; Volantis po regule zyskuje, ale tylko jesli jest w wojnie.
- N7 - liczba z recenzji, test w toku; jak dziala na pulapie - szacunek.
- Rok 2 (odsetki Banku, wyczerpane skrzynie biednych rodow) - nie liczone. **Policzone w krytyce (rozdz. 7): ok. -6 tys. w kazdym scenariuszu.**

## 6. Pliki (wszystko w `synteza\`)

- `WYNIK.md` (ten plik), `s2_synteza.py` + `.out.txt` + `.json` (tabela krolestw), `s1_porownaj_rody.py` + `.out.txt` (W37 vs W43 rok, rody w wojnie), `cmp_g01.py` (strumienie koron W37 / W43 120 / W43 rok), `sprawdz_w43rok.txt` (skrot `sprawdz_logi.py`).
- `rody\`: r1-r4 `*_w43rok*.out.txt`, `r4_w37_theta206.out.txt`, `r4_krolestwa_w43rok_{dan_stala,dl-1,dl100000}.json`, `r4_krolestwa_w37_theta206.json`.
- `krolestwa\`: `k1_w43rok.out.txt`, `k2_w43rok.out.txt`, `k3_w43rok.out.txt`, `k2_w37_Xzmierzone.out.txt`, `k3_w37_Xzmierzone.out.txt`, `k3_krolestwa_*.json`.
- `garnek\`: `g01_W43rok.out.txt`, `g02_W43rok.out.txt`, `g03_W43rok_p1000000.out.txt`, `g04_W43rok.out.txt`, `g05_W43rok_p1000000.out.txt`.
- `pobor\`: `p1_W43rok.out.txt`, `wynik_p2_Armoury-2026-10-13_15-02-59_sarnor{1.0,2.5}.out.txt`.

Ponowne uruchomienie: `python -I s2_synteza.py` (czyta gotowe json/out z podkatalogow); przebiegi modeli - polecenia jak w RAPORT.md kazdego modelu, z `--log D:\Backup-Bannerlord\logs\2026-10-14-noc\Armoury-2026-10-13_15-02-59.log --tabele D:\Backup-Bannerlord\logs\2026-10-14-noc\Logs\2026-10-13_15-02-59`.
Krytyka (rozdz. 7): `synteza\krytyka\` - pliki i polecenia w 7.8.

## 7. KRYTYKA (13.10, po syntezie) - proba obalenia "swiat utrzyma 85 tys."

Ten sam bieg (`D:\Backup-Bannerlord\logs\2026-10-14-noc`, kod 43, 364/364 dob). Skrypty i wyniki w `synteza\krytyka\` (7.8). Poza tym katalogiem i tym rozdzialem (plus dopiski w pkt 14 rozdz. 0, pod tabela rozdz. 1 i w rozdz. 5) nic nie zmienione.

### 7.0 Dla Jeffa - prosto

1. Rachunek syntezy jest policzony dobrze - sumy, tabele i skrypty sprawdzone, bledu zmieniajacego wynik nie ma. N7 i W5 nie sa liczone dwa razy.
2. Ale N7 byl wziety za wysoko. Recenzja liczyla go w dobach 93-120, kiedy panowie mieli w budzecie miejsce na 33 tys. ludzi wiecej; na koniec roku maja miejsce na 23 tys. N7 przyjmuje tylko tylu, na ilu pan ma zold - wiec raczej **+3 do +7 tys.**, nie +4 do +10.
3. Wazniejsze: koniec roku 1 to nie jest stan staly. W logu widac trzy rzeczy, ktore w roku 2 obnizaja wojsko nawet bez zadnej zmiany kodu:
   - lud od ok. doby 250 oddaje w daninie wiecej, niz odklada - kiesy ludu spadaja (28.7 -> 25.1 mln), wiec danina zejdzie z 230 do ok. 200 tys. zl dziennie;
   - 120 biednych rodow (21.5 tys. ludzi, co piaty zolnierz w mierze) wydaje ponad dochod: ich kiesy spadly przez rok z 15.5 do 5.8 mln, a ich wojsko od doby 120 z 26 do 21.5 tys. - i spada dalej;
   - skarbce koron schodza do zapasu 500 tys. (z ich 1/360 dziennie idzie dzis panom ok. 24 tys. zl/d - w roku 2-3 tego nie bedzie); doplata ze skarbca W5b (8 tys. zl/d) tez sie konczy.
4. Razem w roku 2-3: **ok. -6 tys.** w kazdym scenariuszu (pesymistycznie -8 do -10).
5. Kod dzisiejszy i pierwszy krok reguly w roku 2-3: ok. **95 tys., z N7 98-100** - prog trzyma.
6. Pelna regula (S2) w dlugiej kampanii: ok. **84 tys. bez N7, ok. 87 z N7** - na samym progu; przy pechowej kampanii (-5) juz pod progiem.
7. **Najgorszy realny przypadek: ok. 73 tys.** (12 pod progiem) - pelna regula, rok 3, danina tylko 100 tys. zl/d, slaby pobor, N7 przy dolnej granicy, dwa najslabsze krolestwa wybite z wojny, pechowa kampania.
8. Wniosek: "swiat utrzyma 85 tys." **stoi** dla kodu dzisiejszego, pierwszego kroku i dla konca roku 1. Dla pelnej reguly w kampanii dluzszej niz rok **nie stoi pewnie** - srodek 2 tys. nad progiem przy rozrzucie kampanii +-5. Bieg dwuletni musi pilnowac progow z 7.6.

### 7.1 Co sprawdzilem w rachunku syntezy

- `s2_synteza.py`: sumy krolestw, srednie A-D, poprawka B, P4, zapis json - przeliczone, zgodne z `s2_synteza.out.txt` i tabelami rozdz. 1-2; arytmetyka kolumn "+ P4 + N7" i "wobec 85" zgodna.
- Podwojne liczenie W5: nie ma. W r4 pula = zwroty + renty + gotowka W5 - danina bazowa, danina wchodzi raz, gotowka W5 zmniejsza renty. W B poprawka "danina wedlug ludzi" dodana raz (srednia z rodow i z C x 0.81). Model C jest uzyty i jako model, i w poprawce B - to waga (C liczy ok. 1.5 raza w S2), nie blad.
- Podwojne liczenie N7: nie ma - bieg roczny to kod 43 bez N7 (galaz N7 od 4d33042 = WGRANIE 43). N7 i W5 dziela jednak to samo "miejsce w zoldzie" (StragglersRally.cs: odbior najwyzej do miejsca w limicie i w budzecie zoldu, ta sama miara co werbunek gry) - dlatego N7 trzeba wiazac z luzem w budzecie (7.2), a nie dodawac stale.
- `r4_scenariusze.py --danina-ludzie`: danina krolestw w pokoju i Strazy idzie do zera, a ich suma do krolestw w wojnie - na koncu roku ta danina jest ok. 0, wiec bez skutku. Wiersze "WRAZLIWOSC", "NAKLADKA P4" i "DIAGNOSTYKA" w `rody\r4_w43rok_dl*.out.txt` licza BEZ daniny wedlug ludzi (with_s0 bez 'dl') - synteza ich nie uzywa (P4 bierze z wariantu z danina stala), ale nie wolno ich czytac jako S2.
- Garnek (model D) w S3 to CALA droga (S3 ~ S2 z podloga 1 mln), a A-C licza 3/4 drogi - srodek "S3 3/4" jest przez to ok. 0.6 tys. za nisko (ostroznie - zostawiam).
- "Szczesliwa kampania" (+4.5 w dobach 93-120): na koniec roku ten bieg mial pecha - w dobach 330-336 Volantis wyszedl z wojny (3.8 tys. wypadlo z miary), Targaryen (Aegon) zniknal (1.6 tys.). Bez tych dwoch zdarzen miara stalaby ok. 105-107 (plaskowyz d197-329: 105 tys.). Srodka 101.3 nie przesuwam; rozrzut +-5 jest realny (miara spadla o 6 tys. w jednym tygodniu przez mape).

### 7.2 Co poprawione

1. **N7: +3 / +5 / +7 tys.** (synteza: +4 / +7 / +10, w S2/S3 +3 / +5 / +8). Recenzja N7 liczyla skutek przy "luzie w puli zoldu 33 tys." (d93-120 biegu 13-28-08). Ta sama miara (`krytyka\rody\k5_luz.py`): 33.0 tys. tam, **22.9 tys. na koniec roku** kodu 43 (x0.69). Model rodow daje luz w S2 roku 1 21.4-23.0 tys. (x0.67), w roku 2-3 19.1-21.8 tys. (x0.6-0.66) -> N7 w roku 2-3 **+2.5 / +4.5 / +6.5**.
2. **Rok 2-3 (stan ustalony) policzony** - model rodow (tylko on ma kiesy rodow), kopia `krytyka\rody\r4k.py`; na synteze przeniesiona zmiana, nie wartosc. Kontrola: S0 = 101 297 = pomiar.

| skladnik (tys. ludzi) | S0 | S2, danina 100 tys. | S2, danina 225 -> 189 tys. | S3 3/4, danina 100 tys. |
|---|---:|---:|---:|---:|
| skrzynie rodow w stanie ustalonym (pulap idzie za D x 1.0, nie x 0.8) | 0.0 | -2.8 | -2.5 | -3.8 (razem z wierszem nizej) |
| zapas koron 1/360 znika (24 tys. zl/d), W5b 0, danina ustalona (203 tys. zl/d; w S2 jak w kolumnie) | -3.0 | -0.6 | -1.9 | (wyzej) |
| bilans rodow bez luzu (saldo d253-364: -26.5 tys. zl/d; W5 lapie czesc) | -2.9 | -2.3 | -2.1 (razem z Bankiem) | -2.2 |
| Bank na limicie (zwrot ciety do wplywow korony) | 0.0 | -0.5 | (wyzej) | -0.2 |
| **razem rok 2-3, srodek** | **-5.9** | **-6.2** | **-6.6** | **-6.2** |
| pesymistycznie (danina 183 tys. w S0; saldo d309-364 -44.8 tys. zl/d) | -10.1 | -7.9 | - | -9.2 |

   Zmiana S2 dla syntezy = srednia dwoch wariantow daniny = **-6.4**; S0 i S1 -5.9; S3 -6.2.
   Dowody z logu: kiesy ludu swiata 28.7 mln (d225-252) -> 25.1 mln (d337-364), na koniec -27 tys. zl/d przy daninie 232 tys. (Reach: kiesy 4.0 -> 2.0 mln, danina 44 -> 31 tys.); biedne rody (G < R, 120 rodow): kiesy 15.5 -> 5.8 mln, G/R 1.75 -> 0.56, ludzie w partiach 26.1 tys. (d113-140) -> 21.5 tys., dlug w Banku 0 -> 0.40 mln; 51 rodow na pulapie (24 tys. ludzi) tez traci kiesy (-10 tys. zl/d w d309-364); rody z luzem (54 tys. ludzi) zbieraja +67-71 tys. zl/d, ale im zloto ludzi nie doda. Skarbce koron spadaja dokladnie o 1/360 nadwyzki ponad 500 tys. (np. Sarnor, Skagos, Summer Isles: -1 637 zl/d przy 1.07 mln) - klifu nie ma, jest wygasanie.
3. **Utrata twierdz / wypadniecie slabych krolestw.** Model krolestw (wariant wojny, 14 twierdz) daje +0.1 tys. - przejecie twierdzy przenosi tez zold zalogi. Ryzyko jest inne: w S2 roku 2-3 Zelazne Wyspy maja 1.6 tys., Wolni Ludzie 0.5, Skagos 0.25, Sarnor 0.6, Ibben 1.2 tys. - takie krolestwo latwo wybic (w tym biegu Smocza Skala z 3.5 tys. stracila 6 z 10 twierdz, Targaryen (Aegon) z 1.6 tys. zniknal, a panowie "bez krolestwa" maja 5.3 tys. ludzi poza miara). Licze **-1.5 tys. (0 do -3)** w S2 i -1 w S3.

### 7.3 Najgorszy realny przypadek - liczba

Pelna regula (S2), rok 3 kampanii; kazdy skladnik z widelek, ktore dane wspieraja:

| skladnik | tys. |
|---|---:|
| model rodow (najbardziej pesymistyczny z czterech): S2, danina wedlug ludzi tylko 100 tys. zl/d (jak garnek; ludne krainy juz dzis drenuja kiesy ludu), skrzynie ustalone, bez zapasu koron i W5b, bilans rodow bez luzu (srodek), Bank na limicie | **79.6** |
| P4 - slabszy werbunek w pustych krainach (dolny koniec poboru) | -2.4 |
| N7 przy dolnej granicy (rok 2-3) | +2.5 |
| dwa najslabsze krolestwa (Wyspy, Wolni Ludzie) wybite z wojny | -1.5 |
| pechowa kampania | -5.0 |
| **razem** | **ok. 73 tys. (12 pod progiem)** |

Z bilansem rodow z ostatnich 56 dob (deficyt 45 tys. zl/d): ok. **71 tys.** - to juz dno, nie realny przypadek. Najgorszy realny w roku 1 (tak jak liczyla synteza, z poprawionym N7): 85.7 - 2.4 + 3 - 5 = **ok. 81 tys.** - takze w roku 1 pelna regula przy pechu schodzi pod prog, mimo N7.

### 7.4 Tabela po krytyce (tys., miara progu; bez losu kampanii +-5, poza ostatnia kolumna)

| scenariusz | rok 1: + P4 + N7 (srodek; nisko-wysoko) | rok 2-3 bez N7 (srodek) | **rok 2-3: + P4 + N7 + mapa (srodek)** | rok 2-3 nisko (pesym. rok 2-3, P4 nisko, N7 +2.5, mapa) | rok 2-3 nisko i pech -5 |
|---|---|---:|---:|---:|---:|
| S0 kod 43 (bez P4) | 106.3 (104.3-108.3) | 95.4 | **99.9** | 93.7 | 88.7 |
| S1 pierwszy krok | 104.8 (98.2-109.2) | 95.0 | **98.4** | 89.0 | 84.0 |
| S2 granica | 95.5 (86.3-106.0) | 84.0 | **87.0** | 76.4 | 71.4 |
| S3 3/4 drogi | 98.2 (88.5-107.5) | 87.1 | **90.5** | 77.3 | 72.3 |

S0 bez P4, bo P4 nie jest w kodzie 43 (S0 z P4 jak w syntezie: rok 1 105.2). Rok 2-3 = stan ustalony: koniec roku 2 lezy miedzy rokiem 1 a ta kolumna, raczej blizej niej, bo danina i biedne rody juz dzis ida w te strone. Kolumny "nisko" sumuja wszystkie dolne konce naraz; realny najgorszy przypadek - 7.3.

### 7.5 Czy wniosek stoi

- **Stoi** dla kodu 43 (S0) i pierwszego kroku (S1) - w roku 2-3 srodek 98-100 z N7; nawet z wszystkimi dolnymi koncami naraz i pechem 84-89, czyli na progu.
- **Stoi** dla konca roku 1 we wszystkich scenariuszach (srodki 95-106).
- **Nie stoi pewnie** dla pelnej reguly (S2) w kampanii dluzszej niz rok: srodek 87 z N7 (84 bez), zapas 2 tys. przy rozrzucie kampanii +-5; najgorszy realny 73. S3 (nowa kampania, 3/4 drogi) w roku 2-3: srodek 90.5, przy pechu ok. 85.
- Dzwignie (od najwiekszej): ile daniny zbierze swiat w pelnej regule (100 czy 200 tys. zl/d - ok. 5 tys.), los kampanii (+-5), bilans rodow bez luzu (3-5 tys.; dzieje sie juz w kodzie 43, regula tylko pogarsza), N7 (2.5-6.5), P4 (do -2.4).
- Do rozmowy z Jeffem (bez decyzji): rody tracace kiese to dzis 21.5 tys. ludzi; W5 (do 60% limitu) lapie je tylko czesciowo. Bez zmiany ich bilansu pelna regula nie ma zapasu w dlugiej kampanii.

### 7.6 Ryzyka do pilnowania w biegu dwuletnim - progi

Okna po 28 dob; "dzis" = koniec roku 1 kodu 43. ALARM = przy takim stanie pelna regula w dlugiej kampanii schodzi pod 85.

| # | co | dzis (d337-364) | oczekiwane w roku 2 | ALARM | skrypt |
|---|---|---|---|---|---|
| 1 | miara W5-7 (wojsko rodow krolestw w wojnie) | 101.4 (plaskowyz d197-329: 105) | koniec roku 1 >= 98 z N7 (S1); koniec roku 2 >= 95 | koniec roku 1 < 95 albo koniec roku 2 < 90; spadek d421-728 > 8 tys./rok poza zdarzeniami mapy | `k1_przebieg.py`, `sprawdz_logi.py` W5-7 |
| 2 | kiesy ludu swiata (kiesa_W+R+P) / danina P1 | 25.1 mln (-27 tys. zl/d) / 232 tys. zl/d | kiesy 20-25 mln, zmiana >= -30 tys. zl/d; danina >= 190 tys. | kiesy < 18 mln albo danina < 170 tys. zl/d w 2 oknach | `k2_lud.py` |
| 3 | biedne rody (G < R, w wojnie): ludzie w partiach / G/R | 21.5 tys. / 0.56 | >= 18 tys. / >= 0.40 | < 17 tys., spadek > 1.5 tys. na 28 dob w 2 oknach albo G/R < 0.35 | `k6_biedni_przebieg.py` |
| 4 | rody pod klifem (G < 0.25 R - pulap x0.5): ludzie w partiach | 0.9 tys. | <= 2 tys. | > 3 tys. | `k7_klif.py` |
| 5 | saldo rodow bez luzu (zmiana G minus przyrost dlugu) | -26.5 tys. zl/d (d253-364), -44.8 (d309-364) | >= -30 tys. zl/d | < -45 tys. zl/d w 2 oknach albo dlug Banku rodow > 1.5 mln | `k4_bilans_rodow.py` |
| 6 | skarbce koron w wojnie / dlug koron w Banku | 21.0 mln / 0.76 mln | 15-17 mln w d728 (1/360) / <= 2 mln | dlug > 3 mln albo zwrot niepelny ("skarbiec nie mial dosc") w > 2 krolestwach przez 28 dob | `k1_przebieg.py` |
| 7 | luz w puli zoldu (do limitu) - paliwo N7 | 22.9 tys. | >= 18 tys. | < 15 tys. (N7 wtedy +2 i mniej) | `krytyka\rody\k5_luz.py` |
| 8 | N7 - skutek w mierze | niezmierzony | test 120 dob: +3 do +7 wobec biegow W43 120 dob (109.0 / 111.8) | < +2.5 tys. | `sprawdz_logi.py --grupa n7` + W5-7 |
| 9 | mapa: krolestw w wojnie / wojsko "bez krolestwa" | 25 / 5.3 tys. | >= 24 / <= 8 tys. | < 23 krolestw w wojnie albo > 10 tys. poza krolestwami | `k1_przebieg.py`, balans-krolestw.csv |
| 10 | po regule (S2): Polnoc / Dorne / Zelazne Wyspy | 12.6 / 6.7 / 5.0 tys. | >= 7 / >= 3 / >= 1.5 tys. | ktores ponizej - krolestwo moze wypasc z wojny | `k1_przebieg.py` (tabela krolestw) |

### 7.7 Metoda krytyki (krotko)

- `k1_przebieg.py`: miara po dobach z linii "Skarbce:" (d337-364 101.4, zgodnie z synteza), trend, skarbce i dlug koron na krolestwo.
- `k2_lud.py`: kiesy ludu, nadwyzka, danina po oknach i na krolestwo; danina ustalona = danina + zmiana kiesy (srodek 203 tys. zl/d; pesym. bez wzrostu nigdzie - 183 tys.).
- `k3_skrzynie.py`, `k4_bilans_rodow.py`, `k6_biedni_przebieg.py`, `k7_klif.py`: kiesy rodow wedlug grup (G < R / na pulapie / z luzem), saldo na rod (`k4_deficyt_od253.json` srodek, `k4_deficyt_od309.json` pesym.).
- `rody\r4k.py` (kopia `r4_scenariusze.py`): kiesy `lr` (G - R zmienia sie o 11.25 x dD - w stanie ustalonym wydatki = dochod), `cap_add` (saldo rodu bez luzu w pulapie; W5 liczony od nowa), `pool_add` (minus 1/360 zapasu koron), W5b 0, danina ustalona, `refund_cap` (Bank na limicie), luz do limitu na scenariusz. Z `--k1 --k2` liczy tylko blok krytyki.
- Przeniesienie na synteze: zmiana roku 2-3 z modelu rodow dodana do srodka syntezy (S0/S1 -5.9, S2 -6.4, S3 -6.2); "nisko" = zmiana pesymistyczna (-10.1 / -7.9 / -9.2) od dolnego konca syntezy.

### 7.8 Pliki krytyki (`synteza\krytyka\`) i polecenia

- `k1_przebieg.py` -> `k1_przebieg_w43rok.out.txt` / `.json`; `k2_lud.py` -> `k2_lud_w43rok.out.txt` / `.json`; `k3_skrzynie.py` -> `k3_skrzynie_w43rok.out.txt`; `k4_bilans_rodow.py` -> `k4_bilans_rodow_w43rok.out.txt`, `k4_deficyt_od253.json`, `k4_deficyt_od309.json`; `k6_biedni_przebieg.py` -> `k6_biedni_przebieg_w43rok.out.txt`; `k7_klif.py` -> `k7_klif.out.txt`.
- `rody\r4k.py` -> `rody\r4k_w43rok.out.txt`, `rody\r4k_krytyka_Armoury-2026-10-13_15-02-59.json`; `rody\k5_luz.py` (luz w puli zoldu; liczby w 7.2).

Z katalogu `synteza\krytyka\` (L = `D:\Backup-Bannerlord\logs\2026-10-14-noc`):
```
python -I k1_przebieg.py --log "L\Armoury-2026-10-13_15-02-59.log" --wy w43rok
python -I k2_lud.py --tabele "L\Logs\2026-10-13_15-02-59" --wy w43rok
python -I k4_bilans_rodow.py --od 253 --tabele "L\Logs\2026-10-13_15-02-59" --json k4_deficyt_od253.json
python -I k4_bilans_rodow.py --od 309 --tabele "L\Logs\2026-10-13_15-02-59" --json k4_deficyt_od309.json
python -I k6_biedni_przebieg.py --tabele "L\Logs\2026-10-13_15-02-59"
python -I k7_klif.py --tabele "L\Logs\2026-10-13_15-02-59"
cd rody
python -I r4k.py --log "L\Armoury-2026-10-13_15-02-59.log" --tabele "L\Logs\2026-10-13_15-02-59" --okno 28 --do 364 --k1 ..\k1_przebieg_w43rok.json --k2 ..\k2_lud_w43rok.json --k4c ..\k4_deficyt_od253.json --k4p ..\k4_deficyt_od309.json
python -I k5_luz.py --log "L\Armoury-2026-10-13_15-02-59.log" --tabele "L\Logs\2026-10-13_15-02-59" --okno 28 --do 364
```
Na biegu dwuletnim: te same polecenia z nowym logiem; `k4 --od` = poczatek okna trendu (np. 617), `r4k --do 728`. `k4_bilans_rodow.py` z `--od` < 365 liczy do doby 364 (rok 1), z `--od` >= 365 - do konca biegu.
