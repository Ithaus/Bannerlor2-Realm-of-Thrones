# ROZPOZNANIE Z.3 - przyspieszenie cudzego kodu (13.10.2026)

Tylko odczyt kodu i logow. Zadnej zmiany w kodzie, nic nie wgrane, bez commita.
Plan: `docs/PLAN-DO-KONCA-MODA-2026-10-09.md` rozdz. 4, wiersz Z.3 ("AI lordow x44 po roku, malzenstwa ROT, znaczki druzyn").

## Dla gracza - 6 zdan

1. Nasze mody zjadaja dzis ok. 1.2-1.3 s z 12-13 s doby; reszta to gra i cudze mody, wiec dalsze przyspieszanie trzeba robic w cudzym kodzie.
2. Najwiekszy pojedynczy zjadacz czasu poza silnikiem to swaty lordow z Realm of Thrones: co dobe kazdy lord przeglada liste wszystkich doroslych bohaterow swiata, i to prawie dwa razy - okolo 1.2-1.4 s na dobe, czyli mniej wiecej co dziesiata sekunda czekania.
3. Da sie to zrobic 4-6 razy szybciej bez zmiany czegokolwiek w rozgrywce: te same pary, te same losowania, te same sluby - gra tylko nie liczy tego samego kilka razy. Zysk ok. 1 s na dobe dzis i wiecej w pozniejszych latach kampanii.
4. Kafelki druzyn pod tabliczka miasta ("widly", monety) zostaja po druzynach, ktore znikly w srodku miasta - to blad wygladu w grze, latwy do zalatania, bez wplywu na rozgrywke; w pomiarze nie kosztuje czasu.
5. Decyzji lordow (dokad jada, co oblegaja, kiedy zbieraja armie) NIE ruszam: kazde "przyspieszenie" tam zmienia to, co lordowie robia, a sprawdzic "te same wyniki" sie nie da.
6. Warsztaty i codzienny trening wojska to juz nasze przerobki na kodzie gry - najpierw trzeba je zmierzyc dokladniej w kolejnym biegu, dopiero potem ciac.

## Skad liczby i ile sa warte

- Profil klatki: `scratchpad\test\profil-e8r1.txt` - 10 dob od doby 120, 1501 klatek (150 klatek na dobe), stan SPRZED poprawek rundy 2 i sprzed limitu karawan. Doba w tym biegu 24.4 s (z pomiarem), dzis bez pomiaru 11.8 s (doba 120) i 13.0 s (srednia roku, `autotest-2026-10-13.log`).
- Dlatego dwie kolumny kosztu: **"pomiar"** = ms/klatke x 150 (zawyzone narzutem pomiaru, ok. 1.6x dla cudzego kodu jako calosci) oraz **"dzis"** = udzial w klatce x 12.5 s. Prawda lezy miedzy nimi; do decyzji biore kolumne "dzis" (ostrozna).
- Czego NIE zmierzylem (gra zajeta testem): ilu jest doroslych lordow na liscie swatow, jaki ulamek z nich ma zone/meza, podzial czasu wewnatrz warsztatow i treningu. Te trzy liczby da jeden bieg "cienia" (nizej).
- Dekompilacja z tej sesji: `scratchpad\tmp\z3\` (rot_rel.cs, rot_mm.cs, rot_sub.cs, markers.cs, scai.cs, core_ext.cs, fastrandom.cs).

## Tabela (uszeregowana wedlug zysk / ryzyko)

| # | Pozycja (czyj kod) | Koszt: pomiar / dzis (ms na dobe) | Co w niej kosztuje | Najbezpieczniejsza poprawka | Zysk (ms na dobe) | Ryzyko dla rozgrywki | Jak sprawdzic "te same wyniki" | Wielkosc | Werdykt |
|---|---|---|---|---|---|---|---|---|---|
| 1 | `ROTRelationshipsBehavior.CheckNPCMarriages` (ROT-Core) | 2230 / 1175 (9.4% klatki; 367 rodow na dobe, 6 ms na rod) | kazdy lord x cala lista doroslych bohaterow swiata x ok. 1.6 przejscia (IsEmpty + Count + ElementAt na leniwym Where); polowa kandydatow tej samej plci odpada dopiero po sprawdzeniu rodow; lordowie z zona/mezem licza wszystko, choc szansa slubu wynosi u nich zawsze 0 | latka w CrashScribe Mends: ta sama metoda napisana raz jeszcze - jedno przejscie, lista z gory rozdzielona wedlug plci, dla lorda z malzonkiem tylko "czy jest ktokolwiek" (i jeden krok losowania, zeby ciag losowy zostal ten sam) | 800-1000 dzis (70-85%); po roku wiecej (08.10: malzenstwa x2.4 po roku) | zadne przy wlaczonych strazach: te same pary, ta sama kolejnosc, ten sam ciag losowy; czestosc slubow bez zmiany | bieg "cienia": stara i nowa lista kandydatow liczona obok siebie, porownanie element po elemencie przez 30 dob + rok; ma byc 0 roznic | kod S, tor M (1 recenzja + bieg cienia) | **TERAZ** |
| 2 | Kafelki druzyn pod tabliczka miasta: `SettlementNameplatePartyMarkersVM` (SandBox, gra) | poza pomiarem (ekran mapy w biegu 0.95 ms/klatke = 0.6%; gracz siedzi w menu miasta) | kafelek znika tylko na zdarzenie "druzyna wyszla z osady"; `DestroyPartyAction.Apply` usuwa druzyne w srodku bez tego zdarzenia - kafelek zostaje, lista rosnie | latka UI w CrashScribe Mends: przy kazdym wejsciu druzyny do tej osady wyrzuc kafelki druzyn nieaktywnych albo nieobecnych w osadzie | niezmierzony (wyglad; mniej widgetow na mapie w dlugiej kampanii) | zadne dla rozgrywki (sama lista w UI) | zrzut ekranu przed/po w miescie z "widlami"; licznik usunietych kafelkow w logu | S | **TERAZ** (jako blad wygladu, nie szybkosc) |
| 3 | Pomiar wnetrza: `WorkshopsCampaignBehavior.DailyTickTown` i `MobilePartyTrainingBehavior.OnDailyTickParty` (gra + nasze + BK) | 970 / 510 oraz 615 / 325 | nieznany podzial: w obu siedza nasze latki (WorkshopLaw.CyclePrefix ok. 120 ms, musztra 43 -> 137 ms po roku), model BK i petle gry | dwie sekcje w istniejacym `DayProfile` (calosc metody gry minus nasze) - bez zmiany zachowania | 0 (to pomiar) | zadne | nie dotyczy | S | **TERAZ** (warunek dla 4 i 5) |
| 4 | Trening druzyn: `MobilePartyTrainingBehavior.OnDailyTickParty` + `BKPartyTrainningModel.GetEffectiveDailyExperience` (gra + BK, wpiete nasze Drill/ArmsDrill) | 615 / 325 (6534 druzyn na dobe) | dla KAZDEGO wiersza skladu: nowa funkcja zamknieta + TryCatch BK + ok. 11 pytan o perki druzyny (te same odpowiedzi dla calej druzyny) + szukanie wiersza po postaci; liczone takze dla ok. 5500 druzyn bez bohatera (wiesniacy, karawany, bandyci, majatki BK), gdzie wynik to zawsze 0 | pominac druzyny bez bohatera, poza armia i nie-zalogi (wynik 0); pozniej pamiec perkow na druzyne | 80-200 | male, ale nie zero: gra przy "+0 XP" przycina XP wiersza do limitu (`OnXpChanged`) - pominiecie odklada to przyciecie do nastepnej bitwy | licznik: ile razy pominiety wiersz mial XP ponad limit (ma byc ok. 0) | S | po pomiarze (3) |
| 5 | Warsztaty gry: `WorkshopsCampaignBehavior.DailyTickTown` (gra + nasze WorkshopLaw/WorkshopTrade/TownCrafts + model BK) | 970 / 510 (97 miast, 10 ms na miasto) | linie towarowe gry: przejscie calej polki miasta na kazdy wsad kazdego cyklu (bez przerwania po znalezieniu), cena dla kazdego stosu, nowa lista na kazda wyprodukowana sztuke (6% wszystkich sprzatan pamieci); do tego model predkosci BK na kazda linie | po pomiarze: indeks "kategoria -> wiersze polki" na czas tiku miasta (ta sama kolejnosc wyboru) | nieznany, rzad 100-300 | srednie: to serce zamknietej ekonomii, 4 nasze latki na tych samych metodach, kolejnosc wyboru stosu wplywa na ceny | porownanie polek i kas 97 miast po dobie: stary i nowy kod na kopii stanu | M | po pomiarze (3), nie teraz |
| 6 | `AiVisitSettlementBehavior.AiHourlyTick` (gra) | 1055 / 560 (12 840 namyslow na dobe, 82 us kazdy) | kazdy namysl lorda: wszystkie osady krolestwa x odleglosc (z portami NavalDLC dwa razy) + sortowanie + ocena kazdej osady (ochotnicy u notabli, zaloga, jency, zywnosc) | brak bezpiecznej: kazda pamiec wyniku zmienia dane, na ktorych lord decyduje | - | wysokie: inne trasy lordow, werbunek, zaopatrzenie | nie da sie (wynik zalezy od losu i stanu z tej samej godziny) | - | **NIE RUSZAC** |
| 7 | `AiMilitaryBehavior.AiHourlyTick` (gra) + `StrategicCampaignAI145Behavior.OnHourlyTick` | 696 / 360 oraz 294 / 150 (12 ms na godzine) | 4 rodzaje wypraw x osady wrogow x sila armii; rosnie z liczba wojen i druzyn (08.10: x44 po roku) | brak | - | wysokie: oblezenia, rajdy, obrona, zbieranie armii | nie da sie | - | **NIE RUSZAC** |
| 8 | `ClanVariablesCampaignBehavior.DailyTickClan` (gra) | 528 / 275 (1.4 ms na rod) | dzienne finanse rodu: `CalculateClanGoldChange` przez modele ROT + BK + nasze (KingdomTreasury, MoneyLedger, SoldierPay) i wplywy | brak | - | wysokie: kasa rodow = zamknieta ekonomia | - | - | **NIE RUSZAC** |
| 9 | Silnik: RealTick (ruch druzyn, oblezenia) | 3915 / 2060 (16.5%) | kod silnika, nie sluchacze; rosnie z liczba druzyn (2071 -> 6213 w rok, w tym majatki BK 1390) | zadnej latki; jedyna dzwignia to liczba druzyn (decyzja Jeffa, jak limit karawan) | - | zmiana liczby druzyn = zmiana rozgrywki | - | - | **NIE RUSZAC** (ewentualnie osobna decyzja o majatkach BK) |
| 10 | `PoliticalStagnationAndBorderIncident...HourlyTickSettlement` (gra) | 261 / 140 | co 3 h na osade: druzyny w promieniu -> "zagrozenie" i "sojusznicy" osady | brak | - | to wejscie do AI lordow (obrona osad) | - | - | **NIE RUSZAC** |

## Pozycje po kolei

### 1. Swaty lordow ROT - `CheckNPCMarriages` (TERAZ)

Co robi kod (`ROT.dll`, `ROT.CampaignBehaviors.ROTRelationshipsBehavior`): ROT odlacza swaty gry (`ClearListeners(RomanceCampaignBehavior)`) i co dobe, dla kazdego rodu, dla kazdego zywego lorda z `CanMarry()`: filtruje `_unmarriedHeroes` (lista budowana raz na dobe ze WSZYSTKICH zywych bohaterow z `CanMarry()`) warunkiem `IsCoupleSuitableForMarriage(lord, h)`, losuje jednego przez `GetRandomElementInefficiently`, liczy szanse `NpcCoupleMarriageChance`, losuje i ewentualnie `MarriageAction.Apply`.

Co kosztuje (sprawdzone w dekompilacji):
- `GetRandomElementInefficiently` (TaleWorlds.Core) to `IsEmpty()` + `Count()` + `ElementAt()` na leniwym `Where` - warunek pary liczony jest ok. 1.6 raza dla kazdego kandydata.
- Lancuch modelu: `ROTMarriageModel` owija model BK (`BKROTMarriageModel : BKMarriageModel : DefaultMarriageModel`; ROT bierze ostatni zarejestrowany model, BK laduje sie przed ROT-Core). `BKMarriageModel.IsSuitableForMarriage` NIE sprawdza, czy bohater ma malzonka - na liscie "niezameznych" sa wiec wszyscy dorosli lordowie, zonaci tez, i kazdy z nich sam tez szuka pary.
- Rodzaj plci sprawdzany jest dopiero w modelu gry, po pytaniach o wojne i stosunki rodow; para, ktora przejdzie filtry ROT, placi jeszcze za `Romance.GetCourtedHeroInOtherClan` x2 (lordowie rodu x cala lista zalotow, z alokacja LINQ) i `CanMarry()` x2 (kazde = petla po wszystkich zadaniach `IssueManager` i `QuestManager`).
- Dla lorda z malzonkiem `BKMarriageModel.NpcCoupleMarriageChance` zwraca zawsze 0 (`IsMarriageAdequate`: `proposer.HasPrimarySpouse()` -> -10000), wiec cale przeszukanie jest dla niego na nic.

Poprawka (dokladna, bez zmiany wyniku): prefiks Harmony na `CheckNPCMarriages` z ta sama logika i tymi samymi wywolaniami modelu, ale: (a) kandydaci zbierani do listy w JEDNYM przejsciu, potem `MBRandom.RandomInt(lista.Count)` i `lista[i]` - ten sam element co `ElementAt`; (b) `_unmarriedHeroes` rozdzielona na poczatku wywolania na dwie tablice wedlug plci z zachowaniem kolejnosci - para tej samej plci i tak zawsze daje `false` (`DefaultMarriageModel.IsCoupleSuitableForMarriage`); (c) lord z `HasPrimarySpouse()` (wolana metoda BK przez delegata): szukamy tylko pierwszego pasujacego kandydata, a gdy jest - zuzywamy jeden krok generatora (`MBRandom.RandomInt(1)`; `MBFastRandom.Next(int)` to zawsze dokladnie jeden krok XorShift), tak jak oryginal. Dalsza czesc (szansa, losowanie, kontrola zalotow, `MarriageAction.Apply`, `break`) bez zmian.

Straze (latka sie NIE wlacza, gdy cos nie pasuje - jedna linia w logu): typ modelu na wierzchu = `ROT.Models.ROTMarriageModel`, pod nim model pochodny od `BKMarriageModel` z `IsCoupleSuitableForMarriage` z `DefaultMarriageModel`; dlugosc IL `CheckNPCMarriages` zgodna z dzisiejsza (ROT 8.1.8). Zakladac po `OnSessionLaunched` (modele musza istniec). Ustawienie MCM po angielsku, np. "Faster NPC marriage scan (ROT)".

Zysk: 70-85% tej pozycji zaleznie od ulamka lordow z malzonkiem (0% -> 69%, 50% -> 83%): ok. 0.8-1.0 s na dobe dzis, do 1.9 s wedlug pomiaru bezwzglednego. Ryzyko: wyjatek w modelu rzucony dotad dla pary tej samej plci juz by nie padl (bieg roczny ma 0 bledow, wiec dzis nie pada). Sprawdzenie: build probny w trybie "cien" - prefiks liczy nowa liste i stara liste (pelny warunek na calej `_unmarriedHeroes`), porownuje element po elemencie, dla lordow z malzonkiem sprawdza, ze szansa = 0 dla kazdego kandydata, a sterowanie oddaje oryginalowi; linia doby "MarriageFast: lordow N, z malzonkiem M, kandydatow srednio K, roznic 0". 30 dob od zapisu z doby 120 i bieg roczny; dopiero przy 0 roznic przelaczenie. Dodatkowo licznik slubow na dobe (`HeroesMarried`) przed i po.

Przy okazji (NIE do Z.3, do wiedzy Jeffa): poniewaz zonaci zostaja na liscie, losowanie czesto trafia w kandydata z malzonkiem i dzienna proba lorda przepada - sluby NPC sa rzadsze, niz zamierzal ROT. Poprawienie tego to zmiana rozgrywki, nie przyspieszenie.

Odrzucone: rozlozenie rodow na dni tygodnia (kazdy lord losuje raz na dobe - 7x mniej prob to 7x mniej slubow albo zmiana wzoru szansy); pamiec wyniku miedzy rodami w tej samej dobie (miedzy rodami dzialaja inni sluchacze: zmiany krolestw, wojny, smierci).

### 2. Kafelki druzyn pod tabliczka miasta (TERAZ, jako blad wygladu)

`SandBox.ViewModelCollection.Nameplate.SettlementNameplatePartyMarkersVM` slucha tylko `SettlementEntered`, `OnSettlementLeftEvent` i `MapEventEnded`. `DestroyPartyAction.Apply` (w odroznieniu od `ApplyForDisbanding`) nie wola `LeaveSettlementAction`, wiec druzyna zniszczona w srodku osady nie wysyla "wyszla" i jej kafelek zostaje do przebudowy tabliczki. W profilu autotestu ekran mapy to 0.6% klatki - czasu tu nie widac; jest to lista, ktora rosnie przez cala kampanie na miastach w zasiegu wzroku. Latka: postfiks na `OnSettlementEntered` tego VM - gdy osada sie zgadza, usun pozycje, ktorych `Party` jest nieaktywna albo ma inna `CurrentSettlement`. Nie ruszac zrodla (`DestroyPartyAction`): dodanie tam "wyjscia z osady" odpaliloby naszych i cudzych sluchaczy wyjsc. Ryzyko dla rozgrywki zadne; sprawdzenie okiem (The Eyrie z "widlami") i licznikiem w logu.

### 3. Pomiar wnetrza warsztatow i treningu (TERAZ)

Obie pozycje sa w profilu podpisane "Native", ale w srodku pracuja nasze latki (WorkshopLaw.CyclePrefix, TownCrafts, TownFletchers, WorkshopTrade; Drill.TickPrefix/Postfix, ArmsDrill na `GetEffectiveDailyExperience`), model BK i petle gry. Linia "Profil doby (E8/0)" podaje nasza czesc (CyclePrefix 75-118 ms, trening musztry 43 ms w dobie 108837 i 137 ms w 109198), ale nie calosc metody gry. Dwie dodatkowe sekcje `DayProfile` (calosc `DailyTickTown` i calosc `OnDailyTickParty`) daja podzial "gra i BK" / "nasze" bez zmiany zachowania.

### 4. Trening druzyn (po pomiarze)

`OnDailyTickParty` idzie po calym skladzie kazdej z ok. 6500 druzyn; `BKPartyTrainningModel.GetEffectiveDailyExperience` dla kazdego wiersza tworzy funkcje zamknieta i `ExceptionUtils.TryCatch`, pyta `HasPerk` ok. 11 razy (odpowiedz ta sama dla calej druzyny), potem `AddXpToTroop` szuka wiersza liniowo. Dla druzyny bez bohatera w skladzie, poza armia i nie-zalogi wynik jest zawsze 0. Pominiecie takich druzyn jest prawie dokladne: `AddXpToTroop(.., 0)` wola `OnXpChanged`, ktore przycina XP wiersza do limitu - po pominieciu przyciecie nastapi przy nastepnym XP z bitwy. Zmiana siedzi w naszym `Drill.TickPrefix`, wiec to nasz kod, nie cudzy.

### 5. Warsztaty gry (po pomiarze, nie teraz)

Linie uzbrojenia zastepuje juz nasz `WorkshopLaw`; linie towarowe ida kodem gry: `DetermineItemRosterHasSufficientInputs` przechodzi cala polke miasta na kazdy wsad i nie przerywa po znalezieniu (cena liczona takze dla stosow, z ktorych nic nie bierze), `GetItemsToProduce` -> `GetRandomItemAux` alokuje liste na kazda sztuke. Bez podzialu z pozycji 3 zysk jest zgadywaniem, a ryzyko realne (kolejnosc wyboru stosu = ceny i kasy miast).

### 6-10. Nie ruszac

AI lordow (`AiVisitSettlementBehavior`, `AiMilitaryBehavior`, `StrategicCampaignAI145`, wskaznik zagrozenia osad) - kazda pamiec wyniku albo rzadszy namysl zmienia decyzje, a wynik zalezy od losu i stanu z tej samej godziny, wiec proby porownawczej nie ma. Finanse rodow (`ClanVariablesCampaignBehavior`) to serce zamknietej ekonomii z trzema naszymi latkami. Silnik (RealTick) nie jest sluchaczem - rosnie z liczba druzyn; jedyna dzwignia to ich liczba (1390 druzyn majatkow BK), a to decyzja o rozgrywce, nie latka.

## Kolejnosc

1. Pozycja 1 w trybie "cien" (build probny, autotest 30 dob + rok) -> przy 0 roznic przelaczenie i pomiar doby.
2. Pozycja 2 (latka UI) w tym samym DLL CrashScribe albo osobno - niezalezna.
3. Pozycja 3 (dwie sekcje pomiaru) przy najblizszym biegu profilu; po niej decyzja o 4 i 5.

Zmierzony zysk po pozycji 1 trzeba potwierdzic liczba "s/dobe" z autotestu (dzis 11.8 s w dobie 120, 13.0 s srednio w roku).
