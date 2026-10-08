# BADANIE A - rody pomniejsze i rycerze wiosek (dane do pkt 1 i 3 decyzji Jeffa 08.10)

Stan: tylko dane i skrypty w `SCR\dzien-6\wioski-lore`. Repo, worktree, gra: tylko odczyt. Nic nie wgrane, nic nie zacommitowane.
Teksty w plikach danych (herby, linie dymka) po angielsku; ten raport po polsku.

## 0. W skrocie

- **rody.csv: 508 wierszy** = 439 rodow Westeros z AWOIAF + 5 wierszy Daru (Nocna Straz, zamki ROT) + 64 rody Essos.
- Z 439 rodow Westeros: **127 to juz klany ROT** (pan okregu, NIE rycerz wioski), **268 to kandydaci na rycerza / pomniejszy rod
  zaprzysiezony panu**, 43 wymarle (tylko do historii, pkt 6-7), 1 krolewski (Targaryen).
- Polozenie: **212 rodow z osada ROT** (A: zamek/miasto 181, A-: wioska ROT o tej nazwie 31), 16 z kalibrowanej mapy AWOIAF (B),
  70 z opisu lore (C, np. "sworn to Whent" -> Harrenhal), 146 swobodnych (D: AWOIAF nie podaje siedziby -> ziemie pana).
- Herb (blazon po angielsku) ma **350 z 444** wierszy Westeros+Dar, imiona czlonkow **344**; kolory wyciagniete z blazonu (+ HEX).
- **Podglad przydzialu na 2449 wiosek** (`podglad_rycerze.tsv`): 339 wiosek trzyma rod z lore (180 przy siedzibie, 159 rod swobodny
  w okregu swojego pana), 2110 rod wymyslony w stylu krainy. Uzyte 278 z 314 kwalifikujacych sie rodow lore. **1565 wymyslonych rodow,
  0 kolizji** z rodami lore, klanami ROT i nazwami osad ROT; jeden wymyslony rod trzyma najwyzej 2 wioski.

## 1. Zrodla i metoda

Glowne zrodlo: **A Wiki of Ice and Fire (awoiaf.westeros.org)**. WebFetch i curl dostaja 403 (Cloudflare), wiec strony czytane
przez wbudowana przegladarke (strona przeszla sama, bez zadnego CAPTCHA), dane pobrane z API MediaWiki tej samej strony
(`api.php?action=query&prop=revisions`), bez obchodzenia zabezpieczen.

1. Lista rodow: szablony nawigacyjne krain (stan "At the start of A Game of Thrones"):
   `Template:HousesNorth`, `HousesRiverlands`, `HousesVale`, `HousesWesterlands`, `HousesReach`, `HousesStormlands`,
   `HousesCrownlands`, `HousesBlackwater`, `HousesDorne`, `HousesIslands`, `Houses of Braavos`, `Houses of Slaver's Bay` -> 474 rody
   (3 strony Braavos nie istnieja: Torone, Tendyris, Pranelis).
2. Kazdy rod: strona `House_X` (471 stron), infobox: `Coat_of_arms` (blazon), `Seat`, `Overlord` (pan bezposredni wybrany dla roku
   298 AC: wpis z dopiskiem AGOT albo z przedzialem dat obejmujacym 298; "historical", ASOS/AFFC/ADWD pomijane), czlonkowie (linki
   "Imie Nazwisko" na stronie rodu -> same imiona, bez tytulow).
3. Siedziby: 248 stron siedzib - `Location map` (plik mapy krainy + lat/long na tej mapie) i sekcja Geography (linki miejsc).
4. Dar Nocnej Strazy: strony `Gift`, `Night's_Watch` (bracia "take up plain black shields"), oficerowie: Jeor Mormont, Bowen Marsh,
   Othell Yarwyck, Alliser Thorne, Donal Noye, Jaremy Rykker, Ottyn Wythers, Wynton Stout, Mallador Locke, Endrew Tarth,
   Denys Mallister (Shadow Tower), Cotter Pyke (Eastwatch); zamki Muru (Queensgate, Deep Lake, Icemark, Nightfort, ...).
5. Essos: `Triarch_of_Volantis`, `Magister`, `Keyholders`, `Pureborn`, `Thirteen`, `Good_Masters`, `Wise_Masters`, `Great_Masters`,
   `Archon`, strony miast (Pentos, Lys, Tyrosh, Myr, Norvos, Qohor, Lorath, Volantis, Braavos, Qarth).
6. Mapa ROT (tylko odczyt): `Modules\ROT-Map\ModuleData\settlements.xml` (1065 osad: 97 miast, 130 zamkow, 571 wsi gry),
   klany `Modules\ROT-Content\ModuleData\spclans.xml` (243 frakcje).

Surowe dane AWOIAF zapisane w `surowe\awoiaf_houses_1..3.tsv` i `surowe\awoiaf_seats.tsv` (skrypty licza z nich, nie z sieci).

## 2. Liczby na kraine (Westeros + Dar)

| Kraina | wiersze | pan okregu (klan ROT) | pomniejszy lord | rycerz z ziemia | inne (ranga nieznana) | wymarle | A / A- / B / C / D |
|---|---|---|---|---|---|---|---|
| The North | 65 | 21 | 7 | 0 | 29 | 8 | 42 / 3 / 0 / 7 / 13 |
| The Gift | 5 | 5 (Straz) | - | - | - | - | 5 / 0 / 0 / 0 / 0 |
| The Riverlands | 51 | 13 | 15 | 1 | 11 | 11 | 21 / 2 / 1 / 13 / 14 |
| The Vale | 39 | 12 | 8 | 5 | 12 | 2 | 14 / 3 / 4 / 3 / 15 |
| The Westerlands | 55 | 14 | 8 | 7 | 22 | 4 | 19 / 2 / 2 / 11 / 21 |
| The Reach | 73 | 16 | 17 | 2 | 36 | 2 | 18 / 8 / 1 / 3 / 43 |
| The Stormlands | 39 | 15 | 6 | 3 | 12 | 3 | 15 / 4 / 1 / 4 / 15 |
| The Crownlands | 33 | 3 | 0 | 0 | 28 | 2 | 5 / 0 / 5 / 6 / 17 |
| Crownlands (Dragonstone) | 18 | 8 | 0 | 0 | 9 | 0 (+1 Targaryen) | 9 / 2 / 0 / 6 / 1 |
| Dorne | 29 | 15 | 0 | 3 | 2 | 9 | 15 / 1 / 1 / 9 / 3 |
| The Iron Islands | 37 | 10 | 11 | 4 | 10 | 2 | 18 / 6 / 1 / 8 / 4 |

"Pomniejszy lord" = Lordly House w AWOIAF, ktorego ROT nie ma jako klanu (np. Whent, Goodbrook, Butterwell, Vypren, Merryweather,
Mullendore, Costayne, Plumm, Estren, Sunderland, Wynch, Tawney) - to najlepsi kandydaci na "pomniejszy rod zaprzysiezony panu".

## 3. Polaczenie z mapa ROT

Kolejnosc dopasowania siedziby (kolumny `pewnosc`, `dopasowanie`):
- **A** - siedziba = miasto/zamek ROT po nazwie albo aliasie (Twins -> The Twins, Castle Cerwyn -> Cerwin, Hall of the Mormonts ->
  Mormont Keep, Golden Tooth -> Goldentooth, Claw Isle -> Celtigar Keep, Dun Fort -> Duskendale, Horn Hill -> Hornhill, ...).
  Klan ROT zawsze dostaje polozenie swojej siedziby w ROT (np. Paege: lore bez siedziby, w ROT Fairmarket; Orkwood: ROT zamek Orkwood).
- **A-** - siedziba = wies gry ROT o tej nazwie (Hag's Mire, Uplands, Grimston, Cider Hall, New Barrel, Appleton, Feastfires, Newkeep,
  Sweetsister, Grandview, Fawnton, Amberly, Broad Arch, Kingshouse, Deepdown, Brownhollow, Spottswood Village, Stackhouse, ...).
- **B** - siedziba poza ROT, ale z punktem na mapie krainy AWOIAF: kalibracja afiniczna lat/long -> x,y ROT z par siedzib
  obecnych w obu (blad RMS w jednostkach mapy ROT): Crownlands 5.3, Stormlands 8.0, Westerlands 12.7, Dorne 13.2, Reach 14.9,
  Iron Islands 16.5, Vale 20.1, Riverlands 22.8, North 30.4 (`skrypty\kalibracja.txt`). Wynik dalej niz 45 j. od osady ROT jest
  odrzucany (np. Paps). Przyklady: Three Towers, Castamere, Old Anchor, Longsister, Stokeworth, Antlers, Sow's Horn, Crow's Nest.
- **C** - opis lore (pierwsze akapity strony rodu/siedziby): "near Riverrun", "sworn to Whent of Harrenhal", "Crackclaw Point",
  "Fair Isle", "on Old Wyk", "Harlaw" -> osada ROT tego miejsca.
- **D** - AWOIAF nie publikuje siedziby (np. Atranta, Whitewalls, Ninestars, Grey Glen, Gull Tower, Wyndhall, Greenfield, Darkdell,
  Ivy Hall, Holyhall, Blackpool, Poddingfield, Gallowsgrey) albo rod nie ma siedziby: x,y = siedziba pana, rod jest "swobodny" w
  krainie pana (w przydziale idzie do okregu, ktorego pan w ROT = jego pan w lore).

Swiadome wyjatki (ROT ma wioske o nazwie rodu, ale lore stawia rod gdzie indziej - nie dopasowuje po nazwie): Volmark (lore Harlaw,
ROT wioska przy Lordsport), Shatterstone (lore Old Wyk, ROT przy Pyke), Parchments (lore Stormlands, ROT przy Stonedance), Myre,
Kenning, Sunderly, Massey, Hook, Ryder, Tarth, Hayford, Barrow.

Klany ROT (`klan_rot`): 127 rodow Westeros dopasowanych do klanow ROT po nazwisku + siedzibie (galezie "X of Y" tylko po tej samej
osadzie). Niedopasowany klan ROT Westeros: tylko `clan_empire_north_5 Tully,Blackfish` (Harroway - to Tully). Uwaga w danych:
klany ROT `Thorne` (ROTclan_125) i `Tollett` (clan_nord_3) to Nocna Straz (Ser Alliser, Dolorous Edd), nie rody z ziemia.

## 4. Kto moze byc "rycerzem wioski" (pkt 3)

Kolumna `rola`:
- `pan_okregu_ROT` - klan ROT (wielki pan albo pan zamku/miasta) -> **nie** rycerz wioski; to pan z dymka (pkt 1).
- `pomniejszy_rod_lord` (Lordly House bez klanu ROT), `rycerz_z_ziemia` (Knightly House bez klanu ROT, 25: Wode, Hardyng, Shett x2,
  Templeton, Woodhull, Clifton, Greenfield, Hetherspoon, Lorch, Ruttiger, Vikary, Yew, Fossoway of Cider Hall, Osgrey, Bolling,
  Brownhill, Hasty, Dayne of High Hermitage, Drinkwater, Santagar, Harlaw x4), `pomniejszy_rod` (ranga nieznana) -> **kandydaci** (268).
  Rody rycerskie, ktore ROT ma jako klany (Cox, Paege, Waxley, Clegane, Swyft, Connington, Seaworth, Dalt), sa panami okregow.
  `pomniejszy_rod_master` jest pusty: Glover i Tallhart sa klanami ROT.
- `wymarly_historia` (Hoare, Justman, Teague, Mudd, Fisher, Gardener, Reyne, Tarbeck, Darklyn, Durrandon, Greystark, Ryder, ...)
  -> tylko do jednego zdania historii (pkt 6) i pamieci wojny (pkt 7), nigdy jako zywy rycerz.
- `krolewski` (Targaryen) - pominiety.

Imiona: kolumna `imiona` = imiona czlonkow z AWOIAF (kolejnosc ze strony, NIE zawsze obecna glowa rodu). Gdy rod nie ma znanych
czlonkow, generator dobiera imie z listy krainy.

## 5. Generator (skrypty\generator.py) - deterministyczny, bez kolizji

- Hash: **FNV-1a 32 bit** na bajtach UTF-8 uid wioski (`ROT_castle15_village1#e747e32f`) - latwe przeniesienie do C# (uint, mnozenie
  przez 16777619 z przepelnieniem). Ten sam uid = zawsze ten sam rod, rycerz i herb. **Nic do sejwu.**
- Nazwa rodu: przedrostek + przyrostek krainy ("Frost"+"holt", "Rush"+"well", "Sand"+"spring", "Salt"+"cliff"); Essos z sylab
  (walyrianskie -aros/-arys/-ion, ghiscarskie "zo", Ibben, Yi Ti, Wyspy Letnie, Sarnor). Start z hasha, potem **sondowanie liniowe po
  calej przestrzeni kombinacji** - zawsze znajdzie wolna nazwe; odrzuca nazwy z zakazanej listy (wszystkie rody lore lacznie z wymarlymi
  i z Essos, ich siedziby, klany ROT, osady ROT) i juz uzyte (unikalne na calej mapie).
- Osoba: tytul wedlug krainy - "Ser" (Dorzecze, Dolina, Zachod, Reach, Burza, Korona, Dorne), bez "Ser" na Polnocy i Zelaznych Wyspach,
  "Brother" w Darze, "Magister"/rod z miasta w Wolnych Miastach, "X zo Y" Ghis, "Ko X" Dothrakowie; co 9. wioska "Lady".
  Imiona z lore krainy (bez glownych postaci typu Eddard/Tywin/Daenerys).
- Herb wymyslony: pole + godlo krainy, zasada heraldyczna metal na kolorze / kolor na metalu ("a silver elk on grey",
  "two crossed gold oars on orange").
- Przydzial w podgladzie: (1) rody lore A/A-/B/C biora najblizsze wolne wioski swojej krainy (lord 2, rycerz/inny 1, do 70 j.;
  Essos do 40 j.), (2) rody D - wioska w okregu, ktorego pan w ROT nosi nazwisko pana z lore (np. Terrick -> okregi Tully), kolejnosc
  z hasha, (3) reszta - rod wymyslony, 1-2 sasiednie wioski (do 15 j.) tego samego okregu.

Wynik podgladu (`podglad_rycerze.tsv`, kolumny `dymek_pan` = pkt 1, `dymek_rycerz` = pkt 3):

| Kraina | lore przy siedzibie | lore swobodny | wymyslony |
|---|---|---|---|
| The North | 27 | 10 | 239 |
| The Riverlands | 21 | 21 | 238 |
| The Vale | 15 | 17 | 113 |
| The Westerlands | 18 | 23 | 98 |
| The Reach | 21 | 51 | 196 |
| The Stormlands | 11 | 16 | 70 |
| The Crownlands (+Dragonstone) | 19 | 18 | 2 |
| Dorne | 3 | 2 | 114 |
| The Iron Islands | 4 | 1 | 0 |
| Essos (wszystkie) | 41 | 0 | 1040 |

Przyklady linii (po angielsku, jak w grze):
```
A village of the Tumbledown lands, held by House Stark of Winterfell      (pkt 1; okreg = wies gry, pan = wlasciciel zamku/miasta)
Ser Lymond Mudton of Wendbrook, sworn to House Bracken                   (pkt 3, rod wymyslony)
Gwayne Gaunt of Elmcross, sworn to House Rykker                          (pkt 3, rod lore, Duskendale)
Herrock Kenning of Windstack, sworn to House Harlaw                      (Zelazne Wyspy, bez "Ser")
Alequo Nyanoanys of Lasyr, sworn to House Vhassar                        (Volantis)
```
Pan w pkt 1 i "sworn to" w pkt 3 maja byc brane w grze z **biezacego** wlasciciela zamku/miasta (zmienia sie po zdobyciu); CSV podaje
pana na start kampanii (`pan_okregu_rot`) tylko do sprawdzenia. Gdy wlasciciel sie zmieni, rycerz lore zostaje (rod siedzi na ziemi),
zmienia sie tylko "sworn to" - do decyzji w paczce kodu.

## 6. Essos (krotko)

- **Volantis** - Stara Krew (triarchowie): Maegyr, Paenymion, Vhassar, Tagaros, Qhaedar, Vaelaros, Staegone (6 z 7 to juz klany ROT).
- **Pentos** - magistrzy wybieraja Ksiecia: Mopatis (Illyrio), Haratis, Narratys, Draz.
- **Lys** - magistrzy: Rogare (najwiecej imion), Lohar, Pendaerys, Dagareon, Orthys, Haen, Bazanne.
- **Tyrosh** - Archont: Ryndoon, Quaynis, Adarys, Uhoris, Tumitis. **Myr**: Drahar. **Norvos**: Votyris, Golathis, Hotah. **Qohor**: Mott.
- **Braavos** - Morski Wladca i keyholderzy (potomkowie 23 zalozycieli Zelaznego Banku): Antaryon, Zalyne, Nhai, Prestayn, Reyaan,
  Otherys, Fregar, Otharys.
- **Qarth** - Pureborn + Trzynastu: Xhoan Daxos, Xhore, Votar.
- **Zatoka Niewolnicza** - Meereen (Great Masters, 15 rodow piramid: Loraq, Pahl, Kandaq, Reznak, Galare, Ghazeen, Merreq, Dhazak,
  Naqqan, Zhak, Quazzar, Hazkar, Uhlez, Yherizan, Rhazdar), Astapor (Good Masters: Nakloz, Ullhor), Yunkai (Wise Masters: Eraz, Yunzak,
  Qaggaz, Myraq, Rhaezn, Ahlaq, Zherzyn, Faez).
- Lore Essos nie ma rycerzy z ziemia ani herbow - wioski wokol miast to posiadlosci (estates) rodow miasta; dlatego w Essos dominuje
  generator (1040 z 1081 wiosek). Lorath, Ibben, Yi Ti, Sarnor, Dothrakowie, Wyspy Letnie - tylko generator.

## 7. Dar Nocnej Strazy

5 wierszy (Castle Black, Shadow Tower, Eastwatch by the Sea, Nightfort, The Wall z ROT). Bracia nie maja ziemi i rezygnuja z herbow
rodow - "trzymajacy" wioske Daru to brat Strazy ("Kept by Brother X"), pan = Nocna Straz. Lore: Brandon's Gift + New Gift, wsie
Mole's Town i Queenscrown (ROT ma Molestown, Queenscrown, Queen's Head). **Uwaga:** `arm_map_villages.tsv` nie ma ani jednej wioski w
Darze ani za Murem (okregi Strazy plan 0) - linie Daru sa gotowe, ale dzis nieuzywane.

## 8. Ryzyka / co sprawdzic

1. Pan bezposredni z infoboxu AWOIAF to heurystyka "rok 298"; przy kilku rodach (np. Smallwood -> Vance, Stout -> Dustin, Condon ->
   Cerwyn, Hogg -> Hayford) to prawda lore, ale w ROT ten pan moze nie miec okregu - dymek i tak bierze pana z gry.
2. Kalibracja map AWOIAF jest zgrubna (RMS 5-30 j.); rody B maja przyblizone polozenie. Mapa ROT nie jest w skali AWOIAF (Polnoc
   najgorzej: Cerwin, Ironrath, The Rills odbiegaja o 66-84 j. i wypadly z kalibracji).
3. 146 rodow D (bez siedziby w lore) jest rozmieszczanych przez generator - to wybor projektu, nie lore; w dymku nie ma wtedy nazwy
   siedziby lore, tylko wioska.
4. Kolejnosc imion z AWOIAF nie daje obecnej glowy rodu; przy rodach znanych (Bracken: Jonos) pierwsze imie zwykle pasuje, przy innych
   nie musi. Generator i tak bierze jedno imie na rod.
5. Kolory z blazonu to parser slow (gold/yellow/or, silver/white, ...): pole = barwa po "on ..."; przy blazonach zlozonych ("Quartered",
   "Per pale") kolejnosc moze byc nieidealna. Do proporczyka (pkt 2) i tak lepiej uzyc barw klanu pana z gry (do sprawdzenia w kodzie
   gry, nie sprawdzalem).
6. Nazwy wymyslonych rodow sa unikalne na calej mapie i omijaja lore/ROT, ale nie sa sprawdzane wobec nazw 2449 wiosek (rod "Rushwell"
   moze siedziec w wiosce "Rushwell" - w lore to normalne).
7. ROT nazywa niektore wsie jak rody lore (Massey, Mallery, Langward, Rambton, Hayford, Tarth, Stackhouse, Volmark...); czesc dopasowana
   (Stackhouse obok Horseshoe Hills), czesc swiadomie nie (rozdz. 3).

## 9. Pliki

- `rody.csv` (UTF-8 z BOM, separator `;`): kraina, rod, ranga, rola, siedziba, x, y, osada_rot, odl_do_osady, okreg_rot (id|nazwa wsi
  gry), pewnosc, dopasowanie, pan, lord_paramount, pan_okregu_rot, klan_rot, herb, kolory, kolory_hex, imiona, znacznik (M/C/W/S =
  klany gor/crannogmen/Wolfswood/Skagos; P/OW/GW/H/O/S/B = wyspa Zelaznych Wysp; NW Dar; ES Essos), zrodlo (strona AWOIAF), uwagi.
- `podglad_rycerze.tsv` - przydzial na 2449 wiosek z linia dymka pkt 1 i pkt 3 (do oceny, nie dane gry).
- `skrypty\rot_dump.py` (zrzut osad i klanow ROT), `skrypty\build_rody.py` + `build_out.py` (rody.csv), `skrypty\generator.py`
  (generator + podglad), `skrypty\kalibracja.txt`, `skrypty\gen_stat.txt`.
- `surowe\awoiaf_houses_1..3.tsv`, `surowe\awoiaf_seats.tsv` - surowy wyciag z AWOIAF (rod, siedziba, pan, herb, imiona, miejsca;
  siedziba, mapa, lat, long, miejsca).
