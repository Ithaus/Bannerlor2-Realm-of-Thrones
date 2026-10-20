## p6-pola (20.10) - pola wokol wiosek na mapie wedlug pory roku (Armoury)

**Dla Jeffa (prosto).** Wokol kazdej wioski na mapie leza teraz pola. Wczesna wiosna sa zaorane i brazowe, potem zielone, w tygodniach zniw zolte, a po zniwach zostaje sciernisko.
Zniwa na polach to te same zniwa, z ktorych zyja wsie (dni 196-252 roku, tylko w lato albo jesien Westeros), wiec po kolorze widac, kiedy zbiera sie plon.
Gdy przyjdzie zima, pola na Polnocy i w innych zimnych krainach znikaja pod sniegiem; w Dorne i Essos leza odlogiem, a na Wyspach Letnich zostaja zielone.
Wioska zlupiona ma pola czarne i spalone. Wioska, ktorej czesc osad opustoszala po rabunku albo glodzie, ma czesc pol odlogiem (szarobure).
To tylko obrazek: ekonomia, trasy i zapis gry sie nie zmieniaja. Wylacznik w MCM: "Map Village Fields".

**Problem / zrodlo pomyslu.** POMYSLY-35 pkt 18 (R3-07b), Jeff 20.10: "28 - nie, 29 - nie. Pozostale tak", potem "Rob od razu fale 2". Audyt drog i widoku 09.10
(`docs/audyt-2026-10-09/08-drogi-wioski-widok.md`, tabela 4.4 N-3, "Dla Jeffa" pkt 8): pola wokol wiosek zmieniajace wyglad z pora roku - pokazac zniwa i glod
bez liczb; technika jak D-1 (decale na mapie). Dotad wokol wiosek nie bylo zadnych pol (poza 28 decalami pszenicy, ktore ROT-Map ma przy kilku wsiach).

**Zmiana.** Nowy plik `Armoury/src/MapFields.cs` + minimalne wpiecia:
- `MapFields` (statyczna, bez typow SandBox.View): klucz, pora pol swiata (`Phase`), rejestracja widoku z ochrona przed bledem ladowania typu (3 proby, jak drogi boczne);
- `MapFieldsView` (komponent mapy, Priority 107 - po drogach bocznych): lista wiosek, plan pol, komorki, decale, pora i stan wsi, linie logu;
- `SubModuleMain.OnApplicationTick`: jedna linia `MapFields.AppTick()` (przed wylacznikiem wiosek - pola wsi gry sa tez bez wiosek ukladu);
- `MapRoads.cs`: jedna linia `GridNow` (odczyt referencji siatki drog - zmiana siatki = pola od nowa, zeby nie lezaly na drodze);
- `Settings.cs`: nowa sekcja "Fields around villages" z kluczem `MapVillageFields` (NOWY, domyslnie TAK) + `tools/gen_mcm.py` (McmSettings.cs);
- `tools/sprawdz_logi.py`: grupa `--grupa p6pola` (progi PP-0..PP-6, funkcje `p6pola_phase`, `p6pola_main`).

Jak dziala:
- **Naklejka.** Material pola pszenicy gry `decal_production_farm_wheat_a/b/c/d` (Native `map_icon_parts.xml`, prefab `map_icons_production_farm_wheat_*`;
  w `materials.tpac`: shader modulate, use_albedo, **render_on_terrain** - maluje tylko teren, nie domy ani drzewa). Cztery materialy = cztery cwiartki atlasu
  (argument jak w prefabie gry), wybor z hasha - pola nie sa jednakowe. Decale BEZ encji (wzor drog bocznych i piany NavalDLC: `Decal.CreateDecal` -> `SetMaterial`
  -> `Scene.AddDecalInstance(d, "editor_set")` -> `Frame`; zdjecie `RemoveDecalInstance`). Kolor pory = `SetFactor1` na zywym decalu (tak gra barwi tropy partii -
  `MapTracksVisualManager.ParallelUpdateTrackColors`, dekompilacja SandBox.View 1.4.8) - tylko przy zmianie wygladu pola.
- **Kto ma pola (100% wsi rolniczych).** Kazda wies gry (rybacka / gornicza 2 pola, reszta 4) i kazda wioska ukladu Armoury (poziom 1/2/3 = 2/3/4 pola, mlyn /
  wiatrak / zagroda / spichlerz +1, rybacka -2, najmniej 1). Bez pol: przysiolki na pustkowiach (kolumna hamlet - pasterze, drwale, weglarze, mysliwi, rybacy).
- **Gdzie.** Pierscien wokol obrazka (pol obrysu + pol pola + 0.2 jedn.; pole wioski ukladu ok. 1.5 x 1.9 jedn., wsi gry ok. 2 x 2.6), pola co 360/K st. od frontu
  wioski. Pole odrzucone, gdy: za blisko miasta (6 jedn.) / zamku (4), nachodzi na inne pole wioski, nie lezy po swojej stronie granicy z sasiednia wioska
  (polplaszczyzna z zapasem - pola dwoch wiosek nie wchodza na siebie) albo wchodzi na obrazek sasiada, teren nie uprawny (sciana siatki: Plain, Desert, Forest,
  Steppe, RuralArea - bez sniegu wiecznego, bagna, wydm, plazy i wody), droga (`MapRoads.KindAt`: trakt albo droga boczna w siatce premii), stok (rozrzut
  wysokosci rogow > 0.7 albo normalna < 0.85). 6 prob miejsca na pole, potem bez tego pola (liczone w logu wedlug przyczyny).
- **Pora pol (raz na dobe gry, wspolna dla swiata; rok 364 dni, inny - proporcjonalnie).** Dni 0-69 zaorane (siew), 70-195 zielone, okno zniw FreeHands
  (`FreeHands.HarvestWindow` - ta sama regula co rece do zniw i plon): 196-223 dojrzale, 224-252 sciernisko; 253-299 sciernisko (w roku wiosny Westeros - bez zniw -
  zielone), od 300 zaorane. Zima Westeros (pora NIEBA `WesterosClimate.SkySeason` - ta, ktora sypie snieg na mapie): kraina zimna (`WinterRealm.OfVillage` = 1) -
  decal zdjety ze sceny (pole pod sniegiem), ciepla (< 1: Dorne, Essos) - zaorany ugor, Wyspy Letnie (0) - zielone.
- **Stan wsi (co godzine gry, tylko wioski w scenie).** Wies gry rabowana albo zlupiona (`VillageState` BeingRaided / Looted) - wszystkie pola spalone. Wioska
  ukladu - ksiega wiosek (`VillageBook.LookOf`): spalona - spalone; puste osady (rabunek + glod) - tyle pol odlogiem, jaki udzial osad pusty; ksiega czytana tylko
  przy zmianie jej numeru wygladu (`LookVersion`) i przy wejsciu wioski do sceny. Bez ksiegi - stan wsi gry okregu.
- **Wydajnosc.** Plan pol wioski dopiero przy pierwszym wejsciu jej komorki (32 x 32 jedn.) w kolo widoku kamery (do 6 prob miejsca x 16 sond terenu i drogi na pole), potem
  w pamieci. W scenie tylko pola komorek w kole widoku (to samo kolo co drogi boczne - `MapSideRoads.ViewCircle`, histereza 16 jedn.); kamera wyzej niz
  min(MapVillagesHideAboveCameraHeight, 110) - zadnego pola. Praca 4 razy na sekunde, budzet 4 ms na cwierc sekundy (najpierw zdejmowanie, potem stawianie);
  co klatke nic (CLAUDE.md 7). Oczekiwane: ok. 2.4 tys. wiosek z polami, ok. 7-8 tys. pol w planie na calej mapie, w scenie zwykle kilkaset (kolo ok. 80-130 jedn.).

**Ryzyko / co sprawdzic (kontrola calosci).**
- Regresje / kolizje: nowy widok obok drog bocznych (106) i wiosek (100); nie dotyka ich stanu ani decali. `MapRoads.GridNow` tylko czyta referencje (volatile).
  `WinterRealm.OfVillage`, `VillageBook.LookOf`, `FreeHands.HarvestWindow`, `WesterosClimate.SkySeason` - same odczyty z watku glownego (pod ich wlasnymi zamkami).
  Ekonomia, AI, zapis: bez zmian (zero przeplywow zlota i towaru - ksiega Obieg bez nowych pozycji).
- Wyglad NIESPRAWDZONY w grze: kolor przez `SetFactor1` mnozy barwe tekstury (shader modulate - tak jak ziemia drog bocznych). Jesli tekstura pola gry jest bardzo
  ciemna albo kolor nie dziala na tym materiale, pory beda slabo widoczne - wtedy poprawic barwy `MapFields.LookColor` (jedna tablica). Pola pod drzewami lasu
  (Forest dozwolony) - maluja ziemie pod drzewami (jak polana).
- Drogi: sprawdzian drogi idzie przez siatke premii predkosci - przy `MapRoadSpeedBonus` = NIE (KindAt -1) albo `MapSideRoadSpeedBonus` = NIE pola moga lezec na
  narysowanej drodze bocznej. U Jeffa oba TAK. Siatka zmieniona (publikacja drog bocznych chwile po wczytaniu) - pola zdjete i plan od nowa raz na zmiane (linia logu).
- Gesto ustawione wioski ukladu (odstep 4.5 jedn.) maja pola tylko po stronie z dala od sasiadow; w bardzo gestych skupiskach wioska moze zostac bez pol (log: "sasiad").
- Obrazek wioski przesuniety przez widok wiosek (na lad, ze stoku) nie przesuwa pierscienia pol (plan liczy od polozenia z pliku ukladu - tak samo jak drogi boczne).
- Liczba decali: kilkaset w scenie obok ok. 2 tys. decali drog bocznych; przy klopocie z plynnoscia - klucz NIE (pola zdjete od razu).
- Watki: caly kod na watku glownym (tick widoku mapy) - nic nie wolaja watki AI.

**Status.** DO SPRAWDZENIA (build Armoury Release: kod 0, 0 Error(s), ostrzezenia tylko stare; autotestu nie uruchamialem - zakaz w zadaniu).

**Decyzje (moje, Jeff: "ekonomie projektuje Claude").**
- Pory roku Westeros trwaja latami, wiec rytm pol idzie wedlug dnia roku (jak zniwa FreeHands), a pora Westeros decyduje tylko: zima = snieg / ugor / zielone wedlug
  krainy, wiosna Westeros = bez zolknienia (nie ma zniw - jak w ekonomii).
- Snieg: decal zdjety zamiast bialego koloru (kolor mnozy teksture - bialego zrobic sie nie da; snieg mapy i tak lezy pod spodem). Zima = pora nieba, bo to ona sypie
  snieg na mapie (proba zimy autotestu na porze ekonomii nie zmienia pol).
- Spalone: wies gry przy rabunku i po nim (stan gry), wioska ukladu wedlug ksiegi wiosek (ta sama definicja spalenia co obrazek wioski). Glod pokazany odlogiem
  (udzial pustych osad z ksiegi) - wies gry bez tego (ksiega nie daje pustych osad wsi gry przez LookOf).
- Bez zapisu i bez Reset: stan zyje w widoku mapy (nowa kampania / wczytanie = nowa mapa = nowy widok); miejsca pol z hasha id wioski - po wczytaniu te same.
- Nowy klucz `MapVillageFields` domyslnie TAK (nowy klucz dociera do gry Jeffa - jego plik MCM go nie ma).

**Test w grze / log_grep.**
- Start mapy: `Pola \(p6\): start - wsi z polami [0-9]+` - liczba wiosek (ok. 2.4 tys. przy wczytanym ukladzie), czas startu, pora pol teraz z dniem roku.
- Co 2 minuty (przy zmianie): `Pola \(p6\): stan - w scenie pol [0-9]+` - pola w scenie, wioski z planem, odrzucone wedlug przyczyny, wyglad pol w scenie,
  najdluzszy tick, potkniecia (wszystkie 0).
- Zmiana pory: `Pola \(p6\): pora pol [a-z ]+ -> [a-z ]+ \(dzien roku [0-9]+` - np. zielone -> dojrzale w dniu 196 w lato.
- Sprawdzenie: `python -I tools/sprawdz_logi.py --grupa p6pola <Armoury-*.log>` (PP-0..PP-6).
- Zdjecia: wioska w Reach latem (zielone), dzien 196-223 (zolte), Polnoc w zimie (brak pol), wies zlupiona (czarne pola). Kamera nisko (ponizej 110).
