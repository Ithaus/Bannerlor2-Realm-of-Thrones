# ROZPOZNANIE: BIEDNE RODY W ROKU 2 (A3) I PIENIADZE KORON (A15) - 14.10.2026, tylko odczyt

Zakres: inwentarz `docs/CO-ZOSTALO-DO-KONCA-MODA-2026-10-14.md` A3 i A15. Bez zmian w kodzie, bez commitow, gra nieuruchamiana, nic nie wgrywane.
Znaczniki: **[P]** pomiar z logu / tabel albo odczyt kodu, **[S]** szacunek albo wniosek; czego nie da sie ustalic - "nie wiem" + linia diagnozy (rozdz. 7).

**Zrodla.**
- Bieg dwuletni (W49 + lup 5b, kod 43af413, 728 dob): `D:\Backup-Bannerlord\logs\2026-10-14-noc\Armoury-2026-10-13_21-14-13.log`, tabela `...\Logs\2026-10-13_21-14-13\budzet-rodow.csv`, `kiesa-ludu.csv`, sesja CS `session-2026-10-13_21-14-11.log`.
- Z.1 (zapis d720 + 60 dob, fin24): `...\Armoury-2026-10-14_02-30-30.log` (doby 721-781; tabele w folderze gry `Modules\Armoury\Logs\2026-10-14_02-30-30\` - tylko odczyt). W52 120 dob (fin25 = Z.1 + plotno): `...\Armoury-2026-10-14_02-50-28.log`. Biegi roczne do porownania: W43 `...\Armoury-2026-10-13_15-02-59.log` (bez gospodarki od ludzi), GL `...\Armoury-2026-10-13_19-21-12.log` (gospodarka od ludzi).
- Kod: drzewo `zb\pr2` (`w-toku/pieniadze-rok2` od 449d453): `ClanBudget.cs` (Ceiling), `KingdomTreasury.cs` (WageRefund), `KingdomTreasury.NeedAid.cs` (W5), `KingdomTreasury.CrownSurplus.cs` (W5b), `KingdomTreasury.WarAid.cs` (danina P1), `CrownIncome.cs` (165, 1/360, dlugi koron), `CrownCallToWar.cs` (168), `MercContract.cs` (185), `MoneyLedger.cs`; gra `dec32\cs_full` (`DefaultClanFinanceModel.AddIncomeFromCallToWarAgrements`).
- Linie logu: "Doplata wedlug potrzeby (W5)" (czesc "na krolestwo"), "Korona: wplywy dnia (165)" (czesc "na krolestwo"), "Skarbce:", "Tarczowe (W6)", "Stawka wojenna pana (W7)", "Wezwania do wojny (168)", "Kontrakty najemnikow (185)", "Okupy (178)", "Pieniadz swiata (rody)" i "(rody - przyczyny)", "Wyrzutki:"; CS "KRONIKA WOJEN", "Reparacje (T9)".

Doba = dzien gry - 108836 + 1 (jak `sprawdz_logi.py`). Kwartaly po 91 dob: Q1 d2-91, Q2 d92-182, Q3 d183-273, Q4 d274-364 (koniec roku 1), Q5 d365-455, Q6 d456-546 (od d460 zima), Q7 d547-637, Q8 d638-728. Kwoty: tys. zl na dobe (srednia dobowa kwartalu), chyba ze napisano inaczej. "Rok 1 / rok 2" = srednia czterech kwartalow.
"Niepokryte" = potrzeba W5 (koszt doprowadzenia druzyn rodu do 60% limitu ponad jego wlasny pulap) minus to, co dalo garnek daniny, skarbiec ponad zapas wojny (W5b) i tarczowe (W6) - liczba z linii W5. "Niepokryte po W7" = po poborze stawki wojennej pana z wlasnego ludu.

---

## Pytanie z czatu: ile jest band i bandytow na mapie [P]

Licznik linii "Wyrzutki" = wszystkie partie `MobileParty.AllBanditParties` (OutlawLaw.cs:1232; klany z `is_bandit="true"`: looters, sea_raiders, gorskie, lesne, pustynne, stepowe, robber_knights, kingswood_outlaw, wildlings, hilltribesmen, yiti_bandits). Inni (ROTclan_126) NIE sa w tej liczbie (w spclans.xml maja `is_outlaw`, nie `is_bandit`) - osobno.

| bieg / doba | band | bandytow w bandach | sredni rozmiar | wyrzutki w lesie (pula poza bandami) | Inni (osobno) |
|---|---:|---:|---:|---:|---:|
| bieg dwuletni d2 (start) | 371 | 6 085 | 16 | 464 | 4 grupy / 603 |
| bieg dwuletni d121 | 577 | 10 654 | 18 | 1 955 | - |
| bieg dwuletni d364 (koniec roku 1) | 949 | 19 240 | 20 | - | 5 / 5 266 |
| bieg dwuletni d455 | 1 012 | 21 023 | 21 | - | - |
| **bieg dwuletni d729 (koniec roku 2)** | **1 097** | **21 618** | 20 | **4 429** | 5 / 8 925 |
| Z.1, zapis d720 + 60 dob (d781) | 1 110 | 22 129 | 20 | 5 412 | - |
| **W52 = wersja w grze, nowa kampania d121** | **625** | **10 780** | 17 | 1 977 | - |
| W53 (test teraz w toku, d108 - odczyt pliku, gry nie ruszalem) | 617 | 11 144 | 18 | 1 140 | - |

Rosna caly rok 1 (ok. +1.6 bandy i +36 ludzi na dobe), w roku 2 wolniej (+0.4 bandy, +7 ludzi na dobe). Dla skali: na koniec roku 2 wojsko lordow w wojnie 55.3 tys., zalogi ok. 58 tys. Prog etapu 4 (E4/3) z biegu sprzed E4/3: <= 867 band / 15 044 ludzi w d728 - przekroczony; w BD rozdz. 7 nr 28 opisane jako "CECHA / PROG - do decyzji".

---

## 0. Dla Jeffa - osiem zdan

1. "Brak na zold rosnie z 24 do 54 tys. zl dziennie" to porownanie srednich z calych lat; w kwartalach brak rosl w roku 1 (0, 7, 35, 52 tys.) i w roku 2 stoi (55, 58, 52, 51) - to stan, do ktorego swiat doszedl pod koniec roku 1, a nie nowa choroba roku 2.
2. Ponad 4/5 tego braku to dwa kraje: Zelazne Wyspy (ok. 24 tys. dziennie) i Wolni Ludzie (ok. 21 tys.); Pentos i Smocza Skala to 1-3 tys. (Pentos 7 tys. dopiero w ostatnim kwartale, po powrocie do wojny).
3. Przyczyna: korona doplaca do zoldu tylko z daniny SWOJEGO ludu, a lud Wysp ma 0.9%, a Wolnych Ludzi 0.2% oszczednosci swiata - danina daje im 6-7 i 2 tys. dziennie przy potrzebie 30 i 23; w tym samym czasie bogate krolestwa w wojnie maja nadwyzke daniny 166-280 tys. dziennie, ktora idzie w renty ich wlasnych panow - pieniedzy na swiecie jest dosc, tylko nie przechodza przez granice.
4. Do tego u Wysp i Wolnych Ludzi zalogi zjadaja od polowy do 85% pulapu zoldu (zalogi sa placone pierwsze), zwrot 70% zoldu od korony spadl prawie do zera (zold oplacony doplata i stawka pana zwrotu nie dostaje), a ziemia daje im 1-2 tys. dziennie (w biegu bez "ziemi wedlug ludzi" Wyspy mialy z ziemi 5-6 tys.).
5. Zapas skarbcow koron (64 -> 6 tys. dziennie) to zaplanowane wydawanie startowych 2 mln kazdej korony po 1/360 dziennie ponad 0.5 mln - malal rowno od pierwszego dnia (107, 75, 46, 31, 20, 13, 9, 6 tys.); nic sie nie zepsulo, ale w roku 3 tego zrodla juz prawie nie bedzie, a korony zyja tylko z wplywow dnia.
6. Z.1 (wojsko w pokoju) tego nie zmieni - dotyczy tylko pokoju i rodow z kiesa ponad rezerwe; w tescie 60 dob Wyspy i Wolni Ludzie dalej maja po 17-19 tys. braku; zmeczenie wojna /4.33 tez nie, bo oba kraje sa w wojnie bez przerwy - raczej dojdzie ok. 5-11 tys. braku z malych Wolnych Miast, ktore dluzej zostana w wojnie.
7. Pieniadze koron (A15): zalogi 58 tys. to skutek pokojow (w krolestwach w wojnie zalogi stoja na ok. 50 tys.), Z.1 dalo +6 tys. w 60 dob (64 tys., do progu 69 brakuje ok. 5); wezwanie Skagos bez wojny powtorzylo sie (151 tys. zl w dobach 13-70), a Wyspy i Smocza Skala placily sojusznikom po 1.3-1.4 mln w roku 2, gdy ich panowie nie mieli na zold.
8. Reszta A15 jest drobna albo inna, niz opisano: zwyciezca pokoju fabularnego dalej dostaje 0 (32 takie pokoje w roku 2, a ROT kazda z tych wojen wznowil w ciagu 2 dob - 111 z 111 w dwa lata), ale zwykle pokoje roku 2 placa 1.9 mln reparacji; najemnicy krajow w pokoju to do 2 tys. dziennie, z zapasu najwyzej 0.6 tys.; "+373 tys." z ksiegi w roku 2 wynosi +0.65 mln i jest roznica miedzy kwotami naliczonymi a przeniesionymi w rozliczeniach rodow - swiat jest szczelny.

---

## 1. A3 - swiat: brak na zold i jego zrodla, kwartalami [P]

Zrodlo: linia W5 "na krolestwo" (suma krolestw w wojnie), linia 165, linia W7, linie "Skarbce". Skrypty `x1_log.py`, `a1_kwartaly.py`, `a2_niepokryte.py` (rozdz. 8).

| tys. zl/d | Q1 | Q2 | Q3 | Q4 | Q5 | Q6 | Q7 | Q8 | rok 1 | rok 2 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| krolestw w wojnie | 20.8 | 25.6 | 26.7 | 26.2 | 25.9 | 14.0 | 14.7 | 15.1 | | |
| danina P1 krolestw w wojnie | 63.5 | 217.9 | 349.5 | 451.5 | 389.8 | 236.9 | 224.8 | 234.7 | 270.6 | 271.6 |
| - z tego w krolestwach BEZ braku -> po doplacie idzie w renty ("reszta") | 39.4 | 133.8 | 245.3 | 368.9 | 280.4 | 166.3 | 171.3 | 182.1 | 196.9 | 200.0 |
| - z tego w krolestwach Z brakiem | 1.1 | 21.0 | 30.4 | 29.3 | 29.5 | 23.2 | 14.4 | 20.6 | 20.5 | 21.9 |
| potrzeba W5 (swiat) | 33.1 | 97.5 | 110.2 | 108.8 | 122.0 | 103.5 | 81.8 | 85.5 | 87.4 | 98.2 |
| - w krolestwach z brakiem | 3.3 | 33.0 | 61.1 | 72.6 | 71.3 | 70.2 | 63.2 | 66.3 | 42.5 | 67.8 |
| W5 wyplacono (gotowka) | 10.4 | 57.5 | 59.3 | 43.2 | 46.2 | 37.0 | 20.2 | 28.1 | 42.6 | 32.9 |
| W5b ze skarbca ponad zapas wojny | 5.8 | 19.4 | 6.7 | 4.4 | 11.5 | 1.7 | 0.8 | 0.0 | 9.1 | 3.5 |
| W6 tarczowe | 0.0 | 0.3 | 1.3 | 2.8 | 2.2 | 2.6 | 1.5 | 2.1 | 1.1 | 2.1 |
| **niepokryte po W5/W5b/W6** | 0.3 | 7.3 | 35.1 | **51.9** | 54.8 | 57.8 | 51.9 | 51.0 | **23.7** | **53.9** |
| W7 stawka pana | 0.1 | 5.0 | 21.4 | 21.9 | 19.8 | 15.3 | 9.8 | 14.1 | 12.1 | 14.8 |
| niepokryte po W7 | 0.3 | 2.3 | 13.7 | 30.0 | 35.0 | 42.5 | 42.1 | 36.9 | 11.6 | 39.1 |
| 1/360 zapasu koron | 106.6 | 74.8 | 46.1 | 30.6 | 20.2 | 12.6 | 8.6 | 6.5 | 64.5 | 12.0 |
| zwrot 70% zoldu druzyn | 202.8 | 265.1 | 285.5 | 332.2 | 359.3 | 193.3 | 181.3 | 170.0 | 271.4 | 226.0 |
| renty 180 | 271.9 | 315.1 | 454.6 | 521.0 | 471.0 | 404.2 | 381.1 | 359.0 | 390.7 | 403.8 |
| skarbce koron razem, koniec kwartalu (mln) | 52.7 | 41.3 | 31.1 | 25.5 | 21.7 | 18.5 | 16.6 | 15.9 | | |
| skarbcow na podlodze (<= 0.51 mln), koniec kw. | 0 z 29 | 0 z 29 | 3 z 30 | 4 z 29 | 6 z 30 | 11 z 30 | 14 z 32 | 18 z 35 | | |

Liczby roczne zgadzaja sie z BD rozdz. 2.5 (23.7 / 53.9; 64.4 / 12.0; W5 42.7 / 32.9).

**Kto sklada brak (niepokryte po W5/W5b/W6, tys. zl/d) [P]:**

| krolestwo | Q1 | Q2 | Q3 | Q4 | Q5 | Q6 | Q7 | Q8 | rok 1 | rok 2 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Zelazne Wyspy | 0.3 | 4.5 | 14.2 | 21.8 | 23.2 | 29.6 | 24.6 | 18.2 | 10.2 | 23.9 |
| Wolni Ludzie | 0.0 | 2.4 | 11.7 | 17.6 | 20.0 | 21.2 | 21.2 | 20.3 | 7.9 | 20.7 |
| Lys (w wojnie do d451) | 0.0 | 0.0 | 5.5 | 5.4 | 6.1 | 0.0 | 0.0 | 0.0 | 2.7 | 1.5 |
| Pentos | 0.0 | 0.0 | 1.0 | 1.4 | 0.2 | 0.3 | 1.9 | 6.9 | 0.6 | 2.3 |
| Smocza Skala | 0.0 | 0.1 | 0.3 | 0.6 | 3.0 | 2.6 | 0.6 | 1.2 | 0.2 | 1.9 |
| Norvos | 0.0 | 0.0 | 0.1 | 0.3 | 0.5 | 1.8 | 1.1 | 2.3 | 0.1 | 1.4 |
| Tyrosh | 0.0 | 0.0 | 1.9 | 2.1 | 0.6 | 0.0 | 0.0 | 0.3 | 1.0 | 0.2 |
| Lorath | 0.0 | 0.0 | 0.0 | 2.2 | 0.1 | 0.0 | 2.2 | 0.2 | 0.6 | 0.6 |

Wyspy + Wolni Ludzie = 44.6 z 53.9 tys. w roku 2 (83%), po W7 33.9 z 39.1 (87%) [P]. W biegach rocznych ten sam obraz w Q4: W43 36 tys. (Wolni Ludzie 12.2, Wyspy 8.1), GL 54 tys. (Wyspy 26.2, Wolni Ludzie 11.1) [P].

## 2. A3 - cztery biedne kraje, kwartalami [P]

Linie W5 / 165 / Skarbce i `budzet-rodow.csv` (sumy rodow AI krolestwa; skrypt `a8_md.py`). Kolumny "rok 1 / rok 2" - srednia kwartalow.

**Wolni Ludzie (Free Folk)** - tys. zl/d

| | Q1 | Q2 | Q3 | Q4 | Q5 | Q6 | Q7 | Q8 | rok 1 | rok 2 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| doby wojny | 89/89 | 91/91 | 91/91 | 91/91 | 91/91 | 91/91 | 91/91 | 91/91 | | |
| potrzeba W5 (do 60% limitu druzyn) | 5.0 | 14.5 | 17.3 | 20.5 | 22.1 | 23.4 | 23.3 | 21.8 | 14.4 | 22.6 |
| danina P1 wlasnego ludu | 3.7 | 7.9 | 5.2 | 2.9 | 2.1 | 2.2 | 2.0 | 1.5 | 4.9 | 2.0 |
| W5 wyplacono + W5b ze skarbca | 2.9 | 10.8 | 5.5 | 2.9 | 2.1 | 2.2 | 2.0 | 1.5 | 5.5 | 2.0 |
| **niepokryte po W5/W5b/W6** | 0.0 | 2.4 | 11.7 | 17.6 | 20.0 | 21.2 | 21.2 | 20.3 | 7.9 | 20.7 |
| W7 stawka pana (z wlasnego ludu) | 0.0 | 1.4 | 5.8 | 4.6 | 3.3 | 3.2 | 3.0 | 2.5 | 2.9 | 3.0 |
| niepokryte po W7 | 0.0 | 1.1 | 5.9 | 13.0 | 16.6 | 18.0 | 18.2 | 17.8 | 5.0 | 17.7 |
| D staly rodow (dochod, 28 dob) | 13.4 | 8.4 | 8.1 | 5.9 | 4.3 | 5.6 | 5.8 | 6.6 | 9.0 | 5.6 |
| - ziemia | 3.7 | 0.9 | 1.0 | 0.8 | 0.7 | 1.2 | 1.0 | 1.2 | 1.6 | 1.0 |
| - zwrot 70% zoldu od korony | 5.1 | 1.8 | 1.2 | 0.4 | 0.2 | 0.1 | 0.1 | 0.2 | 2.1 | 0.1 |
| - renty 180 od korony | 0.6 | 2.2 | 2.3 | 2.1 | 1.6 | 2.4 | 2.3 | 2.8 | 1.8 | 2.3 |
| pulap zoldu rodow | 28.1 | 22.8 | 17.6 | 10.9 | 7.3 | 8.2 | 7.7 | 6.9 | 19.8 | 7.5 |
| zold zalog | 4.3 | 5.2 | 5.3 | 6.1 | 6.1 | 5.6 | 5.1 | 5.0 | 5.2 | 5.5 |
| zold druzyn | 11.2 | 15.3 | 14.1 | 9.0 | 6.5 | 6.2 | 5.7 | 4.8 | 12.4 | 5.8 |
| zold przyciety | 0.0 | 0.6 | 1.9 | 2.8 | 3.6 | 3.2 | 2.5 | 2.0 | 1.3 | 2.8 |
| ludzie w druzynach / limit (tys.) | 2.6 / 5.8 | 3.0 / 5.8 | 2.5 / 5.5 | 1.5 / 5.6 | 1.0 / 5.6 | 0.9 / 5.4 | 0.8 / 5.2 | 0.7 / 5.2 | | |
| kiesy rodow G (mln) | 1.31 | 0.57 | 0.37 | 0.26 | 0.23 | 0.21 | 0.20 | 0.19 | | |
| rody biedne w wojnie (G < R) z 11 | 1 | 4 | 8 | 9 | 10 | 10 | 10 | 10 | | |
| skarbiec korony, koniec kw. (mln) | 0.84 | 0.54 | 0.50 | 0.50 | 0.50 | 0.50 | 0.50 | 0.50 | | |

**Zelazne Wyspy (Iron Islands)** - tys. zl/d

| | Q1 | Q2 | Q3 | Q4 | Q5 | Q6 | Q7 | Q8 | rok 1 | rok 2 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| doby wojny | 68/89 | 91/91 | 91/91 | 91/91 | 91/91 | 91/91 | 91/91 | 91/91 | | |
| potrzeba W5 | 8.5 | 23.4 | 24.5 | 30.9 | 31.7 | 33.7 | 28.6 | 25.3 | 21.8 | 29.8 |
| danina P1 wlasnego ludu | 2.7 | 16.3 | 10.2 | 9.1 | 8.6 | 5.7 | 5.2 | 7.0 | 9.6 | 6.6 |
| W5 wyplacono + W5b | 4.6 | 17.7 | 10.3 | 9.1 | 8.5 | 4.1 | 4.0 | 7.0 | 10.5 | 5.9 |
| **niepokryte po W5/W5b/W6** | 0.3 | 4.5 | 14.2 | 21.8 | 23.2 | 29.6 | 24.6 | 18.2 | 10.2 | 23.9 |
| W7 stawka pana | 0.1 | 3.4 | 9.7 | 11.4 | 11.1 | 8.4 | 5.2 | 6.1 | 6.2 | 7.7 |
| niepokryte po W7 | 0.3 | 1.1 | 4.5 | 10.4 | 12.1 | 21.1 | 19.3 | 12.2 | 4.0 | 16.2 |
| D staly rodow | 25.5 | 16.1 | 19.3 | 23.8 | 22.6 | 10.1 | 23.2 | 22.4 | 21.2 | 19.6 |
| - ziemia | 7.8 | 1.5 | 1.9 | 2.3 | 1.9 | 1.6 | 2.2 | 2.0 | 3.4 | 1.9 |
| - zwrot 70% zoldu od korony | 5.0 | 2.1 | 2.7 | 1.1 | 1.5 | 0.4 | 0.9 | 1.0 | 2.7 | 0.9 |
| - renty 180 od korony | 4.1 | 4.9 | 4.8 | 8.8 | 6.5 | **0.1** | 1.6 | 5.9 | 5.7 | 3.5 |
| **wezwania placone przez korone Wysp** | 0.0 | 0.0 | 0.0 | 0.0 | 1.8 | **7.7** | 5.8 | 0.0 | 0.0 | 3.8 |
| pulap zoldu rodow | 34.4 | 35.2 | 32.7 | 32.5 | 32.1 | 18.2 | 17.7 | 22.9 | 33.7 | 22.7 |
| zold zalog | 9.3 | 13.0 | 13.5 | 14.7 | 15.6 | 14.3 | 13.9 | 15.6 | 12.6 | 14.9 |
| zold druzyn | 14.4 | 24.3 | 24.8 | 23.5 | 23.4 | 15.1 | 11.3 | 15.2 | 21.8 | 16.2 |
| zold przyciety | 0.0 | 1.3 | 3.2 | 0.9 | 2.1 | 10.0 | 3.7 | 3.5 | 1.3 | 4.8 |
| ludzie w druzynach / limit (tys.) | 3.2 / 7.6 | 5.1 / 8.9 | 4.6 / 8.3 | 3.7 / 8.4 | 3.6 / 8.3 | 2.2 / 8.1 | 1.7 / 7.8 | 2.5 / 7.5 | | |
| kiesy rodow G (mln) | 2.67 | 1.34 | 1.09 | 1.09 | 1.18 | 0.89 | 0.98 | 1.07 | | |
| rody biedne w wojnie (G < R) z 33 | 8 | 16 | 23 | 20 | 17 | 19 | 18 | 17 | | |
| skarbiec korony, koniec kw. (mln) | 0.70 | 0.51 | 0.50 | 0.50 | 0.50 | 0.50 | 0.50 | 0.50 | | |

**Pentos** - tys. zl/d

| | Q1 | Q2 | Q3 | Q4 | Q5 | Q6 | Q7 | Q8 | rok 1 | rok 2 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| doby wojny | 68/89 | 91/91 | 91/91 | 91/91 | 91/91 | 81/91 | 51/91 | 91/91 | | |
| potrzeba W5 | 2.2 | 7.6 | 4.5 | 6.7 | 4.7 | 6.1 | 5.6 | 11.5 | 5.3 | 7.0 |
| danina P1 wlasnego ludu | 2.1 | 4.5 | 5.5 | 4.5 | 7.3 | 6.2 | 2.4 | 5.1 | 4.1 | 5.3 |
| **niepokryte po W5/W5b/W6** | 0.0 | 0.0 | 1.0 | 1.4 | 0.2 | 0.3 | 1.9 | 6.9 | 0.6 | 2.3 |
| niepokryte po W7 | 0.0 | 0.0 | 0.3 | 0.7 | 0.0 | 0.1 | 1.7 | 3.1 | 0.3 | 1.2 |
| D staly rodow | 20.9 | 18.7 | 31.3 | 30.1 | 28.6 | 30.1 | 25.5 | 30.5 | 25.2 | 28.7 |
| wezwania placone przez korone Pentos | 0.1 | 0.1 | 1.8 | 2.8 | 2.7 | 3.8 | 0.0 | 0.0 | 1.2 | 1.6 |
| zold zalog / pulap | 4.6 / 19.4 | 5.7 / 19.2 | 5.1 / 22.8 | 5.2 / 33.4 | 5.5 / 35.2 | 5.3 / 32.1 | 4.0 / 19.9 | 6.1 / 27.6 | | |
| ludzie w druzynach / limit (tys.) | 1.7 / 3.6 | 2.1 / 3.8 | 2.0 / 3.2 | 2.2 / 3.5 | 2.6 / 3.7 | 2.1 / 3.2 | 1.3 / 3.3 | 2.8 / 4.5 | | |
| kiesy rodow G (mln) | 1.22 | 0.77 | 1.09 | 1.65 | 1.76 | 1.74 | 1.58 | 1.27 | | |

**Smocza Skala (Dragonstone)** - tys. zl/d (od d536 wchlonela 14 rodow Krain Burzy: 11 -> 24 rody)

| | Q1 | Q2 | Q3 | Q4 | Q5 | Q6 | Q7 | Q8 | rok 1 | rok 2 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| doby wojny | 89/89 | 91/91 | 91/91 | 91/91 | 91/91 | 81/91 | 51/91 | 91/91 | | |
| potrzeba W5 | 2.1 | 0.8 | 0.7 | 1.3 | 3.3 | 2.8 | 3.9 | 6.9 | 1.2 | 4.2 |
| danina P1 wlasnego ludu | 5.4 | 7.1 | 5.9 | 5.1 | 8.2 | 8.5 | 6.9 | 24.4 | 5.9 | 12.0 |
| garnek W5 (danina, najwyzej reszta wplywow dnia po zwrocie) | 2.8 | 2.2 | 2.3 | 0.5 | 0.2 | 0.7 | 6.6 | 16.9 | | |
| **niepokryte po W5/W5b/W6** | 0.0 | 0.1 | 0.3 | 0.6 | 3.0 | 2.6 | 0.6 | 1.2 | 0.2 | 1.9 |
| wezwania placone przez korone (Pentosowi) | 0.3 | 0.1 | 1.3 | 0.0 | 1.4 | 3.5 | 2.1 | 6.8 | 0.4 | 3.5 |
| D staly rodow | 47.2 | 79.9 | 65.7 | 39.3 | 33.2 | 40.2 | 73.9 | 82.1 | 58.0 | 57.4 |
| ludzie w druzynach / limit (tys.) | 3.8 / 6.9 | 4.3 / 7.2 | 2.7 / 6.3 | 2.3 / 7.1 | 3.5 / 8.0 | 3.1 / 7.7 | 2.9 / 11.1 | 5.0 / 12.1 | | |
| rody biedne w wojnie (G < R) | 1 | 2 | 3 | 1 | 2 | 2 | 6 | 16 | | |

## 3. A3 - przyczyna z dowodem

Kazdy punkt: co, dowod, liczba.

1. **Brak urosl w roku 1, w roku 2 stoi** [P]: niepokryte swiata Q1-Q4 0.3 / 7.3 / 35.1 / 51.9, Q5-Q8 54.8 / 57.8 / 51.9 / 51.0 tys. zl/d. "24 -> 54" to srednie roczne. To samo w obu biegach rocznych: W43 Q3 9.4 -> Q4 35.9, GL Q3 43.3 -> Q4 53.5 [P]. Rosnie, dopoki rody i korony wydaja kiesy startowe (kiesy Wolnych Ludzi 1.31 -> 0.26 mln, Wysp 2.67 -> 1.09 mln w roku 1; skarbce obu na podlodze 0.50 mln od Q3) [P].
2. **Garnek doplaty jest tylko wlasny** [P kod `KingdomTreasury.NeedAid.cs` "GARNEK krolestwa = danina z oszczednosci ludu P1 tej doby ... najwyzej reszta wplywow dnia po zwrocie zoldu"]: danina Wolnych Ludzi 1.5-2.2, Wysp 5.2-8.6 tys. zl/d w roku 2 przy potrzebie 22-23 i 25-34 [P]. Kiesy ludu na koniec roku 2: Wolni Ludzie 0.06 mln (0.2% swiata), Wyspy 0.30 mln (0.9%); dla porownania Dolina 4.07 mln (11.9%) i Dorne 3.07 mln (8.9%) - obie w pokoju, wiec bez daniny (`kiesa-ludu.csv`, ostatnia migawka d722) [P]. Jednoczesnie krolestwa bez braku oddaja w renty swoim panom "reszte" daniny 166-280 tys. zl/d w roku 2 [P, tabela rozdz. 1]. Pieniadz jest, ale nie przechodzi miedzy krolestwami - to regula, nie blad.
3. **W5b nie ma z czego** [P]: skarbiec ponad zapas wojny (podloga 0.5 mln) - nadwyzka Wolnych Ludzi 118-123 zl, Wysp 173-183 zl na koniec kazdego kwartalu roku 2 (linia W5, "nadwyzka"); W5b swiata 19.4 tys. w Q2 -> 0.0 w Q8.
4. **W6 nie ma platnikow** [P]: linia W6 (np. d700), Wolni Ludzie i Wyspy "platnikow 0"; G < R w roku 2 u 10 z 11 rodow Wolnych Ludzi i 17-19 z 33 rodow Wysp, reszta bez wolnego pulapu.
5. **Zalogi pierwsze zjadaja pulap** [P kod `ClanBudget.cs` Ceiling + komentarz "w wojnie pulap gry bez zmian i finansowane pierwsze"]: zold zalog / pulap rodow Wysp 30% (Q1) -> 81% (Q6) -> 69% (Q8), Wolnych Ludzi 17% -> 85% (Q5) -> 72% (Q8); swiat w wojnie 13-17% (`a4_zalogi.py`). Pulap spada, bo spada D i znika skrzynia wojenna (G < R: mnoznik 0.8 + 0.2 G/R, a ponizej 0.25 R x 0.5): pulap Wolnych Ludzi 28.1 -> 6.9, Wysp 34.4 -> 22.9 tys. zl/d [P].
6. **Zwrot 70% zoldu prawie znika** [P kod `KingdomTreasury.WageRefund`: podstawa = zold zaplacony przez pana minus zold oplacony wczorajsza doplata W5 (NeedAidRefundCut) minus pobor W7 (LordLevyRefundCut) minus wydatki ludzi we wlasnych miastach rodu (K3); zalogi bez zwrotu]: Wolni Ludzie 5.1 -> 0.1-0.2, Wyspy 5.0 -> 0.4-1.5 tys. zl/d [P]. Rachunek Wysp Q5: zold druzyn 23.4 - W5 8.5 - W7 11.1 = 3.8 -> 70% = 2.7, wyplacono 1.5 (reszte zjada K3) [S z liczb P]. Wolni Ludzie i Wyspy dostaja wiec pieniadz "znaczony" (W5, W7) zamiast zwrotu - zasada "1 zl = 1 zl zoldu", nie blad.
7. **Ziemia wedlug ludzi** [P dwa biegi, przypisanie S]: ziemia Wysp w Q2-Q4: W43 (bez gospodarki od ludzi) 5.3 / 6.4 / 6.1 tys. zl/d na 21 rodow, GL 2.1 / 1.7 / 1.8 na 29 rodow, bieg dwuletni 1.5 / 1.9 / 2.3 na 33 rody; wplywy dnia korony Wysp W43 24-36, GL 12-22, dwuletni 18-23; D staly rodow Wysp W43 36-44, GL 14-16, dwuletni 16-24 tys. zl/d. Wolni Ludzie: ziemia W43 2.4-2.8, GL 0.8-2.1, dwuletni 0.8-1.0. Niepokryte Q4: W43 Wyspy 8.1 / Wolni Ludzie 12.2, GL 26.2 / 11.1, dwuletni 21.8 / 17.6. Ile z tego to sama regula ziemi, a ile inny sklad rodow i inne paczki (W7 dopiero od GL) - nie wiem (rozdz. 7 pkt 2).
8. **Wezwania sojusznikow zjadaja garnek** [P linia 165, kolejnosc wydatkow `CrownIncome.cs`: ... kontrakty -> wezwania -> zwrot -> doplata W5 -> renty]: korona Wysp placila Dorzeczu (wezwanie d435, 7 581 zl/d, cena 1.38 mln) 1.8 / 7.7 / 5.8 tys. zl/d w Q5-Q7 - w Q6 renty Wysp spadly do 0.1, garnek W5 4.1 przy daninie 5.7, niepokryte doszlo do 29.6 (najwyzej w biegu), zold przyciety 10.0. Smocza Skala d700: wplywy dnia 29 923, danina 15 592, garnek tylko 3 747 (rata Banku 5 959, kontrakty 1 886 i zwrot 18 331 przed doplata); w Q8 jej korona placila tez Pentosowi wezwania srednio 6.8 tys. zl/d (porozumienia d636 i d707, po 7.5-7.6 tys. zl/d) [P].
9. **Pentos i Smocza Skala nie sa biedne stale** [P]: Pentos ma rosnace kiesy (0.77 -> 1.76 mln), brak 0-1.9 tys. az do Q8 (6.9 po powrocie do wojny z wiekszym limitem 4.5 tys.); Smocza Skala ma najwyzszy D z czworki (33-82 tys. zl/d), brak w roku 2 0.6-3.0 tys.; jej "biedni" w Q8 (16 rodow) to przejete rody Krain Burzy. Lys (5-6 tys. w Q3-Q5) zniknal z listy, bo zawarl pokoj d451.
10. **Zapas skarbcow 64 -> 6 tys.** [P kod `CrownIncome.cs` 18-19: "1/CrownReserveReleaseDays (360) zapasu ponad CrownReserveGold (500 000)"]: 1/360 swiata 106.6 / 74.8 / 46.1 / 30.6 / 20.2 / 12.6 / 8.6 / 6.5 tys. zl/d - rowny spadek wykladniczy od d1 (polowa co ok. 250 dob), skarbce 57.8 -> 15.9 mln, na podlodze 0 -> 18 z 35 [P]. To wydawanie startowego zapasu (po 2 mln na korone), zapowiedziane w raporcie rocznym 2.3 ("za rok ok. 13-14 tys.") - pomiar 12.0 [P]. W roku 3 ok. 2-4 tys. zl/d [S: 6.5 x e^(-364/360) = 2.4].

Co NIE jest przyczyna [P]: danina swiata nie spadla (270.6 -> 271.6 tys. zl/d rocznie); okupy krolow nie dotycza czworki w roku 2 (raty dlugow koron placily glownie Dorzecze 5.8 mln i Krolewska Przystan 4.6 mln; Wyspy, Wolni Ludzie, Pentos i Smocza Skala 0) [P].

## 4. A3 - czy Z.1 i zmeczenie wojna /4.33 to zmienia

| | Z.1 (pulap pokoju 0.28 D + 0.4 x skrzynia) | Diplomacy: zmeczenie wojna / 4.33 |
|---|---|---|
| Wolni Ludzie, Zelazne Wyspy | **Nie** [P kod: Ceiling zmienia tylko galaz pokoju i tylko przy G > R]; [P test d721-781]: niepokryte Wysp 17.1 / 18.8, Wolnych Ludzi 18.5 / 16.9 tys. zl/d (d721-750 / d751-781) wobec 18.0 / 19.2 w d701-728 biegu dwuletniego | **Nie** [P: Wolni Ludzie w wojnie 728/728 dob, Wyspy od d23 bez przerwy; wojna Wolni Ludzie - Straz jest fabularna ROT]; zmeczenie ich nie dotyka [S] |
| reszta swiata | brak swiata d751-781 37.3 tys. (d701-728: 53.4) - spadek glownie przez Pentos (8.6 -> 0.4; w czesci okna w pokoju) [P]; W7 9.4 tys. | **Wiecej braku** [S]: 8 z 13 pokojow fali d449-513 wymusilo zmeczenie (`docs/pytania-2026-10-14/RAPORT-pokoj.md`); te pary dluzej w wojnie to m.in. Lys, Tyrosh, Lorath, Pentos, Norvos, ktore w wojnie mialy brak 5.4 / 2.1 / 2.2 / 1.4 / 0.3 tys. (Q4) -> ok. +5..+11 tys. zl/d; danina tych krajow tez poplynie dluzej, ale zostanie u nich |
| zapas 1/360 | bez zmian [P kod] | bez zmian [P kod] |

Wniosek [S]: zadna z dwoch zmian nie rusza przyczyny (garnek tylko z wlasnego ludu + zalogi pierwsze + znikajacy zwrot); brak Wysp i Wolnych Ludzi (ok. 34 tys. zl/d po W7) zostanie na stale.

## 5. A15 - pieniadze koron

### 5.1 Korona nie doplaca do zoldu zalog (zalogi 58 tys. przy progu 69 tys.)

Zalogi rodow AI (`budzet-rodow.csv`, ludzi_zalogi; krolestwo w wojnie / pokoju wedlug linii "Skarbce") [P]:

| | Q1 | Q2 | Q3 | Q4 | Q5 | Q6 | Q7 | Q8 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| zalogi razem (tys. ludzi) | 62.2 | 83.2 | 84.0 | 87.9 | 89.7 | 69.9 | 59.4 | 60.0 |
| - w krolestwach w wojnie | 54.5 | 82.3 | 83.8 | 87.8 | 89.3 | 52.9 | 50.0 | 51.2 |
| - w krolestwach w pokoju | 7.7 | 0.9 | 0.2 | 0.2 | 0.5 | 17.0 | 9.4 | 8.8 |
| zold zalog w wojnie (tys. zl/d) | 133.2 | 190.9 | 196.1 | 209.6 | 215.2 | 130.1 | 126.9 | 129.7 |
| udzial zoldu zalog w pulapie rodow w wojnie (%) | 12.9 | 16.9 | 14.3 | 13.1 | 12.9 | 14.1 | 12.8 | 14.1 |
| zwrot korony (tylko druzyny, tys. zl/d) | 202.8 | 265.1 | 285.5 | 332.2 | 359.3 | 193.3 | 181.3 | 170.0 |

- Przyczyna spadku 89.7 -> 60 tys.: pokoje (krolestw w wojnie 26 -> 14-15); w krolestwach w wojnie zalogi na krolestwo stoja (ok. 3.4 tys.) [P]. W pokoju cel zalogi = 50% zalogi wojennej, najwyzej 80% pulapu 0.28 D [P kod ClanBudget].
- "Korona nie doplaca": zwrot 70% obejmuje tylko druzyny w polu, zalogi nigdy (`KingdomTreasury.WageRefund`: "bez zalog (SoldierPay ich nie zapisuje)"); W5 tez tylko druzyny [P kod]. W swiecie zold zalog to 13-17% pulapu - ciezar tylko u biednych (Wyspy 49-81%, Wolni Ludzie 67-85% w roku 2) [P].
- **Z.1** [P test d720 + 60 dob, CSV]: zalogi 58.7 tys. (d721) -> 60.2 (d730) -> 63.2 (d750) -> 64.3 tys. (d781); w krolestwach w pokoju 6.8 (d720 biegu dwuletniego) -> 9.9 (d730) -> 14.5 tys. (d781), w wojnie ok. 50 tys. bez zmian. Do 69 tys. brakuje ok. 5 tys.
- **/4.33** [S]: kazde krolestwo, ktore zostanie w wojnie zamiast pokoju, trzyma zalogi wojenne (ok. 3.4 tys. na krolestwo w wojnie wobec ok. 1 tys. w pokoju z Z.1) - 2-3 krolestwa wiecej w wojnie wystarcza na prog 69 tys.; ile ich bedzie - nie wiem przed nastepnym biegiem dwuletnim.

### 5.2 Wezwania sojusznikow, na ktore nie stac (Skagos 117 tys.)

Linie "Wezwania do wojny (168)" (porozumienia, zaplaty, zerwania) + CS "KRONIKA WOJEN" (czy wezwany walczy z celem) [P]; `a3_a15.py`.

| | Q1 | Q2 | Q3 | Q4 | Q5 | Q6 | Q7 | Q8 | rok 1 | rok 2 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| nowe porozumienia | 7 | 1 | 7 | 5 | 7 | 2 | 5 | 4 | 20 | 18 |
| zaplacone ze skarbcow wzywajacych (tys. zl w kwartale) | 231 | 388 | 392 | 502 | 700 | 1 701 | 1 295 | 1 328 | 1 512 | 5 024 |
| zerwane z braku wplywow dnia (liczba) | 0 | 0 | 3 | 3 | 0 | 1 | 1 | 0 | 6 | 2 |
| reszta ceny skreslona przy zerwaniu (tys. zl) | 0 | 0 | 2 697 | 3 025 | 0 | 665 | 31 | 0 | 5 721 | 696 |
| reszta skreslona przy koncu porozumienia przez gre (tys. zl) | 331 | 1 704 | 1 757 | 1 504 | 111 | 1 538 | 2 257 | 904 | 5 296 | 4 811 |

- **Wezwany bez wojny z celem** (0 dob wojny wezwany-cel przez 182 doby): 3 z 38 porozumien [P]. Placono tylko w jednym: **Nights Watch -> Skagos przeciw Wolnym Ludziom, d13-70: 2 647 zl/d x 57 dob = 150 879 zl**, Skagos 0 dob jakiejkolwiek wojny przez pierwsze 30 dob, reszta 330 774 skreslona przez gre w d70 - ten sam przypadek co w raporcie rocznym (tam 117 565 zl) [P]. Dwa pozostale (Tyrosh -> Krainy Burzy d323, Norvos -> Lorath d493) zerwane tego samego dnia, zaplacono 0 [P]. Drugie wezwanie Skagos (d657) - Skagos walczy (73/182 doby) [P].
- **Biedni placa sojusznikom** [P linia 165]: w roku 2 korona Wysp zaplacila 1.39 mln (Dorzeczu), Smocza Skala 1.26 mln (Pentosowi), Pentos 0.59 mln (Smoczej Skale) - w tym samym czasie panowie Wysp mieli 16-21 tys. zl/d braku po W7 (rozdz. 2-3 pkt 8). Regula 168 placi wezwanie z wplywow dnia PRZED zwrotem, doplata W5 i rentami [P kod].
- Zerwania roku 1 (6, skreslono 5.7 mln): porozumienia za 0.9-1.1 mln, zerwane po 0-15 dobach, bo reszta wplywow dnia wzywajacego (Pentos, Dorzecze, Tyrosh, Smocza Skala) nie starczyla na rate - regula 168 [P log + kod]; w roku 2 juz tylko 2.
- **Z.1**: bez wplywu [S]. **/4.33** [S]: mniej porozumien konczonych przez pokoj (skreslenia "koniec przez gre" 4.8-5.3 mln rocznie), wiec wiecej zaplat; placacy biedni (Wyspy, Pentos, Smocza Skala) zaplaca wiecej kosztem wlasnych panow.

### 5.3 Zwyciezca nic nie dostaje przy pokoju fabularnym

CS "Reparacje (T9)" + linie "Korona: reparacje (165) - A jest winne B" + "Okupy (178)" [P]; `a3_a15.py`, `a6_dlugi.py`, `a7_okupkr.py`.

| | Q1 | Q2 | Q3 | Q4 | Q5 | Q6 | Q7 | Q8 | rok 1 | rok 2 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| pokoje fabularne ROT (reparacje 0) | 10 | 26 | 23 | 20 | 19 | 3 | 5 | 5 | 79 | 32 |
| - nienaliczone reparacje (tys. zl; ta sama wojna wiele razy) | 1 502 | 3 841 | 3 411 | 3 218 | 3 421 | 631 | 1 487 | 1 321 | 11 973 | 6 861 |
| pokoje zwykle z reparacja (nowy dlug korona -> korona) | 1 | 2 | 0 | 0 | 5 | 6 | 2 | 2 | 3 | 15 |
| - reparacje naliczone (tys. zl) | 176 | 434 | 0 | 0 | 827 | 859 | 127 | 89 | 610 | 1 902 |
| okupy krolow placone przez skarbiec: nowy dlug korony (tys. zl) | 32 | 1 069 | 2 694 | 1 252 | 5 297 | 1 965 | 7 006 | 1 298 | 5 047 | 15 566 |
| "raty reparacji" w linii 165 (tys. zl) | 208 | 673 | 3 175 | 1 601 | 5 031 | 3 395 | 5 334 | 2 541 | 5 657 | 16 301 |

- Zwyciezca pokoju fabularnego dostaje 0 - potwierdzone: w dniach pokojow fabularnych nie powstaje dlug (np. d33 Smocza Skala - Krainy Burzy, "0 zamiast 107 500") [P]. Latka T9 to decyzja Jeffa 08.10 (raport roczny 4.3) [P wedlug raportu].
- W roku 2 problem maleje sam: pokojow fabularnych 79 -> 32, a kazda z tych wojen ROT wznowil w ciagu 2 dob (CS T9 na koniec biegu: "fabularne 111 ... wznowione 111, NIE wznowione 0"); zwykle pokoje (po koncu wymuszania d433) placa 1.9 mln reparacji [P]. Reparacja za pokoj, ktory ROT konczy po 1-2 dobach, bylaby przelewem za nic [S] - rada: zostawic (fabuly ROT nie ruszamy).
- **Znalezisko [P kod `CrownIncome.AddCrownDebt` (178) dopisuje dlug okupu krola do tej samej listy `_debts`]:** "raty reparacji" i "dlugi reparacji" w linii 165 to w ~90% raty okupow krolow, nie reparacje (rok 2: 16.3 mln rat przy 1.9 mln reparacji i 15.6 mln nowego dlugu z okupow); licznik "nowe dzis" liczy tylko reparacje. Do poprawy w napisie linii (drobne, A12/A13) [S].
- **/4.33** [S]: mniej pokojow zwyklych -> mniej reparacji w roku 2; fabularne bez zmian (ROT).

### 5.4 Kraje w pokoju placa najemnikom z zapasu skarbca

Linia 165 "na krolestwo" (kontr.) + flaga wojny z "Skarbce" [P]:

| tys. zl/d | Q1 | Q2 | Q3 | Q4 | Q5 | Q6 | Q7 | Q8 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| kontrakty - krolestwa w wojnie | 10.5 | 13.9 | 12.1 | 14.2 | 15.2 | 13.4 | 12.5 | 9.9 |
| kontrakty - krolestwa w pokoju | 0.8 | 0.4 | 0.1 | 0.1 | 0.3 | 1.7 | 1.8 | 1.0 |
| - krolestw w pokoju z kontraktem (srednio) | 0.8 | 0.4 | 0.1 | 0.2 | 0.3 | 1.8 | 2.2 | 0.9 |
| - najwyzej z zapasu (min z kontraktu i 1/360 tego krolestwa) | 0.8 | 0.3 | 0.0 | 0.1 | 0.2 | 0.6 | 0.5 | 0.1 |
| wplywy dnia krolestw w pokoju | 80.5 | 5.2 | 1.5 | 1.5 | 2.6 | 130.9 | 79.6 | 71.9 |
| niedoplata kontraktow (185, wszystkie) | 0.0 | 0.0 | 0.0 | 0.0 | 0.4 | 1.2 | 0.7 | 0.4 |

- W tym biegu sprawa jest drobna: 1.0-1.8 tys. zl/d w roku 2, z zapasu najwyzej 0.1-0.6 [P]. 7.9 tys. z raportu rocznego to starszy kod (przed P3/W3 "kontrakt za ludzi pod bronia" i polowa kontraktu w pokoju - `MercContract.cs`) [P kod / S co do przyczyny roznicy]. Zapas i tak gasnie (rozdz. 3 pkt 10), wiec w roku 3 kontrakt w pokoju bedzie juz tylko z wplywow dnia [S].
- **Z.1**: bez wplywu. **/4.33** [S]: mniej krolestw w pokoju -> jeszcze mniej.

### 5.5 +373 tys. z rozliczen rodow w ksiedze pieniadza

Linie "Pieniadz swiata (rody)" i "(rody - przyczyny)", sumy kwartalow (tys. zl) [P]:

| tys. zl w kwartale | Q1 | Q2 | Q3 | Q4 | Q5 | Q6 | Q7 | Q8 | rok 1 | rok 2 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| inne linie rozliczen (zmiana + zold naliczony) | 280 | 612 | 982 | 889 | 1 172 | 2 212 | 1 356 | 1 808 | 2 763 | 6 547 |
| - wezwanie do wojny przyjete (model gry) | 177 | 443 | 388 | 505 | 113 | 1 902 | 1 678 | 1 323 | 1 512 | 5 016 |
| - oplata za werbunek zalog do sprzedajacych | 480 | 456 | 427 | 391 | 338 | 192 | 282 | 254 | 1 753 | 1 066 |
| - trybut przyjety + zaplacony | 0 | 2 | -1 | 30 | 32 | 65 | 13 | 347 | 31 | 456 |
| zold naliczony - oddany przez SoldierPay (z pustych kies schodzi mniej) | 395 | 569 | 954 | 843 | 1 045 | 2 112 | 1 320 | 1 421 | 2 760 | 5 898 |
| **netto (= liczba "+373 tys." z raportu rocznego)** | -115 | 44 | 29 | 46 | 126 | 100 | 36 | 388 | **+3** | **+650** |

- To nie jest zloto z niczego: ksiega swiata w tym biegu ma 0 zrodel bez platnika we wszystkich 727 dobach i reszte [R] roku 2 -0.55 tys. zl/d (BD rozdz. 3) [P wedlug BD]. Liczba "netto" laczy dwie niezalezne rzeczy: (a) pozycje modelu gry wpisane w kwotach NALICZONYCH (wezwanie przyjete idzie z portfela wezwan - gra zdejmuje go w tym samym rozliczeniu, `DefaultClanFinanceModel.AddIncomeFromCallToWarAgrements` "CallToWarWallet -= num2" [P kod gry], a portfel jest posiadaczem w `MoneyLedger.cs:845` [P kod]); (b) zold naliczony, ktorego pusta kiesa nie oddala [P]. Wezwania przyjete w 2 latach 6 528 tys. wobec 6 537 tys. zaplaconych do portfeli przez 168 [P] - przeplyw sie zamyka.
- Wielkosc: 0.65 mln w roku 2 = 1.8 tys. zl/d przy 207 mln zlota swiata (0.0009% na dobe) [P]. Rok 2 wiekszy, bo wiecej wezwan (5.0 mln) i trybutu [P]. Ktora czesc daje dokladnie netto - nie wiem (rozdz. 7 pkt 3); rada [S]: nie ruszac logiki, tylko rozbic linie na "zmiane zlota swiata na pozycje" (pomiar przed/po), jak radzil raport roczny.
- **Z.1 / /4.33**: bez wplywu na zasade; /4.33 -> wiecej wezwan -> wieksza liczba netto [S].

## 6. Co z tego wynika dla projektu (kierunki do kroku A1 + A3 + A5 + A15; nic nie zrobione) [S]

Decyzje liczbowe naleza do projektu (zasada Jeffa: projektuje Claude); tu tylko rachunek skali na pomiarach z rozdz. 1-5.
1. **Brak Wysp i Wolnych Ludzi po W7: ok. 34 tys. zl/d w roku 2** (17.7 + 16.2) [P]. Wewnatrz tych krajow nie ma z czego go pokryc: caly lud obu krajow zarabia 4.6 + 14.5 tys. zl/d (`kiesa-ludu.csv`, d722) [P].
2. Zrodla, ktore istnieja: "reszta" daniny krolestw bez braku 166-280 tys. zl/d (idzie w renty wedlug lenn, nie wedlug potrzeby) [P]; lokata ludu krolestw w pokoju 16.6 mln (A5, `ROZPOZNANIE-LUD.md`) [P wedlug tamtego raportu]. Przelew miedzy krolestwami (np. sojusznik albo "danina swiata") to nowa regula - Wyspy walcza z Polnoca i Dolina, wiec "wspolny garnek" placilby wrogowi; musialby byc ograniczony do sojusznikow albo do krolestw bez wspolnej wojny.
3. Wewnatrz istniejacych regul: (a) zalogi pierwsze - przy G < R w wojnie cel zalogi jak w pokoju (50% wojennej) przesunalby u Wysp i Wolnych Ludzi ok. 10 tys. zl/d z zalog na druzyny (Wyspy 7.8, Wolni Ludzie 2.5), kosztem ok. 4 tys. ludzi w zalogach (konflikt z progiem zalog 69 tys.); (b) korona z niepokryta potrzeba wlasnych rodow nie podpisuje i nie placi platnych wezwan (Wyspy 3.8 tys. zl/d w roku 2, Smocza Skala 3.5, Pentos 1.6); (c) albo uznac, ze najubozsze kraje lore maja male wojsko (Wolni Ludzie 0.7 tys. w druzynach, Wyspy 2.5 tys.) i zmierzyc prog bez nich.
4. Zapas skarbcow (1/360): po roku 2 korony zyja tylko z wplywow dnia - kazdy rachunek roku 3 liczyc bez tego zrodla (12 tys. zl/d w roku 2, 2-4 tys. w roku 3).
5. Pomiar konieczny przed decyzja: bieg dwuletni z Z.1 + /4.33 (ile krolestw zostaje w wojnie, ile zalog i ile braku z malych Wolnych Miast).

## 7. Czego nie wiem (+ linia diagnozy)

1. Skutek zmeczenia /4.33 na liczbe krolestw w wojnie w roku 2, zalogi i brak - nie ma biegu po tej zmianie (W53 w toku, 120 dob nowej kampanii - nie dochodzi do d449). Diagnoza: bieg dwuletni; w linii W5 juz jest "na krolestwo" - wystarczy.
2. Ile ze spadku ziemi i D Wysp (W43 -> GL) daje sama regula "ziemia wedlug ludzi", a ile inny sklad rodow (21 / 29 / 33 rody) i paczki dodane miedzy biegami (W7). Diagnoza: w linii "D staly (169c)" rozbicie ziemi na krolestwo (podatek wsi / renta / zawor miast / zawor zamkow) - dzis jest tylko suma swiata.
3. Ktora pozycja rozliczen rodow daje dokladnie "netto" +0.65 mln w roku 2 (rozdz. 5.5). Diagnoza: w `MoneyLedger.ClanTickPrefix/Postfix` zapisac zmiane WorldTotal osobno dla czesci zoldu i reszty (np. migawka kiesy glowy przed/po naliczeniu zoldu) - dzis linia "przyczyny" podaje kwoty naliczone, nie przeniesione.
4. Dlaczego zwrot 70% Wysp w W43 wynosil 10-13 tys. zl/d, a w GL 1-3 (przy W5 obecnym w obu) - rachunek z rozdz. 3 pkt 6 wyjasnia bieg dwuletni (W7 + K3), ale dla GL nie liczylem. Diagnoza: w linii "Skarbce" przy zwrocie podac podstawe i trzy ciecia (W5, W7, K3) na krolestwo.
5. Czy po 60 dobach Z.1 zalogi rosna dalej (64.3 tys. w d781 i trend +0.1 tys./d) - test trwal 60 dob.
6. Czy Skagos-podobne wezwanie (wezwany nie wchodzi do wojny) powtorzy sie w nowej kampanii - w obu biegach to samo porozumienie d13, wiec raczej tak [S]; kod 168 nie sprawdza, czy wezwany walczy z celem, przed kolejna rata [P kod].

## 8. Pliki i polecenia

Skrypty i wyniki: `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\c36b73ca-1de0-4b1b-80ce-71bd02afe545\scratchpad\rody2\` (dysk C - tymczasowy):
- `x1_log.py <log> <wy.pkl>` - wyciag dobowy (W5 i 165 na krolestwo, Skarbce, 168, 185, W6, W7, rozliczenia rodow); wyniki `dw.pkl`, `z1.pkl`, `w52.pkl`, `w43.pkl`, `gl.pkl`.
- `x2_csv.py <budzet-rodow.csv> <wy.pkl>` - sumy rodow na krolestwo i dobe; `dwc.pkl`, `z1c.pkl`, `w52c.pkl`, `w43c.pkl`, `glc.pkl`.
- `a1_kwartaly.py`, `a2_niepokryte.py` (`a2_dw.out.txt`), `a3_a15.py` (`a3_dw.out.txt`), `a4_zalogi.py`, `a5_rep.py`, `a6_dlugi.py`, `a7_okupkr.py`, `a8_md.py` (tabele rozdz. 2).

```
L=D:\Backup-Bannerlord\logs\2026-10-14-noc
python -I x1_log.py L\Armoury-2026-10-13_21-14-13.log dw.pkl
python -I x2_csv.py L\Logs\2026-10-13_21-14-13\budzet-rodow.csv dwc.pkl
python -I a2_niepokryte.py dw.pkl          (z1.pkl "721-750,751-781"; w43.pkl / gl.pkl "274-364")
python -I a3_a15.py dw.pkl L\Armoury-2026-10-13_21-14-13.log L\session-2026-10-13_21-14-11.log
python -I a4_zalogi.py dw.pkl dwc.pkl "2-91,92-182,183-273,274-364,365-455,456-546,547-637,638-728"
python -I a8_md.py dw.pkl dwc.pkl "Free Folk,Iron Islands,Pentos,Dragonstone"
```
