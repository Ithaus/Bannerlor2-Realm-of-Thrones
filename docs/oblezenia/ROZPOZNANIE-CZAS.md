# ROZPOZNANIE - OBLEZENIA: CZAS I PRZEBIEG (14.10)

Zadanie: slowa Jeffa 14.10 po biegu dwuletnim - "sily nie sa rowne, oblezenia sa za szybkie i za latwe". Ta czesc: CZAS i PRZEBIEG oblezen
(start, machiny, decyzja o szturmie, szturm, wynik, glod, historia). Rozpoznanie - bez zmian kodu, bez decyzji.

Oznaczenia: **[P]** pomiar (plik / linia albo kolumna), **[K]** kod (plik:linia), **[U]** ustawienie Jeffa (plik), **[S]** szacunek z wzoru,
**[H]** historia, **[L]** lore ASOIAF - obie z wiedzy ogolnej, NIE sprawdzane w zrodlach w tej sesji.
Kalendarz: rok Armoury = 364 doby [U `Armoury.json`: `LongYearEnabled` true, `WeeksPerSeason` 13]; gra jest strojona pod rok 84 doby
(CLAUDE.md 7) - co w dobach trwa tyle samo, w ulamku roku trwa u nas 4.33 raza krocej niz w zamysle gry.

## 0. Prostym jezykiem

1. W biegu dwuletnim (fin30) bylo 206 oblezen; polowa trwala do 9 dob, najdluzsze 17 dob. Zdobyta twierdza padala po 12 dobach (mediana) - to 1/30 roku. W historii i w ksiazkach oblezenia trwaly miesiace (Storm's End prawie rok).
2. Mury przestaja sie liczyc po niecalym tygodniu: od 4. doby wzor gry co godzine "zjada" przewage murow; po 5.7 dobie wzor pozwala AI szturmowac nawet przy rownych silach, po ok. 6.5 przy dwa razy slabszych, po 7-8 przy cztery razy slabszych.
3. W praktyce AI nie zaczyna oblezenia bez okolo trzykrotnej przewagi (wzor wyboru celu gry), wiec w chwili decyzji o szturmie oblegajacy byli zawsze co najmniej 2.8 raza silniejsi (polowa - ponad 5.7 raza).
4. Dlatego 0 odpartych szturmow w 140 (fin30 + fin21): mury w symulacji daja obroncom tylko okolo dwa razy wiecej ciosow, a obroncy i tak sa w mniejszosci zwykle 3-12 do 1. 18 szturmow (fin30) skonczylo sie "bez rozstrzygniecia" i oblezenie upadlo - zadnego gra nie zapisala jako obrony.
5. Glod nie gra zadnej roli: twierdza ma jedzenia na 75-750 dob (polowa miast 274, zamkow 225), oblezenie konczy sie po 2 tygodniach - 0 dob glodu w 206 oblezeniach.
6. Twierdza ocala sie tylko wtedy, gdy przyjdzie wieksza odsiecz (42 wypady i 14 bitew pod murami przegranych przez oblegajacych; odsiecz zwykle 2 razy liczniejsza) albo gdy zaplaci okup (26 razy, po ok. 9 dobach).
7. Nasze spowolnienie budowy machin (razem z BK x0.25) przesuwa tylko decyzje o szturmie na ok. 8. dobe; sam szturm to bitwa, ktora w symulacji trwa 1-9 dob (mediana 4), i miasto pada ok. 12. doby.
8. Dzwignie sa w rozdz. 9 (czlon czasu w decyzji o szturmie, prog szturmu wobec murow, sila murow w symulacji, glod jako droga zdobycia, wytrzymalosc armii w dlugim oblezeniu) - nic nie zmienione, do decyzji.

## 1. Zrodla i metoda

| Co | Plik |
|---|---|
| fin30 (dwuletni, najnowszy kod) - log Armoury | `D:\Backup-Bannerlord\logs\2026-10-14-noc\Armoury-2026-10-14_06-01-15.log` (287 MB) |
| fin30 - tabele | `D:\Backup-Bannerlord\logs\2026-10-14-noc\tabele-06-01-15\` (`spichlerze-regiony.csv`, `ludzie-regiony.csv`, `bitwy.log`) |
| fin30 - sesja CrashScribe | `D:\Backup-Bannerlord\logs\2026-10-14-noc\session-2026-10-14_06-01-14.log` |
| fin21 (dwuletni, wczesniejszy kod, bez okupu O1) | `...\Armoury-2026-10-13_21-14-13.log`, tabele `...\2026-10-14-noc\Logs\2026-10-13_21-14-13\` |
| skrypty i wyniki posrednie | `docs\oblezenia\robocze\czas\` - `oblezenia_czas.py`, `tabele_md.py`, `fin30.json`, `fin30-epizody.csv` (206 wierszy), `fin21-epizody.csv` (235), `*-oblezenia.log` (wyciag linii) |

**Epizod oblezenia** = ciag dob, w ktorych `spichlerze-regiony.csv` ma `oblezona=1` [P]. Kolumna to `Settlement.IsUnderSiege`, `zywnosc` = `Town.FoodStocks`,
`bilans_zywnosci` = `Town.FoodChange`, `glodna_warownia` = `IsStarving` [K `Armoury/src/Famine.cs:1048-1077`]. Wiersz pisany jest w ticku dobowym, czyli o polnocy:
wiersz z dniem D to stan na poczatku doby D+1 (sprawdzone: pierwsza linia dzienna logu ma `dzien 108837`, pierwszy wiersz CSV `108836` [P]). Dlugosc epizodu = liczba polnocy
w oblezeniu (+-1 doba). Kontrola: suma dob oblezen z CSV (miasta 410, zamki 1358) = suma linii "Oblezone (E5/A4): ... oblezone teraz" (409 + 1358) [P].

Do epizodow przypiete [P]: szturmy, wypady, bitwy pod murami (`bitwy.log`, linie `B175 dzien N | Siege / SallyOut / SiegeOutside`), zdobycia (`Lup miast (L0) ... bitwa szturm|wypad
| zdobycie tak`, 57 linii = 57 zdobyc Jeffa), decyzje gry o szturmie (`Okup oblezenia (O0)`, 113 linii), okupy (`Okup oblezenia (O1)`, 26), choroba obozu (`CampFever`).
Okno dopasowania: od doby startu do 2 dob po ostatniej polnocy oblezenia (bitwa konczy sie zwykle tuz po polnocy, przed tickiem dobowym - np. Cape Wrath: ostatni wiersz
`oblezona=1` 108856, B175 `dzien 108858` [P]).

Uwaga o liczbach B175 dla szturmu: "ludzi" = zdrowi teraz + ranni, polegli i rozbici w bitwie - tylko druzyn, ktore sa w bitwie przy jej koncu.
Przy szturmach wielodniowych bywa niepelne [K `KingdomBalance.cs:119-139`]: Krolewska Przystan B175 `O ... ludzi 92` [P `bitwy.log:36854`], a w CSV zaloga 541 + milicja 340 w dobie decyzji [P `ludzie-regiony.csv`
town_EW4, 109358]. Dlatego sile w chwili decyzji biore z linii O0 (stan tuz przed szturmem), a B175 tylko pomocniczo.

## 2. Przebieg oblezen - wyniki i czas

#### fin30 - wynik oblezenia x rodzaj (epizody = ciagi dob z `oblezona=1` w spichlerze-regiony.csv) [P]

| wynik | miasta: n / mediana / p90 / max dob | zamki: n / mediana / p90 / max dob | razem n | udzial |
|---|---|---|---|---|
| zdobyte szturmem | 13 / 14.0 / 15.8 / 17 | 42 / 12.0 / 14.0 / 15 | 55 | 27% |
| zdobyte po wypadzie | 0 | 2 / 6.0 / 6.8 / 7 | 2 | 1% |
| okup (O1) | 8 / 8.5 / 10.3 / 11 | 18 / 9.0 / 10.0 / 11 | 26 | 13% |
| wypad - pobity oblegajacy | 1 / 6.0 / 6.0 / 6 | 41 / 6.0 / 9.0 / 11 | 42 | 20% |
| pod murami - pobity oblegajacy | 3 / 15.0 / 16.6 / 17 | 11 / 12.0 / 15.0 / 16 | 14 | 7% |
| szturm bez zdobycia, potem odstapione | 7 / 11.0 / 13.8 / 15 | 11 / 10.0 / 15.0 / 15 | 18 | 9% |
| odstapione bez bitwy | 8 / 5.0 / 6.6 / 8 | 39 / 4.0 / 8.0 / 17 | 47 | 23% |
| trwa na koncu biegu | 0 | 2 / 6.0 / 6.0 / 6 | 2 | 1% |
| **wszystkie** | 40 / 10.0 / 15.0 / 17 | 166 / 8.0 / 13.0 / 17 | 206 | 100% |

Histogram dlugosci (doby: liczba oblezen) [P]: 1: 3, 2: 15, 3: 9, 4: 7, 5: 15, 6: 16, 7: 18, 8: 15, 9: 18, 10: 21, 11: 16, 12: 20, 13: 8, 14: 12, 15: 8, 16: 2, 17: 3.

Doby oblezen razem [P]: 1768 (miasta 410, zamki 1358) w 728 dobach biegu = srednio 2.43 twierdzy naraz.

#### fin21 - wynik oblezenia x rodzaj (epizody = ciagi dob z `oblezona=1` w spichlerze-regiony.csv) [P]

| wynik | miasta: n / mediana / p90 / max dob | zamki: n / mediana / p90 / max dob | razem n | udzial |
|---|---|---|---|---|
| zdobyte szturmem | 12 / 12.0 / 13.9 / 15 | 26 / 13.0 / 15.5 / 23 | 38 | 16% |
| zdobyte po wypadzie | 0 | 1 / 7.0 / 7.0 / 7 | 1 | 0% |
| wypad - pobity oblegajacy | 4 / 5.5 / 6.0 / 6 | 73 / 5.0 / 8.0 / 12 | 77 | 33% |
| pod murami - pobity oblegajacy | 6 / 12.0 / 14.5 / 16 | 10 / 12.0 / 15.0 / 15 | 16 | 7% |
| szturm bez zdobycia, potem odstapione | 9 / 11.0 / 14.0 / 14 | 20 / 12.0 / 16.1 / 24 | 29 | 12% |
| odstapione bez bitwy | 20 / 5.5 / 8.0 / 9 | 52 / 4.5 / 8.0 / 11 | 72 | 31% |
| zmiana pana bez szturmu | 1 / 1.0 / 1.0 / 1 | 0 | 1 | 0% |
| trwa na koncu biegu | 1 / 10.0 / 10.0 / 10 | 0 | 1 | 0% |
| **wszystkie** | 53 / 8.0 / 13.0 / 16 | 182 / 6.0 / 13.9 / 24 | 235 | 100% |

Histogram dlugosci (doby: liczba oblezen) [P]: 1: 9, 2: 20, 3: 11, 4: 27, 5: 30, 6: 16, 7: 19, 8: 17, 9: 11, 10: 4, 11: 16, 12: 18, 13: 13, 14: 12, 15: 5, 16: 3, 17: 1, 21: 1, 23: 1, 24: 1.

Doby oblezen razem [P]: 1781 (miasta 451, zamki 1330) w 728 dobach biegu = srednio 2.45 twierdzy naraz.

**Porownanie biegow [P]:** fin30 - 206 oblezen, 57 zdobyc (28%), 26 okupow; fin21 - 235 oblezen, 39 zdobyc (17%), 0 okupow (kod okupu O1 jeszcze nie istnial),
wiecej oblezen przerwanych wypadem (77 vs 42) i odstapionych (72 vs 47). Czas oblezen w obu biegach prawie ten sam: zdobycie mediana 12-13 dob, najdluzsze
oblezenie 17 (fin30) / 24 doby (fin21). Szturmow: fin30 73 (55 zdobyc, 18 bez rozstrzygniecia, **0 wygranych przez obronce**), fin21 67 (38 / 29 / **0**) [P `bitwy.log`, B175 "Siege"].

### 2.1 Typowe zdobycie krok po kroku (fin30)

| Etap | Ile trwa | Dowod |
|---|---|---|
| obozowanie, przygotowania i 2-3 machiny | ok. 4-6 dob przy 2352 ludziach w obozie (mediana) | [S] z kodu, rozdz. 4.1; brak linii w logu |
| pierwsza decyzja gry o szturmie (O0) | wiek oblezenia: mediana 8.4 doby, p10 6.4, min 4.2, max 12.7 (miasta 8.3, zamki 8.5) | [P] 113 linii O0, pole "oblezenie X dob" |
| decyzja -> poczatek szturmu | ta sama doba: 113 decyzji "true" = 113 cykli w calym biegu (po pierwszym "true" gra juz nie pytala - wodz przeszedl na szturm); zaloga zaczyna topniec w dobie decyzji (przyklady nizej). Ze 113 decyzji: 72 bitwy szturmu, 26 okupow (latka O1 wstrzymuje szturm), 14 bitew pod murami z odsiecza, 1 odstapienie | [P] suma "decyzje gry o szturmie (true)" i "cykli" z linii "Okup oblezenia (doba)", epizody; [K] `SiegeRansom.cs:266-283` |
| bitwa szturmu (symulacja co godzine) | 1-9 dob, mediana 4 (zaloga+milicja do 200: 2 doby, 200-400: 3, 400-900: 5) | [P] doba konca B175 - doba O0, 54 zdobycia z O0; [K] rozdz. 6 |
| zdobycie od startu oblezenia | mediana 12 dob (zamki 12, miasta 14), p90 14, max 17 | [P] tabela wyzej |
| w ulamku roku | 12 dob = 0.033 roku (364); w kalendarzu 84 bylaby to 0.143 roku (ok. 7 tygodni "roku gry") | [S] |

Przyklady z CSV (zaloga i milicja topnieja doba po dobie w czasie szturmu) [P `ludzie-regiony.csv`]:
- **Starfall** (miasto, Dorne): decyzja `dzien 108878`, wiek 6.1 doby, szansa na mysl 6.4% [P log:16532]; zaloga/milicja 285/181 -> 266/171 -> 235/148 -> 199/124 -> 161/109, zdobycie `dzien 108883`.
- **Krolewska Przystan** (Robb Stark): decyzja `dzien 109359`, wiek 8.0, sila gry 7700 vs 1556, przewaga 4.459^-1.141, "mury" 1.497 [P log:204696]; zaloga 541 -> 516 -> 469 -> 417 -> 357 -> 310 -> 271,
  milicja 340 -> 234, zdobycie `dzien 109365` (doba biegu 530). Zapas jedzenia w chwili upadku 24 125 przy ubytku 121/dobe = ok. 200 dob [P `spichlerze-regiony.csv` town_EW4, 109364].
- **Cape Wrath** (zamek): decyzja `dzien 108856`, wiek 10.4 [P log:8072]; zaloga 136 -> 102, zdobycie `dzien 108858` [P `bitwy.log:664`].

### 2.2 Czas w kalendarzu

| Miara | Doby | Lata (rok 364) | To samo w roku 84 (zamysl gry) | Zrodlo |
|---|---|---|---|---|
| oblezenie - mediana (fin30) | 9 | 0.025 | 0.107 | [P]/[S] |
| zdobycie - mediana | 12 | 0.033 | 0.143 | [P]/[S] |
| najdluzsze oblezenie fin30 / fin21 | 17 / 24 | 0.047 / 0.066 | 0.20 / 0.29 | [P]/[S] |
| decyzja o szturmie - mediana | 8.4 | 0.023 | 0.100 | [P]/[S] |
| zapas jedzenia miasta / zamku w oblezeniu - mediana | 274 / 225 | 0.75 / 0.62 | 3.3 / 2.7 | [P] rozdz. 7 |
| historyczne oblezenia twierdz (typowo) | ok. 50-330 | 0.15-0.9 | - | [H] rozdz. 8 |

Oblezenia sa rozlozone rowno: 1768 dob oblezen w 728 dobach = srednio 2.43 oblezonej twierdzy naraz na 227 twierdz [P]; ta sama twierdza bywa oblegana kilka razy
pod rzad (Demongate: 7 dob i odstapienie, 2 doby przerwy, 9 dob i okup, potem zdobyta w dobie biegu 132 [P aneks A, #4, #7, #32]).

## 3. Wszystkie zdobycia fin30

Kolumny "A/O moc", "ludzie" i "straty" sa z B175 - zastrzezenie z rozdz. 1 (przy dlugich szturmach obroncow bywa za malo, np. Krolewska Przystan 92 przy 541 + 340 w CSV); sila w chwili decyzji - kolumna O0.

#### fin30 - wszystkie 57 zdobyc (kolejnosc wedlug startu oblezenia) [P]

Kolumny: start = doba biegu (dzien 108836 = doba 1), dob = doby z `oblezona=1` (migawka o polnocy, +-1), O0 = pierwsza decyzja gry o szturmie: wiek oblezenia w dobach (z linii "Okup oblezenia (O0)"), sila gry oblegajacy/obroncy i wykladnik murow w; szturm = doba od startu, w ktorej SKONCZYLA sie bitwa szturmu (B175 "Siege"), A/O = moc oblegajacych / moc zalogi z B175, ludzie A/O, straty A/O; jedzenie = zapas zywnosci / dzienny ubytek w pierwszej dobie oblezenia.

| # | osada | rodzaj | pan przed -> po | start | dob | O0: wiek / sila / w | szturmy (doba: wynik) | A/O moc | ludzie A / O | straty A / O | jedzenie (dob) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | Cape Wrath | zamek | Dragonstone -> Stormlands | 10 | 12 | 10.4 / 8.0 / -2.85 | 13: A zdob. | 5.2 | 1237 / 206 | 234 / 206 | 217 |
| 2 | Fellwood | zamek | Dragonstone -> Stormlands | 29 | 10 | 8.5 / 8.5 / -1.69 | 11: A zdob. | 19.8 | 2089 / 108 | 91 / 108 | 307 |
| 3 | Starfall | miasto | Dorne -> The Reach | 37 | 10 | 6.1 / 8.8 / -0.28 | 11: A zdob. | 7.4 | 3947 / 445 | 973 / 445 | 754 |
| 4 | Gargos | zamek | Qohor -> Volantis | 76 | 12 | 8.3 / 8.8 / -1.60 | 12: A zdob. | 7.1 | 2598 / 363 | 626 / 363 | 195 |
| 5 | Sweetport | zamek | Dragonstone -> Stormlands | 76 | 15 | 10.1 / 10.9 / -2.12 | 15: A zdob. | 8.8 | 4277 / 499 | 899 / 499 | 148 |
| 6 | Sandstone | zamek | Dorne -> The Reach | 87 | 10 | 6.7 / 13.1 / -0.49 | 11: A zdob. | 9.9 | 4443 / 391 | 607 / 391 | 233 |
| 7 | Ar Noy | zamek | Qohor -> Volantis | 93 | 12 | 9.1 / 6.7 / -2.05 | 13: A zdob. | 5.5 | 2113 / 403 | 638 / 403 | 410 |
| 8 | Bloody Gate | zamek | The Vale -> Riverlands | 106 | 10 | 7.6 / 11.2 / -0.93 | 10: A zdob. | 41.2 | 4231 / 96 | 137 / 96 | 365 |
| 9 | Stonedance | zamek | Dragonstone -> Stormlands | 116 | 12 | 8.2 / 6.5 / -1.53 | 12: A zdob. | 5.3 | 2052 / 432 | 483 / 432 | 193 |
| 10 | Harrenhal | miasto | House Baratheon of King's Landing -> The North | 118 | 10 | - | 10: A zdob. | 7.6 | 9689 / 1551 | 2443 / 1551 | 488 |
| 11 | Demongate | zamek | House Targaryen, Aegon -> Volantis | 122 | 10 | 8.5 / 5.9 / -1.69 | 10: A zdob. | 19.2 | 1906 / 94 | 113 / 94 | 338 |
| 12 | Lorath | miasto | Lorath -> Braavos | 130 | 15 | 10.1 / 5.6 / -2.15 | 15: A zdob. | 4.6 | 3229 / 655 | 1032 / 655 | 466 |
| 13 | Blackmont | zamek | Dorne -> The Reach | 137 | 12 | 8.6 / 6.2 / -1.77 | 12: A zdob. | 16.1 | 1820 / 106 | 149 / 106 | 276 |
| 14 | Willow Wood | zamek | Riverlands -> House Baratheon of King's Landing | 167 | 8 | 6.4 / 38.5 / -0.42 | 8: A zdob. | 32.0 | 4469 / 172 | 204 / 172 | 403 |
| 15 | Qamayl | zamek | Ibben -> Sarnor | 180 | 12 | 11.8 / 3.4 / -3.71 | 13: A zdob. | - | 1027 / 0 | 0 / 0 | 289 |
| 16 | Deep Den | zamek | House Baratheon of King's Landing -> Riverlands | 206 | 12 | 7.3 / 9.3 / -0.95 | 12: A zdob. | 20.5 | 2266 / 106 | 133 / 106 | 219 |
| 17 | Vaes Efe | zamek | Dothraki Horde -> House Targaryen | 212 | 9 | 5.9 / 9.2 / -0.14 | 10: A zdob. | 7.0 | 3221 / 657 | 692 / 657 | 193 |
| 18 | Willow Wood | zamek | House Baratheon of King's Landing -> Riverlands | 212 | 7 | 5.6 / 13.4 / 0.07 | 7: A zdob. | 9.5 | 1605 / 172 | 316 / 172 | 314 |
| 19 | Sharp Point | miasto | Dragonstone -> Stormlands | 223 | 9 | 4.2 / 6.1 / 1.08 | 10: A zdob. | 4.8 | 3576 / 732 | 1061 / 732 | 138 |
| 20 | Kingsgrave | zamek | Dorne -> The Reach | 229 | 14 | 9.5 / 11.7 / -1.84 | 15: A zdob. | 8.3 | 4199 / 423 | 723 / 423 | 154 |
| 21 | Totava | zamek | Braavos -> Antaryon League (Braavos Rebels) | 233 | 14 | 9.8 / 4.3 / -2.46 | 14: A zdob. | 3.2 | 1385 / 514 | 692 / 514 | 228 |
| 22 | Skyreach | zamek | Dorne -> The Reach | 262 | 11 | 7.5 / 7.9 / -1.11 | 11: A zdob. | 25.5 | 2724 / 110 | 148 / 110 | 207 |
| 23 | Iksa Calo | zamek | Braavos -> Lorath | 268 | 15 | 9.1 / 3.5 / -2.06 | 15: A zdob. | 2.8 | 1660 / 618 | 1078 / 618 | 562 |
| 24 | Sunstone | zamek | Lys -> Tyrosh | 279 | 12 | 9.4 / 7.1 / -2.27 | 12: A zdob. | 5.8 | 1345 / 261 | 409 / 261 | 240 |
| 25 | Willow Wood | zamek | Riverlands -> House Baratheon of King's Landing | 290 | 9 | 7.2 / 12.0 / -0.95 | 9: A zdob. | 9.5 | 2249 / 243 | 302 / 243 | 298 |
| 26 | Iksa Keria | zamek | Braavos -> Antaryon League (Braavos Rebels) | 297 | 13 | 8.4 / 3.9 / -1.66 | 13: A zdob. | 3.3 | 1861 / 593 | 1008 / 593 | 549 |
| 27 | Bolozo | miasto | Pentos -> Myr | 299 | 14 | 7.4 / 3.8 / -1.02 | 14: A zdob. | 3.0 | 2965 / 943 | 2066 / 943 | 346 |
| 28 | Yronwood | miasto | Dorne -> The Reach | 307 | 17 | 7.7 / 6.5 / -1.00 | 17: A zdob. | 5.7 | 6547 / 1205 | 2091 / 1205 | 359 |
| 29 | Pinkmaiden | zamek | Riverlands -> House Baratheon of King's Landing | 310 | 5 | - | wypad | - | - | - | 292 |
| 30 | Nightsong | zamek | Stormlands -> The Reach | 345 | 12 | 6.9 / 9.1 / -0.71 | 12: A zdob. | 7.8 | 4042 / 596 | 848 / 596 | 122 |
| 31 | City of Bones | zamek | Qarth -> House Targaryen | 346 | 11 | 7.4 / 4.3 / -1.03 | 11: A zdob. | 3.3 | 1978 / 843 | 1047 / 843 | 540 |
| 32 | Wayfarer's Rest | zamek | Riverlands -> House Baratheon of King's Landing | 357 | 11 | 6.4 / 23.6 / -0.46 | 11: A zdob. | 11.6 | 4749 / 428 | 829 / 428 | 272 |
| 33 | Hellholt | zamek | Dorne -> The Reach | 367 | 10 | 5.7 / 14.2 / -0.03 | 10: A zdob. | 11.2 | 4868 / 420 | 684 / 420 | 221 |
| 34 | Omber | miasto | Ibben -> Sarnor | 389 | 14 | 10.0 / 3.5 / -2.62 | 14: A zdob. | 2.8 | 1791 / 612 | 1137 / 612 | 147 |
| 35 | Qamayl | zamek | Sarnor -> Ibben | 392 | 11 | 9.2 / 8.4 / -2.15 | 12: A zdob. | 6.1 | 1342 / 195 | 358 / 195 | 360 |
| 36 | Acorn Hall | zamek | Riverlands -> House Baratheon of King's Landing | 397 | 13 | 7.1 / 10.1 / -0.88 | 13: A zdob. | 8.1 | 4329 / 596 | 874 / 596 | 105 |
| 37 | Gelona | zamek | Pentos -> Myr | 399 | 14 | 9.7 / 5.5 / -1.95 | 14: A zdob. | 4.1 | 2360 / 575 | 1116 / 575 | 135 |
| 38 | Stone Hedge | zamek | Riverlands -> House Baratheon of King's Landing | 413 | 14 | 8.0 / 8.8 / -1.39 | 14: A zdob. | 7.3 | 3798 / 548 | 757 / 548 | 267 |
| 39 | Vaes Dothrak | miasto | Dothraki Horde -> House Targaryen | 415 | 12 | 9.1 / 2.9 / -1.64 | 13: A zdob. | 22.4 | 1545 / 102 | 4 / 102 | 221 |
| 40 | Wyl | zamek | Dorne -> The Reach | 415 | 13 | 9.2 / 5.6 / -1.70 | 14: A zdob. | 4.2 | 2320 / 587 | 575 / 587 | 157 |
| 41 | Stone Hedge | zamek | House Baratheon of King's Landing -> Riverlands | 430 | 12 | 7.8 / 25.6 / -1.02 | 12: A zdob. | 19.9 | 6262 / 296 | 459 / 296 | 247 |
| 42 | Wayfarer's Rest | zamek | House Baratheon of King's Landing -> Riverlands | 456 | 11 | 4.9 / 11.4 / 0.43 | 12: A zdob. | 9.9 | 5894 / 592 | 1321 / 592 | 367 |
| 43 | Acorn Hall | zamek | House Baratheon of King's Landing -> Riverlands | 474 | 14 | 7.5 / 10.6 / -0.86 | 14: A zdob. | 8.3 | 5071 / 608 | 1166 / 608 | 154 |
| 44 | King's Landing | miasto | House Baratheon of King's Landing -> The North | 516 | 14 | 8.0 / 4.9 / -1.14 | 14: A zdob. | 30.6 | 2994 / 92 | 3 / 92 | 574 |
| 45 | Pinkmaiden | zamek | House Baratheon of King's Landing -> Riverlands | 520 | 11 | 6.8 / 8.2 / -0.69 | 12: A zdob. | 6.6 | 2933 / 407 | 814 / 407 | 610 |
| 46 | Tolos | miasto | House Targaryen, Aegon -> House Targaryen | 521 | 13 | 9.3 / 4.7 / -1.72 | 14: A zdob. | 3.6 | 2461 / 872 | 868 / 872 | 250 |
| 47 | Rosby | zamek | House Baratheon of King's Landing -> The North | 532 | 8 | 5.7 / 15.6 / -0.05 | 9: A zdob. | 13.9 | 3532 / 302 | 246 / 302 | 171 |
| 48 | Stoney Sept | miasto | House Baratheon of King's Landing -> Riverlands | 535 | 14 | 6.9 / 7.4 / -0.74 | 14: A zdob. | 5.5 | 4826 / 911 | 1594 / 911 | 418 |
| 49 | Duskendale | miasto | House Baratheon of King's Landing -> The North | 543 | 16 | 8.3 / 5.7 / -1.27 | 16: A zdob. | 6.0 | 6272 / 1293 | 1263 / 1293 | 137 |
| 50 | Craster's Keep | zamek | Free Folk -> Nights Watch | 557 | 13 | 9.5 / 5.8 / -2.33 | 13: A zdob. | 3.9 | 1633 / 587 | 395 / 587 | 166 |
| 51 | Deep Den | zamek | House Baratheon of King's Landing -> Riverlands | 566 | 11 | 8.2 / 10.9 / -1.50 | 12: A zdob. | 34.3 | 3463 / 98 | 133 / 98 | 415 |
| 52 | Lonely Light | miasto | Iron Islands -> The North | 598 | 13 | 9.0 / 6.8 / -2.01 | 14: A zdob. | 6.0 | 3134 / 502 | 713 / 502 | 198 |
| 53 | Silverhill | zamek | House Baratheon of King's Landing -> Riverlands | 603 | 12 | 7.1 / 7.8 / -0.69 | 12: A zdob. | 20.1 | 2903 / 108 | 151 / 108 | 349 |
| 54 | Harvest Hall | zamek | Dragonstone -> The Reach | 605 | 11 | 6.6 / 11.8 / -0.57 | 12: A zdob. | 9.0 | 4524 / 459 | 1015 / 459 | 249 |
| 55 | Blackhaven | zamek | Dragonstone -> The Reach | 610 | 7 | - | wypad | - | - | - | 308 |
| 56 | Odivo | zamek | Myr -> Drahar Conspiracy (Myr Rebels) | 615 | 13 | 9.7 / 4.1 / -2.40 | 13: A zdob. | 3.2 | 1185 / 348 | 625 / 348 | 166 |
| 57 | Blackhaven | zamek | The Reach -> Dragonstone | 634 | 12 | 7.7 / 11.0 / -1.20 | 12: A zdob. | 7.7 | 3808 / 478 | 918 / 478 | 160 |

## 4. Jak gra liczy postep oblezenia (kod)

### 4.1 Przygotowania i machiny

| Element | Wartosc | Zrodlo |
|---|---|---|
| postep na godzine | sqrt(zdrowi w obozie) / (osobodni x 24) + inzynieria, perki | [K] `DefaultSiegeEventModel.cs:351-401`, `:403-414` |
| buduje sie jedna rzecz naraz | najpierw przygotowania, potem pierwsza niezbudowana machina z listy | [K] `SiegeEvent.cs:1098-1124` |
| koszt (osobodni) | przygotowania 24 (Native 48), taran 12, wieza 16, katapulta 20, trebusz 40 | [K] `Modules\RBM\ModuleData\RBMCombat_siege_engines.xml` (RBM `SubModule.xml:194` laduje je po Native `siegeengines.xml`) |
| BannerKings `LongerSieges` | x(1 - 0.5) = x0.5 | [K] `bkfull\BannerKings.Patches\VanillaModelTweakPatches.cs:1251-1268`; [U] `BannerKings.json` 0.5 |
| RealisticBannerlord | x `SiegeConstructionSpeedMultiplier` (1.0), oblegajacy bez narzedzi (Tools) x0.75 | [K] RB `RealisticSiegeEventModel` (dekompilacja); [U] `RealisticBannerlord_v2.json` 1.0 |
| Armoury `SiegePacePercent` | x0.5, raz na lancuch modeli | [K] `Armoury/src/WorldPace.cs:128-145`; [U] `Armoury.json` 50 |
| razem dla oblegajacego | x0.25 (z narzedziami) albo x0.1875 (bez) wzgledem gry | [S] |

Czas do gotowosci (przygotowania + taran + wieza) [S z powyzszego]:

| Ludzi w obozie | koszty RBM bez mnoznikow (x1) | teraz, z narzedziami (x0.25) | teraz, bez narzedzi (x0.1875) |
|---|---|---|---|
| 1000 | 1.6 doby | 6.6 doby | 8.8 doby |
| 2000 | 1.2 | 4.7 | 6.2 |
| 2352 (mediana w chwili decyzji [P]) | 1.1 | 4.3 | 5.7 |
| 5000 | 0.7 | 2.9 | 3.9 |

(Bez perkow i umiejetnosci Engineering; czysta gra bez RBM ma przygotowania 48 osobodni zamiast 24.)

Wniosek [S]: obecne mnozniki (x0.25) odpowiadaja za ok. 3-5 z 8.4 doby do decyzji; reszte robi czekanie na okno w karze za machiny (rozdz. 5) i czlon czasu.
W chwili decyzji oboz mial zawsze 2-3 machiny (2: 84 razy, 3: 29), taran 110 razy, wieze 23 razy [P 113 linii O0].

### 4.2 Mury, ostrzal, naprawy

| Element | Wartosc | Zrodlo |
|---|---|---|
| wytrzymalosc murow | poziom 1: 30 000, 2: 50 000, 3: 67 000 (dzielone na odcinki) | [K] `DefaultWallHitPointCalculationModel.cs` |
| ostrzal (RBM) | katapulta 200 x 5 strzalow/dobe, onager 300 x 5, trebusz 1200 x 5 (przed trafieniem) | [K] `RBMCombat_siege_engines.xml` |
| naprawa murow | 0 w czasie oblezenia; po nim 4% calosci na dobe | [K] `DefaultSettlementGarrisonModel.cs:148-160`, `Town.cs:643-655` |
| wyrwa | przewaga osady x0.25 dopiero, gdy WSZYSTKIE odcinki maja 0 | [K] `DefaultCombatSimulationModel.cs:210-213` (cala przewaga `:172-227`) |
| czy szturm wymaga wyrwy | nie - decyzja patrzy na sily, przewage i czas | [K] `BesiegerCamp.cs:375-431` |

### 4.3 Straty obozu w czasie oblezenia

| Element | Wartosc | Zrodlo |
|---|---|---|
| RealisticBannerlord: 2-5 ludzi/dobe na druzyne (+1 co 5 dob) | wylaczone, gdy oboz nakarmiony (latka Mends); garnizon tylko bez jedzenia | [K] `CrashScribe/src/Mends.cs:4510-4570`; [P] sesja:283 "oboz oblezniczy nie gnije" |
| nasza choroba obozu (CampFever) | od 9. doby 0.6%/dobe, +15% za kazda dobe, x tlok | [K] `CampFever.cs:56-74`; [U] `Armoury.json` (Incubation 9, Base 0.6, Ramp 15) |
| ile to dalo w fin30 | 90 razy "zaraza wstaje" (9. doba oblezenia); 2129 chorych i 203 zmarlych oblegajacych w calym biegu | [P] linie "CampFever" (282 linie dzienne) |

## 5. Decyzja AI o szturmie

Wzor [K `BesiegerCamp.cs:375-431`], pytany tylko z mysli wodza obozu (`MobilePartyAi.GetBesiegeBehavior` [K `MobilePartyAi.cs:710-718`]), i tylko gdy przygotowania = 100%
(`IsReadyToBesiege` [K `BesiegerCamp.cs:49-59`]):

```
w     = 0.8 - 0.02 x (godzin oblezenia - 96)      (pierwsze 96 h: 0.8)        x1.25 bez taranu, x1.25 bez wiezy
s     = sila oblegajacych / (sila obroncow w murach x przewaga^w)          przewaga = (1 + 3 + poziom murow) / (1 + premie za machiny oblegajacych)
jesli s > 1: s x= min(3, machin)/3 x (1 - 0.85 x p^2),  p = postep budowanej machiny + 0.25 x (5 - machin)
szansa na mysl = 10% x s   (mysl AI co ok. 0.3 h w oblezeniu [K MobilePartyAi.cs:321-331; DefaultMobilePartyAIModel.cs:17])
```

Czlon czasu dziala w GODZINACH: po 96 h (4 doby) wykladnik spada o 0.02 na godzine, po 136 h (5.67 doby) jest 0, potem ujemny - mury zaczynaja dzialac na
KORZYSC oblegajacego (obroncy licza sie za mniej, niz ich jest). Ile dob, zanim wzor pozwoli na szturm przy danym stosunku sil [S z kodu; przygotowania musza byc gotowe]:

| Mury i machiny oblegajacego (przewaga, w poczatkowe) | 4:1 | 3:1 | 2:1 | 1:1 | 1:2 | 1:4 |
|---|---|---|---|---|---|---|
| mury 2, taran i wieza (3.82, 0.8) | od razu | od razu | 4.6 doby | 5.7 | 6.7 | 7.8 |
| mury 2, taran bez wiezy (4.26, 1.0) | 4.1 | 4.4 | 4.9 | 5.7 | 6.5 | 7.3 |
| mury 3, taran bez wiezy (4.97, 1.0) | 4.2 | 4.5 | 4.9 | 5.7 | 6.4 | 7.1 |
| mury 2 bez maszyn szturmowych (6.0, 1.25) | 4.6 | 4.8 | 5.2 | 5.7 | 6.2 | 6.7 |

Zmierzone w chwili decyzji (113 linii O0 fin30) [P]:

| Miara | Wartosc |
|---|---|
| wiek oblezenia | mediana 8.4 doby (p10 6.4, min 4.2, max 12.7) |
| wykladnik w | mediana -1.66; ujemny w 110 z 113 decyzji |
| przewaga murow | mediana 4.25 (3.06-5.56) |
| sila gry oblegajacy / obroncy | min 2.82, p10 3.45, mediana 5.74, p90 11.8, max 38.5; pustej warowni 0 |
| "mury" = stosunek bez czlonu czasu (jak gra w pierwszych 4 dobach) | mediana 1.50; <= 1 w 22 z 113 (19%) - tych szturmow bez czlonu czasu wzor by nie dopuscil |
| szansa na mysl | mediana 34.6%, p10 7.4%; 100% w 30 decyzjach |
| ludzie w obozie | p10 1350, mediana 2352, p90 4589 |

Co stalo sie dalej, wedlug "murow" w chwili decyzji [P, epizody fin30]:

| "mury" w chwili decyzji | szturm zdobyl | szturm bez rozstrzygniecia | okup O1 | oblegajacy pobity pod murami | odstapione |
|---|---|---|---|---|---|
| >= 1.5 (56 oblezen) | 43 | 5 | 0 | 8 | 0 |
| 1.0-1.5 (35) | 4 | 4 | 26 | 1 | 0 |
| <= 1.0 (22) | 7 | 9 | 0 | 5 | 1 |

(Okup bierze AI wlasnie przy "murach" < 1.5 - regula "krwawy szturm" [K `SiegeRansom.cs` naglowek, `Settings.cs:2150`].) Przy "murach" <= 1 szturm konczyl sie bez
rozstrzygniecia czesciej niz zdobyciem (9 vs 7) - tylko tu wzor gry "dopuscil" szturm, ktory mury powinny zatrzymac.

## 6. Szturm w symulacji - czemu 0 odpartych

**(a) AI nie zaczyna oblezenia bez przewagi** [K `DefaultTargetScoreCalculatingModel.cs:209-243`]: cel oblezenia odpada (wynik 0), gdy sila armii < 2 x (obroncy x (1 + 0.33 x poziom murow)
+ czesc sily krolestwa) - przy murach 2 to ok. 3.3 x sila obroncow (plus czesc sily krolestwa). SCA145 tylko mnozy wynik celu (front, ziemia, zmeczenie wojna) [K dekompilacja `StrategicTargetScoreModel`].
Skutek [P]: w chwili decyzji najslabsza przewaga 2.82, mediana 5.74 (rozdz. 5).

**(b) Mury w samej bitwie** [K `DefaultCombatSimulationModel.cs:241-281`, `:282-290`, `:336-343`; `MapEventSide.cs:993`]:
- runda co 60 minut (pole: co 30); w rundzie oblegajacy ma 2 x round(1.5 + O^0.3) ciosow, obronca 2 x round(0.5 + max(1 + O^0.3 x 0.7 x przewaga, (O+1)/(A+1))) - przy
  O = 450 obroncach i przewadze 4.25: 16 vs 40 ciosow, czyli obronca ok. 2.5 raza wiecej [S]; liczba ciosow NIE zalezy od liczby oblegajacych (dopoki obroncow > 100/30);
- premia murow tylko, gdy obroncow > 100 (miasto) / > 30 (zamek); ponizej tego ciosy licza sie od liczebnosci (A^0.6) i resztka garnizonu ginie szybko;
- oblegajacy -10% przewagi w szturmie; cios wylacza czlowieka z szansa obrazenia / max HP (ok. 1/4) [K];
- ROT owija model gry i zmienia tylko smoki [K dekompilacja `ROT.Models.ROTCombatSimulationModel`].
Stad bitwa szturmu trwa doby (rozdz. 2.1): oblegajacy traci ok. 1.5-2.5 czlowieka na kazdego obronce, wiec obronca moglby wygrac dopiero, gdy oblegajacych
(rownej jakosci) jest mniej niz ok. 1.5-2.5 raza tylu co obroncow [S]. AI szturmuje od 2.8 raza wzwyz [P] - wynik jest przesadzony, zanim bitwa sie zacznie.

**(c) Pomiar [P `bitwy.log` B175 "Siege"]:**

| Bieg | szturmow | zdobyte | bez rozstrzygniecia | wygral obronca | sila A/O (moc B175) zdobyte: min / mediana | bez rozstrzygniecia: min / mediana |
|---|---|---|---|---|---|---|
| fin30 | 73 | 55 | 18 | **0** | 2.81 / 7.48 | 2.56 / 3.42 |
| fin21 | 67 | 38 | 29 | **0** | 2.51 / 6.82 | 2.01 / 3.96 |

Straty w zdobytych szturmach fin30 (B175, z zastrzezeniem z rozdz. 1): oblegajacy 39 261 z 178 747 (22%), obroncy 25 553 z 25 553 (100%) - ok. 1.5 oblegajacego na obronce [P].
Zaloga na starcie oblezenia [P `ludzie-regiony.csv`]: miasta mediana 526 (+ milicja 205), zamki 257 (+191). Krolewska Przystan: 541 + 340 wobec 4484 ludzi Robba Starka [P log:204696].

**(d) "Bez rozstrzygniecia"** (gra: `WinningSide` = brak) - w fin30 kazde z 18 takich oblezen skonczylo sie razem ze szturmem (ta sama albo nastepna doba), drugiego szturmu nie bylo;
2 z 18 (fin21: 9 z 29) mialy po stronie oblegajacych 0 druzyn przy koncu [P]. Kod gry konczy bitwe bez zwyciezcy m.in., gdy jednej stronie zabraknie wodza albo strony nie sa
juz w wojnie (`DiplomaticallyFinished` [K `MapEvent.cs:1002-1005`]). Saltpans `dzien 108879`: szturm i koniec oblezenia o tej samej godzinie 15 [P log:16910, `bitwy.log:2008`], 2 doby pozniej armia odsieczy Edmure'a Tully'ego
(4539) bije oblegajacych [P `bitwy.log:2125`]. Przyczyny w pozostalych przypadkach nie zmierzono - nie zakladam.

**(e) Jak twierdza naprawde sie broni [P]:** wypad (`SallyOut`) - 44 bitwy, 42 wygrane przez wychodzacych; wychodza glownie armie i lordowie, ktorzy weszli do twierdzy
(lord 23, armia 17, sama zaloga 4), liczniejsi od oblegajacych mediana 1.9 raza. Bitwy pod murami (`SiegeOutside`) - 60, w 14 oblezeniach pobity oblegajacy, w pozostalych
pobita odsiecz. W historii twierdzy fin30: 42 oblezenia przerwane wypadem (mediana 6 dob), 14 bitwa pod murami (12-15 dob), 26 okupem (9 dob), 47 odstapione bez bitwy (4-5 dob).

## 7. Glod zalogi

| Miara (pierwsza doba oblezenia) [P `spichlerze-regiony.csv`] | Miasta (40 oblezen) | Zamki (166) |
|---|---|---|
| zapas jedzenia `zywnosc` - mediana (min - max) | 28 571 (19 264 - 45 910) | 8 616 (6 288 - 16 620) |
| dzienny bilans w oblezeniu - mediana (najgorszy) | -97 (-261) | -38 (-89) |
| doby jedzenia = zapas / ubytek - mediana (min) | 274 (75) | 225 (91) |
| doby glodu (`glodna_warownia` = 1) w oblezeniach | **0** | **0** |
| fin21 (dla porownania) | 263 doby (min 148), 0 glodu | 206 dob (min 108), 0 glodu |

Wniosek [P]: zadne oblezenie nie trwalo nawet 1/4 zapasu. Glod jako droga zdobycia nie istnieje, bo nikt nie czeka. Gra liczy glod przy wyborze celu dopiero przy zapasie < 100
[K `DefaultTargetScoreCalculatingModel.cs:227-229`] - u nas nigdy. Zapasy same w sobie sa historycznie rozsadne (zamek na 6-12 miesiecy [H]).
W oblezeniu bilans jedzenia przechodzi z dodatniego na ujemny (Cape Wrath: +55 w dobie przed, -32 w pierwszej dobie oblezenia [P `spichlerze-regiony.csv` ROT_castle18,
108844-108845]); uchodzcy chronia sie w murach i jedza z zapasu (`FamineSiegeShelter`) [K `Famine.cs:460-470`].

## 8. Historia i lore - ile trwaly oblezenia

| Oblezenie | Kiedy | Ile trwalo | Jak sie skonczylo | Znacznik |
|---|---|---|---|---|
| Antiochia | 1097-1098 | ok. 8 miesiecy | zdrada (otwarta wieza) | [H] |
| Jerozolima | 1099 | ok. 5.5 tygodnia | szturm | [H] |
| Akka | 1189-1191 | ok. 2 lata | kapitulacja | [H] |
| Chateau Gaillard | 1203-1204 | ok. 6-7 miesiecy | wyglodzenie, podkopy, szturm | [H] |
| Rochester | 1215 | ok. 7 tygodni | podkop pod wieze, glod | [H] |
| Kenilworth | 1266 | ok. 6 miesiecy | glod i choroby, kapitulacja | [H] |
| Calais | 1346-1347 | ok. 11 miesiecy | glod, kapitulacja | [H] |
| Malbork | 1410 | ok. 2 miesiace | odstapione | [H] |
| Rouen | 1418-1419 | ok. 6 miesiecy | glod, kapitulacja | [H] |
| Orlean | 1428-1429 | ok. 7 miesiecy | odsiecz | [H] |
| Konstantynopol | 1453 | 53 dni | szturm | [H] |
| Harlech | 1461-1468 | ok. 7 lat (blokada z przerwami) | kapitulacja | [H] |
| Storm's End (Rebelia Roberta) | 283 AC | prawie rok | wyglodzony garnizon Stannisa wytrzymal do odsieczy Eddarda Starka (przemyt Davosa) | [L] |
| Riverrun (Blackfish) | 299-300 AC | miesiace; zapasy "na dwa lata" | poddanie wynegocjowane (Jaime, Edmure) | [L] |
| Krolewska Przystan (zlupienie) | 283 AC | jeden dzien | zdrada - Tywin wpuszczony za mury | [L] |
| Czarny Nurt | 299 AC | jeden dzien | szturm odparty (odsiecz Tyrellow) | [L] |
| Harrenhal (Roose Bolton) | 299 AC | - | zdrada najemnikow | [L] |

Wzor [H]: szturm na cale mury byl rzadki i krwawy; twierdze padaly glownie z glodu, przez zdrade, po umowie o poddaniu ("jesli odsiecz nie przyjdzie do dnia X") albo
nie padaly wcale, gdy przyszla odsiecz. Szybkie szturmy - gdy obsada byla mala albo mury zaniedbane. W grze jest odwrotnie [P]: 55 z 57 zdobyc to szturm, 0 z glodu,
0 z umowy o poddaniu; obsada bywa mala (Krolewska Przystan 541 + 340, rozdz. 6).

Porownanie [S]: polowa zdobyc w grze 12 dob; typowe historyczne oblezenie twierdzy 2-7 miesiecy (60-210 dob) = 5-18 razy dluzej. W ulamku roku: gra 0.033, historia 0.15-0.6.

## 9. Dzwignie (rozpoznanie - nic nie zmienione, do decyzji)

| # | Dzwignia | Gdzie | Co jest dzis | Co by dala [S] | Uwagi |
|---|---|---|---|---|---|
| 1 | Czlon czasu w decyzji o szturmie | `BesiegerCamp.cs:408-409` (postfiks na tej metodzie juz mamy - `SiegeRansom.AssaultPost`) | po 96 h -0.02/h; po 5.7 doby mury nic nie znacza, potem pomagaja oblegajacemu [K] | bez ujemnego czlonu 22 ze 113 decyzji (19%) by nie zapadlo [P]; szturmy przy "murach" >= 1.5 (56) dalej ok. 8. doby | samo to nie robi miesiecy - silne armie i tak szturmuja; slabsze zaczna czekac (pkt 5) |
| 2 | Prog szturmu wobec murow | ten sam wzor (`s > 1`, potem 10% x s na mysl) | wystarcza s > 1 | np. s > 2-3 albo wymog wyrwy: dzis "mury" mediana 1.5 [P] - wiekszosc szturmow by poczekala | AI czeka -> glod, choroba, rozpad armii, odsiecz |
| 3 | Sila murow w samej bitwie | `DefaultCombatSimulationModel.cs:241-281` (0.7 x przewaga, prog 100/30 obroncow) | obronca ok. 2.5 raza wiecej ciosow, premia znika ponizej 100/30 [K] | krwawsze szturmy, odparcia mozliwe przy 2.8-3.5:1 | dotyczy tez autobitwy gracza |
| 4 | Glod jako droga zdobycia | zapasy `Town.FoodStocks` | 225-274 dob zapasu (mediana), 0 dob glodu [P] | zadziala dopiero, gdy AI bedzie czekac (pkt 1-2); dzis 7-9 miesiecy - historycznie rozsadne [H] | zalezy od liczby ludzi w murach (uchodzcy `FamineSiegeShelter`) |
| 5 | Wytrzymalosc armii w dlugim oblezeniu | spojnosc (BK `CohesionBoost` 0.5 [U], SCA -3.5 na wrogiej ziemi [K `StrategicAiTuning`]), zold, jedzenie obozu, CampFever | najdluzsze oblezenie 17 dob (fin30) / 24 (fin21) [P] | bez tego dluzsze oblezenia zamienia sie w odstapienia | CampFever po 60 dobach: 0.6% x (1 + 51 x 0.15) = 5.2%/dobe x tlok [S] - zniszczylby oboz |
| 6 | Tempo machin | `SiegePacePercent` 50 [U], BK `LongerSieges` 0.5 [U], RB 1.0 [U], RBM przygotowania 24 osobodni [K] | razem x0.25 (x0.19 bez narzedzi) | wolniej = decyzja proporcjonalnie pozniej (dzis 4-6 z 8.4 doby) | `AUDYT-CZAS-MAPA.md` 3.1: nie zmieniac kilku naraz |
| 7 | Mury i wyrwa | naprawa 0 w oblezeniu, x0.25 dopiero gdy wszystkie odcinki = 0 [K] | szturm nie potrzebuje wyrwy | szturm (albo premia murow) zalezny od zniszczenia murow; trebusz RBM 6000/dobe wobec 50 000 murow 2 = tygodnie [S] | ostrzal zalezy od trafien - nie mierzone |
| 8 | Prog rozpoczecia oblezenia | `DefaultTargetScoreCalculatingModel.cs:232-243` (2 x obrona z murami) | AI bierze cele, gdzie ma ok. 3x przewagi | to temat "sily" (garnizony, np. KP 541 + 340 wobec 4484) | wspolne z rozpoznaniem sil |
| 9 | Poddanie na warunkach | brak w grze; jest okup O1 (26 oblezen, ok. 9. doby) [P] | twierdza nie poddaje sie bez szturmu | historycznie glowna droga [H]: poddanie, gdy brak odsieczy przez N dob albo zapas < X | nowa mechanika - tylko kierunek |

## 10. Czego nie wiem

- **Czemu 18 (fin30) / 29 (fin21) szturmow skonczylo sie "bez rozstrzygniecia".** Wiem tylko: oblezenie konczylo sie razem ze szturmem, w 2/18 (9/29) strona oblegajacych byla pusta,
  9 z 16 szturmow przy "murach" <= 1 skonczylo sie tak [P]; kod dopuszcza koniec bez zwyciezcy przy braku wodza / koncu wojny [K]. Konkretnej przyczyny nie zmierzono.
- **Czemu 47 oblezen (fin30) odstapiono bez bitwy (mediana 4-5 dob).** Brak linii o odejsciu obozu albo rozwiazaniu armii; nie zakladam (spojnosc, zmiana celu AI, zblizajaca sie odsiecz?).
- **Dokladny czas gotowosci przygotowan i machin.** Gra tego nie loguje (linia `WorldPace: oblezenie` tylko dla gracza); czas w rozdz. 4.1 to szacunek z kodu. Nie wiem, czy partie AI
  maja narzedzia (Tools - x0.75 RB) ani jakie maja Engineering i perki.
- **Czy RBM naprawde nadpisuje przygotowania 48 -> 24 osobodni.** Z kolejnosci modulow i starego audytu (`AUDYT-CZAS-MAPA.md`, "24 osobodni (LOG)"), nie z logu tego biegu.
- **Pelne straty szturmow.** B175 bywa niepelne przy szturmach wielodniowych (KP: 92 obroncow w B175 wobec 541 + 340 w CSV) - wymiana 1.5 oblegajacego na obronce jest orientacyjna.
- **Czestotliwosc mysli AI w oblezeniu.** Sprawdzona tylko gra (0.25 h x 0.6-0.7 x 2); nie sprawdzalem, czy inny mod ja zmienia (w tym biegu i tak szturm zaczynal sie przy pierwszym "true").
- **Trafnosc ostrzalu i realne niszczenie murow** - nie logowane.
- **Historia i lore** - z wiedzy ogolnej, bez sprawdzenia zrodel w tej sesji (daty i dlugosci przyblizone).
- **Skutki dluzszych oblezen** dla CampFever, spojnosci armii, zoldu i jedzenia obozu - nie liczone (poza zakresem "czasu"); potrzebne przed jakakolwiek zmiana pkt 1-2 rozdz. 9.

## Aneks A - wszystkie 206 oblezen fin30 [P]

Kolumny: start = doba biegu (108836 = 1); dob = polnoce w oblezeniu (+-1); O0 = wiek oblezenia przy decyzji gry o szturmie; szturm = doba od startu, w ktorej skonczyla sie bitwa
szturmu; W = wypad, M = bitwa pod murami (doba od startu); okup = doba okupu O1. fin21: `robocze\czas\fin21-epizody.csv`.

| # | osada | rodzaj | pan (start) | start | dob | lata (364) | wynik | O0: wiek dob | szturm: doba konca (wynik) | wypad / pod murami: doba (wynik) | okup: doba |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | Cape Wrath | zamek | Dragonstone | 10 | 12 | 0.033 | zdobyte szturmem | 10.4 | 13 (A zdob.) | - | - |
| 2 | Vaes Efe | zamek | Dothraki Horde | 14 | 4 | 0.011 | odstapione bez bitwy | - | - | - | - |
| 3 | Fellwood | zamek | Dragonstone | 29 | 10 | 0.027 | zdobyte szturmem | 8.5 | 11 (A zdob.) | M10 (odsiecz pobita) | - |
| 4 | Demongate | zamek | House Targaryen, Aegon | 30 | 7 | 0.019 | odstapione bez bitwy | - | - | - | - |
| 5 | Saltpans | miasto | Riverlands | 36 | 8 | 0.022 | szturm bez zdobycia, potem odstapione | 7.9 | 8 (brak bez) | - | - |
| 6 | Starfall | miasto | Dorne | 37 | 10 | 0.027 | zdobyte szturmem | 6.1 | 11 (A zdob.) | - | - |
| 7 | Demongate | zamek | House Targaryen, Aegon | 39 | 9 | 0.025 | okup (O1) | 8.6 | - | - | 9 |
| 8 | Wickenden | miasto | The Vale | 49 | 7 | 0.019 | okup (O1) | 6.6 | - | - | 7 |
| 9 | Blackmont | zamek | Dorne | 52 | 9 | 0.025 | szturm bez zdobycia, potem odstapione | 6.9 | 9 (brak bez) | - | - |
| 10 | Gargos | zamek | Qohor | 53 | 7 | 0.019 | wypad - pobity oblegajacy | - | - | W7 (pobity obl.) | - |
| 11 | Sandstone | zamek | Dorne | 63 | 12 | 0.033 | pod murami - pobity oblegajacy | 8.9 | - | M12 (pobity obl.) | - |
| 12 | Bloody Gate | zamek | The Vale | 65 | 12 | 0.033 | pod murami - pobity oblegajacy | 8.4 | - | M5 (odsiecz pobita), M12 (pobity obl.) | - |
| 13 | Gargos | zamek | Qohor | 76 | 12 | 0.033 | zdobyte szturmem | 8.3 | 12 (A zdob.) | - | - |
| 14 | Qamayl | zamek | Ibben | 76 | 6 | 0.016 | wypad - pobity oblegajacy | - | - | W6 (pobity obl.) | - |
| 15 | Sweetport | zamek | Dragonstone | 76 | 15 | 0.041 | zdobyte szturmem | 10.1 | 15 (A zdob.) | - | - |
| 16 | Vaes Efe | zamek | Dothraki Horde | 81 | 7 | 0.019 | wypad - pobity oblegajacy | - | - | W7 (pobity obl.) | - |
| 17 | Tolerrol | zamek | Myr | 86 | 6 | 0.016 | wypad - pobity oblegajacy | - | - | W6 (pobity obl.) | - |
| 18 | Sandstone | zamek | Dorne | 87 | 10 | 0.027 | zdobyte szturmem | 6.7 | 11 (A zdob.) | - | - |
| 19 | Saltpans | miasto | Riverlands | 87 | 13 | 0.036 | szturm bez zdobycia, potem odstapione | 8.9 | 13 (brak bez) | - | - |
| 20 | Ar Noy | zamek | Qohor | 93 | 12 | 0.033 | zdobyte szturmem | 9.1 | 13 (A zdob.) | - | - |
| 21 | Blackmont | zamek | Dorne | 94 | 11 | 0.030 | pod murami - pobity oblegajacy | 9.3 | - | M8 (odsiecz pobita), M12 (pobity obl.) | - |
| 22 | Sweetport | zamek | Stormlands | 94 | 2 | 0.005 | odstapione bez bitwy | - | - | - | - |
| 23 | Qamayl | zamek | Ibben | 98 | 5 | 0.014 | wypad - pobity oblegajacy | - | - | W5 (pobity obl.) | - |
| 24 | Sunstone | zamek | Lys | 106 | 9 | 0.025 | okup (O1) | 9.1 | - | - | 9 |
| 25 | Bloody Gate | zamek | The Vale | 106 | 10 | 0.027 | zdobyte szturmem | 7.6 | 10 (A zdob.) | M9 (odsiecz pobita) | - |
| 26 | Vaes Efe | zamek | Dothraki Horde | 110 | 3 | 0.008 | wypad - pobity oblegajacy | - | - | W4 (pobity obl.) | - |
| 27 | Stonedance | zamek | Dragonstone | 116 | 12 | 0.033 | zdobyte szturmem | 8.2 | 12 (A zdob.) | - | - |
| 28 | Bloody Gate | zamek | Riverlands | 117 | 2 | 0.005 | odstapione bez bitwy | - | - | - | - |
| 29 | Harrenhal | miasto | House Baratheon of King's Landing | 118 | 10 | 0.027 | zdobyte szturmem | - | 10 (A zdob.) | - | - |
| 30 | Mhysa Faer | miasto | Tyrosh | 119 | 8 | 0.022 | okup (O1) | 7.9 | - | - | 8 |
| 31 | Sandstone | zamek | The Reach | 121 | 9 | 0.025 | wypad - pobity oblegajacy | - | - | W9 (pobity obl.), M6 (odsiecz pobita) | - |
| 32 | Demongate | zamek | House Targaryen, Aegon | 122 | 10 | 0.027 | zdobyte szturmem | 8.5 | 10 (A zdob.) | M9 (odsiecz pobita) | - |
| 33 | Mardosh | zamek | Sarnor | 124 | 5 | 0.014 | wypad - pobity oblegajacy | - | - | W5 (pobity obl.) | - |
| 34 | Lorath | miasto | Lorath | 130 | 15 | 0.041 | zdobyte szturmem | 10.1 | 15 (A zdob.) | - | - |
| 35 | Vaes Efe | zamek | Dothraki Horde | 131 | 2 | 0.005 | odstapione bez bitwy | - | - | - | - |
| 36 | Bolozo | miasto | Pentos | 135 | 12 | 0.033 | szturm bez zdobycia, potem odstapione | 8.0 | 12 (brak bez) | - | - |
| 37 | Blackmont | zamek | Dorne | 137 | 12 | 0.033 | zdobyte szturmem | 8.6 | 12 (A zdob.) | M11 (odsiecz pobita) | - |
| 38 | City of Bones | zamek | Qarth | 144 | 7 | 0.019 | odstapione bez bitwy | - | - | M5 (odsiecz pobita), M7 (odsiecz pobita) | - |
| 39 | Bisa | zamek | Lorath | 153 | 9 | 0.025 | okup (O1) | 8.7 | - | - | 9 |
| 40 | Sharp Point | miasto | Dragonstone | 153 | 6 | 0.016 | okup (O1) | 6.3 | - | - | 7 |
| 41 | Sandstone | zamek | The Reach | 156 | 11 | 0.030 | szturm bez zdobycia, potem odstapione | 7.2 | 11 (brak bez) | - | - |
| 42 | Sweetport | zamek | Stormlands | 160 | 2 | 0.005 | wypad - pobity oblegajacy | - | - | W3 (pobity obl.) | - |
| 43 | Vaes Efe | zamek | Dothraki Horde | 160 | 7 | 0.019 | odstapione bez bitwy | - | - | - | - |
| 44 | Mardosh | zamek | Sarnor | 161 | 5 | 0.014 | wypad - pobity oblegajacy | - | - | W5 (pobity obl.) | - |
| 45 | Goldentooth | zamek | House Baratheon of King's Landing | 161 | 1 | 0.003 | odstapione bez bitwy | - | - | - | - |
| 46 | Willow Wood | zamek | Riverlands | 167 | 8 | 0.022 | zdobyte szturmem | 6.4 | 8 (A zdob.) | - | - |
| 47 | Anogar | zamek | Lorath | 169 | 12 | 0.033 | odstapione bez bitwy | - | - | - | - |
| 48 | Kingsgrave | zamek | Dorne | 169 | 5 | 0.014 | odstapione bez bitwy | - | - | - | - |
| 49 | Stoney Sept | miasto | House Baratheon of King's Landing | 170 | 7 | 0.019 | okup (O1) | 7.0 | - | - | 7 |
| 50 | City of Bones | zamek | Qarth | 173 | 17 | 0.047 | odstapione bez bitwy | 11.9 | - | M4 (odsiecz pobita), M17 (odsiecz pobita) | - |
| 51 | Vaes Efe | zamek | Dothraki Horde | 175 | 2 | 0.005 | odstapione bez bitwy | - | - | - | - |
| 52 | Sandstone | zamek | The Reach | 179 | 12 | 0.033 | pod murami - pobity oblegajacy | 8.6 | - | M13 (pobity obl.) | - |
| 53 | Tolerrol | zamek | Myr | 180 | 6 | 0.016 | wypad - pobity oblegajacy | - | - | W6 (pobity obl.) | - |
| 54 | Qamayl | zamek | Ibben | 180 | 12 | 0.033 | zdobyte szturmem | 11.8 | 13 (A zdob.) | M7 (odsiecz pobita), M13 (odsiecz pobita) | - |
| 55 | Deep Den | zamek | House Baratheon of King's Landing | 180 | 3 | 0.008 | odstapione bez bitwy | - | - | - | - |
| 56 | Sunstone | zamek | Lys | 181 | 10 | 0.027 | szturm bez zdobycia, potem odstapione | 9.0 | 10 (brak bez) | - | - |
| 57 | Draconys | miasto | Qohor | 183 | 5 | 0.014 | odstapione bez bitwy | - | - | - | - |
| 58 | Deep Den | zamek | House Baratheon of King's Landing | 187 | 2 | 0.005 | wypad - pobity oblegajacy | - | - | W2 (pobity obl.) | - |
| 59 | Banefort | zamek | House Baratheon of King's Landing | 190 | 4 | 0.011 | odstapione bez bitwy | - | - | - | - |
| 60 | Blackmont | zamek | The Reach | 191 | 6 | 0.016 | wypad - pobity oblegajacy | - | - | W6 (pobity obl.) | - |
| 61 | Pinkmaiden | zamek | Riverlands | 191 | 2 | 0.005 | odstapione bez bitwy | - | - | - | - |
| 62 | City of Bones | zamek | Qarth | 192 | 9 | 0.025 | okup (O1) | 8.5 | - | - | 9 |
| 63 | Sunstone | zamek | Lys | 194 | 10 | 0.027 | szturm bez zdobycia, potem odstapione | 9.2 | 10 (brak bez) | - | - |
| 64 | Pinkmaiden | zamek | Riverlands | 194 | 10 | 0.027 | pod murami - pobity oblegajacy | 7.3 | - | M10 (pobity obl.) | - |
| 65 | Borash | zamek | House Targaryen | 198 | 1 | 0.003 | odstapione bez bitwy | - | - | - | - |
| 66 | Anogar | zamek | Lorath | 200 | 5 | 0.014 | wypad - pobity oblegajacy | - | - | W5 (pobity obl.) | - |
| 67 | Vaes Efe | zamek | Dothraki Horde | 200 | 10 | 0.027 | szturm bez zdobycia, potem odstapione | 9.0 | 10 (brak bez) | - | - |
| 68 | Kingsgrave | zamek | Dorne | 202 | 10 | 0.027 | szturm bez zdobycia, potem odstapione | 6.3 | 11 (brak bez) | - | - |
| 69 | Sunstone | zamek | Lys | 205 | 4 | 0.011 | odstapione bez bitwy | - | - | - | - |
| 70 | Deep Den | zamek | House Baratheon of King's Landing | 206 | 12 | 0.033 | zdobyte szturmem | 7.3 | 12 (A zdob.) | M11 (odsiecz pobita) | - |
| 71 | Mhysa Faer | miasto | Tyrosh | 210 | 9 | 0.025 | szturm bez zdobycia, potem odstapione | 8.2 | 9 (brak bez) | - | - |
| 72 | Willow Wood | zamek | House Baratheon of King's Landing | 212 | 7 | 0.019 | zdobyte szturmem | 5.6 | 7 (A zdob.) | - | - |
| 73 | Vaes Efe | zamek | Dothraki Horde | 212 | 9 | 0.025 | zdobyte szturmem | 5.9 | 10 (A zdob.) | - | - |
| 74 | Iksa Keria | zamek | Braavos | 217 | 10 | 0.027 | okup (O1) | 9.8 | - | - | 10 |
| 75 | Tolerrol | zamek | Myr | 219 | 8 | 0.022 | wypad - pobity oblegajacy | - | - | W8 (pobity obl.) | - |
| 76 | Sharp Point | miasto | Dragonstone | 223 | 9 | 0.025 | zdobyte szturmem | 4.2 | 10 (A zdob.) | - | - |
| 77 | Sunstone | zamek | Lys | 229 | 5 | 0.014 | odstapione bez bitwy | - | - | - | - |
| 78 | Kingsgrave | zamek | Dorne | 229 | 14 | 0.038 | zdobyte szturmem | 9.5 | 15 (A zdob.) | - | - |
| 79 | Draconys | miasto | Qohor | 233 | 10 | 0.027 | okup (O1) | 10.7 | - | M7 (odsiecz pobita) | 11 |
| 80 | Totava | zamek | Braavos | 233 | 14 | 0.038 | zdobyte szturmem | 9.8 | 14 (A zdob.) | - | - |
| 81 | Omber | miasto | Ibben | 234 | 15 | 0.041 | pod murami - pobity oblegajacy | 11.1 | - | M15 (pobity obl.) | - |
| 82 | Goldentooth | zamek | House Baratheon of King's Landing | 235 | 2 | 0.005 | odstapione bez bitwy | - | - | - | - |
| 83 | Mhysa Faer | miasto | Tyrosh | 236 | 10 | 0.027 | szturm bez zdobycia, potem odstapione | 8.5 | 10 (brak bez) | - | - |
| 84 | Hull | miasto | Dragonstone | 236 | 10 | 0.027 | okup (O1) | 9.9 | - | - | 10 |
| 85 | Iksa Keria | zamek | Braavos | 238 | 10 | 0.027 | okup (O1) | 9.4 | - | - | 10 |
| 86 | Stoney Sept | miasto | House Baratheon of King's Landing | 245 | 8 | 0.022 | odstapione bez bitwy | - | - | - | - |
| 87 | Sunstone | zamek | Lys | 248 | 14 | 0.038 | szturm bez zdobycia, potem odstapione | 11.0 | 15 (brak bez) | - | - |
| 88 | Skyreach | zamek | Dorne | 250 | 7 | 0.019 | wypad - pobity oblegajacy | - | - | W7 (pobity obl.) | - |
| 89 | Dragonstone | miasto | Dragonstone | 252 | 6 | 0.016 | odstapione bez bitwy | - | - | - | - |
| 90 | Vaes Dothrak | miasto | Dothraki Horde | 258 | 17 | 0.047 | pod murami - pobity oblegajacy | 9.9 | - | M17 (pobity obl.) | - |
| 91 | Skyreach | zamek | Dorne | 262 | 11 | 0.030 | zdobyte szturmem | 7.5 | 11 (A zdob.) | M10 (odsiecz pobita) | - |
| 92 | Stoney Sept | miasto | House Baratheon of King's Landing | 267 | 12 | 0.033 | pod murami - pobity oblegajacy | 5.9 | - | M13 (pobity obl.) | - |
| 93 | Iksa Calo | zamek | Braavos | 268 | 15 | 0.041 | zdobyte szturmem | 9.1 | 15 (A zdob.) | - | - |
| 94 | Mhysa Faer | miasto | Tyrosh | 269 | 5 | 0.014 | odstapione bez bitwy | - | - | - | - |
| 95 | Qamayl | zamek | Sarnor | 273 | 7 | 0.019 | wypad - pobity oblegajacy | - | - | W7 (pobity obl.) | - |
| 96 | Sunstone | zamek | Lys | 279 | 12 | 0.033 | zdobyte szturmem | 9.4 | 12 (A zdob.) | - | - |
| 97 | Hellholt | zamek | Dorne | 281 | 11 | 0.030 | okup (O1) | 11.1 | - | M8 (odsiecz pobita) | 11 |
| 98 | City of Bones | zamek | Qarth | 286 | 15 | 0.041 | pod murami - pobity oblegajacy | 11.2 | - | M16 (pobity obl.) | - |
| 99 | Willow Wood | zamek | Riverlands | 290 | 9 | 0.025 | zdobyte szturmem | 7.2 | 9 (A zdob.) | - | - |
| 100 | Pinkmaiden | zamek | Riverlands | 293 | 9 | 0.025 | wypad - pobity oblegajacy | - | - | W9 (pobity obl.) | - |
| 101 | Iksa Keria | zamek | Braavos | 297 | 13 | 0.036 | zdobyte szturmem | 8.4 | 13 (A zdob.) | - | - |
| 102 | Bolozo | miasto | Pentos | 299 | 14 | 0.038 | zdobyte szturmem | 7.4 | 14 (A zdob.) | - | - |
| 103 | Sombazmion | zamek | Lys | 307 | 9 | 0.025 | okup (O1) | 9.5 | - | - | 9 |
| 104 | Yronwood | miasto | Dorne | 307 | 17 | 0.047 | zdobyte szturmem | 7.7 | 17 (A zdob.) | - | - |
| 105 | Pinkmaiden | zamek | Riverlands | 310 | 5 | 0.014 | zdobyte po wypadzie | - | - | W5 (wypad pobity), M1 (odsiecz pobita), M2 (odsiecz pobita) | - |
| 106 | Qamayl | zamek | Sarnor | 311 | 7 | 0.019 | wypad - pobity oblegajacy | - | - | W7 (pobity obl.) | - |
| 107 | City of Bones | zamek | Qarth | 316 | 6 | 0.016 | wypad - pobity oblegajacy | - | - | W6 (pobity obl.) | - |
| 108 | Gelona | zamek | Pentos | 317 | 10 | 0.027 | okup (O1) | 10.1 | - | - | 10 |
| 109 | Alnor | zamek | Lys | 318 | 9 | 0.025 | okup (O1) | 9.0 | - | - | 9 |
| 110 | Maidenpool | miasto | Riverlands | 321 | 5 | 0.014 | odstapione bez bitwy | - | - | - | - |
| 111 | Wayfarer's Rest | zamek | Riverlands | 328 | 4 | 0.011 | wypad - pobity oblegajacy | - | - | W4 (pobity obl.) | - |
| 112 | Vaes Efe | zamek | House Targaryen | 328 | 7 | 0.019 | wypad - pobity oblegajacy | - | - | W7 (pobity obl.), M6 (odsiecz pobita) | - |
| 113 | Wyl | zamek | Dorne | 329 | 3 | 0.008 | odstapione bez bitwy | - | - | - | - |
| 114 | Maidenpool | miasto | Riverlands | 330 | 9 | 0.025 | okup (O1) | 8.9 | - | - | 9 |
| 115 | Wayfarer's Rest | zamek | Riverlands | 336 | 3 | 0.008 | wypad - pobity oblegajacy | - | - | W3 (pobity obl.) | - |
| 116 | Wyl | zamek | Dorne | 338 | 3 | 0.008 | odstapione bez bitwy | - | - | - | - |
| 117 | Qamayl | zamek | Sarnor | 339 | 7 | 0.019 | wypad - pobity oblegajacy | - | - | W7 (pobity obl.) | - |
| 118 | Astapor | miasto | House Targaryen | 341 | 5 | 0.014 | odstapione bez bitwy | - | - | - | - |
| 119 | Mantarys | miasto | House Targaryen, Aegon | 341 | 15 | 0.041 | szturm bez zdobycia, potem odstapione | 11.6 | 15 (brak bez) | - | - |
| 120 | Pinkmaiden | zamek | House Baratheon of King's Landing | 344 | 10 | 0.027 | wypad - pobity oblegajacy | - | - | W10 (pobity obl.), M4 (odsiecz pobita), M5 (odsiecz pobita), M6 (odsiecz pobita), M6 (odsiecz pobita) | - |
| 121 | Nightsong | zamek | Stormlands | 345 | 12 | 0.033 | zdobyte szturmem | 6.9 | 12 (A zdob.) | - | - |
| 122 | City of Bones | zamek | Qarth | 346 | 11 | 0.030 | zdobyte szturmem | 7.4 | 11 (A zdob.) | - | - |
| 123 | Wayfarer's Rest | zamek | Riverlands | 357 | 11 | 0.030 | zdobyte szturmem | 6.4 | 11 (A zdob.) | - | - |
| 124 | City of Bones | zamek | House Targaryen | 359 | 5 | 0.014 | odstapione bez bitwy | - | - | - | - |
| 125 | Qamayl | zamek | Sarnor | 362 | 8 | 0.022 | wypad - pobity oblegajacy | - | - | W9 (pobity obl.) | - |
| 126 | Vaes Efe | zamek | House Targaryen | 362 | 8 | 0.022 | okup (O1) | 7.7 | - | - | 8 |
| 127 | Hellholt | zamek | Dorne | 367 | 10 | 0.027 | zdobyte szturmem | 5.7 | 10 (A zdob.) | - | - |
| 128 | Stone Hedge | zamek | Riverlands | 374 | 6 | 0.016 | wypad - pobity oblegajacy | - | - | W6 (pobity obl.) | - |
| 129 | Acorn Hall | zamek | Riverlands | 378 | 2 | 0.005 | odstapione bez bitwy | - | - | - | - |
| 130 | Harvest Hall | zamek | Stormlands | 379 | 8 | 0.022 | wypad - pobity oblegajacy | - | - | W8 (pobity obl.), M6 (odsiecz pobita) | - |
| 131 | Borash | zamek | House Targaryen | 380 | 2 | 0.005 | wypad - pobity oblegajacy | - | - | W3 (pobity obl.) | - |
| 132 | Wayfarer's Rest | zamek | House Baratheon of King's Landing | 384 | 11 | 0.030 | pod murami - pobity oblegajacy | 6.9 | - | M11 (pobity obl.) | - |
| 133 | Sombazmion | zamek | Lys | 385 | 10 | 0.027 | okup (O1) | 10.5 | - | - | 10 |
| 134 | Omber | miasto | Ibben | 389 | 14 | 0.038 | zdobyte szturmem | 10.0 | 14 (A zdob.) | - | - |
| 135 | Qamayl | zamek | Sarnor | 392 | 11 | 0.030 | zdobyte szturmem | 9.2 | 12 (A zdob.) | - | - |
| 136 | Harvest Hall | zamek | Stormlands | 395 | 8 | 0.022 | okup (O1) | 8.0 | - | M6 (odsiecz pobita), M7 (odsiecz pobita) | 8 |
| 137 | Acorn Hall | zamek | Riverlands | 397 | 13 | 0.036 | zdobyte szturmem | 7.1 | 13 (A zdob.) | - | - |
| 138 | Gelona | zamek | Pentos | 399 | 14 | 0.038 | zdobyte szturmem | 9.7 | 14 (A zdob.) | - | - |
| 139 | Stone Hedge | zamek | Riverlands | 404 | 3 | 0.008 | odstapione bez bitwy | - | - | - | - |
| 140 | Fellwood | zamek | Stormlands | 409 | 5 | 0.014 | wypad - pobity oblegajacy | - | - | W5 (pobity obl.) | - |
| 141 | Qamayl | zamek | Ibben | 413 | 11 | 0.030 | odstapione bez bitwy | - | - | - | - |
| 142 | Stone Hedge | zamek | Riverlands | 413 | 14 | 0.038 | zdobyte szturmem | 8.0 | 14 (A zdob.) | - | - |
| 143 | Wyl | zamek | Dorne | 415 | 13 | 0.036 | zdobyte szturmem | 9.2 | 14 (A zdob.) | - | - |
| 144 | Vaes Dothrak | miasto | Dothraki Horde | 415 | 12 | 0.033 | zdobyte szturmem | 9.1 | 13 (A zdob.) | M12 (odsiecz pobita), M13 (odsiecz pobita) | - |
| 145 | Ghoyan Drohe | zamek | Pentos | 417 | 15 | 0.041 | szturm bez zdobycia, potem odstapione | 11.3 | 15 (brak bez) | - | - |
| 146 | Fellwood | zamek | Stormlands | 422 | 5 | 0.014 | wypad - pobity oblegajacy | - | - | W5 (pobity obl.) | - |
| 147 | Qamayl | zamek | Ibben | 428 | 6 | 0.016 | odstapione bez bitwy | - | - | - | - |
| 148 | Stone Hedge | zamek | House Baratheon of King's Landing | 430 | 12 | 0.033 | zdobyte szturmem | 7.8 | 12 (A zdob.) | - | - |
| 149 | Blackhaven | zamek | Stormlands | 432 | 4 | 0.011 | wypad - pobity oblegajacy | - | - | W4 (pobity obl.) | - |
| 150 | Tolos | miasto | House Targaryen, Aegon | 440 | 11 | 0.030 | okup (O1) | 10.9 | - | - | 11 |
| 151 | Rook's Rest | zamek | Dragonstone | 454 | 9 | 0.025 | szturm bez zdobycia, potem odstapione | 7.0 | 9 (brak bez) | - | - |
| 152 | Wayfarer's Rest | zamek | House Baratheon of King's Landing | 456 | 11 | 0.030 | zdobyte szturmem | 4.9 | 12 (A zdob.) | - | - |
| 153 | Sar Mell | zamek | Volantis | 458 | 14 | 0.038 | pod murami - pobity oblegajacy | 10.6 | - | M15 (pobity obl.) | - |
| 154 | Vaes Athjikhari | zamek | Dothraki Horde | 463 | 7 | 0.019 | okup (O1) | 7.6 | - | - | 8 |
| 155 | Maidenpool | miasto | Riverlands | 469 | 3 | 0.008 | odstapione bez bitwy | - | - | - | - |
| 156 | Acorn Hall | zamek | House Baratheon of King's Landing | 474 | 14 | 0.038 | zdobyte szturmem | 7.5 | 14 (A zdob.) | - | - |
| 157 | Vaes Leisi | zamek | Dothraki Horde | 477 | 8 | 0.022 | okup (O1) | 7.6 | - | - | 8 |
| 158 | Sar Mell | zamek | Volantis | 480 | 11 | 0.030 | wypad - pobity oblegajacy | - | - | W11 (pobity obl.) | - |
| 159 | Gelona | zamek | Myr | 487 | 7 | 0.019 | odstapione bez bitwy | - | - | - | - |
| 160 | Wayfarer's Rest | zamek | Riverlands | 489 | 3 | 0.008 | wypad - pobity oblegajacy | - | - | W3 (pobity obl.) | - |
| 161 | Pinkmaiden | zamek | House Baratheon of King's Landing | 495 | 10 | 0.027 | szturm bez zdobycia, potem odstapione | 5.7 | 11 (brak bez) | - | - |
| 162 | Willow Wood | zamek | House Baratheon of King's Landing | 497 | 1 | 0.003 | odstapione bez bitwy | - | - | - | - |
| 163 | Willow Wood | zamek | House Baratheon of King's Landing | 500 | 2 | 0.005 | odstapione bez bitwy | - | - | - | - |
| 164 | Banefort | zamek | House Baratheon of King's Landing | 515 | 15 | 0.041 | szturm bez zdobycia, potem odstapione | 8.4 | 16 (brak bez) | M14 (odsiecz pobita) | - |
| 165 | King's Landing | miasto | House Baratheon of King's Landing | 516 | 14 | 0.038 | zdobyte szturmem | 8.0 | 14 (A zdob.) | M14 (odsiecz pobita), M14 (odsiecz pobita) | - |
| 166 | Pinkmaiden | zamek | House Baratheon of King's Landing | 520 | 11 | 0.030 | zdobyte szturmem | 6.8 | 12 (A zdob.) | - | - |
| 167 | Tolos | miasto | House Targaryen, Aegon | 521 | 13 | 0.036 | zdobyte szturmem | 9.3 | 14 (A zdob.) | - | - |
| 168 | Tolerrol | zamek | Myr | 531 | 14 | 0.038 | pod murami - pobity oblegajacy | 12.3 | - | M5 (odsiecz pobita), M6 (odsiecz pobita), M15 (pobity obl.) | - |
| 169 | Rosby | zamek | House Baratheon of King's Landing | 532 | 8 | 0.022 | zdobyte szturmem | 5.7 | 9 (A zdob.) | - | - |
| 170 | Stoney Sept | miasto | House Baratheon of King's Landing | 535 | 14 | 0.038 | zdobyte szturmem | 6.9 | 14 (A zdob.) | - | - |
| 171 | Duskendale | miasto | House Baratheon of King's Landing | 543 | 16 | 0.044 | zdobyte szturmem | 8.3 | 16 (A zdob.) | - | - |
| 172 | Banefort | zamek | House Baratheon of King's Landing | 550 | 7 | 0.019 | odstapione bez bitwy | - | - | - | - |
| 173 | Deep Den | zamek | House Baratheon of King's Landing | 554 | 9 | 0.025 | wypad - pobity oblegajacy | - | - | W9 (pobity obl.) | - |
| 174 | Craster's Keep | zamek | Free Folk | 557 | 13 | 0.036 | zdobyte szturmem | 9.5 | 13 (A zdob.) | - | - |
| 175 | Banefort | zamek | House Baratheon of King's Landing | 561 | 9 | 0.025 | okup (O1) | 9.0 | - | - | 9 |
| 176 | Deep Den | zamek | House Baratheon of King's Landing | 566 | 11 | 0.030 | zdobyte szturmem | 8.2 | 12 (A zdob.) | M7 (odsiecz pobita), M7 (odsiecz pobita), M10 (odsiecz pobita), M11 (odsiecz pobita) | - |
| 177 | Tolerrol | zamek | Myr | 567 | 6 | 0.016 | wypad - pobity oblegajacy | - | - | W6 (pobity obl.), M5 (odsiecz pobita) | - |
| 178 | Lonely Light | miasto | Iron Islands | 579 | 6 | 0.016 | odstapione bez bitwy | - | - | - | - |
| 179 | Blacktyde | miasto | Iron Islands | 589 | 11 | 0.030 | szturm bez zdobycia, potem odstapione | 7.9 | 11 (brak bez) | - | - |
| 180 | Harvest Hall | zamek | Dragonstone | 595 | 7 | 0.019 | wypad - pobity oblegajacy | - | - | W7 (pobity obl.) | - |
| 181 | Lonely Light | miasto | Iron Islands | 598 | 13 | 0.036 | zdobyte szturmem | 9.0 | 14 (A zdob.) | - | - |
| 182 | Silverhill | zamek | House Baratheon of King's Landing | 603 | 12 | 0.033 | zdobyte szturmem | 7.1 | 12 (A zdob.) | M8 (odsiecz pobita), M11 (odsiecz pobita) | - |
| 183 | Bronzegate | zamek | House Baratheon of King's Landing | 605 | 2 | 0.005 | odstapione bez bitwy | - | - | - | - |
| 184 | Harvest Hall | zamek | Dragonstone | 605 | 11 | 0.030 | zdobyte szturmem | 6.6 | 12 (A zdob.) | - | - |
| 185 | Blackhaven | zamek | Dragonstone | 610 | 7 | 0.019 | zdobyte po wypadzie | - | - | W8 (wypad pobity) | - |
| 186 | Odivo | zamek | Myr | 615 | 13 | 0.036 | zdobyte szturmem | 9.7 | 13 (A zdob.) | - | - |
| 187 | Goldengrove | zamek | The Reach | 618 | 12 | 0.033 | pod murami - pobity oblegajacy | 7.7 | - | M12 (pobity obl.) | - |
| 188 | Goldentooth | zamek | House Baratheon of King's Landing | 622 | 8 | 0.022 | odstapione bez bitwy | - | - | M6 (odsiecz pobita), M8 (odsiecz pobita), M8 (odsiecz pobita) | - |
| 189 | Tolerrol | zamek | Myr | 624 | 16 | 0.044 | pod murami - pobity oblegajacy | 12.7 | - | M17 (pobity obl.) | - |
| 190 | Bronzegate | zamek | House Baratheon of King's Landing | 624 | 9 | 0.025 | okup (O1) | 9.2 | - | - | 9 |
| 191 | Silverhill | zamek | Riverlands | 633 | 8 | 0.022 | okup (O1) | 8.0 | - | - | 8 |
| 192 | Blackhaven | zamek | The Reach | 634 | 12 | 0.033 | zdobyte szturmem | 7.7 | 12 (A zdob.) | - | - |
| 193 | Wyl | zamek | The Reach | 650 | 4 | 0.011 | odstapione bez bitwy | - | - | - | - |
| 194 | Banefort | zamek | House Baratheon of King's Landing | 657 | 4 | 0.011 | odstapione bez bitwy | - | - | - | - |
| 195 | Wyl | zamek | The Reach | 657 | 2 | 0.005 | wypad - pobity oblegajacy | - | - | W2 (pobity obl.) | - |
| 196 | Blackhaven | zamek | Dragonstone | 660 | 3 | 0.008 | odstapione bez bitwy | - | - | - | - |
| 197 | Goldengrove | zamek | The Reach | 663 | 8 | 0.022 | odstapione bez bitwy | - | - | - | - |
| 198 | Blackhaven | zamek | Dragonstone | 666 | 2 | 0.005 | odstapione bez bitwy | - | - | - | - |
| 199 | Wyl | zamek | The Reach | 676 | 6 | 0.016 | wypad - pobity oblegajacy | - | - | W6 (pobity obl.) | - |
| 200 | Blackhaven | zamek | Dragonstone | 687 | 5 | 0.014 | wypad - pobity oblegajacy | - | - | W5 (pobity obl.) | - |
| 201 | Hornvale | miasto | House Baratheon of King's Landing | 700 | 6 | 0.016 | wypad - pobity oblegajacy | - | - | W6 (pobity obl.), M4 (odsiecz pobita), M4 (odsiecz pobita) | - |
| 202 | Bronzegate | zamek | House Baratheon of King's Landing | 702 | 6 | 0.016 | odstapione bez bitwy | - | - | - | - |
| 203 | Blackhaven | zamek | Dragonstone | 708 | 8 | 0.022 | wypad - pobity oblegajacy | - | - | W8 (pobity obl.) | - |
| 204 | Blackhaven | zamek | Dragonstone | 723 | 6 | 0.016 | trwa na koncu biegu | - | - | - | - |
| 205 | Silverhill | zamek | Riverlands | 723 | 6 | 0.016 | trwa na koncu biegu | - | - | - | - |
| 206 | Bronzegate | zamek | House Baratheon of King's Landing | 724 | 2 | 0.005 | odstapione bez bitwy | - | - | - | - |
