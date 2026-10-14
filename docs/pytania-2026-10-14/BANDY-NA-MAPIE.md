# Bandy na mapie: ile band i bandytow (pytanie Jeffa 14.10, tylko odczyt)

Pytanie: "ile jest ogolnie band i bandytow na mapie". Dokument tylko odpowiada na pytanie. Nic w modach nie zmieniano, gry nie uruchamiano, nie bylo commita.
Oznaczenia: **[P]** pomiar (plik + linia albo skrypt), **[K]** odczyt z kodu albo XML, **[S]** szacunek (podana metoda). Czego nie da sie ustalic: "nie wiem".

Logi (kopie w `D:\Backup-Bannerlord\logs\2026-10-14-noc\`):
- **L2** = `Armoury-2026-10-13_21-14-13.log`: bieg dwuletni, fin21, nowa kampania, 728 dob.
- **W52** = `Armoury-2026-10-14_02-50-28.log`: najnowsza wersja (WGRANIE 52), 120 dob.
- **L120a** = `Armoury-2026-10-13_20-41-38.log`: wczesniejszy bieg, 120 dob.
- Tabele CSV sa w `...\2026-10-14-noc\Logs\<sesja>\`. W `Documents\...\Armoury\Logs` tych folderow juz nie ma.
- CrashScribe: `session-2026-10-13_21-14-11.log`.

Raporty zrodlowe (SP = `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\c36b73ca-1de0-4b1b-80ce-71bd02afe545\scratchpad\bandy`). Skrypty i wyniki posrednie leza obok raportow. To scratchpad, wiec moze zniknac:
- **[PRZ]** `SP\przebieg\RAPORT.md`: liczby w czasie, pula w lesie, trend, porownanie wersji, stosunki do swiata.
- **[SKL]** `SP\sklad\RAPORT.md`: kto liczy sie jako banda, rodzaje (klany), kryjowki, Inni.
- **[TLO]** `SP\tlo\RAPORT.md`: ile dalaby gra bez naszych modow, skad bandy i ludzie, gdzie stoja, co napadaja.

**Numeracja dob.** Liczby to kolejne bloki dobowe logu: d1 to pierwszy blok, d728 ostatni w L2, d120 ostatni w W52 i L120a.
- Linia `Wyrzutki:` podaje "dzien" o 1 wiekszy niz linie `Ludzie` i `Skarbce`... z tego samego bloku [PRZ].
- Wedlug reguly "doba N = dzien 108835 + N" ostatnia linia `Wyrzutki: dzien 109564` wypada na dobe 729, a w W52 `dzien 108956` na dobe 121. [TLO] liczy wlasnie tak.
- To ten sam stan, inaczej ponumerowany.

**Co liczy "band X, ludzi Y".** Linia `Wyrzutki:` liczy `MobileParty.AllBanditParties`, czyli kazda partie z `BanditPartyComponent`, takze bande siedzaca w kryjowce. Kod: OutlawLaw.cs:1231, MobileParty.cs:264 i 4181, CampaignObjectManager.cs:549-553 [K].
Niezalezna kontrola: linia `Ludzie` ("bandy 21618 (1097 partii)", L2 l. 279637) daje te same liczby kazdego dnia [P][SKL].

## 0. Dla Jeffa (prosto)

1. **Najnowsza wersja, po 120 dobach nowej kampanii: 625 band i 10 780 bandytow**, czyli ok. 17 ludzi na bande [P W52 l. 45682]. To bieg probny, a nie gra, ktora teraz trwa.
   - Bieg przed ta wersja mial prawie to samo: 617 band i 10 566 ludzi [P]. Ostatnie wgranie liczby band wiec nie zmienilo [S]. Miedzy biegami liczby i tak rozjezdzaja sie o kilka procent, np. w dobach 57-84 bylo 508 i 489 band [P].
   - W tej liczbie sa tez bandy siedzace w kryjowkach (na mapie ich nie widac) i piraci na morzu [K]. Ile band siedzi w kryjowkach, nie wiem: log tego nie liczy.
2. **Dwa lata gry:**
   - na starcie ok. 380 band i 5 800 bandytow,
   - po pol roku ok. 710 band i 14 tys. ludzi,
   - po roku ok. 930 band i 19 tys.,
   - na koniec 2. roku **1 097 band i 21 618 bandytow** (ok. 20 na bande).
   Najwiecej bylo 1 117 band (doba 717) i 22 050 bandytow (doba 689) [P L2].
3. **Wzrost mocno zwalnia, ale w 2. roku jeszcze nie stanal.**
   - W 1. roku przybylo ok. 13 tys. bandytow (z 6 do 19 tys.). W 2. roku przybylo ok. 2,5 tys. (+13%), a band +150 (+16%) [P].
   - W 2. roku bandy rosna juz tylko ludzmi z lasu. Bandyci razem z lasem stoja: 26,5 tys. na poczatku 2. roku i 26,0 tys. na koncu, po drodze szczyt 28,4 tys. [P].
   - Las sie kurczy (7,4 tys. -> 4,4 tys.), wiec bandom zostaje coraz mniej do wziecia [S]. Co bedzie po 2 latach, nie wiem.
4. **"W lesie" czekaja wyrzutki bez bandy.** Na mapie ich nie widac. Po 120 dobach jest ich 1 977, na koniec 2 lat 4 428, w 2. roku srednio ok. 6 800, a najwiecej bylo 9 889 [P].
   - Wiekszosc to dawni zolnierze (dezerterzy i rozbici z bitew): po 120 dobach ok. 3/4-4/5, po 2 latach ok. 9 na 10 [P].
   - W 2. roku czlowiek siedzi tam srednio 20-30 dni, a po 120 dobach ok. 6 dni [P; log liczy to jako stan lasu / dzienny odplyw].
   - Z tych, ktorzy odchodza z lasu, 4 na 5 trafia do bandy, a 1 na 5 wraca do domu [P].
5. **Kto to jest:** 13 rodzajow band. Dezerterow gry nie ma, bo nasz mod ich wylacza [K; P: 0 w bitwach].
   - W bitwach ponad polowa to Broken Men (lupiezcy wloczacy sie przy wsiach). Ok. 1/3 to bandy z kryjowek: pustynni, gorscy, lesni, Dothrakowie, rycerze-rabusie, Kingswood i inni. Ok. 1/9 to piraci morscy [P].
   - Na samej mapie bandy z kryjowek maja wiekszy udzial: po 2 latach ok. 2/3-3/4, po 120 dobach ok. 40-60% [S, z kas kryjowek]. Czesc z nich siedzi w samych kryjowkach.
6. **Inni (wighty) nie sa wliczeni.** Licze ich osobno: po 2 latach 5 hord i 8 925 trupow, po 120 dobach najnowszej wersji 4 hordy i 2 104 [P].
7. **Gdzie stoja bandy:** ok. 40-45% w Essos, ok. 35-43% w Westeros na poludnie od Polnocy, 11-17% na Polnocy, do 5% za Murem [P, koniec biegow]. Wyrzutki w lesie siedza tam, gdzie sa wojny: Dorzecze i Dorne maja 2/3 calego lasu [P].
8. **Na tle swiata:** po 120 dobach 1 bandyta przypada na ok. 4 800 mieszkancow, po 2 latach na ok. 2 400 [P].
   - Na jednego bandyte przypada ok. 24 zolnierzy rodow, zalog i milicji po 120 dobach, ok. 15 po roku i ok. 10 po 2 latach [P].
   - Do konca 1. roku ta liczba spada, bo bandytow przybywa. W 2. roku spada glownie dlatego, ze ubylo wojska: druzyny rodow -40% (115 tys. -> 69 tys.), zalogi -35% (90 tys. -> 58 tys.), a bandytow +13% [P L2]. Przyczyny spadku wojska nie badalem.
9. **Gra bez naszych modow trzymalaby wiecej band** [S, z kodu gry]:
   - na starcie ok. 600,
   - po 4 miesiacach do ok. 1 400 (u nas po 120 dobach ok. 45% tego),
   - po 2 latach do ok. 1 600 (u nas ok. 2/3),
   - do tego piraci morscy, ktorych liczby nie znam.
   Ludzi byloby wtedy ok. 22-26 tys. [S]. To gorny pulap przy zalozeniu, ze nikt nie czysci kryjowek, a nie pomiar.
10. **Dlaczego u nas mniej:**
    - Gra "zamawia" bande, a ta powstaje tylko wtedy, gdy w okolicy sa ludzie w lesie [K]. 2 z 3 zamowien koncza sie pusta banda, ktora znika w godzine [P].
    - Kryjowka, z ktorej puste bandy zniknely, nie zostaje zajeta, a gra dorabia bandy tylko przy zajetych kryjowkach [K].
    - Ktory z tych powodow wazy wiecej, nie wiem.
11. **Co robia bandy:** bija glownie tabory wsi (ok. 10 napadow dziennie, wygrywaja 9 na 10), a piraci bija rybakow i konwoje. Wsi prawie nie lupia, przez 2 lata zrobily to 3 razy. Wsie lupia lordowie [P].

## 1. Stan teraz i na koniec biegow

| co | W52 (najnowsza), koniec 120 dob | L120a (wczesniejszy), koniec 120 dob | L2 (dwuletni), koniec 728 dob | zrodlo |
|---|---|---|---|---|
| band | **625** | 617 | **1 097** | [P] W52 l. 45682, L120a l. 45680, L2 l. 279487 |
| bandytow | **10 780** | 10 566 | **21 618** | jw. |
| srednio na bande | 17.2 | 17.1 | 19.7 | jw. |
| pula "w lesie" (zolnierze / prosci) | 1 977 (1 639 / 338) | 1 440 (1 206 / 233) | 4 428 (4 062 / 366) | jw. |
| Inni (wighty, POZA bandami) | 4 partie, 2 104 | 4 partie, 2 058 | 5 partii, 8 925 | [P] W52 l. 45702, L2 l. 279508; CrashScribe session-2026-10-13_21-14-11.log l. 17316 [SKL][PRZ] |
| ludnosc swiata (linia `Ludzie`) | 51.925 mln | 51.9 mln | 51.618 mln | [P] W52 l. 45824, L2 l. 279637 |
| 1 bandyta na ... mieszkancow | ok. 4 800 | ok. 4 900 | ok. 2 400 | [S] ludnosc / bandyci |
| zolnierze: partie rodow + garnizony + milicje (linia `Ludzie`) | 106 465 + 86 162 + 70 687 = 263 314 | nie liczylem | 68 953 + 58 366 + 79 940 = 207 259 | [P] W52 l. 45824, L2 l. 279637 |
| bandyci : ci zolnierze | 1 : 24 | - | 1 : 9.6 | [S] dzielenie |
| inne partie na mapie, ta sama doba | partii rodow 645, karawan 649, taborow wsi 563 | - | partii rodow 490, karawan 537, taborow 562; wszystkich partii 4 733 | [P] jw. + `Partie (E4/0)` [TLO] |

Kontrola wersji, okno d93-120 [PRZ]:
- W52: 585 band, 9 849 bandytow, pula 1 476.
- L120a: 588 band, 9 855 bandytow, pula 1 383.
- L2 w tym samym oknie: 554 band, 9 976 bandytow.
- Roznice: bandy -0.5%, bandyci -0.1%. To szum, W52 liczby band nie zmienia [S].

## 2. Przebieg w czasie (L2, srednie z 28 dob) [PRZ]

| okno (doby) | band | bandytow | sr. banda | band min / maks | pula "w lesie" | w tym zolnierzy / prostych | linie `Wyrzutki:` |
|---|---|---|---|---|---|---|---|
| d1-28 | 380 | 5 836 | 15.4 | 371 / 388 | 211 | 54 / 157 | 666-10761 |
| d85-112 | 539 | 9 515 | 17.6 | 513 / 567 | 1 650 | 1 439 / 211 | 32386-42672 |
| d169-196 | 709 | 14 155 | 20.0 | 685 / 730 | 5 205 | 4 894 / 311 | 64556-74990 |
| d253-280 | 836 | 16 186 | 19.4 | 823 / 844 | 9 345 | 8 824 / 521 | 96985-107493 |
| d337-364 (koniec roku 1) | 933 | 18 936 | 20.3 | 920 / 949 | 7 724 | 7 236 / 487 | 129409-139744 |
| d421-448 | 1 009 | 20 523 | 20.3 | 996 / 1 021 | 7 911 | 7 296 / 615 | 161607-171901 |
| d505-532 | 984 | 20 033 | 20.4 | 962 / 997 | 7 309 | 6 619 / 689 | 193730-204091 |
| d589-616 | 1 004 | 20 201 | 20.1 | 997 / 1 011 | 7 058 | 6 670 / 388 | 225869-236259 |
| d673-700 | 1 090 | 21 701 | 19.9 | 1 076 / 1 107 | 5 203 | 4 807 / 395 | 258340-268647 |
| d701-728 (koniec roku 2) | 1 097 | 21 450 | 19.5 | 1 087 / 1 117 | 4 636 | 4 273 / 363 | 269051-279487 |

Skrajne wartosci calego biegu [P]:
- band: min 371 (d1, l. 666), maks 1 117 (d717, l. 275226),
- bandytow: min 5 559 (d31, l. 11888), maks 22 050 (d689, l. 264459),
- pula: maks 9 889 (d265, l. 101662).

Srednio przez 728 dob bylo 852 band i 16 769 bandytow [P].

Najnowsza wersja W52, okna 28 dob [PRZ]:

| okno | band | bandytow | sr. banda | pula | linie |
|---|---|---|---|---|---|
| d1-28 | 370 | 5 921 | 16.0 | 261 | 671-10780 |
| d29-56 | 415 | 6 301 | 15.2 | 411 | 11162-21343 |
| d57-84 | 508 | 7 901 | 15.5 | 577 | 21729-31949 |
| d93-120 | 585 | 9 849 | 16.8 | 1 476 | 35372-45682 |

### 2a. Czy rosnie bez konca (regresja po dobach, L2) [PRZ]

| okres | band na rok (364 doby) | bandytow na rok | pula na rok | bandyci + pula na rok | wojsko rodow (`Skarbce`) na rok |
|---|---|---|---|---|---|
| rok 1 (d1-364) | +631 | +14 844 | +10 895 | - | +33 963 |
| rok 2 (d365-728) | +120 | +1 715 | -3 542 | -1 827 | -64 603 |
| 2. polowa roku 2 | +293 | +4 129 | -5 461 | -1 332 | +3 428 |
| ostatnie 92 doby | +188 | +854 | -4 278 | -3 424 | -13 333 |

Uwaga [weryfikacja]: to nachylenia prostej, a nie przyrost. Przyrost od doby do doby [P]:
- rok 1: bandytow 6 085 -> 19 074 (+12 989), band 371 -> 947,
- rok 2: bandytow 19 074 -> 21 618 (+2 544, +13%), band 947 -> 1 097 (+150, +16%), las 7 443 -> 4 428.

W roku 2 regresja zaniza przyrost: liczba najpierw rosnie, potem stoi, a pod koniec znow rosnie.

Bandyci razem z pula [P], srednie okien:

| okno | bandyci + pula |
|---|---|
| d253-280 | 25 532 |
| d337-364 | 26 661 |
| d421-448 | **28 434** (szczyt) |
| d505-532 | 27 342 |
| d589-616 | 27 259 |
| d673-700 | 26 904 |
| d701-728 | 26 087 |

Wniosek [S]: w 2. roku bandy rosna juz tylko dzieki ludziom wciaganym z puli, a nowych wyrzutkow nie przybywa. Liczba sie ustala.

Ruch na dobe w roku 2 [P]:
- 15.6-18.2 nowych band (127-218 ludzi),
- 28-38 pustych band usunietych,
- 50-80 ludzi z puli dobranych do istniejacych band,
- 43-108 ludzi z rozwiazanych band wraca do puli,
- w puli czeka sie srednio 20.6-30.5 doby (na starcie 1.6 doby).

### 2b. Na tle swiata [PRZ]

| chwila | bandyci | wojsko rodow (`Skarbce`, tylko druzyny rodow krolestw) | bandyci : wojsko rodow | bandytow na 100 tys. ludnosci (DOM) | 1 bandyta na ... mieszkancow [S] |
|---|---|---|---|---|---|
| d1-28 | 5 836 | 54 010 | 1 : 9.3 | 11.2 | ok. 8 900 |
| d337-364 | 18 936 | 109 093 | 1 : 5.8 | 36.7 | ok. 2 700 |
| d701-728 | 21 450 | 57 625 | 1 : 2.7 | 41.6 | ok. 2 400 |
| W52 d93-120 | 9 849 | 106 007 | 1 : 10.8 | 19.0 | ok. 5 300 |

W 2. roku stosunek "bandyci : wojsko rodow" skoczyl z 1 : 5.8 do 1 : 2.7. Bandytow nie przybylo wiele, to wojsko rodow spadlo o polowe: ze 110 tys. (d365-455) do 58 tys. (d638-728). Przyklady: d450 106 089, d510 63 374, d536 48 210 [P]. Przyczyny nie badalem.
Liczac z zalogami i milicja (tabela 1) jest 1 : 9.6 na koniec 2 lat.

## 3. Sklad: kto jest banda [SKL]

### 3a. Rodzaje band (14 klanow `is_bandit`, z czego 13 czynnych)

| klan (id) | nazwa w grze | jak sie rodzi [K] | kryjowek na mapie ROT [K] | udzial w bitwach L2 (stron band) [P] | udzial W52 [P] |
|---|---|---|---|---|---|
| looters | Broken Men | lupiezcy bez kryjowki, przy wsiach i miastach | 0 | **58.2%** | 54.8% |
| desert_bandits | Desert Brigands | kryjowki | 31 | 7.8% | 6.8% |
| mountain_bandits | Highland Brigands | kryjowki | 49 | 6.3% | 5.3% |
| northern_pirates | Sea Raiders | piraci morscy NavalDLC (na okretach) | 0 | 6.0% | 5.2% |
| southern_pirates | Stepstone Pirates | piraci morscy NavalDLC | 0 | 5.8% | 5.9% |
| steppe_bandits | Rogue Dothraki | kryjowki | 24 | 3.8% | 2.9% |
| robber_knights | Robber Knights | kryjowki | 11 | 3.2% | 4.7% |
| forest_bandits | Woodland Brigands | kryjowki | 67 | 3.1% | 4.9% |
| kingswood_outlaw | Kingswood Outlaws | kryjowki | 3 | 2.3% | 6.2% |
| sea_raiders | Pirates (w ROT banda LADOWA, bez okretow) | kryjowki nadmorskie | 62 | 1.2% | 0.6% |
| yiti_bandits | Lengii Slavers | kryjowki | 6 | 1.0% | 0.3% |
| hilltribesmen | Hill Tribesmen | kryjowki | 4 | 0.6% | 2.2% |
| wildlings | Wildling Renegades | kryjowki | 9 | 0.6% | 0% |
| deserters | Deserters | vanilla, **wylaczeni przez OutlawLaw** (OutlawLaw.cs:38) | 0 | **0** (0 z 20 941 bitew) | 0 |
| (dowolny) | herszt BK (`BanditHeroComponent`) | przy kryjowce | - | nowych: L2 5, W52 0 [P] | |

Udzial w bitwach wedlug rodzaju [P]:
- L2: lupiezcy 58%, bandy z kryjowek 30%, piraci morscy 12%.
- W52: 55% / 34% / 11%.

To jest udzial w bitwach, a nie spis mapy. Lupiezcy bija sie czesciej.

### 3b. Sklad na samej mapie (szacunek z kas kryjowek)

Linia `Paser` podaje sume skarbcow kryjowek K = 150·H + 120·N [K OutlawLaw.cs:1866-1884]. H to kryjowki z mieszkancami, N to bandy z domem w kryjowce [SKL].

| chwila | K [P] | band razem | bandy z domem w kryjowce [S] | bez kryjowki: lupiezcy + piraci morscy [S] |
|---|---|---|---|---|
| L2 poczatek | 31 800 | 371 | 207-240 | ok. 130-165 (35-44%) |
| L2 koniec | 108 720 (l. 279489) | 1 097 | **740-820 (67-75%)** | ok. 280-360 (25-33%) |
| W52 koniec | 50 520 (l. 45684) | 625 | **255-381 (41-61%)** | ok. 245-370 (39-59%) |

Kryjowek na mapie ROT jest 266 [K ROT-Map settlements.xml]. Zarazonych moze byc najwyzej 133 (limit ROT: 20 na klan), na starcie najwyzej 47. Ile ich jest naprawde w biegu, nie wiem.

### 3c. Czego NIE ma w liczbie band

| kto | dlaczego | stan | zrodlo |
|---|---|---|---|
| Inni (Nocny Krol, wighty, klan `ROTclan_126`) | mniejsza frakcja bez `is_bandit`; ich hordy to partie wodzow (`IsLordParty`) | L2 koniec: 5 partii, 8 925; W52: 4 partie, 2 104 | [K] spclans.xml, OthersRise.cs:351; [P] linia S16 (bandy bez nieumarlych) = "band X" przez 848 z 848 dob |
| Wolni Ludzie (`freefolk`) | krolestwo | L2: 7 384; W52: 9 576 | [P] L2 l. 279642, W52 l. 45829 |
| Nocna Straz | krolestwo / rod | L2: 3 640; W52: 7 745 | [P] jw. |
| Bractwo bez Choragwi, Moon Brothers, Wild Hares, Bright Banners, Sons of the Harpy | `is_outlaw` + `is_minor_faction`, bez `is_bandit`: najemnicy / partie wodzow | - | [K] ROT-Content spclans.xml |

## 4. Ile dalaby gra bez naszych modow [TLO]

Czynny model gestosci to ROT `ROTBanditDensityModel` [K ROT.Models/ROTBanditDensityModel.cs l. 12-20; ROT SubModule.cs l. 307]:
- pulap klanu to 12 band na kazda zarazona kryjowke (6 "w srodku" + 6 "wokol" to tylko dwa skladniki tej sumy), liczony razem ze wszystkimi bandami klanu [K BanditSpawnCampaignBehavior.cs `SpawnBanditsAroundHideout`],
- najwyzej 20 kryjowek na frakcje, 5 na starcie,
- kryjowka liczy sie jako zarazona, gdy **w srodku** siedza co najmniej 4 bandy [K Hideout.cs l. 26],
- ROT `ROTBanditLeaveHideouts` wyprowadza bandy z kryjowki, w ktorej siedzi ich 5 lub wiecej (po 0-72 h). W srodku zostaja wiec ok. 4 [K].
- Bez naszych modow przy ok. 92 kryjowkach po 120 dobach ok. 370 band (4 x 92) siedzialoby w kryjowkach, poza wzrokiem gracza [S].

Lupiezcy:
- Pulap = min(7 × zarazone kryjowki, 300). Liczba 300 pochodzi z modelu NavalDLC [K BanditSpawnCampaignBehavior.cs l. 505, NavalDLCBanditDensityModel.cs l. 121].
- Ze to wlasnie NavalDLC jest pod ROT, wnioskuje z kolejnosci zaleznosci modulow [S]. [SKL] tego nie sprawdzal.
- Weryfikacja: w `Documents\...\Configs\LauncherData.xml` NavalDLC stoi przed ROT-Core i oba sa wlaczone [K]. ROT bierze jako poprzedni model ten dodany wczesniej (`GetGameModel`, SubModule.cs l. 307), wiec limit 300 jest bardzo prawdopodobny. Plik jest z 08.10, a w logu nie ma wpisu o czynnym modelu.
- Latki BK (+2 bandy wokol kryjowki, `BanditPartiesLimit` 150) dzialaja tylko na model vanilla, wiec tu nic nie zmieniaja [K VanillaModelTweakPatches.cs l. 188-206].

Szansa zarazenia nowej kryjowki spada do zera od 17 kryjowek na frakcje. Daje to realny sufit 118 kryjowek × 12 = **1 416 band** (teoretyczny: 133 × 12 = 1 596) plus 300 lupiezcow [K l. 292][TLO].

| doba | zarazonych kryjowek (gra) [S] | pulap band gry [S] | L2 [P] | W52 [P] | L120a [P] | nasze / pulap gry [S] |
|---|---|---|---|---|---|---|
| 1-2 | 47.8 | 873 (realny start ok. 611 + piraci) | 371 | 337 | 356 | 39-43% |
| 30 | 67.6 | 1 111 | 382 | 371 | 374 | 33-34% |
| 60 | 81.6 | 1 280 | 467 | 466 | 461 | 36% |
| 120 | 92.2 | 1 406 | 576 | 623 | 620 | 41-44% |
| 180 | 96.3 | 1 455 | 714 | - | - | 49% |
| 364 | 103.3 | 1 540 | 949 | - | - | 62% |
| 728 | 111.3 | 1 635 | 1 097 | - | - | 67% |

Zalozenia symulacji (`SP\tlo\vanilla_sim.py`, 2000 przebiegow `AddNewHideouts`) [TLO]:
- pulap = kryjowki × 12 + 300 lupiezcow,
- bez piratow morskich, dezerterow (do 50) i band hersztow BK,
- kryjowek nikt nie czysci (AI lordow ich nie szturmuje; [S], nie sprawdzone w kodzie AI).

Nasze liczby zawieraja piratow, a pulap gry nie, wiec procent "nasze / gra" jest raczej zawyzony [S].
Ludzi w bandach bez naszych modow bylo by ok. 22-26 tys. [S], liczac po 16.2 na bande (rozmiar z pierwszej doby, l. 666).

### 4a. Skad sa nasze bandy (L2, 2 lata) [TLO][SKL]

| co | ile | zrodlo |
|---|---|---|
| gra zamowila band | 38 680 (53 na dobe; 1. doba: 765) | [P] suma `Partie (E4/0)`, l. 824 |
| z tego dostaly ludzi z puli | **12 946 band, 124 690 ludzi** (17.8 band na dobe) | [P] suma `Wyrzutki: bandy nowe` |
| puste (w okolicy brak ludzi), usuniete w godzine | 25 654 (66%) | [P] suma `puste usuniete` |
| odmowy bramek (spawner zatrzymany) | 2 688 (2 663 w 1. roku) | [P] |
| dobrani do istniejacych band z puli | 46 516 ludzi | [P] |
| jency, ktorzy przystali do band (jedyni spoza puli) | 3 948 (2.3%) | [P] |
| bandy rozwiazane (31 990, w tym 25 654 pustych) | 6 336 niepustych | [P] / [S] |
| bandy rozbite w walce | ok. 5 593 [S] z bilansu; 4 593 przegranych w `Bitwa: H3` [P] | [TLO] |

Kazda banda powstaje na zamowienie mechaniki gry (kryjowka, lupiezcy, piraci NavalDLC, BK), a wszystkich jej ludzi bierze z puli. Kod: OutlawLaw.cs RosterPostfix l. 1372-1395, ApplyAll l. 2369-2457 [K].

Doplyw do puli przez 2 lata: razem **215 276** ludzi (suma 728 linii `Pula wyrzutkow (E4/3)`) [P]:

| skad | ludzi | udzial |
|---|---|---|
| rozbici z bitew | 89 975 | 42% |
| rozwiazane bandy | 52 183 | 24% |
| dezercja | 44 847 | 21% |
| bieda / wojna | 12 102 | 6% |
| spalone wsie | 9 611 | 4% |
| siew | 6 558 | 3% |

Odplyw z puli [P]:

| dokad | ludzi |
|---|---|
| narodziny band | 124 690 |
| werbunek do istniejacych band | 46 516 |
| powrot do domu | 37 487 |
| do Strazy | 2 142 |

## 5. Gdzie sa bandy [SKL][TLO]

Zrodlo: `ludzie-regiony.csv`. Banda liczy sie do regionu, w ktorym STOI. Suma CSV zgadza sie z linia `Wyrzutki` [P].

| strefa | L2 koniec: band / ludzi | L2 srednio 2 lata | W52 koniec | W52 srednio |
|---|---|---|---|---|
| Essos | 463 (42%) / 8 848 | 416 (49%) | 275 (44%) / 5 740 | 198 (42%) |
| Westeros na poludnie od Polnocy | 380 (35%) / 7 515 | 316 (37%) | 269 (43%) / 4 234 | 218 (46%) |
| Polnoc | 187 (17%) / 4 091 | 82 (10%) | 67 (11%) / 658 | 49 (10%) |
| Za Murem | 56 (5%) / 1 016 | 30 (3.5%) | 9 (1.4%) / 86 | 6 |
| Mur i Dar | 10 / 148 | 9 | 3 / 25 | 2 |
| Wyspy Letnie | 0 | 0.7 | 1 / 37 | 0.8 |

Najwiecej band w jednym regionie [P]:
- L2, srednio z 2 lat: Haystack Hall 25.4, Lhazosh 24.8, Gargos 22.8, Qohor 18.8, Strongsong 18.7, Vaes Dothrak 15.5.
- L2, ostatnia doba: Haystack Hall 32, Lhazosh 29, Frostfang's Camp 26.
- W52: Haystack Hall 17.4, Lhazosh 15.2, Vaes Diaf 10.6, Draconys 10.6.
- Bandy staly w 189 z 227 regionow (L2 koniec) i w 163 z 227 (W52 koniec).

Pula (las) wedlug regionu, srednio z 2 lat [P]:
- najwieksza: Pinkmaiden 945, Sandstone 659, Stoney Sept 425, Starfall 424, Harrenhal 412,
- wedlug krainy: Dorzecze (river) 44.7% puli, Dorne (aserai) 20.9%.

Bandy rozkladaja sie szerzej: Polnoc 9.3%, Dothrakowie 8.5%, Dorzecze 6.5% wszystkich band.

Co bandy napadaja przez 2 lata (`bitwy.log`; to dolna granica, bo starcia bez strat sa pomijane) [P][TLO]:
- tabory wsi: 7 186 razy (89% wygranych; to 90% wszystkich napadow na tabory),
- rybacy: 725,
- konwoje: 470,
- karawany: 384 (27% napadow na karawany),
- wsie (Raid): 3 z 2 518.

Bandy byly napadniete 3 899 razy. Broken Men to 53% napadajacych band. Lup band sprzedany paserom: 510 412 sztuk za 11 015 217 zl.

## 6. Czego nie wiem

Kryjowki i rodzaje:
- **Ile band jest na mapie wedlug klanu.** Zaden log (Armoury, CSV, CrashScribe) tego nie wypisuje. Sklad mam tylko posrednio: z bitew i z kas kryjowek.
- **Ile band siedzi w kryjowkach, a ile chodzi po mapie.** Linia `Wyrzutki` liczy obie razem. Bandy w kryjowkach nie sa widoczne na mapie. Log nie liczy ani zajetych kryjowek (`IsInfested`), ani band siedzacych w kryjowkach.
- **Ile kryjowek jest zarazonych w biegu.** Z rownania kas wychodzi tylko przedzial: H ok. 69-133 po 2 latach, ok. 32-133 w W52 [S].
- Banda zachowuje dom takze w oczyszczonej kryjowce. Nie wiem, czy przez to H nie jest wieksze od liczby zarazonych kryjowek.
- **Ilu jest piratow morskich (NavalDLC)** i jaki jest ich limit. Limit to liczba punktow startu w scenie morza ROT, a tego nie odczytalem.
- Ilu jest lupiezcow osobno. W rownaniu kas sa w jednej reszcie z piratami morskimi.
- Ktory limit lupiezcow naprawde dziala. Najpewniej 300 z NavalDLC: tak wynika z kolejnosci w LauncherData.xml (rozdz. 4), ale w logu nie ma wpisu. Przy limicie BK 150 pulap gry bylby o 150 band nizszy.
- Ile band rodzi sie z ktorego zrodla gry (kryjowka, lupiezcy, BK, piraci). Nie jest logowane.

Bez naszych modow:
- Ile band i ludzi naprawde byloby bez naszych modow. Mam tylko pulap [S] z symulacji, zakladajacej, ze nikt nie czysci kryjowek. Do pewnej liczby potrzebny bylby bieg bez Armoury.

Przyszlosc i wersje:
- Co po 728 dobach. Nie ma dluzszego biegu, wiec nie wiem, czy po wyczerpaniu puli bandytow ubedzie.
- Czy W52 w 2. roku da to samo co fin21. W d1-120 obie wersje daja to samo, dalej W52 nie byla puszczana.

Inne:
- Kto rozwiazuje 6 336 niepustych band (znikaja bez sprawcy). Podejrzany jest NavalDLC `TryRemoveWeakPirate` [S], nie sprawdzalem.
- Doplyw do puli wedlug regionu z dezercji, biedy, spalonych wsi i rozwiazanych band. Log podaje te liczby tylko dla calego swiata, a na region tylko naplyw z bitew.
- Dlaczego wojsko rodow spadlo o polowe w d450-540. Poza tym pytaniem.
- Czym dokladnie rozni sie "partie rodow" z linii `Ludzie` (68 953) od "wojsko rodow" ze `Skarbce` (60 184) na koniec L2. Roznica ok. 8.8 tys. jest bliska liczbie Innych (8 925), ale tego nie sprawdzalem.

## 7. Weryfikacja (sceptyk, 14.10)

Wlasne skrypty w `SP\weryfikacja\`, liczone od zera na logach, bez wynikow raportow:
- `wer.py`: parser trzech logow, wynik `wer_out.json`,
- `sumy.py`: sumy z 728 linii `Wyrzutki` i `Pula wyrzutkow (E4/3)`,
- `reg.py`: strefy z `ludzie-regiony.csv`,
- `bitwy.py`: napady band z `bitwy.log`,
- `sim2.py`: druga, niezalezna symulacja kryjowek gry.

Przeczytany kod: OutlawLaw.cs l. 1231-1232, 1372-1395, 1401-1459, 1866-1884, 2386-2393; BanditSpawnCampaignBehavior.cs (caly); Hideout.cs l. 26; ROTBanditDensityModel.cs; ROT SubModule.cs l. 307; ROTBanditLeaveHideouts.cs; NavalDLCBanditDensityModel.cs l. 104-124; BK VanillaModelTweakPatches.cs l. 188-206; `LauncherData.xml`; `ROT_Hideouts\ModuleData\settlements.xslt`.

### 7a. Sprawdzone i zgodne [P]

| liczba z rozdz. 0 | moj pomiar | wynik |
|---|---|---|
| W52 koniec: 625 band, 10 780 bandytow, las 1 977 | to samo, l. 45682 | zgodne |
| L120a koniec: 617 / 10 566 | to samo, l. 45680 | zgodne |
| L2 koniec: 1 097 / 21 618, las 4 428 | to samo, l. 279487 | zgodne |
| L2 maksima: 1 117 band (d717), 22 050 bandytow (d689), las 9 889 (d265) | l. 275226, 264459, 101662 | zgodne |
| L2 srednio: 852 band, 16 769 bandytow; las w 2. roku 6 765 | 852.3 / 16 768.9 / 6 765.3 | zgodne |
| okna L2 d1-28, d169-196, d337-364, d701-728 | 380 / 5 836; 709 / 14 155; 933 / 18 936; 1 097 / 21 450 | zgodne |
| linia `Ludzie` = linia `Wyrzutki` (bandy i ludzie) | 728 z 728, 120 z 120, 120 z 120 dob | zgodne |
| puste bandy nie zawyzaja liczby | "w zbiorze pustych 0" we wszystkich 968 liniach | zgodne |
| Inni: 5 / 8 925 (L2), 4 / 2 104 (W52) | l. 279508, l. 45702 | zgodne |
| 2 z 3 zamowien puste | 25 654 z 38 600 (66.5%); w 2. roku 66.6% | zgodne |
| strefy na koniec (Essos / Westeros pd / Polnoc / Za Murem) | W52 44.1 / 43.1 / 10.7 / 1.4%; L2 42.2 / 34.7 / 17.1 / 5.1% | zgodne |
| napady band na tabory wsi, rabunki wsi | 7 130 (89% wygranych); rabunki 3 z 2 438 (403 linie nie pasowaly do wzorca) | zgodne w granicach 1-3% |
| pulap gry d1 / d120 / d364 / d728 | kryjowek 47.8 / 92.1 / 103.3 / 111.3, czyli 873 / 1 405 / 1 540 / 1 635 band | zgodne |
| wzor szansy nowej kryjowki | od 17 kryjowek szansa < 0 | zgodne [K] |

Inne ustalenia z kodu [K]:
- Mod `ROT_Hideouts` jest wlaczony, ale zmienia tylko sceny kryjowek, a nie ich liczbe ani kultury. Liczba 266 i podzial na kultury w symulacji sa wiec dobre.
- Wylaczenie dezerterow gry potwierdzone: `SkipVanillaDeserters` (OutlawLaw.cs l. 1458, 2393).

### 7b. Co poprawilem w rozdz. 0

1. **Pkt 3 (przesada).** "W 1. roku +15 tys., w 2. tylko +1,7 tys. (+8%)" to bylo nachylenie prostej, a nie przyrost.
   - Prawdziwy przyrost: rok 1 +12 989, rok 2 +2 544 (+13%), bandy w 2. roku +150 (+16%) [P].
   - Teza "liczba stoi przy 1 000-1 100" byla za mocna. W 2. polowie 2. roku bandytow przybywalo w tempie ok. +4,1 tys. na rok.
   - Stoi tylko suma bandyci + las: 26 517 -> 26 046. Zmienilem tytul i tresc punktu. Przy tabeli 2a dopisalem uwage.
2. **Pkt 4 (pomieszane biegi).** "Ok. 90% to zolnierze" i "czeka 20-30 dni" dotyczylo tylko 2. roku L2, a stalo przy liczbie z W52.
   - Po 120 dobach zolnierzy jest 76% (sklad H3) albo 83% (pierwszy segment linii). Czas w lesie: 6 dob.
   - Log nie mierzy czasu czekania, tylko liczy stan / dzienny odplyw.
   - "Potem dolacza do bandy" uscislilem: 81% odplywu idzie do band, 18% do domu [P].
3. **Pkt 5.** Udzial 2/3-3/4 band z kryjowek dotyczy tylko konca L2. Po 120 dobach W52 przedzial z kas kryjowek to 41-61%.
4. **Pkt 8 (zla przyczyna).** Spadek z 24 do 10 zolnierzy na bandyte byl tlumaczony tylko tym, ze "wojsko rodow spadlo o polowe".
   - W L2 (linie `Ludzie` d120 / d364 / d728) jest: 24.4 -> 14.7 -> 9.6.
   - Do konca 1. roku spadek daje przyrost bandytow.
   - W 2. roku daje go ubytek wojska: druzyny rodow 115 348 -> 68 953, zalogi 89 543 -> 58 366, milicje 74 722 -> 79 940; bandyci w tym czasie +13%.
   - Poprzednia wersja porownywala W52 d120 z L2 d728, czyli dwa rozne biegi. L2 d120 daje to samo 24.4.
5. **Pkt 1 ("na mapie").** Liczba `AllBanditParties` obejmuje tez bandy siedzace w kryjowkach, ktorych na mapie nie widac, oraz piratow na morzu. Dopisalem to.
   - W grze kryjowka jest zajeta dopiero przy co najmniej 4 bandach w srodku (Hideout.cs l. 26), a ROT wyprowadza nadwyzke ponad 4.
   - Ile band siedzi w kryjowkach w naszych biegach, nie wiem.
   - "Teraz" zmienilem na "po 120 dobach": to bieg probny, nie trwajaca gra.
   - Wniosek "wgranie nie zmienilo liczby band" oznaczylem [S]. Miedzy biegami liczby rozjezdzaja sie o kilka procent, np. d57-84: 508 i 489 band.
6. **Pkt 9.** Dopisalem, ze to gorny pulap, a nie pomiar.
7. **Pkt 10.** Dopisalem drugi mechanizm z kodu. Gra liczy pulap jako 12 band na zajeta kryjowke. Kryjowka, z ktorej usunieto puste bandy, nie jest zajeta, wiec pulap gry u nas spada [K]. Ktory mechanizm wazy wiecej, nie wiem.
8. **Rozdz. 4 i 6.**
   - "6 w srodku i 6 wokol" opisalem jako dwa skladniki jednego pulapu.
   - Dopisalem `ROTBanditLeaveHideouts` i kolejnosc modulow z `LauncherData.xml`.
   - Przy limicie BK 150 pulap gry bylby o 150 band nizszy.

Nie poprawialem rozdz. 1-6 poza tym, co wyzej: liczby z tabel, ktore sprawdzilem, sa dobre.
