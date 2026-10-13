# RAPORT ROCZNY - rozdzial: WOJSKO (klucz: wojsko)

Bieg: nowa kampania, 364 doby, sam swiat AI (rod gracza stoi). Kod: galaz w-toku/zbior-e8 9e2e0e6 (WGRANIE 36 + runda 3 + sluby NPC).
Porownanie: poprzedni bieg roczny (WGRANIE 34, bez rundy 3) - ten sam start, te same 364 doby.
Praca tylko do odczytu. Nic nie uruchamialem, nic nie zmienialem.

## 0. Jak czytac

- **Doba 1 = dzien gry 108837.** Log numeruje te sama dobe na dwa sposoby (czesc linii pisze "dzien 108836", czesc "dzien 108837"). W raporcie wszedzie jest doba 1..364. Srednia 28 dob = doby 337-364.
- **LOG** = D:\Backup-Bannerlord\logs\2026-10-13-noc34-35\Armoury-2026-10-13_06-24-10.log. **LOG34** = ...\Armoury-2026-10-13_02-15-21.log (poprzedni bieg). Lnnn = numer linii.
- **CSV** = ...\Logs\2026-10-13_06-24-10\budzet-rodow.csv (jeden wiersz na rod na dobe; kolumna dzien 108836 = doba 1). Znaczenie kolumn sprawdzone w kodzie: ClanIncomeBook.cs 461-632, ClanBudget.cs 191-210 i 290-510, CrownRents.cs 714-731.
- **[P]** = pomiar z logu albo CSV. **[S]** = szacunek (rachunek na pomiarach, opisany przy liczbie).
- Trzy miary wojska lordow, nie mylic:
  - "druzyny w wojnie" - miara progu 85 tys.: linie "Skarbce", druzyny rodow krolestw, ktore sa w wojnie. Liczy bohaterow i kompanie najemne w sluzbie krola.
  - "druzyny rodow (Ludzie)" - linia "Ludzie:". Liczy takze Innych (wighty; 3.7 tys. na koniec).
  - CSV - sami szeregowi, bez bohaterow. Wychodzi ok. 1 tys. mniej niz "Skarbce".
- Skrypty: raport-rok\skrypty\wojsko\ (a_swiat, b_przeplyw, c_luka, c2_luka28, d_krolestwa, e_ostatnie, f_sprzet, g_zloto, h_wojna, i_najemnicy, j_wypelnienie; kazdy ma plik .out.txt z wynikiem).
- Tabela wszystkich rodow (co kogo trzyma): raport-rok\wojsko-luka-rody.csv (377 wierszy, srednia z dob 337-364).

## Najkrocej

1. Wojska w polu jest 79.4 tys. (srednia 28 dob), w ostatniej dobie 73.6 tys. Prog 85 tys. nie wyszedl. Poprzedni bieg mial 64.0 tys. - runda 3 dala +24%.
2. Wojsko nadal powoli topnieje: od doby 183 srednio 36 ludzi dziennie. Rownowagi jeszcze nie ma.
3. Ostatnia doba to nie trend, tylko trzy bitwy: Dorzecze stracilo dwie cale armie pod Deep Den i Willow Wood (3.4 tys.), Dorne trzecia (1.4 tys.).
4. Przyczyna luki to pieniadz, nie ludzie. 71% brakujacych ludzi to rody, ktorym skonczylo sie miejsce w pulapie zoldu. Ochotnicy sa, weterani stoja w karczmach (8.5 tys.).
5. Rody "na pulapie" (111 rodow z lennem) maja druzyny wypelnione w 38%. Rody z wolnym miejscem (85) w 64% i tak trzymaja caly rok. Gdyby wszyscy mieli 64%, w polu byloby ok. 100 tys. To jest sufit tego swiata przy tej wojnie, nawet z nieograniczonym zlotem.
6. Pulap zjadaja zalogi. W zalogach stoi 83.6 tys. zawodowych zolnierzy, w polu 74.5 tys. Zalogi sa oplacane pierwsze. 16 rodow placi zalogom wiecej, niz wynosi caly ich pulap - w polu maja razem 58 ludzi z 9 766 mozliwych.
7. Zeby dojsc do 85 tys., rodom na pulapie trzeba dolozyc ok. 30-45 tys. zl dziennie na zold, jesli pieniadz trafi tylko do nich. Jesli pojdzie dzisiejsza droga (zwrot zoldu i renty dla wszystkich), trzeba 60-190 tys. zl dziennie nowych wplywow koron. Do 95 tys.: 80-115 tys. celowane.
8. Zloto na to jest w swiecie: lud miast odklada 56-70 tys. zl dziennie i ma juz 44.1 mln (z tego 29.5 mln ponad zapas). Skarbce koron stracily od doby 30 az 27.5 mln.
9. Wojsko jest prawdziwe, ale niedozbrojone: zbroje na tulow wlasciwego szczebla ma 70% ludzi druzyn, helm 66%. Jazdy t3+ jest 17.6% przy celu 27.5%; 5.5 tys. ludzi czeka na konia.
10. Zold jest placony (zalegly 0.12% zlota), dezercja mala (17 ludzi dziennie z druzyn), nic nie powstaje z niczego.

## 1. Ile jest wojska

W kazdej komorce: **ten bieg / poprzedni bieg**.

| co | doba 1 | doba 91 | doba 182 | doba 273 | doba 364 | srednia 28 dob |
|---|---|---|---|---|---|---|
| WSZYSCY pod bronia (z milicja, karawanami, taborami wsi) | 165 159 / 165 374 | 297 955 / 294 237 | 299 292 / 299 142 | 299 937 / 285 470 | 295 576 / 273 116 | 300 111 / 273 453 |
| druzyny rodow razem (z Innymi) | 29 896 / 29 679 | 102 433 / 97 735 | 90 344 / 87 081 | 89 879 / 76 538 | 77 837 / 67 015 | 83 170 / 68 743 |
| **druzyny krolestw w wojnie (miara progu)** | 2 535 / 2 503 | 99 299 / 94 992 | 88 205 / 84 311 | 86 897 / 73 029 | **73 578** / 62 043 | **79 409** / 64 013 |
| druzyny wszystkich krolestw | 27 971 / 27 795 | 101 530 / 96 248 | 88 674 / 84 507 | 87 350 / 73 577 | 74 456 / 62 333 | 80 011 / 64 422 |
| krolestw w wojnie / wszystkich | 2 z 29 / 2 z 29 | 25 z 29 / 26 z 30 | 26 z 29 / 28 z 30 | 26 z 29 / 27 z 31 | 26 z 29 / 26 z 31 | 26 z 29 / 26 z 31 |
| liczba druzyn rodow | 278 / 279 | 677 / 650 | 656 / 670 | 661 / 648 | 626 / 606 | 641 / 617 |
| zalogi razem | 47 356 / 47 341 | 80 219 / 77 828 | 84 191 / 81 493 | 82 056 / 77 627 | 83 593 / 74 147 | 83 593 / 74 122 |
| - zalogi miast | 30 923 / 30 990 | 49 094 / 47 479 | 48 975 / 47 528 | 48 071 / 45 088 | 47 917 / 42 744 | 47 870 / 42 712 |
| - zalogi zamkow | 16 433 / 16 351 | 31 125 / 30 349 | 35 216 / 33 965 | 33 985 / 32 539 | 35 676 / 31 403 | 35 722 / 31 410 |
| kompanie najemne (11 kompanii) | 0 / 0 | 1 998 / 1 962 | 2 109 / 2 081 | 2 114 / 2 001 | 2 184 / 2 494 | 2 190 / 2 374 |
| bandy - ludzie | 5 694 / 5 822 | 10 977 / 10 024 | 14 464 / 13 730 | 15 353 / 17 535 | 17 279 / 20 501 | 17 055 / 20 021 |
| bandy - liczba | 314 / 336 | 593 / 553 | 713 / 715 | 742 / 890 | 819 / 937 | 811 / 945 |
| milicje | 62 991 / 63 044 | 64 316 / 65 452 | 68 403 / 70 258 | 71 281 / 72 481 | 74 006 / 74 947 | 73 617 / 74 581 |
| karawany | 11 881 / 11 862 | 17 335 / 15 180 | 18 457 / 15 984 | 18 113 / 15 535 | 18 085 / 15 070 | 17 913 / 15 078 |
| tabory wsi | 2 878 / 2 873 | 15 199 / 15 416 | 14 687 / 14 798 | 14 552 / 14 592 | 14 666 / 14 747 | 14 563 / 14 428 |
| inne partie | 4 463 / 4 753 | 7 476 / 12 602 | 8 746 / 15 798 | 8 703 / 11 162 | 10 110 / 6 689 | 10 200 / 6 481 |
| Inni (wighty; siedza w "druzynach rodow") | 616 / 616 | 1 244 / 2 055 | 2 001 / 2 982 | 2 838 / 3 508 | 3 699 / 5 084 | 3 490 / 4 840 |
| Nocna Straz (druzyny + zalogi) | 3 958 / 3 845 | 8 308 / 7 868 | 8 354 / 7 897 | 8 481 / 7 880 | 8 243 / 8 014 | 8 264 / 7 936 |
| Wolni Ludzie (druzyny + zalogi) | 5 466 / 5 580 | 9 222 / 9 575 | 9 858 / 9 749 | 10 842 / 10 688 | 11 848 / 6 789 | 11 550 / 6 783 |
| pula wyrzutkow (ludzie w lesie) | 819 / 692 | 2 257 / 2 386 | 8 629 / 4 934 | 10 785 / 7 675 | 11 868 / 9 788 | 11 449 / 9 071 |
| jency szeregowi w druzynach | 17 / 34 | 1 221 / 2 231 | 2 768 / 4 271 | 2 896 / 4 151 | 4 771 / 2 185 | 3 159 / 2 678 |
| druzyny + zalogi jako % mezczyzn 16-60 | 0.55 / 0.55 | 1.31 / 1.25 | 1.25 / 1.21 | 1.23 / 1.11 | 1.16 / 1.01 | 1.20 / 1.03 |

Wszystko [P]. Zrodla: linia "Ludzie:" LOG L770, L32321, L64561, L96942, L129266; LOG34 L739, L32160, L64316, L96635, L129031. Linie "Skarbce:" LOG L603-631, L32160-32188, L64401-64429, L96783-96811, L129103-129131; LOG34 L572-600 i L128864-128894. Zalogi: linia "Zalogi: przyrost bez werbunku (169c)" (doba 364: L129148). Kompanie: CSV, rodzaj = najemnik, kolumna ludzi_partie. Inni: linia "Wzrost Innych (169c)" (L129155). Straz i Wolni Ludzie: L129271. Wyrzutki: L129134.

Co z tego wynika:
- Szczyt byl w dobie 68: 108 131 ludzi w druzynach w wojnie [P, skrypt e_ostatnie]. Od tamtej pory spadek o 27% do sredniej 79.4 tys.
- Poprzedni bieg: szczyt 98 471 w dobie 82, na koniec 64.0 tys. (-35%).
- Zalogi nie topnieja: 80-84 tys. od doby 91 do konca. W poprzednim biegu spadly do 74 tys.
- W zalogach jest wiecej zawodowych zolnierzy niz w polu: 83.6 tys. wobec 74.5 tys. (doba 364).
- W karczmach czeka 8 456 weteranow t3-t6 (L129162). Do tego 10 571 bylych zolnierzy w puli wyrzutkow (L129134).
- Na dobie 1 kompanii najemnych "nie ma" w CSV, bo zaden krol jeszcze ich nie najal. To nie blad.

## 2. Tabela krolestw

Druzyny i zalogi: CSV, kolumny ludzi_partie, ludzi_zalogi, limit_partii, doby 1 i 364 (szeregowi). Limit = suma limitow wielkosci czynnych druzyn rodu. Doby wojny: linie "Skarbce" z dopiskiem (WOJNA). Tier, t3+ i jazda druzyn: linia "Musztra AI wedlug krolestw" L1780 (doba 4) i L129179 (doba 364). Tier zalog: balans-krolestw.csv, kolumna tier_zalog, doba 364. Wszystko [P].

| krolestwo | rody | druzyny d1 | druzyny d364 | druzyny sr. 28 dob | limit druzyn d364 | % limitu d364 | % limitu sr. 28 | zalogi d1 | zalogi d364 | doby wojny | sredni tier druzyn d4 -> d364 | t3+ | jazda | tier zalog |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| The North | 26 | 2 374 | 7 632 | 8 272 | 13 930 | 55% | 60% | 4 204 | 9 394 | 355 | 2.58 -> 3.27 | 75% | 11% | 2.21 |
| House Baratheon of King's Landing | 28 | 1 138 | 6 009 | 5 977 | 12 773 | 47% | 54% | 4 336 | 6 989 | 355 | 2.22 -> 3.09 | 64% | 11% | 2.12 |
| The Reach | 17 | 2 000 | 5 833 | 5 662 | 12 444 | 47% | 47% | 3 104 | 6 093 | 342 | 2.45 -> 2.55 | 45% | 6% | 1.94 |
| The Vale | 21 | 1 887 | 5 455 | 5 591 | 9 533 | 57% | 59% | 2 710 | 5 663 | 342 | 2.31 -> 3.19 | 69% | 14% | 2.26 |
| Stormlands | 14 | 2 141 | 5 166 | 5 242 | 8 423 | 61% | 59% | 2 634 | 5 635 | 363 | 2.43 -> 2.96 | 58% | 6% | 1.92 |
| Dorne | 42 | 751 | 4 310 | 5 832 | 7 710 | 56% | 57% | 2 995 | 4 683 | 342 | 2.64 -> 3.06 | 62% | 15% | 2.54 |
| Nights Watch | 6 | 1 172 | 4 058 | 4 070 | 5 196 | 78% | 78% | 836 | 1 737 | 364 | 2.54 -> 3.78 | 97% | 9% | 3.05 |
| Volantis | 9 | 1 168 | 3 579 | 2 513 | 7 150 | 50% | 44% | 1 739 | 2 879 | 342 | 2.68 -> 3.07 | 66% | 13% | 2.52 |
| Dragonstone | 11 | 1 961 | 3 402 | 3 424 | 6 473 | 53% | 46% | 2 075 | 3 166 | 363 | 2.52 -> 2.95 | 60% | 5% | 2.62 |
| Dothraki Horde | 17 | 704 | 2 982 | 2 605 | 5 123 | 58% | 57% | 1 357 | 2 232 | 342 | 2.44 -> 2.09 | 31% | 45% | 1.79 |
| Riverlands | 15 | 2 918 | 2 397 | 5 615 | 7 107 | 34% | 48% | 3 118 | 4 767 | 277 | 2.63 -> 2.38 | 39% | 6% | 2.16 |
| Tyrosh | 6 | 278 | 2 315 | 2 106 | 3 162 | 73% | 63% | 1 065 | 2 032 | 342 | 2.50 -> 2.75 | 57% | 4% | 1.96 |
| House Targaryen | 9 | 1 132 | 2 205 | 2 378 | 6 111 | 36% | 44% | 1 691 | 2 236 | 342 | 3.56 -> 3.44 | 67% | 26% | 3.41 |
| Myr | 7 | 573 | 1 948 | 1 789 | 3 957 | 49% | 56% | 1 153 | 1 812 | 342 | 2.55 -> 3.01 | 63% | 12% | 2.58 |
| Qarth | 5 | 363 | 1 811 | 2 916 | 2 496 | 73% | 80% | 1 207 | 1 849 | 342 | 2.31 -> 2.47 | 44% | 13% | 2.29 |
| Lorath | 6 | 769 | 1 580 | 1 740 | 3 280 | 48% | 46% | 983 | 1 661 | 342 | 2.87 -> 2.94 | 58% | 7% | 2.51 |
| House Targaryen, Aegon | 7 | 273 | 1 580 | 1 779 | 4 181 | 38% | 47% | 1 178 | 1 732 | 342 | 2.43 -> 2.51 | 45% | 13% | 2.63 |
| Braavos | 6 | 1 028 | 1 510 | 1 959 | 3 076 | 49% | 56% | 1 101 | 2 097 | 342 | 2.61 -> 2.76 | 52% | 6% | 2.43 |
| Iron Islands | 30 | 1 209 | 1 400 | 1 578 | 7 957 | 18% | 19% | 2 325 | 4 860 | 342 | 2.55 -> 2.90 | 62% | 0% | 2.75 |
| Sarnor | 7 | 316 | 1 377 | 1 288 | 5 010 | 27% | 27% | 1 021 | 633 | 342 | 2.17 -> 3.44 | 76% | 16% | 3.17 |
| Qohor | 6 | 277 | 1 342 | 1 246 | 3 643 | 37% | 38% | 1 043 | 1 730 | 342 | 2.43 -> 3.00 | 63% | 16% | 2.25 |
| Pentos | 12 | 398 | 1 271 | 1 157 | 3 606 | 35% | 42% | 1 006 | 1 966 | 342 | 2.92 -> 3.00 | 67% | 9% | 2.34 |
| Norvos | 6 | 683 | 1 243 | 1 398 | 2 675 | 46% | 40% | 1 029 | 2 014 | 342 | 2.36 -> 3.08 | 59% | 12% | 1.90 |
| Lys | 6 | 291 | 1 055 | 1 306 | 2 757 | 38% | 42% | 1 069 | 1 071 | 342 | 2.44 -> 2.40 | 40% | 5% | 2.16 |
| Free Folk | 12 | 1 324 | 696 | 566 | 6 029 | 12% | 10% | 1 210 | 2 797 | 364 | 2.19 -> 2.14 | 35% | 0% | nie wiem (brak wiersza w CSV) |
| Ibben | 6 | 362 | 508 | 413 | 3 720 | 14% | 13% | 797 | 1 660 | 342 | 2.58 -> 2.76 | 58% | 2% | 2.50 |
| Summer Isles (pokoj) | 5 | 58 | 528 | 194 | 1 946 | 27% | - | 97 | 48 | 0 | 2.44 -> 2.63 | 58% | 1% | 3.00 |
| Skagos (pokoj) | 2 | 139 | 237 | 233 | 884 | 27% | - | 176 | 89 | 0 | 2.48 -> 3.27 | 83% | 93% | 3.07 |
| Yi Ti Exiles (pokoj) | 2 | 53 | 67 | 153 | 301 | 22% | - | 97 | 68 | 0 | 2.34 -> 2.22 | 31% | 9% | 2.76 |
| **RAZEM** | 346 | 27 740 | 73 496 | 79 001 | 160 653 | 46% | 48% | 47 356 | 83 593 | - | 2.64 -> 2.96 | 59% | 11% | 2.31 |

Uwagi:
- "Rody" to wszystkie rody AI krolestwa na dobie 364, takze rycerze BK bez lenna (89 rodow na swiecie, z zasady bez wlasnej druzyny - L129087) i kompanie najemne w sluzbie.
- Limit druzyn to gorna granica gry, nie cel. Pasmo projektu 95-115 tys. to 59-71% tego limitu. Prog 85 tys. to 52%.
- Najslabsze: Free Folk 10%, Ibben 13%, Iron Islands 19%, Sarnor 27%. Trzy pierwsze trzyma pulap zoldu, Sarnor - brak ludzi (pkt 5).
- Riverlands na dobie 364 ma 2 397, bo tego dnia stracilo dwie armie (pkt 6). Srednia 28 dob to 5 615.
- 26 z 29 krolestw bylo w wojnie przez 342-364 doby. Pokoju w tym swiecie prawie nie ma (pkt NIESPOJNOSCI, E).
- Wiersz RAZEM, tier: balans-krolestw.csv wazony ludzmi, doba 1 -> 364 (skrypt f_sprzet). t3+ i jazda: L129201.

## 3. Sklad i wyposazenie - na ile wojsko jest prawdziwe

### 3.1 Tiery

| co | doba 1 | doba 91 | doba 182 | doba 273 | doba 364 | poprzedni bieg d364 |
|---|---|---|---|---|---|---|
| sredni tier druzyn | 2.64 | 2.49 | 2.71 | 2.80 | 2.96 | 3.04 |
| sredni tier zalog | 2.71 | 2.28 | 2.27 | 2.32 | 2.31 | 2.32 |
| ludzie t3+ w druzynach | 13 950 z 29 618 (47%) | 41 746 z 101 730 (41%) | 46 428 z 89 668 (52%) | 48 792 z 89 180 (55%) | 45 287 z 77 191 (59%) | 37 857 z 66 381 (57%) |
| zold na czlowieka druzyny (zl/dobe) | - | 4.98 | 5.47 | 5.72 | 6.17 | 6.42 |

[P]. Tier: balans-krolestw.csv (tier_partii, tier_zalog wazone ludzmi). t3+: linia "Rycerze i jazda (219)" L702, L32256, L64496, L96877, L129201. Zold na czlowieka: linia "Zold konnych" (stawki dzienne / ludzie), doba 364: L129200; doba 30: 4.79. Poprzedni bieg d364: TRIAZ-3.
Pelnego rozkladu t1..t6 calego wojska w logu nie ma - **nie wiem**. Jest sredni tier i udzial t3+.
Wniosek: wojsko dojrzewa (tier 2.49 -> 2.96) i przez to drozeje o 29% na glowe (4.79 -> 6.17 zl). Za te same pieniadze jest mniej ludzi.

### 3.2 Jazda (doba 364 i srednia 28 dob)

| co | druzyny rodow | zalogi | zrodlo |
|---|---|---|---|
| ludzie | 77 191 | 83 593 | L129203 |
| jazda wedlug typu zolnierza | 8 640 (11.2%) | 3 030 (3.6%) | L129203 |
| - z koniem | 7 011 | 2 441 | L129203 |
| - z koniem i uprzeza (pelna para) | 6 667 (8.6% ludzi) | 2 243 (2.7%) | L129203 |
| - z koniem bez uprzezy | 344 | 198 | L129203 |
| - bez konia | 1 183 | 423 | L129203 |
| jazda wsrod t3+ | 17.6% (cel szablonow 27.5%) | - | L129201 |
| czeka na konia swojej kategorii | 5 453 ludzi w 294 druzynach (sr. 28 dob 6 026) | - | L129202 |
| proby awansu na jezdzca odrzucone z braku konia (na dobe) | 4 133 | - | L129202 |
| wolne konie w zbrojowniach | 2 328 (zwykle 1 757, bojowe 541, szlachetne 30) | - | L129202 |
| konie kupione przez lordow (doba) | 82 za 63 960 zl; nieudane: brak koni 21, brak zlota 2 | - | L129202 |

Wszystko [P]. Jazda jest o jedna trzecia ponizej celu. Powod to brak konia wlasciwej kategorii tam, gdzie stoi druzyna, nie brak zlota (21 nieudanych zakupow z braku koni wobec 2 z braku zlota; srednio 28 dob 21.9 wobec 2.4). 366 ludzi czeka, choc w taborze sa konie innej kategorii.

### 3.3 Bron i zbroje (procent ludzi z dana rzecza)

| co | druzyny d4 | druzyny d89 | druzyny d184 | druzyny d274 | druzyny d364 | zalogi d364 | poprzedni bieg, druzyny d364 |
|---|---|---|---|---|---|---|---|
| zbroja na tulow wlasciwego szczebla | 96% | 70% | 67% | 70% | 70% | 89% | 67% |
| glowna bron wlasciwego szczebla | 97% | 82% | 83% | 86% | 85% | 98% | 82% |
| helm | 90% | 44% | 54% | 62% | 66% | 89% | 63% |
| tarcza | 90% | 38% | 52% | 63% | 73% | 93% | 66% |
| rekawice | 93% | 48% | 53% | 58% | 61% | 86% | 56% |
| zbroja t3+ u ludzi t3+ | 95% | 33% | 40% | 48% | 52% | 71% | 48% |
| bron t3+ u ludzi t3+ | 95% | 40% | 53% | 63% | 70% | 94% | 66% |
| cokolwiek na tulowiu (kazdy szczebel) | 97% | 88% | 92% | 92% | 92% | 99% | 91% |
| jakakolwiek glowna bron | 96% | 87% | 88% | 89% | 89% | 99% | 86% |
| brakuje sztuk razem | 22 619 | 214 275 | 181 172 | 158 792 | 131 386 | (w sumie) | 124 982 |

[P]. Linia "Pokrycie zbrojowni AI (171)" (co 5 dob): L1774, L31531, L65197, L97225, L129173; LOG34 L128938. Na dobie 364 brakuje 30 255 zbroi na tulow i 28 155 sztuk broni.
- Zalogi sa uzbrojone prawie w komplecie. Druzyny nie.
- Braki maleja caly rok (214 tys. -> 131 tys. sztuk), ale czesc to po prostu mniej ludzi.
- 8% ludzi druzyn nie ma na tulowiu nic, 11% nie ma glownej broni.
- Brak broni obniza cwiczenia druzyn do x0.82; 41 druzyn nie cwiczy wcale (L129172).
- Pieniadz na sprzet jest: przydzial dnia 239 797 zl, wydane 123 386, a 92 162 zl dziennie przepada na dwor jako niewydane po 30 dniach (L129141). Czyli brakuje towaru, nie zlota - to sprawa rozdzialu o gospodarce.

### 3.4 Odziez

Na zoldzie 74 213 ludzi w druzynach i 83 351 w zalogach. Czeka na zakup: skora 2 852, sukno 2 133, plotno 5 927 sztuk w 631 druzynach i zalogach. W 247 druzynach i zalogach odziez przepadla ponad sufit 120 dob (lachmany, bez kary). Powod: 39 wizyt bez towaru na polce, 0 z powodu chudej sakiewki (L129133) [P].

### 3.5 Ocena

Wojsko jest prawdziwe w tym sensie, ze kazdy czlowiek ma pochodzenie, zold i sprzet z rynku. Nie jest w pelni uzbrojone: mniej wiecej 2 na 3 ludzi druzyn maja komplet glownych rzeczy swojego szczebla, jazdy jest o jedna trzecia za malo. Zalogi sa w dobrym stanie.

## 4. Przeplyw ludzi na dobe

Srednie na dobe. Q2 = doby 92-182, Q3 = 183-273, Q4 = 274-364.

| przeplyw | Q2 | Q3 | Q4 | 28 dob | poprzedni bieg, 28 dob | linia logu |
|---|---|---|---|---|---|---|
| WEJSCIA do druzyn rodow | | | | | | |
| werbunek wodzow razem | 688 | 666 | 588 | 535 | 608 | Ludzie |
| - od notabli (ochotnicy) | 605 | 559 | 493 | 434 | 510 | Ludzie |
| - z karczmy (weterani) | 79 | 84 | 81 | 76 | 79 | Ludzie |
| - jency wcieleni, ochotnicy z mapy | 35 | 42 | 29 | 38 | 29 | Ludzie |
| nowe druzyny (ludzie z wolnych rak regionu siedziby) | 436 | 513 | 442 | 455 | 535 | Partie (E4/0) |
| z zalog do druzyn | 338 | 378 | 302 | 302 | 356 | Zalogi (169c) |
| WYJSCIA z druzyn rodow | | | | | | |
| zabici | 474 | 503 | 398 | 398 | 392 | Ludzie |
| zwolnieni przez budzet rodu (pulap zoldu) | 270 | 215 | 314 | 332 | 520 | Budzet rodow (166) |
| wzieci do niewoli | 255 | 348 | 241 | 321 | 186 | Jency (E4/0) |
| z druzyn do zalog | 220 | 210 | 165 | 153 | 186 | Zalogi (169c) |
| dezercja | 24 | 20 | 19 | 17 | 19 | Ludzie |
| rozbici po przegranej (wszystkie rodzaje partii) | 730 | 717 | 759 | 691 | 864 | Ludzie |
| BILANS druzyn rodow na dobe | -133 | -5 | -132 | -213 | -99 | Ludzie (bilans) |
| INNE | | | | | | |
| ranni (wszystkie partie swiata) | 889 | 976 | 848 | 866 | 816 | Ludzie |
| awanse zaplacone przez AI | 672 | 628 | 553 | 538 | 476 | Szczelnosc (164a) |
| zalogi: werbunek od notabli | 224 | 222 | 214 | 210 | 241 | Zalogi (169c) |
| zalogi: zabici | 15 | 21 | 11 | 13 | 3 | Ludzie |
| zalogi: zwolnieni przez budzet | 24 | 6 | 3 | 3 | 7 | Budzet rodow (166) |

Wszystko [P] (skrypt b_przeplyw; srednie z linii dobowych). "Rozbici" to wszystkie rodzaje partii razem, wiec wiersze wyjsc nie sumuja sie dokladnie do bilansu.

Szczegoly ostatnich 28 dob [P]:
- Dezercja z druzyn wedlug przyczyny: glod 6.0, limit gry 5.0, zalegly zold 3.0, morale 2.1 na dobe (linia "Dezercja AI wedlug przyczyny (169c)"). Morale druzyn srednio 68.9.
- Zwolnieni wedlug tieru: t1-2 162, t3-4 139, t5+ 34 na dobe. 57 z nich staje w karczmie.
- Rozbici: 449 dziennie wraca do domu, 212 idzie w las (w tym 184 zolnierzy), 31 do karczmy (linia "Wyrzutki").
- Jency: 39 dziennie odbitych i wcielonych do zwyciezcy, 38 ucieka z lochow, 32 wraca do domu, 96 idzie w niewole (krainy z niewola).
- Lordowie w niewoli: srednio 42 naraz, z tego 11 glow rodow; mediana niewoli 4 doby; nikt ponad 60 dni.
- Ranni tylko w druzynach lordow: **nie wiem** - linia podaje sume wszystkich partii.

Rok razem [P] (skrypt h_wojna): wodzowie zwerbowali 293 288 ludzi, w druzynach zginelo 146 594, budzet zwolnil z druzyn ok. 85 tys. (234 dziennie). W poprzednim biegu: 290 633 / 132 032 / ok. 129 tys.

**Czy wojsko topnieje?** Tak, powoli. Nachylenie druzyn w wojnie (regresja liniowa, skrypt e_ostatnie) [P]:

| okno | ten bieg | poprzedni bieg |
|---|---|---|
| doby 92-182 | -107 ludzi/dobe | -102 |
| doby 183-273 | -34 | -121 |
| doby 274-364 (ostatni kwartal) | -44 | -86 |
| doby 183-364 | -36 | -90 |

Ostatni kwartal: spadek ok. 4 tys. ludzi (5%). Poprzedni bieg tracil 2.5 razy szybciej. Ale to wciaz nie jest stan staly: liczba rodow "na pulapie" rosnie caly rok (72 -> 91 -> 96 -> 110 w dobach 91/182/273/364, pkt 5.2).
Glowna roznica wobec poprzedniego biegu: budzet zwalnia 332 ludzi dziennie zamiast 520.

## 5. Anatomia luki

### 5.1 Ile brakuje do limitu druzyn i dlaczego

Dla kazdego rodu krolestwa w wojnie (339 rodow) policzylem, ile ludzi brakuje do limitu druzyn i co to trzyma. Regula [S]:
- **(a) brak miejsca w pulapie zoldu** - tylu ludzi, ilu nie miesci sie w wolnym miejscu pulapu (pulap minus zold zalog minus zold karawan minus zold druzyn, podzielone przez zold na czlowieka tego rodu);
- **(b) ma miejsce, nie werbuje** - ludzie, ktorzy mieszcza sie w pulapie, a rod nie mial swiezych strat;
- **(c) swieze straty** - to samo, ale rod stracil ponad 20% ludzi w 14 dobach (albo 15% w 7), albo cala druzyne w ostatnich 7 dobach;
- **(d) brak druzyny** - rod stac, a druzyny, ktora mial w ostatnich 28 dobach, nie ma od ponad 7 dob (niewola dowodcy, brak dowodcy);
- **(e) inne** - kompanie najemne (pulap z umowy) i Nocna Straz (sluzy bez zoldu, pulap w ludziach).

| grupa | brak ludzi, srednia 28 dob | udzial | zakres w 28 dobach | doba 364 | rodow (d364) | poprzedni bieg, srednia 28 dob |
|---|---|---|---|---|---|---|
| (a) pulap zoldu | 69 952 | 70.8% | 67 790 - 73 122 | 73 122 | 157 | 87 584 (76.8%) |
| (b) ma miejsce, nie werbuje | 14 065 | 14.2% | 11 730 - 15 437 | 11 730 | 75 | 11 068 (9.7%) |
| (c) swieze straty | 7 020 | 7.1% | 4 244 - 12 869 | 12 869 | 53 | 7 277 (6.4%) |
| (d) brak druzyny | 1 363 | 1.4% | 697 - 2 540 | 929 | 17 | 1 438 (1.3%) |
| (e) najemnicy i Straz | 6 428 | 6.5% | 5 097 - 7 573 | 5 717 | 25 | 6 672 (5.9%) |
| RAZEM luka do limitu | 98 827 | 100% | | 104 367 | | 114 040 |

Podstawa [P]: srednio 78 422 ludzi przy limicie czynnych druzyn 161 743 (48.5%); z druzynami, ktore zniknely w 28 dobach, limit 177 248. Zrodlo: CSV, kolumny ludzi_partie, limit_partii, pulap, zold_partii, zold_zalog, zold_karawan, doby 309-364; skrypt c2_luka28. Podzial na grupy [S].

**Zloto na dopelnienie grupy (a) do pelnego limitu: 446 tys. zl dziennie zoldu brutto** (srednia 28 dob; doba 364: 491 tys.) [S]. To jest liczba teoretyczna - do pelnego limitu nikt nie dochodzi (patrz 5.3).

Podgrupy (a) na dobie 364 [P/S, skrypt c_luka]:

| podgrupa | rodow | brak ludzi | zloto zl/dobe | kiesy rodzin razem |
|---|---|---|---|---|
| ma druzyny, jest ponad pulapem druzyn | 74 | 37 351 | 261 693 | 8.39 mln |
| ma druzyny, jest pod pulapem, ale miejsca za malo | 68 | 25 484 | 167 354 | 10.64 mln |
| bez druzyny, bo zalogi i karawany zjadly caly pulap | 13 | 9 402 | 56 737 | 0.57 mln |
| bez druzyny, miejsca mniej niz na 30 ludzi | 2 | 885 | 5 341 | 0.20 mln |

Wedlug rodzaju rodu: rody z miastem 40 860 ludzi brak (279 tys. zl), z samym zamkiem 27 883 (185 tys.), bez lenna 4 378 (27 tys.).
Wedlug zamoznosci: 101 rodow z grupy (a) ma kiese rodziny ponad rezerwe wojny (razem 15.3 mln przy rezerwie 11.2 mln), 54 rody sa pod rezerwa, 2 w glebokiej biedzie. **Rody z grupy (a) nie sa bankrutami** - maja razem 19.8 mln w kiesach. Trzyma je regula budzetu: zold najwyzej 0.60 dochodu stalego plus 0.8 nadwyzki kiesy ponad rezerwe rozlozone na 45 dni (ClanBudget.cs 191-210).

### 5.2 Rody na pulapie a rody z wolnym miejscem

Rody z lennem w wojnie, bez Strazy i najemnikow. "Na pulapie" = wolne miejsce nie starcza na 10% limitu druzyn [S, skrypt j_wypelnienie na CSV].

| grupa | doba | rodow | ludzi w druzynach | limit | wypelnienie | zold druzyn + zalog / pulap | kiesy rodzin | ludzi w zalogach |
|---|---|---|---|---|---|---|---|---|
| na pulapie | 91 | 72 | 33 196 | 62 760 | 53% | 105% | 10.7 mln | 32 238 |
| na pulapie | 182 | 91 | 31 346 | 73 898 | 42% | 107% | 10.7 mln | 40 792 |
| na pulapie | 273 | 96 | 33 569 | 77 653 | 43% | 110% | 12.1 mln | 44 745 |
| na pulapie | 364 | 110 | 29 879 | 84 387 | 35% | 107% | 12.9 mln | 48 404 |
| na pulapie | sr. 28 dob | 111 | 31 610 | 83 395 | 38% | 108% | 12.6 mln | 48 110 |
| z wolnym miejscem | 91 | 119 | 57 809 | 85 043 | 68% | 55% | 30.8 mln | 42 897 |
| z wolnym miejscem | 182 | 110 | 48 515 | 75 611 | 64% | 51% | 29.8 mln | 40 801 |
| z wolnym miejscem | 273 | 102 | 43 151 | 68 358 | 63% | 50% | 27.1 mln | 35 303 |
| z wolnym miejscem | 364 | 85 | 34 721 | 55 521 | 63% | 46% | 25.8 mln | 32 472 |
| z wolnym miejscem | sr. 28 dob | 85 | 37 841 | 59 350 | 64% | 48% | 25.8 mln | 32 992 |
| poprzedni bieg: na pulapie | sr. 28 dob | 113 | 21 856 | 83 599 | 26% | 110% | 10.4 mln | 44 869 |
| poprzedni bieg: z wolnym miejscem | sr. 28 dob | 72 | 31 129 | 50 728 | 61% | 45% | 22.5 mln | 26 597 |

Co z tego wynika:
- Rod bez ograniczen pieniedzy trzyma 63-68% limitu przez caly rok, bez trendu. To zwykla rownowaga werbunku i strat AI przy tej wojnie. Poprzedni triaz znalazl powod w kodzie gry (szansa werbunku maleje z wypelnieniem druzyny) - sam tego nie sprawdzalem ponownie.
- Rod na pulapie trzyma 38%. Runda 3 podniosla to z 26%.
- Liczba rodow na pulapie rosnie: 72 -> 110. Stad powolne topnienie.
- Rody na pulapie trzymaja 59% zalog tej grupy (48.1 tys. z 81.1 tys.) i placa im 117 tys. zl dziennie. To 38% ich pulapu (310 tys.).
- **Sufit swiata:** gdyby wszystkie rody na pulapie doszly do 64%, przybyloby ok. 21.7 tys. ludzi i w polu byloby ok. 100 tys. [S]. Pasmo projektu 95-115 tys. lezy na samej granicy tego, co ten swiat daje nawet bez ograniczen zlota. Prog 85 tys. wymaga podniesienia rodow na pulapie z 38% do ok. 46%. Cel 95 tys. - do ok. 58%.

### 5.3 Ile zlota do progu

Krzywa [S, skrypt c2_luka28, srednia 28 dob]: kazdy rod z budzetem dostaje co najmniej dany ulamek swojego limitu druzyn; najslabsi pierwsi. Liczone w szeregowych (miara progu jest o ok. 1 tys. wyzsza).

| kazdy rod co najmniej | wojsko po dopelnieniu | przyrost ludzi | z tego miesci sie juz dzis w pulapie | nie miesci sie | zloto na zold brutto, zl/dobe | rodow do podniesienia |
|---|---|---|---|---|---|---|
| 20% limitu | 83 129 | 4 707 | 539 | 4 167 | 25 849 | 50 |
| 25% | 85 195 | 6 773 | 871 | 5 902 | 36 918 | 59 |
| 30% | 87 641 | 9 219 | 1 342 | 7 877 | 49 485 | 68 |
| 35% | 90 489 | 12 067 | 1 913 | 10 154 | 63 947 | 81 |
| 40% | 93 898 | 15 476 | 2 632 | 12 844 | 81 274 | 96 |
| 45% | 97 849 | 19 427 | 3 491 | 15 936 | 101 461 | 108 |
| 50% | 102 418 | 23 996 | 4 545 | 19 451 | 124 359 | 125 |
| 60% | 113 217 | 34 795 | 7 230 | 27 565 | 176 711 | 149 |

Odczyt [S]:
- **Do progu 85 tys.: ok. 30-45 tys. zl dziennie** dodatkowego zoldu u 50-60 najslabszych rodow (dolna liczba z krzywej, gorna z rachunku 6.6 tys. ludzi x 6.9 zl u rodow na pulapie).
- **Do 95 tys.: ok. 80-115 tys. zl dziennie** u ok. 100 rodow.
- To jest zold BRUTTO i pieniadz CELOWANY - trafia tylko do rodow, ktorym brakuje. Dzisiaj caly zold druzyn to 457 tys. zl dziennie (srednia 28 dob, linia "Zold").
- Ludzie na to sa: 8 456 weteranow w karczmach, 10 571 bylych zolnierzy w lesie, werbunek od notabli 434 dziennie przy checi x0.36.

### 5.4 Grupy (b), (c), (d), (e)

- **(b) 14.1 tys.** To rody z wolnym miejscem ponizej limitu. Jak wyzej: to stan rownowagi AI (64%), nie usterka. Wyjatek z pomiarem: **Sarnor** (2 053 ludzi w (b)) - region przekroczyl prog wolnych rak (obciazenie 22.4% przy progu 20%, L129270) i ksiega ludzi odmawia ochotnikow: 328 odmow dziennie (linia "Pobor", L128936) [P]. Dla reszty (Reach 1 778, Riverlands 2 226, Dorne 1 451, Dothraki 1 066) **nie wiem**, czemu rod w danej dobie nie werbuje - log nie liczy postojow bez werbunku.
- **(c) 7.0 tys.** Swieze straty. W dobie 364 skoczylo do 12.9 tys. przez bitwy Dorzecza i Dorne.
- **(d) 1.4 tys.** Niewola i brak dowodcy to drobiazg. Zgodne z pomiarem niewoli: mediana 4 doby.
- **(e) 6.4 tys.** Kompanie najemne maja 2 190 ludzi przy 2 970 z umowy (74%, L129095). Straz ma 78% limitu.

### 5.5 Krolestwa - co trzyma (srednia 28 dob)

| krolestwo | ludzi | limit | (a) pulap | (b) nie werbuje | (c) straty | (d) | (e) | zloto na (a), zl/dobe |
|---|---|---|---|---|---|---|---|---|
| The North | 8 272 | 13 866 | 5 079 | 114 | 366 | 109 | 106 | 37 722 |
| House Baratheon of King's Landing | 5 976 | 11 155 | 5 829 | 998 | 426 | 37 | 81 | 32 002 |
| Dorne | 5 832 | 10 229 | 3 509 | 1 451 | 435 | 164 | 0 | 20 181 |
| The Reach | 5 662 | 12 118 | 4 001 | 1 778 | 1 112 | 100 | 73 | 22 718 |
| Riverlands | 5 615 | 11 595 | 4 276 | 2 226 | 359 | 145 | 87 | 22 726 |
| The Vale | 5 591 | 9 496 | 3 849 | 28 | 0 | 0 | 82 | 27 479 |
| Stormlands | 5 242 | 8 925 | 3 525 | 557 | 441 | 110 | 0 | 16 929 |
| Nights Watch | 4 070 | 5 242 | 0 | 0 | 0 | 0 | 1 179 | 0 |
| Dragonstone | 3 424 | 7 491 | 3 946 | 623 | 113 | 5 | 223 | 24 279 |
| Qarth | 2 916 | 3 637 | 6 | 412 | 323 | 60 | 234 | 30 |
| Dothraki Horde | 2 605 | 4 600 | 495 | 1 066 | 813 | 131 | 146 | 3 323 |
| Volantis | 2 513 | 5 721 | 3 087 | 161 | 1 | 48 | 376 | 20 544 |
| House Targaryen | 2 378 | 5 437 | 2 771 | 567 | 260 | 27 | 258 | 34 326 |
| Tyrosh | 2 106 | 3 335 | 482 | 645 | 145 | 60 | 92 | 2 630 |
| Braavos | 1 959 | 3 528 | 1 485 | 146 | 266 | 46 | 495 | 8 611 |
| Myr | 1 789 | 3 199 | 1 256 | 129 | 172 | 0 | 221 | 9 182 |
| House Targaryen, Aegon | 1 779 | 3 770 | 1 157 | 88 | 486 | 0 | 704 | 7 591 |
| Lorath | 1 740 | 3 780 | 1 200 | 346 | 320 | 120 | 387 | 8 484 |
| Iron Islands | 1 578 | 8 108 | 6 450 | 13 | 1 | 0 | 189 | 44 057 |
| Norvos | 1 398 | 3 512 | 2 573 | 31 | 52 | 0 | 127 | 14 918 |
| Lys | 1 306 | 3 140 | 1 735 | 318 | 475 | 79 | 279 | 8 850 |
| Sarnor | 1 288 | 4 720 | 1 049 | 2 053 | 102 | 0 | 453 | 8 695 |
| Qohor | 1 246 | 3 271 | 2 201 | 24 | 41 | 0 | 16 | 16 260 |
| Pentos | 1 157 | 2 732 | 2 085 | 170 | 108 | 105 | 125 | 13 703 |
| Free Folk | 566 | 5 859 | 4 911 | 117 | 178 | 16 | 318 | 23 764 |
| Ibben | 413 | 3 277 | 2 995 | 4 | 23 | 0 | 180 | 17 263 |

[S] na danych [P] z CSV. Pulap trzyma 22 z 26 krolestw w wojnie. Wyjatki: Qarth, Dothraki, Tyrosh, Sarnor (maja miejsce) i Straz.

Przyklady panow bez wojska polowego, bo zalogi zjadly pulap (srednia 28 dob; zold druzyn + zold zalog / pulap; kiesa rodziny) [P]:
- Joffrey Baratheon: 3 ludzi z 321; 75 + 2 839 / 2 214; kiesa 98.8 tys.
- Casat, krol Ibben: 0 z 1 056; 96 + 1 410 / 924; kiesa 53.7 tys.
- Styr Magnar (Free Folk): 0 z 1 287; 161 + 1 618 / 1 076; kiesa 47.5 tys.
- Farwynd (Iron Islands): 0 z 866; 100 + 2 322 / 1 241; kiesa 57.9 tys.
- Lannister: 351 z 1 665; 1 691 + 2 189 / 3 754; kiesa 190 tys.
Na dobie 364 jest 16 rodow, ktorych sam zold zalog jest wiekszy niz caly pulap: w polu 58 ludzi z 9 766 limitu, kiesy razem 0.74 mln (skrypt h_wojna). W poprzednim biegu bylo ich 26.

## 6. Ostatnia doba (73.6 tys.) a srednia 28 dob (79.4 tys.)

To jedna doba bitew nalozona na powolny trend. Dowody [P]:

| doba | druzyny w wojnie | zmiana | zabici w druzynach rodow | jency z druzyn rodow |
|---|---|---|---|---|
| 360 | 80 479 | -78 | 72 | 96 |
| 361 | 79 334 | -1 145 | 1 254 | 988 |
| 362 | 78 682 | -652 | 215 | 99 |
| 363 | 77 436 | -1 246 | 176 | 111 |
| 364 | 73 578 | -3 858 | 2 186 | 2 396 |

Zwykla doba: mediana zabitych w druzynach 310, srednia 459 (doby 92-364). Doba 364: 2 186 - siodma najkrwawsza doba roku.

Trzy bitwy doby 364 (bitwy.log):
- L25400, wypad pod Willow Wood: Gregor Clegane (King's Landing, 26 druzyn, 2 678 ludzi) rozbil armie Quincy Coxa (Riverlands, 13 druzyn, 2 148 ludzi). Dorzecze: 703 zabitych, 1 442 rannych - cala armia stracona.
- L25373, wypad pod Deep Den: Quenten Banefort (King's Landing, 20 druzyn, 3 751) rozbil druzyne krola Edmure'a Tully'ego (6 druzyn, 1 259). Dorzecze: 417 zabitych, 842 rannych - wszystko stracone.
- L25365, bitwa w polu: Dorne (10 druzyn, 1 566) przegralo z Reach (14 druzyn, 1 477): 243 zabitych, 1 175 rozbitych.

Zmiana na krolestwo, doba 363 -> 364: Riverlands -2 680 (5 152 -> 2 472), Dorne -1 038, reszta razem -140 (linie "Skarbce" L129103-129131).

Bez ostatniej doby srednia 28 dob wynosi 79 631 (doby 336-363). Mediana dob 337-364: 79 857. Czyli **prawdziwy poziom to ok. 79.5 tys.**, a 73.6 tys. to dolek po bitwie.
Takie dolki byly w roku jeszcze dwa razy (doba 77: -5 310, doba 277: -3 999). Po dobie 277 wojsko nie wrocilo do poziomu sprzed bitwy (86.5 tys. przed, 80.0 tys. po 28 dobach). Wiec czesc straty z doby 364 tez moze zostac - **nie wiem ile**, bieg sie skonczyl.

Srednie tygodniowe ostatniego kwartalu (druzyny w wojnie): tydzien 40: 84.0 tys., 42: 80.6, 45: 78.2, 48: 80.1, 49: 81.0, 50: 79.7, 51: 78.3, 52: 78.6. Od tygodnia 45 wojsko stoi w pasmie 78-81 tys.

## 7. Pod pytanie Jeffa: skad zloto na wojsko w czasie wojny

Jeff: "no to podatki trzeba gdzies zwiekszyc, na czas wojny. Masz propozycje?". Ponizej to, co widac od strony wojska. Stawki i skutki dla miast naleza do rozdzialu o gospodarce.

### 7.1 Kto dzis placi za wojne [P]

| pozycja (zl na dobe) | doba 30 | doba 91 | doba 182 | doba 273 | doba 364 | srednia 28 dob |
|---|---|---|---|---|---|---|
| wplywy dnia koron razem | 477 976 | 403 468 | 417 208 | 428 759 | 453 474 | 449 808 |
| - w tym danina wojenna z kas osad | 161 930 | 205 936 | 223 441 | 216 758 | 217 402 | 217 563 |
| - w tym podatek wojenny ludu (184) | 206 | 8 387 | 12 208 | 15 237 | 14 908 | 15 020 |
| doplata ze skarbcow (1/360 zapasu) | 111 593 | 91 509 | 66 224 | 48 145 | 35 105 | 37 116 |
| korona oddaje panom: zwrot 70% zoldu druzyn | 238 921 | 286 206 | 289 375 | 283 188 | 269 013 | 277 190 |
| korona oddaje panom: renty wedlug lenn | 309 836 | 176 398 | 167 478 | 142 879 | 140 689 | 172 002 |
| dochod staly panow z ziemi | 409 020 | 170 648 | 159 959 | 168 762 | 161 274 | 163 355 |
| dochod staly panow od korony | 569 812 | 467 398 | 512 951 | 468 416 | 464 401 | 456 318 |

Zrodla: linie "Korona: wplywy dnia (165)" (L129101), "Korona: podatek wojenny ludu (184)" (L129100), "Korona: danina wojenna" (L129089), "D staly (169c)" (L129146); skrypt g_zloto. Doba 30 to jeszcze rozruch kampanii (wojny dopiero sie zaczynaja, okno pomiaru 28 dob ledwo pelne) - dwoch ostatnich wierszy z tej kolumny nie porownywac z reszta.
- Panowie zyja z korony: 74% ich dochodu stalego to zwrot zoldu i renty (456 tys. z 620 tys.). Sama ziemia (163 tys.) nie pokrywa nawet zoldu zalog (191 tys.).
- Korona wydaje wszystko, co dostanie: zwrot zoldu jest placony w 100%, reszta idzie w renty. Z wplywow dnia w skarbcach zostaje 87 zl (L129101).
- Doplata ze skarbcow gasnie sama: 112 tys. -> 35 tys. zl dziennie. Renty spadly z 310 tys. do 141 tys.
- Podatek wojenny ludu stoi na pulapie 3% w 20 z 26 krolestw i daje 15 tys. zl dziennie. To 3% wplywow koron.

### 7.2 Gdzie pieniadz staje [P]

| zbiornik (zl) | doba 30 | doba 364 | zmiana w ostatnich 28 dobach |
|---|---|---|---|
| kiesy ludu miast | 11.0 mln | 44.1 mln | +70 tys. dziennie |
| - z tego ponad zapas (lokata) | 4.3 mln | 29.5 mln | +56 tys. dziennie |
| skarbce krolestw | 54.6 mln | 27.1 mln | ok. -51 tys. dziennie w Q4 |
| glowy rodow | 60.7 mln | 47.2 mln | stoi od doby 182 |
| notable | 28.9 mln | 21.8 mln | ok. -13 tys. dziennie w Q4 |
| sakiewki zolnierzy | 4.7 mln | 16.6 mln | stoi od doby 182 |
| kompanie najemne (11 rodow) | - | 5.6 mln | ok. +7 tys. dziennie (poprzedni bieg: 20.6 mln) |
| caly pieniadz swiata | 207.9 mln | 208.3 mln | swiat zamkniety |

Zrodla: linie "Pieniadz swiata" L10804 i L129205, "Kiesa ludu" L10837 i L129238; kompanie: CSV kolumna G, rodzaj = najemnik.
Pieniadz plynie ze skarbcow i od notabli do kies ludu miast i tam zostaje. Podatek, ktory go zawroci, nie tworzy nic z niczego.

### 7.3 Ile trzeba i jak to dowiezc [S]

- Potrzeba celowana (pkt 5.3): 85 tys. = 30-45 tys. zl dziennie; 95 tys. = 80-115 tys. zl dziennie.
- **Uwaga na przelicznik: 1 zl podatku to nie 1 zl zoldu.** Dzisiejsza droga korony (zwrot 70% zoldu + renty wedlug lenn) rozdaje pieniadz wszystkim panom. Rody z grupy (a) dostaja 64% rent (85 480 z 133 869 zl, doba 364, CSV kolumna renta_180). Z wzoru budzetu (pulap 0.6-0.8 dochodu stalego, zwrot 0.7 zoldu) wychodzi, ze 1 zl nowych wplywow korony daje 0.45-0.64 zl nowego zoldu u rodow na pulapie, czyli 0.7-1.0 tys. ludzi na kazde 10 tys. zl dziennie. Poprzedni triaz liczyl ostrozniej: 0.3-0.45 tys. Przyjmuje widelki 0.3-1.0 tys. ludzi na 10 tys. zl.
- Stad: **do 85 tys. dzisiejsza droga trzeba 60-190 tys. zl dziennie nowych wplywow koron** (dzis 450 tys.). Do 95 tys.: 160-520 tys.
- Rachunek "89 tys. zl = 15 tys. zolnierzy" (1 zl podatku = 1 zl zoldu przy 6 zl na glowe) jest gorna granica. Tak wyjdzie tylko wtedy, gdy caly podatek trafi do rodow na pulapie.

### 7.4 Propozycje (od strony wojska)

1. **Danina wojenna takze z odkladanych pieniedzy ludu miast.** Dzis danine placa kasy kupcow (1% nadwyzki dziennie) i kiesy wsi (2.5%), a kiesy ludu miast nie. Lud odklada 56-70 tys. zl dziennie [P] - tyle mozna brac trwale, bez uszczuplania jego zapasu. To mniej wiecej dolna granica potrzeby na 85 tys. Byla juz zaprojektowana w poprzednim triazu (pkt B), w tym kodzie jej nie ma (Settings.cs 951-954: sa tylko LaySubsidyTownShare i LaySubsidyVillageShare).
2. **Wyzszy pulap podatku wojennego ludu (184): z 3% na 6%.** Dzis 15 tys. zl przy sredniej stawce 2.4%; kazdy punkt to ok. 6 tys. zl dziennie [S, liniowo]. Daje +15-20 tys. Maly, ale liczony od biezacego zarobku.
3. **Nowy pieniadz wojenny dawac celowanie, nie wszystkim.** To jest najwazniejsze od strony wojska: ten sam podatek da 2-5 razy wiecej ludzi. Mozliwe formy, do wyboru w projekcie:
   - korona doplaca do zoldu ZALOG rodom w wojnie (dzis zwrot obejmuje tylko druzyny w polu). Rody z grupy (a) placa zalogom 143 tys. zl dziennie [P, doba 364] - to one trzymaja twierdze i przez to nie maja wojska w polu;
   - albo: nowy podatek idzie w renty tylko dla rodow "na miare srodkow" (dzis 80 rodow z lennem, 11 827 ludzi w druzynach, L129098), nie wedlug lenn dla wszystkich.
4. **Tarczowe: pan, ktorego stac, a nie wystawia wojska, placi koronie.** 167 rodow w wojnie bez luki (a) ma w kiesach 24.9 mln przy rezerwie wojny 15.7 mln, czyli 9.2 mln ponad rezerwe [P, CSV kolumny G i R, doba 364, skrypt c2_luka28]. To zbiornik, nie strumien - wystarczy na rok po ok. 25 tys. zl dziennie [S]. Historycznie poprawne (oplata w miejsce sluzby), ale mniejsze niz pkt 1.
5. **Nie tylko podatek - tanszy zolnierz.** Premia zoldu jazdy x1.5 to 40 tys. zl dziennie (475 979 zl ze stawkami wobec 435 747 bez premii, L129200) [P]. Zolnierz drozeje z tierem: 4.79 -> 6.17 zl. To nie moja decyzja, tylko liczba do rachunku.

Razem pkt 1 + 2 daja trwale ok. 70-90 tys. zl dziennie [S]. Dowiezione celowanie (pkt 3) wystarcza na 85 tys. z zapasem i zbliza do 95 tys. Dowiezione dzisiejsza droga - daje +2 do +9 tys. ludzi, czyli 81-88 tys.
Dwie uwagi:
- "Na czas wojny" znaczy w tym swiecie "prawie zawsze": 26 z 29 krolestw bylo w wojnie 342-364 doby z 364.
- Zadna z tych liczb nie byla sprawdzona biegiem. To rachunek. Potrzebny bieg 120 dob z pomiarem: lokata ludu, "stac jedzenie", rody na pulapie, druzyny w wojnie.

## 8. Czego nie wiem

- Rozklad wojska na tiery t1..t6 (log daje sredni tier i udzial t3+).
- Ilu rannych przypada na same druzyny lordow.
- Ilu ochotnikow stoi u notabli (brak pomiaru wprost; poprzedni triaz szacowal 10-19 tys.).
- Dlaczego konkretny rod z wolnym miejscem nie werbuje w danej dobie (poza Sarnorem).
- Ilu dowodcow w niewoli ma kazdy rod - grupa (d) jest policzona posrednio z limitu druzyn.
- Czy wojsko wroci po bitwach doby 364.
- Czy przy tym kodzie jest w ogole stan staly - po roku liczba rodow na pulapie nadal rosnie.
- To jeden bieg. Rozrzutu miedzy kampaniami nie znam.

## CO TRZYMA SENS

1. **Zold jest placony.** Zalegly zold to 0.12% zlota (prog 2%; progi-rok5\etap2.txt wiersz 10). Korona wyplaca 100% naleznego zwrotu zoldu (L129101).
2. **Runda 3 zadzialala.** Druzyny w wojnie 79 409 wobec 64 013 w poprzednim biegu (+24%). Zalogi 83 593 wobec 74 122. Zwolnienia z druzyn 332 wobec 520 dziennie. Glowy rodow ponizej 5 000 zl: 5 wobec 21. Kompanie najemne maja 5.6 mln zamiast 20.6 mln.
3. **Dezercja jest mala i ma przyczyny.** 17 ludzi dziennie z druzyn (glod 6, limit gry 5, zalegly zold 3, morale 2). Morale srednio 68.9.
4. **Nic z niczego, nic w nicosc.** Nowe druzyny biora ludzi z wolnych rak regionu siedziby (577 = 577, L129163). Zwolnieni ida do wsi i karczm, "zniklo 0" (L129141). Rozbici: do domu, w las, do karczmy, kontrola kodu 0 (L129134). Pieniadz swiata 207.9 -> 208.3 mln.
5. **Ksiega ludzi trzyma.** Pod bronia 1.16-1.31% mezczyzn 16-60. Ludnosc -1.0% w roku. Hamulec wolnych rak dziala w 8-13 regionach z 227, tam gdzie ludzi naprawde zabraklo (Sarnor 22.4%).
6. **Niewola nie blokuje wojny.** Srednio 42 lordow w niewoli, mediana 4 doby, nikt ponad 60 dni.
7. **Zalogi sa stabilne i uzbrojone.** 80-84 tys. od doby 91. Glowna bron 98%, zbroja 89%. Zadna warownia nie gloduje (L129266).
8. **Uzbrojenie druzyn poprawia sie caly rok.** Helm 44% -> 66%, tarcza 38% -> 73%, bron t3+ 40% -> 70%.
9. **Bitwy w polu maja ludzkie straty.** Ostatnia doba: przegrani 22.9% zabitych, zwyciezcy 4.6% (L128988), w pasmie historycznym.
10. **Wojsko dojrzewa.** Sredni tier druzyn 2.49 -> 2.96, t3+ 41% -> 59%.
11. **Bandy nie rosna bez konca.** 17.1 tys. ludzi wobec 20.0 tys. w poprzednim biegu, tier 2.30.
12. **Podatek wojenny reaguje na potrzebe.** Stawka doszla do pulapu w 20 krolestwach, gdy skarbce spadly ponizej zapasu wojny (L129100).

## NIESPOJNOSCI

| nr | co | dowod | waga | co by to naprawilo (jedno zdanie) |
|---|---|---|---|---|
| A | Wojsko w polu jest ponizej podlogi 85 tys. i dalej powoli topnieje, bo coraz wiecej rodow dochodzi do pulapu zoldu. | 79 409 srednio, szczyt 108 131 w dobie 68; -36 ludzi/dobe od doby 183; rody na pulapie 72 -> 110; 71% luki to pulap (pkt 5). | wysoka | Dodatkowy pieniadz wojenny skierowany do rodow na pulapie: 30-45 tys. zl dziennie na 85 tys. |
| B | Zalogi zjadaja budzet wojska polowego: sa oplacane pierwsze i jest w nich wiecej zawodowych zolnierzy niz w polu. | Zalogi 83 593, druzyny krolestw 74 456 (doba 364); 16 rodow z zoldem zalog ponad caly pulap ma w polu 58 ludzi z 9 766; rody na pulapie placa zalogom 117-143 tys. zl dziennie. | wysoka | Korona doplaca w wojnie do zoldu zalog (dzis zwraca tylko zold druzyn), zeby pan twierdzy mial za co wyjsc w pole. |
| C | Pieniadz wojny staje u ludu miast, a korony i panowie ubozeja; doplata ze skarbcow gasnie. | Kiesy ludu 11.0 -> 44.1 mln, +70 tys./dobe; skarbce 54.6 -> 27.1 mln; doplata 112 -> 35 tys./dobe; renty 310 -> 141 tys./dobe (pkt 7). | wysoka | Danina wojenna takze z nadwyzki kies ludu miast (dzis placa tylko kasy kupcow i wsie). |
| D | Pieniadz korony idzie do wszystkich panow po rowno wedlug lenn, takze do tych, ktorzy maja nadmiar; 1 zl podatku daje 0.45-0.64 zl zoldu tam, gdzie go brakuje. | 85 rodow z wolnym miejscem: zold 48% pulapu, kiesy 25.8 mln; 36% rent trafia poza grupe (a) (pkt 5.2, 7.3). | srednia | Renty albo zwrot liczone wedlug potrzeby wojska (rody na miare srodkow), nie tylko wedlug lenn. |
| E | Wojna nie ma konca i prawie nie ma rozstrzygniec, a zuzywa ludzi jak mlyn. | 26 z 29 krolestw w wojnie 342-364 doby; 100 wojen, 86 pokojow, 73 razy nowa wojna w dobie pokoju (wola ROT EnforceWars, L129278); 1 869 bitew lordow, 14 twierdz zdobytych z 227 (L129204); w druzynach zginelo 146 594 ludzi przy srednim stanie 88 tys. | srednia | Rozejm po pokoju, ktorego fabula ROT nie moze zerwac tego samego dnia (to sprawa rozdzialu o wojnie). |
| F | Jazdy jest o jedna trzecia za malo, choc konie i zloto sa. | Jazda t3+ 17.6% przy celu 27.5%; 5 453 czeka na konia; 4 133 odrzucone proby dziennie; 2 328 wolnych koni w zbrojowniach; nieudane zakupy: brak koni 21, brak zlota 2 (L129201-129203). | srednia | Pozwolic wsiasc na wolnego konia sasiedniej kategorii albo dowozic wolne konie zbrojowni do druzyn, ktore czekaja. |
| G | Druzyny sa niedozbrojone, a pieniadz na sprzet przepada niewydany. | Zbroja szczebla 70%, helm 66%, rekawice 61%; brak 131 386 sztuk; przydzial sprzetu 239 797 zl, wydane 123 386, 92 162 przepada na dwor; cwiczenia x0.82 (L129173, L129141, L129172). | srednia | Sprawdzic podaz zbroi t3+ na targach (rozdzial o gospodarce) - bez towaru pieniadz nic nie kupi. |
| H | Wypad z oblezonej twierdzy konczy sie strata calej armii: kto przegra, traci 100% ludzi. | Doba 364: 2 148 i 1 259 ludzi Dorzecza straconych w calosci (L25400, L25373); regula strat H3 obejmuje tylko bitwy w polu (7 bitew pominietych jako "nie w polu", L128988); w roku zwyciezcy tracili srednio 12.6% zabitych, przegrani 41.6% (linie "Bitwy"). | srednia | Objac wypady i szturmy regula H3: przegrany wypad wraca za mury, zamiast isc w calosci do niewoli. |
| I | "Zalegly zold w partiach" przekracza prog. | 2.49% partii przy progu 2% (progi-rok5\etap2.txt wiersz 11); w zlocie tylko 0.12%. | niska | Dotyczy kilkunastu malych druzyn dziennie - zniknie razem z A. |
| J | Kompanie najemne stoja ponizej umowy i dalej odkladaja zloto. | 2 190 ludzi przy 2 970 z umowy (74%); kiesy 3.8 -> 5.6 mln (CSV, L129095). | niska | Kontrakt placony za ludzi juz jest; do obserwacji, czy kiesy stana. |
| K | Trzy krolestwa prawie bez wojska polowego. | Free Folk 10% limitu, Ibben 13%, Iron Islands 19% (pkt 2); krol Ibben ma 0 ludzi w polu. | srednia | To skutek A i B - naprawa tam. |

Ocena calosci: logika wojska jest spojna (ludzie, zold i sprzet maja pochodzenie i odbiorce, przyczyny dezercji i strat sa nazwane). Niespojna jest skala: swiat zamkniety nie utrzymuje dzis wojska, pod ktore strojono pasmo 95-115 tys., a pieniadz, ktory moglby je utrzymac, odklada sie u ludu miast.
