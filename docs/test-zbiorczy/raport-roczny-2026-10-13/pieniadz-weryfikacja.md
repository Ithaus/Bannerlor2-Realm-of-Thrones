# WERYFIKACJA rozdzialu "PIENIADZ RODOW I KORON" (plik analityka: pieniadz.md)

Niezalezne przeliczenie od zera. Tylko odczyt logow, tabel i kodu - nic nie zmieniono w grze.
Nazwa pliku: w zadaniu byl niewypelniony znacznik ${l.key}; analityk przyjal klucz "pieniadz", wiec ten plik to pieniadz-weryfikacja.md.

## 0. Werdykt w skrocie

- Liczby analityka sa policzone dobrze. Z 22 sprawdzonych 17 wyszlo zgodnie (wiekszosc co do zlota), 2 prawie (roznica metody), 3 maja dobra liczbe, ale zly opis albo wniosek.
- Odpowiedz na pytanie o podatki trzyma sie w glownym punkcie: jedyny staly strumien do opodatkowania to zarobek mieszczan, a samym podatkiem puszczonym starymi drogami progu 85 tys. sie nie kupi [S].
- Trzy rzeczy trzeba poprawic, zanim to pojdzie do Jeffa:
  1. "87 tys. zl dziennie niewydanego sprzetu do przesuniecia na zold" - NIE. Kod juz dzis nie bierze tych pieniedzy od rodu, ktory jest ponad pulapem zoldu. Realnie do wziecia jest 13-20 tys. dziennie [S].
  2. Wojsko nie spadlo dlatego, ze ubylo pieniedzy na zold. Zold druzyn jest prawie ten sam (478 -> 456 tys. dziennie, -5%), a ludzi jest o 20% mniej, bo zolnierz podrozal z 4.88 do 5.80 zl dziennie [P]. Tego w rozdziale nie ma.
  3. "Podatek na czas wojny" to w tym swiecie podatek staly: 25 z 26 wojujacych krolestw ma 340 i wiecej dob wojny w roku [P]. Do tego stawka rosnie tylko o 0.5 punktu na tydzien, wiec dojscie z 3% do 12-15% trwa 18-24 tygodnie.
- Z 12 niespojnosci: 4 prawdziwe, 6 czesciowo, 2 artefakty (N8, N9).

## 1. Skad sa moje liczby

| Skrot | Plik |
|---|---|
| LOG | D:\Backup-Bannerlord\logs\2026-10-13-noc34-35\Armoury-2026-10-13_06-24-10.log |
| LOG-S | D:\Backup-Bannerlord\logs\2026-10-13-noc34-35\Armoury-2026-10-13_02-15-21.log (poprzedni bieg) |
| CSV-R | ...\Logs\2026-10-13_06-24-10\budzet-rodow.csv (318 rodow AI x 364 doby) |
| CSV-R-S | ...\Logs\2026-10-13_02-15-21\budzet-rodow.csv (312 rodow AI) |
| PROGI | ...\scratchpad\test\progi-rok5\etap2.txt |
| KOD | ...\scratchpad\zb\int\Armoury\src (KingdomTreasury.WarTax.cs, ClanBudget.cs, CourtFull.cs, ClanIncomeBook.StableD.cs) |
| SKRYPTY | ...\scratchpad\raport-rok\skrypty\pieniadz-wer\ (00-06, wyniki w podkatalogu wyniki\) |

- [P] = pomiar (takze suma i srednia z pomiarow). [S] = szacunek.
- Doba 1 = "dzien" 108836 w liniach stanu, 108837 w liniach Korona / Renty / 184 / Budzet / Okupy. Doba 364 = 109199 / 109200.
- "Ostatnie 28 dob" = doby 337-364.
- Skrypty analityka czytalem dopiero po wlasnym wyniku (tylko 14_luz_na_rod.py, zeby wyjasnic roznice w wierszu 14).

## 2. Liczby analityka wobec moich

| Nr | Co | Analityk | Moje przeliczenie | Zgodne? | Zrodlo mojej liczby |
|---|---|---|---|---|---|
| 1 | Zloto swiata doba 1 -> 364 | 208 204 213 -> 208 335 095 (+130 882, +0.06%) | to samo; suma 26 pol rowna polu "razem" w obu dobach | TAK | [P] LOG:707, LOG:129205, skrypt 02 |
| 2 | Zloto swiata, poprzedni bieg | -2 373 117 | -2 373 117 (208 400 457 -> 206 027 340) | TAK | [P] LOG-S:676, LOG-S:128970 |
| 3 | Zloto rodow AI doba 1 -> 364 | 59 612 411 -> 52 657 242 | to samo (318 rodow, bez gracza i 28 dworzan BK) | TAK | [P] CSV-R kolumna G, skrypt 03 |
| 4 | Wplywy rodow / zold na dobe | 1 238 965 / 633 472 (51%) | 1 238 965 / 633 472 (51.1%) | TAK | [P] CSV-R wplyw_doby, zold_partii + zold_zalog + zold_karawan, 364 doby |
| 5 | Od korony: zwrot zoldu / renty | 262 753 / 216 852 | 262 753 / 216 852 | TAK | [P] CSV-R zwrot_korony, renta_180 |
| 6 | Korony: wplywy / zwrot / renty na dobe | 443 903 / 263 585 / 214 725 | to samo z linii "Obieg" (363 doby); z linii "Korona (165)" (364 doby): 445 156 / 262 860 / 216 852 | TAK | [P] LOG linie Obieg i Korona: wplywy dnia (165), skrypt 02 |
| 7 | Skarbce koron doba 1 -> 364 | 57 927 181 -> 27 108 168 | to samo; ostatnie 28 dob -49 303 dziennie | TAK | [P] LOG:707, :129205 |
| 8 | Kiesy ludu miast | 6 277 921 -> 44 120 365; +104 249 dziennie; ostatnie 28 dob +70 166 | to samo | TAK | [P] LOG:707, :129205, skrypt 02 |
| 9 | Mieszczanin: zarobek / zakupy / podatki / odklada | 533 649 / 415 443 / 25 733 (4.8%) / 19.5%, pod koniec 12.8% | 533 650 / 415 443 / 24 659 (4.6%) / 19.5%, pod koniec 12.8% | PRAWIE | [P] LOG linie Kiesa ludu (364) i 184; analityk doliczyl do podatkow miast 1 045 zl podatku wojennego podzamczy |
| 10 | Podatek wojenny ludu: rok / ostatnie 28 dob; pulap; na pulapie | 10 695 / 15 020; 3%; 20 z 26 | 10 695 / 15 020; 3% (z polityka War Tax 6%); 20 z 26 w ostatniej dobie, srednio 18.6 w 28 dobach | TAK | [P] LOG:129100 i 364 linie "podatek wojenny ludu (184)" |
| 11 | Ile daje 1 punkt stawki; pulap 12-15% | ok. 5 tys.; +45 do +60 tys. dziennie | 5 496 zl na punkt; +51 do +67 tys., gdy wszystkie krolestwa dojda do pulapu; +39 do +51 tys., gdy tylko te z "potrzeba" (15 z 26) | TAK w granicach szacunku | [S] z [P]: suma (pobrano / stawka) po krolestwach, ostatnie 28 dob, skrypt 02 czesc G |
| 12 | Ludzi w druzynach: doba 364 / srednia 337-364 / poprzedni bieg | 73 280 / 78 693 / 63 129 | 73 280 / 78 693 / 63 129 | TAK | [P] CSV-R, CSV-R-S ludzi_partie |
| 13 | Rody na pulapie zoldu; ich udzial w rentach i zwrocie; zold ich zalog | 133; 43% i 39%; 102 tys. | 132.9; 43% i 39%; 102 098 | TAK | [P] CSV-R, klasa liczona w kazdej z 28 dob, skrypt 03 czesc 6 |
| 14 | Do limitu druzyn brakuje; z tego trzyma pulap / nie trzyma | 83.9 tys.; 63.3 / 20.7 tys. (75% / 25%); 56 rodow stac na cala luke | 83 940; 63 447 / 20 493 (76% / 24%); 72 rody | PRAWIE | [P/S] CSV-R; ja licze na sredniej 28 dob rodu, analityk w kazdej dobie osobno - stad 20.5 wobec 20.7 tys. i 72 wobec 56 |
| 15 | Koszt zolnierza: rod na pulapie / z luzem | 6.88 / 4.76 zl | 6.88 / 4.76 zl (pelne druzyny 6.72, kompanie 6.82) | TAK | [P] CSV-R zold_partii / ludzi_partie |
| 16 | Okupy w roku; rod Kasgos | 1 259 za 52 328 752; 31 okupow za 787 103, glowa 11 razy | 1 259 okupow AI za 52 275 840 plus 1 okup kurierem z kontroli testowej 52 912 = 52 328 752; Kasgos 31 za 787 103, Doro Kasgos 11 razy | TAK | [P] 1 260 linii "Okupy (178)" i 364 linie dzienne, skrypt 02 czesc E |
| 17 | Pojmania lordow | 2 919 | 2 919 (suma dzienna tez 2 919); okup 1 260, ucieczka 680, pokoj 528, po bitwie 334 | TAK | [P] LOG:129147 |
| 18 | Zloto kompanii najemnych doba 364: ten / poprzedni bieg | 5 649 484 / 20 597 542 | 5 649 484 / 20 597 542 | TAK | [P] CSV-R, CSV-R-S, rodzaj = najemnik |
| 19 | Rody z lennem bez warunku renty: ten / poprzedni bieg | 12 / 57 | 12 / 57 (srednia roku 8.4 / 32.8) | TAK | [P] LOG:129098, LOG-S:128859 |
| 20 | Dwor / w tym niewydany sprzet / zwolnieni przez budzet (ostatnie 28 dob) | 148.7 tys. / 87.5 tys. / 335 ludzi dziennie | 148 657 / 87 475 / 335 (w roku 98 927) | TAK liczba, NIE wniosek (rozdzial 3) | [P] LOG linie "Budzet rodow (166)" i "Obieg" |
| 21 | Renty: doby 29-56 -> ostatnie 28 dob -> ostatnia doba | 221 -> 172 -> 141 tys. | 221 278 -> 172 002 -> 140 689 | TAK liczba; "141" to jedna doba (rozdzial 3) | [P] LOG linie "Renty korony (180)" |
| 22 | Para zoldu: zdjete / oddane | 229 480 768 / 229 841 212 (+360 444) | te same sumy; ale to nie zold oddaje wiecej (rozdzial 3) | TAK liczba, NIE opis | [P] LOG linie "bilans - przyczyny" i "Pieniadz swiata (rody)", skrypt 04 |

## 3. Co obalone albo do poprawy

| Co napisal analityk | Dlaczego nie | Poprawna wartosc |
|---|---|---|
| "Prog 85 tys. wymaga przesuniecia na zold niewydanego przydzialu na sprzet, ktory dzis idzie do dworu (87 tys. dziennie)". W tabeli: "ile u rodow na pulapie - nie wiem". | Kod daje odpowiedz. CourtFull.cs:180-186: w wojnie przeterminowany sprzet rodu, ktorego zold jest ponad pulapem, zostaje w kiesie ("ustapilo zoldowi"). CourtFull.cs:187-192: wplaca tylko rod ze zlotem ponad rezerwe. Czyli 87.5 tys. placa prawie same rody, ktore maja wolne miejsce w pulapie. | Przeterminowane 112 970 zl dziennie [P] = do dworu 87 475 + ustapilo zoldowi 12 908 + kompanie bez dworu 8 915 + dluznicy 2 102 + ponizej rezerwy 1 145 + podloga kiesy 436 (LOG linie "Budzet rodow (166)", ostatnie 28 dob). Rody zwalniajace ludzi (zold > 1.10 pulapu, 53 rody) maja przydzial sprzetu ok. 17.6 tys. dziennie przy zlocie ponad rezerwe [S z reguly 0.17 D + 0.2 (G-R)/45, CSV-R]. Do wziecia na zold: 13-20 tys. dziennie [S], nie 87 tys. To ok. 2-3 tys. ludzi [S]. |
| "Z nowej zlotowki dochodu na zold idzie 60-80 groszy" | To jest wynik po wielu tygodniach. Od razu pulap rosnie mniej, bo razem z dochodem rosnie rezerwa rodu (R = 20 x D), a polowa rodow na pulapie jest ponizej rezerwy. | Rody na pulapie (130, z tego 65 ze zlotem ponad rezerwe): +1 zl stalego dochodu daje od razu +0.41 zl pulapu; z udzialem dworu, ktory w wojnie ustepuje zoldowi, +0.60 zl [S z reguly ClanBudget.cs:191-210 i :395-399, liczone rod po rodzie na CSV-R]. 60-80 groszy dopiero, gdy zloto rodu urosnie ponad rezerwe. |
| "Starymi drogami 1.5-3 tys. ludzi, zwrot zoldu zalog 3-4 tys., razem 82-84 tys." | Rachunek poprawny, ale na gornych zalozeniach (60-80 groszy, wszystkie krolestwa na pulapie). | Podatek starymi drogami: 0.9-2.5 tys. ludzi w pierwszych miesiacach, do 3.4 tys. po roku [S]. Zwrot zoldu zalog: 2.1-3.1 tys., do 4.2 tys. po roku [S]. Razem z dzisiejszych 78.7 tys.: 81-83 tys. Wniosek analityka (ponizej 85 tys.) zostaje. |
| "Renty spadly z 221 do 172 tys. i do 141 tys. w ostatniej dobie"; "36 rodow z lennem w krolestwach bez grosza na renty" | 141 tys. i 36 rodow to odczyt jednej doby. Renty skacza z doby na dobe: ostatnie 10 dob to 199, 151, 232, 319, 141, 142, 158, 139, 144, 141 tys. | Renty: 172 tys. (srednia 28 dob). Rodow z lennem w krolestwach bez reszty na renty: srednio 22.5 w ostatnich 28 dobach (doby 29-56: 8.6) [P]. Wazniejsze: zwrot + renty razem stoja od doby 253 na 448-453 tys. dziennie [P] - renty spadaja glownie dlatego, ze rosnie zwrot zoldu. |
| "Petla zoldu oddaje o 360 tys. zl w roku wiecej, niz gra zdjela" | Porownane sa dwie rozne rzeczy: cale rozliczenia rodow (zold i inne linie modelu) z samym zoldem oddanym. | Zold naliczony 230 738 660, oddany do obiegu 229 841 212 - oddano o 897 448 MNIEJ, niz naliczono (z pustej kiesy schodzi mniej). Pozostale linie rozliczen rodow dodaly swiatu +1 270 532. Netto +373 084 w roku [P] LOG linie "Pieniadz swiata (rody)", 363 doby. Zrodlem jest wiec nie zold, tylko reszta rozliczenia rodu. Ktora linia - nie wiem. |
| "Karawan lordow 54 -> 21" | 54 to szczyt, nie poczatek. | Doba 2: 26 karawan, doba 30: 56, doba 182: 33, doba 364: 21 [P] LOG linie "Obieg: notable i karawany". |
| "Podatkow placi 4.8%" | Do podatkow mieszczan doliczony podatek wojenny podzamczy. | 4.6% w roku, 5.4% w ostatnich 28 dobach [P]. Drobne. |
| "Kiesy ludu - jedyny zbiornik, ktory rosnie bez konca" | Swiat jest zamkniety, wiec lud rosnie tylko dopoty, dopoki inni traca. | Ostatnie 28 dob: lud miast +70.2 tys. dziennie; w tym samym czasie skarbce -49.3, sakiewki zolnierzy -11.4, notable -9.9, podzamcza -7.2 tys. [P]. Gdy zapas koron sie skonczy, przyrost spadnie do ok. 20-30 tys. dziennie [S]. Tempo juz spada: 144 -> 118 -> 93 -> 74 -> 70 tys. [P]. |

## 4. Niespojnosci analityka - proba obalenia

| Nr | Tytul | Werdykt | Uzasadnienie |
|---|---|---|---|
| N1 | Mieszczanie odkladaja zloto, a korony i panowie chudna | PRAWDZIWA | Liczby co do zlota [P]. Lud wystawia do wydania nadwyzke 4.5 mln zl dziennie, a caly wydatek, ktory gra od niego bierze, to 454 tys. (LOG:129238: "oferta nadwyzki na jutro 4 490 627", "wydatek gry 454 101") - nie ma na co wydac, a podatki to 4.6% zarobku. To jedyny zbiornik bez drogi powrotu. Zastrzezenia: (a) tempo 70 tys. to w 70% lustro zapasu koron, czyli zjawisko pierwszego roku; (b) 98% przyrostu (68.8 z 70.2 tys. dziennie) jest u wyrobnikow, ktorzy placa polowe stawki podatku wojennego (waga 0.5, WarTax.cs:41). |
| N2 | Cwierc luki w wojsku nie ma nic wspolnego z pieniedzmi | CZESCIOWO | Pomiar potwierdzony: 20.5 z 83.9 tys. brakujacych miesci sie w wolnym pulapie rodu [P/S]. Ale baza jest zla: "limit druzyn" to 100% wielkosci kazdej druzyny, a nikt poza 19 rodami go nie osiaga - takze w poprzednim biegu rody z luzem mialy 52%. To nie luka, tylko zwykly stan druzyn, ktore sa stale rozbijane i odbudowywane (u rodow z luzem zolnierz kosztuje 4.76 zl, czyli to swiezy rekrut). Dane pokazuja tylko, ze nie trzyma pulap zoldu ani gotowka (27 mln zl u 155 rodow) - "nic wspolnego z pieniedzmi" jest szersze niz dane. Przyczyna z pojmaniami to domysl [S]. |
| N3 | Skarbce koron wysychaja, renty spadaja razem z nimi | CZESCIOWO | Spadek jest prawdziwy, ale to zaplanowane wydawanie zapasu startowego (regula 1/360 nadwyzki ponad 0.5 mln), czyli zjawisko pierwszych lat kampanii, a nie przeciek. Jest ograniczony: z zapasu idzie dzis 37 tys. dziennie, 8% tego, co korony daja rodom (449 tys.). Zapas nie "skonczy sie" - maleje o polowe co ok. 250 dob. "141 tys." i "36 rodow" to jedna doba. Dlug koron w Banku rosnie - potwierdzam (1 852 110 zl, 20 krolestw, LOG:129102). |
| N4 | Okupy przenosza tyle zlota, ile maja wszystkie rody | CZESCIOWO | Liczby co do zlota. Ale porownanie przeplywu roku ze stanem kies zawyza wrazenie. Uczciwa miara: okupy miedzy rodami to 124 tys. dziennie przy 1 239 tys. wplywow, czyli 10% obrotu rodow. To nie artefakt biegu: w poprzednim biegu bylo 1 190 okupow za 47.9 mln. Skupienie jest prawdziwe: 52 osoby wykupione 5 i wiecej razy to 322 z 1 235 okupow (26%); rod Quarro 37 okupow za 1.57 mln [P]. |
| N5 | Pieniadz korony trafia w wiekszosci do rodow, ktore go nie potrzebuja | CZESCIOWO | 43% rent i 39% zwrotu dla rodow na pulapie - potwierdzone. Ale rody na pulapie to 133 z 307 rodow bez kompanii, czyli tez 43%. Korona nie omija biednych - dzieli rowno, bez celowania. To cecha reguly (renty wedlug lenn, zwrot wedlug zoldu druzyn), nie blad. Wniosek praktyczny zostaje: z nowej zlotowki korony do rodow na pulapie trafia ok. 40 groszy. |
| N6 | W wojnie rody oddaja niewydany sprzet do dworu i jednoczesnie odprawiaja zolnierzy | CZESCIOWO (w ok. 80% artefakt sumowania) | To dwie rozne grupy rodow. Odprawia rod z zoldem ponad 1.10 pulapu przez 3 doby (ponad 1.10 pulapu srednio 59 rodow, z tego zwalnia 43; LOG linie "Budzet rodow (166)", ostatnie 28 dob). Sprzet do dworu wplaca rod, ktorego zold miesci sie w pulapie i ktory ma zloto ponad rezerwe (114 rodow) - tak stanowi kod (CourtFull.cs:180-192). Rody ponad pulapem juz dzis zostawiaja 12.9 tys. dziennie [P]. Prawdziwa reszta: te 12.9 tys. zostaje w kiesie, ale nie podnosi pulapu, wiec zwolnienia i tak ida. |
| N7 | Zolnierze trzymaja w sakiewkach 26 dni zoldu | PRAWDZIWA | 16 591 066 zl = 26.1 dnia [P] LOG:129215. Stoi od doby 168 (16.0-16.9 mln). To stan, nie przeciek. Waga niska - zgoda. |
| N8 | Biedny rod ma drozsze wojsko | ARTEFAKT | 6.88 wobec 4.76 zl potwierdzone, ale to sklad druzyn, nie cena. Trzy z czterech grup placa tyle samo: na pulapie 6.88, pelne druzyny 6.72, kompanie 6.82. Odstaje tylko grupa z luzem (4.76), bo ma swiezych rekrutow. Wsrod zwolnionych w ostatniej dobie zolnierz szczebla 1-2 kosztowal 2.5 zl, szczebla 3-4 6.9 zl, szczebla 5+ 14.4 zl [P] LOG:129141. Porownanie z niewlasciwa baza. |
| N9 | Warsztaty i karawany panow nie daja dochodu | ARTEFAKT (definicja kolumny) | Kolumna d_majatek liczy w praktyce same warsztaty (76 zl w ostatniej dobie). Zysk karawan gra wyplaca linia "karawany i partie" - 18 479 zl w ostatniej dobie - i nasza ksiega zalicza ja w calosci do "przelewow w rodzie" (LOG:129146; ClanIncomeBook.StableD.cs:19-23). Czy karawany lordow zarabiaja na swoj zold - z tych danych nie wiem. Warsztatow lordowie prawie nie maja, bo sami im to ograniczamy (latki B5 w LOG:129143). |
| N10 | Notable traca zloto caly rok | PRAWDZIWA | 28.8 mln (doba 1; 30.5 mln w dobie 2) -> 21.8 mln; doplaty do zoldu karawan 91 877 zl dziennie; 177 z 535 karawan bez pokrycia zoldu [P] LOG:129216. Tempo maleje. Waga niska - zgoda. |
| N11 | Kraje bez wojny zyja z zapasu skarbca i placa z niego najemnikom | PRAWDZIWA (drobna) | Wplywy 835 / 880 / 998 zl, z zapasu 2 624-2 635 zl dziennie - potwierdzone [P]. Ale 83-88% ich wydatkow to renty dla wlasnego, jedynego rodu; kontrakty to 358-589 zl dziennie srednio w roku (2 085 zl w ostatniej dobie u Wysp Letnich). To ta sama regula 1/360 co w N3. Razem trzy kraje: 7.9 tys. dziennie, niecale 2% przeplywow koron. |
| N12 | Drobne przecieki w ksiedze pieniadza | CZESCIOWO | Sumy potwierdzone. Opis "petla zoldu oddaje wiecej" jest bledny (rozdzial 3): zrodlem +373 tys. sa inne linie rozliczen rodow. 628 169 zl bohaterow, ktorzy wrocili do swiata (427 488 w dobie 52, 167 031 w dobie 60) - potwierdzone [P]. Skala: 0.06% zlota swiata w roku. |

## 5. Czego brakuje

Wazne dla pytania Jeffa ("podatki na czas wojny"):

1. **Wojna trwa prawie caly rok.** 25 z 26 wojujacych krolestw ma 340 i wiecej dob wojny, srednio 344 z 364 [P] LOG linie 184. "Podatek na czas wojny" jest wiec podatkiem stalym. Stawka spada tylko w pokoju, a pokoju nie ma. Rozdzial 8 tego nie mowi.
2. **Tempo.** Stawka rosnie o 0.5 punktu co 7 dob i tylko wtedy, gdy krolestwo ma "potrzebe" (kredyt, zwrot ponad wplywy albo skarbiec ponizej zapasu wojny; WarTax.cs:240-247). Z 3% do 12% to 18 tygodni, do 15% to 24 tygodnie. 7 krolestw nie mialo potrzeby w zadnej z ostatnich 28 dob (22% podstawy podatku) - u nich stawka nie ruszy [P].
3. **Kto placi.** Wagi: wyrobnicy 0.5, rzemieslnicy 1.0, patrycjat 1.5 stawki. Przy pulapie 15% patrycjat placi 22.5% zarobku, wyrobnik 7.5%. A oszczednosci rosna tylko u wyrobnikow: +68.8 tys. dziennie wobec +0.3 i +1.0 tys. u pozostalych [P] LOG linie Kiesa ludu. Brak policzonego wariantu "wyzsza waga wyrobnikow" albo "podatek od lokaty".
4. **Druga runda.** Z kazdej zlotowki zoldu zolnierze wydaja w miastach i wsiach 61-64 grosze [P] LOG linie Obieg. Podatek wraca wiec do ludu jako zarobek. Zdanie "pulap 15% zatrzyma rosniecie kies ludu" jest szersze niz dane.
5. **Podatek od lupu i okupow nie jest policzony.** Lupy i okupy to 27% wplywow rodow (335 tys. dziennie). Korona bierze z nich 1/9: 28.8 tys. dziennie w roku, 30.8 tys. pod koniec [P]. Kazda nastepna dziewiata w wojnie to ok. 30 tys. dziennie [S] - tyle, co podniesienie podatku ludu o 5-6 punktow - i bierze od rodow, ktore wygrywaja i maja wolny pulap.
6. **Dlaczego wojsko spadlo ze 105 do 79 tys.** Zold druzyn: doby 57-84 489 tys., doby 85-112 478 tys., ostatnie 28 dob 456 tys. dziennie (-5 do -7%). Ludzi: 105.1 -> 98.0 -> 78.7 tys. (-20 do -25%). Zolnierz podrozal: 4.65 -> 4.88 -> 5.80 zl [P] CSV-R, skrypt 05. Prog 85 tys. byl ustawiany w dobach 90-120, przy tanszym zolnierzu. Zeby dzis trzymac 85 tys., druzyny potrzebuja ok. 32-37 tys. zl zoldu dziennie wiecej [S].
7. **Nowy rekrut jest tani.** Analityk przelicza zloto na ludzi po 6.88 zl (srednia rodu na pulapie). Nowy rekrut szczebla 1-2 kosztuje 2.5 zl [P] LOG:129141. W pierwszych miesiacach ten sam pieniadz da wiec wiecej glow, potem ludzie awansuja i liczba wraca do rachunku analityka. Rachunek "ile ludzi" ma przez to szeroki margines.
8. **Stan nie jest jeszcze ustalony.** Wojsko stoi od doby 225 (srednie 7 dob: 77.5-81.2 tys.) - potwierdzam. Ale pieniadz rodow dalej sie psuje: rodow na pulapie 103 -> 133, rodow ze zlotem ponizej rezerwy 98 -> 109, suma pulapow 1 050 -> 985 tys. miedzy dobami 225-252 a 337-364 [P] skrypt 05. Analityk pisze o tym tylko przy rentach.
9. **Miara progu.** Prog 85 tys. liczy sie z innej miary: 79 409 (druzyny lordow krolestw w wojnie, srednia 28 dob; PROGI etap2.txt wiersz 12). Analityk uzywa 78 693 z tabeli rodow. Do progu brakuje 5.6 tys., nie 6.3 tys.

Liczby bez zrodla albo slabo oparte:

10. "Ok. 630 druzyn" przy przyczynie N2 - bez zrodla (linia "Zold" podaje 621 partii rodow w ostatniej dobie, LOG:129132).
11. "Gorna granica ok. 97 tys." - rachunek sie zgadza (75.8 tys. limitu x 24 punkty = 18.2 tys.), ale zaklada, ze rod z pieniedzmi dojdzie do 58% i nie wiecej. To zalozenie, nie pomiar.
12. "Bez podatku panom ubedzie 35 tys. rent i 2-4 tys. ludzi" - pierwsza liczba ma gorna granice z pomiaru (37 tys.), druga jest z przeliczenia bez pomiaru skutku.
13. Propozycja B (zwrot zoldu zalog): brak sprawdzenia, ile zoldu zalog juz dzis wraca przez zawor kas. W ostatniej dobie zold zalog poszedl do kas miast 98.8 tys. i do kas zamkow 44.4 tys. (LOG:129132), a zawor oddal z tego panom jako "wlasne" 28.3 tys. (LOG:129146). Nadwyzke kasy miasta zawor dzieli miedzy pana i korone, wiec czesc zoldu zalog trafia tez do korony. Ile - nie wiem, w danych nie ma rozbicia.
14. Moje odtworzenie przydzialu sprzetu z reguly daje 271.8 tys. dziennie wobec 244.1 tys. w logu (o 11% za duzo) - szacunki z rozdzialu 3 o sprzecie maja taki margines.

## 6. Moje skrypty

| Skrypt | Co liczy |
|---|---|
| 00_prefixy.py | spis rodzajow linii logu |
| 01_log.py | wyciag liczb dobowych z obu logow (wyniki\log_nowy.json, log_stary.json) |
| 02_liczby_log.py | zloto swiata, korony, kiesa ludu, budzet, okupy, niewola, podatek wojenny (wyniki\02_liczby_log.txt) |
| 03_csv.py | rody: zloto, wplywy, zold, grupy, luka do limitu, pulap na zlotowke dochodu (wyniki\03_csv.txt) |
| 04_dodatki.py | para zoldu, kraje bez wojny, notable, sakiewki, trend wojska i rent (wyniki\04_dodatki.txt) |
| 05_okna.py | 13 okien po 28 dob: ludzie, pulap, zold, koszt zolnierza (wyniki\05_okna.txt) |
| 06_druga_runda.py | ile zoldu wraca do miast i wsi (wyniki\06_druga_runda.txt) |
