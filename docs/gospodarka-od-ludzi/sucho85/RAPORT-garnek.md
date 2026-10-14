# GARNEK KORONY PO REGULE "PRODUKUJA LUDZIE" - rachunek na sucho (13.10)

Zadanie: czy pieniadz koron (danina P1, doplata W5, W5b ze skarbca, tarczowe W6, skarbce, Bank 186) przesunie sie tam, gdzie po regule zabraknie wojska.
Papier, nic w grze, nic w repo. **[P]** = pomiar z logu / tabel / kodu, **[S]** = szacunek na pomiarach.
Miara progu jak w `sprawdz_logi.py` (W5-7): suma "wojsko rodow" krolestw w WOJNIE z linii `Skarbce:`, srednia 28 dob. Skrypt `g01` odtwarza ja co do 0.1 tys.
we wszystkich 7 biegach (79.4 / 105.6 / 99.9 / 106.7 / 101.7 / 111.2 / 109.0) [P].

## 0. Wynik w 10 liniach

1. Dzis cala kasa korony stoi na **jednym krolestwie**: danina P1, W5, W5b i W6 biora i daja tylko wewnatrz tego samego krolestwa (kod, rozdz. 1). Garnek nie umie przeniesc zlota z Reach na Polnoc - umie tylko przesunac je wewnatrz Polnocy do rodow, ktore nie maja na zold.
2. Bieg W43, doby 93-120 [P]: do skarbcow wplywa 561 tys. zl/d (danina P1 113 tys., danina z kas osad 220 tys., podatek 184 8 tys.) + 88 tys. z 1/360 zapasu; wychodzi zwrot zoldu 297 tys. (100%), renty 287 tys., W5 24 tys., W5b 14 tys., W6 0.1 tys. Z daniny tylko ok. 1/5 idzie na doplate W5 - reszta wraca do panow rentami wedlug lenn.
3. Bieg roczny W37 (bez P1/W5/W5b/W6) na koniec roku [P]: skarbce 27.1 mln (start 57.9), prawie kazdy ok. 1.05 mln, dlug w Banku 1.82 mln (Braavos 0.66 mln), podatek 184 15 tys. zl/d, s na pulapie 3% w 20 z 26 krolestw w wojnie.
4. **S1 (pierwszy krok, zloto jak dzis): TAK - garnek idzie tam, gdzie brak.** Lud pustych krain potrzebuje mniej, a zarabia tyle samo, wiec odklada wiecej i danina rosnie: Polnoc +7.6 tys., Dorne +7.1, Wyspy +7.9, Wolni Ludzie +6.1, Smocza Skala +7.2 tys. zl/d; w Reach, King's Landing, Volantis, Braavos spada do zera albo o polowe. Wzrost daniny pokrywa spadek D tych krolestw (Polnoc -8.6, Dorne -6.1, Wyspy -2.8 tys. zl/d) [S].
5. **S2 (granica) i S3 (nowa kampania): NIE.** Gdy pieniadz lezy wedlug ludzi, lud pustych krain zarabia malo i danina tez idzie wedlug ludzi: Polnoc 8.0 tys., Dorne 2.8, Wyspy 1.2, Wolni Ludzie 0.7 tys. zl/d, a Reach 20.8 i Volantis 12.4 tys. - tam, gdzie W5 nie ma kogo ratowac (te pieniadze ida w renty). W5 pokrywa wtedy potrzebe: Polnoc 38%, Dorne 9%, Wyspy 4%, Wolni Ludzie 8%, Smocza Skala 5% [S].
6. Wojsko lordow w wojnie, koniec roku [S]: **S0 93-99 tys., S1 96-102 tys., S2 84-94 tys., S3 84-91 tys.** (bez garnka: 79 / 79 / 73 / 73). Kampanie roznia sie o +-5 tys. Sprawdzian modelu na dobach 93-120: z baza W37 model 102-106, z baza W43 model 110-113, pomiar W43 109.0 [P].
7. Prog 85 tys.: S0 i S1 trzymaja z zapasem; S2 i S3 sa na progu (dolne widelki 84 tys.) - przechyl zrobia puste krolestwa, nie bogate (bogate maja luz w pulapie i wiecej zlota nie zamieniaja na ludzi).
8. S3, skarbce wedlug ludzi: Reach dostaje 8.1-9.0 mln, Volantis 4.9-5.4, King's Landing 4.6-5.2; najmniejsze schodza na podloge. Proponuje **podloge 1.0 mln** (polowa dzisiejszego startu): przy 0.5 mln (sam zapas korony) 8 koron nie ma ani 1/360, ani W5b w pierwszym roku. Pozycza Smocza Skala (ok. 4.4 tys. zl/d, limit po ok. 230 dobach), Sarnor (1.6-3.2 tys., limit po 120-240), Dorne (2.5 tys.) - w roku 1 to trzyma zwrot zoldu, w roku 2 odsetki 20-30% [S].
9. Zapas kupcow x m (S1, pierwsze tygodnie): puste miasta oddaja jednorazowo 1.8-2.1 mln (panom 0.9-1.2 mln) - przez miesiac puste krolestwa sa bogatsze (+3-4 tys. ludzi chwilowo); ludne zatrzymuja 2.0-2.6 mln, zawor stoi 0-13 dob na starcie kampanii (Volantis, Qarth), 17-66 dob w pozniejszym stanie (Braavos 33-66). Na koniec roku bez znaczenia. W S3 tego skoku nie ma, ale rozruch na kasach startowych przechodzi na ludne krolestwa (w pierwszych tygodniach puste traca ok. 5-14 tys. ludzi-rownowaznikow D, ludne ich nie wykorzystaja - luz).
10. Nakladka P4 (pobor wedlug ludzi) nie zmienia garnka; spowalnia zamiane doplaty na ludzi w Dorne, na Wyspach, Smoczej Skale, Polnocy (czynnik 0.44-0.68): -0.7 do -2.1 tys. na koniec roku [S]. Pyt. 3 (Straz stala, Sarnor 0.25 mln) wliczone; w roku 1 nie zmienia wojska (Sarnor ma luz i skrzynie).

## 1. Strumienie garnka korony - z kodu i z pomiaru

Kod: galaz w-toku/zbior-e8 (WGRANIE 43), katalog `...\ae222cb7-...\scratchpad\zb\int\Armoury\src`.

**Do skarbca krolestwa** (zl/d swiata, bieg W43 doby 93-120 [P]):

| strumien | regula | plik:linia | W43 d93-120 |
|---|---|---|---:|
| powinnosci wasali | 3% (wojna) / 2% (pokoj) czesci "ziemia" D rodu | KingdomTreasury.cs:63-118 (pobor :97-101) | 3.4 tys. |
| danina wojenna z kas osad | miasto 1%/d kasy ponad zapas kupcow, wies 2.5%/d ponad 15 dni zakupow; x2 z polityka War Tax | KingdomTreasury.cs:402-414 | 219.5 tys. |
| clo od handlu | licznik cel miasta -> skarbiec (jeden pobor) | KingdomTreasury.cs:417-438 | 12.0 tys. |
| podatek wojenny ludu 184 | od zarobku ludu: s x waga klasy (W 0.5, R 1, P 1.5); s +0.5 pkt co 7 dob przy kredycie, zwrocie ponad wplywy albo skarbcu < zapasu wojny = max(0.5 mln, 60 dob zwrotu) | KingdomTreasury.WarTax.cs:187-220; krok :274, :285-295 | 8.0 tys. |
| danina z oszczednosci P1 | 2.5%/d x s / 3% kiesy klasy ponad 10/21/30 (podzamcze 10) dni potrzeby; s = wieksza z s korony i SN "brak wojska u panow" (W5b regula 1) | KingdomTreasury.WarAid.cs:108-122 (stawka), :158-224 (pobor); TownFolk.Live.cs:830-841, :911; TownFolk.Savings.cs:82-108; SN: WarTax.cs:311-325 | 113.3 tys. |
| 1/360 zapasu | 1/360 skarbca ponad 0.5 mln dodawane do wplywow dnia | CrownIncome.cs:197-198 | 87.6 tys. |
| inne | czesc korony z zaworu zamkow i miast, 1/9 prawa trzecich, gra / BK / Diplomacy | CrownIncome.cs:186, linia 165 | 39.3 tys. |
| kredyt Banku 186 | tylko na brak zwrotu zoldu; dlug najwyzej 180 x sredni podatek, pula 1/60 kapitalu Banku ponad 2 mln | CrownBorrow.cs:294-330 (:315, :324); KingdomTreasury.cs:294-307 | 1.3 tys. (dlug 55 tys.) |
| tarczowe W6 | 25% wolnego pulapu rodow z luzem, najwyzej 1/45 nadwyzki kiesy; tylko do niepokrytej potrzeby W5 | KingdomTreasury.Scutage.cs:68, :113 | 0.1 tys. (nalezne 67.6 tys.) |

**Ze skarbca do rodow** (kolejnosc doby: rata 186 -> dary -> kontrakty -> wezwania -> zwrot -> W5 -> renty):

| strumien | regula | plik:linia | W43 d93-120 |
|---|---|---|---:|
| rata Banku | 1/182 szczytu dlugu, najwyzej 30% sredniego podatku | CrownBorrow.cs:213-214 | 0.3 tys. |
| dary 182 / kontrakty 185 | | CrownIncome.cs (kolejnosc), linia 165 | 22.8 / 13.0 tys. |
| zwrot zoldu | 70% zoldu partii w polu (bez zalog, minus wydatki we wlasnych miastach) | KingdomTreasury.cs:219-361 (podzial :197-209) | 297.4 tys. (100%) |
| doplata W5 | garnek = danina P1 tej doby, najwyzej reszta po zwrocie; rody ograniczone pulapem do 60% limitu, od najslabszych | KingdomTreasury.NeedAid.cs:169 (garnek), :279 | 24.4 tys. |
| W5b ze skarbca | najwyzej 1/30 nadwyzki skarbca ponad zapas wojny i dlug 186, najwyzej niepokryta potrzeba | KingdomTreasury.CrownSurplus.cs:91 | 13.7 tys. |
| renty 180 | reszta wplywow dnia wedlug wag lenn (miasto 2, zamek 1.5, wies 0.25) | CrownRents.cs:349, :598-599 | 287.5 tys. |
| polityki krola | mennica, monopole, myto - z kas miast do rodu krola | KingdomTreasury.cs:441-461, :588-676 | (poza garnkiem) |

Wazne z kodu: **kazde zrodlo garnka W5 jest wewnatrz krolestwa** (danina ludu tego krolestwa, skarbiec tego krolestwa, tarczowe rodow tego krolestwa). Nie ma drogi zlota miedzy koronami poza darami 182 i reparacjami.

Na krolestwo, W43 doby 93-120 [P] (zl/d; pelna tabela `g01_W43.out.txt`):

| krolestwo | wojsko w wojnie | wplywy dnia | danina P1 | 184 | zwrot | renty | W5 | W5b | s 184 | skarbiec d120 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| The North | 11 705 | 56 997 | 17 620 | 968 | 31 089 | 28 643 | 373 | 0 | 2% | 1.58 mln |
| King's Landing | 10 392 | 63 835 | 26 255 | 1 860 | 34 625 | 32 179 | 123 | 0 | 3% | 1.58 mln |
| The Reach | 8 213 | 35 536 | 4 224 | 169 | 23 250 | 14 717 | 622 | 123 | 0.5% | 1.56 mln |
| The Vale | 7 426 | 29 591 | 0 | 0 | 19 921 | 12 286 | 0 | 4 848 | 0% | 1.37 mln |
| Dorne | 6 316 | 25 809 | 3 895 | 333 | 17 067 | 10 224 | 1 621 | 110 | 1% | 1.57 mln |
| Iron Islands | 6 175 | 24 364 | 7 879 | 474 | 15 265 | 5 264 | 6 704 | 1 338 | 2% | 1.53 mln |
| Free Folk | 3 678 | 14 011 | 5 854 | 448 | 6 462 | 5 479 | 4 745 | 1 933 | 3% | 1.52 mln |
| Dragonstone | 3 581 | 15 775 | 2 749 | 223 | 10 189 | 5 719 | 1 974 | 1 418 | 1% | 1.43 mln |
| Qarth | 2 832 | 16 317 | 0 | 0 | 7 465 | 11 615 | 0 | 0 | 0% | 1.57 mln |
| Volantis | 2 826 | 13 811 | 384 | 30 | 7 852 | 6 347 | 0 | 0 | 0% | 1.32 mln |
| Braavos | 2 627 | 14 640 | 5 099 | 333 | 6 954 | 6 134 | 304 | 25 | 2% | 1.37 mln |
| Targaryen (Meereen) | 2 553 | 16 587 | 4 405 | 262 | 8 246 | 6 777 | 3 928 | 3 076 | 2% | 1.41 mln |

Skarbce w 120 dobach schodza z 2.0 do ok. 1.57 mln samym 1/360 (0.5 + 1.5 x e^(-120/360) = 1.575 mln) [P/S]; W5b zjadl tylko tyle, ile widac roznicy (Vale 0.2 mln).

Koniec roku W37 (doby 337-364, bez P1/W5/W5b/W6) [P, `g01_W37.out.txt`]: skarbce 27.1 mln, typowo 1.05 mln (= 0.5 + 1.5 x e^(-1)); nizej King's Landing 0.66, Lys 0.64, Braavos 0.51 mln; dlug 186 razem 1.82 mln (Braavos 662 tys., Smocza Skala 282, Aegon 195, King's Landing 175 tys.); kredyt 11.4 tys. zl/d; podatek 184 15.0 tys. zl/d (Polnoc 1.9, KL 1.6, Vale 1.4, Dorne 1.1, Reach 1.1 tys.); s = 3% w 20 krolestwach, 1% w 2, 0 w 4 (Straz, Dothrakowie, Wyspy, Sarnor). Zwrot 277 tys. (100%), renty 172 tys. zl/d.

Przebieg w czasie (`g02`): w W37 bez daniny kiesy ludu miast rosna z 8.6 do 41.6 mln w rok (ok. +100 tys. zl/d do polowy roku, +21 tys. na koncu) [P] - to jest nadwyzka, z ktorej bierze danina. W W43 z danina kiesy stoja na 16-18 mln, danina 101-123 tys. zl/d w dobach 85-120 [P]: danina zjada ok. 72% nadwyzki brutto ludu (113 z 157 tys. zl/d) [S z P].

## 2. Po regule: danina P1 i garnek W5 w S1 i S2

Rachunek (g03): danina na koniec roku = 0.72 x nadwyzka brutto ludu krolestwa (miasta + podzamcza: dochod - wydatki + danina, kiesa-ludu.csv W43, 8 probek). S1: potrzeba ludu x m (miasta) i x u (podzamcza), z wiekszych zakupow wraca ludowi 16% (alfa 0.16; wariant 0.30) - jak r06. S2/S3: zloto wedlug ludzi, wiec nadwyzka ludu swiata wedlug ludzi krolestwa. Ludzie: rody z budzet-rodow.csv konca roku W37; zmiana D z r04; rod na pulapie odpowiada 0.60 x zmiana D / zold na glowe (4.4-6 zl/d [P]), rod z luzem nie rosnie (wariant wysoko: polowa). Garnek: danina -> W5 do 60% limitu, potem W5b, potem W6, reszta w renty.

| krolestwo | D dzis | zmiana D S1 / S2 | danina S0 | S1 | S2/S3 | potrzeba W5 S0 / S1 / S2 | pokrycie S1 / S2 |
|---|---:|---|---:|---:|---:|---|---|
| The North | 83.7 tys. | -8.6 / -44.9 | 15.9 | 23.6 | 8.0 | 10.5 / 12.7 / 24.6 | 100% / 38% |
| Dorne | 44.9 | -6.1 / -31.5 | 6.2 | 13.3 | 2.8 | 2.7 / 3.7 / 11.1 | 100% / 9% |
| Iron Islands | 20.1 | -2.8 / -14.4 | 5.3 | 13.2 | 1.2 | 23.1 / 24.6 / 30.1 | 54% / 4% |
| Free Folk | 7.5 | -0.8 / -4.0 | 2.2 | 8.3 | 0.7 | 12.2 / 12.5 / 13.7 | 72% / 8% |
| Dragonstone | 16.0 | -1.9 / -9.9 | 1.0 | 8.2 | 1.3 | 6.7 / 8.4 / 10.8 | 100% / 5% |
| The Vale | 50.6 | -2.3 / -11.8 | 12.5 | 11.9 | 8.0 | 10.3 / 10.8 / 13.3 | 100% / 82% |
| Dothraki | 40.6 | -6.1 / -31.8 | 0 | 0 | 1.8 | 0 / 0 / 1.5 | - / 100% |
| Ibben | 3.9 | -0.1 / -0.4 | 4.2 | 6.1 | 0.7 | 8.7 / 8.7 / 8.8 | 95% / 23% |
| The Reach | 53.2 | +9.1 / +47.3 | 10.2 | 0 (lud wyda oszczednosci w ok. 58 dob) | 20.8 | 7.4 / 6.2 / 2.3 | 100% / 100% |
| King's Landing | 45.9 | +2.3 / +11.7 | 21.0 | 10.9 | 11.9 | 5.3 / 5.2 / 4.6 | 100% / 100% |
| Volantis | 21.8 | +7.4 / +38.3 | 2.1 | 0 | 12.4 | 5.7 / 4.2 / 0.4 | 44% / 100% |
| Braavos | 9.0 | +4.3 / +22.3 | 3.4 | 0 | 0 | 0.4 / 0 / 0 | - |

(tys. zl/d, wariant nisko, alfa 0.16 [S]; pelne tabele `g05_W37_p1000000.out.txt`, `g03_W37_p1000000.out.txt`.)

Swiat [S]: danina S0 108, S1 120, S2 108 tys. zl/d; W5+W5b+W6 wyplacone S0 84, S1 104, S2 72 tys.; potrzeba W5 S0 138, S1 141, S2 164 tys. zl/d. Danina, ktorej W5 nie potrzebowalo (renty wedlug lenn): S1 najwiecej Polnoc i Dorne (10.8 i 9.6 tys. - tam juz jest pokryte), S2 Reach 18.4 i Volantis 12.0 tys.
Ludzie z garnka (W5 + W5b + W6): S0 ok. 13.4 tys., S1 17.0 tys., S2 10.8 tys., S3 10.7 tys. (wariant nisko) [S].

Odpowiedz: **w S1 garnek pustych krolestw lagodzi spadek ich D z nawiazka** (lud tam odklada, bo zarabia "na osade", a potrzebuje "na ludzi"). **W S2/S3 nie lagodzi** - ich danina spada razem z D (Polnoc -7.9, Dorne -3.5, Wyspy -4.1 tys. zl/d), a garnek bogatych nie moze do nich poplynac.

## 3. S3 - skarbce koron wedlug ludzi

Ta sama suma swiata co w dobie 1 bazy (57.9 mln) [P], dzielona wedlug ludzi, z podloga (`g03 --podloga`):

| korona | podloga 0.5 mln | podloga 1.0 mln (propozycja) | 1/360 w dobie 1 (dzis 4.2 tys.) | koniec roku |
|---|---:|---:|---:|---:|
| The Reach | 8.99 mln | 8.10 mln | 23.6 / 21.1 tys. zl/d | 3.6 / 3.3 mln |
| Volantis | 5.38 | 4.85 | 13.6 / 12.1 | 2.3 / 2.1 |
| King's Landing | 5.15 | 4.64 | 12.9 / 11.5 | 2.2 / 2.0 |
| Qarth | 4.29 | 3.87 | 10.5 / 9.4 | 1.9 / 1.7 |
| The North / Vale | 3.48 | 3.13 | 8.3 / 7.3 | 1.6 / 1.5 |
| Dorne | 1.20 | 1.08 | 2.0 / 1.6 | 0.76 / 0.71 |
| Iron Islands, Dragonstone | 0.51-0.54 | 1.00 | 0-0.1 / 1.4 | 0.50 / 0.68 |
| Free Folk, Ibben, Sarnor, Straz, Skagos, Wyspy Letnie, Yi Ti | 0.50 | 1.00 (+ Lorath, Lys, Myr, Aegon, Dothrakowie) | 0 / 1.4 | 0.50 / 0.68 |

- **Jak szybko schodzi do zapasu wojny**: samo 1/360 daje T(t) = 0.5 mln + (T0 - 0.5 mln) x e^(-t/360); Reach z 9 mln ma po roku 3.6 mln, czyli dalej ponad zapasem wojny (60 dob zwrotu = ok. 1.2 mln) [S]. W5b (1/30 nadwyzki dziennie) zjada nadwyzke w kilka miesiecy tylko tam, gdzie rody maja potrzebe - a potrzeby sa w pustych krolestwach, ktore nadwyzki nie maja. Przy podlodze 0.5 mln osiem koron nie ma ani 1/360, ani W5b; przy 1.0 mln maja 0.5 mln "skrzyni wojennej" (W5b do 16.7 tys. zl/d na start, wyczerpane w ciagu roku przy stalej potrzebie) [S].
- **Bank 186** (kredyt tylko na brak zwrotu zoldu): w S2/S3 pozyczaja Smocza Skala ok. 4.2-4.8 tys. zl/d (limit 180 x sredni podatek osiagniety po ok. 215-240 dobach), Sarnor 1.6-3.2 tys. (limit po 120-240 dobach), Dorne 2.4-2.5 tys. (limit po ok. 900 dobach), Dothrakowie 0.4-0.5 tys.; razem 8-11 tys. zl/d - miesci sie w puli Banku (ok. 47 tys. zl/d przy kapitale 4.85 mln [P]). W S0 i S1 na koniec roku w stanie srednim nikt nie musi pozyczac (danina podnosi wplywy; W37 bez daniny pozyczal 11.4 tys. zl/d [P]) [S]. Skutek: w roku 1 kredyt trzyma zwrot zoldu (to ok. 1-2 tys. ludzi), po osiagnieciu limitu zwrot przepada, a odsetki 20-30% rocznie obciazaja rok 2.
- **Wojsko w roku 1** (koniec roku): S3 84.4-90.0 tys. przy podlodze 0.5 mln, 84.4-91.3 tys. przy 1.0 mln [S]. Na koniec roku rozni sie malo (W5b zuzyte w ciagu roku), ale w pierwszych miesiacach podloga 1.0 mln trzyma w pustych krolestwach ok. 1.5-2.5 tys. ludzi wiecej [S].
- Propozycja: **podloga 1.0 mln**; reszta (45.9 mln) wedlug ludzi. Uzasadnienie liczba: przy 0.5 mln biedne korony nie maja nic ponad zapas wojny, wiec wypadaja z nich jednoczesnie 1/360 i W5b - dokladnie tam, gdzie po regule brakuje wojska.

## 4. Zapas kupcow x m (projekt rozdz. 5) - pierwsze miesiace

`g04` na kasy-miast.csv [P], rachunek [S] (zawor 7%/d nadwyzki ponad zapas):
- **S1, start nowej kampanii** (doby 1-28, kasy startowe gry): ludne miasta musza podniesc zapas o 2.0 mln, ale maja na starcie nadwyzke - zawor stoi tylko w Volantis (5-12 dob), Qarcie (12-13) i Braavos (1-2). Puste oddaja jednorazowo 1.77 mln (panom 0.92 mln) w ok. miesiac: +28.5 tys. zl/d do D ich panow -> ok. +3.1-3.6 tys. ludzi chwilowo (bez Strazy, ktora nie placi zoldu w zlocie).
- **S1 wlaczone na starszej kampanii** (stan dob 93-120 / 337-364): ludne zatrzymuja 2.2-2.6 mln, zawor stoi: Reach 17-19 dob, Volantis 26-32, Qarth 34-40, Braavos 33-66; puste oddaja 1.9-2.1 mln (panom 1.0-1.2 mln) -> +4 tys. ludzi chwilowo w pustych, -2.5-3 tys. w ludnych.
- Na koniec roku to nie ma znaczenia (jednorazowe); w pierwszym miesiacu dziala ODWROTNIE do reguly (puste chwilowo silniejsze).
- **S3** (kasy startowe wedlug ludzi): skoku nie ma (kasa i zapas rosna razem z m). Ale rozruch na kasach startowych (zawor startowy swiata 659 tys. zl/d, do panow 344 tys.) idzie wedlug ludzi: w pierwszych tygodniach Reach +32.9 tys. zl/d, Volantis +22.3, Qarth +19.0; Wyspy -15.2, Dorne -13.6, Smocza Skala -12.1, Polnoc -9.0 tys. zl/d. Ludne rody maja luz, wiec tego nie zamienia na ludzi; puste traca - wojsko swiata w pierwszych 1-2 miesiacach nizsze niz w S1 o kilka tysiecy [S].

## 5. Wojsko swiata - scenariusze i 85 tys.

Koniec roku, wojsko lordow krolestw w wojnie (miara W5-7), po skali x1.036 (rody budzetu -> linie Skarbce) [S]:

| scenariusz | widelki | bez garnka | uwagi |
|---|---|---:|---|
| S0 (WGRANIE 43, bez reguly) | **93-99 tys.** | 79.3 (W37 [P] 79.4) | garnek daje +14-20 tys.; sprawdzian na d93-120: baza W37 -> model 102-106, baza W43 -> model 110-113, pomiar W43 109.0 [P] |
| S1 pierwszy krok | **96-102 tys.** | 78.6 | danina rosnie w pustych; W5+W5b+W6 104 tys. zl/d |
| S2 granica | **84-94 tys.** | 73.3 | garnek zostaje w bogatych; niepokryta potrzeba 92 tys. zl/d |
| S3 nowa kampania | **84-91 tys.** (podloga 1.0 mln; 84-90 przy 0.5) | 73.3 | jak S2 + brak W5b u biednych koron |
| nakladka P4 | S0 -1.2, S1 -2.1, S2/S3 -0.7 tys. | | wolniejszy pobor w krolestwach z czynnikiem < 1 |

Na dobach 93-120 (rozruch na kiesach startowych, `g03 --koniec 120`) te same scenariusze daja: S0 102-106, S1 102-107, S2 94-103, S3 91-97 tys. (baza W37); z baza W43: S0 110-113, S1 109-114, S2 100-111, S3 97-105 tys. [S] - prog 85 tys. dotyczy konca roku, bo rozruch maskuje spadek.

Na krolestwo (widelki, po skali) - wiecej `g05_W37_p1000000.out.txt`: Polnoc S0 10.1-10.3 -> S2 6.2-6.6 tys.; Dorne 6.6 -> 4.8-5.0; Wyspy 2.4-3.7 -> 0.7-2.1 (S3 0.7-1.2); Wolni Ludzie 1.2-3.3 -> 0.4-2.6 (S3 0.4-1.1); Smocza Skala 3.9 -> 3.2; Reach 7.3 -> 7.3-8.4; Volantis 3.0-3.4 -> 3.4-4.4; Braavos 1.9-2.0 -> 1.9-2.5 tys.
Traca: Polnoc, Dorne, Wyspy, Wolni Ludzie, Smocza Skala, Vale, Ibben. Zyskuja (malo, bo luz): Volantis, Reach, Braavos, Pentos, Qohor, Norvos.

## 6. Zalozenia, od ktorych wynik zalezy najbardziej

1. Danina na koniec roku = 72% nadwyzki brutto ludu (zmierzone raz, W43 d93-120, swiat) i korona ma potrzebe (s > 0) wszedzie tam, gdzie rody maja potrzebe W5 albo skarbiec ponizej zapasu wojny. Nadwyzka ludu na krolestwo z 8 tygodniowych probek kiesa-ludu.csv - szum jednej doby duzy.
2. S2/S3: nadwyzka ludu dzielona wedlug ludzi krolestwa (granica = zloto lezy wedlug ludzi). To jest stan docelowy, nie prognoza.
3. Odpowiedz rodow: na pulapie 0.60 x zmiana D / zold na glowe; rody z luzem nie rosna (nisko) albo polowa (wysoko); warunek rent (sluzba, zalogi) nie liczony - potrzeba W5 raczej zawyzona.
4. W5b: skarbiec zjadany przez stala potrzebe przez caly rok (nisko) albo bez zuzycia (wysoko - polowa puli 1/30).
5. Zmiana D krolestw z r04 (S1 = 19% D wprost od ludzi, S2 = cale D wedlug ludzi); baza konca roku = W37 + pakiet garnka (bieg roczny W43 jeszcze nie skonczony).
6. Wojny (kto z kim, zdobyte twierdze) jak w bazie.

## 7. Czego nie wiem

- Jak naprawde rozlozy sie nadwyzka ludu na krolestwa po regule (pomiar P0 - zarobek, potrzeba i oszczednosci ludu kazdego miasta).
- Czy rody z luzem (Reach: 47% limitu przy pulapie 2 x wiekszym niz zold [P]) zamienia wiecej zlota na ludzi - to nie pieniadz, wiec garnek tego nie rozstrzyga.
- Tempo zuzycia skarbcow przez W5b w ciagu roku (zalezy od tego, kiedy w roku pojawi sie potrzeba).
- Skutek P4 na zamiane doplaty na ludzi - liczba ("polowa luki tempa") jest z reki.
- Rok 2: odsetki Banku, wyczerpane skrzynie rodow pustych krolestw, wyczerpany zapas kupcow - nie liczylem.
- Straz: zold na glowe ok. 1 zl (sluzy bez zoldu w zlocie) - w przeliczeniach na ludzi pomijana.

## 8. Skrypty (wszystkie: `python -I <skrypt> --log <Armoury-*.log> --tabele <Logs\<data>> [--okno 28]`)

- `g01_pomiar.py` - pomiar na krolestwo (miara W5-7, strumienie garnka, budzet rodow, kiesa ludu, kasy miast); `--koniec 120` = okno do doby 120; `--probki 8`.
- `g02_trend.py` - przebieg 28-dobowy swiata (wojsko, skarbce, dlug, danina, 184, zwrot, renty, W5, W5b, kiesy ludu); `--krolestwa "A,B"`.
- `g03_scenariusze.py` - S0-S3 + P4 + pyt. 3; `--log120/--tabele120` (bieg z danina, gdy baza jej nie ma; domyslnie W43), `--podloga 1000000`, `--f 0.72`, `--rec` (r04).
- `g04_zapas_kupcow.py` - zapas kupcow x m: S1 pierwsze tygodnie i S3 rozruch.
- `g05_podsumowanie.py` - garnek wobec zmiany D i wojsko na krolestwo (po g03, te same argumenty i `--podloga`).

Na nowym biegu rocznym (WGRANIE 43): `g03` z `--log/--tabele` tego biegu (i `g05` z tymi samymi argumentami) - baza ma wtedy danine: S0 to pomiar, a ludzie oplaceni gotowka W5/W5b/W6 sa najpierw odejmowani z bazy i doliczani od nowa w kazdym scenariuszu (sprawdzone na biegu W43 120 dob jako bazie: model S0 110-113 wobec pomiaru 109.0).
