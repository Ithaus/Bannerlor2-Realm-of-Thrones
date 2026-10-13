# PIENIADZ RODOW I KORON - bieg roczny 364 doby (WGRANIE 36 + runda 3)

Rozdzial raportu z testu rocznego. Klucz: pieniadz. Tylko odczyt logow i tabel - nic nie zmieniono w grze.

## 0. W skrocie

- Swiat jest zamkniety. Zloto swiata: 208.20 mln w dobie 1, 208.34 mln w dobie 364. Zmiana w roku +130 882 zl, czyli +0.06% [P].
- Rody zarabiaja razem 1 239 tys. zl na dobe, z tego na zold idzie 633 tys. (51%) [P]. Zloto rodow spadlo z 59.6 do 52.7 mln (-19 tys. na dobe) [P].
- Korony zarabiaja 444 tys. na dobe i wydaja wszystko: 264 tys. na zwrot zoldu, 215 tys. na renty dla panow [P]. Do tego dokladaja z zapasu: skarbce spadly z 57.9 do 27.1 mln [P].
- Jeden zbiornik rosnie caly rok: kiesy mieszczan, z 6.3 do 44.1 mln (+104 tys. na dobe, pod koniec +70 tys.) [P]. To jest to samo zloto, ktore wyszlo ze skarbcow koron.
- Wojsko w druzynach stoi od ok. 225. doby na 78-83 tys. (srednie 28 dob) [P]. W poprzednim biegu spadalo do konca (63 tys.).
- Poprawki rundy 3 zadzialaly w pieniadzach: kompanie najemne maja 5.6 mln zamiast 20.6 mln, rodow z lennem bez renty jest 12 zamiast 57 [P].
- Trzy rzeczy nie trzymaja sensu: (1) mieszczanie odkladaja zloto, ktorego nie maja na co wydac, a korony w tym czasie chudna; (2) ok. 21 tys. brakujacych zolnierzy miesci sie w budzecie swoich rodow, a mimo to ich nie ma - tu pieniadz nic nie zmieni; (3) okupy przenosza w roku 52 mln zl (45 mln miedzy rodami, 7 mln ze skarbcow) - tyle, ile wynosi cale zloto rodow.
- Odpowiedz na pytanie o podatki jest w rozdziale 8.

## 1. Skad sa liczby

| Skrot | Plik |
|---|---|
| LOG | D:\Backup-Bannerlord\logs\2026-10-13-noc34-35\Armoury-2026-10-13_06-24-10.log |
| LOG-S | D:\Backup-Bannerlord\logs\2026-10-13-noc34-35\Armoury-2026-10-13_02-15-21.log (poprzedni bieg, WGRANIE 34) |
| CSV-R | D:\Backup-Bannerlord\logs\2026-10-13-noc34-35\Logs\2026-10-13_06-24-10\budzet-rodow.csv |
| CSV-R-S | ...\Logs\2026-10-13_02-15-21\budzet-rodow.csv (poprzedni bieg) |
| CSV-M | ...\Logs\2026-10-13_06-24-10\kasy-miast.csv |
| PROGI | ...\scratchpad\test\progi-rok5\etap2.txt, etap3.txt, etap5.txt, etap8.txt |
| SKRYPTY | ...\scratchpad\raport-rok\skrypty\pieniadz\ (00-16, wyniki w podkatalogu wyniki\) |

Jak czytac:
- [P] = pomiar z logu albo tabeli (takze suma i srednia z pomiarow). [S] = szacunek (reszta z odejmowania, przeliczenie, prognoza).
- Doba 1 = pole "dzien" 108836 w linii "Pieniadz swiata" (dzien gry 108837). Doba 364 = "dzien" 109199. Linie "Korona", "Skarbce", "Budzet rodow (166)" pisza dzien o 1 wiekszy (ta sama doba).
- "Na dobe (rok)" = srednia z 364 dob (linie "Obieg" i bilansu: 363 doby, bo pierwsza doba nie ma przeplywow).
- Rody: 318 rodow AI. Bez rodu gracza (stoi) i bez 28 pseudo-rodow "Courtiers of ..." (dworzanie BK, razem 174 645 zl i 216 ludzi w dobie 364 [P], CSV-R).
- Zloto rodu (kolumna G) = kiesy wszystkich doroslych lordow rodu. Linia "Pieniadz swiata" dzieli to na "glowy rodow" i "pozostali lordowie".
- Wplywy rodu (kolumna wplyw_doby) = wszystko, co weszlo do kies rodu spoza rodu w danej dobie, przed zoldem.

## 2. Rody: ile zarabiaja i wydaja

### 2.1 Wszystkie rody razem (318 rodow, srednio na dobe w roku)

| Pozycja | zl na dobe | Udzial | Zrodlo |
|---|---|---|---|
| **WPLYWY razem** | **1 238 965** | 100% | [P] CSV-R wplyw_doby |
| Korona razem | 496 626 | 40% | [P] CSV-R d_korona (13 okien 28 dob) |
| - zwrot zoldu od korony (70% zoldu druzyn w wojnie) | 262 753 | 21% | [P] CSV-R zwrot_korony |
| - renty korony wedlug lenn (180) | 216 852 | 18% | [P] CSV-R renta_180 |
| - dary koron (182; Wolne Miasta -> Dothrakowie) | 16 820 | 1% | [P] CSV-R mennica_monopole |
| Lupy, okupy, trzecia lorda (jednorazowe) | 335 022 | 27% | [P] CSV-R sr_jednorazowe (13 okien) |
| - sprzedaz lupu i jencow osadom, zloto z bitew | 186 862 | 15% | [P] CSV-R zdarzenia |
| - okupy otrzymane od innych rodow | 110 718 | 9% | [P] LOG "Okupy (178)" pole "okupy otrzymane", suma roku 40 301 325 |
| - trzecia lorda z nadwyzek ludzi | 31 903 | 3% | [P] CSV-R trzecia |
| Ziemia: podatek wsi, zawor kas miast i zamkow, cla (bez wlasnych pieniedzy) | 194 383 | 16% | [P] CSV-R d_ziemia (13 okien) |
| Kontrakt najemnikow (185) | 14 931 | 1% | [P] CSV-R d_kontrakt |
| Warsztaty i karawany rodow (majatek) | 44 | 0% | [P] CSV-R d_majatek |
| Wlasne pieniadze, ktore wrocily z kas wlasnych siedzib, przelewy w rodzie, inne | 197 960 | 16% | [S] reszta z odejmowania; w tym "wlasne" wedlug pierscienia 103 928 [P] CSV-R sr_wlasne |
| **WYDATKI: zold razem** | **633 472** | 51% wplywow | [P] CSV-R |
| - zold druzyn | 443 647 | 36% | [P] CSV-R zold_partii |
| - zold zalog | 183 398 | 15% | [P] CSV-R zold_zalog |
| - zold karawan lordow | 6 426 | 0.5% | [P] CSV-R zold_karawan |
| **Inne wydatki netto** | **624 654** | 50% | [S] wplywy - zold - zmiana zlota |
| - dwor: wplaty do kas wlasnych siedzib (w tym niewydany przydzial na sprzet 83 730) | 187 008 | | [P] LOG "Obieg" pole "dwor (166/162m)" |
| - okupy zaplacone innym rodom gotowka (bez okupow krolow ze skarbcow) | 110 973 | | [P] LOG linie "Okupy (178): AI-AI", suma roku 40 394 053 |
| - sprzet kupiony przez panow | 115 411 | | [P] LOG "Budzet rodow (166)" pole "wydane przez panow" |
| - werbunek (ochotnicy od notabli, najemnicy z karczm) | 36 062 | | [P] LOG bilans - przyczyny, suma roku 13 126 708 |
| - 1/9 lupu i okupow do skarbcow (178) | 28 778 | | [P] LOG "Obieg" |
| - podatek BK od rodow do skarbcow | 24 250 | | [P] LOG "Obieg" |
| - raty i splaty w Banku 11 767, raty dlugu okupow 6 009 | 17 776 | | [P] LOG "IronBank" suma roku 4 283 246; LOG "Obieg" pole "okupy do porywaczy" |
| - awanse ludzi | 8 709 | | [P] LOG bilans - przyczyny |
| - rada, powinnosci, budowy, zold rycerzy | 14 992 | | [P] LOG "Obieg" i "rody - przyczyny" |
| - reszta: zakupy druzyn na targach (zywnosc, konie), zloto stracone w przegranych bitwach | ok. 81 000 | | [S] nie rozbite w danych - nie wiem dokladniej |
| **SALDO (zmiana zlota rodow)** | **-19 160** | | [P] CSV-R G: 59 612 411 -> 52 657 242 |

Wazne liczby do zapamietania:
- Sama ziemia (194 tys. srednio, 163 tys. pod koniec roku) starcza ledwie na zold zalog (183 tys. srednio, 191 tys. pod koniec roku). Cale wojsko polowe zyje z przelewow korony (480 tys.) i z lupu z okupami (335 tys.).
- Pod koniec roku ziemia daje 163 tys. na dobe (podatek wsi 75, zawor miast i renta wsi 65, cla 18, zawor zamkow 5) [P] LOG:129146 "D staly (169c)", srednia ostatnich 28 dob. Srednia roku jest wyzsza, bo w pierwszych 28 dobach kasy miast oddaly panom startowa nadwyzke.
- Warsztaty i karawany rodow nie daja nic (44 zl na dobe na caly swiat), a karawany lordow kosztuja 6 426 zl zoldu na dobe [P].

### 2.2 Krolestwa - suma rodow

Rod jest przypisany do krolestwa z doby 364. Zaden rod poza kompaniami najemnymi nie zmienil krolestwa w roku [P]. Kompanie najemne (11) sa w osobnym wierszu, bo zmieniaja pracodawce co kilka dob (762 nowe umowy w roku [P] LOG "Kontrakty najemnikow (185)").

| Krolestwo | Rodow | Wplywy/d | Ziemia | Korona (zwrot+renty+dary) | Kontrakt | Lupy i okupy | Wlasne i inne | Zold druzyn | Zold zalog | Inne wydatki [S] | Saldo/d | Zloto d1 (mln) | Zloto d364 (mln) | Rodow z zlotem w dol | Na suficie zoldu d364 | Ludzi w druzynach d1 -> d364 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| The North | 25 | 104 004 | 22 985 | 52 872 | 18 | 9 943 | 18 185 | 49 517 | 19 809 | 36 588 | -2 191 | 4.82 | 4.03 | 21 | 19 | 2374 -> 7631 |
| House Baratheon of King's Landing | 27 | 100 454 | 18 495 | 36 361 | 123 | 30 771 | 14 703 | 38 013 | 15 191 | 48 975 | -1 781 | 4.25 | 3.60 | 20 | 6 | 1138 -> 5975 |
| The Reach | 16 | 86 162 | 17 624 | 33 621 | 0 | 20 518 | 14 400 | 27 113 | 11 257 | 48 911 | -2 177 | 3.61 | 2.82 | 13 | 6 | 2000 -> 5808 |
| Dorne | 41 | 80 079 | 10 403 | 28 912 | 346 | 30 438 | 9 980 | 27 474 | 11 494 | 39 832 | 875 | 4.63 | 4.95 | 36 | 4 | 751 -> 4310 |
| Riverlands | 14 | 77 345 | 13 422 | 26 376 | 0 | 24 897 | 12 624 | 22 817 | 9 494 | 44 405 | -1 029 | 3.52 | 3.14 | 9 | 4 | 2918 -> 2397 |
| Stormlands | 13 | 76 100 | 12 391 | 30 894 | 0 | 21 497 | 11 317 | 24 116 | 9 013 | 45 066 | -2 178 | 3.01 | 2.22 | 11 | 3 | 2141 -> 5166 |
| The Vale | 20 | 70 811 | 15 564 | 37 069 | 22 | 3 933 | 14 223 | 36 277 | 11 413 | 25 958 | -2 861 | 3.31 | 2.27 | 17 | 18 | 1887 -> 5453 |
| House Targaryen | 8 | 64 090 | 6 553 | 20 529 | 17 | 28 564 | 8 428 | 21 108 | 8 849 | 29 725 | 4 349 | 1.63 | 3.21 | 4 | 3 | 1132 -> 2203 |
| Dothraki Horde | 16 | 62 659 | 7 001 | 38 907 | 91 | 7 707 | 8 952 | 14 957 | 6 379 | 40 973 | 136 | 2.09 | 2.14 | 4 | 0 | 704 -> 2982 |
| Dragonstone | 10 | 52 035 | 5 119 | 16 568 | 0 | 24 787 | 5 561 | 17 072 | 7 782 | 28 995 | -2 187 | 2.40 | 1.61 | 9 | 5 | 1961 -> 3402 |
| Iron Islands | 29 | 38 037 | 7 284 | 19 344 | 0 | 2 845 | 8 563 | 17 158 | 12 720 | 12 931 | -5 130 | 3.47 | 1.61 | 28 | 28 | 1209 -> 1400 |
| Volantis | 6 | 36 368 | 7 797 | 16 129 | 0 | 4 298 | 8 144 | 15 952 | 7 360 | 13 465 | -639 | 1.45 | 1.22 | 4 | 4 | 1168 -> 3078 |
| Qarth | 4 | 36 102 | 4 592 | 13 180 | 0 | 9 847 | 8 483 | 11 872 | 4 340 | 18 211 | 1 678 | 1.16 | 1.77 | 0 | 0 | 363 -> 1811 |
| Kompanie najemne | 11 | 33 794 | 0 | 0 | 14 302 | 17 488 | 2 004 | 11 774 | 0 | 14 313 | 7 706 | 2.85 | 5.65 | 1 | 4 | 1266 -> 2184 |
| Lorath | 5 | 32 547 | 3 713 | 12 377 | 0 | 11 223 | 5 234 | 9 757 | 3 908 | 18 501 | 343 | 1.17 | 1.29 | 3 | 1 | 769 -> 1580 |
| Qohor | 5 | 29 233 | 3 735 | 11 480 | 0 | 9 533 | 4 486 | 11 949 | 3 927 | 14 984 | -1 962 | 1.21 | 0.50 | 4 | 4 | 277 -> 1342 |
| Tyrosh | 5 | 28 105 | 3 090 | 9 252 | 0 | 11 823 | 3 940 | 7 293 | 3 691 | 16 931 | 131 | 1.22 | 1.27 | 1 | 0 | 278 -> 2315 |
| Myr | 5 | 28 085 | 4 619 | 9 846 | 0 | 9 775 | 3 845 | 10 823 | 4 134 | 14 063 | -1 099 | 1.19 | 0.79 | 5 | 2 | 573 -> 1706 |
| Braavos | 5 | 26 023 | 3 299 | 7 748 | 0 | 9 999 | 4 978 | 9 509 | 3 937 | 13 917 | -1 470 | 1.22 | 0.68 | 5 | 3 | 1028 -> 1481 |
| House Targaryen, Aegon | 5 | 25 029 | 4 388 | 9 610 | 0 | 5 575 | 5 456 | 10 159 | 4 648 | 11 329 | -1 190 | 1.27 | 0.84 | 5 | 2 | 273 -> 1354 |
| Norvos | 5 | 24 917 | 3 793 | 9 313 | 0 | 7 370 | 4 442 | 8 554 | 2 831 | 14 975 | -1 626 | 1.21 | 0.62 | 4 | 2 | 683 -> 1243 |
| Lys | 5 | 24 039 | 2 927 | 8 020 | 0 | 10 127 | 2 965 | 6 889 | 2 803 | 15 574 | -1 507 | 1.18 | 0.64 | 3 | 2 | 291 -> 1055 |
| Sarnor | 5 | 23 381 | 3 722 | 10 754 | 0 | 4 207 | 4 698 | 8 261 | 2 107 | 12 369 | 504 | 1.20 | 1.39 | 3 | 0 | 316 -> 1106 |
| Pentos | 11 | 21 348 | 2 881 | 6 414 | 5 | 8 349 | 3 700 | 6 890 | 4 774 | 10 810 | -1 336 | 1.52 | 1.03 | 10 | 3 | 398 -> 1271 |
| Ibben | 4 | 15 753 | 1 454 | 6 827 | 0 | 4 847 | 2 625 | 5 709 | 4 262 | 8 052 | -2 271 | 1.14 | 0.32 | 4 | 3 | 362 -> 332 |
| Nights Watch | 5 | 15 287 | 2 785 | 7 177 | 0 | 3 072 | 2 253 | 3 856 | 846 | 10 450 | 136 | 1.35 | 1.40 | 2 | 0 | 1172 -> 4058 |
| Free Folk | 10 | 14 300 | 3 381 | 7 870 | 7 | 1 354 | 1 687 | 6 693 | 5 476 | 5 787 | -3 660 | 1.85 | 0.52 | 9 | 9 | 1324 -> 508 |
| Skagos | 1 | 4 472 | 595 | 2 869 | 0 | 105 | 901 | 643 | 369 | 3 040 | 419 | 0.22 | 0.37 | 0 | 1 | 139 -> 18 |
| Yi Ti Exiles | 1 | 4 278 | 419 | 3 162 | 0 | 61 | 628 | 755 | 296 | 2 772 | 456 | 0.21 | 0.38 | 0 | 1 | 53 -> 67 |
| Summer Isles | 1 | 4 130 | 354 | 3 145 | 0 | 68 | 555 | 686 | 290 | 2 753 | 401 | 0.21 | 0.36 | 0 | 1 | 58 -> 44 |
| **SWIAT** | 318 | 1 238 965 | 194 383 | 496 626 | 14 931 | 335 022 | 197 960 | 443 647 | 183 398 | 624 654 | -19 160 | 59.61 | 52.66 | 235 | 138 | 29006 -> 73280 |

Zrodlo: [P] CSV-R, skrypt 09_tabele_md.py. Kwoty na dobe = srednia roku. "Ziemia", "Korona", "Kontrakt", "Lupy i okupy" z kolumn d_ziemia, d_korona, d_kontrakt, sr_jednorazowe (13 okien po 28 dob). "Wlasne i inne" oraz "Inne wydatki" = reszta z odejmowania [S]. "Rodow z zlotem w dol" = G w dobie 364 mniejsze niz w dobie 1. "Na suficie zoldu" = zold (druzyny + zalogi + karawany) co najmniej 95% pulapu zoldu z budzetu rodu w dobie 364.

Co widac:
- 235 z 318 rodow ma mniej zlota niz na starcie. W ostatnich 28 dobach zloto spada juz tylko u 145 rodow, czyli u polowy [P].
- 138 rodow stoi na suficie zoldu [P]. Najgorzej: Zelazne Wyspy 28 z 29, Dolina 18 z 20, Wolni Ludzie 9 z 10, Polnoc 19 z 25.
- Zelazne Wyspy: wplywy 38.0 tys., zold 29.9 tys. (79%), w tym zalogi 12.7 tys. - wiecej niz cala ziemia (7.3 tys.). Lup tylko 2.8 tys. na dobe. Zloto 3.47 -> 1.61 mln. W druzynach 1 400 ludzi na 29 rodow [P].
- Wolni Ludzie: zloto 1.85 -> 0.52 mln, druzyny 1 324 -> 508 ludzi [P].
- Dolina i Polnoc prawie nie maja lupu (3.9 i 9.9 tys. na dobe) i wydaja na zold 67% wplywow [P].
- Zloto rosnie tam, gdzie jest lup i okupy: House Targaryen +4.3 tys. na dobe (lup 28.6 tys.), Qarth +1.7 tys., Dorne +0.9 tys. [P].

### 2.3 Najbogatsi i najbiedniejsi

**15 najbogatszych rodow (doba 364)**

| Rod | Krolestwo | Lenno | Zloto d1 | Zloto d364 | Saldo/d | Wplywy/d | Zold/d | Ludzi druzyny / limit | Ludzi zalogi | Pulap zoldu d364 | Zold d364 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Stormcrows | House Targaryen | zamek | 229 033 | 1 522 385 | 3 563 | 11 648 | 1 906 | 469 / 697 | 211 | 28 639 | 3 443 |
| Fowler | Dorne | zamek | 199 651 | 1 335 895 | 3 130 | 10 269 | 2 352 | 242 / 460 | 230 | 24 276 | 1 517 |
| Bolton | The North | miasto | 351 685 | 1 234 137 | 2 431 | 16 256 | 8 126 | 1135 / 1137 | 1065 | 27 367 | 13 363 |
| Tully | Riverlands | miasto | 436 689 | 1 068 066 | 1 739 | 17 351 | 6 094 | 169 / 234 | 461 | 22 295 | 6 511 |
| Moon Brothers | Summer Isles | najemnik | 270 000 | 948 856 | 1 870 | 3 887 | 808 | 140 / 540 | 0 | 785 | 820 |
| Baratheon,Renly | Stormlands | miasto | 418 523 | 894 275 | 1 311 | 20 155 | 7 562 | 1366 / 1493 | 1745 | 20 749 | 8 999 |
| Tyrell | The Reach | miasto | 417 847 | 816 690 | 1 099 | 20 466 | 6 062 | 1459 / 2268 | 1400 | 19 641 | 7 990 |
| Long Lances | Skagos | najemnik | 267 831 | 733 588 | 1 283 | 4 330 | 1 846 | 219 / 595 | 0 | 1 359 | 2 136 |
| Targaryen | House Targaryen | miasto | 412 423 | 674 432 | 722 | 21 774 | 13 834 | 759 / 2246 | 612 | 15 446 | 14 972 |
| Vaith | Dorne | miasto | 252 713 | 635 120 | 1 053 | 9 668 | 4 448 | 635 / 763 | 619 | 14 560 | 5 405 |
| Wild Hares | Myr | najemnik | 269 670 | 611 874 | 943 | 3 207 | 1 214 | 190 / 695 | 0 | 2 604 | 1 401 |
| Stark | The North | miasto | 350 231 | 606 571 | 706 | 12 388 | 6 796 | 979 / 981 | 1017 | 14 595 | 8 945 |
| Qar Deeth | Qarth | miasto | 271 951 | 597 715 | 897 | 10 637 | 4 213 | 63 / 433 | 567 | 12 548 | 1 723 |
| Brave Companions | Summer Isles | najemnik | 261 077 | 595 698 | 922 | 2 594 | 728 | 222 / 703 | 0 | 814 | 1 121 |
| Faith Militant | Summer Isles | najemnik | 229 045 | 551 472 | 888 | 2 071 | 687 | 122 / 391 | 0 | 714 | 695 |

15 najbogatszych rodow ma 12.83 mln zl, czyli 24.4% zlota wszystkich rodow. W druzynach maja 8 169 ludzi przy limicie 13 636 [P] CSV-R. W poprzednim biegu 15 najbogatszych mialo 42.1% (26.2 mln) [P] CSV-R-S.

**15 najbiedniejszych rodow (doba 364)**

| Rod | Krolestwo | Lenno | Zloto d1 | Zloto d364 | Saldo/d | Wplywy/d | Zold/d | Ludzi druzyny / limit | Ludzi zalogi | Pulap zoldu d364 | Zold d364 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Cuy | The Reach | zamek | 195 717 | 20 234 | -483 | 2 160 | 1 298 | 83 / 435 | 288 | 803 | 825 |
| Sontho | Pentos | zamek | 223 824 | 20 140 | -561 | 1 625 | 1 279 | 3 / 545 | 293 | 270 | 581 |
| Moesia | Pentos | zamek | 182 571 | 20 045 | -448 | 876 | 860 | 0 / 493 | 235 | 264 | 340 |
| Banu Khashal | Dorne | bez lenna | 39 272 | 19 227 | -55 | 17 | 0 | 0 / 0 | 0 | 17 | 0 |
| Banu Zus | Dorne | bez lenna | 39 132 | 18 812 | -56 | 38 | 0 | 0 / 0 | 0 | 17 | 0 |
| Blackmont | Dorne | bez lenna | 179 312 | 17 536 | -446 | 1 446 | 1 020 | 10 / 316 | 0 | 24 | 59 |
| Ambrose | The Vale | bez lenna | 39 767 | 17 440 | -62 | 4 | 0 | 0 / 0 | 0 | 0 | 0 |
| Moore | The Vale | bez lenna | 39 767 | 16 509 | -64 | 4 | 0 | 0 / 0 | 0 | 0 | 0 |
| Orthys | Lys | zamek | 224 526 | 14 363 | -579 | 1 684 | 906 | 0 / 347 | 127 | 21 | 416 |
| Kasgos | Norvos | zamek | 222 570 | 14 295 | -574 | 3 778 | 1 064 | 36 / 197 | 319 | 621 | 875 |
| Banu Kinyan | Dorne | bez lenna | 39 272 | 14 276 | -69 | 14 | 0 | 0 / 0 | 0 | 16 | 0 |
| Lydden | House Baratheon of King's Landing | zamek | 216 611 | 13 626 | -559 | 3 058 | 1 614 | 32 / 515 | 4 | 171 | 269 |
| Banu Dilis | Dorne | bez lenna | 38 207 | 11 960 | -72 | 14 | 0 | 0 / 0 | 0 | 16 | 0 |
| Banu Laikh | Dorne | bez lenna | 38 207 | 8 193 | -83 | 16 | 0 | 0 / 0 | 0 | 10 | 0 |
| Banu Rawi | Dorne | bez lenna | 38 207 | 7 617 | -84 | 13 | 0 | 0 / 0 | 0 | 9 | 0 |

Rody "Banu ..." (Dorne) i rody bez lenna maja po 8-20 tys. zl, 0 ludzi i dochod 13-38 zl na dobe. To rody bez ziemi i bez wojska - ich kiesa topnieje o 55-84 zl dziennie [P].

**15 najbiedniejszych rodow z lennem (doba 364)**

| Rod | Krolestwo | Lenno | Zloto d1 | Zloto d364 | Saldo/d | Wplywy/d | Zold/d | Ludzi druzyny / limit | Ludzi zalogi | Pulap zoldu d364 | Zold d364 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Sixskins | Free Folk | zamek | 170 128 | 29 980 | -386 | 1 535 | 1 348 | 56 / 275 | 335 | 823 | 925 |
| Mertyns | Stormlands | zamek | 193 298 | 28 257 | -455 | 3 072 | 1 434 | 115 / 494 | 279 | 1 350 | 1 086 |
| Ostyrion | Qohor | zamek | 197 280 | 27 060 | -469 | 4 420 | 1 562 | 217 / 672 | 177 | 1 890 | 1 522 |
| Velaryon | Dragonstone | miasto | 260 682 | 24 879 | -650 | 5 289 | 2 993 | 0 / 213 | 592 | 563 | 1 990 |
| Tendiras | Qohor | zamek | 227 272 | 24 863 | -558 | 2 182 | 1 606 | 0 / 721 | 355 | 438 | 609 |
| Heggo | Dothraki Horde | zamek | 208 394 | 24 572 | -506 | 5 270 | 1 947 | 248 / 525 | 191 | 4 062 | 2 254 |
| Chovaqqo | Dothraki Horde | zamek | 172 804 | 24 182 | -409 | 5 233 | 2 647 | 380 / 678 | 196 | 2 981 | 2 097 |
| Forrester | The North | zamek | 184 920 | 22 248 | -448 | 2 745 | 1 644 | 58 / 459 | 34 | 1 079 | 550 |
| Mormont, Jorah | House Targaryen | miasto | 212 407 | 21 739 | -525 | 5 235 | 3 317 | 113 / 632 | 264 | 2 455 | 3 667 |
| Cuy | The Reach | zamek | 195 717 | 20 234 | -483 | 2 160 | 1 298 | 83 / 435 | 288 | 803 | 825 |
| Sontho | Pentos | zamek | 223 824 | 20 140 | -561 | 1 625 | 1 279 | 3 / 545 | 293 | 270 | 581 |
| Moesia | Pentos | zamek | 182 571 | 20 045 | -448 | 876 | 860 | 0 / 493 | 235 | 264 | 340 |
| Orthys | Lys | zamek | 224 526 | 14 363 | -579 | 1 684 | 906 | 0 / 347 | 127 | 21 | 416 |
| Kasgos | Norvos | zamek | 222 570 | 14 295 | -574 | 3 778 | 1 064 | 36 / 197 | 319 | 621 | 875 |
| Lydden | House Baratheon of King's Landing | zamek | 216 611 | 13 626 | -559 | 3 058 | 1 614 | 32 / 515 | 4 | 171 | 269 |

15 najbiedniejszych rodow z lennem ma razem 330 483 zl i 1 341 ludzi w druzynach przy limicie 7 201 [P] CSV-R. Wszystkie zaczynaly z 170-260 tys. zl.

### 2.4 Rozklad zlota rodow

| Doba | Rodow | Zloto razem (mln) | Min | 25% | Mediana | 75% | 90% | Max | Gorne 10% rodow ma | Dolna polowa ma | Rodow < 20 tys. | Rodow > 1 mln |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 318 | 59.61 | 38 207 | 64 499 | 210 941 | 238 490 | 291 607 | 436 689 | 19.7% | 29.6% | 0 | 0 |
| 364 | 318 | 52.66 | 7 617 | 35 269 | 100 543 | 210 754 | 377 284 | 1 522 385 | 39.4% | 13.6% | 12 | 4 |

Zrodlo: [P] CSV-R kolumna G, skrypt 09_tabele_md.py.
- Mediana spadla o polowe (211 -> 101 tys.). Gorne 10% rodow ma 39.4% zlota (na starcie 19.7%). W poprzednim biegu bylo 56.9% [P] CSV-R-S.
- Zaden rod nie ma mniej niz 5 000 zl w calej rodzinie. Glow rodow z kiesa ponizej 5 000 jest 5 (prog <= 10) [P] PROGI etap2.txt wiersz 3.
- Bankrutow: 0. Rodow w zajeciu dochodu: 1 [P] LOG:129143 "Dlugi (168)".

### 2.5 Kto ma zloto, a kto wojsko

Podzial rodow wedlug tego, co trzyma ich druzyny. Srednia z ostatnich 28 dob, klasa liczona w kazdej dobie [P] CSV-R, skrypt 11_kto_dostaje_korone.py.

| Grupa | Rodow | Zloto (mln) | Ludzi w druzynach | Limit wielkosci druzyn | Wypelnienie | Zold druzyn /d | Zold zalog /d | Zold na czlowieka druzyny | Pulap zoldu /d | Dostaje rent | Dostaje zwrotu zoldu |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Ma wolne miejsce w pulapie zoldu, druzyna ponizej 90% limitu | 155 | 27.07 | 39 204 | 68 147 | 58% | 186 437 | 64 093 | 4.76 zl | 553 459 | 45% | 44% |
| Stoi na pulapie zoldu (zold >= 95% pulapu) | 133 | 12.51 | 25 856 | 75 797 | 34% | 177 967 | 102 098 | 6.88 zl | 252 744 | 43% | 39% |
| Pelna druzyna (>= 90% limitu) | 19 | 6.74 | 11 443 | 11 948 | 96% | 76 888 | 24 671 | 6.72 zl | 158 664 | 12% | 17% |
| Kompanie najemne | 11 | 5.61 | 2 190 | 6 742 | 32% | 14 940 | 0 | 6.82 zl | 19 867 | 0% | 0% |

- 133 rody trzyma pieniadz: placa wiecej, niz pozwala pulap, i odprawiaja ludzi. Budzet odprawia srednio 335 ludzi na dobe (ostatnie 28 dob), w roku 98 927 [P] LOG "Budzet rodow (166)". W poprzednim biegu 148 569 [P] LOG-S.
- 155 rodow ma wolne miejsce w pulapie (razem ok. 300 tys. zl dziennie) i 27 mln zl, a druzyny stoja na 58% limitu. Tak jest caly rok: doba 84 - 63%, doba 182 - 58%, doba 273 - 59%, doba 364 - 55% [P] skrypt 06_pulap_i_zapas.py.
- Rachunek rod po rodzie (srednia ostatnich 28 dob) [P/S] skrypt 14_luz_na_rod.py: do limitu druzyn brakuje na swiecie 83.9 tys. ludzi. 63.3 tys. z nich (75%) nie miesci sie w pulapie zoldu swojego rodu - tu trzyma pieniadz. 20.7 tys. (25%) rod moglby oplacic z wlasnego wolnego pulapu, a ich nie ma. W grupie z wolnym miejscem to 19.2 tys. z 28.9 tys. brakujacych; 56 rodow stac na cala brakujaca druzyne.
- Dlaczego rod, ktorego stac, nie dobiera ludzi - z danych o pieniadzu NIE WIEM (tempo naboru, straty, zachowanie AI - to rozdzial o wojsku). Wolny pulap jest tez nierowno rozlozony: najwiecej maja najbogatsi (Stormcrows 25 tys. zl dziennie luzu, Fowler 23 tys.).
- Rod na pulapie placi za czlowieka 6.88 zl, rod z luzem 4.76 zl (x1.45) [P]. Prog P3W2-4 (<= 1.25x) nie jest spelniony: 1.38x w oknie progow [P] PROGI etap8.txt wiersz 265.
- Zalogi kosztuja rody na pulapie 102 tys. zl na dobe, czyli 36% ich zoldu. Zwrot korony zalog nie obejmuje [P] LOG:129096 ("partie w polu bez zalog").

### 2.6 Okupy

| Miara (rok) | Wartosc | Zrodlo |
|---|---|---|
| Pojmania lordow | 2 919 (9.2 na rod) | [P] LOG:129147 "Niewola lordow i okupy (169c)" |
| Uwolnieni za okup | 1 260 (43%) | [P] tamze, suma dzienna |
| Uwolnieni inaczej: ucieczka 680, pokoj 528, po bitwie 334, z wyboru 41 | | [P] tamze |
| Wszystkie okupy: liczba / cena razem | 1 259 / 52 328 752 zl | [P] LOG "Okupy (178)" suma dzienna |
| - z tego gotowka / dlug okupu | 45 679 415 / 6 649 337 zl | [P] tamze |
| - miedzy rodami | 1 235 okupow, 45 278 661 zl (gotowka 40 394 053, dlug 4 884 608) | [P] linie "Okupy (178): AI-AI", skrypt 08 |
| Cena okupu: mediana / srednia / najwyzsza | 26 140 / 36 663 / 248 463 zl | [P] 1 235 linii "Okupy (178): AI-AI", skrypt 08 |
| Cena wobec dochodu rodu: lord 10 dni, glowa rodu 30 dni | | [P] tamze (mediana 10.0, 90% rodow 30.0) |
| - krolowie i dzieci krolow, placone ze skarbcow koron | 24 okupy, 6 997 179 zl (gotowka 5 232 450, dlug 1 764 729) | [P] LOG, linie "placi skarbiec", plik wyniki\wielcy_jency_nowy.csv |
| Dlug okupow rodow w dobie 364 | 2 703 390 zl | [P] LOG:129143 |
| Rodow, ktore zaplacily choc jeden okup / 10 i wiecej | 223 / 40 | [P] skrypt 08 |

- Okupy to drugi po koronie przeplyw w kiesach panow: 124 tys. zl na dobe miedzy rodami (144 tys. z okupami krolow). W roku przeszlo nimi 52.3 mln - tyle, ile wynosi cale zloto rodow (52.7 mln) [P].
- To przelew miedzy rodami, nic nie powstaje i nic nie ginie. Ale przenosi zloto od przegranych do wygranych: cwiartka rodow z lennem, ktora stracila najwiecej, oddala netto 14.8 mln; cwiartka, ktora zyskala najwiecej, wziela 15.7 mln, a jej zloto wzroslo z 12.8 do 17.5 mln [P] skrypt 08.
- Ten sam czlowiek jest wykupywany wiele razy. Glowa rodu Kasgos (Norvos): 11 okupow glowy, razem 31 okupow rodu za 787 103 zl - to 57% rocznych wplywow rodu (3 778 zl na dobe). Zloto rodu 222 570 -> 14 295 zl, druzyna 36 ludzi. Quarro (Norvos): 37 okupow, 1.57 mln, z tego 841 tys. na dlug. Glowa rodu Orthys (Lys): 11 okupow, zloto 224 526 -> 14 363, druzyna 0 [P] LOG np. :17188, :22488, :25649, :33041; CSV-R.
- Krol Braavos (Tago Celeris) byl pojmany 9 razy, skarbiec Braavos zaplacil 7 okupow za 1.44 mln. Skarbiec Krolewskiej Przystani zaplacil 2 okupy za 1.54 mln [P] wyniki\wielcy_jency_nowy.csv.

## 3. Korony (skarbce krolestw)

### 3.1 Wszystkie korony razem (srednio na dobe)

| Pozycja | Rok | Doby 1-182 | Doby 183-364 | Ostatnie 28 dob | Zrodlo |
|---|---|---|---|---|---|
| **WPLYWY DNIA razem** | **443 903** | 436 203 | 451 645 | 449 808 | [P] LOG "Obieg" pole "165: wplywy dnia" |
| - danina wojenna z kas osad | 202 832 | 186 354 | 219 401 | 217 563 | [P] LOG "Obieg" |
| - udzial korony w zaworze kas miast (polowa) | 119 461 | 130 092 | 108 772 | 112 732 | [P] |
| - 1/9 lupu i okupow lordow (178) | 28 778 | 23 177 | 34 410 | 30 788 | [P] |
| - podatek BK od rodow | 24 250 | 26 426 | 22 062 | 21 493 | [P] |
| - udzial korony w zaworze zamkow (1/3) | 15 806 | 15 279 | 16 336 | 14 332 | [P] |
| - clo od handlu miast | 11 866 | 13 962 | 9 758 | 9 683 | [P] |
| - **podatek wojenny ludu (184)** | **10 695** | 7 033 | 14 378 | 15 020 | [P] |
| - powinnosci wasali | 4 627 | 5 789 | 3 459 | 3 490 | [P] |
| - inne (w tym okupy krolow miedzy skarbcami 20 181) | ok. 25 600 | | | | [S] reszta |
| Dolozone z zapasu skarbca (1/360 nadwyzki ponad 0.5 mln dziennie) | 70 240 | 91 446 | 48 918 | 37 116 | [P] |
| Kredyt z Banku Zelaznego (186) | 8 432 | 4 324 | 12 563 | 11 448 | [P] |
| **WYDATKI** | | | | | |
| - zwrot zoldu panom | 263 585 | 253 905 | 273 317 | 277 190 | [P] |
| - renty panom wedlug lenn (180) | 214 725 | 240 771 | 188 535 | 172 002 | [P] |
| - dary (182) | 16 846 | 18 432 | 15 251 | 15 210 | [P] |
| - kontrakty kompanii najemnych (185) | 14 341 | 12 873 | 15 818 | 17 214 | [P] |
| - wezwania sojusznikow do wojny (168) | 4 931 | 1 959 | 7 920 | 1 576 | [P] |
| - raty kredytu do Banku | 4 105 | 1 313 | 6 911 | 9 616 | [P] |
| **Skarbce razem: zmiana na dobe** | **-84 901** | -108 314 | -61 359 | -49 303 | [P] |

Stan skarbcow: 57 927 181 (doba 1) -> 38 280 005 (doba 182) -> 27 108 168 zl (doba 364) [P] LOG:707, :64500, :129205.

Co widac:
- Korona wydaje co do zlota wszystko, co wplynelo danego dnia. Zwrot zoldu ma pierwszenstwo, renty dostaja reszte. Zwrot wyplacony w 99.8% (19 dob z niedoplata, razem 154 250 zl) [P] LOG "Korona: wplywy dnia (165)".
- Skarbce chudna szybciej (-85 tys.), niz wynosi doplata z zapasu (70 tys.). Roznice robia okupy krolow placone gotowka ze skarbcow: 5.23 mln w roku, czyli 14.4 tys. dziennie [P]. U odbiorcy wchodza do wplywow dnia i ida dalej na renty.
- Renty sa wiec tym, co spada: 221 tys. (doby 29-56) -> 216 tys. (doby 155-182) -> 172 tys. (ostatnie 28 dob) -> 141 tys. w ostatniej dobie [P] LOG "Renty korony (180)".
- W ostatniej dobie 36 rodow z lennem siedzi w krolestwach, gdzie po zwrocie zoldu nic nie zostaje na renty (doby 29-56: srednio 9) [P] LOG:129098.
- Zapas: ponad podloge 0.5 mln na krolestwo zostalo 12.6 mln (27.1 - 29 x 0.5). Regula 1/360 dziennie daje dzis 35 tys. i maleje o polowe co ok. 250 dob [S]. Gdy zapas sie skonczy, panom ubedzie ok. 35 tys. zl rent dziennie [S].
- Podatek wojenny ludu daje 2.4% wplywow koron. Stawka: srednio 2.4%, pulap 3% (6% z polityka War Tax). Na pulapie stoi 20 z 26 krolestw w wojnie - wyzej nie pojdzie [P] LOG:129100.

### 3.2 Krolestwa

| Krolestwo | Dob wojny | Skarbiec d1 (mln) | d182 | d364 | Wplywy dnia /d | w tym podatek wojenny ludu (184) | w tym z zaworu kas miast | Z zapasu (1/360) /d | Zwrot zoldu /d | Renty (180) /d | Najemnicy (185) /d | Wezwania (168) /d | Dary (182) /d | Rata Banku /d | Kredyt Banku /d | Dlug w Banku d364 | Kiesa krola d1 -> d364 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| The North | 355 | 2.00 | 1.41 | 1.04 | 47 845 | 1 334 | 15 262 | 2 625 | 30 504 | 22 318 | 0 | 0 | 0 | 52 | 111 | 24 022 | 332 286 -> 593 703 |
| House Baratheon of King's Landing | 355 | 2.01 | 1.41 | 0.66 | 36 946 | 1 357 | 10 909 | 2 305 | 24 593 | 11 769 | 207 | 0 | 0 | 248 | 679 | 174 530 | 322 537 -> 66 234 |
| The Vale | 342 | 2.00 | 1.41 | 1.05 | 34 566 | 670 | 9 223 | 2 628 | 21 507 | 15 562 | 119 | 0 | 0 | 0 | 0 | 0 | 333 486 -> 507 303 |
| The Reach | 342 | 2.00 | 1.41 | 0.80 | 32 046 | 109 | 10 786 | 2 518 | 17 317 | 16 284 | 0 | 0 | 0 | 9 | 30 | 8 431 | 370 277 -> 769 437 |
| Stormlands | 363 | 2.01 | 1.00 | 0.80 | 29 162 | 585 | 8 444 | 1 888 | 15 673 | 15 221 | 21 | 0 | 0 | 140 | 135 | 3 717 | 380 963 -> 878 567 |
| Dorne | 342 | 2.00 | 1.41 | 1.00 | 25 991 | 758 | 6 522 | 2 614 | 18 065 | 10 848 | 0 | 0 | 0 | 69 | 111 | 18 974 | 336 835 -> 525 899 |
| Riverlands | 277 | 2.00 | 1.41 | 1.05 | 25 007 | 210 | 8 901 | 2 628 | 13 176 | 13 200 | 324 | 916 | 0 | 12 | 20 | 3 509 | 359 230 -> 1 018 499 |
| Dothraki Horde | 342 | 2.00 | 1.41 | 1.06 | 20 347 | 0 | 6 199 | 2 651 | 8 966 | 13 079 | 842 | 0 | 16 820 | 0 | 0 | 0 | 373 824 -> 129 665 |
| House Targaryen | 342 | 2.00 | 1.41 | 1.05 | 18 341 | 602 | 4 209 | 2 630 | 11 558 | 8 984 | 417 | 0 | 0 | 37 | 53 | 7 906 | 344 815 -> 603 508 |
| Volantis | 342 | 2.00 | 1.40 | 1.05 | 17 233 | 132 | 4 525 | 2 626 | 8 993 | 7 154 | 603 | 0 | -2 978 | 29 | 43 | 6 260 | 313 261 -> 129 258 |
| Iron Islands | 342 | 2.00 | 1.41 | 1.05 | 16 974 | 510 | 4 319 | 2 627 | 10 321 | 9 023 | 177 | 72 | 0 | 40 | 37 | 0 | 339 421 -> 164 038 |
| Dragonstone | 363 | 2.00 | 1.12 | 0.70 | 15 357 | 495 | 2 673 | 1 682 | 10 708 | 5 859 | 889 | 0 | 0 | 526 | 1 199 | 281 508 | 383 371 -> 370 895 |
| Qarth | 342 | 2.00 | 1.40 | 1.03 | 12 210 | 251 | 3 028 | 2 623 | 7 049 | 6 140 | 1 028 | 756 | 0 | 259 | 467 | 91 263 | 329 018 -> 489 412 |
| Lorath | 342 | 2.00 | 1.18 | 0.90 | 11 841 | 350 | 3 276 | 2 272 | 5 878 | 6 499 | 540 | 0 | -2 116 | 222 | 310 | 44 421 | 312 058 -> 235 998 |
| Qohor | 342 | 2.00 | 1.39 | 1.03 | 11 277 | 399 | 2 378 | 2 594 | 7 232 | 4 218 | 265 | 0 | -2 080 | 177 | 250 | 36 030 | 329 319 -> 87 164 |
| Tyrosh | 342 | 2.00 | 1.05 | 0.81 | 10 528 | 238 | 1 140 | 1 863 | 4 641 | 4 611 | 734 | 21 | -1 858 | 254 | 406 | 68 327 | 340 161 -> 293 568 |
| House Targaryen, Aegon | 342 | 2.00 | 1.40 | 0.79 | 10 213 | 267 | 2 052 | 2 595 | 6 086 | 3 530 | 1 027 | 2 365 | 0 | 310 | 783 | 194 762 | 332 260 -> 258 574 |
| Norvos | 342 | 2.00 | 1.17 | 0.78 | 9 385 | 484 | 2 868 | 2 137 | 5 322 | 3 991 | 466 | 0 | -1 728 | 58 | 126 | 28 360 | 321 505 -> 76 479 |
| Myr | 342 | 2.00 | 1.40 | 1.05 | 9 212 | 375 | 1 762 | 2 625 | 6 456 | 3 387 | 656 | 0 | -1 775 | 77 | 179 | 42 353 | 310 752 -> 204 605 |
| Braavos | 342 | 2.00 | 0.70 | 0.51 | 9 204 | 503 | 2 129 | 1 351 | 5 462 | 2 286 | 1 459 | 0 | -1 582 | 1 242 | 2 766 | 661 983 | 342 575 -> 135 307 |
| Sarnor | 342 | 2.00 | 1.26 | 0.96 | 9 050 | 115 | 2 067 | 2 428 | 4 968 | 5 786 | 691 | 0 | 0 | 6 | 5 | 0 | 328 672 -> 469 832 |
| Lys | 342 | 2.00 | 1.28 | 0.64 | 7 916 | 176 | 1 377 | 2 085 | 4 153 | 3 867 | 388 | 0 | -1 500 | 137 | 322 | 76 565 | 324 236 -> 187 396 |
| Free Folk | 364 | 2.00 | 1.41 | 1.05 | 6 015 | 246 | 901 | 2 626 | 4 239 | 3 609 | 788 | 0 | 0 | 31 | 28 | 0 | 332 949 -> 49 889 |
| Pentos | 342 | 2.00 | 1.40 | 1.05 | 5 841 | 295 | 1 883 | 2 625 | 4 131 | 2 283 | 501 | 465 | -1 269 | 147 | 331 | 76 670 | 311 331 -> 334 008 |
| Nights Watch | 364 | 2.00 | 1.40 | 1.05 | 5 028 | 1 | 1 282 | 2 625 | 2 508 | 4 669 | 151 | 323 | 0 | 0 | 0 | 0 | 331 641 -> 415 289 |
| Ibben | 342 | 2.00 | 1.40 | 1.05 | 4 908 | 204 | 1 449 | 2 625 | 3 356 | 3 499 | 613 | 0 | 0 | 12 | 17 | 2 519 | 324 748 -> 42 604 |
| Yi Ti Exiles | 0 | 2.00 | 1.41 | 1.05 | 998 | 0 | 599 | 2 635 | 0 | 3 162 | 449 | 0 | 0 | 0 | 0 | 0 | 183 096 -> 348 507 |
| Summer Isles | 0 | 2.00 | 1.40 | 1.05 | 880 | 0 | 549 | 2 624 | 0 | 3 145 | 358 | 0 | 0 | 0 | 0 | 0 | 186 139 -> 306 099 |
| Skagos | 0 | 2.00 | 1.40 | 1.05 | 835 | 0 | 509 | 2 624 | 0 | 2 869 | 589 | 0 | 0 | 0 | 0 | 0 | 190 614 -> 342 495 |
| **RAZEM** | | 57.93 | 38.28 | 27.11 | 445 156 | 10 666 | 121 223 | 70 379 | 262 860 | 216 852 | 14 302 | 4 918 | -68 | 4 093 | 8 409 | 1 852 110 | 9 321 380 -> 10 034 232 |

Zrodlo: [P] LOG linie "Skarbce: dzien N - krolestwo" (stan skarbca, dlug, kiesa krola; doba 1 = :603-:631, doba 364 = :129103-:129131) i "Korona: wplywy dnia (165)" czesc "na krolestwo" (kwoty na dobe, srednia roku; suma krolestw zgadza sie z suma swiata co do zlota we wszystkich 364 dobach); "z zaworu kas miast" [P] CSV-M kolumna do_korony. Skrypt 05_korony.py. Rozbicia wplywow kazdej korony na danine, clo i zamki w danych nie ma - jest tylko dla swiata (3.1).

Co widac:
- Wszystkie skarbce zaczynaly od 2.0 mln i wszystkie spadaja. 17 z 29 konczy przy 1.03-1.06 mln - to sama regula 1/360.
- Nizej sa te, ktore placily okupy krolow albo pozyczaly: Braavos 0.51 mln (dlug w Banku 662 tys.), Lys 0.64, Krolewska Przystan 0.66 (dlug 175 tys.), Dragonstone 0.70 (dlug 282 tys.), Aegon 0.79 (dlug 195 tys.).
- Zaden skarbiec w wojnie nie spadl ponizej 0.25 mln [P] PROGI etap2.txt wiersz 7.
- Skagos, Wyspy Letnie i Wygnancy Yi Ti nie wojuja, zarabiaja 0.8-1.0 tys. dziennie i zyja z zapasu (2.6 tys. dziennie). Placa z tego renty i kontrakty kompanii najemnych.
- Dothrakowie dostaja 16.8 tys. dziennie w darach od Wolnych Miast - to 27% wplywow ich rodow [P].

### 3.3 Bank Zelazny i dlugi

| Miara | Doba 1 | Doba 182 | Doba 364 | Zrodlo |
|---|---|---|---|---|
| Kapital Banku | 5 000 000 | 4 230 256 | 3 126 954 | [P] LOG:707, :64500, :129205 |
| Dlug koron (186) | 0 | 583 513 | 1 852 110 (20 krolestw) | [P] LOG:129102 |
| Dlug rodow w Banku | 0 | 257 071 | 432 250 (37 rodow) | [P] LOG:129139 |
| Dlug okupow rodow | 0 | 1 485 368 | 2 703 390 | [P] LOG:129143 |
| Zaleglosci w ratach, bankructwa | 0 | 0 | 0 | [P] |

- Kapital plus pozyczki = 5.41 mln. Bank nie stracil - zarobil 0.41 mln odsetek i oplat [S z sumy].
- Dlug koron rosnie: w ostatnich 28 dobach korony pozyczaja 11.4 tys., a splacaja 9.6 tys. dziennie [P]. Dlug wzrosl miedzy doba 182 a 364 u 16 krolestw - prog B2-5 niespelniony [P] PROGI etap5.txt wiersz 175.
- Bank pozycza koronom tylko z kapitalu ponad 2.0 mln. Zostalo 1.1 mln. Przy tempie -5.6 tys. dziennie (doby 253-364) starczy na ok. 200 dob [S]. W ostatnich 28 dobach kapital stal (-170 zl dziennie) [P].

## 4. Gdzie lezy zloto swiata

| Posiadacz | Doba 1 | Doba 182 | Doba 364 | Zmiana w roku | Na dobe |
|---|---|---|---|---|---|
| Kiesy ludu miast | 6 277 921 | 29 656 999 | 44 120 365 | +37 842 444 | +104 249 |
| Sakiewki zolnierzy | 338 323 | 16 251 396 | 16 591 066 | +16 252 743 | +44 773 |
| Kiesy wsi | 1 107 474 | 9 817 926 | 9 547 972 | +8 440 498 | +23 252 |
| Kiesy ludu podzamczy | 672 228 | 4 030 169 | 3 326 724 | +2 654 496 | +7 313 |
| Tabory wsi (w drodze) | 3 616 | 800 152 | 677 812 | +674 196 | +1 857 |
| Bandy i kryjowki | 0 | 424 671 | 418 023 | +418 023 | +1 152 |
| Zalogi (kiesy garnizonow) | 1 180 400 | 1 405 865 | 1 472 181 | +291 781 | +804 |
| Gminy, weterani w karczmach, portfele wezwan | 926 | 190 595 | 223 979 | +223 053 | +614 |
| Gracz | 100 | 52 912 | 52 912 | +52 812 | +145 |
| Kapital warsztatow | 2 564 841 | 2 635 503 | 2 568 293 | +3 452 | +10 |
| Karawany | 3 743 269 | 3 142 281 | 2 981 924 | -761 345 | -2 097 |
| Kasy zamkow | 6 942 637 | 4 600 211 | 5 405 157 | -1 537 480 | -4 235 |
| Pozostali lordowie | 12 014 245 | 10 223 101 | 10 436 306 | -1 577 939 | -4 347 |
| Liczniki cel i podatku wsi (zloto w drodze do panow) | 2 022 407 | 235 827 | 222 909 | -1 799 498 | -4 957 |
| Bank Zelazny | 5 000 000 | 4 230 256 | 3 126 954 | -1 873 046 | -5 160 |
| Glowy rodow | 52 250 815 | 47 803 459 | 47 229 257 | -5 021 558 | -13 833 |
| Notable | 28 824 740 | 24 692 396 | 21 849 654 | -6 975 086 | -19 215 |
| Kasy kupcow miast | 27 333 090 | 9 961 445 | 10 975 439 | -16 357 651 | -45 062 |
| Skarbce krolestw | 57 927 181 | 38 280 005 | 27 108 168 | -30 819 013 | -84 901 |
| **RAZEM** | **208 204 213** | **208 435 169** | **208 335 095** | **+130 882** | **+361** |

Zrodlo: [P] LOG:707 (doba 1), LOG:64500 (doba 182), LOG:129205 (doba 364), linia "Pieniadz swiata". Suma pol zgadza sie z polem "razem" co do zlota. "Na dobe" = zmiana / 363. Skrypt 01_pieniadz_swiata.py.

Uwaga do gracza: 52 812 zl to okup z kontroli testowej niewoli w pierwszych dobach (rodzina zaplacila gotowka 52 912, z niczego 0) [P] PROGI etap2.txt wiersz 31.

Kto zyskal, kto stracil:
- Stracili: skarbce koron (-30.8 mln), kupcy miast (-16.4), notable (-7.0), glowy rodow (-5.0), Bank (-1.9; to pozyczki, nie strata).
- Zyskali: mieszczanie (+37.8 mln), zolnierze (+16.3), wsie (+8.4), podzamcza (+2.7).
- Pierwsze 28 dob to rozruch nowej kampanii: kasy kupcow oddaly 16.2 mln (27.3 -> 11.2), a glowy rodow chwilowo urosly do 60.9 mln [P] skrypt 06, szereg co 28 dob.

Tempo w czterech czesciach roku (zl na dobe) [P] skrypt 06:

| Zbiornik | Doby 1-84 | Doby 85-168 | Doby 169-252 | Doby 253-364 | Ostatnie 28 dob | Ocena |
|---|---|---|---|---|---|---|
| Skarbce krolestw | -119 157 | -104 882 | -75 778 | -51 371 | -49 303 | WYSYCHA (do podlogi 0.5 mln na krolestwo) |
| Kiesy ludu miast | +143 627 | +117 966 | +92 520 | +73 577 | +70 166 | ROSNIE caly rok, coraz wolniej |
| Notable | -18 065 | -27 465 | -18 548 | -14 380 | -9 878 | maleje, coraz wolniej |
| Bank Zelazny | -2 535 | -5 819 | -6 542 | -5 574 | -170 | maleje (pozycza koronom) |
| Sakiewki zolnierzy | +143 940 | +44 792 | -896 | +5 522 | -11 364 | stoi od ok. 170. doby (16 mln) |
| Kiesy wsi | +93 721 | +10 986 | -5 310 | +1 650 | +6 881 | stoi od ok. 110. doby (9.5 mln) |
| Kasy kupcow miast | -215 542 | +5 755 | +10 733 | +1 315 | -4 675 | stoi od ok. 60. doby (10-11 mln) |
| Kasy zamkow | -37 554 | +5 798 | +9 025 | +2 986 | +7 079 | stoi, lekko rosnie |
| Glowy rodow | +9 688 | -52 385 | -735 | -12 175 | +16 273 | stoi od ok. 170. doby (47-48 mln) |
| Kiesy ludu podzamczy | +28 046 | +11 564 | +1 774 | -7 086 | -7 150 | maleje od polowy roku |

W drugiej polowie roku w swiecie jest juz tylko jeden duzy ruch: korony -11.2 mln, notable -2.8 mln, Bank -1.1 mln -> mieszczanie +14.5 mln [P] roznica dob 182 i 364.

## 5. Czy pieniadz krazy

Tak. Glowna petla, srednio na dobe w roku [P] LOG "Obieg" (363 doby, linie :1091-:129215) i "Kiesa ludu" (364 doby, :732-:129238), skrypt 04_obieg.py:

| Krok | Strumien | zl na dobe |
|---|---|---|
| 1 | Panowie placa zold: druzyny 445 716, zalogi 183 511, karawany 6 444 | 635 671 |
| 2 | Zold trafia: do sakiewek zolnierzy 503 377, do kas miast (zalogi) 82 545, do kas zamkow 41 005 | |
| 3 | Zolnierze wydaja: u markietanow 215 149 (do kas miast 183 133, do kies wsi 32 017), na zycie w miastach 173 502, naprawy 1 228 | 389 879 |
| 4 | Zwolnieni niosa sakiewki do wsi 24 175 i do karczm 5 119. Pobici oddaja sakiewki zwyciezcom 70 351 (zostaje wsrod zolnierzy) | |
| 5 | Lud miast zarabia: z kas kupcow 314 123 (robocizna 178 107, doplata 99 277, obrot 36 739), od wojska, dworu, warsztatow i budow 219 526 | 533 649 |
| 6 | Lud miast kupuje u kupcow | 415 443 |
| 7 | Lud miast placi podatki: panu 15 038 (podatki 10 711, cech 1 269, farma gmin 3 058), koronie 10 695 (podatek wojenny 184) | 25 733 |
| 8 | Lud miast ODKLADA (przyrost kies ludu miast) | 104 249 |
| 9 | Kupcy miast placa wsiom za plon (utarg taborow) | 310 108 |
| 10 | Kasy miast oddaja zaworem: panom 143 728, koronie 119 461 | 263 189 |
| 11 | Kasy zamkow oddaja zaworem: panom 32 085, koronie 15 806 | 47 891 |
| 12 | Korona zbiera 443 903, doklada z zapasu 70 240 i oddaje panom: zwrot zoldu 263 585, renty 214 725 | 478 310 |
| 13 | Panowie wplacaja do kas wlasnych siedzib (dwor) | 187 008 |

- Zloto wraca do panow trzema drogami: przez korone (478 tys.), przez zawor kas (176 tys.) i przez sprzedaz lupu osadom (151 tys.) [P].
- Z petli wycieka w jedno miejsce: do oszczednosci mieszczan. Mieszczanin zarabia 534 zl, wydaje 415, podatku placi 26 (4.8%), odklada 104 (19.5%) [P]. Pod koniec roku: zarabia 549, odklada 70 (12.8%), podatek wojenny to 15 (2.7%) [P].
- Mieszczanie nie maja na co wydac nadwyzki. Kiesa wyrobnikow starcza na 38 dni zycia, ponad zapas dni lezy 29.5 mln zl ("lokata") [P] LOG:129238.
- Wsie tak samo: chca kupic towaru za 70.4 tys. dziennie, kupuja za 8.9 tys. (13%), bo 479 z 571 wsi nie ma czego kupic [P] LOG:129238, srednia ostatnich 28 dob.
- Zolnierze trzymaja 16.6 mln, czyli 26 dni zoldu. Prog A3-2 (<= 15 dni) niespelniony [P] PROGI etap5.txt wiersz 63.
- Kupcy: pan i korona zabieraja zaworem ok. 100% przyrostu kas (srednia 28 dob: 100% w dobach 155-182 i 337-364, 102% w ostatniej dobie) [P] LOG:129149. Wiecej z kupcow sie nie wezmie.

Zbiornik, ktory rosnie bez konca: kiesy ludu miast (zwalnia, ale po roku dalej +70 tys. dziennie).
Zbiornik, ktory wysycha: skarbce koron (do podlogi 0.5 mln; zapas ponad podloge 12.6 mln).
Reszta stoi.

## 6. Kontrole zamkniecia (nic z niczego, nic w nicosc)

Suma roku, 363 doby (doby 2-364) [P] LOG linie "Pieniadz swiata (bilans - przyczyny)", arytmetyka linii zgodna we wszystkich 363 dobach; skrypt 02_bilans.py.

| Pozycja | Suma roku | Na dobe | Uwagi |
|---|---|---|---|
| Zmiana zlota swiata | +130 882 | +361 | 0.06% z 208 mln |
| Zloto "z niczego" - zmierzone | 241 212 519 | 664 497 | w tym pary, patrz nizej |
| Zloto "w nicosc" - zmierzone | 235 680 674 | 649 258 | |
| Zmiany netto bez pary | -5 265 857 | -14 506 | |
| Reszta niewyjasniona R | -135 106 | -372 | prog: srednia 28 dob miedzy -10 000 a +10 000 - spelniony (ostatnia doba +914) |

Prawie wszystko "z niczego" i "w nicosc" to pary - gra zdejmuje zloto w jednym miejscu, a nasz kod oddaje je odbiorcy:

| Para | Zdjete | Oddane | Roznica w roku |
|---|---|---|---|
| Zold: gra zdejmuje z kies panow, kod oddaje zolnierzom i kasom osad | 229 480 768 | 229 841 212 | +360 444 (z niczego) |
| Dochod notabli z warsztatow i karawan: z kapitalu do kies notabli | 37 565 898 | 37 565 898 | 0 |
| Kapital nowych karawan: z kies notabli, kas miast i ceny kupna | 12 813 799 | 12 813 799 | 0 |
| Bitwy: zloto pokonanych do zwyciezcow | 10 730 565 | 10 730 498 | -67 |
| Werbunek, awanse, kupno karawan BK, drobne | 17 425 210 | 17 078 090 | -347 120 (w nicosc) |
| Portfele wezwan do wojny (168): wplaty do ujemnego portfela i zmiana pola bez platnika | 582 370 | 582 370 | 0 |

Co naprawde powstalo albo zniklo (po odjeciu par) [P/S z sum]:
- Z niczego ok. +1.00 mln w roku: petla zoldu +360 tys., bohaterowie, ktorzy wrocili do swiata ze swoim zlotem +628 tys. (z tego 427 488 zl w jednej dobie 52 - LOG:18514; ktory bohater - nie wiem), rozliczenia rodow na plus +13 tys.
- W nicosc ok. -0.74 mln: werbunek i drobne -347 tys., przelewy do bohaterow poza swiatem -388 tys.
- Reszta niewyjasniona -135 tys.
- Razem +131 tys. Zgadza sie z pomiarem sumy.

Najgorsze doby:
- Najwieksza zmiana sumy swiata: doba 52, +427 658 zl (bohater wrocil do swiata) [P] LOG:18514.
- Najwieksza reszta niewyjasniona: doba 309, -39 593 zl [P] LOG:109657. Dob z reszta ponad 10 000 zl: 10 z 363. Ponad 50 000: 0. Mediana 298 zl.
- Okna 28 dob: suma swiata zmienia sie od -374 tys. (doby 254-281) do +513 tys. (doby 30-57). Ostatnie 28 dob: +230 296 zl (+0.111%), z tego +204 034 to portfele wezwan do wojny bez platnika w tym oknie [P] skrypt 02; prog E5/C1-4 (< 0.1%) niespelniony o wlos [P] PROGI etap5.txt wiersz 161.

Inne kontrole [P] PROGI etap2.txt i etap3.txt:
- Platnik: wyplat bez platnika 0. Awanse, kapital, werbunek bez odbiorcy: 0.
- Utarg wsi: nieprzypisane 0, zniklo 0 we wszystkich 364 dobach.
- Regulator kas gry: skasowal 0, dosypal 0.
- Kiesa ludu: kontrola zmiany kies r = 0 w kazdej dobie [P] LOG "Kiesa ludu".
- Probki swiata wokol okien: 4 067 probek, 2 rozjazdy (oba przy nowych karawanach) - prog 0 niespelniony [P] PROGI etap3.txt wiersz 6.
- Bledy Armoury: 0.

Wniosek: z niczego nie powstaje i w nicosc nie znika nic, co mialoby znaczenie dla gospodarki. Najwieksza pojedyncza dziura to +360 tys. zl w roku w petli zoldu (0.16% zoldu) - przyczyny w linii nie widac, nie wiem.

## 7. Porownanie z poprzednim biegiem rocznym (WGRANIE 34, bez rundy 3)

| Miara | Poprzedni bieg | Ten bieg | Zmiana | Zrodlo |
|---|---|---|---|---|
| Zloto swiata: zmiana w roku | -2 373 117 | +130 882 | lepiej | [P] LOG-S:676, :128970; LOG:707, :129205 |
| Ludzi w druzynach rodow, doba 364 | 61 072 | 73 280 | +12 208 | [P] CSV-R-S, CSV-R |
| Ludzi w druzynach, srednia dob 337-364 | 63 129 | 78 693 | +15 564 | [P] |
| Ludzi w zalogach, doba 364 | 74 147 | 83 593 | +9 446 | [P] |
| Zold druzyn na dobe (ostatnie 28 dob) | 393 317 | 456 860 | +63 543 | [P] LOG-S, LOG "Obieg" |
| Zwrot zoldu od koron (ostatnie 28 dob) | 219 247 | 277 190 | +57 943 | [P] |
| Renty koron (ostatnie 28 dob) | 204 260 | 172 002 | -32 258 | [P] |
| Zloto kompanii najemnych, doba 364 | 20 597 542 | 5 649 484 | -14.9 mln | [P] CSV-R-S, CSV-R |
| Zloto kompanii: przyrost na dobe | +48 844 | +7 706 | | [P] |
| Kontrakty 185 zaplacone w roku | 8 492 101 | 5 205 819 | -3.3 mln | [P] |
| Zadatek BK przy najmie zatrzymany w kiesach krolow | brak pomiaru | 28 773 126 zl (767 najmow) | | [P] LOG "Kontrakty najemnikow (185)" |
| Rody z lennem bez warunku renty, doba 364 | 57 (28.8%) | 12 (5.9%) | -45 | [P] LOG-S, LOG:129098 |
| Rody z lennem bez warunku renty, srednia roku | 32.8 | 8.4 | | [P] |
| Udzialy rent oddawane innym (F9), ostatnie 28 dob | 62 558 /d | 5 523 /d | -57 035 | [P] |
| Rody z lennem z 0 ludzi w druzynach, doba 364 | 29 | 14 | -15 | [P] CSV |
| Rody z lennem, ktorym zold zalog zjada caly pulap | 27 | 16 | -11 | [P] CSV |
| Zwolnieni przez budzet w roku (ludzi) | 148 569 | 98 927 | -49 642 | [P] |
| Zloto rodow, doba 364 | 62 086 181 | 52 657 242 | -9.4 mln | [P] |
| Zloto rodow z lennem, doba 364 | 35 916 626 | 41 709 941 | +5.8 mln | [P] |
| Mediana zlota rodu, doba 364 | 85 483 | 100 543 | +15 060 | [P] |
| Gorne 10% rodow ma | 56.9% | 39.4% | | [P] |
| Rodow z ponad 1 mln zl | 12 | 4 | | [P] |
| Kiesy ludu miast, doba 364 | 36 585 403 | 44 120 365 | +7.5 mln | [P] |
| Kiesy ludu miast: przyrost na dobe, ostatnie 28 dob | +32 888 | +63 085 | gorzej | [P] LOG "Kiesa ludu" |
| Sakiewki zolnierzy, doba 364 | 13 700 849 | 16 591 066 | +2.9 mln | [P] |
| Skarbce koron, doba 364 | 26 337 683 | 27 108 168 | bez zmian | [P] |
| Okupy razem w roku (z okupami krolow) | 47 933 146 | 52 328 752 | +4.4 mln | [P] |

Co runda 3 zmienila w pieniadzach:
1. Zadatek kompanii najemnych. Przedtem krol przy kazdym najmie placil kompanii z wlasnej kiesy; kompanie zebraly 17.7 mln. Teraz zadatek jest wstrzymany: 28.8 mln zostalo w kiesach krolow, a kompanie urosly tylko o 2.8 mln. Kontrakt jest placony za ludzi pod bronia (5.4 mln), nie za ludzi z umowy (8.6 mln) [P] LOG "Kontrakty najemnikow (185)". DZIALA.
2. Rody z lennem bez renty. Rod, ktorego nie stac na wiecej wojska, sluzy tym, co ma ("na miare srodkow"): w dobie 364 jest takich 80 rodow z lennem, dostaja 46.6 tys. zl rent dziennie i trzymaja 11.8 tys. ludzi [P] LOG:129098. Rodow bez warunku 12 zamiast 57. DZIALA.
3. Pulapka rent. Udzialy zabierane rodom bez sluzby i oddawane innym spadly z 62.6 do 5.5 tys. dziennie. Rodow z lennem bez zadnej druzyny 14 zamiast 29 (prog <= 12 - o 2 za duzo) [P] PROGI etap8.txt wiersz 263. DZIALA, nie do konca.
4. Skutek uboczny: wiecej wojska to wiecej zwrotu zoldu (+58 tys. dziennie), a wiec mniej zostaje na renty (-32 tys.). Wiecej zoldu to tez wiecej zlota u mieszczan: ich kiesy rosna teraz 2 razy szybciej niz w poprzednim biegu (+63 wobec +33 tys. dziennie pod koniec roku).
5. Czego runda 3 nie zmienila: skarbce koron (ten sam spadek), rody na suficie zoldu (138 wobec 141), okupy.

## 8. Skad wziac pieniadz na wojsko w czasie wojny (pytanie o podatki)

Najpierw fakty [P], potem propozycje [S].

Fakty:
1. Wojsko druzyn stoi dzis na 78-80 tys. Do limitu druzyn brakuje 83.9 tys. ludzi: 63.3 tys. (75%) trzyma pieniadz, 20.7 tys. (25%) nie (rozdzial 2.5). Na pulapie zoldu stoja 133 rody; ich druzyny maja 25.9 tys. ludzi przy limicie 75.8 tys.
2. Jedyny zbiornik, ktory stale rosnie, to kiesy mieszczan: +70 tys. zl dziennie pod koniec roku, 44.1 mln zl, z tego 29.5 mln lezy bez uzytku ponad zapas na zycie.
3. Mieszczanin placi 4.8% zarobku w podatkach. Podatek wojenny ludu (184) zabiera 15.0 tys. dziennie i stoi na pulapie 3% w 20 z 26 krolestw. Kod pozwala ustawic pulap do 20% (KingdomTreasury.WarTax.cs:93).
4. Korony wydaja wszystko, co zbiora. Zapas, z ktorego dokladaja (37 tys. dziennie), konczy sie.
5. Z kazdej zlotowki, ktora korona oddaje panom, do rodow stojacych na pulapie trafia 43 grosze (renty) albo 39 groszy (zwrot zoldu). Reszta idzie do rodow, ktorym pieniedzy nie brakuje.
6. Budzet rodu w wojnie daje na zold 60% stalego dochodu plus czesc zlota ponad rezerwe (ClanBudget.cs:26-28 opis reguly, :191-210 rachunek). Z nowej zlotowki dochodu na zold idzie wiec 60-80 groszy [S].
7. Rody wplacaja do kas wlasnych siedzib 148.7 tys. zl dziennie (dwor), w tym 87.5 tys. to przydzial na sprzet, ktorego nie wydaly w 30 dni. Na sprzet wydaja 35% przydzialu [P] LOG "Budzet rodow (166)", ostatnie 28 dob.
8. Zalogi kosztuja 190.9 tys. dziennie, z tego rody na pulapie placa 102.1 tys. Korona zalog nie zwraca.

Propozycje (wszystkie to przelew miedzy posiadaczami - nic z niczego):

| Nr | Co | Skad -> dokad | Ile na dobe [S] | Ile ludzi wiecej [S] |
|---|---|---|---|---|
| A | Podatek wojenny od zarobku mieszczan: pulap z 3% do 12-15%, tylko w wojnie | kiesy ludu miast -> skarbce | +45 do +60 tys. (dzis 15 tys. przy 3%, ok. 5 tys. na kazdy punkt) | 1.5-3 tys., jesli pojdzie starymi drogami (renty i zwrot) |
| B | Te pieniadze oddac jako zwrot zoldu ZALOG w wojnie (np. 35%) zamiast dokladac do rent | skarbce -> rody z twierdzami | koszt 67 tys.; rody na pulapie dostaja 36 tys. | 3-4 tys. (36 tys. x 0.6-0.8 / 6.88 zl) |
| C | W wojnie niewydany przydzial na sprzet idzie najpierw na zold, dopiero reszta do dworu | kiesa pana -> zolnierze, zamiast -> kasa siedziby | do 87 tys. u wszystkich rodow; ile u rodow na pulapie - nie wiem | nie wiem (brak rozbicia w tabeli) |
| D | Okup tej samej osoby najwyzej raz na 28 dob albo tanszy przy kolejnym pojmaniu | mniej zlota od slabych do silnych | w obiegu okupow jest 124 tys. dziennie; cwiartka rodow, ktora traci najwiecej, oddaje netto ok. 41 tys. dziennie (14.8 mln w roku) | nie wiem |

Rachunek dla A: 15 020 zl dziennie przy sredniej stawce 2.4% i pulapie 3% [P] -> ok. 5-6 tys. na punkt procentowy [S]. Przy pulapie 15% lud oddawalby ok. 75 tys. dziennie, czyli tyle, ile dzis odklada (70 tys.). Kiesy ludu przestalyby rosnac, a nie zaczelyby malec. Podatki mieszczan wzroslyby z 4.8% do ok. 16% zarobku [S]. Uwaga: w ok. 10 miastach lud juz dzis nie ma na pelne jedzenie (srednia 28 dob 9.6 miasta) [P] PROGI etap5.txt wiersz 102. Tam podatek od zarobku pogorszy sprawe - bezpieczniej brac go tylko od kies ponad zapas dni (29.5 mln "lokaty").

Rachunek dla ludzi: 1 zolnierz druzyny kosztuje 5.80 zl dziennie srednio, 6.88 zl u rodow na pulapie [P] CSV-R. 10 tys. zl dziennie, ktore naprawde trafi na zold rodu na pulapie, to ok. 1.5 tys. ludzi [S].

Uczciwa ocena:
- A + B razem (A placi za B): ok. +3 do +4 tys. ludzi, czyli z dzisiejszych 78-80 tys. do ok. 82-84 tys. [S]. Prog 85 tys. wymaga jeszcze C albo ruszenia rodow z punktu N2.
- Bez A zapas koron wyschnie i panom ubedzie jeszcze ok. 35 tys. zl rent dziennie - to minus 2-4 tys. ludzi w ciagu roku-dwoch [S]. Sam podatek A mniej wiecej to wyrowna. Dlatego cel 70-80 tys. bez nowych podatkow jest realny, ale blizej dolu tego pasma.
- Zadna z tych propozycji nie ruszy ok. 21 tys. ludzi, ktorych brakuje w rodach, choc te rody na nich stac. Rod bez klopotow z pieniedzmi trzyma caly rok 55-63% limitu druzyn - to wyglada na sufit naboru, nie zlota. Mozliwa przyczyna [S]: w roku bylo 2 919 pojman lordow przy ok. 630 druzynach, czyli kazda druzyna jest rozbijana srednio co ok. 80 dob i potem odbudowuje sie od zera.
- Wniosek dla progu: pieniadz moze podniesc tylko rody na pulapie (dzis 34% limitu). Gdyby doszly do poziomu rodow z luzem (58%), przybyloby ok. 18 tys. ludzi i w polu byloby ok. 97 tys. [S]. To jest gorna granica tego, co da sie kupic podatkami.
- Czego nie ruszac: kupcow (pan i korona biora juz caly przyrost ich kas), notabli (traca 10-19 tys. dziennie), wsi i sakiewek zolnierzy (stoja - to jednorazowy zapas 9.5 i 16.6 mln, nie strumien).
- Kazda propozycja wymaga biegu testowego. Liczby [S] sa z prostego przeliczenia, nie z pomiaru skutku.

## 9. CO TRZYMA SENS

1. Swiat jest zamkniety: 208.20 -> 208.34 mln zl w roku (+0.06%). W poprzednim biegu -2.37 mln [P].
2. Kazda wyplata ma platnika i odbiorce: bez platnika 0, bez odbiorcy 0, utarg wsi zniklo 0, regulator kas skasowal 0 i dosypal 0 w 364 dobach [P].
3. Pieniadz krazy pelna petla: zold 636 tys. dziennie -> zolnierze -> miasta i wsie -> kupcy i lud -> zawor kas i podatki -> panowie i korona -> zold [P].
4. Korona nie drukuje i nie gromadzi: wydaje co do zlota wplywy dnia, zwrot zoldu wyplacony w 99.8% [P].
5. Nikt nie zbankrutowal: bankrutow 0, zaleglosci w Banku w dobie 364: 0, rodow w zajeciu 1, glow rodow ponizej 5 000 zl 5 [P].
6. Zalegly zold w zlocie 0.12% (prog 2%) [P] PROGI etap2.txt wiersz 10.
7. Wojsko druzyn przestalo spadac: srednie 28 dob 80.5 -> 83.2 -> 80.5 -> 78.4 -> 78.7 tys. od doby 225 [P]. W poprzednim biegu 71.2 -> 63.1 tys.
8. Kompanie najemne nie wysysaja juz zlota: 5.6 mln zamiast 20.6 mln [P].
9. Renta nie przepada juz biednym rodom: 12 rodow z lennem bez renty zamiast 57 [P].
10. Zloto rodow jest rozlozone rowniej: gorne 10% ma 39% zamiast 57%, mediana 101 zamiast 85 tys. [P].
11. Kasy kupcow, kiesy wsi, sakiewki zolnierzy i kiesy glow rodow ustaly po rozruchu (doby 60-170) i stoja do konca roku [P].
12. Bank dziala jak bank: pozyczyl koronom 3.04 mln i rodom 2.40 mln, korony placa raty w 100%, rody spoznily sie 24 razy w roku, bankructw 0; kapital plus naleznosci 5.41 mln [P] LOG:129102, :129139, "Dlugi (168)".
13. Podatek wojenny ludu rosnie z potrzeba korony: 7.0 tys. dziennie w pierwszej polowie roku, 14.4 tys. w drugiej; w oblezeniu jest wstrzymany [P] LOG "Obieg", "Korona: podatek wojenny ludu (184)".

## 10. NIESPOJNOSCI

| Nr | Co nie trzyma sensu | Dowod | Waga | Co by to naprawilo |
|---|---|---|---|---|
| N1 | Mieszczanie odkladaja zloto, ktorego nie maja na co wydac, a korony i panowie w tym czasie chudna. Kiesy ludu miast rosna caly rok i po roku dalej +70 tys. dziennie. | Kiesy ludu miast 6.3 -> 44.1 mln; ponad zapas na zycie lezy 29.5 mln; skarbce 57.9 -> 27.1 mln; lud placi 4.8% zarobku w podatkach, odklada 12.8-19.5% [P] LOG:707, :129205, :129238 | wysoka | Podatek wojenny od zarobku ludu z pulapem ok. 15% zamiast 3% (propozycja A) - zabiera dokladnie to, co lud dzis odklada. |
| N2 | Cwierc luki w wojsku nie ma nic wspolnego z pieniedzmi: rod, ktorego stac, i tak trzyma druzyny na 55-63% wielkosci. | Do limitu brakuje 83.9 tys. ludzi; 20.7 tys. miesci sie we wlasnym wolnym pulapie rodu; 56 rodow stac na cala brakujaca druzyne; grupa z luzem: 39.2 tys. ludzi przy limicie 68.1 tys. i 27.1 mln zl; tak samo w dobach 84, 182, 273, 364 [P/S] CSV-R, skrypty 06 i 14 | srednia | Ustalic w rozdziale o wojsku, dlaczego rod z pieniedzmi nie dobiera ludzi (tempo naboru po rozbiciu druzyny - 2 919 pojman w roku, nabor u notabli, cel wielkosci druzyny AI). Z danych o pieniadzu nie wiem. |
| N3 | Skarbce koron wysychaja, a renty panow spadaja razem z nimi. Stan dzisiejszy nie jest trwaly. | Skarbce -49 tys. dziennie pod koniec roku; renty 221 -> 172 -> 141 tys.; 36 rodow z lennem w krolestwach bez grosza na renty; dlug koron w Banku rosnie u 16 krolestw [P] LOG:129098, :129102, PROGI etap5.txt wiersz 175 | wysoka | Stale zrodlo zamiast zapasu: propozycja A daje koronom tyle, ile dzis biora z zapasu (37 tys.) i jeszcze 10-20 tys. [S]. |
| N4 | Okupy przenosza w roku tyle zlota, ile maja wszystkie rody razem, i ta sama osoba jest wykupywana po kilkanascie razy. | 1 259 okupow za 52.3 mln zl przy 52.7 mln zlota rodow; 9.2 pojmania na rod; glowa rodu Kasgos 11 okupow, rod 31 okupow za 787 tys. (57% rocznych wplywow), zloto 223 -> 14 tys.; krol Braavos pojmany 9 razy, skarbiec zaplacil 1.44 mln [P] LOG "Okupy (178)", :129147 | srednia | Przerwa ochronna po wykupie (ta sama osoba najwyzej raz na 28 dob) albo nizsza cena przy kolejnym pojmaniu. |
| N5 | Pieniadz korony trafia w wiekszosci do rodow, ktore go nie potrzebuja. | Rody na pulapie zoldu (133) dostaja 43% rent i 39% zwrotu; rody z luzem i pelne (174) dostaja 57-61% [P] CSV-R, ostatnie 28 dob | srednia | Czesc pieniedzy wojennych kierowac wedlug zoldu zalog (propozycja B) - zalogi sa glownym ciezarem rodow na pulapie. |
| N6 | W wojnie rody oddaja niewydany przydzial na sprzet do kas wlasnych siedzib, a jednoczesnie odprawiaja zolnierzy z braku zoldu. | Dwor 148.7 tys. dziennie, w tym niewydany sprzet 87.5 tys.; sprzet kupowany za 35% przydzialu; budzet odprawia 335 ludzi dziennie; 133 rody na pulapie [P] LOG "Budzet rodow (166)", ostatnie 28 dob | srednia | W wojnie niewydany przydzial na sprzet rodu stojacego na pulapie idzie najpierw na zold (propozycja C). |
| N7 | Zolnierze trzymaja w sakiewkach 26 dni zoldu, ktorych nie wydaja. | 16.6 mln zl, stoi od doby 170; prog <= 15 dni niespelniony; oszczednosci 15.2 dnia, ktorych markietani nie ruszaja [P] PROGI etap5.txt wiersz 63 | niska | Nic pilnego - zbiornik nie rosnie. Mozna obnizyc prog oszczednosci zolnierza, to jednorazowo uwolni ok. 6-7 mln [S]. |
| N8 | Rod na pulapie placi za zolnierza 1.45 raza wiecej niz rod z luzem. Biedny ma drozsze wojsko. | 6.88 wobec 4.76 zl na czlowieka dziennie; prog <= 1.25x niespelniony (1.38x) [P] CSV-R, PROGI etap8.txt wiersz 265 | niska | Sprawa rozdzialu o wojsku (kolejnosc odpraw wedlug szczebla juz poprawiona w rundzie 3 - bylo 1.51x). |
| N9 | Warsztaty i karawany panow nie daja dochodu, a karawany kosztuja zold. | Majatek 44 zl dziennie na 318 rodow; zold karawan lordow 6 426 zl dziennie; karawan lordow 54 -> 21 [P] CSV-R d_majatek, LOG "Obieg: notable i karawany" | niska | Zostawic - lordowie sami likwiduja karawany. Sprawdzic tylko, czy to zamierzone, ze pan nie zarabia na warsztacie. |
| N10 | Notable traca zloto caly rok. | 28.8 -> 21.8 mln (-19 tys. dziennie, pod koniec -10 tys.); doplacaja do zoldu swoich karawan 92 tys. dziennie; 177 z 535 karawan nie zarabia na wlasny zold [P] LOG:129216 | niska | Nic pilnego (starczy na lata). Docelowo limit karawan na kupca - jest juz w planie (PROGI etap8.txt wiersz 139). |
| N11 | Kraje bez wojny (Skagos, Wyspy Letnie, Wygnancy Yi Ti) zyja z zapasu skarbca i placa z niego kompaniom najemnym. | Wplywy 0.8-1.0 tys., wydatki 3.5 tys. dziennie, z zapasu 2.6 tys.; goszcza 4 z 11 kompanii [P] LOG "Korona: wplywy dnia (165)" | niska | Kontrakt kompanii tylko z wplywow dnia korony, bez zapasu, gdy krolestwo nie jest w wojnie. |
| N12 | Drobne przecieki w ksiedze: petla zoldu +360 tys. w roku z niczego, portfele wezwan do wojny rozhustuja sume swiata w oknach 28 dob, 2 rozjazdy probek przy nowych karawanach. | [P] LOG bilans - przyczyny (suma roku), PROGI etap3.txt wiersz 6, etap5.txt wiersz 161 | niska | Rozbic w linii, skad roznica miedzy zoldem zdjetym a oddanym; portfel wezwania rozliczac w tej samej dobie. |

Czego nie udalo sie ustalic z danych (nie wiem):
- Ile kazda korona z osobna dostaje z daniny, cla i zamkow (jest tylko suma swiata).
- Na co dokladnie idzie ok. 81 tys. zl dziennie "innych wydatkow" rodow (zakupy na targach, przegrane bitwy).
- Jaka czesc dworu i niewydanego sprzetu przypada na rody stojace na pulapie zoldu.
- Dlaczego rody, ktore na to stac (56 rodow stac na cala brakujaca druzyne), nie dobieraja ludzi.
- Ktory bohater wrocil do swiata w dobie 52 z 427 tys. zl.
- Skad +360 tys. zl roznicy w petli zoldu.
