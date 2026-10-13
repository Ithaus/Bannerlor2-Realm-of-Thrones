# WERYFIKACJA rozdzialu "GOSPODARKA TOWAROWA I LUDZIE MIAST" (plik analityka: gospodarka.md)

Praca tylko do odczytu. Nic nie uruchamialem, nic nie zmienialem.
Uwaga techniczna: w zadaniu nazwa pliku miala niepodstawiony znacznik ${l.key}. Analityk zapisal pod kluczem "gospodarka", ja pisze "gospodarka-weryfikacja.md".

## 0. Skad sa moje liczby

| Skrot | Co to jest |
|---|---|
| LOG | D:\Backup-Bannerlord\logs\2026-10-13-noc34-35\Armoury-2026-10-13_06-24-10.log; "L129205" = numer linii |
| LOG-S | poprzedni bieg roczny: Armoury-2026-10-13_02-15-21.log (ten sam katalog) |
| CSV | ...\Logs\2026-10-13_06-24-10\ (plik + kolumna) |
| [P] | pomiar - liczba wprost z logu albo tabeli |
| [S] | szacunek - moj rachunek z kilku pomiarow |

- Moje skrypty i wyniki: ...\raport-rok\skrypty\gospodarka-wer\ (w0..w23, pliki w*_wynik.txt). Liczylem od zera. Skrypty analityka czytalem dopiero po wlasnym wyniku i tylko tam, gdzie wyszla roznica (s4_handel.py).
- Kwartaly jak u analityka: Q1 = linie 1-91, Q2 = 92-182, Q3 = 183-273, Q4 = 274-364.
- Uwaga o dobach: linie logu maja dwa znaczniki. "Pieniadz swiata" i "Kiesa ludu" koncza sie na dniu 109199, "Korona" i "Zold" na 109200. To ta sama ostatnia doba. Roznice o 1 dobe nie zmieniaja zadnego wniosku.

## 1. Liczby analityka policzone od nowa

| Nr | Liczba analityka | Moja liczba | Zrodlo | Zgodne? |
|---|---|---|---|---|
| 1 | Zloto swiata 207.93 -> 208.34 mln (doba 30 -> 364) | 207 926 622 -> 208 335 095 [P]. Od doby 1: 208 204 213 -> 208 335 095 | LOG "Pieniadz swiata" L10804, L129205, L707 | TAK |
| 2 | Skarbce krolestw -27.46 mln | -27.46 mln (54.57 -> 27.11) [P]. Od doby 1: -30.82 mln. W Q4: -51.4 tys./d | jw. | TAK (liczba). Wniosek - patrz pkt 2.A |
| 3 | Glowy rodow -13.48 mln | -13.48 mln od doby 30 [P]. Od doby 1: -5.02 mln (52.25 -> 47.23). Od doby 182: -0.57 mln. W Q4: -3.2 tys./d | jw. | Liczba TAK, baza mylaca - pkt 2.A |
| 4 | Lud miast +33.09 mln | +33.09 mln od doby 30 [P]; od doby 1 +37.84 mln; w Q4 +75.1 tys./d | jw., pole "kiesy ludu: miasta" | TAK |
| 5 | Sakiewki zolnierzy +11.93 mln, "rosna o 36 tys./d" | +11.93 mln od doby 30 [P]. Ale tempo: Q1 +144 tys./d, Q2 +37 tys., Q3 +5.7 tys., Q4 -1.2 tys./d | LOG "Sakiewka ludzi" (wplynelo - wyszlo), 364 linie; stan 16.48 mln (linia 182) -> 16.89 mln (L128938) | Stan TAK. Tempo NIE - pkt 2.B |
| 6 | Miast, gdzie ludu nie stac na jedzenie: 0.9 (Q1), 9.1 (Q4) | 0.95 / 4.65 / 6.79 / 9.11; doba 364: 9 | LOG "Kiesa ludu", pole "miast ze stac jedzenie < 95%", L129238 | TAK |
| 7 | Zjedzone bez zaplaty 2 856 -> 12 201 zl/d; 2.54% w dobie 364 | 2 856 / 3 872 / 6 686 / 12 201; doba 364: 13 709 = 2.54% [P]. Q1 bez pierwszej doby: 1 287 | jw. | TAK |
| 8 | Miast bez rudy 44 (Q1), 7 (Q4); poprzedni bieg 55 na koniec | 44.1 / 19.2 / 9.1 / 6.9; doba 364: 8. Poprzedni bieg doba 364: 55 | LOG "Ceny surowcow" L129060; LOG-S L128830 | TAK |
| 9 | Woz surowcow dowiozl 42 101 szt. rudy = 77% calego dowozu | 42 101 to ruda KUPIONA i wyslana wozem [P]. Dojechalo wszystkich surowcow 99 512 z 101 531 szt. Udzial: 79% dowozu MIEDZY miastami; 31% gdy doliczyc wozy wsi z kopaln | LOG "Kontrakty surowca (174)" 364 linie, L129190; LOG "Karawany"; LOG "Towary: iron" pole "wozy do osad" | 42 101 TAK. 77% NIE - pkt 2.C |
| 10 | Plotno: 86-90 miast bez caly rok | Q1-4: 85.5 / 88.0 / 89.5 / 88.6; dobowo od doby 30: 83-93; doba 364: 89 | LOG "Ceny surowcow" | TAK |
| 11 | Korpus tier 1-2 na polkach 70 723; tier 3-4: 129; popyt t3 3 004, t4 2 182 | 70 723; 129; popyt 3 004 (brak 2 851), 2 182 (brak 2 061) [P] | LOG "Zbroja na polkach" L129174; warsztaty.log linia 1092 | TAK |
| 12 | Drewno 11 792 -> 152 862 ladunkow, +426 na dobe | 11 792 (dzien 108837) -> 152 862 [P]. "+426/d" to inna miara (stos miast d61-364, progi R1-9). Zapas swiata rosl: Q1 +72, Q2 +350, Q3 +517, Q4 +649 na dobe | LOG "Towary: hardwood" L909, L129019 | TAK, z uwaga: tempo przyspiesza |
| 13 | Budowy: stoi 198 z 227; 144 ponad 300 dni; 63 poziomy w roku | 198; 144 (mediana stania 342 dni); 63; wydane 4.50 mln | LOG "Budowy" L128995; budowy.log ostatni dzien | TAK |
| 14 | Karawan 556; 177 nie ma na zold; notable doplacaja 49 tys./d; stracili 28% | 556; 177 z 535 karawan notabli; niedoplata 49 479; kiesy notabli 30.53 -> 21.85 mln = -28.4% | LOG "Karawany (stan)" L129083; "Obieg: notable i karawany" L1092, L129216 | TAK (liczby). Wniosek - pkt 3 nr 7 |
| 15 | Podatek wojenny ludu ok. 15 tys./d; 20 z 26 krolestw na pulapie 3% | 14 908 zl w ostatniej dobie; Q4 srednio 14 922; w roku 3.88 mln. Na pulapie 20 z 26. Dob z choc jednym krolestwem na pulapie: 302 | LOG "Korona: podatek wojenny ludu (184)" L129100 | TAK |
| 16 | Budzet mieszczan 160 tys. d na dobe, da sie wydac 24% | 160 276 d i 38 289 d = 23.9% [P] - ale to 15 wybranych towarow, nie caly budzet. Caly budzet gry 975 626, wydane 454 101 = 47% | LOG "Ceny surowcow" L129060 (pola "budzet mieszczan"); LOG "Kiesa ludu" L129238 | TAK dla 15 towarow. Dla calego koszyka inna liczba |
| 17 | Korona bierze 59-65% gotowki wsi | Srednia dziennych procentow: 34.5 / 58.7 / 61.4 / 58.5. Z sum kwartalu: 33.3 / 57.1 / 58.9 / 56.4%. Ostatnia doba 65.1% | LOG "Gotowka wsi (E5/0)" L129218, 363 linie | TAK |
| 18 | Zold ok. 6.0 zl na zolnierza na dobe | 443 964 / 74 213 = 5.98 zl (ostatnia doba) [S]. Q1-4: 4.65 / 5.07 / 5.41 / 5.69 | LOG "Zold" L129132; "Odziez wojska (150)" L129133 | TAK |
| 19 | Dobrobyt miast +34.4%, zamkow +107%, ludzie -0.99% | +34.4% (464 917 -> 624 706); +106.7%; -0.99% | CSV kasy-miast.csv kol. dobrobyt; ludnosc-bk.csv; ludzie-regiony.csv kol. ludzie | TAK |
| 20 | Kasy miast < 1 000 zl: 38 dob (Thenn 30, Frostfang's Camp 9) | 38 dob, 39 miasto-dob (Thenn 30, Frostfang's Camp 9) | CSV kasy-miast.csv kol. kasa | TAK |
| 21 | Lokata ludu 29.49 mln; przybywa 68 tys./d w Q4 | 29 485 864 [P]. Lokata rosla: 84.9 / 94.5 / 77.3 / 68.3 tys./d; ostatnie 28 dob: 56.4 tys./d | LOG "Kiesa ludu", pole "lokata" | TAK, ale tempo spada |
| 22 | Kopalnie BK w miastach: 12 sztuk, stoja | 13 przez 273 doby, 7 w dobie 364. Pracuje 0 | LOG "Warsztaty towarowe" L128955 | NIE (drobne) - pkt 3 nr 13 |

Wynik: 22 liczby sprawdzone. 17 zgodnych bez uwag. 3 zgodne co do liczby, ale wniosek z nich za szeroki (nr 2, 3, 16). 2 obalone (tempo sakiewek nr 5, udzial 77% nr 9). 1 drobna pomylka (nr 22).

## 2. Co obalilem

### 2.A "Korony i panowie biednieja" - w wiekszosci to zaplanowane wydawanie zapasu startowego

- Kazde krolestwo startuje z ok. 2 mln (29 krolestw = 57.93 mln, L707) i z zasady wydaje co dobe 1/360 zapasu ponad 500 tys. (LOG "Korona: wplywy dnia (165)", pole "1/360 zapasu ponad 500000") [P].
- Suma tego pola w dobach 31-364: 22.13 mln = 81% spadku skarbcow (27.46 mln) [P, udzial S]. W calym roku 25.62 mln z 30.82 mln.
- W Q4: zaplanowane 41.6 tys./d, faktyczny spadek skarbcow 51.4 tys./d. Czyli prawdziwy niedobor koron to ok. 10 tys./d, nie 82 tys./d [S].
- To zrodlo gasnie samo: 106 tys./d w Q1, 35 tys./d w dobie 364 (L129101), w nastepnym roku srednio ok. 22 tys./d [S].
- Glowy rodow: -13.5 mln jest liczone od szczytu w dobie 30, zaraz po darze startowym (kasy miast 27.33 -> 10.85 mln w tych samych 30 dobach). Od doby 1: -5.0 mln. Od polowy roku stoja: 47.80 mln (doba 182) -> 47.23 mln (doba 364) [P].
- Co zostaje prawda: lud miast zbiera pieniadz caly rok i dalej (+75 tys./d w Q4), a place za to skarbce (-51 tys./d), notable (-13 tys./d), podzamcza (-7 tys./d) i Bank (-4 tys./d) [P, L96881 i L129205].

### 2.B "Zold krotszy o to, czego zolnierz nie wydaje = 36 tys. zl/d = 6 tys. ludzi" - OBALONE

- 36 tys./d to srednia z dob 30-364. Sakiewki napelnialy sie tylko w pierwszym polroczu.
- Wplynelo minus wyszlo: Q1 +144 483, Q2 +36 561, Q3 +5 687, Q4 -1 171 zl na dobe (LOG "Sakiewka ludzi", 364 linie) [P].
- W Q4 zolnierz wydaje wszystko, co dostaje (wplynelo 722 420, wyszlo 723 591 na dobe) [P].
- Poprzedni bieg pokazuje to samo: sakiewki 16.44 mln (doba 182) -> 13.70 mln (doba 364) (LOG-S "Pieniadz swiata") [P].
- Poprawna wartosc: trwale ok. 0 zl na dobe. Krotszy zold zabralby zolnierzowi to, co dzis wydaje w miastach i u markietanow (ok. 356 tys./d w Q4), czyli dochod ludu i kas miast.
- Zostaje tylko jednorazowy zapas: sakiewki partii rodow 11.04 mln = 24.9 dnia zoldu (LOG "Markietani" L129140) [P].

### 2.C "Woz dowiozl 77% calego dowozu rudy" - liczba zla z dwoch powodow

- Karawany sprzedaly miastom z brakiem 8 905 szt. rudy, nie 10 144. Skrypt analityka (s4_handel.py, wzorzec ".*?ruda") lapal slowo "ruda" z dalszej czesci linii ("kupione z nadwyzek"), gdy w liscie sprzedazy rudy nie bylo. Moj wzorzec czyta tylko liste sprzedazy [P].
- Udzial wozu w dowozie miedzy miastami: 42 101 / (42 101 + 8 905 + 2 171) = 79% [S].
- "Caly dowoz" to takze wozy wsi z kopaln do miast: 81 661 szt. w roku (LOG "Towary: iron", pole "przewoz: wozy do osad", suma) [P]. Z nimi udzial wozu kupcow to 31% [S].
- 42 101 to sztuki kupione. W ostatniej dobie 939 szt. rudy bylo jeszcze w drodze (L129190) [P].
- Wniosek analityka (woz dziala, zelazo naprawione) zostaje. Zla jest tylko liczba 77% i 10 144.

### 2.D Pozostale poprawki liczb

| Co napisal analityk | Dlaczego nie | Poprawna wartosc |
|---|---|---|
| "Oba razem ok. 89 tys. zl/d = ok. 15 tys. zolnierzy" | 36 tys. z sakiewek nie istnieje (2.B). Zostaje 53 tys. z ludu, a z tego czesc tylko zastapi gasnace wydawanie zapasu koron (35 tys./d w dobie 364) | Najwyzej 53 tys./d = ok. 9 tys. zolnierzy po 5.98 zl [S]; trwale mniej |
| "63% dochodu ludu to zolnierze" | 63% liczone od dochodu "z zewnatrz i z kas" (221 781). Caly dochod ludu miast to 519 339 (CSV kiesa-ludu.csv, suma kol. dochod) | 140 803 / 519 339 = 27% calego dochodu; 63% dochodu z zewnatrz [S] |
| "Przesunac rece platnerzy z tier 1-2 na tier 3-4" | Warsztaty juz robia glownie tier 3-5: w Q4 z 70.7 zbroi korpusu na dobe 51.2 to tier 3-5 (31.1 + 14.2 + 5.9). Zalew tier 1-2 to lup sprzedany przez zolnierzy, nie produkcja | Do przesuniecia zostaje najwyzej ok. 20 szt./d przy braku ok. 5.5 tys. szt. (LOG "Warsztaty"; warsztaty.log "najwiekszy brak", ostatnie 91 dob) [P] |
| "W podzamczach 12 razy wiecej niz na poczatku" | Porownanie sredniej Q1 (667) z jedna doba (8 140) | Q4 do Q1: 5 681 / 667 = 8.5 raza [S] |
| "Notable doplacaja 49 tys. zl na dobe" | To jedna z dwoch doplat. Druga: dopelnienie kas karawan do 5 000 = 54 833. Razem 104 tys./d. Tego samego dnia notable dostaja z aktywow 104 227 | Strata notabli netto: 21 tys./d srednio, 13 tys./d w Q4 (L129216, LOG "Pieniadz swiata") [P] |
| "Bez wyjasnienia +19 397 szt. = 0.2%, ksiega sie zamyka" | To wynik po zniesieniu sie plusow i minusow | Plusy +124 625 (zboze, ryby), minusy -105 228 (22 towary). Razem bez znaku ok. 230 tys. = 2.5-2.7% produkcji [P, udzial S]. Uwaga: 9.04 mln produkcji u analityka zawiera rozdanie startowe z doby 1; bez niego 8.55 mln (LOG linie "Towary:", suma pola produkcja) [P] |

## 3. Niespojnosci analityka - proba obalenia

| Nr | Tytul | Werdykt | Uzasadnienie |
|---|---|---|---|
| 1 | Pieniadz wojny osiada u ludu i zolnierzy, korony i panowie biednieja | CZESCIOWO | Lud: prawda, +33.1 mln i dalej +75 tys./d w Q4. Zolnierze: tylko pierwsze polrocze, w Q4 -1.2 tys./d. Panowie: liczeni od szczytu po darze startowym; od doby 182 stoja. Korony: 81% spadku to zaplanowane wydawanie zapasu startowego (pkt 2.A). Artefakt pierwszego roku w trzech z czterech czlonow. |
| 2 | Lud ma pieniadze, a nie ma czego kupic | PRAWDZIWA | Mocniejszy dowod niz podal analityk: lud oferuje na towar 4.49 mln dziennie z nadwyzki, a wydaje 454 tys. (L129238). Liczba 24% dotyczy tylko 15 towarow. W poprzednim biegu to samo (26.1%, LOG-S L128830), wiec to nie skutek rundy 3. |
| 3 | Plotna nie ma przez caly rok | PRAWDZIWA | 83-93 miasta bez od doby 30. Plotno powstaje (164 szt./d) i znika tego samego dnia (zuzycie 164, w tym mieszczanie 120; L129027). W 72 miastach przerob stoi z braku lnu (L128928). W poprzednim biegu tak samo (85-95 miast). |
| 4 | Zalew zbroi tier 1-2 przy braku tier 3-4 | CZESCIOWO | Fakt prawdziwy. Naprawa bledna: warsztaty juz robia tier 3-5 (72% zbroi korpusu w Q4). Zalew to lup. Hamulcem jest liczba rak platnerzy (bezczynne 2%), nie ich przydzial. |
| 5 | Dobrobyt rosnie o jedna trzecia w roku wojny | PRAWDZIWA | +34.4% miasta, +107% zamki. Rosnie rowno co kwartal (+8.0 / +8.5 / +7.7 / +6.5%), wiec to nie rozruch. Poprzedni bieg: +33.4%. Przyczyny nie ustalilem. Uwaga: wydatek ludu na jedzenie +33% moze isc z ceny zboza (indeks 1.01 -> 1.47), nie tylko z dobrobytu - analityk tego nie rozdzielil. |
| 6 | Ludu coraz czesciej nie stac na jedzenie | PRAWDZIWA | Rosnie caly rok w obu biegach. W tym biegu jest lepiej niz w poprzednim (Q4: 9.1 wobec 13.3 miasta). Prog B1-T7 mial to z gory jako "spodziewane NIE". "Wyspy i porty bez armii" to hipoteza - nie ma danych o wydatkach wojska wedlug miast. |
| 7 | Karawany nie zarabiaja na siebie | CZESCIOWO | "Kasa nie starczyla na zold" (177 karawan) to stan kasy tego dnia, nie rachunek zysku. Notable doplacaja 104 tys./d i dostaja z aktywow 104 tys./d. Strata netto 13-21 tys./d. Zysku karawan log nie liczy - wniosek szerszy niz dane. |
| 8 | Zalew drewna | PRAWDZIWA | I gorzej, niz napisano: przyrost przyspiesza do +649/d w Q4. Dwie przyczyny: las wsi daje wiecej (1 445 -> 1 861/d), budowy biora mniej (822 -> 591/d). W poprzednim biegu to samo (151 306). |
| 9 | Budowy cywilne stoja caly rok | PRAWDZIWA | To zamierzona regula (cywilne czekaja w wojnie), ale w tym swiecie wojna nie konczy sie nigdy: od doby 23 w wojnie jest 25-26 krolestw kazdej doby (L129100, pole "krolestw w wojnie"). W poprzednim biegu tak samo (198 stoi). |
| 10 | Wsiom nic nie zostaje po podatkach | ARTEFAKT (w czesci glownej) | "Wsi zostaje ok. zera" to zmiana stanu kiesy, a stan w rownowadze zawsze daje zero. Wsie maja 9.55 mln, mediana 8 769 zl (L129213). Danina korony to 2.5% dziennie z nadwyzki ponad 15 dni zakupow (kod KingdomTreasury.Levies, ustawienie LaySubsidyVillageShare) - zdejmuje nadwyzke, nie podstawe. Wsie kupuja malo, bo 478 nie ma czego kupic w swoim miescie, nie z braku pieniedzy. Prawdziwy zostaje tylko wzrost wsi ponizej 1 000 zl: 35 (doba 91) -> 73. Wniosek "nie podnosic we wsiach" zostaje. |
| 11 | Skory wyprawionej brakuje i jest gorzej | PRAWDZIWA | 33.4 -> 42.9 miasta bez. Wobec poprzedniego biegu nie gorzej (tam 46.5 w Q4), produkcja skory +30% (147 wobec 113 na dobe). |
| 12 | Zamki nie dostaja wyrobow ani luksusu | CZESCIOWO | 0.50 to wartosc startowa BannerKings dla kazdej osady (doba 1: miasta 0.503, zamki 0.500; L744). W zamkach nigdy sie nie ruszyla, bo nie ma tam zadnych zakupow tych kategorii. To nie jest pomiar niezadowolenia. |
| 13 | Kopalnie BK w miastach stoja caly rok | CZESCIOWO | Stoja, ale swiat sam je zamyka: 13 -> 7 w Q4, dokladnie 6 bankructw w dniach 109111-109195 (LOG "Warsztaty towarowe"). Analityk podal oba fakty osobno i ich nie polaczyl. |
| 14 | Dwa rachunki ludzi sie rozjezdzaja | CZESCIOWO | Liczby zgodne (-18.4% wsie, +45.1% zamki w BK; -1% w ksiedze). To dwa rozne modele z zalozenia. W poprzednim biegu tak samo (-15.2%, +45.6%). Nie ma dowodu, ze cokolwiek od tego zalezy. |
| 15 | Kasy Thenn i Frostfang's Camp przy zerze | PRAWDZIWA | 38 dob. Trop, nie dowod: oba naleza do 8 miast, ktore w ksiedze ludzi maja 0 mieszkancow osady (CSV ludzie-regiony.csv kol. ludzie_osada), a ich dobrobyt rosnie o 37-43%, wiec rosnie tez wymagany zapas kupcow (64 -> 87 tys.). Przyczyny nie znam. |
| 16 | Jedzenie poza ksiega | CZESCIOWO | Reszty sa prawdziwe (+342/d zboze i ryby, -289/d inne). "Jedna regula zamienia" nie trzyma sie: dobowo plusy i minusy nie ida w parze (korelacja 0.15). Zboze i ryby przybywaja co dobe, reszta ubywa skokami. To raczej dwie osobne sprawy. |
| 17 | Konie leza na polkach, a jazda czeka | ARTEFAKT dla tego rozdzialu | Liczby zgodne (9 423 wierzchowce, czeka 5 540). Progi K3-3 i K3-10 podaja powod: siodla i cena. To nie sprzecznosc gospodarki towarowej, tylko bramka wojska. |

## 4. Czego brakuje

1. **Zimy w tym biegu nie bylo.** Lato trwalo do doby 130, potem jesien na 432 dni (LOG L213 "Klimat: start - lato ... skonczy sie za 129 dni"; L45849 "nowa pora Autumn, potrwa 432 dni"). Pole "zima" w linii Spichlerze: NIE we wszystkich 364 dobach [P]. Zdanie "jedzenia nie brakuje" jest prawdziwe tylko dla lata i jesieni. Zima przyjdzie ok. doby 563 [S] i wedlug kodu (WinterBite.cs) tnie plon wsi o polowe. Zapas zboza w ksiedze towarow to 17 dni zuzycia (150 637 szt. przy 8 824 na dobe, L129017) i spada od doby 91 [P]. Analityk nie pisze o porze roku ani slowa.
2. **"Podatek na czas wojny" to w tym swiecie podatek staly.** Od doby 23 w wojnie jest 25-26 krolestw kazdej doby; w pokoju bylo jedno krolestwo przez jedna dobe [P]. Galaz "w pokoju stawka spada" nie zadzialala ani razu. Jeff pyta o podatek wojenny - trzeba mu to powiedziec.
3. **Nie sprawdzono, czy pieniadz ludu lezy tam, gdzie korona go potrzebuje.** Policzylem [S] z CSV kiesa-ludu.csv (ostatnia probka, nadwyzka ponad 14 dni wydatkow, razem 38.2 mln): Polnoc 13.5%, Krolewska Przystan 10.9%, Dothrakowie 7.6%. Za to Tyrosh 0, Lys 0, Pentos 6 tys., Braavos 82 tys., Norvos 188 tys., Dragonstone 357 tys. To wlasnie krolestwa z dlugiem i kredytem (L129100: Braavos dlug 661 983, Dragonstone 281 508). Im podatek od nadwyzki ludu nie pomoze. Na te 6 krolestw przypada 14.4% zwrotu zoldu swiata.
4. **Propozycja nie liczy, ze dzisiejsze wojsko zyje czesciowo z gasnacego zrodla.** Wyplaty koron dla rodow (renty + zwrot zoldu): 502 / 493 / 474 / 450 tys./d, ostatnia doba 410 tys. (LOG "Korona: wplywy dnia") [P]. Wojsko w partiach: 86.6 / 92.6 / 83.4 / 80.1 tys., ostatnia doba 74.2 tys. [P]. Nowy podatek najpierw zatrzyma ten spadek, dopiero potem doda ludzi.
5. **Tempo, na ktorym stoi propozycja, spada.** Przyrost lokaty: 94.5 -> 77.3 -> 68.3 tys./d, ostatnie 28 dob 56.4 tys./d [P]. W zamknietym obiegu lud zbiera tyle, ile inni traca. Gdy zapas koron sie skonczy, przyrost tez zmaleje. Trwale pewny jest zapas (lokata 29.5 mln), nie tempo.
6. **Brak porownania z zalozeniem projektu.** Prog B2-11 podaje, ze podatek wojenny ludu mial dawac 30-60 tys./d. Daje 15 tys. To najprostszy argument dla Jeffa, a w rozdziale go nie ma.
7. **Brak rachunku prostszej drogi.** Sam wyzszy pulap dzisiejszego podatku (od zarobku): 14 908 zl przy sredniej stawce 2.38% = ok. 6.3 tys. zl na kazdy punkt procentowy [S]. Pulap 6% dla wszystkich dalby ok. +15 tys./d, a uderzylby tez w 33 miasta z kiesa ponizej 14 dni. To potwierdza wybor analityka (podatek od nadwyzki), ale on tego nie policzyl.
8. **Los karawan bez rachunku.** Nowych karawan 1 733, stan urosl o 140 (416 -> 556), w bitwach padlo 554. Co sie stalo z ok. 1 040 pozostalymi - nie wiem i analityk tez nie pisze.
9. **Zalew drewna i stojace budowy to jedna sprawa.** Budowy biora o 231 ladunkow dziennie mniej niz w Q1. Analityk opisal obie rzeczy osobno.
10. **Liczby bez pewnego zrodla albo bledne:** 10 144 (blad wzorca, jest 8 905), "12 kopaln" (13 -> 7), "12 razy" (8.5 raza), "+426/d" przy zapasie swiata (to miara stosu miast).
11. **Skutki uboczne podatku od lokaty nie sa ocenione.** Lud wydaje z nadwyzki 15% dziennie na lepsze towary (ustawienie FolkSurplusSpendShare). Dzis to prawie nic nie kupuje, wiec podatek nie zaboli. Jesli kiedys dojdzie produkcja brakujacych towarow, podatek i zakupy zaczna sie bic o ten sam pieniadz. Analityk wspomnial o tym jednym zdaniem, bez liczby.

## 5. Co z tego zostaje dla pytania Jeffa o podatki

Potwierdzam [P]:
- Jedyne miejsce, gdzie pieniadz przybywa takze pod koniec roku, to lud miast: +75 tys. zl/d w Q4, 44.1 mln razem, z tego 29.5 mln ponad wlasny zapas.
- Lud miast oddaje dzis w podatkach ok. 8.7% dochodu (45 tys. z 519 tys. zl/d). Wsie oddaja koronie 56-59% gotowki i panu 32%.
- Dzisiejszy podatek wojenny ludu daje 15 tys. zl/d i stoi na pulapie 3% w 20 z 26 krolestw.
- Zolnierz w polu kosztuje 5.98 zl zoldu na dobe.
- Nie ma czego brac: we wsiach, w kasach kupcow (17 miast ponizej zapasu), u notabli (-28% w roku), w podzamczach.

Nie potwierdzam:
- Drugiej czesci propozycji (krotszy zold, 36 tys. zl/d). Tego pieniadza od polowy roku nie ma.
- Lacznej liczby "15 tys. zolnierzy wiecej". Z danych wynika najwyzej ok. 9 tys. [S], a trwale mniej, bo czesc nowego wplywu zastapi gasnace wydawanie zapasu koron.
- Ze podatek pomoze kazdemu krolestwu. Szesciu najbiedniejszym koronom nie pomoze, bo ich lud nie ma nadwyzki.

Nie wiem:
- Ile z nowego zoldu wroci do ludu i z jakim opoznieniem. Tego nie da sie odczytac z jednego biegu bez proby.
- Jak wyglada to samo w zimie.
