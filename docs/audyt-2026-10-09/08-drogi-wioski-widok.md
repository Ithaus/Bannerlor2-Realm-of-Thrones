# AUDYT 08 (09.10): drogi na mapie i wyglad wiosek pobocznych (W2)

Zakres: (a) co z wiosek W2 i z 8 punktow lore (decyzja Jeffa 08.10 02:20) jest w grze, czego nie ma, co poprawic;
(b) czy da sie dodac widoczne drogi nieutwardzone / lesne, czy partie po nich szybciej chodza, gdzie maja biec.
Styk z prosba Jeffa z 09.10 ("realne dystanse", "oboz od 24 do 6", "rzeczy, na ktore nie wpadlismy") - rozdz. 3.4, 4.3, 4.4.

Tylko odczyt. Kod = wersja w grze: Armoury c01a54ba = commit 2e235ea (repo audytu `SCR7\audyt\repo`). Nic nie uruchamialem,
niczego nie zmienialem; jedyny zapis to ten plik.

Oznaczenia: [K] kod (plik:linia), [P] pomiar (log / plik danych / zdjecie autotestu), [H] historia / lore ze zrodlem, [S] szacunek.
Skroty sciezek:
- `R` = `C:\Users\GAME\AppData\Local\Temp\claude\...\7016f733-...\scratchpad\audyt\repo` (kod w grze)
- `SCR` = `C:\Users\GAME\AppData\Local\Temp\claude\...\3cf3e0ac-...\scratchpad` (badania z 07-08.10)
- `D` = `SCR\dzien-6\wioski-v4\drogi` (badanie drog 08.10, plik `SCR\dzien-6\wioski-v4\DROGI.md`)
- `WL` = `SCR\dzien-6\wioski-lore` (dane lore A + B)
- `G` = `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord`
- `CS` = `C:\Users\GAME\Documents\Mount and Blade II Bannerlord\CrashScribe`

---

## 0. Dla Jeffa

1. Wioski poboczne sa w grze i dzialaja stabilnie: 2 446 wiosek w 425 okregach, szesc rodzajow obrazka (wioska, mlyn wodny,
   wiatrak, zagroda, spichlerz, rybacy), ogien nad wioska w czasie rabunku. W calym rocznym tescie: 925 pozarow, zero bledow,
   zero wywrotek.
2. Z Twoich uwag do wygladu poprawione sa: domy w Reach juz sie nie zapadaja, wiatrak ma skrzydla, rybacy stoja na brzegu.
   Nadal zle: mlyny wodne stoja czesciowo w samej rzece (widac na zdjeciach z autotestu) - do poprawki tej nocy.
3. Z 8 punktow "lore" (pan i herb w dymku, proporczyk, rycerz, czym zyje wioska, wiara, historia, pamiec wojny, menu okregu)
   w grze nie ma jeszcze ZADNEGO. Dymek pokazuje tylko nazwe, okreg i liczbe ludzi. Teksty sa juz napisane dla kazdej wioski,
   ale trzeba je przeliczyc na uklad wiosek, ktory masz w grze (dzis pasuje tylko co trzecia wioska).
4. Tej nocy proponuje male i bezpieczne kroki: mlyny o pol kratki na lad, w dymku "kto trzyma te ziemie" (rod i jego siedziba,
   na zywo z gry), komunikat w dzienniku, gdy ktos pali Twoja wioske, oraz przeliczenie tekstow lore i drog na Twoj uklad.
5. Drogi: tak, da sie dorysowac drogi polne i lesne tam, gdzie ROT ich nie namalowal (Polnoc za Winterfell, Dorne, Essos,
   drogi od wiosek do zamkow). Najpewniejsza metoda to te same "naklejki sciezek", ktorymi ROT rysuje uliczki w swoich wsiach.
   Tej nocy - tylko proba na dwoch kawalkach mapy ze zdjeciami i pomiarem klatki, bez wgrywania.
6. Dzis zadna droga (ani ROT, ani nasza) nie przyspiesza marszu - gra liczy tylko teren (las, brod, pustynia, snieg, noc).
   Mozemy dodac "po drodze szybciej" (proponuje +15%), ale wojska AI i tak ida najkrotsza trasa, nie drogami. To zmienia
   rozgrywke, wiec decyzja Twoja.
7. Uwaga do "realnych dystansow": w naszych dokumentach sa dwie rozne skale mapy (2.8 km i 4.8 km na kratke). Trzeba wybrac
   jedna, zanim zaczniemy liczyc marsz i drogi w kilometrach. Noc w obozie liczy sie dzis od 21 do 5 - Twoje "od 24 do 6"
   to zmiana w innym miejscu (raport o obozach), tu tylko zaznaczam styk.
8. Nowe pomysly z tego tematu (gra na to pozwala): myto na mostach i brodach (jak Freyowie na Blizniakach), szubienice przy
   rozstajach po egzekucjach band, pola wokol wiosek zmieniajace kolor z pora roku, starozytne drogi Valyrii w Essos jako
   osobny wyglad.

---

## 1. Stan dzis

### 1.1 Wioski W2 - kod w grze [K]

| Co | Gdzie | Stan |
|---|---|---|
| Widok wiosek (obrazki z kopii siatek, bez fizyki i skryptow) | `R\Armoury\src\MapVillagesView.cs:1-45` (opis), klasa `MapVillagesView : EntityVisualManagerBase` :1338 | JEST |
| Rodzaj obrazka z kolumny `model` (village/mill/windmill/farm/granary/fishing) | `MapVillagesView.cs:357-358`, przepisy :1097-1160 | JEST |
| Wiatrak ze skrzydlami (`sturgia_windmill_a` + `sturgia_windmill_fan_a_open`) | :912-941 | JEST (wyglad 3) |
| Mlyn wodny z kolem (`battania_watermill` + `_mill`), kotwica = srodek kola 0.6 polgrubosci za brzegiem | :890-911, :930-933, :2158, :2517-2523, :3287 | JEST, ale mlyn wchodzi w wode (rozdz. 3.1) |
| Wysokosc kazdego budynku = dol BB na najnizszym gruncie (koniec "zapadania" w Reach) | :34-37 (opis wygladu 3) | JEST |
| Ogien nad pierwsza wioska lancucha, gdy wies gry `IsUnderRaid`; dym, gdy `IsRaided` | :1353-1354, :1856-1880 | JEST |
| Dymek: nazwa, "A village of the X district", "N souls in M settlements", podpalacz | :4418-4436; tekst `VillageTexts.cs:15-17` | JEST (3-4 linie) |
| GetPrefabName / GetOldPrefabName (zrodlo AccessViolation 08.10 02:03) | :21-22, :1226-1227 - nie wolane | USUNIETE |
| MCM: `MapVillagesEnabled` (domyslnie on), `MapVillagesHideAboveCameraHeight` 160, `MapVillageNamesOnHover` | `Settings.cs:785-787`; Armoury.json Jeffa nie ma tych kluczy = domyslne z kodu | wlaczone |
| Teksty spalonej / odbudowanej wioski (`VilBurned`, `VilHalfempty`, `VilTipBurned`, `VilTipRebuilt`, `VilLogBurning(Own)` ...) | `VillageTexts.cs:9-24` | ZDEFINIOWANE, NIEUZYWANE (0 wywolan w `R\Armoury\src`) |
| Ksiega wiosek W3 (spalona na stale, odbudowa), rabunek osada po osadzie | :7 "to ksiega wiosek (W3)" | BRAK |

### 1.2 Wioski - pomiar [P]

- Plik w grze: `G\Modules\Armoury\ModuleData\arm_map_villages.tsv` md5 b742f307, crc32 89612CA7, 2 446 wierszy = plik generatora v4
  (`SCR\dzien-6\wioski-v4\gen\arm_map_villages.tsv`, crc32 2594A95D) + jedna linia naglowka `# scene_xml_crc: 146201570`
  (porownanie `diff` bez komentarzy: dane identyczne).
- Rozklad: miejsce (`kind`) road 1 249, river 474, coast 441, crossroad 166, bridge 74, field 35, lake 7; obrazek (`model`)
  village 1 053, farm 446, fishing 400, mill 358, windmill 98, granary 91; poziom 1: 2 133, poziom 2: 313; osad na wioske mediana 16
  (8-27), razem 42 092 osady.
- `Armoury-2026-10-08_11-40-05.log` (nowa kampania z 161): wiosek 2446 w 425 okregach, odrzucone 0, CRC sceny zgodne; do konca
  sesji postawione 631, naraz najwiecej 304; sredni czas obrazka 0.202 ms; kolo mlyna nad woda 81, dotyka wody 74, mlyn->wiatrak
  (brak wody) 3, rybacy->wioska 3; wiatraki ze skrzydlami 29 / bez 0; zdjete budynki na stromym gruncie 81; potkniecia 0.
- `Armoury-2026-10-08_08-18-38.log` (rok, 364 doby): postawione 707, naraz najwiecej 478, ognie od wczytania 925, sredni czas
  obrazka 0.200 ms, wszystkie liczniki potkniec 0.
- `CS\session-2026-10-08*.log`: 0 x AccessViolation po wersji z 08.10 rano (ostatnie AV w `MapVillagesView.TreeLine` - sesja
  02:01, opisana w STAN-PRAC).
- Zdjecia autotestu `CS\zdjecia\at-20261008-065139` (wersja v5 = w grze), ogladane po pomniejszeniu:
  - `windmill-applewick-z08` - wiatrak ze skrzydlami i schodami, stoi na trawie - OK;
  - `slope-poppymead-z17`, `gra-berrybush-z08` - domy Reach na gruncie, nie zapadniete - OK;
  - `fishing-widows-horn-z17` - pomosty od brzegu, lodzie na wodzie - OK;
  - `slope-poppymead-z17` (wyciete 45-98% x 45-72% kadru) i `mill-sweet-bridge-z17`: **trzy mlyny wodne nad rzeka maja budynek w
    korycie** (strzecha w polowie na wodzie) - uwaga Jeffa "mlyny za bardzo w wodzie" nadal aktualna;
  - `village-barrowthwaite-z40` - z wysokosci 40 wioska to kilka drobnych domkow, dymek dziala.

### 1.3 Lore wiosek (8 punktow) - co jest w grze

| Pkt | Co | W grze | Dane gotowe (WL) |
|---|---|---|---|
| 1 | Dymek z panem okregu i herbem | NIE - linia "A village of the X district" (`VillageTexts.cs:15`) | linia pana z gry na zywo (projekt rozdz. 1.2) |
| 2 | Proporczyk w barwach pana | NIE | - (z gry: `MobilePartyVisual.GetBannerOfCharacter`) |
| 3 | Rycerz / pomniejszy rod | NIE | A: `podglad_rycerze.tsv` 2 449 wierszy na STARYM ukladzie 4000 |
| 4-6 | Czym zyje, wiara, historia | NIE | B: `WL\v4-proba\arm_map_village_lore-B.tsv` 2 446 wierszy |
| 7 | Pamiec wojny | NIE | po ksiedze ludzi 108-113 |
| 8 | Menu okregu | NIE | - |

Zgodnosc danych z ukladem w grze [P] (moje liczenie po `uid`, kolumna 10 pliku wiosek):
- B v4-proba: 2 446 / 2 446 `uid`, ALE naglowek `layout_crc32: 2594a95d` = crc pliku generatora, a w grze crc 89612CA7 (dodana
  linia `scene_xml_crc`). Bramka z projektu (`PROJEKT-WIOSKI-LORE-2026-10-08.md` rozdz. 9.4, "inny uklad = lore wylaczone")
  **odrzucilaby poprawne dane**.
- A (rycerze): 687 / 2 446 `uid` wspolnych - trzeba przeliczyc (poprawka P12 projektu).
- Katalog `G\Modules\Armoury\GUI` nie istnieje (potrzebny dla dymka z herbem, L1).

### 1.4 Drogi - co jest [K][P]

- Drogi ROT to farba w terenie (warstwa "road" `terrain.bin`) - ok. 10 000 jedn., na rastrze 1 jedn. 12 172 komorki
  (`SCR\dzien-6\wioski-na-mapie\a-teren\drogi_raster.npz`, maska `painted`) [P].
- Scena mapy ROT (`G\Modules\ROT-Map\SceneObj\Main_map\scene.xscene`): decale sciezek w osadach `decal_battania_path_a/b`
  160 + 181, `decal_empire_path_a/b` 275 + 102, `decal_khuzait_path_a/b` 196 + 234 = **1 148**; skrypt `road_instance` 58 razy
  (rzeki) [P, policzone grep dzis].
- Gra stawia na mapie male decale w duzej liczbie: tropy partii = prefab `map_track_arrow` z samym `decal_component`
  (`G\Modules\Native\Prefabs\map_icons.xml:337-343`), pula 256 encji przez `GameEntity.Instantiate`
  (`SCR\dzien-6\autotest\dec\SandBox.View\SandBox.View.Map.Managers\MapTracksVisualManager.cs:14,16,358-368`), kolor
  `Decal.SetFactor1` (:134); pula tropow kampanii `new TrackPool(2048)` (`SCR\ore-supply\cs\...\MapTracksCampaignBehavior.cs:127`) [K].
- Predkosc: `SCR\ore-supply\cs\TaleWorlds.CampaignSystem.GameComponents\DefaultPartySpeedCalculatingModel.cs:262-336` - tylko
  typ sciany siatki pod partia: las -30% (:264-283), woda / brod / most -30% (:285), pustynia (:293), snieg (:314), noc (:321).
  Zadnego "Road". ROT `ROT.Models\ROTPartySpeedModel.cs:28-31` - tylko +20% dla partii, w ktorej gracz sluzy (enlisted).
  BKROTPatch `BKROTPartySpeedModel.cs:57-85` - +15% blogoslawienstw wiary wg typu terenu. Nikt nie daje premii za droge [K].
- Trasa partii: najkrotsza po scianach siatki, bez kosztu terenu (`DROGI.md` rozdz. 5, `MobileParty.cs:3712`,
  `SandBoxNavigationCache.cs:88,134`) [K, za badaniem 08.10 - nie powtarzalem].
- Armoury nie ma zadnego widoku drog. `RoadMemoryFix.cs`, `IslandRoads.cs`, `MarketRoad.cs` to pamiec tras / wyspy / cel wozow -
  nie wyglad [K].
- Prototyp sieci (08.10, `D\arm_map_roads.tsv`): 3 107 linii, 31 950 jedn. (trakt 9 012, wiejska 9 983, sciezka do wioski
  10 930, polna 2 026), 74 039 stempli; szkic C# kompiluje sie (`D\szkic\DrogiNaMapie.cs`), nic nie wgrane [P].

---

## 2. Jak bylo w sredniowieczu / w lore [H]

**Drogi i tempo**
- Sredniowieczne drogi byly w wiekszosci nieutwardzone; tempo podrozy z itinerariow krolow angielskich ok. 15-20 mil
  (24-32 km) na dobe, stabilne przez cale sredniowiecze, z duzym rozrzutem miedzy podrozami
  (J.R. Hall, working paper 2026, https://gwern.net/doc/technology/2026-hall.pdf). Dwor Edwarda I: zwykle do 15 mil, ponad 20
  mozliwe, przy 20+ wozach (podsumowanie: https://blogs.valpo.edu/ellenfoster/?p=769).
- Pieszo 15-20 mil, w zla pogode 6-8; woz ok. 12 mil; konno 20-25, ze zmiana koni 30-40 (podsumowania za I. Mortimerem:
  https://medieval.substack.com/p/traveling-in-the-middle-ages, https://www.odysseytraveller.com/articles/guide-to-medieval-england/).
- Duza armia piesza: 8-12 mil (13-19 km) na dobe; z dlugim taborem poza glownymi drogami - jednocyfrowo w milach
  (B. Devereaux, ACOUP: https://acoup.blog/2019/10/06/new-acquisitions-how-fast-do-armies-move/).
- Rzym (dla porownania skrajnosci): legion na suchej drodze nieutwardzonej do 8 mil, po deszczu prawie stoi; na drodze
  brukowanej 20-25 mil w kazda pogode (cytat ze zrodla wtornego w watku AskHistorians - slabe zrodlo, tylko kierunek).
- Wniosek [S]: droga nie zmienia tempa marszu w suchy dzien bardzo (moze +10-25%), za to decyduje o tempie z wozami i po
  deszczu (x2-3). W Bannerlordzie bez wozow i bez blota sensowna premia "po drodze" jest umiarkowana.

**Prawo drog**
- Anglia: cztery "krolewskie drogi" (Watling Street, Fosse Way, Ermine Street, Icknield Way) pod szczegolna ochrona krola;
  Leges Henrici Primi (ok. 1115) zakazuja blokowania drogi i zasadzki na goscincu
  (https://en.wikipedia.org/wiki/Leges_Henrici_Primi, https://en.wikipedia.com/wiki/Foss_Way,
  https://www.historytoday.com/archive/ermine-street).
- Utrzymanie mostow i drog: myto mostowe (pontage) i drogowe (pavage) nadawane przez krola na kilka lat temu, kto placi za
  naprawe; np. 1291 brat krola Edmund - 5 lat pontage na most w Lancaster; Shrewsbury 1220 - 1d od wozu
  (https://en.wikipedia.org/wiki/Pontage, https://en.wikipedia.org/wiki/Pavage).

**Lore GoT**
- Drogi Westeros zbudowal / przebudowal Jaehaerys I (od 62 AC): Krolewski Trakt (kingsroad) pierwszy, potem roseroad, ocean road,
  river road, goldroad - finansowane za Martyna Tyrella (https://awoiaf.westeros.org/index.php/Kingsroad,
  https://awoiaf.westeros.org/index.php/Roseroad, https://awoiaf.westeros.org/index.php/Goldroad,
  https://awoiaf.westeros.org/index.php/Ocean_Road, https://awoiaf.westeros.org/index.php/Martyn_Tyrell).
  Krolewski Trakt biegnie do Muru - na mapie ROT za Winterfell go nie ma (prototyp dorysowuje "trakt", `D\drogi-polnoc.png`).
- Essos: drogi Valyrii z topionego kamienia, proste "jak wlocznia", pol stopy nad gruntem; trasy: demon road Meereen - Mantarys -
  Volantis; Pentos - Ghoyan Drohe; Ghoyan Drohe - Norvos - Qohor (https://awoiaf.westeros.org/index.php/Valyrian_roads,
  https://awoiaf.westeros.org/index.php/Dragonstone_(material)).
- Blizniaki: jedyna przeprawa przez Green Fork na setki mil; Freyowie wzbogacili sie na mycie za most
  (https://awoiaf.westeros.org/index.php/The_Twins, https://awoiaf.westeros.org/index.php/A_Game_of_Thrones-Chapter_59).

**Wioski**: historia osadnictwa i rozstawienia - juz zbadane (`docs/HISTORIA-GDZIE-ZYLI-LUDZIE-2026-10-07.md`,
`docs/PROJEKT-WIOSKI-NA-MAPIE-2026-10-07.md` rozdz. 2); lore rodow / wiary / historii sprawdzone u zrodla
(`docs/PROJEKT-WIOSKI-LORE-2026-10-08.md` rozdz. 9) - nie powtarzam.

---

## 3. Luki i bledy logiki

### 3.1 Mlyny wodne stoja w rzece [P][K]
Dowod: zdjecia `CS\zdjecia\at-20261008-065139\slope-poppymead-z17.png` i `mill-sweet-bridge-z17.png` - budynek mlyna
w polowie w korycie. Log mowi "kolo nad woda 81, dotyka wody 74" - licznik sprawdza tylko kolo, nie budynek.
Przyczyna [K]: kotwica = srodek kola stawiany `AnchorOut = 0.6 x polgrubosci kola` ZA brzegiem (`MapVillagesView.cs:3287`,
:2158, :2517-2520); brzeg z wachlarza promieni i scian siatki (:665, :2093) - na waskich rzekach ROT (ok. 1-2 jedn.) brzeg
liczony z sieci 8-10 jedn. jest niedokladny, a mlyn ma ok. 1.5 jedn. (`P(... 1.5f ...)` :1099). Brak licznika "budynek nad woda".

### 3.2 Dane lore i drog nie pasuja do ukladu w grze [P]
- B: poprawne `uid`, zly `layout_crc32` (crc liczony z calego pliku, a plik w grze ma o jedna linie naglowka wiecej) - bramka
  wylaczy lore w calosci.
- A (rycerze): 687 / 2 446 `uid`.
- Drogi: `D\arm_map_roads.tsv` liczony na ukladzie 4000 (`D\s6-wynik.txt`: "2449 nazwanych wiosek (wyniki-4000)"); konce
  sciezek "wioska -> droga" trafiaja (<= 1 jedn.) tylko w **843 z 2 446** wiosek w grze. Plik drog nie nadaje sie do uzycia bez
  ponownego przebiegu `s6_siec.py` na `arm_map_villages.tsv` z gry.

### 3.3 Polowa wiosek "przy drodze", a drogi nie widac [P]
1 249 wiosek ma miejsce `road`, 166 `crossroad`. Ok. 115 z 1 249 i 27 z 166 stoi w promieniu 1.5 jedn. od prototypowego traktu
lub drogi wiejskiej (czyli drogi, ktorej na mapie nie ma; moje liczenie na `D\arm_map_roads.tsv`, klasy trakt + wiejska).
Te ok. 10% wiosek wyglada na rozrzucone "w polu" - dokladnie zarzut Jeffa "rozstawienie ma miec sens". Pozostale stoja przy
farbie ROT.

### 3.4 Drogi nie zmieniaja ruchu; trasy AI ich nie znaja [K][P]
- Patrz 1.4. Nawet przy premii za droge AI nie skreci na droge: trasa = najkrotsza po siatce.
- Ile prostej miedzy osadami biegnie dzis po drodze ROT (moje liczenie: odcinek prosty probkowany co 0.5 jedn., `d_road <= 1.5`
  z `drogi_raster.npz`, 815 polaczen z `drogi.json`): twierdza-twierdza **32%** (mediana 27%), wies-twierdza **55%** (mediana 52%).
  Premia za droge dotknelaby wiec ok. 1/3 marszu armii miedzy twierdzami i ok. 1/2 drogi chlopskich wozow [S].
- Najtansza sciezka a-teren jest tylko 11-19% dluzsza od prostej (`dl/prosto` 1.11 / 1.19) - drogi ROT biegna blisko prostych.

### 3.5 Dwie skale mapy w dokumentach [K][P]
- `docs/AUDYT-CZAS-MAPA.md:16,94` i `WorldPace.cs:17-18`: 4.75-4.8 km / jedn. (Mur - Sunspear = 1 012 jedn.).
- `docs/PROJEKT-WIOSKI-NA-MAPIE-2026-10-07.md:124` i `D\s6_siec.py:311`: 2.84 km / jedn. (Mur 170 jedn.).
- Ta sama siec drog to wiec "91 tys. km" albo "152 tys. km". Dla "realnych dystansow" (prosba 09.10) trzeba jednej stalej;
  AUDYT-CZAS-MAPA pokazuje, ze ROT ma Mur ok. 1.5 x za dlugi, wiec skala kontynentalna 4.77 jest lepsza dla marszu, a 2.84
  dla rzeczy lokalnych (gestosc wsi) - zapisac to jawnie.
- Dodatkowo: komentarz `WorldPace.cs:14-22` liczy "lore'owy miesiac jako ulamek 168-dniowego roku", a rok ma dzis 364 dni
  (`Calendar.cs:12`). Kalibracja tempa jest wiec nieaktualna - to temat raportu o marszu; tu tylko styk.

### 3.6 Noc w obozie [K] (styk, nie moj temat)
`NightRest.cs:107` - `bool night = h >= 21 || h <= 5;` (noc 21:00-05:59). Jeff 09.10: "od 24 do 6 rano". Zmiana granic nocy
dotyka tez kary nocnej predkosci gry (-25% od `Campaign.IsNight`, `DefaultPartySpeedCalculatingModel.cs:321`) - to inna
"noc" niz nasza. Do raportu o obozach.

### 3.7 Spalona wioska nie wyglada na spalona; gracz nie dostaje komunikatu [K]
- Ogien tylko w czasie rabunku, dym tylko w czasie `IsRaided` wsi gry (:1865); po nim wioska wraca do wygladu calej.
- Teksty "Smoke over X: Y is burning your village" (`VillageTexts.cs:21`) i "X has been burned" sa, ale nikt ich nie wola.
  Decyzja Jeffa 07.10 ("Wioska XYZ spalona", "plonie na mapie") spelniona tylko w polowie (ogien jest, komunikatu nie ma).

### 3.8 Prototyp drog a lore Essos [S]
Na `D\drogi-calosc-maly.png` model rysuje gesta siec traktow takze na wschodnich stepach i w glebi Essos (graf twierdz).
Lore zna tam tylko konkretne drogi Valyrii (rozdz. 2) - reszta to raczej szlaki koczownikow / karawan. Do poprawy przy
przeliczeniu (D0), nie blad kodu.

---

## 4. Propozycje

Legenda: priorytet P0 (blokuje) / P1 (wazne) / P2 (dobre); wielkosc mala / srednia / duza.
Kolizje z paczkami 169 (log obiegu), 170 (BetterEconomy), 171 (zbrojenie zalog): **brak** - inne pliki i inne mechaniki.
Wszystkie zmiany W2 skladac na 2e235ea (wersja w grze), jeden czat.

### 4.1 Wioski

| Nr | Co zmienic | Liczby i uzasadnienie | Prior. | Wielk. | Gracz | Nowa kampania | Ryzyko | Zaleznosci |
|---|---|---|---|---|---|---|---|---|
| W-1 | Mlyn wodny przy rzece: kotwica kola na brzegu zamiast za nim + caly obrazek 0.5 jedn. ku ladowi; nowy licznik "budynek mlyna nad woda" (probka gruntu w 4 rogach BB vs poziom wody) | `AnchorOut` 0.6 -> 0.0 x polgrubosci kola i stala `MillLandBack = 0.5` odejmowana w `want` (:2158) tylko gdy `!shore.Sea` - kolo i tak dosuwa `PlaceAtWater` do 1 jedn.; 0.5 jedn. = ok. 1/3 szerokosci mlyna, tyle ile na zdjeciu wchodzi w wode | P1 | mala | wyglad | nie | niskie (te same funkcje; gorzej = kolo nad trawa - licznik "nad ladem") | brak |
| W-2 | Dymek: linia pana na zywo zamiast "district": "A village of the {LANDS} lands, held by House {HOUSE} of {SEAT}" (rod wsi gry `Village.Bound.OwnerClan`, siedziba `Clan.HomeSettlement`, zapas: zamek / miasto okregu; rod gracza: "your fief") - bez herbu | pierwsza czesc pkt 1 Jeffa; tekst wedlug projektu lore rozdz. 1.2 (nazwa ROT z przecinkiem - czlon przed przecinkiem) | P1 | mala | tak (tekst) | nie | niskie (try/catch jak dzis :4438-4442; null = stary tekst) | brak; pelny dymek z herbem = W-5 |
| W-3 | Komunikat w dzienniku, gdy plonie wioska GRACZA: `VilLogBurningOwn` w miejscu logu "ogien nad" (:1872-1876) | tylko wsie rodu gracza (925 pozarow/rok w calym swiecie = ok. 2.5 / dobe - dla wszystkich za duzo spamu) | P1 | mala | tak (informacja) | nie | niskie | brak |
| W-4 | L0 danych lore: A + B na pliku z gry, `layout_crc32` liczony z wierszy danych BEZ komentarzy (odporny na linie naglowka), P1-P13 z projektu | 2 446 / 2 446 `uid` (dzis A 687) | P0 dla W-5..W-8 | mala (Python) | nie | nie | zero (offline) | uklad v4 zamrozony |
| W-5 | L1 dymek z herbem (wlasny prefab Gauntlet, `GUI\Prefabs`) + Alt: czym zyje, wiara, historia | wedlug projektu lore rozdz. 1 i 11 | P1 | srednia | tak | nie | srednie (nowy prefab dymka - [AUTOTEST]) | W-4 |
| W-6 | L2 proporczyki (`campaign_flag` z herbem pana) | max 80 naraz, kamera <= 90, budowa 12 / 0.25 s (projekt rozdz. 2) | P2 | srednia | tak | nie | srednie (klatki) | W-2 |
| W-7 | L3 menu okregu "Look over the district" | projekt rozdz. 7 | P2 | srednia | tak | nie | srednie (kolejnosc opcji BK / ROT) | W-4, W-5 |
| W-8 | W3: wyglad spalonej wioski (kopia siatek wsi `*_looted` matki albo przyciemnienie + brak detali) przez N dob po rabunku; L4 pamiec wojny | czas odbudowy z ksiegi ludzi 113 (powrot uchodzcow) | P2 | duza | tak | nie (sekcja zapisu `arm_district`) | srednie (zapis) | 108-113 wgrane |

### 4.2 Drogi - widok

| Nr | Co zmienic | Liczby i uzasadnienie | Prior. | Wielk. | Gracz | Nowa kampania | Ryzyko | Zaleznosci |
|---|---|---|---|---|---|---|---|---|
| D-0 | Przeliczyc siec na ukladzie z gry: `D\s6_siec.py` z `G\...\ModuleData\arm_map_villages.tsv`; w Essos trakty tylko na trasach lore (demon road, Pentos - Ghoyan Drohe - Norvos - Qohor) + szlaki; naglowek `layout_crc32` (jak W-4) | dzis 843 / 2 446 trafien; generator 7.6 s | P0 dla D-1/D-2 | mala | nie | nie | zero (offline) | brak |
| D-1 | PROBA (tylko autotest, nie do wgrania): stemple decali na 2 komorkach 50 x 50 (Dorzecze kolo Fairmarket 392/532, Polnoc kolo Winterfell 396/845) przez **wlasny prefab z samym `decal_component`** w `Armoury\Prefabs\arm_map_roads.xml` (wzor 1:1 `map_track_arrow`, `map_icons.xml:337-343`) + `GameEntity.Instantiate` jak `MapTracksVisualManager.cs:358-368`; material `decal_battania_path_a/b`, `decal_empire_path_a/b`, `decal_khuzait_path_a/b` | parametry stempli z `DROGI.md` 2.1 (trakt 0.26 x 0.36, odstep 0.50); w kadrze 150-370 decali przy kamerze 38 (`D\s7-wynik.txt`) < pula tropow gry 2 048 | P1 | srednia | wyglad (tylko w tescie) | nie | niskie (tor tropow gry); prefab XML z bledem = blad przy starcie - stad tylko w autotescie | D-0 |
| D-2 | Pelny widok drog: komorki 50 x 50, LOD (kamera <= 60 wszystkie klasy, 60-110 trakt + wiejska, > 110 nic), budzet 40 stempli / klatke, MCM "Unpaved roads (visual only)" | z `DROGI.md` rozdz. 4 i 6; zywych 1.6-2.8 tys. encji przy 53 550 encjach sceny ROT | P1 | duza (ok. 2 dni) | wyglad | nie | niskie-srednie (liczba encji; mierzyc klatke) | D-1 udana |
| D-3 | Wyglad "droga Valyrii" w Essos: ciemniejszy stempel (`SetFactor1`, jak tropy :134) na trasach z lore | lore: topiony czarny kamien | P2 | mala (po D-2) | wyglad | nie | niskie | D-2 |
| D-4 | Odrzucone: przemalowanie `terrain.bin` ROT (cudzy plik 50 MB, kasowany przy kazdej aktualizacji ROT) i `road_instance` jako glowna metoda (natywny, nieuzywany tryb "droga") - zgodnie z `DROGI.md` rozdz. 6 | - | - | - | - | - | - | - |

### 4.3 Drogi - mechanika (realne dystanse)

| Nr | Co zmienic | Liczby i uzasadnienie | Prior. | Wielk. | Gracz | Nowa kampania | Ryzyko | Zaleznosci |
|---|---|---|---|---|---|---|---|---|
| M-1 | Jedna skala mapy w kodzie i dokumentach: `KmPerUnit = 4.77` dla marszu, tempa i dlugosci drog; 2.84 opisane jako "skala lokalna (Mur)" tylko dla gestosci osad | Mur - Sunspear 1 012 jedn. = ok. 4 830 km kanonu (AUDYT-CZAS-MAPA rozdz. 1); Mur w ROT 1.5 x za dlugi, wiec nie nadaje sie na wzorzec | P1 | mala | nie (tylko liczby w logach / opisach) | nie | zero | raport o marszu (WorldPace 168 -> 364 dni) |
| M-2 | "Po drodze szybciej": +15% do predkosci koncowej partii, ktorej srodek jest <= 0.75 jedn. od drogi malowanej ROT (raster 1600 x 1600 bit = 320 KB w ModuleData, odczyt O(1)); pozniej takze nasze trakty / wiejskie; brak premii w lesie (las juz -30%) | historycznie droga daje +10-25% w suchy dzien i x2-3 w blocie / z wozami (rozdz. 2); +15% to srodek bez symulacji blota; dotyka ok. 32% marszu twierdza-twierdza i 55% wies-twierdza (3.4) | P2 | srednia | **TAK** - decyzja Jeffa | nie | srednie: gracz zyska wiecej niz AI (AI nie wybiera drog); zapas - ta sama premia dla wszystkich partii, wiec karawany i wozy wsi tez szybsze | M-1; raport o marszu (MarchPace / WorldPace) |
| M-3 | (na pozniej, jesli M-2 przyjete) Bloto: poza droga -15% w deszczu / na wiosne i jesienia; na drodze bez kary | Rzym: na suchej drodze nieutwardzonej 8 mil, po deszczu prawie stoi; Mortimer: 6-8 mil w zla pogode zamiast 15-20 | P2 | srednia | TAK | nie | srednie | M-2, pogoda mapy gry |
| M-4 | (badanie) Trasy AI po drogach: dla marszu > 60 jedn. punkty posrednie na drodze (rozstaje), jesli droga dluzsza o < 15% | najtansza sciezka po drogach jest 11-19% dluzsza od prostej (3.4) | P2 | duza | TAK | nie | wysokie (ruch AI, StrategicCampaignAI, BK) | M-2 |

### 4.4 Rzeczy, na ktore nie wpadlismy (gra na to pozwala)

| Nr | Co | Dlaczego / zrodlo | Prior. | Wielk. | Gracz | Ryzyko | Zaleznosci |
|---|---|---|---|---|---|---|---|
| N-1 | **Myto na mostach i brodach**: karawana / woz przechodzacy most (wioski `kind=bridge` 74 + mosty ROT z `mosty_brody_brzeg.json`) placi panu mostu stawke od ladunku; zloto przechodzi z kiesy karawany do kasy osady pana - zadnego zlota z niczego (zasady Z projektu ekonomii) | pontage / pavage w Anglii (1d od wozu, Shrewsbury 1220); Freyowie zyja z myta Blizniakow (rozdz. 2) | P2 | srednia | tak (karawany gracza placa) | srednie (nowy przeplyw pieniadza) | **projekt ekonomii 162-168** (tabela zrodel i ujsc - dopisac "myto mostowe" jako transfer); BK `CaravanFee` = myto bramne juz jest (`docs/ZRODLA-DOCHODU.md:167`) - nie dublowac |
| N-2 | **Szubienica przy rozstajach** na N dob po egzekucji bandytow / zbiegow w okregu (prefab gry `gallows`, `G\Modules\Native\Prefabs\archhitecture_aserai.xml`, jako kopia siatek jak W2) | prawo goscinca: krol karze napad na drodze (Leges Henrici Primi); w lore Dorzecza wisielcy przy drogach | P2 | mala-srednia | wyglad | niskie (jak W2) | OutlawLaw (egzekucje), D-2 |
| N-3 | **Pola wokol wiosek wedlug pory roku** (decale `mainmap_decal_mud_a` / `..._sand_*` z NavalDLC jako zaorane pole wiosna, zolte latem, sciernisko jesienia, pod sniegiem zima) | pokazuje zniwa i glod bez liczb (spichlerz z PROJEKT-GLOD) | P2 | srednia | wyglad | niskie-srednie (liczba decali) | D-1 (ta sama technika) |
| N-4 | **Karczma przy rozstajach**: 166 wiosek `crossroad` - jeden budynek wiecej (karczma) i zdanie w dymku | lore: Inn at the Crossroads, Inn of the Kneeling Man (ROT ma je jako osady) | P2 | mala | wyglad | niskie | W-4 (tekst) |
| N-5 | **Ogniska obozu nocnego na mapie**: przy namiocie z `NightRest` (juz stawia namiot, `NightRest.cs:118-123`) male ognisko `psys` jak ogien wiosek (:1353) | widac, kto stoi obozem - nocny napad ma sens | P2 | mala | wyglad | niskie (limit jak `AiTentCap`) | raport o obozach (godziny 24-6) |

---

## 5. Do zrobienia tej nocy vs na pozniej

### 5.1 Tej nocy (male, bezpieczne, autotest 40 dob + zdjecia)

1. **W-1 mlyny** - `R\Armoury\src\MapVillagesView.cs:3287` (`k.AnchorOut = 0.6f * ...` -> `0.0f * ...`, zostawic pole) i :2158
   (`want = shore.Edge + AnchorOutW(...) - ay * g.Sy` minus `MillLandBack` 0.5 dla mlyna przy rzece); nowy licznik do linii
   "woda (mlyn / rybacy)" (:1823-1827): "budynek mlyna nad woda N". Sprawdzenie: log - kolo "nad woda" i "dotyka wody" nie mniej niz
   dzis (81 / 74 w sesji 11-40-05), "budynek nad woda" ok. 0; zdjecia `mill-sweet-bridge` z08/z17 i nowe cele na mlyny przy rzece w tym samym
   okregu (plik w grze): Summer Mill 299.63 / 252.37, Berrybrook 293.30 / 252.47, Crossmill 312.93 / 246.89.
2. **W-2 linia pana** - `VillageTexts.cs:15` nowy klucz `arm_vil_tip_held` "A village of the {LANDS} lands, held by {HOUSE} of {SEAT}" i
   wariant gracza; `MapVillagesView.cs:4424-4425` (zamiast `VilTipDistrict`, z zapasem na stary tekst). Sprawdzenie: zdjecia z
   dymkiem (tryb zdjec juz robi zrzut z dymkiem - `village-barrowthwaite-z40`); 0 potkniec "dymek".
3. **W-3 komunikat** - `MapVillagesView.cs:1872-1876`: gdy `d.S.Village.Bound.OwnerClan == Clan.PlayerClan`, `MBInformationManager` /
   `InformationManager.DisplayMessage` z `VillageTexts.VilLogBurningOwn`. Sprawdzenie: w 40 dobach gracz autotestu nie ma wsi -
   tylko licznik w logu "komunikatow dla gracza 0" i brak bledow (pelna proba przy pierwszym rabunku wsi Jeffa).
4. **W-4 i D-0 dane offline** (bez gry): przeliczenie `WL\skrypty\generator.py` (A) i `WL\b-skrypty\b_dymki.py` (B) oraz `D\s6_siec.py`
   na `G\Modules\Armoury\ModuleData\arm_map_villages.tsv`; `layout_crc32` z wierszy danych. Kontrola: 2 446 / 2 446 `uid`,
   konce sciezek w >= 95% wiosek (dzis 843).
5. **D-1 proba drog** (tylko DLL autotestu, przywrocic zatwierdzony po tescie): 2 komorki + 4 zdjecia (Fairmarket 392/532 i
   Winterfell 396/845, wysokosci 8 / 17 / 40); w logu: stempli zywych, czas tworzenia na stempel, klatka z i bez (pomiar klatki AT3).
   Wgranie dopiero po obejrzeniu zdjec przez Jeffa.

Autotest: jedna zmiana naraz w opisie, ale W-1..W-3 mozna puscic razem (ten sam plik, rozne funkcje, osobne liczniki). Wgranie
tylko na "wgraj".

### 5.2 Na pozniej

- W-5 dymek z herbem, W-6 proporczyki, W-7 menu okregu (kolejnosc z projektu lore rozdz. 11).
- W-8 spalona wioska i pamiec wojny - po 108-113.
- D-2 pelny widok drog, D-3 drogi Valyrii - po zdjeciach z D-1.
- M-1 jedna skala - razem z raportem o marszu (tam tez WorldPace przy roku 364 dni i noc 24-6 z `NightRest.cs:107`).
- M-2..M-4 i N-1..N-5 - po decyzji Jeffa (M-2, N-1 zmieniaja rozgrywke) i po paczkach ekonomii 162-168 (N-1).

### 5.3 Pytania do Jeffa (zmieniaja rozgrywke)

1. Drogi tylko jako widok, czy tez "po drodze szybciej" (+15%, wszystkie partie, AI nie wybiera drog)?
2. Ile drog rysowac: wszystkie (trakty, wiejskie, sciezki do kazdej wioski), bez sciezek do wiosek, czy same trakty?
3. Myto na mostach i brodach (placa tez Twoje karawany) - tak / nie?
