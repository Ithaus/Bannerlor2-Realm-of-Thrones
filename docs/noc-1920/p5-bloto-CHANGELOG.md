## p5-bloto (20.10) - bloto poza droga: gdy pada -15%, na mokrym po deszczu -7.5%, na drodze bez kary (Armoury)

**Dla Jeffa (prosto).** Gdy na mapie pada, kazda partia na ladzie (Twoja, lordow, karawany, wozy wsi i bandy) idzie poza droga o 15% wolniej.
W okienku predkosci jest wtedy linia "Mud". Po deszczu ziemia schnie mniej wiecej dobe i przez ten czas kara jest o polowe mniejsza: 7.5%, linia "Wet ground".
Na trakcie i na drodze do wioski blota nie ma, a premia drogi dziala dalej, wiec w zla pogode droga naprawde sie oplaca.
Snieg ma dalej swoja kare, bez blota na dokladke. Statki na morzu nic nie czuja.
Jesienia i zima w deszczowych krainach bedzie blotnisto czesto, tak jak w sredniowieczu. Linia w logu pokaze, jak czesto.

**Problem / zrodlo pomyslu.** POMYSLY-35 pkt 19 (R3-07c), Jeff 20.10: "28 - nie, 29 - nie. Pozostale tak". Audyt drog 09.10
(`docs/audyt-2026-10-09/08-drogi-wioski-widok.md`, tabela 4.3 M-3): "poza droga -15% w deszczu, na drodze bez kary"; Mortimer: pieszy 15-20 mil dziennie,
w zla pogode 6-8. Dzis droga daje +15% (trakt) / +7.5% (droga do wioski), ale deszcz nie zmienia nic poza mala kara jazdy z gry.
Wiosna i jesien z audytu jako osobna kara - NIE: pory Westeros trwaja latami. Kara idzie tylko z pogody w punkcie partii.

**Zmiana.** Nowy plik `Armoury/src/MapMud.cs` + minimalne wpiecia:
- REGULA (po recenzji): partia ladowa (gracz i AI, ta sama regula) poza droga:
  - pada (`HeavyRain`, albo `Storm` - burza NavalDLC nad ladem wybrzeza): -`MapMudSpeedPercent`% (15), wpis "Mud";
  - mokro po deszczu (`LightRain`): -`MapMudWetSpeedPercent`% (7.5), wpis "Wet ground". W grze `LightRain` to NIE lekki deszcz: teraz nie pada, ale padalo
    w ktoryms z 5 poprzednich okresow po 4 h, czyli do ok. 20-24 h wstecz (`DefaultMapWeatherModel.SetIsRainingOrWetFromFunction`).
  - `Snowy` / `Blizzard` bez blota (maja "Snow" z TerrainEase).
- "Na drodze" = ta sama siatka i ten sam margines co premia drogi (`MapRoads.KindAt`): trakt, a przy `MapSideRoadSpeedBonus` tez droga boczna do wioski.
  Wymaga `MapRoadSpeedBonus` (bez siatki drog bloto NIECZYNNE, z powodem w logu).
- MIEJSCE: `MarchPace.SpeedPostfix` (jedna latka na kazdy CalculateFinalSpeed, Priority.Last, poziom zewnetrzny lancucha - SpeedDepth), po `MapRoads.SpeedRoad`.
  Kara = `Add(-wynik x procent / (1 + suma wspolczynnikow))` - jak premia drogi, w dol. Sufit kolumny marszowej zbija wynik wpisem Add, wiec kara nie ginie
  (kolumna Jeffa 3.00 -> 2.55 gdy pada, -> 2.78 na mokrym). Gdy inny model trzyma wynik samym LimitMax - sufit w dol o ten sam procent ("sufit obnizony").
  NightRest i TownInns biegna po nas (procent od wyniku) - mnoza sie z blotem.
- PAMIEC PREDKOSCI GRY (dekompilacja 1.4.8 `MobileParty.CalculateSpeedForPartyUnified`): gra przelicza predkosc sama tylko przy zmianie
  `GetWeatherEffectOnTerrainForPosition` (Default <-> Wet), sciany siatki, nocy, wiatru. Przejscia ulewa <-> mokro, snieg <-> deszcz i Clear+burza <-> deszcz
  to Wet -> Wet - gra ich nie widzi. Dlatego NOWY postfiks `MobileParty.IsLastSpeedCacheInvalid` (`MapMud.CachePostfix`, DeclaredMethod, wpinany
  w `MapRoads.ApplyAll` obok postfiksu drog): pamiec partii (tablica int bez blokad, 1 << 17 slotow, `Party.Index << 2 | stopien`, -1 pusto) trzyma stopien
  pogody (0 sucho/snieg, 1 mokro, 2 pada) z ostatniego liczenia, ktore gra zapisala do `_lastCalculatedSpeed` (ten sam straznik zapisu co stan drogi -
  nowa metoda `MapRoads.Recording`). Inny stopien w biezacym punkcie = gra liczy od nowa (raz na zmiane pogody). Wejscie na droge i zejscie z niej -
  postfiks pamieci MapRoads jak dotad.
- KOSZT: sciezka predkosci i postfiks pamieci (watki robocze gry) bez alokacji i bez blokad - odczyt tablicy pogody gry (NavalDLC przy Clear przeglada
  liste burz - gra robi to samo w `IsBaseSpeedCacheInvalid` przy kazdym liczeniu), siatka drog tylko w deszczu; liczniki przyblizone ("ok."),
  potkniecia przez Interlocked (licz potkniecia, bez wylacznika).
- POMIAR (tylko log): spis partio-godzin w dobach pomiaru (`LogGate.Today`) z podzialem pada / mokro, pora nieba i czynnik deszczu gry, przeliczenia
  wymuszone zmiana pogody, audyt w takcie linii (do 8 partii AI w blocie + gracz: model wprost bez blota i z blotem, osobno pada / mokro), linie
  `Bloto (BL): latka pamieci ...`, `Bloto (BL): start sesji ...`, `Bloto (BL): dzien N ...`. Nic w zapisie gry (zapis bez zmian).
- KLUCZE NOWE (grupa MCM "The marching column", przy kluczach drog): `MapMudSpeedPenalty` (TAK), `MapMudSpeedPercent` (15, int, 0..50),
  `MapMudWetSpeedPercent` (7.5, float, 0..50; zakresy suwakow w `tools/gen_mcm.py`). Wylaczony klucz = sciezka sprzed zmiany (Speed i postfiks wychodza od razu).
- Wpiecia: `MarchPace.cs` (1 linia), `MapRoads.cs` (nowe metody `KindAt`, `Recording` + 1 linia `MapMud.ApplyCache` w ApplyAll), `ArmouryBehavior.cs`
  (godzina, start sesji po MapRoads.SessionStart, doba po MapRoads.Daily), `Settings.cs` (3 pola po MapSideRoadSpeedPercent), `McmSettings.cs` (gen_mcm),
  `tools/gen_mcm.py` (zakresy suwakow).

**Ryzyko / co sprawdzic (kontrola calosci).**
- CZESTOSC BLOTA ZALEZY OD PORY NIEBA. Czynnik deszczu gry (`CalculateTimeFactorForRain`) = 1 od polowy jesieni przez cala zime i maleje wiosna do 0 latem;
  WesterosClimate rozciaga pore nieba na lata (u Jeffa pierwsza jesien 300-450 dni, potem zima 3-5 lat). W krainach deszczowych, w ktorych snieg nie przejmuje
  pogody (poludnie), "pada" bywa wtedy do 45% czasu, a "mokro" do dalszych 52%. Srednia kara poza droga w najbardziej deszczowej krainie: ok. 10.6%
  (0.45 x 15 + 0.52 x 7.5), przy teksturze deszczu 0.8 ok. 7.6%, przy 0.7 ok. 4.7%. Latem prog deszczu jest wysoki (tekstura >= ok. 0.76), wiec blota
  malo. Wolniejsze dostawy wozow wsi i karawan w deszczowa zime sa zamierzone; gdyby linia doby pokazala za duzo (np. > 30% partio-godzin w blocie
  przez wiele dni), obnizyc `MapMudWetSpeedPercent` albo `MapMudSpeedPercent`.
- AI nie wybiera drog (R3-07d osobno), wiec bloto spowalnia glownie lordow, karawany, wozy wsi i bandy.
- Bez podwojnej kary: RBL Seasons (Flood / SpringStorm) wylaczone na twardo (CrashScribe Mends `SeasonsOff`), ROT i BEE nie licza pogody w predkosci
  (dekompilacja). Zostaje kara gry "Cavalry weather penalty" (mokro: premia jazdy -30%, w bazie, na drodze tez) - zwykle i tak pod sufitem kolumny.
- Zmiana kluczy blota w MCM w trakcie gry: stara predkosc partii zostaje do nastepnego przeliczenia (pogoda, sciana siatki, noc, droga) - jak premia drogi.
- Burza NavalDLC (`Storm`) nad ladem = "pada" (gra w bitwie tez daje wtedy deszcz). Zasieg zdarzenia `Storm` (1.25 x promien burzy) i zasieg "mokro"
  burzy w pamieci gry sa rozne - nasz postfiks pamieci pogody lapie oba przejscia.
- Pomiar E7/T (TerrainEase, srednie predkosci las / rownina) w dobach deszczu dostaje szum z blota (obie grupy poza droga) - tylko log.
- Audyt drog (Drogi (DR)): gracz poza droga w deszczu ma bloto w obu liczeniach (bez premii i z premia) - linia drog bez zmian znaczenia.
- Zamknieta ekonomia: bez zlota i towarow. Zapis: bez zmian.

**Status.** DO SPRAWDZENIA (build Armoury Release: kod 0, 0 Error(s); autotestu nie uruchamialem - zakaz w zadaniu).

**Decyzje (moje, Jeff: "ekonomie projektuje Claude").**
- Dwa stopnie: pada 15% ("Mud"), mokro po deszczu 7.5% ("Wet ground"). Bloto po deszczu jest prawdziwe (ziemia schnie), ale "mokro" w grze trwa do doby
  i w deszczowa zime obejmuje wiekszosc czasu - polowa kary trzyma srednia w ryzach (ok. -10.6% zamiast -14.6% w najbardziej deszczowej krainie).
- Bez osobnej kary za pore roku: pora wchodzi tylko przez czestosc deszczu gry (jak w swiecie: wiecej deszczu jesienia i zima).
- Burza NavalDLC nad ladem = pada (pelna kara).
- Droga boczna do wioski chroni przed blotem tylko wtedy, gdy liczy sie w premii (MapSideRoadSpeedBonus) - jedna zasada "gdzie jest droga" dla premii i blota.
- Bez siatki drog (MapRoadSpeedBonus wylaczony / brak pliku) bloto NIECZYNNE, a nie "wszedzie" - inaczej kara objelaby tez trakty.

**Recenzja (PELNA, 20.10) - co zrobione.**
- WAZNE (LightRain = "mokro do doby", czestosc deszczu z pory nieba, kara przez lata): PRAWDZIWE - potwierdzone w dekompilacji 1.4.8
  (`SetIsRainingOrWetFromFunction`: 5 okresow po 4 h wstecz; szansa ulewy 0.45 x (rainValue - 0.6) / 0.4; `GetSnowAndRainDataForPosition`:
  prog Lerp(0.7, 0.3, czynnik deszczu); `CalculateTimeFactorForRain` = 1 przy postepie roku > 2.5). Poprawione: wariant (b) recenzji - mokro polowa kary
  (nowy klucz `MapMudWetSpeedPercent` 7.5), osobne wpisy "Mud" / "Wet ground", w linii doby pora nieba, postep roku nieba i czynnik deszczu gry oraz
  partio-godziny w blocie osobno pada / mokro. Pomiar w autotescie z jesienia/zima nieba - patrz "Test w grze" (proba zimy wariant b).
- DROBNE (burza NavalDLC: przejscie Wet -> Wet bez przeliczenia): PRAWDZIWE i szersze - ten sam brzeg dotyczy ulewa <-> mokro przy dwoch stopniach kary
  i snieg <-> deszcz. Poprawione w calosci wlasnym postfiksem pamieci stopnia pogody (`MapMud.CachePostfix`), wiec nie trzeba bylo zawezac reguly do
  `GetWeatherEffectOnTerrainForPosition == Wet`. Brzeg snieg <-> deszcz z poprzedniej wersji tez zniknal.

**Test w grze / log_grep.**
- `Bloto (BL): latka pamieci predkosci MobileParty.IsLastSpeedCacheInvalid (stopien pogody` ... `wpieta`.
- `Bloto (BL): start sesji` - klucz tak, `pada ... 15% (wpis Mud), mokro po deszczu ... 7.5% (wpis Wet ground)`, siatka wczytana, latka MarchPace w N > 0
  modelach, `latka pamieci pogody wpieta`, model pogody, `kara CZYNNA`.
- `Bloto (BL): dzien` - `pora nieba ...`, `czynnik deszczu gry ...`, partio-godziny `w blocie ... (pada ... mokro ...)`, `z kara blota` > 0 (jesli pada),
  `pamiec pogody: przeliczenia wymuszone zmiana pogody (ok.)` > 0 w dobach deszczu, audyt `kara rzeczywista sr.: pada 15.00%`, `mokro 7.50%`,
  `zgodnosc z kara stopnia sr. 100.0%`, `bez wpisu Mud/Wet ground 0`, `pamiec gry zgodna n/n`, `potkniecia dzis 0`, koszt sciezki i postfiksu rzedu ulamka us.
- Zwykly autotest (letni start) i DRUGI z jesienia/zima nieba: nowa kampania z plikiem `armoury-proba-zimy.flag` (WinterTrial wariant b: lato 1 dzien,
  pierwsza jesien 7 dni). Plik `armoury-zima-teraz.flag` NIE wystarczy - zmienia tylko pore ekonomii, nie nieba. W probie zimy sprawdzic: `pora nieba Autumn/Winter`,
  `czynnik deszczu gry 1.00` (od polowy jesieni) i udzial partio-godzin w blocie.
- W grze: gdy pada, poza droga linia "Mud" w dymku predkosci (ok. -15%, kolumna 3.00 -> 2.55); po deszczu linia "Wet ground" (ok. -7.5%, 3.00 -> 2.78);
  na trakcie linie znikaja i jest "Road".
