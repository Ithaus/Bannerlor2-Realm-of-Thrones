
## 4. Zalogi wobec gry bez modow

Biegu "bez modow" (ani ROT bez naszych modow) nie ma w logach - porownanie tylko z kodem gry i ze stanem startowym ROT (d1).

| Co | Gra 1.4.8 (kod) | fin30 (pomiar) |
|---|---|---|
| miejsca w zalodze | 200 + 200 w miescie + Leadership + perki + budynki [K] `dec32\...\GameComponents\DefaultPartySizeLimitModel.cs:172-180` | zaloga na poczatku oblezenia: zamki 254, miasta 517 (mediany) - powyzej podstawy gry |
| werbunek zalogi z notabli | najwyzej 1 / dobe [K] `...\DefaultSettlementGarrisonModel.cs:38-43` | K8: 2 / dobe [P] A, linie "Wojsko z ludzi (E4/8)" ("K8: limit 2/d"); [K] `zb\int\Armoury\src\Settings.cs:919`; dobrano ponad 1/d razem 60 152 ludzi (suma linii E4/8); werbunek gry od notabli: rok 1 75 257, rok 2 47 784 [P] A, "Zalogi: przyrost" |
| zaloga na starcie | z ROT / gry | d1: 47 235 (miasta 30 759, zamki 16 476); zdobyte zamki 125, miasta 328 (mediany) |
| szczyt / koniec | - | 92 002 (d361) / 65 611 (d728) |
| ubytek z budzetu rodow | brak takiej reguly w grze (gra: dezercja przy niezaplaconym zoldzie) | zwolnieni z zalog: rok 1 5 357, rok 2 49 186 (rozdz. 2.3) |
| decyzja o szturmie | sila oblegajacych / (sila obroncow x przewaga murow^wykladnik), potem losowanie 10 % wyniku [K] `dec32\...\Siege\BesiegerCamp.cs:375-430` | przy decyzji sila 5.7 : 1 (mediana 113 decyzji); szturm po 8.4 doby (mediana) |
| bitwa pod murami | szturm zmienia sie w SiegeOutside, gdy dolacza obronca spoza murow [K] `MapEvent.cs:651-654` | 11 z 57 zdobyc poprzedzila wygrana SiegeOutside (rozdz. 3.2) |
| przegrani pod murami | gra: zabici / ranni / jency | oblegajacy pobity: 70 % rozbitych (N3); strona twierdzy: jak w grze [P] A "Przegrani pod murami (N3)" |
| glod w oblezeniu | gra: brak zywnosci zabija zaloge | 0 dob glodu w 57 zdobytych twierdzach [P] |
| ostrzal | kazde trafienie machiny w ludzi: 1 (+1 z perkiem Terror) zabity [K] `...\DefaultSiegeEventModel.cs:255-263` | nie mierzone; Krolewska Przystan: zaloga 541 (d523) -> 271 (d529) w czasie oblezenia bez glodu i bez bitwy w polu tych dob - przyczyna nie zmierzona (rozdz. 9) |

Wniosek z liczb: zalogi zdobytych twierdz byly na poczatku oblezenia **wieksze** niz podstawa miejsc w grze (200 / 400) i dwa razy wieksze niz na starcie ROT [P + K]. Przewaga oblegajacych bierze sie z wielkosci armii (13-16 druzyn), nie z malych zalog. K8 dodal zalogom 60 tys. ludzi ponad tempo gry - bez niego zalogi bylyby mniejsze [S].

## 5. Nasze reguly a sila krolestw

### 5.1 Doplaty z korony i pobor: W5, W5b, W6, W7

[P] T `budzet-rodow.csv`, suma dzienna kolumn doplata_w5, doplata_ze_skarbca_w5b, doplata_z_tarczowego_w6, pobor_wojenny_w7, zold_partii za caly bieg; wyliczenie `doplaty-wobec-zoldu.tsv`.

| krolestwo | zold druzyn (zl) | W5 | W5b | W6 | W7 | razem jako % zoldu druzyn | dni rodow w wojnie |
|---|---|---|---|---|---|---|---|
| Polnoc | 48.1 mln | 5.51 mln | 0.03 | 0.06 | 0.21 | 12.1 % | 99 % |
| Baratheon KP | 34.5 mln | 1.88 | 0.02 | 0.87 | 0.42 | 9.3 % | 99 % |
| Reach | 31.2 mln | 0.83 | 0.09 | 0.02 | 0.01 | 3.0 % | 84 % |
| Dorzecze | 30.2 mln | 0.57 | 0.07 | 0.00 | 0 | 2.1 % | 96 % |
| Dolina | 22.4 mln | 3.43 | 0.42 | 0.01 | 0.01 | 17.3 % | 57 % |
| Smocza Skala | 22.2 mln | 1.94 | 0.11 | 0.07 | 1.37 | 15.7 % | 89 % |
| Targaryen (Dany) | 20.4 mln | 2.71 | 0.76 | 0.02 | 0.06 | 17.4 % | 74 % |
| Stormlands | 18.9 mln | 2.28 | 0.03 | 0.00 | 0 | 12.2 % | 100 % |
| Zelazne Wyspy | 18.3 mln | 5.47 | 0.37 | 0.01 | 3.62 | 51.9 % | 97 % |
| Dorne | 15.8 mln | 2.58 | 0.06 | 0.07 | 0.31 | 19.1 % | 70 % |
| Wolni Ludzie | 8.1 mln | 3.09 | 0.35 | 0.02 | 2.47 | 73.4 % | 100 % |

- Polnoc dostala najwiecej W5 w zlocie (5.51 mln, razem z Zelaznymi Wyspami), ale wzgledem wlasnego zoldu (12 %) mniej niz Dorne (19 %), Dolina, Targaryen czy Wyspy. Doplaty ida do biednych krolestw - to nie one robia Polnoc najsilniejsza [P].

### 5.2 Zloto startowe wedlug ludzi (GL2)

Na starcie skarbce koron, kasy kupcow i kiesy ludu przelano wedlug liczby ludzi krolestwa [P] A, linia "Zloto startowe wedlug ludzi (GL2): dzien 108837" (przed -> po):

| krolestwo | skarbiec korony | kasy kupcow miast |
|---|---|---|
| Reach | 2.00 -> 7.80 mln | 2.03 -> 4.61 mln |
| Baratheon KP | 2.01 -> 4.95 | 2.49 -> 2.81 |
| Polnoc | 2.00 -> 2.93 | 2.72 -> 1.61 |
| Dorzecze | 2.00 -> 2.95 | 2.04 -> 1.60 |
| Dolina | 2.00 -> 2.93 | 1.58 -> 1.65 |
| Stormlands | 2.00 -> 2.44 | 1.47 -> 1.33 |
| Targaryen (Dany) | 2.00 -> 1.96 | 1.06 -> 1.15 |
| Dorne | 2.00 -> 1.46 | 1.77 -> 0.80 |

Renty korony za caly bieg (suma kolumny renty): Reach 25.3 mln, Polnoc 17.6, Dorzecze 16.7, Baratheon KP 16.1, Dolina 14.3, Smocza Skala 9.9, Targaryen 9.5, Stormlands 7.5, Zelazne Wyspy 7.5, Dorne 6.4 [P] T `budzet-rodow.csv`. Reach ma od startu 5.3 razy wiekszy skarbiec niz Dorne, z ktorym walczy.

### 5.3 Pobor i milicja wedlug ludzi (GL4)

Przyrost milicji i jej limit mnozone przez "czynnik osady" z liczby ludzi (0.1-3, srednia swiata 1.0) [K] `zb\int\Armoury\src\PeopleLevy.cs:13-36`. Sredni czynnik milicji krolestwa [P] A, linie "Pobor i milicja od ludzi wedlug krolestw (GL4)" d1 / d729: Reach x2.12 / x1.56, Baratheon KP x1.08 / x1.31, Dolina x1.10 / x1.10, Dorzecze x0.89 / x0.96, Stormlands x0.94 / -, Smocza Skala x0.42 / x0.67, Polnoc x0.58 / x0.52, Dorne x0.45 / x0.44, Zelazne Wyspy x0.37 / x0.37. Skutek w twierdzach - rozdz. 2.1 (milicja\* na twierdze: Reach ok. 3 razy Dorne).

### 5.4 Przewaga Polnocy (175.4b, 10 %)

- Dziala tylko w autobitwie w polu, na sniegu i w lasach Polnocy, dla piechoty Polnocy tier 3-6, "never at sieges" [K] `zb\int\Armoury\src\Settings.cs:1509`, `NorthHomeEdge.cs:16`; w fin30 wlaczona 10 % [P] A, linia "Armie 175 (Armoury) - wylaczniki".
- Za caly bieg: 42 bitwy z przewaga, straty wlasne 361, wroga 703 [P] T `balans-krolestw.csv`, battania d729. Polnoc wygrala 21 z 59 bitew lordow (36 %) [P] tamze. Ta regula nie robi z Polnocy potegi; jej zdobycze to szturmy armii Robba (rozdz. 6.1).

### 5.5 Przegrani w polu (H3) i rozbici (N7)

- Bitwy w polu za caly bieg: przegranych 744 956 ludzi; zabici 151 805 (gra dalaby 399 140), jency 50 932 (gra ok. 343 484), rozbici 542 219 [P] A, suma linii "Przegrani (H3)".
- Rozbici z druzyn lordow: 296 739, z tego pod choragiew 165 651, do domu 79 786, do lasu 51 302 [P] A, suma linii "Rozbici wracaja (N7)".
- Pod murami - rozdz. 3.2: pobity oblegajacy w 70 % rozbity, strona twierdzy jak w grze.
- Wplyw na sile: armie po przegranej w polu wracaja (ok. 56 % rozbitych pod choragiew), a zaloga po przegranej pod murami nie - asymetria jest zapisana w samej linii N3: "poza regula - pobita strona twierdzy (jak w grze)" [P] A, linie "Przegrani pod murami (N3)". Czy to rozstrzyga wojny - nie mierzone [S].

