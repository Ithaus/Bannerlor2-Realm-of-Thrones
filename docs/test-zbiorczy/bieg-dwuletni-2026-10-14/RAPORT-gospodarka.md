# BIEG DWULETNI (728 dob) - GOSPODARKA OD LUDZI, TOWARY I LUD (14.10)

Bieg: nowa kampania, 728/728 dob, kod `w-toku/zbior-e8` 43af413 (= WGRANIE 49 + lup 5b; Armoury 6dd1e9e5 = test\fin21 - **bez** poprawek po audycie c3aa637).
Log `D:\Backup-Bannerlord\logs\2026-10-14-noc\Armoury-2026-10-13_21-14-13.log` (278 MB, czytany strumieniowo), tabele `...\Logs\2026-10-13_21-14-13\`.
Porownania: bieg roczny kodu 43 bez gospodarki od ludzi (`Armoury-2026-10-13_15-02-59.log`, dalej "W43") i bieg roczny gospodarki od ludzi
(`Armoury-2026-10-13_19-21-12.log`, paczki A + B bez reszty kiesy, P2 i zbytkow; dalej "GL-rok").
Znaczniki: **[P]** pomiar z logu / tabel, **[S]** szacunek albo wniosek. Doba = doba kampanii (doba gry - 108836 + 1). Kwartal = 91 dob.
Nic nie zmienione w kodzie ani w repo; gra nie uruchamiana.

## 0. Dla Jeffa - prosto

1. Swiat przezyl dwa lata bez glodu: z glodu nie umarl nikt [P], zadna warownia nie byla glodna [P]. Ludzi ubylo w roku 1 o 1.0% (52.13 -> 51.60 mln), w roku 2 liczba stoi (51.60 -> 51.62 mln) [P].
2. Rok 2 to w trzech czwartych **dluga zima ROT**: od doby 460 wsie daja srednio 41% mniej (Polnoc 56%, poludnie 15%), zboza o 31% mniej, lnu o 30% [P]. Zima potrwa 1 316 dob [P] - w nowej kampanii Jeffa przyjdzie tak samo, w dobie 460 [S].
3. Plon wsi idzie za ludzmi: wies zboza w Reach daje 3.3 razy tyle co na Polnocy, bo ma 3.2 razy wiecej ludzi [P]; w zimie 4.5 razy, bo Polnoc marznie mocniej [P].
4. Dochod panow z ziemi tez idzie za ludzmi: "waga ludzi" 0.63-0.81 w roku 1, bez reguly 0.15-0.27 [P]. Nie rosla z kwartalu na kwartal - skoczyla od razu; w zimie spadla do 0.40 (d637) i wrocila do 0.62 (d728) [P].
5. **Biedota wielkich miast rosnie.** Lud ludnych miast mial oszczednosci na 34 dni pod koniec roku 1, na 19 dni pod koniec roku 2 [P]; "stac na pelny koszyk" 94% -> 87% (w ostatnim tygodniu Reach 44%) [P]. Chleb swiata 97.1% -> 89.7% [P] (cel 98%). Na koniec 7 miast nie stac na jedzenie (Highgarden, Oldtown, Braavos, Rhyos, Aquos Dhaen, Volon Therys, Selhorys) [P]. Ludzie jedza (gra daje jedzenie), ale bieda jest ok. 3-4 razy wieksza niz pod koniec roku 1 (brak chleba 2.9% -> 10.3% koszyka, miast z klopotem 1.2 -> 4.4) [P].
6. Danina z oszczednosci ludu spadla z 448 do 221 tys. zl dziennie [P] - nie dlatego, ze lud zbiednial (kiesy ludu wzrosly z 20.8 do 28.9 mln [P]), tylko dlatego, ze od ok. doby 470 14-20 krolestw ma pokoj (w roku 1 trzy) [P], a w pokoju korona nie bierze.
7. Len: nowe wsie lniane podwoily len (860 szt./d w roku 1 [P]; przed paczka 400-520 [P]); w zimie 650 [P]. Plotna dalej brak w 75-77 z 97 miast [P] - waskie gardlo to tkalnie, nie len. Sol 900-1 150 szt./d, prawie cala kupuja wsie [P]; drewno (2 200/d) i ruda (230-250/d) bez klopotu [P].
8. Qarth: lud stac na jedzenie przez caly bieg (100%) [P], zapas warowni stoi (31-38 tys.) [P], ale na polkach targu jest jedzenia tylko na 3.5-4.9 dnia (cel 7) [P]. Dowozu do Qarthu log nie mierzy - nie wiem.
9. Budowy: 150 poziomow w roku 1, 208 w roku 2 [P]; lud dolozyl 36 i 31 poziomow (plan 40-90) [P]. Po lupach na koniec 54 spalone poziomy w 13 osadach; w roku 2 odbudowano 35 przy 38 spalonych - odbudowa nadaza [P]. Zbytki: lud znajduje towar tylko na 29% swojej oferty w roku 1 i 24% w roku 2 (pod koniec 19%; cel 50%) [P], glownie brak plotna. Konie robocze: 13.5 dziennie, stado stoi [P].
10. **Znaleziony blad (do naprawy):** licznik podatku jednej wsi przepelnia sie w roku 2 (ksiega pieniadza swiata pokazuje nagle +2.1 mld i +4.3 mld zl) [P]; zloto rodow NIE skoczylo - to licznik, nie prawdziwe zloto [P], ale gdyby pan go odebral, dostalby miliardy [S]. Podejrzana wies Tanos [S].

## 1. Progi narzedzia (`sprawdz_logi.py`, 8 grup, okno roku 1 `--do-doby 364` i calosci `--do-doby 728`) [P]

| grupa | progow | NIE rok 1 (d2-364) | NIE calosc (d2-728) | NIE - co (rok 1 -> calosc) |
|---|---:|---:|---:|---|
| gl1 | 10 | 1 | 1 | GL1-1 winogrona 0.949-0.950 przy progu 0.95 (dni 2-29 - to samo) |
| gl2 | 22 | 6 | 6 | GL2-1 potkniecia 2 (doba 1, zloto startowe); GL2-2 r 0.78 -> 0.64 (prog 0.8); GL2-6 Qarth min 2.9 -> 2.1 dob (prog 7); GL2-8 dni oszczednosci miast m_t > 1.5 min 27 / sr 31 -> min 14 / sr 17 (prog 60); GL2-9 Braavos bez zaworu 33 -> 220 dob z rzedu (prog 14); GL2-S1 kiesy wsi -4 000 zl (poprawka c3aa637 nie w tym biegu) |
| glb | 18 | 3 | 4 | GLB-P4-2 p90/p10 rekrutow 6.17 -> 100.7 (LevyByPeople wylaczony - INFO [S]); GLB-P5-1 len na x1.0 regulatora 709 TAK -> 628 NIE (prog 680; zima); GLB-P5-2 miast bez plotna 74.9 -> 76.9 (prog 60); GLB-P5-3 regulator lnu na suficie x1.30 |
| kiesa1 | 32 | 5 (+1 DECYZJA) | 4 (+2 DECYZJA) | P1-1 lokata W 15.48 -> 25.08 mln (prog 6; 66% w krolestwach w pokoju); P1-6 chleb 97.1% (prog 98); P1-8 dlug koron 0.93 wobec 0.52 mln; P1-9 rok 1 +20.9 mln NIE -> calosc +4.6 mln TAK; P1-19 czas 0.13 ms (prog 0.1); DECYZJA P1-3 kiesy ludu 20.4 -> 29.0 mln; DECYZJA P1-5c Pieniadz swiata 91% (blad licznika - rozdz. 12) |
| zbytki | 17 | 5 | 5 | ZB-2 kupione / oferta 27.2% -> 25.2% (prog 50); ZB-6 miast z kasa ponizej zapasu >= 7 dob 7 -> 10 (prog 4); ZB-7 filc w jukach 6 970 -> 7 347 (prog 5 000); ZB-11 czas 3.3-3.5 ms (prog 1); ZB-13 czas paczek razem 5.4-5.6 ms (prog 2) |
| p2bud | 13 | 3 | 3 | P2-2 roboty ludu 7.4 -> 6.5 tys. zl/d (prog 8); P2-3 poziomy ludu 40 -> 35 na rok (prog 40-90); P2-7 czas 1.9 ms (prog 1) |
| k13a | 12 | 1 | 1 | KR-8 stos koni zwyklych i bojowych ponad 60 dni zuzycia rosnie (rok 1: 1 przypadek; calosc: 4, bojowe do 3 397 dni zuzycia w d561) |
| m6b | 16 | 2 | 2 | M6b-4b juczne bez wyjasnienia -35.3 -> -32.3 szt./d (prog 20); M6b-5a ryby bez wyjasnienia +41.7 -> +72.2 szt./d (prog 20) |
| **razem** | **140** | **26 (+1 DECYZJA)** | **26 (+2 DECYZJA)** | |

Uwaga do narzedzia [P]: w `--grupa kiesa1` przy `--do-doby 728` okna P1-2, P1-4, P1-6, P1-7 sa na sztywno d301-364, a P1-8 d182 / d364 - powtarzaja rok 1. Odpowiedniki roku 2 liczone osobno (rozdz. 5-6).
GLB-P4-3 (odmowy hamulca) pokazuje 180.8/d w obu oknach - nie sprawdzalem, czy to blad odczytu; LevyByPeople wylaczony, wiersz INFO.

## 2. Pora roku i zima [P]

| kwartal | pora (koniec) | dob zimy | ciecie plonu wsi, swiat % | zywnosc wsi swiata (model gry) |
|---|---|---:|---:|---:|
| d2-91 | lato | 0 | - | 1 156 |
| d92-182 | jesien (od d136) | 0 | - | 1 143 |
| d183-273 | jesien | 0 | - | 1 136 |
| d274-364 | jesien | 0 | - | 1 133 |
| d365-455 | jesien | 0 | - | 1 129 |
| d456-546 | **zima (od d460)** | 87 | 40.8 | 693 |
| d547-637 | zima | 91 | 40.8 | 674 |
| d638-728 | zima | 91 | 40.8 | 676 |

Kalendarz Westeros: kampania startuje w dniu 3 642 z 3 776 lata, jesien 324 dni, zima 1 316 dni (do d1775) [P]. Ciecie wedlug krain: Polnoc (battania) 56.3%, Reach 40.3%, Wolni Ludzie 62.5%, Volantis 15.9%, Qarth 15.4% [P].

## 3. Ludzie swiata i glod [P]

| kwartal | ludnosc mln (koniec) | z uchodzcami mln | uchodzcy poza domem | zmiana dobowa sr. | glodne warownie (max) | zmarli z glodu | C2 "brak pieniedzy" sr. | Food 0 warowni (max) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| start d1 | 52.134 | 52.134 | 0 | - | 0 | - | - | - |
| d2-91 | 51.962 | 51.985 | 22 874 | -1 911 | 0 | 0 | 0.0 | 0 |
| d92-182 | 51.814 | 51.877 | 62 574 | -1 625 | 0 | 0 | 0.1 | 0 |
| d183-273 | 51.713 | 51.811 | 98 027 | -1 115 | 0 | 0 | 0.4 | 0 |
| d274-364 | 51.600 | 51.722 | 122 409 | -1 243 | 0 | 0 | 0.2 | 0 |
| d365-455 | 51.515 | 51.675 | 159 905 | -927 | 0 | 0 | 1.5 | 0 |
| d456-546 | 51.594 | 51.741 | 147 301 | +866 | 0 | 0 | 1.9 | 0 |
| d547-637 | 51.612 | 51.728 | 116 818 | +192 | 0 | 0 | 2.7 | 0 |
| d638-728 | 51.618 | 51.708 | 90 056 | +71 | 0 | 0 | 3.5 | 0 |

- Rok 1: -1.02% (z uchodzcami -0.79%); rok 2: +0.03% (z uchodzcami -0.03%) [P]. Prog projektu "zmiana w roku -1.0% +- 0.5 pkt": rok 1 TAK, rok 2 poza widelkami w strone mniejszych strat [P]. Porownanie rok 1: W43 51.532 mln, GL-rok 51.649 mln w d364 [P].
- Zmarli i uciekli z glodu (spichlerze-regiony.csv, cale 728 dob): 0 / 0 [P]; glod okregu ponad prog 0 [P]. "Brak pieniedzy na jedzenie" (C2: Afford < 0.95 przez 7 dob) rosnie z 0.2 do 3.5 osad srednio [P].
- Zapas warowni: mediana 565 dni (d364) -> 1 012 dni (d728), ponizej 30 dni 0 [P].

## 4. Plon wsi wedlug ludzi (Reach : Polnoc) [P]

Z `plon-wsi.csv` (plon faktyczny towaru glownego wsi typu wheat_farm, probki tygodniowe; krolestwo = wlasciciel dzis; kultura = kultura startowa wsi).

| kwartal | Reach: plon / ludzie na wies (tys.) | Polnoc: plon / ludzie | stosunek plonu | stosunek ludzi | plon : ludzie | reach : battania wg kultury | korelacja plon ~ ludzie w typie (wazona) |
|---|---|---|---:|---:|---:|---:|---:|
| d1-91 | 144.8 / 178 | 39.8 / 50 | 3.64 | 3.53 | 1.03 | 3.87 | 0.946 |
| d92-182 | 134.6 / 167 | 39.9 / 51 | 3.37 | 3.27 | 1.03 | 3.89 | 0.942 |
| d183-273 | 133.8 / 167 | 40.3 / 51 | 3.32 | 3.24 | 1.03 | 3.86 | 0.944 |
| d274-364 | 134.2 / 166 | 40.2 / 51 | 3.34 | 3.24 | 1.03 | 3.87 | 0.930 |
| d365-455 | 132.8 / 166 | 40.6 / 51 | 3.27 | 3.24 | 1.01 | 3.84 | 0.906 |
| d456-546 | 99.4 / 167 | 22.5 / 51 | 4.41 | 3.24 | 1.36 | 5.21 | 0.873 |
| d547-637 | 91.5 / 167 | 19.7 / 51 | 4.64 | 3.24 | 1.43 | 5.34 | 0.901 |
| d638-728 | 90.7 / 167 | 19.9 / 51 | 4.56 | 3.24 | 1.41 | 5.29 | 0.882 |

- Baza modelu (linia GL1, przed zima i regulatorem): Reach 114.4-114.5, Polnoc 35.3 szt./d na wies -> 3.24 przy ludziach 3.24, plon / ludzie 1.000-1.001 w d364 i d728 (GL1-2 TAK) [P]. Wzor gry dawalby 51.8 / 50.0 (1.04) [P].
- Odchylenie w zimie to klimat, nie regula: (1 - 0.403) / (1 - 0.563) = 1.37 x 3.3 = ok. 4.5 [S]. Korelacja plonu z ludzmi w typie spada w zimie z 0.93-0.95 do 0.87-0.90 z tego samego powodu [S] (prog GL1-3 >= 0.95 liczy narzedzie na calym oknie: 0.976 rok 1, 0.973 calosc - TAK [P]).

## 5. Dochod rodow z ziemi wedlug ludzi - regresja po krolestwach (projekt rozdz. 8 "progi biegu rocznego") [P]

y = `d_ziemia` krolestwa (srednia 28 dob konczacych kwartal, `budzet-rodow.csv`), x = ludzie i osady (twierdze + wsie, `ludzie-regiony.csv` w ostatniej dobie okna); waga = beta standaryzowana (regresja z wyrazem wolnym). Metoda sprawdzona na W43: waga ludzi 0.15-0.27, osad 0.77-0.86 - zgodnie z rozpoznaniem (0.15 / 0.87) [P].

| koniec kwartalu | krolestw | **waga ludzi** | waga osad | R2 | tylko >= 300 tys. ludzi (ludzie / osady) | p90/p10 ziemi na 1000 ludzi (krol. > 400 tys.) | ziemia swiata tys. zl/d |
|---|---:|---:|---:|---:|---|---:|---:|
| d91 | 29 | 0.63 | 0.37 | 0.83 | 0.62 / 0.37 | 2.9 | 147.3 |
| d182 | 29 | 0.66 | 0.34 | 0.86 | 0.66 / 0.36 | 2.7 | 137.5 |
| d273 | 30 | 0.81 | 0.21 | 0.94 | 0.81 / 0.22 | 2.5 | 146.7 |
| d364 | 29 | 0.70 | 0.32 | 0.91 | 0.70 / 0.34 | 3.3 | 147.9 |
| d455 | 29 | 0.70 | 0.32 | 0.91 | 0.70 / 0.34 | 2.9 | 151.1 |
| d546 | 30 | 0.52 | 0.38 | 0.70 | 0.48 / 0.38 | 4.3 | 206.2 |
| d637 | 32 | 0.40 | 0.54 | 0.78 | 0.37 / 0.56 | 4.4 | 149.0 |
| d728 | 35 | 0.62 | 0.33 | 0.79 | 0.58 / 0.34 | 4.2 | 143.3 |

Porownanie, ten sam rachunek [P]:

| koniec kwartalu | W43 (bez reguly): ludzie / osady, p90/p10 | GL-rok: ludzie / osady, p90/p10 |
|---|---|---|
| d91 | 0.26 / 0.78, 5.4 | 0.56 / 0.47, 3.1 |
| d182 | 0.15 / 0.86, 5.1 | 0.56 / 0.43, 3.5 |
| d273 | 0.27 / 0.77, 4.1 | 0.52 / 0.51, 3.0 |
| d364 | 0.20 / 0.79, 8.5 | 0.60 / 0.44, 2.9 |

- Progi projektu na koniec roku 1: waga ludzi >= 0.6 - **TAK (0.70)**, waga osad <= 0.4 - TAK (0.32), p90/p10 <= 3 - NIE na styk (3.3) [P]. Kierunek "rosnie z kwartalu na kwartal i > 0.3": waga przekroczyla 0.6 juz w pierwszym kwartale, wiec nie rosla, tylko stoi wysoko; regula dziala [S].
- Rok 2: waga ludzi spada w zimie (0.52, 0.40), na koniec 0.62; p90/p10 4.2-4.4 [P]. Przyczyna [S]: zima tnie plon wedlug krain (Polnoc 56%, poludnie 15%), wiec dochod z ziemi zalezy w roku 2 tez od klimatu; do tego nowe krolestwa buntownikow (29 -> 35 krolestw [P]) z malym dochodem (Emeros Conspiracy 0.8 zl na 1000 ludzi [P]).
- Renta zaplacona na 1000 ludzi, p90/p10 krain (linia "Ludnosc", krainy = kultury; moja rekonstrukcja miary - [S] co do zgodnosci z projektem): d364 18.4, d728 27.1; W43 14-18 [P]. Prog projektu <= 4 - NIE; gospodarka od ludzi renty nie zmienia (zadna paczka jej nie dotyka) [S].

## 6. Sciana ludu i chleb swiata

### 6.1 Linia dobowa "Kiesa ludu" (swiat i miasta m_t > 1.5) [P]

| kwartal | stac pelny koszyk, swiat % | miast ze stac jedzenie < 95% sr. | zjedzone bez zaplaty % | miasta m_t > 1.5: ponizej 60 dni (z 20) | podzamcza stac % |
|---|---:|---:|---:|---:|---:|
| d2-91 | 100.0 | 0.0 | 0.01 | 12.4 | 100.0 |
| d92-182 | 99.4 | 0.3 | 0.00 | 11.5 | 100.0 |
| d183-273 | 97.6 | 0.8 | 0.03 | 12.6 | 100.0 |
| d274-364 | 97.1 | 1.1 | 0.14 | 15.5 | 100.0 |
| d365-455 | 95.7 | 1.2 | 0.29 | 15.9 | 97.1 |
| d456-546 | 95.3 | 2.0 | 0.25 | 16.3 | 98.3 |
| d547-637 | 92.3 | 3.9 | 0.40 | 15.1 | 98.4 |
| d638-728 | 90.9 | 4.2 | 0.51 | 17.3 | 97.9 |

Okna progu P1-6 (srednia 64 dob) [P]: d301-364 swiat 97.1%, miast < 95% 1.2 (max 2), bez zaplaty 0.19%; **d665-728 swiat 89.7%, miast < 95% 4.4 (max 8), bez zaplaty 0.56%**. Prog: swiat >= 98% - NIE w obu latach; miast <= 10 i bez zaplaty <= 2.5% - TAK w obu.
Porownanie d274-364: W43 97.6% / 8.2 miast / 2.05%; GL-rok 94.8% / 7.8 / 2.01% [P] - wersja koncowa ma w roku 1 mniej miast z klopotem niz oba biegi roczne.

### 6.2 Tabela tygodnia GL2 (ludne miasta m_t > 1.5 razem i krolestwa; dni oszczednosci i "stac na koszyk") [P]

| kwartal | dni oszczednosci sr. | min | stac na koszyk % | korelacja wydatkow ludu z ludzmi rynku | Reach dni / stac % | Volantis dni | Braavos dni | Qarth dni |
|---|---:|---:|---:|---:|---|---:|---:|---:|
| d2-91 | 42 | 26 | 100 | 0.84 | 40 / 100 | 47 | 33 | 44 |
| d92-182 | 51 | 48 | 99 | 0.76 | 75 / 97 | 53 | 25 | 65 |
| d183-273 | 49 | 40 | 97 | 0.72 | 69 / 87 | 57 | 17 | 62 |
| d274-364 | 34 | 27 | 94 | 0.75 | 52 / 83 | 47 | 11 | 42 |
| d365-455 | 31 | 26 | 94 | 0.73 | 42 / 87 | 44 | 10 | 42 |
| d456-546 | 27 | 25 | 92 | 0.75 | 36 / 84 | 37 | 7 | 41 |
| d547-637 | 25 | 23 | 89 | 0.73 | 50 / 83 | 16 | 3 | 50 |
| d638-728 | 19 | 14 | 87 | 0.66 | 26 / 83 | 15 | 2 | 32 |

- Ostatnia tabela (d729): miasta m_t > 1.5 stac 71%, 15 dni; Reach stac 44%, Braavos 65% (2 dni), Volantis 77% (10 dni) [P].
- GL-rok (bez reszty kiesy, P2, zbytkow) mial w d274-364 13 dni i 88% [P] - wersja koncowa poprawila rok 1 (34 dni, 94%), ale w roku 2 spada do poziomu GL-rok [P].

### 6.3 Miasta, ktorych lud nie stac na pelny koszyk (kiesa-ludu.csv, ostatnia probka kwartalu) [P]

| doba | miast < 0.95 | z tego jedzenie < 0.95 | najnizsze (stac pelny) |
|---|---:|---:|---|
| 182 | 1 | 1 | Oldtown 0.75 |
| 364 | 2 | 2 | Oldtown 0.67, Rhyos 0.70 |
| 455 | 2 | 2 | Rhyos 0.31, Oldtown 0.78 |
| 546 | 2 | 2 | Oldtown 0.64, Aquos Dhaen 0.69 |
| 637 | 5 | 4 | Rhyos 0.34, Oldtown 0.63, Braavos 0.70, Selhorys 0.78, Tyrosh 0.82 |
| 728 | 7 | 7 | Highgarden 0.33, Aquos Dhaen 0.45, Oldtown 0.52, Volon Therys 0.60, Braavos 0.62, Rhyos 0.68, Selhorys 0.74 |

Oldtown jest ponizej od d183 do konca (kiesa W ok. 8-12 tys. zl przy 215 tys. w d1-91) [P]; Braavos od d547 (kiesa W 245 tys. -> 5 tys.) [P].

## 7. Kiesa ludu i danina (z progiem alarmu 2 z rozdz. 7.6 rachunku 85 tys.) [P]

| okno 28 dob | danina tys. zl/d | kiesy ludu miast mln | lokata W mln | krolestw z danina | krolestw w pokoju |
|---|---:|---:|---:|---:|---:|
| d337-364 | 419.1 | 20.80 | 16.18 | 25.8 | 3.0 |
| d421-448 | 392.0 | 19.72 | 15.02 | 25.0 | 3.0 |
| d449-476 | 310.5 | 20.42 | 15.60 | 18.2 | 10.3 |
| d477-504 | 282.8 | 22.22 | 17.42 | 15.0 | 14.0 |
| d505-532 | 189.8 | 23.94 | 19.31 | 11.6 | 19.1 |
| d533-560 | **152.5** | 26.42 | 21.89 | 8.4 | 20.0 |
| d561-588 | 179.0 | 28.50 | 24.10 | 11.2 | 18.9 |
| d589-616 | 272.4 | 28.85 | 24.60 | 15.1 | 16.2 |
| d645-672 | 267.7 | 28.19 | 24.02 | 13.0 | 17.2 |
| d673-700 | 221.1 | 28.37 | 24.26 | 11.4 | 18.8 |
| d701-728 | 210.2 | 28.87 | 25.01 | 15.7 | 17.1 |

- Kwartaly (danina tys. zl/d): 64 / 222 / 355 / 454 | 395 / 237 / 226 / 236 [P]; srednia roku 1 274, roku 2 273 [P]; d301-364 447.9, d665-728 221.2 [P].
- **Alarm 2** ("kiesy < 18 mln albo danina < 170 tys. zl/d w 2 oknach"): kiesy najnizej 19.72 mln (d421-448) - nie; danina ponizej 170 w 1 oknie (d533-560: 152.5) - **alarm nie wlaczony, ale na granicy** [P]. Przyczyna spadku daniny: pokoj (krolestw z danina 25-27 -> 8-16), nie puste kiesy (kiesy rosna od d449) [P].
- Kiesy ludu W+R+P (`kiesa-obieg.csv`, jak w krytyce): d337-364 23.85 mln, minimum 22.54 (d421-448), d701-728 34.08 mln [P]; W43 w tym samym miejscu 25.1 mln [P].
- Lokata W d728 25.08 mln, z tego w krolestwach w pokoju 16.61 mln (Dothrakowie 4.14, Dolina 3.20, Dorne 2.11, Targaryen 1.58) [P].
- Robocizna 11c (lud od skupu plonu wsi): 25.1 tys. zl/d w d274-364, 21.0 w d638-728; skup kas od taborow wsi 252 -> 211 tys. zl/d (zima) [P].

## 8. Towary: len, plotno, sol, drewno, ruda (zelazo), narzedzia, zboze (szt./d, srednie) [P]

| miara | GL-rok d2-364 | rok 1 d2-364 | rok 2 d365-728 | d337-364 | d701-728 |
|---|---:|---:|---:|---:|---:|
| len - produkcja | 872 | 859 | 718 | 940 | 647 |
| plotno - produkcja | 277 | 273 | 239 | 314 | 204 |
| sol - produkcja | 1 065 | 1 054 | 948 | 1 152 | 880 |
| drewno - produkcja | 2 082 | 2 093 | 2 218 | 2 196 | 2 206 |
| ruda zelaza - produkcja | 267 | 252 | 233 | 258 | 230 |
| narzedzia - produkcja | 106 | 129 | 171 | 138 | 182 |
| zboze - produkcja | 9 673 | 9 466 | 7 177 | 9 436 | 6 532 |
| miast bez plotna (z 97) | 79.6 | 79.9 | 76.4 | 74.9 | 76.7 |
| miast bez lnu | 48.9 | 49.6 | 49.5 | 46.9 | 53.7 |
| miast bez drewna | 42.7 | 37.9 | 34.6 | 34.6 | 34.2 |
| miast bez rudy (linia 174b) | 15.6 | 14.1 | 5.1 | 6.8 | 5.0 |

Szczegoly [P]:
- **Len**: zapas swiata 13.7 tys. (d364) -> 8.5 tys. (d728), na polkach miast 7.4 -> 3.3 tys.; zuzycie = produkcja; regulator lnu na suficie x1.30 (GLB-P5-3); na x1.0 regulatora 709 szt./d w roku 1 (TAK), 628 na calosci (NIE, prog 680) - zima. Bieg 43 bez paczki: 399 szt./d na x1.0.
- **Plotno**: produkcja 204-314 szt./d, zuzycie rowne; na polkach CALEGO swiata 70-207 sztuk; indeks ceny mediana 9.3-10 (sufit). Mieszczanie biora 123-202/d, wsie 24-55/d. Waskie gardlo: tkalnie (warsztaty 28/d + rzemioslo 180/d w d665) [P], nie len (len lezy w miastach: 3-7 tys. szt.) [S].
- **Sol**: wsie kupuja 854-1 131 szt./d z 888-1 156 wyprodukowanych; na polkach miast 25-277 szt.; zapas wsi 3.1-3.2 dnia zakupow (cel projektu >= 14) - GLB-P5-4 INFO.
- **Drewno**: budowy 1 172-1 641 szt./d (61-73% zuzycia); zapas 14.2 tys. (d91) -> 22.2 tys. (d728) = 12 dni zuzycia (projekt 30-60) [P].
- **Ruda zelaza**: produkcja 215-266/d, zuzycie rowne, zapas 6-9 tys.; miast bez rudy 35 (d1-91) -> 5 (rok 2).
- **Narzedzia**: wsie kupuja 35-88/d (prog >= 42 od kwartalu 2 - TAK), na polkach miast 124-215 szt.; polki ponizej 5 szt. w 78-86 z 97 miast.
- **Zboze**: zima -31% produkcji; na polkach miast 44.7 tys. (d91) -> 8.9 tys. (d728); mieszczanie 5 224 -> 2 336 szt./d [P].
- **Welna**: 303-370 szt./d rok 1, 264-268 rok 2 (zima) [P].

## 9. Qarth - jedzenie na polkach, stan ludu, dowoz [P]

Krolestwo Qarth w tabeli GL2 (3 miasta, od d638 2 + Qarth u buntownikow "Emeros Conspiracy"):

| kwartal | jedzenie na polkach Qarthu: sr. / min (dob spozycia) | Volantis min | Braavos min | Reach min | Polnoc min |
|---|---|---:|---:|---:|---:|
| d2-91 | 4.3 / 2.1 | 6.2 | 6.0 | 8.8 | 6.9 |
| d92-182 | 4.9 / 3.2 | 6.8 | 6.0 | 12.8 | 6.8 |
| d183-273 | 4.0 / 1.1 | 5.9 | 5.4 | 11.5 | 6.2 |
| d274-364 | 4.4 / 1.9 | 7.7 | 5.6 | 9.9 | 6.0 |
| d365-455 | 4.7 / 2.3 | 5.8 | 4.8 | 9.3 | 5.3 |
| d456-546 | 4.1 / 2.7 | 6.7 | 2.1 | 8.5 | 2.7 |
| d547-637 | 3.5 / 1.2 | 7.6 | 4.8 | 7.6 | 1.7 |
| d638-728 | 3.7 / 1.8 | 5.4 | 3.5 | 4.9 | 1.4 |

Miasto Qarth (kiesa-ludu.csv i ludzie-regiony.csv):

| kwartal | wlasciciel | kiesa W tys. | stac jedzenie / pelny | zapas jedzenia warowni sr. / min | bilans zywnosci warowni /d |
|---|---|---:|---|---|---:|
| d1-91 | Qarth | 244 | 1.00 / 1.00 | 37 988 / 37 520 | +45.6 |
| d183-273 | Qarth | 530 | 1.00 / 1.00 | 36 065 / 35 386 | +6.5 |
| d274-364 | Qarth | 431 | 1.00 / 1.00 | 34 348 / 33 793 | -13.8 |
| d365-455 | Qarth | 340 | 1.00 / 1.00 | 32 184 / 30 988 | -30.5 |
| d547-637 | Qarth | 247 | 1.00 / 1.00 | 32 798 / 32 415 | +4.3 |
| d638-728 | Emeros Conspiracy | 156 | 1.00 / 1.00 | 32 688 / 32 386 | -0.1 |

- Prog GL2-6 (>= 7 dob na polkach) - NIE przez caly bieg (Qarth min 1.1-3.2) [P]; lud Qarthu nie glodnieje i stac go na koszyk caly czas [P]; kiesa ludu Qarthu spada od d273 (530 -> 156 tys.) [P].
- **Dowoz do Qarthu: nie wiem** - log nie ma miary dowozu do Qarthu ani wywozu z Reach (GL2-7 "brak miary w logu") [P]. Posrednio: zapas warowni stoi, bilans ok. 0 [P].

## 10. Budowy, roboty ludu (P2) i odbudowa po lupach [P]

| kwartal | poziomy ukonczone (czolo kolejki) | budowy stoja sr. (z 227) | wydane na budowy tys. zl/d | lud zaplacil zl/d | poziomy ludu w kwartale | lupow (z pozarem) | spalone poziomy | odbudowane ze zgliszcz | zgliszcza na koniec: osad / poziomow |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| d2-91 | 12 | 138 | 22.3 | 3 560 | 6.0 | 4 | 17 | 2 | 4 / 15 |
| d92-182 | 35 | 140 | 19.7 | 7 994 | 10.5 | 5 | 28 | 4 | 9 / 39 |
| d183-273 | 50 | 153 | 18.8 | 8 134 | 11.0 | 1 | 8 | 2 | 10 / 45 |
| d274-364 | 53 | 156 | 23.4 | 6 209 | 8.3 | 4 | 17 | 11 | 13 / 51 |
| d365-455 | 42 | 165 | 19.5 | 6 971 | 9.2 | 3 | 20 | 6 | 16 / 65 |
| d456-546 | 55 | 139 | 25.2 | 6 433 | 8.6 | 1 | 3 | 11 | 16 / 57 |
| d547-637 | 55 | 123 | 26.1 | 5 049 | 6.6 | 0 | 0 | 8 | 14 / 49 |
| d638-728 | 56 | 129 | 26.5 | 4 916 | 6.4 | 3 | 15 | 10 | 13 / 54 |

- Rok 1: 150 poziomow, lud doplacil rownowartosc 35.8 poziomu [P]; rok 2: 208 poziomow, lud 30.8 [P] (P2-3: 40 i 35 na rok przy progu 40-90; W43 bez P2: 131 na rok; GL-rok 157) [P].
- Najwiecej poziomow ludu na osade 2.5 na rok (Riverrun), pulap 6.1 - TAK [P]; osad bez materialu 181-210 sr. [P] - lud chce budowac, ale nie ma czym (narzedzia i material na polkach) [S].
- Odbudowa po lupach: rok 1 spalone 70, odbudowane 19; rok 2 spalone 38, odbudowane 35 [P]. Cywilne budowy czekajace (front albo pan bez luzu zoldu) 0.1 -> 5.7 [P].

## 11. Zbytki (P3-P5) - ile oferty ludu znalazlo towar [P]

| kwartal | oferta tys. zl/d | kupione tys. zl/d | kupione % | sukno i odziez: oferta / kupione | bez kategorii (zostaje w kiesie) |
|---|---:|---:|---:|---|---:|
| d2-91 | 223.6 | 77.6 | 34.7 | 123.0 / 60.6 | 63.5 |
| d92-182 | 396.5 | 110.7 | 27.9 | 218.1 / 84.0 | 112.3 |
| d183-273 | 439.8 | 112.2 | 25.5 | 241.9 / 85.7 | 127.0 |
| d274-364 | 319.5 | 93.8 | 29.4 | 175.7 / 71.9 | 90.7 |
| d365-455 | 261.8 | 88.6 | 33.8 | 144.0 / 66.7 | 70.6 |
| d456-546 | 310.2 | 80.0 | 25.8 | 170.6 / 59.2 | 86.8 |
| d547-637 | 406.7 | 85.7 | 21.1 | 223.7 / 62.8 | 117.0 |
| d638-728 | 412.7 | 76.5 | 18.5 | 227.0 / 54.8 | 118.0 |

- Rok 1 28.6%, rok 2 23.8%, d701-728 18.7% (prog ZB-2 >= 50%) [P].
- Wedlug kategorii d301-364 (P1-13b): plotno - oferta 95.7, kupione 18.8, bez wiersza na polce 62.2 tys. zl/d; skora 19.1 / 4.6 / 9.6; filc 24.5 / 22.6 / 0.3 [P]. Plotno to 2/3 niezaspokojonej oferty [P].
- Podzamcza i wsie na targu (P5, d701-728): podzamcza chciane 82.4 tys., kupione 4.0 tys.; wsie chciane 235.9 tys., kupione 11.6 tys. zl/d [P].

## 12. Pieniadz swiata - blad licznika podatku wsi (rok 2) [P]

| doba | suma "Pieniadz swiata" | skok | pozycja | zloto glow rodow |
|---|---:|---:|---|---:|
| 698 | 206 839 330 | - | - | 69 380 577 |
| 699 | 2 354 328 538 | +2 147 489 208 | "liczniki: podatek wsi" 2 147 541 739 (+2 147 474 953) | 69 368 218 |
| 707 | 206 928 685 | -2 147 402 451 | licznik wyzerowany, w bilansie "z utargu wsi zniklo 2 147 483 430" | 69 231 263 |
| 709 | 2 354 412 757 | +2 147 488 490 | jak d699 | 69 070 868 |
| 716 | 207 008 595 | -2 147 416 250 | jak d707 | 68 555 231 |
| 725 | 4 501 975 670 | +4 294 965 384 | "podatek wsi" 4 295 000 438 | 69 369 220 |
| 728 | 4 502 004 898 | - | zostaje do konca biegu | 69 501 862 |

- Skok ~2^31 i ~2^32 w liczniku "zloto w drodze do panow: podatek wsi" przy powrocie taboru wsi ("pan (licznik podatku wsi) 2 147 561 580 (837 507%)") [P]; glowy rodow i skarbce bez skoku - prawdziwe zloto sie nie pojawilo [P]. Licznik "zerowal sie" w d707 i d716 przy powrocie "bez podatku BK: Tanos" (ROT_castle31_village1) - podejrzana wies Tanos [S]. Przepelnienie liczby 32-bitowej [S]. Bez tego P1-5c roku 2 jest w progu (rok 1: 0.015%, najgorsze 0.060%) [P dla roku 1; S dla roku 2].
- Ryzyko [S]: jesli gra odda ten licznik panu (odbior podatku wsi), rod dostanie 2-4 mld zl. Do sprawdzenia w kodzie (Armoury, liczniki utargu wsi / BK TradeTaxAccumulated) - bez zmian w tym zadaniu.

## 13. Konie robocze (k13a) i ksiegi m6b [P]

| kwartal | stado robocze (koniec) | kupione /d | czeka: pusta polka (osad sr.) | czeka: za mala kiesa (osad sr.) |
|---|---:|---:|---:|---:|
| d2-91 | 74 310 | 12.6 | 0.00 | 0.00 |
| d183-273 | 73 947 | 13.6 | 0.02 | 0.00 |
| d274-364 | 73 795 | 13.4 | 0.05 | 0.00 |
| d365-455 | 73 677 | 13.5 | 0.42 | 1.19 |
| d547-637 | 73 840 | 13.3 | 0.13 | 1.64 |
| d638-728 | 73 863 | 13.5 | 0.92 | 2.84 |

- Kupione 9 737 w 728 dobach (13.4/d; plan 13.6/d), stado d1 74 564 -> d728 73 863 (x0.991), skokow 0, kontrola licznika 0 [P]. Ludowi coraz czesciej brakuje na konia ("za mala kiesa" 0 -> 2.8 osad) [P].
- KR-8: stos koni bojowych 174-3 397 dni zuzycia w roku 2 (rok 1: 65-85 dni) [P] - w pokoju konie bojowe sie nie zuzywaja [S].
- Nowe karawany bez jucznych 17% (rok 1 14%) [P]; juczne bez wyjasnienia -32/d; ryby bez wyjasnienia +41.7/d (rok 1) -> ok. +100/d (rok 2: (50 397 - 13 930) / 364) [P].

## 14. Wojsko - tylko kontekst (szczegoly: raport `wojsko`)

W5-7 (druzyny lordow krolestw w wojnie): d337-364 108.9 tys., d701-728 **55.3 tys.** (prog 85) [P]; partie rodow 115.3 tys. (d364) -> 57.5 tys. (d546) -> 69.0 tys. (d728) [P]. Gospodarczo zbiega sie to z pokojem 16-20 krolestw (miara liczy tylko krolestwa w wojnie) i z zima [S] - nie rozstrzygam.

## 15. Czego nie wiem

- Dowozu zywnosci do Qarthu i wywozu z Reach - brak miary w logu.
- Czy licznik podatku wsi przepelnia sie tylko w Tanos i czy kiedykolwiek zostalby wyplacony panu - wymaga kodu (nie w tym zadaniu).
- Czy spadek "waga ludzi" w d546-637 to tylko zima, czy tez wymiana krolestw (bunty) - rozbicie wymagaloby regresji z kontrola krainy klimatu.
- Ceny zywnosci w zimie (nie wyciagalem) - wiec nie wiem, ile z biedy wielkich miast to drozsze jedzenie, a ile mniejszy zarobek (robocizna 242 -> 198 tys. zl/d [P]).
- GLB-P4-3: identyczne 180.8/d w obu oknach - nie sprawdzilem, czy to blad odczytu narzedzia.
- Renta p90/p10 - moja rekonstrukcja miary (krainy = kultury), nie wiem, czy tak liczyl projekt ("dzis ok. 10" vs moje W43 14-18).

## 16. Pliki (`...\scratchpad\dwuletni\gospodarka\`)

- `RAPORT.md` - ten raport.
- Wyniki narzedzia: `{gl1,gl2,glb,kiesa1,zbytki,p2bud,k13a,m6b}-{364,728}.txt`.
- Skrypty (`skrypty\`): `wyciag_log.py` (jedno przejscie po logu -> `w728.json`, `w43.json`, `wgl.json`), `analiza.py` (tabele kwartalne -> `tab728.md`, `tab43.md`, `tabgl.md`), `regresja_ziemia.py` (-> `regresja.txt`, `regresja_*.json`), `plon_kwartaly.py` (-> `plon_kwartaly.md`), `qarth.py` (-> `qarth.md`), `k2_lud.py` (kopia skryptu krytyki -> `k2_dwuletni.txt`), `prefiksy.py`, `przyklady.py` (pomocnicze).
- Polecenia: `python -I skrypty\wyciag_log.py <log> w728.json`; `python -I skrypty\analiza.py w728.json 728`; `python -I skrypty\regresja_ziemia.py <Logs\2026-10-13_21-14-13> 108836 728 dwuletni`.
