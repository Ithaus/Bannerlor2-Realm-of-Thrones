# Projekt sklad8-s: strzaly tanie w kuzni i duzo strzal w swiecie

Jeff 09.10 10:35: "wykucie strzal to powinno byc grosze, strzal bylo mega duzo".
(Kopia projektu z 09.10; sciezki `strzaly/...` i `kopia-*` to pliki robocze sesji 7016f733 w scratchpad, poza repo.
Wdrozenie: CHANGELOG "sklad8-s", commity S0-S4 na `noc/sklad8`.)
Podstawa: `strzaly/logi.md` (logi 174b, grupa11, sklad8), `strzaly/kod-historia.md` (kod HEAD 0e17915 + zrodla),
`docs/HISTORIA-STRZALY.md`. Model liczb: `strzaly/projekt-skrypty/model2.py` (ta sama arytmetyka co `ArmsPricing.Compute`,
`WorkshopLaw.Needs`, `HistoricalPrices.HistCost`, ustawienia domyslne; w `Armoury.json` Jeffa jest tylko `AmmoBatchStacks: 3`,
czyli tyle co domyslnie - sprawdzone grep). Oznaczenia: [Z] zrodlo, [L] log, [K] kod, [S] moj szacunek.

## 0. Co jest nie tak (w jednym akapicie)

Wartosc strzaly JUZ jest groszowa i historyczna: kolczan 30 strzal = 9-25 d, 0.3-0.8 d za strzale; w 1341 korona placila
12 d za snop 24 (0.50 d), 14 d za stalowane (0.58 d) [Z, Close Rolls]. Drogo jest z dwoch powodow:
(1) **kuznia gracza** liczy jako zelazo CALA mase kolczana (drzewca i piora tez), a t6 z valyrianskiej stali:
3 kolczany t6 = 21 sztabek Iron6 = 2 100 d za towar wart ok. 75 d [K `Recipes.cs:290-300`];
(2) **w swiecie strzal brakuje**: 35 miast bez strzal, mediana mnoznika polki 2.51, lordowie placa 30.7 d za kolczan,
maja strzaly dla polowy lucznikow [L]. Strzelarzy jest 0.034 na 1000 ludzi (1 831 roboczodni dziennie), polowa rak stoi
bez rudy albo drewna, i robia 280 kolczanow strzal + 93 beltow dziennie - 8.4 tys. strzal na swiat 52.6 mln ludzi [L].
Dla porownania do Tower w 1359 przyszlo 850 tys. strzal (2.3 tys. dziennie dla samej korony, kraj 2.5-3 mln ludzi) [Z].
Do tego wysokie tiery biora 2x za duzo rudy i 1.5-3x za duzo pracy (t5-6 liczone jak stal szlachetna, q do 1.8),
a dozbrajanie K1 w 41% wymian amunicji schodzi o tier w dol [L].

## 1. Zasady, ktorych projekt pilnuje

- **Nic z niczego**: strzala = drewno (drzewce + wegiel) + kawalek zelaza (grot) + robota. Pioro, klej i nic zostaja
  w robocie jak dzis (gra nie ma towaru "pioro"; ges to zywy inwentarz za 4 d, a pioro bylo produktem ubocznym stad -
  w 1417 hrabstwa oddawaly 6 lotek z kazdej gesi [Z], ges zostawala w stadzie [S] - wiec zdejmowanie gesi z polki
  byloby blednie historycznie; osobny towar "piora" ze wsi to nowa paczka, nie ten projekt). Robota nie jest towarem i nie przenosi zlota (strzelarze: polka -> polka tego samego miasta).
- **Jedna regula gracz/AI**: kuznia gracza, zakladka CRAFT BK (`Patches.cs:169`), strzelarze miasta i wartosc sztuki
  licza z TEJ SAMEJ receptury `ArmsPricing.CostOf` (metal kg, gatunek, dni).
- **Nie cofam**: cen po sztuce, K1, sklad7b. S4 zaweza tylko wymiane AMUNICJI w K1 do prawdziwego "lepszego".
- **Jedna zmiana naraz**: S1..S4 osobne commity; autotest 40 dob trwa ok. 11 min (sklad8: 09:56-10:07), wiec kazdy krok
  ma wlasny test i wlasne progi. S0 (tylko log) i S1 (tylko kuznia gracza - autotest jej nie dotyka) ida w jednym tescie z S2.

## 2. Kroki

### S0. Pomiar (tylko log, przed S2)
`TownFletchers.Work` zapamietuje na dobe dla miasta: powod konca pracy (zysk / ruda / drewno / rece / brak kandydatow /
bunt), rece dzis, zrobione snopy, ruda i drewno na polce. `Flush` dopisuje linie "Miasta bez strzal - powod (172)":
liczby wedlug powodu i lista miast bez strzal (najwyzej 40) w formacie `Astapor (drewno, rece 14.2, zrobiono 0, ruda 157, drewno 0)`.
Stan tylko w pamieci sesji - zadnego nowego napisu w SyncData. Cel: przestac zgadywac, czemu Astapor ze 157 ladunkami
rudy nie ma strzal (pustynia - drewno? czy rece?).

### S1. Kuznia gracza: grot, nie caly kolczan ("wykucie strzal za grosze")
`Recipes.BuildRecipe`, galaz Arrows/Bolts (dzis `heads = floor(waga strzaly x stos / 0.5) x seria`, gatunek `IronForTier`, t6 = Iron6):
- sztabek = `ceil(CostOf(it).MetalKg x AmmoBatchStacks / waga sztabki gatunku)`, min 1; gatunek = `CostOf(it).Grade`
  (po S2: t1-4 Iron2, t5-6 Iron3); drewno 1 ladunek na serie jak dzis (drzewca + wegiel kuzni serii to 16-40 kg, ladunek 100 kg);
- `CostOf == null` -> stary przepis (bez zmian); legendy (`Legendize`) bez zmian;
- linia startowa "Kuznia: amunicja (S1)": 5 najdrozszych kwitow amunicji i liczba kwitow z Iron4-Iron6 (ma byc 0).

| Seria 3 kolczanow | Teraz | S1 (+S2) | Historia [Z/S] |
|---|---|---|---|
| default_arrows 27 x 43 g, t1 | 6 x Iron2 (7.5 d) | **2 x Iron2 (2.5 d)** | grot 10-20 g, 1 kg zelaza = 40-80 grotow |
| vlandic_arrows 25 x 60 g, t3 | 9 x Iron3 (15.8 d) | **2 x Iron2 (2.5 d)** | groty masowe z zelaza (Towton) |
| hardened_125_gr 30 x 125 g, t6 | **21 x Iron6 (2 100 d)** | **5 x Iron3 (8.8 d)** | stalowany grot: +2 d na snop (1341) |
| repeater_light_bolts 120 x 25 g | 18 x Iron2 (22.5 d) | 4 x Iron2 (5.0 d) | - |

Metal wychodzi 0.03-0.10 d na strzale - grosze. Przetopu amunicji nie ma (sprawdzone w kod-historia), wiec nie ma furtki
na sztabki. Zakladka CRAFT BK bierze kwit z `Recipes.For` - zmienia sie razem.

### S2. Receptura i robota strzaly wedlug 1341 (`ArmsPricing.Compute`, tylko case Arrows/Bolts)
| Co | Teraz | S2 | Uzasadnienie |
|---|---|---|---|
| Gatunek grotu (nowe `AmmoGrade`, `GradeFor` dla reszty bez zmian) | t1-2 Iron2, t3 Iron3, t4 Iron4, t5-6 Iron5 | **t1-4 Iron2, t5-6 Iron3** | groty z Towton 1461 i Holm Hill 1471 - zelazo, "ilosc ponad jakosc" [Z Starley i Cubitt]; stalowany grot = zelazo ze stalowym ostrzem: 70% zelaza x 2.5 d/kg + 30% stali x 6 d/kg [S] = 3.55 d/kg = cena Iron3 (3.5) |
| Strata kucia grotu | 1.4 (jak blacha) | **1.2** | grot z preta traci glownie zgorzeline, 15-25% [S]; po zmianie sredni grot 14 g + 2.8 g straty - w pasmie 10-20 g [Z] |
| Dni na kolczan `DaysAmmo` (x `HistAmmoLaborMultiplier` 8, bez zmian) | 0.30 0.33 0.36 0.40 0.45 0.50 | **0.30 0.30 0.30 0.30 0.35 0.35** | 1341: stalowane 14 d wobec 12 d = +17%, a nie +67%; grot bodkin i grot plaski to ta sama robota |
| Jakosc q w dniach (tylko amunicja) | 0.6-1.8 | **0.6-1.15** | rozrzut cen snopa 12-16 d (+-15%) [Z]; q 1.8 dawalo 7.2 dnia na kolczan t6 (4 strzaly na roboczodzien) |

Skutek na kolczan 30 strzal (q = 1; `model2.py`):

| Kolczan | Ruda teraz -> S2 | Drewno | Dni (strzal na roboczodzien) | Wartosc (za strzale) |
|---|---|---|---|---|
| lekki 50 g, t1 | 2.62 -> 2.25 kg | 19.0 -> 16.5 kg | 2.40 (12.5) bez zmian | 10.7 -> 10.5 d (0.35 d) |
| lekki 50 g, t4 | 4.10 -> 2.25 kg | 26.4 -> 16.5 kg | 3.20 -> 2.40 (9.4 -> 12.5) | 15.1 -> 10.5 d (0.35 d) |
| lekki 50 g, t6 | 5.13 -> 2.81 kg | 31.5 -> 19.3 kg | 4.00 -> 2.80 (7.5 -> 10.7) | 18.9 -> 12.3 d (0.41 d) |
| ciezki RBM 125 g, t1 | 6.56 -> 5.62 kg | 47.4 -> 41.3 kg | 2.40 bez zmian | 13.2 -> 12.7 d (0.42 d) |
| ciezki RBM 125 g, t6 | 12.82 -> 7.03 kg | 78.7 -> 48.4 kg | 4.00 -> 2.80 | 24.7 -> 15.0 d (0.50 d) |
| ciezki t6, q 1.8 | 12.82 -> 7.03 kg | 78.7 -> 48.4 kg | 7.20 -> 3.22 | 36.7 -> 16.6 d (0.55 d) |

Przy mieszance tierow z d31-40: **ruda -28%, drewno -23%, roboczodni -14% na kolczan**; jeden ladunek rudy daje
ok. 39% wiecej kolczanow. Wartosc: zwykla strzala 0.35-0.42 d, stalowana 0.41-0.50 d, stosunek stalowana/zwykla 1.17 -
dokladnie 14/12 z 1341. Bramka zysku strzelarzy sie nie przesuwa (wartosc i koszt z tej samej receptury).

**Czego NIE ruszam i czemu**: `HistAmmoLaborMultiplier` zostaje 8 (12.5 strzal na roboczodzien dla zwyklej strzaly).
To miedzy wydajnoscia z ceny 1341 (12 d za snop przy 3 d dniowki = 6-10 strzal na roboczodzien z grotem [S]) a gorna
granica warsztatu Malemorta w St Briavels (1228: on, kowal i fletcher; kwota 100 beltow dziennie "z pomocnikami" [Z] -
przy trzech ludziach 33 na roboczodzien [S]). Mnoznik 6 zbilby wartosc do ok. 0.27 d za strzale - ok. polowa ceny 1341 -
i ruszyl tez bron miotana (`HistoricalPrices.IsAmmo` liczy Thrown). Wiecej strzal ma dac liczba rak (S3), nie tansza robota. Nie ruszam dniowki (3 d), ceny rudy, dymarki
(1.5 kg z 10 kg rudy) ani paliwa - to regula calej kuzni.

### S3. Liczba strzelarzy: `TownFletcherHandsPerArmsHand` 0.3 -> **0.9**
- Historia [Z + S]: w 1359 do Tower przyszlo 850 tys. strzal; przy 11 strzalach na roboczodzien i ok. 280 dniach pracy to
  ok. 275 strzelarzy na pelny etat TYLKO dla korony, w kraju 2.5-3 mln ludzi = **0.09-0.11 na 1000 ludzi** (plus rynek
  prywatny i cwiczenia lucznicze). 0.9 x rece rzemieslnikow broni (5 953-6 100) = ok. 5 400-5 500 roboczodni dziennie
  (3 x 1 831) = **0.10 na 1000** ludzi tabeli (52.6 mln). Razem z rzemieslnikami broni to ok. 0.21 na 1000 - nadal ponizej
  pasma 0.3-0.6 z komentarza `WorkshopLaw.TownHands`.
- Moc: najmniejsze miasto 1.8 -> 5.4 roboczodnia (ok. 2.4 kolczana dziennie), mediana 12.3 -> 37 (ok. 16 kolczanow).
  Teoretycznie ok. 2 400 kolczanow dziennie; realnie ogranicza ruda, drewno i bramka zysku.
- Nadmiar rak nic nie kosztuje: bramka zysku staje przy mnozniku polki ok. 0.93 (polka ok. 1.16 x popyt), wolne rece
  sie nie odkladaja, zlota nie ma. Wartosc i cena sztuki sie nie zmieniaja.
- Do tego: poprawic nieaktualny komentarz `Settings.cs:461` (pisze 850, jest 1 831) z rachunkiem 1359 i `python tools/gen_mcm.py`.
  Klucza nie ma w `Armoury.json` Jeffa - nowa domyslna zadziala.

### S4. K1 a amunicja: tylko w gore i tylko z nadwyzki (`MenUpgrade.Pick`, wylacznik `MenUpgradeAmmoNeedsSurplus`, domyslnie wlaczony)
- Strzaly i belty wymieniane tylko na **wyzszy tier** (`w.Tier > oldP.Tier`); "sila" (Effectiveness) nie wystarczy.
  Dzis 41% wymian amunicji schodzi o tier (t3 vlandic -> t1 range), a stary kolczan idzie do kupca za 1 zl [L].
- Kolczan do wymiany tylko z koszyka, w ktorym polka **nie ma braku** (`SupplyDemand.Factor` <= 1.0 - cena nie wyzsza
  od wartosci). Zolnierz z pelnym kolczanem nie zabiera ostatnich snopow temu, kto nie ma zadnego (zakupy brakow AiGear,
  notable dla ochotnikow). Ta sama regula dla ludzi gracza, lordow i zalog.
- Liczniki w linii "Dozbrajanie": wymiany amunicji w gore, odrzucone "nizszy tier", odrzucone "brak na polce".
- Spodziewany skutek: obrot K1 w amunicji z 190-550 do ok. 60-110 kolczanow dziennie; znika netto 35-85 kolczanow
  dziennie zdejmowanych z miast z brakiem [S]. Czesc roznicy sklad8 - 174b (+11 miast bez strzal).

### Zapasowe (tylko gdy po S4 miast bez strzal > 15 - decyduje linia S0)
- **S5a** (wiekszosc pustych miast: "ruda" albo "drewno"): bramka zysku strzelarzy w miescie z surowcem liczy tez cene
  u sasiada w zasiegu kupcow minus droga (`TradeTransportPercentPer100`) - robota "na wywoz" jak dostawy szeryfow do Tower;
  wywoz zalatwia `SupplyDemand.DailyTrade` (juz obejmuje strzaly).
- **S5b** (puste miasta z "bez zysku" w ostatnich dniach - polka za plytka na zakup lorda 100+ kolczanow): glebokosc
  popytu na amunicje `SupplyDemandBase` x 3 dla Arrows/Bolts. Ryzyko: w czasie braku wyzszy mnoznik ceny.

## 3. Oczekiwany skutek [S] (srednio d31-40; sklad8 w nawiasie)

| Miara | Po S2 | Po S3 | Po S4 (cel) |
|---|---|---|---|
| Miast bez strzal z 97 (35.0) | 28-33 | 12-18 | **<= 15** |
| Zrobione kolczany s+b dziennie d11-30 (216) | 250-280 | 700-1 100 | jak S3 |
| Mediana mnoznika ceny strzal (2.51) | 2.3-2.5 | 1.0-1.4 | **<= 1.4** |
| Cena kolczana 30 strzal u lorda (30.7 d) | 25-30 d | 11-17 d | **<= 17 d = 0.35-0.57 d za strzale** (1341: 0.50-0.58) |
| Dziura w zbrojowniach AI (16.4 tys. kolczanow) | bez zmian | domknieta ok. d35-45 | jak S3 |
| Pokrycie strzal: lordowie AI d40 (50%) | 50-55% | >= 75% | >= 75% |
| Ruda strzelarzy dziennie (20 ladunkow z 265 wydobytych) | 17-20 | 25-45 | jak S3 |
| Drewno strzelarzy dziennie (132-152 z ok. 2 300 z lasow wsi) | 110-130 | 250-400 | jak S3 |

## 4. Progi testu (autotest 40 dob, porownanie z sklad8; jeden bieg = szum ok. +-5 miast)

**S1 (linia startowa, kuznia w autotescie nie pracuje)**: build kod 0; "Kuznia: amunicja (S1)" - default_arrows
2 x Iron2 + 1 drewno, hardened_125_gr 5 x Iron3 + 1 drewno, kwitow amunicji z Iron4-Iron6: 0 (poza legendami).

**S0 + S2 (jeden test)**:
- start, linia "Ceny surowcow" (arrows): default_arrows <= 11 d, hardened_125_gr 14-16 d, zaden kolczan strzal/beltow
  (poza giant i repeater) ponad 0.6 d za strzale;
- d31-40: ruda na kolczan (`skrypty/zuzycie.py`) <= 4.6 kg (5.3-6.4), roboczodni na kolczan <= 2.35 (2.65);
- miast bez strzal <= 33; zrobione s+b >= 400 dziennie (373); linia S0 obecna co dobe; potkniecia 0.

**S3**:
- miast bez strzal d31-40 <= 18 (cel 15); > 24 = porazka, analiza linii S0;
- zrobione s+b srednio d11-30 >= 550; mediana mnoznika strzal d31-40 <= 1.4; lord placi za kolczan d31-40 <= 17 d;
- pokrycie strzal lordow AI d40 >= 70%, zalog >= 94%; "AI bez towaru" strzaly <= 50/d (98); awanse cofniete <= 25/d (51);
- ruda strzelarzy <= 50 ladunkow/d; zapas rudy miast d40 >= 2 500 i rosnie w d31-40; cykle platnerzy zatrzymane dla
  strzelarzy <= 30/d (0-9); zbroja korpus t3-4 na polkach d40 >= 80 szt. (90);
- drewno strzelarzy <= 450 ladunkow/d; miast bez drewna <= 12 (9-10); potkniecia 0.
- Jesli zbroja albo ruda przekroczy prog, a miast bez strzal <= 15 - cofnac do 0.6 i powtorzyc.

**S4**: wymian amunicji K1 w dol 0; zakupy amunicji K1 d31-40 <= 115/d (189); miast bez strzal d31-40 **<= 15**;
pokrycie lordow nie nizsze niz po S3; licznik "kupione i sprzedane te same id tej samej doby" nizszy niz w sklad8 (114).

## 5. Ryzyko / co sprawdzic przy wdrozeniu (zasada 0 CLAUDE.md)

- S2 zmienia `Value` strzal i beltow (HistCost, przeliczane przy starcie sesji; t1 -2..-4%, t3 -20%, t4-t6 -30..-39%,
  q 1.8 do -55%): polki starego zapisu dostana nowa cene, lordowie zaplaca mniej zlota miastom za kolczan;
  `MendMaterial` (naprawa) i `WorkshopLaw.Needs` czytaja ten sam koszt - sprawdzic, czy naprawa amunicji w ogole istnieje.
  `GradeFor` uzywaja tez zbroje i bron - nowy gatunek TYLKO w case Arrows/Bolts. `DaysAmmo` jest tylko w tym case,
  bron miotana (`DaysThrown`, `HistAmmoLaborMultiplier`) bez zmian.
- S1: wszystkie miejsca z `Recipes.For` dla amunicji - `Patches.cs:169` (materialy CRAFT BK), `ArmouryBehavior.cs:1244`
  (zegar kuzni i XP od `rr.Tier` - bez zmian), `SmithMenu` (wyswietlanie), `SmeltTab` (amunicji nie przetapia).
  Sprawdzic, czy tanszy kwit nie robi z amunicji taniej drogi do XP kowalstwa (stamina amunicji juz x0.05).
- S3: wiecej krokow petli strzelarzy (do ok. 140 w najwiekszym miescie, bezpiecznik 200) - czas w linii Cost174 SFletch.
  `FletchersBidForOre` przy braku strzal oferuje rudzie platnerzy - z S2 strzelarze zarabiaja wiecej na ladunku, wiec
  w pierwszych tygodniach moga zabrac wiecej rudy (limit 5 ladunkow na miasto na dobe). Prog zbroi w S3.
- S4: K1 jest tez dla ludzi gracza - ta sama regula; `SupplyDemand.Factor` liczyc raz na koszyk w `Pick`. Zamek: polka zamku
  ma wlasny popyt (polowa) - warunek dziala tak samo.
- Zadnego nowego stanu w zapisie (S0 i S4 to liczniki sesji). Cen po sztuce, K1 (poza amunicja), sklad7b, 172b - bez zmian.
- Wpis CHANGELOG przy wdrozeniu: sklad8-s (S0-S4 osobno), status NIEWGRANE, z progami z rozdz. 4.

## Zrodla
- Close Rolls 1341 (York: 12 d snop zwykly, 14 d stalowany) i 1359 - M. Easton, "Medieval English arrow heads made of steel" (`strzaly/web/easton.txt`).
- D. Starley, R. Cubitt, Historical Metallurgy 48 (2014) - groty z Towton i Holm Hill z zelaza (`strzaly/web/hms.txt`).
- St Briavels: Malemort, William the Smith, William the Fletcher (1228), do 100 beltow dziennie - https://castellogy.com/?p=5681 ; medievalists.net (Bachrach, kwota 100 beltow dziennie "z pomocnikami", liczby pomocnikow brak).
- `docs/HISTORIA-STRZALY.md`: 1341-1359 1.23 mln strzal, 1359 850 tys., Tower 1360 566 tys., 1343 7 000 strzal oczyszczonych i opierzonych przez 10 ludzi w 6 dni, 1417 piora 6 z gesi.
