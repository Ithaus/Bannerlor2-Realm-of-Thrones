#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
sprawdz_logi.py - zwiezly skrot sesji logu moda Armoury (Bannerlord / Realm of Thrones).

UZYCIE
  python sprawdz_logi.py [sciezka-logu] [opcje]
      bez sciezki: najnowszy  <gra>\\Modules\\Armoury\\Armoury-*.log
  --temat NAZWA      pelny szereg jednego tematu (wszystkie doby, wszystkie kolumny); nazwa bez rozrozniania
                     wielkosci liter: najpierw dokladna ("ruda"), potem poczatek ("przeplywy" = 5 tematow), potem fragment
  --dni A-B          tylko doby A-B (numery porzadkowe 1..N; liczby >= 10000 = dni gry, np. 108840-108850)
  --surowe           linie bez parsowania (z --temat: wszystkie linie tematu w calosci; bez: pierwsza i ostatnia kazdego tematu)
  --porownaj LOG2    ten sam skrot dla dwoch sesji obok siebie: pierwsza i ostatnia doba kazdego tematu
  --csv              skrot pliku ludzie-regiony.csv z katalogu sesji (Logs\\<sesja>\\): 8 regionow o najwiekszym obciazeniu
                     i - od ogniwa 113, gdy plik ma kolumny uchodzcy / spustoszenie_proc / zabici_spustoszenie - 8 regionow
                     najbardziej spustoszonych
  --wczytanie N      gdy w jednym logu jest kilka kampanii / wczytan: ktora analizowac (domyslnie ta z najwieksza liczba dob)
  --test NAZWA       kontrole "po czym poznac w logu" z docs\\paczki\\<ogniwo>.md, kazda z wynikiem OK / UWAGA / BRAK DANYCH
                     i liczba z logu. NAZWA: numer ogniwa (102b, 106, 117), kilka (103,104), zakres (102b-105; zakres idzie
                     wedle KOLEJNOSCI LANCUCHA, wiec 107-108 obejmuje tez 115-119), grupa testowa (surowce = 102b-105,
                     pieniadz = 106-107, towary2 = 115-119, ludzie = 108-109, kasy = 110-112, dowoz = 100-101, ksiegi =
                     102-102b), slowo z opisu ogniwa (paser, zold, karawany, mineral, warsztaty, wozy, przyrost,
                     spustoszenie...), "wykryte" (wszystkie ogniwa widoczne w logu) albo "wszystkie". Bez tej opcji skrot
                     pokazuje do 10 kontroli najwyzszego ogniwa wykrytego w logu.
  --grupa N          komplet kontroli GRUPY TESTOWEJ naraz (decyzja Jeffa 06.10: testy grupami): 1 = TOWAR (101 + 102 + 102b
                     + 103 + 104 + 105), 2 = PIENIADZ (106 + 107), 2b = TOWARY 2 (115 + 116 + 117 + 118 + 119), 3 = LUDZIE
                     (BetterEconomy + 108 + 109), 4 = KASY (110 + 111 + 112), 5 = SPUSTOSZENIE (113); takze nazwa (towar,
                     pieniadz, towary2, ludzie, kasy, spustoszenie) albo "w-grze" (grupa najwyzszego ogniwa wykrytego w
                     logu). Wypisuje: warunki testu grupy (DLL, nowa kampania albo zapis, liczba dob, bledy, formaty
                     nierozpoznane), kontrole wspolne grupy (lista test_groups z docs\\paczki\\PRZEGLAD-KOLIZJI-LANCUCHA-
                     2026-10-06.txt i powiazania miedzy ogniwami), wszystkie kontrole kazdego ogniwa grupy i alarmy calego
                     logu - kazda pozycja z OK / UWAGA / BRAK DANYCH i liczba z logu. Grupa 3 czyta tez (tylko odczyt) plik
                     ustawien BetterEconomy (13 kluczy; stan pliku TERAZ, nie z chwili sesji) i plik ludzie-regiony.csv
                     sesji (bandy za Murem); grupa 5 - ten sam plik CSV (trzy nowe kolumny). Gdy pliku nie ma, kontrola
                     mowi BRAK DANYCH i podaje sciezke.
                     GRUPA 2b = TOWARY 2 (ogniwa 115-119, DLL Armoury-grupa-towary2.dll = build n119, md5 faade7bf...): idzie
                     do gry PRZED grupami 3-5 (lancuch 07.10: 107 -> 115..119 -> 108..114 przeniesione na 119), a numery 3-5
                     zostaja, bo tak nazywaja je STAN-PRAC i opisy paczek; "2b" = druga czesc towaru po pieniadzu (nazwy:
                     2b, towary2, ceny). Warunki testu: swiezy start gry, NOWA kampania (115, 116 i 118 przeliczaja start
                     kampanii tylko w sesji, ktora ja zalozyla), 20 dob. GLOWNA LICZBA: "Ruda: miast bez towaru" w ostatniej
                     dobie (test 14:08: 74 -> 69; oczekiwane ok. 25-30 po 20 dobach; ponad 50 = ALARM). Grupa 2b wypisuje tez
                     TABELE DZIEN PO DNIU glownych liczb (ruda i drewno bez towaru, indeks i cena rudy w pustym miescie,
                     karawany, wozy, nadplata, niezgodne, ms, piekarnia i chleb, potkniecia).
  --pelny            bez limitu ok. 160 linii (wszystkie alarmy i wszystkie tematy dodatkowe)
  --szer N           szerokosc wyjscia (domyslnie 160)

UKLAD SKROTU (domyslnie ok. 160 linii, przy pelnym lancuchu do ok. 190; do 160 znakow)
  1. naglowek: plik, rozmiar, doby i dni gry, linie startowe modulow (wpiete / CZYNNE / BRAK / NIE wpieta / WYLACZONE),
     uklad ksiegi pieniadza (wpis 102 albo 102b), tabela ogniw lancucha paczek w kolejnosci lancucha 100, 101, 102, 102b,
     103 ... 107, 115 ... 119, 108 ... 113 (po czym poznac kazde w logu, ktore jest w grze, ktorych linii startowych nie
     ma) i linia "OGNIWO W GRZE"
  2. bledy: ERROR / Exception / potkniecia, pogrupowane, z numerem linii pierwszego wystapienia
  3. alarmy: reguly z docs\\paczki\\*.md i z raportu nocnego, kazdy z numerem linii logu (progi = stale na gorze pliku)
  4. co sprawdzic dla ogniwa w grze: do 10 kontroli najwyzszego wykrytego ogniwa, kazda w postaci
     "[OK | UWAGA | BRAK DANYCH] linia N: co sprawdzono -> liczba z logu" (kontrola OK = jedna linia, UWAGA i BRAK DANYCH
     do dwoch linii; "pomiar:" = paczka kaze liczbe zmierzyc, progu nie ma), jedna linia o pozostalych ogniwach
     widocznych w logu i jedna o grupie testowej (--grupa N); pelna lista i inne ogniwa: --test
  5. szeregi dzienne: wiersz = miara, kolumny = pierwsza doba, kilka rownomiernie rozlozonych, ostatnia
  6. formaty nierozpoznane: tematy, ktore spadly do trybu surowego (zmieniony albo nieznany format linii)

LANCUCH PACZEK (galezie 87c8e96 = wpis 102 -> n102b-ksiega -> n103-karawany -> n104-zapas-startowy -> n105-mineral-bk ->
  n106-paser -> n107-zold-i-skarbiec (w grze) -> n115-cena-od-niedoboru -> n116-warsztaty-w-nowej-monecie ->
  n117-karawany-ruda-dociera -> n118-towary-w-nowej-monecie -> n119-wozy-do-najlepszego-miasta (= grupa 2b) ->
  m108-ludzie-jednostka -> m109-ludzie-przyrost -> m110-k5-kasa-zamku -> m111-k6-kasy-miast -> m112-k7-utarg-wsi ->
  m113-ludzie-spustoszenie -> m114-porzadki; kolejka 108..114 przeniesiona na 119 07.10 - docs\\paczki\\PRZEGLAD-ZLOZENIA-
  2026-10-07.txt). Kolejnosc ogniw w tabeli i w kontroli "lancuch niespojny" jest kolejnoscia lancucha: log z 108-113 bez
  115-119 (stare galezie n108..n113 na n107) daje alarm "lancuch niespojny" - te galezie sa nieaktualne. Ksiega pieniadza
  ma dwa uklady linii i skrypt czyta oba: wpis 102 (bilans z pozycja "(zold N", sekcja "zold wyplacony [P]") i 102b (bilans
  z "rozliczenia rodow na plus / na minus" i zdaniem "W tym zold naliczony N", nowa linia "Pieniadz swiata (rody):", sekcja
  "zold naliczony przy rozliczeniach rodow [P]"). Etykiety dnia: "Wyrzutki / Paser / Zold / Korona / Skarbce / Kasy zamkow /
  Kasy miast dzien D" to ten sam tick co "Ruda / Karawany / Dowoz / Przeplywy osad / Pieniadz swiata / Utarg wsi / Ludzie /
  Ludzie (spustoszenie) dzien D-1" (tabela PRZESUNIECIE_DNIA).
  Ogniwa 110-112 zmieniaja linie ksiegi pieniadza: w bilansie nowe ujscia ("z ceny zywnosci kupionej we wsiach zniklo",
  "z sakw zniszczonych taborow wsi zniklo") i dopiski o darze startowym kas ("w tym dar startowy kas zamkow / miast
  przyciety"); w "Przeplywy osad (kasy ...)" pozycje ticku "danina podzamcza", "udzial korony z zaworu kas miast", "dar
  startowy kas ... przyciety", w kiesach wsi "zywnosc kupiona we wsiach" i "sakwy rozwiazanych taborow"; w "Przeplywy osad:"
  dopisek "w tym dopisane wsiom przez K7". Po ogniwie 111 linia "Zold:" konczy sie dopiskami "- ZBEDNA i nieczynna" i
  "zalogi bez zwrotu korony": tarcza zoldu jest wtedy nieczynna i kontrola ogniwa 107 mowi to wprost (nie "wlaczona").
  Ogniwo 113 dopisuje do "Ludzie:" uchodzcow ("uchodzcy poza domem N, razem z nimi X mln") i odcinek "spustoszenie dzis".
  Ogniwa 115-119 (grupa 2b) dodaja piec linii dnia: "Ceny surowcow:" (115; z czescia "Towary z wartoscia z definicji
  przedmiotu" od 118), "Warsztaty towarowe:" (116), "Karawany (przyczyny):" i "Karawany (kierunek):" (117), "Dowoz (wozy):"
  (119) - wszystkie z numerem dnia jak "Ruda:" (dzien zakonczony); 116 dopisuje do "Przeplywy osad (kasy miast)" pozycje
  "warsztaty towarowe - place i utrzymanie z kapitalu warsztatow", 117 druga linie startowa "CaravanBulk: poprawka 115 ..."
  (roboczy numer w kodzie; w lancuchu 117), 118 linie startowe "HistoricalPrices: wartosc z definicji towarow BK ..." i
  "HistoricalPrices: przelicznik popytu od wartosci z definicji przedmiotu ...", 115 i 116 linie "nowa kampania - ..." (tylko w
  sesji, ktora zalozyla kampanie), 119 linie startowa "MarketCarts: poprawka 119 ...".

TEMATY Z PARSEREM KOLUMN (prefiks linii logu)
  surowce:   Ruda, Drewno, Ceny surowcow (115/118), Dowoz, Dowoz (skutki), Dowoz (wozy) (119), Karawany, Karawany (stan),
             Karawany (przyczyny), Karawany (kierunek) (117), Warsztaty, Warsztaty towarowe (116), Rynek surowcow, Rynek broni,
             Mineraly (dubel BK)
  pieniadz:  Pieniadz swiata, Pieniadz swiata (bilans), Pieniadz swiata (rody), Przeplywy osad, Przeplywy osad (kasy miast |
             kasy zamkow | kiesy wsi), Kasy zamkow, Kasy miast, Utarg wsi, Korona (powinnosci | danina i clo | zwrot
             zoldu), Skarbce (suma krolestw), Zold, Sakiewka ludzi, Paser, IronBank
  ludzie:    Ludzie (z odcinkami "hearth za ludzi dzis" i "spustoszenie dzis"), Ludzie (przyrost) = linia "Ludzie: przyrost
             naturalny", Ludzie (spustoszenie), Ludzie (regiony), Ludnosc, Wyrzutki, Bitwy
  dodatkowe: ZakupyAI, Zuzycie AI, HouseLevies, Werbunek, Pobor, Ochotnicy, Komplet rekruta, Budowy oplacone, Budowy,
             PodazPopyt, WPLYW, UniqueLaw (dzien), LegendaryLaw (targi), Audyt predkosci, ArmsPricing (wojny)
  jednorazowe (naglowek, alarmy, kontrole): StartStock, MineralOnce, MaterialLaw (mnoznik wydobycia), CaravanBulk,
             MoneyLedger, OutlawLaw, SoldierPay, KingdomTreasury, PeopleUnit, PopulationLaw, CastlePurse (start i
             jednorazowe przyciecie daru), TownPurse (to samo), VillageTakings, Utarg wsi (latki), Devastation,
             Spustoszenie, Kalendarz, ColdStart, RawPrice (115), WorkshopTrade (116), MarketCarts (119), HistoricalPrices
             (118: wartosc z definicji, popyt w N kategoriach, przelicznik od definicji, "UWAGA - ... wartosc 0")
  Kazdy inny prefiks, ktory powtarza sie w dobach, jest pokazany w trybie surowym (linia obcieta).

Skrypt niczego nie zapisuje (tylko stdout, UTF-8). Nieznany albo zmieniony format linii nie wywraca skryptu:
temat spada do trybu surowego i jest wymieniony w sekcji "formaty nierozpoznane".

STAN NA 07.10 (noc 06/07.10): w grze grupy 1 i 2 (wpisy 101-107; prawdziwy log Armoury-2026-10-06_14-08-11.log, 20 dob nowej
kampanii). Nastepny test: GRUPA 2b TOWARY 2 (115-119; niewgrana - czeka na slowo Jeffa), potem 3-5 z galezi m108..m114.
Linie ogniw 115-119 wziete z kodu galezi n119-wozy-do-najlepszego-miasta (klon scratchpad\\lancuch) i z logow prob
recenzentow (prawdziwy kod poza gra: "Ceny surowcow:" z czescia o towarach z definicji, "Warsztaty towarowe:", "Karawany
(przyczyny)", "Karawany (kierunek)", "Dowoz (wozy):", linie startowe RawPrice / WorkshopTrade / CaravanBulk poprawka /
HistoricalPrices z definicji / MarketCarts); sprawdzone na logu sztucznym = log 14:08 + te linie z prawdziwymi znacznikami
czasu i numerami dni (zero formatow nierozpoznanych). Linii "WorkshopTrade: nowa kampania - kapital startowy ..." i pozycji
"warsztaty towarowe - place i utrzymanie" w kasach miast nikt jeszcze nie wypisal prawdziwym kodem - sa z literalow.
Uklady linii ogniw 108-113 - jak dotad z kodu galezi n108..n113 i testu test_syntetyczny.py (kotwice wobec literalow, linie
wypisane przez prawdziwy kod w probach poza gra). Progi alarmow i kontroli pochodza z opisow paczek (SZACUNKI z prob i
symulacji, nie pomiary) - stale na gorze pliku. Pierwszy prawdziwy log po wgraniu ogniwa trzeba obejrzec takze z --surowe:
gdyby kolumna byla pusta ("-") albo temat spadl do "formatow nierozpoznanych", parser trzeba poprawic, a nie ufac alarmom
tego tematu (--grupa N mowi to w warunkach testu).
"""

import sys
import os
import re
import glob
import argparse
from collections import Counter, OrderedDict, defaultdict

# =====================================================================================================================
# STALE I PROGI ALARMOW (zrodla: docs/paczki/102b..113, docs/RAPORT-NOCNY-2026-10-06.md rozdz. 1-2, CHANGELOG wpisy 100-102)
# =====================================================================================================================
KATALOG_LOGOW = r'C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Armoury'
WZORZEC_LOGU = 'Armoury-*.log'
PLIK_CSV = 'ludzie-regiony.csv'

SZEROKOSC = 160                 # znakow na linie wyjscia
LIMIT_LINII = 160               # domyslny limit dlugosci skrotu (150 + 4 wiersze tabeli lancucha 110-113 + linia grupy
                                # + 5 wierszy ogniw 115-119)
PROBKI = 6                      # ile dob w szeregu (pierwsza, rownomiernie rozlozone, ostatnia)
MAKS_ALARMOW = 24               # ile alarmow w skrocie (reszta: --pelny)
MAKS_GRUP_BLEDOW = 8
MAKS_KONTROLI = 10              # ile kontroli ogniwa w grze w skrocie (wszystkie: --test NR)

# ruda / karawany (103)
PROG_MIAST_BEZ_RUDY_PROC = 60.0     # "Ruda: miast bez towaru" w ostatniej dobie, % miast (test 15:22: 60 z 97)
KARAWANY_MIN_DOB = 7                # po ilu dobach z ogniwem 103 oceniamy "ruda: bez towaru"
KARAWANY_BEZ_TOWARU_CEL = 20        # oczekiwane "kilka-kilkanascie" miast bez rudy po 10-20 dobach
KARAWANY_MIN_SPADEK_PROC = 25.0     # o ile % ma spasc "bez towaru" od pierwszej do ostatniej doby
KUPNO_STOI_MAKS_DOB = 3             # "KUPNO STOI" dluzej niz tyle dob z rzedu = alarm
KARAWANY_ROZRUCH_DOB = 10           # zero zakupow karawan przez tyle pierwszych dob = uwaga (po 104 rozruch 7-10 dob)
KARAWANY_BRAK_MIEJSCA_KROTNOSC = 2.0  # "brak miejsca w jukach" > tyle x "z zakupem" = uwaga
# ksiega rudy i drewna (102)
BEZ_WYJASNIENIA_RUDA_ABS = 50       # |bez wyjasnienia| rudy ponad tyle ladunkow ...
BEZ_WYJASNIENIA_RUDA_PROC = 2.0     # ... i ponad tyle % zapasu "razem"
BEZ_WYJASNIENIA_DREWNO_ABS = 2600   # drewno: znana dosypka RealisticBannerlord ok. +2100 dziennie; alarm dopiero wyzej
# zapas startowy (104) i mineral (105)
RUDA_RAZEM_PO_104 = 900             # "Ruda: razem" pierwszej doby po przeliczeniu ma byc ponizej
# Ogniwo 105 po przebudowie lancucha 06.10 NIE zmienia wydobycia: mnoznik rudy zostaje 3 ("wydobycie rudy x3.0" jest dobrze),
# wiec dawny alarm o x3.0 przy wgranym 105 zostal usuniety. Alarmem ogniwa 105 jest teraz "latka NIE wpieta".
MNOZNIK_RUDY_OCZEKIWANY = 3.0       # tylko do kontroli ogniwa 105; inna wartosc = ustawienie MineOutputMultiplier z MCM
STOSUNEK_NA_WIES_PO_105 = 1.3       # po 105: ("wsie dopisaly" / kopiace wsie) / ("model" / wsie modelu) ok. 1 (przed: ok. 2)
MINERALY_ZNANE = ('iron', 'salt', 'clay', 'silver', 'marble', 'gold_ore')
MINERALY_LIMIT_WSI = {'iron': 26, 'salt': 12, 'clay': 16, 'silver': 30}
# pieniadz (102, 102b)
RESZTA_BILANSU_PROC_RUCHU = 25.0    # |reszta| bilansu swiata ponad tyle % ruchu (zrodla + ujscia)
RESZTA_KAS_PROC_RUCHU = 25.0        # |reszta| kas miast / zamkow / wsi ponad tyle % sumy pozycji zmierzonych
RESZTA_KAS_MIN_ABS = 20000          # ... i ponad tyle zlota
ZOLD_DUBEL_OD = 0.5                 # reszta bilansu w przedziale 0.5-1.5 x zold = podwojne liczenie zoldu (uklad wpisu 102)
ZOLD_DUBEL_DO = 1.5
TICKI_OSAD_MIN_UDZIAL = 0.5         # "N tickow osad" konsumpcji ponizej tej czesci liczby osad = uwaga (ok. 97 miast, do 130 zamkow)
ROZLICZEN_ROZRZUT_PROC = 15.0       # "w K rozliczeniach": K ma sie wahac o kilka miedzy dobami; rozrzut ponad tyle % mediany = uwaga
# dowoz (100-101)
ZATKANE_ROZNICA_PP = 5.0            # zatkane magazyny: wsie zamkowe % minus wsie miejskie % ponad tyle punktow = uwaga
# paser (106)
PASER_MIN_DOB_BEZ_AWANSOW = 5       # "awanse: od pasera" = 0 przez tyle dob z ogniwem 106 = uwaga
PASER_ZOSTALO_DOB = 3               # "z ladunkiem zostalo" > 0 przez tyle dob z rzedu = alarm (paser ma obslugiwac 100% band)
# zold i skarbiec (107)
ZOLD_ROZJAZD_PROC = 2.0             # "z kies zeszlo" wobec sumy pozycji "przekazano" + "nie przekazano", %
ZOLD_KSIEGA_ROZJAZD_PROC = 10.0     # "Zold: naliczony" wobec licznika zoldu w "Przeplywy osad", %
SAKIEWKA_ROSNIE_DOB = 7             # "najwieksza" sakiewka rosnie tyle dob z rzedu = uwaga
# ludzie (102, 108, 109)
LUDNOSC_ROZJAZD_MLN = 0.2           # "Ludzie: ludnosc" wobec "Ludnosc:" (mln)
TABORY_ZERO_DOB = 3                 # "tabory wsi i lodzie ... za 0 ludzi" przez tyle dob przy istniejacych taborach = uwaga
PRZYROST_MIN_LUDZI = 100            # "Ludzie: przyrost naturalny +N ludzi": N poza tym przedzialem = alarm
PRZYROST_MAKS_LUDZI = 1500          # (rachunek paczki 109: pokoj +592, wojna wszedzie +245, perk u wszystkich +806)
PRZYROST_G_MIN = 0.1                # "g srednio" poza tym przedzialem = uwaga (oczekiwane 0.2-0.5 % rocznie)
PRZYROST_G_MAKS = 0.8
PRZYROST_RESZTA_HEARTH = 25.0       # |RESZTA| hearth wsi na dobe ponad tyle = uwaga (inwestycje BetterEconomy to +65..135)
LUDNOSC_MAKS_ZMIANA_PROC = 0.05     # ludnosc swiata zmienia sie o ponad tyle % dziennie przy ogniwie 109 = alarm
# kasy zamkow (110, K5) i kasy miast (111, K6) - liczby z opisow paczek (rachunek, nie pomiar)
KASY_BRAKUJE_ROSNIE_DOB = 21        # "do zapasu brakuje" wyzsze niz przed tyloma dobami (3 tygodnie) = alarm (paczka 110, ryzyko 2)
KASY_BRAKUJE_MIN_WZROST_PROC = 5.0  # ... o co najmniej tyle % i o co najmniej KASY_BRAKUJE_MIN_ABS
KASY_BRAKUJE_MIN_ABS = 10000
DAR_ZAMKOW_ZDJETO = (4000000, 6200000)      # nowa kampania: "zdjeto ok. 5.09 mln w 130 zamkach" (8 014 000 -> 2 924 200)
DAR_MIAST_ZDJETO = (11500000, 16500000)     # nowa kampania: "zdjeto ok. 13.98 mln w 97 miastach" (20 528 000 -> 6 546 400)
KASY_MIAST_DOSYPKA_MAKS = 160000    # "dosypal z niczego" na dobe: szacunek recenzenta 50-160 tys.; mediana ponad to = alarm
RESZTA_KAS_K5_MIN_ABS = 20000       # reszta [R] kas zamkow / miast po K5 / K6 "mala": ponizej tylu zlota albo 25% ruchu
SKARBIEC_WOJNA_MAKS_SPADEK = 100000  # skarbiec krolestwa w wojnie spada szybciej niz o tyle dziennie (mediana) = uwaga
# utarg wsi (112, K7)
UTARG_WIES_RAZEM_PROC = (10.0, 50.0)    # "wies razem" ok. 30% utargu (10-50% zaleznie od dekretow)
KIESY_WSI_WZROST_DOB = 14           # kiesy wsi rosna ok. dwa tygodnie do ok. 2x (mniej przy doplatach notabli)
# spustoszenie (113)
SPUSTOSZENIE_NA_RABUNEK_ALARM = 10000   # zdjeci rabunkiem na jeden rabunek: oczekiwane setki ludzi (75-250 przy poczcie 110);
                                        # dziesiatki tysiecy = tyle, ile zdejmowala gra - latka spustoszenia nie dziala
SPUSTOSZENIE_UDZIAL_GRY_ALARM = 0.5     # ... albo zdjeci >= tej czesci "gra zdjelaby ... ludzi ksiegi"
SPUSTOSZENIE_GRA_MIN_LUDZI = 2000       # ... liczone, gdy gra zdjelaby co najmniej tylu ludzi
ZEROWANIE_OSOBODNI_UWAGA = 20000        # osobodni zerowania armii na dobe (mediana): "dziesiatki tysiecy" = uwaga (ryzyko 2)
SPUSTOSZENIE_UDZIAL_TOL = 3.0           # zabici / w las maja byc po 5% trafionych: odchylenie ponad tyle pkt % = uwaga
# grupa 2b TOWARY 2 (ogniwa 115-119) - progi z "Po czym poznac w logu" docs\paczki\115..119 (SZACUNKI z prob i symulacji
# recenzentow, nie pomiary; "dotad" = test 06.10 14:08, grupy 1 + 2, 20 dob nowej kampanii)
RUDA_BEZ_TOWARU_CEL_2B = 30             # GLOWNA LICZBA: "Ruda: miast bez towaru" po 20 dobach - realnie ok. 25-30 (recenzent wozow;
                                        # dotad 74 -> 69; ponizej 20 dopiero po ok. 40 dobach, 16 miast lezy dalej niz 250 od kopaln)
RUDA_BEZ_TOWARU_ALARM_2B = 50           # ... ponad tyle po 20 dobach = ALARM (wozy i karawany nie dowoza rudy)
TOWARY2_MIN_DOB = 15                    # od tylu pelnych dob oceniamy glowna liczbe (krocej: tylko kierunek)
DREWNO_BEZ_TOWARU_CEL_2B = 20           # "Drewno: miast bez towaru": dotad 41 -> 40, oczekiwane ok. 10-15
INDEKS_SUFIT = 9.95                     # indeks ceny na suficie gry x10 (log pisze 10.00)
CENA_RUDY_PUSTA_MIN = 20.0              # "ruda ... za pierwsza sztuke placa" (pusta polka): dotad ok. 10 d, oczekiwane ok. 37-40 d
INDEKS_DREWNA_MIN = 0.7                 # "Ceny surowcow": mediana indeksu drewna ok. 0.8-1.1 (dotad 0.2-0.6)
INDEKS_WELNY_MAKS = 0.5                 # ... welny 0.10-0.3 (polki pelne welny; dotad 10 przy pustej polce)
CENA_ZBYTU_RUDY_2B = (12.0, 40.0)       # "Karawany (stan): ruda ... cena zbytu" ok. 15-25 d (dotad 29.9 -> 10.4, spadala sama)
CENA_ZBYTU_DREWNA_MIN_2B = 5.0          # ... drewno 8-12 d (dotad 10.4 -> 1.3)
CENA_ZBYTU_WELNY_MAKS_2B = 300.0        # ... welna wyraznie ponizej 372 d
ZYSK_RUDY_KG_MIN = 0.1                  # "Karawany: oczekiwany zysk ... na kilogram": ruda ok. 0.15 (dotad 0.08)
ZYSK_DREWNA_KG_MIN = 0.03               # ... drewno ok. 0.08 (dotad 0.003)
INDEKS_BRONI_DREWNA_MIN = 0.8           # "Rynek broni: indeks drewna" mediana ok. 1.0-1.25 (dotad 0.58 w ostatniej dobie)
WARSZTATY_BEZ_ZYSKU_MAKS = 100          # "Warsztaty: bez zysku" na dobe (mediana): dotad 32; setki = drewno na suficie w wielu miastach
WARSZTATY_TOW_MIN = 250                 # "Warsztaty towarowe: warsztatow" ok. 291 (97 miast x 3 warsztaty notabli bez ukrytych)
KAPITAL_WARSZTATOW_START = (1800000, 3200000)   # "Pieniadz swiata: kapital warsztatow" 1. doby nowej kampanii ok. 2.4 mln (dotad 4.1 mln)
KAPITAL_WARSZTATOW_ZM_MIN = -10000      # ... zmiana dobowa (mediana) - przestaje tracic ok. 29 tys. dziennie w nicosc (dotad -17.6 tys.)
PIEKARNIA_CENA_MAKS = 150000            # cena piekarni w przykladzie miasta (Lannisport): 13-116 tys. przy ok. 20 chlebach dziennie;
                                        # ponad to = chleba i ciast naprawde brakuje (przy ok. 10 bochnach 92-522 tys.)
BUDZET_CHLEBA = (5000, 30000)           # "Ceny surowcow: bread ... budzet mieszczan" ok. 15-20 tys. (przy wylaczonym 118: 30-67 tys.)
KARAWANY_BRAK_MIEJSCA_2B = 15           # "zakup odpuszczony: brak miejsca w jukach" na dobe (mediana): dotad 37 (pod koniec ok. 70)
KARAWANY_KUP_RUDY_2B = 25               # "kupione z nadwyzek: ruda" na dobe (mediana): dotad 10, oczekiwane 35-55
KARAWANY_SPRZ_RUDY_2B = 7               # "sprzedane miastom z brakiem: ruda" na dobe (mediana): dotad 1, oczekiwane 10-25
KARAWANY_PRZY_WYJEZDZIE_PROC = 10.0     # "Karawany (przyczyny): przy wyjezdzie M" wobec N + M: M bliskie 0
KARAWANY_WJAZDY_MIN_2B = 70             # "wjazdy karawan do miast" (mediana): dotad 85; wyraznie mniej przy rosnacym "karawan w miastach"
                                        # = karawany przeciazone (CaravanBulkFillLimit z powrotem na 0.8)
WOZY_N_2B = (100, 350)                  # "Dowoz (wozy): N wozow" na dobe (mediana): oczekiwane ok. 150-250
WOZY_DO_INNEGO_MIN_PROC = 30.0          # "do innego" miasta jako % wozow: "wyraznie > 0" (symulacja: ok. 80% kursow surowcow)
WOZY_DROGA_2B = (40.0, 180.0)           # "srednio X jedn. drogi": oczekiwane ok. 60-140
WOZY_MS_MAKS = 300.0                    # "(cen N, X ms)" na dobe: kilkadziesiat; ponad ok. 300 zglosic
WOZY_WYGASLE_MAKS = 50                  # "wygasle przy porzadkach" na dobe: kilka-kilkanascie to norma
NADPLATA_PROC_MAKS = 30.0               # "tabory oddaly osadom nadplate ... (R%": SZACUNEK kilka-kilkanascie procent
# wartosci "dotad" z testu 14:08 (do porownan w kontrolach grupy 2b; policzone tym narzedziem z Armoury-2026-10-06_14-08-11.log)
BAZA_1408 = {'ruda_bez': (74, 69), 'drewno_bez': (41, 40), 'drewno_budowy_med': 1958, 'drewno_bez_wyj_med': 2055,
             'zatk_zamkowe': 4.6, 'zatk_miejskie': 7.4, 'zakupy_med': 462411, 'miasta_zaplacily_med': 380037,
             'wool': (660, 4094), 'kap_warsztatow': (4101882, 3915736), 'wjazdy_med': 85, 'kup_ruda_med': 10,
             'sprz_ruda_med': 1, 'brak_miejsca_med': 37, 'zamki_glodne': (0, 1), 'ruda_cena_zbytu': (29.9, 10.4)}

# przesuniecie numeru dnia w linii: 1 = linia drukuje (int)Now.ToDays (dzien biezacy), 0 = dzien wlasnie zakonczony
# (Now.ToDays - 1). Ustalenie przegladu kolizji lancucha: "Wyrzutki / Paser / Zold / Korona / Skarbce dzien D" to ten sam
# tick co "Ruda / Karawany / Dowoz / Przeplywy osad / Pieniadz swiata / Ludzie dzien D-1". Klucz = prefiks linii logu, wiec
# "Ludzie: przyrost naturalny ... dzien N ..." (ogniwo 109) idzie z "Ludzie" (ten sam dzien co linia "Ludzie: dzien N").
# Ogniwa 110-113 (z kodu galezi n113): "Kasy zamkow" i "Kasy miast" drukuja (int)Now.ToDays (jak "Ludnosc"), "Utarg wsi"
# (int)Now.ToDays - 1, "Ludzie (spustoszenie)" dostaje dzien od ksiegi ludzi (ten sam co "Ludzie: dzien N").
# Doba skrotu = dzien zakonczony; tematow spoza tabeli skrypt uczy sie z sasiednich linii tej samej sekundy.
PRZESUNIECIE_DNIA = {
    'Bitwy': 1, 'Bitwa': 1, 'Budowy oplacone': 1, 'Budowy': 1, 'Budowa': 1, 'Korona': 1, 'Skarbce': 1, 'Finanse': 1,
    'Wyrzutki': 1, 'Paser': 1, 'Ludnosc': 1, 'Zold': 1, 'IronBank': 1, 'HouseLevies': 1, 'Audyt predkosci': 1,
    'Kronika unikatow': 1, 'Klimat': 1, 'Kasy zamkow': 1, 'Kasy miast': 1,
    'Ruda': 0, 'Drewno': 0, 'Dowoz': 0, 'Dowoz (skutki)': 0, 'Karawany': 0, 'Karawany (stan)': 0,
    'Mineraly (dubel BK)': 0, 'Pieniadz swiata': 0, 'Pieniadz swiata (bilans)': 0, 'Pieniadz swiata (rody)': 0,
    'Przeplywy osad': 0, 'Przeplywy osad (kasy miast)': 0, 'Przeplywy osad (kasy zamkow)': 0,
    'Przeplywy osad (kiesy wsi)': 0, 'Utarg wsi': 0,
    'Ludzie': 0, 'Ludzie (regiony)': 0, 'Ludzie (spustoszenie)': 0, 'ZakupyAI': 0, 'Zuzycie AI': 0, 'Pobor': 0,
    'Werbunek': 0, 'Sakiewka ludzi': 0, 'Komplet rekruta': 0, 'Ochotnicy': 0, 'Warsztaty': 0,
    # ogniwa 115-119 (z kodu galezi n119): wszystkie drukuja (int)Now.ToDays - 1, czyli dzien zakonczony jak "Ruda:"
    'Ceny surowcow': 0, 'Karawany (przyczyny)': 0, 'Karawany (kierunek)': 0, 'Warsztaty towarowe': 0, 'Dowoz (wozy)': 0,
}
# plik ustawien BetterEconomy (grupa 3: 13 kluczy zamykajacych ujscia - tools\bee\zamknij-ujscia-bee.ps1); tylko odczyt
PLIK_BEE = os.path.join('BetterEconomy', 'ModuleData', 'better_economy_settings.xml')
KLUCZE_BEE = [('CastleAiLeaderReserveGold', '1000000000'), ('LordInvestmentReserveFlat', '1000000000'),
              ('VillageDiversionRelationThreshold', '-101'), ('VillageDiversionGrievanceThreshold', '101'),
              ('VillageSecondaryRequiredStableDays', '1000000000'), ('CaravanDeliveryMinGold', '1000000000'),
              ('CaravanEscortHireMinGold', '1000000000'), ('CaravanRecruitPromotionEnabled', '0'),
              ('RouteDangerMaxLossRatio', '0'), ('TradeAgreementCustomsMin', '0'),
              ('TradeAgreementCorridorProsperityPerDay', '0'), ('RaidPeasantFlightFraction', '0')]
# trzynasty klucz: zbrojownia zamknieta tylko AI (domyslnie) albo doslownie z listy fundamentu (-ListaZFundamentu)
KLUCZE_BEE_ZBROJOWNIA = [('ArmoryAiCheckCooldownDays', '1000000000'), ('ArmoryRequiredArtisans', '1000000000')]
ZNACZNIK_KAMPANII = 'Behavior dodany do kampanii'

# =====================================================================================================================
# NARZEDZIA
# =====================================================================================================================
TYPY = {
    'int': r'(-?\d+)',
    'sint': r'([+-]?\d+)',
    'float': r'([+-]?\d+(?:[.,]\d+)?)',
    'slowo': r'([^\s,;.()]+)',
}


def _liczba(s):
    """Tekst -> int albo float albo tekst (nigdy wyjatek)."""
    if s is None:
        return None
    try:
        return int(s)
    except (TypeError, ValueError):
        pass
    try:
        return float(str(s).replace(',', '.'))
    except (TypeError, ValueError):
        return s


class Kol(object):
    """Jedna kolumna tematu: kotwica (doslowny tekst z kodu tuz przed wartoscia) + typ albo wlasne wyrazenie.
    sek = fragment tekstu, po ktorym poznac sekcje linii (miedzy ' | '); krotka = kilka wariantow (dwa uklady tej samej linii).
    zr = plik .cs, z ktorego pochodza literaly kolumny, gdy inny niz plik tematu (tylko dla testu kotwic)."""

    def __init__(self, nazwa, kot=None, typ='int', rx=None, sek=None, wym=False, lit=None, zr=None):
        self.nazwa, self.kot, self.typ, self.wym, self.zr = nazwa, kot, typ, wym, zr
        self.sek = tuple(sek) if isinstance(sek, (list, tuple)) else ((sek,) if sek else None)
        if rx is not None:
            self.rx = re.compile(rx)
            self.lit = list(lit or [])
        elif typ == 'delta':            # "kotwica 1234 (+56" -> +56
            self.rx = re.compile(re.escape(kot) + r'-?\d+(?:[.,]\d+)? \(([+-]\d+)')
            self.lit = list(lit) if lit is not None else [kot]
        else:
            self.rx = re.compile(re.escape(kot) + TYPY[typ])
            self.lit = list(lit) if lit is not None else [kot]

    def czytaj(self, tresc, sekcje):
        zrodlo = tresc
        if self.sek:
            zrodlo = None
            for czesc in sekcje:
                if any(s in czesc for s in self.sek):
                    zrodlo = czesc
                    break
            if zrodlo is None:
                return None
        m = self.rx.search(zrodlo)
        if not m:
            return None
        g = m.groups()
        if len(g) >= 2 and g[1] is not None:    # "+a/-b" -> a - b
            a, b = _liczba(g[0]), _liczba(g[1])
            if isinstance(a, (int, float)) and isinstance(b, (int, float)):
                return a - b
        v = _liczba(g[0])
        # kolumna liczbowa z tekstem zamiast liczby (np. "1.2.3" albo sam przecinek w uszkodzonej linii) = brak wartosci;
        # inaczej tekst trafilby do porownan w regulach alarmow i kontrolach
        if self.typ != 'slowo' and not isinstance(v, (int, float)):
            return None
        return v


def K(nazwa, kot=None, typ='int', rx=None, sek=None, wym=False, lit=None, zr=None):
    return Kol(nazwa, kot, typ, rx, sek, wym, lit, zr)


RX_F = r'([+-]?\d+(?:[.,]\d+)?)'      # liczba z ulamkiem i znakiem (do wlasnych wyrazen kolumn)


def KD(nazwa, kot, wym=False):
    """Wartosc i jej zmiana dobowa w nawiasie: 'kotwica 123 (+4)'."""
    return [K(nazwa, kot, 'int', wym=wym), K(nazwa + '_zm', kot, 'delta')]


class Temat(object):
    """Rodzaj linii logu: prefiks + (opcjonalnie) wzorzec poczatku reszty linii + kolumny."""

    def __init__(self, nazwa, prefiks, rodzaj=None, kol=(), extra=None, skrot=(), grupa='dodatkowe', zrodlo=None,
                 agreg=None, dzienny=True, bezwar=False, wielo=False):
        self.nazwa, self.prefiks = nazwa, prefiks
        self.rodzaj = re.compile(rodzaj) if rodzaj else None
        self.kol = []
        for k in kol:
            if isinstance(k, (list, tuple)):
                self.kol.extend(k)
            else:
                self.kol.append(k)
        self.extra, self.skrot, self.grupa, self.zrodlo = extra, list(skrot), grupa, zrodlo
        self.agreg, self.dzienny, self.bezwar, self.wielo = agreg, dzienny, bezwar, wielo

    def pasuje(self, reszta):
        return self.rodzaj is None or self.rodzaj.search(reszta) is not None

    def czytaj(self, tresc):
        """Zwraca (slownik wartosci, lista brakujacych pol wymaganych)."""
        sekcje = tresc.split(' | ')
        d, braki = OrderedDict(), []
        for k in self.kol:
            try:
                v = k.czytaj(tresc, sekcje)
            except Exception:
                v = None
            d[k.nazwa] = v
            if v is None and k.wym:
                braki.append(k.nazwa)
        if self.extra is not None:
            try:
                dod = self.extra(tresc, d)
                if dod:
                    d.update(dod)
            except Exception as e:
                braki.append('extra:%s' % type(e).__name__)
        return d, braki


def skr(v, znak=False):
    """Krotki zapis liczby do komorki skrotu."""
    if v is None:
        return '-'
    if isinstance(v, bool):
        return 'TAK' if v else 'nie'
    if isinstance(v, float):
        if abs(v) >= 10000:
            return skr(int(round(v)), znak)
        s = ('%.3f' % v).rstrip('0').rstrip('.') if abs(v) < 1000 else '%.0f' % v
        if s in ('-0', ''):
            s = '0'
        return ('+' + s) if (znak and v >= 0) else s
    if isinstance(v, int):
        a = abs(v)
        if a < 10000:
            s = str(v)
        elif a < 1000000:
            s = '%.1fk' % (v / 1000.0)
        else:
            s = '%.2fM' % (v / 1000000.0)
        return ('+' + s) if (znak and v >= 0) else s
    return str(v)


RX_SZABLON = re.compile(r'\{(\w+)(:\+)?\}')


def wypelnij(szablon, d):
    """Szablon komorki '{a}/{b:+}' -> tekst; '-' gdy zadnej wartosci nie ma."""
    if d is None:
        return '-'
    trafione = [0]

    def zamien(m):
        v = d.get(m.group(1))
        if v is None:
            return '-'
        trafione[0] += 1
        return skr(v, bool(m.group(2)))
    try:
        s = RX_SZABLON.sub(zamien, szablon)
    except Exception:
        return '?'
    return s if trafione[0] else '-'


def obetnij(s, n):
    s = s.replace('\t', ' ')
    return s if len(s) <= n else s[:max(0, n - 3)] + '...'


def wycinek(tresc, n):
    """Linia skrocona tak, zeby slowo BRAK / NIE wpiet / WYLACZON bylo widac (inaczej ginie za obcieciem)."""
    m = re.search(r'\bBRAK\b|NIE wpiet|NIECZYNN|WYLACZON|regula wylaczona|\) wylaczone w ustawieniach', tresc)
    if not m or m.end() + 30 <= n:
        return obetnij(tresc, n)
    pref = tresc.split(':', 1)[0][:30]
    od = max(0, m.start() - 35)
    return obetnij(pref + ': ... ' + tresc[od:], n)


def proc(a, b):
    try:
        return 100.0 * a / b if b else None
    except Exception:
        return None


def lista_par(txt):
    """'ruda 12, drewno 30, skory surowe 4' -> {'ruda': 12, ...}; 'nic' -> {}."""
    d = OrderedDict()
    if not txt:
        return d
    for m in re.finditer(r'([A-Za-z_][\w /-]*?) (-?\d+)(?=,|$|\])', txt.strip()):
        d[m.group(1).strip()] = int(m.group(2))
    return d


def lista_par_f(txt):
    """'ruda 0.05, welna 49.125' -> {'ruda': 0.05, ...}; '-' albo 'nic' -> {}. Liczby z kropka dziesietna."""
    d = OrderedDict()
    if not txt:
        return d
    for m in re.finditer(r'([A-Za-z_][\w /-]*?) (-?\d+(?:\.\d+)?)(?=,|$)', txt.strip()):
        d[m.group(1).strip()] = float(m.group(2))
    return d


def mediana(xs):
    """Mediana listy liczb (None pomijane); None dla pustej."""
    v = sorted(x for x in xs if isinstance(x, (int, float)) and not isinstance(x, bool))
    return v[len(v) // 2] if v else None


def najdluzsza_seria(flagi):
    """Najdluzszy ciag prawdziwych wartosci: (dlugosc, indeks poczatku) albo (0, None)."""
    naj, pocz, biez, biez_pocz = 0, None, 0, None
    for i, f in enumerate(flagi):
        if f:
            if biez == 0:
                biez_pocz = i
            biez += 1
            if biez > naj:
                naj, pocz = biez, biez_pocz
        else:
            biez = 0
    return naj, pocz


# =====================================================================================================================
# TEMATY - kolumny wyprowadzone z ciagow Log.Info (galaz n113-ludzie-spustoszenie = szczyt lancucha; uklad ksiegi pieniadza
# wpisu 102 z commitu 87c8e96) i z prawdziwych logow
# =====================================================================================================================
def _kol_ksiegi(zrodlo_miast):
    return [
        K('dopisaly', 'wsie dopisaly ', wym=True),
        K('wsi_kopie', rx=r' t, (\d+) wsi', lit=[' t, ', ' wsi; model ']),
        K('model', '; model ', 'float', wym=True, lit=[' wsi; model ']),
        K('model_wsi', rx=r'; model [\d.,]+ w (\d+) wsiach', lit=[' wsiach z wynikiem > 0), ']),
        K('z_miast', rx=r'(?:kopalnie miast i zamkow|, miasta i zamki) \+(-?\d+)', lit=[zrodlo_miast]),
        K('warsztaty', 'zuzycie: warsztaty zbrojne ', wym=True),
        K('linie', rx=r'linie towarowe \([^)]*\) (-?\d+)', lit=[', linie towarowe (']),
        K('budowy', ', budowy '),
        K('miasta', '; zapas: miasta ', wym=True),
        K('miasta_zm', '; zapas: miasta ', 'delta'),
        K('zamki', ', zamki '),
        K('wsie', ', wsie '),
        K('tabory', ', tabory '),
        K('t_wiesniacy', ' (wiesniacy '),
        K('t_karawany', ', karawany '),
        K('t_lordowie', ', lordowie '),
        K('t_inne', ', inne '),
        K('t_bandy', ' - w tym bandy '),
        K('razem', ', razem ', wym=True, lit=['), razem ']),
        K('zmiana', ', razem ', 'delta', lit=['), razem ']),
        K('bez_wyjasnienia', ', bez wyjasnienia ', 'sint'),
        K('bez_towaru', '; miast bez towaru ', wym=True),
        K('miast', rx=r'; miast bez towaru \d+ z (\d+)', lit=['; miast bez towaru ']),
        K('zima', '; zima: ', 'slowo'),
    ]


def _extra_ksiegi(t, d):
    r = {}
    if d.get('dopisaly') is not None and d.get('model'):
        r['dopisaly_do_modelu'] = round(d['dopisaly'] / float(d['model']), 2)
        # na jedna wies: tyle dopisala kopiaca wies wobec tego, co liczy dla niej model (po ogniwie 105 ok. 1, przed ok. 2).
        # Tylko uklad od wpisu 102 ("w N wsiach z wynikiem > 0"); wczesniej model byl pytany takze o wsie bez tego surowca
        # ("model 96.5 w 92 wsiach" przy 26 kopalniach) i iloraz na wies nic nie znaczyl.
        if d.get('wsi_kopie') and d.get('model_wsi') and 'wsiach z wynikiem > 0' in t:
            r['na_wies_do_modelu'] = round((d['dopisaly'] / float(d['wsi_kopie'])) / (float(d['model']) / d['model_wsi']), 2)
    if d.get('bez_towaru') is not None and d.get('miast'):
        r['bez_towaru_proc'] = round(100.0 * d['bez_towaru'] / d['miast'], 1)
    return r


SKROT_KSIEGI = [
    ('wsie dopisaly / model', '{dopisaly}/{model}'),
    ('zuzycie: warsztaty/linie/budowy', '{warsztaty}/{linie}/{budowy}'),
    ('zapas: miasta/tabory/razem', '{miasta}/{tabory}/{razem}'),
    ('bez wyjasnienia | miast bez towaru', '{bez_wyjasnienia:+} | {bez_towaru}'),
]


def _extra_dowoz(t, d):
    return {'wylaczona': 1 if 'LATKA WYLACZONA' in t else 0}


# ------------------------------------------------------------------------------------------------ ogniwa 115-119 (grupa 2b)
# "Ceny surowcow:" (RawPrice.Daily, ogniwo 115): na surowiec "ruda (wartosc 8 d): indeks min/mediana/max 0.27/2.67/10.00, bez towaru
# 2 miast - za pierwsza sztuke placa 25.5 d, z nadwyzka 1 miast - sprzedaja po 3 d, popyt dobowy miasta: mieszczan 0 d + rzemiosla
# 5.6 d, w danych rynku 5.6 d, sam szacunek gry 4 d" (czesci "- za pierwsza ..." / "- sprzedaja po ..." tylko przy liczbie > 0)
RX_CENY_SUROWCA = re.compile(
    r'([a-z][a-z ]*?) \(wartosc (-?\d+) d\): indeks min/mediana/max ' + RX_F + '/' + RX_F + '/' + RX_F +
    r', bez towaru (\d+) miast(?: - za pierwsza sztuke placa ' + RX_F + r' d)?, z nadwyzka (\d+) miast(?: - sprzedaja po ' + RX_F +
    r' d)?, popyt dobowy miasta: mieszczan ' + RX_F + r' d \+ rzemiosla ' + RX_F + r' d, w danych rynku ' + RX_F +
    r' d, sam szacunek gry ' + RX_F + r' d')
RX_CENY_INNE = re.compile(r'(\w+) ' + RX_F + '/' + RX_F + '/' + RX_F)
# czesc ogniwa 118: "bread (bread 6 d) /3.33 5.39/5.64/5.89, na polkach 24 szt., pusto w 0 miastach, budzet mieszczan 541 d (przy
# dzisiejszych polkach do 541 d)" - nawias z przykladami tylko, gdy towar lezy na polce; "bez przelicznika" zamiast "/N"
RX_CENY_DEF = re.compile(
    r'^(\S+)(?: \(([^)]*)\))? (?:/' + RX_F + r'|bez przelicznika) ' + RX_F + '/' + RX_F + '/' + RX_F +
    r', na polkach (-?\d+) szt\., pusto w (\d+) miastach, budzet mieszczan (-?\d+) d \(przy dzisiejszych polkach do (-?\d+) d\)$')
ZN_CENY_SUR = '= ladunek) - '
ZN_CENY_INNE = '. Inne przeliczone towary, indeks min/mediana/max: '
ZN_CENY_DEF = '. Towary z wartoscia z definicji przedmiotu ('


def _kat_klucz(s):
    """Id kategorii (bread, Eggs, Ink) -> klucz kolumny (bread, eggs, ink)."""
    return re.sub(r'[^a-z0-9_]', '_', s.lower())


def _extra_ceny(t, d):
    """Linia "Ceny surowcow:" (ogniwo 115, RawPrice.Daily; czesc o towarach z wartoscia z definicji = ogniwo 118)."""
    r = OrderedDict()
    if ' - popyt z prawdziwego zuzycia ' not in t:
        raise ValueError('brak naglowka linii')
    r['popyt_stan'] = 'CZYNNY' if 'popyt z prawdziwego zuzycia CZYNNY' in t else 'wylaczony'
    r['stala_stan'] = 'CZYNNA' if 'stala wzoru w nowej monecie CZYNNA' in t else 'wylaczona'
    m = re.search(r'\(model cen ([^,)]+)', t)
    r['model_cen'] = m.group(1) if m else '?'
    i_sur, i_inne, i_def = t.find(ZN_CENY_SUR), t.find(ZN_CENY_INNE), t.find(ZN_CENY_DEF)
    konce = [x for x in (i_inne, i_def) if x >= 0]
    sur = t[i_sur + len(ZN_CENY_SUR):(min(konce) if konce else len(t))] if i_sur >= 0 else ''
    n = 0
    for m in RX_CENY_SUROWCA.finditer(sur):
        k = SUROWCE_KARAWAN.get(m.group(1).strip(), m.group(1).strip().replace(' ', '_'))
        g = m.groups()
        n += 1
        r[k + '_wartosc'] = int(g[1])
        r[k + '_idx_min'], r[k + '_idx_med'], r[k + '_idx_max'] = _liczba(g[2]), _liczba(g[3]), _liczba(g[4])
        r[k + '_bez'] = int(g[5])
        r[k + '_placa'] = _liczba(g[6]) if g[6] is not None else None
        r[k + '_nadw'] = int(g[7])
        r[k + '_sprzedaja'] = _liczba(g[8]) if g[8] is not None else None
        r[k + '_popyt_m'], r[k + '_popyt_r'] = _liczba(g[9]), _liczba(g[10])
        r[k + '_popyt_dane'], r[k + '_popyt_gra'] = _liczba(g[11]), _liczba(g[12])
    if n == 0:
        raise ValueError('brak surowcow')
    r['surowcow'] = n
    if i_inne >= 0:
        kon = i_def if i_def > i_inne else len(t)
        for m in RX_CENY_INNE.finditer(t[i_inne + len(ZN_CENY_INNE):kon]):
            k = 'inne_' + _kat_klucz(m.group(1))
            r[k + '_min'], r[k + '_med'], r[k + '_max'] = _liczba(m.group(2)), _liczba(m.group(3)), _liczba(m.group(4))
    if i_def >= 0:
        ogon = t[i_def + len(ZN_CENY_DEF):]
        ogon = ogon.split('): ', 1)[1] if '): ' in ogon else ''
        ogon = ogon[:-1] if ogon.endswith('.') else ogon
        kat, zle, bez = 0, 0, 0
        budzet = wydane = 0
        for czesc in ogon.split('; '):
            mm = RX_CENY_DEF.match(czesc.strip())
            if not mm:
                zle += 1
                continue
            g = mm.groups()
            k = 'def_' + _kat_klucz(g[0])
            kat += 1
            r[k + '_przel'] = _liczba(g[2]) if g[2] is not None else None
            if g[2] is None:
                bez += 1
            r[k + '_min'], r[k + '_med'], r[k + '_max'] = _liczba(g[3]), _liczba(g[4]), _liczba(g[5])
            r[k + '_szt'], r[k + '_pusto'] = int(g[6]), int(g[7])
            r[k + '_budzet'], r[k + '_wydane'] = int(g[8]), int(g[9])
            budzet += int(g[8])
            wydane += int(g[9])
        if kat == 0:
            raise ValueError('czesc o towarach z definicji bez zadnej kategorii')
        r['def_kat'], r['def_zle'], r['def_bez_przel'] = kat, zle, bez
        r['def_budzet'], r['def_wydane'] = budzet, wydane
    return r


# "Karawany (przyczyny):" (CaravanBulk.Daily, ogniwo 117): "ruda 4: kupily 3, brak miejsca 1, w drodze dosc 0, cena nie nizsza od
# sredniej 0, bez zysku 0, sprzedane tu 0, rozkaz gracza 0"
RX_PRZYCZYNY = re.compile(r'([a-z][a-z ]*?) (\d+): kupily (\d+), brak miejsca (\d+), w drodze dosc (\d+), cena nie nizsza od sredniej '
                          r'(\d+), bez zysku (\d+), sprzedane tu (\d+), rozkaz gracza (\d+)')
POLA_PRZYCZYN = ('wyj', 'kupily', 'brak_miejsca', 'w_drodze_dosc', 'cena', 'bez_zysku', 'sprzedane_tu', 'rozkaz')


def _extra_przyczyny(t, d):
    r = OrderedDict()
    r['brak_latki'] = 1 if 'BRAK latki BuyGoods' in t else 0
    m = re.search(r'; wyjazdy z miasta z nadwyzka surowca - (.*)$', t)
    if not m:
        raise ValueError('brak czesci o wyjazdach z nadwyzka')
    razem = Counter()
    for x in RX_PRZYCZYNY.finditer(m.group(1)):
        k = SUROWCE_KARAWAN.get(x.group(1).strip(), x.group(1).strip().replace(' ', '_'))
        for pole, v in zip(POLA_PRZYCZYN, x.groups()[1:]):
            r[k + '_' + pole] = int(v)
            razem[pole] += int(v)
    r['nadw_wyj'], r['nadw_kupily'], r['nadw_brak_miejsca'] = razem['wyj'], razem['kupily'], razem['brak_miejsca']
    a, b = d.get('przed_celem'), d.get('przy_wyjezdzie')
    if a is not None and b is not None:
        r['przy_wyjezdzie_proc'] = round(100.0 * b / (a + b), 1) if (a + b) else 0.0
    return r


# "Karawany (kierunek):" (ogniwo 117): "ruda: wjazdy 1, w tym do miasta z brakiem 1 (dostawe dostalo 1 miast), wyjazdy 2, w tym z celem
# w miescie z brakiem 1"; "w jukach teraz: ruda 13 w 4 karawanach (z tego 13 stoi w miastach), ..."
RX_KIERUNEK = re.compile(r'([a-z][a-z ]*?): wjazdy (\d+), w tym do miasta z brakiem (\d+) \(dostawe dostalo (\d+) miast\), wyjazdy (\d+), '
                         r'w tym z celem w miescie z brakiem (\d+)')
RX_JUKI_TERAZ = re.compile(r'([a-z][a-z ]*?) (\d+) w (\d+) karawanach \(z tego (\d+) stoi w miastach\)')


def _extra_kierunek(t, d):
    r = OrderedDict()
    if '; ladunek surowcow - ' not in t or '; w jukach teraz: ' not in t:
        raise ValueError('brak czesci o ladunku albo jukach')
    lad = t.split('; ladunek surowcow - ', 1)[1].split('; w jukach teraz: ', 1)[0]
    for x in RX_KIERUNEK.finditer(lad):
        k = SUROWCE_KARAWAN.get(x.group(1).strip(), x.group(1).strip().replace(' ', '_'))
        for pole, v in zip(('wj', 'wj_brak', 'dostalo', 'wyj', 'wyj_brak'), x.groups()[1:]):
            r[k + '_' + pole] = int(v)
    juki = t.split('; w jukach teraz: ', 1)[1]
    for x in RX_JUKI_TERAZ.finditer(juki):
        k = SUROWCE_KARAWAN.get(x.group(1).strip(), x.group(1).strip().replace(' ', '_'))
        r[k + '_juki'], r[k + '_karawan'], r[k + '_stoi'] = int(x.group(2)), int(x.group(3)), int(x.group(4))
    return r


# "Warsztaty towarowe:" (WorkshopTrade.Daily, ogniwo 116): wedle typu "bakery 5 (pracowalo 5, cykli 15, wynik +81 = 16.2 d na warsztat,
# place 49, utrzymanie 20)"; przyklad miasta "bakery 19768 (zysk sredni z 5 dob 16.2 d na dobe, podatek gracza 0%, 3 x roczny zysk =
# 17687, kapital 2081; wyrob bread x1.00, wsad grain x1.00)" (albo "zysk sredni z 30 dob (pamiec 145) 12.2 d", "zysk oczekiwany z cen
# -4 d", "sam sprzet 600")
RX_TYP_WT = re.compile(r'(\S+) (\d+) \(pracowalo (\d+), cykli (\d+), wynik ([+-]?\d+) = ' + RX_F + r' d na warsztat, place (-?\d+), '
                       r'utrzymanie (-?\d+)\)')
RX_PRZYKLAD_WT = re.compile(
    r'(\S+) (-?\d+) \(zysk (?:sredni z (\d+) dob(?: \(pamiec (\d+)\))? |oczekiwany z cen )' + RX_F + r' d na dobe, podatek gracza '
    + RX_F + r'%, (?:sam sprzet (-?\d+)|' + RX_F + r' x roczny zysk = (-?\d+)), kapital (-?\d+)(?:; wyrob (\S+) x' + RX_F
    + r'(?:, wsad (\S+) x' + RX_F + r')?)?\)')


def _extra_warsztaty_tow(t, d):
    r = OrderedDict()
    m = re.search(r' \| wedle typu: (.*?) \| do kas miast: ', t)
    if not m:
        raise ValueError('brak czesci "wedle typu"')
    typow = 0
    for x in RX_TYP_WT.finditer(m.group(1)):
        k = 'typ_' + _kat_klucz(x.group(1))
        typow += 1
        r[k + '_n'], r[k + '_prac'], r[k + '_cykli'] = int(x.group(2)), int(x.group(3)), int(x.group(4))
        r[k + '_wynik'], r[k + '_dnw'] = int(x.group(5)), _liczba(x.group(6))
    r['typow'] = typow
    m = re.search(r'; przyklad (.+?) \(dobrobyt (-?\d+)\): (.*?)(?: \| zasady: |$)', t)
    if m:
        r['przyklad_miasto'], r['przyklad_dobrobyt'] = m.group(1), int(m.group(2))
        podatki = []
        for x in RX_PRZYKLAD_WT.finditer(m.group(3)):
            g = x.groups()
            podatki.append(_liczba(g[5]))
            if g[0] == 'bakery' and 'piekarnia_cena' not in r:
                r['piekarnia_cena'], r['piekarnia_zysk'] = int(g[1]), _liczba(g[4])
                r['piekarnia_dob'] = int(g[2]) if g[2] is not None else 0
                r['piekarnia_kapital'] = int(g[9])
                r['piekarnia_wyrob'] = g[10] or ''
                r['piekarnia_wyrob_x'] = _liczba(g[11]) if g[11] is not None else None
                r['piekarnia_wsad_x'] = _liczba(g[13]) if g[13] is not None else None
        r['przyklad_warsztatow'] = len(podatki)
        r['podatek_max'] = max(podatki) if podatki else None
    return r


def _extra_wozy(t, d):
    """Linia "Dowoz (wozy):" (MarketCarts.Daily, ogniwo 119)."""
    r = OrderedDict()
    m = re.search(r' - wybor miasta (\w+): ', t)
    if not m:
        raise ValueError('brak "wybor miasta"')
    r['wybor'] = m.group(1)
    m = re.search(r' \| cena ladunku sztuka po sztuce (BRAK LATKI|CZYNNA|WYLACZONA): ', t)
    r['cena_stan'] = m.group(1) if m else '?'
    m = re.search(r' \| woz x[\d.,]+ dla wsi (\w+)', t)
    r['dla_wsi'] = m.group(1) if m else '?'
    r['pelny_wyl'] = 1 if ' szt.) - WYLACZONY;' in t else 0
    r['wiesc_wyl'] = 1 if ' - WYLACZONA; z ruda ' in t else 0
    r['potkn'] = d.get('potkniecia') or 0
    if d.get('wozow'):
        r['do_innego_proc'] = round(100.0 * (d.get('do_innego') or 0) / d['wozow'], 1)
    if d.get('z_ruda'):
        r['ruda_do_pustych_proc'] = round(100.0 * (d.get('ruda_do_pustych') or 0) / d['z_ruda'], 1)
    return r


def _extra_skutki(t, d):
    r = {}
    if d.get('zatk_zamkowe') is not None and d.get('wsi_zamkowych'):
        r['zatk_zamkowe_proc'] = round(100.0 * d['zatk_zamkowe'] / d['wsi_zamkowych'], 1)
    if d.get('zatk_miejskie') is not None and d.get('wsi_miejskich'):
        r['zatk_miejskie_proc'] = round(100.0 * d['zatk_miejskie'] / d['wsi_miejskich'], 1)
    return r


def _extra_karawany(t, d):
    r = OrderedDict()
    for klucz, wz in (('sprz', r'sprzedane miastom z brakiem: (.*?) \(miasta zaplacily'),
                      ('kup', r'kupione z nadwyzek: (.*?) \(karawany zaplacily'),
                      ('oddane', r'zakupy gabki BK oddane na polke: (.*?) \(zwrot')):
        m = re.search(wz, t)
        pary = lista_par(m.group(1)) if m else None
        if pary is None:
            continue
        r[klucz + '_szt'] = sum(pary.values())
        if klucz != 'oddane':
            r[klucz + '_ruda'] = pary.get('ruda', 0)
            r[klucz + '_drewno'] = pary.get('drewno', 0)
    # ogniwo 103 po decyzji 06.10: kolejnosc kupna wedle zysku w danej chwili i oczekiwany zysk z kupionego
    m = re.search(r'wyjazdow, ktore zaczely zakupy od: (.*?); oczekiwany zysk', t)
    if m:
        pary = lista_par(m.group(1))
        r['first_szt'] = sum(pary.values())
        r['first_ruda'] = pary.get('ruda', 0)
        r['first_drewno'] = pary.get('drewno', 0)
        r['first_naj'] = max(pary, key=lambda k: pary[k]).replace(' ', '_') if pary else 'nic'
        r['first_towary'] = ','.join(k.replace(' ', '_') for k in pary if pary[k] > 0)
    m = re.search(r'minus zaplacone\): (.*?) \(razem -?\d+ d\)', t)
    if m:
        pary = lista_par_f(m.group(1))
        r['zysk_ruda_kg'] = pary.get('ruda')
        r['zysk_drewno_kg'] = pary.get('drewno')
    r['wylaczona'] = 1 if 'REGULA WYLACZONA' in t else 0
    return r


SUROWCE_KARAWAN = OrderedDict([('ruda', 'ruda'), ('drewno', 'drewno'), ('skory surowe', 'skory'), ('skora', 'skora'),
                               ('len', 'len'), ('plotno', 'plotno'), ('welna', 'welna')])
# "cena zbytu" (ogniwo 103 po decyzji 06.10) jest opcjonalna: linie starszej postaci jej nie maja
RX_STAN_KARAWAN = re.compile(r'(?:- |; )([a-z][a-z ]*?): ponizej zapasu docelowego (\?|-?\d+) -> (-?\d+) '
                             r'\(bez towaru (\?|-?\d+) -> (-?\d+)\), brakuje (-?\d+), w jukach karawan (-?\d+)'
                             r'(?:, cena zbytu (?:(-?\d+(?:[.,]\d+)?) d \(kupuje (\d+) miast\)|(brak - nikt nie kupuje)))?'
                             r'(, KUPNO STOI)?')


def _extra_stan_karawan(t, d):
    r = OrderedDict()
    stoi, bez_ceny = [], []
    n = 0
    for m in RX_STAN_KARAWAN.finditer(t):
        nazwa = SUROWCE_KARAWAN.get(m.group(1).strip(), m.group(1).strip().replace(' ', '_'))
        n += 1
        r[nazwa + '_ponizej'] = int(m.group(3))
        r[nazwa + '_bez'] = int(m.group(5))
        r[nazwa + '_brak'] = int(m.group(6))
        r[nazwa + '_juki'] = int(m.group(7))
        if m.group(8) is not None:
            r[nazwa + '_cena'] = _liczba(m.group(8))
            r[nazwa + '_kupuje'] = int(m.group(9))
        elif m.group(10):
            r[nazwa + '_cena'] = 0
            r[nazwa + '_kupuje'] = 0
            if int(m.group(6)) > 0:             # miasta z kasa maja brak, a nikt nie daje ceny = wycena sie nie udala
                bez_ceny.append(nazwa)
        r[nazwa + '_stoi'] = 1 if m.group(11) else 0
        if m.group(11):
            stoi.append(nazwa)
    if n == 0:
        raise ValueError('brak surowcow')
    r['kupno_stoi'] = ','.join(stoi) if stoi else 'nie'
    r['bez_ceny'] = ','.join(bez_ceny)
    return r


def _extra_rynek_surowcow(t, d):
    m = re.search(r'zapasy na targach miast \[([^\]]*)\]', t)
    if not m:
        raise ValueError('brak listy')
    pary = lista_par(m.group(1))
    if not pary:
        raise ValueError('pusta lista')
    return pary


def _extra_rynek_broni(t, d):
    r = OrderedDict()
    for nazwa, slowo in (('ruda', 'indeks rudy min/med/max'), ('drewno', 'drewna'), ('skora', 'skory'), ('len', 'lnu')):
        m = re.search(re.escape(slowo) + r' (-?[\d.]+)/(-?[\d.]+)/(-?[\d.]+)', t)
        if m:
            r['idx_%s_min' % nazwa] = _liczba(m.group(1))
            r['idx_%s_med' % nazwa] = _liczba(m.group(2))
            r['idx_%s_max' % nazwa] = _liczba(m.group(3))
    m = re.search(r'premie wojenne: (.*?)\.?$', t)
    if m:
        r['premie'] = 0 if m.group(1).strip().startswith('brak') else m.group(1).count('%')
    return r


def _extra_mineraly(t, d):
    r = OrderedDict()
    if 'latka NIE wpieta' in t:
        r['stan'] = 'NIE wpieta'
    elif 'LATKA WYLACZONA' in t:
        r['stan'] = 'WYLACZONA'
    elif 'zadnego powtorzenia' in t:
        r['stan'] = 'zadnych'
    elif 'powtorzenia zdjete' in t:
        r['stan'] = 'zdjete'
    else:
        raise ValueError('nieznany stan')
    m = re.search(r'\(przedmiot i liczba wsi\): (.*?); razem', t)
    pary = lista_par(m.group(1)) if m else OrderedDict()
    for k in MINERALY_ZNANE:
        r[k] = pary.get(k, 0) if m else None
    obce = [k for k in pary if k not in MINERALY_ZNANE]
    r['obce'] = ','.join(obce) if obce else ''
    return r


UKLAD_102, UKLAD_102B = '102', '102b'      # uklad linii ksiegi pieniadza: wpis 102 (w grze dzis) i poprawka n102b-ksiega


def _extra_bilans(t, d):
    r = OrderedDict()
    zr, uj, re_ = d.get('zrodla'), d.get('ujscia'), d.get('reszta')
    if zr is not None and uj is not None and re_ is not None:
        ruch = abs(zr) + abs(uj)
        r['ruch'] = ruch
        r['reszta_proc'] = round(100.0 * abs(re_) / ruch, 1) if ruch else None
    # 102b: zold siedzi w "rozliczeniach rodow" i jest podany osobno ("W tym zold naliczony"); 102: zold jest pozycja ujsc
    r['uklad'] = UKLAD_102B if ('rozliczenia rodow na ' in t or 'W tym zold naliczony' in t) else UKLAD_102
    r['rody_niewpiete'] = 1 if 'licznik rozliczen rodow nie jest wpiety' in t else 0
    return r


def _extra_rody(t, d):
    r = OrderedDict()
    r['niewpiety'] = 1 if 'licznik rozliczen rodow nie jest wpiety' in t else 0
    if d.get('zmiana') is None and not r['niewpiety']:
        raise ValueError('brak zmiany zlota swiata')
    if d.get('zmiana') is None:                 # licznik nie wpiety: linia podaje sam zold
        m = re.search(r' zold naliczony (-?\d+) \[P\] siedzi w tych saldach', t)
        if m:
            r['zold'] = int(m.group(1))
    a, b = d.get('inne_z_niczego'), d.get('inne_w_nicosc')
    r['inne_razem'] = (a or 0) + (b or 0) if (a is not None or b is not None) else None
    return r


def _extra_przeplywy(t, d):
    r = {'licznik_slepy': 1 if 'licznik nie widzial wyplat' in t else 0}
    m = re.search(r'Potkniecia licznikow: okna taborow niedomkniete (\d+), konsumpcja bez pary (\d+), '
                  r'regulator pytany poza konsumpcja (\d+), wyjatki (\d+)', t)
    r['potkniecia'] = sum(int(x) for x in m.groups()) if m else 0
    r['uklad'] = UKLAD_102B if 'zold naliczony przy rozliczeniach rodow' in t else UKLAD_102
    return r


def _extra_kasy(t, d):
    r = {}
    znane = 0
    for k in ('taborom', 'z_utargu', 'zywnosc_wsi', 'sakwy', 'zakupy', 'regulator', 'przelewy', 'poza_tickiem', 'tick',
              'warsztaty_tow'):
        v = d.get(k)
        if isinstance(v, (int, float)):
            znane += abs(v)
    r['ruch'] = znane
    return r


def _extra_kasy_zamkow(t, d):
    """Linia "Kasy zamkow:" (ogniwo 110, CastlePurse.Daily)."""
    r = OrderedDict()
    if d.get('brakuje') is not None and d.get('zapas'):
        r['brakuje_proc'] = round(100.0 * d['brakuje'] / d['zapas'], 2)
    a, b = d.get('chcial_dosypac'), d.get('chcial_skasowac')
    r['regulator_chcial'] = (a or 0) + (b or 0) if (a is not None or b is not None) else None
    a, b = d.get('dosypac_tickow'), d.get('skasowac_tickow')
    r['regulator_tickow'] = (a or 0) + (b or 0) if (a is not None or b is not None) else None
    return r


def _extra_kasy_miast(t, d):
    """Linia "Kasy miast:" (ogniwo 111, TownPurse.Daily)."""
    r = OrderedDict()
    r['zakupy_zostaja'] = 1 if 'zloto z niczego ZOSTAJE w kasach' in t else 0
    r['zawor_nie_dziala'] = 1 if 'NIE DZIALA - renty od ludnosci wylaczone' in t else 0
    if 'gorna galaz BK (1% kasy dziennie w nicosc): ' in t:
        ogon = t.split('gorna galaz BK (1% kasy dziennie w nicosc): ', 1)[1]
        r['bk'] = 'pominieta' if ogon.startswith('pominieta w ') else ('BRAK latki' if ogon.startswith('BRAK latki') else (
            'zostaje' if ogon.startswith('zostaje (ustawienie)') else '?'))
    if d.get('brakuje') is not None and d.get('zapas'):
        r['brakuje_proc'] = round(100.0 * d['brakuje'] / d['zapas'], 2)
    z, p, k = d.get('zeszlo'), d.get('panom'), d.get('koronie')
    if z is not None and p is not None and k is not None:
        r['zawor_rozjazd'] = z - p - k
        r['panom_proc'] = round(100.0 * p / z, 1) if z else None
    return r


def _stan_strumienia(sekcja):
    """Stan jednego strumienia linii "Utarg wsi:": czynne / WYLACZONE / nie wpieta."""
    if sekcja is None:
        return None
    if 'latka nie jest wpieta' in sekcja:
        return 'nie wpieta'
    if 'WYLACZON' in sekcja:
        return 'WYLACZONE'
    return 'czynne'


def _extra_utarg(t, d):
    """Linia "Utarg wsi:" (ogniwo 112, VillageTakings.Daily): trzy strumienie - powroty taborow, zywnosc, sakwy taborow."""
    r = OrderedDict()
    sek = {}
    for czesc in t.split(' | '):
        for klucz, znak in (('powroty', 'powroty taborow z utargiem '), ('zywnosc', 'zywnosc kupiona we wsiach '),
                            ('sakwy', 'tabory zniszczone z gotowka: ')):
            if znak in czesc and klucz not in sek:
                sek[klucz] = czesc
    if 'sakwy' not in sek:
        raise ValueError('brak sekcji taborow')
    for klucz in ('powroty', 'zywnosc', 'sakwy'):
        r[klucz + '_stan'] = _stan_strumienia(sek.get(klucz))
    zn = [d.get(k) for k in ('nieprzypisane', 'zyw_zniklo', 'tab_zniklo')]
    r['zniklo_razem'] = sum(v or 0 for v in zn) if any(v is not None for v in zn) else None
    m = re.search(r' powrotow bez podatku BK: (.*?)\), nieprzypisane ', t)
    r['bez_podatku_wsie'] = m.group(1) if m else ''
    a, b = d.get('tab_bitwa'), d.get('tab_rozw')
    r['tab_razem'] = (a or 0) + (b or 0) if (a is not None or b is not None) else None
    return r


RX_REGION_SPUST = re.compile(r'^(.*?)\s*\[(miasto|zamek|wies), ([^\]]*)\] uchodzcy (-?\d+) z (-?\d+) ludzi wsi '
                             r'\((-?[\d.,]+)%\), plon najslabszej wsi x(-?[\d.,]+), zabici od poczatku (-?\d+)')


def _extra_spustoszenie(t, d):
    """Linia "Ludzie (spustoszenie):" (ogniwo 113, Devastation.RegionsNote)."""
    r = OrderedDict()
    rab, lud = d.get('rabunki'), d.get('ludzi_rab')
    if rab and isinstance(lud, (int, float)):
        r['na_rabunek'] = round(lud / float(rab), 1)
    gl = d.get('gra_ludzi')
    if isinstance(lud, (int, float)) and gl:
        r['wobec_gry_proc'] = round(100.0 * lud / gl, 2)
    m = re.search(r'regiony najbardziej spustoszone \((\d+)\): (.*?)(?: \| |\.?$)', t)
    regiony = []
    if m:
        for czesc in m.group(2).split('; '):
            mm = RX_REGION_SPUST.match(czesc.strip())
            if mm:
                regiony.append((mm.group(1), mm.group(3), int(mm.group(4)), int(mm.group(5)), _liczba(mm.group(6)),
                                _liczba(mm.group(7))))
        r['top_ile'] = int(m.group(1))
    if regiony:
        r['top1'] = regiony[0][0]
        r['top1_kr'] = regiony[0][0][:10].strip()
        r['top1_proc'] = regiony[0][4]
        r['top1_uchodzcy'] = regiony[0][2]
        r['top1_kultura'] = regiony[0][1]
        r['lista'] = ', '.join('%s %s%%' % (x[0], x[4]) for x in regiony)
    return r


# nazwa regionu moze byc pusta (osada bez nazwy w probie poza gra) - wtedy czesc zaczyna sie od "[miasto, ...]"
RX_REGION = re.compile(r'^(.*?)\s*\[(miasto|zamek), ([^\]]*)\] ludzie (-?\d+)(?: \(([+-]\d+)\))?, zaloga (\d+), '
                       r'wyrzutki (\d+), bandy (\d+) w (\d+), zabici dzis (\d+), zwerbowani dzis (\d+), '
                       r'obciazenie (-?[\d.]+)%')


def _extra_regiony(t, d):
    m = re.search(r'ludnosci\): (.*)$', t)
    if not m:
        raise ValueError('brak listy regionow')
    regiony = []
    for czesc in m.group(1).rstrip('.').split('; '):
        mm = RX_REGION.match(czesc.strip())
        if mm:
            regiony.append((mm.group(1), mm.group(2), mm.group(3), int(mm.group(4)), int(mm.group(6)),
                            int(mm.group(7)), int(mm.group(8)), float(mm.group(12))))
    if not regiony:
        raise ValueError('zaden region nie pasuje')
    r = OrderedDict()
    r['top1'] = regiony[0][0]
    r['top1_kr'] = regiony[0][0][:10].strip()
    r['top1_proc'] = regiony[0][7]
    r['top1_ludzie'] = regiony[0][3]
    r['ostatni_proc'] = regiony[-1][7]
    r['ponad_20'] = sum(1 for x in regiony if x[7] > 20.0)
    r['lista'] = ', '.join('%s %.1f%%' % (x[0], x[7]) for x in regiony)
    return r


def _extra_ludnosc(t, d):
    r = {}
    if d.get('zaplacone') is not None and d.get('nalezne'):
        r['zaplacone_proc'] = round(100.0 * d['zaplacone'] / d['nalezne'], 1)
    return r


def _extra_paser(t, d):
    r = OrderedDict()
    r['skup_wylaczony'] = 1 if 'skup lupu WYLACZONY' in t else 0
    r['kurek_otwarty'] = 1 if 'KUREK OTWARTY w ustawieniach - gra dopisala' in t else 0      # kurek zlota kryjowek
    r['kurek_zywnosci'] = 1 if ('zywnosc z niczego: KUREK OTWARTY' in t or 'zywnosci paser nie bierze' in t) else 0
    r['obieg_wylaczony'] = 1 if 'OBIEG KAS WYLACZONY' in t else 0
    a, b = d.get('zabl_bandom'), d.get('zabl_kryjowkom')
    r['zabl_razem'] = (a or 0) + (b or 0) if (a is not None or b is not None) else None
    if d.get('z_ladunkiem') is not None and d.get('z_paserem') is not None:
        r['bez_pasera'] = d['z_ladunkiem'] - d['z_paserem']
    a, b = d.get('zycie_bandy'), d.get('zycie_kryjowki')
    r['zycie_razem'] = (a or 0) + (b or 0) if (a is not None or b is not None) else None
    potk = [d.get(k) for k in ('potk_skup', 'potk_kryjowki', 'potk_zycie', 'potk_awanse')]
    r['potk_razem'] = sum(v or 0 for v in potk)
    return r


def _extra_ludzie(t, d):
    r = OrderedDict()
    r['nieskalibrowana'] = 1 if 'ludnosc nieskalibrowana' in t else 0
    # ogniwo 108: odcinek "hearth za ludzi dzis (...)" - jednostka 1 czlowiek = 1/k hearth
    if 'hearth za ludzi dzis (' in t:
        r['jednostka'] = 'WYLACZONA' if 'jednostka ludzi WYLACZONA' in t else 'czynna'
        r['od_ludnosci'] = 1 if 'wyrzutki liczone od ludnosci' in t else 0
        if d.get('hz_tabory') and d.get('hz_gra') is not None:
            r['hz_gra_do_naszych'] = round(d['hz_gra'] / float(d['hz_tabory']), 1)
    # ogniwo 113: odcinek "spustoszenie dzis: ..." (Devastation.DayNote) albo "spustoszenie WYLACZONE (...)"
    if ' | spustoszenie dzis: zdjeci z wsi ' in t:
        r['spustoszenie'] = 'czynne'
        czesci = [d.get(k) for k in ('sp_zabici', 'sp_w_las', 'sp_uchodzcy')]
        if d.get('sp_zdjeci') is not None and all(isinstance(v, (int, float)) for v in czesci):
            r['sp_rozjazd'] = round(d['sp_zdjeci'] - sum(czesci), 1)
    elif ' | spustoszenie WYLACZONE (' in t:
        r['spustoszenie'] = 'WYLACZONE'
    return r


RX_KRAINA = re.compile(r'([\w-]+) ([+-]?\d+(?:[.,]\d+)?)% \(([+-]?\d+)\)')


def _extra_przyrost(t, d):
    r = OrderedDict()
    r['wylaczony'] = 1 if 'przyrost naturalny WYLACZONY' in t else 0
    if d.get('ludzi') is None and not r['wylaczony']:
        raise ValueError('brak liczby ludzi przyrostu')
    r['pierwsza_doba'] = 1 if 'rozliczenie zmiany od jutra' in t else 0
    # ogniwo 113: przy czynnym spustoszeniu rabunki i zerowanie armii sa w "ruch ludzi", nie w RESZCIE
    if ', ruch ludzi (' in t:
        r['spust_w_ruchu'] = 1 if ', spustoszenie i powroty uchodzcow) ' in t else 0
    if ' | miasta: ' in t:
        r['zamrozone'] = 1 if 'stan (zamrozony)' in t else 0
        r['miasta_nieskalibrowane'] = 1 if 'miasta: ludnosc nieskalibrowana' in t else 0
    m = re.search(r'krainy g rocznie \(ludzi dzis\): (.*?)(?: \| |$)', t)
    if m:
        kr = [(x.group(1), _liczba(x.group(2)), int(x.group(3))) for x in RX_KRAINA.finditer(m.group(1))]
        r['krain'] = len(kr)
        if kr:
            r['kraina_g_min'] = min(k[1] for k in kr)
            r['kraina_g_max'] = max(k[1] for k in kr)
    # sprawdzian z opisu paczki 109: N x dni roku / ludnosc wsi ma dawac "g srednio" - tu sama stopa z "w rok ok."
    if d.get('ludzi') is not None and d.get('w_rok') is not None and d['ludzi']:
        r['dni_roku'] = int(round(d['w_rok'] / float(d['ludzi'])))
    return r


def _extra_zold(t, d):
    r = OrderedDict()
    r['tarcza_wl'] = 1 if 'tarcza zoldu w kasach miast: wlaczona' in t else 0
    # ogniwo 111 (K6): przy czynnym TownPurse linia konczy sie dopiskiem o zalogach bez zwrotu korony, a wlaczona tarcza ma
    # dopisek "- ZBEDNA i nieczynna" - regulator kas miast niczego nie kasuje, tarcza nic nie robi (nie wolno jej liczyc za OK)
    r['k6'] = 1 if ' | zalogi bez zwrotu korony (' in t else 0
    r['tarcza_zbedna'] = 1 if ' - ZBEDNA i nieczynna' in t else 0
    r['tarcza_czynna'] = 1 if (r['tarcza_wl'] and not r['tarcza_zbedna'] and not r['k6']) else 0
    zeszlo = [d.get('rody_zeszlo'), d.get('gar_zeszlo')]
    dalej = [d.get(k) for k in ('do_sakiewek', 'do_miast', 'do_zamkow', 'nieumarli', 'bez_osady', 'wylaczone')]
    if all(v is not None for v in zeszlo) and all(v is not None for v in dalej):
        r['zeszlo_razem'] = sum(zeszlo)
        r['rozliczone'] = sum(dalej)
        r['rozjazd'] = sum(zeszlo) - sum(dalej)
    if d.get('rody_naliczony') is not None and d.get('gar_naliczony') is not None:
        r['naliczony_razem'] = d['rody_naliczony'] + d['gar_naliczony']
    return r


RX_SKARBIEC = re.compile(
    r'^dzien \d+ - (.+?)( \(WOJNA\))?: skarbiec (-?\d+)(?: \(([+-]?\d+)\))?'
    r'(?:, zwrot zoldu (-?\d+) dla (\d+) rodow(?: \(nalezne (-?\d+) - skarbiec nie mial dosc\))?)?'
    r', krol (.*?) kiesa (-?\d+) bilans ([+-]?\d+)/dzien; rodow (\d+), kiesy razem (-?\d+), '
    r'biednych \(<(-?\d+)\) (\d+), na minusie (\d+); wojsko rodow (\d+) ludzi')


def _extra_skarbiec(t, d):
    m = RX_SKARBIEC.search(t[t.find('dzien '):] if 'dzien ' in t else t)
    if not m:
        raise ValueError('linia krolestwa nie pasuje')
    g = m.groups()
    return OrderedDict([
        ('krolestwo', g[0]), ('wojna', 1 if g[1] else 0), ('skarbiec', int(g[2])),
        ('skarbiec_zm', int(g[3]) if g[3] is not None else None),
        ('zwrot', int(g[4]) if g[4] is not None else None), ('zwrot_rodow', int(g[5]) if g[5] is not None else None),
        ('zwrot_nalezne', int(g[6]) if g[6] is not None else None),
        ('krol', g[7]), ('krol_kiesa', int(g[8])), ('krol_bilans', int(g[9])), ('rodow', int(g[10])),
        ('kiesy', int(g[11])), ('biednych', int(g[13])), ('na_minusie', int(g[14])), ('wojsko', int(g[15])),
    ])


def _agreg_skarbce(wiersze):
    r = OrderedDict()
    r['krolestw'] = len(wiersze)
    r['wojna'] = sum(w.get('wojna') or 0 for w in wiersze)
    r['skarbce'] = sum(w.get('skarbiec') or 0 for w in wiersze)
    zm = [w.get('skarbiec_zm') for w in wiersze if w.get('skarbiec_zm') is not None]
    r['skarbce_zm'] = sum(zm) if zm else None
    zw = [w.get('zwrot') for w in wiersze if w.get('zwrot') is not None]
    r['zwrot'] = sum(zw) if zw else None
    r['zwrot_krolestw'] = len(zw) if zw else None
    r['zwrot_niepelny'] = sum(1 for w in wiersze if w.get('zwrot_nalezne') is not None) if zw else None
    r['kiesy'] = sum(w.get('kiesy') or 0 for w in wiersze)
    r['rodow'] = sum(w.get('rodow') or 0 for w in wiersze)
    r['biednych'] = sum(w.get('biednych') or 0 for w in wiersze)
    r['na_minusie'] = sum(w.get('na_minusie') or 0 for w in wiersze)
    r['wojsko'] = sum(w.get('wojsko') or 0 for w in wiersze)
    naj = min(wiersze, key=lambda w: w.get('skarbiec') if w.get('skarbiec') is not None else 0)
    r['min_skarbiec'] = naj.get('skarbiec')
    r['min_krolestwo'] = naj.get('krolestwo')
    r['ujemnych'] = sum(1 for w in wiersze if (w.get('skarbiec') or 0) < 0)
    r['_nr'] = wiersze[0].get('_nr')
    return r


def _agreg_wojny(wiersze):
    return OrderedDict([('premii', len(wiersze)),
                        ('maks_proc', max((w.get('razem') or 0) for w in wiersze)), ('_nr', wiersze[0].get('_nr'))])


def _kol_pieniadz():
    k = []
    k += KD('razem', '| razem ', wym=True)
    k += KD('kasy_miast', 'osady: kasy miast ', wym=True)
    k += KD('kasy_zamkow', ', kasy zamkow ')
    k += KD('kiesy_wsi', ', kiesy wsi ')
    k += KD('kryjowki', ', kryjowki i inne ')
    k += KD('glowy_rodow', 'bohaterowie: glowy rodow ', wym=True)
    k += KD('lordowie', ', pozostali lordowie ')
    k += KD('gracz', ', gracz ')
    k += KD('notable', ', notable ')
    k += KD('wedrowcy', ', wedrowcy ')
    k += KD('inni', ', inni ')
    k += KD('skarbce', '| skarbce krolestw ')
    k += KD('warsztaty', '| kapital warsztatow ')
    k += KD('sakiewki', '| sakiewki ludzi ')
    k += KD('bank', '| Bank Zelazny ')
    k += KD('tabory_wsi', 'partie bez wodza: tabory wsi ')
    k += KD('karawany', ', karawany ')
    k += KD('garnizony', ', garnizony ')
    k += KD('bandy', ', bandy ')
    k += KD('inne_partie', ', inne ')
    k += KD('cla', 'cla miast i zamkow ')
    k += KD('podatek_wsi', ', podatek wsi ')
    return k


def _kol_kasy():
    def para(nazwa):
        return r'%s \+(-?\d+)/-(-?\d+)' % re.escape(nazwa)
    return [
        K('stan', '| stan ', wym=True),
        K('zmiana', ', zmiana ', 'sint', wym=True),
        K('taborom', 'zaplata taborom wsi ', 'sint'),
        K('z_utargu', 'z utargu taborow ', 'sint'),
        # ogniwo 112 (K7), tylko kiesy wsi: cena zywnosci, ktora gra kasuje po zakupie we wsi, i to, co K7 oddal
        K('zywnosc_wsi', 'zywnosc kupiona we wsiach ', 'sint'),
        K('zyw_skasowala', ' [P] (gra skasowala ', 'sint'),
        K('zyw_kiesom', ', K7 oddal kiesom +'),
        K('zyw_panom', ', licznikom panow '),
        K('sakwy', 'sakwy rozwiazanych taborow ', 'sint'),
        K('zakupy', '"zakupy" mieszkancow ', 'sint'),
        K('zakupy_tickow', rx=r'mieszkancow [+-]?\d+ \[P\] \((\d+) tickow osad', lit=[' tickow osad)']),
        K('regulator', 'regulator kasy ', 'sint'),
        K('dosypal', '(dosypal '),
        K('skasowal', ', skasowal '),
        K('reg_tickow', rx=r'skasowal -?\d+; (\d+) tickow', lit=[' tickow)']),
        K('przelewy', 'przelewy gry (GiveGoldAction) ', 'sint'),
        K('lordowie', rx=para('lordowie'), lit=['lordowie']),
        K('notable', rx=para('notable'), lit=['notable']),
        K('karawany', rx=para('karawany'), lit=['karawany']),
        K('poza_tickiem', 'Armoury poza tickiem dobowym ', 'sint'),
        K('zakupy_ai', rx=para('zakupy sprzetu AI'), lit=['zakupy sprzetu AI']),
        K('najemnicy', rx=para('najemnicy z karczmy'), lit=['najemnicy z karczmy']),
        K('warsztaty_zbr', rx=para('warsztaty zbrojne'), lit=['warsztaty zbrojne']),
        K('paser_sprzet', rx=para('paser band (sprzet dla band)'), lit=['paser band (sprzet dla band)']),
        K('zold_garnizonow', rx=para('zold garnizonow'), lit=['zold garnizonow']),
        K('zycie', rx=para('sakiewki ludzi - zycie w miastach'), lit=['sakiewki ludzi - zycie w miastach']),
        # ogniwo 116 (WorkshopTrade -> MoneyLedger.NoteWorkshopPay): place za cykle i utrzymanie z kapitalu warsztatow do kas miast
        K('warsztaty_tow', 'warsztaty towarowe - place i utrzymanie z kapitalu warsztatow ', 'sint'),
        K('warsztaty_tow_place', rx=r'warsztatow [+-]?\d+ \[P\] \(place za cykle \+(-?\d+)', lit=[' [P] (place za cykle +']),
        K('warsztaty_tow_utrz', rx=r'\(place za cykle \+-?\d+, utrzymanie \+(-?\d+)\)', lit=[', utrzymanie +']),
        K('tick', 'Armoury tick dobowy ', 'sint'),
        K('renty', rx=r'[(,] ?renty ([+-]\d+)', lit=['renty']),
        K('budowy', rx=r'[(,] ?budowy ([+-]\d+)', lit=['budowy']),
        K('korona', 'korona (danina, clo, mennica) ', 'sint'),
        K('pozostale', 'pozostale moduly ticku ', 'sint'),
        K('paser_skup', 'paser band (skup lupu) ', 'sint'),
        K('zycie_band', 'bandy i kryjowki (zycie w miastach) ', 'sint'),
        # ogniwa 110 i 111 (K5, K6): zawor kas zamkow i miast oraz jednorazowe przyciecie daru startowego
        K('danina_podzamcza', 'danina podzamcza (kasy zamkow -> panowie) ', 'sint'),
        K('dar_zamkow', 'dar startowy kas zamkow przyciety (raz na kampanie, w nicosc) ', 'sint'),
        K('dar_miast', 'dar startowy kas miast przyciety (raz na kampanie, w nicosc) ', 'sint'),
        K('udzial_korony', 'udzial korony z zaworu kas miast (kasy miast -> skarbce krolestw) ', 'sint'),
        # opis reszty kies wsi zmienia ogniwo 112: do 111 "(m.in. podatek gry od zakupow we wsi)", od 112 "(m.in. doplaty
        # notabli i odplyw BK ponad limit kiesy, zakupy wsi z targowiskiem)" - wyrazenie czyta oba
        K('reszta', rx=r'reszta - [^;|]*? ([+-]?\d+) \[R\]', wym=True,
          lit=['reszta - inne niezmierzone (i regulator, gdy jego licznik stoi na 0 tickow) ',
               'reszta - niezmierzone (m.in. doplaty notabli i odplyw BK ponad limit kiesy, zakupy wsi z targowiskiem) ']),
        K('osad', 'rozklad [P]: osad '),
        K('min', ', min '),
        K('mediana', ', mediana '),
        K('max', ', max '),
        K('ponizej_1000', ', ponizej 1000 zlota '),
    ]


SEK_ZOLN = 'zolnierze: '
SEK_DZIS = 'dzis: zabici '
# sekcja zoldu w "Przeplywy osad:": uklad wpisu 102 i uklad 102b (ten sam licznik, inny naglowek)
SEK_ZOLD = ('zold wyplacony [P]', 'zold naliczony przy rozliczeniach rodow [P]')
SEK_HZ = 'hearth za ludzi dzis ('
SEK_SP = 'spustoszenie dzis: zdjeci z wsi '
SEK_ROZL = 'hearth wsi swiata '

TEMATY = [
    # ------------------------------------------------------------------------------------------------ surowce
    Temat('Ruda', 'Ruda', r'^dzien \d+', _kol_ksiegi('kopalnie miast i zamkow'), _extra_ksiegi, SKROT_KSIEGI, 'surowce',
          'OreLedger.cs', bezwar=True),
    Temat('Drewno', 'Drewno', r'^dzien \d+', _kol_ksiegi('miasta i zamki'), _extra_ksiegi, SKROT_KSIEGI, 'surowce',
          'OreLedger.cs', bezwar=True),
    # ogniwo 115 (RawPrice.Daily, zaraz po "Ruda:" i "Drewno:"): indeks i ceny 7 surowcow masowych w miastach, popyt z prawdziwego
    # zuzycia, przyklady innych przeliczonych towarow (grain, salt, beer) i - od ogniwa 118 - towary z wartoscia z definicji (BK)
    Temat('Ceny surowcow', 'Ceny surowcow', r'^dzien \d+ - popyt z prawdziwego zuzycia ', [], _extra_ceny, [
        ('ruda: indeks med/max | bez towaru miast', '{ruda_idx_med}/{ruda_idx_max}|{ruda_bez}'),
        ('ruda: 1. sztuka w pustym d | popyt dane', '{ruda_placa}|{ruda_popyt_dane}'),
        ('drewno: indeks med/max | 1. sztuka d', '{drewno_idx_med}/{drewno_idx_max}|{drewno_placa}'),
        ('welna: indeks med/max | sol/piwo max', '{welna_idx_med}/{welna_idx_max}|{inne_salt_max}/{inne_beer_max}'),
        ('chleb: indeks med | na polkach | budzet', '{def_bread_med}|{def_bread_szt}|{def_bread_budzet}'),
    ], 'surowce', 'RawPrice.cs', bezwar=True),
    Temat('Dowoz', 'Dowoz', r'^dzien \d+', [
        K('wyslane', 'wyslane na targ miasta ', wym=True),
        K('brak_targu', 'do zamku: brak targu '),
        K('za_daleko', ', targ za daleko '),
        K('wojna', ', oblezenie albo wojna '),
        # ogniwo 110 (K5): tabor wsi z targiem za daleko jedzie na daleki targ, gdy zamek nie ma na caly ladunek
        K('daleki_targ', '; na daleki targ, bo zamek nie mial czym zaplacic: '),
        K('w_drodze', '); w drodze teraz ', wym=True),
        K('do_miasta', ': do miasta '),
        K('do_zamku', ', do zamku '),
        K('rozbite', 'rozbite tabory wsi (wszystkich) '),
        K('przez_bandy', ', w tym przez bandy '),
        K('wsi_zamkowych', '; wsi zamkowych '),
        K('targ_wlasny', 'targ we wlasnym krolestwie '),
        K('targ_obcy', ', w obcym '),
        K('bez_targu', ', bez targu '),
    ], _extra_dowoz, [('tabory: wyslane/w drodze/rozbite', '{wyslane}/{w_drodze}/{rozbite}')],
          'surowce', 'MarketRoad.cs', bezwar=True),
    Temat('Dowoz (skutki)', 'Dowoz (skutki)', r'^dzien \d+', [
        K('zatk_zamkowe', 'zatkane magazyny: wsie zamkowe ', wym=True),
        K('wsi_zamkowych', rx=r'wsie zamkowe \d+ z (\d+)', lit=['zatkane magazyny: wsie zamkowe ']),
        K('zatk_miejskie', ', wsie miejskie ', wym=True),
        K('wsi_miejskich', rx=r'wsie miejskie \d+ z (\d+)', lit=[', wsie miejskie ']),
        K('zamki_glodne', 'zamki: bilans zywnosci ujemny '),
        K('bez_polki_ujemny', ', bez polki ujemny '),
        K('ratuje_polka', '(dzis ratuje polka '),
        K('zywnosc_z_polki', 'zywnosc z polki razem ', 'float'),
        K('pusty_spichlerz', ', pusty spichlerz '),
        K('kasy_miast', '; kasy miast razem ', wym=True),
        K('ponizej_progu', ', ponizej progu rent '),
    ], _extra_skutki, [('zatkane magazyny %: zamkowe/miejskie', '{zatk_zamkowe_proc}/{zatk_miejskie_proc}'),
                       ('kasy miast | ponizej progu rent', '{kasy_miast} | {ponizej_progu}')],
          'surowce', 'MarketRoad.cs', bezwar=True),
    # ogniwo 119 (MarketCarts.Daily, zaraz po "Dowoz:"): wybor najlepiej placacego miasta, wiesc z drogi, cena ladunku sztuka po sztuce
    Temat('Dowoz (wozy)', 'Dowoz (wozy)', r'^dzien \d+ - wybor miasta ', [
        K('wozow', rx=r' - wybor miasta \w+: (\d+) wozow', wym=True, lit=[' wozow (wsi zamkowych ']),
        K('wsi_zamkowych', ' wozow (wsi zamkowych '),
        K('do_wlasnego', ', do wlasnego miasta '),
        K('do_innego', ', do innego '),
        K('bez_miasta', ', bez miasta w zasiegu '),
        K('w_miescie', ', w miescie (jak dotad) '),
        K('srednio', '; srednio ', 'float'),
        K('zasieg', ' jedn. drogi (zasieg '),
        K('zasieg_gry', ', gry dla targu wsi '),
        K('utarg', '; oczekiwany utarg '),
        K('utarg_wybrane', rx=r'wlasne miasto bylo w grze: (-?\d+) d wobec', lit=[' (tam, gdzie wlasne miasto bylo w grze: ', ' d wobec ']),
        K('utarg_wlasne', rx=r' d wobec (-?\d+) d we wlasnym\)', lit=[' d we wlasnym)']),
        K('pelny', '; pelny woz na daleka droge '),
        K('doladowane', ' (doladowane '),
        K('w_drodze', '; wiesc z drogi: wozow w drodze '),
        K('miast_wiesc', rx=r'wozow w drodze -?\d+, miast (\d+)', lit=[', miast ']),
        K('wygasle', ', wygasle przy porzadkach '),
        K('z_ruda', '; z ruda '),
        K('ruda_do_pustych', ', w tym do miasta bez rudy '),
        K('ruda_miast', rx=r'do miasta bez rudy -?\d+ \(roznych miast (\d+)\)', lit=[' (roznych miast ']),
        K('wycen', '; wycen '),
        K('cen', rx=r'; wycen -?\d+ \(cen (\d+), ', lit=[' (cen ']),
        K('ms', rx=r'\(cen \d+, ' + RX_F + r' ms\)', lit=[' ms)']),
        K('sprzedazy', ': sprzedazy '),
        K('pierwsza', ', po cenie pierwszej sztuki '),
        K('sztuka', ' d, sztuka po sztuce '),
        K('nadplata', ' d, tabory oddaly osadom nadplate '),
        K('nadplata_proc', rx=r'osadom nadplate -?\d+ d \(' + RX_F + '%', lit=[' d (']),
        K('nadplata_max', ', najwieksza '),
        K('niezgodne', ', niezgodne ', wym=True),
        K('cena_rosla', ', cena rosla '),
        K('woz_x', ' | woz x', 'float'),
        K('potkniecia', ' | POTKNIECIA '),
    ], _extra_wozy, [
        ('wozow | do wlasnego/innego/bez miasta', '{wozow}|{do_wlasnego}/{do_innego}/{bez_miasta}'),
        ('srednio jedn. drogi | wygasle | ms', '{srednio}|{wygasle}|{ms}'),
        ('z ruda / do miasta bez rudy (miast)', '{z_ruda}/{ruda_do_pustych} ({ruda_miast})'),
        ('nadplata d (%) | niezgodne/cena rosla', '{nadplata}({nadplata_proc}%)|{niezgodne}/{cena_rosla}'),
    ], 'surowce', 'MarketCarts.cs', bezwar=True),
    Temat('Karawany', 'Karawany', r'^dzien \d+', [
        K('wjazdy', 'wjazdy karawan do miast ', wym=True),
        K('ze_sprzedaza', ', w tym ze sprzedaza surowcow '),
        K('miasta_zaplacily', ' (miasta zaplacily '),
        K('wyjazdy', '; wyjazdy z miast ', wym=True),
        K('z_zakupem', ', w tym z zakupem '),
        K('karawany_zaplacily', ' (karawany zaplacily '),
        K('zwrot_gabki', ' (zwrot '),
        K('odp_w_drodze_dosc', 'zakup odpuszczony: w drodze dosc '),
        K('odp_cena', ', cena nie nizsza od sredniej '),
        K('odp_bez_zysku', ', bez zysku wobec ceny zbytu '),
        K('odp_brak_miejsca', ', brak miejsca w jukach '),
        K('odp_sprzedane_tu', ', sprzedane tu w tej wizycie '),
        K('odp_rozkaz', ', rozkaz gracza (tylko zywnosc) '),
        K('wstrzymana_rezerwa', '; sprzedaz wstrzymana rezerwa kasy miasta '),
        K('bk_wyzerowane', '; wyceny zakupu BK wyzerowane '),
        K('zysk_razem', rx=r'minus zaplacone\): .*? \(razem (-?\d+) d\)', lit=[' (razem ', ' d)']),
    ], _extra_karawany, [
        ('wjazdy/sprzedaz | wyjazdy/zakup', '{wjazdy}/{ze_sprzedaza}|{wyjazdy}/{z_zakupem}'),
        ('sprzedane (ruda) | kupione (ruda)', '{sprz_szt}({sprz_ruda})|{kup_szt}({kup_ruda})'),
        ('zaplacily: miasta / karawany', '{miasta_zaplacily}/{karawany_zaplacily}'),
        ('odpuszczone: dosc w drodze/cena/juki', '{odp_w_drodze_dosc}/{odp_cena}/{odp_brak_miejsca}'),
        ('bez zysku | oczek. zysk d | zaczely od', '{odp_bez_zysku}|{zysk_razem}|{first_naj}'),
    ], 'surowce', 'CaravanBulk.cs'),
    Temat('Karawany (stan)', 'Karawany (stan)', r'^dzien \d+', [
        K('miast', ' - miast ', wym=True),
        K('karawan', ', karawan '),
    ], _extra_stan_karawan, [
        ('ruda: miast ponizej zapasu/bez towaru', '{ruda_ponizej}/{ruda_bez}'),
        ('ruda: brakuje / w jukach karawan', '{ruda_brak}/{ruda_juki}'),
        ('ruda: cena zbytu d (kupuje miast)', '{ruda_cena} ({ruda_kupuje})'),
        ('drewno: ponizej/bez | brakuje/w jukach', '{drewno_ponizej}/{drewno_bez}|{drewno_brak}/{drewno_juki}'),
        ('KUPNO STOI (surowce)', '{kupno_stoi}'),
    ], 'surowce', 'CaravanBulk.cs'),
    # ogniwo 117 (CaravanBulk.Daily, "poprawka 115" w kodzie): zakup surowcow przed wyborem celu i juki do 100% udzwigu
    Temat('Karawany (przyczyny)', 'Karawany (przyczyny)', r'^dzien \d+ - zakup surowcow przed wyborem celu ', [
        K('przed_celem', ' - zakup surowcow przed wyborem celu ', wym=True),
        K('przy_wyjezdzie', ' wizyt, przy wyjezdzie ', wym=True),
        K('kg', '; surowce zajely '),
        K('kg_ponad', ' kg jukow, z tego '),
        K('prog', rx=r'BK nie uzywa; prog (\d+)%\)', lit=[' kg ponad 80% udzwigu (miejsce, ktorego BK nie uzywa; prog ', '%)']),
    ], _extra_przyczyny, [
        ('zakup przed celem / przy wyjezdzie', '{przed_celem}/{przy_wyjezdzie}'),
        ('juki: kg surowcow / ponad 80%', '{kg}/{kg_ponad}'),
        ('ruda z nadwyzka: wyj/kupily/brak miejsca', '{ruda_wyj}/{ruda_kupily}/{ruda_brak_miejsca}'),
    ], 'surowce', 'CaravanBulk.cs', bezwar=True),
    Temat('Karawany (kierunek)', 'Karawany (kierunek)', r'^dzien \d+ - wjazdy do ', [
        K('miast_wjazdy', ' - wjazdy do ', wym=True, lit=[' - wjazdy do ', ' roznych miast; karawan w miastach ']),
        K('w_miastach', '; karawan w miastach ', wym=True),
        K('w_drodze', rx=r'karawan w miastach -?\d+, w drodze (-?\d+)', lit=[', w drodze ']),
    ], _extra_kierunek, [
        ('karawan w miastach / w drodze', '{w_miastach}/{w_drodze}'),
        ('ruda: wjazdy (do braku) | wyjazdy (cel)', '{ruda_wj}({ruda_wj_brak})|{ruda_wyj}({ruda_wyj_brak})'),
        ('ruda: dostawe dostalo miast | w jukach', '{ruda_dostalo}|{ruda_juki}'),
    ], 'surowce', 'CaravanBulk.cs', bezwar=True),
    Temat('Warsztaty', 'Warsztaty', r'^dzien \d+', [
        K('wykonano', ' - wykonano ', wym=True),
        K('koszt', '], koszt '),
        K('sprzedaz', ', sprzedaz '),
        K('bez_zysku', '; odpuszczone: bez zysku '),
        K('brak_surowca', ', brak surowca ', wym=True),
        K('bs_ruda', ' [ruda '),
        K('bs_drewno', rx=r'brak surowca \d+ \[ruda \d+, drewno (\d+)', lit=[', drewno ']),
        K('bs_skora', rx=r'brak surowca \d+ \[[^\]]*?, skora (\d+)', lit=[', skora ']),
        K('bs_len', ', len/welna '),
        K('w_robocie', 'w robocie (cykle) '),
        K('brak_zlota', ', brak zlota/kupca '),
        K('rozpoczete', '; rozpoczete sztuki '),
        K('w_toku', ', w toku teraz '),
        K('skor', 'rzemieslnicy miasta wygarbowali skor '),
        K('plotna', ', utkali plotna '),
        K('z_niczego_cykle', 'z niczego zablokowane: cykle rzemieslnikow '),
        K('losowanie', ', sztabki/wegiel z losowania -> ruda/drewno '),
    ], None, [
        ('wykonano szt | koszt/sprzedaz', '{wykonano}|{koszt}/{sprzedaz}'),
        ('odpuszczone: brak surowca | w robocie', '{brak_surowca} | {w_robocie}'),
        ('brak wg surowca: ruda/drewno/skora/len', '{bs_ruda}/{bs_drewno}/{bs_skora}/{bs_len}'),
    ], 'surowce', 'WorkshopLaw.cs'),
    # ogniwo 116 (WorkshopTrade.Daily, zdarzenie DailyTickEvent): warsztaty towarowe w nowej monecie - utrzymanie i place do kas miast,
    # wynik wedle typu, wyplaty, bankructwa, cena kupna (przyklad: Lannisport - zgloszenie Jeffa o piekarni)
    Temat('Warsztaty towarowe', 'Warsztaty towarowe', r'^dzien \d+ \| warsztatow ', [
        K('warsztatow', ' | warsztatow ', wym=True),
        K('rozliczonych', ' (bez ukrytych rzemieslnikow), rozliczonych dzis '),
        K('pracowalo', rx=r'rozliczonych dzis -?\d+, pracowalo (-?\d+)', lit=[', pracowalo ']),
        K('cykli', ' (cykli towarowych '),
        K('bez_kapitalu', '), bez kapitalu na utrzymanie '),
        K('zysk', ' | wynik dnia po placach i utrzymaniu: zysk +'),
        K('strata', ', strata -'),
        K('wynik', rx=r', strata -?\d+, razem ([+-]?\d+)', lit=[', razem ']),
        K('place', ' | do kas miast: place za cykle ', wym=True),
        K('place_kapital', rx=r'place za cykle -?\d+ \(z kapitalu (-?\d+)', lit=[' (z kapitalu ']),
        K('place_kiesa', rx=r'place za cykle -?\d+ \(z kapitalu -?\d+, z kiesy gracza (-?\d+)\)', lit=[', z kiesy gracza ']),
        K('utrzymanie', rx=r'\), utrzymanie (-?\d+) \(z kapitalu', lit=['), utrzymanie ']),
        K('utrzymanie_kiesa', rx=r'\), utrzymanie -?\d+ \(z kapitalu -?\d+, z kiesy gracza (-?\d+)\)', lit=[', z kiesy gracza ']),
        K('sprzet', '), sprzet przy zmianie produkcji '),
        K('niezaplacone', '; niezaplacone (pusty kapital) '),
        K('wypl_notable', '(wyplata zysku wlascicielom, monopol korony): notable '),
        K('wypl_lordowie', rx=r'monopol korony\): notable -?\d+, lordowie (-?\d+)', lit=[', lordowie ']),
        K('wypl_gracz', rx=r'monopol korony\): notable -?\d+, lordowie -?\d+, gracz (-?\d+)', lit=[', gracz ']),
        K('bankructwa', ' | bankructwa '),
        K('bankr_wlozyli', ' (nowi wlasciciele wlozyli z wlasnych kies '),
        K('bez_chetnego', '), bez chetnego z pieniedzmi '),
        K('sprz_zaplacone', '; sprzedaz gracza notablom: zaplacone '),
        K('sprz_zabraklo', ', zabraklo kupcowi '),
        K('cena_med', ' | cena kupna dla gracza: mediana '),
        K('cena_min', rx=r'dla gracza: mediana -?\d+, od (-?\d+) do', lit=[', od ', ' do ']),
        K('cena_max', rx=r'dla gracza: mediana -?\d+, od -?\d+ do (-?\d+)', lit=[' do ']),
        K('cena_n', rx=r' do -?\d+ \((\d+) warsztatow\)', lit=[' warsztatow)']),
        K('utrzymanie_stawka', ' | zasady: utrzymanie ', 'float'),
        K('potkniecia', rx=r'; potkniecia (-?\d+)\.?$', wym=True, lit=['; potkniecia ']),
    ], _extra_warsztaty_tow, [
        ('warsztatow / pracowalo (cykli)', '{warsztatow}/{pracowalo} ({cykli})'),
        ('wynik dnia | do kas: place/utrzymanie', '{wynik:+}|{place}/{utrzymanie}'),
        ('piekarnia: cena | zysk d | chleb x', '{piekarnia_cena}|{piekarnia_zysk}|{piekarnia_wyrob_x}'),
        ('bankructwa/bez chetnego | potkniecia', '{bankructwa}/{bez_chetnego}|{potkniecia}'),
    ], 'surowce', 'WorkshopTrade.cs', bezwar=True),
    Temat('Rynek surowcow', 'Rynek surowcow', r'^zapasy na targach miast', [], _extra_rynek_surowcow, [
        ('targi miast: ruda/drewno', '{iron}/{hardwood}'),
        ('targi miast: skora/skory surowe', '{leather}/{hides}'),
        ('targi miast: plotno/len/welna', '{linen}/{flax}/{wool}'),
    ], 'surowce', 'ArmsPricing.cs'),
    Temat('Rynek broni', 'Rynek broni', r'^na polkach', [
        K('na_polkach', 'na polkach ', wym=True),
    ], _extra_rynek_broni, [
        ('bron na polkach | indeks ruda/drewno', '{na_polkach}|{idx_ruda_med}/{idx_drewno_med}'),
    ], 'surowce', 'ArmsPricing.cs'),
    Temat('Mineraly (dubel BK)', 'Mineraly (dubel BK)', r'^dzien \d+', [
        K('razem', '; razem '),
        K('wywolan', ' zdjetych wpisow w '),
        K('wsi_mnoznik', '; wsi z mnoznikiem modelu teraz '),
        K('potkniecia', '; potkniecia od wczytania kampanii ', wym=True),
    ], _extra_mineraly, [
        ('wsie z dublem: iron/salt/clay/silver', '{iron}/{salt}/{clay}/{silver}'),
        ('zdjetych wpisow|stan|wsi z mnoznikiem', '{razem}|{stan}|{wsi_mnoznik}'),
    ], 'surowce', 'MineralOnce.cs'),
    # ------------------------------------------------------------------------------------------------ pieniadz
    Temat('Pieniadz swiata', 'Pieniadz swiata', r'^dzien \d+', _kol_pieniadz(), None, [
        ('zloto swiata razem (zmiana dobowa)', '{razem} ({razem_zm:+})'),
        ('kasy miast / kasy zamkow', '{kasy_miast}/{kasy_zamkow}'),
        ('kiesy wsi / kryjowki i inne', '{kiesy_wsi}/{kryjowki}'),
        ('glowy rodow / pozostali lordowie', '{glowy_rodow}/{lordowie}'),
        ('notable / kapital warsztatow', '{notable}/{warsztaty}'),
        ('skarbce krolestw / Bank Zelazny', '{skarbce}/{bank}'),
        ('sakiewki ludzi / kiesy band', '{sakiewki}/{bandy}'),
    ], 'pieniadz', 'MoneyLedger.cs', bezwar=True),
    Temat('Pieniadz swiata (bilans)', 'Pieniadz swiata (bilans)', r'^dzien \d+', [
        K('zmiana', '| zmiana sumy ', 'sint', wym=True),
        K('zrodla', rx=r'zmierzone zrodla z niczego \+(-?\d+)', wym=True, lit=['zmierzone zrodla z niczego +']),
        K('zakupy', 'mieszkancow miast i zamkow '),
        K('dosypal', ', regulator kas dosypal '),
        # uklad 102b: rozliczenia rodow zmierzone stanem zlota swiata (zrodlo "na plus", ujscie "na minus")
        K('rody_plus', ', rozliczenia rodow na plus '),
        K('rody_plus_n', rx=r'rozliczenia rodow na plus -?\d+ w (\d+) rodach', lit=[' w ', ' rodach, ']),
        # ogniwo 107 na ukladzie 102b: zold, ktory SoldierPay oddal do obiegu, jest osobnym zrodlem
        K('oddany', 'zold oddany do obiegu przez SoldierPay '),
        K('oddany_sakiewki', ' (sakiewki ludzi '),
        K('oddany_kasy', ', kasy osad '),
        K('z_niczego', rx=r', GiveGoldAction z niczego (?:poza rozliczeniami rodow )?(-?\d+)',
          lit=['), GiveGoldAction z niczego poza rozliczeniami rodow ']),
        K('ujscia', ' - zmierzone ujscia w nicosc ', wym=True),
        K('rody_minus', '(rozliczenia rodow na minus '),
        K('rody_minus_n', rx=r'rozliczenia rodow na minus -?\d+ w (\d+) rodach', lit=[' rodach, regulator kas skasowal ']),
        # zold: uklad 102 "(zold N, regulator kas skasowal", uklad 102b " W tym zold naliczony N [P]" na koncu linii
        K('zold', rx=r'(?:\(zold (?:naliczony )?|W tym zold naliczony )(-?\d+)', lit=[' W tym zold naliczony ']),
        K('skasowal', 'regulator kas skasowal '),
        K('zniklo', ', z utargu wsi zniklo '),
        # ogniwo 112 (K7): dwa ujscia, ktore dotad siedzialy w reszcie - cena zywnosci kupionej we wsi i sakwy taborow
        K('zyw_zniklo', ', z ceny zywnosci kupionej we wsiach zniklo ', 'sint'),
        K('zyw_skasowala', rx=r'kupionej we wsiach zniklo -?\d+ \(gra skasowala (-?\d+)', lit=[' (gra skasowala ']),
        K('zyw_oddane', ', oddane wsiom i panom '),
        K('sakwy_zniklo', ', z sakw zniszczonych taborow wsi zniklo ', 'sint'),
        K('sakwy_bylo', ' (bylo w nich '),
        K('sakwy_oddane', ', oddane zwyciezcom i wsiom '),
        K('w_nicosc', rx=r', GiveGoldAction w nicosc (?:poza rozliczeniami rodow )?(-?\d+)',
          lit=[', GiveGoldAction w nicosc poza rozliczeniami rodow ']),
        K('levy_oddal', rx=r'w nicosc (?:poza rozliczeniami rodow )?-?\d+ minus (-?\d+) oddane',
          lit=[' minus ', ' oddane przez LevyGold notablom i miastom)']),
        # ogniwa 110 i 111 (K5, K6): dar startowy kas zdjety raz na kampanie stoi w ujsciach (dopisek po nawiasie ujsc)
        K('dar_zamkow', ' w tym dar startowy kas zamkow przyciety przez CastlePurse '),
        K('dar_miast', ' w tym dar startowy kas miast przyciety przez TownPurse '),
        K('reszta', ' + reszta ', 'sint', wym=True),
    ], _extra_bilans, [
        ('zmiana sumy | reszta [R]', '{zmiana:+}|{reszta:+}'),
        ('zmierzone zrodla / ujscia', '{zrodla}/{ujscia}'),
        ('reszta jako % ruchu (zrodla + ujscia)', '{reszta_proc}'),
        ('zrodla: zakupy mieszkancow/regulator', '{zakupy}/{dosypal}'),
        ('zold naliczony | regulator skasowal', '{zold}|{skasowal}'),
        ('z niczego|utarg wsi zniklo|w nicosc', '{z_niczego}|{zniklo}|{w_nicosc}'),
        ('rozliczenia rodow: na plus/na minus', '{rody_plus}/{rody_minus}'),
        ('zold oddany do obiegu (SoldierPay)', '{oddany}'),
    ], 'pieniadz', 'MoneyLedger.cs'),
    Temat('Pieniadz swiata (rody)', 'Pieniadz swiata (rody)', r'^dzien \d+', [
        K('zmiana', 'zmienily zloto swiata o ', 'sint'),
        K('rozliczen', rx=r'\[P\] w (\d+) rozliczeniach', lit=[' [P] w ', ' rozliczeniach: ']),
        K('na_plus_n', rx=r'rozliczeniach: (\d+) na plus', lit=[' rozliczeniach: ', ' na plus +']),
        K('na_plus', ' na plus +'),
        K('na_minus_n', rx=r', (\d+) na minus ', lit=[' na minus ']),
        K('na_minus', rx=r', \d+ na minus (-?\d+)', lit=[' na minus ']),
        K('bez_zmiany', rx=r', (\d+) bez zmiany',
          lit=[' bez zmiany (pomiar: suma wszystkich posiadaczy tuz przed i tuz po kazdym rozliczeniu)']),
        K('saldo_plus', ' | z tego saldo dopisane glowom rodow +'),
        K('saldo_plus_n', rx=r'saldo dopisane glowom rodow \+-?\d+ \((\d+) rodow\)', lit=[' rodow) / ']),
        K('saldo_minus', ' rodow) / '),
        K('saldo_minus_n', rx=r' rodow\) / -?\d+ \((\d+) rodow\)',
          lit=[' rodow) [P] (kwota ze zdarzenia gry - z pustej kiesy schodzi mniej), zmiany poza saldem ']),
        K('poza_saldem', ', zmiany poza saldem ', 'sint'),
        K('zold', ' | zold naliczony '),
        K('bez_zoldu', ' [P] jest czescia tych rozliczen; bez niego zmienilyby zloto swiata o ', 'sint'),
        K('oddal', ' Z zoldu SoldierPay oddal do obiegu '),
        K('inne_z_niczego', ' Inne GiveGoldAction w trakcie rozliczen (sa w zmianie zlota swiata): z niczego +'),
        K('inne_w_nicosc', rx=r'w trakcie rozliczen \(sa w zmianie zlota swiata\): z niczego \+-?\d+, w nicosc (-?\d+)',
          lit=[', w nicosc ']),
        K('niedomkniete', ' Rozliczenia niedomkniete (wyjatek w kodzie gry albo moda): '),
    ], _extra_rody, [
        ('rozliczenia rodow: zmiana zlota (ile)', '{zmiana:+} ({rozliczen})'),
        ('saldo glowom rodow: na plus/na minus', '{saldo_plus}/{saldo_minus}'),
        ('poza saldem | bez zoldu byloby', '{poza_saldem:+}|{bez_zoldu:+}'),
    ], 'pieniadz', 'MoneyLedger.cs'),
    Temat('Przeplywy osad', 'Przeplywy osad', r'^dzien \d+ \| utarg', [
        K('miasta_zaplacily', 'utarg taborow wsi [P]: miasta zaplacily ', wym=True),
        K('miasta_wizyt', rx=r'miasta zaplacily -?\d+ \((\d+) wizyt', lit=[' wizyt), zamki ']),
        K('zamki_zaplacily', ' wizyt), zamki '),
        K('zamki_wizyt', rx=r' wizyt\), zamki -?\d+ \((\d+) wizyt', lit=[' wizyt); towar niesprzedany po wizycie: w miastach ']),
        K('niesprz_miasta', 'towar niesprzedany po wizycie: w miastach '),
        K('niesprz_zamki', ' wizyt), w zamkach '),
        K('oddaly', 'powrot do wsi: tabory oddaly ', wym=True),
        K('powrotow', rx=r'\[P\] w (\d+) powrotach', lit=[' powrotach = pan (licznik podatku wsi) ']),
        K('pan', 'pan (licznik podatku wsi) '),
        K('majatki_bk', '+ wlasciciele majatkow BK '),
        K('kiesa_wsi', '+ kiesa wsi '),
        K('zniklo', '+ zniklo ', 'sint'),
        K('zniklo_proc', rx=r'\+ zniklo -?\d+ \[R\] \((-?[\d.,]+)%', lit=[') + zniklo ']),
        # ogniwo 112 (K7): to, co VillageTakings dopisal wsiom, siedzi juz w "kiesa wsi" i "pan" - tu sam dopisek
        K('k7_do_kies', '; w tym dopisane wsiom przez K7: do kies '),
        K('k7_na_liczniki', rx=r'przez K7: do kies -?\d+, na liczniki panow (-?\d+)', lit=[', na liczniki panow ']),
        K('zold_rody', sek=SEK_ZOLD, rx=r'partie rodow (-?\d+) \(', lit=['partie rodow', ' partii, z niedoplata ']),
        K('zold_garnizony', sek=SEK_ZOLD, rx=r', garnizony (-?\d+) \(', lit=['garnizony']),
        K('zold_karawany', sek=SEK_ZOLD, rx=r', karawany (-?\d+) \(', lit=['karawany']),
        K('zold_inne', sek=SEK_ZOLD, rx=r', inne (-?\d+) \(', lit=['inne']),
        K('niedoplata_rody', sek=SEK_ZOLD, rx=r'partie rodow -?\d+ \(\d+ partii, z niedoplata (\d+)\)',
          lit=[' partii, z niedoplata ']),
        K('zold_razem', ', razem ', sek=SEK_ZOLD, wym=True,
          lit=[' | zold naliczony przy rozliczeniach rodow [P]: ', ', razem ']),
        K('do_sakiewek', '; z tego przekazano do sakiewek ludzi '),
        K('do_kas', ', do kas osad '),
    ], _extra_przeplywy, [
        ('utarg taborow wsi: miasta/zamki', '{miasta_zaplacily}/{zamki_zaplacily}'),
        ('tabory oddaly we wsi | zniklo [R]', '{oddaly}|{zniklo:+}'),
        ('zold: partie rodow/garnizony', '{zold_rody}/{zold_garnizony}'),
        ('zold przekazany: sakiewki/kasy osad', '{do_sakiewek}/{do_kas}'),
    ], 'pieniadz', 'MoneyLedger.cs'),
    Temat('Przeplywy osad (modele)', 'Przeplywy osad', r'^modele czynne', [], None, [], 'pieniadz', 'MoneyLedger.cs',
          dzienny=False),
    Temat('Przeplywy osad (kasy miast)', 'Przeplywy osad (kasy miast)', r'^dzien \d+', _kol_kasy(), _extra_kasy, [
        ('stan (zmiana)', '{stan} ({zmiana:+})'),
        ('regulator kasy | reszta [R]', '{regulator:+}|{reszta:+}'),
        ('renty / zaplata taborom wsi', '{renty:+}/{taborom:+}'),
    ], 'pieniadz', 'MoneyLedger.cs'),
    Temat('Przeplywy osad (kasy zamkow)', 'Przeplywy osad (kasy zamkow)', r'^dzien \d+', _kol_kasy(), _extra_kasy, [
        ('stan (zmiana)', '{stan} ({zmiana:+})'),
        ('regulator kasy | reszta [R]', '{regulator:+}|{reszta:+}'),
    ], 'pieniadz', 'MoneyLedger.cs'),
    Temat('Przeplywy osad (kiesy wsi)', 'Przeplywy osad (kiesy wsi)', r'^dzien \d+', _kol_kasy(), _extra_kasy, [
        ('stan (zmiana)', '{stan} ({zmiana:+})'),
        ('z utargu taborow | reszta [R]', '{z_utargu:+}|{reszta:+}'),
    ], 'pieniadz', 'MoneyLedger.cs'),
    # ogniwo 110 (K5, CastlePurse.Daily): kasa zamku jako prawdziwy pieniadz - zapas kupcow, zawor (danina podzamcza), tabory
    Temat('Kasy zamkow', 'Kasy zamkow', r'^dzien \d+ \| stan ', [
        K('stan', ' | stan ', wym=True),
        K('zamkow', rx=r'\| stan -?\d+ w (\d+) zamkach', lit=[' w ', ' zamkach: zapas kupcow ']),
        K('zapas', ' zamkach: zapas kupcow ', wym=True),
        K('ponad', ', ponad zapasem '),
        K('brakuje', ', do zapasu brakuje ', wym=True),
        K('naj_nadwyzka', '; najwieksza nadwyzka '),
        K('ponad_bk', rx=r'BK kasuje tam 1% dziennie\): (\d+)', lit=[' x dobrobyt - BK kasuje tam 1% dziennie): ']),
        K('chcial_dosypac', ' | regulator gry zablokowany: z dzisiejszych kas chcial dosypac z niczego '),
        K('dosypac_tickow', rx=r'chcial dosypac z niczego -?\d+ \((\d+) tickow zamkow', lit=[' tickow zamkow) i skasowac ']),
        K('chcial_skasowac', ' tickow zamkow) i skasowac '),
        K('skasowac_tickow', rx=r'i skasowac -?\d+ \((\d+) tickow\)', lit=[' tickow)']),
        K('cofniete', ' | "zakupy" ludnosci zamkow: zloto z niczego cofniete '),
        K('cofniete_tickow', rx=r'cofniete -?\d+ \(w (\d+) z \d+ tickow', lit=[' (w ', ' tickow; towar zjedzony jak dotad)']),
        K('zakupy_tickow', rx=r'cofniete -?\d+ \(w \d+ z (\d+) tickow', lit=[' z ', ' tickow; towar zjedzony jak dotad)']),
        K('danina', ' | danina podzamcza: ', wym=True),
        K('danina_zamkow', ' do panow z '),
        K('danina_proc', rx=r' do panow z \d+ zamkow \(' + RX_F + '% nadwyzki ponad zapas',
          lit=[' zamkow (', '% nadwyzki ponad zapas']),
        K('danina_gracz', '; w tym rod gracza '),
        K('bez_nadwyzki', '); bez poboru: kasa nie ponad zapasem '),
        K('oblezone', ', oblezone '),
        K('bez_pana', ', bez pana '),
        K('tabory_do_zamku', ' | tabory wsi z targiem za daleko: do zamku (ma czym zaplacic) '),
        K('tabory_daleki', ', na daleki targ (zamek nie mial na caly ladunek) '),
        K('tabory_wartosc', ' - ladunki warte '),
        K('potkniecia', ' | potkniecia (wyjatki, pierwszy w logu): '),
    ], _extra_kasy_zamkow, [
        ('stan/zapas kupcow | do zapasu brakuje', '{stan}/{zapas}|{brakuje}'),
        ('danina podzamcza (zamkow)|ponad lim.BK', '{danina} ({danina_zamkow})|{ponad_bk}'),
        ('regulator chcial | "zakupy" cofniete', '{regulator_chcial}|{cofniete}'),
    ], 'pieniadz', 'CastlePurse.cs'),
    Temat('Kasy zamkow (wylaczone)', 'Kasy zamkow', r'^dzien \d+ \| WYLACZONE', [], None, [], 'pieniadz', 'CastlePurse.cs',
          dzienny=False),
    # ogniwo 111 (K6, TownPurse.Daily): kasa miasta - regulator bez kasowania, "zakupy" cofane, zawor dzielony pan / korona
    Temat('Kasy miast', 'Kasy miast', r'^dzien \d+ \| tryb regulatora ', [
        K('tryb', ' | tryb regulatora ', wym=True),
        K('stan', ' | stan ', wym=True),
        K('miast', rx=r'\| stan -?\d+ w (\d+) miastach', lit=[' w ', ' miastach: zapas kupcow ']),
        K('zapas', ' miastach: zapas kupcow ', wym=True),
        K('ponad', ', ponad zapasem '),
        K('brakuje', ', do zapasu brakuje ', wym=True),
        K('naj_nadwyzka', '; najwieksza nadwyzka '),
        K('ponizej_1000', '; miast z kasa ponizej 1000: '),
        K('ponad_bk', rx=r'miast ponad limitem kasy BK \([^)]*\): (\d+)',
          lit=['; miast ponad limitem kasy BK (', ' x dobrobyt): ']),
        K('dosypal', ' | regulator gry: dosypal z niczego ', wym=True),
        K('dosypal_tickow', rx=r'dosypal z niczego -?\d+ \((\d+) tickow miast',
          lit=[' tickow miast - bezpiecznik ponizej zapasu), kasowanie zablokowane ']),
        K('kasowanie_zabl', ' tickow miast - bezpiecznik ponizej zapasu), kasowanie zablokowane '),
        K('kasowanie_tickow', rx=r'kasowanie zablokowane -?\d+ \((\d+) tickow',
          lit=[' tickow - tyle chcial dzis skasowac), dosypka zablokowana ']),
        K('dosypka_zabl', ' tickow - tyle chcial dzis skasowac), dosypka zablokowana '),
        K('cofniete', rx=r'mieszczan: zloto z niczego cofniete (-?\d+)',
          lit=[' | "zakupy" mieszczan: ', 'zloto z niczego cofniete ']),
        K('cofniete_tickow', rx=r'mieszczan: zloto z niczego cofniete -?\d+ \(w (\d+) z \d+ tickow',
          lit=[' (w ', ' tickow; towar zjedzony jak dotad)']),
        K('zakupy_tickow', rx=r'mieszczan: zloto z niczego (?:cofniete -?\d+ \(w \d+ z |ZOSTAJE w kasach \([^;]*; )(\d+) tickow',
          lit=[' z ', ' tickow)']),
        K('zawor_proc', rx=r'\| zawor \(' + RX_F + '% nadwyzki ponad zapas', lit=[' | zawor (']),
        K('zeszlo', rx=r'bez pulapu naleznej\): zeszlo (-?\d+)',
          lit=['% nadwyzki ponad zapas, bez pulapu naleznej): ', 'zeszlo ']),
        K('panom', ' = panom '),
        K('panom_miast', rx=r' = panom -?\d+ z (\d+) miast', lit=[' z ', ' miast']),
        K('gracz', ' (w tym rod gracza '),
        K('koronie', ' + skarbcom krolestw '),
        K('krolestw', rx=r'skarbcom krolestw -?\d+ \((\d+) krolestw\)',
          lit=[' krolestw); renta nalezna od ludnosci tych miast (dawny pulap, juz tylko dla porownania) ']),
        K('nalezna', ' krolestw); renta nalezna od ludnosci tych miast (dawny pulap, juz tylko dla porownania) '),
        K('bez_nadwyzki', '; bez poboru: kasa nie ponad zapasem '),
        K('malutka', ', nadwyzka ponizej 1 d dziennie '),
        K('dekret', ', pan zwolnil miasto dekretem (zostaje sam udzial korony) '),
        K('bez_krolestwa', ', miast bez krolestwa (calosc dla pana) '),
        K('z_panem', '; miast z zywym panem '),
        K('bk_miast', rx=r'nicosc\): pominieta w (\d+) miastach', lit=['pominieta w ', ' miastach i ']),
        K('bk_zamkow', ' miastach i '),
        K('bk_zloto', ' zamkach (BK skasowalby ok. '),
        K('potkniecia', ' | potkniecia (wyjatki, pierwszy w logu): '),
    ], _extra_kasy_miast, [
        ('stan/zapas kupcow | do zapasu brakuje', '{stan}/{zapas}|{brakuje}'),
        ('dosypal|kasowanie zabl.|"zakupy" cofn.', '{dosypal}|{kasowanie_zabl}|{cofniete}'),
        ('zawor: zeszlo = panom + koronie', '{zeszlo}={panom}+{koronie}'),
    ], 'pieniadz', 'TownPurse.cs'),
    Temat('Kasy miast (wylaczone)', 'Kasy miast', r'^dzien \d+ \| (?:WYLACZONE|Town Purse Regulator )', [], None, [],
          'pieniadz', 'TownPurse.cs', dzienny=False),
    # ogniwo 112 (K7, VillageTakings.Daily): utarg wsi bez znikania - powroty taborow, zywnosc kupiona we wsi, sakwy taborow
    Temat('Utarg wsi', 'Utarg wsi', r'^dzien \d+ \| powroty taborow', [
        K('powrotow', rx=r'powroty taborow z utargiem (\d+): oddane', lit=[' | powroty taborow z utargiem ', ': oddane ']),
        K('oddane', ': oddane '),
        K('pan', ' = pan (licznik podatku) '),
        K('pan_proc', rx=r'pan \(licznik podatku\) -?\d+ \(' + RX_F + r'% po dopisaniu\)',
          lit=[' po dopisaniu) + wlasciciele majatkow BK ']),
        K('majatki', ' po dopisaniu) + wlasciciele majatkow BK '),
        K('kiesa_bk', ' + kiesa wsi od BK '),
        K('bez_odbiorcy', ' + bez odbiorcy '),
        K('dopisane_kiesom', '; z tego dopisane kiesom wsi '),
        K('wies_proc', rx=r' \(wies razem ' + RX_F + r'% utargu\)', lit=[' (wies razem ', ' utargu)']),
        K('dopisane_panom', ', licznikom panow ', sek='powroty taborow z utargiem '),
        K('bez_podatku_bk', rx=r', licznikom panow -?\d+ \((\d+) powrotow bez podatku BK', lit=[' powrotow bez podatku BK']),
        K('nieprzypisane', '), nieprzypisane '),
        K('nic_nie_zniklo', '; powroty, w ktorych nic nie zniklo '),
        K('nadwyzka_bk', ', z nadwyzka rozliczona przez BK '),
        K('okna', ', okna niedomkniete '),
        K('zyw_zakupow', rx=r'zywnosc kupiona we wsiach (\d+) zakupow',
          lit=[' | zywnosc kupiona we wsiach ', ' zakupow: kupcy zaplacili ']),
        K('zyw_zaplacili', ' zakupow: kupcy zaplacili '),
        K('zyw_skasowala', ', gra skasowala '),
        K('zyw_za_duzo', rx=r'; w tym (\d+) zakupow, w ktorych zdjela wiecej',
          lit=['; w tym ', ' zakupow, w ktorych zdjela wiecej, niz zaplacono)']),
        K('zyw_panom', '; oddane: licznikom panow '),
        K('zyw_kiesom', ', kiesom wsi ', sek='zywnosc kupiona we wsiach '),
        K('zyw_zniklo', rx=r'(?:, zniklo |zwrot WYLACZONY w ustawieniach - zniklo )(-?\d+)', sek='zywnosc kupiona we wsiach ',
          lit=[', zniklo ', '; zwrot WYLACZONY w ustawieniach - zniklo ']),
        K('tab_bitwa', ' | tabory zniszczone z gotowka: w bitwie ', wym=True),
        K('tab_bitwa_zl', rx=r'w bitwie \d+ \((-?\d+) po dzialce zwyciezcow\)', lit=[' po dzialce zwyciezcow), rozwiazane ']),
        K('tab_rozw', ' po dzialce zwyciezcow), rozwiazane '),
        K('tab_rozw_zl', rx=r'zwyciezcow\), rozwiazane \d+ \((-?\d+)\)', lit=[' po dzialce zwyciezcow), rozwiazane ']),
        K('tab_wodzom', '; oddane: wodzom zwyciezcow '),
        K('tab_bandom', ', kiesom band '),
        K('tab_wsiom_kiesy', ', wsiom macierzystym '),
        K('tab_wsiom_panom', ' do kies i '),
        K('tab_zniklo', rx=r'(?: na liczniki panow, zniklo |zwrot WYLACZONY w ustawieniach - zniklo )(-?\d+)',
          sek='tabory zniszczone z gotowka: ',
          lit=[' na liczniki panow, zniklo ', '; zwrot WYLACZONY w ustawieniach - zniklo ']),
        K('wyjatki', ' | wyjatki zlapane '),
    ], _extra_utarg, [
        ('powroty: oddane | wies razem % utargu', '{oddane}|{wies_proc}'),
        ('dopisane kiesom/panom | zywn. skasow.', '{dopisane_kiesom}/{dopisane_panom}|{zyw_skasowala}'),
        ('zniklo: nieprzyp./zywnosc/sakwy tab.', '{nieprzypisane}/{zyw_zniklo}/{tab_zniklo}'),
    ], 'pieniadz', 'VillageTakings.cs', bezwar=True),
    Temat('Utarg wsi (latki)', 'Utarg wsi (latki)', None, [], None, [], 'pieniadz', 'VillageTakings.cs', dzienny=False),
    Temat('Korona (powinnosci)', 'Korona', r'^dzien \d+ - powinnosci wasali', [
        K('powinnosci', ' - powinnosci wasali ', wym=True),
        K('rodow', ' zl od '),
        K('gracz', ' (rod gracza '),
    ], None, [('powinnosci wasali (rodow)', '{powinnosci} ({rodow})')], 'pieniadz', 'KingdomTreasury.cs'),
    Temat('Korona (danina i clo)', 'Korona', r'^dzien \d+ - danina wojenna', [
        K('danina', ' - danina wojenna z kas osad ', wym=True),
        K('clo', ', clo od handlu miast '),
        K('mennica', ' (do skarbcow krolestw); mennica '),
        K('monopole', ', monopole '),
    ], None, [('danina wojenna/clo | mennica/monopole', '{danina}/{clo}|{mennica}/{monopole}')],
          'pieniadz', 'KingdomTreasury.cs'),
    Temat('Korona (zwrot zoldu)', 'Korona', r'^dzien \d+ - zwrot zoldu', [
        K('zwrot', ' - zwrot zoldu ze skarbcow krolestw w wojnie: ', wym=True),
        K('rodow', ' zl dla '),
        K('krolestw', rx=r' rodow w (\d+) krolestwach', lit=[' rodow w ', ' krolestwach (']),
        K('procent', rx=r'krolestwach \((-?[\d.,]+)% z ', lit=['% z ']),
        K('zaplacony', rx=r'% z (-?\d+) zaplaconego zoldu', lit=[' zaplaconego zoldu = nalezne ']),
        K('nalezne', ' zaplaconego zoldu = nalezne '),
        K('nie_mial', '); skarbiec nie mial dosc w '),
        K('pusty', ' krolestwach (w tym pusty: '),
        K('niedoplata', '), niedoplata '),
        K('zostalo', '; w skarbcach tych krolestw zostalo '),
        K('gracz', '; rod gracza dostal '),
        K('dluznym', '; z tego '),
        K('dluznych_rodow', rx=r'; z tego -?\d+ dla (\d+) rodow dluznych', lit=[' rodow dluznych koronie (']),
        K('bez_pokoj', 'Bez zwrotu: zold krolestw w pokoju '),
        K('bez_krolestwa', ', rodow bez krolestwa '),
        K('bez_najemnikow', ', najemnikow '),
        K('potkniecia', ' Potkniecia: wyjatek przy '),
    ], None, [
        ('zwrot zoldu / nalezne', '{zwrot}/{nalezne}'),
        ('rodow/krolestw|brak w skarbcu (pusty)', '{rodow}/{krolestw}|{nie_mial}({pusty})'),
    ], 'pieniadz', 'KingdomTreasury.cs'),
    Temat('Skarbce', 'Skarbce', r'^dzien \d+', [], _extra_skarbiec, [
        ('krolestw (wojna) | na minusie/biedne', '{krolestw}({wojna})|{na_minusie}/{biednych}'),
        ('skarbce razem (zmiana dobowa)', '{skarbce} ({skarbce_zm:+})'),
        ('kiesy rodow | wojsko rodow', '{kiesy}|{wojsko}'),
        ('zwrot zoldu (krolestw/niepelnych)', '{zwrot} ({zwrot_krolestw}/{zwrot_niepelny})'),
    ], 'pieniadz', 'KingdomLedger.cs', agreg=_agreg_skarbce, bezwar=True, wielo=True),
    Temat('Zold', 'Zold', r'^dzien \d+', [
        K('rody_naliczony', '| partie rodow: naliczony ', wym=True),
        K('rody_zeszlo', ', z kies zeszlo ', sek='partie rodow: naliczony'),
        K('rody_partii', sek='partie rodow: naliczony', rx=r'z kies zeszlo -?\d+ \((\d+) partii\)',
          lit=[' partii) -> do sakiewek ludzi ']),
        K('do_sakiewek', ' -> do sakiewek ludzi ', wym=True),
        K('do_sakiewek_partii', rx=r'do sakiewek ludzi -?\d+ \((\d+) partii', lit=[' partii, w tym ludzie gracza ']),
        K('gracz', ', w tym ludzie gracza '),
        K('gar_naliczony', '| garnizony: naliczony ', wym=True),
        K('gar_zeszlo', ', z kies zeszlo ', sek='garnizony: naliczony'),
        K('gar_zalog', sek='garnizony: naliczony', rx=r'\((\d+) zalog\)', lit=[' zalog) -> do kas miast ']),
        K('do_miast', ' -> do kas miast '),
        K('do_zamkow', '), do kas zamkow '),
        K('nieumarli', 'nie przekazano: nieumarli '),
        K('bez_osady', ', zaloga bez osady '),
        K('wylaczone', ', wylaczone w ustawieniach '),
        K('inne_partie', '; karawany i inne partie (bez zmian) '),
        K('przyciete', 'saldo rodu nie zmiescilo sie w kiesie glowy: '),
        K('przyciete_rodow', rx=r'w kiesie glowy: -?\d+ w (\d+) rodach',
          lit=[' rodach (w tym brak zapisany przez gre jako dlug wobec korony: ']),
        K('dlug', 'jako dlug wobec korony: '),
        K('dlug_rodow', rx=r'dlug wobec korony: -?\d+ w (\d+) rodach',
          lit=[' rodach); rody z pusta kiesa i nieznanym saldem (nic nie przekazano): ']),
        K('slepe', 'nieznanym saldem (nic nie przekazano): '),
        K('slepe_zl', rx=r'\(nic nie przekazano\): \d+ \((-?\d+)\)', lit=['(nic nie przekazano): ']),
        K('sak_razem', '| sakiewki ludzi: razem ', wym=True),
        K('sak_partii', rx=r'sakiewki ludzi: razem -?\d+ w (\d+) partiach', lit=[' partiach, najwieksza ']),
        K('sak_najwieksza', ' partiach, najwieksza '),
        K('sak_sieroty', ', po partiach, ktorych juz nie ma: '),
        K('sak_sieroty_n', rx=r'ktorych juz nie ma: -?\d+ \((\d+)\)', lit=[', po partiach, ktorych juz nie ma: ']),
        K('znacznik', 'wlaczona, znacznik '),
        K('nie_skasowal', ' miastach, regulator nie skasowal dzis '),
        # ogniwo 111 (K6): zold zalog, ktory wplynal ponad zapas kupcow i wraca panu z kasy osady - bez zwrotu korony
        K('bez_zwrotu_korony', ' | zalogi bez zwrotu korony (czesc zoldu, ktora wplynela ponad zapas kupcow i wraca panu z kasy '
                               'osady: zawor miasta, danina podzamcza): '),
        K('potk_powtorzone', '| potkniecia: powtorzone wyplaty '),
        K('potk_wyjatki', ', wyjatki ', sek='potkniecia: powtorzone wyplaty'),
    ], _extra_zold, [
        ('partie rodow: naliczony/zeszlo z kies', '{rody_naliczony}/{rody_zeszlo}'),
        ('do sakiewek ludzi (w tym gracz)', '{do_sakiewek} ({gracz})'),
        ('zalogi: naliczony/zeszlo z kies', '{gar_naliczony}/{gar_zeszlo}'),
        ('do kas miast / do kas zamkow', '{do_miast}/{do_zamkow}'),
        ('przyciete (dlug korony) | rody slepe', '{przyciete}({dlug})|{slepe}'),
        ('sakiewki: razem / najwieksza', '{sak_razem}/{sak_najwieksza}'),
        ('sakiewki po zniklych partiach (ile)', '{sak_sieroty} ({sak_sieroty_n})'),
    ], 'pieniadz', 'SoldierPay.cs'),
    Temat('Sakiewka ludzi', 'Sakiewka ludzi', r'^dzien \d+ - nadwyzki', [
        K('sprzedane', ' - nadwyzki sprzedane ', wym=True),
        K('za', ' szt. za '),
        K('trzecia_lordow', ' (trzecia lordow AI '),
        K('na_sprzet', '), ludzie wydali na sprzet ', wym=True),
        K('na_zycie', ', na zycie w miastach '),
        K('zold', '; zold wplacony do sakiewek '),
        K('wyplat', rx=r'zold wplacony do sakiewek -?\d+ \((\d+) wyplat', lit=[' wyplat); ruch sakiewek: wplynelo ']),
        K('wplynelo', ' wyplat); ruch sakiewek: wplynelo '),
        K('wyszlo', ' (zold, lup, przejete sakiewki), wyszlo '),
        K('razem', ', w sakiewkach razem '),
    ], None, [
        ('nadwyzki sprzedane: szt/zl', '{sprzedane}/{za}'),
        ('wydane: na sprzet / na zycie', '{na_sprzet}/{na_zycie}'),
        ('zold wplacony | w sakiewkach razem', '{zold}|{razem}'),
        ('ruch sakiewek: wplynelo/wyszlo', '{wplynelo}/{wyszlo}'),
    ], 'pieniadz', 'MenPurse.cs'),
    Temat('Sakiewka ludzi (gracz)', 'Sakiewka ludzi', r'^gracz ', [], None, [], 'pieniadz', 'MenPurse.cs', dzienny=False),
    Temat('Paser', 'Paser', r'^dzien \d+', [
        # skup lupu: kazda banda z ladunkiem ma miec pasera (promien 200, cena maleje z odlegloscia)
        K('udzial', ' | skup lupu (udzial bandy ', 'float'),
        K('udzial_daleko', ' przy miescie -> ', 'float'),
        K('promien', ' na granicy promienia '),
        K('z_ladunkiem', '): band z ladunkiem '),
        K('z_paserem', ', z paserem '),
        K('poza_promieniem', ' (ma byc 100%; w tym poza promieniem '),
        K('sprzedalo', '), sprzedalo '),
        K('w_miastach', rx=r'\), sprzedalo -?\d+ w (\d+) miastach', lit=[' miastach - ']),
        K('szt', ' miastach - '),
        K('ruda', ' szt. (w tym ruda '),
        K('drewno', ', drewno ', sek='skup lupu ('),
        K('zywnosc', ' ladunkow, zywnosc '),
        K('zwierzeta', ', zwierzeta ponad potrzebe '),
        K('za_zl', ') za ', sek='skup lupu ('),
        K('po_cenach', ' zl; po cenach skupu miast '),
        K('dzialka', ' zl, dzialka pasera '),
        K('same_zwierzeta', '; same zwierzeta ponad potrzebe (bez innego ladunku) sprzedalo '),
        K('strefa1_szt', ' | wg odleglosci od miasta, ktore kupilo: do 50 - '),
        K('strefa1_zl', rx=r'do 50 - -?\d+ szt\. za (-?\d+) zl', lit=[' szt. za ']),
        K('strefa2_szt', ' zl, 50-100 - '),
        K('strefa2_zl', rx=r' zl, 50-100 - -?\d+ szt\. za (-?\d+) zl', lit=[' szt. za ']),
        K('strefa3_szt', ' zl, dalej - '),
        K('strefa3_zl', rx=r' zl, dalej - -?\d+ szt\. za (-?\d+) zl', lit=[' szt. za ']),
        K('zostalo', ' | z ladunkiem zostalo '),
        K('zostalo_szt', rx=r'z ladunkiem zostalo -?\d+ band \((-?\d+) szt', lit=[' band (']),
        K('zost_rezerwa', ' szt. razem z bandami poza handlem): kasy miast w zasiegu przy rezerwie '),
        K('zost_brak_miasta', ', brak otwartego miasta '),
        K('poza_bitwa', '; poza handlem z ladunkiem: w bitwie '),
        K('poza_bez_kiesy', ', bez kiesy '),
        K('poza_zadanie', ', zajete przez zadanie '),
        K('poza_nieumarli', ', nieumarli '),
        K('kupno_awansow', ' | kupno sprzetu u pasera: ', wym=True),
        K('kupno_zl', ' awansow za '),
        K('kupno_z_kryjowek', ' zl (w tym z kas kryjowek '),
        K('zycie_bandy', ' | zycie w miastach: bandy wydaly '),
        K('zycie_kryjowki', ' zl, kryjowki z nadwyzki '),
        K('wejsc', ' | kryjowki: wejsc band ', wym=True),
        K('zabl_bandom', rx=r'(?:zloto z niczego zablokowane - bandom|gra dopisala z niczego bandom) (-?\d+) zl',
          lit=[', zloto z niczego zablokowane - bandom ', ', KUREK OTWARTY w ustawieniach - gra dopisala z niczego bandom ']),
        K('zabl_kryjowkom', ' zl, kryjowkom ', sek='kryjowki: wejsc band'),
        K('kryj_doplyw', '; kasy kryjowek: doplyw '),
        K('kryj_odplyw', ' zl (bandy odlozyly z wlasnych kies), odplyw '),
        K('start_dane', ' zl (kiesy startowe nowych band '),
        K('start_chciane', rx=r'kiesy startowe nowych band -?\d+ z (-?\d+) zl', lit=[' z ']),
        K('kryj_sprzet', ' zl, na ktore liczyly; sprzet '),
        K('kryj_zycie', '; zycie w miastach '),
        K('kasy_kryjowek', '); stan '),
        K('kryjowek', rx=r'\); stan -?\d+ zl w (\d+) kryjowkach', lit=[' zl w ']),
        K('kryj_najwieksza', ' kryjowkach, najwieksza '),
        K('skarbce_do', ' zl, skarbce (to, co zostaje dla zdobywcy) razem do '),
        K('start_z_niczego', '; OBIEG KAS WYLACZONY - kiesy startowe z niczego: '),
        K('zywn_zabl', rx=r'zywnosc z niczego: zablokowana (-?\d+)', lit=[' | zywnosc z niczego: ', 'zablokowana ']),
        K('zywn_zdjete', ' nowym bandom, w bitwie zdjete '),
        K('gone_rozbitych', ' | kiesy band, ktore dzis zniknely z mapy: rozbitych '),
        K('gone_rozwiazanych', ' zl (reszta po dzialce zwyciezcy - przepada), rozwiazanych '),
        K('kiesy_band', ' | stan: kiesy band ', wym=True),
        K('juki_ladunek', ' zl; w jukach band: ladunek na sprzedaz '),
        K('juki_ladunek_zl', rx=r'ladunek na sprzedaz -?\d+ szt\. \((-?\d+)', lit=[' szt. (']),
        K('juki_zywnosc', ' zl), zywnosc poza skupem '),
        K('juki_zbroje', ' zl), zbroje na awanse '),
        K('juki_konie', ' zl), konie pod siodlo '),
        K('juki_juczne', ' zl), juczne '),
        K('juki_bron', ' zl), bron i inny sprzet - nie na sprzedaz '),
        K('juki_niehandlowe', ' zl), niehandlowe '),
        K('potk_skup', ' | potkniecia: skup '),
        K('potk_kryjowki', ', kryjowki ', sek='potkniecia: skup'),
        K('potk_zycie', ', zycie ', sek='potkniecia: skup'),
        K('potk_awanse', ', awanse ', sek='potkniecia: skup'),
    ], _extra_paser, [
        ('bandy: z ladunkiem/z paserem/sprzedalo', '{z_ladunkiem}/{z_paserem}/{sprzedalo}'),
        ('z ladunkiem zostalo band (szt.)', '{zostalo} ({zostalo_szt})'),
        ('skup: szt | zaplacono zl | dzialka', '{szt}|{za_zl}|{dzialka}'),
        ('skup szt wg drogi: do 50/50-100/dalej', '{strefa1_szt}/{strefa2_szt}/{strefa3_szt}'),
        ('zycie w miastach zl | awanse u pasera', '{zycie_razem}|{kupno_awansow}'),
        ('kryjowki: wejsc | kasy doplyw/odplyw', '{wejsc}|{kryj_doplyw}/{kryj_odplyw}'),
        ('kiesy band / kasy kryjowek (ile)', '{kiesy_band}/{kasy_kryjowek} ({kryjowek})'),
        ('juki band: ladunek/zywnosc poza skupem', '{juki_ladunek}/{juki_zywnosc}'),
    ], 'pieniadz', 'OutlawLaw.cs'),
    Temat('IronBank', 'IronBank', r'^dzien \d+ - nowe pozyczki', [
        K('nowe', ' - nowe pozyczki ', wym=True),
        K('nowe_suma', rx=r'nowe pozyczki \d+ \((-?\d+)\)', lit=['), splaty ']),
        K('splaty', '), splaty '),
        K('spoznienia', '), spoznienia '),
        K('bankructwa', ', bankructwa dzis '),
        K('dluznikow', '; dluznikow '),
        K('bankrutow', ' (bankrutow '),
        K('dlug', '), dlug razem '),
        K('kapital', ', kapital Banku ', wym=True),
    ], None, [('dluznikow | dlug razem | kapital Banku', '{dluznikow}|{dlug}|{kapital}')],
          'pieniadz', 'IronBank.cs'),
    Temat('IronBank (pozyczki)', 'IronBank', r'pozycza ', [], None, [], 'pieniadz', 'IronBank.cs', dzienny=False),
    # ------------------------------------------------------------------------------------------------ ludzie
    Temat('Ludzie', 'Ludzie', r'^dzien \d+ \| swiat', [
        K('ludnosc_mln', rx=r'swiat: ludnosc (-?\d+(?:[.,]\d+)?) mln', lit=[' | swiat: ', 'ludnosc ', ' mln']),
        K('ludnosc_zm', rx=r'swiat: ludnosc -?[\d.,]+ mln \(([+-]\d+) ludzi\)', lit=[' ludzi)']),
        K('wsie_mln', ' [PopulationLaw: wsie ', 'float'),
        K('miasta_mln', rx=r'\[PopulationLaw: wsie [\d.,]+, miasta ' + RX_F + r'\]', lit=[', miasta ']),
        K('mezczyzn_mln', '], mezczyzn 16-60 ok. ', 'float'),
        # ogniwo 113: uchodzcy zeszli z hearth wsi, ale zyja - ksiega pokazuje ich osobno i razem z ludnoscia w domu
        K('uchodzcy', '; uchodzcy poza domem '),
        K('razem_mln', ', razem z nimi ', 'float'),
        K('razem_zm', rx=r', razem z nimi -?[\d.,]+ mln \(([+-]\d+) ludzi\)', lit=[', razem z nimi ', ' ludzi)']),
        K('rody', sek=SEK_ZOLN, rx=r'partie rodow (-?\d+) \(', wym=True, lit=[' | zolnierze: ', 'partie rodow', ' partii']),
        K('rody_partii', sek=SEK_ZOLN, rx=r'partie rodow -?\d+ \((\d+) partii', lit=[' partii']),
        K('rody_zm', sek=SEK_ZOLN, rx=r'partie rodow -?\d+ \(\d+ partii; ([+-]\d+)\)', lit=[' partii']),
        K('garnizony', sek=SEK_ZOLN, rx=r', garnizony (-?\d+) \(', wym=True, lit=['garnizony']),
        K('garnizony_zm', sek=SEK_ZOLN, rx=r', garnizony -?\d+ \(\d+ partii; ([+-]\d+)\)', lit=['garnizony']),
        K('milicje', sek=SEK_ZOLN, rx=r', milicje (-?\d+) \(', lit=['milicje']),
        K('bandy', sek=SEK_ZOLN, rx=r', bandy (-?\d+) \(', lit=['bandy']),
        K('bandy_partii', sek=SEK_ZOLN, rx=r', bandy -?\d+ \((\d+) partii', lit=['bandy']),
        K('karawany', sek=SEK_ZOLN, rx=r', karawany (-?\d+) \(', lit=['karawany']),
        K('tabory_wsi', sek=SEK_ZOLN, rx=r', tabory wsi (-?\d+) \(', lit=['tabory wsi']),
        K('inne', sek=SEK_ZOLN, rx=r', inne (-?\d+) \(', lit=['inne']),
        K('jency_partie', '; jency w partiach '),
        K('jency_lochy', ', w lochach osad '),
        K('wyrzutki', ' | wyrzutki w puli '),
        K('zabici', ' | dzis: zabici ', wym=True),
        K('starc', sek=SEK_DZIS, rx=r'zabici \d+ w (\d+) starciach', lit=[' starciach (']),
        K('zabici_rody', sek=SEK_DZIS, rx=r'starciach \(partie rodow (\d+)', lit=[' starciach (']),
        K('zabici_bandy', sek=SEK_DZIS, rx=r'starciach \([^)]*?, bandy (\d+)', lit=[' starciach (']),
        K('ranni', '), ranni '),
        K('rozbici', ', rozbici '),
        K('zwerbowani', '; zwerbowani ', wym=True),
        K('zw_notable_rody', ' (od notabli do partii rodow '),
        K('zw_notable_inne', ', od notabli bez wodza - garnizony, karawany '),
        K('zw_karczma', ', z karczmy '),
        K('zw_bez_osady', ', bez osady - jency albo ochotnicy z mapy '),
        K('zw_gracz', ', gracz ', sek=SEK_DZIS),
        K('dezercja', '); dezercja '),
        K('dezercja_rody', sek=SEK_DZIS, rx=r'dezercja \d+ \(partie rodow (\d+)\)', lit=[' (partie rodow ']),
        K('bil_zmiana', ' | bilans partii rodow: zmiana ', 'sint'),
        K('bil_zwerb', ' = zwerbowani przez wodzow +'),
        K('bil_reszta', sek='bilans partii rodow: zmiana', rx=r' \+ reszta ([+-]?\d+)', lit=[' + reszta ']),
        K('pod_bronia_proc', ' | pod bronia (partie rodow + garnizony): ', 'float'),
        K('regionow_ponad', '% mezczyzn swiata; regionow ponad prog 20% mezczyzn: '),
        K('regionow', rx=r'prog 20% mezczyzn: \d+ z (\d+)', lit=[' (bez ludnosci w ksiedze ']),
        K('bez_ludnosci', ' (bez ludnosci w ksiedze '),
        # ogniwo 108 (PeopleUnit.DayNote): ile hearth wsie oddaly i odzyskaly dzis za ludzi; 1 czlowiek = 1/k hearth
        K('hz_tabory', 'tabory wsi i lodzie -', 'float', sek=SEK_HZ, zr='PeopleUnit.cs'),
        K('hz_tabory_ludzi', rx=r'tabory wsi i lodzie -[\d.,]+ za (-?\d+) ludzi', lit=[' za ', ' ludzi (uzupelnien '],
          zr='PeopleUnit.cs'),
        K('hz_uzupelnien', ' ludzi (uzupelnien ', zr='PeopleUnit.cs'),
        K('hz_nowych', ', nowych ', sek=SEK_HZ, zr='PeopleUnit.cs'),
        K('hz_gra', '; gra zdjela ', 'float', zr='PeopleUnit.cs'),
        K('hz_wyrzutki', ', wyrzutki w las -', 'float', zr='PeopleUnit.cs'),
        K('hz_wyrzutki_ludzi', rx=r', wyrzutki w las -[\d.,]+ za ' + RX_F + ' ludzi', lit=[' ludzi'], zr='PeopleUnit.cs'),
        K('hz_powrot', ', powrot z lasu +', 'float', zr='PeopleUnit.cs'),
        K('hz_powrot_ludzi', rx=r', powrot z lasu \+[\d.,]+ za ' + RX_F + ' ludzi', lit=[' ludzi'], zr='PeopleUnit.cs'),
        K('hz_pobor', ', pobor wymuszony gracza -', 'float', zr='PeopleUnit.cs'),
        K('hz_zadania', ', zadania gracza ', 'float', zr='PeopleUnit.cs'),
        K('hz_incydenty', ', incydenty gracza ', 'float', zr='PeopleUnit.cs'),
        # ogniwo 113: hearth zdjety za zabitych i uchodzcow oraz oddany za powroty (dopisek tylko w dobach z ruchem)
        K('hz_spust', ', spustoszenie -', 'float', sek=SEK_HZ, zr='PeopleUnit.cs'),
        K('hz_spust_ludzi', rx=r', spustoszenie -[\d.,]+ za ' + RX_F + ' zabitych i uchodzcow', sek=SEK_HZ,
          lit=[', spustoszenie -', ' za ', ' zabitych i uchodzcow'], zr='PeopleUnit.cs'),
        K('hz_dom', ', powrot uchodzcow +', 'float', sek=SEK_HZ, zr='PeopleUnit.cs'),
        K('hz_dom_ludzi', rx=r', powrot uchodzcow \+[\d.,]+ za ' + RX_F + ' ludzi', sek=SEK_HZ,
          lit=[', powrot uchodzcow +', ' za ', ' ludzi'], zr='PeopleUnit.cs'),
        K('hz_niezgodne', '; niezgodne z formula gry i zostawione: ', zr='PeopleUnit.cs'),
        K('hz_bez_k', '; bez przelicznika kultury: ', zr='PeopleUnit.cs'),
        K('hz_potk', '; potkniecia: ', sek=SEK_HZ, zr='PeopleUnit.cs'),
        # ogniwo 113 (Devastation.DayNote): spustoszenie doby w ludziach i stan konta uchodzcow
        K('sp_zdjeci', ' | spustoszenie dzis: zdjeci z wsi ', 'float', zr='Devastation.cs'),
        K('sp_rabunki', ' ludzi (rabunki ', 'float', sek=SEK_SP, zr='Devastation.cs'),
        K('sp_rab_wsi', rx=r' ludzi \(rabunki -?[\d.,]+ w (\d+) wsiach', sek=SEK_SP, lit=[' wsiach, zerowanie armii '],
          zr='Devastation.cs'),
        K('sp_zerowanie', ' wsiach, zerowanie armii ', 'float', sek=SEK_SP, zr='Devastation.cs'),
        K('sp_zer_wsi', rx=r'zerowanie armii -?[\d.,]+ w (\d+) wsiach\)', sek=SEK_SP, lit=[' wsiach) = zabici '],
          zr='Devastation.cs'),
        K('sp_zabici', ' wsiach) = zabici ', 'float', sek=SEK_SP, zr='Devastation.cs'),
        K('sp_w_las', ' + w las do puli wyrzutkow ', 'float', sek=SEK_SP, zr='Devastation.cs'),
        K('sp_uchodzcy', ' + uchodzcy ', 'float', sek=SEK_SP, zr='Devastation.cs'),
        K('sp_wrocilo', '; wrocilo do domu ', 'float', sek=SEK_SP, zr='Devastation.cs'),
        K('sp_wrocilo_wsi', rx=r'; wrocilo do domu -?[\d.,]+ z (\d+) wsi', sek=SEK_SP, lit=[' z ', ' wsi'],
          zr='Devastation.cs'),
        K('sp_w_drodze', '; uchodzcy w drodze (poza domem) ', sek=SEK_SP, zr='Devastation.cs'),
        K('sp_w_drodze_wsi', rx=r'\(poza domem\) -?\d+ z (\d+) wsi', sek=SEK_SP, lit=[' z ', ' wsi'], zr='Devastation.cs'),
        K('sp_zabici_razem', ' wsi, zabici przy spustoszeniu od poczatku ', sek=SEK_SP, zr='Devastation.cs'),
        K('sp_potk', '; potkniecia: ', sek=SEK_SP, zr='Devastation.cs'),
        K('sp_na_koncie', '; na koncie jeszcze ', zr='Devastation.cs'),
        K('glodne', ' | warownie glodne '),
        K('ujemny_bilans', ', z ujemnym bilansem zywnosci '),
        K('potkniecia', ' Potkniecia ksiegi: '),
    ], _extra_ludzie, [
        ('ludnosc mln (zmiana dobowa)', '{ludnosc_mln} ({ludnosc_zm:+})'),
        ('zolnierze: partie rodow/garnizony', '{rody}/{garnizony}'),
        ('zolnierze: bandy/milicje', '{bandy}/{milicje}'),
        ('dzis: zabici/zwerbowani/dezercja', '{zabici}/{zwerbowani}/{dezercja}'),
        ('pod bronia % | regionow ponad 20%', '{pod_bronia_proc} | {regionow_ponad}'),
        ('hearth za ludzi: tabory/w las/z lasu', '{hz_tabory}/{hz_wyrzutki}/{hz_powrot}'),
        ('tabory: ludzi | gra zdjelaby hearth', '{hz_tabory_ludzi}|{hz_gra}'),
        ('spustoszenie: zdjeci dzis | uchodzcy', '{sp_zdjeci}|{sp_w_drodze}'),
    ], 'ludzie', 'PeopleLedger.cs', bezwar=True),
    # ogniwo 109 (PopulationLaw.GrowthDaily): linia "Ludzie: przyrost naturalny ..." zaraz po linii "Ludzie: dzien N"
    Temat('Ludzie (przyrost)', 'Ludzie', r'^przyrost naturalny', [
        K('ludzi', rx=r'^Ludzie: przyrost naturalny ([+-]?\d+) ludzi \(g srednio', lit=['Ludzie: przyrost naturalny ']),
        K('g_srednio', ' ludzi (g srednio ', 'float'),
        K('hearth', rx=r'; ' + RX_F + r' hearth na \d+ wsi, w rok', lit=[' hearth na ']),
        K('wsi', rx=r' hearth na (\d+) wsi, w rok', lit=[' wsi, w rok ok. ']),
        K('w_rok', ' wsi, w rok ok. ', 'sint'),
        K('uch_ludzi', '; osobno powrot uchodzcow ', 'sint'),
        K('uch_hearth', rx=r'osobno powrot uchodzcow [+-]?\d+ ludzi \(' + RX_F + ' hearth w', lit=[' ludzi (', ' hearth w ']),
        K('uch_wsi', rx=r' hearth w (\d+) wsiach przy dnie', lit=[' wsiach przy dnie - regula ScorchedEarth do kroku 4']),
        K('rosnie', ' | wsie: rosnie '),
        K('kurczy', ', kurczy sie '),
        K('bez_zmiany', ', bez zmiany '),
        K('spalone', ' (spalone, lupione albo pod przymusem '),
        K('g_min', '; g od ', 'float'),
        K('g_med', rx=r'; g od [+-]?[\d.,]+ przez ' + RX_F + r' \(mediana\)', lit=[' przez ', ' (mediana) do ']),
        K('g_max', ' (mediana) do ', 'float'),
        K('sk_podstawa', ' | skladowe g wsi niespalonych (pkt % rocznie, wazone ludnoscia): podstawa ', 'float'),
        K('sk_dobrobyt', ', dobrobyt wobec mediany krainy ', 'float'),
        K('sk_niebezp', ', niebezpieczenstwo ', 'float'),
        K('wsi_wojna', ' (wsi krain w wojnie '),
        K('wsi_spal_reg', ', wsi w regionach ze spalonymi '),
        K('bezp', ', bezpieczenstwo warowni srednio ', 'float'),
        K('sk_glod', rx=r', glod ' + RX_F + r' \(zima tnie plon', lit=[', glod ', ' (zima tnie plon ']),
        K('glod_wsi', rx=r'zima tnie plon (\d+) wsi', lit=[', glod WYLACZONY (na sucho: zima tnie plon ']),
        K('glod_na_sucho', rx=r'; z waga [\d.,]+ byloby ' + RX_F, lit=['; z waga ', ' byloby ']),
        K('premie', ', premie gry srednio x', 'float'),
        K('wsi_premia', ' (wsi z premia '),
        K('pulap_obcial', rx=r'\), pulap \+[\d.,]+ obcial (\d+) wsi', lit=['), pulap +', ' obcial ']),
        K('dno_obcielo', rx=r' wsi, dno -[\d.,]+ obcielo (\d+) wsi', lit=[' wsi, dno -', ' obcielo ']),
        K('uch_regula_wsi', ' wsi, powrot uchodzcow +0.5 hearth: '),
        K('hearth_swiata', ' | hearth wsi swiata ', 'float'),
        K('od_wczoraj', ', od wczoraj ', 'float'),
        K('rozl_przyrost', rx=r' = (?:przyrost naturalny|wynik gry) ' + RX_F, sek=SEK_ROZL,
          lit=[' = ', 'przyrost naturalny ', 'wynik gry ']),
        K('rozl_uchodzcy', ', powrot uchodzcow ', 'float', sek=SEK_ROZL),
        # od ogniwa 113 przy czynnym spustoszeniu: "(tabory, ..., incydenty, spustoszenie i powroty uchodzcow)"
        K('rozl_ruch', rx=r', ruch ludzi \(tabory, wyrzutki, pobor, zadania, incydenty(?:, spustoszenie i powroty uchodzcow)?\) '
          + RX_F, lit=[', ruch ludzi (tabory, wyrzutki, pobor, zadania, incydenty', ') ']),
        K('reszta', ', RESZTA ', 'float'),
        K('prog_0', rx=r'od 600 hearth\): (\d+) / ',
          lit=[' | wsie wedle progow produkcji (ponizej 200 / 200-599 / od 600 hearth): ', ' / ']),
        K('prog_200', rx=r'od 600 hearth\): \d+ / (\d+) / ', lit=[' / ']),
        K('prog_600', rx=r'od 600 hearth\): \d+ / \d+ / (\d+)', lit=[' / ']),
        K('f32_model', ' | float32: model ', 'float'),
        K('f32_wsie', ' hearth, wsie przyjmuja ', 'float'),
        K('f32_male', 'wsi z krokiem ponizej polowy kroku floata: '),
        K('miasta_mln', ' | miasta: ', 'float'),
        K('miasta_n', rx=r' mln w (\d+) miastach', lit=[' mln w ', ' miastach - stan (zamrozony); z dzisiejszego dobrobytu byloby ']),
        K('miasta_live', ' miastach - stan (zamrozony); z dzisiejszego dobrobytu byloby ', 'float'),
        K('miasta_dryf', rx=r' mln \(([+-]?\d+) ludzi dryfu', lit=[' mln (', ' ludzi dryfu od zalozenia stanu)']),
        K('bez_k', ' | wsi bez przelicznika ludzi: '),
        K('potk_rachunek', ' | potkniecia: rachunek wsi '),
        K('potk_linia', ', linia dobowa '),
        # przyrost WYLACZONY: hearth wsi liczy gra
        K('gra_ludzi', rx=r'hearth wsi liczy gra: ([+-]?\d+) ludzi dzis', lit=[') - hearth wsi liczy gra: ', ' ludzi dzis (']),
        K('gra_proc', rx=r' wsi = ' + RX_F + r'% rocznie\)', lit=[' wsi = ', '% rocznie)']),
    ], _extra_przyrost, [
        ('przyrost naturalny ludzi (g % rocznie)', '{ludzi:+} ({g_srednio})'),
        ('wsie: rosnie/kurczy sie/bez zmiany', '{rosnie}/{kurczy}/{bez_zmiany}'),
        ('hearth wsi: zmiana dobowa | RESZTA', '{od_wczoraj:+}|{reszta:+}'),
        ('uchodzcy ludzi | miasta mln (dryf)', '{uch_ludzi}|{miasta_mln} ({miasta_dryf:+})'),
    ], 'ludzie', 'PopulationLaw.cs'),
    Temat('Ludzie (plik)', 'Ludzie', r'^plik wszystkich regionow', [], None, [], 'ludzie', 'PeopleLedger.cs', dzienny=False),
    Temat('Ludzie (pominiete)', 'Ludzie', r'^dzien \d+ - pominiete wiersze', [
        K('pominiete', '(wyjatek przy regionie): ', wym=True),
    ], None, [], 'ludzie', 'PeopleLedger.cs', dzienny=False),
    # ogniwo 113 (Devastation.RegionsNote): pomiar doby - rabunki i zerowanie w osobodniach i ludziach, powroty, regiony.
    # Linia jest pisana tylko w dobach, w ktorych cos sie dzialo albo ktos jest poza domem.
    Temat('Ludzie (spustoszenie)', 'Ludzie (spustoszenie)', r'^dzien \d+', [
        K('rabunki', ' | rabunki zakonczone ', wym=True),
        K('spalone', ' (wies spalona '),
        K('przerwane', ', przerwane '),
        K('napastnikow', ', napastnikow srednio ', 'float'),
        K('osobodni_rab', '; osobodni lupienia ', 'float', wym=True),
        K('r_rab', rx=r'; osobodni lupienia -?[\d.,]+ x ' + RX_F + ' -> ', lit=[' x ', ' -> ']),
        K('ludzi_rab', rx=r'; osobodni lupienia -?[\d.,]+ x -?[\d.,]+ -> ' + RX_F + ' ludzi; gra zdjelaby',
          lit=[' -> ', ' ludzi; gra zdjelaby ']),
        K('gra_hearth', ' ludzi; gra zdjelaby ', 'float'),
        K('gra_ludzi', rx=r'gra zdjelaby -?[\d.,]+ hearth = (-?\d+) ludzi ksiegi', lit=[' hearth = ', ' ludzi ksiegi']),
        K('podjete', ' (rabunki podjete po wczytaniu gry: '),
        K('zer_partii', ' | zerowanie: partii '),
        K('osobodni_zer', rx=r'\| zerowanie: partii -?\d+, osobodni ' + RX_F, lit=[', osobodni ']),
        K('r_zer', rx=r'\| zerowanie: partii -?\d+, osobodni -?[\d.,]+ x ' + RX_F + ' -> ', lit=[' x ', ' -> ']),
        K('ludzi_zer', rx=r'\| zerowanie: partii -?\d+, osobodni -?[\d.,]+ x -?[\d.,]+ -> ' + RX_F + ' ludzi',
          lit=[' -> ', ' ludzi']),
        K('nasycony', rx=r'albo dno hearth\): (\d+) razy', lit=['% albo dno hearth): ', ' razy']),
        K('powrot_ludzi', ' | powrot: ', 'float'),
        K('powrot_wsi', rx=r'\| powrot: -?[\d.,]+ ludzi do (\d+) wsi', lit=[' ludzi do ', ' wsi']),
        K('tempo', ', tempo srednio ', 'float'),
        K('wstrz_stan', '; wstrzymany: wies spalona, lupiona albo pod przymusem '),
        K('wstrz_oblezenie', ', warownia oblezona '),
        K('wstrz_inne', ', niebezpieczenstwo pelne albo tempo 0: '),
        K('uchodzcy', ' | uchodzcy poza domem ', wym=True),
        K('regionow', rx=r'\| uchodzcy poza domem -?\d+ w (\d+) regionach', lit=[' w ', ' regionach']),
        K('min_plon', '; najnizszy mnoznik plonu x', 'float'),
        K('bez_k', ' | bez przelicznika ludzi: '),
        K('obcy_hearth', ' | hearth dopisany przez kogos w srodku kroku rabunku (zachowany): '),
    ], _extra_spustoszenie, [
        ('rabunki (przerw.) | osobodni -> ludzi', '{rabunki}({przerwane})|{osobodni_rab}->{ludzi_rab}'),
        ('gra zdjelaby ludzi | zerowanie os.dni', '{gra_ludzi}|{osobodni_zer}->{ludzi_zer}'),
        ('uchodzcy poza domem|wrocilo|min plon', '{uchodzcy}|{powrot_ludzi}|{min_plon}'),
    ], 'ludzie', 'Devastation.cs'),
    Temat('Ludzie (regiony)', 'Ludzie (regiony)', r'^dzien \d+', [
        K('pokazanych', rx=r': dzien \d+ - (\d+) najbardziej', lit=[' najbardziej obciazonych z ']),
        K('regionow', ' najbardziej obciazonych z ', wym=True),
    ], _extra_regiony, [
        ('najbardziej obciazony region, %', '{top1_kr} {top1_proc}'),
    ], 'ludzie', 'PeopleLedger.cs'),
    Temat('Ludnosc', 'Ludnosc', r'^dzien \d+', [
        K('mln', rx=r'\| (-?\d+(?:[.,]\d+)?) mln \|', wym=True, lit=[' mln | renty zaplacone ']),
        K('zaplacone', ' mln | renty zaplacone ', wym=True),
        K('nalezne', ' z naleznych '),
    ], _extra_ludnosc, [('ludnosc mln | % rent zaplaconych', '{mln} | {zaplacone_proc}'),
                       ('renty zaplacone/nalezne', '{zaplacone}/{nalezne}')],
          'ludzie', 'PopulationLaw.cs'),
    Temat('Wyrzutki', 'Wyrzutki', r'^dzien \d+', [
        K('pula', ' | pula ', wym=True),
        K('zolnierzy', ' ludzi (zolnierzy '),
        K('prostych', ', prostych '),
        K('dezercja', ' | naplyw: dezercja '),
        K('rozbitkowie', ', rozbitkowie '),
        K('rabunki', ', rabunki '),
        K('bieda', ', bieda/wojna ', 'float'),
        K('rozwiazane', ', rozwiazane bandy '),
        K('powrot', ' | powrot do wsi ', 'float'),
        K('bandy_nowe', ' | bandy nowe '),
        K('nowe_ludzi', rx=r'bandy nowe \d+ \((\d+) ludzi', lit=[' ludzi), odmowione ']),
        K('odmowione', ' ludzi), odmowione '),
        K('puste', ', puste usuniete '),
        K('werb_pula', ' | werbunek: z puli '),
        K('werb_jency', ', jency ', sek='werbunek: z puli'),
        K('aw_lup', ' | awanse: z lupu '),
        K('aw_paser', ', od pasera '),
        K('aw_paser_zl', rx=r', od pasera \d+ \((-?\d+) zl', lit=[' zl), bez sprzetu ']),
        K('aw_bez', ' zl), bez sprzetu '),
        K('band', ' | band ', wym=True),
        K('ludzi', rx=r'\| band \d+, ludzi (\d+)', lit=[', ludzi ']),
    ], None, [
        ('pula | band/ludzi w bandach', '{pula} | {band}/{ludzi}'),
        ('awanse: z lupu/od pasera/bez sprzetu', '{aw_lup}/{aw_paser}/{aw_bez}'),
    ], 'ludzie', 'OutlawLaw.cs', bezwar=True),
    Temat('Wyrzutki (start)', 'Wyrzutki', r'^(pula poczatkowa|wczytano pule)', [], None, [], 'ludzie', 'OutlawLaw.cs',
          dzienny=False),
    Temat('Bitwy', 'Bitwy', r'^dzien \d+', [
        K('starc', rx=r': dzien \d+ - (\d+) starc', wym=True, lit=[' starc (w tym pogromow/nierownych ']),
        K('pogromow', ' starc (w tym pogromow/nierownych '),
        K('malych', ', malych potyczek pominietych '),
        K('zabitych', '), zabitych ', wym=True),
        K('rannych', ', rannych '),
        K('prawdziwych', rx=r'; w (\d+) prawdziwych bitwach', lit=[' prawdziwych bitwach zwyciezcy tracili zabitych srednio ']),
        K('zwyciezcy_proc', ' prawdziwych bitwach zwyciezcy tracili zabitych srednio ', 'float'),
        K('przegrani_proc', ', przegrani ', 'float'),
    ], None, [('starc (bitew) | zabitych/rannych', '{starc}({prawdziwych})|{zabitych}/{rannych}')],
          'ludzie', 'BattleChronicle.cs'),
    # ------------------------------------------------------------------------------------------------ dodatkowe
    Temat('ZakupyAI', 'ZakupyAI', r'^dzien \d+', [
        K('wizyt', rx=r': dzien \d+ - (\d+) wizyt', wym=True, lit=[' wizyt, ']),
        K('szt', ' wizyt, '),
        K('zlota', ' szt. kupionych za '),
        K('garnizony', ' zlota; w tym garnizony '),
        K('garnizony_zl', ' zakupow za '),
    ], None, [('zlota razem / w tym garnizony', '{zlota}/{garnizony_zl}')], 'dodatkowe', 'AiGear.cs'),
    Temat('Zuzycie AI', 'Zuzycie AI', r'^dzien \d+', [
        K('lup_obity', ' - lup obity '),
        K('zuzyte', ' szt., zuzyte w bitwach ', wym=True),
        K('naprawione', ', naprawione w miastach '),
        K('za', rx=r'naprawione w miastach \d+ za (-?\d+)', lit=[' z sakiewek ludzi; obitych razem ']),
        K('obitych', ' z sakiewek ludzi; obitych razem '),
        K('partii', rx=r' szt\. w (\d+) partiach', lit=[' szt. w ', ' partiach.']),
    ], None, [('zuzyte w bitwach/naprawione | obitych', '{zuzyte}/{naprawione}|{obitych}')],
          'dodatkowe', 'AiWear.cs'),
    Temat('HouseLevies', 'HouseLevies', r'^dzien \d+', [
        K('ochotnikow', rx=r': dzien \d+ - (\d+) szlacheckich', wym=True,
          lit=[' szlacheckich ochotnikow na ziemiach rodow to teraz ludzie rodu.']),
    ], None, [('szlacheccy ochotnicy do rodow', '{ochotnikow}')], 'dodatkowe', 'HouseLevies.cs'),
    Temat('Werbunek', 'Werbunek', r'^dzien \d+', [
        K('notablom', ' - zloto AI za ochotnikow do notabli ', wym=True),
        K('miastom', ', za najemnikow do miast '),
        K('bez_kompletu', '; nowe partie AI bez darmowego kompletu DTE: '),
    ], None, [('zloto AI: notablom/miastom', '{notablom}/{miastom}')],
          'dodatkowe', 'LevyGold.cs'),
    Temat('Pobor', 'Pobor', r'^dzien \d+', [
        K('chec', ' - chec do sluzby srednio x', 'float', wym=True),
        K('losowan', ' szansy BK ('),
    ], None, [('chec do sluzby x | losowan', '{chec} | {losowan}')], 'dodatkowe', 'Levy.cs'),
    Temat('Ochotnicy', 'Ochotnicy', r'^dzien \d+', [
        K('awanse', ' - awanse z kupionym sprzetem ', wym=True),
        K('szt', rx=r'z kupionym sprzetem \d+ \((\d+) szt', lit=[' szt. za ']),
        K('zl', rx=r' szt\. za (-?\d+) zl z kiesy', lit=[' zl z kiesy notabli do miast), cofniete (brak towaru albo zlota) ']),
        K('cofniete', ' zl z kiesy notabli do miast), cofniete (brak towaru albo zlota) '),
        K('dodatkow', '; dodatkow nie dokupiono '),
    ], None, [('awanse z kupionym sprzetem / cofniete', '{awanse}/{cofniete}')], 'dodatkowe', 'VolunteerKit.cs'),
    Temat('Ochotnicy (diagnoza)', 'Ochotnicy (diagnoza)', None, [], None, [], 'dodatkowe', 'VolunteerKit.cs', dzienny=False),
    Temat('Komplet rekruta', 'Komplet rekruta', r'^dzien \d+', [
        K('z_kompletem', ' - werbunek AI z kompletem od notabla ', wym=True),
        K('bez_zapisu', ', bez zapisu (wzorzec) '),
        K('tier1', ', tier 1 z wlasnym dobytkiem '),
        K('sprzedane', '; rzeczy ochotnikow, ktorzy odeszli, sprzedane '),
    ], None, [('z kompletem/bez zapisu/tier 1', '{z_kompletem}/{bez_zapisu}/{tier1}')], 'dodatkowe', 'RecruitKit.cs'),
    Temat('Budowy oplacone', 'Budowy oplacone', r'^dzien \d+', [
        K('osad', ' - osad ', wym=True),
        K('wydano', ', wydano '),
        K('place', ' (place i wozy do kas osad '),
        K('materialy', ', materialy z targow '),
        K('punktow', '), punktow budowy '),
        K('brak_materialow', '; wstrzymane: brak materialow '),
        K('wojna', ', wojna (budowle cywilne) '),
    ], None, [('osad|wydano|stoi: materialy/wojna', '{osad}|{wydano}|{brak_materialow}/{wojna}')],
          'dodatkowe', 'BuildFunding.cs'),
    Temat('Budowy', 'Budowy', r'^dzien \d+', [
        K('osad', ' - osad ', wym=True),
        K('w_budowie', ': w budowie '),
        K('idzie', ' (idzie '),
        K('stoi', ', stoi '),
        K('ukonczonych', ', ukonczonych poziomow '),
        K('bez_budowy', '), bez budowy '),
        K('moc', '; srednia moc ', 'float'),
    ], None, [('idzie/stoi | srednia moc pkt', '{idzie}/{stoi} | {moc}')], 'dodatkowe', 'BuildDiary.cs'),
    Temat('PodazPopyt', 'PodazPopyt', r'^kupcy wywiezli', [
        K('wywiezli', 'kupcy wywiezli ', wym=True),
        K('transakcji', ' szt. nadwyzki uzbrojenia w '),
        K('zaplacone', ' transakcjach miedzy osadami (zaplacone '),
        K('bez_odbiorcy', ' zlota); '),
    ], None, [('kupcy wywiezli szt | bez odbiorcy', '{wywiezli} | {bez_odbiorcy}')],
          'dodatkowe', 'SupplyDemand.cs'),
    Temat('WPLYW', 'WPLYW', r'^stan ', [
        K('stan', 'WPLYW: stan ', 'float', wym=True),
        K('zmiana', ', dzienna zmiana ', 'float'),
    ], None, [('wplywy gracza: stan (zmiana)', '{stan} ({zmiana:+})')], 'dodatkowe', 'InfluenceWatch.cs'),
    Temat('UniqueLaw (dzien)', 'UniqueLaw (dzien)', None, [
        K('gracz', ' - u gracza '),
        K('ai', ' szt. unikatow zamienionych na zamienniki, u AI ', wym=True),
        K('znikly', ', bez zamiennika (znikly) '),
    ], None, [('unikaty zamienione: gracz/AI | znikly', '{gracz}/{ai} | {znikly}')], 'dodatkowe', 'UniqueLaw.cs'),
    Temat('LegendaryLaw (targi)', 'LegendaryLaw', r'^targi \(dzien\)', [
        K('zdjete', rx=r'targi \([^)]*\) - (\d+) egzotycznych', wym=True,
          lit=['LegendaryLaw: targi (', ' egzotycznych wierzchowcow zdjetych z polek nie u swoich.']),
    ], None, [('egzotyczne wierzchowce zdjete z targow', '{zdjete}')], 'dodatkowe', 'LegendaryLaw.cs'),
    Temat('LegendaryLaw (inne)', 'LegendaryLaw', None, [], None, [], 'dodatkowe', 'LegendaryLaw.cs', dzienny=False),
    Temat('Audyt predkosci', 'Audyt predkosci', None, [
        K('wynik', ' => ', 'float', wym=True),
    ], None, [('predkosc partii gracza', '{wynik}')], 'dodatkowe', 'TerrainEase.cs'),
    Temat('ArmsPricing (wojny)', 'ArmsPricing', r'^wojna ', [
        K('premia', ' - kupcy oczekuja zakupow broni: premia popytu +', wym=True),
        K('razem', '), razem '),
    ], None, [('premie wojenne: ile | najwyzsza %', '{premii} | {maks_proc}')], 'dodatkowe', 'ArmsPricing.cs',
          agreg=_agreg_wojny, wielo=True),
    Temat('ArmsPricing (start)', 'ArmsPricing', None, [], None, [], 'dodatkowe', 'ArmsPricing.cs', dzienny=False),
    Temat('HistoricalPrices', 'HistoricalPrices', None, [], None, [], 'dodatkowe', 'HistoricalPrices.cs', dzienny=False),
    # linie jednorazowe i zdarzenia znanych modulow (czytane w naglowku i alarmach; tu tylko po to, zeby nie byly "obce")
    Temat('SoldierPay', 'SoldierPay', None, [], None, [], 'pieniadz', 'SoldierPay.cs', dzienny=False),
    Temat('MoneyLedger', 'MoneyLedger', None, [], None, [], 'pieniadz', 'MoneyLedger.cs', dzienny=False),
    Temat('KingdomTreasury', 'KingdomTreasury', None, [], None, [], 'pieniadz', 'KingdomTreasury.cs', dzienny=False),
    Temat('CaravanBulk', 'CaravanBulk', None, [], None, [], 'surowce', 'CaravanBulk.cs', dzienny=False),
    Temat('MineralOnce', 'MineralOnce', None, [], None, [], 'surowce', 'MineralOnce.cs', dzienny=False),
    Temat('StartStock', 'StartStock', None, [], None, [], 'surowce', 'StartStock.cs', dzienny=False),
    Temat('MaterialLaw', 'MaterialLaw', None, [], None, [], 'surowce', 'MaterialLaw.cs', dzienny=False),
    Temat('OutlawLaw', 'OutlawLaw', None, [], None, [], 'ludzie', 'OutlawLaw.cs', dzienny=False),
    Temat('PopulationLaw', 'PopulationLaw', None, [], None, [], 'ludzie', 'PopulationLaw.cs', dzienny=False),
    Temat('PeopleUnit', 'PeopleUnit', None, [], None, [], 'ludzie', 'PeopleUnit.cs', dzienny=False),
    # ogniwa 110-113: linie startowe i jednorazowe (przyciecie daru startowego kas, wylaczenie spustoszenia)
    Temat('CastlePurse', 'CastlePurse', None, [], None, [], 'pieniadz', 'CastlePurse.cs', dzienny=False),
    Temat('TownPurse', 'TownPurse', None, [], None, [], 'pieniadz', 'TownPurse.cs', dzienny=False),
    Temat('VillageTakings', 'VillageTakings', None, [], None, [], 'pieniadz', 'VillageTakings.cs', dzienny=False),
    Temat('Devastation', 'Devastation', None, [], None, [], 'ludzie', 'Devastation.cs', dzienny=False),
    Temat('Spustoszenie', 'Spustoszenie', None, [], None, [], 'ludzie', 'Devastation.cs', dzienny=False),
    Temat('WarLedger', 'WarLedger', None, [], None, [], 'ludzie', 'WarLedger.cs', dzienny=False),
    # ogniwa 115, 116 i 119: linie startowe i jednorazowe (nowa kampania, latka modelu finansow rodu zakladana w kampanii)
    Temat('RawPrice', 'RawPrice', None, [], None, [], 'surowce', 'RawPrice.cs', dzienny=False),
    Temat('WorkshopTrade', 'WorkshopTrade', None, [], None, [], 'surowce', 'WorkshopTrade.cs', dzienny=False),
    Temat('MarketCarts', 'MarketCarts', None, [], None, [], 'surowce', 'MarketCarts.cs', dzienny=False),
    Temat('Klimat', 'Klimat', None, [], None, [], 'dodatkowe', 'WesterosClimate.cs', dzienny=False),
]

GRUPY = OrderedDict([('surowce', 'SUROWCE I DOWOZ'), ('pieniadz', 'PIENIADZ'), ('ludzie', 'LUDZIE'),
                     ('dodatkowe', 'DODATKOWE')])
# nazwy tematow w lewej kolumnie skrotu (pelne nazwy obowiazuja w --temat)
NAZWY_KROTKIE = {
    'Pieniadz swiata (bilans)': 'Pieniadz (bilans)',
    'Pieniadz swiata (rody)': 'Pieniadz (rody)',
    'Przeplywy osad (kasy miast)': 'Przepl.: kasy miast',
    'Przeplywy osad (kasy zamkow)': 'Przepl.: kasy zamkow',
    'Przeplywy osad (kiesy wsi)': 'Przepl.: kiesy wsi',
    'Ludzie (spustoszenie)': 'Ludzie (spustosz.)',
    'Korona (powinnosci)': 'Korona: powinnosci',
    'Korona (danina i clo)': 'Korona: danina, clo',
    'Korona (zwrot zoldu)': 'Korona: zwrot zoldu',
}

TEMATY_WG_PREFIKSU = defaultdict(list)
TEMATY_WG_NAZWY = OrderedDict()
for _t in TEMATY:
    TEMATY_WG_PREFIKSU[_t.prefiks].append(_t)
    TEMATY_WG_NAZWY[_t.nazwa] = _t

# Ogniwa lancucha paczek W KOLEJNOSCI LANCUCHA (87c8e96 = wpis 102 -> n102b-ksiega -> ... -> n107 (w grze) -> n115 ... n119 ->
# m108 ... m113; kolejnosc decyduje o "ogniwie w grze" i o alarmie "lancuch niespojny"). Pola:
#   nr, opis, wzorzec linii startowej, tematy dzienne, czy linia startowa jest zawsze (104: tylko przy nowej kampanii),
#   znak = (temat, kolumna): ogniwo widac takze po kolumnie cudzej linii dziennej (108: odcinek "hearth za ludzi dzis"),
#   poznac = po czym poznac ogniwo w logu (do tabeli w naglowku), paczka = plik opisu w docs\paczki albo wpis CHANGELOG
OGNIWA = [
    ('100', 'dowoz na targ', r'^MarketRoad: wsie zamkowe woza plon na targ miasta', ['Dowoz'], True, None,
     'start "MarketRoad: wsie zamkowe woza plon na targ miasta"; co dobe "Dowoz:" i "Dowoz (skutki):"', 'CHANGELOG wpis 100'),
    ('101', 'woz x2', r'^MarketRoad: woz - udzwig taborow', [], True, None,
     'start "MarketRoad: woz - udzwig taborow wsi zamkowych ... x2.0"', 'CHANGELOG wpis 101'),
    ('102', 'ksiegi (log)', r'^MoneyLedger: ', ['Pieniadz swiata', 'Ludzie'], True, None,
     'start "MoneyLedger: ... liczniki wpiete"; co dobe "Pieniadz swiata:", "Przeplywy osad:", "Ludzie:"', 'CHANGELOG wpis 102'),
    ('102b', 'ksiega: rody', r'^MoneyLedger: .*rozliczenie rodow', ['Pieniadz swiata (rody)'], True, None,
     'start "MoneyLedger: ... zold, rozliczenie rodow"; co dobe "Pieniadz swiata (rody):", "W tym zold naliczony"',
     '102b-ksiega-pieniadza.md'),
    ('103', 'karawany wg zysku', r'^CaravanBulk: ', ['Karawany', 'Karawany (stan)'], True, None,
     'start "CaravanBulk: ... latki wpiete"; co dobe "Karawany:" z "kolejnosc kupna wedle zysku" i "cena zbytu"',
     '103-karawany.md'),
    ('104', 'zapas startowy', r'^StartStock: ', [], False, None,
     '4 linie "StartStock:" przy starcie NOWEJ kampanii (wczytany zapis: jedna linia "BEZ przeliczenia" albo zadnej)',
     '104-zapas-startowy.md'),
    ('105', 'mineral BK raz', r'^MineralOnce: ', ['Mineraly (dubel BK)'], True, None,
     'start "MineralOnce: ... latka wpieta"; co dobe "Mineraly (dubel BK):" z "wsi z mnoznikiem modelu"',
     '105-mineral-bk.md'),
    ('106', 'paser', r'^OutlawLaw: .*zloto kryjowek', ['Paser'], True, None,
     'start "OutlawLaw: ... wpiete: ..., zloto kryjowek"; co dobe "Paser:" z "z paserem" i "z ladunkiem zostalo"', '106-paser.md'),
    ('107', 'zold i skarbiec', r'^SoldierPay: zold do obiegu', ['Zold'], True, None,
     'start "SoldierPay: zold do obiegu"; co dobe "Zold:" z "tarcza zoldu ... wlaczona" i "Korona: ... zwrot zoldu"',
     '107-zold-i-skarbiec.md'),
    # grupa 2b TOWARY 2 (lancuch 07.10: 107 -> 115..119 -> 108..114 przeniesione na 119; docs\paczki\PRZEGLAD-ZLOZENIA-2026-10-07.txt)
    ('115', 'cena od niedoboru', r'^RawPrice: cena surowcow od niedoboru', ['Ceny surowcow'], True, None,
     'start "RawPrice: cena surowcow od niedoboru - stala wzoru ... (CZYNNA), popyt ... (CZYNNY)"; co dobe "Ceny surowcow:"',
     '115-cena-od-niedoboru.md'),
    ('116', 'warsztaty: moneta', r'^WorkshopTrade: warsztaty towarowe w nowej monecie', ['Warsztaty towarowe'], True, None,
     'start "WorkshopTrade: warsztaty towarowe w nowej monecie CZYNNE - wpiete: ..."; co dobe "Warsztaty towarowe:"',
     '116-warsztaty-w-nowej-monecie.md'),
    ('117', 'karawany: ruda', r'^CaravanBulk: poprawka 115 - zakup przed wyborem celu', ['Karawany (przyczyny)', 'Karawany (kierunek)'],
     True, None, 'start "CaravanBulk: poprawka 115 - zakup przed wyborem celu (BK BuyGoods): latka wpieta"; co dobe "Karawany '
     '(przyczyny):" i "Karawany (kierunek):"', '117-karawany-ruda-dociera.md'),
    ('118', 'towary BK: moneta', r'^HistoricalPrices: wartosc z definicji towarow BK', [], True, ('Ceny surowcow', 'def_kat'),
     'start "HistoricalPrices: wartosc z definicji towarow BK (BKItems.InitializeTradeGood) - wpieta"; co dobe "Towary z wartoscia '
     'z definicji przedmiotu" w "Ceny surowcow:"', '118-towary-w-nowej-monecie.md'),
    ('119', 'wozy do miasta', r'^MarketCarts: ', ['Dowoz (wozy)'], True, None,
     'start "MarketCarts: poprawka 119 - wozy wsi do najlepiej placacego miasta w zasiegu 250 (CZYNNE; ...)"; co dobe "Dowoz '
     '(wozy):"', '119-wozy-do-najlepszego-miasta.md'),
    ('108', 'ludzie: jednostka', r'^PeopleUnit: ', [], True, ('Ludzie', 'hz_tabory'),
     'start "PeopleUnit: jednostka ludzi ... CZYNNA"; co dobe "hearth za ludzi dzis" w linii "Ludzie:"',
     '108-ludzie-jednostka.md'),
    ('109', 'ludzie: przyrost', r'^PopulationLaw: przyrost naturalny', ['Ludzie (przyrost)'], True, None,
     'start "PopulationLaw: przyrost naturalny wsi ... CZYNNY"; co dobe "Ludzie: przyrost naturalny +N ludzi"',
     '109-ludzie-przyrost.md'),
    ('110', 'K5: kasa zamku', r'^CastlePurse: kasa zamku', ['Kasy zamkow'], True, None,
     'start "CastlePurse: kasa zamku jako prawdziwy pieniadz CZYNNA"; co dobe "Kasy zamkow:" z "danina podzamcza"',
     '110-k5-kasa-zamku.md'),
    ('111', 'K6: kasy miast', r'^TownPurse: kasa miasta', ['Kasy miast'], True, None,
     'start "TownPurse: kasa miasta jako prawdziwy pieniadz CZYNNA, tryb regulatora 1"; co dobe "Kasy miast:" z "zawor"',
     '111-k6-kasy-miast.md'),
    ('112', 'K7: utarg wsi', r'^VillageTakings: ', ['Utarg wsi'], True, None,
     'start "VillageTakings: utarg wsi bez znikania (K7) - latki wpiete"; co dobe "Utarg wsi:" z "nieprzypisane 0"',
     '112-k7-utarg-wsi.md'),
    ('113', 'ludzie: spustosz.', r'^Devastation: ', [], True, ('Ludzie', 'spustoszenie'),
     'start "Devastation: spustoszenie jako ulamek okregu ... CZYNNE"; co dobe "spustoszenie dzis" w linii "Ludzie:"',
     '113-ludzie-spustoszenie.md'),
]
NR_OGNIW = [o[0] for o in OGNIWA]
# grupy ogniw dla --test (nazwy robocze; grupy testowe Jeffa z numerami 1-5 sa nizej w GRUPY_GRY i maja opcje --grupa)
GRUPY_TESTOWE = OrderedDict([
    ('dowoz', ['100', '101']), ('ksiegi', ['102', '102b']), ('surowce', ['102b', '103', '104', '105']),
    ('pieniadz', ['106', '107']), ('towary2', ['115', '116', '117', '118', '119']), ('ludzie', ['108', '109']),
    ('kasy', ['110', '111', '112']), ('wszystkie', list(NR_OGNIW)),
])
# GRUPY TESTOWE W GRZE (decyzja Jeffa 06.10 "3 razy": testy grupami; docs\STAN-PRAC.md i sekcja test_groups przegladu
# kolizji lancucha). Pola: nazwa, ogniwa, galaz DLL, kampania ('nowa' = wymagana, 'zalecana' = nowa zalecana, 'zapis' = ten
# sam zapis co poprzednia grupa), zalecana liczba dob (od, do), opis testu, przypisanie linii do ogniw.
# Liczba dob grup 4 i 5 to parametr projektu: grupa 4 potrzebuje ponad trzech tygodni, bo alarm "do zapasu brakuje" i
# dojscie kies wsi do stanu ustalonego licza sie w tygodniach (paczki 110 ryzyko 2 i 112); grupa 5 jak pozostale.
GRUPY_GRY = OrderedDict([
    ('1', {'nazwa': 'TOWAR', 'ogniwa': ['101', '102', '102b', '103', '104', '105'], 'dll': 'n105-mineral-bk',
           'kampania': 'nowa', 'dob': (15, 20), 'bee': False,
           'test': 'NOWA kampania po swiezym starcie gry (wymaga jej 104), 15-20 dob, zapis na koncu = stan "przed" grupy 2',
           'przypisanie': '"StartStock" = 104 (raz, na starcie); "Karawany" = 103; "Mineraly" i dopisaly = model = 105; '
                          '"Pieniadz swiata" / "Przeplywy osad" = 102 i 102b; "Dowoz" = 100 i 101'}),
    ('2', {'nazwa': 'PIENIADZ', 'ogniwa': ['106', '107'], 'dll': 'n107-zold-i-skarbiec', 'kampania': 'zapis',
           'dob': (12, 15), 'bee': False,
           'test': 'ten sam zapis co koniec grupy 1 (porownanie ksiegi przed / po na tym samym swiecie), 12-15 dob; pierwsze '
                   '2-3 doby to jednorazowa wyprzedaz zaleglego lupu band - nie brac ich do sredniej',
           'przypisanie': '"Paser" i pozycje paser / bandy w kasach miast = 106; "Zold", "Korona: zwrot zoldu", "zold '
                          'garnizonow", "sakiewki ludzi - zycie w miastach" = 107'}),
    # grupa 2b (noc 06/07.10): ogniwa 115-119 zlozone na n107, ida do gry PRZED grupami 3-5 (kolejka 108..114 przeniesiona na 119 -
    # galezie m108..m114); numery 3-5 zostaja, bo tak nazywaja je STAN-PRAC i opisy paczek
    ('2b', {'nazwa': 'TOWARY 2', 'ogniwa': ['115', '116', '117', '118', '119'], 'dll': 'n119-wozy-do-najlepszego-miasta',
            'kampania': 'nowa', 'dob': (20, 20), 'bee': False,
            'test': 'swiezy start gry, NOWA kampania (115, 116 i 118 przeliczaja pamiec rynku, kapital warsztatow i popyt tylko w sesji, '
                    'ktora zalozyla kampanie), 20 dob; DLL Armoury-grupa-towary2.dll (md5 faade7bf...); GLOWNA LICZBA: "Ruda: miast bez '
                    'towaru" w ostatniej dobie - dotad 74 -> 69, oczekiwane ok. 25-30, ponad 50 = ALARM',
            'przypisanie': '"Ceny surowcow" (bez czesci o towarach z definicji), "RawPrice" = 115; "Warsztaty towarowe", "WorkshopTrade", '
                           'pozycja "warsztaty towarowe" w kasach miast = 116; "Karawany (przyczyny)", "Karawany (kierunek)", "CaravanBulk: '
                           'poprawka 115" = 117; "HistoricalPrices: wartosc z definicji / przelicznik popytu", "Towary z wartoscia z '
                           'definicji" w "Ceny surowcow" = 118; "Dowoz (wozy)", "MarketCarts" = 119; "Ruda / Drewno: miast bez towaru" = '
                           'wspolny skutek 115 + 117 + 119'}),
    ('3', {'nazwa': 'LUDZIE', 'ogniwa': ['108', '109'], 'dll': 'm109-ludzie-przyrost', 'kampania': 'nowa',
           'dob': (15, 20), 'bee': True,
           'test': 'przed startem 13 kluczy BetterEconomy (tools\\bee\\zamknij-ujscia-bee.ps1 -NaSucho, potem naprawde); NOWA '
                   'kampania po swiezym starcie gry (siew puli wyrzutkow i kalibracja raz, przy zalozeniu), 15-20 dob',
           'przypisanie': '"hearth za ludzi dzis" i pula poczatkowa = 108; "przyrost naturalny" i stan miast = 109; RESZTA '
                          '+65..135 hearth = klucze BEE otwarte, -27..-33 = latki 108 nie dzialaja'}),
    ('4', {'nazwa': 'KASY', 'ogniwa': ['110', '111', '112'], 'dll': 'm112-k7-utarg-wsi', 'kampania': 'zalecana',
           'dob': (21, 30), 'bee': False,
           'test': 'NOWA kampania zalecana (jednorazowe przyciecie daru startowego kas: zamki ok. 5.09 mln, miasta ok. 13.98 '
                   'mln), 21-30 dob (alarm "do zapasu brakuje" i kiesy wsi licza sie w tygodniach)',
           'przypisanie': '"Kasy zamkow" i "danina podzamcza" = 110; "Kasy miast", "udzial korony", tarcza ZBEDNA = 111; '
                          '"Utarg wsi", "zywnosc kupiona we wsiach", "sakwy" = 112'}),
    ('5', {'nazwa': 'SPUSTOSZENIE', 'ogniwa': ['113'], 'dll': 'm113-ludzie-spustoszenie', 'kampania': 'zalecana',
           'dob': (15, 20), 'bee': False,
           'test': 'NOWA kampania zalecana (konta uchodzcow od zera), 15-20 dob i co najmniej jeden zakonczony rabunek wsi',
           'przypisanie': '"spustoszenie dzis" w "Ludzie:", "Ludzie (spustoszenie)", 3 kolumny CSV, "ruch ludzi (..., '
                          'spustoszenie i powroty uchodzcow)" = 113'}),
])
NAZWY_GRUP_GRY = {'towar': '1', 'towary': '1', 'pieniadz': '2', 'towary2': '2b', 'towary-2': '2b', 'towar2': '2b', 'ceny': '2b',
                  'ludzie': '3', 'kasy': '4', 'spustoszenie': '5'}


def grupa_ogniwa(nr):
    """Numer grupy testowej, do ktorej nalezy ogniwo (100 liczy sie do grupy 1 - jej log je obejmuje); None dla nieznanego."""
    if nr == '100':
        return '1'
    for g, opis in GRUPY_GRY.items():
        if nr in opis['ogniwa']:
            return g
    return None


def ix_ogniwa(nr):
    """Miejsce ogniwa w lancuchu (0 = najstarsze); -1 dla nieznanego numeru."""
    return NR_OGNIW.index(nr) if nr in NR_OGNIW else -1
OCZEKIWANE_STARTU = [
    ('ROT-RBM luki przechwycone', r'^HistoricalPrices: ROT-RBM luki .*przechwycone'),
    ('ColdStart zbrojownie', r'^ColdStart: zbrojownie'),
    ('ColdStart zapas kupiecki', r'^ColdStart: zapas kupiecki'),
    ('ColdStart ochotnicy', r'^ColdStart: ochotnicy tieru 2\+'),
    ('LootPrices 4/4', r'^LootPrices: .*4/4'),
    ('ForgeClock CZYNNY', r'^ForgeClock: .*CZYNNY'),
]


# =====================================================================================================================
# WCZYTANIE I PODZIAL LOGU
# =====================================================================================================================
def dekoduj(b):
    """Bajty -> (tekst, nazwa kodowania). UTF-8 z BOM / bez / cp1250; nigdy wyjatek."""
    if b.startswith(b'\xef\xbb\xbf'):
        return b[3:].decode('utf-8', 'replace'), 'utf-8 z BOM'
    if b.startswith(b'\xff\xfe') or b.startswith(b'\xfe\xff'):
        return b.decode('utf-16', 'replace'), 'utf-16'
    try:
        return b.decode('utf-8'), 'utf-8'
    except UnicodeDecodeError:
        pass
    for utnij in (1, 2, 3):                 # log uciety w polowie znaku wielobajtowego (gra zamknieta w trakcie zapisu)
        try:
            return b[:-utnij].decode('utf-8'), 'utf-8 (uciety ostatni znak)'
        except UnicodeDecodeError:
            pass
    zle = b.decode('utf-8', 'replace').count(u'\ufffd')
    dobre = len(re.findall(br'[\xc2-\xdf][\x80-\xbf]|[\xe0-\xef][\x80-\xbf]{2}|[\xf0-\xf4][\x80-\xbf]{3}', b))
    if dobre >= zle and dobre > 0:
        return b.decode('utf-8', 'replace'), 'utf-8 (uszkodzone bajty: %d)' % zle
    try:
        return b.decode('cp1250'), 'cp1250'
    except UnicodeDecodeError:
        return b.decode('latin-1', 'replace'), 'latin-1 (awaryjnie)'


class Wpis(object):
    __slots__ = ('nr', 'czas', 'tresc', 'pref', 'reszta', 'dzien', 'ciag', 'seg', 'D', 'blok', 'temat', 'dane', 'braki')

    def __init__(self, nr, czas, tresc):
        self.nr, self.czas, self.tresc = nr, czas, tresc
        self.pref = self.reszta = self.dzien = self.D = self.blok = self.temat = self.dane = None
        self.braki = None
        self.ciag = []
        self.seg = 0


RX_LINIA = re.compile(r'^\[(\d\d:\d\d:\d\d)\] ?(.*)$')
RX_PREFIKS = re.compile(r'^([^:]{1,60}?)(?: \(dzien (\d+)[^)]*\))?:(?: (.*))?$', re.S)
RX_DZIEN = re.compile(r'^dzien (\d+)\b')
# "Ludzie: przyrost naturalny +N ludzi (g srednio X% rocznie, wazone ludnoscia wsi; dzien D; ..." (ogniwo 109): numer dnia
# stoi w pierwszym nawiasie, nie na poczatku linii
RX_DZIEN_PRZYROSTU = re.compile(r'^przyrost naturalny [^|]{0,160}?; dzien (\d+)[;)]')


def rozloz(tekst):
    """Tekst logu -> (naglowek sesji, lista wpisow). Linie bez znacznika czasu doklejane do poprzedniego wpisu."""
    wpisy, naglowek = [], None
    for i, linia in enumerate(tekst.splitlines(), 1):
        m = RX_LINIA.match(linia)
        if not m:
            if wpisy:
                wpisy[-1].ciag.append(linia)
            elif linia.strip() and naglowek is None:
                naglowek = linia.strip()
            continue
        w = Wpis(i, m.group(1), m.group(2))
        if w.tresc.startswith('ERROR in '):
            w.pref = 'ERROR'
            w.reszta = w.tresc
        else:
            p = RX_PREFIKS.match(w.tresc)
            if p and p.group(1).strip() and not p.group(1)[0].isspace():
                w.pref = p.group(1).strip()
                w.reszta = p.group(3) or ''
                if p.group(2):
                    w.dzien = int(p.group(2))
                else:
                    d = RX_DZIEN.match(w.reszta)
                    if not d and w.pref == 'Ludzie':
                        d = RX_DZIEN_PRZYROSTU.match(w.reszta)
                    if d:
                        w.dzien = int(d.group(1))
            else:
                w.reszta = w.tresc
        wpisy.append(w)
    return naglowek, wpisy


class Blok(object):
    __slots__ = ('D', 'nr_od', 'nr_do', 'tematow', 'niepelny')

    def __init__(self, D):
        self.D, self.nr_od, self.nr_do, self.tematow, self.niepelny = D, None, None, 0, False


def ustal_przesuniecia(wpisy, uwagi):
    """Przesuniecie numeru dnia kazdego prefiksu: tabela z kodu + nauka z linii tej samej sekundy."""
    glosy = defaultdict(Counter)
    i, n = 0, len(wpisy)
    while i < n:
        j = i
        grupa = []
        while j < n and wpisy[j].czas == wpisy[i].czas:
            if wpisy[j].dzien is not None and wpisy[j].pref:
                grupa.append(wpisy[j])
            j += 1
        znane = set(w.dzien - PRZESUNIECIE_DNIA[w.pref] for w in grupa if w.pref in PRZESUNIECIE_DNIA)
        if len(znane) == 1:
            dref = list(znane)[0]
            for w in grupa:
                glosy[w.pref][w.dzien - dref] += 1
        i = j
    prz = {}
    for pref, licz in glosy.items():
        naj, ile = licz.most_common(1)[0]
        razem = sum(licz.values())
        if pref in PRZESUNIECIE_DNIA:
            if naj != PRZESUNIECIE_DNIA[pref] and ile >= 3 and ile >= 0.8 * razem and naj in (-1, 0, 1, 2):
                prz[pref] = naj
                uwagi.append('numer dnia w liniach "%s:" przesuniety o %+d wobec tabeli z kodu (%d z %d linii) - '
                             'przyjeto wartosc z logu' % (pref, naj - PRZESUNIECIE_DNIA[pref], ile, razem))
        elif naj in (-1, 0, 1, 2):
            prz[pref] = naj
    return prz


def podziel_na_doby(wpisy, uwagi):
    """Nadaje wpisom segmentu numer bloku (doby). Zwraca liste blokow."""
    if not wpisy:
        return []
    prz = ustal_przesuniecia(wpisy, uwagi)
    for w in wpisy:
        if w.dzien is not None and w.pref:
            w.D = w.dzien - prz.get(w.pref, PRZESUNIECIE_DNIA.get(w.pref, 0))
    # linie bez numeru dnia: doba najblizszej linii z numerem w tej samej sekundzie, inaczej poprzedniej
    ostatni = None
    for idx, w in enumerate(wpisy):
        if w.D is not None:
            ostatni = w
            continue
        if ostatni is not None and ostatni.czas == w.czas:
            w.D = ostatni.D
            continue
        nast = None
        for k in range(idx + 1, min(idx + 60, len(wpisy))):
            if wpisy[k].czas != w.czas:
                break
            if wpisy[k].dzien is not None and wpisy[k].D is not None:
                nast = wpisy[k]
                break
        if nast is not None:
            w.D = nast.D
        elif ostatni is not None:
            w.D = ostatni.D
    dni = sorted(set(w.D for w in wpisy if w.D is not None))
    if not dni:
        return []
    tematy_dnia = defaultdict(set)
    for w in wpisy:
        if w.dzien is not None and w.D is not None:
            tematy_dnia[w.D].add(w.pref)
    licznosci = sorted(len(tematy_dnia[d]) for d in dni)
    mediana = licznosci[len(licznosci) // 2]
    # chude doby na poczatku (np. samotna linia startowa z numerem dnia) to nie doby - wracaja do linii startowych
    while len(dni) > 1 and len(tematy_dnia[dni[0]]) < max(2, 0.25 * mediana):
        d0 = dni.pop(0)
        for w in wpisy:
            if w.D == d0:
                w.D = None
    # linie sprzed pierwszej linii dziennej pierwszej doby to start kampanii
    pierwsza = None
    for w in wpisy:
        if w.dzien is not None and w.D == dni[0]:
            pierwsza = w
            break
    if pierwsza is not None:
        for w in wpisy:
            if w.nr >= pierwsza.nr:
                break
            if w.czas != pierwsza.czas or w.dzien is None:
                w.D = None
    indeks = dict((d, i) for i, d in enumerate(dni))
    bloki = [Blok(d) for d in dni]
    for w in wpisy:
        if w.D is None or w.D not in indeks:
            w.blok = None
            continue
        w.blok = indeks[w.D]
        b = bloki[w.blok]
        b.nr_od = w.nr if b.nr_od is None else min(b.nr_od, w.nr)
        b.nr_do = w.nr if b.nr_do is None else max(b.nr_do, w.nr)
    for i, d in enumerate(dni):
        bloki[i].tematow = len(tematy_dnia[d])
    if len(bloki) > 1 and bloki[-1].tematow < 0.5 * mediana:
        bloki[-1].niepelny = True
    return bloki


# =====================================================================================================================
# SESJA
# =====================================================================================================================
class Sesja(object):
    def __init__(self):
        self.sciezka = self.kodowanie = self.naglowek = None
        self.rozmiar = 0
        self.linii = 0
        self.wpisy = []
        self.segmenty = []          # [(nr_linii_znacznika, [wpisy], [bloki])]
        self.wybrany = 0
        self.bloki = []
        self.wyb = []               # indeksy blokow po --dni
        self.szer = defaultdict(dict)       # nazwa tematu -> {blok: slownik}
        self.wiersze = defaultdict(lambda: defaultdict(list))   # temat wielolinijkowy -> {blok: [slowniki]}
        self.linie_tematu = defaultdict(list)
        self.zle = defaultdict(list)        # nazwa tematu -> [(wpis, braki)]
        self.obce = defaultdict(list)       # prefiks bez parsera -> wpisy w dobach
        self.start = []             # linie startowe (gra + kampania wybranego segmentu)
        self.uwagi = []             # uwagi techniczne (sekcja "formaty nierozpoznane")
        self.alarmy = []
        self.bledy = []             # (klucz, licznik, nr pierwszej, przyklad)
        self.n_error = self.n_exception = self.n_potkniec = 0
        self.ogniwa = []
        self.ogniwo_w_grze = None
        self.fakty = OrderedDict()

    # ----- dostep do szeregow
    def ser(self, nazwa):
        """[(indeks bloku, slownik)] dla wybranych dob."""
        s = self.szer.get(nazwa) or {}
        return [(bi, s[bi]) for bi in self.wyb if bi in s]

    def doba(self, bi):
        return bi + 1

    def pelne(self):
        return [bi for bi in self.wyb if not self.bloki[bi].niepelny] or list(self.wyb)


def status_linii(tresc):
    if re.search(r'\bBRAK\b', tresc):
        return 'BRAK'
    # Devastation (113): "NIECZYNNE - wymaga jednostki ludzi, przyrostu naturalnego i obu latek rabunku" - mechanizm stoi,
    # choc ustawienie jest wlaczone (sprawdzane przed "CZYNN", ktore jest czescia tego slowa)
    if 'NIE wpiet' in tresc or 'NIECZYNN' in tresc:
        return 'NIE wpieta'
    if (re.search(r'WYLACZON', tresc) or 'regula wylaczona w ustawieniach' in tresc
            or 'wylaczona - same liczniki' in tresc                    # PeopleUnit (108)
            or 'hearth dziennie) wylaczony w ustawieniach' in tresc    # PopulationLaw: przyrost naturalny (109)
            or 'ponizej progu) wylaczone w ustawieniach' in tresc):    # Devastation (113)
        return 'WYLACZONE'
    if 'CZYNN' in tresc:
        return 'CZYNNE'
    if 'wpiet' in tresc:
        return 'wpiete'
    return 'inne'


def analizuj(tekst, sciezka='(tekst)', kodowanie='?', rozmiar=None, wczytanie=None, dni=None):
    ses = Sesja()
    ses.sciezka, ses.kodowanie = sciezka, kodowanie
    ses.rozmiar = rozmiar if rozmiar is not None else len(tekst.encode('utf-8', 'replace'))
    ses.linii = tekst.count('\n') + (0 if tekst.endswith('\n') or not tekst else 1)
    ses.naglowek, ses.wpisy = rozloz(tekst)

    # --- segmenty: kazde "Behavior dodany do kampanii." to nowa kampania albo wczytanie
    seg, granice = 0, []
    for w in ses.wpisy:
        if w.tresc.startswith(ZNACZNIK_KAMPANII):
            seg += 1
            granice.append(w.nr)
        w.seg = seg
    liczba_seg = max(1, seg)
    for s in range(1, liczba_seg + 1):
        ws = [w for w in ses.wpisy if w.seg == s or (seg == 0)]
        uw = []
        try:
            bloki = podziel_na_doby(ws, uw)
        except Exception as e:
            bloki = []
            uw.append('podzial na doby nie powiodl sie (%s: %s)' % (type(e).__name__, e))
        ses.segmenty.append((granice[s - 1] if granice else 1, ws, bloki, uw))
    if wczytanie is not None and 1 <= wczytanie <= len(ses.segmenty):
        ses.wybrany = wczytanie - 1
    else:
        ses.wybrany = max(range(len(ses.segmenty)), key=lambda i: (len(ses.segmenty[i][2]), i))
    _, ws, ses.bloki, uw = ses.segmenty[ses.wybrany]
    ses.uwagi.extend(uw)
    ses.wpisy_seg = ws

    # --- zakres dob
    ses.wyb = list(range(len(ses.bloki)))
    if dni and ses.bloki:
        a, b = dni
        if a >= 10000 or b >= 10000:
            ses.wyb = [i for i, bl in enumerate(ses.bloki) if a <= bl.D <= b]
        else:
            ses.wyb = [i for i in range(len(ses.bloki)) if a <= i + 1 <= b]

    # --- linie startowe: start gry (przed pierwszym znacznikiem) + start kampanii wybranego segmentu
    pierwszy_nr = ses.bloki[0].nr_od if ses.bloki else None
    for w in ses.wpisy:
        if w.seg == 0 and seg > 0:
            ses.start.append(w)
    for w in ws:
        if w.blok is None and (pierwszy_nr is None or w.nr < pierwszy_nr) and not (w.seg == 0 and seg > 0):
            ses.start.append(w)

    _rozdaj_tematy(ses, ws)
    _zbierz_bledy(ses)
    _zbierz_fakty(ses, ws)
    _ustal_lancuch(ses, ws)
    try:
        _alarmy(ses, ws)
    except Exception as e:      # reguly maja wlasne zabezpieczenia; to jest ostatnia siatka
        ses.uwagi.append('alarmy: blad wewnetrzny (%s: %s)' % (type(e).__name__, e))
    return ses


def _rozdaj_tematy(ses, ws):
    for w in ws:
        if not w.pref or w.pref == 'ERROR':
            continue
        kandydaci = TEMATY_WG_PREFIKSU.get(w.pref)
        if not kandydaci:
            if w.blok is not None:
                ses.obce[w.pref].append(w)
            continue
        temat = None
        for t in kandydaci:
            if t.pasuje(w.reszta or ''):
                temat = t
                break
        if temat is None:
            if w.blok is not None:
                ses.obce[w.pref + ' (inny rodzaj linii)'].append(w)
            continue
        w.temat = temat
        ses.linie_tematu[temat.nazwa].append(w)
        if not temat.kol and temat.extra is None:
            continue
        try:
            d, braki = temat.czytaj(w.tresc)
        except Exception as e:
            d, braki = OrderedDict(), ['wyjatek:%s' % type(e).__name__]
        d['_nr'] = w.nr
        w.dane, w.braki = d, braki
        if braki:
            ses.zle[temat.nazwa].append((w, braki))
            continue
        if w.blok is None:
            continue
        if temat.wielo:
            ses.wiersze[temat.nazwa][w.blok].append(d)
        else:
            if w.blok in ses.szer[temat.nazwa]:
                ses.szer[temat.nazwa][w.blok]['_powtorzen'] = ses.szer[temat.nazwa][w.blok].get('_powtorzen', 1) + 1
                d['_powtorzen'] = ses.szer[temat.nazwa][w.blok]['_powtorzen']
            ses.szer[temat.nazwa][w.blok] = d
    for nazwa, wg_bloku in ses.wiersze.items():
        t = TEMATY_WG_NAZWY[nazwa]
        for bi, lista in wg_bloku.items():
            try:
                ses.szer[nazwa][bi] = t.agreg(lista)
            except Exception as e:
                ses.uwagi.append('%s: agregacja doby %d nie powiodla sie (%s)' % (nazwa, bi + 1, type(e).__name__))


RX_POTK = re.compile(r'(?:[Pp]otkniecia|POTKNIECIA)[^.|]*')     # "| POTKNIECIA N" = "Dowoz (wozy):" (ogniwo 119)


def _zbierz_bledy(ses):
    grupy = OrderedDict()

    def dodaj(klucz, w, przyklad):
        if klucz not in grupy:
            grupy[klucz] = [0, w.nr, przyklad]
        grupy[klucz][0] += 1
    for w in ses.wpisy:
        if w.pref == 'ERROR':
            ses.n_error += 1
            m = re.match(r'^(ERROR in [^:]{1,60}): ?([\w.]+)?:? ?', w.tresc)
            # przyklad: komunikat wyjatku i pierwsza linia sladu stosu
            reszta = (w.tresc[m.end():] if m else '') + ((' | ' + w.ciag[0].strip()) if w.ciag else '')
            dodaj((m.group(1) + ': ' + (m.group(2) or '')) if m else w.tresc[:70], w, reszta)
            continue
        calosc = w.tresc if not w.ciag else w.tresc + ' ' + ' '.join(w.ciag[:3])
        if 'Exception' in calosc or 'exception' in calosc:
            ses.n_exception += 1
            m = re.search(r'([\w.]*Exception)', calosc)
            dodaj('%s: %s' % (w.pref or 'bez prefiksu', m.group(1) if m else 'Exception'), w, w.tresc)
            continue
        for m in RX_POTK.finditer(w.tresc):
            liczby = [int(x) for x in re.findall(r'(?<![\w.])(\d+)(?![\w.%])', m.group(0))]
            if any(x > 0 for x in liczby):
                ses.n_potkniec += 1
                dodaj('%s: %s' % (w.pref or '?', re.sub(r'\d+', 'N', m.group(0)).split(':')[0][:40]), w, m.group(0))
                break
    ses.bledy = sorted(([k] + v for k, v in grupy.items()), key=lambda g: (0 if g[0].startswith('ERROR') else 1, g[2]))


# "CastlePurse: / TownPurse: poczatek kampanii (doba 1.00) - dar startowy w kasach zamkow (...) przyciety do zapasu kupcow:
# zamkow 130, kasy A -> B (zdjeto C w N zamkach, najwiecej M - nazwa; zapas razem R)" albo "zapis z X. doby kampanii
# wczytany pierwszy raz z ta zmiana (regulator gry zostawil ok. P% daru) - dar startowy ..."
RX_PRZYCIECIE = re.compile(
    r'(?:poczatek kampanii \(doba (-?[\d.,]+)\)|zapis z (-?[\d.,]+)\. doby kampanii wczytany pierwszy raz z ta zmiana '
    r'\(regulator gry zostawil ok\. (-?[\d.,]+)% daru\)) - dar startowy w kasach (?:zamkow|miast) \([^)]*\) przyciety do '
    r'zapasu kupcow: (?:zamkow|miast) (\d+), kasy (-?\d+) -> (-?\d+) \(zdjeto (-?\d+) w (\d+) (?:zamkach|miastach), '
    r'najwiecej (-?\d+)(?: - [^;]*)?; zapas razem (-?\d+)\)')


def _zbierz_fakty(ses, ws):
    f = ses.fakty
    wszystkie = ses.start + [w for w in ws if w.blok is not None]
    for w in wszystkie:
        t = w.tresc
        if w.pref == 'Kalendarz':
            m = re.search(r'rok ma (\d+) dni', t)
            if m:
                f['rok_dni'] = int(m.group(1))
        elif w.pref == 'MaterialLaw':
            m = re.search(r'wydobycie rudy x(-?\d+(?:[.,]\d+)?), drewna x(-?\d+(?:[.,]\d+)?)', t)
            if m:
                f['mnoznik_rudy'] = float(m.group(1).replace(',', '.'))
                f['mnoznik_drewna'] = float(m.group(2).replace(',', '.'))
                f['mnoznik_nr'] = w.nr
            m = re.search(r', wydobycie w (\d+) modelach', t)
            if m:
                f['modele_wydobycia'] = (int(m.group(1)), w.nr)
        elif w.pref in ('CastlePurse', 'TownPurse'):
            # ogniwa 110 i 111: linia startowa i jednorazowe przyciecie daru startowego kas
            klucz = 'castle' if w.pref == 'CastlePurse' else 'town'
            if 'jako prawdziwy pieniadz' in t:
                f.setdefault(klucz + 'purse', w)
            else:
                m = RX_PRZYCIECIE.search(t)
                if m:
                    f[klucz + '_trim'] = {
                        'nr': w.nr, 'nowa': m.group(1) is not None, 'doba': _liczba(m.group(1) or m.group(2)),
                        'zostalo_proc': _liczba(m.group(3)), 'osad': int(m.group(4)), 'przed': int(m.group(5)),
                        'po': int(m.group(6)), 'zdjeto': int(m.group(7)), 'zdjeto_osad': int(m.group(8)),
                        'najwiecej': int(m.group(9)), 'zapas': int(m.group(10))}
                elif 'wieku kampanii nie da sie odczytac' in t or 'kampania ma wiek ujemny' in t:
                    f[klucz + '_trim_bez'] = w
        elif w.pref == 'VillageTakings':
            f.setdefault('villagetakings', w)
        elif w.pref == 'Utarg wsi (latki)':
            f.setdefault('utarg_latki', w)
        elif w.pref == 'Devastation':
            f.setdefault('devastation', w)
        elif w.pref == 'Spustoszenie' and 'WYLACZONE' in t:
            f.setdefault('spustoszenie_wyl', w)
        elif w.pref == 'ScorchedEarth':
            f.setdefault('scorched', w)
        elif w.pref == 'Kasy zamkow' and (w.reszta or '').find('| WYLACZONE') >= 0:
            f.setdefault('kasy_zamkow_wyl', w)
        elif w.pref == 'Kasy miast' and ((w.reszta or '').find('| WYLACZONE') >= 0 or 'latki regulatora nie sa wpiete' in t):
            f.setdefault('kasy_miast_wyl', w)
        elif w.pref == 'ColdStart' or (w.pref == 'StartKit' and 'zloto startowe' in t):
            f['nowa_kampania'] = True
        elif w.pref == 'Wyrzutki' and 'pula poczatkowa' in t:
            m = re.search(r'pula poczatkowa (\d+) ludzi', t)
            if m:
                f['pula_poczatkowa'] = int(m.group(1))
            f['pula_nr'] = w.nr
            m = re.search(r'; zdjeto ' + RX_F + r' hearth: (.*?)\)\.?$', t)      # dopisek ogniwa 108
            if m:
                f['zdjeto_hearth'] = _liczba(m.group(1))
                f['zdjeto_jak'] = m.group(2)
            f['nowa_kampania'] = True
        elif w.pref == 'Wyrzutki' and 'wczytano pule' in t:
            f['wczytana'] = True
        elif w.pref == 'KingdomTreasury':
            m = re.search(r'podmienionych stalych (\d+), oczekiwane (\d+)', t)
            if m:
                f['stale_skarbca'] = (int(m.group(1)), int(m.group(2)), w.nr)
        elif w.pref == 'StartStock':
            f.setdefault('startstock', []).append(w)
        elif w.pref == 'Ludzie' and 'plik wszystkich regionow' in t:
            f['csv'] = t.split('): ', 1)[-1].strip() if '): ' in t else None
        elif w.pref == 'Przeplywy osad' and (w.reszta or '').startswith('modele czynne'):
            f['modele'] = w
        elif w.pref == 'MoneyLedger' and 'liczniki wpiete' in t:
            f['moneyledger'] = w
        elif w.pref == 'CaravanBulk':
            # ogniwo 117: druga linia startowa "CaravanBulk: poprawka 115 - ..." (roboczy numer w kodzie) - osobny fakt
            if 'poprawka 115 - zakup przed wyborem celu' in t:
                f.setdefault('caravanbulk_117', w)
            else:
                f.setdefault('caravanbulk', w)
        elif w.pref == 'RawPrice':
            # ogniwo 115: linia startowa (latki modeli) i przeliczenie pamieci rynku nowej kampanii
            if 'cena surowcow od niedoboru' in t:
                f.setdefault('rawprice', w)
            elif t.startswith('RawPrice: nowa kampania - '):
                f.setdefault('rawprice_seed', w)
        elif w.pref == 'WorkshopTrade':
            # ogniwo 116: linia startowa, latka modelu finansow rodu (w kampanii), kapital startowy nowej kampanii
            if 'warsztaty towarowe w nowej monecie' in t:
                f.setdefault('workshoptrade', w)
            elif t.startswith('WorkshopTrade: latka modelu finansow rodu') or 'AddPlayerExpenseForWorkshops' in t:
                f.setdefault('workshoptrade_latka', w)
            elif t.startswith('WorkshopTrade: nowa kampania - '):
                f.setdefault('workshoptrade_seed', w)
            elif 'BRAK' in t:
                f.setdefault('workshoptrade_brak', w)
        elif w.pref == 'MarketCarts':
            f.setdefault('marketcarts', w)
        elif w.pref == 'HistoricalPrices':
            # ogniwo 118 (linie przy starcie gry i po wejsciu do kampanii); "popyt miast przeliczony" bywa tez bez 118 (test 14:08)
            if 'wartosc z definicji towarow BK' in t:
                f.setdefault('hp_definicja', w)
            elif 'popyt miast przeliczony na nowa monete' in t:
                f.setdefault('hp_popyt', w)
            elif 'przelicznik popytu od wartosci z definicji' in t:
                f.setdefault('hp_przelicznik', w)
            elif t.startswith('HistoricalPrices: UWAGA - ') and 'wartosc 0' in t:
                f.setdefault('hp_uwaga0', w)
        elif w.pref == 'MineralOnce':
            f.setdefault('mineralonce', w)
        elif w.pref == 'OutlawLaw' and 'wpiete: ' in t:
            f['outlaw'] = w
        elif w.pref == 'MarketRoad' and 'woz - udzwig taborow' in t:
            f['woz'] = w
        elif w.pref == 'SoldierPay':
            if 'zold do obiegu' in t:
                f['soldierpay'] = w
            elif 'latki modelu finansow' in t:
                f['soldierpay_latki'] = w
            elif 'tarcza zoldu w kasach miast' in t:
                f['soldierpay_tarcza'] = w
                if 'zbedna i nieczynna' in t:       # ogniwo 111 (K6): regulator kas miast niczego nie kasuje
                    f['tarcza_zbedna'] = w
        elif w.pref == 'PeopleUnit':
            f.setdefault('peopleunit', w)
        elif w.pref == 'PopulationLaw':
            if 'przyrost naturalny' in t:
                f.setdefault('przyrost_start', w)
            elif 'czynny model dobrobytu gry' in t:
                f['model_dobrobytu'] = w
            elif 'kalibracja ludnosci' in t:
                f.setdefault('kalibracja', w)
            elif 'srednia swiata' in t:
                f['srednia_swiata'] = w
            elif 'ludnosc miast zapisana jako stan' in t:
                f.setdefault('miasta_stan', w)
    # uklad linii ksiegi pieniadza: wpis 102 (w grze dzis) albo 102b - z linii dziennych, a gdy ich nie ma, z linii startowej
    uklady = Counter()
    for nazwa in ('Pieniadz swiata (bilans)', 'Przeplywy osad'):
        for bi, d in (ses.szer.get(nazwa) or {}).items():
            if d.get('uklad'):
                uklady[d['uklad']] += 1
    if ses.linie_tematu.get('Pieniadz swiata (rody)'):
        uklady[UKLAD_102B] += len(ses.linie_tematu['Pieniadz swiata (rody)'])
    f['uklad_linii'] = dict(uklady)
    if uklady:
        f['uklad_ksiegi'] = UKLAD_102B if uklady[UKLAD_102B] >= uklady[UKLAD_102] else UKLAD_102
        f['uklad_mieszany'] = bool(uklady[UKLAD_102B] and uklady[UKLAD_102])
    elif 'moneyledger' in f:
        f['uklad_ksiegi'] = UKLAD_102B if 'rozliczenie rodow' in f['moneyledger'].tresc else UKLAD_102


def _ustal_lancuch(ses, ws):
    wszystkie = ses.start + [w for w in ws if w.blok is not None]
    najwyzsze = None
    for ix, (nr, opis, wz, dzienne, zawsze, znak, poznac, paczka) in enumerate(OGNIWA):
        rx = re.compile(wz)
        trafienia = [w for w in wszystkie if rx.search(w.tresc)]
        dob = OrderedDict((n, len(ses.szer.get(n) or {})) for n in dzienne)
        linii = OrderedDict((n, len(ses.linie_tematu.get(n) or [])) for n in dzienne)
        znak_dob = None
        if znak:
            znak_dob = len([1 for d in (ses.szer.get(znak[0]) or {}).values() if d.get(znak[1]) is not None])
        jest = bool(trafienia)
        ses.ogniwa.append({'nr': nr, 'ix': ix, 'opis': opis, 'jest': jest, 'wpis': trafienia[0] if trafienia else None,
                           'ile': len(trafienia), 'dob': dob, 'linii': linii, 'zawsze': zawsze, 'znak': znak,
                           'znak_dob': znak_dob, 'poznac': poznac, 'paczka': paczka,
                           'status': status_linii(trafienia[0].tresc) if trafienia else None})
        if jest or any(linii.values()) or znak_dob:
            najwyzsze = nr
    ses.ogniwo_w_grze = najwyzsze


def ma_ogniwo(ses, nr):
    for o in ses.ogniwa:
        if o['nr'] == nr:
            return bool(o['jest'] or any(o['linii'].values()) or o.get('znak_dob'))
    return False


def ogniwo(ses, nr):
    for o in ses.ogniwa:
        if o['nr'] == nr:
            return o
    return None


# =====================================================================================================================
# ALARMY
# =====================================================================================================================
def _alarmy(ses, ws):
    def al(poziom, tekst, nr=None):
        ses.alarmy.append((poziom, nr, tekst))

    reguly = []

    def regula(f):
        reguly.append(f)
        return f


    @regula
    def r_bledy():
        if ses.n_error:
            g = [b for b in ses.bledy if b[0].startswith('ERROR')]
            al('ALARM', 'ERROR w logu: %d linii w %d grupach, pierwsza: %s' % (ses.n_error, len(g), obetnij(g[0][0], 70)),
               g[0][2])
        if ses.n_exception:
            g = [b for b in ses.bledy if not b[0].startswith('ERROR') and 'Exception' in b[0]]
            if g:
                al('ALARM', 'Exception poza liniami ERROR: %d linii, pierwsza: %s' % (ses.n_exception, obetnij(g[0][0], 70)),
                   g[0][2])

    @regula
    def r_start():
        for w in ses.start:
            st = status_linii(w.tresc)
            if st in ('BRAK', 'NIE wpieta'):
                al('ALARM', 'linia startowa %s: %s' % (st, wycinek(w.tresc, 110)), w.nr)
            elif st == 'WYLACZONE':
                al('uwaga', 'linia startowa WYLACZONE: %s' % wycinek(w.tresc, 105), w.nr)
        for w in ws:
            if w.blok is not None and w.pref in ('SoldierPay', 'MoneyLedger', 'CaravanBulk', 'MineralOnce', 'OutlawLaw',
                                                 'PeopleUnit', 'PopulationLaw', 'CastlePurse', 'TownPurse',
                                                 'VillageTakings', 'Devastation', 'RawPrice', 'WorkshopTrade', 'MarketCarts'):
                if status_linii(w.tresc) in ('BRAK', 'NIE wpieta'):
                    al('ALARM', 'linia %s w kampanii: %s' % (status_linii(w.tresc), wycinek(w.tresc, 105)), w.nr)
        st = ses.fakty.get('stale_skarbca')
        if st and st[0] != st[1]:
            al('ALARM', 'KingdomTreasury: podmienionych stalych %d, oczekiwane %d - zloto z niczego do skarbcow moze plynac'
               % (st[0], st[1]), st[2])

    @regula
    def r_lancuch():
        pewne = [o for o in ses.ogniwa if o['zawsze'] and o['ix'] >= ix_ogniwa('102')]
        obecne = [o for o in pewne if o['jest']]
        if obecne:
            szczyt = max(obecne, key=lambda o: o['ix'])
            for o in pewne:
                if o['ix'] < szczyt['ix'] and not o['jest']:
                    al('ALARM', 'lancuch niespojny: jest linia startowa ogniwa %s, a brak ogniwa %s (%s)'
                       % (szczyt['nr'], o['nr'], o['opis']))
        for o in ses.ogniwa:
            # linia startowa z BRAK / WYLACZONE ma juz wlasny alarm - brak linii dziennych jest wtedy skutkiem, nie nowina
            if o['jest'] and o['zawsze'] and ses.bloki and o['status'] not in ('BRAK', 'NIE wpieta', 'WYLACZONE'):
                for n, ile in o['linii'].items():
                    if ile == 0 and len(ses.pelne()) >= 2:
                        al('ALARM', 'ogniwo %s wpiete, ale w %d dobach nie ma ani jednej linii "%s:"'
                           % (o['nr'], len(ses.pelne()), n), o['wpis'].nr if o['wpis'] else None)
                if o['znak'] and not o['znak_dob'] and len(ses.szer.get(o['znak'][0]) or {}) >= 2:
                    al('ALARM', 'ogniwo %s wpiete, ale linie "%s:" nie maja jego odcinka (%d dob) - %s'
                       % (o['nr'], o['znak'][0], len(ses.szer.get(o['znak'][0]) or {}), o['poznac'].split('; ')[-1]),
                       o['wpis'].nr if o['wpis'] else None)
        if ses.fakty.get('uklad_mieszany'):
            u = ses.fakty.get('uklad_linii') or {}
            al('uwaga', 'ksiega pieniadza: w jednej kampanii linie w dwoch ukladach (wpis 102: %d, 102b: %d) - DLL podmieniony '
               'w trakcie sesji?' % (u.get(UKLAD_102, 0), u.get(UKLAD_102B, 0)))

    @regula
    def r_luki():
        for i in range(1, len(ses.bloki)):
            a, b = ses.bloki[i - 1], ses.bloki[i]
            if b.D - a.D > 1 and i in ses.wyb:
                al('ALARM', 'brak dob: po dniu gry %d nastepuje dzien %d (%d dob bez zadnej linii dziennej)'
                   % (a.D, b.D, b.D - a.D - 1), b.nr_od)
        pelne = ses.pelne()
        if len(pelne) < 3:
            return
        for t in TEMATY:
            if not t.bezwar:
                continue
            s = ses.szer.get(t.nazwa) or {}
            obecne = [bi for bi in pelne if bi in s]
            if len(obecne) < 2:
                continue
            brak = [bi for bi in pelne if obecne[0] < bi < obecne[-1] and bi not in s]
            if brak:
                al('ALARM', 'temat "%s": brak linii w dobach %s (tick dobowy przerwany albo zmieniony format)'
                   % (t.nazwa, ', '.join(str(b + 1) for b in brak[:8]) + (' ...' if len(brak) > 8 else '')),
                   ses.bloki[brak[0]].nr_od)

    @regula
    def r_ruda():
        ruda = ses.ser('Ruda')
        stan = ses.ser('Karawany (stan)')
        if ma_ogniwo(ses, '103') and len(stan) >= KARAWANY_MIN_DOB:
            a, b = stan[0][1], stan[-1][1]
            pa, pb = a.get('ruda_bez'), b.get('ruda_bez')
            if pa is not None and pb is not None and pb > KARAWANY_BEZ_TOWARU_CEL:
                spadek = proc(pa - pb, pa) if pa else 0.0
                if spadek is None or spadek < KARAWANY_MIN_SPADEK_PROC:
                    al('ALARM', 'karawany: "ruda: bez towaru" nie spada - %d -> %d miast po %d dobach (cel <= %d), '
                       'w jukach karawan %s ladunkow' % (pa, pb, len(stan), KARAWANY_BEZ_TOWARU_CEL,
                                                         skr(b.get('ruda_juki'))), b.get('_nr'))
        elif ruda:
            d = ruda[-1][1]
            p = d.get('bez_towaru_proc')
            if p is not None and p >= PROG_MIAST_BEZ_RUDY_PROC:
                al('uwaga', 'ruda: %d z %d miast bez towaru w ostatniej dobie (%.0f%%, prog %.0f%%)'
                   % (d.get('bez_towaru'), d.get('miast'), p, PROG_MIAST_BEZ_RUDY_PROC), d.get('_nr'))
        for nazwa, abs_prog, proc_prog, dop in (('Ruda', BEZ_WYJASNIENIA_RUDA_ABS, BEZ_WYJASNIENIA_RUDA_PROC, ''),
                                                ('Drewno', BEZ_WYJASNIENIA_DREWNO_ABS, 0.0,
                                                 ' (znana dosypka RealisticBannerlord ok. +2100 jest ponizej progu)')):
            zle = []
            for bi, d in ses.ser(nazwa):
                bw, raz = d.get('bez_wyjasnienia'), d.get('razem')
                if bw is None:
                    continue
                if abs(bw) > abs_prog and (not raz or 100.0 * abs(bw) / raz > proc_prog):
                    zle.append((abs(bw), bw, bi, d.get('_nr')))
            if zle:
                naj = max(zle)
                al('ALARM', '%s: "bez wyjasnienia" ponad prog %d w %d dobach, najwiecej %+d w dobie %d%s'
                   % (nazwa, abs_prog, len(zle), naj[1], naj[2] + 1, dop), naj[3])
        bud = [(bi, d) for bi, d in ruda if (d.get('budowy') or 0) > 0]
        if bud:
            al('uwaga', 'Ruda: pozycja "budowy" niezerowa w %d dobach (budowy nie powinny kupowac rudy), np. %d'
               % (len(bud), bud[0][1].get('budowy')), bud[0][1].get('_nr'))

    @regula
    def r_karawany():
        kar = ses.ser('Karawany')
        stan = ses.ser('Karawany (stan)')
        wyl = [(bi, d) for bi, d in kar if d.get('wylaczona')]
        if wyl:
            al('ALARM', 'Karawany: REGULA WYLACZONA w ustawieniach (%d dob)' % len(wyl), wyl[0][1].get('_nr'))
        for nazwa in SUROWCE_KARAWAN.values():
            seria, najdl, pocz = 0, 0, None
            biez_pocz = None
            for bi, d in stan:
                if d.get(nazwa + '_stoi'):
                    if seria == 0:
                        biez_pocz = d.get('_nr')
                    seria += 1
                    if seria > najdl:
                        najdl, pocz = seria, biez_pocz
                else:
                    seria = 0
            if najdl > KUPNO_STOI_MAKS_DOB:
                al('ALARM', 'Karawany (stan): %s - "KUPNO STOI" przez %d dob z rzedu (prog %d)'
                   % (nazwa, najdl, KUPNO_STOI_MAKS_DOB), pocz)
        if len(kar) >= KARAWANY_ROZRUCH_DOB:
            pierwsze = kar[:KARAWANY_ROZRUCH_DOB]
            if all((d.get('z_zakupem') or 0) == 0 for _, d in pierwsze):
                al('uwaga', 'Karawany: zero zakupow z nadwyzek przez pierwsze %d dob (rozruch po 104 mial trwac 7-10 dob)'
                   % KARAWANY_ROZRUCH_DOB, pierwsze[-1][1].get('_nr'))
        if kar:
            brak = sum(d.get('odp_brak_miejsca') or 0 for _, d in kar)
            zak = sum(d.get('z_zakupem') or 0 for _, d in kar)
            if brak > 20 and brak > KARAWANY_BRAK_MIEJSCA_KROTNOSC * max(1, zak):
                al('uwaga', 'Karawany: "brak miejsca w jukach" %d razy wobec %d wyjazdow z zakupem - rozwazyc zakup przed '
                   'zakupami BK' % (brak, zak), kar[-1][1].get('_nr'))
        # 103: miasta z kasa maja brak, a zadne nie daje ceny = wycena bez partii sie nie udala (cudzy model cen)
        bez_ceny = [(bi, d) for bi, d in stan if d.get('bez_ceny')]
        if bez_ceny:
            al('ALARM', 'Karawany (stan): "cena zbytu brak - nikt nie kupuje" przy niezerowym "brakuje" (%s; %d dob) - wycena '
               'zbytu sie nie udala, karawany tego nie kupia' % (bez_ceny[0][1].get('bez_ceny'), len(bez_ceny)),
               bez_ceny[0][1].get('_nr'))

    @regula
    def r_startstock():
        linie = ses.fakty.get('startstock') or []
        przeliczone = False
        for w in linie:
            t = w.tresc
            m = re.search(r'StartStock: (\w+) - (\d+) szt\. po .*? -> (\d+) ladunkow po .*?cel (-?\d+)', t)
            if m:
                przeliczone = True
                if int(m.group(3)) != int(m.group(4)):
                    al('ALARM', 'StartStock: %s po przeliczeniu %s ladunkow, cel %s - suma sie nie zgadza'
                       % (m.group(1), m.group(3), m.group(4)), w.nr)
            m = re.search(r'gotowe - .*?; potkniecia (\d+)', t)
            if m and int(m.group(1)) > 0:
                al('ALARM', 'StartStock: potkniecia %s przy przeliczeniu zapasu startowego' % m.group(1), w.nr)
            if 'BEZ przeliczenia' in t or 'WYLACZONE' in t or 'nie da sie odczytac' in t or 'przelicznik ladunku 1' in t:
                al('uwaga', 'StartStock: zapas startowy NIE przeliczony - %s' % obetnij(t[12:], 95), w.nr)
        ruda = ses.ser('Ruda')
        if przeliczone and ruda and ruda[0][0] == 0:
            raz = ruda[0][1].get('razem')
            if raz is not None and raz >= RUDA_RAZEM_PO_104:
                al('ALARM', 'po StartStock "Ruda: razem" pierwszej doby = %d (oczekiwane ponizej %d)'
                   % (raz, RUDA_RAZEM_PO_104), ruda[0][1].get('_nr'))
        if not linie and ses.fakty.get('nowa_kampania') and ma_ogniwo(ses, '105'):
            al('ALARM', 'ogniwo 105 wgrane, nowa kampania, a nie ma ani jednej linii "StartStock:" (ogniwo 104)')

    @regula
    def r_mineral():
        # Mnoznik rudy: po przebudowie lancucha 06.10 ogniwo 105 nie zmienia wydobycia i "wydobycie rudy x3.0" jest dobrze
        # (dawny alarm "x3.0 przy wgranym 105" usuniety). Alarmem ogniwa 105 jest latka NIE wpieta.
        jest105 = ma_ogniwo(ses, '105')
        w = ses.fakty.get('mineralonce')
        if w is not None and status_linii(w.tresc) not in ('NIE wpieta', 'BRAK') and 'latka wpieta' not in w.tresc:
            al('ALARM', 'MineralOnce: linia startowa bez "latka wpieta": %s' % wycinek(w.tresc, 90), w.nr)
        mi = ses.ser('Mineraly (dubel BK)')
        for bi, d in mi:
            if d.get('stan') in ('NIE wpieta', 'WYLACZONA'):
                al('ALARM', 'Mineraly (dubel BK): latka %s - BK dopisuje mineral dwa razy' % d.get('stan'), d.get('_nr'))
                break
        obce = [(bi, d) for bi, d in mi if d.get('obce')]
        if obce:
            al('ALARM', 'Mineraly (dubel BK): na liscie powtorzen inne przedmioty: %s (BK dubluje produkcje typu wsi?)'
               % obce[0][1].get('obce'), obce[0][1].get('_nr'))
        for k, lim in MINERALY_LIMIT_WSI.items():
            ponad = [(bi, d) for bi, d in mi if (d.get(k) or 0) > lim]
            if ponad:
                al('uwaga', 'Mineraly (dubel BK): %s w %d wsiach, wiecej niz %d wsi tego typu na mapie'
                   % (k, ponad[0][1].get(k), lim), ponad[0][1].get('_nr'))
        potk = [(bi, d) for bi, d in mi if (d.get('potkniecia') or 0) > 0]
        if potk:
            al('ALARM', 'Mineraly (dubel BK): potkniecia %d' % potk[-1][1].get('potkniecia'), potk[0][1].get('_nr'))
        # powtorzenia zdjete z listy, a zadna wies nie ma mnoznika modelu = zdjety wpis nie jest oddawany (pol wydobycia)
        bez_mn = [(bi, d) for bi, d in mi if d.get('stan') == 'zdjete' and d.get('wsi_mnoznik') == 0]
        if bez_mn:
            al('ALARM', 'Mineraly (dubel BK): powtorzenia zdjete, a "wsi z mnoznikiem modelu teraz 0" (%d dob) - zdjety wpis nie '
               'jest oddawany, wydobycie mineralow spada o polowe' % len(bez_mn), bez_mn[0][1].get('_nr'))
        ruda = [(bi, d) for bi, d in ses.ser('Ruda') if bi > 0]
        na_wies = [d['na_wies_do_modelu'] for _, d in ruda if d.get('na_wies_do_modelu') is not None]
        if jest105 and len(na_wies) >= 3:
            med = mediana(na_wies)
            if med > STOSUNEK_NA_WIES_PO_105:
                al('ALARM', 'Ruda: kopiaca wies dopisuje %.2f x tyle, ile liczy dla niej "model" (mediana, prog %.1f) przy ogniwie '
                   '105 - mineral nadal podwojny?' % (med, STOSUNEK_NA_WIES_PO_105), ruda[-1][1].get('_nr'))

    @regula
    def r_dowoz():
        dw = ses.ser('Dowoz')
        wyl = [(bi, d) for bi, d in dw if d.get('wylaczona')]
        if wyl:
            al('ALARM', 'Dowoz: LATKA WYLACZONA w ustawieniach (%d dob)' % len(wyl), wyl[0][1].get('_nr'))
        sk = ses.ser('Dowoz (skutki)')
        if sk:
            d = sk[-1][1]
            a, b = d.get('zatk_zamkowe_proc'), d.get('zatk_miejskie_proc')
            if a is not None and b is not None and a - b > ZATKANE_ROZNICA_PP:
                al('uwaga', 'Dowoz (skutki): zatkane magazyny wsi zamkowych %.1f%% wobec %.1f%% wsi miejskich '
                   '(roznica ponad %.0f pp)' % (a, b, ZATKANE_ROZNICA_PP), d.get('_nr'))

    @regula
    def r_pieniadz():
        bil = ses.ser('Pieniadz swiata (bilans)')
        stary_uklad = any(d.get('uklad') == UKLAD_102 for _, d in bil)
        # uklad wpisu 102 liczy zold dwa razy (raz w ujsciach, raz w saldach rodow) - reszta jest dodatnia i bliska zoldowi;
        # w ukladzie 102b zold siedzi w rozliczeniach rodow i reszta rzedu zoldu znaczylaby, ze poprawka nie dziala
        stos = [(d['reszta'] / float(d['zold']), bi, d) for bi, d in bil if d.get('zold') and d.get('reszta') is not None]
        blisko = [x for x in stos if ZOLD_DUBEL_OD <= x[0] <= ZOLD_DUBEL_DO]
        jak_zold = len(stos) >= 2 and len(blisko) >= 0.6 * len(stos)
        znany_dubel = jak_zold and stary_uklad
        zle, ponad_w_dublu = [], 0
        for bi, d in bil:
            rp, re_, zold = d.get('reszta_proc'), d.get('reszta'), d.get('zold')
            if rp is None or rp <= RESZTA_BILANSU_PROC_RUCHU:
                continue
            if znany_dubel and zold and re_ is not None and ZOLD_DUBEL_OD * zold <= re_ <= ZOLD_DUBEL_DO * zold:
                ponad_w_dublu += 1
            else:
                zle.append((rp, re_, bi, d.get('_nr')))
        if zle:
            naj = max(zle)
            al('ALARM', 'Pieniadz swiata (bilans): reszta ponad %.0f%% ruchu w %d dobach, najwiecej %s (%.0f%%) w dobie %d'
               % (RESZTA_BILANSU_PROC_RUCHU, len(zle), skr(naj[1], True), naj[0], naj[2] + 1), naj[3])
        if jak_zold:
            st = sorted(x[0] for x in blisko)
            d = blisko[-1][2]
            if znany_dubel:
                al('uwaga', 'Pieniadz swiata (bilans): znane podwojne liczenie zoldu (uklad wpisu 102, znika po 102b) - reszta '
                   'bliska zoldowi w %d z %d dob (mediana reszta/zold %.2f; np. reszta %s, zold %s); ponad %.0f%% ruchu: %d dob'
                   % (len(blisko), len(stos), st[len(st) // 2], skr(d.get('reszta'), True), skr(d.get('zold')),
                      RESZTA_BILANSU_PROC_RUCHU, ponad_w_dublu), d.get('_nr'))
            else:
                al('uwaga', 'Pieniadz swiata (bilans): uklad 102b, a reszta nadal rzedu zoldu w %d z %d dob (mediana reszta/zold '
                   '%.2f; np. reszta %s, zold %s) - po poprawce nie powinna' % (
                       len(blisko), len(stos), st[len(st) // 2], skr(d.get('reszta'), True), skr(d.get('zold'))), d.get('_nr'))
        niewp = [(bi, d) for bi, d in bil if d.get('rody_niewpiete')]
        if niewp:
            al('ALARM', 'Pieniadz swiata (bilans): licznik rozliczen rodow nie jest wpiety (%d dob) - salda rodow w pozycjach '
               'GiveGoldAction' % len(niewp), niewp[0][1].get('_nr'))
        # 102b: linia "Pieniadz swiata (rody):"
        rody = ses.ser('Pieniadz swiata (rody)')
        nd = [(bi, d) for bi, d in rody if (d.get('niedomkniete') or 0) > 0]
        if nd:
            al('ALARM', 'Pieniadz swiata (rody): rozliczenia niedomkniete w %d dobach (np. %d) - wyjatek w kodzie gry albo moda '
               'w trakcie rozliczenia rodu' % (len(nd), nd[0][1].get('niedomkniete')), nd[0][1].get('_nr'))
        nw = [(bi, d) for bi, d in rody if d.get('niewpiety')]
        if nw and not niewp:
            al('ALARM', 'Pieniadz swiata (rody): licznik rozliczen rodow nie jest wpiety (%d dob)' % len(nw), nw[0][1].get('_nr'))
        bild = dict(bil)
        rozne = []
        for bi, d in rody:
            b = bild.get(bi)
            if b is None or d.get('na_plus') is None or b.get('rody_plus') is None:
                continue
            if d['na_plus'] != b['rody_plus'] or abs(d.get('na_minus') or 0) != abs(b.get('rody_minus') or 0):
                rozne.append((bi, d, b))
        if rozne:
            bi, d, b = rozne[0]
            al('ALARM', 'Pieniadz swiata (rody): rozliczenia +%s/%s, a bilans liczy na plus %s, na minus %s (%d dob z rozjazdem)'
               % (d.get('na_plus'), d.get('na_minus'), b.get('rody_plus'), b.get('rody_minus'), len(rozne)), d.get('_nr'))
        zoldy = []
        for bi, d in rody:
            b = bild.get(bi)
            if b is not None and d.get('zold') is not None and b.get('zold') is not None and d['zold'] != b['zold']:
                zoldy.append((bi, d, b))
        if zoldy:
            bi, d, b = zoldy[0]
            al('uwaga', 'Pieniadz swiata (rody): zold naliczony %s, a w bilansie "W tym zold naliczony" %s (%d dob)'
               % (d.get('zold'), b.get('zold'), len(zoldy)), d.get('_nr'))
        if len(rody) >= 4:
            k = [d.get('rozliczen') for _, d in rody if d.get('rozliczen') is not None]
            med = mediana(k)
            if med and (max(k) - min(k)) > ROZLICZEN_ROZRZUT_PROC / 100.0 * med and (max(k) - min(k)) > 10:
                al('uwaga', 'Pieniadz swiata (rody): liczba rozliczen na dobe waha sie od %d do %d (mediana %d) - gra ma rozliczac '
                   'kazdy rod raz na dobe' % (min(k), max(k), med), rody[-1][1].get('_nr'))
        prz = ses.ser('Przeplywy osad')
        slepy = [(bi, d) for bi, d in prz if d.get('licznik_slepy')]
        if slepy:
            al('ALARM', 'Przeplywy osad: licznik zoldu nie widzial wyplat w %d dobach' % len(slepy), slepy[0][1].get('_nr'))
        potk = [(bi, d) for bi, d in prz if d.get('potkniecia')]
        if potk:
            al('ALARM', 'Przeplywy osad: potkniecia licznikow w %d dobach (suma %d)'
               % (len(potk), sum(d.get('potkniecia') for _, d in potk)), potk[0][1].get('_nr'))
        for nazwa in ('Przeplywy osad (kasy miast)', 'Przeplywy osad (kasy zamkow)', 'Przeplywy osad (kiesy wsi)'):
            ser = ses.ser(nazwa)
            if 'wsi' not in nazwa:
                zero = [(bi, d) for bi, d in ser if d.get('reg_tickow') == 0]
                if zero:
                    al('ALARM', '%s: regulator kasy 0 tickow w %d dobach - regulator siedzi w reszcie'
                       % (nazwa, len(zero)), zero[0][1].get('_nr'))
                malo = [(bi, d) for bi, d in ser if d.get('zakupy_tickow') is not None and d.get('osad')
                        and d['zakupy_tickow'] < TICKI_OSAD_MIN_UDZIAL * d['osad']]
                if malo:
                    al('uwaga', '%s: konsumpcja policzona w %d z %d osad (doba %d; %d dob ponizej %.0f%%) - tick osad dlawiony?'
                       % (nazwa, malo[0][1]['zakupy_tickow'], malo[0][1]['osad'], malo[0][0] + 1, len(malo),
                          100 * TICKI_OSAD_MIN_UDZIAL), malo[0][1].get('_nr'))
            zle = []
            for bi, d in ser:
                re_, ruch = d.get('reszta'), d.get('ruch')
                if re_ is None or abs(re_) < RESZTA_KAS_MIN_ABS:
                    continue
                if not ruch or 100.0 * abs(re_) / (ruch + abs(re_)) > RESZTA_KAS_PROC_RUCHU:
                    zle.append((abs(re_), re_, bi, d.get('_nr')))
            if zle:
                naj = max(zle)
                al('uwaga', '%s: reszta [R] ponad %.0f%% ruchu w %d dobach, najwiecej %s w dobie %d'
                   % (nazwa, RESZTA_KAS_PROC_RUCHU, len(zle), skr(naj[1], True), naj[2] + 1), naj[3])
        sk = ses.ser('Skarbce')
        uj = [(bi, d) for bi, d in sk if d.get('ujemnych')]
        if uj:
            al('ALARM', 'Skarbce: skarbiec ponizej zera w %d dobach (np. %s: %s)'
               % (len(uj), uj[0][1].get('min_krolestwo'), uj[0][1].get('min_skarbiec')), uj[0][1].get('_nr'))

    @regula
    def r_ludzie():
        for w in ses.linie_tematu.get('Ludzie (pominiete)') or []:
            al('ALARM', 'Ludzie: pominiete wiersze pliku regionow: %s' % obetnij(w.tresc, 80), w.nr)
            break
        lu = ses.ser('Ludzie')
        potk = [(bi, d) for bi, d in lu if d.get('potkniecia')]
        if potk:
            al('ALARM', 'Ludzie: potkniecia ksiegi w %d dobach (np. %d)' % (len(potk), potk[0][1].get('potkniecia')),
               potk[0][1].get('_nr'))
        niesk = [(bi, d) for bi, d in lu if d.get('nieskalibrowana')]
        if niesk:
            al('uwaga', 'Ludzie: ludnosc nieskalibrowana w %d dobach (PopulationLaw wylaczone albo przed rentami)'
               % len(niesk), niesk[0][1].get('_nr'))
        ln = dict(ses.ser('Ludnosc'))
        for bi, d in lu:
            a, b = d.get('ludnosc_mln'), (ln.get(bi) or {}).get('mln')
            if a is not None and b is not None and abs(a - b) > LUDNOSC_ROZJAZD_MLN:
                al('uwaga', 'Ludzie: ludnosc %.3f mln wobec %.1f mln w linii "Ludnosc:" (doba %d)' % (a, b, bi + 1),
                   d.get('_nr'))
                break

    @regula
    def r_jednostka():
        # ogniwo 108: odcinek "hearth za ludzi dzis" w linii "Ludzie:" (PeopleUnit)
        lu = [(bi, d) for bi, d in ses.ser('Ludzie') if d.get('jednostka')]
        if not lu:
            return
        wyl = [(bi, d) for bi, d in lu if d.get('jednostka') == 'WYLACZONA']
        if wyl:
            al('uwaga', 'Ludzie: jednostka ludzi WYLACZONA (%d dob) - wsie oddaja hearth po stawkach gry (ustawienie People Unit '
               'Enabled)' % len(wyl), wyl[0][1].get('_nr'))
        potk = [(bi, d) for bi, d in lu if (d.get('hz_potk') or 0) > 0]
        if potk:
            al('ALARM', 'Ludzie: potkniecia w odcinku "hearth za ludzi dzis" w %d dobach (np. %d)'
               % (len(potk), potk[0][1].get('hz_potk')), potk[0][1].get('_nr'))
        for klucz, opis in (('hz_niezgodne', 'niezgodne z formula gry i zostawione'),
                            ('hz_bez_k', 'bez przelicznika kultury (ludzie taborow)')):
            zle = [(bi, d) for bi, d in lu if (d.get(klucz) or 0) > 0]
            if zle:
                al('uwaga', 'Ludzie: "hearth za ludzi dzis" - %s: %d dob, najwiecej %d (inny mod albo inna wersja gry zmienia '
                   'te same liczby)' % (opis, len(zle), max(d.get(klucz) for _, d in zle)), zle[0][1].get('_nr'))
        czynne = [(bi, d) for bi, d in lu if d.get('jednostka') == 'czynna']
        if len(czynne) >= TABORY_ZERO_DOB and all((d.get('hz_tabory_ludzi') or 0) == 0 for _, d in czynne) \
                and any((d.get('tabory_wsi') or 0) > 0 for _, d in czynne):
            al('uwaga', 'Ludzie: "tabory wsi i lodzie ... za 0 ludzi" przez %d dob przy %s ludzi w taborach wsi - latka taborow '
               'nie jest wolana?' % (len(czynne), skr(czynne[-1][1].get('tabory_wsi'))), czynne[-1][1].get('_nr'))

    @regula
    def r_przyrost():
        # ogniwo 109: linia "Ludzie: przyrost naturalny" i ludnosc swiata
        jest109 = ma_ogniwo(ses, '109')
        pr = ses.ser('Ludzie (przyrost)')
        w = ses.fakty.get('model_dobrobytu')
        if w is not None:
            m = re.search(r'postfiksy w kolejnosci biegu: (.*?); prefiksy', w.tresc)
            lancuch = [x.strip() for x in m.group(1).split(' -> ')] if m else []
            if lancuch and 'PopulationLaw.GrowthPostfix' not in lancuch[-1]:
                al('ALARM', 'PopulationLaw: GrowthPostfix nie biegnie ostatni na CalculateHearthChange (ostatni: %s) - wynik '
                   'przyrostu moze byc nadpisany' % obetnij(lancuch[-1], 60), w.nr)
            if 'dopisuje PO naszej latce' in w.tresc:
                al('uwaga', 'PopulationLaw: czynny model dobrobytu dopisuje hearth PO naszej latce (wynik trzymaja limity) - %s'
                   % wycinek(w.tresc, 60), w.nr)
        if not pr:
            return
        wyl = [(bi, d) for bi, d in pr if d.get('wylaczony')]
        if wyl:
            al('uwaga', 'Ludzie: przyrost naturalny WYLACZONY w %d dobach - hearth wsi liczy gra (np. %s ludzi dzis)'
               % (len(wyl), skr(wyl[0][1].get('gra_ludzi'), True)), wyl[0][1].get('_nr'))
        poza = [(bi, d) for bi, d in pr if d.get('ludzi') is not None
                and not (PRZYROST_MIN_LUDZI <= d['ludzi'] <= PRZYROST_MAKS_LUDZI)]
        if poza:
            naj = max(poza, key=lambda x: abs(x[1]['ludzi']))
            al('ALARM', 'Ludzie: przyrost naturalny poza przedzialem %d-%d ludzi dziennie w %d z %d dob (np. %s w dobie %d; '
               'oczekiwane 250-600)' % (PRZYROST_MIN_LUDZI, PRZYROST_MAKS_LUDZI, len(poza), len(pr),
                                        skr(naj[1]['ludzi'], True), naj[0] + 1), naj[1].get('_nr'))
        gz = [(bi, d) for bi, d in pr if d.get('g_srednio') is not None
              and not (PRZYROST_G_MIN <= d['g_srednio'] <= PRZYROST_G_MAKS)]
        if gz and not poza:
            al('uwaga', 'Ludzie: przyrost naturalny - "g srednio" %s%% rocznie poza %.1f-%.1f w %d dobach (oczekiwane 0.2-0.5)'
               % (skr(gz[0][1]['g_srednio'], True), PRZYROST_G_MIN, PRZYROST_G_MAKS, len(gz)), gz[0][1].get('_nr'))
        potk = [(bi, d) for bi, d in pr if (d.get('potk_rachunek') or 0) + (d.get('potk_linia') or 0) > 0]
        if potk:
            al('ALARM', 'Ludzie: przyrost naturalny - potkniecia (rachunek wsi %s, linia dobowa %s) w %d dobach'
               % (potk[0][1].get('potk_rachunek'), potk[0][1].get('potk_linia'), len(potk)), potk[0][1].get('_nr'))
        rz = [(bi, d) for bi, d in pr if d.get('reszta') is not None and abs(d['reszta']) > PRZYROST_RESZTA_HEARTH]
        if rz:
            naj = max(rz, key=lambda x: abs(x[1]['reszta']))
            plus = naj[1]['reszta'] > 0
            # od ogniwa 113 (czynne spustoszenie) rabunki i zerowanie armii sa w "ruch ludzi" - ujemna RESZTA to cos innego
            al('uwaga', 'Ludzie: przyrost naturalny - RESZTA hearth wsi ponad %.0f na dobe w %d dobach, najwiecej %s (doba %d) - %s'
               % (PRZYROST_RESZTA_HEARTH, len(rz), skr(naj[1]['reszta'], True), naj[0] + 1,
                  'hearth z niczego: inwestycje BetterEconomy (13 kluczy nie zamknietych)?' if plus else (
                      'hearth w nicosc mimo czynnego spustoszenia: rabunek poza latka (wies bez przelicznika, dno 10 hearth)?'
                      if naj[1].get('spust_w_ruchu') else
                      'hearth w nicosc: rabunki, marsz armii (spalona wies nie odrasta do kroku 4)')), naj[1].get('_nr'))
        uch = [(bi, d) for bi, d in pr if d.get('uch_ludzi') is not None and d.get('ludzi') is not None
               and d['uch_ludzi'] > max(0, d['ludzi'])]
        if uch:
            naj = max(uch, key=lambda x: x[1]['uch_ludzi'])
            al('uwaga', 'Ludzie: powrot uchodzcow %s ludzi dziennie w %s wsiach przy dnie - wiecej niz caly przyrost naturalny '
               '(%s); regula "+0.5 hearth ponizej 40" to ludzie z niczego do kroku 4 (%d dob)'
               % (skr(naj[1]['uch_ludzi'], True), skr(naj[1].get('uch_wsi')), skr(naj[1]['ludzi'], True), len(uch)),
               naj[1].get('_nr'))
        bezk = [(bi, d) for bi, d in pr if (d.get('bez_k') or 0) > 0]
        if bezk:
            al('uwaga', 'Ludzie: przyrost naturalny - wsi bez przelicznika ludzi: %d (ich przyrost nie wchodzi do sumy ludzi)'
               % bezk[-1][1].get('bez_k'), bezk[-1][1].get('_nr'))
        zam_ = [(bi, d) for bi, d in pr if d.get('zamrozone') == 1 and d.get('miasta_mln') is not None]
        if len(zam_) >= 2:
            stany = [d['miasta_mln'] for _, d in zam_]
            if max(stany) - min(stany) > 0.0015:
                al('ALARM', 'Ludzie: ludnosc miast jest stanem (zamrozona), a zmienia sie: %.3f -> %.3f mln (od %.3f do %.3f)'
                   % (stany[0], stany[-1], min(stany), max(stany)), zam_[-1][1].get('_nr'))
        odm = [(bi, d) for bi, d in pr if d.get('zamrozone') == 0 and not d.get('miasta_nieskalibrowane')]
        if odm:
            al('uwaga', 'Ludzie: ludnosc miast liczona z dobrobytu (Town People Frozen wylaczone) w %d dobach' % len(odm),
               odm[0][1].get('_nr'))
        if not jest109:
            return
        # ludnosc swiata: po ogniwie 109 koniec wzrostu o 0.2-0.35% dziennie
        lu = ses.ser('Ludzie')
        skoki = []
        for bi, d in lu:
            zm, mln = d.get('ludnosc_zm'), d.get('ludnosc_mln')
            if zm is not None and mln:
                pc = 100.0 * abs(zm) / (mln * 1e6)
                if pc > LUDNOSC_MAKS_ZMIANA_PROC:
                    skoki.append((pc, zm, bi, d.get('_nr')))
        if skoki:
            naj = max(skoki)
            al('ALARM', 'Ludzie: ludnosc swiata zmienia sie o ponad %.2f%% dziennie w %d dobach, najwiecej %s ludzi = %.3f%% '
               '(doba %d) - przy ogniwie 109 ma stac' % (LUDNOSC_MAKS_ZMIANA_PROC, len(skoki), skr(naj[1], True), naj[0],
                                                         naj[2] + 1), naj[3])
        ln = [(bi, d) for bi, d in ses.ser('Ludnosc') if d.get('mln')]
        if len(ln) >= 2 and not skoki:
            a, b = ln[0], ln[-1]
            dob = max(1, b[0] - a[0])
            pc = 100.0 * abs(b[1]['mln'] - a[1]['mln']) / a[1]['mln'] / dob
            if pc > LUDNOSC_MAKS_ZMIANA_PROC:
                al('ALARM', '"Ludnosc:" zmienia sie o %.3f%% dziennie (%.1f -> %.1f mln w %d dob; prog %.2f%%) - przy ogniwie '
                   '109 ma stac' % (pc, a[1]['mln'], b[1]['mln'], dob, LUDNOSC_MAKS_ZMIANA_PROC), b[1].get('_nr'))

    @regula
    def r_paser():
        pa = ses.ser('Paser')
        if not pa:
            return
        kurek = [(bi, d) for bi, d in pa if d.get('kurek_otwarty')]
        if kurek:
            al('ALARM', 'Paser: KUREK OTWARTY - gra dopisuje bandom i kryjowkom zloto z niczego (%d dob, np. %s zl)'
               % (len(kurek), skr(kurek[0][1].get('zabl_razem'))), kurek[0][1].get('_nr'))
        wyl = [(bi, d) for bi, d in pa if d.get('skup_wylaczony')]
        if wyl:
            al('uwaga', 'Paser: skup lupu WYLACZONY w ustawieniach (%d dob)' % len(wyl), wyl[0][1].get('_nr'))
        zyw = [(bi, d) for bi, d in pa if d.get('kurek_zywnosci')]
        if zyw:
            al('uwaga', 'Paser: kurek zywnosci z niczego OTWARTY w ustawieniach (%d dob) - gra daje zywnosc kazdej nowej bandzie, '
               'paser jej nie bierze' % len(zyw), zyw[0][1].get('_nr'))
        obieg = [(bi, d) for bi, d in pa if d.get('obieg_wylaczony')]
        if obieg:
            al('uwaga', 'Paser: OBIEG KAS kryjowek WYLACZONY (%d dob) - kiesy startowe nowych band z niczego: %s zl'
               % (len(obieg), skr(sum(d.get('start_z_niczego') or 0 for _, d in obieg))), obieg[0][1].get('_nr'))
        potk = [(bi, d) for bi, d in pa if (d.get('potk_razem') or 0) > 0]
        if potk:
            d = potk[0][1]
            al('ALARM', 'Paser: potkniecia (skup %s, kryjowki %s, zycie %s, awanse %s) w %d dobach'
               % (d.get('potk_skup'), d.get('potk_kryjowki'), skr(d.get('potk_zycie')), skr(d.get('potk_awanse')), len(potk)),
               d.get('_nr'))
        # decyzja 06.10: paser dla 100% band - kazda banda z ladunkiem ma miec pasera i sprzedac ladunek
        bez = [(bi, d) for bi, d in pa if (d.get('bez_pasera') or 0) > 0]
        if bez:
            naj = max(bez, key=lambda x: x[1]['bez_pasera'])
            al('ALARM', 'Paser: "z paserem" mniej niz "band z ladunkiem" w %d z %d dob, najwiecej %d z %d band bez pasera (doba %d) - '
               'paser ma obslugiwac 100%% band' % (len(bez), len(pa), naj[1]['bez_pasera'], naj[1]['z_ladunkiem'], naj[0] + 1),
               naj[1].get('_nr'))
        ponad = [(bi, d) for bi, d in pa if d.get('sprzedalo') is not None and d.get('z_ladunkiem') is not None
                 and d['sprzedalo'] > d['z_ladunkiem']]
        if ponad:
            al('ALARM', 'Paser: "sprzedalo" %d wiecej niz "band z ladunkiem" %d (doba %d) - licznik liczy bande dwa razy'
               % (ponad[0][1]['sprzedalo'], ponad[0][1]['z_ladunkiem'], ponad[0][0] + 1), ponad[0][1].get('_nr'))
        ile, pocz = najdluzsza_seria([(d.get('zostalo') or 0) > 0 for _, d in pa])
        if ile >= PASER_ZOSTALO_DOB:
            ost = pa[pocz + ile - 1][1]
            al('ALARM', 'Paser: "z ladunkiem zostalo" ponad 0 przez %d dob z rzedu (od doby %d; ostatnio %d band, %s szt.: kasy '
               'miast przy rezerwie %s, brak otwartego miasta %s)' % (ile, pa[pocz][0] + 1, ost.get('zostalo'),
                                                                      skr(ost.get('zostalo_szt')), skr(ost.get('zost_rezerwa')),
                                                                      skr(ost.get('zost_brak_miasta'))), pa[pocz][1].get('_nr'))
        # ksiega kas osad ma liczyc ten sam skup i te same wydatki na zycie
        km = dict(ses.ser('Przeplywy osad (kasy miast)'))
        kz = dict(ses.ser('Przeplywy osad (kasy zamkow)'))
        skup_zle, zycie_zle = [], []
        for bi, d in pa:
            k = km.get(bi)
            if k is None:
                continue
            z = kz.get(bi) or {}
            if d.get('za_zl') is not None:
                skup = (k.get('paser_skup') or 0) + (z.get('paser_skup') or 0)
                if abs(skup) != d['za_zl']:
                    skup_zle.append((bi, d, skup))
            if d.get('zycie_razem') is not None:
                zycie = (k.get('zycie_band') or 0) + (z.get('zycie_band') or 0)
                if zycie != d['zycie_razem']:
                    zycie_zle.append((bi, d, zycie))
        if skup_zle:
            bi, d, skup = skup_zle[0]
            al('ALARM', 'Paser: skup za %d zl, a ksiega kas miast liczy "paser band (skup lupu)" %+d (%d dob z rozjazdem)'
               % (d['za_zl'], skup, len(skup_zle)), d.get('_nr'))
        if zycie_zle:
            bi, d, zycie = zycie_zle[0]
            al('ALARM', 'Paser: zycie w miastach %d zl (bandy %s + kryjowki %s), a ksiega kas liczy "bandy i kryjowki (zycie w '
               'miastach)" %+d (%d dob z rozjazdem)' % (d['zycie_razem'], skr(d.get('zycie_bandy')), skr(d.get('zycie_kryjowki')),
                                                       zycie, len(zycie_zle)), d.get('_nr'))
        wy = ses.ser('Wyrzutki')
        if len(wy) >= PASER_MIN_DOB_BEZ_AWANSOW and all((d.get('aw_paser') or 0) == 0 for _, d in wy):
            al('uwaga', 'Wyrzutki: "awanse: od pasera" = 0 przez %d dob mimo ogniwa 106 (bandy nie maja za co kupowac?)'
               % len(wy), wy[-1][1].get('_nr'))
        wej = [(bi, d) for bi, d in pa if (d.get('wejsc') or 0) > 0 and not d.get('kurek_otwarty')]
        if len(wej) >= 3 and all((d.get('zabl_razem') or 0) == 0 for _, d in wej):
            al('uwaga', 'Paser: wejscia band do kryjowek w %d dobach, a "zloto z niczego zablokowane" stale 0' % len(wej),
               wej[-1][1].get('_nr'))
        ost = pa[-1][1]
        if (ost.get('juki_zywnosc') or 0) > 0 and not ost.get('kurek_zywnosci'):
            al('uwaga', 'Paser: w jukach band "zywnosc poza skupem" %d szt. (przy zamknietym kurku zywnosci ma byc 0)'
               % ost['juki_zywnosc'], ost.get('_nr'))

    @regula
    def r_zold():
        zo = ses.ser('Zold')
        if ma_ogniwo(ses, '107') and len(ses.pelne()) >= 1:
            latki = [w for w in ws if w.pref == 'SoldierPay' and 'latki modelu finansow' in w.tresc]
            if not latki:
                al('ALARM', 'SoldierPay: brak linii "latki modelu finansow" mimo %d dob - latki na model nie zalozone?'
                   % len(ses.pelne()))
            if not zo and len(ses.pelne()) >= 2:
                al('ALARM', 'SoldierPay wpiete, a nie ma linii "Zold:" w %d dobach' % len(ses.pelne()))
        if not zo:
            return
        # decyzja 06.10: tarcza zoldu w kasach miast WLACZONA (ogniwo 107) - bez niej regulator kas kasuje ok. 81% zoldu miast.
        # Od ogniwa 111 (K6, doby z dopiskiem "zalogi bez zwrotu korony") regulator kas miast niczego nie kasuje: tarcza jest
        # zbedna i nieczynna, jej stan nie jest ani wada, ani zaleta - te doby nie wchodza do obu regul nizej.
        bez_tarczy = [(bi, d) for bi, d in zo if not d.get('tarcza_wl') and not d.get('k6')]
        if bez_tarczy:
            al('uwaga', 'Zold: tarcza zoldu w kasach miast wylaczona w %d z %d dob (od ogniwa 107 domyslnie wlaczona - ustawienie '
               'Town Wage Shield zapisane w MCM?)' % (len(bez_tarczy), len(zo)), bez_tarczy[0][1].get('_nr'))
        # K6 czynne, a tarcza dalej trzyma znaczniki albo cos "chroni" - dwa mechanizmy na tej samej kasie
        k6_znacznik = [(bi, d) for i, (bi, d) in enumerate(zo) if d.get('k6') and i > 0
                       and ((d.get('znacznik') or 0) > 0 or (d.get('nie_skasowal') or 0) > 0)]
        if k6_znacznik:
            d = k6_znacznik[0][1]
            al('uwaga', 'Zold: kasa miasta jest prawdziwym pieniadzem (K6), a tarcza zoldu dalej ma znacznik %s i "regulator nie '
               'skasowal dzis" %s (%d dob) - przy K6 ma byc 0 / 0' % (skr(d.get('znacznik')), skr(d.get('nie_skasowal')),
                                                                    len(k6_znacznik)), d.get('_nr'))
        z_tarcza = [(bi, d) for bi, d in zo if d.get('tarcza_czynna') and (d.get('do_miast') or 0) > 0]
        if len(z_tarcza) >= 2 and 'soldierpay_tarcza' not in ses.fakty:
            al('uwaga', 'Zold: tarcza wlaczona i zold plynie do kas miast (%d dob), a nie ma linii "SoldierPay: tarcza zoldu w '
               'kasach miast ... regulator kasy z ..." - latka na regulator nie zalozona?' % len(z_tarcza),
               z_tarcza[0][1].get('_nr'))
        slepe = [(bi, d) for bi, d in zo if (d.get('slepe') or 0) > 0]
        if slepe:
            al('ALARM', 'Zold: rody z pusta kiesa i nieznanym saldem w %d dobach (np. %d rodow, %s zl)'
               % (len(slepe), slepe[0][1].get('slepe'), skr(slepe[0][1].get('slepe_zl'))), slepe[0][1].get('_nr'))
        potk = [(bi, d) for bi, d in zo if (d.get('potk_powtorzone') or 0) + (d.get('potk_wyjatki') or 0) > 0]
        if potk:
            al('ALARM', 'Zold: potkniecia (powtorzone wyplaty %s, wyjatki %s) w %d dobach'
               % (potk[0][1].get('potk_powtorzone'), potk[0][1].get('potk_wyjatki'), len(potk)), potk[0][1].get('_nr'))
        roz = []
        for bi, d in zo:
            r, z = d.get('rozjazd'), d.get('zeszlo_razem')
            if r is not None and z and abs(r) > max(10, ZOLD_ROZJAZD_PROC / 100.0 * z):
                roz.append((abs(r), r, bi, d.get('_nr')))
        if roz:
            naj = max(roz)
            al('uwaga', 'Zold: "z kies zeszlo" rozni sie od sumy przekazane + nieprzekazane o %s (doba %d; %d dob ponad %.0f%%)'
               % (skr(naj[1], True), naj[2] + 1, len(roz), ZOLD_ROZJAZD_PROC), naj[3])
        if len(zo) >= 3:
            a, b = zo[0][1].get('sak_sieroty'), zo[-1][1].get('sak_sieroty')
            if a is not None and b is not None and b > a and b > 0:
                al('uwaga', 'Zold: sakiewki po partiach, ktorych juz nie ma, rosna: %s -> %s' % (skr(a), skr(b)),
                   zo[-1][1].get('_nr'))
            a, b = zo[0][1].get('dlug_rodow'), zo[-1][1].get('dlug_rodow')
            if a is not None and b is not None and b > a and b >= 5:
                al('uwaga', 'Zold: rodow z brakiem zapisanym jako dlug wobec korony przybywa: %d -> %d' % (a, b),
                   zo[-1][1].get('_nr'))
        if len(zo) > SAKIEWKA_ROSNIE_DOB:
            ogon = [d.get('sak_najwieksza') for _, d in zo[-(SAKIEWKA_ROSNIE_DOB + 1):]]
            if all(v is not None for v in ogon) and all(ogon[i] < ogon[i + 1] for i in range(len(ogon) - 1)):
                al('uwaga', 'Zold: najwieksza sakiewka rosnie %d dob z rzedu: %s -> %s'
                   % (SAKIEWKA_ROSNIE_DOB, skr(ogon[0]), skr(ogon[-1])), zo[-1][1].get('_nr'))
        prz = dict(ses.ser('Przeplywy osad'))
        roz = []
        for bi, d in zo:
            k = prz.get(bi)
            nal = d.get('naliczony_razem')
            if k is None or nal is None or not k.get('zold_razem'):
                continue
            ks = (k.get('zold_rody') or 0) + (k.get('zold_garnizony') or 0)
            if ks and abs(nal - ks) > ZOLD_KSIEGA_ROZJAZD_PROC / 100.0 * ks:
                roz.append((abs(nal - ks), nal, ks, bi, d.get('_nr')))
        if roz:
            naj = max(roz)
            al('uwaga', 'Zold: naliczony %s wobec licznika ksiegi pieniadza %s (partie rodow + garnizony), doba %d'
               % (skr(naj[1]), skr(naj[2]), naj[3] + 1), naj[4])
        # uklad 102b: bilans podaje, ile zoldu SoldierPay oddal do obiegu - ma to byc suma z linii "Zold:"
        bil = dict(ses.ser('Pieniadz swiata (bilans)'))
        roz = []
        for bi, d in zo:
            b = bil.get(bi)
            if b is None or b.get('oddany') is None:
                continue
            czesci = [d.get('do_sakiewek'), d.get('do_miast'), d.get('do_zamkow')]
            if any(v is None for v in czesci):
                continue
            razem = sum(czesci)
            if abs(razem - b['oddany']) > max(10, ZOLD_KSIEGA_ROZJAZD_PROC / 100.0 * max(razem, b['oddany'])):
                roz.append((abs(razem - b['oddany']), razem, b['oddany'], bi, d.get('_nr')))
        if roz:
            naj = max(roz)
            al('uwaga', 'Zold: do sakiewek i kas osad %s, a bilans "zold oddany do obiegu przez SoldierPay" %s (doba %d; %d dob '
               'z rozjazdem ponad %.0f%%)' % (skr(naj[1]), skr(naj[2]), naj[3] + 1, len(roz), ZOLD_KSIEGA_ROZJAZD_PROC), naj[4])
        zw = ses.ser('Korona (zwrot zoldu)')
        pusty = [(bi, d) for bi, d in zw if (d.get('pusty') or 0) > 0]
        if pusty:
            al('uwaga', 'Korona: zwrot zoldu - pusty skarbiec w %d krolestwach (doba %d)'
               % (pusty[-1][1].get('pusty'), pusty[-1][0] + 1), pusty[-1][1].get('_nr'))
        potk = [(bi, d) for bi, d in zw if (d.get('potkniecia') or 0) > 0]
        if potk:
            al('ALARM', 'Korona: zwrot zoldu - wyjatek przy %d krolestwach' % potk[0][1].get('potkniecia'),
               potk[0][1].get('_nr'))
        sk = ses.ser('Skarbce')
        if ma_ogniwo(ses, '107') and not zw and any((d.get('wojna') or 0) > 0 for _, d in sk) and len(ses.pelne()) >= 2:
            al('uwaga', 'krolestwa sa w wojnie, a nie ma linii "Korona: ... zwrot zoldu" (CrownWageRefundEnabled wylaczone?)')
        sa = ses.ser('Sakiewka ludzi')
        zle = []
        for i in range(1, len(sa)):
            p, d = sa[i - 1][1], sa[i][1]
            if sa[i][0] != sa[i - 1][0] + 1:
                continue
            if None in (p.get('razem'), d.get('razem'), d.get('wplynelo'), d.get('wyszlo')):
                continue
            r = (d['razem'] - p['razem']) - (d['wplynelo'] - d['wyszlo'])
            if r != 0:
                zle.append((abs(r), r, sa[i][0], d.get('_nr')))
        if zle:
            naj = max(zle)
            al('uwaga', 'Sakiewka ludzi: stan zmienil sie inaczej niz wplynelo - wyszlo o %s (doba %d; %d dob z rozjazdem)'
               % (skr(naj[1], True), naj[2] + 1, len(zle)), naj[3])

    def alarm_brakuje(ser, nazwa):
        """ "do zapasu brakuje" wyzsze niz przed trzema tygodniami i rosnace po drodze (paczka 110, ryzyko 2: kasy stoja
        ponizej zapasu kupcow dluzej niz 3-4 tygodnie -> ograniczyc zakupy sprzetu do nadwyzki ponad zapas)."""
        z = [(bi, d.get('brakuje'), d.get('_nr')) for bi, d in ser if isinstance(d.get('brakuje'), (int, float))]
        if len(z) < 3:
            return
        ost = z[-1]
        dawne = [x for x in z if x[0] <= ost[0] - KASY_BRAKUJE_ROSNIE_DOB]
        if not dawne:
            return
        pocz = dawne[-1]
        srodek = min(z, key=lambda x: abs(x[0] - (pocz[0] + ost[0]) // 2))
        wzrost = ost[1] - pocz[1]
        if wzrost >= max(KASY_BRAKUJE_MIN_ABS, KASY_BRAKUJE_MIN_WZROST_PROC / 100.0 * pocz[1]) and pocz[1] <= srodek[1] <= ost[1]:
            al('ALARM', '%s: "do zapasu brakuje" rosnie od ponad %d dob: %s -> %s -> %s (doby %d, %d, %d) - kasy stoja coraz '
               'nizej pod zapasem kupcow (paczka 110, ryzyko 2: zakupy sprzetu z zapasu)'
               % (nazwa, KASY_BRAKUJE_ROSNIE_DOB, skr(pocz[1]), skr(srodek[1]), skr(ost[1]), pocz[0] + 1, srodek[0] + 1,
                  ost[0] + 1), ost[2])

    @regula
    def r_kasy_zamkow():
        # ogniwo 110 (K5): linia "Kasy zamkow:", jednorazowe przyciecie daru startowego i ksiega kas zamkow
        f = ses.fakty
        w = f.get('kasy_zamkow_wyl')
        if w is not None:
            al('uwaga', 'Kasy zamkow: WYLACZONE w ustawieniach (Castle Purse Enabled) - regulator gry i "zakupy" z niczego jak '
               'przed ogniwem 110, bez daniny podzamcza', w.nr)
        for klucz, co in (('castle_trim_bez', 'zamkow'), ('town_trim_bez', 'miast')):
            w = f.get(klucz)
            if w is not None:
                al('uwaga', 'dar startowy kas %s BEZ przyciecia: %s' % (co, obetnij(w.tresc.split(': ', 1)[-1], 95)), w.nr)
        kz = ses.ser('Kasy zamkow')
        if not kz:
            return
        potk = [(bi, d) for bi, d in kz if (d.get('potkniecia') or 0) > 0]
        if potk:
            al('ALARM', 'Kasy zamkow: potkniecia (wyjatki) w %d dobach, np. %d' % (len(potk), potk[0][1].get('potkniecia')),
               potk[0][1].get('_nr'))
        alarm_brakuje(kz, 'Kasy zamkow')
        bk = [(bi, d) for bi, d in kz if (d.get('ponad_bk') or 0) > 0]
        if bk:
            al('uwaga', 'Kasy zamkow: zamki ponad limitem kasy BK w %d dobach (najwiecej %d zamkow) - BK kasuje tam 1%% kasy '
               'dziennie w nicosc (zamyka to ogniwo 111)' % (len(bk), max(d.get('ponad_bk') for _, d in bk)), bk[0][1].get('_nr'))
        # ksiega kas zamkow przy czynnej kasie zamku: "zakupy" +0, regulator +0, danina = pozycja ticku dobowego
        ks = dict(ses.ser('Przeplywy osad (kasy zamkow)'))
        nz = [(bi, ks[bi]) for bi, d in kz if bi in ks and ((ks[bi].get('zakupy') or 0) != 0 or (ks[bi].get('regulator') or 0) != 0)]
        if nz:
            bi, k = nz[0]
            al('ALARM', 'Przeplywy osad (kasy zamkow): przy czynnej kasie zamku "zakupy" %s i regulator %s (doba %d; %d dob) - '
               'maja byc +0, gra dalej tworzy albo kasuje zloto zamkow' % (skr(k.get('zakupy'), True), skr(k.get('regulator'), True),
                                                                          bi + 1, len(nz)), k.get('_nr'))
        roz = []
        for bi, d in kz:
            k = ks.get(bi)
            if k is None or d.get('danina') is None:
                continue
            ksiega = -(k.get('danina_podzamcza') or 0)
            if abs(ksiega - d['danina']) > max(10, 0.01 * d['danina']):
                roz.append((bi, d, ksiega))
        if roz:
            bi, d, ksiega = roz[0]
            al('uwaga', 'Kasy zamkow: danina podzamcza %s, a ksiega kas zamkow liczy "danina podzamcza" %s (doba %d; %d dob z '
               'rozjazdem)' % (skr(d['danina']), skr(-ksiega, True), bi + 1, len(roz)), d.get('_nr'))

    @regula
    def r_kasy_miast():
        # ogniwo 111 (K6): linia "Kasy miast:" i ksiega kas miast
        f = ses.fakty
        w = f.get('kasy_miast_wyl')
        if w is not None:
            if 'latki regulatora nie sa wpiete' in w.tresc:
                al('ALARM', 'Kasy miast: latki regulatora nie sa wpiete (CastlePurse: BRAK) - kasy miast jak przed ogniwem 111',
                   w.nr)
            else:
                al('uwaga', 'Kasy miast: WYLACZONE w ustawieniach (Town Purse Regulator 0) - regulator gry kasuje nadwyzki, '
                   '"zakupy" mieszczan z niczego, tarcza zoldu jak przed ogniwem 111', w.nr)
        km = ses.ser('Kasy miast')
        if not km:
            return
        potk = [(bi, d) for bi, d in km if (d.get('potkniecia') or 0) > 0]
        if potk:
            al('ALARM', 'Kasy miast: potkniecia (wyjatki) w %d dobach, np. %d' % (len(potk), potk[0][1].get('potkniecia')),
               potk[0][1].get('_nr'))
        # "dosypal z niczego": jedyne zloto z niczego po stronie miast (tryb 1: bezpiecznik ponizej zapasu kupcow)
        dos = [(bi, d) for bi, d in km if (d.get('dosypal') or 0) > 0]
        if dos:
            wart = [d['dosypal'] for _, d in km if isinstance(d.get('dosypal'), (int, float))]
            med, naj = mediana(wart), max(dos, key=lambda x: x[1]['dosypal'])
            tryb2 = [1 for _, d in dos if (d.get('tryb') or 0) >= 2]
            ponad = med is not None and med > KASY_MIAST_DOSYPKA_MAKS
            al('ALARM' if (ponad or tryb2) else 'uwaga',
               'Kasy miast: regulator gry "dosypal z niczego" w %d z %d dob - mediana %s, najwiecej %s (doba %d), razem %s zlota z '
               'niczego%s' % (len(dos), len(km), skr(med), skr(naj[1]['dosypal']), naj[0] + 1, skr(sum(wart)),
                              ' - w trybie regulatora 2 ma byc 0' if tryb2 else (
                                  ' - ponad szacunek paczki (50-%dk na dobe)' % (KASY_MIAST_DOSYPKA_MAKS // 1000) if ponad else
                                  ' (kurek trybu 1: miasta ponizej zapasu kupcow; szacunek paczki 50-%dk na dobe)'
                                  % (KASY_MIAST_DOSYPKA_MAKS // 1000))), naj[1].get('_nr'))
        zost = [(bi, d) for bi, d in km if d.get('zakupy_zostaja')]
        if zost:
            al('ALARM', 'Kasy miast: "zakupy" mieszczan - zloto z niczego ZOSTAJE w kasach (Town Folk Buy Without Minting '
               'wylaczone; %d dob) i zaworem plynie do panow i korony' % len(zost), zost[0][1].get('_nr'))
        nd = [(bi, d) for bi, d in km if d.get('zawor_nie_dziala')]
        if nd:
            al('ALARM', 'Kasy miast: zawor NIE DZIALA - renty od ludnosci wylaczone (Population Rent Enabled; %d dob), kasy '
               'miast tylko rosna' % len(nd), nd[0][1].get('_nr'))
        bkz = [(bi, d) for bi, d in km if d.get('bk') in ('BRAK latki', 'zostaje')]
        if bkz:
            brak = bkz[0][1].get('bk') == 'BRAK latki'
            al('ALARM' if brak else 'uwaga', 'Kasy miast: gorna galaz BK (1%% kasy dziennie w nicosc) - %s (%d dob)'
               % ('BRAK latki na BK HandleMarketGold' if brak else 'zostaje (ustawienie Town Purse No Bk Skim)', len(bkz)),
               bkz[0][1].get('_nr'))
        biedne = [(bi, d) for bi, d in km if (d.get('ponizej_1000') or 0) > 0]
        if biedne:
            al('uwaga', 'Kasy miast: miast z kasa ponizej 1000 - %d dob, najwiecej %d miast (miasto bez kasy nie kupuje od wsi '
               'i karawan)' % (len(biedne), max(d.get('ponizej_1000') for _, d in biedne)), biedne[0][1].get('_nr'))
        alarm_brakuje(km, 'Kasy miast')
        rz = [(bi, d) for bi, d in km if abs(d.get('zawor_rozjazd') or 0) > 2]
        if rz:
            bi, d = rz[0]
            al('uwaga', 'Kasy miast: zawor - zeszlo %s, a panom %s + skarbcom %s (roznica %s; %d dob)'
               % (skr(d.get('zeszlo')), skr(d.get('panom')), skr(d.get('koronie')), skr(d.get('zawor_rozjazd'), True), len(rz)),
               d.get('_nr'))
        # ksiega kas miast przy czynnym K6: "zakupy" +0 (gdy cofanie wlaczone), "skasowal 0"
        ks = dict(ses.ser('Przeplywy osad (kasy miast)'))
        kas = [(bi, ks[bi]) for bi, d in km if bi in ks and (ks[bi].get('skasowal') or 0) != 0]
        if kas:
            bi, k = kas[0]
            al('ALARM', 'Przeplywy osad (kasy miast): przy czynnej kasie miasta regulator "skasowal" %s (doba %d; %d dob) - ma '
               'byc 0, gra dalej kasuje nadwyzki kas miast' % (skr(k.get('skasowal')), bi + 1, len(kas)), k.get('_nr'))
        zak = [(bi, ks[bi]) for bi, d in km if bi in ks and not d.get('zakupy_zostaja') and (ks[bi].get('zakupy') or 0) != 0]
        if zak:
            bi, k = zak[0]
            al('ALARM', 'Przeplywy osad (kasy miast): przy czynnej kasie miasta "zakupy" mieszkancow %s (doba %d; %d dob) - maja '
               'byc +0, gra dalej tworzy zloto za zjedzony towar' % (skr(k.get('zakupy'), True), bi + 1, len(zak)), k.get('_nr'))

    @regula
    def r_utarg_wsi():
        # ogniwo 112 (K7): linia "Utarg wsi:" - nic nie moze znikac ani zostac nieprzypisane
        f = ses.fakty
        w = f.get('utarg_latki')
        if w is not None:
            if 'na tej metodzie: BRAK' in w.tresc:
                al('ALARM', 'Utarg wsi (latki): na Village.DailyTick nie ma latki innego moda - gra obcina kiese wsi do 1000 co '
                   'dobe, dopisany utarg zniknie nastepnej doby', w.nr)
            if 'blad odczytu' in w.tresc:
                al('uwaga', 'Utarg wsi (latki): %s' % obetnij(w.tresc, 100), w.nr)
        ut = ses.ser('Utarg wsi')
        if not ut:
            return
        for klucz, co, pole in (('powroty_stan', 'powroty taborow (reszta utargu do kiesy wsi)', None),
                                ('zywnosc_stan', 'zywnosc kupiona we wsiach', 'zyw_zniklo'),
                                ('sakwy_stan', 'sakwy zniszczonych taborow', 'tab_zniklo')):
            nw = [(bi, d) for bi, d in ut if d.get(klucz) == 'nie wpieta']
            if nw:
                al('ALARM', 'Utarg wsi: %s - latka nie jest wpieta (%d dob)' % (co, len(nw)), nw[0][1].get('_nr'))
            wyl = [(bi, d) for bi, d in ut if d.get(klucz) == 'WYLACZONE']
            if wyl:
                zn = sum((d.get(pole) or 0) for _, d in wyl) if pole else None
                al('uwaga', 'Utarg wsi: %s - WYLACZONE w ustawieniach (%d dob)%s' % (
                    co, len(wyl), (', zniklo razem %s' % skr(zn)) if zn is not None else
                    '; ile znika, mierzy linia "Przeplywy osad:"'), wyl[0][1].get('_nr'))
        for pole, co, stan in (('nieprzypisane', '"nieprzypisane" (powroty taborow)', 'powroty_stan'),
                               ('zyw_zniklo', '"zniklo" przy zywnosci kupionej we wsiach', 'zywnosc_stan'),
                               ('tab_zniklo', '"zniklo" przy sakwach zniszczonych taborow', 'sakwy_stan')):
            zle = [(bi, d) for bi, d in ut if d.get(stan) == 'czynne' and (d.get(pole) or 0) != 0]
            if zle:
                naj = max(zle, key=lambda x: abs(x[1][pole]))
                al('ALARM', 'Utarg wsi: %s niezerowe w %d z %d dob, najwiecej %s (doba %d), razem %s - K7 ma oddawac wszystko'
                   % (co, len(zle), len(ut), skr(naj[1][pole]), naj[0] + 1, skr(sum(d[pole] for _, d in zle))), naj[1].get('_nr'))
        wyj = [(bi, d) for bi, d in ut if (d.get('wyjatki') or 0) > 0]
        if wyj:
            al('ALARM', 'Utarg wsi: wyjatki zlapane w %d dobach (np. %d)' % (len(wyj), wyj[0][1].get('wyjatki')),
               wyj[0][1].get('_nr'))
        okna = [(bi, d) for bi, d in ut if (d.get('okna') or 0) > 0]
        if okna:
            al('uwaga', 'Utarg wsi: okna niedomkniete w %d dobach (razem %d) - powrot taboru bez rozliczenia'
               % (len(okna), sum(d.get('okna') for _, d in okna)), okna[0][1].get('_nr'))
        bezbk = [(bi, d) for bi, d in ut if (d.get('bez_podatku_bk') or 0) > 0]
        if bezbk:
            d = bezbk[-1][1]
            al('uwaga', 'Utarg wsi: powroty bez podatku BK w %d dobach (razem %d, licznikom panow dopisano %s) - BK nie zna tych '
               'wsi: %s' % (len(bezbk), sum(x.get('bez_podatku_bk') for _, x in bezbk),
                            skr(sum((x.get('dopisane_panom') or 0) for _, x in bezbk)), obetnij(d.get('bez_podatku_wsie') or '?', 40)),
               d.get('_nr'))
        # "Przeplywy osad: ... + zniklo N [R]" ma przy czynnym K7 spasc do zera
        prz = dict(ses.ser('Przeplywy osad'))
        zn = [(bi, prz[bi]) for bi, d in ut if d.get('powroty_stan') == 'czynne' and bi in prz and (prz[bi].get('zniklo') or 0) != 0]
        if zn:
            bi, p = max(zn, key=lambda x: abs(x[1]['zniklo']))
            al('ALARM', 'Przeplywy osad: przy czynnym K7 z utargu taborow wsi nadal "zniklo" %s [R] (doba %d; %d dob)'
               % (skr(p['zniklo'], True), bi + 1, len(zn)), p.get('_nr'))

    @regula
    def r_spustoszenie():
        # ogniwo 113: odcinek "spustoszenie dzis" w "Ludzie:" i linia "Ludzie (spustoszenie):"
        f = ses.fakty
        w = f.get('spustoszenie_wyl')
        if w is not None:
            al('uwaga', obetnij(w.tresc, 120), w.nr)
        lu = ses.ser('Ludzie')
        wyl = [(bi, d) for bi, d in lu if d.get('spustoszenie') == 'WYLACZONE']
        if wyl:
            al('uwaga', 'Ludzie: spustoszenie WYLACZONE w %d dobach - rabunek i zerowanie zdejmuja hearth jak po kroku 3 '
               '(-39.7%% hearth za rabunek, +0.5 hearth ponizej progu)' % len(wyl), wyl[0][1].get('_nr'))
        cz = [(bi, d) for bi, d in lu if d.get('spustoszenie') == 'czynne']
        potk = [(bi, d) for bi, d in cz if (d.get('sp_potk') or 0) > 0]
        if potk:
            al('ALARM', 'Ludzie: potkniecia w odcinku "spustoszenie dzis" w %d dobach (np. %d)'
               % (len(potk), potk[0][1].get('sp_potk')), potk[0][1].get('_nr'))
        roz = [(bi, d) for bi, d in cz if abs(d.get('sp_rozjazd') or 0) > max(1.0, 0.01 * (d.get('sp_zdjeci') or 0))]
        if roz:
            bi, d = roz[0]
            al('uwaga', 'Ludzie: spustoszenie - zdjeci z wsi %s, a zabici + w las + uchodzcy = %s (doba %d; %d dob z rozjazdem)'
               % (skr(d.get('sp_zdjeci')), skr(round((d.get('sp_zdjeci') or 0) - (d.get('sp_rozjazd') or 0), 1)), bi + 1,
                  len(roz)), d.get('_nr'))
        # zdjeci rabunkiem rzedu dziesiatek tysiecy ludzi = tyle, ile zdejmowala gra (-39.7% hearth): latka nie ogranicza
        sp = ses.ser('Ludzie (spustoszenie)')
        zle = []
        for bi, d in sp:
            na, lud, gra = d.get('na_rabunek'), d.get('ludzi_rab'), d.get('gra_ludzi')
            if not isinstance(lud, (int, float)) or lud <= 0:
                continue
            if (na is not None and na >= SPUSTOSZENIE_NA_RABUNEK_ALARM) or (
                    gra and gra >= SPUSTOSZENIE_GRA_MIN_LUDZI and lud >= SPUSTOSZENIE_UDZIAL_GRY_ALARM * gra):
                zle.append((lud, bi, d))
        widziane = set(bi for _, bi, _ in zle)
        for bi, d in cz:
            lud, wsi = d.get('sp_rabunki'), d.get('sp_rab_wsi')
            if bi not in widziane and isinstance(lud, (int, float)) and lud / float(max(1, wsi or 1)) >= SPUSTOSZENIE_NA_RABUNEK_ALARM:
                zle.append((lud, bi, d))
        if zle:
            lud, bi, d = max(zle, key=lambda x: x[0])
            al('ALARM', 'spustoszenie: rabunek wygnal z wsi %s ludzi (doba %d%s; %d dob) - rzad dziesiatek tysiecy to tyle, ile '
               'zdejmowala gra; oczekiwane setki ludzi na rabunek - latka spustoszenia nie dziala'
               % (skr(lud), bi + 1, ('; gra zdjelaby %s' % skr(d.get('gra_ludzi'))) if d.get('gra_ludzi') else '', len(zle)),
               d.get('_nr'))
        # hearth zdjety przez gre w kroku rabunku ma wrocic do wsi co do bitu - inaczej siedzi w RESZCIE linii przyrostu
        pr = dict(ses.ser('Ludzie (przyrost)'))
        nw = []
        for bi, d in sp:
            p = pr.get(bi)
            gh = d.get('gra_hearth')
            if p is None or not isinstance(gh, (int, float)) or gh < 20 or p.get('reszta') is None:
                continue
            if p['reszta'] <= -0.5 * gh:
                nw.append((bi, d, p))
        if nw:
            bi, d, p = nw[0]
            al('ALARM', 'spustoszenie: w dobie rabunku RESZTA hearth wsi %s przy "gra zdjelaby %s hearth" (doba %d; %d dob) - '
               'hearth zdjety przez gre nie wrocil do wsi' % (skr(p['reszta'], True), skr(d.get('gra_hearth')), bi + 1,
                                                             len(nw)), p.get('_nr'))
        for klucz, co in (('bez_k', 'bez przelicznika ludzi'), ('obcy_hearth', 'hearth dopisany przez kogos w srodku kroku '
                                                                               'rabunku'),
                          ('podjete', 'rabunki podjete po wczytaniu gry')):
            z = [(bi, d) for bi, d in sp if (d.get(klucz) or 0) > 0]
            if z:
                al('uwaga', 'Ludzie (spustoszenie): dopisek "%s" w %d dobach (najwiecej %s) - mial sie nie pojawiac'
                   % (co, len(z), skr(max(d.get(klucz) for _, d in z))), z[0][1].get('_nr'))
        zer = [d.get('osobodni_zer') for _, d in sp if isinstance(d.get('osobodni_zer'), (int, float))]
        if zer and mediana(zer) >= ZEROWANIE_OSOBODNI_UWAGA:
            al('uwaga', 'Ludzie (spustoszenie): osobodni zerowania armii - mediana %s na dobe (prog %s): uchodzcow przybedzie '
               'ponad 1%% ludzi wsi rocznie; Devastation Per Forager Day w dol albo Forage Radius'
               % (skr(mediana(zer)), skr(ZEROWANIE_OSOBODNI_UWAGA)), sp[-1][1].get('_nr'))
        stara = [(bi, p) for bi, p in pr.items() if p.get('spust_w_ruchu') and (p.get('uch_regula_wsi') or 0) > 0]
        if stara:
            bi, p = stara[0]
            al('uwaga', 'Ludzie: przy czynnym spustoszeniu regula "powrot uchodzcow +0.5 hearth" objela %d wsi (doba %d; %d dob) '
               '- ma byc 0' % (p.get('uch_regula_wsi'), bi + 1, len(stara)), p.get('_nr'))

    @regula
    def r_towary2():
        # ogniwa 115-119 (grupa 2b): reguly z "Po czym poznac w logu" docs\paczki\115..119 - tylko, gdy ich linie sa w logu
        n = len(ses.pelne()) if ses.bloki else 0
        ru = ses.ser('Ruda')
        if (ma_ogniwo(ses, '119') or ma_ogniwo(ses, '117')) and ru and n >= TOWARY2_MIN_DOB:
            d = ru[-1][1]
            b = d.get('bez_towaru')
            if b is not None and b > RUDA_BEZ_TOWARU_ALARM_2B:
                al('ALARM', 'GLOWNA LICZBA grupy 2b: "Ruda: miast bez towaru" %d po %d dobach (prog %d; oczekiwane ok. 25-30, test 14:08: '
                   '74 -> 69) - wozy i karawany nie dowoza rudy do miast bez rudy' % (b, n, RUDA_BEZ_TOWARU_ALARM_2B), d.get('_nr'))
        wz = ses.ser('Dowoz (wozy)')
        zle = [(bi, d) for bi, d in wz if (d.get('niezgodne') or 0) > 0]
        if zle:
            al('ALARM', 'Dowoz (wozy): "niezgodne" %d w %d dobach (ma byc 0) - korekta ceny ladunku nie dziala, ktos zmienia cene osady '
               'w trakcie sprzedazy' % (sum(d.get('niezgodne') for _, d in zle), len(zle)), zle[0][1].get('_nr'))
        ros = [(bi, d) for bi, d in wz if (d.get('cena_rosla') or 0) > 0]
        if ros:
            al('uwaga', 'Dowoz (wozy): "cena rosla" %d w %d dobach (ma byc 0)' % (sum(d.get('cena_rosla') for _, d in ros), len(ros)),
               ros[0][1].get('_nr'))
        pot = [(bi, d) for bi, d in wz if d.get('potkn')]
        if pot:
            al('ALARM', 'Dowoz (wozy): POTKNIECIA %d w %d dobach (wyjatek przy taborze; pierwszy z kazdego miejsca jest w ERROR)'
               % (sum(d.get('potkn') for _, d in pot), len(pot)), pot[0][1].get('_nr'))
        bl = [(bi, d) for bi, d in wz if d.get('cena_stan') == 'BRAK LATKI']
        if bl:
            al('ALARM', 'Dowoz (wozy): cena ladunku sztuka po sztuce - BRAK LATKI (%d dob): tabory dalej po cenie pierwszej sztuki za '
               'caly ladunek' % len(bl), bl[0][1].get('_nr'))
        wyl = [(bi, d) for bi, d in wz if d.get('wybor') != 'CZYNNY' or d.get('cena_stan') == 'WYLACZONA']
        if wyl:
            al('uwaga', 'Dowoz (wozy): wybor miasta albo cena ladunku WYLACZONE w ustawieniach (%d dob)' % len(wyl),
               wyl[0][1].get('_nr'))
        ms = _wart(wz, 'ms')
        if ms and mediana(ms) > WOZY_MS_MAKS:
            al('uwaga', 'Dowoz (wozy): wyceny wozow %s ms na dobe (mediana; prog %s, oczekiwane kilkadziesiat) - za drogo, zglosic'
               % (skr(mediana(ms)), skr(WOZY_MS_MAKS)), wz[-1][1].get('_nr'))
        ce = ses.ser('Ceny surowcow')
        wyl = [(bi, d) for bi, d in ce if d.get('popyt_stan') != 'CZYNNY' or d.get('stala_stan') != 'CZYNNA']
        if wyl:
            al('uwaga', 'Ceny surowcow: popyt z prawdziwego zuzycia albo stala wzoru w nowej monecie wylaczone (%d dob) - ceny jak '
               'przed ogniwem 115' % len(wyl), wyl[0][1].get('_nr'))
        wt = ses.ser('Warsztaty towarowe')
        pk = [(bi, d) for bi, d in wt if (d.get('potkniecia') or 0) > 0]
        if pk:
            al('ALARM', 'Warsztaty towarowe: potkniecia w %d dobach (np. %d)' % (len(pk), pk[0][1].get('potkniecia')),
               pk[0][1].get('_nr'))
        nz, nz_od = najdluzsza_seria([(d.get('niezaplacone') or 0) > 0 for _, d in wt])
        if nz > 3:
            al('uwaga', 'Warsztaty towarowe: "niezaplacone (pusty kapital)" > 0 przez %d dob z rzedu (od doby %d)'
               % (nz, wt[nz_od][0] + 1), wt[nz_od][1].get('_nr'))
        bc = [d.get('bez_chetnego') for _, d in wt]
        rosnie, naj = 0, 0
        for i in range(1, len(bc)):
            if bc[i] is not None and bc[i - 1] is not None and bc[i] > bc[i - 1] > 0:
                rosnie += 1
                naj = max(naj, rosnie)
            else:
                rosnie = 0
        if naj >= 3:
            al('uwaga', 'Warsztaty towarowe: "bez chetnego z pieniedzmi" rosnie z doby na dobe (%d dob z rzedu, ostatnio %s) - notable nie '
               'maja pieniedzy na przejecie warsztatow' % (naj + 1, skr(bc[-1])), wt[-1][1].get('_nr'))
        bl = [(bi, d) for bi, d in ses.ser('Karawany (przyczyny)') if d.get('brak_latki')]
        if bl:
            al('ALARM', 'Karawany (przyczyny): BRAK latki BuyGoods (%d dob) - zakup surowcow przy wyjezdzie jak dotad (inna wersja BK)'
               % len(bl), bl[0][1].get('_nr'))
        w = ses.fakty.get('hp_uwaga0')
        if w is not None and ma_ogniwo(ses, '118'):
            al('uwaga', 'HistoricalPrices (118): %s' % wycinek(w.tresc[len('HistoricalPrices: '):], 100), w.nr)

    for f in reguly:
        try:
            f()
        except Exception as e:
            ses.uwagi.append('regula alarmu %s: blad wewnetrzny (%s: %s)' % (f.__name__, type(e).__name__, e))
    ses.alarmy.sort(key=lambda a: (0 if a[0] == 'ALARM' else 1, a[1] if a[1] is not None else 0))


# =====================================================================================================================
# KONTROLE OGNIW - "po czym poznac w logu" z docs\paczki\<ogniwo>.md i z wpisow 100-102 CHANGELOG
# Kazda kontrola: (status, co sprawdzono, wynik z liczba z logu, numer linii logu albo None).
# Status: OK = jest tak, jak opisuje paczka; UWAGA = jest inaczej (albo liczba poza oczekiwanym przedzialem);
# BRAK DANYCH = w logu nie ma linii, z ktorej da sie to odczytac. "pomiar:" = paczka kaze liczbe zmierzyc, nie ma progu.
# =====================================================================================================================
OK, UW, BD = 'OK', 'UWAGA', 'BRAK DANYCH'


class Kontrole(object):
    def __init__(self):
        self.lista = []

    def dodaj(self, status, co, wynik, nr=None):
        self.lista.append((status, co, wynik, nr))

    def gdy(self, warunek, co, wynik, nr=None):
        self.dodaj(OK if warunek else UW, co, wynik, nr)

    def brak(self, co, czego):
        self.dodaj(BD, co, czego, None)


def _wart(ser, klucz):
    """Liczby jednej kolumny szeregu (bez pustych i bez tekstow)."""
    return [d.get(klucz) for _, d in ser if isinstance(d.get(klucz), (int, float)) and not isinstance(d.get(klucz), bool)]


def _pierw_ost(ser, klucz):
    """(pierwsza, ostatnia, doba pierwszej, doba ostatniej) albo None."""
    z = [(bi, d.get(klucz)) for bi, d in ser if d.get(klucz) is not None]
    return (z[0][1], z[-1][1], z[0][0] + 1, z[-1][0] + 1) if z else None


def _zakres(v):
    """'mediana M (od A do B)' dla listy liczb."""
    if not v:
        return '-'
    return '%s (od %s do %s)' % (skr(mediana(v)), skr(min(v)), skr(max(v)))


def _start(K, ses, nr, co, wymagane=(), zakazane=('BRAK', 'NIE wpiet')):
    """Kontrola linii startowej ogniwa: jest, ma wymagane slowa, nie ma BRAK / NIE wpieta."""
    o = ogniwo(ses, nr)
    if not o or not o['jest']:
        K.brak(co, 'nie ma linii startowej (%s)' % (o['poznac'].split(';')[0] if o else nr))
        return None
    t = o['wpis'].tresc
    brakuje = [x for x in wymagane if x not in t]
    zle = [x for x in zakazane if re.search(r'\b' + re.escape(x), t)]
    K.gdy(not brakuje and not zle, co, ('brakuje: %s; ' % ', '.join(brakuje) if brakuje else '') +
          ('w linii jest %s; ' % ', '.join(zle) if zle else '') + wycinek(t, 110), o['wpis'].nr)
    return o['wpis']


def _k100(K, ses):
    _start(K, ses, '100', 'start: latka dowozu na targ miasta wpieta', ['wpieta'])
    dw, sk = ses.ser('Dowoz'), ses.ser('Dowoz (skutki)')
    if dw:
        wys = _wart(dw, 'wyslane')
        K.gdy(bool(wys) and mediana(wys) > 0, 'co dobe tabory wsi zamkowych jada na targ miasta',
              'wyslane na dobe: mediana %s w %d dobach; w drodze ostatnio %s' % (_zakres(wys), len(dw),
                                                                               skr(dw[-1][1].get('w_drodze'))), dw[-1][1].get('_nr'))
        d = dw[-1][1]
        K.gdy((d.get('bez_targu') or 0) == 0, 'kazda wies zamkowa ma targ miasta',
              'targ we wlasnym krolestwie %s, w obcym %s, bez targu %s; dzis zostalo przy zamku: targ za daleko %s, wojna %s'
              % (skr(d.get('targ_wlasny')), skr(d.get('targ_obcy')), skr(d.get('bez_targu')), skr(d.get('za_daleko')),
                 skr(d.get('wojna'))), d.get('_nr'))
        K.dodaj(OK, 'pomiar: rozbite tabory wsi (ladunek idzie do jukow band)', 'razem %s w %d dobach, w tym przez bandy %s'
                % (skr(sum(_wart(dw, 'rozbite'))), len(dw), skr(sum(_wart(dw, 'przez_bandy')))), d.get('_nr'))
    else:
        K.brak('co dobe tabory wsi zamkowych jada na targ miasta', 'nie ma linii "Dowoz:"')
    if sk:
        a, d = sk[0][1], sk[-1][1]
        za, zm = d.get('zatk_zamkowe_proc'), d.get('zatk_miejskie_proc')
        if za is not None and zm is not None:
            K.gdy(za - zm <= ZATKANE_ROZNICA_PP, 'zatkane magazyny: wsie zamkowe nie gorzej niz miejskie',
                  '%.1f%% zamkowych wobec %.1f%% miejskich w ostatniej dobie (pierwsza doba: %s%% / %s%%)'
                  % (za, zm, skr(a.get('zatk_zamkowe_proc')), skr(a.get('zatk_miejskie_proc'))), d.get('_nr'))
        K.gdy((d.get('pusty_spichlerz') or 0) == 0, 'zamki nie gloduja bez plonu z polki',
              'bilans zywnosci ujemny w %s zamkach, bez polki ujemny %s, pusty spichlerz %s (ostatnia doba)'
              % (skr(d.get('zamki_glodne')), skr(d.get('bez_polki_ujemny')), skr(d.get('pusty_spichlerz'))), d.get('_nr'))
        K.gdy((d.get('ponizej_progu') or 0) <= 5, 'kasy miast udzwigna skup plonu od wsi',
              'kasy miast razem %s -> %s, ponizej progu rent %s miast' % (skr(a.get('kasy_miast')), skr(d.get('kasy_miast')),
                                                                          skr(d.get('ponizej_progu'))), d.get('_nr'))
    else:
        K.brak('zatkane magazyny: wsie zamkowe nie gorzej niz miejskie', 'nie ma linii "Dowoz (skutki):"')
    po = _pierw_ost(ses.ser('Ruda'), 'bez_towaru')
    if po:
        K.gdy(po[1] <= po[0], 'ruda: miast bez towaru ubywa', '%s -> %s miast bez rudy (doby %d-%d)' % (po[0], po[1], po[2], po[3]),
              ses.ser('Ruda')[-1][1].get('_nr'))


def _k101(K, ses):
    w = ses.fakty.get('woz')
    if w is not None:
        m = re.search(r'x(\d+(?:[.,]\d+)?)', w.tresc)
        K.gdy(bool(m) and abs(float(m.group(1).replace(',', '.')) - 2.0) < 0.01, 'start: udzwig taboru wsi zamkowej x2',
              wycinek(w.tresc, 110), w.nr)
    else:
        K.brak('start: udzwig taboru wsi zamkowej x2', 'nie ma linii "MarketRoad: woz - udzwig taborow ..."')
    sk = ses.ser('Dowoz (skutki)')
    if sk:
        d = sk[-1][1]
        za, zm = d.get('zatk_zamkowe_proc'), d.get('zatk_miejskie_proc')
        if za is not None and zm is not None:
            K.gdy(za - zm <= ZATKANE_ROZNICA_PP, 'zatkane magazyny wsi zamkowych jak miejskich (ok. 8%)',
                  '%.1f%% zamkowych wobec %.1f%% miejskich w ostatniej dobie; w szeregu zamkowe %s'
                  % (za, zm, _zakres(_wart(sk, 'zatk_zamkowe_proc'))), d.get('_nr'))
    else:
        K.brak('zatkane magazyny wsi zamkowych jak miejskich (ok. 8%)', 'nie ma linii "Dowoz (skutki):"')
    for nazwa in ('Ruda', 'Drewno'):
        ser = [(bi, d) for bi, d in ses.ser(nazwa) if bi > 0]
        po = _pierw_ost(ser, 'wsie')
        if po:
            K.gdy(po[1] <= po[0] * 1.05, '%s: zapas lezacy we wsiach nie rosnie' % nazwa,
                  '%s -> %s ladunkow we wsiach (doby %d-%d); wsie dopisaly na dobe: %s'
                  % (skr(po[0]), skr(po[1]), po[2], po[3], _zakres(_wart(ser, 'dopisaly'))), ser[-1][1].get('_nr'))
        else:
            K.brak('%s: zapas lezacy we wsiach nie rosnie' % nazwa, 'nie ma linii "%s:" z pozycja "wsie" (ksiega wpisu 102)' % nazwa)
    dw = ses.ser('Dowoz')
    if dw:
        K.dodaj(OK, 'pomiar: rozbite tabory (wiekszy woz = wiecej do stracenia)', 'razem %s w %d dobach, w tym przez bandy %s'
                % (skr(sum(_wart(dw, 'rozbite'))), len(dw), skr(sum(_wart(dw, 'przez_bandy')))), dw[-1][1].get('_nr'))


def _k102(K, ses):
    f = ses.fakty
    w = _start(K, ses, '102', 'start: ksiega pieniadza - liczniki wpiete, bez "BRAK"',
               ['konsumpcja mieszkancow', 'regulator kasy w', 'zold'])
    if w is not None:
        m = re.search(r'regulator kasy w (\d+) modelach', w.tresc)
        K.gdy(bool(m) and int(m.group(1)) == 3, 'regulator kasy liczony w 3 modelach (gra, BK, BetterEconomy)',
              ('w %s modelach' % m.group(1)) if m else 'liczby modeli nie ma w linii startowej', w.nr)
    pelne = len(ses.pelne())
    jest = [('"Przeplywy osad: modele czynne"', 'modele' in f), ('"Ludzie: plik wszystkich regionow"', 'csv' in f),
            ('"Pieniadz swiata: ... Pierwsza doba ksiegi"', any('Pierwsza doba ksiegi' in x.tresc for x in
                                                               ses.linie_tematu.get('Pieniadz swiata') or []))]
    if ses.linie_tematu.get('Pieniadz swiata') or ses.linie_tematu.get('Ludzie'):
        K.gdy(all(j for _, j in jest), 'pierwsza doba: trzy linie rozruchu ksiag',
              ', '.join('%s %s' % (n, 'jest' if j else 'BRAK') for n, j in jest))
        tem = ['Pieniadz swiata', 'Pieniadz swiata (bilans)', 'Przeplywy osad', 'Przeplywy osad (kasy miast)',
               'Przeplywy osad (kasy zamkow)', 'Przeplywy osad (kiesy wsi)', 'Ludzie', 'Ludzie (regiony)']
        dob = [(n, len(ses.ser(n))) for n in tem]
        chce = dict((n, pelne if n in ('Pieniadz swiata', 'Ludzie', 'Ludzie (regiony)') else max(0, pelne - 1)) for n in tem)
        zle = [(n, c) for n, c in dob if c < chce[n]]
        K.gdy(not zle, 'od drugiej doby komplet: 6 linii pieniadza i 2 linie ludzi',
              ('komplet w %d dobach' % pelne) if not zle else 'niepelne: ' + ', '.join('%s %d z %d dob' % (n, c, chce[n])
                                                                                    for n, c in zle))
    else:
        K.brak('od drugiej doby komplet: 6 linii pieniadza i 2 linie ludzi', 'nie ma linii "Pieniadz swiata:" ani "Ludzie:"')
    for nazwa, ile in (('Przeplywy osad (kasy miast)', 'ok. 97 miast'), ('Przeplywy osad (kasy zamkow)', 'do 130 zamkow')):
        ser = ses.ser(nazwa)
        t, osad = _wart(ser, 'zakupy_tickow'), _wart(ser, 'osad')
        if t and osad:
            K.gdy(mediana(t) >= TICKI_OSAD_MIN_UDZIAL * mediana(osad), '%s: konsumpcja policzona w kazdej osadzie (%s)'
                  % (nazwa.split('(')[1].rstrip(')'), ile), 'tickow osad: mediana %s przy %s osadach; regulator kasy: %s tickow'
                  % (_zakres(t), skr(mediana(osad)), _zakres(_wart(ser, 'reg_tickow'))), ser[-1][1].get('_nr'))
    prz = ses.ser('Przeplywy osad')
    if prz:
        zr = _wart(prz, 'zold_razem')
        K.dodaj(OK, 'pomiar: licznik zoldu (porownac z CSV CrashScribe)',
                'zold razem na dobe: mediana %s' % _zakres(zr), prz[-1][1].get('_nr'))
        zp = _wart(prz, 'zniklo_proc')
        # ogniwo 112 (K7) zamyka te dziure: przy czynnym "Utarg wsi: powroty taborow" ma znikac 0, nie 15%
        k7 = [1 for _, d in ses.ser('Utarg wsi') if d.get('powroty_stan') == 'czynne']
        if k7:
            K.gdy(bool(zp) and max(abs(x) for x in zp) < 0.5, 'z utargu taborow wsi nic nie znika (dziure gry zamyka K7, ogniwo 112)',
                  'zniklo: mediana %s%% utargu oddanego we wsi, %s zlota na dobe' % (_zakres(zp), _zakres(_wart(prz, 'zniklo'))),
                  prz[-1][1].get('_nr'))
        else:
            K.gdy(bool(zp) and 10.0 <= mediana(zp) <= 20.0, 'z utargu taborow wsi znika ok. 15% (znana dziura gry, krok K7)',
                  'zniklo: mediana %s%% utargu oddanego we wsi, %s zlota na dobe' % (_zakres(zp), _zakres(_wart(prz, 'zniklo'))),
                  prz[-1][1].get('_nr'))
        potk = sum(_wart(prz, 'potkniecia'))
        K.gdy(potk == 0, 'potkniecia licznikow ksiegi pieniadza: brak', 'suma %d w %d dobach' % (potk, len(prz)),
              prz[-1][1].get('_nr'))
    else:
        K.brak('licznik zoldu, utarg taborow, potkniecia licznikow', 'nie ma linii "Przeplywy osad:" (pojawia sie od drugiej doby)')
    dr, ru = [(bi, d) for bi, d in ses.ser('Drewno') if bi > 0], [(bi, d) for bi, d in ses.ser('Ruda') if bi > 0]
    bd = _wart(dr, 'budowy')
    if bd:
        K.gdy(1200 <= mediana(bd) <= 2200, 'Drewno: budowy biora ok. 1500-1900 ladunkow na dobe',
              'budowy: mediana %s; "bez wyjasnienia": mediana %s (ok. +2100 to znana dosypka RealisticBannerlord)'
              % (_zakres(bd), _zakres(_wart(dr, 'bez_wyjasnienia'))), dr[-1][1].get('_nr'))
    br = _wart(ru, 'budowy')
    if br:
        K.gdy(max(br) == 0, 'Ruda: budowy nie kupuja rudy (pozycja 0)', 'budowy: najwiecej %s na dobe' % skr(max(br)),
              ru[-1][1].get('_nr'))
    lu, ln = ses.ser('Ludzie'), dict(ses.ser('Ludnosc'))
    pary = [(d.get('ludnosc_mln'), (ln.get(bi) or {}).get('mln')) for bi, d in lu]
    pary = [(a, b) for a, b in pary if a is not None and b is not None]
    if pary:
        naj = max(abs(a - b) for a, b in pary)
        K.gdy(naj <= LUDNOSC_ROZJAZD_MLN, '"Ludzie: ludnosc" zgodna z linia "Ludnosc:"',
              'najwieksza roznica %.3f mln w %d dobach (ostatnio %.3f wobec %.1f mln)' % (naj, len(pary), pary[-1][0], pary[-1][1]),
              lu[-1][1].get('_nr'))
    elif lu:
        K.brak('"Ludzie: ludnosc" zgodna z linia "Ludnosc:"', 'brak liczby ludnosci w jednej z linii')
    bil = ses.ser('Pieniadz swiata (bilans)')
    if bil and f.get('uklad_ksiegi') == UKLAD_102:
        st = [d['reszta'] / float(d['zold']) for _, d in bil if d.get('zold') and d.get('reszta') is not None]
        if st:
            K.dodaj(UW, 'bilans swiata: reszta [R] zawyzona o zold (naprawia 102b)',
                    'reszta / zold: mediana %.2f w %d dobach; reszta %s, zold %s' % (mediana(st), len(st),
                                                                                    _zakres(_wart(bil, 'reszta')),
                                                                                    _zakres(_wart(bil, 'zold'))),
                    bil[-1][1].get('_nr'))


def _k102b(K, ses):
    _start(K, ses, '102b', 'start: licznik rozliczen rodow wpiety', ['rozliczenie rodow'])
    rody, bil = ses.ser('Pieniadz swiata (rody)'), ses.ser('Pieniadz swiata (bilans)')
    pelne = len(ses.pelne())
    if not rody:
        K.brak('co dobe linia "Pieniadz swiata (rody):"', 'nie ma jej w logu (pojawia sie od drugiej doby ksiegi)')
    else:
        K.gdy(len(rody) >= pelne - 1, 'co dobe linia "Pieniadz swiata (rody):" (od drugiej doby)',
              '%d linii w %d dobach' % (len(rody), pelne), rody[0][1].get('_nr'))
        k = _wart(rody, 'rozliczen')
        if k:
            med = mediana(k)
            K.gdy((max(k) - min(k)) <= max(10, ROZLICZEN_ROZRZUT_PROC / 100.0 * med),
                  'liczba rozliczen na dobe stala (tyle, ile rodow bez band)', 'rozliczen: mediana %s' % _zakres(k),
                  rody[-1][1].get('_nr'))
        zm = _wart(rody, 'zmiana')
        if zm:
            K.gdy(mediana(zm) < 0, 'rozliczenia rodow: zmiana zlota swiata (ok. -400..-600 tys.)',
                  'zmiana zlota swiata przez rozliczenia: mediana %s; bez zoldu byloby %s'
                  % (_zakres(zm), _zakres(_wart(rody, 'bez_zoldu'))), rody[-1][1].get('_nr'))
        nd = sum(_wart(rody, 'niedomkniete'))
        K.gdy(nd == 0 and not any(d.get('niewpiety') for _, d in rody), 'rozliczenia niedomkniete: brak',
              'niedomknietych %d w %d dobach%s' % (nd, len(rody), '; LICZNIK NIE WPIETY' if any(d.get('niewpiety')
                                                                                              for _, d in rody) else ''),
              rody[-1][1].get('_nr'))
    if bil:
        u = ses.fakty.get('uklad_ksiegi')
        K.gdy(u == UKLAD_102B, 'bilans w ukladzie 102b (rozliczenia rodow, "W tym zold naliczony")',
              'uklad linii: %s' % ('102b' if u == UKLAD_102B else 'wpis 102 - poprawka ksiegi nie jest wgrana'),
              bil[-1][1].get('_nr'))
        st = [d['reszta'] / float(d['zold']) for _, d in bil if d.get('zold') and d.get('reszta') is not None]
        if st:
            med = mediana(st)
            K.gdy(not (ZOLD_DUBEL_OD <= med <= ZOLD_DUBEL_DO), 'reszta [R] bilansu nie jest juz rzedu zoldu',
                  'reszta / zold: mediana %.2f w %d dobach; reszta %s, zold naliczony %s; reszta to %s%% ruchu'
                  % (med, len(st), _zakres(_wart(bil, 'reszta')), _zakres(_wart(bil, 'zold')),
                     _zakres(_wart(bil, 'reszta_proc'))), bil[-1][1].get('_nr'))
        bild, rozne, razem = dict(bil), 0, 0
        for bi, d in rody:
            b = bild.get(bi)
            if b is None or b.get('rody_plus') is None or d.get('na_plus') is None:
                continue
            razem += 1
            if d['na_plus'] != b['rody_plus'] or abs(d.get('na_minus') or 0) != abs(b.get('rody_minus') or 0) \
                    or (d.get('zold') is not None and b.get('zold') is not None and d['zold'] != b['zold']):
                rozne += 1
        if razem:
            K.gdy(rozne == 0, 'bilans i linia rodow podaja te same rozliczenia i ten sam zold',
                  'zgodne w %d z %d dob' % (razem - rozne, razem), rody[-1][1].get('_nr'))
    else:
        K.brak('reszta [R] bilansu nie jest juz rzedu zoldu', 'nie ma linii "Pieniadz swiata (bilans):"')
    prz = ses.ser('Przeplywy osad')
    if prz:
        potk = sum(_wart(prz, 'potkniecia'))
        K.gdy(potk == 0 and all(d.get('uklad') == UKLAD_102B for _, d in prz),
              '"Przeplywy osad" w ukladzie 102b, bez potkniec licznikow',
              'uklad 102b w %d z %d dob, potkniecia licznikow %d' % (len([1 for _, d in prz if d.get('uklad') == UKLAD_102B]),
                                                                   len(prz), potk), prz[-1][1].get('_nr'))
    st = ses.fakty.get('stale_skarbca')
    if st:
        K.gdy(st[0] == st[1], 'bez zmian: skarbiec - podmienione stale 4 z 4',
              'podmienionych stalych %d, oczekiwane %d' % (st[0], st[1]), st[2])


def _k103(K, ses):
    _start(K, ses, '103', 'start: latki karawan wpiete, regula CZYNNA', ['latki wpiete', 'CZYNNA'], ('BRAK', 'NIE wpiet', 'WYLACZON'))
    kar, stan = ses.ser('Karawany'), ses.ser('Karawany (stan)')
    if not kar or not stan:
        K.brak('co dobe linie "Karawany:" i "Karawany (stan):"', 'nie ma ich w logu')
        return
    po = _pierw_ost(stan, 'ruda_bez')
    if po:
        ok = po[1] <= KARAWANY_BEZ_TOWARU_CEL or (len(stan) < KARAWANY_MIN_DOB and po[1] <= po[0]) \
            or (po[0] and 100.0 * (po[0] - po[1]) / po[0] >= KARAWANY_MIN_SPADEK_PROC)
        K.gdy(ok, 'ruda: miast bez towaru ubywa (cel: kilkanascie w 10-20 dob)',
              '%s -> %s miast bez rudy (doby %d-%d); brakuje %s -> %s ladunkow, w jukach karawan %s'
              % (po[0], po[1], po[2], po[3], skr(stan[0][1].get('ruda_brak')), skr(stan[-1][1].get('ruda_brak')),
                 skr(stan[-1][1].get('ruda_juki'))), stan[-1][1].get('_nr'))
    ceny = _wart(stan, 'ruda_cena')
    if ceny:
        bez = [d.get('bez_ceny') for _, d in stan if d.get('bez_ceny')]
        K.gdy(not bez and max(ceny) > 0, 'cena zbytu rudy jest (7-10 d przy pustych miastach, potem w dol)',
              'cena zbytu rudy %s -> %s d (kupuje %s -> %s miast)%s'
              % (skr(ceny[0]), skr(ceny[-1]), skr(stan[0][1].get('ruda_kupuje')), skr(stan[-1][1].get('ruda_kupuje')),
                 ('; BRAK CENY przy niezerowym braku: %s' % bez[0]) if bez else ''), stan[-1][1].get('_nr'))
    else:
        K.brak('cena zbytu rudy w "Karawany (stan):"', 'linie nie maja pozycji "cena zbytu" (starsza postac ogniwa 103)')
    kup, zak = sum(_wart(kar, 'kup_szt')), sum(_wart(kar, 'z_zakupem'))
    K.gdy(zak > 0, 'karawany kupuja surowce z nadwyzek miast',
          'wyjazdow z zakupem %s z %s w %d dobach; kupione %s szt. (ruda %s, drewno %s) za %s d'
          % (skr(zak), skr(sum(_wart(kar, 'wyjazdy'))), len(kar), skr(kup), skr(sum(_wart(kar, 'kup_ruda'))),
             skr(sum(_wart(kar, 'kup_drewno'))), skr(sum(_wart(kar, 'karawany_zaplacily')))), kar[-1][1].get('_nr'))
    sprz = sum(_wart(kar, 'sprz_szt'))
    K.gdy(sprz > 0, 'karawany sprzedaja surowce miastom z brakiem',
          'wjazdow ze sprzedaza %s; sprzedane %s szt. (ruda %s, drewno %s), miasta zaplacily %s d'
          % (skr(sum(_wart(kar, 'ze_sprzedaza'))), skr(sprz), skr(sum(_wart(kar, 'sprz_ruda'))),
             skr(sum(_wart(kar, 'sprz_drewno'))), skr(sum(_wart(kar, 'miasta_zaplacily')))), kar[-1][1].get('_nr'))
    if _wart(kar, 'first_szt'):
        od = Counter()
        for _, d in kar:
            if d.get('first_naj') and d.get('first_naj') != 'nic':
                od[d['first_naj']] += 1
        K.dodaj(OK, 'pomiar: kolejnosc kupna wedle zysku (ruda rzadko pierwsza)',
                'wyjazdow, ktore zaczely od rudy: %s z %s; najczesciej pierwsze: %s; oczekiwany zysk razem %s d'
                % (skr(sum(_wart(kar, 'first_ruda'))), skr(sum(_wart(kar, 'first_szt'))),
                   ', '.join('%s (%d dob)' % x for x in od.most_common(3)) or '-', skr(sum(_wart(kar, 'zysk_razem')))),
                kar[-1][1].get('_nr'))
    else:
        K.brak('kolejnosc kupna wedle zysku', 'linia "Karawany:" bez pozycji "wyjazdow, ktore zaczely zakupy od" (starsza postac)')
    bz, bm = sum(_wart(kar, 'odp_bez_zysku')), sum(_wart(kar, 'odp_brak_miejsca'))
    K.gdy(not (bm > 20 and bm > KARAWANY_BRAK_MIEJSCA_KROTNOSC * max(1, zak)),
          'zakup odpuszczony: "brak miejsca w jukach" nie dominuje',
          'bez zysku wobec ceny zbytu %s, brak miejsca w jukach %s, cena nie nizsza od sredniej %s, w drodze dosc %s'
          % (skr(bz), skr(bm), skr(sum(_wart(kar, 'odp_cena'))), skr(sum(_wart(kar, 'odp_w_drodze_dosc')))), kar[-1][1].get('_nr'))
    naj = (0, None)
    for nazwa in SUROWCE_KARAWAN.values():
        ile, _ = najdluzsza_seria([bool(d.get(nazwa + '_stoi')) for _, d in stan])
        if ile > naj[0]:
            naj = (ile, nazwa)
    K.gdy(naj[0] <= KUPNO_STOI_MAKS_DOB, '"KUPNO STOI" najwyzej %d doby z rzedu' % KUPNO_STOI_MAKS_DOB,
          ('najdluzej %d dob (%s)' % naj) if naj[0] else 'ani razu', stan[-1][1].get('_nr'))
    ru = [(bi, d) for bi, d in ses.ser('Ruda') if bi > 0]
    wz = _wart(ru, 'warsztaty')
    if wz:
        K.dodaj(OK, 'pomiar: ruda zuzyta przez warsztaty zbrojne (cel 50-75)',
                'warsztaty zbrojne: mediana %s ladunkow na dobe; "Warsztaty: brak surowca [ruda]": %s'
                % (_zakres(wz), _zakres(_wart(ses.ser('Warsztaty'), 'bs_ruda'))), ru[-1][1].get('_nr'))


def _k104(K, ses):
    f = ses.fakty
    linie = f.get('startstock') or []
    if not linie:
        if f.get('nowa_kampania'):
            K.dodaj(UW, 'start nowej kampanii: 4 linie "StartStock:"', 'nowa kampania, a linii "StartStock:" nie ma - ogniwo 104 nie '
                    'jest wgrane albo zapas nie zostal przeliczony')
        else:
            K.brak('start nowej kampanii: 4 linie "StartStock:"', 'wczytany zapis - linie sa tylko przy starcie NOWEJ kampanii')
        return
    bez = [w for w in linie if 'BEZ przeliczenia' in w.tresc or 'nie da sie odczytac' in w.tresc]
    K.gdy(len(linie) == 4 and not bez, 'start nowej kampanii: 4 linie "StartStock:"',
          '%d linii "StartStock:" przy starcie kampanii%s' % (len(linie), ('; zapas NIE przeliczony: ' +
                                                                         obetnij(bez[0].tresc[12:], 80)) if bez else ''),
          linie[0].nr)
    potk = None
    for w in linie:
        m = re.search(r'StartStock: (\w+) - (\d+) szt\. po .*? -> (\d+) ladunkow po .*?cel (-?\d+)', w.tresc)
        if m:
            n, szt, po, cel = m.group(1), int(m.group(2)), int(m.group(3)), int(m.group(4))
            K.gdy(po == cel, 'zapas startowy: %s przeliczony na ladunki, "po" = "cel"' % n,
                  '%d szt. -> %d ladunkow, cel %d (oczekiwane: %s)' % (szt, po, cel, 'ruda ok. 7000 -> ok. 700' if n == 'ruda'
                                                                       else 'drewno ok. 25-28 tys. -> ok. 2500-2800'), w.nr)
        m = re.search(r'gotowe - .*?; potkniecia (\d+)', w.tresc)
        if m:
            potk = (int(m.group(1)), w.nr)
    if potk is not None:
        K.gdy(potk[0] == 0, 'przeliczenie bez potkniec i bez "ERROR in StartStock"', 'potkniecia %d; linii ERROR ze StartStock: %d'
              % (potk[0], len([b for b in ses.bledy if 'StartStock' in b[0]])), potk[1])
    ru, dr = ses.ser('Ruda'), ses.ser('Drewno')
    if ru and ru[0][0] == 0 and ru[0][1].get('razem') is not None:
        K.gdy(ru[0][1]['razem'] < RUDA_RAZEM_PO_104, '"Ruda: razem" pierwszej doby ponizej %d ladunkow' % RUDA_RAZEM_PO_104,
              'razem %s ladunkow rudy w pierwszej dobie (przed przeliczeniem bylo ok. 7000)' % skr(ru[0][1]['razem']),
              ru[0][1].get('_nr'))
    else:
        K.brak('"Ruda: razem" pierwszej doby ponizej %d ladunkow' % RUDA_RAZEM_PO_104, 'nie ma linii "Ruda:" pierwszej doby')
    if ru and dr:
        K.dodaj(OK, 'pomiar: wsie dopisuja od pierwszej doby (ok. 26 / 45 wsi)',
                'pierwsza doba: ruda %s ladunkow w %s wsiach, drewno %s w %s wsiach'
                % (skr(ru[0][1].get('dopisaly')), skr(ru[0][1].get('wsi_kopie')), skr(dr[0][1].get('dopisaly')),
                   skr(dr[0][1].get('wsi_kopie'))), ru[0][1].get('_nr'))
    sk = ses.ser('Dowoz (skutki)')
    if sk:
        d = sk[0][1]
        K.gdy((d.get('zatk_zamkowe') or 0) < 25 and (d.get('zatk_miejskie') or 0) < 32,
              'zatkane magazyny 1. doby ponizej 25 (zamkowe) i 32 (miejskie)',
              'zatkane: wsie zamkowe %s, wsie miejskie %s' % (skr(d.get('zatk_zamkowe')), skr(d.get('zatk_miejskie'))), d.get('_nr'))
    kar = ses.ser('Karawany')
    if kar:
        pierwsza = [bi + 1 for bi, d in kar if (d.get('z_zakupem') or 0) > 0]
        K.gdy(bool(pierwsza) and pierwsza[0] <= KARAWANY_ROZRUCH_DOB or len(kar) < KARAWANY_ROZRUCH_DOB,
              'karawany: pierwsze zakupy w 7-10 dob po przeliczeniu zapasu',
              ('pierwszy wyjazd z zakupem w dobie %d' % pierwsza[0]) if pierwsza else 'zadnego zakupu w %d dobach' % len(kar),
              kar[-1][1].get('_nr'))


def _k105(K, ses):
    f = ses.fakty
    _start(K, ses, '105', 'start: latka mineralow wpieta (nie "NIE wpieta")', ['latka wpieta'])
    mn = f.get('mnoznik_rudy')
    if mn is not None:
        K.gdy(abs(mn - MNOZNIK_RUDY_OCZEKIWANY) < 0.01, 'wydobycie bez zmian: "wydobycie rudy x3.0"',
              'x%.1f (drewna x%.1f)%s' % (mn, f.get('mnoznik_drewna', 0.0), '' if abs(mn - MNOZNIK_RUDY_OCZEKIWANY) < 0.01
                                          else ' - inna wartosc Mine Output Multiplier zapisana w MCM'), f.get('mnoznik_nr'))
    else:
        K.brak('wydobycie bez zmian: "wydobycie rudy x3.0"', 'nie ma linii "MaterialLaw: ... wydobycie rudy x..."')
    mi = ses.ser('Mineraly (dubel BK)')
    if not mi:
        K.brak('co dobe linia "Mineraly (dubel BK):"', 'nie ma jej w logu')
    else:
        stany = Counter(d.get('stan') for _, d in mi)
        zdj = [(bi, d) for bi, d in mi if d.get('stan') == 'zdjete']
        K.gdy(bool(zdj) and not stany.get('NIE wpieta') and not stany.get('WYLACZONA'),
              'co dobe powtorzenia zdjete z list produkcji BK',
              'stan latki w %d dobach: %s' % (len(mi), ', '.join('%s %d' % (k, v) for k, v in stany.most_common())),
              mi[-1][1].get('_nr'))
        if zdj:
            naj = dict((k, max(_wart(zdj, k) or [0])) for k in MINERALY_ZNANE)
            K.gdy(all(naj[k] <= lim for k, lim in MINERALY_LIMIT_WSI.items()) and naj['iron'] > 0,
                  'lista: clay do 16, iron do 26, salt do 12, silver do 30 wsi',
                  'najwiecej wsi: iron %d, salt %d, clay %d, silver %d, marble %d, gold_ore %d'
                  % (naj['iron'], naj['salt'], naj['clay'], naj['silver'], naj['marble'], naj['gold_ore']), zdj[-1][1].get('_nr'))
            wm = _wart(zdj, 'wsi_mnoznik')
            if wm:
                K.gdy(min(wm) > 0, 'zdjety wpis oddaje mnoznik modelu (wsi z mnoznikiem ok. 84)',
                      'wsi z mnoznikiem: mediana %s' % _zakres(wm), zdj[-1][1].get('_nr'))
            else:
                K.brak('zdjety wpis oddaje mnoznik modelu (wsi z mnoznikiem ok. 84)', 'linia bez tej pozycji (starsza postac ogniwa 105)')
        obce = [d.get('obce') for _, d in mi if d.get('obce')]
        potk = max(_wart(mi, 'potkniecia') or [0])
        K.gdy(not obce and potk == 0, 'bez obcych przedmiotow na liscie i bez potkniec',
              'obce przedmioty: %s; potkniecia od wczytania kampanii: %d' % (obce[0] if obce else 'brak', potk),
              mi[-1][1].get('_nr'))
    ru = [(bi, d) for bi, d in ses.ser('Ruda') if bi > 0]
    nw = _wart(ru, 'na_wies_do_modelu')
    if nw:
        K.gdy(mediana(nw) <= STOSUNEK_NA_WIES_PO_105, 'Ruda: kopiaca wies dopisuje tyle, ile liczy "model"',
              'dopisane na wies / model na wies: mediana %s; "model" %s w %s wsiach (dotad ok. 97, po zmianie ok. 192)'
              % (_zakres(nw), _zakres(_wart(ru, 'model')), skr(mediana(_wart(ru, 'model_wsi')))), ru[-1][1].get('_nr'))
        K.dodaj(OK, 'pomiar: liczba kopiacych wsi (dotad srednio 14.9 z 26)',
                'kopiacych wsi na dobe: mediana %s; wsie dopisaly: mediana %s ladunkow'
                % (_zakres(_wart(ru, 'wsi_kopie')), _zakres(_wart(ru, 'dopisaly'))), ru[-1][1].get('_nr'))
    else:
        K.brak('Ruda: kopiaca wies dopisuje tyle, ile liczy "model"', 'nie ma linii "Ruda:" od drugiej doby')


def _k106(K, ses):
    f = ses.fakty
    chce = ['awanse band', 'zywnosc band', 'NavalDLC zywnosc piratow', 'zloto dzienne', 'zloto startowe',
            'NavalDLC zloto startowe', 'zloto kryjowek']
    w = f.get('outlaw')
    if w is not None:
        brak = [x for x in chce if x not in w.tresc.split('; BRAK:')[0]]
        K.gdy(not brak and '; BRAK:' not in w.tresc, 'start: OutlawLaw wpiete (zywnosc band, zloto kryjowek)',
              ('brak na liscie wpietych: ' + ', '.join(brak)) if brak else wycinek(w.tresc, 110), w.nr)
    else:
        K.brak('start: OutlawLaw wpiete (zywnosc band, zloto kryjowek)', 'nie ma linii "OutlawLaw: ... wpiete: ..."')
    pa = ses.ser('Paser')
    if not pa:
        K.brak('co dobe linia "Paser:"', 'nie ma jej w logu')
        return
    lad, zp = sum(_wart(pa, 'z_ladunkiem')), sum(_wart(pa, 'z_paserem'))
    bez = [(bi, d) for bi, d in pa if (d.get('bez_pasera') or 0) > 0]
    K.gdy(not bez and bool(_wart(pa, 'z_paserem')), 'kazda banda z ladunkiem ma pasera (100%)',
          'z paserem %s z %s band z ladunkiem w %d dobach%s; poza promieniem %s'
          % (skr(zp), skr(lad), len(pa), ('; dob z bandami bez pasera: %d' % len(bez)) if bez else '',
             skr(sum(_wart(pa, 'poza_promieniem')))), (bez[0][1] if bez else pa[-1][1]).get('_nr'))
    zost = [(bi, d) for bi, d in pa if (d.get('zostalo') or 0) > 0]
    ile, pocz = najdluzsza_seria([(d.get('zostalo') or 0) > 0 for _, d in pa])
    K.gdy(ile < PASER_ZOSTALO_DOB, '"z ladunkiem zostalo 0 band"',
          'zostalo ponad 0 w %d z %d dob, najdluzej %d dob z rzedu; powody razem: kasy miast przy rezerwie %s, brak otwartego '
          'miasta %s' % (len(zost), len(pa), ile, skr(sum(_wart(pa, 'zost_rezerwa'))), skr(sum(_wart(pa, 'zost_brak_miasta')))),
          (zost[0][1] if zost else pa[-1][1]).get('_nr'))
    ponad = [1 for _, d in pa if d.get('sprzedalo') is not None and d.get('z_ladunkiem') is not None
             and d['sprzedalo'] > d['z_ladunkiem']]
    K.gdy(not ponad, '"sprzedalo" nigdy wiecej niz "band z ladunkiem"',
          'sprzedalo %s band, %s szt. (ruda %s, drewno %s, zywnosc %s, zwierzeta %s) za %s zl; dzialka pasera %s zl'
          % (skr(sum(_wart(pa, 'sprzedalo'))), skr(sum(_wart(pa, 'szt'))), skr(sum(_wart(pa, 'ruda'))),
             skr(sum(_wart(pa, 'drewno'))), skr(sum(_wart(pa, 'zywnosc'))), skr(sum(_wart(pa, 'zwierzeta'))),
             skr(sum(_wart(pa, 'za_zl'))), skr(sum(_wart(pa, 'dzialka')))), pa[-1][1].get('_nr'))
    km, kz = dict(ses.ser('Przeplywy osad (kasy miast)')), dict(ses.ser('Przeplywy osad (kasy zamkow)'))
    razem = skup_zle = zycie_zle = 0
    for bi, d in pa:
        k = km.get(bi)
        if k is None:
            continue
        z = kz.get(bi) or {}
        razem += 1
        if d.get('za_zl') is not None and abs((k.get('paser_skup') or 0) + (z.get('paser_skup') or 0)) != d['za_zl']:
            skup_zle += 1
        if d.get('zycie_razem') is not None and (k.get('zycie_band') or 0) + (z.get('zycie_band') or 0) != d['zycie_razem']:
            zycie_zle += 1
    if razem:
        K.gdy(skup_zle == 0 and zycie_zle == 0, 'ksiega kas miast liczy ten sam skup i zycie band',
              'porownane %d dob: skup zgodny w %d, zycie w miastach zgodne w %d; zycie razem %s zl'
              % (razem, razem - skup_zle, razem - zycie_zle, skr(sum(_wart(pa, 'zycie_razem')))), pa[-1][1].get('_nr'))
    else:
        K.brak('ksiega kas miast liczy ten sam skup i zycie band', 'nie ma linii "Przeplywy osad (kasy miast):" w dobach z linia "Paser:"')
    aw = sum(_wart(ses.ser('Wyrzutki'), 'aw_paser'))
    K.gdy(aw > 0 or len(pa) < PASER_MIN_DOB_BEZ_AWANSOW, 'bandy kupuja sprzet u pasera (awanse od pasera ponad 0)',
          'awansow od pasera %s w %d dobach za %s zl (w tym z kas kryjowek %s zl)'
          % (skr(aw), len(pa), skr(sum(_wart(pa, 'kupno_zl'))), skr(sum(_wart(pa, 'kupno_z_kryjowek')))), pa[-1][1].get('_nr'))
    otw = [1 for _, d in pa if d.get('kurek_otwarty') or d.get('kurek_zywnosci') or d.get('obieg_wylaczony')]
    K.gdy(not otw, 'koniec zlota i zywnosci band z niczego (kurki zamkniete)',
          'zablokowane zloto kryjowek %s zl w %s wejsciach; zywnosc zablokowana %s nowym bandom%s'
          % (skr(sum(_wart(pa, 'zabl_razem'))), skr(sum(_wart(pa, 'wejsc'))), skr(sum(_wart(pa, 'zywn_zabl'))),
             ('; DOB Z OTWARTYM KURKIEM albo wylaczonym obiegiem: %d' % len(otw)) if otw else ''), pa[-1][1].get('_nr'))
    ost = pa[-1][1]
    K.gdy((ost.get('juki_zywnosc') or 0) == 0, 'juki band: ladunek na sprzedaz, "zywnosc poza skupem 0"',
          'ostatnia doba: ladunek na sprzedaz %s szt. (%s zl), zywnosc poza skupem %s, zbroje na awanse %s, konie %s'
          % (skr(ost.get('juki_ladunek')), skr(ost.get('juki_ladunek_zl')), skr(ost.get('juki_zywnosc')),
             skr(ost.get('juki_zbroje')), skr(ost.get('juki_konie'))), ost.get('_nr'))
    po = _pierw_ost(pa, 'kasy_kryjowek')
    K.dodaj(OK, 'pomiar: kasy kryjowek i skup wedle odleglosci od miasta',
            'kasy kryjowek %s -> %s zl; doplyw %s, odplyw %s; skup szt.: do 50 - %s, 50-100 - %s, dalej - %s'
            % (skr(po[0]) if po else '-', skr(po[1]) if po else '-', skr(sum(_wart(pa, 'kryj_doplyw'))),
               skr(sum(_wart(pa, 'kryj_odplyw'))), skr(sum(_wart(pa, 'strefa1_szt'))), skr(sum(_wart(pa, 'strefa2_szt'))),
               skr(sum(_wart(pa, 'strefa3_szt')))), ost.get('_nr'))


def _k107(K, ses):
    f = ses.fakty
    _start(K, ses, '107', 'start: "SoldierPay: zold do obiegu ... rozliczenie rodu wpiete"', ['rozliczenie rodu wpiete'])
    w = f.get('soldierpay_latki')
    if w is not None:
        K.gdy('zold partii wpiete' in w.tresc and 'BRAK' not in w.tresc,
              'pierwsza doba: latki modelu finansow wpiete', wycinek(w.tresc[12:], 110), w.nr)
    elif ses.bloki:
        K.dodaj(UW, 'pierwsza doba: latki modelu finansow wpiete',
                'nie ma linii "SoldierPay: latki modelu finansow ..." - latki na model nie zalozone')
    zo = ses.ser('Zold')
    if not zo:
        K.brak('co dobe linia "Zold:"', 'nie ma jej w logu')
        return
    wl = [1 for _, d in zo if d.get('tarcza_wl')]
    t = f.get('soldierpay_tarcza')
    k6 = [(i, bi, d) for i, (bi, d) in enumerate(zo) if d.get('k6')]
    if k6:
        # Ogniwo 111 (K6) w grze: regulator kas miast niczego nie kasuje, tarcza zoldu nie dopisuje znacznikow i nie rusza
        # wyniku. "wlaczona" w linii "Zold:" znaczy wtedy tylko, ze ustawienie Town Wage Shield stoi - nie wolno tego liczyc
        # za dzialajaca tarcze. Kontrola sprawdza to, co po K6 ma byc prawda: znacznik 0 i "nie skasowal dzis" 0.
        brudne = [(bi, d) for i, bi, d in k6 if i > 0 and ((d.get('znacznik') or 0) > 0 or (d.get('nie_skasowal') or 0) > 0)]
        zb = [1 for _, _, d in k6 if d.get('tarcza_zbedna')]
        tz = f.get('tarcza_zbedna')
        K.gdy(len(k6) == len(zo) and not brudne,
              'tarcza zoldu NIECZYNNA przy K6 (ogniwo 111): kas miast nic nie kasuje, znacznik 0',
              'K6 w %d z %d dob; ustawienie tarczy wlaczone w %d dobach (dopisek "ZBEDNA i nieczynna" w %d); znacznik ostatnio %s, '
              '"regulator nie skasowal dzis": najwiecej %s; linia "SoldierPay: tarcza ... zbedna i nieczynna": %s'
              % (len(k6), len(zo), len(wl), len(zb), skr(zo[-1][1].get('znacznik')), skr(max(_wart(zo, 'nie_skasowal') or [0])),
                 ('jest (linia %d)' % tz.nr) if tz is not None else 'nie ma'),
              (brudne[0][1] if brudne else zo[-1][1]).get('_nr'))
    else:
        K.gdy(len(wl) == len(zo) and (t is None or 'BRAK' not in t.tresc), 'tarcza zoldu w kasach miast wlaczona (decyzja 06.10)',
              'wlaczona w %d z %d dob; znacznik zoldu w kasach %s, regulator nie skasowal dzis: mediana %s; latka regulatora: %s'
              % (len(wl), len(zo), skr(zo[-1][1].get('znacznik')), _zakres(_wart(zo, 'nie_skasowal')),
                 ('BRAK - tarcza nie dziala' if 'BRAK' in t.tresc else 'wpieta') if t is not None else 'linii jeszcze nie ma'),
              (t.nr if t is not None else zo[-1][1].get('_nr')))
    roz = [abs(d['rozjazd']) for _, d in zo if d.get('rozjazd') is not None]
    zeszlo = _wart(zo, 'zeszlo_razem')
    if roz and zeszlo:
        K.gdy(max(roz) <= max(10, ZOLD_ROZJAZD_PROC / 100.0 * mediana(zeszlo)),
              'zold nie znika: z kies zeszlo = przekazane + nieprzekazane',
              'z kies zeszlo na dobe: mediana %s; do sakiewek ludzi %s, do kas miast %s, do kas zamkow %s; najwiekszy rozjazd %s'
              % (_zakres(zeszlo), skr(mediana(_wart(zo, 'do_sakiewek'))), skr(mediana(_wart(zo, 'do_miast'))),
                 skr(mediana(_wart(zo, 'do_zamkow'))), skr(max(roz))), zo[-1][1].get('_nr'))
    prz = dict(ses.ser('Przeplywy osad'))
    pary = []
    for bi, d in zo:
        k = prz.get(bi)
        if k is not None and d.get('naliczony_razem') is not None and k.get('zold_rody') is not None:
            pary.append((d['naliczony_razem'], (k.get('zold_rody') or 0) + (k.get('zold_garnizony') or 0)))
    if pary:
        naj = max(abs(a - b) / float(b) for a, b in pary if b) if any(b for _, b in pary) else 0.0
        K.gdy(100.0 * naj <= ZOLD_KSIEGA_ROZJAZD_PROC, '"Zold: naliczony" bliski licznikowi zoldu ksiegi pieniadza',
              'porownane %d dob, najwieksza roznica %.1f%% (ostatnio %s wobec %s)' % (len(pary), 100.0 * naj, skr(pary[-1][0]),
                                                                                    skr(pary[-1][1])), zo[-1][1].get('_nr'))
    else:
        K.brak('"Zold: naliczony" bliski licznikowi zoldu ksiegi pieniadza', 'nie ma linii "Przeplywy osad:" w dobach z linia "Zold:"')
    slepe = sum(_wart(zo, 'slepe'))
    K.gdy(slepe == 0, '"rody z pusta kiesa i nieznanym saldem" = 0', 'razem %d w %d dobach' % (slepe, len(zo)),
          zo[-1][1].get('_nr'))
    pd, ps, pn = _pierw_ost(zo, 'dlug_rodow'), _pierw_ost(zo, 'sak_sieroty'), _pierw_ost(zo, 'sak_najwieksza')
    rosnie = (ps is not None and len(zo) >= 3 and ps[1] > ps[0] > 0) or (pd is not None and len(zo) >= 3 and pd[1] > pd[0]
                                                                          and pd[1] >= 5)
    K.gdy(not rosnie, 'nie rosna: rody z dlugiem, sakiewki po zniklych partiach',
          'rodow z dlugiem %s -> %s; sakiewki po zniklych partiach %s -> %s zl; najwieksza sakiewka %s -> %s zl'
          % ((skr(pd[0]), skr(pd[1])) + (skr(ps[0]), skr(ps[1])) + (skr(pn[0]), skr(pn[1])))
          if pd and ps and pn else 'brak pozycji w linii', zo[-1][1].get('_nr'))
    bil = dict(ses.ser('Pieniadz swiata (bilans)'))
    pary = []
    for bi, d in zo:
        b = bil.get(bi)
        cz = [d.get('do_sakiewek'), d.get('do_miast'), d.get('do_zamkow')]
        if b is not None and b.get('oddany') is not None and all(v is not None for v in cz):
            pary.append((sum(cz), b['oddany']))
    if pary:
        zle = [1 for a, b in pary if abs(a - b) > max(10, ZOLD_KSIEGA_ROZJAZD_PROC / 100.0 * max(a, b))]
        K.gdy(not zle, 'bilans swiata: "zold oddany do obiegu przez SoldierPay" = "Zold:"',
              'zgodne w %d z %d dob (ostatnio %s wobec %s)' % (len(pary) - len(zle), len(pary), skr(pary[-1][0]),
                                                             skr(pary[-1][1])), zo[-1][1].get('_nr'))
    elif ses.fakty.get('uklad_ksiegi') == UKLAD_102B:
        K.brak('bilans swiata: "zold oddany do obiegu przez SoldierPay"', 'bilans bez tej pozycji w dobach z linia "Zold:"')
    zw, sk = ses.ser('Korona (zwrot zoldu)'), ses.ser('Skarbce')
    wojna = any((d.get('wojna') or 0) > 0 for _, d in sk)
    if zw:
        K.gdy(sum(_wart(zw, 'potkniecia')) == 0, 'skarbiec krolestwa w wojnie zwraca rodom polowe zoldu',
              'zwrot w %d dobach, razem %s zl z naleznych %s; pusty skarbiec: najwiecej %s krolestw'
              % (len(zw), skr(sum(_wart(zw, 'zwrot'))), skr(sum(_wart(zw, 'nalezne'))), skr(max(_wart(zw, 'pusty') or [0]))),
              zw[-1][1].get('_nr'))
    elif sk:
        K.gdy(not wojna or len(ses.pelne()) < 2, 'skarbiec krolestwa w wojnie zwraca rodom polowe zoldu',
              'nie ma linii "Korona: ... zwrot zoldu"; krolestw w wojnie: %s' % ('sa' if wojna else 'brak'))
    sa = ses.ser('Sakiewka ludzi')
    zy = _wart(sa, 'na_zycie')
    if zy:
        K.gdy(mediana(zy) >= 100000, 'ludzie wydaja zold w miastach (setki tysiecy dziennie)',
              'na zycie w miastach: mediana %s zl na dobe; zold wplacony do sakiewek: mediana %s'
              % (_zakres(zy), skr(mediana(_wart(sa, 'zold')))), sa[-1][1].get('_nr'))


def _k108(K, ses):
    f = ses.fakty
    w = _start(K, ses, '108', 'start: "PeopleUnit: jednostka ludzi ... CZYNNA", bez "BRAK"',
               ['CZYNNA', 'tabor wsi - uzupelnienie', 'tabor wsi - nowy', 'pobor wymuszony gracza', 'incydenty gracza - licznik'])
    if w is not None:
        m = re.search(r'zadania gracza (\d+) z (\d+)', w.tresc)
        K.gdy(bool(m) and m.group(1) == m.group(2) and 'wyrzutki liczone od ludnosci wsi' in w.tresc,
              'start: zadania gracza 13 z 13, wyrzutki liczone od ludnosci wsi',
              ('zadania gracza %s z %s' % m.groups() if m else 'brak pozycji "zadania gracza"') +
              (', wyrzutki liczone od ludnosci wsi' if 'wyrzutki liczone od ludnosci wsi' in w.tresc else
               ', wyrzutki liczone od hearth wsi (Outlaws Counted By People wylaczone)'), w.nr)
    if f.get('nowa_kampania'):
        kal, pula = f.get('kalibracja'), f.get('pula_nr')
        if kal is not None and pula is not None:
            K.gdy(kal.nr < pula, 'start kampanii: "kalibracja ludnosci" PRZED pula wyrzutkow',
                  'kalibracja w linii %d, pula poczatkowa w linii %d' % (kal.nr, pula), kal.nr)
        if 'zdjeto_hearth' in f:
            K.gdy('1 czlowiek = 1/k hearth' in (f.get('zdjeto_jak') or ''),
                  'pula poczatkowa wyrzutkow: zdjeto 1/k hearth za czlowieka',
                  'pula %s ludzi, zdjeto %s hearth: %s' % (skr(f.get('pula_poczatkowa')), skr(f.get('zdjeto_hearth')),
                                                          obetnij(f.get('zdjeto_jak') or '', 70)), pula)
        elif pula is not None:
            K.dodaj(UW, 'pula poczatkowa wyrzutkow: zdjeto 1/k hearth za czlowieka',
                    'linia "Wyrzutki: pula poczatkowa" bez dopisku "zdjeto N hearth" (pula %s)' % skr(f.get('pula_poczatkowa')),
                    pula)
        sw = f.get('srednia_swiata')
        if sw is not None:
            m = re.search(r'srednia swiata ' + RX_F + ' ludzi wsi na punkt hearth', sw.tresc)
            K.gdy(bool(m) and 150 <= float(m.group(1).replace(',', '.')) <= 240,
                  'srednia swiata ludzi wsi na punkt hearth (rachunek: 191.2)', (m.group(1) if m else '?') + ' ludzi na hearth',
                  sw.nr)
    lu = [(bi, d) for bi, d in ses.ser('Ludzie') if d.get('jednostka')]
    if not lu:
        K.brak('co dobe odcinek "hearth za ludzi dzis" w linii "Ludzie:"', 'linie "Ludzie:" go nie maja (albo nie ma linii)')
        return
    czynne = [1 for _, d in lu if d.get('jednostka') == 'czynna']
    K.gdy(len(czynne) == len(lu), 'co dobe odcinek "hearth za ludzi dzis", jednostka czynna',
          'jest w %d dobach, jednostka czynna w %d' % (len(lu), len(czynne)), lu[0][1].get('_nr'))
    tl = _wart(lu, 'hz_tabory_ludzi')
    d0 = lu[0][1]
    K.gdy(sum(tl) > 0 or not any((d.get('tabory_wsi') or 0) > 0 for _, d in lu),
          'tabory wsi i lodzie: latka wolana (nie "za 0 ludzi")',
          'pierwsza doba: -%s hearth za %s ludzi (uzupelnien %s, nowych %s); razem %s ludzi w %d dobach; w taborach wsi %s ludzi'
          % (skr(d0.get('hz_tabory')), skr(d0.get('hz_tabory_ludzi')), skr(d0.get('hz_uzupelnien')), skr(d0.get('hz_nowych')),
             skr(sum(tl)), len(lu), skr(lu[-1][1].get('tabory_wsi'))), d0.get('_nr'))
    nasze, gra = sum(_wart(lu, 'hz_tabory')), sum(_wart(lu, 'hz_gra'))
    if nasze > 0:
        K.gdy(gra / nasze >= 10.0, 'wies oddaje za tabor ok. 100 razy mniej hearth niz w grze',
              'nasze -%s hearth wobec %s, ktore zdjelaby gra (%.0f razy mniej)' % (skr(round(nasze, 3)), skr(round(gra, 1)),
                                                                                  gra / nasze), lu[-1][1].get('_nr'))
    zle = [(k, sum(_wart(lu, k))) for k in ('hz_niezgodne', 'hz_bez_k', 'hz_potk')]
    K.gdy(all(v == 0 for _, v in zle), 'bez dopiskow "niezgodne z formula gry", "potkniecia"',
          'niezgodne %s, bez przelicznika kultury %s, potkniecia %s' % tuple(skr(v) for _, v in zle), lu[-1][1].get('_nr'))
    K.dodaj(OK, 'pomiar: hearth za wyrzutkow, pobor, zadania, incydenty',
            'w las -%s za %s ludzi, z lasu +%s za %s ludzi; pobor -%s, zadania %s, incydenty %s (suma %d dob)'
            % (skr(round(sum(_wart(lu, 'hz_wyrzutki')), 3)), skr(round(sum(_wart(lu, 'hz_wyrzutki_ludzi')), 1)),
               skr(round(sum(_wart(lu, 'hz_powrot')), 3)), skr(round(sum(_wart(lu, 'hz_powrot_ludzi')), 1)),
               skr(round(sum(_wart(lu, 'hz_pobor')), 3)), skr(round(sum(_wart(lu, 'hz_zadania')), 3), True),
               skr(round(sum(_wart(lu, 'hz_incydenty')), 1), True), len(lu)), lu[-1][1].get('_nr'))
    wy = ses.ser('Wyrzutki')
    if wy:
        d = wy[0][1]
        K.dodaj(OK, 'pomiar: bandy pierwszej doby (test 15:22: nowe 391, pula 299)',
                'bandy nowe %s (%s ludzi), odmowione %s, puste usuniete %s; pula po pierwszej dobie %s; band %s -> %s'
                % (skr(d.get('bandy_nowe')), skr(d.get('nowe_ludzi')), skr(d.get('odmowione')), skr(d.get('puste')),
                   skr(d.get('pula')), skr(d.get('band')), skr(wy[-1][1].get('band'))), d.get('_nr'))
    ln = ses.ser('Ludnosc')
    if ln and ln[0][1].get('mln') is not None:
        K.gdy(abs(ln[0][1]['mln'] - 52.6) <= 0.25 or not f.get('nowa_kampania'), '"Ludnosc:" pierwszej doby nadal 52.6 mln',
              '%.1f mln w pierwszej dobie sesji%s' % (ln[0][1]['mln'], '' if f.get('nowa_kampania') else ' (wczytany zapis)'),
              ln[0][1].get('_nr'))


def _k109(K, ses):
    f = ses.fakty
    w = _start(K, ses, '109', 'start: przyrost naturalny wsi CZYNNY, latka wpieta',
               ['CZYNNY', 'wpieta'], ('BRAK', 'NIE wpiet', 'wylaczony w ustawieniach'))
    if w is not None:
        m = re.search(r'g = (.*?) \[% rocznie\], korytarz (\S+?)[;,]', w.tresc)
        wzor = 'g = 0.5 + 0.6 x dobrobyt - 0.6 x niebezpieczenstwo (glod wylaczony)'
        K.gdy(bool(m) and ('g = ' + m.group(1)) == wzor and m.group(2) == '-1.3..+0.8',
              'wzor przyrostu z ustawien domyslnych, korytarz -1.3..+0.8',
              ('g = %s, korytarz %s' % m.groups()) if m else 'wzoru nie ma w linii startowej (oczekiwany: %s)' % wzor,
              w.nr)
    md = f.get('model_dobrobytu')
    if md is not None:
        m = re.search(r'postfiksy w kolejnosci biegu: (.*?); prefiksy', md.tresc)
        lancuch = [x.strip() for x in m.group(1).split(' -> ')] if m else []
        ost = lancuch[-1] if lancuch else '?'
        K.gdy('PopulationLaw.GrowthPostfix' in ost and 'dopisuje PO naszej latce' not in md.tresc,
              'pierwsza doba: GrowthPostfix biegnie ostatni',
              '%d postfiksow, ostatni: %s%s' % (len(lancuch), obetnij(ost, 60), '; model dopisuje PO naszej latce'
                                                if 'dopisuje PO naszej latce' in md.tresc else ''), md.nr)
    elif ses.bloki:
        K.brak('pierwsza doba: GrowthPostfix biegnie ostatni', 'nie ma linii "PopulationLaw: czynny model dobrobytu gry ..."')
    pr = ses.ser('Ludzie (przyrost)')
    if not pr:
        K.brak('co dobe linia "Ludzie: przyrost naturalny +N ludzi"', 'nie ma jej w logu')
        return
    wyl = [1 for _, d in pr if d.get('wylaczony')]
    n, g = _wart(pr, 'ludzi'), _wart(pr, 'g_srednio')
    if n:
        K.gdy(not wyl and all(PRZYROST_MIN_LUDZI <= x <= PRZYROST_MAKS_LUDZI for x in n),
              'przyrost naturalny N w przedziale %d-%d (oczekiwane 250-600)' % (PRZYROST_MIN_LUDZI,
                                                                                             PRZYROST_MAKS_LUDZI),
              'N: mediana %s ludzi dziennie w %d dobach; w rok ok. %s ludzi%s' % (_zakres(n), len(n),
                                                                                skr(mediana(_wart(pr, 'w_rok')), True),
                                                                                ('; WYLACZONY w %d dobach' % len(wyl)) if wyl
                                                                                else ''), pr[-1][1].get('_nr'))
    else:
        K.dodaj(UW, 'przyrost naturalny N w przedziale %d-%d (oczekiwane 250-600)' % (PRZYROST_MIN_LUDZI, PRZYROST_MAKS_LUDZI),
                'przyrost WYLACZONY we wszystkich %d dobach - hearth wsi liczy gra: %s ludzi dziennie'
                % (len(pr), _zakres(_wart(pr, 'gra_ludzi'))), pr[-1][1].get('_nr'))
    if g:
        K.gdy(0.2 <= mediana(g) <= 0.5, '"g srednio" 0.2-0.5% rocznie', 'g srednio: mediana %s%% rocznie; wsie: rosnie %s, kurczy '
              'sie %s, bez zmiany %s (ostatnia doba)' % (_zakres(g), skr(pr[-1][1].get('rosnie')), skr(pr[-1][1].get('kurczy')),
                                                        skr(pr[-1][1].get('bez_zmiany'))), pr[-1][1].get('_nr'))
    # N x dni roku / ludnosc wsi ma dawac g (ludnosc wsi z linii "Ludzie:")
    lu = dict(ses.ser('Ludzie'))
    roz = []
    for bi, d in pr:
        wsie = (lu.get(bi) or {}).get('wsie_mln')
        if d.get('w_rok') is not None and d.get('g_srednio') is not None and wsie:
            roz.append(abs(100.0 * d['w_rok'] / (wsie * 1e6) - d['g_srednio']))
    if roz:
        K.gdy(max(roz) <= 0.05, 'rachunek sie zgadza: N x dni roku / ludnosc wsi = g srednio',
              'najwieksza roznica %.3f pkt %% w %d dobach' % (max(roz), len(roz)), pr[-1][1].get('_nr'))
    uch = _wart(pr, 'uch_ludzi')
    K.dodaj(OK, 'pomiar: powrot uchodzcow (osobno, nie wchodzi do N)',
            ('osobno powrot uchodzcow: mediana %s ludzi dziennie w %d dobach, najwiecej wsi przy dnie %s'
             % (_zakres(uch), len(uch), skr(max(_wart(pr, 'uch_wsi') or [0])))) if uch else 'zadna wies nie spadla ponizej 40 hearth',
            pr[-1][1].get('_nr'))
    rz = _wart(pr, 'reszta')
    if rz:
        naj = max(rz, key=abs)
        K.gdy(abs(naj) <= PRZYROST_RESZTA_HEARTH, 'RESZTA hearth wsi bliska zera (BetterEconomy: +65..135)',
              'RESZTA: mediana %s hearth na dobe, najwieksza %s; zmiana hearth wsi od wczoraj: mediana %s'
              % (_zakres(rz), skr(naj, True), skr(mediana(_wart(pr, 'od_wczoraj')), True)), pr[-1][1].get('_nr'))
    else:
        K.brak('RESZTA hearth wsi bliska zera (BetterEconomy: +65..135)', 'rozliczenie zmiany hearth zaczyna sie od drugiej doby po wczytaniu')
    progi = [(d.get('prog_0'), d.get('prog_200'), d.get('prog_600')) for _, d in pr if d.get('prog_600') is not None]
    if progi:
        K.gdy(progi[0] == progi[-1], 'wsie wedle progow produkcji stale (nowa kampania 12/522/37)',
              'ponizej 200 / 200-599 / od 600 hearth: %s / %s / %s -> %s / %s / %s' % (progi[0] + progi[-1]), pr[-1][1].get('_nr'))
    potk = sum(_wart(pr, 'potk_rachunek')) + sum(_wart(pr, 'potk_linia')) + sum(_wart(pr, 'bez_k'))
    lus = ses.ser('Ludzie')
    pc = [100.0 * abs(d['ludnosc_zm']) / (d['ludnosc_mln'] * 1e6) for _, d in lus if d.get('ludnosc_zm') is not None
          and d.get('ludnosc_mln')]
    ln = [d['mln'] for _, d in ses.ser('Ludnosc') if d.get('mln')]
    if pc:
        K.gdy(max(pc) <= LUDNOSC_MAKS_ZMIANA_PROC, 'ludnosc swiata stoi (zmiana najwyzej %.2f%% dziennie)'
              % LUDNOSC_MAKS_ZMIANA_PROC, 'najwieksza zmiana dobowa %.3f%%, mediana %.3f%%; "Ludnosc:" %s -> %s mln'
              % (max(pc), mediana(pc), skr(ln[0]) if ln else '-', skr(ln[-1]) if ln else '-'), lus[-1][1].get('_nr'))
    elif len(ln) >= 2:
        dob = max(1, len(ln) - 1)
        zm = 100.0 * abs(ln[-1] - ln[0]) / ln[0] / dob
        K.gdy(zm <= LUDNOSC_MAKS_ZMIANA_PROC, 'ludnosc swiata stoi (zmiana najwyzej %.2f%% dziennie)' % LUDNOSC_MAKS_ZMIANA_PROC,
              '"Ludnosc:" %.1f -> %.1f mln w %d dob = %.3f%% dziennie' % (ln[0], ln[-1], dob, zm))
    else:
        K.brak('ludnosc swiata stoi (zmiana najwyzej %.2f%% dziennie)' % LUDNOSC_MAKS_ZMIANA_PROC, 'za malo linii "Ludzie:" / "Ludnosc:"')
    zam_ = [d for _, d in pr if d.get('zamrozone') == 1 and d.get('miasta_mln') is not None]
    if zam_:
        st = [d['miasta_mln'] for d in zam_]
        K.gdy(len(zam_) == len(pr) and max(st) - min(st) <= 0.0015, 'ludnosc miast zamrozona (stan nie idzie za dobrobytem)',
              'miasta %.3f mln w %s miastach, stale w %d z %d dob; z dzisiejszego dobrobytu byloby %s mln (dryf %s ludzi)'
              % (st[-1], skr(zam_[-1].get('miasta_n')), len(zam_), len(pr), skr(zam_[-1].get('miasta_live')),
                 skr(zam_[-1].get('miasta_dryf'), True)), pr[-1][1].get('_nr'))
    else:
        K.dodaj(UW, 'ludnosc miast zamrozona (stan nie idzie za dobrobytem)',
                'linia przyrostu nie podaje stanu zamrozonego (Town People Frozen wylaczone albo ludnosc nieskalibrowana)',
                pr[-1][1].get('_nr'))
    K.gdy(potk == 0, 'bez potkniec i bez wsi bez przelicznika ludzi', 'potkniecia i wsie bez przelicznika razem: %s' % skr(potk),
          pr[-1][1].get('_nr'))


def _przyciecie(K, ses, klucz, co, przedzial, osady, flaga):
    """Kontrola jednorazowego przyciecia daru startowego kas (ogniwa 110 i 111): linia "CastlePurse: / TownPurse: poczatek
    kampanii ... zdjeto N" w pierwszej dobie nowej kampanii albo "zapis z X. doby ... wczytany pierwszy raz"."""
    f = ses.fakty
    tr, bez = f.get(klucz + '_trim'), f.get(klucz + '_trim_bez')
    if tr:
        opis = ('kasy %s -> %s, zdjeto %s w %d z %d %s, najwiecej %s; zapas kupcow razem %s'
                % (skr(tr['przed']), skr(tr['po']), skr(tr['zdjeto']), tr['zdjeto_osad'], tr['osad'], osady, skr(tr['najwiecej']),
                   skr(tr['zapas'])))
        if tr['nowa']:
            K.gdy(przedzial[0] <= tr['zdjeto'] <= przedzial[1], co, 'poczatek kampanii (doba %s): %s' % (skr(tr['doba']), opis),
                  tr['nr'])
        else:
            K.dodaj(OK, 'pomiar: ' + co, 'zapis z %s. doby wczytany pierwszy raz (regulator gry zostawil ok. %s%% daru): %s'
                    % (skr(tr['doba']), skr(tr['zostalo_proc']), opis), tr['nr'])
    elif bez is not None:
        K.dodaj(UW, co, 'BEZ przyciecia: ' + obetnij(bez.tresc.split(': ', 1)[-1], 110), bez.nr)
    elif f.get('nowa_kampania') and ses.bloki:
        K.dodaj(UW, co, 'nowa kampania, a linii o przycieciu daru nie ma (ustawienie przyciecia albo cala kasa wylaczone; bez '
                        'przyciecia dar splynie zaworem do panow)')
    else:
        K.brak(co, 'wczytany zapis albo log bez pierwszej doby: przyciecie zrobione wczesniej (flaga %s w sejwie)' % flaga)


def _reszta_mala(wiersze):
    """Czy reszta [R] linii kas jest mala: ponizej RESZTA_KAS_K5_MIN_ABS albo ponizej RESZTA_KAS_PROC_RUCHU % ruchu."""
    for k in wiersze:
        re_, ruch = k.get('reszta'), k.get('ruch')
        if re_ is None or abs(re_) < RESZTA_KAS_K5_MIN_ABS:
            continue
        if not ruch or 100.0 * abs(re_) / (ruch + abs(re_)) > RESZTA_KAS_PROC_RUCHU:
            return False
    return True


def _k110(K, ses):
    f = ses.fakty
    _start(K, ses, '110', 'start: "CastlePurse: kasa zamku ... CZYNNA", regulator = 0 w 3 modelach, bez "BRAK"',
           ['CZYNNA', '"zakupy" ludnosci zamkow bez zlota z niczego', 'regulator kasy zamku = 0 w 3 modelach',
            'dar startowy przycinany w pierwszej dobie nowej kampanii: tak', 'tabor do zamku tylko gdy zamek ma czym zaplacic: tak'],
           ('BRAK', 'NIE wpiet', 'WYLACZON'))
    _przyciecie(K, ses, 'castle', 'pierwsza doba: dar startowy kas zamkow przyciety raz (ok. 5.09 mln w 130 zamkach)',
                DAR_ZAMKOW_ZDJETO, 'zamkow', 'arm_castlepurse')
    kz = ses.ser('Kasy zamkow')
    if not kz:
        K.brak('co dobe linia "Kasy zamkow:"', 'nie ma jej w logu' + (' - kasa zamku WYLACZONA w ustawieniach'
                                                                     if f.get('kasy_zamkow_wyl') is not None else ''))
        return
    ost = kz[-1][1]
    chc, cof, tick = sum(_wart(kz, 'regulator_chcial')), sum(_wart(kz, 'cofniete')), _wart(kz, 'regulator_tickow')
    K.gdy(chc > 0 and cof > 0, 'regulator gry zablokowany i "zakupy" ludnosci zamkow cofniete (obie liczby niezerowe)',
          'regulator chcial dosypac %s i skasowac %s (tickow zamkow na dobe: mediana %s przy %s zamkach); "zakupy" cofniete %s '
          'w %d dobach' % (skr(sum(_wart(kz, 'chcial_dosypac'))), skr(sum(_wart(kz, 'chcial_skasowac'))), skr(mediana(tick)),
                           skr(ost.get('zamkow')), skr(cof), len(kz)), ost.get('_nr'))
    po, dn = _pierw_ost(kz, 'danina'), _wart(kz, 'danina')
    if po:
        K.gdy(max(dn) > 0 and po[1] >= po[0], 'danina podzamcza plynie do panow i rosnie w pierwszych tygodniach',
              'danina %s -> %s na dobe (doby %d-%d; mediana %s), placi %s -> %s zamkow; ostatnio bez poboru: kasa nie ponad '
              'zapasem %s, oblezone %s, bez pana %s' % (skr(po[0]), skr(po[1]), po[2], po[3], skr(mediana(dn)),
                                                        skr(kz[0][1].get('danina_zamkow')), skr(ost.get('danina_zamkow')),
                                                        skr(ost.get('bez_nadwyzki')), skr(ost.get('oblezone')),
                                                        skr(ost.get('bez_pana'))), ost.get('_nr'))
    potk = sum(_wart(kz, 'potkniecia'))
    K.gdy(max(_wart(kz, 'ponad_bk') or [0]) == 0 and potk == 0, '"zamki ponad limitem kasy BK" = 0, bez potkniec',
          'najwiecej %s zamkow ponad limitem (BK kasuje tam 1%% dziennie); najwieksza nadwyzka ostatnio %s; potkniecia razem %d w '
          '%d dobach' % (skr(max(_wart(kz, 'ponad_bk') or [0])), skr(ost.get('naj_nadwyzka')), potk, len(kz)), ost.get('_nr'))
    po = _pierw_ost(kz, 'brakuje')
    if po:
        K.gdy(po[1] <= po[0], '"do zapasu brakuje" maleje (kasy zamkow dochodza do zapasu kupcow)',
              'brakuje %s -> %s (doby %d-%d); stan %s -> %s przy zapasie %s; ponad zapasem %s -> %s'
              % (skr(po[0]), skr(po[1]), po[2], po[3], skr(kz[0][1].get('stan')), skr(ost.get('stan')), skr(ost.get('zapas')),
                 skr(kz[0][1].get('ponad')), skr(ost.get('ponad'))), ost.get('_nr'))
    ks = dict(ses.ser('Przeplywy osad (kasy zamkow)'))
    wsp = [(bi, d, ks[bi]) for bi, d in kz if bi in ks]
    if wsp:
        zle = [1 for _, _, k in wsp if (k.get('zakupy') or 0) != 0 or (k.get('regulator') or 0) != 0]
        bez_tickow = [1 for _, _, k in wsp if not (k.get('reg_tickow') or 0) > 0]
        K.gdy(not zle and not bez_tickow, 'ksiega kas zamkow: "zakupy" +0 i regulator +0 przy tickach > 0',
              'zgodne w %d z %d dob; ticki regulatora: mediana %s, konsumpcji: mediana %s'
              % (len(wsp) - len(zle), len(wsp), skr(mediana([k.get('reg_tickow') for _, _, k in wsp])),
                 skr(mediana([k.get('zakupy_tickow') for _, _, k in wsp]))), wsp[-1][2].get('_nr'))
        roz = [1 for _, d, k in wsp if d.get('danina') is not None
               and abs(-(k.get('danina_podzamcza') or 0) - d['danina']) > max(10, 0.01 * d['danina'])]
        rs = [k.get('reszta') for _, _, k in wsp if k.get('reszta') is not None]
        K.gdy(not roz and _reszta_mala([k for _, _, k in wsp]),
              'ksiega kas zamkow: "danina podzamcza" ujemna i rowna daninie z "Kasy zamkow:", reszta [R] bliska 0',
              'danina zgodna w %d z %d dob (ostatnio %s w ksiedze wobec %s); reszta: mediana %s; pozostale moduly ticku: mediana %s'
              % (len(wsp) - len(roz), len(wsp), skr(wsp[-1][2].get('danina_podzamcza'), True), skr(wsp[-1][1].get('danina')),
                 _zakres(rs), skr(mediana([k.get('pozostale') for _, _, k in wsp]), True)), wsp[-1][2].get('_nr'))
    else:
        K.brak('ksiega kas zamkow: "zakupy" +0, regulator +0, danina podzamcza, reszta [R]',
               'nie ma linii "Przeplywy osad (kasy zamkow):" w dobach z linia "Kasy zamkow:" (ksiega pisze od drugiej doby)')
    dw = [(bi, d) for bi, d in ses.ser('Dowoz') if d.get('daleki_targ') is not None]
    pz = _pierw_ost(ses.ser('Dowoz (skutki)'), 'zatk_zamkowe_proc')
    co = '"Dowoz:" targ za daleko + na daleki targ razem ok. 6 dziennie; zatkane magazyny wsi zamkowych nie rosna'
    if dw:
        razem = [(d.get('za_daleko') or 0) + d['daleki_targ'] for _, d in dw]
        K.gdy(pz is None or pz[1] <= pz[0] + ZATKANE_ROZNICA_PP, co,
              'za daleko + daleki targ: mediana %s na dobe, z tego na daleki targ (zamek nie mial czym zaplacic): mediana %s, razem '
              '%s w %d dobach; zatkane magazyny wsi zamkowych %s%% -> %s%%'
              % (_zakres(razem), skr(mediana(_wart(dw, 'daleki_targ'))), skr(sum(_wart(dw, 'daleki_targ'))), len(dw),
                 skr(pz[0]) if pz else '-', skr(pz[1]) if pz else '-'), dw[-1][1].get('_nr'))
    else:
        K.brak(co, 'linia "Dowoz:" bez dopisku "na daleki targ, bo zamek nie mial czym zaplacic" (albo nie ma linii "Dowoz:")')
    zo, zw = ses.ser('Zold'), ses.ser('Korona (zwrot zoldu)')
    K.dodaj(OK, 'pomiar: danina podzamcza wobec zoldu zalog zamkow i zwrotu korony (ryzyko 12)',
            'danina: mediana %s na dobe; "Zold: do kas zamkow": mediana %s; "Korona: zwrot zoldu": mediana %s; zalogi bez zwrotu '
            'korony: mediana %s' % (skr(mediana(dn)), skr(mediana(_wart(zo, 'do_zamkow'))), skr(mediana(_wart(zw, 'zwrot'))),
                                    skr(mediana(_wart(zo, 'bez_zwrotu_korony')))), ost.get('_nr'))


def _k111(K, ses):
    f = ses.fakty
    _start(K, ses, '111', 'start: "TownPurse: kasa miasta ... CZYNNA, tryb regulatora 1", 3 modele, zawor 7% (panu 67%), bez "BRAK"',
           ['CZYNNA, tryb regulatora 1', 'przez latki CastlePurse w 3 modelach', '"zakupy" mieszczan bez zlota z niczego: tak',
            'wpiete (prefiks na BK HandleMarketGold)', 'zawor 7% nadwyzki dziennie, z tego panu 67%',
            'dar startowy przycinany w pierwszej dobie nowej kampanii: tak',
            'zwrot zoldu z korony bez zalog, ktorych zold wraca panu z kasy osady: tak'], ('BRAK', 'NIE wpiet', 'WYLACZON'))
    _przyciecie(K, ses, 'town', 'pierwsza doba: dar startowy kas miast przyciety raz (ok. 13.98 mln w 97 miastach)',
                DAR_MIAST_ZDJETO, 'miast', 'arm_townpurse')
    km = ses.ser('Kasy miast')
    if not km:
        K.brak('co dobe linia "Kasy miast:"', 'nie ma jej w logu' + (' - ' + obetnij(f['kasy_miast_wyl'].tresc.split(' | ', 1)[-1],
                                                                                  80) if f.get('kasy_miast_wyl') is not None
                                                                    else ''))
        return
    ost = km[-1][1]
    kas, cof = _wart(km, 'kasowanie_zabl'), _wart(km, 'cofniete')
    zost = [1 for _, d in km if d.get('zakupy_zostaja')]
    K.gdy(sum(kas) > 0 and sum(cof) > 0 and not zost, '"kasowanie zablokowane" i "zakupy" mieszczan cofniete niezerowe',
          'kasowanie zablokowane: mediana %s na dobe (cwierc nadwyzki, nie strata); "zakupy" cofniete: mediana %s (pierwszy pomiar '
          '"zakupow" mieszczan)%s' % (_zakres(kas), _zakres(cof), ('; ZLOTO Z "ZAKUPOW" ZOSTAJE w %d dobach' % len(zost))
                                      if zost else ''), ost.get('_nr'))
    zes = _wart(km, 'zeszlo')
    roz = [1 for _, d in km if abs(d.get('zawor_rozjazd') or 0) > 2]
    nd = [1 for _, d in km if d.get('zawor_nie_dziala')]
    K.gdy(bool(zes) and sum(zes) > 0 and not roz and not nd, 'zawor: "zeszlo = panom + skarbcom krolestw" (pan 2/3, korona 1/3)',
          ('zeszlo: mediana %s na dobe = panom %s (%s%%) + skarbcom %s; placi %s z %s miast z panem; renta nalezna (dawny pulap) %s'
           % (_zakres(zes), skr(mediana(_wart(km, 'panom'))), skr(mediana(_wart(km, 'panom_proc'))),
              skr(mediana(_wart(km, 'koronie'))), skr(ost.get('panom_miast')), skr(ost.get('z_panem')), skr(ost.get('nalezna'))))
          if zes else ('zawor NIE DZIALA - renty od ludnosci wylaczone' if nd else 'linia bez pozycji "zeszlo"'), ost.get('_nr'))
    potk = sum(_wart(km, 'potkniecia'))
    K.gdy(max(_wart(km, 'ponizej_1000') or [0]) == 0 and potk == 0, '"miast z kasa ponizej 1000: 0", bez potkniec',
          'najwiecej %s miast ponizej 1000; ponad limitem kasy BK najwiecej %s (gorna galaz BK: %s, ostatnio w %s miastach i %s '
          'zamkach, BK skasowalby ok. %s); potkniecia razem %d'
          % (skr(max(_wart(km, 'ponizej_1000') or [0])), skr(max(_wart(km, 'ponad_bk') or [0])), ost.get('bk') or '?',
             skr(ost.get('bk_miast')), skr(ost.get('bk_zamkow')), skr(ost.get('bk_zloto')), potk), ost.get('_nr'))
    po = _pierw_ost(km, 'brakuje')
    dos = _wart(km, 'dosypal')
    tryb2 = [1 for _, d in km if (d.get('tryb') or 0) >= 2 and (d.get('dosypal') or 0) > 0]
    stabilne = po is None or po[1] <= po[0] * 1.25 + KASY_BRAKUJE_MIN_ABS
    K.gdy(stabilne and not tryb2 and (not dos or mediana(dos) <= KASY_MIAST_DOSYPKA_MAKS),
          '"do zapasu brakuje" stabilne; pomiar "dosypal z niczego" (kurek trybu 1, szacunek 50-160 tys. na dobe)',
          'brakuje %s -> %s (stan %s -> %s przy zapasie %s); dosypal: mediana %s na dobe, razem %s w %d dobach (tickow miast: '
          'mediana %s), dosypka zablokowana razem %s; tryb regulatora %s'
          % (skr(po[0]) if po else '-', skr(po[1]) if po else '-', skr(km[0][1].get('stan')), skr(ost.get('stan')),
             skr(ost.get('zapas')), _zakres(dos), skr(sum(dos)), len(km), skr(mediana(_wart(km, 'dosypal_tickow'))),
             skr(sum(_wart(km, 'dosypka_zabl'))), skr(ost.get('tryb'))), ost.get('_nr'))
    ks, bil = dict(ses.ser('Przeplywy osad (kasy miast)')), dict(ses.ser('Pieniadz swiata (bilans)'))
    wsp = [(bi, d, ks[bi]) for bi, d in km if bi in ks]
    if wsp:
        zle = [1 for _, d, k in wsp if (k.get('skasowal') or 0) != 0 or (not d.get('zakupy_zostaja') and (k.get('zakupy') or 0) != 0)]
        bw = [bil[bi] for bi, _, _ in wsp if bi in bil]
        bsk = max([b.get('skasowal') or 0 for b in bw] or [0])
        K.gdy(not zle and bsk == 0, 'ksiega kas miast: "zakupy" +0 i "skasowal 0"; bilans swiata: "regulator kas skasowal 0"',
              'ksiega zgodna w %d z %d dob, regulator kasy (sama dosypka): mediana %s; bilans: skasowal najwiecej %s, "zakupy" '
              'mieszkancow miast i zamkow najwiecej %s' % (len(wsp) - len(zle), len(wsp),
                                                         skr(mediana([k.get('regulator') for _, _, k in wsp]), True), skr(bsk),
                                                         skr(max([b.get('zakupy') or 0 for b in bw] or [0]))),
              wsp[-1][2].get('_nr'))
        rz = 0
        for _, d, k in wsp:
            for pole_k, pole_d in (('renty', 'panom'), ('udzial_korony', 'koronie')):
                if d.get(pole_d) is not None and abs(-(k.get(pole_k) or 0) - d[pole_d]) > max(10, 0.01 * d[pole_d]):
                    rz += 1
                    break
        rs = [k.get('reszta') for _, _, k in wsp if k.get('reszta') is not None]
        K.gdy(rz == 0 and _reszta_mala([k for _, _, k in wsp]),
              'ksiega kas miast: "renty" = czesc panow, "udzial korony" = czesc skarbcow (obie ujemne), reszta [R] mala',
              'zgodne w %d z %d dob (ostatnio renty %s wobec panom %s; udzial korony %s wobec skarbcom %s); reszta: mediana %s'
              % (len(wsp) - rz, len(wsp), skr(wsp[-1][2].get('renty'), True), skr(wsp[-1][1].get('panom')),
                 skr(wsp[-1][2].get('udzial_korony'), True), skr(wsp[-1][1].get('koronie')), _zakres(rs)), wsp[-1][2].get('_nr'))
    else:
        K.brak('ksiega kas miast: "zakupy" +0, "skasowal 0", renty i udzial korony, reszta [R]',
               'nie ma linii "Przeplywy osad (kasy miast):" w dobach z linia "Kasy miast:" (ksiega pisze od drugiej doby)')
    wg = ses.wiersze.get('Skarbce') or {}
    spadki = defaultdict(list)
    for bi in ses.wyb:
        for wiersz in wg.get(bi, []):
            if wiersz.get('wojna') and wiersz.get('skarbiec_zm') is not None:
                spadki[wiersz.get('krolestwo')].append(wiersz['skarbiec_zm'])
    co = '"Skarbce:" krolestw w wojnie nie spadaja szybciej niz o kilkadziesiat tys. dziennie'
    if spadki:
        naj = min(((mediana(v), k) for k, v in spadki.items()), key=lambda x: x[0])
        K.gdy(naj[0] >= -SKARBIEC_WOJNA_MAKS_SPADEK, co,
              'krolestw w wojnie %d; najszybciej spada %s: mediana %s na dobe' % (len(spadki), obetnij(str(naj[1]), 40),
                                                                               skr(naj[0], True)),
              ses.ser('Skarbce')[-1][1].get('_nr') if ses.ser('Skarbce') else None)
    else:
        K.brak(co, 'nie ma linii "Skarbce:" krolestwa w wojnie ze zmiana dobowa')
    zo = ses.ser('Zold')
    co = '"Zold:" z dopiskami K6: tarcza ZBEDNA i nieczynna, "zalogi bez zwrotu korony"'
    if zo:
        k6 = [1 for _, d in zo if d.get('k6')]
        czynna = [1 for _, d in zo if d.get('tarcza_czynna')]
        K.gdy(len(k6) == len(zo) and not czynna, co,
              'dopisek K6 w %d z %d dob; zalogi bez zwrotu korony: mediana %s na dobe; linia "SoldierPay: tarcza ... zbedna i '
              'nieczynna": %s' % (len(k6), len(zo), _zakres(_wart(zo, 'bez_zwrotu_korony')),
                                 'jest' if f.get('tarcza_zbedna') is not None else 'nie ma (pisana raz na kampanie przy wlaczonej '
                                                                                   'tarczy)'), zo[-1][1].get('_nr'))
    else:
        K.brak(co, 'nie ma linii "Zold:"')


def _k112(K, ses):
    f = ses.fakty
    _start(K, ses, '112', 'start: "VillageTakings: ... latki wpiete: powrot taboru do wsi, sprzedaz z magazynu wsi", 3 x WLACZONE',
           ['latki wpiete: powrot taboru do wsi, sprzedaz z magazynu wsi', 'reszta utargu do kiesy wsi WLACZONE',
            'zywnosc kupiona we wsi WLACZONE', 'sakwa zniszczonego taboru WLACZONE'])
    lt = f.get('utarg_latki')
    co = 'pierwsza doba "Utarg wsi (latki)": nasz prefiks (800) przed latka BK (400), model podatku BKTaxModel, sufit kiesy zdjety'
    if lt is not None:
        t = lt.tresc
        m = re.search(r'powrot taboru - prefiksy \[([^\]]*)\]', t)
        pref = [x.strip() for x in m.group(1).split(', ')] if m and m.group(1).strip() else []
        nasz = [i for i, x in enumerate(pref) if ':VillageTakings (' in x]
        bk = [i for i, x in enumerate(pref) if 'VillagerSettlementEnterPatch' in x]
        mm = re.search(r'; model podatku ([^;]+);', t)
        model = mm.group(1).strip() if mm else '?'
        sufit = 'na tej metodzie: jest (' in t
        K.gdy(bool(nasz) and nasz[0] == 0 and (not bk or bk[0] > nasz[0]) and model == 'BannerKings.Models.Vanilla.BKTaxModel'
              and sufit, co, 'prefiksy powrotu taboru: %s%s; model podatku %s; sufit kiesy wsi: %s'
              % (obetnij(', '.join(pref) or 'brak', 95), '' if bk else ' (LATKI BK NIE MA)', model,
                 'latka innego moda na Village.DailyTick jest' if sufit else 'BRAK latki - gra obetnie kiesy wsi do 1000'), lt.nr)
    elif ses.bloki:
        K.brak(co, 'nie ma tej linii w logu (pisana raz na sesje, w pierwszej dobie)')
    ut = ses.ser('Utarg wsi')
    if not ut:
        K.brak('co dobe linia "Utarg wsi:"', 'nie ma jej w logu')
        return
    ost = ut[-1][1]
    czynne = [(bi, d) for bi, d in ut if d.get('powroty_stan') == 'czynne']
    wp = _wart(czynne, 'wies_proc')
    co = '"wies razem" ok. 30% utargu (10-50% wedle dekretow)'
    if wp:
        K.gdy(UTARG_WIES_RAZEM_PROC[0] <= mediana(wp) <= UTARG_WIES_RAZEM_PROC[1], co,
              'wies razem: mediana %s%% utargu; oddane na dobe: mediana %s; dopisane kiesom wsi: mediana %s; pan po dopisaniu: '
              'mediana %s%%' % (_zakres(wp), skr(mediana(_wart(czynne, 'oddane'))), skr(mediana(_wart(czynne, 'dopisane_kiesom'))),
                                skr(mediana(_wart(czynne, 'pan_proc')))), czynne[-1][1].get('_nr'))
    else:
        stany = Counter(d.get('powroty_stan') for _, d in ut)
        K.dodaj(UW if (stany.get('WYLACZONE') or stany.get('nie wpieta')) else BD, co,
                'powroty taborow: %s' % (', '.join('%s %d dob' % (k, v) for k, v in stany.most_common()) +
                                         ('' if czynne else '; zadnej doby z podzialem utargu')), ost.get('_nr'))
    bp, dp = sum(_wart(ut, 'bez_podatku_bk')), sum(_wart(ut, 'dopisane_panom'))
    npz, okn, wyj = sum(_wart(ut, 'nieprzypisane')), sum(_wart(ut, 'okna')), sum(_wart(ut, 'wyjatki'))
    wsie = [d.get('bez_podatku_wsie') for _, d in ut if d.get('bez_podatku_wsie')]
    K.gdy(bp == 0 and dp == 0 and npz == 0 and okn == 0 and wyj == 0,
          '"licznikom panow 0 (0 powrotow bez podatku BK)", "nieprzypisane 0", "okna niedomkniete 0", bez "wyjatki zlapane"',
          'powrotow bez podatku BK razem %s (licznikom panow dopisano %s%s), nieprzypisane %s, okna niedomkniete %s, wyjatki %s w '
          '%d dobach; powrotow na dobe: mediana %s' % (skr(bp), skr(dp), (' - BK nie zna wsi: ' + obetnij(wsie[-1], 40)) if wsie
                                                       else '', skr(npz), skr(okn), skr(wyj), len(ut),
                                                       skr(mediana(_wart(ut, 'powrotow')))), ost.get('_nr'))
    zz, tz = sum(_wart(ut, 'zyw_zniklo')), sum(_wart(ut, 'tab_zniklo'))
    wyl = [k for k in ('zywnosc_stan', 'sakwy_stan') if any(d.get(k) != 'czynne' for _, d in ut)]
    K.gdy(zz == 0 and tz == 0 and not wyl, 'przy zywnosci i sakwach taborow "zniklo 0"',
          'zywnosc: %s zakupow, kupcy zaplacili %s, gra skasowala %s (pierwszy pomiar), zniklo %s; tabory z gotowka: w bitwie %s '
          '(%s zl), rozwiazane %s (%s zl), zniklo %s%s'
          % (skr(sum(_wart(ut, 'zyw_zakupow'))), skr(sum(_wart(ut, 'zyw_zaplacili'))), skr(sum(_wart(ut, 'zyw_skasowala'))), skr(zz),
             skr(sum(_wart(ut, 'tab_bitwa'))), skr(sum(_wart(ut, 'tab_bitwa_zl'))), skr(sum(_wart(ut, 'tab_rozw'))),
             skr(sum(_wart(ut, 'tab_rozw_zl'))), skr(tz), '; ZWROT WYLACZONY albo latka nie wpieta' if wyl else ''), ost.get('_nr'))
    prz = dict(ses.ser('Przeplywy osad'))
    pw = [(d, prz[bi]) for bi, d in czynne if bi in prz]
    co = '"Przeplywy osad": "zniklo 0 [R]", a dopisane przez K7 bliskie polowie "kiesa wsi"'
    if pw:
        pp = [(0, p) for _, p in pw]
        zn = [p.get('zniklo') or 0 for _, p in pw]
        dk, kw = _wart(pp, 'k7_do_kies'), _wart(pp, 'kiesa_wsi')
        udzial = (100.0 * mediana(dk) / mediana(kw)) if dk and kw and mediana(kw) else None
        K.gdy(max(abs(x) for x in zn) == 0 and (udzial is None or 30.0 <= udzial <= 70.0), co,
              'zniklo: najwiecej %s w %d dobach; dopisane do kies: mediana %s wobec "kiesa wsi" %s (%s%%); na liczniki panow %s'
              % (skr(max(zn, key=abs)), len(pw), skr(mediana(dk)), skr(mediana(kw)), skr(round(udzial, 1)) if udzial is not None
                 else '-', skr(sum(_wart(pp, 'k7_na_liczniki')))), pw[-1][1].get('_nr'))
    else:
        K.brak(co, 'nie ma linii "Przeplywy osad:" w dobach z czynnym K7 (ksiega pisze od drugiej doby)')
    bil = dict(ses.ser('Pieniadz swiata (bilans)'))
    bw = [(0, bil[bi]) for bi, _ in ut if bi in bil]
    co = '"Pieniadz swiata (bilans)": z utargu wsi, z ceny zywnosci i z sakw taborow "zniklo 0"'
    if bw:
        a, b, c = [x.get('zniklo') or 0 for _, x in bw], _wart(bw, 'zyw_zniklo'), _wart(bw, 'sakwy_zniklo')
        K.gdy(max(a) == 0 and bool(b) and max(abs(x) for x in b) == 0 and bool(c) and max(abs(x) for x in c) == 0, co,
              ('z utargu wsi najwiecej %s; z zywnosci najwiecej %s (gra skasowala: mediana %s); z sakw najwiecej %s (bylo w nich: '
               'mediana %s)' % (skr(max(a)), skr(max(b, key=abs)), skr(mediana(_wart(bw, 'zyw_skasowala'))), skr(max(c, key=abs)),
                                skr(mediana(_wart(bw, 'sakwy_bylo')))))
              if (b and c) else 'bilans bez pozycji "z ceny zywnosci kupionej we wsiach zniklo" (uklad sprzed ogniwa 112)',
              bw[-1][1].get('_nr'))
    else:
        K.brak(co, 'nie ma linii "Pieniadz swiata (bilans):" w dobach z linia "Utarg wsi:" (ksiega pisze od drugiej doby)')
    kw = ses.ser('Przeplywy osad (kiesy wsi)')
    po, pb = _pierw_ost(kw, 'stan'), _pierw_ost(kw, 'ponizej_1000')
    co = '"Przeplywy osad (kiesy wsi)": stan rosnie (ok. 2x w dwa tygodnie), "ponizej 1000 zlota" spada'
    if po:
        K.gdy(po[1] >= po[0], co,
              'stan %s -> %s (x%.2f, doby %d-%d); wsi ponizej 1000 zlota %s -> %s; zywnosc kupiona we wsiach: mediana %s'
              % (skr(po[0]), skr(po[1]), (po[1] / float(po[0])) if po[0] else 0.0, po[2], po[3], skr(pb[0]) if pb else '-',
                 skr(pb[1]) if pb else '-', skr(mediana(_wart(kw, 'zywnosc_wsi')), True)), kw[-1][1].get('_nr'))
    else:
        K.brak(co, 'nie ma linii "Przeplywy osad (kiesy wsi):"')
    pr = _pierw_ost(ses.ser('Ludnosc'), 'zaplacone')
    if pr:
        K.dodaj(OK, 'pomiar: "Ludnosc: renty zaplacone" (w gore o ok. tyle, ile K7 dopisuje kiesom wsi)',
                'renty zaplacone %s -> %s (doby %d-%d)' % (skr(pr[0]), skr(pr[1]), pr[2], pr[3]),
                ses.ser('Ludnosc')[-1][1].get('_nr'))
    dw = dict(ses.ser('Dowoz'))
    rozbite = sum((dw[bi].get('rozbite') or 0) for bi, _ in ut if bi in dw)
    z_got = sum(_wart(ut, 'tab_bitwa')) + sum(_wart(ut, 'tab_rozw'))
    if [1 for bi, _ in ut if bi in dw]:
        K.gdy(z_got <= rozbite + 2, '"Dowoz: rozbite tabory wsi" nie mniej niz "tabory zniszczone z gotowka"',
              'rozbite tabory razem %s, z gotowka %s (w bitwie %s, rozwiazane %s)'
              % (skr(rozbite), skr(z_got), skr(sum(_wart(ut, 'tab_bitwa'))), skr(sum(_wart(ut, 'tab_rozw')))), ost.get('_nr'))


def _k113(K, ses):
    _start(K, ses, '113', 'start: "Devastation: ... CZYNNE", parametry domyslne, 4 latki wpiete, bez "BRAK" i "NIECZYNNE"',
           ['CZYNNE', 'rabunek 0.5 i zerowanie 0.2 czlowieka na zbrojnego na dobe, pulap 90% okregu, ginie 5%, w las 5%, powrot '
                      '0.1% dziennie x (1 - niebezpieczenstwo), plon ^0.5', 'krok rabunku', 'koniec rabunku',
            'progi produkcji od ludzi sprzed spustoszenia', 'zywnosc wsi'],
           ('BRAK', 'NIE wpiet', 'NIECZYNN', 'wylaczone w ustawieniach'))
    lu = [(bi, d) for bi, d in ses.ser('Ludzie') if d.get('spustoszenie')]
    if not lu:
        K.brak('co dobe odcinek "spustoszenie dzis" w linii "Ludzie:"', 'linie "Ludzie:" go nie maja (albo nie ma linii)')
        return
    cz = [(bi, d) for bi, d in lu if d.get('spustoszenie') == 'czynne']
    ost = lu[-1][1]
    zdj, zab, las, uch = (sum(_wart(cz, k)) for k in ('sp_zdjeci', 'sp_zabici', 'sp_w_las', 'sp_uchodzcy'))
    K.gdy(len(cz) == len(lu), 'co dobe odcinek "spustoszenie dzis: zdjeci z wsi ..." (spustoszenie czynne)',
          'jest w %d dobach, czynne w %d; zdjeci razem %s ludzi (rabunki %s, zerowanie armii %s); uchodzcy poza domem ostatnio %s z '
          '%s wsi' % (len(lu), len(cz), skr(round(zdj, 1)), skr(round(sum(_wart(cz, 'sp_rabunki')), 1)),
                      skr(round(sum(_wart(cz, 'sp_zerowanie')), 1)), skr(ost.get('sp_w_drodze')), skr(ost.get('sp_w_drodze_wsi'))),
          lu[0][1].get('_nr'))
    co = 'trafieni: 5% ginie, 5% w las, 90% uchodzcy (suma sie zgadza)'
    if zdj >= 100:
        pz, pl = 100.0 * zab / zdj, 100.0 * las / zdj
        roz = max([abs(d.get('sp_rozjazd') or 0) for _, d in cz] or [0])
        K.gdy(abs(pz - 5.0) <= SPUSTOSZENIE_UDZIAL_TOL and abs(pl - 5.0) <= SPUSTOSZENIE_UDZIAL_TOL and roz <= 1.0,
              co, 'zdjeci %s = zabici %s (%.1f%%) + w las %s (%.1f%%) + uchodzcy %s (%.1f%%); najwiekszy rozjazd doby %s'
              % (skr(round(zdj, 1)), skr(round(zab, 1)), pz, skr(round(las, 1)), pl, skr(round(uch, 1)), 100.0 * uch / zdj,
                 skr(roz)), cz[-1][1].get('_nr'))
    else:
        K.brak(co, 'za malo trafionych do rachunku: zdjeci razem %s ludzi w %d dobach' % (skr(round(zdj, 1)), len(cz)))
    sp = ses.ser('Ludzie (spustoszenie)')
    rab = [(bi, d) for bi, d in sp if (d.get('rabunki') or 0) > 0]
    co = 'po rabunku: zdjeci rzedu setek ludzi, "gra zdjelaby" rzedu dziesiatek tysiecy'
    if rab:
        na = _wart(rab, 'na_rabunek')
        zle = [1 for _, d in rab if (d.get('na_rabunek') or 0) >= SPUSTOSZENIE_NA_RABUNEK_ALARM or (
            (d.get('gra_ludzi') or 0) >= SPUSTOSZENIE_GRA_MIN_LUDZI and isinstance(d.get('ludzi_rab'), (int, float))
            and d['ludzi_rab'] >= SPUSTOSZENIE_UDZIAL_GRY_ALARM * d['gra_ludzi'])]
        K.gdy(not zle, co, 'rabunkow %s (wies spalona %s, przerwane %s) w %d dobach, napastnikow srednio %s; zdjeci na rabunek: '
              'mediana %s ludzi przy osobodniach %s; gra zdjelaby: mediana %s ludzi (%s hearth)'
              % (skr(sum(_wart(rab, 'rabunki'))), skr(sum(_wart(rab, 'spalone'))), skr(sum(_wart(rab, 'przerwane'))), len(rab),
                 skr(mediana(_wart(rab, 'napastnikow'))), _zakres(na), skr(mediana(_wart(rab, 'osobodni_rab'))),
                 skr(mediana(_wart(rab, 'gra_ludzi'))), skr(mediana(_wart(rab, 'gra_hearth')))), rab[-1][1].get('_nr'))
    else:
        K.brak(co, 'zadnego zakonczonego rabunku w logu (linii "Ludzie (spustoszenie):" %d - pisana tylko w dobach z ruchem)'
               % len(sp))
    co = 'pomiar: osobodni zerowania armii (dziesiatki tysiecy dziennie = za duzo)'
    if sp:
        zer = _wart(sp, 'osobodni_zer')
        K.gdy(not zer or mediana(zer) < ZEROWANIE_OSOBODNI_UWAGA, co,
              'partii na dobe: mediana %s; osobodni: mediana %s -> ludzi %s; okreg nasycony razem %s razy'
              % (skr(mediana(_wart(sp, 'zer_partii'))), _zakres(zer), skr(mediana(_wart(sp, 'ludzi_zer'))),
                 skr(sum(_wart(sp, 'nasycony')))), sp[-1][1].get('_nr'))
        so = sp[-1][1]
        K.dodaj(OK, 'pomiar: powroty uchodzcow (0.1% dziennie x (1 - niebezpieczenstwo)) i plon od rak',
                'wrocilo razem %s ludzi; tempo: mediana %s%% dziennie; uchodzcy poza domem %s -> %s w %s regionach; najnizszy '
                'mnoznik plonu x%s; najbardziej spustoszony: %s'
                % (skr(round(sum(_wart(sp, 'powrot_ludzi')), 1)), skr(mediana(_wart(sp, 'tempo'))), skr(sp[0][1].get('uchodzcy')),
                   skr(so.get('uchodzcy')), skr(so.get('regionow')), skr(so.get('min_plon')),
                   ('%s %s%%' % (obetnij(str(so.get('top1')), 30), skr(so.get('top1_proc')))) if so.get('top1') else '-'),
                so.get('_nr'))
    else:
        K.brak(co, 'nie ma linii "Ludzie (spustoszenie):" (pisana tylko w dobach z rabunkiem, zerowaniem albo uchodzcami)')
    pr = ses.ser('Ludzie (przyrost)')
    rozl = [(bi, d) for bi, d in pr if d.get('spust_w_ruchu') is not None]
    co = '"Ludzie: przyrost naturalny": ruch ludzi ze spustoszeniem, "powrot uchodzcow +0.5 hearth: 0 wsi"'
    if rozl:
        bez = [1 for _, d in rozl if not d.get('spust_w_ruchu')]
        reg = max(_wart(pr, 'uch_regula_wsi') or [0])
        K.gdy(not bez and reg == 0, co,
              'dopisek "spustoszenie i powroty uchodzcow" w %d z %d dob; regula +0.5 hearth: najwiecej %s wsi; RESZTA: mediana %s '
              '(bez rabunkow; zostaja inwestycje BetterEconomy)' % (len(rozl) - len(bez), len(rozl), skr(reg),
                                                                   _zakres(_wart(rozl, 'reszta'))), rozl[-1][1].get('_nr'))
    else:
        K.brak(co, 'nie ma linii przyrostu z rozliczeniem zmiany hearth (zaczyna sie od drugiej doby)')
    wy = dict(ses.ser('Wyrzutki'))
    pary = [(wy[bi].get('rabunki'), d.get('sp_w_las')) for bi, d in cz if bi in wy and wy[bi].get('rabunki') is not None
            and isinstance(d.get('sp_w_las'), (int, float))]
    if pary:
        zle = [1 for a, b in pary if abs(a - b) > 1.5]
        K.gdy(not zle, '"Wyrzutki: ... rabunki N" = 5% trafionych, ktorzy poszli w las',
              'zgodne w %d z %d dob; rabunki razem %s wobec "w las do puli wyrzutkow" %s'
              % (len(pary) - len(zle), len(pary), skr(sum(a for a, _ in pary)), skr(round(sum(b for _, b in pary), 1))),
              cz[-1][1].get('_nr'))
    dop = [(k, sum(_wart(sp, k))) for k in ('bez_k', 'obcy_hearth', 'podjete')] + [('sp_potk', sum(_wart(cz, 'sp_potk')))]
    K.gdy(all(v == 0 for _, v in dop), 'bez dopiskow: "bez przelicznika ludzi", "potkniecia", "hearth dopisany przez kogos", '
          '"rabunki podjete po wczytaniu"', 'bez przelicznika %s, hearth dopisany przez kogos %s, rabunki podjete po wczytaniu %s, '
          'potkniecia %s' % tuple(skr(v) for _, v in dop), ost.get('_nr'))
    pc = []
    for _, d in lu:
        zm = d.get('razem_zm') if d.get('razem_zm') is not None else d.get('ludnosc_zm')
        mln = d.get('razem_mln') or d.get('ludnosc_mln')
        if zm is not None and mln:
            pc.append(100.0 * abs(zm) / (mln * 1e6))
    if pc:
        K.gdy(max(pc) <= LUDNOSC_MAKS_ZMIANA_PROC, 'ludnosc razem z uchodzcami stoi (zmiana najwyzej %.2f%% dziennie)'
              % LUDNOSC_MAKS_ZMIANA_PROC, 'najwieksza zmiana dobowa %.3f%%; ludnosc w domu %s mln, uchodzcy poza domem %s, razem '
              '%s mln' % (max(pc), skr(ost.get('ludnosc_mln')), skr(ost.get('uchodzcy') if ost.get('uchodzcy') is not None
                                                                 else ost.get('sp_w_drodze')),
                          skr(ost.get('razem_mln') or ost.get('ludnosc_mln'))), ost.get('_nr'))


# ------------------------------------------------------------------------------------------------ ogniwa 115-119 (grupa 2b)
def _bledy_z(ses, *fragmenty):
    """Grupy bledow (ERROR / Exception / potkniecia), ktorych klucz albo przyklad zawiera ktorys fragment: [(klucz, ile, nr)]."""
    return [(b[0], b[1], b[2]) for b in ses.bledy if any(x in b[0] or x in (b[3] or '') for x in fragmenty)]


def _opis_bledow(bl):
    return ('; '.join('%s x%d (linia %d)' % (obetnij(k, 60), ile, nr) for k, ile, nr in bl[:3])) if bl else 'brak'


def _miast_swiata(ses):
    """Liczba miast swiata z linii "Ruda: ... miast bez towaru N z M" (97 w ROT, gdy linii nie ma)."""
    v = _wart(ses.ser('Ruda'), 'miast')
    return mediana(v) if v else 97


def _pierw_ost_txt(ser, klucz):
    po = _pierw_ost(ser, klucz)
    return ('%s -> %s' % (skr(po[0]), skr(po[1]))) if po else '-'


def _k115(K, ses):
    """Ogniwo 115 cena od niedoboru (docs\\paczki\\115-cena-od-niedoboru.md, "PO CZYM POZNAC W LOGU")."""
    f = ses.fakty
    w = _start(K, ses, '115', 'start: "RawPrice: cena surowcow od niedoboru - stala wzoru ... (CZYNNA), popyt ... (CZYNNY)"',
               ['(CZYNNA)', '(CZYNNY)'], ('BRAK', 'NIE wpiet', 'NIECZYNN'))
    if w is not None:
        m = re.search(r'wpieta w (\d+) modelach cen .*?wpiety w (\d+) modelach ekonomii osad', w.tresc)
        K.gdy(bool(m) and int(m.group(1)) > 0 and int(m.group(2)) > 0,
              'latki weszly: modeli cen > 0 i modeli ekonomii osad > 0 (0 = latka nie weszla; u Jeffa po 2)',
              ('modeli cen %s, modeli ekonomii osad %s' % m.groups()) if m else 'liczb modeli nie ma w linii', w.nr)
    s = f.get('rawprice_seed')
    co = 'nowa kampania: "RawPrice: nowa kampania - ... w 97 miastach (zamki bez zmian), ok. 46 kategorii (bez 118: 38); potkniecia 0"'
    if s is not None:
        m = re.search(r'na nowa monete w (\d+) miastach .*?, (\d+) kategorii \((\d+) pozycji\).*?; potkniecia (\d+)', s.tresc)
        if m:
            miast, kat, poz, potk = [int(x) for x in m.groups()]
            K.gdy(miast >= 90 and potk == 0, co, '%d miast, %d kategorii (%d pozycji), potkniecia %d' % (miast, kat, poz, potk), s.nr)
        else:
            K.dodaj(UW, co, 'linia bez oczekiwanych liczb: ' + wycinek(s.tresc, 100), s.nr)
    elif f.get('nowa_kampania'):
        K.dodaj(UW, co, 'nowa kampania, a tej linii nie ma - pamiec rynku z tickow startowych zostala w starej monecie (ceny przez 2-3 '
                        'tygodnie z mieszanki monet)')
    else:
        K.brak(co, 'wczytany zapis - linia jest tylko w sesji, ktora zalozyla kampanie')
    ce = ses.ser('Ceny surowcow')
    if not ce:
        K.brak('co dobe linia "Ceny surowcow:"', 'nie ma jej w logu (pisze ja RawPrice.Daily po przeliczeniu cen historycznych)')
        return
    pelne = len(ses.pelne())
    cz = [1 for _, d in ce if d.get('popyt_stan') == 'CZYNNY' and d.get('stala_stan') == 'CZYNNA']
    K.gdy(len(ce) >= pelne - 1 and len(cz) == len(ce), 'co dobe "Ceny surowcow:" - popyt z prawdziwego zuzycia CZYNNY, stala wzoru CZYNNA',
          '%d linii w %d dobach, obie czesci czynne w %d; model cen %s' % (len(ce), pelne, len(cz), ce[-1][1].get('model_cen')),
          ce[-1][1].get('_nr'))
    # ruda w miastach bez rudy: indeks na suficie gry (dopoki ponad polowa miast nie ma rudy - takze mediana)
    miast = _miast_swiata(ses)
    puste = [(bi, d) for bi, d in ce if (d.get('ruda_bez') or 0) > 0 and d.get('ruda_idx_max') is not None]
    pol = [(bi, d) for bi, d in puste if d['ruda_bez'] > miast / 2.0]
    co = 'ruda: indeks w miastach bez rudy ok. 10 (sufit) - max 10.00, mediana 10.00, dopoki wiekszosc miast nie ma rudy (dotad 1.50)'
    if puste:
        sufit = len([1 for _, d in puste if d['ruda_idx_max'] >= INDEKS_SUFIT])
        med = len([1 for _, d in pol if (d.get('ruda_idx_med') or 0) >= INDEKS_SUFIT])
        a, b = ce[0][1], ce[-1][1]
        K.gdy(sufit >= 0.8 * len(puste) and (not pol or med >= 0.8 * len(pol)), co,
              'max na suficie w %d z %d dob z pustymi miastami; mediana na suficie w %d z %d dob, gdy ponad polowa z %s miast bez rudy; '
              'min/med/max 1. doba %s/%s/%s (bez rudy %s), ostatnia %s/%s/%s (bez rudy %s)'
              % (sufit, len(puste), med, len(pol), skr(miast), skr(a.get('ruda_idx_min')), skr(a.get('ruda_idx_med')),
                 skr(a.get('ruda_idx_max')), skr(a.get('ruda_bez')), skr(b.get('ruda_idx_min')), skr(b.get('ruda_idx_med')),
                 skr(b.get('ruda_idx_max')), skr(b.get('ruda_bez'))), b.get('_nr'))
    else:
        K.brak(co, 'w zadnej dobie nie ma miasta bez rudy')
    pl = _wart(ce, 'ruda_placa')
    co = 'ruda: "bez towaru N miast - za pierwsza sztuke placa" ok. 37-40 d (dotad 10 d); "z nadwyzka ... sprzedaja po" 2-4 d'
    if pl:
        K.gdy(mediana(pl) >= CENA_RUDY_PUSTA_MIN, co, 'za pierwsza sztuke: mediana %s d; sprzedaja po: %s d'
              % (_zakres(pl), _zakres(_wart(ce, 'ruda_sprzedaja'))), ce[-1][1].get('_nr'))
    else:
        K.brak(co, 'linie bez pozycji "za pierwsza sztuke placa" (w zadnej dobie nie ma miasta bez rudy)')
    # popyt w danych rynku = mieszczanie + rzemioslo od pierwszej doby nowej kampanii (wczytany zapis: dochodzi wygladzaniem gry)
    nowa = bool(f.get('nowa_kampania'))
    bi, d = ce[0] if nowa else ce[-1]
    dn, pm, pr, pg = d.get('ruda_popyt_dane'), d.get('ruda_popyt_m'), d.get('ruda_popyt_r'), d.get('ruda_popyt_gra')
    co = ('ruda: "popyt dobowy miasta: mieszczan 0 d + rzemiosla 5.6 d, w danych rynku" ok. 5.6 d OD PIERWSZEJ DOBY nowej kampanii '
          '("sam szacunek gry" 4 d)')
    if dn is not None and pm is not None and pr is not None:
        suma = pm + pr
        K.gdy(abs(dn - suma) <= max(0.5, 0.2 * suma), co, '%s (doba %d): mieszczan %s + rzemiosla %s = %s d, w danych rynku %s d, sam '
              'szacunek gry %s d' % ('pierwsza doba' if nowa else 'ostatnia doba - wczytany zapis, popyt dochodzi wygladzaniem gry', bi + 1,
                                     skr(pm), skr(pr), skr(round(suma, 2)), skr(dn), skr(pg)), d.get('_nr'))
    else:
        K.brak(co, 'linia bez czesci "popyt dobowy miasta" dla rudy')
    dm = _wart(ce, 'drewno_idx_med')
    co = 'drewno: mediana indeksu ok. 0.8-1.1 (dotad 0.2-0.6), za pierwszy ladunek w pustym miescie ok. 39 d'
    if dm:
        K.gdy(mediana(dm) >= INDEKS_DREWNA_MIN, co, 'mediana indeksu po dobach %s; za pierwszy ladunek %s d; miast bez drewna %s'
              % (_zakres(dm), _zakres(_wart(ce, 'drewno_placa')), _pierw_ost_txt(ce, 'drewno_bez')), ce[-1][1].get('_nr'))
    else:
        K.brak(co, 'linie bez drewna')
    wm = _wart(ce, 'welna_idx_med')
    co = ('welna: mediana indeksu 0.10-0.3 (polki pelne welny), max 0.8-10 tylko w miastach z tkalnia; pierwsza sztuka w pustym '
          'miescie bez tkalni ok. 81 d')
    if wm:
        K.gdy(mediana(wm) <= INDEKS_WELNY_MAKS, co, 'mediana indeksu %s, max %s; za pierwsza sztuke %s d'
              % (_zakres(wm), _zakres(_wart(ce, 'welna_idx_max')), _zakres(_wart(ce, 'welna_placa'))), ce[-1][1].get('_nr'))
    else:
        K.brak(co, 'linie bez welny')
    smax, pmax = _wart(ce, 'inne_salt_max'), _wart(ce, 'inne_beer_max')
    co = '"Inne przeliczone towary": sol i piwo max 10.00 (dotad 2.54 i 3.74 - stala wzoru w starej monecie)'
    if smax and pmax:
        K.gdy(max(smax) >= INDEKS_SUFIT and max(pmax) >= INDEKS_SUFIT, co, 'najwyzszy max po dobach: sol %s, piwo %s; mediana indeksu '
              'zboza %s' % (skr(max(smax)), skr(max(pmax)), _zakres(_wart(ce, 'inne_grain_med'))), ce[-1][1].get('_nr'))
    else:
        K.brak(co, 'linie bez czesci "Inne przeliczone towary" (grain, salt, beer)')
    st = ses.ser('Karawany (stan)')
    rc = _wart(st, 'ruda_cena')
    co = ('"Karawany (stan): ruda ... cena zbytu" ok. 15-25 d (dotad 29.9 -> 10.4) i nie spada sama z doby na dobe; drewno 8-12 d '
          '(dotad 1.3); welna wyraznie ponizej 372 d')
    if rc:
        med = mediana(rc)
        dc, wc = _wart(st, 'drewno_cena'), _wart(st, 'welna_cena')
        K.gdy(CENA_ZBYTU_RUDY_2B[0] <= med <= CENA_ZBYTU_RUDY_2B[1] and rc[0] <= 1.5 * med
              and (not dc or mediana(dc) >= CENA_ZBYTU_DREWNA_MIN_2B) and (not wc or mediana(wc) < CENA_ZBYTU_WELNY_MAKS_2B), co,
              'ruda %s -> %s d (mediana %s); drewno: mediana %s d; welna: mediana %s d'
              % (skr(rc[0]), skr(rc[-1]), skr(med), skr(mediana(dc)) if dc else '-', skr(mediana(wc)) if wc else '-'), st[-1][1].get('_nr'))
    else:
        K.brak(co, 'linie "Karawany (stan):" bez ceny zbytu rudy')
    kar = ses.ser('Karawany')
    zr, zd = _wart(kar, 'zysk_ruda_kg'), _wart(kar, 'zysk_drewno_kg')
    co = '"Karawany: oczekiwany zysk ... na kilogram": ruda ok. 0.15 (dotad 0.08), drewno ok. 0.08 (dotad 0.003); drewno wsrod kupionych'
    if zr or zd:
        kd = sum(_wart(kar, 'kup_drewno'))
        K.gdy(bool(zr) and mediana(zr) >= ZYSK_RUDY_KG_MIN and bool(zd) and mediana(zd) >= ZYSK_DREWNA_KG_MIN and kd > 0, co,
              'ruda: mediana %s d/kg (%d dob z zakupem rudy); drewno: mediana %s d/kg (%d dob); kupione drewno razem %s ladunkow'
              % (skr(mediana(zr)) if zr else '-', len(zr), skr(mediana(zd)) if zd else '-', len(zd), skr(kd)), kar[-1][1].get('_nr'))
    else:
        K.brak(co, 'linie "Karawany:" bez zakupow rudy i drewna')
    rb = ses.ser('Rynek broni')
    idr = _wart(rb, 'idx_drewno_med')
    co = '"Rynek broni: indeks drewna" mediana ok. 1.0-1.25 (dotad 0.58 w ostatniej dobie)'
    if idr:
        K.gdy(idr[-1] >= INDEKS_BRONI_DREWNA_MIN, co, 'ostatnia doba %s, po dobach %s; indeks rudy %s'
              % (skr(idr[-1]), _zakres(idr), _zakres(_wart(rb, 'idx_ruda_med'))), rb[-1][1].get('_nr'))
    else:
        K.brak(co, 'nie ma linii "Rynek broni:" z indeksem drewna')
    dr = [(bi, d) for bi, d in ses.ser('Drewno') if bi > 0]
    if dr:
        K.dodaj(OK, 'pomiar: "Drewno: zuzycie ... budowy" wyraznie mniej ladunkow (dotad mediana 1958, pod koniec 2.3-2.4 tys.) przy '
                    'podobnych "Budowy oplacone: materialy z targow" w denarach; zapas miast rosnie, "bez wyjasnienia" (dosypka RBL) maleje',
                'budowy: mediana %s ladunkow; materialy z targow: mediana %s d; zapas drewna w miastach %s; bez wyjasnienia: mediana %s '
                '(dotad 2055)' % (_zakres(_wart(dr, 'budowy')), _zakres(_wart(ses.ser('Budowy oplacone'), 'materialy')),
                                  _pierw_ost_txt(dr, 'miasta'), _zakres(_wart(dr, 'bez_wyjasnienia'))), dr[-1][1].get('_nr'))
    wr = ses.ser('Warsztaty')
    bz = _wart(wr, 'bez_zysku')
    co = '"Warsztaty: odpuszczone: bez zysku" moze wzrosnac o kilkadziesiat cykli (setki = drewno na suficie w wielu miastach)'
    if bz:
        K.gdy(mediana(bz) <= WARSZTATY_BEZ_ZYSKU_MAKS, co, 'bez zysku: mediana %s na dobe (dotad 32); koszt: mediana %s, wykonano: mediana %s '
              'szt.' % (_zakres(bz), skr(mediana(_wart(wr, 'koszt'))), skr(mediana(_wart(wr, 'wykonano')))), wr[-1][1].get('_nr'))
    bl = _bledy_z(ses, 'RawPrice.')
    K.gdy(not bl, 'bez ERROR "RawPrice." (blad = wycena wraca do wzoru gry, jeden wpis na kampanie)', _opis_bledow(bl),
          bl[0][2] if bl else None)
    prz, rs, ps = ses.ser('Przeplywy osad'), ses.ser('Rynek surowcow'), ses.ser('Pieniadz swiata')
    K.dodaj(OK, 'pomiar (do obserwacji): "Przeplywy osad: miasta zaplacily" (spadek o kilka % - welna), "Rynek surowcow: wool" na polkach, '
                '"Kasy miast"', 'miasta zaplacily taborom: mediana %s (dotad 380.0k); welna na polkach miast %s (dotad 660 -> 4094); '
                'kasy miast %s'
            % (skr(mediana(_wart(prz, 'miasta_zaplacily'))), _pierw_ost_txt(rs, 'wool'), _pierw_ost_txt(ps, 'kasy_miast')),
            ce[-1][1].get('_nr'))


def _k116(K, ses):
    """Ogniwo 116 warsztaty towarowe w nowej monecie (docs\\paczki\\116-warsztaty-w-nowej-monecie.md, "Po czym poznac w logu")."""
    f = ses.fakty
    w = _start(K, ses, '116', 'start: "WorkshopTrade: warsztaty towarowe w nowej monecie CZYNNE - wpiete: ...; stala 200 podmieniona w 2 z 2 '
                              'metod" (bez "BRAK")', ['CZYNNE', 'podmieniona w 2 z 2 metod'], ('BRAK', 'NIE wpiet', 'NIECZYNN'))
    if w is not None:
        m = re.search(r' - wpiete: (.*?); stala 200', w.tresc)
        wp = [x for x in m.group(1).split(', ') if x.strip() and x.strip() != 'nic'] if m else []
        K.gdy(len(wp) == 15, 'start: wpiete wszystkie 15 latek (prog, place, wynik doby, wydatek, kapital, sprzedaz, ceny, koszt sprzetu)',
              'wpietych %d z 15%s' % (len(wp), ('; ' + ', '.join(wp[:4]) + ' ...') if wp else ''), w.nr)
    la = f.get('workshoptrade_latka')
    co = 'po wejsciu do kampanii: "WorkshopTrade: latka modelu finansow rodu (zakladana w kampanii) - drugi pobor wydatku ... wylaczony"'
    if la is not None:
        K.gdy('BRAK' not in la.tresc and 'wylaczony' in la.tresc, co, wycinek(la.tresc, 110), la.nr)
    elif ma_ogniwo(ses, '116') and ses.bloki:
        K.dodaj(UW, co, 'tej linii nie ma - wydatek warsztatu gracza moze schodzic dwa razy')
    else:
        K.brak(co, 'w logu nie ma kampanii')
    s = f.get('workshoptrade_seed')
    co = ('nowa kampania: "WorkshopTrade: nowa kampania - kapital startowy wedle cen nowej monety: sprawdzono 291 warsztatow, poprawiono '
          'ok. 14 (velvet_weavery 2000 -> 10000), kapital warsztatow +112000"')
    if s is not None:
        m = re.search(r'sprawdzono (\d+) warsztatow, poprawiono (\d+)(?: \((.*?)\))?, kapital warsztatow ([+-]?\d+)', s.tresc)
        if m:
            K.gdy(int(m.group(1)) >= WARSZTATY_TOW_MIN, co, 'sprawdzono %s, poprawiono %s (%s), kapital warsztatow %s'
                  % (m.group(1), m.group(2), obetnij(m.group(3) or '-', 50), m.group(4)), s.nr)
        else:
            K.dodaj(UW, co, 'linia bez oczekiwanych liczb: ' + wycinek(s.tresc, 100), s.nr)
    elif f.get('nowa_kampania'):
        K.dodaj(UW, co, 'nowa kampania, a tej linii nie ma - kapital startowy tkalni aksamitu zostal w starej monecie')
    else:
        K.brak(co, 'wczytany zapis - linia jest tylko w sesji, ktora zalozyla kampanie')
    wt = ses.ser('Warsztaty towarowe')
    if not wt:
        K.brak('co dobe linia "Warsztaty towarowe:"', 'nie ma jej w logu')
        return
    pelne = len(ses.pelne())
    n, r = _wart(wt, 'warsztatow'), _wart(wt, 'rozliczonych')
    K.gdy(len(wt) >= pelne - 1 and n and mediana(n) >= WARSZTATY_TOW_MIN and r and mediana(r) >= 0.9 * mediana(n),
          'co dobe "Warsztaty towarowe:" - warsztatow ok. 291 (bez ukrytych rzemieslnikow), "rozliczonych dzis" ok. tyle samo',
          '%d linii w %d dobach; warsztatow %s, rozliczonych %s; pracowalo %s (cykli %s)'
          % (len(wt), pelne, _zakres(n), _zakres(r), _zakres(_wart(wt, 'pracowalo')), _zakres(_wart(wt, 'cykli'))), wt[-1][1].get('_nr'))
    bp = [(bi, d.get('typ_bakery_prac'), d.get('typ_bakery_n')) for bi, d in wt if d.get('typ_bakery_prac') is not None]
    co = 'piekarnie pracuja od pierwszej doby ("wedle typu: bakery 76 (pracowalo X" > 0)'
    if bp:
        K.gdy(bp[0][1] > 0 and mediana([x[1] for x in bp]) > 0, co, 'bakery: 1. doba pracowalo %s z %s, potem mediana %s; d na warsztat %s'
              % (skr(bp[0][1]), skr(bp[0][2]), _zakres([x[1] for x in bp]), _zakres(_wart(wt, 'typ_bakery_dnw'))), wt[-1][1].get('_nr'))
    else:
        K.brak(co, 'w linii "wedle typu" nie ma piekarni (bakery)')
    pk = [(bi, d) for bi, d in wt if d.get('piekarnia_cena') is not None]
    co = ('piekarnia: "d na warsztat" 16-50 przy "wyrob bread x1-2" albo 150-400 przy x5-10; cena w przykladzie miasta (Lannisport) '
          '13-116 tys. przy ok. 20 chlebach dziennie (ponad %s = chleba naprawde brakuje)' % skr(PIEKARNIA_CENA_MAKS))
    if pk:
        bi, d = pk[-1]
        x, dnw = d.get('piekarnia_wyrob_x'), wt[-1][1].get('typ_bakery_dnw')
        zgodnie = ''
        if isinstance(x, (int, float)) and isinstance(dnw, (int, float)):
            spod = (16, 50) if x <= 2.0 else ((150, 400) if x >= 5.0 else (16, 400))
            zgodnie = '; zysk %s d na warsztat przy chlebie x%s - %s z rachunkiem paczki (%d-%d)' % (
                skr(dnw), skr(x), 'zgodnie' if spod[0] <= dnw <= spod[1] else 'INACZEJ niz', spod[0], spod[1])
        K.gdy(d['piekarnia_cena'] <= PIEKARNIA_CENA_MAKS and (x is None or x < INDEKS_SUFIT), co,
              '%s (dobrobyt %s), ostatnia doba: piekarnia %s (zysk %s d na dobe ze sredniej %s dob, kapital %s; wyrob %s x%s, wsad x%s); '
              'w szeregu cena %s%s' % (d.get('przyklad_miasto'), skr(d.get('przyklad_dobrobyt')), skr(d['piekarnia_cena']),
                                       skr(d.get('piekarnia_zysk')), skr(d.get('piekarnia_dob')), skr(d.get('piekarnia_kapital')),
                                       d.get('piekarnia_wyrob') or '?', skr(x), skr(d.get('piekarnia_wsad_x')),
                                       _zakres(_wart(pk, 'piekarnia_cena')), zgodnie), d.get('_nr'))
    else:
        K.brak(co, 'przyklad miasta w "Warsztaty towarowe:" bez piekarni')
    pod = _wart(wt, 'podatek_max')
    co = '"podatek gracza 0%" w przykladzie miasta (u Jeffa czynny model warsztatow gry - rody nie placa podatku od warsztatow)'
    if pod:
        K.gdy(max(pod) == 0, co, 'najwyzszy podatek gracza w przykladach: %s%%' % skr(max(pod)), wt[-1][1].get('_nr'))
    else:
        K.brak(co, 'linie bez przykladu miasta')
    ps = ses.ser('Pieniadz swiata')
    kap, kzm = _wart(ps, 'warsztaty'), _wart(ps, 'warsztaty_zm')
    co = ('"Pieniadz swiata: kapital warsztatow" przestaje tracic ok. 29 tys. dziennie w nicosc (nowa kampania: start ok. 2.4 mln; dotad '
          '4.1 mln i zmiana dobowa -17.6 tys.)')
    if kap:
        ok = (not kzm or mediana(kzm) >= KAPITAL_WARSZTATOW_ZM_MIN) and (
            not ses.fakty.get('nowa_kampania') or KAPITAL_WARSZTATOW_START[0] <= kap[0] <= KAPITAL_WARSZTATOW_START[1])
        K.gdy(ok, co, 'kapital warsztatow %s -> %s; zmiana dobowa: mediana %s' % (skr(kap[0]), skr(kap[-1]), _zakres(kzm)),
              ps[-1][1].get('_nr'))
    else:
        K.brak(co, 'nie ma linii "Pieniadz swiata:" z kapitalem warsztatow')
    km = ses.ser('Przeplywy osad (kasy miast)')
    wv = _wart(km, 'warsztaty_tow')
    co = '"Przeplywy osad (kasy miast)": nowa pozycja "warsztaty towarowe - place i utrzymanie z kapitalu warsztatow +..."'
    if km:
        K.gdy(len(wv) >= len(km) - 1 and bool(wv) and mediana(wv) > 0, co, 'pozycja w %d z %d dob, mediana %s (place %s, utrzymanie %s)'
              % (len(wv), len(km), _zakres(wv), skr(mediana(_wart(km, 'warsztaty_tow_place'))),
                 skr(mediana(_wart(km, 'warsztaty_tow_utrz')))), km[-1][1].get('_nr'))
    else:
        K.brak(co, 'nie ma linii "Przeplywy osad (kasy miast):"')
    ut = [(d.get('utrzymanie'), d.get('warsztatow'), d.get('utrzymanie_stawka')) for _, d in wt]
    ut = [a / (b * c) for a, b, c in ut if isinstance(a, (int, float)) and b and c]
    co = 'do kas miast: utrzymanie ok. 4 d x liczba warsztatow (291 x 4 = 1164 na dobe)'
    if ut:
        K.gdy(0.85 <= mediana(ut) <= 1.05, co, 'utrzymanie wobec stawka x warsztatow: mediana %s; utrzymanie %s, place za cykle %s na dobe'
              % (_zakres([round(x, 2) for x in ut]), _zakres(_wart(wt, 'utrzymanie')), _zakres(_wart(wt, 'place'))), wt[-1][1].get('_nr'))
    potk = sum(_wart(wt, 'potkniecia'))
    bl = _bledy_z(ses, 'WorkshopTrade')
    nz, _ = najdluzsza_seria([(d.get('niezaplacone') or 0) > 0 for _, d in wt])
    bc = _wart(wt, 'bez_chetnego')
    K.gdy(potk == 0 and not bl and nz <= 3 and not (len(bc) >= 4 and bc[-1] > bc[0] > 0),
          'sygnaly bledu: potkniecia 0, ERROR "WorkshopTrade." brak, "niezaplacone" nie stale > 0, "bez chetnego z pieniedzmi" nie rosnie',
          'potkniecia %d; bledy: %s; niezaplacone > 0 najdluzej %d dob z rzedu; bez chetnego %s; bankructwa razem %s'
          % (potk, _opis_bledow(bl), nz, _pierw_ost_txt(wt, 'bez_chetnego'), skr(sum(_wart(wt, 'bankructwa')))), wt[-1][1].get('_nr'))
    K.dodaj(OK, 'pomiar: wynik dnia warsztatow, cena kupna dla gracza (mediana, od-do), wyplaty wlascicielom',
            'wynik dnia: mediana %s; cena kupna: mediana %s (ostatnio od %s do %s); wyplaty notablom %s, lordom %s na dobe'
            % (_zakres(_wart(wt, 'wynik')), _zakres(_wart(wt, 'cena_med')), skr(wt[-1][1].get('cena_min')), skr(wt[-1][1].get('cena_max')),
               _zakres(_wart(wt, 'wypl_notable')), _zakres(_wart(wt, 'wypl_lordowie'))), wt[-1][1].get('_nr'))


def _k117(K, ses):
    """Ogniwo 117 karawany: ruda dociera (docs\\paczki\\117-karawany-ruda-dociera.md, "Po czym poznac w logu")."""
    _start(K, ses, '117', 'start: druga linia "CaravanBulk: poprawka 115 - zakup przed wyborem celu (BK BuyGoods): latka wpieta; juki na '
                          'surowce do 100% udzwigu"', ['latka wpieta', 'do 100% udzwigu'], ('BRAK', 'NIE wpiet', 'wylaczone w ustawieniach'))
    pr, kr, kar = ses.ser('Karawany (przyczyny)'), ses.ser('Karawany (kierunek)'), ses.ser('Karawany')
    pelne = len(ses.pelne())
    if not pr or not kr:
        K.brak('co dobe linie "Karawany (przyczyny):" i "Karawany (kierunek):"', 'nie ma ich w logu')
        return
    bl = [1 for _, d in pr if d.get('brak_latki')]
    K.gdy(len(pr) >= pelne - 1 and len(kr) >= pelne - 1 and not bl,
          'co dobe "Karawany (przyczyny):" i "Karawany (kierunek):", bez "BRAK latki BuyGoods"',
          '"(przyczyny)" %d, "(kierunek)" %d linii w %d dobach; BRAK latki w %d' % (len(pr), len(kr), pelne, len(bl)),
          pr[-1][1].get('_nr'))
    przed, przy = sum(_wart(pr, 'przed_celem')), sum(_wart(pr, 'przy_wyjezdzie'))
    zak = sum(_wart(kar, 'z_zakupem'))
    K.gdy((przed + przy) > 0 and 100.0 * przy / (przed + przy) <= KARAWANY_PRZY_WYJEZDZIE_PROC,
          'zakup przed wyborem celu N prawie rowny liczbie wyjazdow z zakupem, "przy wyjezdzie M" bliskie 0',
          'przed celem %s, przy wyjezdzie %s wizyt w %d dobach (%s%%); wyjazdow z zakupem ("Karawany:") %s'
          % (skr(przed), skr(przy), len(pr), skr(round(100.0 * przy / (przed + przy), 1)) if (przed + przy) else '-', skr(zak)),
          pr[-1][1].get('_nr'))
    kp = [(bi, d.get('kg_ponad')) for bi, d in pr if d.get('kg_ponad') is not None]
    K.gdy(bool(kp) and len([1 for _, v in kp if v > 0]) >= 0.5 * len(kp), '"surowce zajely X kg jukow, z tego Y kg ponad 80% udzwigu" - Y '
          'wyraznie > 0', 'Y > 0 w %d z %d dob; Y: mediana %s kg, X: mediana %s kg; prog %s%%'
          % (len([1 for _, v in kp if v > 0]), len(kp), _zakres(_wart(pr, 'kg_ponad')), _zakres(_wart(pr, 'kg')),
             skr(pr[-1][1].get('prog'))), pr[-1][1].get('_nr'))
    bm, kru, sru = _wart(kar, 'odp_brak_miejsca'), _wart(kar, 'kup_ruda'), _wart(kar, 'sprz_ruda')
    if kar:
        K.gdy(bool(bm) and mediana(bm) <= KARAWANY_BRAK_MIEJSCA_2B, '"zakup odpuszczony: brak miejsca w jukach" z ok. 70 do kilku dziennie',
              'mediana %s na dobe (dotad 37, pod koniec 72)' % _zakres(bm), kar[-1][1].get('_nr'))
        K.gdy(bool(kru) and mediana(kru) >= KARAWANY_KUP_RUDY_2B, '"kupione z nadwyzek: ruda" z ok. 19 do ok. 35-55 dziennie',
              'mediana %s ladunkow na dobe (dotad 10), razem %s' % (_zakres(kru), skr(sum(kru))), kar[-1][1].get('_nr'))
        K.gdy(bool(sru) and mediana(sru) >= KARAWANY_SPRZ_RUDY_2B, '"sprzedane miastom z brakiem: ruda" z ok. 4 do ok. 10-25 dziennie',
              'mediana %s ladunkow na dobe (dotad 1), razem %s' % (_zakres(sru), skr(sum(sru))), kar[-1][1].get('_nr'))
    e, fb = sum(_wart(kr, 'ruda_wyj')), sum(_wart(kr, 'ruda_wyj_brak'))
    if e:
        u = fb / float(e)
        obraz = 'ROZPROSZONY (karawany wioza rude tam, gdzie brakuje)' if u >= 0.5 else (
            'SZYNY (rude zostawiaja tam, dokad jada po inne towary - nastepny krok to wozy wsi gorniczych, nie karawany)' if u < 0.25
            else 'posredni')
        K.dodaj(OK, 'pomiar: obraz ruchu rudy - "wyjazdy E, w tym z celem w miescie z brakiem F": F >= polowy E = rozproszony, F < 1/4 E = '
                    'szyny', 'F %s z E %s (%s%%) = %s; wjazdy z ruda do miasta z brakiem %s z %s, dostawe dostalo miast: mediana %s'
                % (skr(fb), skr(e), skr(round(100.0 * u, 1)), obraz, skr(sum(_wart(kr, 'ruda_wj_brak'))), skr(sum(_wart(kr, 'ruda_wj'))),
                   _zakres(_wart(kr, 'ruda_dostalo'))), kr[-1][1].get('_nr'))
    else:
        K.brak('pomiar: obraz ruchu rudy (szyny / rozproszony)', 'w liniach "Karawany (kierunek):" nie ma wyjazdow z ruda')
    st = ses.ser('Karawany (stan)')
    po = _pierw_ost(st, 'ruda_bez')
    if po:
        K.dodaj(OK, 'pomiar: "Karawany (stan): ruda: bez towaru" - same karawany: ok. 40-60 po 20 dobach (zamiast 69); ponizej 20 to zadanie '
                    'wozow wsi (119)', '%s -> %s miast (doby %d-%d); w jukach karawan %s -> %s ladunkow rudy'
                % (po[0], po[1], po[2], po[3], skr(st[0][1].get('ruda_juki')), skr(st[-1][1].get('ruda_juki'))), st[-1][1].get('_nr'))
    wj, wm = _wart(kar, 'wjazdy'), _wart(kr, 'w_miastach')
    if wj:
        rosnie = len(wm) >= 2 and wm[-1] > 1.2 * max(1, wm[0])
        K.gdy(mediana(wj) >= KARAWANY_WJAZDY_MIN_2B or not rosnie, '"wjazdy karawan do miast" (dotad ok. 85-90 na dobe) - wyraznie mniej przy '
              'rosnacym "karawan w miastach" = karawany przeciazone (CaravanBulkFillLimit na 0.8)',
              'wjazdy: mediana %s; karawan w miastach %s, w drodze %s' % (_zakres(wj), _pierw_ost_txt(kr, 'w_miastach'),
                                                                        _pierw_ost_txt(kr, 'w_drodze')), kar[-1][1].get('_nr'))
    bl = _bledy_z(ses, 'CaravanBulk')
    K.gdy(not bl, 'bez ERROR "CaravanBulk." (BuyGoods, rozkazy BK)', _opis_bledow(bl), bl[0][2] if bl else None)


def _k118(K, ses):
    """Ogniwo 118 towary BK w nowej monecie (docs\\paczki\\118-towary-w-nowej-monecie.md, "Po czym poznac w logu")."""
    f = ses.fakty
    _start(K, ses, '118', 'start: "HistoricalPrices: wartosc z definicji towarow BK (BKItems.InitializeTradeGood) - wpieta (przed latkami '
                          'innych modow)"', ['wpieta'], ('BRAK', 'NIE wpiet'))
    w = f.get('hp_popyt')
    co = ('"popyt miast przeliczony na nowa monete (CZYNNE) w 72 kategoriach" z "bread /3.3, mead /12, ..." (test 14:08: 64 kategorie, bez '
          'chleba, mead /0.1)')
    if w is not None:
        m = re.search(r'\((\w+)\) w (\d+) kategoriach: (.*)$', w.tresc)
        prz = dict((a, _liczba(b)) for a, b in re.findall(r'(\S+) /(\d+(?:\.\d+)?)', m.group(3))) if m else {}
        n = int(m.group(2)) if m else 0
        mead = prz.get('mead')
        K.gdy(bool(m) and m.group(1) == 'CZYNNE' and 'bread' in prz and isinstance(mead, (int, float)) and mead >= 1.0, co,
              '%s w %d kategoriach; bread /%s, mead /%s, Eggs /%s, fur /%s, gems /%s' % (m.group(1) if m else '?', n, skr(prz.get('bread')),
                                                                                   skr(mead), skr(prz.get('Eggs')), skr(prz.get('fur')),
                                                                                   skr(prz.get('gems'))), w.nr)
    else:
        K.brak(co, 'nie ma linii "HistoricalPrices: popyt miast przeliczony na nowa monete"')
    w = f.get('hp_przelicznik')
    co = '"przelicznik popytu od wartosci z definicji przedmiotu CZYNNY (...); wziete z definicji 18 [...] w 15 kategoriach"'
    if w is not None:
        m = re.search(r'wziete z definicji (\d+) \[.*\] w (\d+) kategoriach', w.tresc)
        K.gdy('przedmiotu CZYNNY' in w.tresc and bool(m) and int(m.group(1)) > 0, co,
              ('wziete z definicji %s w %s kategoriach' % m.groups()) if m else wycinek(w.tresc, 110), w.nr)
    else:
        K.brak(co, 'nie ma tej linii (pisze ja HistoricalPrices.Apply po wejsciu do kampanii)')
    w = f.get('hp_uwaga0')
    K.gdy(w is None, 'BRAK linii "HistoricalPrices: UWAGA - ... przeliczonych przedmiotow ma wartosc 0"',
          wycinek(w.tresc, 110) if w is not None else 'takiej linii nie ma', w.nr if w is not None else None)
    s = f.get('rawprice_seed')
    if s is not None:
        m = re.search(r', (\d+) kategorii \(', s.tresc)
        K.gdy(bool(m) and int(m.group(1)) >= 44, 'nowa kampania: "RawPrice: nowa kampania - ... ok. 46 kategorii" (bylo 38)',
              ('%s kategorii' % m.group(1)) if m else wycinek(s.tresc, 100), s.nr)
    ce = ses.ser('Ceny surowcow')
    dk = [(bi, d) for bi, d in ce if d.get('def_kat') is not None]
    co = 'co dobe w "Ceny surowcow:" czesc "Towary z wartoscia z definicji przedmiotu" (chleb, ciasta, miod, futro, zloto...)'
    if not dk:
        K.brak(co, 'linie "Ceny surowcow:" bez tej czesci' if ce else 'nie ma linii "Ceny surowcow:"')
        return
    K.gdy(len(dk) >= len(ce) - 1 and all((d.get('def_zle') or 0) == 0 for _, d in dk), co,
          'czesc jest w %d z %d dob; kategorii %s; bez przelicznika %s; nierozpoznanych pozycji %s'
          % (len(dk), len(ce), _zakres(_wart(dk, 'def_kat')), _zakres(_wart(dk, 'def_bez_przel')), skr(sum(_wart(dk, 'def_zle')))),
          dk[-1][1].get('_nr'))
    bm, bb = _wart(dk, 'def_bread_med'), _wart(dk, 'def_bread_budzet')
    co = ('chleb: mediana indeksu ponizej 10, "na polkach" N sztuk (pomiar doplywu), "budzet mieszczan" ok. 15-20 tys. (przy wylaczonym '
          'wlaczniku 30-67 tys.)')
    if bm:
        K.gdy(bm[-1] < INDEKS_SUFIT and bool(bb) and BUDZET_CHLEBA[0] <= mediana(bb) <= BUDZET_CHLEBA[1], co,
              'indeks chleba: mediana po dobach %s (ostatnio %s); na polkach %s szt.; budzet mieszczan %s d; pusto w %s miastach'
              % (_zakres(bm), skr(bm[-1]), _pierw_ost_txt(dk, 'def_bread_szt'), _zakres(bb), _pierw_ost_txt(dk, 'def_bread_pusto')),
              dk[-1][1].get('_nr'))
    else:
        K.brak(co, 'w czesci o towarach z definicji nie ma chleba (bread)')
    drogie = [(k, _wart(dk, 'def_%s_med' % k)) for k in ('fur', 'mead', 'gold', 'ink', 'dyes')]
    drogie = [(k, v) for k, v in drogie if v]
    co = ('futro, miod pitny, zloto, atrament, barwnik: mediana indeksu wyraznie ponizej 10 (dotad 10), "przy dzisiejszych polkach do" '
          'kilku-kilkunastu tys.')
    if drogie:
        zle = [k for k, v in drogie if mediana(v) >= INDEKS_SUFIT]
        K.gdy(not zle, co, ', '.join('%s %s (wydane do %s d)' % (k, skr(mediana(v)), skr(dk[-1][1].get('def_%s_wydane' % k)))
                                     for k, v in drogie) + (('; NA SUFICIE: ' + ', '.join(zle)) if zle else ''), dk[-1][1].get('_nr'))
    else:
        K.brak(co, 'w czesci o towarach z definicji nie ma tych kategorii')
    tanie = [(k, _wart(dk, 'def_%s_med' % k), _wart(dk, 'def_%s_szt' % k)) for k in ('limestone', 'marble')]
    tanie = [x for x in tanie if x[1]]
    if tanie:
        K.dodaj(OK, 'pomiar: wapien i marmur - setki sztuk na polkach i indeks na podlodze 0.1',
                ', '.join('%s: indeks %s, na polkach %s szt.' % (k, _zakres(v), _zakres(n)) for k, v, n in tanie), dk[-1][1].get('_nr'))
    bil, prz = ses.ser('Pieniadz swiata (bilans)'), ses.ser('Przeplywy osad')
    if bil or prz:
        K.dodaj(OK, 'pomiar: "zakupy mieszkancow miast i zamkow" nizsze (kilkadziesiat do ok. 250 tys. dziennie - zalezy od futra), "miasta '
                    'zaplacily" taborom nizsze o podobna kwote', 'zakupy mieszkancow: mediana %s (dotad 462.4k); miasta zaplacily: mediana %s '
                '(dotad 380.0k); budzet mieszczan na towary z definicji: mediana %s, wydane %s'
                % (skr(mediana(_wart(bil, 'zakupy'))), skr(mediana(_wart(prz, 'miasta_zaplacily'))), skr(mediana(_wart(dk, 'def_budzet'))),
                   skr(mediana(_wart(dk, 'def_wydane')))), (bil or prz)[-1][1].get('_nr'))
    bl = _bledy_z(ses, 'HistoricalPrices.DefinePostfix', 'HistoricalPrices.ApplyAll (BKItems', 'RawPrice.Daily (budzet BK)')
    K.gdy(not bl, 'bez ERROR "HistoricalPrices.DefinePostfix", "HistoricalPrices.ApplyAll (BKItems.InitializeTradeGood)", "RawPrice.Daily '
                  '(budzet BK)"', _opis_bledow(bl), bl[0][2] if bl else None)


def _k119(K, ses):
    """Ogniwo 119 wozy wsi do najlepiej placacego miasta (docs\\paczki\\119-wozy-do-najlepszego-miasta.md, "Po czym poznac w logu")."""
    _start(K, ses, '119', 'start: "MarketCarts: poprawka 119 - wozy wsi do najlepiej placacego miasta w zasiegu 250 (CZYNNE; ...), cena '
                          'ladunku sztuka po sztuce - latka wpieta (CZYNNA), woz x2.0 dla wsi wszystkich"',
           ['w zasiegu 250', '(CZYNNE;', 'latka wpieta (CZYNNA)', 'dla wsi wszystkich'], ('BRAK', 'NIE wpiet'))
    o = ogniwo(ses, '100')
    K.gdy(bool(o and o['jest']), 'start: "MarketRoad: wsie zamkowe woza plon na targ miasta" (jak dotad)',
          wycinek(o['wpis'].tresc, 100) if o and o['wpis'] else 'nie ma tej linii', o['wpis'].nr if o and o['wpis'] else None)
    wz = ses.ser('Dowoz (wozy)')
    if not wz:
        K.brak('co dobe linia "Dowoz (wozy):"', 'nie ma jej w logu')
        return
    pelne = len(ses.pelne())
    cz = [1 for _, d in wz if d.get('wybor') == 'CZYNNY' and d.get('cena_stan') == 'CZYNNA']
    K.gdy(len(wz) >= pelne - 1 and len(cz) == len(wz),
          'co dobe "Dowoz (wozy):" - "wybor miasta CZYNNY", "cena ladunku sztuka po sztuce CZYNNA"',
          '%d linii w %d dobach, oba czynne w %d; woz x%s dla wsi %s' % (len(wz), pelne, len(cz), skr(wz[-1][1].get('woz_x')),
                                                                      wz[-1][1].get('dla_wsi')), wz[-1][1].get('_nr'))
    n = _wart(wz, 'wozow')
    K.gdy(bool(n) and WOZY_N_2B[0] <= mediana(n) <= WOZY_N_2B[1], '"N wozow" ok. 150-250 na dobe',
          'wozow: mediana %s (wsi zamkowych %s); do wlasnego miasta %s, bez miasta w zasiegu %s, w miescie %s'
          % (_zakres(n), _zakres(_wart(wz, 'wsi_zamkowych')), _zakres(_wart(wz, 'do_wlasnego')), _zakres(_wart(wz, 'bez_miasta')),
             _zakres(_wart(wz, 'w_miescie'))), wz[-1][1].get('_nr'))
    di = _wart(wz, 'do_innego_proc')
    K.gdy(bool(di) and mediana(di) >= WOZY_DO_INNEGO_MIN_PROC, '"do innego" miasta wyraznie > 0 (symulacja: ok. 80% kursow surowcow)',
          'do innego: mediana %s%% wozow (%s na dobe)' % (_zakres(di), _zakres(_wart(wz, 'do_innego'))), wz[-1][1].get('_nr'))
    sr = _wart(wz, 'srednio')
    K.gdy(bool(sr) and WOZY_DROGA_2B[0] <= mediana(sr) <= WOZY_DROGA_2B[1], '"srednio X jedn. drogi" ok. 60-140 (zasieg 250)',
          'srednio %s jedn. drogi; zasieg %s (gry dla targu wsi %s)' % (_zakres(sr), skr(wz[-1][1].get('zasieg')),
                                                                       skr(wz[-1][1].get('zasieg_gry'))), wz[-1][1].get('_nr'))
    j = _wart(wz, 'z_ruda')
    if j:
        pierwsze = wz[:5]
        jj, kk = sum(_wart(pierwsze, 'z_ruda')), sum(_wart(pierwsze, 'ruda_do_pustych'))
        K.dodaj(OK, 'pomiar: "z ruda J" ok. 4-6 na dobe, "w tym do miasta bez rudy K" ok. polowy J w pierwszych dobach, "roznych '
                    'miast L" rosnie', 'z ruda: mediana %s; do miasta bez rudy w pierwszych %d dobach %s z %s (%s%%); roznych miast %s'
                % (_zakres(j), len(pierwsze), skr(kk), skr(jj), skr(round(100.0 * kk / jj, 1)) if jj else '-',
                   _pierw_ost_txt(wz, 'ruda_miast')),
                wz[-1][1].get('_nr'))
    nz, cr = sum(_wart(wz, 'niezgodne')), sum(_wart(wz, 'cena_rosla'))
    K.gdy(nz == 0 and cr == 0, '"niezgodne" 0 i "cena rosla" 0 (inaczej korekta ceny ladunku nie dziala - kto zmienia cene osady?)',
          'niezgodne razem %s, cena rosla razem %s w %d dobach' % (skr(nz), skr(cr), len(wz)), wz[-1][1].get('_nr'))
    ms = _wart(wz, 'ms')
    K.gdy(bool(ms) and mediana(ms) <= WOZY_MS_MAKS, 'ms na dobe - kilkadziesiat (setki = za drogo, ponad ok. 300 zglosic)',
          'ms: mediana %s, najwiecej %s; wycen %s, cen %s na dobe' % (_zakres(ms), skr(max(ms)) if ms else '-', _zakres(_wart(wz, 'wycen')),
                                                                     _zakres(_wart(wz, 'cen'))), wz[-1][1].get('_nr'))
    pt = sum(_wart(wz, 'potkn'))
    bl = _bledy_z(ses, 'MarketCarts', 'MarketRoad')
    K.gdy(pt == 0 and not bl, 'POTKNIECIA - brak; bez ERROR "MarketCarts." / "MarketRoad."', 'potkniecia razem %d; bledy: %s'
          % (pt, _opis_bledow(bl)), bl[0][2] if bl else wz[-1][1].get('_nr'))
    wg = _wart(wz, 'wygasle')
    if wg:
        K.gdy(mediana(wg) <= WOZY_WYGASLE_MAKS, '"wygasle przy porzadkach N" - kilka-kilkanascie na dobe to norma',
              'wygasle: mediana %s; wiesc z drogi: wozow w drodze %s, miast %s' % (_zakres(wg), _pierw_ost_txt(wz, 'w_drodze'),
                                                                                  _pierw_ost_txt(wz, 'miast_wiesc')), wz[-1][1].get('_nr'))
    p, q = sum(_wart(wz, 'pierwsza')), sum(_wart(wz, 'sztuka'))
    npr = _wart(wz, 'nadplata_proc')
    co = ('nadplata wozow: "tabory oddaly osadom nadplate P-Q d (R%%)" - kilka-kilkanascie procent (ponad %s%% = uwaga)'
          % skr(NADPLATA_PROC_MAKS))
    if p:
        K.gdy(100.0 * (p - q) / p <= NADPLATA_PROC_MAKS, co,
              'razem po cenie pierwszej sztuki %s d, sztuka po sztuce %s d, nadplata %s d (%s%%); na dobe: mediana %s%%, najwieksza '
              'jednorazowa %s; sprzedazy %s' % (skr(p), skr(q), skr(p - q), skr(round(100.0 * (p - q) / p, 1)), _zakres(npr),
                                                 skr(max(_wart(wz, 'nadplata_max') or [0])), skr(sum(_wart(wz, 'sprzedazy')))),
              wz[-1][1].get('_nr'))
    else:
        K.brak(co, 'zadnej sprzedazy taborow z korekta ceny ("sprzedazy 0")')
    pe = _wart(wz, 'pelny')
    K.dodaj(OK, 'pomiar: pelny woz na daleka droge, oczekiwany utarg (tam, gdzie wlasne miasto bylo w grze, wobec utargu we wlasnym)',
            'pelny woz: mediana %s (doladowane %s szt.); oczekiwany utarg: mediana %s d; wybrane %s wobec wlasnego %s d'
            % (_zakres(pe), skr(sum(_wart(wz, 'doladowane'))), skr(mediana(_wart(wz, 'utarg'))), skr(sum(_wart(wz, 'utarg_wybrane'))),
               skr(sum(_wart(wz, 'utarg_wlasne')))), wz[-1][1].get('_nr'))
    dr = ses.ser('Drewno')
    po = _pierw_ost(dr, 'bez_towaru')
    co = '"Drewno: miast bez towaru" (dotad 41 -> 40) ok. 10-15 po 20 dobach'
    if po:
        n_dob = len(ses.pelne())
        K.gdy(po[1] <= DREWNO_BEZ_TOWARU_CEL_2B or (n_dob < TOWARY2_MIN_DOB and po[1] <= po[0]), co, '%s -> %s miast bez drewna (doby %d-%d)'
              % (po[0], po[1], po[2], po[3]), dr[-1][1].get('_nr'))
    else:
        K.brak(co, 'nie ma linii "Drewno:"')
    sk = ses.ser('Dowoz (skutki)')
    if sk:
        d = sk[-1][1]
        za, zm = d.get('zatk_zamkowe_proc'), d.get('zatk_miejskie_proc')
        K.gdy(za is not None and zm is not None and zm <= BAZA_1408['zatk_miejskie'] and za <= BAZA_1408['zatk_zamkowe'] + 3.0,
              '"Dowoz (skutki): zatkane magazyny" - wsie miejskie spadna z ok. 7% (woz), zamkowe moga wzrosnac o 1-3 punkty',
              'ostatnia doba: zamkowe %s%%, miejskie %s%% (dotad 4.6%% / 7.4%%); zamki z ujemnym bilansem zywnosci %s (dotad 0 -> 1)'
              % (skr(za), skr(zm), _pierw_ost_txt(sk, 'zamki_glodne')), d.get('_nr'))
    ru = ses.ser('Ruda')
    po = _pierw_ost(ru, 'bez_towaru')
    if po:
        K.dodaj(OK, 'pomiar: "Ruda: miast bez towaru" - GLOWNA LICZBA grupy 2b (ocena: --grupa 2b; cel ok. 25-30 po 20 dobach)',
                '%s -> %s miast bez rudy (doby %d-%d; dotad 74 -> 69); "Karawany (stan): ruda: bez towaru" %s'
                % (po[0], po[1], po[2], po[3], _pierw_ost_txt(ses.ser('Karawany (stan)'), 'ruda_bez')), ru[-1][1].get('_nr'))


FUNKCJE_KONTROLI = OrderedDict([('100', _k100), ('101', _k101), ('102', _k102), ('102b', _k102b), ('103', _k103),
                                ('104', _k104), ('105', _k105), ('106', _k106), ('107', _k107), ('115', _k115),
                                ('116', _k116), ('117', _k117), ('118', _k118), ('119', _k119), ('108', _k108),
                                ('109', _k109), ('110', _k110), ('111', _k111), ('112', _k112), ('113', _k113)])


def kontrole_ogniwa(ses, nr):
    """Lista kontroli jednego ogniwa: [(status, co, wynik, numer linii)]. Nigdy wyjatek."""
    K = Kontrole()
    f = FUNKCJE_KONTROLI.get(nr)
    if f is None:
        K.brak('ogniwo %s' % nr, 'nieznany numer ogniwa')
        return K.lista
    # Ogniwa nie widac w logu: nie ma czego oceniac (inaczej linie wspolne ze starszym kodem dawalyby mylace OK albo UWAGA).
    # Wyjatek: 104 nie ma linii przy starcie gry - jego kontrole same rozrozniaja nowa kampanie i wczytany zapis.
    if nr != '104' and not ma_ogniwo(ses, nr):
        o = ogniwo(ses, nr)
        K.brak('ogniwo %s w logu' % nr, 'nie widac go (nie wgrane?) - poznac po: %s' % (o['poznac'] if o else '?'))
        return K.lista
    try:
        f(K, ses)
    except Exception as e:
        K.dodaj(BD, 'kontrole ogniwa %s przerwane' % nr, 'blad wewnetrzny (%s: %s) - reszta skrotu bez zmian' % (type(e).__name__, e))
    return K.lista


def ogniwa_z_nazwy(ses, nazwa):
    """Numery ogniw dla opcji --test: numer, lista, zakres, grupa testowa, slowo z opisu, 'wykryte', 'w-grze'."""
    q = (nazwa or '').strip().lower()
    if not q or q in ('w-grze', 'wgrze', 'gra'):
        return [ses.ogniwo_w_grze] if ses.ogniwo_w_grze else []
    if q in ('wykryte', 'lancuch'):
        return [o['nr'] for o in ses.ogniwa if ma_ogniwo(ses, o['nr'])]
    wyn = []
    for czesc in re.split(r'[,+ ]+', q):
        if not czesc:
            continue
        m = re.match(r'^(\d{3}b?)(?:-|\.\.)(\d{3}b?)$', czesc)
        if m and m.group(1) in NR_OGNIW and m.group(2) in NR_OGNIW:
            a, b = sorted((ix_ogniwa(m.group(1)), ix_ogniwa(m.group(2))))
            wyn.extend(NR_OGNIW[a:b + 1])
        elif czesc in NR_OGNIW:
            wyn.append(czesc)
        elif czesc in GRUPY_TESTOWE:
            wyn.extend(GRUPY_TESTOWE[czesc])
        else:
            traf = [o[0] for o in OGNIWA if czesc in o[1].lower() or czesc in o[7].lower()]
            wyn.extend(traf)
    return [n for i, n in enumerate(wyn) if n not in wyn[:i]]


def zawin(tekst, szer, maks=3, wciecie='        '):
    """Lamie dluga linie na najwyzej `maks` linii o szerokosci `szer` (ciag dalszy z wcieciem); nadmiar jest obciety."""
    if len(tekst) <= szer or maks <= 1:
        return [obetnij(tekst, szer)]
    wyn, reszta = [], tekst
    while reszta and len(wyn) < maks:
        miejsce = szer if not wyn else szer - len(wciecie)
        if len(reszta) <= miejsce:
            kawalek, reszta = reszta, ''
        else:
            ciecie = reszta.rfind(' ', 0, miejsce + 1)
            if ciecie < miejsce // 2:
                ciecie = miejsce
            kawalek, reszta = reszta[:ciecie], reszta[ciecie:].lstrip()
        wyn.append(kawalek if not wyn else wciecie + kawalek)
    if reszta:
        wyn[-1] = obetnij(wyn[-1] + ' ' + reszta, szer)
    return wyn


def linie_kontroli(ses, nr, szer, limit=None, naglowek=True, pelne=False):
    """Sekcja kontroli jednego ogniwa. pelne = kazda kontrola w calosci (zawijana do 3 linii); inaczej kontrola OK to jedna
    linia, a UWAGA i BRAK DANYCH najwyzej dwie (to je trzeba umiec przepisac w calosci)."""
    o = ogniwo(ses, nr)
    lista = kontrole_ogniwa(ses, nr)
    licz = Counter(k[0] for k in lista)
    L = []
    if naglowek:
        jest = ma_ogniwo(ses, nr)
        L.append(obetnij('--- CO SPRAWDZIC DLA OGNIWA %s %s (zrodlo: %s)%s: OK %d, UWAGA %d, BRAK DANYCH %d ---'
                         % (nr, o['opis'] if o else '', o['paczka'] if o else '?', '' if jest else ' - OGNIWA NIE WIDAC W LOGU',
                            licz[OK], licz[UW], licz[BD]), szer))
    pokaz = lista
    if limit and len(lista) > limit:
        # przy limicie najpierw to, co wymaga uwagi, potem reszta w kolejnosci z paczki
        wazne = [k for k in lista if k[0] != OK]
        reszta = [k for k in lista if k[0] == OK]
        pokaz = (wazne + reszta)[:limit]
        pokaz = [k for k in lista if k in pokaz]
    for status, co, wynik, nr_linii in pokaz:
        gdzie = ('linia %d' % nr_linii) if nr_linii else 'caly log'
        L.extend(zawin('  [%s] %s: %s -> %s' % (status, gdzie, co, wynik), szer, 3 if pelne else (1 if status == OK else 2)))
    if len(pokaz) < len(lista):
        L.append('  ... i %d dalszych kontroli tego ogniwa (--test %s)' % (len(lista) - len(pokaz), nr))
    return L


def linia_pozostalych_ogniw(ses, szer):
    """Jedna linia o ogniwach widocznych w logu poza najwyzszym: ktore maja kontrole z UWAGA albo BRAK DANYCH."""
    inne = [o['nr'] for o in ses.ogniwa if ma_ogniwo(ses, o['nr']) and o['nr'] != ses.ogniwo_w_grze]
    if not inne:
        return []
    czyste, z_uwagami = [], []
    for nr in inne:
        licz = Counter(k[0] for k in kontrole_ogniwa(ses, nr))
        if licz[UW] or licz[BD]:
            z_uwagami.append((nr, licz))
        else:
            czyste.append(nr)
    if not z_uwagami:
        return [obetnij('  pozostale ogniwa w logu (%s): wszystkie kontrole OK | szczegoly: --test wykryte' % ', '.join(czyste),
                        szer)]
    opis = ', '.join('%s%s%s' % (nr, (' UWAGA %d' % licz[UW]) if licz[UW] else '', (' BRAK DANYCH %d' % licz[BD]) if licz[BD]
                                 else '') for nr, licz in z_uwagami)
    return [obetnij('  pozostale ogniwa w logu: z uwagami %s | bez uwag: %s | szczegoly: --test %s'
                    % (opis, ', '.join(czyste) or 'zadne', ','.join(nr for nr, _ in z_uwagami)), szer)]


def drukuj_test(ses, nazwa, szer=SZEROKOSC):
    """Wyjscie opcji --test: kontrole wybranych ogniw w calosci."""
    numery = ogniwa_z_nazwy(ses, nazwa)
    L = []
    if not numery:
        if (nazwa or '').strip().lower() in ('', 'w-grze', 'wgrze', 'gra', 'wykryte', 'lancuch'):
            L.append('--test "%s": w tym logu nie widac zadnego ogniwa lancucha (sesja sprzed wpisu 100 albo sam start gry).'
                     % nazwa)
        else:
            L.append('--test "%s": nie ma takiego ogniwa ani grupy.' % obetnij(nazwa, 60))
        L.append(obetnij('  ogniwa: %s | takze: wykryte, w-grze, wszystkie' % ', '.join(NR_OGNIW), szer))
        L.append(obetnij('  grupy: %s' % ', '.join('%s = %s' % (k, '+'.join(v)) for k, v in GRUPY_TESTOWE.items()
                                                    if k != 'wszystkie'), szer))
        L.append(obetnij('  grupy testowe w grze: --grupa 1, 2, 2b, 3, 4, 5 (%s)'
                         % ', '.join('%s = %s%s' % (k, 'BetterEconomy + ' if o['bee'] else '', '+'.join(o['ogniwa']))
                                     for k, o in GRUPY_GRY.items()), szer))
        return L
    w_grze = ses.ogniwo_w_grze
    L.append(obetnij('=== TEST: ogniwa %s | OGNIWO W GRZE (wg linii logu): %s | doby: %d ==='
                     % (', '.join(numery), w_grze or 'sprzed wpisu 100', len(ses.pelne()) if ses.bloki else 0), szer))
    razem = Counter()
    for nr in numery:
        blok = linie_kontroli(ses, nr, szer, pelne=True)
        L.extend(blok)
        razem.update(k[0] for k in kontrole_ogniwa(ses, nr))
    al = [a for a in ses.alarmy if a[0] == 'ALARM']
    L.append(obetnij('=== RAZEM: OK %d, UWAGA %d, BRAK DANYCH %d | w calym logu: ERROR %d, Exception %d, alarmy %d, uwagi %d '
                     '(pelny skrot: bez --test) ===' % (razem[OK], razem[UW], razem[BD], ses.n_error, ses.n_exception, len(al),
                                                       len(ses.alarmy) - len(al)), szer))
    return L


# =====================================================================================================================
# GRUPY TESTOWE (--grupa N): warunki testu, kontrole wspolne grupy i wszystkie kontrole jej ogniw naraz.
# Lista kontroli wspolnych grup 1-3: sekcja test_groups w docs\paczki\PRZEGLAD-KOLIZJI-LANCUCHA-2026-10-06.txt (tylko to,
# czego nie ma juz w kontrolach ogniw); grupy 4-5: powiazania miedzy liniami ogniw 110-113 z docs\paczki\110..113.
# =====================================================================================================================
SCIEZKA_BEE = None      # None = plik ustawien BetterEconomy obok katalogu logu, potem w katalogu gry (test podmienia sciezke)
SCIEZKA_CSV = None      # None = plik ludzie-regiony.csv sesji wedle sciezki logu (test podmienia sciezke)
KULTURY_ZA_MUREM = ('nightswatch', 'wildlings', 'freefolk')     # id kultur ROT: Nocna Straz i Wolni Ludzie


def sciezka_bee(sciezka_logu):
    """Plik ustawien BetterEconomy: <gra>\\Modules\\BetterEconomy\\ModuleData\\better_economy_settings.xml (tylko odczyt)."""
    if SCIEZKA_BEE is not None:
        return SCIEZKA_BEE
    kandydaci = []
    try:
        kat = os.path.dirname(os.path.abspath(sciezka_logu))
        kandydaci.append(os.path.join(os.path.dirname(kat), PLIK_BEE))
    except Exception:
        pass
    kandydaci.append(os.path.join(os.path.dirname(KATALOG_LOGOW), PLIK_BEE))
    for k in kandydaci:
        if os.path.isfile(k):
            return k
    return kandydaci[0]


def stan_kluczy_bee(tekst):
    """[(klucz, wartosc z pliku albo None, wartosc docelowa, zgodny)] dla 13 kluczy zamykajacych ujscia BetterEconomy;
    trzynasty to ktorykolwiek z dwoch kluczy zbrojowni (domyslny albo z listy fundamentu)."""
    tekst = re.sub(r'<!--.*?-->', '', tekst, flags=re.S)

    def wartosc(k):
        m = re.findall(r'<%s>\s*([^<]*?)\s*</%s>' % (re.escape(k), re.escape(k)), tekst)
        return m[-1] if m else None

    def rowne(a, b):
        try:
            return abs(float(str(a).replace(',', '.')) - float(b)) < 1e-9
        except Exception:
            return False
    wyn = []
    for k, cel in KLUCZE_BEE:
        v = wartosc(k)
        wyn.append((k, v, cel, v is not None and rowne(v, cel)))
    zb = [(k, wartosc(k), cel) for k, cel in KLUCZE_BEE_ZBROJOWNIA]
    traf = [x for x in zb if x[1] is not None and rowne(x[1], x[2])]
    k, v, cel = traf[0] if traf else zb[0]
    wyn.append((k, v, cel, bool(traf)))
    return wyn


def _csv_wiersze(tekst):
    """(naglowek, wiersze jako slowniki, liczba wierszy uszkodzonych) pliku ludzie-regiony.csv."""
    linie = [x for x in tekst.splitlines() if x.strip()]
    if not linie:
        return [], [], 0
    nagl = [x.strip() for x in linie[0].split(';')]
    wiersze, zle = [], 0
    for x in linie[1:]:
        p = x.split(';')
        if len(p) < len(nagl):
            zle += 1
            continue
        wiersze.append(dict((nagl[i], p[i].strip()) for i in range(len(nagl))))
    return nagl, wiersze, zle


def _csv_sesji(ses):
    """Plik ludzie-regiony.csv sesji: (sciezka, naglowek, wiersze); naglowek None = pliku nie ma albo nie da sie go czytac."""
    try:
        sc = SCIEZKA_CSV if SCIEZKA_CSV is not None else sciezka_csv(ses.sciezka)
        if not sc or not os.path.isfile(sc):
            return sc, None, None
        with open(sc, 'rb') as f:
            tekst, _ = dekoduj(f.read())
        nagl, wiersze, _ = _csv_wiersze(tekst)
        return sc, nagl, wiersze
    except Exception:
        return None, None, None


def _ogon(sciezka, n):
    """Sciezka skrocona od lewej (koniec sciezki mowi wiecej niz poczatek)."""
    s = str(sciezka)
    return s if len(s) <= n else '...' + s[-(n - 3):]


def _lf(s):
    """Tekst komorki CSV -> float albo None."""
    try:
        v = float(str(s).replace(',', '.'))
    except (TypeError, ValueError):
        return None
    return v if (v == v and abs(v) != float('inf')) else None       # "nan" i "inf" w komorce to brak liczby


def _warunki_grupy(K, ses, g):
    """Warunki testu grupy: czy log w ogole jest tym testem (DLL, komplet ogniw, kampania, dlugosc, bledy, parser)."""
    opis = GRUPY_GRY[g]
    f = ses.fakty
    ogn = opis['ogniwa']
    szczyt, w_grze = ogn[-1], ses.ogniwo_w_grze
    nowa, wczytana = bool(f.get('nowa_kampania')), bool(f.get('wczytana'))
    co = 'DLL grupy: najwyzsze ogniwo w logu = %s (galaz %s)' % (szczyt, opis['dll'])
    if w_grze is None:
        K.dodaj(UW, co, 'w logu nie widac zadnego ogniwa lancucha (sesja sprzed wpisu 100 albo log bez linii startowych)')
    elif w_grze == szczyt:
        o = ogniwo(ses, szczyt)
        K.dodaj(OK, co, 'ogniwo w grze wg linii logu: %s %s' % (w_grze, o['opis'] if o else ''),
                o['wpis'].nr if o and o['wpis'] else None)
    elif ix_ogniwa(w_grze) > ix_ogniwa(szczyt):
        K.dodaj(UW, co, 'w grze jest wyzsze ogniwo %s (grupa %s) - log obejmuje tez nastepne grupy, ich skutki nakladaja sie na '
                        'liczby tej grupy' % (w_grze, grupa_ogniwa(w_grze) or '?'))
    else:
        K.dodaj(UW, co, 'najwyzsze ogniwo w logu to %s - grupa %s nie jest wgrana w calosci' % (w_grze, g))
    # ogniwo 104 nie ma linii przy starcie gry: na wczytanym zapisie poznac je po wyzszym ogniwie
    wyzsze = dict((n, any(ma_ogniwo(ses, x) for x in NR_OGNIW[ix_ogniwa(n) + 1:])) for n in ogn)
    brak = [n for n in ogn if not ma_ogniwo(ses, n) and not (n == '104' and not nowa and wyzsze[n])]
    zle = [(o['nr'], o['status']) for o in ses.ogniwa if o['nr'] in ogn and o['status'] in ('BRAK', 'NIE wpieta', 'WYLACZONE')]
    K.gdy(not brak and not zle, 'wszystkie ogniwa grupy widac w logu, linie startowe bez BRAK / NIE wpieta / WYLACZONE',
          ('ogniwa %s: komplet' % ' + '.join(ogn)) if not brak and not zle else
          ('nie widac: %s' % ', '.join(brak) if brak else '') + ('; ' if brak and zle else '') +
          ', '.join('%s [%s]' % x for x in zle))
    tryb = opis['kampania']
    stan = 'nowa kampania (ColdStart / pula poczatkowa wyrzutkow w logu)' if nowa else (
        'wczytany zapis ("Wyrzutki: wczytano pule")' if wczytana else 'nie wiadomo (brak linii startu kampanii)')
    if tryb == 'nowa':
        K.dodaj(OK if nowa else (UW if wczytana else BD), 'NOWA kampania (wymagana przez grupe)', stan + ('' if nowa else (
            ' - 104 przelicza zapas tylko przy starcie nowej kampanii' if g == '1' else (
                ' - 115 (pamiec rynku), 116 (kapital startowy warsztatow) i 118 (popyt w nowej monecie) licza start kampanii tylko w '
                'sesji, ktora ja zalozyla' if g == '2b' else
                ' - siew puli wyrzutkow i kalibracja ludnosci dzieja sie raz, przy zalozeniu kampanii'))), f.get('pula_nr'))
    elif tryb == 'zalecana':
        K.dodaj(OK if nowa else (UW if wczytana else BD), 'NOWA kampania (zalecana)', stan + ('' if nowa else (
            ' - jednorazowe przyciecie daru startowego kas moze juz byc za nami (flaga w sejwie)' if g == '4' else
            ' - konta uchodzcow nie zaczynaja od zera, wsie zbite wczesniej nie maja kto wracac')), f.get('pula_nr'))
    else:
        K.dodaj(OK if (wczytana and not nowa) else (UW if nowa else BD), 'ten sam zapis co poprzednia grupa (wczytana kampania)',
                stan + (' - bez porownania ksiegi przed / po na tym samym swiecie' if nowa else ''), f.get('pula_nr'))
    n = len(ses.pelne()) if ses.bloki else 0
    od, do = opis['dob']
    K.gdy(n >= od, ('dlugosc testu: %d-%d dob' % (od, do)) if od != do else ('dlugosc testu: %d dob' % od),
          ('%d pelnych dob (dni gry %d-%d)' % (n, ses.bloki[0].D, ses.bloki[-1].D)) if ses.bloki else 'zadnej doby w logu')
    K.gdy(len(ses.segmenty) == 1, 'jedna kampania w logu, po swiezym starcie gry',
          'kampanii / wczytan w sesji: %d%s' % (len(ses.segmenty), '' if len(ses.segmenty) == 1 else
                                              ' (analizowana nr %d) - druga gra bez restartu niesie stan statyczny modulow'
                                              % (ses.wybrany + 1)))
    K.gdy(ses.n_error == 0 and ses.n_exception == 0 and ses.n_potkniec == 0, 'bez ERROR, Exception i potkniec w calym logu',
          'ERROR %d, Exception %d, potkniecia niezerowe %d%s' % (ses.n_error, ses.n_exception, ses.n_potkniec, (
              '; pierwsze: ' + obetnij(ses.bledy[0][0], 60)) if ses.bledy else ''), ses.bledy[0][2] if ses.bledy else None)
    sur = sorted(tematy_surowe(ses))
    poza = [(nazwa, len(v)) for nazwa, v in ses.zle.items() if v]
    inne = [p for p in ses.obce if p.endswith('(inny rodzaj linii)')]
    K.gdy(not sur and not poza and not inne, 'parser: zaden temat w trybie surowym, zadna linia poza wzorcem',
          'wszystkie linie dzienne rozpoznane' if not (sur or poza or inne) else
          ('tryb surowy: %s; ' % ', '.join(sur) if sur else '') +
          ('linie poza wzorcem: %s; ' % ', '.join('%s x%d' % x for x in poza[:6]) if poza else '') +
          ('nieznany rodzaj linii: %s; ' % ', '.join(inne[:4]) if inne else '') + 'kontrolom tych tematow nie ufac - poprawic parser')
    al = [a for a in ses.alarmy if a[0] == 'ALARM']
    K.gdy(not al, 'alarmy calego logu: 0', '%d alarmow, %d uwag%s' % (len(al), len(ses.alarmy) - len(al), (
        '; pierwszy: ' + obetnij(al[0][2], 90)) if al else ''), al[0][1] if al else None)


def _g1(K, ses):
    """Grupa 1 TOWAR - kontrole wspolne spoza list ogniw 101-105."""
    f = ses.fakty
    mw = f.get('modele_wydobycia')
    if mw:
        K.gdy(mw[0] > 0, 'start gry: "MaterialLaw: ... wydobycie w N modelach" (N > 0)', 'wydobycie w %d modelach' % mw[0], mw[1])
    else:
        K.brak('start gry: "MaterialLaw: ... wydobycie w N modelach" (N > 0)', 'nie ma tej linii')
    co = 'start kampanii: StartStock ruda ok. 7000 szt. -> ok. 700 ladunkow, drewno ok. 24000 -> ok. 2400, 97 miast'
    linie = f.get('startstock') or []
    dane, miasta, nr = {}, None, None
    for w in linie:
        m = re.search(r'StartStock: (\w+) - (\d+) szt\. po .*? -> (\d+) ladunkow', w.tresc)
        if m:
            dane[m.group(1)] = (int(m.group(2)), int(m.group(3)))
            nr = nr or w.nr
        m = re.search(r'gotowe - .*? w (\d+) miastach; potkniecia (\d+)', w.tresc)
        if m:
            miasta = (int(m.group(1)), int(m.group(2)))
    if dane:
        r, d = dane.get('ruda'), dane.get('drewno')
        ok = bool(r) and 6000 <= r[0] <= 8000 and 600 <= r[1] <= 800 and bool(d) and 20000 <= d[0] <= 30000 \
            and 2000 <= d[1] <= 3000 and miasta is not None and miasta[0] == 97 and miasta[1] == 0
        K.gdy(ok, co, 'ruda %s; drewno %s; %s' % (('%d szt. -> %d ladunkow' % r) if r else 'brak linii',
                                                  ('%d szt. -> %d ladunkow' % d) if d else 'brak linii',
                                                  ('dane rynku w %d miastach, potkniecia %d' % miasta) if miasta
                                                  else 'brak linii "gotowe"'), nr)
    else:
        K.brak(co, 'nie ma linii "StartStock: ruda / drewno - N szt. ... -> M ladunkow"' + (
            '' if f.get('nowa_kampania') else ' (wczytany zapis)'))
    kar, stan = ses.ser('Karawany'), ses.ser('Karawany (stan)')
    od = Counter(d.get('first_naj') for _, d in kar if d.get('first_naj') and d.get('first_naj') != 'nic')
    towary = Counter()
    for _, d in kar:
        towary.update(x for x in (d.get('first_towary') or '').split(',') if x)
    co = '103: "wyjazdow, ktore zaczely zakupy od" - nie zawsze ten sam towar (kolejnosc wedle zysku w danej chwili)'
    if towary:
        K.gdy(len(towary) >= 2, co, 'towary, od ktorych zaczynano zakupy (w ilu dobach): %s; najczesciej pierwszy w dobie: %s'
              % (', '.join('%s %d' % x for x in towary.most_common(6)), ', '.join('%s %d dob' % x for x in od.most_common(3))),
              kar[-1][1].get('_nr'))
    else:
        K.brak(co, 'nie ma linii "Karawany:" z ta pozycja (albo zadnych zakupow)')
    po = _pierw_ost(stan, 'ruda_ponizej')
    if po:
        K.gdy(po[1] <= po[0], '103: "Karawany (stan): ruda: ponizej zapasu docelowego A -> B" maleje',
              '%s -> %s miast ponizej zapasu docelowego rudy (doby %d-%d); drewno %s -> %s'
              % (po[0], po[1], po[2], po[3], skr(stan[0][1].get('drewno_ponizej')), skr(stan[-1][1].get('drewno_ponizej'))),
              stan[-1][1].get('_nr'))
    else:
        K.brak('103: "Karawany (stan): ruda: ponizej zapasu docelowego A -> B" maleje', 'nie ma linii "Karawany (stan):"')
    ru, sk = ses.ser('Ruda'), ses.ser('Dowoz (skutki)')
    pr, pz = _pierw_ost(ru, 'bez_towaru'), _pierw_ost(sk, 'zatk_zamkowe_proc')
    if pr or pz:
        K.dodaj(OK, 'pomiar: wspolne skutki grupy - "Ruda: miast bez towaru" i "Dowoz (skutki): zatkane magazyny"',
                'miast bez rudy %s -> %s; zatkane magazyny wsi zamkowych %s%% -> %s%%, miejskich %s%% -> %s%%'
                % (skr(pr[0]) if pr else '-', skr(pr[1]) if pr else '-', skr(pz[0]) if pz else '-', skr(pz[1]) if pz else '-',
                   skr(sk[0][1].get('zatk_miejskie_proc')) if sk else '-', skr(sk[-1][1].get('zatk_miejskie_proc')) if sk else '-'),
                (ru or sk)[-1][1].get('_nr'))
    ps, bil = ses.ser('Pieniadz swiata'), ses.ser('Pieniadz swiata (bilans)')
    if ps:
        K.dodaj(OK, 'pomiar: stan "przed" dla grupy 2 - zloto swiata, reszta bilansu, rezerwa kas miast w "Karawany:"',
                'zloto swiata %s -> %s; reszta bilansu: mediana %s (%s%% ruchu); "sprzedaz wstrzymana rezerwa kasy miasta": mediana %s'
                % (skr(ps[0][1].get('razem')), skr(ps[-1][1].get('razem')), skr(mediana(_wart(bil, 'reszta')), True),
                   skr(mediana(_wart(bil, 'reszta_proc'))), skr(mediana(_wart(kar, 'wstrzymana_rezerwa')))), ps[-1][1].get('_nr'))
    else:
        K.brak('pomiar: stan "przed" dla grupy 2 (zloto swiata, reszta bilansu)', 'nie ma linii "Pieniadz swiata:"')


def _g2(K, ses):
    """Grupa 2 PIENIADZ - kontrole wspolne spoza list ogniw 106-107."""
    f = ses.fakty
    st = f.get('stale_skarbca')
    if st:
        K.gdy(st[0] == st[1] == 4, 'start gry: "podmienionych stalych 4, oczekiwane 4" (skarbiec bez zlota z niczego)',
              'podmienionych stalych %d, oczekiwane %d' % (st[0], st[1]), st[2])
    else:
        K.brak('start gry: "podmienionych stalych 4, oczekiwane 4"', 'nie ma linii "KingdomTreasury: ... podmienionych stalych"')
    pa = ses.ser('Paser')
    co = 'K2: paser z promieniem 200 i "poza promieniem" bliskie 0 (w Armoury.json nie ma OutlawFenceRadius = 20)'
    prom = _wart(pa, 'promien')
    if prom:
        poza, lad = sum(_wart(pa, 'poza_promieniem')), sum(_wart(pa, 'z_ladunkiem'))
        K.gdy(min(prom) == max(prom) == 200 and poza <= 0.05 * max(1, lad), co,
              'promien %s; poza promieniem %s z %s band z ladunkiem (%s%%) w %d dobach'
              % (skr(prom[-1]), skr(poza), skr(lad), skr(round(100.0 * poza / max(1, lad), 1)), len(pa)), pa[-1][1].get('_nr'))
    else:
        K.brak(co, 'nie ma linii "Paser:" z pozycja "na granicy promienia"')
    km = ses.ser('Przeplywy osad (kasy miast)')
    co = 'ksiega kas miast: osobne pozycje ogniwa 106 (paser, bandy) i 107 (zold garnizonow, zycie zolnierzy w miastach)'
    if km:
        s106 = [sum(_wart(km, k)) for k in ('paser_skup', 'zycie_band', 'paser_sprzet')]
        s107 = [sum(_wart(km, k)) for k in ('zold_garnizonow', 'zycie')]
        jest106 = any(_wart(km, k) for k in ('paser_skup', 'zycie_band', 'paser_sprzet'))
        jest107 = any(_wart(km, k) for k in ('zold_garnizonow', 'zycie'))
        K.gdy(jest106 and jest107, co,
              'razem w %d dobach: paser skup lupu %s, bandy i kryjowki zycie %s, paser sprzet %s | zold garnizonow %s, sakiewki '
              'ludzi - zycie %s | regulator: dosypal %s, skasowal %s'
              % (len(km), skr(s106[0], True), skr(s106[1], True), skr(s106[2], True), skr(s107[0], True), skr(s107[1], True),
                 skr(sum(_wart(km, 'dosypal'))), skr(sum(_wart(km, 'skasowal')))), km[-1][1].get('_nr'))
    else:
        K.brak(co, 'nie ma linii "Przeplywy osad (kasy miast):"')
    kar = ses.ser('Karawany')
    ws = _wart(kar, 'wstrzymana_rezerwa')
    if ws:
        K.dodaj(OK, 'pomiar: "Karawany: sprzedaz wstrzymana rezerwa kasy miasta" (skok wobec grupy 1 = paser 106, nie karawany 103)',
                'mediana %s na dobe; pierwsze 3 doby: %s' % (_zakres(ws), ', '.join(skr(x) for x in ws[:3])), kar[-1][1].get('_nr'))
    za = [(bi, d.get('za_zl')) for bi, d in pa if d.get('za_zl') is not None]
    if len(za) >= 4:
        K.dodaj(OK, 'pomiar: jednorazowa wyprzedaz zaleglego lupu w pierwszych 2-3 dobach (nie brac ich do sredniej)',
                'skup lupu w dobach 1-3: %s zl; od doby 4: mediana %s zl; kasy kryjowek %s -> %s'
                % (', '.join(skr(v) for _, v in za[:3]), skr(mediana([v for _, v in za[3:]])),
                   skr(pa[0][1].get('kasy_kryjowek')), skr(pa[-1][1].get('kasy_kryjowek'))), pa[-1][1].get('_nr'))
    elif pa:
        K.brak('pomiar: jednorazowa wyprzedaz zaleglego lupu w pierwszych 2-3 dobach', 'za malo dob z linia "Paser:" (%d)' % len(za))
    sa = ses.ser('Sakiewka ludzi')
    zw = _wart(sa, 'zold')
    if zw:
        K.gdy(mediana(zw) > 0, '107: "Sakiewka ludzi: ... zold wplacony do sakiewek" co dobe',
              'zold wplacony: mediana %s na dobe; w sakiewkach razem %s -> %s'
              % (_zakres(zw), skr(sa[0][1].get('razem')), skr(sa[-1][1].get('razem'))), sa[-1][1].get('_nr'))
    else:
        K.brak('107: "Sakiewka ludzi: ... zold wplacony do sakiewek" co dobe', 'linia "Sakiewka ludzi:" bez czesci o zoldzie')


def _g3(K, ses):
    """Grupa 3 LUDZIE - BetterEconomy (13 kluczy) i kontrole wspolne spoza list ogniw 108-109."""
    f = ses.fakty
    # --- BetterEconomy: plik ustawien (stan pliku TERAZ - nie z chwili sesji) i slady w logu
    co = 'BetterEconomy: 13 kluczy zamykajacych ujscia w pliku ustawien (stan pliku TERAZ, nie z chwili sesji)'
    sc = None
    try:
        sc = sciezka_bee(ses.sciezka)
        if sc and os.path.isfile(sc):
            with open(sc, 'rb') as fh:
                tekst, _ = dekoduj(fh.read())
            stan = stan_kluczy_bee(tekst)
            zle = [(k, v, cel) for k, v, cel, ok in stan if not ok]
            # sciezka pliku przed lista kluczy: wydruk kontroli ma trzy linie i ucina koniec, a nie poczatek
            K.gdy(not zle, co, ('%d z %d kluczy ma wartosc docelowa (plik %s)' % (len(stan) - len(zle), len(stan), _ogon(sc, 64))) + (
                ('; otwarte: ' + ', '.join('%s = %s (ma byc %s)' % (k, v if v is not None else 'brak', cel) for k, v, cel in zle[:2])
                 + (' ... i %d dalszych' % (len(zle) - 2) if len(zle) > 2 else '')
                 + ' - uruchomic tools\\bee\\zamknij-ujscia-bee.ps1 (-NaSucho pokaze wszystkie)') if zle else ''))
        else:
            K.brak(co, 'nie ma pliku %s' % _ogon(sc, 110))
    except Exception as e:
        K.brak(co, 'pliku nie da sie odczytac (%s: %s)' % (type(e).__name__, obetnij(str(e), 80)))
    pr = ses.ser('Ludzie (przyrost)')
    rz = _wart(pr, 'reszta')
    co = 'RESZTA hearth wsi: nie +65..135 (klucze BEE otwarte) i nie -27..-33 (latki 108 nie dzialaja)'
    if rz:
        med = mediana(rz)
        K.gdy(abs(med) <= PRZYROST_RESZTA_HEARTH, co, 'RESZTA: mediana %s hearth na dobe w %d dobach%s'
              % (_zakres(rz), len(rz), ' - inwestycje BetterEconomy dopisuja hearth z niczego (13 kluczy nie zamknietych)'
                 if med > PRZYROST_RESZTA_HEARTH else (' - rzad ubytku taborow z gry: latki ogniwa 108 nie dzialaja?'
                                                       if -40.0 <= med < -PRZYROST_RESZTA_HEARTH else (
                                                           ' - rabunki i zerowanie armii (do ogniwa 113 hearth w nicosc)'
                                                           if med < -40.0 else ''))), pr[-1][1].get('_nr'))
    else:
        K.brak(co, 'nie ma linii "Ludzie: przyrost naturalny" z rozliczeniem (od drugiej doby)')
    bil, ps = ses.ser('Pieniadz swiata (bilans)'), ses.ser('Pieniadz swiata')
    if bil:
        K.dodaj(OK, 'pomiar BEE: "GiveGoldAction w nicosc poza rozliczeniami rodow" (bez inwestycji panow we wsie i wplat do '
                    'skarbcow zamkow ma byc nizsze niz w grupie 2)',
                'w nicosc: mediana %s na dobe; reszta bilansu: mediana %s' % (_zakres(_wart(bil, 'w_nicosc')),
                                                                             skr(mediana(_wart(bil, 'reszta')), True)),
                bil[-1][1].get('_nr'))
    kz = [(d.get('karawany_zm'), d.get('karawany')) for _, d in ps if d.get('karawany_zm') is not None and d.get('karawany')]
    if kz:
        pc = [100.0 * a / b for a, b in kz]
        K.gdy(mediana(pc) > -2.0, 'pomiar BEE: kiesy karawan bez straty na szlaku (bylo do 7.6% dziennie)',
              'kiesy karawan %s -> %s; zmiana dobowa: mediana %s%% (od %s%% do %s%%)'
              % (skr(ps[0][1].get('karawany')), skr(ps[-1][1].get('karawany')), skr(round(mediana(pc), 2), True),
                 skr(round(min(pc), 2), True), skr(round(max(pc), 2), True)), ps[-1][1].get('_nr'))
    ru, dr = [(bi, d) for bi, d in ses.ser('Ruda') if bi > 0], [(bi, d) for bi, d in ses.ser('Drewno') if bi > 0]
    if ru or dr:
        K.dodaj(OK, 'pomiar BEE: "bez wyjasnienia" rudy i drewna (koniec darmowych dostaw BEE na targi)',
                'ruda: mediana %s ladunkow na dobe; drewno: mediana %s (ok. +2100 to znana dosypka RealisticBannerlord)'
                % (_zakres(_wart(ru, 'bez_wyjasnienia')), _zakres(_wart(dr, 'bez_wyjasnienia'))), (ru or dr)[-1][1].get('_nr'))
    # --- start kampanii: stan ludnosci miast
    ms = f.get('miasta_stan')
    co = 'start kampanii: "ludnosc miast zapisana jako stan (przy kalibracji) - 97 miast, 10.536 mln"'
    if ms is not None:
        m = re.search(r'razem (\d+) miast i ' + RX_F + ' mln ludzi', ms.tresc)
        K.gdy(bool(m) and int(m.group(1)) == 97 and 10.0 <= float(m.group(2).replace(',', '.')) <= 11.0
              and 'przy kalibracji' in ms.tresc, co,
              ('%s miast, %s mln ludzi%s' % (m.group(1), m.group(2), '' if 'przy kalibracji' in ms.tresc else
                                             ' - stan zalozony POZA kalibracja')) if m else wycinek(ms.tresc, 110), ms.nr)
    elif f.get('nowa_kampania'):
        K.dodaj(UW, co, 'nowa kampania, a tej linii nie ma (Town People Frozen wylaczone albo ogniwo 109 nie wgrane)')
    else:
        K.brak(co, 'wczytany zapis - stan miast zalozony wczesniej (klucz arm_people)')
    # --- bandy za Murem: jedyna rzecz z tej grupy do pokazania Jeffowi (kanon) - z pliku regionow, jesli jest
    co = 'bandy za Murem i na Murze (Nocna Straz, Wolni Ludzie) - gdyby ich nie bylo: pokazac Jeffowi (kanon)'
    sc, nagl, wiersze = _csv_sesji(ses)
    if nagl and 'kultura' in nagl and 'bandy_ludzie' in nagl and 'dzien' in nagl:
        mur = [w for w in wiersze if w.get('kultura') in KULTURY_ZA_MUREM and _lf(w.get('dzien')) is not None]
        dni = sorted(set(_lf(w['dzien']) for w in mur))
        if dni:
            def suma(dzien, pole):
                return sum((_lf(w.get(pole)) or 0.0) for w in mur if _lf(w['dzien']) == dzien)
            a, b = dni[0], dni[-1]
            K.gdy(suma(b, 'bandy_ludzie') > 0 or suma(b, 'wyrzutki') >= 20, co,
                  'regionow %d (kultury %s): bandy %s -> %s ludzi w %s -> %s partiach, pula wyrzutkow %s -> %s (dni %d-%d, plik CSV)'
                  % (len(set(w.get('region_id') for w in mur)), ' / '.join(sorted(set(w['kultura'] for w in mur))),
                     skr(int(suma(a, 'bandy_ludzie'))), skr(int(suma(b, 'bandy_ludzie'))), skr(int(suma(a, 'bandy_partie'))),
                     skr(int(suma(b, 'bandy_partie'))), skr(round(suma(a, 'wyrzutki'), 1)), skr(round(suma(b, 'wyrzutki'), 1)),
                     int(a), int(b)))
        else:
            K.brak(co, 'w pliku regionow nie ma regionow kultur %s' % ' / '.join(KULTURY_ZA_MUREM))
    else:
        K.brak(co, 'nie ma pliku regionow sesji (%s) - sprawdzic w grze albo w CSV z opcja --csv' % _ogon(sc, 70))


def _g4(K, ses):
    """Grupa 4 KASY - powiazania miedzy liniami ogniw 110, 111 i 112 (ta sama kwota w dwoch liniach ma byc ta sama)."""
    f = ses.fakty
    kz, km, ut = dict(ses.ser('Kasy zamkow')), dict(ses.ser('Kasy miast')), dict(ses.ser('Utarg wsi'))
    bil, prz = ses.ser('Pieniadz swiata (bilans)'), dict(ses.ser('Przeplywy osad'))
    oba = [(bi, b) for bi, b in bil if bi in kz and bi in km]
    co = 'bilans swiata przy K5 + K6: "zakupy" mieszkancow miast i zamkow 0, "regulator kas skasowal 0"'
    if oba:
        zak, ska = [b.get('zakupy') or 0 for _, b in oba], [b.get('skasowal') or 0 for _, b in oba]
        zost = [1 for bi, _ in oba if km[bi].get('zakupy_zostaja')]
        K.gdy(max(ska) == 0 and (max(zak) == 0 or zost), co,
              '"zakupy": najwiecej %s, skasowal: najwiecej %s w %d dobach%s' % (skr(max(zak)), skr(max(ska)), len(oba), (
                  '; zloto z "zakupow" mieszczan zostaje w kasach (ustawienie) w %d dobach' % len(zost)) if zost else ''),
              oba[-1][1].get('_nr'))
        roz = [(bi, b) for bi, b in oba if b.get('dosypal') is not None and km[bi].get('dosypal') is not None
               and abs(b['dosypal'] - km[bi]['dosypal']) > max(10, 0.01 * b['dosypal'])]
        K.gdy(not roz, 'bilans "regulator kas dosypal" = "Kasy miast: dosypal z niczego" (zamki dosypki nie maja)',
              'zgodne w %d z %d dob (ostatnio bilans %s wobec %s)' % (len(oba) - len(roz), len(oba), skr(oba[-1][1].get('dosypal')),
                                                                     skr(km[oba[-1][0]].get('dosypal'))), oba[-1][1].get('_nr'))
    else:
        K.brak(co, 'nie ma doby z liniami "Kasy zamkow:", "Kasy miast:" i "Pieniadz swiata (bilans):" naraz')
    co = 'dar startowy kas w ujsciach bilansu = "zdjeto" z linii CastlePurse / TownPurse'
    tz, tm = f.get('castle_trim'), f.get('town_trim')
    dz = [b.get('dar_zamkow') for _, b in bil if b.get('dar_zamkow') is not None]
    dm = [b.get('dar_miast') for _, b in bil if b.get('dar_miast') is not None]
    if dz or dm:
        ok = (not dz or (tz is not None and sum(dz) == tz['zdjeto'])) and (not dm or (tm is not None and sum(dm) == tm['zdjeto']))
        K.gdy(ok, co, 'bilans: zamki %s, miasta %s; linie przyciecia: zamki %s, miasta %s'
              % (skr(sum(dz)) if dz else '-', skr(sum(dm)) if dm else '-', skr(tz['zdjeto']) if tz else '-',
                 skr(tm['zdjeto']) if tm else '-'))
    elif tz or tm:
        K.dodaj(OK, 'pomiar: ' + co, 'przyciecie wypadlo w pierwszej dobie ksiegi (bilansu jeszcze nie ma): zamki %s, miasta %s; '
                'razem z kas zniklo jednorazowo %s' % (skr(tz['zdjeto']) if tz else '-', skr(tm['zdjeto']) if tm else '-',
                                                      skr((tz['zdjeto'] if tz else 0) + (tm['zdjeto'] if tm else 0))),
                (tz or tm)['nr'])
    else:
        K.brak(co, 'nie ma linii o przycieciu daru (wczytany zapis po przycieciu albo ustawienia wylaczone)')
    co = '"Przeplywy osad: dopisane wsiom przez K7" = "Utarg wsi: dopisane kiesom wsi / licznikom panow"'
    wsp = [(ut[bi], prz[bi]) for bi in sorted(ut) if bi in prz and ut[bi].get('powroty_stan') == 'czynne']
    if wsp:
        roz = [1 for u, p in wsp if (u.get('dopisane_kiesom') or 0) != (p.get('k7_do_kies') or 0)
               or (u.get('dopisane_panom') or 0) != (p.get('k7_na_liczniki') or 0)]
        K.gdy(not roz, co, 'zgodne w %d z %d dob (ostatnio do kies %s wobec %s, na liczniki panow %s wobec %s)'
              % (len(wsp) - len(roz), len(wsp), skr(wsp[-1][1].get('k7_do_kies')), skr(wsp[-1][0].get('dopisane_kiesom')),
                 skr(wsp[-1][1].get('k7_na_liczniki')), skr(wsp[-1][0].get('dopisane_panom'))), wsp[-1][1].get('_nr'))
    else:
        K.brak(co, 'nie ma doby z liniami "Utarg wsi:" (czynne powroty) i "Przeplywy osad:" naraz')
    bild = dict(bil)
    co = 'bilans "z ceny zywnosci ... gra skasowala" = "Utarg wsi: zywnosc ... gra skasowala"'
    wsp = [(ut[bi], bild[bi]) for bi in sorted(ut) if bi in bild and bild[bi].get('zyw_skasowala') is not None]
    if wsp:
        roz = [1 for u, b in wsp if (u.get('zyw_skasowala') or 0) != b['zyw_skasowala']]
        K.gdy(not roz, co, 'zgodne w %d z %d dob (ostatnio %s wobec %s); sakwy taborow: w bilansie bylo w nich razem %s'
              % (len(wsp) - len(roz), len(wsp), skr(wsp[-1][1].get('zyw_skasowala')), skr(wsp[-1][0].get('zyw_skasowala')),
                 skr(sum(b.get('sakwy_bylo') or 0 for _, b in wsp))), wsp[-1][1].get('_nr'))
    else:
        K.brak(co, 'nie ma doby z liniami "Utarg wsi:" i bilansem w ukladzie ogniwa 112 naraz')
    ps = ses.ser('Pieniadz swiata')
    if ps and bil:
        K.dodaj(OK, 'pomiar: zloto swiata po K5-K7 (z niczego zostaje dosypka miast ponizej zapasu kupcow)',
                'zloto swiata %s -> %s (zmiana dobowa: mediana %s); zrodla z niczego: mediana %s, ujscia: mediana %s, reszta: '
                'mediana %s (%s%% ruchu)' % (skr(ps[0][1].get('razem')), skr(ps[-1][1].get('razem')),
                                            skr(mediana(_wart(ps, 'razem_zm')), True), skr(mediana(_wart(bil, 'zrodla'))),
                                            skr(mediana(_wart(bil, 'ujscia'))), skr(mediana(_wart(bil, 'reszta')), True),
                                            skr(mediana(_wart(bil, 'reszta_proc')))), bil[-1][1].get('_nr'))
    kw = ses.ser('Przeplywy osad (kiesy wsi)')
    pm, pzk, pw = _pierw_ost(ses.ser('Kasy miast'), 'stan'), _pierw_ost(ses.ser('Kasy zamkow'), 'stan'), _pierw_ost(kw, 'stan')
    if pm or pzk or pw:
        K.dodaj(OK, 'pomiar: masa pieniadza w kasach (miasta ok. 10.6 mln, zamki ok. 2.9 mln + nadwyzka, kiesy wsi do ok. 2x)',
                'kasy miast %s -> %s; kasy zamkow %s -> %s; kiesy wsi %s -> %s'
                % (skr(pm[0]) if pm else '-', skr(pm[1]) if pm else '-', skr(pzk[0]) if pzk else '-', skr(pzk[1]) if pzk else '-',
                   skr(pw[0]) if pw else '-', skr(pw[1]) if pw else '-'))


def _g5(K, ses):
    """Grupa 5 SPUSTOSZENIE - powiazania linii ogniwa 113 z ksiega ludzi, plikiem regionow i reszta lancucha."""
    f = ses.fakty
    w = f.get('scorched')
    co = 'start gry: "ScorchedEarth: ... przy czynnym spustoszeniu (Devastation) marsz pustoszy ulamek okregu w ludziach"'
    if w is not None:
        K.gdy('przy czynnym spustoszeniu (Devastation)' in w.tresc, co, wycinek(w.tresc, 115), w.nr)
    else:
        K.brak(co, 'nie ma linii "ScorchedEarth: foraging ..."')
    lu = [(bi, d) for bi, d in ses.ser('Ludzie') if d.get('spustoszenie') == 'czynne']
    co = 'ksiega hearth: "hearth za ludzi dzis ... spustoszenie -X za N zabitych i uchodzcow" = zabici + uchodzcy doby'
    pary = [(d.get('hz_spust_ludzi'), (d.get('sp_zabici') or 0) + (d.get('sp_uchodzcy') or 0)) for _, d in lu
            if d.get('hz_spust_ludzi') is not None]
    if pary:
        roz = [1 for a, b in pary if abs(a - b) > max(1.0, 0.01 * b)]
        K.gdy(not roz, co, 'zgodne w %d z %d dob z ruchem (razem %s wobec %s ludzi)'
              % (len(pary) - len(roz), len(pary), skr(round(sum(a for a, _ in pary), 1)), skr(round(sum(b for _, b in pary), 1))),
              lu[-1][1].get('_nr'))
    else:
        K.brak(co, 'dopisek jest tylko w dobach, w ktorych kogos zdjeto z wsi albo ktos wrocil - takiej doby nie ma')
    co = 'ksiega hearth: "powrot uchodzcow +X za N ludzi" = "wrocilo do domu N"'
    pary = [(d.get('hz_dom_ludzi'), d.get('sp_wrocilo') or 0) for _, d in lu if d.get('hz_dom_ludzi') is not None]
    if pary:
        roz = [1 for a, b in pary if abs(a - b) > max(1.0, 0.01 * b)]
        K.gdy(not roz, co, 'zgodne w %d z %d dob (razem %s wobec %s ludzi)'
              % (len(pary) - len(roz), len(pary), skr(round(sum(a for a, _ in pary), 1)), skr(round(sum(b for _, b in pary), 1))),
              lu[-1][1].get('_nr'))
    else:
        K.brak(co, 'nie ma doby z dopiskiem o powrotach w odcinku "hearth za ludzi dzis"')
    sp = dict(ses.ser('Ludzie (spustoszenie)'))
    co = '"Ludzie (spustoszenie): uchodzcy poza domem" = "Ludzie: uchodzcy w drodze (poza domem)"'
    pary = [(sp[bi].get('uchodzcy'), d.get('sp_w_drodze')) for bi, d in lu if bi in sp and d.get('sp_w_drodze') is not None]
    if pary:
        roz = [1 for a, b in pary if a is None or abs(a - b) > max(2, 0.01 * b)]
        K.gdy(not roz, co, 'zgodne w %d z %d dob (ostatnio %s wobec %s)' % (len(pary) - len(roz), len(pary), skr(pary[-1][0]),
                                                                          skr(pary[-1][1])), lu[-1][1].get('_nr'))
    else:
        K.brak(co, 'nie ma doby z obiema liniami (linia "Ludzie (spustoszenie):" jest tylko w dobach z ruchem)')
    co = 'plik regionow: 3 nowe kolumny (uchodzcy, spustoszenie_proc, zabici_spustoszenie)'
    sc, nagl, wiersze = _csv_sesji(ses)
    if nagl:
        nowe = [k for k in ('uchodzcy', 'spustoszenie_proc', 'zabici_spustoszenie') if k in nagl]
        if len(nowe) == 3:
            dni = sorted(set(_lf(x.get('dzien')) for x in wiersze if _lf(x.get('dzien')) is not None))
            ost = [x for x in wiersze if dni and _lf(x.get('dzien')) == dni[-1]]
            naj = max(ost, key=lambda x: _lf(x.get('spustoszenie_proc')) or 0.0) if ost else None
            K.dodaj(OK, co, 'sa; ostatnia doba %s: uchodzcy razem %s w %d regionach, zabici przy spustoszeniu %s; najbardziej '
                    'spustoszony: %s %s%%' % (skr(int(dni[-1])) if dni else '-',
                                              skr(int(sum((_lf(x.get('uchodzcy')) or 0) for x in ost))),
                                              len([1 for x in ost if (_lf(x.get('uchodzcy')) or 0) > 0]),
                                              skr(int(sum((_lf(x.get('zabici_spustoszenie')) or 0) for x in ost))),
                                              obetnij(naj.get('region', '?'), 30) if naj else '-',
                                              (naj.get('spustoszenie_proc') or '0') if naj else '-'))
        else:
            K.dodaj(UW, co, 'plik ma %d kolumn, nowych %d z 3 (%s) - plik sprzed ogniwa 113?' % (len(nagl), len(nowe),
                                                                                              ', '.join(nowe) or 'zadnej'))
    else:
        K.brak(co, 'nie ma pliku regionow sesji (%s)' % _ogon(sc, 90))
    ru = [(bi, d) for bi, d in ses.ser('Ruda') if bi > 0]
    spl = ses.ser('Ludzie (spustoszenie)')
    if spl:
        K.dodaj(OK, 'pomiar: plon od rak (6a) - najnizszy mnoznik plonu wsi i wydobycie rudy',
                'najnizszy mnoznik plonu: x%s -> x%s; "Ruda: wsie dopisaly": mediana %s ladunkow na dobe przy modelu %s'
                % (skr(spl[0][1].get('min_plon')), skr(spl[-1][1].get('min_plon')), skr(mediana(_wart(ru, 'dopisaly'))),
                   skr(mediana(_wart(ru, 'model')))), spl[-1][1].get('_nr'))


def _g2b(K, ses):
    """Grupa 2b TOWARY 2 - glowne liczby testu i powiazania miedzy liniami ogniw 115-119 (szczegoly - kontrole kazdego ogniwa)."""
    f = ses.fakty
    n = len(ses.pelne()) if ses.bloki else 0
    ru = ses.ser('Ruda')
    po = _pierw_ost(ru, 'bez_towaru')
    co = ('GLOWNA LICZBA: "Ruda: miast bez towaru" w ostatniej dobie - dotad 74 -> 69, oczekiwane ok. 25-30 po 20 dobach (<= %d OK, '
          'ponad %d ALARM)' % (RUDA_BEZ_TOWARU_CEL_2B, RUDA_BEZ_TOWARU_ALARM_2B))
    if po:
        spadek = po[0] - po[1]
        if n >= TOWARY2_MIN_DOB:
            if po[1] <= RUDA_BEZ_TOWARU_CEL_2B:
                ocena = 'OK'
            elif po[1] > RUDA_BEZ_TOWARU_ALARM_2B:
                ocena = 'ALARM - ponad %d' % RUDA_BEZ_TOWARU_ALARM_2B
            else:
                ocena = 'za malo (miedzy %d a %d)' % (RUDA_BEZ_TOWARU_CEL_2B, RUDA_BEZ_TOWARU_ALARM_2B)
            ok = po[1] <= RUDA_BEZ_TOWARU_CEL_2B
        else:
            ocena = 'za malo dob do oceny (%d < %d) - tylko kierunek: %s' % (n, TOWARY2_MIN_DOB, 'spada' if spadek > 0 else 'NIE spada')
            ok = spadek > 0
        K.gdy(ok, co, '%s -> %s miast bez rudy z %s (doby %d-%d, %d pelnych dob; ubylo %s) - %s'
              % (po[0], po[1], skr(_miast_swiata(ses)), po[2], po[3], n, skr(spadek), ocena), ru[-1][1].get('_nr'))
    else:
        K.brak(co, 'nie ma linii "Ruda:" z pozycja "miast bez towaru"')
    dr = ses.ser('Drewno')
    po = _pierw_ost(dr, 'bez_towaru')
    co = '"Drewno: miast bez towaru" w ostatniej dobie - dotad 41 -> 40, oczekiwane ok. 10-15 (<= %d OK)' % DREWNO_BEZ_TOWARU_CEL_2B
    if po:
        K.gdy(po[1] <= DREWNO_BEZ_TOWARU_CEL_2B if n >= TOWARY2_MIN_DOB else po[1] <= po[0], co, '%s -> %s miast bez drewna (doby %d-%d)%s'
              % (po[0], po[1], po[2], po[3], '' if n >= TOWARY2_MIN_DOB else ' - za malo dob do oceny, tylko kierunek'), dr[-1][1].get('_nr'))
    else:
        K.brak(co, 'nie ma linii "Drewno:"')
    # dostawy rudy do miast bez rudy: wozy wsi (119) i karawany (117)
    wz, kr = ses.ser('Dowoz (wozy)'), ses.ser('Karawany (kierunek)')
    k_wozy, k_kar = sum(_wart(wz, 'ruda_do_pustych')), sum(_wart(kr, 'ruda_wj_brak'))
    co = ('dostawy rudy do miast bez rudy: wozy wsi (119: "z ruda ..., w tym do miasta bez rudy K") + karawany (117: "wjazdy, w tym do '
          'miasta z brakiem")')
    if wz or kr:
        K.gdy(k_wozy + k_kar > 0, co,
              'wozy: %s kursow do miast bez rudy (z %s z ruda); karawany: %s wjazdow z ruda do miast z brakiem (z %s); '
              'razem %s w %d dobach' % (skr(k_wozy), skr(sum(_wart(wz, 'z_ruda'))), skr(k_kar), skr(sum(_wart(kr, 'ruda_wj'))),
                                        skr(k_wozy + k_kar), n), (wz or kr)[-1][1].get('_nr'))
    else:
        K.brak(co, 'nie ma linii "Dowoz (wozy):" ani "Karawany (kierunek):"')
    # 116 x 118: wspolczynnik chleba w przykladzie warsztatow miesci sie w rozpietosci indeksu chleba z "Ceny surowcow" tej samej doby
    wt, ce = dict(ses.ser('Warsztaty towarowe')), dict(ses.ser('Ceny surowcow'))
    pary = [(bi, wt[bi].get('piekarnia_wyrob_x'), ce[bi].get('def_bread_min'), ce[bi].get('def_bread_max')) for bi in sorted(wt)
            if bi in ce and wt[bi].get('piekarnia_wyrob') == 'bread']
    pary = [p for p in pary if None not in p[1:]]
    co = '116 + 118: "Warsztaty towarowe: ... wyrob bread xN" (przyklad miasta) miesci sie w "Ceny surowcow: bread min/max" tej samej doby'
    if pary:
        zle = [p for p in pary if not (p[2] - 0.01 <= p[1] <= p[3] + 0.01)]
        bi, x, a, b = pary[-1]
        K.gdy(not zle, co, 'zgodne w %d z %d dob (ostatnio chleb x%s w przykladzie, indeks chleba w miastach %s-%s)%s'
              % (len(pary) - len(zle), len(pary), skr(x), skr(a), skr(b), ('; pierwsza niezgodna doba %d' % (zle[0][0] + 1)) if zle else ''),
              wt[bi].get('_nr'))
    else:
        K.brak(co, 'nie ma doby z piekarnia (wyrob bread) w przykladzie i czescia o chlebie w "Ceny surowcow:" naraz')
    # 116 x ksiega kas miast: pozycja "warsztaty towarowe" wobec "do kas miast" z linii warsztatow (pomiar - inne okna czasu i migawki)
    km = ses.ser('Przeplywy osad (kasy miast)')
    a = sum(_wart(km, 'warsztaty_tow'))
    b = sum((d.get('place_kapital') or 0) + (d.get('utrzymanie') or 0) - (d.get('utrzymanie_kiesa') or 0) for d in wt.values())
    if a or b:
        K.dodaj(OK, 'pomiar 116: "Przeplywy osad (kasy miast): warsztaty towarowe - place i utrzymanie" wobec "Warsztaty towarowe: do '
                    'kas miast" '
                    '(z kapitalu; ksiega liczy miedzy tickami miast, w ticku dobowym Armoury lapia je migawki)',
                'ksiega kas miast %s, linia warsztatow %s (%s%%)' % (skr(a), skr(b), skr(round(100.0 * a / b, 1)) if b else '-'),
                km[-1][1].get('_nr') if km else None)
    # linie startowe i jednorazowe ogniw 115-119 - bez BRAK / NIECZYNNE / wylaczone
    klucze = [('rawprice', '115 start'), ('rawprice_seed', '115 nowa kampania'), ('workshoptrade', '116 start'),
              ('workshoptrade_latka', '116 latka w kampanii'), ('workshoptrade_seed', '116 nowa kampania'),
              ('workshoptrade_brak', '116 BRAK'), ('caravanbulk_117', '117 start'), ('hp_definicja', '118 start'),
              ('hp_popyt', '118 popyt'), ('hp_przelicznik', '118 przelicznik'), ('hp_uwaga0', '118 UWAGA wartosc 0'),
              ('marketcarts', '119 start')]
    # linia "popyt miast przeliczony" jest tez bez ogniwa 118 (test 14:08) - liczy sie tylko przy 118
    jest = [(op, f[k]) for k, op in klucze if f.get(k) is not None and (k != 'hp_popyt' or ma_ogniwo(ses, '118'))]
    # "drugi pobor wydatku z kiesy gracza wylaczony" to dobry stan latki 116 (nie wylacznik)
    zle = [(op, w) for op, w in jest if re.search(r'\bBRAK\b|NIECZYNN|WYLACZON|wylaczon|UWAGA - ',
                                                  w.tresc.replace('drugi pobor wydatku z kiesy gracza wylaczony', ''))]
    co = 'linie startowe i jednorazowe ogniw 115-119 bez "BRAK", "NIECZYNNA", "WYLACZONE / wylaczone", "UWAGA - ... wartosc 0"'
    if jest:
        K.gdy(not zle, co, ('%d linii, wszystkie czyste: %s' % (len(jest), ', '.join(op for op, _ in jest))) if not zle else
              '; '.join('%s: %s' % (op, wycinek(w.tresc, 70)) for op, w in zle[:2]), zle[0][1].nr if zle else None)
    else:
        K.brak(co, 'w logu nie ma zadnej linii startowej ogniw 115-119')
    # ERROR i potkniecia w modulach grupy (wszystkie miejsca, ktore paczki opisuja jako sygnal bledu)
    bl = _bledy_z(ses, 'RawPrice', 'WorkshopTrade', 'CaravanBulk', 'HistoricalPrices.Define', 'HistoricalPrices.ApplyAll (BKItems',
                  'MarketCarts', 'Dowoz (wozy)', 'Warsztaty towarowe')
    s = f.get('rawprice_seed')
    m = re.search(r'; potkniecia (\d+)', s.tresc) if s is not None else None
    pot = sum(_wart(ses.ser('Warsztaty towarowe'), 'potkniecia')) + sum(_wart(wz, 'potkn')) + (int(m.group(1)) if m else 0)
    K.gdy(not bl and pot == 0,
          'potkniecia 0 i bez ERROR w modulach 115-119 (RawPrice, WorkshopTrade, CaravanBulk, HistoricalPrices z definicji, '
          'MarketCarts)', 'potkniecia razem %d; bledy: %s' % (pot, _opis_bledow(bl)), bl[0][2] if bl else None)
    ps, bil = ses.ser('Pieniadz swiata'), ses.ser('Pieniadz swiata (bilans)')
    if ps:
        K.dodaj(OK, 'pomiar: stan "przed" dla grupy 3 - zloto swiata, reszta bilansu, "zakupy" mieszkancow (dotad 462.4k), kapital warsztatow',
                'zloto swiata %s -> %s; reszta bilansu: mediana %s (%s%% ruchu); zakupy mieszkancow: mediana %s; kapital warsztatow %s'
                % (skr(ps[0][1].get('razem')), skr(ps[-1][1].get('razem')), skr(mediana(_wart(bil, 'reszta')), True),
                   skr(mediana(_wart(bil, 'reszta_proc'))), skr(mediana(_wart(bil, 'zakupy'))), _pierw_ost_txt(ps, 'warsztaty')),
                ps[-1][1].get('_nr'))


def tabela_dzienna_2b(ses, szer):
    """Grupa 2b: tabela dzien po dniu glownych liczb testu (wiersz = doba; kolumny lamane do szerokosci, pierwsze 3 powtarzane)."""
    zr = dict((n, dict(ses.ser(n))) for n in ('Ruda', 'Drewno', 'Karawany (stan)', 'Ceny surowcow', 'Karawany', 'Dowoz (wozy)',
                                               'Warsztaty towarowe'))

    def v(nazwa, bi, klucz):
        return (zr[nazwa].get(bi) or {}).get(klucz)

    def para(a, b):
        return '-' if a is None and b is None else '%s/%s' % (skr(a), skr(b))
    kol = ['doba', 'dzien', 'ruda bez', 'drewno bez', 'kar.ruda bez', 'ruda idx med/max', 'ruda 1.szt d', 'ruda zbyt d', 'kar.ruda kup/sprz',
           'wozy', 'do innego %', 'wozy ruda/puste', 'nadplata %', 'niezg.', 'ms', 'piekarnia', 'chleb x', 'chleb idx', 'potkn.']
    wiersze = []
    for bi in ses.wyb:
        if not any(bi in zr[n] for n in zr):
            continue
        pt = [x for x in (v('Warsztaty towarowe', bi, 'potkniecia'), v('Dowoz (wozy)', bi, 'potkn')) if x is not None]
        wiersze.append([bi + 1, ses.bloki[bi].D, skr(v('Ruda', bi, 'bez_towaru')), skr(v('Drewno', bi, 'bez_towaru')),
                        skr(v('Karawany (stan)', bi, 'ruda_bez')),
                        para(v('Ceny surowcow', bi, 'ruda_idx_med'), v('Ceny surowcow', bi, 'ruda_idx_max')),
                        skr(v('Ceny surowcow', bi, 'ruda_placa')), skr(v('Karawany (stan)', bi, 'ruda_cena')),
                        para(v('Karawany', bi, 'kup_ruda'), v('Karawany', bi, 'sprz_ruda')), skr(v('Dowoz (wozy)', bi, 'wozow')),
                        skr(v('Dowoz (wozy)', bi, 'do_innego_proc')),
                        para(v('Dowoz (wozy)', bi, 'z_ruda'), v('Dowoz (wozy)', bi, 'ruda_do_pustych')),
                        skr(v('Dowoz (wozy)', bi, 'nadplata_proc')), skr(v('Dowoz (wozy)', bi, 'niezgodne')),
                        skr(v('Dowoz (wozy)', bi, 'ms')), skr(v('Warsztaty towarowe', bi, 'piekarnia_cena')),
                        skr(v('Warsztaty towarowe', bi, 'piekarnia_wyrob_x')), skr(v('Ceny surowcow', bi, 'def_bread_med')),
                        skr(sum(pt)) if pt else '-'])
    L = ['--- GRUPA 2b: TABELA DZIEN PO DNIU (glowne liczby; "-" = brak linii; ruda bez = "Ruda: miast bez towaru", kar. = "Karawany", '
         'idx = indeks ceny, 1.szt = "za pierwsza sztuke placa", piekarnia = cena w przykladzie miasta) ---']
    if not wiersze:
        return [obetnij(L[0], szer), '  brak dob z liniami grupy']
    return [obetnij(L[0], szer)] + tabela_pelna(kol, wiersze, szer)


FUNKCJE_GRUP = OrderedDict([('1', _g1), ('2', _g2), ('2b', _g2b), ('3', _g3), ('4', _g4), ('5', _g5)])
# grupy z tabela dzien po dniu glownych liczb (drukowana po kontrolach wspolnych)
TABELE_GRUP = {'2b': tabela_dzienna_2b}


def grupa_z_nazwy(ses, nazwa):
    """Numer grupy testowej dla opcji --grupa: 1..5 i 2b, nazwa (towar, pieniadz, towary2, ludzie, kasy, spustoszenie), "w-grze"."""
    q = (nazwa or '').strip().lower()
    if q in GRUPY_GRY:
        return q
    if q in NAZWY_GRUP_GRY:
        return NAZWY_GRUP_GRY[q]
    if q in ('', 'w-grze', 'wgrze', 'gra', 'auto'):
        return grupa_ogniwa(ses.ogniwo_w_grze) if ses.ogniwo_w_grze else None
    m = re.match(r'^(?:grupa[ -]?)?(2b|[1-5])$', q)
    return m.group(1) if m else None


def kontrole_grupy(ses, g):
    """(warunki testu, kontrole wspolne) grupy g - dwie listy [(status, co, wynik, numer linii)]. Nigdy wyjatek."""
    wyn = []
    for funkcja, co in ((_warunki_grupy, 'warunki testu'), (FUNKCJE_GRUP.get(g), 'kontrole wspolne')):
        K = Kontrole()
        try:
            if funkcja is _warunki_grupy:
                funkcja(K, ses, g)
            elif funkcja is not None:
                funkcja(K, ses)
        except Exception as e:
            K.dodaj(BD, '%s grupy %s przerwane' % (co, g), 'blad wewnetrzny (%s: %s) - reszta wyjscia bez zmian'
                    % (type(e).__name__, e))
        wyn.append(K.lista)
    return wyn[0], wyn[1]


def _linie_listy(lista, szer):
    L = []
    for status, co, wynik, nr_linii in lista:
        gdzie = ('linia %d' % nr_linii) if nr_linii else 'caly log'
        L.extend(zawin('  [%s] %s: %s -> %s' % (status, gdzie, co, wynik), szer, 3))
    return L


def drukuj_grupe(ses, nazwa, szer=SZEROKOSC):
    """Wyjscie opcji --grupa: warunki testu, kontrole wspolne, wszystkie kontrole ogniw grupy, alarmy calego logu."""
    g = grupa_z_nazwy(ses, nazwa)
    L = []
    if g is None:
        if (nazwa or '').strip().lower() in ('', 'w-grze', 'wgrze', 'gra', 'auto'):
            L.append('--grupa "%s": w tym logu nie widac zadnego ogniwa lancucha - nie ma z czego poznac grupy.' % (nazwa or ''))
        else:
            L.append('--grupa "%s": nie ma takiej grupy.' % obetnij(str(nazwa), 60))
        for k, opis in GRUPY_GRY.items():
            L.append(obetnij('  %s = %s: %s%s' % (k, opis['nazwa'].lower(), ('BetterEconomy (13 kluczy) + ' if opis['bee'] else '')
                                                  + ' + '.join(opis['ogniwa']), ' | DLL ' + opis['dll']), szer))
        L.append('  takze: --grupa w-grze (grupa najwyzszego ogniwa wykrytego w logu)')
        return L
    opis = GRUPY_GRY[g]
    sklad = ('BetterEconomy (13 kluczy) + ' if opis['bee'] else '') + ' + '.join(opis['ogniwa'])
    L.append(obetnij('=== GRUPA %s %s: %s | DLL galezi %s | OGNIWO W GRZE (wg linii logu): %s | doby: %d ==='
                     % (g, opis['nazwa'], sklad, opis['dll'], ses.ogniwo_w_grze or 'sprzed wpisu 100',
                        len(ses.pelne()) if ses.bloki else 0), szer))
    L.extend(zawin('  test: ' + opis['test'], szer, 2, wciecie='        '))
    L.extend(zawin('  przypisanie linii do ogniw: ' + opis['przypisanie'], szer, 2, wciecie='        '))
    warunki, wspolne = kontrole_grupy(ses, g)
    razem = Counter()
    for tytul, lista in (('WARUNKI TESTU', warunki), ('KONTROLE WSPOLNE GRUPY (przeglad kolizji lancucha, powiazania ogniw)',
                                                      wspolne)):
        licz = Counter(k[0] for k in lista)
        razem.update(licz)
        L.append(obetnij('--- GRUPA %s: %s: OK %d, UWAGA %d, BRAK DANYCH %d ---' % (g, tytul, licz[OK], licz[UW], licz[BD]), szer))
        L.extend(_linie_listy(lista, szer))
    if g in TABELE_GRUP:
        try:
            L.extend(TABELE_GRUP[g](ses, szer))
        except Exception as e:
            L.append('--- GRUPA %s: tabela dzien po dniu przerwana (blad wewnetrzny %s: %s) ---' % (g, type(e).__name__, e))
    for nr in opis['ogniwa']:
        L.extend(linie_kontroli(ses, nr, szer, pelne=True))
        razem.update(k[0] for k in kontrole_ogniwa(ses, nr))
    al = [a for a in ses.alarmy if a[0] == 'ALARM']
    L.append('--- ALARMY CALEGO LOGU: %d, uwagi: %d ---' % (len(al), len(ses.alarmy) - len(al)))
    for poziom, nr, tekst in ses.alarmy[:MAKS_ALARMOW]:
        L.extend(zawin('  [%s] %s: %s' % (poziom, ('linia %d' % nr) if nr else 'caly log', tekst), szer, 2))
    if len(ses.alarmy) > MAKS_ALARMOW:
        L.append('  ... i %d dalszych (pelny skrot z --pelny)' % (len(ses.alarmy) - MAKS_ALARMOW))
    if not ses.alarmy:
        L.append('  brak')
    L.append(obetnij('=== RAZEM GRUPA %s: OK %d, UWAGA %d, BRAK DANYCH %d | w calym logu: ERROR %d, Exception %d, potkniecia %d, '
                     'alarmy %d, uwagi %d (pelny skrot: bez --grupa) ===' % (g, razem[OK], razem[UW], razem[BD], ses.n_error,
                                                                            ses.n_exception, ses.n_potkniec, len(al),
                                                                            len(ses.alarmy) - len(al)), szer))
    return L


def linia_grupy(ses, szer):
    """Jedna linia skrotu: do ktorej grupy testowej nalezy ogniwo w grze i jak wywolac komplet jej kontroli."""
    g = grupa_ogniwa(ses.ogniwo_w_grze) if ses.ogniwo_w_grze else None
    if g is None:
        return []
    opis = GRUPY_GRY[g]
    return [obetnij('  grupa testowa w grze: %s %s (%s%s) | komplet kontroli grupy, warunki testu i powiazania ogniw: --grupa %s'
                    % (g, opis['nazwa'], 'BetterEconomy + ' if opis['bee'] else '', ' + '.join(opis['ogniwa']), g), szer)]


# =====================================================================================================================
# WYDRUK
# =====================================================================================================================
def probki(indeksy, ile):
    """Pierwszy, rownomiernie rozlozone, ostatni."""
    n = len(indeksy)
    if n <= ile:
        return list(indeksy)
    if ile <= 1:
        return [indeksy[-1]]
    wyn = []
    for i in range(ile):
        k = int(round(i * (n - 1) / float(ile - 1)))
        if indeksy[k] not in wyn:
            wyn.append(indeksy[k])
    return wyn


def linie_naglowka(ses, szer):
    L = []
    nazwa = os.path.basename(ses.sciezka)
    L.append('=== SPRAWDZ LOGI: %s ===' % nazwa)
    ost = ses.wpisy[-1].czas if ses.wpisy else '-'
    pelna = os.path.normpath(ses.sciezka)
    L.append('plik: %s' % (pelna if len(pelna) <= szer - 6 else '...' + pelna[-(szer - 9):]))
    sesja = (ses.naglowek or '').strip('= ').strip()
    L.append('rozmiar %d B, %d linii, kodowanie %s | sesja: %s, ostatni wpis %s'
             % (ses.rozmiar, ses.linii, ses.kodowanie, sesja or 'brak naglowka', ost))
    f = ses.fakty
    if ses.bloki:
        b0, b1 = ses.bloki[0], ses.bloki[-1]
        zakres = 'doby 1-%d = dni gry %d-%d (%d dob)' % (len(ses.bloki), b0.D, b1.D, len(ses.bloki))
        if b1.niepelny:
            zakres += ', ostatnia NIEPELNA (%d tematow) - poza szeregiem' % b1.tematow
        if len(ses.wyb) != len(ses.bloki):
            zakres += ' | --dni: wybrano %d dob (%s)' % (len(ses.wyb), ('%d-%d' % (ses.wyb[0] + 1, ses.wyb[-1] + 1))
                                                       if ses.wyb else 'zadna')
    else:
        zakres = 'BRAK DOB (zadnej linii dziennej - sam start gry albo menu)'
    kamp = 'kampanie/wczytania w sesji: %d' % len(ses.segmenty)
    if len(ses.segmenty) > 1:
        kamp += ' (' + ', '.join('nr %d od linii %d: %d dob' % (i + 1, s[0], len(s[2]))
                                 for i, s in enumerate(ses.segmenty)) + '; analizowana nr %d)' % (ses.wybrany + 1)
    L.append('%s | %s' % (kamp, zakres))
    czesci = []
    czesci.append('rok %s dni' % f['rok_dni'] if 'rok_dni' in f else 'rok: brak linii Kalendarz')
    czesci.append('nowa kampania' if f.get('nowa_kampania') else ('wczytany zapis' if f.get('wczytana') else
                                                                  'nowa kampania / zapis: nie wiadomo'))
    if 'mnoznik_rudy' in f:
        czesci.append('wydobycie rudy x%.1f, drewna x%.1f' % (f['mnoznik_rudy'], f.get('mnoznik_drewna', 0.0)))
    if 'pula_poczatkowa' in f:
        czesci.append('pula wyrzutkow na starcie %d%s' % (f['pula_poczatkowa'], (' (zdjeto %s hearth)' % skr(f['zdjeto_hearth']))
                                                         if 'zdjeto_hearth' in f else ''))
    L.append('start: ' + ' | '.join(czesci))
    ss = f.get('startstock') or []
    if ss:
        op = []
        for w in ss:
            m = re.search(r'StartStock: (\w+) - (\d+) szt\. po .*? -> (\d+) ladunkow po .*?cel (-?\d+)', w.tresc)
            if m:
                op.append('%s %s szt. -> %s ladunkow (cel %s)' % m.groups())
            m = re.search(r'gotowe - .*?; potkniecia (\d+)', w.tresc)
            if m:
                op.append('potkniecia %s' % m.group(1))
        L.append('StartStock (linie %d-%d): %s' % (ss[0].nr, ss[-1].nr, '; '.join(op) if op else
                                                 'bez przeliczenia - ' + obetnij(ss[0].tresc[12:], 100)))
    # linie startowe modulow
    licz = Counter(status_linii(w.tresc) for w in ses.start if w.pref)
    L.append('linie startowe modulow: %d (wpiete %d, CZYNNE %d, BRAK %d, NIE wpieta %d, WYLACZONE %d, pozostale %d)'
             % (sum(licz.values()), licz['wpiete'], licz['CZYNNE'], licz['BRAK'], licz['NIE wpieta'], licz['WYLACZONE'],
                licz['inne']))
    zle = [w for w in ses.start if w.pref and status_linii(w.tresc) in ('BRAK', 'NIE wpieta', 'WYLACZONE')]
    for w in zle[:5]:
        L.append('  [%s] linia %d: %s' % (status_linii(w.tresc), w.nr, wycinek(w.tresc, szer - 30)))
    if len(zle) > 5:
        L.append('  ... i %d dalszych (linie %s)' % (len(zle) - 5, ', '.join(str(w.nr) for w in zle[5:15])))
    brak_startu = []
    for nazwa_o, wz in OCZEKIWANE_STARTU:
        rx = re.compile(wz)
        if not any(rx.search(w.tresc) for w in ses.start):
            brak_startu.append(nazwa_o)
    L.append('oczekiwane linie startu (STAN-PRAC): %d z %d%s'
             % (len(OCZEKIWANE_STARTU) - len(brak_startu), len(OCZEKIWANE_STARTU),
                (' - BRAK: ' + ', '.join(brak_startu)) if brak_startu else ''))
    # uklad linii ksiegi pieniadza (wpis 102, ktory jest w grze, albo poprawka 102b)
    uk = f.get('uklad_ksiegi')
    if uk:
        ul = f.get('uklad_linii') or {}
        if uk == UKLAD_102B:
            op = 'uklad 102b - bilans z "rozliczenia rodow na plus / na minus" i "W tym zold naliczony", linia "Pieniadz swiata (rody):"'
        else:
            op = 'uklad wpisu 102 - zold jest pozycja ujsc bilansu i siedzi tez w saldach rodow (reszta zawyzona o zold; naprawia 102b)'
        if f.get('uklad_mieszany'):
            op += ' | UWAGA: linie w obu ukladach (102: %d, 102b: %d)' % (ul.get(UKLAD_102, 0), ul.get(UKLAD_102B, 0))
        elif not ul:
            op += ' (wg linii startowej - linii dziennych ksiegi jeszcze nie ma)'
        L.append('ksiega pieniadza: ' + op)
    # lancuch
    w_grze = ses.ogniwo_w_grze
    nowa = bool(f.get('nowa_kampania'))
    # ogniwo 104 nie ma linii przy starcie gry - jego linie "StartStock:" pojawiaja sie tylko przy starcie kampanii
    brakujace = [o for o in ses.ogniwa if not o['jest'] and (o['zawsze'] or nowa) and o['ix'] >= ix_ogniwa('102')]
    L.append('OGNIWO W GRZE (wg linii logu): %s%s' % (
        ('%s %s' % (w_grze, [o['opis'] for o in ses.ogniwa if o['nr'] == w_grze][0])) if w_grze else 'sprzed wpisu 100',
        (' | NIE MA linii startowych ogniw: ' + ', '.join(o['nr'] for o in brakujace)) if brakujace else
        ' | komplet linii startowych lancucha 102-107, 115-119, 108-113'))
    for o in ses.ogniwa:
        if o['jest']:
            op = 'JEST  linia %d%s %s' % (o['wpis'].nr, (' [%s]' % o['status']) if o['status'] != 'inne' else '',
                                          wycinek(o['wpis'].tresc, 62))
        elif not o['zawsze']:
            wyzsze = any(x['jest'] for x in ses.ogniwa if x['ix'] > o['ix'] and x['zawsze'])
            if nowa:
                op = 'BRAK  linii "StartStock:" przy starcie NOWEJ kampanii -> ogniwo nie wgrane' + (
                    ' (a wyzsze ogniwo jest - patrz alarmy)' if wyzsze else '')
            else:
                op = 'brak linii "StartStock:" - ' + ('wyzsze ogniwo wgrane, wiec 104 jest (linie tylko przy starcie nowej '
                                                      'kampanii)' if wyzsze else 'nie do rozstrzygniecia (wczytany zapis; '
                                                                                  'linie tylko przy starcie nowej kampanii)')
        else:
            op = 'BRAK  poznac po: ' + o['poznac']
        dz = ', '.join('"%s:" %d dob' % (n, c) for n, c in o['dob'].items())
        if o['znak']:
            dz = (dz + ', ' if dz else '') + '"%s:" z odcinkiem ogniwa %d dob' % (o['znak'][0], o['znak_dob'] or 0)
        if not o['jest'] and o['zawsze'] and not any(o['linii'].values()) and not o['znak_dob']:
            dz = ''                         # brak ogniwa: miejsce zajmuje opis, po czym je poznac
        L.append('  %-4s %-17s %s%s' % (o['nr'], o['opis'], op, (' | ' + dz) if dz else ''))
    return [obetnij(x, szer) for x in L]


def linie_bledow(ses, szer, pelny):
    L = ['--- BLEDY: ERROR %d, Exception %d, potkniecia niezerowe %d ---' % (ses.n_error, ses.n_exception, ses.n_potkniec)]
    lim = len(ses.bledy) if pelny else MAKS_GRUP_BLEDOW
    for klucz, ile, nr, przyklad in ses.bledy[:lim]:
        L.append(obetnij('  %3dx %s  (pierwsze: linia %d)%s' % (ile, klucz, nr, (' ' + przyklad) if przyklad else ''), szer))
    if len(ses.bledy) > lim:
        L.append('  ... i %d dalszych grup (--pelny)' % (len(ses.bledy) - lim))
    return L


def linie_alarmow(ses, szer, pelny):
    ile_a = sum(1 for a in ses.alarmy if a[0] == 'ALARM')
    L = ['--- ALARMY: %d, uwagi: %d (progi: stale na gorze skryptu) ---' % (ile_a, len(ses.alarmy) - ile_a)]
    lim = len(ses.alarmy) if pelny else MAKS_ALARMOW
    for poziom, nr, tekst in ses.alarmy[:lim]:
        gdzie = ('linia %d' % nr) if nr else 'caly log'
        L.append(obetnij('  [%s] %s: %s' % (poziom, gdzie, tekst), szer))
    if len(ses.alarmy) > lim:
        L.append('  ... i %d dalszych (--pelny)' % (len(ses.alarmy) - lim))
    if not ses.alarmy:
        L.append('  brak')
    return L


def wiersze_tematu(ses, t, bloki_probki):
    """[(etykieta tematu, etykieta miary, [komorki])] dla skrotu."""
    s = ses.szer.get(t.nazwa) or {}
    wyn = []
    for etykieta, szablon in t.skrot:
        kom = [wypelnij(szablon, s.get(bi)) for bi in bloki_probki]
        if all(k == '-' for k in kom):
            wszystkie = [wypelnij(szablon, s.get(bi)) for bi in ses.wyb]
            if all(k == '-' for k in wszystkie):
                continue
        wyn.append((NAZWY_KROTKIE.get(t.nazwa, t.nazwa), etykieta, kom))
    return wyn


def tabela_grupy(ses, tytul, tematy, szer, ile_probek, surowe_tematy):
    """Linie tabeli jednej grupy; liczba probek zmniejszana, az tabela zmiesci sie w szerokosci."""
    pelne = ses.pelne()
    if not pelne:
        return []
    ile = min(ile_probek, len(pelne))
    while True:
        pr = probki(pelne, ile)
        wiersze = []
        for t in tematy:
            if t.nazwa in surowe_tematy:
                continue
            wiersze.extend(wiersze_tematu(ses, t, pr))
        if not wiersze:
            break
        w1 = min(22, max(len(w[0]) for w in wiersze))
        w2 = min(38, max(len(w[1]) for w in wiersze))
        szerokosci = []
        for k in range(len(pr)):
            szerokosci.append(max([len(str(pr[k] + 1)) + 1] + [len(w[2][k]) for w in wiersze]) + 2)
        if w1 + 1 + w2 + sum(szerokosci) <= szer or ile <= 2:
            break
        ile -= 1
    L = []
    if wiersze:
        glowa = ('--- %s ---' % tytul).ljust(w1 + 1 + w2 - 5) + 'doba:'
        glowa += ''.join(str(pr[k] + 1).rjust(szerokosci[k]) for k in range(len(pr)))
        L.append(obetnij(glowa, szer))
        poprzedni = None
        for nazwa, etykieta, kom in wiersze:
            lewa = (obetnij(nazwa, w1) if nazwa != poprzedni else '').ljust(w1) + ' ' + obetnij(etykieta, w2).ljust(w2)
            L.append(obetnij(lewa + ''.join(kom[k].rjust(szerokosci[k]) for k in range(len(pr))), szer))
            poprzedni = nazwa
    # tematy w trybie surowym (format nierozpoznany): pierwsza, srodkowa, ostatnia linia
    for t in tematy:
        if t.nazwa not in surowe_tematy:
            continue
        lin = [w for w in ses.linie_tematu.get(t.nazwa, []) if w.blok is None or w.blok in ses.wyb]
        if not lin:
            continue
        if not L:
            L.append('--- %s ---' % tytul)
        wyb = [lin[0]] if len(lin) == 1 else ([lin[0], lin[-1]] if len(lin) == 2 else [lin[0], lin[len(lin) // 2], lin[-1]])
        L.append('%s  [TRYB SUROWY - format nierozpoznany, %d linii]' % (t.nazwa, len(lin)))
        for w in wyb:
            L.append(obetnij('  doba %s linia %d: %s' % ((w.blok + 1) if w.blok is not None else '-', w.nr, w.tresc), szer))
    return L


def tematy_surowe(ses):
    """Tematy, ktore spadaja do trybu surowego: zadna linia dzienna nie przeszla parsera."""
    wyn = set()
    for t in TEMATY:
        if not t.dzienny or (not t.kol and t.extra is None):
            continue
        linie = ses.linie_tematu.get(t.nazwa) or []
        if linie and not (ses.szer.get(t.nazwa) or {}):
            wyn.add(t.nazwa)
    return wyn


def linie_nierozpoznanych(ses, szer):
    L = []
    surowe = tematy_surowe(ses)
    for t in TEMATY:
        zle = ses.zle.get(t.nazwa) or []
        if not zle:
            continue
        linie = ses.linie_tematu.get(t.nazwa) or []
        braki = Counter()
        for w, b in zle:
            braki.update(b)
        L.append(obetnij('  %s: %d z %d linii nie pasuje do wzorca (brak pol: %s; pierwsza: linia %d)%s'
                         % (t.nazwa, len(zle), len(linie), ', '.join(k for k, _ in braki.most_common(6)), zle[0][0].nr,
                            ' -> TRYB SUROWY' if t.nazwa in surowe else ' -> te doby pominiete w szeregu'), szer))
    for pref, wpisy in ses.obce.items():
        if pref.endswith('(inny rodzaj linii)'):
            L.append(obetnij('  %s: %d linii o nieznanym poczatku (pierwsza: linia %d: %s)'
                             % (pref, len(wpisy), wpisy[0].nr, wpisy[0].tresc), szer))
    for nazwa, s in ses.szer.items():
        powt = [bi for bi, d in s.items() if isinstance(d, dict) and d.get('_powtorzen')]
        if powt:
            L.append(obetnij('  %s: w %d dobach wiecej niz jedna linia (brana ostatnia), np. doba %d'
                             % (nazwa, len(powt), min(powt) + 1), szer))
    for u in ses.uwagi:
        L.append(obetnij('  ' + u, szer))
    return ['--- FORMATY NIEROZPOZNANE: %d ---' % len(L)] + (L if L else ['  brak'])


def linie_obcych(ses, szer, surowe=False):
    """Prefiksy bez parsera, ktore powtarzaja sie w dobach: jedna linia na prefiks."""
    L = []
    for pref, wpisy in ses.obce.items():
        if pref.endswith('(inny rodzaj linii)'):
            continue
        wp = [w for w in wpisy if w.blok in ses.wyb]
        dob = len(set(w.blok for w in wp))
        if dob < 2 and not any(w.dzien is not None for w in wp):
            continue
        if not wp:
            continue
        if surowe and len(wp) > 1:
            L.append(obetnij('%s [x%d, %d dob] doba %d linia %d: %s' % (pref, len(wp), dob, wp[0].blok + 1, wp[0].nr,
                                                                       wp[0].reszta or ''), szer))
        L.append(obetnij('%s [x%d, %d dob] doba %d linia %d: %s' % (pref, len(wp), dob, wp[-1].blok + 1, wp[-1].nr,
                                                                   wp[-1].reszta or ''), szer))
    return L


def drukuj_skrot(ses, szer=SZEROKOSC, pelny=False, surowe=False, limit=LIMIT_LINII):
    L = []
    L.extend(linie_naglowka(ses, szer))
    L.extend(linie_bledow(ses, szer, pelny))
    L.extend(linie_alarmow(ses, szer, pelny))
    # kontrole najwyzszego ogniwa wykrytego w logu ("po czym poznac w logu" z jego paczki)
    if ses.ogniwo_w_grze:
        try:
            L.extend(linie_kontroli(ses, ses.ogniwo_w_grze, szer, None if pelny else MAKS_KONTROLI, pelne=pelny))
            L.extend(linia_pozostalych_ogniw(ses, szer))
            L.extend(linia_grupy(ses, szer))
        except Exception as e:
            ses.uwagi.append('kontrole ogniwa %s: blad wewnetrzny (%s: %s)' % (ses.ogniwo_w_grze, type(e).__name__, e))
    nier = linie_nierozpoznanych(ses, szer)
    sur = tematy_surowe(ses)
    if surowe:
        sur = set(t.nazwa for t in TEMATY)
    obce = linie_obcych(ses, szer, surowe)
    cialo, dodatkowe = [], []
    for g, tytul in GRUPY.items():
        tematy = [t for t in TEMATY if t.grupa == g and t.dzienny]
        if surowe:
            blok = _surowe_grupy(ses, tytul, tematy, szer)
        else:
            blok = tabela_grupy(ses, tytul, tematy, szer, PROBKI, sur)
        if g == 'dodatkowe':
            dodatkowe = blok
        else:
            cialo.extend(blok)
    zd = []
    for t in TEMATY:
        if t.dzienny:
            continue
        ile = len([w for w in ses.linie_tematu.get(t.nazwa, []) if w.blok in ses.wyb])
        if ile:
            zd.append('%s x%d' % (t.nazwa, ile))
    if zd:
        dodatkowe = dodatkowe + [obetnij('linie niedzienne w dobach (zdarzenia; --temat NAZWA): ' + ', '.join(zd), szer)]
    if obce:
        dodatkowe = dodatkowe + ['--- POZOSTALE PREFIKSY DZIENNE (bez parsera, linia obcieta) ---'] + obce
    stale = len(L) + len(cialo) + len(nier)
    if not pelny and limit and stale + len(dodatkowe) > limit:
        miejsce = max(3, limit - stale - 1)
        pominiete = len(dodatkowe) - miejsce
        dodatkowe = dodatkowe[:miejsce] + ['  ... %d dalszych wierszy tematow dodatkowych (--pelny albo --temat NAZWA)'
                                           % pominiete]
    L.extend(cialo)
    L.extend(dodatkowe)
    L.extend(nier)
    return L


def _surowe_grupy(ses, tytul, tematy, szer):
    L = []
    for t in tematy:
        lin = [w for w in ses.linie_tematu.get(t.nazwa, []) if w.blok in ses.wyb]
        if not lin:
            continue
        if not L:
            L.append('--- %s (linie surowe: pierwsza i ostatnia) ---' % tytul)
        for w in ([lin[0]] if len(lin) == 1 else [lin[0], lin[-1]]):
            L.append(obetnij('%s [x%d] doba %d linia %d: %s' % (t.nazwa, len(lin), w.blok + 1, w.nr, w.reszta or ''), szer))
    return L


# ------------------------------------------------------------------------------------------------ --temat
def znajdz_tematy(ses, nazwa):
    """Nazwy tematow (z parserem) i prefiksow obcych pasujace do zapytania."""
    q = nazwa.strip().lower().rstrip(':')
    nazwy = list(TEMATY_WG_NAZWY.keys())
    obce = [p for p in ses.obce.keys()]
    inne = sorted(set(w.pref for w in ses.wpisy if w.pref) - set(t.prefiks for t in TEMATY) - set(obce))
    for pula in (nazwy, obce, inne):
        dokl = [n for n in pula if n.lower() == q]
        if dokl:
            return dokl
    wsz = nazwy + obce + inne
    pocz = [n for n in wsz if n.lower().startswith(q)]
    if pocz:
        return pocz
    return [n for n in wsz if q in n.lower()]


def tabela_pelna(naglowki, wiersze, szer, stale=3):
    """Tabela wiersz = doba; kolumny lamane na kawalki mieszczace sie w szerokosci (pierwsze 'stale' powtarzane)."""
    L = []
    if not wiersze:
        return L
    szerok = [max(len(str(naglowki[i])), max(len(str(w[i])) for w in wiersze)) for i in range(len(naglowki))]
    szerok = [min(s, 40) for s in szerok]
    baza = sum(szerok[:stale]) + stale
    i = stale
    while i < len(naglowki) or (i == stale and len(naglowki) == stale):
        j, zajete = i, baza
        while j < len(naglowki) and (zajete + szerok[j] + 1 <= szer or j == i):
            zajete += szerok[j] + 1
            j += 1
        kol = list(range(stale)) + list(range(i, j))
        L.append(obetnij(' '.join(obetnij(str(naglowki[k]), szerok[k]).rjust(szerok[k]) for k in kol), szer))
        for w in wiersze:
            L.append(obetnij(' '.join(obetnij(str(w[k]), szerok[k]).rjust(szerok[k]) for k in kol), szer))
        if j >= len(naglowki):
            break
        L.append('')
        i = j
    return L


KOLUMNY_ZE_ZNAKIEM = ('zmiana', 'bez_wyjasnienia', 'reszta', 'bil_zmiana', 'bil_reszta', 'rozjazd')


def _komorka_pelna(klucz, v):
    """Komorka tabeli --temat: pelna liczba, zmiany ze znakiem."""
    if v is None:
        return '-'
    if isinstance(v, bool):
        return 'TAK' if v else 'nie'
    if isinstance(v, float):
        return '%g' % v
    if isinstance(v, int) and (klucz.endswith('_zm') or klucz in KOLUMNY_ZE_ZNAKIEM):
        return '%+d' % v
    return v


def drukuj_temat(ses, nazwa, szer=SZEROKOSC, surowe=False):
    L = []
    trafione = znajdz_tematy(ses, nazwa)
    if not trafione:
        L.append('temat "%s": nic nie pasuje. Tematy z parserem: %s' % (nazwa, ', '.join(TEMATY_WG_NAZWY.keys())))
        L.append('prefiksy bez parsera w tym logu: %s' % (', '.join(sorted(ses.obce.keys())) or 'brak'))
        return L
    for n in trafione:
        t = TEMATY_WG_NAZWY.get(n)
        if t is None:
            lin = [w for w in ses.wpisy if w.pref == n or (n in ses.obce and w in ses.obce[n])]
            L.append('=== PREFIKS BEZ PARSERA: %s (%d linii) ===' % (n, len(lin)))
            for w in lin:
                if w.blok is not None and w.blok not in ses.wyb:
                    continue
                L.append('doba %s linia %d: %s' % ((w.blok + 1) if w.blok is not None else '-', w.nr, w.tresc))
            continue
        lin = [w for w in ses.linie_tematu.get(n, []) if w.blok is None or w.blok in ses.wyb]
        # linie startu gry stoja przed znacznikiem kampanii - nie ma ich wsrod linii tematu; temat jednorazowy pokazuje je
        # zawsze (np. "CastlePurse:" = linia startowa + jednorazowe przyciecie daru w pierwszej dobie kampanii)
        if not lin or not t.dzienny:
            znane = set(w.nr for w in lin)
            ze_startu = [w for w in ses.start if w.pref == t.prefiks and t.pasuje(w.reszta or '') and w.nr not in znane]
            if ze_startu:
                lin = sorted(ze_startu + lin, key=lambda w: w.nr)
        s = ses.szer.get(n) or {}
        L.append('=== TEMAT: %s (prefiks "%s:", %d linii, %d dob z danymi%s) ==='
                 % (n, t.prefiks, len(lin), len([bi for bi in ses.wyb if bi in s]),
                    (', linie %d-%d' % (lin[0].nr, lin[-1].nr)) if lin else ''))
        if not lin:
            L.append('  brak linii w tym logu')
            continue
        if surowe or not s:
            if not surowe:
                L.append('  (bez kolumn: temat jednorazowy albo format nierozpoznany - linie surowe)')
            for w in lin:
                L.append('doba %s linia %d: %s' % ((w.blok + 1) if w.blok is not None else '-', w.nr, w.tresc))
            continue
        klucze = []
        for bi in ses.wyb:
            for k in (s.get(bi) or {}):
                if not k.startswith('_') and k not in klucze:
                    klucze.append(k)
        naglowki = ['doba', 'dzien', 'linia'] + klucze
        wiersze = []
        for bi in ses.wyb:
            d = s.get(bi)
            if d is None:
                continue
            wiersze.append([bi + 1, ses.bloki[bi].D, d.get('_nr', '')] +
                           [_komorka_pelna(k, d.get(k)) for k in klucze])
        L.extend(tabela_pelna(naglowki, wiersze, szer))
        if t.wielo and t.nazwa == 'Skarbce':
            L.append('')
            L.append('krolestwa: pierwsza i ostatnia doba (skarbiec, kiesy rodow, wojsko rodow)')
            wg = ses.wiersze.get(n) or {}
            dob = [bi for bi in ses.wyb if bi in wg]
            if dob:
                a = dict((w['krolestwo'], w) for w in wg[dob[0]])
                b = dict((w['krolestwo'], w) for w in wg[dob[-1]])
                nag = ['krolestwo', 'wojna', 'skarbiec:%d' % (dob[0] + 1), 'skarbiec:%d' % (dob[-1] + 1),
                       'kiesy:%d' % (dob[0] + 1), 'kiesy:%d' % (dob[-1] + 1), 'wojsko:%d' % (dob[0] + 1),
                       'wojsko:%d' % (dob[-1] + 1), 'zwrot:%d' % (dob[-1] + 1)]
                wr = []
                for k in list(a.keys()) + [x for x in b.keys() if x not in a]:
                    x, y = a.get(k, {}), b.get(k, {})
                    wr.append([k, 'TAK' if y.get('wojna') else 'nie', x.get('skarbiec', '-'), y.get('skarbiec', '-'),
                               x.get('kiesy', '-'), y.get('kiesy', '-'), x.get('wojsko', '-'), y.get('wojsko', '-'),
                               y.get('zwrot') if y.get('zwrot') is not None else '-'])
                L.extend(tabela_pelna(nag, wr, szer, stale=1))
        zle = ses.zle.get(n) or []
        if zle:
            L.append('  UWAGA: %d linii nie pasuje do wzorca (pierwsza: linia %d, brak pol: %s)'
                     % (len(zle), zle[0][0].nr, ', '.join(zle[0][1])))
    return L


# ------------------------------------------------------------------------------------------------ --porownaj
def drukuj_porownanie(a, b, szer=SZEROKOSC):
    L = ['=== POROWNANIE SESJI: A = %s | B = %s ===' % (os.path.basename(a.sciezka), os.path.basename(b.sciezka))]
    for znak, s in (('A', a), ('B', b)):
        ile_a = sum(1 for x in s.alarmy if x[0] == 'ALARM')
        zakres = ('doby 1-%d = dni gry %d-%d' % (len(s.bloki), s.bloki[0].D, s.bloki[-1].D)) if s.bloki else 'brak dob'
        L.append(obetnij('%s: %s | %d linii | %s | ogniwo w grze: %s | ERROR %d, Exception %d, potkniecia %d | alarmy %d, '
                         'uwagi %d' % (znak, os.path.basename(s.sciezka), s.linii, zakres, s.ogniwo_w_grze or 'sprzed 100',
                                       s.n_error, s.n_exception, s.n_potkniec, ile_a, len(s.alarmy) - ile_a), szer))
    pa, pb = a.pelne(), b.pelne()
    if not pa and not pb:
        L.append('zadna z sesji nie ma dob - nie ma czego porownac')
        return L
    kol = [('A:doba %d' % (pa[0] + 1), a, pa[0]) if pa else None, ('A:doba %d' % (pa[-1] + 1), a, pa[-1]) if pa else None,
           ('B:doba %d' % (pb[0] + 1), b, pb[0]) if pb else None, ('B:doba %d' % (pb[-1] + 1), b, pb[-1]) if pb else None]
    wiersze = []
    for t in TEMATY:
        if not t.dzienny or not t.skrot:
            continue
        for etykieta, szablon in t.skrot:
            kom = []
            for k in kol:
                kom.append('-' if k is None else wypelnij(szablon, (k[1].szer.get(t.nazwa) or {}).get(k[2])))
            if all(x == '-' for x in kom):
                continue
            wiersze.append((t.nazwa, etykieta, kom))
    if not wiersze:
        L.append('brak wspolnych tematow z danymi')
        return L
    w1 = min(28, max(len(w[0]) for w in wiersze))
    w2 = min(46, max(len(w[1]) for w in wiersze))
    sz = []
    for i in range(4):
        sz.append(max([len(kol[i][0]) if kol[i] else 1] + [len(w[2][i]) for w in wiersze]) + 2)
    nadmiar = w1 + 1 + w2 + sum(sz) - szer
    if nadmiar > 0:
        w2 = max(20, w2 - nadmiar)
    L.append(obetnij(' ' * (w1 + 1 + w2) + ''.join((kol[i][0] if kol[i] else '-').rjust(sz[i]) for i in range(4)), szer))
    poprzedni = None
    for nazwa, etykieta, kom in wiersze:
        lewa = (obetnij(nazwa, w1) if nazwa != poprzedni else '').ljust(w1) + ' ' + obetnij(etykieta, w2).ljust(w2)
        L.append(obetnij(lewa + ''.join(kom[i].rjust(sz[i]) for i in range(4)), szer))
        poprzedni = nazwa
    for znak, s in (('A', a), ('B', b)):
        al = [x for x in s.alarmy if x[0] == 'ALARM']
        L.append('--- alarmy %s: %d ---' % (znak, len(al)))
        for poziom, nr, tekst in al[:10]:
            L.append(obetnij('  [%s] %s: %s' % (poziom, ('linia %d' % nr) if nr else 'caly log', tekst), szer))
        if len(al) > 10:
            L.append('  ... i %d dalszych' % (len(al) - 10))
    return L


# ------------------------------------------------------------------------------------------------ --csv
def sciezka_csv(sciezka_logu):
    kat = os.path.dirname(os.path.abspath(sciezka_logu))
    baza = os.path.splitext(os.path.basename(sciezka_logu))[0]
    sesja = baza[len('Armoury-'):] if baza.startswith('Armoury-') else baza
    for kandydat in (os.path.join(kat, 'Logs', sesja, PLIK_CSV), os.path.join(kat, sesja, PLIK_CSV),
                     os.path.join(kat, PLIK_CSV)):
        if os.path.isfile(kandydat):
            return kandydat
    return os.path.join(kat, 'Logs', sesja, PLIK_CSV)


def skrot_csv_z_tekstu(tekst, szer=SZEROKOSC, ile=8):
    """Skrot pliku ludzie-regiony.csv: regiony o najwiekszym obciazeniu w ostatniej dobie."""
    L = []
    linie = [x for x in tekst.splitlines() if x.strip()]
    if len(linie) < 2:
        return ['plik CSV pusty albo sam naglowek']
    nagl = [x.strip() for x in linie[0].split(';')]
    idx = dict((n, i) for i, n in enumerate(nagl))
    if 'dzien' not in idx or 'obciazenie_proc_mezczyzn' not in idx:
        return ['plik CSV ma inny naglowek niz oczekiwany (brak kolumn dzien / obciazenie_proc_mezczyzn): '
                + obetnij(linie[0], szer - 90)]
    wiersze, zle = [], 0
    for x in linie[1:]:
        p = x.split(';')
        if len(p) < len(nagl):
            zle += 1
            continue
        wiersze.append(p)

    def pole(p, n):
        return p[idx[n]].strip() if n in idx and idx[n] < len(p) else ''

    def lf(s):
        try:
            return float(s.replace(',', '.'))
        except ValueError:
            return None
    dni = sorted(set(int(lf(pole(p, 'dzien'))) for p in wiersze if lf(pole(p, 'dzien')) is not None))
    if not dni:
        return ['plik CSV bez wierszy z numerem dnia']
    ostatni = dni[-1]
    dzis = [p for p in wiersze if lf(pole(p, 'dzien')) == ostatni]
    z_obc = [(lf(pole(p, 'obciazenie_proc_mezczyzn')), p) for p in dzis]
    ranking = sorted([x for x in z_obc if x[0] is not None], key=lambda x: -x[0])
    ponad = sum(1 for x in ranking if x[0] > 20.0)
    L.append('dni w pliku: %d-%d (%d dob), wierszy %d%s; ostatnia doba %d: regionow %d, z ludnoscia %d, ponad prog 20%% '
             'mezczyzn: %d' % (dni[0], ostatni, len(dni), len(wiersze), (', uszkodzonych %d' % zle) if zle else '',
                              ostatni, len(dzis), len(ranking), ponad))
    kol = [('region', 'region'), ('rodzaj', 'rodzaj'), ('kultura', 'kultura'), ('krolestwo', 'krolestwo'),
           ('ludzie', 'ludzie'), ('zmiana', 'ludzie_zmiana'), ('zaloga', 'zaloga'), ('milicja', 'milicja'),
           ('wyrzutki', 'wyrzutki'), ('bandy', 'bandy_ludzie'), ('zabici', 'zabici_dzis'), ('zwerb.', 'zwerbowani_dzis'),
           ('obciaz.%', 'obciazenie_proc_mezczyzn'), ('bezp.', 'bezpieczenstwo'), ('bil.zywn.', 'bilans_zywnosci'),
           ('glod', 'glod'), ('wojna', 'wojna')]
    kol = [k for k in kol if k[1] in idx]
    nag = ['nr'] + [k[0] for k in kol]
    wr = []
    for i, (o, p) in enumerate(ranking[:ile], 1):
        wr.append([i] + [obetnij(pole(p, k[1]), 26) for k in kol])
    L.extend(tabela_pelna(nag, wr, szer, stale=2))
    # ogniwo 113: trzy kolumny na koncu pliku - uchodzcy wsi regionu poza domem, ich udzial w ludziach wsi sprzed
    # spustoszenia, zabici przy spustoszeniu od poczatku kampanii
    nowe = [k for k in ('uchodzcy', 'spustoszenie_proc', 'zabici_spustoszenie') if k in idx]
    if len(nowe) < 3:
        L.append('spustoszenie (ogniwo 113): plik ma %d z 3 kolumn uchodzcy / spustoszenie_proc / zabici_spustoszenie%s'
                 % (len(nowe), ' - sesja sprzed ogniwa 113' if not nowe else ''))
        return L
    sp = sorted([(lf(pole(p, 'spustoszenie_proc')) or 0.0, lf(pole(p, 'uchodzcy')) or 0.0, p) for p in dzis],
                key=lambda x: (-x[0], -x[1]))
    z_uch = [x for x in sp if x[1] > 0]
    L.append('spustoszenie (ogniwo 113), ostatnia doba %d: uchodzcy poza domem razem %d w %d z %d regionow, zabici przy '
             'spustoszeniu od poczatku kampanii %d%s'
             % (ostatni, int(sum(x[1] for x in sp)), len(z_uch), len(dzis),
                int(sum((lf(pole(x[2], 'zabici_spustoszenie')) or 0.0) for x in sp)),
                '' if z_uch else ' - zaden region nie ma uchodzcow'))
    if z_uch:
        kol2 = [('region', 'region'), ('rodzaj', 'rodzaj'), ('kultura', 'kultura'), ('wsie', 'wsie'),
                ('spalone', 'wsie_spalone'), ('ludzie wsi', 'ludzie_wsie'), ('uchodzcy', 'uchodzcy'),
                ('spust.%', 'spustoszenie_proc'), ('zab.spust.', 'zabici_spustoszenie'), ('hearth', 'hearth'),
                ('bezp.', 'bezpieczenstwo'), ('wojna', 'wojna')]
        kol2 = [k for k in kol2 if k[1] in idx]
        wr = []
        for i, (o, u, p) in enumerate(z_uch[:ile], 1):
            wr.append([i] + [obetnij(pole(p, k[1]), 26) for k in kol2])
        L.extend(tabela_pelna(['nr'] + [k[0] for k in kol2], wr, szer, stale=2))
    return L


def drukuj_csv(sciezka_logu, szer=SZEROKOSC):
    sc = sciezka_csv(sciezka_logu)
    L = ['=== CSV: %s ===' % sc]
    if not os.path.isfile(sc):
        L.append('pliku nie ma (ksiega ludzi pisze go od wpisu 102; katalog sesji: Logs\\<sesja>\\)')
        return L
    try:
        with open(sc, 'rb') as f:
            b = f.read()
        tekst, kod = dekoduj(b)
        L.append('rozmiar %d B, kodowanie %s' % (len(b), kod))
        L.extend(skrot_csv_z_tekstu(tekst, szer))
    except Exception as e:
        L.append('nie udalo sie odczytac pliku (%s: %s)' % (type(e).__name__, e))
    return L


# =====================================================================================================================
# WEJSCIE
# =====================================================================================================================
def najnowszy_log(katalog=KATALOG_LOGOW):
    pliki = glob.glob(os.path.join(katalog, WZORZEC_LOGU))
    if not pliki:
        return None
    return max(pliki, key=lambda p: (os.path.getmtime(p), p))


def wczytaj_sesje(sciezka, wczytanie=None, dni=None):
    with open(sciezka, 'rb') as f:
        b = f.read()
    tekst, kod = dekoduj(b)
    return analizuj(tekst, sciezka, kod, len(b), wczytanie, dni)


def parsuj_dni(s):
    m = re.match(r'^\s*(\d+)\s*(?:-|\.\.|:)\s*(\d+)\s*$', s or '')
    if m:
        a, b = int(m.group(1)), int(m.group(2))
        return (min(a, b), max(a, b))
    m = re.match(r'^\s*(\d+)\s*$', s or '')
    if m:
        return (int(m.group(1)), int(m.group(1)))
    return None


def wypisz(linie):
    out = sys.stdout
    try:
        out.reconfigure(encoding='utf-8', errors='replace')
    except Exception:
        pass
    for x in linie:
        try:
            out.write(x + '\n')
        except Exception:
            out.write(x.encode('ascii', 'replace').decode('ascii') + '\n')


def main(argv=None):
    ap = argparse.ArgumentParser(description='Zwiezly skrot sesji logu Armoury (tylko stdout).', add_help=True)
    ap.add_argument('log', nargs='?', help='sciezka logu; domyslnie najnowszy Armoury-*.log w katalogu moda')
    ap.add_argument('--temat', help='pelny szereg jednego tematu (wszystkie doby)')
    ap.add_argument('--dni', help='zakres dob A-B (numery porzadkowe albo dni gry)')
    ap.add_argument('--surowe', action='store_true', help='linie bez parsowania')
    ap.add_argument('--porownaj', metavar='LOG2', help='drugi log: pierwsza i ostatnia doba kazdego tematu obok siebie')
    ap.add_argument('--csv', action='store_true', help='skrot pliku ludzie-regiony.csv z katalogu sesji')
    ap.add_argument('--wczytanie', type=int, help='numer kampanii / wczytania w logu (od 1)')
    ap.add_argument('--test', metavar='NAZWA', help='kontrole "po czym poznac w logu" ogniwa albo grupy: 102b, 106, 103,104, '
                                                    '102b-105, surowce, pieniadz, towary2, ludzie, kasy, wykryte, wszystkie')
    ap.add_argument('--grupa', metavar='N', help='komplet kontroli grupy testowej naraz: 1 (101-105), 2 (106-107), 2b (115-119, '
                                                 'TOWARY 2), 3 (BetterEconomy + 108-109), 4 (110-112), 5 (113) albo w-grze')
    ap.add_argument('--pelny', action='store_true', help='bez limitu dlugosci skrotu')
    ap.add_argument('--szer', type=int, default=SZEROKOSC, help='szerokosc wyjscia (domyslnie %d)' % SZEROKOSC)
    try:
        a = ap.parse_args(argv)
    except SystemExit as e:
        return e.code if isinstance(e.code, int) else 2
    szer = max(60, a.szer or SZEROKOSC)
    sciezka = a.log or najnowszy_log()
    if not sciezka:
        wypisz(['nie znaleziono zadnego logu: %s' % os.path.join(KATALOG_LOGOW, WZORZEC_LOGU)])
        return 1
    if not os.path.isfile(sciezka):
        wypisz(['nie ma pliku: %s' % sciezka])
        return 1
    dni = parsuj_dni(a.dni) if a.dni else None
    if a.dni and dni is None:
        wypisz(['--dni: nie rozumiem "%s" (przyklady: 5-12, 108840-108850, 7)' % a.dni])
        return 2
    try:
        if a.csv:
            wypisz(drukuj_csv(sciezka, szer))
            return 0
        ses = wczytaj_sesje(sciezka, a.wczytanie, dni)
        if a.porownaj:
            if not os.path.isfile(a.porownaj):
                wypisz(['nie ma pliku: %s' % a.porownaj])
                return 1
            wypisz(drukuj_porownanie(ses, wczytaj_sesje(a.porownaj, None, dni), szer))
        elif a.grupa:
            wypisz(linie_naglowka(ses, szer)[:4] + drukuj_grupe(ses, a.grupa, szer))
        elif a.test:
            wypisz(linie_naglowka(ses, szer)[:4] + drukuj_test(ses, a.test, szer))
        elif a.temat:
            wypisz(linie_naglowka(ses, szer)[:3] + drukuj_temat(ses, a.temat, szer, a.surowe))
        else:
            wypisz(drukuj_skrot(ses, szer, a.pelny, a.surowe))
        return 0
    except Exception as e:     # ostatnia siatka: skrypt ma zawsze cos powiedziec
        import traceback
        tb = traceback.extract_tb(sys.exc_info()[2])
        gdzie = ('%s:%d' % (os.path.basename(tb[-1].filename), tb[-1].lineno)) if tb else '?'
        wypisz(['BLAD WEWNETRZNY skryptu (%s: %s w %s) - plik: %s' % (type(e).__name__, e, gdzie, sciezka),
                'sprobuj: --surowe albo --temat NAZWA --surowe'])
        return 3


if __name__ == '__main__':
    sys.exit(main())
