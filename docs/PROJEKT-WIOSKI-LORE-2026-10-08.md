# PROJEKT: wioski na mapie - lore i immersja (08.10; synteza badan A, B, C + sprawdzenie u zrodla; NIC NIE ZAKODOWANE)

**Decyzja Jeffa (08.10, doslownie):** "bierzemy wszystkie 8 do wiosek":
1. dymek z panem okregu i HERBEM ("a village of the X lands, held by House Y of Z");
2. proporczyk w barwach pana nad wioska na mapie;
3. rycerz z ziemia albo pomniejszy rod zaprzysiezony panu - najpierw prawdziwe rody lore z siedziba w poblizu, potem wymyslone w stylu krainy;
4. czym zyje wioska; 5. wiara i swiete miejsce; 6. jedno zdanie historii wedlug krainy i miejsca;
7. pamiec wojny (kto spalil, kiedy, ilu ucieklo) - po ksiedze ludzi 108-113;
8. menu okregu we wsi gry z lista wiosek (herb, rycerz, rodzaj, ludzie, stan).

**Stan:** tylko projekt, dane i szkice. Repo, worktree W2 i pliki gry tylko czytane; nic nie zbudowane w Armoury, nic nie wgrane,
nic nie zacommitowane. Ten plik jest jedynym nowym plikiem w repo. Kod W2 (`MapVillagesView.cs`) zmienia teraz inny agent (wyglad) -
wpiecia z tego projektu trzeba bedzie scalic z jego wersja.

Oznaczenia:
- SCR = `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\3cf3e0ac-5529-4b68-a794-0edec69cfda7\scratchpad`
- WL = `SCR\dzien-6\wioski-lore` (badania A, B, C i wszystkie dane tego projektu)
- W2 = `SCR\dzien-6\wioski-2200\repo\Armoury\src\MapVillagesView.cs` (galaz w2-wioski-2200, wgrana u Jeffa)
- v4 = `SCR\dzien-6\wioski-v4\gen\arm_map_villages.tsv` (nowy uklad w toku, 2 446 wiosek, kolumna `model`)
- Badanie A = rody i rycerze (`WL\A.md`), B = wiara / zajecia / historia (`WL\B.md`), C = technika (`WL\TECHNIKA.md`).
- [ZRODLO] = sprawdzone dzis na awoiaf.westeros.org; [KOD] = sprawdzone w dekompilacji (badanie C); [AUTOTEST] = do sprawdzenia w grze.

Zwiazek z `docs/PROJEKT-WIOSKI-NA-MAPIE-2026-10-07.md`: tamten projekt stawia wioski, pali je i odbudowuje (W0-W4). Ten projekt
dokleja do nich pana, herb, rycerza, wiare, zajecie, historie, menu i pamiec wojny. Niczego tam nie zmienia poza tekstem dymka.

---

## 0. W skrocie

1. **Dymek** (rozdz. 1): herb pana okregu obok nazwy, linia pana ("A village of the Tumbledown lands, held by House Stark of
   Winterfell"), linia rycerza ("Kept for them by Harwood Pinehill of Crossbeck"), ludzie, stan. Po Alt: czym zyje, wiara, historia,
   pamiec wojny. Pan i herb sa brane z gry na zywo (zmieniaja sie po zdobyciu zamku), reszta z pliku danych.
2. **Proporczyk** (rozdz. 2): ta sama choragiew na drzewcu, co przy partiach w zamku (siatka gry "campaign_flag" z herbem pana),
   tylko blisko kamery (najwyzej 80 naraz). Zmiana pana albo barw krolestwa = nowa flaga w ciagu sekundy.
3. **Rycerze** (rozdz. 3): 339 z 2 449 wiosek trzyma prawdziwy rod z lore (180 przy swojej siedzibie, 159 w okregu swojego pana),
   2 110 rod wymyslony w stylu krainy (1 565 rodow, zero kolizji z lore, klanami i osadami ROT). **Poprawka syntezy:** imie
   rycerza rodu lore NIE moze byc brane z listy czlonkow AWOIAF (9 z 10 sprawdzonych to postacie martwe przed 298 AC, bracia
   Nocnej Strazy albo glowne postacie, np. Petyr Baelish) - rod zostaje prawdziwy, imie dobiera generator z listy krainy.
4. **Wiara, zajecie, historia** (rozdz. 4-6): gotowe zdania po angielsku dla kazdej wioski (2 449 / 2 449; proba v4 2 446 / 2 446),
   zero losowania w grze, nic do sejwu.
5. **Menu okregu** (rozdz. 7): opcja "Look over the district" w menu wsi gry (takze spalonej): lista wiosek z herbem w kazdym
   wierszu, dymkiem wiersza i "Hear more" z pelnym opisem. Okienka gry, zero przelaczania menu.
6. **Pamiec wojny** (rozdz. 8): kto i kiedy (zdarzenie gry), ilu ucieklo i zginelo (ksiega 113). Jedyny nowy zapis: sekcja `m:`
   wspolnego klucza `arm_district`, typowo 2-5 KB. Przed pierwszym rabunkiem w grze - pamiec z ksiazek tam, gdzie lore ja zna
   (Sherrer, Mummer's Ford, Wendish Town - ludzie bez choragwi, koniec 298 AC).
7. **Sprawdzenie u zrodla** (rozdz. 9): 12 rodow - siedziba, pan i herb zgodne 12 / 12, rola 11 / 12 (Hollard to rod prawie
   wymarly), imie 1 / 10. 16 faktow historii - 14 zgodnych, 1 bledny (zaraza "wyszla z Lannisportu"), 1 niepotwierdzony
   (Haereg "7 rodzin na 10").
8. **Przed zamrozeniem danych 13 poprawek** (rozdz. 10) - wszystkie w Pythonie, zadna nie zmienia kodu moda.
9. **Paczki** (rozdz. 11): L0 dane -> L1 dymek -> L2 proporczyk -> L3 menu -> [108-113] -> L4 pamiec wojny. L0-L3 nie wymagaja
   ksiegi ludzi i nie dotykaja sejwu.
10. **Najwieksze ryzyka** (rozdz. 12): wlasny prefab dymka i proporczyki to rzeczy, ktorych gra jeszcze nie widziala ([AUTOTEST]);
    dane trzeba przeliczyc na ostatecznym pliku v4 (tylko 687 z 2 446 `uid` v4 jest takich samych jak w pliku wgranym).

---

## 1. Dymek (pkt 1, 3, 4, 5, 6, 7)

### 1.1 Uklad (po angielsku, jak w grze)

```
[herb pana]  STONY HOLT
A village of the Tumbledown lands, held by House Stark of Winterfell      <- pan i herb z gry, na zywo
Kept for them by Harwood Pinehill of Crossbeck                            <- rycerz / pomniejszy rod (dane)
4,000 souls in 16 settlements                                             <- jak dzis w W2
Ser Gregor Clegane is putting it to the torch.                            <- stan (W2 / W3, juz w VillageTexts), tylko gdy jest
Hold Alt for more info                                                    <- wiersz gry str_map_tooltip_info
-- po Alt (albo przy ustawieniu gry "dlugie dymki") --
Brewers and beekeepers: barley ale and heather mead. A smithy on the road shoes the horses of passing riders.
Faith: the old gods of the forest, nameless and countless, who watch through the weirwoods' eyes.
Its fields were first given to a younger son of Winterfell, with a holdfast and the land around it.
Burned by Ser Gregor Clegane's men in the autumn of 299 AC; about 600 souls fled and 40 were killed.   <- pkt 7, gdy jest
```

Dzis (W2) dymek ma 3 linie: nazwa / "A village of the Tumbledown district" / ludzie. Nowy dymek zmienia linie 2, dodaje linie 3 i
czesc po Alt. Glowna czesc ma najwyzej 5 linii - nie zaslania mapy.

### 1.2 Reguly tekstu

- **Linia pana:** `A village of {LANDS}, held by {HOUSE} of {SEAT}`.
  - `{LANDS}` z pliku danych, policzone raz na okreg: zwykle "the Tumbledown lands"; dla 32 z 418 okregow, ktorych wies gry nazywa
    sie jak wies albo gospoda (Worm Village, Dread Village, Wendish Town, Cider Hall, Cornhall's, The Inn of the Kneeling Man ...)
    "the lands of Worm Village", "the lands of the Inn of the Kneeling Man" - zamiast "the Worm Village lands".
  - `{HOUSE}` = rod wlasciciela wsi gry (`Village.Bound.OwnerClan`), na zywo. Nazwy ROT z przecinkiem ("Tully,Blackfish") - czlon
    przed przecinkiem. "House X" dla rodow; bez "House" dla nazw, ktore juz maja "the" albo sa kompaniami i bractwami ("the Second
    Sons"), dla Dothrakow "under the khalasar of X".
  - `{SEAT}` = siedziba rodu (`Clan.HomeSettlement`), zapas: zamek / miasto okregu.
  - Gdy pan to rod gracza: `A village of {LANDS}, your fief` i `Kept for you by ...`.
- **Linia rycerza:** `Kept for them by {KNIGHT}` - `{KNIGHT}` gotowe z danych: "Ser Guthor Bridges of Summer Mill",
  "Harwood Pinehill of Crossbeck" (Polnoc bez "Ser"), "Lady Ravella Terrick of Hazelpool". Dothrakowie: `{KNIGHT} takes its tribute`.
  Zamiast "Sworn to them: ..." z badania C - krocej i nie powtarza nazwy rodu pana.
- **Herb** = `Clan.Banner` pana (dla rodu panujacego gra zwraca herb krolestwa - tak samo jak przy partiach) [KOD].
- **Tlo dymka** jak przy osadach gry: wrog / swoi / obcy [KOD].
- **Zdania po Alt** (czym zyje, wiara, historia) sa z pliku danych; historia nie uzywa obecnego pana - tylko miejsc i faktow lore do
  298 AC, wiec nie klamie po zdobyciu zamku.
- Dlugosc zdan: zajecie i historia do 125 znakow, wiara do 110 (dymek zawija wiersz). Tylko ASCII. Bez slowa "hamlet".
- BannerKings: gdy wies gry ma tytul lordship u kogos innego niz glowa rodu pana, linia "Held in fief by {HERO}" idzie do naglowka
  menu okregu (rozdz. 7), nie do dymka - dymek zostaje krotki.

### 1.3 Przyklady z 8 krain

Dane: plik wgrany u Jeffa (2 449 wiosek), linie A po poprawce imion (rozdz. 3.2), linie B bez zmian. Ludzie = osady x 250.
Pan = wlasciciel na start kampanii ROT (w grze - biezacy). Stan i pamiec gry dopisane na probe tam, gdzie zaznaczono.

```
STONY HOLT                                                       [Polnoc; rod wymyslony]
A village of the Tumbledown lands, held by House Stark of Winterfell
Kept for them by Harwood Pinehill of Crossbeck
4,000 souls in 16 settlements
Hold Alt for more info
Brewers and beekeepers: barley ale and heather mead. A smithy on the road shoes the horses of passing riders.
Faith: the old gods of the forest, nameless and countless, who watch through the weirwoods' eyes.
Its fields were first given to a younger son of Winterfell, with a holdfast and the land around it.

HAZELPOOL                                                        [Dorzecze; rod lore Terrick, swobodny w okregu Tully]
A village of the Sherrer lands, held by House Tully of Riverrun
Kept for them by Lady Ravella Terrick of Hazelpool
4,000 souls in 16 settlements
Hold Alt for more info
Farmers and millers: wheat, barley and oats. The mill wheel turns on the Red Fork.
Faith: the Seven. Father, Mother, Warrior, Smith, Maiden, Crone and Stranger each have a candle in the sept.
Under the Storm Kings, and later the ironborn kings, the village paid tribute it never forgot.
In the last days of 298 AC men without banners burned Sherrer and the villages near it.   (pamiec z ksiazek)

WAVENESS                                                         [Zelazne Wyspy; rod lore Farwynd of Sealskin Point]
A village of the Sealskin Point lands, held by House Merlyn of Pebbleton
Kept for them by Adrack Farwynd of Sealskin Point
6,000 souls in 24 settlements
Thralls and farmers: barley and oats on thin, stony soil. Boats drawn up on the shingle, and salt fish for the markets.
Faith: the Drowned God. No temple: the drowned men baptise in the surf below the village.
The drowned men say the Grey King himself once walked this shore.

SUMMER MILL                                                      [Reach; rod lore Bridges, swobodny w okregu Tyrell]
A village of the Whitegrove lands, held by House Tyrell of Highgarden
Kept for them by Ser Guthor Bridges of Summer Mill
4,000 souls in 16 settlements
Farmers and millers: wheat, barley and apples. The mill wheel turns on the Mander.
Faith: the Seven. A roadside shrine bears the seven-pointed star; the sept is in Whitegrove.
Its men spent a year at the siege of Storm's End in Robert's war, and came home with nothing but stories.

GREENCREEK                                                       [Dolina; rod wymyslony]
A village of the Gull Point lands, held by House Grafton of Gulltown
Kept for them by Ser Godric Eaglecliff of Longhaven
4,000 souls in 16 settlements
Miners in the silver workings of the hills. A smithy on the road shoes the horses of passing riders.
Faith: the Seven. Women pray to the Mother in the sept, men to the Warrior and the Smith.
Its men rode under Jon Arryn's banners in 282 AC, when the Vale rose for Robert.

SALTSANDS                                                        [Dorne; rod wymyslony; stan i pamiec gry na probe]
A village of the Red Mines lands, held by House Dayne of Starfall
Kept for them by Ser Aron Bonewater of Saltsands
4,000 souls in 16 settlements
Burned 40 days ago. 300 of 4,000 souls have come back.
Silver miners and smelters. Salt fish and seaweed are dried along the shore.
Faith: the Seven. The sept is cool and dim inside, and the Mother's statue is veiled against the sun.
The sun is the village's oldest enemy; work stops at noon and begins again at dusk.
Burned by Lord Tarly's men in the summer of 300 AC; about 2,400 souls fled and 160 were killed.

KRYVEA                                                           [Volantis; rod wymyslony]
A village of the Gorcorys lands, held by House Maegyr of Volantis
Kept for them by Malaquo Vodoaros of Kryvea
4,000 souls in 16 settlements
Field slaves: wheat, barley and figs for the masters of Volantis. A landing on the river for barges and boats.
Faith: many gods. A red priest, himself a slave of the temple, tends the night fire.
Five slaves to every free man work these fields, and their faces bear the tattoos of their trade.

TIALO                                                            [Pentos; rod wymyslony]
A village of the Alatys lands, held by House Mopatis of Pentos
Kept for them by Bambarro Qhaloarys of Vinestrand
4,000 souls in 16 settlements
Fishers and net-makers: crabs, eels and mackerel.
Faith: many gods. The magisters' gods have temples in Pentos; the village makes do with a roadside altar.
The magisters' estates swallowed the village fields long ago; most here work another man's land.
```
(W kazdym dymku po linii ludzi / stanu jest "Hold Alt for more info"; skrocone od trzeciego przykladu.)

---

## 2. Proporczyk w barwach pana (pkt 2)

- **Co widac:** z bliska nad kazda wioska maly proporczyk na drzewcu z herbem pana okregu - ta sama choragiew, ktora gra stawia przy
  partiach w zamku i przy namiotach oblezenia. Z daleka znika razem z wioska.
- **Jak** [KOD, badanie C rozdz. 2]: osady gry same nie maja herbu na mapie; choragwie przy zamkach to choragwie partii.
  `MobilePartyVisual.GetBannerOfCharacter(banner, "campaign_flag")` (public static, SandBox.View - juz w referencjach W2) daje siatke z
  herbem. Osobna encja w scenie przy przednim narozniku obrysu wioski (strona drogi / wody), wysokosc z terenu, skala ok. 0.32
  (namiot gry 0.15-0.5). Bez fizyki i skryptow - nie lapie klikniec.
- **Tylko blisko kamery:** kamera z <= 90, najblizsze 80 proporczykow, budowa najwyzej 12 na cwierc sekundy; dalej schowane, nie
  usuwane. Gestosc na pliku 2 449 [ZMIERZONE C]: w promieniu 60 jest 41 / 66 / 87 wiosek (mediana / 90% / max) i najwyzej 18 okregow
  (= najwyzej 18 roznych herbow).
- **Zmiana pana i barw:** raz na sekunde dla pokazanych proporczykow porownanie KODU herbu (gra zmienia kolory herbu w miejscu, ta
  sama referencja `Banner` [KOD]); inny kod = przebudowa tej jednej flagi. Obejmuje zdobycie zamku, zmiane krolestwa i zmiane rodu
  panujacego bez osobnych nasluchow. Jeden obiekt `Banner` na kod herbu (50 wiosek jednego pana = jedna tekstura).
- **Po bitwie:** gra czysci tekstury herbow przy kazdym wejsciu w misje [KOD]; widok W2 juz ma `ClearVisualMemory` - tam kasujemy
  pamiec herbow i proporczyki buduja sie od nowa (inaczej pusta flaga).
- **Nic co klatke** (CLAUDE.md 7), nic do sejwu, wylacznik w MCM.
- [AUTOTEST]: czy "campaign_flag" na encji bez skryptow sie rysuje i czy plotno sie rusza; klatki przy kamerze z 30 / 60 / 90 i 0 /
  40 / 80 proporczykach (cel: spadek < 5%). Zapas bez plotna: siatka "map_banner".

---

## 3. Rycerze i rody (pkt 3)

### 3.1 Kto trzyma wioske - kolejnosc przydzialu (badanie A, `WL\skrypty\generator.py`)

Zrodlo: 474 rody z 12 szablonow krain AWOIAF (stan na poczatek A Game of Thrones), strony 471 rodow i 248 siedzib, czytane przez API
tej samej wiki. W `WL\rody.csv` 508 wierszy: 439 Westeros, 5 Daru, 64 Essos. 127 rodow Westeros to klany ROT - oni sa PANAMI okregow,
nigdy rycerzami wiosek. 43 rody wymarle ida tylko do historii i pamieci. Kandydatow na rycerza: 268 + rody Essos.

1. **Prawdziwy rod z siedziba w poblizu** (pewnosc A / A- / B / C: siedziba = osada ROT, wies ROT o tej nazwie, punkt z mapy AWOIAF
   przelozony na mape ROT, albo opis lore typu "sworn to Whent"): najblizsze wolne wioski tej samej krainy do 70 jedn. (Essos 40),
   pomniejszy lord 2 wioski, rycerz i inni 1. Wynik: 180 wiosek.
2. **Prawdziwy rod bez znanej siedziby** (pewnosc D, 146 rodow): wioska w okregu, ktorego pan w ROT nosi nazwisko pana z lore
   (np. Terrick -> okregi Tully). Wynik: 159 wiosek.
3. **Rod wymyslony w stylu krainy**: reszta, 1-2 sasiednie wioski (do 15 jedn.) jednego okregu na rod. Nazwa z czlonow krainy
   (Frost+holt, Rush+well, Sand+spring; Essos z sylab walyrianskich, ghiscarskich, Ibbenu, Yi Ti, Sarnoru), unikalna na calej
   mapie, omija rody lore (takze wymarle), ich siedziby, klany ROT i nazwy osad ROT. Wynik: 2 110 wiosek, 1 565 rodow.

Uzytych 278 z 314 rodow lore, ktore sie nadaja. Krainy: Polnoc 37 lore / 239 wymyslonych, Dorzecze 42 / 238, Dolina 32 / 113,
Zachod 41 / 98, Reach 72 / 196, Ziemie Burzy 27 / 70, Korona i Smocza Skala 37 / 2, Dorne 5 / 114, Wyspy 5 / 0, Essos 41 / 1 040.

### 3.2 Imie rycerza - POPRAWKA SYNTEZY (obowiazkowa przed L0)

Badanie A bierze dla rodu lore PIERWSZE imie z listy czlonkow na stronie AWOIAF. Ta lista idzie w kolejnosci strony, nie wedlug
roku 298 AC. Sprawdzone u zrodla [ZRODLO]: z 10 rodow z imieniem 9 daje osobe, ktora nie moze trzymac wioski w 299 AC:
Martyn Cassel (nie zyje), Ambrose Butterwell (lord z czasow Blackfyre'ow), Petyr Baelish (Littlefinger), Raymun Fossoway (209 AC),
Symon Hollard (zginal w 277 AC), Luthor Largent (Taniec Smokow), Jarman Buckwell (zwiadowca Nocnej Strazy), Oswald Wode (Taniec
Smokow), Rupert Falwell (Wiara Wojujaca). Tylko Lymond Goodbrook (glowa rodu w 298) jest dobry. W calym podgladzie podobnych jest
wiecej: Bowen Marsh, Othell Yarwyck, Ottyn Wythers (Nocna Straz), Meryn Trant, Boros Blount (Gwardia Krolewska), Ilyn Payne, Jeyne
Poole, Areo Hotah, Tobho Mott, Hizdahr Loraq, Skahaz Kandaq, Galazza Galare (kaplanka), Vardis Egen (zginal w 298), Jeyne Arryn
(zmarla w 134 AC), kobiety bez tytulu.

**Zasada:** rod zostaje prawdziwy (nazwisko, herb, siedziba, pan z lore), imie dobiera generator z listy imion krainy (ta sama
funkcja co dla rodow wymyslonych, deterministycznie z `uid`), z odrzuceniem kazdego zestawu "imie + nazwisko", ktory wystepuje w
lore (wszystkie listy czlonkow z `rody.csv`). Dotyczy 254 wiosek (202 rodow). Przyklady po poprawce: "Ser Guthor Bridges of Summer
Mill", "Adrack Farwynd of Sealskin Point", "Lady Ravella Terrick of Hazelpool". Z list imion generatora usunac imiona glownych
postaci Essos (Hizdahr, Skahaz, Kraznys, Galazza i podobne).

### 3.3 Tytul wedlug krainy
- "Ser" - Dorzecze, Dolina, Zachod, Reach, Ziemie Burzy, Korona, Dorne (takze dla rodu pomniejszego lorda - wioske trzyma rycerz
  tego rodu); Polnoc i Zelazne Wyspy bez "Ser" (rycerze na Polnocy sa rzadkoscia poza Manderlymi - lore); co 9. wioska "Lady"
  (imie z listy kobiecych); Dar "Brother" (dzis 0 wiosek); Ghis "Imie zo Rod"; Dothrakowie "Ko Imie"; Wolne Miasta bez tytulu.
- Seat w linii rycerza: siedziba rodu lore, jesli ROT albo lore ja zna, inaczej wioska (albo sasiednia wioska tego samego rodu).

### 3.4 Herb rycerza (do menu, rozdz. 7)
- Tekst herbu po angielsku: rody lore z AWOIAF (350 z 444 wierszy Westeros i Daru, np. "Three white hedgehogs on yellow" Wode),
  rody wymyslone z generatora z zasada metal na kolorze ("a silver elk on grey").
- **Obrazek herbu** w wierszu menu wymaga kodu herbu Bannerlorda (`knight_banner`). Plan L0: kod z dwoch barw (najblizsze kolory
  palety gry) i jednego godla z ikon gry (Native ok. 237 ikon, ROT 14, CCsBanners 728) wedlug slowa z herbu (wolf, lion, stag,
  fish, tower, tree, bridge, apple ...); kod sprawdzany w grze `Banner.IsValidBannerCode`. Brak kodu = herb pana w wierszu.
- Poprawka danych: w 156 okregach dwa rody wymyslone maja identyczny herb (bo FNV-1a z sola dopisana na koncu daje skorelowane
  reszty dla pola, barwy i godla), a 747 rodow Essos nie ma herbu wcale. Herb wymyslony ma byc unikalny w krainie (jak nazwa), a
  Essos dostaje herb z barw miasta i godla (lore Essos nie zna herbow, ale gra potrzebuje obrazka).

### 3.5 Zmiana pana
Rycerz siedzi na ziemi - po zdobyciu zamku zostaje, zmienia sie tylko linia pana i herb (gra, na zywo). "Kept for them" mowi wtedy
o nowym panu - zgodnie z lore (pomniejsze rody przechodza pod zwyciezce). BannerKings: jesli BK nada wies gry komus innemu (lordship,
`GrantKnighthood`), menu pokazuje "Held in fief by {HERO}" - to prawdziwy rycerz z ziemia w grze (refleksja, licznik potkniec).

---

## 4. Wiara i swiete miejsce (pkt 5; `WL\wiara.csv`, 51 wierszy mapy, 96 zdan)

Wiara wedlug krainy, z wyjatkami per siedziba i rzeka; zdanie wedlug klasy miejsca (wezel H z wlasnym septem / przy drodze R z
septonem z wsi gry / ubocze F z wedrownym septonem).

| Kraina | Wiara | Swiete miejsce (przyklady) |
|---|---|---|
| Polnoc | starzy bogowie; okregi White Harbor 75% i Ramsgate 60% Siedmiu (Manderly) | czardrzewo z twarza na bloni, slub przed drzewem serca, bez kaplanow |
| Dorzecze | Siedmiu, 12% z dawnym czardrzewem; okreg Raventree Hall 60% starzy bogowie | sept z dzwonem / drewniany sept / wedrowny septon dwa razy w roku; septria braci |
| Dolina, Ziemie Burzy | Siedmiu, 10% / 5% z czardrzewem | jak wyzej |
| Zachod, Reach, Korona | Siedmiu | jak wyzej |
| Smocza Skala | Siedmiu, 15% wzmianka o "czerwonym bogu" ze Smoczej Skaly | "the septon frowns" |
| Zelazne Wyspy | Utopiony Bog | bez swiatyn i idoli; topieni chrzcza w przyboju; kaplan z maczuga z drewna wyrzuconego przez morze |
| Dorne | Siedmiu; nad Greenblood 60% Matka Rhoyne | sept z piaskowca przy studni; Sieroty Greenblood |
| Wolne Miasta | wedlug miasta: Ksiezycowe Spiewaczki (Braavos), bog brodatych kaplanow (Norvos), Czarny Koziol (Qohor), Trios (Tyrosh), Placzaca Pani (Lys), R'hllor wsrod niewolnikow (Volantis) | kapliczki, nocny ogien czerwonych kaplanow |
| Zatoka Niewolnicza | bogowie Ghis; Lhazosh i Hesh - Wielki Pasterz | harpia nad drzwiami; swiatynia z gliny, "godswife" |
| Dothrakowie, Qarth, Yi Ti, Ibben, Sarnor, Wyspy Letnie | bog-kon (ksiazki: "horse god"), bogowie Qarthu, Lew Nocy i Panna ze Swiatla, bogowie Ibbenu, sto bogow Sarnoru, bog i bogini milosci | |

Uwagi syntezy: Lady of Spears (tylko Nieskalani) slusznie pominieta; "czerwony bog" na Smoczej Skali zaklada dwor Stannisa w 299 AC
- zgodne z ksiazkami, a gdyby Jeff nie chcial - waga 0 w `wiara.csv`. Na mapie 2 449: Siedmiu 891 wiosek, starzy bogowie 248, reszta
wedlug miast Essos.

---

## 5. Czym zyje wioska (pkt 4; `WL\zajecia.csv`, 69 zdan glownych + 19 zdan miejsca)

- **Rodzaj obrazka v4** (kolumna `model`: mill / windmill / farm / granary / fishing / village) wybiera zdanie glowne: mlynarze
  ("The mill wheel turns on the Red Fork."), wiatrak, spichlerz ("The great barn here holds the grain of the district."), rybacy,
  zagroda. Pozostale wioski - wedlug typu wsi gry z settlements.xml (23 typy: zboze, rybacy, drwale, srebro, zelazo, glina, winnice,
  len, oliwki, sol, bydlo, owce, swinie, konie ...). v4 trzyma `kind` = miejsce, `model` = rodzaj - kolizji kolumn nie ma
  (sprawdzone w naglowku v4).
- **Kto pracuje w polu wedlug lore:** wolni rolnicy w Westeros, thralle na Wyspach, "bond servants" w Pentos, niewolnicy w Lys, Myr,
  Tyrosh (3:1), Volantis (5:1) i Zatoce Niewolniczej, wolni w Braavos i Lorath.
- **Rozjazdy ROT poprawione tekstem:** 9 "winnic" na Polnocy = piwowarzy i pszczelarze (ale z jeczmienia, miod z wrzosu); "silk" w
  Westeros = farbiarze i tkacze cienkiego sukna (jedwab tylko Yi Ti).
- **Drugie zdanie od miejsca:** most (myto, kuznia), brod, skrzyzowanie (gospoda, targ), droga (karczma, kuznia, stacja), rzeka
  (przewoznicy, przystan), brzeg (lodzie, suszona ryba), jezioro; w Dorne i na poludniu Essos studnia dla karawan. Tylko gdy calosc
  <= 125 znakow.

---

## 6. Jedno zdanie historii (pkt 6; `WL\historia.csv`, 472 szablony)

- Kraina ma 15-30 szablonow ogolnych + do 11 przypietych do siedzib (bitwy, rody, ruiny). Filtry: miejsce (most, brod, droga ...),
  siedziba okregu (geografia - wies gry jest przypisana do zamku na stale), rzeka z nazwa (rubiny Rhaegara tylko nad Tridentem,
  Sieroty tylko nad Greenblood). Pula na wioske 8-20 szablonow (mediana 13); w jednym okregu szablon sie nie powtarza (wyjatek: 36
  wiosek w okregach wiekszych niz pula).
- Os czasu do 298 AC wlacznie (Era Switu, Andalowie, Wojny Rhoynarow, Podboj, Taniec Smokow, Redgrass, Summerhall, Rebelia Roberta,
  Rebelia Greyjoya, khalasar Drogo pod Pentos). **Nic z Wojny Pieciu Kroli** - ta dzieje sie w grze.
- Pochodzenie: 314 szablonow z lore (strona AWOIAF w kolumnie `source`), 97 [STYL] (szczegol w stylu krainy), 61 [WYM] (scenka
  wiejska bez nazw i dat). [STYL] i [WYM] = projekt, nie lore - Jeff moze je przejrzec jak nazwy wiosek.
- Najczestszy szablon trafia do 71 wiosek (2.9% mapy); tylko 4 z 418 okregow maja dwie wioski z identycznymi trzema zdaniami.
- Data Zaglady Valyrii sporna (102 / 114 BC) - tekst mowi "four hundred years ago".

---

## 7. Menu okregu (pkt 8)

- **Gdzie:** opcja **"Look over the district"** (tekst juz jest w W2, `arm_dist_option`) w menu `village` (za "Take a walk through
  the lands") i `village_looted`. Oba to zwykle menu gry, nie menu oczekiwania [KOD] - pulapka `SwitchToMenu` z CLAUDE.md 7 nie
  dotyczy, a projekt i tak nie przelacza menu wcale.
- **Okienko wyboru gry** (`ShowMultiSelectionInquiry`) [KOD]: wiersz ma obrazek (herb) i wlasny dymek - bez wlasnego prefabu.

```
Villages of the Sherrer district                                   (arm_dist_title, juz jest)
Held by House Tully of Riverrun. Choose a village to hear more of it.
Held in fief by Ser X.                                              (tylko BK, gdy lordship u kogos innego)

[herb Terrick]   Hazelpool - on the river, 4,000 souls - burned, 300 of 4,000 souls back
[herb Chambers]  Greyhurst - on the river, 4,000 souls - standing
[herb Tully]     Sherrer (the district village) - 4,250 souls - looted, smoke still rising
                                                              [Hear more]   [Leave]
```
  - Dymek wiersza: rycerz + herb tekstem ("Kept for them by Lady Ravella Terrick. Arms: per saltire purple and gold, four hawks'
    heads countercharged."), czym zyje, pamiec wojny.
  - Rodzaj miejsca w wierszu: "by the bridge", "at the crossroads", "on the road", "on the river", "on the coast", "by the lake",
    "among the fields"; rodzaj v4: "(the mill)", "(the granary)" itd. gdy jest.
  - Stan: W2 (dzis) - standing / burning; W3 (po 113) - burned / half burned / rebuilding / abandoned z liczbami ksiegi.
  - Wierszy na okreg: mediana 5, 90% 11, najwiecej 19 + wies gry (lista przewijana, wyszukiwarka przy > 10).
- **"Hear more"** -> okienko z pelnym tekstem: nazwa, linia pana, rycerz z herbem tekstem, ludzie, czym zyje, wiara, historia,
  pamiec wojny; przycisk "Back to the villages" otwiera liste od nowa. Bezpieczne: akcja okienka jest wolana przed jego zamknieciem,
  nowe okienko idzie do kolejki gry [KOD].
- Zero stanu i zero zapisu. Menu potrzebuje wspolnej statycznej kopii pliku wiosek (dzis plik czyta tylko widok mapy przy kazdym
  `OnInitialize`) - jedno wczytanie na sesje.

---

## 8. Pamiec wojny (pkt 7; po ksiedze ludzi 108-113)

- **Dlaczego wlasny zapis:** gra pamieta sprawce rabunku (`VillageStateChangedLogEntry`), ale wpis ginie po 7 dniach; ksiega 113
  trzyma tylko sumy okregu [KOD]. "Kto i kiedy" trzeba zapisac samemu - CLAUDE.md "nic do sejwu bez potrzeby" jest spelnione.
- **Skad dane:** `CampaignEvents.VillageStateChanged` - sprawca i dzien (Looted = spalona, przerwany rabunek = raided); liczby ludzi
  - wpiecie w koniec rabunku 113 (`Devastation.EndFinalizer`: 2 pola w `RaidState`, 2 dodawania w `Strike` obok `sc.Away += ...`,
  wywolanie `WarMemory.NoteRaidPeople`). Kolejnosc w jednym rabunku sprawdzona w kodzie: Looted -> RaidCompleted -> 113 dopisuje
  liczby do rekordu tej samej doby.
- **Ktora wioska:** W2 - wioska, na ktorej stal ogien (pierwsza wioska lancucha); W3 - wioska z planu kroku ksiegi wiosek.
- **Zapis:** sekcja `m:` wspolnego klucza `arm_district` (ten sam klucz co W3; kazda paczka czyta swoje sekcje): ostatnie zdarzenie
  okregu + licznik spalen, ok. 70 B na okreg (wojna w kilkudziesieciu okregach 2-5 KB, wszystkie 571 ok. 50 KB). Wlasny try przy
  `SyncData`; smieci i stare zapisy pomijane wiersz po wierszu. Proba poza gra 6 / 6 (badanie C).
- **Teksty (EN):**
  - "Burned by {RAIDER} in the {SEASON} of {YEAR} AC; about {FLED} souls fled and {DEAD} were killed."
  - "Raided by {RAIDER} in the {SEASON} of {YEAR} AC; about {FLED} souls fled."
  - bez 113: "Burned by {RAIDER} in the {SEASON} of {YEAR} AC."
  - "It has been burned {TIMES} times in living memory." (od 2)
  - `{RAIDER}`: "Ser Gregor Clegane's men" (bohater), "your men" (gracz), "outlaws" (bandyci), "the dead" (armia Innych ROT),
    "men without banners" (brak bohatera). Liczby zaokraglone ("about 600 souls"). Pora roku i rok z kalendarza Armoury (364 dni).
- **Pamiec z ksiazek** (kolumna `war_lore`, bez zapisu): pokazywana, dopoki gra nie zapisze wlasnej. Tylko tam, gdzie lore zna
  zdarzenie tuz przed startem ROT (299 AC): okregi Sherrer, Mummer's Ford, Wendish Town - ludzie bez choragwi, koniec 298 AC
  [ZRODLO: Raid on Sherrer, "late 298 AC"]. Tekst: "In the last days of 298 AC men without banners burned {DISTRICT} and the villages
  near it." Reszta wiosek: brak linii (historia z pkt 6 juz mowi o dawnych wojnach).

---

## 9. Sprawdzenie syntezy

### 9.1 Lore u zrodla - 12 rodow [ZRODLO, API awoiaf.westeros.org, 08.10]

| Rod | Siedziba / pan / herb u zrodla vs `rody.csv` | Imie z badania A | Wynik |
|---|---|---|---|
| Goodbrook | brak siedziby, pan Tully (AGOT), herb zgodny | Lymond - glowa rodu | OK |
| Butterwell | Whitewalls "formerly", pan Tully, herb zgodny | Ambrose - lord sprzed stu lat | imie ZLE; siedziba drobnostka |
| Hersy | Newkeep, Arryn, herb zgodny | brak (generator) | OK |
| Baelish | wieza na Palcach (Drearfort), Arryn, herb zgodny | Petyr - Littlefinger | imie ZLE |
| Fossoway of New Barrel | New Barrel, Tyrell, herb zgodny | Raymun - postac z 209 AC | imie ZLE |
| Cassel | brak siedziby, Stark, herb zgodny | Martyn - nie zyje | imie ZLE |
| Largent | Korona, brak herbu i pana (puste w CSV - zgodnie) | Luthor - Taniec Smokow | imie ZLE |
| Hollard | Hollard castle, Darklyn, herb zgodny; **rod prawie wymarly po 277 AC, zamek w ruinie** | Symon - zginal w 277 | rola ZLE, imie ZLE |
| Buckwell | Antlers, Iron Throne / Baratheon, herb zgodny | Jarman - zwiadowca Nocnej Strazy | imie ZLE |
| Wode | rycerze z ziemia pod Whentami, blisko Sow's Horn, herb zgodny | Oswald - Taniec Smokow | imie ZLE |
| Falwell | Lannister, herb zgodny | Rupert - Wiara Wojujaca | imie ZLE |
| Bridges | Tyrell, herb zgodny | brak (generator) | OK |

Wynik: siedziba, pan i herb 12 / 12 (Butterwell - Whitewalls tylko "dawniej"), rola 11 / 12 (Hollard), imie 1 / 10 -> poprawka 3.2.

### 9.2 Historia u zrodla - 16 faktow [ZRODLO]
Zgodne (14): Honeywine 130 AC, lupiez Lannisportu 130 AC, Spicetown 130 AC, Tumbleton 130 AC, Rook's Rest 129 AC, Kingswood
Brotherhood rozbite w 281 AC, Kamienne Wrony zabijaja ojca i braci Jeyne Arryn w 97 AC, Pentos poswieca czterech ksiazat w jednym
roku wojny zakonczonej w 209 AC, zaraza 209 AC "worst of all in King's Landing" (4 na 10) i gorsza w Oldtown, Corbrayowie zdobywaja
Palce za Andalow, sekta Boasha z Valyrii zaklada Lorath w labiryntach Mazemakerow, Ashford - straz przednia Tarly'ego, rajd na
Sherrer pod koniec 298 AC, Summerhall - trzy bitwy jednego dnia.
Bledny (1): westerlands_11 "The Great Spring Sickness ... came out of Lannisport" - zrodlo mowi tylko, ze w Lannisporcie byla
ciezka. Niepotwierdzony (1): "seven families in ten fish" wedlug archmaestra Haerega (zajecia Wysp i iron_09) - brak na stronach
Iron Islands, Ironborn i Haereg.

### 9.3 Sensownosc tekstow (cala mapa)
- Powtorzenia w jednym dymku: 124 z 2 449 wiosek (v4: 137 z 2 446) ma to samo slowo w dwoch liniach; realne powtorki tej samej
  rzeczy (karczma, wozy, przystan, barki, rybacy, most, labirynt) ok. 90 wiosek (ok. 4%), np. "Carters and drovers meet at the
  crossroads inn." + "The village grew along the road one alehouse at a time; carters still stop here...", Lorath: Slepy Bog w wierze
  i w historii. Reszta to "sept" w wierze i historii - dopuszczalne.
- Inne: north_10 "The Andals never came north of the Neck" przeczy north_22 (statki Andalow na Weeping Water) -> "never conquered";
  dragonstone_12 "Its lord was once a smuggler" zalezy od obecnego pana (zasada historii) -> przepisac na fakt o ziemi.
- Ryby na Zelaznych Wyspach bywaja rzeczne (eels, pike, trout) w pliku wgranym (przyblizenie rodzaju) - v4 `model` + miejsce
  "coast" daje ryby morskie; sprawdzic po przeliczeniu.
- Wlasciciele ROT bywaja dziwni (Astapor - Mormont, Mantarys - Targaryen, Harroway - Tully): to stan gry, dymek ma go pokazac.

### 9.4 Wykonalnosc techniczna (badanie C, sprawdzone w kodzie gry 1.4.8)
- Szkice skompilowane na DLL gry: 0 bledow. Sygnatury potwierdzone: `RegisterTooltip` z delegatem kontrawariantnym,
  `PropertyBasedTooltipVM`, `BannerImageIdentifierVM`, `MobilePartyVisual.GetBannerOfCharacter`, `MultiSelectionInquiryData`,
  `InquiryElement` z obrazkiem, `CampaignEvents.VillageStateChanged`. Proba zapisu pamieci 6 / 6.
- Herb w dymku = TYLKO wlasny prefab (zwykly dymek gry nie umie obrazka; `<img>` dziala tylko dla sprite'ow). Pulapka delegata
  (`Action<PropertyBasedTooltipVM, object[]>`, inaczej pusty dymek bez bledu) opisana i obejsta w szkicu.
- Nowe w instalacji: katalog `Modules\Armoury\GUI\Prefabs` (dzis go nie ma). Nowych referencji brak.
- Wpiecia w W2 tylko opisane (C rozdz. 8) - do scalenia z wersja agenta, ktory teraz zmienia wyglad.
- Dane: plik lore z naglowkiem `layout_crc32` - inny uklad w grze = lore wylaczone, dymek wraca do dzisiejszego. Konieczne, bo
  `uid` zmienia sie przy przestawieniu wioski (v4 ma tylko 687 z 2 446 `uid` wspolnych z plikiem wgranym, nazw wspolnych 1 783).

---

## 10. Poprawki danych przed zamrozeniem (L0; tylko Python w WL, zero zmian w modzie)

| Nr | Poprawka | Gdzie |
|---|---|---|
| P1 | Imie rycerza rodu lore z listy krainy, z odrzuceniem zestawow z lore; z list imion usunac glowne postacie Essos | `skrypty\generator.py` |
| P2 | Hollard -> `wymarly_historia` (zamek w ruinie od 277 AC); przejrzec rody z dopiskiem "nearly extinct" / "formerly" | `skrypty\build_rody.py` |
| P3 | Herby wymyslone unikalne w krainie; jeden hash dzielony na pole / barwe / godlo zamiast trzech soli na koncu | `generator.py gen_arms` |
| P4 | Herb dla Essos (barwy miasta + godlo) i kody herbow Bannerlorda `knight_banner` (palette + ikony Native / CCs) | nowy krok L0 |
| P5 | Linia rycerza "Kept for them by {KNIGHT}" zamiast "{KNIGHT}, sworn to House X" | `generator.py` |
| P6 | `lands` na okreg: "the X lands" albo "the lands of X" (32 okregi) | L0 |
| P7 | Historia omija szablon o tym samym temacie co zdanie zajecia / miejsca / wiary (tagi: inn, carters, landing, barges, fishers, bridge, maze, Blind God ...) | `b-skrypty\b_dymki.py` |
| P8 | westerlands_11: "struck Lannisport hard" zamiast "came out of Lannisport" | `b_historia.py` |
| P9 | Usunac odwolanie do Haerega ("seven families in ten") z zajec Wysp i iron_09 albo oznaczyc [STYL] | `b_wiara_zajecia.py`, `b_historia.py` |
| P10 | north_10 "never conquered the North"; dragonstone_12 bez "its lord" | `b_historia.py` |
| P11 | `war_lore` dla okregow Sherrer, Mummer's Ford, Wendish Town | L0 |
| P12 | Generator A czyta plik ukladu z argumentu (jak B), nie ze stalej sciezki; A i B liczone na ostatecznym v4 | `generator.py` |
| P13 | Jeden plik `arm_map_village_lore.tsv` (A + B po `uid`) z `layout_crc32` ostatecznego ukladu; kontrola: pokrycie 100%, 0 wstawek, ASCII, dlugosci | L0 |

---

## 11. Kolejnosc paczek (numery nada skladanie; L = robocze)

| Krok | Paczka | Pkt | Co zobaczy gracz | Wymaga | Sejw | Proba |
|---|---|---|---|---|---|---|
| L0 | **Dane lore** (Python, offline): P1-P13 -> `Armoury\ModuleData\arm_map_village_lore.tsv` | 3-6 | nic | zamrozony v4 | - | kontrole L0; Jeff moze przejrzec [STYL] / [WYM] |
| L1 | **Dymek z herbem**: typ dymka + prefab (NOWY `GUI\Prefabs`), linia pana na zywo, rycerz, Alt (zajecie, wiara, historia, `war_lore`); MCM off = dzisiejszy dymek z linia pana w kolorze rodu | 1, 3-6 | herb, pan, rycerz, opis wioski | W2 (po pracy nad wygladem), L0 | nic | autotest: prefab, herb w warstwie dymka, Alt, tlo wrog / swoi, dlugie nazwy |
| L2 | **Proporczyki** | 2 | flagi pana nad wioskami z bliska | W2 | nic | autotest: klatki 0 / 40 / 80 flag, powrot z bitwy, zdobycie zamku |
| L3 | **Menu okregu** (stan z W2: standing / burning) | 8 | "Look over the district", lista z herbami, "Hear more" | L0, L1 (teksty) | nic | autotest: menu `village` i `village_looted`, herb w ramce 86 x 63, inne opcje BK / ROT |
| (108-113) | Ksiega ludzi - grupy 3, 4, 5 wedlug STAN-PRAC | - | (ich opisy) | (ich kolejnosc) | (ich) | (ich) |
| L4 | **Pamiec wojny** (sekcja `m:` w `arm_district`, wpiecie w `Devastation.EndFinalizer`) | 7 | "Burned by ... ; about 600 souls fled ..." w dymku i menu | 113 wgrane i sprawdzone | `arm_district` `m:` | autotest: rabunek AI -> rekord, zapis -> wczytanie, rozmiar klucza |
| W3+ | Stany menu z ksiegi wiosek (burned / rebuilding ...) | 8 | liczby w menu | W3 | (W3) | (W3) |

Zalecenie: L0 od razu (nie dotyka gry Jeffa); L1 i L2 jako jedna proba wygladu, ale z osobnymi wylacznikami (proporczyk ma
najwieksze ryzyko klatek); L3 zaraz po nich; L4 dopiero po 113 - zgodnie z decyzja "po ksiedze ludzi". Kazda paczka: autor +
niezalezny recenzent, build kod 0, kontrola calosci (CLAUDE.md 8.0), wpis CHANGELOG, wylacznik w MCM, wgranie tylko na "wgraj".

MCM (po angielsku, `python tools/gen_mcm.py`): Map Village Tooltip Herb (on), Map Village Pennants (on), Map Village Pennant Max
Camera Height (90), Map Village Pennant Max (80), District Menu (on), War Memory (on).

---

## 12. Ryzyka

1. **Prefab dymka** - Gauntlet go jeszcze nie widzial: czy sie laduje, czy herb rysuje sie w warstwie dymka. Zly prefab = brak
   dymka, nie wywrotka (gra laduje w try/catch). Zapas: MCM off = dzisiejszy dymek z linia pana w kolorze rodu. [AUTOTEST]
2. **Proporczyki** - plotno "campaign_flag" x 80 moze kosztowac klatki; zapas "map_banner" bez plotna, nizszy sufit. [AUTOTEST]
3. **Puste flagi po bitwie** - gra czysci tekstury herbow; przebudowa w `ClearVisualMemory` musi zadzialac. [AUTOTEST]
4. **Dane a uklad** - `uid` v4 rozni sie od wgranego w 72% wiosek; lore musi byc liczone na ostatecznym v4. Bramka `layout_crc32`
   chroni przed pomieszaniem (zle dane = brak opisu, nie zly opis).
5. **Imiona i herby rycerzy** - bez P1 dymek pokaze martwych i glowne postacie; bez P3 / P4 menu pokaze dwa identyczne herby
   obok siebie albo herb pana w kazdym wierszu.
6. **Kody herbow z blazonow** (P4) - mapowanie slow na ikony jest przyblizone; kod musi przejsc `Banner.IsValidBannerCode`, ikony
   CCsBanners tylko gdy mod jest w zestawie (jest u Jeffa). Brak kodu = herb pana.
7. **Menu** - inne mody (BK, ROT) dokladaja opcje do `village`; czy nasz index nie przesuwa ich opcji - niesprawdzone. Herb w ramce
   86 x 63 moze wyjsc rozciagniety. [AUTOTEST]
8. **Pamiec** - dodaje sekcje do sejwu; cofniecie DLL gubi ja (bez szkody dla reszty). Liczby ludzi tylko z 113.
9. **BK przez refleksje** - zmiana API BK = znika linia "Held in fief by" (licznik potkniec, bez wywrotki).
10. **Teksty [STYL] / [WYM]** (158 z 472 historii i czesc wiary) to projekt, nie lore - mozliwe drobne zgrzyty; lista w kolumnie
    `source`, poprawka = wiersz w Pythonie, bez zmian w modzie.
11. **Scalenie z W2** - inny agent zmienia teraz `MapVillagesView.cs`; wpiecia L1-L3 sa opisane (C rozdz. 8), nie wpisane.
12. **AWOIAF blokuje curl / WebFetch** (Cloudflare) - ponowne pobranie danych tylko przez przegladarke (API MediaWiki w karcie).

---

## 13. Dane i pliki (wszystko w WL, tylko odczyt dla moda do czasu L0)

| Plik | Co |
|---|---|
| `rody.csv` | 508 rodow (Westeros 439, Dar 5, Essos 64): kraina, ranga, rola, siedziba, x / y na mapie ROT, osada ROT, pewnosc A / A- / B / C / D, pan lore 298 AC, klan ROT, herb EN, barwy (+HEX), imiona, zrodlo AWOIAF |
| `podglad_rycerze.tsv` | przydzial na 2 449 wiosek (linia pana i rycerza - wersja PRZED P1 / P5) |
| `A.md` | raport badania A |
| `skrypty\generator.py`, `build_rody.py`, `build_out.py`, `rot_dump.py`, `kalibracja.txt`, `gen_stat.txt` | generator rodow i rycerzy, budowa `rody.csv`, zrzut ROT, kalibracja map AWOIAF -> ROT |
| `surowe\awoiaf_houses_1..3.tsv`, `surowe\awoiaf_seats.tsv` | surowy wyciag z AWOIAF |
| `wiara.csv`, `zajecia.csv`, `historia.csv` | tabele zdan B (RFC 4180, UTF-8) |
| `arm_map_village_lore-B.tsv` | zdania B dla 2 449 wiosek (`#uid livelihood faith history`, `layout_crc32: 86c412f5`) |
| `wioski-lore-przypis.tsv` | kazda wioska z kraina, miejscem, klasa, wiara, pula historii - do przegladu okiem |
| `dymki-przyklady.txt`, `v4-proba\*` | 20 dymkow; to samo na v4 (crc32 `2594a95d`, kolumna `model`) |
| `B.md`, `b-skrypty\b_dane.py`, `b_wiara_zajecia.py`, `b_historia.py`, `b_dymki.py` | raport i skrypty B (przebieg ok. 5 s) |
| `TECHNIKA.md`, `szkice\*.cs`, `szkice\GUI\Prefabs\ArmMapVillageTooltip.xml`, `szkice\_proba_kompilacji\*` | badanie C: technika, szkice kodu, prefab, proba kompilacji i zapisu |

**Kontrakt pliku dla moda (po L0):** `Armoury\ModuleData\arm_map_village_lore.tsv`, UTF-8, jeden wiersz na wioske:
```
# Armoury map village lore (A + B). Generated, do not edit.
# layout_crc32: <crc32 arm_map_villages.tsv>
#uid  lands  knight  knight_house  knight_arms  knight_banner  livelihood  faith  history  war_lore
```
- `lands` "the Tumbledown lands"; `knight` "Ser Guthor Bridges of Summer Mill" (z tytulem); `knight_house` "House Bridges";
  `knight_arms` herb tekstem EN (moze byc pusty); `knight_banner` kod herbu Bannerlorda (moze byc pusty -> herb pana);
  `livelihood`, `faith`, `history` - zdania B; `war_lore` - pamiec z ksiazek (zwykle pusta).
- Kolejnosc kolumn wedlug naglowka, brak kolumny = puste. Pan, herb pana i siedziba pana NIE sa w pliku - zawsze z gry.
- Mod niczego nie losuje i nie liczy hasha - tylko czyta gotowe zdania.

---

## 14. Zrodla

A Wiki of Ice and Fire, https://awoiaf.westeros.org/index.php/<strona> (dzis przez API MediaWiki w przegladarce):
House_Goodbrook, House_Butterwell, House_Hersy, House_Baelish, House_Fossoway_of_New_Barrel, House_Cassel, House_Largent,
House_Hollard, House_Buckwell, House_Wode, House_Falwell, House_Bridges, Martyn_Cassel, Jarman_Buckwell, Symon_Hollard, Oswald_Wode,
Lymond_Goodbrook, Rupert_Falwell, House_Corbray, Great_Spring_Sickness, Prince_of_Pentos, Lorath, Boash, Haereg, Iron_Islands,
Ironborn, Battle_of_the_Honeywine, Sack_of_Lannisport, Spicetown, Stone_Crows, Kingswood_Brotherhood, Battle_of_Ashford,
First_Battle_of_Tumbleton, Battle_of_Rook's_Rest, Battles_at_Summerhall, Raid_on_Sherrer, Sherrer, Mummer's_Ford, Wendish_Town.
Pelne listy stron badan A i B: `WL\A.md` rozdz. 1, `WL\B.md` rozdz. 10; zrodla techniczne (dekompilacja gry 1.4.8):
`WL\TECHNIKA.md` (numery linii przy kazdym ustaleniu).
