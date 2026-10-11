# Etap 5 - rozpoznanie A: liczby z logow (zbior 31, bieg A, 120 dob nowej kampanii)

Data: 11.10.2026. Tylko odczyt logow i CSV - kod niezmieniany, gra nieuruchamiana.

**Zrodla**
- Log: `scratchpad\kopia-z31-A\Armoury-2026-10-11_08-09-11.log` (31 756 linii). `Lnnnnn` = numer linii w tym pliku.
- CSV tego biegu: `Modules\Armoury\Logs\2026-10-11_08-09-11\` (`budzet-rodow.csv`, `kasy-miast.csv`, `ludnosc-bk.csv`) - tylko odczyt.
- Porownanie: bieg zimowy B `scratchpad\kopia-z31-B\Armoury-2026-10-11_08-50-00.log` (rozdz. 10).
- Skrypty i pelne szeregi: `scratchpad\e5A\parse.py` -> `serie-A.txt`, `serie-B.txt`; `csv.py` -> `csv-A.txt`; `csv2.py`.

**Oznaczenia.** Doba kampanii 1 = dzien gry 108836, doba 120 = 108955. **sr28** = srednia z dob 93-120 (28 linii danego rodzaju),
**d120** = ostatnia doba, **d40** = doba 40. Kwoty w zl na dobe, chyba ze napisano "stan". Linia raportu doby N bywa wypisana
na poczatku doby N+1 (np. "Korona: wplywy dnia ... dzien 108956"), wiec ostatnia linia niektorych rodzajow to "doba 121".
Rok = 364 doby (kalendarz Armoury); tu nic nie przeliczam na lata.

---

## 0. Najwazniejsze liczby wobec planu (PLAN rozdz. "Etap 5")

| Co | PLAN "dzis" / prog | Bieg A (zbior 31) | Dowod |
|---|---|---|---|
| "Zakupy" mieszkancow miast z niczego (5.1) | ok. 450 tys./d | **sr28 421 538** (min 371 181, max 486 615); d120 425 318; d2 334 824; d40 410 033 - **rosnie** (+27% od d2) | L31717 (`"zakupy" mieszkancow miast i zamkow 425318`), L784, L10660 |
| ... w zamkach | - | 0 z niczego (110 blokuje); towar **zjedzony bez zaplaty** sr28 50 105 (d120 56 923) | L31541 (`zloto z niczego cofniete 56923`) |
| Regulator kas miast: kasowanie / dosypka | 5.1, 5.8 | kasowanie **sr28 295 208** (d120 248 065), dosypka **sr28 17 057** (+ zamki 953) | L31686, L31723 |
| Pan bierze z gotowki wsi | ok. 85% (cel 30-50%) | **86%** (licznik podatku + renta z kiesy, wobec calej gotowki wplywajacej do wsi); z majatkami BK 95%, z korona 97%; wies zatrzymuje ok. 0 | rozdz. 4 |
| Udzial panow samych zamkow w stalym dochodzie rodow (D staly) | dzis 24%, cel 30-35% | **24.3%** sr28 (d1 33.6%, d40 27.4%) | `budzet-rodow.csv`, rodzaj=zamek |
| Zatkane magazyny wsi zamkowych | 5.6%, nie wiecej | sr28 **4.9%** (12.8 z 260; min 9, max 16 = 6.2%), d120 5.4% (14/260), srednia biegu 3.8% | L31610 |
| Sakiewki zolnierzy | najwyzej 15 dni zoldu | **ok. 29 dni** naliczonego zoldu partii i zalog (22.55 mln / 768.9 tys.), ok. 35 dni tego, co wplywa do sakiewek; stan wciaz rosnie | L31673, L31721 |
| Zloto swiata, zmiana | < 0.1% na 28 dob | **sr28 +662 448/d** (= ok. +0.28%/d, ok. 7.8% na 28 dob); w tym statki NavalDLC sr28 429 987/d (3.3 wylaczone do etapu 5) | L31716, L31718 |
| Czy ludzi w miastach stac na jedzenie (5.13) | 95% / 98% | **nie da sie policzyc** - lud nie ma kiesy; 100% "zakupow" miast to zloto z niczego, w zamkach 100% zjedzone bez zaplaty | rozdz. 9 |

---

## 1. Ksiega pieniadza swiata (stan i bilans)

Stan d120 (L31716, "Pieniadz swiata: dzien 108955"): razem **238 394 240** (+561 430). Kasy miast 12 691 304, kasy zamkow 3 862 570,
kiesy wsi 482 792, glowy rodow 98 191 819, pozostali lordowie 11 610 693, notable 26 581 815, skarbce krolestw 48 805 743, kapital warsztatow
2 607 051, sakiewki ludzi 22 511 916, Bank 3 683 442, tabory wsi 657 828, karawany 4 705 832, garnizony 1 416 621, bandy 249 445;
liczniki w drodze do panow: cla 107 512, podatek wsi 167 276.

Ruch w biegu: razem 186.8 mln (d2, L783) -> 238.4 mln (d120): +51.6 mln. Glowy rodow 52.5 -> 98.2 mln, sakiewki ludzi 0.16 -> 22.5 mln,
skarbce 57.9 -> 48.8 mln (ok. -76 tys./d), kasy miast 17.0 (start) -> 9.9 (d40) -> 12.7 mln, notable 29.3 -> 26.6 mln (L538, L31716).

Bilans dobowy, sr28 (linia "Pieniadz swiata (bilans)", d120 L31717):

| Pozycja | sr28 | d120 |
|---|---|---|
| ZRODLA z niczego razem | 1 941 079 | 1 889 171 |
| - "zakupy" mieszkancow miast i zamkow | 421 538 | 425 318 |
| - regulator kas dosypal | 18 010 | 20 920 |
| - zold oddany do obiegu przez SoldierPay (sakiewki + kasy osad) | 747 139 | 775 035 |
| - GiveGoldAction z niczego poza rozliczeniami rodow | 754 390 | 667 898 |
| &nbsp;&nbsp;w tym statki NavalDLC inne / sprzedaz statkow osadom (L31718) | 429 987 / 18 298 | 370 870 / 12 808 |
| &nbsp;&nbsp;w tym bitwy | 89 976 | 85 762 |
| &nbsp;&nbsp;w tym wyplata dochodu notablom (minus przelew z kapitalu 114 008) | 120 934 | 127 963 |
| &nbsp;&nbsp;w tym pasmo notabli (dosypka) | 93 571 | 69 000 |
| &nbsp;&nbsp;w tym kapital nowych karawan | 69 821 | 30 000 |
| UJSCIA w nicosc razem | 1 203 424 | 1 164 208 |
| - rozliczenia rodow na minus (w tym zold naliczony 755 406) | 752 306 | 779 099 |
| - regulator kas skasowal | 295 208 | 248 065 |
| - GiveGoldAction w nicosc poza rozliczeniami (pasmo notabli 132 804, werbunek od notabli 26 489 ...) | 189 845 | 168 232 |
| reszta zgrubna [R] | -75 208 | -163 533 |
| **zmiana sumy** | **+662 448** | **+561 430** |

Reszta po rozbiciu przyczyn: d120 **-507** (L31718 `reszta -507 [R]`), sr28 -1 302 (narzedzie progow etap3, `progi-etap3.txt`).
SoldierPay (747 tys.) i rozliczenia rodow na minus (752 tys.) to ten sam zold - znosza sie.
**Netto "z niczego" samych kas miast** = zakupy 421.5 - kasowanie 295.2 + dosypka 17.1 = **ok. +143 tys./d** (regulator kasuje ok. 70% tego, co tworza "zakupy").

## 2. Kasy miast (97 miast)

Linia "Kasy miast (169c)" d120 L31686: kasy **12 564 644** wobec celu regulatora (10 000 + 12 x dobrobyt) **7 222 435**; kasa/cel mediana 1.48,
ponad celem 82, ponizej 15, ponizej polowy 0; regulator dzis skasowal 248 065, dosypal 20 220; pod tarcza zoldu 2 863 805; **zawor do pana 512 470**.
`kasy-miast.csv` d120: kasa/cel min 0.77, max 6.55; najwiekszy zawor King's Landing 27 598, Menetragos 26 177, Highgarden 22 630.
Kasy na starcie 17.4 mln (L505), d40 9.76 mln (L10629), sr28 11.90 mln; cel 6.55 -> 7.22 mln (rosnie z dobrobytem).

Przeplywy kas miast, sr28 (linia "Przeplywy osad (kasy miast)", d120 L31722; przyczyny reszty L31725):

| Wplywy | sr28 | Wyplywy | sr28 |
|---|---|---|---|
| **"zakupy" mieszkancow (z niczego)** | **421 538** | **zawor do panow (renty miast)** | **487 312** |
| sakiewki ludzi - zycie w miastach | 384 239 | zaplata taborom wsi (plon) | 283 555 |
| zakupy sprzetu AI | 177 306 | regulator netto (skasowal 295 208 - dosypal 17 057) | 278 151 |
| budzet rodow 166 (dwor do kas siedzib) | 140 961 | warsztaty BK - kasa placi za wyroby (L31725) | 153 450 |
| warsztaty towarowe - nadwyzka ceny wyrobow | 74 608 | warsztaty zbrojne (zakup wsadu) | 131 132 |
| zold garnizonow do kas | 71 276 | korona (danina wojenna, clo, mennica) | 90 443 |
| kontrakty surowca (174) | 54 004 | GiveGold: karawany | 230 880 |
| warsztaty zbrojne (sprzedaz) | 50 016 | GiveGold: lordowie | 86 520 |
| wywoz kupcow (PodazPopyt) | 40 093 | prowizja od sprzedazy partiom (do licznika cel) | 29 111 |
| odziez wojska | 25 108 | BK rynek osady | 15 085 |
| bandy i kryjowki (zycie w miastach) | 22 789 | paser band (skup lupu) | 11 458 |
| najemnicy z karczmy | 19 851 | | |
| rzemieslnicy BK (147) | 19 236 | | |
| warsztaty placa za wsad (BK) | 22 533 | | |
| budowy | 11 389 | | |
| warsztaty towarowe - place i utrzymanie | 4 863 | | |
| GiveGold: karawany / lordowie / notable | 127 823 / 102 218 / 51 684 | | |
| **zmiana kas sr28** | **+54 237** | reszta po rozbiciu przyczyn (prog 10 000: OK) | +1 966 |

**"Nadwyzka kupcow"** (kasa ponad zapas kupcow = cel regulatora): sr28 **4 951 608** w ok. 85 miastach (d120 5 587 094 w 82, L31726);
zawor do pana = 7% kasy ponad 20 000 dziennie (`Settings.TownRentShare 0.07`, `TownRentFloorGold 20000`, `PopulationLaw.cs:1184-1191`),
ograniczony renta nalezna i dekretem BK: sr28 487 312 = ok. 9.8% nadwyzki dziennie. Linia "Ludnosc:" (L31540): renty zaplacone sr28 602 964
(zawor miast + renta wsi) z "naleznych" 5 723 057 (10.5%).

## 3. Kasy zamkow (130 zamkow)

"Zawor zamkow (110)" d120 L31541: stan 3 857 733, zapas kupcow 3 494 158, ponad zapasem 365 659; kasowanie regulatora zablokowane sr28 99 288;
dosypka (tryb 1) sr28 953; **"zakupy" ludnosci zamkow: zloto z niczego cofniete sr28 50 105** (d120 56 923; d1 16 968) - "towar zjedzony jak dotad";
zawor do panow sr28 18 600 (d120 17 430) + korona (114) sr28 9 163; pan samych zamkow z zaworu: srednio sr28 153, mediana 103 zl/d.
Przeplywy (L31723, sr28): zold garnizonow do kas +32 985, budzet rodow 166 +44 478, wywoz kupcow -40 093, zmiana +5 941.

## 4. Wsie: utarg, podzial, kiesy, renta

Utarg taborow wsi (linia "Przeplywy osad", d120 L31721; sr28): miasta zaplacily **283 555** (86.7 wizyt), zamki 1 977.
Powrot do wsi: tabory oddaly **270 562** = pan (licznik podatku wsi) **159 626 (59.0%)** + wlasciciele majatkow BK **29 731 (11%)** + kiesa wsi **81 205 (30%)** + zniklo 0.
"Utarg wsi (112)" L31737: zniklo 0, nieprzypisane 0; towar kupiony we wsi d120 9 149 - oddany kiesom wsi.

Kiesy wsi (571 wsi, "Przeplywy osad (kiesy wsi)", d120 L31724; sr28): stan **548 197** (d120 482 792); wplywy: utarg 81 205, przelewy gry 14 452,
sakiewki zwolnionych (166) 34 590; wyplywy: **renta do panow 115 754** (20% kiesy dziennie, `PopulationRentMaxShare 0.2`), korona 6 324, reszta -7 297 (BK rynek wsi);
zmiana **+872/d**. Mediana kiesy wsi sr28 **276 zl**, ponizej 1 000 zl: **436 z 571** wsi (d120 447, L31724).

**Ile gotowki bierze pan (sr28):** cala gotowka wplywajaca do wsi = utarg 270 562 + przelewy 14 452 + sakiewki zwolnionych 34 590 = 319 604.
Pan: licznik 159 626 + renta 115 754 = 275 380 = **86.2%**; z majatkami BK (+29 731) 95.5%; z korona z kies (+6 324) 97.4%.
Renta z kiesy (115.8 tys.) to 142% tego, co wies dostaje z utargu (81.2 tys.) - kiesy stoja tylko dzieki przelewom gry i sakiewkom zwolnionych.
(Liczba PLAN "85%" pochodzi z audytu 01 sprzed paczki 112: 70% licznik + renta, 15% w nicosc; dzis nic nie znika, ale renta zabiera reszte.)

## 5. Panowie: dochody, D staly, panowie samych zamkow

"Obieg" d120 L31726 i sr28 - **rody dostaly**: zawor miast **487 312**, renta wsi **115 652**, zawor zamkow 18 600, zwrot zoldu od korony **225 212**,
renty korony (180) 80 164, trzecia lordow z nadwyzek ludzi 59 864 (d120 152 019), wplywy spoza rodu poza rozliczeniem 636 726 (w tym z niczego 539 397 - statki, bitwy),
dochod modelu gry (do D) 231 967. **Rody wydaly**: dwor do kas siedzib 185 439, zold (rozdz. 7), powinnosci 14 755, budowy 15 957.

"D staly (169c)" d120 L31683: rody AI 318, D staly razem **1 071 679** (sr28 1 034 996), D169 razem 2 068 182; czesci (swiat, srednio): ziemia 736 188
(podatek wsi BK 120 423, renta wsi i zawor miast 577 405, cla i podatek miast 23 281; zawor zamkow 15 079), korona 319 631, kontrakt 15 838.

**Panowie samych zamkow** (120 rodow, `budzet-rodow.csv`, rodzaj=zamek; sr28 na dobe, sumy):
- D staly 251 371 = ziemia 128 923 + korona 122 447; wszystkie rody AI 1 035 k -> **udzial 24.3%** (d1 33.6%, d10 29.9%, d40 27.4%, d80 24.1%, d120 24.3%).
- Udzial w samej "ziemi" (d_ziemia): **18.0%** (pan miasta: ziemia 586 695 - glownie zawor miast).
- Udzial w D (dochod 28 dob z rentami i wplywami): 30.1%.
- D staly mediana sr28 **1 775** (d120 1 865; prog testu 400-1300 -> NIE w `progi-etap2.txt`), D169 mediana 3 684 (L31683).
- Mediana kiesy glowy: okno sr28 213 318 -> 207 920 (**-2.5%**); bloki 28 dob: d1-28 -2.8%, d29-56 +12.3%, d57-84 +13.5%, d85-112 -3.2% (prog etapu 5: spadek <= 10% na 28 dob wojny).

## 6. Korona

"Korona: wplywy dnia (165)" (ostatnia L31642, sr28): wplywy dnia **256 287** (nasze: powinnosci, danina wojenna, clo, 1/3 zaworu zamkow, 1/9 prawa trzecich 170 699;
inne - gra, BK (podatek BK 59 260), Diplomacy 72 619) + 1/360 zapasu ponad 500 000: 98 433. Wydane: **zwrot zoldu 225 212** (100% naleznego), renty (180) 80 164
(wstrzymane w skarbcach 33 120), dary 14 233, kontrakty najemnikow 14 996, rata Banku ok. 6.8 tys. (L31642).
Skladniki (L31632-31633, sr28): **danina wojenna z kas osad 96 763** (miasta ok. 90.4 tys., wsie 6.3 tys.), clo 12 341, powinnosci wasali 14 755, udzial z zaworu zamkow 9 163.
Skarbce: 57.9 mln (d1) -> 48.8 mln (d120), d120 -75 605 (L31716). (5.6 "podatek wojenny ok. 100 tys./d" - dzisiejsza danina wojenna z kas osad jest juz w tej skali.)

## 7. Zolnierze: zold i sakiewki

Zold naliczony w rozliczeniach rodow (L31721, sr28): partie rodow **536 318**, garnizony **204 705**, karawany lordow 14 383 = **755 406**.
Do sakiewek ludzi 642 879 (partie 528 310, zalogi 100 427, karawany lordow 14 142 - L31673), do kas osad 104 260 (miasta 71 276, zamki 32 985).
Ludzie (L31742, d120): partie rodow 110 407, garnizony 81 233, milicje 67 309, karawany 32 405.

Sakiewki ludzi (L31726 "Obieg", d120): stan **22 511 916** (-233 686), zold wplynal 653 984, wydaly w miastach na zycie **689 127** (sr28 384 239 - bardzo zmienne: 245 883-867 139),
naprawy 9 270, z rozbitych partii do zwyciezcow 395 257 (sr28 172 793), ze zwolnionymi do kies wsi 21 725; na sprzet 0 (L31505). Stan: d1 0.16 mln, d40 7.5 mln,
sr28 21.1 mln (min 19.1, max 22.8), najwieksza 483 708 (L31397). **Sakiewki = 29.3 dni naliczonego zoldu partii i zalog** (22.55 mln / 768.9 tys. z d120) albo 35 dni tego,
co do nich wplywa; PLAN: najwyzej 15 dni. (Linia "Sakiewka ludzi" L31505 podaje "zold wplacony do sakiewek 841 718" - inne okno niz ksiega; do liczenia brac "Obieg".)

## 8. Karawany, warsztaty, notable

**Karawany:** kiesy karawan sr28 4 669 841 (L31716). Kasy miast: od karawan +127 823, do karawan -230 880 (netto ok. -103 tys./d z kas miast do karawan).
Karawany notabli (L31727): 914 karawan u 322 notabli, zold nalezny sr28 **271 452**, "zold w nicosc 0", niedoplata 88 123, doplaty notabli do 5 000 +80 192 (przelew);
napis w linii: "po planowanej zmianie: zold do sakiewek ludzi karawan". Nowe karawany - kapital z niczego sr28 69 821 (d120 30 000, 3 karawany).
Karawany lordow: zold 14 383 -> sakiewki ludzi karawan. Handel surowcem (L31626, d120): miasta zaplacily 4 370, karawany 3 415 - drobne.

**Warsztaty zbrojne** ("Warsztaty", L31497): sr28 1 671 szt./d, koszt 49 968, sprzedaz 131 117 (d120 1 782 szt., 76 091 / 200 268).
**Warsztaty towarowe** (291, L31517): wynik po placach i utrzymaniu sr28 +10 346 (d120 +19 827); place i utrzymanie do kas miast 4 863; nadwyzka ceny wyrobow do kas miast 74 608;
warsztaty BK: kasa placi za wyroby -153 450, wsad +22 533 (L31725). Kapital warsztatow 2.57 mln. Rzemieslnicy BK (147) do kas 19 236.

**Notable** (L31727, sr28): kiesy 26 814 158 (29.3 mln d1 -> 26.6 mln); pasmo 4 500-10 500: w nicosc -132 804, dosypka +93 571; dochod z aktywow +120 934
(z kapitalu -114 008, netto z niczego +6 927); werbunek od notabli w nicosc 26 489 (L31718). Majatki BK (L31735): udzial z utargu 18-27 tys./d, 2 379 notabli wiejskich.

## 9. Ludzie w miastach i jedzenie

- **Ksiega ludzi (E4/4):** miasta **10 471 333** - stale przez 120 dob (L515 = L31696); wsie 41 557 228. Wedlug dobrobytu x k byloby 11 607 364 (L31739, tylko informacja).
- **Ludnosc BK** (`ludnosc-bk.csv`, L31689): miasta **1 515 755** (d1 1 556 966, **-2.6%**): szlachta 1 079, rzemieslnicy 119 192, chlopi 779 127, niewolni 143 349, dzierzawcy 473 008;
  zamki 421 971 (+16.8%), wsie 965 848 (-8.3%); swiat 2 903 574.
- **Konsumpcja miast:** budzet gry (popyt kategorii) 795 180 zl/d (d1 676 513), zuzycie BK (ConsumedValue) **425 318 = dokladnie "zakupy" z niczego**
  (0.281 zl na glowe BK na dobe; d1 0.190). Zamki: budzet 210 761, zuzycie 56 923 = "cofniete" (zjedzone bez zaplaty).
  **"Zakupy" rosna z dobrobytem (+44% od d1 w CSV), choc ludzi BK w miastach ubywa** - zgodne z PROJEKT-KIESA L3.
- **Jedzenie mieszczan** (linie "Towary", d120): zboze 6 401 (L31564), ryby 1 681 (L31560), chleb 815 (L31551), mieso 239 (L31577) szt./d.
- **Kto placi:** nikt z ludu - kiesy ludu nie ma. 100% konsumpcji miast to zloto z niczego (do kas kupcow), w zamkach 100% zjedzone bez zaplaty (prog etapu 5: < 1%).
  Wszystkie dzisiejsze strumienie "za prace i uslugi" ida do kas miast, nie do ludu: zycie zolnierzy 384 tys., nadwyzka ceny wyrobow 75 tys., place warsztatow 5 tys.
- **Czy stac:** nie da sie policzyc z dzisiejszego logu (brak linii 5.13 i dochodu ludu). Glodu w warowniach brak: glodne 0, mediana zapasu 302 dni (L31742).
- Miasta kupuja plon: towar niesprzedany po wizycie taboru - 824 szt. w 29 wizytach przez 119 dob (`Przeplywy osad`), przy ok. 9 tys. szt. zboza/d dowozu (L31564) - ponizej 0.1%.

## 10. Bieg zimowy B (dla porownania, sr28 dob 93-120, `serie-B.txt`)

"Zakupy" z niczego 340 540 (d120 317 160, L32006); kasowanie 271 384, dosypka 15 278; zawor miast 476 386; pan 59.0% utargu; renta wsi 141 297;
kiesy wsi 757 058 (mediana 530, ponizej 1 000 zl 389 z 571); zamki zjedzone bez zaplaty 40 371; zatkane wsie zamkowe 16.8/260 = **6.5%** (d120 13 = 5.0%);
D staly panow zamkow mediana 2 114; danina wojenna 97 304; sakiewki 21.5 mln; zloto swiata +481 516/d (statki 308 082).

## 11. Uwagi i ograniczenia

1. Udzial panow zamkow i kiesy liczone z CSV (rodzaj=zamek = 120-122 rody "samych zamkow"); 24.3% zgadza sie z PLAN (T9 24%).
2. "Pan bierze 86%" - moja definicja (licznik + renta / cala gotowka wplywajaca do wsi); innej liczby log nie podaje wprost.
3. Zatkane magazyny: ten sam pomiar co PLAN ("Dowoz (skutki)": wsie zamkowe X z 260); PLAN 5.6% miesci sie w rozrzucie dziennym A (3.5-6.2% w oknie).
4. Zold karawan notabli: log mowi "w nicosc 0" przy naleznym 271 tys./d - gdzie idzie (czy w ogole schodzi z kas), trzeba sprawdzic w kodzie przed 5.5 (`CirculationWindows.cs:670-716`).
5. Linia "Sakiewka ludzi" i "Obieg" licza zold i zycie w miastach w innych oknach (841 718 / 578 745 wobec 653 984 / 689 127 w d120) - w projekcie brac "Obieg"/"Przeplywy osad".
6. Ludnosc miast w ksiedze E4/4 stoi (10 471 333 cale 120 dob); ludnosc BK miast spada 2.6% - dwie skale (PROJEKT-KIESA L9).
7. CrashScribe `session-2026-10-11_08-09-10.log` L3793 (EKONOMIA d120): krol - kiesa rodziny mediana 539 204, rod krolestwa 166 661 - bez danych o kasach miast i ludzie.
