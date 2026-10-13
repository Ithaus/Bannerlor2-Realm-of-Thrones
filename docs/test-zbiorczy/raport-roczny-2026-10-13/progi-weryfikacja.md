# Weryfikacja rozdzialu PROGI, BLEDY, CZAS (plik analityka: progi.md)

Praca tylko do odczytu. Nic nie uruchamialem w grze, nic nie zmienialem.
Nazwa pliku: zadanie podalo "${l.key}-weryfikacja.md" (niepodstawiony szablon). Zapisalem jako `progi-weryfikacja.md`, skrypty w `skrypty\progi-wer\`.
Wszystkie liczby policzylem wlasnymi skryptami z surowych danych. Skryptow analityka nie czytalem (nie bylo roznic do wyjasniania w liczbach surowych; roznice sa we wnioskach).

## Skroty zrodel

| Skrot | Plik |
|---|---|
| LOG:n | `D:\Backup-Bannerlord\logs\2026-10-13-noc34-35\Armoury-2026-10-13_06-24-10.log`, linia n |
| LOGP | to samo, `Armoury-2026-10-13_02-15-21.log` (poprzedni bieg roczny) |
| CS:n | `...\session-2026-10-13_06-24-09.log`, linia n |
| BUD | `...\Logs\2026-10-13_06-24-10\budzet-rodow.csv` (doba = kolumna dzien - 108835) |
| LUD | `...\Logs\2026-10-13_06-24-10\kiesa-ludu.csv` (53 probki, co 7 dob) |
| KASY | `...\Logs\2026-10-13_06-24-10\kasy-miast.csv` |
| R5 e5:63 | `scratchpad\test\progi-rok5\etap5.txt`, linia 63 |
| KOD | `...\ae222cb7-...\scratchpad\zb\int\Armoury\src\` |
| PROJ | `C:\Users\GAME\Bannerlor2-Realm-of-Thrones\docs\kiesa-ludu\PROJEKT-OBIEG-KIESY-LUDU-2026-10-13.md` |
| W:n | wynik mojego skryptu `raport-rok\skrypty\progi-wer\wN.txt` (skrypt `wN_*.py` obok) |

[P] = pomiar. [S] = moj szacunek (wzor obok).

## 0. W skrocie

- Liczby surowe analityka sa dobre. Z 24 sprawdzonych 19 zgadza sie co do sztuki, 1 czesciowo, 4 nie (wszystkie 4 to jego szacunki albo definicja, nie odczyt).
- Rozdzial o bledach, czasie i ksiegach: potwierdzam.
- Rozdzial 7 (odpowiedz na pytanie Jeffa o podatki): liczby wejsciowe dobre, wniosek za szeroki. Trzy rzeczy:
  1. Skarbce koron nie "topnieja z biedy". To zaplanowana regula: korona wydaje dziennie 1/360 zapasu ponad 500 tys. Dzis to 37 tys. zl/d, nie 51-85 tys.
  2. Glow ubylo, bo zolnierz zdrozal, a nie dlatego, ze zabraklo pieniedzy. Rachunek zoldu spadl o 6.5%, liczba ludzi o 25%, zold na czlowieka wzrosl o 25%.
  3. Tylko co trzeci rod stoi na pulapie zoldu. Do tych rodow trafia 45% rent korony. Reszta ma pieniadze i niepelne druzyny z innych przyczyn. Dlatego 50 tys. zl/d nowego podatku da ok. 2-4 tys. ludzi [S], nie 5.6 tys. Gotowy projekt kiesy ludu, na ktory analityk sie powoluje, sam to pisze (1.5-2.5 tys. ludzi).
- Podatki sa mozliwe i maja sens dla zamkniecia obiegu. Ale same raczej nie dadza 85 tys. Najmocniejsza dzwignia lezy gdzie indziej: drogi zolnierz u rodow na pulapie (niespojnosc nr 7 analityka) - do +11.8 tys. ludzi bez jednego zlotego [S].

## 1. Liczby analityka policzone od zera

| Nr | Liczba analityka | Moja | Zgodne? | Zrodlo mojej |
|---|---|---|---|---|
| 1 | Progow 1161, NIE 45, DECYZJA 4; poprzedni rok NIE 41 z 1124 | 1161 wierszy, NIE 45, DECYZJA 4; poprzednio 41 z 1124 | TAK. Uwaga: 1161 to wiersze razem z INFO. Wierszy z werdyktem jest 673 (INFO 488). 45 NIE to 6.7% sprawdzanych progow, nie 3.9% | [P] W:w8; zliczenie wierszy `progi-rok5\etap2..8.txt` |
| 2 | Naprawione 9, zepsute formalnie 11, nowe wiersze rundy 3: 37 (TAK 22, NIE 2, INFO 13) | 9 / 11 / 37 (22 / 2 / 13) | TAK | [P] W:w8 |
| 3 | Wojsko w wojnie 79 409, ostatnia doba 73 578; rok wczesniej 64 013 | 79 409 / 73 578 / 64 013 | TAK | [P] W:w1, W:w10; linie "Skarbce:" LOG:128749-128777, srednia 28 dob |
| 4 | Zloto swiata 208 204 213 -> 208 335 095 (+0.063%); rok wczesniej -2 373 117 | to samo; -2 373 117 (-1.139%) | TAK | [P] LOG:707, LOG:129205; W:w1, W:w1_poprz |
| 5 | Kto trzyma zloto (8 wierszy tabeli 7a) | skarbce 57.93 -> 27.11; kasy miast 27.33 -> 10.98; notable 28.82 -> 21.85; glowy 52.25 -> 47.23; lud miast 6.28 -> 44.12; sakiewki 0.34 -> 16.59; wsie 1.11 -> 9.55; podzamcza 0.67 -> 3.33 mln | TAK, wszystkie 8 | [P] LOG:707, LOG:129205 |
| 6 | Ubytek skarbcow 84 900 zl/d w roku, 51 400 w ostatnim kwartale | 84 901 / 51 440 | TAK. Ale patrz 2a: z tego regula 1/360 to 70 477 zl/d w roku i 37 253 w d337-364 | [P] W:w1 |
| 7 | Wplywy koron 448 915; danina z kas osad 217 636; podatek ludu 15 025; zwrot zoldu 277 712; renty 172 233 (d337-364) | 448 915 / 217 636 / 15 025 / 277 712 / 172 233 | TAK | [P] W:w1; linie "Korona: wplywy dnia (165)", ostatnia LOG:129101 |
| 8 | Stawka podatku ludu srednio 2.37%, pulap 3.0%, 19 z 26 krolestw na pulapie | 2.36% (d337-364), pulap 3.0%, na pulapie srednio 18.5, w ostatniej dobie 20 | TAK | [P] W:w1; LOG:129100 |
| 9 | Zold partii 456 511 na 79 001 ludzi = 5.78 zl | 456 511 / 79 001 = 5.779 | TAK | [P] BUD kolumny zold_partii, ludzi_partie, d337-364; W:w2 |
| 10 | Lud miast: dochod 537 132, wydatki 436 616, odklada 100 516; ostatnie 8 probek 61 612 | to samo | TAK. Ale 100 tys. to srednia roku. Kwartalami: 165 / 107 / 70 / 63 tys. Ostatnie 4 probki: 45 tys. | [P] LUD kolumny dochod, wydatki; W:w4 |
| 11 | Czas 11.98 s/dobe (mediana 11.76); rok wczesniej 12.98 (12.85); trend +1.49 i +0.01 s na 100 dob; pamiec 1 218 -> 1 517 MB | to samo | TAK | [P] W:w5; pole "doba N ms (zegar)" |
| 12 | 0 linii ERROR z 129 290; 23 bloki CrashScribe | 0 z 129 290; 23 bloki "ERROR CAUGHT FURTHER UP" | TAK | [P] W:w5; grep CS |
| 13 | Konie: reszta +54 358, juczne +56 478; stan 21 839 -> 66 616 | to samo | TAK | [P] W:w5; LOG:709, LOG:129221 |
| 14 | Zboze +65 002, ryby +59 623; kwartaly zboza 17 471 / 14 980 / 17 688 / 14 863; towary razem +19 397 | to samo | TAK (mianownik produkcji u mnie 8 548 572, u niego 9 042 624 - inna definicja, bez znaczenia) | [P] W:w7 |
| 15 | Thenn: kasa 7 zl w d321; 331 dob ponizej zapasu, seria 323; rok wczesniej 105 i 7; 38 dob z kasa ponizej 1000 | to samo (Thenn 30 dob, Frostfang's Camp 9) | TAK | [P] KASY kolumny kasa, zapas_kupcow; W:w6 |
| 16 | Swaty 464 -> 697 ms/d, srednio 609 | 471 -> 697, srednio 611 | TAK | [P] W:w5; CS linie "Swaty ROT (Z.3)" |
| 17 | Rody z lennem bez wojska 14 z 205; rok wczesniej 29 | 14 z 205; 29 z 198 | TAK | [P] BUD; W:w13 |
| 18 | "26 z 29 krolestw wojuje caly rok" | 26 z 29 od ok. 24. doby do konca; srednia roku 24.6; w dobach 2-23 wojuja 2-4 krolestwa | CZESCIOWO (sens ten sam) | [P] W:w1 |
| 19 | 16 wojen w ostatniej dobie | 16 | TAK | [P] CS:9247 |
| 20 | Glowy rodow ponizej 5 000 zl: 21 -> 5 | 5 wedlug linii logu (z zamkiem 5); moj spis z BUD, wszystkie rody AI w krolestwach: 52 -> 33 | TAK co do kierunku; inna grupa rodow | [P] LOG:129144; W:w13 |
| 21 | Kolejka czekajacych na konia rosnie +40/d (4 476 -> 5 878) | netto +5.1/d: (5 878 - 4 476) / 273 dob; z csv 4 359 -> 5 540 = +4.3/d | NIE. "+40/d" to mediana zmian dziennych z progu, nie tempo wzrostu | [P] R5 e5:371; `konie-krolestwa.csv` kolumna czeka; W:w14 |
| 22 | Propozycja A: pulap 10% da ok. 63 tys. zl/d, 12% ok. 76 tys. | 42-55 tys. przy 10%, 49-66 tys. przy 12% - razem z dzisiejszymi 15 tys. Przyrost: +27-40 tys. albo +34-51 tys. | NIE. Patrz 2c | [S] z [P] W:w9 |
| 23 | Propozycja B: 0.2% dziennie z 44.12 mln = ok. 88 tys. zl/d | Projekt, na ktory sie powoluje, liczy inaczej: 2.5% dziennie z nadwyzki ponad zapas. Wynik projektu: 49-75 tys. zl/d (srodek 62), w nowej kampanii 70 tys. | NIE. Stawka 0.2% nie ma zrodla | PROJ linie 132, 180, 191 |
| 24 | Potrzeba koron 85-115 tys. zl/d, zeby utrzymac stan i dojsc do 85 tys. ludzi | Rzad wielkosci podobny (ok. 90-155 tys. [S]), ale skladniki inne i to nie wystarczy wiedziec - patrz 2a-2d | NIE co do rachunku | [S] rozdz. 2 |

## 2. Co zmienia odpowiedz na pytanie Jeffa (rozdzial 7 analityka)

### 2a. Skarbce koron: to regula, nie dziura

- Kod: korona wydaje co dobe wplywy dnia plus 1/360 zapasu ponad 500 000 zl. [P: LOG:129101, opis w linii; LOG:128742]
- Start: kazde z 29 krolestw ma ok. 2 mln. Po roku krolestwa, ktore nie robily nic wiecej, maja 1 047 000 - dokladnie tyle wychodzi z reguly (500 000 + 1 500 000 x 0.364). [P: LOG:128749-128777, np. Myr 1 047 151, Pentos 1 047 217, Ibben 1 047 236]
- Z ubytku 30.82 mln w roku regula 1/360 to 25.62 mln (83%). [P: W:w1, suma pola "1/360 zapasu"]
- Doplata z zapasu dzis: 37 253 zl/d (d337-364) = 7.7% wydatkow koron (486 168 zl/d). W pierwszym kwartale bylo 106 154. [P: W:w1]
- Za rok ta doplata spadnie do ok. 14 tys. zl/d, czyli ubedzie ok. 23 tys. zl/d. [S: 37.3 tys. x e^(-364/360)]
- Reszty ubytku (ok. 11.5 tys. zl/d w d337-364: 48 824 - 37 253) nie umiem wyjasnic. Nie wiem.
- Wniosek: liczba "51-85 tys. zl/d" miesza srednia roku (rozped pierwszego roku) ze stanem dzisiejszym. Prawdziwa dziura do zatkania na dluzej to 23-37 tys. zl/d [S], nie 51-85.

### 2b. Dlaczego glow jest mniej: zolnierz zdrozal

| Doby | Ludzi w partiach rodow | Zold partii, zl/d | Zold na czlowieka |
|---|---|---|---|
| 57-84 (szczyt) | 105 185 | 488 497 | 4.64 |
| 169-196 | 86 925 | 460 534 | 5.30 |
| 337-364 | 79 001 | 456 511 | 5.78 |
| Zmiana od szczytu | -24.9% | -6.5% | +24.5% |

[P: BUD, W:w2]

- Rody wydaja na zold prawie tyle samo co w szczycie. Kupuja za to o cwierc mniej ludzi, bo ludzie awansowali.
- Dzisiejszy rachunek zoldu przy cenie czlowieka z dob 57-84 oplacilby 98.3 tys. ludzi. [S: 456 511 / 4.644]
- Wojsko dalej powoli spada: -27 ludzi na dobe w ostatnich 140 dobach (ok. -9.8 tys. na rok, gdyby tempo sie utrzymalo). [P: W:w10, trend liniowy; przedluzenie na rok to S]
- Tego analityk nie napisal. Jego zdanie "bez nowego dochodu wojsko bedzie dalej spadac" jest zgodne z trendem, ale przyczyna jest glownie inna niz skarbce.

### 2c. Kogo naprawde ogranicza pieniadz (doby 337-364, rody AI bez najemnikow)

| Grupa rodow | Rodow | Ludzi w partiach | Zold wobec pulapu | Niewykorzystany pulap, zl/d | Zold na czlowieka | Udzial w rentach korony |
|---|---|---|---|---|---|---|
| A: na pulapie zoldu (zold >= 95% pulapu) | 109.5 | 29 001 (38%) | 1.11 | 0 (ponad pulap 30 153) | 7.01 | 45.2% |
| B: druzyny pelne (sufit liczby ludzi) | 12.8 | 8 298 | 0.54 | 57 974 | 6.23 | 9.4% |
| C: 80-95% pulapu | 17.3 | 7 170 | 0.88 | 8 208 | 5.74 | 8.4% |
| D: luz (zold ponizej 80% pulapu, druzyny niepelne) | 138.1 | 32 034 (42%) | 0.40 | 292 835 | 4.53 | 36.9% |
| bez budzetu (bez lenna) | 57.2 | 308 | - | - | - | 0 |

[P: BUD kolumny pulap, zold_partii, zold_zalog, zold_karawan, na_suficie, limit_partii, renta_180; W:w3, W:w3b]

- Rodow na pulapie przybywa caly rok: 39.5 (d57-84) -> 84.4 (d169-196) -> 109.5 (d337-364). Ich zolnierz drozeje: 5.60 -> 6.11 -> 7.01 zl. [P: W:w3]
- 73 rody z lennem z grupy D maja 9.98 mln zl ponad swoja rezerwe wojenna i 258 tys. zl/d wolnego pulapu, a druzyny wypelnione w 60%. Pieniadz ich nie ogranicza. Co ogranicza: nie wiem (nie mierzylem werbunku ani strat). [P: W:w11]
- Regula budzetu: w wojnie na zold idzie 60% stalego dochodu rodu. [P: KOD `ClanBudget.cs`:205, `Settings.cs`:1034 WarWageShare = 0.6]
- Rachunek: 50 tys. zl/d nowego dochodu koron -> 45% do rodow na pulapie = 22.6 tys. -> 60% na zold = 13.6 tys. -> 1.9-2.3 tys. ludzi (po 7.01 albo 5.78 zl). Z obiegiem zwrotu zoldu najwyzej 3.3-4.0 tys. [S]
- To samo mowi gotowy projekt kiesy ludu: do panow 50-65 tys. zl/d, wojska "ok. 1.5-2.5 tys. ludzi wiecej (najwyzej 6-7 tys.)", bo "dwa rody na trzy maja pieniadze i niepelne druzyny z innych przyczyn". [PROJ linie 33, 35, 36]
- Analityk liczy 5 591 ludzi x 5.78 zl = 32 tys. zl/d, czyli zaklada, ze kazdy zloty korony staje sie zoldem. Dane tego nie potwierdzaja. Przy dzisiejszym rozdziale potrzeba ok. 70-120 tys. zl/d na same 5.6 tys. ludzi. [S: 32 tys. / (0.45 x 0.60), z obiegiem zwrotu i bez]

### 2d. Ile da sie wziac od mieszczan

- Podatek 184 jest od zarobku, nie od oszczednosci. Stawka rosnie o 0.5 punktu co 7 dob tylko w krolestwie, ktore ma potrzebe (kredyt w Banku, zwrot ponad wplywy albo skarbiec ponizej zapasu wojny). [P: KOD `KingdomTreasury.WarTax.cs` opis linie 10-30; LOG:129100]
- Podstawa podatku (zarobek z wagami klas) to ok. 553 tys. zl/d. [S: suma pobrano / stawka po krolestwach, W:w9]
- 75.7% dzisiejszego poboru (11 375 z 15 025 zl/d) pochodzi z krolestw, ktore stoja na pulapie i maja potrzebe przez co najmniej polowe dob. Tylko one pojda w gore same. [P: W:w9]
- Pulap 10%: razem 42-55 tys. zl/d (dol: rosna tylko krolestwa z potrzeba; gora: wszystkie). Pulap 12%: 49-66 tys. Dojscie z 3% do 10% trwa 14 tygodni. [S z P]
- Blad analityka: 15 025 x (10 / 2.37). Srednia 2.37% liczy tez krolestwa, ktore prawie nic nie placa (Straz, Dothrakowie, Zelazne Wyspy: 0-4 zl/d). Krolestwa placace sa juz na 3%. I 63 tys. to suma, nie przyrost.
- Mieszczanie odkladaja dzis 62-75 tys. zl/d, nie 62-100: csv ostatni kwartal 62 522, ksiega pieniadza ostatni kwartal 74 790. Tempo spada co kwartal: 143 -> 116 -> 84 -> 75 tys. zl/d wedlug ksiegi. [P: W:w4, W:w1]
- W ostatnich 4 probkach lud odklada tylko w kilku krolestwach (Polnoc 13.8 tys., Volantis 12.8, Targaryenowie 9.1, Dothrakowie 7.0, Dolina 5.9 zl/d). W 12 krolestwach lud wydaje wiecej, niz zarabia. Tam podatek od zarobku obetnie jedzenie. [P: LUD, W:w12; 4 probki - malo]
- Oszczednosci sa skupione: 10 miast ma 36.5% kies, biedniejsza polowa miast 7.0%. [P: W:w4]
- Dlatego kolejnosc analityka (najpierw danina z oszczednosci ponad zapas, potem pulap 184) jest dobra. Zgadzam sie.
- Dobra wiadomosc: Polnoc i Dolina maja razem 35.5% ludzi z rodow na pulapie i 19.4% kies ludu. Tam podatek trafi do potrzebujacych. [P: W:w12]

### 2e. "Czas wojny" to zawsze - i to nie przypadek biegu

- W roku byly 73 pokoje w wojnach fabularnych ROT. Wszystkie 73 wojny wrocily. [P: CS:9240 "fabularne 73 ... wznowione 73, NIE wznowione 0"]
- 16 wojen w ostatniej dobie to stale pary z fabuly. 3 krolestwa bez wojny to Skagos, Wyspy Letnie i wygnancy Yi Ti (752 ludzi razem). [P: CS:9247, LOG:128773-128775]
- To nie jest skutek biegu bez gracza ani pierwszego roku. Podatek "na czas wojny" bedzie w tym modzie podatkiem stalym. Jeff powinien to wiedziec przed decyzja.

### 2f. Czego w propozycjach brakuje

| Co | Dane | Znacznik |
|---|---|---|
| Drogi zolnierz u rodow na pulapie. Prog P3W2-4: 31 257 ludzi po 6.88 zl wobec 5.00 zl u rodow z luzem. Gdyby rod na pulapie mial sklad jak rod z luzem, ten sam zold oplacilby 43 010 ludzi, czyli +11.8 tys. | R5 e8:265 | [S: 31 257 x 6.88 / 5.00]. Gorna granica |
| Panowie z pieniedzmi i niepelna druzyna (grupa D z lennem): 9.98 mln ponad rezerwe, 258 tys. zl/d wolnego pulapu. Sredniowieczna "tarczowe": kto nie wystawia ludzi, placi koronie. Glowy rodow to najwieksza kiesa swiata (47.23 mln), a w tabeli propozycji ich nie ma | W:w11, LOG:129205 | [P] stan; propozycja to S |
| Kierowanie pieniedzy korony: dzis renty ida wedlug lenn (waga miasto 2, zamek 1.5, wies 0.25), wiec 55% trafia do rodow, ktorym zold nie brakuje | LOG:128744, W:w3b | [P] |
| Ile ludzi da kazda propozycja. Analityk podaje zlote, nie ludzi | - | brak |

## 3. Niespojnosci analityka - proba obalenia

| Nr | Tytul | Werdykt | Uzasadnienie |
|---|---|---|---|
| 1 | Wojsko oplacane z topniejacych skarbcow, zloto zbiera sie u mieszczan | CZESCIOWO | Kierunek prawdziwy: 37 tys. zl/d wydatkow koron idzie z zapasu [P]. Ale to zaplanowana regula 1/360 (83% ubytku roku), sama gasnie, a "85 tys." to srednia z rozpedem pierwszego roku. Kiesy mieszczan rosna naprawde (+37.8 mln), tempo spada co kwartal. Glownym powodem spadku glow jest drozszy zolnierz (2b) |
| 2 | Konie juczne z niczego | PRAWDZIWA | 85% reszty to staly doplyw 116-155 jucznych na dobe, rosnacy w drugim polroczu. Tylko 15% (8 269) to pierwszy tydzien kampanii. W poprzednim biegu to samo (+56 728). Nie artefakt startu ani braku gracza [P: W:w5] |
| 3 | Thenn bez pieniedzy pol roku | CZESCIOWO | Pomiar prawdziwy. Ale to 1 miasto z 97 i 1 bieg wobec 1 biegu - moze byc los kampanii. Dowodu na runde 3 nie ma (analityk tez tak pisze). Trop: karawana kupca stoi w Thenn w 285 liniach dobowych tego biegu wobec 29 w poprzednim [P: grep "Wyspy i drogi"]. Lacznie zatkanych karawan jest mniej (13 -> 7) |
| 4 | Zboze i ryby przybywaja | PRAWDZIWA | Liczby zgodne. Dwa tropy, ktorych analityk nie podal: (a) reszta zboza idzie w parze z przyrostem zboza u lordow (korelacja dzienna 0.58; u lordow 14 259 -> 48 199 szt.); (b) najwieksze skoki w dobach 23-24, czyli przy wybuchu wojen, w obu biegach (+3 134 i +3 619; poprzednio +2 487 i +3 752). To wskazuje na jedzenie dawane partiom lordow przez gre. 12 innych rodzajow jedzenia ma razem ok. -84 tys., wiec netto ksiega towarow to +19 tys. (0.2%) [P: W:w7] |
| 5 | Drewno w miastach rosnie bez konca | PRAWDZIWA | W ostatniej dobie produkcja 2 214, zuzycie 1 664 ladunkow - nadwyzka trwa do konca roku, wiec to nie napelnianie pierwszego roku. Waga mala: cena zbytu 2.9 zl za ladunek [P: LOG:129019, LOG:128730] |
| 6 | Mieszczanie razem bogaci, w ok. 10 miastach lud nie stac na jedzenie | PRAWDZIWA | I pogarsza sie w ciagu roku: srednio 0.7 -> 4.3 -> 6.9 -> 8.9 miast w kolejnych kwartalach. Analityk pisze "LEPIEJ" (wobec poprzedniego roku), ale wewnatrz biegu rosnie. W d364 jest 8 miast; ich lud zarabia razem 15 914 zl/d, a potrzebuje 36 097 [P: W:w4] |
| 7 | Biedny rod ma drozszego zolnierza, czesc rodow bez wojska | PRAWDZIWA, waga za niska | To glowna przyczyna braku do 85 tys., nie "srednia". Moj podzial: 7.01 wobec 4.53 zl. Rody z lennem bez wojska rosna caly rok: 1.4 (d57-84) -> 9.4 (d225-252) -> 16.0 (d337-364), bez plateau [P: W:w3, W:w13] |
| 8 | Jazdy za malo | CZESCIOWO | Brak jazdy wzgledem celu - znany, nie obalam (Dothrakowie 27.8% czeka na konia: zgodne z csv). Ale "kolejka +40/d" to mediana zmian dziennych; kolejka rosnie netto o 5 na dobe [P: R5 e5:371, W:w14] |
| 9 | Polowa uprzezy znika z ksiegi | CZESCIOWO | Nie liczylem sam. Analityk sam pisze, ze to raczej brak licznika. W jego pliku sa dwie liczby: -7 395 z 14 995 (prog) i -7 420 z 15 026 (skrypt) - roznica jednej doby |
| 10 | Sakiewki zolnierzy puchna | CZESCIOWO | Nie puchna: 16.25 mln w d182, 16.73 w d273, 16.59 w d364 - stoja od pol roku [P: W:w1]. Z 26.1 dnia zoldu 15.2 dnia to oszczednosci K1, ktorych markietani z zalozenia nie ruszaja [P: R5 e5:63]. Prog 15 dni jest starszy niz ta regula |
| 11 | Pojedyncze potkniecia ksiag ludzi | CZESCIOWO | Nie liczylem sam. Dwie pojedyncze doby w roku (56 i 6 ludzi). Z jednej doby nie da sie orzec regresu |
| 12 | Wejscie do osady 1.6 s, stoper ponad dobe | CZESCIOWO | Blad stopera potwierdzony: MenPurse.OnEntered 25 652 ms przy dobie 14 646 ms, a jego czesci daja razem 194 ms [P: LOG:76078, LOG:76079]. Samo "1 594 ms" pochodzi z tego samego stopera, wiec tez moze byc zawyzone. Pewne jest tylko, ze doba trwala o ok. 2.4-2.9 s dluzej niz zwykle |
| 13 | Koszt swatow rosnie z liczba lordow | CZESCIOWO | Wzrost prawdziwy (471 -> 697 ms). Przyczyna podana tylko w polowie trafnie. Do ok. doby 100 czas idzie za liczba szukajacych (d76 -> d100: szukajacych 1 188 -> 1 335, ocen par 425 -> 535 tys., czas 469 -> 623 ms). Po dobie 100 szukajacych przybywa 3-5%, ocen par ubywa ok. 20%, a czas dalej rosnie o ok. 12% [P: W:w5, CS linie "Swaty ROT (Z.3)"]. Co rosnie po dobie 100: nie wiem |
| 14 | Pamiec gry rosnie caly rok | CZESCIOWO | Rosnie, ale coraz wolniej: 1.65 -> 0.81 -> 0.49 MB na dobe miedzy kolejnymi oknami [S z P: W:w5]. To wyglada na zapelnianie swiata, nie na wyciek. Pewnosc da bieg dwuletni |

## 4. Braki

1. Najwazniejszy: nie ma mostu "zloty podatku -> zolnierz". Analityk przyjal 1:1. Dane (2c) i projekt kiesy ludu mowia: ok. jedna trzecia do polowy. Wniosek "85-115 tys. zl/d i dojdziemy do 85 tys." jest szerszy niz dane.
2. Nie nazwano glownej przyczyny spadku glow: zold na czlowieka +25% przy rachunku zoldu -6.5% (2b).
3. Regula 1/360 opisana jednym zdaniem i bez rozbicia ("pelnego rozbicia nie robilem"). Rozbicie zmienia wniosek (2a).
4. "Mieszczanie odkladaja 62-100 tys." - gorna liczba to srednia roku z rozpedem startu. Dzis 62-75 tys. i spada.
5. Propozycja A liczona zla srednia i jako suma zamiast przyrostu. Propozycja B ze stawka 0.2% bez zrodla, choc projekt ma wlasny rachunek (49-75 tys.).
6. Brak propozycji poza mieszczanami: panowie z luzem (9.98 mln ponad rezerwe), kierowanie rent do rodow na pulapie, poprawka drogiego zolnierza. Pytanie Jeffa brzmialo "gdzies zwiekszyc" - nie tylko u ludu.
7. Brak liczby ludzi przy kazdej propozycji. Jeff pyta o wojsko, nie o zlote.
8. Brak trendu wewnatrz roku dla trzech pozycji opisanych jako "LEPIEJ": rody z lennem bez wojska (rosnie), miasta bez pieniedzy na jedzenie (rosnie), wojsko (-27 ludzi/d).
9. "1161 progow" - to wiersze z INFO. Sprawdzanych progow jest 673.
10. Ok. 11.5 tys. zl/d ubytku skarbcow poza regula 1/360 - bez wyjasnienia (u mnie tez).
11. Nie sprawdzono, co ogranicza 73 rody z lennem, ktore maja pieniadze i druzyny w 60%. Bez tego nie wiadomo, czy jakikolwiek podatek dojdzie do 85 tys.

## 5. Moje skrypty

Katalog `raport-rok\skrypty\progi-wer\`. Kazdy `python -I`, tylko odczyt. Wyniki w plikach .txt obok.

| Skrypt | Co liczy |
|---|---|
| w1_pieniadz_korona.py (w1.txt, w1_poprz.txt) | posiadacze zlota, skarbce, wplywy koron, podatek 184, zwrot, renty, wojsko w wojnie |
| w2_budzet_rodow.py (w2.txt, w2_poprz.txt) | zold, ludzie, pulap w blokach 28 dob |
| w3_co_ogranicza_wojsko.py, w3b_renty_kategorie.py | rody na pulapie i z luzem; do kogo ida renty |
| w4_kiesa_ludu.py | dochod i wydatki ludu, rozklad miedzy miastami |
| w5_czas_konie.py | czas doby, pamiec, swaty, ksiega koni, linie ERROR |
| w6_kasy_thenn.py | kasy miast, Thenn |
| w7_towary.py | ksiega towarow, zboze i ryby |
| w8_progi.py | zliczenie wierszy progow |
| w9_podatek184.py | podatek ludu wedlug krolestw |
| w10_wojsko_dzienne.py | wojsko dzien po dniu, trend |
| w11_kiesy_rodow.py | kiesy rodow wobec rezerwy |
| w12_lud_wg_krolestw.py | kiesy ludu wedlug krolestw |
| w13_glowy_lenno0.py | biedne glowy, rody z lennem bez wojska |
| w14_jazda.py | jazda i kolejka na konia |
