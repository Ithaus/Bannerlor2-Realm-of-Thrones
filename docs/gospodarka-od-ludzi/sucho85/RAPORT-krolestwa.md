# Rachunek na sucho: czy swiat utrzyma 85 tys. wojska - MODEL OD GORY (krolestwa)

Zespol "krolestwa", 13.10. Rachunek na papierze (python na logach i tabelach biegow). Nic w grze, kodzie, ustawieniach ani repo nie zmienione.
Miara progu wszedzie ta sama co w `sprawdz_logi.py` (blok W5-7): suma "wojsko rodow" z linii "Skarbce" krolestw w stanie WOJNA, srednia 28 dob (tu: konca roku).
Same druzyny lordow (z bohaterami i kompaniami w sluzbie krola) - BEZ zalog.
**[P]** = pomiar z logu / CSV, **[S]** = szacunek (rachunek na pomiarach). "Nie wiem" = nie da sie ustalic z tych danych.

## 0. Wynik w 10 liniach

1. **S0 (kod WGRANIA 43, bez reguly, koniec roku): ok. 92 tys. [S], widelki 85-99 tys.**; do tego los kampanii +-5 tys. Bieg roczny WGRANIA 37 mial 79.4 [P]; kod 43 dorzuca danine P1, doplate W5 i W5b.
2. Spadek od doby 120 do konca roku to glownie DROZSZY zolnierz, nie brak zlota: w biegu W37 druzyny -18% [P], z tego zold czlowieka (tier 2.5 -> 3.0) -16%, skrzynie rodow -3%, D -2%, zalogi -1% [S z P].
3. **S1 (pierwszy krok, zloto lezy jak dzis): ok. 92.5 tys. [S], 85-99** - prawie bez zmian wobec S0 (przesuwa sie tylko 19% D, a tracacych trzymaja skrzynie i lup).
4. **S2 (granica, cale D wedlug ludzi): ok. 86 tys. [S], 80-94** - na styk z progiem. Wariant, w ktorym danina zostaje tam, gdzie lud dzis odklada: ok. 89 tys. (82-96).
5. **S3 (nowa kampania wedlug pyt. 7): ok. 88 tys. [S], 82-96** (gdy swiat utrzyma 3/4 drogi do granicy; przy polowie 90, przy calej 86).
6. **Nakladka P4 (pobor i milicja wedlug ludzi): -1 do +2 tys.** - prawie nic, bo pobor przyspiesza tam, gdzie i tak brakuje zlota albo stoi naturalne wypelnienie druzyn.
7. Regula sama kosztuje swiat ok. 3-7 tys. ludzi w polu [S]: biedni na pulapie traca czlowieka za kazde ~10 zl/d, bogaci z luzem zyskuja czlowieka dopiero za ~30-200 zl/d.
8. Traca najbardziej: Polnoc (-2.8 tys.), Zelazne Wyspy (-2.9; pulap mniejszy niz zold samych zalog - w polu zostaje ok. 300 ludzi), Wolni Ludzie (-1.7), Ibben (-1.0), Dragonstone (-1.0), Dorne (-0.9). Zyskuja: Reach (+2.2), Volantis (+1.5), Dorzecze (+0.5), Braavos, Norvos, Qohor, Pentos (po +0.2-0.35). King's Landing -1.0 (traci czesc daniny).
9. Czy swiat utrzyma 85 tys.? **S1 i S3 - tak, ze srodkiem 88-92 tys.; S2 (granica) - na styk (86).** Dolne konce widelek (80-82) i los kampanii (+-5) sa ponizej progu: ryzyko spadku pod 85 w granicy ok. 1 na 2, w S3 ok. 1 na 3 [S].
10. Najwieksze "nie wiem": ile pieniadza kod 43 da naprawde na koniec roku (danina rosnie jeszcze w dobie 120) - **nowy bieg roczny to zmierzy, skrypt k2 wypisze wtedy S0 [P] wprost**; czy bogate krolestwa zamienia zloto na ludzi szybciej, niz pokazuje krzywa; jak daleko S3 dojdzie do granicy.

## 1. Model w jednym akapicie

Krolestwo to calosc: **wypelnienie druzyn** (ludzie / limit druzyn) zalezy od **pokrycia** = (pulap - zold zalog - zold karawan) / (zold czlowieka x limit druzyn).
Zalogi sa placone pierwsze, wiec liczy sie tylko miejsce, ktore po nich zostaje. Krzywa dopasowana na 657 punktach krolestwo x blok 28 dob ze wszystkich biegow [P]:
wypelnienie = c / (1 + (c / 0.66)^3)^(1/3) (c = pokrycie) - przy malym pokryciu kazdy czlowiek miejsca to czlowiek w polu, przy pokryciu ok. 1 i wyzej druzyny stoja na ok. 60-66% limitu (naturalne wypelnienie AI) i pieniadz prawie nic nie daje.

| pokrycie | 0.2 | 0.4 | 0.6 | 0.8 | 1.0 | 1.2 | 1.5 |
|---|---:|---:|---:|---:|---:|---:|---:|
| wypelnienie | 0.20 | 0.37 | 0.50 | 0.57 | 0.61 | 0.63 | 0.64 |
| ile czlowieka z czlowieka miejsca | 0.96 | 0.76 | 0.47 | 0.26 | 0.14 | 0.07 | 0.03 |

Kazde krolestwo ma swoj mnoznik (stan dzis wychodzi dokladnie), zmiane D przelicza sie na pulap: 0.76 zl pulapu za 1 zl/d D (0.6 + dwor ustepujacy zoldowi 0.16; rod ze skrzynia krotko tylko 0.40, bo rezerwa 20 x D rosnie razem z D) [P kod ClanBudget.Ceiling, Settings].
Swiat w biegu W37 trzyma w polu staly ulamek miejsca w pulapie: 0.50-0.58 (bloki 28 dob od doby 57) [P].

## 2. Ile ludzi daje 1 zl/d (elastycznosci)

"Na pulapie" = pokrycie krolestwa ponizej 0.7 (pieniadz trzyma); "z luzem" = pokrycie 1.0 i wiecej (druzyny stoja na naturalnym wypelnieniu - to jest ich "limit"; Qarth 84% limitu, Straz 78%, reszta 45-65%).

| skad | na pulapie | srodek | z luzem |
|---|---|---|---|
| (a) przekroj 26 krolestw w wojnie, doby 337-364 W37: srednio ludzi na 1000 zl/d D | 120 [P] | 118 [P] | 129 [P] |
| (a) regresja przekroju: ludzi na 1 zl/d D (przy stalych zalogach i limicie) | **0.105** (11 krol., R2 0.84; 1 zl zoldu zalog = -0.20 czl.) [S] | - | -0.02 (nic) [S] |
| (a) krzywa, krancowo, krotko (ze skrzyniami) | 0.046 [S] | 0.014 [S] | 0.005 [S] |
| (b) szereg czasowy W37 (zmiany blokow 28 dob w krolestwie) | **0.086** (70 par) [S] | 0.044 (82) [S] | 0.034 (95) [S] |
| doplata celowana (W5): krzywa, 1 zl/d pulapu | 0.106 [S] | 0.033 [S] | 0.011 [S] |
| (c) biegi 120 dob: doplata W5 na krolestwo (25 krol. x 4 biegi) | 0.124 ludzi na 1 zl/d gotowki (R2 0.12) [S] | | |
| (c) biegi 120 dob: swiat, danina P1 (+77 tys. zl/d D; w 4 z 6 biegow takze doplata 22-38 tys. zl/d) | +6.5 tys. ludzi = 0.085 na 1 zl/d (+-0.07 z losu kampanii) [S] | | |

Prosto: **pan na pulapie: ok. 1 czlowiek za 10-12 zl/d stalego dochodu; doplata celowana: 1 czlowiek za 6-10 zl/d (gora: 1 czlowiek na zold dzienny 5.75 zl); pan z luzem: 1 czlowiek za 30-200 zl/d.**
Test wstecz (doby 93-120): biegi z danina +6.5 tys. wobec biegow W37 [P], sam W43 +9.9 [P]; model daje +3.9 do +5.9 i +5.1 do +8.2 - jest ostrozny, zanizal ok. 1.5 raza.
Stad w S0-S3 dodatkowy czlon "luz" 0-0.03 ludzi na 1 zl/d dla krolestw z luzem (szereg czasowy 0.034) - w widelkach.

## 3. S0 - kod WGRANIA 43 bez reguly, koniec roku

Biegi 120 dob, doby 93-120 [P]: kod W37 95.4 / 101.1 / 100.8 tys. (srednio 99.1); kod z danina 105.6 / 99.9 / 106.7 / 101.7 / 111.2 / 109.0 (W43).
Rozrzut tego samego kodu: +-3-6 tys. Bieg roczny W37 byl w dobach 93-120 o 3.7 tys. ponizej sredniej swojego kodu.

Co sie dzieje od doby 120 do 364 (bieg roczny W37) [P, rozklad S]:

| | doby 93-120 | doby 337-364 |
|---|---:|---:|
| miara progu | 95.4 tys. | 79.4 tys. |
| zold czlowieka druzyny | 4.92 zl | 5.75 zl (+17%: tier 2.5 -> 3.0) |
| D budzetu krolestw w wojnie | 647 tys. zl/d | 625 tys. |
| pulap (w tym skrzynie rodow) | 1 003 (489) tys. | 963 (467) tys. |
| zold zalog | 185 tys. | 190 tys. |
| wplywy koron / 1/360 skarbca / renty | 442 / 86 / 213 tys. | 449 / 37 / 171 tys. |
| skarbce koron / glowy rodow / kiesy ludu miast | 45.1 / 50.9 / 21.0 mln | 27.8 / 46.9 / 43.2 mln |

Druzyny -18%: zolnierz drozszy -16%, skrzynie -3%, D -2%, zalogi -1%, reszta +2%. Skarbce spadaja o 17 mln, ale wplywy koron rosna, a renty spadaja tylko o 1/5 - pieniadz panow prawie stoi. Wojen tyle samo (26 z 29 krolestw).

Kod 43 dodaje do tego pieniadz koron dla panow. W dobach 93-120 W43: danina P1 113 tys. + W5b 14 tys. zl/d [P], z tego 38 tys. idzie celowo (doplata W5) [P]; wplywy koron 561 tys. wobec 408-443 w W37 [P]. Danina dalej rosnie (d106-120: 120-126 tys. zl/d), 10 krolestw ma jeszcze stawke s ponizej 1% (Reach, Dolina, Qarth, Dothrakowie, Volantis, Stormlands...), a nadwyzka ludu to 12.2 mln [P].
Zalozenie na koniec roku [S]: **X = 110 / 145 / 180 tys. zl/d** wiecej dla panow niz w W37, z tego 30-50% celowo (doplata), rozdzielone jak danina w dobach 93-120 (King's Landing 21%, Polnoc 14%, Wyspy 7%, Wolni Ludzie 6%, Meereen 6%, Qohor / Ibben / Braavos / Dolina po 4%).

**S0 = 92 tys. [S], widelki 85-99 tys.** (X, czesc celowana, ludzi na zl doplaty 0.10-0.20, pulap 0.40 albo 0.76 za zl D, czlon "luz").
Do tego: los kampanii +-5 tys.; jesli kampania W37 byla "pechowa" jak w dobach 93-120: 89-103. Prosty rachunek kontrolny: 109.0 x (1 - 18%) = 89 tys. + rosnaca danina -> ok. 90-93.
Najwiecej zyskuja z kodu 43: King's Landing +2.3, Wyspy +1.6, Polnoc +1.6, Wolni Ludzie +1.2, Ibben +1.0 tys. [S].

## 4. Scenariusze reguly (koniec roku, miara progu)

D krolestw z r04 recenzji (pierwszy krok i granica), na nie nalozony pieniadz kodu 43. W S2 takze danina i doplata ida wedlug ludzi; w S2b zostaja jak dzis.
Pyt. 3 w rachunku: Straz - D bez zmian (stala produkcja i rynek Castle Black); Sarnor 0.25 mln ludzi. Krolestwa w wojnie - jak w biegu (26).

| scenariusz | nisko | srodek | wysoko | wobec 85 tys. (srodek) | krolestwa, w ktorych pulap < zold zalog |
|---|---:|---:|---:|---:|---|
| S0 (kod 43, bez reguly) | 85.0 | **92.8** | 99.2 | +7.8 | 0 |
| S1 pierwszy krok | 85.0 | **92.5** | 99.2 | +7.5 | 0 |
| S2 granica | 80.4 | **85.9** | 93.8 | +0.9 | 1 (Zelazne Wyspy: pulap 11.9 tys. zl/d, zalogi 14.9) |
| S2b granica, danina jak dzis | 82.3 | **88.9** | 96.3 | +3.9 | 0 |
| S3 nowa kampania, 1/2 drogi do granicy | 83.8 | **89.8** | 97.3 | +4.8 | 0 |
| S3 nowa kampania, 3/4 drogi | 81.9 | **87.7** | 95.6 | +2.7 | 0 |
| S3 nowa kampania, cala droga | 80.4 | **85.9** | 93.8 | +0.9 | 1 |
| to samo na bazie W37 (bez kodu 43): S0 / S1 / S2 / S3 3/4 | 79.4 | 79.3 / 76.5 / 77.3 | | -5.7 / -8.5 / -7.7 | S2: Wyspy i Wolni Ludzie |

Wszystko [S]. Do kazdej liczby los kampanii +-5 tys.

**Nakladka P4** (szansa ochotnika x min(3, max(0.1, skala wsi x udzial zbrojnych)); Wyspy x2, Wolni Ludzie x5, Dothrakowie x3): mnoznik naturalnego wypelnienia m^eta, eta 0.15-0.3 [S, nie wiem dokladnie]:
S1 -0.9 do +0.2, S2 +0.1 do +1.8, S3 +0.1 do +1.4 tys. (zalezy od eta i od czlonu "luz"). Mnoznik przy eta 0.3: Qarth x1.39, Volantis x1.37, Reach x1.25, Dothrakowie x1.19, Braavos x1.16 ... Dorne x0.81, Wyspy x0.78, Ibben x0.73, Sarnor x0.63.
Pobor dziala tylko tam, gdzie nie trzyma pieniadz - a krolestwa, ktorym tnie, i tak sa na pulapie. Odmowy werbunku zalog w ostatniej dobie W37: zold albo miejsce 42, brak ochotnikow 6 [P linia "Wojsko z ludzi"] - dzis podaz ochotnikow rzadko wiaze, stad male eta.
Pyt. 3: wyjatek Strazy trzyma ok. 4.1 tys. w mierze (bez niego projekt liczy 62-147 ludzi, czyli -4 tys.); Sarnor 0.25 mln zdejmuje hamulec wolnych rak (328 odmow dziennie [P]) - w S1 do +1 tys. [S], w S2 malo, bo Sarnor traci zloto.

**S3 - jak blisko S2 swiat zaczyna** [S]: zloto startowe kas miast, kies ludu i wsi oraz skarbcow wedlug ludzi daje od pierwszego dnia pieniadz koron prawie jak w granicy (1/360 skarbca w pierwszych miesiacach to ok. 100 tys. zl/d [P W37 d29-56], danina z kas i z oszczednosci ida za kasami) - na start ok. 3/4-1 drogi.
Ale kiesy rodow sa bez zmian, a skrzynia wojenna wydaje (G - R) / 56 dziennie, wiec przez pierwsze 3-4 miesiace wojsko tracacych trzymaja skrzynie: w dobach 93-120 S3 bedzie blisko S0 (minus 0-3 tys.). Na koniec roku 86-90 tys. Czy swiat utrzyma 3/4 drogi - nie wiem: lud ludnych miast nie udzwignie wiekszego koszyka (recenzja), ceny i podatek od utargu moga tam zjechac w strone S1.

**Na krolestwo** (miara progu, srodek; S0 -> S1 -> S2 -> S3 3/4) [S]:

| krolestwo | dzis W37 | S0 | S1 | S2 | S3 3/4 | grupa dzis |
|---|---:|---:|---:|---:|---:|---|
| The North | 8 381 | 9 938 | 9 697 | 7 095 | 7 920 | srodek |
| King's Landing | 6 044 | 8 349 | 8 403 | 7 395 | 7 647 | srodek |
| Dorne | 5 923 | 6 253 | 6 131 | 5 367 | 5 590 | luz (zyje z lupu) |
| The Vale | 5 668 | 6 206 | 6 068 | 5 755 | 5 843 | pulap |
| The Reach | 5 724 | 6 095 | 6 314 | 8 342 | 7 858 | luz |
| Riverlands | 5 683 | 5 731 | 5 721 | 6 219 | 6 095 | srodek |
| Stormlands | 5 295 | 5 492 | 5 471 | 5 784 | 5 706 | luz |
| Nights Watch | 4 089 | 4 089 | 4 089 | 4 089 | 4 089 | wyjatek |
| Dragonstone | 3 462 | 3 892 | 3 799 | 2 892 | 3 155 | pulap |
| Iron Islands | 1 628 | 3 190 | 2 789 | 296 | 438 | pulap |
| Volantis | 2 539 | 2 584 | 3 016 | 4 121 | 4 121 | pulap |
| Qarth | 2 930 | 2 930 | 3 028 | 3 148 | 3 148 | luz (84% limitu) |
| Dothraki Horde | 2 629 | 2 630 | 2 533 | 2 221 | 2 310 | luz (lup) |
| Braavos | 1 987 | 2 413 | 2 413 | 2 760 | 2 678 | pulap |
| Free Folk | 602 | 1 835 | 1 738 | 174 | 569 | pulap |
| Ibben | 426 | 1 466 | 1 452 | 490 | 732 | pulap |
| reszta (Meereen, Tyrosh, Myr, Lorath, Aegon, Qohor, Norvos, Lys, Pentos, Sarnor) | 16 398 | 19 695 | 19 877 | 19 766 | 19 823 | |
| **SWIAT (26 krolestw w wojnie)** | **79 408** | **92 788** | **92 539** | **85 914** | **87 722** | |

Wariant wojny (S2): 14 twierdz wiecej w roku przechodzi od 6 najwiekszych przegranych do 6 najwiekszych wygranych. Przegrany traci na twierdzy ok. 1 530 zl/d ziemi i rent, ale przestaje placic ok. 1 000 zl/d zalodze; wygrany odwrotnie [S z P]. Suma wojska w polu: **+0.0 tys.** - samo przejmowanie twierdz sumy nie rusza. Obniza ja dopiero zniszczenie albo pokoj biednego krolestwa (wypada z miary 0.2-0.6 tys. kazde: Wyspy, Wolni Ludzie, Ibben w S2) [S].

## 5. Czym to sie rozni od tabeli projektu ("wojsko w granicy 131-167 tys.")

- Tabela projektu to **druzyny + zalogi** wszystkich krolestw (dzis 162.6 tys. = 79.0 druzyn + 83.6 zalog), liczone recznie na baze W37 (bez kodu 43). Prog 85 tys. to **same druzyny krolestw w wojnie** (dzis 79.4).
- Moj odpowiednik tabeli (druzyny wszystkich krolestw + zalogi stale): baza W37: dzis 164 -> S2 161 tys.; z kodem 43: 177 -> S2 170 tys. [S] - gorny koniec widelek projektu.
- Gdyby z widelek projektu odjac stale zalogi, na druzyny zostaje 47-83 tys. - czyli tabela projektu, czytana uczciwie, mowi "na bazie W37 prog nie wyjdzie". Moj model na bazie W37 daje S2 76.5 tys. - w gornej polowie tamtego pasma.
- Roznica w dolnym koncu: projekt zakladal, ze tracacy traca wojsko proporcjonalnie do D (Dorne -70%). Model od gory mowi, ze tracacy z luzem (Dorne, Dothrakowie) zyja w duzej czesci z lupu i okupow (Dorne: wplyw 103 tys. zl/d przy D 45 tys. [P r01]) i maja zapas w pulapie - traca 10-20% druzyn, nie 70%. Za to biedni na pulapie (Wyspy, Wolni Ludzie, Ibben) traca prawie wszystko, bo zalogi placa pierwsze.
- Zyskujacy: projekt dawal Reach 15-17 tys. (z zalogami), ja +2.6 tys. w druzynach - Reach stoi na naturalnym wypelnieniu 47% limitu, 1.8 tys. ludzi "ma miejsce, nie werbuje" [P raport roczny, tabela 5.5] i pieniadz tego nie zmieni.

## 6. Zalozenia krytyczne i czego nie wiem

Zalozenia, od ktorych wynik zalezy najbardziej:
1. Ile pieniadza kod 43 da panom na koniec roku (X 110-180 tys. zl/d ponad W37). +-35 tys. zl/d X = +-3-4 tys. ludzi. Nowy bieg roczny to zmierzy.
2. Krzywa nasycenia: krolestwo z luzem prawie nie zamienia zlota na ludzi (0.005-0.034 czl./zl). Gdyby nasycenie roslo z pieniadzem mocniej (test wstecz sugeruje model ostrozny x1.5), S2 bylby wyzej o 2-4 tys.
3. Zold czlowieka (tier) rosnie jak w W37 (+17% od doby 120). Wiecej zlota = wiecej awansow = drozszy zolnierz - nie liczone (raczej w dol).
4. W S2 danina i doplata ida za ludzmi (S2), albo zostaja tam, gdzie lud dzis odklada (S2b): roznica 3 tys.
5. Wojny jak w biegu (26 z 29 krolestw w wojnie przez caly rok); zalogi i limity druzyn stale; bohaterowie i kompanie najemne w mierze stale na krolestwo.
6. S3: ile drogi do granicy swiat utrzyma w roku 1 (1/2 - cala) - nie wiem.
7. P4: jak mocno podaz ochotnikow wiaze werbunek (eta 0-0.3) - nie wiem, brak pomiaru zapasu ochotnikow u notabli.

Nie wiem: los kampanii (+-5 tys., z jednego biegu rocznego); kto wygra wojny po zmianie i czy biedne krolestwa znikna; ile zlota bogate krolestwa wloza w zalogi i najemnikow; czemu Reach przy luzie trzyma tylko 47% limitu.

## 7. Skrypty (python -I, kazdy z --log / --tabele / --okno)

Katalog: `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\c36b73ca-1de0-4b1b-80ce-71bd02afe545\scratchpad\sucho85\krolestwa\`
(B = `D:\Backup-Bannerlord\logs\2026-10-13-noc34-35`; logi czytane strumieniowo, wynik czytania w `cache_*.json` - klucz to nazwa i rozmiar logu).

- `wspolne.py`, `model.py` - czytanie logu i CSV, miara progu (jak sprawdz_logi W5-7), krzywa i przeliczenie.
- `k0_wczytaj.py --log A.log [B.log ...] --tabele A [B ...]` - miara progu w kazdym biegu; `k0_bloki.py` - bloki 28 dob swiata (wyniki `k0_bloki.out.txt`).
- `k1_elastycznosc.py --log ROK.log [biegi120 ...] --tabele ...` - (a) przekroj, (b) szereg czasowy, (c) biegi 120 dob, krzywa -> `k1_wynik.json` (`k1_elastycznosc.out.txt`).
- `k2_s0.py --log ROK.log W43_120.log [inne120 ...] --tabele ...` - S0, test wstecz, rozklad spadku -> `k2_wynik.json` (`k2_s0.out.txt`).
  **Jesli pierwszy log to bieg roczny kodu 43 (jest danina P1 i doplata W5) - wypisuje S0 zmierzone [P] i zapisuje baze bez dodatku.**
- `k3_scenariusze.py --log ROK.log W43_120.log --tabele ... [--bez-w43]` - S1, S2, S2b, S3, P4, zalogi, wariant wojny (`k3_scenariusze.out.txt`, `k3_scenariusze_bez_w43.out.txt`).

Na nowym biegu rocznym (kolejnosc: k1, k2, k3):
```
python -I k1_elastycznosc.py --log NOWY.log B\Armoury-2026-10-13_05-47-42.log B\Armoury-2026-10-13_10-30-28.log B\Armoury-2026-10-13_11-11-41.log B\Armoury-2026-10-13_11-46-24.log B\Armoury-2026-10-13_12-21-21.log B\Armoury-2026-10-13_12-59-58.log B\Armoury-2026-10-13_13-28-08.log --tabele NOWY_DIR B\Logs\2026-10-13_05-47-42 B\Logs\2026-10-13_10-30-28 B\Logs\2026-10-13_11-11-41 B\Logs\2026-10-13_11-46-24 B\Logs\2026-10-13_12-21-21 B\Logs\2026-10-13_12-59-58 B\Logs\2026-10-13_13-28-08
python -I k2_s0.py --log NOWY.log B\Armoury-2026-10-13_13-28-08.log --tabele NOWY_DIR B\Logs\2026-10-13_13-28-08
python -I k3_scenariusze.py --log NOWY.log B\Armoury-2026-10-13_13-28-08.log --tabele NOWY_DIR B\Logs\2026-10-13_13-28-08
```
(dla bazy W37 tak, jak w tym raporcie: pierwszy log `B\Armoury-2026-10-13_06-24-10.log` z `B\Logs\2026-10-13_06-24-10`, k2 z dodatkowymi biegami 120 dob jako odniesienie).
