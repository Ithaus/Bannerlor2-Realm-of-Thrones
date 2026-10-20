## p6-sztormy (20.10) - zimowe sztormy na morzu: zima 2 razy wiecej burz, statek w burzy -15% (Armoury)

**Dla Jeffa (prosto).** Zima w Westeros na otwartym morzu rodzi sie dwa razy wiecej burz i naraz moze ich szalec do 12 zamiast 6.
Kazda burza jest taka jak dotad: statki w niej widza mniej, kadluby obrywaja, a AI z poobijana flota ucieka od srodka burzy.
Nowe jest to, ze statek w burzy plynie o 15% wolniej (linia "Storm" w okienku predkosci) - Twoj, lordow, karawan i piratow, przez caly rok.
Burza tylko spowalnia, nigdy nie zatrzymuje, wiec rejsy na Pyke, Lys czy do Braavos trwaja zima troche dluzej, ale szlak zostaje otwarty.
Dowoz jedzenia na wyspy wozami kupcow (liczony w dobach) burze omijaja, wiec dluga zima nie zaglodzi wysp przez sam sztorm.

**Problem / zrodlo pomyslu.** POMYSLY-35 pkt 11 (R3-05d), Jeff 20.10: "28 - nie, 29 - nie. Pozostale tak", "Rob od razu fale 2"; po wstrzymaniu
"zacznij fale 2". Zrodlo: `docs/audyt-2026-10-09/10-pomysly.md` Z15 "Sezon zeglugi" (zima czestsze sztormy, wolniejsze statki; najpierw sprawdzic NavalDLC),
`00-AUDYT-SWIATA-2026-10-09.md` 5.3 poz. 16. Dzis burze NavalDLC nie znaja pory roku (liczba burz ta sama latem i zima), a burza nie spowalnia statkow.

**Rozpoznanie NavalDLC (dekompilacja 1.4.8, NavalDLC.dll - nie zgadywane).**
- `StormCampaignBehavior.HourlyTick` -> `TrySpawningNewStorm`: dla kazdego wezla pogody na pelnym morzu (`TerrainType` 19, siatka `DefaultWeatherNodeDimension`^2,
  kolejnosc tasowana raz na kampanie) burza rodzi sie z szansa `NavalDLCStormModel.GetHourlyStormSpawnChanceForPosition` = 0.0025 (stala), o ile burz jest
  mniej niz `MaximumNumberOfStorms` = 6 i nowa jest >= 200 jedn. od srodka kazdej innej (`GetStormSpawnDistanceSquaredThresholdWithOtherStorms` 40000).
  Przy setkach wezlow wolne miejsce zapelnia sie w godziny - wiaze SUFIT, nie szansa. Burze rodzi tylko to miejsce (`CreateStormAtPosition` 1 wywolanie).
- Zycie 10-21 dob + 8 h narodzin + 8 h wygasania; slaby wiatr (< 0.15) obniza sile -0.01/h, sila 0 gasi; zderzenie dwoch (odleglosc < suma promieni)
  gasi mniejsza. Pora roku dziala tylko na RODZAJ burzy (deszcz w punkcie: Storm / ThunderStorm / Hurricane).
- W burzy (aktywna, odleglosc od srodka < `EffectRadius` 20/30/40 x (1 +-0.2 wedlug sily), najwyzej 48): wzrok -40% (`NavalDLCMapVisibilityModel`, wpis
  "Storm"), co godzine szkody kadluba wedlug dzielnosci morskiej (`DamageNearbyParties`), AI z uszkodzona flota ucieka (`NavalDLCMobilePartyAIModel`),
  wiatr +0.1..0.6 (`NavalDLCMapWeatherModel.GetWindForPosition`).
- PREDKOSC: `NavalDLCPartySpeedCalculationModel` nie ma kary za burze - silniejszy wiatr burzy daje wieksza premie "Wind" (do +150% z wiatrem w plecy).
  W naszych modach i w BK/ROT/RBL kary predkosci za burze tez nie ma (grep `GetWeatherEventInPosition`: MapMud - tylko lad, TerrainEase/WinterRealm - snieg).
- Okluder mapy NavalDLC (`CampaignMapAmbientOccluder`): bufor kosci max(sufit + 2, 16) ustawiany raz przy starcie mapy, w kazdej klatce po jednej kosci na
  burze z listy i na burze zadania fabuly (1). Burz wiecej niz 14 = zapis poza bufor - stad twarda granica sufitu 14.
- Pamiec predkosci gry (`MobileParty.IsLastSpeedCacheInvalid`, TaleWorlds.CampaignSystem 1.4.8): na morzu przelicza przy zmianie wiatru w punkcie
  (kierunek > 0.06 rad albo kwadrat dlugosci > 0.0001); wiatr burzy skacze o >= 0.1 na granicy `EffectRadius` - wejscie i wyjscie z burzy przelicza
  predkosc samo, bez latki pamieci.

**Zmiana.** Nowy plik `Armoury/src/SeaStorms.cs` + minimalne wpiecia:
- ZIMA (pora Westeros `WesterosClimate.Now()` - ta sama zima co WinterBite, plony, jarmarki i uboj): postfiks `NavalDLCStormModel.GetHourlyStormSpawnChanceForPosition`
  x `SeaStormWinterFactor` (2: 0.0025 -> 0.005) i postfiks gettera `MaximumNumberOfStorms` x ten sam mnoznik (6 -> 12; nigdy ponad 14, nigdy w dol).
  "x2 do sufitu": przy wiazacym suficie sama szansa nic by nie dala, wiec zima podnosi oba. Po zimie sufit wraca do 6, burze juz trwajace dozywaja
  swoje (nic ich nie gasi na sile). Sila, zasieg, zycie, szkody i ucieczka AI - bez zmian (NavalDLC).
- STATEK W BURZY (caly rok; partia na morzu, nie w przejsciu lad-morze; ta sama strefa co kara wzroku i wiatr burzy NavalDLC): -`SeaStormShipSpeedPercent`%
  (15) wyniku predkosci, wpis "Storm". Miejsce: `MarchPace.SpeedPostfix` (Priority.Last, poziom zewnetrzny lancucha - SpeedDepth), po `MapMud.Speed`,
  czyli po wietrze NavalDLC: `Add(-wynik x procent / (1 + suma wspolczynnikow))`; gdy sufit NavalDLC na morzu (LimitMax 10) zjadlby kare - sufit w dol o
  ten sam procent (jak MapMud). Najwyzej 50%, nigdy zatrzymanie. `MarchPace.ApplyAll` wpina latke takze, gdy wlaczony jest tylko `SeaStormSlowsShips`.
- KOSZT: postfiksy NavalDLC (watek glowny) - mnozenie przez liczbe z pamieci; predkosc (watki robocze, PartiesThink) - tylko partie na morzu: przeglad listy
  burz NavalDLC (najwyzej 14) skompilowanymi getterami, burza dalej niz 48.5 jedn. odpada bez wolania promienia; bez alokacji i bez blokad (NavalDLC czyta
  te sama liste z tych samych watkow w swoim modelu predkosci). Pora i mnoznik - raz na godzine w watku glownym. Spis partii na morzu tylko w dobach pomiaru.
- POMIAR (tylko log): linie `Sztormy (SZ): latki: ...` (menu glowne), `Sztormy (SZ): start sesji - ...`, `Sztormy (SZ): dzien N | ...` w dobie linii:
  pora i flaga zimy, mnoznik czynny, sufit, szansa (NavalDLC -> nasza), wezly pelnego morza, burze (teraz / srednio na godzine / przy suficie / nowe wedlug
  rodzaju), spis partio-godzin AI na morzu i w burzy wedlug rodzaju (rody, karawany, wozy wsi, bandy, inne, gracz), stan kadlubow w burzy, predkosc z pamieci
  gry w burzy i poza, "od wczytania" osobno zima / poza zima (burze aktywne, nowe na dobe, udzial burzy na morzu, predkosc w burzy / poza), audyt do 8
  statkow AI w ruchu w burzy + gracz (model wprost bez kary i z kara: mnoznik, wpis Storm, kara zjedzona, pamiec gry), koszt, potkniecia.
- KLUCZE NOWE (grupa MCM "The marching column", przy kluczach blota): `SeaStormWinter` (TAK), `SeaStormWinterFactor` (2.0, suwak 1..2.3 - kod przycina),
  `SeaStormSlowsShips` (TAK), `SeaStormShipSpeedPercent` (15, suwak 0..50 - kod przycina). Wylaczony klucz = jak przed zmiana (postfiksy oddaja wynik NavalDLC,
  predkosc wychodzi od razu).
- Wpiecia: `MarchPace.cs` (1 linia w SpeedPostfix + warunek ApplyAll), `ArmouryBehavior.cs` (osobna linia Reset `_p6SztormyReset`, godzina, start sesji po
  MapMud.SessionStart, doba po MapMud.Daily), `SubModuleMain.cs` (ApplyAll po MapRoads.ApplyAll), `Settings.cs` (4 pola po kluczach blota), `McmSettings.cs`
  (gen_mcm), `tools/gen_mcm.py` (zakresy suwakow), `tools/sprawdz_logi.py` (`--grupa p6sztormy`, progi SZ-0..SZ-12).
- STAN: brak zapisu (burze zapisuje NavalDLC, regula wynika z pory roku). Liczniki od zera przy nowej kampanii / przed wczytaniem (Reset). Nowa kampania Jeffa
  save040 i stare zapisy - bez zmian w zapisie.

**Ryzyko / co sprawdzic (kontrola calosci).**
- ODSTEP 200 JEDN. MIEDZY BURZAMI: przy narodzinach nowa burza musi stac >= 200 jedn. od kazdej innej. Mapa ROT ok. 1318 x 804 jedn. (siatka drog 659 x 402
  po 2 jedn.), wiec na pelnym morzu moze sie zmiescic mniej niz 12 burz - wtedy zima da np. 8-10 zamiast 12. Linia doby pokaze "godzin przy suficie" i srednia
  aktywnych; jesli zima burz przybedzie malo, nie podnosic mnoznika na slepo (sufit i tak nie jest wtedy wiazacy).
- WIECEJ BURZ = WIECEJ SZKOD I OBJAZDOW AI zima (tak ma byc), ale tez wiecej blota na ladzie wybrzeza: MapMud (p5-bloto) liczy burze NavalDLC nad ladem jako
  "pada" (wybrzeze w zasiegu `HasWetWeatherEffectAtPosition` 1.2 x promien). Burze rodza sie na pelnym morzu, nad brzeg dochodza rzadziej - sprawdzic w linii
  `Bloto (BL)` zima, czy udzial blota przy wybrzezach nie skacze.
- KADLUBY: 2x burz zima = wiecej godzin w burzy i wiecej szkod (NavalDLC), a -15% predkosci = dluzej w burzy. Kara 15% jest mala wobec premii wiatru burzy,
  ale razem moga zwiekszyc zatopienia slabych statkow. Linia doby: "kadluby w burzy ... ponizej 25%". Gdyby zima rosla liczba zatopionych - obnizyc
  `SeaStormShipSpeedPercent`, nie mnoznik.
- WYSPY (Pyke, Lys, Braavos, wyspy z jednym miastem): wozy jedzenia KS-H i hurt TradeCarts plyna "w dobach" (tabela dob drogi, bez partii na morzu) - burze ich
  nie dotycza, dowoz plynie zima jak latem; lordowie i karawany na morzu tylko zwalniaja w burzy (szlak otwarty). Sprawdzic w linii doby karawany na
  morzu zima > 0 (SZ-12) i jak dotad linie KS-H / "Wyspy i drogi".
- OKLUDER: sufit najwyzej 14 + 1 burza fabuly = 15 <= 16 kosci. Domyslnie 12.
- MALE METODY Z DETOUREM (pamiec 19.10, zawieszenie GC): latane metody NavalDLC to `return 0.0025f` (vmovss + ret, ok. 9 B kodu JIT) i getter `=> 6`
  (mov eax, 6 + ret, 6 B) - obie >= 5 B, jmp Harmony miesci sie w metodzie; i tak dziala CrashScribe UnwindRepair. Getter sufitu czyta okluder w kazdej
  klatce - postfiks bez alokacji (porownanie i mnozenie).
- WATKI: lista burz zmienia sie tylko w watku glownym (TickEvent / HourlyTick NavalDLC), predkosc liczona w fazie rownoleglej partii - jak sam NavalDLC
  (wiatr, wzrok). Przeglad ma straznik dlugosci listy. Liczniki watkow roboczych przyblizone ("ok."), potkniecia przez Interlocked, bez wylacznika.
- KOLIZJE (grep w n22-calosc): nikt inny nie lata `NavalDLCStormModel` ani nie czyta listy burz; drzewa n22f-* nie ruszaja MarchPace/MapMud/MapRoads; p6-drogi
  (AiRoadRoutes) i p6-pola, p6-szubienice ruszaja MapRoads, nie SpeedPostfix. Kara burzy i bloto wykluczaja sie (morze / lad).
- Pora: `WesterosClimate.Now()` = pora EKONOMII (ta sama zima co reszta modow). Plik autotestu `armoury-zima-teraz.flag` (pora ekonomii zima na sesje)
  wlacza tez zimowe burze - dobre do proby. Rodzaj burzy NavalDLC idzie dalej z deszczu nieba (bez zmian).
- Zmiana kluczy w MCM: mnoznik zimy od nastepnej godziny gry, kara od nastepnego przeliczenia predkosci statku. Kara przy wylaczonych MarchPace i drogach
  wymaga restartu gry (latka wpinana przy starcie) - linia mowi wtedy NIECZYNNA.
- Zamknieta ekonomia: bez zlota i towarow (tylko pogoda i predkosc). Zapis: bez zmian.

**Status.** DO SPRAWDZENIA (build Armoury Release: kod 0, 0 Error(s); `python tools/gen_mcm.py` bez zmian; autotestu nie uruchamialem - zakaz w zadaniu).

**Decyzje (moje, Jeff: "ekonomie projektuje Claude").**
- Zima x2 dla szansy i sufitu (12 burz zamiast 6), granica 14 z bufora okludera. Tylko zima (pora Westeros), wiosna i jesien jak latem - jedna zasada.
- Kara predkosci w burzy przez caly rok, nie tylko zima: burza to burza; zima jest ich po prostu wiecej. 15% jak bloto na ladzie - lagodnie, bo wiatr burzy
  i tak pcha statek, a NavalDLC daje juz wzrok -40% i szkody.
- Wozy KS-H i TradeCarts (doby) bez zimowej doplaty dob: nie sa partiami na morzu, burza ich nie widzi, a POMYSLY ostrzega przed glodem wysp w wieloletniej
  zimie. Gdyby Jeff chcial, zeby zima wydluzala tez rejs wozow, to osobna zmiana w tabeli dob (dzis w rekach n22f-plany).
- Bez zmiany sily, zasiegu i zycia burz - wiecej burz to wiecej rejsow przez burze, nie mocniejsze burze.

**Test w grze / log_grep.**
- Menu glowne: `Sztormy (SZ): latki: szansa narodzin (GetHourlyStormSpawnChanceForPosition) wpieta, sufit burz (MaximumNumberOfStorms) wpiety | gettery burz NavalDLC gotowe.`
- Start sesji: `Sztormy (SZ): start sesji - wiecej burz zima SeaStormWinter tak (x2.00` ... `lista burz NavalDLC jest (N burz)` ... `| kara CZYNNA.`
- Doba: `Sztormy (SZ): dzien N` - poza zima `(zima nie) - mnoznik czynny x1.00, sufit burz 6 (NavalDLC 6, granica 14)`, zima `(zima tak) - mnoznik czynny x2.00,
  sufit burz 12`, `szansa narodzin na wezel/h x2.00 (NavalDLC 0.0025 -> 0.0050)`, `podniesionych` = `wywolan szansy w dobie`; audyt
  `mnoznik predkosci sr. 0.850 (oczekiwany 0.850`, `bez wpisu Storm 0`, `potkniecia dzis 0 (od wczytania 0), w predkosci dzis 0 (od wczytania 0)`.
- Progi: `python -I tools/sprawdz_logi.py --grupa p6sztormy <Armoury-*.log>` (SZ-0..SZ-12).
- Zwykly autotest (start poza zima): sufit 6, kara w burzy dziala (SZ-7). Drugi bieg na tym samym zapisie z plikiem `armoury-zima-teraz.flag`: sufit 12, burz
  aktywnych srednio wiecej niz w biegu 1 (porownac SZ-5 obu biegow - w jednym biegu jest tylko jedna pora), udzial burzy na morzu wyzszy (SZ-6), karawany
  na morzu dalej plyna (SZ-12).
- W grze: na morzu w burzy linia "Storm" w dymku predkosci (ok. -15%), obok "Wind"; zima na mapie wiecej wirow burz na otwartym morzu.
