## m97-posilki (20.10) - posilki Dynamic Reinforcements marszem: przychodza tylko z bliska, im dalej, tym pozniej (Armoury)

**Dla Jeffa (prosto).** Posilki z Dynamic Reinforcements przychodza teraz tylko z bliska i tak szybko, jak naprawde moga dojsc: oddzial liczy godziny
marszu ze swojej odleglosci i swojej predkosci na mapie (zaloga i milicja wychodza z murow pieszo, noca i w sniegu wolniej). Kazda godzina marszu to 2 minuty czekania
w bitwie, plus pol minuty na rozwiniecie szyku - lord z 2 km stanie po ok. minucie, z 9 km po 2.5 minuty, z 17 km po 4.5 minuty. Kto ma wiecej
niz 2 godziny marszu (dalej niz ok. 17-20 km), nie przychodzi wcale, a DR powie, ze jest za daleko. Wylacznik: "Dr Arrival By March" w MCM
(The Armoury, grupa Battle reinforcements); tam tez trzy suwaki: najdluzszy marsz, sekundy na godzine marszu i rozwiniecie.

**Problem.** Jeff 20.10: "te Dynamic Reinforcements cos zle dziala, wojsko za szybko pojawia sie i powinno pojawiac sie tylko, jesli jest naprawde
blisko, i im jest dalej, tym dluzej sie na nie czeka".

**Rozpoznanie (dekompilacja DynamicReinforcements v2.1.6, `m97-posilki-tmp/drcalc.cs` i `dr_*.cs`).**
- `PartyScanner.Scan`: kandydaci = kazda przyjazna partia i warownia (zaloga, milicja) w promieniu `MaxScanDistance` od bitwy, `Distance` = linia
  prosta w jednostkach mapy. U Jeffa (`Configs/DynamicReinforcements.cfg` i MCM `DynamicReinforcements_v1.json`, tylko odczyt): `MaxScanDistance 50`,
  `ArrivalCap 60`, `Mode Balanced`, `MinGapBetweenGroups 8`, `ArmiesAsBlock True`, `ExplainRefusals True`.
- `ReinforcementCalculator.ComputeArrivalTime`: ulamek `Distance / MaxScanDistance` -> stopnie 25/30/40/50/60 s x `ArrivalCap/60`, potem
  `Math.Min(ArrivalCap)`. Czyli kazdy w promieniu 50 jedn. staje w bitwie najpozniej po 60 s, a wszyscy do 5 jedn. po 25 s.
- Skala mapy: 4.75 km na jednostke (WorldPace, lore Mur -> Sunspear). 50 jedn. = ok. 240 km. Pomiar marszu w logu Jeffa (Armoury 14-53-56, linia
  "Miara: marsz"): partie lordow ponad 300 ludzi mediana 142 km na dobe przy 17.6 h ruchu = ok. 1.7 jedn./h; czysto konne ok. 2.4 jedn./h.
  Czyli lord z 40 jedn. (190 km, ok. 22 h marszu) dochodzil po 60 s bitwy.
- Odmowa "za daleko" (`SetRejectReason` -> `DR_refuse_far`) dzialala tylko, gdy wynik (`ComputeScore`) byl ponizej progu trybu - wysoki wynik
  (relacja, honor, ta sama frakcja) = przychodzi z kazdej odleglosci w promieniu skanu. Kara za odleglosc w wyniku to najwyzej -40 pkt.
- Kto czyta wynik `Evaluate` (`BattleStateBehavior.AfterStart`): `Candidates = list`; do `SpawnManager.Register` tylko `Accepted`; komunikat odmowy
  `Messages.AnnounceRefusal` - jeden, kandydat z najwyzszym wynikiem wsrod odrzuconych z `RejectReasonId` (z OBU stron bitwy); relacje
  `Consequences.RewardHelpers` - tylko `Accepted && TroopsSpawned > 0`; straty zalog/milicji (`PendingCasualties`, `GarrisonCommitPercent`) - tylko
  za ludzi wystawionych w bitwie. Daleki odrzucony oddzial nie jest wiec ogloszony, nie wchodzi do bitwy i nie traci ludzi.
- Predkosc partii: `MobileParty.ComputeNextMoveDistance` = `Speed * dt`, `Campaign.Tick` = 4320 s gry na jednostke dt, wiec jedn./h = Speed / 1.2
  (jak `NightMarch.UnitsPerHour`). Czlonek armii doczepiony do wodza idzie z predkoscia wodza (`MobileParty.CalculateSpeed`). Piesi: wzor gry
  `DefaultPartySpeedCalculatingModel.CalculateBaseSpeedForParty` = BaseSpeed (4) x (200 / (200 + ludzi))^0.4.

**Zasada (jedna regula na wszystkie zrodla).**
- Trasa = 1.1 x linia prosta (ta sama zapasowa trasa co `NightMarch.HoursToSettlement`).
- Predkosc: partia lorda / armii / najemnikow - ostatnia predkosc, ktora gra przesuwala ja po mapie (`_lastCalculatedSpeed`, czlonek armii - wodza)
  / 1.2; zaloga i milicja (oddzial wychodzi z murow pieszo, tylu ludzi, ilu DR wysyla) - wzor gry dla pieszych x tempo swiata (`WorldPacePercent`, u Jeffa
  75%), noc i snieg jak w grze (u Jeffa `TerrainEase`: noc -0.5, snieg -0.2 plasko; bez TerrainEase procent gry -25% / -10%), najmniej 1 (`MinimumSpeed`),
  / 1.2. Przyklad: 100 ludzi z zalogi = 2.13 jedn./h w dzien, 1.81 noca, 1.69 noca w sniegu; 300 ludzi = 1.73 jedn./h w dzien (tyle co zmierzone partie lordow).
- Partia lorda albo armia, ktora od wczytania zapisu stoi w miejscu (gra nie ma jeszcze jej predkosci - `_lastCalculatedSpeed` = 0): ten sam wzor
  pieszych z ludzi calej kolumny (wodz + doczepieni, jak `CalculateLandBaseSpeed`), noc i snieg w pozycji wodza. W logu etykieta
  `(wzor, stoi od wczytania, N ludzi kolumny)`.
- Godziny marszu h = trasa / predkosc. Czas w bitwie t = `DrDeploySeconds` (30 s) + h x `DrSecondsPerMarchHour` (120 s).
- h > `DrMaxMarchHours` (2 h) - oddzial NIE przychodzi: odrzucony z powodem DR `DR_refuse_far` ("{NAME} is too far to reach us in time.").
- Armia jako blok (`ArmiesAsBlock`): czas najdalszego/najwolniejszego czlonka (DR bierze Max z czlonkow - bez zmian); odstep `MinGapBetweenGroups`
  miedzy grupami - bez zmian (kod DR). `ArrivalCap` i stopnie DR przy wlaczonym kluczu nie licza sie wcale (to one dawaly 25-60 s kazdemu).

Przyklady (lord z partia, ok. 2.0 jedn./h na mapie):

| Marsz | Odleglosc (linia prosta) | Czas w bitwie teraz | Bylo (DR, ustawienia Jeffa) |
|---|---|---|---|
| 0.3 h | 0.55 jedn. (ok. 2.6 km) | 30 + 36 = **66 s** | 25 s |
| 1 h | 1.8 jedn. (ok. 8.6 km) | 30 + 120 = **150 s** (2.5 min) | 25 s |
| 2 h | 3.6 jedn. (ok. 17 km) | 30 + 240 = **270 s** (4.5 min) | 25 s |
| 3 h | 5.5 jedn. (ok. 26 km) | **nie przychodzi** - "too far to reach us in time" | 30 s |
| ok. 22 h | 40 jedn. (ok. 190 km) | **nie przychodzi** | 60 s |

Zaloga zamku (100 ludzi, 2.13 jedn./h w dzien): zamek 3 jedn. od bitwy = 1.55 h -> t+216 s; 4 jedn. = 2.07 h -> nie wychodzi.
Ta sama zaloga noca (1.81 jedn./h): 3 jedn. = 1.82 h -> t+248 s; 4 jedn. = 2.43 h -> nie wychodzi.
Armia 800 ludzi, stoi od wczytania (wzor kolumny 1.31 jedn./h): przychodzi z najwyzej ok. 2.4 jedn. (przed poprawka liczona jak 300 ludzi: 1.73 jedn./h, do 3.1 jedn.).

**Zmiana.**
- `Armoury/src/DrMarchArrival.cs` (nowy): latki na DR przez refleksje (`AccessTools.TypeByName` + `DeclaredMethod`), zakladane w kampanii
  (`ArmouryBehavior.OnSessionLaunched` -> `EnsureHooks`, raz na proces; `SubModuleMain` tylko podaje Harmony):
  - prefiks `ReinforcementCalculator.Evaluate`: pomiar marszu kazdego kandydata; dalecy zdjeci z listy na czas `Evaluate` - DR nie liczy ich do progu,
    limitu ludzi (`MaxTotalReinforcementTroops`), limitu grup ani armii jako bloku;
  - finalizer `Evaluate` (tez po wyjatku DR): dalecy wracaja na koniec listy: `Accepted = false`, `ArrivalTime = 0`, wynik z `ComputeScore` DR (kolejnosc
    komunikatu odmowy i opinii frakcji DR jak dotad); powod `DR_refuse_far` tylko po stronie gracza (tekst mowi "us", a komunikat DR wybiera z obu stron -
    bez tego co bitwe wyskakiwaloby "lord wroga is too far to reach us"); daleki sojusznik wroga zostaje w domu po cichu;
  - prefiks `ReinforcementCalculator.ComputeArrivalTime`: nasz czas z marszu; kandydat bez pomiaru (wyjatek) albo klucz NIE - czas DR po staremu.
- `Settings.cs` / MCM (grupa "Battle reinforcements", nowe klucze - nie ma ich w Armoury.json Jeffa): `DrArrivalByMarch` (TAK), `DrMaxMarchHours` (2, suwak
  0.25-6), `DrSecondsPerMarchHour` (120, 10-600), `DrDeploySeconds` (30, 0-300); kod przycina tak samo jak suwaki (`gen_mcm.py` RANGES).
- Log Armoury: `Posilki marszem (m97-posilki): start - klucz DrArrivalByMarch TAK, latki wpiete 2/2 ...` przy kazdym starcie sesji (bez DR: `start - brak
  modu DynamicReinforcements ... - wylaczone`); przy kazdej bitwie z kandydatami DR: `... bitwa N | kandydatow, za daleko (ponad 2.00 h marszu; po stronie
  gracza), przyjetych, blisko a odmowa DR, bez pomiaru | czas = 30 s + h x 120 s | najblizsi za daleko: nazwa [zrodlo] odleglosc / predkosc = h | sesja ...`
  i linia na kazdego przyjetego: `przyjety NAZWA [Lord] 120 ludzi | odleglosc, predkosc (partia/armia/zaloga pieszo/milicja pieszo/wzor, stoi od wczytania,
  N ludzi kolumny; dopisek ", noc" / ", snieg" przy wzorze), marsz h -> czas z marszu, w bitwie t+` (koncowy czas DR po odstepie grup i armii).
  Linia bitwy konczy sie `| noc TAK/NIE, partii liczonych wzorem (stoja od wczytania) w sesji N`; linia startu podaje predkosc 100 ludzi w dzien,
  noca i noca w sniegu oraz kary nocy i sniegu (TerrainEase albo procent gry).
- `tools/sprawdz_logi.py --grupa m97posilki`: P97-0 linia startu (klucz TAK, latki 2/2), P97-1 potkniecia i ERROR DrMarchArrival 0, P97-2 zaden przyjety
  ponad maks. godzin marszu, P97-3 czas rosnie z godzinami (wzor, w bitwie nie wczesniej niz z marszu, dalszy nie wczesniej), P97-4 przyklady "za daleko"
  ponad maks., P97-5 bitwy/przyjeci/dalecy (INFO), P97-6 przyjeci bez pomiaru 0. Sprawdzone na logu probnym (7 progow TAK/INFO) i na logu bez paczki (INFO).

**Ryzyko / co sprawdzic (kontrola calosci).**
- Posilkow bedzie DUZO mniej: przy 2 h marszu przychodza tylko oddzialy z ok. 3-5 jedn. (15-25 km). To jest zamiar (Jeff: "tylko, jesli jest naprawde
  blisko"); jesli za malo - suwak `Dr Max March Hours` (np. 3 h), jesli za dlugo sie czeka - `Dr Seconds Per March Hour`.
- Bitwa krotsza niz czas marszu: posilki nie zdaza (nie wchodza, nie traca ludzi, bez relacji DR) - zgodne z zasada.
- Predkosc z `_lastCalculatedSpeed`: bez wolania modelu predkosci w misji (zadnych latek predkosci ani mapy w trakcie bitwy; czytamy tylko pore dnia
  i pogode w punkcie - `Campaign.IsNight`, `MapWeatherModel.GetWeatherEventInPosition`, jak `TerrainEase`/`WinterRealm`, w try/catch). Partia, ktora
  po marszu stanela, ma predkosc z ostatniego marszu (z jego pora i terenem). PO WCZYTANIU ZAPISU pole ma 0, nie 1.0 (recenzja 20.10, sprawdzone:
  `[CachedData] _lastCalculatedSpeed = 1f` nie jest zapisywane, a `ObjectLoadData` tworzy obiekt przez `FormatterServices.GetUninitializedObject` - bez
  inicjalizatora pol; liczy je dopiero ruch: `CalculateSpeedForPartyUnified` / `ValidateSpeed` w `ParallelTickMovingArmies`, stojace partie ida
  `TickForStationaryMobileParty` bez liczenia predkosci). Takie partie (lord w zamku, oboz, zbiorka armii) licza sie wzorem pieszych z ludzi calej kolumny
  - jezdzcy tez jak piesi (wolniej niz naprawde, gdy cala kolumna konna), juki i tabor pominiete. Predkosc bzdurna (> 50) - to samo.
  Linia prosta x 1.1 nie widzi rzek i gor (jak `NightMarch` zapasowo).
- Wzor pieszych liczy noc i snieg, ale nie las, brod ani pustynie w miejscu osady (pole nawigacji osady - nie ruszamy mapy w misji). Lord z predkoscia
  z mapy ma je w sobie (z miejsca, gdzie ostatnio szedl). Zalogi z lesnych zamkow (Polnoc) wychodza wiec troche szybciej niz lord obok w tym samym lesie.
- Tryb DR `TotalWar` (x0.8 czasu) i `ArrivalCap` przy kluczu TAK nie dzialaja - nasz czas zastepuje caly `ComputeArrivalTime`. Klucz NIE = DR jak dotad.
- Kolizje: `BandShields.DrFriends` (herby strony gracza z `Candidates` z `Accepted`) i `ReinforcementThanks` (`RewardHelpers`, tylko `Accepted &&
  TroopsSpawned > 0`) czytaja ten sam stan - dalecy nie sa przyjeci, wiec nie licza sie tam (jak kazdy odrzucony). `CallToArmsBehavior` (wezwanie na mapie)
  nie wola `Evaluate` - bez zmian. Innych latek na DR w pieciu modach nie ma. Liczba wojska, prog trybu, limity, fale, `GarrisonCommitPercent` - bez zmian.
- Komunikat odmowy DR (jeden na bitwe, gdy ExplainRefusals) bedzie teraz czesto "X is too far to reach us in time." - prawda i wyjasnienie, czemu lord nie
  przyszedl. Daleki sojusznik WROGA nie dostaje powodu (cicho).
- Latka: `ComputeArrivalTime` 191 bajtow IL z galeziami (zmierzone System.Reflection.Metadata, `m97-posilki-tmp/ilsize`) - JIT nie wstawia jej w lambdy
  `Evaluate`, nie jest 1-bajtowa (pulapka GC z 19.10 nie dotyczy); `ReinforcementCalculator` bez konstruktora statycznego, `DRConfig..cctor` 12 B
  (`Current = new DRConfig()`) - kompilacja przy zakladaniu latki nic z gry nie czyta.
  `Harmony.Patch` zwrocil null (BKROTPatch polyka wyjatek) = linia startu "latki wpiete 0/2 albo 1/2" i DR po staremu.
- Wyjatek w pomiarze kandydata: ten kandydat - czas DR po staremu, licznik potkniec (pierwsze 3 jako ERROR in DrMarchArrival.*), bitwa idzie dalej.

**Poprawki po recenzji (20.10).**
- WAZNE, prawdziwe: po wczytaniu zapisu stojace partie maja `_lastCalculatedSpeed` = 0 (nie 1.0, jak pisal pierwszy wpis - dowod wyzej w Ryzyku),
  a zapasowy wzor bral `TroopCount` samego kandydata. Czlonek stojacej armii (300 ludzi w armii 800) szedl wiec jak 300 ludzi: 1.73 zamiast 1.31 jedn./h,
  ok. 30% za szybko, i przychodzil z 3.1 zamiast 2.4 jedn. Teraz: ludzie calej kolumny (`ColumnMen`: partia + `AttachedParties`, to samo co
  `CalculateLandBaseSpeed`), etykieta `wzor, stoi od wczytania`, licznik w linii bitwy. Opis ryzyka poprawiony.
- DROBNE, poprawione tanio: wzor pieszych pomijal noc i snieg - zaloga noca szla szybciej niz lord obok. Teraz noc (`Campaign.IsNight` w chwili bitwy)
  i snieg (pogoda przy bramie osady albo przy wodzu) jak w grze z `TerrainEase` Jeffa (plasko -0.5 / -0.2 od bazy) albo bez niej (-25% / -10%).
  Las, brod i pustynia w miejscu osady - pominiete (opisane w Ryzyku).
- Build Armoury kod 0 (5 starych ostrzezen), `sprawdz_logi --grupa m97posilki` bez zmian wyrazen (etykiety bez nawiasow, dopiski na koncu linii).

**Status: DO SPRAWDZENIA** (build Armoury kod 0; gra i autotest nieuruchamiane - Jeff gra na 95a / trwa test 96).

**Test w grze krok po kroku.**
1. Po wgraniu 97: MCM -> The Armoury -> Battle reinforcements: "Dr Arrival By March" TAK, 2.00 h, 120 s, 30 s.
2. Wczytaj zapis; w `Modules/Armoury/Armoury-*.log` linia `Posilki marszem (m97-posilki): start - klucz DrArrivalByMarch TAK, latki wpiete 2/2`.
3. Bitwa obok przyjaznego zamku albo lorda (1-3 jedn. na mapie, prawie stykaja sie ikony): posilki wchodza po ok. 1-4 min (najpierw zapowiedz DR
   "approaching"), nie po 25 s. W logu linia `bitwa N` i `przyjety ... marsz X h -> czas z marszu Y s, w bitwie t+Z s`. Lord, ktory od wczytania
   siedzi w zamku, ma etykiete `(wzor, stoi od wczytania, N ludzi kolumny)` - to poprawne; noca przy zalodze dopisek `, noc`, a czas dluzszy niz w dzien.
4. Bitwa z dala od swoich (np. 10-20 jedn. od najblizszego zamku): nikt nie przychodzi, DR pokazuje "... is too far to reach us in time."; w logu
   `za daleko K` i `najblizsi za daleko: ... = X h` (X > 2).
5. `python -I tools/sprawdz_logi.py --grupa m97posilki <Armoury-*.log>` - P97-0..P97-6 bez NIE.
6. Kontrola wylacznika: "Dr Arrival By March" NIE -> nastepna bitwa jak dotad (posilki po 25-60 s, w logu brak linii `bitwa N`).
