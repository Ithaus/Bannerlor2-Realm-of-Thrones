## p5-bloto (20.10) - bloto poza droga: w deszczu partia ladowa poza droga 15% wolniej, na drodze bez kary (Armoury)

**Dla Jeffa (prosto).** Gdy na mapie pada albo ziemia jest jeszcze mokra po deszczu, kazda partia na ladzie - Twoja, lordow, karawany, wozy wsi i bandy -
idzie poza droga o 15% wolniej. Na trakcie (i na drodze do wioski) blota nie ma, a do tego dalej dziala premia drogi, wiec w zla pogode droga oplaca
sie naprawde. W okienku predkosci widac osobna linie "Mud". Snieg ma dalej swoja kare (bez blota na dokladke), a statki na morzu nic nie czuja.

**Problem / zrodlo pomyslu.** POMYSLY-35 pkt 19 (R3-07c), Jeff 20.10: "28 - nie, 29 - nie. Pozostale tak". Audyt drog 09.10
(`docs/audyt-2026-10-09/08-drogi-wioski-widok.md`, tabela 4.3 M-3): "poza droga -15% w deszczu, na drodze bez kary"; Mortimer: pieszy 15-20 mil dziennie,
w zla pogode 6-8. Dzis droga daje +15% (trakt) / +7.5% (droga do wioski), ale deszcz nie zmienia nic poza mala kara jazdy z gry.
Wiosna i jesien z audytu - NIE: pory Westeros trwaja latami, wiec kara "jesienia" bylaby przez lata. Tylko deszcz.

**Zmiana.** Nowy plik `Armoury/src/MapMud.cs` + minimalne wpiecia:
- REGULA: partia ladowa (gracz i AI, ta sama regula) poza droga w deszczu: -`MapMudSpeedPercent`% (15) wyniku predkosci jako wpis "Mud".
  Deszcz = pogoda mapy w punkcie partii (`MapWeatherModel.GetWeatherEventInPosition`): `LightRain` (w grze: mokro - padalo w ostatnich 24 h) albo `HeavyRain`
  (pada teraz). `Snowy` / `Blizzard` bez blota (maja "Snow" z TerrainEase), `Storm` (burza morska NavalDLC) bez blota.
- "Na drodze" = ta sama siatka i ten sam margines co premia drogi (`MapRoads.KindAt` - nowa metoda, nic w istniejacych liniach MapRoads): trakt, a przy
  `MapSideRoadSpeedBonus` tez droga boczna do wioski. Wymaga `MapRoadSpeedBonus` (bez siatki drog bloto NIECZYNNE, z powodem w logu).
- MIEJSCE: `MarchPace.SpeedPostfix` (jedna latka na kazdy CalculateFinalSpeed, Priority.Last, poziom zewnetrzny lancucha - SpeedDepth), po `MapRoads.SpeedRoad`.
  Kara = `Add(-wynik x procent / (1 + suma wspolczynnikow))` - jak premia drogi, w dol. Sufit kolumny marszowej zbija wynik wpisem Add, wiec po karze wynik
  jest pod sufitem i kara nie ginie (kolumna Jeffa 3.00 -> 2.55 w blocie). Gdy inny model trzyma wynik samym LimitMax (bez Add) - sufit w dol o ten sam procent
  (licznik "sufit obnizony"). NightRest i TownInns biegna po nas (procent od wyniku) - mnoza sie z blotem.
- PAMIEC PREDKOSCI GRY (dekompilacja 1.4.8 `MobileParty.CalculateSpeedForPartyUnified` / `IsBaseSpeedCacheInvalid`): gra sama liczy predkosc od nowa, gdy
  w punkcie partii zmienia sie `GetWeatherEffectOnTerrainForPosition` (Default <-> Wet; Wet = LightRain, HeavyRain, Snowy, Blizzard). Wejscie na droge i zejscie
  z niej - postfiks pamieci MapRoads (stan drogi partii) jak dotad. Nowej latki pamieci nie ma.
- KOSZT: sciezka predkosci (watki robocze gry) bez alokacji i bez blokad - odczyt tablicy pogody gry, siatka drog tylko w deszczu; liczniki przyblizone
  ("ok.", jak MapRoads), potkniecia przez Interlocked (licz potkniecia, bez wylacznika).
- POMIAR (tylko log): spis partio-godzin w dobach pomiaru (`LogGate.Today`), audyt w takcie linii (do 8 partii AI w blocie + gracz: model wprost bez blota
  i z blotem), linie `Bloto (BL): start sesji ...` i `Bloto (BL): dzien N ...`. Nic w zapisie gry (zapis bez zmian).
- KLUCZE NOWE (grupa MCM "The marching column", przy kluczach drog): `MapMudSpeedPenalty` (TAK), `MapMudSpeedPercent` (15, kod przycina do 0..50; zakres
  suwaka w `tools/gen_mcm.py`). Wylaczony klucz = sciezka sprzed zmiany (MapMud.Speed wychodzi od razu).
- Wpiecia: `MarchPace.cs` (1 linia), `MapRoads.cs` (nowa metoda KindAt), `ArmouryBehavior.cs` (godzina, start sesji po MapRoads.SessionStart, doba po
  MapRoads.Daily), `Settings.cs` (2 pola po MapSideRoadSpeedPercent), `McmSettings.cs` (gen_mcm), `tools/gen_mcm.py` (zakres suwaka).

**Ryzyko / co sprawdzic (kontrola calosci).**
- AI nie wybiera drog (R3-07d osobno), wiec bloto spowalnia glownie lordow, karawany, wozy wsi i bandy: wolniejsze dostawy do miast i wolniejsze wojny w deszczu.
  Ile to razy - zalezy od tego, jak czesto pada na mapie ROT (deszcz z tekstury mapy i pory roku gry); nie mierzylem tego wczesniej - linia doby pokaze
  "partio-godziny w deszczu" i "w blocie". Gdyby blota bylo za duzo (np. > 30% partio-godzin), obnizyc `MapMudSpeedPercent`.
- Bez podwojnej kary: RBL Seasons (Flood / SpringStorm) wylaczone na twardo (CrashScribe Mends `SeasonsOff`), ROT i BEE nie licza pogody w predkosci
  (sprawdzone w dekompilacji). Zostaje kara gry "Cavalry weather penalty" (mokro: premia jazdy -30%, w bazie, na drodze tez) - zwykle i tak pod sufitem kolumny.
- Pomiar E7/T (TerrainEase, srednie predkosci las / rownina) w dobach deszczu dostaje szum z blota (obie grupy poza droga) - tylko log.
- Audyt drog (Drogi (DR)): gracz poza droga w deszczu ma bloto w obu liczeniach (bez premii i z premia) - linia drog bez zmian znaczenia.
- Brzeg: przejscie snieg <-> deszcz (oba Wet) bez zmiany sciany siatki - kara wchodzi przy nastepnym przeliczeniu (sciana, noc, droga). Gra liczy tak samo
  swoja kare sniegu.
- NavalDLC: `GetWeatherEventInPosition` przy Clear przeglada liste burz - wolane juz dzis z watkow roboczych (TerrainEase, gra); wyjatek = potkniecie.
- Zamknieta ekonomia: bez zlota i towarow. Zapis: bez zmian.

**Status.** DO SPRAWDZENIA (build Armoury 0 bledow; autotestu nie uruchamialem - zakaz w zadaniu).

**Decyzje (moje, Jeff: "ekonomie projektuje Claude").**
- Tylko deszcz (LightRain + HeavyRain), bez wiosny i jesieni (pory Westeros trwaja latami). Jedna kara 15% dla mokro i ulewy (prosto, jak w audycie).
- Droga boczna do wioski chroni przed blotem tylko wtedy, gdy liczy sie w premii (MapSideRoadSpeedBonus) - jedna zasada "gdzie jest droga" dla premii i blota.
- Bez siatki drog (MapRoadSpeedBonus wylaczony / brak pliku) bloto NIECZYNNE, a nie "wszedzie" - inaczej kara objelaby tez trakty.
- Storm (burza NavalDLC) bez blota - gra liczy jej zasieg inaczej niz deszcz (pamiec predkosci), a to zjawisko morza.

**Test w grze / log_grep.**
- `Bloto (BL): start sesji` - klucz tak, siatka wczytana, latka MarchPace w N > 0 modelach, model pogody, `kara CZYNNA`.
- `Bloto (BL): dzien` - partio-godziny w deszczu i w blocie > 0 (jesli pada), `z kara blota` > 0, audyt AI w blocie: `kara rzeczywista sr. 15.00%`,
  `bez wpisu Mud 0`, `pamiec gry zgodna n/n`, `potkniecia dzis 0`, koszt sciezki rzedu ulamka us.
- W grze: w deszczu poza droga dymek predkosci pokazuje linie "Mud" (ok. -15%); po wjechaniu na trakt linia znika i jest "Road".
