# AUDYT: skala mapy, ruch, oblezenia, budowy, XP - pod "1 dzien gry = 1 dzien"

Data: 2026-10-04. Audyt TYLKO DO ODCZYTU - zadnych zmian w kodzie ani w MCM.
Zakres: skala mapy ROT i predkosc partii, oblezenia i budowy, XP i awanse, inne tempa
dzienne (leczenie, morale, jedzenie, rekrutacja, bandyci, spojnosc armii).
Kalendarz, starzenie, wydarzenia i ekonomie audytuje osobny agent - tu tylko tam,
gdzie dotykaja ruchu i czasu w polu.

Oznaczenia: **KANON** = tekst GRRM / wiki; **SZACUNEK** = fanowskie wyliczenia albo moje;
**KOD** = sprawdzone w dekompilacji; **LOG** = zmierzone w logach Jeffa.

---

## 0. Najwazniejsze wnioski (TL;DR)

1. **Skala mapy ROT: ~4.8 km na jednostke mapy** (Castle Black -> Sunspear = 1012 jedn. ~ 3000 mil).
   Skala jest spojna dla Westeros na duzych odleglosciach (Krolewska Przystan -> Sunspear wychodzi
   999 mil przy fanowskich ~990 milach drogi), ale lokalnie znieksztalcona (Mur ~1.5x za dlugi)
   i **osady sa scisniete**: wioska lezy srednio 32 jedn. (~150 km) od swojego miasta.
2. **Ruch teraz (WorldPace 75%, LOG):** armia 800 ludzi robi ~113 km/dzien, lord z 150 ludzmi
   ~170 km/dzien, czysta jazda ~250 km/dzien, statki ~390 km/dzien. To **5-8x szybciej niz
   historyczna armia** (15-25 km/dzien) i 2-3x szybciej niz statek zaglowy (100-180 km/dzien).
   Winterfell -> Krolewska Przystan: armia 800 ludzi ~22 dni (historycznie ~120, w kanonie ~1-2 mies.).
3. **Trzy rzeczy w kodzie ZABLOKUJA kazde dalsze zwolnienie swiata** i trzeba je naprawic PRZED
   zmiana suwaka:
   - `PartySpeedModel.MinimumSpeed = 1.0` (vanilla, KOD) - podloga predkosci. Juz przy 50% duze
     armie na nia wpadaja; przy 30% wpadaja na nia wszystkie armie i wiekszosc lordow.
   - kary terenu `TerrainEase` sa PLASKIE (las -0.1, brod -0.5, noc -0.5...) i NIE skaluja sie
     z `WorldPacePercent` (MarchPace skaluje, TerrainEase nie - KOD). Przy predkosci ~0.8 noc -0.5
     to -60%.
   - `Campaign.EstimatedAverageLordPartySpeed` = 3.36 (i 7 podobnych stalych, KOD) - AI liczy
     z nich "ile dni do celu", opoznienia teleportu bohaterow, zasiegi patroli, wioskowych,
     karawan. Armoury ich nie skaluje, wiec AI juz teraz mysli, ze jest 1.3x szybsze niz jest.
4. **Spojnosc armii (cohesion) rozwali dlugie marsze i oblezenia:** armia AI z 5 partii traci
   ~1.8/dzien we wlasnym kraju i ~5.3/dzien na wrogim terenie (vanilla x BK CohesionBoost 0.5
   + StrategicCampaignAI -3.5 po 5 dniach). Rozwiazuje sie przy 30 (poza oblezeniem) = po ~18 dniach
   na wrogiej ziemi. Przy marszu 50+ dni armia rozpadnie sie w drodze.
5. **Oblezenia - zagadka z 17.09 rozwiazana:** budowe machin mnoza CZTERY warstwy: vanilla
   x **BannerKings `LongerSieges` 0.5** (tego nikt nie liczyl!) x RealisticBannerlord
   `SiegeConstructionSpeedMultiplier` (0.4 w logu z 17.09, 1.0 w obecnym json) x RB 0.75 bez Tools
   x nasz `SiegePacePercent` 50. Stad 0.55%/h zamiast oczekiwanych 0.9. Przy RB 1.0 przygotowania
   obozu (300 ludzi) to ~6 dni - historycznie OK.
6. **XP: BannerKings juz mnozy koszt awansu x3** (`TroopUpgradeXp` = 3.0 w BannerKings.json).
   "Dwa razy wolniejszy awans" = **`TroopUpgradeXp` 3.0 -> 6.0** - zero kodu, tylko MCM.
   Dla bohaterow brak gotowego pokretla - potrzebny postfix na `CalculateLearningRate` (x0.5).
7. **Rekomendacja ruchu:** wariant **K ("kanoniczny")** = `WorldPacePercent` 75 -> **30**
   (po naprawie trzech blokad z pkt 3): armia ~45 km/dzien, lord ~70, jazda ~100, statek ~155
   (statki trafiaja w historie, lad ~2x historii - co pasuje do scisnietej mapy).
   Winterfell -> KP: lord ~36 dni, armia 800 ~54 dni. Wariant **H ("historyczny")** = 13%
   tylko z osobnym suwakiem morza i wylaczeniem wiesniakow/karawan - duze ryzyko dla ekonomii.

---

## 1. Skala mapy ROT

### 1.1 Wymiary i gestosc (KOD: `Modules\ROT-Map\ModuleData\settlements.xml`)

| Wielkosc | Wartosc |
|---|---|
| Osady w pliku | 1065 (227 fortyfikacji: miasta + zamki; 571 wiosek) |
| Zakres X | 12.4 - 1576.6 jedn. |
| Zakres Y | 2.3 - 1244.5 jedn. (polnoc = wieksze Y) |
| Srednia odleglosc wioska -> jej miasto/zamek | 32.1 jedn. (mediana 29, max 108) |
| Srednia odleglosc fortyfikacji do najblizszej innej | 47.4 jedn. (mediana 44.4) |

### 1.2 Odleglosci w linii prostej (jedn. mapy, liczone z posX/posY)

| Trasa | Jedn. | km przy 4.77 km/jedn. | Odniesienie |
|---|---|---|---|
| Castle Black -> Sunspear | 1012 | 4830 | **KANON**: Westeros ~1000 lig = ~3000 mil (~4830 km) od Muru do poludniowego wybrzeza Dorne |
| Winterfell -> Krolewska Przystan | 465 | 2218 | **SZACUNEK** (fani, z kalibracji Murem): ~1500 mil droga (~2400 km); "Krolewski Trakt ponad 2000 mil od Storm's End do Muru" (**KANON**) |
| Krolewska Przystan -> Sunspear | 337 | 1607 | **SZACUNEK**: ~990 mil droga (~1590 km) - zgodne 1:1 |
| Shadow Tower -> Eastwatch | 150 | 716 | **KANON**: caly Mur = 100 lig = 300 mil (~480 km). ROT: Mur ~1.5x za dlugi |
| Winterfell -> Castle Black | 256 | 1221 | - |
| Winterfell -> White Harbor | 100 | 477 | - |
| Winterfell -> Moat Cailin | 142 | 677 | - |
| Moat Cailin -> The Twins | 74 | 353 | - |
| The Twins -> Riverrun | 132 | 630 | - |
| KP -> Riverrun | 215 | 1026 | - |
| KP -> Casterly Rock | 351 | 1674 | - |
| KP -> Highgarden | 294 | 1402 | - |
| KP -> Oldtown | 426 | 2032 | - |
| KP -> Storm's End | 149 | 711 | - |
| KP -> Dragonstone | 120 | 572 | - |
| Riverrun -> Casterly Rock | 187 | 892 | - |
| Highgarden -> Oldtown | 135 | 644 | - |
| Storm's End -> Sunspear | 193 | 921 | - |
| Winterfell -> Pyke | 373 | 1779 | - |
| KP -> Pentos (morze) | 339 | 1617 | - |
| Pentos -> Braavos | 261 | 1245 | - |
| Pentos -> Volantis | 389 | 1856 | - |
| Volantis -> Meereen | 308 | 1469 | (Essos - brak twardych danych kanonu, SZACUNEK) |

**Wniosek:** skala **~4.8 km/jedn.** (2.96 mili/jedn.) - ta sama, ktora przyjal komentarz
w `WorldPace.cs` (4.75). Droga jest dluzsza od linii prostej o ~10% (Krolewski Trakt
~1500 mil vs 1378 w linii) - w rachunkach nizej **trasa = 1.1 x linia prosta** (SZACUNEK).

**Zastrzezenie, wazne dla ekonomii:** mapa jest scisnieta lokalnie. Prawdziwa wies targowa
lezala 10-25 km od miasta; w ROT wioska jest srednio ~150 km od swojego miasta, bo jedna wioska
"reprezentuje" caly okreg. Dlatego pelne zwolnienie do historycznych km/dzien uderzy
w wiesniakow i karawany nieproporcjonalnie (patrz ryzyka, rozdz. 7).

---

## 2. Predkosc partii - jak to liczy gra teraz

### 2.1 Jednostki czasu (KOD)

- `Campaign.TickMapTime`: `dt = 0.25 x realDt (x mnoznik przyspieszenia)`, czas mapy rosnie
  o `4320 x dt` sekund. **1 jednostka dt = 1.2 godziny gry.**
- `MobileParty.ComputeNextMoveDistance`: droga = `Speed x dt`.
- => **partia przechodzi `Speed / 1.2` jednostek mapy na godzine gry.**
  (AI w `AiMilitaryBehavior`, `Army`, `DefaultDelayedTeleportationModel` liczy `Speed x 24`
  na dobe - czyli juz w vanilli zawyza tempo o 20%.)
- Noc wg zegara gry: 22:00-02:00 (`SunRise 2`, `SunSet 22`). NightRest: AI obozuje 22-04
  (6 h), gracz potrzebuje 6 h snu. **Ruch efektywny ~18 h/dobe** => `jedn./dobe = 15 x Speed`.

### 2.2 Lancuch modeli predkosci (KOD)

| Warstwa | Co robi |
|---|---|
| vanilla `DefaultPartySpeedCalculatingModel` | baza `4 x (200/(200+ludzie))^0.4`; jazda +30% x udzial, piechota na koniach +15%, stado -0.3..-0.8, ranni -5%, ladunek, morale +-; teren procentowo (las -30%, brod -30%, pustynia/snieg -10%, noc -25%); **`MinimumSpeed = 1.0` (LimitMin)** |
| RealisticBannerlord `RealisticPartySpeedModel` | dziedziczy po vanilli, woła jej CalculateFinalSpeed (podloga 1.0 juz w srodku), dodaje pory roku (zima -20%, jesien -10%, wiosna +5%) i pogode (zamiec -30%, powodz -25%...); LimitMin(0.2) tylko gdy <0.2 - podloga 1.0 i tak wygrywa |
| ROT `ROTPartySpeedModel` | przelotka; +20% tylko dla partii, w ktorej gracz sluzy (enlistment) |
| BetterEconomy `BEE_PartySpeedModel` | tylko karawany: pory roku (procentowo) i plaski bonus za konna eskorte |
| NavalDLC `NavalDLCPartySpeedCalculationModel` | na morzu wlasna baza z predkosci statkow, otwarte morze +44.8%, rzeka +50%, wiatr, max 10 |
| BannerKings | `SlowerParties` (u Jeffa 0.0 - wylaczone 15.09), `PartySizes` 2.0 (wieksze partie = nizsza baza) |
| **Armoury `WorldPace`** | `AddFactor(p/100 - 1)` na BAZIE kazdego modelu (raz - SpeedDepth); u Jeffa **75%** (Armoury.json), wszystkie partie, takze statki, karawany, wiesniacy |
| **Armoury `TerrainEase`** | oddaje procent vanilli i dokłada PLASKIE kary: las 0.1, pustynia 0.2, snieg 0.2, bagno 0.3, brod 0.5, noc 0.5 - **nieskalowane przez WorldPace** |
| **Armoury `MarchPace`** | sufity kolumny (piechur 4.0, tabor 4.2, piechota na luzakach 5.0, jazda 6.5) **x WorldPace** |
| **Armoury `NightRest`** | dlug snu -25/-40/-90% predkosci; AI obozuje noca (15% kolumn idzie dalej) |

Uwaga: log startowy pisze `WorldPace: mapa 50%`, bo drukuje sie przed wczytaniem MCM;
dymek/audyt pokazuja faktyczne 75% (`World pace -0.71` przy bazie 2.84 = -25%). LOG.

### 2.3 Zmierzone i policzone predkosci (stan obecny, WorldPace 75%)

LOG (`Armoury-2026-10-0x`, "Audyt predkosci", partia gracza, mieszana konno-piesza, rowniny):
**2.65-2.68**, w lesie 2.32-2.35, po zarwanej nocy 2.04.

| Typ partii | Speed (KOD/LOG) | jedn./dobe (18 h) | km/dobe | Historycznie |
|---|---|---|---|---|
| Gracz (LOG) | 2.68 | 40 | 192 | - |
| Lord ~150 ludzi, glownie piechota | ~2.4 (baza 3.2 x 0.75) | 36 | 172 | 25-40 km (maly oddzial mieszany) |
| Armia ~800 ludzi | ~1.58 (2.10 x 0.75) | 24 | 113 | 15-25 km (marsz z dniami odpoczynku) |
| Armia ~2000 ludzi | ~1.15 (1.53 x 0.75) - tuz nad podloga 1.0 | 17 | 82 | 12-20 km |
| Czysta jazda ~60 | ~3.5 (4.68 x 0.75) | 53 | 251 | 50-65 km na dluzszy dystans; goniec ze zmiana koni 100-200 |
| Flota (morze, 24 h, SZACUNEK speed ~5.5) | ~4.1 | ~82 | ~390 | zagiel 100-180 km (3-5 wezlow calodobowo), galera 50-90 |

### 2.4 Ile trwa marsz (trasa = 1.1 x linia prosta)

| Trasa (jedn. trasy) | Lord 150 | Armia 800 | Armia 2000 | Jazda | Kanon / historia |
|---|---|---|---|---|---|
| Winterfell -> KP (512) | 14 d | 22 d | 30 d | 10 d | KANON: dwor Roberta ~1 mies.+ (fani: 30 mil/dzien = ~50 dni); historycznie armia ~120 d |
| Winterfell -> Castle Black (282) | 8 d | 12 d | 16 d | 5 d | historycznie armia ~65 d |
| KP -> Riverrun (237) | 7 d | 10 d | 14 d | 4.5 d | ~55 d |
| KP -> Casterly Rock (386) | 11 d | 16 d | 22 d | 7 d | ~90 d |
| KP -> Sunspear (371) | 10 d | 16 d | 22 d | 7 d | ~85 d |
| KP -> Pentos morzem (~370) | - | - | - | - | flota ~4.5 d; historycznie ~12-14 d |

### 2.5 Warianty docelowe (liniowo od obecnego 75%)

| | Obecnie (75%) | Etap posredni (50%) | **K - kanoniczny (30%)** | H - historyczny (13%) |
|---|---|---|---|---|
| Lord 150 km/dobe | 172 | 115 | **69** | 30 |
| Armia 800 km/dobe | 113 | 75 | **45** | 20 |
| Armia 2000 km/dobe | 82 | 55* | **33*** | 14* |
| Jazda km/dobe | 251 | 167 | **100** | 43 |
| Flota km/dobe | ~390 | ~260 | **~157** | ~68 (za wolno - potrzebny osobny suwak morza ~30%) |
| Winterfell -> KP, armia 800 | 22 d | 32 d | **54 d** | 125 d |
| Winterfell -> KP, lord | 14 d | 21 d | **36 d** | 82 d |
| Winterfell -> KP, jazda | 10 d | 15 d | **24 d** | 56 d |

`*` = wymaga obnizenia podlogi `MinimumSpeed` (bez tego armia stoi na 1.0 i jedzie szybciej
niz powinna; przy 30% podloga lapie armie 800 (0.63), lorda 150 (0.96), armie 2000 (0.46)).

**Propozycja: wariant K (`WorldPacePercent` = 30).** Uzasadnienie: statki wychodza
historycznie, lad ~2x historii, co rekompensuje scisniecie osad (wioska co ~150 km),
a marsz z Winterfell do KP trwa miesiac-dwa jak w ksiazkach. Wariant H tylko jako dalszy
krok z rozdzielonym suwakiem morza i wylaczeniem/lagodniejszym zwolnieniem wiesniakow
i karawan.

---

## 3. Oblezenia

### 3.1 Budowa machin (KOD + LOG)

Vanilla `DefaultSiegeEventModel.GetConstructionProgressPerHour`:
`postep/h = 1 / (ManDayCost / sqrt(zdrowi w obozie) x 24)` + inzynieria, perki.
Przygotowania obozu (`preparations`) = **24 osobodni** (LOG), `fire_catapult` 24 (LOG).
Dopoki przygotowania nie sa gotowe, zadna machina nie rusza (KOD, `SiegeEvent.ConstructionTick`).

Lancuch mnoznikow (KOD):

| Warstwa | Mnoznik | Zrodlo |
|---|---|---|
| vanilla, 318 zdrowych | 3.09%/h -> przygotowania 1.35 dnia | wzor |
| **BannerKings `LongerSieges`** | **x0.5** (postfix na `DefaultSiegeEventModel`, BannerKings.json `LongerSieges: 0.5`) | `VanillaModelTweakPatches.cs:1445` |
| RealisticBannerlord `SiegeConstructionSpeedMultiplier` | x0.4 (log 17.09 i 04.10: "RB mnoznik budowy z MCM = 0.40") / json obecnie 1.0 | RB |
| RB: oblegajacy bez Tools | x0.75 | RB |
| Armoury `SiegePacePercent` | x0.5 | WorldPace |

**Rozwiazanie zagadki z CHANGELOG 17.09** ("0.55%/h zamiast 0.9 - jest jeszcze jeden czynnik"):
to BK `LongerSieges` 0.5. 3.09 x 0.5 x 0.4 = 0.62%/h ~ zmierzone 0.55-0.57%/h.

| Stan | Przygotowania (318 ludzi) | Machina 24 osobodni po przygotowaniach |
|---|---|---|
| LOG (RB 0.4) | 0.57%/h -> po suwaku 0.29%/h = **14.6 dnia** | ~podobnie, 5 dni u obroncy |
| Jesli RB naprawde czyta 1.0 z json | ~1.43%/h -> ~0.71%/h = **~5.8 dnia** | ~6 dni |
| Historycznie | oboz, palisada, rowy: 1-2 tyg. | trebusz/wieza: 1-3 tyg. |

**Wniosek:** przy RB 1.0 obecne 50% jest historycznie w porzadku - nie ruszac, tylko
POTWIERDZIC w logu, co RB faktycznie czyta (linia `RB mnoznik budowy z MCM`). Jesli dalej
0.40 - albo podniesc nasz `SiegePacePercent` do ~100, albo zostawic (15 dni przygotowan to
gorna granica historyczna). Nie zmieniac `LongerSieges` i `SiegePacePercent` naraz.

### 3.2 Glodzenie twierdzy (KOD - SZACUNEK liczb, brak danych w logu)

- vanilla `DefaultSettlementFoodModel`: magazyn miasta max 300, zamek +150; zjada
  `zamoznosc/40 + garnizon/20` dziennie; w oblezeniu odcina dostawy z wiosek.
- BannerKings `BKFoodModel` (aktywny przez ROT): magazyn **500**, zamek **+250**; konsumpcja
  od populacji: szlachta 0.075, rzemieslnicy 0.04, dzierzawcy 0.03, chlopi 0.02 na osobe/dzien;
  w oblezeniu odpada produkcja pol (`if (!town.IsUnderSiege)`).
- SZACUNEK: miasto 10 000 mieszkancow zjada ~250-300/dzien => **magazyn znika w 2-3 dni**,
  zamek z kilkuset duszami ~10-30 dni. Historycznie: dobrze zaopatrzony zamek 6-12 miesiecy,
  miasto 1-6 miesiecy (Chateau Gaillard 1203-04: ~6 mies.; Rouen 1418-19: ~5.5 mies.).
- To jest **DO SPRAWDZENIA w logu** zanim cokolwiek zmienimy: brak dziennej linii
  "oblezona osada: zapas X, zmiana Y/dzien, garnizon Z, glod od N dni".

### 3.3 Ile trwa oblezenie AI (KOD)

Oblezenie AI konczy sie szturmem (gdy machiny gotowe i przewaga), poddaniem z glodu albo
rozpadem armii. **Rozpad armii** decyduje przy dlugich oblezeniach: w oblezeniu armia AI nie
rozwiazuje sie przy 30, ale rozprasza przy 10 (`CohesionThresholdForDispersion`). Patrz 5.1.

---

## 4. Budowy w osadach

| Element | Wartosc (KOD) |
|---|---|
| vanilla moc budowy miasta/dzien | `zamoznosc x 0.01` + lojalnosc, gubernator, budynki, "boost" ze skarbca (500 zlota -> +50/dzien miasto, 250 -> +20 zamek) |
| BannerKings `BKConstructionModel` | sila robocza z populacji: rzemieslnicy x0.015, niewolnicy x0.02, chlopi i dzierzawcy x0.015 (z puli "construction force") |
| Fortyfikacje miasta poz. 2 / 3 | 6000 / 12000 pkt |
| Fortyfikacje zamku poz. 2 / 3 | 1400 / 2800 pkt |
| Koszary miasta | 1800 / 3000 / 4200 |

SZACUNEK: miasto o mocy ~50 pkt/dzien stawia mury poz. 2 w ~120 dni, poz. 3 w ~240 dni;
zamek przy ~15-20 pkt/dzien mury poz. 2 w ~70-90 dni.
Historycznie: Conwy ~4-5 lat (1283-87), Harlech ~7 lat, Caernarfon w budowie ~45 lat,
Beaumaris nigdy nieukonczony; wieksza przebudowa murow miejskich - lata.

Przy roku 365 dni obecne tempo daje mury miasta w ~4-8 miesiecy. Docelowo 1-3 lata =>
**moc budowy x0.2-0.4**. Brak pokretla w MCM - potrzebny postfix `ConstructionPacePercent`
na `CalculateDailyConstructionPower` (wszystkie modele, licznik zagniezdzenia). Najpierw linia
w logu z faktyczna moca budowy kilku miast (brak takich danych - nie zgadujmy).

---

## 5. Inne tempa dzienne

Wazne: wszystko ponizej jest liczone **na dzien**. Przejscie na "1 dzien = 1 dzien" nie zmienia
ich wartosci dziennych, tylko proporcje do drogi (marsz trwa 2-3x dluzej) i do roku.

### 5.1 Spojnosc armii (KOD) - KRYTYCZNE przy wolniejszym swiecie

`DefaultArmyManagementCalculationModel.CalculateDailyCohesionChange`: -2 bazowo, dla armii AI
`-0.25 x liczba partii` i `-0.125 x (glodne+1)`, `(morale<=25 +1)`, `(<=10 zdrowych +1)`;
**BK `CohesionBoost` 0.5** mnozy calosc x0.5; **StrategicCampaignAI** po 5 dniach na wrogiej
ziemi dodaje **-3.5** (juz po BK, nieskalowane). Armia AI: poza oblezeniem rozwiazuje sie przy
<30, w oblezeniu rozprasza przy 10.

| Armia AI 5 partii | Zmiana/dzien | Dni do 30 | Dni do 10 (oblezenie) |
|---|---|---|---|
| wlasny kraj | ~-1.8 | ~39 | - |
| wrogi teren (po 5 dniach) | ~-5.3 | ~18 | ~22 |

Przy wariancie K armia 800 idzie z Winterfell do KP ~54 dni - **rozpadnie sie w polowie drogi**,
a oblezenie AI nie potrwa dluzej niz ~3 tygodnie. Potrzebny postfix
`ArmyCohesionPacePercent` (tylko ujemna czesc, na najbardziej zewnetrznym modelu, po SCA)
= ten sam stosunek co zwolnienie swiata (K: 40%), opcjonalnie dodatkowo x0.5 w oblezeniu.

`MaximumWaitTime` (zbiorka armii, godziny) - po jego uplywie armia rusza przy 75% sily;
przy wolniejszym swiecie partie dochodza pozniej - przeskalowac x1/zwolnienie albo zostawic
(armie beda ruszac niepelne - akceptowalne).

### 5.2 Jedzenie (KOD)

- vanilla: 1 jednostka jedzenia na 20 ludzi na dzien (`NumberOfMenOnMapToEatOneFood 20`).
- Armoury `FoodConsumptionCutPercent` 40 (domyslnie; w Armoury.json klucza nie ma - bierze
  domyslne), `WinterPartyFoodBonusPercent` 50 (u Jeffa 50), RB zima +25%, lato -10%, zamiec +40%.
- BK `SlowerParties` tez tnie jedzenie (`+20 x SlowerParties` ludzi na jednostke) - u Jeffa 0.
- Przy K armia 800: 800/20 x 0.6 = **24 jedn./dzien**; marsz 54 dni = ~1300 jedn. Nie do
  uniesienia - armia MUSI kupowac po drodze. Fortyfikacje sa co ~47 jedn. = co ~5 dni marszu
  armii przy K - zaopatrzenie jest mozliwe, ale rynki miast beda wysysane (`AiStarvingBuysAnyPrice`
  pomaga). Ciecie 40% zostawic; nie zwiekszac.

### 5.3 Leczenie (KOD)

vanilla: szeregowi **5 ludzi/dzien** bazowo (+medycyna, +10 w osadzie; RB wlasny model,
+50% w osadzie, zima -20%, bez chirurga -80%); bohaterowie 11 HP/dzien.
Armoury: gracz 50%, AI 100%. Przy 1 dzien = 1 dzien: 100 rannych bez medyka ~20 dni (AI),
~40 dni (gracz); bohater 100 HP ~9-18 dni. Historycznie rany 2-6 tygodni - **bez zmian**.

### 5.4 Morale (KOD)

`MobileParty.DailyTick`: zdarzenia (bitwy, glod) wygasaja **10% dziennie** (polowa po ~6.6 dnia).
Realistyczne - **bez zmian**. NightRest kary za brak snu sa procentowe - bez zmian.

### 5.5 Rekrutacja (KOD)

BK `GetDailyVolunteerProductionProbability` (0.5-0.8 na slot/dzien) x Armoury
`VolunteerRegenPercent` 25 => slot napelnia sie srednio co ~5-8 dni. Wartosc dzienna OK.
Przy roku 365 zamiast 168 rocznie przybedzie ~2.2x wiecej ochotnikow - to decyzja kalendarza
(drugi agent). Wolniejszy ruch sam ograniczy zaciag (lord odwiedza mniej wiosek dziennie).
**Na razie bez zmian; zmierzyc po zmianie predkosci.**

### 5.6 Bandyci, kryjowki, patrole (KOD)

ROT: max 20 kryjowek na frakcje, 6 band wokol kryjowki; BK `BanditPartiesLimit` 150.
Spawn dzienny/godzinny - liczony na dzien, ograniczony limitem. Wolniejsze bandy beda
krazyc blizej kryjowek - mniej nalotow na drogi daleko od kryjowek. Patrole (vanilla 1.4 + BK)
korzystaja z `EstimatedAverage...Speed` - patrz 7. **Bez zmian, obserwowac.**

---

## 6. XP i awanse

### 6.1 Zolnierze (KOD)

| Element | Wartosc |
|---|---|
| Koszt awansu vanilla (na stopien) | T0->1: 100, T1->2: 300, T2->3: 550, T3->4: 900, T4->5: 1300, T5->6: 1700, T6->7: 2100 |
| **BannerKings `TroopUpgradeXp`** | **3.0 u Jeffa** (domyslnie BK 2.0) - postfix x3 na `DefaultPartyTroopUpgradeModel.GetXpCostForUpgrade`; ROT i NavalDLC tylko przekazuja dalej |
| XP za trafienie (vanilla) | `0.4 x sila atakujacego x sila ofiary x obrazenia`, arena x0.0625, turniej x0.33, symulacja x0.9 |
| RBM | plaska stawka za cios, arena/turniej pelna stawka |
| Armoury FairXp | arena 20%, turniej 50%, bitwa: XP wg obrazen + drugi raz za zabicie (x0.25..x8) |
| BK trening AI (`BKPartyTrainningModel`) | partie lordow AI (nie gracza) dostaja **za darmo co dzien na czlowieka**: glowa rodu 15 + 3 x tier, inni 10 + 2 x tier |
| vanilla trening | perki (Leadership, Bow Trainer, Polearm Drills...) + koszary/pola treningowe w garnizonie |

Ile trwa awans AI bez bitew (sam trening BK, glowa rodu, x3 koszt): T1->T2 900 XP / 18 = ~50 dni;
do T6 lacznie 14 250 XP / ~24 dziennie = **~600 dni**. Przy x6: ~1200 dni (~3.3 roku przy 365 d).

**Propozycja "dwa razy wolniejszy awans": BK `TroopUpgradeXp` 3.0 -> 6.0.** Jedno pokretlo,
obejmuje wszystkie zrodla XP zolnierzy (bitwa, trening, perki, garnizon), zero kodu.
Zapisany XP w save zostaje - zolnierze "w polowie" po prostu dluzej czekaja.
Uwaga: wolniejszy swiat sam zmniejszy liczbe bitew na dzien (2-3x), wiec realne tempo awansu
gracza spadnie jeszcze bardziej - dlatego robic to OSOBNO i po zmianie predkosci zmierzyc.
Wariant lagodniejszy: 3.0 -> 4.5 (x1.5), jesli po zmianie predkosci bitew bedzie wyraznie mniej.

### 6.2 Bohaterowie (KOD)

| Element | Wartosc |
|---|---|
| Learning rate (BK `BKLearningModel`, jak vanilla) | `1.25 x (1 + 0.4 x atrybut + 1.0 x focus)`, ponad limitem nauki kara, min 0.05 |
| Poziomy | XP poziomu rosnie o 1000 na poziom; 1 punkt skupienia na poziom, 1 atrybut co 4 poziomy, start 5 focus / 15 atrybutow, max focus 5, max atrybut 10 |
| BK | cechy Aptitude (+0.6 na poziom cechy), Scholarship |
| Mody punktow | BetterAttributePoints / BetterAttributes maja konfiguracje, ale **nie sa wlaczone** (nie ma ich na liscie modulow CrashScribe) |

**Propozycja:** nowe ustawienie Armoury `HeroLearningPercent` = 50 - postfix na
`CalculateLearningRate` kazdego `CharacterDevelopmentModel` (BK, NavalDLC, vanilla), tylko na
najbardziej zewnetrznym poziomie (jak SpeedDepth). Umiejetnosci rosna 2x wolniej => poziomy,
punkty skupienia i atrybutow tez 2x wolniej - bez ruszania `FocusPointsPerLevel` i
`LevelsPerAttributePoint`. Uwaga: obejmie tez Kowalstwo z kuzni Armoury (`XpPerDayPerTier`)
i Charm (BK juz x0.5) - jesli Jeff chce kuznie bez zmian, wylaczyc Smithing z mnoznika.

---

## 7. Czego NIE skaluje dzis WorldPace (ukryte zaleznosci od predkosci)

| Miejsce (KOD) | Co sie stanie przy wolniejszym swiecie | Naprawa |
|---|---|---|
| `PartySpeedModel.MinimumSpeed` = 1.0 | podloga - duze armie przestaja zwalniac (juz przy 50%) | postfix na getterze `MinimumSpeed` wszystkich modeli: `1.0 x WorldPace/100` (albo stale 0.2) |
| `TerrainEase` plaskie kary | przy speed ~0.8 noc -0.5 = -60%, brod -0.5 = prawie stoja | mnozyc kary `x WorldPacePercent/100` (tak jak MarchPace robi z sufitami) |
| `Campaign.EstimatedAverageLordPartySpeed` 3.36, `...Caravan` 4.2, `...Villager` 3.43, `...Bandit` 3.41, `...Naval*`, `EstimatedMaximumLordPartySpeedExceptPlayer` 10 | AI liczy dni do celu, zasieg celow armii (`DefaultTargetScoreCalculatingModel`), opoznienie teleportu bohaterow (2 dni x speed x 24), zasiegi patroli, wioskowych, karawan, questow - wszystko jak przy pelnej predkosci | po wczytaniu/nowej grze (`OnSessionLaunched`) pomnozyc wszystkie przez `WorldPace/100` (settery sa publiczne) |
| `ArmyManagementCalculationModel` spojnosc | armie rozpadaja sie w marszu i w oblezeniu | `ArmyCohesionPacePercent` (5.1) |
| Wiesniacy i karawany | ten sam WorldPace co armie; przy wariancie H dostawy jedzenia do miast spadna ~6x | osobny suwak dla wiesniakow/karawan (np. pierwiastek ze zwolnienia) |
| Statki | ten sam WorldPace; przy K trafiaja w historie, przy H sa 2x za wolne | osobny `SeaPacePercent` |
| Questy z terminem (20-30 dni) | przy K dalekie zlecenia beda nie do wykonania | obserwowac; ewentualnie wydluzyc terminy (osobna sprawa) |

---

## 8. PLAN - co, o ile, w jakiej kolejnosci

Zasada z CLAUDE.md: **jedna zmiana naraz**, po kazdej test Jeffa i wpis w CHANGELOG.

| Krok | Zmiana | Typ | Wartosc | Co sprawdzic w logu |
|---|---|---|---|---|
| 0 | Diagnostyka bez mechaniki: raz dziennie (a) przebyte jedn./dzien i Speed dla kilku partii AI (lord, armia, karawana, wiesniak, flota), (b) oblezone osady: zapas jedzenia, zmiana/dzien, dni glodu, (c) moc budowy 3-5 miast, (d) spojnosc i zmiana/dzien kazdej armii AI | kod (log) | - | linie wystepuja, liczby z rozdz. 2-5 sie zgadzaja |
| 1 | Awans zolnierzy 2x wolniej | MCM BK | `TroopUpgradeXp` 3.0 -> **6.0** | dymek awansu w ekranie partii (np. T3->T4 = 5400) |
| 2 | Naprawa blokad ruchu: `MinimumSpeed x pace`, kary `TerrainEase x pace`, `Estimated*Speed x pace` | kod Armoury | przy 75% efekt maly (las -0.075, noc -0.375) | audyt predkosci: kary terenu skalowane; brak skoku zachowan AI |
| 3 | Spojnosc armii x zwolnienie | kod Armoury | `ArmyCohesionPacePercent` = 67 (dla kroku 4) | linia (d): armie nie rozpadaja sie szybciej niz przed zmiana w przeliczeniu na droge |
| 4 | Swiat wolniej - etap posredni | MCM Armoury | `WorldPacePercent` 75 -> **50** | (a) armia 800 ~16 jedn./dzien; czy AI dalej oblega, czy armie dochodza do celu, czy miasta nie glodzeja (dostawy wiesniakow) |
| 5 | Swiat wolniej - wariant K | MCM Armoury | `WorldPacePercent` 50 -> **30**, `ArmyCohesionPacePercent` 40 | jak wyzej; Winterfell -> KP armia ~50+ dni |
| 6 | Nauka bohaterow 2x wolniej | kod Armoury | `HeroLearningPercent` = 50 | dymek nauki umiejetnosci (learning rate x0.5) |
| 7 | Budowy wolniej | kod Armoury | `ConstructionPacePercent` 33 (mury miasta poz. 2 ~1 rok) - wartosc po danych z kroku 0c | linia (c) |
| 8 | Glodzenie twierdzy | kod Armoury | dopiero gdy krok 0b pokaze, ze miasta padaja z glodu w dni: racjonowanie populacji w oblezeniu (np. x0.3) albo wiekszy magazyn | linia (b): miasto 45-90 dni, zamek 90-180 |
| 9 | (opcja) Wariant H | kod + MCM | lad 13%, `SeaPacePercent` 30, wiesniacy/karawany ~40% | caly zestaw (a)-(d); ekonomia (drugi agent) |
| - | Oblezenia (machiny) | **bez zmian** | `SiegePacePercent` 50, BK `LongerSieges` 0.5 | tylko potwierdzic `RB mnoznik budowy z MCM` (0.40 czy 1.00) |
| - | Leczenie, morale, jedzenie, ochotnicy | **bez zmian** | 50/100%, 10%/dzien, -40%, 25% | zmierzyc po krokach 4-5 |

### Ryzyka

1. **Podloga predkosci (krok 2 przed 4!).** Bez niej zmiana suwaka do 50/30% nie zwolni duzych
   armii - tylko male partie i gracza. Swiat bylby niespojny: gracz wolniejszy od armii AI.
2. **AI zle oceniajace odleglosci.** Bez skalowania `Estimated*Speed` armie AI wybieraja cele
   2-3x za dalekie, maszeruja tygodniami, glodzeja i rozpadaja sie po drodze; bohaterowie
   "teleportujacy sie" z opoznieniem liczonym na stare tempo.
3. **Armie glodujace na dlugich marszach.** 54 dni marszu = ~1300 jedn. jedzenia dla 800 ludzi.
   AI musi kupowac po drodze; jesli rynki puste (BEE, wiesniacy wolniejsi) - glod, spojnosc -,
   rozpad. Obserwowac `IsStarving` w linii (d).
4. **Oblezenia, ktore nigdy sie nie koncza / koncza sie za szybko.** Za szybko: miasta BK
   (magazyn 500) glodne po 2-3 dniach - szturm albo kapitulacja zanim przyjdzie odsiecz
   (przy K odsiecz idzie 2-3x dluzej!). Za wolno: przy ciagle ~15 dniach przygotowan (RB 0.4)
   i armii, ktora po ~3 tygodniach sie rozprasza - AI zwija oblezenie przed szturmem.
   Krok 3 (spojnosc) i krok 8 (zapasy) musza isc w parze z krokiem 5.
5. **Ekonomia przez wiesniakow i karawany.** Wiesniacy dowoza jedzenie i surowce srednio
   z ~150 km; przy K jada 2.5x dluzej => 2.5x mniej dostaw na dzien. Mozliwy glod miast
   i skoki cen (nasz nowy rynek uzbrojenia: warsztaty kupuja surowce z targu). Jesli linia
   rynku pokaze spadek zapasow - osobny suwak dla wiesniakow/karawan.
6. **Plaskie bonusy innych modow.** BEE dodaje plaski bonus za konna eskorte karawan; przy
   niskich predkosciach staje sie on relatywnie duzy (karawany z eskorta szybsze od armii).
7. **Zarwane noce.** NightRest -25/-40/-90% predkosci - procentowe, OK; ale przy 30% swiata
   gracz, ktory nie spi, praktycznie stoi - to zamierzone, ale odczuwalne.
8. **Postep w zapisanej grze.** Wszystkie zmiany dzialaja na biezaco; trwajace budowy machin
   i budynkow licza dalej od zapisanego postepu, tylko w nowym tempie. `WeeksPerSeason`
   (kalendarz) to inna sprawa - nie ruszac w trakcie kampanii.
9. **Podwojne liczenie.** Kazdy nowy postfix (MinimumSpeed, spojnosc, nauka, budowa) musi
   miec licznik zagniezdzenia jak `SpeedDepth`, bo modele sa lancuchem (ROT -> RB -> vanilla,
   SCA -> vanilla+BK, Naval -> BK -> vanilla). Inaczej x0.5 wejdzie 2-3 razy (patrz 02.09 i 17.09).

---

## Zrodla

- settlements.xml ROT-Map (pozycje osad), dekompilacja TaleWorlds.CampaignSystem 1.4.8,
  BannerKings, RealisticBannerlord, ROT.dll, BetterEconomy, NavalDLC, StrategicCampaignAI145
  (scratchpad z dekompilacja), Armoury src, Armoury.json, BannerKings.json,
  RealisticBannerlord_v2.json, Armoury-2026-10-0x.log, CrashScribe session-2026-10-04.
- Wall - A Wiki of Ice and Fire: https://awoiaf.westeros.org/index.php/Wall (100 lig = 300 mil)
- Westeros - A Wiki of Ice and Fire: https://awoiaf.westeros.org/index.php/Westeros
- Atlas of Ice and Fire, "It's smaller on TV: distances on Game of Thrones":
  https://atlasoficeandfireblog.wordpress.com/2016/03/15/its-smaller-on-tv-distances-on-game-of-thrones/
- Distances in Westeros: https://mapofwesteros.com/distances/ (Winterfell-KP ~1500 mil, KP-Sunspear ~990 mil - SZACUNEK fanow)
- Screen Rant, Winterfell -> King's Landing travel time:
  https://screenrant.com/game-thrones-winterfell-kings-landing-distance-travel-time/
- Historyczne tempa marszu/zeglugi i daty budow zamkow - wiedza ogolna (SZACUNEK, zakresy).
