# SUCHO 85 - MODEL OD DOLU, ROD PO RODZIE (rachunek na papierze, 13.10)

Katalog: `...\c36b73ca-...\scratchpad\sucho85\rody\`. Nic w grze, kodzie moda, ustawieniach ani w folderze gry nie zmienione; bez commita.
Miara wszedzie ta sama co prog Jeffa: suma "wojsko rodow" z linii `Skarbce:` krolestw w stanie WOJNA, srednia 28 dob konca okna (jak `sprawdz_logi.py` blok W5-7).
[P] = pomiar (log, CSV, kod), [S] = szacunek modelu. Liczby wojska w tysiacach ludzi.

## 0. Wynik w 10 liniach

1. Dzis (kod WGRANIA 43) na koniec roku: **S0 = 87.6-94.6 tys., srodek 92.0** [S] (bieg roczny W37 bez daniny/W5/W6 mial 79.6 [P]; model doklada P1 + W5 + W5b + W6).
2. Pierwszy krok reguly przez caly rok: **S1 = 87.3-94.1, srodek 91.7** [S] - prawie bez zmiany wobec S0 (-0.4 tys.).
3. Granica (D krolestw w pelni wedlug ludzi): **S2 = 82.6-92.5, srodek 87.7** [S] - ok. -4.4 tys.; 2 z 18 wariantow ponizej 85.
4. Nowa kampania ze zlotem startowym wedlug ludzi (pyt. 7), polowa drogi do granicy: **S3 = 85.8-93.9, srodek 90.8** [S].
5. Nakladka P4 (pobor i milicja wedlug ludzi): **+0.5 (S0) do +2.0 tys. (S2)** [S] - przesuwa ludzi z pustych krain do ludnych, suma lekko w gore.
6. Do tego los kampanii: rozne kampanie tego samego kodu roznia sie o +-5 tys. [P] - wieksze niz roznica S0-S1.
7. Swiat utrzyma 85 tys. w S0, S1 i S3 we wszystkich wariantach modelu; w granicy (S2) jest na styk (srodek 87.7, dol 82.6).
8. Traca: Polnoc (-2.9 tys. w S2), Zelazne Wyspy (-1.2), Dolina (-1.0), Dorne (-1.0), Dothrakowie (-1.0); zyskuja: Volantis (+1.4), Reach (+0.9), Norvos, Qohor, Pentos (+0.4-0.7 kazde).
9. Zyskujacy zyskuja malo, bo pieniadz trafia do rodow, ktore juz maja luz (Tyrell, Qarth, Braavos, King's Landing stoja na sufitie wypelnienia), a tracacy traca od razu, bo zalogi sa placone pierwsze.
10. Nie wiem: ile daniny P1 bedzie na koniec roku, czy rody z doplata W5/W6 naprawde werbuja w dane miejsce, jak szybko pieniadz dojdzie do granicy - te trzy rzeczy robia wiekszosc widelek; bieg roczny W43 (w toku) zmierzy S0 wprost.

## 1. Wzor pulapu z kodu i ile ludzi z niego wychodzi

Kod: galaz w-toku/zbior-e8 (WGRANIE 43), `...\ae222cb7-...\scratchpad\zb\int\Armoury\src`; gra: dekompilacja `...\ae222cb7-...\scratchpad\dec32\cs_full`.

| krok | wzor | plik:linia |
|---|---|---|
| D rodu | D staly = ziemia + korona + kontrakt + majatek; u najezdzcow (Wyspy, Dothrakowie, Wolni Ludzie) + lup; pomiar < 28 dob mieszany z D z zapisu; start D = G/60 | ClanBudget.cs:327-344, :54, :338; ClanIncomeBook.StableD.cs:570-581 |
| G, dorosli | G = kiesy rodziny; dorosli czlonkowie | ClanBudget.cs:403-404, :318-325 |
| rezerwa | R = max(20 000; 20 x D) + 5 000 x doroslych | ClanBudget.cs:197; Settings.cs:1045-1047 |
| pulap w wojnie | 0.60 x D x f + 0.8 x (G - R) / 45 przy G > R; przy G <= R f = 0.8 + 0.2 G/R, przy G < 0.25 R f = 0.5 | ClanBudget.cs:199-207; Settings.cs:1036, 1041-1042, 1048-1049 |
| pulap w pokoju | 0.28 x D x f, f = 1 - 0.1 (1 - G/60D) przy G < 60 D | ClanBudget.cs:209-211; Settings.cs:1035, 1043 |
| + dwor ustepuje zoldowi | w wojnie + max(0; 0.20 x D x f - jedzenie druzyn) | ClanBudget.cs:443-457; Settings.cs:1038, 1068 |
| + kredyt wojenny 168 | przy G < R do 0.40 D (Bank) | ClanBudget.cs:490-510 |
| zalogi pierwsze | w wojnie zalogi na pulapie gry; pulap druzyn = pulap - zold zalog - zold karawan | ClanBudget.cs:511-512, 581; Settings.cs:1058 |
| + W5 | przydzial korony (gotowka + miejsce na werbunek) do pulapu druzyn i rodu | ClanBudget.cs:521-526 |
| limity druzyn | kazda druzyna: wlasny zold + czesc wolnego miejsca (glowa 1.5, inni 1); ponad pulapem - proporcjonalnie | ClanBudget.cs:612-654 |
| zwolnienia | zold > 1.10 x pulap przez 3 doby -> 15% nadwyzki dziennie, losowo z calej druzyny | ClanBudget.cs:780-868; Settings.cs:1050-1055 |
| nowa druzyna | tylko gdy wolne miejsce >= 30 ludzi x sredni zold | ClanBudget.cs:1249-1276 |
| werbunek gry | od notabli tylko gdy ludzi < limit i zold < limit zoldu; ochotnik brany, gdy los (najwiekszy z 1 + sqrt(zloto druzyny/10 000) liczb) > wypelnienie - 0.1 | RecruitmentCampaignBehavior.cs:496-498, 534-551 (dec32) |
| zwrot i renty | zwrot 70% zoldu druzyn w polu placony z wplywow korony PIERWSZY (brak -> kredyt Banku 186), reszta wplywow = renty 180 wedlug lenn | CrownIncome.cs:20-27 |
| W5 | garnek = danina P1 dnia, najwyzej reszta wplywow po zwrocie; poziom wyrownania f <= 0.60 limitu (od najslabszych); gotowka tylko za ludzi, ktorzy sa; miejsce na werbunek 10% limitu na dobe | KingdomTreasury.NeedAid.cs:143-148, 169, 206-219, 230-232; Settings.cs:1670-1671 |
| W5b | skarbiec ponad zapas wojny: najwyzej 1/30 nadwyzki dziennie na niepokryta potrzebe | KingdomTreasury.CrownSurplus.cs; Settings.cs:1685 |
| W6 | rod z luzem i kiesa ponad R placi 0.25 x (pulap wlasny druzyn - zold przy celu 0.6), najwyzej (G-R)/45 na dobe, tylko na niepokryta potrzebe | KingdomTreasury.Scutage.cs:10-40; Settings.cs:1676 |

**Sprawdzenie na CSV** [P] (`r2_wzor.py`): wzor oddaje pulap z kolumny `pulap` w 96.9% dob-rodow w wojnie (W37 d337-364) i 98.1% (W43 d93-120) - reszta to roznica chwili odczytu kiesy (mediana 35-67 zl). Uwaga: kolumna `D` w CSV to NIE D budzetu - budzet liczy na `d_staly` (+ lup najezdzcow); kolumna `R` jest liczona z `D` logu.
**Z czego stoi pulap w koncu roku** [P, W37, krolestwa w wojnie, zl/d]: 0.6 D f = 366 tys., skrzynia (zloto rodzin ponad rezerwe) = 448 tys., dwor + kredyt = 124 tys.; razem 938 tys. Zalogi biora 189 tys., druzyny 437 tys. **Wojsko konca roku stoi bardziej na kiesach rodzin (karmionych lupem i okupami, 362 tys. zl/d) niz na D** - dlatego regula, ktora rusza tylko D, rusza wojsko slabo.

**Ile ludzi z pulapu** (`r3_kalibracja.py`): x = pulap druzyn / (zold na czlowieka x limit druzyn) - ile limitu rod MOZE oplacic; y = ludzie / limit.
- Rod na pulapie (x < 0.9) trzyma 97% ludzi, na ktorych go stac (W37), 89% (W43 d93-120) [P].
- Rod z luzem (x >= 1) trzyma 70% limitu (oba biegi) [P]. Krzywa: y = min(kappa x, phi), kappa 0.94 / 0.92, phi 0.71 / 0.68 (W37 / W43).
- "Pan bez klopotu z pieniadzem trzyma ok. 60% druzyny" (raport: 64%, inna definicja "z luzem") to **rownowaga werbunku gry, nie pieniadz**: szansa wziecia ochotnika spada z wypelnieniem (przy jednym dodatkowym losie: 1 - (y - 0.1)^2 - przy 50% wypelnienia 0.84, przy 70% 0.64, przy 90% 0.36), a straty (zabici ok. 400/d, jency, rozbici, zwolnienia) ida za liczba ludzi. Przyjmuje ja jako zmierzony sufit phi i sprawdzam wrazliwosc 0.55 / 0.64 / 0.75.
- Model wobec pomiaru (szeregowi w druzynach rodow z budzetem) [S]: swiat -1.9% (W37), -0.4% (W43); sredni blad krolestwa 378 / 666 ludzi; krzywa z drugiego biegu: -5.2% / +3.4%. Najwiekszy blad: Sarnor (+1.6 tys. - trzyma go hamulec wolnych rak, nie pieniadz) i Dorzecze w W43 (dwie stracone armie).

## 2. Model scenariuszy (`r4_scenariusze.py`)

Rod z budzetem w wojnie (210 rodow): D = reszta stala (kontrakt, lup) + ziemia + zwrot + renta 180. Zwrot = rho x zold druzyn (rho zmierzone na rod, srednio 0.61); renta = udzial rodu (zmierzony, wedlug lenn) x (pula korony + danina - zwroty - gotowka W5). Pulap z wzoru kodu, ludzie = pomiar + limit x (g(x nowe) - g(x dzis)). Wszystko liczone do rownowagi w krolestwie (wiecej ludzi -> wiekszy zwrot -> mniej rent dla innych rodow).
- Kontrola: model bez zmian daje miare 79.69 tys. przy pomiarze 79.63 [S/P].
- **Zalozenia stale**: mapa wojen, lenna, zalogi (zold), limity druzyn, kiesy rodow (pyt. 7), lup i okupy - jak w oknie biegu; Straz Nocna poza modelem (wyjatek pyt. 3, D bez zmian); kompanie najemne bez zmian.
- **D krolestw** - metoda r04 recenzji przeliczona na tym biegu (ludzie z `ludzie-regiony.csv`, D = ziemia + korona z CSV), z wyjatkiem Strazy i Sarnorem x2.5 (pyt. 3): np. Reach 52.7 -> 61.6 -> 98.8, Polnoc 84.3 -> 75.5 -> 38.2, Dorne 45.5 -> 39.3 -> 13.2, Wyspy 20.1 -> 17.3 -> 5.6 tys. zl/d (dzis -> 1. krok -> granica).
- **S1** (pierwszy krok): zmiana czesci pierwotnej D (19%: podatek wsi, oplaty, podatki ludu, cla - placone panu lenna) rozdana rodom wedlug ich ziemi `d_ziemia`; wariant: wedlug ziemi + renty (wynik +-0.3 tys.). Pula korony bez zmian.
- **S2** (granica): ziemia rodu i pula korony krolestwa x r_k = D docelowe / D dzis; pula placi najpierw zwroty, reszta to renty; biedna korona bez rent, zwrot z kredytu Banku (wariant: zwrot ciety do wplywow - wynik -1.2 tys.).
- **S3** (nowa kampania, pyt. 7): kiesy rodow bez zmian, D = pierwszy krok + w x (granica - pierwszy krok); w nie wiem - srodek 0.5, widelki 0.25-0.75.
- **G** (kiesy rodzin): wariant "G stale" i wariant "G - R stale" (rodzina trzyma te sama nadwyzke ponad rezerwe - stan ustalony, bo nadwyzke karmi lup, ktorego regula nie rusza; srodek).
- **S0 z biegu W37**: danina P1 na krolestwo z W43 d93-120 (111.6 tys. zl/d [P], `kiesa-obieg.csv`) x theta (0.5 / 0.75 / 1.0 [S]: w W37 lud odkladal na koniec roku 56-70 tys. zl/d, a stawka s rosnie, gdy brak wojska), W5b 13.6 tys. zl/d x theta, W5 i W6 jak w kodzie; skutecznosc miejsca na werbunek z W5/W6 0 / 0.5 / 1 (gotowka za ludzi, ktorzy sa - zawsze).
- **Sprawdzenie S0 na d93-120** [S wobec P]: W37 d93-120 (95.6 tys.) + model = 104.0 tys. (101.2-105.5); W43 zmierzyl 108.7 tys., z tego 3.4 tys. to Dorzecze, ktore w W37 w tym oknie bylo w pokoju -> porownywalne 105.35 tys. Zgodne w granicach losu kampanii.

## 3. Wyniki - koniec roku (miara progu, tys.)

| scenariusz | nisko | srodek | wysoko | z P4 (srodek) | wariantow < 85 |
|---|---:|---:|---:|---:|---:|
| S0 dzis (kod W43) | 87.6 | 92.0 | 94.6 | 92.6 (+0.5) | 0 z 18 |
| S1 pierwszy krok caly rok | 87.3 | 91.7 | 94.1 | 92.5 (+0.8) | 0 z 18 |
| S2 granica | 82.6 | 87.7 | 92.5 | 89.6 (+2.0) | 2 z 18 |
| S3 nowa kampania, w = 0.5 | 85.8 | 90.8 | 93.9 | 91.9 (+1.1) | 0 z 18 |
| S3, w = 0.25 / 0.75 | 87.2 / 84.1 | 92.0 / 89.3 | 94.4 / 93.1 | - | 0 / 1 z 18 |

Widelki = siatka: G stale / G - R stale, theta 0.5 / 0.75 / 1.0, skutecznosc W5/W6 0 / 0.5 / 1 (18 wariantow). Srodek = G - R stale, theta 0.75, skutecznosc 0.5. Los kampanii +-5 tys. nie jest w widelkach.
Ten sam rachunek na bazie W43 d93-120 (dla porownania, nie koniec roku): S0 108.7 [P], S1 107.7-108.4, S2 103.0-105.5, S3 105.8-107.4 [S].

**Na krolestwo** (srodek; krolestwa w wojnie; pomiar W37 d337-364 -> S0 / S1 / S2 / S3) [S]:

| krolestwo | pomiar | S0 | S1 | S2 | S3 |
|---|---:|---:|---:|---:|---:|
| The North | 8.43 | 10.22 | 10.07 | 7.34 | 9.02 |
| King's Landing | 6.02 | 7.56 | 7.57 | 7.64 | 7.57 |
| Dorne | 6.01 | 6.25 | 5.95 | 5.25 | 5.74 |
| Riverlands | 5.78 | 6.75 | 6.71 | 6.49 | 6.60 |
| The Reach | 5.69 | 6.97 | 6.98 | 7.82 | 7.58 |
| The Vale | 5.68 | 5.94 | 5.79 | 4.92 | 5.37 |
| Stormlands | 5.29 | 6.22 | 6.21 | 6.14 | 6.17 |
| Nights Watch | 4.10 | 4.10 | 4.10 | 4.10 | 4.10 |
| Dragonstone | 3.46 | 3.74 | 3.70 | 3.45 | 3.49 |
| Qarth | 2.97 | 2.97 | 2.97 | 2.97 | 2.97 |
| Dothraki Horde | 2.64 | 2.64 | 2.50 | 1.67 | 2.19 |
| Volantis | 2.49 | 2.53 | 2.95 | 3.96 | 3.82 |
| Targaryen (Meereen) | 2.43 | 3.02 | 3.02 | 3.02 | 3.02 |
| Tyrosh | 2.12 | 2.20 | 2.25 | 2.29 | 2.29 |
| Braavos | 2.00 | 2.14 | 2.25 | 2.25 | 2.25 |
| Targaryen (Aegon) | 1.80 | 2.09 | 2.09 | 2.09 | 2.09 |
| Myr | 1.80 | 2.02 | 2.02 | 2.03 | 2.03 |
| Lorath | 1.75 | 2.03 | 2.02 | 1.99 | 1.99 |
| Iron Islands | 1.63 | 2.23 | 1.96 | 1.07 | 1.49 |
| Norvos | 1.43 | 1.81 | 1.89 | 2.46 | 2.25 |
| Lys | 1.36 | 1.65 | 1.65 | 1.84 | 1.79 |
| Sarnor | 1.30 | 1.31 | 1.21 | 0.80 | 1.00 |
| Qohor | 1.27 | 1.76 | 1.88 | 2.25 | 2.14 |
| Pentos | 1.17 | 1.51 | 1.64 | 1.92 | 1.79 |
| Free Folk | 0.59 | 1.47 | 1.38 | 1.04 | 1.21 |
| Ibben | 0.43 | 0.94 | 0.94 | 0.89 | 0.91 |
| **SWIAT** | **79.63** | **92.05** | **91.68** | **87.68** | **90.85** |

**Nakladka P4** [S]: szansa ochotnika x min(3, max(0.1, skala x udzial zbrojnych)), skala = 0.7 u wsi + 0.3 m rynku (r04), udzial: Wyspy 2, Wolni Ludzie 5, Dothrakowie 3, reszta 1. Przelozona na sufit wypelnienia phi_k z rownowagi werbunku gry (beta = 0.45 / 0.60 doplywu zaleznego od szansy ochotnika - gorna ocena: ochotnicy od notabli to 434 z ok. 1 290 wejsc do druzyn na dobe, 34% [P raport]; przy 34% skutek P4 jeszcze mniejszy): Reach 0.82, Qarth 0.87, Dothrakowie 0.80, Polnoc 0.66, Dorne 0.63, Wyspy 0.61, Sarnor 0.57, Wolni Ludzie 0.72. Swiat +0.5 do +2.0 tys.; w S1: Reach +0.7, Qarth +0.5, Braavos +0.3, King's Landing +0.2; Sarnor -0.3, Dorne -0.3, Dragonstone -0.2, Polnoc -0.2. Rusza tylko rody z luzem - rodom na pulapie brakuje zlota, nie ochotnikow.
**Pyt. 3**: Straz z wyjatkiem stoi na 4.1 tys. w kazdym scenariuszu (bez wyjatku - nie wiem dokladnie, projekt podaje 0.1-3 tys.). Sarnor 0.25 mln: w modelu tylko udzial w D i skala P4 (Sarnor 1.3 -> 0.8 tys. w S2); poza modelem zdjecie hamulca wolnych rak (dzis 328 odmow dziennie, 2.0 tys. ludzi "ma miejsce, nie werbuje" [P raport]) - do +1.0-1.5 tys. w S0/S1 [S], mniej w S2.

## 4. Wrazliwosc

- **Piec krolestw decyduje** (S2 - S0, srodek): Polnoc -2.9 tys., Volantis +1.4, Zelazne Wyspy -1.2, Dolina -1.0, Dorne -1.0 (dalej Dothrakowie -1.0, Reach +0.9). Na bazie W43 d93-120: Wyspy -2.3, Wolni Ludzie -1.4, Polnoc -0.8, Dorne -0.8, Dragonstone -0.7.
- **Dlaczego tracacy traca wiecej, niz zyskujacy zyskuja**: (1) zalogi sa placone pierwsze - cala strata D schodzi z druzyn (Wyspy: u 6 z 10 najwiekszych rodow zalogi zostawiaja druzynom miejsce na mniej niz 10% limitu [P]); (2) zysk D trafia glownie do rodow, ktore juz maja luz (Tyrell x = 2.2, Qarth, Braavos, King's Landing - na sufitie phi albo na limicie druzyn), a rody na pulapie w ludnych krolestwach dostaja tylko swoja czesc rent.
- **Wypelnienie rodow z luzem phi 0.55 / 0.64 / 0.75**: S0 91.3 / 91.7 / 92.2; S2 87.4 / 87.8 / 87.8 - prawie bez wplywu (liczymy zmiane od pomiaru).
- **Pierwszy krok caly rok** = S1 91.7 (-0.4 wobec S0); **swiat w granicy** = S2 87.7 (-4.4).
- **Droga do granicy w S3**: w 0.25 / 0.5 / 0.75 / 1.0 -> 92.0 / 90.8 / 89.3 / 87.7.
- **Rody nie werbuja w miejsce z W5/W6** (skutecznosc 0): S0 89.4, S2 84.2; pelna (1): S0 92.9, S2 88.9. W biegach 120 dob wypelnienie rodow z doplata: W5 49.8 -> 54.0%, W6 47.5 -> 46.7% [P STAN-PRAC] - raczej ponizej 1.
- **Bez W6**: S0 86.7 / 89.5 / 91.8 (theta 0.5 / 0.75 / 1.0) - W6 daje w modelu ok. +2.6 tys. na koniec roku, a w d93-120 W43 zebral tylko 0.1 tys. zl/d [P] - niesprawdzone.
- **Zwrot bez kredytu Banku** (biedna korona tnie zwrot): S2 86.5 (-1.2).
- **G stale zamiast G - R stale**: S2 90.3 zamiast 87.7 (tracacym zostaje wieksza skrzynia).

## 5. Czego nie wiem

- Ile daniny P1 bedzie na koniec roku (theta) - od tego zalezy +-3 tys. w S0; liczy to rachunek "garnek".
- Czy rody z doplata W5/W6 werbuja w dane im miejsce (pomiary 120 dob sa sprzeczne) - +-3 tys.
- Jak szybko pieniadz idzie od pierwszego kroku do granicy i jak blisko granicy zaczyna nowa kampania (w) - +-2.5 tys.
- Wynik wojen: model trzyma mape, lenna, stan wojny i lup jak w biegu; tracace krolestwo (Polnoc, Wyspy) moze w granicy przegrywac i tracic lenna - to nie jest policzone.
- Przesuniecie daniny miedzy krolestwami po regule (lud pustych krain odklada wiecej) - nie liczone tutaj (rachunek "garnek").
- Skutek N3 i N5 (w kodzie W43) na koniec roku - osobno nie liczony; S0 z biegu W37 ma tylko P1 / W5 / W5b / W6.
- Straz bez wyjatku i Sarnor bez hamulca - tylko szacunek slowny.

## 6. Pliki i ponowne uruchomienie

Skrypty (python -I; kazdy przyjmuje `--log --tabele --okno 28 --do <ostatnia doba>`):
- `wspolne.py` - czytniki (linie Skarbce, budzet-rodow.csv, ludzie-regiony.csv, kiesa-obieg.csv), wzor pulapu; pamiec podreczna `cache_*.json`.
- `model.py` - rod jako srednia okna, pulap rodu, krzywa wypelnienia.
- `r1_pomiar.py` - miara progu (swiat, krolestwo) i tabela rodow; wynik `r1_*.out.txt`.
- `r2_wzor.py` - sprawdzenie wzoru pulapu z kodu na CSV; `r2_*.out.txt`.
- `r3_kalibracja.py` - krzywa wypelnienia i blad modelu (`--log2/--tabele2/--do2` = test krzyzowy); `r3_*.out.txt`.
- `r4_scenariusze.py` - S0-S3, P4, wrazliwosc, widelki; `r4_*.out.txt`, `r4_wynik_*.json`.

Na nowym biegu rocznym W43 (S0 wtedy = pomiar, danina z jego `kiesa-obieg.csv`):
```
cd <ten katalog>
python -I r1_pomiar.py      --log "<D>\Armoury-<data>.log" --tabele "<D>\Logs\<data>" --okno 28 --do 364 --wy w43rok
python -I r2_wzor.py        --log "<D>\Armoury-<data>.log" --tabele "<D>\Logs\<data>" --okno 28 --do 364
python -I r3_kalibracja.py  --log "<D>\Armoury-<data>.log" --tabele "<D>\Logs\<data>" --okno 28 --do 364 --log2 "<W37 log>" --tabele2 "<W37 tabele>" --do2 364
python -I r4_scenariusze.py --log "<D>\Armoury-<data>.log" --tabele "<D>\Logs\<data>" --okno 28 --do 364
```
Bieg W37 (dzisiejsze liczby): `--log D:\Backup-Bannerlord\logs\2026-10-13-noc34-35\Armoury-2026-10-13_06-24-10.log --tabele ...\Logs\2026-10-13_06-24-10 --do 364 --s0-log ...\Armoury-2026-10-13_13-28-08.log --s0-tabele ...\Logs\2026-10-13_13-28-08 --s0-do 120`.
