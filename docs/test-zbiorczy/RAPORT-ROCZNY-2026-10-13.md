# RAPORT ROCZNY - bieg 364 doby, nowa kampania, sam swiat AI (13.10.2026)

Bieg: nowa kampania, 364 doby (doba 1 = dzien gry 108837), rod gracza stoi i nic nie robi.
Kod: galaz w-toku/zbior-e8 9e2e0e6 (WGRANIE 36 + poprawki wojska i zelaza "rundy 3" + reguly slubow NPC). Ten kod jest w grze od WGRANIA 37 (13.10, 16:17).
Wynik biegu: 364 z 364 dob, 0 linii ERROR w logu Armoury, srednio 12.0 s na dobe.
Porownanie: poprzedni bieg roczny (WGRANIE 34, bez rundy 3) - ten sam start, te same 364 doby.
Praca tylko do odczytu. Nic nie uruchamiano, nic nie zmieniano. Zadna liczba o podatkach nie byla sprawdzona biegiem - to rachunek na pomiarach.

## Jak czytac

- **[P]** = pomiar z logu albo tabeli. **[S]** = szacunek (rachunek na pomiarach). **(wer)** = liczba albo opis poprawiony przez weryfikatora - tam, gdzie analityk i weryfikator sie roznia, ten raport bierze wersje weryfikatora.
- Zrodla danych:
  - LOG:n = D:\Backup-Bannerlord\logs\2026-10-13-noc34-35\Armoury-2026-10-13_06-24-10.log, linia n. LOG34 = ...\Armoury-2026-10-13_02-15-21.log (poprzedni bieg).
  - CS:n = ...\session-2026-10-13_06-24-09.log, linia n (log CrashScribe).
  - Tabele z ...\Logs\2026-10-13_06-24-10\: BUD = budzet-rodow.csv, BAL = balans-krolestw.csv, LR = ludzie-regiony.csv, KM = kasy-miast.csv, KLU = kiesa-ludu.csv, BIT = bitwy.log.
  - PROGI eN:n = scratchpad\test\progi-rok5\etapN.txt, linia n.
- Rozdzialy analitykow i weryfikacje (scratchpad dbc3d3d1\raport-rok\): R-WOJ = wojsko.md, R-PIE = pieniadz.md, R-GOS = gospodarka.md, R-WNA = wojna.md, R-PRO = progi.md; z dopiskiem "-wer" = plik weryfikatora (np. R-WOJ-wer). Skrypty obok, w podkatalogu skrypty\.
- Moje wlasne sprawdzenia (tam, gdzie rozdzialy sobie przeczyly): skrypty\raport\r1_grupy.py (wynik r1_grupy.out.txt) i odczyty linii logu podane przy liczbie.
- Rozdzial 0 jest pisany prostym jezykiem, bez znacznikow i zrodel przy liczbach. Kazda jego liczba stoi ze zrodlem i znacznikiem w rozdzialach 1-7 (stan planu: pkt 5.5).
- Srednia 28 dob = doby 337-364. Kwartaly: Q1 = doby 1-91, Q2 = 92-182, Q3 = 183-273, Q4 = 274-364. Kwoty w zlocie gry (zl), "na dobe" = dziennie.
- Trzy miary wojska lordow (nie mylic): "druzyny w wojnie" = miara progu 85 tys. (linie "Skarbce", druzyny krolestw w wojnie, z bohaterami i kompaniami w sluzbie); "druzyny rodow" = linia "Ludzie" (z Innymi); BUD = sami szeregowi (ok. 1 tys. mniej).

## 0. Dla Jeffa - prostym jezykiem
### (a) Na jakim etapie jestesmy
- Plan z 9 pazdziernika mial 8 etapow i zakonczenie. Kod wszystkich 8 etapow jest w grze od 13 pazdziernika. Ostatnia duza paczka (poprawki wojska i zelaza, sluby rodow) tez jest juz wgrana - ten bieg roczny byl jej sprawdzianem.
- Bieg wyszedl czysto: 364 z 364 dob, zero bledow naszych modow, 12 sekund na dobe gry.
- W robocie sa: hamulec slubow (test), danina z oszczednosci mieszczan (kod), lup zdobytego miasta (projekt). W kolejce: konie robocze mieszczan, lud doklada do budowy miasta, podatki wojenne (Twoje "1, 2 i 3").
- Z planu zostalo zakonczenie: strojenie liczb biegami rocznymi, przyspieszanie, porzadki, test czteroletni z zima. Do tego Twoje testy reczne (29 punktow) i decyzja o drogach.
### (b) Wojsko
- W polu stoi 79 tysiecy ludzi lordow (srednia z ostatnich 4 tygodni). Poprzedni bieg roczny mial 64 tysiace. Szczyt byl w 68. dobie: 108 tysiecy.
- Ostatni dzien pokazuje 74 tysiace, bo tego dnia rozbito trzy armie. To dolek, nie trend. Wojsko stoi tygodniami, a po wielkiej bitwie spada o 4 tysiace i juz tego nie odrabia.
- Do 85 tysiecy brakuje 5.6 tysiaca. Powod pierwszy: zolnierz zdrozal. Wojsko awansowalo i czlowiek kosztuje 5.78 zl dziennie zamiast 4.64. Panowie placa prawie tyle zoldu co w szczycie, ale starcza to na cwierc mniej ludzi.
- Powod drugi: okolo 100 z 205 panow z ziemia doszlo do granicy tego, co wolno im wydac na zold. Ich druzyny sa wypelnione w 37%. Zalogi twierdz sa oplacane pierwsze i u 47 panow zjadaja caly budzet wojska.
- Powod trzeci: pan, ktoremu pieniedzy nie brakuje, i tak trzyma okolo 60% druzyny. Dlaczego - nie wiem. Dlatego pieniadzem da sie dojsc najwyzej do ok. 96-105 tysiecy.
- Najwiecej ludzi w polu: Polnoc 8.3 tys., Krolewska Przystan 6.0, Dorne 5.8. Prawie nic: Wolni Ludzie 0.6 tys., Ibben 0.4, Zelazne Wyspy 1.6.
### (c) Kto ile zarabia i gdzie lezy zloto
- Zlota w swiecie jest stale 208 milionow (zmiana w roku +0.06%). Nic nie powstaje z niczego.
- Wszyscy panowie razem zarabiaja 1.24 mln zl dziennie: 40% daje korona, 27% lup i okupy, 16% ziemia. Polowa idzie na zold. Najbogatsi: Stormcrows 1.5 mln, Fowler 1.3, Bolton 1.2, Tully 1.1. Najbiedniejsi panowie z zamkiem maja po 14 tys. (Lydden, Kasgos, Orthys).
- Korony zbieraja 444 tys. zl dziennie i wszystko oddaja panom. Dokladaja tez z zapasu startowego: skarbce spadly z 58 do 27 mln. To zaplanowane, ale doplata gasnie (dzis 37 tys. dziennie, za rok ok. 14 tys.).
- Najwiekszy dochod korony ma Polnoc: 48 tys. dziennie. Najmniejszy w wojnie: Ibben, Nocna Straz, Pentos i Wolni Ludzie, po 5-6 tys. Najgorzej stoi Braavos: skarbiec 0.5 mln i 0.66 mln dlugu w Banku.
- Gdzie lezy zloto: glowy rodow 47 mln, mieszczanie 44 mln, skarbce koron 27 mln, notable 22 mln, sakiewki zolnierzy 17 mln, kupcy miast 11 mln, wsie 10 mln.
- Mieszczanie zaczynali z 6 mln. Odkladaja, bo polki sa puste: chca wydac 4.5 mln dziennie, a kupuja za 0.45 mln.
### (d) Gospodarka
- Jedzenie: caly rok zero glodu. Ale w tym roku nie bylo zimy - przyjdzie okolo 560. doby.
- Zelazo naprawione: miast bez rudy bylo 44, jest 7. Ceny ida za brakami we wszystkich 7 surowcach.
- Caly rok brakuje plotna (89 z 97 miast), skory (42 miasta) i dobrej zbroi (129 sztuk na polkach przy braku ok. 5 tysiecy). Za duzo jest drewna (12 -> 153 tys. ladunkow) i taniego zlomu z pobojowisk.
- Budowy cywilne stoja w 198 z 227 osad, bo czekaja na pokoj. W 9 miastach ludu nie stac na pelne jedzenie; na poczatku roku bylo takie jedno.
### (e) Wojna
- Naraz toczy sie 15-18 wojen. 25-26 z 29 krolestw wojuje caly rok. 14 wojen wymusza fabula Realm of Thrones: krolestwa 73 razy zawarly pokoj i 73 razy wojna wrocila tego samego dnia.
- Dla 10 z tych 14 wojen przymus konczy sie w 433. dobie, czyli 69 dob po koncu tego biegu. 4 trwaja dalej z zamyslu fabuly (wojna krolow w Westeros, Mur). Co bedzie potem - nie wiem, bieg tego nie objal.
- Bitew lordow bylo 1 869; silniejszy wygral 98.9%. Mapa prawie stoi: 14 twierdz z 227 zmienilo pana, kazda raz. Reach i Stormlands +4, Dragonstone i Dorne -4. Zadne krolestwo nie upadlo.
- Cena: 147 tysiecy zabitych w druzynach lordow, czyli ponad 10 tysiecy na jedna zdobyta twierdze.
### (f) Werdykt: czy swiat jest spojny
#### Co trzyma sens
1. Zloto jest zamkniete: 208.20 -> 208.34 mln w roku, zero wyplat bez platnika.
2. Ludzie sa policzeni: ludnosc -1% w roku wojny, ksiega ludzi sie zamyka.
3. W bitwie wygrywa silniejszy, a twierdza pada dopiero przy przewadze 3.6 do 1.
4. Zold jest placony: zaleglosci to 0.12% zlota, dezercja 17 ludzi dziennie.
5. Zaden rod nie zbankrutowal, zadne krolestwo nie upadlo, nikt nie rosnie jak kula sniezna (najwiecej +4 twierdze).
6. Ceny reaguja na braki, a zelazo po poprawce dociera do miast.
7. Zero bledow naszych modow przez 364 doby; po 120. dobie gra nie zwalnia.
#### Co NIE trzyma sensu (od najwazniejszego)
1. Wojna sie nie konczy: 73 pokoje i 73 wojny wznowione tego samego dnia. Podatek "na czas wojny" jest przez to w pierwszym roku podatkiem stalym.
2. Wojska jest 79 tys. przy celu 85 tys.; ok. 100 panow ma druzyny w 37%, bo zalogi i drozszy zolnierz zjadly im budzet.
3. Armia oblegajaca, pobita wypadem z twierdzy, ginie cala: 38 razy w roku, 55 tysiecy ludzi.
4. Zloto osiada u mieszczan (6 -> 44 mln), a podatek wojenny bierze od nich tylko 15 tys. zl dziennie.
5. Korona dzieli pieniadz wedlug ziemi, nie wedlug potrzeby: panowie, ktorym brakuje na zold, dostaja 45% rent.
6. Plotna, skory i dobrej zbroi nie ma caly rok (plotna brak w 89 z 97 miast), a drewna jest 13 razy wiecej niz na starcie.
7. Budowy cywilne stoja ponad 300 dni w 144 osadach, bo pokoj nie przychodzi.
8. Wolni Ludzie, Ibben i Zelazne Wyspy maja druzyny wypelnione w 10-19% - prawie bez wojska polowego.
9. Jazdy jest 17.6% przy celu 27.5%; 5.5 tys. ludzi czeka na konia.
10. Najemnicy zmieniaja pana co 3 doby (764 razy w roku), te sama osobe wykupuje sie z niewoli do 11 razy, a 440 lordow-jencow wyszlo za darmo przy pokoju na zero dni.
11. Trzy rzeczy bez wyjasnienia w ksiegach: 56 tys. koni jucznych z niczego, 125 tys. sztuk zboza i ryb bez zrodla, miasto Thenn pol roku bez pieniedzy.
### (g) Co proponujemy naprawic i w jakiej kolejnosci
1. Pieniadz na wojsko - odpowiedz na Twoje pytanie o podatki. Danina z oszczednosci mieszczan da koronom ok. 50-75 tys. zl dziennie. Rozdana jak dzis (wszystkim panom wedlug ziemi) da tylko 1.5-4 tys. ludzi, czyli 81-83 tys. wojska. Gdy korona da ja najpierw panom, ktorym brakuje na zold, na 85 tys. wystarczy ok. 50 tys. zl dziennie. To rachunek, nie pomiar.
2. Drugie zrodlo: oplata od pana, ktory ma pieniadze, a nie wystawia ludzi (lezy u nich ok. 10 mln ponad rezerwe). Wyzszej stawki podatku pana ten raport nie policzyl. Nie brac wiecej ze wsi, od kupcow, notabli i z podzamczy - tam nadwyzki nie ma. Zelaznym Wyspom, Wolnym Ludziom i Ibbenowi podatek od ludu nie pomoze, bo ich lud odklada za malo albo wcale.
3. Pobity oblegajacy ma uchodzic jak przegrany w polu, a nie ginac caly (dzis to 55 tysiecy ludzi rocznie).
4. Bieg dwuletni, zanim ruszymy wojny bez konca, jencow, uchodzcow i bandy. Przymus 10 wojen konczy sie w 433. dobie, zima przychodzi ok. 560. - czesc klopotow moze zniknac sama, a zima moze dodac nowe.
5. Towary: wiecej lnu i skor u zrodla, wiecej platnerzy do dobrej zbroi, hamulec wyrebu drewna, budowy cywilne z dala od frontu.
6. Male poprawki: najkrotsza sluzba najemnikow, przerwa po wykupie z niewoli, konie juczne i zboze w ksiegach, rozpoznanie Thenn.
7. Na koniec jak w planie: strojenie liczb, przyspieszanie, test czteroletni. Kazda zmiana z punktow 1-3 wymaga biegu 120 dob przed wgraniem.

## 1. Wojsko

### 1.1 Ile jest wojska

W kazdej komorce: **ten bieg / poprzedni bieg**. Wszystko [P].

| co | doba 1 | doba 91 | doba 182 | doba 273 | doba 364 | srednia 28 dob |
|---|---|---|---|---|---|---|
| **druzyny krolestw w wojnie (miara progu 85 tys.)** | 2 535 / 2 503 | 99 299 / 94 992 | 88 205 / 84 311 | 86 897 / 73 029 | **73 578** / 62 043 | **79 409** / 64 013 |
| druzyny rodow razem (z Innymi) | 29 896 / 29 679 | 102 433 / 97 735 | 90 344 / 87 081 | 89 879 / 76 538 | 77 837 / 67 015 | 83 170 / 68 743 |
| zalogi miast i zamkow | 47 356 / 47 341 | 80 219 / 77 828 | 84 191 / 81 493 | 82 056 / 77 627 | 83 593 / 74 147 | 83 593 / 74 122 |
| milicje | 62 991 / 63 044 | 64 316 / 65 452 | 68 403 / 70 258 | 71 281 / 72 481 | 74 006 / 74 947 | 73 617 / 74 581 |
| karawany | 11 881 / 11 862 | 17 335 / 15 180 | 18 457 / 15 984 | 18 113 / 15 535 | 18 085 / 15 070 | 17 913 / 15 078 |
| tabory wsi | 2 878 / 2 873 | 15 199 / 15 416 | 14 687 / 14 798 | 14 552 / 14 592 | 14 666 / 14 747 | 14 563 / 14 428 |
| bandy - ludzie | 5 694 / 5 822 | 10 977 / 10 024 | 14 464 / 13 730 | 15 353 / 17 535 | 17 279 / 20 501 | 17 055 / 20 021 |
| pula wyrzutkow (ludzie w lesie) | 819 / 692 | 2 257 / 2 386 | 8 629 / 4 934 | 10 785 / 7 675 | 11 868 / 9 788 | 11 449 / 9 071 |
| Inni (siedza w "druzynach rodow") | 616 / 616 | 1 244 / 2 055 | 2 001 / 2 982 | 2 838 / 3 508 | 3 699 / 5 084 | 3 490 / 4 840 |
| WSZYSCY pod bronia | 165 159 / 165 374 | 297 955 / 294 237 | 299 292 / 299 142 | 299 937 / 285 470 | 295 576 / 273 116 | 300 111 / 273 453 |
| druzyny + zalogi jako % mezczyzn 16-60 | 0.55 / 0.55 | 1.31 / 1.25 | 1.25 / 1.21 | 1.23 / 1.11 | 1.16 / 1.01 | 1.20 / 1.03 |

Zrodla: linie "Ludzie:" LOG:770, 32321, 64561, 96942, 129266; LOG34:739, 32160, 64316, 96635, 129031; linie "Skarbce:" LOG:603-631 ... 129103-129131; wyrzutki LOG:129134; Inni LOG:129155 (R-WOJ pkt 1; sprawdzone w R-WOJ-wer nr 1-4, 10).

- Szczyt: 108 131 w dobie 68 [P, LOG:24069]. Poprzedni bieg: szczyt 98 471 w dobie 82.
- Wobec poprzedniego biegu +24% w miarze progu [P]. Uwaga (wer): to roznica dwoch pojedynczych biegow i kilku zmian naraz (WGRANIE 35-36, runda 3, sluby). Rozrzut miedzy biegami znam tylko z jednej pary okien: test 120 dob mial 101 144, bieg roczny w tych samych dobach 93-120 mial 95 428, czyli 5.7 tys. roznicy - tyle, ile brakuje do progu [P, R-WOJ-wer A].
- Kompanie najemne (11): 2 425 ludzi przy umowie 2 970 = 82% (srednia 28 dob); doba 364: 2 462 przy 3 008 [P, LOG:129095] (wer - analityk podal 74%, bo pomieszal dwie miary). Umowa placi za ludzi pod bronia, wiec stan ponizej umowy jest zgodny z regula.
- W karczmach czeka 8 456 weteranow [P, LOG:129162], w lesie 10 571 bylych zolnierzy [P, LOG:129134]. Ludzi nie brakuje.
- W ostatnim kwartale wojsko w mierze progu bylo ponad 75 tys. w 90 z 91 dob, ponad 80 tys. w 54 z 91; ostatnia doba ponad 85 tys. to doba 276 [P, R-WNA-wer 4.5].

### 1.2 Tabela krolestw

Druzyny, zalogi, limit: BUD (kolumny ludzi_partie, ludzi_zalogi, limit_partii; doby 1 i 364; szeregowi). Doby wojny: linie "Skarbce" z dopiskiem (WOJNA). Tier, t3+ (ludzie szczebla 3 i wyzej) i jazda: linia "Musztra AI wedlug krolestw" LOG:1780 (doba 4) i LOG:129179 (doba 364). Wszystko [P] (R-WOJ pkt 2).

| krolestwo | rody | druzyny d1 | druzyny d364 | druzyny sr. 28 dob | limit druzyn d364 | % limitu sr. 28 | zalogi d1 | zalogi d364 | doby wojny | sredni tier d4 -> d364 | t3+ | jazda |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| The North | 26 | 2 374 | 7 632 | 8 272 | 13 930 | 60% | 4 204 | 9 394 | 355 | 2.58 -> 3.27 | 75% | 11% |
| House Baratheon of King's Landing | 28 | 1 138 | 6 009 | 5 977 | 12 773 | 54% | 4 336 | 6 989 | 355 | 2.22 -> 3.09 | 64% | 11% |
| The Reach | 17 | 2 000 | 5 833 | 5 662 | 12 444 | 47% | 3 104 | 6 093 | 342 | 2.45 -> 2.55 | 45% | 6% |
| The Vale | 21 | 1 887 | 5 455 | 5 591 | 9 533 | 59% | 2 710 | 5 663 | 342 | 2.31 -> 3.19 | 69% | 14% |
| Stormlands | 14 | 2 141 | 5 166 | 5 242 | 8 423 | 59% | 2 634 | 5 635 | 363 | 2.43 -> 2.96 | 58% | 6% |
| Dorne | 42 | 751 | 4 310 | 5 832 | 7 710 | 57% | 2 995 | 4 683 | 342 | 2.64 -> 3.06 | 62% | 15% |
| Nights Watch | 6 | 1 172 | 4 058 | 4 070 | 5 196 | 78% | 836 | 1 737 | 364 | 2.54 -> 3.78 | 97% | 9% |
| Volantis | 9 | 1 168 | 3 579 | 2 513 | 7 150 | 44% | 1 739 | 2 879 | 342 | 2.68 -> 3.07 | 66% | 13% |
| Dragonstone | 11 | 1 961 | 3 402 | 3 424 | 6 473 | 46% | 2 075 | 3 166 | 363 | 2.52 -> 2.95 | 60% | 5% |
| Dothraki Horde | 17 | 704 | 2 982 | 2 605 | 5 123 | 57% | 1 357 | 2 232 | 342 | 2.44 -> 2.09 | 31% | 45% |
| Riverlands | 15 | 2 918 | 2 397 | 5 615 | 7 107 | 48% | 3 118 | 4 767 | 277 | 2.63 -> 2.38 | 39% | 6% |
| Tyrosh | 6 | 278 | 2 315 | 2 106 | 3 162 | 63% | 1 065 | 2 032 | 342 | 2.50 -> 2.75 | 57% | 4% |
| House Targaryen | 9 | 1 132 | 2 205 | 2 378 | 6 111 | 44% | 1 691 | 2 236 | 342 | 3.56 -> 3.44 | 67% | 26% |
| Myr | 7 | 573 | 1 948 | 1 789 | 3 957 | 56% | 1 153 | 1 812 | 342 | 2.55 -> 3.01 | 63% | 12% |
| Qarth | 5 | 363 | 1 811 | 2 916 | 2 496 | 80% | 1 207 | 1 849 | 342 | 2.31 -> 2.47 | 44% | 13% |
| Lorath | 6 | 769 | 1 580 | 1 740 | 3 280 | 46% | 983 | 1 661 | 342 | 2.87 -> 2.94 | 58% | 7% |
| House Targaryen, Aegon | 7 | 273 | 1 580 | 1 779 | 4 181 | 47% | 1 178 | 1 732 | 342 | 2.43 -> 2.51 | 45% | 13% |
| Braavos | 6 | 1 028 | 1 510 | 1 959 | 3 076 | 56% | 1 101 | 2 097 | 342 | 2.61 -> 2.76 | 52% | 6% |
| Iron Islands | 30 | 1 209 | 1 400 | 1 578 | 7 957 | 19% | 2 325 | 4 860 | 342 | 2.55 -> 2.90 | 62% | 0% |
| Sarnor | 7 | 316 | 1 377 | 1 288 | 5 010 | 27% | 1 021 | 633 | 342 | 2.17 -> 3.44 | 76% | 16% |
| Qohor | 6 | 277 | 1 342 | 1 246 | 3 643 | 38% | 1 043 | 1 730 | 342 | 2.43 -> 3.00 | 63% | 16% |
| Pentos | 12 | 398 | 1 271 | 1 157 | 3 606 | 42% | 1 006 | 1 966 | 342 | 2.92 -> 3.00 | 67% | 9% |
| Norvos | 6 | 683 | 1 243 | 1 398 | 2 675 | 40% | 1 029 | 2 014 | 342 | 2.36 -> 3.08 | 59% | 12% |
| Lys | 6 | 291 | 1 055 | 1 306 | 2 757 | 42% | 1 069 | 1 071 | 342 | 2.44 -> 2.40 | 40% | 5% |
| Free Folk | 12 | 1 324 | 696 | 566 | 6 029 | 10% | 1 210 | 2 797 | 364 | 2.19 -> 2.14 | 35% | 0% |
| Ibben | 6 | 362 | 508 | 413 | 3 720 | 13% | 797 | 1 660 | 342 | 2.58 -> 2.76 | 58% | 2% |
| Summer Isles (pokoj) | 5 | 58 | 528 | 194 | 1 946 | - | 97 | 48 | 0 | 2.44 -> 2.63 | 58% | 1% |
| Skagos (pokoj) | 2 | 139 | 237 | 233 | 884 | - | 176 | 89 | 0 | 2.48 -> 3.27 | 83% | 93% |
| Yi Ti Exiles (pokoj) | 2 | 53 | 67 | 153 | 301 | - | 97 | 68 | 0 | 2.34 -> 2.22 | 31% | 9% |
| **RAZEM** | 346 | 27 740 | 73 496 | 79 001 | 160 653 | 48% | 47 356 | 83 593 | - | 2.64 -> 2.96 | 59% | 11% |

Uwagi:
- "Rody" to wszystkie rody AI krolestwa w dobie 364, takze rycerze bez lenna i kompanie najemne w sluzbie. Kompanie zmieniaja pana co kilka dob, wiec male krolestwa skacza (Qarth: srednia 28 dob wyzsza niz limit z doby 364).
- Limit druzyn to gorna granica gry, nie cel. Prog 85 tys. to ok. 52% tego limitu.
- Riverlands w dobie 364 ma 2 397, bo tego dnia stracilo dwie armie (pkt 1.7); srednia 28 dob to 5 615.
- Najslabsze: Free Folk 10%, Ibben 13%, Iron Islands 19% - trzyma je pulap zoldu (pkt 1.6). Sarnor 27% - tam zabraklo ludzi: region przekroczyl prog wolnych rak (obciazenie 22.4% przy progu 20%, LOG:129270), 328 odmow ochotnikow dziennie (LOG:128936) [P].
- Linia logu "Straz i Wolni Ludzie" podaje 9 051 ludzi w partiach Wolnych Ludzi, ale liczy tez milicje, tabory wsi i karawany. Druzyn lordow Wolnych Ludzi jest 696 [P, BUD doba 364] (wer).

### 1.3 Sklad i wyposazenie

| co | doba 1 | doba 91 | doba 182 | doba 273 | doba 364 | poprzedni bieg d364 |
|---|---|---|---|---|---|---|
| sredni tier druzyn | 2.64 | 2.49 | 2.71 | 2.80 | 2.96 | 3.04 |
| sredni tier zalog | 2.71 | 2.28 | 2.27 | 2.32 | 2.31 | 2.32 |
| ludzie t3+ w druzynach | 47% | 41% | 52% | 55% | 59% | 57% |

[P] BAL (tier wazony ludzmi), linia "Rycerze i jazda (219)" LOG:702, 32256, 64496, 96877, 129201 (R-WOJ 3.1). Pelnego rozkladu t1-t6 w logu nie ma - nie wiem.

| procent ludzi druzyn z dana rzecza | d4 | d89 | d184 | d274 | d364 | zalogi d364 | poprzedni bieg d364 |
|---|---|---|---|---|---|---|---|
| zbroja na tulow wlasciwego szczebla | 96% | 70% | 67% | 70% | 70% | 89% | 67% |
| glowna bron wlasciwego szczebla | 97% | 82% | 83% | 86% | 85% | 98% | 82% |
| helm | 90% | 44% | 54% | 62% | 66% | 89% | 63% |
| tarcza | 90% | 38% | 52% | 63% | 73% | 93% | 66% |
| cokolwiek na tulowiu | 97% | 88% | 92% | 92% | 92% | 99% | 91% |
| brakuje sztuk razem | 22 619 | 214 275 | 181 172 | 158 792 | 131 386 | - | 124 982 |

[P] linia "Pokrycie zbrojowni AI (171)": LOG:1774, 31531, 65197, 97225, 129173 (R-WOJ 3.3). Doba 4 to darmowy komplet startowy, nie uczciwa baza (wer).

- Zbroja wlasciwego szczebla stoi na 67-72% od doby 29 do konca w obu biegach - stan trwaly [P, R-WOJ-wer G]. Helm i tarcza poprawiaja sie caly rok.
- Powod: brak towaru, nie zlota. W dobie 364 brakuje 30 255 zbroi na tulow, a na polkach calego swiata lezy 129 zbroi t3-4 i 98 zbroi t5-6 [P, linia "Zbroja na polkach (174b)", LOG:129174].
- Przydzial rodow na sprzet: srednio 244 127 zl dziennie, wydane 85 214 (35%) [P, linie "Budzet rodow (166)", srednia 28 dob] (wer - doba 364 to dzien wyjatkowo duzych zakupow). Niewydane po 30 dniach nie przepada, tylko idzie do kasy siedziby rodu (87 475 zl dziennie) (wer).
- Jazda: wsrod ludzi t3+ jest 17.6% jazdy przy celu 27.5% [P, LOG:129201]; stoi na 16.9-17.8% od doby 85 w obu biegach [P, R-WOJ-wer F]. Czeka na konia 5 453 ludzi w 294 druzynach [P, LOG:129202]. Wolnych koni w zbrojowniach jest 2 328, wiec nawet po rozwiezieniu 3.1 tys. zostaje bez konia (wer). Nieudane zakupy: z braku koni 21.9 dziennie (17.0 przy pustej polce), z braku zlota 2.4 [P, srednia 28 dob]. Progi podaja tez siodla i cene (PROGI e5:362-378).
- Odziez: czeka na zakup 2 852 skory, 2 133 sukna, 5 927 plotna w 631 druzynach i zalogach; powod to pusta polka (39 wizyt bez towaru, 0 z braku zlota) [P, LOG:129133].

### 1.4 Przeplyw ludzi na dobe

Srednie na dobe. Wszystko [P] (R-WOJ pkt 4, skrypt b_przeplyw; linie "Ludzie", "Partie (E4/0)", "Zalogi (169c)", "Budzet rodow (166)", "Jency (E4/0)").

| przeplyw | Q2 | Q3 | Q4 | 28 dob | poprzedni bieg, 28 dob |
|---|---|---|---|---|---|
| WEJSCIA do druzyn: werbunek wodzow razem | 688 | 666 | 588 | 535 | 608 |
| - od notabli (ochotnicy) | 605 | 559 | 493 | 434 | 510 |
| - z karczmy (weterani) | 79 | 84 | 81 | 76 | 79 |
| nowe druzyny (ludzie z wolnych rak regionu siedziby) | 436 | 513 | 442 | 455 | 535 |
| z zalog do druzyn | 338 | 378 | 302 | 302 | 356 |
| WYJSCIA z druzyn: zabici | 474 | 503 | 398 | 398 | 392 |
| zwolnieni przez budzet rodu (pulap zoldu) | 270 | 215 | 314 | 332 | 520 |
| wzieci do niewoli | 255 | 348 | 241 | 321 | 186 |
| z druzyn do zalog | 220 | 210 | 165 | 153 | 186 |
| dezercja | 24 | 20 | 19 | 17 | 19 |
| rozbici po przegranej (wszystkie rodzaje partii) | 730 | 717 | 759 | 691 | 864 |

- Rok razem [P]: wodzowie zwerbowali 293 288 ludzi, w druzynach zginelo 146 593, budzet zwolnil z druzyn ok. 85 tys. (poprzedni bieg: 290 633 / 132 032 / ok. 129 tys.).
- Dezercja z druzyn w ostatnich 28 dobach wedlug przyczyny: glod 6.0, limit gry 5.0, zalegly zold 3.0, morale 2.1 na dobe; morale srednio 68.9 [P, linia "Dezercja AI wedlug przyczyny (169c)"].
- Rozbici: 449 dziennie wraca do domu, 212 idzie w las, 31 do karczmy [P, linia "Wyrzutki"]. Nic nie znika: "zniklo 0" [P, LOG:129141, LOG:129134].
- Lordowie w niewoli: srednio 42 naraz, mediana 4 doby, nikt ponad 60 dni [P].

### 1.5 Czy wojsko topnieje

Analityk: -36 ludzi na dobe od doby 183 (regresja -35.7, zgodna). Weryfikator: to srednia ze schodkow, nie rowny ubytek [P, R-WOJ-wer A, skrypt w08]:

| okres | co sie dzialo |
|---|---|
| doby 197-276 | +1.2 na dobe - wojsko stoi na ok. 83 tys. |
| doba 277 | -3 999 w jedna dobe |
| doby 309-363 | +11.2 na dobe - wojsko stoi na ok. 79.4 tys. |
| doba 364 | -3 858 w jedna dobe |

- Wojsko nie topnieje, tylko nie odrabia strat po wielkiej bitwie, bo rody sa na pulapie zoldu.
- Stanu stalego nie ma: rodow na pulapie przybywa (pkt 1.6), doplata koron z zapasu gasnie (pkt 2.3). Bez nowego pieniadza takze 79 tys. sie nie utrzyma [S]. Gdzie wojsko sie zatrzyma - nie wiem.

### 1.6 Anatomia luki do 85 tys.

**Ile brakuje do limitu druzyn i co to trzyma** (rody krolestw w wojnie, srednia 28 dob; podzial [S] na danych [P] z BUD; R-WOJ 5.1, potwierdzone w R-WOJ-wer nr 8):

| grupa | brak ludzi | udzial | poprzedni bieg |
|---|---|---|---|
| (a) brak miejsca w pulapie zoldu | 69 952 | 70.8% | 87 584 (76.8%) |
| (b) ma miejsce, nie werbuje | 14 065 | 14.2% | 11 068 |
| (c) swieze straty | 7 020 | 7.1% | 7 277 |
| (d) brak druzyny (niewola, brak dowodcy) | 1 363 | 1.4% | 1 438 |
| (e) kompanie najemne i Nocna Straz | 6 428 | 6.5% | 6 672 |
| RAZEM | 98 827 | 100% | 114 040 |

Drugi, niezalezny rachunek (318 rodow AI): brakuje 83 940, z tego 63 447 (76%) nie miesci sie w pulapie zoldu rodu, 20 493 (24%) rod moglby oplacic, a ich nie ma [P/S, R-PIE-wer nr 14]. Oba rachunki mowia to samo: ok. trzy czwarte luki to pulap zoldu, jedna czwarta nie.

**Ile rodow stoi na pulapie - rozdzialy podaja 111, 133, 109.5, 118 i 143.** Sprawdzilem w BUD (r1, doby 337-364) [P]:

| grupa (rody z lennem, 205) | rodow | ludzi w druzynach | wypelnienie limitu | zold na czlowieka | udzial w rentach korony |
|---|---|---|---|---|---|
| na pulapie (zold >= 95% pulapu) | 103.2 | 28 309 | 37.2% | 7.01 zl | 45.2% |
| pelna druzyna (>= 90% limitu) | 12.8 | 8 298 | 96.0% | 6.23 zl | 9.4% |
| 80-95% pulapu | 16.3 | 6 869 | 50.3% | 5.72 zl | 8.4% |
| luz (zold ponizej 80% pulapu) | 72.7 | 30 177 | 59.8% | 4.48 zl | 36.9% |

- Wszystkie liczby rozdzialow sa dobre, tylko licza rozne grupy. 133 (R-PIE) wychodzi, gdy doliczyc rody bez lenna z pulapem bliskim zera (u mnie 132.9, co do sztuki); 111 (R-WOJ) to rody z lennem w wojnie przy definicji "wolne miejsce nie starcza na 10% limitu"; 103-110 to rody z lennem przy definicji "zold >= 95% pulapu". Do raportu: **ok. 100-110 z 205 rodow z lennem**.
- Rodow na pulapie przybywa caly rok: 72 / 91 / 96 / 110 w dobach 91 / 182 / 273 / 364 [S na danych P, R-WOJ 5.2]; skokiem, nie rowno (85-93 w dobach 113-280, potem 108-111) (wer).
- Wlasny licznik logu: 47 rodow w wojnie, ktorym zalogi i karawany zajely caly pulap, z tego 42 bez ludzi w druzynach [P, LOG:129141; PROGI e8:256]. Liczba ta nie rosnie rowno: 24 / 48 / 30 / 43 / 47 w dobach 91 / 182 / 273 / 336 / 364.
- Rody z lennem bez ani jednego zolnierza: 14 z 205 (prog 12; poprzedni bieg 29) [P, PROGI e8:263]; w ciagu roku rosnie: 1.4 -> 9.4 -> 16.0 (doby 57-84 / 225-252 / 337-364) [P, R-PRO-wer nr 7].

**Trzy przyczyny luki:**

1. **Zolnierz zdrozal** [P, BUD, R-PRO-wer 2b; to samo R-PIE-wer brak 6]:

| doby | ludzi w druzynach rodow | zold druzyn, zl/d | zold na czlowieka |
|---|---|---|---|
| 57-84 (szczyt) | 105 185 | 488 497 | 4.64 |
| 169-196 | 86 925 | 460 534 | 5.30 |
| 337-364 | 79 001 | 456 511 | 5.78 |
| zmiana od szczytu | -24.9% | -6.5% | +24.5% |

Dzisiejszy zold przy cenie czlowieka ze szczytu oplacilby 98.3 tys. ludzi [S: 456 511 / 4.644]. Zeby dzis trzymac 85 tys., druzyny potrzebuja ok. 32-37 tys. zl zoldu dziennie wiecej [S, R-PIE-wer]. Prog 85 tys. byl ustawiany w dobach 90-120, przy tanszym zolnierzu. Szczyt 108 tys. to tez rozruch pierwszego roku na kiesach startowych: "skarbiec wojenny" rodow dawal 38% pulapu w dobie 91 i 26-29% od doby 182 [S, R-WOJ-wer A].

2. **Pulap zoldu i zalogi.** Regula budzetu: zold najwyzej 0.60 dochodu stalego plus 0.8 nadwyzki kiesy ponad rezerwe rozlozone na 45 dni (ClanBudget.cs 191-210). W wojnie zalogi sa platne pierwsze (decyzja projektu, nie blad; ClanBudget.cs 455). Zold zalog stoi na 183-191 tys. zl dziennie od doby 57 - zalogi nie rosna, kurczy sie pulap [P, R-WOJ-wer B]. Zalogi to 29.5% calego zoldu (190 862 z 647 722 zl dziennie); czlowiek zalogi kosztuje 2.28 zl, czlowiek druzyny 5.75 zl [P] (wer). U rodow na pulapie zalogi to 38% pulapu. Srednio w zalogach jest 83.6 tys. ludzi, w druzynach krolestw 80.0 tys. (wer - analityk podal roznice 9 tys. z doby dolka).

3. **Rod z pieniedzmi tez nie ma pelnej druzyny.** Rody z luzem trzymaja 60-64% limitu przez caly rok, bez trendu (64% wg R-WOJ, 58% wg R-PIE, 59.8% wg r1) [P]. Druzyny sa stale rozbijane i odbudowywane (ich zolnierz kosztuje 4.5-4.8 zl, czyli to swiezy rekrut). Dokladnej przyczyny nikt nie zmierzyl - nie wiem. Stad gorna granica tego, co da sie kupic pieniadzem: 96-105 tys. [S, R-WOJ-wer 3.4; R-PIE: ok. 97 tys.].

**Ile zlota do progu** (krzywa: kazdy rod dostaje co najmniej dany ulamek limitu druzyn; najslabsi pierwsi; pieniadz CELOWANY, tylko do rodow, ktorym brakuje) [S na danych P, R-WOJ 5.3 i R-WOJ-wer 3.1]:

| kazdy rod co najmniej | przyrost ludzi | zold wedlug analityka, zl/d | z zasypaniem dziury (wer), zl/d |
|---|---|---|---|
| 20% limitu | 4 689 | 25 903 | 38 439 |
| 25% limitu (prog 85 tys.) | 6 748 | 37 002 | 50 558 |
| 30% limitu | 9 186 | 49 615 | 64 272 |
| 40% limitu (ok. 94 tys. wojska) | 15 425 | 81 521 | 99 970 |

- "Dziura" (wer): 93 rody w wojnie juz dzis placa zold ponad swoj pulap, razem 31 126 zl dziennie [P, BUD doby 337-364]. Taki rod nie werbuje (ClanBudget.cs 565-580), wiec nowy pieniadz najpierw zasypuje dziure. Analityk podal 30-45 tys. - to dolna granica.
- Dzisiejsza droga (zwrot zoldu i renty dla wszystkich): 1 zl nowych wplywow korony daje 0.37-0.54 zl zoldu u rodow na pulapie [S, R-WOJ-wer D]; od razu +0.41 zl pulapu, z udzialem dworu ustepujacym zoldowi +0.60 zl [S, R-PIE-wer]. Dlatego ta droga na 5.6 tys. ludzi trzeba 70-120 tys. zl dziennie nowego dochodu koron [S, R-PRO-wer 2c] (analityk wojska: 60-190 tys.).
- Pieniadz i potrzeba leza w roznych krolestwach (wer): z 37 tys. zl najtanszej drogi 14.7 tys. (40%) przypada na Zelazne Wyspy, Wolnych Ludzi i Ibben, a 18.3 tys. (50%) na krolestwa, gdzie lud miast nie odklada tyle albo wcale [S na danych P, R-WOJ-wer 3.2].

### 1.7 Ostatnia doba (73.6 tys.) a srednia (79.4 tys.)

| doba | druzyny w wojnie | zmiana | zabici w druzynach | jency z druzyn |
|---|---|---|---|---|
| 360 | 80 479 | -78 | 72 | 96 |
| 361 | 79 334 | -1 145 | 1 254 | 988 |
| 362 | 78 682 | -652 | 215 | 99 |
| 363 | 77 436 | -1 246 | 176 | 111 |
| 364 | 73 578 | -3 858 | 2 186 | 2 396 |

[P] linie "Skarbce" i "Ludzie" (R-WOJ pkt 6). Mediana dob 337-364: 79 596 (wer). Zwykla doba: mediana zabitych 310.

Trzy bitwy doby 364 [P, BIT:25400, 25373, 25365]:
- Pod Willow Wood: Gregor Clegane (King's Landing, 2 678 ludzi) rozbil armie Quincy Coxa (Riverlands, 2 148 ludzi) - cala stracona.
- Pod Deep Den: Quenten Banefort (King's Landing, 3 751) rozbil druzyne krola Edmure'a Tully'ego (1 259) - cala stracona.
- W polu: Dorne (1 566) przegralo z Reach (1 477): 243 zabitych, 1 175 rozbitych.

**Poprawka weryfikatora (wazna):** analityk napisal, ze Dorzecze stracilo armie "w przegranych wypadach". Bylo odwrotnie. Obie twierdze nalezaly tego dnia do King's Landing (LR, dzien 109199); wypad zrobilo i wygralo King's Landing, a Dorzecze oblegalo. W calym roku: 40 wypadow, 38 wygral wychodzacy z twierdzy, 0 oblegajacy, 2 bez rozstrzygniecia. Oblegajacy stracili w tych 38 bitwach 55 018 ludzi (19 328 zabitych = 13% wszystkich zabitych w druzynach, 35 687 rannych do niewoli), za kazdym razem 100% [P, R-WOJ-wer H, skrypt w10]. Do tego bitwy pod murami: 21 bitew, przegrani 18 480 ludzi. Regula ludzkich strat (przegrani uchodza) dziala tylko w polu (LosersFlee.cs, warunek IsFieldBattle).

## 2. Pieniadz rodow i koron

### 2.1 Rody razem (318 rodow AI, srednio na dobe w roku)

| pozycja | zl na dobe | udzial | zrodlo |
|---|---|---|---|
| **WPLYWY razem** | **1 238 965** | 100% | [P] BUD wplyw_doby |
| korona razem | 496 626 | 40% | [P] BUD d_korona |
| - zwrot 70% zoldu druzyn w wojnie | 262 753 | 21% | [P] BUD zwrot_korony |
| - renty wedlug lenn | 216 852 | 18% | [P] BUD renta_180 |
| - dary koron (Wolne Miasta -> Dothrakowie) | 16 820 | 1% | [P] BUD mennica_monopole |
| lupy, okupy, trzecia lorda | 335 022 | 27% | [P] BUD sr_jednorazowe |
| ziemia (podatek wsi, zawor kas miast i zamkow, cla) | 194 383 | 16% | [P] BUD d_ziemia |
| kontrakt najemnikow | 14 931 | 1% | [P] BUD d_kontrakt |
| wlasne pieniadze wracajace z kas siedzib, przelewy w rodzie, inne | 197 960 | 16% | [S] reszta |
| **ZOLD razem** | **633 472** | 51% wplywow | [P] BUD |
| - druzyny / zalogi / karawany lordow | 443 647 / 183 398 / 6 426 | | [P] BUD |
| inne wydatki (dwor 187 008, okupy 110 973, sprzet 115 411, werbunek 36 062 i inne) | 624 654 | | [P] skladniki, [S] suma |
| **SALDO (zmiana zlota rodow)** | **-19 160** | | [P] BUD G: 59 612 411 -> 52 657 242 |

(R-PIE 2.1; sprawdzone co do zlota w R-PIE-wer nr 3-5.)

- Sama ziemia (163 tys. dziennie pod koniec roku) nie pokrywa nawet zoldu zalog (191 tys.). Wojsko polowe zyje z korony i z lupu [P, LOG:129146].
- Panowie nie ubozeja od polowy roku (wer): glowy rodow 47.8 mln (doba 182), 47.5 mln (doba 273), 47.2 mln (doba 364); w ostatnich 28 dobach +16 tys. zl dziennie [P, LOG:64500, 96881, 129205]. Spadek z pierwszego polrocza to wydawanie kies startowych.
- Kolumna "warsztaty i karawany rodow 44 zl dziennie" nie znaczy, ze nie daja dochodu: zysk karawan gra wyplaca inna linia (18 479 zl w ostatniej dobie), ktora ksiega zalicza do przelewow w rodzie (wer - artefakt definicji kolumny, LOG:129146). Czy karawany lordow zarabiaja na swoj zold - nie wiem.

### 2.2 Krolestwa - suma rodow (srednio na dobe w roku)

Rod przypisany do krolestwa z doby 364; kompanie najemne osobno. [P] BUD (R-PIE 2.2, skrypt 09). "Na suficie" = zold co najmniej 95% pulapu w dobie 364.

| krolestwo | rodow | wplywy/d | ziemia | korona | lupy i okupy | zold druzyn | zold zalog | saldo/d | zloto d1 (mln) | zloto d364 (mln) | na suficie zoldu d364 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| The North | 25 | 104 004 | 22 985 | 52 872 | 9 943 | 49 517 | 19 809 | -2 191 | 4.82 | 4.03 | 19 |
| House Baratheon of King's Landing | 27 | 100 454 | 18 495 | 36 361 | 30 771 | 38 013 | 15 191 | -1 781 | 4.25 | 3.60 | 6 |
| The Reach | 16 | 86 162 | 17 624 | 33 621 | 20 518 | 27 113 | 11 257 | -2 177 | 3.61 | 2.82 | 6 |
| Dorne | 41 | 80 079 | 10 403 | 28 912 | 30 438 | 27 474 | 11 494 | +875 | 4.63 | 4.95 | 4 |
| Riverlands | 14 | 77 345 | 13 422 | 26 376 | 24 897 | 22 817 | 9 494 | -1 029 | 3.52 | 3.14 | 4 |
| Stormlands | 13 | 76 100 | 12 391 | 30 894 | 21 497 | 24 116 | 9 013 | -2 178 | 3.01 | 2.22 | 3 |
| The Vale | 20 | 70 811 | 15 564 | 37 069 | 3 933 | 36 277 | 11 413 | -2 861 | 3.31 | 2.27 | 18 |
| House Targaryen | 8 | 64 090 | 6 553 | 20 529 | 28 564 | 21 108 | 8 849 | +4 349 | 1.63 | 3.21 | 3 |
| Dothraki Horde | 16 | 62 659 | 7 001 | 38 907 | 7 707 | 14 957 | 6 379 | +136 | 2.09 | 2.14 | 0 |
| Dragonstone | 10 | 52 035 | 5 119 | 16 568 | 24 787 | 17 072 | 7 782 | -2 187 | 2.40 | 1.61 | 5 |
| Iron Islands | 29 | 38 037 | 7 284 | 19 344 | 2 845 | 17 158 | 12 720 | -5 130 | 3.47 | 1.61 | 28 |
| Volantis | 6 | 36 368 | 7 797 | 16 129 | 4 298 | 15 952 | 7 360 | -639 | 1.45 | 1.22 | 4 |
| Qarth | 4 | 36 102 | 4 592 | 13 180 | 9 847 | 11 872 | 4 340 | +1 678 | 1.16 | 1.77 | 0 |
| Kompanie najemne | 11 | 33 794 | 0 | 0 | 17 488 | 11 774 | 0 | +7 706 | 2.85 | 5.65 | 4 |
| Lorath | 5 | 32 547 | 3 713 | 12 377 | 11 223 | 9 757 | 3 908 | +343 | 1.17 | 1.29 | 1 |
| Qohor | 5 | 29 233 | 3 735 | 11 480 | 9 533 | 11 949 | 3 927 | -1 962 | 1.21 | 0.50 | 4 |
| Tyrosh | 5 | 28 105 | 3 090 | 9 252 | 11 823 | 7 293 | 3 691 | +131 | 1.22 | 1.27 | 0 |
| Myr | 5 | 28 085 | 4 619 | 9 846 | 9 775 | 10 823 | 4 134 | -1 099 | 1.19 | 0.79 | 2 |
| Braavos | 5 | 26 023 | 3 299 | 7 748 | 9 999 | 9 509 | 3 937 | -1 470 | 1.22 | 0.68 | 3 |
| House Targaryen, Aegon | 5 | 25 029 | 4 388 | 9 610 | 5 575 | 10 159 | 4 648 | -1 190 | 1.27 | 0.84 | 2 |
| Norvos | 5 | 24 917 | 3 793 | 9 313 | 7 370 | 8 554 | 2 831 | -1 626 | 1.21 | 0.62 | 2 |
| Lys | 5 | 24 039 | 2 927 | 8 020 | 10 127 | 6 889 | 2 803 | -1 507 | 1.18 | 0.64 | 2 |
| Sarnor | 5 | 23 381 | 3 722 | 10 754 | 4 207 | 8 261 | 2 107 | +504 | 1.20 | 1.39 | 0 |
| Pentos | 11 | 21 348 | 2 881 | 6 414 | 8 349 | 6 890 | 4 774 | -1 336 | 1.52 | 1.03 | 3 |
| Ibben | 4 | 15 753 | 1 454 | 6 827 | 4 847 | 5 709 | 4 262 | -2 271 | 1.14 | 0.32 | 3 |
| Nights Watch | 5 | 15 287 | 2 785 | 7 177 | 3 072 | 3 856 | 846 | +136 | 1.35 | 1.40 | 0 |
| Free Folk | 10 | 14 300 | 3 381 | 7 870 | 1 354 | 6 693 | 5 476 | -3 660 | 1.85 | 0.52 | 9 |
| Skagos | 1 | 4 472 | 595 | 2 869 | 105 | 643 | 369 | +419 | 0.22 | 0.37 | 1 |
| Yi Ti Exiles | 1 | 4 278 | 419 | 3 162 | 61 | 755 | 296 | +456 | 0.21 | 0.38 | 1 |
| Summer Isles | 1 | 4 130 | 354 | 3 145 | 68 | 686 | 290 | +401 | 0.21 | 0.36 | 1 |
| **SWIAT** | 318 | 1 238 965 | 194 383 | 496 626 | 335 022 | 443 647 | 183 398 | -19 160 | 59.61 | 52.66 | 138 |

Co widac [P]:
- 235 z 318 rodow ma mniej zlota niz na starcie; w ostatnich 28 dobach zloto spada juz tylko u 145.
- Zelazne Wyspy: zold 29.9 tys. z 38.0 tys. wplywow (79%), same zalogi 12.7 tys. - wiecej niz cala ziemia (7.3 tys.); lup tylko 2.8 tys.; zloto 3.47 -> 1.61 mln; 28 z 29 rodow na suficie. Wolni Ludzie: 1.85 -> 0.52 mln, 9 z 10 na suficie. Dolina 18 z 20, Polnoc 19 z 25.
- Zloto rosnie tam, gdzie jest lup i okupy: House Targaryen +4.3 tys. dziennie (lup 28.6 tys.), Qarth +1.7 tys., Dorne +0.9 tys.

### 2.3 Korony (skarbce krolestw)

Wszystkie korony razem, srednio na dobe [P] linie "Obieg" i "Korona: wplywy dnia (165)" (R-PIE 3.1):

| pozycja | rok | ostatnie 28 dob |
|---|---|---|
| **WPLYWY DNIA razem** | **443 903** | 449 808 |
| - danina wojenna z kas osad | 202 832 | 217 563 |
| - udzial korony w zaworze kas miast (polowa) | 119 461 | 112 732 |
| - 1/9 lupu i okupow lordow | 28 778 | 30 788 |
| - podatek od rodow (BannerKings) | 24 250 | 21 493 |
| - udzial w zaworze zamkow (1/3) | 15 806 | 14 332 |
| - clo od handlu miast | 11 866 | 9 683 |
| - **podatek wojenny ludu** | **10 695** | **15 020** |
| dolozone z zapasu skarbca (1/360 nadwyzki ponad 0.5 mln dziennie) | 70 240 | 37 116 |
| kredyt z Banku Zelaznego | 8 432 | 11 448 |
| WYDATKI: zwrot zoldu panom | 263 585 | 277 190 |
| renty panom wedlug lenn | 214 725 | 172 002 |
| dary | 16 846 | 15 210 |
| kontrakty kompanii najemnych | 14 341 | 17 214 |
| wezwania sojusznikow do wojny | 4 931 | 1 576 |
| raty kredytu do Banku | 4 105 | 9 616 |
| **skarbce razem: zmiana na dobe** | **-84 901** | -49 303 |

Stan skarbcow: 57 927 181 (doba 1) -> 38 280 005 (doba 182) -> 27 108 168 zl (doba 364) [P, LOG:707, 64500, 129205].

Poprawki weryfikatorow (trzy rozdzialy opisaly spadek skarbcow jako "wojna drenuje korony" - to za mocne):
- 83% ubytku roku (25.62 z 30.82 mln) to zaplanowana regula: korona wydaje co dobe 1/360 zapasu ponad 500 tys. Krolestwa, ktore nie robily nic wiecej, maja po roku dokladnie ok. 1 047 000 z 2 000 000 (np. Myr 1 047 151, Pentos 1 047 217, Ibben 1 047 236) [P, R-PRO-wer 2a; LOG:129101]. To wyjasnia pytanie z R-WNA-wer ("15 z 29 skarbcow stoi na tej samej linii - nie wiem, jaka regula").
- Doplata z zapasu dzis: ok. 37 tys. zl dziennie = 7.7% wydatkow koron; w pierwszym kwartale 106 tys. Za rok ok. 13-14 tys. [S: 37.3 tys. x e^(-364/360)]. Zapas nie konczy sie nagle - maleje o polowe co ok. 250 dob.
- Reszta ubytku poza regula (ok. 11.5 tys. zl dziennie w dobach 337-364): dwaj weryfikatorzy pisza "nie wiem", R-PIE podaje przyczyne - okupy krolow placone gotowka ze skarbcow (5.23 mln w roku), ktore odbiorca wydaje tego samego dnia na renty. Sprawdzilem: w dobach 337-364 sa dwa takie okupy, razem 304 900 zl = 10.9 tys. zl dziennie [P, LOG:126438 i LOG:126793]. To tlumaczy prawie cala reszte [S].
- Renty: 221 tys. (doby 29-56) -> 172 tys. (ostatnie 28 dob) [P]. "141 tys." i "36 rodow w krolestwach bez grosza na renty" to odczyt jednej doby; srednio 22.5 rodu (doby 29-56: 8.6) (wer). Zwrot + renty razem stoja od doby 253 na 448-453 tys. dziennie - renty spadaja glownie dlatego, ze rosnie zwrot zoldu (wiecej wojska) (wer).
- Korona wydaje co do zlota wplywy dnia; zwrot zoldu wyplacony w 99.8% [P, PROGI e5:178].

Krolestwa [P] linie "Skarbce" i "Korona: wplywy dnia (165)" czesc "na krolestwo" (R-PIE 3.2). Kwoty na dobe = srednia roku. Kazdy skarbiec zaczynal od 2.00 mln.

| krolestwo | dob wojny | skarbiec d364 (mln) | wplywy dnia /d | w tym podatek wojenny ludu | z zapasu /d | zwrot zoldu /d | renty /d | dlug w Banku d364 |
|---|---|---|---|---|---|---|---|---|
| The North | 355 | 1.04 | 47 845 | 1 334 | 2 625 | 30 504 | 22 318 | 24 022 |
| House Baratheon of King's Landing | 355 | 0.66 | 36 946 | 1 357 | 2 305 | 24 593 | 11 769 | 174 530 |
| The Vale | 342 | 1.05 | 34 566 | 670 | 2 628 | 21 507 | 15 562 | 0 |
| The Reach | 342 | 0.80 | 32 046 | 109 | 2 518 | 17 317 | 16 284 | 8 431 |
| Stormlands | 363 | 0.80 | 29 162 | 585 | 1 888 | 15 673 | 15 221 | 3 717 |
| Dorne | 342 | 1.00 | 25 991 | 758 | 2 614 | 18 065 | 10 848 | 18 974 |
| Riverlands | 277 | 1.05 | 25 007 | 210 | 2 628 | 13 176 | 13 200 | 3 509 |
| Dothraki Horde | 342 | 1.06 | 20 347 | 0 | 2 651 | 8 966 | 13 079 | 0 |
| House Targaryen | 342 | 1.05 | 18 341 | 602 | 2 630 | 11 558 | 8 984 | 7 906 |
| Volantis | 342 | 1.05 | 17 233 | 132 | 2 626 | 8 993 | 7 154 | 6 260 |
| Iron Islands | 342 | 1.05 | 16 974 | 510 | 2 627 | 10 321 | 9 023 | 0 |
| Dragonstone | 363 | 0.70 | 15 357 | 495 | 1 682 | 10 708 | 5 859 | 281 508 |
| Qarth | 342 | 1.03 | 12 210 | 251 | 2 623 | 7 049 | 6 140 | 91 263 |
| Lorath | 342 | 0.90 | 11 841 | 350 | 2 272 | 5 878 | 6 499 | 44 421 |
| Qohor | 342 | 1.03 | 11 277 | 399 | 2 594 | 7 232 | 4 218 | 36 030 |
| Tyrosh | 342 | 0.81 | 10 528 | 238 | 1 863 | 4 641 | 4 611 | 68 327 |
| House Targaryen, Aegon | 342 | 0.79 | 10 213 | 267 | 2 595 | 6 086 | 3 530 | 194 762 |
| Norvos | 342 | 0.78 | 9 385 | 484 | 2 137 | 5 322 | 3 991 | 28 360 |
| Myr | 342 | 1.05 | 9 212 | 375 | 2 625 | 6 456 | 3 387 | 42 353 |
| Braavos | 342 | 0.51 | 9 204 | 503 | 1 351 | 5 462 | 2 286 | 661 983 |
| Sarnor | 342 | 0.96 | 9 050 | 115 | 2 428 | 4 968 | 5 786 | 0 |
| Lys | 342 | 0.64 | 7 916 | 176 | 2 085 | 4 153 | 3 867 | 76 565 |
| Free Folk | 364 | 1.05 | 6 015 | 246 | 2 626 | 4 239 | 3 609 | 0 |
| Pentos | 342 | 1.05 | 5 841 | 295 | 2 625 | 4 131 | 2 283 | 76 670 |
| Nights Watch | 364 | 1.05 | 5 028 | 1 | 2 625 | 2 508 | 4 669 | 0 |
| Ibben | 342 | 1.05 | 4 908 | 204 | 2 625 | 3 356 | 3 499 | 2 519 |
| Yi Ti Exiles | 0 | 1.05 | 998 | 0 | 2 635 | 0 | 3 162 | 0 |
| Summer Isles | 0 | 1.05 | 880 | 0 | 2 624 | 0 | 3 145 | 0 |
| Skagos | 0 | 1.05 | 835 | 0 | 2 624 | 0 | 2 869 | 0 |
| **RAZEM** | | 27.11 | 445 156 | 10 666 | 70 379 | 262 860 | 216 852 | 1 852 110 |

- Nizej niz linia reguly sa te skarbce, ktore placily okupy krolow albo pozyczaly: Braavos (krol pojmany 9 razy, skarbiec zaplacil 7 okupow za 1.44 mln), Lys, Krolewska Przystan (2 okupy za 1.54 mln), Dragonstone, Aegon [P, R-PIE 2.6 i 3.2].
- Zaden skarbiec w wojnie nie spadl ponizej 0.25 mln [P, PROGI e2:7].
- Dlug koron w Banku: 0 -> 583 513 (doba 182) -> 1 852 110 zl u 20 krolestw (doba 364); w drugim polroczu wzrosl u 16 krolestw (prog: 0) [P, LOG:129102; PROGI e5:175]. Bank: kapital 5.00 -> 3.13 mln, kapital plus pozyczki 5.41 mln; zaleglosci i bankructwa 0 [P].
- Skagos, Wyspy Letnie i Wygnancy Yi Ti nie wojuja, zarabiaja 0.8-1.0 tys. dziennie i zyja z zapasu (2.6 tys. dziennie); 83-88% ich wydatkow to renty dla wlasnego, jedynego rodu (wer).

### 2.4 Najbogatsi i najbiedniejsi (doba 364)

[P] BUD kolumna G (zloto calej rodziny) (R-PIE 2.3).

| 10 najbogatszych rodow | krolestwo | lenno | zloto d1 | zloto d364 | ludzi druzyny / limit |
|---|---|---|---|---|---|
| Stormcrows | House Targaryen | zamek | 229 033 | 1 522 385 | 469 / 697 |
| Fowler | Dorne | zamek | 199 651 | 1 335 895 | 242 / 460 |
| Bolton | The North | miasto | 351 685 | 1 234 137 | 1135 / 1137 |
| Tully | Riverlands | miasto | 436 689 | 1 068 066 | 169 / 234 |
| Moon Brothers | Summer Isles | najemnik | 270 000 | 948 856 | 140 / 540 |
| Baratheon, Renly | Stormlands | miasto | 418 523 | 894 275 | 1366 / 1493 |
| Tyrell | The Reach | miasto | 417 847 | 816 690 | 1459 / 2268 |
| Long Lances | Skagos | najemnik | 267 831 | 733 588 | 219 / 595 |
| Targaryen | House Targaryen | miasto | 412 423 | 674 432 | 759 / 2246 |
| Vaith | Dorne | miasto | 252 713 | 635 120 | 635 / 763 |

| 10 najbiedniejszych rodow z lennem | krolestwo | lenno | zloto d1 | zloto d364 | ludzi druzyny / limit | ludzi zalogi |
|---|---|---|---|---|---|---|
| Lydden | House Baratheon of King's Landing | zamek | 216 611 | 13 626 | 32 / 515 | 4 |
| Kasgos | Norvos | zamek | 222 570 | 14 295 | 36 / 197 | 319 |
| Orthys | Lys | zamek | 224 526 | 14 363 | 0 / 347 | 127 |
| Moesia | Pentos | zamek | 182 571 | 20 045 | 0 / 493 | 235 |
| Sontho | Pentos | zamek | 223 824 | 20 140 | 3 / 545 | 293 |
| Cuy | The Reach | zamek | 195 717 | 20 234 | 83 / 435 | 288 |
| Mormont, Jorah | House Targaryen | miasto | 212 407 | 21 739 | 113 / 632 | 264 |
| Forrester | The North | zamek | 184 920 | 22 248 | 58 / 459 | 34 |
| Chovaqqo | Dothraki Horde | zamek | 172 804 | 24 182 | 380 / 678 | 196 |
| Heggo | Dothraki Horde | zamek | 208 394 | 24 572 | 248 / 525 | 191 |

- Rozklad: mediana zlota rodu 210 941 (doba 1) -> 100 543 (doba 364); gorne 10% rodow ma 39.4% zlota (start 19.7%; poprzedni bieg 56.9%); rodow z ponad 1 mln: 4 (poprzedni bieg 12) [P].
- 15 najbogatszych ma 12.83 mln = 24.4% zlota rodow i 8 169 ludzi przy limicie 13 636 [P].
- Bankrutow 0; rodow w zajeciu dochodu 1 [P, LOG:129143]. Glow rodow z kiesa ponizej 5 000 zl: 5 (prog 10; poprzedni bieg 21) [P, PROGI e2:3]. Uwaga: to licznik rodow z zamkiem; inny spis (wszystkie rody AI w krolestwach) daje 33 (poprzedni bieg 52) [P, R-PRO-wer nr 20; CS:9238] - kierunek ten sam, grupa inna.
- Rody bez ziemi (np. "Banu ..." w Dorne) maja po 8-20 tys. zl, 0 ludzi i dochod 13-38 zl dziennie [P].

### 2.5 Gdzie lezy zloto swiata

[P] linia "Pieniadz swiata": LOG:707 (doba 1), LOG:64500 (doba 182), LOG:129205 (doba 364) (R-PIE pkt 4; zgodne w czterech rozdzialach).

| posiadacz | doba 1 | doba 182 | doba 364 | zmiana w roku |
|---|---|---|---|---|
| glowy rodow | 52 250 815 | 47 803 459 | 47 229 257 | -5 021 558 |
| kiesy ludu miast (mieszczanie) | 6 277 921 | 29 656 999 | 44 120 365 | +37 842 444 |
| skarbce krolestw | 57 927 181 | 38 280 005 | 27 108 168 | -30 819 013 |
| notable | 28 824 740 | 24 692 396 | 21 849 654 | -6 975 086 |
| sakiewki zolnierzy | 338 323 | 16 251 396 | 16 591 066 | +16 252 743 |
| kasy kupcow miast | 27 333 090 | 9 961 445 | 10 975 439 | -16 357 651 |
| pozostali lordowie | 12 014 245 | 10 223 101 | 10 436 306 | -1 577 939 |
| kiesy wsi | 1 107 474 | 9 817 926 | 9 547 972 | +8 440 498 |
| kasy zamkow | 6 942 637 | 4 600 211 | 5 405 157 | -1 537 480 |
| kiesy ludu podzamczy | 672 228 | 4 030 169 | 3 326 724 | +2 654 496 |
| Bank Zelazny | 5 000 000 | 4 230 256 | 3 126 954 | -1 873 046 |
| karawany | 3 743 269 | 3 142 281 | 2 981 924 | -761 345 |
| kapital warsztatow | 2 564 841 | 2 635 503 | 2 568 293 | +3 452 |
| pozostale (zalogi, tabory wsi, bandy, gminy, liczniki w drodze, gracz) | 3 207 449 | 3 110 022 | 3 067 816 | -139 633 |
| **RAZEM** | **208 204 213** | **208 435 169** | **208 335 095** | **+130 882** |

Tempo w ostatnich 28 dobach (zl na dobe) [P, R-PIE pkt 4, R-PIE-wer]: lud miast +70.2 tys.; skarbce -49.3 tys.; sakiewki zolnierzy -11.4 tys.; notable -9.9 tys.; podzamcza -7.2 tys.; glowy rodow +16.3 tys. Reszta stoi: kasy kupcow od ok. 60. doby (10-11 mln), kiesy wsi od ok. 110. doby (9.5 mln), sakiewki od ok. 170. doby (16-17 mln), glowy rodow od ok. 170. doby (47-48 mln).

- Pierwsze 28 dob to rozruch: kasy kupcow oddaly 16.2 mln (zaplanowany dar startowy), glowy rodow chwilowo urosly do 60.9 mln. Dlatego liczenie "od doby 30" zawyza spadek u panow (wer).
- Kiesy mieszczan: tempo przyrostu gasnie caly rok: 144 -> 118 -> 93 -> 74 -> 70 tys. zl dziennie [P]. W 70% to lustro wydawania zapasu koron. Po wyczerpaniu zapasu ok. 20-30 tys. dziennie [S, R-PIE-wer]. W poprzednim biegu lud odkladal pod koniec 33-38 tys. dziennie [P].
- Ponad wlasny zapas na zycie mieszczanie maja 29 485 864 zl ("lokata") [P, LOG:129238]; lokata rosla 84.9 / 94.5 / 77.3 / 68.3 tys. dziennie w kwartalach, w ostatnich 28 dobach 56.4 tys. [P, R-GOS-wer nr 21].
- 98% przyrostu (68.8 z 70.2 tys. dziennie) jest u wyrobnikow, ktorzy placa polowe stawki podatku wojennego [P, R-PIE-wer N1].
- Sakiewki zolnierzy nie puchna (wer): Q1 +144 483, Q2 +36 561, Q3 +5 687, Q4 -1 171 zl dziennie [P, linia "Sakiewka ludzi"]. W ostatnim kwartale zolnierz wydaje wszystko, co dostaje.

### 2.6 Czy pieniadz krazy

Tak. Glowna petla, srednio na dobe w roku [P, linie "Obieg" i "Kiesa ludu", R-PIE pkt 5]:

| krok | strumien | zl na dobe |
|---|---|---|
| 1 | panowie placa zold (druzyny 445 716, zalogi 183 511, karawany 6 444) | 635 671 |
| 2 | zolnierze wydaja: markietani 215 149, zycie w miastach 173 502, naprawy 1 228 | 389 879 |
| 3 | lud miast zarabia (z kas kupcow 314 123; od wojska, dworu, warsztatow i budow 219 526) | 533 649 |
| 4 | lud miast kupuje u kupcow | 415 443 |
| 5 | lud miast placi podatki (panu 15 038, koronie 10 695) | 25 733 (wer: 24 659 = 4.6% zarobku; 5.4% w ostatnich 28 dobach) |
| 6 | lud miast ODKLADA | 104 249 (pod koniec roku 70 tys.) |
| 7 | kupcy miast placa wsiom za plon | 310 108 |
| 8 | kasy miast oddaja zaworem: panom 143 728, koronie 119 461 | 263 189 |
| 9 | korona zbiera 443 903, doklada z zapasu 70 240 i oddaje panom | 478 310 |

- Z kazdej zlotowki zoldu zolnierze wydaja w miastach i wsiach 61-64 grosze [P, R-PIE-wer brak 4] - podatek od ludu wraca wiec do ludu jako zarobek.
- Lud wystawia do wydania nadwyzke 4 490 627 zl dziennie, a caly wydatek, ktory gra od niego bierze, to 454 101 zl [P, LOG:129238]. Wsie: chca kupic za 70.4 tys. dziennie, kupuja za 8.9 tys., bo 478-479 z 571 wsi nie ma czego kupic w swoim miescie [P].
- Kupcy: pan i korona zabieraja zaworem ok. 100% przyrostu kas [P, LOG:129149]. Wsie: korona bierze 56-59% gotowki, pan 32% [P, LOG:129218]. Tam nie ma czego brac.
- Zamkniecie: zmiana zlota swiata +130 882 zl w roku (+0.063%; poprzedni bieg -2 373 117); wyplat bez platnika 0, bez odbiorcy 0; reszta niewyjasniona -135 106 zl w roku (srednia 28 dob w progu) [P, PROGI e3:3, e3:16-20]. Poprawka (wer): zoldu oddano o 897 448 zl MNIEJ, niz naliczono, a +1 270 532 zl dodaly inne linie rozliczen rodow (netto +373 084 zl w roku); ktora linia - nie wiem. Do tego 628 169 zl bohaterow, ktorzy wrocili do swiata ze swoim zlotem (427 488 w dobie 52) [P].

### 2.7 Okupy

[P] linie "Okupy (178)" i "Niewola lordow i okupy (169c)", LOG:129147 (R-PIE 2.6, R-WNA 5.1).

- Pojmania lordow: 2 919 w roku (9.2 na rod); w tym krolowie 53, glowy rodow 844. Uwolnieni: za okup 1 260, ucieczka 680, przez pokoj 528, po bitwie 334, z wyboru 41. W niewoli nie zmarl nikt.
- Okupy: 1 259 za 52 328 752 zl (gotowka 45.68 mln, dlug 6.65 mln). Mediana ceny 26 140 zl; lord ok. 10 dni dochodu rodu, glowa rodu ok. 30 dni.
- Skala (wer): okupy miedzy rodami to 124 tys. zl dziennie przy 1 239 tys. wplywow, czyli 10% obrotu rodow (analityk porownal przeplyw roku ze stanem kies - to zawyza wrazenie). W poprzednim biegu 1 190 okupow za 47.9 mln - to nie przypadek biegu.
- Skupienie: 52 osoby wykupione 5 i wiecej razy to 322 z 1 235 okupow (26%). Glowa rodu Kasgos (Norvos) 11 razy, rod 31 okupow za 787 103 zl = 57% rocznych wplywow, zloto 222 570 -> 14 295. Rod Quarro 37 okupow za 1.57 mln. Krol Braavos pojmany 9 razy.
- Cwiartka rodow z lennem, ktora stracila najwiecej, oddala netto 14.8 mln; cwiartka, ktora zyskala najwiecej, wziela 15.7 mln [P].

### 2.8 Podatki na czas wojny - co wynika z danych (pytanie Jeffa)

Fakty [P]:
- Podatek wojenny ludu: 15 020 zl dziennie (ostatnie 28 dob), srednia stawka 2.37%, pulap 3% (6% z polityka War Tax, po ktora zadne krolestwo AI przez rok nie siegnelo). Na pulapie 20 z 26 krolestw w wojnie [LOG:129100]. Projekt zakladal 30-60 tys. dziennie (PROGI, prog B2-11) - daje jedna czwarta do polowy.
- Korona juz dzis doplaca do wojska w wojnie: zwrot 70% zoldu druzyn = 274-277 tys. zl dziennie. Podatek wojenny ludu to 5.4% tej kwoty [LOG:129096, R-WNA-wer 4.2].
- "Czas wojny" w pierwszym roku znaczy "zawsze": od doby 23 w wojnie jest 25-26 krolestw kazdej doby. Galaz "w pokoju stawka spada" nie zadzialala ani razu. Wedlug kodu Realm of Thrones dla 17 krolestw przymus wojny znika w dobie 433 (rozdz. 4.2) - potem podatek moze byc naprawde wojenny, ale tego bieg nie objal.
- Flaga wojny wlacza dzis cztery rzeczy naraz: zwrot 70% zoldu, podwojny pulap zoldu rodu (0.28 dochodu w pokoju, 0.60 w wojnie), podatek wojenny ludu i wolniejszy powrot wyrzutkow [ClanBudget.cs 26, R-WNA-wer].
- Gotowa miara "krolestwo naprawde walczy" jest w kodzie: styk z wrogiem w linii "Renty korony (180)". W ostatnim kwartale z 26 krolestw w wojnie srednio 14.4 nie mialo danego dnia styku [LOG:129098, R-WNA-wer 4.2].
- Lud odklada glownie tam, gdzie wojska juz jest duzo: Polnoc +14.4 tys. dziennie, Dolina +10.8, Volantis +7.8, King's Landing +7.0, Dorzecze +5.9 [KLU, dni 109171-109199, R-WOJ-wer 3.2]. Polnoc i Dolina maja razem 35.5% ludzi z rodow na pulapie i 19.4% kies ludu - tam podatek trafi do potrzebujacych [R-PRO-wer 2d]. Za to nadwyzka ludu to: Tyrosh 0, Lys 0, Pentos 6 tys., Braavos 82 tys., Norvos 188 tys., Dragonstone 357 tys. zl - krolestwom z dlugiem podatek od nadwyzki nie pomoze [S, R-GOS-wer brak 3]. W ostatnich 4 probkach lud jest na minusie w 12 krolestwach [P, 4 probki - malo].

Propozycje policzone w rozdzialach (wszystko [S]; zadna nie byla sprawdzona biegiem):

| nr | co | kto placi | ile zl na dobe | ile ludzi wiecej | zrodlo |
|---|---|---|---|---|---|
| 1 | Danina wojenna z oszczednosci mieszczan ponad zapas (2.5% dziennie) - decyzja Jeffa "tak", kod w toku | mieszczanie z nadwyzka (lezy 29.5 mln [P]) | 49-75 tys. (srodek 62); trwale nie wiecej, niz lud odklada (dzis 56-75 tys. i spada) | rozdana jak dzis: 1.5-2.5 tys. (projekt, najwyzej 6-7 tys.); 1.9-4.0 tys. na 50 tys. zl (R-PRO-wer) | projekt kiesy ludu linie 33-36, 180; R-PRO-wer 2c |
| 2 | Korona placi wedlug potrzeby: nowy pieniadz najpierw rodom na pulapie zoldu | nikt nowy - inny podzial | potrzeba celowana: 37 tys. bez dziury, ok. 50.6 tys. z dziura (do 85 tys.); ok. 100 tys. (do ok. 94 tys.) | +6.7 tys. (do 85 tys.); +15.4 tys. (do ok. 94 tys.) | R-WOJ-wer 3.1 |
| 3 | Tarczowe: pan z pieniedzmi i niepelna druzyna placi koronie | 73 rody z lennem maja 9.98 mln ponad rezerwe wojenna i 258 tys. zl/d wolnego pulapu [P] | to zbiornik, nie strumien: ok. 25 tys. przez rok | nie policzone | R-PRO-wer 2f; R-WOJ 7.4 |
| 4 | Stawka wojenna podatku pana (z kosztem lojalnosci) | mieszczanie przez pana | nie policzone w zadnym rozdziale - nie wiem | nie wiem | STAN-PRAC linia 1657 |
| 5 | Wyzszy pulap podatku wojennego od zarobku (3% -> 10-12%) | wszyscy mieszczanie, takze w biednych miastach | przyrost +27-40 tys. (10%) albo +34-51 tys. (12%); dojscie trwa 14-18 tygodni; 7 krolestw bez "potrzeby" nie ruszy stawki | 0.9-2.5 tys. (do 3.4 tys. po roku) | R-PRO-wer 2d; R-PIE-wer |
| 6 | Zwrot czesci zoldu ZALOG w wojnie (np. 35%) | korona (z pkt 1 albo 5) | koszt 67 tys.; rody na pulapie dostaja 36 tys. Pelny zwrot zoldu zalog rodom z luka: 143.7 tys. - 3-4 razy wiecej niz potrzeba | 2.1-3.1 tys. (do 4.2 tys. po roku) | R-PIE pkt 8, R-PIE-wer; R-WOJ-wer B |
| 7 | Druga "dziewiata" z lupu i okupow w wojnie (dzis korona bierze 1/9 = 30.8 tys.) | rody, ktore wygrywaja i maja wolny pulap | ok. 30 tys. | nie policzone | R-PIE-wer brak 5 |
| 8 | Niewydany przydzial na sprzet rodow ponad pulapem idzie na zold | rody ponad pulapem | 13-20 tys. (nie 87 tys. - kod juz dzis nie bierze tych pieniedzy od rodu ponad pulapem) | 2-3 tys. | R-PIE-wer rozdz. 3 |

Czego nie ruszac [P]: wsi (korona bierze juz 56-59% gotowki), kas kupcow (17 miast ponizej zapasu), notabli (-28% w roku), podzamczy (zjedzone bez zaplaty x8.5 w roku), sakiewek zolnierzy (stoja; "krotszy zold" dalby trwale ok. 0 zl - wer).

Uczciwa ocena [S]:
- Sam podatek od ludu rozdany dzisiejsza droga: 81-83 tys. wojska, ponizej progu (R-PIE-wer).
- Danina (srodek 62 tys.) rozdana CELOWANIE pokrywa potrzebe do 85 tys. (50.6 tys.) - ale w tym samym czasie gasnie doplata z zapasu koron (ok. -23 tys. zl dziennie w ciagu roku). Razem potrzeba ok. 74 tys. [S: 50.6 + 23]; to gorny koniec tego, co lud dzis odklada. Dlatego samo zrodlo 1 moze nie wystarczyc bez zrodla 3 albo 7.
- Trzy najslabsze krolestwa (Zelazne Wyspy, Wolni Ludzie, Ibben) zostana bez wojska przy kazdym podatku od wlasnego ludu: przyrost kies ludu +3 874 / -1 887 / +179 zl dziennie przy potrzebie 6 080 / 4 750 / 3 875 [P/S, R-WOJ-wer K].
- Gorna granica tego, co kupi pieniadz: 96-105 tys. (pkt 1.6).
- Nowy rekrut szczebla 1-2 kosztuje 2.5 zl dziennie, wiec w pierwszych miesiacach ten sam pieniadz da wiecej glow; potem ludzie awansuja i liczba wraca do rachunku [P, LOG:129141; wniosek S].
- Miara progu sumuje tylko krolestwa z flaga wojny. Gdy po dobie 433 czesc zawrze pokoj, suma spadnie sama (i pulap zoldu rodu w pokoju spada z 0.60 do 0.28 dochodu). Prog 85 tys. trzeba zapisac "przy 26 krolestwach w wojnie" albo na krolestwo [S, R-WNA-wer 4.5].
- Kazda propozycja wymaga biegu 120 dob z pomiarem: lokata ludu, "stac jedzenie", rody na pulapie, druzyny w wojnie.

### 2.9 Co runda 3 zmienila w pieniadzach

[P] (R-PIE pkt 7): zloto kompanii najemnych w dobie 364: 5.65 mln zamiast 20.60 mln (zadatek wstrzymany: 28.77 mln zostalo w kiesach krolow przy 767 najmach); rody z lennem bez warunku renty 12 zamiast 57; udzialy rent oddawane innym 5.5 tys. zamiast 62.6 tys. dziennie; zwolnieni przez budzet w roku 98 927 zamiast 148 569; gorne 10% rodow ma 39.4% zlota zamiast 56.9%. Skutek uboczny: wiecej wojska = wiecej zwrotu zoldu (+58 tys. dziennie) i mniej na renty (-32 tys.), a kiesy mieszczan rosna pod koniec roku 2 razy szybciej niz w poprzednim biegu.

## 3. Gospodarka

### 3.1 Jedzenie

| co | wynik w roku | zrodlo |
|---|---|---|
| wsie z glodem, okregi glodne | 0 w kazdej dobie | [P] linia "Spichlerze (E4/6)", LOG:128976 |
| zmarli z glodu | 0 | [P] LOG:128976 |
| spichlerze wsi swiata | 728 -> 726 dni potrzeby | [P] LOG:480 -> 128976 |
| warownie z zapasem 0 albo ponizej 30 dni | 0 w kazdej dobie | [P] "Glod ludu (E5/C2)"; spichlerze-regiony.csv |
| plon dnia wobec potrzeby | 118.5-119.5% | [P] |

- **Zastrzezenie (wer): w tym biegu nie bylo zimy.** Lato trwalo do ok. 130. doby, potem jesien na 432 dni [P, LOG:213, LOG:45849]. Zima przyjdzie ok. doby 560 [S] i wedlug kodu tnie plon wsi mniej wiecej o polowe. Zapas zboza w ksiedze towarow to 17 dni zuzycia i spada od doby 91 (168 877 -> 150 637) [P, LOG:129017]. Osobna proba zimy (120 dob na WGRANIU 35) wyszla bez glodu: ciecie plonu srednio 40.8%, zadna warownia nie glodowala [P, STAN-PRAC linie 1544-1546] - ale to byl inny bieg i krotszy.
- "Nie stac" rosnie caly rok [P, linia "Kiesa ludu", LOG:129238]:

| co | Q1 | Q2 | Q3 | Q4 | doba 364 |
|---|---|---|---|---|---|
| miast, gdzie lud ma ponizej 95% na jedzenie | 0.9 | 4.6 | 6.8 | 9.1 | 9 z 97 |
| miast z pelnym koszykiem ponizej 95% | 1.6 | 7.4 | 10.5 | 13.8 | 14 z 97 |
| zjedzone bez zaplaty, zl na dobe | 2 856 | 3 872 | 6 686 | 12 201 | 13 709 |
| zjedzone bez zaplaty, % wydatku | 1.16 | 0.81 | 1.29 | 2.25 | 2.54 |

- Prog "zjedzone bez zaplaty ponizej 1%": NIE (2.16% w oknie progu) [P, PROGI e5:101]. Wobec poprzedniego biegu lepiej (Q4: 9.1 miasta wobec 13.3; 2.25% wobec 2.90%).
- W dobie 364: 8 miast, ktorych lud zarabia razem 15 914 zl dziennie, a potrzebuje 36 097 [P, R-PRO-wer nr 6]. Najbiedniejsze: Ten Towers, New Ghys, Sisterton, Seagard, Celtigar Keep, Thenn. Hipoteza "wyspy i porty bez armii" nie ma danych (wer).
- Dlaczego rosnie [S]: wydatek ludu na jedzenie +33% (102 901 -> 137 155 zl), dobrobyt miast +31-34%, dochod ludu z zewnatrz tylko +18%. Moze to isc tez z ceny zboza (indeks 1.01 -> 1.47) - nie rozdzielono (wer).

### 3.2 Towary i ceny

Surowce (indeks 1.0 = cena bazowa, 10 = sufit; mediana po miastach) [P, linia "Ceny surowcow", LOG:129060] (R-GOS 2.1, potwierdzone w R-GOS-wer):

| surowiec | indeks d1 / d91 / d182 / d273 / d364 | miast BEZ towaru Q1 / Q2 / Q3 / Q4 (d364) |
|---|---|---|
| ruda (zelazo) | 10.00 / 0.88 / 0.61 / 0.55 / 0.52 | 44 / 19 / 9 / 7 (8) |
| drewno | 0.34 / 0.58 / 0.26 / 0.23 / 0.16 | 40 / 16 / 9 / 8 (8) |
| skory surowe | 0.37 / 0.98 / 1.24 / 1.53 / 1.51 | 18 / 10 / 12 / 13 (10) |
| skora wyprawiona | 10.00 / 1.29 / 1.59 / 1.89 / 1.85 | 33 / 34 / 39 / 43 (42) |
| len | 1.07 / 1.24 / 1.30 / 1.40 / 1.44 | 62 / 63 / 64 / 66 (70) |
| plotno | 1.01 / 10.00 / 10.00 / 10.00 / 10.00 | 86 / 88 / 90 / 89 (89) |
| welna | 1.74 / 1.38 / 1.06 / 0.89 / 0.85 | 34 / 16 / 8 / 7 (3) |

- Ceny ida za niedoborem we wszystkich 7 surowcach.
- **Zelazo naprawione.** Miast bez zelaza srednio w dobach 91-364: 11.7 (poprzedni bieg 41.4; prog 30) [P, PROGI e5:297]; w ostatnich 30 dobach 4.2 [P, PROGI e8:272]. Woz kupcow rundy 3: 7 228 kontraktow, 42 101 szt. rudy, dojechalo 7 102 wozy, 0 zawroconych [P, LOG:129190]. Udzial wozu (wer): 79% dowozu rudy MIEDZY miastami; 31% calego dowozu, gdy doliczyc wozy wsi z kopaln (81 661 szt.) - analityk podal 77% przez blad wzorca. Cykle warsztatow stracone z braku rudy: 551 -> 45 na dobe.
- **Plotno:** 83-93 miasta bez od doby 30 do konca; powstaje 164 szt. dziennie i znika tego samego dnia; w 72 miastach przerob stoi z braku lnu [P, LOG:129027, LOG:128928]. W poprzednim biegu tak samo. Woz nie pomaga, bo nigdzie nie ma nadwyzki.
- **Skora wyprawiona:** 33.4 -> 42.9 miasta bez; 96 cykli warsztatow dziennie traconych z braku skory [P]. Wobec poprzedniego biegu nie gorzej (tam 46.5), produkcja +30%.
- **Drewno:** zapas swiata 11 792 -> 152 862 ladunkow; przyrost przyspiesza: +72 / +350 / +517 / +649 na dobe w kwartalach (wer). Dwie przyczyny: las wsi daje wiecej (1 445 -> 1 861 dziennie), budowy biora mniej (822 -> 591 dziennie). W poprzednim biegu to samo (151 306). Test 120 dob tego nie widzial.
- **Towary "lepsze":** sol na suficie ceny (10.00); jajka puste w 94 miastach, miod i owoce w 97; garum, atramentu, barwnikow i klejnotow nikt nie produkuje (brak w ksiedze towarow) [P, LOG:129060, LOG:129048]. Z budzetu mieszczan na 15 takich towarow (160 276 zl dziennie) da sie wydac 38 289 (24%); caly budzet gry wydany w 47% (454 101 z 975 626) (wer).
- **Bron i zbroje:** na polkach miast 66 190 -> 321 682 szt.; zbroja korpusu t1-2: 6 338 -> 70 723; t3-4: 194 -> 129; t5-6: 45 -> 98 [P, LOG:496 -> 128991, LOG:678 -> 129174]. Doba 364: zbroja t3 - popyt 3 004, brak 2 851; t4 - popyt 2 182, brak 2 061 [P, warsztaty.log linia 1092]. Zalew t1-2 to lup sprzedany przez zolnierzy, nie produkcja: warsztaty robia juz glownie t3-5 (51.2 z 70.7 zbroi korpusu dziennie w Q4), rece platnerzy zajete w 98% (wer - naprawa "przesunac rece" jest bledna). Kolejka zamowien wojska maleje: 43 994 -> 14 740 szt.
- **Konie:** ceny lekko spadaja (bojowy 3 226 -> 2 708 zl), polki rosna (zwykly 1 616 -> 7 223; bojowy 116 -> 1 863) [P, LOG:709 -> 129221].
- **Ksiega towarow:** bez wyjasnienia netto +19 397 szt. w roku. Ale to plusy i minusy (wer): +124 625 (zboze +65 002, ryby +59 623) i -105 228 (22 towary), razem bez znaku ok. 230 tys. = 2.5-2.7% produkcji (8.55 mln bez rozdania startowego). To dwie osobne sprawy, nie jedna regula zamiany (korelacja dobowa 0.15). Trop (wer): reszta zboza idzie w parze z przyrostem zboza u lordow (korelacja 0.58; 14 259 -> 48 199 szt.), najwieksze skoki przy wybuchu wojen w dobach 23-24 w obu biegach - wskazuje na jedzenie dawane partiom lordow przez gre. Zrodla nie ustalono.

### 3.3 Produkcja, warsztaty, budowy

- Wsie: zboze 9 116 -> 8 641 dziennie (-5%), ryby ok. 2 330, len 420 -> 501, welna 272 -> 308 [P, linie "Towary:"]. Wioski: puste osady 0 -> 824, spalonych 27-28 z 2 446 [P, LOG:128977].
- Warsztaty zbrojne: wykonano 1 453 -> 1 212 szt. dziennie; stracone cykle: brak surowca 944 -> 176, "bez zysku" 223 -> 549, "brak zlota albo kupca" 129 -> 308; rece bezczynne 2.3% [P, LOG:128929]. Wobec poprzedniego biegu +16% sztuk w Q4.
- Warsztaty towarowe (291): pracuje 211-218 (72-75%); bankructw 6 (wszystkie w Q4) [P, LOG:128955]. Kopalnie BannerKings w miastach: 13 przez 273 doby, 7 w dobie 364, pracuje 0 - swiat sam je zamyka (6 bankructw to wlasnie one) (wer).
- Budowy: idzie w 29 z 227 osad, stoi w 198; wstrzymane wojna (cywilne) 189; z braku materialow 0. Ukonczonych poziomow w roku: 63. 144 osady stoja co najmniej 300 dni, mediana 342 dni [P, LOG:128995, budowy.log]. To zamierzona regula (cywilne czekaja w wojnie), ale w tym roku wojna nie konczyla sie nigdy. W poprzednim biegu tak samo (198 stoi). Laczy sie z zalewem drewna.

### 3.4 Handel i kasy

- Karawany: 416 -> 627 (doba 91) -> 556 (doba 364) [P, LOG:584 -> 129083]. Nowych w roku 1 733, w bitwach padlo 554 (512 przez lordow, 38 przez bandy); stan urosl o 140 - co sie stalo z ok. 1 040 pozostalymi, nie wiem (wer).
- "177 z 535 karawan nie ma na zold" to stan kasy dnia, nie rachunek zysku (wer). Notable doplacaja 104 tys. zl dziennie (dwie doplaty) i dostaja z aktywow 104 tys.; strata netto 21 tys. dziennie srednio, 13.4 tys. w Q4; kiesy notabli 30.5 -> 21.8 mln [P, LOG:129216]. Zysku karawan log nie liczy - nie wiem.
- Wozy wsi: utarg 301-352 tys. zl dziennie; rozbitych w roku 3 972 (3 014 przez bandy), rosnie 7.3 -> 13.8 na dobe [P, LOG:129063]. Niesprzedane w miastach 0.181% (prog 0.1%; glownie Thenn).
- Kasy kupcow miast: 27.67 mln (doba 1) -> 10.82 mln (doba 30, zaplanowany dar startowy) -> 10.93 mln (doba 364). Miast ponizej zapasu kupcow: 7.2 / 10.5 / 13.0 / 16.1 w kwartalach; 17 w dobie 364 [P, KM].
- **Thenn i Frostfang's Camp** (Wolni Ludzie): kasa ponizej 1 000 zl przez 38 dob w roku (Thenn 30, Frostfang's Camp 9; poprzedni bieg: 1 doba). Thenn: 331 dob ponizej zapasu, kasa 7 zl w dobie 321; lud stac na 40-69% jedzenia; karawana kupca stoi tam bez statkow 289 dob [P, KM, KLU, LOG:129081]. Przyczyny nie znam. Tropy: oba naleza do 8 miast z zerem mieszkancow osady w ksiedze ludzi, a ich dobrobyt rosnie o 37-43%, wiec rosnie wymagany zapas kupcow (wer). Dowodu na runde 3 nie ma; to 1 miasto z 97 i jedna para biegow.

### 3.5 Ludzie

- Ludnosc: 51.99 -> 51.47 mln (-0.99%); miasta -1.07%, wsie -0.97% [P, LR]. Ludzi zywych ubywa 505 na dobe = -0.35% na rok po doliczeniu uchodzcow (w pasie decyzji Jeffa z 12.10) [P, PROGI e4:121].
- Najgorsze regiony: Sharp Point -32.8%, Rathylar -25.6%, Rosby -22.1%. Krolestwa: Dragonstone -7.6%, Sarnor -6.6%, Wolni Ludzie -4.2% [P].
- Dobrobyt miast +34.4% (464 917 -> 624 706), zamkow +107%, rowno co kwartal (+8.0 / +8.5 / +7.7 / +6.5%), w poprzednim biegu +33.4% [P, KM]. Przyczyny nie ustalono - nie wiem.
- Rachunek ludnosci BannerKings rozjezdza sie z nasza ksiega (wsie -18.4%, zamki +45.1% wobec -1%) - to dwa rozne modele z zalozenia; nie ma dowodu, ze cos od tego zalezy (wer).
- Kiesy mieszczan: 10 najbogatszych miast ma 36% pieniedzy ludu, 31 najbiedniejszych 1.5%; mediana kiesy 41 dni wydatkow; ponizej 14 dni: 33 miasta [P, KLU ostatnia probka]. Najbogatsze: Riverrun 2.50 mln, Vaes Dothrak 2.17 mln, Winterfell 1.73 mln.
- Skad lud ma dochod (doba 364): wojsko 140 803 zl = 27% calego dochodu ludu (519 339) i 63% dochodu z zewnatrz (wer).

### 3.6 Porownanie z poprzednim biegiem (wybrane)

| co | poprzedni bieg | ten bieg |
|---|---|---|
| miast bez zelaza, srednio doby 91-364 | 41.4 | 11.7 |
| miast bez rudy w dobie 364 | 55 | 8 |
| cykle warsztatow stracone z braku rudy, Q4 | 486 | 45 |
| miast bez welny, doba 364 | 35 | 3 |
| zapas drewna, doba 364 | 151 306 | 152 862 |
| dob z kasa miasta ponizej 1 000 zl | 1 | 38 |
| miast, gdzie ludu nie stac na jedzenie, Q4 | 13.3 | 9.1 |
| zamkow bez kredytu dworu, doba 364 | 45 | 26 |
| karawany, doba 364 | 439 | 556 |
| towary bez wyjasnienia w roku (netto) | -296 183 | +19 397 |
| zloto swiata, zmiana w roku | -2 373 117 | +130 882 |

[P] (R-GOS pkt 7).

## 4. Wojna i swiat

### 4.1 Ile wojen

- Od doby 31 do konca: srednio 16.6 par w wojnie (15-18), srednio 25.8 krolestw w wojnie (25-26 z 29) [P, linie KRONIKA WOJEN w CS; PROGI e7:55]. Poza wojna caly rok tylko Skagos, Summer Isles i Yi Ti Exiles.
- Zdarzenia roku [P, linie "Wojna i pokoj (E7/0)"; R-WNA 1.2, R-WNA-wer nr 6]: wypowiedzenia wojny 103 (86 przez Realm of Thrones: 13 pierwszych + 73 ponowne; 17 z wezwania sojusznika); pokoje 88 (80 z decyzji krolestwa: 73 w wojnach fabularnych + 7 w zwyklych; 8 przez zerwanie wezwania). Krolestwa zniszczone 0, bunty 0 (poprzedni bieg: 3 bunty).

### 4.2 Czternascie wojen fabularnych

[P] linie E7/0, BIT (bitwy lordow), LR (twierdze) (R-WNA 1.3). Kolumna "koniec przymusu" (wer): z kodu Realm of Thrones 8.1.8 (lista wojen z dniem startu i konca; nasz rozrusznik fabuly mnozy oba dni x4.33).

| para (A - B) | wojna od doby | pokoje zerwane tego samego dnia | bitwy lordow (wygrane A : B) | twierdze | koniec przymusu |
|---|---|---|---|---|---|
| Free Folk - Nights Watch | od startu | 3 | 7 (2 : 5) | bez zmian | bez konca |
| Dragonstone - Stormlands | 1 | 10 | 384 (212 : 172) | Stormlands +4 zamki | bez konca |
| KL - The North | 9 | 4 | 57 (7 : 50) | North +1 (Harrenhal, z fabuly) | do Krwawych Godow (ok. doba 953) |
| The Vale - Iron Islands | 22 | 0 | 0 | bez zmian | doba 433 |
| The Reach - Dorne | 22 | 6 | 165 (108 : 57) | Reach +4 | doba 433 |
| Braavos - Lorath | 22 | 8 | 210 (77 : 133) | Braavos +1 | doba 433 |
| Tyrosh - Lys | 22 | 7 | 151 (101 : 50) | Tyrosh +1 | doba 433 |
| Pentos - Myr | 22 | 4 | 121 (57 : 64) | bez zmian | doba 433 |
| Norvos - Qohor | 22 | 8 | 279 (97 : 182) | bez zmian | doba 433 |
| Volantis - Aegon | 22 | 2 | 25 (16 : 9) | Volantis +1 | doba 433 |
| Qarth - House Targaryen | 22 | 6 | 92 (42 : 50) | bez zmian | doba 433 |
| Dothraki - House Targaryen | 22 | 6 | 55 (37 : 18) | bez zmian | doba 433 |
| Sarnor - Ibben | 22 | 2 | 34 (10 : 24) | bez zmian | doba 433 |
| KL - Riverlands | 130 | 7 | 220 (118 : 102) | KL +1 | bez konca |

- Pokojow fabularnych 73, wznowionych tego samego dnia 73, nie wznowionych 0 [P, CS:9240]. Od ponownego wypowiedzenia do nastepnego pokoju mija 14-127 dob, mediana 47.
- 7 z 14 wojen nie zmienilo ani jednej twierdzy (Norvos - Qohor: 279 bitew lordow i zero zmian na mapie).
- **Spor miedzy rozdzialami i moje rozstrzygniecie.** R-PRO-wer pisze, ze "wojna zawsze" nie jest skutkiem pierwszego roku. R-WNA-wer pisze, ze 10 z 14 wojen ma w kodzie koniec okna w dobie 433. Sprawdzilem: log sesji ma linie "Fabula: wojny fabularne x4.33 (StartDay/EndDay)" [P, CS:285], audyt w repo podaje te same okna (docs\AUDYT-KALENDARZ.md linia 241: "5-100 -> 22-433"), a start 10 wojen w dobie 22 i KL - Riverlands w dobie 130 zgadza sie z logiem. **Racje ma R-WNA-wer**: caly rok 1 lezy w oknie wymuszonych wojen. Po dobie 433 bez przymusu zostaje 17 krolestw (cale Essos, Dolina, Dorne, House Targaryen, Dothrakowie) [S z listy]. W przymusie zostaje rdzen Westeros; dochodza nowe: Zelazne Wyspy - Polnoc od doby 433 do 1018, od doby 585 Dragonstone - KL, Stormlands - KL, Reach - Dragonstone [P z kodu]. Czy pokoj sie potem utrzyma - nie wiem, bieg skonczyl sie w dobie 364.
- Mechanizm "pokoj z biedy": 0 wnioskow w roku, powod "tylko wojny fabularne" [P, CS:9239]. Gra pytala o pokoj 6 469 razy, 5 634 razy odpowiedz "wojna trwa za krotko" - bo po kazdym pokoju wojna zaczyna sie od nowa.
- Fabula w roku odpalila 5 zdarzen, 17 jest zaleglych na osi [P, CS:9234]. Czy z graczem fabula szlaby dalej i konczyla wojny - nie wiem.

### 4.3 Wojny zwykle, wezwania, reparacje

- Z wezwania sojusznika: 8 par, 17 odcinkow: 7 skonczylo sie prawdziwym pokojem po 43-160 dobach, 8 zerwaniem wezwania, 2 trwaja [P]. Te wojny sie koncza.
- Wezwania do wojny (wer): 18 porozumien podpisanych, 17 dalo wojne, 8 zerwanych z braku wplywow dnia; zaplacono sojusznikom 1 790 085 zl, skreslono 5 396 992 zl; 4 wojny trwaly 1 dobe [P, linie "Wezwania do wojny (168)"]. Pentos wezwal Tyrosh 5 razy: pierwszy raz za 15 436 zl (oplacone, wojna 61 dob), 4 razy za 0.87-1.03 mln - wszystkie 4 zerwane [P, LOG:17900, 40827, 61036, 80628, 107600]. "Nie stac" znaczy: rata ponad wplyw dnia, przy skarbcu ok. 1 mln - zderzenie reguly gry z nasza. Ceny wezwan sa dwojakie (4-39 tys. albo 0.56-1.14 mln); od czego to zalezy - nie wiem.
- Skagos (wer): Nocna Straz placila mu 3 359 zl dziennie przez 35 dob = 117 565 zl; Skagos nie ma ani jednej doby wojny; gra skreslila 493 769 zl w dobie 87 [P, LOG:18251, LOG:30746].
- Reparacje: w wojnach fabularnych nie naliczono 12 313 400 zl w 72 pokojach - ale to ta sama wojna liczona do 10 razy (wer). Przy jednym pokoju na wojne: 2 071 200 - 2 832 100 zl [P, CS linie "Reparacje (T9)"]. Nic nie przepada: kwot nie naliczono (nasza latka, decyzja Jeffa 08.10, bo placily z niczego). Naprawde zaplacone w zwyklych wojnach: 213 600 zl w 2 pokojach. Placi zawsze strona, ktora zawiera pokoj (72 z 72) - to definicja moda Diplomacy, nie sprzecznosc z bitwami (wer).

### 4.4 Bitwy i oblezenia

[P] BIT (linie B175 i H3), LOG:129204 (R-WNA pkt 2).

| rodzaj | w roku | na dobe |
|---|---|---|
| bitwy lordow (lord lub armia po obu stronach) | 1 869 | 5.13 |
| rabunki wsi | 2 220 | 6.1 |
| szturmy | 23 (14 udanych) | 0.06 |
| bitwy pod murami i wypady | 61 (40 wypadow + 21 pod murami) | 0.17 |
| bitwy morskie | 131 | 0.36 |
| wszystkie bitwy polowe z ksiegi przegranych | 8 266 | 22.7 |

- Silniejszy wygrywa 1 849 z 1 869 bitew lordow (98.9%); przy przewadze 1.5 do 1 i wiecej - zawsze.
- Straty w polu: przegrani 337 935 ludzi - zabici 22%, jency 6%, rozbici 72%; zwyciezcy zabici 1.8% [P, linie "Przegrani (H3)"]. Gra bez naszej reguly zabilaby 173 236 zamiast 74 744.
- Wszyscy zabici w roku: 260 864, w tym z druzyn lordow 146 593, z band 37 985, z taborow wsi 25 105, z karawan 19 267 [P].
- Szturmy: udane - atakujacy 47 013 ludzi, stracili 13 663 (29%); najmniejsza przewaga 3.6 do 1, mediana 6.47 do 1 (wer). Nieudane - atakujacy stracili 31%.
- Cena wojny: 14 twierdz za 146 593 zabitych z druzyn lordow = ok. 10.5 tys. na twierdze [S: dzielenie dwoch pomiarow; R-WNA-wer 4.6].
- Oblezenia rzadko sie udaja takze dlatego, ze armia oblegajaca ginie w calosci po wypadzie: 38 razy w roku przy 14 zdobytych twierdzach (pkt 1.7).

Twierdze, ktore zmienily pana (14 z 227; 11 zamkow, 3 miasta; kazda raz) [P, BIT, LR]:

| doba | twierdza | od | do | atakujacy / zaloga | straty atakujacego |
|---|---|---|---|---|---|
| 51 | Cape Wrath (zamek) | Dragonstone | Stormlands | 1649 / 109 | 123 |
| 62 | Fellwood (zamek) | Dragonstone | Stormlands | 1803 / 110 | 136 |
| 64 | Saltpans (miasto) | Riverlands | The Vale | 3500 / 756 | 1224 |
| 67 | Starfall (miasto) | Dorne | The Reach | 5035 / 573 | 1059 |
| 83 | Demongate (zamek) | Aegon | Volantis | 2324 / 299 | 891 |
| 113 | Blackmont (zamek) | Dorne | The Reach | 3205 / 341 | 472 |
| 132 | Harrenhal (miasto, z fabuly) | KL | The North | 10321 / 2459 | 4804 |
| 158 | Sandstone (zamek) | Dorne | The Reach | 2339 / 303 | 650 |
| 194 | Kingsgrave (zamek) | Dorne | The Reach | 3662 / 573 | 1005 |
| 209 | Sombazmion (zamek) | Lys | Tyrosh | 1122 / 349 | 453 |
| 223 | Sweetport (zamek) | Dragonstone | Stormlands | 3340 / 535 | 1157 |
| 225 | Bisa (zamek) | Lorath | Braavos | 1529 / 452 | 620 |
| 236 | Stonedance (zamek) | Dragonstone | Stormlands | 2658 / 192 | 489 |
| 298 | Willow Wood (zamek) | Riverlands | KL | 4526 / 348 | 580 |

Krolestwa - twierdze i bitwy lordow [P, BAL] (R-WNA 2.3):

| krolestwo | twierdze start -> koniec | bitwy lordow wygrane / przegrane |
|---|---|---|
| The North | 22 -> 23 | 50 / 7 (88%) |
| House Baratheon of King's Landing | 21 -> 21 (+1 -1) | 126 / 152 (45%) |
| The Reach | 16 -> 20 | 108 / 57 (65%) |
| Dorne | 16 -> 12 | 57 / 108 (35%) |
| Riverlands | 14 -> 12 | 113 / 121 (48%) |
| The Vale | 13 -> 14 | 3 / 11 (21%) |
| Stormlands | 13 -> 17 | 172 / 212 (45%) |
| Iron Islands | 10 -> 10 | 0 / 1 |
| Dragonstone | 10 -> 6 | 212 / 172 (55%) |
| Dothraki Horde | 7 -> 7 | 37 / 18 (67%) |
| Free Folk | 7 -> 7 | 2 / 5 |
| House Targaryen | 6 -> 6 | 74 / 92 (45%) |
| Volantis | 6 -> 7 | 20 / 9 (69%) |
| Lorath | 5 -> 4 | 133 / 77 (63%) |
| Nights Watch | 5 -> 5 | 5 / 2 |
| Braavos | 5 -> 6 | 77 / 133 (37%) |
| Pentos | 5 -> 5 | 59 / 64 (48%) |
| Norvos | 5 -> 5 | 97 / 182 (35%) |
| Qohor | 5 -> 5 | 182 / 97 (65%) |
| Myr | 5 -> 5 | 76 / 74 (51%) |
| Lys | 5 -> 4 | 50 / 103 (33%) |
| Tyrosh | 5 -> 6 | 118 / 62 (66%) |
| Sarnor | 5 -> 5 | 10 / 24 (29%) |
| House Targaryen, Aegon | 5 -> 4 | 22 / 22 (50%) |
| Qarth | 4 -> 4 | 42 / 54 (44%) |
| Ibben | 4 -> 4 | 24 / 10 (71%) |
| Skagos, Summer Isles, Yi Ti Exiles | 1 -> 1 | 0 / 0 |

- Nie ma kuli snieznej: najwiekszy zysk +4 (Reach, Stormlands), najwieksza strata -4 (Dragonstone, Dorne). Zadne krolestwo nie upadlo, zaden z 347 rodow nie zniknal, zaden rod z lennem nie zmienil krolestwa [P].
- Wobec poprzedniego biegu: zmian pana twierdzy 14 zamiast 41 (wtedy 12 twierdz zmienialo pana wiecej niz raz), buntow 0 zamiast 3. Czy to skutek poprawek, czy przypadek - nie wiem (jeden bieg).
- Zwyciestwo nie daje zlota w skarbcu (Stormlands +4 twierdze: 0.80 mln; Dorne -4: 1.00 mln), ale skarbiec nie mierzy wygranej - wiekszosc stoi na linii reguly 1/360 (pkt 2.3) (wer).

### 4.5 Uchodzcy, wyrzutki, bandy

- Rabunki wsi: 2 220 bitew; sesji rabunku 1 485, z tego doprowadzonych do konca 345 (77% przerwanych); ludzi trafionych 230 527: zabici 6 422 (2.8%), do lasu 8 037 (3.5%), uchodzcy 216 069 (94%) [P].
- Uchodzcy poza domem: 1 090 (doba 30) -> 65 633 (doba 182) -> 187 910 (doba 363) [P, LOG:129272]. W roku przybylo 216 068, wrocilo 24 232 (11%), zmarlo 1 002. Poprawka (wer): powroty rosna co kwartal 1 -> 20 -> 82 -> 164 ludzi na dobe, naplyw stanal (833 -> 796 na dobe), calosc to 0.46% ludnosci wsi, a szkoda dla plonu dotyczy jednej wsi swiata (Eastpoint, x0.43; "Sharp Point 31.5%" to caly okreg). Prognoza analityka "430 tys. po 2 latach" to gorna granica.
- Pula wyrzutkow: 819 -> 11 869; tempo +40 / +53 / +4.8 na dobe w dobach 1-120 / 121-244 / 245-364 - od doby 245 prawie stoi [P]. Naplyw w roku 129 678 (61% rozbici z bitew), odplyw 118 619 (61% rodzi nowe bandy, 18% wraca do domu). Powrot do domu jest 4 razy wolniejszy w regionach "w wojnie", a tak liczy sie 212 z 227 regionow (19 298 powrotow z regionow w wojnie, 55 z regionow w pokoju) [P].
- Bandy: 314 / 5 694 ludzi -> 819 / 17 279; tempo +62 / +29 / +20 ludzi na dobe - zwalnia, nie stoi [P, LOG:129134]. Wobec poprzedniego biegu lepiej (937 / 20 501). Progi band i puli maja baze z doby 120 biegu bazowego - porownanie z doba 364 jest nietrafne (wer); w dobie 120 tego biegu bandy byly ponizej bazy (652 / 12 153), pula ponad (5 022 wobec 3 053).
- Bandy wygrywaja coraz wiecej malych starc: 378 / 708 / 821 / 904 na kwartal (wer); od Q2 do Q4 to +27% wygranych przy +20% band; 89% ofiar to tabory wsi. Szkody dla handlu nikt nie zmierzyl.

### 4.6 Jency, sluby, turnieje, najemnicy

- Jency szeregowi: wzieto 105 180, odbito 21 589; prawo jenca: do domu 26 394, na Mur 224, sprzedani w krainach z niewola 20 604 [P, LOG:128989]. Rownanie jencow zamyka sie z reszta 1.5% obrotu.
- 440 z 528 lordow uwolnionych "przez pokoj" wyszlo w dobach pokoju fabularnego trwajacego zero dni - za darmo, a wojna trwala dalej [P, potwierdzone dwiema metodami].
- Sluby rodow: 215 w roku (poprzedni bieg 113) [P, CS:9237]. Wolni lordowie szukajacy pary: 669 -> 576; 229 z nich nie ma ani jednego kandydata z szansa (w dobie 2: 28). Jeff zdecydowal przyhamowac (cel ok. 150 rocznie; test w toku).
- Dorosli zywi lordowie AI: 1 227 -> 1 662 [P, LOG:683, LOG:129182]. Zgonow i narodzin log nie liczy - nie wiem.
- Turnieje: 524 starty, najdluzsza przerwa 6 dob [P, PROGI e7:52].
- Najemnicy: 11 kompanii zmienilo pracodawce 762-764 razy; mediana sluzby 3 doby; 151 sluzb jednodniowych; 11 razy prosto do wroga poprzedniego pana; w obu biegach (681 poprzednio) [P, BUD]. Pomiar z migawki dobowej - to dolna granica.

### 4.7 Krainy szczegolne i pory roku

- Nocna Straz: druzyny 1 191 -> 4 077; nie rabuje (0 rabunkow); ludzi bierze z Muru 2 145, z wyrzutkow 1 439, z Westeros 2 658 [P, LOG:129165]. Zgodnie ze swiatem.
- Wolni Ludzie: druzyny lordow 1 344 -> 720 (szczyt 3 627 w dobie 33); zginelo 4 197, z rak Innych 3 382; jeden rabunek, zero szturmow [P].
- Inni: spia do dnia 728; 195 prob oblezen zablokowanych; rosna tylko z poleglych 616 -> 3 699 [P, CS:9242-9243, LOG:129155]. Zgodnie z planem.
- Dothrakowie: wojna tylko z House Targaryen (55 bitew, 37 wygranych); jazda 43.9% przy progu 60%, 27.8% czeka na konia [P, PROGI e5:366-368]. "Nie ruszaja Wolnych Miast" to nie dziw - nie mieli z nimi wojny (wer).
- Zelazne Wyspy: 342 doby wojny z Dolina, w roku 1 bitwa lordow i 0 rabunkow wsi; wojsko 5 849 (szczyt) -> 1 428; kiesy rodow -59% [P]. Koszt wojny nie jest wykazany - wojsko topnieje tez w krolestwach bez wojny (Skagos 853 -> 253, Yi Ti 698 -> 68) (wer). Przymus wojny z Dolina konczy sie w dobie 433.
- Pory roku: lato do ok. 130. doby, potem jesien (432 dni). W roku 1 zimy nie bylo [P, LOG:213, LOG:45849].

## 5. Progi, bledy, czas

### 5.1 Progi

- Wierszy progow 1 161, z tego 673 z werdyktem (488 to wiersze informacyjne). NIE: 45 = 6.7% sprawdzanych progow. Czeka na decyzje Jeffa: 4. Poprzedni bieg roczny: NIE 41 przy 1 124 wierszach [P, R-PRO-wer nr 1].
- Runda 3 naprawila 9 pozycji (NIE -> TAK), formalnie zepsulo sie 11 (TAK -> NIE). Nowe progi rundy 3: 37 wierszy - TAK 22, NIE 2, informacyjne 13 [P].
- Werdykty 49 wierszy NIE i DECYZJA: gorzej niz rok temu 8 (5 przyczyn), prog do zmiany 7, poprawa (ale prog dalej niespelniony) 19, znane bez poprawy 15 [P, R-PRO pkt 1].

Co sie poprawilo po rundzie 3 - wybrane [P, PROGI] (R-PRO pkt 2 i 3):

| co | poprzedni rok | teraz | prog |
|---|---|---|---|
| glowy rodow z kiesa ponizej 5 000 zl (doba 364) | 21 | 5 | <= 10 |
| wojsko lordow srednio w dobach 90-120 | 93 392 | 98 796 | 95-115 tys. |
| podzamcze stac na jedzenie | 90.3% | 95.5% | >= 95% |
| miasta bez zelaza (srednio doby 91-364) | 41.4 | 11.7 | <= 30 |
| karawany stojace 7+ dob w tym samym miescie | 13 | 7 | <= 10 |
| rody z lennem bez renty korony | 27% | 5.0% | <= 10% |
| zold zwalnianego zolnierza | 3.65 zl | 5.52 zl | >= 5.0 zl |
| kiesy 11 kompanii najemnych, przyrost | +48.7 tys./d | +7.7 tys./d | <= +10 tys./d |
| dlug wezwan splacany w nicosc | 2.01 mln | 0 | 0 |
| zelazo bez wyjasnienia w ksiedze | -1 474 lup | -38 lup | >= -150 |

Wazniejsze progi niespelnione [P, PROGI] (R-PRO pkt 1):

| co mierzy | teraz | poprzedni rok | prog | ocena |
|---|---|---|---|---|
| wojsko w druzynach lordow, krolestwa w wojnie (srednia 28 dob) | 79 409 | 64 013 | podloga 85 tys. | lepiej o 15.4 tys., brakuje 5.6 tys. |
| rody z lennem bez ani jednego zolnierza | 14 z 205 | 29 | <= 12 | lepiej, brakuje 2 |
| zold na czlowieka: rody na pulapie wobec rodow z luzem | 1.38x | 1.51x | <= 1.25x | lepiej; patrz uwaga nizej |
| miasta z kasa ponizej 1 000 zl | 38 dob | 1 doba | 0 | GORZEJ (Thenn) |
| towar wsi niesprzedany w miastach | 0.181% | 0.077% | < 0.1% | GORZEJ (Thenn) |
| dlug koron w Banku rosnie w II polroczu | u 16 z 26 | u 19 | 0 | lepiej, dalej NIE |
| lud zjadl bez zaplaty | 2.16% | 3.62% | < 1% | lepiej |
| partie z zaleglym zoldem | 2.49% (w zlocie 0.12%) | 3.17% | <= 2% | lepiej |
| sakiewki zolnierzy w dniach zoldu | 26.1 | 24.7 | <= 15 | znane; prog starszy niz regula oszczednosci |
| jazda t3+ wobec celu | 63% | 64% | >= 90% | znane |
| kolejka odziezy wojska d90 -> d364 | 9 910 -> 10 912 | 10 379 -> 9 299 | bez wzrostu | GORZEJ (wojska jest wiecej [S]) |
| ksiega koni - reszta bez wyjasnienia | 47 z 47 tygodni | 47 z 47 | 0 | znane |
| stos drewna w miastach | +426/d | +373/d | <= 290/d | znane, gorzej |
| pula wyrzutkow doba 364 | 11 869 | 9 788 | <= 3 053 | prog do zmiany (baza z doby 120) |

- Uwaga do "biedny rod ma drozszego zolnierza" - **spor miedzy weryfikatorami**. R-PRO-wer: to glowna przyczyna braku do 85 tys.; gdyby rod na pulapie mial sklad jak rod z luzem, ten sam zold oplacilby +11.8 tys. ludzi. R-PIE-wer: to artefakt porownania - sklad druzyn, nie cena. Sprawdzilem w BUD (r1) [P]: rod na pulapie placi 7.01 zl na czlowieka, rod z pelna druzyna (bogaty) 6.23 zl, rod z luzem 4.48 zl; kompanie 6.82 zl. Czyli bogaty rod z pelna druzyna placi prawie tyle, co rod na pulapie; tani jest tylko rod w odbudowie (swiezy rekrut: 2.5 zl; szczebel 3-4: 6.9 zl; 5+: 14.4 zl - LOG:129141). **Racje ma R-PIE-wer co do nazwy** (nie "biedny placi drozej", tylko "weterani kosztuja"), a R-PRO-wer co do skutku (drozszy zolnierz to glowny powod spadku glow, pkt 1.6). Liczba +11.8 tys. to gorna granica i oznaczalaby wojsko nizszego szczebla - to decyzja projektu, nie blad.
- Progi do zmiany, nie bledy swiata (7): podloga wojska (zapis "przy 26 krolestwach w wojnie"), udzial panow samych zamkow w dochodzie (36.6% przy pasmie 30-35%), pula wyrzutkow, kiesa na karawane wobec wzoru, sprzedaz nadwyzek sprzetu zalog, zatkana wies bez wozu, przebudowy pamieci polki [R-PRO pkt 1].
- Czekaja na decyzje Jeffa (4): miasta, gdzie ludu nie stac na jedzenie (srednio 9.6; projekt z gory zaklada NIE); pelny koszyk ludu (swiat 98.2%; 14.1% miast ponizej 95%); polki uprzezy rosna (+71%); cena bardy najwyzszego szczebla 2 760 zl przy progu 2 600.

### 5.2 Bledy

| co | ten bieg | poprzedni rok |
|---|---|---|
| linie ERROR w logu Armoury | 0 z 129 290 | 0 z 129 051 |
| linie WARN, wyjatki, potkniecia | 0 | 0 |
| bloki bledow CrashScribe | 23 (22 przed wejsciem na mape) | 23 - te same sygnatury |
| bloki "blad w naszym modzie" | 0 | 0 |
| wyjatki pierwszej szansy w kampanii | 2 432 (6.7 na dobe), zgloszonych 0 | 4 271 |

[P] (R-PRO pkt 4, potwierdzone R-PRO-wer nr 12).

- 23 bloki startowe to wyjatki cudzych modow: BannerKings 14, BKROTPatch 6, TournamentsXPanded 1, UIExtenderEx 1, oraz 1 zlapany w srodku Harmony ze stosem przez Armoury.WesterosClimate (log po nim potwierdza, ze latka jest wpieta). Te same w 4 sesjach tej nocy.
- Wywrotka przy wyjsciu z gry: po zapisie (07:38:31 zapis OK, 07:38:37 wywrotka silnika), ten sam stos silnika co w biegu z 05:11. Znany blad zamykania, nie wplywa na wynik. Test 120 dob wyszedl bez wywrotki - blad jest losowy.
- Test zapisu: wczytany zapis z doby 360 + 9 dob, 0 ERROR [P, STAN-PRAC linia 1648].

### 5.3 Czas

| okno dob | ten bieg, s/dobe | poprzedni rok, s/dobe | pamiec, MB |
|---|---|---|---|
| 1-28 | 10.63 | 10.64 | 1 218 |
| 85-112 | 12.01 | 13.03 | 1 357 |
| 197-224 | 12.39 | 14.80 | 1 448 |
| 337-364 | 12.40 | 12.63 | 1 517 |
| caly rok | 11.98 (mediana 11.76) | 12.98 | - |

[P] pole "doba N ms (zegar)" (R-PRO pkt 5).

- Gra zwalnia tylko na poczatku: doby 2-120 +1.49 s na 100 dob (swiat sie zapelnia), doby 120-364 +0.01 s na 100 dob.
- Nasze mody: 1 468 ms na dobe = 12.2% doby (poprzednio 1 325 ms). Najdrozsze: takt doby 359 ms, godzina 273 ms, wejscia do osad 255 ms, musztra w treningu gry 127 ms.
- Koszt rundy 3: wozy surowcow 1.7 ms na dobe; budzet rodow +0.7 ms.
- Swaty: 471 -> 697 ms na dobe w ciagu roku (srednio 611); do doby 100 idzie za liczba szukajacych, potem rosnie dalej (+12%) mimo mniejszej liczby ocen par - przyczyny nie znam (wer).
- Pamiec rosnie caly rok (1 218 -> 1 517 MB), ale coraz wolniej: 1.65 -> 0.81 -> 0.49 MB na dobe (wer). Wyglada na zapelnianie swiata; pewnosc da bieg dwuletni.
- Blad stopera w dobie 215 (wejscia do osad 25.7 s przy dobie 14.6 s) - pomiar, nie gra.

### 5.4 Spojnosc ksiag

| ksiega | wynik | zrodlo |
|---|---|---|
| pieniadz - suma swiata | +130 882 zl w roku (+0.063%); poprzedni rok -2 373 117 | [P] LOG:707, 129205 |
| pieniadz - bez platnika, bez odbiorcy | 0 / 0 | [P] PROGI e3:16-20 |
| kiesy ludu, "zakupy" z niczego | 0 dob poza progiem | [P] PROGI e5:98, e5:100 |
| ludzie - ognie domowe, rownanie krain | reszta 0.000; najgorsza kraina 115 ludzi | [P] PROGI e4:14, e4:147 |
| pula wyrzutkow, konto uchodzcow | 0.00 w 363 dobach | [P] PROGI e4:80, e4:107 |
| Nocna Straz, ksiega wiosek | 56 ludzi w dobie 175; 6 ludzi w dobie 229 (pojedyncze doby) | [P] PROGI e4:264, e4:243 |
| KONIE | +54 358 koni w roku bez wyjasnienia, prawie wszystko juczne (+56 478); stan 21 839 -> 66 616 | [P] PROGI e5:257 |
| UPRZAZ | -7 395 z 14 995 szt. (prawdopodobnie brak licznika zapisow notabli, nie przeciek) | [P] PROGI e8:181 |
| TOWARY | netto +19 397 szt.; zboze +65 002, ryby +59 623, 22 inne towary -105 228 | [P] R-GOS-wer 2.D |
| zelazo | -38 lup | [P] PROGI e8:283 |

- Konie juczne: 85% reszty to staly doplyw 116-155 jucznych na dobe, rosnacy w drugim polroczu; w poprzednim biegu to samo (+56 728) - nie artefakt startu (wer). Sprawa znana (naplyw z taborow wsi), paczki nie ma.

### 5.5 Stan planu (zrodla do rozdzialu 0a)

PLAN = docs\PLAN-DO-KONCA-MODA-2026-10-09.md; SP:n = docs\STAN-PRAC.md, linia n.

| co | stan | zrodlo |
|---|---|---|
| Plan z 09.10: 8 etapow i zakonczenie, 127 pozycji | - | PLAN rozdz. 0-1 |
| Kod etapow 1-8 | w grze: WGRANIE 32 (etap 4 caly, etap 5 caly, etap 6 waski), 33 (etap 6 porcja 2), 34 (caly kod etapow 5-8 i poprawki, 13.10) | SP:1229, SP:1254, SP:1476 |
| Limit karawan kupca 3; czytelna data na pasku | w grze: WGRANIE 35 i 36 | SP:1505, SP:1576 |
| Runda 3 (wojsko, zelazo), pomiary, sluby rodow - kod tego biegu | w grze: WGRANIE 37 (13.10, 16:17); bieg kontrolny 10 dob po wgraniu: 0 ERROR | SP:1636-1667 |
| Proba zimy (osobny bieg 120 dob na WGRANIU 35) | zrobiona: 120 z 120 dob, zadna warownia nie glodowala; wojsko w zimie 80 353 | SP:1544-1546 |
| Hamulec slubow (cel ok. 150 rocznie; bylo 113, po zmianie 215) | kod gotowy, test 120 dob w toku; potem WGRANIE 38 | SP:1668-1684 |
| Danina z oszczednosci mieszczan (pierwsza paczka projektu kiesy ludu) | Jeff: tak; kod w toku; potem test 120 dob | SP:1654, SP:1662-1665 |
| Konie robocze mieszczan | kod gotowy i zrecenzowany, NIE wgrany (sam zabiera panom ok. 14.6 tys. ludzi); wejdzie razem z danina po biegu 120 dob | SP:1595-1600, SP:1655 |
| Lud doklada do budowy miasta | Jeff: tak (tylko budowle sluzace miastu); kodowac po daninie | SP:1679-1681 |
| Kara lojalnosci, zbytki | Jeff: tak; osobne kroki pozniej | SP:1654 |
| Podatki wojenne na wojsko: korona placi wedlug potrzeby, tarczowe, stawka wojenna podatku pana | Jeff: "1 2 i 3" - projektowac wszystkie trzy po tym raporcie | SP:1668-1670 |
| Lup zdobytego miasta | projekt w toku | SP:1660-1663 |
| Zakonczenie: strojenie liczb biegami rocznymi, przyspieszanie naszych i cudzych modow, cichsze logi, menu, test czteroletni z zima, dokumenty, instrukcja | nie zaczete; z cudzych modow zrobione swaty Realm of Thrones (ok. 685 ms na dobe zamiast ok. 1 450) | PLAN rozdz. 4; SP:1644 |
| Bieg dwuletni | nie zrobiony | SP:1531 |
| Testy reczne Jeffa (29 punktow), zdjecia proby drog i decyzja "ile drog", myto na Bliznakach | czekaja | SP:1497-1499 |

## 6. Lista niespojnosci

Kolejnosc wedlug wagi. "Werdykt" = wynik proby obalenia przez weryfikatora (prawdziwa / czesciowo / artefakt). Dowody z rozdzialow 1-5.

### 6.1 Waga wysoka

| nr | co nie trzyma sensu | dowod | werdykt | propozycja |
|---|---|---|---|---|
| 1 | Wojna fabularna nie konczy sie w roku 1; z tego ida: podatek "wojenny" jako staly, stojace budowy, jency za darmo, uchodzcy, wolny powrot wyrzutkow | 73 pokoje, 73 wznowione tego samego dnia [P, CS:9240]; 25-26 z 29 krolestw w wojnie od doby 23 | czesciowo: 10 z 14 par ma koniec przymusu w dobie 433; 4 bez konca z zamyslu Realm of Thrones | Najpierw bieg co najmniej do ok. doby 550 (lepiej dwuletni, bo zima przychodzi ok. doby 560). Dla 4 par bez konca: rozejm przed wznowieniem zmienia fabule - decyzja Jeffa |
| 2 | Wojska w polu 79.4 tys. przy podlodze 85 tys.; po wielkiej bitwie nie odrabia strat | 79 409 srednio; schodki w dobach 277 i 364; ok. 100-110 rodow z lennem na pulapie z druzynami w 37% [P] | prawdziwa | Pieniadz wojenny celowany (pkt 2.8): ok. 50 tys. zl dziennie do rodow na pulapie [S]; prog zapisac "przy 26 krolestwach w wojnie" |
| 3 | Zalogi zjadaja budzet wojska polowego | 47 rodow z zerowym pulapem na druzyny, 42 bez ludzi [P, LOG:129141]; zalogi 83.6 tys. ludzi, 190.9 tys. zl dziennie | prawdziwa (to decyzja projektu, nie blad) | Czesciowa doplata korony do zoldu zalog rodom na pulapie; pelna (143.7 tys.) jest za droga - do policzenia w projekcie podatkow |
| 4 | Armia oblegajaca pobita wypadem albo pod murami ginie w calosci | 38 wypadow wygral wychodzacy, oblegajacy stracili 55 018 ludzi (100%); pod murami 21 bitew, 18 480 ludzi [P] | czesciowo: fakt prawdziwy i wiekszy, opis analityka byl odwrotny | Regula "przegrani uchodza" takze dla pobitego oblegajacego i przegranego pod murami (nie "przegrany wypad wraca za mury") |
| 5 | Zloto osiada u mieszczan, a podatek wojenny prawie go nie zawraca | kiesy 6.28 -> 44.12 mln; lokata 29.5 mln; podatek 15 tys. dziennie przy zalozeniu 30-60 tys. [P] | prawdziwa, z zastrzezeniem: tempo gasnie (144 -> 70 tys.), w 70% to lustro zapasu koron | Danina z oszczednosci ponad zapas (Jeff: tak, kod w toku) |
| 6 | Lud ma pieniadze, a nie ma czego kupic | oferta nadwyzki 4.49 mln dziennie, wydatek 454 tys. [P, LOG:129238]; 4 towarow nikt nie produkuje; tak samo w poprzednim biegu | prawdziwa | Dac brakujacym towarom zrodlo albo zdjac je z koszyka; zbytki z projektu kiesy ludu (Jeff: tak) |
| 7 | Korona dzieli pieniadz bez celowania | do rodow na pulapie trafia 43-48% rent i 39-47.5% zwrotu zoldu [P]; te same wplywy koron w obu biegach (451 i 450 tys.), a wojska +15.4 tys. | prawdziwa (cecha reguly, nie blad) | Korona placi wedlug potrzeby (kierunek 1 z wyboru Jeffa) |
| 8 | Plotna nie ma caly rok | 83-93 miasta bez od doby 30; cena na suficie; w 72 miastach przerob stoi z braku lnu [P] | prawdziwa | Wiecej lnu u zrodla (wsie lniane; regulator plonow) |
| 9 | Dobrej zbroi brak, zlomu zalew | korpus t3-4 na polkach 129 szt. przy braku 4 912; t1-2: 70 723 szt.; druzyny: zbroja szczebla 70% [P] | czesciowo: fakt prawdziwy, naprawa analityka bledna | Wiecej rak platnerzy albo inne zrodlo zbroi t3-4; nie "przesuwac rak" (robia juz t3-5) |

### 6.2 Waga srednia

| nr | co nie trzyma sensu | dowod | werdykt | propozycja |
|---|---|---|---|---|
| 10 | Trzy krolestwa prawie bez wojska polowego | Free Folk 10% limitu, Ibben 13%, Iron Islands 19% [P] | prawdziwa; podatek od wlasnego ludu im nie pomoze | Osobne rozwiazanie w projekcie podatkow (np. doplata do zalog albo dar) |
| 11 | Budowy cywilne stoja caly rok | 198 z 227 osad; 144 ponad 300 dni; 63 poziomy w roku [P] | prawdziwa (zamierzona regula przy wojnie bez konca) | Budowac cywilne w osadach daleko od frontu (miara "styk z wrogiem" jest w kodzie) |
| 12 | Zalew drewna | 11 792 -> 152 862 ladunkow; +649 dziennie w Q4 [P] | prawdziwa | Regulator wyrebu wsi; razem z nr 11 |
| 13 | Skory wyprawionej brakuje | 33 -> 43 miasta bez; 96 cykli warsztatow dziennie traconych [P] | prawdziwa | Wiecej skor surowych u zrodla |
| 14 | Jazdy o jedna trzecia za malo | t3+ 17.6% przy celu 27.5%; czeka 5 453; wolnych koni 2 328 [P] | czesciowo: koni starcza dla mniej niz polowy czekajacych | Dowozic wolne konie zbrojowni + poprawka uprzezy z poprzedniego triazu |
| 15 | Ludu nie stac na jedzenie w ok. 9 miastach, choc jedzenia jest w brod | 0.9 -> 9.1 miast; zjedzone bez zaplaty 2.16-2.54% (prog 1%) [P] | prawdziwa (rosnie w roku; lepiej niz w poprzednim biegu) | Decyzja Jeffa z projektu; dochod dla biednych miast |
| 16 | Dobrobyt miast +34% w roku wojny, zamkow +107% | KM; rowno co kwartal; poprzedni bieg +33.4% [P] | prawdziwa; przyczyna nieznana | Rozpoznac, co podnosi dobrobyt (podnosi koszt koszyka i zapas kupcow) |
| 17 | Najemnicy zmieniaja pana co 3 doby | 762-764 zmian, 151 sluzb jednodniowych, 11 razy prosto do wroga [P] | prawdziwa | Najkrotszy czas sluzby (propozycja 28 dob czeka na Jeffa) i zakaz przejscia prosto do wroga |
| 18 | Ta sama osoba wykupywana wiele razy | 52 osoby 5+ razy = 26% okupow; Kasgos 11 razy; krol Braavos pojmany 9 razy [P] | czesciowo (skala: 10% obrotu rodow) | Przerwa ochronna po wykupie albo nizsza cena przy kolejnym pojmaniu |
| 19 | Pokoj na zero dni wypuszcza jencow za darmo | 440 z 528 uwolnien "przez pokoj" [P] | prawdziwa | Nie zwalniac jencow, gdy ta sama para jest znow w wojnie przed koncem doby |
| 20 | Wezwania sojusznikow, na ktore nie stac; wezwanie bez wojny | 8 z 18 zerwanych, 4 wojny jednodniowe, skreslono 5.40 mln; Skagos wzielo 117 565 zl bez wojny [P] | prawdziwa | Nie podpisywac, gdy rata przekracza wplyw dnia; sprawdzic, czy wezwany wchodzi do wojny przed pierwsza rata |
| 21 | Zwyciezca nic nie dostaje przy pokoju fabularnym | reparacje nienaliczone: 2.1-2.8 mln przy jednym pokoju na wojne (nie 12.3 mln) [P] | czesciowo | Przy prawdziwym pokoju przelew skarbiec przegranego -> skarbiec wygranego |
| 22 | Konie juczne biora sie z niczego | +56 478 w roku; staly doplyw 116-155 dziennie; oba biegi [P] | prawdziwa | Paczka "juczne z niczego" (tabor wsi bierze juczne ze stada albo z targu) |
| 23 | Zboze i ryby przybywaja bez zrodla, inne jedzenie ubywa | +65 002 i +59 623 szt.; 22 towary -105 228 [P] | prawdziwa | Znalezc zrodlo (trop: jedzenie dawane partiom lordow przez gre) i dopisac do ksiegi |
| 24 | Miasto Thenn (i Frostfang's Camp) bez pieniedzy | 38 dob z kasa ponizej 1 000 zl; Thenn 331 dob ponizej zapasu [P] | prawdziwa co do pomiaru; przyczyna nieznana | Najpierw rozpoznanie |
| 25 | Uchodzcy: 187 910 poza domem | wrocilo 11% w roku [P] | czesciowo: powroty podwajaja sie co kwartal, 0.46% ludnosci wsi | Obserwowac po dobie 433; nie podkrecac powrotu bez pokoju |
| 26 | Wyrzutki wracaja w tempie wojny prawie wszedzie; bandy rosna | 212 z 227 regionow "w wojnie"; pula 11 869; bandy +20 ludzi dziennie [P] | czesciowo: pula stoi od doby 245; progi maja zla baze | Liczyc "wojne regionu" od prawdziwego zagrozenia; prog do zmiany |
| 27 | Rody z lennem bez wojska | 14 z 205 (prog 12); w roku rosnie 1.4 -> 16.0 [P] | prawdziwa | Skutek nr 2 i 3 |
| 28 | Dlug koron w Banku rosnie | 1.85 mln u 20 krolestw; w II polroczu wzrost u 16 [P] | prawdziwa | Do obserwacji po nowym dochodzie koron |
| 29 | Wojna bez walki (Dolina - Zelazne Wyspy) | 342 doby, 0 bitew lordow [P] | czesciowo: koszt niewykazany; przymus konczy sie w dobie 433 | Nic przed biegiem do doby 550 |
| 30 | Notable traca zloto; czesc karawan bez pokrycia zoldu | 30.5 -> 21.8 mln; 177 z 535 karawan [P] | czesciowo: strata netto 13-21 tys. dziennie; zysku karawan log nie liczy | Do obserwacji; dopisac rachunek losu karawan |

### 6.3 Waga niska

| nr | co | dowod | werdykt | propozycja |
|---|---|---|---|---|
| 31 | Zalegly zold w partiach ponad prog | 2.49% partii (22-23 dziennie) przy progu 2%; w zlocie 0.12% [P] | prawdziwa, mala | Bez osobnej poprawki |
| 32 | Sakiewki zolnierzy: 26 dni zoldu | 16.59 mln, stoja od doby 168; 15.2 dnia to oszczednosci, ktorych markietani z zalozenia nie ruszaja [P] | stan, nie przeciek | Zmienic prog (starszy niz regula) |
| 33 | Kraje bez wojny placa najemnikom z zapasu skarbca | 7.9 tys. zl dziennie razem [P] | prawdziwa, drobna | Kontrakt tylko z wplywow dnia, gdy krolestwo nie jest w wojnie |
| 34 | Kopalnie BannerKings w miastach stoja | 13 -> 7, pracuje 0 [P] | czesciowo: swiat sam je zamyka | Nic albo zamknac |
| 35 | Polowa uprzezy znika z ksiegi | -7 395 z 14 995 szt. [P] | czesciowo: raczej brak licznika | Dopisac stan "zapisy notabli" |
| 36 | Drobne w ksiedze pieniadza | +373 084 zl z rozliczen rodow; 628 169 zl bohaterow wracajacych do swiata; 2 rozjazdy probek po 45 zl [P] | czesciowo: 0.06% zlota swiata | Rozbic w linii, ktora pozycja rozliczen dodaje |
| 37 | Koszt swatow i pamiec rosna | 471 -> 697 ms; 1 218 -> 1 517 MB [P] | czesciowo: pamiec coraz wolniej | Etap optymalizacji; bieg dwuletni |
| 38 | Pojedyncze potkniecia ksiag ludzi; lordowie ruszaja noca | Straz 56 ludzi (doba 175), wioski 6 ludzi (doba 229); 2.3 lorda na noc przy progu 2.0 (srednia z 333 nocy) [P] | czesciowo | Obejrzec dwie doby; 2.3 to staly poziom |
| 39 | Dothrakowie nie sa horda konna | jazda 43.9% przy progu 60% [P] | prawdziwa, znana | Konie i uprzaz (razem z nr 14) |
| 40 | Brak pomiaru smierci i narodzin bohaterow | lordowie AI 1 227 -> 1 662 [P] | brak pomiaru | Jedna linia dobowa |
| 41 | Zamki bez wyrobow i luksusu | zadowolenie 0.50 caly rok [P] | czesciowo: 0.50 to wartosc startowa, nie pomiar | Wozy kupcow na targi zamkow - do rozwazenia |
| 42 | Dwa rachunki ludnosci | BannerKings wsie -18.4%, nasza ksiega -1% [P] | czesciowo: dwa modele z zalozenia | Sprawdzic, czy cos od tego zalezy |

### 6.4 Odrzucone przez weryfikatorow (artefakty - nie naprawiac)

| co twierdzil analityk | dlaczego nie |
|---|---|
| "Panowie ubozeja" | Liczone od szczytu po darze startowym; od doby 182 glowy rodow stoja (47.8 -> 47.2 mln) |
| "Wojna drenuje korony 51-85 tys. dziennie" | 83% ubytku to zaplanowane wydawanie zapasu startowego (1/360 dziennie); dzis 37 tys. |
| "Sakiewki zolnierzy puchna; krotszy zold da 36 tys. zl dziennie" | Sakiewki napelnialy sie tylko w pierwszym polroczu; w Q4 -1.2 tys. dziennie. Trwale ok. 0 |
| "Biedny rod ma drozsze wojsko" | Sklad druzyn (weterani), nie cena; bogaty rod z pelna druzyna placi 6.2-6.7 zl, prawie tyle samo |
| "Warsztaty i karawany panow nie daja dochodu" | Definicja kolumny - zysk karawan idzie inna linia |
| "Wsiom nic nie zostaje po podatkach" | To zmiana stanu kiesy w rownowadze; wsie maja 9.55 mln, danina zdejmuje nadwyzke, nie podstawe |
| "Niewydany sprzet (87 tys. dziennie) mozna dac na zold" | Wplacaja go rody, ktore pulapu nie wykorzystuja; do wziecia 13-20 tys. |
| "Konie leza na polkach, a jazda czeka" (jako sprzecznosc gospodarki) | Brama wojska: siodla, cena, kategoria konia |
| "Kierunek reparacji nie zgadza sie z bitwami" | Placi strona zawierajaca pokoj - 72 z 72 |
| "Dothrakowie nie ruszaja Wolnych Miast" | Nie mieli z nimi wojny |
| "Kompanie najemne maja 74% ludzi z umowy" | Dwie rozne miary; ta sama linia daje 82% |
| "Przegrany wypad to strata calej armii" | Zadnego przegranego wypadu nie bylo; cala armie traci oblegajacy (nr 4) |

## 7. Czego nie wiemy

Wazne dla decyzji:
- Co bedzie po dobie 433 (koniec przymusu 10 wojen) i w zimie (ok. doby 560). Bieg skonczyl sie w dobie 364. Potrzebny bieg do ok. doby 550 albo dwuletni.
- Dlaczego rod, ktorego stac, trzyma tylko ok. 60% druzyny (werbunek wobec strat, niewola dowodcow, cel wielkosci druzyny AI). Nikt tego nie zmierzyl. Bez tego nie wiadomo, czy jakikolwiek podatek dojdzie do 85 tys.
- Ile nowego podatku trzeba na 1 zl nowego zoldu przy doplacie celowanej - zalezy od formy. Zadna liczba o podatkach nie byla sprawdzona biegiem.
- Ile z nowego zoldu wroci do ludu i z jakim opoznieniem (zolnierze wydaja w miastach i wsiach 61-64 grosze z kazdej zlotowki).
- Ile lud bedzie odkladal za rok (tempo gasnie) i gdzie wojsko sie zatrzyma bez nowego pieniadza.
- Ile da "stawka wojenna podatku pana" - nie policzona w zadnym rozdziale.
- Rozrzut miedzy biegami: znam jedna pare okien (5.7 tys. ludzi). Odleglosc od progu (5.6 tys.) jest tego samego rzedu.
- Czy fabula Realm of Thrones z graczem konczylaby wojny (w biegu bez gracza 17 zdarzen zaleglo na osi).

Przyczyny nieustalone:
- Thenn i Frostfang's Camp - co oproznia kasy.
- Skad przybywa zboze i ryby; ktora linia rozliczen rodow dodaje 373 tys. zl w roku; co sie stalo z ok. 1 040 karawanami; ile karawany zarabiaja na handlu; czy karawany lordow zarabiaja na swoj zold.
- Dlaczego dobrobyt miast rosnie o jedna trzecia w roku wojny.
- Od czego zalezy cena wezwania sojusznika; ilu ludzi zabila zaraza obozowa (31 oblezen).
- Czy brak buntow to skutek poprawek, czy przypadek.
- Co rosnie w koszcie swatow po dobie 100; co trwalo 1.6 s w dobie 215; czy pamiec stanie w drugim roku.

Brak pomiaru:
- Zgony i narodziny bohaterow.
- Rozklad wojska na szczeble 1-6; ranni w samych druzynach lordow; ilu ochotnikow stoi u notabli.
- Lojalnosc miast; ile miast nie ma narzedzi, odziezy i koni; indeks cen narzedzi, ceramiki, wina i aksamitu.
- Wydatki wojska wedlug miast (hipoteza "biedne sa miasta, gdzie nie chodzi wojsko" nie ma danych).
- Plik zakupy.log nie byl analizowany.

O samym raporcie:
- Stan planu (rozdz. 0a) opieram na wpisach STAN-PRAC od WGRANIA 34 i na PLAN-DO-KONCA-MODA-2026-10-09; nie sprawdzalem kazdej ze 127 pozycji planu osobno.
- Liczby pochodza z rozdzialow analitykow i weryfikatorow. Sam sprawdzilem w danych cztery rzeczy, w ktorych rozdzialy sie roznily: liczbe rodow na pulapie i zold na czlowieka (BUD, skrypt r1_grupy.py), okna wojen fabularnych (CS:285, docs\AUDYT-KALENDARZ.md linia 241), okupy krolow ze skarbcow w dobach 337-364 (LOG:126438, LOG:126793) i date poczatku jesieni (LOG:213, LOG:45849).
