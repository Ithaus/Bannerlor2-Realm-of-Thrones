# Rozpoznanie danych biegu rocznego: produkcja, spozycie i dochod na czlowieka (13.10.2026)

Praca tylko do odczytu. Nic nie uruchamiano w grze, nic nie budowano, nie zmieniano kodu, ustawien ani zapisow. Powstaly tylko pliki w `scratchpad\gosp-ludzie`.

Pytanie (decyzja Jeffa 13.10): "produkuja ludzie; tam, gdzie jest ich wiecej, wiecej sie produkuje (...) wioska to tylko SYMBOL wielu wiosek". Ten dokument odpowiada liczbami na cztery sprawy:
1. jak DZIS rozklada sie produkcja, spozycie, zarobek, podatki, pobor i wojsko na czlowieka - wedlug krolestw i regionow;
2. czy dzisiejszy dochod idzie za liczba LUDZI, czy za liczba OSAD;
3. czy liczby ludzi w ksiedze sa wiarygodne (takze krainy szczegolne);
4. co wyszloby, gdyby wszystko podzielic wedlug ludzi (rachunek na papierze) i gdzie prosta proporcja daje absurd.

## Jak czytac

- **[P]** = pomiar z tabeli albo logu biegu. **[S]** = szacunek (rachunek na pomiarach). "Nie wiem" = nie ustalono.
- Bieg: nowa kampania, 364 doby (doba 1 = dzien gry 108836, doba 364 = dzien gry 109199), kod galezi w-toku/zbior-e8 (stan WGRANIA 37), sam swiat AI.
- **d364** = ostatnia doba. **s28** = srednia dob 337-364 (dni gry 109172-109199). Jesli nie napisano inaczej, tabele pokazuja s28.
- Zrodla: LOG:n = `D:\Backup-Bannerlord\logs\2026-10-13-noc34-35\Armoury-2026-10-13_06-24-10.log`, linia n. Tabele z `...\Logs\2026-10-13_06-24-10\`: LR = ludzie-regiony.csv, SP = spichlerze-regiony.csv, BK = ludnosc-bk.csv, KL = kiesa-ludu.csv (probka co 7 dob; "s28" = 4 probki z dob 343, 350, 357, 364), KM = kasy-miast.csv, BUD = budzet-rodow.csv.
- Kod: sciezki wzgledem `...\scratchpad\zb\int\Armoury\src` (nasz mod), `dec32\cs_full` (gra), `e4tools\bkfull` (BannerKings).
- Region = miasto albo zamek + jego wsie (227 regionow: 97 miast, 130 zamkow, 569 wsi). Krolestwo regionu i rodu = stan z doby 364 (w roku 14 twierdz zmienilo pana).
- "Ludzie" = ksiega ludzi naszego moda (PopulationLaw.PeopleOf). "Ludnosc BK" = osobny rachunek BannerKings (inna, duzo mniejsza liczba). "Hearth" i "dobrobyt" = liczby gry.
- "King's Landing" = krolestwo Baratheonow z Krolewskiej Przystani (Westerlands + Ziemie Korony). "Targaryen (Meereen)" i "Targaryen (Aegon)" = dwa krolestwa Targaryenow z logu.
- Wszystkie liczby tabel wyszly ze skryptow w `skrypty\` (lista w rozdz. 8); tabel nie przepisywano recznie.

## 0. Dla Jeffa - prostym jezykiem

1. **Ksiega ludzi ma 51.5 mln ludzi**: 41.1 mln na wsi i 10.4 mln w miastach. Jedna wies na mapie stoi srednio za 72 tysiacami ludzi - ale od 1.3 tysiaca (Dar Nocnej Strazy) do 302 tysiecy (Reach). To 239 razy roznicy.
2. **Gra tej roznicy nie widzi.** Jej wlasna miara wielkosci wsi ("hearth") jest prawie rowna wszedzie: 8 wsi na 10 ma miedzy 316 a 512. Miasta tak samo: w 8 miastach na 10 "dobrobyt" to 4 800-7 700, choc miasto ma od 2.5 tysiaca ludzi (Zelazne Wyspy) do 626 tysiecy (Qarth).
3. **Wies z symbolem zboza daje dzis wszedzie to samo: okolo 54 sztuki zboza dziennie.** W Reach stoi za nia srednio 167 tysiecy ludzi, na Polnocy 52 tysiace, w Darze niecale 2 tysiace - a wozy wyjezdzaja takie same. Na 1000 ludzi Reach wystawia na targ 0.11 sztuki zboza dziennie, Polnoc 0.33, Zelazne Wyspy 0.88, Nocna Straz 12.4.
4. **Dochod panow z ziemi idzie za liczba osad, nie ludzi.** Rachunek po krolestwach: kazda osada daje panom 224 zl dziennie, a kazde 1000 ludzi tylko 0.43 zl. Jedna osada wazy wiec tyle, co pol miliona ludzi. Reach ma 16% ludzi swiata i 11% dochodu z ziemi; Polnoc 6% ludzi i 12% dochodu.
5. **Zarobek mieszczan tez.** Miasto w Reach (117 tysiecy ludzi) zarabia 4.9 tys. zl dziennie, miasto Polnocy (27 tysiecy ludzi) 5.7 tys. Powod: zarobek liczy sie na "glowach BannerKings", a tych kazde miasto ma podobnie (10-25 tysiecy), nie na ludziach ksiegi.
6. **Wojsko i milicja tak samo.** Krolestwo trzyma okolo 203 zolnierzy na kazda osade i ani jednego wiecej za ludzi. Milicji jest okolo 93 na osade w kazdym krolestwie (86-100 w duzych).
7. **Trzy rzeczy juz dzis ida za ludzmi**: (a) ukryta ksiega jedzenia wsi (pole daje 1.2 racji na mieszkanca), (b) liczba rak w warsztatach miast, (c) renta "nalezna" panu - 40 zl od glowy rocznie. Ale z tej naleznej renty panowie dostaja 2.6%: wies placi z kasy, a kasa wsi napelnia sie z utargu woza, ktory jest taki sam w kazdej wsi.
8. **Twoj wniosek "zarobek idzie za liczba osad" jest potwierdzony liczbami** (rozdz. 3). Wyjatkiem jest tylko ukryta ksiega jedzenia.
9. **Gdyby wszystko podzielic wedlug ludzi (ta sama suma swiata)**: Reach, Volantis i Qarth mialyby 2.3-2.7 razy wiecej wojska i 2.0-2.4 razy wiecej pieniedzy panow niz dzis; wojsko Polnocy spadloby do 0.58 dzisiejszego, Dorne do 0.34, Zelaznych Wysp do 0.23, Sarnoru do 0.15, Nocnej Strazy do 0.01 (62 ludzi zamiast 5.8 tysiaca).
10. **Jedzenie wedlug ludzi**: przy polu dajacym 1.2 racji na wiesniaka wies zywi siebie i jednego mieszczanina na pieciu wiesniakow. Westeros ma wtedy nadwyzke (8-14%; krolestwo z Krolewska Przystania wychodzi na zero), a wielkie miasta Essos glod: Qarth -25%, Meereen -30%, Lys -33%. Swiat jako calosc: -4%. Wielkie miasta trzeba albo karmic morzem, albo zmniejszyc, albo podniesc plon.
11. **Gdzie prosta proporcja daje bzdure** (rozdz. 5.7): Nocna Straz i Sarnor (prawie bez ludzi, a z twierdzami), Zelazne Wyspy, Wolni Ludzie i Dothrakowie (walcza prawie wszyscy), 8 miast z zerem ludzi w ksiedze, kopalnie (zelazo ucieka z Zelaznych Wysp do Reach), wsie-karly (wies zboza w Darze dalaby 1 sztuke dziennie).
12. **Liczby ludzi sa dobre dla duzych krain Westeros, watpliwe w czterech miejscach**: miasta Essos (Qarth 626 tys., New Ghys 465 tys. - a Krolewska Przystan 259 tys., Oldtown 144 tys.), Duskendale 185 tys. przy White Harbor 33 tys., Nocna Straz (20 tys. ludzi, 5.8 tys. wojska), Sarnor (93 tys. ludzi, 5 twierdz, 2 tys. wojska).

## 1. Co dzis liczy sie "od ludzi", a co "od osady" (z kodu i logu)

To jest mapa do reszty dokumentu. Wyjasnia, DLACZEGO liczby w rozdz. 2-3 wychodza tak, a nie inaczej.

| zjawisko | od czego dzis zalezy | dowod |
|---|---|---|
| ludzie wsi | hearth wsi x "k" krainy (ludzi na punkt hearth); k ustawione raz, tak by suma dala ludnosc krainy z tabeli | PopulationLaw.cs:42-74 (tabela krain), LOG:209 (k: Reach 444, Polnoc 132, Dorne 88, Zelazne Wyspy 47, Sarnor 17, Nocna Straz 3 ludzi na punkt hearth) |
| ludzie miasta | stan zalozony raz przy starcie: dobrobyt x k krainy; potem juz nie idzie za dobrobytem | LOG:211 |
| produkcja wsi na targ (zboze, ryby, surowce) | OSADA: stawka typu wsi x (szczebel hearth + 1) x 0.5; szczebel zmienia sie dopiero przy hearth 200 i 600, wiec 532 z 569 wsi ma ten sam szczebel | DefaultVillageProductionCalculatorModel.cs:31; Village.cs:320-327; LOG:464 (czynny model: NavalDLC -> Default, nie BK); LOG:129262 (wsie wedlug progow: 13 / 532 / 24) |
| regulator wsi (surowce) | jeden mnoznik na towar dla CALEGO swiata (0.38-1.30), nie na region | HarvestValve.cs:16-36; LOG:119436 |
| ukryta ksiega jedzenia wsi (racje) | LUDZIE: plon dnia = 1.2 x ludzie okregu x q (q = stan wsi, zima, spustoszenie) | Famine.cs:21; Settings.cs:838; LOG:128976 |
| jedzenie miasta i zamku (zapas "Food" gry) | OSADA: model gry i BK, liczony od dobrobytu i ludnosci BK; nie od ludzi ksiegi | LR kolumny zywnosc, bilans_zywnosci; tabela 3.1 (r z osadami 0.96, z ludzmi 0.63) |
| zarobek i koszyk mieszczan (kiesa ludu) | GLOWY BK z kalibracji (W, R, P) x r, gdzie r = ludzie ksiegi dzis / ludzie ksiegi przy starcie; czyli liczba glow BK, a ksiega tylko ja przesuwa procentowo | TownFolk.cs:49 (koszyk na glowe), :74, :428-433 (Scale); LOG:129238 (10.36 mln ludzi ksiegi = 1.42 mln glow BK w miastach) |
| rece warsztatow | LUDZIE: 0.10 roboczodnia na 1000 ludzi rynku x poziom placy, z sufitem na miasto | WorkshopLaw.cs:792-804; LOG:305 (Reach 178 -> 888 rak, Polnoc 248 -> 333) |
| renta NALEZNA panu | LUDZIE: 40 zl od glowy rocznie = 5.66 mln zl dziennie w swiecie | PopulationLaw.cs:1038-1042; Settings.cs:734-735; LOG:128649 |
| renta ZAPLACONA przez wies | OSADA: najwyzej nalezna, ale nie wiecej niz czesc kasy wsi; kasa wsi bierze sie z utargu woza (produkcja osady) | PopulationLaw.cs:1258; LOG:129218 (utarg wsi 235 905 zl/d) |
| renta z miasta (zawor kasy) | OSADA: 7% nadwyzki kasy kupcow ponad zapas, 2/3 pan, 1/3 korona; zapas kupcow = 10 000 + 12 x dobrobyt | PopulationLaw.cs:1238-1240, :1294-1316; LOG:129149 |
| milicja | OSADA: wies +0.5 + hearth / 400 dziennie, twierdza +2 + dobrobyt / 1000, minus 2.5% stanu | DefaultSettlementMilitiaModel.cs:101-120 (wzor gry; nie sprawdzalem, czy BK go podmienia - dane: 93 na osade) |
| zalogi | OSADA: dzis srednio 461 ludzi na miasto i 268 na zamek (srednia krolestw z 28 dob; renta korony wymaga zalogi co najmniej 50% sredniej krolestwa). Wzoru gry na wielkosc zalogi nie czytalem - dane: r z liczba osad 0.85 (rody) | LOG:128744; LOG:598 (doba 1: 307 / 124) |
| pobor (hamulec) | LUDZIE: region odmawia ochotnika, gdy sluzba przekroczy 20% mezczyzn regionu (mezczyzni = 27% ludzi) | PeopleLedger.cs:40-41; LOG:128936 (377 odmow dziennie: Sarnor 328, Ibben 28, Krolewska Przystan 21) |
| pobor (podaz ochotnikow) | OSADA: miejsca u notabli (liczba notabli i ich sila), szansa BK x chec do sluzby | LOG:128936; BKVolunteerModel (nie czytalem w calosci - nie wiem, jaki ma wzor na osade) |

Wniosek z mapy: ludzie sa w grze policzeni i trzy ksiegi juz od nich licza, ale wszystko, co daje TOWAR NA TARG i ZLOTO (produkcja wsi, utarg, kasa wsi i miasta, kiesa mieszczan), stoi na liczbach gry rownych dla kazdej osady. Dlatego renta "od glowy" jest tylko sufitem, do ktorego nikt nie dochodzi.

## 2. Tabele krolestw i regionow

### 2.1 Stan: ludzie, osady, hearth, dobrobyt [P]

Zrodlo: LR (ludzie, hearth, dobrobyt), BK (ludnosc BK = miasto + zamek + wsie regionu), s28.

| krolestwo | ludzie tys. | w miastach tys. | na wsi tys. | miast | zamkow | wsi | hearth na wies | dobrobyt na miasto | ludzi na wies tys. | ludzi na miasto tys. | ludnosc BK tys. |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| The Reach | 8 346 | 818 | 7 528 | 7 | 13 | 49 | 406 | 6 567 | 153.6 | 116.8 | 233 |
| Volantis | 4 994 | 1 248 | 3 746 | 4 | 3 | 18 | 378 | 6 066 | 208.1 | 311.9 | 104 |
| King's Landing | 4 786 | 789 | 3 998 | 6 | 15 | 49 | 393 | 6 973 | 81.6 | 131.4 | 252 |
| Qarth | 3 983 | 1 474 | 2 509 | 3 | 1 | 11 | 366 | 7 138 | 228.1 | 491.4 | 68 |
| The North | 3 226 | 269 | 2 957 | 10 | 13 | 60 | 364 | 6 390 | 49.3 | 26.9 | 286 |
| The Vale | 3 222 | 207 | 3 015 | 6 | 8 | 34 | 402 | 6 250 | 88.7 | 34.5 | 180 |
| Stormlands | 2 756 | 122 | 2 635 | 4 | 13 | 39 | 413 | 7 151 | 67.6 | 30.4 | 171 |
| Riverlands | 2 740 | 182 | 2 558 | 6 | 6 | 32 | 407 | 6 817 | 79.9 | 30.4 | 173 |
| Braavos | 2 594 | 796 | 1 798 | 2 | 4 | 15 | 361 | 7 191 | 119.9 | 397.9 | 68 |
| Targaryen (Meereen) | 1 974 | 813 | 1 161 | 4 | 2 | 17 | 351 | 6 132 | 68.3 | 203.2 | 90 |
| Pentos | 1 488 | 491 | 997 | 2 | 3 | 13 | 383 | 7 298 | 76.7 | 245.3 | 56 |
| Qohor | 1 487 | 492 | 995 | 2 | 3 | 13 | 394 | 6 450 | 76.5 | 245.9 | 67 |
| Norvos | 1 480 | 594 | 885 | 2 | 3 | 12 | 373 | 6 868 | 73.8 | 297.1 | 74 |
| Dorne | 1 116 | 91 | 1 024 | 4 | 8 | 29 | 401 | 7 000 | 35.3 | 22.8 | 135 |
| Tyrosh | 1 066 | 394 | 672 | 2 | 4 | 14 | 401 | 6 553 | 48.0 | 197.1 | 64 |
| Targaryen (Aegon) | 1 031 | 373 | 658 | 3 | 1 | 11 | 346 | 6 047 | 59.8 | 124.3 | 73 |
| Myr | 980 | 397 | 584 | 2 | 3 | 12 | 351 | 6 596 | 48.6 | 198.3 | 68 |
| Lys | 888 | 394 | 494 | 2 | 2 | 10 | 409 | 6 553 | 49.4 | 197.0 | 58 |
| Dothraki Horde | 733 | 0 | 733 | 2 | 5 | 17 | 366 | 6 530 | 43.1 | 0.0 | 71 |
| Lorath | 674 | 243 | 430 | 2 | 2 | 10 | 347 | 6 606 | 43.0 | 121.7 | 53 |
| Dragonstone | 509 | 36 | 473 | 4 | 2 | 15 | 356 | 6 762 | 31.5 | 9.1 | 77 |
| Iron Islands | 469 | 22 | 447 | 6 | 4 | 28 | 334 | 5 346 | 16.0 | 3.6 | 124 |
| Ibben | 288 | 67 | 221 | 3 | 1 | 9 | 307 | 4 997 | 24.6 | 22.4 | 60 |
| Free Folk | 288 | 0 | 288 | 3 | 4 | 18 | 383 | 6 182 | 16.0 | 0.0 | 79 |
| Summer Isles | 108 | 11 | 97 | 1 | 0 | 3 | 50 | 1 982 | 32.5 | 10.6 | 15 |
| Yi Ti Exiles | 100 | 20 | 80 | 1 | 0 | 3 | 50 | 2 094 | 26.8 | 19.7 | 19 |
| Sarnor | 93 | 19 | 74 | 2 | 3 | 13 | 336 | 6 707 | 5.7 | 9.5 | 61 |
| Skagos | 50 | 0 | 50 | 1 | 0 | 4 | 314 | 5 236 | 12.6 | 0.0 | 16 |
| Nights Watch | 20 | 0 | 20 | 1 | 4 | 11 | 488 | 7 387 | 1.8 | 0.0 | 49 |
| **SWIAT** | 51 489 | 10 361 | 41 128 | 97 | 130 | 569 | 378 | 6 383 | 72.3 | 106.8 | 2 846 |

Co widac:
- Hearth na wies: 307-488 w kazdym krolestwie z normalnymi wsiami (Wyspy Letnie i Yi Ti maja po 50 - to osady poza glowna mapa). Dobrobyt na miasto: 5 000-7 400.
- Ludzi na wies: od 1.8 tys. (Nocna Straz) i 5.7 tys. (Sarnor) do 208 tys. (Volantis) i 228 tys. (Qarth). Ludzi na miasto: od 3.6 tys. (Zelazne Wyspy) do 491 tys. (Qarth).
- Ludnosc BK calego swiata to 2.85 mln - 18 razy mniej niz ksiega (51.5 mln) - i tez jest rowna na osade: Reach 233 tys., Polnoc 286 tys.

### 2.2 Produkcja wsi na targ [S] i ukryta ksiega jedzenia wsi [P]

**Uwaga o zrodle.** Log NIE dzieli produkcji towarow na regiony. Linie "Towary:" (LOG:128999-129046 dla doby 364) podaja swiat: kazdy towar, ile dal kazdy TYP wsi. Podzial na krolestwa to moj szacunek [S]: produkcje swiata danego typu wsi rozdzielilem na wsie tego typu wedlug wzoru gry (szczebel hearth + 1) - skrypt d11. Typ wsi i warownia: `Modules\ROT-Map\ModuleData\settlements.xml` + 28 podmian z linii "Klimat wsi (T8)" (LOG:269). Dokladnosc: 90.8% wartosci przydzielono wprost po typie wsi; 4 wsie z 571 maja w grze inny typ niz w pliku (gra: 23 winnice, plik: 27 - przyczyny nie znam); stanu "spladrowana" ani obnizek, ktore nasz mod naklada na wynik wsi (ubytek rak po spustoszeniu, zima - MaterialLaw.cs:27-34), nie uwzgledniam. Ich laczna waga jest mala: wies zboza daje w swiecie srednio 52.2 szt. dziennie (6 419 szt. ze 123 wsi, s28), a model dla takiej wsi na zwyklym szczeblu daje 55.0 (LOG:464) - wszystkie obnizki razem zdejmuja ok. 5%; lokalnie, w pasie wojny, moga zdjac wiecej. Sumy swiata sa [P].

Produkcja wsi swiata, towar po towarze (s28) [P]; "wartosc bazowa" = Value przedmiotu z linii "Towary" (GoodsLedger.cs:970):

| towar | wsie swiata szt./d | wartosc bazowa zl | wartosc zl/d | szt. na 1000 ludzi na ROK | zl na 1000 ludzi na dobe |
|---|---:|---:|---:|---:|---:|
| cotton | 59.9 | 1000 | 59 893 | 0.42 | 1.16 |
| wool | 335.8 | 83 | 27 873 | 2.37 | 0.54 |
| grain | 8 637.8 | 3 | 25 913 | 61.07 | 0.50 |
| fish | 2 315.5 | 10 | 23 155 | 16.37 | 0.45 |
| flax | 507.6 | 20 | 10 152 | 3.59 | 0.20 |
| iron | 251.8 | 38 | 9 567 | 1.78 | 0.19 |
| cow | 84.6 | 113 | 9 561 | 0.60 | 0.19 |
| hardwood | 2 186.5 | 4 | 8 746 | 15.46 | 0.17 |
| date_fruit | 121.4 | 60 | 7 284 | 0.86 | 0.14 |
| butter | 286.4 | 24 | 6 873 | 2.02 | 0.13 |
| hog | 193.7 | 30 | 5 811 | 1.37 | 0.11 |
| cheese | 368.2 | 13 | 4 787 | 2.60 | 0.09 |
| fur | 14.1 | 200 | 2 821 | 0.10 | 0.05 |
| sheep | 157.9 | 17 | 2 684 | 1.12 | 0.05 |
| spice | 7.0 | 270 | 1 890 | 0.05 | 0.04 |
| silver | 86.5 | 20 | 1 730 | 0.61 | 0.03 |
| olives | 257.1 | 5 | 1 286 | 1.82 | 0.02 |
| salt | 545.8 | 2 | 1 092 | 3.86 | 0.02 |
| grape | 270.6 | 3 | 812 | 1.91 | 0.02 |
| Papyrus | 6.4 | 100 | 636 | 0.04 | 0.01 |
| mead | 46.2 | 10 | 462 | 0.33 | 0.01 |
| gold_ore | 8.1 | 50 | 407 | 0.06 | 0.01 |
| clay | 343.9 | 1 | 344 | 2.43 | 0.01 |
| Egg | 42.9 | 8 | 343 | 0.30 | 0.01 |
| walrus_tusk | 0.8 | 300 | 246 | 0.01 | 0.00 |
| whale_oil | 5.3 | 24 | 127 | 0.04 | 0.00 |
| limestone | 94.4 | 1 | 94 | 0.67 | 0.00 |
| marble | 42.6 | 2 | 85 | 0.30 | 0.00 |
| **RAZEM** |  |  | 214 673 |  | 4.17 |

- Wsie swiata wystawiaja towar za 214.7 tys. zl dziennie = 377 zl na wies = 4.17 zl na 1000 ludzi dziennie [P]. Bawelna to 28% tej wartosci (60 szt. po 1000 zl).
- Zboze: 8 638 szt. dziennie = 0.17 szt. na 1000 ludzi dziennie = 61 szt. na 1000 ludzi rocznie [P]. Cala zywnosc ze wsi (zboze, ryby, nabial, owoce): 12 300 szt. dziennie [P].
- Skala: w partii 1 szt. zywnosci to 20 osobodni (DefaultMobilePartyFoodConsumptionModel.cs:16). Cala zywnosc targu karmi wiec 246 tys. ludzi dziennie = 2.4% mieszczan ksiegi [S]. Towar na targu jest symbolem nadwyzki, nie jedzeniem 51 mln ludzi.

Krolestwa (s28):

| krolestwo | zboze szt./d [S] | ryby szt./d [S] | inna zywnosc szt./d [S] | wartosc zl/d [S] | bez bawelny zl/d [S] | zl na wies [S] | zl na 1000 ludzi [S] | zboze na 1000 ludzi [S] | plon ksiegi tys. racji/d [P] | zjedli tys. racji/d [P] | plon na 1000 ludzi wsi [P] |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| The Reach | 943 | 177 | 168 | 25 911 | 13 076 | 529 | 3.10 | 0.11 | 9 012 | 7 532 | 1 197 |
| Volantis | 230 | 70 | 48 | 17 310 | 4 476 | 962 | 3.47 | 0.05 | 4 446 | 3 748 | 1 187 |
| King's Landing | 798 | 162 | 55 | 10 287 | 10 287 | 210 | 2.15 | 0.17 | 4 619 | 4 061 | 1 155 |
| Qarth | 51 | 5 | 18 | 11 173 | 2 617 | 1 016 | 2.81 | 0.01 | 2 992 | 2 512 | 1 193 |
| The North | 1 066 | 130 | 103 | 15 135 | 15 135 | 252 | 4.69 | 0.33 | 3 548 | 2 957 | 1 200 |
| The Vale | 611 | 200 | 81 | 12 801 | 12 801 | 376 | 3.97 | 0.19 | 3 618 | 3 015 | 1 200 |
| Stormlands | 374 | 171 | 134 | 10 368 | 10 368 | 266 | 3.76 | 0.14 | 3 162 | 2 650 | 1 200 |
| Riverlands | 567 | 46 | 120 | 7 795 | 7 795 | 244 | 2.84 | 0.21 | 3 070 | 2 560 | 1 200 |
| Braavos | 214 | 68 | 11 | 4 686 | 4 686 | 312 | 1.81 | 0.08 | 2 110 | 1 802 | 1 173 |
| Targaryen (Meereen) | 320 | 39 | 29 | 11 490 | 7 212 | 676 | 5.82 | 0.16 | 1 333 | 1 173 | 1 148 |
| Pentos | 309 | 52 | 42 | 3 465 | 3 465 | 267 | 2.33 | 0.21 | 1 179 | 1 001 | 1 182 |
| Qohor | 107 | 37 | 27 | 1 807 | 1 807 | 139 | 1.22 | 0.07 | 1 183 | 998 | 1 189 |
| Norvos | 201 | 6 | 26 | 3 084 | 3 084 | 257 | 2.08 | 0.14 | 1 043 | 891 | 1 178 |
| Dorne | 433 | 212 | 116 | 7 025 | 7 025 | 242 | 6.30 | 0.39 | 1 218 | 1 026 | 1 189 |
| Tyrosh | 165 | 37 | 60 | 4 724 | 4 724 | 337 | 4.43 | 0.15 | 807 | 675 | 1 200 |
| Targaryen (Aegon) | 98 | 36 | 30 | 11 701 | 3 145 | 1 064 | 11.35 | 0.09 | 757 | 660 | 1 152 |
| Myr | 204 | 67 | 19 | 14 663 | 1 829 | 1 222 | 14.96 | 0.21 | 695 | 592 | 1 190 |
| Lys | 145 | 35 | 44 | 3 313 | 3 313 | 331 | 3.73 | 0.16 | 577 | 498 | 1 169 |
| Dothraki Horde | 175 | 39 | 46 | 4 214 | 4 214 | 248 | 5.75 | 0.24 | 878 | 736 | 1 198 |
| Lorath | 244 | 66 | 5 | 2 618 | 2 618 | 262 | 3.89 | 0.36 | 515 | 433 | 1 196 |
| Dragonstone | 264 | 159 | 26 | 5 074 | 5 074 | 338 | 9.96 | 0.52 | 557 | 498 | 1 178 |
| Iron Islands | 414 | 196 | 28 | 8 347 | 8 347 | 298 | 17.81 | 0.88 | 537 | 447 | 1 200 |
| Ibben | 112 | 44 | 13 | 1 873 | 1 873 | 208 | 6.50 | 0.39 | 265 | 221 | 1 200 |
| Free Folk | 82 | 131 | 24 | 4 347 | 4 347 | 241 | 15.11 | 0.28 | 345 | 288 | 1 200 |
| Summer Isles | 7 | 16 | 1 | 289 | 289 | 96 | 2.67 | 0.06 | 117 | 97 | 1 200 |
| Yi Ti Exiles | 32 | 1 | 1 | 264 | 264 | 88 | 2.64 | 0.32 | 96 | 80 | 1 200 |
| Sarnor | 209 | 67 | 31 | 4 519 | 4 519 | 348 | 48.36 | 2.23 | 86 | 77 | 1 157 |
| Skagos | 19 | 42 | 6 | 2 101 | 2 101 | 525 | 41.61 | 0.38 | 61 | 50 | 1 200 |
| Nights Watch | 246 | 5 | 34 | 4 290 | 4 290 | 390 | 216.84 | 12.41 | 15 | 20 | 774 |
| **SWIAT** | 8 638 | 2 315 | 1 347 | 214 673 | 154 781 | 377 | 4.17 | 0.17 | 48 839 | 41 299 | 1 188 |

Co widac:
- **Na wies** produkcja jest podobna: bez bawelny 140-420 zl na wies w duzych krolestwach. **Na 1000 ludzi** rozrzut jest ogromny: Qarth 2.81 zl (bez bawelny 0.66), Reach 3.10, Polnoc 4.69, Dorne 6.30, Zelazne Wyspy 17.81, Sarnor 48.4, Nocna Straz 216.8.
- Zboze na 1000 ludzi: Qarth 0.01, Volantis 0.05, Reach 0.11, Polnoc 0.33, Zelazne Wyspy 0.88, Sarnor 2.23, Nocna Straz 12.41. Polnoc daje na targ wiecej zboza (1 066 szt.) niz Reach (943 szt.), bo ma wiecej wsi z symbolem zboza.
- **Ksiega racji** (ostatnie trzy kolumny) jest odwrotna: plon to ok. 1 150-1 200 racji na 1000 ludzi wsi w kazdym krolestwie poza Nocna Straza (774). Wzor obniza plon przy zimie, spustoszeniu i wsi poza stanem Normal (Famine.cs:21-24); ktora z tych przyczyn dziala w Darze - nie sprawdzalem. Swiat: plon 48.8 mln racji, zjedzone 41.3 mln, nadwyzka 7.5 mln racji dziennie [P]. Ta nadwyzka nie trafia nigdzie: spichlerze sa pelne (726 dni potrzeby) i 7.28 mln racji dziennie przepada "ponad sufit" (LOG:128976).

### 2.3 Spozycie i zywnosc warowni [P]

Log nie podaje zuzycia towarow na region. Swiat (linie "Towary", s28) [P]: zywnosc z targu zjadaja mieszczanie i zamki 10 208 szt. dziennie, partie 2 029, przerob (piekarnie, browary, rzemieslnicy BK) 1 579. Na region sa trzy inne miary: zapas i bilans zywnosci warowni (LR: liczba "Food" gry), wydatek ludu na jedzenie w zl (KL) i wartosc zuzycia BK (BK).

| krolestwo | zapas zywnosci warowni | bilans zywnosci/d | bilans na 1000 ludzi | lud na jedzenie zl/d | wydatek gry zl/d | zuzycie BK (wartosc)/d | jedzenie zl na 1000 ludzi | jedzenie zl na 1000 ludzi miast |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| The Reach | 327 569 | 2 259 | 0.27 | 10 424 | 38 678 | 38 794 | 1.25 | 12.75 |
| Volantis | 156 891 | 517 | 0.10 | 4 276 | 14 870 | 16 632 | 0.86 | 3.43 |
| King's Landing | 352 062 | 2 723 | 0.57 | 12 347 | 47 516 | 48 166 | 2.58 | 15.66 |
| Qarth | 105 795 | 115 | 0.03 | 4 272 | 16 011 | 17 059 | 1.07 | 2.90 |
| The North | 425 252 | 3 073 | 0.95 | 15 853 | 48 938 | 49 546 | 4.91 | 58.86 |
| The Vale | 254 795 | 1 993 | 0.62 | 11 886 | 39 106 | 36 932 | 3.69 | 57.45 |
| Stormlands | 237 774 | 1 565 | 0.57 | 7 302 | 25 032 | 27 463 | 2.65 | 59.96 |
| Riverlands | 247 957 | 1 455 | 0.53 | 10 827 | 31 357 | 29 863 | 3.95 | 59.38 |
| Braavos | 96 159 | 607 | 0.23 | 3 362 | 24 024 | 22 668 | 1.30 | 4.22 |
| Targaryen (Meereen) | 135 732 | 351 | 0.18 | 4 824 | 15 740 | 16 242 | 2.44 | 5.93 |
| Pentos | 78 142 | 548 | 0.37 | 3 986 | 12 241 | 12 325 | 2.68 | 8.12 |
| Qohor | 94 326 | 448 | 0.30 | 3 515 | 9 779 | 13 612 | 2.36 | 7.15 |
| Norvos | 108 869 | 703 | 0.48 | 4 007 | 16 362 | 14 882 | 2.71 | 6.74 |
| Dorne | 206 807 | 1 229 | 1.10 | 7 587 | 30 920 | 29 988 | 6.80 | 83.09 |
| Tyrosh | 99 445 | 394 | 0.37 | 3 100 | 10 548 | 10 230 | 2.91 | 7.86 |
| Targaryen (Aegon) | 113 651 | 345 | 0.33 | 4 521 | 14 039 | 13 445 | 4.39 | 12.12 |
| Myr | 102 442 | 444 | 0.45 | 3 775 | 15 658 | 15 901 | 3.85 | 9.52 |
| Lys | 88 161 | 292 | 0.33 | 2 080 | 5 354 | 6 701 | 2.34 | 5.28 |
| Dothraki Horde | 108 183 | 366 | 0.50 | 3 154 | 13 452 | 13 632 | 4.30 | 0.00 |
| Lorath | 86 499 | 301 | 0.45 | 2 786 | 15 125 | 13 542 | 4.14 | 11.45 |
| Dragonstone | 124 470 | 866 | 1.70 | 6 206 | 19 894 | 19 438 | 12.19 | 170.79 |
| Iron Islands | 178 924 | 800 | 1.71 | 5 962 | 19 196 | 20 062 | 12.72 | 276.19 |
| Ibben | 92 497 | 214 | 0.74 | 3 262 | 6 572 | 6 684 | 11.32 | 48.57 |
| Free Folk | 103 264 | 544 | 1.89 | 3 380 | 11 126 | 12 292 | 11.75 | 0.00 |
| Summer Isles | 28 783 | 98 | 0.90 | 322 | 1 373 | 1 535 | 2.98 | 30.45 |
| Yi Ti Exiles | 35 136 | 184 | 1.84 | 268 | 694 | 702 | 2.68 | 13.58 |
| Sarnor | 91 630 | 706 | 7.55 | 3 306 | 18 210 | 19 348 | 35.38 | 173.22 |
| Skagos | 22 335 | 304 | 6.02 | 963 | 2 524 | 1 541 | 19.08 | 0.00 |
| Nights Watch | 69 354 | 401 | 20.26 | 1 749 | 6 655 | 6 390 | 88.39 | 0.00 |
| **SWIAT** | 4 172 905 | 23 843 | 0.46 | 149 304 | 530 991 | 535 618 | 2.90 | 14.41 |

Co widac:
- Bilans zywnosci warowni jest dodatni w kazdym krolestwie (zero glodu w roku - zgodnie z raportem rocznym 3.1). Ale idzie za liczba twierdz, nie ludzi: Polnoc +3 073 dziennie, Reach +2 259, Qarth +115.
- Wydatek mieszczan na jedzenie na 1000 ludzi MIASTA: Qarth 2.90 zl, Volantis 3.43, Braavos 4.22 wobec Polnocy 58.9, Dorne 83.1, Zelaznych Wysp 276. Mieszczanin Qarthu "zjada" 20-95 razy mniej niz mieszczanin Westeros - bo liczba jedzacych to glowy BK, nie ludzie ksiegi.

### 2.4 Zarobek ludu miast i podzamczy [P]

Zrodlo: KL (4 probki), kolumny ludzie_bk_kal, dochod, koszyk, wydatki, kiesy. Wsie maja osobna kiese wsi - w tej tabeli ich nie ma (swiat: do kies wsi wplywa 294.8 tys. zl dziennie, z tego utarg 235.9 tys. - LOG:129218, d364).

| krolestwo | glowy BK w kiesie | dochod zl/d | koszyk (potrzeba) zl/d | wydatki zl/d | kiesy tys. zl | dochod na 1000 ludzi | dochod na 1000 ludzi miast | dochod na glowe BK | dochod na miasto zl/d |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| The Reach | 137 921 | 37 737 | 150 000 | 36 268 | 3 022 | 4.52 | 46.14 | 0.27 | 4 903 |
| Volantis | 69 486 | 26 490 | 68 963 | 13 181 | 2 095 | 5.30 | 21.23 | 0.38 | 6 405 |
| King's Landing | 144 844 | 46 642 | 135 748 | 47 316 | 4 864 | 9.74 | 59.15 | 0.32 | 7 026 |
| Qarth | 54 757 | 16 169 | 64 034 | 20 212 | 1 505 | 4.06 | 10.97 | 0.30 | 4 929 |
| The North | 191 063 | 60 760 | 229 667 | 48 046 | 5 745 | 18.83 | 225.60 | 0.32 | 5 723 |
| The Vale | 114 406 | 47 220 | 132 643 | 40 131 | 3 078 | 14.66 | 228.25 | 0.41 | 7 323 |
| Stormlands | 96 428 | 22 868 | 109 448 | 25 049 | 1 848 | 8.30 | 187.75 | 0.24 | 4 717 |
| Riverlands | 111 501 | 35 885 | 133 113 | 33 642 | 2 763 | 13.09 | 196.80 | 0.32 | 5 543 |
| Braavos | 41 155 | 25 144 | 37 097 | 28 102 | 358 | 9.69 | 31.60 | 0.61 | 11 942 |
| Targaryen (Meereen) | 60 435 | 28 506 | 65 002 | 20 210 | 2 645 | 14.44 | 35.06 | 0.47 | 6 494 |
| Pentos | 32 996 | 10 244 | 37 011 | 12 230 | 176 | 6.89 | 20.88 | 0.31 | 4 529 |
| Qohor | 35 969 | 12 059 | 37 033 | 11 624 | 1 656 | 8.11 | 24.52 | 0.34 | 5 668 |
| Norvos | 47 999 | 16 036 | 39 670 | 14 822 | 354 | 10.84 | 26.98 | 0.33 | 7 615 |
| Dorne | 90 371 | 34 081 | 89 660 | 34 118 | 2 139 | 30.55 | 373.25 | 0.38 | 6 686 |
| Tyrosh | 36 891 | 8 382 | 34 722 | 7 890 | 263 | 7.86 | 21.26 | 0.23 | 3 158 |
| Targaryen (Aegon) | 53 767 | 12 566 | 60 852 | 15 988 | 1 515 | 12.19 | 33.69 | 0.23 | 4 003 |
| Myr | 43 150 | 18 546 | 34 050 | 17 342 | 1 097 | 18.92 | 46.76 | 0.43 | 8 682 |
| Lys | 40 838 | 6 183 | 37 721 | 7 744 | 97 | 6.97 | 15.69 | 0.15 | 2 495 |
| Dothraki Horde | 41 660 | 18 967 | 39 130 | 11 845 | 3 470 | 25.88 | 0.00 | 0.46 | 8 164 |
| Lorath | 37 776 | 12 620 | 38 091 | 11 600 | 701 | 18.73 | 51.86 | 0.33 | 5 816 |
| Dragonstone | 61 451 | 23 766 | 75 541 | 22 264 | 662 | 46.67 | 654.11 | 0.39 | 5 693 |
| Iron Islands | 85 742 | 21 968 | 101 865 | 19 591 | 2 689 | 46.86 | 1 017.65 | 0.26 | 3 475 |
| Ibben | 45 575 | 5 431 | 66 628 | 7 288 | 1 090 | 18.85 | 80.86 | 0.12 | 1 652 |
| Free Folk | 50 477 | 9 285 | 60 615 | 9 800 | 420 | 32.28 | 0.00 | 0.18 | 2 700 |
| Summer Isles | 14 173 | 1 368 | 11 736 | 1 046 | 404 | 12.66 | 129.21 | 0.10 | 1 368 |
| Yi Ti Exiles | 16 202 | 1 324 | 14 703 | 396 | 621 | 13.25 | 67.18 | 0.08 | 1 324 |
| Sarnor | 38 724 | 19 254 | 34 395 | 16 417 | 736 | 206.04 | 1 008.92 | 0.50 | 8 834 |
| Skagos | 10 947 | 1 576 | 17 207 | 1 666 | 16 | 31.21 | 0.00 | 0.14 | 1 576 |
| Nights Watch | 25 492 | 5 524 | 25 784 | 6 784 | 672 | 279.19 | 0.00 | 0.22 | 4 761 |
| **SWIAT** | 1 832 196 | 586 600 | 1 982 129 | 542 610 | 46 701 | 11.39 | 56.62 | 0.32 | 5 509 |

Co widac:
- Lud zarabia 586.6 tys. zl dziennie (miasta 534.4 tys., podzamcza 52.2 tys.), a "koszyk" (to, czego potrzebuje) to 1.98 mln - zgodnie z raportem rocznym (polki puste).
- Na miasto: 2.5-8.8 tys. zl w 24 z 29 krolestw (Braavos 11.9 tys.; cztery male krolestwa 1.3-1.7 tys.). Na glowe BK: 0.23-0.47 zl w duzych krolestwach (Braavos 0.61). Na 1000 ludzi ksiegi: Qarth 4.06, Reach 4.52, Volantis 5.30 wobec Polnocy 18.8, Dorne 30.6, Zelaznych Wysp 46.9, Sarnoru 206, Nocnej Strazy 279.
- 21 z 97 miast ma wiecej glow BK niz ludzi w ksiedze (m.in. wszystkie 6 miast Zelaznych Wysp: 2.5-4.6 tys. ludzi ksiegi przy 9.8-19 tys. glow BK).

### 2.5 Podatki, zawor kas miast i dochod rodow z ziemi [P]

Zrodlo: KM (zawor_do_pana, do_korony), BUD (d_ziemia = podatek wsi + zawor kas miast i zamkow + cla; d_korona; wplyw_doby).

| krolestwo | zawor do pana zl/d | zawor do korony zl/d | ziemia s28 zl/d | ziemia srednia roku | od korony zl/d | wplyw rodow zl/d | ziemia na 1000 ludzi | wplyw na 1000 ludzi | ziemia na osade | ziemia na twierdze |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| The Reach | 13 815 | 11 161 | 17 639 | 19 557 | 35 576 | 82 680 | 2.11 | 9.91 | 256 | 882 |
| Volantis | 6 741 | 5 189 | 6 140 | 8 603 | 15 691 | 58 951 | 1.23 | 11.80 | 246 | 877 |
| King's Landing | 10 676 | 9 192 | 16 508 | 22 129 | 29 364 | 100 128 | 3.45 | 20.92 | 236 | 786 |
| Qarth | 5 159 | 4 036 | 3 941 | 5 234 | 13 171 | 39 675 | 0.99 | 9.96 | 263 | 985 |
| The North | 21 206 | 18 462 | 19 367 | 26 033 | 64 359 | 108 906 | 6.00 | 33.76 | 233 | 842 |
| The Vale | 11 189 | 8 921 | 12 698 | 17 908 | 37 877 | 68 251 | 3.94 | 21.18 | 265 | 907 |
| Stormlands | 8 779 | 6 758 | 10 480 | 14 165 | 26 417 | 58 681 | 3.80 | 21.29 | 187 | 616 |
| Riverlands | 8 823 | 6 836 | 10 856 | 15 467 | 23 920 | 70 197 | 3.96 | 25.62 | 247 | 905 |
| Braavos | 2 059 | 1 586 | 2 571 | 3 975 | 6 408 | 17 016 | 0.99 | 6.56 | 122 | 429 |
| Targaryen (Meereen) | 3 556 | 2 878 | 4 941 | 7 452 | 19 695 | 90 170 | 2.50 | 45.67 | 215 | 823 |
| Pentos | 2 433 | 1 874 | 2 809 | 3 404 | 5 364 | 22 380 | 1.89 | 15.04 | 156 | 562 |
| Qohor | 2 176 | 1 675 | 2 719 | 4 347 | 8 508 | 20 403 | 1.83 | 13.72 | 151 | 544 |
| Norvos | 3 645 | 2 804 | 3 814 | 4 347 | 7 261 | 22 289 | 2.58 | 15.06 | 224 | 763 |
| Dorne | 9 585 | 7 431 | 10 086 | 12 172 | 34 807 | 103 143 | 9.04 | 92.45 | 246 | 840 |
| Tyrosh | 985 | 759 | 2 417 | 3 790 | 7 379 | 29 072 | 2.27 | 27.26 | 121 | 403 |
| Targaryen (Aegon) | 2 438 | 1 878 | 3 607 | 5 068 | 7 505 | 36 738 | 3.50 | 35.65 | 240 | 902 |
| Myr | 3 095 | 2 382 | 2 842 | 5 348 | 8 634 | 27 694 | 2.90 | 28.25 | 167 | 568 |
| Lys | 380 | 294 | 1 959 | 3 645 | 4 026 | 19 409 | 2.21 | 21.86 | 140 | 490 |
| Dothraki Horde | 6 891 | 6 060 | 5 381 | 7 871 | 35 210 | 56 168 | 7.34 | 76.64 | 224 | 769 |
| Lorath | 3 634 | 2 797 | 4 042 | 4 290 | 8 565 | 29 863 | 6.00 | 44.33 | 289 | 1 011 |
| Dragonstone | 1 301 | 1 184 | 3 665 | 6 818 | 12 368 | 40 763 | 7.20 | 80.05 | 175 | 611 |
| Iron Islands | 4 291 | 3 306 | 5 346 | 9 062 | 14 736 | 29 994 | 11.40 | 63.99 | 141 | 535 |
| Ibben | 406 | 315 | 938 | 2 086 | 2 925 | 12 919 | 3.25 | 44.83 | 72 | 234 |
| Free Folk | 108 | 108 | 2 387 | 4 340 | 5 085 | 11 274 | 8.30 | 39.20 | 95 | 341 |
| Summer Isles | 233 | 180 | 129 | 511 | 1 734 | 7 913 | 1.19 | 73.23 | 32 | 129 |
| Yi Ti Exiles | 173 | 134 | 208 | 529 | 1 722 | 2 098 | 2.08 | 20.99 | 52 | 208 |
| Sarnor | 3 151 | 2 425 | 3 164 | 4 257 | 10 079 | 26 115 | 33.86 | 279.45 | 176 | 633 |
| Skagos | 74 | 58 | 228 | 800 | 1 202 | 5 246 | 4.52 | 103.89 | 46 | 228 |
| Nights Watch | 2 053 | 2 054 | 2 473 | 3 233 | 6 937 | 15 288 | 124.99 | 772.76 | 155 | 495 |
| **SWIAT** | 139 054 | 112 732 | 163 355 | 226 443 | 456 521 | 1 213 424 | 3.17 | 23.57 | 205 | 720 |

Co widac:
- **Dochod z ziemi na osade** jest prawie staly: 233-265 zl dziennie w siedmiu z osmiu najwiekszych krolestw (Stormlands 187). **Na 1000 ludzi**: Qarth i Braavos 0.99, Volantis 1.23, Reach 2.11, Polnoc 6.00, Dorne 9.04, Zelazne Wyspy 11.4, Sarnor 33.9, Nocna Straz 125.
- Swiat: 163.4 tys. zl dziennie z ziemi = 3.17 zl na 1000 ludzi dziennie = **1.15 zl od glowy rocznie**. Ustawienie "renta od glowy" to 40 zl rocznie (Settings.cs:735).

Renta nalezna od glowy wobec zaplaconej, wedlug krain (linia "Ludnosc:", s28) [P]:

| kraina (kultura osady) | ludzie mln | renta nalezna zl/d | zaplacona zl/d | % naleznej | zaplacone na 1000 ludzi |
|---|---:|---:|---:|---:|---:|
| Reach (reach) | 8.00 | 878 909 | 12 565 | 1.4 | 1.57 |
| Volantis (volantine) | 4.99 | 548 797 | 6 974 | 1.3 | 1.40 |
| Qarth (qartheen) | 3.98 | 437 699 | 5 331 | 1.2 | 1.34 |
| Westerlands (vlandia) | 3.94 | 433 208 | 6 629 | 1.5 | 1.68 |
| Dorzecze (river) | 3.47 | 381 448 | 15 172 | 4.0 | 4.37 |
| Dolina (vale) | 3.00 | 329 405 | 11 290 | 3.4 | 3.76 |
| Polnoc (battania) | 2.99 | 328 426 | 21 434 | 6.5 | 7.17 |
| Zatoka Niewolnicza (ghiscari) | 2.96 | 324 890 | 5 999 | 1.8 | 2.03 |
| Braavos (empire) | 2.49 | 273 224 | 2 333 | 0.9 | 0.94 |
| Krainy Burzy (stormlands) | 2.48 | 272 130 | 9 474 | 3.5 | 3.82 |
| Pentos (pentoshi) | 1.49 | 163 466 | 2 685 | 1.6 | 1.80 |
| Qohor (qohorik) | 1.49 | 163 370 | 2 426 | 1.5 | 1.63 |
| Norvos (norvos) | 1.48 | 162 592 | 3 884 | 2.4 | 2.62 |
| Dorne (aserai) | 1.46 | 160 783 | 12 614 | 7.8 | 8.64 |
| Myr (myrish) | 0.98 | 107 738 | 3 210 | 3.0 | 3.28 |
| Tyrosh (tyroshi) | 0.98 | 107 620 | 1 193 | 1.1 | 1.22 |
| Lys (lyseni) | 0.98 | 107 133 | 602 | 0.6 | 0.62 |
| Ziemie Korony (dragonstone) | 0.79 | 86 754 | 1 669 | 1.9 | 2.11 |
| Lorath (nord) | 0.78 | 85 889 | 3 904 | 4.5 | 5.01 |
| Dothrakowie (khuzait) | 0.73 | 80 540 | 7 262 | 9.0 | 9.95 |
| Krolewska Przystan (crownlands) | 0.58 | 63 222 | 572 | 0.9 | 0.99 |
| Zelazne Wyspy (sturgia) | 0.47 | 51 509 | 4 607 | 8.9 | 9.80 |
| Ibben (ibbenese) | 0.34 | 37 802 | 553 | 1.5 | 1.63 |
| Wolni Ludzie (freefolk) | 0.29 | 31 607 | 475 | 1.5 | 1.64 |
| Wyspy Letnie (summer) | 0.11 | 11 873 | 228 | 1.9 | 2.07 |
| Yi Ti (yiti) | 0.10 | 10 985 | 181 | 1.6 | 1.81 |
| Sarnor (sarnor) | 0.09 | 10 271 | 3 298 | 32.1 | 36.64 |
| Skagos (skagosi) | 0.05 | 5 548 | 126 | 2.3 | 2.52 |
| Valyria (valyrian) | 0.05 | 5 331 | 372 | 7.0 | 7.44 |
| Nocna Straz (nightswatch) | 0.02 | 2 174 | 2 300 | 105.8 | 115.00 |
| **SWIAT** | 51.49 | 5 664 028 | 149 364 | 2.6 | 2.90 |

- Swiat: nalezne 5.66 mln zl dziennie, zaplacone 149 tys. (2.6%). Reach placi 1.4% naleznej, Polnoc 6.5%, Dorne 7.8%, Sarnor 32%, Nocna Straz 106%.
- Dla skali: cale zloto swiata to 208 mln (raport roczny 0c). Pelna renta od glowy przenosilaby 2.7% zlota swiata co dobe - przy kiesach mieszczan 44 mln i wsi 10 mln to niewykonalne [S]. Stawka 40 zl od glowy nie pasuje do dzisiejszej skali pieniadza: caly dochod ludu miast i wsi to ok. 0.88 mln zl dziennie (586.6 tys. + 294.8 tys.) = 6.2 zl na glowe rocznie [S].

### 2.6 Wojsko i rekruci [P]

Zrodlo: BUD (ludzi_partie, ludzi_zalogi, zold - szeregowi rodow krolestwa), LR (milicja, zwerbowani_dzis - rekrut liczony w regionie notabla albo karczmy). Mezczyzni 16-60 = 27% ludzi (PeopleLedger.cs:40).

| krolestwo | druzyny | zalogi | milicja | zold zl/d | rekruci w roku | rekruci na 1000 ludzi/rok | wojsko na 1000 ludzi | wojsko % mezczyzn | wojsko na twierdze | milicja na osade |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| The Reach | 5 662 | 5 973 | 6 406 | 35 775 | 23 065 | 2.76 | 1.39 | 0.52 | 582 | 92.8 |
| Volantis | 2 924 | 2 949 | 2 155 | 27 269 | 7 753 | 1.55 | 1.18 | 0.44 | 839 | 86.2 |
| King's Landing | 5 966 | 7 027 | 6 569 | 50 134 | 21 795 | 4.55 | 2.71 | 1.01 | 619 | 93.8 |
| Qarth | 2 858 | 2 060 | 1 496 | 19 082 | 10 813 | 2.71 | 1.23 | 0.46 | 1 229 | 99.8 |
| The North | 8 272 | 9 283 | 7 573 | 81 267 | 18 179 | 5.63 | 5.44 | 2.02 | 763 | 91.2 |
| The Vale | 5 591 | 5 741 | 4 599 | 57 606 | 13 442 | 4.17 | 3.52 | 1.30 | 809 | 95.8 |
| Stormlands | 5 242 | 5 657 | 5 178 | 33 031 | 23 553 | 8.54 | 3.95 | 1.46 | 641 | 92.5 |
| Riverlands | 5 567 | 4 607 | 4 329 | 38 691 | 25 559 | 9.33 | 3.71 | 1.38 | 848 | 98.4 |
| Braavos | 1 741 | 2 065 | 2 015 | 14 184 | 8 357 | 3.22 | 1.47 | 0.54 | 634 | 96.0 |
| Targaryen (Meereen) | 2 311 | 2 155 | 1 779 | 30 100 | 10 348 | 5.24 | 2.26 | 0.84 | 744 | 77.3 |
| Pentos | 1 088 | 1 980 | 1 754 | 11 447 | 6 388 | 4.29 | 2.06 | 0.76 | 614 | 97.4 |
| Qohor | 1 239 | 1 834 | 1 616 | 13 671 | 6 110 | 4.11 | 2.07 | 0.77 | 615 | 89.8 |
| Norvos | 1 377 | 2 018 | 1 612 | 11 829 | 7 011 | 4.74 | 2.30 | 0.85 | 679 | 94.8 |
| Dorne | 5 832 | 4 609 | 3 634 | 40 476 | 18 217 | 16.33 | 9.36 | 3.47 | 870 | 88.6 |
| Tyrosh | 2 034 | 1 998 | 1 686 | 13 353 | 8 341 | 7.82 | 3.78 | 1.40 | 672 | 84.3 |
| Targaryen (Aegon) | 1 696 | 1 733 | 1 430 | 15 944 | 5 295 | 5.14 | 3.33 | 1.23 | 857 | 95.4 |
| Myr | 1 922 | 1 801 | 1 567 | 16 835 | 6 472 | 6.60 | 3.80 | 1.41 | 745 | 92.2 |
| Lys | 1 225 | 1 088 | 1 234 | 7 481 | 8 142 | 9.17 | 2.61 | 0.96 | 578 | 88.2 |
| Dothraki Horde | 2 522 | 2 322 | 2 423 | 21 463 | 8 125 | 11.09 | 6.61 | 2.45 | 692 | 101.0 |
| Lorath | 1 584 | 1 632 | 1 272 | 14 597 | 8 148 | 12.09 | 4.77 | 1.77 | 804 | 90.8 |
| Dragonstone | 3 331 | 3 060 | 1 843 | 26 136 | 13 484 | 26.48 | 12.55 | 4.65 | 1 065 | 87.8 |
| Iron Islands | 1 545 | 4 871 | 3 620 | 25 763 | 11 697 | 24.95 | 13.69 | 5.07 | 642 | 95.3 |
| Ibben | 484 | 1 694 | 1 222 | 7 403 | 5 635 | 19.55 | 7.56 | 2.80 | 544 | 94.0 |
| Free Folk | 674 | 2 800 | 2 446 | 9 134 | 6 609 | 22.98 | 12.08 | 4.47 | 496 | 97.8 |
| Summer Isles | 536 | 59 | 363 | 3 118 | 703 | 6.51 | 5.51 | 2.04 | 595 | 90.7 |
| Yi Ti Exiles | 60 | 71 | 315 | 572 | 924 | 9.24 | 1.31 | 0.49 | 131 | 78.7 |
| Sarnor | 1 357 | 668 | 1 119 | 13 025 | 2 814 | 30.11 | 21.68 | 8.03 | 405 | 62.2 |
| Skagos | 310 | 90 | 336 | 2 985 | 868 | 17.19 | 7.92 | 2.93 | 400 | 67.1 |
| Nights Watch | 4 049 | 1 748 | 1 614 | 4 996 | 3 681 | 186.06 | 293.01 | 108.52 | 1 159 | 100.9 |
| **SWIAT** | 79 001 | 83 593 | 73 203 | 647 372 | 291 528 | 5.66 | 3.16 | 1.17 | 716 | 92.0 |

Co widac:
- Swiat: 162.6 tys. w druzynach i zalogach = 3.16 na 1000 ludzi = 1.17% mezczyzn; z milicja 1.70%. Rekrutow w roku 291.5 tys. = 2.1% mezczyzn.
- Wojsko na twierdze: 582 (Reach), 763 (Polnoc), 870 (Dorne), 642 (Zelazne Wyspy) - ten sam rzad. Wojsko na 1000 ludzi: Reach 1.39, Volantis 1.18, Polnoc 5.44, Dorne 9.36, Zelazne Wyspy 13.7, Sarnor 21.7, Nocna Straz 293.
- Milicja na osade: 86-100 w kazdym duzym krolestwie.
- Nocna Straz: 5 797 ludzi pod bronia przy 5.4 tys. mezczyzn w calej krainie (108%). To nie blad ksiegi: Straz bierze ludzi z 10 krain Westeros (LOG:668), a nie z Daru.

### 2.7 Doba 364 obok sredniej 28 dob [P]

W kazdej komorce: **doba 364 / srednia dob 337-364**. Kiesa ludu: ostatnia probka / 4 probki.

| krolestwo | ludzie tys. | hearth | dobrobyt miast | plon ksiegi tys. racji | bilans zywnosci warowni | dochod ludu zl | zawor do pana zl | ziemia zl | wplyw rodow zl | wojsko | rekruci dnia |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| The Reach | 8 345 / 8 346 | 19 906 / 19 910 | 46 410 / 45 968 | 9 033 / 9 012 | 2 263 / 2 259 | 35 159 / 37 737 | 8 523 / 13 815 | 17 847 / 17 639 | 173 753 / 82 680 | 11 926 / 11 636 | 5 / 52 |
| Volantis | 4 993 / 4 994 | 6 796 / 6 797 | 24 420 / 24 263 | 4 495 / 4 446 | 516 / 517 | 25 831 / 26 490 | 8 041 / 6 741 | 7 048 / 6 140 | 80 526 / 58 951 | 6 458 / 5 873 | 64 / 30 |
| King's Landing | 4 781 / 4 786 | 19 228 / 19 247 | 42 118 / 41 837 | 4 686 / 4 619 | 2 780 / 2 723 | 34 375 / 46 642 | 12 030 / 10 676 | 14 194 / 16 508 | 213 292 / 100 128 | 12 998 / 12 993 | 3 / 34 |
| Qarth | 3 983 / 3 983 | 4 028 / 4 028 | 21 541 / 21 415 | 2 751 / 2 992 | 184 / 115 | 14 716 / 16 169 | 5 111 / 5 159 | 4 613 / 3 941 | 28 388 / 39 675 | 3 660 / 4 918 | 25 / 21 |
| The North | 3 227 / 3 226 | 21 818 / 21 814 | 64 653 / 63 900 | 3 549 / 3 548 | 3 102 / 3 073 | 62 273 / 60 760 | 18 580 / 21 206 | 20 682 / 19 367 | 99 749 / 108 906 | 17 026 / 17 555 | 26 / 15 |
| The Vale | 3 222 / 3 222 | 13 664 / 13 662 | 37 731 / 37 502 | 3 618 / 3 618 | 1 948 / 1 993 | 43 667 / 47 220 | 8 655 / 11 189 | 12 007 / 12 698 | 60 838 / 68 251 | 11 118 / 11 332 | 18 / 20 |
| Stormlands | 2 757 / 2 756 | 16 093 / 16 093 | 28 896 / 28 603 | 3 162 / 3 162 | 1 574 / 1 565 | 19 051 / 22 868 | 7 111 / 8 779 | 10 040 / 10 480 | 96 273 / 58 681 | 10 801 / 10 899 | 21 / 24 |
| Riverlands | 2 741 / 2 740 | 13 022 / 13 021 | 41 431 / 40 900 | 3 070 / 3 070 | 1 548 / 1 455 | 31 334 / 35 885 | 9 231 / 8 823 | 10 836 / 10 856 | 216 958 / 70 197 | 7 164 / 10 174 | 25 / 50 |
| Braavos | 2 593 / 2 594 | 5 416 / 5 420 | 14 512 / 14 382 | 2 003 / 2 110 | 563 / 607 | 21 216 / 25 144 | 1 026 / 2 059 | 1 889 / 2 571 | 23 278 / 17 016 | 3 607 / 3 806 | 11 / 14 |
| Targaryen (Meereen) | 1 972 / 1 974 | 5 952 / 5 959 | 24 603 / 24 528 | 1 392 / 1 333 | 279 / 351 | 38 105 / 28 506 | 2 752 / 3 556 | 4 749 / 4 941 | 36 955 / 90 170 | 4 441 / 4 466 | 34 / 10 |
| Pentos | 1 487 / 1 488 | 4 975 / 4 975 | 14 722 / 14 595 | 1 196 / 1 179 | 636 / 548 | 11 014 / 10 244 | 1 986 / 2 433 | 2 193 / 2 809 | 12 064 / 22 380 | 3 237 / 3 069 | 2 / 8 |
| Qohor | 1 487 / 1 487 | 5 121 / 5 122 | 13 021 / 12 900 | 1 194 / 1 183 | 482 / 448 | 11 452 / 12 059 | 2 319 / 2 176 | 2 519 / 2 719 | 18 212 / 20 403 | 3 072 / 3 073 | 2 / 7 |
| Norvos | 1 479 / 1 480 | 4 475 / 4 477 | 13 882 / 13 735 | 1 062 / 1 043 | 662 / 703 | 12 125 / 16 036 | 2 936 / 3 645 | 2 984 / 3 814 | 12 663 / 22 289 | 3 257 / 3 396 | 3 / 6 |
| Dorne | 1 116 / 1 116 | 11 637 / 11 633 | 28 309 / 28 000 | 1 230 / 1 218 | 1 185 / 1 229 | 29 049 / 34 081 | 6 690 / 9 585 | 10 343 / 10 086 | 218 036 / 103 143 | 8 993 / 10 441 | 6 / 26 |
| Tyrosh | 1 066 / 1 066 | 5 612 / 5 615 | 13 212 / 13 106 | 806 / 807 | 410 / 394 | 8 554 / 8 382 | 131 / 985 | 2 800 / 2 417 | 18 061 / 29 072 | 4 347 / 4 032 | 10 / 20 |
| Targaryen (Aegon) | 1 030 / 1 031 | 3 801 / 3 805 | 18 263 / 18 142 | 788 / 757 | 440 / 345 | 16 878 / 12 566 | 3 161 / 2 438 | 3 079 / 3 607 | 16 656 / 36 738 | 3 312 / 3 429 | 29 / 25 |
| Myr | 979 / 980 | 4 203 / 4 208 | 13 316 / 13 192 | 699 / 695 | 451 / 444 | 20 959 / 18 546 | 3 687 / 3 095 | 3 464 / 2 842 | 44 369 / 27 694 | 3 760 / 3 723 | 12 / 14 |
| Lys | 887 / 888 | 4 080 / 4 085 | 13 226 / 13 106 | 592 / 577 | 298 / 292 | 3 972 / 6 183 | 75 / 380 | 1 897 / 1 959 | 20 638 / 19 409 | 2 126 / 2 313 | 38 / 23 |
| Dothraki Horde | 732 / 733 | 6 224 / 6 230 | 13 163 / 13 060 | 879 / 878 | 305 / 366 | 16 492 / 18 967 | 6 748 / 6 891 | 5 818 / 5 381 | 57 944 / 56 168 | 5 214 / 4 844 | 38 / 24 |
| Lorath | 673 / 674 | 3 463 / 3 465 | 13 335 / 13 213 | 516 / 515 | 273 / 301 | 18 160 / 12 620 | 2 239 / 3 634 | 2 855 / 4 042 | 18 143 / 29 863 | 3 241 / 3 216 | 29 / 23 |
| Dragonstone | 506 / 509 | 5 307 / 5 347 | 27 311 / 27 050 | 579 / 557 | 782 / 866 | 42 753 / 23 766 | 2 205 / 1 301 | 3 719 / 3 665 | 71 106 / 40 763 | 6 568 / 6 391 | 83 / 24 |
| Iron Islands | 469 / 469 | 9 346 / 9 343 | 32 286 / 32 076 | 537 / 537 | 741 / 800 | 21 924 / 21 968 | 4 744 / 4 291 | 5 777 / 5 346 | 26 733 / 29 993 | 6 260 / 6 416 | 9 / 15 |
| Ibben | 288 / 288 | 2 762 / 2 763 | 15 097 / 14 990 | 265 / 265 | 155 / 214 | 4 970 / 5 431 | 337 / 406 | 954 / 938 | 6 277 / 12 919 | 2 168 / 2 177 | 0 / 2 |
| Free Folk | 287 / 288 | 6 885 / 6 895 | 18 735 / 18 545 | 345 / 345 | 517 / 544 | 8 440 / 9 285 | 0 / 108 | 2 218 / 2 387 | 7 344 / 11 274 | 3 493 / 3 474 | 0 / 17 |
| Summer Isles | 108 / 108 | 150 / 150 | 2 039 / 1 982 | 117 / 117 | 101 / 98 | 1 413 / 1 368 | 443 / 233 | 146 / 129 | 3 247 / 7 913 | 576 / 595 | 0 / 1 |
| Yi Ti Exiles | 100 / 100 | 150 / 150 | 2 159 / 2 094 | 96 / 96 | 164 / 184 | 809 / 1 324 | 0 / 173 | 209 / 208 | 1 977 / 2 098 | 135 / 131 | 0 / 3 |
| Sarnor | 93 / 93 | 4 356 / 4 367 | 13 534 / 13 414 | 88 / 86 | 677 / 706 | 11 169 / 19 254 | 3 541 / 3 151 | 3 375 / 3 165 | 20 403 / 26 115 | 2 010 / 2 026 | 0 / 1 |
| Skagos | 50 / 50 | 1 256 / 1 256 | 5 320 / 5 236 | 61 / 61 | 284 / 304 | 1 771 / 1 576 | 148 / 74 | 284 / 228 | 2 290 / 5 246 | 326 / 400 | 0 / 2 |
| Nights Watch | 20 / 20 | 5 367 / 5 366 | 7 464 / 7 387 | 15 / 15 | 407 / 401 | 5 473 / 5 524 | 1 877 / 2 053 | 2 744 / 2 473 | 12 378 / 15 288 | 5 795 / 5 797 | 0 / 0 |
| **SWIAT** | 51 471 / 51 489 | 215 094 / 215 204 | 624 711 / 619 119 | 48 824 / 48 839 | 23 726 / 23 843 | 573 104 / 586 600 | 124 327 / 139 054 | 161 283 / 163 355 | 1 618 551 / 1 213 424 | 157 089 / 162 594 | 493 / 510 |

- Stany (ludzie, hearth, dobrobyt, plon, wojsko) roznia sie o ulamki procenta - mozna brac ktorekolwiek okno. Riverlands w dobie 364 ma 7.2 tys. wojska przy sredniej 10.2 tys. (dwie armie rozbite tego dnia - raport roczny 1.7).
- Przeplywy dnia (wplyw rodow, zawor, rekruci dnia) skacza: wplyw swiata 1.62 mln w dobie 364 wobec 1.21 mln sredniej (lupy trzech wielkich bitew). Dlatego wszedzie dalej uzywam s28.

### 2.8 Regiony

Pelna tabela 227 regionow (33 kolumny, s28): `skrypty\d11_regiony.tsv`. Wsie (569, z typem, ludzmi, hearth i szacunkiem produkcji): `skrypty\d11_wsie.tsv`. Rody z lennem (205): `skrypty\d11_rody.tsv`.

Skrajne przyklady [P], dochod ludu miasta na 1000 ludzi regionu (zl dziennie): Arbor (Reach, 745 tys. ludzi) 2.24; New Ghys (Qarth, 1 117 tys.) 2.69; Valysar (Volantis, 971 tys.) 2.95; Qarth (1 279 tys.) 3.26 - wobec Pyke (55 tys.) 106; Sharp Point (73 tys.) 145; Saath (Sarnor, 33 tys.) 392; Castle Black (5 tys.) 867.

Produkcja wsi na 1000 ludzi regionu [S] (zl dziennie): Bandallon (Reach, 452 tys. ludzi, 2 wsie) 0.21; New Ghys 0.21; Sar Mell (Volantis) 0.28 - wobec regionow Nocnej Strazy 87-613.

### 2.9 Rozrzut: ile razy rozni sie produkcja i dochod na czlowieka

Percentyle po regionach, krolestwach i rodach (kazdy region, krolestwo, rod = jeden glos):

| miara | n | p10 | p50 | p90 | p90 / p10 | min | max |
|---|---:|---:|---:|---:|---:|---:|---:|
| **REGIONY (227), miara na 1000 ludzi** |  |  |  |  |  |  |  |
| produkcja wsi na targ, zl/d [S] | 227 | 1.13 | 3.64 | 18.31 | 16.3x | 0.21 | 612.54 |
| to samo bez bawelny [S] | 227 | 0.92 | 3.18 | 14.64 | 15.9x | 0.15 | 612.54 |
| zboze na targ, szt./d [S] | 227 | 0.04 | 0.14 | 0.78 | 21.7x | 0.01 | 23.10 |
| plon ksiegi racji na 1000 ludzi wsi [P] | 227 | 1 114.54 | 1 200.00 | 1 200.02 | 1.1x | 600.05 | 1 429.31 |
| dochod ludu (miasto albo podzamcze), zl/d [P] | 227 | 1.06 | 7.08 | 46.45 | 43.9x | 0.12 | 867.43 |
| dochod ludu na 1000 ludzi MIASTA (89 miast z ludzmi) [P] | 89 | 16.42 | 73.88 | 509.75 | 31.1x | 6.46 | 1 906.52 |
| wydatek na jedzenie na 1000 ludzi MIASTA [P] | 89 | 3.96 | 34.93 | 155.64 | 39.4x | 2.30 | 463.79 |
| zawor kasy miasta do pana, zl/d (97 miast) [P] | 97 | 0.38 | 2.28 | 20.14 | 53.5x | 0.00 | 374.07 |
| zaloga, ludzi [P] | 227 | 0.71 | 2.10 | 9.62 | 13.6x | 0.18 | 112.78 |
| rekruci w roku [P] | 227 | 1.84 | 6.35 | 23.45 | 12.7x | 0.08 | 223.66 |
| **KROLESTWA (29), miara na 1000 ludzi** |  |  |  |  |  |  |  |
| produkcja wsi na targ, zl/d [S] | 29 | 2.14 | 3.97 | 22.57 | 10.6x | 1.22 | 216.84 |
| zboze na targ, szt./d [S] | 29 | 0.07 | 0.21 | 0.59 | 8.4x | 0.01 | 12.41 |
| dochod ludu, zl/d [P] | 29 | 6.57 | 13.25 | 46.71 | 7.1x | 4.06 | 279.19 |
| dochod rodow z ziemi, zl/d [P] | 29 | 1.22 | 3.45 | 9.51 | 7.8x | 0.99 | 124.99 |
| wplyw rodow razem, zl/d [P] | 29 | 11.44 | 28.25 | 94.74 | 8.3x | 6.56 | 772.76 |
| zawor do pana, zl/d [P] | 29 | 0.90 | 2.23 | 9.20 | 10.3x | 0.37 | 103.78 |
| wojsko (druzyny + zalogi) [P] | 29 | 1.38 | 3.78 | 12.78 | 9.3x | 1.18 | 293.01 |
| rekruci w roku [P] | 29 | 3.13 | 7.82 | 25.26 | 8.1x | 1.55 | 186.06 |
| **RODY Z LENNEM (205)** |  |  |  |  |  |  |  |
| dochod z ziemi na 1000 ludzi lenn, zl/d [P] | 205 | 0.61 | 2.78 | 13.54 | 22.0x | 0.19 | 276.32 |
| dochod z ziemi na osade (twierdza + wsie), zl/d [P] | 205 | 38.48 | 120.70 | 394.44 | 10.3x | 13.17 | 1 031.72 |
| **DLA POROWNANIA: miara na OSADE** |  |  |  |  |  |  |  |
| produkcja wsi na targ, zl na wies/d [S] (regiony) | 227 | 75.81 | 249.74 | 706.24 | 9.3x | 40.32 | 2 962.17 |
| dochod ludu na miasto, zl/d [P] | 97 | 1 700.15 | 4 571.50 | 10 603.45 | 6.2x | 1 230.25 | 18 937.50 |
| zawor do pana na miasto, zl/d [P] | 97 | 116.57 | 437.32 | 4 100.85 | 35.2x | 0.00 | 6 925.68 |
| zaloga na miasto [P] | 97 | 410.42 | 508.00 | 589.14 | 1.4x | 58.82 | 663.96 |
| milicja na region [P] | 227 | 232.43 | 306.82 | 433.05 | 1.9x | 96.00 | 735.96 |
| **MIARY WIELKOSCI** |  |  |  |  |  |  |  |
| ludzi na wies, tys. (569 wsi) [P] | 569 | 15.61 | 60.11 | 155.74 | 10.0x | 1.26 | 301.98 |
| hearth na wies (569 wsi) [P] | 569 | 316.40 | 349.00 | 512.00 | 1.6x | 50.00 | 807.00 |
| ludnosc BK na wies [P] | 568 | 726.70 | 1 408.50 | 2 386.70 | 3.3x | 3.00 | 12 378.00 |
| ludzi na miasto, tys. (89 miast z ludzmi) [P] | 89 | 9.59 | 35.07 | 295.77 | 30.8x | 2.45 | 625.99 |
| dobrobyt miasta (97) [P] | 97 | 4 836.62 | 6 310.70 | 7 706.78 | 1.6x | 1 981.64 | 10 053.86 |
| glowy BK w kiesie ludu na miasto (97) [P] | 97 | 11 283.80 | 15 625.00 | 19 814.20 | 1.8x | 9 840.00 | 24 546.00 |

Co widac:
- **Na czlowieka** regiony roznia sie 13-54 razy miedzy 10. a 90. percentylem (produkcja wsi 16 razy, dochod ludu 44 razy, zawor 54 razy, zaloga 14 razy, rekruci 13 razy). Krolestwa: 7-11 razy.
- **Na osade** te same miary sa duzo rowniejsze: zaloga na miasto 1.4 razy, milicja 1.9 razy, dochod ludu na miasto 6.2 razy, produkcja na wies 9.3 razy. Wyjatek: zawor na miasto 35 razy (zalezy od stanu kasy kupcow, nie od wielkosci).
- Jedyna miara rowna na czlowieka to plon ksiegi racji (1.1 razy).
- Miary wielkosci: ludzi na wies 10 razy, ludzi na miasto 31 razy - a hearth 1.6 razy, dobrobyt 1.6 razy, glowy BK 1.8 razy. Gra widzi osady prawie rowne, ksiega - bardzo rozne.

## 3. Zaleznosc: ludzie czy osady

Metoda (skrypty d12, d12b): korelacja Pearsona (r) i Spearmana (rho, na rangach - odporna na skrajne wartosci), regresja prosta (R2 = jaka czesc roznic wyjasnia jedna zmienna), regresja z dwiema zmiennymi naraz (ludzie + osady; "beta" = waga kazdej po odjeciu drugiej, w tych samych jednostkach). "Osady" = miasta + zamki + wsie.
Ostrzezenie: ludzie i osady sa ze soba powiazane (krolestwa: wiecej osad to zwykle wiecej ludzi; regiony: r = 0.40), wiec sama korelacja z ludzmi nie dowodzi, ze cos od ludzi zalezy - rozstrzyga regresja z dwiema zmiennymi.

### 3.1 Krolestwa (29)

| miara | r ludzie | r osady | r hearth | r dobrobyt miast | r ludnosc BK | rho ludzie | rho osady | R2 ludzie | R2 osady | R2 oba | beta ludzie | beta osady |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| dochod rodow z ziemi | 0.74 | 0.97 | 0.97 | 0.93 | 0.98 | 0.73 | 0.84 | 0.55 | 0.94 | 0.96 | 0.15 | 0.87 |
| od korony | 0.54 | 0.87 | 0.86 | 0.86 | 0.87 | 0.66 | 0.84 | 0.30 | 0.76 | 0.77 | -0.09 | 0.93 |
| wplyw rodow razem | 0.62 | 0.82 | 0.82 | 0.84 | 0.84 | 0.72 | 0.79 | 0.39 | 0.68 | 0.69 | 0.12 | 0.74 |
| dochod ludu | 0.65 | 0.89 | 0.87 | 0.92 | 0.91 | 0.73 | 0.87 | 0.42 | 0.79 | 0.79 | 0.09 | 0.83 |
| wydatki ludu | 0.65 | 0.89 | 0.89 | 0.90 | 0.91 | 0.73 | 0.81 | 0.43 | 0.79 | 0.80 | 0.09 | 0.83 |
| zawor do pana | 0.68 | 0.91 | 0.91 | 0.89 | 0.92 | 0.74 | 0.76 | 0.46 | 0.84 | 0.84 | 0.11 | 0.84 |
| produkcja wsi na targ zl [S] | 0.82 | 0.66 | 0.67 | 0.70 | 0.69 | 0.68 | 0.69 | 0.67 | 0.44 | 0.69 | 0.69 | 0.19 |
| to samo bez bawelny [S] | 0.63 | 0.95 | 0.95 | 0.92 | 0.93 | 0.53 | 0.90 | 0.39 | 0.89 | 0.89 | -0.02 | 0.96 |
| zboze na targ [S] | 0.67 | 0.94 | 0.94 | 0.92 | 0.95 | 0.55 | 0.82 | 0.45 | 0.89 | 0.89 | 0.06 | 0.91 |
| plon ksiegi racji | 0.98 | 0.75 | 0.77 | 0.72 | 0.77 | 0.98 | 0.74 | 0.97 | 0.56 | 0.98 | 0.88 | 0.15 |
| bilans zywnosci warowni | 0.63 | 0.96 | 0.95 | 0.90 | 0.96 | 0.51 | 0.89 | 0.39 | 0.92 | 0.92 | -0.04 | 0.99 |
| wojsko | 0.63 | 0.96 | 0.96 | 0.93 | 0.96 | 0.66 | 0.88 | 0.40 | 0.92 | 0.92 | -0.03 | 0.98 |
| zold | 0.57 | 0.90 | 0.88 | 0.93 | 0.92 | 0.75 | 0.86 | 0.32 | 0.80 | 0.81 | -0.07 | 0.94 |
| milicja | 0.68 | 1.00 | 0.99 | 0.93 | 0.99 | 0.67 | 0.97 | 0.46 | 0.99 | 0.99 | 0.01 | 0.99 |
| rekruci w roku | 0.66 | 0.86 | 0.88 | 0.84 | 0.86 | 0.70 | 0.80 | 0.43 | 0.74 | 0.75 | 0.14 | 0.77 |

To samo dla 22 krolestw powyzej 400 tys. ludzi (bez Nocnej Strazy, Sarnoru, Skagos, Wysp Letnich, Yi Ti, Ibbenu, Wolnych Ludzi):

| miara | r ludzie | r osady | r hearth | r dobrobyt miast | r ludnosc BK | rho ludzie | rho osady | R2 ludzie | R2 osady | R2 oba | beta ludzie | beta osady |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| dochod rodow z ziemi | 0.67 | 0.97 | 0.97 | 0.92 | 0.98 | 0.54 | 0.83 | 0.45 | 0.94 | 0.95 | 0.13 | 0.89 |
| od korony | 0.42 | 0.85 | 0.84 | 0.83 | 0.84 | 0.44 | 0.83 | 0.17 | 0.72 | 0.74 | -0.15 | 0.94 |
| wplyw rodow razem | 0.49 | 0.78 | 0.78 | 0.79 | 0.80 | 0.48 | 0.77 | 0.24 | 0.61 | 0.62 | 0.02 | 0.77 |
| dochod ludu | 0.54 | 0.87 | 0.85 | 0.91 | 0.90 | 0.57 | 0.85 | 0.29 | 0.75 | 0.75 | 0.02 | 0.86 |
| wydatki ludu | 0.54 | 0.87 | 0.87 | 0.87 | 0.90 | 0.57 | 0.77 | 0.29 | 0.76 | 0.76 | 0.03 | 0.86 |
| zawor do pana | 0.59 | 0.91 | 0.90 | 0.89 | 0.92 | 0.59 | 0.77 | 0.35 | 0.83 | 0.83 | 0.07 | 0.87 |
| produkcja wsi na targ zl [S] | 0.78 | 0.56 | 0.57 | 0.61 | 0.60 | 0.55 | 0.51 | 0.60 | 0.32 | 0.62 | 0.69 | 0.15 |
| to samo bez bawelny [S] | 0.55 | 0.94 | 0.93 | 0.91 | 0.93 | 0.42 | 0.90 | 0.30 | 0.88 | 0.88 | -0.02 | 0.95 |
| zboze na targ [S] | 0.60 | 0.94 | 0.94 | 0.93 | 0.95 | 0.39 | 0.85 | 0.35 | 0.89 | 0.89 | 0.04 | 0.92 |
| plon ksiegi racji | 0.98 | 0.70 | 0.73 | 0.65 | 0.72 | 0.96 | 0.70 | 0.96 | 0.49 | 0.98 | 0.88 | 0.17 |
| bilans zywnosci warowni | 0.57 | 0.97 | 0.96 | 0.90 | 0.97 | 0.45 | 0.87 | 0.32 | 0.93 | 0.93 | -0.02 | 0.98 |
| wojsko | 0.53 | 0.96 | 0.96 | 0.94 | 0.96 | 0.52 | 0.91 | 0.29 | 0.93 | 0.93 | -0.07 | 1.01 |
| zold | 0.42 | 0.88 | 0.86 | 0.92 | 0.90 | 0.49 | 0.86 | 0.18 | 0.77 | 0.78 | -0.16 | 0.97 |
| milicja | 0.60 | 1.00 | 0.99 | 0.92 | 0.99 | 0.53 | 0.99 | 0.36 | 0.99 | 0.99 | 0.00 | 1.00 |
| rekruci w roku | 0.53 | 0.83 | 0.86 | 0.78 | 0.82 | 0.39 | 0.75 | 0.28 | 0.69 | 0.69 | 0.05 | 0.80 |

- Dochod rodow z ziemi: r z osadami 0.97, z ludzmi 0.74 (0.67 w duzych krolestwach). Po wlozeniu obu zmiennych: waga osad 0.87, waga ludzi 0.15. Osady same wyjasniaja 94% roznic, ludzie doloza 2 punkty.
- Dochod ludu, zawor, wojsko, zold, milicja, bilans zywnosci warowni, zboze na targ: waga osad 0.83-0.99, waga ludzi -0.07 do +0.11 (czyli zero).
- Plon ksiegi racji: odwrotnie - waga ludzi 0.88, osad 0.15.
- Produkcja wsi w zl [S] wyglada na "od ludzi" (r 0.82), ale to zasluga bawelny: 14 wsi jedwabiu nasza tabela klimatu postawila w ludnych krainach (Volantis, Qarth, Reach, Myr), a bawelna to 28% wartosci. Bez bawelny: waga osad 0.96, ludzi -0.02.
- Hearth, dobrobyt miast i ludnosc BK koreluja z dochodem tak samo jak liczba osad (0.92-0.98), bo sa prawie stale na osade - to ta sama informacja.

### 3.2 Rody z lennem (205)

| miara | r ludzie lenn | r osady | r hearth | r dobrobyt miast | r produkcja wsi [S] | rho ludzie lenn | rho osady | R2 ludzie lenn | R2 osady | R2 oba | beta ludzie lenn | beta osady |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| dochod z ziemi | 0.40 | 0.68 | 0.64 | 0.58 | 0.44 | 0.39 | 0.63 | 0.16 | 0.46 | 0.47 | 0.09 | 0.63 |
| od korony | 0.34 | 0.57 | 0.52 | 0.45 | 0.38 | 0.30 | 0.32 | 0.12 | 0.32 | 0.33 | 0.08 | 0.53 |
| wplyw doby razem | 0.42 | 0.54 | 0.50 | 0.53 | 0.40 | 0.45 | 0.47 | 0.18 | 0.29 | 0.32 | 0.20 | 0.44 |
| zold | 0.41 | 0.57 | 0.54 | 0.61 | 0.39 | 0.44 | 0.57 | 0.17 | 0.32 | 0.34 | 0.18 | 0.48 |
| wojsko (druzyny + zalogi) | 0.47 | 0.75 | 0.75 | 0.67 | 0.43 | 0.44 | 0.62 | 0.22 | 0.57 | 0.58 | 0.13 | 0.69 |
| zalogi | 0.49 | 0.85 | 0.85 | 0.78 | 0.43 | 0.40 | 0.77 | 0.24 | 0.73 | 0.74 | 0.10 | 0.81 |
| druzyny | 0.34 | 0.50 | 0.51 | 0.43 | 0.34 | 0.32 | 0.29 | 0.12 | 0.25 | 0.27 | 0.13 | 0.44 |

- Dochod z ziemi rodu: r z osadami 0.68, z ludzmi lenn 0.40; waga osad 0.63, ludzi 0.09. Zalogi: r z osadami 0.85.
- Przyklady [P]: Emeros (Qarth) ma 1.28 mln ludzi w 4 osadach i 1 399 zl dziennie z ziemi; Tyrell 1.27 mln ludzi w 18 osadach i 6 308 zl; Mallarawan (Qarth) 1.12 mln ludzi i 734 zl. Mormont z Nocnej Strazy: 5.5 tys. ludzi w 4 osadach i 1 517 zl.

### 3.3 Miasta (97)

Para w regresji: ludzie samego miasta i dobrobyt.

| miara | r ludzie miasta | r ludzie regionu | r osady | r dobrobyt | r glowy BK | rho ludzie miasta | rho dobrobyt | R2 ludzie miasta | R2 dobrobyt | R2 oba | beta ludzie miasta | beta dobrobyt |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| dochod ludu miasta | 0.18 | 0.17 | 0.34 | 0.48 | 0.30 | 0.28 | 0.46 | 0.03 | 0.23 | 0.23 | 0.06 | 0.46 |
| wydatki ludu | 0.24 | 0.20 | 0.32 | 0.54 | 0.35 | 0.25 | 0.55 | 0.06 | 0.29 | 0.30 | 0.10 | 0.51 |
| wydatek na jedzenie | 0.11 | 0.11 | 0.32 | 0.79 | 0.48 | 0.29 | 0.76 | 0.01 | 0.62 | 0.64 | -0.11 | 0.82 |
| koszyk ludu (potrzeba) | -0.07 | 0.00 | -0.03 | 0.48 | 0.71 | -0.01 | 0.41 | 0.01 | 0.23 | 0.27 | -0.22 | 0.54 |
| zawor do pana | -0.04 | 0.08 | 0.19 | 0.26 | 0.14 | 0.15 | 0.15 | 0.00 | 0.07 | 0.08 | -0.13 | 0.29 |
| kasa kupcow | 0.03 | 0.15 | 0.21 | 0.49 | 0.23 | 0.26 | 0.47 | 0.00 | 0.24 | 0.25 | -0.11 | 0.52 |
| zaloga | 0.12 | 0.10 | 0.02 | 0.41 | 0.11 | -0.07 | 0.23 | 0.01 | 0.17 | 0.17 | 0.01 | 0.41 |
| rekruci w roku | 0.15 | 0.19 | 0.25 | 0.39 | 0.15 | 0.25 | 0.38 | 0.02 | 0.15 | 0.15 | 0.04 | 0.37 |

- Dochod ludu miasta: r z ludzmi miasta 0.18, z dobrobytem 0.48. Wydatek na jedzenie: 0.11 wobec 0.79. Koszyk (potrzeba): -0.07 z ludzmi miasta, 0.71 z glowami BK.
- Zawor do pana nie zalezy od niczego z tych miar (R2 0.08) - zalezy od stanu kasy kupcow ponad zapasem.

### 3.4 Regiony (227)

| miara | r ludzie | r osady | r hearth | r dobrobyt | r ludnosc BK | rho ludzie | rho osady | R2 ludzie | R2 osady | R2 oba | beta ludzie | beta osady |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| produkcja wsi na targ zl [S] | 0.42 | 0.30 | 0.26 | 0.30 | 0.31 | 0.27 | 0.46 | 0.18 | 0.09 | 0.20 | 0.36 | 0.16 |
| to samo bez bawelny [S] | 0.10 | 0.39 | 0.38 | 0.31 | 0.28 | 0.18 | 0.43 | 0.01 | 0.16 | 0.16 | -0.07 | 0.42 |
| zboze na targ [S] | 0.21 | 0.56 | 0.48 | 0.57 | 0.52 | 0.33 | 0.71 | 0.04 | 0.32 | 0.32 | -0.02 | 0.57 |
| plon ksiegi racji [P] | 0.93 | 0.28 | 0.37 | 0.32 | 0.32 | 0.94 | 0.27 | 0.86 | 0.08 | 0.87 | 0.97 | -0.11 |
| bilans zywnosci warowni | 0.23 | 0.67 | 0.62 | 0.72 | 0.73 | 0.33 | 0.69 | 0.05 | 0.45 | 0.46 | -0.05 | 0.70 |
| zuzycie BK (wartosc) | 0.43 | 0.80 | 0.61 | 0.86 | 0.82 | 0.39 | 0.84 | 0.19 | 0.65 | 0.66 | 0.13 | 0.75 |
| dochod ludu | 0.40 | 0.75 | 0.53 | 0.78 | 0.74 | 0.35 | 0.83 | 0.16 | 0.56 | 0.57 | 0.12 | 0.70 |
| wydatek na jedzenie | 0.41 | 0.85 | 0.68 | 0.94 | 0.90 | 0.40 | 0.83 | 0.17 | 0.72 | 0.73 | 0.09 | 0.81 |
| zaloga | 0.37 | 0.68 | 0.62 | 0.80 | 0.72 | 0.33 | 0.72 | 0.14 | 0.46 | 0.47 | 0.12 | 0.63 |
| milicja | 0.40 | 0.75 | 0.70 | 0.80 | 0.77 | 0.36 | 0.76 | 0.16 | 0.56 | 0.57 | 0.12 | 0.70 |
| rekruci w roku | 0.42 | 0.70 | 0.56 | 0.74 | 0.70 | 0.45 | 0.73 | 0.18 | 0.50 | 0.52 | 0.17 | 0.64 |

- Regresja liniowa po regionach: waga osad 0.57-0.81 dla zboza na targ, bilansu zywnosci, zuzycia BK, dochodu ludu, zalogi, milicji i rekrutow; waga ludzi -0.05 do +0.17.
- Tu korelacja z "osadami" i z dobrobytem jest wysoka glownie dlatego, ze region miejski ma i wiecej wsi, i wyzszy dobrobyt niz region zamkowy. W samych zamkach (130; kazdy ma 2 wsie) ani ludzie, ani hearth nie wyjasniaja niczego: r od -0.10 do 0.28 (skrypt d12, blok C).

### 3.5 Ile daje jedna osada, a ile 1000 ludzi [S na danych P]

Regresja: miara = stala + b1 x (ludzie w tys.) + b2 x (liczba osad). W nawiasie t (powyzej 2 = wyraznie rozne od zera). Skrypt d12b.

| miara (krolestwa, 29) | za 1000 ludzi | za 1 osade | R2 |
|---|---:|---:|---:|
| dochod rodow z ziemi, zl/d | 0.43 (t 2.7) | 224 (t 15.5) | 0.96 |
| od korony, zl/d | -0.68 (t -0.7) | 673 (t 7.3) | 0.77 |
| wplyw rodow razem, zl/d | 1.99 (t 0.8) | 1 142 (t 5.0) | 0.69 |
| dochod ludu, zl/d | 0.69 (t 0.7) | 604 (t 6.8) | 0.79 |
| zawor do pana, zl/d | 0.29 (t 1.1) | 203 (t 8.0) | 0.84 |
| zboze na targ, szt./d [S] | 0.008 (t 0.6) | 11.9 (t 10.4) | 0.89 |
| produkcja wsi bez bawelny, zl/d [S] | -0.04 (t -0.3) | 182 (t 11.1) | 0.89 |
| wojsko (druzyny + zalogi) | -0.07 (t -0.4) | 203 (t 13.0) | 0.92 |
| zold, zl/d | -0.66 (t -0.6) | 841 (t 8.0) | 0.81 |
| milicja | 0.007 (t 0.3) | 93.2 (t 44.2) | 0.99 |
| rekruci w roku | 0.51 (t 1.0) | 262 (t 5.8) | 0.75 |

| miara (rody z lennem, 205) | za 1000 ludzi | za 1 osade | R2 |
|---|---:|---:|---:|
| dochod z ziemi, zl/d | 0.36 (t 1.6) | 337 (t 10.7) | 0.47 |
| wojsko | 0.23 (t 2.5) | 168 (t 13.2) | 0.58 |
| zalogi | 0.09 (t 2.3) | 99 (t 19.4) | 0.74 |

| miara (miasta, 97) | za 1000 ludzi miasta | za 1000 punktow dobrobytu | R2 |
|---|---:|---:|---:|
| dochod ludu miasta, zl/d | 1.65 (t 0.6) | 1 389 (t 4.9) | 0.23 |
| wydatek na jedzenie, zl/d | -0.49 (t -1.8) | 362 (t 12.7) | 0.64 |
| zaloga | 0.005 (t 0.1) | 35.9 (t 4.2) | 0.17 |

Czytanie: w dochodzie z ziemi jedna osada wazy tyle, co 527 tys. ludzi (224 / 0.43). W krolestwach w wojsku, zoldzie, milicji, zbozu i dochodzie ludu liczba ludzi nie wazy nic (wspolczynnik nie rozni sie od zera). U rodow wazy malo: 1000 ludzi w lennach to 0.23 zolnierza, jedna osada to 168.

### 3.6 Zmiennosc: na 1000 ludzi czy na osade

Wspolczynnik zmiennosci (odchylenie / srednia) miedzy 22 krolestwami powyzej 400 tys. ludzi. Mniejsza liczba = miara lepiej opisuje to, co sie dzieje.

| miara | zmiennosc na 1000 ludzi | zmiennosc na osade |
|---|---:|---:|
| dochod rodow z ziemi | 0.68 | 0.24 |
| od korony | 0.94 | 0.45 |
| wplyw rodow razem | 0.74 | 0.42 |
| dochod ludu | 0.76 | 0.31 |
| wydatki ludu | 0.74 | 0.35 |
| zawor do pana | 0.80 | 0.45 |
| produkcja wsi na targ zl [S] | 0.79 | 0.71 |
| to samo bez bawelny [S] | 0.91 | 0.24 |
| zboze na targ [S] | 0.84 | 0.32 |
| plon ksiegi racji | 0.20 | 0.68 |
| bilans zywnosci warowni | 0.79 | 0.33 |
| wojsko | 0.79 | 0.19 |
| zold | 0.79 | 0.28 |
| milicja | 0.83 | 0.06 |
| rekruci w roku | 0.79 | 0.30 |

### 3.7 Werdykt

**Wniosek "zarobek idzie za liczba osad, nie ludzi" jest POTWIERDZONY** [P + S]:
- kazda miara zlota, towaru na targ i wojska ma wage osad 0.74-1.00 i wage ludzi bliska zera (krolestwa), a na osade jest 1.8-14 razy rowniejsza niz na czlowieka (tabela 3.6);
- hearth i dobrobyt nie wnosza nic ponad liczbe osad, bo sa prawie stale na osade (p90/p10 = 1.6);
- wyjatki: plon ksiegi racji (od ludzi z zalozenia), rece warsztatow (od ludzi z zalozenia; tu nie mierzone osobno) i wartosc produkcji z bawelna (przypadek rozmieszczenia 14 wsi).

## 4. Wiarygodnosc liczb ludzi

### 4.1 Skad sa liczby

- Ludnosc krain to tabela w kodzie (PopulationLaw.cs:42-74; opis :17-19: "estymacje", dla Essos estymacje fanow). Przyklady: Reach 8.0 mln, Volantis 5.0 mln, Westerlands 4.0 mln, Qarth 4.0 mln, Dorzecze 3.5 mln, Polnoc 3.0 mln, Dolina 3.0 mln, Dorne 1.5 mln, Zelazne Wyspy 0.5 mln, Wolni Ludzie 0.3 mln, Sarnor 0.1 mln, Nocna Straz 0.02 mln. Do tego udzial miejski krainy: Westeros 5-10% (Krolewska Przystan 75%), Essos 20-40%.
- Przy starcie kampanii kazda kraina dostaje "k": ilu ludzi na punkt hearth wsi i na punkt dobrobytu miasta, tak by suma dala tabele (LOG:209). Wies ma wiec ludzi = hearth x k krainy; miasto = dobrobyt x k krainy (potem stan zapisany, LOG:211).
- Skutek 1: MIEDZY krainami roznice sa wielkie (k wsi od 3 do 650 ludzi na punkt hearth). Skutek 2: W SRODKU krainy wsie i miasta sa prawie rowne, bo hearth i dobrobyt sa prawie rowne. Ksiega nie zna "stolicy" i "miasteczka".
- Wyspy Letnie: obciete z 0.75 mln do 0.11 mln (sufit 650 ludzi na punkt hearth), 0.64 mln "poza mapa" (LOG:209).

### 4.2 Ludzie na wies i na miasto wedlug krain [P]

Kraina = kultura regionu z 1. doby. s28.

| kraina (kultura) | ludzie tys. | wsi | ludzi na wies: min tys. | mediana tys. | max tys. | hearth na wies | ludzi na punkt hearth | miast | ludzi na miasto: min tys. | mediana tys. | max tys. | ludzi na punkt dobrobytu | udzial miejski % |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Reach (reach) | 7 998 | 40 | 134.1 | 156.0 | 302.0 | 406 | 444 | 6 | 117.9 | 128.6 | 157.8 | 19.1 | 9.9 |
| Volantis (volantine) | 4 994 | 18 | 191.9 | 193.2 | 275.2 | 378 | 551 | 4 | 290.5 | 313.6 | 329.8 | 51.4 | 25.0 |
| Qarth (qartheen) | 3 983 | 11 | 216.5 | 217.4 | 277.4 | 366 | 623 | 3 | 382.8 | 465.3 | 626.0 | 68.8 | 37.0 |
| Westerlands (vlandia) | 3 942 | 33 | 77.7 | 95.1 | 185.7 | 401 | 274 | 3 | 97.5 | 97.6 | 121.0 | 16.0 | 8.0 |
| Dorzecze (river) | 3 471 | 43 | 17.7 | 68.6 | 139.9 | 379 | 197 | 9 | 19.5 | 30.5 | 35.5 | 4.9 | 7.8 |
| Dolina (vale) | 2 998 | 31 | 66.8 | 77.8 | 156.8 | 409 | 223 | 5 | 32.4 | 33.7 | 43.2 | 5.5 | 5.9 |
| Polnoc (battania) | 2 989 | 57 | 40.9 | 46.4 | 79.6 | 364 | 132 | 9 | 17.1 | 25.4 | 36.0 | 4.0 | 7.9 |
| Zatoka Niewolnicza (ghiscari) | 2 752 | 23 | 63.3 | 67.3 | 76.9 | 349 | 195 | 6 | 182.4 | 190.0 | 246.5 | 32.3 | 43.1 |
| Braavos (empire) | 2 486 | 13 | 112.3 | 129.4 | 139.9 | 350 | 371 | 2 | 364.5 | 397.9 | 431.3 | 55.3 | 32.0 |
| Krainy Burzy (stormlands) | 2 476 | 31 | 57.9 | 68.2 | 118.6 | 417 | 182 | 4 | 26.6 | 30.0 | 35.2 | 4.3 | 4.9 |
| Pentos (pentoshi) | 1 488 | 13 | 60.9 | 69.4 | 128.3 | 383 | 200 | 2 | 239.3 | 245.3 | 251.4 | 33.6 | 33.0 |
| Qohor (qohorik) | 1 487 | 13 | 67.5 | 67.7 | 111.5 | 394 | 194 | 2 | 230.7 | 245.9 | 261.1 | 38.1 | 33.1 |
| Norvos (norvos) | 1 480 | 12 | 64.9 | 68.8 | 114.5 | 373 | 198 | 2 | 294.8 | 297.1 | 299.4 | 43.3 | 40.2 |
| Dorne (aserai) | 1 463 | 38 | 29.6 | 31.9 | 52.9 | 403 | 88 | 5 | 20.8 | 22.4 | 25.7 | 3.5 | 7.8 |
| Myr (myrish) | 980 | 12 | 45.2 | 48.2 | 61.2 | 351 | 139 | 2 | 188.7 | 198.3 | 207.9 | 30.1 | 40.5 |
| Tyrosh (tyroshi) | 979 | 12 | 39.5 | 42.5 | 68.6 | 408 | 120 | 2 | 190.6 | 197.1 | 203.6 | 30.1 | 40.3 |
| Lys (lyseni) | 975 | 12 | 41.6 | 46.6 | 59.8 | 400 | 121 | 2 | 188.9 | 197.0 | 205.2 | 30.1 | 40.4 |
| Ziemie Korony (dragonstone) | 789 | 23 | 15.2 | 30.3 | 52.8 | 370 | 88 | 4 | 7.9 | 9.0 | 10.3 | 1.3 | 4.6 |
| Lorath (nord) | 782 | 12 | 41.9 | 42.3 | 65.5 | 361 | 124 | 2 | 117.4 | 121.7 | 125.9 | 18.4 | 31.1 |
| Dothrakowie (khuzait) | 733 | 17 | 39.1 | 40.5 | 53.9 | 367 | 118 | 2 | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 |
| Krolewska Przystan (crownlands) | 575 | 11 | 6.3 | 9.7 | 22.1 | 436 | 27 | 2 | 185.0 | 221.8 | 258.6 | 27.5 | 77.1 |
| Zelazne Wyspy (sturgia) | 469 | 28 | 14.7 | 16.2 | 16.8 | 334 | 48 | 6 | 2.5 | 4.0 | 4.6 | 0.7 | 4.6 |
| Ibben (ibbenese) | 288 | 9 | 7.6 | 28.1 | 31.5 | 307 | 80 | 3 | 18.6 | 24.2 | 24.3 | 4.5 | 23.3 |
| Wolni Ludzie (freefolk) | 288 | 18 | 12.6 | 15.3 | 21.8 | 383 | 42 | 3 | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 |
| Valyria (valyrian) | 253 | 5 | 24.3 | 68.0 | 68.0 | 345 | 146 | 1 | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 |
| Wyspy Letnie (summer) | 108 | 3 | 32.4 | 32.5 | 32.5 | 50 | 650 | 1 | 10.6 | 10.6 | 10.6 | 5.3 | 9.8 |
| Yi Ti (yiti) | 100 | 3 | 26.8 | 26.8 | 26.8 | 50 | 535 | 1 | 19.7 | 19.7 | 19.7 | 9.4 | 19.7 |
| Sarnor (sarnor) | 93 | 13 | 4.4 | 5.8 | 7.1 | 336 | 17 | 2 | 9.3 | 9.5 | 9.8 | 1.4 | 20.4 |
| Skagos (skagosi) | 50 | 4 | 12.6 | 12.6 | 12.6 | 314 | 40 | 1 | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 |
| Nocna Straz (nightswatch) | 20 | 11 | 1.3 | 1.8 | 2.1 | 488 | 4 | 1 | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 |

Swiat [P]: wsie (569) - ludzi na wies min 1 264, p10 15 608, mediana 60 111, srednia 72 281, p90 155 742, max 301 979. Miasta z ludzmi (89) - min 2 454, p10 9 590, mediana 35 065, srednia 116 414, p90 295 768, max 625 991.
Dla wyobrazenia [S]: przy 300 ludziach na prawdziwa sredniowieczna wies jeden symbol wsi to srednio 241 wsi (od 4 w Darze do 1 007 w Reach).

### 4.3 Wartosci skrajne [P]

- Najwieksze miasta: Qarth 626 tys., New Ghys 465 tys., Braavos 431 tys., Qarkash 383 tys., Rhyos 364 tys., Volantis 330 tys., Selhorys 317 tys., Valysar 310 tys., Norvos 299 tys., Menetragos 295 tys., Volon Therys 291 tys., Qohor 261 tys. **Dwanascie najwiekszych miast swiata lezy w Essos.**
- Westeros: Krolewska Przystan 259 tys., Duskendale 185 tys., Highgarden 158 tys., Oldtown 144 tys., Lannisport 121 tys., Winterfell 36 tys., Riverrun 36 tys., Gulltown 34 tys., Storm's End 33 tys., White Harbor 33 tys., Sunspear 26 tys., Pyke 4.6 tys.
- Najmniejsze miasta z ludzmi: Pebbleton i Lonely Light po 2.5 tys., Ten Towers 3.9 tys., Lordsport 4.0 tys., Blacktyde 4.1 tys., Pyke 4.6 tys. (wszystkie Zelazne Wyspy; kazde trzyma zaloge 560-630 ludzi), Sharp Point 7.9 tys., Hull 8.4 tys., Kyth 9.3 tys., Saath 9.8 tys.
- **8 miast ma w ksiedze zero mieszkancow** (krainy z udzialem miejskim 0): Castle Black, Vaes Dothrak, Vaes Diaf, Thenn, Hardhome, Frostfang's Camp, Driftwood Hall, Elyria. Ludzie tych regionow siedza tylko we wsiach.
- Najludniejsze wsie: Ramsport 302 tys., Sunflower 296 tys., Shieldview 296 tys. (Reach), Fozzo i Gabato 277 tys. (Qarth), Sagora 275 tys. (Volantis). Najmniej ludne: 11 wsi Daru Nocnej Strazy po 1.3-2.1 tys., potem Hornoth (Sarnor) 4.4 tys.

Ocena [S]:
- **Sensowne**: sumy krain Westeros i ich kolejnosc (Reach > Westerlands > Dorzecze > Polnoc i Dolina > Krainy Burzy > Dorne > Zelazne Wyspy); udzial miejski Westeros 5-10%.
- **Watpliwe 1 - miasta Essos.** Udzialy miejskie 25-43% daja po 190-630 tys. ludzi na KAZDE miasto krainy. New Ghys (465 tys.) i trzy mniejsze miasta Volantis (291-317 tys.) wychodza wieksze niz Krolewska Przystan. To skutek rownego podzialu w srodku krainy, nie decyzji o konkretnym miescie.
- **Watpliwe 2 - kolejnosc w Westeros.** Duskendale 185 tys. przy White Harbor 33 tys. i Oldtown 144 tys.: kraina "Krolewska Przystan" ma 2 miasta i 75% udzialu miejskiego, wiec oba dostaly po polowie.
- **Watpliwe 3 - Winterfell, Riverrun, Storm's End po 33-36 tys.** jako "miasta": to zamki z podgrodziem; w ksiedze sa rowne portom.
- Zadna z tych watpliwosci nie zmienia sum krolestw. Zmienia to, co dostanie pojedyncze miasto, gdy gospodarka pojdzie "od ludzi".

### 4.4 Krainy szczegolne: ludzie wobec wojska i osad [P]

| krolestwo | ludzie tys. | mezczyzni 16-60 tys. | twierdz | wsi | ludzi na twierdze tys. | druzyny | zalogi | milicja | wojsko % mezczyzn | z milicja % mezczyzn | rekruci w roku % mezczyzn | udzial w ludziach swiata % | udzial w dochodzie z ziemi % | udzial w wojsku % |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| The Reach | 8 346 | 2 253 | 20 | 49 | 417.3 | 5 662 | 5 973 | 6 406 | 0.52 | 0.80 | 1.02 | 16.21 | 10.80 | 7.16 |
| Volantis | 4 994 | 1 348 | 7 | 18 | 713.4 | 2 924 | 2 949 | 2 155 | 0.44 | 0.60 | 0.57 | 9.70 | 3.76 | 3.61 |
| King's Landing | 4 786 | 1 292 | 21 | 49 | 227.9 | 5 966 | 7 027 | 6 569 | 1.01 | 1.51 | 1.69 | 9.30 | 10.11 | 7.99 |
| Qarth | 3 983 | 1 075 | 4 | 11 | 995.8 | 2 858 | 2 060 | 1 496 | 0.46 | 0.60 | 1.01 | 7.74 | 2.41 | 3.02 |
| The North | 3 226 | 871 | 23 | 60 | 140.3 | 8 272 | 9 283 | 7 573 | 2.02 | 2.88 | 2.09 | 6.27 | 11.86 | 10.80 |
| The Vale | 3 222 | 870 | 14 | 34 | 230.1 | 5 591 | 5 741 | 4 599 | 1.30 | 1.83 | 1.55 | 6.26 | 7.77 | 6.97 |
| Stormlands | 2 756 | 744 | 17 | 39 | 162.1 | 5 242 | 5 657 | 5 178 | 1.46 | 2.16 | 3.16 | 5.35 | 6.42 | 6.70 |
| Riverlands | 2 740 | 740 | 12 | 32 | 228.4 | 5 567 | 4 607 | 4 329 | 1.38 | 1.96 | 3.45 | 5.32 | 6.65 | 6.26 |
| Braavos | 2 594 | 700 | 6 | 15 | 432.4 | 1 741 | 2 065 | 2 015 | 0.54 | 0.83 | 1.19 | 5.04 | 1.57 | 2.34 |
| Targaryen (Meereen) | 1 974 | 533 | 6 | 17 | 329.0 | 2 311 | 2 155 | 1 779 | 0.84 | 1.17 | 1.94 | 3.83 | 3.02 | 2.75 |
| Pentos | 1 488 | 402 | 5 | 13 | 297.5 | 1 088 | 1 980 | 1 754 | 0.76 | 1.20 | 1.59 | 2.89 | 1.72 | 1.89 |
| Qohor | 1 487 | 401 | 5 | 13 | 297.3 | 1 239 | 1 834 | 1 616 | 0.77 | 1.17 | 1.52 | 2.89 | 1.66 | 1.89 |
| Norvos | 1 480 | 399 | 5 | 12 | 295.9 | 1 377 | 2 018 | 1 612 | 0.85 | 1.25 | 1.76 | 2.87 | 2.33 | 2.09 |
| Dorne | 1 116 | 301 | 12 | 29 | 93.0 | 5 832 | 4 609 | 3 634 | 3.47 | 4.67 | 6.05 | 2.17 | 6.17 | 6.42 |
| Tyrosh | 1 066 | 288 | 6 | 14 | 177.7 | 2 034 | 1 998 | 1 686 | 1.40 | 1.99 | 2.90 | 2.07 | 1.48 | 2.48 |
| Targaryen (Aegon) | 1 031 | 278 | 4 | 11 | 257.7 | 1 696 | 1 733 | 1 430 | 1.23 | 1.75 | 1.90 | 2.00 | 2.21 | 2.11 |
| Myr | 980 | 265 | 5 | 12 | 196.1 | 1 922 | 1 801 | 1 567 | 1.41 | 2.00 | 2.45 | 1.90 | 1.74 | 2.29 |
| Lys | 888 | 240 | 4 | 10 | 221.9 | 1 225 | 1 088 | 1 234 | 0.96 | 1.48 | 3.40 | 1.72 | 1.20 | 1.42 |
| Dothraki Horde | 733 | 198 | 7 | 17 | 104.7 | 2 522 | 2 322 | 2 423 | 2.45 | 3.67 | 4.11 | 1.42 | 3.29 | 2.98 |
| Lorath | 674 | 182 | 4 | 10 | 168.4 | 1 584 | 1 632 | 1 272 | 1.77 | 2.47 | 4.48 | 1.31 | 2.47 | 1.98 |
| Dragonstone | 509 | 137 | 6 | 15 | 84.9 | 3 331 | 3 060 | 1 843 | 4.65 | 5.99 | 9.81 | 0.99 | 2.24 | 3.93 |
| Iron Islands | 469 | 127 | 10 | 28 | 46.9 | 1 545 | 4 871 | 3 620 | 5.07 | 7.93 | 9.24 | 0.91 | 3.27 | 3.95 |
| Ibben | 288 | 78 | 4 | 9 | 72.0 | 484 | 1 694 | 1 222 | 2.80 | 4.37 | 7.24 | 0.56 | 0.57 | 1.34 |
| Free Folk | 288 | 78 | 7 | 18 | 41.1 | 674 | 2 800 | 2 446 | 4.47 | 7.62 | 8.51 | 0.56 | 1.46 | 2.14 |
| Summer Isles | 108 | 29 | 1 | 3 | 108.0 | 536 | 59 | 363 | 2.04 | 3.28 | 2.41 | 0.21 | 0.08 | 0.37 |
| Yi Ti Exiles | 100 | 27 | 1 | 3 | 100.0 | 60 | 71 | 315 | 0.49 | 1.65 | 3.42 | 0.19 | 0.13 | 0.08 |
| Sarnor | 93 | 25 | 5 | 13 | 18.7 | 1 357 | 668 | 1 119 | 8.03 | 12.47 | 11.15 | 0.18 | 1.94 | 1.25 |
| Skagos | 50 | 14 | 1 | 4 | 50.5 | 310 | 90 | 336 | 2.93 | 5.39 | 6.37 | 0.10 | 0.14 | 0.25 |
| Nights Watch | 20 | 5 | 5 | 11 | 4.0 | 4 049 | 1 748 | 1 614 | 108.52 | 138.73 | 68.91 | 0.04 | 1.51 | 3.57 |

- **Dorne** (1.12 mln ludzi w krolestwie; 12 twierdz, 29 wsi): 2.2% ludzi swiata, a 6.2% dochodu z ziemi i 6.4% wojska. Wojsko to 3.5% mezczyzn, z milicja 4.7%, rekruci roku 6.1% mezczyzn. Liczba ludzi jest sensowna (najmniej ludne z siedmiu krolestw); niesensowne jest to, ze Dorne wystawia tyle wojska co Dolina przy trzeciej czesci jej ludzi.
- **Zelazne Wyspy** (469 tys.; 10 twierdz, 28 wsi): 0.9% ludzi, 3.3% dochodu z ziemi, 3.9% wojska. Wojsko 5.1% mezczyzn, z milicja 7.9%, rekruci roku 9.2%. Miasta po 2.5-4.6 tys. ludzi z zalogami po 560-630. Sama liczba 0.5 mln jest sensowna; nie pasuje do niej 10 twierdz z pelnymi zalogami.
- **Wolni Ludzie** (288 tys.; 7 twierdz, 18 wsi; miasta z zerem ludzi): wojsko 4.5% mezczyzn, z milicja 7.6%. Liczba sensowna (decyzja Jeffa 05.10: 300 tys., "walcza wszyscy dorosli" - PopulationLaw.cs:54). Region Hornfoot ma obciazenie 61% mezczyzn.
- **Nocna Straz** (20 tys. ludzi; 5 twierdz, 11 wsi po 1.3-2.1 tys.): 5 797 ludzi pod bronia = 108% mezczyzn krainy; obciazenie regionow 31-171%. Liczba 20 tys. to ludnosc Daru, nie zrodlo Strazy (zrodla: 10 krain Westeros, LOG:668). Dzis Straz ma 1.5% dochodu z ziemi swiata przy 0.04% ludzi.
- **Sarnor** (93 tys.; 5 twierdz, 13 wsi po 4.4-7.1 tys.): wojsko 2 026 = 8.0% mezczyzn, z milicja 12.5%; rekruci roku 11.2% mezczyzn; wszystkie 5 regionow ponad progiem 20% (20.2-25.2%) i 328 odmow ochotnika dziennie (LOG:128936). Dzis Sarnor ma 1.9% dochodu z ziemi swiata przy 0.18% ludzi. To jedyne krolestwo, w ktorym ksiega ludzi juz dzis hamuje wojsko.
- **Dothrakowie** (733 tys.; 0% miejskich): wojsko 2.5% mezczyzn - malo jak na lud, w ktorym walczy kazdy mezczyzna.

### 4.5 Co stanie sie z nimi przy gospodarce "od ludzi"

Liczby z symulacji (rozdz. 5) [S]:
- Nocna Straz: 62 ludzi wojska zamiast 5 797; 63 zl dziennie z ziemi zamiast 2 473; wies zboza w Darze 1.3 szt. dziennie zamiast ok. 54. Bez wyjatku Straz przestaje istniec.
- Sarnor: 295 ludzi zamiast 2 026; dochod z ziemi 296 zl zamiast 3 164; wies zboza 4.1 szt. dziennie. Norma zalog 5 twierdz (1 726) jest 6 razy wieksza niz cale wojsko "od ludzi".
- Zelazne Wyspy: 1 480 ludzi zamiast 6 416 (norma zalog 3 838); dochod ludu 5.3 tys. zamiast 22.0 tys.; zywnosc na targ 155 szt. zamiast 638.
- Wolni Ludzie: 908 ludzi zamiast 3 474; Dothrakowie 2 314 zamiast 4 844; Dorne 3 523 zamiast 10 441 (norma zalog 3 988 - nie obsadzi twierdz).
- Miasta z zerem ludzi: zero zarobku, zero spozycia, zero podatku.

## 5. Symulacja na papierze [S]

### 5.1 Zalozenia

- Suma swiata kazdej wielkosci zostaje taka jak dzis (s28). Zmienia sie tylko podzial: wedlug ludzi ksiegi.
- Produkcja wsi: kazdy towar i kazdy typ wsi osobno - wies z symbolem zboza dalej daje zboze, ale tyle, ilu ludzi stoi za jej symbolem (dokladnie przyklad Jeffa: Polnoc 10, Reach 37).
- Pieniadz (dochod z ziemi, zarobek ludu, zawor, wplyw rodow), pobor i wojsko: wedlug udzialu krolestwa w ludziach swiata.
- To rachunek, nie bieg gry. Nie uwzglednia, ze bogatsze krolestwo wygrywa wojny, ze towar trzeba przewiezc, ani ze ceny by sie zmienily.

### 5.2 Zywnosc na targ: dzis i "od ludzi"

Spozycie miast "od ludzi" = dzisiejsze spozycie swiata przez mieszczan i zamki (10 208 szt. dziennie) rozdzielone wedlug ludzi miast krolestwa.

| krolestwo | zywnosc na targ dzis szt./d | od ludzi szt./d | zmiana | spozycie miast od ludzi szt./d | bilans od ludzi | pokrycie | zboze dzis | zboze od ludzi | wsi z symbolem zboza | ludzi na taka wies tys. | zboze jednej takiej wsi dzis | od ludzi |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| The Reach | 1 288 | 2 770 | 2.15x | 806 | 1 965 | 344% | 943 | 2 089 | 14 | 166.7 | 55.7 | 128.1 |
| Volantis | 347 | 993 | 2.86x | 1 229 | -236 | 81% | 230 | 626 | 3 | 193.8 | 53.8 | 148.9 |
| King's Landing | 1 015 | 1 006 | 0.99x | 777 | 229 | 130% | 798 | 735 | 11 | 63.8 | 56.2 | 49.0 |
| Qarth | 74 | 210 | 2.83x | 1 452 | -1 243 | 14% | 51 | 135 | 0 | - | - | - |
| The North | 1 299 | 994 | 0.77x | 265 | 729 | 375% | 1 066 | 805 | 16 | 51.5 | 53.8 | 39.5 |
| The Vale | 892 | 1 042 | 1.17x | 204 | 838 | 511% | 611 | 683 | 9 | 75.5 | 53.8 | 58.0 |
| Stormlands | 680 | 696 | 1.02x | 120 | 576 | 580% | 374 | 406 | 4 | 83.8 | 53.8 | 64.4 |
| Riverlands | 733 | 782 | 1.07x | 180 | 602 | 435% | 567 | 567 | 9 | 63.2 | 50.8 | 48.6 |
| Braavos | 294 | 544 | 1.85x | 784 | -240 | 69% | 214 | 396 | 3 | 135.1 | 53.8 | 103.8 |
| Targaryen (Meereen) | 388 | 369 | 0.95x | 801 | -432 | 46% | 320 | 304 | 5 | 67.8 | 53.8 | 52.1 |
| Pentos | 403 | 457 | 1.13x | 483 | -27 | 94% | 309 | 345 | 5 | 78.4 | 53.8 | 60.2 |
| Qohor | 171 | 209 | 1.23x | 485 | -275 | 43% | 107 | 142 | 1 | 111.5 | 53.8 | 85.6 |
| Norvos | 233 | 239 | 1.03x | 586 | -346 | 41% | 201 | 203 | 3 | 68.8 | 53.8 | 52.9 |
| Dorne | 762 | 367 | 0.48x | 90 | 277 | 408% | 433 | 196 | 6 | 30.6 | 53.8 | 23.5 |
| Tyrosh | 262 | 183 | 0.70x | 388 | -205 | 47% | 165 | 120 | 2 | 54.1 | 53.8 | 41.5 |
| Targaryen (Aegon) | 164 | 119 | 0.73x | 367 | -248 | 32% | 98 | 54 | 1 | 24.3 | 53.8 | 18.6 |
| Myr | 290 | 194 | 0.67x | 391 | -197 | 50% | 204 | 135 | 3 | 47.7 | 53.8 | 36.6 |
| Lys | 224 | 151 | 0.67x | 388 | -237 | 39% | 145 | 94 | 2 | 44.1 | 53.8 | 33.8 |
| Dothraki Horde | 260 | 154 | 0.59x | 0 | 154 | brak miast w ksiedze | 175 | 102 | 2 | 40.0 | 53.8 | 30.7 |
| Lorath | 314 | 204 | 0.65x | 240 | -36 | 85% | 244 | 156 | 4 | 44.2 | 53.8 | 33.9 |
| Dragonstone | 450 | 215 | 0.48x | 36 | 179 | 601% | 264 | 115 | 4 | 28.5 | 53.8 | 21.9 |
| Iron Islands | 638 | 155 | 0.24x | 21 | 133 | 728% | 414 | 97 | 6 | 15.9 | 53.8 | 12.2 |
| Ibben | 168 | 78 | 0.46x | 66 | 12 | 118% | 112 | 46 | 2 | 17.8 | 40.3 | 13.7 |
| Free Folk | 236 | 63 | 0.27x | 0 | 63 | brak miast w ksiedze | 82 | 23 | 0 | - | - | - |
| Summer Isles | 23 | 24 | 1.01x | 10 | 13 | 226% | 7 | 7 | 0 | - | - | - |
| Yi Ti Exiles | 33 | 26 | 0.80x | 19 | 7 | 136% | 32 | 25 | 1 | 26.8 | 26.9 | 20.5 |
| Sarnor | 307 | 25 | 0.08x | 19 | 6 | 133% | 209 | 16 | 3 | 5.4 | 53.8 | 4.1 |
| Skagos | 67 | 24 | 0.36x | 0 | 24 | brak miast w ksiedze | 19 | 8 | 0 | - | - | - |
| Nights Watch | 285 | 7 | 0.02x | 0 | 7 | brak miast w ksiedze | 246 | 6 | 4 | 1.7 | 53.8 | 1.3 |

- Przyklad Jeffa w liczbach (cztery ostatnie kolumny - sama wies z symbolem zboza): w Reach srednio 128 szt. dziennie (najwieksza 210), na Polnocy 40, w Volantis 149, w Dorne 24, na Zelaznych Wyspach 12, w Sarnorze 4, w Darze 1.3. Dzis kazda ok. 54. Stosunek Reach do Polnocy 3.2 - tyle, ile wynosi stosunek ludzi na taka wies (167 tys. do 52 tys.); Jeff podal 37 do 10.
- Stawka wychodzi 0.77 szt. zboza dziennie na 1000 ludzi wsi zbozowej (6 589 szt. ze 123 wsi, za ktorymi stoi 8.58 mln ludzi) - to dzisiejsza suma swiata, nie wzorzec historyczny.
- Reach: 2.15 razy wiecej zywnosci niz dzis, Volantis 2.86 razy. Polnoc 0.77, Dorne 0.48, Zelazne Wyspy 0.24, Sarnor 0.08, Nocna Straz 0.02.
- Pokrycie spozycia wlasnych miast: Westeros 130-730% (nadwyzka), Volantis 81%, Braavos 69%, Meereen 46%, Qohor 43%, Norvos 41%, Lys 39%, **Qarth 14%**. Qarth nie ma ani jednej wsi z symbolem zboza.
- Rozrzut w srodku typu wsi (ta sama stawka na czlowieka):

| typ wsi | wsi | ludzi min tys. | mediana tys. | max tys. | max / min | max / mediana |
|---|---:|---:|---:|---:|---:|---:|
| wheat_farm | 123 | 1.3 | 65.5 | 272.7 | 216x | 4.2x |
| fisherman | 65 | 5.5 | 48.8 | 247.5 | 45x | 5.1x |
| lumberjack | 43 | 2.1 | 53.3 | 217.4 | 103x | 4.1x |
| silver_mine | 30 | 11.1 | 68.0 | 217.5 | 20x | 3.2x |
| vineyard | 27 | 1.8 | 67.6 | 302.0 | 164x | 4.5x |
| iron_mine | 26 | 1.9 | 70.5 | 191.9 | 99x | 2.7x |
| trapper | 25 | 7.1 | 45.8 | 155.0 | 22x | 3.4x |
| sheep_farm | 24 | 1.3 | 66.9 | 277.3 | 218x | 4.1x |
| cattle_farm | 24 | 4.4 | 60.4 | 160.9 | 36x | 2.7x |
| flax_plant | 20 | 5.8 | 68.6 | 175.5 | 30x | 2.6x |
| europe_horse_ranch | 20 | 6.9 | 72.2 | 148.6 | 22x | 2.1x |
| olive_trees | 19 | 29.8 | 56.1 | 295.9 | 10x | 5.3x |
| clay_mine | 16 | 1.8 | 68.7 | 193.2 | 106x | 2.8x |
| desert_horse_ranch | 15 | 32.1 | 43.7 | 193.2 | 6x | 4.4x |
| date_farm | 14 | 30.6 | 46.6 | 277.4 | 9x | 6.0x |
| silk_plant | 14 | 24.3 | 155.1 | 275.2 | 11x | 1.8x |
| steppe_horse_ranch | 13 | 5.8 | 68.0 | 217.5 | 37x | 3.2x |
| salt_mine | 12 | 9.6 | 41.3 | 195.7 | 20x | 4.7x |
| swine_farm | 10 | 14.9 | 45.9 | 155.8 | 10x | 3.4x |
| vlandian_horse_ranch | 10 | 77.6 | 122.4 | 295.5 | 4x | 2.4x |

  Wies zboza w Darze (1.3 tys. ludzi) dalaby 216 razy mniej niz Honeybank w Reach (273 tys.): 1.0 szt. wobec 210 szt. dziennie.

### 5.3 Bilans racji "od ludzi"

Jedna miara dla wszystkich: racja = jedzenie jednego czlowieka na dobe. Pole daje 1.2 racji na mieszkanca wsi (jak dzis w ksiedze, Settings.cs:838); jedza wiesniacy, mieszczanie i wojsko krolestwa.

| krolestwo | ludzie wsi tys. | ludzie miast tys. | wojsko | plon przy 1.2 tys. racji/d | potrzeba tys. racji/d | bilans tys. racji/d | bilans % | udzial miejski % | plon potrzebny na wiesniaka |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| The Reach | 7 528 | 818 | 11 636 | 9 033 | 8 357 | 676 | 8.1 | 9.8 | 1.11 |
| Volantis | 3 746 | 1 248 | 5 873 | 4 496 | 5 000 | -504 | -10.1 | 25.0 | 1.33 |
| King's Landing | 3 998 | 789 | 12 993 | 4 797 | 4 799 | -2 | -0.0 | 16.5 | 1.20 |
| Qarth | 2 509 | 1 474 | 4 918 | 3 011 | 3 988 | -977 | -24.5 | 37.0 | 1.59 |
| The North | 2 957 | 269 | 17 555 | 3 548 | 3 244 | 304 | 9.4 | 8.3 | 1.10 |
| The Vale | 3 015 | 207 | 11 332 | 3 618 | 3 233 | 385 | 11.9 | 6.4 | 1.07 |
| Stormlands | 2 635 | 122 | 10 899 | 3 162 | 2 767 | 394 | 14.2 | 4.4 | 1.05 |
| Riverlands | 2 558 | 182 | 10 174 | 3 070 | 2 751 | 319 | 11.6 | 6.7 | 1.08 |
| Braavos | 1 798 | 796 | 3 806 | 2 158 | 2 598 | -440 | -16.9 | 30.7 | 1.44 |
| Targaryen (Meereen) | 1 161 | 813 | 4 466 | 1 393 | 1 979 | -585 | -29.6 | 41.2 | 1.70 |
| Pentos | 997 | 491 | 3 069 | 1 196 | 1 491 | -294 | -19.8 | 33.0 | 1.50 |
| Qohor | 995 | 492 | 3 073 | 1 194 | 1 490 | -296 | -19.9 | 33.1 | 1.50 |
| Norvos | 885 | 594 | 3 396 | 1 062 | 1 483 | -421 | -28.4 | 40.2 | 1.68 |
| Dorne | 1 024 | 91 | 10 441 | 1 229 | 1 126 | 103 | 9.2 | 8.2 | 1.10 |
| Tyrosh | 672 | 394 | 4 032 | 807 | 1 071 | -264 | -24.6 | 37.0 | 1.59 |
| Targaryen (Aegon) | 658 | 373 | 3 429 | 789 | 1 034 | -245 | -23.7 | 36.2 | 1.57 |
| Myr | 584 | 397 | 3 723 | 700 | 984 | -284 | -28.8 | 40.5 | 1.69 |
| Lys | 494 | 394 | 2 313 | 592 | 890 | -298 | -33.4 | 44.4 | 1.80 |
| Dothraki Horde | 733 | 0 | 4 844 | 879 | 738 | 142 | 19.2 | 0.0 | 1.01 |
| Lorath | 430 | 243 | 3 216 | 516 | 677 | -160 | -23.7 | 36.1 | 1.57 |
| Dragonstone | 473 | 36 | 6 391 | 567 | 516 | 52 | 10.1 | 7.1 | 1.09 |
| Iron Islands | 447 | 22 | 6 416 | 537 | 475 | 61 | 12.9 | 4.6 | 1.06 |
| Ibben | 221 | 67 | 2 177 | 265 | 290 | -25 | -8.7 | 23.3 | 1.31 |
| Free Folk | 288 | 0 | 3 474 | 345 | 291 | 54 | 18.6 | 0.0 | 1.01 |
| Summer Isles | 97 | 11 | 595 | 117 | 109 | 8 | 7.7 | 9.8 | 1.11 |
| Yi Ti Exiles | 80 | 20 | 131 | 96 | 100 | -4 | -3.8 | 19.7 | 1.25 |
| Sarnor | 74 | 19 | 2 026 | 89 | 95 | -6 | -6.5 | 20.4 | 1.28 |
| Skagos | 50 | 0 | 400 | 61 | 51 | 10 | 19.1 | 0.0 | 1.01 |
| Nights Watch | 20 | 0 | 5 797 | 24 | 26 | -2 | -7.2 | 0.0 | 1.29 |
| **SWIAT** | 41 128 | 10 361 | 162 594 | 49 353 | 51 651 | -2 298 | -4.4 | 20.1 | 1.26 |

- **Swiat: -2.3 mln racji dziennie (-4.4%).** Przy udziale miejskim 20.1% pole musialoby dawac 1.26 racji na wiesniaka, zeby swiat sie wyzywil; przy 1.2 wyzywi sie swiat z 16.7% mieszczan.
- Westeros ma nadwyzke: Reach +676 tys., Krainy Burzy +394 tys., Dolina +385 tys., Dorzecze +319 tys., Polnoc +304 tys., Dorne +103 tys.; King's Landing wychodzi na zero. Razem Westeros ok. +2.3 mln racji.
- Essos ma niedobor ok. -4.6 mln racji: Qarth -977 tys. (-24.5%), Meereen -585 tys. (-29.6%), Volantis -504 tys. (-10.1%), Braavos -440 tys., Norvos -421 tys., Lys -298 tys. (-33.4%). Kraina z 40% mieszczan potrzebuje pola dajacego 1.7-1.8 racji na wiesniaka.
- Dzis tego nie widac, bo miasta nie jedza z ksiegi racji: nadwyzka wsi (7.5 mln racji dziennie) przepada, a miasta zyja z osobnego licznika gry.

### 5.4 Pieniadz "od ludzi"

| krolestwo | udzial w ludziach % | ziemia dzis zl/d | od ludzi | zmiana | dochod ludu dzis zl/d | od ludzi | zmiana | zawor (pan + korona) dzis | od ludzi | wplyw rodow dzis zl/d | od ludzi | zmiana |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| The Reach | 16.21 | 17 639 | 26 477 | 1.50x | 37 737 | 95 079 | 2.52x | 24 976 | 40 811 | 82 680 | 196 678 | 2.38x |
| Volantis | 9.70 | 6 140 | 15 844 | 2.58x | 26 490 | 56 895 | 2.15x | 11 930 | 24 421 | 58 951 | 117 692 | 2.00x |
| King's Landing | 9.30 | 16 508 | 15 185 | 0.92x | 46 642 | 54 529 | 1.17x | 19 868 | 23 406 | 100 128 | 112 798 | 1.13x |
| Qarth | 7.74 | 3 941 | 12 637 | 3.21x | 16 169 | 45 378 | 2.81x | 9 195 | 19 478 | 39 675 | 93 868 | 2.37x |
| The North | 6.27 | 19 367 | 10 236 | 0.53x | 60 760 | 36 756 | 0.60x | 39 667 | 15 777 | 108 906 | 76 033 | 0.70x |
| The Vale | 6.26 | 12 698 | 10 221 | 0.80x | 47 220 | 36 704 | 0.78x | 20 110 | 15 754 | 68 251 | 75 925 | 1.11x |
| Stormlands | 5.35 | 10 480 | 8 745 | 0.83x | 22 868 | 31 404 | 1.37x | 15 536 | 13 480 | 58 681 | 64 962 | 1.11x |
| Riverlands | 5.32 | 10 856 | 8 694 | 0.80x | 35 885 | 31 221 | 0.87x | 15 659 | 13 401 | 70 197 | 64 584 | 0.92x |
| Braavos | 5.04 | 2 571 | 8 230 | 3.20x | 25 144 | 29 554 | 1.18x | 3 645 | 12 686 | 17 016 | 61 135 | 3.59x |
| Targaryen (Meereen) | 3.83 | 4 941 | 6 263 | 1.27x | 28 506 | 22 492 | 0.79x | 6 434 | 9 654 | 90 170 | 46 525 | 0.52x |
| Pentos | 2.89 | 2 809 | 4 719 | 1.68x | 10 244 | 16 947 | 1.65x | 4 307 | 7 274 | 22 380 | 35 056 | 1.57x |
| Qohor | 2.89 | 2 719 | 4 717 | 1.73x | 12 059 | 16 937 | 1.40x | 3 850 | 7 270 | 20 403 | 35 036 | 1.72x |
| Norvos | 2.87 | 3 814 | 4 694 | 1.23x | 16 036 | 16 856 | 1.05x | 6 449 | 7 235 | 22 289 | 34 869 | 1.56x |
| Dorne | 2.17 | 10 086 | 3 539 | 0.35x | 34 081 | 12 710 | 0.37x | 17 016 | 5 456 | 103 143 | 26 292 | 0.25x |
| Tyrosh | 2.07 | 2 417 | 3 384 | 1.40x | 8 382 | 12 150 | 1.45x | 1 745 | 5 215 | 29 072 | 25 133 | 0.86x |
| Targaryen (Aegon) | 2.00 | 3 607 | 3 270 | 0.91x | 12 566 | 11 742 | 0.93x | 4 317 | 5 040 | 36 738 | 24 289 | 0.66x |
| Myr | 1.90 | 2 842 | 3 110 | 1.09x | 18 546 | 11 169 | 0.60x | 5 477 | 4 794 | 27 694 | 23 104 | 0.83x |
| Lys | 1.72 | 1 959 | 2 816 | 1.44x | 6 183 | 10 113 | 1.64x | 674 | 4 341 | 19 409 | 20 920 | 1.08x |
| Dothraki Horde | 1.42 | 5 381 | 2 325 | 0.43x | 18 967 | 8 349 | 0.44x | 12 951 | 3 584 | 56 168 | 17 271 | 0.31x |
| Lorath | 1.31 | 4 042 | 2 137 | 0.53x | 12 620 | 7 676 | 0.61x | 6 431 | 3 295 | 29 863 | 15 878 | 0.53x |
| Dragonstone | 0.99 | 3 665 | 1 616 | 0.44x | 23 766 | 5 801 | 0.24x | 2 485 | 2 490 | 40 763 | 12 001 | 0.29x |
| Iron Islands | 0.91 | 5 346 | 1 487 | 0.28x | 21 968 | 5 340 | 0.24x | 7 596 | 2 292 | 29 994 | 11 047 | 0.37x |
| Ibben | 0.56 | 938 | 914 | 0.97x | 5 431 | 3 283 | 0.60x | 721 | 1 409 | 12 919 | 6 791 | 0.53x |
| Free Folk | 0.56 | 2 387 | 912 | 0.38x | 9 285 | 3 277 | 0.35x | 216 | 1 406 | 11 274 | 6 778 | 0.60x |
| Summer Isles | 0.21 | 129 | 343 | 2.66x | 1 368 | 1 231 | 0.90x | 413 | 528 | 7 913 | 2 546 | 0.32x |
| Yi Ti Exiles | 0.19 | 208 | 317 | 1.53x | 1 324 | 1 139 | 0.86x | 307 | 489 | 2 098 | 2 356 | 1.12x |
| Sarnor | 0.18 | 3 164 | 296 | 0.09x | 19 254 | 1 065 | 0.06x | 5 575 | 457 | 26 115 | 2 202 | 0.08x |
| Skagos | 0.10 | 228 | 160 | 0.70x | 1 576 | 575 | 0.37x | 131 | 247 | 5 246 | 1 190 | 0.23x |
| Nights Watch | 0.04 | 2 473 | 63 | 0.03x | 5 524 | 225 | 0.04x | 4 107 | 97 | 15 288 | 466 | 0.03x |

- Zyskuja: Braavos (wplyw rodow 3.6 razy), Reach 2.4, Qarth 2.4, Volantis 2.0, Qohor 1.7, Pentos i Norvos 1.6.
- Traca: Polnoc 0.70, Lorath 0.53, Iron Islands 0.37, Dothrakowie 0.31, Dragonstone 0.29, Dorne 0.25, Sarnor 0.08, Nocna Straz 0.03.
- Dzisiejszy "wplyw rodow" zawiera lupy i okupy (27% w roku), ktore od ludzi nie zaleza - te czesc trzeba by liczyc osobno.

### 5.5 Pobor i wojsko "od ludzi"

"Wojsko od ludzi" = dzisiejsze 162.6 tys. swiata rozdzielone wedlug ludzi (czyli wszedzie 1.17% mezczyzn). "Druzyny przy 5.78 zl" = ile ludzi druzyn oplaci dzisiejszy zold druzyn swiata (456.5 tys. zl dziennie) rozdzielony wedlug ludzi, przy 5.78 zl na czlowieka (raport roczny 1.6). "Norma zalog" = miasta x 461 + zamki x 268, czyli tyle ludzi, ile trzeba, zeby trzymac twierdze krolestwa tak jak dzis srednio w swiecie (LOG:128744). To miara pomocnicza, nie twardy wymog gry.

| krolestwo | rekruci w roku dzis | od ludzi | wojsko dzis | od ludzi | zmiana | dzis % mezczyzn | zold od ludzi zl/d | druzyny przy 5.78 zl | norma zalog twierdz | wojsko od ludzi minus norma |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| The Reach | 23 065 | 47 252 | 11 636 | 26 354 | 2.26x | 0.52 | 104 930 | 12 802 | 6 711 | 19 643 |
| Volantis | 7 753 | 28 276 | 5 873 | 15 770 | 2.69x | 0.44 | 62 790 | 7 661 | 2 648 | 13 122 |
| King's Landing | 21 795 | 27 100 | 12 993 | 15 114 | 1.16x | 1.01 | 60 179 | 7 342 | 6 786 | 8 328 |
| Qarth | 10 813 | 22 552 | 4 918 | 12 578 | 2.56x | 0.46 | 50 079 | 6 110 | 1 651 | 10 927 |
| The North | 18 179 | 18 267 | 17 555 | 10 188 | 0.58x | 2.02 | 40 564 | 4 949 | 8 094 | 2 094 |
| The Vale | 13 442 | 18 241 | 11 332 | 10 174 | 0.90x | 1.30 | 40 506 | 4 942 | 4 910 | 5 264 |
| Stormlands | 23 553 | 15 607 | 10 899 | 8 705 | 0.80x | 1.46 | 34 658 | 4 228 | 5 328 | 3 377 |
| Riverlands | 25 559 | 15 516 | 10 174 | 8 654 | 0.85x | 1.38 | 34 456 | 4 204 | 4 374 | 4 280 |
| Braavos | 8 357 | 14 688 | 3 806 | 8 192 | 2.15x | 0.54 | 32 616 | 3 979 | 1 994 | 6 198 |
| Targaryen (Meereen) | 10 348 | 11 178 | 4 466 | 6 234 | 1.40x | 0.84 | 24 822 | 3 028 | 2 380 | 3 854 |
| Pentos | 6 388 | 8 422 | 3 069 | 4 697 | 1.53x | 0.76 | 18 703 | 2 282 | 1 726 | 2 971 |
| Qohor | 6 110 | 8 417 | 3 073 | 4 695 | 1.53x | 0.77 | 18 692 | 2 280 | 1 726 | 2 969 |
| Norvos | 7 011 | 8 377 | 3 396 | 4 672 | 1.38x | 0.85 | 18 603 | 2 270 | 1 726 | 2 946 |
| Dorne | 18 217 | 6 317 | 10 441 | 3 523 | 0.34x | 3.47 | 14 027 | 1 711 | 3 988 | -465 |
| Tyrosh | 8 341 | 6 038 | 4 032 | 3 368 | 0.84x | 1.40 | 13 409 | 1 636 | 1 994 | 1 374 |
| Targaryen (Aegon) | 5 295 | 5 835 | 3 429 | 3 255 | 0.95x | 1.23 | 12 958 | 1 581 | 1 651 | 1 604 |
| Myr | 6 472 | 5 551 | 3 723 | 3 096 | 0.83x | 1.41 | 12 326 | 1 504 | 1 726 | 1 370 |
| Lys | 8 142 | 5 026 | 2 313 | 2 803 | 1.21x | 0.96 | 11 161 | 1 362 | 1 458 | 1 345 |
| Dothraki Horde | 8 125 | 4 149 | 4 844 | 2 314 | 0.48x | 2.45 | 9 214 | 1 124 | 2 262 | 52 |
| Lorath | 8 148 | 3 815 | 3 216 | 2 128 | 0.66x | 1.77 | 8 471 | 1 033 | 1 458 | 670 |
| Dragonstone | 13 484 | 2 883 | 6 391 | 1 608 | 0.25x | 4.65 | 6 402 | 781 | 2 380 | -772 |
| Iron Islands | 11 697 | 2 654 | 6 416 | 1 480 | 0.23x | 5.07 | 5 894 | 719 | 3 838 | -2 358 |
| Ibben | 5 635 | 1 632 | 2 177 | 910 | 0.42x | 2.80 | 3 623 | 442 | 1 651 | -741 |
| Free Folk | 6 609 | 1 628 | 3 474 | 908 | 0.26x | 4.47 | 3 616 | 441 | 2 455 | -1 547 |
| Summer Isles | 703 | 612 | 595 | 341 | 0.57x | 2.04 | 1 358 | 166 | 461 | -120 |
| Yi Ti Exiles | 924 | 566 | 131 | 316 | 2.41x | 0.49 | 1 257 | 153 | 461 | -145 |
| Sarnor | 2 814 | 529 | 2 026 | 295 | 0.15x | 8.03 | 1 175 | 143 | 1 726 | -1 431 |
| Skagos | 868 | 286 | 400 | 159 | 0.40x | 2.93 | 635 | 77 | 461 | -302 |
| Nights Watch | 3 681 | 112 | 5 797 | 62 | 0.01x | 108.52 | 249 | 30 | 1 533 | -1 471 |

- Reach 26.4 tys. wojska (dzis 11.6 tys.), Volantis 15.8 tys. (5.9), King's Landing 15.1 tys. (13.0), Qarth 12.6 tys. (4.9), Polnoc 10.2 tys. (17.6), Dolina 10.2 tys. (11.3).
- **Dziesiec krolestw nie obsadzi wlasnych twierdz tak jak dzis** (cale wojsko od ludzi mniejsze niz norma zalog): Zelazne Wyspy -2 358, Wolni Ludzie -1 547, Nocna Straz -1 471, Sarnor -1 431, Dragonstone -772, Ibben -741, Dorne -465, Skagos -302, Yi Ti -145, Wyspy Letnie -120. Dothrakom zostaje 52 ludzi w polu, Polnocy 2.1 tys.
- Mezczyzn nie brakuje nigdzie: 1.17% mezczyzn to malo. Granica nie jest w ludziach, tylko w pieniadzu i w liczbie twierdz.

### 5.6 Surowce: dzis i "od ludzi"

| towar | wsie swiata szt./d | najwieksi dzis: udzial dzis -> od ludzi | najwieksi od ludzi |
|---|---:|---|---|
| grain | 8 638 | The North 12% -> 9%; The Reach 11% -> 24%; King's Landing 9% -> 9%; The Vale 7% -> 8% | The Reach 24%, The North 9%, King's Landing 9% |
| fish | 2 316 | Dorne 9% -> 5%; The Vale 9% -> 11%; Iron Islands 8% -> 2%; The Reach 8% -> 16% | The Reach 16%, Volantis 11%, The Vale 11% |
| iron | 252 | Iron Islands 15% -> 3%; The Reach 11% -> 23%; Stormlands 11% -> 10%; King's Landing 8% -> 11% | The Reach 23%, King's Landing 11%, Stormlands 10% |
| wool | 336 | The Vale 16% -> 20%; Targaryen (Meereen) 12% -> 10%; The Reach 8% -> 19%; The North 8% -> 4% | The Vale 20%, The Reach 19%, Qarth 13% |
| flax | 508 | King's Landing 17% -> 20%; The North 15% -> 10%; Stormlands 10% -> 13%; Braavos 10% -> 13% | King's Landing 20%, Braavos 13%, Stormlands 13% |
| hardwood | 2 187 | The North 11% -> 9%; The Reach 8% -> 17%; King's Landing 8% -> 9%; Stormlands 7% -> 7% | The Reach 17%, The North 9%, King's Landing 9% |
| cotton | 60 | The Reach 21% -> 19%; Volantis 21% -> 39%; Myr 21% -> 8%; Qarth 14% -> 24% | Volantis 39%, Qarth 24%, The Reach 19% |
| cow | 85 | Riverlands 19% -> 24%; The North 12% -> 9%; The Vale 12% -> 15%; The Reach 6% -> 15% | Riverlands 24%, The Reach 15%, The Vale 15% |
| salt | 546 | King's Landing 19% -> 4%; Stormlands 19% -> 26%; Dorne 15% -> 11%; The Reach 8% -> 7% | Volantis 30%, Stormlands 26%, Dorne 11% |
| clay | 344 | Riverlands 25% -> 27%; Braavos 12% -> 15%; Norvos 12% -> 12%; Volantis 6% -> 17% | Riverlands 27%, Volantis 17%, Braavos 15% |

- Zelazo: Zelazne Wyspy maja dzis 15% zelaza swiata, "od ludzi" 3%; Reach z 11% na 23%. Sol: King's Landing z 19% na 4%, Volantis z 8% na 30%. Bawelna: Volantis z 21% na 39%.
- Dla zboza, ryb i drewna przesuniecie jest zgodne z intuicja (Reach 24% zboza swiata zamiast 11%). Dla kopalin jest sprzeczne z tym, gdzie leza zloza.

### 5.7 Gdzie prosta proporcja daje absurd i jakiej granicy albo wyjatku wymaga

| nr | absurd w liczbach [S] | czego wymaga (kierunek, nie parametr) |
|---|---|---|
| 1 | **Nocna Straz**: 20 tys. ludzi -> 62 zolnierzy, 63 zl dziennie z ziemi, 7 szt. zywnosci, a 5 twierdz z norma zalog 1 533. | Wyjatek: Straz zyje z ludzi krain-zrodel (10 krain Westeros juz jest zrodlem rekruta, LOG:668), nie z Daru. Dar liczyc tylko jako wlasna zywnosc. |
| 2 | **Sarnor**: 93 tys. ludzi, 5 twierdz, 13 wsi -> 295 zolnierzy przy normie zalog 1 726; wies zboza 4.1 szt. dziennie. | Albo wieksza liczba ludzi w tabeli, albo mniej twierdz do obsadzenia; norma zalog musi isc za ludzmi regionu, nie byc stala na twierdze. |
| 3 | **Zelazne Wyspy**: 469 tys. ludzi, 10 twierdz -> 1 480 zolnierzy przy normie 3 838; miasta po 2.5-4.6 tys. ludzi. | Norma zalog od ludzi; osobne zrodlo utrzymania (lup z morza) zamiast ziemi - inaczej Wyspy znikaja jako sila. |
| 4 | **Wolni Ludzie, Dothrakowie** (0% miejskich): 908 i 2 314 zolnierzy. | Wyjatek udzialu walczacych: lud, w ktorym walczy kazdy dorosly, wystawia wiekszy procent mezczyzn niz 1.2% (dla Wolnych Ludzi jest juz taka decyzja, PopulationLaw.cs:54). |
| 5 | **Miasta Essos**: Qarth 626 tys. ludzi, 3 wsie bez symbolu zboza; pokrycie spozycia miast Qarthu 14%; bilans racji -24.5%. Karmienie 626 tys. ludzi stawka partii (1 szt. na 20 ludzi) = 31.3 tys. szt. dziennie - 3.6 raza wiecej niz cale zboze targu swiata. | (a) skala: 1 szt. towaru na targu musi znaczyc ok. 1 000 racji mieszczan (dzis 10 208 szt. dla 10.36 mln) albo mieszczanie jedza z ksiegi racji, nie z targu; (b) granica wielkosci miasta: tyle ludzi, ile wykarmi nadwyzka wsi regionu plus dowoz (port), albo nizszy udzial miejski Essos, albo wyzszy plon w krainach nawadnianych. |
| 6 | **8 miast z zerem ludzi** (Castle Black, Vaes Dothrak, Vaes Diaf, Thenn, Hardhome, Frostfang's Camp, Driftwood Hall, Elyria): zero zarobku, spozycia i podatku. | Podloga: miasto ma co najmniej ludzi zalogi, dworu i targu, albo liczy sie ludzi calego regionu. |
| 7 | **Wsie-karly i wsie-olbrzymy**: wies zboza od 1.0 szt. (Dar) do 210 szt. dziennie (Reach); stosunek 216. Magazyn wsi i woz gry sa stale (zatkana wies = zapas ponad 1.5 W, LOG:129065). | Granica dolna i gorna produkcji jednej wsi albo liczba i ladownosc wozow rosnaca z produkcja; inaczej wsie Reach sie zatkaja, a wsie Daru nigdy nie wysla woza. |
| 8 | **Kopaliny**: zelazo Zelaznych Wysp 15% -> 3% swiata, sol King's Landing 19% -> 4%. | Kopalnia nie daje "od ludzi krainy", tylko od zloza i rak przy kopalni: ludzie jako sufit rak, zloze jako stawka. |
| 9 | **Renta od glowy 40 zl rocznie** = 5.66 mln zl dziennie; caly dochod ludu miast i wsi to ok. 0.88 mln zl dziennie; zaplacone jest 2.6%. | Najpierw DOCHOD ludu musi isc za ludzmi; stawka renty jako czesc tego dochodu, nie stala od glowy (dzis panowie dostaja z ziemi 1.15 zl od glowy rocznie). |
| 10 | **Sila krolestw**: Reach 26.4 tys. wojska, Polnoc 10.2 tys., Dorne 3.5 tys.; Volantis i Qarth razem 28.3 tys. (dzis 10.8 tys.). | To zamierzony skutek decyzji Jeffa ("poludnie bogate"), ale zmienia wynik kazdej wojny - wymaga biegu rocznego, nie strojenia na papierze. |
| 11 | **Kiesa mieszczan na glowach BK**: miasto Qarth ma 22 tys. glow BK na 626 tys. ludzi ksiegi (1 do 28), Pyke 19 tys. glow BK na 4.6 tys. ludzi (4 do 1). | Jedna liczba ludzi dla kiesy: albo glowy BK przeskalowane do ksiegi, albo koszyk i zarobek liczone od ludzi ksiegi ze skala (1 jednostka kiesy = N ludzi). |
| 12 | **Milicja i zalogi stale na osade**: 93 milicji na osade wszedzie; zaloga miasta 410-590. | Jesli wojsko ma isc za ludzmi, milicja i norma zalog tez - inaczej ludne krainy maja wielkie armie polowe i takie same mury jak puste. |

Czego ta symulacja NIE mowi: jaki ma byc plon na czlowieka, ile sztuk towaru na 1000 ludzi i jakie stawki podatku. To zadanie nastepnego kroku (wzorzec sredniowieczny przeliczony na jednostki gry). Tu sa tylko dzisiejsze sumy swiata i ich podzial.

## 6. Liczby "na czlowieka" dzis - sciaga do nastepnego kroku [P, s28]

| wielkosc swiata | razem na dobe | na 1000 ludzi na dobe | na czlowieka na rok |
|---|---:|---:|---:|
| ludzie ksiegi | 51 488 796 | - | - |
| produkcja wsi na targ, wartosc bazowa | 214 673 zl | 4.17 zl | 1.52 zl |
| zboze ze wsi na targ | 8 638 szt. | 0.168 szt. | 0.061 szt. (= 1.2 osobodnia jedzenia) |
| cala zywnosc ze wsi na targ | 12 300 szt. | 0.239 szt. | 0.087 szt. |
| plon ukrytej ksiegi racji | 48.84 mln racji | 949 racji (1 188 na 1000 ludzi wsi) | 345 racji |
| zarobek ludu miast i podzamczy | 586 600 zl | 11.39 zl (56.6 zl na 1000 ludzi miast) | 4.15 zl |
| koszyk ludu (potrzeba) | 1 982 129 zl | 38.5 zl | 14.0 zl |
| wplyw do kies wsi (d364) | 294 830 zl | 5.73 zl (7.17 zl na 1000 ludzi wsi) | 2.08 zl |
| dochod rodow z ziemi | 163 355 zl | 3.17 zl | 1.15 zl |
| renta nalezna (40 zl od glowy) | 5 664 028 zl | 110 zl | 40 zl |
| renta zaplacona (wsie + zawor miast) | 149 364 zl | 2.90 zl | 1.06 zl |
| zawor kas miast: pan + korona | 251 786 zl | 4.89 zl | 1.78 zl |
| wplyw rodow razem | 1 213 424 zl | 23.57 zl | 8.58 zl |
| zold (druzyny + zalogi) | 647 372 zl | 12.57 zl | 4.58 zl |
| wojsko (druzyny + zalogi) | 162 594 ludzi | 3.16 | 1.17% mezczyzn |
| milicja | 73 203 ludzi | 1.42 | 0.53% mezczyzn |
| rekruci | 291 528 w roku | 5.66 w roku | 2.10% mezczyzn rocznie |
| rece warsztatow (doba 1, LOG:305) | 5 920 roboczodni | 0.11 | - |

## 7. Czego nie wiem i ograniczenia

- **Produkcja towarow na region nie jest w logu.** Podzial na krolestwa i regiony to szacunek [S] z typu wsi i szczebla hearth; sumy swiata sa pomiarem. Szacunek nie widzi wsi spladrowanych ani obnizek za spustoszenie i zime (w swiecie razem ok. 5%). Dla 4 wsi typ w pliku mapy rozni sie od typu w grze - przyczyny nie znam.
- **Zuzycia towarow na region nie ma w logu.** Uzylem wydatku ludu w zl, wartosci zuzycia BK i licznika zywnosci warowni.
- Nie czytalem wzoru BK na liczbe ochotnikow u notabla ani wzoru gry na wielkosc zalogi - dla nich mam tylko dane (krolestwa: rekruci r z osadami 0.86, wojsko 0.96; rody: zalogi 0.85).
- Nie wiem, dlaczego dochod ludu na glowe BK rozni sie miedzy krolestwami 0.08-0.61 zl (Braavos 0.61, Sarnor 0.50, Lys 0.15, Yi Ti 0.08) - nie badalem skladu dochodu na miasto.
- Kiesa ludu to probka co 7 dob (4 probki w oknie); przeplywy dnia w tej tabeli maja wiekszy blad niz stany.
- Krolestwo regionu i rodu wziete z doby 364; rekruci roku licza sie do krolestwa, ktore trzymalo region w dobie 364.
- W roku nie bylo zimy (raport roczny 3.1) - plon ksiegi 1.2 i bilans racji dotycza lata i jesieni.
- Liczb ludzi nie sprawdzalem z kanonem ksiazek poza porownaniem kolejnosci miast; tabela krain to estymacje (PopulationLaw.cs:17-19).
- Symulacja 5 to podzial dzisiejszych sum, nie przewidywanie biegu gry.

## 8. Pliki

Katalog: `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\dbc3d3d1-6a1e-4825-afe7-38583389c73c\scratchpad\gosp-ludzie\skrypty\`. Uruchamianie: `python -I nazwa.py` w tej kolejnosci.

| skrypt | co robi | wynik |
|---|---|---|
| d00_prefiksy.py | spis rodzajow linii logu | d00_prefiksy.out.txt |
| d01_ksztalt.py | ksztalt tabel (doby, wiersze, krolestwa) | na ekran |
| d02_wsie_typy.py | typ i warownia kazdej wsi z settlements.xml + podmiany klimatu z logu | d02_wsie_typy.tsv |
| d10_zbierz.py | zbiera tabele i linie "Towary", "Ludnosc" do jednego pliku | d10_dane.json |
| d11_tabele.py | tabele krolestw, przydzial produkcji wsi [S], rozrzut | d11_tabele.out.txt, d11_regiony.tsv, d11_wsie.tsv, d11_rody.tsv, d11_krolestwa.json, d11_wsie_prod.json |
| d12_korelacje.py | korelacje i regresje (regiony, miasta, zamki, rody, krolestwa) | d12_korelacje.out.txt |
| d12b_wspolczynniki.py | "ile za osade, ile za 1000 ludzi" | d12b_wspolczynniki.out.txt |
| d13_wiarygodnosc.py | rozklad ludzi na wies i miasto, skrajne, krainy szczegolne | d13_wiarygodnosc.out.txt |
| d14_symulacja.py | symulacja "od ludzi" | d14_symulacja.out.txt, d14_symulacja.json |
| d15_doba364.py | doba 364 obok sredniej 28 dob | d15_doba364.out.txt, d15_doba364.json |
| d16_tabele_md.py | tabele do tego dokumentu | d16_tabele.json |
| d17_raport.py | sklada ten dokument | ..\rozpoznanie-dane.md |
