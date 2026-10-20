## m96-valyria - drogi Valyrii i szlaki karawan Essos (Armoury)

**Dla Jeffa (prosto):** w Essos dawne drogi Valyrii - demon road z Volantis przez Mantarys do Meereen (z odnoga do Tolos) oraz droga z Pentos przez
Ghoyan Drohe i Norvos do Qohor - sa teraz osobnym rodzajem drogi: kazda partia ladowa jedzie po nich o **25%** szybciej (zwykly trakt +15%), a w
podpowiedzi predkosci widac wpis **Valyrian road**. Tam, gdzie farba ROT na tych trasach sie urywa (pod miastami, przy brodach), premia idzie dalej
prosto przez przerwe (7 krotkich "mostow", razem ok. 27 jedn.). Malowany trakt w glebi Essos (Sarnor: Kyth - Saath) liczy sie jak szlak karawan
(+7.5%, wpis **Caravan trail**), bo lore nie zna tam drog; Westeros bez zmian. Wyglad drog sie nie zmienia (farba ROT to warstwa terenu - opis nizej).
Wylaczniki: MCM Armoury -> **Map Valyrian Roads** (i **Map Valyrian Road Speed Percent**), **Map Essos Caravan Trails** - wylaczone = jak dzis.

**Problem:** Jeff 20.10: "Rob drogi Valyrii i trakty Essos"; PLAN 7.5 "drogi do wiosek i Valyrii"; audyt `docs/audyt-2026-10-09/08-drogi-wioski-widok.md`
rozdz. 2 (lore) i 3.8 ("gesta siec traktow takze na stepach i w glebi Essos - graf twierdz"). Dzis kazda droga Essos, takze droga Valyrii, daje to samo
+15% co zwykly trakt Westeros, a premia ma dziury tam, gdzie farba ROT sie urywa.

**Dowod (liczby, nie zalozenia):**
- Plik drog moda `arm_map_roads_geo.tsv` (farba ROT, 311 lamanych, 10016.3 jedn.) - w Essos 2797 jedn.; po kulturze najblizszej twierdzy (ROT-Map
  `settlements.xml`): khuzait (stepy Dothrakow) **0** jedn. farby, ibbenese / yiti / qartheen **0**, sarnor **161** (lamana 45 Kyth - Saath, 166 jedn.).
  "Graf twierdz na stepach" z audytu 3.8 to prototyp `arm_map_roads.tsv` z 08.10 - w grze go nie ma.
- Drogi boczne (plan f19, podglad `tools/drogi/boczne-podglad` = ten sam kod co gra; w logu 12-25-19 WGRANIA 95: drog 2446, 30463 jedn.): w stepie khuzait
  783 jedn. w 37 drogach do wiosek, sarnor 699, ibbenese 502 - to drogi do wiosek z premia +7.5% ("Side road"), czyli juz dzis poziom szlaku karawan.
- Log WGRANIA 95 (Armoury-2026-10-20_12-25-19.log): `Drogi (DR): start sesji ... premia 15% ... odcinkow 10565` - jeden rodzaj traktu dla calej mapy.
- Lore: awoiaf "Valyrian roads" / "Demon road" (strona 403 dla WebFetch; trasy z wyszukiwarki i z audytu 09.10): drogi z topionego kamienia, proste,
  pol stopy nad gruntem, od osrodka handlu do osrodka; demon road Volantis - Mantarys - (ruiny Bhorash) - Meereen z odnoga do Tolos; Pentos - Ghoyan Drohe
  - Norvos - Qohor. Selhorys - Volantis: zrodlo nie potwierdza - zostaje zwyklym traktem.
- Trasy po farbie (generator, Dijkstra po punktach lamanych; konce laczone do 0.6 jedn. jak siec AI drog): z samymi rozstajami 0.6 **4 z 6 odcinkow tras
  bez polaczenia** (Mantarys - Meereen, Mantarys - Tolos, Ghoyan Drohe - Norvos, Norvos - Qohor), Pentos - Ghoyan Drohe objazdem 192 jedn. zamiast 120 -
  farba urywa sie 2-7 jedn. pod miastami i przy brodach (w Essos kilkadziesiat koncow lamanych z przerwa 0.6..12 jedn.). Przy przerwach do 8 jedn. (most x1.5 w
  koszcie) wszystkie trasy sie zamykaja: Volantis - Mantarys 136.5 (prosta 123.9), Mantarys - Meereen 260.3 (184.6), Mantarys - Tolos 81.0, Pentos - Ghoyan
  Drohe 120.5 (99.4), Ghoyan Drohe - Norvos 172.4 (129.8), Norvos - Qohor 171.7 (136.2); razem V 823.4 jedn. (831 odcinkow) + 7 mostow 26.8 jedn.

**Przyczyna:** `MapRoads` zna dwa rodzaje: farba ROT (2, trakt, MapRoadSpeedPercent) i droga boczna (1); plik drog nie niesie rodzaju, a AI drog
(`AiRoadRoutes`) liczy kazda krawedz farby premia traktu. Przerwy farby (lamane sondy geografii urywaja sie pod osadami) nie sa krawedziami ani
odcinkami premii.

**Zmiana (Armoury):**
- NOWY plik danych `Armoury/ModuleData/arm_map_roads_valyria.tsv` z generatora `tools/drogi/drogi_valyria.py` (python -I, biblioteka standardowa): odcinki V
  (droga Valyrii: najkrotsza trasa po farbie miedzy miastami trasy), C (szlak karawan: odcinek farby, ktorego najblizsza twierdza ma kulture glebi Essos -
  khuzait, sarnor, ibbenese, yiti, qartheen; 162 odcinki, 160.7 jedn.) i B (most Valyrii przez przerwe farby do 8 jedn.). Naglowek `roads_md5` = md5
  tekstu pliku drog bez '\r' - inny plik drog w grze = rodzaje nieczynne z powodem w linii startu (wszystko trakt).
- NOWY `Armoury/src/MapRoadKinds.cs`: wczytanie (raz na sesje, watek glowny, w `MapRoads.Load`), kolejnosc odcinkow siatki V, mosty, trakty, szlaki
  (w komorce wygrywa pierwszy trafiony - droga Valyrii wygrywa na rozstajach, trakt nad szlakiem), rodzaj czynny z kluczy (`Eff`), linie logu, audyt.
- `MapRoads.cs` (wpiecia): `Grid.Cls` + przedzial mostow; `KindS` (Kind z trafionym odcinkiem; most liczony tylko przy MapValyrianRoads); `SpeedRoad`:
  Valyria +max(MapValyrianRoadSpeedPercent, MapRoadSpeedPercent)% wpis "Valyrian road", szlak +MapSideRoadSpeedPercent% wpis "Caravan trail", sufit
  kolumny w gore o ten sam procent (jak dotad); pamiec partii stan 3 bity (4 Valyria, 5 szlak - zmiana rodzaju = przeliczenie predkosci); `KindAt` BEZ ZMIAN
  (2 dla kazdej farby) - bloto (MapMud) i spisy "na trakcie" licza jak dotad; nowe `KindClassAt` / `ValPctNow` / `CarPctNow` / `ProbeFor`; audyt DR bierze tylko
  zwykle trakty (inna premia = audyt w linii VR); pola (RectHit) pomijaja mosty (to nie farba).
- `AiRoadRoutes.cs` (wpiecia): klasa krawedzi (`AdjCls`; granice klas wymuszone jako wezly sieci), waga w Dijkstrze z rodzaju czynnego (Valyria 1/1.25,
  szlak 1/1.075, most bez klucza = poza droga), mosty Valyrii jako krawedzie sieci (przy kluczu na starcie budowy sieci), koszt przejazdu `RideCost` z
  `KindClassAt` (Valyria / szlak licza sie do "drogi na trasie" jak trakt).
- Klucze NOWE (domyslnie TAK, `Settings.cs`, `gen_mcm.py` - suwak 0..50): **MapValyrianRoads**, **MapValyrianRoadSpeedPercent** (25), **MapEssosCaravanTrails**.
  Wylaczone = sciezka sprzed zmiany (trakt, bez mostow - most bez klucza jest pomijany w KindS; w sieci AI waga poza droga).
- `ArmouryBehavior.cs`: jedna linia `MapRoadKinds.Daily()` po `MapRoads.Daily()`. `tools/sprawdz_logi.py`: grupa **m96valyria** (VR-0..VR-6).
- Uzasadnienie 25%: trakt +15% to zwykla droga sredniowieczna; audyt 08 rozdz. 2: w suchy dzien droga daje +10-25%, brukowana rzymska trzyma tempo w
  kazda pogode (legion 20-25 mil na dobe po bruku, ok. 8 po drodze gruntowej). Droga Valyrii (topiony kamien, nad gruntem) to najlepsza droga swiata -
  gorna granica suchego dnia, 25%; po deszczu przewaga rosnie sama (bloto MapMud tylko poza droga). Ponizej traktu nigdy (max z MapRoadSpeedPercent).

**Wyglad (NIE w tej zmianie):** drogi ROT to farba warstwy `road` w `terrain.bin` - gra nie daje zmiany koloru farby w biegu. Ciemniejszy pas da sie
zrobic tylko nakladka decali (jak drogi boczne: stemple `mainmap_decal_undervillage` z ciemnym kolorem, np. FF4A4744, alfa ok. 0.5, w komorkach f22 -
bez pracy co klatke): ok. 830 jedn. tras V = ok. 2000 stempli (drogi boczne maja 73 941). Wymaga koloru na stempel w `MapSideRoads.Chunks.cs` (dzis jeden
kolor na plan) i zdjec w autotescie (Jeff 14.10 o pierwszej probie drog: "drogi wygladaja fatalnie") - do decyzji, osobna paczka.

**Ryzyko / co sprawdzic (kontrola calosci):**
- Regresje: `KindAt` bez zmian (bloto, AI spis "na trakcie", MapMud audyt); linie `Drogi (DR)` bez zmian formatu (ndrogi: NDR-0 "odcinkow 10565" =
  odcinki farby, mosty poza ta liczba; siatka "wpisow" / "maks. na komorke" rosnie o mosty). NDR-5 audyt tylko zwyklych traktow. NDR-6 (gracz na drodze):
  gracz na drodze Valyrii pokaze 25% zamiast 15% - w autotescie gracz stoi w Westeros (ryzyko male; wpis "Valyrian road" liczony jako wpis drogi).
- Kolizje: premia drogi Valyrii i drogi bocznej sie nie sumuja (pierwszy trafiony odcinek; trakty przed bocznymi jak dotad). Karczmy / sen (TownInns,
  NightRest) dalej po nas - procent od wyniku jak przy trakcie. Szubienice (rozstaje z pliku drog), zajazdy (plik `arm_roadside_inns*.tsv`), pola (p6-pola)
  i plan drog bocznych - ta sama farba, bez zmian.
- Watki: `KindS` / `Eff` czytaja tablice niezmienne po publikacji i `Settings.Current` (jak dotad); liczniki VR bez blokad (przyblizone, jak DR); bez alokacji.
- Zapis gry: nic nie zapisywane - zapis Jeffa z 94/95 wczytuje sie bez zmian.
- Straznik: inny `arm_map_roads_geo.tsv` = rodzaje nieczynne (linia VR-0 mowi dlaczego); po zmianie pliku drog uruchomic `python -I tools/drogi/drogi_valyria.py`.
- Klucz Valyrii przelaczony w trakcie gry: premia od razu; mosty w sieci AI dopiero od nastepnej sesji (siec budowana raz).

**Status:** DO SPRAWDZENIA (build Armoury kod 0; gra nieuruchomiona - Jeff gra; nic nie wgrane).

**Test w grze (autotest WGRANIA 96, 120 / 160 dob):** `python -I tools/sprawdz_logi.py --grupa m96valyria <Armoury-*.log>` - oczekiwane: VR-0 TAK
(`Drogi Valyrii (VR): start sesji ... drogi Valyrii 831 odcinkow (823.4 jedn.) + mosty 7 (26.8 jedn.), szlaki karawan 162 odcinkow (160.7 jedn.) ... stan
CZYNNE`), VR-2 partio-godziny na drodze Valyrii > 0 (szacunek 0.3-2% partio-godzin w marszu: V to 8.2% farby, na farbie lezy ok. 9% punktow tras AI),
VR-3 premia rzeczywista sr. 25.0 +-1.5 pp z wpisem "Valyrian road", VR-5 `siec AI drog: mosty Valyrii 7` (albo mniej, gdy silnik nie znalazl sciezki przez
przerwe), VR-6 0. Do tego `--grupa ndrogi` bez nowych NIE. W grze: podpowiedz predkosci na demon road (np. miedzy Volantis a Mantarys) - wpis "Valyrian road
+25%"; na Kyth - Saath "Caravan trail".
Linia do grep: `Drogi Valyrii (VR): dzien` (doba) i `Drogi Valyrii (VR): start sesji`.
