# Etap 8 - rozpoznanie D: 8.17 OPTYMALIZACJA ("gra ma dzialac szybciej")

Data: 12.10.2026. Tylko rozpoznanie: kodu nie zmienialem, gry nie uruchamialem. Kod: drzewo `zb/58` (galaz w-toku/zbior-5-8, b461cc5).
Dane: autotest 120 dob nowej kampanii (WGRANIE 33, `Armoury-2026-10-12_02-44-09.log`, 119 pelnych dob), dlugi bieg 728 dob
(`scratchpad/kopia-728/Armoury-2026-10-10_20-58-02.log`), CrashScribe `session-2026-10-12_02-44-08.log`, STAN-PRAC "08.10 POPOLUDNIE"
(pomiar klatki autotestem -Profile). Skrypty analizy: `scratchpad/e8d/*.py` (koszt.py, czasy.py, seria.py, partie.py).

## 0. Najwazniejsze w 10 zdaniach

1. Doba autotestu: mediana **17.6 s** (srednio 17.5 s, p90 19.7 s); pierwsze 20 dob **13.9 s**, ostatnie 20 **19.7 s**. Dlugi bieg: mediana 21.5 s, maks. 38.8 s.
2. Doba rosnie razem z wielkoscia swiata, nie z naszymi petlami: korelacja czasu doby z liczba ludzi w sluzbie r = 0.89 (164 tys. -> 276 tys.),
   z kosztem musztry 0.91, z partiami lordow 0.69; z "nasze 171-174b" r = -0.12 (nasze stoi na ok. 1.2 s). Partii na mapie 2319 -> 5201.
3. "Nasze 7%" (linia `Koszt 171-174`) to tylko podzbior: 1.24 s z 17.5 s. Razem z cena sprzetu, wywozem, musztra i liniami ksiag nasze mody
   to **co najmniej 2.5-3.1 s/dobe = ok. 15-18% doby**; czesc nadal niemierzona (MenPurse.OnEntered, AiWear, Stables, ok. 120 wywolan Daily()).
4. Najdrozsze mierzone: WorkshopLaw.CyclePrefix 0.81 s, ceny sprzetu (PricePostfix) 0.62 s (dlugi bieg 1.0 s, dzien rekordowy 7.6 s),
   wywoz kupcow (PodazPopyt) 0.30 s w JEDNEJ klatce, musztra 0.26 s (dlugi bieg 0.48-0.57 s), AiGear 0.21 s, VolunteerKit 0.13 s, GarrisonArmory 0.10 s (szczyty 2.8 s).
5. Znalezione przyczyny w kodzie (pewne): pamiec polki (ShelfIndex) przebudowuje cala polke **13.8 tys. razy na dobe** - m.in. dlatego, ze kazdy zakup
   sztuka po sztuce (ShelfBuy.Take: cena -> AddToCounts(-1) -> cena) uniewaznia indeks; WorkshopLaw trzyma slowniki z kluczem `KeyValuePair<Workshop,string>`
   (domyslne porownanie struktury = refleksja i boxing przy kazdym odczycie) i alokuje `LineKey` 3 razy na cykl; wywoz kupcow liczy popyt, potege i
   mnoznik ceny dla WSZYSTKICH 227 osad przy kazdej transakcji, choc zasieg to 250 jednostek.
6. Globalny `ArmouryBehavior.OnDailyTick` wola ok. 120 metod `Daily()` w jednej klatce (wywoz 0.3-0.5 s, maks. 1.6 s + GarrisonArmory 0.1-2.8 s + linie ksiag
   ok. 0.3 s) - to jest "przyciecie raz na dobe", ktore gracz czuje bardziej niz srednia.
7. Linie "tylko log" (ksiegi i pomiary) kosztuja zmierzone ok. 250-300 ms/dobe; sam zapis logu (File.AppendAllText na linie) ok. 40 ms/dobe (pomiar na tym komputerze).
8. Gra i cudze mody to ok. 80-85% doby; z naszych logow nie da sie ich rozbic. Ostatni pomiar per mod (08.10, -Profile, po roku): klatka 176 ms -
   gra 55, silnik 46, Armoury 36, ROT 13, BK 10.5, StrategicCampaignAI 6; w grze AiMilitaryBehavior x44 wobec startu.
9. Realny zysk paczek O1-O3+O6 (niskie ryzyko, te same liczby): **ok. 0.4-1.2 s/dobe (2-7%)**; z O4-O5 do ok. 0.8-1.8 s (5-10%). Sufit z naszej strony
   to ok. 15-18% - wiecej da tylko mniej partii na swiecie (decyzja rozgrywki, poza 8.17) albo cudzy kod (wysokie ryzyko).
10. Proponuje najpierw **E8/0 - profil doby w logu** (koszt ponizej 1 ms/dobe), bo dla CyclePrefix i musztry nie wiadomo, ktora czesc kosztuje.

## 1. Co mierzy dzisiejszy log (i pulapka)

- `Koszt 171-174 (doba)` (Cost174.cs): zegar miedzy taktami doby + czas petli 171-174b. Pomiar probkowany: Stopwatch przy co 16. wywolaniu, czas x (wywolan/probek).
  Do sumy "nasze 171-174b" ida tylko pozycje rozlaczne (BuySubstitutes, zamowienia zamkow, VolunteerKit, CyclePrefix, TownFletchers, CaravanAmmo,
  MaterialOrders, GarrisonArmory). Ceny (PricePostfix), AiGear, BuyLoop, TryStart, rezerwa kramu - "poza suma" (zagniezdzone albo starsze).
- **Pulapka:** probka 1/16 mnozy rzadka dluga pauze (sprzatanie pamieci gen2, zrzut na dysk) x16. Dni z "ceny sprzetu 5.7-7.6 s" (dlugi bieg: 81-90 tys.
  wywolan, 70-93 us na wywolanie wobec typowych 14-18 us) moga byc czesciowo artefaktem. Ale doba tego dnia tez byla dluga (38.8 s wobec 21.5 s) - cos
  realnego sie dzialo. E8/0 musi zapisywac najdluzsza pojedyncza probke i liczniki GC, zeby to rozroznic.
- Inne linie z czasem: `PodazPopyt: ... czas wywozu`, `Musztra AI ... koszt`, `Ceny hurtu ... czas petli`, `Obieg ... koszt [P]`, `Uzbrojenie wedlug
  zrodla ... koszt pomiaru E6/0`, `Kiesa ludu`, `Towary (bilans) ... ramki`, `Markietani ... koszt`, `Pochodzenie ... czas`, `Zatkane wsie ... koszt` i ok. 15 drobnych.
- Nie mierzy nikt: MenPurse.OnEntered (sprzedaz nadwyzek AI i AiWear.MendInTown przy ok. 9600 wejsc do miast/dobe - 08.10 bylo to 17 ms/klatke po roku),
  Stables, NightRest/NightMarch poza stoperami, ArmouryBehavior.OnDailyTick jako calosc, sluchacze godzinowe, CrashScribe, RealisticCaptivity.

## 2. Profil doby z logow

### 2.1 Calosc i wzrost

| | autotest 120 dob | dlugi bieg 728 dob |
|---|---|---|
| doba (zegar) mediana / srednia | 17 600 / 17 463 ms | 21 492 / 21 578 ms |
| pierwsze 20 dob / ostatnie 20 | 13 908 / 19 726 ms | 13 516 / 21 814 ms |
| p90 / maks. | 19 726 / 22 859 ms | 25 250 / 38 815 ms |
| "nasze 171-174b" mediana | 1 198 ms (6.8%) | 1 317 ms (6.1%) |

Co rosnie razem z doba (autotest, wspolczynnik korelacji z czasem doby): ludzie w sluzbie (Pochodzenie, SLUZBA 163 640 -> 276 011) **0.89**,
koszt musztry 0.91, korpus zbroi w swiecie 0.90, ludzie w partiach lordow AI (35 tys. -> 87 tys.) 0.79, partie lordow AI (353 -> 613) 0.69,
towar bez odbiorcy na polkach (1.3 tys. -> 13.9 tys.) 0.62; "nasze 171-174b" **-0.12**, WorkshopLaw 0.13. Partii na mapie (linia `Partie (E4/0)`):
2319 (doba 1) -> 4418 (doba 11) -> 5201 (doba 120). Nowe partie w 120 dob: bandy 8063, "inne" 3253, konwoje BK 3031, karawany 1408, partie rodow 1375,
milicje i zalogi 1339, tabory wsi 1218. Wniosek: czas doby to przede wszystkim liczba partii i ludzi, ktora gra (AI, tik partii, bitwy) obsluguje.
(Dla porownania: test roczny 08.10 mial na starcie 11.7 s/dobe, po roku ok. 24 s - warunki testu inne, liczby orientacyjne.)

### 2.2 Nasze pozycje mierzone (ms na dobe, srednio)

| pozycja | 120 dob | 728 dob | wywolan/dobe | us/wyw. | gdzie biegnie |
|---|---|---|---|---|---|
| WorkshopLaw.CyclePrefix (w tym TryStart 88 ms) | **806** (maks. 1536) | 823 | 5 926 | 136 | DailyTickTown - rozlozone po dobie |
| ceny sprzetu SupplyDemand.PricePostfix (poza suma) | **617** (maks. 1464) | 1 015 (maks. 7 553) | 44 870 | 13.8 | kazda wycena sprzetu w miescie/zamku |
| PodazPopyt - wywoz kupcow (DailyTradeCore) | **301** (maks. 608) | 489 (maks. 1 621) | 1 | - | globalny OnDailyTick - **jedna klatka** |
| Musztra AI "koszt" (Drill) | **264** (76 -> 355) | 476 (doby 200-400: 574, maks. 656) | - | - | godzina + DailyTickParty |
| AiGear.TryBuyCore (w tym BuyLoop 123, BuySubstitutes 20) | 209 | 249 | 332 | 629 | wejscie lorda do miasta |
| VolunteerKit.BuyCore | 130 | 206 | 1 139 | 114 | ochotnicy notabli |
| GarrisonArmory (SellWeek) | 103 (maks. 748) | 183 (maks. **2 841**) | 1 | - | globalny OnDailyTick - jedna klatka |
| rezerwa kramu (zagniezdzone) | 100 | 176 | 9 712 | 10 | w zakupach |
| MaterialOrders.Order | 85 | 55 | 505 | 169 | |
| Ceny hurtu - czas petli (ShelfBuy) | 72 | - | 2 632 wycen po zdjeciu | 16/szt. | w zakupach |
| TownFletchers.Work | 67 | 36 | 97 | 693 | DailyTickTown |
| Obieg (przeliczenie 55, okna 24, probki swiata 11, raport 6) | ok. 96 | - | | | tylko log |
| Marsz - konie partii (SpeedPostfix 31, stan doby 10, razem 11) | ok. 52 | - | | | mechanika |
| Kiesa ludu (latki 42, doba 7) | 49 | - | | | mechanika |
| E6/0 koszt pomiaru (Uzbrojenie wedlug zrodla) | 46 | - | | | tylko log |
| Towary (bilans) ramki 40 + zapas 5 | 46 | - | | | tylko log |
| Pochodzenie (czas 24 + uzgodnienie 21) | 45 | - | | | ksiega + log |
| Markietani | 38 | - | | | mechanika |
| Zatkane wsie (diagnoza) | 24 | - | | | tylko log |
| Pomiary 169c razem | 24 | - | | | tylko log |
| Spichlerze 17, Wojsko z ludzi 16, Pokrycie zbrojowni 12, Targ przy zamku 11, Przegrani 10, Hearth 10, reszta < 6 | ok. 85 | | | | |

Suma (z odjeciem zagniezdzen ceny w zakupach): **ok. 2.5-3.1 s/dobe = 15-18% doby autotestu**. Nasze tez rosna ze swiatem (musztra x4.6 w 120 dob,
ceny i GarrisonArmory w dlugim biegu wyzej).

### 2.3 Skoki doby (jedna klatka)

Gra rozklada tik dobowy partii i osad po calej dobie (`CampaignPeriodicEventManager`, `PeriodicTicker` - sprawdzone w dekompilacji), wiec
CyclePrefix, AiGear, VolunteerKit, ceny nie robia skokow. **Globalne** `CampaignEvents.DailyTickEvent` -> `ArmouryBehavior.OnDailyTick` wola ok. 120
metod `X.Daily()` (ArmsPricing, SupplyDemand.DailyTrade, GarrisonArmory, ksiegi, linie...) w jednej klatce; podobnie ok. 16 sluchaczy `HourlyTickEvent`.
Szacunek skoku: wywoz 0.3-0.5 s (maks. 1.6 s) + GarrisonArmory 0.1-0.2 s (50 dni w dlugim biegu > 0.5 s, maks. 2.8 s) + linie ksiag ok. 0.3 s = **ok. 0.8-1.5 s zamrozenia raz
na dobe** (08.10: "1.5 s naraz", x10 wobec startu). Dokladnej liczby nie ma - E8/0.

### 2.4 Polki puchna

"szt. bez odbiorcy w zasiegu 250" (PodazPopyt): 1 264 (doba 1) -> 13 909 (doba 120); dlugi bieg do ok. 50-56 tys. Kazda petla po polce (przebudowa
ShelfIndex, spis DailyTradeCore, Stock bez indeksu, AiGear OnShelf, sprzedaz nadwyzek) rosnie z polka. To sprawa ekonomii (cennik/wywoz - 8.5),
ale ma skutek wydajnosciowy - warto to zapisac przy 8.5.

## 3. Najdrozsze miejsca w kodzie (sprawdzone w zb/58)

**K1. ShelfIndex - 13.8 tys. przebudow na dobe (srednio; odczytow z pamieci 142 tys.).** `ShelfIndex.Get` przebudowuje indeks calej polki przy kazdej
zmianie `VersionNo`/`Count` (petla po ok. 300 stosach + 2 tablice int na przebudowe). Zakupy sztuka po sztuce (`ShelfBuy.Take`, 9 miejsc: AiGear x2,
SupplyDemand wywoz, Stables x2, HorseFate, PackMarket, BuildFunding, BkSupplyMarket) robia: cena (`TownMarketData.GetPrice` -> nasz PricePostfix ->
`Factor` -> `ShelfIndex.Count`) -> `AddToCounts(-1)` -> cena nastepnej = przebudowa. Tylko wyceny po zdjeciu: hurt 2 632 + wywoz 2 932 = ok. 5.5 tys./dobe
(ok. 40% przebudow); reszta to zmiany polek przez gre i nasze inne `AddToCounts` (warsztaty). Koszt jednej przebudowy nieznany (szacunek 10-20 us ->
140-280 ms/dobe razem). Poprawka bez zmiany wyniku: po WLASNYM `AddToCounts` zaktualizowac indeks o -n/+n i nowe (Ver, Count), jesli przed zmiana byl
aktualny (wersja przed == indeks); samokontrola (co 64./4096. odczyt) zostaje.

**K2. WorkshopLaw.CyclePrefix 0.81 s (136 us/cykl).** Sprawdzone:
- `_wip` i `_rank`: `Dictionary<KeyValuePair<Workshop,string>, ...>` z domyslnym porownaniem. `KeyValuePair` nie nadpisuje Equals/GetHashCode, wiec .NET 4.7.2
  liczy hash z pierwszego pola (wszystkie linie tego samego warsztatu w jednym kubelku) i porownuje pola refleksja z boxingiem - kilka us na odczyt,
  2+ odczyty na cykl (CyclePrefix, Candidates) i przy budowie planu. Jedyne takie klucze w calym repo (grep).
- `LineKey(production)` buduje `List<string>` + `string.Join` przy KAZDYM wywolaniu: 3 razy na cykl (CyclePrefix, LineShare, Candidates) i w BuildPlan.
- `Candidates` (raz na warsztat-linie-dobe, do 40 sztuk - `WorkshopCandidates` 40, brak w Armoury.json Jeffa): dla kazdej sztuki `Needs` (alokacja
  float[4], Math.Pow, odczyty HistoricalPrices), `DayWage`, `Revenue` = `SupplyDemand.Factor` (Demand + Substitution + Stock + Math.Pow) + `ArmsPricing.Multiplier`.
  Factor zalezy tylko od koszyka (typ, tier) i polki - mozna liczyc raz na koszyk; `Needs` i `DayWage` sa stale dla sztuki w sesji (zalezne od ustawien).
  `Forbidden(it)` robi `ToLowerInvariant()` na kazdej sztuce.
- Ktora czesc dominuje (pierwszy cykl doby warsztatu z BuildPlan/Candidates czy kolejne cykle z TryStart), log nie mowi - podzial w E8/0.

**K3. Ceny sprzetu 0.62-1.0 s (45-57 tys. wywolan, 14-18 us).** W kazdym wywolaniu: `Prices()`, `Factor` (2 x ShelfIndex.Count, ConditionalWeakTable
klucza zamowien, Math.Pow), `ArmsPricing.Multiplier` (BaseOf, IsUnique, MarketOf, _idx, CostOf - 4-5 slownikow), Settings.Current kilka razy. Zysk
glownie z K1 (przebudowy siedza w tych wywolaniach). Kto wola ceny tak czesto (karawany gry, nasze petle, DTE) - nieznane; E8/0 policzy wedlug wolajacego.

**K4. Wywoz kupcow (SupplyDemand.DailyTradeCore) 0.3-0.5 s (maks. 1.6 s) w jednej klatce.** Petla: koszyki (ok. 90) x zrodla z nadwyzka x transakcje x **wszystkie
osady** (227 miast i zamkow) z `Demand`, `Math.Pow`, `ArmsPricing.Multiplier` i odlegloscia w kazdym obrocie, potem `ShelfBuy.Take` sztuka po sztuce (K1).
Pozycje osad sie nie zmieniaja - lista celow w zasiegu 250 dla kazdego zrodla raz na sesje (w tej samej kolejnosci co `places`, wiec remisy identyczne);
`Demand(dst)` i `Multiplier(dst, probe)` raz na koszyk. Szacunek 5-10x mniej pracy w tej petli.

**K5. Musztra (Drill) 0.26 s (120 dob) -> 0.48-0.57 s w dlugim biegu.** Jedna sekcja "koszt" obejmuje godziny ruchu (`Hourly` po `AllLordParties`), latke
`MobilePartyTrainingBehavior.OnDailyTickParty` (Tick/Postfix - petle po rosterze), zuzycie, kowali, ekrany zapasu i linie dnia. Rosnie szybciej niz liczba
partii (na partie 0.2 -> 0.58 ms) - petle po ludziach/sprzecie. Znana zapowiedz z 09.10 (STAN-PRAC "DECYZJE JEFFA 08.10"): jeden odczyt pozycji lordow na
godzine dla Drill, NightMarch.AiSleepLedger i WorldMeasure (dzis 3 osobne petle + SnapshotPositions). Najpierw podzial pomiaru.

**K6. Zakupy AI.** AiGear.TryBuyCore 0.6-0.8 ms na wejscie lorda (BuyLoop 0.37-0.47 ms - ceny sztuka po sztuce, K1); `MethodInfo.Invoke` do DTE
(`_add`, `_blTest`, `object[]` przy kazdej sztuce) - zamiana na delegaty (AccessTools.MethodDelegate) to drobny, bezpieczny zysk. VolunteerKit 114-210 us.

**K7. GarrisonArmory.SellWeek - 32 zalogi/dobe (Id % 7), 0.1-0.18 s, szczyty 2.8 s** w globalnym takcie doby (MenPurse.SellGarrisonSurplus - dopasowanie).
Rozlozenie po godzinach (Id % (7 x 24)) usuwa skok bez zmiany sumy, ale przenosi sprzedaz z "Armoury tick dobowy" do "poza tickiem" w ksiedze obiegu
(linie sprawdz_logi) - srednie ryzyko.

**K8. Linie "tylko log".** Zmierzone ok. 250-300 ms/dobe: Obieg ok. 96, E6/0 46, Towary ramki 46, Zatkane wsie 24, Pomiary 169c 24, Pochodzenie -
uzgodnienie 21, Pokrycie zbrojowni AI 12 (+ linie bez pomiaru). Zapis: `Log.Info` = `File.AppendAllText` (otwarcie i zamkniecie pliku) na kazda linie;
ok. 300 linii/dobe (36 104 linie / 120 dob, 32 MB). Pomiar na tym komputerze: 300 x 1 KB na pliku 30 MB = 37 ms (0.125 ms/linia), StreamWriter 3 ms.
Uwaga: logi to glowne narzedzie (CLAUDE.md rozdz. 5) - wylaczenie ksiag w grze Jeffa to jego decyzja.

**K9. Sprawdzone - tanie, bez zmian:** slowniki z kluczem ItemObject/Settlement/Hero (MBObjectBase nadpisuje GetHashCode przez Id, Equals referencyjny -
masowa zmiana na RefEq nic nie da; Workshop.GetHashCode liczy hash napisu _tag - drobiazg); SleepGate (prefiksy SetMove* z wczesnym wyjsciem, gdy nikt nie
spi); CrossingLaw.OnTick (co 0.5 s, tylko gracz); HorseState.RosterPostfix (probkowany, kilka porownan); ManLedger (tylko roster gracza);
McmSettings.Apply co godzine (zwykle przypisania); SellByCondition.Seen; Equipmentish (switch). Refleksja w goracych sciezkach - glownie zapamietane
FieldInfo/MethodInfo; jedyne Invoke w petlach: AiGear (DTE), MaterialOrders (BK/statki - rzadko).
**CrashScribe:** `FirstChanceException` z `Blame.Culprits` (przejscie stosu) dla kazdego wyjatku w procesie; zgloszen w kampanii prawie zero, ale ile
odfiltrowanych - nieznane (dodac licznik w E8/0; spodziewam sie malo).

## 4. Gra i cudze mody

- Z naszych logow nie da sie rozbic czasu gry, ROT, BK, DTE, SCAI. Jedyne dane per mod: 08.10 autotest `-Profile` (sekcje silnika + sluchacze zdarzen;
  repo autotestu w scratch sesji 3cf3e0ac `dzien-6\autotest\repo`, galaz at1-autotest c5c25ce, `CrashScribe-spis.dll`): po roku gra 55 ms/klatke,
  silnik 46, Armoury 36 (x11 wobec startu), ROT 13, BK 10.5, StrategicCampaignAI 6; w grze AiMilitaryBehavior x44, werbunek x7; ROT malzenstwa NPC x2.4;
  druzyn 2071 -> 6213 (majatki BK 1390 od doby 4, karawany x3, bandy x1.8).
- Najwieksza dzwignia czasu doby to liczba partii i ludzi (2.1), czyli rozgrywka/ekonomia (bandy, konwoje BK, majatki, karawany) - **poza 8.17**;
  jesli Jeff chce, osobna decyzja (np. limit band w regionie, laczenie konwojow BK) z wlasnym testem ekonomii.
- `BKROTPatch.DynamicPartySizePerformancePatch` nie wpina sie (TargetMethod null - brak moda DynamicPartySize; znane w ERRORS.md) - nic do odzyskania.
- Przyspieszanie cudzego kodu (AI lordow, ROT) tylko po pomiarze -Profile i z dekompilacja; wysokie ryzyko regresji AI - nie proponuje do etapu 8 bez dowodu.

## 5. E8/0 - profil doby w logu (proponowany pomiar, tylko log)

Klucz `Stage8ProfileLog` (domyslnie TAK - koszt < 1 ms/dobe; hint MCM po angielsku: "Writes one daily line with the time each part of the mod
takes (diagnostics only)."). Linia raz na dobe `Profil doby (E8/0): ...`:
1. **Takt doby:** czas calego `ArmouryBehavior.OnDailyTick` (jedna klatka) + top 15 z ok. 120 wywolan `X.Daily()` (Stopwatch przy kazdym - 120 odczytow/dobe).
2. **Godzina:** suma i maks. kazdego z ok. 16 sluchaczy `HourlyTickEvent` (24 x 16 odczytow).
3. **Zdarzenia:** SettlementEntered (MenPurse.OnEntered z rozbiciem SellArmorySurplus / AiWear.MendInTown / AiGear.TryBuy, CaravanBulk, CaravanAmmo,
   MaterialOrders, Drill, Stables, QohorMasters), MobilePartyDestroyed (ok. 15 sluchaczy), MapEventEnded (ok. 10) - probka 1/16 jak Cost174, liczba zdarzen pelna.
4. **Podzialy:** CyclePrefix - pierwszy cykl doby warsztatu (BuildPlan/Candidates) vs kolejne; Drill - godziny ruchu / tik treningu / zuzycie / kowale / linie;
   PricePostfix wedlug wolajacego (flaga [ThreadStatic] ustawiana w naszych petlach: AiGear, wywoz, VolunteerKit, warsztaty, reszta = gra/cudze);
   ShelfIndex - przebudowy wedlug zrodla zmiany (wlasne Take / reszta); Log.Info - laczny czas zapisu.
5. **Pamiec:** `GC.CollectionCount(0/1/2)` przyrost na dobe, `GC.GetTotalMemory(false)`; najdluzsza pojedyncza probka w kazdej pozycji probkowanej (odroznia pauze
   sprzatania x16 od kosztu).
6. CrashScribe: liczba FirstChance na dobe (zgloszone / odfiltrowane) i czas `Blame.Culprits`.
7. Autotest: bieg 120 dob nowej kampanii (porownanie z ta baza: 17.5 s/dobe) + `-Profile` na scalonym DLL (per mod, per klatka), jesli narzedzie z 08.10
   jeszcze dziala na 1.4.8.

## 6. Paczki (ranking: zysk ms/dobe x ryzyko)

Kazda paczka z wylacznikiem MCM (wylaczona = kod jak dzis), jedna naraz, "te same liczby": w probce 1/64 liczyc stara i nowa droga i pisac licznik
rozjazdow w linii (wzor ShelfIndex). Test: autotest 120 dob - czas doby i bloki ekonomii sprawdz_logi bez zmian.

| # | paczka | zysk (szacunek) | ryzyko | uwagi |
|---|---|---|---|---|
| E8/0 | profil doby (rozdz. 5) | 0 (pomiar) | bardzo niskie | najpierw; decyduje o O2/O4 |
| O1 | ShelfIndex po wlasnej zmianie polki (ShelfBuy.Take/PutBack, warsztaty); (b) opcjonalnie wszystkie zmiany przez `ItemRoster.OnRosterUpdated` (juz latany przez HorseState) | (a) 60-110 ms, (b) do 150-280 ms | (a) niskie, (b) srednie | wynik identyczny; zostaje samokontrola |
| O2 | WorkshopLaw: klucz struktury z IEquatable albo slownik w slowniku (format zapisu arm_wip bez zmian), pamiec LineKey (Production -> napis), Factor raz na koszyk w Candidates, pamiec Needs/DayWage na sztuke z wersja ustawien, Forbidden bez ToLower na sztuce | 150-400 ms | niskie | te same liczby; zakres po E8/0 |
| O3 | Wywoz kupcow: cele w zasiegu raz na sesje (kolejnosc jak `places`), Demand i Multiplier celu raz na koszyk | 200-450 ms (**ta jedna klatka**) | niskie | uwaga na TradeCarts.Incoming i oblezenia - zostaja w petli |
| O4 | Musztra: wspolny odczyt pozycji lordow na godzine (Drill, NightMarch, WorldMeasure), petle po rosterach raz na partie-dobe | 100-300 ms po roku | srednie | dopiero po podziale E8/0 |
| O5 | Linie diagnostyczne: klucz "lekki log w grze" (drogie ksiegi i pomiary tylko w autotescie) + buforowany zapis logu (flush przy bledzie, na koniec doby i przy wyjsciu) | 250-350 ms | niskie technicznie | **decyzja Jeffa** - mniej danych z jego gry; przy CTD buforowany log moze zgubic ostatnie linie |
| O6 | DTE i BK przez delegaty zamiast MethodInfo.Invoke (AiGear `_add`, `_blTest`; MaterialOrders) | 10-40 ms | niskie | |
| O7 | Rozlozenie skokow: GarrisonArmory.SellWeek po godzinach; ewentualnie wywoz po koszykach w godzinach | 0 CPU, znika 0.1-2.8 s zamrozenia | srednie | zmienia godzine zdarzen i podzial w ksiedze obiegu |
| - | Liczba partii na swiecie | najwiecej (r 0.89) | wysokie (ekonomia, AI) | poza 8.17 - osobna decyzja |

Razem O1-O3+O6: ok. 0.4-1.2 s/dobe (2-7% z 17.5 s); z O4-O5: ok. 0.8-1.8 s (5-10%). Uczciwie: nawet zero naszego kosztu to najwyzej ok. 15-18% szybciej.

## 7. Ryzyko / co sprawdzic (zasada 0 CLAUDE.md)

- **Regresje:** O1 - indeks musi byc nieaktualny, gdy miedzy odczytem wersji a nasza zmiana zmienil sie ktos inny (sprawdzac wersje PRZED i PO wlasnym
  AddToCounts; gdy skok wersji > 1 - zwykla przebudowa). O2 - `_wip` jest w zapisie (`Warsztaty (zapis 174)`, 1761 sztuk) - format bez zmian, test
  zapis -> wczytanie. O3 - ta sama kolejnosc celow i ten sam warunek remisu (`profit <= bestProfit`), inaczej zmieni sie kto dostaje towar.
- **Kolizje:** HorseState juz lata `ItemRoster.OnRosterUpdated` (O1b dopisuje drugi postfix - kolejnosc bez znaczenia, oba tylko licza); Cost174 i
  E8/0 nie moga liczyc tego samego dwa razy w sumie "nasze".
- **Spojnosc:** zadna paczka O1-O4, O6 nie zmienia przeplywow zlota ani towaru (te same liczby); O7 zmienia godzine - sprawdzic linie ksiegi obiegu
  (R, "Armoury tick dobowy" / "poza tickiem") w sprawdz_logi.
- **Stan miedzy kampaniami:** nowe pamieci (LineKey, cele w zasiegu, Needs) czyscic w Reset/konstruktorze behavior, jak ShelfIndex.Reset.
- **Martwy kod:** nic nie ozywa (zadna paczka nie zdejmuje wylacznika).

## 8. Poza zakresem tego rozpoznania (do innych rozpoznan etapu 8)

- Puchnace polki (towar bez odbiorcy 14 tys. / 50 tys.) - przyczyna ekonomiczna (cennik, zasieg wywozu) - przy 8.5; skutek wydajnosciowy opisany w 2.4.
- Rozmiar zapisu (26-31 MB; arm_people 561 tys. znakow, Warsztaty 230 tys.) i czas autozapisu - nie mierzony; ewentualnie w E8/0 (czas SyncData).
- FPS mapy (MapVillagesView: budowa obrazka 0.21 ms, budzet budowy < 8 ms/klatke z E7/W5) - linie sa, problemu w logu nie widac.
