# Historia: warsztaty zbrojeniowe XIV w. a model warsztatow Armoury (wpisy 48-49)

Kwerenda 2026-10-04. Sprawdza model z `WorkshopLaw.cs`, `ArmsPricing.cs Compute()`,
`HistoricalPrices.cs` (`HistCost`, `HistDays`, `FuelPerMetalKg`) i pol `Workshop*` / `Hist*`
w `Settings.cs` wobec zrodel (Anglia, Francja, Wlochy, Rzesza, ok. 1290-1410).
Uzupelnia `docs/HISTORIA-ZBROJE-STRATY.md` (tam: zapasy Tower, zamowienia, straty) - nie powtarzam.

Oznaczenia: **[DOK]** liczba ze zrodla lub opracowania, ktore je cytuje; **[HIST]** szacunek
historyka; **[SZAC]** moje przeliczenie - do gry, nie do cytowania.
Waluta: 1 moneta gry = 1 d. angielski ok. 1300; 12 d. = 1 s., 240 d. = 1 l.

---

## 0. Werdykt w jednej tabeli

| Parametr (kod) | Model | Historia | Ocena | Proponowana wartosc |
|---|---|---|---|---|
| Rece miasta `WorkshopProsperityPerHand` (Settings.cs:377) | dobrobyt/500 (mediana ~10) | 0,3-0,6 rzemieslnika broni/zbroi na 1000 mieszk. kraju | **za malo 15-25x** (pkt 1, 5) | etap 1: **170** (x3); historycznie **35-50** |
| `WorkshopArtisansMax` (Settings.cs:379) | 20 | Mediolan 1288: >100 mistrzow samych platnerzy kolczug, kazdy z wieloma robotnikami | **za nisko** dla stolic | **60** przy x3; historycznie 150-250 |
| `WorkshopArtisansMin` (Settings.cs:378) | 2 | male miasto: kilku kowali + siodlarz | ok | **6** przy x3 (proporcjonalnie) |
| Podzial rak po rowno miedzy 6 cechow (`LineShare`, WorkshopLaw.cs:358-376) | 1/6 kazdy | Paryz 1292: krawcy 197, siodlarze+uprzaz 75, pochwy+nozownicy 80, platnerze ~34, lucznicy+kusznicy 11 | **za duzo lucznikow i tarczownikow, za malo krawcow/siodlarzy** | wagi: krawiec 0.30, platnerz 0.20, miecznik 0.20, siodlarz 0.15, lucznik 0.10, tarczownik 0.05 |
| Warsztat notabla `WorkshopWorkers` (Settings.cs:376) | 6 roboczodni | mistrz + 2-4 czeladnikow/uczniow | ok | bez zmian (6) |
| Kolczuga `days = w*5` (ArmsPricing.cs:183) | haubergeon 9 kg = 45 dni | ~1000 h jednego czlowieka; warsztat z podzialem pracy 40-60 roboczodni; cena 23 s. (zwykly) - 66-73 s. (stalowy) | **zgodne** (model daje 34 s. t3 i 61 s. t6) | bez zmian |
| Plyty `days = w*1.5 + t` (ArmsPricing.cs:182) | pelna zbroja 25 kg t6 = 43,5 dnia, cena ~78 s. | Paryz 1384: zbroja tulowia "na amunicje" - zgodne; pelna zbroja rycerska 1374: komplet 16 l. 6 s. 8 d., Paryz 83-125 l.t. | **dobre dla t1-t3, za tanie 4-6x dla t5-t6** | `days = w*1.5 + t` dla t<=3, `w*(1.5 + 1.0*(t-3)) + t` dla t>3 (t6 25 kg -> 118 dni, ~8 l.) |
| Miecz `DaysWeapon {1,2,3,5,8,12}` (ArmsPricing.cs:60) | t1 ~10 d., t6 ~180 d. | tani miecz 1340s 6 d.; dobry 6 s. - 2 l. | ok | bez zmian |
| Luk `DaysBow*0.5` (`HistBowLaborMultiplier`, Settings.cs:416) | t1 1 dzien, ~6 d. | luk bialy 12 d., malowany 18 d. (1341, 1359) | **za tanio ~2x** | `HistBowLaborMultiplier` **1.0** (t1 ~11 d.) |
| Strzaly `DaysAmmo*8` (ArmsPricing.cs:66, `HistAmmoLaborMultiplier` Settings.cs:411) | t1 2,4 dnia (~11 d.), t6 12 dni (~47 d.) | snop 24 szt.: 12 d. (zwykle groty), 14-16 d. (stalowe) | **t1 zgodne, t4-t6 za drogie 3x** | `DaysAmmo = {0.3,0.33,0.36,0.4,0.45,0.5}` (t6 4 dni, ~18 d.) |
| Wydajnosc dymarki `WorkshopCrudeKgPerOre` (Settings.cs:382) | 10 kg rudy -> 1,5 kg | ruda->lupa 20-40%, lupa->sztaba 60-75%; ruda->sztaba 12-30% | ok (dolna polowa) | bez zmian (1.5; dopuszczalne 2.0) |
| Drewno na rude `WorkshopWoodPerOre` (Settings.cs:383) | 5 kg drewna / kg rudy | wegiel:ruda 1:1-2:1; drewno:wegiel 3-5:1 wagowo | ok | bez zmian (5) |
| Paliwo kuzni `+2.5f` kg drewna/kg metalu (WorkshopLaw.cs:117, HistoricalPrices.cs:126) | ~0,6 kg wegla/kg | przekuwanie lupy na sztabe + kucie wyrobu: kilka kg wegla na kg | **za malo ~5x** | **12.5** kg drewna/kg metalu (+0,44 d./kg) |
| Cena drewna pod wegiel `HistWoodPerKg` (Settings.cs:397) | 0,035 d./kg | drewno opalowe w miescie ~0,03 d./kg, ale wegiel 0,07 d./kg z 4-5 kg drewna -> drewno w lesie <0,015 d./kg | **paliwo dymarki 2x za drogie** | drewno na wegiel liczyc po **0.015** (nowe pole) albo zostawic (+0,7 d./kg metalu) |
| Ruda `HistIronOrePerKg` 0,075 | | Bedburn 1408: wydobycie+kupno+przewoz ~9 d. za konski ladunek (100-150 kg) -> 0,06-0,09 | **zgodne** | bez zmian |
| Wegiel `HistCharcoalPerKg` 0,07 | | Rogers/Bedburn: 0,05-0,08 | zgodne | bez zmian |
| Lupa / surowka `HistCrudeIronPerKg` 2 | | Tudeley (Edw. III): lupa 3 s. 4 d.; przy lupie 25-50 kg -> 0,8-1,6 d./kg | **za drogo ~1,5x** | **1.4** |
| Zelazo kute `HistWroughtIronPerKg` 2,5 | | Leeds Castle (Edw. III) 7 s./cwt = 1,65 d./kg; Clark 1300-49 ~2,5 | ok (gorna granica) | bez zmian |
| Stal 6 / stal przednia 8 | | 2-3x zelaza [SZAC, brak dobrej serii] | ok | bez zmian |
| Skora 4, len 10 d./kg | | skora wolowa garbowana ~3-5; plotno grube 7-13 | ok | bez zmian |
| Placa `HistMasterWageT1` 3 + 1,5/tier (Settings.cs:408-409) | 3 - 10,5 d./dzien | mistrz murarz 1351: 4 d.; ciesla 3 d.; kowal Bedburn 1408: 2 s./tydz. (~4 d.); krolewski platnerz 12 d., hauberger krolewski 6 d., czeladnik 2 d. | ok (srednia warsztatu z czeladzia) | bez zmian; po 1350 mozna T1 = **3.5-4** |
| Zysk `HistProfitPercent` 25 | | brak bezposrednich ksiag; marze kupieckie Datiniego 10-20% netto [HIST] | ok, gorna granica | bez zmian |

---

## 1. (a) Ilu rzemieslnikow

| Osrodek | Dane | Status |
|---|---|---|
| Paryz 1292, taille (ok. 15 000 podatnikow, miasto ok. 200 tys.) | 22 armuriers, 4 haubergiers, 7 chainetiers, 1 trumelier, 11 ferrons; 51 selliers (siodlarze), 24 wytworcow uprzezy, 58 pochwiarzy, 22 nozownikow, 34 kowali; 8 archiers (lukmistrzow), 3 arbaletriers; 197 krawcow | [DOK] Geraud 1837, przez Histoires de Paris i B. Crowder |
| Paryz 1416 | 10 heaumiers, 8 armuriers | [DOK] Histoires de Paris |
| Mediolan 1288 (Bonvesin, ok. 200 tys. wg autora) | "glownych kowali kolczug jest ponad stu, kazdy ma pod soba wielka liczbe robotnikow"; eksport "do miast bliskich i bardzo dalekich" | [DOK] Bonvesin, *De magnalibus*, przez myArmoury |
| Londyn XIV w. | 26-28 sygnatariuszy ordynacji 1322; 38-83 osob z branzy na 25 lat w aktach (Kirkland); 13 haubergerow przez caly wiek | [DOK] - patrz HISTORIA-ZBROJE-STRATY 1.1 |
| York 1381 | 7248 podatnikow poglownego; cechy platnerzy, bowyerow, fletcherow, miecznikow z ordynacjami 1380-1400; 57 cechow w 1415. Liczb osob w zawodach nie znalazlem (Fenwick, *Poll Taxes* cz. 3 - bez dostepu) | [DOK] czesciowo |
| Norymberga 1363 | lista rzemiosl z osobnymi Plattner, Messerer, Klingenschmiede, helmsmiede, Panzermacher. Liczb mistrzow w tych zawodach nie znalazlem (czesto cytowane "1217 mistrzow w ok. 50 rzemioslach" - niepotwierdzone) | [DOK] lista / liczby brak |

**Przeliczenie [SZAC]:**
- Paryz 1292: wszystkie "nasze" cechy (platnerze ~34, siodlarze i uprzaz 75, pochwy/nozownicy 80+,
  luki/kusze 11) = ok. 200 mistrzow placacych podatek, z czeladzia x2-3 = 400-600 osob na 200 tys.
  = **2-3 na 1000 mieszkancow miasta**. Paryz obslugiwal rowniez okolice i dwor.
- Mediolan (osrodek eksportowy): sama kolczuga 100+ mistrzow x 5-10 = **500-1000 osob = 3-5/1000**.
- Kraj (Anglia, z HISTORIA-ZBROJE-STRATY 1.1 i 2.2): platnerze 250-800 + luki/strzaly 300-800,
  plus miecznicy, siodlarze, tarczownicy (w Paryzu tyle co platnerzy) ->
  **0,3-0,6 rzemieslnika broni i zbroi na 1000 mieszkancow kraju** (bez krawcow).

**Model:** suma dobrobytu 464 700 / 500 = ok. 930 roboczodni/dzien (rzemieslnicy miast) + 31
warsztatow notabli x 6 = 186 -> **ok. 1 100 roboczodni/dzien = ok. 1 550 ludzi** (pracujacych 260 dni
w roku) na 52,6 mln = **0,03 na 1000**. Bez krawcow (1/6 rak): ~0,025/1000.
Historia dla 52,6 mln: 15-30 tys. ludzi = **11-22 tys. roboczodni/dzien**. Model jest
**10-20x za maly** (dokladnie: x10-x20 bez krawcow).

**Podzial cechow:** proporcje Paryza (siodlarze ~ pochwiarze+nozownicy > platnerze >> lucznicy)
i Anglii (luki i strzaly liczne, bo longbow) nie pasuja do rownego 1/6. Uwaga: w kodzie dzial jest
na **czynne** cechy (z czyms oplacalnym) - jesli tarczownik nie ma zysku, jego czesc idzie do reszty,
co ogranicza szkode. Propozycja wag w `LineShare` (WorkshopLaw.cs:375, `Hands / c.Value.Count / lines`
-> `Hands * waga[cech] / suma wag czynnych cechow / lines`):
krawiec 0,30 | platnerz 0,20 | miecznik 0,20 | siodlarz 0,15 | lucznik 0,10 | tarczownik 0,05.
Warsztaty notabli (armorsmithy x7, weaponsmithy x12, fletcher x5, barding-smithy x7) -
proporcje 7:12:5:7 sa rozsadne, bez zmian.

---

## 2. (b) Czas pracy na sztuke

| Wyrob | Historia | Model | Ocena |
|---|---|---|---|
| Haubergeon 9 kg | ~1000 h jednego czlowieka (Williams, rekonstrukcje 500-2000 h); w warsztacie z podzialem pracy (drut, ciecie kolek, nitowanie w osobnych pracowniach po 3-6 osob) 1-2 tyg. na dopasowanie gotowych plachet; Wegry 1633: 2 miesiace. Cena 23 s. (zwykly, 1364) - 66-73 s. (stalowy) [DOK] | 45 dni; 34 s. (t3) - 61 s. (t6) | **zgodne** |
| Pelna kolczuga (hauberk 12-14 kg) | 1,3-1,5x haubergeona | 60-70 dni | zgodne |
| Coat of plates / brigantyna 10-12 kg | 2-4 tyg. warsztatu (3-4 osoby) = 40-80 roboczodni [SZAC]; Paryz: "plates" 11-36 l.t. | 20-22 dni | dolna granica, akceptowalne |
| Zbroja tulowia "na amunicje" (Paryz 1384) | 500 kompletow + 300 par nagolennikow w <3 mies. przez dwa warsztaty z podwykonawcami [DOK, Bernard 2015] -> przy modelu (~26 dni/komplet) ~16 000 roboczodni / 80 dni = ~200 ludzi | 26 dni | **zgodne** |
| Pelna zbroja rycerska 25 kg | 1-3 mies. warsztatu (mistrz + 2-4) = 100-300 roboczodni [SZAC]; ceny: komplet zbroi rycerza 1374 16 l. 6 s. 8 d. [DOK, Goucher], Paryz 83-125 l.t. (~16-25 l. st.) | 43,5 dnia, ~78 s. | **za malo 3-5x** -> wzor z tabeli 0 (118 dni, ~8 l.) |
| Basinet / helm | 3-10 dni mistrza z pomocnikiem [SZAC] | plyta 2-3 kg: 4-10 dni | zgodne |
| Miecz | klinga 1-3 dni kowala; oprawa i pochwa osobno (Paryz: 58 pochwiarzy); tani 6 d. (1340s), dobry 6 s. - 2 l. [DOK] | 1-12 dni | zgodne |
| Luk | luk z gotowego kostura: ok. 1 dzien [SZAC]; cena 12 d. (bialy), 18 d. (malowany) [DOK]; kostur cisowy importowany | 1-6 dni x 3-10,5 d. -> t1 ~6 d. | za tanio; mnoznik 1.0 |
| Snop strzal (24) | cena 12-16 d. [DOK]; z ceny: 2-3 roboczodni razem z grotnikiem [SZAC]; grotnik/beltnik XIII w.: 100-200 beltow dziennie [DOK, Storey] | t1 2,4 dnia, t6 12 dni | t1 ok, t6 za duzo (stalowy grot to tylko +2 d. ceny) |
| Tarcza | 1-3 dni (deski, skora, malowanie; robili ja tez malarze i siodlarze) [SZAC] | 1-5 dni | ok |
| Siodlo | 2-5 dni siodlarza [SZAC]; cen XIV w. nie znalazlem | uprzaz skorzana: w + t dni | ok, brak zrodla |

Czas obrobki obejmuje caly warsztat (mistrz + czeladz + uczniowie), wiec placa sredniego roboczodnia
3-10 d. jest srednia z mistrza 6-12 d., czeladnika 2-4 d. i ucznia ~1 d.

---

## 3. (c) Dymarka, paliwo, surowce

**Wydajnosc:**
- Wegiel do rudy w dymarce ok. **1:1** (Wikipedia "Bloomery"); w eksperymentach do 2:1 (Markewitz);
  na lupe: 3-10 kg wegla na kg lupy (Sauder); ruda->lupa 20-40%, lupa->sztaba 60-75% [DOK eksperym.].
- Crew 1991 (piec prehistoryczny, maly): **1 kg kutej sztaby = ok. 100 kg wegla i 25 roboczodni** -
  gorna granica; sredniowieczny piec z miechem wodnym duzo lepszy.
- Bedburn/Byrkeknott 1408 (Durham, miech wodny): do 6 lup tygodniowo (~530 kg), placa blooma
  6 d. za lupe, kowala 6 d. za lupe [DOK, Lapsley 1899 przez Salzmana i Five Nine].
- Wegiel z drewna: **ok. 25% wagi** (100 czesci drewna -> 25 czesci wegla), mielerz 1/3-1/4,
  male mielerze nawet mniej -> 3-5 kg drewna na kg wegla.

**Model:** 10 kg rudy -> 1,5 kg (15%) i 50 kg drewna (~10-12 kg wegla) na 10 kg rudy -> ~7-8 kg wegla
na kg surowki. **Miesci sie w historii.** Paliwo kuzni 2,5 kg drewna/kg metalu (~0,6 kg wegla)
jest za male: przekucie lupy na sztabe (20-40% straty, wiele grzan) i kucie wyrobu zjadaja kilka kg
wegla na kg [SZAC] -> **12.5** kg drewna/kg (WorkshopLaw.cs:117 `c.MetalKg * 2.5f` i
HistoricalPrices.cs:126 `+ 2.5f` - te same liczby w obu miejscach, inaczej warsztat i wycena sie rozjada).

**Ceny:**
- Ruda: Bedburn 1408 - wydobycie 5 s., kupno 2 s., przewoz 2 s. za tuzin konskich ladunkow
  (ok. 9 d. za ladunek 100-150 kg) -> 0,06-0,09 d./kg [DOK / przelicznik SZAC]. Model 0,075 - **ok**.
- Wegiel: Bedburn - wypalenie 2 s. za tuzin ladunkow (sama praca weglarza) [DOK]; Rogers ~4-6 d.
  za quarter (~60-70 kg) -> 0,06-0,09 d./kg [SZAC]. Model 0,07 - **ok**.
- **Niespojnosc:** drewno 0,035 d./kg x 4-5 kg = 0,14-0,18 d. na kg wegla, a wegiel kosztuje 0,07.
  Weglarz kupowal drewno w lesie (podszyt, chrust - Bedburn: "z chrustu") za 0,01-0,015 d./kg.
  Paliwo dymarki w modelu kosztuje ~1,25 d./kg metalu zamiast ~0,6. Poprawka: drewno na wegiel
  po **0.015** (nowe pole `HistCharcoalWoodPerKg`, uzyte w `FuelPerMetalKg` i `WorkshopLaw.Needs`)
  albo zostawic - roznica 0,6 d./kg jest mala przy stali 6-8 d./kg.
- Lupa: Tudeley (Edw. III) 3 s. 4 d. za lupe [DOK, Salzman]; przy wadze 25-50 kg -> 0,8-1,6 d./kg.
  Model 2,0 -> **1.4** (`HistCrudeIronPerKg`, Settings.cs:399).
- Zelazo: Leeds Castle (Edw. III) ok. 7 s. za cwt (50,8 kg) = 1,65 d./kg [DOK, Salzman];
  Clark 1300-49 1,15 d./funt = 2,5 d./kg. Model 2,5 - **ok** (gorna granica; z przewozem do miasta).
  1354: zelazo 4x drozsze niz 1348 [DOK, Kirkland] - wojna podnosi cene metalu.

---

## 4. (d) Place i marze

- Rzemieslnicy budowlani (Rogers/Clark): 2,5 d. (1301-31) -> 5 d. (1381-91); mistrz murarz 1351
  4 d., mistrz ciesla 3 d.; lucznik w wojsku 1346: 3 d. [DOK].
- Branza: krolewski platnerz 12 d. (+ czeladnik 6 d., pacholek 3 d.), krolewski hauberger 6 d.,
  czeladnik haubergera 2 d. (Kirkland) [DOK]; kowal w Bedburn 1408: 2 s. tygodniowo (~4 d.) [DOK].
- Model 3 d. (t1) + 1,5 d./tier (t6 10,5 d.) jako srednia roboczodnia warsztatu - **zgodne**.
  Po Czarnej Smierci (1350+) mozna podniesc T1 do 3,5-4 d.
- Zysk 25%: zgodny z gorna granica marz kupieckich (Datini 10-20% netto, plus transport).
  Uwaga: placa mistrza juz zawiera jego dochod z pracy, 25% to zysk na kapitale i ryzyku - nie
  podnosic.

---

## 5. (e) Roczna produkcja swiata a historia

**Model (symulacja wpisu 48):** ~490 sztuk/dzien, w tym ~22 zbroje i ~2 kolczugi/plyty dziennie
-> **~8 000 zbroi rocznie, ~730 kolczug i plyt rocznie** (rok 364 dni).

**Historia przeskalowana** (Anglia ok. 1340: 5 mln; 52,6 mln = x10,5; z HISTORIA-ZBROJE-STRATY 1.5, rok wojenny):

| Wyrob | Anglia/rok | Swiat gry/rok (x10,5) | Na dzien | Model/dzien |
|---|---|---|---|---|
| Aketon / zbroja pikowana | 4-12 tys. | 42-126 tys. | 115-345 | razem z innymi ~22 |
| Helm | 2-6 tys. | 21-63 tys. | 58-173 | (jw.) |
| Kolczuga | 0,5-2 tys. | 5-21 tys. | 14-58 | ~2 (kolczugi + plyty) |
| Coat of plates / napiersnik | 0,5-2 tys. | 5-21 tys. | 14-58 | (jw.) |
| Pelna zbroja | 0,2-0,8 tys. | 2-8 tys. | 6-23 | - |
| Strzaly (snopy) | 1359: 35 tys. snopow do Tower | ~370 tys. snopow w roku wielkiej kampanii | ~1000 | ? (z ~490 sztuk) |

Drugi sprawdzian - od strony armii: 0,15-0,35% ludnosci pod bronia w roku wojny = 80-180 tys. zbrojnych
na 52,6 mln; 25-45% z metalowa ochrona tulowia = 20-80 tys.; kolczuga/plyty zyja 10-20 lat ->
**wymiana 1-8 tys. rocznie + straty bitewne** - minimum 3-14x wiecej niz model.

**Wniosek:** model daje **10-30x za malo** pancerzy i 15-25x za malo rak. Rozne sprawdziany
(rece na 1000 mieszkancow, produkcja Anglii, zapotrzebowanie armii) zgadzaja sie co do rzedu wielkosci.

**Hamulec:** x10 rak = x10 rudy i drewna (dzis ~440 ladunkow rudy i ~2 400 drewna dziennie; juz teraz
"brak surowca" przy rudzie, wpis 48) i ~x10 wywolan rankingu dziennie. Dlatego **jedna zmiana naraz**:
1. Etap 1: `WorkshopProsperityPerHand` 500 -> **170**, `WorkshopArtisansMax` 20 -> **60**,
   `WorkshopArtisansMin` 2 -> **6** (x3). Sprawdzic w logu: "brak surowca", czas dnia na mapie,
   polki zbroi.
2. Etap 2 (jesli wsie nadazaja z ruda i drewnem): 170 -> 70-100, Max 100-150.
3. Historyczne maksimum: 35-50 (mediana ~100-140 rak), Max 200+ dla stolic typu Mediolan/Paryz.
   W grze to zapewne za duzo dla rynku (wojsko w Bannerlordzie dostaje sprzet z szablonow, rynek
   obsluguje gracza i lordow) - decyzja Jeffa.

Kolejnosc reszty poprawek (kazda osobno): wagi cechow -> plyty t4-t6 -> strzaly t4-t6 -> luk x1.0 ->
paliwo kuzni 12.5 -> lupa 1.4 -> (opcjonalnie) drewno na wegiel 0.015.

---

## 6. Czego nie znalazlem
- Liczby platnerzy/bowyerow/fletcherow z poglownego 1377/1381 (Fenwick, *The Poll Taxes of 1377,
  1379 and 1381*, cz. 3 York - bez dostepu online).
- Liczby mistrzow z listy norymberskiej 1363 w zawodach zbrojeniowych.
- Ceny siodel i tarcz w XIV w. (tylko czasy [SZAC]).
- Waga lupy z Tudeley (przelicznik 0,8-1,6 d./kg to [SZAC]).

## Zrodla
- Geraud, H., *Paris sous Philippe-le-Bel... role de la taille 1292*, 1837 (Gallica): https://gallica.bnf.fr/ark:/12148/bpt6k6430168x/f23
- Histoires de Paris, "Les armuriers et les haubergiers": https://www.histoires-de-paris.fr/armuriers-haubergiers/ ; "Les archiers et les artilliers": https://www.histoires-de-paris.fr/archiers-artilliers/
- Crowder, B., "Occupations in 1292 Paris": https://bencrowder.net/blog/2015/occupations-in-1292-paris/
- Bonvesin de la Riva, *De magnalibus Mediolani* 1288, fragment o platnerzach: http://myarmoury.com/talk/viewtopic.9772.html ; populacja: https://medievalmilanetc.wordpress.com/2013/11/22/a-e-i-o-and-u-bonvesin-de-la-riva-and-the-marvels-of-milan/
- Kirkland, B., *"Now thrive the Armourers"*, PhD York 2015: https://www.academia.edu/35407598/
- VCH York, "Craft organisation and the guilds": https://www.british-history.ac.uk/vch/yorks/city-of-york/pp91-97
- Nurnberg 1363, lista rzemiosl: https://www.mittelalter-lexikon.de/wiki/Messerer ; https://www.historisches-lexikon-bayerns.de/Lexikon/N%C3%BCrnberg,_Reichsstadt:_Handwerk
- Salzman, L. F., *English Industries of the Middle Ages* (Tudeley, Leeds Castle, Bedburn): https://www.gutenberg.org/cache/epub/48588/pg48588-images.html
- Byrkeknott 1408 (Lapsley, EHR 14, 1899), Five Nine: https://www.fivenine.co.uk/local_history_notebook/South%20Bedburn/Byrkeknott/byrkeknott__english.html
- Crew, P., "The experimental production of prehistoric bar iron", HMS 1991: https://www.hmsjournal.org/index.php/home/article/view/553
- Markewitz/Sauder, "Charcoal to bar": https://warehamforgeblog.blogspot.com/2009/01/charcoal-to-bar-in-colonial-furnaces.html
- Wikipedia "Bloomery": https://en.wikipedia.org/wiki/Bloomery ; "Charcoal": https://en.wikipedia.org/wiki/Charcoal
- Vermont Archaeology, historia weglarstwa (wydajnosc 25% wagowo): https://www.vtarchaeology.org/wp-content/uploads/200_years_ch5_optimized.pdf
- Kolczuga - czasy rekonstrukcji: https://www.ironskin.com/faq-chainmail-weight-and-cost/ ; https://www.outfit4events.com/eur/articles/historical-armor/chainmail-armour/
- C14 Price List (Goucher): http://faculty.goucher.edu/eng211/c14_price_list.htm
- Clark, G., "The Condition of the Working-Class in England 1209-2004": https://faculty.econ.ucdavis.edu/faculty/gclark/papers/Working%20Class.pdf
- Bernard, M., "L'organisation du travail des armuriers parisiens", *Medievales* 69 (2015): https://journals.openedition.org/medievales/7579
- Pozostale liczby (Tower, ceny haubergeonow, lukow, strzal) - patrz `docs/HISTORIA-ZBROJE-STRATY.md`.
