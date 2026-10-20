## p6-pola (20.10) - pola wokol wiosek na mapie wedlug pory roku (Armoury)

**Dla Jeffa (prosto).** Wokol wiosek na mapie leza teraz pola (ma je prawie kazda wioska - bez pol zostaja tylko te otoczone woda, bagnem albo stokiem).
Na poczatku kazdego roku pola sa zaorane i brazowe, potem zielone, w tygodniach zniw zolte, a po zniwach zostaje sciernisko.
Zniwa na polach to te same zniwa, z ktorych zyja wsie (dni 196-252 roku, tylko w lato albo jesien Westeros), wiec po kolorze widac, kiedy zbiera sie plon.
Gdy przyjdzie zima, pola na Polnocy i w innych zimnych krainach znikaja pod sniegiem; w Dorne i Essos sa zaorane i puste, a na Wyspach Letnich zostaja zielone.
Pola nie wchodza na drogi.
Wioska zlupiona ma pola czarne i spalone. Wioska, ktorej czesc osad opustoszala po rabunku albo glodzie, ma czesc pol odlogiem (szarobure).
To tylko obrazek: ekonomia, trasy i zapis gry sie nie zmieniaja. Wylacznik w MCM: "Map Village Fields".

**Problem / zrodlo pomyslu.** POMYSLY-35 pkt 18 (R3-07b), Jeff 20.10: "28 - nie, 29 - nie. Pozostale tak", potem "Rob od razu fale 2". Audyt drog i widoku 09.10
(`docs/audyt-2026-10-09/08-drogi-wioski-widok.md`, tabela 4.4 N-3, "Dla Jeffa" pkt 8): pola wokol wiosek zmieniajace wyglad z pora roku - pokazac zniwa i glod
bez liczb; technika jak D-1 (decale na mapie). Dotad wokol wiosek nie bylo zadnych pol (poza 28 decalami pszenicy, ktore ROT-Map ma przy kilku wsiach).

**Zmiana.** Nowy plik `Armoury/src/MapFields.cs` + minimalne wpiecia:
- `MapFields` (statyczna, bez typow SandBox.View): klucz, pora pol swiata (`Phase`), rejestracja widoku z ochrona przed bledem ladowania typu (3 proby, jak drogi boczne);
- `MapFieldsView` (komponent mapy, Priority 107 - po drogach bocznych): lista wiosek, plan pol, komorki, decale, pora i stan wsi, linie logu;
- `SubModuleMain.OnApplicationTick`: jedna linia `MapFields.AppTick()` zaraz po rejestracji drog bocznych (przed wylacznikiem wiosek - pola wsi gry sa tez
  bez wiosek ukladu; po wznowieniu przeniesiona sprzed `if (_mapVillagesLoadFails ...)`, bo w to samo miejsce dopisuje swoj blok galaz p6-szubienice);
- `MapRoads.cs`: linia `GridNow` (odczyt referencji siatki drog - zmiana siatki = pola od nowa, zeby nie lezaly na drodze), po recenzji `PaintNear` (jedna
  linia obok `GridNow`) i metoda `Grid.RectHit` z pomocnikami `SegBoxHit` / `Clip` / `PtBox2` na koncu klasy `Grid` (test prostokata pola wobec pasa farby drogi);
- `Settings.cs`: nowa sekcja "Fields around villages" z kluczem `MapVillageFields` (NOWY, domyslnie TAK) + `tools/gen_mcm.py` (McmSettings.cs);
- `tools/sprawdz_logi.py`: grupa `--grupa p6pola` (progi PP-0..PP-6, funkcje `p6pola_phase`, `p6pola_main`).

Jak dziala:
- **Naklejka.** Material pola pszenicy gry `decal_production_farm_wheat_a/b/c/d` (Native `map_icon_parts.xml`, prefab `map_icons_production_farm_wheat_*`;
  w `materials.tpac`: shader modulate, use_albedo, **render_on_terrain** - maluje tylko teren, nie domy ani drzewa). Cztery materialy = cztery cwiartki atlasu
  (argument jak w prefabie gry), wybor z hasha - pola nie sa jednakowe. Decale BEZ encji (wzor drog bocznych i piany NavalDLC: `Decal.CreateDecal` -> `SetMaterial`
  -> `Scene.AddDecalInstance(d, "editor_set")` -> `Frame`; zdjecie `RemoveDecalInstance`). Kolor pory = `SetFactor1` na zywym decalu (tak gra barwi tropy partii -
  `MapTracksVisualManager.ParallelUpdateTrackColors`, dekompilacja SandBox.View 1.4.8) - tylko przy zmianie wygladu pola.
- **Kto ma pola (plan dla kazdej wsi rolniczej).** Kazda wies gry (rybacka / gornicza 2 pola, reszta 4) i kazda wioska ukladu Armoury (poziom 1/2/3 = 2/3/4 pola, mlyn /
  wiatrak / zagroda / spichlerz +1, rybacka -2, najmniej 1). Bez pol: przysiolki na pustkowiach (kolumna hamlet - pasterze, drwale, weglarze, mysliwi, rybacy) i wsie,
  wokol ktorych nie ma ladu uprawnego (symulacja: ok. 1.4% wsi; linia "stan" liczy je: "bez zadnego pola").
- **Gdzie.** Pierscien wokol obrazka (pol obrysu + pol pola + 0.2 jedn.; pole wioski ukladu ok. 1.5 x 1.9 jedn., wsi gry ok. 2 x 2.6), pola co 360/K st. od frontu
  wioski. Pole odrzucone, gdy: za blisko miasta (6 jedn.) / zamku (4), nachodzi na inne pole wioski, nie lezy po swojej stronie granicy z sasiednia wioska
  (polplaszczyzna z zapasem - pola dwoch wiosek nie wchodza na siebie) albo wchodzi na obrazek sasiada, teren nie uprawny (sciana siatki: Plain, Desert, Forest,
  Steppe, RuralArea - bez sniegu wiecznego, bagna, wydm, plazy i wody), farba drogi (`MapRoads.PaintNear` - po recenzji: caly prostokat pola wobec pasa traktu albo
  drogi bocznej, pol-szerokosc farby + 0.2 jedn.), stok (rozrzut wysokosci rogow > 0.7 albo normalna < 0.85). 10 prob miejsca na pole (kat +-0 / 0.45 / 0.9 rad na
  dwoch odleglosciach), potem bez tego pola (liczone w logu wedlug przyczyny). Wioska bez zadnego pola po 10 probach - pierscien zapasowy (24 katy x 3 odleglosci,
  do K pol).
- **Pora pol (raz na dobe gry, wspolna dla swiata; rok 364 dni, inny - proporcjonalnie).** Dni 0-69 zaorane (siew), 70-195 zielone, okno zniw FreeHands
  (`FreeHands.HarvestWindow` - ta sama regula co rece do zniw i plon): 196-223 dojrzale, 224-252 sciernisko; 253-299 sciernisko (w roku wiosny Westeros - bez zniw -
  zielone), od 300 zaorane. Zima Westeros (pora NIEBA `WesterosClimate.SkySeason` - ta, ktora sypie snieg na mapie): kraina zimna (`WinterRealm.OfVillage` = 1) -
  decal zdjety ze sceny (pole pod sniegiem), ciepla (< 1: Dorne, Essos) - zaorane i puste (wyglad "zaorane"), Wyspy Letnie (0) - zielone.
- **Stan wsi (co godzine gry, tylko wioski w scenie).** Wies gry rabowana albo zlupiona (`VillageState` BeingRaided / Looted) - wszystkie pola spalone. Wioska
  ukladu - ksiega wiosek (`VillageBook.LookOf`): spalona - spalone; puste osady (rabunek + glod) - taki udzial postawionych pol odlogiem, jaki udzial osad pusty
  (po recenzji: od pol naprawde postawionych, nie od planowanych); ksiega czytana tylko
  przy zmianie jej numeru wygladu (`LookVersion`) i przy wejsciu wioski do sceny. Bez ksiegi - stan wsi gry okregu.
- **Wydajnosc.** Plan pol wioski dopiero przy pierwszym wejsciu jej komorki (32 x 32 jedn.) w kolo widoku kamery (do 10 prob miejsca x 5 sond terenu + test pasa
  drogi na pole; pierscien zapasowy najwyzej 72 proby, tylko wies bez pola), potem
  w pamieci. W scenie tylko pola komorek w kole widoku (to samo kolo co drogi boczne - `MapSideRoads.ViewCircle`, histereza 16 jedn.); kamera wyzej niz
  min(MapVillagesHideAboveCameraHeight, 110) - zadnego pola. Praca 4 razy na sekunde, budzet 4 ms na cwierc sekundy (najpierw zdejmowanie, potem stawianie);
  co klatke nic (CLAUDE.md 7). Oczekiwane: ok. 2.4 tys. wiosek z polami, ok. 7-8 tys. pol w planie na calej mapie, w scenie zwykle kilkaset (kolo ok. 80-130 jedn.).

**Ryzyko / co sprawdzic (kontrola calosci).**
- Regresje / kolizje: nowy widok obok drog bocznych (106) i wiosek (100); nie dotyka ich stanu ani decali. `MapRoads.GridNow` tylko czyta referencje (volatile).
  `WinterRealm.OfVillage`, `VillageBook.LookOf`, `FreeHands.HarvestWindow`, `WesterosClimate.SkySeason` - same odczyty z watku glownego (pod ich wlasnymi zamkami).
  Ekonomia, AI, zapis: bez zmian (zero przeplywow zlota i towaru - ksiega Obieg bez nowych pozycji).
- Wyglad NIESPRAWDZONY w grze: kolor przez `SetFactor1` mnozy barwe tekstury. Dowod, ze barwienie dziala na tym rodzaju naklejki: material pola
  (`decal_production_farm_wheat_a`) ma w `materials.tpac` ten sam shader `modulate:b` + use_albedo + render_on_terrain co strzalka tropow gry `map_track_arrow`,
  ktora gra barwi wlasnie `SetFactor1` (SandBox.View `MapTracksVisualManager`, kolor z `DefaultMapTrackModel.GetTrackColor` - `Vec3.ToARGB`, ten sam format co
  `LookColor`). Jesli tekstura pola jest bardzo ciemna albo barwy wyjda za slabe, pory beda slabo widoczne - wtedy poprawic `MapFields.LookColor` (jedna tablica).
  Pola pod drzewami lasu (Forest dozwolony) - maluja ziemie pod drzewami (jak polana).
- Wielkosc i odstep sprawdzone wobec ROT: ROT-Map (`SceneObj/Main_map/scene.xscene`) ma ok. 28 takich naklejek jako dzieci encji wsi - srodki 2.1-3.5 jedn. od
  srodka wsi, skala 1.0-1.7 (prefab: naklejka o skali 1 = 2 x 2 jedn.). U nas wies gry: srodek pola 3.65 jedn. od srodka, pole ok. 1.8-2.0 x 2.1-2.7 jedn. - ta sama
  wielkosc co pola ROT. Przy tych kilkunastu wsiach ROT nasze pola leza obok pol ROT (moga sie czesciowo nakladac - obie to ta sama tekstura pszenicy; nie zdejmuje
  pol ROT - to dane sceny ROT).
- Drogi: test farby drogi czyta odcinki z siatki drog `MapRoads` (trakty ROT + drogi boczne, gdy widok drog bocznych opublikowal plan - niezaleznie od
  `MapSideRoadSpeedBonus`, `MapRoadWideField`, `MapRoadWideMargin`). Siatki nie ma przy `MapRoadSpeedBonus` = NIE (plik drog niewczytany) - wtedy pola moga lezec
  na drodze (u Jeffa TAK). Siatka zmieniona (publikacja drog bocznych chwile po wczytaniu) - pola zdjete i plan od nowa raz na zmiane (linia logu; liczniki planu
  od zera).
- Gesto ustawione wioski ukladu (odstep 4.5 jedn.) maja pola tylko po stronie z dala od sasiadow; w bardzo gestych skupiskach wioska moze zostac bez pol (log: "sasiad").
- Obrazek wioski przesuniety przez widok wiosek (na lad, ze stoku) nie przesuwa pierscienia pol (plan liczy od polozenia z pliku ukladu - tak samo jak drogi boczne).
- Liczba decali: kilkaset w scenie obok ok. 2 tys. decali drog bocznych; przy klopocie z plynnoscia - klucz NIE (pola zdjete od razu).
- Watki: caly kod na watku glownym (tick widoku mapy) - nic nie wolaja watki AI.
- Scalenie: `git merge-tree` galezi z n22-calosc (4885ae75 - 18 galezi fali 1) i z galeziami fali 2 (p6-szubienice, p6-drogi, p6-sztormy, p6-a7, p6-spichlerz,
  p6-zakladnicy) oraz drzewami n22f-* - 0 konfliktow; drzewo scalone z n22-calosc zbudowane osobno (Armoury Release: kod 0, 0 Error(s)). n22-calosc i n22f-* nie
  ruszaja `MapRoads.cs` ani `SubModuleMain.cs`; uzyte odczyty (FreeHands.HarvestWindow / HarvestFrom / HarvestTo, WesterosClimate.SkySeason, WinterRealm.OfVillage,
  VillageBook.LayoutCrcNow / LayoutRecords / LookOf / LookVersion, MapSideRoads.ViewCircle, siatka MapRoads) sa w n22-calosc bez zmian. Priorytet widoku 107 - wolny
  (wioski 100, zajazdy 101, szubienice p6 102, proba drog 105, drogi boczne 106).

**Wznowienie (20.10, po "wstrzymaj fala 2" i "zacznij fale 2").** Poprzedni agent zostawil 2 commity (3cbe284d kod, 032e6cb5 grupa progow + ten wpis), drzewo czyste.
Przejrzany caly kod `MapFields.cs` wobec wzoru drog bocznych (ta sama kolejnosc CreateDecal -> SetMaterial -> AddDecalInstance -> Frame, zdjecie, rejestracja,
OnFinalize), materialy i argumenty atlasu z prefabow gry (zgodne z `map_icon_parts.xml`), numery scian siatki (`TerrainType` 1.4.8: Plain 1, Desert 2, Snow 3,
Forest 4, Steppe 5, RuralArea 14, Swamp 15, Dune 16, Beach 20 - zgodne), kolejnosc `CampaignTime.Seasons` (Spring 0 .. Winter 3 - zgodna z `SeasonName`).
Snieg wieczny bez pol - spojne z ekonomia (VillageCrafts: "snieg bez upraw"). `gen_mcm` bez zmian w McmSettings, `--grupa p6pola` sprawdzona na logu probnym
i na logu wzorcowym 160 dob (INFO - log sprzed galezi). Jedyna zmiana kodu: przeniesienie linii wpiecia w SubModuleMain (wyzej).

**Recenzja (20.10, dwie soczewki - "calosc": 0 krytycznych, 1 wazne, 4 drobne; wszystkie prawdziwe, wszystkie poprawione).**
- WAZNE - test drogi za szeroki (prawda): `MapRoads.KindAt` liczy pas `Half + MarginOf(s)`, a przy `MapRoadWideField` TAK (domyslnie, Jeff bez klucza w
  Armoury.json) margines to `MapRoadWideMargin` 1.0 - pole odrzucane w pasie 1.25 jedn. od osi drogi bocznej, ktorej farba ma 0.25; do tego uklad pol zalezal od
  kluczy premii. Poprawka: `MapRoads.PaintNear(_gridRef, ...)` -> `Grid.RectHit` - DOKLADNY test prostokata pola (obrot jak decal) wobec kazdego odcinka siatki
  (trakt i boczna) z promieniem `Half + 0.2`: odcinek przecina prostokat (Liang-Barsky) albo koniec odcinka / rog prostokata blizej niz promien. Bez marginesu
  premii i bez kluczy premii; 5 sond `KindAt` usuniete (sondy nie widzialy drogi przechodzacej miedzy rogami). Komorka srodka pola wystarcza (wpis siatki siega
  `Half + MaxMargin 2` + pol przekatnej komorki; przekatna/2 pola + 0.2 <= 1.92), przy wiekszym polu tez komorki rogow. Sprawdzenie: `Grid.RectHit` wyciety z
  pliku i porownany z brutalnym liczeniem (81 x 81 punktow prostokata, 20 tys. losowych przypadkow): 0 bledow (p6-pola-tmp/rt).
- DROBNE - waskie szukanie miejsca (prawda): 6 prob +-0.45 rad. Poprawka: 10 prob (+-0.9 rad) i pierscien zapasowy dla wsi bez zadnego pola (24 katy x 3 odleglosci,
  do K pol); w linii "stan" nowe liczniki "bez zadnego pola" i "pierscien zapasowy: wsi, pol"; "bez miejsca" = pola planu ostatecznie bez miejsca; przyczyny = pola
  odrzucone w 10 probach. Napisy "100%" / "wokol kazdej wioski" zmienione.
  Symulacja (sim recenzji przerobiona na test prostokata - p6-pola-tmp/sim/sim_p6*.py; wsie z ROT-Map settlements.xml, wioski z arm_map_villages.tsv, teren z rastra
  teren.npz, drogi promieniowe z losowym katem jak w recenzji, 2368 wsi, 7324 pola planu):
  | wariant | drogi (uklad / wies gry) | pol postawionych | wsi bez pola |
  |---|---|---|---|
  | przed recenzja (margines 1.0, 6 prob) | 0 / 0 | 90.5% | 3.0% |
  | przed recenzja | 1 / 2 | 72.1% (droga 1224) | 6.0% |
  | przed recenzja | 2 / 3 | 57.5% (droga 2268) | 8.7% |
  | po recenzji (farba + 0.2, 10 prob, pierscien) | 1 / 2 | 92.4% (droga 146) | 1.4% |
  | po recenzji | 2 / 3 | 88.8% (droga 382) | 1.7% |
- DROBNE - odlogiem od K (prawda): `BookFallow = round(K * q)` po indeksach listy krotszej od K. Poprawka: zapamietany udzial `BookQ`, odlogiem =
  `round(Fields.Length * BookQ)` przy kazdym odswiezeniu stanu (tez po planie od nowa).
- DROBNE - PP-5 bez przeliczenia roku (prawda): C# liczy pore z dnia przeliczonego na rok 364, log pisal dzien surowy. Poprawka: linie startu, zmiany pory i stanu
  maja "rok N dni"; `p6pola_phase` przelicza tak samo (round - zaokraglenie do parzystej jak Math.Round). Log probny: dzien 99 z 168 -> 214 -> "dojrzale" (TAK).
- DROBNE - napisy (prawda): (a) cieple krainy w zimie maja wyglad "zaorane" - zostaje w kodzie (ugor "odlogiem" to w tym widoku znak pustych osad - zima w Dorne nie
  ma wygladac jak glod), napis MCM i tekst dla Jeffa poprawione na "ploughed and bare" / "zaorane i puste"; (b) "ploughed in early spring" -> "ploughed at the start
  of every year" (pory Westeros trwaja latami). Settings.cs + gen_mcm.
- Przy okazji: po planie od nowa (zmiana siatki drog) liczniki planu od zera - wczesniej wsie liczone dwa razy ("wsi z planem" > wszystkich wsi); srednia "us na wies"
  z liczby wywolan planu.
- Progi: PP-3 <= 25% przy >= 500 polach planu (mniej - <= 60%), nowe PP-3b (wsi z planem bez pola <= 5%, przy < 100 wsiach <= 15%) i PP-3c (pola odrzucone przez
  farbe drogi <= 10% pol planu); log sprzed recenzji - INFO.

**Status.** DO SPRAWDZENIA (build Armoury Release: kod 0, 0 Error(s), ostrzezenia tylko stare - na galezi i na drzewie scalonym z n22-calosc 3c3a7ad9 + n22f-plany,
n22f-najemnicy, n22f-ruda, n22f-scalenie; gen_mcm na drzewie scalonym bez zmian; autotestu nie uruchamialem - zakaz w zadaniu). Scalenie probne (`git merge-tree`)
galezi z kazda galezia fali 2 i z n22-calosc / n22f-* - 0 konfliktow; konflikty w `ArmouryBehavior.cs` sa MIEDZY innymi galeziami fali 2 (np. p6-szubienice +
p6-drogi, tez bez tej galezi) - p6-pola tego pliku nie rusza.

**Decyzje (moje, Jeff: "ekonomie projektuje Claude").**
- Pory roku Westeros trwaja latami, wiec rytm pol idzie wedlug dnia roku (jak zniwa FreeHands), a pora Westeros decyduje tylko: zima = snieg / ugor / zielone wedlug
  krainy, wiosna Westeros = bez zolknienia (nie ma zniw - jak w ekonomii).
- Snieg: decal zdjety zamiast bialego koloru (kolor mnozy teksture - bialego zrobic sie nie da; snieg mapy i tak lezy pod spodem). Zima = pora nieba, bo to ona sypie
  snieg na mapie (proba zimy autotestu na porze ekonomii nie zmienia pol).
- Spalone: wies gry przy rabunku i po nim (stan gry), wioska ukladu wedlug ksiegi wiosek (ta sama definicja spalenia co obrazek wioski). Glod pokazany odlogiem
  (udzial pustych osad z ksiegi) - wies gry bez tego (ksiega nie daje pustych osad wsi gry przez LookOf).
- Bez zapisu i bez Reset: stan zyje w widoku mapy (nowa kampania / wczytanie = nowa mapa = nowy widok); miejsca pol z hasha id wioski - po wczytaniu te same.
- Nowy klucz `MapVillageFields` domyslnie TAK (nowy klucz dociera do gry Jeffa - jego plik MCM go nie ma).
- Po recenzji: pola omijaja FARBE drogi (to, co gracz widzi), a nie pas premii predkosci - premia moze byc szersza od drogi (zeby "lekko zejsc z drogi"
  nie gubilo predkosci), obraz pol nie. Zapas 0.2 jedn. ponad pol-szerokosc farby (cieciwa osi drogi bocznej w siatce ok. 0.06 + miekki brzeg stempla).
- Po recenzji: zima w cieplych krainach = wyglad "zaorane" (nie "odlogiem") - "odlogiem" zostaje znakiem pustych osad, zeby zima w Dorne nie wygladala jak glod.

**Test w grze / log_grep.**
- Start mapy: `Pola \(p6\): start - wsi z polami [0-9]+` - liczba wiosek (ok. 2.4 tys. przy wczytanym ukladzie), czas startu, pora pol teraz z dniem roku.
- Co 2 minuty (przy zmianie): `Pola \(p6\): stan - w scenie pol [0-9]+` - pola w scenie, wioski z planem (i bez zadnego pola), pola bez miejsca, odrzucone
  wedlug przyczyny (farba drogi), pierscien zapasowy, wyglad pol w scenie, najdluzszy tick, potkniecia (wszystkie 0).
- Zmiana pory: `Pola \(p6\): pora pol [a-z ]+ -> [a-z ]+ \(dzien roku [0-9]+` - np. zielone -> dojrzale w dniu 196 w lato.
- Sprawdzenie: `python -I tools/sprawdz_logi.py --grupa p6pola <Armoury-*.log>` (PP-0..PP-6).
- Zdjecia: wioska w Reach latem (zielone), dzien 196-223 (zolte), Polnoc w zimie (brak pol), wies zlupiona (czarne pola), wioska z droga boczna (pole nie
  wchodzi na farbe drogi). Kamera nisko (ponizej 110). Do oceny: czy barwy `LookColor` sa dobrze widoczne na teksturze pola.
